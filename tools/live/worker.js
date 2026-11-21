// AdeeboMeter Live - the website's showcase data (adeebometer.com/live.html).
// A pass-through cache: each public source is fetched at most once every few minutes and every visitor gets the same copy.
// Nothing about visitors is stored or logged (rate limits are counted in memory only); the one thing kept is the day's stock
// movers (the MOVERS store, bug-hunt L-254). Visitors' addresses never reach the sources. Sources and credit are on the page.
import { connect } from "cloudflare:sockets";
const ORIGINS = ["https://adeebometer.com", "https://www.adeebometer.com"];
const FX = ["USD", "EUR", "GBP", "CNY", "JPY", "INR", "CHF", "AUD", "MXN", "HKD"];
const IDX = ["ENER", "MTLS", "FOPR", "AGRI", "BCPI"];
// services with a public Statuspage status feed (name, host)
const STATUS = [["Zoom", "status.zoom.us"], ["Discord", "discordstatus.com"], ["GitHub", "www.githubstatus.com"], ["Cloudflare", "www.cloudflarestatus.com"], ["Reddit", "www.redditstatus.com"], ["OpenAI (ChatGPT)", "status.openai.com"],
  ["Dropbox", "status.dropbox.com"], ["Epic Games (Fortnite)", "status.epicgames.com"], ["Atlassian (Jira, Trello)", "status.atlassian.com"], ["Twilio", "status.twilio.com"], ["Vercel", "www.vercel-status.com"], ["Notion", "status.notion.so"]];
const siteSeen = new Map();   // ip -> {w: minute, n} for /site (bug-hunt M-14)
function siteBusy(ip, kind, max) {   // bug-hunt M-14 / ML-123: /site, /k, /mac and /port each have a per-minute budget per address
  const k = (kind || "site") + "|" + ip, w = Math.floor(Date.now() / 60000), r = siteSeen.get(k);
  if (!r || r.w !== w) { if (siteSeen.size > 5000) for (const [kk, x] of siteSeen) if (x.w !== w) siteSeen.delete(kk); siteSeen.set(k, { w, n: 1 }); return false; }
  return ++r.n > (max || 10);
}
const memo = new Map(), flying = new Map(), MAX = 400;   // isolate memory only: key -> {t, v}; at most MAX entries

// every answer carries "at" (when the source was last read). A failed refresh serves the last copy only up to maxStale,
// then the card says it's unavailable (AM-CG-308). Concurrent misses share one upstream call (AM-CG-322); the memo
// drops expired entries and never grows past MAX (AM-CG-323).
// anything that looks like a key or token never leaves the relay in a message
const scrub = s => s.replace(/(api[_ -]?key|token|key)[=: ]+\S+/gi, "$1 …").replace(/\b[A-Za-z0-9_-]{12,}\b/g, "…");
async function cached(key, ttl, maxStale, make) {
  const m = memo.get(key), now = Date.now();
  if (m && m.failed && now - m.failed < 900000) return m.v && m.at && now - m.at < maxStale * 1000 ? Object.assign({ stale: 1 }, m) : { at: 0, v: m.fv };   // a failed source is asked again after 15 minutes, not on every visit (AM 16 Nov: Alpha Vantage's daily limit)
  if (m && !m.failed && now - m.at < ttl * 1000) return m;
  if (flying.has(key)) return flying.get(key);
  const p = (async () => {
    try { const v = await make(); const e = { at: Date.now(), v }; put(key, e); return e; }
    catch (e) { const fv = { error: "source unavailable", why: scrub(String(e && e.message || e)).slice(0, 120) };
      put(key, Object.assign({}, m || { at: 0 }, { failed: Date.now(), fv }));
      if (m && m.v && m.at && Date.now() - m.at < maxStale * 1000) return Object.assign({ stale: 1 }, m); return { at: 0, v: fv }; }
    finally { flying.delete(key); }
  })();
  flying.set(key, p); return p;
}
function put(key, e) {
  if (memo.size >= MAX) { const now = Date.now(); for (const [k, x] of memo) if (now - x.at > 86400000) memo.delete(k); }
  while (memo.size >= MAX) memo.delete(memo.keys().next().value);   // oldest first
  memo.delete(key); memo.set(key, e);
}
async function get(url, ttl, type) {
  // bug-hunt L-228: a URL that carries a key is never put in Cloudflare's cache (the answer is memoised in memory instead)
  const keyed = /[?&](apikey|api_key|token|key)=/i.test(url);
  const r = await fetch(url, { cf: keyed ? { cacheTtl: 0, cacheEverything: false } : { cacheTtl: ttl, cacheEverything: true }, headers: { "User-Agent": "AdeeboMeter-Live (+https://adeebometer.com)" }, signal: AbortSignal.timeout(8000) });
  if (!r.ok) throw new Error(new URL(url).hostname + " answered " + r.status);
  // bug-hunt ML-22: the 2 MB cap holds while reading, not after the whole body is in memory
  if (+(r.headers.get("Content-Length") || 0) > 2000000) { try { await r.body.cancel(); } catch (e) {} throw new Error("too big"); }
  let t = ""; const rd = r.body.getReader(), dec = new TextDecoder(); let n = 0;
  for (;;) { const { done, value } = await rd.read(); if (done) break; n += value.length; if (n > 2000000) { try { await rd.cancel(); } catch (e) {} throw new Error("too big"); } t += dec.decode(value, { stream: true }); }
  t += dec.decode();
  return type === "text" ? t : JSON.parse(t);
}
const out = e => Object.assign({ at: e.at, stale: e.stale ? 1 : 0 }, e.v);

