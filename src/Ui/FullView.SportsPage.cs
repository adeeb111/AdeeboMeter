// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Sports (1.1.6): THE SPORTS PAGE
    // Overview (the matchday briefing for your next game), My Teams, Live Scores, Fixtures, Headlines and - highlighted -
    // Stream Check, the part AdeeboMeter itself can judge: will your stream play, and how to make it play better. The Stream
    // Check strip sits under every tab. Scores and headlines need Sports turned on (and keys for football/cricket); the
    // stream check only ever talks to the stream's own server, when the person presses Test.
    sealed partial class FullView
    {
        static readonly string[][] SpTabs = { new[] { "overview", "Overview" }, new[] { "teams", "My Teams" }, new[] { "live", "Live Scores" }, new[] { "fixtures", "Fixtures" }, new[] { "headlines", "Headlines" }, new[] { "stream", "Stream Check", "hl" } };
        TextBox spLink; string spFilter = "all"; bool spMineOnly;

        void PaintSports(Graphics g)
        {
            if (spTab == "" || !SpTabs.Any(t => t[0] == spTab)) spTab = "overview";
            Paper.Ground(g, new RectangleF(0, 0, sportsCanvas.ClientSize.Width, sportsCanvas.ClientSize.Height));
            g.TranslateTransform(sportsCanvas.AutoScrollPosition.X, sportsCanvas.AutoScrollPosition.Y);
            Begin(sportsCanvas); Paper.Fonts();
            float x = 22 * S, y = 16 * S, w = sportsCanvas.ClientSize.Width - 44 * S;
            y = Paper.Masthead(g, x, y, w, L.T("THE SPORTS PAGE"), L.T("AdeeboMeter Matchday Edition"), L.T("Your PC · Your Network · Your Streaming"), L.T("All in One Place"), spTab != "overview");
            List<Tuple<string, RectangleF>> hits; y = Paper.TabBar(g, x, y, w, SpTabs, spTab, out hits);
            foreach (var h in hits) { string id = h.Item1; Act(sportsCanvas, h.Item2, delegate { spTab = id; sportsCanvas.AutoScrollPosition = Point.Empty; sportsCanvas.Invalidate(); }); }
            bool linkShown = false;
            if (spTab != "stream") y = StreamStrip(g, x, y, w);
            y = TryThis(g, sportsCanvas, "sports", x, y, w, true);
            switch (spTab)
            {
                case "teams": y = SpTeams(g, x, y, w); break;
                case "live": y = SpScores(g, x, y, w, true); break;
                case "fixtures": y = SpScores(g, x, y, w, false); break;
                case "headlines": y = SpNeedsOn(g, x, y, w) ? PaintSportsHeadlines(g, x, y, w) : y + 140 * S; break;
                case "stream": y = SpStream(g, x, y, w); linkShown = true; break;
                default: y = SpOverview(g, x, y, w); linkShown = true; break;
            }
            if (!linkShown && spLink != null) spLink.Visible = false;
            Need(sportsCanvas, y + 20 * S);
        }
        // scores and headlines are off until turned on: say so on the tabs that need them (the stream check doesn't)
        bool SpNeedsOn(Graphics g, float x, float y, float w)
        {
            if (Cfg.Sports && !Policy.NoOnline) return true;
            var r = new RectangleF(x, y, w, 120 * S); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 18 * S, y + 14 * S, "Scores and headlines", null);
            using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(Policy.NoOnline ? L.T("Off - your IT team turned online lookups off.") : L.T("Off until you turn it on. Then, while this page is open: hockey scores from the NHL, football from ESPN's public scoreboards (or football-data.org with your own free key), cricket with your own free key, and BBC Sport's headlines - every 5 minutes at most."), Paper.Body, ib, new RectangleF(x + 18 * S, y + 50 * S, w - 220 * S, 60 * S));
            if (!Policy.NoOnline) { var br = Paper.Pill(g, L.T("Turn on"), x + w - 90 * S, y + 46 * S, Paper.Green); Act(sportsCanvas, br, delegate { Cfg.Sports = true; Cfg.Save(); sportsCanvas.Invalidate(); }); }
            return false;
        }
        Action SpRepaint() { return () => { try { if (!IsDisposed) BeginInvoke((Action)delegate { if (sportsCanvas != null) sportsCanvas.Invalidate(); }); } catch (Exception e) { if (!(e is InvalidOperationException || e is ObjectDisposedException)) throw; } }; }

        // ---------- the Stream Check strip (every tab but its own)
        float StreamStrip(Graphics g, float x, float y, float w)
        {
            var r = StreamCheck.Last; string verdict, sub, tip = null; int lv;
            if (StreamCheck.Running) { verdict = L.T(StreamCheck.Step) + "..."; sub = L.T("about 30 seconds"); lv = 3; }
            else if (r == null) { verdict = L.T("Is your stream ready?"); sub = L.T("Paste your stream link and test it: which quality will play, and how to make it better."); lv = 3; }
            else
            {
                verdict = L.T(r.Verdict); lv = r.Score < 0 ? 3 : r.Score >= 80 ? 0 : r.Score >= 55 ? 1 : 2;
                sub = L.T("Tested") + " " + r.When.ToString("t", CultureInfo.CurrentCulture) + (double.IsNaN(r.Mbps) ? "" : " · " + L.T("video came in at") + " " + r.Mbps.ToString("0.0", CultureInfo.CurrentCulture) + " Mb/s") + (r.Best != null ? " · " + StreamCheck.Short(r.Best) + " " + L.T("needs") + " " + (r.Best.Bw / 1e6).ToString("0.0", CultureInfo.CurrentCulture) : "");
                var t = r.Tips.FirstOrDefault(); if (t != null) tip = L.T(t.Title);
            }
            Paper.StripHits sh; y = Paper.Strip(g, x, y, w, L.T("STREAM CHECK"), false, verdict, sub, tip, L.T("Test my stream"), lv, out sh);
            Act(sportsCanvas, sh.Button, delegate { spTab = "stream"; sportsCanvas.AutoScrollPosition = Point.Empty; sportsCanvas.Invalidate(); var links = StreamCheck.Links(); if (links.Count > 0 && !StreamCheck.Running) StreamCheck.Start(links[0][1], M, SpRepaint()); });
            Act(sportsCanvas, sh.Details, delegate { spTab = "stream"; sportsCanvas.AutoScrollPosition = Point.Empty; sportsCanvas.Invalidate(); });
            return y;
        }
        // the link box: a real text box on the canvas, moved with the page
        float SpLinkBox(Graphics g, float x, float y, float w)
        {
            if (spLink == null)
            {
                spLink = new TextBox { Font = Draw.fInput, MaxLength = 2000, BorderStyle = BorderStyle.FixedSingle };
                spLink.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SpTest(); } };
                sportsCanvas.Controls.Add(spLink);
                var l = StreamCheck.Links(); if (l.Count > 0) spLink.Text = l[0][1];
                // screenshots and testers: --view sports:stream --dest <stream link> starts the check on open
                if (spTab == "stream" && Program.StartDest != null && Program.StartDest.StartsWith("http", StringComparison.OrdinalIgnoreCase)) { spLink.Text = Program.StartDest; Program.StartDest = null; BeginInvoke((Action)SpTest); }
            }
            float bw = 150 * S, pw = 90 * S; var box = new Rectangle((int)x, (int)(y + sportsCanvas.AutoScrollPosition.Y), (int)(w - bw - pw - 20 * S), spLink.PreferredHeight);
            if (spLink.Bounds != box) spLink.Bounds = box;
            if (!spLink.Visible) spLink.Visible = true;
            spLink.BackColor = Paper.Dark ? Paper.Soft : Color.White; spLink.ForeColor = Paper.Ink;
            var pr = new RectangleF(x + w - bw - pw - 10 * S, y - 2 * S, pw, box.Height + 4 * S); Theme.Fill(g, pr, 6 * S, Paper.DeskBg); Theme.Stroke(g, pr, 6 * S, Paper.Line, 1);
            var ps = g.MeasureString(L.T("Paste"), Draw.fBtn); using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(L.T("Paste"), Draw.fBtn, ib, pr.X + (pr.Width - ps.Width) / 2, pr.Y + (pr.Height - ps.Height) / 2);
            Act(sportsCanvas, pr, delegate { try { if (Clipboard.ContainsText()) spLink.Text = Clipboard.GetText().Trim(); } catch (System.Runtime.InteropServices.ExternalException) { } });
            var tr = new RectangleF(x + w - bw, y - 2 * S, bw, box.Height + 4 * S); Theme.Fill(g, tr, 6 * S, StreamCheck.Running ? Paper.Muted : Paper.Green);
            string tt = StreamCheck.Running ? L.T("Testing...") : L.T("Test my stream"); var ts = g.MeasureString(tt, Draw.fBtn); using (var wb = new SolidBrush(Color.White)) g.DrawString(tt, Draw.fBtn, wb, tr.X + (tr.Width - ts.Width) / 2, tr.Y + (tr.Height - ts.Height) / 2);
            Act(sportsCanvas, tr, SpTest);
            return y + box.Height + 14 * S;
        }
        void SpTest()
        {
            if (spLink == null || StreamCheck.Running) return; string url = spLink.Text.Trim(); Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u) || (u.Scheme != "http" && u.Scheme != "https")) { AskCard.Show(this, L.T("Paste a web address that starts with http:// or https:// - the stream's own link (ending in .m3u8 or .mpd) gives the full check."), "AdeeboMeter", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (Policy.NoOnline) { AskCard.Show(this, L.T("Online checks are turned off by your IT team."), "AdeeboMeter", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            StreamCheck.SaveLink(StreamCheck.LabelFor(url), url);
            StreamCheck.Start(url, M, SpRepaint()); sportsCanvas.Invalidate();
        }

        // ---------- the next game for your teams (live first, then the soonest), or null
        Sports.Game SpNext(List<Sports.Game> games)
        {
            var mine = games.Where(Sports.Mine).ToList();
            return mine.FirstOrDefault(z => z.State == "live") ?? mine.Where(z => z.State == "soon" && z.StartUtc > DateTime.UtcNow.AddHours(-3)).OrderBy(z => z.StartUtc).FirstOrDefault();
        }
        static Color TeamColor(string name)
        {
            var pal = new[] { Color.FromArgb(200, 16, 46), Color.FromArgb(3, 70, 148), Color.FromArgb(0, 106, 78), Color.FromArgb(108, 29, 69), Color.FromArgb(241, 120, 0), Color.FromArgb(40, 40, 40), Color.FromArgb(0, 122, 194), Color.FromArgb(122, 38, 58) };
            int h = 0; foreach (char c in name ?? "") h = h * 31 + c; return pal[Math.Abs(h) % pal.Length];
        }
        static string Code(string name) { var n = new string((name ?? "").Where(char.IsLetter).ToArray()).ToUpperInvariant(); return n.Length <= 3 ? n : n.Substring(0, 3); }
        void SpBadge(Graphics g, float cx, float y, string name, string side)
        {
            var r = new RectangleF(cx - 34 * S, y, 68 * S, 68 * S); using (var b = new SolidBrush(TeamColor(name))) g.FillEllipse(b, r);
            using (var p = new Pen(Color.FromArgb(242, 201, 76), 2 * S)) g.DrawEllipse(p, r.X + 6 * S, r.Y + 6 * S, r.Width - 12 * S, r.Height - 12 * S);
            string c = Code(name); var cs = g.MeasureString(c, Paper.BodyB); using (var wb = new SolidBrush(Color.White)) g.DrawString(c, Paper.BodyB, wb, cx - cs.Width / 2, y + 34 * S - cs.Height / 2);
            var ns = g.MeasureString(name, Paper.Head3); using (var hb = new SolidBrush(Paper.Head)) g.DrawString(name, Paper.Head3, hb, cx - ns.Width / 2, y + 74 * S);
        }

        // ---------- Overview: the matchday briefing, your setup for it, the stream, your line at kick-off, before kick-off
        float SpOverview(Graphics g, float x, float y, float w)
        {
            List<Sports.Game> games; lock (Sports.k) games = Sports.Games.ToList();
            var next = Cfg.Sports ? SpNext(games) : null;
            float lw = w * 0.56f, rx = x + lw + 16 * S, rw = w - lw - 16 * S;
            float ly = Paper.Ribbon(g, L.T("MATCHDAY BRIEFING"), x, y) + 10 * S;
            using (var hb = new SolidBrush(Paper.Head)) using (var ib = new SolidBrush(Paper.Ink))
            {
                if (next != null)
                {
                    string title = next.Home + " " + L.T("vs") + " " + next.Away; var ts = g.MeasureString(title, Paper.Head1, (int)(lw - 200 * S));
                    g.DrawString(title, Paper.Head1, hb, new RectangleF(x, ly, lw - 200 * S, ts.Height + 4)); float ty = ly + ts.Height + 6 * S;
                    g.DrawString(L.T("Your match, your setup and your stream - all in one place."), Paper.Italic, ib, new RectangleF(x, ty, lw - 200 * S, 50 * S));
                    SpBadge(g, x + lw - 150 * S, ly, next.Home, "home"); SpBadge(g, x + lw - 50 * S, ly, next.Away, "away");
                    ly = Math.Max(ty + 30 * S, ly + 110 * S);
                }
                else
                {
                    g.DrawString(Cfg.Sports ? (Sports.Teams().Count == 0 ? L.T("Follow your teams") : L.T("No game today for your teams")) : L.T("Your stream, ready for the match"), Paper.Head1, hb, x, ly); ly += 56 * S;
                    g.DrawString(!Cfg.Sports ? L.T("Turn on scores to see your teams' next game here.") : Sports.Teams().Count == 0 ? L.T("Their next game becomes this page's lead, with everything you need to watch it.") : L.T("Their next game shows here as soon as it's in today's fixtures."), Paper.Italic, ib, new RectangleF(x, ly, lw, 50 * S)); ly += 40 * S;
                    var fb = MkSpButton(g, Sports.Teams().Count == 0 ? L.T("Follow teams...") : L.T("My teams ›"), x, ly, true); Act(sportsCanvas, fb, delegate { if (!Cfg.Sports) { Cfg.Sports = true; Cfg.Save(); } if (Sports.Teams().Count == 0) AskTeams(); else spTab = "teams"; sportsCanvas.Invalidate(); }); ly += 50 * S;
                }
            }
            // match details
            var dr = new RectangleF(rx, y, rw, 170 * S); Paper.Desk(g, dr);
            float dy = Paper.DeskTitle(g, rx + 16 * S, y + 12 * S, "Match Details", null) + 6 * S;
            var rows = next == null ? new[] { new[] { L.T("Next game"), Cfg.Sports ? L.T("none in today's fixtures") : L.T("scores are off") } } : new[] {
                new[] { L.T("Competition:"), next.League == "" ? L.T(Sports.Name(next.Sport)) : next.League },
                new[] { L.T("Kick-off (your time):"), next.State == "live" ? L.T("playing now") + (next.Detail != "" ? " · " + next.Detail : "") : next.StartUtc.ToLocalTime().ToString("ddd h:mm tt", CultureInfo.CurrentCulture) },
                new[] { L.T("Starts in:"), next.State == "live" ? L.T("it's on") : SpUntil(next.StartUtc.ToLocalTime()) },
                new[] { L.T("Score:"), next.State == "live" ? next.Home + " " + next.HomeScore + " - " + next.AwayScore + " " + next.Away : "-" } };
            foreach (var rw2 in rows) { MkSpText(g, rw2[0], Paper.BodyB, Paper.Head, rx + 16 * S, dy); MkSpText(g, rw2[1], Paper.Body, Paper.Ink, rx + rw * 0.45f, dy); dy += 27 * S; }
            y = Math.Max(ly, dr.Bottom) + 16 * S;

            // weather at kick-off (your place) | your network | this PC
            float tw = (w - 32 * S) / 3; DateTime ko = next == null ? DateTime.Now : next.State == "live" ? DateTime.Now : next.StartUtc.ToLocalTime();
            float b1 = SpWeatherDesk(g, x, y, tw, ko), b2 = SpNetDesk(g, x + tw + 16 * S, y, tw), b3 = SpPcDesk(g, x + 2 * (tw + 16 * S), y, tw);
            y = Math.Max(b1, Math.Max(b2, b3));

            // the stream: the link box, the last result
            var sr = new RectangleF(x, y, w, 190 * S); Paper.Desk(g, sr);
            float sy = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "IPTV / Streaming Readiness", L.T("HLS (.m3u8), DASH (.mpd) or a web page · the link stays on this PC")) + 8 * S;
            sy = SpLinkBox(g, x + 16 * S, sy, w * 0.55f - 16 * S);
            var last = StreamCheck.Last;
            var rr = new RectangleF(x + w * 0.57f, y + 14 * S, w * 0.43f - 16 * S, sr.Height - 28 * S);
            if (last != null)
            {
                int lv = last.Score < 0 ? 3 : last.Score >= 80 ? 0 : last.Score >= 55 ? 1 : 2; Theme.Fill(g, rr, 6 * S, lv == 0 ? Paper.GoodSoft : Paper.Soft); Theme.Stroke(g, rr, 6 * S, Paper.Lv(lv), 1.4f);
                Paper.Tick(g, new RectangleF(rr.X + 14 * S, rr.Y + 14 * S, 34 * S, 34 * S), Paper.Lv(lv));
                MkSpText(g, L.T(last.Verdict), Paper.Head2, Paper.Head, rr.X + 58 * S, rr.Y + 12 * S); MkSpText(g, L.T("Last test:") + " " + last.When.ToString("g", CultureInfo.CurrentCulture), Paper.Small, Paper.Muted, rr.X + 58 * S, rr.Y + 42 * S);
                var info = new[] { new[] { L.T("Detected type:"), last.Kind == "page" ? L.T("web page") : last.Kind }, new[] { L.T("Server:"), double.IsNaN(last.ServerMs) ? "-" : L.T("online") + " (" + last.ServerMs.ToString("0", CultureInfo.CurrentCulture) + " ms)" }, new[] { L.T("Secure:"), last.Secure ? L.T("yes (HTTPS)") : L.T("no") }, new[] { L.T("Video came in at:"), double.IsNaN(last.Mbps) ? "-" : last.Mbps.ToString("0.0", CultureInfo.CurrentCulture) + " Mb/s" } };
                float iy = rr.Y + 72 * S; for (int i = 0; i < info.Length; i++) { float ix = rr.X + 14 * S + (i % 2) * rr.Width / 2; MkSpText(g, info[i][0] + " " + info[i][1], Paper.Small, Paper.Ink, ix, iy + (i / 2) * 22 * S); }
                var db = MkSpButton(g, L.T("Details") + " ›", rr.Right - 110 * S, rr.Bottom - 46 * S, false); Act(sportsCanvas, db, delegate { spTab = "stream"; sportsCanvas.Invalidate(); });
            }
            else { Theme.Fill(g, rr, 6 * S, Paper.Soft); using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(L.T("Paste your stream link and press Test my stream. In about 30 seconds you'll know which quality will play smoothly - and what to change if it won't."), Paper.Body, ib, new RectangleF(rr.X + 16 * S, rr.Y + 16 * S, rr.Width - 32 * S, rr.Height - 32 * S)); }
            y = sr.Bottom + 16 * S;

            // your line at kick-off time (same weekday, last 6 weeks) | before kick-off
            float lw2 = w * 0.58f;
            float c1 = SpSlotDesk(g, x, y, lw2, ko), c2 = SpChecklist(g, x + lw2 + 16 * S, y, w - lw2 - 16 * S, next);
            return Math.Max(c1, c2);
        }
        static string SpUntil(DateTime t) { var s = t - DateTime.Now; if (s.TotalMinutes < 1) return L.T("now"); return (s.Days > 0 ? s.Days + " " + L.T("d") + " " : "") + (s.Hours > 0 ? s.Hours + " " + L.T("h") + " " : "") + s.Minutes + " " + L.T("min"); }
        void MkSpText(Graphics g, string t, Font f, Color c, float x, float y) { using (var b = new SolidBrush(c)) g.DrawString(t, f, b, x, y); }
        RectangleF MkSpButton(Graphics g, string t, float x, float y, bool primary)
        {
            var s = g.MeasureString(t, Draw.fBtn); var r = new RectangleF(x, y, s.Width + 28 * S, 38 * S);
            if (primary) Theme.Fill(g, r, 6 * S, Paper.Navy); else { Theme.Fill(g, r, 6 * S, Paper.DeskBg); Theme.Stroke(g, r, 6 * S, Paper.Line, 1); }
            MkSpText(g, t, Draw.fBtn, primary ? Color.White : Paper.Ink, r.X + 14 * S, r.Y + (r.Height - s.Height) / 2); return r;
        }
        float SpWeatherDesk(Graphics g, float x, float y, float w, DateTime at)
        {
            var r = new RectangleF(x, y, w, 196 * S); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Weather at Kick-off", Cfg.WeatherOn && Cfg.WeatherPlace != "" ? "(" + Weather.PlaceName() + ")" : null);
            var h = Weather.HourAt(at.ToUniversalTime());
            if (!Cfg.WeatherOn || h == null) { using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(L.T("Turn on Weather in Settings to see the weather for your place at kick-off."), Paper.Body, ib, new RectangleF(x + 16 * S, y + 56 * S, w - 32 * S, 80 * S)); return r.Bottom + 16 * S; }
            Paper.WeatherGlyph(g, h.Code, new RectangleF(x + 16 * S, y + 58 * S, 64 * S, 64 * S));
            MkSpText(g, Weather.Temp(h.Temp), Paper.Num, Paper.Head, x + 92 * S, y + 60 * S); MkSpText(g, L.T(Weather.Words(h.Code)), Paper.Body, Paper.Ink, x + 92 * S, y + 100 * S);
            float dx = x + w * 0.62f, dy = y + 60 * S;
            foreach (var kv in new[] { new[] { L.T("Rain:"), h.Pop < 0 ? "-" : h.Pop + "%" }, new[] { L.T("Wind:"), double.IsNaN(h.Wind) ? "-" : h.Wind.ToString("0", CultureInfo.CurrentCulture) + " km/h" }, new[] { L.T("Feels like:"), double.IsNaN(h.Feels) ? "-" : Weather.Temp(h.Feels) } }) { MkSpText(g, kv[0] + " " + kv[1], Paper.Small, Paper.Ink, dx, dy); dy += 22 * S; }
            var band = new RectangleF(x + 12 * S, r.Bottom - 44 * S, w - 24 * S, 32 * S); Theme.Fill(g, band, 4 * S, Paper.Soft);
            MkSpText(g, h.Pop >= 50 ? L.T("At kick-off: rain likely.") : h.Pop >= 0 ? L.T("At kick-off: dry, most likely.") : L.T("At kick-off: forecast unknown."), Paper.Small, Paper.Ink, band.X + 10 * S, band.Y + 8 * S);
            return r.Bottom + 16 * S;
        }
        float SpNetDesk(Graphics g, float x, float y, float w)
        {
            var r = new RectangleF(x, y, w, 196 * S); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Network Outlook", L.T("(your connection)"));
            double net, jit, loss; double[] hist; lock (M.L) { net = M.NetPing.Avg(30); loss = M.Loss.Avg(20) * 100; hist = M.NetPing.Values(30); }
            var okv = hist.Where(v => !double.IsNaN(v)).ToList(); jit = okv.Count >= 3 ? Enumerable.Range(1, okv.Count - 1).Average(i => Math.Abs(okv[i] - okv[i - 1])) : double.NaN;
            int lv = double.IsNaN(net) ? 3 : (!double.IsNaN(loss) && loss >= 2) || net > 100 ? 2 : net > 50 || jit > 20 ? 1 : 0;
            Paper.Pill(g, lv == 0 ? L.T("Stable") : lv == 1 ? L.T("Busy") : lv == 2 ? L.T("Unsteady") : L.T("Checking"), x + w / 2, y + 50 * S, Paper.Lv(lv) == Paper.Up ? Paper.Green : Paper.Lv(lv));
            float dy = y + 88 * S;
            foreach (var kv in new[] { new[] { L.T("Latency:"), double.IsNaN(net) ? "-" : net.ToString("0", CultureInfo.CurrentCulture) + " ms" }, new[] { L.T("Jitter:"), double.IsNaN(jit) ? "-" : jit.ToString("0", CultureInfo.CurrentCulture) + " ms" }, new[] { L.T("Packet loss:"), double.IsNaN(loss) ? "-" : loss.ToString("0.0", CultureInfo.CurrentCulture) + "%" } }) { MkSpText(g, kv[0], Paper.BodyB, Paper.Head, x + 16 * S, dy); MkSpText(g, kv[1], Paper.Body, Paper.Ink, x + 140 * S, dy); dy += 24 * S; }
            Paper.Spark(g, new RectangleF(x + w * 0.58f, y + 92 * S, w * 0.42f - 20 * S, 56 * S), hist, Paper.Up, true);
            return r.Bottom + 16 * S;
        }
        float SpPcDesk(Graphics g, float x, float y, float w)
        {
            var r = new RectangleF(x, y, w, 196 * S); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "PC Outlook", L.T("(this computer)"));
            double cpu, mem; lock (M.L) { cpu = M.Cpu.Avg(10); mem = M.Mem.Last; }
            var snap = M.Latest;
            int lv = double.IsNaN(cpu) ? 3 : cpu > 85 || mem > 92 ? 2 : cpu > 60 || mem > 85 ? 1 : 0;
            Paper.Pill(g, lv == 0 ? L.T("Healthy") : lv == 1 ? L.T("Busy") : lv == 2 ? L.T("Struggling") : L.T("Checking"), x + w / 2, y + 50 * S, lv == 0 ? Paper.Green : Paper.Lv(lv));
            float dy = y + 88 * S;
            foreach (var kv in new[] { new[] { "CPU:", double.IsNaN(cpu) ? "-" : cpu.ToString("0", CultureInfo.CurrentCulture) + "%" }, new[] { L.T("RAM:"), double.IsNaN(mem) ? "-" : mem.ToString("0", CultureInfo.CurrentCulture) + "%" }, new[] { L.T("Storage:"), snap == null || snap.DiskText == "" ? "-" : snap.DiskText } }) { MkSpText(g, kv[0], Paper.BodyB, Paper.Head, x + 16 * S, dy); MkSpText(g, kv[1], Paper.Body, Paper.Ink, x + 120 * S, dy); dy += 24 * S; }
            var band = new RectangleF(x + 12 * S, r.Bottom - 40 * S, w - 24 * S, 28 * S); Theme.Fill(g, band, 4 * S, lv == 0 ? Paper.GoodSoft : Paper.Soft);
            MkSpText(g, lv == 0 ? L.T("Resources are in good shape for streaming.") : lv == 3 ? L.T("Measuring...") : L.T("Close heavy apps before kick-off."), Paper.Small, Paper.Ink, band.X + 10 * S, band.Y + 6 * S);
            return r.Bottom + 16 * S;
        }
        float SpSlotDesk(Graphics g, float x, float y, float w, DateTime ko)
        {
            var r = new RectangleF(x, y, w, 250 * S); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Your Line at Kick-off Time", ko.ToString("dddd h tt", CultureInfo.CurrentCulture) + " · " + L.T("last 6 weeks"));
            var slots = Cfg.History ? LineHours.SameSlot(ko, 2) : null;
            if (!Cfg.History) { using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(L.T("Turn on History in Settings: AdeeboMeter writes one line a minute on this PC, and this chart is made from it."), Paper.Body, ib, new RectangleF(x + 16 * S, y + 60 * S, w - 32 * S, 60 * S)); return r.Bottom + 16 * S; }
            if (slots == null) { MkSpText(g, L.T("Reading your history..."), Paper.Body, Paper.Muted, x + 16 * S, y + 60 * S); return r.Bottom + 16 * S; }
            var ok = slots.Where(s => !double.IsNaN(s.Ms)).ToList();
            if (ok.Count == 0) { MkSpText(g, L.T("No history yet for this day and hour - ask again after a few weeks."), Paper.Body, Paper.Ink, x + 16 * S, y + 60 * S); return r.Bottom + 16 * S; }
            double mx = Math.Max(20, ok.Max(s => s.Ms) * 1.25); var ch = new RectangleF(x + 60 * S, y + 56 * S, w * 0.62f - 60 * S, 150 * S);
            for (int i = 0; i <= 4; i++) { float gy = ch.Y + ch.Height * i / 4; using (var gp = new Pen(Paper.Line2, 1)) g.DrawLine(gp, ch.X, gy, ch.Right, gy); var lt = (mx * (4 - i) / 4).ToString("0", CultureInfo.CurrentCulture) + " ms"; var ls = g.MeasureString(lt, Draw.fTiny); MkSpText(g, lt, Draw.fTiny, Paper.Muted, ch.X - ls.Width - 4 * S, gy - 7 * S); }
            float bw = ch.Width / slots.Count;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i]; float bx = ch.X + i * bw + bw * 0.2f;
                if (!double.IsNaN(s.Ms)) { float bh = (float)(s.Ms / mx) * ch.Height; using (var b = new SolidBrush(!double.IsNaN(s.Loss) && s.Loss >= 2 ? Paper.Down : Color.FromArgb(31, 111, 235))) g.FillRectangle(b, bx, ch.Bottom - bh, bw * 0.6f, bh); }
                else MkSpText(g, "-", Paper.Small, Paper.Muted, bx + bw * 0.2f, ch.Bottom - 20 * S);
                MkSpText(g, s.Day.ToString("MMM d", CultureInfo.CurrentCulture), Draw.fTiny, Paper.Muted, bx - 4 * S, ch.Bottom + 4 * S);
            }
            // the outlook beside it: from those weeks only
            double med = ok.Select(s => s.Ms).OrderBy(v => v).ElementAt(ok.Count / 2); bool lossy = ok.Any(s => !double.IsNaN(s.Loss) && s.Loss >= 2);
            float ox = x + w * 0.66f; MkSpText(g, L.T("Outlook for kick-off"), Paper.Head3, Paper.Head, ox, y + 58 * S);
            Paper.Pill(g, ok.Count < 3 ? L.T("Too little history") : lossy ? L.T("Watch out") : med < 50 ? L.T("Good") : L.T("Fair"), ox + 80 * S, y + 88 * S, ok.Count < 3 ? Paper.Muted : lossy ? Paper.Amber : med < 50 ? Paper.Green : Paper.Amber);
            float ty = y + 128 * S;
            foreach (var t in new[] { L.T("Delay is usually around") + " " + med.ToString("0", CultureInfo.CurrentCulture) + " ms", L.T("Based on") + " " + ok.Count + " " + L.T("of the last 6 weeks"), lossy ? L.T("Some weeks lost packets at this hour") : L.T("No drops at this hour") })
            { Paper.Tick(g, new RectangleF(ox, ty + 1 * S, 16 * S, 16 * S), lossy && t.StartsWith(L.T("Some"), StringComparison.Ordinal) ? Paper.Amber : Paper.Green); using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(t, Paper.Small, ib, new RectangleF(ox + 22 * S, ty, w * 0.34f - 30 * S, 36 * S)); ty += 34 * S; }
            return r.Bottom + 16 * S;
        }
        float SpChecklist(Graphics g, float x, float y, float w, Sports.Game next)
        {
            var r = new RectangleF(x, y, w, 250 * S); Paper.Desk(g, r);
            float ty = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Before Kick-off", L.T("what to prepare")) + 8 * S;
            double net, cpu; lock (M.L) { net = M.NetPing.Avg(10); cpu = M.Cpu.Avg(10); }
            var last = StreamCheck.Last; bool tested = last != null && (DateTime.Now - last.When).TotalHours < 12 && last.Score >= 55;
            var items = new List<Tuple<int, string, Action>> {
                Tuple.Create(double.IsNaN(net) ? -1 : net < 80 ? 1 : 0, double.IsNaN(net) ? L.T("Checking your network...") : net < 80 ? L.T("Your network is stable - no action needed.") : L.T("Your network is slow right now."), (Action)null),
                Tuple.Create(double.IsNaN(cpu) ? -1 : cpu < 70 ? 1 : 0, double.IsNaN(cpu) || cpu < 70 ? L.T("Your PC is ready for streaming.") : L.T("Your PC is busy: close heavy apps."), (Action)null),
                Tuple.Create(tested ? 1 : 0, tested ? L.T("Your stream link is tested.") : L.T("Test your stream link (recommended)."), (Action)delegate { spTab = "stream"; sportsCanvas.Invalidate(); }),
                Tuple.Create(Cfg.KickoffRemind ? 1 : 0, L.T("Remind me in the corner 15 minutes before kick-off") + (Cfg.KickoffRemind ? "" : "  " + L.T("(off)")), (Action)delegate { Cfg.KickoffRemind = !Cfg.KickoffRemind; Cfg.Save(); sportsCanvas.Invalidate(); }) };
            foreach (var it in items)
            {
                var box = new RectangleF(x + 16 * S, ty, 22 * S, 22 * S);
                if (it.Item1 == 1) Paper.Tick(g, box, Paper.Green); else { Theme.Fill(g, box, 4 * S, Paper.Dark ? Paper.Soft : Color.White); Theme.Stroke(g, box, 4 * S, it.Item1 < 0 ? Paper.Line2 : Paper.Line, 1.4f); }
                using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(it.Item2, it.Item1 == 0 && it.Item3 != null && !it.Item2.Contains(L.T("Remind")) ? Paper.BodyB : Paper.Body, ib, new RectangleF(x + 48 * S, ty + 1 * S, w - 64 * S, 44 * S));
                if (it.Item3 != null) Act(sportsCanvas, new RectangleF(x + 12 * S, ty - 4 * S, w - 24 * S, 34 * S), it.Item3);
                ty += 44 * S;
            }
            return r.Bottom + 16 * S;
        }

        // ---------- My Teams
        float SpTeams(Graphics g, float x, float y, float w)
        {
            List<Sports.Game> games; lock (Sports.k) games = Sports.Games.ToList();
            var teams = Sports.Teams();
            var r = new RectangleF(x, y, w, 110 * S + Math.Max(1, teams.Count) * 64 * S); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "The Teams You Follow", L.T("their games come first everywhere: Overview, the corner, Streaming"));
            var fb = MkSpButton(g, "+ " + L.T("Follow a team..."), x + w - 16 * S - g.MeasureString("+ " + L.T("Follow a team..."), Draw.fBtn).Width - 28 * S, y + 12 * S, true); Act(sportsCanvas, fb, AskTeams);
            float ty = y + 62 * S;
            if (teams.Count == 0) MkSpText(g, L.T("None yet: names or short codes work - Leafs, TOR, Arsenal, India."), Paper.Body, Paper.Ink, x + 16 * S, ty + 10 * S);
            foreach (var t in teams)
            {
                using (var lp = new Pen(Paper.Line2, 1)) g.DrawLine(lp, x + 12 * S, ty, x + w - 12 * S, ty);
                var bc = new RectangleF(x + 18 * S, ty + 10 * S, 44 * S, 44 * S); using (var b = new SolidBrush(TeamColor(t))) g.FillEllipse(b, bc); var cs = g.MeasureString(Code(t), Draw.fTiny); MkSpText(g, Code(t), Draw.fTiny, Color.White, bc.X + (bc.Width - cs.Width) / 2, bc.Y + (bc.Height - cs.Height) / 2);
                MkSpText(g, t, Paper.Head3, Paper.Head, x + 76 * S, ty + 20 * S);
                var tg = games.Where(z => Sports.TeamIn(t, z)).ToList();
                var live = tg.FirstOrDefault(z => z.State == "live"); var done = tg.Where(z => z.State == "done").OrderByDescending(z => z.StartUtc).FirstOrDefault(); var soon = tg.Where(z => z.State == "soon" && (z.StartUtc == DateTime.MinValue || z.StartUtc > DateTime.UtcNow.AddHours(-3))).OrderBy(z => z.StartUtc).FirstOrDefault();   /* round 6: a game from hours ago is not "next" */ var offg = tg.FirstOrDefault(z => z.State == "off");
                string a = live != null ? L.T("Live:") + " " + live.Home + " " + live.HomeScore + " - " + live.AwayScore + " " + live.Away : offg != null ? OffWord(offg) + ": " + offg.Home + " " + L.T("vs") + " " + offg.Away : done != null ? L.T("Last:") + " " + done.Home + " " + done.HomeScore + " - " + done.AwayScore + " " + done.Away : L.T("No game in today's feeds");
                string b2 = soon != null ? L.T("Next:") + " " + soon.Home + " " + L.T("vs") + " " + soon.Away + (soon.StartUtc > DateTime.MinValue.AddDays(2) ? " · " + soon.StartUtc.ToLocalTime().ToString("ddd h:mm tt", CultureInfo.CurrentCulture) : "") : "";   // R2-158: no time is no time, not year 1
                MkSpText(g, a, Paper.Body, live != null ? Paper.Down : Paper.Ink, x + w * 0.32f, ty + 12 * S); if (b2 != "") MkSpText(g, b2, Paper.Body, Paper.Ink, x + w * 0.32f, ty + 34 * S);
                var xr = new RectangleF(x + w - 52 * S, ty + 16 * S, 32 * S, 32 * S); Theme.Fill(g, xr, 6 * S, Paper.DeskBg); Theme.Stroke(g, xr, 6 * S, Paper.Line, 1); MkSpText(g, "×", Paper.BodyB, Paper.Muted, xr.X + 11 * S, xr.Y + 6 * S);
                string tt = t; Act(sportsCanvas, xr, delegate { Cfg.SportTeams = string.Join(", ", Sports.Teams().Where(z => !string.Equals(z, tt, StringComparison.OrdinalIgnoreCase))); Cfg.Save(); sportsCanvas.Invalidate(); });
                ty += 64 * S;
            }
            y = r.Bottom + 16 * S;
            // where the scores come from
            var sr = new RectangleF(x, y, w, 210 * S); Paper.Desk(g, sr);
            float sy = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Where the Scores Come From", L.T("fetched only while a Sports page is open, every 5 minutes at most")) + 8 * S;
            foreach (var src in new[] { new[] { "hockey", L.T("Hockey · the NHL's public feed") }, new[] { "football", Sports.HasKey("football") ? L.T("Football · football-data.org") : L.T("Football · ESPN's scoreboards (or your own key)") }, new[] { "cricket", L.T("Cricket · CricketData") }, new[] { "news", L.T("Headlines · BBC Sport") } })
            {
                MkSpText(g, src[1], Paper.Body, Paper.Ink, x + 16 * S, sy);
                bool keyed = src[0] == "hockey" || src[0] == "news" || Sports.HasKey(src[0]);
                string st = src[0] == "hockey" || src[0] == "news" ? L.T("no key needed") : keyed ? L.T("your key is set") : src[0] == "football" ? L.T("no key needed - a key gives more leagues") : L.T("needs your free key");   // R2-046: football works without a key (ESPN)
                if (src[0] == "football" && !keyed) keyed = true;
                MkSpText(g, st, Paper.BodyB, keyed ? Paper.Up : Paper.Amber, x + w * 0.5f, sy);
                if (src[0] == "football" || src[0] == "cricket") { var kb = MkSpButton(g, keyed ? L.T("Change key...") : L.T("Add my key..."), x + w - 170 * S, sy - 8 * S, false); string sp = src[0]; Act(sportsCanvas, kb, delegate { AskSportKey(sp); sportsCanvas.Invalidate(); }); }
                sy += 40 * S;
            }
            return sr.Bottom + 16 * S;
        }

        // ---------- Live Scores / Fixtures
        float SpScores(Graphics g, float x, float y, float w, bool liveTab)
        {
            if (!SpNeedsOn(g, x, y, w)) return y + 140 * S;
            List<Sports.Game> games; DateTime at; lock (Sports.k) { games = Sports.Games.ToList(); at = Sports.FetchedAt; }
            // filter chips
            float cx = x; foreach (var f in new[] { new[] { "all", "All" }, new[] { "hockey", "Hockey" }, new[] { "football", "Football" }, new[] { "cricket", "Cricket" } })
            {
                var s = g.MeasureString(L.T(f[1]), Draw.fSmall); var r = new RectangleF(cx, y, s.Width + 26 * S, 32 * S); bool on = spFilter == f[0];
                if (on) Theme.Fill(g, r, 16 * S, Paper.Navy); else { Theme.Fill(g, r, 16 * S, Paper.DeskBg); Theme.Stroke(g, r, 16 * S, Paper.Line, 1); }
                MkSpText(g, L.T(f[1]), Draw.fSmall, on ? Color.White : Paper.Ink, r.X + 13 * S, r.Y + (r.Height - s.Height) / 2); string id = f[0]; Act(sportsCanvas, r, delegate { spFilter = id; sportsCanvas.Invalidate(); }); cx = r.Right + 8 * S;
            }
            var mb = new RectangleF(cx + 16 * S, y + 5 * S, 22 * S, 22 * S); if (spMineOnly) Paper.Tick(g, mb, Paper.Navy); else { Theme.Fill(g, mb, 4 * S, Color.White); Theme.Stroke(g, mb, 4 * S, Paper.Line, 1.4f); }
            MkSpText(g, L.T("Only my teams"), Paper.Body, Paper.Ink, mb.Right + 8 * S, y + 5 * S); Act(sportsCanvas, new RectangleF(mb.X, y, 160 * S, 32 * S), delegate { spMineOnly = !spMineOnly; sportsCanvas.Invalidate(); });
            if (at != DateTime.MinValue) { var us = L.T("Updated") + " " + at.ToLocalTime().ToString("t", CultureInfo.CurrentCulture); var uz = g.MeasureString(us, Paper.Small); MkSpText(g, us, Paper.Small, Paper.Muted, x + w - uz.Width, y + 8 * S); }
            y += 46 * S;
            var pick = games.Where(z => (spFilter == "all" || z.Sport == spFilter) && (!spMineOnly || Sports.Mine(z))).ToList();
            if (liveTab)
            {
                var live = pick.Where(z => z.State == "live").ToList();
                int per = w > 1000 * S ? 3 : w > 640 * S ? 2 : 1; float cw = (w - 32 * S - (per - 1) * 12 * S) / per; int rows = Math.Max(1, (live.Count + per - 1) / per);
                var lr = new RectangleF(x, y, w, 70 * S + rows * 112 * S); Paper.Desk(g, lr);
                float ly = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Live Now", live.Count > 0 ? live.Count + " " + L.T("live") : null) + 6 * S;
                if (live.Count == 0) MkSpText(g, L.T("Nothing live right now."), Paper.Body, Paper.Ink, x + 16 * S, ly);
                for (int i = 0; i < live.Count; i++)
                {
                    var m = live[i]; var gr = new RectangleF(x + 16 * S + (i % per) * (cw + 12 * S), ly + (i / per) * 112 * S, cw, 100 * S);
                    Theme.Fill(g, gr, 6 * S, Paper.Dark ? Paper.Soft : Color.White); Theme.Stroke(g, gr, 6 * S, Paper.Line2, 1); if (Sports.Mine(m)) using (var mp = new Pen(Color.FromArgb(31, 111, 235), 4 * S)) g.DrawLine(mp, gr.X + 2 * S, gr.Y + 4 * S, gr.X + 2 * S, gr.Bottom - 4 * S);
                    MkSpText(g, m.Home, Paper.BodyB, Paper.Head, gr.X + 14 * S, gr.Y + 10 * S); MkSpText(g, m.Away, Paper.BodyB, Paper.Head, gr.X + 14 * S, gr.Y + 36 * S);
                    var hs = g.MeasureString(Or(m.HomeScore, "-"), Paper.NumS); MkSpText(g, Or(m.HomeScore, "-"), Paper.NumS, Paper.Head, gr.Right - hs.Width - 12 * S, gr.Y + 8 * S); var as2 = g.MeasureString(Or(m.AwayScore, "-"), Paper.NumS); MkSpText(g, Or(m.AwayScore, "-"), Paper.NumS, Paper.Head, gr.Right - as2.Width - 12 * S, gr.Y + 34 * S);
                    MkSpText(g, (m.League != "" ? m.League : L.T(Sports.Name(m.Sport))) + (m.Detail != "" ? " · " + Cut(m.Detail, 40) : ""), Paper.Small, Paper.Down, gr.X + 14 * S, gr.Bottom - 26 * S);
                }
                y = lr.Bottom + 16 * S;
                var done = pick.Where(z => z.State == "done").OrderByDescending(z => z.StartUtc).Take(12).ToList();
                var dr = new RectangleF(x, y, w, 70 * S + Math.Max(1, done.Count) * 32 * S); Paper.Desk(g, dr);
                float dy = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Final Scores", null) + 6 * S;
                if (done.Count == 0) MkSpText(g, L.T("No finished games in today's feeds."), Paper.Body, Paper.Ink, x + 16 * S, dy);
                foreach (var m in done) { MkSpText(g, m.Home + "  " + m.HomeScore + "  ·  " + m.Away + "  " + m.AwayScore, Paper.Body, Paper.Ink, x + 16 * S, dy); var lg = m.League != "" ? m.League : L.T(Sports.Name(m.Sport)); var ls = g.MeasureString(lg, Paper.Small); MkSpText(g, lg, Paper.Small, Paper.Muted, x + w - ls.Width - 16 * S, dy + 2 * S); dy += 32 * S; }
                return dr.Bottom + 16 * S;
            }
            var soon = pick.Where(z => z.State == "soon" || z.State == "off").OrderBy(z => z.StartUtc).Take(20).ToList();   /* hunt-5: postponed / cancelled games listed, with that word */
            var fr = new RectangleF(x, y, w, 70 * S + Math.Max(1, soon.Count) * 36 * S); Paper.Desk(g, fr);
            float fy = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Fixtures", L.T("in your time")) + 6 * S;
            if (soon.Count == 0) MkSpText(g, L.T("No upcoming games in today's feeds."), Paper.Body, Paper.Ink, x + 16 * S, fy);
            foreach (var m in soon)
            {
                bool mine = Sports.Mine(m); if (mine) using (var mp = new Pen(Color.FromArgb(31, 111, 235), 4 * S)) g.DrawLine(mp, x + 10 * S, fy, x + 10 * S, fy + 26 * S);
                MkSpText(g, m.State == "off" ? OffWord(m) : m.StartUtc == DateTime.MinValue ? "-" : m.StartUtc.ToLocalTime().ToString("ddd h:mm tt", CultureInfo.CurrentCulture), Paper.BodyB, m.State == "off" ? Paper.Muted : Paper.Head, x + 20 * S, fy);
                MkSpText(g, m.Home + " " + L.T("vs") + " " + m.Away + (mine ? "  · " + L.T("your team") : ""), mine ? Paper.BodyB : Paper.Body, Paper.Ink, x + w * 0.22f, fy);
                var lg = m.League != "" ? m.League : L.T(Sports.Name(m.Sport)); var ls = g.MeasureString(lg, Paper.Small); MkSpText(g, lg, Paper.Small, Paper.Muted, x + w - ls.Width - 16 * S, fy + 2 * S);
                fy += 36 * S;
            }
            return fr.Bottom + 16 * S;
        }

        // ---------- Stream Check: the link, the verdict and score, which quality will play, the test, the way along, tips
        // a game that won't be played as planned: postponed, cancelled, suspended (from the feed's own word)
        static string OffWord(Sports.Game m) { string d = (m.Detail ?? "").ToLowerInvariant(); return L.T(d.Contains("cancel") ? "Cancelled" : d.Contains("suspend") ? "Suspended" : d.Contains("abandon") ? "Abandoned" : "Postponed"); }
        float SpStream(Graphics g, float x, float y, float w)
        {
            var ir = new RectangleF(x, y, w, 150 * S); Paper.Desk(g, ir);
            float iy = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Check Your Stream", L.T("about 30 seconds · it downloads the real video to see what your line can carry")) + 8 * S;
            iy = SpLinkBox(g, x + 16 * S, iy, w - 32 * S);
            float cx = x + 16 * S; MkSpText(g, L.T("Saved on this PC:"), Paper.Small, Paper.Muted, cx, iy + 4 * S); cx += g.MeasureString(L.T("Saved on this PC:"), Paper.Small).Width + 8 * S;
            foreach (var l in StreamCheck.Links().Take(4))
            {
                var s = g.MeasureString(l[0], Draw.fSmall); var r = new RectangleF(cx, iy, s.Width + 24 * S, 28 * S); if (r.Right > x + w - 16 * S) break;
                Theme.Fill(g, r, 14 * S, Paper.Dark ? Paper.Soft : Color.White); Theme.Stroke(g, r, 14 * S, Paper.Line, 1); MkSpText(g, l[0], Draw.fSmall, Paper.Ink, r.X + 12 * S, r.Y + (r.Height - s.Height) / 2);
                string url = l[1]; Act(sportsCanvas, r, delegate { spLink.Text = url; }); cx = r.Right + 8 * S;
            }
            y = ir.Bottom + 16 * S;
            var res = StreamCheck.Last;
            if (StreamCheck.Running)
            {
                var pr = new RectangleF(x, y, w, 110 * S); Paper.Desk(g, pr); MkSpText(g, L.T(StreamCheck.Step) + "...", Paper.Head2, Paper.Head, x + 16 * S, y + 16 * S);
                var bar = new RectangleF(x + 16 * S, y + 64 * S, w - 32 * S, 14 * S); Theme.Fill(g, bar, 7 * S, Paper.Line2); Theme.Fill(g, new RectangleF(bar.X, bar.Y, Math.Max(14 * S, bar.Width * (float)StreamCheck.Progress), bar.Height), 7 * S, Paper.Green);
                return pr.Bottom + 16 * S;
            }
            if (res == null) return y;
            // verdict | which quality will play
            float lw = Math.Max(300 * S, w * 0.32f);
            var vr = new RectangleF(x, y, lw, 340 * S); Paper.Desk(g, vr);
            Paper.Ring(g, new RectangleF(x + (lw - 170 * S) / 2, y + 20 * S, 170 * S, 170 * S), res.Score);
            var vs = g.MeasureString(L.T(res.Verdict), Paper.Head2); MkSpText(g, L.T(res.Verdict), Paper.Head2, Paper.Head, x + (lw - vs.Width) / 2, y + 200 * S);
            using (var ib = new SolidBrush(Paper.Ink)) using (var cf = new StringFormat { Alignment = StringAlignment.Center }) g.DrawString(res.Line, Paper.Body, ib, new RectangleF(x + 14 * S, y + 236 * S, lw - 28 * S, 60 * S), cf);
            var ws = res.When.ToString("g", CultureInfo.CurrentCulture) + (res.Kind != "" ? " · " + (res.Kind == "page" ? L.T("web page") : res.Kind) : "") + " · " + res.Host; var wz = g.MeasureString(ws, Paper.Small); MkSpText(g, ws, Paper.Small, Paper.Muted, x + Math.Max(8 * S, (lw - wz.Width) / 2), vr.Bottom - 28 * S);
            float qx = x + lw + 16 * S, qw = w - lw - 16 * S; var qr = new RectangleF(qx, y, qw, 340 * S); Paper.Desk(g, qr);
            float qy = Paper.DeskTitle(g, qx + 16 * S, y + 12 * S, "Which Quality Will Play", L.T("what each needs comes from the stream itself")) + 6 * S;
            if (res.Ladder.Count == 0) MkSpText(g, res.IsPage ? L.T("A web page doesn't list its qualities. Paste the stream's own address for this table.") : L.T("This stream lists one quality."), Paper.Body, Paper.Ink, qx + 16 * S, qy);
            else
            {
                using (var hb = new SolidBrush(Paper.Soft)) g.FillRectangle(hb, qx + 8 * S, qy, qw - 16 * S, 26 * S);
                MkSpText(g, L.T("Quality"), Draw.fSmall, Paper.Ink, qx + 16 * S, qy + 5 * S); MkSpText(g, L.T("Needs"), Draw.fSmall, Paper.Ink, qx + qw * 0.4f, qy + 5 * S); MkSpText(g, L.T("You got"), Draw.fSmall, Paper.Ink, qx + qw * 0.55f, qy + 5 * S); MkSpText(g, L.T("Verdict"), Draw.fSmall, Paper.Ink, qx + qw * 0.72f, qy + 5 * S); qy += 32 * S;
                foreach (var v in res.Ladder.Take(5))
                {
                    int p = double.IsNaN(res.Mbps) ? 3 : StreamCheck.Plays(v, res.Mbps, res.Low);
                    MkSpText(g, v.Name, Paper.BodyB, Paper.Head, qx + 16 * S, qy); MkSpText(g, (v.Bw / 1e6).ToString("0.0", CultureInfo.CurrentCulture) + " Mb/s", Paper.Body, Paper.Ink, qx + qw * 0.4f, qy); MkSpText(g, double.IsNaN(res.Mbps) ? "-" : res.Mbps.ToString("0.0", CultureInfo.CurrentCulture) + " Mb/s", Paper.Body, Paper.Ink, qx + qw * 0.55f, qy);
                    if (p <= 2) Paper.Mark(g, new RectangleF(qx + qw * 0.72f, qy + 1 * S, 18 * S, 18 * S), p); MkSpText(g, p == 0 ? L.T("Plays smoothly") : p == 1 ? L.T("May drop at peaks") : p == 2 ? L.T("Would buffer") : "-", Paper.BodyB, p == 0 ? Paper.Up : p == 1 ? Paper.Amber : p == 2 ? Paper.Down : Paper.Muted, qx + qw * 0.72f + 24 * S, qy);
                    qy += 30 * S;
                }
            }
            // the test itself
            if (res.Samples.Count >= 2)
            {
                var ch = new RectangleF(qx + 16 * S, qr.Bottom - 110 * S, qw - 32 * S, 80 * S); MkSpText(g, L.T("During the test (Mb/s, one point a second)"), Paper.Small, Paper.Muted, ch.X, ch.Y - 20 * S);
                double mx = Math.Max(res.Samples.Max(), res.Best != null ? res.Best.Bw / 1e6 : 0) * 1.15;
                if (res.Best != null) { float ny = ch.Bottom - (float)(res.Best.Bw / 1e6 / mx) * ch.Height; using (var dp = new Pen(Paper.Red, 1.4f * S) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash }) g.DrawLine(dp, ch.X, ny, ch.Right, ny); MkSpText(g, StreamCheck.Short(res.Best) + " " + L.T("needs") + " " + (res.Best.Bw / 1e6).ToString("0.0", CultureInfo.CurrentCulture), Draw.fTiny, Paper.Red, ch.X + 4 * S, ny - 16 * S); }
                var pts = res.Samples.Select((v, i) => new PointF(ch.X + ch.Width * i / (res.Samples.Count - 1), ch.Bottom - (float)(v / mx) * ch.Height)).ToArray();
                using (var lp = new Pen(Paper.Navy, 2.4f * S)) g.DrawLines(lp, pts);
            }
            y = Math.Max(vr.Bottom, qr.Bottom) + 16 * S;
            // along the way
            var ar = new RectangleF(x, y, w, 140 * S); Paper.Desk(g, ar); Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Along the Way", null);
            float hw = (w - 32 * S - 4 * 10 * S) / 5;
            for (int i = 0; i < 5; i++)
            {
                var hr = new RectangleF(x + 16 * S + i * (hw + 10 * S), y + 52 * S, hw, 72 * S); Theme.Fill(g, hr, 6 * S, Paper.Dark ? Paper.Soft : Color.White); Theme.Stroke(g, hr, 6 * S, res.HopLv[i] == 1 ? Paper.Amber : res.HopLv[i] == 2 ? Paper.Down : Paper.Line2, res.HopLv[i] >= 1 && res.HopLv[i] <= 2 ? 1.6f : 1);
                using (var db = new SolidBrush(res.HopLv[i] == 0 ? Paper.Green : res.HopLv[i] == 1 ? Paper.Amber : res.HopLv[i] == 2 ? Paper.Down : Paper.Muted)) g.FillEllipse(db, hr.X + hr.Width / 2 - 6 * S, hr.Y + 10 * S, 12 * S, 12 * S);
                var hn = L.T(res.Hops[i] ?? ""); var hz = g.MeasureString(hn, Paper.BodyB); MkSpText(g, hn, Paper.BodyB, Paper.Head, hr.X + (hr.Width - hz.Width) / 2, hr.Y + 26 * S);
                var nn = res.HopNote[i] ?? ""; var nz = g.MeasureString(nn, Paper.Small); MkSpText(g, nn, Paper.Small, Paper.Muted, hr.X + Math.Max(4 * S, (hr.Width - nz.Width) / 2), hr.Y + 48 * S);
            }
            y = ar.Bottom + 16 * S;
            // how to make it better
            var tr = new RectangleF(x, y, w, 70 * S + Math.Max(1, res.Tips.Count) * 64 * S); Paper.Desk(g, tr);
            float ty = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "How to Make It Better", L.T("most helpful first · each from something measured")) + 6 * S;
            if (res.Tips.Count == 0) MkSpText(g, L.T("Nothing to change: it should play as well as this stream allows."), Paper.Body, Paper.Ink, x + 16 * S, ty);
            MkTipsOn(g, sportsCanvas, res.Tips, x, ty, w);
            y = tr.Bottom + 16 * S;
            // earlier checks
            var ear = StreamCheck.Earlier().Take(6).ToList();
            if (ear.Count > 0)
            {
                var er = new RectangleF(x, y, w, 64 * S + ear.Count * 30 * S); Paper.Desk(g, er); float ey = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Earlier Checks", null) + 4 * S;
                foreach (var e in ear)
                {
                    DateTime t; DateTime.TryParse(e[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t); int sc; int.TryParse(e[4], out sc);
                    MkSpText(g, t == DateTime.MinValue ? "-" : t.ToString("ddd MMM d · h:mm tt", CultureInfo.CurrentCulture), Paper.BodyB, Paper.Head, x + 16 * S, ey); MkSpText(g, e[1], Paper.Body, Paper.Ink, x + w * 0.3f, ey); MkSpText(g, e[2] == "" ? "-" : e[2] + " Mb/s", Paper.Body, Paper.Ink, x + w * 0.6f, ey);
                    MkSpText(g, L.T(e[3]) + (sc >= 0 ? " · " + sc : ""), Paper.BodyB, sc >= 80 ? Paper.Up : sc >= 55 ? Paper.Amber : sc >= 0 ? Paper.Down : Paper.Muted, x + w * 0.72f, ey); ey += 30 * S;
                }
                y = er.Bottom + 16 * S;
            }
            return y;
        }
        // the ranked tips (shared look with Line Check)
        void MkTipsOn(Graphics g, Control c, List<StreamCheck.Tip> tips, float tx, float ty, float tw)
        {
            foreach (var t in tips)
            {
                using (var lp = new Pen(Paper.Line2, 1)) g.DrawLine(lp, tx + 12 * S, ty, tx + tw - 12 * S, ty);
                var nr = new RectangleF(tx + 16 * S, ty + 12 * S, 28 * S, 28 * S); using (var nb = new SolidBrush(t.Rank == 1 && tips.Count > 1 ? Paper.Down : t.Rank == 2 && tips.Count > 2 ? Paper.Amber : Paper.Navy)) g.FillEllipse(nb, nr);
                var ns = g.MeasureString(t.Rank.ToString(CultureInfo.CurrentCulture), Draw.fBtn); MkSpText(g, t.Rank.ToString(CultureInfo.CurrentCulture), Draw.fBtn, Color.White, nr.X + (nr.Width - ns.Width) / 2, nr.Y + (nr.Height - ns.Height) / 2);
                MkSpText(g, L.T(t.Title), Paper.BodyB, Paper.Head, tx + 56 * S, ty + 10 * S);
                using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(L.T(t.Body), Paper.Small, ib, new RectangleF(tx + 56 * S, ty + 30 * S, tw - 72 * S, 20 * S));
                MkSpText(g, L.T("seen:") + " " + t.Seen, Draw.fTiny, Paper.Muted, tx + 56 * S, ty + 48 * S);
                ty += 64 * S;
            }
        }

        // the kick-off reminder (only when ticked): once, 15 minutes before a followed team's game
        void SpReminder() { Sports.ReminderTick(); }   // round 6 M-1: the work is in Sports, run by the corner too
    }
}
