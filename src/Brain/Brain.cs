// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    // ================================================================== the brain: what the Monitor's numbers mean over time
    // Runs on the Monitor's own once-a-second thread and only does arithmetic on numbers already measured - no new packets,
    // no new processes. Once a minute it writes the history line, adds up data usage, learns what's normal for this network
    // and decides whether anything deserves an alert. Everything it keeps lives on this PC.
    static class Brain
    {
        // ---------- the network we're on (told apart by the router's hardware address - works with location off)
        public static volatile string NetKey = "", NetLabel = "";
        static DateTime lastKey = DateTime.MinValue; static readonly Dictionary<string, string> gwMacs = new Dictionary<string, string>();
        static void LearnNetwork(Monitor m)
        {
            if ((Clock.Mono - lastKey).TotalSeconds < 60 && NetKey != "") return;
            lastKey = Clock.Mono;
            string gw, ssid, kind; lock (m.L) { gw = m.Gateway; ssid = m.Ssid; kind = m.AdapterKind; }
            if (gw == "") { NetKey = "offline"; NetLabel = "Offline"; return; }
            string mac = ""; try { mac = Arp.MacOf(gw); } catch { }
            // one missed ARP read must not split a network into two rows: remember the router's MAC per gateway address
            if (mac != "") gwMacs[gw] = mac; else gwMacs.TryGetValue(gw, out mac);
            NetKey = (!string.IsNullOrEmpty(mac) ? mac : gw).Replace(':', '-');
            NetLabel = ssid != "" ? ssid : (kind == "Wi-Fi" ? "Wi-Fi" : kind) + " via " + gw;
        }

        // ---------- one-minute accumulation
        static int minute = -1, n, worst; static double cpu, mem, down, up, gw, net, wifi, rx, gwMax, netMax, mbDown, mbUp; static int gwN, netN, wifiN, rxN, lossN, lossBad, cpuN, memN, downN, upN;   // each average over its own real samples
        static DateTime lastGwAt, lastNetAt, minuteAt;   // a new sample = a new probe (by its time), never "the value changed"; minuteAt: the minute the samples belong to
        public static void Second(Monitor m)
        {
            try
            {
                LearnNetwork(m);
                var now = DateTime.Now; int mm = now.Hour * 60 + now.Minute;
                if (minute >= 0 && mm != minute && n > 0) Flush(m, minuteAt);   // stamped with its own minute: after sleep, not the minute of waking
                if (mm != minute) { minute = mm; minuteAt = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Kind); Reset(); }
                n++;
                var s = m.Latest; worst = Math.Max(worst, s.Level);
                double cv = m.Cpu.Last, mv = m.Mem.Last; if (!double.IsNaN(cv)) { cpu += cv; cpuN++; } if (!double.IsNaN(mv)) { mem += mv; memN++; }
                double d = m.Down.Last, u = m.Up.Last;
                if (!double.IsNaN(d)) { down += d; downN++; mbDown += d / 8; }
                if (!double.IsNaN(u)) { up += u; upN++; mbUp += u / 8; }
                double g = m.GwPing.Last, t = m.NetPing.Last;
                DateTime gAt = m.GwAt, tAt = m.NetAt;
                if (gAt != lastGwAt) { if (!double.IsNaN(g)) { gw += g; gwN++; gwMax = Math.Max(gwMax, g); Bloat.Sample(g, d, u); } lastGwAt = gAt; }
                if (tAt != lastNetAt) { if (double.IsNaN(t)) lossBad++; else { net += t; netN++; netMax = Math.Max(netMax, t); } lossN++; lastNetAt = tAt; }
                int sig, rate; bool call; lock (m.L) { sig = m.WifiSignal; rate = m.RxRate; call = m.CallLikely; }
                if (sig >= 0) { wifi += sig; wifiN++; }
                if (rate > 0) { rx += rate; rxN++; }
                Calls.Second(m, call, g, t, sig, rate, d, u);
                Watch.Upload(m, u, call);
                Pinned.Tick();
                // the PC itself, lightly: counters every 5 s, frozen windows every 10 s, disk users every minute, leaks every 5 min, free space every 6 h
                tick++;
                // (off the sampler thread: reading counters the first time can take seconds, and the corner widget must never wait)
                int k = Cfg.Preset == "penny" ? 3 : 1;   // Penny watches the PC three times less often
                if (tick % (5 * k) == 0) Bg(delegate { var pv = PcHealth.Sample(); double dm = pv["diskms"], pf = pv["faults"]; lock (pcLock) { if (!double.IsNaN(dm)) { diskMs += dm; diskN++; } if (!double.IsNaN(pf)) { faults += pf; faultN++; } } });
                if (tick % (10 * k) == 0) Bg(delegate { PcHealth.HungTick(m); });
                if (tick % (60 * k) == 30) Bg(PcHealth.IoTick);
                if (tick % (30 * k) == 7) Bg(delegate { RoutingTools.GwTick(m); });
                if (tick % (300 * k) == 150) Bg(PcHealth.LeakTick);
                if (tick % 21600 == 20) Bg(PcHealth.SpaceTick);
            }
            catch (Exception e) { Program.Log("brain: " + e.Message); }
        }
        static int tick; static double diskMs, faults; static int diskN, faultN; static readonly object pcLock = new object(); static int pcBusy;
        // one PC job at a time, on a background thread, lowest priority; skipped if the previous one is still running
        // one long-lived, lowest-priority worker for the PC jobs (not a new thread every 5 s); a job that comes while the last
        // one still runs is skipped
        static System.Threading.Thread bgThread; static readonly System.Threading.AutoResetEvent bgGo = new System.Threading.AutoResetEvent(false); static volatile Action bgJob;
        static void Bg(Action a)
        {
            if (System.Threading.Interlocked.CompareExchange(ref pcBusy, 1, 0) != 0) return;
            bgJob = a;
            if (bgThread == null)
            {
                bgThread = new System.Threading.Thread(() =>
                {
                    while (true)
                    {
                        bgGo.WaitOne(); var job = bgJob; bgJob = null;
                        try { if (job != null) job(); } catch (Exception e) { Program.Log("pc job: " + e.Message); } finally { System.Threading.Interlocked.Exchange(ref pcBusy, 0); }
                    }
                }) { IsBackground = true, Priority = System.Threading.ThreadPriority.Lowest, Name = "pc jobs" };
                bgThread.Start();
            }
            bgGo.Set();
        }
        static void Reset() { diskMs = faults = 0; diskN = faultN = 0; n = 0; worst = 0; cpu = mem = down = up = gw = net = wifi = rx = gwMax = netMax = mbDown = mbUp = 0; gwN = netN = wifiN = rxN = lossN = lossBad = cpuN = memN = downN = upN = 0; }
        static void Flush(Monitor m, DateTime t)
        {
            double gwAvg = gwN > 0 ? gw / gwN : double.NaN, netAvg = netN > 0 ? net / netN : double.NaN, loss = lossN > 0 ? 100.0 * lossBad / lossN : double.NaN;
            History.Minute(t, new[] { worst, cpuN > 0 ? cpu / cpuN : 0, memN > 0 ? mem / memN : 0, downN > 0 ? down / downN : 0, gwAvg, netAvg, wifiN > 0 ? wifi / wifiN : -1, upN > 0 ? up / upN : 0, gwN > 0 ? gwMax : double.NaN, netN > 0 ? netMax : double.NaN, loss, rxN > 0 ? rx / rxN : double.NaN, mbDown, mbUp }, NetKey, diskN > 0 ? diskMs / diskN : double.NaN, faultN > 0 ? faults / faultN : double.NaN);
            Usage.Add(NetKey, NetLabel, mbDown, mbUp);
            Baseline.Minute(m, NetKey, NetLabel, gwAvg, netAvg, loss);
            if (t.Minute % 10 == 0) Task.Factory.StartNew(() => Devices.Check(m));
            if (t.Minute == 5) Task.Factory.StartNew(() => { try { AirLog.Record(m); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("air: " + e.Message); } });   // neighbour wars: once an hour
            if (t.Minute == 20 && Cfg.History && (Cfg.Preset != "penny" || t.Hour == 12)) UpdateBreak.Background();   // what Windows Update changed: once an hour, in the background
        }
    }

    // ================================================================== small files in %LOCALAPPDATA%\AdeeboMeter (never leave the PC)
    static class Store
    {
        public static string PathOf(string name) { return Path.Combine(Program.DataDir, name); }
        public static List<string[]> Read(string name)
        {
            var r = new List<string[]>();
            try { var f = PathOf(name); if (File.Exists(f)) { var q = new Queue<string>(); foreach (var l in File.ReadLines(f)) { q.Enqueue(l); if (q.Count > 20000) q.Dequeue(); } foreach (var l in q) { var p = l.Split('\t'); if (p.Length > 1) r.Add(p); } } /* the NEWEST 20,000 rows: the file grows oldest-first */ }
            catch (Exception e) { Program.Log("store " + name + ": " + e.Message); }
            return r;
        }
        public static void Write(string name, IEnumerable<string[]> rows)
        {
            try
            {
                string f = PathOf(name), tmp = f + ".tmp";
                File.WriteAllLines(tmp, rows.Select(r => string.Join("\t", r.Select(x => (x ?? "").Replace('\t', ' ').Replace('\n', ' ')))));
                Program.ReplaceFile(tmp, f);
            }
            catch (Exception e) { Program.Log("store " + name + ": " + e.Message); }
        }
        public static double D(string s) { double v; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0; }
        public static string S(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
    }

    // ================================================================== data usage per day and network (DU Meter's job), kept 400 days
    static class Usage
    {
        sealed class Row { public string Day, Key, Label; public double Down, Up; }
        static List<Row> rows; static readonly object k = new object(); static DateTime saved = DateTime.UtcNow; static bool capWarned;
        static void Load() { if (rows == null) rows = Store.Read("usage.txt").Where(r => r.Length >= 5).Select(r => new Row { Day = r[0], Key = r[1], Label = r[2], Down = Store.D(r[3]), Up = Store.D(r[4]) }).ToList(); }
        public static void Add(string key, string label, double mbDown, double mbUp)
        {
            lock (k)
            {
                Load(); string day = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var r = rows.FirstOrDefault(x => x.Day == day && x.Key == key);
                if (r == null) { r = new Row { Day = day, Key = key, Label = label }; rows.Add(r); }
                r.Down += mbDown; r.Up += mbUp; r.Label = label;
                if ((DateTime.UtcNow - saved).TotalMinutes >= 5) Save();
            }
            if (Cfg.CapGB > 0)
            {
                double month = Month(null) / 1024;
                if (month >= Cfg.CapGB * 0.8 && !capWarned) { capWarned = true; Alerts.Raise("cap", "You've used " + month.ToString("0.#", CultureInfo.CurrentCulture) + " GB of your " + Cfg.CapGB + " GB this month.", 24 * 60); }
                if (month < Cfg.CapGB * 0.8) capWarned = false;
            }
        }
        public static void Save()
        {
            lock (k)
            {
                if (rows == null) return; saved = DateTime.UtcNow;
                string cut = DateTime.Today.AddDays(-400).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                rows.RemoveAll(x => string.CompareOrdinal(x.Day, cut) < 0);
                Store.Write("usage.txt", rows.Select(x => new[] { x.Day, x.Key, x.Label, Store.S(x.Down), Store.S(x.Up) }).ToList());
            }
        }
        // today's MB down and up, every network - for the widget; worked out at most every 5 s
        static double[] todayDU = { 0, 0 }; static DateTime todayAt;
        public static double[] TodayDU()
        {
            lock (k)
            {
                if ((DateTime.UtcNow - todayAt).TotalSeconds < 5) return todayDU;
                Load(); string day = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); double d = 0, u = 0;
                foreach (var x in rows) if (x.Day == day) { d += x.Down; u += x.Up; }
                todayDU = new[] { d, u }; todayAt = DateTime.UtcNow; return todayDU;
            }
        }
        // MB down + up; key null = every network
        public static double Today(string key) { return Sum(key, DateTime.Today, DateTime.Today); }
        static double monthAll = double.NaN; static DateTime monthAt;   // the widget's hover asks every second: kept 30 s
        public static double Month(string key)
        {
            var t = DateTime.Today;
            if (key == null) { lock (k) if (!double.IsNaN(monthAll) && (DateTime.UtcNow - monthAt).TotalSeconds < 30) return monthAll; double v = Sum(null, new DateTime(t.Year, t.Month, 1), t); lock (k) { monthAll = v; monthAt = DateTime.UtcNow; } return v; }
            return Sum(key, new DateTime(t.Year, t.Month, 1), t);
        }
        static double Sum(string key, DateTime from, DateTime to)
        {
            lock (k)
            {
                Load(); string a = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), b = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return rows.Where(x => (key == null || x.Key == key) && string.CompareOrdinal(x.Day, a) >= 0 && string.CompareOrdinal(x.Day, b) <= 0).Sum(x => x.Down + x.Up);
            }
        }
        // per network this month: label, MB down, MB up
        public static List<string[]> MonthByNetwork()
        {
            lock (k)
            {
                Load(); var t = DateTime.Today; string a = new DateTime(t.Year, t.Month, 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return rows.Where(x => string.CompareOrdinal(x.Day, a) >= 0).GroupBy(x => x.Key)
                    .Select(g => new[] { g.Last().Label, Store.S(g.Sum(x => x.Down)), Store.S(g.Sum(x => x.Up)) })
                    .OrderByDescending(x => Store.D(x[1]) + Store.D(x[2])).ToList();
            }
        }
        public static string Mb(double mb) { return mb >= 1024 ? (mb / 1024).ToString("0.0", CultureInfo.CurrentCulture) + " GB" : mb.ToString("0", CultureInfo.CurrentCulture) + " MB"; }
    }

    // ================================================================== what's normal for each network ("slower than usual")
    static class Baseline
    {
        sealed class B { public string Key, Label; public double Gw = double.NaN, Net = double.NaN, Loss = double.NaN; public int N; }   // NaN: never measured here (a router that never answers stays "unknown", never "0 ms")
        static List<B> list; static readonly object k = new object(); static DateTime saved = DateTime.UtcNow, warned = DateTime.MinValue;
        static readonly List<double> recentGw = new List<double>(), recentNet = new List<double>(); static string recentKey = "";
        static void Load() { if (list == null) list = Store.Read("baselines.txt").Where(r => r.Length >= 6).Select(r => new B { Key = r[0], Label = r[1], Gw = Store.D(r[2]), Net = Store.D(r[3]), Loss = Store.D(r[4]), N = (int)Store.D(r[5]) }).ToList(); }
        public static void Minute(Monitor m, string key, string label, double gw, double net, double loss)
        {
            if (key == "" || key == "offline") return;
            B b;
            lock (k)
            {
                Load(); b = list.FirstOrDefault(x => x.Key == key);
                if (b == null) { b = new B { Key = key, Label = label }; list.Add(b); }
                b.Label = label;
                // compare the last 5 minutes with what this network is normally like, before learning from them - minutes of
                // this network only: right after switching, the last network's minutes would make the new one look "slower"
                if (key != recentKey) { recentKey = key; recentGw.Clear(); recentNet.Clear(); }
                Keep(recentGw, gw); Keep(recentNet, net);
                if (b.N >= 120 && recentGw.Count >= 4 && (DateTime.UtcNow - warned).TotalMinutes > 30)
                {
                    double g5 = Avg(recentGw), n5 = Avg(recentNet);
                    if (!double.IsNaN(g5) && !double.IsNaN(b.Gw) && g5 > Math.Max(b.Gw * 2.5, b.Gw + 15))
                    { warned = DateTime.UtcNow; m.Events.Add(1, "base", "Slower than usual for " + label + ": your router answers in " + g5.ToString("0", CultureInfo.CurrentCulture) + " ms (normally " + b.Gw.ToString("0", CultureInfo.CurrentCulture) + " ms)."); Alerts.Raise("base", "Slower than usual on " + label + " - the link to your router is " + (g5 / Math.Max(1, b.Gw)).ToString("0", CultureInfo.CurrentCulture) + "x slower than normal.", 60); }
                    else if (!double.IsNaN(n5) && !double.IsNaN(b.Net) && n5 > Math.Max(b.Net * 2.5, b.Net + 40))
                    { warned = DateTime.UtcNow; m.Events.Add(1, "base", "Slower than usual for " + label + ": the internet answers in " + n5.ToString("0", CultureInfo.CurrentCulture) + " ms (normally " + b.Net.ToString("0", CultureInfo.CurrentCulture) + " ms)."); }
                }
                // learn: a slow average over roughly the last day of use
                double a = 1.0 / Math.Min(b.N + 1, 1440);
                if (!double.IsNaN(gw)) b.Gw = double.IsNaN(b.Gw) ? gw : b.Gw + (gw - b.Gw) * a;
                if (!double.IsNaN(net)) b.Net = double.IsNaN(b.Net) ? net : b.Net + (net - b.Net) * a;
                if (!double.IsNaN(loss)) b.Loss = double.IsNaN(b.Loss) ? loss : b.Loss + (loss - b.Loss) * a;
                if (!double.IsNaN(gw) || !double.IsNaN(net)) b.N++;   // only minutes that measured something teach "normal"
                if ((DateTime.UtcNow - saved).TotalMinutes >= 10) { saved = DateTime.UtcNow; Store.Write("baselines.txt", list.Select(x => new[] { x.Key, x.Label, Store.S(x.Gw), Store.S(x.Net), Store.S(x.Loss), x.N.ToString(CultureInfo.InvariantCulture) }).ToList()); }
            }
        }
        static void Keep(List<double> q, double v) { q.Add(v); while (q.Count > 5) q.RemoveAt(0); }
        static double Avg(List<double> q) { var v = q.Where(x => !double.IsNaN(x)).ToList(); return v.Count >= 4 ? v.Average() : double.NaN; }
        // "normally 4 ms to your router, 21 ms to the internet" for the current network, or "" while still learning
        public static string Normal(string key)
        {
            lock (k)
            {
                Load(); var b = list.FirstOrDefault(x => x.Key == key);
                if (b == null || b.N < 120) return b == null ? "" : "Still learning what's normal here (" + b.N + " of 120 minutes).";
                if (double.IsNaN(b.Gw) && double.IsNaN(b.Net)) return "";
                return "Normally " + (double.IsNaN(b.Gw) ? "" : b.Gw.ToString("0", CultureInfo.CurrentCulture) + " ms to your router" + (double.IsNaN(b.Net) ? "" : " and ")) + (double.IsNaN(b.Net) ? "" : b.Net.ToString("0", CultureInfo.CurrentCulture) + " ms to the internet") + " on " + b.Label + ".";
            }
        }
    }

    // ================================================================== latency under load (bufferbloat), measured passively
    static class Bloat
    {
        static readonly List<double> idle = new List<double>(), busy = new List<double>(); static readonly object k = new object();
        public static void Sample(double gwMs, double downMbps, double upMbps)
        {
            bool loaded = (!double.IsNaN(upMbps) && upMbps > 2) || (!double.IsNaN(downMbps) && downMbps > 20);
            bool quiet = (double.IsNaN(upMbps) || upMbps < 0.3) && (double.IsNaN(downMbps) || downMbps < 1);
            lock (k)
            {
                if (loaded) { busy.Add(gwMs); if (busy.Count > 120) busy.RemoveAt(0); }
                else if (quiet) { idle.Add(gwMs); if (idle.Count > 120) idle.RemoveAt(0); }
            }
        }
        static double Median(IEnumerable<double> v) { var s = v.OrderBy(x => x).ToList(); return s.Count == 0 ? double.NaN : s[s.Count / 2]; }
        // extra delay your own traffic adds at the router; NaN until there are enough busy and quiet samples
        public static double Extra(out int busyN)
        {
            lock (k) { busyN = busy.Count; return busy.Count >= 8 && idle.Count >= 8 ? Median(busy) - Median(idle) : double.NaN; }
        }
        public static string Story()
        {
            int nb; double x = Extra(out nb);
            if (double.IsNaN(x)) return "Not measured yet - it needs a few minutes of both busy and quiet traffic (" + nb + " busy samples so far).";
            if (x < 20) return "Your router keeps up: your own downloads and uploads add only " + x.ToString("0", CultureInfo.CurrentCulture) + " ms. Calls won't stutter because of it.";
            if (x < 80) return "Your own traffic adds " + x.ToString("0", CultureInfo.CurrentCulture) + " ms at the router - noticeable in games and calls during big uploads.";
            return "Your router adds " + x.ToString("0", CultureInfo.CurrentCulture) + " ms when your PC is busy uploading or downloading - that's why calls stutter during OneDrive syncs or big downloads. A router with \"SQM\" / \"smart queue\" fixes this.";
        }
    }
}
