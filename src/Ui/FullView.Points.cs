// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Adeebo Points dashboard
    // One place for every feature: your rank, how to earn, and every tool, Learn topic and page as a row with its points
    // and a Go that takes you straight there (a tool opens with its Example). The Adeebo button in the header opens it.
    sealed partial class FullView
    {
        Panel pointsPage; DB pointsCanvas; RoundBox pointsSearch; PillButton pointsBtn; DateTime pointsFlashUntil; string pointsFlash = "";
        string learnSeen = ""; DateTime learnAt; bool learnTimed;

        sealed class PtItem { public string Key = "", Title = "", Tag = "", Group = ""; public bool Replay; public int Value; public Action Go; }
        List<PtItem> ptItems; static readonly char[] Space = { ' ' };

        // the pages: points key, tab, name, what it is
        static readonly string[][] PtPages =
        {
            new[] { "page.guide", "guide", "What's wrong?", "answers in plain words, step by step" },
            new[] { "page.dest", "dest", "Check a destination", "is it me, the path or the site?" },
            new[] { "page.health", "health", "PC health", "memory, disk, heat and crashes" },
            new[] { "page.conn", "conn", "Connections", "every app's connections right now" },
            new[] { "page.link", "link", "Wi-Fi and link", "signal, channel and roams" },
            new[] { "page.procs", "procs", "Apps", "which app uses the network" },
            new[] { "page.security", "security", "Security scorecard", "what's open and exposed on this PC" },
            new[] { "page.gamer", "gamer", "Gamer Zone", "add a game and watch its ping" },
            new[] { "page.travel", "travel", "Network story", "who your apps talk to, and where" },
            new[] { "page.replay", "replay", "Time machine", "replay any minute of a day you recorded" },
            new[] { "page.changes", "changes", "What changed", "new apps, networks and settings" },
            new[] { "page.windows", "windows", "Windows inside", "what Windows runs, explained" },
        };

        Panel BuildPointsPage()
        {
            pointsPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            pointsCanvas = new DB { Name = "pointsCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            pointsCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintPoints(e.Graphics); }; Wire(pointsCanvas);
            var top = new Panel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(24), Sc(12), Sc(24), Sc(4)) };
            pointsSearch = new RoundBox(Sc(420), Draw.fInput, "Find a feature... mtr, dns, wifi, certificate") { Dock = DockStyle.Left };
            pointsSearch.Box.TextChanged += delegate { pointsCanvas.AutoScrollPosition = Point.Empty; pointsCanvas.Invalidate(); };
            pointsSearch.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; var f = PtFiltered().FirstOrDefault(); if (f != null) f.Go(); } };
            top.Controls.Add(pointsSearch);
            pointsPage.Controls.Add(pointsCanvas); pointsPage.Controls.Add(top);
            return pointsPage;
        }

        List<PtItem> PtAll()
        {
            if (ptItems != null) return ptItems;
            var l = new List<PtItem>();
            foreach (var t in Tools2())
            {
                string id = t.Id, sec = SectionOf(t); var term = Explain.Term("tb." + id);
                l.Add(new PtItem
                {
                    Key = id, Title = t.Title, Tag = term != null ? term.Tag : "", Group = sec == "health" ? "The PC" : SectionName(sec), Replay = Points.Replay.Contains(id), Value = Points.Value(id),
                    Go = delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(id); if (sec != "health") ShowExample(); }
                });
            }
            foreach (var t in Knowledge.Learn())
            {
                string id = t.Id;
                l.Add(new PtItem { Key = "learn." + id, Title = t["title"], Tag = t["tag"], Group = "Learn", Value = 1, Go = delegate { tab = "learn"; ShowTab(); learnSel = "topic:" + id; learnPkts = null; learnInfo.AutoScrollPosition = Point.Empty; learnList.Invalidate(); learnInfo.Invalidate(); } });
            }
            foreach (var pg in PtPages) { string tb = pg[1]; l.Add(new PtItem { Key = pg[0], Title = pg[2], Tag = pg[3], Group = "Pages", Value = 1, Go = delegate { tab = tb; ShowTab(); } }); }
            return ptItems = l;
        }
        IEnumerable<PtItem> PtFiltered()
        {
            string q = pointsSearch == null ? "" : pointsSearch.Box.Text.Trim();
            if (q == "") return PtAll();
            var words = q.Split(Space, StringSplitOptions.RemoveEmptyEntries);
            return PtAll().Where(i => words.All(wd => (i.Title + " " + i.Tag + " " + i.Group + " " + i.Key).IndexOf(wd, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        // award once, flash it on the header button; returns the line to show ("+1 Adeebo Point", "+2 replay bonus" or "")
        string EarnPts(string id, bool opened, bool completed)
        {
            if (!Cfg.Points || !completed) return "";
            bool had = Points.Me.Has(id); int n = Points.Me.Award(id, opened, true, DateTime.UtcNow);
            if (n <= 0) return "";
            Program.Log("adeebo points: " + id + " +" + n + " (total " + Points.Me.Total + ")");
            string line = had ? L.T("+2 replay bonus") : n == 1 ? L.T("+1 Adeebo Point") : "+" + n + " " + L.T("Adeebo Points");
            pointsFlash = line; pointsFlashUntil = DateTime.Now.AddSeconds(5); SyncPointsBtn();
            if (pointsCanvas != null && tab == "points") pointsCanvas.Invalidate();
            return line;
        }
        void SyncPointsBtn()
        {
            if (pointsBtn == null) return;
            bool show = Cfg.Points; if (pointsBtn.Visible != show) pointsBtn.Visible = show;
            string t = DateTime.Now < pointsFlashUntil ? pointsFlash : "Adeebo · " + Points.Me.Total.ToString(CultureInfo.CurrentCulture);
            if (pointsBtn.Text != t) pointsBtn.Text = t;
            pointsBtn.Selected = tab == "points";
        }
        // a page's first real use (Gamer Zone: a game added; Time machine: a real day on screen)
        void PageUsed(string tabId)
        {
            if (!Cfg.Points) return;
            var pg = PtPages.FirstOrDefault(p => p[1] == tabId); if (pg == null || Points.Me.Has(pg[0])) return;
            if (tabId == "gamer" && Games.Monitored().Count == 0) return;
            if (tabId == "replay" && (replayData == null || replayExample || replayData.M.Count == 0)) return;
            EarnPts(pg[0], true, true);
        }
        // a Learn topic read to the end: its end on screen, after 15 s on it
        void LearnRead(float end)
        {
            if (!Cfg.Points || !learnSel.StartsWith("topic:", StringComparison.Ordinal)) return;
            if (learnSel != learnSeen) { learnSeen = learnSel; learnAt = DateTime.Now; learnTimed = false; }
            string key = "learn." + learnSel.Substring(6); if (Points.Me.Has(key)) return;
            bool atEnd = -learnInfo.AutoScrollPosition.Y + learnInfo.ClientSize.Height >= end - 30 * S;
            if (atEnd && (DateTime.Now - learnAt).TotalSeconds >= 15) EarnPts(key, true, true);
        }
        bool LearnPending { get { return Cfg.Points && tab == "learn" && learnSel.StartsWith("topic:", StringComparison.Ordinal) && !Points.Me.Has("learn." + learnSel.Substring(6)); } }

        void PaintPoints(Graphics g)
        {
            g.TranslateTransform(pointsCanvas.AutoScrollPosition.X, pointsCanvas.AutoScrollPosition.Y); Begin(pointsCanvas);
            float x = 24 * S, y = 8 * S, w = pointsCanvas.ClientSize.Width - 48 * S;
            float top = -pointsCanvas.AutoScrollPosition.Y, bottom = top + pointsCanvas.ClientSize.Height;
            int total = Points.Me.Total, left; string rank = Points.Rank(total), next = Points.Next(total, out left);
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted)) using (var line = new Pen(Theme.Line2))
            using (var blue = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) using (var green = new SolidBrush(Theme.Green))
            using (var nowrap = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                bool searching = pointsSearch.Box.Text.Trim() != "";
                if (!searching)
                {
                    // the rank card
                    var card = new RectangleF(x, y, w, 96 * S); Theme.Fill(g, card, 14 * S, Theme.BlueSoft);
                    g.DrawString(L.T(rank) + "  ·  " + total + " " + L.T(total == 1 ? "Adeebo Point" : "Adeebo Points"), Draw.fTitle, ink, x + 18 * S, y + 14 * S);
                    if (!Cfg.Points) g.DrawString(L.T("Earning is off (Settings › Adeebo Points) - your points are kept."), Draw.fSmall, sub, x + 18 * S, y + 46 * S);
                    else
                    {
                        float bw = Math.Min(360 * S, w - 36 * S), frac = next == "" ? 1f : 1f - (float)left / Math.Max(1, left + total - RankFloor(total));
                        var bar = new RectangleF(x + 18 * S, y + 54 * S, bw, 10 * S); Theme.Fill(g, bar, 5 * S, Theme.Track);
                        if (frac > 0) Theme.Fill(g, new RectangleF(bar.X, bar.Y, Math.Max(10 * S, bw * Math.Min(1f, frac)), bar.Height), 5 * S, Theme.Blue);
                        g.DrawString(next == "" ? L.T("Top rank - every feature you tried taught you something.") : left + " " + L.T("to") + " " + L.T(next), Draw.fSmall, sub, bar.Right + 12 * S, y + 50 * S);
                    }
                    y += card.Height + 18 * S;
                    // how to earn
                    g.DrawString(L.T("HOW TO EARN"), Draw.fTiny, muted, x, y); y += 20 * S;
                    foreach (var h in new[] { new[] { "Open a tool's Example, then run it for real", "+1, or +2 for the longer tests" }, new[] { "Run a tool marked replay again, 10 minutes or more later", "+2, once" }, new[] { "Read a Learn topic to the end", "+1" }, new[] { "Use a page for the first time", "+1" } })
                    {
                        g.DrawString(L.T(h[0]), Draw.fBody, ink, x, y); g.DrawString(L.T(h[1]), Draw.fBodyB, green, x + Math.Min(420 * S, w * 0.6f), y); y += 24 * S;
                    }
                    g.DrawString(L.T("Kept on this PC only. Every feature is below - Go takes you straight there."), Draw.fSmall, muted, x, y + 4 * S); y += 34 * S;
                }
                // every feature, grouped
                var items = PtFiltered().ToList(); float rh = 30 * S, tagX = x + Math.Min(300 * S, w * 0.34f), ptsX = x + w - 150 * S;
                if (items.Count == 0) { g.DrawString(L.T("Nothing matches - try another word."), Draw.fBody, sub, x, y); y += 30 * S; }
                foreach (var grp in items.GroupBy(i => i.Group))
                {
                    var all = PtAll().Where(i => i.Group == grp.Key).ToList(); int done = all.Count(i => Points.Me.Has(i.Key));
                    y += 6 * S; g.DrawString(L.T(grp.Key).ToUpperInvariant(), Draw.fTiny, muted, x, y);
                    string dn = done + " " + L.T("of") + " " + all.Count + " " + L.T(grp.Key == "Learn" ? "read" : "done"); var ds = g.MeasureString(dn, Draw.fTiny);
                    g.DrawString(dn, Draw.fTiny, muted, x + w - ds.Width, y); y += 22 * S;
                    foreach (var it in grp)
                    {
                        if (y + rh >= top && y <= bottom)
                        {
                            bool has = Points.Me.Has(it.Key), rep = it.Replay && Points.Me.Replayed(it.Key);
                            var rr = new RectangleF(x - 6 * S, y, w + 12 * S, rh);
                            using (var dot = new SolidBrush(has ? Theme.Green : Theme.Line)) g.FillEllipse(dot, x, y + 10 * S, 10 * S, 10 * S);
                            g.DrawString(it.Title, Draw.fBodyB, ink, new RectangleF(x + 18 * S, y + 5 * S, tagX - x - 26 * S, 22 * S), nowrap);
                            g.DrawString(it.Tag, Draw.fSmall, sub, new RectangleF(tagX, y + 7 * S, ptsX - tagX - 10 * S, 22 * S), nowrap);
                            string pts = it.Value == 0 ? L.T("no points") : (has ? "✓ " : "") + "+" + it.Value + (it.Replay ? (rep ? "  ·  replay ✓" : "  ·  replay +2") : "");
                            g.DrawString(pts, Draw.fSmall, it.Value == 0 ? muted : has ? green : ink, ptsX, y + 7 * S);
                            g.DrawString(L.T("Go") + " ›", Draw.fBodyB, blue, x + w - 34 * S, y + 5 * S);
                            var go = it.Go; Act(pointsCanvas, rr, delegate { go(); });
                            g.DrawLine(line, x, y + rh, x + w, y + rh);
                        }
                        y += rh;
                    }
                    y += 8 * S;
                }
            }
            Need(pointsCanvas, y);
        }
        static int RankFloor(int total) { int f = 0; foreach (var t in new[] { 0, 15, 50, 100 }) if (total >= t) f = t; return f; }
    }
}
