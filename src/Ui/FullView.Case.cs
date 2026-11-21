// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== the front door, the side doors, the case and its report
    // Front door: the search box ("Search or describe a problem") and Home's "What's going on?" chips. Side doors: an
    // "Investigate this ›" link wherever something went wrong (Your week, a stream, a broker day, a failed tool). All of
    // them open the same case page - the guided investigation with the case's time, where it came from and what was
    // recorded then - and one report you can keep or send: a PDF (Windows' own Microsoft Print to PDF) or, where IT took
    // that printer away, one HTML file any browser opens. Made on this PC; nothing is sent.
    sealed partial class FullView
    {
        Cases.Case guideCase; DateTime reportAt = DateTime.MinValue; string reportFile = "";

        // ---------- opening a case (every door ends here)
        internal void OpenCase(string symptom, string source, string context, DateTime? from, DateTime? to)
        {
            if (guideBusy) { guideNote = L.T("A check is already running - its report opens here when it's done."); tab = "guide"; ShowTab(); SyncNav(); if (guideCanvas != null) guideCanvas.Invalidate(); return; }   // bug-hunt L-327
            var c = Cases.New(symptom, source, context, from, to);
            tab = "guide"; ShowTab(); SyncNav();
            if (c.Flow == "site" && context != "" && guideBox != null) guideBox.Box.Text = context;
            guideCase = c; reportFile = "";
            StartGuide(c.Flow, false); c.Run = guideRun;
            if (context != "" && c.Flow != "site" && guideRun != null) guideRun.Obs("m", 1, L.T("What AdeeboMeter recorded"), context);   // the side door's own facts come first
            guideCanvas.Invalidate();
        }
        bool CaseShowing { get { return guideCase != null && guideRun != null && guideCase.Run == guideRun; } }

        // ---------- a side door: "Investigate this ›" - returns the right edge, so callers can line things up after it
        float InvLink(Graphics g, Control on, float x, float y, string symptom, string source, string context, DateTime? from, DateTime? to)
        {
            string t = L.T("Investigate this ›"); var ts = g.MeasureString(t, Draw.fBodyB); var r = new RectangleF(x, y, ts.Width, 22 * S);
            using (var b = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(t, Draw.fBodyB, b, x, y);
            Act(on, r, delegate { OpenCase(symptom, source, context, from, to); });
            return r.Right + 20 * S;
        }

        // ---------- the front door on Home: "What's going on?" - one tap per common problem, plus your recent cases
        static readonly string[] HomeChips = { "calls", "game", "buffer", "slow", "drops", "site", "pc" };
        float PaintFrontDoor(Graphics g, float x, float y, float w)
        {
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("WHAT'S GOING ON?  ·  TAP IT, OR TYPE IT IN THE SEARCH BOX (CTRL+K)"), Draw.fTiny, mb, x, y);
            y += 22 * S; float cx = x;
            foreach (var id in HomeChips)
            {
                string t = L.T(Cases.Of(id)[1]); var ts = g.MeasureString(t, Draw.fBody); var r = new RectangleF(cx, y, ts.Width + 26 * S, 32 * S);
                if (r.Right > x + w && cx > x) { cx = x; y += 40 * S; r = new RectangleF(cx, y, r.Width, r.Height); }
                Theme.Fill(g, r, 16 * S, Kit.Soft("home")); using (var tb = new SolidBrush(Theme.Ink)) g.DrawString(t, Draw.fBody, tb, r.X + 13 * S, r.Y + (32 * S - ts.Height) / 2);
                string sid = id; Act(overviewCanvas, r, delegate { OpenCase(sid, "home", "", null, null); });
                cx = r.Right + 8 * S;
            }
            y += 44 * S;
            var rec = Cases.Recent().Where(c => c.Run != null && c.Run.Done).Take(3).ToList();
            if (rec.Count > 0)
            {
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("YOUR RECENT CASES"), Draw.fTiny, mb, x, y); y += 20 * S;
                foreach (var c in rec)
                {
                    string t = c.Opened.ToString("HH:mm", CultureInfo.CurrentCulture) + "  ·  " + c.Title + "  ·  " + Draw.Trunc(c.Run.Finding, 90);
                    using (var tb = new SolidBrush(c.Run.Lv >= 1 && c.Run.Lv <= 2 ? Theme.LvText(c.Run.Lv) : Theme.Sub)) g.DrawString(t, Draw.fSmall, tb, new RectangleF(x, y, w, 18 * S), GameNoWrap);
                    var cc = c; Act(overviewCanvas, new RectangleF(x, y, w, 18 * S), delegate { guideCase = cc; guideRun = cc.Run; tab = "guide"; ShowTab(); SyncNav(); guideCanvas.Invalidate(); });
                    y += 20 * S;
                }
                y += 8 * S;
            }
            return y + 6 * S;
        }

        // ---------- the case header on the investigation page: what, when, from where, how sure, and the minutes around it
        float PaintCaseHead(Graphics g, float x, float y, float w)
        {
            var c = guideCase; var r = guideRun; int sure = Cases.Sure(r);
            var card = new RectangleF(x, y, w, 132 * S); Kit.Card(g, card, "home", false);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("CASE") + "  ·  " + L.T("from") + " " + L.T(Cases.SourceName(c.Source)) + "  ·  " + c.From.ToString("ddd HH:mm", CultureInfo.CurrentCulture) + " - " + c.To.ToString("HH:mm", CultureInfo.CurrentCulture), Draw.fTiny, mb, x + 18 * S, y + 12 * S);
            // bug-hunt L-329: the title stops before the "how sure" dots, with "…" if it's longer
            float titleRoom = w - 36 * S - (sure > 0 ? g.MeasureString(L.T(Cases.SureWords(sure)), Draw.fSmall).Width + 4 * 14 * S + 12 * S : 0);
            using (var ib = new SolidBrush(Theme.Ink)) using (var tf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter }) g.DrawString(c.Title, Draw.fHead, ib, new RectangleF(x + 18 * S, y + 30 * S, Math.Max(60 * S, titleRoom), 26 * S), tf);
            if (sure > 0)
            {
                string sw = L.T(Cases.SureWords(sure)); var ss = g.MeasureString(sw, Draw.fSmall); float dx = x + w - ss.Width - 18 * S - 4 * 14 * S;
                for (int i = 0; i < 4; i++) Theme.Fill(g, new RectangleF(dx + i * 14 * S, y + 36 * S, 10 * S, 10 * S), 5 * S, i < sure ? Kit.Accent("home") : Theme.Track);
                using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(sw, Draw.fSmall, sb, x + w - ss.Width - 14 * S, y + 32 * S);
            }
            var ch = new RectangleF(x + 18 * S, y + 62 * S, w - 36 * S, 58 * S);
            CaseChart(g, ch, CaseSeries(c), false);
            return card.Bottom + 16 * S;
        }
        Cases.Series caseSeries; DateTime caseSeriesAt; Cases.Case caseSeriesFor;
        Cases.Series CaseSeries(Cases.Case c)
        {
            if (caseSeriesFor != c || (DateTime.UtcNow - caseSeriesAt).TotalSeconds > 60) { caseSeries = Cases.Minutes(c.From, c.To); caseSeriesFor = c; caseSeriesAt = DateTime.UtcNow; }   // read once a minute at most - never on every paint
            return caseSeries;
        }
        // router and internet delay (lines) and lost replies (red ticks), one point a minute; Wi-Fi signal dashed when known
        static void CaseChart(Graphics g, RectangleF r, Cases.Series s, bool print)
        {
            Color ink = print ? Color.FromArgb(87, 96, 106) : Theme.Muted, bg = print ? Color.FromArgb(246, 248, 250) : Theme.Soft;
            Font f = print ? null : Draw.fTiny;
            using (var bb = new SolidBrush(bg)) g.FillRectangle(bb, r);
            if (s == null || s.T.Count < 2)
            {
                string t = Cfg.History ? "No minutes recorded for this time yet - the case uses what it measures now." : "History is off (Settings > History & data), so there are no past minutes to draw.";
                using (var tb = new SolidBrush(ink)) using (var ff = f == null ? RFont(12, FontStyle.Regular) : null) g.DrawString(L.T(t), f ?? ff, tb, new RectangleF(r.X + 8, r.Y + 8, r.Width - 16, r.Height - 16));
                return;
            }
            double max = Math.Max(20, new[] { s.Gw, s.Net }.SelectMany(z => z).Where(v => !double.IsNaN(v)).DefaultIfEmpty(20).Max() * 1.15);
            float x0 = r.X + 4, w = r.Width - 8, top = r.Y + 4, h = r.Height - 8; int n = s.T.Count;
            Func<int, float> X = i => x0 + w * i / Math.Max(1, n - 1);
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            Action<List<double>, Color, double, bool> line = (vals, col, scale, dash) =>
            {
                var pts = new List<PointF>();
                using (var p = new Pen(col, print ? 2f : 1.8f * S) { DashStyle = dash ? DashStyle.Dash : DashStyle.Solid })
                    for (int i = 0; i <= n; i++)
                    {
                        if (i < n && !double.IsNaN(vals[i])) { pts.Add(new PointF(X(i), top + h - (float)(Math.Min(vals[i], scale) / scale) * h)); continue; }
                        if (pts.Count > 1) g.DrawLines(p, pts.ToArray()); pts.Clear();
                    }
            };
            line(s.Wifi, Color.FromArgb(150, 130, 80, 223), 100, true);
            line(s.Gw, Theme.Green, max, false);
            line(s.Net, Theme.Blue, max, false);
            using (var rb = new SolidBrush(Theme.Red)) for (int i = 0; i < n; i++) if (!double.IsNaN(s.Loss[i]) && s.Loss[i] > 0) g.FillRectangle(rb, X(i) - 1.5f, top + h - 6, 3, 6);
            g.SmoothingMode = sm;
            using (var tb = new SolidBrush(ink)) using (var ff = f == null ? RFont(11, FontStyle.Regular) : null)
            {
                var fo = f ?? ff;
                // the key: a short swatch of each line, drawn - not font glyphs, which some fonts lack
                float lx = r.X + 6, ly = r.Y + 3, lh = g.MeasureString("Ag", fo).Height;
                foreach (var k in new[] { new object[] { L.T("router"), Theme.Green, 0 }, new object[] { L.T("internet"), Theme.Blue, 0 }, new object[] { L.T("Wi-Fi %"), Color.FromArgb(150, 130, 80, 223), 1 }, new object[] { L.T("lost"), Theme.Red, 2 } })
                {
                    using (var p = new Pen((Color)k[1], 2) { DashStyle = (int)k[2] == 1 ? DashStyle.Dash : DashStyle.Solid })
                        if ((int)k[2] == 2) using (var kb = new SolidBrush((Color)k[1])) g.FillRectangle(kb, lx + 6, ly + lh / 2 - 4, 3, 8); else g.DrawLine(p, lx, ly + lh / 2, lx + 16, ly + lh / 2);
                    lx += 20; g.DrawString((string)k[0], fo, tb, lx, ly); lx += g.MeasureString((string)k[0], fo).Width + 12;
                }
                g.DrawString(L.T("up to") + " " + max.ToString("0", CultureInfo.CurrentCulture) + " ms", fo, tb, lx + 6, ly);
                g.DrawString(s.T[0].ToString("HH:mm", CultureInfo.CurrentCulture), fo, tb, r.X + 4, r.Bottom - 16);
                var es = g.MeasureString(s.T[n - 1].ToString("HH:mm", CultureInfo.CurrentCulture), fo); g.DrawString(s.T[n - 1].ToString("HH:mm", CultureInfo.CurrentCulture), fo, tb, r.Right - es.Width - 4, r.Bottom - 16);
            }
        }

        // ================================================================== the report
        sealed class RBlock { public float H; public Action<Graphics, float> Draw; }
        const float PW = 800, PH = 1130, PTop = 50, PBottom = 1080;
        static readonly Color RBlue = Color.FromArgb(31, 111, 235), RInk = Color.FromArgb(31, 35, 40), RSub = Color.FromArgb(87, 96, 106), RLine = Color.FromArgb(208, 215, 222);
        static Color RSoft(int lv) { return lv == 2 ? Color.FromArgb(255, 235, 233) : lv == 1 ? Color.FromArgb(255, 248, 197) : lv == 0 ? Color.FromArgb(218, 251, 225) : Color.FromArgb(240, 242, 245); }
        static Color RText(int lv) { return lv == 2 ? Color.FromArgb(207, 34, 46) : lv == 1 ? Color.FromArgb(154, 103, 0) : lv == 0 ? Color.FromArgb(26, 127, 55) : RSub; }
        // the report as blocks of known height, so pages break between blocks, never through one
        List<RBlock> ReportBlocks(Cases.Case c, Graphics mg, Dictionary<string, Font> F)
        {
            var r = c.Run; string ssid; Snap snap = M.Latest; lock (M.L) ssid = M.Ssid;
            Func<string, string> clean = t => GameSession.Redact(t ?? "", ssid);
            var b = new List<RBlock>(); float W = PW - 100;
            Func<string, Font, float, float> hgt = (t, f, wd) => mg.MeasureString(t, f, (int)wd).Height;
            // the band
            b.Add(new RBlock { H = 86, Draw = (g, y) =>
            {
                using (var bb = new SolidBrush(RBlue)) g.FillRectangle(bb, 0, y - PTop, PW, 110);
                using (var wb = new SolidBrush(Color.White)) { g.DrawString(L.T("AdeeboMeter diagnosis report"), F["title"], wb, 50, y - PTop + 22); g.DrawString(DateTime.Now.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.CurrentCulture) + "  ·  " + L.T("made on this PC - nothing was sent"), F["small"], wb, 52, y - PTop + 70); }
            } });
            // the case
            string when = c.From.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture) + " - " + c.To.ToString("HH:mm", CultureInfo.CurrentCulture);
            b.Add(new RBlock { H = 64, Draw = (g, y) => { using (var ib = new SolidBrush(RInk)) g.DrawString(c.Title, F["head"], ib, 50, y); using (var sb = new SolidBrush(RSub)) g.DrawString(L.T("When") + ": " + when + "   ·   " + L.T("Started from") + ": " + L.T(Cases.SourceName(c.Source)), F["body"], sb, 50, y + 32); } });
            // the finding, and how sure
            string fin = clean(r.Finding); int sure = Cases.Sure(r); float fh = hgt(fin, F["fin"], W - 40);
            b.Add(new RBlock { H = fh + 74, Draw = (g, y) =>
            {
                var box = new RectangleF(50, y, W, fh + 60); using (var fb = new SolidBrush(RSoft(r.Lv))) g.FillRectangle(fb, box);
                using (var lb = new SolidBrush(RText(r.Lv))) g.FillRectangle(lb, box.X, box.Y, 6, box.Height);
                using (var sb = new SolidBrush(RSub)) g.DrawString(L.T("FINDING") + "   ·   " + L.T(Cases.SureWords(sure)).ToUpperInvariant(), F["tiny"], sb, 70, y + 12);
                for (int i = 0; i < 4; i++) using (var db = new SolidBrush(i < sure ? RBlue : RLine)) g.FillEllipse(db, box.Right - 80 + i * 16, y + 14, 10, 10);
                using (var tb = new SolidBrush(r.Lv == 0 ? RInk : RText(r.Lv))) g.DrawString(fin, F["fin"], tb, new RectangleF(70, y + 34, W - 40, fh + 6));
            } });
            // the path, as it is now
            b.Add(new RBlock { H = 92, Draw = (g, y) =>
            {
                using (var sb = new SolidBrush(RSub)) g.DrawString(L.T("THE PATH, AS IT IS NOW"), F["tiny"], sb, 50, y);
                float slot = W / 5;
                for (int i = 0; i < 5; i++)
                {
                    float cx = 50 + slot * i + slot / 2; int lv = snap.Path != null && i < snap.Path.Length ? snap.Path[i] : 3;
                    if (i < 4) using (var p = new Pen(RLine, 3)) g.DrawLine(p, cx + 12, y + 40, cx + slot - 12, y + 40);
                    using (var db = new SolidBrush(lv == 2 ? Theme.Red : lv == 1 ? Theme.Amber : lv == 0 ? Theme.Green : RLine)) g.FillEllipse(db, cx - 10, y + 30, 20, 20);
                    string lab = snap.PathLabel != null && i < snap.PathLabel.Length ? snap.PathLabel[i] ?? "" : ""; var ls = g.MeasureString(lab, F["small"]);
                    using (var tb = new SolidBrush(RInk)) g.DrawString(lab, F["small"], tb, cx - ls.Width / 2, y + 56);
                }
            } });
            // the minutes around it
            var series = Cases.Minutes(c.From, c.To);
            b.Add(new RBlock { H = 170, Draw = (g, y) => { using (var sb = new SolidBrush(RSub)) g.DrawString(L.T("THE MINUTES AROUND IT"), F["tiny"], sb, 50, y); CaseChart(g, new RectangleF(50, y + 20, W, 130), series, true); } });
            // lists: what was ruled out, what isn't proven, what to try
            var obs = r.ObservedCopy();
            Action<string, IEnumerable<string>, Color> list = (head, items, col) =>
            {
                var its = items.Select(clean).Where(z => z != "").ToList(); if (its.Count == 0) return;
                b.Add(new RBlock { H = 30, Draw = (g, y) => { using (var sb = new SolidBrush(RSub)) g.DrawString(head, F["tiny"], sb, 50, y + 8); } });
                foreach (var it in its) { string t = it; float h = hgt(t, F["body"], W - 24) + 6; b.Add(new RBlock { H = h, Draw = (g, y) => { using (var db = new SolidBrush(col)) g.FillEllipse(db, 52, y + 6, 7, 7); using (var tb = new SolidBrush(RInk)) g.DrawString(t, F["body"], tb, new RectangleF(68, y, W - 24, h)); } }); }
            };
            list(L.T("RULED OUT - LOOKED FINE"), obs.Where(o => o[1] == "0").Select(o => o[2] + ": " + o[3]).Take(8), Theme.Green);
            list(L.T("NOT ESTABLISHED"), r.NotEst, Theme.Amber);
            list(L.T("WHAT TO TRY NEXT"), r.Next, RBlue);
            // the evidence, every line with where it came from
            b.Add(new RBlock { H = 40, Draw = (g, y) => { using (var sb = new SolidBrush(RSub)) g.DrawString(L.T("EVERYTHING AdeeboMeter OBSERVED"), F["tiny"], sb, 50, y + 16); } });
            foreach (var o in obs)
            {
                int lv; int.TryParse(o[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv); string what = clean(o[2]), val = clean(o[3]), ev = Evidence.Label(o[0]);
                float h = Math.Max(hgt(what, F["bodyb"], 230), hgt(val, F["body"], W - 440)) + 10; string tm = o[4];
                b.Add(new RBlock { H = h, Draw = (g, y) =>
                {
                    using (var db = new SolidBrush(lv == 2 ? Theme.Red : lv == 1 ? Theme.Amber : lv == 0 ? Theme.Green : RLine)) g.FillEllipse(db, 50, y + 5, 9, 9);
                    using (var ib = new SolidBrush(RInk)) g.DrawString(what, F["bodyb"], ib, new RectangleF(66, y, 230, h));
                    using (var vb = new SolidBrush(lv >= 1 && lv <= 2 ? RText(lv) : RInk)) g.DrawString(val, F["body"], vb, new RectangleF(300, y, W - 440, h));
                    using (var sb = new SolidBrush(RSub)) g.DrawString(ev + " · " + tm, F["tiny"], sb, new RectangleF(PW - 230, y + 2, 180, h));
                    using (var p = new Pen(RLine)) g.DrawLine(p, 50, y + h - 2, PW - 50, y + h - 2);
                } });
            }
            b.Add(new RBlock { H = 60, Draw = (g, y) => { using (var sb = new SolidBrush(RSub)) g.DrawString(L.T("Measured = AdeeboMeter measured it. Windows-reported = Windows said so. Inferred = worked out from the rest. Made by AdeeboMeter") + " " + Program.Version + " " + L.T("on this PC; your user and PC names and Wi-Fi name are left out. adeebometer.com"), F["tiny"], sb, new RectangleF(50, y + 20, W, 40)); } });
            return b;
        }
        // Segoe UI on every Windows; the system sans-serif anywhere it's missing
        static Font RFont(float size, FontStyle st)
        {
            try { return new Font("Segoe UI", size, st, GraphicsUnit.World); }
            catch (ArgumentException) { return new Font(FontFamily.GenericSansSerif, size, st, GraphicsUnit.World); }
        }
        static Dictionary<string, Font> ReportFonts()
        {
            Func<float, FontStyle, Font> f = RFont;
            return new Dictionary<string, Font> { { "title", f(30, FontStyle.Bold) }, { "head", f(22, FontStyle.Bold) }, { "fin", f(17, FontStyle.Bold) }, { "body", f(13, FontStyle.Regular) }, { "bodyb", f(13, FontStyle.Bold) }, { "small", f(12, FontStyle.Regular) }, { "tiny", f(10.5f, FontStyle.Bold) } };
        }
        // which blocks go on which page
        static List<List<RBlock>> Paginate(List<RBlock> bs)
        {
            var pages = new List<List<RBlock>> { new List<RBlock>() }; float y = PTop;
            foreach (var b in bs) { if (y + b.H > PBottom && pages[pages.Count - 1].Count > 0) { pages.Add(new List<RBlock>()); y = PTop; } pages[pages.Count - 1].Add(b); y += b.H; }
            return pages;
        }
        static void DrawPage(Graphics g, List<RBlock> page, int no, int of)
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; float y = PTop;
            foreach (var b in page) { b.Draw(g, y); y += b.H; }
            using (var f = RFont(10, FontStyle.Regular)) using (var sb = new SolidBrush(RSub)) g.DrawString(AdeeboMeter.L.T("AdeeboMeter · ") + (no + 1) + " / " + of, f, sb, PW - 150, PH - 30);
        }
        // the PDF (Microsoft Print to PDF, built into Windows 10 and 11 - no admin, nothing installed), else one HTML file
        internal string MakeReport(Cases.Case c)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AdeeboMeter"); Directory.CreateDirectory(dir);
            string name = "AdeeboMeter report " + c.Opened.ToString("yyyy-MM-dd HHmm", CultureInfo.InvariantCulture) + " - " + new string(c.Title.Where(ch => char.IsLetterOrDigit(ch) || ch == ' ').ToArray()).Trim();
            var fonts = ReportFonts(); List<List<RBlock>> pages;
            try
            {
                using (var mb = new Bitmap(10, 10)) using (var mg = Graphics.FromImage(mb)) pages = Paginate(ReportBlocks(c, mg, fonts));
                string pdf = Path.Combine(dir, name + ".pdf");
                if (PrinterSettings.InstalledPrinters.Cast<string>().Contains("Microsoft Print to PDF"))
                    try
                    {
                        using (var pd = new PrintDocument())
                        {
                            pd.PrinterSettings.PrinterName = "Microsoft Print to PDF"; pd.PrinterSettings.PrintToFile = true; pd.PrinterSettings.PrintFileName = pdf;
                            pd.DocumentName = name; pd.PrintController = new StandardPrintController(); pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                            int at = 0;
                            pd.PrintPage += (s, e) => { float sc = e.PageBounds.Width / PW; e.Graphics.ScaleTransform(sc, sc); DrawPage(e.Graphics, pages[at], at, pages.Count); at++; e.HasMorePages = at < pages.Count; };
                            pd.Print();
                        }
                        for (int i = 0; i < 30 && !File.Exists(pdf); i++) System.Threading.Thread.Sleep(100);   // the printer writes the file as it spools
                        if (File.Exists(pdf)) return pdf;
                    }
                    catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("report pdf: " + e.Message); }
                // one self-contained HTML file: each page a picture, ready to print (Ctrl+P > Save as PDF)
                string html = Path.Combine(dir, name + ".html"); var sb = new StringBuilder();
                sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>").Append(System.Net.WebUtility.HtmlEncode(name)).Append("</title><style>body{margin:0;background:#e9edf2;font-family:Segoe UI,sans-serif}img{display:block;width:100%;max-width:800px;margin:16px auto;box-shadow:0 2px 10px rgba(0,0,0,.15);background:#fff}p{text-align:center;color:#57606a}@media print{body{background:#fff}img{margin:0;box-shadow:none;page-break-after:always}p{display:none}}</style></head><body><p>")
                  .Append(System.Net.WebUtility.HtmlEncode(L.T("To keep it as a PDF: press Ctrl+P and choose Save as PDF."))).Append("</p>");
                for (int i = 0; i < pages.Count; i++)
                    using (var bmp = new Bitmap((int)(PW * 2), (int)(PH * 2))) using (var g = Graphics.FromImage(bmp)) using (var ms = new MemoryStream())
                    {
                        g.Clear(Color.White); g.ScaleTransform(2, 2); g.SmoothingMode = SmoothingMode.AntiAlias; DrawPage(g, pages[i], i, pages.Count);
                        bmp.Save(ms, ImageFormat.Png); sb.Append("<img alt=\"page ").Append(i + 1).Append("\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(ms.ToArray())).Append("\">");
                    }
                sb.Append("</body></html>"); File.WriteAllText(html, sb.ToString(), Encoding.UTF8);
                return html;
            }
            finally { foreach (var f in fonts.Values) f.Dispose(); }
        }
    }
}
