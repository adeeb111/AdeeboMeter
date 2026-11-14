// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Trading (PLAN-modes.md 3)
    // Active traders: a light TCP knock on their broker (Penny every 30 s, Balanced 10 s, Gamer 5 s, every 2 s only with the
    // engineer Details switch on), only while the market is open (or 24/7 for crypto, if
    // they say so) - delay, jitter, loss, whose fault, and an alert before they click Buy. Never logs in, never sees an
    // account, never suggests a trade. Casual investors: end-of-day numbers (currencies from the Bank of Canada first).
    static class Trading
    {
        public static readonly object k = new object();
        public static long Ver;
        public static WatchKit.Session Day;                 // today's broker session (saved when the market closes)
        public static List<WatchKit.Sample> Samples = new List<WatchKit.Sample>();   // the last 10 minutes
        static WatchKit.DipMaker maker = new WatchKit.DipMaker();
        static Thread thread; static volatile bool running; public static volatile bool stopAll;
        static IPAddress hostIp; static DateTime resolvedAt = DateTime.MinValue; static string resolvedFor = "";
        public static string Status = "";
        public const string FarName = "your broker";
        // how often: the preset's pace - a check every 2 s is a lot of knocks on someone else's server (about 12,000 a
        // trading day), so only the engineer view asks for it
        public static int Sec { get { return Cfg.Details ? 2 : Cfg.Preset == "penny" ? 30 : Cfg.Preset == "gamer" ? 5 : 10; } }

        // ---------- market hours (US/Canada stocks: 9:30-16:00 Eastern, Monday to Friday; holidays are not known here)
        static TimeZoneInfo eastern;
        public static DateTime EasternNow()
        {
            try { if (eastern == null) eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); return TimeZoneInfo.ConvertTime(DateTime.Now, eastern); }
            catch (Exception e) { if (!(e is TimeZoneNotFoundException || e is InvalidTimeZoneException)) throw; return DateTime.UtcNow.AddHours(-5); }
        }
        public static bool MarketOpen(DateTime et) { return et.DayOfWeek != DayOfWeek.Saturday && et.DayOfWeek != DayOfWeek.Sunday && et.TimeOfDay >= new TimeSpan(9, 30, 0) && et.TimeOfDay < new TimeSpan(16, 0, 0); }
        public static bool Watching { get { return Cfg.TradeLive && !Cfg.Off("trade") && Cfg.TradeHost != "" && (Cfg.TradeCrypto || MarketOpen(EasternNow())); } }

        // started by the Monitor's probe loop: a thread of its own that ends by itself when the market closes
        public static void Ensure(Monitor m)
        {
            if (running || !Watching || m == null) return;
            running = true; stopAll = false;
            thread = new Thread(() => Loop(m)) { IsBackground = true, Name = "broker watch", Priority = ThreadPriority.BelowNormal }; thread.Start();
        }
        static void Loop(Monitor m)
        {
            try
            {
                lock (k) { if (Day == null || Day.Start.Date != DateTime.Today || Day.Far != Cfg.TradeHost) { Close(); Day = new WatchKit.Session { Mode = "trade", What = Cfg.TradeHost, Via = "", Far = Cfg.TradeHost, Start = DateTime.Now, End = DateTime.Now }; } Ver++; }
                while (Watching && !stopAll)
                {
                    var t0 = DateTime.UtcNow; Step(m);
                    int wait = Sec * 1000 - (int)(DateTime.UtcNow - t0).TotalMilliseconds; if (wait > 0) Thread.Sleep(wait);
                }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("broker watch: " + e.Message); }
            finally { lock (k) { Close(); Ver++; } running = false; }
        }
        static void Step(Monitor m)
        {
            string host = Cfg.TradeHost;
            if (hostIp == null || resolvedFor != host || (DateTime.UtcNow - resolvedAt).TotalMinutes > 10)
            {
                resolvedFor = host; resolvedAt = DateTime.UtcNow; IPAddress ip;
                if (IPAddress.TryParse(host, out ip)) hostIp = ip;
                else try { hostIp = Dns.GetHostAddresses(host).FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) ?? Dns.GetHostAddresses(host).FirstOrDefault(); }
                    catch (Exception e) { if (!(e is System.Net.Sockets.SocketException || e is ArgumentException)) throw; hostIp = null; Status = "Can't find " + host + " - check the name in Trading."; }
            }
            var s = new WatchKit.Sample { T = DateTime.Now };
            if (hostIp != null) { s.Far = GamePing.TcpMs(hostIp, 443, 1500); s.FarLost = double.IsNaN(s.Far); if (!s.FarLost) Status = ""; }
            lock (m.L)
            {
                s.Router = m.GwPing.Last; s.RouterLost = m.GwEverReplied && double.IsNaN(s.Router); s.Net = m.NetPing.Last; s.NetLost = m.NetEverReplied && double.IsNaN(s.Net);
                s.Cpu = m.Cpu.Last; s.Wifi = m.AdapterKind == "Wi-Fi"; s.Signal = m.WifiSignal;
            }
            lock (k)
            {
                if (Day == null) return;
                Samples.Add(s); while (Samples.Count > 600 / Sec) Samples.RemoveAt(0);   // the last 10 minutes
                var farOk = Samples.Where(z => !double.IsNaN(z.Far)).Select(z => z.Far).ToList(); double normal = Streams.Median(farOk);
                // the trader's own rule: over 100 ms to the broker, or a lost reply, is a moment to wait
                string blame = WatchKit.Judge(s, normal);
                if (blame == null && (!double.IsNaN(s.Far) && s.Far > 100)) blame = "far";
                if (blame == "far" && s.FarLost && farOk.Count == 0) blame = null;
                var d = maker.Add(s, blame, Sec, FarName); if (d != null) Day.Dips.Add(d);
                if (blame != null && Cfg.TradeAlert) Alerts.Raise("broker", "Wait before you trade: " + WatchKit.BlameName(blame, FarName).ToLowerInvariant() + " - " + WatchKit.Evidence(s, blame, FarName), 2);
                int mi = (int)(s.T - Day.Start).TotalMinutes;
                if (Day.Line.Count == 0 || Day.Line.Last()[0] < mi) Day.Line.Add(new[] { (float)mi, 0f, double.IsNaN(s.Far) ? -1f : (float)s.Far });
                Day.End = s.T; Day.FarNormal = normal; Ver++;
            }
        }
        static void Close()
        {
            if (Day == null) return;
            var d = maker.Close(FarName); if (d != null) Day.Dips.Add(d);
            if (Day.Length.TotalMinutes >= 5) WatchKit.Save(Day);
            Day = null; Samples.Clear();
        }
        // the verdict line at the top of the panel
        public static string Headline(out int lv)
        {
            lock (k)
            {
                var last = Samples.LastOrDefault(); lv = 3;
                if (last == null) return Status != "" ? Status : "Starting...";
                var recent = Samples.Skip(Math.Max(0, Samples.Count - 5)).ToList();
                bool bad = recent.Any(z => z.FarLost || (!double.IsNaN(z.Far) && z.Far > 100) || z.RouterLost || z.NetLost);
                if (!bad && double.IsNaN(last.Far)) { lv = 3; return Status != "" ? Status : "Waiting for " + FarName + " to answer..."; }
                lv = bad ? 2 : 0;
                return bad ? "Slow - wait a moment before you trade" : "Safe to trade · " + FarName + " answering in " + (double.IsNaN(last.Far) ? "-" : last.Far.ToString("0", CultureInfo.InvariantCulture) + " ms");
            }
        }

        // ---------- currencies, end of day: the Bank of Canada's daily rates (official, free, no key)
        // One fetch a day, after the Bank publishes (about 16:30 Eastern), only while the Trading page is open.
        public static readonly string[] Fx = { "USD", "EUR", "GBP", "CNY", "JPY", "INR", "CHF", "AUD", "MXN", "HKD" };
        public sealed class Rate { public string Code; public DateTime Day; public double Cad, Prev = double.NaN; }
        public static List<Rate> Rates = new List<Rate>(); public static string FxNote = ""; static volatile bool fxBusy; static DateTime fxTried = DateTime.MinValue;
        static string FxFile { get { return Path.Combine(Program.DataDir, "fx.txt"); } }
        public static void LoadFx()
        {
            lock (k)
            {
                if (Rates.Count > 0) return;
                try { if (File.Exists(FxFile)) Rates = ParseFx(File.ReadAllText(FxFile)); }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            }
        }
        public static bool FxStale()
        {
            lock (k)
            {
                var et = EasternNow(); var want = et.TimeOfDay >= new TimeSpan(16, 45, 0) ? et.Date : et.Date.AddDays(-1);
                while (want.DayOfWeek == DayOfWeek.Saturday || want.DayOfWeek == DayOfWeek.Sunday) want = want.AddDays(-1);
                return Rates.Count == 0 || Rates.Max(r => r.Day) < want;
            }
        }
        public static void FetchFx(Action done)
        {
            if (fxBusy || Policy.NoOnline || !Cfg.TradeFx || (DateTime.UtcNow - fxTried).TotalMinutes < 30) return;
            fxBusy = true; fxTried = DateTime.UtcNow;
            new Thread(() =>
            {
                try
                {
                    string url = "https://www.bankofcanada.ca/valet/observations/" + string.Join(",", Fx.Select(c => "FX" + c + "CAD")) + "/json?recent=7";
                    var q = (HttpWebRequest)WebRequest.Create(url); q.Timeout = 10000; q.ReadWriteTimeout = 10000; q.UserAgent = "AdeeboMeter/" + Program.Version; q.Proxy = WebRequest.GetSystemWebProxy();
                    string body; using (var r = q.GetResponse()) using (var rd = new StreamReader(r.GetResponseStream())) body = rd.ReadToEnd();
                    if (body.Length > 200000) throw new InvalidDataException("answer too large");
                    var l = ParseFx(body);
                    if (l.Count == 0) throw new InvalidDataException("no rates in the answer");
                    lock (k) { Rates = l; FxNote = ""; Ver++; }
                    try { File.WriteAllText(FxFile, body); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; lock (k) FxNote = Rates.Count > 0 ? "Couldn't reach the Bank of Canada just now - showing the last rates." : "Couldn't reach the Bank of Canada just now - it tries again in 30 minutes."; Program.Log("fx: " + e.Message); }
                finally { fxBusy = false; if (done != null) done(); }
            }) { IsBackground = true, Name = "fx" }.Start();
        }
        static readonly Regex obsRx = new Regex("\\{\\s*\"d\"\\s*:\\s*\"(\\d{4}-\\d{2}-\\d{2})\"(.*?)\\}\\s*\\}", RegexOptions.Singleline);
        static readonly Regex valRx = new Regex("\"FX([A-Z]{3})CAD\"\\s*:\\s*\\{\\s*\"v\"\\s*:\\s*\"([0-9.]+)\"");
        // {"observations":[{"d":"2026-11-13","FXUSDCAD":{"v":"1.3950"},"FXEURCAD":{"v":"1.5021"}},...]}  - the last two days per currency
        public static List<Rate> ParseFx(string json)
        {
            var byCode = new Dictionary<string, List<Tuple<DateTime, double>>>();
            foreach (Match o in obsRx.Matches(json ?? ""))
            {
                DateTime d; if (!DateTime.TryParseExact(o.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) continue;
                foreach (Match v in valRx.Matches(o.Value))
                {
                    double x; if (!double.TryParse(v.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out x) || x <= 0 || x > 1e6) continue;
                    List<Tuple<DateTime, double>> l; if (!byCode.TryGetValue(v.Groups[1].Value, out l)) byCode[v.Groups[1].Value] = l = new List<Tuple<DateTime, double>>();
                    l.Add(Tuple.Create(d, x));
                }
            }
            var r = new List<Rate>();
            foreach (var c in Fx)
            {
                List<Tuple<DateTime, double>> l; if (!byCode.TryGetValue(c, out l) || l.Count == 0) continue;
                var o = l.OrderByDescending(t => t.Item1).ToList();
                r.Add(new Rate { Code = c, Day = o[0].Item1, Cad = o[0].Item2, Prev = o.Count > 1 ? o[1].Item2 : double.NaN });
            }
            return r;
        }
        public static readonly Dictionary<string, string> FxNames = new Dictionary<string, string> { { "USD", "US dollar" }, { "EUR", "Euro" }, { "GBP", "British pound" }, { "CNY", "Chinese yuan" }, { "JPY", "Japanese yen" }, { "INR", "Indian rupee" }, { "CHF", "Swiss franc" }, { "AUD", "Australian dollar" }, { "MXN", "Mexican peso" }, { "HKD", "Hong Kong dollar" } };
    }
}
