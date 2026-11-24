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
            bool own = navOwn != "" && tbQ == navOwn && tbFilter == "";   // Calculators / Reports from the bar: their own page, no chips
            bool rail = w > 860 * S && !own; float rw = rail ? 250 * S : 0, cx = x + (rail ? rw + 24 * S : 0), cw = w - (rail ? rw + 24 * S : 0);
            float top = y + 6 * S; railEnd = 0;
            // ---- the questions: a rail, or a chip row on a narrow window
            if (!idxSeen) { idxSeen = true; if (tbQ == "") tbQ = "*"; }   // the first look is every tool (22 Nov)
            int every = Tools2().Count;   // Adeeb, 23 Nov: the same number as the docs and the site (232) - SNMP's views count, though they live in one card
            var items = new List<string[]> { new[] { "*", L.T("All tools"), every.ToString(CultureInfo.CurrentCulture) }, new[] { "", L.T("Pinned"), StarterPins.Union(MyPins()).Count().ToString(CultureInfo.CurrentCulture) } };   // bug-hunt L-152: a starter you also pinned counts once
            foreach (var qd in Questions) items.Add(new[] { qd[0], L.T(qd[1]), ToolsOf(qd[0]).Count(t => !SnmpView(t.Id) || t.Id == "snmp").ToString(CultureInfo.CurrentCulture) });
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                if (rail)
                {
                    float ry = top;
                    for (int i = 0; i < items.Count; i++)
                    {
                        var it = items[i]; bool on = tbFilter == "" && tbQ == it[0];
                        if (i == 2) { g.DrawString(Kit.Sent(L.T("BY QUESTION")), Draw.fTiny, muted, x + 12 * S, ry + 8 * S); ry += 28 * S; }
                        var rr = new RectangleF(x, ry, rw, 38 * S);
                        if (on) { Theme.Fill(g, rr, 10 * S, Theme.BlueSoft); }   // v8: the picked row in the app's one selection colour (was each group's tint - brown in dark)
                        using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                        {   // Pinned carries a drawn star (was a ★ character), the other rows line up after it
                            float tx = rr.X + 14 * S; if (it[0] == "") { Kit.Icon(g, "star", new RectangleF(tx, rr.Y + 11 * S, 16 * S, 16 * S), on ? Theme.Ink : Theme.Sub); tx += 22 * S; }
                            g.DrawString(it[1], on ? Draw.fBodyB : Draw.fBody, on ? ink : sub, new RectangleF(tx, rr.Y + 9 * S, rr.Right - 46 * S - tx, 22 * S), nf);
                        }
                        var cs = g.MeasureString(it[2], Draw.fSmall); g.DrawString(it[2], Draw.fSmall, muted, rr.Right - cs.Width - 12 * S, rr.Y + 10 * S);
                        string q = it[0]; Act(tbCanvas, rr, delegate { tbSearch.Box.Text = ""; tbQ = q; tbCanvas.AutoScrollPosition = Point.Empty; tbCanvas.Invalidate(); });
                        ry += 40 * S;
                    }
                    using (var p = new Pen(Theme.Line)) g.DrawLine(p, x + rw + 12 * S, top, x + rw + 12 * S, Math.Max(ry, top + 200 * S));
                    railEnd = ry + 20 * S;   // one height for the whole page (rail and list): two Need calls a paint flipped the scrollbar on every repaint - the blinking
                }
                else if (!own)
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
                /* release check 24 Nov (perf: 17% of a Toolbox draw was list copying, 7% string splits): what's listed is worked out
                   once per question / search / language, not on every repaint (opening the page starts it afresh) */
                string listKey = tbQ + "\n" + tbFilter + "\n" + navOwn + "\n" + L.Code + "\n" + Tools2().Count;
                if (listKey == idxListKey && idxRows != null) { rows = idxRows; title = idxTitle; count = idxCount; }
                else if (tbFilter != "")
                {
                    var allHits = Matches().Where(t => !SnmpView(t.Id) || t.Id == "snmp" || tbFilter.IndexOf("snmp", StringComparison.OrdinalIgnoreCase) >= 0).ToList(); rows = allHits.Take(80).ToList();
                    // bug-hunt L-142: the real count, and "the first 80 shown" when there are more
                    title = rows.Count == 0 ? L.T("Nothing matches") + " “" + tbFilter + "”" : allHits.Count + " " + L.T(allHits.Count == 1 ? "tool matches" : "tools match") + " “" + tbFilter + "”" + (allHits.Count > rows.Count ? " · " + L.T("the first") + " " + rows.Count + " " + L.T("shown") : "");
                    count = rows.Count == 0 ? L.T("try a shorter word, like dns or wifi") : L.T("Enter opens the first");
                }
                else if (tbQ == "*")
                {
                    rows = Tools2().Where(t => !SnmpView(t.Id) || t.Id == "snmp").ToList();
                    title = L.T("All tools"); int snmpIn = Tools2().Count(t => SnmpView(t.Id) && t.Id != "snmp"); count = Tools2().Count + " " + L.T("tools") + (snmpIn > 0 ? " · " + snmpIn + " " + L.T("of them inside SNMP") : "") + " · " + L.T("hover one for what it does");
                }
                else if (tbQ == "" || Questions.All(z => z[0] != tbQ))
                {
                    tbQ = ""; py = PaintPinned(g, cx, py, cw); Need(tbCanvas, Math.Max(railEnd, py + 30 * S)); return;
                }
                else
                {
                    var qd = Questions.First(z => z[0] == tbQ); rows = ToolsOf(tbQ).Where(t => !SnmpView(t.Id) || t.Id == "snmp").ToList();
                    title = own ? L.T(navOwn == "calc" ? "Calculators" : "Reports") : L.T(qd[1]); count = rows.Count + " " + L.T(rows.Count == 1 ? "tool shown" : "tools shown");
                }
                if (listKey != idxListKey) { idxListKey = listKey; idxRows = rows; idxTitle = title; idxCount = count; idxGroups = null; }
                var tsz = g.MeasureString(title, Draw.fTitle); g.DrawString(title, Draw.fTitle, ink, new RectangleF(cx, py, cw - 200 * S, tsz.Height + 4));
                var csz = g.MeasureString(count, Draw.fBody); g.DrawString(count, Draw.fBody, sub, cx + cw - csz.Width, py + 8 * S);
                py += tsz.Height + 12 * S;
                if (tbFilter == "") py = PinRow(g, cx, py, cw);
                // grouped: by the tool's group inside a question (by question in a search)
                var groups = idxGroups ?? (idxGroups = tbFilter != "" || tbQ == "*" ? rows.GroupBy(t => { var qn = Questions.FirstOrDefault(z => z[0] == QuestionOf(t)); return qn != null ? L.T(qn[1]) : GroupName(t.Group); }).ToList()
                                            : rows.GroupBy(t => GroupName(t.Group)).ToList());
                // tiles, like Home's "Go to" (Adeeb, 22 Nov: "copy the homepage go-to style, hover for the details, in sections") -
                // an icon in the section's colour, the name and one short line; the card with everything shows on hover
                idxTiles.Clear();
                // 24 Nov (the approved mockup): a list in two columns - name, one line on what it does, the last result - instead of
                // a wall of same-size cards; a question's heading in plain words with its count
                int cols = cw > 760 * S ? 2 : 1; float tw = (cw - (cols - 1) * 24 * S) / cols, th = 58 * S;
                foreach (var grp in groups)
                {
                    py += 18 * S;
                    g.DrawString(grp.Key, Draw.fHead, ink, cx, py); var gk = g.MeasureString(grp.Key, Draw.fHead);
                    g.DrawString(grp.Count().ToString(CultureInfo.CurrentCulture), Draw.fSmall, muted, cx + gk.Width + 4 * S, py + (gk.Height - Draw.fSmall.GetHeight(g)) / 2);
                    py += gk.Height + 8 * S; int n = 0;
                    foreach (var t in grp)
                    {
                        var r = new RectangleF(cx + (n % cols) * (tw + 24 * S), py + (n / cols) * th, tw, th); n++;
                        idxTiles.Add(new KeyValuePair<RectangleF, string>(r, t.Id));
                        if (!Kit.Off(g, r)) ToolTile(g, t, r, t.Id == idxHot);
                        string id = t.Id; Act(tbCanvas, r, delegate { OpenFromIndex(id); }); Hint(tbCanvas, new RectangleF(r.X, r.Y, r.Width - 28 * S, r.Height), "tb." + id);   /* the ? clear of the row's › (release check screenshots) */
                    }
                    py += ((n + cols - 1) / cols) * th;
                }
                Need(tbCanvas, Math.Max(railEnd, py + 30 * S));
            }
        }
        float railEnd; bool idxSeen;
        readonly List<KeyValuePair<RectangleF, string>> idxTiles = new List<KeyValuePair<RectangleF, string>>(); string idxHot;
        // one row of the tool list: a small icon in the question's colour, the name, one line on what it does, and the last
        // result from this session (a dot and the time) with a chevron; a soft fill under the mouse, a line under each row
        void ToolTile(Graphics g, Tool t, RectangleF r, bool hot)
        {
            string q = QuestionOf(t), acc = QAccent(q), gk; Color c = Kit.Accent(acc);
            using (var p = new Pen(Theme.Line2)) g.DrawLine(p, r.X, r.Bottom - 0.5f, r.Right, r.Bottom - 0.5f);
            if (hot) Theme.Fill(g, new RectangleF(r.X, r.Y + 2 * S, r.Width, r.Height - 4 * S), 10 * S, Theme.Soft);
            float ic = 32 * S; var ir = new RectangleF(r.X + 8 * S, r.Y + (r.Height - ic) / 2, ic, ic);
            if (BigGlyph.TryGetValue("q:" + q, out gk) && Glyphs.Has(gk)) Glyphs.Tile(g, gk, ir, c, 8 * S); else Kit.Icon(g, "tool", ir, c);
            // the right side: the last run here, then the chevron
            float right = r.Right - 12 * S; PrevRun last;
            using (var mp = new Pen(Theme.Muted, 1.6f * S) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
            { float ax = right - 4 * S, ay = r.Y + r.Height / 2; g.DrawLines(mp, new[] { new PointF(ax - 4 * S, ay - 5 * S), new PointF(ax + 1 * S, ay), new PointF(ax - 4 * S, ay + 5 * S) }); }
            right -= 22 * S;
            if (tbLast.TryGetValue(t.Id, out last) && last.Rows != null && last.Rows.Count > 0)
            {
                int lv = 0; foreach (var row in last.Rows) { int l; if (row.Length > 0 && int.TryParse(row[0], out l) && (l == 1 || l == 2) && l > lv) lv = l; }
                string lt = last.At.ToString("HH:mm", CultureInfo.CurrentCulture); var ls = g.MeasureString(lt, Draw.fSmall);
                using (var mb = new SolidBrush(Theme.Sub)) g.DrawString(lt, Draw.fSmall, mb, right - ls.Width, r.Y + (r.Height - ls.Height) / 2);
                Draw.LvDot(g, lv, right - ls.Width - 12 * S, r.Y + r.Height / 2 - 4 * S, 8 * S, 8 * S); right -= ls.Width + 22 * S;
            }
            float tx = ir.Right + 12 * S, tw = Math.Max(40 * S, right - tx); string name = ToolName(t);
            var te = Explain.Term("tb." + t.Id); string line = te != null ? (te.Tag ?? te.What ?? "") : "";
            if (line.Length > 0) { int dot = line.IndexOf(". ", StringComparison.Ordinal); if (dot > 0) line = line.Substring(0, dot); }
            using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub))
            {
                float nh = Draw.fBodyB.GetHeight(g), lh = line != "" ? Draw.fSmall.GetHeight(g) : 0, ty = r.Y + (r.Height - nh - lh - (lh > 0 ? 2 * S : 0)) / 2;
                g.DrawString(name, Draw.fBodyB, ink, new RectangleF(tx, ty, tw, nh + 2), nf);
                if (line != "") g.DrawString(L.T(line), Draw.fSmall, sub, new RectangleF(tx, ty + nh + 2 * S, tw, lh + 2), nf);
            }
        }
        // the tile under the mouse gets its edge in the section's colour (only that tile and the one before are drawn again)
        void IdxHover(Point client)
        {
            if (!EveryTool || !tbHome) return;
            var sp = tbCanvas.AutoScrollPosition; var pt = new PointF(client.X - sp.X, client.Y - sp.Y); string hit = null; RectangleF hr = RectangleF.Empty, was = RectangleF.Empty;
            foreach (var kv in idxTiles) { if (kv.Key.Contains(pt)) { hit = kv.Value; hr = kv.Key; } if (kv.Value == idxHot) was = kv.Key; }
            if (hit == idxHot) return; idxHot = hit;
            foreach (var r in new[] { was, hr }) if (r.Width > 0) tbCanvas.Invalidate(Rectangle.Round(RectangleF.Inflate(new RectangleF(r.X + sp.X, r.Y + sp.Y, r.Width, r.Height), 4, 4)));
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
                        string id = t.Id; Act(tbCanvas, r, delegate { OpenFromIndex(id); }); Hint(tbCanvas, new RectangleF(r.X, r.Y, r.Width - 28 * S, r.Height), "tb." + id);   /* the ? clear of the row's › (release check screenshots) */
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
                y = grid(Kit.Sent(L.T("READY-MADE")), StarterPins.Select(id => all.FirstOrDefault(t => t.Id == id)).Where(t => t != null).ToList(), false);
                y = grid(Kit.Sent(L.T("YOURS")), MyPins().Select(id => all.FirstOrDefault(t => t.Id == id)).Where(t => t != null && !StarterPins.Contains(t.Id)).ToList(), true);
                string how = L.T("Add your own: press ☆ on any tool in a question on the left - it shows up here and above that question's tools. Up to 12.");
                float hh = Draw.Measure(how, Draw.fBody, (int)w).Height; g.DrawString(how, Draw.fBody, sub, new RectangleF(x, y, w, hh + 4)); y += hh + 16 * S;
                var recent = tbRecent.Select(id => all.FirstOrDefault(z => z.Id == id)).Where(t => t != null).Take(8).ToList();
                if (recent.Count > 0)
                {
                    g.DrawString(Kit.Sent(L.T("RECENT")), Draw.fTiny, muted, x, y); y += 22 * S; float px = x;
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
