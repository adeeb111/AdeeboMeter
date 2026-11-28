// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== The markets page's numbers (1.1.6, PLAN-1.1.6 Markets)
    // Everything a casual investor or a trader can read here without an account:
    //  - currencies: any of the 26 the Bank of Canada publishes, the person picks which (Cfg.FxList); a long history for the chart
    //  - commodities: the Bank's weekly commodity price indexes, all seven groups, five years back for the comparison chart
    //  - Canada: the Bank's policy rate (and when it last moved) and inflation (Statistics Canada's CPI, via the Bank)
    //  - the world's exchanges: hours, worked out on this PC (no download)
    //  - index levels and futures prices only with the person's own Financial Modeling Prep key (free: 250 calls a day)
    // Fetched only while a Markets page is open, never more than the source updates. Numbers only - never advice.
    static class MarketData
    {
        public static readonly object k = new object();
        public static long Ver;

        // ---------- the currencies the Bank of Canada publishes each business day (RUB stopped in 2022: left out)
        public static readonly string[][] AllFx = {
            new[] { "USD", "US dollar" }, new[] { "EUR", "Euro" }, new[] { "GBP", "British pound" }, new[] { "JPY", "Japanese yen" },
            new[] { "CNY", "Chinese yuan" }, new[] { "INR", "Indian rupee" }, new[] { "CHF", "Swiss franc" }, new[] { "AUD", "Australian dollar" },
            new[] { "MXN", "Mexican peso" }, new[] { "HKD", "Hong Kong dollar" }, new[] { "NZD", "New Zealand dollar" }, new[] { "SGD", "Singapore dollar" },
            new[] { "KRW", "South Korean won" }, new[] { "SEK", "Swedish krona" }, new[] { "NOK", "Norwegian krone" }, new[] { "BRL", "Brazilian real" },
            new[] { "ZAR", "South African rand" }, new[] { "TRY", "Turkish lira" }, new[] { "SAR", "Saudi riyal" }, new[] { "TWD", "New Taiwan dollar" },
            new[] { "THB", "Thai baht" }, new[] { "IDR", "Indonesian rupiah" }, new[] { "MYR", "Malaysian ringgit" }, new[] { "PEN", "Peruvian sol" },
            new[] { "VND", "Vietnamese dong" }, new[] { "PLN", "Polish zloty" } };
        public const string DefaultFx = "USD,EUR,GBP,JPY,CNY,INR";
        public static string FxName(string code) { var f = AllFx.FirstOrDefault(a => a[0] == code); return f == null ? code : f[1]; }
        public static bool KnownFx(string code) { return AllFx.Any(a => a[0] == code); }
        // the person's list, in their order; junk and duplicates dropped, at most 12
        public static List<string> MyFx()
        {
            var l = new List<string>();
            foreach (var c in (Cfg.FxList ?? DefaultFx).Split(',').Select(z => z.Trim().ToUpperInvariant()))
                if (KnownFx(c) && !l.Contains(c) && l.Count < 12) l.Add(c);
            return l;
        }
        public static bool AddFx(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant(); var l = MyFx();
            if (!KnownFx(code) || l.Contains(code) || l.Count >= 12) return false;
            l.Add(code); Cfg.FxList = string.Join(",", l); Cfg.Save(); return true;
        }
        public static bool RemoveFx(string code)
        {
            var l = MyFx(); if (!l.Remove(code) || l.Count == 0) return false;   // the list never goes empty: the page needs something to show
            Cfg.FxList = string.Join(",", l); Cfg.Save(); return true;
        }
        // the picker's search: code or name, start of a word first
        public static List<string[]> FindFx(string q)
        {
            q = (q ?? "").Trim(); if (q == "") return AllFx.ToList();
            return AllFx.Where(a => a[0].StartsWith(q, StringComparison.OrdinalIgnoreCase) || a[1].Split(' ').Any(w => w.StartsWith(q, StringComparison.OrdinalIgnoreCase)))
                .Concat(AllFx.Where(a => a[1].IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)).Distinct().ToList();
        }

        // ---------- the Bank's Valet answers, read without a JSON library: {"observations":[{"d":"2026-11-20","FXUSDCAD":{"v":"1.3982"}},...]}
        public sealed class Series { public string Code; public List<DateTime> D = new List<DateTime>(); public List<double> V = new List<double>(); public int Count { get { return V.Count; } } public double Last { get { return V.Count == 0 ? double.NaN : V[V.Count - 1]; } } public DateTime LastDay { get { return D.Count == 0 ? DateTime.MinValue : D[D.Count - 1]; } } }
        static readonly Regex dayRx = new Regex("\"d\"\\s*:\\s*\"(\\d{4}-\\d{2}-\\d{2})\"");
        static readonly Regex valRx = new Regex("\"([A-Za-z0-9_.]+)\"\\s*:\\s*\\{\\s*\"v\"\\s*:\\s*\"(-?[0-9]+(?:\\.[0-9]+)?)\"\\s*\\}");
        public static Dictionary<string, Series> ParseValet(string json)
        {
            json = json ?? ""; var r = new Dictionary<string, Series>();
            var days = dayRx.Matches(json).Cast<Match>().ToList();
            for (int i = 0; i < days.Count; i++)
            {
                DateTime d; if (!DateTime.TryParseExact(days[i].Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) continue;
                int from = days[i].Index + days[i].Length, to = i + 1 < days.Count ? days[i + 1].Index : json.Length;
                foreach (Match v in valRx.Matches(json.Substring(from, to - from)))
                {
                    double x; if (!double.TryParse(v.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out x) || double.IsNaN(x) || Math.Abs(x) > 1e7) continue;
                    Series s; if (!r.TryGetValue(v.Groups[1].Value, out s)) r[v.Groups[1].Value] = s = new Series { Code = v.Groups[1].Value };
                    if (s.D.Count > 0 && d <= s.LastDay) { if (d == s.LastDay) continue; }   // one value a day; out-of-order rows are sorted below
                    s.D.Add(d); s.V.Add(x);
                }
            }
            foreach (var s in r.Values)
                if (s.D.Zip(s.D.Skip(1), (a, b) => b <= a).Any(z => z))
                {
                    var pairs = s.D.Zip(s.V, Tuple.Create).GroupBy(t => t.Item1).Select(g => g.First()).OrderBy(t => t.Item1).ToList();
                    s.D = pairs.Select(t => t.Item1).ToList(); s.V = pairs.Select(t => t.Item2).ToList();
                }
            return r;
        }

        // ---------- the long view: one currency since 2017 (the Bank's series start), the commodity groups for five years,
        // the policy rate and inflation. Each kept in a file; fetched again only once the Bank has had time to add a day.
        public static readonly string[] Groups = { "BCPI", "BCNE", "ENER", "MTLS", "AGRI", "FOPR", "FISH" };
        public static readonly Dictionary<string, string[]> GroupInfo = new Dictionary<string, string[]> {
            { "BCPI", new[] { "All commodities", "everything below, weighted by what Canada produces" } },
            { "BCNE", new[] { "Without energy", "all commodities except oil, gas and coal" } },
            { "ENER", new[] { "Energy", "crude oil, natural gas, coal" } },
            { "MTLS", new[] { "Metals and minerals", "gold, silver, copper, nickel, zinc, aluminum, iron ore, potash" } },
            { "AGRI", new[] { "Agriculture", "wheat, barley, canola, corn, cattle, hogs" } },
            { "FOPR", new[] { "Forestry", "lumber, pulp, newsprint" } },
            { "FISH", new[] { "Fish", "lobster, crab, salmon, shrimp" } } };
        static readonly Dictionary<string, Series> hist = new Dictionary<string, Series>();
        static readonly Dictionary<string, DateTime> tried = new Dictionary<string, DateTime>();
        static readonly HashSet<string> busy = new HashSet<string>();
        public static string Note = "";
        static string HistFile(string what) { return Path.Combine(Program.DataDir, "mk-" + what.ToLowerInvariant() + ".txt"); }

        // what = "FX:USD", "BCPI" (all groups), "CANADA" (rate + inflation)
        public static Series Get(string series)
        {
            lock (k) { Series s; return hist.TryGetValue(series, out s) ? s : null; }
        }
        static string Url(string what)
        {
            string b = "https://www.bankofcanada.ca/valet/observations/";
            if (what.StartsWith("FX:", StringComparison.Ordinal)) return b + "FX" + what.Substring(3) + "CAD/json?start_date=2017-01-03";
            if (what == "BCPI") return b + string.Join(",", Groups.Select(g => "W." + g)) + "/json?start_date=" + DateTime.Today.AddYears(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (what == "CANADA") return b + "V39079,STATIC_TOTALCPICHANGE/json?start_date=" + DateTime.Today.AddYears(-3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            throw new ArgumentException(what);
        }
        static void Take(string what, Dictionary<string, Series> got)
        {
            lock (k)
            {
                foreach (var kv in got)
                {
                    string key = kv.Key.StartsWith("FX", StringComparison.Ordinal) && kv.Key.EndsWith("CAD", StringComparison.Ordinal) && kv.Key.Length == 8 ? "FX:" + kv.Key.Substring(2, 3)
                        : kv.Key.StartsWith("W.", StringComparison.Ordinal) ? kv.Key.Substring(2) : kv.Key;
                    hist[key] = kv.Value; kv.Value.Code = key;
                }
                Ver++;
            }
        }
        // stale: a daily series misses the last business day (after the Bank's 16:30 posting), a weekly one is 12+ days old,
        // a monthly one is 40+ days old
        public static bool Stale(string what)
        {
            Series s = what == "BCPI" ? Get("ENER") : what == "CANADA" ? Get("V39079") : Get(what);
            if (s == null || s.Count == 0) return true;
            if (what == "BCPI") return s.LastDay < DateTime.Today.AddDays(-12);
            var et = Trading.EasternNow(); var want = et.TimeOfDay >= new TimeSpan(16, 45, 0) ? et.Date : et.Date.AddDays(-1);
            while (want.DayOfWeek == DayOfWeek.Saturday || want.DayOfWeek == DayOfWeek.Sunday) want = want.AddDays(-1);
            return s.LastDay < (what.StartsWith("FX", StringComparison.Ordinal) ? want : want.AddDays(-3));   /* review 1.1.6 P4 #3: a rate is stale once it is older than the last business day (the tried clock limits holiday refetches) */ // a Bank holiday or two is not "stale"; anything older is
        }
        static readonly HashSet<string> emptyRead = new HashSet<string>(StringComparer.Ordinal);
        public static void Load(string what)
        {
            lock (k) if (hist.Count > 0 && (what == "BCPI" ? hist.ContainsKey("ENER") : what == "CANADA" ? hist.ContainsKey("V39079") : hist.ContainsKey(what))) return;
            try
            {
                var f = HistFile(what.Replace(":", "-")); if (!File.Exists(f)) return;
                var fi = new FileInfo(f); string sig = f + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
                lock (k) if (emptyRead.Contains(sig)) return;   // R2-043: a kept file that held no series is read once, not on every paint
                if (fi.Length < 4 * 1024 * 1024) Take(what, ParseValet(File.ReadAllText(f)));
                lock (k) if (!(what == "BCPI" ? hist.ContainsKey("ENER") : what == "CANADA" ? hist.ContainsKey("V39079") : hist.ContainsKey(what))) emptyRead.Add(sig);
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
        }
        // fetch in the background when stale; done() runs on that thread (callers marshal to the UI, guarded - see Done())
        public static void Fetch(string what, Action done)
        {
            Load(what);
            if (Policy.NoOnline || !Stale(what)) return;
            lock (k)
            {
                DateTime t; if (busy.Contains(what) || (tried.TryGetValue(what, out t) && (DateTime.UtcNow - t).TotalMinutes < 30)) return;
                busy.Add(what); tried[what] = DateTime.UtcNow;
            }
            new Thread(() =>
            {
                try
                {
                    string body = HttpSmall.Get(Url(what), 3072);
                    var got = ParseValet(body);
                    if (got.Count == 0 || got.Values.All(s => s.Count == 0)) throw new InvalidDataException("no numbers in the answer");
                    Take(what, got); lock (k) Note = "";
                    try { File.WriteAllText(HistFile(what.Replace(":", "-")), body); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; bool kept = false; try { kept = File.Exists(HistFile(what.Replace(":", "-"))); } catch (Exception e2) { if (!(e2 is IOException || e2 is UnauthorizedAccessException)) throw; }
                    // carry-over: a first fetch that fails has nothing kept to show - it says so, and tries again in 5 minutes, not 30
                    lock (k) { Note = kept ? "Couldn't reach the Bank of Canada just now - showing what was kept. It tries again in 30 minutes." : "Couldn't reach the Bank of Canada yet - nothing to show until it answers. It tries again in 5 minutes."; if (!kept) tried[what] = DateTime.UtcNow.AddMinutes(-25); } Program.Log("markets " + what + ": " + e.Message); }
                finally { lock (k) busy.Remove(what); Done(done); }
            }) { IsBackground = true, Name = "markets" }.Start();
        }
        // bug-hunt M-3/M-4: a callback that touches a closing window must never take the app down from a background thread
        public static void Done(Action done)
        {
            if (done == null) return;
            try { done(); } catch (Exception e) { if (!(e is InvalidOperationException || e is ObjectDisposedException)) throw; }
        }

        // ---------- what the numbers say (pure, for the pages and the self-test)
        // the value on or just before a day (or NaN)
        public static double At(Series s, DateTime day)
        {
            if (s == null) return double.NaN;
            for (int i = s.Count - 1; i >= 0; i--) if (s.D[i] <= day) return s.V[i];
            return double.NaN;
        }
        public static double Pct(double now, double then) { return double.IsNaN(now) || double.IsNaN(then) || then <= 0 ? double.NaN : (now - then) / then * 100; }
        public static double ChangeOver(Series s, TimeSpan span) { return s == null || s.Count == 0 ? double.NaN : Pct(s.Last, At(s, s.LastDay - span)); }
        public static Series Window(Series s, DateTime from)
        {
            var w = new Series { Code = s == null ? "" : s.Code }; if (s == null) return w;
            for (int i = 0; i < s.Count; i++) if (s.D[i] >= from) { w.D.Add(s.D[i]); w.V.Add(s.V[i]); }
            return w;
        }
        // how many periods in a row it went the same way (+n up, -n down, 0 flat or unknown)
        public static int Streak(Series s)
        {
            if (s == null || s.Count < 2) return 0;
            int dir = Math.Sign(s.V[s.Count - 1] - s.V[s.Count - 2]), n = 0; if (dir == 0) return 0;
            for (int i = s.Count - 1; i > 0 && Math.Sign(s.V[i] - s.V[i - 1]) == dir; i--) n++;
            return dir * n;
        }
        // the policy rate's last move: the first day the current value held
        public static DateTime RateSince(Series s)
        {
            if (s == null || s.Count == 0) return DateTime.MinValue;
            int i = s.Count - 1; while (i > 0 && Math.Abs(s.V[i - 1] - s.Last) < 0.001) i--;
            return i == 0 ? DateTime.MinValue : s.D[i];
        }
        // one unit of a buys this much b, through the Canadian dollar (CAD = 1)
        public static double Cross(string a, string b)
        {
            double va = a == "CAD" ? 1 : At(Get("FX:" + a), DateTime.MaxValue), vb = b == "CAD" ? 1 : At(Get("FX:" + b), DateTime.MaxValue);
            return double.IsNaN(va) || double.IsNaN(vb) || vb <= 0 ? double.NaN : va / vb;
        }

        // ---------- the world's exchanges (regular hours, local time) - holidays are known only for New York and Toronto (Markets.cs)
        public sealed class Exchange { public string Name, City, Zone, Ccy; public TimeSpan Open, Close, LunchFrom, LunchTo; }
        public static readonly Exchange[] Exchanges = {
            new Exchange { Name = "Tokyo", City = "Tokyo", Zone = "Tokyo Standard Time", Ccy = "JPY", Open = new TimeSpan(9, 0, 0), Close = new TimeSpan(15, 30, 0), LunchFrom = new TimeSpan(11, 30, 0), LunchTo = new TimeSpan(12, 30, 0) },
            new Exchange { Name = "Shanghai", City = "Shanghai", Zone = "China Standard Time", Ccy = "CNY", Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(15, 0, 0), LunchFrom = new TimeSpan(11, 30, 0), LunchTo = new TimeSpan(13, 0, 0) },
            new Exchange { Name = "Hong Kong", City = "Hong Kong", Zone = "China Standard Time", Ccy = "HKD", Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(16, 0, 0), LunchFrom = new TimeSpan(12, 0, 0), LunchTo = new TimeSpan(13, 0, 0) },
            new Exchange { Name = "Mumbai", City = "Mumbai", Zone = "India Standard Time", Ccy = "INR", Open = new TimeSpan(9, 15, 0), Close = new TimeSpan(15, 30, 0) },
            new Exchange { Name = "Frankfurt", City = "Frankfurt", Zone = "W. Europe Standard Time", Ccy = "EUR", Open = new TimeSpan(9, 0, 0), Close = new TimeSpan(17, 30, 0) },
            new Exchange { Name = "Paris", City = "Paris", Zone = "Romance Standard Time", Ccy = "EUR", Open = new TimeSpan(9, 0, 0), Close = new TimeSpan(17, 30, 0) },
            new Exchange { Name = "London", City = "London", Zone = "GMT Standard Time", Ccy = "GBP", Open = new TimeSpan(8, 0, 0), Close = new TimeSpan(16, 30, 0) },
            new Exchange { Name = "Toronto", City = "Toronto", Zone = "Eastern Standard Time", Ccy = "CAD", Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(16, 0, 0) },
            new Exchange { Name = "New York", City = "New York", Zone = "Eastern Standard Time", Ccy = "USD", Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(16, 0, 0) } };
        public static DateTime LocalTo(Exchange x, DateTime utc)
        {
            try { return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(x.Zone)); }
            catch (Exception e) { if (!(e is TimeZoneNotFoundException || e is InvalidTimeZoneException)) throw; return DateTime.MinValue; }
        }
        // "open", "lunch", "closed" (weekends and, for New York and Toronto, their holidays and half days)
        public static string State(Exchange x, DateTime utc)
        {
            var t = LocalTo(x, utc); if (t == DateTime.MinValue) return "unknown";
            if (t.DayOfWeek == DayOfWeek.Saturday || t.DayOfWeek == DayOfWeek.Sunday) return "closed";
            var close = x.Close;
            if (x.Name == "New York" || x.Name == "Toronto")
            {
                string venue = x.Name == "New York" ? "us" : "ca";
                if (Markets.Holiday(t.Date, venue) != null) return "closed";
                close = Markets.Close(t.Date, venue);
            }
            var tod = t.TimeOfDay;
            if (tod < x.Open || tod >= close) return "closed";
            if (x.LunchTo > x.LunchFrom && tod >= x.LunchFrom && tod < x.LunchTo) return "lunch";
            return "open";
        }
        // the exchange's session today (or the next weekday's) in the PC's own time, for the day-strip
        public static Tuple<DateTime, DateTime> NextSession(Exchange x, DateTime utc)
        {
            TimeZoneInfo tz; try { tz = TimeZoneInfo.FindSystemTimeZoneById(x.Zone); } catch (Exception e) { if (!(e is TimeZoneNotFoundException || e is InvalidTimeZoneException)) throw; return null; }
            var t = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            for (int add = 0; add < 8; add++)
            {
                var d = t.Date.AddDays(add);
                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                bool na = x.Name == "New York" || x.Name == "Toronto"; string venue = x.Name == "New York" ? "us" : "ca";
                if (na && Markets.Holiday(d, venue) != null) continue;
                var close = na ? Markets.Close(d, venue) : x.Close;
                if (add == 0 && t.TimeOfDay >= close) continue;
                var o = TimeZoneInfo.ConvertTimeToUtc(d + x.Open, tz).ToLocalTime(); var c = TimeZoneInfo.ConvertTimeToUtc(d + close, tz).ToLocalTime();
                return Tuple.Create(o, c);
            }
            return null;
        }

        // ---------- index levels and futures: only with the person's own Financial Modeling Prep key (kept encrypted with the
        // sports keys). 250 calls a day on the free plan: one call per symbol, every 15 minutes at most, stops at 230.
        public sealed class Quote { public string Sym, Name, Region, Ccy, Unit = ""; public double Price = double.NaN, Pct = double.NaN, WeekPct = double.NaN; public DateTime When; }
        public static readonly string[][] IndexList = {   // symbol, name, region, currency, exchange
            new[] { "^GSPC", "S&P 500", "Americas", "USD", "New York" }, new[] { "^IXIC", "Nasdaq Composite", "Americas", "USD", "New York" },
            new[] { "^DJI", "Dow Jones", "Americas", "USD", "New York" }, new[] { "^GSPTSE", "S&P/TSX Composite", "Americas", "CAD", "Toronto" },
            new[] { "^FTSE", "FTSE 100 (London)", "Europe", "GBP", "London" }, new[] { "^GDAXI", "DAX (Frankfurt)", "Europe", "EUR", "Frankfurt" },
            new[] { "^FCHI", "CAC 40 (Paris)", "Europe", "EUR", "Paris" }, new[] { "^STOXX50E", "Euro Stoxx 50", "Europe", "EUR", "Frankfurt" },
            new[] { "^N225", "Nikkei 225 (Tokyo)", "Asia", "JPY", "Tokyo" }, new[] { "000001.SS", "Shanghai Composite", "Asia", "CNY", "Shanghai" },
            new[] { "^HSI", "Hang Seng (Hong Kong)", "Asia", "HKD", "Hong Kong" }, new[] { "^NSEI", "Nifty 50 (Mumbai)", "Asia", "INR", "Mumbai" } };
        public static readonly string[][] FuturesList = {   // symbol, name, unit
            new[] { "CLUSD", "WTI crude oil", "USD per barrel" }, new[] { "BZUSD", "Brent crude oil", "USD per barrel" }, new[] { "NGUSD", "Natural gas", "USD per MMBtu" },
            new[] { "GCUSD", "Gold", "USD per ounce" }, new[] { "SIUSD", "Silver", "USD per ounce" }, new[] { "HGUSD", "Copper", "USD per pound" } };
        public static Dictionary<string, Quote> Quotes = new Dictionary<string, Quote>(); public static string QuoteNote = "";
        static volatile bool qBusy; static readonly DateTime[] qTried = { DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue };   /* [3]: your companies */   // R2-066: the indexes and the futures each keep their own clock
        public static void QuoteKeyChanged() { qTried[0] = qTried[1] = qTried[2] = qTried[3] = DateTime.MinValue; }   // R2-067: a new key is tried at once
        // ---------- companies to follow (24 Nov, Adeeb: "how do I even add Anthropic, Google, Meta?"): well-known names to pick from,
        // by group - ticker, name, group, exchange. A name with "-" as its ticker has no public shares (it is shown, and says so).
        public static readonly string[][] Companies = {
            new[] { "AAPL", "Apple", "Popular", "Nasdaq" }, new[] { "MSFT", "Microsoft", "Popular", "Nasdaq" }, new[] { "GOOGL", "Alphabet (Google)", "Popular", "Nasdaq" },
            new[] { "AMZN", "Amazon", "Popular", "Nasdaq" }, new[] { "META", "Meta Platforms", "Popular", "Nasdaq" }, new[] { "NVDA", "NVIDIA", "Popular", "Nasdaq" },
            new[] { "TSLA", "Tesla", "Popular", "Nasdaq" }, new[] { "NFLX", "Netflix", "Popular", "Nasdaq" }, new[] { "JPM", "JPMorgan Chase", "Popular", "New York" },
            new[] { "AMD", "AMD", "AI and tech", "Nasdaq" }, new[] { "AVGO", "Broadcom", "AI and tech", "Nasdaq" }, new[] { "TSM", "TSMC", "AI and tech", "New York" },
            new[] { "ORCL", "Oracle", "AI and tech", "New York" }, new[] { "PLTR", "Palantir", "AI and tech", "Nasdaq" }, new[] { "IBM", "IBM", "AI and tech", "New York" },
            new[] { "CRM", "Salesforce", "AI and tech", "New York" }, new[] { "INTC", "Intel", "AI and tech", "Nasdaq" }, new[] { "ADBE", "Adobe", "AI and tech", "Nasdaq" },
            new[] { "-", "Anthropic", "AI and tech", "private" }, new[] { "-", "OpenAI", "AI and tech", "private" }, new[] { "-", "SpaceX", "AI and tech", "private" },
            new[] { "SHOP.TO", "Shopify", "Canada", "Toronto" }, new[] { "RY.TO", "Royal Bank of Canada", "Canada", "Toronto" }, new[] { "TD.TO", "TD Bank", "Canada", "Toronto" },
            new[] { "BNS.TO", "Scotiabank", "Canada", "Toronto" }, new[] { "BMO.TO", "BMO", "Canada", "Toronto" }, new[] { "ENB.TO", "Enbridge", "Canada", "Toronto" },
            new[] { "CNR.TO", "CN Rail", "Canada", "Toronto" }, new[] { "BCE.TO", "BCE (Bell)", "Canada", "Toronto" }, new[] { "CSU.TO", "Constellation Software", "Canada", "Toronto" },
            new[] { "^GSPC", "S&P 500", "Indexes", "New York" }, new[] { "^IXIC", "Nasdaq Composite", "Indexes", "New York" }, new[] { "^DJI", "Dow Jones", "Indexes", "New York" },
            new[] { "^GSPTSE", "S&P/TSX Composite", "Indexes", "Toronto" }, new[] { "^FTSE", "FTSE 100 (London)", "Indexes", "London" }, new[] { "^N225", "Nikkei 225 (Tokyo)", "Indexes", "Tokyo" },
            new[] { "BTCUSD", "Bitcoin", "Crypto", "24/7" }, new[] { "ETHUSD", "Ethereum", "Crypto", "24/7" }, new[] { "GCUSD", "Gold", "Commodities", "futures" }, new[] { "SIUSD", "Silver", "Commodities", "futures" }, new[] { "CLUSD", "WTI crude oil", "Commodities", "futures" } };
        public static readonly string[] CompanyGroups = { "Popular", "AI and tech", "Canada", "Indexes", "Crypto", "Commodities" };
        // your list (Cfg.Follow): at most 20, in your order, junk dropped
        public static List<string> Follow() { return (Cfg.Follow ?? "").Split(',').Select(z => z.Trim()).Where(ValidSym).Distinct().Take(20).ToList(); }
        public static void SetFollow(IEnumerable<string> l) { Cfg.Follow = string.Join(",", l.Where(ValidSym).Distinct().Take(20)); Cfg.Save(); }
        public static void FetchFollow(Action done) { var l = Follow(); if (l.Count > 0) FetchList(3, l, done); }
        // a name or ticker search at the service you have a key for (the picker's search box) - [ticker, name, exchange]
        public static List<string[]> Search(string q)
        {
            var res = new List<string[]>(); q = (q ?? "").Trim(); if (q.Length < 2 || Policy.NoOnline) return res;
            try
            {
                string k1 = Sports.Key("fmp"), k2 = Sports.Key("finnhub"), k3 = Sports.Key("twelve");
                // PX-ML10: each service you have a key for is tried in turn until one finds something
                if (k1 != "")
                {
                try
                {
                    string b = HttpSmall.Get("https://financialmodelingprep.com/stable/search-name?query=" + Uri.EscapeDataString(q) + "&limit=15&apikey=" + Uri.EscapeDataString(k1), 128); Call();
                    foreach (Match m in Regex.Matches(b ?? "", "\\{[^{}]*?\"symbol\"\\s*:\\s*\"([^\"]{1,20})\"[^{}]*?\"name\"\\s*:\\s*\"([^\"]{1,80})\"[^{}]*?\\}")) { string ex = Regex.Match(m.Value, "\"exchange\"\\s*:\\s*\"([^\"]{1,20})\"").Groups[1].Value; res.Add(new[] { m.Groups[1].Value, m.Groups[2].Value, ex }); }
                }
                catch (Exception e) { if (!(e is System.Net.WebException || e is IOException)) throw; Program.Log("search: " + e.GetType().Name); }
                }
                if (res.Count == 0 && k2 != "")
                {
                try
                {
                    string b = HttpSmall.Get("https://finnhub.io/api/v1/search?q=" + Uri.EscapeDataString(q) + "&token=" + Uri.EscapeDataString(k2), 128);
                    foreach (Match m in Regex.Matches(b ?? "", "\"description\"\\s*:\\s*\"([^\"]{1,80})\"[^{}]*?\"symbol\"\\s*:\\s*\"([^\"]{1,20})\"")) res.Add(new[] { m.Groups[2].Value, m.Groups[1].Value, "" });
                }
                catch (Exception e) { if (!(e is System.Net.WebException || e is IOException)) throw; Program.Log("search: " + e.GetType().Name); }
                }
                if (res.Count == 0 && k3 != "")
                {
                try
                {
                    string b = HttpSmall.Get("https://api.twelvedata.com/symbol_search?symbol=" + Uri.EscapeDataString(q) + "&apikey=" + Uri.EscapeDataString(k3), 128);
                    foreach (Match m in Regex.Matches(b ?? "", "\\{[^{}]*?\"symbol\"\\s*:\\s*\"([^\"]{1,20})\"[^{}]*?\"instrument_name\"\\s*:\\s*\"([^\"]{1,80})\"[^{}]*?\"exchange\"\\s*:\\s*\"([^\"]{1,20})\"")) res.Add(new[] { m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value });
                }
                catch (Exception e) { if (!(e is System.Net.WebException || e is IOException)) throw; Program.Log("search: " + e.GetType().Name); }
                }
            }
            catch (Exception e) { if (!(e is System.Net.WebException || e is IOException)) throw; Program.Log("search: " + e.GetType().Name); }
            foreach (var r in res) if (string.IsNullOrEmpty(r[2])) r[2] = Venue(r[0]);   // PX-L2: the exchange column is never empty
            return res.Where(r => ValidSym(r[0])).GroupBy(r => r[0]).Select(gr => gr.First()).Take(15).ToList();
        }
        // the price services, in the order they're asked: key name, name, where to get a free key, what the free plan gives
        public static readonly string[][] Providers = {
            new[] { "fmp", "Financial Modeling Prep", "https://site.financialmodelingprep.com/register", "250 calls a day - indexes, shares, futures" },
            new[] { "finnhub", "Finnhub", "https://finnhub.io/register", "60 calls a minute - US shares" },
            new[] { "twelve", "Twelve Data", "https://twelvedata.com/register", "800 calls a day - US shares and crypto" } };
        public static bool HasQuoteKey() { return KeyCount() > 0; }
        static int KeyCount() { return Providers.Count(p => Sports.Key(p[0]) != ""); }
        // what each service can price on its free plan: FMP anything; Finnhub plain US tickers; Twelve Data plain US tickers and BTC/ETH
        internal static bool Covers(string prov, string sym)
        {
            if (prov == "fmp") return true;
            bool plain = Regex.IsMatch(sym ?? "", "^[A-Z]{1,5}(\\.[AB])?$") && !FuturesList.Any(f => f[0] == sym);   // PX-L1: BRK.B, BF.A - share classes, still New York
            if (prov == "finnhub") return plain;
            return plain || sym == "BTCUSD" || sym == "ETHUSD";
        }
        // one price from one service (throws WebException when the service can't be reached or refuses the key)
        internal static Quote Ask(string prov, string key, string sym)
        {
            if (prov == "fmp") return ParseFmp(HttpSmall.Get("https://financialmodelingprep.com/stable/quote?symbol=" + Uri.EscapeDataString(sym) + "&apikey=" + Uri.EscapeDataString(key), 64), sym);
            if (prov == "finnhub") return ParseFinnhub(HttpSmall.Get("https://finnhub.io/api/v1/quote?symbol=" + Uri.EscapeDataString(sym) + "&token=" + Uri.EscapeDataString(key), 16), sym);
            string ts = sym == "BTCUSD" ? "BTC/USD" : sym == "ETHUSD" ? "ETH/USD" : sym;
            return ParseTwelve(HttpSmall.Get("https://api.twelvedata.com/quote?symbol=" + Uri.EscapeDataString(ts) + "&apikey=" + Uri.EscapeDataString(key), 32), sym);
        }
        static double JNum(string json, string key) { var m = Regex.Match(json ?? "", "\"" + key + "\"\\s*:\\s*\"?(-?[0-9]+(?:\\.[0-9]+)?(?:[eE][-+]?[0-9]+)?)\"?"); double v; return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }
        static Quote Named(Quote q, string json)
        {
            var c = Companies.FirstOrDefault(z => z[0] == q.Sym); if (c != null) { q.Name = c[1]; return q; }
            var nm = Regex.Match(json ?? "", "\"(?:name|instrument_name)\"\\s*:\\s*\"([^\"]{1,60})\""); q.Name = nm.Success ? nm.Groups[1].Value : q.Sym; return q;
        }
        public static Quote ParseFinnhub(string json, string sym)
        {
            double price = JNum(json, "c"), pct = JNum(json, "dp"), ts = JNum(json, "t");
            if (double.IsNaN(price) || price <= 0 || price > 1e7) return null;   // Finnhub answers 0 for a symbol it doesn't know
            return Named(new Quote { Sym = sym, Price = price, Pct = Math.Abs(pct) < 1000 ? pct : double.NaN, When = !double.IsNaN(ts) && ts > 946684800 && ts < 4102444800 ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(ts) : DateTime.UtcNow }, json);
        }
        public static Quote ParseTwelve(string json, string sym)
        {
            if ((json ?? "").IndexOf("\"status\":\"error\"", StringComparison.Ordinal) >= 0) return null;
            double price = JNum(json, "close"), pct = JNum(json, "percent_change"), ts = JNum(json, "timestamp");
            if (double.IsNaN(price) || price <= 0 || price > 1e7) return null;
            var q = Named(new Quote { Sym = sym, Price = price, Pct = Math.Abs(pct) < 1000 ? pct : double.NaN, When = !double.IsNaN(ts) && ts > 946684800 && ts < 4102444800 ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(ts) : DateTime.UtcNow }, json);
            var cc = Regex.Match(json ?? "", "\"currency\"\\s*:\\s*\"([A-Z]{3})\""); if (cc.Success) q.Ccy = cc.Groups[1].Value;   // PX-ML7
            return q;
        }
        // 21 Nov (Adeeb): your own key, your own speed - every hour, 15 minutes (the free plan's pace), 5 minutes or every minute (paid)
        public static int EveryMin { get { int m; return int.TryParse(Cfg.FmpEvery, out m) && m >= 1 ? m : 15; } }
        public static bool FreePace { get { return EveryMin >= 15; } }
        static string CallsFile(string prov) { return Path.Combine(Program.DataDir, prov == "fmp" ? "mk-calls.txt" : "mk-calls-" + prov + ".txt"); }   // FMP keeps its old file
        static int CallsToday() { return CallsToday("fmp"); }
        static int CallsToday(string prov)
        {
            try { string f = CallsFile(prov); if (File.Exists(f)) { var p = File.ReadAllText(f).Split(' '); int n; if (p.Length == 2 && p[0] == DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) && int.TryParse(p[1], out n)) return n; } }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            return 0;
        }
        static void Call() { Call("fmp"); }
        static void Call(string prov) { try { File.WriteAllText(CallsFile(prov), DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + (CallsToday(prov) + 1).ToString(CultureInfo.InvariantCulture)); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; } }
        public static void FetchQuotes(bool futures, Action done) { FetchList(futures ? 1 : 0, futures ? FuturesList.Select(f => f[0]).ToList() : IndexList.Select(f => f[0]).ToList(), done); }
        // the corner widget's own symbols (24 Nov): any the person typed (AAPL, SHOP.TO) plus the listed ones, at the key's pace
        public static int CallsUsedToday { get { return CallsToday(); } }
        public static readonly Dictionary<string, List<double>> Hist = new Dictionary<string, List<double>>();   // the widget's little graph: the last 40 prices seen
        public static void FetchWidget(List<string> syms, Action done) { if (syms.Count > 0) FetchList(2, syms.Take(6).ToList(), done); }
        // hunters13 data M1: symbols the service answered without a price today (a typo, delisted, a wrong suffix): after two, the
        // widget stops asking for it until tomorrow (UTC) and says so - the good symbols keep their calls
        static readonly Dictionary<string, int> noQuote = new Dictionary<string, int>(); static string noQuoteDay = "";
        public static bool Dead(string sym) { lock (k) { NoQuoteDay(); int n; return noQuote.TryGetValue(sym, out n) && n >= 2; } }
        static void NoQuoteDay() { string d = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); if (d != noQuoteDay) { noQuote.Clear(); noQuoteDay = d; } }
        // hunters12 data M3: is this symbol's own market open right now - an index by its exchange, a typed symbol by its suffix
        // (.TO Toronto, .L London, .T Tokyo...; none: New York), futures Sunday evening to Friday evening (Eastern) but their daily hour
        public static bool OpenNow(string sym, DateTime utc)
        {
            if (string.IsNullOrEmpty(sym) || sym.StartsWith("FX:", StringComparison.Ordinal)) return false;
            if (sym == "BTCUSD" || sym == "ETHUSD") return true;   // PX-ML9: crypto trades around the clock
            if (FuturesList.Any(f => f[0] == sym))
            {
                var et = Trading.EasternNow(); var t = et.TimeOfDay; var d = et.DayOfWeek;
                if (d == DayOfWeek.Saturday) return false; if (d == DayOfWeek.Sunday) return t >= new TimeSpan(18, 0, 0); if (d == DayOfWeek.Friday) return t < new TimeSpan(17, 0, 0);
                return t < new TimeSpan(17, 0, 0) || t >= new TimeSpan(18, 0, 0);
            }
            string v = Venue(sym); var x = Exchanges.FirstOrDefault(e => e.Name == v); return x != null && State(x, utc) == "open";   // hunters14 CORE-L2: a lunch break is not open for fetching (the price can't change). Holidays are known for New York and Toronto only - elsewhere a holiday still counts as a trading day
        }
        public static string Venue(string sym)
        {
            var ix = IndexList.FirstOrDefault(i => i[0] == sym); if (ix != null) return ix[4];
            string u = (sym ?? "").ToUpperInvariant();
            foreach (var p in new[] { new[] { ".TO", "Toronto" }, new[] { ".V", "Toronto" }, new[] { ".NE", "Toronto" }, new[] { ".CN", "Toronto" }, new[] { ".L", "London" }, new[] { ".T", "Tokyo" }, new[] { ".SS", "Shanghai" }, new[] { ".SZ", "Shanghai" }, new[] { ".HK", "Hong Kong" }, new[] { ".NS", "Mumbai" }, new[] { ".BO", "Mumbai" }, new[] { ".DE", "Frankfurt" }, new[] { ".F", "Frankfurt" }, new[] { ".PA", "Paris" } })
                if (u.EndsWith(p[0], StringComparison.Ordinal)) return p[1];
            return "New York";
        }
        public static bool ValidSym(string s) { return !string.IsNullOrEmpty(s) && s.Length <= 20 && Regex.IsMatch(s, @"^[\^A-Za-z0-9.=\-]{1,20}$"); }
        // hunters15 B4: one clock per symbol, shared by the four callers (Markets indexes, futures, the widget, your companies) -
        // a symbol fetched a minute ago by one isn't fetched again by another (PX-ML2)
        static readonly Dictionary<string, DateTime> fetchedAt = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        // per-service pace (PX-ML5): Twelve Data 800 a day and 8 a minute, Finnhub 60 a minute, FMP 250 a day (free plan)
        static readonly Dictionary<string, Queue<DateTime>> perMin = new Dictionary<string, Queue<DateTime>>();
        static int MinuteCap(string prov) { return prov == "twelve" ? 8 : prov == "finnhub" ? 60 : int.MaxValue; }
        static int DayCap(string prov) { return prov == "fmp" ? 230 : prov == "twelve" ? 780 : int.MaxValue; }
        static bool Room(string prov)
        {
            if (FreePace && CallsToday(prov) >= DayCap(prov)) return false;
            lock (k) { Queue<DateTime> q; if (!perMin.TryGetValue(prov, out q)) perMin[prov] = q = new Queue<DateTime>(); while (q.Count > 0 && (DateTime.UtcNow - q.Peek()).TotalSeconds >= 60) q.Dequeue(); return q.Count < MinuteCap(prov); }
        }
        static void Spent(string prov) { lock (k) { Queue<DateTime> q; if (!perMin.TryGetValue(prov, out q)) perMin[prov] = q = new Queue<DateTime>(); q.Enqueue(DateTime.UtcNow); } if (prov != "finnhub") Call(prov); }
        // is it worth asking for this symbol now? (PX-ML1) - its market open, or no price yet since it last closed
        static bool Due(string sym)
        {
            DateTime at; lock (k) fetchedAt.TryGetValue(sym, out at);
            if ((DateTime.UtcNow - at).TotalMinutes < EveryMin) return false;
            if (OpenNow(sym, DateTime.UtcNow)) return true;
            return (DateTime.UtcNow - at).TotalHours >= 12;   // closed: one look for the closing price, then rest until it opens
        }
        static void FetchList(int qi, List<string> list, Action done)
        {
            if (qBusy || Policy.NoOnline || (DateTime.UtcNow - qTried[qi]).TotalMinutes < EveryMin) return;
            if (!HasQuoteKey()) return;
            var todo = list.Where(Due).ToList(); if (todo.Count == 0) return;
            qBusy = true; qTried[qi] = DateTime.UtcNow;
            new Thread(() =>
            {
                int got = 0, unreached = 0; bool capped = false, limited = false, badKey = false;
                var dead = new HashSet<string>();   // PX-ML6: a service that failed this round isn't asked again this round
                var fails = new Dictionary<string, int>();   // PX-ML3: failures counted per service, not per round
                try
                {
                    foreach (var sym in todo)
                    {
                        // 24 Nov (Adeeb: "give 2-3 websites in case 1 doesn't work"): each symbol is asked of the services you have a key
                        // for, in order, skipping one that can't price it, is out of calls (PX-ML4/5: skip and go on - never end the round)
                        // or failed this round; the next one answers. A call is counted only when the service answered.
                        Quote q = null; bool tried = false;
                        foreach (var pv in Providers)
                        {
                            string pk = Sports.Key(pv[0]); if (pk == "" || !Covers(pv[0], sym) || dead.Contains(pv[0])) continue;
                            if (!Room(pv[0])) { capped = true; continue; }
                            tried = true;
                            try { q = Ask(pv[0], pk, sym); Spent(pv[0]); if (q != null) break; }
                            catch (System.Net.WebException we)
                            {
                                var hr = we.Response as System.Net.HttpWebResponse; if (hr != null) Spent(pv[0]);
                                int code = hr == null ? 0 : (int)hr.StatusCode;
                                if (code == 429) { limited = true; dead.Add(pv[0]); }
                                else if (code == 401 || code == 403) { badKey = KeyCount() == 1; dead.Add(pv[0]); }
                                else { int n; fails.TryGetValue(pv[0], out n); fails[pv[0]] = ++n; if (n >= 2 || hr == null) dead.Add(pv[0]); }
                                Program.Log("quotes: " + pv[0] + " " + sym + " " + we.Status + (code > 0 ? " " + code : ""));
                            }
                        }
                        if (!tried) { if (!Providers.Any(pv => Sports.Key(pv[0]) != "" && Covers(pv[0], sym))) continue; unreached++; if (unreached >= 3 && got == 0) break; continue; }
                        lock (k) fetchedAt[sym] = DateTime.UtcNow;
                        if (q == null) { if (qi == 2) lock (k) { NoQuoteDay(); int n; noQuote.TryGetValue(sym, out n); noQuote[sym] = n + 1; } continue; }
                        if (string.IsNullOrEmpty(q.Ccy)) { var ex = Exchanges.FirstOrDefault(e => e.Name == Venue(sym)); q.Ccy = sym.EndsWith("USD", StringComparison.Ordinal) ? "USD" : ex != null ? ex.Ccy : "USD"; }   // PX-ML7
                        if (qi == 2) lock (k) noQuote.Remove(sym);
                        lock (k) { Quotes[sym] = q; Ver++; List<double> h; if (!Hist.TryGetValue(sym, out h)) Hist[sym] = h = new List<double>(); if (h.Count == 0 || h[h.Count - 1] != q.Price) h.Add(q.Price); if (h.Count > 40) h.RemoveAt(0); } got++;
                    }
                    // one honest note for the round, most important first (never the provider's own text: it can carry the key)
                    string note = badKey ? "The quote service didn't accept the key. Check it in Settings › Markets."
                        : limited ? "The price service says too many requests (its rate limit) - it tries again in " + EveryMin + " minutes."
                        : capped ? "Today's free calls are used up - the numbers are from earlier today."
                        : got == 0 && dead.Count > 0 ? "Couldn't reach the quote service just now - it tries again in " + EveryMin + " minutes." : "";
                    lock (k) QuoteNote = note;
                }
                catch (Exception e)
                {
                    if (e is OutOfMemoryException) throw;
                    lock (k) QuoteNote = "Couldn't reach the quote service just now - it tries again in " + EveryMin + " minutes.";   // R2-172
                    Program.Log("quotes: " + e.GetType().Name);
                }
                finally { qBusy = false; Done(done); }
            }) { IsBackground = true, Name = "quotes" }.Start();
        }
        // [{"symbol":"^GSPC","name":"S&P 500","price":6812.4,"changePercentage":0.42,...,"timestamp":1763740800}]
        public static Quote ParseFmp(string json, string sym)
        {
            json = json ?? ""; if (json.IndexOf("\"symbol\"", StringComparison.Ordinal) < 0) return null;
            Func<string, double> num = key => { var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?(?:[eE][-+]?[0-9]+)?)"); double v; return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; };
            var s = Regex.Match(json, "\"symbol\"\\s*:\\s*\"([^\"]{1,20})\""); if (!s.Success || s.Groups[1].Value != sym) return null;
            double price = num("price"), pct = num("changePercentage"), ts = num("timestamp");
            if (double.IsNaN(price) || price <= 0 || price > 1e7) return null;
            var q = new Quote { Sym = sym, Price = price, Pct = Math.Abs(pct) < 1000 ? pct : double.NaN, When = !double.IsNaN(ts) && ts > 946684800 && ts < 4102444800 ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(ts) : DateTime.UtcNow };
            var ix = IndexList.FirstOrDefault(i => i[0] == sym); var fu = FuturesList.FirstOrDefault(f => f[0] == sym);
            if (ix != null) { q.Name = ix[1]; q.Region = ix[2]; q.Ccy = ix[3]; }
            if (fu != null) { q.Name = fu[1]; q.Unit = fu[2]; q.Ccy = "USD"; }
            if (ix == null && fu == null) { var nm = Regex.Match(json, "\"name\"\\s*:\\s*\"([^\"]{1,60})\""); q.Name = nm.Success ? nm.Groups[1].Value : sym; }   // a typed symbol: the company's name
            return q;
        }
    }
}
