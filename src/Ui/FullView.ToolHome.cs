// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Toolbox, by question (the default view since 1.1.5)
    // One big search, your recent tools, and a card per question you'd ask - "Can I reach it?", "Why is it slow?" - with its
    // three most useful tools in plain words. A tool opens across the whole width. "All tools (list)" brings back the classic
    // view (sections, quick row, the side list) for people who like everything at once; the choice is remembered.
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
            new[] { "win", "Windows and certificates", "WINDOWS|CERTIFICATES & FILES", "winev|certs|fw" },
            new[] { "lab", "Build test traffic", "BUILD A PACKET|TEST A COLLECTOR|LOAD & QUEUES", "icmpb|udpsend|iperf" },
            new[] { "pc", "Is it this PC?", "THE PC", "usb|winupdate|drivers" },   // the PC's own checks: on no other card
            new[] { "calc", "Work it out", "CALCULATORS", "subnet|stack|bandwidth" },   // pure arithmetic, nothing sent
            new[] { "game", "Is it my game?", "GAMING", "prematch|gpn|tonight" },
            new[] { "time", "How has my network been?", "OVER TIME|SHARE A DIAGNOSIS|PRIVACY & IPV6", "month|rushhour|vpnleak" } };
        string tbQ = "";   // the question page open ("" = all questions)
        DB tbHead; PillButton tbViewQ, tbViewL; readonly List<Control> tbClassic = new List<Control>(); Panel tbLeft; TableLayoutPanel tbSplit;
        static bool ByQuestion { get { return Cfg.TbView != "list"; } }

        static string QuestionOf(Tool t) { var q = Questions.FirstOrDefault(z => z[2].Split('|').Contains(t.Group)); return q != null ? q[0] : ""; }
        List<Tool> ToolsOf(string q)
        {
            var groups = Questions.First(z => z[0] == q)[2].Split('|');
            return Tools2().Where(t => groups.Contains(t.Group) && (!SnmpView(t.Id) || t.Id == "snmp")).ToList();
        }
        // the two ways to see the Toolbox: by question (roomy) or the classic all-at-once list
        void ApplyTbView()
        {
            if (tbSplit == null) return;
            bool q = ByQuestion;
            toolboxPage.SuspendLayout();
            tbSplit.ColumnStyles[0].Width = q ? 0 : Sc(290); tbLeft.Visible = !q;
            foreach (var c in tbClassic) c.Visible = !q;
            tbViewQ.Selected = q; tbViewL.Selected = !q;
            tbSearch.Width = q ? Math.Max(Sc(420), toolboxPage.Width - Sc(420)) : Sc(320);
            toolboxPage.ResumeLayout(true);
            if (q && tbHome) tbBar.Visible = false;
            tbHead.Visible = q && !tbHome;
            tbCanvas.Invalidate(); tbList.Invalidate();
        }
        void SetTbView(string v)
        {
            if (tbBusy) return;
            Cfg.TbView = v; Cfg.Save(); ApplyTbView();
            if (ByQuestion) ShowQuestions(""); else ShowSection(tbSection);
        }
        void ShowQuestions(string q)
        {
            if (tbBusy) { if (tbRun0 != null) tbRun0.Status = "still running - press Stop first"; tbCanvas.Invalidate(); return; }
            tbQ = q; tbHome = true; tbBar.Visible = false; tbHead.Visible = false; tbCanvas.AutoScrollPosition = Point.Empty; tbCanvas.Invalidate();
        }
        // the header above the boxes: back to all tools, the question, and the tool's name - big, then what it answers
        void PaintTbHead(Graphics g)
        {
            Begin(tbHead); var cur = Tools2().FirstOrDefault(z => z.Id == tbSel); if (cur == null) return;
            float x = 20 * S, y = 12 * S, w = tbHead.ClientSize.Width - 40 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            using (var nowrap = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                var qd = Questions.FirstOrDefault(z => z[0] == QuestionOf(cur));
                string back = "‹  " + L.T("All tools"); var bs = g.MeasureString(back, Draw.fBody);
                g.DrawString(back, Draw.fBody, lb, x, y); Act(tbHead, new RectangleF(x, y, bs.Width, bs.Height), delegate { ShowQuestions(""); });
                if (qd != null) { string qt = "·  " + L.T(qd[1]); var qs = g.MeasureString(qt, Draw.fBody); g.DrawString(qt, Draw.fBody, muted, x + bs.Width + 8 * S, y); string qid = qd[0]; Act(tbHead, new RectangleF(x + bs.Width + 8 * S, y, qs.Width, qs.Height), delegate { ShowQuestions(qid); }); }
                y += 30 * S;
                string title = SnmpView(tbSel) ? L.T("SNMP - ask a switch or router") : L.T(cur.Title);
                g.DrawString(title, Draw.fTitle, ink, new RectangleF(x, y, w, 34 * S), nowrap); y += 36 * S;
                var te = Explain.Term("tb." + cur.Id); if (te != null && te.Tag != "") g.DrawString(L.T(te.Tag), Draw.fBody, muted, new RectangleF(x, y, w, 22 * S), nowrap);
            }
        }
        // ---------- painting the by-question home, a question's page, and search results
        void PaintQuestions(Graphics g, float x, float y, float w)
        {
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            using (var nowrap = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                y += 8 * S;
                // a tool as a roomy row: its name, and what it answers in plain words
                Func<Tool, RectangleF, string, float> toolRow = (t, r, where) =>
                {
                    var te = Explain.Term("tb." + t.Id); string tag = t.Id == "snmp" ? L.T("Ports, traffic, CPU, neighbours, sensors, any OID.") : te != null ? L.T(te.Tag) : "";
                    Theme.Fill(g, r, 10 * S, Theme.Soft);
                    string title = t.Id == "snmp" ? L.T("SNMP - ask a switch or router") : L.T(t.Title);
                    g.DrawString(title, Draw.fBodyB, ink, new RectangleF(r.X + 14 * S, r.Y + 9 * S, r.Width - 28 * S, 22 * S), nowrap);
                    g.DrawString((where != "" ? where + "  ·  " : "") + tag, Draw.fSmall, muted, new RectangleF(r.X + 14 * S, r.Y + 32 * S, r.Width - 28 * S, 18 * S), nowrap);
                    if (tbLast.ContainsKey(t.Id)) using (var db = new SolidBrush(Theme.Green)) g.FillEllipse(db, r.Right - 16 * S, r.Y + 12 * S, 6 * S, 6 * S);   // run this session
                    string id = t.Id; Act(tbCanvas, r, delegate { tbSearch.Box.Text = ""; OpenTool(id); }); Hint(tbCanvas, r, "tb." + id);
                    return r.Height;
                };
                int cols = w > 980 * S ? 3 : w > 600 * S ? 2 : 1; float gap = 16 * S;
                if (tbFilter != "")
                {
                    var found = Matches();
                    g.DrawString(found.Count == 0 ? L.T("Nothing matches - try another word") : found.Count + (found.Count == 1 ? L.T(" tool matches") : L.T(" tools match")) + "  ·  " + L.T("Enter opens the first"), Draw.fHead, ink, x, y); y += 36 * S;
                    if (found.Count == 0) { string dym = DidYouMean(tbFilter); if (dym != null) { string tx = L.T("Did you mean") + "  " + dym + " ?"; var ts = g.MeasureString(tx, Draw.fBody); g.DrawString(tx, Draw.fBody, lb, x, y); string qq = dym; Act(tbCanvas, new RectangleF(x, y, ts.Width, ts.Height), delegate { tbSearch.Box.Text = qq; tbSearch.Box.SelectionStart = qq.Length; }); y += 30 * S; } }
                    int c2 = Math.Min(cols, 2); float rw = (w - gap * (c2 - 1)) / c2; int i = 0;
                    foreach (var t in found.Take(60)) { var qn = Questions.FirstOrDefault(z => z[0] == QuestionOf(t)); toolRow(t, new RectangleF(x + (i % c2) * (rw + gap), y + (i / c2) * 66 * S, rw, 58 * S), qn != null ? L.T(qn[1]) : SectionName(SectionOf(t))); i++; }
                    Need(tbCanvas, y + ((i + c2 - 1) / c2) * 66 * S + 10 * S); return;
                }
                if (tbQ != "")
                {
                    var qd = Questions.First(z => z[0] == tbQ);
                    string back = "‹  " + L.T("All tools"); var bs = g.MeasureString(back, Draw.fBody); g.DrawString(back, Draw.fBody, lb, x, y); Act(tbCanvas, new RectangleF(x, y, bs.Width, bs.Height), delegate { ShowQuestions(""); }); y += 32 * S;
                    g.DrawString(L.T(qd[1]), Draw.fTitle, ink, x, y); y += 40 * S;
                    if (tbQ == "lab") { string note = L.T("Test traffic you build yourself - to one target, small and capped (10 packets a second, 1,000 at most), never a flood or a scan."); g.DrawString(note, Draw.fBody, sub, new RectangleF(x, y, w, 40 * S)); y += Draw.Measure(note, Draw.fBody, (int)w).Height + 12 * S; }
                    var list = ToolsOf(tbQ); int c2 = Math.Min(cols, 2); float rw = (w - gap * (c2 - 1)) / c2; int i = 0;
                    foreach (var t in list) { toolRow(t, new RectangleF(x + (i % c2) * (rw + gap), y + (i / c2) * 66 * S, rw, 58 * S), ""); i++; }
                    Need(tbCanvas, y + ((i + c2 - 1) / c2) * 66 * S + 10 * S); return;
                }
                // all questions
                g.DrawString(L.T("Toolbox"), Draw.fTitle, ink, x, y); y += 38 * S;
                string intro = L.T("Ask the question - the right tool comes up. Type in the box above, or pick a question.");
                g.DrawString(intro, Draw.fBody, sub, new RectangleF(x, y, w, 40 * S)); y += Draw.Measure(intro, Draw.fBody, (int)w).Height + 14 * S;
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
                    float ch = 50 * S + feat.Count * 66 * S + 34 * S; var r = new RectangleF(x + col * (cw + gap), rowY, cw, ch);
                    Theme.Fill(g, r, 14 * S, Theme.Card); Theme.Stroke(g, r, 14 * S, Theme.Line, 1);
                    g.DrawString(L.T(qd[1]), Draw.fHead, ink, new RectangleF(r.X + 16 * S, r.Y + 14 * S, r.Width - 110 * S, 26 * S), nowrap);
                    string cnt = all.Count + " " + L.T("tools"); var cs = g.MeasureString(cnt, Draw.fSmall); g.DrawString(cnt, Draw.fSmall, muted, r.Right - 16 * S - cs.Width, r.Y + 19 * S);
                    float iy = r.Y + 50 * S;
                    foreach (var t in feat) { toolRow(t, new RectangleF(r.X + 10 * S, iy, r.Width - 20 * S, 58 * S), ""); iy += 66 * S; }
                    string more = L.T("All") + " " + all.Count + "  ›"; var mz = g.MeasureString(more, Draw.fBodyB); g.DrawString(more, Draw.fBodyB, lb, r.X + 16 * S, iy + 4 * S);
                    string qid = qd[0]; Act(tbCanvas, new RectangleF(r.X + 16 * S, iy + 4 * S, mz.Width, mz.Height), delegate { ShowQuestions(qid); });
                    rowH = Math.Max(rowH, ch);
                    if (++col == cols) { col = 0; rowY += rowH + gap; rowH = 0; }
                }
                Need(tbCanvas, rowY + rowH + 20 * S);
            }
        }
    }
}
