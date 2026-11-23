// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Streaming tab (PLAN-modes.md 2)
    // Now streaming (found by itself), the 4K check, the 90-day records with who caused each dip, when dips happen,
    // and the monthly report for the provider. Scores and the sport newspaper are next door (FullView.Sports.cs).
    sealed partial class FullView
    {
        Panel streamPage; DB streamCanvas; string streamSel = "", streamKey = "", streamNote = ""; bool stream4k; DateTime streamCopied = DateTime.MinValue;
        Panel BuildStreamPage()
        {
            streamPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            streamCanvas = new DB { Name = "streamCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            streamCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintStream(e.Graphics); }; Wire(streamCanvas);
            streamPage.Controls.Add(streamCanvas);
            return streamPage;
        }
        // repaint only when something moved (a sample, a dip, a session start/end) and once a minute for the times
        string streamVer = "";
        void StreamTick()
        {
            if (tab != "stream" || streamCanvas == null) return;
            // a new sample only moves the NOW STREAMING card; a stream starting or ending, a new dip or anything else redraws the page
            var nw = Streams.Now; string ver = System.Threading.Interlocked.Read(ref Streams.Ver).ToString(CultureInfo.InvariantCulture);
            string k = (nw == null || nw.S == null ? "-" : nw.S.Start.Ticks + ":" + nw.S.Dips.Count) + "|" + System.Threading.Interlocked.Read(ref WatchKit.CacheVer) + "|" + DateTime.Now.Minute + "|" + SpeedTest.Busy + "|" + (SpeedTest.Last != null ? SpeedTest.Last.T.Ticks : 0);
            if (k != streamKey) { streamKey = k; streamVer = ver; streamCanvas.Invalidate(); }
            else if (ver != streamVer) { streamVer = ver; if (nw == null || !RedrawSpots(streamCanvas, "st.now")) streamCanvas.Invalidate(); }
        }
        static string Dur(TimeSpan t) { return t.TotalHours >= 1 ? ((int)t.TotalHours).ToString(CultureInfo.InvariantCulture) + " h " + t.Minutes.ToString("00", CultureInfo.InvariantCulture) + " min" : Math.Max(1, (int)t.TotalMinutes).ToString(CultureInfo.InvariantCulture) + " min"; }
        static string TallyText(WatchKit.Session s)
        {
            if (s.Dips.Count == 0) return L.T("smooth");
            var t = s.Tally().Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => WatchKit.BlameName(kv.Key, "stream server").ToLowerInvariant().Replace("your ", "").Replace("the ", "") + " " + kv.Value);
            return s.Dips.Count + (s.Dips.Count == 1 ? " dip · " : " dips · ") + string.Join(" · ", t);
        }

        void PaintStream(Graphics g)
        {
            g.TranslateTransform(streamCanvas.AutoScrollPosition.X, streamCanvas.AutoScrollPosition.Y);
            Begin(streamCanvas);
            float x = 18 * S, y = 16 * S, w = streamCanvas.ClientSize.Width - 36 * S;
            y = NowLine(g, streamCanvas, x, y, w); y = TryThis(g, streamCanvas, "stream", x, y, w, false);   // the NOW line: router, internet, Wi-Fi, PC (FullView.Gateway.cs)
            var all = WatchKit.Recent("stream");
            if (streamSel != "")
            {
                var rec = all.FirstOrDefault(z => z.Start.Ticks.ToString(CultureInfo.InvariantCulture) == streamSel);
                if (rec != null) { y = PaintStreamRecord(g, rec, x, y, w); Need(streamCanvas, y); return; }
                streamSel = "";
            }
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("Streaming"), Draw.fTitle, ink, x, y);
                // watching your team? its live score, from Sports (fetched at most every 5 min while a stream plays) - a stutter
                // below gets its verdict here, the score lives next door
                // AM-CG-272/273/281: it's your followed team's game (not necessarily what's playing), with how old the score is;
                // a score older than 15 minutes isn't shown as live, and the badge never runs past the title's room
                var lm = Cfg.Sports ? Sports.LiveMine() : null; int ageMin; lock (Sports.k) ageMin = (int)(DateTime.UtcNow - Sports.FetchedAt).TotalMinutes;
                if (lm != null && ageMin <= 15)
                {
                    int more = Sports.LiveMineCount() - 1;
                    string t = L.T("Your team") + "  " + lm.Away + " " + lm.AwayScore + " · " + lm.Home + " " + lm.HomeScore + (lm.Detail != "" ? "  (" + lm.Detail + ")" : "") + "  · " + (ageMin < 1 ? L.T("just now") : ageMin + " " + L.T("min ago")) + (more > 0 ? "  +" + more : "") + "   " + L.T("Sports ›");
                    float room = Math.Max(160 * S, w - g.MeasureString(L.T("Streaming"), Draw.fTitle).Width - 30 * S);
                    var ts = g.MeasureString(t, Draw.fSmall); float bwid = Math.Min(ts.Width + 26 * S, room);
                    var lr = new RectangleF(x + w - bwid, y + 4 * S, bwid, 26 * S); Theme.Fill(g, lr, 13 * S, Theme.LvSoft(2));
                    using (var lb = new SolidBrush(Theme.LvText(2))) using (var cf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(t, Draw.fSmall, lb, new RectangleF(lr.X + 13 * S, lr.Y + (26 * S - ts.Height) / 2, lr.Width - 20 * S, ts.Height + 2), cf);
                    Act(streamCanvas, lr, delegate { tab = "sports"; ShowTab(); SyncNav(); });
                }
                string lead = Voice.P("Is the buffering you, your Wi-Fi, your provider - or the stream? When something plays, in an app or a browser, I notice and keep an eye on it.", "Stream quality: who caused each dip. Streams are found by their servers' names - tabs and titles are never read.");
                // Test before I watch
                string tb = L.T(SpeedTest.Busy ? "Testing..." : "Can I stream 4K right now?"); var tbs = g.MeasureString(tb, Draw.fBodyB);
                var tbr = new RectangleF(x + w - tbs.Width - 32 * S, y - 2 * S, tbs.Width + 32 * S, 34 * S);
                Theme.Fill(g, tbr, 17 * S, Kit.Accent("stream")); using (var wb = new SolidBrush(Theme.OnBlue)) g.DrawString(tb, Draw.fBodyB, wb, tbr.X + 16 * S, tbr.Y + (34 * S - tbs.Height) / 2);
                Act(streamCanvas, tbr, delegate { if (Policy.NoSpeed) { streamNote = L.T("Speed tests are turned off by your IT team."); streamCanvas.Invalidate(); return; } if (!SpeedTest.Busy && AskCard.Show(this, L.T("The 4K check runs a speed test: it downloads and uploads up to 500 MB for 20-30 seconds, and anything else on your line slows down while it runs. Start it?"), "AdeeboMeter", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return; stream4k = true; StartSpeed(); streamCanvas.Invalidate(); });
                y += 32 * S;
                float lh = Draw.Measure(lead, Draw.fBody, (int)(w - tbr.Width - 20 * S)).Height; g.DrawString(lead, Draw.fBody, sub, new RectangleF(x, y, w - tbr.Width - 20 * S, lh + 4)); y += lh + 14 * S;
                if (streamNote != "") { using (var nb = new SolidBrush(Kit.Ink("stream"))) g.DrawString(streamNote, Draw.fSmall, nb, x, y); y += 22 * S; }
                if (!Cfg.Streams)
                {
                    y = Kit.Empty(g, x, y, Math.Min(w, 760 * S), "app", "stream", "Streaming is switched off", "Turn it on and AdeeboMeter notices when something plays and keeps an eye on it. Nothing extra goes on the network until a stream starts.", null);
                    var on = new RectangleF(x, y, 150 * S, 34 * S); Theme.Fill(g, on, 17 * S, Kit.Accent("stream"));
                    using (var wb = new SolidBrush(Theme.OnBlue)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(L.T("Turn it on"), Draw.fBodyB, wb, on, c);
                    Act(streamCanvas, on, delegate { Cfg.Streams = true; Cfg.Save(); streamCanvas.Invalidate(); });
                    Need(streamCanvas, on.Bottom + 20 * S); return;
                }
                {
                    WatchKit.Session ns = null; lock (Streams.k) if (Streams.Now != null) ns = Streams.Now.S;
                    var ld = ns != null && ns.Dips.Count > 0 ? ns.Dips.Last() : null;
                    if (ld != null && (DateTime.Now - ld.T).TotalMinutes < 3) y = PaintModeLine(g, "stream.dip", WatchKit.BlameName(ld.Blame, Streams.FarName) + ".", true, x, y, w);
                    else y = PaintModeLine(g, ns != null ? "stream.on" : "stream.none", ns != null ? ns.What : null, false, x, y, w);
                }
                if (stream4k) y = PaintStream4K(g, x, y, w);
                float sy0 = y; y = PaintStreamNow(g, x, y, w); LiveSpot(streamCanvas, "st.now", new RectangleF(x - 4 * S, sy0, w + 8 * S, y - sy0));   // a new sample redraws only this card (StreamTick)
                // the records
                if (all.Count > 0)
                {
                    g.DrawString(L.T("WHAT YOU WATCHED · KEPT 90 DAYS ON THIS PC · CLICK ONE TO SEE WHO CAUSED THE DIPS"), Draw.fTiny, muted, x, y); y += 20 * S;
                    var rows = all.Take(12).ToList(); float rh = 38 * S;
                    var card = new RectangleF(x, y, w, rows.Count * rh + 12 * S); Kit.Box(g, card); y += 6 * S;
                    using (var line = new Pen(Theme.Line2))
                        for (int i = 0; i < rows.Count; i++)
                        {
                            var s = rows[i]; var rr = new RectangleF(x + 8 * S, y, w - 16 * S, rh);
                            g.DrawString(s.Start.ToString(s.Start.Date == DateTime.Today ? "'Today' HH:mm" : "ddd d MMM", CultureInfo.CurrentCulture), Draw.fSmall, muted, rr.X + 10 * S, rr.Y + 11 * S);
                            using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                                g.DrawString(s.What + (s.Via != "" && s.Via != "you said so" ? " " + L.T("in") + " " + s.Via : "") + " · " + Dur(s.Length), Draw.fBodyB, ink, new RectangleF(rr.X + 110 * S, rr.Y + 10 * S, rr.Width - 400 * S, 20 * S), nf);
                            string tt = TallyText(s); var ts = g.MeasureString(tt, Draw.fSmall); var pill = new RectangleF(rr.Right - ts.Width - 26 * S, rr.Y + 8 * S, ts.Width + 18 * S, 22 * S);
                            Theme.Fill(g, pill, 11 * S, Theme.LvSoft(s.Dips.Count == 0 ? 0 : 1)); using (var pb = new SolidBrush(Theme.LvText(s.Dips.Count == 0 ? 0 : 1))) g.DrawString(tt, Draw.fSmall, pb, pill.X + 9 * S, pill.Y + (22 * S - ts.Height) / 2);
                            if (i + 1 < rows.Count) g.DrawLine(line, rr.X + 10 * S, rr.Bottom, rr.Right - 10 * S, rr.Bottom);
                            string key = s.Start.Ticks.ToString(CultureInfo.InvariantCulture); Act(streamCanvas, rr, delegate { streamSel = key; streamCanvas.AutoScrollPosition = Point.Empty; streamCanvas.Invalidate(); });
                            y += rh;
                        }
                    y += 22 * S;
                    y = PaintStreamPatterns(g, all, x, y, w);
                }
                else y = Kit.Empty(g, x, y, Math.Min(w, 760 * S), "time", "stream", "No streams recorded yet", "Play something - a match, a show, a live channel - for a couple of minutes. The record lands here with every dip and whose fault it was.", StreamPoints);
            }
            Need(streamCanvas, y + 10 * S);
        }
        static readonly string[] StreamPoints = { "YouTube, Netflix, Prime Video, Hulu, Disney+, Max, Twitch, Crunchyroll, Hotstar, DAZN and more - in an app or a browser", "IPTV and network video in VLC, Kodi and other players", "Kept on this PC for 90 days; about 2 KB a stream" };

        float PaintStreamNow(Graphics g, float x, float y, float w)
        {
            Streams.Live l; WatchKit.Session s = null; List<WatchKit.Sample> samples = null; string far = "";
            lock (Streams.k) { l = Streams.Now; if (l != null) { s = l.S; samples = l.Samples.ToList(); far = l.FarIp; } }
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                if (s == null)
                {
                    var card = new RectangleF(x, y, w, 96 * S); Kit.Box(g, card);
                    g.DrawString(L.T("Nothing streaming right now"), Draw.fName, ink, x + 20 * S, y + 16 * S);
                    g.DrawString(L.T("Recognised by their servers' names: ") + string.Join(", ", Streams.Known.Take(12).Select(z => z.Name)) + L.T(", IPTV players and more."), Draw.fSmall, sub, new RectangleF(x + 20 * S, y + 44 * S, w - 360 * S, 40 * S));
                    string mt = Streams.Manual != "" ? L.T("Watching: ") + Streams.Manual + L.T(" (stop)") : L.T("Not listed? Tell it what you're watching"); var ms = g.MeasureString(mt, Draw.fBodyB);
                    var mr = new RectangleF(card.Right - ms.Width - 40 * S, y + 30 * S, ms.Width + 24 * S, 34 * S); Theme.Fill(g, mr, 17 * S, Kit.Soft("stream"));
                    using (var mb = new SolidBrush(Kit.Ink("stream"))) g.DrawString(mt, Draw.fBodyB, mb, mr.X + 12 * S, mr.Y + (34 * S - ms.Height) / 2);
                    Act(streamCanvas, mr, delegate { if (Streams.Manual != "") { Streams.SetManual(""); streamCanvas.Invalidate(); } else AskWatching(new Point((int)mr.X, (int)(mr.Bottom + streamCanvas.AutoScrollPosition.Y))); });
                    return card.Bottom + 22 * S;
                }
                var last = samples.LastOrDefault(); var tally = s.Tally();
                int lv = s.Dips.Count == 0 ? 0 : 1; float h = 236 * S;
                var c2 = new RectangleF(x, y, w, h); Kit.Box(g, c2); Theme.Stroke(g, c2, 16 * S, Theme.Lv(lv), 2);
                float cx = x + 22 * S, cy = y + 16 * S;
                g.DrawString(L.T("NOW STREAMING") + (s.Via != "" ? " · " + L.T("in") + " " + s.Via : "") + (s.Via != "you said so" ? " · " + L.T("recognised by its servers, not by reading your tabs") : ""), Draw.fTiny, muted, cx, cy); cy += 20 * S;
                g.DrawString(s.What + " · " + Dur(DateTime.Now - s.Start) + " · " + (s.Dips.Count == 0 ? L.T("smooth so far") : s.Dips.Count + (s.Dips.Count == 1 ? L.T(" dip so far") : L.T(" dips so far"))), Draw.fTitle, ink, cx, cy); cy += 34 * S;
                if (s.Dips.Count > 0) { var ld = s.Dips.Last(); using (var ab = new SolidBrush(Theme.LvText(1))) g.DrawString(ld.T.ToString("HH:mm", CultureInfo.CurrentCulture) + " · " + WatchKit.BlameName(ld.Blame, Streams.FarName) + ": " + ld.Evidence, Draw.fBody, ab, new RectangleF(cx, cy, w - 44 * S, 22 * S)); }
                cy += 28 * S;
                // four numbers
                string[][] tiles = {
                    new[] { "DOWNLOAD NOW", last == null || double.IsNaN(last.Down) ? "-" : last.Down.ToString("0.0", CultureInfo.CurrentCulture) + " Mb/s", "the whole PC" },
                    new[] { "YOUR ROUTER", last == null ? "-" : last.RouterLost ? L.T("no reply") : Ms(last.Router) + " ms", last != null && last.Wifi && last.Signal >= 0 ? "Wi-Fi " + last.Signal + "%" : "" },
                    new[] { "THE INTERNET", last == null ? "-" : last.NetLost ? L.T("no reply") : Ms(last.Net) + " ms", "" },
                    new[] { "STREAM SERVER", double.IsNaN(s.FarNormal) ? (far == "" ? "-" : L.T("checking")) : Ms(s.FarNormal) + " ms", far == "" ? "" : L.T("usual delay") } };
                float tw = (w - 44 * S - 3 * 10 * S) / 4;
                for (int i = 0; i < 4; i++)
                {
                    var tr = new RectangleF(cx + i * (tw + 10 * S), cy, tw, 66 * S); Theme.Fill(g, tr, 12 * S, Theme.Soft);
                    g.DrawString(L.T(tiles[i][0]), Draw.fTiny, muted, tr.X + 12 * S, tr.Y + 9 * S);
                    g.DrawString(tiles[i][1], Draw.fName, ink, tr.X + 12 * S, tr.Y + 25 * S);
                    if (tiles[i][2] != "") g.DrawString(L.T(tiles[i][2]), Draw.fSmall, sub, tr.X + 12 * S, tr.Y + 47 * S);
                }
                cy += 78 * S;
                // the last 15 minutes: download (green), dips shaded
                var chart = new RectangleF(cx, cy, w - 44 * S, 34 * S);
                if (samples.Count >= 2)
                {
                    double max = Math.Max(5, samples.Where(z => !double.IsNaN(z.Down)).Select(z => z.Down).DefaultIfEmpty(5).Max()); var t0 = samples[0].T; double span = Math.Max(60, (samples.Last().T - t0).TotalSeconds);
                    foreach (var d in s.Dips.Where(z => z.T >= t0))
                        using (var db = new SolidBrush(Theme.LvSoft(1))) g.FillRectangle(db, chart.X + (float)((d.T - t0).TotalSeconds / span) * chart.Width, chart.Y, Math.Max(3 * S, (float)(d.Secs / span) * chart.Width), chart.Height);
                    var pts = samples.Where(z => !double.IsNaN(z.Down)).Select(z => new PointF(chart.X + (float)((z.T - t0).TotalSeconds / span) * chart.Width, chart.Bottom - (float)(z.Down / max) * chart.Height)).ToArray();
                    if (pts.Length >= 2) using (var p = new Pen(Theme.Green, 2 * S)) g.DrawLines(p, pts);
                }
                else g.DrawString(L.T("Gathering the first minute..."), Draw.fSmall, muted, chart.X, chart.Y + 8 * S);
                return c2.Bottom + 22 * S;
            }
        }
        void AskWatching(Point at)
        {
            var menu = new ContextMenuStrip { ShowImageMargin = false, Font = Draw.fBody };
            var box = new ToolStripTextBox { AutoSize = false, Width = Sc(260), Font = Draw.fBody };
            menu.Items.Add(new ToolStripLabel(L.T("What are you watching? (only for your own records)")));
            menu.Items.Add(box);
            var go = new ToolStripMenuItem(L.T("Start watching my stream")); go.Click += delegate { if (box.Text.Trim() != "") { Streams.SetManual(box.Text); streamCanvas.Invalidate(); } };
            box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && box.Text.Trim() != "") { Streams.SetManual(box.Text); menu.Close(); streamCanvas.Invalidate(); } };
            menu.Items.Add(go);
            menu.Closed += delegate { try { if (!IsDisposed) BeginInvoke((Action)menu.Dispose); else menu.Dispose(); } catch (InvalidOperationException) { menu.Dispose(); } };
            menu.Show(streamCanvas, at); box.Focus();
        }

        float PaintStream4K(Graphics g, float x, float y, float w)
        {
            var res = SpeedTest.Last; bool busy = SpeedTest.Busy;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                double gw; int sig; bool wifi; lock (M.L) { gw = M.GwPing.Avg(5); sig = M.WifiSignal; wifi = M.AdapterKind == "Wi-Fi"; }
                float h = busy || res == null ? 70 * S : 200 * S; var card = new RectangleF(x, y, w, h); Kit.Box(g, card);
                float cx = x + 22 * S, cy = y + 16 * S;
                g.DrawString(L.T("CAN I STREAM 4K RIGHT NOW?"), Draw.fTiny, muted, cx, cy); cy += 20 * S;
                if (busy || res == null) { g.DrawString(busy ? speedRun.Status : L.T("Starting the speed test..."), Draw.fBody, sub, cx, cy); return card.Bottom + 18 * S; }
                if (res.Error != "") { g.DrawString(L.T("The speed test didn't finish: ") + res.Error, Draw.fBody, sub, cx, cy); return card.Bottom + 18 * S; }
                double age = (DateTime.Now - res.T).TotalMinutes;   // AM-CG-149: a test from earlier (or another network) isn't "right now"
                if (age > 15) { g.DrawString(L.T("The last speed test was ") + Dur(DateTime.Now - res.T) + L.T(" ago - too old to say what you can stream right now. Press the button for a fresh one."), Draw.fBody, sub, new RectangleF(cx, cy, w - 44 * S, 40 * S)); return card.Bottom + 18 * S; }
                var v = Streams.Verdict4K(res.Down, gw, sig, wifi);
                g.DrawString(L.T("Download ") + res.Down.ToString("0", CultureInfo.CurrentCulture) + " Mb/s · " + res.T.ToString("HH:mm", CultureInfo.CurrentCulture), Draw.fName, ink, cx, cy); cy += 30 * S;
                float bw = (w - 44 * S - 30 * S) / 4;
                for (int i = 0; i < 4; i++)
                {
                    var p = v[i].Split('|'); int lv = p[1] == "safe" ? 0 : p[1] == "risky" ? 1 : 2; var br = new RectangleF(cx + i * (bw + 10 * S), cy, bw, 58 * S);
                    Theme.Fill(g, br, 12 * S, Theme.LvSoft(lv));
                    using (var lb = new SolidBrush(Theme.LvText(lv))) { g.DrawString(p[0], Draw.fBodyB, lb, br.X + 12 * S, br.Y + 8 * S); g.DrawString(L.T(p[1] == "safe" ? "Safe" : p[1] == "risky" ? "Risky" : "Not tonight") + " · " + L.T("needs ~") + p[2] + " Mb/s", Draw.fSmall, lb, br.X + 12 * S, br.Y + 32 * S); }
                }
                cy += 70 * S;
                g.DrawString(L.T(v[4]), Draw.fBody, sub, new RectangleF(cx, cy, w - 44 * S, 24 * S));
                return card.Bottom + 18 * S;
            }
        }

        float PaintStreamPatterns(Graphics g, List<WatchKit.Session> all, float x, float y, float w)
        {
            var last30 = all.Where(s => s.Start >= DateTime.Now.AddDays(-30)).ToList();
            var grid = WatchKit.DipGrid(last30); int total = last30.Sum(s => s.Dips.Count);
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                float half = (w - 16 * S) / 2, h = 250 * S;
                // when do your streams dip
                var a = new RectangleF(x, y, half, h); Kit.Box(g, a);
                float ax = a.X + 20 * S, ay = a.Y + 16 * S;
                g.DrawString(L.T("WHEN DO YOUR STREAMS DIP? · LAST 30 DAYS"), Draw.fTiny, muted, ax, ay); ay += 24 * S;
                int h0 = 16, h1 = 24; float cw = (half - 40 * S - 40 * S) / (h1 - h0), ch = 20 * S; int max = 1; for (int d = 0; d < 7; d++) for (int hh = h0; hh < h1; hh++) max = Math.Max(max, grid[d, hh]);
                string[] days = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
                for (int hh = h0; hh < h1; hh++) g.DrawString(hh.ToString(CultureInfo.InvariantCulture), Draw.fTiny, muted, ax + 40 * S + (hh - h0) * cw, ay);
                ay += 16 * S;
                foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
                {
                    g.DrawString(days[d], Draw.fTiny, muted, ax, ay + 4 * S);
                    for (int hh = h0; hh < h1; hh++)
                    {
                        int n = grid[d, hh]; var cr = new RectangleF(ax + 40 * S + (hh - h0) * cw, ay, cw - 3 * S, ch - 3 * S);
                        Theme.Fill(g, cr, 4 * S, n == 0 ? Theme.LvSoft(0) : Color.FromArgb(Math.Min(255, 70 + 185 * n / max), Theme.Amber));
                    }
                    ay += ch;
                }
                string pat = total == 0 ? L.T("No dips in the last 30 days.") : PatternLine(last30);
                g.DrawString(pat, Draw.fSmall, sub, new RectangleF(ax, a.Bottom - 46 * S, half - 40 * S, 40 * S));
                // the monthly report
                var b = new RectangleF(x + half + 16 * S, y, half, h); Kit.Box(g, b);
                float bx = b.X + 20 * S, by = b.Y + 16 * S; var month = DateTime.Now.Day <= 3 ? DateTime.Now.AddMonths(-1) : DateTime.Now;
                var ms = all.Where(s => s.Start.Year == month.Year && s.Start.Month == month.Month).ToList(); var dips = ms.SelectMany(s => s.Dips).ToList();
                g.DrawString(L.T("FOR YOUR PROVIDER · ") + month.ToString("MMMM", CultureInfo.CurrentCulture).ToUpperInvariant(), Draw.fTiny, muted, bx, by); by += 26 * S;
                string[][] nums = { new[] { "HOURS WATCHED", ms.Sum(s => s.Length.TotalHours).ToString("0.#", CultureInfo.CurrentCulture) }, new[] { "DIPS", dips.Count.ToString(CultureInfo.CurrentCulture) }, new[] { "PAST YOUR ROUTER", dips.Count(d => d.Blame == "provider").ToString(CultureInfo.CurrentCulture) } };
                float nw = (half - 40 * S - 20 * S) / 3;
                for (int i = 0; i < 3; i++)
                {
                    var nr = new RectangleF(bx + i * (nw + 10 * S), by, nw, 62 * S); Theme.Fill(g, nr, 10 * S, i == 2 && dips.Any(d => d.Blame == "provider") ? Theme.LvSoft(1) : Theme.Soft);
                    g.DrawString(L.T(nums[i][0]), Draw.fTiny, muted, nr.X + 10 * S, nr.Y + 8 * S); g.DrawString(nums[i][1], Draw.fName, ink, nr.X + 10 * S, nr.Y + 28 * S);
                }
                by += 78 * S;
                g.DrawString(L.T("Dates, times and the evidence for each one - ready to paste into a chat or an email to your provider."), Draw.fSmall, sub, new RectangleF(bx, by, half - 40 * S, 40 * S)); by += 46 * S;
                string bt = L.T((DateTime.Now - streamCopied).TotalSeconds < 3 ? "Copied" : "Copy report"); var bs = g.MeasureString(bt, Draw.fBodyB);
                var br = new RectangleF(bx, by, bs.Width + 30 * S, 34 * S); Theme.Fill(g, br, 17 * S, Kit.Accent("stream"));
                using (var wb = new SolidBrush(Theme.OnBlue)) g.DrawString(bt, Draw.fBodyB, wb, br.X + 15 * S, br.Y + (34 * S - bs.Height) / 2);
                Act(streamCanvas, br, delegate { try { Clipboard.SetText(WatchKit.MonthReport(all, month, "Streaming")); streamCopied = DateTime.Now; } catch (Exception e) { if (!(e is System.Runtime.InteropServices.ExternalException || e is System.Threading.ThreadStateException)) throw; } streamCanvas.Invalidate(); });
                return a.Bottom + 20 * S;
            }
        }
        static string PatternLine(List<WatchKit.Session> ss)
        {
            var dips = ss.SelectMany(s => s.Dips).ToList(); if (dips.Count == 0) return "";
            int peak = dips.GroupBy(d => d.T.Hour).OrderByDescending(g => g.Count()).First().Key;
            int past = dips.Count(d => d.Blame == "provider" || d.Blame == "far"), home = dips.Count(d => d.Blame == "wifi" || d.Blame == "home" || d.Blame == "pc");
            return L.T("Most dips around ") + peak.ToString("00", CultureInfo.InvariantCulture) + ":00. " + (past > home ? past + L.T(" of ") + dips.Count + L.T(" were past your router - your provider's or the service's busy hours, not your home.") : home + L.T(" of ") + dips.Count + L.T(" were in your home or PC - Wi-Fi first."));
        }

        // ---------- one record: who caused the dips
        float PaintStreamRecord(Graphics g, WatchKit.Session s, float x, float y, float w)
        {
            var bk = Kit.Back(g, "Streaming", x, y, "stream", false); Act(streamCanvas, bk, delegate { streamSel = ""; streamCanvas.AutoScrollPosition = Point.Empty; streamCanvas.Invalidate(); }); y += bk.Height + 16 * S;
            string farName = Streams.FarName;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString((s.Start.ToString("ddd d MMM · HH:mm", CultureInfo.CurrentCulture) + " - " + s.End.ToString("HH:mm", CultureInfo.CurrentCulture) + " · " + s.What + (s.Via != "" ? " · " + s.Via : "")).ToUpperInvariant(), Draw.fTiny, muted, x, y); y += 20 * S;
                var tally = s.Tally(); var top = tally.OrderByDescending(kv => kv.Value).First();
                string head = s.Dips.Count == 0 ? L.T("Smooth all the way - ") + Dur(s.Length) + L.T(" without a dip") : s.Dips.Count + (s.Dips.Count == 1 ? L.T(" dip in ") : L.T(" dips in ")) + Dur(s.Length) + " - " + (top.Value * 2 > s.Dips.Count ? L.T("mostly ") : "") + WatchKit.BlameName(top.Key, farName).ToLowerInvariant();
                g.DrawString(head, Draw.fTitle, ink, x, y); y += 40 * S;
                // the tally
                string[] order = { "pc", "wifi", "home", "provider", "far", "unsure" }; float tw = (w - 5 * 10 * S) / 6;
                for (int i = 0; i < order.Length; i++)
                {
                    int n = tally[order[i]]; bool lead = n > 0 && order[i] == top.Key; var tr = new RectangleF(x + i * (tw + 10 * S), y, tw, 66 * S);
                    if (lead) { Theme.Fill(g, tr, 14 * S, Theme.LvSoft(1)); Theme.Stroke(g, tr, 14 * S, Theme.Amber, 2); } else Kit.Box(g, tr, 14 * S);
                    using (var nb = new SolidBrush(n > 0 ? Theme.LvText(1) : Theme.Muted)) { g.DrawString(WatchKit.BlameName(order[i], Streams.FarName).ToUpperInvariant(), Draw.fTiny, nb, tr.X + 12 * S, tr.Y + 9 * S); g.DrawString(n.ToString(CultureInfo.CurrentCulture), Draw.fHero, nb, tr.X + 10 * S, tr.Y + 22 * S); }
                }
                y += 80 * S;
                // the whole session: download (green), the stream server's delay (purple), dips shaded
                var card = new RectangleF(x, y, w, 170 * S); Kit.Box(g, card);
                g.DrawString(L.T("THE WHOLE SESSION · download (green) · stream server delay (purple) · dips shaded"), Draw.fTiny, muted, x + 18 * S, y + 12 * S);
                var ch = new RectangleF(x + 18 * S, y + 36 * S, w - 36 * S, 110 * S); double span = Math.Max(1, s.Length.TotalMinutes);
                foreach (var d in s.Dips) using (var db = new SolidBrush(Theme.LvSoft(d.Blame == "provider" ? 2 : 1))) g.FillRectangle(db, ch.X + (float)((d.T - s.Start).TotalMinutes / span) * ch.Width, ch.Y, Math.Max(4 * S, (float)(d.Secs / 60.0 / span) * ch.Width), ch.Height);
                if (s.Line.Count >= 2)
                {
                    double maxD = Math.Max(5, s.Line.Max(p => p[1])), maxF = Math.Max(100, s.Line.Max(p => p[2]));
                    var pd = s.Line.Select(p => new PointF(ch.X + (float)(p[0] / span) * ch.Width, ch.Y + ch.Height * 0.55f - (float)(p[1] / maxD) * ch.Height * 0.5f)).ToArray();
                    using (var pn = new Pen(Theme.Green, 2 * S)) g.DrawLines(pn, pd);
                    var pf = s.Line.Where(p => p[2] >= 0).Select(p => new PointF(ch.X + (float)(p[0] / span) * ch.Width, ch.Bottom - (float)(p[2] / maxF) * ch.Height * 0.4f)).ToArray();
                    if (pf.Length >= 2) using (var pn = new Pen(Theme.C("#8250DF"), 2 * S)) g.DrawLines(pn, pf);
                }
                g.DrawString(s.Start.ToString("HH:mm", CultureInfo.CurrentCulture), Draw.fTiny, muted, ch.X, ch.Bottom + 4 * S);
                var es = g.MeasureString(s.End.ToString("HH:mm", CultureInfo.CurrentCulture), Draw.fTiny); g.DrawString(s.End.ToString("HH:mm", CultureInfo.CurrentCulture), Draw.fTiny, muted, ch.Right - es.Width, ch.Bottom + 4 * S);
                y = card.Bottom + 18 * S;
                // each dip and the evidence
                if (s.Dips.Count > 0)
                {
                    g.DrawString(L.T("EACH DIP AND THE EVIDENCE"), Draw.fTiny, muted, x, y); y += 20 * S;
                    float rows = 0; foreach (var d in s.Dips) rows += Math.Max(36 * S, Draw.Measure(d.Evidence, Draw.fBody, (int)(w - 420 * S)).Height + 16 * S);
                    var dc = new RectangleF(x, y, w, rows + 12 * S); Kit.Box(g, dc); y += 6 * S;
                    using (var line = new Pen(Theme.Line2))
                        for (int i = 0; i < s.Dips.Count; i++)
                        {
                            var d = s.Dips[i]; float rh = Math.Max(36 * S, Draw.Measure(d.Evidence, Draw.fBody, (int)(w - 420 * S)).Height + 16 * S);
                            g.DrawString(d.T.ToString("HH:mm", CultureInfo.CurrentCulture), Draw.fBodyB, ink, x + 18 * S, y + 9 * S);
                            if (Cfg.History)   // the dip's minute in the Time machine (FullView.Gateway.cs)
                            {
                                var dt = d.T; string what = s.What; var tr0 = new RectangleF(x + w - 150 * S, y + 9 * S, 64 * S, 20 * S);
                                using (var bl = new SolidBrush(Theme.BlueText)) g.DrawString(L.T("Replay") + " ›", Draw.fSmall, bl, tr0.X, tr0.Y + 2 * S);
                                Act(streamCanvas, tr0, delegate { OpenTimeAt(dt, L.T("Streaming") + " - " + what + " " + L.T("dip")); });
                            }
                            using (var bb = new SolidBrush(Theme.LvText(d.Blame == "provider" ? 2 : 1))) g.DrawString(WatchKit.BlameName(d.Blame, farName), Draw.fBodyB, bb, x + 80 * S, y + 9 * S);
                            g.DrawString(d.Evidence, Draw.fBody, sub, new RectangleF(x + 270 * S, y + 9 * S, w - 420 * S, rh));
                            g.DrawString(d.Secs + " s", Draw.fSmall, muted, x + w - 80 * S, y + 11 * S);
                            y += rh; if (i + 1 < s.Dips.Count) g.DrawLine(line, x + 16 * S, y, x + w - 16 * S, y);
                        }
                    y += 18 * S;
                }
                // in one line + copy
                string one = s.Dips.Count == 0 ? L.T("Your connection held up the whole time.") : OneLine(s, farName);
                var or = new RectangleF(x, y, w, 44 * S); Theme.Fill(g, or, 12 * S, Kit.Soft("stream"));
                using (var ob = new SolidBrush(Kit.Ink("stream"))) g.DrawString(L.T("In one line: ") + one, Draw.fBody, ob, new RectangleF(or.X + 16 * S, or.Y + 12 * S, w - 32 * S, 24 * S));
                y = or.Bottom + 14 * S;
                string ct = L.T((DateTime.Now - streamCopied).TotalSeconds < 3 ? "Copied" : "Copy for my provider"); var cs = g.MeasureString(ct, Draw.fBodyB);
                var cr = new RectangleF(x, y, cs.Width + 30 * S, 34 * S); Theme.Fill(g, cr, 17 * S, Theme.Soft); g.DrawString(ct, Draw.fBodyB, ink, cr.X + 15 * S, cr.Y + (34 * S - cs.Height) / 2);
                var rec = s; Act(streamCanvas, cr, delegate { try { Clipboard.SetText(RecordText(rec)); streamCopied = DateTime.Now; } catch (Exception e) { if (!(e is System.Runtime.InteropServices.ExternalException || e is System.Threading.ThreadStateException)) throw; } streamCanvas.Invalidate(); });
                // the lesson that fits what went wrong most, right where it's needed
                // 1.1.6: this PC to the stream's server in the Network Lab (the CDN server it played from)
                if (s.Far != "") { var pr = LabPill(g, streamCanvas, "Show my path", cr.Right + 10 * S, cr.Y, false, delegate { OpenLab(LabLink.PathJson(M, "view", rec.Far, (rec.What == "" ? "Stream" : rec.What) + " server", rec.FarNormal, MinOf(M.GwPing), MinOf(M.NetPing)), false); }); cr = new RectangleF(cr.X, cr.Y, pr.Right - cr.X, cr.Height); }
                if (s.Dips.Count > 0) { float lx2 = InvLink(g, streamCanvas, cr.Right + 16 * S, cr.Y + 8 * S, top.Key == "wifi" ? "drops" : "buffer", "stream", OneLine(s, farName), s.Start, s.End > s.Start ? s.End : DateTime.Now); LessonLink(g, streamCanvas, WatchKit.Lesson(top.Key, "stream"), lx2, cr.Y + 8 * S); }
                return cr.Bottom + 20 * S;
            }
        }
        static string OneLine(WatchKit.Session s, string farName)
        {
            var t = s.Tally(); int home = t["pc"] + t["wifi"] + t["home"];
            var parts = t.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => kv.Value + " " + WatchKit.BlameName(kv.Key, farName).ToLowerInvariant());
            string lead = home == 0 && t["unsure"] == 0 ? L.T("no dip was on your home network. ") : home == 0 ? L.T("no dip was clearly on your home network (some couldn't be pinned on anyone). ") : "";   // AM-CG-140: absence of blame isn't proof
            return lead + L.T("The dips: ") + string.Join(", ", parts) + ".";
        }
        static string RecordText(WatchKit.Session s)
        {
            var sb = new System.Text.StringBuilder(); string farName = Streams.FarName;
            sb.AppendLine((s.Via == "you said so" ? "A stream" : s.What) + " on " + s.Start.ToString("ddd d MMM yyyy, HH:mm", CultureInfo.CurrentCulture) + " - " + s.End.ToString("HH:mm", CultureInfo.CurrentCulture) + " (" + Dur(s.Length) + "), measured by AdeeboMeter on my PC");
            sb.AppendLine(s.Dips.Count == 0 ? "No dips." : OneLine(s, farName));
            foreach (var d in s.Dips) sb.AppendLine(d.T.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + d.Secs + " s  " + WatchKit.BlameName(d.Blame, farName) + ": " + d.Evidence);
            return sb.ToString();
        }
    }
}
