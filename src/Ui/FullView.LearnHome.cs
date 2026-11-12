// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Learn, two ways (like the Toolbox): By goal - cards for each part, with your progress - or All, the whole list beside
    // the reading pane with filter chips. Cfg.LearnView keeps the choice.
    sealed partial class FullView
    {
        TableLayoutPanel learnSplit; PillButton learnViewG, learnViewA; readonly List<PillButton> learnChips = new List<PillButton>(); Label learnTip;
        string learnGroup = "", learnFilter = "all";
        static readonly string[][] LearnCards = {
            new[] { "basics", "Start here", "How apps use the internet - in plain words." },
            new[] { "protocol", "How your packets talk", "DNS, DHCP, TCP, TLS, Wi-Fi... two lines and a picture each." },
            new[] { "windows", "Inside Windows", "What all those processes and services are for." },
            new[] { "apps", "Teams and office apps", "What a call or a sync really does on the network." },
            new[] { "labs", "Labs", "Play with how networks behave, with your own numbers." },
            new[] { "games", "Games", "An escape room, catch the thief, packet detective, quick-fire rounds." },
            new[] { "check", "Check yourself", "One question per topic - right first time earns." },
            new[] { "paths", "Learning paths", "Home, gamer, engineer: a route through the lot." } };

        void AddLearnViewButtons(FlowLayoutPanel bar)
        {
            learnViewG = new PillButton("By goal", PillButton.K.Chip) { Margin = new Padding(0, Sc(3), Sc(4), 0) }; learnViewG.Click += delegate { SetLearnView("goal"); };
            learnViewA = new PillButton("All", PillButton.K.Chip) { Margin = new Padding(0, Sc(3), Sc(14), 0) }; learnViewA.Click += delegate { SetLearnView("all"); };
            bar.Controls.Add(learnViewG); bar.Controls.Add(learnViewA);
        }
        void AddLearnChips(FlowLayoutPanel bar)
        {
            foreach (var c in new[] { new[] { "all", "Everything" }, new[] { "topics", "Topics" }, new[] { "labs", "Labs" }, new[] { "games", "Games" }, new[] { "terms", "Terms" } })
            {
                string id = c[0]; var b = new PillButton(c[1], PillButton.K.Chip) { Margin = new Padding(Sc(4), Sc(3), 0, 0), Tag = id };
                b.Click += delegate { learnFilter = id; SyncLearnView(); learnList.AutoScrollPosition = Point.Empty; learnList.Invalidate(); };
                learnChips.Add(b); bar.Controls.Add(b);
            }
        }
        void SetLearnView(string v) { Cfg.LearnView = v; Cfg.Save(); if (v == "goal") { learnGroup = ""; learnSel = ""; } SyncLearnView(); learnInfo.AutoScrollPosition = Point.Empty; learnList.Invalidate(); learnInfo.Invalidate(); }
        void SyncLearnView()
        {
            bool all = Cfg.LearnView == "all";
            if (learnViewG != null) { learnViewG.Selected = !all; learnViewA.Selected = all; }
            foreach (var c in learnChips) { c.Visible = all; c.Selected = (string)c.Tag == learnFilter; }
            if (learnTip != null) learnTip.Visible = !all;
            if (learnSplit != null && learnSplit.ColumnStyles.Count == 2)
            {
                learnSplit.SuspendLayout();
                learnSplit.ColumnStyles[0].Width = all ? 34 : 0; learnSplit.ColumnStyles[1].Width = all ? 66 : 100; learnList.Visible = all;
                learnSplit.ResumeLayout();
            }
        }

        // ---------- By goal: the cards, or one card's page
        static int LearnDone(string card, out int all)
        {
            var t = Knowledge.Learn();
            switch (card)
            {
                case "labs": { var ls = Labs.Where(l => l[0] != "quiz").ToList(); all = ls.Count; return ls.Count(l => Points.Me.Has("lab." + l[0])); }
                case "games": { var k = LearnGames.Keys().ToList(); all = k.Count; return k.Count(Points.Me.Has); }
                case "check": { var c = t.Where(x => x["check"] != "").ToList(); all = c.Count; return c.Count(x => Points.Me.Has("check." + x.Id)); }
                case "paths": all = Paths.Length; return Paths.Count(p => Points.Me.Has("path." + p.Id));
                default: { var l = t.Where(x => x["kind"] == card).ToList(); all = l.Count; return l.Count(x => Points.Me.Has("learn." + x.Id)); }
            }
        }
        float PaintLearnHome(Graphics g, float x, float y, float w)
        {
            if (learnGroup != "") return PaintLearnGroup(g, x, y, w);
            y = Title2(g, L.T("Learn"), x, y, w);
            y = Lead2(g, L.T("Pick a goal. New to networking? Start here. Want to play? Games and Labs. Search above shows everything at once."), x, y, w);
            int cols = w > 900 * S ? 4 : w > 560 * S ? 3 : 2; float cw = (w - (cols - 1) * 12 * S) / cols, ch = 118 * S;
            for (int i = 0; i < LearnCards.Length; i++)
            {
                var c = LearnCards[i]; var r = new RectangleF(x + (i % cols) * (cw + 12 * S), y + (i / cols) * (ch + 12 * S), cw, ch);
                int all, done = LearnDone(c[0], out all);
                Theme.Fill(g, r, 12 * S, c[0] == "games" ? Theme.BlueSoft : Theme.Soft);
                using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var mb = new SolidBrush(Theme.Muted))
                {
                    g.DrawString(L.T(c[1]), Draw.fHead, ink, new RectangleF(r.X + 14 * S, r.Y + 12 * S, r.Width - 28 * S, 24 * S), GameNoWrap);
                    g.DrawString(L.T(c[2]), Draw.fSmall, sub, new RectangleF(r.X + 14 * S, r.Y + 40 * S, r.Width - 28 * S, 40 * S));
                    string cnt = done + " / " + all + " " + L.T(c[0] == "paths" ? "finished" : "done");
                    g.DrawString(cnt, Draw.fTiny, mb, r.X + 14 * S, r.Bottom - 28 * S);
                    var bar = new RectangleF(r.X + 14 * S, r.Bottom - 12 * S, r.Width - 28 * S, 4 * S);
                    using (var bb = new SolidBrush(Theme.Line)) g.FillRectangle(bb, bar);
                    if (all > 0 && done > 0) using (var fb = new SolidBrush(Theme.Green)) g.FillRectangle(fb, bar.X, bar.Y, bar.Width * done / all, bar.Height);
                }
                string id = c[0];
                Act(learnInfo, r, delegate { if (id == "paths") { tab = "points"; ShowTab(); return; } learnGroup = id; learnInfo.AutoScrollPosition = Point.Empty; learnInfo.Invalidate(); });
            }
            y += (float)Math.Ceiling(LearnCards.Length / (double)cols) * (ch + 12 * S) + 10 * S;
            return y;
        }
        // one card's page: its items as tiles, ✓ on the ones done
        static List<string[]> LearnItems(string card)   // key, title, tag, done ("1" / "")
        {
            var l = new List<string[]>();
            switch (card)
            {
                case "labs": foreach (var lab in Labs) l.Add(new[] { "lab:" + lab[0], lab[1], lab[2], Points.Me.Has("lab." + lab[0]) || (lab[0] == "quiz" && Points.Me.Has("quiz.5")) ? "1" : "" }); break;
                case "games": foreach (var gm in LearnGames.All) { string pre = GamePrefix(gm[0]); l.Add(new[] { "game:" + gm[0], gm[1], gm[2] + " · " + gm[3], LearnGames.Keys().Where(k => k.StartsWith(pre, StringComparison.Ordinal)).All(Points.Me.Has) ? "1" : "" }); } break;
                case "check": foreach (var t in Knowledge.Learn().Where(t => t["check"] != "")) l.Add(new[] { "topic:" + t.Id, t["title"], CheckParts(t["check"])[0], Points.Me.Has("check." + t.Id) ? "1" : "" }); break;
                default: foreach (var t in Knowledge.Learn().Where(t => t["kind"] == card)) l.Add(new[] { "topic:" + t.Id, t["title"], t["tag"], Points.Me.Has("learn." + t.Id) ? "1" : "" }); break;
            }
            return l;
        }
        float PaintLearnGroup(Graphics g, float x, float y, float w)
        {
            var card = LearnCards.FirstOrDefault(c => c[0] == learnGroup) ?? LearnCards[0];
            y = BackLink(g, "‹ " + L.T("All of Learn"), x, y, delegate { learnGroup = ""; });
            y = Title2(g, L.T(card[1]), x, y, w); y = Lead2(g, L.T(card[2]), x, y, w);
            var items = LearnItems(card[0]); int cols = w > 700 * S ? 2 : 1; float cw = (w - (cols - 1) * 12 * S) / cols, ch = 66 * S;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i]; var r = new RectangleF(x + (i % cols) * (cw + 12 * S), y + (i / cols) * (ch + 10 * S), cw, ch);
                Theme.Fill(g, r, 10 * S, Theme.Soft);
                using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var okb = new SolidBrush(Theme.LvText(0)))
                {
                    g.DrawString((it[3] == "1" ? "✓ " : "") + L.T(it[1]), Draw.fBodyB, it[3] == "1" ? okb : ink, new RectangleF(r.X + 14 * S, r.Y + 10 * S, r.Width - 28 * S, 22 * S), GameNoWrap);
                    g.DrawString(L.T(it[2]), Draw.fSmall, sub, new RectangleF(r.X + 14 * S, r.Y + 34 * S, r.Width - 28 * S, 22 * S), GameNoWrap);
                }
                string key = it[0]; Act(learnInfo, r, delegate { learnSel = key; learnPkts = null; learnErr = ""; learnInfo.AutoScrollPosition = Point.Empty; learnInfo.Invalidate(); });
            }
            return y + (float)Math.Ceiling(items.Count / (double)cols) * (ch + 10 * S) + 10 * S;
        }
        float BackLink(Graphics g, string t, float x, float y, Action a)
        {
            var ts = g.MeasureString(t, Draw.fBodyB); var r = new RectangleF(x, y, ts.Width + 4 * S, ts.Height + 4 * S);
            using (var b = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(t, Draw.fBodyB, b, x, y);
            Act(learnInfo, r, delegate { a(); learnInfo.AutoScrollPosition = Point.Empty; learnInfo.Invalidate(); });
            return y + 30 * S;
        }
        // the games in the All list
        float PaintGameList(Graphics g, float x, float y, float w, Brush ink, Brush muted, StringFormat nowrap, Func<string, bool> hit)
        {
            var list = LearnGames.All.Where(z => hit(z[1]) || hit(z[2]) || hit("game")).ToList(); if (list.Count == 0) return y;
            g.DrawString(L.T("GAMES - PLAY TO LEARN"), Draw.fTiny, muted, x, y + 6 * S); y += 26 * S;
            foreach (var z in list) y = LearnRow(g, "game:" + z[0], L.T(z[1]), L.T(z[2]), x, y, w, ink, muted, nowrap);
            return y + 8 * S;
        }
    }
}
