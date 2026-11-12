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
                    Go = delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); SelectTool(id); ShowExample(); }   // THE PC tools too: their Example first, never a whole-disk scan straight away
                });
            }
            foreach (var t in Knowledge.Learn())
            {
                string id = t.Id;
                Action go = delegate { tab = "learn"; ShowTab(); learnSel = "topic:" + id; learnPkts = null; learnInfo.AutoScrollPosition = Point.Empty; learnList.Invalidate(); learnInfo.Invalidate(); };
                l.Add(new PtItem { Key = "learn." + id, Title = t["title"], Tag = t["tag"], Group = "Learn", Value = Points.Value("learn." + id), Go = go });
                if (t["check"] != "") l.Add(new PtItem { Key = "check." + id, Title = t["title"], Tag = CheckParts(t["check"])[0], Group = "Check yourself", Value = Points.Value("check." + id), Go = go });
            }
            // lab challenges, subnetting streaks, the packet journey
            foreach (var lab in Labs)
            {
                string id = lab[0]; Action go = delegate { tab = "learn"; ShowTab(); learnSel = "lab:" + id; learnInfo.AutoScrollPosition = Point.Empty; learnList.Invalidate(); learnInfo.Invalidate(); };
                if (id == "quiz") { l.Add(new PtItem { Key = "quiz.5", Title = "Subnetting: 5 in a row", Tag = "network, broadcast, hosts - without a slip", Group = "Challenges", Value = Points.Value("quiz.5"), Go = go }); l.Add(new PtItem { Key = "quiz.10", Title = "Subnetting: 10 in a row", Tag = "the same, twice as long", Group = "Challenges", Value = Points.Value("quiz.10"), Go = go }); continue; }
                string goal; LabGoals.TryGetValue(id, out goal);
                l.Add(new PtItem { Key = "lab." + id, Title = lab[1], Tag = goal ?? "", Group = "Challenges", Value = Points.Value("lab." + id), Go = go });
            }
            l.Add(new PtItem { Key = "journey.explain", Title = "Explain a packet journey", Tag = "capture one of your own connections and press Explain", Group = "Challenges", Value = Points.Value("journey.explain"), Go = delegate { tab = "travel"; ShowTab(); } });
            foreach (var pg in PtPages) { string tb = pg[1]; l.Add(new PtItem { Key = pg[0], Title = pg[2], Tag = pg[3], Group = "Pages", Value = Points.Value(pg[0]), Go = delegate { tab = tb; ShowTab(); } }); }
            foreach (var pa in Paths) { var p2 = pa; l.Add(new PtItem { Key = "path." + pa.Id, Title = pa.Title, Tag = pa.Tag, Group = "Learning paths", Value = Points.Value("path." + pa.Id), Go = delegate { tab = "points"; ShowTab(); pointsSearch.Box.Text = ""; pointsCanvas.AutoScrollPosition = Point.Empty; pointsCanvas.Invalidate(); } }); }
            return ptItems = l;
        }

        // ---------- learning paths: a short list in a sensible order; finishing one is +10, once
        sealed class PathDef { public string Id, Title, Tag; public string[] Keys; }
        static readonly PathDef[] Paths =
        {
            new PathDef { Id = "home", Title = "Home user", Tag = "why the internet feels slow, and what to do about it", Keys = new[] { "learn.osi", "learn.open-website", "learn.dns", "learn.dhcp", "learn.wifi", "learn.speed-vs-delay", "learn.background-chatter", "page.guide", "speedtest", "hotel", "usb", "winupdate", "lab.airtime" } },
            new PathDef { Id = "gamer", Title = "Gamer", Tag = "ping, jitter and lag - where every millisecond goes", Keys = new[] { "learn.udp", "learn.speed-vs-delay", "learn.streaming-vs-calls", "page.gamer", "ping", "lag", "bloat", "gamecompare", "lab.bloat", "lab.jitter", "lab.game" } },
            new PathDef { Id = "engineer", Title = "Network engineer", Tag = "TCP, MTU, routing and subnets - from the packets up", Keys = new[] { "learn.tcp", "learn.mtu", "learn.nat", "learn.ipv6", "learn.bgp", "mtr", "mtu", "subnet", "vlsm", "lab.tcp", "lab.mtu", "lab.route", "quiz.10", "journey.explain" } },
        };
        public static IEnumerable<string> PathKeys { get { return Paths.SelectMany(p => p.Keys); } }   // the self-test checks every one exists
        // is this a points key something on the Points page can earn? (the self-test runs every path step through it)
        public static bool PtKeyKnown(string k)
        {
            if (k.StartsWith("learn.", StringComparison.Ordinal)) return Knowledge.Topic(k.Substring(6)) != null;
            if (k.StartsWith("check.", StringComparison.Ordinal)) { var t = Knowledge.Topic(k.Substring(6)); return t != null && t["check"] != ""; }
            if (k.StartsWith("lab.", StringComparison.Ordinal)) return LabGoals.ContainsKey(k.Substring(4)) && k != "lab.quiz";
            if (k.StartsWith("page.", StringComparison.Ordinal)) return PtPages.Any(p => p[0] == k);
            if (k == "quiz.5" || k == "quiz.10" || k == "journey.explain") return true;
            return Explain.Term("tb." + k) != null && Points.Value(k) > 0;
        }
        void CheckPaths()
        {
            if (!Cfg.Points) return;
            foreach (var p in Paths) if (!Points.Me.Has("path." + p.Id) && p.Keys.All(Points.Me.Has)) EarnPts("path." + p.Id, true, true);
        }
        // "question | right answer | wrong | wrong | wrong"
        static string[] CheckParts(string s) { return (s ?? "").Split('|').Select(x => x.Trim()).ToArray(); }
        IEnumerable<PtItem> PtFiltered()
        {
            string q = pointsSearch == null ? "" : pointsSearch.Box.Text.Trim();
            if (q == "") return PtAll();
            var words = q.Split(Space, StringSplitOptions.RemoveEmptyEntries);
            return PtAll().Where(i => words.All(wd => (i.Title + " " + L.T(i.Title) + " " + i.Tag + " " + i.Group + " " + L.T(i.Group) + " " + i.Key).IndexOf(wd, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        // award once, flash it on the header button; returns the line to show ("+2 Adeebo Points", "+3 replay bonus" or "")
        string EarnPts(string id, bool opened, bool completed)
        {
            if (!Cfg.Points || !completed) return "";
            bool had = Points.Me.Has(id); int before = Points.Me.Total, n = Points.Me.Award(id, opened, true, DateTime.UtcNow);
            if (n <= 0) return "";
            Program.Log("adeebo points: " + id + " +" + n + " (total " + Points.Me.Total + ")");
            string line = had ? "+" + n + " " + L.T("replay bonus") : id.StartsWith("path.", StringComparison.Ordinal) ? "+" + n + " " + L.T("path finished") : "+" + n + " " + L.T("Adeebo Points");
            pointsFlash = line; pointsFlashUntil = DateTime.Now.AddSeconds(5);
            if (before < Points.AwardAt && Points.Me.Total >= Points.AwardAt) { pointsFlash = L.T("You got the Adeebo Award!"); pointsFlashUntil = DateTime.Now.AddSeconds(12); Program.Log("adeebo points: the Adeebo Award"); }
            SyncPointsBtn();
            if (!id.StartsWith("path.", StringComparison.Ordinal)) CheckPaths();
            if (pointsCanvas != null && tab == "points") pointsCanvas.Invalidate();
            return line;
        }
        void SyncPointsBtn()
        {
            if (pointsBtn == null) return;
            bool show = Cfg.Points; if (pointsBtn.Visible != show) pointsBtn.Visible = show;
            string t = DateTime.Now < pointsFlashUntil ? pointsFlash : "Adeebo · " + Points.Me.Total.ToString(CultureInfo.CurrentCulture);
            if (pointsBtn.Text != L.T(t)) pointsBtn.Text = t;
            pointsBtn.Selected = tab == "points";
        }
        // "first use" means doing something there - a click or a key on the page - not just opening the tab
        readonly HashSet<string> useWatched = new HashSet<string>();
        void WatchUse(string tabId)
        {
            if (tabId == "gamer" || tabId == "replay" || useWatched.Contains(tabId) || !PtPages.Any(p => p[1] == tabId)) return;
            var pages = new Dictionary<string, Control> { { "guide", guidePage }, { "dest", destPage }, { "health", healthPage }, { "conn", connPage }, { "link", linkPage }, { "procs", procPage }, { "security", securityPage }, { "travel", travelPage }, { "changes", changesPage }, { "windows", insidePage } };
            Control page; if (!pages.TryGetValue(tabId, out page) || page == null) return;
            useWatched.Add(tabId); string id = tabId;
            Action<Control> hook = null;
            hook = c => { c.MouseDown += delegate { PageUsed(id); }; c.KeyDown += delegate { PageUsed(id); }; foreach (Control k in c.Controls) hook(k); };
            hook(page);
        }
        // a page's first real use (Gamer Zone: a game added; Time machine: a real day on screen; the others: a click or a key there)
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
            CheckPaths();   // a path finished before 1.1.5 (or in another window) is paid the first time this page is drawn
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
                    bool award = total >= Points.AwardAt;
                    var card = new RectangleF(x, y, w, 96 * S); Theme.Fill(g, card, 14 * S, award ? Theme.LvSoft(1) : Theme.BlueSoft);
                    if (award) Theme.Stroke(g, card, 14 * S, Theme.Amber, 2);
                    g.DrawString(award ? "★  " + L.T("You got the Adeebo Award") + "  ·  " + total + " " + L.T("Adeebo Points") : L.T(rank) + "  ·  " + total + " " + L.T(total == 1 ? "Adeebo Point" : "Adeebo Points"), Draw.fTitle, ink, x + 18 * S, y + 14 * S);
                    if (!Cfg.Points) g.DrawString(L.T("Earning is off (Settings › Adeebo Points) - your points are kept."), Draw.fSmall, sub, x + 18 * S, y + 46 * S);
                    else
                    {
                        float bw = Math.Min(360 * S, w - 36 * S), frac = next == "" ? 1f : 1f - (float)left / Math.Max(1, left + total - RankFloor(total));
                        var bar = new RectangleF(x + 18 * S, y + 54 * S, bw, 10 * S); Theme.Fill(g, bar, 5 * S, Theme.Track);
                        if (frac > 0) Theme.Fill(g, new RectangleF(bar.X, bar.Y, Math.Max(10 * S, bw * Math.Min(1f, frac)), bar.Height), 5 * S, Theme.Blue);
                        g.DrawString(next == "" ? L.T("Nearly everything AdeeboMeter can teach, tried and understood. Well done.") : left + " " + L.T("to") + " " + L.T(next), Draw.fSmall, sub, bar.Right + 12 * S, y + 50 * S);
                    }
                    y += card.Height + 18 * S;
                    // how to earn
                    g.DrawString(L.T("HOW TO EARN"), Draw.fTiny, muted, x, y); y += 20 * S;
                    foreach (var h in new[] { new[] { "Open a tool's Example, then run it for real", "+2, or +4 for the longer tests" }, new[] { "Run a tool marked replay again, 10 minutes or more later", "+3, once" }, new[] { "Read a Learn topic to the end", "+2" }, new[] { "Answer its Check yourself question right first time", "+2" }, new[] { "Meet a lab's challenge", "+4" }, new[] { "Subnetting practice: 5 in a row, then 10 in a row", "+3, then +5" }, new[] { "Explain a packet journey of your own", "+3" }, new[] { "Use a page for the first time", "+2" }, new[] { "Finish a learning path", "+10" }, new[] { "Reach " + Points.AwardAt, "the Adeebo Award" } })
                    {
                        g.DrawString(L.T(h[0]), Draw.fBody, ink, x, y); g.DrawString(L.T(h[1]), Draw.fBodyB, green, x + Math.Min(420 * S, w * 0.6f), y); y += 24 * S;
                    }
                    g.DrawString(L.T("Kept on this PC only. Every feature is below - Go takes you straight there."), Draw.fSmall, muted, x, y + 4 * S); y += 34 * S;
                    // learning paths: progress for each
                    g.DrawString(L.T("LEARNING PATHS"), Draw.fTiny, muted, x, y); y += 22 * S;
                    var byKey = PtAll().GroupBy(i => i.Key).ToDictionary(gr => gr.Key, gr => gr.First());
                    float pw = Math.Max(220 * S, (w - 28 * S) / 3);
                    for (int i = 0; i < Paths.Length; i++)
                    {
                        var pa = Paths[i]; int got = pa.Keys.Count(Points.Me.Has); bool fin = Points.Me.Has("path." + pa.Id) || got == pa.Keys.Length;
                        float px = w >= 700 * S ? x + i * (pw + 14 * S) : x, py = w >= 700 * S ? y : y + i * 96 * S; var pc = new RectangleF(px, py, w >= 700 * S ? pw : w, 84 * S);
                        Theme.Fill(g, pc, 12 * S, fin ? Theme.LvSoft(0) : Theme.Soft);
                        g.DrawString((fin ? "✓ " : "") + L.T(pa.Title), Draw.fBodyB, ink, pc.X + 14 * S, pc.Y + 10 * S);
                        g.DrawString(L.T(pa.Tag), Draw.fSmall, sub, new RectangleF(pc.X + 14 * S, pc.Y + 32 * S, pc.Width - 28 * S, 20 * S), nowrap);
                        var pb = new RectangleF(pc.X + 14 * S, pc.Y + 60 * S, pc.Width - 100 * S, 8 * S); Theme.Fill(g, pb, 4 * S, Theme.Track);
                        if (got > 0) Theme.Fill(g, new RectangleF(pb.X, pb.Y, Math.Max(8 * S, pb.Width * got / pa.Keys.Length), pb.Height), 4 * S, fin ? Theme.Green : Theme.Blue);
                        g.DrawString(got + " / " + pa.Keys.Length + (fin ? "" : "  · +10"), Draw.fTiny, fin ? green : muted, pb.Right + 10 * S, pb.Y - 4 * S);
                        // Go: the first step not done yet
                        var nextKey = pa.Keys.FirstOrDefault(k2 => !Points.Me.Has(k2)); PtItem nx;
                        if (nextKey != null && byKey.TryGetValue(nextKey, out nx)) { var go = nx.Go; Act(pointsCanvas, pc, delegate { go(); }); }
                    }
                    y += (w >= 700 * S ? 84 * S : Paths.Length * 96 * S) + 22 * S;
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
                            string pts = it.Value == 0 ? L.T("no points") : (has ? "✓ " : "") + "+" + it.Value + (it.Replay ? (rep ? "  ·  replay ✓" : "  ·  replay +" + Points.ReplayPts) : "");
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
        static int RankFloor(int total) { int f = 0; foreach (var t in Points.Floors) if (total >= t) f = t; return f; }
    }
}
