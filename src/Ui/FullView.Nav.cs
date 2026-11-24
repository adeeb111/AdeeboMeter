// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== navigation (PLAN 11.11, 11.11b)
    // Five sections on the bar (Home, Toolbox, Gamer, Time machine, Learn), everything else under More (Network story,
    // Security, Check a destination, Calculators, Reports, your own tabs). The order and what sits on the bar are the
    // user's (Settings > Navigation); nothing is hidden or dropped. Two looks for the same pages: Classic (coloured tabs
    // along the top) and Sidebar (a navy column on the left, topogram style). Only the frame changes - every page is shared.
    sealed partial class FullView
    {
        // id -> name, icon, the colour (Kit.Accents) and where it opens
        static readonly Dictionary<string, string[]> NavInfo = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "home", new[] { "Home", "home", "home" } }, { "outlook", new[] { "Outlook", "outlook", "outlook" } }, { "toolbox", new[] { "Toolbox", "tool", "toolbox" } },
            { "gamer", new[] { "Gamer", "game", "gamer" } }, { "stream", new[] { "Streaming", "app", "stream" } }, { "sports", new[] { "Sports", "star", "sports" } }, { "trade", new[] { "Markets", "traffic", "trade" } }, { "time", new[] { "Time machine", "time", "time" } },
            { "learn", new[] { "Learn", "learn", "learn" } }, { "story", new[] { "Network story", "story", "story" } },
            { "security", new[] { "Security", "shield", "security" } }, { "dest", new[] { "Check a destination", "dest", "dest" } },
            { "calc", new[] { "Calculators", "calc", "calc" } }, { "lab", new[] { "AdeeboLab", "route", "learn" } }, { "shell", new[] { "AdeeboShell", "go", "home" } }, { "reports", new[] { "Reports", "report", "reports" } }
        };
        static string NavName(string id) { string[] i; if (NavInfo.TryGetValue(id, out i)) return i[0]; var t = MyTab(id); return t != null ? t.Name : id; }
        static string NavIcon(string id) { string[] i; return NavInfo.TryGetValue(id, out i) ? i[1] : "star"; }
        static string NavAccent(string id) { string[] i; return NavInfo.TryGetValue(id, out i) ? i[2] : "mine"; }
        static List<string> NavBar() { return Cfg.NavClean(Cfg.Nav).Split(',').Where(z => !Cfg.Off(z) && (z.StartsWith("mine", StringComparison.Ordinal) ? MyTab(z) != null : true)).ToList(); }
        static List<string> NavMore()
        {
            var bar = NavBar(); var l = Cfg.NavIds.Where(z => !bar.Contains(z) && !Cfg.Off(z)).ToList();
            foreach (var t in MyTabs()) if (!bar.Contains(t.Id)) l.Add(t.Id);
            return l;
        }
        // the section a page belongs to, in nav terms (Calculators and Reports are Toolbox questions; your tabs are their own)
        string NavOf(string page)
        {
            if (page == "toolbox" && tbQ == "calc") return "calc";
            if (page == "toolbox" && tbQ == "report") return "reports";
            if (page == "mine") return curMine;
            string s = SecOf(page); return s == "" ? "" : s;
        }
        string curMine = "", navOwn = "";   // navOwn: Calculators or Reports opened from the bar - shown as their own page
        // Review-1: a search left in the box kept covering Calculators and Reports - each destination opens clean
        void ClearTbSearch() { if (tbSearch != null && tbSearch.Box.Text != "") tbSearch.Box.Text = ""; tbFilter = ""; }
        internal void OpenNav(string id)
        {
            if (tbBusy && tab == "toolbox" && (id == "calc" || id == "reports" || id == "toolbox")) { BusyNote(); return; }
            switch (id)
            {
                // 22 Nov (Adeeb: "why did Calculators open the Toolbox?"): their own page - the tiles of that one section, under its
                // own title, without the Toolbox's chips; the Toolbox's own view setting is left as it was
                case "lab": tab = "lab"; ShowTab(); break;   // AdeeboLab's own page in the app; a drawing opens in its window from there (Adeeb, 22 Nov)
                case "calc": case "reports": tab = "toolbox"; ShowTab(); EnsureToolbox(); navOwn = id == "calc" ? "calc" : "report"; if (!EveryTool) { Cfg.TbView = "list"; ApplyTbView(); } ClearTbSearch(); ShowQuestions(navOwn); break;
                default:
                    if (id.StartsWith("mine", StringComparison.Ordinal)) { curMine = id; tab = "mine"; ShowTab(); if (mineCanvas != null) mineCanvas.Invalidate(); break; }
                    var def = Secs.FirstOrDefault(x => x[0] == id); if (def == null) return;
                    string pg; tab = lastIn.TryGetValue(id, out pg) ? pg : def[2];
                    if (id == "toolbox") { navOwn = ""; if (tbQ == "calc" || tbQ == "report") ShowQuestions(EveryTool ? "*" : ""); }
                    ShowTab(); break;
            }
            SyncNav();
        }

        // ---------- Classic: the tab row
        PillButton moreBtn;
        void BuildNavBar()
        {
            tabs.SuspendLayout();
            foreach (Control c in tabs.Controls.Cast<Control>().ToList()) { tabs.Controls.Remove(c); c.Dispose(); }   // search lives in the top line now
            foreach (var id in NavBar())
            {
                var b = new PillButton(NavName(id), PillButton.K.Tab) { Tag = id, Accent = NavAccent(id) };
                string nid = id; b.Click += delegate { OpenNav(nid); };
                tabs.Controls.Add(b);
            }
            moreBtn = new PillButton(L.T("More") + "  ▾", PillButton.K.Tab) { Tag = "more" };
            moreBtn.Click += delegate { ShowMoreMenu(moreBtn, new Point(0, moreBtn.Height)); };
            tabs.Controls.Add(moreBtn);
            tabs.ResumeLayout(true); tabs.Invalidate(true); SyncNav();
        }
        void SyncNav()
        {
            string cur = NavOf(tab); bool onBar = false;
            foreach (Control c in tabs.Controls) { var p = c as PillButton; if (p == null || (string)p.Tag == "more") continue; p.Selected = (string)p.Tag == cur; onBar |= p.Selected; }
            if (moreBtn != null) { moreBtn.Selected = !onBar && cur != "" && NavMore().Contains(cur); moreBtn.Accent = moreBtn.Selected ? NavAccent(cur) : null; moreBtn.Text = L.T(moreBtn.Selected ? NavName(cur) : "More") + "  ▾"; }
            if (sideNav != null && sideNav.Visible) sideNav.Invalidate();
        }
        // the More menu: everything not on the bar, with its icon; and a way to the Navigation settings
        void ShowMoreMenu(Control at, Point p)
        {
            var m = Menus.Make();
            foreach (var id in NavMore())
            {
                string nid = id; var it = new ToolStripMenuItem(L.T(NavName(id)), NavBitmap(NavIcon(id), Kit.Accent(NavAccent(id))));
                it.Click += delegate { OpenNav(nid); }; it.Padding = new Padding(0, Sc(3), 0, Sc(3)); m.Items.Add(it);
            }
            m.Items.Add(new ToolStripSeparator());
            var set = new ToolStripMenuItem(L.T("Arrange tabs..."), NavBitmap("settings", Theme.Muted)); set.Click += delegate { OpenSettingsAt("Navigation"); }; m.Items.Add(set);
            m.Closed += delegate { BeginInvoke((Action)m.Dispose); };
            m.Show(at, p);
        }
        readonly Dictionary<string, Bitmap> navBmps = new Dictionary<string, Bitmap>();
        Bitmap NavBitmap(string icon, Color c)
        {
            string k = icon + c.ToArgb().ToString(CultureInfo.InvariantCulture); Bitmap b;
            if (navBmps.TryGetValue(k, out b)) return b;
            b = new Bitmap(Sc(18), Sc(18)); using (var g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); Kit.Icon(g, icon, new RectangleF(0, 0, Sc(18), Sc(18)), c); }
            navBmps[k] = b; return b;
        }

        // ---------- Sidebar: the navy column (topogram's look: faint highlight + a 3 px aqua bar, never a solid block)
        DB sideNav;
        // the rail: graphite for the standard themes (23 Nov polish - the website's #15181C, not navy); the colour themes get a
        // deep shade of their own so it doesn't clash (Sunset was navy-on-plum)
        static bool StdRail { get { return Theme.Palette == "standard" || Theme.Palette == ""; } }
        static Color Navy { get { return StdRail ? Theme.C(Theme.Dark ? "#0C0E10" : "#15181C") : Theme.Dark ? Kit.Mix(Kit.Mix(Theme.Bg, Color.Black, 0.35f), Theme.Blue, 0.05f) : Kit.Mix(Theme.Blue, Color.Black, 0.78f); } }
        static Color Navy2 { get { return Kit.Mix(Navy, Color.White, 0.08f); } }
        static Color Aqua { get { return StdRail ? Theme.C("#7FA6F8") : Theme.Blue; } }
        static Color NavyText { get { return StdRail ? Theme.C("#C9CED6") : Kit.Mix(Navy, Color.White, 0.72f); } }
        DB BuildSideNav()
        {
            sideNav = new DB { Name = "sideNav", Dock = DockStyle.Left, Width = Sc(214), BackColor = Navy, Visible = false };
            sideNav.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintSideNav(e.Graphics, sideNav.ClientRectangle); };
            Wire(sideNav);
            return sideNav;
        }
        void PaintSideNav(Graphics g, Rectangle r)
        {
            Begin(sideNav); bool hc = Theme.HighContrast; sideNav.Acc.Clear();
            Color bg = hc ? SystemColors.Control : Navy, ink = hc ? SystemColors.ControlText : Color.White, sub = hc ? SystemColors.GrayText : NavyText;
            if (!hc && sideNav.BackColor != bg) sideNav.BackColor = bg;
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, r);
            float x = 10 * S, y = 14 * S, w = r.Width - 20 * S; string cur = NavOf(tab);
            // 23 Nov: no search box here - the top bar's search (Ctrl K) is the one way to search everything
            // too many tabs for the height (short screen, 150-200 % scaling): rows get compact, and what still doesn't fit
            // goes behind a "More..." row that opens the same menu as the top bar's More - nothing is ever cut off unreachable
            var bar = NavBar().ToList(); var more = NavMore().ToList(); float room = r.Height - y - 6 * S - 18 * S - 4 * S - 44 * S - 40 * S;   // 44: Settings always has its row at the bottom; 40: Your phone above it
            // 22 Nov (Adeeb: "make it tighter when all is enabled"): tighter rows to start with, and they shrink until everything fits
            // (40/34 px down to 30/28); only past that does the rest go behind "More..."
            float bigH = 40, smallH = 34;
            while ((bar.Count * (bigH + 1) + more.Count * (smallH + 1)) * S > room && bigH > 30) { bigH -= 1; smallH = Math.Max(28, smallH - 1); }
            bool compact = (bar.Count * (bigH + 1) + more.Count * (smallH + 1)) * S > room;
            int fitMore = more.Count; if (compact) { float left = room - bar.Count * (bigH + 1) * S; fitMore = Math.Max(0, (int)(left / ((smallH + 1) * S))); if (fitMore < more.Count) fitMore = Math.Max(0, fitMore - 1); }
            Action<string, bool> row = (id, small) =>
            {
                float h = (small ? smallH : bigH) * S; var rr = new RectangleF(x, y, w, h); bool sel = cur == id;
                if (sel) { if (hc) Theme.Stroke(g, rr, 9 * S, SystemColors.Highlight, 2 * S); else Theme.Fill(g, rr, 9 * S, Theme.Blue); }   // the current page: a solid logo-blue row (review 24 Nov: the faint wash was 1.3:1)
                Kit.Icon(g, NavIcon(id), new RectangleF(x + 14 * S, y + (h - 18 * S) / 2, 18 * S, 18 * S), sel ? (hc ? SystemColors.Highlight : Theme.OnBlue) : sub);
                using (var b = new SolidBrush(sel ? (hc ? ink : Theme.OnBlue) : sub)) g.DrawString(L.T(NavName(id)), small ? Draw.fBody : Draw.fBtn, b, x + 42 * S, y + (h - (small ? Draw.fBody : Draw.fBtn).GetHeight(g)) / 2);
                string nid = id; Act(sideNav, rr, delegate { OpenNav(nid); }); Hint(sideNav, rr, "nav." + id); sideNav.Acc.Add(Tuple.Create(L.T(NavName(id)), Rectangle.Round(rr), (Action)delegate { OpenNav(nid); }));
                y += h + 1 * S;
            };
            foreach (var id in bar) row(id, false);
            y += 6 * S;
            using (var b = new SolidBrush(Kit.Mix(sub, bg, 0.3f))) g.DrawString(L.T("More"), Draw.fTiny, b, x + 14 * S, y); y += 18 * S;
            foreach (var id in more.Take(fitMore)) row(id, true);
            if (fitMore < more.Count)
            {
                var rr = new RectangleF(x, y, w, 36 * S); Kit.Icon(g, "more", new RectangleF(x + 14 * S, y + 9 * S, 18 * S, 18 * S), sub);
                using (var b = new SolidBrush(sub)) g.DrawString(L.T("More...") + "  (" + (more.Count - fitMore).ToString(CultureInfo.CurrentCulture) + ")", Draw.fBody, b, x + 42 * S, y + (36 * S - Draw.fBody.GetHeight(g)) / 2);
                Act(sideNav, rr, delegate { ShowMoreMenu(sideNav, new Point((int)rr.X, (int)rr.Bottom)); }); y += 38 * S;
            }
            // Settings at the bottom, always (AM-CG-102: it went missing when the list filled the sidebar); the live meter sits
            // under it when there's room - the gear at the top right is the other way in
            float mh = 96 * S; bool meter = r.Height - 12 * S - mh - 46 * S - 40 * S > y + 8 * S;
            float my = r.Height - mh - 12 * S;
            {
                var srr = new RectangleF(x, Math.Max(y + 40 * S, (meter ? my : r.Height - 4 * S) - 44 * S), w - 44 * S, 38 * S); bool ssel = tab == "settings";
                // the heart beside Settings (Adeeb, 22 Nov): community and support - small, always there, never in the way
                {
                    var hr = new RectangleF(x + w - 42 * S, srr.Y + 2 * S, 34 * S, 34 * S);
                    heartRect = hr; HeartGlow(g, hr);
                    SupportCard.Heart(g, new RectangleF(hr.X + 8 * S, hr.Y + 9 * S, 18 * S, 16 * S), hc ? SystemColors.WindowText : Color.FromArgb(0xE6, 0x49, 0x80));
                    Act(sideNav, hr, OpenSupport); sideNav.Acc.Add(Tuple.Create(L.T("Community and support"), Rectangle.Round(hr), (Action)OpenSupport));
                }
                // Your phone, the second way in (Adeeb, 16 Nov: "connect option should also be in the left bar") - its state on the right
                {
                    var prr = new RectangleF(x, srr.Y - 40 * S, w, 38 * S); bool psel = tab == "phone"; string plb; string pst = PhoneHub.StateOf(out plb);
                    if (psel) Theme.Fill(g, prr, 9 * S, hc ? SystemColors.Highlight : Theme.Blue);
                    Color pc = pst == "on" ? Theme.Green : pst == "ask" ? Theme.Amber : pst == "wait" ? Aqua : (psel ? (hc ? SystemColors.Highlight : Theme.OnBlue) : sub);
                    Kit.Icon(g, "link", new RectangleF(x + 14 * S, prr.Y + 10 * S, 18 * S, 18 * S), pc);
                    using (var b = new SolidBrush(psel && !hc ? Theme.OnBlue : psel ? ink : sub)) g.DrawString(L.T("Your phone"), Draw.fBody, b, x + 42 * S, prr.Y + (38 * S - Draw.fBody.GetHeight(g)) / 2);
                    if (plb != "") using (var b = new SolidBrush(pc)) using (var nf = new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                        g.DrawString(plb, Draw.fSmall, b, new RectangleF(prr.X + 120 * S, prr.Y + (38 * S - Draw.fSmall.GetHeight(g)) / 2, prr.Width - 128 * S, 20 * S), nf);
                    Act(sideNav, prr, LinkClicked); sideNav.Acc.Add(Tuple.Create(L.T("Your phone"), Rectangle.Round(prr), (Action)LinkClicked));
                }
                if (ssel) Theme.Fill(g, srr, 9 * S, hc ? SystemColors.Highlight : Theme.Blue);
                Kit.Icon(g, "settings", new RectangleF(x + 14 * S, srr.Y + 10 * S, 18 * S, 18 * S), ssel ? (hc ? SystemColors.Highlight : Theme.OnBlue) : sub);
                using (var b = new SolidBrush(ssel && !hc ? Theme.OnBlue : ssel ? ink : sub)) g.DrawString(L.T("Settings"), Draw.fBody, b, x + 42 * S, srr.Y + (38 * S - Draw.fBody.GetHeight(g)) / 2);
                Act(sideNav, srr, delegate { tab = "settings"; ShowTab(); SyncNav(); }); Hint(sideNav, srr, "nav.settings"); sideNav.Acc.Add(Tuple.Create(L.T("Settings"), Rectangle.Round(srr), (Action)delegate { tab = "settings"; ShowTab(); SyncNav(); }));
            }
            if (meter)
            {
                var mr = new RectangleF(x, my, w, mh);
                Theme.Fill(g, mr, 10 * S, hc ? SystemColors.Window : Navy2);
                int lv = snap.Level; Draw.LvDot(g, lv, mr.X + 12 * S, mr.Y + 13 * S, 9 * S, 9 * S);
                using (var b = new SolidBrush(hc ? SystemColors.WindowText : Color.White)) g.DrawString(L.T(lv == 2 ? "Problem" : lv == 1 ? "Busy · holding" : "All quiet"), Draw.fBtn, b, mr.X + 28 * S, mr.Y + 8 * S);
                using (var b = new SolidBrush(hc ? SystemColors.GrayText : NavyText))
                {
                    float ly = mr.Y + 34 * S;
                    foreach (var kv in new[] { new[] { "CPU", Draw.Fmt(snap.Cpu, "0", "%") }, new[] { "RAM", Draw.Fmt(snap.Mem, "0", "%") }, new[] { L.T("Me"), (selfMem / 1048576.0).ToString("0", CultureInfo.CurrentCulture) + " MB" } })
                    {
                        g.DrawString(kv[0], Draw.fBody, b, mr.X + 12 * S, ly); var vs = g.MeasureString(kv[1], Draw.fBody);
                        g.DrawString(kv[1], Draw.fBody, b, mr.Right - 12 * S - vs.Width, ly); ly += 19 * S;
                    }
                }
                Act(sideNav, mr, delegate { tab = "procs"; ShowTab(); SyncNav(); });
            }
        }

        // the heart's card: community and support (SupportCard.cs); its "share an idea" / "report a bug" open the app's own boxes
        void OpenSupport() { heartSeen = true; SupportCard.Open(this, k => ReportDialog(k, "")); }

        // the heart glows once (Adeeb, 22 Nov): after about 35 minutes with the window open - three soft pulses, then never again
        // this run; missing it is fine. Nothing glows if the heart was already opened.
        RectangleF heartRect; double heartOpenSec; bool heartSeen; DateTime heartGlowAt = DateTime.MinValue; System.Windows.Forms.Timer heartTimer;
        const double HeartAfterSec = 35 * 60, HeartGlowSec = 3.6;
        void HeartTick()
        {
            if (heartSeen || heartGlowAt != DateTime.MinValue || sideNav == null || !sideNav.Visible || WindowState == FormWindowState.Minimized || !Visible) return;
            heartOpenSec += 0.5; if (heartOpenSec < HeartAfterSec) return;
            if (!Motion.On) { heartSeen = true; return; }   // no pulse when Windows animations are off or in Penny
            heartGlowAt = DateTime.Now;
            heartTimer = new System.Windows.Forms.Timer { Interval = 33 };
            heartTimer.Tick += delegate
            {
                bool done = (DateTime.Now - heartGlowAt).TotalSeconds > HeartGlowSec;
                if (sideNav != null && heartRect.Width > 0) sideNav.Invalidate(Rectangle.Round(RectangleF.Inflate(heartRect, 14 * S, 14 * S)));
                if (done) { heartTimer.Stop(); heartTimer.Dispose(); heartTimer = null; heartSeen = true; }
            };
            heartTimer.Start();
        }
        void HeartGlow(Graphics g, RectangleF hr)
        {
            if (heartGlowAt == DateTime.MinValue || heartSeen || Theme.HighContrast) return;
            double t = (DateTime.Now - heartGlowAt).TotalSeconds / HeartGlowSec; if (t < 0 || t > 1) return;
            float ph = (float)((t * 3) % 1.0);   // three pulses
            float k = 1 + ph * 0.55f; int a = (int)(150 * (1 - ph) * (t > 0.9 ? (1 - t) * 10 : 1));
            var c = new RectangleF(hr.X + hr.Width / 2 - hr.Width * k / 2, hr.Y + hr.Height / 2 - hr.Height * k / 2, hr.Width * k, hr.Height * k);
            using (var b = new SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, a / 3)), 0xE6, 0x49, 0x80))) g.FillEllipse(b, c);
            using (var p = new Pen(Color.FromArgb(Math.Max(0, Math.Min(255, a)), 0xE6, 0x49, 0x80), 2 * S)) g.DrawEllipse(p, c);
        }

        // ---------- applying a layout: which frame shows, how wide the right-hand panel is
        TableLayoutPanel bodyGrid;
        void ApplyLayout()
        {
            bool sb = Cfg.Layout == "sidebar";
            SuspendLayout();
            if (sideNav != null) sideNav.Visible = sb;
            if (tabs != null) tabs.Visible = !sb;
            if (side != null) side.Visible = !sb;
            SizeSide();
            ResumeLayout(true);
            if (tabs != null && !sb) BuildNavBar(); else SyncNav();
            PlaceTop();
        }
        void SizeSide()
        {
            if (bodyGrid == null) return;
            float sw = Cfg.Layout == "sidebar" ? 0 : !Cfg.SideOpen ? Sc(78) : Math.Max(Sc(300), Math.Min(Sc(390), bodyGrid.Width * 0.30f));
            if (Math.Abs(bodyGrid.ColumnStyles[1].Width - sw) > 1) bodyGrid.ColumnStyles[1].Width = sw;
            if (leftPane != null) leftPane.Margin = new Padding(0, 0, sw > 0 ? Sc(14) : 0, 0);
        }
        public void SetLayout(string l)
        {
            Cfg.Layout = l == "sidebar" ? "sidebar" : "classic"; Cfg.SidebarTried = true; Cfg.Save(); ApplyLayout();
            foreach (Control c in Descendants(this)) c.Invalidate();
        }

        // ---------- the slim strip (Classic, side panel closed): CPU, RAM, disk and the app's own size as icons + numbers
        void PaintSlim(Graphics g, Rectangle r)
        {
            Begin(side);
            var card = new RectangleF(4 * S, 0, r.Width - 8 * S, Math.Min(r.Height - 4 * S, 330 * S));
            Kit.Card(g, card, null, false);
            float y = card.Y + 14 * S, cx = card.X + card.Width / 2;
            Kit.Icon(g, "back", new RectangleF(cx - 8 * S, y, 16 * S, 16 * S), Theme.Muted);   // opens the panel again
            Act(side, new RectangleF(card.X, card.Y, card.Width, 34 * S), delegate { Cfg.SideOpen = true; Cfg.Save(); SizeSide(); side.Invalidate(); });
            Hint(side, new RectangleF(card.X, card.Y, card.Width, 34 * S), "side.open");
            y += 34 * S;
            Func<double, int> lvOf = v => double.IsNaN(v) ? 3 : v >= 90 ? 2 : v >= 75 ? 1 : 0;
            foreach (var it in new[] { new object[] { "cpu", "CPU", snap.Cpu }, new object[] { "devices", "RAM", snap.Mem }, new object[] { "report", L.T("Disk"), snap.Disk } })
            {
                double v = (double)it[2]; int lv = lvOf(v);
                Kit.Icon(g, (string)it[0], new RectangleF(cx - 10 * S, y, 20 * S, 20 * S), lv == 0 || lv == 3 ? Theme.Muted : Theme.Lv(lv));
                string t = Draw.Fmt(v, "0", "%"); var ts = g.MeasureString(t, Draw.fBtn);
                using (var b = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(t, Draw.fBtn, b, cx - ts.Width / 2, y + 22 * S);
                using (var m = new SolidBrush(Theme.Muted)) { var ls = g.MeasureString((string)it[1], Draw.fTiny); g.DrawString((string)it[1], Draw.fTiny, m, cx - ls.Width / 2, y + 40 * S); }
                y += 64 * S;
            }
            string me = (selfMem / 1048576.0).ToString("0", CultureInfo.CurrentCulture) + " MB"; var mes = g.MeasureString(me, Draw.fSmall);
            using (var m = new SolidBrush(Theme.Muted)) g.DrawString(me, Draw.fSmall, m, cx - mes.Width / 2, y);
            Act(side, new RectangleF(card.X, card.Y + 34 * S, card.Width, card.Height - 34 * S), delegate { Cfg.SideOpen = true; Cfg.Save(); SizeSide(); side.Invalidate(); });
        }

        // ---------- the header: the layout switch (Classic tabs <-> Sidebar). The big search sits at the end of the tab row
        // (Classic) or the top of the sidebar, where there's room - the header keeps its width for the headline.
        // ---------- Details off: the plain path - three chips instead of the five-dot strip (11.4)
        void PaintPlainPath(Graphics g, Rectangle r)
        {
            Begin(pathPanel);
            float x = Sc(20), y = (r.Height - 30 * S) / 2;
            int lvPc = snap.Path.Length > 0 ? snap.Path[0] : 3, lvLan = snap.Path.Length > 1 ? snap.Path[1] : 3, lvNet = 0;
            for (int i = 2; i < 5 && i < snap.Path.Length; i++) if (snap.Path[i] >= 0 && snap.Path[i] <= 2 && snap.Path[i] > lvNet) lvNet = snap.Path[i];
            bool netKnown = false; for (int i = 2; i < 5 && i < snap.Path.Length; i++) netKnown |= snap.Path[i] >= 0 && snap.Path[i] <= 2;
            if (!netKnown) lvNet = 3;
            string lan = snap.PathLabel.Length > 1 ? snap.PathLabel[1] : "Wi-Fi";
            var items = new[] {
                new object[] { "cpu", "PC", lvPc, lvPc == 0 ? "fine" : lvPc == 1 ? "busy" : lvPc == 2 ? "struggling" : "?", "procs" },
                new object[] { lan == "Wi-Fi" ? "wifi" : "devices", lan, lvLan, lvLan == 0 ? "fine" : lvLan == 1 ? "shaky" : lvLan == 2 ? "problem" : (lan == "Wi-Fi" && snap.PathMain.Length > 1 && (snap.PathMain[1] ?? "").IndexOf("hidden", StringComparison.OrdinalIgnoreCase) >= 0 ? "hidden by Windows" : "?"), "link" },
                new object[] { "dest", "Internet", lvNet, lvNet == 0 ? "OK" : lvNet == 1 ? "slow" : lvNet == 2 ? "problem" : "checking...", "dest" } };
            foreach (var it in items)
            {
                int lv = (int)it[2]; string t = L.T((string)it[1]) + "  ·  " + L.T((string)it[3]); var ts = g.MeasureString(t, Draw.fBtn);
                var cr = new RectangleF(x, y, ts.Width + 50 * S, 30 * S);
                Theme.Fill(g, cr, 15 * S, lv >= 0 && lv <= 2 ? Theme.LvSoft(lv) : Theme.Soft);
                Draw.LvDot(g, lv, cr.X + 12 * S, cr.Y + 10 * S, 10 * S, 10 * S);
                Kit.Icon(g, (string)it[0], new RectangleF(cr.X + 27 * S, cr.Y + 7 * S, 16 * S, 16 * S), lv >= 0 && lv <= 2 ? Theme.LvText(lv) : Theme.Muted);
                using (var b = new SolidBrush(lv >= 1 && lv <= 2 ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(t, Draw.fBtn, b, cr.X + 47 * S, cr.Y + (cr.Height - ts.Height) / 2);
                string go = (string)it[4]; Act(pathPanel, cr, delegate { tab = go; ShowTab(); SyncNav(); }); Hint(pathPanel, cr, "path." + it[1]);
                x += cr.Width + 10 * S;
            }
            PaintVerdict(g, pathPanel, x + 10 * S, r.Height / 2f, r.Width - (onlineChip.Visible ? onlineChip.Width + 40 * S : 20 * S));
        }
        static int PathHeight { get { return Cfg.Details ? Sc(128) : Sc(58); } }
        void OpenSettingsAt(string sec)
        {
            tab = "settings"; ShowTab(); SyncNav();
            if (SetSections.Contains(sec)) { setSec = sec; if (settingsSearch != null) settingsSearch.Box.Text = ""; settingsCanvas.AutoScrollPosition = Point.Empty; settingsCanvas.Invalidate(); }
        }

        // ---------- your own tabs (11.11b): "name=kind:id;kind:id" separated by "|" in Cfg.MyTabs, up to 3, up to 24 items each
        sealed class MineTab { public string Id, Name; public List<string> Items = new List<string>(); }
        static List<MineTab> MyTabs()
        {
            var l = new List<MineTab>(); int n = 0;
            foreach (var part in (Cfg.MyTabs ?? "").Split('|'))
            {
                int eq = part.IndexOf('='); if (eq <= 0) continue;
                string name = part.Substring(0, eq).Trim(); if (name == "" || name.Length > 24) continue;
                var t = new MineTab { Id = "mine" + (++n).ToString(CultureInfo.InvariantCulture), Name = name };
                foreach (var it in part.Substring(eq + 1).Split(';')) { var s = it.Trim(); if (s.Length > 0 && s.Length < 200 && t.Items.Count < 24 && !t.Items.Contains(s)) t.Items.Add(s); }
                l.Add(t); if (n == 3) break;
            }
            return l;
        }
        static MineTab MyTab(string id) { return MyTabs().FirstOrDefault(t => t.Id == id); }
        static void SaveMyTabs(List<MineTab> l)
        {
            Cfg.MyTabs = string.Join("|", l.Take(3).Select(t => t.Name.Replace("|", " ").Replace("=", " ").Replace(";", " ") + "=" + string.Join(";", t.Items.Select(i => i.Replace("|", " ").Replace(";", " ")))));
            Cfg.Save();
        }
        // pin something to a tab of your own (the first one, made if there's none)
        public void PinToMine(string item)
        {
            var l = MyTabs(); if (l.Count == 0) l.Add(new MineTab { Id = "mine1", Name = L.T("My tab") });
            var t = l[0]; if (!t.Items.Contains(item) && t.Items.Count < 24) t.Items.Add(item);
            SaveMyTabs(l);
            if (!Cfg.NavClean(Cfg.Nav).Split(',').Contains("mine1")) { Cfg.Nav = Cfg.NavClean(Cfg.Nav + ",mine1"); Cfg.Save(); }
            ApplyLayout();
        }
        DB mineCanvas; Panel minePage;
        Panel BuildMinePage()
        {
            minePage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            mineCanvas = new DB { Name = "mineCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            mineCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintMine(e.Graphics); }; Wire(mineCanvas);
            minePage.Controls.Add(mineCanvas); return minePage;
        }
        void PaintMine(Graphics g)
        {
            g.TranslateTransform(mineCanvas.AutoScrollPosition.X, mineCanvas.AutoScrollPosition.Y); Begin(mineCanvas);
            var t = MyTab(curMine); float x = 22 * S, y = 18 * S, w = mineCanvas.ClientSize.Width - 44 * S;
            if (t == null) { y = Kit.Header(g, x, y, w, "star", "Your tab", "Pin tools here from any tool page or search result.", "mine"); return; }
            y = Kit.Header(g, x, y, w, "star", t.Name, L.T("Your own tab - one click to what you use most."), "mine");
            if (t.Items.Count == 0)
            {
                using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T("Nothing pinned yet. Open a tool and press the pin, or pin a search result."), Draw.fBody, m, x, y + 8 * S);
                mineCanvas.AutoScrollMinSize = new Size(0, (int)(y + 60 * S)); return;
            }
            int cols = Math.Max(1, (int)(w / (300 * S))); float cw = (w - (cols - 1) * 12 * S) / cols, ch = 64 * S; int i = 0;
            foreach (var it in t.Items)
            {
                string title, icon, acc; Action go = MineTarget(it, out title, out icon, out acc); if (go == null) continue;
                var r = new RectangleF(x + (i % cols) * (cw + 12 * S), y + (i / cols) * (ch + 12 * S), cw, ch);
                Kit.Tile(g, r, icon, title, null, acc, false, -1); Act(mineCanvas, r, go);
                var xr = new RectangleF(r.Right - 26 * S, r.Y + 8 * S, 18 * S, 18 * S);   // unpin
                using (var m = new SolidBrush(Theme.Muted)) g.DrawString("×", Draw.fHead, m, xr.X + 3 * S, xr.Y - 3 * S);
                string item = it; Act(mineCanvas, xr, delegate { var l = MyTabs(); var tt = l.FirstOrDefault(z => z.Id == curMine); if (tt != null) { tt.Items.Remove(item); SaveMyTabs(l); } mineCanvas.Invalidate(); });
                i++;
            }
            mineCanvas.AutoScrollMinSize = new Size(0, (int)(y + ((i + cols - 1) / cols) * (ch + 12 * S) + 30 * S));
        }
        // "tool:mtr", "nav:security", "site:github.com", "set:Wi-Fi watch"
        Action MineTarget(string item, out string title, out string icon, out string acc)
        {
            title = item; icon = "star"; acc = "mine";
            int c = item.IndexOf(':'); if (c <= 0) return null;
            string kind = item.Substring(0, c), id = item.Substring(c + 1);
            switch (kind)
            {
                case "tool":
                    var tl = Tools2().FirstOrDefault(z => z.Id == id); if (tl == null) return null;
                    title = tl.Title; icon = "tool"; acc = "toolbox"; return delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(id); SyncNav(); };
                case "nav":
                    if (!NavInfo.ContainsKey(id)) return null; title = NavName(id); icon = NavIcon(id); acc = NavAccent(id); return delegate { OpenNav(id); };
                case "site":
                    title = id; icon = "dest"; acc = "dest"; return delegate { tab = "dest"; ShowTab(); destBox.Box.Text = id; StartDest(); SyncNav(); };
                case "set":
                    title = id; icon = "settings"; acc = "reports"; return delegate { tab = "settings"; ShowTab(); if (settingsSearch != null) settingsSearch.Box.Text = id; SyncNav(); };
            }
            return null;
        }
    }
}
