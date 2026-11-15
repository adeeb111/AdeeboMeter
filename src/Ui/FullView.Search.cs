// "Search anything" (PLAN 11.11a): Ctrl+K, "/" or the big pill. One box finds tools, pages, settings, Learn topics, terms,
// programs, today's events and sites, grouped, with icons and colours. Forgives a typo, remembers the last few searches,
// pins a result to your own tab (Ctrl+P), and never comes back empty-handed without a word.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    sealed class PalItem
    {
        public string Group, Title, Sub = "", Icon = "star", Acc = "toolbox", Pin, Words; public Action Go;
        public bool Header; public int Rank;
    }

    partial class FullView
    {
        internal static readonly string[] SearchGroups = { "Tools", "Pages", "Fix a problem", "Settings", "Learn", "Terms", "Programs", "Today", "Sites" };
        internal static readonly string[] SearchExamples = { "port 443", "why is my game lagging", "wifi channel", "github.com", "dns", "mtu", "speed test", "who is on my network" };

        List<PalItem> SearchItems()
        {
            var l = new List<PalItem>();
            Action<string, string, string, string, string, string, Action> add = (grp, title, sub, icon, acc, pin, go) =>
                l.Add(new PalItem { Group = grp, Title = title ?? "", Sub = sub ?? "", Icon = icon, Acc = acc, Pin = pin, Go = go, Words = ((title ?? "") + " " + L.T(title ?? "") + " " + (sub ?? "")).ToLowerInvariant() });
            foreach (var id in NavIds())
            {
                string nid = id; add("Pages", L.T(NavName(id)), "", NavIcon(id), NavAccent(id), "nav:" + id, delegate { OpenNav(nid); });
            }
            foreach (var kv in tabNames)
            {
                string t = kv.Key; if (NavInfo.ContainsKey(t)) continue;
                add("Pages", L.T(kv.Value[0]), "", "go", "home", null, delegate { tab = t; ShowTab(); SyncNav(); });
            }
            add("Pages", L.T("Settings"), "", "settings", "reports", null, delegate { tab = "settings"; ShowTab(); SyncNav(); });
            add("Pages", L.T("About"), "", "star", "home", null, delegate { tab = "about"; ShowTab(); SyncNav(); });
            foreach (var f in Investigate.All) { string id = f.Id; add("Fix a problem", L.T(f.Title), "", "question", "home", null, delegate { GoGuide(id); }); }
            foreach (var it in SetItems().Where(i => i.Show == null || i.Show()))
            {
                var si = it; add("Settings", L.T(si.Label), L.T(si.Sec), "settings", "reports", "set:" + si.Label, delegate { OpenSettingsAt(si.Sec); if (settingsSearch != null) settingsSearch.Box.Text = si.Label; });
                l[l.Count - 1].Words += " " + (si.Keys ?? "").ToLowerInvariant();
            }
            foreach (var t in Knowledge.Learn()) { string id = t.Id; add("Learn", t["title"], "", "learn", "learn", null, delegate { tab = "learn"; ShowTab(); SyncNav(); learnSel = "topic:" + id; learnPkts = null; learnList.Invalidate(); learnInfo.Invalidate(); }); }
            foreach (var e in Explain.AllTerms) { if (e.Friendly == null || e.Friendly == "") continue; string f = e.Friendly; add("Terms", f, e.Tag, "question", "learn", null, delegate { tab = "learn"; ShowTab(); SyncNav(); learnSel = "term:" + f; learnList.Invalidate(); learnInfo.Invalidate(); }); }
            List<AppStat> apps; lock (M.L) apps = M.Apps.ToList();
            foreach (var a in apps.OrderBy(a => a.Name)) { var aa = a; add("Programs", a.Name, "", "app", "home", null, delegate { tab = "procs"; ShowTab(); SyncNav(); RefreshProcs(); SelectProc(aa); }); }
            foreach (var ev in M.Events.Recent(60)) add("Today", ev.Text, ev.T.ToString("t", CultureInfo.CurrentCulture), "time", "time", null, delegate { OpenNav("time"); });
            foreach (var p in Cfg.Pins) { string pin = p; add("Sites", p, L.T("Check a destination"), "dest", "dest", "site:" + p, delegate { tab = "dest"; ShowTab(); SyncNav(); destBox.Box.Text = pin; StartDest(); }); }
            return l;
        }
        static string[] NavIds() { return Cfg.NavIds; }

        void OpenPalette()
        {
            var items = SearchItems(); var tools = Tools2();
            Func<string, List<PalItem>> toolHits = q => Matches(q).Take(8).Select(t =>
            {
                string id = t.Id, qq = QuestionOf(t);
                return new PalItem { Group = "Tools", Title = t.Id == "snmp" ? L.T("SNMP - ask a switch or router") : L.T(t.Title), Sub = qq != "" ? L.T(Questions.First(z => z[0] == qq)[1]) : "", Icon = QIcon(qq), Acc = QAccent(qq), Pin = "tool:" + id, Go = delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); SyncNav(); OpenTool(id); } };
            }).ToList();
            // the words a typo is fixed towards: tool names, the everyday words and every title
            var vocab = new HashSet<string>(StringComparer.Ordinal);
            foreach (var w in Synonyms.Keys) vocab.Add(w.ToLowerInvariant());
            foreach (var t in tools) foreach (var w in (t.Id + " " + t.Title).ToLowerInvariant().Split(TitleSplit, StringSplitOptions.RemoveEmptyEntries)) vocab.Add(w);
            foreach (var it in items) foreach (var w in it.Words.Split(TitleSplit, StringSplitOptions.RemoveEmptyEntries)) if (w.Length > 2) vocab.Add(w);
            // the front door: a problem in your own words opens a case ("teams keeps freezing" -> Calls freeze), above the tools
            Func<string, List<PalItem>> hits = q =>
            {
                var l = toolHits(q); var sym = Cases.Match(q);
                if (sym != null) { string sid = sym[0]; l.Insert(0, new PalItem { Group = L.T("Investigate"), Title = L.T("Investigate") + ": " + L.T(sym[1]), Sub = L.T("Runs the checks that fit, then a report you can keep"), Icon = "question", Acc = "home", Pin = "", Words = sym[3], Go = delegate { OpenCase(sid, "search", "", null, null); } }); }
                return l;
            };
            using (var pal = new Palette(items, hits, vocab, PinToMine))
            {
                pal.StartPosition = FormStartPosition.Manual; pal.Location = new Point(Left + (Width - pal.Width) / 2, Top + Sc(90));
                pal.ShowDialog(this);
                if (pal.Chosen != null) pal.Chosen();
            }
        }
        internal void CheckSite(string site) { tab = "dest"; ShowTab(); SyncNav(); destBox.Box.Text = site; StartDest(); }

        // one word against the vocabulary: the closest word one letter away (two for long words), or null
        internal static string Fix(string w, HashSet<string> vocab)
        {
            if (w.Length < 4 || vocab.Contains(w) || w.All(char.IsDigit) || w.Contains('.')) return null;
            string best = null; int bd = w.Length >= 7 ? 3 : 2;
            foreach (var v in vocab)
            {
                if (Math.Abs(v.Length - w.Length) >= bd) continue;
                int d = EditDist(w, v, bd); if (d < bd) { bd = d; best = v; if (d == 1 && v[0] == w[0]) break; }
            }
            return best;
        }
        // Damerau-Levenshtein (a swap counts as one), stopping early past the limit
        internal static int EditDist(string a, string b, int limit)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                int rowMin = int.MaxValue;
                for (int j = 1; j <= b.Length; j++)
                {
                    int c = a[i - 1] == b[j - 1] ? 0 : 1;
                    int v = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + c);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) v = Math.Min(v, d[i - 2, j - 2] + 1);
                    d[i, j] = v; if (v < rowMin) rowMin = v;
                }
                if (rowMin >= limit) return limit;
            }
            return d[a.Length, b.Length];
        }
        // how well one item matches the words: 0 = not at all
        internal static int Score(PalItem it, string[] words)
        {
            string title = it.Title.ToLowerInvariant(); int total = 0;
            foreach (var w in words)
            {
                int best = 0;
                if (title.StartsWith(w, StringComparison.Ordinal)) best = 30;
                else if (System.Text.RegularExpressions.Regex.IsMatch(title, @"(^|[^\p{L}0-9])" + System.Text.RegularExpressions.Regex.Escape(w))) best = 20;
                else if (title.Contains(w)) best = 8;
                else if (System.Text.RegularExpressions.Regex.IsMatch(it.Words, @"(^|[^\p{L}0-9])" + System.Text.RegularExpressions.Regex.Escape(w))) best = 4;
                if (best == 0) return 0;
                total += best;
            }
            return total;
        }
    }

    // the search window: a box, grouped results, a hint line
    sealed class Palette : Form
    {
        static readonly char[] Space = { ' ' };
        readonly List<PalItem> all; readonly Func<string, List<PalItem>> toolHits; readonly HashSet<string> vocab; readonly Action<string> pin;
        readonly RoundBox box; readonly ListBox list; readonly Label foot; string fixedNote = "";
        public Action Chosen; public string Pinned;
        static float S { get { return Draw.S; } }
        static readonly StringFormat TitleFmt = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter }, SubFmt = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter, Alignment = StringAlignment.Far };
        public Palette(List<PalItem> items, Func<string, List<PalItem>> toolHits, HashSet<string> vocab, Action<string> pin)
        {
            all = items; this.toolHits = toolHits; this.vocab = vocab; this.pin = pin;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; KeyPreview = true; BackColor = Theme.Card;
            Size = new Size((int)(680 * S), (int)(500 * S)); Padding = new Padding((int)(14 * S));
            string ex = FullView.SearchExamples[DateTime.Now.Minute % FullView.SearchExamples.Length];
            box = new RoundBox((int)(650 * S), Draw.fInput, L.T("Search anything... try") + " \"" + ex + "\"") { Dock = DockStyle.Top };
            list = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Ink, Font = Draw.fBody, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawVariable };
            list.MeasureItem += delegate(object s, MeasureItemEventArgs e) { var it = (PalItem)list.Items[e.Index]; e.ItemHeight = (int)((it.Header ? 30 : 40) * S); };
            list.DrawItem += DrawRow;
            foot = new Label { Dock = DockStyle.Bottom, Height = (int)(26 * S), ForeColor = Theme.Muted, Font = Draw.fSmall, TextAlign = ContentAlignment.MiddleLeft, Text = L.T("↑↓ move · Enter open · Ctrl+P pin to your tab · Esc close") };
            var gap = new Panel { Dock = DockStyle.Top, Height = (int)(8 * S), BackColor = Theme.Card };
            Controls.Add(list); Controls.Add(foot); Controls.Add(gap); Controls.Add(box);
            box.Box.TextChanged += delegate { Fill(); };
            list.MouseUp += delegate(object s, MouseEventArgs e) { int i = list.IndexFromPoint(e.Location); if (i >= 0 && !((PalItem)list.Items[i]).Header) { list.SelectedIndex = i; Pick(); } };
            list.SelectedIndexChanged += delegate { if (list.SelectedIndex >= 0 && ((PalItem)list.Items[list.SelectedIndex]).Header) Step(1); };
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
                else if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Pick(); }
                else if (e.KeyCode == Keys.P && e.Control) { e.SuppressKeyPress = true; PinSel(); }
                else if (e.KeyCode == Keys.Down) { e.Handled = true; Step(1); }
                else if (e.KeyCode == Keys.Up) { e.Handled = true; Step(-1); }
            };
            Deactivate += delegate { Close(); };
            Shown += delegate { box.Box.Focus(); };
            Fill();
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using (var p = new Pen(Theme.Line)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1); }

        void DrawRow(object s, DrawItemEventArgs e)
        {
            if (e.Index < 0) return; var it = (PalItem)list.Items[e.Index]; var g = e.Graphics; var b0 = e.Bounds;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.TextRenderingHint = Draw.WindowText;
            using (var bg = new SolidBrush(Theme.Card)) g.FillRectangle(bg, b0);
            if (it.Header)
            {
                using (var m = new SolidBrush(it.Acc == "" ? Theme.Muted : Kit.Ink(it.Acc))) g.DrawString(it.Title.ToUpperInvariant(), Draw.fTiny, m, b0.X + 6 * S, b0.Bottom - 20 * S);
                return;
            }
            bool sel = (e.State & DrawItemState.Selected) != 0;
            var r = new RectangleF(b0.X + 2 * S, b0.Y + 2 * S, b0.Width - 4 * S, b0.Height - 4 * S);
            if (sel) Theme.Fill(g, r, 8 * S, Kit.Soft(it.Acc));
            var ic = new RectangleF(r.X + 8 * S, r.Y + (r.Height - 26 * S) / 2, 26 * S, 26 * S);
            using (var cb = new SolidBrush(sel ? Theme.Card : Kit.Soft(it.Acc))) g.FillEllipse(cb, ic);
            Kit.Icon(g, it.Icon, new RectangleF(ic.X + 6 * S, ic.Y + 6 * S, 14 * S, 14 * S), Kit.Accent(it.Acc));
            float tx = ic.Right + 10 * S, right = r.Right - 8 * S;
            if (sel && it.Pin != null) { string ph = "Ctrl+P"; var ps = g.MeasureString(ph, Draw.fTiny); using (var m = new SolidBrush(Theme.Muted)) g.DrawString(ph, Draw.fTiny, m, right - ps.Width, r.Y + (r.Height - ps.Height) / 2); right -= ps.Width + 10 * S; }
            float subW = 0;
            if (it.Sub != "") { subW = Math.Min(g.MeasureString(it.Sub, Draw.fSmall).Width + 4, (right - tx) * 0.45f); using (var m = new SolidBrush(Theme.Muted)) g.DrawString(it.Sub, Draw.fSmall, m, new RectangleF(right - subW, r.Y + (r.Height - 18 * S) / 2, subW, 18 * S), SubFmt); }
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(it.Title, Draw.fBody, ink, new RectangleF(tx, r.Y + (r.Height - 20 * S) / 2, right - tx - subW - 10 * S, 22 * S), TitleFmt);
        }

        void Step(int dir)
        {
            int n = list.Items.Count, i = list.SelectedIndex;
            for (int k = 0; k < n; k++)
            {
                i += dir; if (i < 0 || i >= n) return;
                if (!((PalItem)list.Items[i]).Header) { list.SelectedIndex = i; return; }
            }
        }

        void Fill()
        {
            string q = box.Box.Text.Trim().ToLowerInvariant();
            var words = q.Split(Space, StringSplitOptions.RemoveEmptyEntries);
            var rows = new List<PalItem>(); fixedNote = "";
            if (words.Length == 0)
            {
                var recent = (Cfg.Searches ?? "").Split('|').Where(x => x.Trim() != "").Take(6).ToList();
                if (recent.Count > 0)
                {
                    rows.Add(new PalItem { Header = true, Title = L.T("Recent searches"), Acc = "" });
                    foreach (var r0 in recent) { string rr = r0; rows.Add(new PalItem { Title = rr, Icon = "search", Acc = "home", Go = delegate { box.Box.Text = rr; box.Box.SelectionStart = rr.Length; } }); }
                }
                rows.Add(new PalItem { Header = true, Title = L.T("Pages"), Acc = "" });
                rows.AddRange(all.Where(i => i.Group == "Pages").Take(12));
                Show(rows); return;
            }
            var hits = Find(words);
            if (hits.Count == 0)
            {
                // a typo? fix each word towards a word we know
                var fixd = words.Select(w => FullView.Fix(w, vocab) ?? w).ToArray();
                if (!fixd.SequenceEqual(words)) { hits = Find(fixd); if (hits.Count > 0) fixedNote = string.Join(" ", fixd); }
            }
            // looks like a site name: offer to check it
            if (words.Length == 1 && q.Contains('.') && !q.EndsWith(".", StringComparison.Ordinal) && q.Length < 120)
            {
                string site = q; var fv = FindForm<FullView>();
                if (fv != null) hits.Add(new PalItem { Group = "Sites", Title = L.T("Check") + " " + site, Sub = L.T("Check a destination"), Icon = "dest", Acc = "dest", Pin = "site:" + site, Go = delegate { fv.CheckSite(site); } });
            }
            if (fixedNote != "") rows.Add(new PalItem { Header = true, Title = L.T("Showing results for") + " \"" + fixedNote + "\"", Acc = "q.keys" });
            foreach (var grp in FullView.SearchGroups)
            {
                var gl = hits.Where(h => h.Group == grp).ToList(); if (gl.Count == 0) continue;
                rows.Add(new PalItem { Header = true, Title = L.T(grp) + "  ·  " + gl.Count, Acc = "" });
                rows.AddRange(gl.Take(grp == "Tools" ? 8 : 5));
            }
            if (rows.Count == 0)
            {
                rows.Add(new PalItem { Header = true, Title = L.T("Nothing by that name"), Acc = "" });
                rows.Add(new PalItem { Title = Buddy.NotFound(), Icon = "mood", Acc = "q.keys" });
                rows.Add(new PalItem { Title = L.T("Browse every tool"), Icon = "tool", Acc = "toolbox", Go = delegate { var fv = FindForm<FullView>(); if (fv != null) fv.OpenNav("toolbox"); } });
            }
            Show(rows);
        }
        List<PalItem> Find(string[] words)
        {
            var hits = toolHits(string.Join(" ", words));
            foreach (var it in all) { int sc = FullView.Score(it, words); if (sc > 0) { it.Rank = sc; hits.Add(it); } }
            return hits.Select((h, i) => new { h, i }).OrderBy(x => Array.IndexOf(FullView.SearchGroups, x.h.Group)).ThenByDescending(x => x.h.Group == "Tools" ? -x.i : x.h.Rank).Select(x => x.h).ToList();
        }
        static T FindForm<T>() where T : Form { return Application.OpenForms.OfType<T>().FirstOrDefault(); }
        void Show(List<PalItem> rows)
        {
            list.BeginUpdate(); list.Items.Clear(); foreach (var r in rows) list.Items.Add(r); list.EndUpdate();
            Step(1);
        }
        void Remember()
        {
            string q = box.Box.Text.Trim().Replace("|", " "); if (q.Length < 2 || q.Length > 60) return;
            var l = (Cfg.Searches ?? "").Split('|').Where(x => x.Trim() != "" && !string.Equals(x, q, StringComparison.OrdinalIgnoreCase)).ToList();
            l.Insert(0, q); Cfg.Searches = string.Join("|", l.Take(6)); Cfg.Save();
        }
        void Pick()
        {
            if (list.SelectedIndex < 0) return; var it = (PalItem)list.Items[list.SelectedIndex]; if (it.Header || it.Go == null) return;
            if (it.Icon == "search") { it.Go(); return; }   // a recent search fills the box
            Remember(); Chosen = it.Go; Close();
        }
        void PinSel()
        {
            if (list.SelectedIndex < 0) return; var it = (PalItem)list.Items[list.SelectedIndex]; if (it.Header || it.Pin == null) return;
            pin(it.Pin); Pinned = it.Pin; Remember();
            foot.Text = L.T("Pinned to your tab") + ": " + it.Title; foot.ForeColor = Kit.Ink("mine");
        }
    }

    // the few plain words when a search finds nothing (the moods take these over in the companion wave)
    static class Buddy
    {
        static readonly string[] Lines = { "Nothing here by that name. Fewer words usually find it.", "Came up empty. Try the thing, not the symptom: \"dns\", \"wifi\", \"port\".", "Not a thing I have. Yet. Try another word?", "Searched every drawer. Try a shorter word." };
        static int last = -1; static readonly Random rnd = new Random();
        public static string NotFound() { int i; do i = rnd.Next(Lines.Length); while (i == last && Lines.Length > 1); last = i; return L.T(Lines[i]); }
    }
}
