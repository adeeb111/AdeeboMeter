// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AdeeboMeter
{
    sealed partial class FullView
    {
        // ---------- Saved destinations
        static string Norm(string t) { return (t ?? "").Trim(); }
        void SyncPinBtn()
        {
            string t = Norm(destBox.Box.Text); bool pinned = Cfg.Pins.Contains(t, StringComparer.OrdinalIgnoreCase);
            pinBtn.Text = pinned ? "Unpin" : Cfg.Pins.Count >= 5 ? "Saved list is full (5)" : "Pin";
            pinBtn.Enabled = pinned || (Cfg.Pins.Count < 5 && t != "");
        }
        void TogglePin()
        {
            string t = Norm(destBox.Box.Text);
            int i = Cfg.Pins.FindIndex(p => string.Equals(p, t, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) Cfg.Pins.RemoveAt(i);
            else
            {
                string h; int pt; bool tl; string err;
                if (Cfg.Pins.Count >= 5 || !DestCheck.Parse(t, out h, out pt, out tl, out err)) { SyncPinBtn(); return; }
                Cfg.Pins.Add(t);
            }
            Cfg.Save(); BuildPinBar(); SyncPinBtn();
        }
        void BuildPinBar()
        {
            pinBar.SuspendLayout();
            foreach (Control c in pinBar.Controls.Cast<Control>().ToList()) { pinBar.Controls.Remove(c); c.Dispose(); }   // no leaked handles
            if (Cfg.Pins.Count > 0)
            {
                pinBar.Controls.Add(new Label { Text = "SAVED", AutoSize = true, Font = Draw.fTiny, ForeColor = Theme.Muted, BackColor = Theme.Card, Margin = new Padding(0, Sc(10), Sc(8), 0) });
                foreach (var p in Cfg.Pins)
                {
                    var b = new PillButton(Draw.Trunc(p, 28), PillButton.K.Chip) { Tag = p, Margin = new Padding(0, Sc(2), Sc(6), 0) };
                    b.Click += delegate(object s, EventArgs e) { destBox.Box.Text = (string)((Control)s).Tag; StartDest(); };
                    pinBar.Controls.Add(b);
                }
                var all = new PillButton(Cfg.Pins.Count == 1 ? "Run it" : "Run all " + Cfg.Pins.Count, PillButton.K.Primary) { Margin = new Padding(Sc(6), 0, 0, 0) };
                all.Height = Sc(32); all.Click += delegate { RunAllPins(); };
                pinBar.Controls.Add(all);
            }
            pinBar.Visible = Cfg.Pins.Count > 0;
            pinBar.ResumeLayout();
            Recolor(pinBar);
        }
        void RunAllPins()
        {
            if (pinRunning || Cfg.Pins.Count == 0) return;
            var list2 = Cfg.Pins.Select(p => new DestResult { Input = p, Target = p }).ToList();
            pinResults = list2; dest = null; pinRunning = true; destCanvas.Invalidate();
            var tasks = list2.Select(r => Task.Factory.StartNew(() =>
            {
                try { DestCheck.Run(r.Input, r, delegate { }, M); }
                catch (Exception e) { r.Verdict = "Check failed: " + e.Message; r.Level = 1; r.Done = true; Program.Log("pin: " + e.Message); }
            }, TaskCreationOptions.LongRunning)).ToArray();
            Task.Factory.ContinueWhenAll(tasks, delegate { try { BeginInvoke((Action)delegate { pinRunning = false; destCanvas.Invalidate(); }); } catch { } });
        }
        float PaintPinTable(Graphics g, List<DestResult> pr, float x, float y, float w)
        {
            float rh = 40 * S, h = 34 * S + pr.Count * rh;
            var card = new RectangleF(x, y, w, h); Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var line = new Pen(Theme.Line2))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString("SAVED DESTINATIONS" + (pinRunning ? "  ·  CHECKING ALL AT ONCE..." : "  ·  CHECKED " + pr[0].At.ToString("HH:mm", CultureInfo.CurrentCulture)), Draw.fTiny, muted, x + 14 * S, y + 12 * S);
                float yy = y + 32 * S, dotsW = 8 * 14 * S;
                foreach (var r in pr)
                {
                    var row = new RectangleF(x + 1, yy, w - 2, rh);
                    if (r == dest) using (var b = new SolidBrush(Theme.RowSel)) g.FillRectangle(b, row);
                    bool done = r.Done;
                    if (done) using (var b = new SolidBrush(Theme.Lv(r.Level))) g.FillEllipse(b, x + 14 * S, yy + rh / 2 - 6 * S, 12 * S, 12 * S);
                    else using (var p = new Pen(Theme.Blue, 2 * S)) g.DrawEllipse(p, x + 14 * S, yy + rh / 2 - 6 * S, 12 * S, 12 * S);
                    g.DrawString(r.Input, Draw.fBodyB, ink, new RectangleF(x + 36 * S, yy + 11 * S, 220 * S, 20 * S), nowrap);
                    float dx = x + 266 * S;
                    for (int i = 0; i < 8; i++)
                        using (var b = new SolidBrush(r.Lv[i] < 0 ? Theme.Track : Theme.Lv(r.Lv[i]))) g.FillEllipse(b, dx + i * 14 * S, yy + rh / 2 - 4 * S, 8 * S, 8 * S);
                    string v = done ? r.Verdict : r.Note ?? "checking...";
                    using (var vb = new SolidBrush(done ? Theme.LvText(r.Level) : Theme.Sub)) g.DrawString(v, Draw.fBody, vb, new RectangleF(dx + dotsW + 12 * S, yy + 11 * S, w - (dx - x) - dotsW - 26 * S, 20 * S), nowrap);
                    g.DrawLine(line, x + 1, yy + rh, x + w - 1, yy + rh);
                    var captured = r; Act(destCanvas, row, delegate { dest = captured; destBox.Box.Text = captured.Input; destCanvas.Invalidate(); });
                    yy += rh;
                }
            }
            return y + h + 16 * S;
        }
            // the all-day watch of the first pinned destination: one ping every 30 s, last 24 hours
        float PaintPinWatch(Graphics g, float x, float y, float w)
        {
            var v = Pinned.Ms.Values(2880); var card = new RectangleF(x, y, w, 120 * S);
            Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            int lost = v.Count(double.IsNaN); var ok = v.Where(a => !double.IsNaN(a)).ToList();
            string head = "WATCHING " + Pinned.Host.ToUpperInvariant() + " ALL DAY  ·  " + (v.Length == 0 ? "FIRST PING IN A MOMENT" : (ok.Count > 0 ? "TYPICAL " + ok.OrderBy(a => a).ElementAt(ok.Count / 2).ToString("0", CultureInfo.CurrentCulture) + " MS  ·  " : "") + "LOST " + lost + " OF " + v.Length + "  ·  LAST " + (v.Length / 2 >= 60 ? (v.Length / 120.0).ToString("0.#", CultureInfo.CurrentCulture) + " H" : v.Length / 2 + " MIN"));
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(head, Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(destCanvas, new RectangleF(x, y, w, 26 * S), "dest.PINWATCH");
            if (v.Length >= 2)
            {
                var r = new RectangleF(x + 14 * S, y + 34 * S, w - 28 * S, 72 * S);
                Theme.Spark(g, v.Select(a => double.IsNaN(a) ? 0 : a).ToArray(), r, Theme.Blue, 0, true);
                using (var red = new SolidBrush(Theme.Red)) for (int i = 0; i < v.Length; i++) if (double.IsNaN(v[i])) g.FillRectangle(red, r.X + (float)i / (v.Length - 1) * r.Width - 1, r.Bottom - 6 * S, 2 * S, 6 * S);
            }
            return y + card.Height + 14 * S;
        }
    }
}
