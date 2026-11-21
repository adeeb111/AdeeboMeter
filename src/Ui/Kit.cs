// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== the drawing kit (PLAN 11.2 / 11.3)
    // One place for the 1.1.5 look: a colour per section, cards with a soft shadow instead of a border, chips, tiles, the big
    // back button and a small set of icons drawn with a few lines (nothing loaded). Pages call these instead of drawing
    // their own boxes, so a change of look is made once. All of it is plain GDI+ - drawn only when a page repaints.
    static class Kit
    {
        // ---------- colours: one accent per section, the same everywhere it appears (tab, card stripe, tool header, back button)
        public static readonly Dictionary<string, Color> Accents = new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            { "home", Theme.C("#1F6FEB") }, { "toolbox", Theme.C("#B7791F") }, { "gamer", Theme.C("#E0484A") }, { "stream", Theme.C("#5B8C0A") }, { "trade", Theme.C("#0F766E") },
            { "time", Theme.C("#16967A") }, { "learn", Theme.C("#7A6FD8") }, { "story", Theme.C("#3A86D8") },
            { "security", Theme.C("#CF4F7C") }, { "dest", Theme.C("#0E95A2") }, { "calc", Theme.C("#D0582E") },
            { "reports", Theme.C("#5F6B7A") }, { "mine", Theme.C("#8A5CD6") },
            // Toolbox questions (11.3): reach teal, slow amber, break red, network blue, join purple, keys pink, padlock green,
            // Windows grey, test traffic coral
            { "q.reach", Theme.C("#16967A") }, { "q.slow", Theme.C("#B7791F") }, { "q.break", Theme.C("#E0484A") },
            { "q.network", Theme.C("#3A86D8") }, { "q.join", Theme.C("#7A6FD8") }, { "q.keys", Theme.C("#CF4F7C") },
            { "q.padlock", Theme.C("#2E8B3E") }, { "q.windows", Theme.C("#6B7785") }, { "q.traffic", Theme.C("#D0582E") }, { "q.hw", Theme.C("#8250DF") }
        };
        public static Color Accent(string id)
        {
            Color c; if (id != null && Accents.TryGetValue(id, out c)) return Theme.HighContrast ? SystemColors.HotTrack : id == "home" ? Theme.Blue : Theme.Palette == "neon" ? Mix(c, Color.White, 0.22f) : c;   // home follows the theme's own accent; Neon lifts every accent
            return Theme.HighContrast ? SystemColors.HotTrack : Theme.Blue;
        }
        // the soft wash of an accent behind a selected tab, a chip, a tile's icon: mixed into the card colour, so it works on
        // light and dark alike
        public static Color Soft(string id) { return Theme.HighContrast ? SystemColors.Window : Mix(Theme.Card, Accent(id), Theme.Dark ? 0.22f : 0.11f); }
        // text in an accent's colour that stays readable on the card (dark mode lifts it)
        public static Color Ink(string id) { var a = Accent(id); return Theme.HighContrast ? SystemColors.WindowText : Theme.Dark ? Mix(a, Color.White, 0.35f) : Mix(a, Color.Black, 0.18f); }
        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static float S { get { return Draw.S; } }

        // ---------- the card: 16 px corners and a soft shadow (four faint layers) instead of a hard frame; an optional stripe
        // in the section's colour along the top
        public static void Shadow(Graphics g, RectangleF r, float rad)
        {
            if (Theme.HighContrast) return;
            // two soft layers (four cost twice the time for a difference nobody sees - PLAN 11.8a, 30 ms a page)
            int[] a = Theme.Dark ? new[] { 46, 18 } : new[] { 18, 8 };
            for (int i = 0; i < 2; i++)
            {
                float k = (i * 2 + 1.5f) * S;
                using (var p = Theme.Round(new RectangleF(r.X - k * 0.5f, r.Y + k * 0.6f, r.Width + k, r.Height + k * 0.5f), rad + k))
                using (var b = new SolidBrush(Color.FromArgb(a[i], 15, 23, 42))) g.FillPath(b, p);
            }
        }
        // a raised frame for a panel whose inside is already drawn: the soft shadow only outside it, a light edge on it -
        // turns the older outlined boxes into 1.1.5 cards without repainting what's inside (works in any drawing order)
        // ---------- an empty-state card: big tinted icon, a title, one line, and up to three "what you'll get" points. Returns the y below it.
        public static float Empty(Graphics g, float x, float y, float w, string icon, string accent, string title, string body, string[] points)
        {
            title = L.T(title); body = L.T(body);
            bool art = w >= 640 * S; float artW = art ? 210 * S : 0;   // wide enough: a faint sketch of what will land here
            float tw = w - 104 * S - (art ? artW + 16 * S : 0), bh = Draw.Measure(body, Draw.fBody, (int)tw).Height;
            float ph = 0; if (points != null) foreach (var p in points) ph += Draw.Measure(L.T(p), Draw.fBody, (int)(tw - 20 * S)).Height + 6 * S;
            var r = new RectangleF(x, y, w, Math.Max(108 * S, 58 * S + bh + (ph > 0 ? ph + 12 * S : 0) + 22 * S));
            Box(g, r);
            if (art) EmptyArt(g, new RectangleF(r.Right - artW - 20 * S, r.Y + 20 * S, artW, Math.Min(96 * S, r.Height - 40 * S)), icon, Accent(accent));
            var ic = new RectangleF(r.X + 24 * S, r.Y + 24 * S, 56 * S, 56 * S); Theme.Fill(g, ic, 16 * S, Soft(accent));
            Icon(g, icon, new RectangleF(ic.X + 14 * S, ic.Y + 14 * S, 28 * S, 28 * S), Accent(accent));
            float tx = r.X + 104 * S, ty = r.Y + 24 * S;
            using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(title, Draw.fTitle, ib, tx, ty);
            ty += 32 * S;
            using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(body, Draw.fBody, sb, new RectangleF(tx, ty, tw, bh + 4)); ty += bh + 12 * S;
            if (points != null)
                using (var sb = new SolidBrush(Theme.Sub))
                    foreach (var p in points)
                    {
                        string t = L.T(p); float h = Draw.Measure(t, Draw.fBody, (int)(tw - 20 * S)).Height;
                        Theme.Fill(g, new RectangleF(tx + 2 * S, ty + 6 * S, 7 * S, 7 * S), 3.5f * S, Accent(accent));
                        g.DrawString(t, Draw.fBody, sb, new RectangleF(tx + 20 * S, ty, tw - 20 * S, h + 4)); ty += h + 6 * S;
                    }
            return r.Bottom + 12 * S;
        }

        // the empty state's sketch: a ghost of the picture that fills in once there's something to show - never real data
        static void EmptyArt(Graphics g, RectangleF r, string icon, Color accent)
        {
            Theme.Fill(g, r, 12 * S, Mix(Theme.Card, accent, Theme.Dark ? 0.10f : 0.06f));
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            Color ghost = Mix(Theme.Card, accent, 0.55f), faint = Mix(Theme.Card, Theme.Muted, 0.45f);
            float x0 = r.X + 14 * S, x1 = r.Right - 14 * S, base0 = r.Bottom - 18 * S, top = r.Y + 16 * S, h = base0 - top;
            using (var bl = new Pen(faint, 1.5f * S) { DashStyle = DashStyle.Dot }) g.DrawLine(bl, x0, base0, x1, base0);
            using (var p = new Pen(ghost, 2.5f * S) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var dip = new SolidBrush(Mix(Theme.Card, Theme.Amber, 0.45f)))
            {
                int n = 24; var pts = new List<PointF>(); var rnd = new[] { 0.62, 0.66, 0.64, 0.7, 0.68, 0.71, 0.69, 0.3, 0.25, 0.6, 0.67, 0.7, 0.72, 0.69, 0.7, 0.66, 0.68, 0.35, 0.62, 0.7, 0.71, 0.69, 0.72, 0.7 };
                switch (icon)
                {
                    case "time": case "app":   // a stream's record: steady, with two dips marked
                        for (int i = 0; i < n; i++) pts.Add(new PointF(x0 + (x1 - x0) * i / (n - 1), base0 - h * (float)rnd[i]));
                        g.FillRectangle(dip, pts[7].X - 4 * S, top, pts[8].X - pts[7].X + 8 * S, h); g.FillRectangle(dip, pts[17].X - 4 * S, top, 8 * S, h);
                        g.DrawLines(p, pts.ToArray()); break;
                    case "traffic":   // bars of who's using what
                        float bw = (x1 - x0) / 9f; var hs = new[] { 0.9f, 0.62f, 0.45f, 0.3f, 0.22f, 0.15f, 0.1f };
                        for (int i = 0; i < hs.Length; i++) Theme.Fill(g, new RectangleF(x0 + i * bw * 1.25f, base0 - h * hs[i], bw, h * hs[i]), 3 * S, ghost);
                        break;
                    case "story":   // a connection, box by box
                        for (int i = 0; i < 4; i++) { float cx = x0 + 12 * S + (x1 - x0 - 24 * S) * i / 3f; Theme.Stroke(g, new RectangleF(cx - 12 * S, top + h / 2 - 12 * S, 24 * S, 24 * S), 6 * S, ghost, 2 * S); if (i < 3) g.DrawLine(p, cx + 16 * S, top + h / 2, cx + (x1 - x0 - 24 * S) / 3f - 16 * S, top + h / 2); }
                        break;
                    default:   // a health ring and a few lines
                        float d = Math.Min(h, 64 * S); var ring = new RectangleF(x0, top + (h - d) / 2, d, d);
                        using (var rp = new Pen(faint, 6 * S)) g.DrawEllipse(rp, ring); using (var rp = new Pen(ghost, 6 * S) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(rp, ring, -90, 250);
                        for (int i = 0; i < 3; i++) Theme.Fill(g, new RectangleF(x0 + d + 16 * S, top + (h - d) / 2 + 8 * S + i * 18 * S, (x1 - x0 - d - 16 * S) * (i == 0 ? 0.9f : i == 1 ? 0.7f : 0.5f), 7 * S), 3.5f * S, faint);
                        break;
                }
            }
            g.SmoothingMode = sm;
        }

        // a raised card: theme card colour, soft shadow, hairline edge, rounded
        public static void Box(Graphics g, RectangleF r) { Box(g, r, 16 * S); }
        public static void Box(Graphics g, RectangleF r, float rad) { if (Off(g, r)) return; Theme.Fill(g, r, rad, Theme.Card); Frame(g, r, rad); }
        public static void Frame(Graphics g, RectangleF r) { Frame(g, r, 16 * S); }
        public static void Frame(Graphics g, RectangleF r, float rad)
        {
            if (Off(g, r)) return;
            var st = g.Save();
            using (var inside = Theme.Round(r, rad)) using (var rg = new Region(inside)) { g.ExcludeClip(rg); Shadow(g, r, rad); }
            g.Restore(st);
            Theme.Stroke(g, r, rad, Theme.Dark ? Theme.Line : Theme.Line2, 1);
        }
        // off screen (scrolled away): nothing to draw
        public static bool Off(Graphics g, RectangleF r) { var c = g.ClipBounds; return r.Bottom + 8 * S < c.Top || r.Top - 8 * S > c.Bottom || r.Right + 8 * S < c.Left || r.Left - 8 * S > c.Right; }
        public static void Card(Graphics g, RectangleF r, string accent, bool hot)
        {
            if (Off(g, r)) return;
            float rad = 16 * S;
            Shadow(g, r, rad);
            Theme.Fill(g, r, rad, Theme.Card);
            Theme.Stroke(g, r, rad, hot && accent != null ? Accent(accent) : Theme.Dark ? Theme.Line : Theme.Line2, hot ? 1.5f * S : 1);
            if (accent != null && Theme.Palette == "neon" && !Theme.HighContrast)   // Neon: the accent edge glows a little - two faint strokes, drawn once
            {
                using (var p = Theme.Round(new RectangleF(r.X - 1.5f * S, r.Y - 1.5f * S, r.Width + 3 * S, r.Height + 3 * S), rad + 1.5f * S)) using (var pen = new Pen(Color.FromArgb(40, Accent(accent)), 3 * S)) g.DrawPath(pen, p);
                Theme.Stroke(g, r, rad, Color.FromArgb(150, Accent(accent)), 1);
            }
            if (accent != null)
            {
                var st = g.Save();
                using (var clip = Theme.Round(r, rad)) { g.SetClip(clip); using (var b = new SolidBrush(Accent(accent))) g.FillRectangle(b, r.X, r.Y, r.Width, 5 * S); }
                g.Restore(st);
            }
        }

        // ---------- chips: a rounded pill with optional icon. Returns the chip's rectangle so the page can make it clickable.
        public static RectangleF Chip(Graphics g, string text, float x, float y, string accent, bool on, string icon)
        {
            var f = Draw.fPill; var sz = g.MeasureString(text, f);
            float ih = icon != null ? 16 * S : 0, h = 28 * S, w = sz.Width + 22 * S + ih;
            var r = new RectangleF(x, y, w, h);
            Theme.Fill(g, r, h / 2, on ? Soft(accent) : Theme.Card);
            Theme.Stroke(g, r, h / 2, on ? Accent(accent) : Theme.Line, on ? 1.5f * S : 1);
            Color fg = on ? Ink(accent) : Theme.Sub;
            if (icon != null) Icon(g, icon, new RectangleF(x + 10 * S, y + (h - 14 * S) / 2, 14 * S, 14 * S), on ? Accent(accent) : Theme.Muted);
            using (var b = new SolidBrush(fg)) g.DrawString(text, f, b, x + 11 * S + ih, y + (h - sz.Height) / 2);
            return r;
        }

        // ---------- a tile: round icon, bold title, a 3-word hint. Used for the "What's wrong?" picks, the Security checkup,
        // the More menu and anywhere a choice was a paragraph.
        public static void Tile(Graphics g, RectangleF r, string icon, string title, string hint, string accent, bool hot, int level)
        {
            if (Off(g, r)) return;
            Card(g, r, null, hot);
            if (hot) Theme.Stroke(g, r, 16 * S, Accent(accent), 1.5f * S);
            float d = Math.Min(38 * S, r.Height - 20 * S), cx = L.IsRtl ? r.Right - 14 * S - d : r.X + 14 * S, cy = r.Y + (r.Height - d) / 2;
            Color ring = level >= 0 && level <= 2 ? Theme.LvSoft(level) : Soft(accent), ic = level >= 0 && level <= 2 ? Theme.Lv(level) : Accent(accent);
            using (var b = new SolidBrush(ring)) g.FillEllipse(b, cx, cy, d, d);
            Icon(g, icon, new RectangleF(cx + d * 0.24f, cy + d * 0.24f, d * 0.52f, d * 0.52f), ic);
            float tx = L.IsRtl ? r.X + 10 * S : cx + d + 12 * S, tw = r.Width - d - 36 * S;
            using (var b = new SolidBrush(Theme.Ink)) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (L.IsRtl) sf.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
                bool two = !string.IsNullOrEmpty(hint);
                float th = Draw.fHead.GetHeight(g), hh = Draw.fSmall.GetHeight(g), top = r.Y + (r.Height - (two ? th + hh + 2 * S : th)) / 2;
                g.DrawString(L.T(title), Draw.fHead, b, new RectangleF(tx, top, tw, th + 2), sf);
                if (two) using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T(hint), Draw.fSmall, m, new RectangleF(tx, top + th + 2 * S, tw, hh + 2), sf);
            }
        }

        // ---------- the big back button (11.6): an arrow and where it goes, in that section's colour. Returns its rectangle.
        public static RectangleF Back(Graphics g, string text, float x, float y, string accent, bool hot)
        {
            text = L.T(text); var f = Draw.fHead; var sz = g.MeasureString(text, f);
            var r = new RectangleF(x, y, sz.Width + 46 * S, 36 * S);
            Theme.Fill(g, r, 10 * S, hot ? Soft(accent) : Theme.Soft);
            Icon(g, "back", new RectangleF(x + 12 * S, y + 10 * S, 16 * S, 16 * S), hot ? Accent(accent) : Theme.Sub);
            using (var b = new SolidBrush(hot ? Ink(accent) : Theme.Ink)) g.DrawString(text, f, b, x + 34 * S, y + (r.Height - sz.Height) / 2);
            return r;
        }

        // ---------- a section header: coloured bar, icon, title, a short hint
        public static float Header(Graphics g, float x, float y, float w, string icon, string title, string hint, string accent)
        {
            if (L.IsRtl)
            {
                using (var bar = new SolidBrush(Accent(accent))) g.FillRectangle(bar, x + w - 3 * S, y + 2 * S, 3 * S, 34 * S);
                Icon(g, icon, new RectangleF(x + w - 36 * S, y + 7 * S, 24 * S, 24 * S), Accent(accent));
                using (var sf = new StringFormat { FormatFlags = StringFormatFlags.DirectionRightToLeft, Trimming = StringTrimming.EllipsisCharacter })
                {
                    using (var b = new SolidBrush(Theme.Ink)) g.DrawString(L.T(title), Draw.fTitle, b, new RectangleF(x, y, w - 44 * S, 24 * S), sf);
                    if (!string.IsNullOrEmpty(hint)) using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T(hint), Draw.fSmall, m, new RectangleF(x, y + 24 * S, w - 45 * S, 22 * S), sf);
                }
                return y + 50 * S;
            }
            using (var b = new SolidBrush(Accent(accent))) g.FillRectangle(b, x, y + 2 * S, 3 * S, 34 * S);
            Icon(g, icon, new RectangleF(x + 12 * S, y + 7 * S, 24 * S, 24 * S), Accent(accent));
            using (var b = new SolidBrush(Theme.Ink)) g.DrawString(L.T(title), Draw.fTitle, b, x + 44 * S, y);
            if (!string.IsNullOrEmpty(hint)) using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T(hint), Draw.fSmall, m, x + 45 * S, y + 21 * S);
            return y + 46 * S;
        }

        // ---------- icons: a small set drawn with lines, sized to any square. Unknown names draw a dot, never nothing.
        public static void Icon(Graphics g, string name, RectangleF r, Color c)
        {
            float u = r.Width / 24f; Func<float, float> X = v => r.X + v * u, Y = v => r.Y + v * u;
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(c, Math.Max(1.2f, 2f * u)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var b = new SolidBrush(c))
            {
                switch (name)
                {
                    case "home": g.DrawLines(p, new[] { new PointF(X(4), Y(11)), new PointF(X(12), Y(4)), new PointF(X(20), Y(11)) }); g.DrawLines(p, new[] { new PointF(X(6), Y(9.5f)), new PointF(X(6), Y(20)), new PointF(X(18), Y(20)), new PointF(X(18), Y(9.5f)) }); g.DrawRectangle(p, X(10), Y(14), 4 * u, 6 * u); break;
                    case "tool": g.DrawLine(p, X(5), Y(19), X(14), Y(10)); g.DrawArc(p, X(12), Y(3), 9 * u, 9 * u, 100, 290); break;
                    case "game": g.DrawArc(p, X(3), Y(7), 18 * u, 11 * u, 0, 360); g.DrawLine(p, X(8), Y(10.5f), X(8), Y(14.5f)); g.DrawLine(p, X(6), Y(12.5f), X(10), Y(12.5f)); g.FillEllipse(b, X(15), Y(10), 2 * u, 2 * u); g.FillEllipse(b, X(17), Y(13), 2 * u, 2 * u); break;
                    case "outlook": g.DrawEllipse(p, X(3), Y(3), 9 * u, 9 * u); g.DrawLines(p, new[] { new PointF(X(9), Y(20)), new PointF(X(19), Y(20)) }); g.DrawArc(p, X(13), Y(11), 8 * u, 9 * u, 180, 270); g.DrawArc(p, X(7), Y(13), 8 * u, 7 * u, 90, 180); break;   // a sun behind a cloud (1.1.6 Outlook)
                    case "time": g.DrawEllipse(p, X(4), Y(4), 16 * u, 16 * u); g.DrawLines(p, new[] { new PointF(X(12), Y(8)), new PointF(X(12), Y(12)), new PointF(X(15), Y(14)) }); break;
                    case "learn": g.DrawLines(p, new[] { new PointF(X(2), Y(9)), new PointF(X(12), Y(5)), new PointF(X(22), Y(9)), new PointF(X(12), Y(13)), new PointF(X(2), Y(9)) }); g.DrawArc(p, X(6), Y(8), 12 * u, 10 * u, 0, 180); break;
                    case "more": for (int i = 0; i < 3; i++) g.FillEllipse(b, X(5 + i * 6), Y(10.5f), 3 * u, 3 * u); break;
                    case "search": g.DrawEllipse(p, X(4), Y(4), 12 * u, 12 * u); g.DrawLine(p, X(14.5f), Y(14.5f), X(20), Y(20)); break;
                    case "back": g.DrawLine(p, X(5), Y(12), X(19), Y(12)); g.DrawLines(p, new[] { new PointF(X(11), Y(6)), new PointF(X(5), Y(12)), new PointF(X(11), Y(18)) }); break;
                    case "go": g.DrawLine(p, X(5), Y(12), X(19), Y(12)); g.DrawLines(p, new[] { new PointF(X(13), Y(6)), new PointF(X(19), Y(12)), new PointF(X(13), Y(18)) }); break;
                    case "wifi": g.DrawArc(p, X(3), Y(6), 18 * u, 18 * u, 225, 90); g.DrawArc(p, X(6.5f), Y(9.5f), 11 * u, 11 * u, 225, 90); g.FillEllipse(b, X(10.5f), Y(16), 3 * u, 3 * u); break;
                    case "wifioff": g.DrawArc(p, X(3), Y(6), 18 * u, 18 * u, 225, 90); g.FillEllipse(b, X(10.5f), Y(16), 3 * u, 3 * u); g.DrawLine(p, X(4), Y(4), X(20), Y(20)); break;
                    case "shield": g.DrawLines(p, new[] { new PointF(X(12), Y(3)), new PointF(X(20), Y(6)), new PointF(X(19), Y(13)), new PointF(X(12), Y(21)), new PointF(X(5), Y(13)), new PointF(X(4), Y(6)), new PointF(X(12), Y(3)) }); g.DrawLines(p, new[] { new PointF(X(8.5f), Y(12)), new PointF(X(11), Y(14.5f)), new PointF(X(15.5f), Y(9.5f)) }); break;
                    case "story": g.DrawEllipse(p, X(3), Y(15), 5 * u, 5 * u); g.DrawEllipse(p, X(16), Y(4), 5 * u, 5 * u); g.DrawBezier(p, X(7), Y(16), X(12), Y(12), X(10), Y(9), X(16), Y(7.5f)); break;
                    case "dest": g.DrawEllipse(p, X(4), Y(4), 16 * u, 16 * u); g.DrawEllipse(p, X(9), Y(9), 6 * u, 6 * u); g.FillEllipse(b, X(11), Y(11), 2 * u, 2 * u); break;
                    case "calc": g.DrawRectangle(p, X(5), Y(3), 14 * u, 18 * u); g.DrawLine(p, X(8), Y(7), X(16), Y(7)); for (int i = 0; i < 3; i++) for (int j = 0; j < 2; j++) g.FillEllipse(b, X(8 + j * 6), Y(11 + i * 3.5f), 2 * u, 2 * u); break;
                    case "report": g.DrawLines(p, new[] { new PointF(X(14), Y(3)), new PointF(X(6), Y(3)), new PointF(X(6), Y(21)), new PointF(X(18), Y(21)), new PointF(X(18), Y(7)), new PointF(X(14), Y(3)) }); g.DrawLine(p, X(9), Y(12), X(15), Y(12)); g.DrawLine(p, X(9), Y(16), X(15), Y(16)); break;
                    case "speed": g.DrawArc(p, X(3), Y(5), 18 * u, 18 * u, 180, 180); g.DrawLine(p, X(12), Y(14), X(17), Y(9)); break;
                    case "route": g.FillEllipse(b, X(4), Y(16), 4 * u, 4 * u); g.FillEllipse(b, X(16), Y(4), 4 * u, 4 * u); g.DrawLines(p, new[] { new PointF(X(6), Y(16)), new PointF(X(6), Y(11)), new PointF(X(18), Y(11)), new PointF(X(18), Y(8)) }); break;
                    case "link":
                        {
                            // two chain links at 45 degrees - the phone link button (16 Nov)
                            var st = g.Save(); g.TranslateTransform(X(12), Y(12)); g.RotateTransform(-45);
                            foreach (float ox in new[] { -10.5f, -1.5f })
                                using (var cp = new System.Drawing.Drawing2D.GraphicsPath())
                                {
                                    float cx0 = ox * u, cy0 = -3.5f * u, cw = 12 * u, chh = 7 * u, d = 7 * u;
                                    cp.AddArc(cx0, cy0, d, d, 90, 180); cp.AddArc(cx0 + cw - d, cy0, d, d, 270, 180); cp.CloseFigure(); g.DrawPath(p, cp);
                                }
                            g.Restore(st); break;
                        }
                    case "lock": g.DrawRectangle(p, X(5), Y(11), 14 * u, 10 * u); g.DrawArc(p, X(8), Y(4), 8 * u, 10 * u, 180, 180); break;
                    case "key": g.DrawEllipse(p, X(3), Y(9), 7 * u, 7 * u); g.DrawLine(p, X(10), Y(12.5f), X(21), Y(12.5f)); g.DrawLine(p, X(18), Y(12.5f), X(18), Y(16)); break;
                    case "devices": g.DrawRectangle(p, X(3), Y(5), 13 * u, 9 * u); g.DrawLine(p, X(7), Y(18), X(12), Y(18)); g.DrawRectangle(p, X(17), Y(9), 4 * u, 10 * u); break;
                    case "join": g.DrawLines(p, new[] { new PointF(X(4), Y(12)), new PointF(X(9), Y(12)) }); g.DrawRectangle(p, X(9), Y(8), 6 * u, 8 * u); g.DrawLine(p, X(15), Y(12), X(20), Y(12)); break;
                    case "windows": g.DrawRectangle(p, X(4), Y(4), 7 * u, 7 * u); g.DrawRectangle(p, X(13), Y(4), 7 * u, 7 * u); g.DrawRectangle(p, X(4), Y(13), 7 * u, 7 * u); g.DrawRectangle(p, X(13), Y(13), 7 * u, 7 * u); break;
                    case "traffic": g.DrawLine(p, X(4), Y(8), X(18), Y(8)); g.DrawLines(p, new[] { new PointF(X(15), Y(5)), new PointF(X(18), Y(8)), new PointF(X(15), Y(11)) }); g.DrawLine(p, X(20), Y(16), X(6), Y(16)); g.DrawLines(p, new[] { new PointF(X(9), Y(13)), new PointF(X(6), Y(16)), new PointF(X(9), Y(19)) }); break;
                    case "cpu": g.DrawRectangle(p, X(6), Y(6), 12 * u, 12 * u); for (int i = 0; i < 3; i++) { float v = 9 + i * 3; g.DrawLine(p, X(v), Y(3), X(v), Y(6)); g.DrawLine(p, X(v), Y(18), X(v), Y(21)); } break;
                    case "question": g.DrawArc(p, X(8), Y(4), 8 * u, 8 * u, 180, 270); g.DrawLine(p, X(12), Y(12), X(12), Y(15)); g.FillEllipse(b, X(11), Y(18), 2 * u, 2 * u); break;
                    case "phone": g.DrawBezier(p, X(6), Y(4), X(3), Y(12), X(12), Y(21), X(20), Y(18)); g.DrawLine(p, X(6), Y(4), X(9), Y(8)); g.DrawLine(p, X(20), Y(18), X(16), Y(15)); break;
                    case "app": g.DrawRectangle(p, X(3), Y(5), 18 * u, 14 * u); g.DrawLine(p, X(3), Y(9), X(21), Y(9)); break;
                    case "pin": g.DrawEllipse(p, X(8), Y(3), 8 * u, 8 * u); g.DrawLine(p, X(12), Y(11), X(12), Y(21)); break;
                    case "plus": g.DrawLine(p, X(12), Y(5), X(12), Y(19)); g.DrawLine(p, X(5), Y(12), X(19), Y(12)); break;
                    case "check": g.DrawLines(p, new[] { new PointF(X(5), Y(12.5f)), new PointF(X(10), Y(17)), new PointF(X(19), Y(7)) }); break;
                    case "star": g.DrawPolygon(p, new[] { new PointF(X(12), Y(3)), new PointF(X(14.6f), Y(9)), new PointF(X(21), Y(9.5f)), new PointF(X(16), Y(13.7f)), new PointF(X(17.6f), Y(20)), new PointF(X(12), Y(16.6f)), new PointF(X(6.4f), Y(20)), new PointF(X(8), Y(13.7f)), new PointF(X(3), Y(9.5f)), new PointF(X(9.4f), Y(9)) }); break;
                    case "mood": g.DrawEllipse(p, X(3), Y(3), 18 * u, 18 * u); g.FillEllipse(b, X(8), Y(8.5f), 2.2f * u, 2.2f * u); g.FillEllipse(b, X(13.8f), Y(8.5f), 2.2f * u, 2.2f * u); g.DrawArc(p, X(7.5f), Y(9), 9 * u, 8 * u, 20, 140); break;
                    case "settings":
                        {
                            // a gear: a ring with eight square teeth and a hole - reads as a gear, not a star
                            using (var tp = new Pen(c, 2.6f * u) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                                for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(tp, X(12 + (float)Math.Cos(a) * 7.4f), Y(12 + (float)Math.Sin(a) * 7.4f), X(12 + (float)Math.Cos(a) * 10f), Y(12 + (float)Math.Sin(a) * 10f)); }
                            g.DrawEllipse(p, X(5), Y(5), 14 * u, 14 * u); g.DrawEllipse(p, X(9.25f), Y(9.25f), 5.5f * u, 5.5f * u);
                        }
                        break;
                    case "layout": g.DrawRectangle(p, X(3), Y(4), 18 * u, 16 * u); g.DrawLine(p, X(9), Y(4), X(9), Y(20)); break;
                    default: g.FillEllipse(b, X(9), Y(9), 6 * u, 6 * u); break;
                }
            }
            g.SmoothingMode = sm;
        }
    }
}
