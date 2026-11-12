// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== Adeebo Points
    // A small reward for learning: open a tool's Example, then run it for real and get a result -> +2 (+4 the longer
    // tests), once. Learn topics, their check questions, lab challenges, subnetting streaks, the packet journey and the
    // learning paths earn too (see Value). 500 is the Adeebo Award. Tools where a second look teaches something carry a
    // replay bonus: the first real run again, at least 10 minutes later -> +3, once. Everything after that, a stopped run or a run that only asked for input -> 0.
    // Off until you turn it on (Settings); off pauses earning and keeps the total. Local only: one line per tool in
    // points.txt in AdeeboMeter's data folder ("id|first award time|replay award time"), written once per award - so
    // reopening the app, pressing Run twice or replaying again can never add more.
    sealed class Points
    {
        // the replay bonus is for tests worth repeating to compare - never the ones that move a lot of data (speed test, iperf3, bufferbloat)
        public static readonly HashSet<string> Replay = new HashSet<string>(StringComparer.Ordinal) { "ispreport", "gamecompare", "beforeafter", "certcmp", "mtr" };
        // the Traffic Lab senders and the scanners behind the "my own network" tick never earn - nobody should send test
        // traffic, or tick "I'm allowed" on a company network, just for points (the self-test checks every gated tool is here)
        public static readonly HashSet<string> NoEarn = new HashSet<string>(StringComparer.Ordinal) { "udpsend", "tcpsend", "trap", "netflow", "syslog", "icmpb", "arpwho", "dhcpopt", "manyports", "sweep", "twamp", "udpmtu", "radiuslab", "smtppath", "dnsrace" };
        // longer tests are worth +2 the first time; everything else +1 (a Learn topic read to the end, a page's first use: +1)
        public static readonly HashSet<string> Big = new HashSet<string>(StringComparer.Ordinal) { "mtr", "ispreport", "speedtest", "iperf", "bloat", "gamecompare", "mtu" };
        public static readonly TimeSpan ReplayGap = TimeSpan.FromMinutes(10);
        // 1.1.5: every item is worth more, and there is more to earn (about 580 in all), so 500 means you've done nearly everything
        public const int AwardAt = 500, ReplayPts = 3;
        static readonly string[][] Ranks = { new[] { "0", "Beginner" }, new[] { "30", "Learner" }, new[] { "100", "Troubleshooter" }, new[] { "200", "Expert" }, new[] { "300", "Network detective" }, new[] { "400", "Packet whisperer" }, new[] { "500", "Adeebo Award" } };
        public static int[] Floors { get { return Ranks.Select(r => int.Parse(r[0], CultureInfo.InvariantCulture)).ToArray(); } }
        // a tool +2 (the longer tests +4), a Learn topic read +2, its check question +2, a page's first use +2, a lab challenge +4,
        // subnetting 5 in a row +3 and 10 in a row +5, the first packet journey explained +3, a learning path finished +10
        public static int Value(string id)
        {
            id = id ?? ""; if (NoEarn.Contains(id)) return 0;
            if (id.StartsWith("path.", StringComparison.Ordinal)) return 10;
            if (id.StartsWith("lab.", StringComparison.Ordinal)) return 4;
            if (id.StartsWith("game.", StringComparison.Ordinal)) return LearnGames.Pts(id);   // Learn › Games: first solve of each scenario
            if (id == "quiz.5" || id == "journey.explain") return 3;
            if (id == "quiz.10") return 5;
            return Big.Contains(id) ? 4 : 2;
        }
        public static string Next(int total, out int left) { foreach (var x in Ranks) { int at = int.Parse(x[0], CultureInfo.InvariantCulture); if (total < at) { left = at - total; return x[1]; } } left = 0; return ""; }

        sealed class Entry { public DateTime First, Again; public int Pts = 1; public long Tick; }   // Tick: ms since Windows started, at the first award (the replay wait survives a restart)
        readonly Dictionary<string, DateTime> firstMono = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        readonly string file; readonly Dictionary<string, Entry> map = new Dictionary<string, Entry>(StringComparer.Ordinal); readonly object k = new object();

        public Points(string path) { file = path; Load(); }
        static Points me;
        public static Points Me { get { if (me == null) me = new Points(Path.Combine(Program.DataDir, "points.txt")); return me; } }

        bool loadFailed;   // the file was there but couldn't be read (a scanner or OneDrive held it): never overwrite it from an empty map
        void Load()
        {
            loadFailed = false;
            try
            {
                if (!File.Exists(file)) return;
                foreach (var line in File.ReadAllLines(file))
                {
                    var p = line.Split('|'); if (p.Length < 2 || p[0].Trim() == "") continue;
                    long a, b; var e = new Entry();
                    // a damaged line (a bit flip, ticks out of range) is skipped - it never takes the whole file down with it
                    if (long.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out a) && a > 0 && a <= DateTime.MaxValue.Ticks) e.First = new DateTime(a, DateTimeKind.Utc); else continue;
                    if (p.Length > 2 && long.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out b) && b > 0 && b <= DateTime.MaxValue.Ticks) e.Again = new DateTime(b, DateTimeKind.Utc);
                    int pts; if (p.Length > 3 && int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out pts) && pts >= 0 && pts <= 10) e.Pts = pts;
                    long tk; if (p.Length > 4 && long.TryParse(p[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out tk) && tk > 0) e.Tick = tk;
                    map[p[0].Trim()] = e;
                }
            }
            catch (IOException e) { loadFailed = true; Program.Log("points: " + e.Message); }
            catch (UnauthorizedAccessException e) { loadFailed = true; Program.Log("points: " + e.Message); }
        }
        void Save()
        {
            if (loadFailed) { Load(); if (loadFailed) return; }   // read what's saved first (merged with today's award), or wait for next time
            var lines = map.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "|" + kv.Value.First.Ticks.ToString(CultureInfo.InvariantCulture) + "|" + (kv.Value.Again == default(DateTime) ? "-" : kv.Value.Again.Ticks.ToString(CultureInfo.InvariantCulture)) + "|" + kv.Value.Pts.ToString(CultureInfo.InvariantCulture) + "|" + kv.Value.Tick.ToString(CultureInfo.InvariantCulture));
            string tmp = file + ".tmp";
            File.WriteAllLines(tmp, lines);
            Program.ReplaceFile(tmp, file);   // never a half-written file
        }

        // counted by today's values, so points earned before 1.1.5 move up with everyone else's
        // Only items AdeeboMeter can really award count (a line typed into points.txt for something that doesn't exist
        // counts 0), the replay bonus only for the replay tools, and never more than everything there is to earn.
        public int Total { get { lock (k) return Math.Min(Max, map.Where(kv => Known(kv.Key)).Sum(kv => Value(kv.Key) + (kv.Value.Again != default(DateTime) && Replay.Contains(kv.Key) ? ReplayPts : 0))); } }
        public const int Max = 800;   // a little above everything there is (about 750 - the self-test fails if it grows past this): a file saying more was edited by hand
        static readonly Dictionary<string, bool> known = new Dictionary<string, bool>(StringComparer.Ordinal);
        public static bool Known(string id)
        {
            lock (known) { bool v; if (known.TryGetValue(id, out v)) return v; v = FullView.PtKeyKnown(id); if (known.Count < 5000) known[id] = v; return v; }
        }
        // a question answered wrong the first time: kept (0 points) so a restart can't pay for it
        public bool Tried(string id) { lock (k) return map.ContainsKey("tried." + id); }
        public void MarkTried(string id)
        {
            lock (k) { if (map.ContainsKey("tried." + id) || map.ContainsKey(id)) return; map["tried." + id] = new Entry { First = DateTime.UtcNow, Pts = 0 }; if (!TrySave()) map.Remove("tried." + id); }
        }
        public int Done { get { lock (k) return map.Keys.Count(Known); } }
        public bool Has(string id) { lock (k) return map.ContainsKey(id); }
        public bool Replayed(string id) { lock (k) { Entry e; return map.TryGetValue(id, out e) && e.Again != default(DateTime); } }
        public static string Rank(int total) { string r = Ranks[0][1]; foreach (var x in Ranks) if (total >= int.Parse(x[0], CultureInfo.InvariantCulture)) r = x[1]; return r; }

        // a real run of a tool finished with results. opened = its Example was opened first. Returns the points awarded (0 when nothing new).
        public int Award(string id, bool opened, bool earning, DateTime nowUtc) { return Award(id, opened, earning, nowUtc, Clock.Mono, BootMs()); }
        // mono: the steady clock (the self-test passes its own)
        public int Award(string id, bool opened, bool earning, DateTime nowUtc, DateTime mono) { return Award(id, opened, earning, nowUtc, mono, (mono.Ticks / 10000) & 0xFFFFFFFFL); }   // the self-test's own clocks
        internal int Award(string id, bool opened, bool earning, DateTime nowUtc, DateTime mono, long boot)
        {
            if (!earning || string.IsNullOrEmpty(id) || NoEarn.Contains(id)) return 0;
            lock (k)
            {
                Entry e;
                if (!map.TryGetValue(id, out e))
                {
                    if (!opened) return 0;
                    int v = Value(id); map[id] = new Entry { First = nowUtc, Pts = v, Tick = boot };
                    if (!TrySave()) { map.Remove(id); return 0; }
                    firstMono[id] = mono;
                    return v;
                }
                // the clock went back since the first run: start the 10 minutes again from now
                if (e.First > nowUtc.AddMinutes(1)) { e.First = nowUtc; TrySave(); firstMono[id] = mono; return 0; }
                // first run in this session: the 10 minutes are counted on the steady clock too (moving the Windows clock doesn't help)
                DateTime fm; if (firstMono.TryGetValue(id, out fm) && mono - fm < ReplayGap) return 0;
                // after a restart: the same Windows session (ms since boot still counting up from the first award) must also
                // have run 10 minutes - moving the clock forward and restarting doesn't help
                // The value carries Windows' boot number in its high half (0 = unknown): a different boot is a real restart of the PC,
                // never a reason to wait - only the same boot is held to the 10 minutes of uptime (round-3 #10)
                long bootNow = boot >> 32, bootThen = e.Tick >> 32, bt = boot & 0xFFFFFFFFL, et = e.Tick & 0xFFFFFFFFL;
                bool sameBoot = bootNow == 0 || bootThen == 0 || bootNow == bootThen;
                if (sameBoot && et > 0 && bt >= et && bt - et < (long)ReplayGap.TotalMilliseconds) return 0;
                if (!Replay.Contains(id) || e.Again != default(DateTime) || nowUtc - e.First < ReplayGap) return 0;
                e.Again = nowUtc;
                if (!TrySave()) { e.Again = default(DateTime); return 0; }
                return ReplayPts;
            }
        }
        static long BootMs() { return BootId() << 32 | (long)(uint)Environment.TickCount; }
        // Windows counts its boots (the prefetcher's BootId); 0 when it can't be read
        static long bootId = -1;
        static long BootId()
        {
            if (bootId >= 0) return bootId; bootId = 0;
            try { using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters")) { var v = k == null ? null : k.GetValue("BootId"); if (v is int) bootId = (uint)(int)v; } }
            catch (Exception e) { if (!(e is System.Security.SecurityException || e is UnauthorizedAccessException || e is System.IO.IOException)) throw; }
            return bootId;
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
