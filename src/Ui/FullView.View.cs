// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== the View button and its sheet (Views.cs)
    // A sheet of coloured cards over the page - each view with what it shows, how hard it watches and what sits in the
    // corner. Picking one shows "Switching to ..." with a line from the buddy, applies everything in one layout pass and
    // fades back; anything that goes wrong puts the old view back - a switch never crashes the app.
    sealed partial class FullView
    {
        DB viewSheet; string switchingTo, switchLine; bool switchFailed; System.Windows.Forms.Timer switchTimer;
        // each view's colour and icon: the group it's for (combinations get both colours)
        static readonly Dictionary<string, string[]> ViewLook = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "everyday", new[] { "home", "", "home" } }, { "gamer", new[] { "gamer", "", "game" } }, { "streamer", new[] { "stream", "", "app" } },
            { "trader", new[] { "trade", "", "traffic" } }, { "engineer", new[] { "toolbox", "", "settings" } }, { "everything", new[] { "learn", "", "layout" } },
            { "play-watch", new[] { "gamer", "stream", "game" } }, { "play-trade", new[] { "gamer", "trade", "game" } }, { "watch-trade", new[] { "stream", "trade", "app" } }
        };
        static string[] LookOf(string id) { string[] l; return ViewLook.TryGetValue(id, out l) ? l : new[] { "mine", "", "mood" }; }
        void SyncViewBtn() { var l = LookOf(Cfg.View); viewBtn.Set(Cfg.View == "" ? L.T("My setup") : Views.NameOf(Cfg.View), l[0], l[1] == "" ? l[0] : l[1], l[2]); }

        void ViewMenu()
        {
            if (viewSheet != null && viewSheet.Visible && switchingTo == null) { HideViewSheet(); return; }
            Views.EnsureStarted();
            if (viewSheet == null)
            {
                viewSheet = new DB { Name = "viewSheet", BackColor = Theme.Bg, AutoScroll = true, Visible = false };
                viewSheet.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintViewSheet(e.Graphics); }; Wire(viewSheet);
                Controls.Add(viewSheet);
                Resize += delegate { if (viewSheet.Visible) viewSheet.Bounds = new Rectangle(0, header.Bottom, ClientSize.Width, Math.Max(Sc(200), ClientSize.Height - header.Bottom)); };
            }
            switchingTo = null; SetTipKey(null);   // Review-2: the View button's tooltip stayed over the Gamer card
            viewSheet.BackColor = Theme.Bg;
            viewSheet.Bounds = new Rectangle(0, header.Bottom, ClientSize.Width, Math.Max(Sc(200), ClientSize.Height - header.Bottom));
            viewSheet.AutoScrollPosition = Point.Empty; viewSheet.Visible = true; viewSheet.BringToFront(); viewSheet.Focus(); viewSheet.Invalidate();
            viewBtn.Selected = true;
        }
        void HideViewSheet() { if (viewSheet != null) viewSheet.Visible = false; viewBtn.Selected = false; SetTipKey(null); }

        void PaintViewSheet(Graphics g)
        {
            Begin(viewSheet);
            g.TranslateTransform(viewSheet.AutoScrollPosition.X, viewSheet.AutoScrollPosition.Y);
            float W = viewSheet.ClientSize.Width, w = Math.Min(W - 48 * S, 1080 * S), x = (W - w) / 2, y = 26 * S;
            Act(viewSheet, new RectangleF(0, 0, W, Math.Max(viewSheet.ClientSize.Height, 4000 * S)), delegate { if (switchingTo == null) HideViewSheet(); });   // outside the cards: close
            if (switchingTo != null) { PaintSwitching(g, x, w); Need(viewSheet, 10); return; }   // no scroll bar behind the switching card
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("Choose your view"), Draw.fTitle, ink, x, y);
                string cl = L.T("Close"); var cs = g.MeasureString(cl, Draw.fBodyB); var cr = new RectangleF(x + w - cs.Width, y + 4 * S, cs.Width, cs.Height);
                g.DrawString(cl, Draw.fBodyB, sub, cr.X, cr.Y); Act(viewSheet, cr, HideViewSheet);
                y += 36 * S;
                g.DrawString(L.T("One click changes the pages, how hard it watches and what sits in the corner. Each view keeps your own changes. Learn is in every view."), Draw.fBody, sub, new RectangleF(x, y, w, 40 * S)); y += 40 * S;
                var all = Views.All(); all = all.Where(v => !Views.IsBuiltIn(v[0])).Concat(all.Where(v => Views.IsBuiltIn(v[0]))).ToList(); int cols = w > 980 * S ? 3 : w > 640 * S ? 2 : 1; float gap = 14 * S, cw = (w - (cols - 1) * gap) / cols, ch = 250 * S;
                for (int i = 0; i < all.Count; i++)
                {
                    var v = all[i]; var r = new RectangleF(x + (i % cols) * (cw + gap), y + (i / cols) * (ch + gap), cw, ch);
                    PaintViewCard(g, v, r);
                    string id = v[0]; Act(viewSheet, r, delegate { StartSwitch(id); });
                }
                y += ((all.Count + cols - 1) / cols) * (ch + gap) + 6 * S;
                // make one / put back / remove / pages one by one
                float bx = x;
                Action<string, string, Action> pill = (t, acc, a) =>
                {
                    var ts = g.MeasureString(t, Draw.fBodyB); var pr = new RectangleF(bx, y, ts.Width + 32 * S, 38 * S);
                    Theme.Fill(g, pr, 19 * S, acc == null ? Theme.Card : Kit.Soft(acc)); Theme.Stroke(g, pr, 19 * S, acc == null ? Theme.Line : Kit.Accent(acc), 1);
                    using (var b = new SolidBrush(acc == null ? Theme.Ink : Kit.Ink(acc))) g.DrawString(t, Draw.fBodyB, b, pr.X + 16 * S, pr.Y + (38 * S - ts.Height) / 2);
                    Act(viewSheet, pr, a); bx = pr.Right + 10 * S;
                };
                pill(L.T("+ Make a view from what I have now"), "mine", MakeView);
                string cur = Cfg.View;
                if (Views.IsBuiltIn(cur)) pill(L.T("Put ") + Views.NameOf(cur) + L.T(" back as it starts"), null, delegate { RunSwitch(cur, true); });
                else if (cur != "") pill(L.T("Remove ") + "\"" + Views.NameOf(cur) + "\"", null, delegate { Views.Delete(cur); Cfg.Save(); AfterView(); viewSheet.Invalidate(); });
                pill(L.T("Pages one by one ›"), null, delegate { HideViewSheet(); tab = "settings"; setSec = "Navigation"; ShowTab(); });
                y += 60 * S;
            }
            Need(viewSheet, y);
        }
        static void PaintViewCard(Graphics g, string[] v, RectangleF r)
        {
            string id = v[0]; bool cur = id == Cfg.View; var look = LookOf(id); string a1 = look[0], a2 = look[1] == "" ? look[0] : look[1];
            Kit.Card(g, r, a1, cur);
            // the colour band along the top (two halves for a combination)
            using (var clip = Theme.Round(r, 16 * S))
            {
                var st = g.Save(); g.SetClip(clip);
                using (var b1 = new SolidBrush(Kit.Accent(a1))) g.FillRectangle(b1, r.X, r.Y, r.Width / 2 + 1, 6 * S);
                using (var b2 = new SolidBrush(Kit.Accent(a2))) g.FillRectangle(b2, r.X + r.Width / 2, r.Y, r.Width / 2, 6 * S);
                g.Restore(st);
            }
            if (cur) Theme.Stroke(g, r, 16 * S, Kit.Accent(a1), 2.5f * S);
            float cx = r.X + 18 * S, cy = r.Y + 22 * S;
            var ic = new RectangleF(cx, cy, 38 * S, 38 * S); Theme.Fill(g, ic, 12 * S, Kit.Soft(a1)); Kit.Icon(g, look[2], new RectangleF(ic.X + 9 * S, ic.Y + 9 * S, 20 * S, 20 * S), Kit.Accent(a1));
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(L.T(v[1]), Draw.fHead, ink, cx + 50 * S, cy + 1 * S);
            using (var sb = new SolidBrush(Theme.Sub)) using (var nf = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                g.DrawString(L.T(v[2]), Draw.fSmall, sb, new RectangleF(cx + 50 * S, cy + 24 * S, r.Right - cx - 68 * S, 34 * S), nf);   // two lines at most
            if (cur)
            {
                string t = L.T("IN USE"); var ts = g.MeasureString(t, Draw.fTiny); var tr = new RectangleF(r.Right - ts.Width - 30 * S, r.Y + 16 * S, ts.Width + 14 * S, 20 * S);
                Theme.Fill(g, tr, 10 * S, Kit.Accent(a1)); using (var tb = new SolidBrush(Theme.OnBlue)) g.DrawString(t, Draw.fTiny, tb, tr.X + 7 * S, tr.Y + (20 * S - ts.Height) / 2);
            }
            // the points: pages, pace, corner
            var stt = ViewStateOf(id); cy += 66 * S;
            var pts = ViewPoints(stt);
            // Review-2: label above value, the value gets two lines before it's cut - no more "Pages: Home, Gam..."
            float lh = Draw.fSmall.GetHeight(g);
            using (var nf = new StringFormat { FormatFlags = StringFormatFlags.LineLimit, Trimming = StringTrimming.EllipsisWord })
                for (int i = 0; i < pts.Length; i++)
                {
                    using (var db = new SolidBrush(Kit.Accent(i == 1 ? a2 : a1))) g.FillEllipse(db, cx + 2 * S, cy + 5 * S, 7 * S, 7 * S);
                    using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(pts[i][0].TrimEnd(' ', ':').ToUpperInvariant(), Draw.fTiny, mb, cx + 16 * S, cy);
                    cy += Draw.fTiny.GetHeight(g) + 1 * S;
                    var vr = new RectangleF(cx + 16 * S, cy, r.Right - cx - 34 * S, 2 * lh + 1); float vh = Math.Min(vr.Height, g.MeasureString(pts[i][1], Draw.fSmall, (int)vr.Width).Height);
                    using (var sb = new SolidBrush(Theme.Ink)) g.DrawString(pts[i][1], Draw.fSmall, sb, vr, nf);
                    cy += vh + 6 * S;
                }
        }
        static Views.State ViewStateOf(string id)
        {
            if (id == Cfg.View) return Views.Current();
            var kept = Views.KeptState(id); return kept ?? Views.Default(id) ?? Views.Current();
        }
        // "Pages: Home, Gamer Zone, Trading" · "Checks: Gamer pace" · "Corner: ping"
        static string[][] ViewPoints(Views.State s)
        {
            // Review-2: the pages in the bar, as the sidebar shows them - not a guess from what's hidden
            var pages = (s.Nav ?? "").Split(',').Where(p => p != "").Select(p => L.T(NavName(p))).ToList();
            if (pages.Count == 0)   // a view that leaves the bar as it is: Home, its activity pages, Learn
            {
                pages.Add(L.T("Home"));
                foreach (var p in new[] { new[] { "gamer", "Gamer Zone" }, new[] { "stream", "Streaming" }, new[] { "trade", "Markets" } }) if (!("," + s.Hidden + ",").Contains("," + p[0] + ",")) pages.Add(L.T(p[1]));
                pages.Add(L.T("Learn"));
            }
            if (s.Mode == "pro") pages.Add(L.T("every tool"));
            string pace = s.Preset == "penny" ? L.T("Penny - the lightest touch") : s.Preset == "gamer" ? L.T("Gamer - game servers every ") + s.GameBg + " s" : s.Preset == "custom" ? L.T("your own mix") : L.T("Balanced - trouble seen in seconds");
            string corner = s.Widget == "gamer" ? L.T("ping to your game") : s.Widget == "engineer" ? L.T("the engineer's card") : s.Widget == "speed" ? L.T("speeds") : s.Widget == "speedstatus" ? L.T("speed + status") : s.Widget == "speedapps" ? L.T("speeds and busy apps") : s.Widget == "calls" ? L.T("call quality") : s.Widget == "everyday" ? L.T("the everyday card") : L.T("game ping while a game you added is open, call quality in a call, else speed + status");
            return new[] { new[] { L.T("Featured pages: "), string.Join(", ", pages) }, new[] { L.T("Background checks: "), pace + (s.Details ? " · Details on" : "") }, new[] { L.T("Corner widget: "), corner } };
        }

        // ---------- the switch: a short "switching" screen with a line from the buddy, then everything in one pass
        void StartSwitch(string id) { if (id == Cfg.View) { HideViewSheet(); return; } RunSwitch(id, false); }
        void RunSwitch(string id, bool reset)
        {
            if (switchingTo != null) return;   // one at a time
            switchingTo = id; switchFailed = false; SetTipKey(null);
            string name = Views.NameOf(id);
            try { switchLine = Lines.Steady("view.switch", name) ?? ""; } catch (Exception e) { if (e is OutOfMemoryException) throw; switchLine = ""; }
            viewSheet.AutoScrollPosition = Point.Empty; viewSheet.Invalidate(); viewSheet.Update();   // the "switching" screen shows before the work starts
            BeginInvoke((Action)delegate
            {
                var before = Views.Current(); string beforeId = Cfg.View, err = null;
                try
                {
                    SuspendLayout();
                    try { if (reset) Views.Reset(id); else Views.Switch(id); Cfg.Save(); AfterView(); }
                    finally { ResumeLayout(true); }
                }
                catch (Exception e)
                {
                    if (e is OutOfMemoryException) throw;
                    // anything odd: the old view comes back, and the app carries on
                    Program.Log("view switch to " + id + ": " + e.GetType().Name + " " + e.Message); err = e.Message;
                    try { Cfg.View = beforeId; Views.Put(before); Policy.Apply(); Cfg.Save(); AfterView(); } catch (Exception e2) { if (e2 is OutOfMemoryException) throw; Program.Log("view restore: " + e2.Message); }
                }
                // motion review (23 Nov): a switch that worked closes the card at once - no minimum wait, the card only stood
                // in for the time the work took. A switch that failed keeps its line up long enough to read, without repainting.
                if (err == null) { switchingTo = null; HideViewSheet(); return; }
                switchFailed = true; switchLine = L.T("That didn't switch cleanly - your previous view is back, nothing lost."); viewSheet.Invalidate();
                if (switchTimer == null) { switchTimer = new System.Windows.Forms.Timer { Interval = 2500 }; switchTimer.Tick += delegate { SwitchTick(); }; }
                switchTimer.Stop(); switchTimer.Start();
            });
        }
        void SwitchTick()
        {
            switchTimer.Stop(); switchingTo = null; HideViewSheet();
        }
        void PaintSwitching(Graphics g, float x, float w)
        {
            var look = LookOf(switchingTo); float H = viewSheet.ClientSize.Height;
            var card = new RectangleF(x + (w - Math.Min(w, 560 * S)) / 2, Math.Max(30 * S, H / 2 - 110 * S), Math.Min(w, 560 * S), 200 * S);
            Kit.Card(g, card, look[0], true);
            var ic = new RectangleF(card.X + 26 * S, card.Y + 30 * S, 48 * S, 48 * S); Theme.Fill(g, ic, 14 * S, Kit.Soft(look[0])); Kit.Icon(g, look[2], new RectangleF(ic.X + 12 * S, ic.Y + 12 * S, 24 * S, 24 * S), Kit.Accent(look[0]));
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(L.T("Switching to ") + Views.NameOf(switchingTo) + "...", Draw.fTitle, ink, ic.Right + 18 * S, ic.Y + 8 * S);
            if (!string.IsNullOrEmpty(switchLine)) using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(switchLine, Draw.fBody, sb, new RectangleF(card.X + 26 * S, card.Y + 100 * S, card.Width - 52 * S, 46 * S));
            // the bar says "working" while the switch runs; once it failed, the line is the message and the bar goes
            if (switchFailed) return;
            float t = 0; var track = new RectangleF(card.X + 26 * S, card.Bottom - 30 * S, card.Width - 52 * S, 5 * S);
            Theme.Fill(g, track, 2.5f * S, Theme.Track);
            Theme.Fill(g, new RectangleF(track.X + (track.Width * 0.7f) * t, track.Y, track.Width * 0.3f, track.Height), 2.5f * S, Kit.Accent(look[1] == "" ? look[0] : look[1]));
        }

        void MakeView()
        {
            string n = Ask(L.T("A name for this view (what's on now: pages, pace, widget)"), ""); if (string.IsNullOrWhiteSpace(n)) return;
            if (Views.Make(n) == null) return;
            Cfg.Save(); AfterView(); if (viewSheet != null) viewSheet.Invalidate();
        }
        // after a switch: tabs, pages, the Details switch, the pace and the widget all follow
        void AfterView()
        {
            SyncViewBtn();
            ApplyLayout(); ApplyMode();   // the bar is rebuilt first, then Simple's filter
            if (detailsBtn.On != Cfg.Details) { detailsBtn.On = Cfg.Details; if (pathPanel != null) pathPanel.Height = PathHeight; }
            if (M != null) M.Nudge();
            string sec = SecOf(tab); if (sec != "" && Cfg.Off(sec)) { tab = "overview"; ShowTab(); }
            Invalidate(true);
        }
        // from Settings › View: the same switching screen as the button
        void SwitchView(string id)
        {
            if (id == Cfg.View || Views.Default(id) == null && Views.KeptState(id) == null) return;
            if (viewSheet == null || !viewSheet.Visible) ViewMenu();
            RunSwitch(id, false);
        }
    }

    // the View button: the view's colours (both for a combination), its icon, "VIEW" over its name, and a chevron
    sealed class ViewButton : Control
    {
        string name = "", a1 = "home", a2 = "home", icon = "home"; bool hot, sel;
        public bool Selected { get { return sel; } set { if (sel != value) { sel = value; Invalidate(); } } }
        public ViewButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand; TabStop = true; AccessibleRole = AccessibleRole.ButtonDropDown;
        }
        public void Set(string n, string acc1, string acc2, string ic)
        {
            name = n ?? ""; a1 = acc1; a2 = acc2; icon = ic; AccessibleName = L.T("View: ") + name;
            float S = Draw.S; var f = Draw.fBodyB ?? Font;
            Width = (int)(Math.Max(Draw.Measure(name, f, 0).Width, Draw.Measure(L.T("VIEW"), Draw.fTiny ?? Font, 0).Width) + 74 * S); Height = (int)(38 * S);
            Invalidate();
        }
        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space || e.KeyCode == Keys.Down) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.TextRenderingHint = Draw.WindowText; g.TextContrast = Draw.Contrast;
            float S = Draw.S; var r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
            using (var b = new SolidBrush(Parent != null ? Parent.BackColor : BackColor)) g.FillRectangle(b, ClientRectangle);
            // a soft wash from the first colour to the second, a coloured edge
            using (var path = Theme.Round(r, r.Height / 2))
            using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(r, Kit.Mix(Theme.Card, Kit.Accent(a1), hot || sel ? 0.20f : 0.12f), Kit.Mix(Theme.Card, Kit.Accent(a2), hot || sel ? 0.20f : 0.12f), 0f))
                g.FillPath(lg, path);
            Theme.Stroke(g, r, r.Height / 2, Kit.Mix(Kit.Accent(a1), Theme.Card, sel ? 0f : 0.45f), sel ? 2 * S : 1);
            // the icon in a filled circle
            var ic = new RectangleF(6 * S, (Height - 26 * S) / 2, 26 * S, 26 * S);
            using (var cb = new SolidBrush(Kit.Accent(a1))) g.FillEllipse(cb, ic);
            Kit.Icon(g, icon, new RectangleF(ic.X + 6 * S, ic.Y + 6 * S, 14 * S, 14 * S), Theme.OnBlue);
            float tx = ic.Right + 8 * S;
            using (var mb = new SolidBrush(Kit.Ink(a1))) g.DrawString(L.T("VIEW"), Draw.fTiny, mb, tx, 3 * S);
            using (var nb = new SolidBrush(Theme.Ink)) g.DrawString(name, Draw.fBodyB, nb, tx - 1 * S, 14 * S);
            // the chevron, drawn (no font glyph needed)
            float cx = Width - 18 * S, cy = Height / 2f + (sel ? -1 : 1) * 1.5f * S, d = 4 * S;
            using (var p = new Pen(Kit.Ink(a1), 1.8f * S) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
                g.DrawLines(p, sel ? new[] { new PointF(cx - d, cy + d / 2), new PointF(cx, cy - d / 2), new PointF(cx + d, cy + d / 2) } : new[] { new PointF(cx - d, cy - d / 2), new PointF(cx, cy + d / 2), new PointF(cx + d, cy - d / 2) });
            if (Focused && ShowFocusCues) Theme.Stroke(g, new RectangleF(1.5f, 1.5f, Width - 3, Height - 3), (Height - 3) / 2f, Theme.BlueDark, 2);
        }
    }
}
