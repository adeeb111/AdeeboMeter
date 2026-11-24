// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== The newspaper look (1.1.6): Sports and Markets
    // Cream paper (the default), white, or the app's own light/dark theme (Settings › Markets and Sports › Page paper).
    // A masthead with laurels, a tab bar with the current tab in navy, desks with a red rule on top, green pills, and the
    // Stream Check / Line Check strip that sits under every tab. High contrast always wins: system colours, no texture.
    static class Paper
    {
        static float S { get { return Draw.S; } }
        // 24 Nov (Adeeb approved the app-style mockups): "app" draws these pages in the app's own cards, type and colours; the
        // newspaper (cream, white, or the theme's colours) stays one switch away in Settings
        public static bool App { get { return Theme.HighContrast || Cfg.Paper == "app" || string.IsNullOrEmpty(Cfg.Paper); } }
        public static bool Dark { get { return !Theme.HighContrast && Cfg.Paper == "theme" && Theme.Dark; } }
        public static bool Cream { get { return !Theme.HighContrast && Cfg.Paper == "cream"; } }
        static Color C(string h) { return ColorTranslator.FromHtml(h); }
        public static Color Bg { get { return App && !Theme.HighContrast ? Theme.Bg : Theme.HighContrast ? SystemColors.Window : Dark ? C("#0E1319") : Cream ? C("#F3EBDA") : C("#FFFFFF"); } }
        public static Color DeskBg { get { return App && !Theme.HighContrast ? Theme.Card : Theme.HighContrast ? SystemColors.Window : Dark ? C("#161D26") : Cream ? C("#FBF6EA") : C("#FFFFFF"); } }
        public static Color Line { get { return App && !Theme.HighContrast ? Theme.Line : Theme.HighContrast ? SystemColors.WindowText : Dark ? C("#2E3A46") : Cream ? C("#CDBF9F") : C("#D6DEE8"); } }
        public static Color Line2 { get { return App && !Theme.HighContrast ? Theme.Line2 : Theme.HighContrast ? SystemColors.GrayText : Dark ? C("#2E3A46") : Cream ? C("#E2D7BE") : C("#EEF2F6"); } }
        public static Color Head { get { return App && !Theme.HighContrast ? Theme.Ink : Theme.HighContrast ? SystemColors.WindowText : Dark ? C("#EEF2F6") : C("#141B2B"); } }
        public static Color Ink { get { return App && !Theme.HighContrast ? Theme.Ink : Theme.HighContrast ? SystemColors.WindowText : Dark ? C("#E6EDF3") : C("#1F2633"); } }
        public static Color Muted { get { return App && !Theme.HighContrast ? Theme.Sub : Theme.HighContrast ? SystemColors.GrayText : Dark ? C("#9AA7B4") : Cream ? C("#6B5E44") : C("#5B6B7C"); } }
        public static Color Red { get { return App && !Theme.HighContrast ? Theme.LvText(2) : Theme.HighContrast ? SystemColors.Highlight : Dark ? C("#E5534B") : C("#B3261E"); } }
        public static Color Navy { get { return App && !Theme.HighContrast ? Theme.Blue : Theme.HighContrast ? SystemColors.Highlight : Dark ? C("#2563EB") : C("#0F2A5C"); } }
        public static Color Green { get { return App && !Theme.HighContrast ? Theme.Blue : Theme.HighContrast ? SystemColors.Highlight : C("#2E8B3A"); } }
        public static Color Up { get { return App && !Theme.HighContrast ? Theme.LvText(0) : Theme.HighContrast ? SystemColors.WindowText : Dark ? C("#7EE2A8") : C("#1E7A30"); } }
        public static Color Down { get { return App && !Theme.HighContrast ? Theme.LvText(2) : Theme.HighContrast ? SystemColors.WindowText : Dark ? C("#FFA198") : C("#B3261E"); } }
        public static Color Amber { get { return App && !Theme.HighContrast ? Theme.LvText(1) : Theme.HighContrast ? SystemColors.WindowText : Dark ? C("#F2CC60") : C("#9A6200"); } }
        public static Color Soft { get { return App && !Theme.HighContrast ? Theme.Soft : Theme.HighContrast ? SystemColors.Window : Dark ? C("#1B232D") : Cream ? C("#EEE6D2") : C("#F4F7FB"); } }
        public static Color GoodSoft { get { return App && !Theme.HighContrast ? Theme.LvSoft(0) : Theme.HighContrast ? SystemColors.Window : Dark ? C("#12301C") : C("#E6F1DD"); } }
        public static Color Lv(int lv) { return lv == 0 ? Up : lv == 1 ? Amber : lv == 2 ? Down : Muted; }

        // ---------- fonts (built for the current scale; rebuilt when it changes)
        static float fontsAt = -1; static string fontsLang = ""; static bool fontsApp;
        public static Font Mast, MastSmall, Head1, Head2, Head3, Body, BodyB, Small, Italic, Big, Num, NumS;   // Num: figures in Inter (lining digits - the serif's old-style digits are hard to read in tables)
        public static void Fonts()
        {
            if (fontsAt == S && fontsLang == L.Code && fontsApp == App) return;
            foreach (var f in new[] { Mast, MastSmall, Head1, Head2, Head3, Body, BodyB, Small, Italic, Big, Num, NumS }) if (f != null) f.Dispose();
            Mast = Draw.Serif(52 * S, true); MastSmall = Draw.Serif(34 * S, true); Head1 = Draw.Serif(40 * S, false); Head2 = Draw.Serif(22 * S, false); Head3 = Draw.Serif(17 * S, false); Big = Draw.Serif(34 * S, true); Num = new Font(Draw.UiFamilyName(), 30 * S, FontStyle.Bold, GraphicsUnit.Pixel); NumS = new Font(Draw.UiFamilyName(), 19 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            string body = L.IsRtl ? "Segoe UI" : L.Code == "bn" ? "Nirmala UI" : "Georgia";
            Body = new Font(body, 15 * S, FontStyle.Regular, GraphicsUnit.Pixel); BodyB = new Font(body, 15 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            Small = new Font(body, 13 * S, FontStyle.Regular, GraphicsUnit.Pixel); Italic = new Font(body, 17 * S, L.IsRtl ? FontStyle.Regular : FontStyle.Italic, GraphicsUnit.Pixel);
            if (App && !L.IsRtl && L.Code != "bn")
            {
                // the app's type: Barlow throughout (the figures were already the app's)
                foreach (var f in new[] { Mast, MastSmall, Head1, Head2, Head3, Body, BodyB, Small, Italic }) if (f != null) f.Dispose();
                Mast = new Font(Draw.fHero.FontFamily, 26 * S, Draw.fHero.Style, GraphicsUnit.Pixel); MastSmall = new Font(Draw.fHero.FontFamily, 22 * S, Draw.fHero.Style, GraphicsUnit.Pixel);
                Head1 = new Font(Draw.fBig.FontFamily, 26 * S, Draw.fBig.Style, GraphicsUnit.Pixel); Head2 = new Font(Draw.fBig.FontFamily, 19 * S, Draw.fBig.Style, GraphicsUnit.Pixel);
                Head3 = new Font(Draw.fHead.FontFamily, 15 * S, Draw.fHead.Style, GraphicsUnit.Pixel);
                Body = new Font(Draw.fBody.FontFamily, 14.5f * S, Draw.fBody.Style, GraphicsUnit.Pixel); BodyB = new Font(Draw.fHead.FontFamily, 14.5f * S, Draw.fHead.Style, GraphicsUnit.Pixel);
                Small = new Font(Draw.fSmall.FontFamily, 13 * S, Draw.fSmall.Style, GraphicsUnit.Pixel); Italic = new Font(Draw.fBody.FontFamily, 15 * S, Draw.fBody.Style, GraphicsUnit.Pixel);
            }
            fontsAt = S; fontsLang = L.Code; fontsApp = App;
        }

        // ---------- the paper itself: a fill, and on cream a faint dot texture (one small tile, drawn once)
        static TextureBrush dots; static float dotsAt = -1;
        public static void Ground(Graphics g, RectangleF r)
        {
            using (var b = new SolidBrush(Bg)) g.FillRectangle(b, r);
            if (!Cream || App) return;
            if (dots == null || dotsAt != S)
            {
                if (dots != null) dots.Dispose();
                int t = Math.Max(7, (int)Math.Round(13 * S));
                using (var bmp = new Bitmap(t, t))
                {
                    using (var bg = Graphics.FromImage(bmp)) using (var d1 = new SolidBrush(Color.FromArgb(18, 120, 90, 40))) using (var d2 = new SolidBrush(Color.FromArgb(12, 120, 90, 40)))
                    { bg.Clear(Color.Transparent); bg.FillRectangle(d1, 0, 0, 1, 1); bg.FillRectangle(d1, t / 2, t / 2, 1, 1); bg.FillRectangle(d2, t / 3, (t * 4) / 5, 1, 1); }
                    dots = new TextureBrush(bmp, WrapMode.Tile);
                }
                dotsAt = S;
            }
            g.FillRectangle(dots, r);
        }

        // ---------- the masthead: the paper's name between two laurel branches, the edition on the right
        public static float Masthead(Graphics g, float x, float y, float w, string title, string edition, string left1, string left2, bool compact)
        {
            Fonts();
            if (App) return y;   // the app look: no masthead - the page starts with its tabs
            using (var head = new SolidBrush(Head)) using (var ink = new SolidBrush(Ink))
            {
                var mf = compact ? MastSmall : Mast; var ms = g.MeasureString(title, mf); float h = ms.Height;
                if (compact)
                {
                    g.DrawString(title, mf, head, x, y);
                    var dt = DateTime.Now.ToString("ddd, MMM d, yyyy", CultureInfo.CurrentCulture); var ds = g.MeasureString(dt, Body);
                    g.DrawString(dt, Body, ink, x + w - ds.Width, y + h - ds.Height - 4 * S);
                    return y + h + 4 * S;
                }
                float lw = 44 * S, cx = x + w / 2, tx = cx - ms.Width / 2;
                // the side blocks only when they fit between the page edge and the laurels (1.1.6 walk: "Your Markets" ran into the left laurel)
                float gapL = tx - lw - 10 * S - 14 * S - (x + 46 * S), rightW = 0;
                foreach (var ln in new[] { DateTime.Now.ToString("ddd, MMM d, yyyy", CultureInfo.CurrentCulture), Edition() }) rightW = Math.Max(rightW, g.MeasureString(ln, Body).Width);
                float gapR = (x + w) - (tx + ms.Width + 10 * S + lw) - 14 * S;
                string side = left1; float sideW = Math.Max(g.MeasureString("AdeeboMeter", Head2).Width, Math.Max(g.MeasureString(left1, Small).Width, g.MeasureString(left2, Small).Width));
                if (sideW > gapL) { int cut = left1.LastIndexOf(" · ", StringComparison.Ordinal); side = cut > 0 ? left1.Substring(0, cut) + "\n" + left1.Substring(cut + 3) : left1; sideW = g.MeasureString("AdeeboMeter", Head2).Width; foreach (var part in (side + "\n" + left2).Split('\n')) sideW = Math.Max(sideW, g.MeasureString(part, Small).Width); }   // the last item on its own line before giving up (three lines still sit inside the masthead)
                bool room = sideW <= gapL && rightW <= gapR;
                Laurel(g, new RectangleF(tx - lw - 10 * S, y + 2 * S, lw, h + 30 * S), false); Laurel(g, new RectangleF(tx + ms.Width + 10 * S, y + 2 * S, lw, h + 30 * S), true);
                g.DrawString(title, mf, head, tx, y);
                var es = g.MeasureString(edition, Head2); g.DrawString(edition, Head2, head, cx - es.Width / 2, y + h - 6 * S);
                if (room)
                {
                    Theme.Logo(g, new RectangleF(x, y + 6 * S, 38 * S, 38 * S), false);
                    g.DrawString("AdeeboMeter", Head2, head, x + 46 * S, y + 4 * S);
                    g.DrawString(side + "\n" + left2, Small, ink, x + 46 * S, y + 32 * S);
                    float ry = y + 8 * S;
                    foreach (var ln in new[] { DateTime.Now.ToString("ddd, MMM d, yyyy", CultureInfo.CurrentCulture), Edition() })
                    { if (ln == null) continue; var lz = g.MeasureString(ln, Body); g.DrawString(ln, Body, ink, x + w - lz.Width, ry); ry += lz.Height; }
                }
                return y + h + es.Height + 2 * S;
            }
        }
        static string Edition() { return L.T("Edition") + " v" + Program.Version; }
        static void Laurel(Graphics g, RectangleF r, bool flip)
        {
            var st = g.Save();
            if (flip) { g.TranslateTransform(r.X + r.Width, r.Y); g.ScaleTransform(-1, 1); } else g.TranslateTransform(r.X, r.Y);
            float sx = r.Width / 44f, sy = r.Height / 80f;
            using (var b = new SolidBrush(Head)) using (var p = new Pen(Head, 2.2f * S))
            {
                g.DrawBezier(p, 38 * sx, 76 * sy, 14 * sx, 64 * sy, 6 * sx, 40 * sy, 12 * sx, 8 * sy);
                foreach (var leaf in new[] { new[] { 9f, 20f, -20f }, new[] { 7f, 36f, -35f }, new[] { 10f, 52f, -50f }, new[] { 20f, 65f, -65f }, new[] { 20f, 26f, 20f }, new[] { 22f, 44f, 5f }, new[] { 28f, 58f, -20f } })
                {
                    var s2 = g.Save(); g.TranslateTransform(leaf[0] * sx, leaf[1] * sy); g.RotateTransform(leaf[2]);
                    g.FillEllipse(b, -3.6f * sx, -7.5f * sy, 7.2f * sx, 15 * sy); g.Restore(s2);
                }
            }
            g.Restore(st);
        }

        // ---------- the tab bar: thick rule, the tabs with | between them, the current tab in navy (a highlighted tab in green),
        // then the red double rule. Returns each tab's rectangle (the caller makes them clickable).
        public static float TabBar(Graphics g, float x, float y, float w, string[][] tabs, string current, out List<Tuple<string, RectangleF>> hits)
        {
            Fonts(); hits = new List<Tuple<string, RectangleF>>();
            if (App)
            {
                // the app look: words with a line under the open one, left-aligned, one line under the row (as the page tabs)
                float ax = x, th = 40 * S;
                using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var bl = new SolidBrush(Theme.BlueText))
                    foreach (var t in tabs)
                    {
                        string label = L.T(t[1]); var ls = g.MeasureString(label, BodyB); var r = new RectangleF(ax, y, ls.Width + 24 * S, th);
                        if (ax > x && r.Right > x + w) { ax = x; y += th; r = new RectangleF(ax, y, ls.Width + 24 * S, th); }
                        bool on = t[0] == current, hl = t.Length > 2 && t[2] == "hl";
                        g.DrawString(label, BodyB, on ? ink : hl ? bl : sub, r.X + 12 * S, r.Y + (th - ls.Height) / 2);
                        if (on) Theme.Fill(g, new RectangleF(r.X + 8 * S, r.Bottom - 3 * S, r.Width - 16 * S, 2.5f * S), 1.25f * S, Theme.Blue);
                        hits.Add(Tuple.Create(t[0], r)); ax = r.Right + 4 * S;
                    }
                using (var pl = new Pen(Theme.Line, 1)) g.DrawLine(pl, x, y + th, x + w, y + th);
                return y + th + 16 * S;
            }
            using (var p3 = new Pen(Head, 3 * S)) g.DrawLine(p3, x, y, x + w, y);
            float pad = 14 * S, h = 40 * S, gap = 18 * S;
            var widths = tabs.Select(t => g.MeasureString(L.T(t[1]), Body).Width + 2 * pad + (t.Length > 2 && t[2] == "hl" ? 16 * S : 0)).ToList();
            float total = widths.Sum() + gap * (tabs.Length - 1);
            // too narrow for one row: wrap onto two
            var rows = new List<List<int>> { new List<int>() }; float run = 0;
            for (int i = 0; i < tabs.Length; i++) { if (run > 0 && run + widths[i] > w) { rows.Add(new List<int>()); run = 0; } rows[rows.Count - 1].Add(i); run += widths[i] + gap; }
            float ty = y + 8 * S;
            using (var head = new SolidBrush(Head)) using (var white = new SolidBrush(Color.White)) using (var sep = new SolidBrush(Muted))
                foreach (var row in rows)
                {
                    float rw = row.Sum(i => widths[i]) + gap * (row.Count - 1), tx = x + (w - rw) / 2;
                    for (int j = 0; j < row.Count; j++)
                    {
                        int i = row[j]; var r = new RectangleF(tx, ty, widths[i], h - 6 * S);
                        bool on = tabs[i][0] == current, hl = tabs[i].Length > 2 && tabs[i][2] == "hl";
                        if (on || hl) Theme.Fill(g, r, 6 * S, hl ? Green : Navy);
                        var label = L.T(tabs[i][1]); var ls = g.MeasureString(label, Body); float lx = r.X + (r.Width - ls.Width) / 2;
                        if (hl) { lx += 8 * S; float my = r.Y + r.Height / 2; g.FillPolygon(white, new[] { new PointF(lx - 16 * S, my - 6 * S), new PointF(lx - 6 * S, my), new PointF(lx - 16 * S, my + 6 * S) }); }
                        g.DrawString(label, Body, on || hl ? white : head, lx, r.Y + (r.Height - ls.Height) / 2);
                        hits.Add(Tuple.Create(tabs[i][0], r));
                        if (j < row.Count - 1) g.DrawString("|", Body, sep, r.Right + gap / 2 - 4 * S, r.Y + (r.Height - ls.Height) / 2);
                        tx += widths[i] + gap;
                    }
                    ty += h;
                }
            using (var p1 = new Pen(Head, 1)) g.DrawLine(p1, x, ty + 2 * S, x + w, ty + 2 * S);
            using (var pr = new Pen(Red, 2 * S)) g.DrawLine(pr, x, ty + 7 * S, x + w, ty + 7 * S);
            using (var pr1 = new Pen(Red, 1)) g.DrawLine(pr1, x, ty + 10 * S, x + w, ty + 10 * S);
            return ty + 22 * S;
        }

        // ---------- a desk: the boxed section with the red line on top
        public static void Desk(Graphics g, RectangleF r)
        {
            if (Kit.Off(g, r)) return;
            if (App) { Kit.Box(g, r, 14 * S); return; }   // the app look: a card like every other page's
            Theme.Fill(g, r, 6 * S, DeskBg);
            using (var p = new Pen(Line, 1)) Theme.Stroke(g, r, 6 * S, Line, 1);
            using (var pr = new Pen(Red, 2 * S)) g.DrawLine(pr, r.X + 3 * S, r.Y + 1 * S, r.Right - 3 * S, r.Y + 1 * S);
        }
        public static float DeskTitle(Graphics g, float x, float y, string title, string aside)
        {
            Fonts();
            using (var head = new SolidBrush(Head)) using (var mut = new SolidBrush(Muted))
            {
                var t = L.T(title); var ts = g.MeasureString(t, Head2); g.DrawString(t, Head2, head, x, y);
                if (!string.IsNullOrEmpty(aside)) g.DrawString(aside, Small, mut, x + ts.Width + 6 * S, y + ts.Height - g.MeasureString(aside, Small).Height - 3 * S);
                return y + ts.Height + 4 * S;
            }
        }
        public static RectangleF Pill(Graphics g, string text, float cx, float y, Color c)
        {
            var f = Draw.fBtn; var s = g.MeasureString(text, f); var r = new RectangleF(cx - s.Width / 2 - 14 * S, y, s.Width + 28 * S, s.Height + 6 * S);
            Theme.Fill(g, r, r.Height / 2, c); using (var w = new SolidBrush(Color.White)) g.DrawString(text, f, w, r.X + 14 * S, r.Y + 3 * S);
            return r;
        }
        public static float Ribbon(Graphics g, string text, float x, float y)
        {
            Fonts(); if (App) return y;   // no label above a heading in the app look
            var s = g.MeasureString(text, BodyB); float h = s.Height + 6 * S, w = s.Width + 34 * S;
            using (var b = new SolidBrush(Red)) g.FillPolygon(b, new[] { new PointF(x, y), new PointF(x + w, y), new PointF(x + w - 14 * S, y + h), new PointF(x, y + h) });
            using (var wb = new SolidBrush(Color.White)) g.DrawString(text, BodyB, wb, x + 10 * S, y + 3 * S);
            return y + h;
        }
        public static void Tick(Graphics g, RectangleF r, Color c)
        {
            using (var b = new SolidBrush(c)) g.FillEllipse(b, r);
            using (var p = new Pen(Color.White, Math.Max(1.6f, r.Width / 8)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLines(p, new[] { new PointF(r.X + r.Width * 0.27f, r.Y + r.Height * 0.52f), new PointF(r.X + r.Width * 0.44f, r.Y + r.Height * 0.68f), new PointF(r.X + r.Width * 0.74f, r.Y + r.Height * 0.36f) });
        }
        // a verdict mark: 0 tick (green), 1 "!" (amber), 2 cross (red) - drawn, the fonts lack the symbols
        public static void Mark(Graphics g, RectangleF r, int kind)
        {
            using (var b = new SolidBrush(kind == 0 ? Green : kind == 1 ? Amber : Down)) g.FillEllipse(b, r);
            using (var p = new Pen(Color.White, Math.Max(1.6f, r.Width / 8)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (kind == 0) g.DrawLines(p, new[] { new PointF(r.X + r.Width * 0.27f, r.Y + r.Height * 0.52f), new PointF(r.X + r.Width * 0.44f, r.Y + r.Height * 0.68f), new PointF(r.X + r.Width * 0.74f, r.Y + r.Height * 0.36f) });
                else if (kind == 1) { g.DrawLine(p, r.X + r.Width / 2, r.Y + r.Height * 0.25f, r.X + r.Width / 2, r.Y + r.Height * 0.55f); g.DrawLine(p, r.X + r.Width / 2, r.Y + r.Height * 0.74f, r.X + r.Width / 2, r.Y + r.Height * 0.76f); }
                else { g.DrawLine(p, r.X + r.Width * 0.32f, r.Y + r.Height * 0.32f, r.X + r.Width * 0.68f, r.Y + r.Height * 0.68f); g.DrawLine(p, r.X + r.Width * 0.68f, r.Y + r.Height * 0.32f, r.X + r.Width * 0.32f, r.Y + r.Height * 0.68f); }
            }
        }
        // a score ring: 0-100, green / amber / red by the score, the number in the middle
        public static void Ring(Graphics g, RectangleF r, int score)
        {
            float t = r.Width / 10; var rr = new RectangleF(r.X + t / 2, r.Y + t / 2, r.Width - t, r.Height - t);
            using (var p = new Pen(Line2, t)) g.DrawEllipse(p, rr);
            if (score >= 0) using (var p = new Pen(score >= 80 ? Green : score >= 55 ? Amber : Down, t) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, rr, -90, 360f * score / 100);
            string n = score >= 0 ? score.ToString(CultureInfo.CurrentCulture) : "-";
            using (var f = Draw.Serif(r.Width * 0.3f, true)) using (var b = new SolidBrush(Head)) using (var m = new SolidBrush(Muted))
            {
                var ns = g.MeasureString(n, f); g.DrawString(n, f, b, r.X + (r.Width - ns.Width) / 2, r.Y + r.Height / 2 - ns.Height * 0.62f);
                var os = g.MeasureString(L.T("out of 100"), Small); g.DrawString(L.T("out of 100"), Small, m, r.X + (r.Width - os.Width) / 2, r.Y + r.Height / 2 + ns.Height * 0.28f);
            }
        }
        // a line chart of one series (NaN gaps skipped), optionally filled under; scaled to its own range
        public static void Spark(Graphics g, RectangleF r, IList<double> v, Color c, bool fill)
        {
            var pts = new List<PointF>(); var ok = v.Where(z => !double.IsNaN(z)).ToList(); if (ok.Count < 2) return;
            double lo = ok.Min(), hi = ok.Max(); if (hi - lo < 1e-9) { hi = lo + 1; lo -= 1; }
            for (int i = 0; i < v.Count; i++) if (!double.IsNaN(v[i])) pts.Add(new PointF(r.X + r.Width * i / Math.Max(1, v.Count - 1), (float)(r.Bottom - (v[i] - lo) / (hi - lo) * r.Height)));
            if (fill) { var poly = new List<PointF>(pts) { new PointF(pts[pts.Count - 1].X, r.Bottom), new PointF(pts[0].X, r.Bottom) }; using (var b = new SolidBrush(Color.FromArgb(28, c))) g.FillPolygon(b, poly.ToArray()); }
            using (var p = new Pen(c, 2 * S) { LineJoin = LineJoin.Round }) g.DrawLines(p, pts.ToArray());
        }
        // "▲ 0.42%" / "▼ 0.12%" / "● 0.00%", in the up / down / flat colours (words for screen readers come from the caller)
        public static void Change(Graphics g, double pct, Font f, float rightX, float y, string fmt)
        {
            string t = double.IsNaN(pct) ? "-" : (pct > 0.005 ? "▲ " : pct < -0.005 ? "▼ " : "● ") + Math.Abs(pct).ToString(fmt ?? "0.00", CultureInfo.CurrentCulture) + "%";
            var s = g.MeasureString(t, f); using (var b = new SolidBrush(double.IsNaN(pct) ? Muted : pct > 0.005 ? Up : pct < -0.005 ? Down : Muted)) g.DrawString(t, f, b, rightX - s.Width, y);
        }

        // a flat weather glyph from the forecast's code (no emoji on the newspaper pages): sun, cloud, rain, snow, storm, fog
        public static void WeatherGlyph(Graphics g, int code, RectangleF r)
        {
            float u = r.Width / 64f; bool sun = code == 0 || code == 1 || code == 2, cloud = code != 0, rain = (code >= 51 && code <= 67) || (code >= 80 && code <= 82) || code >= 95, snow = (code >= 71 && code <= 77) || code == 85 || code == 86;
            if (sun) using (var b = new SolidBrush(C("#F5B82E"))) g.FillEllipse(b, r.X + (cloud ? 8 : 14) * u, r.Y + (cloud ? 6 : 12) * u, (cloud ? 26 : 36) * u, (cloud ? 26 : 36) * u);
            if (cloud && code != 0 && code != 1)
                using (var b = new SolidBrush(Dark ? C("#9AA7B4") : C("#DCE5F0"))) using (var p = new Pen(C("#8C98A8"), 1.2f * u))
                using (var gp = new GraphicsPath())
                {
                    gp.AddEllipse(r.X + 12 * u, r.Y + 20 * u, 22 * u, 20 * u); gp.AddEllipse(r.X + 24 * u, r.Y + 12 * u, 26 * u, 26 * u); gp.AddEllipse(r.X + 40 * u, r.Y + 22 * u, 18 * u, 18 * u); gp.AddRectangle(new RectangleF(r.X + 22 * u, r.Y + 28 * u, 28 * u, 12 * u));
                    g.FillPath(b, gp);
                }
            if (rain) using (var p = new Pen(C("#1F6FEB"), 3 * u) { StartCap = LineCap.Round, EndCap = LineCap.Round }) foreach (var dx in new[] { 20f, 32f, 44f }) g.DrawLine(p, r.X + dx * u, r.Y + 46 * u, r.X + (dx - 4) * u, r.Y + 56 * u);
            if (snow) using (var b = new SolidBrush(C("#9CC2FF"))) foreach (var dx in new[] { 18f, 30f, 42f }) g.FillEllipse(b, r.X + dx * u, r.Y + 48 * u, 6 * u, 6 * u);
            if (code >= 95) using (var b = new SolidBrush(C("#F2CC60"))) g.FillPolygon(b, new[] { new PointF(r.X + 34 * u, r.Y + 40 * u), new PointF(r.X + 26 * u, r.Y + 54 * u), new PointF(r.X + 33 * u, r.Y + 54 * u), new PointF(r.X + 28 * u, r.Y + 64 * u), new PointF(r.X + 42 * u, r.Y + 48 * u), new PointF(r.X + 35 * u, r.Y + 48 * u) });
            if (code == 45 || code == 48) using (var p = new Pen(C("#AEB8C2"), 3 * u)) foreach (var dy in new[] { 46f, 52f, 58f }) g.DrawLine(p, r.X + 14 * u, r.Y + dy * u, r.X + 52 * u, r.Y + dy * u);
        }

        // ---------- the check strip under every tab: STREAM CHECK / LINE CHECK, the verdict, the main tip, a button, Details
        public sealed class StripHits { public RectangleF Button, Details; }
        public static float Strip(Graphics g, float x, float y, float w, string label, bool line, string verdict, string sub, string tip, string button, int lv, out StripHits hits)
        {
            Fonts(); hits = new StripHits();
            float h = 96 * S; var r = new RectangleF(x, y, w, h);
            if (App)
            {
                // the app look (mockup): a card - a level dot, the answer in big type, one line under it, the button on the right
                h = 84 * S; r = new RectangleF(x, y, w, h); Kit.Box(g, r, 14 * S);
                var bs0 = g.MeasureString(button, Draw.fBtn); var br0 = new RectangleF(r.Right - 20 * S - bs0.Width - 28 * S, r.Y + (h - 38 * S) / 2, bs0.Width + 28 * S, 38 * S);
                Theme.Fill(g, br0, 10 * S, Theme.Card); Theme.Stroke(g, br0, 10 * S, Theme.Line, 1);
                using (var bb = new SolidBrush(Theme.BlueText)) g.DrawString(button, Draw.fBtn, bb, br0.X + 14 * S, br0.Y + (38 * S - bs0.Height) / 2); hits.Button = br0;
                var dl0 = L.T("Details") + " ›"; var dz0 = g.MeasureString(dl0, Draw.fSmall); hits.Details = new RectangleF(br0.X - dz0.Width - 18 * S, br0.Y, dz0.Width + 8 * S, br0.Height);
                using (var lb = new SolidBrush(Theme.BlueText)) g.DrawString(dl0, Draw.fSmall, lb, hits.Details.X + 4 * S, br0.Y + (br0.Height - dz0.Height) / 2);
                float tx0 = r.X + 40 * S, tw0 = hits.Details.X - tx0 - 12 * S;
                Draw.LvDot(g, lv < 0 || lv > 2 ? 3 : lv, r.X + 20 * S, r.Y + 24 * S, 9 * S, 9 * S);
                using (var ib = new SolidBrush(Theme.Ink)) using (var sb = new SolidBrush(Theme.Sub)) using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                {
                    g.DrawString(verdict, Draw.fBig, ib, new RectangleF(tx0, r.Y + 14 * S, tw0, 30 * S), nf);
                    g.DrawString(sub + (string.IsNullOrEmpty(tip) ? "" : "  ·  " + tip), Draw.fSmall, sb, new RectangleF(tx0, r.Y + 48 * S, tw0, 20 * S), nf);
                }
                return y + h + 14 * S;
            }
            Theme.Fill(g, r, 8 * S, Navy);
            using (var gp = new Pen(lv == 0 ? Green : lv == 1 ? Amber : lv == 2 ? Down : Muted, 3 * S)) g.DrawLine(gp, r.X + 6 * S, r.Bottom - 2 * S, r.Right - 6 * S, r.Bottom - 2 * S);
            var ic = new RectangleF(r.X + 18 * S, r.Y + 19 * S, 58 * S, 58 * S);
            using (var b = new SolidBrush(lv == 0 ? Green : lv == 1 ? Amber : lv == 2 ? Down : Color.FromArgb(90, 110, 140))) g.FillEllipse(b, ic);
            using (var wb = new SolidBrush(Color.White))
            {
                if (!line) g.FillPolygon(wb, new[] { new PointF(ic.X + 23 * S, ic.Y + 17 * S), new PointF(ic.X + 41 * S, ic.Y + 29 * S), new PointF(ic.X + 23 * S, ic.Y + 41 * S) });
                else using (var p = new Pen(Color.White, 2.6f * S) { LineJoin = LineJoin.Round }) g.DrawLines(p, new[] { new PointF(ic.X + 12 * S, ic.Y + 29 * S), new PointF(ic.X + 20 * S, ic.Y + 29 * S), new PointF(ic.X + 25 * S, ic.Y + 18 * S), new PointF(ic.X + 32 * S, ic.Y + 41 * S), new PointF(ic.X + 37 * S, ic.Y + 29 * S), new PointF(ic.X + 46 * S, ic.Y + 29 * S) });
                float tx = ic.Right + 16 * S;
                using (var lab = new SolidBrush(C("#9CC2FF"))) g.DrawString(label, Draw.fTiny, lab, tx, r.Y + 14 * S);
                g.DrawString(verdict, Head2, wb, tx, r.Y + 28 * S);
                using (var sb = new SolidBrush(C("#C9D3E0"))) g.DrawString(sub, Small, sb, new RectangleF(tx, r.Y + 58 * S, w * 0.42f, 34 * S));
                // the button and Details on the right; the tip between when there's room
                var bs = g.MeasureString(button, Draw.fBtn); var br = new RectangleF(r.Right - 110 * S - bs.Width, r.Y + (h - 44 * S) / 2, bs.Width + 30 * S, 44 * S);
                Theme.Fill(g, br, 6 * S, Green); g.DrawString(button, Draw.fBtn, wb, br.X + 15 * S, br.Y + (br.Height - bs.Height) / 2); hits.Button = br;
                var dl = L.T("Details") + " ›"; var dz = g.MeasureString(dl, Draw.fBtn); hits.Details = new RectangleF(br.Right + 10 * S, br.Y, dz.Width + 12 * S, br.Height);
                using (var lb = new SolidBrush(C("#9CC2FF"))) g.DrawString(dl, Draw.fBtn, lb, hits.Details.X + 4 * S, br.Y + (br.Height - dz.Height) / 2);
                float tipX = tx + w * 0.42f + 10 * S, tipW = br.X - 16 * S - tipX;
                if (!string.IsNullOrEmpty(tip) && tipW > 180 * S)
                {
                    var tr = new RectangleF(tipX, r.Y + 16 * S, tipW, h - 32 * S); using (var tb = new SolidBrush(Color.FromArgb(22, 255, 255, 255))) g.FillRectangle(tb, tr);
                    using (var yb = new SolidBrush(C("#F2CC60"))) g.FillEllipse(yb, tr.X + 10 * S, tr.Y + 11 * S, 10 * S, 10 * S);
                    g.DrawString(L.T("Tip:") + " " + tip, Small, wb, new RectangleF(tr.X + 30 * S, tr.Y + 8 * S, tr.Width - 38 * S, tr.Height - 10 * S));
                }
            }
            return y + h + 14 * S;
        }
    }
}
