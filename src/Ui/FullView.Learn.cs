// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Learn mode: every protocol and Windows part as a one-page story with its ladder - and, with Wireshark, live on your PC.
    // Plus Ctrl+K: jump to anything.
    sealed partial class FullView
    {
        Panel learnPage; DB learnList, learnInfo; RoundBox learnSearch; string learnSel = ""; List<Packets.Pkt> learnPkts; string learnErr = ""; volatile bool learnBusy;

        Panel BuildLearnPage()
        {
            learnPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            // AM-CG-255: the row wraps on a narrow window or at 150-200% and grows to fit, instead of cutting off the filters
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(0, Sc(64)), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, Sc(6)), WrapContents = true };
            learnSearch = new RoundBox(Sc(300), Draw.fBody, "Search: DNS, TCP, svchost, jitter...");
            learnSearch.Box.TextChanged += delegate { if (learnSearch.Box.Text.Trim() != "") { if (learnFilter != "all") { learnFilter = "all"; SyncLearnView(); } if (Cfg.LearnView != "all") SetLearnView("all"); } learnList.AutoScrollPosition = Point.Empty; learnList.Invalidate(); };   // searching shows everything
            AddLearnViewButtons(bar);
            bar.Controls.Add(learnSearch);
            learnTip = new Label { Text = AdeeboMeter.L.T("Tip: press Ctrl+K anywhere to jump straight to a tab, a program, a term or a protocol."), AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fSmall, Margin = new Padding(Sc(4), Sc(10), 0, 0) };
            bar.Controls.Add(learnTip);
            AddLearnChips(bar);
            var split = learnSplit = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Card };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
            learnList = new DB { Name = "learnList", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            learnList.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintLearnList(e.Graphics); }; Wire(learnList);
            learnInfo = new DB { Name = "learnInfo", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            learnInfo.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintLearnInfo(e.Graphics); }; Wire(learnInfo);
            split.Controls.Add(learnList, 0, 0); split.Controls.Add(learnInfo, 1, 0);
            learnPage.Controls.Add(split); learnPage.Controls.Add(bar);
            SyncLearnView();
            return learnPage;
        }
        static readonly string[][] LearnGroups = { new[] { "basics", "START HERE - HOW APPS USE THE INTERNET" }, new[] { "protocol", "PROTOCOLS - HOW YOUR PACKETS TALK" }, new[] { "provider", "INSIDE THE INTERNET - PROVIDERS, TV AND STREAMING" }, new[] { "mobile", "INSIDE THE MOBILE NETWORK - 4G AND 5G" }, new[] { "crypto", "LOCKS AND KEYS - CERTIFICATES AND HTTPS" }, new[] { "windows", "WINDOWS - WHAT'S INSIDE" }, new[] { "apps", "TEAMS AND OFFICE" }, new[] { "streaming", "STREAMING - HOW VIDEO REACHES YOU" }, new[] { "investing", "INVESTING BASICS - LEARNING, NEVER ADVICE" } };
        void PaintLearnList(Graphics g)
        {
            g.TranslateTransform(learnList.AutoScrollPosition.X, learnList.AutoScrollPosition.Y);
            Begin(learnList);
            float x = 14 * S, y = 8 * S, w = learnList.ClientSize.Width - 22 * S; string q = learnSearch.Box.Text.Trim();
            Func<string, bool> hit = s => q == "" || (s ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            var topics = Knowledge.Learn();
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (learnFilter == "all" || learnFilter == "labs") y = PaintLabList(g, x, y, w, ink, muted, nowrap, hit);
                if (learnFilter == "all" || learnFilter == "games") y = PaintGameList(g, x, y, w, ink, muted, nowrap, hit);
                foreach (var grp in LearnGroups)
                {
                    if (learnFilter != "all" && learnFilter != "topics") break;
                    var list = topics.Where(t => t["kind"] == grp[0] && (hit(t["title"]) || hit(t["tag"]) || t.Keys.Any(hit))).ToList(); if (list.Count == 0) continue;
                    g.DrawString(L.T(grp[1]), Draw.fTiny, muted, x, y + 6 * S); y += 26 * S;
                    foreach (var t in list) { y = LearnRow(g, "topic:" + t.Id, t["title"], t["tag"], x, y, w, ink, muted, nowrap); }
                    y += 8 * S;
                }
                if (q != "" || learnFilter == "terms")
                {
                    var termsAll = Explain.AllTerms.Where(e => hit(e.Friendly) || hit(e.Tag)).ToList(); var terms = termsAll.Take(learnFilter == "terms" ? 400 : 30).ToList();   /* L-359: the heading says when more matched */
                    if (terms.Count > 0) { g.DrawString(L.T("TERMS USED IN ADEEBOMETER") + (termsAll.Count > terms.Count ? "  ·  " + terms.Count + " " + L.T("of") + " " + termsAll.Count + " - " + L.T("type more to narrow it") : ""), Draw.fTiny, muted, x, y + 6 * S); y += 26 * S; foreach (var e in terms) y = LearnRow(g, "term:" + e.Friendly, e.Friendly, e.Tag, x, y, w, ink, muted, nowrap); }
                }
            }
            Need(learnList, y);
        }
        float LearnRow(Graphics g, string key, string title, string sub, float x, float y, float w, Brush ink, Brush muted, StringFormat nowrap)
        {
            title = L.T(title);
            var rr = new RectangleF(0, y, learnList.ClientSize.Width, 30 * S);
            if (key == learnSel) Theme.Fill(g, rr, 8 * S, Theme.RowSel);
            g.DrawString(title, Draw.fBody, ink, new RectangleF(x, y + 6 * S, w, 18 * S), nowrap);
            Act(learnList, rr, delegate { learnSel = key; learnPkts = null; learnErr = ""; learnInfo.AutoScrollPosition = Point.Empty; learnList.Invalidate(); learnInfo.Invalidate(); });
            return y + 30 * S;
        }
        void PaintLearnInfo(Graphics g)
        {
            g.TranslateTransform(learnInfo.AutoScrollPosition.X, learnInfo.AutoScrollPosition.Y);
            Begin(learnInfo);
            float x = 20 * S, y = 14 * S, w = learnInfo.ClientSize.Width - 40 * S;
            if (Cfg.LearnView != "all")
            {
                // By goal: the cards (or a card's page) when nothing is open; a way back above anything that is
                if (learnSel == "") { y = PaintLearnHome(g, x, y, w); Need(learnInfo, y); return; }
                var card = LearnCards.FirstOrDefault(c => c[0] == learnGroup);
                y = BackLink(g, "‹ " + (card != null ? L.T(card[1]) : L.T("All of Learn")), x, y, delegate { learnSel = ""; });
            }
            if (learnSel.StartsWith("game:", StringComparison.Ordinal)) { y = PaintGame(g, learnSel.Substring(5), x, y, w); Need(learnInfo, y + 20 * S); return; }
            if (learnSel == "") { y = Title2(g, AdeeboMeter.L.T("Learn"), x, y, w); y = Lead2(g, AdeeboMeter.L.T("Labs at the top: play with how networks behave, with your own numbers. Below them, two lines and a picture each - new to networking? Start with The layers, in plain words."), x, y, w); Need(learnInfo, y); return; }
            if (learnSel == "lab:netlab") { y = PaintNetLab(g, x, y, w); Need(learnInfo, y + 20 * S); return; }   // 1.1.6: the Network Lab card
            if (learnSel.StartsWith("lab:", StringComparison.Ordinal)) { y = PaintLab(learnInfo, g, learnSel.Substring(4), x, y, w); Need(learnInfo, y + 20 * S); return; }
                        if (learnSel.StartsWith("term:", StringComparison.Ordinal))
            {
                var e = Explain.AllTerms.FirstOrDefault(t => t.Friendly == learnSel.Substring(5));
                if (e != null) { y = Title2(g, e.Friendly, x, y, w); y = ExplainPaint.Paint(g, e, x, y, w, false); }
                Need(learnInfo, y); return;
            }
            var topic = Knowledge.Topic(learnSel.Substring(6));
            y = PaintTopic(g, learnInfo, topic, x, y, w);
            if (topic != null) LearnRead(y);   // Adeebo Points: the topic's text read to the end
            if (topic != null && topic["check"] != "") y = PaintCheck(g, topic, x, y + 8 * S, w);
            if (topic != null && topic["live"] != "")
            {
                y += 6 * S;
                var go = new RectangleF(x, y, 420 * S, 24 * S);
                bool mine = learnBusy && learnLiveFor == topic.Id;
                string label = !LiveCapture.Available ? "See it live on my PC - needs Wireshark" : mine ? (learnPhase == "decoding" ? "Decoding what was recorded..." : "Recording 10 seconds of " + topic["title"] + "...  Stop") : learnBusy ? "Another lesson is recording - wait or stop it there" : "See it live on my PC (10 seconds)  →";
                using (var lb = new SolidBrush(LiveCapture.Available && (!learnBusy || mine) ? (Theme.Dark ? Theme.BlueDark : Theme.Blue) : Theme.Muted)) g.DrawString(label, Draw.fBodyB, lb, go.X, go.Y);
                if (LiveCapture.Available && !learnBusy) Act(learnInfo, go, delegate { LearnLive(topic); });
                else if (mine && learnPhase != "decoding") Act(learnInfo, go, delegate { learnStop = true; learnInfo.Invalidate(); });
                y += 32 * S;
                if (LiveCapture.Available && !mine) y = Lead2(g, AdeeboMeter.L.T("Records this lesson's traffic (") + topic["live"] + ") on your active adapter for 10 s: packet headers only, up to 5 MB, deleted as soon as it's decoded.", x, y, w);
                if (topic["try"] != "") y = Lead2(g, AdeeboMeter.L.T("While it records: ") + topic["try"], x, y, w);
                if (learnErr != "" && learnPktsFor == topic.Id) y = Lead2(g, learnErr, x, y, w);
                if (learnPkts != null && learnPktsFor == topic.Id)
                {
                    if (learnPkts.Count == 0) y = Lead2(g, AdeeboMeter.L.T("Nothing of that kind happened in those 10 seconds. ") + (topic["try"] != "" ? "Try it while you " + topic["try"].TrimEnd('.').ToLowerInvariant() + "." : ""), x, y, w);
                    else y = InfoCardOn(learnInfo, g, "WHAT YOUR PC JUST DID  ·  " + learnPkts.Count + " PACKETS", learnPkts.Take(30).Select(p => new[] { p.T.ToString("0.00", CultureInfo.InvariantCulture) + " s  " + p.Proto, Draw.Trunc(p.Src, 22) + " → " + Draw.Trunc(p.Dst, 22) + "   " + p.Info, "3" }).ToList(), null, x, y, w);
                }
            }
            Need(learnInfo, y);
        }
        // ---------- Check yourself: one question per topic. Right the first time -> +2, once. A wrong first pick shows
        // the answer and is remembered (points.txt, "tried.check.<id>", 0 points), so a restart can't pay for it.
        readonly Dictionary<string, string> checkPick = new Dictionary<string, string>();
        float PaintCheck(Graphics g, Knowledge.Rec t, float x, float y, float w)
        {
            var p = CheckParts(t["check"]); if (p.Length < 3) return y;
            string id = t.Id, q = p[0], right = p[1], picked; checkPick.TryGetValue(id, out picked);
            // the same order every time, not always the right one first
            var opts = p.Skip(1).Where(o => o != "").OrderBy(o => Hash(id + o)).ToList();
            bool earned = Points.Me.Has("check." + id), tried = !earned && Points.Me.Tried("check." + id);
            if (picked == null && tried) picked = "\u0001";   // answered wrong before: show the answer, no buttons
            if (picked == null && !Cfg.Points && Points.Me.Right("check." + id)) picked = right;   // answered right with Points off: stays answered
            float qh = Draw.Measure(L.T(q), Draw.fBodyB, (int)(w - 32 * S)).Height, ow = (w - 46 * S) / 2, oh = 40 * S;
            var card = new RectangleF(x, y, w, 44 * S + qh + (float)Math.Ceiling(opts.Count / 2.0) * (oh + 10 * S) + (picked != null ? 34 * S : 0));
            Theme.Fill(g, card, 12 * S, Theme.Soft);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("CHECK YOURSELF") + (Cfg.Points ? "  ·  " + (earned ? "✓ " : "") + "+" + Points.Value("check." + id) : ""), Draw.fTiny, mb, x + 16 * S, y + 12 * S);
            using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(L.T(q), Draw.fBodyB, ib, new RectangleF(x + 16 * S, y + 32 * S, w - 32 * S, qh + 4));
            float oy = y + 40 * S + qh;
            for (int i = 0; i < opts.Count; i++)
            {
                string o = opts[i]; var r = new RectangleF(x + 16 * S + (i % 2) * (ow + 14 * S), oy + (i / 2) * (oh + 10 * S), ow, oh);
                bool isRight = o == right, isPick = o == picked, done = picked != null;
                Theme.Fill(g, r, 10 * S, done && isRight ? Theme.LvSoft(0) : isPick ? Theme.LvSoft(2) : Theme.Card); if (done && isRight) Theme.Stroke(g, r, 10 * S, Theme.Green, 2);
                using (var tb = new SolidBrush(Theme.Ink)) using (var c = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord }) g.DrawString(L.T(o), Draw.fBody, tb, new RectangleF(r.X + 12 * S, r.Y, r.Width - 20 * S, r.Height), c);
                if (!done) Act(learnInfo, r, delegate { checkPick[id] = o; if (o == right) { if (Cfg.Points) EarnPts("check." + id, true, true); else Points.Me.MarkRight("check." + id); } else Points.Me.MarkTried("check." + id);   /* remembered even with Points off: a revealed answer can never pay later */ learnInfo.Invalidate(); });
            }
            if (picked != null)
            {
                string msg = picked == right ? L.T("Right.") : picked == "\u0001" ? L.T("You answered this one before - the answer is") + ": " + L.T(right) : L.T("Not quite - the answer is") + ": " + L.T(right);
                using (var b = new SolidBrush(picked == right ? Theme.LvText(0) : Theme.LvText(2))) g.DrawString(msg, Draw.fSmall, b, new RectangleF(x + 16 * S, card.Bottom - 30 * S, w - 32 * S, 24 * S));
            }
            return card.Bottom + 14 * S;
        }
        static int Hash(string s) { unchecked { int h = 17; foreach (char c in s ?? "") h = h * 31 + c; return h; } }

        int learnGen; volatile bool learnStop; string learnPhase = "", learnLiveFor = "", learnPktsFor = "";
        void LearnLive(Knowledge.Rec t)
        {
            // AM-CG-249: the result belongs to the lesson (and the run) that started it - another lesson never shows it
            int gen = ++learnGen; string forId = t.Id; learnBusy = true; learnStop = false; learnPhase = "recording"; learnLiveFor = forId; learnPkts = null; learnPktsFor = ""; learnErr = ""; learnInfo.Invalidate();
            string filter = t["live"];
            Task.Factory.StartNew(() =>
            {
                string err, file; var pk = Packets.Capture(M, filter, 10, null, out file, out err, true, () => learnStop, ph => { try { BeginInvoke((Action)delegate { if (gen == learnGen) { learnPhase = ph; learnInfo.Invalidate(); } }); } catch (InvalidOperationException) { } });
                try { BeginInvoke((Action)delegate { if (gen != learnGen) return; learnBusy = false; learnLiveFor = ""; learnPkts = err == "" ? pk : null; learnPktsFor = forId; learnErr = err; learnInfo.Invalidate(); }); } catch { learnBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }
    }
}
