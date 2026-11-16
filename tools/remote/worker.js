// AdeeboMeter phone remote - the relay. It only passes sealed messages between one PC and one phone.
//
//  - It never sees a key: messages are encrypted end to end (ECDH P-256, AES-256, HMAC-SHA256) with a secret that only
//    the QR code carries. To this relay they are random bytes.
//  - Nothing is stored: no database, no KV, no logs of content. A room lives in memory while two sockets are open.
//  - A room holds exactly one PC and one phone. A second phone is refused. Rooms close after 30 minutes, after 2 minutes
//    with no phone, or as soon as either side leaves.
//  - Limits: 64 KB per message, 40 messages a second per side, room names must be 22 random characters.
//
// Deploy (your step): `npx wrangler deploy` in this folder, then put the address in Remote.Relay (src/Remote/Remote.cs)
// and RELAY in docs/remote.html.

const ROOM = /^[A-Za-z0-9_-]{22}$/;
const MAX_MSG = 64 * 1024, MAX_RATE = 40, LIFE_MS = 30 * 60 * 1000, WAIT_MS = 2 * 60 * 1000, LONG_WAIT_MS = 25 * 60 * 1000;   // long: a remembered phone's room
const ORIGINS = ["https://adeebometer.com", "https://www.adeebometer.com"];

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
    const id = env.ROOMS.idFromName(m[1]);
    return env.ROOMS.get(id).fetch(req);
  },
};

export class Room {
  constructor(state) { this.state = state; this.pc = null; this.phone = null; this.born = Date.now(); }

  async fetch(req) {
    const u = new URL(req.url), role = u.searchParams.get("role"), long = role === "pc" && u.searchParams.get("wait") === "long";
    if (this.born && this.long && role === "phone" && !this.phone && Date.now() - this.born <= LONG_WAIT_MS) this.born = Date.now();   // its 30 minutes start now
    if (Date.now() - this.born > (this.long && !this.phone ? LONG_WAIT_MS : LIFE_MS)) return new Response("expired", { status: 410 });
    if (role === "pc" ? this.pc : this.phone) return new Response("taken", { status: 409 });   // one PC, one phone
    if (role === "phone" && !this.pc) return new Response("no pc", { status: 404 });
    const [client, server] = Object.values(new WebSocketPair());
    server.accept();
    const me = { ws: server, n: 0, t: Date.now() };
    this[role] = me; if (long) this.long = true;
    const other = () => (role === "pc" ? this.phone : this.pc);
    server.addEventListener("message", (ev) => {
      const now = Date.now();
      if (now - me.t > 1000) { me.t = now; me.n = 0; }
      const size = typeof ev.data === "string" ? ev.data.length : ev.data.byteLength;
      if (++me.n > MAX_RATE || size > MAX_MSG || (this.phone && now - this.born > LIFE_MS)) { this.end(); return; }
      const o = other(); if (o) try { o.ws.send(ev.data); } catch (e) { this.end(); }
    });
    const gone = () => this.end();
    server.addEventListener("close", gone); server.addEventListener("error", gone);
    // the room's clock: no phone within 2 minutes, or 30 minutes in all, and it closes
    await this.state.storage.setAlarm(Date.now() + (role === "pc" ? (long ? LONG_WAIT_MS : WAIT_MS) : LIFE_MS - (Date.now() - this.born)));
    return new Response(null, { status: 101, webSocket: client });
  }

  async alarm() { if (!this.phone || Date.now() - this.born >= LIFE_MS - 1000) this.end(); else await this.state.storage.setAlarm(this.born + LIFE_MS); }

  end() {
    for (const s of [this.pc, this.phone]) if (s) try { s.ws.close(1000, "session over"); } catch (e) {}
    this.pc = this.phone = null; this.born = 0;   // a finished room can't be reused
  }
}
