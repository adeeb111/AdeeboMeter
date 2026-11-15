// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

        // a short note beside the links after a save or a copy
        string barNote = ""; DateTime barAt;
        void Toast(string t) { barNote = t; barAt = DateTime.Now; tbCanvas.Invalidate(); }
        // the link bar under a picture
        float PaintPictureBar(Graphics g, float x, float y, float w)
        {
            if (visRect.IsEmpty || tbBusy) return y;
            float lx = x + 4 * S; var col = Theme.Dark ? Theme.BlueDark : Theme.Blue;
            Action<string, string, Action> link = (icon, t, a) =>
            {
                var ts = g.MeasureString(L.T(t), Draw.fBodyB); Kit.Icon(g, icon, new RectangleF(lx, y + 2 * S, 16 * S, 16 * S), col);
                using (var b = new SolidBrush(col)) g.DrawString(L.T(t), Draw.fBodyB, b, lx + 20 * S, y);
                Act(tbCanvas, new RectangleF(lx, y - 2 * S, ts.Width + 24 * S, ts.Height + 4 * S), a); lx += ts.Width + 46 * S;
            };
            link("report", "Save picture", SavePicture); link("plus", "Copy picture", CopyPicture); link("phone", "Open on my phone", OpenOnPhone); link("devices", "Send by Bluetooth", SendBluetooth);
            if (barNote != "" && (DateTime.Now - barAt).TotalSeconds < 8) using (var b = new SolidBrush(Theme.LvText(0))) g.DrawString(barNote, Draw.fSmall, b, lx, y + 2 * S);
            return y + 30 * S;
        }

        // the picture alone, drawn again into a bitmap with a footer
        Bitmap PictureBitmap()
        {
            float w = Math.Max(600 * S, visRect.Width); var size = new Size((int)(w + 40 * S), 4000);
            using (var probe = new Bitmap(10, 10)) using (var pg = Graphics.FromImage(probe)) { Prep(pg); float h0 = PaintToolVisual0(pg, 20 * S, 20 * S, w); size.Height = (int)(h0 + 40 * S); }
            var bmp = new Bitmap(size.Width, Math.Max(100, size.Height));
            using (var g = Graphics.FromImage(bmp))
            {
                Prep(g); g.Clear(Theme.Bg); PaintToolVisual0(g, 20 * S, 20 * S, w);
                string foot = L.T("made with AdeeboMeter") + " · adeebometer.com · " + DateTime.Now.ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture);
                using (var b = new SolidBrush(Theme.Muted)) { var fs = g.MeasureString(foot, Draw.fTiny); g.DrawString(foot, Draw.fTiny, b, bmp.Width - fs.Width - 20 * S, bmp.Height - fs.Height - 10 * S); }
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
                using (var d = new SaveFileDialog { Filter = "PNG picture|*.png", InitialDirectory = dir, FileName = tbSel + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".png" })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    bmp.Save(d.FileName, ImageFormat.Png); Toast(L.T("Saved") + " " + Path.GetFileName(d.FileName));
                }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("save picture: " + e.Message); Toast(L.T("Couldn't save the picture")); }
        }
        void CopyPicture()
        {
            try { using (var bmp = PictureBitmap()) Clipboard.SetImage(bmp); Toast(L.T("Picture copied - paste it into a chat, a mail or a document")); }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("copy picture: " + e.Message); Toast(L.T("Couldn't copy the picture")); }
        }
        // no internet on this PC? Windows' own Bluetooth sender takes the picture to another PC or an Android phone (iPhones don't
        // take Bluetooth files - use Open on my phone). AdeeboMeter never listens: Windows does the sending and the receiving.
        void SendBluetooth()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AdeeboMeter"); Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, tbSel + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".png");
                using (var bmp = PictureBitmap()) bmp.Save(file, ImageFormat.Png);
                string fsq = Path.Combine(Environment.SystemDirectory, "fsquirt.exe");
                if (File.Exists(fsq)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fsq, "-send") { UseShellExecute = false });
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + file + "\"");
                Toast(L.T("Saved - in the Bluetooth window pick the other device, then Browse to the selected file (or right-click it > Send to > Bluetooth)"));
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("bluetooth send: " + e.Message); Toast(L.T("Couldn't start Bluetooth - is it on? Settings > Bluetooth & devices")); }
        }
        void OpenOnPhone() { string link = PhoneLink(); if (link != null) ShowQr(link); }

        // ---------- the link: adeebometer.com/r#2!time!tool!caption!kind!A!B!rows  (fields "!", items "~", parts ",";
        // every text escaped, then spaces as "+"). Kinds: x exchange (A actors, B steps), r race, p path, b bars, "" rows only.
        static string E(string s) { return Uri.EscapeDataString(s ?? "").Replace("~", "%7E").Replace("%20", "+"); }
        static string Nm(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "" : Math.Round(v, v < 10 ? 1 : 0).ToString(CultureInfo.InvariantCulture); }
        internal string PhoneLink()
        {
            try { using (var probe = new Bitmap(10, 10)) using (var pg = Graphics.FromImage(probe)) PaintToolVisual0(pg, 0, 0, 900 * S); }
            catch (Exception e) { if (e is OutOfMemoryException) throw; PhoneKind = ""; }
            tbCanvas.Invalidate();
            string cap = "", a = "", b = "";
            for (int detail = 1; detail >= 0; detail--)   // the details give way first when it's too long for one QR code
            {
                switch (PhoneKind)
                {
                    case "x": cap = PhoneX.Item1; a = string.Join("~", PhoneX.Item2.Select(E).ToArray()); b = string.Join("~", PhoneX.Item3.Take(10).Select(s => s.From + "," + (s.All ? PhoneX.Item2.Length - 1 : s.To) + "," + s.Lv + "," + (s.Sealed ? 1 : 0) + "," + (s.All ? 1 : 0) + "," + Nm(s.Ms) + "," + E(Cut(s.Label, 60)) + "," + (detail == 1 ? E(Cut(s.Detail, 60)) : "")).ToArray()); break;
                    case "r": cap = PhoneR.Item1; b = string.Join("~", PhoneR.Item2.Take(8).Select(l => E(Cut(l.Item1, 40)) + "," + Nm(l.Item2) + "," + Nm(l.Item3) + "," + l.Item4).ToArray()); break;
                    case "p": cap = PhoneP.Item1; b = string.Join("~", PhoneP.Item2.Take(12).Select(h => E(h.Item1) + "," + E(Cut(h.Item2, 30)) + "," + Nm(h.Item3) + "," + (detail == 1 ? E(Cut(h.Item4, 30)) : "") + "," + h.Item5).ToArray()); break;
                    case "b": cap = PhoneB.Item1; a = E(PhoneB.Item2); b = string.Join("~", PhoneB.Item3.Take(10).Select(z => E(Cut(z.Item1, 30)) + "," + Nm(z.Item2) + "," + (detail == 1 ? E(Cut(z.Item3, 30)) : "") + "," + z.Item4).ToArray()); break;
                    default: cap = ""; break;
                }
                string head = "https://adeebometer.com/r#2!" + DateTime.Now.ToString("yyMMddHHmm", CultureInfo.InvariantCulture) + "!" + E(Cut(ToolName(), 60)) + "!" + E(Cut(cap, 70)) + "!" + PhoneKind + "!" + a + "!" + b + "!";
                // the result rows fill what's left
                var rows = (tbRows ?? new List<string[]>()).Where(o => o.Length > 2 && o[0] != "-1" && o[0] != "-2").ToList(); var sb = new StringBuilder();
                foreach (var o in rows)
                {
                    string item = o[0] + "," + E(Cut(o[1], 40)) + "," + E(Cut(o[2], 90));
                    if (Encoding.UTF8.GetByteCount(head) + sb.Length + item.Length + 1 > Qr.MaxBytes) break;
                    if (sb.Length > 0) sb.Append('~'); sb.Append(item);
                }
                string link = head + sb;
                if (Encoding.UTF8.GetByteCount(link) <= Qr.MaxBytes) return link;
            }
            // still too long (a very long exchange): rows only
            string min = "https://adeebometer.com/r#2!" + DateTime.Now.ToString("yyMMddHHmm", CultureInfo.InvariantCulture) + "!" + E(Cut(ToolName(), 60)) + "!!!!!";
            return min;
        }
    }
}
