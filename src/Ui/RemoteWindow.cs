// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // The small window for the phone remote: the QR to scan, what's happening, and End. Closing it ends the session.
    sealed class RemoteWindow : Form
    {
        readonly Remote r; readonly Label status, head; readonly Button end; readonly Timer t = new Timer { Interval = 1000 };
        public RemoteWindow(Remote session)
        {
            r = session; Text = L.T("Phone remote"); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = true; StartPosition = FormStartPosition.CenterScreen; BackColor = Color.White; ShowInTaskbar = true;
            int pad = Sc(16), w = Sc(330);
            head = new Label { AutoSize = true, MaximumSize = new Size(w, 0), Font = Draw.fBodyB, ForeColor = Color.FromArgb(17, 24, 39), Location = new Point(pad, pad), Text = L.T("Watch this PC from your phone") };
            Controls.Add(head);
            int y = head.Bottom + Sc(10);
            var m = Qr.Encode(r.Link);
            if (m != null)
            {
                var pb = new PictureBox { Image = Qr.Picture(m, Math.Max(4, Sc(5))), SizeMode = PictureBoxSizeMode.AutoSize };
                pb.Location = new Point(pad + (w - pb.Image.Width) / 2, y); Controls.Add(pb); y = pb.Bottom + Sc(10);
                Disposed += delegate { pb.Image.Dispose(); };
            }
            var how = new Label { AutoSize = true, MaximumSize = new Size(w, 0), Font = Draw.fSmall, ForeColor = Color.FromArgb(75, 85, 99), Location = new Point(pad, y),
                Text = L.T("Scan it with your phone's camera. You'll see live speed, ping, game and trading numbers, and can start a check while you play.") + "\n\n" +
                       L.T("Encrypted end to end: the key is only in this code. The relay passes messages along and keeps nothing. One phone, up to 30 minutes, only after you press Allow.") };
            Controls.Add(how); y = how.Bottom + Sc(12);
            status = new Label { AutoSize = true, MaximumSize = new Size(w, 0), Font = Draw.fBodyB, ForeColor = Color.FromArgb(37, 99, 235), Location = new Point(pad, y), Text = "" };
            Controls.Add(status); y = status.Bottom + Sc(30);
            end = new Button { Text = L.T("End session"), AutoSize = true, Location = new Point(pad, y) }; end.Click += delegate { Close(); };
            Controls.Add(end);
            ClientSize = new Size(w + pad * 2, end.Bottom + pad);
            t.Tick += delegate { Sync(); }; t.Start(); Sync();
            FormClosed += delegate { t.Stop(); t.Dispose(); r.End("you closed the remote window"); };
            KeyPreview = true; KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }
        static int Sc(int v) { return (int)(v * Draw.S); }
        public void Sync()
        {
            if (IsDisposed) return;
            var l = r.Left; string left = ((int)l.TotalMinutes) + ":" + l.Seconds.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
            switch (r.St)
            {
                case Remote.State.Connecting: status.Text = L.T("Connecting to the relay..."); break;
                case Remote.State.Waiting: status.Text = L.T("Waiting for your phone to scan the code"); break;
                case Remote.State.Asking: status.Text = L.T("A phone is asking to connect"); break;
                case Remote.State.Paired: status.Text = "📱 " + L.T("Connected") + (r.PhoneName != "" ? " - " + r.PhoneName : "") + " · " + left + " " + L.T("left"); break;
                default: status.Text = L.T("Session over") + (r.Why != "" ? ": " + L.T(r.Why) : "."); end.Text = L.T("Close"); break;
            }
            end.Top = status.Bottom + Sc(14); ClientSize = new Size(ClientSize.Width, end.Bottom + Sc(16));
        }
    }
}
