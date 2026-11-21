// AdeeboMeter phone remote - the relay. It only passes sealed messages between one PC and one phone.
//
//  - It never sees a key: messages are encrypted end to end (ECDH P-256, AES-256, HMAC-SHA256) with a secret that only
//    the QR code carries. To this relay they are random bytes.
//  - Nothing is stored: no database, no KV, no logs of content. A room lives in memory while two sockets are open; the
//    only things written are the room's close-alarm time and, on each socket, its role and a few timestamps.
//  - The room name is the only gate to the relay (L-24, by design): whoever has it can take a side, but the messages are
//    sealed with keys only the QR code carries, so a stranger who grabs a side first only blocks the session (the PC
//    then shows it couldn't connect and makes a new code) - they can never read or send anything.
//  - A room holds exactly one PC and one phone. A second phone is refused. Rooms close after 30 minutes, after 2 minutes
//    with no phone, or as soon as either side leaves.
//  - Limits: 64 KB per message, 40 messages a second per side, room names must be 22 random characters. From one
//    address, at most 6 rooms opened a minute (60 an hour) (Adeeb, 17 Nov: "shouldn't open more than 5-6") and 20 tries to join
//    one - a remembered phone looks through up to 10 rooms to find where its PC is waiting, so joining needs more.
//
// Hibernation (Adeeb, 17 Nov: "go with 3rd"): sockets are held by Cloudflare, not by this code, so a room with nobody
// talking - a PC waiting for its remembered phone, a quiet session - sleeps and isn't billed for the time. It wakes for
// each message in a few milliseconds. Because a sleeping room forgets its memory, everything a room needs to know
// (when it started, whether it's a remembered phone's room, each side's message rate) rides on the sockets themselves.
//
//   PC ══ socket ══►  room (asleep: free)  ◄══ socket ══ phone
//                     message arrives → wakes, forwards, sleeps again
//
// Deploy (your step): `npx wrangler deploy` in this folder, then put the address in Remote.Relay (src/Remote/Remote.cs)
// and RELAY in docs/remote.html.

const ROOM = /^[A-Za-z0-9_-]{22}$/;
const MAX_MSG = 64 * 1024, MAX_RATE = 40, LIFE_MS = 30 * 60 * 1000, WAIT_MS = 2 * 60 * 1000, LONG_WAIT_MS = 25 * 60 * 1000;   // long: a remembered phone's room
const ORIGINS = ["https://adeebometer.com", "https://www.adeebometer.com"];
const PER_MIN = { pc: 6, phone: 20 };   // a minute from one address (best effort: counted per Cloudflare location, in memory)
const PER_HOUR = { pc: 60, phone: 240 };  // bug-hunt L-242: and an hour - a steady 6 a minute all day isn't a person
const recent = new Map();

function tooMany(ip, role) {
  const now = Date.now(), w = Math.floor(now / 60000), h = Math.floor(now / 3600000), k = role + "|" + ip;
  let r = recent.get(k);
  if (!r || r.w !== w) {
    // bug-hunt ML-20: a full table drops stale counts (and then the oldest), never everyone's current count at once
    if (!r && recent.size > 5000) { for (const [key, x] of recent) if (x.w < w - 1 && x.h !== h) recent.delete(key); let over = recent.size - 4000; for (const key of recent.keys()) { if (over-- <= 0) break; recent.delete(key); } }
    // bug-hunt L-241: the last minute still counts, by how much of it is inside the last 60 s - no 2x burst across the boundary
    const prev = r && r.w === w - 1 ? r.n : 0;
    r = { w, n: 0, prev, h, hn: r && r.h === h ? r.hn : 0 }; recent.set(k, r);
  }
  r.n++; r.hn++;
  const inWindow = r.n + r.prev * (1 - (now % 60000) / 60000);
  return inWindow > PER_MIN[role] || r.hn > PER_HOUR[role];
}

export default {
  async fetch(req, env) {
    const url = new URL(req.url);
    const m = url.pathname.match(/^\/room\/([^/]+)$/);
    if (!m || !ROOM.test(m[1])) return new Response("not found", { status: 404 });
    if (req.headers.get("Upgrade") !== "websocket") return new Response("websocket only", { status: 426 });
    const role = url.searchParams.get("role");
    if (role !== "pc" && role !== "phone") return new Response("bad role", { status: 400 });
    // a phone page must come from the real site; the PC app sends no Origin
    const origin = req.headers.get("Origin");
    if (role === "phone" && !ORIGINS.includes(origin)) return new Response("forbidden", { status: 403 });
    if (role === "pc" && origin) return new Response("forbidden", { status: 403 });
    // bug-hunt ML-126: Cloudflare's rate-limit binding counts across all its machines; the in-memory count stays as the fallback
    const cip = req.headers.get("CF-Connecting-IP") || "";
    if (env.LIMITER) { try { const { success } = await env.LIMITER.limit({ key: role + "|" + cip }); if (!success) return new Response("slow down", { status: 429 }); } catch (e) {} }
    if (tooMany(cip, role)) return new Response("slow down", { status: 429 });
    const id = env.ROOMS.idFromName(m[1]);
    return env.ROOMS.get(id).fetch(req);
  },
};

