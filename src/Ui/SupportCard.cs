// Community and support (1.1.6, Adeeb 22 Nov: "a small heart at the bottom of the sidebar that opens community and support -
// very designed and good, so people feel the urge to donate by themselves"). An honest card, no guilt and no made-up numbers:
// who makes it, what it promises, what a donation actually pays for, the amounts, and the free ways to help.
// Every link opens in the browser - AdeeboMeter itself sends nothing. Esc or a click outside the buttons' row closes it.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AdeeboMeter
{
    sealed class SupportCard : Form
    {
        static float S { get { return Draw.S; } }
        static readonly Color Pink = Color.FromArgb(0xE6, 0x49, 0x80), PinkDeep = Color.FromArgb(0xC2, 0x25, 0x5C);
        sealed class Hit { public RectangleF R; public Action Go; public string Key; }
        readonly List<Hit> hits = new List<Hit>(); string hot = "", picked = "10";
        readonly Action<string> feedback;   // the app's own feedback / bug boxes ("feedback", "bug")

        public static void Open(IWin32Window owner, Action<string> feedback)
        {
            try { using (var c = new SupportCard(feedback)) { var o = owner as Control; if (o != null && (o.IsDisposed || !o.IsHandleCreated)) owner = null; if (owner != null) c.ShowDialog(owner); else c.ShowDialog(); } }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("support card: " + e.Message); }
        }
        SupportCard(Action<string> feedback)
        {
            this.feedback = feedback;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; KeyPreview = true;
            BackColor = Theme.Card; Font = Draw.fBody; Text = L.T("Community and support"); DoubleBuffered = true;
            /* review 1.1.6 U4 #5: never taller than the screen - the rest scrolls with the wheel */
            float full = Measure(); int capH = (int)(Screen.FromPoint(Cursor.Position).WorkingArea.Height - 24 * S);
            ClientSize = new Size((int)(640 * S), (int)Math.Min(full + 0.5f, capH)); maxScroll = Math.Max(0, full - ClientSize.Height); Draw.FitOnShow(this, false);   // on screen, so its close button is reachable
            using (var p = Theme.Round(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 18 * S)) Region = new Region(p);
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x00020000; return cp; } }   // CS_DROPSHADOW

        // the amounts: a word for what each feels like - never a claim about what it buys
        static readonly string[][] Amounts = { new[] { "5", "A coffee" }, new[] { "10", "A thank-you" }, new[] { "20", "Fuel a feature" }, new[] { "50", "Champion" } };
        static string Url(string amt) { return amt == "" ? "https://paypal.me/adeeb1111" : "https://paypal.me/adeeb1111/" + amt + "CAD"; }

        float Measure() { using (var bmp = new Bitmap(1, 1)) using (var g = Graphics.FromImage(bmp)) return Lay(g, false); }
        float scrollY, maxScroll;
        Point At(Point p) { return new Point(p.X, p.Y + (int)scrollY); }
        protected override void OnMouseWheel(MouseEventArgs e) { if (maxScroll > 0) { scrollY = Math.Max(0, Math.Min(maxScroll, scrollY - e.Delta / 120f * 48 * S)); Invalidate(); } base.OnMouseWheel(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = Draw.WindowText; g.TextContrast = Draw.Contrast;
            g.TranslateTransform(0, -scrollY); Lay(g, true); g.ResetTransform();
            if (maxScroll > 0) using (var sb = new SolidBrush(Color.FromArgb(90, Theme.Muted))) { float th = ClientSize.Height * ClientSize.Height / (ClientSize.Height + maxScroll); g.FillRectangle(sb, ClientSize.Width - 5 * S, (ClientSize.Height - th) * scrollY / maxScroll, 3 * S, th); }
        }

        float Lay(Graphics g, bool draw)
        {
            hits.Clear();
            float W = 640 * S, pad = 30 * S, x = pad, w = W - 2 * pad, y = 0;
            Color ink = Theme.Ink, sub = Theme.Sub, mut = Theme.Muted, warm = Theme.Dark ? Color.FromArgb(0x2A, 0x1A, 0x26) : Color.FromArgb(0xFF, 0xF1, 0xF5);
            Func<string, Font, float, float> H = (t, f, ww) => Draw.Measure(t, f, (int)ww).Height;

            // ---- the warm top: a heart, the ask in one line, who makes it
            float topH = 0;
            {
                string t1 = L.T("Keep AdeeboMeter free"), t2 = L.T("One person builds it, in his own time. No ads, no tracking, no account, no paywall - and it stays that way. If it ever saved you an hour or a phone call to your provider, you can help keep it going.");
                float tx = x + 84 * S, tw = w - 84 * S;
                topH = 30 * S + Math.Max(64 * S, H(t1, Draw.fHero, tw) + 6 * S + H(t2, Draw.fBody, tw)) + 26 * S;
                if (draw)
                {
                    using (var b = new SolidBrush(warm)) g.FillRectangle(b, 0, 0, W, topH);
                    var hc = new RectangleF(x, 30 * S, 64 * S, 64 * S);
                    using (var b = new SolidBrush(Color.FromArgb(Theme.Dark ? 70 : 40, Pink))) g.FillEllipse(b, hc);
                    Heart(g, new RectangleF(hc.X + 15 * S, hc.Y + 17 * S, 34 * S, 30 * S), Pink);
                    using (var b = new SolidBrush(ink)) { var s1 = H(t1, Draw.fHero, tw); g.DrawString(t1, Draw.fHero, b, new RectangleF(tx, 28 * S, tw, s1 + 4)); using (var sb = new SolidBrush(sub)) g.DrawString(t2, Draw.fBody, sb, new RectangleF(tx, 28 * S + s1 + 6 * S, tw, H(t2, Draw.fBody, tw) + 4)); }
                    // close
                    var cx = new RectangleF(W - 40 * S, 12 * S, 28 * S, 28 * S); if (hot == "close") Theme.Fill(g, cx, 14 * S, Theme.Soft);
                    using (var p = new Pen(mut, 1.8f * S)) { g.DrawLine(p, cx.X + 9 * S, cx.Y + 9 * S, cx.Right - 9 * S, cx.Bottom - 9 * S); g.DrawLine(p, cx.Right - 9 * S, cx.Y + 9 * S, cx.X + 9 * S, cx.Bottom - 9 * S); }
                }
                hits.Add(new Hit { R = new RectangleF(W - 40 * S, 12 * S, 28 * S, 28 * S), Key = "close", Go = Close });
                y = topH + 22 * S;
            }

            // ---- the promises, as small ticks
            {
                float px = x;
                foreach (var p in new[] { "No ads", "No tracking", "No account", "Free, for everyone" })
                {
                    string t = L.T(p); var sz = g.MeasureString(t, Draw.fSmall); var r = new RectangleF(px, y, sz.Width + 34 * S, 28 * S);
                    if (px + r.Width > x + w) { px = x; y += 34 * S; r.X = px; r.Y = y; }
                    if (draw) { Theme.Fill(g, r, 14 * S, Theme.Soft); Kit.Icon(g, "check", new RectangleF(r.X + 9 * S, r.Y + 7 * S, 14 * S, 14 * S), Theme.LvText(0)); using (var b = new SolidBrush(sub)) g.DrawString(t, Draw.fSmall, b, r.X + 26 * S, r.Y + (28 * S - sz.Height) / 2); }
                    px += r.Width + 8 * S;
                }
                y += 28 * S + 24 * S;
            }

            // ---- what a donation pays for (no prices - the real bills, named)
            {
                if (draw) using (var b = new SolidBrush(mut)) g.DrawString(Kit.Sent("WHAT YOUR SUPPORT PAYS FOR"), Draw.fTiny, b, x, y);
                y += 20 * S;
                string[][] items = { new[] { "shield", "Code signing", "so Windows trusts the download instead of warning you" }, new[] { "windows", "Microsoft Store", "one-click installs and updates" }, new[] { "learn", "The website", "the guides, Learn and the phone app" } };
                float cw = (w - 2 * 10 * S) / 3, ch = 0;
                foreach (var it in items) ch = Math.Max(ch, 44 * S + H(L.T(it[1]), Draw.fBodyB, cw - 24 * S) + H(L.T(it[2]), Draw.fSmall, cw - 24 * S) + 14 * S);
                for (int i = 0; i < items.Length; i++)
                {
                    var r = new RectangleF(x + i * (cw + 10 * S), y, cw, ch);
                    if (draw)
                    {
                        Theme.Stroke(g, r, 12 * S, Theme.Dark ? Theme.Line : Theme.Line2, 1);
                        Kit.Icon(g, items[i][0], new RectangleF(r.X + 12 * S, r.Y + 14 * S, 20 * S, 20 * S), Theme.Blue);
                        float ty = r.Y + 42 * S; string a = L.T(items[i][1]), b2 = L.T(items[i][2]);
                        using (var b = new SolidBrush(ink)) g.DrawString(a, Draw.fBodyB, b, new RectangleF(r.X + 12 * S, ty, cw - 24 * S, H(a, Draw.fBodyB, cw - 24 * S) + 4));
                        using (var b = new SolidBrush(sub)) g.DrawString(b2, Draw.fSmall, b, new RectangleF(r.X + 12 * S, ty + H(a, Draw.fBodyB, cw - 24 * S) + 2 * S, cw - 24 * S, H(b2, Draw.fSmall, cw - 24 * S) + 4));
                    }
                }
                y += ch + 26 * S;
            }

            // ---- the amounts: pick one, then one big button
            {
                if (draw) using (var b = new SolidBrush(mut)) g.DrawString(Kit.Sent("CHIP IN - ONCE, ANY AMOUNT"), Draw.fTiny, b, x, y);
                y += 20 * S;
                float aw = (w - 4 * 8 * S) / 5, ah = 64 * S;
                for (int i = 0; i <= Amounts.Length; i++)
                {
                    bool other = i == Amounts.Length; string key = other ? "" : Amounts[i][0];
                    var r = new RectangleF(x + i * (aw + 8 * S), y, aw, ah); bool sel = picked == key, h = hot == "amt" + key;
                    if (draw)
                    {
                        Theme.Fill(g, r, 14 * S, sel ? Color.FromArgb(Theme.Dark ? 60 : 28, Pink) : h ? Theme.Soft : Theme.Card);
                        Theme.Stroke(g, r, 14 * S, sel ? Pink : (Theme.Dark ? Theme.Line : Theme.Line2), sel ? 2 * S : 1);
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                        {
                            using (var b = new SolidBrush(sel ? (Theme.Dark ? Color.FromArgb(0xFF, 0x9C, 0xC0) : PinkDeep) : ink)) g.DrawString(other ? L.T("Other") : "$" + key, Draw.fHead, b, new RectangleF(r.X, r.Y + 10 * S, r.Width, 26 * S), sf);
                            using (var b = new SolidBrush(sub)) g.DrawString(L.T(other ? "you choose" : Amounts[i][1]), Draw.fTiny, b, new RectangleF(r.X + 4 * S, r.Y + 38 * S, r.Width - 8 * S, 20 * S), sf);
                        }
                    }
                    string k2 = key; hits.Add(new Hit { R = r, Key = "amt" + key, Go = delegate { picked = k2; Invalidate(); } });
                }
                y += ah + 14 * S;
                string bt = picked == "" ? L.T("Donate with PayPal") + "  ›" : L.T("Donate") + " $" + picked + " " + L.T("with PayPal") + "  ›";
                var br = new RectangleF(x, y, w, 48 * S);
                if (draw)
                {
                    Theme.Fill(g, br, 24 * S, hot == "give" ? PinkDeep : Pink);
                    var bs = g.MeasureString(bt, Draw.fBtn); float hx = br.X + (br.Width - bs.Width - 26 * S) / 2;
                    Heart(g, new RectangleF(hx, br.Y + (br.Height - 16 * S) / 2, 18 * S, 16 * S), Color.White);
                    using (var b = new SolidBrush(Color.White)) g.DrawString(bt, Draw.fBtn, b, hx + 26 * S, br.Y + (br.Height - bs.Height) / 2);
                }
                hits.Add(new Hit { R = br, Key = "give", Go = delegate { Crash.OpenUrl(Url(picked)); } });
                y += 48 * S + 8 * S;
                string note = L.T("PayPal, in Canadian dollars - it opens in your browser. AdeeboMeter itself sends nothing.");
                if (draw) using (var b = new SolidBrush(mut)) using (var sf = new StringFormat { Alignment = StringAlignment.Center }) g.DrawString(note, Draw.fSmall, b, new RectangleF(x, y, w, H(note, Draw.fSmall, w) + 4), sf);
                y += H(note, Draw.fSmall, w) + 24 * S;
            }

            // ---- free ways to help
            {
                if (draw) using (var b = new SolidBrush(Theme.Dark ? Theme.Line : Theme.Line2)) g.FillRectangle(b, x, y, w, 1);
                y += 18 * S;
                if (draw) using (var b = new SolidBrush(mut)) g.DrawString(Kit.Sent("OR HELP FOR FREE"), Draw.fTiny, b, x, y);
                y += 22 * S;
                string[][] ways = { new[] { "star", "Star it on GitHub", "gh" }, new[] { "link", "Tell a friend", "share" }, new[] { "question", "Share an idea", "idea" }, new[] { "report", "Report a bug", "bug" } };
                float ww = (w - 3 * 8 * S) / 4;
                for (int i = 0; i < ways.Length; i++)
                {
                    var r = new RectangleF(x + i * (ww + 8 * S), y, ww, 40 * S); string k = "way" + ways[i][2];
                    if (draw)
                    {
                        Theme.Fill(g, r, 12 * S, hot == k ? Theme.Soft : Theme.Card); Theme.Stroke(g, r, 12 * S, Theme.Dark ? Theme.Line : Theme.Line2, 1);
                        Kit.Icon(g, ways[i][0], new RectangleF(r.X + 10 * S, r.Y + 11 * S, 18 * S, 18 * S), ways[i][2] == "gh" ? Theme.Amber : Theme.Blue);
                        string t = copied && ways[i][2] == "share" ? L.T("Link copied") : L.T(ways[i][1]);
                        using (var b = new SolidBrush(ink)) using (var sf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter }) g.DrawString(t, Draw.fSmall, b, new RectangleF(r.X + 34 * S, r.Y + (40 * S - Draw.fSmall.GetHeight(g)) / 2, r.Width - 40 * S, 20 * S), sf);
                    }
                    string id = ways[i][2];
                    hits.Add(new Hit { R = r, Key = k, Go = delegate
                    {
                        if (id == "gh") Crash.OpenUrl("https://github.com/adeeb111/AdeeboMeter");
                        else if (id == "share") { try { Clipboard.SetText("https://adeebometer.com - " + L.T("free: finds out why your internet or PC is slow")); copied = true; Invalidate(); } catch (System.Runtime.InteropServices.ExternalException) { } }
                        else { Close(); if (feedback != null) feedback(id == "idea" ? "feedback" : "bug"); }
                    } });
                }
                y += 40 * S + 22 * S;
                string thanks = L.T("Thank you for using it - that already helps more than you think.");
                if (draw) using (var b = new SolidBrush(sub)) using (var sf = new StringFormat { Alignment = StringAlignment.Center }) g.DrawString(thanks, Draw.fSmall, b, new RectangleF(x, y, w, H(thanks, Draw.fSmall, w) + 4), sf);
                y += H(thanks, Draw.fSmall, w) + 24 * S;
            }
            if (draw) Theme.Stroke(g, new RectangleF(0.5f, 0.5f, ClientSize.Width - 1, ClientSize.Height - 1), 18 * S, Theme.Dark ? Theme.Line : Theme.Line2, 1);
            return y;
        }
        bool copied;

        // a heart drawn from two arcs and a point (the icon set has none)
        public static void Heart(Graphics g, RectangleF r, Color c)
        {
            using (var p = new GraphicsPath())
            {
                float w = r.Width, h = r.Height;
                p.AddBezier(r.X + w / 2, r.Y + h * 0.28f, r.X + w / 2, r.Y, r.X, r.Y, r.X, r.Y + h * 0.32f);
                p.AddBezier(r.X, r.Y + h * 0.32f, r.X, r.Y + h * 0.62f, r.X + w * 0.32f, r.Y + h * 0.8f, r.X + w / 2, r.Bottom);
                p.AddBezier(r.X + w / 2, r.Bottom, r.X + w * 0.68f, r.Y + h * 0.8f, r.Right, r.Y + h * 0.62f, r.Right, r.Y + h * 0.32f);
                p.AddBezier(r.Right, r.Y + h * 0.32f, r.Right, r.Y, r.X + w / 2, r.Y, r.X + w / 2, r.Y + h * 0.28f);
                using (var b = new SolidBrush(c)) g.FillPath(b, p);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var h = hits.Find(z => z.R.Contains(At(e.Location))); string k = h != null ? h.Key : "";
            if (k != hot) { hot = k; Cursor = h != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { if (hot != "") { hot = ""; Invalidate(); } base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            var h = hits.Find(z => z.R.Contains(At(e.Location)));
            if (h != null) { h.Go(); return; }
            if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); }   // drag it by the card
            base.OnMouseDown(e);
        }
        protected override bool ProcessDialogKey(Keys keyData)
        {
            // hunters10 ML-5: Tab / Shift+Tab walk every button on the card (amounts, donate, the free ways to help); the highlight is the mark
            if ((keyData & Keys.KeyCode) == Keys.Tab && hits.Count > 0)
            {
                int i = hits.FindIndex(z => z.Key == hot); bool back = (keyData & Keys.Shift) != 0;
                i = i < 0 ? (back ? hits.Count - 1 : 0) : (i + (back ? hits.Count - 1 : 1)) % hits.Count; hot = hits[i].Key; Invalidate(); return true;
            }
            return base.ProcessDialogKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; return; }
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { var hh = hits.Find(z => z.Key == hot); if (hh != null) hh.Go(); else if (e.KeyCode == Keys.Enter) Crash.OpenUrl(Url(picked)); e.Handled = true; return; }   // hunters10 ML-5: Enter presses what Tab marked
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                var keys = new List<string>(); foreach (var a in Amounts) keys.Add(a[0]); keys.Add("");
                int i = keys.IndexOf(picked); picked = keys[(i + (e.KeyCode == Keys.Right ? 1 : keys.Count - 1)) % keys.Count]; Invalidate(); e.Handled = true; return;
            }
            base.OnKeyDown(e);
        }
    }
}
