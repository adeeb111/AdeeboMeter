using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Health › Look deeper (Adeeb, 22 Nov): the PC checks as mid-size tiles - an icon in its colour, the name, one line -
    // four across on a wide window. Hover lifts the edge into the colour; only that tile is drawn again.
    sealed partial class FullView
    {
        DB deeperCanvas;
        static readonly string[][] Deeper = {
            new[] { "pressure", "Memory, disk, heat", "pressure right now", "pc", "#1D9E75" },
            new[] { "disk", "Disk", "who uses it, health, space", "database", "#378ADD" },
            new[] { "space", "Where did my space go?", "the usual suspects", "cabinet", "#BA7517" },
            new[] { "bg", "Background jobs", "updates, sync, indexing", "clock", "#7F77DD" },
            new[] { "crash", "Crashes and hangs", "the last 7 days", "shield", "#E24B4A" },
            new[] { "leaks", "Slow leaks", "memory that only grows", "chart", "#D4537E" },
            new[] { "gpu", "Graphics per app", "who draws the most", "laptop", "#1D9E75" },
            new[] { "changed", "What changed (7 days)", "installs and updates", "tool", "#378ADD" },
            new[] { "battery", "Battery", "health and charge cycles", "bolt", "#BA7517" } };
        RectangleF[] deeperRect = new RectangleF[0]; int deeperHot = -1;
        int DeeperCols() { int w = deeperCanvas == null ? 800 : deeperCanvas.ClientSize.Width - Sc(28); return Math.Max(1, Math.Min(4, w / Sc(250))); }
        int DeeperHeight() { int rows = (Deeper.Length + DeeperCols() - 1) / DeeperCols(); return Sc(30) + rows * Sc(66) + Sc(8); }
        void PaintDeeper(Graphics g)
        {
            Begin(deeperCanvas);
            float x = 14 * S, w = deeperCanvas.ClientSize.Width - 28 * S, y = 4 * S;
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Kit.Sent("LOOK DEEPER"), Draw.fTiny, mb, x + 2 * S, y);
            y += 22 * S;
            int cols = DeeperCols(); float tw = (w - (cols - 1) * 10 * S) / cols, th = 56 * S;
            if (deeperRect.Length != Deeper.Length) deeperRect = new RectangleF[Deeper.Length];
            for (int i = 0; i < Deeper.Length; i++)
            {
                var r = new RectangleF(x + (i % cols) * (tw + 10 * S), y + (i / cols) * (th + 10 * S), tw, th); deeperRect[i] = r;
                DeeperTile(g, i, r, i == deeperHot);
                var d = Deeper[i]; string id = d[0], title = d[1]; Act(deeperCanvas, r, delegate { RunTool("tb:" + id, L.T(title)); }); Hint(deeperCanvas, r, "tb." + id);
            }
        }
        void DeeperTile(Graphics g, int i, RectangleF r, bool hot)
        {
            var d = Deeper[i]; Color c = Hex(d[4]);
            Theme.Fill(g, r, 14 * S, Theme.Card);
            if (hot) Theme.Fill(g, r, 14 * S, Color.FromArgb(Theme.Dark ? 22 : 14, c));   // white at rest, a faint tint under the mouse (24 Nov)
            Theme.Stroke(g, r, 14 * S, hot ? c : (Theme.Dark ? Theme.Line : Theme.Line2), 1);
            float ic = 34 * S; var ir = new RectangleF(r.X + 12 * S, r.Y + (r.Height - ic) / 2, ic, ic);
            Glyphs.Tile(g, d[3], ir, c, 9 * S);
            float tx = ir.Right + 10 * S, tw = r.Right - tx - 8 * S;
            using (var one = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            using (var ink = new SolidBrush(Theme.Ink)) using (var mut = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T(d[1]), Draw.fBodyB, ink, new RectangleF(tx, r.Y + 9 * S, tw, 22 * S), one);
                g.DrawString(L.T(d[2]), Draw.fSmall, mut, new RectangleF(tx, r.Y + 30 * S, tw, 20 * S), one);
            }
        }
        void DeeperHover(Point p)
        {
            int hit = -1; for (int i = 0; i < deeperRect.Length; i++) if (deeperRect[i].Contains(p)) { hit = i; break; }
            if (hit == deeperHot) return;
            int was = deeperHot; deeperHot = hit;
            foreach (int k in new[] { was, hit }) if (k >= 0 && k < deeperRect.Length) deeperCanvas.Invalidate(Rectangle.Round(RectangleF.Inflate(deeperRect[k], 3, 3)));
        }
    }
}
