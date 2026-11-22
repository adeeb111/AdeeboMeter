// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Toolbox › Every tool (Adeeb + GPT, 16 Nov - replaces "All tools (list)")
    // The same questions as By question, as an index: the questions down the left (a chip row on a narrow window), every tool
    // ONCE on the right - its name, what it answers (two lines at most, then "…" - bug-hunt L-150), when it last ran or the points it still gives, a pin, Open.
    // A family is one row (SNMP: "15 views ▾"). Pinned first: ready-made big coloured buttons, then your own pins.
    //
    //   [ search ]                                       By question | Every tool
    //   ★ Pinned            ┃ Pinned / What's on my network?            N tools shown
    //   Can I reach it?  26 ┃  group ─────────────────────────────────────────────
    //   ...                 ┃  Name · what it answers            ran 7:41 · ☆ · Open
    sealed partial class FullView
    {
        static bool EveryTool { get { return Cfg.TbView == "list"; } }
        // ready-made pins: the tools people reach for first, as big coloured buttons (Adeeb: "easily identifiable")
        static readonly string[] StarterPins = { "speedtest", "port", "mtr", "dig", "inventory", "snmp", "http", "pcwhy" };
        static List<string> MyPins() { return (Cfg.TbPins ?? "").Split(',').Where(s => s != "").ToList(); }
        static void SetPin(string id, bool on)
        {
            var l = MyPins(); l.Remove(id); if (on) l.Add(id);
            Cfg.TbPins = string.Join(",", l.Distinct().Take(12)); Cfg.Save();
        }
        string idxBackQ = null; int idxBackY;   // Back from a tool returns to the same spot in the index
        void OpenFromIndex(string id) { idxBackQ = tbQ; idxBackY = -tbCanvas.AutoScrollPosition.Y; tbSearch.Box.Text = ""; OpenTool(id); }

        // "SWITCHES & ROUTERS (SNMP)" -> "Switches & routers (SNMP)"
        static readonly string[] KeepCaps = { "SNMP", "DNS", "TLS", "HTTPS", "HTTP", "PC", "VPN", "TDM", "ISP", "IP", "LAN", "Wi-Fi", "IPv6", "TCP", "UDP" };
        static string GroupName(string g)
        {
            if (string.IsNullOrEmpty(g)) return "";
            string s = g.ToLowerInvariant(); s = char.ToUpperInvariant(s[0]) + s.Substring(1);
            foreach (var k in KeepCaps) s = Regex.Replace(s, @"\b" + Regex.Escape(k.ToLowerInvariant()) + @"\b", k);
            s = Regex.Replace(s, @"\bit maths\b", "IT maths");   // bug-hunt L-141: "IT" only where it means IT - "Is it reachable?" stays a word
            return s;
        }
        static string Answers(Tool t) { var te = Explain.Term("tb." + t.Id); return te != null && !string.IsNullOrEmpty(te.Tag) ? te.Tag : ""; }
        static string ToolName(Tool t) { return t.Id == "snmp" ? L.T("SNMP - switches & routers") : L.T(t.Title); }

        void PaintIndex(Graphics g, float x, float y, float w)
        {
            bool rail = w > 860 * S; float rw = rail ? 250 * S : 0, cx = x + (rail ? rw + 24 * S : 0), cw = w - (rail ? rw + 24 * S : 0);
            float top = y + 6 * S; railEnd = 0;
            // ---- the questions: a rail, or a chip row on a narrow window
            if (!idxSeen) { idxSeen = true; if (tbQ == "") tbQ = "*"; }   // the first look is every tool (22 Nov)
            int every = Tools2().Count(t => !SnmpView(t.Id) || t.Id == "snmp");
            var items = new List<string[]> { new[] { "*", L.T("All tools"), every.ToString(CultureInfo.CurrentCulture) }, new[] { "", "★ " + L.T("Pinned"), StarterPins.Union(MyPins()).Count().ToString(CultureInfo.CurrentCulture) } };   // bug-hunt L-152: a starter you also pinned counts once
            foreach (var qd in Questions) items.Add(new[] { qd[0], L.T(qd[1]), ToolsOf(qd[0]).Count(t => !SnmpView(t.Id) || t.Id == "snmp").ToString(CultureInfo.CurrentCulture) });
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                if (rail)
                {
                    float ry = top;
                    for (int i = 0; i < items.Count; i++)
                    {
                        var it = items[i]; bool on = tbFilter == "" && tbQ == it[0];
                        if (i == 2) { g.DrawString(L.T("BY QUESTION"), Draw.fTiny, muted, x + 12 * S, ry + 8 * S); ry += 28 * S; }
                        var rr = new RectangleF(x, ry, rw, 38 * S);
                        if (on) { Theme.Fill(g, rr, 10 * S, Kit.Soft(it[0] == "" ? "mine" : QAccent(it[0]))); using (var ab = new SolidBrush(Kit.Accent(it[0] == "" ? "mine" : QAccent(it[0])))) g.FillRectangle(ab, rr.X, rr.Y + 6 * S, 3 * S, rr.Height - 12 * S); }
                        using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                            g.DrawString(it[1], on ? Draw.fBodyB : Draw.fBody, on ? ink : sub, new RectangleF(rr.X + 14 * S, rr.Y + 9 * S, rr.Width - 60 * S, 22 * S), nf);
                        var cs = g.MeasureString(it[2], Draw.fSmall); g.DrawString(it[2], Draw.fSmall, muted, rr.Right - cs.Width - 12 * S, rr.Y + 10 * S);
                        string q = it[0]; Act(tbCanvas, rr, delegate { tbSearch.Box.Text = ""; tbQ = q; tbCanvas.AutoScrollPosition = Point.Empty; tbCanvas.Invalidate(); });
                        ry += 40 * S;
                    }
                    using (var p = new Pen(Theme.Line)) g.DrawLine(p, x + rw + 12 * S, top, x + rw + 12 * S, Math.Max(ry, top + 200 * S));
                    railEnd = ry + 20 * S;   // one height for the whole page (rail and list): two Need calls a paint flipped the scrollbar on every repaint - the blinking
                }
                else
                {
                    float chx = cx, chy = top;
                    foreach (var it in items)
                    {
                        bool on = tbFilter == "" && tbQ == it[0];
                        if (chx + Kit.ChipWidth(g, it[1], null) > cx + cw && chx > cx) { chx = cx; chy += 36 * S; }   // wrap first, then draw (a chip was drawn twice: cut at the edge, then again below)
                        var r = Kit.Chip(g, it[1], chx, chy, it[0] == "" ? "mine" : QAccent(it[0]), on, null);
                        string q = it[0]; Act(tbCanvas, r, delegate { tbSearch.Box.Text = ""; tbQ = q; tbCanvas.AutoScrollPosition = Point.Empty; tbCanvas.Invalidate(); });
                        chx = r.Right + 8 * S;
                    }
                    top = chy + 46 * S;
                }

                // ---- the page: a search, Pinned, or one question
                float py = top;
                List<Tool> rows; string title, count;
                if (tbFilter != "")
                {
                    var allHits = Matches().Where(t => !SnmpView(t.Id) || t.Id == "snmp" || tbFilter.IndexOf("snmp", StringComparison.OrdinalIgnoreCase) >= 0).ToList(); rows = allHits.Take(80).ToList();
                    // bug-hunt L-142: the real count, and "the first 80 shown" when there are more
                    title = rows.Count == 0 ? L.T("Nothing matches") + " “" + tbFilter + "”" : allHits.Count + " " + L.T(allHits.Count == 1 ? "tool matches" : "tools match") + " “" + tbFilter + "”" + (allHits.Count > rows.Count ? " · " + L.T("the first") + " " + rows.Count + " " + L.T("shown") : "");
                    count = rows.Count == 0 ? L.T("try a shorter word, like dns or wifi") : L.T("Enter opens the first");
                }
                else if (tbQ == "*")
                {
                    rows = Tools2().Where(t => !SnmpView(t.Id) || t.Id == "snmp").ToList();
                    title = L.T("All tools"); count = rows.Count + " " + L.T("tools") + " · " + L.T("hover one for what it does");
                }
                else if (tbQ == "" || Questions.All(z => z[0] != tbQ))
                {
                    tbQ = ""; py = PaintPinned(g, cx, py, cw); Need(tbCanvas, Math.Max(railEnd, py + 30 * S)); return;
                }
                else
                {
                    var qd = Questions.First(z => z[0] == tbQ); rows = ToolsOf(tbQ).Where(t => !SnmpView(t.Id) || t.Id == "snmp").ToList();
                    title = L.T(qd[1]); count = rows.Count + " " + L.T(rows.Count == 1 ? "tool shown" : "tools shown");
                }
                var tsz = g.MeasureString(title, Draw.fTitle); g.DrawString(title, Draw.fTitle, ink, new RectangleF(cx, py, cw - 200 * S, tsz.Height + 4));
                var csz = g.MeasureString(count, Draw.fBody); g.DrawString(count, Draw.fBody, sub, cx + cw - csz.Width, py + 8 * S);
                py += tsz.Height + 12 * S;
                if (tbFilter == "") py = PinRow(g, cx, py, cw);
                // grouped: by the tool's group inside a question (by question in a search)
                var groups = tbFilter != "" || tbQ == "*" ? rows.GroupBy(t => { var qn = Questions.FirstOrDefault(z => z[0] == QuestionOf(t)); return qn != null ? L.T(qn[1]) : GroupName(t.Group); }).ToList()
                                            : rows.GroupBy(t => GroupName(t.Group)).ToList();
                foreach (var grp in groups)
                {
                    py += 14 * S;
                    using (var gb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(grp.Key, Draw.fBodyB, gb, cx, py);
                    string gc = grp.Count().ToString(CultureInfo.CurrentCulture); var gs = g.MeasureString(gc, Draw.fSmall); g.DrawString(gc, Draw.fSmall, muted, cx + cw - gs.Width, py + 2 * S);
                    py += 26 * S; using (var p = new Pen(Theme.Line)) g.DrawLine(p, cx, py, cx + cw, py);
                    foreach (var t in grp) py = IndexRow(g, t, cx, py, cw);
                }
                Need(tbCanvas, Math.Max(railEnd, py + 30 * S));
            }
        }
        float railEnd; bool idxSeen;

        // one tool: name + what it answers (two lines before a cut), its state, a pin, Open; a family shows its views
        float IndexRow(Graphics g, Tool t, float x, float y, float w)
        {
            string desc = Answers(t); float right = 200 * S, tw = w - right - 12 * S;
            float dh = desc == "" ? 0 : Math.Min(2 * Draw.fSmall.GetHeight(g) + 1, Draw.Measure(desc, Draw.fSmall, (int)tw).Height);
            float h = 16 * S + 22 * S + dh + 12 * S; var r = new RectangleF(x, y, w, h);
            if (Kit.Off(g, r)) { string id0 = t.Id; Act(tbCanvas, new RectangleF(x, y, w - right, h), delegate { OpenFromIndex(id0); }); return y + h; }
            string acc = QAccent(QuestionOf(t));
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(ToolName(t), Draw.fBodyB, ink, new RectangleF(x + 4 * S, y + 14 * S, tw, 22 * S), nf);
                if (desc != "") using (var two = new StringFormat { FormatFlags = StringFormatFlags.LineLimit, Trimming = StringTrimming.EllipsisWord })
                    g.DrawString(desc, Draw.fSmall, sub, new RectangleF(x + 4 * S, y + 38 * S, tw, dh), two);
                // right side, from the edge in: Open · pin · state (or the family's views)
                float bx = x + w, cy = y + (h - 36 * S) / 2;
                string ot = L.T("Open"); var os = g.MeasureString(ot, Draw.fBodyB); var orr = new RectangleF(bx - os.Width - 28 * S, cy, os.Width + 28 * S, 36 * S);
                Theme.Fill(g, orr, 10 * S, Kit.Soft(acc)); Theme.Stroke(g, orr, 10 * S, Kit.Accent(acc), 1);
                using (var ob = new SolidBrush(Kit.Ink(acc))) g.DrawString(ot, Draw.fBodyB, ob, orr.X + 14 * S, orr.Y + (36 * S - os.Height) / 2);
                string id = t.Id; Act(tbCanvas, orr, delegate { OpenFromIndex(id); });
                bx = orr.X - 10 * S;
                bool pinned = MyPins().Contains(t.Id) || StarterPins.Contains(t.Id);
                var pr = new RectangleF(bx - 30 * S, cy + 3 * S, 30 * S, 30 * S);
                using (var pb = new SolidBrush(pinned ? Kit.Accent("mine") : Theme.Muted)) { string st = pinned ? "★" : "☆"; var ss = g.MeasureString(st, Draw.fHead); g.DrawString(st, Draw.fHead, pb, pr.X + (pr.Width - ss.Width) / 2, pr.Y + (pr.Height - ss.Height) / 2); }
                if (!StarterPins.Contains(t.Id)) Act(tbCanvas, pr, delegate { SetPin(id, !MyPins().Contains(id)); tbCanvas.Invalidate(); });
                bx = pr.X - 10 * S;
                if (t.Id == "snmp")
                {
                    var views = Tools2().Where(z => SnmpView(z.Id) && z.Id != "snmp").ToList();
                    string vt = views.Count + " " + L.T("views"); var vs = g.MeasureString(vt, Draw.fBody); var vr = new RectangleF(bx - vs.Width - 40 * S, cy, vs.Width + 40 * S, 36 * S);
                    Theme.Stroke(g, vr, 10 * S, Theme.Line, 1); g.DrawString(vt, Draw.fBody, ink, vr.X + 12 * S, vr.Y + (36 * S - vs.Height) / 2);
                    float ax = vr.Right - 20 * S, ay = vr.Y + 16 * S; g.FillPolygon(ink, new[] { new PointF(ax - 5 * S, ay), new PointF(ax + 5 * S, ay), new PointF(ax, ay + 6 * S) });
                    Act(tbCanvas, vr, delegate { ViewsMenu(views, vr); });
                }
                else
                {
                    PrevRun last; string state; Color sc;
                    if (tbLast.TryGetValue(t.Id, out last)) { state = L.T("ran") + " " + last.At.ToString("t", CultureInfo.CurrentCulture); sc = Theme.Green; }
                    else if (Cfg.Points && Points.Value(t.Id) > 0 && !Points.NoEarn.Contains(t.Id)) { bool got = Points.Me.Has(t.Id); state = got ? "✓" : "+" + Points.Value(t.Id).ToString(CultureInfo.InvariantCulture); sc = got ? Theme.Muted : Theme.Amber; }
                    else { state = ""; sc = Theme.Muted; }
                    if (state != "") using (var stb = new SolidBrush(sc)) { var ss = g.MeasureString(state, Draw.fSmall); g.DrawString(state, Draw.fSmall, stb, bx - ss.Width, y + (h - ss.Height) / 2); }
                }
                Act(tbCanvas, new RectangleF(x, y, w - right, h), delegate { OpenFromIndex(id); });
                Hint(tbCanvas, new RectangleF(x, y, w - right, h), "tb." + t.Id);
            }
            using (var p = new Pen(Theme.Line2)) g.DrawLine(p, x, y + h, x + w, y + h);
            return y + h;
        }
        void ViewsMenu(List<Tool> views, RectangleF at)
        {
            var m = new ContextMenuStrip { ShowImageMargin = false, Font = Draw.fBody };
            foreach (var v in views) { string id = v.Id; m.Items.Add(L.T(v.Title), null, delegate { OpenFromIndex(id); }); }
            var sp = tbCanvas.AutoScrollPosition; m.Show(tbCanvas, new Point((int)at.X + sp.X, (int)at.Bottom + sp.Y));
        }
        // the pins as a row of small chips above a question's tools (GPT's "Pinned tools")
        void More(Graphics g, int n, float px, float y, float right)
        {
            if (n <= 0) return; string tx = "+" + n.ToString(CultureInfo.CurrentCulture); var sz = g.MeasureString(tx, Draw.fBodyB);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(tx, Draw.fBodyB, mb, Math.Min(px, right - sz.Width), y + 8 * S);
        }
        float PinRow(Graphics g, float x, float y, float w)
        {
            var pins = MyPins().Select(id => Tools2().FirstOrDefault(t => t.Id == id)).Where(t => t != null).ToList();
            if (pins.Count == 0) return y;
            using (var sub = new SolidBrush(Theme.Sub)) g.DrawString(L.T("Pinned"), Draw.fBody, sub, x, y + 6 * S);
            float px = x + g.MeasureString(L.T("Pinned"), Draw.fBody).Width + 10 * S;
            int shown = 0;
            foreach (var t in pins)
            {
                if (px + 60 * S > x + w) break;
                if (px + Kit.ChipWidth(g, ToolName(t), null) > x + w) break; var r = Kit.Chip(g, ToolName(t), px, y, QAccent(QuestionOf(t)), false, null);
                string id = t.Id; Act(tbCanvas, r, delegate { OpenFromIndex(id); }); px = r.Right + 8 * S; shown++;
            }
            More(g, pins.Count - shown, px, y, x + w);   // bug-hunt L-151: what didn't fit is counted, not dropped
            return y + 46 * S;
        }
        // Pinned: the ready-made big coloured buttons, then yours (remove with ✕), then how to add more
        float PaintPinned(Graphics g, float x, float y, float w)
        {
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("Pinned"), Draw.fTitle, ink, x, y); y += 40 * S;
                Func<string, List<Tool>, bool, float> grid = (head, list, mine) =>
                {
                    if (list.Count == 0) return y;
                    g.DrawString(head, Draw.fTiny, muted, x, y); y += 22 * S;
                    int cols = w > 1100 * S ? 5 : w > 820 * S ? 4 : w > 560 * S ? 3 : 2; float gap = 10 * S, bw = (w - gap * (cols - 1)) / cols, bh = 62 * S;   // Adeeb: a little smaller
                    for (int i = 0; i < list.Count; i++)
                    {
                        var t = list[i]; string q = QuestionOf(t), acc = QAccent(q);
                        var r = new RectangleF(x + (i % cols) * (bw + gap), y + (i / cols) * (bh + gap), bw, bh);
                        Theme.Fill(g, r, 12 * S, Kit.Soft(acc)); Theme.Stroke(g, r, 12 * S, Kit.Accent(acc), 1.5f);
                        using (var cb = new SolidBrush(Kit.Accent(acc))) g.FillEllipse(cb, r.X + 12 * S, r.Y + 15 * S, 28 * S, 28 * S);
                        Kit.Icon(g, QIcon(q), new RectangleF(r.X + 19 * S, r.Y + 22 * S, 14 * S, 14 * S), Color.White);
                        using (var tb = new SolidBrush(Kit.Ink(acc))) using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                        {
                            g.DrawString(ToolName(t), Draw.fBodyB, tb, new RectangleF(r.X + 50 * S, r.Y + 11 * S, r.Width - (mine ? 76 : 60) * S, 22 * S), nf);
                            string d = Answers(t);
                            if (d != "") g.DrawString(d, Draw.fSmall, sub, new RectangleF(r.X + 50 * S, r.Y + 33 * S, r.Width - 60 * S, 18 * S), nf);
                        }
                        string id = t.Id; Act(tbCanvas, r, delegate { OpenFromIndex(id); }); Hint(tbCanvas, r, "tb." + id);
                        if (mine)
                        {
                            var xr = new RectangleF(r.Right - 26 * S, r.Y + 6 * S, 20 * S, 20 * S);
                            using (var xp = new Pen(Theme.Muted, 1.8f * S)) { g.DrawLine(xp, xr.X + 6 * S, xr.Y + 6 * S, xr.Right - 6 * S, xr.Bottom - 6 * S); g.DrawLine(xp, xr.Right - 6 * S, xr.Y + 6 * S, xr.X + 6 * S, xr.Bottom - 6 * S); }
                            Act(tbCanvas, xr, delegate { SetPin(id, false); tbCanvas.Invalidate(); });
                        }
                    }
                    y += ((list.Count + cols - 1) / cols) * (bh + gap) + 14 * S;
                    return y;
                };
                var all = Tools2();
                y = grid(L.T("READY-MADE"), StarterPins.Select(id => all.FirstOrDefault(t => t.Id == id)).Where(t => t != null).ToList(), false);
                y = grid(L.T("YOURS"), MyPins().Select(id => all.FirstOrDefault(t => t.Id == id)).Where(t => t != null && !StarterPins.Contains(t.Id)).ToList(), true);
                string how = L.T("Add your own: press ☆ on any tool in a question on the left - it shows up here and above that question's tools. Up to 12.");
                float hh = Draw.Measure(how, Draw.fBody, (int)w).Height; g.DrawString(how, Draw.fBody, sub, new RectangleF(x, y, w, hh + 4)); y += hh + 16 * S;
                var recent = tbRecent.Select(id => all.FirstOrDefault(z => z.Id == id)).Where(t => t != null).Take(8).ToList();
                if (recent.Count > 0)
                {
                    g.DrawString(L.T("RECENT"), Draw.fTiny, muted, x, y); y += 22 * S; float px = x;
                    int shownR = 0;
                    foreach (var t in recent) { if (px + 60 * S > x + w) break; if (px + Kit.ChipWidth(g, ToolName(t), null) > x + w) break; var r = Kit.Chip(g, ToolName(t), px, y, QAccent(QuestionOf(t)), false, null); shownR++; string id = t.Id; Act(tbCanvas, r, delegate { OpenFromIndex(id); }); px = r.Right + 8 * S; }
                    More(g, recent.Count - shownR, px, y, x + w);
                    y += 46 * S;
                }
            }
            return y;
        }
    }
}
