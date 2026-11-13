// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // The PC itself: memory pressure, disk latency, heat / throttling, driver interrupts, frozen windows, slow leaks,
    // crashes, background jobs, what changed, battery and disk space. All local, all without admin.
    static class PcHealth
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string Gb(double b) { return (b / 1073741824.0).ToString(b >= 10737418240 ? "0" : "0.0", CultureInfo.CurrentCulture) + " GB"; }

        // ---------- performance counters (made once, read every few seconds by Brain; NaN when Windows won't give them)
        static readonly object k = new object(); static Dictionary<string, PerformanceCounter> pc; static bool pcBroken;
        static readonly string[][] Wanted = {
            new[] { "faults", "Memory", "Pages Input/sec", "" }, new[] { "commit", "Memory", "Committed Bytes", "" }, new[] { "limit", "Memory", "Commit Limit", "" },
            new[] { "diskms", "PhysicalDisk", "Avg. Disk sec/Transfer", "_Total" }, new[] { "queue", "PhysicalDisk", "Current Disk Queue Length", "_Total" },
            new[] { "perf", "Processor Information", "% Processor Performance", "_Total" }, new[] { "intr", "Processor", "% Interrupt Time", "_Total" }, new[] { "dpc", "Processor", "% DPC Time", "_Total" } };
        public static Dictionary<string, double> Sample()
        {
            var v = new Dictionary<string, double>();
            lock (k)
            {
                if (pc == null && !pcBroken)
                {
                    pc = new Dictionary<string, PerformanceCounter>();
                    foreach (var w in Wanted) try { var c = new PerformanceCounter(w[1], w[2], w[3], true); c.NextValue(); pc[w[0]] = c; } catch (Exception e) { Program.Log("counter " + w[2] + ": " + e.Message); }
                    if (pc.Count == 0) { pcBroken = true; pc = null; }
                }
                foreach (var w in Wanted) { PerformanceCounter c; double x = double.NaN; if (pc != null && pc.TryGetValue(w[0], out c)) try { x = c.NextValue(); } catch { } v[w[0]] = x; }
            }
            if (!double.IsNaN(v["diskms"])) v["diskms"] *= 1000;
            return v;
        }
        public static void Release() { lock (k) { if (pc != null) foreach (var c in pc.Values) c.Dispose(); pc = null; } }

        public static List<string[]> Pressure(Monitor m)
        {
            var rows = new List<string[]>(); var v = Sample(); Thread.Sleep(1000); v = Sample();
            double f = v["faults"], cm = v["commit"], lim = v["limit"], dms = v["diskms"], q = v["queue"], perf = v["perf"], intr = v["intr"], dpc = v["dpc"];
            rows.Add(double.IsNaN(f) ? R(3, "Memory pressure", "Windows didn't give this counter", "", "u") : R(f > 300 ? 2 : f > 60 ? 1 : 0, "Memory read back from disk", f.ToString("0", CultureInfo.CurrentCulture) + " pages/s", f > 60 ? "Memory is full, so Windows keeps reading parts of programs back from the disk - that's what feels slow. Close a heavy app (What's running shows who)." : "Memory isn't under pressure.", "w"));
            if (!double.IsNaN(cm) && !double.IsNaN(lim) && lim > 0) rows.Add(R(cm / lim > 0.9 ? 2 : cm / lim > 0.8 ? 1 : 0, "Committed memory", Gb(cm) + " of " + Gb(lim) + " (" + (100 * cm / lim).ToString("0", CultureInfo.CurrentCulture) + "%)", cm / lim > 0.9 ? "Near the limit: programs may fail to start or crash. Close apps, or let Windows grow the page file." : "", "w"));
            rows.Add(double.IsNaN(dms) ? R(3, "Disk latency", "not available", "", "u") : R(dms > 50 ? 2 : dms > 15 ? 1 : 0, "Disk latency", dms.ToString("0.0", CultureInfo.CurrentCulture) + " ms per request" + (double.IsNaN(q) ? "" : " · queue " + q.ToString("0", CultureInfo.CurrentCulture)), dms > 15 ? "Normal is under 10 ms (SSD under 2). Slow disk = slow everything. See 'Disk' for who's using it and the disk's health." : "", "w"));
            rows.Add(double.IsNaN(perf) ? R(3, "Processor speed", "not available", "", "u") : R(perf < 60 ? 1 : 0, "Processor speed", perf.ToString("0", CultureInfo.CurrentCulture) + "% of its rated speed", perf < 60 ? "It's being held back - heat, battery saver or the power plan. Plug in, check vents, Settings > Power > Best performance." : "", "w"));
            if (!double.IsNaN(intr) && !double.IsNaN(dpc)) rows.Add(R(intr + dpc > 15 ? 2 : intr + dpc > 6 ? 1 : 0, "Driver interrupts", (intr + dpc).ToString("0.0", CultureInfo.CurrentCulture) + "% of the processor", intr + dpc > 6 ? "A driver is keeping the processor busy - the usual cause of crackling audio and stuttering mice. Update Wi-Fi, audio and graphics drivers." : "", "w"));
            var ps = System.Windows.Forms.SystemInformation.PowerStatus;
            // a desktop or VM says "unknown" (255): no battery talk then
            bool noBat = (ps.BatteryChargeStatus & (System.Windows.Forms.BatteryChargeStatus.NoSystemBattery | System.Windows.Forms.BatteryChargeStatus.Unknown)) != 0 || ps.BatteryLifePercent > 1f;
            rows.Add(R(3, "Power", ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online || (noBat && ps.PowerLineStatus != System.Windows.Forms.PowerLineStatus.Offline) ? "plugged in" : noBat ? "on battery" : "on battery " + (ps.BatteryLifePercent * 100).ToString("0", CultureInfo.CurrentCulture) + "%", "", "w"));
            return rows;
        }

        // ---------- graphics card per app (GPU engine counters, sampled twice on press)
        public static List<string[]> Gpu()
        {
            var rows = new List<string[]>();
            try
            {
                // the whole category in one read, twice a second apart - not hundreds of separate counters
                var cat = new PerformanceCounterCategory("GPU Engine");
                Func<string, bool> wanted = n => n.Contains("engtype_3D") || n.Contains("engtype_VideoDecode") || n.Contains("engtype_VideoEncode");
                var d1 = cat.ReadCategory()["Utilization Percentage"]; Thread.Sleep(1000); var d2 = cat.ReadCategory()["Utilization Percentage"];
                var by = new Dictionary<int, double>();
                if (d1 != null && d2 != null)
                    foreach (InstanceData i2 in d2.Values)
                    {
                        if (!wanted(i2.InstanceName) || !d1.Contains(i2.InstanceName)) continue;
                        var mm = Regex.Match(i2.InstanceName, @"pid_(\d+)_"); int pid; if (!mm.Success || !int.TryParse(mm.Groups[1].Value, out pid)) continue;
                        double x; try { x = CounterSample.Calculate(d1[i2.InstanceName].Sample, i2.Sample); } catch (InvalidOperationException) { continue; }
                        double o; by.TryGetValue(pid, out o); by[pid] = o + x;
                    }
                foreach (var kv in by.Where(x => x.Value >= 0.5).OrderByDescending(x => x.Value).Take(8))
                { string name = "pid " + kv.Key; try { using (var p = Process.GetProcessById(kv.Key)) name = p.ProcessName; } catch { } rows.Add(R(kv.Value > 60 ? 1 : 0, name, kv.Value.ToString("0", CultureInfo.CurrentCulture) + "% of the graphics card", "", "w")); }
                if (rows.Count == 0) rows.Add(R(0, "Graphics card", "nothing is using it noticeably right now", "", "w"));
            }
            catch (Exception e) { rows.Add(R(3, "Graphics card", "Windows didn't give GPU counters here (" + Draw.Trunc(e.Message, 50) + ")", "", "u")); }
            return rows;
        }

        // ---------- frozen windows (checked by Brain every 10 s)
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool EnumWindows(EnumProc f, IntPtr l);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool IsHungAppWindow(IntPtr h);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, int cmd);
        static readonly Dictionary<int, DateTime> hungSince = new Dictionary<int, DateTime>();
        public static void HungTick(Monitor m)
        {
            var now = new HashSet<int>();
            EnumWindows(delegate(IntPtr h, IntPtr l) { if (IsWindowVisible(h) && GetWindow(h, 4) == IntPtr.Zero && IsHungAppWindow(h)) { int pid; if (GetWindowThreadProcessId(h, out pid) != 0) now.Add(pid); } return true; }, IntPtr.Zero);
            lock (hungSince)
            {
                foreach (int pid in now) if (!hungSince.ContainsKey(pid)) hungSince[pid] = DateTime.Now;
                foreach (var pid in hungSince.Keys.ToList())
                {
                    if (now.Contains(pid)) continue;
                    var secs = (DateTime.Now - hungSince[pid]).TotalSeconds; hungSince.Remove(pid);
                    if (secs >= 8) { string n = "A program"; try { using (var p = Process.GetProcessById(pid)) n = p.ProcessName; } catch { } m.Events.Add(1, "pc", n + " stopped responding for " + secs.ToString("0", CultureInfo.CurrentCulture) + " s"); }
                }
            }
        }

        // ---------- slow leaks: memory and handles per program, every 5 minutes, last 2 hours (in memory only)
        static readonly List<Tuple<DateTime, Dictionary<string, long[]>>> snaps = new List<Tuple<DateTime, Dictionary<string, long[]>>>();
        public static void LeakTick()
        {
            var d = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
                using (p) try { long[] o; if (!d.TryGetValue(p.ProcessName, out o)) d[p.ProcessName] = o = new long[2]; o[0] += p.PrivateMemorySize64; o[1] += p.HandleCount; } catch { }
            lock (snaps) { snaps.Add(Tuple.Create(DateTime.Now, d)); while (snaps.Count > 25) snaps.RemoveAt(0); }
        }
        public static List<string[]> Leaks()
        {
            var rows = new List<string[]>(); List<Tuple<DateTime, Dictionary<string, long[]>>> s; lock (snaps) s = snaps.ToList();
            if (s.Count < 3) { rows.Add(R(3, "Slow leaks", "still watching - needs about 15 minutes (" + s.Count + " of 3 samples)", "AdeeboMeter checks every program's memory every 5 minutes while it runs.", "u")); return rows; }
            var a = s.First(); var b = s.Last(); double hrs = Math.Max(0.1, (b.Item1 - a.Item1).TotalHours);
            foreach (var kv in b.Item2)
            {
                long[] o; if (!a.Item2.TryGetValue(kv.Key, out o)) continue;
                long dm = kv.Value[0] - o[0], dh = kv.Value[1] - o[1];
                bool steady = s.Select(x => { long[] y; return x.Item2.TryGetValue(kv.Key, out y) ? y[0] : 0; }).Zip(s.Skip(1).Select(x => { long[] y; return x.Item2.TryGetValue(kv.Key, out y) ? y[0] : 0; }), (p, q) => q >= p).Count(t => t) >= (s.Count - 1) * 0.8;
                if ((dm > 300L << 20 && steady) || dh > 3000) rows.Add(R(dm > 1L << 30 ? 2 : 1, kv.Key, (dm > 0 ? "+" + Gb(dm) : "") + (dh > 3000 ? (dm > 0 ? " · " : "") + "+" + dh + " handles" : "") + " in " + hrs.ToString("0.#", CultureInfo.CurrentCulture) + " h", "It keeps growing and never gives it back - restarting it frees the memory. If it happens every day, update the app.", "w"));
            }
            if (rows.Count == 0) rows.Add(R(0, "Slow leaks", "no program has kept growing over the last " + hrs.ToString("0.#", CultureInfo.CurrentCulture) + " h", "", "w"));
            return rows;
        }

        // ---------- crashes and hangs from the Application log
        public static List<string[]> Crashes()
        {
            var rows = new List<string[]>(); string err;
            var evs = EvLog.Recent("Application", "EventID=1000 or EventID=1002", 24 * 7, 40, out err);
            if (err != "") { rows.Add(R(3, "Crashes", err, "", "u")); return rows; }
            var g = evs.Select(e =>
            {
                var app = Regex.Match(e.Text, @"(?:Faulting application name|The program)\s*:?\s*([^\s,]+)", RegexOptions.IgnoreCase);
                var mod = Regex.Match(e.Text, @"Faulting module name\s*:\s*([^\s,]+)", RegexOptions.IgnoreCase);
                // the event's own data (the same in every language): 1000 and 1002 carry the program first, 1000 the module 4th
                var data = Regex.Matches(e.Xml ?? "", @"<Data(?: Name=['""][^'""]*['""])?>([^<]*)</Data>").Cast<Match>().Select(z => System.Net.WebUtility.HtmlDecode(z.Groups[1].Value).Trim()).ToList();
                string appX = data.Count > 0 && data[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? data[0] : "", modX = e.Id == 1000 && data.Count > 3 && data[3] != "" ? data[3] : "";
                return new { e, App = appX != "" ? appX : app.Success ? app.Groups[1].Value : "a program", Mod = modX != "" ? modX : mod.Success ? mod.Groups[1].Value : "", Hang = e.Id == 1002 };
            }).GroupBy(x => x.App + (x.Hang ? " (hang)" : "")).OrderByDescending(x => x.Count()).Take(8);
            foreach (var x in g)
            {
                var mods = x.Where(y => y.Mod != "").GroupBy(y => y.Mod).OrderByDescending(y => y.Count()).Select(y => y.Key).Take(2).ToList();
                rows.Add(R(x.Count() >= 3 ? 2 : 1, x.Key, x.Count() + (x.Count() == 1 ? " time" : " times") + " this week · last " + x.First().e.T.ToString("ddd HH:mm", CultureInfo.CurrentCulture) + (mods.Count > 0 ? " · in " + string.Join(", ", mods) : ""),
                    mods.Any(mm => !mm.StartsWith("ntdll", StringComparison.OrdinalIgnoreCase) && !mm.StartsWith("KERNEL", StringComparison.OrdinalIgnoreCase) && !mm.Equals(x.First().App, StringComparison.OrdinalIgnoreCase)) ? "The same add-in or driver DLL each time is the likely culprit - disable or update it." : "Update the app; if it keeps happening, repair or reinstall it.", "w"));
            }
            if (rows.Count == 0) rows.Add(R(0, "Crashes", "no program crashed or hung in the last 7 days", "", "w"));
            return rows;
        }

        // ---------- background jobs right now
        static readonly string[][] Jobs = {
            new[] { "MsMpEng", "Microsoft Defender scanning" }, new[] { "SearchIndexer", "Windows Search indexing your files" }, new[] { "SearchProtocolHost", "Windows Search reading files" },
            new[] { "TiWorker", "Windows Update installing" }, new[] { "TrustedInstaller", "Windows Update installing" }, new[] { "MoUsoCoreWorker", "Windows Update checking / downloading" },
            new[] { "OneDrive", "OneDrive syncing" }, new[] { "OfficeClickToRun", "Office updating" }, new[] { "CompatTelRunner", "Windows compatibility check" },
            new[] { "MicrosoftEdgeUpdate", "Edge updating" }, new[] { "dllhost", "" }, new[] { "WmiPrvSE", "Windows management queries (often another program asking)" } };
        public static List<string[]> Background()
        {
            var rows = new List<string[]>(); var first = new Dictionary<int, Tuple<string, string, TimeSpan, ulong>>();
            foreach (var j in Jobs.Where(x => x[1] != ""))
                foreach (var p in Process.GetProcessesByName(j[0]))
                    using (p) try { var io = ProcIo.Of(p.Id); first[p.Id] = Tuple.Create(j[0], j[1], p.TotalProcessorTime, io == null ? 0UL : io[0] + io[1]); } catch { }
            int dosvc = 0; try { var s = Svc.List().FirstOrDefault(x => x.Name == "DoSvc" && x.Pid > 0); if (s != null) dosvc = s.Pid; } catch { }
            Thread.Sleep(1500);
            foreach (var kv in first)
            {
                try
                {
                    using (var p = Process.GetProcessById(kv.Key))
                    {
                        double cpu = (p.TotalProcessorTime - kv.Value.Item3).TotalSeconds / 1.5 / Environment.ProcessorCount * 100;
                        var io = ProcIo.Of(kv.Key); double mbs = io == null ? 0 : (io[0] + io[1] - kv.Value.Item4) / 1.5 / 1048576.0;
                        if (cpu < 2 && mbs < 1) continue;
                        rows.Add(R(cpu > 25 || mbs > 30 ? 1 : 0, kv.Value.Item2, cpu.ToString("0", CultureInfo.CurrentCulture) + "% processor · " + mbs.ToString("0.0", CultureInfo.CurrentCulture) + " MB/s disk", "It finishes on its own. If it's every day at a bad time, the schedule can usually be changed in its settings.", "w"));
                    }
                }
                catch { }
            }
            if (dosvc > 0) rows.Add(R(3, "Delivery Optimization", "running (Windows Update's downloader, pid " + dosvc + ")", "It can share updates with other PCs on your network; Settings > Windows Update > Advanced > Delivery Optimization limits its bandwidth.", "w"));
            if (rows.Count == 0) rows.Add(R(0, "Background jobs", "none busy right now", "", "w"));
            return rows;
        }

        // ---------- what changed in the last 7 days
        public static List<string[]> Changes()
        {
            var rows = new List<string[]>(); var since = DateTime.Today.AddDays(-7);
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
                foreach (var path in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
                    try
                    {
                        using (var k2 = root.OpenSubKey(path)) if (k2 != null) foreach (var s in k2.GetSubKeyNames())
                                    using (var a = k2.OpenSubKey(s))
                                    {
                                        if (a == null) continue; string d = Convert.ToString(a.GetValue("InstallDate", ""), CultureInfo.InvariantCulture), n = Convert.ToString(a.GetValue("DisplayName", ""), CultureInfo.InvariantCulture); DateTime t;
                                        if (n != "" && DateTime.TryParseExact(d, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) && t >= since && !rows.Any(r => r[2] == n)) rows.Add(R(3, "Installed " + t.ToString("ddd d MMM", CultureInfo.CurrentCulture), n, "", "w"));
                                    }
                    }
                    catch (Exception e) { Program.Log("uninstall reg: " + e.Message); }
            string err;
            foreach (var e in EvLog.Recent("System", "EventID=19", 24 * 7, 30, out err).Where(x => x.Text.IndexOf("Installation Successful", StringComparison.OrdinalIgnoreCase) >= 0 || x.Xml.Contains("WindowsUpdateClient")))
            { string t = EvLog.Field(e.Xml, "updateTitle"); rows.Add(R(3, "Windows Update " + e.T.ToString("ddd d MMM", CultureInfo.CurrentCulture), t != "" ? t : Draw.Trunc(e.Text, 120), "", "w")); }
            foreach (var e in EvLog.Recent("System", "EventID=20001", 24 * 7, 20, out err))
                rows.Add(R(3, "Driver " + e.T.ToString("ddd d MMM", CultureInfo.CurrentCulture), Draw.Trunc(Regex.Replace(e.Text, @"\s+", " "), 140), "", "w"));
            if (rows.Count == 0) rows.Add(R(0, "What changed", "nothing installed or updated in the last 7 days", "", "w"));
            else rows.Insert(0, R(3, "Last 7 days", rows.Count + " changes - if the PC got slow on one of these days, start there", "", "i"));
            return rows;
        }

        // ---------- battery (powercfg's own report, read and deleted)
        public static List<string[]> Battery()
        {
            var rows = new List<string[]>();
            if (System.Windows.Forms.SystemInformation.PowerStatus.BatteryChargeStatus == System.Windows.Forms.BatteryChargeStatus.NoSystemBattery) { rows.Add(R(3, "Battery", "this PC has no battery", "", "w")); return rows; }
            string f = Path.Combine(Path.GetTempPath(), "adeebometer-battery-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".xml");   // its own name: two runs at once don't delete each other's
            try
            {
                Job.Run(Job.Sys("powercfg.exe"), "/batteryreport /xml /output \"" + f + "\"", 20000);
                string x = File.Exists(f) ? File.ReadAllText(f) : "";
                Func<string, double> num = n => { var mm = Regex.Match(x, "<" + n + ">(\\d+)</" + n + ">"); double v; return mm.Success && double.TryParse(mm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; };
                double des = num("DesignCapacity"), full = num("FullChargeCapacity"), cyc = num("CycleCount");
                if (double.IsNaN(des) || double.IsNaN(full) || des <= 0) rows.Add(R(3, "Battery", "Windows didn't report its capacity here", "", "u"));
                else
                {
                    double h = 100 * full / des;
                    rows.Add(R(h < 60 ? 2 : h < 80 ? 1 : 0, "Battery health", h.ToString("0", CultureInfo.CurrentCulture) + "% of its design capacity (" + (full / 1000).ToString("0.#", CultureInfo.CurrentCulture) + " of " + (des / 1000).ToString("0.#", CultureInfo.CurrentCulture) + " Wh)", h < 80 ? "Worn - it lasts noticeably less than when new. Below about 60% a replacement makes sense." : "", "w"));
                    if (!double.IsNaN(cyc) && cyc > 0) rows.Add(R(3, "Charge cycles", cyc.ToString("0", CultureInfo.CurrentCulture), "", "w"));
                }
            }
            catch (Exception e) { rows.Add(R(3, "Battery", "couldn't read: " + Draw.Trunc(e.Message, 60), "", "u")); }
            finally { try { File.Delete(f); } catch { } }
            return rows;
        }

        // ---------- disk: who reads / writes most over the last hour (sampled every minute by Brain), health, space
        static readonly Dictionary<string, long> lastIo = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        static readonly List<Dictionary<string, long>> ioMin = new List<Dictionary<string, long>>();   // bytes per program per minute, last 60
        public static void IoTick()
        {
            var cur = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses()) using (p) try { var io = ProcIo.Of(p.Id); if (io == null) continue; long o; cur.TryGetValue(p.ProcessName, out o); cur[p.ProcessName] = o + (long)(io[0] + io[1]); } catch { }
            var delta = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            lock (ioMin)
            {
                foreach (var kv in cur) { long o; if (lastIo.TryGetValue(kv.Key, out o) && kv.Value >= o) delta[kv.Key] = kv.Value - o; }
                lastIo.Clear(); foreach (var kv in cur) lastIo[kv.Key] = kv.Value;
                ioMin.Add(delta); while (ioMin.Count > 60) ioMin.RemoveAt(0);
            }
        }
        public static List<string[]> Disk(Monitor m)
        {
            var rows = new List<string[]>();
            var v = Sample(); Thread.Sleep(800); v = Sample();
            double dms = v["diskms"], q = v["queue"];
            if (!double.IsNaN(dms)) rows.Add(R(dms > 50 ? 2 : dms > 15 ? 1 : 0, "Latency now", dms.ToString("0.0", CultureInfo.CurrentCulture) + " ms per request · queue " + (double.IsNaN(q) ? "-" : q.ToString("0", CultureInfo.CurrentCulture)), dms > 15 ? "Busy or slow - under 10 ms is normal." : "", "w"));
            List<Dictionary<string, long>> mins; lock (ioMin) mins = ioMin.ToList();
            var tot = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase); foreach (var d in mins) foreach (var kv in d) { long o; tot.TryGetValue(kv.Key, out o); tot[kv.Key] = o + kv.Value; }
            foreach (var kv in tot.OrderByDescending(x => x.Value).Take(6).Where(x => x.Value > 10L << 20))
                rows.Add(R(kv.Value > 5L << 30 ? 1 : 3, "Busy: " + kv.Key, Gb(kv.Value) + " read + written in the last " + mins.Count + " min", "", "w"));
            if (mins.Count == 0) rows.Add(R(3, "Who uses the disk", "still collecting - one sample a minute", "", "u"));
            try
            {
                var scope = new System.Management.ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                using (var s = new System.Management.ManagementObjectSearcher(scope, new System.Management.ObjectQuery("SELECT FriendlyName, MediaType, HealthStatus, Size FROM MSFT_PhysicalDisk"), new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(10), ReturnImmediately = true, Rewindable = false }))   // a sleeping USB disk or a broken WMI can't hang it forever
                    foreach (System.Management.ManagementObject d in s.Get())
                        using (d)
                        {
                            int hs = Convert.ToInt32(d["HealthStatus"] ?? 5, CultureInfo.InvariantCulture), mt = Convert.ToInt32(d["MediaType"] ?? 0, CultureInfo.InvariantCulture);
                            rows.Add(R(hs == 0 ? 0 : hs == 1 ? 1 : hs == 2 ? 2 : 3, Convert.ToString(d["FriendlyName"], CultureInfo.InvariantCulture), (mt == 4 ? "SSD" : mt == 3 ? "hard disk" : "disk") + " · " + Gb(Convert.ToDouble(d["Size"] ?? 0, CultureInfo.InvariantCulture)) + " · Windows says: " + (hs == 0 ? "healthy" : hs == 1 ? "warning" : hs == 2 ? "unhealthy" : "unknown"), hs == 1 || hs == 2 ? "Back up now - Windows sees the disk failing." : "", "w"));
                        }
            }
            catch (Exception e) { rows.Add(R(3, "Disk health", "Windows keeps this to admins here (" + Draw.Trunc(e.Message, 40) + ")", "", "u")); }
            foreach (var d in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed && x.IsReady))
            {
                double free = d.AvailableFreeSpace, size = d.TotalSize; string trend = SpaceTrend(d.Name);
                rows.Add(R(free / size < 0.1 ? 2 : free / size < 0.2 ? 1 : 0, "Space on " + d.Name.TrimEnd('\\'), Gb(free) + " free of " + Gb(size) + trend, free / size < 0.2 ? "Windows slows down and can't update when the system disk is nearly full. 'Where did my space go?' shows the usual suspects." : "", "w"));
            }
            return rows;
        }
        // one line a day per drive in space.txt (30 days)
        public static void SpaceTick()
        {
            try
            {
                string day = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var rows = Store.Read("space.txt").Where(r => r.Length >= 3 && string.CompareOrdinal(r[0], DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) >= 0 && r[0] != day).ToList();
                foreach (var d in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed && x.IsReady)) rows.Add(new[] { day, d.Name, Store.S(d.AvailableFreeSpace / 1073741824.0) });
                Store.Write("space.txt", rows);
            }
            catch (Exception e) { Program.Log("space: " + e.Message); }
        }
        static string SpaceTrend(string drive)
        {
            var r = Store.Read("space.txt").Where(x => x.Length >= 3 && x[1] == drive).OrderBy(x => x[0]).ToList();
            var week = r.FirstOrDefault(x => string.CompareOrdinal(x[0], DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) >= 0);
            if (week == null || r.Count < 2) return ""; double d = Store.D(r.Last()[2]) - Store.D(week[2]);
            return Math.Abs(d) < 1 ? " · steady this week" : d < 0 ? " · lost " + (-d).ToString("0", CultureInfo.CurrentCulture) + " GB since " + week[0].Substring(5) : " · gained " + d.ToString("0", CultureInfo.CurrentCulture) + " GB since " + week[0].Substring(5);
        }
        // where did my space go - the usual suspects, sized with a time limit (nothing is deleted)
        public static List<string[]> Space(Action<string> progress)
        {
            var found = new List<Tuple<long, string[]>>(); var deadline = DateTime.UtcNow.AddSeconds(20);
            string up = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ra = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var places = new[] {
                new[] { "Downloads", Path.Combine(up, "Downloads"), "Old installers and files you downloaded - safe to sort and delete what you don't need." },
                new[] { "Temp files", Path.GetTempPath(), "Safe to clean: Settings > System > Storage > Temporary files." },
                new[] { "Teams cache (classic)", Path.Combine(ra, "Microsoft", "Teams"), "Rebuilt by Teams; clearing it fixes many Teams glitches (quit Teams first)." },
                new[] { "Teams cache (new)", Path.Combine(la, "Packages"), "" },
                new[] { "Edge cache", Path.Combine(la, "Microsoft", "Edge", "User Data"), "Edge > Settings > Privacy > Clear browsing data." },
                new[] { "Chrome cache", Path.Combine(la, "Google", "Chrome", "User Data"), "Chrome > Settings > Privacy > Clear browsing data." },
                new[] { "OneDrive (copies on this PC)", Environment.GetEnvironmentVariable("OneDrive") ?? "", "Right-click the OneDrive folder > Free up space - files stay in the cloud." },
                new[] { "Windows Update leftovers", Path.Combine(win, "SoftwareDistribution", "Download"), "Settings > System > Storage > Temporary files > Windows Update Cleanup." },
                new[] { "Previous Windows", Path.Combine(Path.GetPathRoot(win) ?? "C:\\", "Windows.old"), "The old Windows from the last big upgrade - Storage > Temporary files removes it." } };
            foreach (var pl in places)
            {
                if (pl[1] == "" || !Directory.Exists(pl[1])) continue;
                progress("Measuring " + pl[0] + "...");
                bool cut; long b = Size(pl[1], deadline, pl[0].StartsWith("Teams cache (new)", StringComparison.Ordinal) ? "MSTeams_" : null, out cut);
                if (b < 50L << 20) continue;
                found.Add(Tuple.Create(b, R(b > 10L << 30 ? 1 : 3, pl[0], Gb(b) + (cut ? "+ (stopped counting - time limit)" : ""), (pl[2] + " AdeeboMeter never deletes anything.").Trim(), "w")));
            }
            var rows = found.OrderByDescending(x => x.Item1).Select(x => x.Item2).ToList();
            if (rows.Count == 0) rows.Add(R(0, "Space", "none of the usual suspects is big", "", "w"));
            return rows;
        }
        static long Size(string dir, DateTime deadline, string onlySub, out bool cut)
        {
            long t = 0; cut = false; var stack = new List<string> { dir };   // a plain list used as a stack
            while (stack.Count > 0)
            {
                if (DateTime.UtcNow > deadline) { cut = true; break; }
                string d = stack[stack.Count - 1]; stack.RemoveAt(stack.Count - 1);
                try
                {
                    var di = new DirectoryInfo(d);
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0 && d != dir) continue;
                    foreach (var f in di.EnumerateFiles()) try { if ((f.Attributes & (FileAttributes)0x400000) == 0) t += f.Length; } catch { }   // skip cloud-only files (RECALL_ON_DATA_ACCESS)
                    foreach (var s in di.EnumerateDirectories()) if (onlySub == null || d != dir || s.Name.StartsWith(onlySub, StringComparison.OrdinalIgnoreCase)) stack.Add(s.FullName);
                }
                catch { }
            }
            return t;
        }
    }
}
