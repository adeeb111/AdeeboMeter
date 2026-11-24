// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // The speed test on Home (Adeeb, 22 Nov: "redone, shown as a speed meter gauge, and the server selection should look modern").
    // One card: a gauge on the left (the needle follows the live rate while a test runs, then rests on the download it found),
    // the four results on the right, the button and the server chip under them. The server chip opens SpeedPicker: Automatic,
    // "find the closest", then each provider's cities as chips. Still only when the button is pressed - never by itself.
    sealed partial class FullView
    {
        RectangleF gaugeRect; float gaugeShown; Timer gaugeAnim;   // gaugeShown: where the needle is, 0..1 of the scale
        static readonly double[] GaugeMarks = { 0, 5, 10, 25, 50, 100, 250, 500, 1000 };

        // a speed to a point on the scale: equal steps between the marks, so 20 Mb/s and 900 Mb/s both read well
        static float GaugeFrac(double mbps)
        {
            if (double.IsNaN(mbps) || mbps <= 0) return 0;
            int n = GaugeMarks.Length - 1;
            for (int i = 0; i < n; i++)
                if (mbps <= GaugeMarks[i + 1]) return (float)((i + (mbps - GaugeMarks[i]) / (GaugeMarks[i + 1] - GaugeMarks[i])) / n);
            return 1f;
        }
        // what the needle should point at right now, and the big number under it
        double GaugeValue(out string label)
        {
            var now = SpeedTest.Now; int st = SpeedTest.Stage;
            if (SpeedTest.Busy)
            {
                if (st == 3) { label = Kit.Sent(L.T("UPLOAD")); return SpeedTest.LiveMbps; }
                if (st == 2) { label = Kit.Sent(L.T("DOWNLOAD")); return SpeedTest.LiveMbps; }
                label = Kit.Sent(L.T("PING")); return double.NaN;
            }
            var last = SpeedTest.Last; label = last != null ? Kit.Sent(L.T("DOWNLOAD")) : Kit.Sent(L.T("NOT TESTED YET"));
            return last != null ? last.Down : double.NaN;
        }

        float PaintSpeedCard(Graphics g, float x, float y, float w)
        {
            var last = SpeedTest.Last; bool busy = SpeedTest.Busy; var cur = SpeedTest.Now ?? last;
            bool narrow = w < 640 * S;
            float gw = narrow ? Math.Min(w - 40 * S, 300 * S) : 280 * S, gh = 196 * S;
            float rightH = 22 * S + 74 * S + 16 * S + 40 * S + 12 * S + 20 * S;   // title, stats, button row, status
            float H = 20 * S + (narrow ? gh + 12 * S + rightH : Math.Max(gh, rightH + 8 * S)) + 18 * S;
            var cr = new RectangleF(x, y, w, H); Kit.Box(g, cr);

            // the gauge
            gaugeRect = narrow ? new RectangleF(x + (w - gw) / 2, y + 16 * S, gw, gh) : new RectangleF(x + 18 * S, y + 16 * S, gw, gh);
            string lab; double v = GaugeValue(out lab); float want = GaugeFrac(v);
            if (!busy && gaugeAnim == null) gaugeShown = want;   // nothing moving: straight to it
            DrawGauge(g, gaugeRect, gaugeShown, v, lab, busy);
            Hint(overviewCanvas, gaugeRect, "tb.speedtest");
            if (busy || Math.Abs(gaugeShown - want) > 0.002f) GaugeAnimate();

            // the right side: title and where to, the results, the button and the server
            float rx = narrow ? x + 20 * S : gaugeRect.Right + 24 * S, rw = x + w - 20 * S - rx, ry = narrow ? gaugeRect.Bottom + 12 * S : y + 22 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var mut = new SolidBrush(Theme.Muted)) using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(L.T("Internet speed"), Draw.fHead, ink, rx, ry);
                string when = last != null && !busy ? L.T("Last test") + " " + last.T.ToString("HH:mm", CultureInfo.CurrentCulture) + (last.Where != "" && last.Where.StartsWith("data centre", StringComparison.Ordinal) ? " · " + last.Where.Substring(12) : "") : L.T("Only when you press the button");
                var ws = g.MeasureString(L.T("Internet speed"), Draw.fHead);
                g.DrawString(when, Draw.fSmall, mut, new RectangleF(rx + ws.Width + 10 * S, ry + (ws.Height - Draw.fSmall.GetHeight(g)) / 2 + 1 * S, Math.Max(10 * S, rw - ws.Width - 10 * S), 20 * S), nf);
                ry += 30 * S;

                // four results: download, upload, ping, when busy
                Func<double, string> mb = d => double.IsNaN(d) ? "-" : SpeedTest.Mbps(d);
                var stats = new[] {
                    new[] { "Download", cur != null ? mb(cur.Down) : "-", "Mb/s", busy && SpeedTest.Stage == 2 ? "1" : "" },
                    new[] { "Upload", cur == null ? "-" : cur.NoUpload ? L.T("n/a") : mb(cur.Up), cur != null && cur.NoUpload ? L.T("this server: download only") : "Mb/s", busy && SpeedTest.Stage == 3 ? "1" : "" },
                    new[] { "Ping", cur == null || double.IsNaN(cur.Idle) ? "-" : Math.Round(cur.Idle).ToString(CultureInfo.CurrentCulture), "ms", busy && SpeedTest.Stage == 1 ? "1" : "" },
                    new[] { "When busy", cur != null && cur.Grade != "" ? cur.Grade : "-", cur != null && cur.Grade != "" ? L.T("lag under load") : L.T("run a test to see"), "" } };   // hunters10 L-5: before a test the dash says why
                float sw = (rw - 3 * 8 * S) / 4, sh = 66 * S;
                for (int i = 0; i < 4; i++)
                {
                    var sr = new RectangleF(rx + i * (sw + 8 * S), ry, sw, sh);
                    bool live = stats[i][3] == "1";
                    Theme.Fill(g, sr, 12 * S, live ? Theme.BlueSoft : Theme.Soft);
                    if (live) Theme.Stroke(g, Kit.Edge(sr), 12 * S, Theme.Blue, 1);
                    g.DrawString(L.T(stats[i][0]), Draw.fTiny, mut, new RectangleF(sr.X + 12 * S, sr.Y + 9 * S, sr.Width - 16 * S, 16 * S), nf);
                    var vs = g.MeasureString(stats[i][1], Draw.fName);
                    g.DrawString(stats[i][1], Draw.fName, ink, sr.X + 12 * S, sr.Y + 25 * S);
                    g.DrawString(L.T(stats[i][2]), Draw.fSmall, mut, new RectangleF(sr.X + 12 * S + (stats[i][2].Length <= 4 ? vs.Width : 0), stats[i][2].Length <= 4 ? sr.Y + 25 * S + (vs.Height - Draw.fSmall.GetHeight(g)) - 2 * S : sr.Y + 46 * S, sr.Width - 16 * S - (stats[i][2].Length <= 4 ? vs.Width : 0), 18 * S), nf);
                }
                if (cur != null && cur.Grade != "") Hint(overviewCanvas, new RectangleF(rx + 3 * (sw + 8 * S), ry, sw, sh), "tb.speedtest");
                ry += sh + 16 * S;

                // the button, then the server chip
                string bt = L.T(busy ? "Stop" : "Test my speed"); var bsz = g.MeasureString(bt, Draw.fBtn);
                var br = new RectangleF(rx, ry, Math.Max(150 * S, bsz.Width + 44 * S), 40 * S);
                Theme.Fill(g, br, 20 * S, busy ? Theme.Soft : Theme.Blue);
                if (!busy) Kit.Icon(g, "speed", new RectangleF(br.X + 16 * S, br.Y + 11 * S, 18 * S, 18 * S), Theme.OnBlue);
                using (var tb = new SolidBrush(busy ? Theme.Ink : Theme.OnBlue)) g.DrawString(bt, Draw.fBtn, tb, busy ? br.X + (br.Width - bsz.Width) / 2 : br.X + 40 * S, br.Y + (br.Height - bsz.Height) / 2);
                Act(overviewCanvas, br, delegate { if (SpeedTest.Busy) speedRun.Stop = true; else StartSpeed(); });

                var sv = SpeedTest.Pick(Cfg.SpeedServer) ?? SpeedTest.Pick("auto");
                string ct = speedFinding ? L.T("Finding the closest...") : sv.Id == "cloudflare" ? L.T("Automatic") + " · Cloudflare" : sv.Where != "" ? sv.Where + " · " + sv.Provider : sv.Label;
                var csz = g.MeasureString(ct, Draw.fBody);
                float cw = Math.Min(rw - br.Width - 10 * S, csz.Width + 66 * S);
                var chip = new RectangleF(br.Right + 10 * S, ry, cw, 40 * S);
                Theme.Fill(g, chip, 20 * S, Theme.Card); Theme.Stroke(g, Kit.Edge(chip), 20 * S, Theme.Line, 1);
                Kit.Icon(g, "pin", new RectangleF(chip.X + 14 * S, chip.Y + 11 * S, 18 * S, 18 * S), Kit.Accent("home"));
                g.DrawString(ct, Draw.fBody, ink, new RectangleF(chip.X + 38 * S, chip.Y + (chip.Height - csz.Height) / 2, chip.Width - 66 * S, csz.Height + 2), nf);
                using (var cp = new Pen(Theme.Muted, 1.6f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                { float ax = chip.Right - 22 * S, ay = chip.Y + 18 * S; g.DrawLines(cp, new[] { new PointF(ax - 4 * S, ay), new PointF(ax, ay + 4 * S), new PointF(ax + 4 * S, ay) }); }
                var chipScreen = chip;
                Act(overviewCanvas, chip, delegate { if (!SpeedTest.Busy && !speedFinding) OpenSpeedPicker(chipScreen); });
                ry += 40 * S + 12 * S;

                // one line: what it's doing, what went wrong, or what it costs
                string st = busy ? speedRun.Status : speedErr != "" ? L.T("Speed test:") + " " + speedErr : speedNote != "" ? speedNote
                    : L.T("About 20 seconds, up to") + " " + 2 * (SpeedTest.MaxDownMB + SpeedTest.MaxUpMB) + " MB. "   /* review 1.1.6 P3 #19: the real ceiling */ + L.T("Never runs by itself.");
                using (var stb = new SolidBrush(speedErr != "" && !busy ? Theme.LvText(1) : Theme.Muted)) g.DrawString(st, Draw.fSmall, stb, new RectangleF(rx, ry, rw, 20 * S), nf);
            }
            return cr.Bottom + 18 * S;
        }

        // the gauge itself: a 240-degree track, the coloured part up to the needle, the marks, the needle and the number
        void DrawGauge(Graphics g, RectangleF r, float frac, double value, string label, bool busy)
        {
            float R = Math.Min(r.Width / 2 - 8 * S, (r.Height - 46 * S) / 1.5f), cx = r.X + r.Width / 2, cy = r.Y + R + 8 * S, th = 14 * S;
            var arc = new RectangleF(cx - R + th / 2, cy - R + th / 2, 2 * R - th, 2 * R - th);
            const float start = 150f, sweep = 240f;
            using (var tp = new Pen(Theme.Dark ? Theme.Line : Theme.Line2, th) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(tp, arc, start, sweep);
            Color a1 = Theme.HighContrast ? SystemColors.Highlight : Theme.Blue, a2 = Theme.HighContrast ? SystemColors.Highlight : Color.FromArgb(0x1D, 0xB9, 0x8E);
            if (frac > 0.004f)
                using (var lg = new LinearGradientBrush(RectangleF.Inflate(arc, th, th), a1, a2, 0f) { WrapMode = WrapMode.TileFlipX })
                using (var vp = new Pen(lg, th) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(vp, arc, start, sweep * frac);
            // the marks, inside the track
            using (var mb = new SolidBrush(Theme.Muted)) using (var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                for (int i = 0; i < GaugeMarks.Length; i++)
                {
                    double ang = (start + sweep * i / (GaugeMarks.Length - 1)) * Math.PI / 180; float lr = R - th - 14 * S;
                    float lx = cx + (float)Math.Cos(ang) * lr, ly = cy + (float)Math.Sin(ang) * lr;
                    g.DrawString(GaugeMarks[i] >= 1000 ? "1G" : GaugeMarks[i].ToString(CultureInfo.CurrentCulture), Draw.fTiny, mb, new RectangleF(lx - 18 * S, ly - 9 * S, 36 * S, 18 * S), cf);
                }
            // the needle
            {
                double ang = (start + sweep * frac) * Math.PI / 180; float nl = R - th - 30 * S;
                var tip = new PointF(cx + (float)Math.Cos(ang) * nl, cy + (float)Math.Sin(ang) * nl);
                var side = new PointF((float)-Math.Sin(ang) * 4 * S, (float)Math.Cos(ang) * 4 * S);
                using (var nb = new SolidBrush(Theme.Ink))
                {
                    g.FillPolygon(nb, new[] { tip, new PointF(cx + side.X, cy + side.Y), new PointF(cx - side.X, cy - side.Y) });
                    g.FillEllipse(nb, cx - 8 * S, cy - 8 * S, 16 * S, 16 * S);
                }
                using (var hb = new SolidBrush(Theme.Card)) g.FillEllipse(hb, cx - 3 * S, cy - 3 * S, 6 * S, 6 * S);
            }
            // the number and what it is
            string num = double.IsNaN(value) ? (busy ? "..." : "-") : SpeedTest.Mbps(value);
            using (var ib = new SolidBrush(Theme.Ink)) using (var mb = new SolidBrush(Theme.Muted)) using (var cf = new StringFormat { Alignment = StringAlignment.Center })
            {
                float ny = cy + R * 0.5f - 22 * S;
                g.DrawString(num, Draw.fHero, ib, new RectangleF(r.X, ny, r.Width, 40 * S), cf);
                g.DrawString((double.IsNaN(value) ? "" : "Mb/s  ·  ") + label, Draw.fTiny, mb, new RectangleF(r.X, ny + 40 * S, r.Width, 18 * S), cf);
            }
        }

        // the needle moves on its own clock while a test runs (or settles after one) - only the gauge is redrawn, straight onto the window
        void GaugeAnimate()
        {
            if (gaugeAnim == null) { gaugeAnim = new Timer { Interval = 40 }; gaugeAnim.Tick += delegate { GaugeStep(); }; }
            if (!gaugeAnim.Enabled) gaugeAnim.Start();
        }
        void GaugeStep()
        {
            if (overviewCanvas == null || overviewCanvas.IsDisposed || !overviewCanvas.Visible || tab != "overview") { gaugeAnim.Stop(); return; }
            string lab; double v = GaugeValue(out lab); float want = GaugeFrac(v);
            float was = gaugeShown; gaugeShown = !Typing.On ? want : was + (want - was) * 0.18f;
            if (Math.Abs(gaugeShown - want) < 0.002f) gaugeShown = want;
            if (!SpeedTest.Busy && gaugeShown == want) gaugeAnim.Stop();
            var sp = overviewCanvas.AutoScrollPosition;
            var area = Rectangle.Round(new RectangleF(gaugeRect.X + sp.X, gaugeRect.Y + sp.Y, gaugeRect.Width, gaugeRect.Height));
            area.Intersect(overviewCanvas.ClientRectangle); if (area.Width <= 0 || area.Height <= 0) return;
            using (var cg = overviewCanvas.CreateGraphics())
            using (var buf = BufferedGraphicsManager.Current.Allocate(cg, area))
            {
                var g = buf.Graphics; g.Clear(Theme.Card); Prep(g);
                g.TranslateTransform(sp.X, sp.Y); DrawGauge(g, gaugeRect, gaugeShown, v, lab, SpeedTest.Busy);
                buf.Render(cg);
            }
        }

        // the server picker, under the chip
        void OpenSpeedPicker(RectangleF chip)
        {
            var sp = overviewCanvas.AutoScrollPosition;
            var at = overviewCanvas.PointToScreen(new Point((int)(chip.X + sp.X), (int)(chip.Bottom + sp.Y + 6 * S)));
            SpeedPicker.Show(this, at, Cfg.SpeedServer, id =>
            {
                if (id == "find") { FindClosestSpeed(); return; }
                if (id == "add") { tab = "toolbox"; ShowTab(); EnsureToolbox(); if (tbBusy) { BusyNote(); return; } OpenTool("speedtest"); if (tbSel == "speedtest" && tbBoxes.Count > 0) tbBoxes[0].Box.Text = L.T("list"); return; }   // never into a running tool's boxes
                Cfg.SpeedServer = id == "cloudflare" ? "auto" : id; Cfg.Save(); speedErr = ""; speedNote = ""; overviewCanvas.Invalidate();
            });
        }
    }

    // A modern drop-down for the speed-test server: Automatic first, "find the closest", then each provider's cities as chips.
    // Closes on Esc, on a pick, or when anything else is clicked.
    sealed class SpeedPicker : Form
    {
        static float S { get { return Draw.S; } }
        sealed class Hit { public RectangleF R; public string Id; }
        readonly List<Hit> hits = new List<Hit>(); string hot = "", cur; readonly Action<string> pick; bool done;
        static SpeedPicker open;

        public static void Show(Form owner, Point at, string current, Action<string> pick)
        {
            try
            {
                if (open != null && !open.IsDisposed) { open.Close(); return; }
                var p = new SpeedPicker(current, pick);
                var wa = Screen.FromPoint(at).WorkingArea;
                p.Location = new Point(Math.Max(wa.Left, Math.Min(at.X, wa.Right - p.Width)), at.Y + p.Height <= wa.Bottom ? at.Y : Math.Max(wa.Top, at.Y - p.Height - (int)(52 * S)));
                open = p; p.Show(owner);
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("speed picker: " + e.Message); }
        }
        SpeedPicker(string current, Action<string> pick)
        {
            cur = current == "auto" ? "cloudflare" : current; this.pick = pick;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; KeyPreview = true; TopMost = false;
            BackColor = Theme.Card; Font = Draw.fBody; Text = L.T("Speed test server"); DoubleBuffered = true;
            ClientSize = new Size((int)(460 * S), (int)Math.Ceiling(Measure()));
            using (var pth = Theme.Round(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 14 * S)) Region = new Region(pth);
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x00020000; return cp; } }   // CS_DROPSHADOW
        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); if (!done) { done = true; BeginInvoke((Action)Close); } }
        protected override void OnFormClosed(FormClosedEventArgs e) { base.OnFormClosed(e); if (open == this) open = null; }   // a modeless form disposes itself on Close
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { e.Handled = true; done = true; Close(); } base.OnKeyDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { string h = ""; foreach (var x in hits) if (x.R.Contains(e.Location)) { h = x.Id; break; } if (h != hot) { hot = h; Cursor = h != "" ? Cursors.Hand : Cursors.Default; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { if (hot != "") { hot = ""; Invalidate(); } }
        // hunters10 ML-3: the keyboard too - arrows or Tab move the highlight, Enter or Space picks it
        protected override bool ProcessDialogKey(Keys keyData)
        {
            var k = keyData & Keys.KeyCode;
            if (hits.Count > 0 && (k == Keys.Tab || k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right))
            {
                int i = hits.FindIndex(x => x.Id == hot); bool back = k == Keys.Up || k == Keys.Left || (k == Keys.Tab && (keyData & Keys.Shift) != 0);
                i = i < 0 ? (back ? hits.Count - 1 : 0) : (i + (back ? hits.Count - 1 : 1)) % hits.Count; hot = hits[i].Id; Invalidate(); return true;
            }
            if ((k == Keys.Enter || k == Keys.Space) && hot != "") { string id = hot; done = true; Close(); try { pick(id); } catch (Exception ex) { if (ex is OutOfMemoryException) throw; Program.Log("speed picker pick: " + ex.Message); } return true; }
            return base.ProcessDialogKey(keyData);
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            foreach (var x in hits) if (x.R.Contains(e.Location)) { string id = x.Id; done = true; Close(); try { pick(id); } catch (Exception ex) { if (ex is OutOfMemoryException) throw; Program.Log("speed picker pick: " + ex.Message); } return; }
        }
        float Measure() { using (var bmp = new Bitmap(1, 1)) using (var g = Graphics.FromImage(bmp)) return Lay(g, false); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = Draw.WindowText; g.TextContrast = Draw.Contrast;
            Lay(g, true);
            using (var pth = Theme.Round(new RectangleF(0.5f, 0.5f, ClientSize.Width - 1, ClientSize.Height - 1), 14 * S)) using (var pen = new Pen(Theme.Line, 1)) g.DrawPath(pen, pth);
        }

        float Lay(Graphics g, bool draw)
        {
            hits.Clear();
            float W = 460 * S, pad = 16 * S, x = pad, w = W - 2 * pad, y = pad;
            Color acc = Kit.Accent("home");
            using (var ink = new SolidBrush(Theme.Ink)) using (var mut = new SolidBrush(Theme.Muted)) using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                if (draw) g.DrawString(Kit.Sent(L.T("TEST TO")), Draw.fTiny, mut, x, y); y += 22 * S;

                // Automatic
                var ar = new RectangleF(x, y, w, 58 * S); bool asel = cur == "cloudflare";
                if (draw)
                {
                    Theme.Fill(g, ar, 12 * S, asel ? Theme.BlueSoft : hot == "cloudflare" ? Theme.Soft : Theme.Card);
                    Theme.Stroke(g, Kit.Edge(ar), 12 * S, asel ? acc : Theme.Line, asel ? 1.5f : 1);
                    Kit.Icon(g, "speed", new RectangleF(ar.X + 14 * S, ar.Y + 19 * S, 20 * S, 20 * S), acc);
                    g.DrawString(L.T("Automatic"), Draw.fBodyB, ink, ar.X + 46 * S, ar.Y + 10 * S);
                    g.DrawString(L.T("Cloudflare's nearest data centre · download and upload"), Draw.fSmall, mut, new RectangleF(ar.X + 46 * S, ar.Y + 31 * S, ar.Width - (asel ? 80 : 56) * S, 18 * S), nf);
                    if (asel) Kit.Icon(g, "check", new RectangleF(ar.Right - 30 * S, ar.Y + 20 * S, 18 * S, 18 * S), acc);
                }
                hits.Add(new Hit { R = ar, Id = "cloudflare" }); y += ar.Height + 8 * S;

                // find the closest
                {
                    string ft = L.T("Find the closest server for me"); var fs = g.MeasureString(ft, Draw.fBody);
                    var fr = new RectangleF(x, y, fs.Width + 48 * S, 34 * S);
                    if (draw)
                    {
                        Theme.Fill(g, fr, 17 * S, hot == "find" ? Theme.BlueSoft : Theme.Card); Theme.Stroke(g, Kit.Edge(fr), 17 * S, hot == "find" ? acc : Theme.Line, 1);
                        Kit.Icon(g, "search", new RectangleF(fr.X + 12 * S, fr.Y + 9 * S, 16 * S, 16 * S), acc);
                        using (var ab = new SolidBrush(Kit.Ink("home"))) g.DrawString(ft, Draw.fBody, ab, fr.X + 34 * S, fr.Y + (fr.Height - fs.Height) / 2);
                    }
                    hits.Add(new Hit { R = fr, Id = "find" }); y += fr.Height + 16 * S;
                }

                // each provider's cities
                foreach (var grp in SpeedTest.All().Where(s => s.Id != "cloudflare").GroupBy(s => s.Provider))
                {
                    bool downOnly = grp.All(s => s.Up == "");
                    if (draw) g.DrawString(grp.Key.ToUpper(CultureInfo.CurrentCulture) + (downOnly ? "  ·  " + Kit.Sent(L.T("DOWNLOAD ONLY")) : ""), Draw.fTiny, mut, x, y);
                    y += 22 * S;
                    float cx = x, ch = 30 * S;
                    foreach (var sv in grp)
                    {
                        string t = sv.Where != "" ? sv.Where : sv.Id; var ts = g.MeasureString(t, Draw.fSmall);
                        float cw = Math.Min(w, ts.Width + 24 * S);
                        if (cx > x && cx + cw > x + w) { cx = x; y += ch + 6 * S; }
                        var r = new RectangleF(cx, y, cw, ch); bool sel = cur == sv.Id, h = hot == sv.Id;
                        if (draw)
                        {
                            Theme.Fill(g, r, ch / 2, sel ? acc : h ? Theme.BlueSoft : Theme.Soft);
                            if (h && !sel) Theme.Stroke(g, Kit.Edge(r), ch / 2, acc, 1);
                            using (var tb = new SolidBrush(sel ? Theme.OnBlue : Theme.Ink)) g.DrawString(t, Draw.fSmall, tb, new RectangleF(r.X + 12 * S, r.Y + (ch - ts.Height) / 2, r.Width - 16 * S, ts.Height + 2), nf);
                        }
                        hits.Add(new Hit { R = r, Id = sv.Id }); cx += cw + 6 * S;
                    }
                    y += ch + 14 * S;
                }

                // your own
                {
                    string mt = L.T("Add my own server...") + "  ›"; var ms = g.MeasureString(mt, Draw.fSmall);
                    var mr = new RectangleF(x, y, ms.Width, 22 * S);
                    if (draw) using (var bb = new SolidBrush(hot == "add" ? Theme.BlueHot : Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(mt, Draw.fSmall, bb, mr.X, mr.Y + 2 * S);
                    hits.Add(new Hit { R = mr, Id = "add" }); y += mr.Height + pad - 4 * S;
                }
            }
            return y;
        }
    }
}
