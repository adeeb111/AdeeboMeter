// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== Adeebo Points
    // A small reward for learning the tools: open a tool's Example, then run it for real and get a result -> +1, once.
    // Eight tools where a second look teaches something carry a replay bonus: the first real run again, at least
    // 10 minutes later -> +2, once. Everything after that, a stopped run or a run that only asked for input -> 0.
    // Off until you turn it on (Settings); off pauses earning and keeps the total. Local only: one line per tool in
    // points.txt in AdeeboMeter's data folder ("id|first award time|replay award time"), written once per award - so
    // reopening the app, pressing Run twice or replaying again can never add more.
    sealed class Points
    {
        public static readonly HashSet<string> Replay = new HashSet<string>(StringComparer.Ordinal) { "ispreport", "speedtest", "gamecompare", "beforeafter", "certcmp", "mtr", "bloat", "iperf" };
        // the Traffic Lab senders never earn - nobody should send test traffic just for points
        public static readonly HashSet<string> NoEarn = new HashSet<string>(StringComparer.Ordinal) { "udpsend", "tcpsend", "trap", "netflow", "syslog" };
        // longer tests are worth +2 the first time; everything else +1 (a Learn topic read to the end, a page's first use: +1)
        public static readonly HashSet<string> Big = new HashSet<string>(StringComparer.Ordinal) { "mtr", "ispreport", "speedtest", "iperf", "bloat", "gamecompare", "finddev", "mtu" };
        public static readonly TimeSpan ReplayGap = TimeSpan.FromMinutes(10);
        static readonly string[][] Ranks = { new[] { "0", "Beginner" }, new[] { "15", "Learner" }, new[] { "50", "Troubleshooter" }, new[] { "100", "Expert" } };
        public static int Value(string id) { return NoEarn.Contains(id ?? "") ? 0 : Big.Contains(id ?? "") ? 2 : 1; }
        public static string Next(int total, out int left) { foreach (var x in Ranks) { int at = int.Parse(x[0], CultureInfo.InvariantCulture); if (total < at) { left = at - total; return x[1]; } } left = 0; return ""; }

        sealed class Entry { public DateTime First, Again; public int Pts = 1; }
        readonly string file; readonly Dictionary<string, Entry> map = new Dictionary<string, Entry>(StringComparer.Ordinal); readonly object k = new object();

        public Points(string path) { file = path; Load(); }
        static Points me;
        public static Points Me { get { if (me == null) me = new Points(Path.Combine(Program.DataDir, "points.txt")); return me; } }

        void Load()
        {
            try
            {
                if (!File.Exists(file)) return;
                foreach (var line in File.ReadAllLines(file))
                {
                    var p = line.Split('|'); if (p.Length < 2 || p[0].Trim() == "") continue;
                    long a, b; var e = new Entry();
                    if (long.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out a) && a > 0) e.First = new DateTime(a, DateTimeKind.Utc); else continue;
                    if (p.Length > 2 && long.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out b) && b > 0) e.Again = new DateTime(b, DateTimeKind.Utc);
                    int pts; if (p.Length > 3 && int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out pts) && pts >= 0 && pts <= 2) e.Pts = pts;
                    map[p[0].Trim()] = e;
                }
            }
            catch (IOException e) { Program.Log("points: " + e.Message); }
            catch (UnauthorizedAccessException e) { Program.Log("points: " + e.Message); }
        }
        void Save()
        {
            var lines = map.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "|" + kv.Value.First.Ticks.ToString(CultureInfo.InvariantCulture) + "|" + (kv.Value.Again == default(DateTime) ? "-" : kv.Value.Again.Ticks.ToString(CultureInfo.InvariantCulture)) + "|" + kv.Value.Pts.ToString(CultureInfo.InvariantCulture));
            string tmp = file + ".tmp";
            File.WriteAllLines(tmp, lines);
            Program.ReplaceFile(tmp, file);   // never a half-written file
        }

        public int Total { get { lock (k) return map.Values.Sum(e => e.Pts + (e.Again != default(DateTime) ? 2 : 0)); } }
        public int Done { get { lock (k) return map.Count; } }
        public bool Has(string id) { lock (k) return map.ContainsKey(id); }
        public bool Replayed(string id) { lock (k) { Entry e; return map.TryGetValue(id, out e) && e.Again != default(DateTime); } }
        public static string Rank(int total) { string r = Ranks[0][1]; foreach (var x in Ranks) if (total >= int.Parse(x[0], CultureInfo.InvariantCulture)) r = x[1]; return r; }

        // a real run of a tool finished with results. opened = its Example was opened first. Returns the points awarded (0, 1 or 2).
        public int Award(string id, bool opened, bool earning, DateTime nowUtc)
        {
            if (!earning || string.IsNullOrEmpty(id) || NoEarn.Contains(id)) return 0;
            lock (k)
            {
                Entry e;
                if (!map.TryGetValue(id, out e))
                {
                    if (!opened) return 0;
                    int v = Value(id); map[id] = new Entry { First = nowUtc, Pts = v };
                    if (!TrySave()) { map.Remove(id); return 0; }
                    return v;
                }
                if (!Replay.Contains(id) || e.Again != default(DateTime) || nowUtc - e.First < ReplayGap) return 0;
                e.Again = nowUtc;
                if (!TrySave()) { e.Again = default(DateTime); return 0; }
                return 2;
            }
        }
        bool TrySave()
        {
            try { Save(); return true; }
            catch (IOException e) { Program.Log("points: " + e.Message); return false; }
            catch (UnauthorizedAccessException e) { Program.Log("points: " + e.Message); return false; }
        }

        // a finished run counts when it has a real result: at least one row read, measured or seen (not only "type a host")
        public static bool Completed(List<string[]> rows, bool stopped)
        {
            if (stopped || rows == null || Examples.IsExample(rows)) return false;
            return rows.Any(r => r.Length > 4 && (r[4] == "m" || r[4] == "w" || r[4] == "p" || r[4] == "i"));
        }
    }
}
