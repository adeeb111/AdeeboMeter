// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Share a diagnosis: a QR code the phone's camera reads (made here, offline - the picture is just the text).
    sealed partial class FullView
    {
        List<string[]> QrRows()
        {
            string t = OverTime.Summary(M, 400);
            while (Encoding.UTF8.GetByteCount(t) > Qr.MaxBytes - 1) t = t.Substring(0, t.Length - (t.Length > 1 && char.IsLowSurrogate(t[t.Length - 1]) ? 2 : 1));   // whole characters only
            var rows = new List<string[]>
            {
                new[] { "0", "QR code", "ready - click to show it, then point your phone's camera at it", "The code holds only this text: no link, no address, nothing to download.", "i", "qr:" + t },
                new[] { "3", "What it says", t.Replace("\n", " · "), "Names, Wi-Fi name and addresses are left out.", "i" }
            };
            return rows;
        }
        void ShowQr(string text)
        {
            var m = Qr.Encode(text); if (m == null) return;
            int px = Math.Max(4, (int)(6 * S));
            using (var bmp = Qr.Picture(m, px))
            using (var f = new Form { Text = L.T("Scan with your phone"), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, BackColor = Color.White, ShowInTaskbar = false })
            using (var pb = new PictureBox { Image = bmp, SizeMode = PictureBoxSizeMode.AutoSize, Location = new Point(Sc(16), Sc(16)) })
            using (var note = new Label { Text = L.T("Your phone's camera reads it - nothing is sent."), AutoSize = true, ForeColor = Color.FromArgb(91, 102, 117), Font = Draw.fSmall, Location = new Point(Sc(16), Sc(24) + bmp.Height) })
            {
                f.Controls.Add(pb); f.Controls.Add(note);
                f.ClientSize = new Size(bmp.Width + Sc(32), bmp.Height + Sc(60));
                f.KeyPreview = true; f.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) f.Close(); };
                f.ShowDialog(this);
            }
        }
    }
}
