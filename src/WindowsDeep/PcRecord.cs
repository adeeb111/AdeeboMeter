// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AdeeboMeter
{
    // ================================================================== "It feels slow": the last minutes of this PC, kept in memory
    // Brain reads the PC's counters every 5 s anyway; they go into a 15-minute ring next to the network's own numbers. When
    // something feels slow, the last 2 minutes are frozen and judged three ways: the internet, this PC, or the site / app itself.
    // Nothing leaves the PC; program names only - never window titles (they hold mail subjects and file names).
    static class PcRecord
    {
        internal sealed class Pt
        {
            public DateTime T; public double DiskMBs = double.NaN, Cpu, Busy, DiskMs, Faults, Perf, Dpc, Net, Gw, Down, Up, Mem; public bool Lost; public string TopCpu = "", TopIo = ""; public double TopCpuPct;
        }
        internal sealed class Capture { public DateTime T; public string Who = "", Text = "", Cause = "", Culprit = "", Fix = ""; public int Lv; public List<Pt> Pts = new List<Pt>(); }
        static readonly List<Pt> ring = new List<Pt>(); static readonly object k = new object();
        static DateTime lastNetAt;

        // from Brain's PC job, every 5 s (15 s on Penny)
        public static void Add(Monitor m, Dictionary<string, double> pv)
        {
            var p = new Pt { T = DateTime.Now, Cpu = m.Cpu.Last, Mem = m.Mem.Last, Net = m.NetPing.Last, Gw = m.GwPing.Last, Down = m.Down.Last, Up = m.Up.Last };
            double v; p.DiskMs = pv.TryGetValue("diskms", out v) ? v : double.NaN; p.Faults = pv.TryGetValue("faults", out v) ? v : double.NaN; p.Perf = pv.TryGetValue("perf", out v) ? v : double.NaN;
            double i1, i2; p.Dpc = pv.TryGetValue("dpc", out i1) && pv.TryGetValue("intr", out i2) && !double.IsNaN(i1) && !double.IsNaN(i2) ? i1 + i2 : double.NaN;
            p.Busy = pv.TryGetValue("idle", out v) && !double.IsNaN(v) ? Math.Max(0, Math.Min(100, 100 - v)) : double.NaN;
            if (pv.TryGetValue("dbytes", out v) && !double.IsNaN(v)) p.DiskMBs = v / 1048576;
            DateTime nat = m.NetAt; p.Lost = nat != lastNetAt && double.IsNaN(m.NetPing.Last) && m.NetPing.Values(60).Length > 0; lastNetAt = nat;
            lock (m.L) { var top = m.Apps.Where(a => a.Name != "AdeeboMeter" && a.Name != "Idle" && a.Name != "_Total").OrderByDescending(a => a.Cpu).FirstOrDefault(); if (top != null) { p.TopCpu = top.Name; p.TopCpuPct = top.Cpu; } }
            p.TopIo = PcHealth.TopIo();
            lock (k) { ring.Add(p); while (ring.Count > 0 && (p.T - ring[0].T).TotalMinutes > 15) ring.RemoveAt(0); }
        }
        internal static List<Pt> Last(double seconds) { var since = DateTime.Now.AddSeconds(-seconds); lock (k) return ring.Where(p => p.T >= since).ToList(); }

        // ---------- the verdict over a window of samples
        static double Share(List<Pt> pts, Func<Pt, bool> f) { var n = pts.Count; return n == 0 ? 0 : (double)pts.Count(f) / n; }
        static double Peak(List<Pt> pts, Func<Pt, double> f) { var x = pts.Select(f).Where(z => !double.IsNaN(z)).ToList(); return x.Count == 0 ? double.NaN : x.Max(); }
        static double Avg(List<Pt> pts, Func<Pt, double> f) { var x = pts.Select(f).Where(z => !double.IsNaN(z)).ToList(); return x.Count == 0 ? double.NaN : x.Average(); }
        static string Most(IEnumerable<string> names) { return names.Where(n => !string.IsNullOrEmpty(n)).GroupBy(n => n).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? ""; }
        internal static Capture Judge(List<Pt> pts, DateTime at)
        {
            var c = new Capture { T = at, Pts = pts };
            if (pts.Count < 3) { c.Who = "unknown"; c.Lv = 3; c.Text = "Not enough of a record yet - AdeeboMeter keeps the last minutes once it has run for a little while. Press it again next time it feels slow."; return c; }
            double cpuS = Share(pts, p => p.Cpu > 90), diskS = Share(pts, p => p.Busy > 90 || p.DiskMs > 50), pageS = Share(pts, p => p.Faults > 300), heldS = Share(pts, p => !double.IsNaN(p.Perf) && p.Perf < 60 && p.Cpu > 50), dpcS = Share(pts, p => p.Dpc > 15);
            int netN = pts.Count(p => !double.IsNaN(p.Net) || p.Lost);
            double netBadS = netN == 0 ? 0 : (double)pts.Count(p => p.Lost || p.Net > 150 || p.Gw > 50) / Math.Max(1, netN);
            var pc = new List<Tuple<double, string, string, string>>();   // share, cause, culprit, fix
            if (pageS > 0) pc.Add(Tuple.Create(pageS + 0.05, "memory is full - Windows keeps reading programs back from the disk (" + HwSpecs.Fmt(Peak(pts, p => p.Faults)) + " pages/s at worst)", Most(pts.Select(p => p.TopCpu)), "close the heaviest app, or add memory"));
            if (diskS > 0) pc.Add(Tuple.Create(diskS, "the disk was " + (Peak(pts, p => p.Busy) >= 99 ? "100%" : HwSpecs.Fmt(Peak(pts, p => p.Busy)) + "%") + " busy, up to " + HwSpecs.Fmt(Peak(pts, p => p.DiskMs)) + " ms per request", Most(pts.Select(p => p.TopIo)), "let it finish, or move that job to the night (Hardware nerds > Disk speed shows if the disk itself is slow)"));
            if (cpuS > 0) pc.Add(Tuple.Create(cpuS, "the processor was maxed out (" + HwSpecs.Fmt(Peak(pts, p => p.Cpu)) + "%)", Most(pts.Where(p => p.Cpu > 90).Select(p => p.TopCpu)), "close or pause that program"));
            if (heldS > 0) pc.Add(Tuple.Create(heldS, "the processor was held back to " + HwSpecs.Fmt(Avg(pts.Where(p => !double.IsNaN(p.Perf) && p.Perf < 60).ToList(), p => p.Perf)) + "% of its speed (heat, battery saver or the power plan)", "", "plug in, clear the vents, and pick Best performance in Settings > Power"));
            if (dpcS > 0) pc.Add(Tuple.Create(dpcS, "a driver kept the processor busy (" + HwSpecs.Fmt(Peak(pts, p => p.Dpc)) + "% on interrupts) - stutter, crackling sound, mouse lag", "", "update the Wi-Fi, audio and graphics drivers"));
            var best = pc.OrderByDescending(t => t.Item1).FirstOrDefault(); bool pcBad = best != null && best.Item1 >= 0.25, netBad = netBadS >= 0.25;
            string when = at.ToString("t", CultureInfo.CurrentCulture);
            string netNow = netN == 0 ? "the internet wasn't measured (the background checks are off)" : "the internet answered in " + HwSpecs.Fmt(Avg(pts, p => p.Net)) + " ms" + (pts.Any(p => p.Lost) ? " with lost replies" : "");
            if (pcBad)
            {
                c.Cause = best.Item2; c.Culprit = best.Item3; c.Fix = best.Item4; c.Lv = best.Item1 >= 0.5 ? 2 : 1;
                c.Who = netBad ? "both" : "pc";
                c.Text = "At " + when + ": " + (netBad ? "both this PC and the internet. " : "this PC, not the internet. ") + Up(c.Cause) + (c.Culprit != "" ? " - mostly " + c.Culprit : "") + ". " + (netBad ? "The internet was slow too (" + netNow + ")." : Up(netNow) + ".") + " Try: " + c.Fix + ".";
            }
            else if (netBad)
            {
                c.Who = "internet"; c.Lv = netBadS >= 0.5 ? 2 : 1;
                double gwA = Avg(pts, p => p.Gw); bool near = pts.Count(p => p.Gw > 50) > pts.Count(p => p.Net > 150 || p.Lost) / 2;
                c.Cause = near ? "your router / Wi-Fi was slow to answer (" + HwSpecs.Fmt(gwA) + " ms)" : "the internet beyond your router was slow or dropped replies (" + HwSpecs.Fmt(Avg(pts, p => p.Net)) + " ms)";
                c.Text = "At " + when + ": the internet, not this PC. " + Up(c.Cause) + ". The PC itself was fine: processor " + HwSpecs.Fmt(Avg(pts, p => p.Cpu)) + "%, disk " + HwSpecs.Fmt(Avg(pts, p => p.Busy)) + "% busy.";
                c.Fix = near ? "move closer to the router or use a cable" : "if it keeps happening, the Report for my ISP shows it with numbers";
            }
            else
            {
                c.Who = "app"; c.Lv = 1;
                c.Text = "At " + when + ": not this PC and not the internet - both were fine (processor " + HwSpecs.Fmt(Avg(pts, p => p.Cpu)) + "%, disk " + HwSpecs.Fmt(Avg(pts, p => p.Busy)) + "% busy, " + netNow + "). The slowness is most likely the site or app itself - its servers, or the app's own work.";
                c.Fix = "try another site or app: if only this one is slow, it's on their side";
            }
            return c;
        }
        static string Up(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        // ---------- pressing "It feels slow": freeze the last 2 minutes, judge, keep the last 30
        static readonly object fk = new object();
        public static Capture FeltSlow()
        {
            var c = Judge(Last(150), DateTime.Now);
            if (c.Pts.Count >= 3) lock (fk)
                {
                    try
                    {
                        var path = Store.PathOf("feltslow.txt"); var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
                        lines.Add(c.T.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "\t" + c.Who + "\t" + c.Lv.ToString(CultureInfo.InvariantCulture) + "\t" + Clean(c.Text) + "\t" + Series(c.Pts));
                        while (lines.Count > 30) lines.RemoveAt(0);
                        FullView.WriteSafe(path, string.Join("\n", lines) + "\n", false);
                    }
                    catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("feltslow: " + e.Message); }
                }
            if (c.Pts.Count >= 3) { lock (fk) lastCap = c; }
            return c;
        }
        static Capture lastCap;
        internal static Capture Latest { get { lock (fk) return lastCap; } }
        static string Clean(string s) { return (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' '); }
        internal static List<string[]> Earlier()
        {
            var r = new List<string[]>();
            try { var path = Store.PathOf("feltslow.txt"); if (File.Exists(path)) foreach (var l in File.ReadAllLines(path)) { var p = l.Split('\t'); if (p.Length >= 4) r.Add(p); } }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            return r;
        }
        // "CPU 12 15 95 ; Disk 5 8 99 ; Ping 14 15 210" - one number per sample, so a picture, the CSV and an AI can all read it
        internal static string Series(List<Pt> pts)
        {
            Func<double, string> n = v => double.IsNaN(v) ? "-" : Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
            var take = pts.Skip(Math.Max(0, pts.Count - 30)).ToList(); var sb = new StringBuilder();
            sb.Append("CPU ").Append(string.Join(" ", take.Select(p => n(p.Cpu)).ToArray()));
            sb.Append(" ; Disk ").Append(string.Join(" ", take.Select(p => n(p.Busy)).ToArray()));
            sb.Append(" ; Ping ").Append(string.Join(" ", take.Select(p => p.Lost ? "x" : n(p.Net)).ToArray()));
            return sb.ToString();
        }

        // ---------- the tool: the last time it felt slow (or right now, if never pressed)
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        internal static List<string[]> Rows(bool now)
        {
            var rows = new List<string[]>();
            var c = now ? FeltSlow() : (Latest ?? FeltSlow());
            rows.Add(R(c.Lv, "Verdict", c.Text, c.Fix == "" ? "" : "Try: " + c.Fix, c.Pts.Count >= 3 ? "m" : "u"));
            if (c.Pts.Count >= 3)
            {
                var pts = c.Pts;
                rows.Add(R(c.Who == "pc" || c.Who == "both" ? 2 : c.Who == "internet" ? 0 : 0, "Who", c.Who == "pc" ? "this PC" : c.Who == "both" ? "this PC and the internet" : c.Who == "internet" ? "the internet / your router" : "the site or app itself", "", "i"));
                rows.Add(R(Peak(pts, p => p.Cpu) > 90 ? 1 : 0, "Processor", "average " + HwSpecs.Fmt(Avg(pts, p => p.Cpu)) + "% · peak " + HwSpecs.Fmt(Peak(pts, p => p.Cpu)) + "%" + (Most(pts.Select(p => p.TopCpu)) != "" ? " · busiest: " + Most(pts.Select(p => p.TopCpu)) : ""), "", "m"));
                rows.Add(R(Peak(pts, p => p.Busy) > 90 || Peak(pts, p => p.DiskMs) > 50 ? 1 : 0, "Disk", "busy " + HwSpecs.Fmt(Avg(pts, p => p.Busy)) + "% on average · up to " + HwSpecs.Fmt(Peak(pts, p => p.DiskMs)) + " ms per request" + (Most(pts.Select(p => p.TopIo)) != "" ? " · busiest: " + Most(pts.Select(p => p.TopIo)) : ""), "", "m"));
                rows.Add(R(Peak(pts, p => p.Faults) > 300 ? 1 : 0, "Memory", HwSpecs.Fmt(Avg(pts, p => p.Mem)) + "% in use · read back from disk up to " + HwSpecs.Fmt(Peak(pts, p => p.Faults)) + " pages/s", "", "m"));
                double perf = Avg(pts, p => p.Perf); if (!double.IsNaN(perf)) rows.Add(R(perf < 60 ? 1 : 0, "Processor speed", HwSpecs.Fmt(perf) + "% of its rated speed", "", "m"));
                double dpc = Peak(pts, p => p.Dpc); if (!double.IsNaN(dpc)) rows.Add(R(dpc > 15 ? 1 : 0, "Driver interrupts", "up to " + HwSpecs.Fmt(dpc) + "% of the processor", "", "m"));
                rows.Add(R(c.Who == "internet" || c.Who == "both" ? 1 : 0, "Internet", double.IsNaN(Avg(pts, p => p.Net)) ? "not measured" : HwSpecs.Fmt(Avg(pts, p => p.Net)) + " ms on average · " + pts.Count(p => p.Lost) + " lost replies · router " + HwSpecs.Fmt(Avg(pts, p => p.Gw)) + " ms", "", "m"));
                rows.Add(R(3, "Timeline (5 s steps)", Series(pts), "", "m"));
            }
            string pat = Pattern(); if (pat != "") rows.Add(R(3, "Pattern", pat, "", "m"));
            foreach (var e in Earlier().AsEnumerable().Reverse().Skip(c.Pts.Count >= 3 ? 1 : 0).Take(8))
            {
                DateTime t; DateTime.TryParse(e[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out t); int lv; if (!int.TryParse(e[2], out lv)) lv = 0;
                rows.Add(R(Math.Min(lv, 1), "Earlier: " + t.ToString("d MMM HH:mm", CultureInfo.CurrentCulture), e[1] == "pc" ? "this PC" : e[1] == "internet" ? "the internet" : e[1] == "both" ? "both" : "the site / app", "", "m"));
            }
            return rows;
        }
        // how often each side was to blame, over the saved presses - the pattern beats any single moment
        internal static string Pattern()
        {
            var e = Earlier(); if (e.Count < 3) return "";
            int pc = e.Count(x => x[1] == "pc" || x[1] == "both"), net = e.Count(x => x[1] == "internet" || x[1] == "both"), app = e.Count(x => x[1] == "app");
            return "Of the last " + e.Count + " times it felt slow: this PC " + pc + "×, the internet " + net + "×, the site / app " + app + "×.";
        }
    }
}
