// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        DateTime toolCopied, destCopied;
        string ToolText()
        {
            var sb = new System.Text.StringBuilder(); sb.AppendLine("AdeeboMeter " + Program.Version + " · " + toolTitle + " · " + toolAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            var rows = toolRows; if (rows != null) foreach (var r in rows) if (r.Length > 2) sb.AppendLine(r[1] + ": " + r[2] + (r.Length > 3 && r[3] != "" ? "  - " + r[3] : ""));
            return sb.ToString();
        }
        void SaveText(string name, string text)
        {
            foreach (var ch in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(ch, '-');
            using (var d = new SaveFileDialog { Title = "Save the result", Filter = "Text (*.txt)|*.txt", FileName = name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".txt" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { System.IO.File.WriteAllText(d.FileName, text, new System.Text.UTF8Encoding(true)); } catch (Exception e) { if (!(e is System.IO.IOException || e is UnauthorizedAccessException)) throw; Program.Log("save: " + e.Message); }
            }
        }
        float PaintToolResult(Graphics g, float x, float y, float w)
        {
            if (!toolBusy && toolRows == null) return y;
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(toolTitle.ToUpperInvariant() + (toolBusy ? "  ·  " + toolStatus : "  ·  DONE " + DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture)), Draw.fTiny, mb, x, y + 6 * S);
            y += 28 * S;
            if (toolRows == null) return y + 10 * S;
            {
                string ct = toolCopied > DateTime.Now.AddSeconds(-3) ? "Copied ✓" : "Copy as text", st = "Save..."; var cs = g.MeasureString(ct, Draw.fSmall); var ss = g.MeasureString(st, Draw.fSmall);
                var cr = new RectangleF(x + w - cs.Width, y - 26 * S, cs.Width, cs.Height); var sr = new RectangleF(cr.X - 14 * S - ss.Width, cr.Y, ss.Width, ss.Height);
                using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) { g.DrawString(ct, Draw.fSmall, lb, cr.X, cr.Y); g.DrawString(st, Draw.fSmall, lb, sr.X, sr.Y); }
                Act(healthCanvas, cr, delegate { try { Clipboard.SetText(ToolText()); toolCopied = DateTime.Now; } catch (Exception ce) { Program.Log("copy: " + ce.Message); } healthCanvas.Invalidate(); });
                Act(healthCanvas, sr, delegate { SaveText("AdeeboMeter-" + toolWhich.Replace("tb:", "").Replace(':', '-'), ToolText()); });
            }
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
                    if (row.Length > 5 && row[5].StartsWith("fix:", StringComparison.Ordinal))
                    {
                        // a hand-off: opens the Windows screen where you act; AdeeboMeter changes nothing
                        var ls = g.MeasureString("Open ›", Draw.fSmall); var lr = new RectangleF(x + w - ls.Width, y + 8 * S, ls.Width, ls.Height); string f = row[5].Substring(4);
                        using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString("Open ›", Draw.fSmall, lb, lr.X, lr.Y); Act(healthCanvas, lr, delegate { SecFix(f); });
                    }
                    y += rh; g.DrawLine(line, x, y, x + w, y); y += 4 * S;
                }
            return y + 18 * S;
        }
    }
}
