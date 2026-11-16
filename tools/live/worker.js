// AdeeboMeter Live - the website's showcase data (adeebometer.com/live.html).
// A pass-through cache: each public source is fetched at most once every few minutes and every visitor gets the same copy.
// Nothing is stored or logged; visitors' addresses never reach the sources. Sources and credit are shown on the page.
const ORIGINS = ["https://adeebometer.com", "https://www.adeebometer.com"];
const FX = ["USD", "EUR", "GBP", "CNY", "JPY", "INR", "CHF", "AUD", "MXN", "HKD"];
const IDX = ["ENER", "MTLS", "FOPR", "AGRI", "BCPI"];
const memo = new Map();   // isolate memory only: key -> {t, v}

async function cached(key, ttl, make) {
  const m = memo.get(key);
  if (m && Date.now() - m.t < ttl * 1000) return m.v;
  try { const v = await make(); memo.set(key, { t: Date.now(), v }); return v; }
  catch (e) { if (m) return m.v; return { error: "source unavailable" }; }   // a broken feed only breaks its own card
}
async function get(url, ttl, type) {
  const r = await fetch(url, { cf: { cacheTtl: ttl, cacheEverything: true }, headers: { "User-Agent": "AdeeboMeter-Live (+https://adeebometer.com)" } });
  if (!r.ok) throw new Error(url + " " + r.status);
  return type === "text" ? r.text() : r.json();
}

const markets = () => cached("mkt", 900, async () => {
  const fx = await get("https://www.bankofcanada.ca/valet/observations/" + FX.map(c => "FX" + c + "CAD").join(",") + "/json?recent=2", 900);
  const ix = await get("https://www.bankofcanada.ca/valet/observations/" + IDX.map(c => "W." + c).join(",") + "/json?recent=2", 3600);
  const pick = (o, ids, pre) => {
    const rows = o.observations || [], last = rows[rows.length - 1] || {}, prev = rows[rows.length - 2] || {};
    return { date: last.d || "", items: ids.map(c => ({ c, v: +(last[pre + c] || {}).v || null, p: +(prev[pre + c] || {}).v || null })) };
  };
  return { fx: pick(fx, FX, "FX"), idx: pick(ix, IDX, "W.") };
});

const hockey = () => cached("nhl", 60, async () => {
  const d = await get("https://api-web.nhle.com/v1/score/now", 60);
  return { date: d.currentDate || "", games: (d.games || []).slice(0, 16).map(g => ({
    a: g.awayTeam && g.awayTeam.abbrev, h: g.homeTeam && g.homeTeam.abbrev,
    as: g.awayTeam && g.awayTeam.score, hs: g.homeTeam && g.homeTeam.score,
    st: g.gameState, per: g.periodDescriptor && g.periodDescriptor.number, clk: g.clock && g.clock.timeRemaining, start: g.startTimeUTC })) };
});

const news = () => cached("bbc", 600, async () => {
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

const weather = (lat, lon) => cached("w" + lat + "," + lon, 1800, async () => {
  const d = await get("https://api.open-meteo.com/v1/forecast?latitude=" + lat + "&longitude=" + lon +
    "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m,precipitation&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=4", 1800);
  return { cur: d.current || {}, daily: d.daily || {}, tz: d.timezone_abbreviation || "" };
});
const places = q => cached("g" + q, 86400, async () => {
  const d = await get("https://geocoding-api.open-meteo.com/v1/search?count=5&language=en&format=json&name=" + encodeURIComponent(q), 86400);
  return { places: (d.results || []).map(p => ({ n: p.name, r: [p.admin1, p.country].filter(Boolean).join(", "), lat: +p.latitude.toFixed(1), lon: +p.longitude.toFixed(1) })) };
});

function reply(req, body, maxAge) {
  const o = req.headers.get("Origin"), h = {
    "Content-Type": "application/json; charset=utf-8", "Cache-Control": maxAge ? "public, max-age=" + maxAge : "no-store",
    "Access-Control-Allow-Origin": ORIGINS.includes(o) ? o : ORIGINS[0], "Vary": "Origin", "X-Content-Type-Options": "nosniff" };
  return new Response(typeof body === "string" ? body : JSON.stringify(body), { headers: h });
}
const round1 = v => { const n = Math.round(parseFloat(v) * 10) / 10; return isFinite(n) && Math.abs(n) <= 180 ? n : null; };

export default {
  async fetch(req) {
    const u = new URL(req.url);
    if (req.method !== "GET") return new Response("", { status: 405 });
    switch (u.pathname) {
      case "/all": {
        const [m, h, n] = await Promise.all([markets(), hockey(), news()]);
        return reply(req, { t: Date.now(), markets: m, hockey: h, news: n }, 60);
      }
      case "/weather": {
        // the visitor's rough area from Cloudflare (city level) unless they picked a place; used for this answer only
        let lat = round1(u.searchParams.get("lat")), lon = round1(u.searchParams.get("lon")), name = (u.searchParams.get("n") || "").slice(0, 60);
        if (lat === null || lon === null) { lat = round1(req.cf && req.cf.latitude); lon = round1(req.cf && req.cf.longitude); name = (req.cf && req.cf.city) || ""; }
        if (lat === null || lon === null) return reply(req, { error: "no place" }, 0);
        return reply(req, Object.assign({ place: name, lat, lon }, await weather(lat, lon)), 600);
      }
      case "/place": {
        const q = (u.searchParams.get("q") || "").trim().slice(0, 60);
        return reply(req, q.length < 2 ? { places: [] } : await places(q.toLowerCase()), 3600);
      }
      case "/ping": return reply(req, { colo: (req.cf && req.cf.colo) || "" }, 0);   // for the page's honest "browser -> Cloudflare" timing
      default: return new Response("AdeeboMeter Live - see https://adeebometer.com/live.html", { status: 404 });
    }
  }
};
