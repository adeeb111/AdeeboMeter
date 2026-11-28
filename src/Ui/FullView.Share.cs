// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Share a diagnosis: a QR code the phone's camera reads (made here, offline - the picture is just the text).
    sealed partial class FullView
    {
        // The QR holds a link to adeebometer.com/r with the result after the # - the part of a link browsers never send to the
        // server, so the site never sees it; the phone's own browser draws the page (docs/r.html) from it: the verdict, the path,
        // charts, what to do, and buttons to send it to the provider. Addresses and names are left out. Format v1, fields by "!":
        // 1 ! yyMMddHHmm ! levels (overall + PC, LAN, ISP, DNS, Internet) ! gw,ping,jitter,loss,dns,down,up,cpu,mem,wifi,outages,c|w
        //   ! last 30 internet checks ! router per minute (30 min) ! loss % per minute ! headline (+ for spaces)
        // A series is one character per value from QrAlpha: ms as round(2*sqrt(ms)) (back: (k/2)^2), loss as the %, "-" = no answer.
        const string QrAlpha = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_.";
        const string QrSite = "https://adeebometer.com/r#";
        // loss: 1 % steps up to 50 %, then about 4 % steps to 100 % - so 63 % and 100 % no longer look the same
        static char QrLoss(double p) { p = Math.Max(0, Math.Min(100, p)); return QrAlpha[(int)(p <= 50 ? Math.Round(p) : 50 + Math.Round((p - 50) * 13 / 50))]; }
        // bug-hunt L-325: the top character means "1 s or more" - the phone page reads it as 1000 ms, a cap rather than a wrong 992
        static char QrMs(double ms) { return double.IsNaN(ms) || ms < 0 ? '-' : QrAlpha[(int)Math.Min(63, Math.Round(2 * Math.Sqrt(ms)))]; }
        static string QrNum(double v, int d) { return double.IsNaN(v) || double.IsInfinity(v) ? "" : Math.Round(v, d).ToString(CultureInfo.InvariantCulture); }
        static readonly string[] QrPhoneRow = { "3", "On the phone", "the verdict, the path, the numbers in plain words, two charts, what to do - and Send to my provider, Share, Save as PDF", "Names, the Wi-Fi name and addresses are left out.", "i" };
        internal string QrLink()
        {
            var s = M.Latest; var now = DateTime.Now; var c = CultureInfo.InvariantCulture;
            var mins = Cases.Minutes(now.AddMinutes(-30), now); var wifi = mins.Wifi.Where(z => !double.IsNaN(z) && z > 0).ToList();
            string kindNow; lock (M.L) kindNow = M.AdapterKind ?? ""; string link = kindNow.IndexOf("Wi", StringComparison.OrdinalIgnoreCase) >= 0 ? "w" : kindNow == "LAN" ? "c" : "";   // the connection now, not the last 30 minutes
            var lv = new StringBuilder().Append(Math.Max(0, Math.Min(3, s.Level))); for (int i = 0; i < 5; i++) lv.Append(i < s.Path.Length ? Math.Max(0, Math.Min(3, s.Path[i])) : 3);
            int outs = 0; try { outs = Outages.Last(M, 1).Count; } catch (Exception e) { if (e is OutOfMemoryException) throw; }
            string nums = string.Join(",", new[] { QrNum(s.GwMs, 0), QrNum(s.Ping, 0), QrNum(s.Jitter, 1), QrNum(s.LossPct, 1), QrNum(s.DnsMs, 0), QrNum(s.Down, 1), QrNum(s.Up, 1), QrNum(s.Cpu, 0), QrNum(s.Mem, 0), link == "w" && wifi.Count > 0 ? QrNum(wifi[wifi.Count - 1], 0) : "", outs.ToString(c), link });
            var net = new StringBuilder(); foreach (var v in s.NetHist.Skip(Math.Max(0, s.NetHist.Length - 30))) net.Append(QrMs(v));
            var gw = new StringBuilder(); var lo = new StringBuilder();
            for (int i = Math.Max(0, mins.T.Count - 30); i < mins.T.Count; i++) { gw.Append(QrMs(mins.Gw[i])); lo.Append(double.IsNaN(mins.Loss[i]) ? '-' : QrLoss(mins.Loss[i])); }
            string head = OverTime.NoAddr(GameSession.Redact(s.Headline ?? "", "")); head = Regex.Replace(head, @"[^\w .,%'()-]", "").Trim().Replace(' ', '+');
            string body = "1!" + now.ToUniversalTime().ToString("yyMMddHHmm", c) + "!" + lv + "!" + nums + "!" + net + "!" + gw + "!" + lo + "!";
            // the QR holds at most Qr.MaxBytes: the headline gives way first, then the oldest of each series
            int room = Qr.MaxBytes - QrSite.Length - body.Length; if (room < head.Length) head = room > 8 ? head.Substring(0, room) : "";
            // a safety net only (bug-hunt L-324): the series are ~90 characters and the limit 1,732, so it never runs today
            while (QrSite.Length + body.Length + head.Length > Qr.MaxBytes && net.Length > 10) { net.Remove(0, 1); if (gw.Length > 10) { gw.Remove(0, 1); lo.Remove(0, 1); } body = "1!" + now.ToUniversalTime().ToString("yyMMddHHmm", c) + "!" + lv + "!" + nums + "!" + net + "!" + gw + "!" + lo + "!"; }
            return QrSite + body + head;
        }
        List<string[]> QrRows()
        {
            string link = QrLink();
            return new List<string[]>
            {
                new[] { "0", "Scan it with your phone", "point the camera at the code above - one tap opens your result with charts", "It opens adeebometer.com/r. The numbers ride in the link after the # and stay on your phone - the site never sees them.", "i", "qr:" + link },
                QrPhoneRow,
                new[] { "3", "Bigger code", "click to open it full size", "", "i", "qr:" + link }
            };
        }
        void ShowQr(string text)
        {
            var m = Qr.Encode(text); if (m == null) return;
            int px = Math.Max(4, (int)(6 * S)); var wa = Screen.FromControl(this).WorkingArea; int fit = (Math.Min(wa.Width, wa.Height) - Sc(140)) / (m.GetLength(0) + 8); if (fit >= 2 && fit < px) px = fit;   // a dense code on a small screen still fits
            string what = tbSel != null ? ToolName() : L.T("your result");
            // drawn like the rest of the app: its colours (light or dark), its fonts, its rounded cards. The code itself always
            // sits on white with a quiet margin - phone cameras need dark squares on light to read it.
            using (var bmp = Qr.Picture(m, px))
            using (var f = new Form { Text = L.T("Open on your phone"), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Bg, ShowInTaskbar = false, Icon = Icon })
            {
                int pad = Sc(20), w = Math.Max(bmp.Width + Sc(32), Sc(340)), qrBox = bmp.Width + Sc(32);
                var steps = new[] { L.T("Point your phone's camera at the code (on some Android phones, Google Lens)."), L.T("Tap the link that appears - the result opens with its charts, ready to copy, share or save as a PDF.") };
                string privacy = L.T("The result travels inside the link itself. Nothing is uploaded, and adeebometer.com never receives it.");
                float hHead = 0, hSteps = 0, hPriv = 0;
                using (var mg = CreateGraphics())
                {
                    hHead = mg.MeasureString(what, Draw.fHead, w).Height;
                    foreach (var st in steps) hSteps += mg.MeasureString(st, Draw.fBody, w - Sc(34)).Height + Sc(10);
                    hPriv = mg.MeasureString(privacy, Draw.fSmall, w - Sc(32)).Height;
                }
                int yQr = pad + Sc(22) + (int)hHead + Sc(14), ySteps = yQr + qrBox + Sc(18), yPriv = ySteps + (int)hSteps + Sc(6), yBtn = yPriv + (int)hPriv + Sc(28) + Sc(14);
                var done = new PillButton("Done", PillButton.K.Primary); done.Location = new Point(pad + w - done.Width, yBtn); done.Click += delegate { f.Close(); };
                f.Controls.Add(done); f.AcceptButton = null; f.CancelButton = null;
                f.ClientSize = new Size(w + pad * 2, done.Bottom + pad);
                var canvas = new Panel { Bounds = new Rectangle(0, 0, f.ClientSize.Width, done.Top - Sc(4)), BackColor = Theme.Bg };
                canvas.Paint += delegate(object s2, PaintEventArgs e)
                {
                    var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.TextRenderingHint = Draw.WindowText; g.TextContrast = Draw.Contrast;
                    using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Kit.Sent("SCAN TO OPEN"), Draw.fTiny, mb, pad, pad);
                    using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(what, Draw.fHead, ib, new RectangleF(pad, pad + Sc(20), w, hHead + 4));
                    var qr = new RectangleF(pad + (w - qrBox) / 2f, yQr, qrBox, qrBox);
                    Theme.Fill(g, qr, Sc(16), Color.White); Theme.Stroke(g, qr, Sc(16), Theme.Line, 1);
                    g.DrawImageUnscaled(bmp, (int)(qr.X + Sc(16)), (int)(qr.Y + Sc(16)));
                    float y = ySteps;
                    for (int k = 0; k < steps.Length; k++)
                    {
                        var dot = new RectangleF(pad, y, Sc(22), Sc(22)); Theme.Fill(g, dot, Sc(11), Theme.BlueSoft);
                        using (var nb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) using (var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString((k + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Draw.fSmall, nb, dot, cf);
                        float th = g.MeasureString(steps[k], Draw.fBody, w - Sc(34)).Height;
                        using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(steps[k], Draw.fBody, ib, new RectangleF(pad + Sc(34), y + Sc(1), w - Sc(34), th + 2));
                        y += th + Sc(10);
                    }
                    using (var sb2 = new SolidBrush(Theme.Sub)) g.DrawString("🔒 " + privacy, Draw.fSmall, sb2, new RectangleF(pad, yPriv, w, hPriv + Sc(4)));
                };
                f.Controls.Add(canvas);
                f.KeyPreview = true; f.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) f.Close(); };
                Kit.Polish(f); f.BackColor = Theme.Bg; f.ShowDialog(this);   // B3 L-4: one colour behind the card and the button row
            }
        }
    }
}
