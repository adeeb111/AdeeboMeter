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
        static readonly Dictionary<string, string> LearnIcons = new Dictionary<string, string>(StringComparer.Ordinal) {
            { "basics", "star" }, { "protocol", "traffic" }, { "provider", "route" }, { "mobile", "phone" }, { "crypto", "lock" }, { "windows", "windows" },
            { "apps", "app" }, { "streaming", "app" }, { "investing", "traffic" }, { "labs", "speed" }, { "games", "game" }, { "check", "check" }, { "paths", "go" } };
        static readonly string[][] LearnCards = {
            new[] { "basics", "Start here", "How apps use the internet - in plain words." },
            new[] { "protocol", "How your packets talk", "DNS, DHCP, TCP, TLS, Wi-Fi... two lines and a picture each." },
            new[] { "provider", "Inside the internet", "BGP, OSPF and IS-IS, MPLS, L3VPNs, multicast TV, how Netflix and live football reach you." },
            new[] { "mobile", "Inside the mobile network", "Towers, the 4G and 5G cores (MME, AMF, SMF, UPF), attach, sessions, NSA vs SA." },
            new[] { "crypto", "Locks and keys", "Hashes, keys, signatures, certificates and the padlock - why HTTPS works, and why it breaks." },
            new[] { "windows", "Inside Windows", "What all those processes and services are for." },
            new[] { "apps", "Teams and office apps", "What a call or a sync really does on the network." },
            new[] { "streaming", "Streaming", "How video reaches you, why it buffers, and whose fault it is - with three games." },
            new[] { "investing", "Money & investing", "Bull and bear markets, recessions, inflation, compound growth, emergency funds, budgets and scams - learning, never advice. Seven games." },
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
                case "labs": all = Labs.Length; return Labs.Count(l => l[0] == "quiz" ? Points.Me.Has("quiz.10") : Points.Me.Has("lab." + l[0]));   // the same tick as the Labs page
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
            int cols = w > 900 * S ? 4 : w > 560 * S ? 3 : 2; float cw = (w - (cols - 1) * 12 * S) / cols, ch = 128 * S;
            for (int i = 0; i < LearnCards.Length; i++)
            {
                var c = LearnCards[i]; var r = new RectangleF(x + (i % cols) * (cw + 12 * S), y + (i / cols) * (ch + 12 * S), cw, ch);
                int all, done = LearnDone(c[0], out all);
                // an icon card with a progress ring (PLAN 11.5)
                string acc = c[0] == "games" ? "gamer" : c[0] == "labs" ? "toolbox" : "learn", icon; if (!LearnIcons.TryGetValue(c[0], out icon)) icon = "learn";
                Kit.Card(g, r, acc, false);
                using (var cb = new SolidBrush(Kit.Soft(acc))) g.FillEllipse(cb, r.X + 14 * S, r.Y + 16 * S, 34 * S, 34 * S);
                Kit.Icon(g, icon, new RectangleF(r.X + 22 * S, r.Y + 24 * S, 18 * S, 18 * S), Kit.Accent(acc));
                var ring = new RectangleF(r.Right - 52 * S, r.Y + 14 * S, 38 * S, 38 * S);
                using (var tp = new Pen(Theme.Track, 4 * S)) g.DrawEllipse(tp, ring);
                if (all > 0 && done > 0) using (var ap = new Pen(done >= all ? Theme.Green : Kit.Accent(acc), 4 * S) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round }) g.DrawArc(ap, ring, -90, 360f * done / all);
                bool full = done >= all && all > 0;
                using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var mb = new SolidBrush(full ? Theme.Green : Theme.Muted))
                using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(full ? "✓" : done + "/" + all, Draw.fTiny, mb, ring, center);
                    g.DrawString(L.T(c[1]), Draw.fHead, ink, new RectangleF(r.X + 14 * S, r.Y + 60 * S, r.Width - 28 * S, 24 * S), GameNoWrap);
                    g.DrawString(L.T(c[2]), Draw.fSmall, sub, new RectangleF(r.X + 14 * S, r.Y + 86 * S, r.Width - 28 * S, 2 * Draw.fSmall.GetHeight(g) + 1), TwoLines);
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
                case "labs": foreach (var lab in Labs) l.Add(new[] { "lab:" + lab[0], lab[1], lab[2], Points.Me.Has("lab." + lab[0]) || (lab[0] == "quiz" && Points.Me.Has("quiz.10")) ? "1" : "" }); break;
                case "games": foreach (var gm in LearnGames.All) { string pre = GamePrefix(gm[0]); l.Add(new[] { "game:" + gm[0], gm[1], L.T(gm[2]) + " · " + L.T(gm[3]), LearnGames.Keys().Where(k => k.StartsWith(pre, StringComparison.Ordinal)).All(Points.Me.Has) ? "1" : "" }); } break;
                case "check": foreach (var t in Knowledge.Learn().Where(t => t["check"] != "")) l.Add(new[] { "topic:" + t.Id, t["title"], CheckParts(t["check"])[0], Points.Me.Has("check." + t.Id) ? "1" : "" }); break;
                default:
                    foreach (var t in Knowledge.Learn().Where(t => t["kind"] == card)) l.Add(new[] { "topic:" + t.Id, t["title"], t["tag"], Points.Me.Has("learn." + t.Id) ? "1" : "" });
                    if (card == "investing") l.Add(new[] { "game:years", L.T("Ten years of money") + " (" + L.T("game") + ")", L.T("Crashes, a recession, inflation and surprise bills - where does your money go?"), Points.Me.Has("game.years") ? "1" : "" });
                    foreach (var gm in LearnRounds.Games.Where(z => z[3] == card)) l.Add(new[] { "game:" + gm[0], gm[1] + " (" + L.T("game") + ")", gm[2], Points.Me.Has("game.rounds." + gm[0]) ? "1" : "" });
                    break;
            }
            return l;
        }
        float PaintLearnGroup(Graphics g, float x, float y, float w)
        {
            var card = LearnCards.FirstOrDefault(c => c[0] == learnGroup) ?? LearnCards[0];
            y = BackLink(g, "‹ " + L.T("All of Learn"), x, y, delegate { learnGroup = ""; });
            y = Title2(g, L.T(card[1]), x, y, w); y = Lead2(g, L.T(card[2]), x, y, w);
            var items = LearnItems(card[0]); int cols = w > 700 * S ? 2 : 1; float cw = (w - (cols - 1) * 12 * S) / cols, ch = 80 * S;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i]; var r = new RectangleF(x + (i % cols) * (cw + 12 * S), y + (i / cols) * (ch + 10 * S), cw, ch);
                // a raised tile with a dot in the group's colour (a tick once you've done it)
                Kit.Box(g, r, 14 * S); bool done = it[3] == "1";
                var dot = new RectangleF(r.X + 14 * S, r.Y + (ch - 22 * S) / 2, 22 * S, 22 * S);
                Theme.Fill(g, dot, 11 * S, done ? Theme.LvSoft(0) : Kit.Soft("learn"));
                if (done) Kit.Icon(g, "check", new RectangleF(dot.X + 4 * S, dot.Y + 4 * S, 14 * S, 14 * S), Theme.LvText(0));
                else Theme.Fill(g, new RectangleF(dot.X + 8 * S, dot.Y + 8 * S, 6 * S, 6 * S), 3 * S, Kit.Accent("learn"));
                float tx = r.X + 48 * S, tw = r.Width - 62 * S;
                using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub))
                {
                    g.DrawString(L.T(it[1]), Draw.fBodyB, ink, new RectangleF(tx, r.Y + 12 * S, tw, 22 * S), GameNoWrap);
                    g.DrawString(L.T(it[2]), Draw.fSmall, sub, new RectangleF(tx, r.Y + 34 * S, tw, 2 * Draw.fSmall.GetHeight(g) + 1), TwoLines);
                }
                string key = it[0]; Act(learnInfo, r, delegate { learnSel = key; learnPkts = null; learnErr = ""; learnInfo.AutoScrollPosition = Point.Empty; learnInfo.Invalidate(); });
            }
            return y + (float)Math.Ceiling(items.Count / (double)cols) * (ch + 10 * S) + 10 * S;
        }
        float BackLink(Graphics g, string t, float x, float y, Action a)
        {
            // the big Back button, in Learn's colour (the "‹ " callers pass is dropped - the button draws its own arrow)
            if (t.StartsWith("‹ ", StringComparison.Ordinal)) t = t.Substring(2);
            var r = Kit.Back(g, t, x, y, "learn", false);
            Act(learnInfo, r, delegate { a(); learnInfo.AutoScrollPosition = Point.Empty; learnInfo.Invalidate(); });
            return r.Bottom + 14 * S;
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
