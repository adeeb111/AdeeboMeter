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
    // ================================================================== drawing
    static class Theme
    {
        // Colour tokens. Light and dark follow Windows' own "app mode" setting and switch live.
        public static bool Dark;
        public static Color Ink, Muted, Sub, Line, Line2, Track, Soft, Card, Bg, Rail, UpText, BlueSoft, BlueDark, RowNew, RowGone, RowSel, RowHot, Grey;
        // declared before the colours below: static fields start in the order they're written, and C() needs this one
        static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Color> parsed = new System.Collections.Concurrent.ConcurrentDictionary<string, Color>(StringComparer.Ordinal);
        // 23 Nov polish: the website's colours - the faceplate blue, and the logo's three arcs as the status colours (deeper on light)
        public static Color Blue = C("#1155E0"), BlueHot = C("#0E47BD"); public static readonly Color Orange = C("#E36209"); public static Color Green = C("#1F9E6B"), Amber = C("#B7791F"), Red = C("#D9473A");
        static readonly Color[] lvText = new Color[4], lvSoft = new Color[4];
        static Theme() { Apply(false); }
        // bug-hunt L-261: each hex is parsed once - paints call this often
        public static Color C(string h) { if (h == null) return ColorTranslator.FromHtml(h); return parsed.GetOrAdd(h, x => ColorTranslator.FromHtml(x)); }
        public static bool SystemDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                { var v = k == null ? null : k.GetValue("AppsUseLightTheme"); return v is int && (int)v == 0; }
            }
            catch { return false; }
        }
        // the themes past Light and Dark (ForceTheme 2, 3, ...): name -> bg, card, soft, line, line2, track, ink, muted, sub, rail,
        // blueSoft, blueDark, accent, rowSel, rowHot, grey, upText, ok/warn/bad text, ok/warn/bad wash
        public static readonly string[] Palettes = { "neon", "midnight", "sunset", "mint" };
        public static readonly string[] PaletteNames = { "Neon", "Midnight", "Sunset", "Mint" };
        public static readonly bool[] PaletteDark = { true, true, true, false };
        public static readonly Dictionary<string, string[]> PaletteColors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "neon", new[] { "#07080D", "#10121A", "#161925", "#262B3D", "#1A1E2B", "#222739", "#EEF4FF", "#9AA2BA", "#C9D0E0", "#323A52", "#0C2A33", "#7FF7FF", "#00E5FF", "#0F2A36", "#141826", "#4C536C", "#FF9EF0", "#5CFFB0", "#FFE066", "#FF7A99", "#0D2A1E", "#2D2709", "#331421" } },
            { "midnight", new[] { "#08142B", "#0E1F3D", "#12264A", "#22396A", "#162B52", "#1C3360", "#E8F1FF", "#9DB0CF", "#C6D5EE", "#2C4878", "#10355A", "#8AF0DD", "#3DE0C2", "#133158", "#11264A", "#4A6290", "#FFC38A", "#7EE8B0", "#F5D06A", "#FF9A9A", "#0F3326", "#3A3112", "#3D1C26" } },
            { "sunset", new[] { "#1A1016", "#241720", "#2C1C27", "#43303B", "#301F2A", "#3A2531", "#FBEFF2", "#C9AEBA", "#EBD2DC", "#563846", "#3E2230", "#FFC2A3", "#FF7A59", "#3A2030", "#2A1A24", "#6B4C5A", "#FFD27A", "#86E5A6", "#FFD36E", "#FF9C9C", "#16301F", "#3A2F12", "#43191F" } },
            { "mint", new[] { "#EEF7F4", "#FFFFFF", "#F2FAF7", "#CBE3DA", "#E4F2ED", "#DDEFE8", "#14302A", "#4F6A62", "#2F4C45", "#B2D1C6", "#E0F5EE", "#0B6E58", "#0E9C78", "#DDF3EC", "#F5FBF9", "#9FBAB1", "#9A4A00", "#116329", "#7D4E00", "#B42318", "#DAFBE1", "#FFF8C5", "#FFEBE9" } }
        };
        public static string Palette = "standard";
        // text on the main accent: white on deep blues, near-black on Neon's cyan and Midnight's aqua
        public static Color OnBlue { get { return OnColor(Blue); } }
        // AM-CG-198: Mint's white-on-green was 3.47:1 - choose the foreground by computed contrast
        // round 6 H6-ML3: the accent used as text - itself where it reads at 4.5:1 on a card, otherwise moved toward the text colour
        // step by step until it does (Mint's green and the dark theme's blue were 3.5-3.7:1)
        public static double Ratio(Color a, Color b) { Func<double, double> lin = c => { c /= 255; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }; Func<Color, double> L = x => 0.2126 * lin(x.R) + 0.7152 * lin(x.G) + 0.0722 * lin(x.B); double la = L(a), lb = L(b); return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05); }
        public static Color BlueText { get { var c = Blue; for (int i = 1; i <= 10 && Ratio(c, Card) < 4.5; i++) c = Kit.Mix(Blue, Ink, i * 0.1f); return c; } }
        public static Color OnColor(Color bg)
        {
            Func<double, double> lin = c => { c /= 255; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); };
            double L = 0.2126 * lin(bg.R) + 0.7152 * lin(bg.G) + 0.0722 * lin(bg.B); var dark = C("#06121A"); double Ld = 0.2126 * lin(dark.R) + 0.7152 * lin(dark.G) + 0.0722 * lin(dark.B);
            return 1.05 / (L + 0.05) >= (L + 0.05) / (Ld + 0.05) ? Color.White : dark;
        }
        // AM-CG-204: a change from one contrast theme to another (still high contrast) re-applies too
        public static string AppliedSig = "";
        public static string ContrastSig() { return SystemColors.Window.ToArgb() + "|" + SystemColors.WindowText.ToArgb() + "|" + SystemColors.Highlight.ToArgb() + "|" + SystemColors.HighlightText.ToArgb() + "|" + SystemColors.HotTrack.ToArgb(); }
        public static bool ContrastChanged() { return System.Windows.Forms.SystemInformation.HighContrast != AppliedContrast || (System.Windows.Forms.SystemInformation.HighContrast && ContrastSig() != AppliedSig); }
        public static bool AppliedContrast;   // the high-contrast state the colours were made for (a change re-applies them)
        public static void Apply(bool dark)
        {
            Dark = dark; AppliedContrast = SystemInformation.HighContrast; AppliedSig = ContrastSig();
            if (!dark)
            {
                Ink = C("#16191E"); Muted = C("#5A6270"); Sub = C("#4B5360"); Line = C("#DDE1E7"); Line2 = C("#EEF0F3"); Track = C("#E6E9ED"); Soft = C("#F1F3F6");
                Card = C("#FFFFFF"); Bg = C("#F1F3F6"); Rail = C("#C9CED6"); UpText = C("#9A4A00"); BlueSoft = C("#EAF0FD"); BlueDark = C("#0D47BD");
                RowNew = C("#E6F4EC"); RowGone = C("#FCEBE8"); RowSel = C("#E8EEFC"); RowHot = C("#F6F8FB"); Grey = C("#B3B9C2");
                lvText[0] = C("#17794F"); lvText[1] = C("#875A00"); lvText[2] = C("#B42318");
                // the light level-text colours are a step darker than the website's (#17794F / #875A00 / #B42318): small text on
                // Windows needs 5:1 or more, the website draws them larger (review L-10, kept on purpose)
                lvSoft[0] = C("#E6F4EC"); lvSoft[1] = C("#FCF1DC"); lvSoft[2] = C("#FCEBE8");
                Green = C("#1F9E6B"); Amber = C("#B7791F"); Red = C("#D9473A");
            }
            else
            {
                // graphite, not navy (23 Nov): the website's dark
                Ink = C("#EEF1F5"); Muted = C("#7E8794"); Sub = C("#A9B1BD"); Line = C("#262A31"); Line2 = C("#202329"); Track = C("#262A31"); Soft = C("#1F2329");
                // review 24 Nov (design ML-4 / L-10), kept on purpose: dark link text is #9CB9FA here, #5B91F7 on the website. Windows
                // draws text thinner than a browser, so the app's links take the lighter blue (8.8:1 on the card); the website's
                // value also serves as a button fill, where a lighter blue would fail under white text.
                Card = C("#181B20"); Bg = C("#111316"); Rail = C("#3A3F47"); UpText = C("#F0A060"); BlueSoft = C("#16233D"); BlueDark = C("#9CB9FA");
                RowNew = C("#14271F"); RowGone = C("#3A1E1C"); RowSel = C("#1C2638"); RowHot = C("#1C1F24"); Grey = C("#4A5058");
                lvText[0] = C("#5EF2B0"); lvText[1] = C("#FCD34D"); lvText[2] = C("#FF7A6B");
                lvSoft[0] = C("#14271F"); lvSoft[1] = C("#3A2E14"); lvSoft[2] = C("#3A1E1C");
                Green = C("#5EF2B0"); Amber = C("#FCD34D"); Red = C("#FF7A6B");   // the logo's gauge colours, as on the website and phone
            }
            Blue = C("#1155E0"); BlueHot = C("#0D47BD"); Palette = "standard";
            // 1.1.5 themes on top of Light / Dark: their own background, cards, lines and main accent. Green, amber and red
            // keep their meaning in every one (only their shades move), and the text stays readable (self-tested).
            string pal = Program.ForceTheme >= 2 && Program.ForceTheme < Palettes.Length + 2 ? Palettes[Program.ForceTheme - 2] : "standard";
            string[] v; if (pal != "standard" && PaletteColors.TryGetValue(pal, out v) && Program.WantDark() == dark)
            {
                Palette = pal;
                Bg = C(v[0]); Card = C(v[1]); Soft = C(v[2]); Line = C(v[3]); Line2 = C(v[4]); Track = C(v[5]); Ink = C(v[6]); Muted = C(v[7]); Sub = C(v[8]); Rail = C(v[9]);
                BlueSoft = C(v[10]); BlueDark = C(v[11]); Blue = C(v[12]); BlueHot = Kit.Mix(Blue, dark ? Color.White : Color.Black, 0.15f); RowSel = C(v[13]); RowHot = C(v[14]); Grey = C(v[15]); UpText = C(v[16]);
                lvText[0] = C(v[17]); lvText[1] = C(v[18]); lvText[2] = C(v[19]); lvSoft[0] = C(v[20]); lvSoft[1] = C(v[21]); lvSoft[2] = C(v[22]);
                RowNew = lvSoft[0]; RowGone = lvSoft[2];
            }
            lvText[3] = Muted; lvSoft[3] = Line2;
            // Windows High Contrast: the user's own colours for text, background and lines (levels keep their hue, text the
            // system's) - so nothing becomes unreadable on a black-on-yellow or white-on-black theme
            if (SystemInformation.HighContrast)
            {
                Ink = Sub = SystemColors.WindowText; Muted = SystemColors.GrayText; Card = Soft = RowHot = SystemColors.Window; Bg = SystemColors.Control;
                Line = Line2 = Rail = Track = SystemColors.WindowFrame; RowSel = SystemColors.Highlight; BlueDark = SystemColors.HotTrack; BlueSoft = SystemColors.Window;   // tinted panels: the window colour, so their text stays readable
                for (int i = 0; i < 4; i++) { lvText[i] = SystemColors.WindowText; lvSoft[i] = SystemColors.Window; }
            }
        }
        public static bool HighContrast { get { return SystemInformation.HighContrast; } }
        public static Color Lv(int l) { return l == 2 ? Red : l == 1 ? Amber : l == 0 ? Green : Grey; }
        public static Color LvText(int l) { return lvText[l < 0 || l > 3 ? 3 : l]; }
        public static Color LvSoft(int l) { return lvSoft[l < 0 || l > 3 ? 3 : l]; }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath(); float d = Math.Max(1, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
        public static void Fill(Graphics g, RectangleF r, float rad, Color c) { using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p); }
        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w) { using (var p = Round(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p); }

        // The AdeeboMeter mark: a gauge (green, amber, red) with its needle, over a three-stop path. Same drawing as docs/img/logo.svg.
        public static void Logo(Graphics g, RectangleF r, bool arrows) { Logo(g, r, arrows, float.NaN, null); }
        // needle: degrees from straight up (- = green side, + = red side; NaN = the drawn 36); glow: 0..1 per dot (the ping)
        public static void Logo(Graphics g, RectangleF r, bool arrows, float needle, float[] glow)
        {
            float k = r.Width / 64f; bool tiny = r.Width < 24;
            Func<float, float> X = delegate(float v) { return r.X + v * k; };
            Func<float, float> Y = delegate(float v) { return r.Y + v * k; };
            Fill(g, new RectangleF(X(4), Y(4), 56 * k, 56 * k), 14 * k, C("#1155E0"));
            float w = (tiny ? 6.5f : 5f) * k;
            var arc = new RectangleF(X(16), Y(17.6f), 32 * k, 32 * k);
            foreach (var seg in new[] { new { a = 184f, c = "#5EF2B0" }, new { a = 248f, c = "#FCD34D" }, new { a = 312f, c = "#FF7A6B" } })
                using (var p = new Pen(C(seg.c), w)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawArc(p, arc, seg.a, 44); }
            float nx = 40.5f, ny = 21.9f;
            if (!float.IsNaN(needle)) { double a = needle * Math.PI / 180; nx = 32f + (float)(14.46 * Math.Sin(a)); ny = 33.6f - (float)(14.46 * Math.Cos(a)); }
            using (var pn = new Pen(Color.White, (tiny ? 3f : 1.6f) * k)) { pn.StartCap = LineCap.Round; pn.EndCap = LineCap.Round; g.DrawLine(pn, X(32), Y(33.6f), X(nx), Y(ny)); }
            using (var wb = new SolidBrush(Color.White)) { float h = tiny ? 3.4f : 2.6f; g.FillEllipse(wb, X(32 - h), Y(33.6f - h), 2 * h * k, 2 * h * k); }
            if (!tiny) using (var pl = new Pen(Color.White, 0.9f * k)) g.DrawLine(pl, X(18.8f), Y(42.6f), X(45.2f), Y(42.6f));
            using (var db = new SolidBrush(C("#A8F5D4"))) using (var hb = new SolidBrush(Color.White))
            {
                float d = tiny ? 3f : 2.3f; var xs = new[] { 18.8f, 32f, 45.2f };
                for (int i = 0; i < 3; i++) { float gl = glow != null && i < glow.Length ? Math.Max(0, Math.Min(1, glow[i])) : 0, dd = d * (1 + 0.35f * gl); g.FillEllipse(gl > 0.5f ? hb : db, X(xs[i] - dd), Y(42.6f - dd), 2 * dd * k, 2 * dd * k); }
            }
        }
        // Home's gauge (24 Nov mockup): the logo's three arcs without the blue plate, an ink needle that points at the level
        // (0 = the green side, 1 = straight up, 2 = the red side). Drawn still - nothing animates it.
        public static void Gauge(Graphics g, RectangleF r, int level)
        {
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            float d = Math.Min(r.Width, r.Height * 1.55f), w = d * 0.085f;
            var arc = new RectangleF(r.X + (r.Width - d) / 2 + w, r.Y + w, d - 2 * w, d - 2 * w);
            float cx = arc.X + arc.Width / 2, cy = arc.Y + arc.Height / 2, rad = arc.Width / 2;
            foreach (var seg in new[] { new { a = 184f, c = "#5EF2B0" }, new { a = 248f, c = "#FCD34D" }, new { a = 312f, c = "#FF7A6B" } })
                using (var p = new Pen(C(seg.c), w)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawArc(p, arc, seg.a, 44); }
            double a = (level == 2 ? 48 : level == 1 ? 0 : -48) * Math.PI / 180;
            using (var pn = new Pen(Ink, Math.Max(1.5f, d * 0.035f))) { pn.StartCap = LineCap.Round; pn.EndCap = LineCap.Round; g.DrawLine(pn, cx, cy, cx + (float)(rad * 0.78 * Math.Sin(a)), cy - (float)(rad * 0.78 * Math.Cos(a))); }
            using (var hb = new SolidBrush(Ink)) { float h = d * 0.05f; g.FillEllipse(hb, cx - h, cy - h, 2 * h, 2 * h); }
            g.SmoothingMode = sm;
        }
        // one copy of the logo for every window that can show in the taskbar - a window without it showed Windows' plain default icon (1.1.6 test)
        static Icon appIcon; public static Icon AppIcon { get { if (appIcon == null) appIcon = MakeIcon(32); return appIcon; } }
        public static Icon MakeIcon(int size)
        {
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); Logo(g, new RectangleF(0, 0, size, size), false); }
                IntPtr h = bmp.GetHicon();
                try { using (var tmp = Icon.FromHandle(h)) return (Icon)tmp.Clone(); }
                finally { Native.DestroyIcon(h); }
            }
        }
        // A real Windows .ico of the logo (PNG images inside, 16 to 256 px) - for the exe, the Start menu and Settings > Apps.
        public static byte[] IcoBytes()
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            var pngs = new List<byte[]>();
            foreach (int sz in sizes)
                using (var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); Logo(g, new RectangleF(0, 0, sz, sz), sz >= 48); }
                    using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
                }
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int off = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32); w.Write(pngs[i].Length); w.Write(off); off += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
                w.Flush(); return ms.ToArray();
            }
        }
        public static void Pill(Graphics g, string text, Font f, float x, float y, int level, float S)
        {
            var sz = g.MeasureString(text, f);
            Fill(g, new RectangleF(x, y, sz.Width + 8 * S, sz.Height + 1 * S), (sz.Height + S) / 2, LvSoft(level));
            using (var b = new SolidBrush(LvText(level))) g.DrawString(text, f, b, x + 4 * S, y + 0.5f * S);
        }
        public static void Spark(Graphics g, double[] v, RectangleF r, Color c, double cap, bool fill)
        {
            if (v == null || v.Length < 2) return;
            double mx = cap;
            if (mx <= 0) { mx = 1; foreach (var x in v) if (!double.IsNaN(x) && x > mx) mx = x; mx *= 1.15; }
            // AM-CG-205: each run of measured samples is drawn on its own; a missing sample is a gap, never a dip to zero
            var run = new List<PointF>();
            Action flush = () =>
            {
                if (run.Count >= 2)
                {
                    if (fill) { var poly = new List<PointF>(run); poly.Add(new PointF(run[run.Count - 1].X, r.Bottom)); poly.Add(new PointF(run[0].X, r.Bottom)); using (var b = new SolidBrush(Color.FromArgb(56, c))) g.FillPolygon(b, poly.ToArray()); }
                    using (var p = new Pen(c, 1.4f)) g.DrawLines(p, run.ToArray());
                }
                else if (run.Count == 1) using (var b = new SolidBrush(c)) g.FillEllipse(b, run[0].X - 1.2f, run[0].Y - 1.2f, 2.4f, 2.4f);
                run.Clear();
            };
            for (int i = 0; i < v.Length; i++)
            {
                if (double.IsNaN(v[i])) { flush(); continue; }
                double y = Math.Min(1, v[i] / mx);
                run.Add(new PointF(r.X + (float)i / (v.Length - 1) * r.Width, r.Bottom - 1 - (float)y * (r.Height - 2)));
            }
            flush();
        }
    }

    // All fonts made once; nothing is created per paint (a lesson from topogram).
    static partial class Draw
    {
        // review 1.1.6 P4 #7: "h tt" on a Windows format with no AM/PM (French, German, Arabic) loses morning vs evening -
        // there, use the 24-hour clock instead
        public static string Tt(string f)
        {
            var d = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat;
            if (!string.IsNullOrEmpty(d.AMDesignator) && !string.IsNullOrEmpty(d.PMDesignator)) return f;
            return f.Replace("h:mm tt", "H:mm").Replace("h tt", "H':00'").Replace(" tt", "").Replace("tt", "");
        }
        // a status dot that doesn't rely on colour alone: a problem carries a bar across it, a "worth a look" a short upright
        // stroke - red / amber / green still tell apart for colour-blind eyes and in high contrast
        public static void LvDot(Graphics g, int lv, float x, float y, float w, float h)
        {
            using (var b = new SolidBrush(Theme.Lv(lv))) g.FillEllipse(b, x, y, w, h);
            if (w < 8 * S || (lv != 1 && lv != 2)) return;
            using (var p = new Pen(Theme.HighContrast ? SystemColors.Window : Color.White, Math.Max(1.5f, w / 6)))
            {
                float cx = x + w / 2, cy = y + h / 2;
                if (lv == 2) g.DrawLine(p, x + w * 0.25f, cy, x + w * 0.75f, cy); else g.DrawLine(p, cx, y + h * 0.25f, cx, y + h * 0.62f);
            }
        }
        public static float S = 1;
        public static float Base = 1;   // the screen's own scale; the window adds its zoom on top (Cfg.Zoom), the corner widget never does
        // switch the drawing scale: each scale's fonts are made once and kept (at most three sets), so swapping is instant
        static readonly Dictionary<int, Font[]> sets = new Dictionary<int, Font[]>();
        static int K(float s) { return (int)Math.Round(s * 1000); }
        static Font[] Grab() { return new[] { fName, fHead, fBody, fBodyB, fMono, fMonoS, fTiny, fPill, fSmall, fBig, fTitle, fInput, fHero, fValue, fCardNum, fZ, fBtn }; }
        static void Put(Font[] a) { fName = a[0]; fHead = a[1]; fBody = a[2]; fBodyB = a[3]; fMono = a[4]; fMonoS = a[5]; fTiny = a[6]; fPill = a[7]; fSmall = a[8]; fBig = a[9]; fTitle = a[10]; fInput = a[11]; fHero = a[12]; fValue = a[13]; fCardNum = a[14]; fZ = a[15]; fBtn = a[16]; }
        public static void Use(float s)
        {
            if (fName == null) { Init(s); return; }
            if (Math.Abs(s - S) < 0.0005f) return;
            if (!sets.ContainsKey(K(S))) sets[K(S)] = Grab();
            Font[] f; if (sets.TryGetValue(K(s), out f)) { Put(f); S = s; return; }
            // 1.1.6 PC test: an old zoom's fonts are kept, never disposed - a button made at that zoom still holds one, and a
            // disposed font throws "Parameter is not valid" on its next paint. Zoom is 80-175 % in steps, so the sets stay few.
            fName = null; Init(s); sets[K(s)] = Grab();
        }
        public static float ViewScale { get { return Base * Math.Max(80, Math.Min(175, Cfg.Zoom)) / 100f; } }
        public static Font fName, fHead, fBody, fBodyB, fMono, fMonoS, fTiny, fPill, fSmall, fBig, fTitle, fInput;
        static readonly Bitmap measureBmp = new Bitmap(1, 1);
        static Graphics measureG;
        public static Font fHero, fValue, fCardNum, fZ, fBtn;   // fBtn: buttons and tabs (Barlow Bold)   // fCardNum: the 26 px number on the Gamer / metric cards; fZ: the sleeping buddy's "z"
        public static string Fam(params string[] names)
        {
            try
            {
                using (var ifc = new InstalledFontCollection())
                {
                    var have = new HashSet<string>(ifc.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
                    foreach (var n in names) if (have.Contains(n)) return n;
                }
            }
            catch (Exception e) { Program.Log("fonts: " + e.Message); }
            return names[names.Length - 1];
        }
        // Barlow (assets\fonts, SIL Open Font License - OFL-Barlow.txt): the same type as the website and the phone app (23 Nov).
        // 24 Nov: one step heavier than the website - Windows draws Barlow thinner than a browser does (Adeeb: "looks thinner on my
        // PC"): Barlow SemiBold for the writing, Barlow Bold for labels and buttons, Barlow Semi Condensed Bold for headings and big
        // numbers. Their digits are the fixed-width ones (remapped when the fonts were made), so live numbers never jitter.
        // Loaded once from the exe into this process only (nothing installed); Arabic and Bengali keep Windows' own faces.
        static System.Drawing.Text.PrivateFontCollection brand;
        public static string UiFam = "Segoe UI", UiSemi = "Segoe UI", UiDisp = "Segoe UI";
        public static FontStyle UiFamStyle = FontStyle.Regular, UiSemiStyle = FontStyle.Bold, UiDispStyle = FontStyle.Bold;
        static void LoadBrand()
        {
            if (brand != null) return;   // once per process: a zoom change makes new sizes, not new copies of the font files
            try
            {
                var asm = typeof(Draw).Assembly; var pfc = new System.Drawing.Text.PrivateFontCollection();
                foreach (var w in new[] { "Barlow-SemiBold", "Barlow-Bold", "BarlowSemiCondensed-SemiBold", "BarlowSemiCondensed-Bold" })
                    // R2-080: each font on its own - a damaged one never takes the others with it
                    try
                    {
                        using (var rs = asm.GetManifestResourceStream("AdeeboMeter.font." + w + ".gz"))
                        {
                            if (rs == null) { Program.Log("font " + w + ": not in the exe"); continue; }
                            byte[] data; using (var gz = new System.IO.Compression.GZipStream(rs, System.IO.Compression.CompressionMode.Decompress)) using (var ms = new MemoryStream()) { gz.CopyTo(ms); data = ms.ToArray(); }
                            IntPtr mem = Marshal.AllocCoTaskMem(data.Length); Marshal.Copy(data, 0, mem, data.Length);   // kept for the life of the process: GDI+ and GDI read the font from it
                            pfc.AddMemoryFont(mem, data.Length);
                            uint n; Native.AddFontMemResourceEx(mem, (uint)data.Length, IntPtr.Zero, out n);   // the same font for GDI (Windows text boxes and labels)
                        }
                    }
                    catch (Exception fe) { Program.Log("font " + w + ": " + fe.Message); }   // GDI+ reports a bad font file as OutOfMemoryException too
                brand = pfc;
                foreach (var f in pfc.Families)
                {
                    if (f.Name == "Barlow SemiBold") { UiFam = f.Name; UiFamStyle = FontStyle.Regular; }
                    else if (f.Name == "Barlow Bold") { UiSemi = f.Name; UiSemiStyle = FontStyle.Regular; }
                    else if (f.Name == "Barlow Semi Condensed" && f.IsStyleAvailable(FontStyle.Bold)) { UiDisp = f.Name; UiDispStyle = FontStyle.Bold; }
                }
                // one missing weight borrows its neighbour, so the type stays one family
                if (UiSemi == "Segoe UI" && UiFam != "Segoe UI") { UiSemi = UiFam; UiSemiStyle = FontStyle.Bold; }
                if (UiDisp == "Segoe UI" && UiSemi != "Segoe UI") { UiDisp = UiSemi; UiDispStyle = UiSemiStyle; }
            }
            catch (Exception e) { Program.Log("Barlow font: " + e.Message); UiFam = UiSemi = UiDisp = "Segoe UI"; UiFamStyle = FontStyle.Regular; UiSemiStyle = UiDispStyle = FontStyle.Bold; }
        }
        // the newspaper mastheads (Sports, Markets): Windows' Georgia - a serif every PC has, no extra font in the exe
        public static Font Serif(float px, bool black)
        {
            return new Font(L.IsRtl ? "Segoe UI" : L.Code == "bn" ? BengaliFontFamily : "Georgia", px, FontStyle.Bold, GraphicsUnit.Pixel);
        }
        public static FontFamily UiFamilyName() { return UiFamily(UiFam); }
        static FontFamily UiFamily(string name)
        {
            if (brand != null) foreach (var f in brand.Families) if (f.Name == name) return f;
            return new FontFamily(name);
        }
        static string bengaliFontFamily;
        public static string LangFamily { get { return L.Code == "bn" ? BengaliFontFamily : "Segoe UI"; } }   /* review 1.1.6 U1 #16: report and freeze card */
        static string BengaliFontFamily { get { return bengaliFontFamily ?? (bengaliFontFamily = Fam("Nirmala UI", "Vrinda", "Segoe UI")); } }
        // 24 Nov (Adeeb: "very small fonts still have the pixelated issue"): ClearType grid-fitting snaps each letter to whole
        // pixels, and a size like 11.5 or 13.75 px (half sizes times 125%) leaves the strokes landing between pixels - the small
        // text came out uneven. Every size is now a whole pixel, and nothing goes under 12 px.
        public static float Px(float px) { return Math.Max(12f, (float)Math.Round(px)); }
        static Font Ui(string fam, float px, FontStyle st) { px = Px(px); return L.IsRtl || L.Code == "bn" ? new Font(L.IsRtl ? "Segoe UI" : BengaliFontFamily, px, st, GraphicsUnit.Pixel) : new Font(UiFamily(fam), px, st, GraphicsUnit.Pixel); }
        // the self-test's language pass switches fonts for bn / ar and back: it keeps the set it started with and puts it back
        public static object SaveFonts() { return new object[] { Grab(), S }; }
        public static void RestoreFonts(object saved) { var o = (object[])saved; Put((Font[])o[0]); S = (float)o[1]; sets.Clear(); }
        public static void UseLanguageFonts()
        {
            if (!L.IsRtl && L.Code != "bn") return;
            // R2-079: the zoom cache may hold these very fonts - empty it first (fonts not in use now are let go), so a zoom back
            // never puts a disposed font in use
            sets.Clear();   // fonts aren't disposed here: a window or button made earlier may still hold one (a disposed font throws on paint)
            fName = ArabicFont(fName); fHead = ArabicFont(fHead);
            fBody = ArabicFont(fBody); fBodyB = ArabicFont(fBodyB);
            fSmall = ArabicFont(fSmall); fTiny = ArabicFont(fTiny);
            fPill = ArabicFont(fPill); fBig = ArabicFont(fBig);
            fTitle = ArabicFont(fTitle); fHero = ArabicFont(fHero);
            fValue = ArabicFont(fValue); fCardNum = ArabicFont(fCardNum);
            fBtn = ArabicFont(fBtn); fInput = ArabicFont(fInput);
        }
        static Font ArabicFont(Font old)
        {
            if (old == null) return old;
            var font = new Font(L.IsRtl ? "Segoe UI" : BengaliFontFamily, old.Size, old.Style, GraphicsUnit.Pixel);
            return font;   // the old one stays valid for whoever holds it
        }
        public static void Init(float s)
        {
            if (fName != null) return;
            S = s; LoadBrand();
            string mono = Fam("Cascadia Mono", "Consolas"), tb = "Segoe UI";
            // one family, three weights, the same roles as the website: headings and big numbers in Barlow Semi Condensed Bold,
            // labels and buttons in Barlow Bold, the writing in Barlow SemiBold (24 Nov). Barlow is narrower than Inter, so the
            // sizes are half a pixel up to keep the same reading size. Monospace is for code and captures only.
            fName = Ui(UiDisp, 13.5f * S, UiDispStyle);
            fHead = Ui(UiSemi, 14f * S, UiSemiStyle);
            fBody = Ui(UiFam, 13.5f * S, UiFamStyle);
            fBodyB = Ui(UiSemi, 13f * S, UiSemiStyle);
            fSmall = Ui(UiFam, 13f * S, UiFamStyle);
            fMono = new Font(mono, Px(12f * S), FontStyle.Regular, GraphicsUnit.Pixel);
            fMonoS = new Font(mono, Px(11f * S), FontStyle.Regular, GraphicsUnit.Pixel);
            fTiny = Ui(UiSemi, 12f * S, UiSemiStyle);
            fPill = Ui(UiSemi, 11.5f * S, UiSemiStyle);
            fBig = Ui(UiDisp, 21 * S, UiDispStyle);
            fTitle = Ui(UiDisp, 17 * S, UiDispStyle);
            fHero = Ui(UiDisp, 31 * S, UiDispStyle);
            fValue = Ui(UiDisp, 15 * S, UiDispStyle);
            fCardNum = Ui(UiDisp, 27 * S, UiDispStyle); fBtn = Ui(UiSemi, 13f * S, UiSemiStyle); fZ = new Font(tb, 5f * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fInput = Ui(UiFam, 13.5f * S, UiFamStyle);
            measureG = Graphics.FromImage(measureBmp); measureG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; measureG.TextContrast = Contrast;   /* measured as the windows draw */
        }
        // pages measure the same text on every repaint - remember the answers (cleared when it gets big or the scale changes)
        static readonly Dictionary<string, SizeF> measured = new Dictionary<string, SizeF>(); static readonly Queue<string> measuredOrder = new Queue<string>();
        public static SizeF Measure(string t, Font f, int width)
        {
            t = t ?? ""; string key = f.Name + "|" + f.Size.ToString("R", CultureInfo.InvariantCulture) + "|" + (int)f.Style + "|" + width.ToString(CultureInfo.InvariantCulture) + "|" + t;   // the font's own name, size and style (its hash ignores the family)
            lock (measureBmp)
            {
                SizeF sz; if (measured.TryGetValue(key, out sz)) return sz;
                sz = width > 0 ? measureG.MeasureString(t, f, width) : measureG.MeasureString(t, f);
                // bug-hunt L-262: the oldest thousand go when it's full - the rest of the page's text stays measured
                if (measured.Count >= 3000) for (int i = 0; i < 1000 && measuredOrder.Count > 0; i++) measured.Remove(measuredOrder.Dequeue());
                measured[key] = sz; measuredOrder.Enqueue(key); return sz;
            }
        }
        // the corner widget can be drawn a little bigger (tray menu > Bigger widget): same layout, scaled when drawn, so text stays sharp
        public static float WidgetK { get { return Cfg.BigWidget ? 1.3f : 1f; } }
        static Graphics PrepW(Bitmap b) { var g = Prep(b); g.ScaleTransform(WidgetK, WidgetK); return g; }
        static Graphics Prep(Bitmap b) { var g = Graphics.FromImage(b); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = ImageText; g.TextContrast = Contrast; g.Clear(Color.Transparent); return g; }
        public static string Fmt(double v, string f, string unit) { return double.IsNaN(v) ? "-" : v.ToString(f, CultureInfo.CurrentCulture) + unit; }
        public static string Trunc(string s, int n)
        {
            if (s == null) return ""; if (s.Length <= n) return s;
            int cut = Math.Max(0, n - 1); if (cut > 0 && char.IsHighSurrogate(s[cut - 1])) cut--;   // never half an emoji
            return s.Substring(0, cut) + "…";
        }
        public static string MemStr(long b) { return b >= 1073741824 ? (b / 1073741824.0).ToString("0.0", CultureInfo.CurrentCulture) + " GB" : (b / 1048576.0).ToString("0", CultureInfo.CurrentCulture) + " MB"; }
        public static string Word(int l) { return l == 2 ? "Problem" : l == 1 ? "Busy" : "OK"; }

        // ---------- the buddy: a little face for the five dots. All green: smug (a wink now and then). Your PC busy: sweating.
        // Your Wi-Fi or router: dizzy. Past the router (provider, DNS, internet) red: betrayed. Amber anywhere: worried. Asleep
        // on the folded chip while all is quiet. Drawn with a few lines - no pictures, nothing loaded.
        public static string BuddyMood(Snap s)
        {
            // a reaction first (a tool run, an outage starting or ending...) - never on a replayed minute
            string re = s != null && !(s.Chip ?? "").StartsWith("«", StringComparison.Ordinal) ? BuddyReact.Now(s.Level) : null;
            return re ?? BaseMood(s);
        }
        public static string BaseMood(Snap s)
        {
            int worst = -1, lv = 0; for (int i = 0; i < 5 && i < s.Path.Length; i++) if (s.Path[i] >= 1 && s.Path[i] <= 2 && s.Path[i] > lv) { lv = s.Path[i]; worst = i; }
            if (lv == 0) return "smug"; if (lv == 1) return worst == 0 ? "sweaty" : "worried";
            return worst == 0 ? "sweaty" : worst == 1 ? "dizzy" : "betrayed";
        }
        // hats, unlocked by points (Learn and the games earn them); at Halloween and in December the buddy dresses up on its own
        // unless you've picked one. Too small to see on the folded chip, so only drawn on the card and bigger.
        public static readonly string[] Hats = { "", "cap", "party", "crown" }; public static readonly int[] HatPts = { 0, 50, 150, 400 };
        public static string HatNow()
        {
            string h = Cfg.BuddyHat; int i = Array.IndexOf(Hats, h);
            if (i > 0 && Points.Me.Total >= HatPts[i]) return h;
            string se = Mood.Season(); return Voice.Serious ? "" : se == "halloween" ? "witch" : DateTime.Today.Month == 12 ? "santa" : "";
        }
        static void Hat(Graphics g, RectangleF r, string hat)
        {
            float u = r.Width / 20f, cx = r.X + r.Width / 2, top = r.Y + 1.5f * u;
            switch (hat)
            {
                case "cap":
                    using (var b = new SolidBrush(Color.FromArgb(31, 111, 235))) { g.FillPie(b, cx - 6 * u, top - 5 * u, 12 * u, 10 * u, 180, 180); g.FillRectangle(b, cx - 1 * u, top - 0.6f * u, 9 * u, 1.6f * u); }
                    break;
                case "party":
                    using (var b = new SolidBrush(Color.FromArgb(230, 90, 160))) g.FillPolygon(b, new[] { new PointF(cx - 4.5f * u, top + 0.5f * u), new PointF(cx + 4.5f * u, top + 0.5f * u), new PointF(cx + 1 * u, top - 9 * u) });
                    using (var p = new Pen(Color.FromArgb(255, 210, 70), 1.1f * u)) { g.DrawLine(p, cx - 3 * u, top - 1.5f * u, cx + 3.2f * u, top - 1.5f * u); g.DrawLine(p, cx - 1.4f * u, top - 4.5f * u, cx + 2.2f * u, top - 4.5f * u); }
                    using (var b = new SolidBrush(Color.FromArgb(255, 210, 70))) g.FillEllipse(b, cx - 0.4f * u, top - 10.5f * u, 2.8f * u, 2.8f * u);
                    break;
                case "crown":
                    using (var b = new SolidBrush(Color.FromArgb(240, 190, 40))) g.FillPolygon(b, new[] { new PointF(cx - 6 * u, top + 0.5f * u), new PointF(cx - 6 * u, top - 4.5f * u), new PointF(cx - 3 * u, top - 1.5f * u), new PointF(cx, top - 6 * u), new PointF(cx + 3 * u, top - 1.5f * u), new PointF(cx + 6 * u, top - 4.5f * u), new PointF(cx + 6 * u, top + 0.5f * u) });
                    using (var b = new SolidBrush(Color.FromArgb(207, 34, 46))) g.FillEllipse(b, cx - 1 * u, top - 2 * u, 2 * u, 2 * u);
                    break;
                case "santa":
                    using (var b = new SolidBrush(Color.FromArgb(207, 34, 46))) g.FillPolygon(b, new[] { new PointF(cx - 6 * u, top), new PointF(cx + 6 * u, top), new PointF(cx + 2.5f * u, top - 5 * u), new PointF(cx + 8 * u, top - 4 * u), new PointF(cx - 1 * u, top - 7.5f * u) });
                    using (var b = new SolidBrush(Color.FromArgb(245, 245, 245))) using (var p = new Pen(Color.FromArgb(200, 200, 200), Math.Max(1f, u / 3))) { g.FillRectangle(b, cx - 6.5f * u, top - 1 * u, 13 * u, 2.2f * u); g.DrawRectangle(p, cx - 6.5f * u, top - 1 * u, 13 * u, 2.2f * u); g.FillEllipse(b, cx + 7 * u, top - 5.3f * u, 2.6f * u, 2.6f * u); g.DrawEllipse(p, cx + 7 * u, top - 5.3f * u, 2.6f * u, 2.6f * u); }
                    break;
                case "witch":
                    using (var b = new SolidBrush(Color.FromArgb(40, 30, 60))) { g.FillPolygon(b, new[] { new PointF(cx - 4 * u, top), new PointF(cx + 4 * u, top), new PointF(cx + 2.5f * u, top - 9 * u) }); g.FillEllipse(b, cx - 8 * u, top - 1 * u, 16 * u, 2.4f * u); }
                    using (var b = new SolidBrush(Color.FromArgb(230, 120, 30))) g.FillRectangle(b, cx - 3.6f * u, top - 2 * u, 7.2f * u, 1.2f * u);
                    break;
            }
        }
        // ---------- emoji faces (PLAN 11.7c): Twemoji, CC-BY 4.0 (assets\emoji\CREDIT.txt), embedded PNGs loaded on first use
        static readonly Dictionary<string, Bitmap> emoji = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        internal static Bitmap Emoji(string code)
        {
            lock (emoji)
            {
                Bitmap b; if (emoji.TryGetValue(code, out b)) return b;
                try { using (var st = typeof(Draw).Assembly.GetManifestResourceStream("AdeeboMeter.emoji." + code + ".png")) b = st == null ? null : new Bitmap(st); } catch (ArgumentException) { b = null; }
                emoji[code] = b; return b;
            }
        }
        // the window's text: ClearType when Windows uses it (sharp on an LCD, on the window's solid backgrounds), plain
        // smoothing otherwise; the corner widget keeps plain smoothing (it's drawn on see-through pixels)
        // 22 Nov (Adeeb): ClearType grid-fit looked jagged ("writing is not very smooth"); plain anti-aliasing then spaced letters
        // unevenly ("some words look broken"). Grey anti-aliasing with grid fitting: smooth edges, letters on whole pixels, even spacing
        // web-like text (Adeeb, 22 Nov: "doesn't look web standard yet"): browsers on Windows draw grey anti-aliased text with a
        // stronger gamma than GDI+'s default (4), so the strokes look fuller and evener. 1 is close to Chrome/Edge at 13-15 px.
        // 23 Nov (Adeeb, textlab on his PC): contrast 1 with grid-fitting is what made the letters jagged - every row drawn that
        // way looked broken, while contrast 4 looked right. Windows (opaque): ClearType + grid-fit at 4 (his pick D, closest to
        // Windows' own text). Anything drawn on see-through pixels or saved as a picture - the corner widget, cards, exports -
        // can't use ClearType: plain grey smoothing at 4 (his pick C) - see Draw.ImageText.
        public const int Contrast = 4;
        public static TextRenderingHint WindowText { get { return TextRenderingHint.ClearTypeGridFit; } }
        public static TextRenderingHint ImageText { get { return TextRenderingHint.AntiAlias; } }
        // the calm face of a mood (the buddy sheet's chips)
        public static string MoodEmoji(string mood) { switch (mood) { case "roast": return "1f60f"; case "hype": return "1f929"; case "support": return "1f60a"; case "bossy": return "1f624"; case "emo": return "1f614"; case "plain": return "1f610"; default: return "1f60c"; } }
        // the face for a state; the calm face follows the mood
        public static string EmojiFor(string m)
        {
            switch (m)
            {
                case "asleep": return "1f634";
                case "sweaty": return "1f613";
                case "worried": return "1f61f";
                case "dizzy": return "1f635";
                case "betrayed": return "1f612";
                case "smile": return "1f60a";
                default:
                    if (Voice.Serious) return "1f610";
                    switch (Lines.Mood) { case "roast": return "1f60f"; case "hype": return "1f929"; case "support": return "1f60a"; case "bossy": return "1f624"; case "emo": return "1f614"; default: return "1f60c"; }
            }
        }
        // our trick: now and then (New Year, a rare day) the calm face wears a surprise for the day - every mode, Plain too.
        // Only the face, only while all is well, never in anything copied or saved, never permanent.
        static readonly string[] Surprises = { "1f920", "1f92f", "1f970", "1f607", "1f973", "1f929", "1f60e" };
        public static string SurpriseFace(DateTime d)
        {
            if (d.Month == 1 && d.Day == 1) return "1f973";
            uint h = (uint)(d.Year * 1000 + d.DayOfYear) * 2654435761u;
            return h % 23 == 0 ? Surprises[(int)(h / 23 % (uint)Surprises.Length)] : null;
        }
        // the hour mark (PLAN 11.7d): for a moment at the top of the hour the face smiles; at 22:00 and 23:00 it's sleepy.
        // Not during a call or when something's wrong; it only shows if the face is drawn anyway (no extra repaints).
        public static string HourMark(Snap s, DateTime now)
        {
            if (now.Minute != 0 || now.Second > 2 || s == null || s.Call || s.Level > 0 || UiActivity.ToolRunning) return null;
            return now.Hour == 22 || now.Hour == 23 ? "asleep" : "smile";
        }
        public static void Buddy(Graphics g, RectangleF r, Snap s, bool asleep) { Buddy(g, r, s, asleep, false); }
        // forFile: a picture that gets saved or shared - never the surprise face or the hour mark (they're on-screen moments)
        public static void Buddy(Graphics g, RectangleF r, Snap s, bool asleep, bool forFile)
        {
            string re = asleep || forFile || s == null || (s.Chip ?? "").StartsWith("«", StringComparison.Ordinal) ? null : BuddyReact.Now(s.Level);
            string m = asleep ? "asleep" : re ?? BaseMood(s);
            string hm = asleep || forFile || re != null ? null : HourMark(s, DateTime.Now); if (hm != null) m = hm;
            string face = !forFile && re == null && (m == "smug" || m == "smile") ? SurpriseFace(DateTime.Today) : null;
            if (!forFile && re == null) { string mf = Moments.FaceFor(s); if (mf != null) face = mf; }   // a moment (12 s) or the weather (2.5 min) wakes the folded chip too
            if (face == null && re == null && Cfg.BuddyFace == "emoji" && !forFile) face = EmojiFor(m);   // a reaction is always the drawn round face   // a saved picture keeps the drawn face (no third-party artwork to credit inside a shared image)
            var em = face != null ? Emoji(face) : null;
            if (em != null)
            {
                var q = g.InterpolationMode; g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(em, r); g.InterpolationMode = q;
                if (!asleep && r.Width >= 22 * S) { string hat = HatNow(); if (hat != "") Hat(g, r, hat); }
                return;
            }
            if (m == "smile") m = "smug";
            // the colour says how things are: red family for trouble, amber for a worry, grey for no answer / asleep, green otherwise
            bool redF = m == "betrayed" || m == "dizzy" || m == "tears" || m == "shout" || m == "angry" || m == "frustrated" || m == "gloom" || m == "sad";
            bool ambF = m == "worried" || m == "sweaty"; bool greyF = m == "speechless";
            // 21 Nov (Adeeb: "3 colours, distracting - do vector 2D"): flat, one colour that says how things are
            Color body = redF ? Theme.Red : ambF ? Theme.Amber : greyF ? Theme.Muted : Theme.Green;
            // 22 Nov (Adeeb: "the inside was transparent before"): no filled disc - a round outline and the features, all in that one colour
            Color ink = body;
            using (var ring = new Pen(body, Math.Max(1.2f, r.Width / 14f))) { float iw = ring.Width / 2; g.DrawEllipse(ring, r.X + iw, r.Y + iw, r.Width - 2 * iw, r.Height - 2 * iw); }
            float u = r.Width / 20f, cx = r.X + r.Width / 2, ey = r.Y + 8 * u, ex = 4 * u;
            using (var p = new Pen(ink, Math.Max(1f, 1.4f * u)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
            using (var b = new SolidBrush(ink))
            {
                bool wink = m == "smug" && DateTime.Now.Second % 12 == 0;   // now and then - only seen while the card is open
                switch (m)
                {
                    case "asleep":
                        g.DrawArc(p, cx - ex - 2 * u, ey - u, 4 * u, 3 * u, 20, 140); g.DrawArc(p, cx + ex - 2 * u, ey - u, 4 * u, 3 * u, 20, 140);
                        g.DrawLine(p, cx - 1.5f * u, r.Y + 14 * u, cx + 1.5f * u, r.Y + 14 * u);
                        using (var zb = new SolidBrush(body)) g.DrawString("z", fZ ?? fTiny, zb, r.Right - 4 * u, r.Y - 2 * u);
                        break;
                    case "smug":
                        g.DrawLine(p, cx - ex - 1.5f * u, ey, cx - ex + 1.5f * u, ey);   // half-closed, pleased
                        if (wink) g.DrawLine(p, cx + ex - 1.5f * u, ey, cx + ex + 1.5f * u, ey); else g.FillEllipse(b, cx + ex - 1.2f * u, ey - 1.5f * u, 2.4f * u, 2.4f * u);
                        g.DrawArc(p, cx - 4 * u, r.Y + 10 * u, 8 * u, 5 * u, 10, 140);   // a sideways smirk
                        break;
                    case "worried":
                        g.FillEllipse(b, cx - ex - 1.2f * u, ey - u, 2.4f * u, 2.4f * u); g.FillEllipse(b, cx + ex - 1.2f * u, ey - u, 2.4f * u, 2.4f * u);
                        g.DrawLine(p, cx - ex - 2 * u, ey - 3 * u, cx - ex + 1.5f * u, ey - 4 * u); g.DrawLine(p, cx + ex + 2 * u, ey - 3 * u, cx + ex - 1.5f * u, ey - 4 * u);   // brows up
                        g.DrawLine(p, cx - 3 * u, r.Y + 14 * u, cx + 3 * u, r.Y + 14 * u);
                        break;
                    case "sweaty":
                        g.FillEllipse(b, cx - ex - 1.2f * u, ey - u, 2.4f * u, 2.4f * u); g.FillEllipse(b, cx + ex - 1.2f * u, ey - u, 2.4f * u, 2.4f * u);
                        g.DrawArc(p, cx - 3 * u, r.Y + 13 * u, 6 * u, 4 * u, 200, 140);
                        using (var db = new SolidBrush(Color.FromArgb(210, body))) g.FillEllipse(db, r.X + 16 * u, r.Y + 3.2f * u, 2.2f * u, 3.2f * u);   // a drop of sweat
                        break;
                    case "dizzy":
                        foreach (var dx in new[] { -ex, ex }) { g.DrawLine(p, cx + dx - 1.5f * u, ey - 1.5f * u, cx + dx + 1.5f * u, ey + 1.5f * u); g.DrawLine(p, cx + dx - 1.5f * u, ey + 1.5f * u, cx + dx + 1.5f * u, ey - 1.5f * u); }
                        g.DrawEllipse(p, cx - 1.5f * u, r.Y + 12.5f * u, 3 * u, 3 * u);
                        break;
                    // ---- 21 Nov: the reactions (BuddyReact) - still the round face, small marks around it
                    case "happy":
                    case "sparkle":
                    case "love":
                    case "blush":
                        g.DrawArc(p, cx - ex - 1.6f * u, ey - 0.6f * u, 3.2f * u, 2.6f * u, 200, 140); g.DrawArc(p, cx + ex - 1.6f * u, ey - 0.6f * u, 3.2f * u, 2.6f * u, 200, 140);   // ^ ^
                        g.DrawArc(p, cx - 4 * u, r.Y + 9.5f * u, 8 * u, 5.5f * u, 20, 140);   // a wide smile
                        if (m == "blush") using (var cb = new SolidBrush(Color.FromArgb(110, body))) { g.FillEllipse(cb, cx - ex - 2.6f * u, ey + 2 * u, 3 * u, 1.8f * u); g.FillEllipse(cb, cx + ex - 0.4f * u, ey + 2 * u, 3 * u, 1.8f * u); }
                        if (m == "sparkle") Star(g, r.Right - 2.5f * u, r.Y + 2.5f * u, 2.6f * u, body);
                        if (m == "love") Heart(g, r.Right - 3 * u, r.Y + 1.5f * u, 3.2f * u, body);
                        break;
                    case "sad":
                    case "tears":
                        g.FillEllipse(b, cx - ex - 1.2f * u, ey - u, 2.4f * u, 2.4f * u); g.FillEllipse(b, cx + ex - 1.2f * u, ey - u, 2.4f * u, 2.4f * u);
                        g.DrawArc(p, cx - 3 * u, r.Y + 13.5f * u, 6 * u, 4 * u, 200, 140);   // a frown
                        if (m == "tears") using (var tp = new Pen(Color.FromArgb(210, body), Math.Max(1f, 1.2f * u)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                            { g.DrawLine(tp, cx - ex, ey + 2.2f * u, cx - ex, ey + 6.5f * u); g.DrawLine(tp, cx + ex, ey + 2.2f * u, cx + ex, ey + 6.5f * u); }
                        break;
                    case "gloom":
                        g.DrawLine(p, cx - ex - 1.6f * u, ey + 0.5f * u, cx - ex + 1.6f * u, ey + 0.5f * u); g.DrawLine(p, cx + ex - 1.6f * u, ey + 0.5f * u, cx + ex + 1.6f * u, ey + 0.5f * u);   // heavy lids
                        g.DrawLine(p, cx - 2.5f * u, r.Y + 14 * u, cx + 2.5f * u, r.Y + 14 * u);
                        using (var gp = new Pen(body, Math.Max(1f, 0.9f * u)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                            for (int i = 0; i < 3; i++) g.DrawLine(gp, cx - 3 * u + i * 3 * u, r.Y - 3.5f * u, cx - 3 * u + i * 3 * u, r.Y + 0.5f * u);   // the cloud of gloom
                        break;
                    case "angry":
                    case "shout":
                        g.DrawLine(p, cx - ex - 2 * u, ey - 3.6f * u, cx - ex + 1.6f * u, ey - 2.4f * u); g.DrawLine(p, cx + ex + 2 * u, ey - 3.6f * u, cx + ex - 1.6f * u, ey - 2.4f * u);   // brows down
                        g.FillEllipse(b, cx - ex - 1.2f * u, ey - u, 2.4f * u, 2.4f * u); g.FillEllipse(b, cx + ex - 1.2f * u, ey - u, 2.4f * u, 2.4f * u);
                        if (m == "shout") g.FillEllipse(b, cx - 2.2f * u, r.Y + 12 * u, 4.4f * u, 4 * u); else g.DrawLine(p, cx - 3 * u, r.Y + 14 * u, cx + 3 * u, r.Y + 14 * u);
                        using (var ap = new Pen(body, Math.Max(1f, 1.1f * u)))   // the anger mark
                        { float ax = r.Right - 3 * u, ay = r.Y + 2 * u; g.DrawArc(ap, ax - 2 * u, ay - 2 * u, 2 * u, 2 * u, 0, 90); g.DrawArc(ap, ax, ay - 2 * u, 2 * u, 2 * u, 90, 90); g.DrawArc(ap, ax - 2 * u, ay, 2 * u, 2 * u, 270, 90); g.DrawArc(ap, ax, ay, 2 * u, 2 * u, 180, 90); }
                        break;
                    case "frustrated":
                        foreach (var dx in new[] { -ex, ex }) { float sx = dx < 0 ? 1 : -1; g.DrawLine(p, cx + dx - 1.4f * u * sx, ey - 1.4f * u, cx + dx + 1.4f * u * sx, ey); g.DrawLine(p, cx + dx + 1.4f * u * sx, ey, cx + dx - 1.4f * u * sx, ey + 1.4f * u); }   // > <
                        g.DrawLines(p, new[] { new PointF(cx - 3.5f * u, r.Y + 14 * u), new PointF(cx - 1.75f * u, r.Y + 13 * u), new PointF(cx, r.Y + 14 * u), new PointF(cx + 1.75f * u, r.Y + 13 * u), new PointF(cx + 3.5f * u, r.Y + 14 * u) });
                        break;
                    case "speechless":
                        g.FillEllipse(b, cx - ex - 0.9f * u, ey - 0.6f * u, 1.8f * u, 1.8f * u); g.FillEllipse(b, cx + ex - 0.9f * u, ey - 0.6f * u, 1.8f * u, 1.8f * u);
                        g.DrawLine(p, cx - 2 * u, r.Y + 14 * u, cx + 2 * u, r.Y + 14 * u);
                        using (var db = new SolidBrush(body)) for (int i = 0; i < 3; i++) g.FillEllipse(db, r.Right - 6 * u + i * 2.4f * u, r.Y - 0.5f * u, 1.6f * u, 1.6f * u);   // ...
                        break;
                    default:   // betrayed: wide eyes, a wobbly frown, one tear (a hat, if any, is drawn after the switch)
                        g.FillEllipse(b, cx - ex - 1.8f * u, ey - 1.8f * u, 3.6f * u, 3.6f * u); g.FillEllipse(b, cx + ex - 1.8f * u, ey - 1.8f * u, 3.6f * u, 3.6f * u);   // big wet eyes: dark, with a shine
                        using (var hb = new SolidBrush(Color.FromArgb(235, Theme.Card))) { g.FillEllipse(hb, cx - ex - 0.2f * u, ey - 1.2f * u, 1.2f * u, 1.2f * u); g.FillEllipse(hb, cx + ex - 0.2f * u, ey - 1.2f * u, 1.2f * u, 1.2f * u); }
                        g.DrawArc(p, cx - 3.5f * u, r.Y + 13.5f * u, 7 * u, 4 * u, 200, 140);
                        using (var tb = new SolidBrush(Color.FromArgb(210, body))) g.FillEllipse(tb, cx - ex - 0.8f * u, ey + 3.2f * u, 1.6f * u, 2.6f * u);
                        break;
                }
            }
            if (!asleep && r.Width >= 22 * S) { string h = HatNow(); if (h != "") Hat(g, r, h); }
        }
        static void Star(Graphics g, float x, float y, float rad, Color c)
        {
            var pts = new PointF[8]; for (int i = 0; i < 8; i++) { double a = Math.PI / 4 * i - Math.PI / 2; float rr = i % 2 == 0 ? rad : rad * 0.35f; pts[i] = new PointF(x + (float)Math.Cos(a) * rr, y + (float)Math.Sin(a) * rr); }
            using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
        }
        static void Heart(Graphics g, float x, float y, float w, Color c)
        {
            using (var gp = new System.Drawing.Drawing2D.GraphicsPath())
            {
                gp.AddBezier(x, y + w * 0.3f, x, y - w * 0.1f, x - w * 0.5f, y - w * 0.1f, x - w * 0.5f, y + w * 0.3f);
                gp.AddBezier(x - w * 0.5f, y + w * 0.3f, x - w * 0.5f, y + w * 0.6f, x, y + w * 0.8f, x, y + w);
                gp.AddBezier(x, y + w, x, y + w * 0.8f, x + w * 0.5f, y + w * 0.6f, x + w * 0.5f, y + w * 0.3f);
                gp.AddBezier(x + w * 0.5f, y + w * 0.3f, x + w * 0.5f, y - w * 0.1f, x, y - w * 0.1f, x, y + w * 0.3f);
                using (var b = new SolidBrush(c)) g.FillPath(b, gp);
            }
        }
        // the folded chip's words: a tile preset's own, the game's ping while the gamer card is up, else the verdict's
        public static string ChipWords(Snap s)
        {
            string st = Style;
            if (Corner.IsTile(st) && !(s.Level == 2 && !s.Waiting)) { var w = TileChip(st, s); if (!string.IsNullOrEmpty(w)) return Trunc(w, 48); }   // hunters12 display M-1: a network problem folds to the verdict, not a price
            if (!SpeedStyle && Layout(s.Call) == "gamer") { var w = GamerChip(); if (!string.IsNullOrEmpty(w)) return Trunc(w, 48); }
            return s.Chip;
        }
        public static Bitmap Chip(Snap s, double dwell)
        {
            if (SpeedStyle) return SpeedChip(s, dwell);
            string words = ChipWords(s); var ts = Measure(words, fSmall, 0);
            float ww = Layout(s.Call) == "everyday" && !Corner.IsTile(Style) ? WeatherChipW() : 0; /* AM-CG-212: weather on the chip only where the open card shows it too */ int W = (int)(9 * S + 16 * S + 6 * S + 8 * S + 6 * S + ts.Width + 9 * S + (Cfg.Buddy ? 8 * S : 0) + ww), H = (int)(26 * S);
            var bmp = new Bitmap((int)Math.Ceiling(W * WidgetK), (int)Math.Ceiling(Math.Min(H, 900 * S) * WidgetK), PixelFormat.Format32bppArgb);   /* review 1.1.6 R5 N4: a card is never taller than 900 px, whatever the font metrics say */
            using (var g = PrepW(bmp))
            {
                var r = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, r, H / 2f, Color.FromArgb(246, Theme.Card)); bool bad = s.Level == 2 && !s.Waiting; Theme.Stroke(g, r, H / 2f, bad ? Theme.Red : Theme.Line, bad ? 1.5f : 1);   /* hunters12 display M-1: a problem shows on the folded strip, not only in its 8 px dot */
                float x = 9 * S;
                if (Cfg.Buddy) { Buddy(g, new RectangleF(x - 2 * S, (H - 20 * S) / 2, 20 * S, 20 * S), s, s.Level == 0 && !s.Call); x += 36 * S; }   // folded and all quiet: asleep
                else
                {
                    Theme.Logo(g, new RectangleF(x, (H - 16 * S) / 2, 16 * S, 16 * S), false); x += 22 * S;
                    using (var b = new SolidBrush(Theme.Lv(s.Level))) g.FillEllipse(b, x, (H - 8 * S) / 2, 8 * S, 8 * S); x += 14 * S;
                }
                using (var b = new SolidBrush(Theme.Ink)) g.DrawString(words, fSmall, b, x, (H - ts.Height) / 2);
                if (ww > 0) WeatherChipDraw(g, x + ts.Width + 6 * S, H);
                if (dwell > 0)
                {
                    var tr = new RectangleF(10 * S, H - 4 * S, W - 20 * S, 2 * S);
                    using (var b = new SolidBrush(Theme.Track)) g.FillRectangle(b, tr);
                    using (var b = new SolidBrush(Theme.Blue)) g.FillRectangle(b, tr.X, tr.Y, tr.Width * (float)Math.Min(1, dwell), tr.Height);
                }
            }
            return bmp;
        }

        // which layout the widget card uses: Auto follows what you're doing - Gamer while a game you added is open, Calls during
        // a call - and is Everyday the rest of the time. Engineer is chosen by hand (Pro mode).
        public static string Layout(bool call)
        {
            string st = Style; var gp = GamePing.Me.Playing;
            if (st == "gamer" || (st == "auto" && gp != null && Cfg.GameWidget)) { if (GamerTarget() != null) return "gamer"; if (st == "gamer") return "everyday"; }   // AM-CG-213: auto goes on to the call check
            if (st == "calls" || (st == "auto" && call)) return "calls";
            if (st == "engineer") return Cfg.Simple ? "everyday" : "engineer";
            return "everyday";
        }
        // the game and server the gamer layout shows: the one being played, else the first game you added; its best region
        static Tuple<Game, GameServer> GamerTarget()
        {
            var games = Games.All; var mon = Games.Monitored(); var id = GamePing.Me.Playing;
            var g = games.FirstOrDefault(z => z.Id == id) ?? games.FirstOrDefault(z => mon.Contains(z.Id) && z.Servers.Count > 0);
            if (g == null || g.Servers.Count == 0) return null;
            return Tuple.Create(g, GamePing.Me.Best(g) ?? g.Servers[0]);
        }
        public static Bitmap GamerCard(Snap s, Dictionary<string, RectangleF> hits)
        {
            hits.Clear();
            var t = GamerTarget(); if (t == null) return Draw.Card(s, hits);   // the game ended between choosing this card and drawing it
            var st = GamePing.Me.Stat(t.Item1.Id, t.Item2); var v = st.Window(60, 3600);
            double now = v.Length > 0 ? v[v.Length - 1] : double.NaN, avg = GameStat.Avg(v), jit = GameStat.Jitter(v), loss = GameStat.Loss(v);
            string grade = v.Length == 0 ? "Waiting" : GameStat.Grade(avg, jit, double.IsNaN(loss) ? 0 : loss);
            Color gc = grade == "Competitive" ? Theme.Green : grade == "Casual" ? Theme.Amber : grade == "Laggy" ? Theme.Red : Theme.Grey;
            Color gt = grade == "Competitive" ? Theme.LvText(0) : grade == "Casual" ? Theme.LvText(1) : grade == "Laggy" ? Theme.LvText(2) : Theme.Muted;   // H6-ML3: text in the grade's readable shade (4.5:1), the raw colour only for the wash and the graph
            Spike last = null; lock (GamePing.Me.Spikes) last = GamePing.Me.Spikes.FirstOrDefault(z => z.Game == t.Item1.Id && (DateTime.Now - z.T).TotalSeconds < 20);
            string head = last != null ? L.T("Spike") + " " + Math.Round(last.Ms) + " ms: " + L.T(last.Blame) + "." : t.Item1.Name + " · " + t.Item2.Name + (grade == "Competitive" ? L.T(" is steady.") : grade == "Casual" ? L.T(" is playable.") : grade == "Laggy" ? L.T(" is laggy.") : "...");
            string gradeW = L.T(grade);   /* review 1.1.6 U4 #3 */
            int W = (int)(264 * S); float pad = 12 * S, inner = W - 2 * pad;
            float headH = Measure(head, fHead, (int)inner).Height, wh = HintLineH(inner);
            int H = (int)Math.Ceiling(pad + 24 * S + headH + wh + 8 * S + 46 * S + 30 * S + 20 * S + 14 * S + 12 * S + pad);   // bug-hunt L-147: the path strip is 12 high
            var bmp = new Bitmap((int)Math.Ceiling(W * WidgetK), (int)Math.Ceiling(Math.Min(H, 900 * S) * WidgetK), PixelFormat.Format32bppArgb);   /* review 1.1.6 R5 N4: a card is never taller than 900 px, whatever the font metrics say */
            using (var g = PrepW(bmp)) using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                var cr = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, cr, 16 * S, Color.FromArgb(250, Theme.Card)); Theme.Stroke(g, cr, 16 * S, last != null ? Theme.Amber : Theme.Line, last != null ? 1.5f : 1f);
                float y = pad;
                Theme.Logo(g, new RectangleF(pad, y, 18 * S, 18 * S), false);
                g.DrawString(L.T("AdeeboMeter"), fName, ink, pad + 24 * S, y + 1 * S);
                float nw = Measure(L.T("AdeeboMeter"), fName, 0).Width;
                var tag = new RectangleF(pad + 24 * S + nw + 4 * S, y + 2 * S, Measure(Kit.Sent("GAME"), fTiny, 0).Width + 10 * S, 15 * S);
                Theme.Fill(g, tag, tag.Height / 2, Theme.BlueSoft); using (var tb = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.BlueDark)) g.DrawString(Kit.Sent("GAME"), fTiny, tb, tag.X + 5 * S, tag.Y + 1 * S);
                y += 24 * S;
                using (var hb = new SolidBrush(last != null ? Theme.LvText(1) : Theme.Ink)) g.DrawString(head, fHead, hb, new RectangleF(pad, y, inner, headH + 2)); y += headH; HintLine(g, pad, y, inner, wh); y += wh + 8 * S;
                // (AM-CG-207: no hover area here - there's no detail card for it)
                string big = double.IsNaN(now) ? "-" : Math.Round(now).ToString(CultureInfo.InvariantCulture);
                var bf = fCardNum; using (var bb = new SolidBrush(gt))
                {
                    g.DrawString(big, bf, bb, pad - 2 * S, y); float bw = g.MeasureString(big, bf).Width;
                    g.DrawString(L.T("ms"), fBody, muted, pad + bw - 2 * S, y + 16 * S);
                }
                var gp = new RectangleF(W - pad - Measure(gradeW, fTiny, 0).Width - 12 * S, y + 12 * S, Measure(gradeW, fTiny, 0).Width + 12 * S, 17 * S);
                Theme.Fill(g, gp, gp.Height / 2, Color.FromArgb(Theme.Dark ? 60 : 40, gc)); using (var gb = new SolidBrush(Theme.Dark ? Theme.Ink : gt)) g.DrawString(gradeW, fTiny, gb, gp.X + 6 * S, gp.Y + 2 * S);
                y += 46 * S;
                var sr = new RectangleF(pad, y, inner, 26 * S); Theme.Fill(g, sr, 9 * S, Theme.Soft);
                double cap = 30; foreach (var x in v) if (!double.IsNaN(x) && x > cap) cap = x;
                Theme.Spark(g, v, sr, gc, cap * 1.15, false);
                y += 30 * S;
                string row = L.T("jitter") + " " + Fmt(jit, "0.0", " ms") + "   " + L.T("loss") + " " + Fmt(loss, "0.#", "%") + "   " + Pair(s.Down, s.Up);
                g.DrawString(row, fSmall, muted, pad, y); y += 20 * S;
                hits["path"] = new RectangleF(pad - 4 * S, y - 4 * S, inner + 8 * S, 30 * S);
                g.DrawString(t.Item2.Where + " · " + L.T(t.Item2.Kind == "estimate" ? "region estimate" : t.Item2.Kind == "valve" ? "Valve server" : "exact"), fTiny, muted, pad, y); y += 14 * S;
                PathStrip(g, s, new RectangleF(pad, y, inner, 12 * S), false);
            }
            if (WidgetK != 1f) foreach (var k in hits.Keys.ToList()) { var r = hits[k]; hits[k] = new RectangleF(r.X * WidgetK, r.Y * WidgetK, r.Width * WidgetK, r.Height * WidgetK); }
            return bmp;
        }
        // Calls: what a call cares about - steadiness (jitter), loss and the delay; the VPN as its own line when one is up
        public static Bitmap CallsCard(Snap s, Dictionary<string, RectangleF> hits)
        {
            double jit = s.Jitter, loss = s.LossPct, ping = s.Ping;
            bool partial = double.IsNaN(jit) || double.IsNaN(loss);
            int lv = double.IsNaN(ping) ? 3 : (jit > 30 || loss > 3 || ping > 250) ? 2 : (jit > 15 || loss > 1 || ping > 150) ? 1 : partial ? 3 : 0;   // AM-CG-208: missing jitter or loss is never "clear"
            string grade = lv == 0 ? "LINE OK" : lv == 1 ? "SOME BREAKUP" : lv == 2 ? "CHOPPY" : "WAITING";
            string head = s.Call ? (lv == 0 ? "Your connection looks fine for calls (an estimate from the line - your call app's own numbers can differ)." : lv == 1 ? "Your connection may make calls break up now and then." : lv == 2 ? "Your connection will likely make calls choppy." : partial && !double.IsNaN(ping) ? "Not enough measured yet to judge calls (jitter or loss still missing)." : "Measuring your connection...") : "No call right now - this is how the line looks for one.";
            string vpnLine = s.PathLabel != null && s.PathLabel.Length > 2 && s.PathLabel[2] == "VPN" ? L.T("through the VPN") + " · " + (s.PathSub != null && s.PathSub.Length > 2 ? s.PathSub[2] : "") : "";
            return MetricCard(s, hits, "CALL", head, lv, double.IsNaN(jit) ? "-" : Math.Round(jit).ToString(CultureInfo.InvariantCulture), "ms jitter", grade,
                s.NetHist, new[] { L.T("delay") + " " + Fmt(ping, "0", " ms") + "   " + L.T("loss") + " " + Fmt(loss, "0.#", "%") + "   ↑ " + Rate(s.Up, true), vpnLine });
        }
        // Engineer: the raw numbers - router and internet delay, DNS, TCP retransmits / resets, throughput
        public static Bitmap EngineerCard(Snap s, Dictionary<string, RectangleF> hits)
        {
            string h1 = s.Health != null && s.Health.Count > 0 ? s.Health[0][0] + " " + s.Health[0][1] : "", h2 = s.Health != null && s.Health.Count > 1 ? s.Health[1][0] + " " + s.Health[1][1] : "";
            double gw = s.GwMs;
            return MetricCard(s, hits, "ENG", s.Headline, s.Level, Fmt(s.Ping, "0", ""), "ms internet", s.Level == 0 ? "OK" : s.Level == 1 ? "SLOW" : s.Level == 2 ? "PROBLEM" : "WAITING",
                s.NetHist, new[] { L.T("router") + " " + Fmt(gw, "0", " ms") + "   DNS " + Fmt(s.DnsMs, "0", " ms") + "   " + L.T("jitter") + " " + Fmt(s.Jitter, "0.0", " ms") + "   " + L.T("loss") + " " + Fmt(s.LossPct, "0.#", "%"), (h1 + (h2 != "" ? "   " + h2 : "")).Trim(), "↓" + Fmt(s.Down, "0.0", "") + " ↑" + Fmt(s.Up, "0.0", " Mb/s") + "   CPU " + Fmt(s.Cpu, "0", "%") + "   RAM " + Fmt(s.Mem, "0", "%") });
        }
        // the shared shape: tag, headline, one big number with its level colour and a grade pill, a 60-sample sparkline,
        // a few small rows, then the five-dot path strip
        static Bitmap MetricCard(Snap s, Dictionary<string, RectangleF> hits, string tagText, string head, int lv, string big, string unit, string grade, double[] series, string[] rows)
        {
            hits.Clear();
            head = L.T(head); unit = L.T(unit); grade = L.T(grade); tagText = L.T(tagText);   /* review 1.1.6 U4 #3: the widget card speaks the app's language */
            var lines = rows.Where(r => !string.IsNullOrEmpty(r)).ToArray();
            Color gc = lv == 0 ? Theme.Green : lv == 1 ? Theme.Amber : lv == 2 ? Theme.Red : Theme.Grey;
            int W = (int)(264 * S); float pad = 12 * S, inner = W - 2 * pad;
            float headH = Measure(head, fHead, (int)inner).Height, wh = HintLineH(inner);
            int H = (int)Math.Ceiling(pad + 24 * S + headH + wh + 8 * S + 46 * S + 30 * S + lines.Length * 18 * S + 4 * S + 12 * S + pad);   // L-147
            var bmp = new Bitmap((int)Math.Ceiling(W * WidgetK), (int)Math.Ceiling(Math.Min(H, 900 * S) * WidgetK), PixelFormat.Format32bppArgb);   /* review 1.1.6 R5 N4: a card is never taller than 900 px, whatever the font metrics say */
            using (var g = PrepW(bmp)) using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                var cr = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, cr, 16 * S, Color.FromArgb(250, Theme.Card)); Theme.Stroke(g, cr, 16 * S, lv == 1 || lv == 2 ? gc : Theme.Line, lv == 1 || lv == 2 ? 1.5f : 1f);
                float y = pad;
                Theme.Logo(g, new RectangleF(pad, y, 18 * S, 18 * S), false);
                g.DrawString(L.T("AdeeboMeter"), fName, ink, pad + 24 * S, y + 1 * S);
                float nw = Measure(L.T("AdeeboMeter"), fName, 0).Width;
                var tag = new RectangleF(pad + 24 * S + nw + 4 * S, y + 2 * S, Measure(tagText, fTiny, 0).Width + 10 * S, 15 * S);
                Theme.Fill(g, tag, tag.Height / 2, Theme.BlueSoft); using (var tb = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.BlueDark)) g.DrawString(tagText, fTiny, tb, tag.X + 5 * S, tag.Y + 1 * S);
                y += 24 * S;
                g.DrawString(head, fHead, ink, new RectangleF(pad, y, inner, headH + 2)); y += headH; HintLine(g, pad, y, inner, wh); y += wh + 8 * S;
                // (AM-CG-207: no hover area here - there's no detail card for it)
                var bf = fCardNum; using (var bb = new SolidBrush(lv == 3 ? Theme.Ink : gc))
                {
                    g.DrawString(big, bf, bb, pad - 2 * S, y); float bw = g.MeasureString(big, bf).Width;
                    g.DrawString(unit, fBody, muted, pad + bw - 2 * S, y + 16 * S);
                }
                float pw = Measure(grade, fTiny, 0).Width + 12 * S; var gp = new RectangleF(W - pad - pw, y + 12 * S, pw, 17 * S);
                Theme.Fill(g, gp, gp.Height / 2, Color.FromArgb(Theme.Dark ? 60 : 40, gc)); using (var gb = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.LvText(lv))) g.DrawString(grade, fTiny, gb, gp.X + 6 * S, gp.Y + 2 * S);
                y += 46 * S;
                var sr = new RectangleF(pad, y, inner, 26 * S); Theme.Fill(g, sr, 9 * S, Theme.Soft);
                var v = series ?? Array.Empty<double>(); double cap = 30; foreach (var x in v) if (!double.IsNaN(x) && x > cap) cap = x;
                Theme.Spark(g, v, sr, gc, cap * 1.15, false);
                y += 30 * S;
                foreach (var l in lines) { using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(l, fSmall, muted, new RectangleF(pad, y, inner, 18 * S), sf); y += 18 * S; }
                y += 4 * S;
                hits["path"] = new RectangleF(pad - 4 * S, y - 4 * S, inner + 8 * S, 20 * S);
                PathStrip(g, s, new RectangleF(pad, y, inner, 12 * S), false);
            }
            if (WidgetK != 1f) foreach (var k in hits.Keys.ToList()) { var r = hits[k]; hits[k] = new RectangleF(r.X * WidgetK, r.Y * WidgetK, r.Width * WidgetK, r.Height * WidgetK); }
            return bmp;
        }
        public static volatile bool CardHovered = true;   // set by the widget before it draws the card
        // the hint line (24 Nov): where the second, grey fun line used to be - how to move the widget, by its settings; "" = none
        public static volatile string WidgetHint = "";
        static float HintLineH(float inner) { return string.IsNullOrEmpty(WidgetHint) ? 0 : Measure(WidgetHint, fSmall, (int)inner).Height + 4 * S; }
        static void HintLine(Graphics g, float pad, float y, float inner, float h) { if (h <= 0) return; using (var sf = new StringFormat(L.IsRtl ? StringFormatFlags.DirectionRightToLeft : 0)) using (var b = new SolidBrush(Theme.BlueText)) g.DrawString(WidgetHint, fSmall, b, new RectangleF(pad, y + 4 * S, inner, h), sf); }
        public static Bitmap Card(Snap s, Dictionary<string, RectangleF> hits)
        {
            if (SpeedStyle) return SpeedCard(s, hits);
            if (Corner.IsTile(Style)) return TileCard(s, hits, Style);
            string lay = Layout(s.Call);
            if (lay == "gamer") return GamerCard(s, hits);
            if (lay == "calls") return CallsCard(s, hits);
            if (lay == "engineer") return EngineerCard(s, hits);
            hits.Clear();
            int W = (int)(264 * S); float pad = 12 * S, inner = W - 2 * pad;
            // one line, not two (Adeeb, 24 Nov): with Fun lines on, the mood's line (or the season's) says the verdict in its own
            // words, in the verdict's place and colour; Fun lines off, or nothing to say, it's the plain verdict ("All quiet")
            bool replay = ReferenceEquals(s, AppCtx.Rewind);   /* a replayed minute gets no quip: it would reset the outage and koan clocks */
            string quip = replay ? "" : Mood.Quip(s), season = replay ? "" : Mood.SeasonLine(s);
            string head = s.Level == 2 && !s.Waiting ? s.Headline : quip != "" ? quip : season != "" ? season : s.Headline;   // hunters12 display L-1: a problem keeps the plain verdict
            float headH = Measure(head, fHead, (int)inner).Height, quipH = HintLineH(inner);
            float hintH = string.IsNullOrEmpty(s.Hint) ? 0 : Measure(L.T("Why: ") + s.Hint, fSmall, (int)inner).Height + 4 * S;
            string wtx = WeatherCardText();
            float h = pad + 24 * S + headH + quipH + hintH + 10 * S + 4 * 21 * S + (s.Call ? 44 * S : 0) + 10 * S + 34 * S + (wtx != null ? 24 * S : 0) + pad;
            int H = (int)Math.Ceiling(h);
            var bmp = new Bitmap((int)Math.Ceiling(W * WidgetK), (int)Math.Ceiling(Math.Min(H, 900 * S) * WidgetK), PixelFormat.Format32bppArgb);   /* review 1.1.6 R5 N4: a card is never taller than 900 px, whatever the font metrics say */
            using (var g = PrepW(bmp))
            using (var ink = new SolidBrush(Theme.Ink))
            using (var muted = new SolidBrush(Theme.Muted))
            {
                var cr = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, cr, 16 * S, Color.FromArgb(250, Theme.Card));
                Theme.Stroke(g, cr, 16 * S, s.Level == 2 ? Theme.Red : Theme.Line, s.Level == 2 ? 1.5f : 1f);
                float y = pad;
                Theme.Logo(g, new RectangleF(pad, y, 18 * S, 18 * S), false);
                g.DrawString(L.T("AdeeboMeter"), fName, ink, pad + 24 * S, y + 1 * S);
                float nw = Measure(L.T("AdeeboMeter"), fName, 0).Width;
                Theme.Pill(g, s.Waiting ? L.T("Checking") : L.T(Word(s.Level)), fPill, pad + 24 * S + nw + 2 * S, y + 2 * S, s.Waiting ? 3 : s.Level, S);   // review 1.1.6 U4 #3, D1 #10
                if (Cfg.Buddy) Buddy(g, new RectangleF(W - pad - 24 * S, y, 24 * S, 24 * S), s, false);   // the buddy, awake, in the corner of the card
                else if (CardHovered) { string cd = s.Countdown + "s"; var cs = Measure(cd, fSmall, 0); g.DrawString(cd, fSmall, muted, W - pad - cs.Width, y + 2 * S); }   // the countdown only while you look: unhovered, the card isn't redrawn each second
                y += 24 * S;
                using (var rsf = new StringFormat(L.IsRtl ? StringFormatFlags.DirectionRightToLeft : 0))   /* review 1.1.6 U4 #8: Arabic paragraphs read right to left */
                {
                    using (var hb = new SolidBrush(s.Level == 0 ? Theme.Ink : Theme.LvText(s.Level))) g.DrawString(head, fHead, hb, new RectangleF(pad, y, inner, headH + 2), rsf);
                    y += headH;
                    if (hintH > 0) { using (var wb = new SolidBrush(Theme.Sub)) g.DrawString(AdeeboMeter.L.T("Why: ") + s.Hint, fSmall, wb, new RectangleF(pad, y + 4 * S, inner, hintH), rsf); y += hintH; }
                    HintLine(g, pad, y, inner, quipH); y += quipH;   // the move hint, by the settings - after the verdict and its Why
                }
                y += 10 * S;
                // 24 Nov (widget mockup): the same four readings as Home, in the same order - this PC, the router, the internet,
                // your traffic - a dot in each one's level, the number on the right
                {
                    bool memHot = !double.IsNaN(s.Mem) && s.Mem >= 85 && (double.IsNaN(s.Cpu) || s.Mem - 85 >= s.Cpu - 85);
                    int pcLv = s.Path.Length > 0 ? s.Path[0] : 3, gwLv = s.Path.Length > 1 ? s.Path[1] : 3, netLv = s.Path.Length > 4 ? s.Path[4] : 3;
                    var rows = new[] {
                        Tuple.Create("cpu", L.T("This PC"), memHot ? Fmt(s.Mem, "0", "% ") + L.T("memory") : Fmt(s.Cpu, "0", "% ") + Kit.Sent("CPU"), pcLv),
                        Tuple.Create("path", L.T("Router"), Fmt(s.GwMs, "0", " ms"), gwLv),
                        Tuple.Create("inet", L.T("Internet"), Fmt(s.Ping, "0", " ms"), netLv),
                        Tuple.Create("net", L.T("Traffic"), "↓ " + Rate(s.Down, true) + "  ↑ " + Rate(s.Up, true), 3) };
                    foreach (var rw in rows)
                    {
                        hits[rw.Item1] = new RectangleF(pad - 4 * S, y - 2 * S, inner + 8 * S, 21 * S);
                        Draw.LvDot(g, rw.Item4, pad, y + 6 * S, 7 * S, 7 * S);
                        using (var lb = new SolidBrush(Theme.Sub)) g.DrawString(rw.Item2, fSmall, lb, pad + 14 * S, y + 1 * S);
                        var vs = Measure(rw.Item3, fSmall, 0);
                        using (var vb = new SolidBrush(rw.Item4 == 1 || rw.Item4 == 2 ? Theme.LvText(rw.Item4) : Theme.Ink)) g.DrawString(rw.Item3, fSmall, vb, W - pad - vs.Width, y + 1 * S);
                        y += 21 * S;
                    }
                    y += 6 * S;
                }
                if (s.Call)
                {
                    bool bad = s.Jitter > 30 || s.LossPct > 2;
                    var r = new RectangleF(pad, y, inner, 38 * S); hits["call"] = r;
                    Theme.Fill(g, r, 7 * S, bad ? Theme.LvSoft(1) : Theme.BlueSoft);
                    using (var b = new SolidBrush(bad ? Theme.LvText(1) : Theme.BlueDark))
                    {
                        g.DrawString(Kit.Sent("MEETING MODE · TEAMS CALL"), fTiny, b, r.X + 8 * S, r.Y + 4 * S);
                        g.DrawString(AdeeboMeter.L.T("up ") + Rate(s.Up, true) + "   " + AdeeboMeter.L.T("jitter") + " " + Fmt(s.Jitter, "0", " ms") + "   " + AdeeboMeter.L.T("loss") + " " + Fmt(s.LossPct, "0", "%"), fSmall, b, r.X + 8 * S, r.Y + 19 * S);
                    }
                    y += 44 * S;
                }
                y += 4 * S;
                var gr = new RectangleF(pad, y, inner, 34 * S);   // the traffic graph under the readings (the speeds are in the Traffic row)
                Theme.Fill(g, gr, 9 * S, Theme.Soft);
                double cap = 1; foreach (var v in s.DownHist) if (!double.IsNaN(v) && v > cap) cap = v; foreach (var v in s.UpHist) if (!double.IsNaN(v) && v > cap) cap = v;
                Theme.Spark(g, s.DownHist, gr, Theme.Blue, cap * 1.15, true);
                Theme.Spark(g, s.UpHist, gr, Theme.Orange, cap * 1.15, false);
                // data used today, in the graph's top corners (no extra height); this month against a cap at the bottom right
                var du = Usage.TodayDU();
                using (var tb = new SolidBrush(Theme.Muted))
                {
                    g.DrawString(AdeeboMeter.L.T("today ↓ ") + Usage.Mb(du[0]), fTiny, tb, gr.X + 4 * S, gr.Y + 2 * S);
                    string ut = "↑ " + Usage.Mb(du[1]); g.DrawString(ut, fTiny, tb, gr.Right - 4 * S - Measure(ut, fTiny, 0).Width, gr.Y + 2 * S);
                    if (Cfg.CapGB > 0) { string ct = (Usage.Month(null) / 1000).ToString("0", CultureInfo.CurrentCulture) + " / " + Cfg.CapGB + " GB"; g.DrawString(ct, fTiny, tb, gr.Right - 4 * S - Measure(ct, fTiny, 0).Width, gr.Bottom - 2 * S - Measure(ct, fTiny, 0).Height); }
                }
                if (wtx != null) WeatherCardDraw(g, pad, gr.Bottom + 8 * S, inner, wtx);
            }
            if (WidgetK != 1f) foreach (var k in hits.Keys.ToList()) { var r = hits[k]; hits[k] = new RectangleF(r.X * WidgetK, r.Y * WidgetK, r.Width * WidgetK, r.Height * WidgetK); }
            return bmp;
        }

        public static void Bar(Graphics g, string key, string label, double v, string txt, double yl, double rl, float x, ref float y, float inner, Dictionary<string, RectangleF> hits)
        {
            if (hits != null) hits[key] = new RectangleF(x - 4 * S, y - 2 * S, inner + 8 * S, 19 * S);
            int lv = double.IsNaN(v) ? 3 : v >= rl ? 2 : v >= yl ? 1 : 0;
            using (var b = new SolidBrush(Theme.Muted)) g.DrawString(label, fBody, b, x, y);
            var ts = Measure(txt, fBodyB, 0);   // 23 Nov: numbers in Barlow's fixed-width digits, not monospace
            float tw = Math.Max(44 * S, ts.Width + 6 * S);
            var tr = new RectangleF(x + 66 * S, y + 6 * S, Math.Max(10, inner - 66 * S - tw), 7 * S);
            Theme.Fill(g, tr, 3.5f * S, Theme.Track);
            if (!double.IsNaN(v)) Theme.Fill(g, new RectangleF(tr.X, tr.Y, Math.Max(7 * S, tr.Width * (float)Math.Min(100, v) / 100f), tr.Height), 3.5f * S, Theme.Lv(lv));
            using (var b = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(txt, fBodyB, b, x + inner - ts.Width, y);
            y += 20 * S;
        }

        public static void PathStrip(Graphics g, Snap s, RectangleF r, bool big)
        {
            int n = 5; float slot = r.Width / n, dot = (big ? 18 : 12) * S; Font lf = big ? fName : fSmall;
            using (var line = new Pen(Theme.Rail, 2 * S))
                for (int i = 0; i < n - 1; i++) g.DrawLine(line, r.X + slot * i + slot / 2 + dot / 2 + 2 * S, r.Y + dot / 2, r.X + slot * (i + 1) + slot / 2 - dot / 2 - 2 * S, r.Y + dot / 2);
            for (int i = 0; i < n; i++)
            {
                float cx = r.X + slot * i + slot / 2; int lv = s.Path[i];
                Draw.LvDot(g, lv, cx - dot / 2, r.Y, dot, dot);
                string lab = s.PathLabel[i] ?? ""; var ls = Measure(lab, lf, 0);
                using (var b = new SolidBrush(lv == 2 ? Theme.LvText(2) : Theme.Ink)) g.DrawString(lab, lf, b, cx - ls.Width / 2, r.Y + dot + 3 * S);
                if (big)
                {
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    {
                        using (var b = new SolidBrush(Theme.Sub)) g.DrawString(s.PathMain[i] ?? "", fBody, b, new RectangleF(cx - slot / 2 + 4 * S, r.Y + dot + 22 * S, slot - 8 * S, 18 * S), sf);
                        using (var b = new SolidBrush(Theme.Muted)) g.DrawString(s.PathSub[i] ?? "", fSmall, b, new RectangleF(cx - slot / 2 + 4 * S, r.Y + dot + 41 * S, slot - 8 * S, 18 * S), sf);
                    }
                }
            }
        }

        public static Bitmap DetailCard(Detail d)
        {
            int W = (int)(280 * S); float pad = 14 * S, inner = W - 2 * pad;
            float noteH = string.IsNullOrEmpty(d.Note) ? 0 : Measure(d.Note, fSmall, (int)inner).Height;
            float h = pad + 22 * S + d.Lines.Count * 18 * S + (d.Top.Count > 0 ? 19 * S + d.Top.Count * 20 * S : 0) + (noteH > 0 ? noteH + 6 * S : 0) + pad;
            int H = (int)Math.Ceiling(h);
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var g = Prep(bmp))
            using (var ink = new SolidBrush(Theme.Ink))
            using (var muted = new SolidBrush(Theme.Muted))
            {
                var cr = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, cr, 16 * S, Theme.Card); Theme.Stroke(g, cr, 16 * S, Theme.Line, 1);
                float y = pad;
                g.DrawString(d.Title, fName, ink, pad, y); y += 22 * S;
                foreach (var l in d.Lines)
                {
                    g.DrawString(l[0], fBody, muted, pad, y);
                    string v = Trunc(l[1], 30); var vs = Measure(v, fMono, 0);
                    using (var vb = new SolidBrush(l.Length > 2 ? Theme.LvText(int.Parse(l[2], CultureInfo.InvariantCulture)) : Theme.Ink)) g.DrawString(v, fMono, vb, W - pad - vs.Width, y);
                    y += 18 * S;
                }
                if (d.Top.Count > 0)
                {
                    y += 4 * S; g.DrawString(d.TopTitle, fTiny, muted, pad, y); y += 15 * S;
                    using (var lp = new Pen(Theme.Line2))
                        foreach (var t in d.Top)
                        {
                            g.DrawLine(lp, pad, y, W - pad, y);
                            var vs = Measure(t[1], fMono, 0);
                            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                                g.DrawString(t[0], fBody, ink, new RectangleF(pad, y + 3 * S, inner - vs.Width - 8 * S, 18 * S), sf);
                            g.DrawString(t[1], fMono, ink, W - pad - vs.Width, y + 3 * S);
                            y += 20 * S;
                        }
                }
                if (noteH > 0) { y += 6 * S; g.DrawString(d.Note, fSmall, muted, new RectangleF(pad, y, inner, noteH + 2)); }
            }
            return bmp;
        }
    }
}
