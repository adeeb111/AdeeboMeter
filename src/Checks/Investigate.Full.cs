// AdeeboMeter - part of the source; see src/Program.cs for the overview.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // Full checkup (1.1.5, Adeeb: "full device - not only the internet speed but processes and disk too"). Every layer in
    // one run, on Home: this PC (uptime, pending reboot, every disk, memory, power, the busiest apps, then "My PC is slow"),
    // the network (the whole "What's wrong?" walk, which doesn't stop at a busy PC here), and - only if ticked - a speed
    // test. One report: the biggest problem first, then the next one, each fact with where it came from.
    static partial class Investigate
    {
        [ThreadStatic] static bool fullMode;
        public static volatile bool CheckupSpeed = true;   // the Home tick; the caller turns it off on a capped or mobile connection

        static void Full(Monitor m, GuideRun r, Action changed)
        {
            r.Title = "Full checkup";
            var found = new List<KeyValuePair<int, string>>();   // level, finding - ranked at the end
            Action<string> step = s => { r.Status = s; changed(); };

            // 1. this PC: the health checks
            step("Checking this PC...");
            foreach (var h in HealthCheck.Run(m))
            {
                int lv; int.TryParse(h[0], NumberStyles.Integer, CultureInfo.CurrentCulture, out lv);
                r.Obs("w", lv, "1. This PC: " + h[1], h[2]);
                if (lv >= 1 && h.Length > 3 && h[3] != "") found.Add(new KeyValuePair<int, string>(lv, h[1] + ": " + h[2] + " - " + h[3]));
            }
            List<AppStat> top; lock (m.L) top = m.Apps.Where(a => a.Name != "AdeeboMeter" && a.Name != "Idle").OrderByDescending(a => a.Cpu).Take(3).ToList();
            foreach (var a in top) r.Obs("w", a.Cpu > 60 ? 1 : 0, "1. This PC: busy app", a.Name + " - " + a.Cpu.ToString("0", CultureInfo.CurrentCulture) + "% processor, " + (a.Mem / 1048576) + " MB");
            // 2. what slows this PC (processes, memory pressure, disk, heat, drivers, frozen apps)
            step("Looking at what slows this PC...");
            var pc = new GuideRun(); Pc(m, pc, delegate { });
            foreach (var o in pc.ObservedCopy()) r.Obs(o[0], int.Parse(o[1], CultureInfo.InvariantCulture), "2. " + o[2], o[3]);
            if (pc.Lv >= 1 && pc.Finding != "") found.Add(new KeyValuePair<int, string>(pc.Lv, pc.Finding));
            // 3. the network, every layer
            step("Checking the network, layer by layer...");
            var net = new GuideRun { Input = r.Input }; fullMode = true;
            try { Whats(m, net, changed); } finally { fullMode = false; }
            foreach (var o in net.ObservedCopy()) if (!o[2].StartsWith("1. This PC", StringComparison.Ordinal)) r.Obs(o[0], int.Parse(o[1], CultureInfo.InvariantCulture), "3. " + o[2], o[3]);
            if (net.Lv >= 1 && net.Finding != "") found.Add(new KeyValuePair<int, string>(net.Lv, net.Finding));
            // 4. the speed test - only when ticked (it moves 100-300 MB)
            if (CheckupSpeed)
            {
                step("Speed test (download, then upload)...");
                var run = new Toolkit.Run { Deadline = DateTime.Now.AddSeconds(60) };
                try
                {
                    foreach (var row in SpeedTest.Run("auto", run))
                    {
                        int lv; if (row.Length < 3 || !int.TryParse(row[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv) || lv < 0 || lv > 3 || row[1] == "") continue;
                        r.Obs(row.Length > 4 && row[4] != "" ? row[4] : "m", lv, "4. Speed: " + row[1], row[2]);
                        if (lv >= 1) found.Add(new KeyValuePair<int, string>(lv, (row[1].IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0 ? "" : "Speed test - ") + row[1] + ": " + row[2]));
                    }
                }
                catch (Exception e) { r.NotEst.Add("Speed test: " + e.Message); }
            }
            else r.NotEst.Add("Internet speed - the speed test wasn't ticked (it uses 100-300 MB).");
            // 5. the parts between the internet and your files: a disk or a link slower than the plan caps every download
            {
                var spd = SpeedTest.Last; double wr = HwTests.LastNum("disk", 1);
                if (spd != null && !double.IsNaN(spd.Down) && (DateTime.Now - spd.T).TotalHours < 24 && !double.IsNaN(wr))
                {
                    bool cap = wr * 8 < spd.Down; r.Obs("m", cap ? 2 : 0, "5. Disk vs internet", HwSpecs.Fmt(wr) + " MB/s disk write = " + HwSpecs.Fmt(wr * 8) + " Mbps · internet " + HwSpecs.Fmt(spd.Down) + " Mbps");
                    if (cap) found.Add(new KeyValuePair<int, string>(2, "Your disk writes slower than your internet delivers (" + HwSpecs.Fmt(wr * 8) + " vs " + HwSpecs.Fmt(spd.Down) + " Mbps) - big downloads are limited by the disk, not your ISP."));
                }
                var nl = HwTools.NetLinkIssue(m, HwSpecs.Get()); if (nl != null) { r.Obs("w", int.Parse(nl[0], CultureInfo.InvariantCulture), "5. " + nl[1], nl[2]); found.Add(new KeyValuePair<int, string>(int.Parse(nl[0], CultureInfo.InvariantCulture), nl[1] + ": " + nl[2] + " - " + nl[3])); }
            }
            // the report: biggest problem first
            var ranked = found.OrderByDescending(f => f.Key).Select(f => f.Value).Distinct().ToList();
            int worst = found.Count == 0 ? 0 : found.Max(f => f.Key);
            r.Conclude(worst, ranked.Count == 0 ? "All clear: this PC, its disks, the link, your router, the internet and names all look healthy."
                : ranked[0] + (ranked.Count > 1 ? "  Also: " + ranked[1] + (ranked.Count > 2 ? "  (+" + (ranked.Count - 2) + " more below)" : "") : ""));
            foreach (var n in pc.Next.Concat(net.Next).Distinct()) r.Next.Add(n);
            if (pc.GoTab != "" && pc.Lv >= net.Lv) { r.GoTab = pc.GoTab; r.GoLabel = pc.GoLabel; } else if (net.GoTab != "") { r.GoTab = net.GoTab; r.GoLabel = net.GoLabel; }
        }
    }
}
