// The speed rules that fail the build (PLAN 11.8a): every page draws in under 30 ms in both layouts and both themes,
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
                            times.Add(t + " " + best.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                            if (best > limitMs) bad.Add(lay + "/" + (th == 0 ? "light" : "dark") + "/" + t + " " + best.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ms");
                        }
                        log.AppendLine("      draw ms (" + lay + ", " + (th == 0 ? "light" : "dark") + "): " + string.Join(" · ", times));
                    }
            }
            finally { Cfg.Layout = layout0; ApplyLayout(); Cfg.SetTheme(theme0); }
            return string.Join("; ", bad);
        }
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
