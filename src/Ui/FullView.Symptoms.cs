using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // "Or tap what you see" (Adeeb, 22 Nov): each kind of problem has a faded colour and an icon; under the mouse the tile lifts
    // a little, its edge takes the colour and an arrow slides in (150 ms). The animation draws only that tile, straight onto the
    // window - never the whole page - so hovering costs about a millisecond a frame.
    sealed partial class FullView
    {
        // colour of each kind: no connection, speed, calls, this PC, time, one site
        static readonly Color SymRed = Color.FromArgb(0xE2, 0x4B, 0x4A), SymAmber = Color.FromArgb(0xBA, 0x75, 0x17), SymPurple = Color.FromArgb(0x7F, 0x77, 0xDD),
                              SymTeal = Color.FromArgb(0x1D, 0x9E, 0x75), SymBlue = Color.FromArgb(0x37, 0x8A, 0xDD), SymPink = Color.FromArgb(0xD4, 0x53, 0x7E);
        // icon and colour for Symptoms[i], same order
        static readonly string[] SymIcon = { "internet", "bolt", "wifi", "route", "play", "wifi", "phone", "pc", "laptop", "tool", "clock", "server" };
        static readonly Color[] SymColor = { SymRed, SymAmber, SymRed, SymRed, SymAmber, SymRed, SymPurple, SymTeal, SymTeal, SymTeal, SymBlue, SymPink };
        RectangleF[] symRect;   // where each tile was painted, in page coordinates
        readonly float[] symHot = new float[16]; int symHover = -1; Timer symAnim;

        void DrawSymptom(Graphics g, int i, RectangleF br, float t)
        {
            Color c = i < SymColor.Length ? SymColor[i] : Theme.Blue; string icon = i < SymIcon.Length ? SymIcon[i] : "star";
            var r = br; r.Offset(0, -2 * S * t);
            Theme.Fill(g, r, 16 * S, Theme.Card);
            Theme.Fill(g, r, 16 * S, Color.FromArgb((int)((Theme.Dark ? 22 : 16) + 14 * t), c));
            if (t > 0.01f) Theme.Stroke(g, r, 16 * S, Color.FromArgb((int)(255 * t), c), 1); else Theme.Stroke(g, r, 16 * S, Theme.Dark ? Theme.Line : Theme.Line2, 1);
            float ic = 38 * S, grow = 3 * S * t; var ir = new RectangleF(r.X + 14 * S - grow / 2, r.Y + (r.Height - ic) / 2 - grow / 2, ic + grow, ic + grow);
            Glyphs.Tile(g, icon, ir, c, 10 * S);
            float tx = r.X + 14 * S + ic + 12 * S, tw = r.Right - tx - 30 * S;
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(L.T(Symptoms[i][1]), Draw.fBodyB, ink, new RectangleF(tx, r.Y + 12 * S, tw, 22 * S));
            using (var muted = new SolidBrush(Theme.Muted)) using (var ef = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                g.DrawString(L.T(Symptoms[i][2]), Draw.fSmall, muted, new RectangleF(tx, r.Y + 35 * S, tw, Math.Max(18 * S, r.Height - 40 * S)), ef);
            if (t > 0.01f)
            {
                float ax = r.Right - 22 * S - 4 * S * (1 - t), ay = r.Y + r.Height / 2;
                using (var p = new Pen(Color.FromArgb((int)(255 * t), c), 2 * S) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                    g.DrawLines(p, new[] { new PointF(ax, ay - 6 * S), new PointF(ax + 6 * S, ay), new PointF(ax, ay + 6 * S) });
            }
        }
        // the mouse over Home: which tile is under it
        void SymMouse(Point client)
        {
            int hit = -1; var rs = symRect;
            if (rs != null && tab == "overview") { var sp = overviewCanvas.AutoScrollPosition; var pt = new PointF(client.X - sp.X, client.Y - sp.Y); for (int i = 0; i < rs.Length; i++) if (rs[i].Contains(pt)) { hit = i; break; } }
            if (hit == symHover) return; symHover = hit;
            if (symAnim == null) { symAnim = new Timer { Interval = 15 }; symAnim.Tick += delegate { SymStep(); }; }
            if (!symAnim.Enabled) symAnim.Start();
        }
        void SymStep()
        {
            var rs = symRect; bool moving = false;
            if (rs == null || overviewCanvas == null || overviewCanvas.IsDisposed || !overviewCanvas.Visible) { symAnim.Stop(); return; }
            for (int i = 0; i < rs.Length && i < symHot.Length; i++)
            {
                float want = i == symHover ? 1f : 0f, was = symHot[i]; if (was == want) continue;
                float now = !Typing.On ? want : want > was ? Math.Min(1f, was + 0.1f) : Math.Max(0f, was - 0.1f);   /* Windows' animations off: straight to the end */ symHot[i] = now; moving = true;
                SymPaintOne(i, now);
            }
            if (!moving) symAnim.Stop();
        }
        // one tile, double-buffered, straight onto the canvas (its card's colour behind it first)
        void SymPaintOne(int i, float t)
        {
            var sp = overviewCanvas.AutoScrollPosition; var br = symRect[i];
            var area = Rectangle.Round(RectangleF.Inflate(new RectangleF(br.X + sp.X, br.Y + sp.Y, br.Width, br.Height), 4 * S, 6 * S));
            area.Intersect(overviewCanvas.ClientRectangle); if (area.Width <= 0 || area.Height <= 0) return;
            using (var cg = overviewCanvas.CreateGraphics())
            using (var buf = BufferedGraphicsManager.Current.Allocate(cg, area))
            {
                var g = buf.Graphics; Prep(g);
                using (var bb = new SolidBrush(Theme.Card)) g.FillRectangle(bb, area);
                g.TranslateTransform(sp.X, sp.Y); DrawSymptom(g, i, br, t);
                buf.Render(cg);
            }
        }
    }
}
