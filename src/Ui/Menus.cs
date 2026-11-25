// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== menus in AdeeboMeter's own colours (23 Nov polish)
    // The tray menu, the Hide menu, Save/Copy and the More menus all use one renderer: the card colour, a rounded hover, the
    // app's fonts, a short line under a heading - instead of Windows' grey default. High contrast uses the user's own colours
    // (Theme maps them) and keeps the lines under the headings.
    static class Menus
    {
        public static ContextMenuStrip Make()
        {
            var m = new ContextMenuStrip(); Style(m); return m;
        }
        public static void Style(ToolStrip m)
        {
            m.Renderer = new Look(); m.Font = Draw.fBody; m.ShowItemToolTips = true;
            m.Padding = new Padding(Sc(6), Sc(6), Sc(6), Sc(6));
            var cm = m as ContextMenuStrip;
            if (cm != null) { cm.ShowImageMargin = true; cm.ImageScalingSize = new Size(Sc(18), Sc(18)); cm.Opened += delegate { Round(cm); }; }
        }
        static int Sc(float v) { return (int)Math.Round(v * Draw.S); }
        // a row with a heading and a short line under it (the Hide menu); plain rows stay one line
        public static ToolStripMenuItem Add(ToolStrip m, string title, string sub, EventHandler click)
        {
            var it = new Row(title, sub); if (click != null) it.Click += click; m.Items.Add(it); return it;
        }
        static void Round(ToolStripDropDown d)
        {
            try { using (var p = Theme.Round(new RectangleF(0, 0, d.Width, d.Height), 10 * Draw.S)) d.Region = new Region(p); } catch (ArgumentException) { }
        }
        public sealed class Row : ToolStripMenuItem
        {
            string sub;
            public string Sub { get { return sub; } }
            // the line under the heading can change while the menu is built (the tray's verdict, read on open)
            // the row grows to fit: wider first (up to 440 px), then a second line; anything longer ends in "..." (review 24 Nov:
            // "Comes back if it gets worse or something new breaks" and long tray verdicts were cut mid-word)
            public string Line
            {
                set
                {
                    sub = value ?? ""; int w = Sc(320); float lineH = Draw.fSmall.GetHeight(), lines = 1;
                    if (sub != "")
                    {
                        float tw = Math.Max(Draw.Measure(Text ?? "", Draw.fBodyB, 0).Width, Draw.Measure(sub, Draw.fSmall, 0).Width) + Sc(56);
                        if (tw > w) w = Math.Min(Sc(440), (int)Math.Ceiling(tw));
                        if (Draw.Measure(sub, Draw.fSmall, w - Sc(56)).Height > lineH * 1.5f) lines = 2;
                    }
                    SubLines = (int)lines;
                    Size = new Size(w, sub == "" ? Sc(34) : (int)Math.Ceiling(Sc(30) + lineH * lines + Sc(6)));
                }
            }
            public int SubLines;
            public Row(string title, string sub) : base(title) { AutoSize = false; Line = sub; }
        }
        sealed class Look : ToolStripRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (var b = new SolidBrush(Theme.Card)) e.Graphics.FillRectangle(b, e.AffectedBounds);
            }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new RectangleF(0.5f, 0.5f, e.ToolStrip.Width - 1.5f, e.ToolStrip.Height - 1.5f);
                Theme.Stroke(e.Graphics, r, 10 * Draw.S, Theme.Line, 1);
            }
            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Selected || !e.Item.Enabled) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new RectangleF(2 * Draw.S, 1, e.Item.Width - 4 * Draw.S, e.Item.Height - 2);
                Theme.Fill(e.Graphics, r, 8 * Draw.S, Theme.HighContrast ? SystemColors.Highlight : Theme.Soft);
            }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using (var p = new Pen(Theme.Line)) e.Graphics.DrawLine(p, 8 * Draw.S, e.Item.Height / 2f, e.Item.Width - 8 * Draw.S, e.Item.Height / 2f);
            }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                var g = e.Graphics; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                var row = e.Item as Row; bool red = e.Item.Tag as string == "exit";
                bool hcSel = Theme.HighContrast && e.Item.Selected && e.Item.Enabled;
                Color ink = hcSel ? SystemColors.HighlightText : !e.Item.Enabled && row == null ? Theme.Muted : red ? Theme.LvText(2) : Theme.Ink;
                float x = e.TextRectangle.X;
                /* review 1.1.6 U4 #8: in a mirrored (Arabic) menu the text starts at the icon side, right to left */
                bool rtl = e.Item.RightToLeft == RightToLeft.Yes || (e.Item.Owner != null && e.Item.Owner.RightToLeft == RightToLeft.Yes);
                if (rtl)
                {
                    float right = e.TextRectangle.Right, left = 10 * Draw.S;
                    using (var sf = new StringFormat(StringFormatFlags.DirectionRightToLeft) { Trimming = StringTrimming.EllipsisWord })
                    {
                        if (row != null && row.Sub != "")
                        {
                            using (var b = new SolidBrush(ink)) g.DrawString(e.Text, Draw.fBodyB, b, new RectangleF(left, 7 * Draw.S, right - left, Draw.fBodyB.GetHeight(g) + 2), sf);
                            using (var b = new SolidBrush(hcSel ? SystemColors.HighlightText : Theme.Muted)) g.DrawString(row.Sub, Draw.fSmall, b, new RectangleF(left, 26 * Draw.S, right - left, Draw.fSmall.GetHeight(g) * row.SubLines + 2), sf);
                            return;
                        }
                        var f0 = e.Item.Font ?? Draw.fBody;
                        using (var b = new SolidBrush(ink)) g.DrawString(e.Text, f0, b, new RectangleF(left, (e.Item.Height - f0.GetHeight(g)) / 2, right - left, f0.GetHeight(g) + 2), sf);
                        return;
                    }
                }
                if (row != null && row.Sub != "")
                {
                    using (var b = new SolidBrush(ink)) g.DrawString(e.Text, Draw.fBodyB, b, x, 7 * Draw.S);
                    using (var b = new SolidBrush(hcSel ? SystemColors.HighlightText : Theme.Muted))
                    using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                        g.DrawString(row.Sub, Draw.fSmall, b, new RectangleF(x, 26 * Draw.S, e.Item.Width - x - 10 * Draw.S, Draw.fSmall.GetHeight(g) * row.SubLines + 2), sf);
                    return;
                }
                var f = e.Item.Font ?? Draw.fBody;
                using (var b = new SolidBrush(ink)) g.DrawString(e.Text, f, b, x, (e.Item.Height - f.GetHeight(g)) / 2);
                var mi = e.Item as ToolStripMenuItem;
                if (mi != null && mi.ShortcutKeyDisplayString != null && mi.ShowShortcutKeys)
                {
                    var ks = g.MeasureString(mi.ShortcutKeyDisplayString, Draw.fSmall);
                    using (var b = new SolidBrush(Theme.Muted)) g.DrawString(mi.ShortcutKeyDisplayString, Draw.fSmall, b, e.Item.Width - ks.Width - 14 * Draw.S, (e.Item.Height - ks.Height) / 2);
                }
            }
            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = e.ArrowRectangle; float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, k = 3.5f * Draw.S;
                float dir = e.Item != null && (e.Item.RightToLeft == RightToLeft.Yes || (e.Item.Owner != null && e.Item.Owner.RightToLeft == RightToLeft.Yes)) ? -1 : 1;   /* U4 #8: points at its submenu */
                using (var p = new Pen(Theme.Muted, 1.6f * Draw.S)) e.Graphics.DrawLines(p, new[] { new PointF(cx - dir * k / 2, cy - k), new PointF(cx + dir * k / 2, cy), new PointF(cx - dir * k / 2, cy + k) });
            }
            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = e.ImageRectangle; float k = Math.Min(r.Width, r.Height);
                using (var p = new Pen(Theme.Blue, 2f * Draw.S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    e.Graphics.DrawLines(p, new[] { new PointF(r.X + k * 0.2f, r.Y + k * 0.52f), new PointF(r.X + k * 0.42f, r.Y + k * 0.74f), new PointF(r.X + k * 0.82f, r.Y + k * 0.3f) });
            }
            protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
            {
                if (e.Image != null) e.Graphics.DrawImage(e.Image, e.ImageRectangle);
            }
        }
    }
}
