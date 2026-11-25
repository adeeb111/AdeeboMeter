// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AdeeboMeter
{
    // --view tour (24 Nov, for the content creators' OBS recordings): every Toolbox tool in turn, as a person would use it -
    // open it, fill the Example's inputs, run it, scroll the result to the end, hold a few seconds, next. Tools that send
    // test traffic, touch files or the company's servers, or need a password (the deep test's by-hand list) are never run:
    // their Example result is shown instead. A chapter list (tour-chapters.csv beside the exe) gives each tool's start time,
    // so one recording can be cut into a video per tool or kept as one demo.
    //   AdeeboMeter.exe --view tour                 every tool from the first
    //   AdeeboMeter.exe --view tour:mtr             from MTR on
    //   AdeeboMeter.exe --view tour --hold 8        8 s on each finished result (default 5)
    //   AdeeboMeter.exe --view tour:mtr --count 1   just MTR (redo one tool)
    // Esc (back to the corner) or closing the window ends the tour. One timer drives it and repaints only the Toolbox.
    sealed partial class FullView
    {
        System.Windows.Forms.Timer tourT;
        List<Tool> tourList;
        int tourI, tourStep, tourPos;
        bool tourReal;
        Stopwatch tourClock, tourStepClock;
        StringBuilder tourCsv;

        void DemoTour(string from)
        {
            tab = "toolbox"; ShowTab(); EnsureToolbox();
            tourList = Tools2().Where(t => SectionOf(t) != "health").ToList();
            tourI = 0;
            if (!string.IsNullOrEmpty(from)) { int at = tourList.FindIndex(t => t.Id == from); if (at >= 0) tourI = at; else Program.Log("tour: no tool called \"" + from + "\" - starting from the first"); }
            if (Program.TourCount > 0) tourList = tourList.Skip(tourI).Take(Program.TourCount).ToList(); else tourList = tourList.Skip(tourI).ToList();
            tourI = 0; tourStep = 0;
            tourCsv = new StringBuilder("n,start,clock,tool,title,shown\r\n");   // start: from the tour's start; clock: the time of day, to line up with a recording file
            tourClock = Stopwatch.StartNew(); tourStepClock = Stopwatch.StartNew();
            Program.Log("tour: " + tourList.Count + " tools, " + Program.TourHoldMs / 1000 + " s on each result");
            tourT = new System.Windows.Forms.Timer { Interval = 50 };
            tourT.Tick += delegate { TourTick(); };
            tourT.Start();
        }

        void TourEnd(string why)
        {
            if (tourT == null) return;
            tourT.Stop(); tourT.Dispose(); tourT = null;
            try { File.WriteAllText(Path.Combine(Program.WriteDir, "tour-chapters.csv"), tourCsv.ToString(), Encoding.UTF8); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("tour: chapters - " + e.Message); }
            Program.Log("tour: " + why + " after " + (int)tourClock.Elapsed.TotalSeconds + " s - chapters in tour-chapters.csv");
        }

        static string Csv(string s) { s = s ?? ""; return s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s; }

        void TourTick()
        {
            if (IsDisposed || !Visible) { TourEnd("stopped (the window closed)"); return; }
            if (tourI >= tourList.Count) { TourEnd("finished"); return; }
            var t = tourList[tourI]; long ms = tourStepClock.ElapsedMilliseconds;
            switch (tourStep)
            {
                case 0:   // open the tool and note where its chapter starts
                    SelectTool(t.Id);
                    tourReal = t.Do != null && !ByHand.Contains(t.Id) && !t.Inputs.Any(SecretInput);
                    var at = tourClock.Elapsed;
                    tourCsv.Append((tourI + 1).ToString(CultureInfo.InvariantCulture)).Append(',').Append(((int)at.TotalHours).ToString("00", CultureInfo.InvariantCulture)).Append(at.ToString(@"\:mm\:ss", CultureInfo.InvariantCulture)).Append(',').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(',')
                           .Append(Csv(t.Id)).Append(',').Append(Csv(AdeeboMeter.L.T(t.Title))).Append(',').Append(tourReal ? "run" : t.Do == null ? "worked out" : "example").Append("\r\n");
                    Next(); break;
                case 1:   // a moment on the empty tool, then its example's inputs in the boxes
                    if (ms < 1500) break;
                    if (Examples.Get(t.Id) != null && t.Inputs.Length > 0) FillExample(t.Id);
                    if (t.Do == null) { tourPos = 0; tourStep = 4; tourStepClock.Restart(); break; }   // a calculator: filling it is the result
                    Next(); break;
                case 2:   // run it for real, or show its example result
                    if (ms < 900) break;
                    if (tourReal) { StartTool(); Next(); }
                    else { ShowExample(); tourPos = 0; tourStep = 4; tourStepClock.Restart(); }
                    break;
                case 3:   // wait for the run; a slow one is asked to stop at 60 s
                    if (tbBusy && ms > 60000 && tbRun0 != null) tbRun0.Stop = true;
                    if (tbBusy && ms < 75000) break;
                    tourPos = 0; Next(); break;
                case 4:   // a second on the top of the result, then scroll to its end at a reading pace
                    if (ms < 1200) break;
                    int max = Math.Max(0, tbCanvas.DisplayRectangle.Height - tbCanvas.ClientSize.Height);
                    if (tourPos < max) { tourPos = Math.Min(max, tourPos + Sc(12)); tbCanvas.AutoScrollPosition = new Point(0, tourPos); tbCanvas.Invalidate(); break; }
                    Next(); break;
                default:  // hold the finished result, then the next tool from its top
                    if (ms < Program.TourHoldMs) break;
                    tbCanvas.AutoScrollPosition = Point.Empty; tourI++; tourStep = 0; tourStepClock.Restart(); break;
            }
        }

        void Next() { tourStep++; tourStepClock.Restart(); }
    }
}
