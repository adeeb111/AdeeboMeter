// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Streamer lag graph
    // A small borderless always-on-top window: the game's ping now and its last 60 seconds as a graph, so viewers see the lag
    // when it happens. For OBS: capture this window (Window Capture) - on a green background for a Chroma Key filter - or tick
    // "Write a PNG for OBS" and add  overlay.png  in AdeeboMeter's data folder as an Image source (it's rewritten every second).
    // Drag to move; right-click for options. It shows only what AdeeboMeter already measures - it sends nothing of its own.
    sealed class StreamOverlay : Form, ITouchDrag
    {
        Point touchAt;
        bool ITouchDrag.Grab(Point screen) { touchAt = screen; return true; }
        void ITouchDrag.DragTo(Point screen) { Location = new Point(Location.X + screen.X - touchAt.X, Location.Y + screen.Y - touchAt.Y); touchAt = screen; }
        static StreamOverlay open;
        readonly Monitor M; readonly Timer tick = new Timer { Interval = 1000 };
        bool green, png; Point down; byte[] lastPng; string lastKey = "";
        readonly Font f1 = new Font(L.Code == "bn" || L.Code == "zh" ? Draw.LangFamily : "Segoe UI", 9f), f2 = new Font(L.Code == "bn" || L.Code == "zh" ? Draw.LangFamily : "Segoe UI Semibold", 20f);   // made once, not on every paint
        static int Sc(float v) { return (int)(v * Draw.S); }
        public static string PngPath { get { return Path.Combine(Program.DataDir, "overlay.png"); } }

        public static void Toggle(Monitor m)
        {
            if (open != null && !open.IsDisposed) { open.Close(); return; }
            open = new StreamOverlay(m); open.Show();
        }
        StreamOverlay(Monitor m)
        {
            M = m; FormBorderStyle = FormBorderStyle.None; TopMost = true; ShowInTaskbar = true; Text = AdeeboMeter.L.T("AdeeboMeter - lag graph"); Icon = Theme.AppIcon;
            StartPosition = FormStartPosition.Manual; Size = new Size(Sc(320), Sc(110));
            var wa = Screen.PrimaryScreen.WorkingArea; Location = new Point(wa.Left + Sc(24), wa.Top + Sc(24));
            // back where it was left - if that spot is still on a screen (a monitor unplugged since: the primary's corner)
            var at = Cfg.OverlayAt.Split(','); int ox, oy;
            if (at.Length == 2 && int.TryParse(at[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out ox) && int.TryParse(at[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out oy) && Screen.AllScreens.Any(sc => sc.WorkingArea.Contains(new Point(ox + Sc(20), oy + Sc(20))))) Location = new Point(ox, oy);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) down = e.Location; };
            /* a finger moves it too (Touch.cs) */
            MouseMove += (s, e) => { if (e.Button == MouseButtons.Left) Location = new Point(Location.X + e.X - down.X, Location.Y + e.Y - down.Y); };
            var menu = Menus.Make(); menu.ShowImageMargin = false;
            var g = new ToolStripMenuItem(L.T("Green background (for OBS Chroma Key)")) { CheckOnClick = true }; g.CheckedChanged += delegate { green = g.Checked; Invalidate(); };
            var p = new ToolStripMenuItem(L.T("Write a PNG for OBS (overlay.png)")) { CheckOnClick = true }; p.CheckedChanged += delegate { png = p.Checked; lastKey = ""; if (png) Program.Log("lag graph png: " + PngPath); };
            var f = new ToolStripMenuItem(L.T("Show the PNG in its folder")); f.Click += delegate { if (File.Exists(PngPath)) try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + PngPath + "\""); } catch (System.ComponentModel.Win32Exception) { } };
            var c = new ToolStripMenuItem(L.T("Close")); c.Click += delegate { Close(); };
            menu.Items.AddRange(new ToolStripItem[] { g, p, f, new ToolStripSeparator(), c });
            ContextMenuStrip = menu;
            // hunters10: a finger has no right-click to rely on - a tap opens the same menu; Esc closes it from the keyboard
            MouseUp += (s, e) => { if (e.Button == MouseButtons.Left && TouchPan.Finger() && Math.Abs(e.X - down.X) < Sc(6) && Math.Abs(e.Y - down.Y) < Sc(6)) menu.Show(this, e.Location); };
            KeyPreview = true; KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            tick.Tick += delegate
            {
                // repaint (and encode the PNG) only when what it shows changed
                string t; double[] v; int st; Series(out t, out v, out st); var rv = M.GwPing.Values(60);
                string key = t + "|" + green + "|" + Spikes() + "|" + string.Join(",", v.Concat(rv).Select(x => double.IsNaN(x) ? "x" : Math.Round(x).ToString(CultureInfo.InvariantCulture)));
                if (key == lastKey) return; lastKey = key; Invalidate(); if (png) WritePng();
            };
            tick.Start();
            FormClosed += delegate { Cfg.OverlayAt = Location.X.ToString(CultureInfo.InvariantCulture) + "," + Location.Y.ToString(CultureInfo.InvariantCulture); Cfg.Save(); tick.Stop(); tick.Dispose(); menu.Dispose(); f1.Dispose(); f2.Dispose(); if (open == this) open = null; };
        }
        // what to show: the game you're playing (its best server), else the internet check
        void Series(out string title, out double[] v, out int step)   // step: seconds between samples
        {
            var gp = GamePing.Me; string id = gp.Playing;
            var game = id != null ? Games.All.FirstOrDefault(z => z.Id == id) : null;
            if (game != null)
            {
                var best = gp.Best(game) ?? (game.Servers.Count > 0 ? game.Servers[0] : null);
                if (best != null) { step = Cfg.GameFast ? 1 : Math.Max(1, Cfg.GameBgSec); title = game.Name + " · " + best.Name; v = gp.Stat(game.Id, best).Recent(Math.Max(2, 60 / step)); return; }
            }
            step = Math.Max(1, M.NetSecNow); title = L.T("Internet ping"); v = M.NetPing.Values(Math.Max(2, 60 / step));
        }
        // spikes in the open game session - so viewers see whether it was "his Wi-Fi" or not
        static int Spikes() { var gp = GamePing.Me; var ss = gp.Session; if (ss == null || !ss.Open) return -1; lock (gp.Spikes) return gp.Spikes.Count(z => z.Game == ss.GameId && z.T >= ss.Start); }
        protected override void OnPaint(PaintEventArgs e) { Paint1(e.Graphics, ClientSize); }
        void Paint1(Graphics g, Size sz)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = Draw.ImageText; g.TextContrast = Draw.Contrast;
            string title; double[] v; int step; Series(out title, out v, out step);
            g.Clear(green ? Color.FromArgb(0, 255, 0) : Color.FromArgb(20, 24, 31));
            var box = new RectangleF(4, 4, sz.Width - 8, sz.Height - 8);
            using (var bg = new SolidBrush(Color.FromArgb(230, 20, 24, 31))) using (var path = Round(box, 12)) g.FillPath(bg, path);
            double now = v.Length > 0 ? v[v.Length - 1] : double.NaN;
            var ok = v.Where(x => !double.IsNaN(x)).ToArray();
            double loss = v.Length == 0 ? double.NaN : 100.0 * v.Count(double.IsNaN) / v.Length;
            Color c = v.Length == 0 ? Color.FromArgb(150, 160, 170) : double.IsNaN(now) ? Color.FromArgb(240, 80, 80) : now > 100 ? Color.FromArgb(240, 80, 80) : now > 60 ? Color.FromArgb(240, 180, 60) : Color.FromArgb(80, 210, 120);
            using (var w = new SolidBrush(Color.FromArgb(200, 210, 220))) using (var cb = new SolidBrush(c))
            {
                g.DrawString(title, f1, w, box.X + 10, box.Y + 6);
                string big = v.Length == 0 ? L.T("waiting") : double.IsNaN(now) ? L.T("no reply") :   /* review 1.1.6 U4 #2: nothing measured yet is not "no reply" */ Math.Round(now).ToString(CultureInfo.InvariantCulture) + " ms";
                g.DrawString(big, f2, cb, box.X + 8, box.Y + 22);
                int sp = Spikes(); string sub = (sp >= 0 ? sp + " " + L.T("spikes") + " · " : "") + (ok.Length > 1 ? L.T("jitter") + " " + Math.Round(GameStat.Jitter(v)).ToString(CultureInfo.InvariantCulture) + " ms" : "") + (double.IsNaN(loss) ? "" : " · " + L.T("loss") + " " + Math.Round(loss).ToString(CultureInfo.InvariantCulture) + "%");
                g.DrawString(sub.Trim(' ', '·'), f1, w, box.X + 10, box.Bottom - 22);
            }
            // the graph: last 60 s, red marks where a reply was missing
            var gr = new RectangleF(box.X + box.Width * 0.45f, box.Y + 12, box.Width * 0.55f - 14, box.Height - 24);
            if (v.Length > 1)
            {
                double max = Math.Max(60, ok.Length > 0 ? ok.Max() * 1.15 : 60);
                float dx = gr.Width / 59f;
                // your router on the same scale, thin and grey: flat while the game line jumps = not your home network
                // both lines are placed by time (newest at the right edge, one router sample every GwSecNow s) - never squeezed
                int rstep = Math.Max(1, M.GwSecNow); var rv = M.GwPing.Values(Math.Max(2, 60 / rstep));
                bool routerOn = rv.Length > 1 && (Clock.Mono - M.GwAt).TotalSeconds < 60;
                if (routerOn)
                    using (var rp = new Pen(Color.FromArgb(150, 200, 210, 220), 1.2f) { DashStyle = DashStyle.Dash })
                    {
                        PointF? lr = null; int roff = 60 - rv.Length * rstep;
                        for (int i = 0; i < rv.Length; i++) { if (double.IsNaN(rv[i])) { lr = null; continue; } var pt = new PointF(gr.X + (roff + (i + 1) * rstep - 1) * dx, gr.Bottom - (float)(Math.Min(rv[i], max) / max) * gr.Height); if (lr != null) g.DrawLine(rp, lr.Value, pt); lr = pt; }
                    }
                if (routerOn) using (var lf = new SolidBrush(Color.FromArgb(170, 200, 210, 220))) g.DrawString(AdeeboMeter.L.T("- - router"), f1, lf, gr.Right - 64, gr.Y - 12);   // bug-hunt L-108: only with its line
                using (var pen = new Pen(Color.FromArgb(120, 170, 255), 2f)) using (var red = new SolidBrush(Color.FromArgb(240, 80, 80)))
                {
                    PointF? last = null; int off = 60 - v.Length * step;
                    for (int i = 0; i < v.Length; i++)
                    {
                        float x = gr.X + (off + (i + 1) * step - 1) * dx;
                        if (double.IsNaN(v[i])) { g.FillRectangle(red, x - 1, gr.Bottom - 6, 3, 6); last = null; continue; }
                        var pt = new PointF(x, gr.Bottom - (float)(v[i] / max) * gr.Height);
                        if (last != null) g.DrawLine(pen, last.Value, pt); last = pt;
                    }
                }
            }
        }
        void WritePng()
        {
            try
            {
                using (var bmp = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp)) Paint1(g, ClientSize);
                    // only when the picture changed: an idle graph doesn't rewrite the file 86,400 times a day
                    byte[] png; using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); png = ms.ToArray(); }
                    if (lastPng != null && png.Length == lastPng.Length && png.SequenceEqual(lastPng) && File.Exists(PngPath)) return;
                    lastPng = png;
                    string tmp = PngPath + ".tmp"; File.WriteAllBytes(tmp, png);
                    Program.ReplaceFile(tmp, PngPath);
                }
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is System.Runtime.InteropServices.ExternalException)) throw; }
        }
        static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath(); float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
            return p;
        }
    }
}
