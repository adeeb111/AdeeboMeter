// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Report a bug / Send feedback (Relay.cs)
    // Type what happened, see exactly what goes with it, press Send - no account, nothing sent before that. When sending
    // isn't possible (IT turned online lookups off, or the relay isn't set up) it's copied for the website instead.
    sealed partial class FullView
    {
        internal void ReportDialog(string kind, string prefill)
        {
            bool bug = kind == "bug";
            using (var f = new Form { Text = L.T(bug ? "Report a bug" : "Send feedback"), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(Sc(600), Sc(bug ? 560 : 400)), BackColor = Theme.Card, Font = Draw.fBody })
            {
                int x = Sc(18), w = Sc(564), y = Sc(14);
                var head = new Label { Text = L.T(bug ? "What happened? What did you expect?" : "Your idea or question"), Font = Draw.fBodyB, ForeColor = Theme.Ink, Location = new Point(x, y), AutoSize = true }; y += Sc(28);
                var what = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Font = Draw.fInput, Location = new Point(x, y), Size = new Size(w, Sc(130)), MaxLength = 4000, Text = prefill ?? "", BackColor = Theme.Card, ForeColor = Theme.Ink }; y += Sc(140);
                var tech = new CheckBox { Text = L.T("Add the technical details below (names, PC name, paths and addresses are taken out - read them first)"), Checked = false, Visible = bug, Location = new Point(x, y), AutoSize = true, ForeColor = Theme.Sub };
                var preview = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = Draw.fSmall, Location = new Point(x, y + Sc(28)), Size = new Size(w, Sc(160)), Text = bug ? BugReport.Tech(bugPage) : "", Visible = bug, BackColor = Theme.Soft, ForeColor = Theme.Sub };
                if (bug) y += Sc(200);
                var note = new Label { Text = L.T(Relay.Ready ? "Posted as a PUBLIC issue on GitHub - anyone can read it. Leave out anything private. What you see above (and the details box) is exactly what goes, and nothing goes until you press Send." : Policy.NoOnline ? "Your IT team turned online sending off - Copy puts it on the clipboard instead." : "Sending isn't switched on yet - Copy puts it on the clipboard for the form on adeebometer.com."), ForeColor = Theme.Muted, Font = Draw.fSmall, Location = new Point(x, y), Size = new Size(w, Sc(36)) }; y += Sc(42);
                var send = new PillButton(Relay.Ready ? "Send" : "Copy", PillButton.K.Primary) { Location = new Point(x, y) };
                var cancel = new PillButton("Close", PillButton.K.Chip) { Location = new Point(x + Sc(130), y + Sc(2)) };
                var status = new Label { Text = "", ForeColor = Theme.Ink, Font = Draw.fBodyB, Location = new Point(x + Sc(240), y + Sc(8)), Size = new Size(w - Sc(240), Sc(40)) };
                tech.CheckedChanged += delegate { preview.Enabled = tech.Checked; };
                cancel.Click += delegate { f.Close(); };
                send.Click += delegate
                {
                    string text = what.Text.Trim(); if (text == "") { status.Text = L.T("Write a line or two first."); return; }
                    string body = text + (tech.Checked ? "\n\n" + preview.Text : "");
                    if (!Relay.Ready)
                    {
                        try { Clipboard.SetText(body); status.Text = L.T("Copied - paste it into the form on adeebometer.com."); if (!Policy.NoOnline) Crash.OpenUrl("https://adeebometer.com/#feedback"); }
                        catch (Exception e) { if (!(e is System.Runtime.InteropServices.ExternalException || e is System.Threading.ThreadStateException)) throw; status.Text = L.T("Couldn't copy - try again."); }
                        return;
                    }
                    if (AskCard.Show(f, L.T("This becomes a public GitHub issue that anyone can read:") + "\n\n" + (body.Length > 700 ? body.Substring(0, 700) + "\n..." : body) + "\n\n" + L.T("Send it?"), "AdeeboMeter", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
                    send.Enabled = false; status.Text = L.T("Sending...");
                    string first = text.Split('\n')[0].Trim(); if (first.Length > 80) first = first.Substring(0, 80);
                    Relay.Send(Relay.Json(bug ? "bug" : "feedback", first, body, null), r =>
                    {
                        try
                        {
                            f.BeginInvoke((Action)delegate
                            {
                                if (r.Ok) { status.Text = L.T("Sent - thank you!") + (r.Number > 0 ? " (#" + r.Number + ")" : ""); var t = new Timer { Interval = 1800 }; t.Tick += delegate { t.Stop(); t.Dispose(); if (!f.IsDisposed) f.Close(); }; t.Start(); }
                                else if (r.Queued) { status.Text = L.T("No internet right now - it goes the next time AdeeboMeter starts online."); send.Enabled = true; }
                                else { status.Text = L.T("Couldn't send: ") + r.Why; send.Enabled = true; }
                            });
                        }
                        catch (InvalidOperationException) { }   // the box was closed meanwhile: the result only goes to the log
                        Program.Log("report: " + (r.Ok ? "sent #" + r.Number : r.Queued ? "queued" : "not sent (" + r.Why + ")"));
                    });
                };
                f.Controls.AddRange(new Control[] { head, what, tech, preview, note, send, cancel, status });
                // AM-CG-216: reports kept while offline can be read and removed before they go
                if (Relay.Waiting > 0)
                {
                    // bug-hunt L-330: built from its parts, so the two links sit exactly on the words whatever the language
                    string lead = Relay.Waiting + " " + L.T("earlier report(s) waiting to send when you're online") + " - ", rd = L.T("Read"), dl = L.T("Delete");
                    var wl = new LinkLabel { Text = lead + rd + " · " + dl, AutoSize = true, Location = new Point(x, send.Bottom + Sc(10)), LinkColor = Theme.Blue };
                    wl.Links.Clear(); wl.Links.Add(lead.Length, rd.Length, "read"); wl.Links.Add(lead.Length + rd.Length + 3, dl.Length, "del");
                    wl.LinkClicked += delegate(object s2, LinkLabelLinkClickedEventArgs e2)
                    {
                        if ((string)e2.Link.LinkData == "read") AskCard.Show(f, Relay.WaitingText(), L.T("Waiting to send"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        else if (AskCard.Show(f, L.T("Delete the waiting report(s)? They won't be sent."), "AdeeboMeter", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK) { wl.Text = Relay.DropWaiting() ? L.T("Deleted - nothing is waiting.") : L.T("Couldn't delete them."); wl.Links.Clear(); }
                    };
                    f.Controls.Add(wl); f.ClientSize = new Size(f.ClientSize.Width, Math.Max(f.ClientSize.Height, wl.Bottom + Sc(14)));
                }
                f.ShowDialog(this);
            }
        }
        // the Hall of Fame card, sent the same way (the check runs by itself and the issue closes on a match)
        void SendCard()
        {
            if (!Relay.Ready) { try { Clipboard.SetText(HallOfFame.Text(awardName, awardTotal, awardDate, awardCode)); } catch (Exception e) { if (!(e is System.Runtime.InteropServices.ExternalException || e is System.Threading.ThreadStateException)) throw; } if (!Policy.NoOnline) Crash.OpenUrl(HallOfFame.Page); return; }
            var card = new System.Collections.Generic.Dictionary<string, string> { { "name", awardName }, { "points", awardTotal.ToString(System.Globalization.CultureInfo.InvariantCulture) }, { "date", awardDate }, { "code", awardCode } };
            pointsFlash = L.T("Sending your card..."); pointsFlashUntil = DateTime.Now.AddSeconds(30); SyncPointsBtn(); pointsCanvas.Invalidate();
            Relay.Send(Relay.Json("hof", "", "", card), r =>
            {
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        pointsFlash = r.Ok ? L.T("Card sent - you're on the Hall of Fame page in a few minutes if the code checks out.") : r.Queued ? L.T("No internet - the card goes the next time AdeeboMeter starts online.") : L.T("Couldn't send the card: ") + r.Why;
                        pointsFlashUntil = DateTime.Now.AddSeconds(10); SyncPointsBtn(); pointsCanvas.Invalidate();
                    });
                }
                catch (InvalidOperationException) { }
            });
        }
    }
}
