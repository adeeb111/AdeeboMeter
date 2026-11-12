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
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            learnSearch = new RoundBox(Sc(300), Draw.fBody, "Search: DNS, TCP, svchost, jitter...");
            learnSearch.Box.TextChanged += delegate { learnList.AutoScrollPosition = Point.Empty; learnList.Invalidate(); };
            bar.Controls.Add(learnSearch);
            bar.Controls.Add(new Label { Text = "Tip: press Ctrl+K anywhere to jump straight to a tab, a program, a term or a protocol.", AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fSmall, Margin = new Padding(Sc(4), Sc(10), 0, 0) });
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Card };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
            learnList = new DB { Name = "learnList", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            learnList.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintLearnList(e.Graphics); }; Wire(learnList);
            learnInfo = new DB { Name = "learnInfo", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            learnInfo.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintLearnInfo(e.Graphics); }; Wire(learnInfo);
            split.Controls.Add(learnList, 0, 0); split.Controls.Add(learnInfo, 1, 0);
            learnPage.Controls.Add(split); learnPage.Controls.Add(bar);
            return learnPage;
        }
        static readonly string[][] LearnGroups = { new[] { "basics", "START HERE - HOW APPS USE THE INTERNET" }, new[] { "protocol", "PROTOCOLS - HOW YOUR PACKETS TALK" }, new[] { "windows", "WINDOWS - WHAT'S INSIDE" }, new[] { "apps", "TEAMS AND OFFICE" } };
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
                foreach (var grp in LearnGroups)
                {
                    var list = topics.Where(t => t["kind"] == grp[0] && (hit(t["title"]) || hit(t["tag"]) || t.Keys.Any(hit))).ToList(); if (list.Count == 0) continue;
                    g.DrawString(grp[1], Draw.fTiny, muted, x, y + 6 * S); y += 26 * S;
                    foreach (var t in list) { y = LearnRow(g, "topic:" + t.Id, t["title"], t["tag"], x, y, w, ink, muted, nowrap); }
                    y += 8 * S;
                }
                if (q != "")
                {
                    var terms = Explain.AllTerms.Where(e => hit(e.Friendly) || hit(e.Tag)).Take(30).ToList();
                    if (terms.Count > 0) { g.DrawString(L.T("TERMS USED IN ADEEBOMETER"), Draw.fTiny, muted, x, y + 6 * S); y += 26 * S; foreach (var e in terms) y = LearnRow(g, "term:" + e.Friendly, e.Friendly, e.Tag, x, y, w, ink, muted, nowrap); }
                }
            }
            Need(learnList, y);
        }
        float LearnRow(Graphics g, string key, string title, string sub, float x, float y, float w, Brush ink, Brush muted, StringFormat nowrap)
        {
            var rr = new RectangleF(0, y, learnList.ClientSize.Width, 30 * S);
            if (key == learnSel) using (var b = new SolidBrush(Theme.RowSel)) g.FillRectangle(b, rr);
            g.DrawString(title, Draw.fBody, ink, new RectangleF(x, y + 6 * S, w, 18 * S), nowrap);
            Act(learnList, rr, delegate { learnSel = key; learnPkts = null; learnErr = ""; learnInfo.AutoScrollPosition = Point.Empty; learnList.Invalidate(); learnInfo.Invalidate(); });
            return y + 30 * S;
        }
        void PaintLearnInfo(Graphics g)
        {
            g.TranslateTransform(learnInfo.AutoScrollPosition.X, learnInfo.AutoScrollPosition.Y);
            Begin(learnInfo);
            float x = 20 * S, y = 14 * S, w = learnInfo.ClientSize.Width - 40 * S;
            if (learnSel == "") { y = Title2(g, "Learn", x, y, w); y = Lead2(g, "Two lines and a picture each. New to networking? Start with The layers, in plain words.", x, y, w); Need(learnInfo, y); return; }
            if (learnSel.StartsWith("term:", StringComparison.Ordinal))
            {
                var e = Explain.AllTerms.FirstOrDefault(t => t.Friendly == learnSel.Substring(5));
                if (e != null) { y = Title2(g, e.Friendly, x, y, w); y = ExplainPaint.Paint(g, e, x, y, w, false); }
                Need(learnInfo, y); return;
            }
            var topic = Knowledge.Topic(learnSel.Substring(6));
            y = PaintTopic(g, learnInfo, topic, x, y, w);
            if (topic != null && topic["live"] != "")
            {
                y += 6 * S;
                var go = new RectangleF(x, y, 420 * S, 24 * S);
                string label = !LiveCapture.Available ? "See it live on my PC - needs Wireshark" : learnBusy ? "Recording 10 seconds of " + topic["title"] + "..." : "See it live on my PC (10 seconds)  →";
                using (var lb = new SolidBrush(LiveCapture.Available ? (Theme.Dark ? Theme.BlueDark : Theme.Blue) : Theme.Muted)) g.DrawString(label, Draw.fBodyB, lb, go.X, go.Y);
                if (LiveCapture.Available && !learnBusy) Act(learnInfo, go, delegate { LearnLive(topic); });
                y += 32 * S;
                if (topic["try"] != "") y = Lead2(g, "While it records: " + topic["try"], x, y, w);
                if (learnErr != "") y = Lead2(g, learnErr, x, y, w);
                if (learnPkts != null)
                {
                    if (learnPkts.Count == 0) y = Lead2(g, "Nothing of that kind happened in those 10 seconds. " + (topic["try"] != "" ? "Try it while you " + topic["try"].TrimEnd('.').ToLowerInvariant() + "." : ""), x, y, w);
                    else y = InfoCardOn(learnInfo, g, "WHAT YOUR PC JUST DID  ·  " + learnPkts.Count + " PACKETS", learnPkts.Take(30).Select(p => new[] { p.T.ToString("0.00", CultureInfo.InvariantCulture) + " s  " + p.Proto, Draw.Trunc(p.Src, 22) + " → " + Draw.Trunc(p.Dst, 22) + "   " + p.Info, "3" }).ToList(), null, x, y, w);
                }
            }
            Need(learnInfo, y);
        }
        void LearnLive(Knowledge.Rec t)
        {
            learnBusy = true; learnPkts = null; learnErr = ""; learnInfo.Invalidate();
            string filter = t["live"];
            Task.Factory.StartNew(() =>
            {
                string err, file; var pk = Packets.Capture(M, filter, 10, null, out file, out err);
                try { BeginInvoke((Action)delegate { learnBusy = false; learnPkts = err == "" ? pk : null; learnErr = err; learnInfo.Invalidate(); }); } catch { learnBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }

        // ---------- Ctrl+K: jump to anything
        void OpenPalette()
        {
            var items = new List<KeyValuePair<string, Action>>();
            foreach (var kv in tabNames) { string t = kv.Key; items.Add(new KeyValuePair<string, Action>("Tab · " + kv.Value[0], delegate { tab = t; ShowTab(); })); }
            foreach (var tl in Tools2()) { string id = tl.Id; items.Add(new KeyValuePair<string, Action>("Tool · " + tl.Title, delegate { tab = "toolbox"; ShowTab(); SelectTool(id); })); }
            foreach (var f in Investigate.All) { string id = f.Id; items.Add(new KeyValuePair<string, Action>("Investigate · " + f.Title, delegate { GoGuide(id); })); }
            items.Add(new KeyValuePair<string, Action>("Settings", delegate { tab = "settings"; ShowTab(); }));
            items.Add(new KeyValuePair<string, Action>("About", delegate { tab = "about"; ShowTab(); }));
            foreach (var tl in new[] { new[] { "precall", "Pre-call check" }, new[] { "dns", "DNS speed test" }, new[] { "vpn", "VPN map" }, new[] { "see", "What can AdeeboMeter see here?" } }) { var t2 = tl; items.Add(new KeyValuePair<string, Action>("Tool · " + t2[1], delegate { tab = "health"; ShowTab(); RunTool(t2[0], t2[1]); })); }
            foreach (var t in Knowledge.Learn()) { string id = t.Id; items.Add(new KeyValuePair<string, Action>("Learn · " + t["title"], delegate { tab = "learn"; ShowTab(); learnSel = "topic:" + id; learnPkts = null; learnList.Invalidate(); learnInfo.Invalidate(); })); }
            List<AppStat> apps; lock (M.L) apps = M.Apps.ToList();
            foreach (var a in apps.OrderBy(a => a.Name)) { var aa = a; items.Add(new KeyValuePair<string, Action>("Program · " + a.Name, delegate { tab = "procs"; ShowTab(); RefreshProcs(); SelectProc(aa); })); }
            foreach (var e in Explain.AllTerms) { string f = e.Friendly; items.Add(new KeyValuePair<string, Action>("Term · " + f, delegate { tab = "learn"; ShowTab(); learnSel = "term:" + f; learnList.Invalidate(); learnInfo.Invalidate(); })); }
            foreach (var p in Cfg.Pins) { string pin = p; items.Add(new KeyValuePair<string, Action>("Check · " + p, delegate { tab = "dest"; ShowTab(); destBox.Box.Text = pin; StartDest(); })); }
            using (var pal = new Palette(items)) { pal.StartPosition = FormStartPosition.Manual; pal.Location = new Point(Left + (Width - pal.Width) / 2, Top + Sc(90)); pal.ShowDialog(this); if (pal.Chosen != null) pal.Chosen(); }
        }
    }

    // the Ctrl+K box: type, arrows, Enter
    sealed class Palette : Form
    {
        static readonly char[] Space = { ' ' };
        readonly List<KeyValuePair<string, Action>> all; readonly RoundBox box; readonly ListBox list; public Action Chosen;
        public Palette(List<KeyValuePair<string, Action>> items)
        {
            all = items; float S = Draw.S;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; KeyPreview = true; BackColor = Theme.Card; Size = new Size((int)(620 * S), (int)(420 * S)); Padding = new Padding((int)(12 * S));
            box = new RoundBox((int)(596 * S), Draw.fInput, "Jump to a tab, program, term or protocol...") { Dock = DockStyle.Top };
            list = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Ink, Font = Draw.fBody, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = (int)(30 * S) };
            list.DrawItem += delegate(object s, DrawItemEventArgs e)
            {
                if (e.Index < 0) return; bool sel = (e.State & DrawItemState.Selected) != 0;
                using (var b = new SolidBrush(sel ? Theme.RowSel : Theme.Card)) e.Graphics.FillRectangle(b, e.Bounds);
                string t = (string)list.Items[e.Index]; int dot = t.IndexOf(" · ", StringComparison.Ordinal);
                TextRenderer.DrawText(e.Graphics, dot > 0 ? t.Substring(0, dot) : "", Draw.fSmall, new Rectangle(e.Bounds.X + (int)(8 * S), e.Bounds.Y, (int)(80 * S), e.Bounds.Height), Theme.Muted, TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics, dot > 0 ? t.Substring(dot + 3) : t, Draw.fBody, new Rectangle(e.Bounds.X + (int)(92 * S), e.Bounds.Y, e.Bounds.Width - (int)(100 * S), e.Bounds.Height), Theme.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            var gap = new Panel { Dock = DockStyle.Top, Height = (int)(8 * S), BackColor = Theme.Card };
            Controls.Add(list); Controls.Add(gap); Controls.Add(box);
            box.Box.TextChanged += delegate { Fill(); };
            list.DoubleClick += delegate { Pick(); };
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
                else if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Pick(); }
                else if (e.KeyCode == Keys.Down && list.Items.Count > 0) { e.Handled = true; list.SelectedIndex = Math.Min(list.Items.Count - 1, list.SelectedIndex + 1); }
                else if (e.KeyCode == Keys.Up && list.Items.Count > 0) { e.Handled = true; list.SelectedIndex = Math.Max(0, list.SelectedIndex - 1); }
            };
            Deactivate += delegate { Close(); };
            Shown += delegate { box.Box.Focus(); };
            Fill();
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using (var p = new Pen(Theme.Line)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1); }
        void Fill()
        {
            string q = box.Box.Text.Trim();
            var words = q.Split(Space, StringSplitOptions.RemoveEmptyEntries);
            var hits = all.Where(i => words.All(wd => i.Key.IndexOf(wd, StringComparison.OrdinalIgnoreCase) >= 0)).Take(200).Select(i => i.Key).ToArray();
            list.BeginUpdate(); list.Items.Clear(); list.Items.AddRange(hits); list.EndUpdate();
            if (list.Items.Count > 0) list.SelectedIndex = 0;
        }
        void Pick()
        {
            if (list.SelectedIndex < 0) return; string k = (string)list.SelectedItem;
            Chosen = all.First(i => i.Key == k).Value; Close();
        }
    }
}
