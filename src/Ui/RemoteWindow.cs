// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // The small window for the phone remote: the QR to scan, what's happening, and End. Closing it ends the session.
    sealed class RemoteWindow : Form
    {
        readonly Remote r; readonly Label status, head, xfer; readonly Button end, sendFile, sendText, copy, openLink, openFolder; readonly TextBox got; readonly CheckBox autoClip; string lastFile = ""; readonly Timer t = new Timer { Interval = 1000 };
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
            // send to the phone, and what came from it (AM: Send to PC / send to phone)
            sendFile = new Button { Text = L.T("Send a file to the phone..."), AutoSize = true, Visible = false }; sendText = new Button { Text = L.T("Send text..."), AutoSize = true, Visible = false };
            xfer = new Label { AutoSize = true, MaximumSize = new Size(w, 0), Font = Draw.fSmall, ForeColor = Color.FromArgb(75, 85, 99), Text = "" };
            got = new TextBox { Multiline = true, ReadOnly = true, Width = w, Height = Sc(60), Visible = false, ScrollBars = ScrollBars.Vertical };
            copy = new Button { Text = L.T("Copy"), AutoSize = true, Visible = false }; openLink = new Button { Text = L.T("Open link"), AutoSize = true, Visible = false }; openFolder = new Button { Text = L.T("Open the folder"), AutoSize = true, Visible = false };
            sendFile.Click += delegate { using (var d = new OpenFileDialog { Title = L.T("Send a file to the phone (up to 100 MB)") }) if (d.ShowDialog(this) == DialogResult.OK) r.SendFile(d.FileName); };
            sendText.Click += delegate { string s = Ask(L.T("Text or a link for the phone")); if (!string.IsNullOrWhiteSpace(s)) { r.SendText(s); xfer.Text = L.T("Sent to the phone."); Sync(); } };
            copy.Click += delegate { try { Clipboard.SetText(got.Text); xfer.Text = L.T("Copied."); } catch (System.Runtime.InteropServices.ExternalException) { } };
            openLink.Click += delegate { string u = got.Text.Trim(); if (System.Text.RegularExpressions.Regex.IsMatch(u, @"^https?://\S+$")) try { System.Diagnostics.Process.Start(u); } catch (Exception e) { if (e is OutOfMemoryException) throw; } };
            openFolder.Click += delegate { try { if (lastFile != "" && System.IO.File.Exists(lastFile)) System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + lastFile + "\""); else System.Diagnostics.Process.Start("explorer.exe", Remote.DropDir); } catch (Exception e) { if (e is OutOfMemoryException) throw; } };
            autoClip = new CheckBox { Text = L.T("Put text from my phone on this PC's clipboard"), AutoSize = true, Visible = false, Checked = false };   // off unless ticked, for this session only
            Controls.AddRange(new Control[] { sendFile, sendText, xfer, got, copy, openLink, openFolder, autoClip });
            r.GotText = s => Ui(() => { got.Text = s; if (autoClip.Checked) try { Clipboard.SetText(s); } catch (System.Runtime.InteropServices.ExternalException) { } got.Visible = copy.Visible = true; openLink.Visible = System.Text.RegularExpressions.Regex.IsMatch(s.Trim(), @"^https?://\S+$"); xfer.Text = L.T("From your phone:"); Sync(); });
            r.FileSaved = p => Ui(() => { lastFile = p; openFolder.Visible = true; xfer.Text = L.T("From your phone, saved to Downloads\\AdeeboMeter:") + " " + System.IO.Path.GetFileName(p); Sync(); });
            r.SendState = msg => Ui(() => { xfer.Text = msg; Sync(); });
            end = new Button { Text = L.T("End session"), AutoSize = true, Location = new Point(pad, y) }; end.Click += delegate { Close(); };
            Controls.Add(end);
            ClientSize = new Size(w + pad * 2, end.Bottom + pad);
            t.Tick += delegate { Sync(); }; t.Start(); Sync();
            FormClosed += delegate { t.Stop(); t.Dispose(); r.End("you closed the remote window"); };
            KeyPreview = true; KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }
        static int Sc(int v) { return (int)(v * Draw.S); }
        void Ui(Action a) { try { if (!IsDisposed) BeginInvoke(a); } catch (InvalidOperationException) { } }
        // a small "type something" box (WinForms has none built in)
        string Ask(string title)
        {
            using (var f = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(Sc(360), Sc(140)), Font = Font })
            {
                var tb = new TextBox { Multiline = true, Location = new Point(Sc(12), Sc(12)), Size = new Size(Sc(336), Sc(80)), MaxLength = 4000 };
                var ok = new Button { Text = L.T("Send"), DialogResult = DialogResult.OK, Location = new Point(Sc(196), Sc(102)), AutoSize = true };
                var no = new Button { Text = L.T("Cancel"), DialogResult = DialogResult.Cancel, Location = new Point(Sc(276), Sc(102)), AutoSize = true };
                f.Controls.AddRange(new Control[] { tb, ok, no }); f.AcceptButton = null; f.CancelButton = no;
                return f.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
            }
        }
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
            bool on = r.St == Remote.State.Paired; sendFile.Visible = sendText.Visible = autoClip.Visible = on; if (!on) { got.Visible = copy.Visible = openLink.Visible = false; }
            int x0 = status.Left, yy = status.Bottom + Sc(12);
            if (on) { sendFile.Location = new Point(x0, yy); sendText.Location = new Point(sendFile.Right + Sc(8), yy); yy = sendFile.Bottom + Sc(6); autoClip.Location = new Point(x0, yy); yy = autoClip.Bottom + Sc(8); }
            xfer.Location = new Point(x0, yy); if (xfer.Text != "") yy = xfer.Bottom + Sc(6);
            if (got.Visible) { got.Location = new Point(x0, yy); yy = got.Bottom + Sc(6); copy.Location = new Point(x0, yy); openLink.Location = new Point(copy.Right + Sc(8), yy); yy = copy.Bottom + Sc(6); }
            if (openFolder.Visible) { openFolder.Location = new Point(x0, yy); yy = openFolder.Bottom + Sc(6); }
            end.Top = yy + Sc(8); ClientSize = new Size(ClientSize.Width, end.Bottom + Sc(16));
        }
    }
}
