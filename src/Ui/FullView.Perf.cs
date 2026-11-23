// The speed rules that fail the build (PLAN 11.8a): every page draws in under 50 ms (x3 on a busy PC) in both layouts and both themes,
// the window answers within 100 ms while heavy tools start (their work left the window thread), and the freeze card
// appears when the window is stuck and goes away when it answers.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AdeeboMeter
{
    partial class FullView
    {
        internal string PerfCheck(StringBuilder log, double limitMs)
        {
            var bad = new List<string>(); string layout0 = Cfg.Layout; int theme0 = Cfg.GetTheme();
            try
            {
                foreach (var lay in new[] { "classic", "sidebar" })
                    foreach (var th in new[] { 0, 1 })
                    {
                        Cfg.Layout = lay; ApplyLayout(); Cfg.SetTheme(th); Application.DoEvents();
                        var times = new List<string>();
                        foreach (var t in tabNames.Keys.ToList())
                        {
                            tab = t; ShowTab(); Application.DoEvents(); leftPane.Refresh();   // first paint: caches, fonts, layout
                            double best = double.MaxValue;
                            for (int i = 0; i < 3; i++) { var sw = Stopwatch.StartNew(); leftPane.Refresh(); best = Math.Min(best, sw.Elapsed.TotalMilliseconds); }
                            // 22 Nov (Adeeb's PC: security 59 ms once, 24-29 ms in the other three layouts): a short burst elsewhere on the PC
                            // can slow three draws in a row - over the limit, wait half a second and measure again; a slow page is slow both times
                            // R2-143: the second look is judged on its own typical draw (the median of 5), not the fastest of all six
                            if (best > limitMs) { System.Threading.Thread.Sleep(500); Application.DoEvents(); var again = new List<double>(); for (int i = 0; i < 5; i++) { var sw = Stopwatch.StartNew(); leftPane.Refresh(); again.Add(sw.Elapsed.TotalMilliseconds); } again.Sort(); best = again[2]; }
                            times.Add(t + " " + best.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                            if (best > limitMs) bad.Add(lay + "/" + (th == 0 ? "light" : "dark") + "/" + t + " " + best.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ms");
                        }
                        log.AppendLine("      draw ms (" + lay + ", " + (th == 0 ? "light" : "dark") + "): " + string.Join(" · ", times));
                    }
            }
            finally { Cfg.Layout = layout0; ApplyLayout(); Cfg.SetTheme(theme0); }
            return string.Join("; ", bad);
        }
        // --perf (23 Nov, Adeeb's PC: 50-150 ms pages that drew in 25 ms on 22 Nov): where a page's draw time goes. A sampler
        // stops the window thread every ~2 ms while the page is drawn again and again, and counts the AdeeboMeter method it was in
        // and what that method had called (DrawString, MeasureString, a LINQ step...) - the top ones per page, for the slow pages.
        #pragma warning disable 618   // Thread.Suspend / StackTrace(Thread): obsolete, still the only way in .NET Framework, fine for a diagnostic run
        internal string PerfProfile(double overMs)
        {
            var log = new StringBuilder(); string layout0 = Cfg.Layout; int theme0 = Cfg.GetTheme(); var ui = System.Threading.Thread.CurrentThread;
            log.AppendLine("WHERE THE TIME GOES (pages over " + overMs + " ms; samples every ~2 ms; 'AdeeboMeter method  <-  what it was calling')");
            try
            {
                foreach (var lay in new[] { "classic", "sidebar" })
                    foreach (var th in new[] { 0, 1 })
                    {
                        Cfg.Layout = lay; ApplyLayout(); Cfg.SetTheme(th); Application.DoEvents();
                        foreach (var t in tabNames.Keys.ToList())
                        {
                            tab = t; ShowTab(); Application.DoEvents(); leftPane.Refresh();
                            var sw0 = Stopwatch.StartNew(); for (int i = 0; i < 3; i++) leftPane.Refresh(); double avg = sw0.Elapsed.TotalMilliseconds / 3;
                            if (avg < overMs) continue;
                            var counts = new Dictionary<string, int>(); int total = 0; bool on = true;
                            var sampler = new System.Threading.Thread(() =>
                            {
                                while (on)
                                {
                                    System.Diagnostics.StackTrace st = null;
                                    try { ui.Suspend(); try { st = new System.Diagnostics.StackTrace(ui, false); } finally { ui.Resume(); } }
                                    catch (System.Threading.ThreadStateException) { }
                                    if (st != null)
                                    {
                                        string ours = null, leaf = null;
                                        for (int f = 0; f < st.FrameCount && f < 60; f++)
                                        {
                                            var m = st.GetFrame(f).GetMethod(); if (m == null || m.DeclaringType == null) continue;
                                            string nm = m.DeclaringType.Name + "." + m.Name;
                                            if (leaf == null) leaf = nm;
                                            if (m.DeclaringType.Namespace == "AdeeboMeter" && !nm.StartsWith("FullView.PerfProfile", StringComparison.Ordinal) && !nm.Contains("<")) { ours = nm; break; }
                                            if (m.DeclaringType.Namespace == "AdeeboMeter" && ours == null && nm.Contains("<")) { ours = nm; break; }
                                        }
                                        string k = (ours ?? "?") + "  <-  " + (leaf ?? "?"); lock (counts) { int c0; counts.TryGetValue(k, out c0); counts[k] = c0 + 1; total++; }
                                    }
                                    System.Threading.Thread.Sleep(2);
                                }
                            }) { IsBackground = true };
                            sampler.Start();
                            var sw = Stopwatch.StartNew(); int n = 0; while (sw.ElapsedMilliseconds < 1500) { leftPane.Refresh(); n++; }
                            on = false; sampler.Join(1000);
                            log.AppendLine(lay + "/" + (th == 0 ? "light" : "dark") + "/" + t + ": " + avg.ToString("0") + " ms a draw (" + n + " draws, " + total + " samples)");
                            lock (counts) foreach (var kv in counts.OrderByDescending(z => z.Value).Take(10)) log.AppendLine("    " + (100.0 * kv.Value / Math.Max(1, total)).ToString("0").PadLeft(3) + "%  " + kv.Key);
                        }
                    }
            }
            finally { Cfg.Layout = layout0; ApplyLayout(); Cfg.SetTheme(theme0); }
            return log.ToString();
        }
        #pragma warning restore 618
        // starting a heavy tool must hand its work to a worker: the call returns fast and the window keeps answering
        internal string StartCheck(StringBuilder log, double limitMs)
        {
            var bad = new List<string>(); var times = new List<string>();
            tab = "toolbox"; ShowTab(); EnsureToolbox(); Application.DoEvents();
            foreach (var id in new[] { "space", "drivers", "winev", "certs", "listen", "pressure", "subnet", "units" })
            {
                if (!Tools2().Any(t => t.Id == id)) continue;
                SelectTool(id); Application.DoEvents();
                var sw = Stopwatch.StartNew(); StartTool(); double ms = sw.Elapsed.TotalMilliseconds;
                // the window answers while it runs
                var sw2 = Stopwatch.StartNew(); Application.DoEvents(); double ans = sw2.Elapsed.TotalMilliseconds;
                times.Add(id + " " + ms.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "/" + ans.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                if (ms > limitMs || ans > limitMs) bad.Add(id + " took " + ms.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ms to start, " + ans.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ms to answer");
                var until = DateTime.UtcNow.AddSeconds(30); while (tbBusy && DateTime.UtcNow < until) { if (tbRun0 != null) tbRun0.Stop = true; Application.DoEvents(); System.Threading.Thread.Sleep(20); }
            }
            log.AppendLine("      start/answer ms: " + string.Join(" · ", times));
            return string.Join("; ", bad);
        }
        // block the window thread for 3.5 s: the card must appear (its own thread), then go once the window answers
        internal static string FreezeCheck(StringBuilder log)
        {
            var until = DateTime.UtcNow.AddSeconds(2.5); while (DateTime.UtcNow < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }   // a pong, so the watchdog knows where the window is
            int before = FreezeCard.Shown;
            System.Threading.Thread.Sleep(3500);   // the window thread is "stuck"
            bool shown = FreezeCard.Shown > before;
            until = DateTime.UtcNow.AddSeconds(2.5); while (DateTime.UtcNow < until && FreezeCard.Visible) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
            log.AppendLine("      freeze card: " + (shown ? "appeared" : "did NOT appear") + ", " + (FreezeCard.Visible ? "still showing" : "gone after the window answered"));
            return !shown ? "the freeze card didn't appear" : FreezeCard.Visible ? "the freeze card stayed after the window answered" : "";
        }
    }
}