// bug-hunt L-243: a real 0 stays 0 - only a missing or unreadable value is null
const num = v => { if (v == null || v === "") return null; const n = +v; return Number.isFinite(n) ? n : null; };
const pick = (o, ids, pre, post) => {
  const rows = o.observations || [], last = rows[rows.length - 1] || {}, prev = rows[rows.length - 2] || {};
  return { date: last.d || "", items: ids.map(c => ({ c, v: num((last[pre + c + post] || {}).v), p: num((prev[pre + c + post] || {}).v) })) };
};
// rates and the commodity index are fetched and kept separately: one source down never hides the other (AM-CG-309)
const fxRates = () => cached("fx", 900, 4 * 86400, async () => pick(await get("https://www.bankofcanada.ca/valet/observations/" + FX.map(c => "FX" + c + "CAD").join(",") + "/json?recent=2", 900), FX, "FX", "CAD"));
const commodities = () => cached("idx", 3600, 14 * 86400, async () => pick(await get("https://www.bankofcanada.ca/valet/observations/" + IDX.map(c => "W." + c).join(",") + "/json?recent=2", 3600), IDX, "W.", ""));

const hockey = () => cached("nhl", 60, 600, async () => {
  const d = await get("https://api-web.nhle.com/v1/score/now", 60);
  return { date: d.currentDate || "", games: (d.games || []).slice(0, 16).map(g => ({
    a: g.awayTeam && g.awayTeam.abbrev, h: g.homeTeam && g.homeTeam.abbrev,
    as: g.awayTeam && g.awayTeam.score, hs: g.homeTeam && g.homeTeam.score,
    st: g.gameState, per: g.periodDescriptor && g.periodDescriptor.number, clk: g.clock && g.clock.timeRemaining, start: g.startTimeUTC })) };
});

