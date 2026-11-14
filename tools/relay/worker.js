// AdeeboMeter relay - a Cloudflare Worker (free plan). It turns a bug report, feedback or a Hall of Fame card sent from
// the app or the website into an issue in the repo, so nobody needs a GitHub account.
//
// Secrets (Cloudflare > Worker > Settings > Variables and Secrets - typed by Adeeb, never in this file):
//   GITHUB_TOKEN      a fine-grained token: only the adeeb111/AdeeboMeter repo, only "Issues: Read and write"
//   TURNSTILE_SECRET  the secret half of the free Turnstile widget on adeebometer.com/report
// Binding:
//   RL                a KV namespace, used to count sends per address (5 an hour, 20 a day)
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

    const raw = await req.text();
    if (raw.length > 12000) return out(413, { ok: false, why: "too long" });
    let m; try { m = JSON.parse(raw); } catch { return out(400, { ok: false, why: "not JSON" }); }
    const kind = KINDS[m.kind] ? m.kind : null, from = m.from === "site" ? "site" : m.from === "app" ? "app" : null;
    if (!kind || !from) return out(400, { ok: false, why: "unknown kind" });

    // the website proves a person is there (Turnstile); the app can't, so it leans on the counts below
    if (from === "site") {
      const f = new FormData(); f.append("secret", env.TURNSTILE_SECRET); f.append("response", String(m.turnstile || "")); f.append("remoteip", req.headers.get("CF-Connecting-IP") || "");
      const t = await (await fetch("https://challenges.cloudflare.com/turnstile/v0/siteverify", { method: "POST", body: f })).json();
      if (!t.success) return out(403, { ok: false, why: "the check didn't pass - try again" });
    }
    // at most 5 an hour and 20 a day from one address (the address itself is only hashed, never kept)
    const ip = req.headers.get("CF-Connecting-IP") || "?";
    const h = Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(ip + "|am")))).slice(0, 12).map(b => b.toString(16).padStart(2, "0")).join("");
    const hourKey = "h:" + h + ":" + Math.floor(Date.now() / 3600000), dayKey = "d:" + h + ":" + Math.floor(Date.now() / 86400000);
    const [hn, dn] = await Promise.all([env.RL.get(hourKey), env.RL.get(dayKey)]);
    if ((+hn || 0) >= 5 || (+dn || 0) >= 20) return out(429, { ok: false, why: "a lot sent already - try again later" });
    await Promise.all([env.RL.put(hourKey, String((+hn || 0) + 1), { expirationTtl: 3700 }), env.RL.put(dayKey, String((+dn || 0) + 1), { expirationTtl: 90000 })]);

    const clean = s => String(s || "").replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/g, "")
      .replace(/[\w.+-]+@[\w-]+(\.[\w-]+)+/g, "<email>")
      .replace(/\b\d{1,3}(\.\d{1,3}){3}\b/g, "<ip>")
      .replace(/([A-Za-z]:\\Users\\)[^\\\s]+/gi, "$1<user>")
      .replace(/@(?=\w)/g, "@​");   // no accidental @mentions of real GitHub users
    let title = clean(m.title).replace(/\s+/g, " ").trim().slice(0, 100), body = clean(m.body).slice(0, 8000);
    const version = clean(m.version).slice(0, 20);
    if (kind === "hof") {
      // the Hall of Fame check (hall-of-fame.yml) reads the same headings the issue form makes
      const f = m.card || {};
      const one = (k, n) => String(f[k] || "").replace(/[\u0000-\u001F]/g, "").replace(/\s+/g, " ").trim().slice(0, n);   // hof_verify.py cleans these itself
      body = ["### Name on the card", one("name", 40), "### Points on the card", one("points", 6), "### Date on the card", one("date", 10), "### Claim code", one("code", 30),
              "### Where people can find you (optional)", one("where", 12) || "_No response_", "### Your handle (optional)", one("handle", 40) || "_No response_"].join("\n\n");
      title = "[Hall of Fame] " + one("name", 40);
    } else {
      if (!title) title = (kind === "bug" ? "Bug: " : "Feedback: ") + body.split("\n")[0].slice(0, 80);
      body = body + "\n\n---\nSent from " + (from === "app" ? "the app" + (version ? " " + version : "") : "adeebometer.com/report") + " through the relay - not checked by a person yet.";
    }
    const gh = await fetch("https://api.github.com/repos/" + REPO + "/issues", {
      method: "POST",
      headers: { "Authorization": "Bearer " + env.GITHUB_TOKEN, "Accept": "application/vnd.github+json", "User-Agent": "adeebometer-relay", "X-GitHub-Api-Version": "2022-11-28" },
      body: JSON.stringify({ title, body, labels: [KINDS[kind], "from-" + from] })
    });
    if (!gh.ok) return out(502, { ok: false, why: "couldn't file it just now - it will try again" });
    const made = await gh.json();
    return out(200, { ok: true, number: made.number });
  }
};
