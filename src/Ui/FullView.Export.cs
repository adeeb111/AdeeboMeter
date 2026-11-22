// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== take any picture with you (Adeeb, 15 Nov)
    // Under every tool picture: Save picture (a PNG, with a small "made with AdeeboMeter" footer), Copy picture (straight into a
    // chat or a mail) and Open on my phone - a QR code whose link carries the picture's own data and the result rows to
    // adeebometer.com/r, where the phone redraws it with Copy, Share, Save image and PDF (docs/r.html). Shown as it is: nothing hidden.
    sealed partial class FullView
    {
        // what the last picture drew, by family - the phone page draws the same family from it
        static string PhoneKind = "";
        static Tuple<string, string[], List<XStep>> PhoneX; static Tuple<string, List<Tuple<string, double, double, int>>> PhoneR;
        static Tuple<string, List<Tuple<string, string, double, string, int>>> PhoneP; static Tuple<string, string, List<Tuple<string, double, string, int>>> PhoneB;

        // everything a closed window must let go of: a running WinForms timer keeps a hidden window (a USER object) and, through
        // its Tick handler, the whole closed view alive - the QA's "+71 USER, +360 GDI, 128 MB over 30 opens" on real Windows
        void ReleaseExtras()
        {
            foreach (var t in new[] { animTimer, calcTimer, jTimer, labTimer, sparkT, switchTimer }) if (t != null) { t.Stop(); t.Dispose(); }
            animTimer = calcTimer = null; jTimer = labTimer = sparkT = switchTimer = null;
            if (qrBmp != null) { qrBmp.Dispose(); qrBmp = null; }
            foreach (var b in navBmps.Values) b.Dispose(); navBmps.Clear();
            foreach (var b in wpics.Values) if (b != null) b.Dispose(); wpics.Clear();   // AM-CG-210: the widget previews too
            PhoneX = null; PhoneR = null; PhoneP = null; PhoneB = null; PhoneKind = "";   // statics: don't keep the last view's data alive
        }
        // written whole beside the target under a name nobody else uses, then swapped in; never left behind
        internal static void WriteSafe(string path, string text, bool bom)
        {
            string tmp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp");
            try { File.WriteAllText(tmp, text, new UTF8Encoding(bom)); if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path); }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        // a short note beside the links after a save or a copy
        string barNote = ""; DateTime barAt;
        void Toast(string t) { barNote = t; barAt = DateTime.Now; tbCanvas.Invalidate(); }
        // 1.1.6: the far end a tool measured, for the Network Lab - the go-live check's ingest server, or the service board's
        // worst host:port (a down one first). null when the result has none.
        string[] LabRow()
        {
            var rows = tbRows; if (rows == null) return null;
            if (tbSel == "golive") return rows.FirstOrDefault(r => r.Length > 2 && r[1].EndsWith(" ingest", StringComparison.Ordinal));
            if (tbSel == "board") return rows.Where(r => r.Length > 2 && r[1].Contains(":") && r[2] != "not a host name or address").OrderByDescending(r => r[0]).FirstOrDefault();
            return null;
        }
        void ToolPath()
        {
            var r = LabRow(); if (r == null) return;
            var mm = System.Text.RegularExpressions.Regex.Match(r[2], @"([0-9]+(?:[.,][0-9]+)?)\s*ms"); double ms;
            if (!mm.Success || !double.TryParse(mm.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out ms)) ms = double.NaN;
            bool live = tbSel == "golive"; string name = live ? r[1].Replace(" ingest", "") + " ingest" : "Your server", host = live ? "" : r[1];
            OpenLab(LabLink.PathJson(M, live ? "stream" : "server", host, name, ms, MinOf(M.GwPing), MinOf(M.NetPing)), false);
        }
        // the link bar under a picture
        float PaintPictureBar(Graphics g, float x, float y, float w)
        {
            if (visRect.IsEmpty || tbBusy) return y;
            float lx = x + 4 * S; var col = Theme.Dark ? Theme.BlueDark : Theme.Blue;
            Action<string, string, Action> link = (icon, t, a) =>
            {
                var ts = g.MeasureString(L.T(t), Draw.fBodyB); if (lx > x + 4 * S && lx + ts.Width + 24 * S > x + w) { lx = x + 4 * S; y += 26 * S; }
                Kit.Icon(g, icon, new RectangleF(lx, y + 2 * S, 16 * S, 16 * S), col);
                using (var b = new SolidBrush(col)) g.DrawString(L.T(t), Draw.fBodyB, b, lx + 20 * S, y);
                Act(tbCanvas, new RectangleF(lx, y - 2 * S, ts.Width + 24 * S, ts.Height + 4 * S), a); lx += ts.Width + 46 * S;
            };
            link("report", "Save picture ...", SaveMenu); link("plus", "Copy picture", CopyPicture); link("phone", "Open on my phone", OpenOnPhone);
            if (!VisEx() && tbRows != null && (tbSel == "golive" || tbSel == "board") && LabRow() != null) link("route", "Show my path", ToolPath);
            if (!VisEx() && Cfg.History && tbRows != null && tbRows.Any(r => r.Length > 0 && r[0] == "2"))   // a red result: that minute in the Time machine (FullView.Gateway.cs)
            { DateTime at = tbAt; string tt = tbTitle; link("time", L.T("See") + " " + at.ToString("HH:mm", CultureInfo.CurrentCulture) + " " + L.T("in the Time machine"), delegate { OpenTimeAt(at, L.T("Toolbox") + " - " + tt); }); }   // 1.1.6: the Network Lab if (File.Exists(Path.Combine(Environment.SystemDirectory, "fsquirt.exe"))) link("devices", "Picture by Bluetooth (PNG)", SendBluetooth); link("lock", "Encrypt picture, then send", EncryptPicture); if (QuickShareExe() != null) link("go", "Picture to Quick Share (PNG)", SendQuickShare);
            if (barNote == "" || (DateTime.Now - barAt).TotalSeconds >= 8) { string kh = L.T("Keys: Ctrl+Shift+S save · Ctrl+Shift+C copy · Ctrl+Shift+Q phone"); var ks = g.MeasureString(kh, Draw.fTiny); if (lx + ks.Width > x + w) { lx = x + 4 * S; y += 26 * S; } using (var b = new SolidBrush(Theme.Muted)) g.DrawString(kh, Draw.fTiny, b, lx, y + 4 * S); }
            if (barNote != "" && (DateTime.Now - barAt).TotalSeconds < 8)
            {
                var ns = g.MeasureString(barNote, Draw.fSmall); if (lx + ns.Width > x + w) { lx = x + 4 * S; y += 26 * S; }
                using (var b = new SolidBrush(Theme.LvText(0))) g.DrawString(barNote, Draw.fSmall, b, new RectangleF(lx, y + 2 * S, x + w - lx, 40 * S));
            }
            return y + 30 * S;
        }

        // keyboard: the links under a picture (Ctrl+Shift+S save, C copy, Q phone)
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (tbCanvas != null && tbCanvas.Visible && !visRect.IsEmpty && !tbBusy)
            {
                if (keyData == (Keys.Control | Keys.Shift | Keys.S)) { SaveMenuAt(tbCanvas.PointToScreen(new Point((int)(20 * S), (int)Math.Min(tbCanvas.Height - 40 * S, visRect.Bottom)))); return true; }
                if (keyData == (Keys.Control | Keys.Shift | Keys.C)) { CopyPicture(); return true; }
                if (keyData == (Keys.Control | Keys.Shift | Keys.Q)) { OpenOnPhone(); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        // the picture alone, drawn again into a bitmap with a footer
        Bitmap PictureBitmap()
        {
            float w = 860 * S; var size = new Size((int)(w + 40 * S), 4000);
            using (var probe = new Bitmap(10, 10)) using (var pg = Graphics.FromImage(probe)) { Prep(pg); float h0 = PaintToolVisual0(pg, 20 * S, 20 * S, w); size.Height = (int)(h0 + 40 * S); }
            var bmp = new Bitmap(size.Width, Math.Max(100, size.Height));
            using (var g = Graphics.FromImage(bmp))
            {
                Prep(g); g.Clear(Theme.Bg); PaintToolVisual0(g, 20 * S, 20 * S, w);
                string foot = L.T("made with AdeeboMeter") + " · adeebometer.com · " + DateTime.Now.ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture);
                using (var b = new SolidBrush(Theme.Muted)) { var fs = g.MeasureString(foot, Draw.fTiny); g.DrawString(foot, Draw.fTiny, b, new RectangleF(Math.Max(20 * S, bmp.Width - fs.Width - 20 * S), bmp.Height - fs.Height - 10 * S, bmp.Width - 40 * S, fs.Height + 2 * S)); }
            }
            tbCanvas.Invalidate();   // the click areas belong to the screen again
            return bmp;
        }
        string ToolName() { var t = Tools2().FirstOrDefault(z => z.Id == tbSel); return t == null ? tbSel : t.Title; }
        void SavePicture()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AdeeboMeter"); Directory.CreateDirectory(dir);
                using (var bmp = PictureBitmap())
                using (var d = new SaveFileDialog { Filter = "PNG picture|*.png", InitialDirectory = dir, FileName = tbSel + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".png" })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    bmp.Save(d.FileName, ImageFormat.Png); Toast(L.T("Saved") + " " + Path.GetFileName(d.FileName));
                }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("save picture: " + e.Message); Toast(L.T("Couldn't save the picture")); }
        }
        // Save picture ...: PNG, a draw.io diagram (real boxes and arrows to edit), or the data behind it (JSON, CSV)
        void SaveMenu() { SaveMenuAt(Cursor.Position); }
        void SaveMenuAt(Point at)
        {
            var m = new ContextMenuStrip { Font = Draw.fBody };
            m.Items.Add(L.T("Picture (PNG)"), null, delegate { SavePicture(); });
            m.Items.Add(L.T("Diagram for draw.io / diagrams.net (.drawio) - move and restyle every box"), null, delegate { SaveText("drawio", "draw.io diagram|*.drawio", DrawIo); });
            m.Items.Add(L.T("Data (JSON) - for scripts and other tools"), null, delegate { SaveText("json", "JSON|*.json", Json); });
            m.Items.Add(L.T("Data (CSV) - opens in Excel"), null, delegate { SaveText("csv", "CSV|*.csv", Csv); });
            m.Closed += delegate { BeginInvoke((Action)m.Dispose); };
            m.Show(at); if (m.Items.Count > 0) m.Items[0].Select();
        }
        void SaveText(string ext, string filter, Func<string> make)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AdeeboMeter"); Directory.CreateDirectory(dir);
                using (var d = new SaveFileDialog { Filter = filter, InitialDirectory = dir, FileName = tbSel + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "." + ext })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    WriteSafe(d.FileName, make(), ext == "csv"); Toast(L.T("Saved") + " " + Path.GetFileName(d.FileName) + (ext == "drawio" ? " - open it at app.diagrams.net or in draw.io" : ""));
                }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("save " + ext + ": " + e.Message); Toast(L.T("Couldn't save it")); }
        }
        // draw the picture once off-screen so PhoneKind and its data describe what's on screen now
        void CapturePicture()
        {
            try { using (var probe = new Bitmap(10, 10)) using (var pg = Graphics.FromImage(probe)) PaintToolVisual0(pg, 0, 0, 900 * S); }
            catch (Exception e) { if (e is OutOfMemoryException) throw; PhoneKind = ""; }
            tbCanvas.Invalidate();
        }
        // AM-CG-220: every share path (picture, phone link, CSV, JSON, text) starts from ShareRows, which never carries a secret row
        // AM-CG-224: shared copies show a web address as origin and path - the query and fragment (where tokens live) only stay on screen
        List<string[]> ShareRows() { return (tbRows ?? new List<string[]>()).Where(o => o != null && o.Length > 2 && !IsSecretRow(o)).Select(o => o.Select((c, i) => i >= 1 && i <= 3 ? ShareCell(c) : c).ToArray()).ToList(); }
        static readonly Regex UrlTail = new Regex(@"(https?://[^\s?#""'<>]+)([?#][^\s""'<>]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        internal static string ShareCell(string c) { return string.IsNullOrEmpty(c) ? c : UrlTail.Replace(c, m => m.Groups[1].Value + (m.Groups[2].Value.StartsWith("#", StringComparison.Ordinal) ? "#…" : "?…")); }
        List<string[]> ResRows() { return ShareRows().Where(o => o[0] != "-2").ToList(); }

        // ---------- draw.io: an uncompressed mxGraph file - every actor, step, hop, lane and row a separate shape
        static string Xa(string s) { return System.Security.SecurityElement.Escape(s ?? "") ?? ""; }
        // a label as draw.io wants it with html=1: each line HTML-escaped, lines joined by <br>, then escaped for the XML attribute
        static string Hv(string v) { return Xa(string.Join("<br>", (v ?? "").Split('\n').Select(System.Net.WebUtility.HtmlEncode).ToArray())); }
        static string LvFill(int lv) { return lv == 2 ? "#FDE7E9" : lv == 1 ? "#FFF4D6" : lv == 0 ? "#E6F4EA" : "#EEF1F5"; }
        static string LvLine(int lv) { return lv == 2 ? "#CF222E" : lv == 1 ? "#BF8700" : lv == 0 ? "#2DA44E" : "#6B7685"; }
        internal string DrawIo()
        {
            CapturePicture(); var sb = new StringBuilder(); int id = 2; var ic = CultureInfo.InvariantCulture;
            Action<string, string, double, double, double, double> box = (v, style, x, y, w, h) => sb.Append("<mxCell id=\"c" + (id++) + "\" value=\"" + Hv(v) + "\" style=\"" + style + "\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"" + x.ToString("0", ic) + "\" y=\"" + y.ToString("0", ic) + "\" width=\"" + w.ToString("0", ic) + "\" height=\"" + h.ToString("0", ic) + "\" as=\"geometry\"/></mxCell>");
            Action<string, string, double, double, double, double> line = (v, style, x1, y1, x2, y2) => sb.Append("<mxCell id=\"c" + (id++) + "\" value=\"" + Hv(v) + "\" style=\"" + style + "\" edge=\"1\" parent=\"1\"><mxGeometry relative=\"1\" as=\"geometry\"><mxPoint x=\"" + x1.ToString("0", ic) + "\" y=\"" + y1.ToString("0", ic) + "\" as=\"sourcePoint\"/><mxPoint x=\"" + x2.ToString("0", ic) + "\" y=\"" + y2.ToString("0", ic) + "\" as=\"targetPoint\"/></mxGeometry></mxCell>");
            const string Txt = "text;html=1;align=left;verticalAlign=top;whiteSpace=wrap;fontSize=12;";
            string title = ToolName(); double y0 = 70;
            box(title, "text;html=1;fontSize=20;fontStyle=1;align=left;", 20, 10, 700, 30);
            switch (PhoneKind)
            {
                case "x":
                    {
                        var act = PhoneX.Item2; var st = PhoneX.Item3; double gap = Math.Min(200, act.Length > 1 ? (1020.0 - 160) / (act.Length - 1) : 200),   /* L-333: all actors fit the 1100-wide page */ top = y0 + 20, rowH = 70, bottom = top + 50 + st.Count * rowH;
                        box(PhoneX.Item1, Txt + "fontColor=#6B7685;", 20, y0 - 20, 700, 20);
                        for (int i = 0; i < act.Length; i++) { box(act[i], "rounded=1;whiteSpace=wrap;html=1;fillColor=#EEF1F5;strokeColor=#E3E8EF;fontStyle=1;", 40 + i * gap, top, 160, 36); line("", "endArrow=none;dashed=1;html=1;strokeColor=#B4BDC9;", 120 + i * gap, top + 36, 120 + i * gap, bottom); }
                        for (int i = 0; i < st.Count; i++)
                        {
                            var s = st[i]; int to = s.All ? act.Length - 1 : s.To; double y = top + 70 + i * rowH, x1 = 120 + s.From * gap, x2 = 120 + to * gap; string c = s.Lv == 2 ? "#CF222E" : s.Lv == 1 ? "#BF8700" : to < s.From ? "#2DA44E" : "#1F6FEB";
                            string v = (s.Sealed ? "\U0001F512 " : "") + s.Label + (s.Detail != "" ? "\n" + s.Detail : "") + (double.IsNaN(s.Ms) ? "" : "  (" + Nm(s.Ms) + " ms)");
                            if (s.From == to) box(v, Txt + "fontColor=" + c + ";", x1 + 10, y - 18, 300, 40);
                            else line(v, "endArrow=block;endFill=1;html=1;strokeWidth=2;strokeColor=" + c + ";fontSize=11;labelBackgroundColor=#FFFFFF;" + (s.All ? "dashed=1;" : ""), x1, y, x2, y);
                        }
                        y0 = bottom + 20;
                    }
                    break;
                case "p":
                    {
                        int prevHop = 0;
                        var hops = PhoneP.Item2; var owners = hops.Select(h => h.Item4).Distinct().ToList(); string[] pal = { "#E8F0FE", "#F1EAFE", "#FFF4D6", "#E6F4EA", "#EEF1F5" };
                        box(PhoneP.Item1, Txt + "fontColor=#6B7685;", 20, y0 - 20, 700, 20);
                        for (int i = 0; i < hops.Count; i++)
                        {
                            var h = hops[i]; double x = 40 + (i % 7) * 150, yR = y0 + (i / 7) * 190;   // bug-hunt L-333: 7 hops a row, then the next row - never past the page
                            int me = id; if (i > 0) sb.Append("<mxCell id=\"c" + (id++) + "\" style=\"endArrow=block;html=1;strokeWidth=3;strokeColor=#B4BDC9;\" edge=\"1\" parent=\"1\" source=\"c" + prevHop + "\" target=\"c" + (me + 1) + "\"><mxGeometry relative=\"1\" as=\"geometry\"/></mxCell>");
                            prevHop = id; box(h.Item1, "ellipse;whiteSpace=wrap;html=1;fontStyle=1;fillColor=" + LvFill(h.Item5) + ";strokeColor=" + LvLine(h.Item5) + ";strokeWidth=2;", x + 10, yR + 35, 50, 50);
                            box(h.Item2 + (double.IsNaN(h.Item3) ? "" : "\n" + Nm(h.Item3) + " ms") + "\n" + h.Item4, "rounded=1;whiteSpace=wrap;html=1;fontSize=11;fillColor=" + pal[owners.IndexOf(h.Item4) % pal.Length] + ";strokeColor=none;", x - 20, yR + 95, 130, 70);
                        }
                        y0 += Math.Max(1, (hops.Count + 6) / 7) * 190;
                    }
                    break;
                case "r": case "b":
                    {
                        var lanes = PhoneKind == "r" ? PhoneR.Item2.Select(l => Tuple.Create(l.Item1, l.Item2, Nm(l.Item2) + " ms" + (double.IsNaN(l.Item3) ? "" : " / " + Nm(l.Item3) + " ms"), l.Item4)).ToList() : PhoneB.Item3.Select(b => Tuple.Create(b.Item1, b.Item2, Nm(b.Item2) + " " + PhoneB.Item2 + (b.Item3 != "" ? " · " + b.Item3 : ""), b.Item4)).ToList();
                        box(PhoneKind == "r" ? PhoneR.Item1 : PhoneB.Item1, Txt + "fontColor=#6B7685;", 20, y0 - 20, 700, 20);
                        double max = Math.Max(1, lanes.Select(l => double.IsNaN(l.Item2) ? 0 : l.Item2).DefaultIfEmpty(1).Max()); if (PhoneKind == "r") max = Math.Max(max, PhoneR.Item2.Select(l => double.IsNaN(l.Item3) ? 0 : l.Item3).DefaultIfEmpty(0).Max());
                        for (int i = 0; i < lanes.Count; i++)
                        {
                            var l = lanes[i]; double y = y0 + 20 + i * 40, w = double.IsNaN(l.Item2) ? 4 : Math.Max(4, 400 * l.Item2 / max);
                            box(l.Item1, Txt + "fontStyle=1;", 20, y, 200, 26); box("", "rounded=1;html=1;fillColor=" + LvLine(l.Item4 < 0 ? -9 : l.Item4).Replace("#6B7685", "#1F6FEB") + ";strokeColor=none;", 230, y + 4, w, 18); box(l.Item3, Txt + "fontColor=#4A5568;", 240 + w, y, 260, 26);
                            if (PhoneKind == "r" && !double.IsNaN(PhoneR.Item2[i].Item3)) box("", "rounded=1;html=1;fillColor=#8250DF;strokeColor=none;", 230, y + 24, Math.Max(4, 400 * PhoneR.Item2[i].Item3 / max), 6);
                        }
                        y0 += 40 + lanes.Count * 40;
                    }
                    break;
                default:
                    if (!visRect.IsEmpty)
                        using (var bmp = PictureBitmap()) using (var ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Png); double iw = 700, ih = Math.Round(700.0 * bmp.Height / Math.Max(1, bmp.Width));
                            box("", "shape=image;html=1;imageAspect=0;aspect=fixed;image=data:image/png," + Convert.ToBase64String(ms.ToArray()) + ";", 20, y0, iw, ih); y0 += ih + 10;
                        }
                    break;
            }
            // the result rows, as a column of cards
            var rows = ResRows(); double ry = y0 + 10;
            if (rows.Count > 0) box(L.T("THE RESULT"), Txt + "fontStyle=1;fontColor=#6B7685;", 20, ry, 300, 20); ry += 26;
            if (rows.Count > 40) { box((rows.Count - 40) + " " + L.T("more rows not drawn - every row is in the JSON and CSV"), Txt + "fontColor=#6B7685;", 20, ry, 700, 20); ry += 24; }
            foreach (var o in rows.Take(40))
            {
                int lv; if (!int.TryParse(o[0], out lv)) lv = 3;
                if (lv == -1) { box(o[1], Txt + "fontStyle=1;fontColor=#6B7685;", 20, ry + 6, 700, 20); ry += 30; continue; }
                double ch = 30 + 16 * Math.Ceiling(Math.Max(1, (o[1].Length + o[2].Length) / 95.0)); box(o[1] + "\n" + o[2], "rounded=1;whiteSpace=wrap;html=1;align=left;spacingLeft=10;fontSize=12;fillColor=" + LvFill(lv) + ";strokeColor=" + LvLine(lv) + ";", 20, ry, 700, ch); ry += ch + 8;
            }
            box(L.T("made with AdeeboMeter") + " · adeebometer.com · " + DateTime.Now.ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture), Txt + "fontSize=10;fontColor=#6B7685;", 20, ry + 10, 500, 20);
            return "<mxfile host=\"AdeeboMeter\"><diagram name=\"" + Xa(title) + "\"><mxGraphModel dx=\"1000\" dy=\"800\" grid=\"1\" gridSize=\"10\" page=\"1\" pageWidth=\"1100\" pageHeight=\"850\"><root><mxCell id=\"0\"/><mxCell id=\"1\" parent=\"0\"/>" + sb + "</root></mxGraphModel></diagram></mxfile>";
        }

        // ---------- the data: JSON (the picture's own data + every row) and CSV (the rows)
        static string Js(string s)
        {
            var sb = new StringBuilder("\""); foreach (char c in s ?? "") { if (c == '"' || c == '\\') sb.Append('\\').Append(c); else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); else sb.Append(c); }
            return sb.Append('"').ToString();
        }
        static string Jn(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "null" : v.ToString("0.###", CultureInfo.InvariantCulture); }
        internal string Json()
        {
            CapturePicture(); var sb = new StringBuilder("{\n");
            sb.Append("  \"tool\": " + Js(tbSel) + ",\n  \"title\": " + Js(ToolName()) + ",\n  \"made\": " + Js(DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)) + ",\n  \"state\": " + Js(VisState()) + ",\n");
            sb.Append("  \"inputs\": [" + string.Join(", ", VisIn().Select(Js).ToArray()) + "],\n  \"picture\": ");
            switch (PhoneKind)
            {
                case "x": sb.Append("{ \"kind\": \"exchange\", \"caption\": " + Js(PhoneX.Item1) + ", \"actors\": [" + string.Join(", ", PhoneX.Item2.Select(Js).ToArray()) + "], \"steps\": [\n" + string.Join(",\n", PhoneX.Item3.Select(s => "    { \"from\": " + s.From + ", \"to\": " + (s.All ? PhoneX.Item2.Length - 1 : s.To) + ", \"broadcast\": " + (s.All ? "true" : "false") + ", \"sealed\": " + (s.Sealed ? "true" : "false") + ", \"level\": " + s.Lv + ", \"label\": " + Js(s.Label) + ", \"detail\": " + Js(s.Detail) + ", \"ms\": " + Jn(s.Ms) + " }").ToArray()) + "\n  ] }"); break;
                case "r": sb.Append("{ \"kind\": \"race\", \"caption\": " + Js(PhoneR.Item1) + ", \"lanes\": [\n" + string.Join(",\n", PhoneR.Item2.Select(l => "    { \"name\": " + Js(l.Item1) + ", \"ms\": " + Jn(l.Item2) + ", \"ms2\": " + Jn(l.Item3) + ", \"level\": " + l.Item4 + " }").ToArray()) + "\n  ] }"); break;
                case "p": sb.Append("{ \"kind\": \"path\", \"caption\": " + Js(PhoneP.Item1) + ", \"hops\": [\n" + string.Join(",\n", PhoneP.Item2.Select(h => "    { \"hop\": " + Js(h.Item1) + ", \"address\": " + Js(h.Item2) + ", \"ms\": " + Jn(h.Item3) + ", \"owner\": " + Js(h.Item4) + ", \"level\": " + h.Item5 + " }").ToArray()) + "\n  ] }"); break;
                case "b": sb.Append("{ \"kind\": \"bars\", \"caption\": " + Js(PhoneB.Item1) + ", \"unit\": " + Js(PhoneB.Item2) + ", \"bars\": [\n" + string.Join(",\n", PhoneB.Item3.Select(b => "    { \"name\": " + Js(b.Item1) + ", \"value\": " + Jn(b.Item2) + ", \"label\": " + Js(b.Item3) + ", \"level\": " + b.Item4 + " }").ToArray()) + "\n  ] }"); break;
                default: sb.Append(visRect.IsEmpty ? "null" : "{ \"kind\": \"image\", \"note\": \"this picture has no data form of its own - every number in it is in rows; Save picture gives the PNG\" }"); break;
            }
            sb.Append(",\n  \"rows\": [\n" + string.Join(",\n", ResRows().Select(o => "    { \"level\": " + Js(o[0]) + ", \"name\": " + Js(o[1]) + ", \"value\": " + Js(o[2]) + ", \"advice\": " + Js(o.Length > 3 ? o[3] : "") + " }").ToArray()) + "\n  ]\n}\n");
            return sb.ToString();
        }
        internal static string Cv(string s) { s = s ?? ""; if (s.Length > 0 && "=+-@\t\r".IndexOf(s[0]) >= 0) s = "'" + s; return s.IndexOfAny(CsvSpecial) >= 0 || s.IndexOf(CsvSep) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s; }
        static readonly char[] CsvSpecial = { ',', ';', '"', '\n', '\r' };
        // Excel splits a double-clicked CSV on Windows' list separator: ',' in English, ';' in much of Europe
        internal static char CsvSep { get { var ls = CultureInfo.CurrentCulture.TextInfo.ListSeparator; return ls != null && ls.Length == 1 && ls[0] != '"' && !char.IsLetterOrDigit(ls[0]) ? ls[0] : ','; } }
        internal string Csv()
        {
            char sp = CsvSep; var sb = new StringBuilder("level" + sp + "name" + sp + "value" + sp + "advice\r\n");
            foreach (var o in ResRows()) sb.Append(Cv(o[0] == "0" ? "ok" : o[0] == "1" ? "watch" : o[0] == "2" ? "problem" : o[0] == "-1" ? "section" : "info")).Append(sp).Append(Cv(o[1])).Append(sp).Append(Cv(o[2])).Append(sp).Append(Cv(o.Length > 3 ? o[3] : "")).Append("\r\n");
            return sb.ToString();
        }
        void CopyPicture()
        {
            try { using (var bmp = PictureBitmap()) Clipboard.SetDataObject(new DataObject(DataFormats.Bitmap, bmp), true, 6, 150); Toast(L.T("Picture copied - paste it into a chat, a mail or a document")); }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("copy picture: " + e.Message); Toast(L.T("Couldn't copy the picture")); }
        }
        // no internet on this PC? Windows' own Bluetooth sender takes the picture to another PC or an Android phone (iPhones don't
        // take Bluetooth files - use Open on my phone). AdeeboMeter never listens: Windows does the sending and the receiving.
        void SendBluetooth()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AdeeboMeter"); Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, tbSel + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".png");
                using (var bmp = PictureBitmap()) bmp.Save(file, ImageFormat.Png);
                string fsq = Path.Combine(Environment.SystemDirectory, "fsquirt.exe");
                if (File.Exists(fsq)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fsq, "-send") { UseShellExecute = false });
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + file + "\"");
                Toast(L.T("Saved - in the Bluetooth window pick the device and Browse to the selected file. On Android, tap Accept - it lands in Downloads"));
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("bluetooth send: " + e.Message); Toast(L.T("Couldn't start Bluetooth - is it on? Settings > Bluetooth & devices")); }
        }
        // Android: Google's Quick Share for Windows (if installed) - Bluetooth finds the phone, Wi-Fi carries the file, so it's fast
        static string qsExe; static DateTime qsAt;
        static string QuickShareExe()
        {
            if ((DateTime.Now - qsAt).TotalMinutes < 5) return qsExe; qsAt = DateTime.Now; qsExe = null;
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
            {
                if (string.IsNullOrEmpty(root)) continue;
                foreach (var rel in QsPaths) { string f = Path.Combine(root, rel); if (File.Exists(f) && Signed.By(f, "Google LLC")) { qsExe = f; return f; } }   // AM-CG-222: only Google's own, signed
            }
            return null;
        }
        static readonly string[] QsPaths = { @"Google\NearbyShare\nearby_share.exe", @"Google\QuickShare\quick_share.exe", @"Programs\NearbyShare\nearby_share.exe" };
        // AM-CG-226: the protected way - the picture is saved, then Encrypt a file opens with it filled in; you pick a code or a
        // person, and send the .adeebo (Bluetooth, Quick Share, mail) instead of the PNG
        void EncryptPicture()
        {
            try
            {
                if (tbSel == "encrypt" || tbSel == "decrypt") { Toast(L.T("This picture is from the encryption tools - nothing to encrypt")); return; }
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AdeeboMeter"); Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, tbSel + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".png");
                using (var bmp = PictureBitmap()) bmp.Save(file, ImageFormat.Png);
                SelectTool("encrypt");
                if (tbBoxes.Count > 1) { tbBoxes[0].Box.Text = file; tbBoxes[1].Box.Text = ""; tbBoxes[1].Box.Focus(); }
                Toast(L.T("Type code (or a person's name) in For, press Run, then send the .adeebo file - the PNG stays in Documents\\AdeeboMeter"));
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("encrypt picture: " + e.Message); Toast(L.T("Couldn't save the picture")); }
        }
        void SendQuickShare()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AdeeboMeter"); Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, tbSel + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".png");
                using (var bmp = PictureBitmap()) bmp.Save(file, ImageFormat.Png);
                qsAt = DateTime.MinValue; string qs = QuickShareExe();   // the signature is checked again right before it starts, not trusted from the cache
                if (qs != null) using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(qs) { UseShellExecute = false })) { }
                using (System.Diagnostics.Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "/select,\"" + file + "\"")) { }
                Toast(L.T(qs != null ? "Saved - drag the selected file onto Quick Share, then accept it on your Android phone" : "Saved - open Quick Share yourself from the Start menu (AdeeboMeter only starts it when Google's signature checks out)"));
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("quick share: " + e.Message); Toast(L.T("Couldn't start Quick Share")); }
        }
        // AM-CG-219: before the code shows, the exact lines the link carries (anyone with the link can read them) - once a session
        // AM-CG-286: the preview is the link itself, decoded - every line and hop it carries, nothing else - and it's asked again
        // whenever what would be shared changes (the same result shared twice isn't asked twice)
        static string phoneOk = "";
        void OpenOnPhone()
        {
            string link = PhoneLink(); if (link == null) return;
            var f = link.Substring(link.IndexOf('#') + 1).Split('!'); string body = string.Join("!", f.Skip(2).ToArray());
            if (body != phoneOk)
            {
                Func<string, string> D = x => { try { return Uri.UnescapeDataString((x ?? "").Replace("+", " ")); } catch (UriFormatException) { return x; } };
                var lines = new List<string>();
                if (f.Length > 4 && f[4] == "p" && f.Length > 6) foreach (var h in f[6].Split('~').Where(z => z != "")) { var p = h.Split(','); lines.Add("• " + L.T("Hop") + " " + D(p[0]) + (p.Length > 1 ? " · " + D(p[1]) : "") + (p.Length > 3 && p[3] != "" ? " · " + D(p[3]) : "")); }
                // bug-hunt ML-173: the picture's own data (an exchange's actors and steps, a race's lanes, bars) is in the link too - so it's shown
                if (f.Length > 6 && (f[4] == "x" || f[4] == "r" || f[4] == "b"))
                {
                    lines.Add("\n" + L.T("THE PICTURE"));
                    foreach (var part in new[] { f[5], f[6] }) foreach (var it in part.Split('~').Where(z => z != "")) lines.Add("• " + string.Join(" · ", it.Split(',').Select(D).Where(z => z != "")));
                }
                if (f.Length > 7) foreach (var it in f[7].Split('~').Where(z => z != "")) { var p = it.Split(','); if (p.Length < 2) continue; lines.Add(p[0] == "-1" ? "\n" + D(p[1]).ToUpperInvariant() : "• " + D(p[1]) + (p.Length > 2 && p[2] != "" ? ": " + D(p[2]) : "") + (p.Length > 3 && p[3] != "" ? " (" + D(p[3]) + ")" : "")); }
                string what = string.Join("\n", lines.Take(60)) + (lines.Count > 60 ? "\n• ... " + (lines.Count - 60) + " " + L.T("more lines") : "");
                if (AskCard.Show(this, L.T("Exactly what the link carries - anyone you give it to can read it (nothing goes to a server; it all sits in the link):") + "\n\n" + what + "\n\n" + L.T("Show the code?"), "AdeeboMeter", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
                phoneOk = body;
            }
            ShowQr(link);
        }

        // ---------- the link: adeebometer.com/r#2!time!tool!caption!kind!A!B!rows  (fields "!", items "~", parts ",";
        // every text escaped, then spaces as "+"). Kinds: x exchange (A actors, B steps), r race, p path, b bars, "" rows only.
        static string E(string s) { return Uri.EscapeDataString(s ?? "").Replace("~", "%7E").Replace("%20", "+"); }
        static string Nm(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "" : Math.Round(v, v < 10 ? 1 : 0).ToString(CultureInfo.InvariantCulture); }
        static string More(int shown, int all) { return all > shown ? " (" + shown + " of " + all + ")" : ""; }
        static int PhoneRank(string lv) { return lv == "2" ? 0 : lv == "1" ? 1 : lv == "0" ? 2 : lv == "3" ? 3 : 4; }
        internal string PhoneLink()
        {
            CapturePicture(); string cap = "", a = "", b = "";
            // UTC (the phone shows it in its own time zone), then the PC's own offset: when the two disagree, the phone shows the
            // PC's clock too - a PC set to the wrong zone is visible at a glance
            string when = DateTime.UtcNow.ToString("yyMMddHHmm", CultureInfo.InvariantCulture) + "o" + ((int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes).ToString(CultureInfo.InvariantCulture);
            // section headings travel too (the phone shows the same groups as the PC); "-2" rows are layout only
            var rows = ShareRows().Where(o => o[0] != "-2").ToList();
            for (int detail = 1; detail >= -1; detail--)   // the details give way first, then the picture itself - never the rows
            {
                switch (detail < 0 ? "" : PhoneKind)
                {
                    case "x": cap = PhoneX.Item1 + More(Math.Min(20, PhoneX.Item3.Count), PhoneX.Item3.Count); a = string.Join("~", PhoneX.Item2.Select(E).ToArray()); b = string.Join("~", PhoneX.Item3.Take(20).Select(s => s.From + "," + (s.All ? PhoneX.Item2.Length - 1 : s.To) + "," + s.Lv + "," + (s.Sealed ? 1 : 0) + "," + (s.All ? 1 : 0) + "," + Nm(s.Ms) + "," + E(Cut(s.Label, 60)) + "," + (detail == 1 ? E(Cut(s.Detail, 60)) : "")).ToArray()); break;
                    case "r": cap = PhoneR.Item1 + More(Math.Min(16, PhoneR.Item2.Count), PhoneR.Item2.Count); a = ""; b = string.Join("~", PhoneR.Item2.Take(16).Select(l => E(Cut(l.Item1, 40)) + "," + Nm(l.Item2) + "," + Nm(l.Item3) + "," + l.Item4).ToArray()); break;
                    case "p":
                        {   // a long path keeps its first hops and the last one (the destination), with the gap counted
                            var hp = PhoneP.Item2; var hk = hp.Count <= 30 ? hp : hp.Take(29).Concat(new[] { hp[hp.Count - 1] }).ToList();
                            cap = PhoneP.Item1 + More(hk.Count, hp.Count); a = ""; b = string.Join("~", hk.Select(h => E(h.Item1) + "," + E(Cut(h.Item2, 30)) + "," + Nm(h.Item3) + "," + (detail == 1 ? E(Cut(h.Item4, 30)) : "") + "," + h.Item5).ToArray()); break;
                        }
                    case "b": cap = PhoneB.Item1 + More(Math.Min(20, PhoneB.Item3.Count), PhoneB.Item3.Count); a = E(PhoneB.Item2); b = string.Join("~", PhoneB.Item3.Take(20).Select(z => E(Cut(z.Item1, 30)) + "," + Nm(z.Item2) + "," + (detail == 1 ? E(Cut(z.Item3, 30)) : "") + "," + z.Item4).ToArray()); break;
                    default: cap = ""; a = ""; b = ""; break;
                }
                string kind = detail < 0 ? "" : PhoneKind;
                string head = "https://adeebometer.com/r#2!" + when + "!" + E(Cut(ToolName(), 60)) + "!" + E(Cut(cap, 80)) + "!" + kind + "!" + a + "!" + b + "!";
                if (Encoding.UTF8.GetByteCount(head) > Qr.MaxBytes - 40) continue;
                // the rows fill what's left: one that doesn't fit is skipped (the next may), and the rest are counted
                // what someone needs to act on gets in first: problems and warnings with their advice, then the numbers that
                // were fine, then notes and table lines. Kept rows are sent in the PC's own order, with their section headings.
                int hb = Encoding.UTF8.GetByteCount(head), room = Qr.MaxBytes - 90;
                var order = Enumerable.Range(0, rows.Count).Where(i => rows[i][0] != "-1").OrderBy(i => PhoneRank(rows[i][0])).ThenBy(i => i).ToList();
                var keep = new Dictionary<int, string>(); int used = hb;
                foreach (int i in order)
                {
                    var o = rows[i]; bool bad = o[0] == "1" || o[0] == "2";
                    string adv = o.Length > 3 && o[3] != "" && (bad || detail == 1) ? "," + E(Cut(o[3], bad ? 140 : 90)) : "";
                    string item = o[0] + "," + E(Cut(o[1], 40)) + "," + E(Cut(o[2], bad ? 120 : 90)) + adv;
                    if (used + item.Length + 1 > room) { item = o[0] + "," + E(Cut(o[1], 40)) + "," + E(Cut(o[2], 40)) + (bad && adv != "" ? "," + E(Cut(o[3], 60)) : ""); if (used + item.Length + 1 > room) continue; }
                    keep[i] = item; used += item.Length + 1;
                }
                while (true)
                {
                    var parts = new List<string>(); string pendingHead = null;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        if (rows[i][0] == "-1") { pendingHead = "-1," + E(Cut(rows[i][1], 40)) + ","; continue; }
                        string it; if (!keep.TryGetValue(i, out it)) continue;
                        if (pendingHead != null) { parts.Add(pendingHead); pendingHead = null; }
                        parts.Add(it);
                    }
                    int left = rows.Count(o => o[0] != "-1") - keep.Count;
                    if (left > 0) parts.Add("3," + E(L.T("Left on the PC")) + "," + E(left + " " + (rows.Where((o, i) => o[0] != "-1" && !keep.ContainsKey(i)).Any(o => o[0] == "1" || o[0] == "2") ? L.T("more rows, including warnings - open the result on the PC") : L.T("detail rows that were fine or just notes"))));
                    string link = head + string.Join("~", parts.ToArray());
                    if (Encoding.UTF8.GetByteCount(link) <= Qr.MaxBytes) return link;
                    if (keep.Count == 0) break;
                    keep.Remove(order.Last(i => keep.ContainsKey(i)));   // the least important row still in comes out first
                }
            }
            return "https://adeebometer.com/r#2!" + when + "!" + E(Cut(ToolName(), 60)) + "!!!!!";
        }
    }
}