export class Room {
  constructor(state) { this.state = state; this.done = false; }

  // each socket carries { role, born, long, n, t }: born and long live on the PC's socket, which is there for the
  // room's whole life; n and t are that side's message count in the current second
  side(role) { const l = this.state.getWebSockets(role); return l.length ? l[0] : null; }
  info(ws) { try { return ws.deserializeAttachment() || {}; } catch (e) { return {}; } }
  room() { const pc = this.side("pc"); return pc ? this.info(pc) : null; }

  async fetch(req) {
    const u = new URL(req.url), role = u.searchParams.get("role"), long = role === "pc" && u.searchParams.get("wait") === "long";
    // bug-hunt L-23: "finished" is also written down for 30 minutes, so a room that slept and woke can't be reused either
    if (this.done || await this.state.storage.get("done")) return new Response("expired", { status: 410 });
    const now = Date.now(), pc = this.side("pc"), phone = this.side("phone");
    if (role === "pc" ? pc : phone) return new Response("taken", { status: 409 });   // one PC, one phone
    if (role === "phone" && !pc) return new Response("no pc", { status: 404 });
    let alarm;
    if (role === "phone") {
      const r = this.info(pc);
      if (now - r.born > (r.long ? LONG_WAIT_MS : WAIT_MS)) return new Response("expired", { status: 410 });
      r.born = now; pc.serializeAttachment(r);   // the 30 minutes start when the phone joins - for a code too (bug-hunt ML-110)
      alarm = r.born + LIFE_MS;
    } else alarm = now + (long ? LONG_WAIT_MS : WAIT_MS);
    const [client, server] = Object.values(new WebSocketPair());
    this.state.acceptWebSocket(server, [role]);
    server.serializeAttachment(role === "pc" ? { role, born: now, long, n: 0, t: now } : { role, n: 0, t: now });
    // the room's clock: no phone within 2 minutes (25 for a remembered phone), or 30 minutes in all, and it closes
    await this.state.storage.setAlarm(alarm);
    return new Response(null, { status: 101, webSocket: client });
  }

  async webSocketMessage(ws, data) {
    if (this.done) return;
    const me = this.info(ws), now = Date.now();
    if (now - me.t > 1000) { me.t = now; me.n = 0; }
    const size = typeof data === "string" ? new TextEncoder().encode(data).length : data.byteLength;   // L-25: bytes, not UTF-16 units
    const r = this.room(), live = !!this.side("phone");
    if (++me.n > MAX_RATE || size > MAX_MSG || !r || (live && now - r.born > LIFE_MS)) { await this.end(); return; }
    try { ws.serializeAttachment(me); } catch (e) { await this.end(); return; }   // L-26: a lost count would let the rate check reset
    const o = this.side(me.role === "pc" ? "phone" : "pc");
    if (o) try { o.send(data); } catch (e) { await this.end(); }
  }

  async webSocketClose() { await this.end(); }
  async webSocketError() { await this.end(); }

  async alarm() {
    if (await this.state.storage.get("done")) { try { await this.state.storage.deleteAll(); } catch (e) {} return; }   // the "finished" mark's 30 minutes are up
    const r = this.room(), phone = this.side("phone");
    if (!r || !phone || Date.now() - r.born >= LIFE_MS - 1000) await this.end();
    else await this.state.storage.setAlarm(r.born + LIFE_MS);
  }

  async end() {
    if (this.done) return;   // once: closing a socket twice can leave the other side never hearing it close
    this.done = true;
    for (const s of this.state.getWebSockets()) try { s.close(1000, "session over"); } catch (e) {}
    try { await this.state.storage.deleteAll(); } catch (e) {}
    // only a "finished" mark is kept, for 30 minutes, then the alarm deletes that too - nothing else is left behind
    try { await this.state.storage.put("done", Date.now()); await this.state.storage.setAlarm(Date.now() + LIFE_MS); } catch (e) {}
  }
}
