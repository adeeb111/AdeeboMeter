// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Hardware nerds: every part against its own ceiling
    // The rule everywhere: read the specs, work out what the part should do, measure what it does, and judge the gap.
    static class HwTools
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string F(double v) { return HwSpecs.Fmt(v); }
        static readonly string[] DashSep = { " - " };
        static string Pct(double a, double b) { return double.IsNaN(a) || double.IsNaN(b) || b <= 0 ? "?" : (100 * a / b).ToString("0", CultureInfo.CurrentCulture) + "%"; }
        internal static string Gb(double gb) { return gb >= 10 ? gb.ToString("0", CultureInfo.CurrentCulture) + " GB" : gb.ToString("0.#", CultureInfo.CurrentCulture) + " GB"; }
        static string Before(string what, double now, int i)
        {
            var l = HwTests.Last(what); double prev; if (l == null || l.Length < 5 || !double.TryParse(l[4], NumberStyles.Float, CultureInfo.InvariantCulture, out prev) || prev <= 0 || double.IsNaN(now)) return "";
            double ch = now / prev; return ch < 0.75 ? " · " + (100 - ch * 100).ToString("0", CultureInfo.CurrentCulture) + "% slower than the last run" : ch > 1.33 ? " · faster than the last run" : " · same as the last run";
        }

        // ---------- how each part works, told with this PC's own numbers
        internal static string Explain(string part, HwSpecs.Specs s)
        {
            double ghz = s.BaseMhz / 1000.0; string g = ghz > 0 ? ghz.ToString("0.0#", CultureInfo.CurrentCulture) : "3"; var sd = HwSpecs.SystemDisk(s); var ig = s.Gpus.FirstOrDefault(x => x.Integrated); var dg = s.Gpus.FirstOrDefault(x => !x.Integrated && !x.Software);
            switch (part)
            {
                case "cores": return s.Cores + " cores = " + s.Cores + " workers that each run their own job at the same moment. " + (s.Threads > s.Cores ? s.Threads + " threads: each core juggles two streams of work, filling the gaps while one waits for memory. " : "") + "One program often keeps only one or a few workers busy - so a game cares about the speed of one core, while exporting a video or zipping files uses all " + s.Cores + ".";
                case "clock": return g + " GHz = " + g + " billion ticks a second; on each tick a core finishes a few small steps (add, compare, move a number). The clock isn't fixed: it drops when idle to save power, boosts above the rated speed when busy, and is held back when hot or on battery saver - that's what 'speed under load' measures.";
                case "ram": return HwTools.Gb(s.RamGb) + " of memory is the desk: open programs, tabs and files lie here while you use them, because it answers about 1,000 times faster than even an SSD. When the desk is full, Windows puts things back in the drawer (the disk) and fetches them again - that's the slowness called paging. Speed = transfers per second × 8 bytes × channels; " + s.Channels + " channel" + (s.Channels > 1 ? "s" : "") + " here - two channels are two lanes to the desk.";
                case "disk": return "The disk is the filing cabinet: everything kept when the power is off - Windows, programs, your files. Two numbers matter: MB/s for big files (copies, installs), and the wait for one small read for how quick everything feels. " + (sd != null && sd.Media == "HDD" ? "This one spins: each small read waits for the platter to turn under the head - half a turn on average, 30,000 ÷ rpm ms." : "An SSD has no moving parts: a small read takes about 0.1 ms, a spinning disk 5-15 ms.");
                case "link": return "A PCIe link is a road made of lanes: x4 = 4 lanes, x16 = 16. Each generation doubles a lane's speed (PCIe 3.0 ≈ 1 GB/s per lane, 4.0 ≈ 2 GB/s). A fast drive on a narrow road can't use its speed.";
                case "gpu": return (dg != null ? dg.Name + " is a separate card with " + HwTools.Gb(dg.DedicatedMb / 1024) + " of its own fast memory. " : "") + "A graphics chip has thousands of small cores doing the same maths on many pixels at once - drawing the screen 60+ times a second. " + (ig != null ? "The built-in graphics (" + ig.Name + ") have no memory of their own: they borrow the PC's memory, so they're only as fast as the memory is. " : "") + "When a card's own memory overflows, it borrows the PC's over the much slower link - that's stutter.";
                case "net": return "The network link is the front door: data can't arrive faster than the door is wide. Mbps = megabits a second; divide by 8 for megabytes (1,000 Mbps ≈ 125 MB/s). Wi-Fi delivers about 60% of its link rate; a cable delivers almost all of it.";
                case "battery": return "Wh (watt-hours) is how much energy the battery holds. Health = today's full charge ÷ the full charge when new. Every full charge and discharge is a cycle; most laptop batteries are made for 300-1,000 cycles before they drop to 80%.";
                case "cache": return "Cheaper SSDs write first into a small fast area (the cache), then move the data to the slower main chips. Short writes all land in the cache and look fast; a long copy fills it and the real speed shows.";
                default: return "";
            }
        }

        // ---------- disk: speed against the ceiling of its own kind and link
        internal static List<string[]> DiskSpeed(string letter, bool full, Toolkit.Run run)
        {
            var rows = new List<string[]>(); var s = HwSpecs.Get(); var d = HwSpecs.DiskFor(s, letter);
            string why = "kind unknown"; double ceil = d == null ? 550 : HwSpecs.DiskCeiling(d, out why);
            if (d != null)
            {
                rows.Add(R(3, "The disk", d.Name + " · " + (d.Media == "" ? "kind not reported" : d.Media == "HDD" ? "hard disk (spinning)" : d.Media) + (d.Bus != "" ? " on " + d.Bus : "") + " · " + F(d.Gb) + " GB" + (d.Letters != "" ? " · " + d.Letters : ""), "", "w"));
                if (d.MaxWidth > 0) rows.Add(R(d.LinkWidth < d.MaxWidth || d.LinkGen < d.MaxGen ? 1 : 0, "Its link", "PCIe " + d.LinkGen + ".0 x" + d.LinkWidth + " now · up to PCIe " + d.MaxGen + ".0 x" + d.MaxWidth, d.LinkWidth < d.MaxWidth || d.LinkGen < d.MaxGen ? "The drive sits on a slower link than it can use (the slot, or a power-saving setting) - its ceiling drops with it." : "", "w"));
            }
            rows.Add(R(3, "Ceiling", F(ceil) + " MB/s - " + why, "", "i"));
            var r = HwTests.Disk(letter, full, run);
            if (r.Error != "") { rows.Insert(0, R(3, "Verdict", "The speed test didn't run: " + r.Error, "", "u")); return rows; }
            if (!double.IsNaN(r.BusyBefore)) rows.Add(R(r.BusyBefore > 40 ? 1 : 0, "Busy before the test", F(r.BusyBefore) + "%" + (r.Culprit != "" ? " - mostly " + r.Culprit : ""), r.BusyBefore > 40 ? "Something else was using the disk, so the numbers below are lower than the disk can do. Run it again when it's quiet." : "", "m"));
            double share = r.WriteMBs / ceil, rshare = r.ReadMBs / ceil;
            rows.Add(R(rshare < 0.25 ? 2 : rshare < 0.5 ? 1 : 0, "Read", F(r.ReadMBs) + " MB/s · " + Pct(r.ReadMBs, ceil) + " of its ceiling" + Before("disk", r.ReadMBs, 2), "", "m"));
            rows.Add(R(share < 0.2 ? 2 : share < 0.4 ? 1 : 0, "Write", F(r.WriteMBs) + " MB/s · " + Pct(r.WriteMBs, ceil) + " of its ceiling (" + Gb(r.Gb) + " written)" + Before("disk", r.WriteMBs, 1), "", "m"));
            if (!double.IsNaN(r.CliffGb)) rows.Add(R(r.CliffMBs < 150 ? 2 : 1, "Write cache runs out", "after about " + Gb(Math.Max(0.1, r.CliffGb)) + " the speed drops to " + F(r.CliffMBs) + " MB/s", "This SSD writes fast into a small cache, then slows to its real speed. Copying big files or installing large games feels slow once the cache is full - it's how cheaper SSDs are built, not a fault.", "m"));
            else if (full && r.Gb >= 2) rows.Add(R(0, "Write cache", "no drop in " + Gb(r.Gb) + " - steady all the way", "", "m"));
            // the spin gives itself away: a small random read waits half a turn on average, 30,000 / rpm ms
            string kind = ""; if (!double.IsNaN(r.LatMs))
            {
                if (r.LatMs > 2.5) { double rpm = 30000 / Math.Max(1, r.LatMs - 1); kind = "behaves like a spinning disk (≈ " + (rpm > 6300 ? "7,200" : "5,400") + " rpm: 30,000 ÷ rpm = the half-turn wait)"; }
                else if (r.LatMs > 0.6) kind = "behaves like eMMC, an SD card or a busy SSD";
                else kind = "SSD-fast";
                rows.Add(R(r.LatMs > 2.5 ? (d != null && d.Media == "SSD" ? 2 : 1) : 0, "One small read", F(r.LatMs) + " ms · " + F(r.Iops) + " per second - " + kind, r.LatMs > 2.5 ? "Every program start and file open waits for this. An SSD answers in about 0.1 ms - the single biggest upgrade an older PC can get." : "", "m"));
            }
            if (r.WriteSeries.Count >= 2) rows.Add(R(3, "Write speed each ½ s", "MB/s " + string.Join(" ", r.WriteSeries.Take(60).Select(z => Math.Round(z).ToString("0", CultureInfo.InvariantCulture)).ToArray()), "", "m"));
            int lv = rows.Max(x => int.Parse(x[0], CultureInfo.InvariantCulture) == 3 ? 0 : int.Parse(x[0], CultureInfo.InvariantCulture));
            string v = lv == 0 ? "This disk does what its kind should: " + F(r.ReadMBs) + " MB/s read, " + F(r.WriteMBs) + " MB/s write (" + Pct(Math.Min(r.ReadMBs, r.WriteMBs), ceil) + " of its ceiling or better)."
                : !double.IsNaN(r.BusyBefore) && r.BusyBefore > 40 ? "The disk was already busy (" + F(r.BusyBefore) + "%" + (r.Culprit != "" ? ", " + r.Culprit : "") + "), so it measured low - that program is what makes the PC feel slow right now."
                : r.LatMs > 2.5 && (d == null || d.Media != "SSD") ? "A spinning disk: fine for storage, slow for Windows. Moving Windows to an SSD makes this PC feel new."
                : !double.IsNaN(r.CliffGb) ? "Fast at first, then slow: the SSD's write cache fills after about " + Gb(Math.Max(0.1, r.CliffGb)) + ". Normal for a budget SSD; big copies will crawl."
                : "This disk is slower than its kind should be (" + Pct(Math.Min(r.ReadMBs, r.WriteMBs), ceil) + " of its ceiling). A worn or overheating SSD, a nearly full disk, or a slow link can all do this.";
            rows.Insert(0, R(lv, "Verdict", v, "", "m"));
            rows.Add(R(3, "How a disk works", Explain("disk", s), "", "i")); if (d != null && d.MaxWidth > 0) rows.Add(R(3, "How the link works", Explain("link", s), "", "i")); if (!double.IsNaN(r.CliffGb)) rows.Add(R(3, "How the cache works", Explain("cache", s), "", "i"));
            return rows;
        }

        // ---------- memory: sticks, channels, the integrated GPU's share, measured against the ceiling
        internal static List<string[]> Ram(Toolkit.Run run)
        {
            var rows = new List<string[]>(); var s = HwSpecs.Get(); string type = HwSpecs.RamTypeOf(s);
            foreach (var r in s.Rams.Take(8)) rows.Add(R(r.Mts > 0 && r.RatedMts > r.Mts + 100 ? 1 : 3, r.Slot == "" ? "Memory stick" : r.Slot, Gb(r.Gb) + (type != "" ? " " + type : "") + " · " + (r.Mts > 0 ? r.Mts + " MT/s" : "speed not reported") + (r.RatedMts > r.Mts + 100 ? " (rated " + r.RatedMts + ")" : ""), r.RatedMts > r.Mts + 100 ? "Running below its rated speed - turn on XMP / EXPO in the BIOS, or the other stick is slower." : "", "w"));
            rows.Add(R(s.RamGb < 8 ? 1 : 0, "Total", Gb(s.RamGb) + (s.RamGb < 8 ? " - tight for Windows 11 and a browser" : ""), "", "w"));
            rows.Add(R(s.Channels < 2 ? 1 : 0, "Channels", s.Channels + (s.Channels < 2 ? " - single channel: half the memory speed" : " - dual channel"), s.Channels < 2 && !s.Soldered ? "Adding a second matching stick doubles memory speed - the integrated graphics gain the most." : "", "i"));
            double ceil = HwSpecs.RamCeilingGBs(s);
            if (!double.IsNaN(ceil)) rows.Add(R(3, "Ceiling", F(ceil) + " GB/s = " + s.Rams.Max(x => x.Mts) + " MT/s × 8 bytes × " + s.Channels + " channel" + (s.Channels > 1 ? "s" : ""), "", "i"));
            if (run != null) run.Status = "Measuring memory speed (1.5 s)...";
            double got = HwTests.MemoryGBs(run);
            rows.Add(R(!double.IsNaN(ceil) && got < ceil * 0.25 ? 1 : 0, "Measured", F(got) + " GB/s copying" + (double.IsNaN(ceil) ? "" : " · " + Pct(got, ceil) + " of the ceiling (50-70% is normal for a copy)") + Before("ram", got, 1), "", "m"));
            var ig = s.Gpus.FirstOrDefault(g => g.Integrated);
            if (ig != null) rows.Add(R(s.Channels < 2 ? 1 : 3, "Built-in graphics borrows", "up to " + Gb(ig.SharedMb / 1024) + " of this memory (" + ig.Name + ")", s.Channels < 2 ? "Built-in graphics have no memory of their own: on one channel they get half the speed - games and video show it first." : "", "w"));
            int lv = rows.Max(x => x[0] == "3" ? 0 : int.Parse(x[0], CultureInfo.InvariantCulture));
            rows.Insert(0, R(lv, "Verdict", s.Channels < 2 ? "Single-channel memory: " + Gb(s.RamGb) + " on one channel runs at half the speed it could." + (ig != null ? " The built-in graphics share it, so they lose the most." : "") + (s.Soldered ? " (It's soldered - this can't be changed.)" : " A second matching stick fixes it.")
                : s.RamGb < 8 ? "Memory is fast enough but small (" + Gb(s.RamGb) + "): with a browser open Windows reads programs back from the disk." : "Memory is set up right: " + Gb(s.RamGb) + " on " + s.Channels + " channels, " + F(got) + " GB/s measured.", "", "m"));
            rows.Add(R(3, "How memory works", Explain("ram", s), "", "i"));
            return rows;
        }

        // ---------- graphics: dedicated vs borrowed memory, the link, which app runs on which card
        internal static List<string[]> Gpu()
        {
            var rows = new List<string[]>(); var s = HwSpecs.Get(); var real = s.Gpus.Where(g => !g.Software).ToList();
            if (real.Count == 0) { rows.Add(R(3, "Verdict", "Windows didn't list a graphics card here (a remote session or a virtual machine shows none).", "", "u")); return rows; }
            var use = GpuUse();   // luid -> (3D %, dedicated MB used, shared MB used, [app -> %])
            foreach (var g in real)
            {
                Tuple<double, double, double, Dictionary<string, double>> u; use.TryGetValue(g.Luid, out u);
                rows.Add(R(-1, (g.Integrated ? "BUILT-IN: " : "GRAPHICS CARD: ") + g.Name, "", "", ""));
                rows.Add(R(3, "Memory", g.Integrated ? "none of its own · borrows up to " + Gb(g.SharedMb / 1024) + " of the PC's memory" : Gb(g.DedicatedMb / 1024) + " of its own" + (g.SharedMb > 0 ? " · can borrow " + Gb(g.SharedMb / 1024) + " more" : ""), "", "w"));
                if (u != null) rows.Add(R(u.Item2 > g.DedicatedMb * 0.9 && !g.Integrated ? 1 : 0, "In use now", F(u.Item1) + "% busy · " + (g.Integrated ? F(u.Item3) + " MB borrowed" : F(u.Item2) + " MB of its memory" + (u.Item3 > 300 ? " + " + F(u.Item3) + " MB borrowed (overflow - stutter)" : "")), "", "m"));
                if (g.MaxWidth > 0)
                {
                    bool low = g.LinkWidth < g.MaxWidth || g.LinkGen < g.MaxGen; bool busy = u != null && u.Item1 > 30;
                    double now = HwSpecs.PcieMBs(g.LinkGen, g.LinkWidth), max = HwSpecs.PcieMBs(g.MaxGen, g.MaxWidth);
                    rows.Add(R(low && busy ? 1 : 0, "Link", "PCIe " + g.LinkGen + ".0 x" + g.LinkWidth + " (" + F(now / 1000) + " GB/s) now · up to PCIe " + g.MaxGen + ".0 x" + g.MaxWidth + " (" + F(max / 1000) + " GB/s)",
                        low ? (busy ? "Busy and still on a narrow link: check the card sits in the top full-length slot, or the riser cable." : "Idle cards lower the link to save power - run this again with a game open to judge it.") : "", "w"));
                }
                if (u != null) foreach (var a in u.Item4.OrderByDescending(x => x.Value).Take(4)) rows.Add(R(0, "  " + a.Key, F(a.Value) + "% of this card", "", "m"));
            }
            // a gaming laptop's classic: the game on the weak built-in GPU
            var dg = real.FirstOrDefault(g => !g.Integrated); var igp = real.FirstOrDefault(g => g.Integrated); string verdict; int lv = 0;
            Tuple<double, double, double, Dictionary<string, double>> iu = null; if (igp != null) use.TryGetValue(igp.Luid, out iu);
            var heavy = iu == null ? null : iu.Item4.Where(x => x.Value > 25).OrderByDescending(x => x.Value).Select(x => x.Key).FirstOrDefault();
            if (dg != null && igp != null && heavy != null) { lv = 1; verdict = heavy + " runs on the built-in graphics (" + igp.Name + ") while the stronger " + dg.Name + " waits. Settings > System > Display > Graphics > " + heavy + " > High performance."; }
            else if (dg == null) verdict = "Built-in graphics only (" + igp.Name + "): fine for work and video; light games at low settings. " + (s.Channels < 2 ? "On single-channel memory it runs at half speed - a second stick helps most." : "");
            else verdict = dg.Name + " with " + Gb(dg.DedicatedMb / 1024) + " of its own memory" + (igp != null ? ", plus built-in graphics for light work (laptops switch between them)" : "") + ".";
            rows.Insert(0, R(lv, "Verdict", verdict.Trim(), "", "m"));
            rows.Add(R(3, "How graphics work", Explain("gpu", s), "", "i")); if (real.Any(g => g.MaxWidth > 0)) rows.Add(R(3, "How the link works", Explain("link", s), "", "i"));
            return rows;
        }
        // GPU counters per adapter: "luid_0x00000000_0x0000D1A3_phys_0..." - one read of each category
        static Dictionary<long, Tuple<double, double, double, Dictionary<string, double>>> GpuUse()
        {
            var r = new Dictionary<long, Tuple<double, double, double, Dictionary<string, double>>>();
            try
            {
                Func<string, long> luid = n => { var m = Regex.Match(n, @"luid_0x([0-9a-fA-F]{8})_0x([0-9a-fA-F]{8})"); return m.Success ? (long.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) << 32) | long.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) : -1; };
                var busy = new Dictionary<long, double>(); var apps = new Dictionary<long, Dictionary<string, double>>(); var names = new Dictionary<int, string>();
                var cat = new PerformanceCounterCategory("GPU Engine"); var d1 = cat.ReadCategory()["Utilization Percentage"]; Thread.Sleep(800); var d2 = cat.ReadCategory()["Utilization Percentage"];
                if (d1 != null && d2 != null)
                    foreach (InstanceData i2 in d2.Values)
                    {
                        if (!i2.InstanceName.Contains("engtype_3D") || !d1.Contains(i2.InstanceName)) continue;
                        double x; try { x = CounterSample.Calculate(d1[i2.InstanceName].Sample, i2.Sample); } catch (InvalidOperationException) { continue; }
                        long l = luid(i2.InstanceName); if (l < 0) continue; double o; busy.TryGetValue(l, out o); busy[l] = Math.Min(100, o + x);
                        var pm = Regex.Match(i2.InstanceName, @"pid_(\d+)_"); int pid; if (x < 1 || !pm.Success || !int.TryParse(pm.Groups[1].Value, out pid)) continue;
                        string nm; if (!names.TryGetValue(pid, out nm)) { nm = "pid " + pid; try { using (var p = Process.GetProcessById(pid)) nm = p.ProcessName; } catch (ArgumentException) { } catch (InvalidOperationException) { } names[pid] = nm; }
                        Dictionary<string, double> a; if (!apps.TryGetValue(l, out a)) apps[l] = a = new Dictionary<string, double>(); double ao; a.TryGetValue(nm, out ao); a[nm] = ao + x;
                    }
                var ded = new Dictionary<long, double>(); var sh = new Dictionary<long, double>();
                try
                {
                    var mem = new PerformanceCounterCategory("GPU Adapter Memory").ReadCategory();
                    foreach (var kv in new[] { Tuple.Create("Dedicated Usage", ded), Tuple.Create("Shared Usage", sh) })
                    { var c = mem[kv.Item1]; if (c != null) foreach (InstanceData i in c.Values) { long l = luid(i.InstanceName); if (l >= 0) { double o; kv.Item2.TryGetValue(l, out o); kv.Item2[l] = o + i.RawValue / 1048576.0; } } }
                }
                catch (InvalidOperationException) { }
                foreach (var l in busy.Keys.Concat(ded.Keys).Distinct())
                {
                    double b, de, s2; busy.TryGetValue(l, out b); ded.TryGetValue(l, out de); sh.TryGetValue(l, out s2); Dictionary<string, double> a; apps.TryGetValue(l, out a);
                    r[l] = Tuple.Create(b, de, s2, a ?? new Dictionary<string, double>());
                }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("gpu use: " + e.Message); }
            return r;
        }

        // ---------- the processor under load: how fast it really runs, and what holds it back
        internal static List<string[]> Cpu(Toolkit.Run run)
        {
            var rows = new List<string[]>(); var s = HwSpecs.Get();
            rows.Add(R(3, "Processor", (s.Cpu == "" ? "not reported" : s.Cpu) + " · " + s.Cores + " cores / " + s.Threads + " threads · rated " + (s.BaseMhz > 0 ? (s.BaseMhz / 1000.0).ToString("0.0#", CultureInfo.CurrentCulture) + " GHz" : "?"), "", "w"));
            if (run != null) run.Status = "Running every core for 3 seconds...";
            var r = HwTests.Cpu(run);
            Func<double, string> ghz = p => s.BaseMhz <= 0 || double.IsNaN(p) ? "" : " ≈ " + (s.BaseMhz * p / 100000).ToString("0.0#", CultureInfo.CurrentCulture) + " GHz";
            bool onBat = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline, saver = HwSpecs.BatterySaver();
            if (!double.IsNaN(r.PerfAvg))
            {
                rows.Add(R(r.PerfAvg < 60 ? 2 : r.PerfAvg < 85 ? 1 : 0, "Speed under full load", F(r.PerfAvg) + "% of its rated speed" + ghz(r.PerfAvg), "", "m"));
                if (r.PerfFirst - r.PerfLast > 20) rows.Add(R(1, "Slowed while running", F(r.PerfFirst) + "% → " + F(r.PerfLast) + "% within 3 seconds", "It slowed down as it warmed up - heat. Clean the vents and fan, don't run it on a bed or lap, and check the fan spins up.", "m"));
            }
            rows.Add(R(3, "Work done", F(r.Score) + " million steps/s on all threads · " + F(r.OneScore) + " on one" + Before("cpu", r.Score, 1), "", "m"));
            rows.Add(R(onBat ? 1 : 0, "Power", (onBat ? "on battery" : "plugged in") + (saver ? " · battery saver ON" : "") + (s.Power != "" ? " · plan: " + s.Power : ""), onBat || saver ? "Laptops hold the processor back on battery and in battery saver - plug in for full speed." : "", "w"));
            string v; int lv;
            if (double.IsNaN(r.PerfAvg)) { lv = 3; v = "Windows didn't report the processor's speed here, so only the work done is shown (compare it with the next run)."; }
            else if (r.PerfAvg >= 85) { lv = 0; v = "The processor runs at full speed under load (" + F(r.PerfAvg) + "%" + ghz(r.PerfAvg) + ")."; }
            else { lv = r.PerfAvg < 60 ? 2 : 1; v = "Held back to " + F(r.PerfAvg) + "% of its speed under load" + ghz(r.PerfAvg) + " - " + (saver ? "battery saver is on." : onBat ? "it's on battery." : r.PerfFirst - r.PerfLast > 20 ? "it slowed as it heated up: heat." : Regex.IsMatch(s.Power, "saver|quiet|eco", RegexOptions.IgnoreCase) ? "the power plan (" + s.Power + ") limits it." : "heat or a power limit (the maker's app may have a quiet / eco mode on)."); }
            rows.Insert(0, R(lv, "Verdict", v, "", "m"));
            rows.Add(R(3, "How cores work", Explain("cores", s), "", "i")); rows.Add(R(3, "How the clock works", Explain("clock", s), "", "i"));
            return rows;
        }

        // ---------- is it the internet, or what's between the internet and the files?
        internal static List<string[]> NetCap(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>(); var s = HwSpecs.Get();
            var spd = SpeedTest.Last; double plan = spd != null && !double.IsNaN(spd.Down) ? spd.Down : double.NaN; int rate; lock (m.L) rate = m.RxRate;
            rows.Add(R(3, "Internet (last speed test)", double.IsNaN(plan) ? "no speed test yet this session - Toolbox > Speed test" : F(plan) + " Mbps = " + F(plan / 8) + " MB/s", "", double.IsNaN(plan) ? "u" : "m"));
            if (!double.IsNaN(s.NicMbps)) rows.Add(R(!double.IsNaN(plan) && s.NicMbps < plan * 1.1 ? 2 : s.NicMbps <= 100 && s.NicKind == "Cable" ? 1 : 0, "This PC's link", s.NicKind + " · " + F(s.NicMbps) + " Mbps" + (s.Nic != "" ? " (" + Cutl(s.Nic, 40) + ")" : ""),
                s.NicKind == "Cable" && s.NicMbps <= 100 ? "A cable link at 100 Mbps: a damaged cable (one of 4 pairs broken), an old cable, or a 100 Mbps port. Most PCs and routers do 1,000." : !double.IsNaN(plan) && s.NicMbps < plan * 1.1 ? "Your link can't carry your plan - it's the cap, not your ISP." : "", "w"));
            if (rate > 0) rows.Add(R(!double.IsNaN(plan) && rate < plan * 1.4 ? 1 : 0, "Wi-Fi link rate", rate + " Mbps (real throughput is about 60% of this)", !double.IsNaN(plan) && rate < plan * 1.4 ? "Wi-Fi delivers about 60% of its link rate, so this Wi-Fi can't reach your plan - closer to the router, 5 GHz, or a cable." : "", "w"));
            if (Regex.IsMatch(s.Nic, @"USB 2|USB2", RegexOptions.IgnoreCase)) rows.Add(R(1, "USB adapter", "a USB 2 network adapter tops out near 300 Mbps", "Plug it into a blue USB 3 port, or use a USB 3 adapter.", "w"));
            // the disk can be the slowest part of a download: 1 Gbps internet = 125 MB/s
            double wr = HwTests.LastNum("disk", 1);
            if (double.IsNaN(wr)) { if (run != null) run.Status = "Quick disk write test (256 MB)..."; var dr = HwTests.Disk("", false, run); wr = dr.WriteMBs; }
            if (!double.IsNaN(wr)) rows.Add(R(!double.IsNaN(plan) && wr < plan / 8 ? 2 : 0, "Disk write speed", F(wr) + " MB/s = " + F(wr * 8) + " Mbps", !double.IsNaN(plan) && wr < plan / 8 ? "Downloads can't land faster than the disk writes: your disk, not your internet, is the limit." : "", "m"));
            string v; int lv = 0;
            double link = Math.Min(double.IsNaN(s.NicMbps) ? double.MaxValue : s.NicMbps, rate > 0 ? rate * 0.6 : double.MaxValue);
            if (double.IsNaN(plan)) { lv = 3; v = "Run a speed test first (Toolbox > Speed test); then this compares your plan with every part on the way to your files."; }
            else if (!double.IsNaN(wr) && wr * 8 < plan && wr * 8 < link) { lv = 2; v = "Your internet does " + F(plan) + " Mbps, but your disk can only take " + F(wr) + " MB/s (" + F(wr * 8) + " Mbps). Big downloads are limited by the disk, not your ISP."; }
            else if (link < plan * 1.1) { lv = 2; v = "Your " + (rate > 0 ? "Wi-Fi" : "network link") + " (" + F(link) + " Mbps usable) is narrower than your internet (" + F(plan) + " Mbps) - that's the cap you see, not your provider."; }
            else v = "Nothing on this PC caps your internet: link " + (double.IsNaN(s.NicMbps) ? "?" : F(s.NicMbps)) + " Mbps, disk " + F(wr * 8) + " Mbps, internet " + F(plan) + " Mbps.";
            rows.Insert(0, R(lv, "Verdict", v, "", "m"));
            rows.Add(R(3, "How the link works", Explain("net", s), "", "i"));
            return rows;
        }
        static string Cutl(string s, int n) { return s.Length <= n ? s : s.Substring(0, n - 1) + "…"; }

        // ---------- why is the PC struggling? The same depth as a network diagnosis: what's full, what that does, why it's full,
        // who, the maths behind the feeling, and what fixes it (with how much it frees) - not just "close something".
        internal static List<string[]> WhySlow(Monitor m)
        {
            var rows = new List<string[]>(); var s = HwSpecs.Get(); var sd = HwSpecs.SystemDisk(s);
            var v = PcHealth.Sample(); Thread.Sleep(900); v = PcHealth.Sample();
            Func<string, double> c = k => { double x; return v.TryGetValue(k, out x) ? x : double.NaN; };
            double cpu = m.Cpu.Avg(10), mem = m.Mem.Last, faults = c("faults"), commit = c("commit"), limit = c("limit"), dms = c("diskms"), q = c("queue"), perf = c("perf"), dpc = c("dpc") + c("intr"), busy = 100 - c("idle");
            if (double.IsNaN(cpu)) cpu = m.Cpu.Last;
            List<AppStat> apps; lock (m.L) apps = m.Apps.Where(a => a.Name != "Idle" && a.Name != "_Total").ToList();
            Func<AppStat, string> who = a => { var e = global::AdeeboMeter.Explain.Find(a.Name); string nm = e != null && !string.IsNullOrEmpty(e.Friendly) ? e.Friendly : a.Name; return nm + (a.Count > 1 ? " (" + a.Count + " processes" + (e != null && (e.Tag ?? "").IndexOf("tab", StringComparison.OrdinalIgnoreCase) >= 0 ? "; one per tab" : "") + ")" : ""); };
            Func<AppStat, int> closeable = a => { var e = global::AdeeboMeter.Explain.Find(a.Name); return e == null ? 1 : e.Close; };
            double ramMb = s.RamGb * 1024, usedMb = ramMb * (double.IsNaN(mem) ? 0 : mem) / 100;
            // the slowest part of a page read back from the disk: what one costs on this disk (measured if we have it)
            double readMs = HwTests.LastNum("disk", 3); if (double.IsNaN(readMs)) readMs = sd != null && sd.Media == "HDD" ? 10 : 0.15;
            if (!double.IsNaN(dms) && dms > readMs) readMs = dms;
            var causes = new List<Tuple<double, string, string, string, string>>();   // weight, what's full, what that does, the maths, why
            // 1. memory
            if (mem > 85 || (commit / limit) > 0.9 || faults > 100)
            {
                double waitMs = double.IsNaN(faults) ? double.NaN : faults * readMs;   // each page read back waits for the disk
                var hogs = apps.OrderByDescending(a => a.Mem).Take(3).ToList();
                bool small = s.RamGb > 0 && s.RamGb < 8; var leak = PcHealth.Leaks().FirstOrDefault(x => x[0] != "0" && x[0] != "3");
                causes.Add(Tuple.Create(Math.Max(mem, double.IsNaN(faults) ? 0 : Math.Min(100, faults / 5)) + 10,
                    "Memory is " + F(mem) + "% full (" + F(usedMb / 1024) + " of " + Gb(s.RamGb) + ")" + (limit > 0 && !double.IsNaN(commit) ? ", " + F(100 * commit / limit) + "% of what Windows promised programs" : ""),
                    "When memory is full, Windows moves parts of programs you're not using this second to the disk, and reads them back the moment you touch them again (paging). Each read-back waits for the disk - so the click waits, the tab freezes, the typing lags.",
                    double.IsNaN(faults) ? "" : F(faults) + " pages read back a second × " + F(readMs) + " ms each (" + (sd != null && sd.Media == "HDD" ? "spinning disk" : "this disk") + ") ≈ " + F(waitMs) + " ms of waiting every second" + (waitMs > 500 ? " - that's the freezing you feel" : waitMs > 100 ? " - noticeable as stutter" : " - small on this fast disk, so you'll notice it mostly as apps reloading"),
                    small ? "This PC has only " + Gb(s.RamGb) + " - Windows and a browser alone fill that." : leak != null ? "Something grows and never gives memory back: " + leak[1] + " (" + leak[2] + ")." : hogs.Count > 0 ? "It's the apps: " + string.Join(", ", hogs.Select(a => who(a) + " " + Draw.MemStr(a.Mem)).ToArray()) + "." : ""));
            }
            // 2. the processor
            if (cpu > 80 || (!double.IsNaN(perf) && perf < 60 && cpu > 40))
            {
                double eff = double.IsNaN(perf) ? double.NaN : s.Cores * perf / 100;
                var hog = apps.OrderByDescending(a => a.Cpu).FirstOrDefault();
                bool held = !double.IsNaN(perf) && perf < 70;
                causes.Add(Tuple.Create(cpu, "The processor is " + F(cpu) + "% busy" + (held ? " - and running at only " + F(perf) + "% of its speed" : ""),
                    "Every program takes turns on the " + s.Cores + " cores. When they're all busy, each click and keystroke waits in line for a turn - the PC feels sluggish even though nothing is 'broken'.",
                    held ? s.Cores + " cores × " + F(perf) + "% speed ≈ " + F(eff) + " cores' worth of work - " + F(100 - perf) + "% of the processor is switched off by heat or power settings" : hog != null ? who(hog) + " alone uses " + F(hog.Cpu) + "% of the whole processor" : "",
                    held ? (HwSpecs.BatterySaver() ? "Battery saver is on." : System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline ? "It's on battery - laptops slow down to last longer." : "It's hot or a power setting caps it (plan: " + (s.Power == "" ? "?" : s.Power) + ").") : hog != null ? who(hog) + " is doing the work." : ""));
            }
            // 3. the disk
            if (busy > 80 || dms > 30)
            {
                string top = PcHealth.TopIo(); double waitQ = double.IsNaN(q) || double.IsNaN(dms) ? double.NaN : q * dms;
                causes.Add(Tuple.Create(Math.Max(busy, Math.Min(100, dms)), "The disk is " + F(busy) + "% busy, " + F(dms) + " ms per request",
                    "Programs start, files open and memory pages come back only as fast as the disk answers. A busy disk makes everything queue behind it - even things that have nothing to do with the program using it.",
                    double.IsNaN(waitQ) ? "" : F(q) + " requests waiting × " + F(dms) + " ms each ≈ " + F(waitQ) + " ms before a new one even starts (Little's law: wait = queue × time per request)",
                    (top != "" ? top + " is moving the most data. " : "") + (sd != null && sd.Media == "HDD" ? "Windows runs from a spinning disk, which can only do about 100 small reads a second - an SSD does thousands." : "")));
            }
            // 4. drivers
            if (dpc > 6) causes.Add(Tuple.Create(dpc * 4, "Drivers take " + F(dpc) + "% of the processor", "Drivers interrupt the processor to handle hardware (Wi-Fi, sound, USB). A misbehaving one interrupts so often that sound crackles and the mouse jumps.", "", "Usually the Wi-Fi, audio or graphics driver - update them from the PC maker's site."));
            if (causes.Count == 0)
            {
                rows.Add(R(0, "Verdict", "Nothing on this PC is struggling right now: processor " + F(cpu) + "%, memory " + F(mem) + "%, disk " + F(busy) + "% busy. If something feels slow, it's that app or the network - or press Ctrl+Alt+S the moment it happens.", "", "m"));
                foreach (var x in Hidden(m).Where(x => x[1] != "Verdict" && (x[0] == "1" || x[0] == "2")).Take(4)) rows.Add(x);
                return rows;
            }
            var main = causes.OrderByDescending(t => t.Item1).First();
            rows.Add(R(-1, "WHAT'S FULL", "", "", ""));
            rows.Add(R(2, "1. Symptom", main.Item2, "", "m"));
            rows.Add(R(3, "2. What that does", main.Item3, "", "i"));
            if (main.Item4 != "") rows.Add(R(1, "3. The maths", main.Item4, "", "m"));
            if (main.Item5 != "") rows.Add(R(1, "4. Why it's full", main.Item5, "", "m"));
            foreach (var o in causes.Where(t => t != main)) rows.Add(R(1, "Also", o.Item2 + (o.Item4 != "" ? " - " + o.Item4 : ""), o.Item3, "m"));
            // what to do, each with what it would give back
            rows.Add(R(-1, "WHAT FIXES IT", "", "", ""));
            int n = 0;
            if (main.Item2.StartsWith("Memory", StringComparison.Ordinal))
            {
                foreach (var a in apps.Where(a => closeable(a) == 0).OrderByDescending(a => a.Mem).Take(2))
                { double after = Math.Max(0, (usedMb - a.Mem / 1048576.0) / ramMb * 100); rows.Add(R(3, "Fix " + (++n), "Close " + who(a) + " - frees about " + Draw.MemStr(a.Mem) + ", memory drops to ≈ " + F(after) + "%" + (after < 80 ? " and the paging stops" : ""), "", "i")); }
                if (s.RamGb > 0 && s.RamGb <= 8 && !s.Soldered) rows.Add(R(3, "Fix " + (++n), "Add memory: " + Gb(s.RamGb) + " → " + Gb(s.RamGb * 2) + (s.Channels < 2 ? " (a second stick also doubles memory speed)" : ""), "", "i"));
            }
            else if (main.Item2.StartsWith("The processor", StringComparison.Ordinal))
            {
                if (main.Item2.Contains("speed")) rows.Add(R(3, "Fix " + (++n), "Plug in, turn battery saver off, and pick Best performance (Settings > System > Power) - that gives back the switched-off part", "", "i"));
                var hog = apps.OrderByDescending(a => a.Cpu).FirstOrDefault(); if (hog != null && hog.Cpu > 20) rows.Add(R(3, "Fix " + (++n), (closeable(hog) == 0 ? "Close or pause " : "Wait for, or restart, ") + who(hog) + " - gives back about " + F(hog.Cpu) + "% of the processor", "", "i"));
            }
            else if (main.Item2.StartsWith("The disk", StringComparison.Ordinal))
            {
                string top = PcHealth.TopIo(); if (top != "") rows.Add(R(3, "Fix " + (++n), "Let " + top + " finish, or schedule it for the night (antivirus scans, OneDrive, updates)", "", "i"));
                if (sd != null && sd.Media == "HDD") rows.Add(R(3, "Fix " + (++n), "Move Windows to an SSD - small reads go from about 10 ms to 0.1 ms, a hundred times faster", "", "i"));
            }
            else rows.Add(R(3, "Fix " + (++n), "Update the Wi-Fi, audio and graphics drivers", "", "i"));
            double days = (DateTime.Now - s.Boot).TotalDays; if (days > 14) rows.Add(R(3, "Fix " + (++n), "Restart (it's been " + F(Math.Floor(days)) + " days) - clears leaks and half-finished updates", "", "i"));
            string why = main.Item5 != "" ? " " + main.Item5 : "";
            rows.Insert(0, R(2, "Verdict", main.Item2 + ". " + main.Item3.Split('.')[0] + "." + why + (rows.Any(r => r[1] == "Fix 1") ? " Best fix: " + rows.First(r => r[1] == "Fix 1")[2].Split(DashSep, StringSplitOptions.None)[0].Trim() + "." : ""), "", "m"));
            return rows;
        }

        // ---------- the hidden reasons a PC is slow when the internet is fine
        internal static List<string[]> Hidden(Monitor m)
        {
            var rows = new List<string[]>(); var s = HwSpecs.Get();
            double days = (DateTime.Now - s.Boot).TotalDays;
            rows.Add(R(days > 14 ? 1 : 0, "Last restart", days < 1 ? "today" : F(Math.Floor(days)) + " days ago", days > 14 ? "Windows collects leaks and half-done updates over weeks. A real restart (not shut down - Fast Startup keeps the old state) clears them." : "", "w"));
            if (s.RestartPending) rows.Add(R(1, "Restart waiting", "an update is waiting for a restart", "Until then the update stays half done, and Windows keeps working on it in the background.", "w"));
            rows.Add(R(s.Startup.Count > 12 ? 1 : 0, "Start with Windows", s.Startup.Count + " programs" + (s.Startup.Count > 0 ? ": " + string.Join(", ", s.Startup.Take(6).ToArray()) + (s.Startup.Count > 6 ? " …" : "") : ""), s.Startup.Count > 12 ? "Each one slows the start and sits in memory. Task Manager > Startup apps turns them off (they still work when you open them)." : "", "w"));
            // browsers: dozens of processes, one bill
            List<AppStat> apps; lock (m.L) apps = m.Apps.ToList();
            foreach (var b in new[] { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi" })
            {
                var mine = apps.Where(a => a.Name.Equals(b, StringComparison.OrdinalIgnoreCase)).ToList(); if (mine.Count == 0) continue;
                double mb = mine.Sum(a => a.Mem) / 1048576.0; int n = mine.Sum(a => Math.Max(1, a.Count));
                rows.Add(R(mb > 3000 ? 1 : 0, "Browser: " + b, F(mb) + " MB in " + n + " processes · " + F(mine.Sum(a => a.Cpu)) + "% processor", mb > 3000 ? "Tabs add up: close what you don't need, or use the browser's sleeping tabs / efficiency mode." : "", "m"));
            }
            bool saver = HwSpecs.BatterySaver(); rows.Add(R(saver || Regex.IsMatch(s.Power, "saver", RegexOptions.IgnoreCase) ? 1 : 0, "Power", (s.Power == "" ? "plan not readable" : "plan: " + s.Power) + (saver ? " · battery saver ON" : ""), saver ? "Battery saver holds the processor back." : "", "w"));
            var sd = HwSpecs.SystemDisk(s);
            if (sd != null) rows.Add(R(sd.Media == "HDD" ? 2 : 0, "Windows is on", (sd.Media == "HDD" ? "a spinning hard disk" : sd.Media == "" ? "a disk of unreported kind" : "an " + sd.Media) + " (" + sd.Name + ")", sd.Media == "HDD" ? "The biggest single cause of a slow PC. An SSD makes start-up and every program open several times faster." : "", "w"));
            try { var di = new DriveInfo(HwSpecs.SystemLetter()); double fr = (double)di.AvailableFreeSpace / di.TotalSize; rows.Add(R(fr < 0.1 ? 2 : fr < 0.15 ? 1 : 0, "Free space on " + di.Name.TrimEnd('\\'), (fr * 100).ToString("0", CultureInfo.CurrentCulture) + "% free", fr < 0.15 ? "An SSD slows down when nearly full, and Windows can't update. Toolbox > Where did my space go?" : "", "w")); } catch (IOException) { } catch (ArgumentException) { }
            rows.Add(R(s.RamGb < 8 ? 1 : 0, "Memory", Gb(s.RamGb) + " · " + s.Channels + " channel" + (s.Channels > 1 ? "s" : ""), s.RamGb < 8 ? "8 GB is the comfortable minimum for Windows and a browser." : "", "w"));
            var errs = Errors(30); int bad = errs.Sum(e => e.Item3);
            rows.Add(R(errs.Any(e => e.Item1 == "disk" && e.Item3 > 0) ? 2 : bad > 0 ? 1 : 0, "Hardware errors (30 days)", bad == 0 ? "none" : string.Join(" · ", errs.Where(e => e.Item3 > 0).Select(e => e.Item3 + " " + e.Item2).ToArray()), bad > 0 ? "Disk errors mean a failing disk or cable - back up now. Sudden power-offs mean a crash, overheating or a weak battery / charger." : "", "w"));
            string pat = PcRecord.Pattern(); if (pat != "") rows.Add(R(3, "When it felt slow", pat, "", "m"));
            var top = rows.Where(x => x[0] == "2").Concat(rows.Where(x => x[0] == "1")).FirstOrDefault();
            rows.Insert(0, R(top == null ? 0 : int.Parse(top[0], CultureInfo.InvariantCulture), "Verdict", top == null ? "None of the usual hidden causes: restarted recently, few startup apps, an SSD with room, no hardware errors." : "The likeliest hidden cause: " + top[1].ToLowerInvariant() + " - " + top[2] + ". " + top[3], "", "i"));
            return rows;
        }
        // (key, words, count): disk errors, controller resets, hardware errors (WHEA), sudden power-offs - the System log, no admin
        internal static List<Tuple<string, string, int>> Errors(int days)
        {
            long ms = days * 86400000L;
            var q = new[] {
                Tuple.Create("disk", "disk errors", "*[System[(Provider[@Name='disk'] or Provider[@Name='Ntfs']) and (EventID=7 or EventID=11 or EventID=51 or EventID=55 or EventID=153) and TimeCreated[timediff(@SystemTime) <= " + ms + "]]]"),
                Tuple.Create("reset", "disk controller resets", "*[System[(Provider[@Name='stornvme'] or Provider[@Name='storahci'] or Provider[@Name='iaStorAC'] or Provider[@Name='iaStorA']) and (EventID=129 or EventID=11) and TimeCreated[timediff(@SystemTime) <= " + ms + "]]]"),
                Tuple.Create("whea", "hardware errors (WHEA)", "*[System[Provider[@Name='Microsoft-Windows-WHEA-Logger'] and (Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= " + ms + "]]]"),
                Tuple.Create("power", "sudden power-offs", "*[System[Provider[@Name='Microsoft-Windows-Kernel-Power'] and EventID=41 and TimeCreated[timediff(@SystemTime) <= " + ms + "]]]") };
            var r = new List<Tuple<string, string, int>>();
            foreach (var x in q)
            {
                int n = 0;
                try { n = CountEvents(x.Item3); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("hw errors " + x.Item1 + ": " + e.Message); }
                r.Add(Tuple.Create(x.Item1, x.Item2, n));
            }
            return r;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static int CountEvents(string xpath)
        {
            int n = 0;
            using (var rd = new System.Diagnostics.Eventing.Reader.EventLogReader(new System.Diagnostics.Eventing.Reader.EventLogQuery("System", System.Diagnostics.Eventing.Reader.PathType.LogName, xpath)))
                for (var e = rd.ReadEvent(); e != null && n < 999; e = rd.ReadEvent()) using (e) n++;
            return n;
        }

        // ---------- the whole PC on one page: every part, its ceiling, and the narrowest pipe
        internal static List<string[]> Map(Monitor m)
        {
            var rows = new List<string[]>(); var s = HwSpecs.Get(); var sd = HwSpecs.SystemDisk(s);
            string dw; double dc = sd == null ? double.NaN : HwSpecs.DiskCeiling(sd, out dw);
            double dr = HwTests.LastNum("disk", 2), dwr = HwTests.LastNum("disk", 1), ram = HwTests.LastNum("ram", 1), rc = HwSpecs.RamCeilingGBs(s), perf = HwTests.LastNum("cpu", 3);
            var pv = PcHealth.Sample(); double busy, bytes; pv.TryGetValue("idle", out busy); busy = double.IsNaN(busy) ? double.NaN : 100 - busy; pv.TryGetValue("dbytes", out bytes);
            rows.Add(R(3, "Processor", (s.Cpu == "" ? "?" : s.Cpu) + " · " + s.Cores + "C/" + s.Threads + "T · " + F(m.Cpu.Last) + "% busy now" + (double.IsNaN(perf) ? "" : " · " + F(perf) + "% speed under load (last test)"), "", "w"));
            rows.Add(R(s.Channels < 2 ? 1 : 3, "Memory", Gb(s.RamGb) + " " + HwSpecs.RamTypeOf(s) + " · " + s.Channels + " ch · ceiling " + F(rc) + " GB/s" + (double.IsNaN(ram) ? "" : " · measured " + F(ram)) + " · " + F(m.Mem.Last) + "% in use", "", "w"));
            foreach (var d in s.Disks.Take(3)) { string w; double c = HwSpecs.DiskCeiling(d, out w); rows.Add(R(d.Media == "HDD" && d.System ? 1 : 3, "Disk" + (d.Letters != "" ? " " + d.Letters : ""), d.Name + " · " + (d.Media == "" ? d.Bus : d.Media + " " + d.Bus) + (d.MaxWidth > 0 ? " · PCIe " + d.LinkGen + ".0 x" + d.LinkWidth : "") + " · ceiling " + F(c) + " MB/s" + (d.System && !double.IsNaN(dr) ? " · measured " + F(dr) + " read / " + F(dwr) + " write" : ""), "", "w")); }
            if (!double.IsNaN(busy)) rows.Add(R(busy > 90 ? 1 : 0, "Disk now", F(busy) + "% busy · " + F(bytes / 1048576) + " MB/s moving", "", "m"));
            foreach (var g in s.Gpus.Where(g => !g.Software)) rows.Add(R(3, g.Integrated ? "Built-in graphics" : "Graphics card", g.Name + " · " + (g.Integrated ? "borrows up to " + Gb(g.SharedMb / 1024) : Gb(g.DedicatedMb / 1024) + " own memory") + (g.MaxWidth > 0 ? " · PCIe " + g.LinkGen + ".0 x" + g.LinkWidth + " (max x" + g.MaxWidth + ")" : ""), "", "w"));
            rows.Add(R(3, "Network", (s.NicKind == "" ? "?" : s.NicKind) + " · " + F(s.NicMbps) + " Mbps link · " + F(m.Down.Last) + " Mb/s down now", "", "w"));
            // every part explained in its own numbers (the advice line under each row)
            foreach (var x in rows) if (x[3] == "") x[3] = x[1] == "Processor" ? Explain("cores", s) + " " + Explain("clock", s) : x[1] == "Memory" ? Explain("ram", s) : x[1].StartsWith("Disk ", StringComparison.Ordinal) || x[1] == "Disk" ? Explain("disk", s) : x[1].Contains("raphics") ? Explain("gpu", s) : x[1] == "Network" ? Explain("net", s) : "";
            // the narrowest pipe, from what's measured or specified
            string neck = sd != null && sd.Media == "HDD" && sd.System ? "the spinning hard disk Windows runs from" : s.Channels < 2 && s.Gpus.Any(g => g.Integrated) && !s.Gpus.Any(g => !g.Integrated && !g.Software) ? "single-channel memory shared with the built-in graphics"
                : !double.IsNaN(dwr) && !double.IsNaN(dc) && dwr < dc * 0.3 ? "the disk's write speed (" + F(dwr) + " of " + F(dc) + " MB/s)" : s.RamGb < 8 ? "the amount of memory (" + Gb(s.RamGb) + ")" : !double.IsNaN(perf) && perf < 70 ? "the processor held back to " + F(perf) + "%" : "";
            rows.Insert(0, R(neck == "" ? 0 : 1, "Narrowest pipe", neck == "" ? "nothing stands out - the parts are balanced" + (double.IsNaN(dr) ? " (run Disk speed and CPU held back? for measured numbers)" : "") : neck, "", "i"));
            return rows;
        }

        // ---------- buying a used laptop: everything above, the battery, the seller's words, and hands-on tests
        internal static List<string[]> Laptop(Monitor m, string seller, Toolkit.Run run)
        {
            var rows = new List<string[]>(); var s = HwSpecs.Get(); var bad = new List<string>();
            Action<string> sec = t => rows.Add(R(-1, t, "", "", ""));
            Action<List<string[]>, string> take = (src, title) => { var v = src.FirstOrDefault(x => x[1] == "Verdict"); if (v != null) { rows.Add(R(int.Parse(v[0], CultureInfo.InvariantCulture), title, v[2], v[3], v[4])); if (v[0] == "2" || v[0] == "1") bad.Add(title.ToLowerInvariant() + ": " + Cutl(v[2], 90)); } foreach (var x in src.Where(x => x[1] != "Verdict" && x[0] != "3" && x[0] != "-1").Take(4)) rows.Add(x); };
            sec("WHAT'S INSIDE");
            rows.Add(R(3, "Model", (s.Maker + " " + s.Model).Trim() + (s.Bios != "" ? " · BIOS from " + s.Bios : ""), "", "w"));
            rows.Add(R(3, "Processor", s.Cpu + " · " + s.Cores + " cores", "", "w"));
            rows.Add(R(3, "Memory", Gb(s.RamGb) + " " + HwSpecs.RamTypeOf(s) + " · " + s.Channels + " channel" + (s.Channels > 1 ? "s" : "") + (s.Soldered ? " · soldered" : ""), "", "w"));
            foreach (var d in s.Disks.Where(d => d.Bus != "USB" && d.Bus != "Virtual").Take(2)) rows.Add(R(d.Health == 0 ? 3 : d.Health == 1 ? 1 : d.Health == 2 ? 2 : 3, "Disk", d.Name + " · " + F(d.Gb) + " GB " + d.Media + " " + d.Bus + (d.Health == 1 ? " · Windows: WARNING" : d.Health == 2 ? " · Windows: UNHEALTHY" : ""), "", "w"));
            foreach (var g in s.Gpus.Where(g => !g.Software)) rows.Add(R(3, g.Integrated ? "Built-in graphics" : "Graphics card", g.Name + (g.Integrated ? "" : " · " + Gb(g.DedicatedMb / 1024)), "", "w"));
            if (!string.IsNullOrWhiteSpace(seller)) foreach (var x in Seller(seller, s)) { rows.Add(x); if (x[0] == "2") bad.Add(x[1] + ": " + x[2]); }
            sec("BATTERY");
            double de, fu; int cy; HwSpecs.Battery(out de, out fu, out cy);
            if (double.IsNaN(de) || double.IsNaN(fu) || de <= 0) rows.Add(R(3, "Battery health", System.Windows.Forms.SystemInformation.PowerStatus.BatteryChargeStatus == System.Windows.Forms.BatteryChargeStatus.NoSystemBattery ? "no battery" : "Windows didn't report it - run 'powercfg /batteryreport' in a terminal", "", "u"));
            else
            {
                double h = 100 * fu / de; rows.Add(R(h < 60 ? 2 : h < 80 ? 1 : 0, "Battery health", F(h) + "% of new (" + F(fu / 1000) + " of " + F(de / 1000) + " Wh)" + (cy > 0 ? " · " + cy + " charge cycles" : ""), h < 80 ? "Below 80% the battery runs noticeably shorter; a replacement costs real money - count it in the price." : "", "w"));
                if (h < 80) bad.Add("battery at " + F(h) + "% of new");
            }
            sec("DISK"); take(DiskSpeed("", true, run), "Disk speed");
            var wear = HwSpecs.Wear();
            if (wear != null) rows.Add(R(wear[0] > 50 || wear[3] > 0 ? 2 : wear[0] > 20 ? 1 : 0, "Disk wear", F(wear[0]) + "% worn · " + F(wear[1]) + " hours used" + (wear[2] > 0 ? " · " + F(wear[2]) + " °C" : "") + (wear[3] > 0 ? " · " + F(wear[3]) + " read errors" : "") + (wear[0] > 0 && wear[1] > 0 ? " · at this pace ≈ " + F((100 - wear[0]) / wear[0] * wear[1] / 2000) + " more years (2,000 h a year)" : ""), "", "w"));
            else rows.Add(R(3, "Disk wear", "needs admin - right-click AdeeboMeter > Run as administrator to see wear %, hours used and temperature", "", "u"));
            sec("MEMORY, GRAPHICS, PROCESSOR"); take(Ram(run), "Memory"); take(Gpu(), "Graphics"); take(Cpu(run), "Processor");
            sec("HIDDEN TROUBLE");
            var errs = Errors(30); int nbad = errs.Sum(e => e.Item3);
            rows.Add(R(errs.Any(e => (e.Item1 == "disk" || e.Item1 == "reset") && e.Item3 > 0) ? 2 : nbad > 0 ? 1 : 0, "Errors in 30 days", nbad == 0 ? "none - no disk, hardware or power-off errors" : string.Join(" · ", errs.Where(e => e.Item3 > 0).Select(e => e.Item3 + " " + e.Item2).ToArray()), nbad > 0 ? "Ask the seller about these. Disk errors = a failing disk; sudden power-offs = crashes, heat or the battery." : "", "w"));
            if (nbad > 0) bad.Add("errors in the event log");
            rows.Add(R(3, "Hands-on", "test the screen, every key, the speakers and the mic with the buttons under this result - a camera test opens Windows' Camera app", "", "i"));
            int worst = rows.Where(x => x[0] != "3" && x[0] != "-1").Select(x => int.Parse(x[0], CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max();
            rows.Insert(0, R(worst, "Verdict", bad.Count == 0 ? "Nothing found against it: the parts do what their specs say, the battery holds, and there are no hidden errors. Do the hands-on tests, then decide." : "Things to weigh (or haggle with): " + string.Join("; ", bad.Take(4).ToArray()) + ".", "", "m"));
            return rows;
        }
        // "i7, 16GB, 512 SSD, RTX 3050": each number the seller wrote, against what's inside
        internal static List<string[]> Seller(string said, HwSpecs.Specs s)
        {
            var r = new List<string[]>(); said = said ?? "";
            var ram = Regex.Match(said, @"(\d{1,3})\s*GB?\s*(?:RAM|DDR|memory|ram)?\b(?!\s*(?:SSD|HDD|NVMe|storage|disk))", RegexOptions.IgnoreCase);
            var disk = Regex.Match(said, @"(\d(?:\.\d)?\s*TB|\d{3,4}\s*GB?)\s*(?:SSD|HDD|NVMe|storage|disk)", RegexOptions.IgnoreCase);
            var cpu = Regex.Match(said, @"\b(i[3579]|ryzen\s*[3579]|core\s*ultra\s*[3579]|celeron|pentium|m[1-4])\b", RegexOptions.IgnoreCase);
            var gpu = Regex.Match(said, @"\b(rtx|gtx|rx)\s*\d{3,4}\w*", RegexOptions.IgnoreCase);
            if (ram.Success) { double want = double.Parse(ram.Groups[1].Value, CultureInfo.InvariantCulture); bool ok = Math.Abs(s.RamGb - want) <= Math.Max(1, want * 0.1); r.Add(R(ok ? 0 : 2, "Seller said: " + want + " GB memory", ok ? "matches (" + Gb(s.RamGb) + ")" : "this PC has " + Gb(s.RamGb), "", "w")); }
            if (disk.Success)
            {
                string t = disk.Groups[1].Value.ToUpperInvariant().Replace(" ", ""); double want = t.EndsWith("TB", StringComparison.Ordinal) ? double.Parse(t.Replace("TB", ""), CultureInfo.InvariantCulture) * 1000 : double.Parse(Regex.Replace(t, "[^0-9]", ""), CultureInfo.InvariantCulture);
                double have = s.Disks.Where(d => d.Bus != "USB").Select(d => d.Gb).DefaultIfEmpty(0).Max(); bool ok = have >= want * 0.9 && have <= want * 1.15;
                bool ssdSaid = Regex.IsMatch(disk.Value, "SSD|NVMe", RegexOptions.IgnoreCase); var sd = HwSpecs.SystemDisk(s); bool hdd = sd != null && sd.Media == "HDD";
                r.Add(R(ok && !(ssdSaid && hdd) ? 0 : 2, "Seller said: " + disk.Value.Trim(), (ok ? "size matches (" + F(have) + " GB)" : "this PC's disk is " + F(have) + " GB") + (ssdSaid && hdd ? " - but it's a spinning hard disk, not an SSD" : ""), "", "w"));
            }
            if (cpu.Success) { string w = cpu.Groups[1].Value.Replace(" ", "").ToLowerInvariant(); bool ok = s.Cpu.Replace(" ", "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0; r.Add(R(ok ? 0 : 2, "Seller said: " + cpu.Value, ok ? "matches (" + s.Cpu + ")" : "this PC has " + s.Cpu, "", "w")); }
            if (gpu.Success) { bool ok = s.Gpus.Any(g => g.Name.Replace(" ", "").IndexOf(gpu.Value.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) >= 0); r.Add(R(ok ? 0 : 2, "Seller said: " + gpu.Value, ok ? "matches" : "not found - this PC has " + string.Join(", ", s.Gpus.Where(g => !g.Software).Select(g => g.Name).ToArray()), "", "w")); }
            if (r.Count == 0) r.Add(R(3, "Seller said", "couldn't read numbers from \"" + Cutl(said, 40) + "\" - try e.g. i5, 16GB, 512GB SSD, RTX 3050", "", "u"));
            return r;
        }

        // ---------- for the Full checkup and "my PC is slow": the quick, cheap version (no 8 GB writes)
        internal static List<string[]> Quick(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>(); var s = HwSpecs.Get(); var sd = HwSpecs.SystemDisk(s);
            if (sd != null)
            {
                string why; double c = HwSpecs.DiskCeiling(sd, out why); var r = HwTests.Disk("", false, run);
                if (r.Error == "")
                {
                    double worst = Math.Min(r.ReadMBs, r.WriteMBs); bool busy = r.BusyBefore > 40;
                    rows.Add(R(busy ? 1 : worst < c * 0.2 || r.LatMs > 2.5 ? 2 : worst < c * 0.4 ? 1 : 0, "Disk speed vs its ceiling", F(r.ReadMBs) + " read / " + F(r.WriteMBs) + " write MB/s · " + Pct(worst, c) + " of " + F(c) + " MB/s" + (r.LatMs > 2.5 ? " · " + F(r.LatMs) + " ms per small read (spinning-disk slow)" : ""),
                        busy ? "The disk was already " + F(r.BusyBefore) + "% busy" + (r.Culprit != "" ? " (" + r.Culprit + ")" : "") + " - that's what slows the PC now." : r.LatMs > 2.5 ? "Windows on a slow disk: an SSD is the biggest upgrade." : worst < c * 0.4 ? "Slower than its kind: Hardware nerds > Disk speed runs the full test." : "", "m"));
                }
            }
            if (s.Channels < 2 && s.RamSticks > 0) rows.Add(R(1, "Memory channels", "single channel (" + Gb(s.RamGb) + ")", s.Soldered ? "Half the memory speed (soldered - can't be changed)." : "Half the memory speed: a second matching stick doubles it.", "w"));
            if (s.RamGb > 0 && s.RamGb < 8) rows.Add(R(1, "Memory size", Gb(s.RamGb), "Tight for Windows and a browser.", "w"));
            var errs = Errors(30); var de = errs.Where(e => (e.Item1 == "disk" || e.Item1 == "reset") && e.Item3 > 0).ToList();
            if (de.Count > 0) rows.Add(R(2, "Disk errors (30 days)", string.Join(" · ", de.Select(e => e.Item3 + " " + e.Item2).ToArray()), "A failing disk or cable - back up now.", "w"));
            var pw = errs.FirstOrDefault(e => e.Item1 == "power"); if (pw != null && pw.Item3 > 0) rows.Add(R(1, "Sudden power-offs (30 days)", pw.Item3.ToString(CultureInfo.CurrentCulture), "Crashes, overheating or a weak battery / charger.", "w"));
            double days = (DateTime.Now - s.Boot).TotalDays; if (days > 14) rows.Add(R(1, "Last restart", F(Math.Floor(days)) + " days ago", "A real restart clears weeks of leaks and half-done updates.", "w"));
            if (s.RestartPending) rows.Add(R(1, "Restart waiting", "an update is waiting for a restart", "", "w"));
            var link = NetLinkIssue(m, s); if (link != null) rows.Add(link);
            return rows;
        }
        internal static string[] NetLinkIssue(Monitor m, HwSpecs.Specs s)
        {
            var spd = SpeedTest.Last; double plan = spd != null ? spd.Down : double.NaN;
            if (s.NicKind == "Cable" && s.NicMbps > 0 && s.NicMbps <= 100) return R(1, "Cable link", F(s.NicMbps) + " Mbps", "A 100 Mbps cable link: a damaged or old cable, or a 100 Mbps port - most do 1,000.", "w");
            if (!double.IsNaN(plan) && !double.IsNaN(s.NicMbps) && s.NicMbps < plan * 1.1) return R(2, "Network link vs plan", F(s.NicMbps) + " Mbps link, " + F(plan) + " Mbps plan", "The PC's own link caps the internet - not the ISP.", "w");
            return null;
        }
    }
}