const news = () => cached("bbc", 600, 6 * 3600, async () => {
  const x = await get("https://feeds.bbci.co.uk/sport/rss.xml", 600, "text");
  const items = [];
  for (const m of x.matchAll(/<item>([\s\S]*?)<\/item>/g)) {
    const t = (m[1].match(/<title>(?:<!\[CDATA\[)?([\s\S]*?)(?:\]\]>)?<\/title>/) || [])[1];
    const l = (m[1].match(/<link>([^<]+)<\/link>/) || [])[1];
    if (t && l && /^https:\/\/www\.bbc\.(co\.uk|com)\//.test(l.trim())) items.push({ t: t.trim().slice(0, 200), l: l.trim() });
    if (items.length >= 8) break;
  }
  return { items };
});

// ---- Live v2 (Adeeb, 16 Nov): markets and scores with your own picks. Each source is read at most once per cache window
// and every visitor gets the same copy; the picks themselves stay on the phone (only the league or nothing is asked for).
const BOCFX = ["USD", "EUR", "GBP", "JPY", "CNY", "INR", "CHF", "AUD", "MXN", "HKD", "NZD", "SGD", "KRW", "SEK", "NOK", "BRL", "ZAR", "SAR", "TRY", "TWD", "THB", "IDR", "MYR"];
const series = (o, ids, pre, post, n) => { const rows = (o.observations || []).filter(r => ids.some(c => r[pre + c + post] && r[pre + c + post].v != null)).sort((a, b) => a.d < b.d ? -1 : a.d > b.d ? 1 : 0).slice(-(n || 8)); const s = {}; ids.forEach(c => { s[c] = rows.map(r => num((r[pre + c + post] || {}).v)); }); return { days: rows.map(r => r.d), s }; };
const bocWeek = () => cached("bocw", 3 * 3600, 4 * 86400, async () => series(await get("https://www.bankofcanada.ca/valet/observations/" + BOCFX.map(c => "FX" + c + "CAD").join(",") + "/json?recent=8", 3 * 3600), BOCFX, "FX", "CAD"));
// a year of weeks: week, month and year changes and the 52-week range for oil and gas
const idxWeeks = () => cached("idxy", 12 * 3600, 14 * 86400, async () => series(await get("https://www.bankofcanada.ca/valet/observations/" + IDX.map(c => "W." + c).join(",") + "/json?recent=53", 12 * 3600), IDX, "W.", "", 53));
// every other currency (taka, rupee, dirham...): ExchangeRate-API's open daily table, per US dollar - credited on the page
const fxAll = () => cached("erapi", 12 * 3600, 3 * 86400, async () => { const d = await get("https://open.er-api.com/v6/latest/USD", 6 * 3600); if (d.result !== "success") throw new Error("er-api"); return { date: String(d.time_last_update_utc || "").slice(5, 16), usd: d.rates || {} }; });
const xml = (x, n) => { const items = []; for (const m of x.matchAll(/<item>([\s\S]*?)<\/item>/g)) { const t = (m[1].match(/<title>(?:<!\[CDATA\[)?([\s\S]*?)(?:\]\]>)?<\/title>/) || [])[1], l = (m[1].match(/<link>([^<]+)<\/link>/) || [])[1]; if (t && l && /^https:\/\/www\.bbc\.(co\.uk|com)\//.test(l.trim())) items.push({ t: t.trim().slice(0, 200), l: l.trim() }); if (items.length >= n) break; } return items; };
const bbc = sp => cached("bbc:" + sp, 3 * 3600, 6 * 3600, async () => ({ items: xml(await get("https://feeds.bbci.co.uk/sport/" + sp + "/rss.xml", 900, "text"), 8) }));
// leagues the page offers; NHL and MLB come through the relay from the leagues' own feeds, the rest from ESPN on the phone
const LG = { nhl: ["hockey", "nhl"], nba: ["basketball", "nba"], nfl: ["football", "nfl"], mlb: ["baseball", "mlb"], epl: ["soccer", "eng.1"], ucl: ["soccer", "uefa.champions"], mls: ["soccer", "usa.1"] };
const HEAD = { cri: "cricket", ten: "tennis", f1: "formula1" };
// ---- stock movers (Adeeb, 16 Nov: option A) - Alpha Vantage's US top gainers / losers / most traded, once a day with the
// relay's own free key (a Cloudflare secret, AV_KEY - set with "wrangler secret put AV_KEY"; never in this file)
const mv = l => (l || []).map(x => ({ t: String(x.ticker || "").slice(0, 10), p: +x.price || null, c: parseFloat(x.change_percentage) || 0, v: +x.volume || 0 }))
  .filter(x => x.t && x.p >= 1 && !/[^A-Z.]/.test(x.t) && !(x.t.length === 5 && /[WUR]$/.test(x.t) && x.p < 15)).slice(0, 5);   // under $1, warrants, units and rights are mostly noise - bug-hunt L-238: those trade low; a 5-letter W/U/R ticker at $15+ is a real company
// Never pass the provider's own words on: Alpha Vantage's refusal repeats the API key (17 Nov - the key showed on the
// public Live page). Only our fixed sentences reach a page.
const avWhy = m => /rate limit|requests per day|per minute|frequency|premium|thank you for using/i.test(m) ? "the daily limit is used up" : /invalid|apikey|api key/i.test(m) ? "the key was refused" : "no answer";
const movers = (key, d) => { const m = d.Information || d.Note || d["Error Message"]; if (m) throw new Error(avWhy(String(m))); return { day: String(d.last_updated || "").slice(0, 16), up: mv(d.top_gainers), dn: mv(d.top_losers), act: mv(d.most_actively_traded) }; };
const avShared = env => cached("av", 12 * 3600, 3 * 86400, async () => { if (!env || !env.AV_KEY) throw new Error("nokey"); return movers(env.AV_KEY, await get("https://www.alphavantage.co/query?function=TOP_GAINERS_LOSERS&apikey=" + encodeURIComponent(env.AV_KEY), 12 * 3600)); });
// ---- Alpaca (Adeeb, 21 Nov): the shared daily copy now comes from Alpaca's free market-data screener. Alpaca counts by
// account key, not by the address a call comes from, so Cloudflare's shared outgoing addresses can't use our allowance up
// the way they did with Alpha Vantage (17 Nov: the scheduled call and its retry were both refused). Two Cloudflare
// secrets, typed by Adeeb: "wrangler secret put ALPACA_KEY_ID" and "wrangler secret put ALPACA_SECRET" - never in this file.
// Three calls a day: gainers and losers, the most traded, and those few symbols' prices. Visitors never call Alpaca.
const ALP = "https://data.alpaca.markets";
const apWhy = st => st === 401 || st === 403 ? "the key was refused" : st === 429 ? "the limit is used up" : "no answer";
async function apGet(env, path) {
  const r = await fetch(ALP + path, { headers: { "APCA-API-KEY-ID": env.ALPACA_KEY_ID, "APCA-API-SECRET-KEY": env.ALPACA_SECRET, "User-Agent": "AdeeboMeter-Live (+https://adeebometer.com)" }, signal: AbortSignal.timeout(15000), cf: { cacheTtl: -1 } });
  if (!r.ok) throw new Error(apWhy(r.status));   // only our own sentence - never the provider's words
  try { return await r.json(); } catch (e) { throw new Error("no answer"); }
}
const apMv = l => mv((l || []).map(x => ({ ticker: x.symbol, price: x.price, change_percentage: String(x.percent_change), volume: 0 })));
async function alpacaMovers(env) {
  const m = await apGet(env, "/v1beta1/screener/stocks/movers?top=25");
  const a = await apGet(env, "/v1beta1/screener/stocks/most-actives?by=volume&top=25");
  const act = (a.most_actives || []).map(x => ({ t: String(x.symbol || "").slice(0, 10), v: +x.volume || 0 })).filter(x => x.t && !/[^A-Z.]/.test(x.t)).slice(0, 12);
  let px = {}; if (act.length) { try { px = await apGet(env, "/v2/stocks/snapshots?feed=iex&symbols=" + encodeURIComponent(act.map(x => x.t).join(","))); } catch (e) { px = {}; } }
  const actL = act.map(x => { const s = px[x.t] || {}, d = s.dailyBar || {}, pd = s.prevDailyBar || {}, p = +d.c || +((s.latestTrade || {}).p) || null;
    return { ticker: x.t, price: p, change_percentage: p && +pd.c ? String((p / pd.c - 1) * 100) : "0", volume: x.v }; });
  return { day: String(m.last_updated || "").slice(0, 16).replace("T", " "), src: "Alpaca", up: apMv(m.gainers), dn: apMv(m.losers), act: mv(actL) };
}
const hasAlpaca = env => !!(env && env.ALPACA_KEY_ID && env.ALPACA_SECRET);
const apShared = env => cached("alp", 12 * 3600, 3 * 86400, async () => alpacaMovers(env));
// Once a day for everyone (17 Nov): with a MOVERS store (Cloudflare KV) the relay asks Alpha Vantage only from its
// schedule - after the US close, and once more if that failed - and every visitor reads that one stored copy. Visitors
// never trigger a call, so the free 25-a-day can't be used up by traffic. Without the store it works as before.
async function moversDaily(env) {
  if (!env.MOVERS || (!hasAlpaca(env) && !env.AV_KEY)) return;
  let old; try { old = await env.MOVERS.get("movers", "json"); } catch (e) { console.log("movers: the store can't be read - " + (e && e.message)); return; }   // bug-hunt L-253: said, not a silent end
  const today = new Date().toISOString().slice(0, 10);
  if (old && old.ok && old.on === today) return;   // the retry run: today's copy is already in
  let rec;
  try {
    if (hasAlpaca(env)) rec = { ok: 1, on: today, at: Date.now(), v: await alpacaMovers(env) };
    else {
      const r = await fetch("https://www.alphavantage.co/query?function=TOP_GAINERS_LOSERS&apikey=" + encodeURIComponent(env.AV_KEY), { headers: { "User-Agent": "AdeeboMeter-Live (+https://adeebometer.com)" }, signal: AbortSignal.timeout(15000) });
      rec = { ok: 1, on: today, at: Date.now(), v: movers(env.AV_KEY, await r.json()) };
    }
  } catch (e) { const who = hasAlpaca(env) ? "Alpaca" : "Alpha Vantage"; rec = Object.assign({}, old && old.ok ? old : { ok: 0 }, { tried: Date.now(), why: (m => /daily limit|limit is used up|refused/.test(m) ? m : "no answer from " + who)(String(e && e.message || e)) }); }
  try { await env.MOVERS.put("movers", JSON.stringify(rec)); } catch (e) { console.log("movers: the store can't be written - " + (e && e.message)); }
}
async function moversShared(env) {
  if (!env.MOVERS) return out(await (hasAlpaca(env) ? apShared(env) : avShared(env)));
  let rec; try { rec = await env.MOVERS.get("movers", "json", { cacheTtl: 600 }); } catch (e) { return { at: 0, stale: 0, error: "source unavailable", why: "the stored copy can't be read just now" }; }   // bug-hunt ML-122
  // bug-hunt ML-129: a copy from a failed later try still has its data - shown, and why the newer try failed is said too
  if (rec && rec.ok) return Object.assign({ at: rec.at, stale: Date.now() - rec.at > 5 * 86400000 ? 1 : 0 }   /* bug-hunt L-236: Thursday's close to Tuesday's (Good Friday + a weekend + a Monday holiday) is 5 days, and still the latest */, rec.why ? { why: rec.why } : {}, rec.v);
  return { at: 0, stale: 0, error: "source unavailable", why: rec && rec.why ? rec.why : "not fetched yet - the first copy comes after today's US close" };
}
// a person's own key (Live > Your keys): it comes to us in a header, is used for this one call and dropped - never cached,
// logged or kept here. Alpha Vantage and CricketData only take a key in their own address (?apikey=), so it goes to them
// that way - over HTTPS, with Cloudflare's cache off for that request (bug-hunt ML-130)
async function keyed(svc, key, u) {
  const nc = { headers: { "User-Agent": "AdeeboMeter-Live (+https://adeebometer.com)" }, signal: AbortSignal.timeout(9000), cf: { cacheTtl: -1 } };
  const j2 = async (url, h) => { const r = await fetch(url, h ? { headers: Object.assign({}, nc.headers, h), signal: nc.signal, cf: nc.cf } : nc); const t = await r.text(); let d; try { d = JSON.parse(t); } catch (e) { throw new Error("not JSON (" + r.status + ")"); } if (r.status === 401 || r.status === 403) throw new Error("the key was refused"); if (r.status === 429) throw new Error("the key's daily limit is used up"); if (!r.ok) throw new Error("answered " + r.status); return d; };
  if (svc === "ap") { const i = key.indexOf(":"); const d = await alpacaMovers({ ALPACA_KEY_ID: key.slice(0, i), ALPACA_SECRET: key.slice(i + 1) }); d.src = "Alpaca"; return d; }   // ID:SECRET, one refresh, nothing kept
  if (svc === "av") return movers(key, await j2("https://www.alphavantage.co/query?function=TOP_GAINERS_LOSERS&apikey=" + encodeURIComponent(key)));
  if (svc === "fd") { const d = await j2("https://api.football-data.org/v4/matches", { "X-Auth-Token": key });
    return { g: (d.matches || []).slice(0, 40).map(m => ({ lg: String((m.competition || {}).name || "").slice(0, 40), d: m.utcDate, st: /IN_PLAY|PAUSED/.test(m.status) ? "in" : m.status === "FINISHED" ? "post" : "pre", det: m.status === "PAUSED" ? "Half-time" : m.minute ? m.minute + "'" : m.status === "IN_PLAY" ? "Live" : "",
      a: { n: String((m.awayTeam || {}).shortName || (m.awayTeam || {}).name || "").slice(0, 30), ab: String((m.awayTeam || {}).tla || ""), id: "", s: (((m.score || {}).fullTime || {}).away ?? "") + "" },
      h: { n: String((m.homeTeam || {}).shortName || (m.homeTeam || {}).name || "").slice(0, 30), ab: String((m.homeTeam || {}).tla || ""), id: "", s: (((m.score || {}).fullTime || {}).home ?? "") + "" } })) }; }
  if (svc === "cd") { const d = await j2("https://api.cricapi.com/v1/currentMatches?offset=0&apikey=" + encodeURIComponent(key)); if (d.status && d.status !== "success") throw new Error(String(d.reason || d.status).slice(0, 120));
    return { hits: (d.info || {}).hitsToday || null, lim: (d.info || {}).hitsLimit || null, g: (d.data || []).slice(0, 25).map(m => { const sc = (m.score || []).map(x => (x.r ?? "") + "/" + (x.w ?? "") + " (" + (x.o ?? "") + ")"), tm = m.teams || [];
      return { lg: String(m.matchType || "").toUpperCase().slice(0, 10) + (m.venue ? " · " + String(m.venue).slice(0, 40) : ""), d: m.dateTimeGMT ? m.dateTimeGMT + "Z" : m.date, st: m.matchEnded ? "post" : m.matchStarted ? "in" : "pre", det: "", sum: String(m.status || "").slice(0, 80),
        a: { n: String(tm[1] || "").slice(0, 30), s: sc[1] || "" }, h: { n: String(tm[0] || "").slice(0, 30), s: sc[0] || "" } }; }) }; }
  if (svc === "gn") { const q = (u.searchParams.get("q") || "").slice(0, 60), d = await j2("https://gnews.io/api/v4/" + (q ? "search?q=" + encodeURIComponent(q) + "&" : "top-headlines?category=sports&") + "lang=en&max=10&apikey=" + encodeURIComponent(key));
    return { items: (d.articles || []).slice(0, 10).map(a => ({ t: String(a.title || "").slice(0, 200), l: String(a.url || ""), s: String((a.source || {}).name || "").slice(0, 40) })).filter(a => /^https:\/\//.test(a.l)) }; }
  throw new Error("unknown service");
}
// ---- official league feeds through the relay (Adeeb, 16 Nov: "mix of 2" - ESPN refuses servers, so NHL and MLB come from
// the leagues themselves; NBA, NFL, soccer and cricket matches the phone asks ESPN for itself, only when the person turns it on)
const ISO = d => d.toISOString().slice(0, 10);
const nhlT = t => t ? { ab: String(t.abbrev || ""), n: String(((t.commonName || t.name || {}).default) || t.abbrev || "").slice(0, 30), id: String(t.abbrev || ""), s: t.score != null ? String(t.score) : "" } : null;
const nhlG = g => { const st = g.gameState || "", live = /LIVE|CRIT/.test(st), fin = /FINAL|OFF/.test(st);
  return { d: g.startTimeUTC, st: live ? "in" : fin ? "post" : "pre", det: live ? "P" + ((g.periodDescriptor || {}).number || "") + " " + ((g.clock || {}).timeRemaining || "") : fin ? "Final" : "", sum: "", a: nhlT(g.awayTeam), h: nhlT(g.homeTeam) }; };
const nhlDay = day => cached("nhl:" + (day === "now" ? "now:" + new Date().toISOString().slice(0, 10) : day),   /* bug-hunt L-244: "now" is today's - yesterday's copy is never served after midnight */ day === "now" ? 3 * 3600 : 12 * 3600, 6 * 3600, async () => ({ g: ((await get("https://api-web.nhle.com/v1/score/" + day, 3 * 3600)).games || []).slice(0, 20).map(nhlG) }));
const nhlNext = () => cached("nhl:wk", 12 * 3600, 86400, async () => { const d = await get("https://api-web.nhle.com/v1/schedule/now", 12 * 3600), g = []; (d.gameWeek || []).forEach(w => (w.games || []).forEach(x => g.push(nhlG(x)))); return { g: g.filter(x => x.st === "pre").slice(0, 20) }; });
const nhlTable = () => cached("nhl:st", 12 * 3600, 2 * 86400, async () => { const d = await get("https://api-web.nhle.com/v1/standings/now", 12 * 3600), by = {};
  (d.standings || []).forEach(t => { const c = String(t.conferenceName || "League"); (by[c] = by[c] || []).push({ ab: String((t.teamAbbrev || {}).default || ""), n: String((t.teamCommonName || t.teamName || {}).default || "").slice(0, 30), id: String((t.teamAbbrev || {}).default || ""), gp: String(t.gamesPlayed ?? ""), w: String(t.wins ?? ""), l: String(t.losses ?? ""), pts: String(t.points ?? ""), seed: +t.conferenceSequence || 99 }); });
  return { gr: Object.keys(by).map(n => ({ n, rows: by[n].sort((a, b) => a.seed - b.seed).slice(0, 16) })) }; });
const MLBDIV = { 200: "AL West", 201: "AL East", 202: "AL Central", 203: "NL West", 204: "NL East", 205: "NL Central" };
const mlbT = t => ({ ab: String(((t || {}).team || {}).abbreviation || ""), n: String(((t || {}).team || {}).teamName || ((t || {}).team || {}).name || "").slice(0, 30), id: String(((t || {}).team || {}).id || ""), s: (t || {}).score != null ? String(t.score) : "" });
const mlbG = g => { const a = (g.status || {}).abstractGameState; return { d: g.gameDate, st: a === "Live" ? "in" : a === "Final" ? "post" : "pre", det: a === "Final" ? "Final" : a === "Live" ? String((g.status || {}).detailedState || "Live").slice(0, 30) : "", sum: "", a: mlbT((g.teams || {}).away), h: mlbT((g.teams || {}).home) }; };
const mlbSched = (q, ttl) => cached("mlb:" + q, ttl, 86400, async () => { const d = await get("https://statsapi.mlb.com/api/v1/schedule?sportId=1&hydrate=team&" + q, ttl), g = []; (d.dates || []).forEach(x => (x.games || []).forEach(y => g.push(mlbG(y)))); return { g: g.slice(0, 20) }; });
const mlbTable = () => cached("mlb:st", 12 * 3600, 2 * 86400, async () => { const now = new Date(), yr = now.getUTCMonth() < 3 ? now.getUTCFullYear() - 1 : now.getUTCFullYear()   /* bug-hunt L-245: the season opens at the end of March - until April, last season's table */, d = await get("https://statsapi.mlb.com/api/v1/standings?leagueId=103,104&hydrate=team&season=" + yr, 12 * 3600);
  return { season: yr, gr: (d.records || []).map(r => ({ n: MLBDIV[(r.division || {}).id] || "Division", rows: (r.teamRecords || []).map(t => ({ ab: String((t.team || {}).abbreviation || ""), n: String((t.team || {}).teamName || (t.team || {}).name || "").slice(0, 30), id: String((t.team || {}).id || ""), gp: String(t.gamesPlayed ?? ""), w: String(t.wins ?? ""), l: String(t.losses ?? ""), pts: "", seed: +t.divisionRank || 99 })).sort((a, b) => a.seed - b.seed) })) }; });
const weather = (lat, lon) => cached("w" + lat + "," + lon, 1800, 3 * 3600, async () => {
  const d = await get("https://api.open-meteo.com/v1/forecast?latitude=" + lat + "&longitude=" + lon +
    "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m,precipitation&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=4", 1800);
  return { cur: d.current || {}, daily: d.daily || {}, tz: d.timezone_abbreviation || "" };
});
const places = q => cached("g" + q, 86400, 86400, async () => {
  const d = await get("https://geocoding-api.open-meteo.com/v1/search?count=5&language=en&format=json&name=" + encodeURIComponent(q), 86400);
  return { places: (d.results || []).map(p => ({ n: p.name, r: [p.admin1, p.country].filter(Boolean).join(", "), lat: +p.latitude.toFixed(1), lon: +p.longitude.toFixed(1) })) };
});

// the same network: the first 3 bytes of an IPv4 address, or the first 48 bits of IPv6
function net(a) { const v4m = /^::ffff:(\d+\.\d+\.\d+\.\d+)$/i.exec(a); if (v4m) a = v4m[1];   /* L-27: an IPv4-mapped address compares as the IPv4 it is */
  if (!a.includes(":")) return a.split(".").slice(0, 3).join("."); const [h, t = ""] = a.split("::"), L = h ? h.split(":") : [], R2 = t ? t.split(":") : [], all = L.concat(Array(Math.max(0, 8 - L.length - R2.length)).fill("0"), R2); return all.slice(0, 3).map(x => parseInt(x || "0", 16)).join(":"); }
const PRIV = /^(localhost|.*\.local|.*\.internal|.*\.localhost|10\.\d+\.\d+\.\d+|127\.\d+\.\d+\.\d+|169\.254\.\d+\.\d+|192\.168\.\d+\.\d+|172\.(1[6-9]|2\d|3[01])\.\d+\.\d+|100\.(6[4-9]|[7-9]\d|1[01]\d|12[0-7])\.\d+\.\d+|0\.\d+\.\d+\.\d+|\[.*\])$/i;   // bug-hunt ML-21: CGNAT 100.64/10 too
// bug-hunt ML-21: a public name can point at a private address - ask DNS (over HTTPS) first and refuse those
async function privateName(host) {
  if (/^\d+\.\d+\.\d+\.\d+$/.test(host)) return false;
  try {
    const r = await fetch("https://cloudflare-dns.com/dns-query?type=A&name=" + encodeURIComponent(host), { headers: { Accept: "application/dns-json" }, signal: AbortSignal.timeout(4000) });
    const j = r.ok ? await r.json() : null; return !!(j && (j.Answer || []).some(a => a.type === 1 && PRIV.test(String(a.data))));
  } catch (e) { return false; }   // DNS failing here means the fetch fails too
}
async function siteHops(start) {
  const hops = []; let cur = start;
  for (let i = 0; i < 6; i++) {
    const port = cur.port || (cur.protocol === "http:" ? "80" : "443");
    if (!/^https?:$/.test(cur.protocol) || PRIV.test(cur.hostname) || !["80", "443", "8080", "8443"].includes(port) || await privateName(cur.hostname)) { hops.push({ u: cur.href.slice(0, 200), err: "not checked (only public web addresses)" }); break; }
    const t0 = Date.now(); let r;
    try { r = await fetch(cur.href, { redirect: "manual", headers: { "User-Agent": "AdeeboMeter-Check (+https://adeebometer.com)" }, signal: AbortSignal.timeout(8000) }); }
    catch (e) { const m = String(e && e.message || e); hops.push({ u: cur.href.slice(0, 200), err: /timeout|abort/i.test(m) ? "no answer in 8 s" : /certificate|ssl|tls/i.test(m) ? "secure connection failed (certificate)" : /dns|resolve|not found/i.test(m) ? "the name doesn't exist" : "couldn't connect" }); break; }
    const ms = Date.now() - t0; try { if (r.body) await r.body.cancel(); } catch (e) {}
    const H = n => (r.headers.get(n) || "").slice(0, 120), loc = H("location");
    hops.push({ u: cur.href.slice(0, 200), s: r.status, ms, srv: H("server"), ct: H("content-type").split(";")[0], hsts: H("strict-transport-security") ? 1 : 0, loc: loc.slice(0, 200) });
    if (r.status >= 300 && r.status < 400 && loc) { try { cur = new URL(loc, cur); } catch (e) { break; } } else break;
  }
  return { hops };
}
function reply(req, body, maxAge) {
  const o = req.headers.get("Origin"), h = {
    "Content-Type": "application/json; charset=utf-8", "Cache-Control": maxAge > 0 ? "public, max-age=" + maxAge : maxAge < 0 ? "private, no-store" : "no-store",
    "Access-Control-Allow-Origin": ORIGINS.includes(o) ? o : ORIGINS[0], "Vary": "Origin", "Timing-Allow-Origin": ORIGINS.includes(o) ? o : ORIGINS[0], "X-Content-Type-Options": "nosniff" };
  return new Response(typeof body === "string" ? body : JSON.stringify(body), { headers: h });
}
// a whole number, nothing after it, inside its own range (AM-CG-311); rounded to ~10 km so a forecast is shared by an area
const coord = (v, lim) => { if (v == null || !/^\s*-?\d{1,3}(\.\d+)?\s*$/.test(String(v))) return null; const n = Math.round(Number(v) * 10) / 10; return Number.isFinite(n) && Math.abs(n) <= lim ? n : null; };

export default {
  async fetch(req, env) {
    const u = new URL(req.url);
    // the "your own key" calls carry the key in a header (not the address, which can end up in logs): the browser asks first
    if (req.method === "OPTIONS" && u.pathname === "/k") { const o = req.headers.get("Origin"); return new Response(null, { status: 204, headers: { "Access-Control-Allow-Origin": ORIGINS.includes(o) ? o : ORIGINS[0], "Access-Control-Allow-Methods": "GET", "Access-Control-Allow-Headers": "X-Key", "Access-Control-Max-Age": "86400", "Vary": "Origin" } }); }
    if (req.method !== "GET") return new Response("", { status: 405 });
    switch (u.pathname) {
      case "/all": {
        const [f, i, h, n] = await Promise.all([fxRates(), commodities(), hockey(), news()]);
        return reply(req, { t: Date.now(), markets: { fx: out(f), idx: out(i) }, hockey: out(h), news: out(n) }, 60);
      }
      case "/weather": {
        // the visitor's rough area from Cloudflare (city level) unless they picked a place; used for this answer only
        const given = u.searchParams.has("lat") || u.searchParams.has("lon");
        let lat = coord(u.searchParams.get("lat"), 90), lon = coord(u.searchParams.get("lon"), 180), name = (u.searchParams.get("n") || "").slice(0, 60);
        if (given && (lat === null || lon === null)) return reply(req, { error: "bad place" }, 0);
        if (!given) { lat = coord(req.cf && req.cf.latitude, 90); lon = coord(req.cf && req.cf.longitude, 180); name = (req.cf && req.cf.city) || ""; }
        if (lat === null || lon === null) return reply(req, { error: "no place" }, 0);
        // the automatic answer is about this visitor's area: never stored by a shared cache (AM-CG-310)
        return reply(req, Object.assign({ place: name, lat, lon }, out(await weather(lat, lon))), given ? 600 : -1);
      }
      case "/place": {
        const q = (u.searchParams.get("q") || "").trim().slice(0, 60);
        return reply(req, q.length < 2 ? { places: [] } : out(await places(q.toLowerCase())), 3600);
      }
      // Live v2: the day's currencies (a week of Bank of Canada days, plus a table of every currency) and commodities
      case "/markets": {
        const [w, a, ix] = await Promise.all([bocWeek(), fxAll(), idxWeeks()]);
        return reply(req, { boc: out(w), all: out(a), idx: out(ix) }, 300);
      }
      // one league: last night, today (or the week ahead when today is empty), a short table and its headlines
      case "/scores": {
        const k = (u.searchParams.get("lg") || "").toLowerCase();
        if (HEAD[k]) { const n = await bbc(HEAD[k]); return reply(req, { lg: k, news: out(n), espn: k === "cri" ? 1 : 0 }, 600); }
        if (!LG[k]) return reply(req, { error: "unknown league" }, 0);
        const now = new Date(), y = new Date(now.getTime() - 86400000);
        if (k === "nhl") { const [today, yest, st, nx] = await Promise.all([nhlDay("now"), nhlDay(ISO(y)), nhlTable(), nhlNext()]); return reply(req, { lg: k, at: Date.now(), src: "NHL", today: out(today), yest: out(yest), next: out(nx), table: out(st), news: null }, 600); }
        if (k === "mlb") { const [today, yest, st, nx] = await Promise.all([mlbSched("date=" + ISO(now), 3 * 3600), mlbSched("date=" + ISO(y), 12 * 3600), mlbTable(), mlbSched("startDate=" + ISO(new Date(now.getTime() + 86400000)) + "&endDate=" + ISO(new Date(now.getTime() + 8 * 86400000)), 12 * 3600)]);
          return reply(req, { lg: k, at: Date.now(), src: "MLB", today: out(today), yest: out(yest), next: out(nx), table: out(st), news: null }, 600); }
        return reply(req, { lg: k, espn: 1 }, 3600);   // NBA, NFL, soccer: the phone asks ESPN itself, if the person turned that on
      }
      // stock movers: the relay's shared daily copy (or "nokey" when no AV_KEY is set - the page then offers "your own key")
      case "/movers": return reply(req, await moversShared(env), 3600);
      // a person's own key: X-Key header, one call, nothing kept; only these five services
      case "/k": {
        if (siteBusy(req.headers.get("CF-Connecting-IP") || "?", "k", 20)) return reply(req, { error: "a lot of requests from you just now - try again in a minute" }, 0);
        const svc = u.searchParams.get("svc") || "", key = req.headers.get("X-Key") || "";
        if (!["ap", "av", "fd", "cd", "gn"].includes(svc) || !(svc === "ap" ? /^[A-Za-z0-9]{8,40}:[A-Za-z0-9]{8,80}$/ : /^[A-Za-z0-9_-]{8,80}$/).test(key)) return reply(req, { error: "bad request" }, 0);
        try { return reply(req, await keyed(svc, key, u), -1); } catch (e) { return reply(req, { error: scrub(String(e && e.message || e)).slice(0, 160) }, -1); }
      }
      case "/ping": return reply(req, { colo: (req.cf && req.cf.colo) || "", now: Date.now() }, 0);   // now: lets the phone check its clock (AM-CG-376)
      // the phone app's "this phone" card: who carries this connection and whether it came over IPv6 - for this answer only,
      // the address itself is never returned or kept
      // "Is it down for everyone?" - the services' own public status pages (Statuspage), read every 2 minutes and shared
      case "/status": {
        const list = await Promise.all(STATUS.map(([n, host]) => cached("st:" + host, 120, 900, async () => { const j = await get("https://" + host + "/api/v2/status.json", 120); return { i: String((j.status || {}).indicator || ""), d: String((j.status || {}).description || "").slice(0, 80) }; })
          .then(e => ({ n, i: e.v.error ? "" : e.v.i, d: e.v.error ? "" : e.v.d }))));
        return reply(req, { list }, 60);
      }
      // "Who made this device?" - only the maker part of a hardware address (first 3 bytes) is looked up; cached a day
      case "/mac": {
        if (siteBusy(req.headers.get("CF-Connecting-IP") || "?", "mac", 30)) return reply(req, { error: "a lot of requests from you just now - try again in a minute" }, 0);
        const p = (u.searchParams.get("p") || "").toUpperCase();
        if (!/^[0-9A-F]{6}$/.test(p)) return reply(req, { error: "bad prefix" }, 0);
        const e = await cached("mac:" + p, 86400, 86400 * 7, async () => {
          const r = await fetch("https://api.macvendors.com/" + p.replace(/(..)(?=.)/g, "$1:"), { signal: AbortSignal.timeout(6000) });
          if (r.status === 404) return { v: "" }; if (!r.ok) throw new Error("mac " + r.status); return { v: (await r.text()).slice(0, 80) };
        });
        return reply(req, e.v.error ? { error: "lookup failed" } : { v: e.v.v }, e.v.error ? 0 : 3600);   // a failed lookup is not "unknown maker" (AM-CG-407)
      }
      case "/me": {
        const ip = req.headers.get("CF-Connecting-IP") || "", cf = req.cf || {}, o = { org: String(cf.asOrganization || "").slice(0, 80), v6: ip.includes(":") ? 1 : 0, colo: cf.colo || "", country: cf.country || "" };
        // VPN leak check: the phone sends the address its video-call test (STUN) saw; only "same / same network" comes back
        const c = (u.searchParams.get("cmp") || "").trim().toLowerCase().slice(0, 45);
        if (c && /^[0-9a-f:.]+$/.test(c)) { const a = ip.toLowerCase(); o.fam = c.includes(":") === a.includes(":") ? 1 : 0; o.same = o.fam && c === a ? 1 : 0; o.near = o.fam && !o.same && net(c) === net(a) ? 1 : 0; }
        return reply(req, o, -1);
      }   // for the page's honest "browser -> Cloudflare" timing
      // "Is my port open?" - one TCP connection from Cloudflare to the address this request came from, and nowhere else,
      // so it can't be used to probe anyone else. TCP only; port 25 can't be reached from Workers.
      case "/port": {
        const ip = req.headers.get("CF-Connecting-IP") || "", p = Number(u.searchParams.get("p")), v6 = ip.includes(":") ? 1 : 0;
        if (!ip || !Number.isInteger(p) || p < 1 || p > 65535) return reply(req, { error: "bad port" }, 0);
        if (siteBusy(ip, "port", 10)) return reply(req, { error: "a lot of port checks from you just now - try again in a minute" }, 0);   // only ever your own address, and never a sweep
        if (p === 25) return reply(req, { v6, open: false, why: "25" }, -1);
        // never await close(): on a socket that never opened it may not settle, and the visitor would get no answer at all
        const t0 = Date.now(); let sock = null, open = false, why = "", timer = null;
        try {
          sock = connect({ hostname: ip.includes(":") ? "[" + ip + "]" : ip, port: p });
          sock.closed.catch(() => {});
          await Promise.race([sock.opened, new Promise((_, no) => { timer = setTimeout(() => no(new Error("timeout")), 5000); })]);
          open = true;
        } catch (e) { const m = String(e && e.message || e); why = /timeout/i.test(m) ? "timeout" : /cloudflare|specified address|prohibited|not allowed/i.test(m) ? "cf" : "refused"; }
        finally { if (timer) clearTimeout(timer); }
        const msTaken = Date.now() - t0; try { if (sock) sock.close().catch(() => {}); } catch (e) {}
        return reply(req, { v6, open, why, ms: open ? msTaken : null }, -1);
      }
      // "A website from outside" - status, redirects, timing and a few headers; never the page itself. Ports 80/443/8080/8443 only,
      // no addresses inside private networks; the same address is answered from memory for 30 s so it can't be used to hammer a site
      case "/site": {
        let t = (u.searchParams.get("u") || "").trim().slice(0, 300); if (!/^https?:\/\//i.test(t)) t = "https://" + t;
        let start; try { start = new URL(t); } catch (e) { return reply(req, { error: "bad address" }, 0); }
        // bug-hunt M-14: 10 checks a minute from one address (best effort: per Cloudflare location, in memory), and the
        // cache keyed without the query string, so ?x=1, ?x=2... can't be used to send many requests at a site
        if (siteBusy(req.headers.get("CF-Connecting-IP") || "?")) return reply(req, { error: "a lot of checks from you just now - try again in a minute" }, 0);
        start.search = ""; start.hash = "";
        const e = await cached("site:" + start.href, 30, 30, () => siteHops(start));
        return reply(req, Object.assign({ colo: (req.cf && req.cf.colo) || "" }, e.v.error ? { hops: [{ u: start.href.slice(0, 200), err: "couldn't check" }] } : e.v), 0);
      }
      default: return new Response("AdeeboMeter Live - see https://adeebometer.com/live.html", { status: 404 });
    }
  },
  // the schedule in wrangler.toml: stock movers once a day (and one retry), stored for everyone
  async scheduled(event, env, ctx) { ctx.waitUntil(moversDaily(env)); }
};
