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
    // ================================================================== Your market (1.1.7, D7 - Adeeb, 25 Nov)
    // The Markets page used to be Canada's. Now it follows a market: eleven built-in presets (one per app language plus the
    // biggest economies) that work with no key, and "Custom market" for every other country - your currency and exchange,
    // prices with your own key. Picked from the Windows region on first run, changed on the Markets page or in Settings.
    // Stored in this PC's settings; nothing is sent anywhere.
    static class MyMarket
    {
        public sealed class Preset { public string Id, Name, Ccy, Exchange, Index, Fx, Widget, Leagues, Region; }
        public static readonly Preset[] All = {
            new Preset { Id = "ca", Name = "Canada", Region = "CA", Ccy = "CAD", Exchange = "Toronto", Index = "^GSPTSE", Fx = "USD,EUR,GBP,JPY,CNY,INR", Widget = "^GSPTSE,FX:USD,GCUSD", Leagues = "usa.1" },
            new Preset { Id = "us", Name = "United States", Region = "US", Ccy = "USD", Exchange = "New York", Index = "^GSPC", Fx = "EUR,CAD,GBP,JPY,CNY,MXN", Widget = "^GSPC,FX:EUR,GCUSD", Leagues = "usa.1" },
            new Preset { Id = "uk", Name = "United Kingdom", Region = "GB", Ccy = "GBP", Exchange = "London", Index = "^FTSE", Fx = "USD,EUR,CAD,INR,JPY,CNY", Widget = "^FTSE,FX:USD,GCUSD", Leagues = "eng.1,uefa.champions" },
            new Preset { Id = "bd", Name = "Bangladesh", Region = "BD", Ccy = "BDT", Exchange = "Dhaka", Index = "", Fx = "USD,CAD,GBP,EUR,MYR,SAR", Widget = "FX:USD,FX:SAR,GCUSD", Leagues = "eng.1" },
            new Preset { Id = "fr", Name = "France", Region = "FR", Ccy = "EUR", Exchange = "Paris", Index = "^FCHI", Fx = "USD,GBP,CHF,CAD,JPY,CNY", Widget = "^FCHI,FX:USD,GCUSD", Leagues = "fra.1,uefa.champions" },
            new Preset { Id = "mx", Name = "Mexico", Region = "MX", Ccy = "MXN", Exchange = "Mexico City", Index = "^MXX", Fx = "USD,EUR,CAD,GBP,JPY,CNY", Widget = "^MXX,FX:USD,GCUSD", Leagues = "mex.1" },
            new Preset { Id = "es", Name = "Spain", Region = "ES", Ccy = "EUR", Exchange = "Madrid", Index = "^IBEX", Fx = "USD,GBP,CHF,MXN,CAD,JPY", Widget = "^IBEX,FX:USD,GCUSD", Leagues = "esp.1,uefa.champions" },
            new Preset { Id = "sa", Name = "Saudi Arabia", Region = "SA", Ccy = "SAR", Exchange = "Riyadh", Index = "^TASI.SR", Fx = "USD,EUR,GBP,INR,CNY,CAD", Widget = "^TASI.SR,FX:EUR,CLUSD", Leagues = "ksa.1" },
            new Preset { Id = "cn", Name = "China", Region = "CN", Ccy = "CNY", Exchange = "Shanghai", Index = "000001.SS", Fx = "USD,EUR,JPY,HKD,GBP,KRW", Widget = "000001.SS,FX:USD,GCUSD", Leagues = "chn.1" },
            new Preset { Id = "de", Name = "Germany", Region = "DE", Ccy = "EUR", Exchange = "Frankfurt", Index = "^GDAXI", Fx = "USD,GBP,CHF,PLN,JPY,CNY", Widget = "^GDAXI,FX:USD,GCUSD", Leagues = "ger.1,uefa.champions" },
            new Preset { Id = "jp", Name = "Japan", Region = "JP", Ccy = "JPY", Exchange = "Tokyo", Index = "^N225", Fx = "USD,EUR,CNY,KRW,GBP,AUD", Widget = "^N225,FX:USD,GCUSD", Leagues = "jpn.1" } };

        // the Windows region's two letters -> a built-in market, or "custom"
        internal static string ForRegion(string iso2)
        {
            var p = All.FirstOrDefault(a => string.Equals(a.Region, iso2, StringComparison.OrdinalIgnoreCase));
            return p != null ? p.Id : "custom";
        }
        static string RegionIso() { try { return RegionInfo.CurrentRegion.TwoLetterISORegionName; } catch (ArgumentException) { return "CA"; } }
        static string RegionCcy() { try { return RegionInfo.CurrentRegion.ISOCurrencySymbol; } catch (ArgumentException) { return "USD"; } }

        // the chosen market; an empty setting means "from the Windows region" (and is saved the first time the page opens)
        public static string Id { get { string m = Cfg.Market ?? ""; return m == "custom" || All.Any(a => a.Id == m) ? m : ForRegion(RegionIso()); } }
        public static Preset Current
        {
            get
            {
                string id = Id; var p = All.FirstOrDefault(a => a.Id == id); if (p != null) return p;
                string ccy = MarketData.KnownFx(Cfg.MarketCcy) || Cfg.MarketCcy == "CAD" ? Cfg.MarketCcy : MarketData.KnownFx(RegionCcy()) ? RegionCcy() : "USD";
                string ex = MarketData.Exchanges.Any(e => e.Name == Cfg.MarketEx) ? Cfg.MarketEx : "New York";
                return new Preset { Id = "custom", Name = "Custom market", Region = "", Ccy = ccy, Exchange = ex, Index = "", Fx = string.Join(",", new[] { "USD", "EUR", "GBP", "CAD", "JPY", "CNY" }.Where(c => c != ccy).Take(6)), Widget = ccy == "USD" ? "^GSPC,FX:EUR,GCUSD" : "^GSPC,FX:USD,GCUSD", Leagues = "" };
            }
        }
        public static string Home { get { return Current.Ccy; } }
        public static bool Canada { get { return Home == "CAD"; } }
        // the currency the page compares against when "against the US dollar" makes no sense
        public static string Anchor { get { return Home == "USD" ? "EUR" : "USD"; } }
        public static string HomeName { get { return MarketData.FxName(Home); } }
        public static string Name(Preset p) { return p.Id == "custom" ? L.T("Custom market") + " (" + p.Ccy + ")" : L.T(p.Name); }

        // switching: the lists you never changed follow the new market; the ones you made your own stay
        public static void Set(string id, string ccy, string ex)
        {
            var old = Current;
            Cfg.Market = id; if (ccy != null) Cfg.MarketCcy = ccy; if (ex != null) Cfg.MarketEx = ex;
            var now = Current;
            if (Cfg.FxList == old.Fx || Cfg.FxList == MarketData.DefaultFx) Cfg.FxList = now.Fx;
            if (Cfg.WSyms == old.Widget || Cfg.WSyms == "^GSPC,FX:USD,GCUSD") Cfg.WSyms = Corner.CleanSyms(now.Widget);
            Cfg.Save(); Interlocked.Increment(ref MarketData.Ver);
        }
        // first run: the region's market is written once, so a later change of Windows region doesn't move the page under you
        public static void Settle() { if (string.IsNullOrEmpty(Cfg.Market)) Set(Id, Id == "custom" ? Current.Ccy : null, null); }

        // ---------- currencies the Bank of Canada doesn't publish (the taka...): one free, keyless source, today's rate only
        // open.er-api.com (ExchangeRate-API's open access): one call a day, rates per Canadian dollar, kept in the data folder
        public static readonly string[][] ExtraFx = { new[] { "BDT", "Bangladeshi taka" }, new[] { "PKR", "Pakistani rupee" }, new[] { "EGP", "Egyptian pound" }, new[] { "NGN", "Nigerian naira" }, new[] { "AED", "UAE dirham" }, new[] { "PHP", "Philippine peso" } };
        static readonly object k = new object(); static Dictionary<string, double> er = new Dictionary<string, double>(); static DateTime erDay = DateTime.MinValue, erTried = DateTime.MinValue; static volatile bool erBusy;
        public static string ErNote = "";
        static string ErFile { get { return Path.Combine(Program.DataDir, "mk-er.json"); } }
        internal static Dictionary<string, double> ParseEr(string json, out DateTime day)
        {
            var o = new Dictionary<string, double>(StringComparer.Ordinal); day = DateTime.MinValue;
            if (!Regex.IsMatch(json ?? "", "\"result\"\\s*:\\s*\"success\"")) return o;
            var d = Regex.Match(json, "\"time_last_update_unix\"\\s*:\\s*(\\d+)"); long u; if (d.Success && long.TryParse(d.Groups[1].Value, out u)) day = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(u).ToLocalTime().Date;
            var rates = Regex.Match(json, "\"rates\"\\s*:\\s*\\{([^}]*)\\}"); if (!rates.Success) return o;
            foreach (Match m in Regex.Matches(rates.Groups[1].Value, "\"([A-Z]{3})\"\\s*:\\s*(-?[0-9.eE+-]+)")) { double v; if (double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0) o[m.Groups[1].Value] = v; }
            return o;
        }
        static void ErLoad()
        {
            lock (k) if (er.Count > 0) return;
            try { if (File.Exists(ErFile)) { DateTime d; var got = ParseEr(File.ReadAllText(ErFile), out d); lock (k) { er = got; erDay = d; } } }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
        }
        public static void ErFetch(Action done)
        {
            ErLoad();
            lock (k) { if (erBusy || (erDay >= DateTime.Today.AddDays(-1) && er.Count > 0) || (DateTime.UtcNow - erTried).TotalMinutes < 30) return; erBusy = true; erTried = DateTime.UtcNow; }
            if (Policy.NoOnline) { erBusy = false; return; }
            new Thread(() =>
            {
                try
                {
                    string body = HttpSmall.Get("https://open.er-api.com/v6/latest/CAD", 512); DateTime d; var got = ParseEr(body, out d);
                    if (got.Count == 0) throw new InvalidDataException("no rates in the answer");
                    lock (k) { er = got; erDay = d; ErNote = ""; }
                    try { File.WriteAllText(ErFile, body); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
                    Interlocked.Increment(ref MarketData.Ver);
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; lock (k) ErNote = "Couldn't reach the rate service for the " + string.Join(", ", ExtraFx.Select(x => x[0]).Take(1)) + " just now."; Program.Log("er-api: " + e.Message); }
                finally { erBusy = false; MarketData.Done(done); }
            }) { IsBackground = true, Name = "markets-er" }.Start();
        }
        // one unit of the code, in Canadian dollars (NaN when not known)
        public static double ErCad(string code) { ErLoad(); lock (k) { double v; return er.TryGetValue(code, out v) && v > 0 ? 1 / v : double.NaN; } }
        public static DateTime ErDay { get { lock (k) return erDay; } }
        public static bool IsExtra(string code) { return ExtraFx.Any(x => x[0] == code); }

        // ---------- exchange holidays beyond New York and Toronto: rules where the calendar has rules, hand-checked tables
        // where it follows the moon. A day past a table's end says "not checked" instead of guessing.
        static readonly Dictionary<string, DateTime> Checked = new Dictionary<string, DateTime> {
            { "Dhaka", new DateTime(2027, 12, 31) }, { "Riyadh", new DateTime(2027, 12, 31) }, { "Shanghai", new DateTime(2026, 12, 31) } };
        // published closures (exchange notices and the government calendars; Dhaka's Eid days move with the moon)
        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]> {
            { "Dhaka", new[] { "2026-12-16 Victory Day", "2026-12-25 Christmas", "2027-01-24 Shab-e-Barat", "2027-02-21 Language Martyrs' Day", "2027-03-07 Shab-e-Qadr", "2027-03-08 Eid-ul-Fitr", "2027-03-09 Eid-ul-Fitr", "2027-03-10 Eid-ul-Fitr", "2027-03-11 Eid-ul-Fitr",
                "2027-04-14 Bengali New Year", "2027-05-16 Eid-ul-Azha", "2027-05-17 Eid-ul-Azha", "2027-05-18 Eid-ul-Azha", "2027-05-19 Eid-ul-Azha", "2027-05-20 Eid-ul-Azha", "2027-06-16 Ashura", "2027-08-05 July Uprising Day", "2027-08-15 Eid-e-Miladunnabi",
                "2027-08-25 Janmashtami", "2027-10-10 Bijoya Dashami", "2027-11-07 National Revolution Day", "2027-12-16 Victory Day" } },
            { "Riyadh", new[] { "2027-02-22 Founding Day", "2027-03-07 Eid al-Fitr", "2027-03-08 Eid al-Fitr", "2027-03-09 Eid al-Fitr", "2027-03-10 Eid al-Fitr", "2027-03-11 Eid al-Fitr",
                "2027-05-16 Eid al-Adha", "2027-05-17 Eid al-Adha", "2027-05-18 Eid al-Adha", "2027-05-19 Eid al-Adha", "2027-05-20 Eid al-Adha", "2027-09-23 National Day" } },
            { "Shanghai", new[] { "2026-10-01 National Day", "2026-10-02 National Day", "2026-10-05 National Day", "2026-10-06 National Day", "2026-10-07 National Day", "2026-10-08 National Day" } } };
        // the holiday's name, "" on a trading day, null when this exchange's holidays aren't known for that day
        public static string Holiday(string exchange, DateTime day)
        {
            var d = day.Date; int y = d.Year; var easter = Markets.Easter(y);
            Func<int, int, DateTime> on = (m, dd) => new DateTime(y, m, dd);
            switch (exchange)
            {
                case "London":
                    {
                        if (d == Sub(on(1, 1))) return "New Year's Day"; if (d == easter.AddDays(-2)) return "Good Friday"; if (d == easter.AddDays(1)) return "Easter Monday";
                        if (d == Nth(y, 5, DayOfWeek.Monday, 1)) return "Early May bank holiday"; if (d == Last(y, 5, DayOfWeek.Monday)) return "Spring bank holiday"; if (d == Last(y, 8, DayOfWeek.Monday)) return "Summer bank holiday";
                        DateTime x, b; Pair(y, out x, out b); if (d == x) return "Christmas"; if (d == b) return "Boxing Day"; return "";
                    }
                case "Frankfurt":
                    if (d == on(1, 1)) return "New Year's Day"; if (d == easter.AddDays(-2)) return "Good Friday"; if (d == easter.AddDays(1)) return "Easter Monday"; if (d == on(5, 1)) return "Labour Day";
                    if (d == on(12, 24)) return "Christmas Eve"; if (d == on(12, 25)) return "Christmas"; if (d == on(12, 26)) return "Boxing Day"; if (d == on(12, 31)) return "New Year's Eve"; return "";
                case "Paris": case "Madrid":
                    if (d == on(1, 1)) return "New Year's Day"; if (d == easter.AddDays(-2)) return "Good Friday"; if (d == easter.AddDays(1)) return "Easter Monday"; if (d == on(5, 1)) return "Labour Day";
                    if (d == on(12, 25)) return "Christmas"; if (d == on(12, 26)) return "Boxing Day"; return "";
                case "Mexico City":
                    if (d == on(1, 1)) return "New Year's Day"; if (d == Nth(y, 2, DayOfWeek.Monday, 1)) return "Constitution Day"; if (d == Nth(y, 3, DayOfWeek.Monday, 3)) return "Benito Juárez's birthday";
                    if (d == easter.AddDays(-3)) return "Holy Thursday"; if (d == easter.AddDays(-2)) return "Good Friday"; if (d == on(5, 1)) return "Labour Day"; if (d == on(9, 16)) return "Independence Day";
                    if (d == on(11, 2)) return "Day of the Dead"; if (d == Nth(y, 11, DayOfWeek.Monday, 3)) return "Revolution Day"; if (d == on(12, 12)) return "Our Lady of Guadalupe"; if (d == on(12, 25)) return "Christmas"; return "";
                case "Tokyo": return Japan(d);
                case "Dhaka": case "Riyadh": case "Shanghai":
                    {
                        DateTime until; if (!Checked.TryGetValue(exchange, out until) || d > until) return null;
                        foreach (var t in Table[exchange]) if (t.StartsWith(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal)) return t.Substring(11);
                        if (exchange == "Dhaka" && ((d.Month == 2 && d.Day == 21) || (d.Month == 3 && d.Day == 26) || (d.Month == 5 && d.Day == 1) || (d.Month == 12 && (d.Day == 16 || d.Day == 25)))) return "Public holiday";
                        if (exchange == "Riyadh" && ((d.Month == 2 && d.Day == 22) || (d.Month == 9 && d.Day == 23))) return d.Month == 2 ? "Founding Day" : "National Day";
                        return "";
                    }
                default: return null;
            }
        }
        // Japan: the national holidays with the substitute (Sunday -> Monday) and "sandwiched" rules, the equinoxes by formula,
        // and the exchange's own 31 Dec - 3 Jan break
        internal static string Japan(DateTime d)
        {
            if ((d.Month == 12 && d.Day == 31) || (d.Month == 1 && d.Day <= 3)) return "New Year break";
            string n = JpDay(d); if (n != null) return n;
            // a holiday on a Sunday moves to the next day that isn't already a holiday (Golden Week can push it to Wednesday)
            for (var p = d.AddDays(-1); JpDay(p) != null; p = p.AddDays(-1)) if (p.DayOfWeek == DayOfWeek.Sunday) return "Substitute holiday";
            if (JpDay(d.AddDays(-1)) != null && JpDay(d.AddDays(1)) != null) return "Citizens' holiday";
            return "";
        }
        static string JpDay(DateTime d)
        {
            int y = d.Year, m = d.Month, dd = d.Day, k1980 = y - 1980;
            int spring = (int)Math.Floor(20.8431 + 0.242194 * k1980 - Math.Floor(k1980 / 4.0)), autumn = (int)Math.Floor(23.2488 + 0.242194 * k1980 - Math.Floor(k1980 / 4.0));
            if (m == 1 && dd == 1) return "New Year's Day"; if (d == Nth(y, 1, DayOfWeek.Monday, 2)) return "Coming of Age Day"; if (m == 2 && dd == 11) return "National Foundation Day"; if (m == 2 && dd == 23) return "Emperor's Birthday";
            if (m == 3 && dd == spring) return "Spring Equinox"; if (m == 4 && dd == 29) return "Showa Day"; if (m == 5 && dd == 3) return "Constitution Day"; if (m == 5 && dd == 4) return "Greenery Day"; if (m == 5 && dd == 5) return "Children's Day";
            if (d == Nth(y, 7, DayOfWeek.Monday, 3)) return "Marine Day"; if (m == 8 && dd == 11) return "Mountain Day"; if (d == Nth(y, 9, DayOfWeek.Monday, 3)) return "Respect for the Aged Day"; if (m == 9 && dd == autumn) return "Autumn Equinox";
            if (d == Nth(y, 10, DayOfWeek.Monday, 2)) return "Sports Day"; if (m == 11 && dd == 3) return "Culture Day"; if (m == 11 && dd == 23) return "Labour Thanksgiving Day";
            return null;
        }
        static DateTime Sub(DateTime h) { return h.DayOfWeek == DayOfWeek.Saturday ? h.AddDays(2) : h.DayOfWeek == DayOfWeek.Sunday ? h.AddDays(1) : h; }
        static void Pair(int y, out DateTime x, out DateTime b)
        {
            x = new DateTime(y, 12, 25); b = new DateTime(y, 12, 26);
            if (x.DayOfWeek == DayOfWeek.Saturday) { x = x.AddDays(2); b = b.AddDays(2); } else if (x.DayOfWeek == DayOfWeek.Sunday) { x = x.AddDays(1); b = b.AddDays(1); } else if (b.DayOfWeek == DayOfWeek.Saturday) b = b.AddDays(2);
        }
        static DateTime Nth(int y, int month, DayOfWeek dow, int n) { var d = new DateTime(y, month, 1); while (d.DayOfWeek != dow) d = d.AddDays(1); return d.AddDays(7 * (n - 1)); }
        static DateTime Last(int y, int month, DayOfWeek dow) { var d = new DateTime(y, month, DateTime.DaysInMonth(y, month)); while (d.DayOfWeek != dow) d = d.AddDays(-1); return d; }

        // ---------- the football leagues ESPN scores with no key; your market's lead, then the classic six, then any you add
        public static readonly string[][] Leagues = {
            new[] { "eng.1", "Premier League" }, new[] { "esp.1", "LaLiga" }, new[] { "ger.1", "Bundesliga" }, new[] { "ita.1", "Serie A" }, new[] { "fra.1", "Ligue 1" }, new[] { "uefa.champions", "Champions League" },
            new[] { "usa.1", "MLS" }, new[] { "mex.1", "Liga MX" }, new[] { "ksa.1", "Saudi Pro League" }, new[] { "chn.1", "Chinese Super League" }, new[] { "jpn.1", "J1 League" } };
        static readonly string[] Classic = { "eng.1", "esp.1", "ger.1", "ita.1", "uefa.champions", "usa.1" };
        // Cfg.SportLeagues: "+code" turns one on, "-code" turns one off - your picks survive a market change
        public static List<string[]> LeaguesOn()
        {
            var picks = (Cfg.SportLeagues ?? "").Split(',').Select(z => z.Trim()).Where(z => z.Length > 1).ToList();
            var lead = Current.Leagues.Split(',').Where(z => z != "").ToList();
            Func<string, bool> on = c => picks.Contains("+" + c) || (!picks.Contains("-" + c) && (lead.Contains(c) || Classic.Contains(c)));
            return lead.Concat(Leagues.Select(l => l[0])).Distinct().Where(on).Select(c => Leagues.First(l => l[0] == c)).ToList();
        }
        public static bool LeadLeague(string code) { return Current.Leagues.Split(',').Contains(code); }
        public static void ToggleLeague(string code)
        {
            bool now = LeaguesOn().Any(l => l[0] == code);
            var picks = (Cfg.SportLeagues ?? "").Split(',').Select(z => z.Trim()).Where(z => z.Length > 1 && z.Substring(1) != code).ToList();
            picks.Add((now ? "-" : "+") + code); Cfg.SportLeagues = string.Join(",", picks); Cfg.Save();
        }
    }
}
