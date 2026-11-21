// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Toolbox, by question (the default view since 1.1.5)
    // One big search, your recent tools, and a card per question you'd ask - "Can I reach it?", "Why is it slow?" - with its
    // three most useful tools in plain words. A tool opens across the whole width. "Every tool" (FullView.Index.cs) lists them all,
    // each once; the choice is remembered. (bug-hunt L-153: the classic "All tools (list)" view is gone.)
    sealed partial class FullView
    {
        // id, question, the groups (from TbLayout) it covers, its three featured tools
        static readonly string[][] Questions = {
            new[] { "reach", "Can I reach it?", "IS IT REACHABLE?|NAMES & TIME", "port|http|allips" },
            new[] { "slow", "Why is it slow?", "SPEED & QUALITY|COMPARE & WATCH", "lag|speedtest|timeline" },
            new[] { "path", "Where does it break?", "PATH & ROUTING", "mtr|mtu|route" },
            new[] { "lan", "What's on my network?", "SWITCHES & ROUTERS (SNMP)|ON YOUR NETWORK|REMOTE ACCESS", "inventory|snmp|arp" },
            new[] { "join", "Why can't it join?", "WHY CAN'T IT JOIN?|REPLAY A CAPTURE|TRUST & MAIL", "join|radiuslab|dhcpreplay" },
            new[] { "keys", "Keys and encryption", "KEYS & ENCRYPTION", "encrypt|decrypt|keys" },
            new[] { "cert", "Is the padlock OK?", "CERTIFICATES & HTTPS", "https|chain|certfile" },
            new[] { "win", "Windows", "WINDOWS", "winev|certs|fw" },
            new[] { "lab", "Build test traffic", "BUILD A PACKET|TEST A COLLECTOR|LOAD & QUEUES", "icmpb|udpsend|iperf" },
            new[] { "pc", "Is it this PC?", "THE PC|THIS PC AND YOUR PHONE", "phonevs|usb|winupdate" },
            new[] { "hw", "Hardware nerds", "HARDWARE NERDS", "feltslow|hwmap|diskspeed" },   // the PC's own checks: on no other card
            new[] { "calc", "Work it out", "NETWORK MATHS|WIRELESS MATHS|SERVERS & IT MATHS|STREAMING & HOME MATHS|VOICE & TDM MATHS|MONEY & MARKETS MATHS|COMMODITY MATHS|GAMING MATHS", "subnet|plan|stream" },   // pure arithmetic, nothing sent
            new[] { "game", "Is it my game?", "GAMING", "prematch|gpn|tonight" },
            new[] { "time", "How has my network been?", "OVER TIME|PRIVACY & IPV6", "month|rushhour|vpnleak" },
            new[] { "report", "Make a report", "SHARE A DIAGNOSIS", "ispreport|qr|friend" },   // More > Reports opens this
            new[] { "fun", "Make it fun", "JUST FOR FUN", "roast|therapy|letter" } };
        string tbQ = "";   // the question page open ("" = all questions)
        // each question's colour and icon (PLAN 11.3) - the same on its card, its tools' pages and their back buttons
        static readonly Dictionary<string, string[]> QLook = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "reach", new[] { "q.reach", "dest" } }, { "slow", new[] { "q.slow", "speed" } }, { "path", new[] { "q.break", "route" } },
            { "lan", new[] { "q.network", "devices" } }, { "join", new[] { "q.join", "join" } }, { "keys", new[] { "q.keys", "key" } },
            { "cert", new[] { "q.padlock", "lock" } }, { "hw", new[] { "q.hw", "cpu" } }, { "win", new[] { "q.windows", "windows" } }, { "lab", new[] { "q.traffic", "traffic" } },
            { "pc", new[] { "q.windows", "cpu" } }, { "calc", new[] { "calc", "calc" } }, { "game", new[] { "gamer", "game" } },
            { "time", new[] { "time", "time" } }, { "report", new[] { "reports", "report" } }, { "fun", new[] { "q.keys", "mood" } }
        };
        static string QAccent(string q) { string[] l; return q != null && QLook.TryGetValue(q, out l) ? l[0] : "toolbox"; }
        static string QIcon(string q) { string[] l; return q != null && QLook.TryGetValue(q, out l) ? l[1] : "tool"; }
        DB tbHead; PillButton tbViewQ, tbViewT, tbViewL; readonly List<Control> tbClassic = new List<Control>(); Panel tbLeft; TableLayoutPanel tbSplit;
        // both views now use the roomy layout (a tool across the whole width); Every tool only changes the home page (FullView.Index.cs)
        static bool ByQuestion { get { return true; } }

        static string QuestionOf(Tool t) { var q = Questions.FirstOrDefault(z => z[2].Split('|').Contains(t.Group)); return q != null ? q[0] : ""; }
        List<Tool> ToolsOf(string q)
        {
            var qd = Questions.First(z => z[0] == q); var groups = qd[2].Split('|'); var feat = qd[3].Split('|');
            // its groups' tools, plus a featured tool that lives in another group (Make a report shows the ISP report too)
            return Tools2().Where(t => (groups.Contains(t.Group) || feat.Contains(t.Id)) && (!SnmpView(t.Id) || t.Id == "snmp")).ToList();
        }
        // the two ways to see the Toolbox: by question (roomy) or the classic all-at-once list
        // the search box: roomy in question view, never wider than the page (narrow windows, 150-200 % scaling)
        int TbSearchW(bool q) { return Math.Max(Sc(160), Math.Min(toolboxPage.Width - Sc(48), q ? Math.Max(Sc(420), toolboxPage.Width - Sc(420)) : Sc(320))); }
        void ApplyTbView()
        {
            if (tbSplit == null) return;
            bool q = ByQuestion;
            toolboxPage.SuspendLayout();
            tbSplit.ColumnStyles[0].Width = q ? 0 : Sc(290); tbLeft.Visible = !q;
            foreach (var c in tbClassic) c.Visible = !q;
            tbViewQ.Selected = !EveryTool && !ByTopic; tbViewT.Selected = ByTopic; tbViewL.Selected = EveryTool;
            tbSearch.Width = TbSearchW(q);
            toolboxPage.ResumeLayout(true);
            // the bar with the search and the two views: laid out and painted again (it stayed blank when the Toolbox opened in Every tool)
            if (tbSearch.Parent != null) { tbSearch.Parent.PerformLayout(); tbSearch.Parent.Invalidate(true); }
            if (IsHandleCreated) BeginInvoke((Action)delegate { if (tbSearch != null && tbSearch.Parent != null && !tbSearch.IsDisposed) { foreach (Control c in tbSearch.Parent.Controls) c.Invalidate(true); tbSearch.Parent.Update(); } });
            if (q && tbHome) tbBar.Visible = false; if (tbAssist != null) tbAssist.Visible = false;
            tbHead.Visible = q && !tbHome;
            tbCanvas.Invalidate(); tbList.Invalidate();
        }
        void SetTbView(string v)
        {
            if (tbBusy) return;
            Cfg.TbView = v; Cfg.Save(); ApplyTbView();
            tbSearch.Box.Text = ""; ShowQuestions(EveryTool ? tbQ : "");
        }
        void ShowQuestions(string q)
        {
            if (tbBusy) { if (tbRun0 != null) tbRun0.Status = "still running - press Stop first"; tbCanvas.Invalidate(); return; }
            tbQ = q; tbHome = true; tbBar.Visible = false; if (tbAssist != null) tbAssist.Visible = false; tbHead.Visible = false; tbCanvas.AutoScrollPosition = Point.Empty;
            // Every tool: Back from a tool lands on the same spot of the same question
            if (EveryTool && idxBackQ != null && idxBackQ == q) { int by = idxBackY; idxBackQ = null; tbCanvas.Invalidate(); tbCanvas.Update(); tbCanvas.AutoScrollPosition = new Point(0, by); }
            tbCanvas.Invalidate();
        }
        // the header above the boxes: back to all tools, the question, and the tool's name - big, then what it answers
        // a tool's header (PLAN 11.6): a big back button to its question in that question's colour, the icon and name,
        // one line on what it answers, and on the right its points and a pin to your own tab
        void PaintTbHead(Graphics g)
        {
            Begin(tbHead); var cur = Tools2().FirstOrDefault(z => z.Id == tbSel); if (cur == null) return;
            float x = 20 * S, y = 12 * S, right = tbHead.ClientSize.Width - 20 * S;
            string q = QuestionOf(cur), acc = QAccent(q); var qd = Questions.FirstOrDefault(z => z[0] == q);
            var bk = Kit.Back(g, qd != null ? qd[1] : "All tools", x, y, acc, true);
            string qid = qd != null ? qd[0] : ""; Act(tbHead, bk, delegate { ShowQuestions(qid); });
            // pin, then points, from the right
            bool pinned = MyTabs().Any(t => t.Items.Contains("tool:" + cur.Id));
            string pt = L.T(pinned ? "Pinned" : "Pin"); var pz = g.MeasureString(pt, Draw.fBtn);
            var pr = new RectangleF(right - pz.Width - 40 * S, y + 2 * S, pz.Width + 40 * S, 32 * S);
            Theme.Fill(g, pr, 10 * S, pinned ? Kit.Soft("mine") : Theme.Soft);
            Kit.Icon(g, pinned ? "check" : "pin", new RectangleF(pr.X + 10 * S, pr.Y + 8 * S, 16 * S, 16 * S), pinned ? Kit.Accent("mine") : Theme.Sub);
            using (var b = new SolidBrush(pinned ? Kit.Ink("mine") : Theme.Ink)) g.DrawString(pt, Draw.fBtn, b, pr.X + 30 * S, pr.Y + (pr.Height - pz.Height) / 2);
            string tid = cur.Id; if (!pinned) Act(tbHead, pr, delegate { PinToMine("tool:" + tid); tbHead.Invalidate(); });
            float px = pr.X - 10 * S;
            if (Cfg.Points && Points.Value(cur.Id) > 0 && !Points.NoEarn.Contains(cur.Id))
            {
                bool got = Points.Me.Has(cur.Id);
                string pv = got ? L.T("Earned") : "+" + Points.Value(cur.Id).ToString(CultureInfo.InvariantCulture) + " " + L.T("points"); var vs = g.MeasureString(pv, Draw.fSmall);
                var vr = new RectangleF(px - vs.Width - (got ? 34 : 18) * S, y + 6 * S, vs.Width + (got ? 34 : 18) * S, 24 * S);
                Theme.Fill(g, vr, 12 * S, Kit.Soft(acc));
                if (got) Kit.Icon(g, "check", new RectangleF(vr.X + 9 * S, vr.Y + 5 * S, 14 * S, 14 * S), Kit.Accent(acc));
                using (var b = new SolidBrush(Kit.Ink(acc))) g.DrawString(pv, Draw.fSmall, b, vr.X + (got ? 27 : 9) * S, vr.Y + (vr.Height - vs.Height) / 2);
            }
            y = bk.Bottom + 12 * S;
            using (var cb = new SolidBrush(Kit.Soft(acc))) g.FillEllipse(cb, x, y, 34 * S, 34 * S);
            Kit.Icon(g, QIcon(q), new RectangleF(x + 8 * S, y + 8 * S, 18 * S, 18 * S), Kit.Accent(acc));
            using (var nowrap = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                string title = SnmpView(tbSel) ? L.T("SNMP - ask a switch or router") : L.T(cur.Title);
                var ts = g.MeasureString(title, Draw.fTitle);
                g.DrawString(title, Draw.fTitle, ink, new RectangleF(x + 46 * S, y + (34 * S - ts.Height) / 2, right - x - 46 * S, ts.Height + 2), nowrap);
                var te = Explain.Term("tb." + cur.Id);
                if (te != null && te.Tag != "") { float tx = x + 46 * S + Math.Min(ts.Width, right - x - 46 * S) + 14 * S; if (right - tx > 160 * S) g.DrawString(L.T(te.Tag), Draw.fBody, muted, new RectangleF(tx, y + 8 * S, right - tx, 22 * S), nowrap); }
            }
        }
        // ---------- painting the by-question home, a question's page, and search results
        // on a question page (Calculators, Reports...) the search looks inside it first; nothing there = every tool
        List<Tool> ScopedMatches()
        {
            var found = Matches(); if (tbQ == "" || !ByQuestion) return found;
            var mine = new HashSet<string>(ToolsOf(tbQ).Select(t => t.Id)); var inQ = found.Where(t => mine.Contains(t.Id)).ToList();
            return inQ.Count > 0 ? inQ : found;
        }
        void PaintQuestions(Graphics g, float x, float y, float w)
        {
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            using (var nowrap = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                y += 8 * S;
                // a tool as a roomy row: its name, and what it answers in plain words
                // a tool as a short row: its name (what it answers shows on hover - fewer words, PLAN 11.5), where it lives in a
                // search, and its points: "+2" not tried yet, a tick once earned
                Func<Tool, RectangleF, string, float> toolRow = (t, r, where) =>
                {
                    if (Kit.Off(g, r)) { string id0 = t.Id; Act(tbCanvas, r, delegate { tbSearch.Box.Text = ""; OpenTool(id0); }); Hint(tbCanvas, r, "tb." + id0); return r.Height; }   // scrolled away: clickable, not drawn
                    string q = QuestionOf(t), acc = QAccent(q);
                    Theme.Fill(g, r, 10 * S, Theme.Soft);
                    string title = t.Id == "snmp" ? L.T("SNMP - ask a switch or router") : L.T(t.Title);
                    float px = r.Right - 14 * S; bool pts = Cfg.Points && Points.Value(t.Id) > 0 && !Points.NoEarn.Contains(t.Id);
                    if (pts)
                    {
                        bool got = Points.Me.Has(t.Id);
                        if (got) { Kit.Icon(g, "check", new RectangleF(px - 16 * S, r.Y + (r.Height - 16 * S) / 2, 16 * S, 16 * S), Kit.Accent(acc)); px -= 26 * S; }
                        else
                        {
                            string pv = "+" + Points.Value(t.Id).ToString(CultureInfo.InvariantCulture); var ps = g.MeasureString(pv, Draw.fTiny);
                            var pr = new RectangleF(px - ps.Width - 10 * S, r.Y + (r.Height - 18 * S) / 2, ps.Width + 10 * S, 18 * S);
                            Theme.Fill(g, pr, 6 * S, Kit.Soft(acc)); using (var pb = new SolidBrush(Kit.Ink(acc))) g.DrawString(pv, Draw.fTiny, pb, pr.X + 5 * S, pr.Y + 2 * S);
                            px = pr.X - 10 * S;
                        }
                    }
                    if (tbLast.ContainsKey(t.Id)) { using (var db = new SolidBrush(Theme.Green)) g.FillEllipse(db, px - 6 * S, r.Y + (r.Height - 6 * S) / 2, 6 * S, 6 * S); px -= 14 * S; }   // run this session
                    float ty = r.Y + (r.Height - (where != "" ? 36 * S : 18 * S)) / 2;
                    g.DrawString(title, Draw.fBodyB, ink, new RectangleF(r.X + 14 * S, ty, px - r.X - 20 * S, 22 * S), nowrap);
                    if (where != "") g.DrawString(where, Draw.fSmall, muted, new RectangleF(r.X + 14 * S, ty + 20 * S, px - r.X - 20 * S, 18 * S), nowrap);
                    string id = t.Id; Act(tbCanvas, r, delegate { tbSearch.Box.Text = ""; OpenTool(id); }); Hint(tbCanvas, r, "tb." + id);
                    return r.Height;
                };
                int cols = w > 980 * S ? 3 : w > 600 * S ? 2 : 1; float gap = 16 * S;
                if (tbFilter != "")
                {
                    var found = ScopedMatches();
                    g.DrawString(found.Count == 0 ? L.T("Nothing matches - try another word") : found.Count + (found.Count == 1 ? L.T(" tool matches") : L.T(" tools match")) + (found.Count > 60 ? "  ·  " + L.T("the first") + " 60 " + L.T("shown") : "") + "  ·  " + L.T("Enter opens the first"), Draw.fHead, ink, x, y);   /* L-143 */ y += 36 * S;
                    if (found.Count == 0) { string dym = DidYouMean(tbFilter); if (dym != null) { string tx = L.T("Did you mean") + "  " + dym + " ?"; var ts = g.MeasureString(tx, Draw.fBody); g.DrawString(tx, Draw.fBody, lb, x, y); string qq = dym; Act(tbCanvas, new RectangleF(x, y, ts.Width, ts.Height), delegate { tbSearch.Box.Text = qq; tbSearch.Box.SelectionStart = qq.Length; }); y += 30 * S; } }
                    int c2 = Math.Min(cols, 2); float rw = (w - gap * (c2 - 1)) / c2; int i = 0;
                    foreach (var t in found.Take(60)) { var qn = Questions.FirstOrDefault(z => z[0] == QuestionOf(t)); toolRow(t, new RectangleF(x + (i % c2) * (rw + gap), y + (i / c2) * 66 * S, rw, 58 * S), qn != null ? L.T(qn[1]) : SectionName(SectionOf(t))); i++; }
                    Need(tbCanvas, y + ((i + c2 - 1) / c2) * 66 * S + 10 * S); return;
                }
                if (tbQ != "")
                {
                    var qd = Questions.First(z => z[0] == tbQ);
                    var bk = Kit.Back(g, "All tools", x, y, "toolbox", false); Act(tbCanvas, bk, delegate { ShowQuestions(""); }); y += 52 * S;
                    y = Kit.Header(g, x, y, w, QIcon(tbQ), qd[1], ToolsOf(tbQ).Count + " " + L.T("tools"), QAccent(tbQ)) - 40 * S;
                    y += 40 * S;
                    if (tbQ == "lab") { string note = L.T("Test traffic you build yourself - to one target, small and capped (10 packets a second, 1,000 at most), never a flood or a scan."); g.DrawString(note, Draw.fBody, sub, new RectangleF(x, y, w, 40 * S)); y += Draw.Measure(note, Draw.fBody, (int)w).Height + 12 * S; }
                    var list = ToolsOf(tbQ); int c2 = Math.Min(cols, 2); float rw = (w - gap * (c2 - 1)) / c2; int i = 0;
                    // a question that spans groups (the calculators) gets a small heading per group
                    var grps = list.Select(t => t.Group).Distinct().ToList();
                    foreach (var gname in grps)
                    {
                        if (grps.Count > 1) { using (var hb = new SolidBrush(Kit.Ink(QAccent(tbQ)))) g.DrawString(L.T(gname), Draw.fTiny, hb, x, y + 4 * S); y += 24 * S; }
                        i = 0;
                        foreach (var t in list.Where(z => z.Group == gname)) { toolRow(t, new RectangleF(x + (i % c2) * (rw + gap), y + (i / c2) * 52 * S, rw, 44 * S), ""); i++; }
                        y += ((i + c2 - 1) / c2) * 52 * S + (grps.Count > 1 ? 10 * S : 0);
                    }
                    Need(tbCanvas, y + 10 * S); return;
                }
                // all questions
                g.DrawString(L.T("Toolbox"), Draw.fTitle, ink, x, y); y += 38 * S;
                var sn = snap;
                if (sn != null && sn.Level >= 1 && !string.IsNullOrEmpty(sn.Headline))
                {
                    // "Right now": only when something is off - the headline and the question that fits it
                    string fit = sn.Path != null && sn.Path.Length > 1 && sn.Path[1] == 2 ? "join" : "slow"; var fq = Questions.First(z => z[0] == fit);
                    var rr = new RectangleF(x, y, w, 58 * S); Theme.Fill(g, rr, 12 * S, Theme.LvSoft(sn.Level));
                    using (var lb2 = new SolidBrush(Theme.LvText(sn.Level))) { g.DrawString(L.T("RIGHT NOW"), Draw.fTiny, lb2, x + 16 * S, y + 10 * S); g.DrawString(sn.Headline, Draw.fBodyB, lb2, new RectangleF(x + 16 * S, y + 27 * S, w - 230 * S, 22 * S), nowrap); }
                    string go = L.T(fq[1]) + "  ›"; var gz = g.MeasureString(go, Draw.fBtn);
                    var gr = new RectangleF(rr.Right - gz.Width - 32 * S, y + 13 * S, gz.Width + 20 * S, 32 * S);
                    Theme.Fill(g, gr, 10 * S, Kit.Accent(QAccent(fit))); using (var wb = new SolidBrush(Color.White)) g.DrawString(go, Draw.fBtn, wb, gr.X + 10 * S, gr.Y + (gr.Height - gz.Height) / 2);
                    Act(tbCanvas, gr, delegate { ShowQuestions(fit); });
                    y += 72 * S;
                }
                else
                {
                    string intro = L.T("Pick a question - the right tool comes up.");
                    g.DrawString(intro, Draw.fBody, sub, new RectangleF(x, y, w, 40 * S)); y += Draw.Measure(intro, Draw.fBody, (int)w).Height + 14 * S;
                }
                var recent = tbRecent.Select(id => Tools2().FirstOrDefault(z => z.Id == id)).Where(t => t != null).ToList();
                if (recent.Count > 0)
                {
                    g.DrawString(L.T("RECENT"), Draw.fTiny, muted, x, y + 9 * S); float rx = x + g.MeasureString(L.T("RECENT"), Draw.fTiny).Width + 14 * S;
                    foreach (var t in recent)
                    {
                        string tt = L.T(t.Title); var ts = g.MeasureString(tt, Draw.fBody); var pr = new RectangleF(rx, y, ts.Width + 28 * S, 32 * S); if (pr.Right > x + w) break;
                        Theme.Fill(g, pr, 16 * S, Theme.Soft); g.DrawString(tt, Draw.fBody, ink, pr.X + 14 * S, pr.Y + (32 * S - ts.Height) / 2);
                        string id = t.Id; Act(tbCanvas, pr, delegate { OpenTool(id); }); rx = pr.Right + 8 * S;
                    }
                    y += 46 * S;
                }
                float cw = (w - gap * (cols - 1)) / cols, rowY = y, rowH = 0; int col = 0;
                foreach (var qd in Questions)
                {
                    var all = ToolsOf(qd[0]); var feat = qd[3].Split('|').Select(id => all.FirstOrDefault(t => t.Id == id)).Where(t => t != null).ToList();
                    float ch = 62 * S + feat.Count * 50 * S + 34 * S; var r = new RectangleF(x + col * (cw + gap), rowY, cw, ch);
                    string acc = QAccent(qd[0]);
                    Kit.Card(g, r, acc, false);
                    bool big = BigIcon(g, "q:" + qd[0], new RectangleF(r.X + 12 * S, r.Y + 10 * S, 44 * S, 44 * S), Kit.Accent(acc));   // 1.1.6: big icons (FullView.Gateway.cs)
                    if (!big) { using (var rb = new SolidBrush(Kit.Soft(acc))) g.FillEllipse(rb, r.X + 14 * S, r.Y + 16 * S, 32 * S, 32 * S); Kit.Icon(g, QIcon(qd[0]), new RectangleF(r.X + 22 * S, r.Y + 24 * S, 16 * S, 16 * S), Kit.Accent(acc)); }
                    g.DrawString(L.T(qd[1]), Draw.fHead, ink, new RectangleF(r.X + (big ? 66 : 54) * S, r.Y + 21 * S, r.Width - (big ? 142 : 130) * S, 26 * S), nowrap);
                    string cnt = all.Count.ToString(CultureInfo.CurrentCulture); var cs = g.MeasureString(cnt, Draw.fTiny);
                    var cr = new RectangleF(r.Right - 16 * S - cs.Width - 12 * S, r.Y + 22 * S, cs.Width + 12 * S, 20 * S);
                    Theme.Fill(g, cr, 10 * S, Kit.Soft(acc)); using (var cb = new SolidBrush(Kit.Ink(acc))) g.DrawString(cnt, Draw.fTiny, cb, cr.X + 6 * S, cr.Y + 3 * S);
                    float iy = r.Y + 60 * S;
                    foreach (var t in feat) { toolRow(t, new RectangleF(r.X + 10 * S, iy, r.Width - 20 * S, 42 * S), ""); iy += 50 * S; }
                    string more = L.T("All") + " " + all.Count + "  ›"; var mz = g.MeasureString(more, Draw.fBodyB); using (var ab = new SolidBrush(Kit.Ink(acc))) g.DrawString(more, Draw.fBodyB, ab, r.X + 16 * S, iy + 4 * S);
                    string qid = qd[0]; Act(tbCanvas, new RectangleF(r.X + 16 * S, iy + 4 * S, mz.Width, mz.Height), delegate { ShowQuestions(qid); });
                    rowH = Math.Max(rowH, ch);
                    if (++col == cols) { col = 0; rowY += rowH + gap; rowH = 0; }
                }
                Need(tbCanvas, rowY + rowH + 20 * S);
            }
        }
    }
}
