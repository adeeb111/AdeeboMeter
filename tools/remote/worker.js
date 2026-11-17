// AdeeboMeter phone remote - the relay. It only passes sealed messages between one PC and one phone.
//
//  - It never sees a key: messages are encrypted end to end (ECDH P-256, AES-256, HMAC-SHA256) with a secret that only
//    the QR code carries. To this relay they are random bytes.
//  - Nothing is stored: no database, no KV, no logs of content. A room lives in memory while two sockets are open; the
//    only things written are the room's close-alarm time and, on each socket, its role and a few timestamps.
//  - A room holds exactly one PC and one phone. A second phone is refused. Rooms close after 30 minutes, after 2 minutes
//    with no phone, or as soon as either side leaves.
//  - Limits: 64 KB per message, 40 messages a second per side, room names must be 22 random characters, and at most
//    30 new rooms a minute from one address.
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
const NEW_PER_MIN = 30;   // new rooms a minute from one address (best effort: counted per Cloudflare location, in memory)
const recent = new Map();

function tooMany(ip) {
  const now = Date.now(), w = Math.floor(now / 60000);
  const r = recent.get(ip);
  if (!r || r.w !== w) { if (recent.size > 5000) recent.clear(); recent.set(ip, { w, n: 1 }); return false; }
  return ++r.n > NEW_PER_MIN;
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
    if (tooMany(req.headers.get("CF-Connecting-IP") || "")) return new Response("slow down", { status: 429 });
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
    if (this.done) return new Response("expired", { status: 410 });   // a finished room can't be reused (while it's in memory)
    const now = Date.now(), pc = this.side("pc"), phone = this.side("phone");
    if (role === "pc" ? pc : phone) return new Response("taken", { status: 409 });   // one PC, one phone
    if (role === "phone" && !pc) return new Response("no pc", { status: 404 });
    let alarm;
    if (role === "phone") {
      const r = this.info(pc);
      if (now - r.born > (r.long ? LONG_WAIT_MS : WAIT_MS)) return new Response("expired", { status: 410 });
      if (r.long) { r.born = now; pc.serializeAttachment(r); }   // a remembered phone: its 30 minutes start now
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
    const size = typeof data === "string" ? data.length : data.byteLength;
    const r = this.room(), live = !!this.side("phone");
    if (++me.n > MAX_RATE || size > MAX_MSG || !r || (live && now - r.born > LIFE_MS)) { await this.end(); return; }
    try { ws.serializeAttachment(me); } catch (e) {}
    const o = this.side(me.role === "pc" ? "phone" : "pc");
    if (o) try { o.send(data); } catch (e) { await this.end(); }
  }

  async webSocketClose() { await this.end(); }
  async webSocketError() { await this.end(); }

  async alarm() {
    const r = this.room(), phone = this.side("phone");
    if (!r || !phone || Date.now() - r.born >= LIFE_MS - 1000) await this.end();
    else await this.state.storage.setAlarm(r.born + LIFE_MS);
  }

  async end() {
    if (this.done) return;   // once: closing a socket twice can leave the other side never hearing it close
    this.done = true;
    for (const s of this.state.getWebSockets()) try { s.close(1000, "session over"); } catch (e) {}
    try { await this.state.storage.deleteAll(); } catch (e) {}   // nothing is left behind - not even the alarm
  }
}
