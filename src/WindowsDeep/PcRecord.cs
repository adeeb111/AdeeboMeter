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
            public DateTime T; public long Mono; public double DiskMBs = double.NaN, Commit = double.NaN, Cpu, Busy, DiskMs, Faults, Perf, Dpc, Net, Gw, Down, Up, Mem; public bool Lost; public string TopCpu = "", TopIo = ""; public double TopCpuPct;
        }
        internal sealed class Capture { public DateTime T; public string Who = "", Text = "", Cause = "", Culprit = "", Fix = ""; public int Lv; public List<Pt> Pts = new List<Pt>(); }
        static readonly List<Pt> ring = new List<Pt>(); static readonly object k = new object();
        static DateTime lastNetAt, lastGwAt;
        // a steady clock for keeping and windowing samples: moving the wall clock back can't keep old samples "in the future" (PC-008)
        static long MonoNow() { return System.Diagnostics.Stopwatch.GetTimestamp(); }
        static double MonoSecs(long a, long b) { return (b - a) / (double)System.Diagnostics.Stopwatch.Frequency; }

        // from Brain's PC job, every 5 s (15 s on Penny)
        public static void Add(Monitor m, Dictionary<string, double> pv)
        {
            var p = new Pt { T = DateTime.Now, Mono = MonoNow(), Cpu = m.Cpu.Last, Mem = m.Mem.Last, Down = m.Down.Last, Up = m.Up.Last };
            // PC-006: a network reading counts once - at the sample after its probe - not again at every PC tick until the next probe
            DateTime nat = m.NetAt, gat = m.GwAt; bool freshNet = nat != lastNetAt, freshGw = gat != lastGwAt; lastGwAt = gat;
            p.Net = freshNet ? m.NetPing.Last : double.NaN; p.Gw = freshGw ? m.GwPing.Last : double.NaN;
            double v; p.DiskMs = pv.TryGetValue("diskms", out v) ? v : double.NaN; p.Faults = pv.TryGetValue("faults", out v) ? v : double.NaN; p.Perf = pv.TryGetValue("perf", out v) ? v : double.NaN;
            double i1, i2; p.Dpc = pv.TryGetValue("dpc", out i1) && pv.TryGetValue("intr", out i2) && !double.IsNaN(i1) && !double.IsNaN(i2) ? i1 + i2 : double.NaN;
            p.Busy = pv.TryGetValue("idle", out v) && !double.IsNaN(v) ? Math.Max(0, Math.Min(100, 100 - v)) : double.NaN;
            if (pv.TryGetValue("dbytes", out v) && !double.IsNaN(v)) p.DiskMBs = v / 1048576;
            double cm, lim; if (pv.TryGetValue("commit", out cm) && pv.TryGetValue("limit", out lim) && lim > 0 && !double.IsNaN(cm)) p.Commit = 100 * cm / lim;
            p.Lost = freshNet && double.IsNaN(m.NetPing.Last) && m.NetPing.Values(60).Length > 0; lastNetAt = nat;
            lock (m.L) { var top = m.Apps.Where(a => a.Name != "AdeeboMeter" && a.Name != "Idle" && a.Name != "_Total").OrderByDescending(a => a.Cpu).FirstOrDefault(); if (top != null) { p.TopCpu = top.Name; p.TopCpuPct = top.Cpu; } }
            p.TopIo = PcHealth.TopIo();
            lock (k) { ring.Add(p); while (ring.Count > 0 && MonoSecs(ring[0].Mono, p.Mono) > 15 * 60) ring.RemoveAt(0); }
        }
        internal static List<Pt> Last(double seconds) { long now = MonoNow(); lock (k) return ring.Where(p => p.Mono == 0 ? (DateTime.Now - p.T).TotalSeconds <= seconds : MonoSecs(p.Mono, now) <= seconds).ToList(); }

        // ---------- the verdict over a window of samples
        static double Share(List<Pt> pts, Func<Pt, bool> f) { var n = pts.Count; return n == 0 ? 0 : (double)pts.Count(f) / n; }
        static double Peak(List<Pt> pts, Func<Pt, double> f) { var x = pts.Select(f).Where(z => !double.IsNaN(z)).ToList(); return x.Count == 0 ? double.NaN : x.Max(); }
        static double Avg(List<Pt> pts, Func<Pt, double> f) { var x = pts.Select(f).Where(z => !double.IsNaN(z)).ToList(); return x.Count == 0 ? double.NaN : x.Average(); }
        static string Most(IEnumerable<string> names) { return names.Where(n => !string.IsNullOrEmpty(n)).GroupBy(n => n).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? ""; }
        internal static Capture Judge(List<Pt> pts, DateTime at)
        {
            var c = new Capture { T = at, Pts = pts };
            if (pts.Count < 3) { c.Who = "unknown"; c.Lv = 3; c.Text = "Not enough of a record yet - AdeeboMeter keeps the last minutes once it has run for a little while. Press it again next time it feels slow."; return c; }
            // PC-003: memory pressure is a full memory; pages read back only count with it (on their own they're often files being opened)
            double memS = Share(pts, p => p.Mem > 90 || p.Commit > 92), pageS = Share(pts, p => p.Faults > 300 && (p.Mem > 80 || p.Commit > 85)), cpuS = Share(pts, p => p.Cpu > 90);
            Func<Pt, bool> diskBusy = p => p.Busy > 90 || p.DiskMs > 50;
            double diskS = Share(pts, diskBusy), heldS = Share(pts, p => !double.IsNaN(p.Perf) && p.Perf < 60 && p.Cpu > 50), dpcS = Share(pts, p => p.Dpc > 15);
            // PC-004: the internet and the router are judged each against its own readings
            int inetN = pts.Count(p => !double.IsNaN(p.Net) || p.Lost), gwN = pts.Count(p => !double.IsNaN(p.Gw)), lost = pts.Count(p => p.Lost);
            double inetS = inetN == 0 ? 0 : (double)pts.Count(p => p.Lost || p.Net > 150) / inetN, gwS = gwN == 0 ? 0 : (double)pts.Count(p => p.Gw > 50) / gwN;
            bool netKnown = inetN > 0 || gwN > 0, netBad = inetS >= 0.25 || gwS >= 0.25, near = gwS >= 0.25 && gwS >= inetS;
            var pc = new List<Tuple<double, string, string, string, double>>();   // share, cause, culprit, fix, tie-break
            if (memS > 0 || pageS > 0) pc.Add(Tuple.Create(Math.Max(pageS, memS), "memory was " + HwSpecs.Fmt(Peak(pts, p => p.Mem)) + "% full" + (Peak(pts, p => p.Commit) > 92 ? " (" + HwSpecs.Fmt(Peak(pts, p => p.Commit)) + "% of what Windows promised programs)" : "") + " - Windows swaps programs out to the disk and back" + (double.IsNaN(Peak(pts, p => p.Faults)) ? "" : " (" + HwSpecs.Fmt(Peak(pts, p => p.Faults)) + " pages/s read back at worst)"), "", "close the heaviest app (Hardware nerds > Why is this PC struggling? shows how much each one frees), or add memory", 0.05));
            // PC-007: the program doing the disk work while the disk was struggling, not across the whole two minutes
            if (diskS > 0) pc.Add(Tuple.Create(diskS, "the disk was " + (Peak(pts, p => p.Busy) >= 99 ? "100%" : HwSpecs.Fmt(Peak(pts, p => p.Busy)) + "%") + " busy, up to " + HwSpecs.Fmt(Peak(pts, p => p.DiskMs)) + " ms per request", Most(pts.Where(diskBusy).Select(p => p.TopIo)), "let it finish, or move that job to the night (Hardware nerds > Disk speed shows if the disk itself is slow)", 0.0));
            if (cpuS > 0) pc.Add(Tuple.Create(cpuS, "the processor was maxed out (" + HwSpecs.Fmt(Peak(pts, p => p.Cpu)) + "%)", Most(pts.Where(p => p.Cpu > 90).Select(p => p.TopCpu)), "close or pause that program", 0.0));
            if (heldS > 0) pc.Add(Tuple.Create(heldS, "the processor was held back to " + HwSpecs.Fmt(Avg(pts.Where(p => !double.IsNaN(p.Perf) && p.Perf < 60).ToList(), p => p.Perf)) + "% of its speed (heat, battery saver or the power plan)", "", "plug in, clear the vents, and pick Best performance in Settings > Power", 0.0));
            if (dpcS > 0) pc.Add(Tuple.Create(dpcS, "a driver kept the processor busy (" + HwSpecs.Fmt(Peak(pts, p => p.Dpc)) + "% on interrupts) - stutter, crackling sound, mouse lag", "", "update the Wi-Fi, audio and graphics drivers", 0.0));
            // the line first, then the order: a nudge can't lift a part over the line (PC-003)
            var best = pc.Where(t => t.Item1 >= 0.25).OrderByDescending(t => t.Item1 + t.Item5).FirstOrDefault(); bool pcBad = best != null;
            bool pcBlind = Share(pts, p => double.IsNaN(p.Busy) && double.IsNaN(p.Faults) && double.IsNaN(p.Mem) && double.IsNaN(p.Cpu)) > 0.5;
            string when = at.ToString("t", CultureInfo.CurrentCulture);
            // PC-005: replies that never came are loss, not "? ms"
            double netAvg = Avg(pts, p => p.Net);
            string netNow = !netKnown ? "the internet wasn't measured (the background checks are off)" : inetN == 0 ? "the internet wasn't measured, the router answered in " + HwSpecs.Fmt(Avg(pts, p => p.Gw)) + " ms"
                : double.IsNaN(netAvg) ? "the internet didn't answer at all (" + lost + " lost replies)" : "the internet answered in " + HwSpecs.Fmt(netAvg) + " ms" + (lost > 0 ? " with " + lost + " lost repl" + (lost == 1 ? "y" : "ies") : "");
            string pcFine = pcBlind ? "This PC's own counters weren't readable, so I can't clear the PC." : "The PC itself was fine: processor " + HwSpecs.Fmt(Avg(pts, p => p.Cpu)) + "%, disk " + HwSpecs.Fmt(Avg(pts, p => p.Busy)) + "% busy.";
            if (pcBad)
            {
                c.Cause = best.Item2; c.Culprit = best.Item3; c.Fix = best.Item4; c.Lv = best.Item1 >= 0.5 ? 2 : 1;
                c.Who = netBad ? "both" : "pc";
                // PC-001: an unmeasured internet isn't cleared
                c.Text = "At " + when + ": " + (netBad ? "both this PC and the internet. " : netKnown ? "this PC, not the internet. " : "this PC. ") + Up(c.Cause) + (c.Culprit != "" ? " - mostly " + c.Culprit : "") + ". " + (netBad ? "The internet was slow too (" + netNow + ")." : Up(netNow) + ".") + " Try: " + c.Fix + ".";
            }
            else if (netBad)
            {
                c.Who = "internet"; c.Lv = Math.Max(inetS, gwS) >= 0.5 ? 2 : 1;
                c.Cause = near ? "your router / Wi-Fi was slow to answer (" + HwSpecs.Fmt(Avg(pts, p => p.Gw)) + " ms)" : double.IsNaN(netAvg) ? "the internet beyond your router didn't answer (" + lost + " lost replies)" : "the internet beyond your router was slow or dropped replies (" + HwSpecs.Fmt(netAvg) + " ms" + (lost > 0 ? ", " + lost + " lost" : "") + ")";
                c.Text = "At " + when + ": the internet" + (pcBlind ? "" : ", not this PC") + ". " + Up(c.Cause) + ". " + pcFine;   // PC-002: only cleared when it was read
                c.Fix = near ? "move closer to the router or use a cable" : "if it keeps happening, the Report for my ISP shows it with numbers";
            }
            else if (pcBlind)
            {
                c.Who = "unknown"; c.Lv = 3;
                c.Text = "At " + when + ": " + Up(netNow) + ", but this PC's own counters (memory, disk) weren't readable for most of the 2 minutes - so I can't clear the PC. Press it again; if it keeps saying this, Windows' performance counters are switched off.";
            }
            else if (!netKnown)
            {   // PC-001: the PC was fine; the internet is unknown, so it can't be put on the site or app either
                c.Who = "notpc"; c.Lv = 3;
                c.Text = "At " + when + ": not this PC - " + pcFine.Substring(pcFine.IndexOf(':') + 2).TrimEnd('.') + ". The internet wasn't measured (the background checks are off), so it could be the internet or the site / app.";
                c.Fix = "turn the background checks on (Settings) so next time the internet is measured too";
            }
            else
            {
                c.Who = "app"; c.Lv = 1;
                c.Text = "At " + when + ": not this PC and not the internet - both were fine (processor " + HwSpecs.Fmt(Avg(pts, p => p.Cpu)) + "%, disk " + HwSpecs.Fmt(Avg(pts, p => p.Busy)) + "% busy, " + netNow + "). The slowness is most likely the site or app itself - its servers, or the app's own work.";
                c.Fix = "try another site or app: if only this one is slow, it's on their side";
            }
            return c;
        }
        internal static string WhoText(string who) { return who == "pc" ? "this PC" : who == "both" ? "this PC and the internet" : who == "internet" ? "the internet / your router" : who == "app" ? "the site or app itself" : who == "notpc" ? "not this PC - the internet wasn't measured" : "not sure"; }   // PC-010
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
            lock (fk) { lastTry = c; if (c.Pts.Count >= 3) lastCap = c; }
            return c;
        }
        // PC-012: the last press and the last press with a full record are kept apart, so the result view shows what was just pressed
        static Capture lastCap, lastTry;
        internal static Capture Latest { get { lock (fk) return lastCap; } }
        internal static Capture LastTry { get { lock (fk) return lastTry; } }
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
            sb.Append(" ; Memory ").Append(string.Join(" ", take.Select(p => n(p.Mem)).ToArray()));
            sb.Append(" ; Ping ").Append(string.Join(" ", take.Select(p => p.Lost ? "x" : n(p.Net)).ToArray()));
            if (take.Count > 0) sb.Append(" ; Seconds ").Append(string.Join(" ", take.Select(p => n((p.T - take[0].T).TotalSeconds)).ToArray()));   // PC-009: when each reading was taken
            return sb.ToString();
        }

        // ---------- the tool: the last time it felt slow (or right now, if never pressed)
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        internal static List<string[]> Rows(bool now)
        {
            var rows = new List<string[]>();
            var c = now ? FeltSlow() : (LastTry ?? FeltSlow());
            rows.Add(R(c.Lv, "Verdict", c.Text, c.Fix == "" ? "" : "Try: " + c.Fix, c.Pts.Count >= 3 ? "m" : "u"));
            var full = Latest; if (c.Pts.Count < 3 && full != null) rows.Add(R(3, "Last full record", full.T.ToString("d MMM HH:mm", CultureInfo.CurrentCulture) + ": " + full.Text, "", "m"));
            if (c.Pts.Count >= 3)
            {
                var pts = c.Pts;
                rows.Add(R(c.Who == "pc" || c.Who == "both" ? 2 : c.Who == "unknown" || c.Who == "notpc" ? 3 : 0, "Who", WhoText(c.Who), "", "i"));
                // PC-011: a part with no readable sample is unknown (3), never green
                Func<double, bool, int> lvOf = (pk, bad) => double.IsNaN(pk) ? 3 : bad ? 1 : 0;
                rows.Add(R(lvOf(Peak(pts, p => p.Cpu), Peak(pts, p => p.Cpu) > 90), "Processor", "average " + HwSpecs.Fmt(Avg(pts, p => p.Cpu)) + "% · peak " + HwSpecs.Fmt(Peak(pts, p => p.Cpu)) + "%" + (Most(pts.Select(p => p.TopCpu)) != "" ? " · busiest: " + Most(pts.Select(p => p.TopCpu)) : ""), "", "m"));
                rows.Add(R(double.IsNaN(Peak(pts, p => p.Busy)) && double.IsNaN(Peak(pts, p => p.DiskMs)) ? 3 : Peak(pts, p => p.Busy) > 90 || Peak(pts, p => p.DiskMs) > 50 ? 1 : 0, "Disk", "busy " + HwSpecs.Fmt(Avg(pts, p => p.Busy)) + "% on average · up to " + HwSpecs.Fmt(Peak(pts, p => p.DiskMs)) + " ms per request" + (Most(pts.Select(p => p.TopIo)) != "" ? " · busiest: " + Most(pts.Select(p => p.TopIo)) : ""), "", "m"));
                rows.Add(R(double.IsNaN(Peak(pts, p => p.Faults)) && double.IsNaN(Peak(pts, p => p.Mem)) ? 3 : Peak(pts, p => p.Faults) > 300 || Peak(pts, p => p.Mem) > 90 ? 1 : 0, "Memory", HwSpecs.Fmt(Avg(pts, p => p.Mem)) + "% in use (peak " + HwSpecs.Fmt(Peak(pts, p => p.Mem)) + "%) · read back from disk up to " + HwSpecs.Fmt(Peak(pts, p => p.Faults)) + " pages/s", "", "m"));
                double perf = Avg(pts, p => p.Perf); if (!double.IsNaN(perf)) rows.Add(R(perf < 60 ? 1 : 0, "Processor speed", HwSpecs.Fmt(perf) + "% of its rated speed", "", "m"));
                double dpc = Peak(pts, p => p.Dpc); if (!double.IsNaN(dpc)) rows.Add(R(dpc > 15 ? 1 : 0, "Driver interrupts", "up to " + HwSpecs.Fmt(dpc) + "% of the processor", "", "m"));
                int lostN = pts.Count(p => p.Lost); double netA = Avg(pts, p => p.Net), gwA = Avg(pts, p => p.Gw);
                string inet = !double.IsNaN(netA) ? HwSpecs.Fmt(netA) + " ms on average · " + lostN + " lost replies" : lostN > 0 ? "no replies - " + lostN + " lost" : "not measured";   // PC-005
                rows.Add(R(c.Who == "internet" || c.Who == "both" ? 1 : double.IsNaN(netA) && lostN == 0 && double.IsNaN(gwA) ? 3 : 0, "Internet", inet + (double.IsNaN(gwA) ? "" : " · router " + HwSpecs.Fmt(gwA) + " ms"), "", "m"));
                // PC-009: the real spacing - 5 s normally, 15 s on Penny, longer if the PC was asleep
                var gaps = pts.Zip(pts.Skip(1), (a, b) => (b.T - a.T).TotalSeconds).Where(g => g > 0).OrderBy(g => g).ToList(); double step = gaps.Count > 0 ? gaps[gaps.Count / 2] : 5;
                rows.Add(R(3, "Timeline (about " + HwSpecs.Fmt(step) + " s steps)", Series(pts), "", "m"));
                rows.Add(R(3, "Judged on", pts.First().T.ToString("T", CultureInfo.CurrentCulture) + " - " + pts.Last().T.ToString("T", CultureInfo.CurrentCulture) + " · " + pts.Count + " readings · a part counts as the cause when it's over its line for a quarter of them: processor over 90%, memory over 90% full, more than 300 pages/s read back, disk over 90% busy or 50 ms per request, processor below 60% of its speed while busy, drivers over 15%; the internet: over 150 ms, the router over 50 ms, or lost replies", "", "i"));
            }
            string pat = Pattern(); if (pat != "") rows.Add(R(3, "Pattern", pat, "", "m"));
            foreach (var e in Earlier().AsEnumerable().Reverse().Skip(c.Pts.Count >= 3 ? 1 : 0).Take(8))
            {
                DateTime t; DateTime.TryParse(e[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out t); int lv; if (!int.TryParse(e[2], out lv)) lv = 0;
                rows.Add(R(Math.Min(lv, e[1] == "unknown" || e[1] == "notpc" ? 3 : 1), "Earlier: " + t.ToString("d MMM HH:mm", CultureInfo.CurrentCulture), WhoText(e[1]), "", "m"));
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
