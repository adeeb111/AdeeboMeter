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
        public static readonly Color Blue = C("#1F6FEB"), BlueHot = C("#1A5FCC"), Orange = C("#E36209"), Green = C("#2DA44E"), Amber = C("#BF8700"), Red = C("#CF222E");
        static readonly Color[] lvText = new Color[4], lvSoft = new Color[4];
        static Theme() { Apply(false); }
        public static Color C(string h) { return ColorTranslator.FromHtml(h); }
        public static bool SystemDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                { var v = k == null ? null : k.GetValue("AppsUseLightTheme"); return v is int && (int)v == 0; }
            }
            catch { return false; }
        }
        public static void Apply(bool dark)
        {
            Dark = dark;
            if (!dark)
            {
                Ink = C("#1B2330"); Muted = C("#5B6B7C"); Sub = C("#3D4B5A"); Line = C("#D6DEE8"); Line2 = C("#EEF2F6"); Track = C("#E6ECF3"); Soft = C("#F4F7FB");
                Card = C("#FFFFFF"); Bg = C("#F2F5F9"); Rail = C("#C5CED8"); UpText = C("#9A4A00"); BlueSoft = C("#EEF4FF"); BlueDark = C("#0B4FB3");
                RowNew = C("#E6F7EC"); RowGone = C("#FFEFEF"); RowSel = C("#E8F0FE"); RowHot = C("#F7FAFF"); Grey = C("#AEB8C2");
                lvText[0] = C("#116329"); lvText[1] = C("#7D4E00"); lvText[2] = C("#B42318");
                lvSoft[0] = C("#DAFBE1"); lvSoft[1] = C("#FFF8C5"); lvSoft[2] = C("#FFEBE9");
            }
            else
            {
                Ink = C("#E6EDF3"); Muted = C("#9AA7B4"); Sub = C("#C2CCD6"); Line = C("#2E3A46"); Line2 = C("#222B35"); Track = C("#2A3542"); Soft = C("#1B232D");
                Card = C("#161D26"); Bg = C("#0E1319"); Rail = C("#3A4757"); UpText = C("#F0A060"); BlueSoft = C("#13284A"); BlueDark = C("#9CC2FF");
                RowNew = C("#11301D"); RowGone = C("#3B1B1E"); RowSel = C("#1B2B44"); RowHot = C("#1A222C"); Grey = C("#4A5868");
                lvText[0] = C("#7EE2A8"); lvText[1] = C("#F2CC60"); lvText[2] = C("#FFA198");
                lvSoft[0] = C("#12301C"); lvSoft[1] = C("#3A2F0B"); lvSoft[2] = C("#3A1A1D");
            }
            lvText[3] = Muted; lvSoft[3] = Line2;
            // Windows High Contrast: the user's own colours for text, background and lines (levels keep their hue, text the
            // system's) - so nothing becomes unreadable on a black-on-yellow or white-on-black theme
            if (SystemInformation.HighContrast)
            {
                Ink = Sub = SystemColors.WindowText; Muted = SystemColors.GrayText; Card = Soft = RowHot = SystemColors.Window; Bg = SystemColors.Control;
                Line = Line2 = Rail = Track = SystemColors.WindowFrame; RowSel = SystemColors.Highlight; BlueDark = SystemColors.HotTrack;
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

        public static void Logo(Graphics g, RectangleF r, bool arrows)
        {
            float k = r.Width / 64f;
            Func<float, float> X = delegate(float v) { return r.X + v * k; };
            Func<float, float> Y = delegate(float v) { return r.Y + v * k; };
            Fill(g, new RectangleF(X(2), Y(2), 60 * k, 60 * k), 14 * k, Blue);
            float w = (r.Width < 20 ? 8 : 7) * k;
            using (var p1 = new Pen(C("#3FB950"), w)) g.DrawArc(p1, X(12), Y(20), 40 * k, 40 * k, 180, 60);
            using (var p2 = new Pen(C("#F2CC60"), w)) g.DrawArc(p2, X(12), Y(20), 40 * k, 40 * k, 240, 60);
            using (var p3 = new Pen(C("#FF7B72"), w)) g.DrawArc(p3, X(12), Y(20), 40 * k, 40 * k, 300, 60);
            using (var pn = new Pen(Color.White, (r.Width < 20 ? 6 : 5) * k)) { pn.StartCap = LineCap.Round; pn.EndCap = LineCap.Round; g.DrawLine(pn, X(32), Y(40), X(43), Y(27)); }
            if (r.Width >= 20) using (var wb = new SolidBrush(Color.White)) g.FillEllipse(wb, X(27), Y(35), 10 * k, 10 * k);
            if (arrows && r.Width >= 28)
                using (var pa = new Pen(Color.FromArgb(220, 255, 255, 255), 3 * k))
                {
                    pa.LineJoin = LineJoin.Round; pa.StartCap = pa.EndCap = LineCap.Round;
                    g.DrawLines(pa, new[] { new PointF(X(22), Y(50)), new PointF(X(26), Y(54)), new PointF(X(30), Y(50)) });
                    g.DrawLines(pa, new[] { new PointF(X(34), Y(54)), new PointF(X(38), Y(50)), new PointF(X(42), Y(54)) });
                }
        }
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
            var pts = new PointF[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                double y = double.IsNaN(v[i]) ? 0 : Math.Min(1, v[i] / mx);
                pts[i] = new PointF(r.X + (float)i / (v.Length - 1) * r.Width, r.Bottom - 1 - (float)y * (r.Height - 2));
            }
            if (fill)
            {
                var poly = new List<PointF>(pts); poly.Add(new PointF(r.Right, r.Bottom)); poly.Add(new PointF(r.X, r.Bottom));
                using (var b = new SolidBrush(Color.FromArgb(56, c))) g.FillPolygon(b, poly.ToArray());
            }
            using (var p = new Pen(c, 1.4f)) g.DrawLines(p, pts);
        }
    }

    // All fonts made once; nothing is created per paint (a lesson from topogram).
    static class Draw
    {
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
        public static Font fName, fHead, fBody, fBodyB, fMono, fMonoS, fTiny, fPill, fSmall, fBig, fTitle, fInput;
        static readonly Bitmap measureBmp = new Bitmap(1, 1);
        static Graphics measureG;
        public static Font fHero, fValue;
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
        public static void Init(float s)
        {
            if (fName != null) return;
            S = s;
            // Windows 11 type (Segoe UI Variable, Cascadia Mono) when it's there; Windows 10 falls back to Segoe UI / Consolas.
            string text = Fam("Segoe UI Variable Text", "Segoe UI"), disp = Fam("Segoe UI Variable Display", "Segoe UI"), semi = Fam("Segoe UI Semibold", "Segoe UI"), mono = Fam("Cascadia Mono", "Consolas");
            FontStyle semiStyle = semi == "Segoe UI" ? FontStyle.Bold : FontStyle.Regular;
            // GDI+ can't pick a variable font's real bold - it smears the regular one. Bold text uses Segoe UI's true bold.
            if (disp.IndexOf("Variable", StringComparison.Ordinal) >= 0) disp = "Segoe UI";
            string tb = text.IndexOf("Variable", StringComparison.Ordinal) >= 0 ? "Segoe UI" : text;
            fName = new Font(disp, 12.5f * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fHead = new Font(semi, 13.5f * S, semiStyle, GraphicsUnit.Pixel);
            fBody = new Font(text, 12.5f * S, FontStyle.Regular, GraphicsUnit.Pixel);
            fBodyB = new Font(semi, 12.5f * S, semiStyle, GraphicsUnit.Pixel);
            fSmall = new Font(text, 11 * S, FontStyle.Regular, GraphicsUnit.Pixel);
            fMono = new Font(mono, 11.5f * S, FontStyle.Regular, GraphicsUnit.Pixel);
            fMonoS = new Font(mono, 10.5f * S, FontStyle.Regular, GraphicsUnit.Pixel);
            fTiny = new Font(tb, 9.5f * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fPill = new Font(tb, 10 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fBig = new Font(disp, 20 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fTitle = new Font(disp, 16 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fHero = new Font(disp, 30 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fValue = new Font(disp, 14 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fInput = new Font(mono, 13 * S, FontStyle.Regular, GraphicsUnit.Pixel);
            measureG = Graphics.FromImage(measureBmp); measureG.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }
        // pages measure the same text on every repaint - remember the answers (cleared when it gets big or the scale changes)
        static readonly Dictionary<string, SizeF> measured = new Dictionary<string, SizeF>();
        public static SizeF Measure(string t, Font f, int width)
        {
            t = t ?? ""; string key = f.Name + "|" + f.Size.ToString("R", CultureInfo.InvariantCulture) + "|" + (int)f.Style + "|" + width.ToString(CultureInfo.InvariantCulture) + "|" + t;   // the font's own name, size and style (its hash ignores the family)
            lock (measureBmp)
            {
                SizeF sz; if (measured.TryGetValue(key, out sz)) return sz;
                sz = width > 0 ? measureG.MeasureString(t, f, width) : measureG.MeasureString(t, f);
                if (measured.Count > 3000) measured.Clear();
                measured[key] = sz; return sz;
            }
        }
        // the corner widget can be drawn a little bigger (tray menu > Bigger widget): same layout, scaled when drawn, so text stays sharp
        public static float WidgetK { get { return Cfg.BigWidget ? 1.3f : 1f; } }
        static Graphics PrepW(Bitmap b) { var g = Prep(b); g.ScaleTransform(WidgetK, WidgetK); return g; }
        static Graphics Prep(Bitmap b) { var g = Graphics.FromImage(b); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; g.Clear(Color.Transparent); return g; }
        public static string Fmt(double v, string f, string unit) { return double.IsNaN(v) ? "-" : v.ToString(f, CultureInfo.CurrentCulture) + unit; }
        public static string Trunc(string s, int n)
        {
            if (s == null) return ""; if (s.Length <= n) return s;
            int cut = Math.Max(0, n - 1); if (cut > 0 && char.IsHighSurrogate(s[cut - 1])) cut--;   // never half an emoji
            return s.Substring(0, cut) + "…";
        }
        public static string MemStr(long b) { return b >= 1073741824 ? (b / 1073741824.0).ToString("0.0", CultureInfo.CurrentCulture) + " GB" : (b / 1048576.0).ToString("0", CultureInfo.CurrentCulture) + " MB"; }
        public static string Word(int l) { return l == 2 ? "Problem" : l == 1 ? "Busy" : "OK"; }

        public static Bitmap Chip(Snap s, double dwell)
        {
            var ts = Measure(s.Chip, fSmall, 0);
            int W = (int)(9 * S + 16 * S + 6 * S + 8 * S + 6 * S + ts.Width + 9 * S), H = (int)(26 * S);
            var bmp = new Bitmap((int)Math.Ceiling(W * WidgetK), (int)Math.Ceiling(H * WidgetK), PixelFormat.Format32bppArgb);
            using (var g = PrepW(bmp))
            {
                var r = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, r, H / 2f, Color.FromArgb(246, Theme.Card)); Theme.Stroke(g, r, H / 2f, Theme.Line, 1);
                float x = 9 * S;
                Theme.Logo(g, new RectangleF(x, (H - 16 * S) / 2, 16 * S, 16 * S), false); x += 22 * S;
                using (var b = new SolidBrush(Theme.Lv(s.Level))) g.FillEllipse(b, x, (H - 8 * S) / 2, 8 * S, 8 * S); x += 14 * S;
                using (var b = new SolidBrush(Theme.Ink)) g.DrawString(s.Chip, fSmall, b, x, (H - ts.Height) / 2);
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
            string st = Cfg.WidgetStyle; var gp = GamePing.Me.Playing;
            if (st == "gamer" || (st == "auto" && gp != null && Cfg.GameWidget)) return GamerTarget() != null ? "gamer" : "everyday";
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
            var st = GamePing.Me.Stat(t.Item1.Id, t.Item2); var v = st.Recent(60);
            double now = v.Length > 0 ? v[v.Length - 1] : double.NaN, avg = GameStat.Avg(v), jit = GameStat.Jitter(v), loss = GameStat.Loss(v);
            string grade = v.Length == 0 ? "Waiting" : GameStat.Grade(avg, jit, double.IsNaN(loss) ? 0 : loss);
            Color gc = grade == "Competitive" ? Theme.Green : grade == "Casual" ? Theme.Amber : grade == "Laggy" ? Theme.Red : Theme.Grey;
            Spike last = null; lock (GamePing.Me.Spikes) last = GamePing.Me.Spikes.FirstOrDefault(z => z.Game == t.Item1.Id && (DateTime.Now - z.T).TotalSeconds < 20);
            string head = last != null ? "Spike " + Math.Round(last.Ms) + " ms: " + last.Blame + "." : t.Item1.Name + " · " + t.Item2.Name + (grade == "Competitive" ? " is steady." : grade == "Casual" ? " is playable." : grade == "Laggy" ? " is laggy." : "...");
            int W = (int)(264 * S); float pad = 12 * S, inner = W - 2 * pad;
            float headH = Measure(head, fHead, (int)inner).Height;
            int H = (int)Math.Ceiling(pad + 24 * S + headH + 8 * S + 46 * S + 30 * S + 20 * S + 14 * S + 30 * S + pad);
            var bmp = new Bitmap((int)Math.Ceiling(W * WidgetK), (int)Math.Ceiling(H * WidgetK), PixelFormat.Format32bppArgb);
            using (var g = PrepW(bmp)) using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                var cr = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, cr, 12 * S, Color.FromArgb(250, Theme.Card)); Theme.Stroke(g, cr, 12 * S, last != null ? Theme.Amber : Theme.Line, last != null ? 1.5f : 1f);
                float y = pad;
                Theme.Logo(g, new RectangleF(pad, y, 18 * S, 18 * S), false);
                g.DrawString(L.T("AdeeboMeter"), fName, ink, pad + 24 * S, y + 1 * S);
                float nw = Measure(L.T("AdeeboMeter"), fName, 0).Width;
                var tag = new RectangleF(pad + 24 * S + nw + 4 * S, y + 2 * S, Measure("GAME", fTiny, 0).Width + 10 * S, 15 * S);
                Theme.Fill(g, tag, 5 * S, Theme.BlueSoft); using (var tb = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.BlueDark)) g.DrawString(L.T("GAME"), fTiny, tb, tag.X + 5 * S, tag.Y + 1 * S);
                y += 24 * S;
                using (var hb = new SolidBrush(last != null ? Theme.LvText(1) : Theme.Ink)) g.DrawString(head, fHead, hb, new RectangleF(pad, y, inner, headH + 2)); y += headH + 8 * S;
                hits["game"] = new RectangleF(pad - 4 * S, y - 4 * S, inner + 8 * S, 80 * S);
                string big = double.IsNaN(now) ? "-" : Math.Round(now).ToString(CultureInfo.InvariantCulture);
                using (var bf = new Font(fHead.FontFamily, 26 * S, FontStyle.Bold, GraphicsUnit.Pixel)) using (var bb = new SolidBrush(gc))
                {
                    g.DrawString(big, bf, bb, pad - 2 * S, y); float bw = g.MeasureString(big, bf).Width;
                    g.DrawString(L.T("ms"), fBody, muted, pad + bw - 2 * S, y + 16 * S);
                }
                var gp = new RectangleF(W - pad - Measure(grade.ToUpperInvariant(), fTiny, 0).Width - 12 * S, y + 12 * S, Measure(grade.ToUpperInvariant(), fTiny, 0).Width + 12 * S, 17 * S);
                Theme.Fill(g, gp, 6 * S, Color.FromArgb(Theme.Dark ? 60 : 40, gc)); using (var gb = new SolidBrush(Theme.Dark ? Theme.Ink : gc)) g.DrawString(grade.ToUpperInvariant(), fTiny, gb, gp.X + 6 * S, gp.Y + 2 * S);
                y += 46 * S;
                var sr = new RectangleF(pad, y, inner, 26 * S); Theme.Fill(g, sr, 5 * S, Theme.Soft);
                double cap = 30; foreach (var x in v) if (!double.IsNaN(x) && x > cap) cap = x;
                Theme.Spark(g, v, sr, gc, cap * 1.15, false);
                y += 30 * S;
                string row = "jitter " + Fmt(jit, "0.0", " ms") + "   loss " + Fmt(loss, "0.#", "%") + "   ↓" + Fmt(s.Down, "0.0", "") + " ↑" + Fmt(s.Up, "0.0", " Mb/s");
                g.DrawString(row, fSmall, muted, pad, y); y += 20 * S;
                hits["path"] = new RectangleF(pad - 4 * S, y - 4 * S, inner + 8 * S, 30 * S);
                g.DrawString(t.Item2.Where + " · " + (t.Item2.Kind == "estimate" ? "region estimate" : t.Item2.Kind == "valve" ? "Valve server" : "exact"), fTiny, muted, pad, y); y += 14 * S;
                PathStrip(g, s, new RectangleF(pad, y, inner, 12 * S), false);
            }
            if (WidgetK != 1f) foreach (var k in hits.Keys.ToList()) { var r = hits[k]; hits[k] = new RectangleF(r.X * WidgetK, r.Y * WidgetK, r.Width * WidgetK, r.Height * WidgetK); }
            return bmp;
        }
        // Calls: what a call cares about - steadiness (jitter), loss and the delay; the VPN as its own line when one is up
        public static Bitmap CallsCard(Snap s, Dictionary<string, RectangleF> hits)
        {
            double jit = s.Jitter, loss = s.LossPct, ping = s.Ping;
            int lv = double.IsNaN(ping) ? 3 : (jit > 30 || loss > 3 || ping > 250) ? 2 : (jit > 15 || loss > 1 || ping > 150) ? 1 : 0;
            string grade = lv == 0 ? "CLEAR" : lv == 1 ? "SOME BREAKUP" : lv == 2 ? "CHOPPY" : "WAITING";
            string head = s.Call ? (lv == 0 ? "Your call should sound clear." : lv == 1 ? "Your call may break up now and then." : lv == 2 ? "Your call will sound choppy." : "Measuring your call...") : "No call right now - this is how one would sound.";
            string vpnLine = s.PathLabel != null && s.PathLabel.Length > 2 && s.PathLabel[2] == "VPN" ? "through the VPN · " + (s.PathSub != null && s.PathSub.Length > 2 ? s.PathSub[2] : "") : "";
            return MetricCard(s, hits, "CALL", head, lv, double.IsNaN(jit) ? "-" : Math.Round(jit).ToString(CultureInfo.InvariantCulture), "ms jitter", grade,
                s.NetHist, new[] { "delay " + Fmt(ping, "0", " ms") + "   loss " + Fmt(loss, "0.#", "%") + "   ↑" + Fmt(s.Up, "0.0", " Mb/s"), vpnLine });
        }
        // Engineer: the raw numbers - router and internet delay, DNS, TCP retransmits / resets, throughput
        public static Bitmap EngineerCard(Snap s, Dictionary<string, RectangleF> hits)
        {
            string h1 = s.Health != null && s.Health.Count > 0 ? s.Health[0][0] + " " + s.Health[0][1] : "", h2 = s.Health != null && s.Health.Count > 1 ? s.Health[1][0] + " " + s.Health[1][1] : "";
            double gw = s.GwMs;
            return MetricCard(s, hits, "ENG", s.Headline, s.Level, Fmt(s.Ping, "0", ""), "ms internet", s.Level == 0 ? "OK" : s.Level == 1 ? "SLOW" : s.Level == 2 ? "PROBLEM" : "WAITING",
                s.NetHist, new[] { "router " + Fmt(gw, "0", " ms") + "   DNS " + Fmt(s.DnsMs, "0", " ms") + "   jitter " + Fmt(s.Jitter, "0.0", " ms") + "   loss " + Fmt(s.LossPct, "0.#", "%"), (h1 + (h2 != "" ? "   " + h2 : "")).Trim(), "↓" + Fmt(s.Down, "0.0", "") + " ↑" + Fmt(s.Up, "0.0", " Mb/s") + "   CPU " + Fmt(s.Cpu, "0", "%") + "   RAM " + Fmt(s.Mem, "0", "%") });
        }
        // the shared shape: tag, headline, one big number with its level colour and a grade pill, a 60-sample sparkline,
        // a few small rows, then the five-dot path strip
        static Bitmap MetricCard(Snap s, Dictionary<string, RectangleF> hits, string tagText, string head, int lv, string big, string unit, string grade, double[] series, string[] rows)
        {
            hits.Clear();
            var lines = rows.Where(r => !string.IsNullOrEmpty(r)).ToArray();
            Color gc = lv == 0 ? Theme.Green : lv == 1 ? Theme.Amber : lv == 2 ? Theme.Red : Theme.Grey;
            int W = (int)(264 * S); float pad = 12 * S, inner = W - 2 * pad;
            float headH = Measure(head, fHead, (int)inner).Height;
            int H = (int)Math.Ceiling(pad + 24 * S + headH + 8 * S + 46 * S + 30 * S + lines.Length * 18 * S + 4 * S + 30 * S + pad);
            var bmp = new Bitmap((int)Math.Ceiling(W * WidgetK), (int)Math.Ceiling(H * WidgetK), PixelFormat.Format32bppArgb);
            using (var g = PrepW(bmp)) using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                var cr = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, cr, 12 * S, Color.FromArgb(250, Theme.Card)); Theme.Stroke(g, cr, 12 * S, lv == 1 || lv == 2 ? gc : Theme.Line, lv == 1 || lv == 2 ? 1.5f : 1f);
                float y = pad;
                Theme.Logo(g, new RectangleF(pad, y, 18 * S, 18 * S), false);
                g.DrawString(L.T("AdeeboMeter"), fName, ink, pad + 24 * S, y + 1 * S);
                float nw = Measure(L.T("AdeeboMeter"), fName, 0).Width;
                var tag = new RectangleF(pad + 24 * S + nw + 4 * S, y + 2 * S, Measure(tagText, fTiny, 0).Width + 10 * S, 15 * S);
                Theme.Fill(g, tag, 5 * S, Theme.BlueSoft); using (var tb = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.BlueDark)) g.DrawString(tagText, fTiny, tb, tag.X + 5 * S, tag.Y + 1 * S);
                y += 24 * S;
                g.DrawString(head, fHead, ink, new RectangleF(pad, y, inner, headH + 2)); y += headH + 8 * S;
                hits["main"] = new RectangleF(pad - 4 * S, y - 4 * S, inner + 8 * S, 80 * S);
                using (var bf = new Font(fHead.FontFamily, 26 * S, FontStyle.Bold, GraphicsUnit.Pixel)) using (var bb = new SolidBrush(lv == 3 ? Theme.Ink : gc))
                {
                    g.DrawString(big, bf, bb, pad - 2 * S, y); float bw = g.MeasureString(big, bf).Width;
                    g.DrawString(unit, fBody, muted, pad + bw - 2 * S, y + 16 * S);
                }
                float pw = Measure(grade, fTiny, 0).Width + 12 * S; var gp = new RectangleF(W - pad - pw, y + 12 * S, pw, 17 * S);
                Theme.Fill(g, gp, 6 * S, Color.FromArgb(Theme.Dark ? 60 : 40, gc)); using (var gb = new SolidBrush(Theme.Dark ? Theme.Ink : gc)) g.DrawString(grade, fTiny, gb, gp.X + 6 * S, gp.Y + 2 * S);
                y += 46 * S;
                var sr = new RectangleF(pad, y, inner, 26 * S); Theme.Fill(g, sr, 5 * S, Theme.Soft);
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
        public static Bitmap Card(Snap s, Dictionary<string, RectangleF> hits)
        {
            string lay = Layout(s.Call);
            if (lay == "gamer") return GamerCard(s, hits);
            if (lay == "calls") return CallsCard(s, hits);
            if (lay == "engineer") return EngineerCard(s, hits);
            hits.Clear();
            int W = (int)(264 * S); float pad = 12 * S, inner = W - 2 * pad;
            float headH = Measure(s.Headline, fHead, (int)inner).Height;
            float hintH = string.IsNullOrEmpty(s.Hint) ? 0 : Measure("Why: " + s.Hint, fSmall, (int)inner).Height + 4 * S;
            float h = pad + 24 * S + headH + hintH + 10 * S + 52 * S + (s.Call ? 44 * S : 0) + 3 * 20 * S + 6 * S + 17 * S + 34 * S + pad;
            int H = (int)Math.Ceiling(h);
            var bmp = new Bitmap((int)Math.Ceiling(W * WidgetK), (int)Math.Ceiling(H * WidgetK), PixelFormat.Format32bppArgb);
            using (var g = PrepW(bmp))
            using (var ink = new SolidBrush(Theme.Ink))
            using (var muted = new SolidBrush(Theme.Muted))
            {
                var cr = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, cr, 12 * S, Color.FromArgb(250, Theme.Card));
                Theme.Stroke(g, cr, 12 * S, s.Level == 2 ? Theme.Red : Theme.Line, s.Level == 2 ? 1.5f : 1f);
                float y = pad;
                Theme.Logo(g, new RectangleF(pad, y, 18 * S, 18 * S), false);
                g.DrawString(L.T("AdeeboMeter"), fName, ink, pad + 24 * S, y + 1 * S);
                float nw = Measure(L.T("AdeeboMeter"), fName, 0).Width;
                Theme.Pill(g, Word(s.Level), fPill, pad + 24 * S + nw + 2 * S, y + 2 * S, s.Level, S);
                if (CardHovered) { string cd = s.Countdown + "s"; var cs = Measure(cd, fSmall, 0); g.DrawString(cd, fSmall, muted, W - pad - cs.Width, y + 2 * S); }   // the countdown only while you look: unhovered, the card isn't redrawn each second
                y += 24 * S;
                using (var hb = new SolidBrush(s.Level == 0 ? Theme.Ink : Theme.LvText(s.Level))) g.DrawString(s.Headline, fHead, hb, new RectangleF(pad, y, inner, headH + 2));
                y += headH;
                if (hintH > 0) { using (var wb = new SolidBrush(Theme.Sub)) g.DrawString("Why: " + s.Hint, fSmall, wb, new RectangleF(pad, y + 4 * S, inner, hintH)); y += hintH; }
                y += 10 * S;
                hits["path"] = new RectangleF(pad - 4 * S, y - 4 * S, inner + 8 * S, 52 * S);
                g.DrawString(L.T("IS IT ME OR THE NETWORK?"), fTiny, muted, pad, y);
                y += 15 * S;
                PathStrip(g, s, new RectangleF(pad, y, inner, 32 * S), false);
                y += 37 * S;
                if (s.Call)
                {
                    bool bad = s.Jitter > 30 || s.LossPct > 2;
                    var r = new RectangleF(pad, y, inner, 38 * S); hits["call"] = r;
                    Theme.Fill(g, r, 7 * S, bad ? Theme.LvSoft(1) : Theme.BlueSoft);
                    using (var b = new SolidBrush(bad ? Theme.LvText(1) : Theme.BlueDark))
                    {
                        g.DrawString(L.T("MEETING MODE · TEAMS CALL"), fTiny, b, r.X + 8 * S, r.Y + 4 * S);
                        g.DrawString("up " + Fmt(s.Up, "0.0", " Mb/s") + "   jitter " + Fmt(s.Jitter, "0", " ms") + "   loss " + Fmt(s.LossPct, "0", "%"), fSmall, b, r.X + 8 * S, r.Y + 19 * S);
                    }
                    y += 44 * S;
                }
                Bar(g, "cpu", "CPU", s.Cpu, Fmt(s.Cpu, "0", "%"), 60, 88, pad, ref y, inner, hits);
                Bar(g, "ram", "Memory", s.Mem, Fmt(s.Mem, "0", "%"), 80, 90, pad, ref y, inner, hits);
                Bar(g, "disk", "Disk", s.Disk, Fmt(s.Disk, "0", "%"), 60, 90, pad, ref y, inner, hits);
                y += 6 * S;
                hits["net"] = new RectangleF(pad - 4 * S, y - 3 * S, inner + 8 * S, 56 * S);
                using (var b = new SolidBrush(Theme.Blue)) g.DrawString("↓ " + Fmt(s.Down, "0.0", " Mb/s"), fMono, b, pad, y);
                string up = "↑ " + Fmt(s.Up, "0.0", " Mb/s"); var us = Measure(up, fMono, 0);
                using (var b = new SolidBrush(Theme.UpText)) g.DrawString(up, fMono, b, W - pad - us.Width, y);
                y += 17 * S;
                var gr = new RectangleF(pad, y, inner, 34 * S);
                Theme.Fill(g, gr, 5 * S, Theme.Soft);
                double cap = 1; foreach (var v in s.DownHist) if (!double.IsNaN(v) && v > cap) cap = v; foreach (var v in s.UpHist) if (!double.IsNaN(v) && v > cap) cap = v;
                Theme.Spark(g, s.DownHist, gr, Theme.Blue, cap * 1.15, true);
                Theme.Spark(g, s.UpHist, gr, Theme.Orange, cap * 1.15, false);
                // data used today, in the graph's top corners (no extra height); this month against a cap at the bottom right
                var du = Usage.TodayDU();
                using (var tb = new SolidBrush(Theme.Muted))
                {
                    g.DrawString("today ↓ " + Usage.Mb(du[0]), fTiny, tb, gr.X + 4 * S, gr.Y + 2 * S);
                    string ut = "↑ " + Usage.Mb(du[1]); g.DrawString(ut, fTiny, tb, gr.Right - 4 * S - Measure(ut, fTiny, 0).Width, gr.Y + 2 * S);
                    if (Cfg.CapGB > 0) { string ct = (Usage.Month(null) / 1024).ToString("0", CultureInfo.CurrentCulture) + " / " + Cfg.CapGB + " GB"; g.DrawString(ct, fTiny, tb, gr.Right - 4 * S - Measure(ct, fTiny, 0).Width, gr.Bottom - 2 * S - Measure(ct, fTiny, 0).Height); }
                }
            }
            if (WidgetK != 1f) foreach (var k in hits.Keys.ToList()) { var r = hits[k]; hits[k] = new RectangleF(r.X * WidgetK, r.Y * WidgetK, r.Width * WidgetK, r.Height * WidgetK); }
            return bmp;
        }

        public static void Bar(Graphics g, string key, string label, double v, string txt, double yl, double rl, float x, ref float y, float inner, Dictionary<string, RectangleF> hits)
        {
            if (hits != null) hits[key] = new RectangleF(x - 4 * S, y - 2 * S, inner + 8 * S, 19 * S);
            int lv = double.IsNaN(v) ? 3 : v >= rl ? 2 : v >= yl ? 1 : 0;
            using (var b = new SolidBrush(Theme.Muted)) g.DrawString(label, fBody, b, x, y);
            var ts = Measure(txt, fMono, 0);
            float tw = Math.Max(44 * S, ts.Width + 6 * S);
            var tr = new RectangleF(x + 66 * S, y + 6 * S, Math.Max(10, inner - 66 * S - tw), 7 * S);
            Theme.Fill(g, tr, 3.5f * S, Theme.Track);
            if (!double.IsNaN(v)) Theme.Fill(g, new RectangleF(tr.X, tr.Y, Math.Max(7 * S, tr.Width * (float)Math.Min(100, v) / 100f), tr.Height), 3.5f * S, Theme.Lv(lv));
            using (var b = new SolidBrush(lv == 1 || lv == 2 ? Theme.Lv(lv) : Theme.Ink)) g.DrawString(txt, fMono, b, x + inner - ts.Width, y);
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
                using (var halo = new SolidBrush(Color.FromArgb(lv == 2 ? 70 : 38, Theme.Lv(lv)))) g.FillEllipse(halo, cx - dot / 2 - 5 * S, r.Y - 5 * S, dot + 10 * S, dot + 10 * S);
                Draw.LvDot(g, lv, cx - dot / 2, r.Y, dot, dot);
                if (big) using (var hi = new SolidBrush(Color.FromArgb(90, Color.White))) g.FillEllipse(hi, cx - dot / 4, r.Y + dot / 5, dot / 2, dot / 3);
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
                Theme.Fill(g, cr, 12 * S, Theme.Card); Theme.Stroke(g, cr, 12 * S, Theme.Line, 1);
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
