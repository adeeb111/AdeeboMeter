// AdeeboMeter relay - a Cloudflare Worker (free plan). It turns a bug report, feedback or a Hall of Fame card sent from
// the app or the website into an issue in the repo, so nobody needs a GitHub account.
//
// Secrets (Cloudflare > Worker > Settings > Variables and Secrets - typed by Adeeb, never in this file):
//   GITHUB_TOKEN      a fine-grained token: only the adeeb111/AdeeboMeter repo, only "Issues: Read and write"
//   TURNSTILE_SECRET  the secret half of the free Turnstile widget on adeebometer.com/report
// Binding:
//   RL                a KV namespace, used to count sends per address (5 an hour, 20 a day)
//   LIMITER           optional: a Cloudflare Rate Limiting binding (atomic). When it's there it decides first - KV counts
//                     alone can be raced by simultaneous requests (AM-CG-217)
//
// What it refuses: anything not JSON, over 12 KB, a kind it doesn't know, a website send without a passing Turnstile
// check, and an address that already sent too much. What it strips again: e-mail addresses, IP addresses, Windows
// user paths (the app already removes them - this is the second net).

const REPO = "adeeb111/AdeeboMeter";
const ORIGINS = ["https://adeebometer.com", "https://www.adeebometer.com"];
const KINDS = { bug: "bug", feedback: "feedback", hof: "hall-of-fame" };

