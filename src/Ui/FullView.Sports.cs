// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Streaming › Sports: today in sport, as a newspaper page
    // A masthead, the lead story, then one column per sport - its scores box (live in red) and its headlines. The Streaming
    // tab next door is where a stutter during the match gets its verdict (your Wi-Fi, your provider or the stream).
    sealed partial class FullView
    {
        Panel sportsPage; DB sportsCanvas; long sportsVer = -1; int sportsMin = -1; string spTab = "";
        static Font serifHead, serifLead, serifBody, serifSmall; static float serifAt;
        static void SerifFonts()
        {
            if (serifHead != null && serifAt == S) return;   /* rebuilt when the window moves to a screen with another scale */
            foreach (var f in new[] { serifHead, serifLead, serifBody, serifSmall }) if (f != null) f.Dispose(); serifAt = S;
            // Georgia ships with Windows; if a PC lacks it, Windows picks its own serif
            serifHead = new Font("Georgia", 26 * S, FontStyle.Bold, GraphicsUnit.Pixel); serifLead = new Font("Georgia", 22 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            serifBody = new Font("Georgia", 15 * S, FontStyle.Bold, GraphicsUnit.Pixel); serifSmall = new Font("Georgia", 13 * S, FontStyle.Regular, GraphicsUnit.Pixel);
        }
        Panel BuildSportsPage()
        {
            sportsPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            sportsCanvas = new DB { Name = "sportsCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            sportsCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintSports(e.Graphics); }; Wire(sportsCanvas);
            sportsPage.Controls.Add(sportsCanvas);
            return sportsPage;
        }
        // while the page is open: fetch every 5 minutes, repaint when something new came in (and once a minute for "live")
        void SportsTick()
        {
            bool streamingMine = tab == "stream" && Streams.Active && Sports.Teams().Count > 0;
            if ((tab == "sports" || streamingMine) && Cfg.Sports && Sports.Due) Sports.Fetch(() => { try { if (!IsDisposed) BeginInvoke((Action)delegate { if (sportsCanvas != null) sportsCanvas.Invalidate(); if (streamCanvas != null) streamCanvas.Invalidate(); }); } catch (InvalidOperationException) { } });
            SpReminder(); CasualTick();
            if (tab != "sports" || sportsCanvas == null) return;
            long v = System.Threading.Interlocked.Read(ref Sports.Ver) * 1000003 + System.Threading.Interlocked.Read(ref LineHours.Ver); if (v != sportsVer || DateTime.Now.Minute != sportsMin || StreamCheck.Running) { sportsVer = v; sportsMin = DateTime.Now.Minute; sportsCanvas.Invalidate(); }   // a running stream test repaints each tick (its progress)
        }

        // Sports › Headlines: the 1.1.5 newspaper page's body (the lead story, then one column per sport: scores and headlines)
        float PaintSportsHeadlines(Graphics g, float x, float y, float w)
        {
            SerifFonts();
            using (var ink = new SolidBrush(Paper.Ink)) using (var sub = new SolidBrush(Paper.Ink)) using (var muted = new SolidBrush(Paper.Muted))
            {
                if (!Cfg.Sports || Policy.NoOnline)
                {
                    var cr = new RectangleF(x, y, w, 96 * S); Kit.Box(g, cr, 14 * S);
                    g.DrawString(L.T("Today in sport - scores and headlines"), Draw.fBodyB, ink, cr.X + 20 * S, cr.Y + 16 * S);
                    g.DrawString(Policy.NoOnline ? L.T("Off - your IT team turned online lookups off.") : L.T("Off until you turn it on. Then, while this page is open - and while a stream plays on the Streaming page if you follow a team - hockey scores from the NHL, football from ESPN's public scoreboards (or football-data.org with your own free key), cricket with your own free key, and BBC Sport's headlines, every 5 minutes at most."), Draw.fSmall, sub, new RectangleF(cr.X + 20 * S, cr.Y + 40 * S, cr.Width - 200 * S, 50 * S));
                    if (!Policy.NoOnline)
                    {
                        string bt = L.T("Turn on"); var bs = g.MeasureString(bt, Draw.fBtn); var br = new RectangleF(cr.Right - bs.Width - 44 * S, cr.Y + 32 * S, bs.Width + 24 * S, 32 * S);
                        Theme.Fill(g, br, 10 * S, Kit.Accent("stream")); using (var wb = new SolidBrush(Theme.OnBlue)) g.DrawString(bt, Draw.fBtn, wb, br.X + 12 * S, br.Y + (32 * S - bs.Height) / 2);
                        Act(sportsCanvas, br, delegate { Cfg.Sports = true; Cfg.Save(); sportsCanvas.Invalidate(); });
                    }
                    return cr.Bottom + 30 * S;
                }
                List<Sports.Game> games; List<Sports.Story> stories; Dictionary<string, string> notes; DateTime at;
                lock (Sports.k) { games = Sports.Games.ToList(); stories = Sports.Stories.ToList(); notes = new Dictionary<string, string>(Sports.Note); at = Sports.FetchedAt; }
                if (at == DateTime.MinValue) { g.DrawString(L.T("Fetching today's scores and headlines..."), Draw.fBody, sub, x, y); return y + 60 * S; }
                // your teams, first
                var mine = games.Where(Sports.Mine).OrderBy(z => z.State == "live" ? 0 : z.State == "soon" ? 1 : 2).ToList();
                if (mine.Count > 0)
                {
                    g.DrawString(Kit.Sent("YOUR TEAMS"), Draw.fTiny, muted, x, y); y += 18 * S; float cx = x;
                    foreach (var m in mine.Take(6))
                    {
                        string t = m.Away + " " + Or(m.AwayScore, "") + "  ·  " + m.Home + " " + Or(m.HomeScore, "") + (m.State == "live" ? "  " + Kit.Sent("LIVE") : m.State == "soon" ? "  " + m.StartUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture) : "  " + m.Detail);
                        var ts = g.MeasureString(t, Draw.fBodyB); if (cx + ts.Width + 30 * S > x + w) { cx = x; y += 40 * S; }
                        var rr = new RectangleF(cx, y, ts.Width + 26 * S, 32 * S); Theme.Fill(g, rr, 16 * S, m.State == "live" ? Theme.LvSoft(2) : Theme.Soft);
                        using (var b = new SolidBrush(m.State == "live" ? Theme.LvText(2) : Theme.Ink)) g.DrawString(t, Draw.fBodyB, b, rr.X + 13 * S, rr.Y + (32 * S - ts.Height) / 2);
                        cx += rr.Width + 10 * S;
                    }
                    y += 48 * S;
                }
                // the lead story (AM-CG-279: a headline feed that failed says so, it doesn't just look empty)
                string topNote; if (notes.TryGetValue("news-top", out topNote)) { g.DrawString(L.T("BBC Sport's top stories didn't load") + " - " + L.T(topNote) + (stories.Any(s => s.Sport == "top") ? " · " + L.T("showing the last ones") : ""), Draw.fSmall, muted, new RectangleF(x, y, w, 20 * S)); y += 22 * S; }
                var lead = stories.FirstOrDefault(s => s.Sport == "top");
                if (lead != null)
                {
                    var lh = g.MeasureString(lead.Title, serifLead, (int)w); g.DrawString(lead.Title, serifLead, ink, new RectangleF(x, y, w, lh.Height + 4 * S));
                    var lr = new RectangleF(x, y, w, lh.Height); Act(sportsCanvas, lr, OpenLink(lead.Link)); y += lh.Height + 4 * S;
                    var sh = g.MeasureString(lead.Summary, serifSmall, (int)w); g.DrawString(lead.Summary, serifSmall, sub, new RectangleF(x, y, w, sh.Height + 2 * S)); y += sh.Height + 4 * S;
                    using (var lb = new SolidBrush(Kit.Ink("stream"))) g.DrawString(L.T("Read at BBC Sport ›"), Draw.fSmall, lb, x, y); Act(sportsCanvas, new RectangleF(x, y, 160 * S, 18 * S), OpenLink(lead.Link)); y += 26 * S;
                    using (var p = new Pen(Theme.Line, 1)) g.DrawLine(p, x, y, x + w, y); y += 14 * S;
                }
                // one column per sport (stacked when the window is narrow)
                int cols = w > 820 * S ? 3 : 1; float colW = (w - (cols - 1) * 24 * S) / cols; float top = y, bottom = y;
                for (int i = 0; i < Sports.Kinds.Length; i++)
                {
                    string sp = Sports.Kinds[i]; float cx = x + (cols == 3 ? i * (colW + 24 * S) : 0), cy = cols == 3 ? top : bottom;
                    if (cols == 3 && i > 0) using (var p = new Pen(Theme.Line, 1)) g.DrawLine(p, cx - 12 * S, top, cx - 12 * S, top + 600 * S);
                    g.DrawString(L.T(Sports.Name(sp)), serifBody, ink, cx, cy); cy += 26 * S;
                    // the scores box
                    var gs = games.Where(z => z.Sport == sp).OrderBy(z => z.State == "live" ? 0 : z.State == "soon" ? 2 : 1).ThenBy(z => z.StartUtc).Take(8).ToList();
                    string note; notes.TryGetValue(sp, out note);
                    if (sp == "football" && Sports.FromEspn && note == null)
                    {   // the free default: ESPN, once a day - and how to get live scores
                        g.DrawString(L.T("From ESPN's free scoreboards, updated once a day."), Draw.fSmall, sub, new RectangleF(cx, cy, colW, 20 * S)); cy += 22 * S;
                        string em = Sports.EspnMissing; if (em != "") { using (var ab = new SolidBrush(Theme.LvText(1))) g.DrawString(em, Draw.fSmall, ab, new RectangleF(cx, cy, colW, 36 * S)); cy += Draw.Measure(em, Draw.fSmall, (int)colW).Height + 4 * S; }   // R2-045
                        var ekr = MkSpButton(g, L.T("Live scores: add my key..."), cx, cy, false); string spk = sp; Act(sportsCanvas, ekr, delegate { AskSportKey(spk); }); cy += 48 * S;
                    }
                    if (note == "nokey")
                    {
                        g.DrawString(L.T("Scores need your own free key from ") + (sp == "cricket" ? "cricketdata.org" : "football-data.org") + ".", Draw.fSmall, sub, new RectangleF(cx, cy, colW, 36 * S)); cy += 38 * S;
                        var br = MkSpButton(g, L.T("Add my key..."), cx, cy, false);   // the paper page's own button (it was the app's dark card: black on the paper)
                        string spc = sp; Act(sportsCanvas, br, delegate { AskSportKey(spc); }); cy += 48 * S;
                    }
                    else if (note != null) { g.DrawString(Cut(note, 120), Draw.fSmall, sub, new RectangleF(cx, cy, colW, 36 * S)); cy += 38 * S; }
                    else if (gs.Count == 0) { g.DrawString(L.T("No games today."), Draw.fSmall, muted, cx, cy); cy += 24 * S; }
                    foreach (var m in gs)
                    {
                        var row = new RectangleF(cx, cy, colW, 40 * S); bool live = m.State == "live";
                        if (live) Draw.LvDot(g, 2, cx, cy + 6 * S, 8 * S, 8 * S);
                        using (var nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                        {
                            g.DrawString(m.Away, Draw.fBodyB, ink, new RectangleF(cx + 14 * S, cy, colW * 0.62f, 18 * S), nf); g.DrawString(m.Home, Draw.fBodyB, ink, new RectangleF(cx + 14 * S, cy + 18 * S, colW * 0.62f, 18 * S), nf);
                            using (var sb = new SolidBrush(live ? Theme.LvText(2) : Theme.Ink)) { g.DrawString(Or(m.AwayScore, "-"), Draw.fBodyB, sb, new RectangleF(cx + colW * 0.64f, cy, colW * 0.36f, 18 * S), nf); g.DrawString(Or(m.HomeScore, "-"), Draw.fBodyB, sb, new RectangleF(cx + colW * 0.64f, cy + 18 * S, colW * 0.36f, 18 * S), nf); }
                        }
                        string st = live ? Kit.Sent("LIVE") + (m.Detail != "" ? " · " + Cut(m.Detail, 50) : "") : m.State == "off" ? OffWord(m) : m.State == "soon" ? m.StartUtc == DateTime.MinValue ? "" : m.StartUtc.ToLocalTime().ToString("ddd t", CultureInfo.CurrentCulture) : Cut(m.Detail, 60);
                        if (m.League != "") st = m.League + (st != "" ? " · " + st : "");
                        using (var sb = new SolidBrush(live ? Theme.LvText(2) : Theme.Muted)) g.DrawString(Cut(st, 70), Draw.fTiny, sb, cx + 14 * S, cy + 37 * S);
                        cy += 58 * S;
                    }
                    // the headlines
                    cy += 6 * S; using (var p = new Pen(Theme.Line, 1)) g.DrawLine(p, cx, cy, cx + colW, cy); cy += 10 * S;
                    foreach (var s in stories.Where(z => z.Sport == sp).Take(4))
                    {
                        var th = g.MeasureString(s.Title, serifBody, (int)colW); g.DrawString(s.Title, serifBody, ink, new RectangleF(cx, cy, colW, th.Height + 2 * S)); Act(sportsCanvas, new RectangleF(cx, cy, colW, th.Height), OpenLink(s.Link)); cy += th.Height + 2 * S;
                        var sm = g.MeasureString(Cut(s.Summary, 160), serifSmall, (int)colW); g.DrawString(Cut(s.Summary, 160), serifSmall, sub, new RectangleF(cx, cy, colW, sm.Height + 2 * S)); cy += sm.Height + 12 * S;
                    }
                    string newsNote; if (notes.TryGetValue("news-" + sp, out newsNote)) { g.DrawString(L.T("Headlines didn't load") + " - " + L.T(newsNote), Draw.fTiny, muted, new RectangleF(cx, cy, colW, 30 * S)); cy += 24 * S; }
                    if (sp == "hockey" && !stories.Any(z => z.Sport == "hockey")) { g.DrawString(L.T("Hockey headlines aren't in BBC Sport's feeds - scores only."), Draw.fTiny, muted, new RectangleF(cx, cy, colW, 30 * S)); cy += 30 * S; }
                    bottom = Math.Max(bottom, cy); if (cols == 1) bottom = cy + 20 * S;
                }
                y = bottom + 10 * S;
                // follow teams, the key, and the link to Streaming
                using (var p = new Pen(Theme.Ink, 1)) g.DrawLine(p, x, y, x + w, y); y += 14 * S;
                string tl = Sports.Teams().Count == 0 ? L.T("Follow your teams - they go to the top, and the Streaming tab shows their live score while you watch.") : L.T("Following:") + " " + string.Join(", ", Sports.Teams().ToArray());
                g.DrawString(tl, Draw.fSmall, sub, new RectangleF(x, y, w - 220 * S, 36 * S));
                string eb = L.T("Teams..."); var es = g.MeasureString(eb, Draw.fBtn); var er = new RectangleF(x + w - es.Width - 20 * S, y - 4 * S, es.Width + 20 * S, 30 * S); Kit.Box(g, er, 10 * S);
                g.DrawString(eb, Draw.fBtn, ink, er.X + 10 * S, er.Y + (30 * S - es.Height) / 2); Act(sportsCanvas, er, AskTeams); y += 40 * S;
                g.DrawString(L.T("Updated") + " " + at.ToLocalTime().ToString("t", CultureInfo.CurrentCulture) + " · " + L.T("every 5 minutes while this page is open · cricket at most 90 calls a day (a free key allows 100) · keys are stored encrypted for your Windows account, never in the settings file") + " · ", Draw.fTiny, muted, new RectangleF(x, y, w, 34 * S));
                y += 36 * S;
                string kb = L.T("Change my keys..."); var ks = g.MeasureString(kb, Draw.fSmall); var kr = new RectangleF(x, y, ks.Width + 4 * S, 20 * S);
                using (var lb = new SolidBrush(Kit.Ink("stream"))) g.DrawString(kb, Draw.fSmall, lb, kr.X, kr.Y); Act(sportsCanvas, kr, delegate { var mm = Menus.Make(); mm.Items.Add(L.T("Football key (football-data.org)..."), null, delegate { AskSportKey("football"); }); mm.Items.Add(L.T("Cricket key (cricketdata.org)..."), null, delegate { AskSportKey("cricket"); }); mm.Items.Add(L.T("Turn Sports off"), null, delegate { Cfg.Sports = false; Cfg.Save(); sportsCanvas.Invalidate(); }); mm.Closed += delegate { BeginInvoke((Action)mm.Dispose); }; mm.Show(Cursor.Position); });
                y += 30 * S;
            }
            return y + 20 * S;
        }
        static Action OpenLink(string url) { return delegate { if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) Crash.OpenUrl(url); }; }
        // the key: typed by the person, masked, kept encrypted (Sports.SetKey); empty = remove it
        void AskSportKey(string sport)
        {
            using (var f = new Form { Text = AdeeboMeter.L.T("AdeeboMeter - ") + L.T(Sports.Name(sport)) + " " + L.T("key"), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(Sc(460), Sc(150)), BackColor = Theme.Card, Font = Draw.fBody, ShowInTaskbar = false, Icon = Theme.AppIcon })
            {
                var lb = new Label { Text = L.T("Paste your free key from ") + (sport == "cricket" ? "cricketdata.org" : "football-data.org") + L.T(". It stays on this PC, encrypted. Leave empty to remove it."), Location = new Point(Sc(14), Sc(12)), Size = new Size(Sc(430), Sc(44)), ForeColor = Theme.Sub };
                var tb = new TextBox { Location = new Point(Sc(14), Sc(62)), Width = Sc(430), UseSystemPasswordChar = true };
                var ok = new Button { Text = L.T("Save"), DialogResult = DialogResult.OK, Location = new Point(Sc(274), Sc(104)), Size = new Size(Sc(80), Sc(30)) }; var cancel = new Button { Text = L.T("Cancel"), DialogResult = DialogResult.Cancel, Location = new Point(Sc(364), Sc(104)), Size = new Size(Sc(80), Sc(30)) };
                f.Controls.AddRange(new Control[] { lb, tb, ok, cancel }); f.AcceptButton = ok; f.CancelButton = cancel;
                if (f.ShowDialog(this) == DialogResult.OK) { if (!Sports.SetKey(sport, tb.Text)) AskCard.Show(this, L.T("The key couldn't be saved - check that AdeeboMeter's data folder isn't read-only, then try again."), "AdeeboMeter", MessageBoxButtons.OK, MessageBoxIcon.Warning); Sports.Fetch(() => { try { if (!IsDisposed) BeginInvoke((Action)delegate { sportsCanvas.Invalidate(); }); } catch (InvalidOperationException) { } }); }
                tb.Text = "";
            }
        }
        void AskTeams()
        {
            AskTeams(!Cfg.Sports);
        }
        // B2 (review 1.1.6): when scores are off, the box says what turning them on sends, and only Save turns them on - Cancel leaves them off
        void AskTeams(bool turnOn)
        {
            using (var f = new Form { Text = AdeeboMeter.L.T("AdeeboMeter - ") + L.T("teams you follow"), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(Sc(460), Sc(turnOn ? 214 : 150)), BackColor = Theme.Card, Font = Draw.fBody, ShowInTaskbar = false, Icon = Theme.AppIcon })
            {
                var lb = new Label { Text = L.T("Names or short codes, separated by commas - e.g. TOR, MTL, Arsenal, India") + (turnOn ? "\n\n" + L.T("Saving turns on scores: AdeeboMeter then asks the NHL, ESPN and BBC Sport for the day's fixtures, about every 5 minutes while the Sports page is open. Nothing about you is sent.") : ""), Location = new Point(Sc(14), Sc(12)), Size = new Size(Sc(430), Sc(turnOn ? 108 : 44)), ForeColor = Theme.Sub };
                var tb = new TextBox { Location = new Point(Sc(14), Sc(turnOn ? 126 : 62)), Width = Sc(430), Text = Cfg.SportTeams ?? "" };
                var ok = new Button { Text = L.T("Save"), DialogResult = DialogResult.OK, Location = new Point(Sc(274), Sc(turnOn ? 168 : 104)), Size = new Size(Sc(80), Sc(30)) }; var cancel = new Button { Text = L.T("Cancel"), DialogResult = DialogResult.Cancel, Location = new Point(Sc(364), Sc(turnOn ? 168 : 104)), Size = new Size(Sc(80), Sc(30)) };
                f.Controls.AddRange(new Control[] { lb, tb, ok, cancel }); f.AcceptButton = ok; f.CancelButton = cancel;
                if (f.ShowDialog(this) == DialogResult.OK) { if (turnOn && !Policy.NoOnline) Cfg.Sports = true; Cfg.SportTeams = string.Join(", ", tb.Text.Split(',').Select(t => t.Trim()).Where(t => t.Length >= 2 && t.Length <= 40).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray()); Cfg.Save(); sportsCanvas.Invalidate(); }
            }
        }
    }
}
