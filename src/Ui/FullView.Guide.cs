// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Investigate > Investigate a problem: pick the problem, AdeeboMeter runs the checks and answers
    // Finding · What I observed (with where each fact came from) · Not established · Next test.
    sealed partial class FullView
    {
        Panel guidePage; DB guideCanvas; RoundBox guideBox; GuideRun guideRun; volatile bool guideBusy;

        Panel BuildGuidePage()
        {
            guidePage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            guideBox = new RoundBox(Sc(320), Draw.fInput, "Site, app or server - e.g. outlook.office.com");
            guideBox.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; StartGuide("site", false); } };
            bar.Controls.Add(guideBox);
            bar.Controls.Add(new Label { Text = Voice.P("Pick what's wrong - I'll run the checks and tell you what I saw, what I can't know, and what to try next.", "Choose the problem. AdeeboMeter runs the checks and reports findings, evidence, limits and next steps."), AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fSmall, Margin = new Padding(Sc(10), Sc(10), 0, 0) });
            guideCanvas = new DB { Name = "guideCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            guideCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintGuide(e.Graphics); }; Wire(guideCanvas);
            guidePage.Controls.Add(guideCanvas); guidePage.Controls.Add(bar);
            return guidePage;
        }

        // test = run right here on this thread (self-test; only the flows that send nothing new)
        void StartGuide(string id, bool test)
        {
            if (guideBusy) return;
            var r = new GuideRun { Input = guideBox.Box.Text };
            guideRun = r; guideCanvas.AutoScrollPosition = Point.Empty;
            if (test) { if (id == "slow" || id == "drops") Investigate.Run(id, M, r, delegate { }); else { r.Id = id; r.Title = Investigate.All.First(f => f.Id == id).Title; r.Conclude(3, "(self-test: not run - it would send traffic)"); r.Done = true; } guideCanvas.Invalidate(); return; }
            guideBusy = true; guideCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            {
                Investigate.Run(id, M, r, delegate { try { BeginInvoke((Action)guideCanvas.Invalidate); } catch { } });
                try { BeginInvoke((Action)delegate { guideBusy = false; guideCanvas.Invalidate(); }); } catch { guideBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }

        void PaintGuide(Graphics g)
        {
            g.TranslateTransform(guideCanvas.AutoScrollPosition.X, guideCanvas.AutoScrollPosition.Y);
            float x = 14 * S, y = 8 * S, w = guideCanvas.ClientSize.Width - 28 * S;
            Begin(guideCanvas);
            var r = guideRun;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
            {
                if (r == null)
                {
                    g.DrawString("WHAT'S WRONG?", Draw.fTiny, muted, x, y); y += 24 * S;
                    float cw = (w - 14 * S) / 2, chh = 104 * S;
                    for (int i = 0; i < Investigate.All.Length; i++)
                    {
                        var f = Investigate.All[i];
                        var cr = new RectangleF(x + (i % 2) * (cw + 14 * S), y + (i / 2) * (chh + 14 * S), cw, chh);
                        Theme.Fill(g, cr, 12 * S, Theme.Soft); Theme.Stroke(g, cr, 12 * S, Theme.Track, 1);
                        g.DrawString(f.Title, Draw.fName, ink, cr.X + 16 * S, cr.Y + 14 * S);
                        g.DrawString(f.Ask, Draw.fBody, sub, new RectangleF(cr.X + 16 * S, cr.Y + 42 * S, cr.Width - 32 * S, cr.Height - 48 * S));
                        string id = f.Id; Act(guideCanvas, cr, delegate { StartGuide(id, false); }); Hint(guideCanvas, cr, "guide." + id);
                    }
                    y += ((Investigate.All.Length + 1) / 2) * (chh + 14 * S) + 10 * S;
                    g.DrawString(Voice.P("Every answer says where each fact came from - measured, Windows-reported, seen in packets or inferred - and what I couldn't establish.", "Each fact is labelled: measured, Windows-reported, seen in packets, inferred or unavailable."), Draw.fSmall, muted, new RectangleF(x, y, w, 40 * S));
                    y += 40 * S;
                }
                else
                {
                    string back = "<  All problems"; var bs = g.MeasureString(back, Draw.fBodyB);
                    using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(back, Draw.fBodyB, lb, x, y);
                    Act(guideCanvas, new RectangleF(x, y, bs.Width, bs.Height), delegate { if (!guideBusy) { guideRun = null; guideCanvas.Invalidate(); } });
                    g.DrawString(r.Title.ToUpperInvariant() + (r.Input != "" && r.Id == "site" ? "  ·  " + r.Input.ToUpperInvariant() : "") + "  ·  " + (r.Done ? "DONE " + r.Started.ToString("HH:mm", CultureInfo.CurrentCulture) : (r.Status != "" ? r.Status : "WORKING...")), Draw.fTiny, muted, x + bs.Width + 16 * S, y + 3 * S);
                    y += 32 * S;
                    // the finding
                    string fin = r.Done ? r.Finding : Voice.P("Looking...", "Running...");
                    float fh = Draw.Measure(fin, Draw.fName, (int)(w - 40 * S)).Height;
                    var frc = new RectangleF(x, y, w, fh + 44 * S);
                    Theme.Fill(g, frc, 12 * S, Theme.LvSoft(r.Done ? r.Lv : 3));
                    using (var tb = new SolidBrush(Theme.Muted)) g.DrawString("FINDING", Draw.fTiny, tb, x + 18 * S, y + 10 * S);
                    using (var fb = new SolidBrush(r.Done ? (r.Lv == 0 ? Theme.Ink : Theme.LvText(r.Lv)) : Theme.Muted)) g.DrawString(fin, Draw.fName, fb, new RectangleF(x + 18 * S, y + 28 * S, w - 36 * S, fh + 6));
                    y += frc.Height + 18 * S;
                    // observed
                    var obs = r.ObservedCopy();
                    if (obs.Count > 0)
                    {
                        g.DrawString("WHAT I OBSERVED", Draw.fTiny, muted, x, y); y += 22 * S;
                        float nameW = Math.Min(280 * S, w * 0.34f), pillW = Cfg.Details ? 170 * S : 120 * S;
                        foreach (var o in obs)
                        {
                            int lv; int.TryParse(o[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv);
                            float vh = Draw.Measure(o[3], Draw.fBody, (int)(w - nameW - pillW - 30 * S)).Height, rh = Math.Max(30 * S, vh + 12 * S);
                            using (var b = new SolidBrush(Theme.Lv(lv))) g.FillEllipse(b, x, y + 9 * S, 10 * S, 10 * S);
                            g.DrawString(o[2], Draw.fBodyB, ink, new RectangleF(x + 20 * S, y + 5 * S, nameW - 24 * S, rh));
                            using (var vb = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Sub)) g.DrawString(o[3], Draw.fBody, vb, new RectangleF(x + nameW, y + 5 * S, w - nameW - pillW - 10 * S, vh + 4));
                            DateTime at; DateTime? atN = DateTime.TryParse(o[4], CultureInfo.CurrentCulture, DateTimeStyles.None, out at) ? at : (DateTime?)null;
                            float px = x + w - pillW + 4 * S; Evidence.Pill(g, o[0], px, y + 6 * S, atN);
                            Hint(guideCanvas, new RectangleF(px, y, pillW, rh), Evidence.Key(o[0]));
                            y += rh; g.DrawLine(line, x, y, x + w, y);
                        }
                        y += 16 * S;
                    }
                    if (r.Done)
                    {
                        Func<string, System.Collections.Generic.List<string>, float> list = delegate(string head, System.Collections.Generic.List<string> items)
                        {
                            if (items.Count == 0) return y;
                            g.DrawString(head, Draw.fTiny, muted, x, y); y += 22 * S;
                            foreach (var it in items) { float ih = Draw.Measure(it, Draw.fBody, (int)(w - 24 * S)).Height; g.DrawString("-", Draw.fBody, sub, x + 2 * S, y); g.DrawString(it, Draw.fBody, sub, new RectangleF(x + 18 * S, y, w - 24 * S, ih + 4)); y += ih + 8 * S; }
                            return y + 10 * S;
                        };
                        y = list("NOT ESTABLISHED", r.NotEst);
                        y = list("NEXT TEST", r.Next);
                        if (r.GoTab != "")
                        {
                            var ls = g.MeasureString(r.GoLabel + "  >", Draw.fBodyB); var br = new RectangleF(x, y, ls.Width + 28 * S, 34 * S);
                            Theme.Fill(g, br, 17 * S, Theme.Blue);
                            using (var wb = new SolidBrush(Color.White)) g.DrawString(r.GoLabel + "  >", Draw.fBodyB, wb, x + 14 * S, y + (34 * S - ls.Height) / 2);
                            string go = r.GoTab; Act(guideCanvas, br, delegate { tab = go; ShowTab(); });
                            y += 50 * S;
                        }
                    }
                }
            }
            var need = new Size(0, (int)(y + 20 * S));
            if (guideCanvas.AutoScrollMinSize != need) { guideCanvas.AutoScrollMinSize = need; guideCanvas.Invalidate(); }
        }
    }
}
