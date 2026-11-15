// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // ================================================================== tool pictures
    // Every tool gets a picture of what it does: before you press Run it shows how it works (from what's in the boxes),
    // after a run - or after Example - the same picture filled with the result. Drawn only from the boxes and the
    // result rows on screen: nothing is sent to draw it. First wave: packet stack, encrypt / open, ping, TCP ping, dig,
    // HTTP and TLS, speed test, subnet, MTR, MTU, port test, Wi-Fi link rate, Wi-Fi roams.
    sealed partial class FullView
    {
        static readonly string[] EmptyIcons = { "time", "app", "traffic", "story", "cpu" };
        static readonly string[] DotSep = { " · " }; static readonly char[] SpaceSep = { ' ' };
        static readonly Color VPurple = Theme.C("#8250DF"), VSlate = Theme.C("#57606A");
        static readonly HashSet<string> Pictured = new HashSet<string> { "stack", "encrypt", "decrypt", "ping", "tcpping", "dig", "http", "speedtest", "subnet", "mtr", "mtu", "port", "wifirate", "roams" };
        public static bool HasPicture(string id) { return Pictured.Contains(id) || IsCalc(id) || InWave3(id); }
        string visFail = "";
        // self-test: every picture draws before a run and with its Example, and the Example's picture grows from the result
        internal string VisualCheck()
        {
            EnsureToolbox(); var bad = new List<string>();
            using (var bmp = new Bitmap(1000, 2400)) using (var g = Graphics.FromImage(bmp))
                foreach (var id in Pictured.Concat(Pictured3).Concat(Pictured4).Concat(Pictured5).Concat(Pictured6).Concat(CalcSpecs().Select(z => z.Id)).Distinct())
                {
                    SelectTool(id); visFail = "";
                    float y0 = PaintToolVisual(g, 10, 10, 900); if (visFail != "") bad.Add("idle " + visFail); else if (y0 <= 10) bad.Add(id + ": no picture before a run");
                    if (Examples.Get(id) == null) { bad.Add(id + ": no example"); continue; }
                    ShowExample(); visFail = "";
                    float y1 = PaintToolVisual(g, 10, 10, 900); if (visFail != "") bad.Add("example " + visFail); else if (y1 <= 10) bad.Add(id + ": no picture with the example");
                    if (VisState() != "EXAMPLE") bad.Add(id + ": example not marked");
                }
            if (HopsNowCount("mtr") < 3) bad.Add("mtr example: hops not read");
            // the front door: words people use find the right case; tool names alone don't
            foreach (var t in new[] { new[] { "my teams call keeps freezing", "calls" }, new[] { "netflix buffering", "buffer" }, new[] { "wifi keeps dropping", "drops" }, new[] { "check everything", "full" }, new[] { "game lag", "game" }, new[] { "mtu", "" }, new[] { "dig", "" }, new[] { "subnet", "" } })
            { var m = Cases.Match(t[0]); if ((m == null ? "" : m[0]) != t[1]) bad.Add("case for \"" + t[0] + "\": " + (m == null ? "none" : m[0])); }
            // a finished case makes a report (an HTML file here - Wine has no Microsoft Print to PDF), with its pages as pictures
            var cs = Cases.New("calls", "search", "a test", DateTime.Now.AddMinutes(-20), DateTime.Now); var gr = new GuideRun { Id = "calls", Title = "Calls" };
            gr.Obs("m", 2, "Jitter", "48 ms"); gr.Obs("m", 0, "Your router answers in", "2 ms"); gr.Obs("w", 1, "Wi-Fi signal", "31%"); gr.NotEst.Add("whether the other side's line is fine"); gr.Next.Add("Use a cable for calls"); gr.Conclude(2, "Your Wi-Fi: jitter jumps when the signal drops."); gr.Done = true; cs.Run = gr;
            if (Cases.Sure(gr) < 2) bad.Add("a case backed by measurements isn't 'likely' or better");
            try { string f = MakeReport(cs); if (!System.IO.File.Exists(f)) bad.Add("report not written"); else { if (f.EndsWith(".html", StringComparison.Ordinal) && !System.IO.File.ReadAllText(f).Contains("data:image/png;base64,")) bad.Add("report has no pages"); System.IO.File.Delete(f); } }
            catch (Exception e) { bad.Add("report: " + e.GetType().Name + " " + e.Message); }
            // the ISP report's picture pages draw (a made-up evening problem)
            try { string isp = IspReport.Sample(); if (isp == null || !System.IO.File.ReadAllText(isp).Contains("<svg")) bad.Add("ISP report: no picture pages"); }
            catch (Exception e) { bad.Add("ISP report pages: " + e.GetType().Name + " " + e.Message); }
            // Ten years of money: always-index gets forced to sell in a bad year; emergency-fund-first never does
            double s1 = 0, i1 = 0, f1 = 0, s3 = 0, i3 = 0, f3 = 0; for (int yr = 0; yr < 10; yr++) { f1 += YrStep(yr, 1, ref s1, ref i1); f3 += YrStep(yr, 3, ref s3, ref i3); }
            if (f1 > 0 || f3 <= 0) bad.Add("ten years of money: forced sales " + f1 + " / " + f3);
            using (var bmp = new Bitmap(900, 400)) using (var g2 = Graphics.FromImage(bmp))
                foreach (var t in Knowledge.Learn().Where(z => z["pic"] != "")) { try { MoneyPic(g2, t["pic"], 0, 0, 860); } catch (Exception e) { bad.Add("money picture " + t["pic"] + ": " + e.Message); } }
            // every calculator's form: its defaults give a clean result, and reading its own text back changes nothing
            foreach (var sp in CalcSpecs())
            {
                var t = Tools2().FirstOrDefault(z => z.Id == sp.Id); if (t == null) { bad.Add(sp.Id + ": no tool"); continue; }
                if (sp.F.Max(f => f.Map) >= t.Inputs.Length) bad.Add(sp.Id + ": the form writes more boxes than the tool has");
                var a = Compose(sp); List<string[]> rows; try { rows = t.Do(a, new Toolkit.Run()); } catch (Exception e) { bad.Add(sp.Id + ": " + e.Message); continue; }
                var b2 = rows == null ? null : rows.FirstOrDefault(o => o[0] == "2"); if (rows == null || rows.Count == 0 || (b2 != null && sp.Id != "fxconv")) bad.Add(sp.Id + " defaults: " + (b2 == null ? "nothing" : b2[1] + " " + b2[2]));
                var back = SpecOf(sp.Id); FillSpec(back, a); if (string.Join("|", Compose(back)) != string.Join("|", a)) bad.Add(sp.Id + ": the form doesn't read its own text back (" + string.Join("|", a) + " -> " + string.Join("|", Compose(back)) + ")");
            }
            using (var bmp = new Bitmap(1000, 400)) using (var g = Graphics.FromImage(bmp))
                foreach (var ic in EmptyIcons) { try { Kit.Empty(g, 10, 10, 760 * S, ic, "stream", "t", "b", null); } catch (Exception e) { bad.Add("empty-state sketch " + ic + ": " + e.Message); } }
            return string.Join("; ", bad.Take(8));
        }
        int HopsNowCount(string id) { SelectTool(id); ShowExample(); return HopsNow().Count; }

        float PaintToolVisual(Graphics g, float x, float y, float w)
        {
            if (!HasPicture(tbSel) || tbBusy) { visRect = RectangleF.Empty; return y; }
            float y0 = y; float yEnd = PaintToolVisual0(g, x, y, w); visRect = yEnd > y0 ? new RectangleF(x, y0, w, yEnd - y0 - 14 * S) : RectangleF.Empty; return yEnd;
        }
        float PaintToolVisual0(Graphics g, float x, float y, float w)
        {
            try
            {
                string st = VisState();
                if (st == "YOUR RESULT" && IsCalc(tbSel)) st = "YOUR NUMBERS";
                switch (tbSel)
                {
                    case "stack": return VisStack(g, x, y, w, st);
                    case "encrypt": return VisCrypt(g, x, y, w, st, true);
                    case "decrypt": return VisCrypt(g, x, y, w, st, false);
                    case "ping": case "tcpping": return VisPing(g, x, y, w, st);
                    case "dig": return DeepExchange(g, x, y, w, st);
                    case "http": return VisHttp(g, x, y, w, st);
                    case "speedtest": return VisSpeed(g, x, y, w, st);
                    case "subnet": return VisSubnet(g, x, y, w, st);
                    case "mtr": case "mtu": return VisHops(g, x, y, w, st);
                    case "port": return VisPort(g, x, y, w, st);
                    case "wifirate": return VisRate(g, x, y, w, st);
                    case "roams": return VisRoams(g, x, y, w, st);
                    default: return InWave3(tbSel) ? Wave3(g, x, y, w, st) : CalcPicture(g, x, y, w, st);
                }
            }
            catch (Exception e) { visFail = tbSel + ": " + e.GetType().Name + " " + e.Message; Program.Log("tool picture " + visFail); }   // a picture never breaks the page
            return y;
        }

        // ---------- what the picture is drawn from
        bool VisEx() { return Examples.IsExample(tbRows); }
        string VisState() { return VisEx() ? "EXAMPLE" : tbRows != null ? "YOUR RESULT" : "HOW IT WORKS"; }
        // the boxes the result was made from (an example's own inputs), or what's typed now
        string[] VisIn()
        {
            string[] a;
            if (VisEx()) { var ex = Examples.Get(tbSel); a = ex == null ? null : ex.In; }
            else a = tbRows != null && tbShown != null ? tbShown : Inputs();
            var r = new string[4]; for (int i = 0; i < 4; i++) r[i] = a != null && i < a.Length && a[i] != null ? a[i].Trim() : ""; return r;
        }
        // the result rows without the example banner and table lines
        List<string[]> VisRows()
        {
            if (tbRows == null) return new List<string[]>();
            return tbRows.Where(o => o.Length > 2 && o[1] != Examples.Mark && o[0] != "6" && o[0] != "7" && o[0] != "8").ToList();
        }
        string[] VRow(string name) { return VisRows().FirstOrDefault(o => o[1].StartsWith(name, StringComparison.OrdinalIgnoreCase)); }
        string VisText() { return string.Join("  ", VisRows().Select(o => o[1] + " " + o[2]).ToArray()); }
        // -1 nothing yet, 0 fine, 1 note, 2 problem
        int VisLevel()
        {
            var rs = VisRows(); if (rs.Count == 0) return -1;
            if (rs.Any(o => o[0] == "2")) return 2; if (rs.Any(o => o[0] == "0")) return 0; return rs.Any(o => o[0] == "1") ? 1 : 0;
        }
        static double VNum(string text, string pattern)
        {
            var m = Regex.Match(text ?? "", pattern, RegexOptions.IgnoreCase); double v;
            return m.Success && double.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN;
        }
        static string VFile(string path) { string p = (path ?? "").Trim().Trim('"'); int i = Math.Max(p.LastIndexOf('\\'), p.LastIndexOf('/')); return i >= 0 ? p.Substring(i + 1) : p; }

        // ---------- the kit
        // the card every picture sits on: caption on the left, what it shows (how it works / example / your result) on the right
        static RectangleF VisCard(Graphics g, float x, float y, float w, float h, string caption, string state)
        {
            var r = new RectangleF(x, y, w, h); Kit.Box(g, r, 14 * S);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T(caption), Draw.fTiny, mb, x + 16 * S, y + 12 * S);
            string sl = L.T(state); var ss = g.MeasureString(sl, Draw.fTiny); var pr = new RectangleF(x + w - ss.Width - 30 * S, y + 9 * S, ss.Width + 16 * S, ss.Height + 4 * S);
            Theme.Fill(g, pr, pr.Height / 2, state == "YOUR RESULT" || state == "YOUR NUMBERS" ? Theme.LvSoft(0) : state == "EXAMPLE" ? Theme.LvSoft(1) : Theme.Soft);
            using (var sb = new SolidBrush(state == "YOUR RESULT" || state == "YOUR NUMBERS" ? Theme.LvText(0) : state == "EXAMPLE" ? Theme.LvText(1) : Theme.Sub)) g.DrawString(sl, Draw.fTiny, sb, pr.X + 8 * S, pr.Y + 2 * S);
            return r;
        }
        // a round stop: filled when it's done or reached, an outline while it's only the plan
        static void VNode(Graphics g, float cx, float cy, float rad, string icon, Color c, bool filled)
        {
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2);
            if (filled) Theme.Fill(g, r, rad, c); else { Theme.Fill(g, r, rad, Theme.Card); Theme.Stroke(g, r, rad, c, 2 * S); }
            float ic = rad * 1.1f; var ir = new RectangleF(cx - ic / 2, cy - ic / 2, ic, ic);
            if (icon == "doc") VDoc(g, ir, filled ? Color.White : c); else if (icon != "") Kit.Icon(g, icon, ir, filled ? Color.White : c);
            g.SmoothingMode = sm;
        }
        // a page with a folded corner - a file
        static void VDoc(Graphics g, RectangleF r, Color c)
        {
            float u = r.Width / 24f; Func<float, float> X = v => r.X + v * u, Y = v => r.Y + v * u;
            using (var p = new Pen(c, Math.Max(1.2f, 2f * u)) { LineJoin = LineJoin.Round })
            {
                g.DrawLines(p, new[] { new PointF(X(14), Y(3)), new PointF(X(6), Y(3)), new PointF(X(6), Y(21)), new PointF(X(18), Y(21)), new PointF(X(18), Y(7)), new PointF(X(14), Y(3)), new PointF(X(14), Y(7)), new PointF(X(18), Y(7)) });
                g.DrawLine(p, X(9), Y(12), X(15), Y(12)); g.DrawLine(p, X(9), Y(16), X(15), Y(16));
            }
        }
        static void VArrow(Graphics g, float x1, float y1, float x2, float y2, Color c, bool dashed)
        {
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(c, 2 * S)) { if (dashed) p.DashStyle = DashStyle.Dash; p.CustomEndCap = Cap45; g.DrawLine(p, x1, y1, x2, y2); }
            g.SmoothingMode = sm;
        }
        // centred words under a stop: a bold name and a quieter line
        static void VLabel(Graphics g, float cx, float y, float width, string name, string detail, Color detailColor)
        {
            using (var c = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            using (var ib = new SolidBrush(Theme.Ink)) using (var db = new SolidBrush(detailColor))
            {
                g.DrawString(name, Draw.fBodyB, ib, new RectangleF(cx - width / 2, y, width, 20 * S), c);
                if (detail != "") g.DrawString(detail, Draw.fSmall, db, new RectangleF(cx - width / 2, y + 20 * S, width, 34 * S), c);
            }
        }
        static void VPill(Graphics g, string text, float x, float y, int lv)
        {
            var ts = g.MeasureString(text, Draw.fSmall); var r = new RectangleF(x, y, ts.Width + 14 * S, ts.Height + 4 * S);
            Theme.Fill(g, r, r.Height / 2, Theme.LvSoft(lv)); using (var b = new SolidBrush(Theme.LvText(lv))) g.DrawString(text, Draw.fSmall, b, x + 7 * S, y + 2 * S);
        }
        static Color VLv(int lv) { return lv == 2 ? Theme.Red : lv == 1 ? Theme.Amber : lv == 0 ? Theme.Green : Theme.Muted; }

        // ================================================================== packet stack: one full-size packet, outermost first
        float VisStack(Graphics g, float x, float y, float w, string state)
        {
            var a = VisIn(); string err; var st = Calc.Parse(a[0], out err); if (err != null || st.Count == 0) return y;
            int mtu = 1500, m; if (int.TryParse(a[1].Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out m) && m >= 576 && m <= 9216) mtu = m;
            int pay = st.Where(l => l.Kind == "p").Sum(l => l.Bytes), ip = st.Where(l => l.Kind == "i").Sum(l => l.Bytes), tun = st.Where(l => l.Kind == "t").Sum(l => l.Bytes), shim = st.Where(l => l.Kind == "l").Sum(l => l.Bytes);
            int maxData = mtu - shim - tun - ip - pay; bool tls = st.Any(l => l.Kind == "r");
            var wire = st.Where(l => l.Kind != "r").Reverse().ToList();
            var r = VisCard(g, x, y, w, 186 * S, "ONE FULL-SIZE PACKET, AS IT GOES ON THE WIRE", state);
            float bx = r.X + 18 * S, bw = r.Width - 36 * S, by = r.Y + 58 * S, bh = 50 * S;
            using (var mb = new SolidBrush(Theme.Muted)) { string sf = L.T("sent first"); float sfw = g.MeasureString(sf, Draw.fTiny).Width; g.DrawString(sf, Draw.fTiny, mb, bx, by - 18 * S); VArrow(g, bx + sfw + 4 * S, by - 11 * S, bx + sfw + 30 * S, by - 11 * S, Theme.Muted, false); }
            float hw = Math.Min(96 * S, bw * 0.64f / Math.Max(1, wire.Count)), cx = bx, mtuStart = -1;
            using (var wb = new SolidBrush(Color.White)) using (var c = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                foreach (var l in wire)
                {
                    if (l.Kind != "f" && mtuStart < 0) mtuStart = cx;
                    Color col = l.Kind == "f" ? VSlate : l.Kind == "l" ? Theme.Orange : l.Kind == "t" ? VPurple : l.Kind == "i" ? Theme.Blue : Theme.Green;
                    Theme.Fill(g, new RectangleF(cx + 1 * S, by, hw - 2 * S, bh), 7 * S, col);
                    string nm = l.Name; int k = nm.IndexOf(" (", StringComparison.Ordinal); if (k > 0) nm = nm.Substring(0, k);
                    g.DrawString(nm, Draw.fSmall, wb, new RectangleF(cx + 3 * S, by + 8 * S, hw - 6 * S, 18 * S), c);
                    g.DrawString((l.Approx ? "~" : "") + l.Bytes + " B", Draw.fTiny, wb, new RectangleF(cx + 3 * S, by + 28 * S, hw - 6 * S, 16 * S), c);
                    cx += hw;
                }
            if (mtuStart < 0) mtuStart = cx;
            var dr = new RectangleF(cx + 1 * S, by, bx + bw - cx - 1 * S, bh);
            Theme.Fill(g, dr, 7 * S, maxData > 0 ? Theme.LvSoft(0) : Theme.LvSoft(2));
            using (var tb = new SolidBrush(maxData > 0 ? Theme.LvText(0) : Theme.LvText(2))) using (var c = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(maxData > 0 ? L.T("YOUR DATA") : L.T("NO ROOM LEFT"), Draw.fSmall, tb, new RectangleF(dr.X, by + 8 * S, dr.Width, 18 * S), c);
                g.DrawString(maxData > 0 ? "up to " + maxData.ToString("N0", CultureInfo.CurrentCulture) + " bytes" + (tls ? " · TLS adds 22 B a record" : "") : "the headers fill the MTU", Draw.fTiny, tb, new RectangleF(dr.X, by + 28 * S, dr.Width, 16 * S), c);
            }
            // what the link's MTU covers, and what rides outside it
            float ly = by + bh + 8 * S;
            using (var p = new Pen(Theme.Muted, 1.5f * S)) using (var sb = new SolidBrush(Theme.Sub)) using (var c = new StringFormat { Alignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawLines(p, new[] { new PointF(mtuStart + 2 * S, ly), new PointF(mtuStart + 2 * S, ly + 6 * S), new PointF(bx + bw - 2 * S, ly + 6 * S), new PointF(bx + bw - 2 * S, ly) });
                g.DrawString("fits in the link MTU: " + mtu.ToString("N0", CultureInfo.CurrentCulture) + " bytes", Draw.fSmall, sb, new RectangleF(mtuStart, ly + 9 * S, bx + bw - mtuStart, 18 * S), c);
                if (mtuStart > bx + 4 * S)
                {
                    g.DrawLines(p, new[] { new PointF(bx + 2 * S, ly), new PointF(bx + 2 * S, ly + 6 * S), new PointF(mtuStart - 4 * S, ly + 6 * S), new PointF(mtuStart - 4 * S, ly) });
                    g.DrawString(L.T("outside the MTU"), Draw.fSmall, sb, new RectangleF(bx, ly + 9 * S, mtuStart - bx, 18 * S), c);
                }
            }
            // the colours, only the kinds in this stack
            float lx = bx, lgy = r.Bottom - 26 * S;
            var kinds = new[] { new[] { "f", "Link (outside the MTU)" }, new[] { "l", "PPPoE" }, new[] { "t", "Tunnel (its own outer headers)" }, new[] { "i", "IP" }, new[] { "p", "Transport" } };
            using (var sb = new SolidBrush(Theme.Sub))
                foreach (var kd in kinds)
                {
                    if (!wire.Any(l => l.Kind == kd[0])) continue;
                    Color col = kd[0] == "f" ? VSlate : kd[0] == "l" ? Theme.Orange : kd[0] == "t" ? VPurple : kd[0] == "i" ? Theme.Blue : Theme.Green;
                    Theme.Fill(g, new RectangleF(lx, lgy + 4 * S, 10 * S, 10 * S), 3 * S, col);
                    string t = L.T(kd[1]); g.DrawString(t, Draw.fSmall, sb, lx + 14 * S, lgy); lx += 26 * S + g.MeasureString(t, Draw.fSmall).Width;
                }
            return r.Bottom + 14 * S;
        }

        // ================================================================== encrypt / open: the file, the lock, the way the code travels
        float VisCrypt(Graphics g, float x, float y, float w, string state, bool enc)
        {
            var a = VisIn(); int lv = VisLevel(); bool done = lv == 0, bad = lv == 2;
            string file = VFile(a[0]); if (file == "") file = enc ? L.T("your file") : L.T("the .adeebo file");
            bool code = a.Length < 2 || a[1] == "" || a[1].Equals("code", StringComparison.OrdinalIgnoreCase);
            var r = VisCard(g, x, y, w, 214 * S, enc ? "WHAT HAPPENS TO YOUR FILE" : "OPENING A LOCKED FILE", state);
            string[][] stops;
            if (enc)
            {
                string outName = file.EndsWith(".adeebo", StringComparison.OrdinalIgnoreCase) ? file : file + (code ? ".adeebo" : ".p7m");
                var er = VRow("Encrypted"); if (er != null) { string v = er[2]; int k = v.IndexOf(" - ", StringComparison.Ordinal); outName = k > 0 ? v.Substring(0, k) : v; }
                stops = new[] { new[] { "doc", file, L.T("stays as it is") }, new[] { "lock", L.T("Locked"), "AES-256 + a seal (HMAC)" }, new[] { "doc", outName, L.T("send it any way - email, USB, cloud") }, new[] { "devices", code ? L.T("The other person") : a[1], L.T("opens it in AdeeboMeter") } };
            }
            else
            {
                string got = L.T("the original file"); var orow = VRow("Opened"); if (orow != null) { string v = orow[2]; int k = v.IndexOf(" - ", StringComparison.Ordinal); got = k > 0 ? v.Substring(0, k) : v; }
                stops = new[] { new[] { "doc", file, L.T("locked") }, new[] { "key", a[1] != "" ? L.T("The code") : L.T("Your key"), L.T("unlocks it") }, new[] { "doc", got, L.T("saved next to it") }, new[] { "check", L.T("Seal checked"), L.T("not changed since it was locked") } };
            }
            float colW = (r.Width - 32 * S) / stops.Length, cy = r.Y + 70 * S, rad = 24 * S;
            for (int i = 0; i < stops.Length; i++)
            {
                float cx = r.X + 16 * S + colW * i + colW / 2;
                if (i > 0) VArrow(g, cx - colW + rad + 6 * S, cy, cx - rad - 6 * S, cy, done ? Theme.Green : Theme.Line, !done);
                Color c = bad && i >= 1 ? Theme.Red : done ? Theme.Green : i == 1 ? Theme.Blue : Theme.Muted;
                VNode(g, cx, cy, rad, stops[i][0], c, done || (i == 1 && !bad));
                VLabel(g, cx, cy + rad + 8 * S, colW - 10 * S, stops[i][1], stops[i][2], Theme.Sub);
            }
            // the second road: the code (or their key) never travels with the file
            float ky = r.Bottom - 30 * S, x1 = r.X + 16 * S + colW * 1.5f, x2 = r.X + 16 * S + colW * 3.5f;
            if (enc)
            {
                string kc = ""; var cr = VRow("The code"); if (cr != null) kc = cr[2];
                string kt = code ? (kc != "" ? L.T("The code") + " " + kc + " - " + L.T("by phone or text, never with the file") : L.T("The code goes another way - by phone or text, never with the file")) : L.T("Their public key: only they can open it - no code to share");
                using (var p = new Pen(code ? Theme.Orange : VPurple, 2 * S) { DashStyle = DashStyle.Dash }) { p.CustomEndCap = Cap45; g.DrawLines(p, new[] { new PointF(x1, cy + rad + 44 * S), new PointF(x1, ky), new PointF(x2, ky), new PointF(x2, cy + rad + 44 * S) }); }
                using (var tb = new SolidBrush(code ? Theme.LvText(1) : Theme.Ink)) using (var c = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    var ts = g.MeasureString(kt, Draw.fSmall); float tw = Math.Min(ts.Width + 16 * S, r.Width - 40 * S);
                    Theme.Fill(g, new RectangleF((x1 + x2) / 2 - tw / 2, ky - 10 * S, tw, 20 * S), 10 * S, Theme.Card);
                    g.DrawString(kt, Draw.fSmall, tb, new RectangleF((x1 + x2) / 2 - tw / 2, ky - 9 * S, tw, 18 * S), c);
                }
            }
            else using (var sb = new SolidBrush(bad ? Theme.LvText(2) : Theme.Sub))
                g.DrawString(bad ? L.T("Didn't open: a wrong code, the wrong key - or the file was changed after it was locked. The seal can't be faked.") : L.T("If even one byte was changed on the way, the seal doesn't match and nothing is opened."), Draw.fSmall, sb, new RectangleF(r.X + 18 * S, ky - 8 * S, r.Width - 36 * S, 22 * S));
            return r.Bottom + 14 * S;
        }

        // ================================================================== ping / TCP ping: where the time lands on a scale you can read
        static readonly double[] PingTicks = { 0, 10, 20, 50, 100, 200, 300 };
        static float PingPos(double ms, float x0, float width) { return x0 + width * (float)Math.Sqrt(Math.Max(0, Math.Min(300, ms)) / 300.0); }
        float VisPing(Graphics g, float x, float y, float w, string state)
        {
            var a = VisIn(); bool tcp = tbSel == "tcpping"; string target = a[0] != "" ? a[0] : tcp ? "host:port" : L.T("the target");
            double avg = double.NaN, best = double.NaN, worst = double.NaN, loss = double.NaN;
            var run = tbRun0; List<double> s = run != null && !VisEx() && tbRows != null ? run.Copy() : null;
            if (s != null && s.Count > 0)
            {
                var ok = s.Where(v => !double.IsNaN(v)).ToList(); loss = 100.0 * (s.Count - ok.Count) / s.Count;
                if (ok.Count > 0) { avg = ok.Average(); best = ok.Min(); worst = ok.Max(); }
            }
            else if (tbRows != null)
            {
                string t = VisText();
                avg = VNum(t, @"(?:avg|average|median)\s+([0-9.,]+)"); best = VNum(t, @"(?:best|min)\s+([0-9.,]+)"); worst = VNum(t, @"(?:worst|max)\s+([0-9.,]+)");
                loss = VNum(t, @"([0-9.]+)%\s*loss"); if (double.IsNaN(loss)) loss = VNum(t, @"loss\s*([0-9.]+)%");
                if (double.IsNaN(loss)) { var mm = Regex.Match(t, @"([0-9]+) of ([0-9]+)"); if (mm.Success) { double n1 = double.Parse(mm.Groups[1].Value, CultureInfo.InvariantCulture), n2 = double.Parse(mm.Groups[2].Value, CultureInfo.InvariantCulture); if (n2 > 0) loss = 100 * (n2 - n1) / n2; } }
            }
            bool have = !double.IsNaN(avg);
            var r = VisCard(g, x, y, w, 236 * S, tcp ? "A TCP HELLO AND BACK, TIMED" : "A PING THERE AND BACK, TIMED", state);
            // the trip
            float py = r.Y + 52 * S, lx = r.X + 40 * S, rx = r.Right - 40 * S;
            VNode(g, lx, py, 16 * S, "devices", Theme.Blue, true); VNode(g, rx, py, 16 * S, "dest", have ? VLv(avg < 50 ? 0 : avg < 100 ? 1 : 2) : Theme.Muted, have);
            VArrow(g, lx + 22 * S, py - 5 * S, rx - 22 * S, py - 5 * S, Theme.Blue, !have); VArrow(g, rx - 22 * S, py + 5 * S, lx + 22 * S, py + 5 * S, Theme.Green, !have);
            using (var sb = new SolidBrush(Theme.Sub)) using (var c = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(tcp ? L.T("\"can I connect?\" (SYN)") : L.T("\"are you there?\" (echo request)"), Draw.fSmall, sb, new RectangleF(lx + 30 * S, py - 26 * S, rx - lx - 60 * S, 18 * S), c);
                g.DrawString(tcp ? L.T("\"yes, come in\" (SYN-ACK) - the time is this round trip") : L.T("\"yes, here\" (echo reply) - the time is this round trip"), Draw.fSmall, sb, new RectangleF(lx + 30 * S, py + 9 * S, rx - lx - 60 * S, 18 * S), c);
            }
            using (var ib = new SolidBrush(Theme.Ink))
            {
                g.DrawString(L.T("This PC"), Draw.fSmall, ib, r.X + 16 * S, py + 20 * S);
                using (var far = new StringFormat { Alignment = StringAlignment.Far, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(target, Draw.fSmall, ib, new RectangleF(r.Right - 260 * S, py + 20 * S, 244 * S, 18 * S), far);
            }
            // the scale: square-root spaced so 1-50 ms (where most of life is) gets room
            float sx = r.X + 18 * S, sw = r.Width - 36 * S, sy = r.Y + 150 * S, sh = 16 * S;
            var bands = new[] { new object[] { 0.0, 20.0, 0, "great - games" }, new object[] { 20.0, 50.0, 0, "good" }, new object[] { 50.0, 100.0, 1, "fine for calls" }, new object[] { 100.0, 300.0, 2, "laggy" } };
            foreach (var b in bands)
            {
                float b0 = PingPos((double)b[0], sx, sw), b1 = PingPos((double)b[1], sx, sw); int blv = (int)b[2];
                Theme.Fill(g, new RectangleF(b0 + 1 * S, sy, b1 - b0 - 2 * S, sh), 4 * S, Theme.LvSoft(blv));
                using (var tb = new SolidBrush(Theme.LvText(blv))) using (var c = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(L.T((string)b[3]), Draw.fTiny, tb, new RectangleF(b0, sy + 1 * S, b1 - b0, sh), c);
            }
            using (var mb = new SolidBrush(Theme.Muted)) using (var c = new StringFormat { Alignment = StringAlignment.Center })
                foreach (var t in PingTicks) g.DrawString(t.ToString("0", CultureInfo.CurrentCulture) + (t == 300 ? " ms" : ""), Draw.fTiny, mb, new RectangleF(PingPos(t, sx, sw) - 30 * S, sy + sh + 3 * S, 60 * S, 14 * S), c);
            if (have)
            {
                if (!double.IsNaN(best) && !double.IsNaN(worst))
                    using (var p = new Pen(Theme.Ink, 3 * S)) { float b0 = PingPos(best, sx, sw), b1 = PingPos(worst, sx, sw); g.DrawLine(p, b0, sy - 7 * S, b1, sy - 7 * S); g.DrawLine(p, b0, sy - 11 * S, b0, sy - 3 * S); g.DrawLine(p, b1, sy - 11 * S, b1, sy - 3 * S); }
                float ax = PingPos(avg, sx, sw); int alv = avg < 50 ? 0 : avg < 100 ? 1 : 2;
                var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var ab = new SolidBrush(VLv(alv))) g.FillPolygon(ab, new[] { new PointF(ax - 7 * S, sy - 20 * S), new PointF(ax + 7 * S, sy - 20 * S), new PointF(ax, sy - 9 * S) });
                g.SmoothingMode = sm;
                string lab = "avg " + avg.ToString(avg < 10 ? "0.#" : "0", CultureInfo.CurrentCulture) + " ms" + (!double.IsNaN(best) ? "  ·  best " + best.ToString("0.#", CultureInfo.CurrentCulture) + ", worst " + worst.ToString("0.#", CultureInfo.CurrentCulture) : "");
                var ls = g.MeasureString(lab, Draw.fBodyB); float lxx = Math.Max(sx, Math.Min(sx + sw - ls.Width, ax - ls.Width / 2));
                using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(lab, Draw.fBodyB, ib, lxx, sy - 42 * S);
                if (!double.IsNaN(loss)) VPill(g, loss.ToString("0.#", CultureInfo.CurrentCulture) + "% " + L.T("lost"), r.Right - 120 * S, r.Bottom - 30 * S, loss >= 3 ? 2 : loss > 0 ? 1 : 0);
            }
            else using (var mb = new SolidBrush(Theme.Muted))
                {
                    // where things usually land, so the number means something before you have one
                    var marks = new[] { new object[] { 2.0, "your router ~2" }, new object[] { 12.0, "same city ~12" }, new object[] { 70.0, "other coast ~70" }, new object[] { 180.0, "other side of the world ~180" } };
                    foreach (var mk in marks)
                    {
                        float mx = PingPos((double)mk[0], sx, sw); string t = L.T((string)mk[1]); var ts = g.MeasureString(t, Draw.fTiny);
                        using (var p = new Pen(Theme.Muted, 1 * S)) g.DrawLine(p, mx, sy - 10 * S, mx, sy - 2 * S);
                        g.DrawString(t, Draw.fTiny, mb, Math.Max(sx, Math.Min(sx + sw - ts.Width, mx - ts.Width / 2)), sy - 26 * S);
                    }
                }
            using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(L.T("Light in glass does about 100 km a millisecond each way - distance sets the floor, busy links add the rest."), Draw.fSmall, sb, new RectangleF(sx, r.Bottom - 30 * S, sw - (have ? 130 * S : 0), 20 * S));
            return r.Bottom + 14 * S;
        }

        // ================================================================== dig: who your PC asks, and who they ask

        // ================================================================== HTTP and TLS: the handshake, then the page
        float VisHttp(Graphics g, float x, float y, float w, string state)
        {
            var a = VisIn(); string url = a[0] != "" ? a[0] : "example.com"; bool plain = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
            string host = Regex.Replace(url, @"^[a-z]+://", "", RegexOptions.IgnoreCase).Split('/', ':')[0]; if (host == "") host = "the server";
            string sni = a[1] != "" ? a[1] : host;
            string tlsv = "", status = L.T("the page"), cert = L.T("its certificate"), ms = "";
            var hr = VRow("HTTP"); if (hr != null) { string v = hr[2]; var parts = v.Split(DotSep, StringSplitOptions.None); status = parts[0]; foreach (var p in parts) { if (p.StartsWith("TLS", StringComparison.Ordinal)) tlsv = p; if (p.EndsWith(" ms", StringComparison.Ordinal)) ms = p; } }
            var cr = VRow("Certificate"); if (cr != null) { string v = cr[2]; int k = v.IndexOf(" · ", StringComparison.Ordinal); cert = L.T("certificate:") + " " + (k > 0 ? v.Substring(0, k) : v); }
            var st = new List<XStep> { X(0, 1, L.T("hello (SYN)"), "TCP " + (plain ? "80" : "443"), double.NaN), X(1, 0, L.T("hello back (SYN-ACK)"), L.T("the connection is open"), double.NaN) };
            if (!plain) { st.Add(X(0, 1, L.T("TLS hello - name") + ": " + sni, L.T("ClientHello - the name picks the certificate (SNI)"), double.NaN)); st.Add(X(1, 0, cert + (tlsv != "" ? " · " + tlsv : ""), L.T("ServerHello + certificate + keys"), double.NaN)); }
            st.Add(X(0, 1, "GET /", plain ? L.T("readable by anyone on the way") : L.T("sealed"), double.NaN)); st.Add(X(1, 0, status, ms != "" ? L.T("all of it took") + " " + ms : L.T("the page"), double.NaN));
            if (!plain) foreach (var z in st.Skip(4)) z.Sealed = true; else foreach (var z in st.Skip(2)) z.Lv = 1;
            return ExchangeCard(g, x, y, w, state, plain ? "OPENING A PAGE - NOT ENCRYPTED" : "OPENING A PAGE SAFELY - THE HANDSHAKE, THEN THE PAGE", new[] { L.T("This PC"), Draw.Trunc(host, 26) }, st,
                plain ? "Plain HTTP: anyone on the way can read and change it." : "The name you ask for (SNI) picks the certificate; everything after the hello is encrypted.");
        }

        // ================================================================== speed test: two dials and the lag under load
        static float SpeedFrac(double mbps) { return (float)Math.Max(0, Math.Min(1, Math.Log10(Math.Max(1, mbps)) / 3.0)); }   // 1 to 1000 Mb/s
        static void VGauge(Graphics g, float cx, float cy, float rad, double v, Color c, string title)
        {
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            var ar = new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2);
            using (var p = new Pen(Theme.Track, 12 * S) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, ar, 180, 180);
            if (!double.IsNaN(v) && v > 0) using (var p = new Pen(c, 12 * S) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, ar, 180, Math.Max(2, 180 * SpeedFrac(v)));
            using (var mb = new SolidBrush(Theme.Muted))
                foreach (var t in new[] { 10, 100, 1000 })
                {
                    double ang = Math.PI * (1 + SpeedFrac(t)); float tx = cx + (rad + 24 * S) * (float)Math.Cos(ang), ty = cy + (rad + 18 * S) * (float)Math.Sin(ang) - (t == 1000 ? 8 * S : 0);
                    string s = t == 1000 ? "1 Gb" : t.ToString(CultureInfo.InvariantCulture); var ts = g.MeasureString(s, Draw.fTiny); g.DrawString(s, Draw.fTiny, mb, tx - ts.Width / 2, ty - ts.Height / 2);
                }
            g.SmoothingMode = sm;
            using (var ib = new SolidBrush(double.IsNaN(v) ? Theme.Muted : Theme.Ink)) using (var sb = new SolidBrush(Theme.Sub)) using (var c2 = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString(double.IsNaN(v) ? "-" : v.ToString(v < 10 ? "0.#" : "0", CultureInfo.CurrentCulture), Draw.fBig, ib, new RectangleF(cx - rad, cy - 30 * S, rad * 2, 30 * S), c2);
                g.DrawString(title + "  ·  Mb/s", Draw.fSmall, sb, new RectangleF(cx - rad - 20 * S, cy + 4 * S, rad * 2 + 40 * S, 18 * S), c2);
            }
        }
        static string EnoughFor(double mbps)
        {
            if (double.IsNaN(mbps)) return L.T("4K video needs about 25 · HD 8 · a video call 4");
            if (mbps >= 25) return ((int)(mbps / 25)).ToString(CultureInfo.CurrentCulture) + " " + L.T("4K streams at once");
            if (mbps >= 8) return ((int)(mbps / 8)).ToString(CultureInfo.CurrentCulture) + " " + L.T("HD streams at once");
            return mbps >= 4 ? L.T("one video call") : L.T("audio and browsing");
        }
        float VisSpeed(Graphics g, float x, float y, float w, string state)
        {
            double down = double.NaN, up = double.NaN, idle = double.NaN, busy = double.NaN;
            var dr = VRow("Download"); if (dr != null) down = VNum(dr[2], @"([0-9.,]+)");
            var ur = VRow("Upload"); if (ur != null) up = VNum(ur[2], @"([0-9.,]+)");
            var pr = VRow("Ping"); if (pr != null) { idle = VNum(pr[2], @"([0-9.,]+)\s*ms\s*idle"); busy = VNum(pr[2], @"([0-9.,]+)\s*ms\s*while"); if (double.IsNaN(idle)) idle = VNum(pr[2], @"([0-9.,]+)"); }
            float colW = (w - 32 * S) / 3, rad = Math.Min(76 * S, colW / 2 - 40 * S);
            var r = VisCard(g, x, y, w, 64 * S + rad + 80 * S, "HOW MUCH, AND HOW QUICKLY IT ANSWERS WHEN IT'S BUSY", state);
            float gy = r.Y + 64 * S + rad;
            float c1 = r.X + 16 * S + colW / 2, c2 = c1 + colW;
            VGauge(g, c1, gy, rad, down, Theme.Blue, L.T("Download")); VGauge(g, c2, gy, rad, up, Theme.Green, L.T("Upload"));
            using (var sb = new SolidBrush(Theme.Sub)) using (var c = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(EnoughFor(down), Draw.fSmall, sb, new RectangleF(c1 - colW / 2, gy + 30 * S, colW, 34 * S), c);
                g.DrawString(double.IsNaN(up) ? L.T("matters for uploads, video calls and streaming yourself") : up >= 10 ? L.T("plenty to stream or call") : L.T("tight for streaming yourself"), Draw.fSmall, sb, new RectangleF(c2 - colW / 2, gy + 30 * S, colW, 34 * S), c);
            }
            // lag: idle against busy - the gap is what games and calls feel when someone downloads
            float bx = r.X + 16 * S + colW * 2 + 10 * S, bw = colW - 20 * S, by = r.Y + 48 * S;
            using (var ib = new SolidBrush(Theme.Ink)) using (var sb = new SolidBrush(Theme.Sub)) using (var mb = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("PING"), Draw.fTiny, mb, bx, by); by += 20 * S;
                double max = Math.Max(50, Math.Max(double.IsNaN(idle) ? 0 : idle, double.IsNaN(busy) ? 0 : busy) * 1.2);
                foreach (var row in new[] { new object[] { L.T("quiet"), idle, Theme.Blue }, new object[] { L.T("busy"), busy, Theme.Orange } })
                {
                    double v = (double)row[1]; g.DrawString((string)row[0], Draw.fSmall, sb, bx, by);
                    var tr = new RectangleF(bx + 46 * S, by + 3 * S, bw - 100 * S, 12 * S); Theme.Fill(g, tr, 6 * S, Theme.Track);
                    if (!double.IsNaN(v)) Theme.Fill(g, new RectangleF(tr.X, tr.Y, Math.Max(8 * S, tr.Width * (float)(v / max)), tr.Height), 6 * S, (Color)row[2]);
                    g.DrawString(double.IsNaN(v) ? "-" : v.ToString("0", CultureInfo.CurrentCulture) + " ms", Draw.fBodyB, ib, tr.Right + 8 * S, by - 2 * S);
                    by += 30 * S;
                }
                double gap = busy - idle; string gl; int glv;
                if (double.IsNaN(gap)) { gl = L.T("The busy number is the one games feel when someone else is downloading."); glv = 3; }
                else if (gap < 30) { gl = "+" + gap.ToString("0", CultureInfo.CurrentCulture) + " ms " + L.T("under load - fine, it stays snappy"); glv = 0; }
                else if (gap < 100) { gl = "+" + gap.ToString("0", CultureInfo.CurrentCulture) + " ms " + L.T("under load - noticeable in games and calls"); glv = 1; }
                else { gl = "+" + gap.ToString("0", CultureInfo.CurrentCulture) + " ms " + L.T("under load - lag spikes when anyone downloads (bufferbloat)"); glv = 2; }
                using (var lb = new SolidBrush(glv == 3 ? Theme.Sub : Theme.LvText(glv))) g.DrawString(gl, Draw.fSmall, lb, new RectangleF(bx, by, bw, 60 * S));
            }
            return r.Bottom + 14 * S;
        }

        // ================================================================== subnet: the bits, then the range with your addresses on it
        static bool V4Mask(string s, out uint ip, out int pre)
        {
            ip = 0; pre = -1; s = (s ?? "").Trim(); if (s == "") return false;
            string addr = s, mask = ""; int sl = s.IndexOf('/'); if (sl > 0) { addr = s.Substring(0, sl); mask = s.Substring(sl + 1); }
            else { var sp = s.Split(SpaceSep, StringSplitOptions.RemoveEmptyEntries); if (sp.Length == 2) { addr = sp[0]; mask = sp[1]; } }
            IPAddress a; if (!IPAddress.TryParse(addr.Trim(), out a) || a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            var b = a.GetAddressBytes(); ip = (uint)b[0] << 24 | (uint)b[1] << 16 | (uint)b[2] << 8 | b[3];
            mask = mask.Trim(); if (mask == "") return true;
            int n; if (int.TryParse(mask, NumberStyles.None, CultureInfo.InvariantCulture, out n)) { if (n < 0 || n > 32) return false; pre = n; return true; }
            IPAddress m; if (!IPAddress.TryParse(mask, out m)) return false; var mb = m.GetAddressBytes(); uint mv = (uint)mb[0] << 24 | (uint)mb[1] << 16 | (uint)mb[2] << 8 | mb[3];
            int c = 0; while (c < 32 && (mv & (0x80000000u >> c)) != 0) c++; pre = c; return true;
        }
        static string V4(uint v) { return (v >> 24) + "." + ((v >> 16) & 255) + "." + ((v >> 8) & 255) + "." + (v & 255); }
        float VisSubnet(Graphics g, float x, float y, float w, string state)
        {
            var a = VisIn(); uint ip; int pre; bool guess = false;
            var nr = VRow("Network");
            if (!(V4Mask(a[0], out ip, out pre) && pre >= 0) && !(nr != null && V4Mask(nr[2].Split(' ')[0], out ip, out pre) && pre >= 0))
            {
                if (V4Mask(a[0], out ip, out pre) && pre < 0) pre = 24; else { ip = 0xC0A80100u; pre = 24; guess = true; }   // nothing to go on yet: a home network as the picture
            }
            uint mask = pre == 0 ? 0 : 0xFFFFFFFFu << (32 - pre), net = ip & mask, bc = net | ~mask; long size = 1L << (32 - pre);
            var r = VisCard(g, x, y, w, 220 * S, guess ? "HOW AN ADDRESS SPLITS - E.G. A HOME NETWORK" : "HOW " + V4(ip) + "/" + pre + " SPLITS", state);
            // the 32 bits
            float bx = r.X + 18 * S, bw = r.Width - 36 * S, by = r.Y + 56 * S, gap = 6 * S, cell = (bw - 3 * gap) / 32f;
            using (var wb = new SolidBrush(Color.White)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                for (int i = 0; i < 32; i++)
                {
                    float cx = bx + i * cell + (i / 8) * gap; bool netBit = i < pre; int bit = (int)((ip >> (31 - i)) & 1);
                    Theme.Fill(g, new RectangleF(cx + 1 * S, by, cell - 2 * S, 22 * S), 3 * S, netBit ? Theme.Blue : Theme.Green);
                    if (cell > 13 * S) g.DrawString(bit.ToString(CultureInfo.InvariantCulture), Draw.fTiny, wb, new RectangleF(cx, by, cell, 22 * S), c);
                }
            using (var bb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) using (var gb = new SolidBrush(Theme.LvText(0)))
            {
                g.DrawString(L.T("network part") + ": " + pre + " " + L.T("bits") + " (" + V4(mask) + ")", Draw.fSmall, bb, bx, by + 26 * S);
                string hs = L.T("hosts") + ": " + (32 - pre) + " " + L.T("bits") + " = " + size.ToString("N0", CultureInfo.CurrentCulture) + " " + L.T("addresses"); var hsz = g.MeasureString(hs, Draw.fSmall);
                g.DrawString(hs, Draw.fSmall, gb, bx + bw - hsz.Width, by + 26 * S);
            }
            // the range, with the gateway and the address you asked about on it
            float ry = by + 82 * S, rw = bw * 0.78f; var rr = new RectangleF(bx, ry, rw, 14 * S);
            Theme.Fill(g, rr, 7 * S, Theme.LvSoft(0)); Theme.Fill(g, new RectangleF(rr.X, ry, 10 * S, 14 * S), 5 * S, Theme.Blue); Theme.Fill(g, new RectangleF(rr.Right - 10 * S, ry, 10 * S, 14 * S), 5 * S, Theme.Orange);
            using (var sb = new SolidBrush(Theme.Sub)) using (var ib = new SolidBrush(Theme.Ink))
            {
                g.DrawString(V4(net) + "  " + L.T("network"), Draw.fSmall, sb, rr.X, ry + 18 * S);
                string bl = L.T("broadcast") + "  " + V4(bc); var bs = g.MeasureString(bl, Draw.fSmall); g.DrawString(bl, Draw.fSmall, sb, rr.Right - bs.Width, ry + 18 * S);
                string ul = pre <= 30 ? L.T("usable") + " " + V4(net + 1) + " - " + V4(bc - 1) + "  (" + Math.Max(0, size - 2).ToString("N0", CultureInfo.CurrentCulture) + ")" : L.T("point-to-point: every address usable");
                using (var c = new StringFormat { Alignment = StringAlignment.Center }) g.DrawString(ul, Draw.fSmall, ib, new RectangleF(rr.X, ry + 36 * S, rw, 18 * S), c);
                Action<string, string, Color> mark = (txt, label, col) =>
                {
                    uint v; int p2; if (!V4Mask(txt, out v, out p2)) return;
                    bool inside = v >= net && v <= bc; float mx = inside ? rr.X + rw * (float)((v - net) / (double)Math.Max(1, bc - net)) : rr.Right + (bw - rw) / 2;
                    if (!inside) using (var p = new Pen(Theme.Line, 2 * S) { DashStyle = DashStyle.Dot }) g.DrawLine(p, rr.Right + 6 * S, ry + 7 * S, mx - 8 * S, ry + 7 * S);
                    Color mc = inside ? col : Theme.Red; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var mbr = new SolidBrush(mc)) { g.FillPolygon(mbr, new[] { new PointF(mx - 6 * S, ry - 12 * S), new PointF(mx + 6 * S, ry - 12 * S), new PointF(mx, ry - 2 * S) }); }
                    g.SmoothingMode = sm;
                    string t = label + " " + V4(v) + (inside ? "" : " - " + L.T("outside")); var ts = g.MeasureString(t, Draw.fSmall);
                    using (var tb = new SolidBrush(inside ? Theme.Ink : Theme.LvText(2))) g.DrawString(t, Draw.fSmall, tb, Math.Max(bx, Math.Min(bx + bw - ts.Width, mx - ts.Width / 2)), ry - 30 * S);
                };
                if (!guess) { mark(a[1], L.T("gateway"), Theme.Blue); mark(a[2], "", VPurple); }
            }
            return r.Bottom + 14 * S;
        }

        // ================================================================== MTR / MTU: the path, one stop per router
        sealed class VHop { public string N = "", Ip = "", Val = ""; public int Lv = -1; public bool Dest; }
        List<VHop> HopsNow()
        {
            var list = new List<VHop>(); bool ex = VisEx();
            if (!ex && tbSel == "mtr" && mtrHops != null)
                foreach (var h in mtrHops.ToList())
                {
                    double loss = h.Sent > 0 ? 100.0 * h.Lost / h.Sent : 0, avg = h.Sent - h.Lost > 0 ? h.Sum / (h.Sent - h.Lost) : double.NaN;
                    list.Add(new VHop { N = h.N.ToString(CultureInfo.InvariantCulture), Ip = h.Ip, Dest = h.Reached, Val = h.Ip == "" ? "" : PathTools.F(avg) + " ms · " + loss.ToString("0", CultureInfo.CurrentCulture) + "%", Lv = h.Ip == "" ? -1 : loss >= 10 ? 2 : loss >= 3 ? 1 : 0 });
                }
            else if (!ex && tbSel == "mtu" && mtuHops != null)
            {
                int prev = 99999;
                foreach (var h in mtuHops.ToList())
                {
                    bool drop = h.Mtu > 0 && prev < 99999 && h.Mtu < prev; if (h.Mtu > 0) prev = Math.Min(prev, h.Mtu);
                    list.Add(new VHop { N = h.N.ToString(CultureInfo.InvariantCulture), Ip = h.Ip, Dest = h.Reached, Val = h.Mtu > 0 ? h.Mtu.ToString(CultureInfo.InvariantCulture) : "", Lv = h.Ip == "" ? -1 : h.Mtu <= 0 ? 3 : drop || h.BlackHole ? 2 : h.Mtu < 1500 ? 1 : 0 });
                }
            }
            else
                foreach (var o in VisRows())
                {
                    var m = Regex.Match(o[1], @"^Hop\s+([0-9]+)\s*·\s*(.+)$"); if (!m.Success) continue;
                    string ipPart = m.Groups[2].Value; var ip2 = Regex.Match(ipPart, @"\(([^)]+)\)"); string shown = ip2.Success ? ipPart.Substring(0, ip2.Index).Trim() : ipPart;
                    double loss = VNum(o[2], @"loss\s*([0-9.]+)%"); string v = o[2];
                    double avg = VNum(v, @"avg\s*([0-9.]+)"); if (!double.IsNaN(avg)) v = avg.ToString("0", CultureInfo.CurrentCulture) + " ms" + (!double.IsNaN(loss) ? " · " + loss.ToString("0", CultureInfo.CurrentCulture) + "%" : "");
                    int lv = o[0] == "2" ? 2 : o[0] == "1" ? 1 : 0; if (!double.IsNaN(loss) && loss >= 3 && lv == 0) lv = 1;
                    list.Add(new VHop { N = m.Groups[1].Value, Ip = shown, Val = v, Lv = lv });
                }
            if (list.Count > 0) list[list.Count - 1].Dest = true;
            return list;
        }
        float VisHops(Graphics g, float x, float y, float w, string state)
        {
            var a = VisIn(); var hops = HopsNow(); bool idle = hops.Count == 0; string dest = a[0] != "" ? a[0] : L.T("the destination");
            if (idle) hops = new List<VHop> { new VHop { N = "1", Ip = L.T("Your router"), Val = L.T("at home") }, new VHop { N = "2", Ip = L.T("Your provider"), Val = L.T("their first router") }, new VHop { N = "…", Ip = L.T("The internet"), Val = L.T("a few more routers") }, new VHop { N = "", Ip = dest, Val = L.T("the far end"), Dest = true } };
            bool mtu = tbSel == "mtu";
            var r = VisCard(g, x, y, w, 186 * S, mtu ? "THE PATH - BIGGEST PACKET THAT GETS TO EACH ROUTER" : "THE PATH - TIME AND LOSS AT EACH ROUTER", state);
            float bx = r.X + 44 * S, bw = r.Width - 104 * S, cy = r.Y + 74 * S;
            // too many routers for the width: keep the start and the end, fold the middle
            int fit = Math.Max(4, (int)(bw / (78 * S))); var show = hops.ToList();
            if (show.Count + 1 > fit) { int keepEnd = 2, keepStart = fit - 1 - keepEnd - 1; var mid = new VHop { N = "…", Ip = (show.Count - keepStart - keepEnd) + " " + L.T("more"), Val = "", Lv = -1 }; show = show.Take(keepStart).Concat(new[] { mid }).Concat(show.Skip(show.Count - keepEnd)).ToList(); }
            int n = show.Count + 1; float step = bw / Math.Max(1, n - 1);
            using (var p = new Pen(Theme.Line, 3 * S)) g.DrawLine(p, bx, cy, bx + bw, cy);
            VNode(g, bx, cy, 17 * S, "devices", Theme.Blue, true); VLabel(g, bx, cy + 24 * S, step - 6 * S, L.T("This PC"), "", Theme.Sub);
            for (int i = 0; i < show.Count; i++)
            {
                var h = show[i]; float cx = bx + step * (i + 1); bool up = i % 2 == 1 && step < 110 * S;   // crowded: every other label above
                bool silent = h.Ip == "" && h.N != "…";
                Color c = idle ? Theme.Muted : silent ? Theme.Line : h.Lv == 3 ? Theme.Blue : VLv(h.Lv);
                if (h.N == "…") { using (var mb = new SolidBrush(Theme.Muted)) using (var cf = new StringFormat { Alignment = StringAlignment.Center }) g.DrawString("• • •", Draw.fBodyB, mb, new RectangleF(cx - 30 * S, cy - 10 * S, 60 * S, 20 * S), cf); }
                else if (h.Dest) VNode(g, cx, cy, 17 * S, "dest", c, !idle);
                else
                {
                    float rad = silent ? 7 * S : 13 * S; var cr = new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2);
                    var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
                    if (idle) { Theme.Fill(g, cr, rad, Theme.Card); Theme.Stroke(g, cr, rad, c, 2 * S); } else Theme.Fill(g, cr, rad, c);
                    g.SmoothingMode = sm;
                    if (!silent) using (var tb = new SolidBrush(idle ? Theme.Sub : Color.White)) using (var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(h.N, Draw.fTiny, tb, cr, cf);
                }
                string ipl = silent ? L.T("no answer") : h.Ip, val = h.Val;
                float lw = Math.Max(70 * S, step * (step < 110 * S ? 1.9f : 1f) - 6 * S);
                if (up) using (var ib = new SolidBrush(Theme.Ink)) using (var sb = new SolidBrush(h.Lv >= 1 && h.Lv <= 2 ? Theme.LvText(h.Lv) : Theme.Sub)) using (var cf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    {
                        g.DrawString(val, Draw.fSmall, sb, new RectangleF(cx - lw / 2, cy - 40 * S, lw, 16 * S), cf);
                        g.DrawString(Draw.Trunc(ipl, 22), Draw.fSmall, ib, new RectangleF(cx - lw / 2, cy - 58 * S, lw, 16 * S), cf);
                    }
                else using (var ib = new SolidBrush(silent ? Theme.Muted : Theme.Ink)) using (var sb = new SolidBrush(h.Lv >= 1 && h.Lv <= 2 ? Theme.LvText(h.Lv) : Theme.Sub)) using (var cf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    {
                        g.DrawString(Draw.Trunc(ipl, 22), Draw.fSmall, ib, new RectangleF(cx - lw / 2, cy + 24 * S, lw, 16 * S), cf);
                        g.DrawString(val, Draw.fSmall, sb, new RectangleF(cx - lw / 2, cy + 41 * S, lw, 16 * S), cf);
                    }
            }
            string foot = mtu ? L.T("Every router has a biggest packet it passes. The smallest one on the path is the path's MTU - a drop shows where a tunnel or PPPoE eats room.") : L.T("Loss at a middle router that doesn't carry on to the end isn't real - that router just doesn't bother answering pings. Loss that carries on to the end is.");
            using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(foot, Draw.fSmall, sb, new RectangleF(r.X + 18 * S, r.Bottom - 40 * S, r.Width - 36 * S, 36 * S));
            return r.Bottom + 14 * S;
        }

        // ================================================================== port test: the knock, the wall, the door
        float VisPort(Graphics g, float x, float y, float w, string state)
        {
            var a = VisIn(); string target = a[0] != "" ? a[0] : "host:port"; string t = VisText().ToLowerInvariant();
            int outcome = tbRows == null ? -1 : Regex.IsMatch(t, @"\bopen\b") ? 0 : t.Contains("refused") ? 1 : t.Contains("no answer") || t.Contains("dropped") ? 2 : VisLevel() == 2 ? 2 : -1;
            var r = VisCard(g, x, y, w, 190 * S, "KNOCKING ON ONE DOOR", state);
            float cy = r.Y + 70 * S, x0 = r.X + 70 * S, x2 = r.Right - 120 * S, x1 = (x0 + x2) / 2;
            VNode(g, x0, cy, 20 * S, "devices", Theme.Blue, true); VLabel(g, x0, cy + 26 * S, 140 * S, L.T("This PC"), "", Theme.Sub);
            VNode(g, x1, cy, 20 * S, "shield", outcome == 2 ? Theme.Red : outcome >= 0 ? Theme.Green : Theme.Muted, outcome >= 0); VLabel(g, x1, cy + 26 * S, 200 * S, L.T("Firewalls on the way"), outcome == 2 ? L.T("something dropped it here - or at theirs") : outcome >= 0 ? L.T("let it through") : L.T("yours, your network's, theirs"), outcome == 2 ? Theme.LvText(2) : Theme.Sub);
            VNode(g, x2, cy, 20 * S, outcome == 1 ? "lock" : "dest", outcome == 0 ? Theme.Green : outcome == 1 ? Theme.Red : Theme.Muted, outcome == 0 || outcome == 1);
            VLabel(g, x2, cy + 26 * S, 200 * S, target, outcome == 0 ? L.T("open - something answered") : outcome == 1 ? L.T("refused - nothing listens there") : outcome == 2 ? L.T("never heard back") : L.T("the service you're trying"), outcome == 0 ? Theme.LvText(0) : outcome == 1 ? Theme.LvText(2) : Theme.Sub);
            VArrow(g, x0 + 26 * S, cy, x1 - 26 * S, cy, outcome == -1 ? Theme.Line : Theme.Blue, outcome == -1);
            if (outcome == 2)
                using (var p = new Pen(Theme.Red, 3 * S)) { float mx = (x1 + x2) / 2; g.DrawLine(p, mx - 8 * S, cy - 8 * S, mx + 8 * S, cy + 8 * S); g.DrawLine(p, mx - 8 * S, cy + 8 * S, mx + 8 * S, cy - 8 * S); }
            else VArrow(g, x1 + 26 * S, cy, x2 - 26 * S, cy, outcome == -1 ? Theme.Line : outcome == 1 ? Theme.Red : Theme.Green, outcome == -1);
            using (var sb = new SolidBrush(Theme.Sub))
                g.DrawString(L.T("Three answers, three different problems: open = fine;  refused = the server's there but that service isn't;  no answer = a firewall swallowed it - try from mobile data to see whose."), Draw.fSmall, sb, new RectangleF(r.X + 18 * S, r.Bottom - 40 * S, r.Width - 36 * S, 36 * S));
            return r.Bottom + 14 * S;
        }

        // ================================================================== Wi-Fi link rate: the number on the label vs what you'll download
        float VisRate(Graphics g, float x, float y, float w, string state)
        {
            double link = double.NaN, lo = double.NaN, hi = double.NaN;
            var lr = VRow("Link rate"); if (lr != null) link = VNum(lr[2], @"([0-9.,]+)");
            var rr = VRow("Real"); if (rr != null) { lo = VNum(rr[2], @"([0-9.,]+)\s*-"); hi = VNum(rr[2], @"-\s*([0-9.,]+)"); if (double.IsNaN(lo)) lo = hi = VNum(rr[2], @"([0-9.,]+)"); }
            bool have = !double.IsNaN(link);
            var r = VisCard(g, x, y, w, 190 * S, "THE LINK RATE IS NOT YOUR DOWNLOAD SPEED", state);
            float bx = r.X + 140 * S, bw = r.Width - 160 * S - 110 * S, by = r.Y + 50 * S;
            double max = have ? link : 100;
            using (var ib = new SolidBrush(Theme.Ink)) using (var sb = new SolidBrush(Theme.Sub))
            {
                g.DrawString(L.T("Link rate"), Draw.fBodyB, ib, r.X + 18 * S, by); var b1 = new RectangleF(bx, by + 3 * S, bw, 16 * S);
                Theme.Fill(g, b1, 8 * S, have ? Theme.Blue : Theme.Track); g.DrawString(have ? link.ToString("N0", CultureInfo.CurrentCulture) + " Mb/s" : "100%", Draw.fBodyB, ib, b1.Right + 10 * S, by);
                g.DrawString(L.T("what the radio talks at, both ways, shared"), Draw.fSmall, sb, bx, by + 22 * S);
                by += 52 * S;
                g.DrawString(L.T("Real downloads"), Draw.fBodyB, ib, r.X + 18 * S, by); var b2 = new RectangleF(bx, by + 3 * S, bw, 16 * S); Theme.Fill(g, b2, 8 * S, Theme.Track);
                double l0 = have && !double.IsNaN(lo) ? lo : max * 0.55, h0 = have && !double.IsNaN(hi) ? hi : max * 0.7;
                Theme.Fill(g, new RectangleF(bx, b2.Y, (float)(bw * l0 / max), 16 * S), 8 * S, have ? Theme.Green : Theme.Line);
                Theme.Fill(g, new RectangleF(bx + (float)(bw * l0 / max) - 8 * S, b2.Y, Math.Max(10 * S, (float)(bw * (h0 - l0) / max) + 8 * S), 16 * S), 8 * S, have ? Theme.LvSoft(0) : Theme.Soft);
                g.DrawString(have && !double.IsNaN(lo) ? lo.ToString("N0", CultureInfo.CurrentCulture) + "-" + hi.ToString("N0", CultureInfo.CurrentCulture) : "55-70%", Draw.fBodyB, ib, b2.Right + 10 * S, by);
                g.DrawString(L.T("after Wi-Fi's own overhead - close to the router, nobody else busy"), Draw.fSmall, sb, bx, by + 22 * S);
            }
            using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(L.T("Walls, distance and every other device on the same channel take their share of what's left."), Draw.fSmall, sb, new RectangleF(r.X + 18 * S, r.Bottom - 28 * S, r.Width - 36 * S, 20 * S));
            return r.Bottom + 14 * S;
        }

        // ================================================================== Wi-Fi roams: a day, with every move between access points on it
        float VisRoams(Graphics g, float x, float y, float w, string state)
        {
            var evs = new List<object[]>();
            foreach (var o in VisRows())
            {
                var m = Regex.Match(o[1], @"^([0-9]{1,2}):([0-9]{2})$"); if (!m.Success) continue;
                double hr = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) + int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) / 60.0;
                bool drop = o[2].IndexOf("drop", StringComparison.OrdinalIgnoreCase) >= 0 || o[2].IndexOf("lost", StringComparison.OrdinalIgnoreCase) >= 0 || o[0] == "2";
                var sg = Regex.Match(o[2], @"([0-9]+%\s*→\s*[0-9]+%)");
                evs.Add(new object[] { hr, o[1], drop, sg.Success ? sg.Groups[1].Value : "" });
            }
            var r = VisCard(g, x, y, w, 172 * S, "THE LAST DAY - EVERY MOVE BETWEEN ACCESS POINTS", state);
            float bx = r.X + 28 * S, bw = r.Width - 56 * S, ay = r.Y + 96 * S;
            using (var p = new Pen(Theme.Line, 3 * S)) g.DrawLine(p, bx, ay, bx + bw, ay);
            using (var mb = new SolidBrush(Theme.Muted)) using (var c = new StringFormat { Alignment = StringAlignment.Center })
                for (int h = 0; h <= 24; h += 6) { float tx = bx + bw * h / 24f; using (var tp = new Pen(Theme.Line2, 1 * S)) g.DrawLine(tp, tx, ay - 4 * S, tx, ay + 4 * S); g.DrawString(h == 24 ? "24:00" : h.ToString("00", CultureInfo.InvariantCulture) + ":00", Draw.fTiny, mb, new RectangleF(tx - 30 * S, ay + 8 * S, 60 * S, 14 * S), c); }
            if (evs.Count == 0)
                using (var sb = new SolidBrush(Theme.Sub)) using (var c = new StringFormat { Alignment = StringAlignment.Center })
                    g.DrawString(tbRows == null ? L.T("Each move to a stronger access point lands here as a blue mark; a drop is red.") : L.T("No moves - this PC stayed on one access point."), Draw.fSmall, sb, new RectangleF(bx, ay - 34 * S, bw, 18 * S), c);
            int k = 0;
            foreach (var e in evs)
            {
                float ex = bx + bw * (float)((double)e[0] / 24.0); bool drop = (bool)e[2]; bool up = k++ % 2 == 0;
                var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
                Theme.Fill(g, new RectangleF(ex - 7 * S, ay - 7 * S, 14 * S, 14 * S), 7 * S, drop ? Theme.Red : Theme.Blue);
                g.SmoothingMode = sm;
                string lab = (string)e[1] + ((string)e[3] != "" ? "  " + e[3] : drop ? "  " + L.T("drop") : "");
                var ls = g.MeasureString(lab, Draw.fSmall); float lx = Math.Max(bx, Math.Min(bx + bw - ls.Width, ex - ls.Width / 2));
                using (var tb = new SolidBrush(drop ? Theme.LvText(2) : Theme.Ink)) g.DrawString(lab, Draw.fSmall, tb, lx, up ? ay - 30 * S : ay - 50 * S);
            }
            using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(L.T("A few moves a day is healthy - it means your PC hops to the stronger access point. Many moves, or drops, mean the access points overlap badly."), Draw.fSmall, sb, new RectangleF(r.X + 18 * S, r.Bottom - 36 * S, r.Width - 36 * S, 34 * S));
            return r.Bottom + 14 * S;
        }
    }
}
