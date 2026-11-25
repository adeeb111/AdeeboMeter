// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
// The corner widget, 1.1.6 (Adeeb, 24 Nov): presets made of tiles, and one ladder that decides what the corner shows.
//  - Presets: the eight cards as before, plus Markets, Sports, Data use, Weather and My widget (pick up to six readings).
//  - The ladder: a game you're playing (then its recap) > a pinned or followed live match (then 5 minutes of the final) >
//    rain or snow within the hour > the day plan's slot > your preset. A network problem shows on every one of them (the
//    verdict row in red, the red dot), so nothing hides an outage. "Hold this" pins what shows; "Back to my widget" sends a
//    takeover away until it ends. On battery only a game takes over.
//  - The feeds: nothing is asked unless it's on the corner or about to be - quotes in market hours at your key's pace,
//    scores while a followed game is live, the forecast hourly while the weather can show.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    static class Corner
    {
        public static readonly string[] TileStyles = { "markets", "sports", "data", "weather", "mine" };
        public static bool IsTile(string st) { return Array.IndexOf(TileStyles, st) >= 0 || st == "recap"; }
        static volatile string style = "", why = "", held = "";
        // hunters13 lifecycle ML-2: each takeover you sent away is remembered on its own (up to four), so sending a second one away
        // doesn't bring the first back
        static readonly List<string> backs = new List<string>(); static readonly Dictionary<string, DateTime> backSeen = new Dictionary<string, DateTime>();
        static string curKey = ""; static bool loaded; static DateTime loadedAt = DateTime.MinValue;
        public const string BackSep = "~~";
        // what the corner shows now: a takeover's preset, else yours
        public static string Effective { get { var h = held; if (h != "") return h; var s = style; return s == "" ? Cfg.WidgetStyle : s; } }
        public static string Why { get { return held != "" ? "held" : why; } }
        public static bool Holding { get { return held != ""; } }
        public static bool TakenOver { get { return style != ""; } }
        // the gaming spot is for the game card only - not its recap, not a hold of something else (hunters12 M-3, ML-4)
        public static bool UseGamePos { get { return why == "game" && (held == "" || held == "gamer") && Cfg.WPosGame != ""; } }

        // Hold pins the card you see: on Auto it's the card Auto is showing right now (M-3), and a held recap keeps its own
        // game (M-7). A hold and a "Back to my widget" are kept in the settings, so a restart keeps them.
        public static void Hold(bool call) { var e = Effective; held = e == "auto" ? Draw.Layout(call) : e; Save(); }
        public static void Release() { held = ""; Save(); }
        public static void Back() { if (curKey != "" && !backs.Contains(curKey)) { backs.Add(curKey); if (backs.Count > 4) backs.RemoveAt(0); backSeen[curKey] = DateTime.UtcNow; } held = ""; Save(); }
        // a held recap isn't kept over a restart on purpose: the session behind it is gone, so it would be an empty card
        static void Save() { Cfg.WHeld = held == "recap" ? "" : held; Cfg.WBack = string.Join(BackSep, backs); Cfg.Save(); }
        public static List<string> SplitBacks(string v) { return (v ?? "").Split(new[] { BackSep }, StringSplitOptions.RemoveEmptyEntries).Where(z => z.Length < 200).Distinct().Take(4).ToList(); }
        // can a sent-away takeover's absence be trusted to mean it's over? Not on battery (those rungs rest), not before the
        // scores were read since the start (a cold feed), not without a forecast (hunters13 lifecycle M-1, ML-1)
        static bool CanJudge(string key)
        {
            string kind = key.Split('|')[0];
            if (kind == "game" || kind == "recap") return true;
            if (onBattery) return false;
            if (kind == "match") { DateTime f; lock (Sports.k) f = Sports.FetchedAt; return Cfg.Sports && f > loadedAt; }
            if (kind == "weather") return Weather.Active && Weather.Fetched != DateTime.MinValue;
            return true;
        }
        // one short line for the card: why this isn't your own preset right now (hunters12 ML-3 / ML-5)
        public static string WhyLine()
        {
            switch (Why)
            {
                case "held": return held == "recap" && RecapOf != null ? L.T("Held · the match that ended at") + " " + Draw.When(RecapOf.End.ToUniversalTime()) + " - " + L.T("tray › Stop holding lets it go") : L.T("Held - tray › Stop holding lets it go");   /* hunters12 M-7: a held recap says how old it is */
                case "plan": return L.T("Day plan") + " · " + L.T(SlotOf(DateTime.Now) == "m" ? "morning" : SlotOf(DateTime.Now) == "w" ? "working hours" : SlotOf(DateTime.Now) == "e" ? "evening" : "night") + " - " + L.T("tray › Back to my widget");
                case "match": return L.T("Your team is playing - back to yours 5 min after the end");
                case "weather": return L.T("Rain or snow within the hour - back to yours once it starts");
                case "recap": return L.T("Back to yours in a minute");
            }
            return "";
        }

        // ---------- the ladder (UI thread, each tick: cheap - the heavy looks are throttled)
        static string lastPlaying; static DateTime recapLook = DateTime.MinValue, recapUntil = DateTime.MinValue, nextLook = DateTime.MinValue, battAt = DateTime.MinValue;
        static bool onBattery; static string matchKey = ""; static DateTime matchDoneAt = DateTime.MinValue;
        public static GameSession RecapOf;
        static string cMatch = "", cWeather = "";
        public static void Tick(Snap s)
        {
            var now = DateTime.UtcNow;
            if (!loaded) { loaded = true; loadedAt = now; held = Known(Cfg.WHeld) && Cfg.WHeld != "auto" ? Cfg.WHeld : ""; backs.Clear(); backs.AddRange(SplitBacks(Cfg.WBack)); foreach (var b in backs) backSeen[b] = now; }
            if ((now - battAt).TotalSeconds > 30) { battAt = now; try { onBattery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline; } catch (Exception e) { if (e is OutOfMemoryException) throw; onBattery = false; } }
            // a game: from any preset; when it closes, its recap for a minute
            string playing = Cfg.GameWidget ? GamePing.Me.Playing : null; bool game = playing != null && Draw.HasGamerTarget();
            // the recap: the session that just closed (it may be filed a moment after the game goes - looked for 15 s)
            if (lastPlaying != null && GamePing.Me.Playing == null) recapLook = now.AddSeconds(15);
            if (now < recapLook && held != "recap") { var ls = GamePing.Me.LastSession; if (ls != null && !ls.Open && ls != RecapOf && (DateTime.Now - ls.End).TotalSeconds < 60 && (ls.End - ls.Start).TotalMinutes >= 2) { RecapOf = ls; recapUntil = now.AddSeconds(60); recapLook = DateTime.MinValue; } }
            lastPlaying = playing;
            if (now >= nextLook) { nextLook = now.AddSeconds(5); cMatch = MatchNow(now); var wet = RainSoon(now); cWeather = wet == DateTime.MinValue ? "" : wet.Ticks.ToString(CultureInfo.InvariantCulture); }
            // every rung that wants the corner, best first; one sent away is skipped (and the next one down gets it) until its own
            // event is over - not until something else happens to show (hunters12 M-2 / L-5: the weather's key is its rain, not the hour)
            var rungs = new List<string[]>();
            if (game) rungs.Add(new[] { "gamer", "game", "game|" + playing });
            if (now < recapUntil && RecapOf != null) rungs.Add(new[] { "recap", "recap", "recap|" + RecapOf.Start.Ticks });
            if (!onBattery && cMatch != "") rungs.Add(new[] { "sports", "match", "match|" + cMatch });
            if (!onBattery && cWeather != "") rungs.Add(new[] { "weather", "weather", "weather|" + cWeather });
            if (!onBattery) { var p = PlanNow(DateTime.Now); if (p != "") rungs.Add(new[] { p, "plan", "plan|" + SlotOf(DateTime.Now) + "|" + p }); }
            // a sent-away takeover is forgotten once it has really been gone for a minute - not while its feed is cold or its rung
            // rests on battery (hunters13 lifecycle M-1, ML-1)
            bool forgot = false;
            foreach (var b in backs.ToList())
            {
                DateTime seen; if (!backSeen.TryGetValue(b, out seen)) backSeen[b] = seen = now;
                if (rungs.Any(r => r[2] == b) || !CanJudge(b)) backSeen[b] = now;
                else if ((now - seen).TotalSeconds > 60) { backs.Remove(b); backSeen.Remove(b); forgot = true; }
            }
            if (forgot) Save();
            var win = rungs.FirstOrDefault(r => !backs.Contains(r[2]) && r[0] != Cfg.WidgetStyle);   // L-1: a takeover that is your own preset changes nothing
            string st = win == null ? "" : win[0], w = win == null ? "" : win[1], key = win == null ? "" : win[2];
            if (win == null && game && Cfg.WidgetStyle == "gamer" && !backs.Contains("game|" + playing)) w = "game";   // your own preset is Gamer: still the gaming spot - but a game you sent away doesn't park your card there (hunters13 lifecycle L-4)
            curKey = key; style = st; why = w;
        }

        // ---------- sports: the pinned game, else a followed team live in a ticked sport; the final stays 5 minutes
        public static string PinKey(Sports.Game g) { return g.Sport + "|" + g.Home + "|" + g.Away + "|" + g.StartUtc.Ticks.ToString(CultureInfo.InvariantCulture); }
        public static bool Wanted(Sports.Game g)
        {
            if (Cfg.WPinGame != "" && PinKey(g) == Cfg.WPinGame) return true;
            return Kinds().Contains(g.Sport) && Sports.Mine(g);
        }
        public static HashSet<string> Kinds() { return new HashSet<string>((Cfg.WSportKinds ?? "").Split(',').Select(x => x.Trim()).Where(x => x != "")); }
        public static Sports.Game MatchGame()
        {
            List<Sports.Game> gs; lock (Sports.k) gs = Sports.Games.ToList();
            var pin = Cfg.WPinGame == "" ? null : gs.FirstOrDefault(g => PinKey(g) == Cfg.WPinGame && (g.State == "live" || g.State == "done"));
            if (pin != null && pin.State == "done" && matchKey == "" && (DateTime.UtcNow - pin.StartUtc).TotalHours < 6) matchKey = PinKey(pin);   // ML-7: started after the whistle - its final still shows once
            if (pin != null && (pin.State == "live" || PinKey(pin) == matchKey)) return pin;
            var live = gs.Where(g => g.State == "live" && Wanted(g)).OrderBy(g => g.StartUtc).FirstOrDefault();
            if (live != null) return live;
            return matchKey == "" ? null : gs.FirstOrDefault(g => PinKey(g) == matchKey);
        }
        static string MatchNow(DateTime now)
        {
            if (!Cfg.Sports || (Cfg.WPinGame == "" && Kinds().Count == 0)) { matchKey = ""; return ""; }
            var g = MatchGame(); if (g == null) { matchKey = ""; return ""; }
            string k = PinKey(g);
            if (g.State == "live") { matchKey = k; matchDoneAt = DateTime.MinValue; return k; }
            if (g.State == "done" && k == matchKey)
            {
                if (matchDoneAt == DateTime.MinValue) matchDoneAt = now;
                if ((now - matchDoneAt).TotalMinutes < 5) return k;
                if (Cfg.WPinGame == k) { Cfg.WPinGame = ""; Cfg.Save(); }   // the pinned game is over: the pin goes
            }
            matchKey = ""; return "";
        }
        // ---------- weather: rain, snow or a storm starting within the hour (forecast hours), while it isn't already
        // the hour the rain starts (UTC), or MinValue - the same rain keeps the same key across the clock's hour
        public static DateTime RainSoon(DateTime now)
        {
            if (!Cfg.WRainSoon || !Weather.Active) return DateTime.MinValue;
            var cur = Weather.Now(now); string ck = Weather.Kind(cur); if (ck == "rain" || ck == "snow" || ck == "storm") return DateTime.MinValue;
            var h = Weather.Ahead(now, 3).FirstOrDefault(z => z.Utc > now && (z.Utc - now).TotalMinutes <= 60 && (Weather.Kind(z) == "rain" || Weather.Kind(z) == "snow" || Weather.Kind(z) == "storm"));
            return h == null ? DateTime.MinValue : h.Utc;
        }
        // ---------- the day plan: four slots, each a preset or nothing - "m=weather;w=;e=sports;n="
        public static readonly string[] Slots = { "m", "w", "e", "n" };
        public static string SlotOf(DateTime local) { int h = local.Hour; return h >= 7 && h < 9 ? "m" : h >= 9 && h < 17 ? "w" : h >= 17 && h < 23 ? "e" : "n"; }
        public static string PlanGet(string slot)
        {
            foreach (var part in (Cfg.WPlan ?? "").Split(';')) { var kv = part.Split('='); if (kv.Length == 2 && kv[0] == slot) return kv[1]; }
            return "";
        }
        public static void PlanSet(string slot, string st)
        {
            Cfg.WPlan = string.Join(";", Slots.Select(z => z + "=" + (z == slot ? st : PlanGet(z))));
        }
        public static string PlanNow(DateTime local) { var p = PlanGet(SlotOf(local)); return p == "" || !Known(p) ? "" : p; }
        public static string CleanPlan(string v)
        {
            var d = new Dictionary<string, string>(); foreach (var part in (v ?? "").Split(';')) { var kv = part.Split('='); if (kv.Length == 2 && Array.IndexOf(Slots, kv[0]) >= 0 && (kv[1] == "" || Known(kv[1]))) d[kv[0]] = kv[1]; }
            return string.Join(";", Slots.Select(z => z + "=" + (d.ContainsKey(z) ? d[z] : "")));
        }
        public static bool Known(string st) { return Array.IndexOf(AllStyles, st) >= 0; }
        public static readonly string[] AllStyles = { "speed", "speedstatus", "speedapps", "everyday", "gamer", "calls", "engineer", "auto", "markets", "sports", "data", "weather", "mine" };

        // ---------- the feeds: asked only for what the corner shows or may show soon (AppCtx, every ~15 s)
        static DateTime feedsAt = DateTime.MinValue, closedLook = DateTime.MinValue;
        public static void Feeds()
        {
            var now = DateTime.UtcNow; if ((now - feedsAt).TotalSeconds < 15) return; feedsAt = now;
            // ML-3: what shows now and your own preset both stay fresh - a long takeover doesn't leave your card hours old
            string st = Effective, mineSt = Cfg.WidgetStyle;
            var tiles = st == "mine" || mineSt == "mine" ? Mine() : new List<string>();
            bool marketsUp = st == "markets" || mineSt == "markets" || tiles.Any(t => t.StartsWith("market", StringComparison.Ordinal));
            if (marketsUp && !Policy.NoOnline)
            {
                var syms = Syms();
                // hunters12 data M3: each symbol by its own exchange's hours (London's FTSE in London's morning, futures nearly round the
                // clock); hunters13 data M1: a symbol the service never prices (a typo, delisted) is dropped for the day after two misses,
                // and a closed one is looked at once an hour at most - one bad symbol can't spend the day's free calls
                var fmp = syms.Where(z => !z.StartsWith("FX:", StringComparison.Ordinal) && !MarketData.Dead(z)).ToList();
                bool lookClosed = (now - closedLook).TotalMinutes >= 60;
                List<string> ask; lock (MarketData.k) ask = fmp.Where(z => MarketData.OpenNow(z, now) || (lookClosed && !MarketData.Quotes.ContainsKey(z))).ToList();
                if (ask.Count > 0) { if (ask.Any(z => !MarketData.OpenNow(z, now))) closedLook = now; MarketData.FetchWidget(ask, null); }
                foreach (var fx in syms.Where(z => z.StartsWith("FX:", StringComparison.Ordinal))) { MarketData.Load(fx); if (MarketData.Stale(fx)) MarketData.Fetch(fx, null); }
            }
            if ((Cfg.WPinGame != "" || Kinds().Count > 0 || st == "sports" || mineSt == "sports" || tiles.Contains("team")) && Sports.Teams().Count + (Cfg.WPinGame != "" ? 1 : 0) > 0)
                Sports.WidgetTick(g => Wanted(g) || ((st == "sports" || mineSt == "sports" || tiles.Contains("team")) && Sports.Mine(g)));
            Weather.Hourly = st == "weather" || mineSt == "weather" || Cfg.WRainSoon || tiles.Contains("weather") || Slots.Any(z => PlanGet(z) == "weather");
        }
        public static List<string> Syms() { return (Cfg.WSyms ?? "").Split(',').Select(z => z.Trim()).Where(z => z != "" && (z.StartsWith("FX:", StringComparison.Ordinal) ? MarketData.KnownFx(z.Substring(3)) : MarketData.ValidSym(z))).Distinct().Take(4).ToList(); }
        public static string CleanSyms(string v)
        {
            var l = (v ?? "").Split(',').Select(z => z.Trim()).Select(z => z.StartsWith("fx:", StringComparison.OrdinalIgnoreCase) ? "FX:" + z.Substring(3).ToUpperInvariant() : z.ToUpperInvariant())
                .Where(z => z != "" && (z.StartsWith("FX:", StringComparison.Ordinal) ? MarketData.KnownFx(z.Substring(3)) : MarketData.ValidSym(z))).Distinct().Take(4);
            return string.Join(",", l);
        }
        // ---------- My widget: up to six readings
        public static readonly string[][] Fields = {
            new[] { "verdict", "The verdict" }, new[] { "down", "Download" }, new[] { "up", "Upload" }, new[] { "ping", "Internet ping" },
            new[] { "router", "Router ping" }, new[] { "dns", "DNS lookup" }, new[] { "jitter", "Jitter" }, new[] { "loss", "Packet loss" },
            new[] { "cpu", "CPU" }, new[] { "mem", "Memory" }, new[] { "disk", "Disk" }, new[] { "apps", "Busiest app" },
            new[] { "today", "Data today" }, new[] { "month", "Data this month" }, new[] { "game", "Game ping" }, new[] { "weather", "Weather" },
            new[] { "market1", "First market" }, new[] { "market2", "Second market" }, new[] { "team", "My team" }, new[] { "clock", "Time" } };
        // "none": every row set to (nothing) - an empty My widget, not the defaults back (hunters12 ML-2)
        // hunters13 lifecycle ML-4: six rows, each in its own place ("down,,cpu" - row 2 empty), so a reading set on row 3 stays on row 3
        public static List<string> Mine() { return MineSlots().Where(z => z != "").ToList(); }
        public static List<string> MineSlots()
        {
            var l = new List<string>(); if (Cfg.WMine != "none") foreach (var z0 in CleanMine(Cfg.WMine).Split(',')) l.Add(z0);
            while (l.Count < 6) l.Add(""); return l.Take(6).ToList();
        }
        public static string CleanMine(string v)
        {
            if (v == "none") return "none";
            var l = new List<string>();
            foreach (var z0 in (v ?? "").Split(',').Take(6)) { var z = z0.Trim(); l.Add(Fields.Any(f => f[0] == z) && !l.Contains(z) ? z : ""); }
            while (l.Count > 0 && l[l.Count - 1] == "") l.RemoveAt(l.Count - 1);
            return l.Count == 0 ? "down,ping,weather,market1" : string.Join(",", l);
        }
        // a row set from Settings: the same reading elsewhere moves here; all rows empty is "none"
        public static string SetMine(int slot, string f)
        {
            var l = MineSlots(); if (f != null) for (int i = 0; i < l.Count; i++) if (l[i] == f) l[i] = "";
            l[slot] = f ?? ""; while (l.Count > 0 && l[l.Count - 1] == "") l.RemoveAt(l.Count - 1);
            return l.Count == 0 ? "none" : string.Join(",", l);
        }

        // ---------- "since you looked": what changed while you weren't hovering (built when a hover starts)
        static DateTime lookAt = DateTime.MinValue; static Dictionary<string, double> lookPx = new Dictionary<string, double>(); static Dictionary<string, string> lookScore = new Dictionary<string, string>(); static string lookWx = "";
        public static void Looked()
        {
            lookAt = DateTime.Now; lookPx = new Dictionary<string, double>(); lookScore = new Dictionary<string, string>();
            lock (MarketData.k) foreach (var q in MarketData.Quotes) lookPx[q.Key] = q.Value.Price;
            lock (Sports.k) foreach (var g in Sports.Games) lookScore[PinKey(g)] = g.State + "|" + g.HomeScore + "-" + g.AwayScore;
            lookWx = Weather.Active ? WxNext() ?? "" : "";
        }
        static string WxNext() { var t = Weather.Text(DateTime.UtcNow); if (t == null) return null; int i = t.IndexOf(" · ", StringComparison.Ordinal); return i < 0 ? "" : t.Substring(i + 3); }
        public static string Since(Snap s)
        {
            if (!Cfg.WSince || lookAt == DateTime.MinValue || (DateTime.Now - lookAt).TotalMinutes < 2) return "";
            var parts = new List<string>();
            var evs = (s.Recent ?? new List<Timeline.Ev>()).Where(e => e.T > lookAt && e.Lv >= 1).ToList();
            if (evs.Count > 0) parts.Add(Draw.Trunc(evs[0].Text, 40) + (evs.Count > 1 ? " +" + (evs.Count - 1).ToString(CultureInfo.CurrentCulture) : ""));
            lock (Sports.k) foreach (var g in Sports.Games.Where(z => (z.State == "live" || z.State == "done") && Sports.Mine(z)))
                { string was; string k = PinKey(g); if (lookScore.TryGetValue(k, out was) && was != g.State + "|" + g.HomeScore + "-" + g.AwayScore) parts.Add(g.Home + " " + g.HomeScore + "–" + g.AwayScore + " " + g.Away + (g.State == "done" ? " · " + L.T("Final") : "")); }
            lock (MarketData.k) foreach (var sym in Syms())
                { MarketData.Quote q; double was; if (MarketData.Quotes.TryGetValue(sym, out q) && lookPx.TryGetValue(sym, out was) && was > 0) { double d = (q.Price - was) / was * 100; if (Math.Abs(d) >= 0.5) parts.Add(Short(q) + " " + (d > 0 ? "+" : "") + d.ToString("0.0", CultureInfo.CurrentCulture) + "%"); } }
            if (Weather.Active) { var wx = WxNext(); if (wx != null && wx != "" && lookWx != "" && wx != lookWx) parts.Add(wx); }
            if (parts.Count == 0) return "";
            return L.T("Since you looked:") + " " + string.Join(" · ", parts.Take(3));
        }
        public static string Short(MarketData.Quote q)
        {
            if (q.Sym == "^GSPC") return "S&P"; if (q.Sym == "^GSPTSE") return "TSX"; if (q.Sym == "^IXIC") return "Nasdaq"; if (q.Sym == "^DJI") return "Dow";
            if (q.Sym == "GCUSD") return L.T("Gold"); if (q.Sym == "CLUSD") return L.T("Oil");
            return q.Sym.TrimStart('^');
        }
    }

    // ================================================================== the tiles
    sealed class Tile { public string Label = "", Value = "", Sub = ""; public int Lv = 3; public double[] Series; public bool Big, Moved; public Color? Tint; }

    static partial class Draw
    {
        public static bool HasGamerTarget() { return GamerTarget() != null; }
        static string Title(string st, out List<Tile> tiles, Snap s)
        {
            tiles = new List<Tile>();
            switch (st)
            {
                case "markets":
                    foreach (var sym in Corner.Syms()) tiles.Add(MarketTile(sym, true));
                    if (tiles.Count == 0) tiles.Add(new Tile { Label = L.T("No markets picked"), Sub = L.T("Settings › Widget › Markets on the widget") });
                    else { var qn = QuoteWhy(); if (qn != "" && tiles.Any(z => z.Value != "-")) tiles.Add(new Tile { Label = qn, Lv = 1 }); }   // hunters12 data ML4: why the prices are old
                    return L.T("Markets") + AsOf();
                case "sports":
                    {
                        var main = Corner.MatchGame(); List<Sports.Game> gs; lock (Sports.k) gs = Sports.Games.Where(Sports.Mine).ToList();
                        if (main != null) tiles.Add(GameTile(main, true));
                        foreach (var g in gs.Where(z => main == null || Corner.PinKey(z) != Corner.PinKey(main)).OrderBy(z => z.State == "live" ? 0 : z.State == "soon" ? 1 : 2).ThenBy(z => z.State == "done" ? -z.StartUtc.Ticks : z.StartUtc == DateTime.MinValue ? long.MaxValue : z.StartUtc.Ticks).Take(main == null ? 4 : 3)) tiles.Add(GameTile(g, false));
                        if (tiles.Count == 0) tiles.Add(!Cfg.Sports ? new Tile { Label = L.T("Sports is off"), Sub = L.T("Sports is off - turn it on from the Sports page") } : new Tile { Label = Sports.Teams().Count == 0 ? L.T("No teams followed yet") : NoGames(), Sub = Sports.Teams().Count == 0 ? L.T("Add your teams on the Sports page") : NoGamesWhy() });   // lifecycle L-6; hunters12 data ML5: not fetched yet, or the feed failed, says so
                        return L.T("My teams");
                    }
                case "data":
                    {
                        var td = Usage.TodayDU(); double today = td != null && td.Length > 1 ? td[0] + td[1] : double.NaN, month = Usage.Month(null);
                        tiles.Add(new Tile { Label = L.T("Today"), Value = double.IsNaN(today) ? "-" : Usage.Mb(today), Big = true });
                        var mt = new Tile { Label = L.T("This month"), Value = Usage.Mb(month) + (Cfg.CapGB > 0 ? " / " + Cfg.CapGB + " GB" : "") };
                        if (Cfg.CapGB > 0) { double pct = month / 1000 / Cfg.CapGB; mt.Lv = pct >= 1 ? 2 : pct >= 0.8 ? 1 : 0; mt.Series = new[] { Math.Min(1, pct) }; }
                        tiles.Add(mt);
                        var t = DateTime.Now; int dim = DateTime.DaysInMonth(t.Year, t.Month); double ran = (t - new DateTime(t.Year, t.Month, 1)).TotalDays, pace = ran >= 1 ? month / ran * dim : double.NaN;
                        if (ran >= 2 && !double.IsNaN(pace)) tiles.Add(new Tile { Label = L.T("At this pace"), Value = Usage.Mb(pace), Sub = L.T("by the end of the month"), Lv = Cfg.CapGB > 0 && pace / 1000 > Cfg.CapGB ? 1 : 3 });
                        tiles.Add(new Tile { Label = L.T("Right now"), Value = Pair(s.Down, s.Up) });
                        return L.T("Data use");
                    }
                case "weather":
                    {
                        var now = DateTime.UtcNow; var h = Weather.Active ? Weather.Now(now) : null;
                        if (h == null) { tiles.Add(new Tile { Label = L.T("No forecast yet"), Sub = Cfg.WeatherOn ? L.T("It comes with the next look") : L.T("Turn Weather on in Settings › Weather") }); return L.T("Weather"); }
                        var fat = Weather.Fetched; string age = fat != DateTime.MinValue && (now - fat).TotalHours >= 3 ? " · " + L.T("forecast from") + " " + When(fat) : "";
                        tiles.Add(new Tile { Label = Weather.PlaceName(), Value = Weather.Temp(h.Temp), Sub = L.T(Weather.Words(h.Code)) + age, Big = true });
                        string wk = Weather.Kind(h); bool wetNow = wk == "rain" || wk == "snow" || wk == "storm";
                        var t = Weather.Text(now); int i = t == null ? -1 : t.IndexOf(" · ", StringComparison.Ordinal); if (i > 0) tiles.Add(new Tile { Label = wetNow ? L.T("Now") : L.T("Next"), Value = t.Substring(i + 3), Lv = Corner.RainSoon(now) != DateTime.MinValue ? 1 : 3 });
                        foreach (var a in Weather.Ahead(now, 12).Where(z => z.Utc > now).Where((z, k) => k % 3 == 2).Take(3))
                            tiles.Add(new Tile { Label = a.Utc.ToLocalTime().ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H") ? "HH:mm" : Tt("h tt"), CultureInfo.CurrentCulture).ToLowerInvariant(), Value = Weather.TempShort(a.Temp) + (Weather.Words(a.Code) != "" ? " " + L.T(Weather.Words(a.Code)) : "") });
                        if (!string.IsNullOrEmpty(Weather.Note)) tiles.Add(new Tile { Label = L.T(Weather.Note), Lv = 1 });
                        return L.T("Weather");
                    }
                case "recap":
                    {
                        var ss = Corner.RecapOf; var rows = ss == null ? new List<GameSession.Sec>() : ss.All();
                        var all = rows.Select(r => r.Game).Where(x => double.IsNaN(x) || x > 0).ToArray(); var v = all.Where(x => !double.IsNaN(x)).ToArray();
                        double loss = GameStat.Loss(all);   /* hunters12 data M4: a lost reply is a lost reply - 40% loss at 30 ms isn't Competitive */
                        double avg = v.Length == 0 ? double.NaN : v.Average(), worst = v.Length == 0 ? double.NaN : v.Max();
                        List<Spike> sp; lock (GamePing.Me.Spikes) sp = ss == null ? new List<Spike>() : GamePing.Me.Spikes.Where(z => z.Game == ss.GameId && z.T >= ss.Start && z.T <= ss.End).ToList();
                        double jit = GameStat.Jitter(v); string grade = v.Length == 0 ? "No reply" : GameStat.Grade(avg, jit, double.IsNaN(loss) ? 0 : loss);
                        tiles.Add(new Tile { Label = L.T("Average"), Value = Fmt(avg, "0", " ms"), Big = true, Lv = grade == "Competitive" ? 0 : grade == "Casual" ? 1 : grade == "Laggy" ? 2 : 3 });
                        tiles.Add(new Tile { Label = L.T("Worst"), Value = Fmt(worst, "0", " ms") });
                        if (!double.IsNaN(loss) && loss > 0) tiles.Add(new Tile { Label = L.T("Packet loss"), Value = Fmt(loss, "0.#", "%"), Lv = loss >= 3 ? 2 : loss >= 1 ? 1 : 3 });
                        var blames = sp.GroupBy(z => z.Blame).OrderByDescending(z => z.Count()).Select(z => L.T(z.Key)).ToList();
                        tiles.Add(new Tile { Label = L.T("Spikes"), Value = sp.Count.ToString(CultureInfo.CurrentCulture) + (blames.Count > 0 ? " · " + blames[0] : ""), Lv = sp.Count == 0 ? 0 : sp.Count > 4 ? 1 : 3 });
                        tiles.Add(new Tile { Label = L.T("Grade"), Value = L.T(grade) });
                        int mins = ss == null ? 0 : (int)Math.Round((ss.End - ss.Start).TotalMinutes);
                        return L.T("Match recap") + (ss == null ? "" : " · " + ss.GameName + " · " + mins.ToString(CultureInfo.CurrentCulture) + " " + L.T("min"));
                    }
                default:   // mine
                    foreach (var f in Corner.Mine()) { var t = FieldTile(f, s); if (t != null) tiles.Add(t); }
                    if (tiles.Count == 0) tiles.Add(new Tile { Label = L.T("No readings picked"), Sub = L.T("Pick up to six in Settings › Widget") });
                    return L.T("My widget");
            }
        }
        static string AsOf()
        {
            // the oldest price on the card, so one fresh symbol can't hide a stale one (hunters12 data ML10)
            DateTime last = DateTime.MaxValue; lock (MarketData.k) foreach (var sym in Corner.Syms()) { MarketData.Quote q; if (MarketData.Quotes.TryGetValue(sym, out q) && q.When < last) last = q.When; }
            if (last == DateTime.MaxValue) return "";
            return " · " + L.T("as of") + " " + When(last);
        }
        // "16:00" today, "Fri 16:00" this week, "13 Nov 16:00" before that (hunters13 display L-6: a weekday alone can be a week out)
        public static string When(DateTime utc)
        {
            var lt = utc.ToLocalTime(); string tm = lt.ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H") ? "HH:mm" : Tt("h:mm tt"), CultureInfo.CurrentCulture);
            return lt.Date == DateTime.Today ? tm : (DateTime.Today - lt.Date).TotalDays < 6 ? lt.ToString("ddd", CultureInfo.CurrentCulture) + " " + tm : lt.ToString("d MMM", CultureInfo.CurrentCulture) + " " + tm;
        }
        // the quote service's reason, in a few words, when the prices are old
        static string QuoteWhy()
        {
            string n; lock (MarketData.k) n = MarketData.QuoteNote ?? "";
            if (n.StartsWith("Today's free calls", StringComparison.Ordinal)) return L.T("Today's free calls are used up - prices are from earlier");
            if (n.IndexOf("didn't accept", StringComparison.Ordinal) >= 0) return L.T("The quote service didn't accept your key - Settings › Markets");
            if (n.StartsWith("Couldn't reach", StringComparison.Ordinal)) return L.T("Couldn't reach the quote service - it tries again soon");
            return "";
        }
        static string NoGames() { DateTime f; lock (Sports.k) f = Sports.FetchedAt; return f == DateTime.MinValue ? L.T("Looking for today's games...") : L.T("No games today"); }
        static string NoGamesWhy()
        {
            lock (Sports.k) foreach (var kv in Sports.Note) if (!kv.Key.StartsWith("news-", StringComparison.Ordinal) && kv.Value != "" && kv.Value != "nokey") return L.T("A scores feed couldn't be read - the Sports page says which");
            return "";
        }
        static Tile MarketTile(string sym, bool graph)
        {
            if (sym.StartsWith("FX:", StringComparison.Ordinal))
            {
                var ser = MarketData.Get(sym); string code = sym.Substring(3);
                var t = new Tile { Label = code + "/CAD" };
                if (ser == null || ser.Count == 0) { t.Value = "-"; t.Sub = L.T("Bank of Canada, daily"); return t; }
                double pct = MarketData.ChangeOver(ser, TimeSpan.FromDays(1)); int dir = Dir(pct);
                t.Value = ser.Last.ToString(ser.Last < 10 ? "0.0000" : "0.00", CultureInfo.CurrentCulture) + (double.IsNaN(pct) ? "" : "  " + Signed(pct) + " " + L.T("day"));   /* hunters12 data L2: which change it is */
                t.Lv = dir == 0 ? 3 : dir > 0 ? 0 : 2; t.Tint = dir == 0 ? (Color?)null : dir > 0 ? Theme.LvText(0) : Theme.LvText(2);
                if (graph) t.Series = ser.V.Skip(Math.Max(0, ser.Count - 30)).ToArray();
                t.Sub = L.T("Bank of Canada") + " · " + (ser.LastDay.Date == DateTime.Today ? L.T("today") : ser.LastDay.ToString((DateTime.Today - ser.LastDay.Date).TotalDays < 6 ? "ddd" : "d MMM", CultureInfo.CurrentCulture));
                return t;
            }
            MarketData.Quote q; List<double> h; lock (MarketData.k) { MarketData.Quotes.TryGetValue(sym, out q); MarketData.Hist.TryGetValue(sym, out h); h = h == null ? null : h.ToList(); }
            var tq = new Tile { Label = q != null && !string.IsNullOrEmpty(q.Name) ? Trunc(q.Name, 22) : sym.TrimStart('^') };
            if (q == null) { tq.Value = "-"; tq.Sub = Policy.NoOnline ? L.T("Online lookups are off on this PC (your IT team's policy)") : !MarketData.HasQuoteKey() ? L.T("Needs your Financial Modeling Prep key (Settings › Markets)") : MarketData.Dead(sym) ? L.T("No price for this symbol - check it in Settings › Widget") : QuoteWhy() != "" ? QuoteWhy() : L.T("Waiting for the first price"); return tq; }   // lifecycle L-5; hunters13 data M1
            tq.Value = q.Price.ToString(q.Price >= 1000 ? "#,0.00" : "0.00", CultureInfo.CurrentCulture) + (double.IsNaN(q.Pct) ? "" : "  " + Signed(q.Pct) + " " + L.T("day"));   /* hunters12 L3: cents kept past $1,000 */
            int qd = Dir(q.Pct); tq.Tint = qd == 0 ? (Color?)null : qd > 0 ? Theme.LvText(0) : Theme.LvText(2);
            if (graph && h != null && h.Count >= 2) tq.Series = h.ToArray();
            return tq;
        }
        // hunters13 data L4: the sign of what's shown - a -0.04% move is "0.0%", not "−0.0%"
        static string Signed(double pct) { double r = Math.Round(pct, 1); return (r > 0 ? "+" : r < 0 ? "−" : "") + Math.Abs(r).ToString("0.0", CultureInfo.CurrentCulture) + "%"; }
        static int Dir(double pct) { if (double.IsNaN(pct)) return 0; double r = Math.Round(pct, 1); return r > 0 ? 1 : r < 0 ? -1 : 0; }
        static Tile GameTile(Sports.Game g, bool main)
        {
            var t = new Tile { Big = main };
            if (g.State == "soon" && g.StartUtc == DateTime.MinValue) { t.Label = g.Home + " " + L.T("vs") + " " + g.Away; t.Value = "-"; t.Sub = L.T("Start time not given yet"); return t; }
            if (g.State == "soon") { t.Label = g.Home + " " + L.T("vs") + " " + g.Away; t.Value = g.StartUtc.ToLocalTime().ToString((g.StartUtc.ToLocalTime().Date == DateTime.Today ? "" : "ddd ") + (CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H") ? "HH:mm" : Tt("h:mm tt")), CultureInfo.CurrentCulture); return t; }
            t.Label = g.Home + " " + g.HomeScore + " – " + g.AwayScore + " " + g.Away;
            t.Value = g.State == "live" ? L.T("Live") : g.State == "done" ? L.T("Final") : FullView.OffWord(g);
            t.Lv = g.State == "live" ? 2 : 3; t.Tint = g.State == "live" ? Theme.LvText(2) : (Color?)null;
            t.Sub = g.State == "live" ? g.Detail : "";
            return t;
        }
        static Tile FieldTile(string f, Snap s)
        {
            switch (f)
            {
                case "verdict": return new Tile { Label = s.Headline, Lv = s.Level };
                case "down": return new Tile { Label = L.T("Download"), Value = Rate(s.Down, true), Series = s.DownHist.Skip(Math.Max(0, s.DownHist.Length - 30)).ToArray() };
                case "up": return new Tile { Label = L.T("Upload"), Value = Rate(s.Up, true), Series = s.UpHist.Skip(Math.Max(0, s.UpHist.Length - 30)).ToArray() };
                case "ping": return new Tile { Label = L.T("Internet ping"), Value = Fmt(s.Ping, "0", " ms"), Lv = s.Path.Length > 4 ? s.Path[4] : 3 };
                case "router": return new Tile { Label = L.T("Router ping"), Value = Fmt(s.GwMs, "0", " ms"), Lv = s.Path.Length > 1 ? s.Path[1] : 3 };
                case "dns": return new Tile { Label = L.T("DNS lookup"), Value = Fmt(s.DnsMs, "0", " ms"), Lv = s.Path.Length > 3 ? s.Path[3] : 3 };
                case "jitter": return new Tile { Label = L.T("Jitter"), Value = Fmt(s.Jitter, "0.0", " ms") };
                case "loss": return new Tile { Label = L.T("Packet loss"), Value = Fmt(s.LossPct, "0.#", "%"), Lv = double.IsNaN(s.LossPct) ? 3 : s.LossPct >= 3 ? 2 : s.LossPct >= 1 ? 1 : 0 };
                case "cpu": return new Tile { Label = L.T("CPU"), Value = Fmt(s.Cpu, "0", "%"), Lv = double.IsNaN(s.Cpu) ? 3 : s.Cpu >= 90 ? 1 : 0 };
                case "mem": return new Tile { Label = L.T("Memory"), Value = Fmt(s.Mem, "0", "%"), Lv = double.IsNaN(s.Mem) ? 3 : s.Mem >= 85 ? 1 : 0 };
                case "disk": return new Tile { Label = L.T("Disk"), Value = Fmt(s.Disk, "0", "%") };
                case "apps": { var a = s.TopCpu.FirstOrDefault(z => z.Name != "AdeeboMeter"); return new Tile { Label = L.T("Busiest app"), Value = a == null ? "-" : Trunc(a.Name, 18) + " " + Fmt(a.Cpu, "0", "%") }; }
                case "today": { var td = Usage.TodayDU(); return new Tile { Label = L.T("Data today"), Value = td != null && td.Length > 1 ? Usage.Mb(td[0] + td[1]) : "-" }; }
                case "month": return new Tile { Label = L.T("Data this month"), Value = Usage.Mb(Usage.Month(null)) + (Cfg.CapGB > 0 ? " / " + Cfg.CapGB + " GB" : "") };
                case "game":
                    {
                        var t = GamerTarget(); if (t == null) return new Tile { Label = L.T("Game ping"), Value = "-", Sub = L.T("Add a game in the Gamer Zone") };
                        var v = GamePing.Me.Stat(t.Item1.Id, t.Item2).Window(60, 3600); double now = v.Length > 0 ? v[v.Length - 1] : double.NaN;
                        return new Tile { Label = t.Item1.Name, Value = Fmt(now, "0", " ms") };
                    }
                case "weather": { var w = Weather.Active ? Weather.Text(DateTime.UtcNow) : null; return new Tile { Label = L.T("Weather"), Value = w ?? "-", Sub = w != null ? "" : Cfg.WeatherOn ? L.T("It comes with the next look") : L.T("Turn Weather on in Settings › Weather") }; }   // L-7
                case "market1": case "market2": { var l = Corner.Syms(); int i = f == "market1" ? 0 : 1; return i < l.Count ? MarketTile(l[i], false) : new Tile { Label = L.T("Market"), Value = "-", Sub = L.T("Pick one in Settings › Widget") }; }
                case "team":
                    {
                        List<Sports.Game> gs; lock (Sports.k) gs = Sports.Games.Where(Sports.Mine).ToList();
                        var g = gs.OrderBy(z => z.State == "live" ? 0 : z.State == "soon" ? 1 : 2).ThenBy(z => z.State == "done" ? -z.StartUtc.Ticks : z.StartUtc == DateTime.MinValue ? long.MaxValue : z.StartUtc.Ticks).FirstOrDefault();   /* hunters13 data L3: the latest final, as the sports card */
                        return g == null ? new Tile { Label = L.T("My team"), Value = "-", Sub = !Cfg.Sports ? L.T("Sports is off - turn it on from the Sports page") : Sports.Teams().Count == 0 ? L.T("Add your teams on the Sports page") : L.T("No games today") } : GameTile(g, false);   // L-7
                    }
                case "clock": return new Tile { Label = DateTime.Now.ToString("ddd d MMM", CultureInfo.CurrentCulture), Value = DateTime.Now.ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H") ? "HH:mm" : Tt("h:mm tt"), CultureInfo.CurrentCulture) };
            }
            return null;
        }
        // the folded chip's words for a tile preset
        static string tcKey = ""; static DateTime tcAt = DateTime.MinValue; static string tcVal = "";
        public static string TileChip(string st, Snap s)
        {
            if (styleOverride == null && st == tcKey && (DateTime.UtcNow - tcAt).TotalSeconds < 2) return tcVal;   // asked every tick: worked out at most every 2 s
            var v = TileChipNow(st, s); if (styleOverride == null) { tcKey = st; tcAt = DateTime.UtcNow; tcVal = v; }
            return v;
        }
        static string TileChipNow(string st, Snap s)
        {
            List<Tile> tl; Title(st, out tl, s);
            if (st == "mine") return string.Join(" · ", tl.Where(t => t.Value != "" && t.Value != "-").Take(3).Select(t => t.Value));
            if (st == "sports") { var g = Corner.MatchGame(); if (g != null && g.State != "soon") return g.Home + " " + g.HomeScore + "–" + g.AwayScore + " " + g.Away + (g.State == "live" && g.Detail != "" ? " · " + Trunc(g.Detail, 14) : g.State == "done" ? " · " + L.T("Final") : ""); }
            if (st == "recap") return L.T("Match") + " · " + (tl.Count > 0 ? tl[0].Value : "-") + " " + L.T("avg");
            var f = tl.FirstOrDefault(); if (f == null) return s.Chip;
            if (st == "markets") return Corner.Syms().Count == 0 ? L.T("Markets") : (f.Label.Length > 12 ? Trunc(f.Label, 12) : f.Label) + " " + f.Value + ChipAge();   /* hunters12 display L-3: an old price on the strip says its day */
            if (st == "data") return L.T("Today") + " " + f.Value;
            if (st == "weather") { var w = Weather.Active ? Weather.Text(DateTime.UtcNow) : null; return w ?? L.T("Weather"); }
            return f.Label + " " + f.Value;
        }
        static string ChipAge()
        {
            DateTime last = DateTime.MaxValue; lock (MarketData.k) foreach (var sym in Corner.Syms()) { MarketData.Quote q; if (MarketData.Quotes.TryGetValue(sym, out q) && q.When < last) last = q.When; }
            if (last == DateTime.MaxValue || last.ToLocalTime().Date == DateTime.Today) return "";
            var lt = last.ToLocalTime(); return " · " + lt.ToString((DateTime.Today - lt.Date).TotalDays < 6 ? "ddd" : "d MMM", CultureInfo.CurrentCulture);
        }
        // the gamer chip: "32 ms · Competitive", a spike held 5 s: "Spike 140 ms · Wi-Fi"
        public static string GamerChip()
        {
            var t = GamerTarget(); if (t == null) return null;
            Spike last = null; lock (GamePing.Me.Spikes) last = GamePing.Me.Spikes.LastOrDefault(z => z.Game == t.Item1.Id && (DateTime.Now - z.T).TotalSeconds < 5);
            if (last != null) return L.T("Spike") + " " + Math.Round(last.Ms).ToString(CultureInfo.CurrentCulture) + " ms · " + L.T(last.Blame);
            var v = GamePing.Me.Stat(t.Item1.Id, t.Item2).Window(60, 3600); if (v.Length == 0) return t.Item1.Name;
            double now = v[v.Length - 1], avg = GameStat.Avg(v), jit = GameStat.Jitter(v), loss = GameStat.Loss(v);
            return t.Item1.Name + " " + Fmt(now, "0", " ms") + " · " + L.T(GameStat.Grade(avg, jit, double.IsNaN(loss) ? 0 : loss));
        }

        // the tile card: header, title (or the verdict in red when the network has a problem), the tiles, the network line,
        // the blue line
        public static Bitmap TileCard(Snap s, Dictionary<string, RectangleF> hits, string st)
        {
            hits.Clear();
            List<Tile> tiles; string title = Title(st, out tiles, s);
            bool problem = s.Level == 2 && !s.Waiting;
            int W = (int)((Cfg.BigWidget ? 300 : 264) * S); float pad = 12 * S, inner = W - 2 * pad;
            string head = problem ? s.Headline : title;
            float headH = Measure(head, fHead, (int)inner).Height, wh = HintLineH(inner);
            float subH = problem ? Measure(title, fSmall, (int)inner).Height + 2 * S : 0;
            // hunters12 display ML-1: a value too wide for its row (a long forecast, a far place's "(your PC's time)") goes on its own
            // line under the label, where it can wrap - never off the card's left edge over the label
            foreach (var t in tiles)
                if (t.Value != "" && Measure(t.Value, t.Big ? fName : fMono, 0).Width > inner * 0.62f) { t.Sub = t.Value + (string.IsNullOrEmpty(t.Sub) ? "" : " · " + t.Sub); t.Value = ""; t.Moved = true; }
            var hs = new List<float>();
            foreach (var t in tiles)
            {
                float h = (t.Big ? 26 : 20) * S;
                if (t.Value == "" && t.Label != "") h = Math.Max(h, Measure(t.Label, fBody, (int)inner).Height + 4 * S);
                if (!string.IsNullOrEmpty(t.Sub)) h += Measure(t.Sub, fSmall, (int)inner).Height;
                if (t.Series != null && t.Series.Length >= 2) h += 16 * S; else if (t.Series != null && t.Series.Length == 1) h += 8 * S;
                hs.Add(h);
            }
            string net = NetLine(s); bool showNet = st != "mine" || !Corner.Mine().Contains("verdict");
            float netH = showNet ? 22 * S : 0;
            float H0 = pad + 24 * S + headH + subH + 8 * S + hs.Sum() + netH + wh + pad;
            int H = (int)Math.Ceiling(H0);
            var bmp = new Bitmap((int)Math.Ceiling(W * WidgetK), (int)Math.Ceiling(Math.Min(H, 900 * S) * WidgetK), PixelFormat.Format32bppArgb);
            using (var g = PrepW(bmp))
            using (var ink = new SolidBrush(Theme.Ink))
            using (var muted = new SolidBrush(Theme.Muted))
            using (var sf = new StringFormat(L.IsRtl ? StringFormatFlags.DirectionRightToLeft : 0))
            {
                var cr = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, cr, 16 * S, Color.FromArgb(250, Theme.Card));
                Theme.Stroke(g, cr, 16 * S, problem ? Theme.Red : Theme.Line, problem ? 1.5f : 1f);
                float y = pad;
                Theme.Logo(g, new RectangleF(pad, y, 18 * S, 18 * S), false);
                g.DrawString(L.T("AdeeboMeter"), fName, ink, pad + 24 * S, y + 1 * S);
                float nw = Measure(L.T("AdeeboMeter"), fName, 0).Width;
                Theme.Pill(g, s.Waiting ? L.T("Checking") : L.T(Word(s.Level)), fPill, pad + 24 * S + nw + 2 * S, y + 2 * S, s.Waiting ? 3 : s.Level, S);
                if (Cfg.Buddy) Buddy(g, new RectangleF(W - pad - 24 * S, y, 24 * S, 24 * S), s, false);
                y += 24 * S;
                using (var hb = new SolidBrush(problem ? Theme.LvText(2) : Theme.Ink)) g.DrawString(head, fHead, hb, new RectangleF(pad, y, inner, headH + 2), sf);
                y += headH;
                if (subH > 0) { g.DrawString(title, fSmall, muted, new RectangleF(pad, y + 2 * S, inner, subH), sf); y += subH; }
                HintLine(g, pad, H, inner, wh);   // at the foot of the card (24 Nov)
                y += 8 * S;
                using (var lp = new Pen(Theme.Line2))
                    for (int i = 0; i < tiles.Count; i++)
                    {
                        var t = tiles[i]; float h = hs[i];
                        if (i > 0) g.DrawLine(lp, pad, y, W - pad, y);
                        var lf = t.Big ? fBodyB : fBody; var vf = t.Big ? fName : fMono;
                        float vw = t.Value == "" ? 0 : Measure(t.Value, vf, 0).Width;
                        float ly = y + 3 * S;
                        bool dot = t.Lv <= 2 && (t.Value != "" || t.Moved);   /* hunters13 data L7: a value moved under its label keeps the label's dot */
                        if (dot) { LvDot(g, t.Lv, pad, ly + 5 * S, 7 * S, 7 * S); }
                        float lx = pad + (dot ? 12 * S : 0);
                        using (var ls = new StringFormat(sf) { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = (t.Value == "" ? 0 : StringFormatFlags.NoWrap) | (L.IsRtl ? StringFormatFlags.DirectionRightToLeft : 0) })
                        using (var lb = new SolidBrush(t.Value == "" ? (t.Lv <= 2 ? Theme.LvText(t.Lv) : Theme.Ink) : t.Big ? Theme.Ink : Theme.Muted))
                            g.DrawString(t.Label, lf, lb, new RectangleF(lx, ly, Math.Max(10, inner - (lx - pad) - vw - 6 * S), t.Value == "" ? h : 20 * S), ls);
                        if (t.Value != "") using (var vb = new SolidBrush(t.Tint ?? (t.Lv <= 2 && t.Lv > 0 ? Theme.LvText(t.Lv) : Theme.Ink))) g.DrawString(t.Value, vf, vb, W - pad - vw, ly - (t.Big ? 2 * S : 0));
                        float yy = ly + (t.Big ? 24 : 18) * S;
                        if (!string.IsNullOrEmpty(t.Sub)) { float sh = Measure(t.Sub, fSmall, (int)inner).Height; g.DrawString(t.Sub, fSmall, muted, new RectangleF(pad, yy - 2 * S, inner, sh), sf); yy += sh; }
                        if (t.Series != null && t.Series.Length >= 2) MiniLine(g, t.Series, new RectangleF(pad, yy, inner, 12 * S), t.Tint ?? Theme.BlueLine);
                        else if (t.Series != null && t.Series.Length == 1)
                        {
                            var br = new RectangleF(pad, yy, inner, 5 * S);
                            using (var tb = new SolidBrush(Theme.Track)) g.FillRectangle(tb, br);
                            using (var fb = new SolidBrush(t.Lv == 2 ? Theme.Red : t.Lv == 1 ? Theme.Amber : Theme.BlueLine)) g.FillRectangle(fb, br.X, br.Y, br.Width * (float)Math.Max(0, Math.Min(1, t.Series[0])), br.Height);
                        }
                        y += h;
                    }
                if (showNet)
                {
                    y += 4 * S; LvDot(g, s.Waiting ? 3 : s.Level, pad, y + 5 * S, 7 * S, 7 * S);
                    using (var nb = new SolidBrush(s.Level >= 1 && s.Level <= 2 ? Theme.LvText(s.Level) : Theme.Muted)) g.DrawString(net, fSmall, nb, new RectangleF(pad + 12 * S, y, inner - 12 * S, 18 * S), sf);
                    y += 18 * S;
                }
            }
            return bmp;
        }
        // a price line scaled to its own low and high (a stock's day is a few tenths of a percent - from zero it would be flat)
        static void MiniLine(Graphics g, double[] v, RectangleF r, Color c)
        {
            var ok = v.Where(x => !double.IsNaN(x)).ToArray(); if (ok.Length < 2) return;
            double lo = ok.Min(), hi = ok.Max(); if (hi - lo < 1e-9) { hi = lo + 1; lo -= 1; }
            var pts = new List<PointF>(); for (int i = 0; i < v.Length; i++) if (!double.IsNaN(v[i])) pts.Add(new PointF(r.X + r.Width * i / (v.Length - 1), r.Bottom - (float)((v[i] - lo) / (hi - lo)) * r.Height));
            if (pts.Count >= 2) using (var p = new Pen(c, Math.Max(1f, 1.3f * S))) g.DrawLines(p, pts.ToArray());
        }
        // "Network fine · 18 ms" - the one line every preset keeps
        static string NetLine(Snap s)
        {
            if (s.Waiting) return L.T("Checking your connection...");
            if (s.Level == 0) return L.T("Network fine") + (double.IsNaN(s.Ping) ? "" : " · " + Fmt(s.Ping, "0", " ms"));
            return Trunc(s.Chip, 40);
        }
    }
}
