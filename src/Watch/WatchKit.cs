// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AdeeboMeter
{
    // ================================================================== the Watch engine (PLAN-modes.md 1)
    // One set of rules for every "watch a far end" mode - Streaming now, Trading next, Gamer and Calls later: a sample of
    // the whole path every few seconds, a bad sample becomes part of a dip, each dip gets a verdict from the same evidence
    // (this PC / your Wi-Fi / your home network / your provider / the far end), and each session is kept 90 days as one
    // small record. Nothing here talks to the network: the modes measure, this judges and remembers.
    static class WatchKit
    {
        // one look at the whole path
        public sealed class Sample
        {
            public DateTime T; public double Router = double.NaN, Net = double.NaN, Far = double.NaN, Down = double.NaN, Cpu = double.NaN;
            public bool RouterLost, NetLost, FarLost, Wifi, NoProbe;   /* NoProbe: the far end couldn't be tried (its name didn't resolve) - not a reply, not a loss */ public int Signal = -1; public string Busy = "";
        }
        public sealed class Dip { public DateTime T; public int Secs; public string Blame = "", Evidence = ""; }
        public sealed class Session
        {
            public string Mode = "", What = "", Via = "", Far = ""; public DateTime Start, End; public double AvgDown = double.NaN, FarNormal = double.NaN;
            public List<Dip> Dips = new List<Dip>();
            public List<float[]> Line = new List<float[]>();   // one point a minute: minute index, download Mb/s, far ms (NaN as -1)
            public TimeSpan Length { get { return End - Start; } }
            // a reader's own copy, taken under the writer's lock - never a list that grows while it's read (AM-CG-131/148)
            public Session Copy() { return new Session { Mode = Mode, What = What, Via = Via, Far = Far, Start = Start, End = End, AvgDown = AvgDown, FarNormal = FarNormal, Dips = Dips.ToList(), Line = Line.Select(p => (float[])p.Clone()).ToList() }; }
            public Dictionary<string, int> Tally() { var d = Blames.ToDictionary(b => b, b => 0); foreach (var x in Dips) { int c; if (d.TryGetValue(x.Blame, out c)) d[x.Blame] = c + 1; } return d; }
        }
        public static readonly string[] Blames = { "pc", "wifi", "home", "provider", "far", "unsure" };
        // the 2-minute lesson that fits a verdict: { Learn topic id, what the link says }
        public static string[] Lesson(string blame, string mode)
        {
            switch (blame)
            {
                case "wifi": return new[] { "wifi", "Wi-Fi - why it drops and how to steady it" };
                case "pc": return new[] { "memory-pressure", "Why a PC gets too busy to keep up" };
                case "home": return new[] { "speed-vs-delay", "Speed vs delay - what a slow home network does" };
                case "far": return mode == "trade" ? new[] { "inv-connection", "What your connection means for trading" } : new[] { "cdn", "How streams reach you - and why a server gets slow" };
                default: return mode == "trade" ? new[] { "inv-connection", "What your connection means for trading" } : new[] { "stream-fault", "Whose fault is the buffering?" };
            }
        }
        public static string BlameName(string b, string farName)
        {
            switch (b)
            {
                case "pc": return "This PC";
                case "wifi": return "Your Wi-Fi";
                case "home": return "Your home network";
                case "provider": return "Your provider";
                case "far": return string.IsNullOrEmpty(farName) ? "The far end" : char.ToUpperInvariant(farName[0]) + farName.Substring(1);
                default: return "Not sure";
            }
        }

        // The verdict for one sample: null = fine. Earliest broken hop wins - a slow router makes everything after it slow
        // too, so it's never the provider's fault when the router itself is struggling.
        public static string Judge(Sample s, double farNormal)
        {
            if (!double.IsNaN(s.Cpu) && s.Cpu >= 92) return "pc";
            if (s.RouterLost || (!double.IsNaN(s.Router) && s.Router > 80)) return s.Wifi ? "wifi" : "home";
            if (s.Wifi && s.Signal >= 0 && s.Signal < 30) return "wifi";
            if (s.NetLost || (!double.IsNaN(s.Net) && s.Net > 250)) return "provider";
            if (s.FarLost || (!double.IsNaN(s.Far) && s.Far > Math.Max(150, (double.IsNaN(farNormal) ? 50 : farNormal) * 3))) return "far";
            return null;
        }
        static string Cap(string t) { return string.IsNullOrEmpty(t) ? t : char.ToUpperInvariant(t[0]) + t.Substring(1); }
        public static string Evidence(Sample s, string blame, string farName)
        {
            Func<double, string> ms = v => double.IsNaN(v) ? "-" : v.ToString("0", CultureInfo.InvariantCulture) + " ms";
            string far = string.IsNullOrEmpty(farName) ? "the far end" : farName;
            switch (blame)
            {
                case "pc": return "Processor at " + s.Cpu.ToString("0", CultureInfo.InvariantCulture) + "%" + (s.Busy != "" ? " (" + s.Busy + ")" : "") + " - the PC itself was too busy.";
                case "wifi": return (s.RouterLost ? "Your router stopped answering" : s.Signal >= 0 && s.Signal < 30 ? "Wi-Fi signal fell to " + s.Signal + "%" : "Router answered in " + ms(s.Router)) + (s.Signal >= 0 ? " (signal " + s.Signal + "%)" : "") + " - the problem was before your router.";
                case "home": return (s.RouterLost ? "Your router stopped answering" : "Router answered in " + ms(s.Router)) + " on a cable - your home network or router.";
                case "provider": return (s.NetLost ? "The internet check lost a reply" : "Internet answered in " + ms(s.Net)) + "; your router was fine (" + ms(s.Router) + ") - the problem was past your router.";
                case "far": return (s.FarLost ? Cap(far) + " stopped answering" : Cap(far) + " answered in " + ms(s.Far)) + "; your router (" + ms(s.Router) + ") and the internet (" + ms(s.Net) + ") were normal.";
                default: return "Something was off but the evidence doesn't point one way.";
            }
        }

        // Bad samples close together become one dip; its verdict is the most common one inside it.
        public sealed class DipMaker
        {
            readonly List<Tuple<Sample, string>> run = new List<Tuple<Sample, string>>(); DateTime lastBad = DateTime.MinValue;
            public Dip Add(Sample s, string blame, int gapSec, string farName)
            {
                Dip done = null;
                if (blame != null)
                {
                    if (run.Count > 0 && (s.T - lastBad).TotalSeconds > gapSec * 2.5) done = Close(farName);
                    run.Add(Tuple.Create(s, blame)); lastBad = s.T;
                }
                else if (run.Count > 0 && (s.T - lastBad).TotalSeconds > gapSec * 2.5) done = Close(farName);
                return done;
            }
            public Dip Close(string farName)
            {
                if (run.Count == 0) return null;
                string b = run.GroupBy(r => r.Item2).OrderByDescending(gr => gr.Count()).ThenBy(gr => Array.IndexOf(Blames, gr.Key)).First().Key;
                var worst = run.Where(r => r.Item2 == b).Last().Item1;
                var d = new Dip { T = run[0].Item1.T, Secs = Math.Max(1, (int)(lastBad - run[0].Item1.T).TotalSeconds), Blame = b, Evidence = Evidence(worst, b, farName) };
                run.Clear(); return d;
            }
            public bool Open { get { return run.Count > 0; } }
        }

        // ---------- the 90-day store: one line per session in watch-YYYYMM.log next to History
        static readonly object k = new object(); static readonly char[] Semi = { ';' };
        static string FileOf(DateTime d) { return Path.Combine(History.Dir, "watch-" + d.ToString("yyyyMM", CultureInfo.InvariantCulture) + ".log"); }
        static string Clean(string s) { return (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Replace('|', '/').Replace('~', '-').Replace(';', ','); }
        static string N(double v) { return double.IsNaN(v) ? "" : v.ToString("0.##", CultureInfo.InvariantCulture); }
        static double P(string s) { double v; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }
        public static string Encode(Session s)
        {
            var sb = new StringBuilder();
            sb.Append("1\t").Append(Clean(s.Mode)).Append('\t').Append(Clean(s.What)).Append('\t').Append(Clean(s.Via)).Append('\t').Append(Clean(s.Far)).Append('\t')
              .Append(s.Start.ToString("o", CultureInfo.InvariantCulture)).Append('\t').Append(s.End.ToString("o", CultureInfo.InvariantCulture)).Append('\t')
              .Append(N(s.AvgDown)).Append('\t').Append(N(s.FarNormal)).Append('\t');
            sb.Append(string.Join(";", s.Dips.Select(d => d.T.ToString("o", CultureInfo.InvariantCulture) + "|" + d.Secs + "|" + d.Blame + "|" + Clean(d.Evidence)))).Append('\t');
            sb.Append(string.Join(";", s.Line.Select(p => p[0].ToString("0", CultureInfo.InvariantCulture) + "~" + p[1].ToString("0.#", CultureInfo.InvariantCulture) + "~" + p[2].ToString("0", CultureInfo.InvariantCulture))));
            return sb.ToString();
        }
        public static Session Decode(string line)
        {
            var f = line.Split('\t'); if (f.Length < 11 || f[0] != "1") return null;
            DateTime a, b;
            if (!DateTime.TryParse(f[5], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out a) || !DateTime.TryParse(f[6], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out b)) return null;
            var s = new Session { Mode = f[1], What = f[2], Via = f[3], Far = f[4], Start = a, End = b, AvgDown = P(f[7]), FarNormal = P(f[8]) };
            foreach (var d in f[9].Split(Semi, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = d.Split('|'); DateTime t; int secs;
                if (p.Length < 4 || !DateTime.TryParse(p[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t) || !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out secs)) continue;
                s.Dips.Add(new Dip { T = t, Secs = secs, Blame = Blames.Contains(p[2]) ? p[2] : "unsure", Evidence = p[3] });
            }
            foreach (var pt in f[10].Split(Semi, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = pt.Split('~'); if (p.Length < 3) continue;
                s.Line.Add(new[] { (float)P(p[0]), (float)P(p[1]), (float)P(p[2]) });
            }
            return s;
        }
        public static void Save(Session s)
        {
            if (!Cfg.History) return;
            lock (k)
                try { Directory.CreateDirectory(History.Dir); File.AppendAllText(FileOf(s.Start), Encode(s) + "\n"); cache = null; Tidy(); }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("watch store: " + e.Message); }
        }
        static List<Session> cache; static DateTime cacheAt;
        public static void Drop() { lock (k) cache = null; }
        // the last 90 days, newest first (cached for a minute: the page asks on every paint)
        public static List<Session> Recent(string mode)
        {
            lock (k)
            {
                if (cache == null || (DateTime.UtcNow - cacheAt).TotalSeconds > 60)
                {
                    var l = new List<Session>(); var from = DateTime.Now.AddDays(-90);
                    try
                    {
                        if (Directory.Exists(History.Dir))
                            foreach (var f in Directory.GetFiles(History.Dir, "watch-*.log").OrderByDescending(x => x, StringComparer.Ordinal).Take(4))
                                foreach (var line in File.ReadLines(f).Take(5000)) { var s = Decode(line); if (s != null && s.Start >= from) l.Add(s); }
                    }
                    catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("watch read: " + e.Message); }
                    cache = l.OrderByDescending(s => s.Start).ToList(); cacheAt = DateTime.UtcNow;
                }
                return cache.Where(s => mode == null || s.Mode == mode).ToList();
            }
        }
        static DateTime tidied;
        static void Tidy()
        {
            if ((DateTime.Now - tidied).TotalHours < 6) return; tidied = DateTime.Now;
            string keep = FileOf(DateTime.Now.AddDays(-92));
            foreach (var f in Directory.GetFiles(History.Dir, "watch-*.log"))
                if (string.CompareOrdinal(f, keep) < 0) try { File.Delete(f); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
        }

        // ---------- what the records say over time: the hour-by-weekday grid and the monthly report
        public static int[,] DipGrid(IEnumerable<Session> ss)
        {
            var g = new int[7, 24];
            foreach (var s in ss) foreach (var d in s.Dips) g[(int)d.T.DayOfWeek, d.T.Hour]++;
            return g;
        }
        public static string MonthReport(List<Session> all, DateTime month, string mode)
        {
            var ss = all.Where(s => s.Start.Year == month.Year && s.Start.Month == month.Month).ToList();
            double hours = ss.Sum(s => s.Length.TotalHours); var dips = ss.SelectMany(s => s.Dips).ToList();
            int past = dips.Count(d => d.Blame == "provider" || d.Blame == "far");
            var sb = new StringBuilder();
            sb.AppendLine(month.ToString("MMMM yyyy", CultureInfo.CurrentCulture) + " - " + mode + " report from AdeeboMeter (measured on my PC)");
            sb.AppendLine(hours.ToString("0.#", CultureInfo.CurrentCulture) + " h watched, " + dips.Count + " dips, " + dips.Count(d => d.Blame == "provider") + " beyond my router (router replies normal, internet check failing), " + dips.Count(d => d.Blame == "far") + " at the service's own servers, " + dips.Count(d => d.Blame == "wifi" || d.Blame == "home" || d.Blame == "pc") + " in my home or PC.");
            if (dips.Count > 0)
            {
                var worst = dips.GroupBy(d => d.T.Hour).OrderByDescending(gr => gr.Count()).First().Key;
                sb.AppendLine("Most dips around " + worst.ToString("00", CultureInfo.InvariantCulture) + ":00.");
                sb.AppendLine();
                foreach (var d in dips.Where(x => x.Blame == "provider").OrderBy(x => x.T).Take(40))
                    sb.AppendLine(d.T.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "  " + d.Secs + " s  " + d.Evidence);
            }
            if (past == 0 && dips.Count == 0) sb.AppendLine("No dips this month.");
            return sb.ToString();
        }
    }
}