export default {
  async fetch(req, env) {
    const origin = req.headers.get("Origin") || "";
    const cors = ORIGINS.includes(origin) ? { "Access-Control-Allow-Origin": origin, "Access-Control-Allow-Methods": "POST", "Access-Control-Allow-Headers": "Content-Type", "Vary": "Origin" } : {};
    const out = (status, obj) => new Response(JSON.stringify(obj), { status, headers: { "Content-Type": "application/json", ...cors } });
    if (req.method === "OPTIONS") return new Response(null, { status: 204, headers: cors });
    if (req.method !== "POST" || new URL(req.url).pathname !== "/send") return out(404, { ok: false, why: "not here" });

    // AM-CG-218: the size limit holds while reading - a huge body is cut off, not read whole first
    if (+(req.headers.get("Content-Length") || 0) > 12000) return out(413, { ok: false, why: "too long" });
    let raw = "";
    { const rd = req.body ? req.body.getReader() : null, dec = new TextDecoder(); let n = 0;
      if (rd) for (;;) { const { done, value } = await rd.read(); if (done) break; n += value.length; if (n > 12000) { try { await rd.cancel(); } catch {} return out(413, { ok: false, why: "too long" }); } raw += dec.decode(value, { stream: true }); }
      raw += dec.decode(); }
    let m; try { m = JSON.parse(raw); } catch { return out(400, { ok: false, why: "not JSON" }); }
    if (!m || typeof m !== "object" || Array.isArray(m)) return out(400, { ok: false, why: "not a report" });   // a record, before any field is read
    for (const k of ["kind", "from", "title", "body", "version", "turnstile", "rid"]) if (m[k] !== undefined && typeof m[k] !== "string") return out(400, { ok: false, why: "bad field" });
    if (m.card !== undefined && (m.card === null || typeof m.card !== "object" || Array.isArray(m.card))) return out(400, { ok: false, why: "bad card" });
    const kind = Object.prototype.hasOwnProperty.call(KINDS, m.kind) ? m.kind : null;
    // AM-CG-217: the sender's own label isn't trusted - anything a browser sends (it carries an Origin) must pass Turnstile,
    // whatever it calls itself; only a request with no browser origin is treated as the app (and gets the app's limits)
    const browser = origin !== "", from = browser ? "site" : "app";
    if (!kind) return out(400, { ok: false, why: "unknown kind" });
    // bug-hunt ML-19: the app can't prove a person is there, so a request without an Origin must at least look like the app -
    // and its counts rely on the atomic LIMITER binding (the KV counts below are best effort: two requests at once can both pass)
    if (!browser && !/^AdeeboMeter\/\d/.test(req.headers.get("User-Agent") || "")) return out(403, { ok: false, why: "send it from AdeeboMeter or adeebometer.com" });
    if (browser && !ORIGINS.includes(origin)) return out(403, { ok: false, why: "not from adeebometer.com" });

    // the website proves a person is there (Turnstile); the app can't, so it leans on the counts below
    if (from === "site") {
      const f = new FormData(); f.append("secret", env.TURNSTILE_SECRET); f.append("response", String(m.turnstile || "")); f.append("remoteip", req.headers.get("CF-Connecting-IP") || "");
      // bug-hunt ML-121: a Turnstile outage is a plain 503 with a reason, not an unhandled 500
      let t; try { t = await (await fetch("https://challenges.cloudflare.com/turnstile/v0/siteverify", { method: "POST", body: f, signal: AbortSignal.timeout(8000) })).json(); } catch (e) { return out(503, { ok: false, why: "the robot check isn't answering - try again in a minute" }); }
      if (!t.success) return out(403, { ok: false, why: "the check didn't pass - try again" });
    }
    // hunters15 R-ML1: a report carries its own id (rid); one already filed is answered "ok" again instead of filed twice
    const rid = /^[0-9a-f]{16,32}$/.test(m.rid || "") ? m.rid : "";
    if (rid) { let seen = null; try { seen = await env.RL.get("r:" + rid); } catch (e) {} if (seen && seen !== "pending") return out(200, { ok: true, number: +seen || null, again: true }); }
    // at most 5 an hour and 20 a day from one address (the address itself is only hashed, never kept). R-L2: rolling windows,
    // not clock hours; R-L4: an IPv6 address counts by its /64 (privacy addresses rotate inside it)
    let ip = req.headers.get("CF-Connecting-IP") || "?";
    if (ip.includes(":")) ip = ip.split(":").slice(0, 4).join(":") + "::/64";
    if (env.LIMITER) { const { success } = await env.LIMITER.limit({ key: ip + "|" + from }); if (!success) return out(429, { ok: false, why: "a lot sent already - try again later" }); }
    const h = Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(ip + "|am")))).slice(0, 12).map(b => b.toString(16).padStart(2, "0")).join("");
    const now = Date.now(), tKey = "t:" + h;
    let times = []; try { times = JSON.parse((await env.RL.get(tKey)) || "[]").filter(t => typeof t === "number" && now - t < 86400000); } catch (e) { times = []; }
    if (times.filter(t => now - t < 3600000).length >= 5 || times.length >= 20) return out(429, { ok: false, why: "a lot sent already - try again later" });
    // hunt-5: the count goes up only for a send that was filed (below) - a GitHub hiccup doesn't spend your five

    const clean = s => String(s || "").replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/g, "")
      .replace(/[\w.+-]+@[\w-]+(\.[\w-]+)+/g, "<email>")
      .replace(/\b\d{1,3}(\.\d{1,3}){3}\b/g, "<ip>")
      .replace(/\b[0-9A-Fa-f]{2}([:-])[0-9A-Fa-f]{2}(\1[0-9A-Fa-f]{2}){4}\b/g, "<mac>")          // bug-hunt ML-30: 00:1a:2b:3c:4d:5e / 00-1A-2B-...
      .replace(/\b[0-9A-Fa-f]{4}\.[0-9A-Fa-f]{4}\.[0-9A-Fa-f]{4}\b/g, "<mac>")                  // and Cisco's 001a.2b3c.4d5e
      .replace(/(?<![\w:])(?:[0-9A-Fa-f]{1,4}:){0,7}:(?:[0-9A-Fa-f]{1,4}:?){0,7}(?:%[\w.-]+)?(?![\w:])/g, x => x.includes("::") && x.length > 2 ? "<ipv6>" : x)   // AM-CG-215: compressed / scoped IPv6
      .replace(/\b([0-9A-Fa-f]{1,4}:){7}[0-9A-Fa-f]{1,4}\b/g, "<ipv6>")
      .replace(/\\\\[\w.$-]+(?:\\[^\s"'<>|]*)?/g, "<path>")                     // \\server\share\...
      .replace(/\b[A-Za-z]:\\[^\s"'<>|]+/g, "<path>")                              // any drive path
      .replace(/@(?=\w)/g, "@​");   // no accidental @mentions of real GitHub users
    let title = clean(m.title).replace(/\s+/g, " ").trim().slice(0, 100), body = clean(m.body).slice(0, 8000);
    const version = clean(m.version).slice(0, 20);
    if (kind === "hof") {
      // the Hall of Fame check (hall-of-fame.yml) reads the same headings the issue form makes
      const f = m.card || {};
      const one = (k, n) => clean(String(f[k] || "").replace(/[\u0000-\u001F]/g, "").replace(/\s+/g, " ").trim()).slice(0, n);   // R-L1: the scrubber too - this is a public issue
      body = ["### Name on the card", one("name", 40), "### Points on the card", one("points", 6), "### Date on the card", one("date", 10), "### Claim code", one("code", 30),
              "### Where people can find you (optional)", one("where", 12) || "_No response_", "### Your handle (optional)", one("handle", 40) || "_No response_"].join("\n\n");
      title = "[Hall of Fame] " + one("name", 40);
      if (!one("name", 40) || !one("code", 30)) return out(400, { ok: false, why: "the card has no name or claim code" });   // R-L3
    } else {
      if (!title && !body.trim()) return out(400, { ok: false, why: "empty report" });   // R-L3: no blank issues
      if (!title) title = (kind === "bug" ? "Bug: " : "Feedback: ") + body.split("\n")[0].slice(0, 80);
      body = body + "\n\n---\nSent from " + (from === "app" ? "the app" + (version ? " " + version : "") : "adeebometer.com/report") + " through the relay - not checked by a person yet.";
    }
    if (rid) { body += "\n\n<!-- ref " + rid + " -->"; try { const was = await env.RL.get("r:" + rid); if (was === "pending") {
      // R-ML1: a first try timed out at GitHub - look for it before filing again
      const s = await fetch("https://api.github.com/search/issues?q=" + encodeURIComponent("repo:" + REPO + " \"ref " + rid + "\" in:body"), { headers: { "Authorization": "Bearer " + env.GITHUB_TOKEN, "Accept": "application/vnd.github+json", "User-Agent": "adeebometer-relay" }, signal: AbortSignal.timeout(8000) });
      if (s.ok) { const j = await s.json(); if (j && j.items && j.items.length) { try { await env.RL.put("r:" + rid, String(j.items[0].number), { expirationTtl: 8 * 86400 }); } catch (e) {} return out(200, { ok: true, number: j.items[0].number, again: true }); } }
    } await env.RL.put("r:" + rid, "pending", { expirationTtl: 8 * 86400 }); } catch (e) {} }
    let gh; try { gh = await fetch("https://api.github.com/repos/" + REPO + "/issues", {
      method: "POST", signal: AbortSignal.timeout(10000),
      headers: { "Authorization": "Bearer " + env.GITHUB_TOKEN, "Accept": "application/vnd.github+json", "User-Agent": "adeebometer-relay", "X-GitHub-Api-Version": "2022-11-28" },
      body: JSON.stringify({ title, body, labels: [KINDS[kind], "from-" + from] })
    }); } catch (e) { return out(503, { ok: false, why: "couldn't reach GitHub just now - it will try again" }); }
    // R-ML2: GitHub refusing the relay's own access (401/403) is not "busy" - it won't fix itself, so it isn't retried for a week
    if (gh.status === 401 || gh.status === 403) return out(424, { ok: false, why: "the relay can't file reports right now - its GitHub access needs renewing" });
    if (!gh.ok) return out(502, { ok: false, why: "couldn't file it just now - please send it again in a few minutes" });
    // hunters14 SEC-L1: read GitHub's answer before counting - an unreadable body still means it was filed (200), so it's a
    // success without a number, never a 500 that makes the app send it again
    let made = null; try { made = await gh.json(); } catch (e) { made = null; }
    // R-ML1: filed is filed - a counting hiccup afterwards never turns it into an error the app would resend
    try { times.push(now); await Promise.all([env.RL.put(tKey, JSON.stringify(times), { expirationTtl: 90000 }), rid ? env.RL.put("r:" + rid, String(made && made.number ? made.number : 1), { expirationTtl: 8 * 86400 }) : Promise.resolve()]); } catch (e) {}
    return out(200, { ok: true, number: made && made.number ? made.number : null });
  }
};
