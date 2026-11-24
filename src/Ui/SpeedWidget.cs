// The speed widgets (DU Meter style, 1.1.5): Speed only, Speed + status, Speed + apps. Folded, the widget is one line of
// speeds; hovered, a speed card with the last minute as a graph, today's totals and (Speed + apps) the busiest apps.
// Same numbers the Monitor already reads every second - nothing extra is measured. The strip repaints only when a shown
// number changes (ChipKey), so a steady line costs nothing.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    static partial class Draw
    {
        [ThreadStatic] static string styleOverride;   // the Settings preview draws every style without changing yours
        public static string Style { get { return styleOverride ?? Corner.Effective; } }   // 24 Nov: a takeover (a game, a match, the weather, the day plan) can stand in for yours
        public static bool SpeedStyle { get { string st = Style; return st == "speed" || st == "speedstatus" || st == "speedapps"; } }
        public static Bitmap Preview(Snap s, string style, bool card)
        {
            styleOverride = style;
            try { return card ? Card(s, new Dictionary<string, RectangleF>()) : Chip(s, 0); }
            finally { styleOverride = null; }
        }

        // 12.4 Mb/s · 850 kb/s · 1.6 MB/s
        public static string Rate(double mbps, bool unit)
        {
            if (double.IsNaN(mbps) || mbps < 0) return "-";
            bool bytes = Cfg.WUnits == "bytes"; double v = bytes ? mbps / 8 : mbps; string u;
            if (v < 1) { v *= 1000; u = bytes ? "kB/s" : "kb/s"; } else if (v >= 1000) { v /= 1000; u = bytes ? "GB/s" : "Gb/s"; } else u = bytes ? "MB/s" : "Mb/s";
            string n = v == 0 ? "0" : v.ToString(v < 10 ? "0.0" : "0", CultureInfo.CurrentCulture);   // a quiet line reads "0 kb/s", not "0.0"
            return unit ? n + " " + u : n;
        }
        // down and up together: one unit when they share it ("↓ 850  ↑ 120 kb/s"), each its own when they don't
        public static string Pair(double down, double up)
        {
            string ud = Unit(Num(down)), uu = Unit(Num(up));
            return ud == uu ? "↓ " + Rate(down, false) + "  ↑ " + Rate(up, false) + " " + ud : "↓ " + Rate(down, true) + "  ↑ " + Rate(up, true);
        }
        static string Unit(double mbps) { var r = Rate(mbps, true); int sp = r.IndexOf(' '); return sp > 0 ? r.Substring(sp + 1) : ""; }
        // 21 Nov (Adeeb): the strip's two clocks - the speeds may change every 2 s, the rest (status words, the graph,
        // the weather) every 5 s; a change of level (amber / red) still shows at once (AppCtx)
        public static string ChipSpeedKey(Snap s) { return SpeedStyle ? SpeedText(s) : ""; }
        public static string ChipOtherKey(Snap s)
        {
            if (!SpeedStyle) return ChipWords(s) + WeatherKeyPart();
            return (Style == "speed" ? "" : s.Chip) + "|" + (Cfg.WGraph ? Spark30(Cfg.WDir == "up" ? s.UpHist : s.DownHist) : "") + WeatherKeyPart();
        }
        // what the folded strip shows: it's redrawn only when this changes
        public static string ChipKey(Snap s)
        {
            if (!SpeedStyle) return ChipWords(s) + WeatherKeyPart();
            return SpeedText(s) + "|" + (Style == "speed" ? "" : s.Chip) + "|" + (Cfg.WGraph ? Spark30(Cfg.WDir == "up" ? s.UpHist : s.DownHist) : "") + WeatherKeyPart();
        }
        static string SpeedText(Snap s)
        {
            if (Cfg.WDir == "up") return "↑ " + Rate(s.Up, true);       // AM-CG-193: the shown figure's own unit
            if (Cfg.WDir == "down") return "↓ " + Rate(s.Down, true);
            string ud = Unit(Num(s.Down)), uu = Unit(Num(s.Up));
            return ud == uu ? "↓ " + Rate(s.Down, false) + "  ↑ " + Rate(s.Up, false) + " " + ud : "↓ " + Rate(s.Down, true) + "  ↑ " + Rate(s.Up, true);
        }
        static double Num(double v) { return double.IsNaN(v) ? 0 : v; }
        // AM-CG-206: all 30 plotted samples, as the pixel rows they land on (and the scale they share)
        static string Spark30(double[] h)
        {
            if (h == null || h.Length == 0) return ""; var v = h.Skip(Math.Max(0, h.Length - 30)).ToArray(); double cap = 1; foreach (var q in v) if (!double.IsNaN(q) && q > cap) cap = q;
            return Math.Round(cap, 1).ToString(CultureInfo.InvariantCulture) + ":" + string.Join(",", v.Select(q => double.IsNaN(q) ? "-" : ((int)(q / (cap * 1.1) * 16)).ToString(CultureInfo.InvariantCulture)));
        }

        static Bitmap SpeedChip(Snap s) { return SpeedChip(s, 0); }
        static Bitmap SpeedChip(Snap s, double dwell)
        {
            bool status = Style != "speed"; string txt = SpeedText(s);
            // width from a template, not the live digits - the strip doesn't wobble as numbers change
            string tpl = (Cfg.WDir == "both" ? "↓ 888  ↑ 888" : "↓ 888") + " " + (Cfg.WUnits == "bytes" ? "MB/s" : "Mb/s");
            float tw = Math.Max(Measure(tpl, fBtn, 0).Width, Measure(txt, fBtn, 0).Width), sw = status ? Measure(s.Chip, fSmall, 0).Width + 22 * S : 0, gw = Cfg.WGraph ? 46 * S : 0;
            float ww = WeatherChipW(); int W = (int)(10 * S + (Cfg.Buddy ? 26 * S : 12 * S) + sw + tw + gw + ww + 12 * S), H = (int)(28 * S);
            var bmp = new Bitmap((int)Math.Ceiling(W * WidgetK), (int)Math.Ceiling(Math.Min(H, 900 * S) * WidgetK), PixelFormat.Format32bppArgb);   /* review 1.1.6 R5 N4: a card is never taller than 900 px, whatever the font metrics say */
            using (var g = PrepW(bmp))
            {
                var r = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, r, H / 2f, Color.FromArgb(246, Theme.Card)); Theme.Stroke(g, r, H / 2f, s.Level >= 1 && s.Level <= 2 && status ? Theme.Lv(s.Level) : Theme.Line, 1);
                float x = 10 * S;
                if (Cfg.Buddy) { Buddy(g, new RectangleF(x - 2 * S, (H - 20 * S) / 2, 20 * S, 20 * S), s, false); x += 26 * S; }
                else { using (var b = new SolidBrush(Theme.Lv(s.Level))) g.FillEllipse(b, x, (H - 8 * S) / 2, 8 * S, 8 * S); x += 14 * S; }
                if (status)
                {
                    using (var b = new SolidBrush(s.Level >= 1 && s.Level <= 2 ? Theme.LvText(s.Level) : Theme.Sub)) g.DrawString(s.Chip, fSmall, b, x, (H - Measure(s.Chip, fSmall, 0).Height) / 2);
                    x += sw - 10 * S; using (var p = new Pen(Theme.Line)) g.DrawLine(p, x, 7 * S, x, H - 7 * S); x += 10 * S;
                }
                using (var b = new SolidBrush(Theme.Ink)) g.DrawString(txt, fBtn, b, x, (H - Measure(txt, fBtn, 0).Height) / 2);
                x += tw + 6 * S;
                if (Cfg.WGraph)
                {
                    var gr = new RectangleF(x, 6 * S, 40 * S, H - 12 * S);
                    var v = ((Cfg.WDir == "up" ? s.UpHist : s.DownHist) ?? Array.Empty<double>()); v = v.Skip(Math.Max(0, v.Length - 30)).ToArray(); double cap = 1; foreach (var q in v) if (!double.IsNaN(q) && q > cap) cap = q;
                    Theme.Spark(g, v, gr, Cfg.WDir == "up" ? Theme.Green : Theme.Blue, cap * 1.1, true);
                    x += gw;
                }
                if (ww > 0) WeatherChipDraw(g, x + 2 * S, H);
                if (dwell > 0)   // AM-CG-195
                {
                    var tr = new RectangleF(12 * S, H - 4 * S, W - 24 * S, 2 * S);
                    using (var b = new SolidBrush(Theme.Track)) g.FillRectangle(b, tr);
                    using (var b = new SolidBrush(Theme.Blue)) g.FillRectangle(b, tr.X, tr.Y, tr.Width * (float)Math.Min(1, dwell), tr.Height);
                }
            }
            return bmp;
        }

        static Bitmap SpeedCard(Snap s, Dictionary<string, RectangleF> hits)
        {
            hits.Clear(); bool apps = Style == "speedapps", status = Style != "speed";
            int W = (int)(264 * S); float pad = 12 * S, inner = W - 2 * pad;
            var top = (s.TopCpu ?? new List<AppStat>()).Where(a => a != null && a.Name != "AdeeboMeter").Take(3).ToList();
            float headH = status ? Measure(s.Headline, fSmall, (int)inner).Height + 6 * S : 0;
            string wtx = WeatherCardText(); float h = pad + 24 * S + headH + 64 * S + (Cfg.WGraph ? 46 * S : 0) + (Cfg.WTotal ? 22 * S : 0) + (wtx != null ? 22 * S : 0) + (apps ? 22 * S + Math.Max(1, top.Count) * 19 * S : 0) + 16 * S + pad;   // bug-hunt L-147: 4 of space and the 12-high path strip
            int H = (int)Math.Ceiling(h);
            var bmp = new Bitmap((int)Math.Ceiling(W * WidgetK), (int)Math.Ceiling(Math.Min(H, 900 * S) * WidgetK), PixelFormat.Format32bppArgb);   /* review 1.1.6 R5 N4: a card is never taller than 900 px, whatever the font metrics say */
            using (var g = PrepW(bmp)) using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                var cr = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, cr, 16 * S, Color.FromArgb(250, Theme.Card)); Theme.Stroke(g, cr, 16 * S, status && s.Level >= 1 && s.Level <= 2 ? Theme.Lv(s.Level) : Theme.Line, 1);
                float y = pad;
                Theme.Logo(g, new RectangleF(pad, y, 18 * S, 18 * S), false);
                g.DrawString(L.T("AdeeboMeter"), fName, ink, pad + 24 * S, y + 1 * S);
                string tag = Kit.Sent("SPEED"); float nw = Measure(L.T("AdeeboMeter"), fName, 0).Width; var tr = new RectangleF(pad + 24 * S + nw + 4 * S, y + 2 * S, Measure(tag, fTiny, 0).Width + 10 * S, 15 * S);
                Theme.Fill(g, tr, tr.Height / 2, Theme.BlueSoft); using (var tb = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.BlueDark)) g.DrawString(tag, fTiny, tb, tr.X + 5 * S, tr.Y + 1 * S);
                if (Cfg.Buddy) Buddy(g, new RectangleF(W - pad - 22 * S, y - 2 * S, 22 * S, 22 * S), s, false);
                y += 24 * S;
                if (status) { using (var hb = new SolidBrush(s.Level >= 1 && s.Level <= 2 ? Theme.LvText(s.Level) : Theme.Sub)) g.DrawString(s.Headline, fSmall, hb, new RectangleF(pad, y, inner, headH)); y += headH; }
                // (AM-CG-207: no hover area here - there's no detail card for it)
                // the two big numbers
                float half = inner / 2;
                for (int i = 0; i < 2; i++)
                {
                    bool down = i == 0; if ((Cfg.WDir == "down" && !down) || (Cfg.WDir == "up" && down)) continue;
                    float bx = Cfg.WDir == "both" ? pad + i * half : pad; double v = down ? s.Down : s.Up;
                    using (var lb = new SolidBrush(down ? Theme.Blue : Theme.Green)) g.DrawString((down ? "↓ " : "↑ ") + L.T(down ? "Download" : "Upload"), fTiny, lb, bx, y);
                    string n = Rate(v, false); g.DrawString(n, fCardNum, ink, bx - 2 * S, y + 14 * S);
                    float nw2 = g.MeasureString(n, fCardNum).Width; g.DrawString(Unit(v), fSmall, muted, bx + nw2 - 4 * S, y + 32 * S);
                }
                y += 64 * S;
                if (Cfg.WGraph)
                {
                    var sr = new RectangleF(pad, y, inner, 38 * S); Theme.Fill(g, sr, 9 * S, Theme.Soft);
                    var dv = s.DownHist ?? Array.Empty<double>(); var uv = s.UpHist ?? Array.Empty<double>(); double cap = 1; foreach (var q in (Cfg.WDir == "up" ? uv : Cfg.WDir == "down" ? dv : dv.Concat(uv))) if (!double.IsNaN(q) && q > cap) cap = q;   // AM-CG-194: scaled to what's drawn
                    if (Cfg.WDir != "up") Theme.Spark(g, dv, sr, Theme.Blue, cap * 1.1, true);
                    if (Cfg.WDir != "down") Theme.Spark(g, uv, sr, Theme.Green, cap * 1.1, false);
                    y += 46 * S;
                }
                if (Cfg.WTotal)
                {
                    string t = L.T("Today") + "  ↓ " + MB(s.TodayDown) + "   ↑ " + MB(s.TodayUp);
                    g.DrawString(t, fSmall, muted, pad, y); y += 22 * S;
                }
                if (wtx != null) { WeatherCardDraw(g, pad, y, inner, wtx); y += 22 * S; }
                if (apps)
                {
                    using (var hb = new SolidBrush(Theme.Muted)) g.DrawString(Kit.Sent("BUSIEST APPS (CPU)"), fTiny, hb, pad, y + 4 * S); y += 22 * S;
                    if (top.Count == 0) { g.DrawString(L.T("Nothing busy."), fSmall, muted, pad, y); y += 19 * S; }
                    foreach (var a in top)
                    {
                        using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                            g.DrawString(a.Name, fSmall, ink, new RectangleF(pad, y, inner - 60 * S, 18 * S), sf);
                        string c = a.Cpu.ToString("0.0", CultureInfo.CurrentCulture) + "%"; var cs = g.MeasureString(c, fSmall);
                        g.DrawString(c, fSmall, muted, pad + inner - cs.Width, y); y += 19 * S;
                    }
                }
                y += 4 * S;
                hits["path"] = new RectangleF(pad - 4 * S, y - 4 * S, inner + 8 * S, 20 * S);
                PathStrip(g, s, new RectangleF(pad, y, inner, 12 * S), false);
            }
            if (WidgetK != 1f) foreach (var k in hits.Keys.ToList()) { var r = hits[k]; hits[k] = new RectangleF(r.X * WidgetK, r.Y * WidgetK, r.Width * WidgetK, r.Height * WidgetK); }
            return bmp;
        }
        static string MB(double bytes) { return bytes >= 1e9 ? (bytes / 1e9).ToString("0.00", CultureInfo.CurrentCulture) + " GB" : (bytes / 1e6).ToString("0", CultureInfo.CurrentCulture) + " MB"; }
    }
}
