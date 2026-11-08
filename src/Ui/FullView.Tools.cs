// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    // Health page: the on-demand tools - pre-call check, DNS speed test, VPN map, and what this PC lets AdeeboMeter see.
    sealed partial class FullView
    {
        string toolTitle = "", toolStatus = "", toolWhich = ""; DateTime toolAt; List<string[]> toolRows; volatile bool toolBusy;
        static string[] NoInputs() { return new[] { "", "", "", "" }; }
        void RunTool(string which, string title)
        {
            if (toolBusy) return;
            toolBusy = true; toolWhich = which; toolTitle = title; toolRows = null; toolStatus = "Starting..."; healthCanvas.Invalidate();
            var tbTool = which.StartsWith("tb:", StringComparison.Ordinal) ? System.Linq.Enumerable.FirstOrDefault(Tools2(), z => z.Id == which.Substring(3)) : null;   // a Toolbox PC check, run here
            Action<string> progress = delegate(string s) { toolStatus = s; try { BeginInvoke((Action)healthCanvas.Invalidate); } catch { } };
            Task.Factory.StartNew(() =>
            {
                List<string[]> res;
                try
                {
                    if (tbTool != null) { var run = new Toolkit.Run(); var pr = new System.Threading.Timer(delegate { if (run.Status != "") progress(run.Status); }, null, 300, 500); try { res = tbTool.Do(NoInputs(), run); } finally { pr.Dispose(); } }
                    else res = which == "precall" ? Tools.PreCall(M, progress) : which == "dns" ? Tools.DnsBench(M, progress) : which == "vpn" ? Tools.VpnMap(M) : Tools.Visibility(M);
                }
                catch (Exception e) { Program.Log("tool " + which + ": " + e); res = new List<string[]> { new[] { "2", "Couldn't finish", Draw.Trunc(e.Message, 60), "" } }; }
                try { BeginInvoke((Action)delegate { toolRows = res; toolAt = DateTime.Now; toolBusy = false; healthCanvas.Invalidate(); }); } catch { toolBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }
        float PaintToolResult(Graphics g, float x, float y, float w)
        {
            if (!toolBusy && toolRows == null) return y;
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(toolTitle.ToUpperInvariant() + (toolBusy ? "  ·  " + toolStatus : "  ·  DONE " + DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture)), Draw.fTiny, mb, x, y + 6 * S);
            y += 28 * S;
            if (toolRows == null) return y + 10 * S;
            float nameW = 250 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
                foreach (var row in toolRows)
                {
                    int lv; int.TryParse(row[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv);
                    string adv = row.Length > 3 ? row[3] : "";
                    float dh = Draw.Measure(row[2], Draw.fBodyB, (int)(w - nameW - 30 * S)).Height, ah = adv == "" ? 0 : Draw.Measure(adv, Draw.fBody, (int)(w - nameW - 30 * S)).Height;
                    float rh = Math.Max(36 * S, dh + ah + 16 * S);
                    using (var b = new SolidBrush(Theme.Lv(lv))) g.FillEllipse(b, x, y + 10 * S, 11 * S, 11 * S);
                    g.DrawString(row[1], Draw.fBodyB, ink, new RectangleF(x + 22 * S, y + 6 * S, nameW - 26 * S, rh));
                    { string ev = Evidence.For(toolWhich, row); float nh = Draw.Measure(row[1], Draw.fBodyB, (int)(nameW - 26 * S)).Height, ew = Evidence.Pill(g, ev, x + 22 * S, y + 8 * S + nh, toolAt); Hint(healthCanvas, new RectangleF(x + 22 * S, y + 6 * S + nh, ew, 20 * S), Evidence.Key(ev)); rh = Math.Max(rh, nh + 34 * S); }
                    using (var vb = new SolidBrush(lv == 3 ? Theme.Ink : Theme.LvText(lv))) g.DrawString(row[2], Draw.fBodyB, vb, new RectangleF(x + nameW, y + 6 * S, w - nameW, dh + 4));
                    if (adv != "") g.DrawString(adv, Draw.fBody, sub, new RectangleF(x + nameW, y + 8 * S + dh, w - nameW, ah + 4));
                    y += rh; g.DrawLine(line, x, y, x + w, y); y += 4 * S;
                }
            return y + 18 * S;
        }
    }
}
