// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Your phone (Adeeb, 16 Nov: "Link icon to quickly connect to my phone ... merge and make it better")
    // One page for everything this PC does with your phone, opened by the link button in the top line (and the tray):
    //   left  - the link itself: the QR, the Allow question (on the page - no pop-up box), time left, End; remembered phones
    //   right - send to the phone (a file, the clipboard, or drop a file here), what came from it, the checks that need both
    //
    //   tray / link button ─► PhoneHub.Start ─► AppCtx.OpenRemote ─► Remote.Start ─► relay
    //   relay thread: phone asks ─► PhoneHub.Ask (waits ≤ 90 s) ─► this page shows Allow / Don't ─► PhoneHub.Answer
    sealed partial class FullView
    {
        Panel phonePage; DB phoneCanvas; LinkPill linkBtn; Timer phoneTick; Action phoneWatch;
        Bitmap phoneQr; string phoneQrFor = "";
        DateTime phoneCopied = DateTime.MinValue;

        // ---- screen peek: draw one of AdeeboMeter's pages off-screen (this is the hidden copy of the window the phone uses, so the
        // window you may be looking at never changes) and hand it over as a JPEG, at most 1200 px wide
        internal byte[] PeekPage(string page)
        {
            if (!Remote.PeekPages.Contains(page)) return null;
            if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
            var want = new Size(Sc(1280), Sc(860)); if (Size != want) Size = want;
            tab = page; ShowTab(); if (page == "toolbox") EnsureToolbox();
            Typing.Hold = true;
            try { return PeekShot(); } finally { Typing.Hold = false; }
        }
        byte[] PeekShot()
        {
            PerformLayout(); foreach (Control c in Descendants(this)) c.Invalidate();
            using (var bmp = new Bitmap(Math.Max(200, ClientSize.Width), Math.Max(200, ClientSize.Height)))
            {
                using (var g = Graphics.FromImage(bmp)) { g.Clear(BackColor); PeekDraw(this, g, Point.Empty, new Rectangle(Point.Empty, bmp.Size)); }
                Bitmap outB = bmp; Bitmap scaled = null;
                if (bmp.Width > 1200) { scaled = new Bitmap(1200, bmp.Height * 1200 / bmp.Width); using (var g = Graphics.FromImage(scaled)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(bmp, 0, 0, scaled.Width, scaled.Height); } outB = scaled; }
                try
                {
                    var enc = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(e => e.MimeType == "image/jpeg");
                    using (var ms = new MemoryStream())
                    {
                        if (enc != null) using (var ps = new System.Drawing.Imaging.EncoderParameters(1)) { ps.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 72L); outB.Save(ms, enc, ps); }
                        else outB.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        return ms.ToArray();
                    }
                }
                finally { if (scaled != null) scaled.Dispose(); }
            }
        }
        // Our pages are painted by their own OnPaint, so peek paints each visible control into the picture itself (back to
        // front, each clipped to its parent) - DrawToBitmap leaves them blank on some systems. Native boxes (text, lists)
        // that draw nothing in OnPaint fall back to DrawToBitmap for just themselves.
        void PeekDraw(Control c, Graphics g, Point at, Rectangle clip)
        {
            var r = new Rectangle(at, c.ClientSize); clip = Rectangle.Intersect(clip, r); if (clip.Width <= 0 || clip.Height <= 0) return;
            var st = g.Save();
            try
            {
                g.SetClip(clip); g.TranslateTransform(at.X, at.Y);
                bool native = c is TextBoxBase || c is ListBox || c is ComboBox || c is WebBrowser || c is DataGridView || c is ListView || c is TreeView
                    || c is Label || c is ButtonBase;   // labels and buttons draw text with GDI (TextRenderer), which ignores the offset: in the phone picture they landed top-left (screenshots 21 Nov)
                if (native)
                {
                    try { using (var b = new Bitmap(Math.Max(1, c.Width), Math.Max(1, c.Height))) { c.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height)); g.DrawImageUnscaled(b, 0, 0); } }
                    catch (Exception e) { if (e is OutOfMemoryException) throw; }
                }
                else
                {
                    var lc = new Rectangle(clip.X - at.X, clip.Y - at.Y, clip.Width, clip.Height);
                    using (var pe = new PaintEventArgs(g, lc)) { try { InvokePaintBackground(c, pe); InvokePaint(c, pe); } catch (Exception e) { if (e is OutOfMemoryException) throw; } }
                }
            }
            finally { g.Restore(st); }
            if (c is TextBoxBase || c is ListBox || c is ComboBox || c is WebBrowser) return;
            for (int i = c.Controls.Count - 1; i >= 0; i--)
            {
                var k = c.Controls[i]; if (!k.Visible || k.Width <= 0 || k.Height <= 0) continue;
                var o = k.Location;
                PeekDraw(k, g, new Point(at.X + o.X, at.Y + o.Y), clip);
            }
        }
        Panel BuildPhonePage()
        {
            phonePage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            phoneCanvas = new DB { Name = "phoneCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, AllowDrop = true };
            phoneCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintPhone(e.Graphics); }; Wire(phoneCanvas);
            // drop a file anywhere on the page: it goes to the linked phone
            phoneCanvas.DragEnter += (s, e) => { e.Effect = PhoneOn() && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; };
            phoneCanvas.DragDrop += (s, e) => { var f = e.Data.GetData(DataFormats.FileDrop) as string[]; var r = Remote.Now; if (f != null && f.Length > 0 && r != null && r.St == Remote.State.Paired && File.Exists(f[0])) r.SendFile(f[0]); };
            phonePage.Controls.Add(phoneCanvas);
            // the link button, left of the buddy in the top line
            if (header != null) { linkBtn = new LinkPill(); linkBtn.Click += delegate { LinkClicked(); }; header.Controls.Add(linkBtn); linkBtn.Sync(); }
            phoneWatch = delegate { try { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)PhoneChanged); } catch (InvalidOperationException) { } };
            PhoneHub.Watch(phoneWatch); Disposed += delegate { PhoneHub.Unwatch(phoneWatch); if (phoneQr != null) phoneQr.Dispose(); };
            // countdowns (time left, a question's 90 s) and the button's minutes
            string phoneKey = "";
            phoneTick = new Timer { Interval = 1000 }; phoneTick.Tick += delegate { if (linkBtn != null && linkBtn.Sync()) PlaceTop(); if (tab == "phone" && Visible) { var rn = Remote.Now; string pk = (PhoneHub.Pending != null ? "ask" + DateTime.UtcNow.Second : "") + "|" + (rn == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(rn)) + "|" + DateTime.Now.Minute + "|" + (phoneCopied > DateTime.Now.AddSeconds(-3)); if (pk != phoneKey) { phoneKey = pk; phoneCanvas.Invalidate(); } } };   /* 21 Nov sitting test: every second only while a question counts down - not always */ phoneTick.Start();
            Disposed += delegate { phoneTick.Stop(); phoneTick.Dispose(); };
            return phonePage;
        }
        void PhoneChanged() { if (linkBtn != null && linkBtn.Sync()) PlaceTop(); if (phoneCanvas != null) phoneCanvas.Invalidate(); if (sideNav != null && sideNav.Visible) sideNav.Invalidate(); }
        static bool PhoneOn() { var r = Remote.Now; return r != null && r.St == Remote.State.Paired; }
        void LinkClicked()
        {
            var r = Remote.Now; var st = PhoneHub.Pending;
            // not linked and no code showing: one click shows the code; otherwise just the page
            if ((r == null || r.St == Remote.State.Ended) && st == null && PhoneHub.Start != null) PhoneHub.Start(); else ShowPage("phone");
        }

        // ---- painting helpers: a button, a card title
        RectangleF PhBtn(Graphics g, float x, float y, string t, int kind, Action a)   // kind 0 plain, 1 blue, 2 red, 3 green
        {
            var ts = g.MeasureString(t, Draw.fBodyB); var r = new RectangleF(x, y, ts.Width + 32 * S, 40 * S);
            Color bg = kind == 1 ? Theme.Blue : kind == 2 ? Theme.Red : kind == 3 ? Theme.Green : Theme.Soft;
            Theme.Fill(g, r, 12 * S, bg); if (kind == 0) Theme.Stroke(g, r, 12 * S, Theme.Line, 1);
            using (var b = new SolidBrush(kind == 0 ? Theme.Ink : Color.White)) g.DrawString(t, Draw.fBodyB, b, r.X + 16 * S, r.Y + (40 * S - ts.Height) / 2);
            Act(phoneCanvas, r, a); return r;
        }
        float PhHead(Graphics g, string t, float x, float y) { using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T(t), Draw.fTiny, m, x, y); return y + 22 * S; }
        float PhText(Graphics g, string t, Font f, Color c, float x, float y, float w) { var h = Draw.Measure(t, f, (int)w).Height; using (var b = new SolidBrush(c)) g.DrawString(t, f, b, new RectangleF(x, y, w, h + 4)); return y + h + 6 * S; }
        static string Mmss(TimeSpan l) { return ((int)l.TotalMinutes).ToString(CultureInfo.CurrentCulture) + ":" + l.Seconds.ToString("00", CultureInfo.InvariantCulture); }

        void PaintPhone(Graphics g)
        {
            Begin(phoneCanvas);
            g.TranslateTransform(phoneCanvas.AutoScrollPosition.X, phoneCanvas.AutoScrollPosition.Y);
            float W = phoneCanvas.ClientSize.Width, x = 32 * S, y = 24 * S, w = W - 64 * S;
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(L.T("Your phone"), Draw.fTitle, ink, x, y);
            y += 36 * S; y = PhText(g, L.T("Everything this PC does with your phone, in one place. Sealed end to end: the relay passes on messages it can't read."), Draw.fBody, Theme.Sub, x, y, w) + 8 * S;
            bool two = w > 900 * S; float lw = two ? Math.Min(440 * S, w * 0.42f) : w, rx = two ? x + lw + 24 * S : x, rw = two ? w - lw - 24 * S : w;
            float ly = PaintPhoneLink(g, x, y, lw);
            float ry = PaintPhoneUse(g, rx, two ? y : ly + 8 * S, rw);
            Need(phoneCanvas, Math.Max(ly, ry) + 30 * S);
        }

        // ---- left: the question, the link, remembered phones
        float PaintPhoneLink(Graphics g, float x, float y, float w)
        {
            var r = Remote.Now; var q = PhoneHub.Pending;
            // a phone is asking - the answer is here, on the page
            if (q != null)
            {
                string title = q.Kind == "file" ? L.T("Accept a file from the phone?") : q.Kind == "remember" ? L.T("Remember this phone?") : L.T("Allow this phone?");
                string body = (q.Phone != "" ? q.Phone : L.T("A phone")) + " " + q.Text;
                // matching words (RemoteWords.cs): allow only if your phone shows the same four
                bool allow = q.Kind == "allow", hasW = allow && q.Words != "";
                string wHint = hasW ? L.T("Look at the phone in your own hand: does it show the same words?") : allow ? L.T("This phone page can't show matching words (it's an older version). Allow it only if it's your phone and you just scanned the code.") : "";
                string wAfter = hasW ? L.T("Different words, no words on your phone, or you didn't just scan the code? Press Don't allow - it isn't your phone.") : "";
                var fW = Draw.fBig;
                int left = Math.Max(0, PhoneHub.AskSeconds - (int)(DateTime.UtcNow - q.At).TotalSeconds);
                float bh = Draw.Measure(body, Draw.fBody, (int)(w - 40 * S)).Height;
                float wh = wHint == "" ? 0 : Draw.Measure(wHint, Draw.fBody, (int)(w - 40 * S)).Height + 10 * S;
                if (hasW) wh += 62 * S + Draw.Measure(wAfter, Draw.fBody, (int)(w - 40 * S)).Height + 10 * S;
                var card = new RectangleF(x, y, w, 20 * S + 28 * S + bh + 12 * S + wh + 22 * S + 52 * S);
                Theme.Fill(g, card, 18 * S, Theme.LvSoft(1)); Theme.Stroke(g, card, 18 * S, Theme.Amber, 1.5f);
                float cy = y + 18 * S;
                using (var b = new SolidBrush(Theme.LvText(1))) g.DrawString(title, Draw.fHead, b, x + 20 * S, cy); cy += 30 * S;
                cy = PhText(g, body, Draw.fBody, Theme.Ink, x + 20 * S, cy, w - 40 * S);
                if (wHint != "") { cy += 10 * S; cy = PhText(g, wHint, Draw.fBody, hasW ? Theme.Ink : Theme.LvText(1), x + 20 * S, cy, w - 40 * S); }
                if (hasW)
                {
                    var wb = new RectangleF(x + 20 * S, cy + 8 * S, w - 40 * S, 50 * S);
                    Theme.Fill(g, wb, 12 * S, Theme.Soft); Theme.Stroke(g, wb, 12 * S, Theme.Line, 1f);
                    using (var b = new SolidBrush(Theme.Ink)) using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(q.Words, fW, b, wb, sf);
                    cy = wb.Bottom + 12 * S;
                    cy = PhText(g, wAfter, Draw.fBody, Theme.Sub, x + 20 * S, cy, w - 40 * S);
                }
                using (var m = new SolidBrush(Theme.Sub)) g.DrawString(L.T("No answer in") + " " + left + " s " + L.T("means no."), Draw.fSmall, m, x + 20 * S, cy); cy += 26 * S;
                var yes = PhBtn(g, x + 20 * S, cy, q.Kind == "file" ? L.T("Accept") : q.Kind == "remember" ? L.T("Remember it") : L.T("Allow"), 1, delegate { PhoneHub.Answer(true); });
                PhBtn(g, yes.Right + 10 * S, cy, q.Kind == "file" ? L.T("Don't accept") : q.Kind == "remember" ? L.T("Not now") : L.T("Don't allow"), 0, delegate { PhoneHub.Answer(false); });
                y = card.Bottom + 16 * S;
            }
            // the link
            var st = r == null ? Remote.State.Ended : r.St;
            var lc = new RectangleF(x, y, w, 10); float ly = y + 20 * S, ix = x + 22 * S, iw = w - 44 * S;
            if (st == Remote.State.Paired)
            {
                using (var d = new SolidBrush(Theme.Green)) g.FillEllipse(d, ix, ly + 7 * S, 10 * S, 10 * S);
                using (var b = new SolidBrush(Theme.Ink)) g.DrawString(L.T("Linked to") + " " + (r.PhoneName != "" ? r.PhoneName : L.T("your phone")), Draw.fHead, b, new RectangleF(ix + 18 * S, ly, iw - 18 * S, 28 * S)); ly += 32 * S;
                ly = PhText(g, Mmss(r.Left) + " " + L.T("left") + " · " + (r.Known != null ? L.T("a remembered phone - it proved its key") : L.T("linked with the code")) + ". " + L.T("It can watch this PC and start checks - never change Windows settings."), Draw.fBody, Theme.Sub, ix, ly, iw);
                ly += 6 * S; var end = PhBtn(g, ix, ly, L.T("End now"), 2, delegate { var rr = Remote.Now; if (rr != null) rr.End("you ended it on this PC"); PhoneHub.Fire(); }); ly = end.Bottom;
            }
            else if (st == Remote.State.Waiting || st == Remote.State.Connecting || st == Remote.State.Asking)
            {
                if (st == Remote.State.Waiting && r.Link != "")
                {
                    if (phoneQrFor != r.Link) { if (phoneQr != null) phoneQr.Dispose(); var m = Qr.Encode(r.Link); phoneQr = m != null ? Qr.Picture(m, 8) : null; phoneQrFor = r.Link; }
                    float qs = Math.Min(260 * S, iw); var qr = new RectangleF(x + (w - qs) / 2, ly, qs, qs);
                    Theme.Fill(g, qr, 14 * S, Color.White);
                    if (phoneQr != null) { var im = g.InterpolationMode; g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(phoneQr, RectangleF.Inflate(qr, -12 * S, -12 * S)); g.InterpolationMode = im; g.PixelOffsetMode = PixelOffsetMode.Default; }
                    ly = qr.Bottom + 14 * S;
                    ly = PhText(g, L.T("1. Point your phone's camera at the code - it opens adeebometer.com/app.") + "\n" + L.T("2. Allow it here when it asks."), Draw.fBody, Theme.Ink, ix, ly, iw);
                    using (var m = new SolidBrush(Theme.Sub)) g.DrawString(L.T("This code works for") + " " + Mmss(r.Left), Draw.fSmall, m, ix, ly); ly += 26 * S;
                    var nb = PhBtn(g, ix, ly, L.T("New code"), 0, delegate { var rr = Remote.Now; if (rr != null) rr.End("a new code"); if (PhoneHub.Start != null) PhoneHub.Start(); });
                    string link = r.Link; var cb = PhBtn(g, nb.Right + 10 * S, ly, phoneCopied > DateTime.Now.AddSeconds(-3) ? L.T("Copied ✓") : L.T("Copy link"), 0, delegate { try { Clipboard.SetText(link); phoneCopied = DateTime.Now; } catch (System.Runtime.InteropServices.ExternalException) { } phoneCanvas.Invalidate(); });
                    PhBtn(g, cb.Right + 10 * S, ly, L.T("Cancel"), 0, delegate { var rr = Remote.Now; if (rr != null) rr.End("cancelled"); PhoneHub.Fire(); }); ly += 40 * S;
                }
                else
                {
                    using (var b = new SolidBrush(Theme.Ink)) g.DrawString(st == Remote.State.Asking ? L.T("A phone is asking to link...") : L.T("Connecting to the relay..."), Draw.fHead, b, ix, ly); ly += 34 * S;
                    ly = PhText(g, L.T("A few seconds."), Draw.fBody, Theme.Sub, ix, ly, iw);
                }
            }
            else
            {
                using (var b = new SolidBrush(Theme.Ink)) g.DrawString(L.T("Link your phone"), Draw.fHead, b, ix, ly); ly += 32 * S;
                ly = PhText(g, L.T("See this PC and run its checks from your phone - on mobile data or any Wi-Fi. No account. One phone at a time, 30 minutes, and nothing happens until you press Allow here."), Draw.fBody, Theme.Sub, ix, ly, iw);
                if (r != null && r.Why != "" && r.Why != "you ended it on this PC") ly = PhText(g, L.T("Last session ended") + ": " + L.T(r.Why), Draw.fSmall, Theme.Muted, ix, ly, iw);
                ly += 4 * S; var sb = PhBtn(g, ix, ly, L.T("Show the QR code"), 1, delegate { if (PhoneHub.Start != null) PhoneHub.Start(); }); ly = sb.Bottom;
            }
            lc.Height = ly + 20 * S - y;
            // the frame goes on once the height is known (a frame only, so it never covers the content)
            PhCard(g, lc, st == Remote.State.Paired ? Theme.Green : st == Remote.State.Asking ? Theme.Amber : Theme.Line);
            y = lc.Bottom + 16 * S;

            // remembered phones
            List<RemotePhones.P> ph; try { ph = RemotePhones.All(); } catch (Exception e) { if (e is OutOfMemoryException) throw; ph = new List<RemotePhones.P>(); }
            var rc = new RectangleF(x, y, w, 10); float ry = PhHead(g, "REMEMBERED PHONES", x + 22 * S, y + 18 * S);
            if (RemotePhones.Unreadable > 0) ry = PhText(g, RemotePhones.Unreadable + " " + L.T("remembered phone(s) couldn't be read (the list was damaged, or this is another Windows account) - remember them again from the phone."), Draw.fBody, Theme.LvText(1), x + 22 * S, ry, w - 44 * S);
            if (ph.Count == 0) ry = PhText(g, L.T("None yet. Once linked, a phone can ask to be remembered (Face ID or a fingerprint) and come back without a code."), Draw.fBody, Theme.Sub, x + 22 * S, ry, w - 44 * S);
            foreach (var p in ph)
            {
                using (var b = new SolidBrush(Theme.Ink)) g.DrawString(p.Name != "" ? p.Name : L.T("A phone"), Draw.fBodyB, b, x + 22 * S, ry);
                using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T("added") + " " + p.Added.ToLocalTime().ToString("d MMM", CultureInfo.CurrentCulture) + (r != null && r.Known != null && r.Known.Id == p.Id && r.St == Remote.State.Paired ? " · " + L.T("linked now") : ""), Draw.fSmall, m, x + 22 * S, ry + 20 * S);
                string rm = L.T("Remove"); var rz = g.MeasureString(rm, Draw.fBodyB); var rr = new RectangleF(x + w - 22 * S - rz.Width, ry + 8 * S, rz.Width, rz.Height);
                using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(rm, Draw.fBodyB, lb, rr.X, rr.Y);
                string id = p.Id; Act(phoneCanvas, rr, delegate { RemotePhones.Remove(id); var now = Remote.Now; if (now != null && now.Known != null && now.Known.Id == id) now.End("forgotten"); PhoneHub.Fire(); });
                ry += 48 * S;
            }
            // bug-hunt ML-111: the remembered phones' waiting room is your choice - off, nothing goes to the relay until you make a code
            if (ph.Count > 0)
            {
                string sw = (Cfg.PhoneStandby ? L.T("On") : L.T("Off")) + " · " + L.T("Let remembered phones reconnect while AdeeboMeter runs");
                var sr = new RectangleF(x + 22 * S, ry + 4 * S, w - 44 * S, 22 * S);
                using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(sw, Draw.fBodyB, lb, sr);
                Act(phoneCanvas, sr, delegate { Cfg.PhoneStandby = !Cfg.PhoneStandby; Cfg.Save(); PhoneHub.Fire(); });
                ry += 32 * S;
            }
            rc.Height = ry + 14 * S - y; Theme.Stroke(g, rc, 18 * S, Theme.Line, 1);
            return rc.Bottom + 16 * S;
        }
        // a locked .adeebo file to open: Open an encrypted file, with the file filled in - the code is yours to type (bug-hunt ML-127)
        internal void OpenLocked(string path)
        {
            tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool("decrypt");
            if (tbBoxes.Count > 0 && tbBoxes[0] != null) { tbBoxes[0].Box.Text = path; if (tbBoxes.Count > 1 && tbBoxes[1] != null) tbBoxes[1].Box.Focus(); }
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate();
        }
        // a card's frame and a coloured left edge (drawn after its content, so it never covers it)
        void PhCard(Graphics g, RectangleF r, Color edge)
        {
            Theme.Stroke(g, r, 18 * S, edge == Theme.Line ? Theme.Line : edge, edge == Theme.Line ? 1 : 1.5f);
        }

        // ---- right: send, what came, the checks that need both
        float PaintPhoneUse(Graphics g, float x, float y, float w)
        {
            bool on = PhoneOn(); var r = Remote.Now;
            // send to the phone
            var c1 = new RectangleF(x, y, w, 10); float cy = PhHead(g, "SEND TO THE PHONE", x + 22 * S, y + 18 * S), iw = w - 44 * S, ix = x + 22 * S;
            if (on)
            {
                var fb = PhBtn(g, ix, cy, L.T("A file..."), 1, delegate { using (var d = new OpenFileDialog { Title = L.T("Send a file to the phone (up to 100 MB)") }) if (d.ShowDialog(this) == DialogResult.OK) { var rr = Remote.Now; if (rr != null && rr.St == Remote.State.Paired) rr.SendFile(d.FileName); } });
                PhBtn(g, fb.Right + 10 * S, cy, L.T("What's on my clipboard"), 0, delegate
                {
                    string t = ""; try { t = Clipboard.ContainsText() ? Clipboard.GetText() : ""; } catch (System.Runtime.InteropServices.ExternalException) { }
                    var rr = Remote.Now; if (t.Trim() == "") PhoneHub.Xfer = L.T("The clipboard has no text."); else if (rr != null && rr.St == Remote.State.Paired) { rr.SendText(t); PhoneHub.Xfer = L.T("Sent the clipboard text to the phone."); }
                    phoneCanvas.Invalidate();
                });
                cy += 48 * S; cy = PhText(g, L.T("Or drop a file anywhere on this page · up to 100 MB · any tool's result has To my phone while you're linked."), Draw.fSmall, Theme.Sub, ix, cy, iw);
                if (PhoneHub.Xfer != "") cy = PhText(g, PhoneHub.Xfer, Draw.fBodyB, Theme.Ink, ix, cy, iw);
            }
            else cy = PhText(g, L.T("Link a phone first - then send it a file, a link or any result."), Draw.fBody, Theme.Sub, ix, cy, iw);
            c1.Height = cy + 12 * S - y; Theme.Stroke(g, c1, 18 * S, Theme.Line, 1); y = c1.Bottom + 16 * S;

            // what came from the phone (this run only)
            var c2 = new RectangleF(x, y, w, 10); cy = PhHead(g, "FROM THE PHONE", ix, y + 18 * S);
            var got = PhoneHub.Inbox;
            // a locked file and its code both came from the phone (Adeeb, 16 Nov: "yes do that"): open it in one click -
            // Open an encrypted file, filled in and started. The code is only the phone's "XXXX-XXXX-...-XXXX" kind.
            var lockedF = got.FirstOrDefault(z => z.File != "" && z.File.EndsWith(".adeebo", StringComparison.OrdinalIgnoreCase) && File.Exists(z.File));
            // bug-hunt ML-117/118: the code read the way Open an encrypted file reads it (any case, spaces or dashes), and the one sent
            // nearest in time to that file - within 10 minutes - so a file and a code from two different sends aren't paired
            var codeT = lockedF == null ? null : got.Where(z => z.File == "" && Regex.IsMatch(FileCrypt.Clean(z.Text), "^[A-Z0-9]{24}$") && Math.Abs((z.T - lockedF.T).TotalMinutes) <= 10).OrderBy(z => Math.Abs((z.T - lockedF.T).TotalSeconds)).FirstOrDefault();
            if (lockedF != null && codeT != null)
            {
                var oc = new RectangleF(ix, cy, iw, 64 * S); Theme.Fill(g, oc, 14 * S, Theme.LvSoft(0)); Theme.Stroke(g, oc, 14 * S, Theme.Green, 1.5f);
                Kit.Icon(g, "key", new RectangleF(oc.X + 14 * S, oc.Y + 22 * S, 20 * S, 20 * S), Theme.LvText(0));
                using (var b = new SolidBrush(Theme.LvText(0))) g.DrawString(L.T("A locked file and its code came from your phone"), Draw.fBodyB, b, oc.X + 44 * S, oc.Y + 10 * S);
                using (var m = new SolidBrush(Theme.Sub)) using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(Path.GetFileName(lockedF.File), Draw.fSmall, m, new RectangleF(oc.X + 44 * S, oc.Y + 34 * S, oc.Width - 200 * S, 20 * S), nf);
                string lf = lockedF.File, lc = codeT.Text.Trim();
                PhBtn(g, oc.Right - 150 * S, oc.Y + 12 * S, L.T("Open it now"), 3, delegate
                {
                    tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool("decrypt");
                    if (tbBoxes.Count > 1 && tbBoxes[0] != null && tbBoxes[1] != null) { tbBoxes[0].Box.Text = lf; tbBoxes[1].Box.Text = lc; if (!tbBusy) StartTool(); }
                });
                cy += 74 * S;
            }
            if (got.Count == 0) cy = PhText(g, L.T("Nothing yet. On the phone: PC › Files › Send to PC. Files land in Downloads\\AdeeboMeter after you accept them."), Draw.fBody, Theme.Sub, ix, cy, iw);
            foreach (var it in got.Take(5))
            {
                bool file = it.File != ""; string what = file ? Path.GetFileName(it.File) : Regex.Replace(it.Text, @"\s+", " ");
                using (var b = new SolidBrush(Theme.Ink)) using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString((file ? "📄 " : "“") + what + (file ? "" : "”"), Draw.fBodyB, b, new RectangleF(ix, cy, iw, 22 * S), nf);
                cy += 22 * S; float lx = ix;
                Action<string, Action> lnk = (t, a) => { var tz = g.MeasureString(t, Draw.fSmall); var lr = new RectangleF(lx, cy, tz.Width, tz.Height); using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(t, Draw.fSmall, lb, lr.X, lr.Y); Act(phoneCanvas, lr, a); lx = lr.Right + 16 * S; };
                using (var m = new SolidBrush(Theme.Muted)) { string at = it.T.ToString("t", CultureInfo.CurrentCulture); g.DrawString(at, Draw.fSmall, m, lx, cy); lx += g.MeasureString(at, Draw.fSmall).Width + 16 * S; }
                var item = it;
                if (file) lnk(L.T("Show in folder"), delegate { try { if (File.Exists(item.File)) System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + item.File + "\""); else System.Diagnostics.Process.Start("explorer.exe", Remote.DropDir); } catch (Exception e) { if (e is OutOfMemoryException) throw; } });
                else
                {
                    lnk(L.T("Copy"), delegate { try { Clipboard.SetText(item.Text); } catch (System.Runtime.InteropServices.ExternalException) { } });
                    string u = item.Text.Trim(); if (Regex.IsMatch(u, @"^https?://\S+$")) lnk(L.T("Open link"), delegate { try { System.Diagnostics.Process.Start(u); } catch (Exception e) { if (e is OutOfMemoryException) throw; } });
                }
                cy += 26 * S;
            }
            // put the phone's text on this PC's clipboard - off unless ticked, this run only
            var tk = new RectangleF(ix, cy + 4 * S, 18 * S, 18 * S); Theme.Fill(g, tk, 4 * S, PhoneHub.AutoClip ? Theme.Blue : Theme.Card); Theme.Stroke(g, tk, 4 * S, PhoneHub.AutoClip ? Theme.Blue : Theme.Line, 1);
            if (PhoneHub.AutoClip) Kit.Icon(g, "check", RectangleF.Inflate(tk, -3 * S, -3 * S), Color.White);
            using (var s = new SolidBrush(Theme.Sub)) g.DrawString(L.T("Put text from my phone on this PC's clipboard (this time only)"), Draw.fSmall, s, tk.Right + 8 * S, cy + 4 * S);
            Act(phoneCanvas, new RectangleF(ix, cy, iw, 26 * S), delegate { PhoneHub.AutoClip = !PhoneHub.AutoClip; phoneCanvas.Invalidate(); });
            cy += 32 * S;
            c2.Height = cy + 8 * S - y; Theme.Stroke(g, c2, 18 * S, Theme.Line, 1); y = c2.Bottom + 16 * S;

            // the checks that need both
            var c3 = new RectangleF(x, y, w, 10); cy = PhHead(g, "CHECKS THAT NEED BOTH", ix, y + 18 * S);
            foreach (var id in new[] { "phonevs", "phonedown", "phoneweek" })
            {
                var t = Tools2().FirstOrDefault(z => z.Id == id); if (t == null) continue;
                var rr = new RectangleF(ix, cy, iw, 44 * S); Theme.Fill(g, rr, 12 * S, Theme.Soft);
                using (var b = new SolidBrush(Theme.Ink)) g.DrawString(L.T(t.Title), Draw.fBodyB, b, rr.X + 14 * S, rr.Y + 12 * S);
                using (var m = new SolidBrush(Theme.Muted)) { string sfx = on ? "›" : L.T("needs a linked phone"); var sz = g.MeasureString(sfx, Draw.fSmall); g.DrawString(sfx, Draw.fSmall, m, rr.Right - sz.Width - 14 * S, rr.Y + 14 * S); }
                string tid = id; Act(phoneCanvas, rr, delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(tid); });
                cy += 52 * S;
            }
            // the phone's own tools, run on the phone from here - two columns of short names
            cy = PhHead(g, "RUN ON YOUR PHONE", ix, cy + 6 * S);
            var ph = Tools2().Where(z => z.Id.StartsWith("ph", StringComparison.Ordinal) && z.Id != "phonevs" && z.Id != "phonedown" && z.Id != "phoneweek").ToList();
            int pc2 = iw > 520 * S ? 2 : 1; float pw = (iw - (pc2 - 1) * 8 * S) / pc2;
            for (int i = 0; i < ph.Count; i++)
            {
                var t = ph[i]; var rr = new RectangleF(ix + (i % pc2) * (pw + 8 * S), cy + (i / pc2) * 40 * S, pw, 34 * S); Theme.Fill(g, rr, 10 * S, Theme.Soft);
                string nm = Regex.Replace(L.T(t.Title), @"^On my phone:\s*", ""); if (nm.Length > 0) nm = char.ToUpperInvariant(nm[0]) + nm.Substring(1);   /* L-229 */
                using (var b = new SolidBrush(on ? Theme.Ink : Theme.Muted)) using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(nm, Draw.fBody, b, new RectangleF(rr.X + 12 * S, rr.Y + 8 * S, rr.Width - 20 * S, 20 * S), nf);
                string tid = t.Id; Act(phoneCanvas, rr, delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(tid); });
            }
            cy += ((ph.Count + pc2 - 1) / pc2) * 40 * S + 6 * S;
            cy = PhText(g, L.T("The phone runs these with its own network and shows a note when it does. A phone's browser stops in the background, so keep AdeeboMeter open on it. Not from here: anything needing someone at the phone - the camera, files, walking about, switching networks."), Draw.fSmall, Theme.Sub, ix, cy, iw);
            cy = PhText(g, L.T("Signal journey lives on the phone (Tools › Signal journey): record its field-test screen while you move. Copy the report there and Send to PC - it lands under From the phone."), Draw.fSmall, Theme.Sub, ix, cy + 2 * S, iw);
            c3.Height = cy + 10 * S - y; Theme.Stroke(g, c3, 18 * S, Theme.Line, 1);
            return c3.Bottom + 16 * S;
        }
    }

    // the link button in the top line: off (just the icon) · Waiting (blue) · Allow? (amber) · iPhone · 24 min (green)
    sealed class LinkPill : Control
    {
        string state = "", label = ""; bool hot;
        public LinkPill()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            Cursor = Cursors.Hand; TabStop = true; AccessibleRole = AccessibleRole.PushButton; AccessibleName = L.T("Your phone");
            Height = (int)(36 * Draw.S);
        }
        // true when the size changed (the top line places it again)
        public bool Sync()
        {
            string lb; string st = PhoneHub.StateOf(out lb);
            if (st == state && lb == label) return false;
            state = st; label = lb; AccessibleName = L.T("Your phone") + (lb != "" ? " - " + lb : "");
            int w = st == "off" ? (int)(36 * Draw.S) : (int)(TextRenderer.MeasureText(lb, Draw.fBodyB).Width + 44 * Draw.S);
            bool resized = w != Width; Width = w; Invalidate(); return resized;
        }
        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; OnClick(EventArgs.Empty); } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; float S = Draw.S; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = Draw.WindowText;
            using (var b = new SolidBrush(Parent != null ? Parent.BackColor : BackColor)) g.FillRectangle(b, ClientRectangle);
            var r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
            Color acc = state == "on" ? Theme.Green : state == "ask" ? Theme.Amber : state == "wait" ? Theme.Blue : Theme.Line;
            Color ink = state == "on" ? Theme.LvText(0) : state == "ask" ? Theme.LvText(1) : state == "wait" ? (Theme.Dark ? Theme.BlueDark : Theme.Blue) : hot ? Kit.Accent("home") : Theme.Sub;
            if (state == "off") { using (var f = new SolidBrush(hot ? Theme.Soft : Theme.Card)) g.FillEllipse(f, r); using (var p = new Pen(Focused ? Kit.Accent("home") : Theme.Line, Focused ? 1.5f * S : 1)) g.DrawEllipse(p, r); }
            else { Theme.Fill(g, r, 12 * S, state == "on" ? Theme.LvSoft(0) : state == "ask" ? Theme.LvSoft(1) : Theme.BlueSoft); Theme.Stroke(g, r, 12 * S, acc, Focused ? 2 : 1); }
            float ix = state == "off" ? (Width - 20 * S) / 2 : 10 * S;
            Kit.Icon(g, "link", new RectangleF(ix, (Height - 20 * S) / 2, 20 * S, 20 * S), ink);
            if (state != "off") using (var b = new SolidBrush(ink)) { var ts = g.MeasureString(label, Draw.fBodyB); g.DrawString(label, Draw.fBodyB, b, 34 * S, (Height - ts.Height) / 2); }
        }
    }
}
