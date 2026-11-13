// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Gamer Zone tab
    // Your games (installed ones found automatically, one click to monitor), a card per monitored game with each server's
    // ping / jitter / loss / grade, the best region over 7 days, and who caused each spike.
    sealed partial class FullView
    {
        Panel gamerPage; DB gamerCanvas; string gamerNote = "", gamerKey = ""; volatile bool valveBusy;
        Panel BuildGamerPage()
        {
            gamerPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            gamerCanvas = new DB { Name = "gamerCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            gamerCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintGamer(e.Graphics); }; Wire(gamerCanvas);
            gamerPage.Controls.Add(gamerCanvas);
            return gamerPage;
        }
        void ClickGame(Game g)
        {
            if (g.Soon) return;
            if (Policy.NoGamer) { gamerNote = "Game pings are turned off by your IT team (AdeeboMeter.policy)."; gamerCanvas.Invalidate(); return; }
            var mon = Games.Monitored();
            if (!mon.Contains(g.Id))
            {
                Games.SetMonitored(g.Id, true);
                gamerNote = "Monitoring " + g.Name + (Cfg.GameAuto ? " - and from now on it starts by itself when the game opens." : ".");
                if (g.Valve && Valve.Cached() == null) RefreshValve(g);
            }
            gamerCanvas.Invalidate();
        }
        void RefreshValve(Game g)
        {
            if (valveBusy) return; valveBusy = true; gamerNote = "Getting Valve's server list (one request to api.steampowered.com)...";
            int app = g.SteamIds.Length > 0 ? g.SteamIds[0] : 730;
            Task.Factory.StartNew(() =>
            {
                string r = Valve.Refresh(app);
                try { BeginInvoke((Action)delegate { valveBusy = false; gamerNote = r; gamerCanvas.Invalidate(); }); } catch (InvalidOperationException) { valveBusy = false; }
            });
        }
        void EditGame(Game gm, bool asNew)
        {
            using (var d = new GameEditor(gm, asNew))
            {
                d.ShowDialog(this);
                if (d.Saved) { Games.Reload(); if (asNew && !string.IsNullOrEmpty(gm.Id)) Games.SetMonitored(gm.Id, true); gamerNote = asNew ? "Added " + gm.Name + " - monitoring it now." : "Saved " + gm.Name + "'s servers."; }
            }
            gamerCanvas.Invalidate();
        }
        static Color Hex(string h) { try { return Theme.C(h); } catch (Exception e) { if (!(e is ArgumentException || e is FormatException)) throw; return Theme.Grey; } }
        static string Ms(double v) { return double.IsNaN(v) ? "-" : v < 10 ? v.ToString("0.0", CultureInfo.InvariantCulture) : Math.Round(v).ToString(CultureInfo.InvariantCulture); }
        static Color GradeColor(string grade) { return grade == "Competitive" ? Theme.Green : grade == "Casual" ? Theme.Amber : grade == "Laggy" ? Theme.Red : Theme.Grey; }

        void PaintGamer(Graphics g)
        {
            g.TranslateTransform(gamerCanvas.AutoScrollPosition.X, gamerCanvas.AutoScrollPosition.Y);
            Begin(gamerCanvas);
            float x = 18 * S, y = 16 * S, w = gamerCanvas.ClientSize.Width - 36 * S;
            var games = Games.All; var mon = Games.Monitored(); var inst = GameFinder.Installed(games);
            string playing = GamePing.Me.Playing;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            using (var blue = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            using (var nowrap = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                var pg = playing == null ? null : games.FirstOrDefault(z => z.Id == playing);
                string head = pg != null ? "Playing " + pg.Name + (Cfg.GameFast ? " · up to " + GamePing.MaxServers + " servers checked once a second" : " · checked every " + Math.Max(1, Cfg.GameBgSec) + " s (once a second is off in Settings)") : mon.Count == 0 ? "Your games" : "Your games · watching in the background " + (Cfg.GameBgSec > 0 ? "every " + Cfg.GameBgSec + " s" : "(off in Settings)");
                g.DrawString(head, Draw.fTitle, ink, x, y); y += 32 * S;
                string lead = mon.Count == 0 ? "Games on this PC are already here. Click one to start monitoring it - nothing is sent to a game company's servers until you do." : "Click another game to add it. Monitoring starts by itself whenever a game you added opens" + (Cfg.GameAuto ? "." : " (that's off in Settings).");
                var lh = Draw.Measure(lead, Draw.fBody, (int)w).Height; g.DrawString(lead, Draw.fBody, sub, new RectangleF(x, y, w, lh + 4)); y += lh + 10 * S;
                if (gamerNote != "") { var nh = Draw.Measure(gamerNote, Draw.fSmall, (int)w).Height; g.DrawString(gamerNote, Draw.fSmall, blue, new RectangleF(x, y, w, nh + 4)); y += nh + 10 * S; }

                // no game yet: what watching one looks like - made-up, under a banner
                if (mon.Count == 0 && pg == null) y = PaintExample(g, gamerCanvas, "page.gamer", x, y, w);
                // this match: Start / Stop / Mark lag, then the summary
                if (mon.Count > 0) { y = PaintSession(g, games, x, y, w); PageUsed("gamer"); }

                // tiles: monitored first, then installed, then the rest; GTA VI-style "soon" last
                var order = games.OrderBy(z => mon.Contains(z.Id) ? 0 : inst.Contains(z.Id) ? 1 : z.Soon ? 3 : 2).ToList();
                int cols = Math.Max(2, Math.Min(6, (int)(w / (190 * S)))); float tw = (w - (cols - 1) * 10 * S) / cols, th = 64 * S;
                for (int i = 0; i < order.Count; i++)
                {
                    var gm = order[i]; var r = new RectangleF(x + (i % cols) * (tw + 10 * S), y + (i / cols) * (th + 10 * S), tw, th);
                    bool on = mon.Contains(gm.Id), here = inst.Contains(gm.Id);
                    Theme.Fill(g, r, 10 * S, on ? Theme.BlueSoft : Theme.Soft); Theme.Stroke(g, r, 10 * S, on ? Theme.Blue : here ? Theme.Blue : Theme.Line, on ? 2 : 1);
                    var ic = new RectangleF(r.X + 10 * S, r.Y + 14 * S, 30 * S, 30 * S);
                    using (var ib = new SolidBrush(gm.Soon ? Theme.Grey : Hex(gm.Color))) g.FillRectangle(ib, ic);
                    using (var wb = new SolidBrush(Color.White)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(System.Globalization.StringInfo.GetNextTextElement(string.IsNullOrEmpty(gm.Name) ? "?" : gm.Name), Draw.fBodyB, wb, ic, c);
                    using (var nb = new SolidBrush(gm.Soon ? Theme.Muted : Theme.Ink)) g.DrawString(gm.Name, Draw.fBodyB, nb, new RectangleF(r.X + 48 * S, r.Y + 12 * S, r.Width - 56 * S, 20 * S), nowrap);
                    string state;
                    if (gm.Soon) state = "Coming when it launches on PC";
                    else if (on)
                    {
                        var best = GamePing.Me.Best(gm); double now = double.NaN;
                        if (best != null) now = GameStat.Avg(GamePing.Me.Stat(gm.Id, best).Recent(10));
                        state = (gm.Id == playing ? "Playing · " : "Monitoring · ") + (best != null && !double.IsNaN(now) ? Ms(now) + " ms " + best.Name : "starting...");
                    }
                    else state = here ? "On this PC · click to monitor" : "Click to add";
                    using (var sb = new SolidBrush(on ? (Theme.Dark ? Theme.Ink : Theme.BlueDark) : Theme.Muted)) g.DrawString(state, Draw.fSmall, sb, new RectangleF(r.X + 48 * S, r.Y + 34 * S, r.Width - 56 * S, 18 * S), nowrap);
                    var gm2 = gm; if (!gm.Soon) Act(gamerCanvas, r, delegate { ClickGame(gm2); });
                }
                {
                    int i = order.Count; var r = new RectangleF(x + (i % cols) * (tw + 10 * S), y + (i / cols) * (th + 10 * S), tw, th);
                    Theme.Stroke(g, r, 10 * S, Theme.Line, 1);
                    g.DrawString(L.T("+ Add your own game"), Draw.fBodyB, blue, new RectangleF(r.X + 14 * S, r.Y + 12 * S, r.Width - 20 * S, 20 * S), nowrap);
                    g.DrawString(L.T("any game, any server IP"), Draw.fSmall, muted, new RectangleF(r.X + 14 * S, r.Y + 34 * S, r.Width - 20 * S, 18 * S), nowrap);
                    Act(gamerCanvas, r, delegate { EditGame(new Game { Name = "", Custom = true }, true); });
                }
                y += ((order.Count + 1 + cols - 1) / cols) * (th + 10 * S) + 14 * S;

                // one card per monitored game, the one being played first
                foreach (var gm in games.Where(z => mon.Contains(z.Id)).OrderBy(z => z.Id == playing ? 0 : 1))
                    y = PaintGameCard(g, gm, gm.Id == playing, x, y, w);
                // the ping map: the game being played, else the first watched one
                y = PaintPingMap(g, games.Where(z => mon.Contains(z.Id) && !z.Soon).OrderBy(z => z.Id == playing ? 0 : 1).FirstOrDefault(), x, y, w);

                // spikes and who caused them
                List<Spike> sp; lock (GamePing.Me.Spikes) sp = GamePing.Me.Spikes.Take(8).ToList();
                if (sp.Count > 0)
                {
                    y = SetHead(g, "WHY DID IT SPIKE?", x, y, w);
                    foreach (var s in sp)
                    {
                        string gname = games.Where(z => z.Id == s.Game).Select(z => z.Name).FirstOrDefault() ?? s.Game;
                        string t = s.T.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "   " + gname + " " + s.Server + " " + Ms(s.Normal) + " -> " + Ms(s.Ms) + " ms   ·   " + s.Blame;
                        using (var d = new SolidBrush(s.Blame.StartsWith("past", StringComparison.Ordinal) ? Theme.Red : Theme.Amber)) g.FillEllipse(d, x + 2 * S, y + 6 * S, 8 * S, 8 * S);
                        g.DrawString(t, Draw.fBody, ink, new RectangleF(x + 18 * S, y, w - 18 * S, 22 * S), nowrap); y += 24 * S;
                    }
                }
                y += 8 * S;
                string foot = "How it measures: a ping, or a timed TCP connection when a server ignores ping - never game traffic. \"Estimate\" rows measure the cloud region the game runs in; Valve rows are Valve's own servers. At most " + GamePing.MaxServers + " servers while you play (16 on the Gamer preset), " + GamePing.MaxPerSecond + " probes a second.";
                var fh = Draw.Measure(foot, Draw.fSmall, (int)w).Height; g.DrawString(foot, Draw.fSmall, muted, new RectangleF(x, y, w, fh + 4)); y += fh + 16 * S;
            }
            var need = new Size(0, (int)(y + 20 * S)); SetNeed(gamerCanvas, need);
        }

        DateTime gzCopied; volatile bool findBusy;
        void FindMatch(Game gm)
        {
            if (findBusy) return; findBusy = true; gamerNote = "Listening to " + gm.Name + "'s own ports for 8 seconds...";
            string iface; lock (M.L) iface = M.IfaceId;
            Task.Factory.StartNew(() =>
            {
                string ip; string err = MatchFinder.Find(gm, iface, out ip);
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        findBusy = false;
                        if (ip != null && Games.ValidHost(ip))
                        {
                            var have = gm.Servers.FirstOrDefault(z => z.Host == ip);
                            if (have == null)
                            {
                                gm.Servers.RemoveAll(z => z.Name == "My match");
                                gm.Servers.Insert(0, new GameServer { Name = "My match", Where = "found " + DateTime.Now.ToString("d MMM HH:mm", CultureInfo.CurrentCulture), Host = ip, Port = -1, Kind = "exact" });
                                if (gm.Servers.Count > 12) gm.Servers.RemoveAt(gm.Servers.Count - 1);
                                Games.SaveGame(gm); Games.Reload();
                            }
                            gamerNote = "Busiest connection: " + ip + " - most likely the match server (in a peer-to-peer game it can be another player). " + (have == null ? "Added as \"My match\" and watched from now on." : "It's already in the list (" + have.Name + ").");
                        }
                        else gamerNote = err ?? "Couldn't tell which server the match uses.";
                        gamerCanvas.Invalidate();
                    });
                }
                catch (InvalidOperationException) { findBusy = false; }
            });
            gamerCanvas.Invalidate();
        }
        float PaintSession(Graphics g, List<Game> games, float x, float y, float w)
        {
            var gp = GamePing.Me; var ss = gp.Session ?? gp.LastSession; bool open = gp.Session != null && gp.Session.Open;
            var mon = Games.Monitored();
            var target = games.FirstOrDefault(z => z.Id == gp.Playing) ?? games.FirstOrDefault(z => mon.Contains(z.Id) && z.Servers.Count > 0);
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            using (var blue = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            {
                float bx = x;
                Func<string, bool, Action, float> btn = (t, primary, a) =>
                { t = L.T(t);
                    var sz = g.MeasureString(t, Draw.fBodyB); var r = new RectangleF(bx, y, sz.Width + 28 * S, 34 * S);
                    Theme.Fill(g, r, 17 * S, primary ? Theme.Blue : Theme.Soft); if (!primary) Theme.Stroke(g, r, 17 * S, Theme.Line, 1);
                    using (var tb = new SolidBrush(primary ? Color.White : Theme.Ink)) g.DrawString(t, Draw.fBodyB, tb, r.X + 14 * S, r.Y + (34 * S - sz.Height) / 2);
                    Act(gamerCanvas, r, a); bx += r.Width + 10 * S; return r.Width;
                };
                if (open)
                {
                    btn("Mark lag", true, delegate { gp.Session.Mark(); gamerNote = "Marked - the 30 seconds around it are in the match summary."; gamerCanvas.Invalidate(); });
                    btn("Stop session", false, delegate { gp.EndSession(); gamerCanvas.Invalidate(); });
                }
                else if (target != null) btn("Start session · " + target.Name, true, delegate { gp.StartSession(target, true); gamerNote = "Session started - press Mark lag whenever it feels wrong."; gamerCanvas.Invalidate(); });
                if (gp.Playing != null)
                {
                    var pg = games.FirstOrDefault(z => z.Id == gp.Playing);
                    if (pg != null) btn(findBusy ? "Finding your match server..." : "Find my match server", false, delegate { FindMatch(pg); });
                    if (pg != null) btn("See the packet journey", false, delegate { GameJourney(pg); });
                }
                btn("Test under load...", false, delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool("bloat"); });
                var rg = ss != null ? games.FirstOrDefault(z => z.Id == ss.GameId) : target;
                btn("Lag graph for streaming", false, delegate { StreamOverlay.Toggle(M); });
                btn("Wi-Fi vs cable...", false, delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool("gamecompare"); if (rg != null && tbBoxes.Count > 0) tbBoxes[0].Box.Text = rg.Id; });
                btn("Report for my ISP...", false, delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool("ispreport"); if (rg != null && tbBoxes.Count > 1) { tbBoxes[0].Box.Text = rg.Id; tbBoxes[1].Box.Text = "2"; } });
                y += 44 * S;
                if (ss == null) return y + 6 * S;

                var rows = ss.All(); var gv = rows.Select(r => r.Game).ToArray();
                DateTime end = ss.Open ? DateTime.Now : ss.End;
                List<Spike> sp; lock (gp.Spikes) sp = gp.Spikes.Where(z => z.Game == ss.GameId && z.T >= ss.Start && z.T <= end).ToList();
                y = SetHead(g, (open ? "THIS MATCH · " : "LAST MATCH · ") + ss.GameName.ToUpperInvariant() + " · " + ss.Server.ToUpperInvariant() + " · " + ss.Start.ToString("HH:mm", CultureInfo.CurrentCulture) + " - " + (open ? "NOW" : end.ToString("HH:mm", CultureInfo.CurrentCulture)), x, y, w);
                string line = rows.Count == 0 ? "Collecting - one row a second while the session is open." :
                    Math.Round((end - ss.Start).TotalMinutes) + " min · avg " + Ms(GameStat.Avg(gv)) + " ms · 95% " + Ms(GameStat.Pct(gv, 0.95)) + " ms · jitter " + Ms(GameStat.Jitter(gv)) + " ms · loss " + (double.IsNaN(GameStat.Loss(gv)) ? "-" : GameStat.Loss(gv).ToString("0.#", CultureInfo.InvariantCulture) + "%") + " · " + sp.Count + (sp.Count == 1 ? " spike" : " spikes") + (sp.Count > 0 ? " (" + string.Join(", ", sp.GroupBy(z => z.Blame).Select(gr => gr.Count() + " " + gr.Key)) + ")" : "");
                var lh = Draw.Measure(line, Draw.fBody, (int)w).Height; g.DrawString(line, Draw.fBody, ink, new RectangleF(x, y, w, lh + 4)); y += lh + 8 * S;
                // the worst moments and the player's marks, each with the 30 seconds around it
                var moments = sp.OrderByDescending(z => z.Ms).Take(3).Select(z => Tuple.Create(z.T, "Spike " + Ms(z.Normal) + " -> " + Ms(z.Ms) + " ms · " + z.Blame)).ToList();
                moments.AddRange(ss.Marks.Select(m => Tuple.Create(m, "You marked lag here")));
                foreach (var m in moments.OrderBy(z => z.Item1).Take(8))
                {
                    var ar = ss.Around(m.Item1, 15);
                    g.DrawString(m.Item1.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  " + m.Item2, Draw.fBodyB, ink, x, y); y += 22 * S;
                    float sw = Math.Min(260 * S, (w - 30 * S) / 4);
                    string[] names = { "game ms", "router ms", "Wi-Fi %", "traffic Mb/s" };
                    for (int i = 0; i < 4; i++)
                    {
                        var sr = new RectangleF(x + i * (sw + 10 * S), y, sw, 30 * S); Theme.Fill(g, sr, 5 * S, Theme.Soft);
                        double[] series = i == 0 ? ar.Select(r => r.Game).ToArray() : i == 1 ? ar.Select(r => r.Router).ToArray() : i == 2 ? ar.Select(r => r.Signal < 0 ? double.NaN : (double)r.Signal).ToArray() : ar.Select(r => (double.IsNaN(r.Down) ? 0 : r.Down) + (double.IsNaN(r.Up) ? 0 : r.Up)).ToArray();
                        double cap = 1; foreach (var v in series) if (!double.IsNaN(v) && v > cap) cap = v;
                        Theme.Spark(g, series, sr, i == 0 ? Theme.Blue : i == 1 ? Theme.Amber : i == 2 ? Theme.Green : Theme.Orange, cap * 1.15, false);
                        using (var lb = new SolidBrush(Theme.Muted)) g.DrawString(names[i], Draw.fTiny, lb, sr.X + 4 * S, sr.Y + 2 * S);
                    }
                    y += 38 * S;
                }
                var cp = new RectangleF(x, y, 360 * S, 22 * S);
                g.DrawString((DateTime.Now - gzCopied).TotalSeconds < 3 ? "Copied - internal addresses removed" : "Copy match report (internal addresses removed)", Draw.fBodyB, blue, cp.X, cp.Y);
                var ss2 = ss; Act(gamerCanvas, cp, delegate
                {
                    try { string ssid; lock (M.L) ssid = M.Ssid; Clipboard.SetText(ss2.Report(sp, ssid)); gzCopied = DateTime.Now; } catch (Exception e) { CopyFailed(e); }
                    gamerCanvas.Invalidate();
                });
                y += 34 * S;
            }
            return y;
        }
        float PaintGameCard(Graphics g, Game gm, bool playing, float x, float y, float w)
        {
            var week = GameHistory.Week(gm.Id); var best = GamePing.Me.Best(gm);
            float top = y, pad = 14 * S, rowH = 34 * S;
            var rows = gm.Servers.Take(GamePing.MaxServers).ToList();
            float h = pad + 30 * S + 22 * S + rows.Count * rowH + 40 * S;
            var card = new RectangleF(x, y, w, h);
            Theme.Fill(g, card, 12 * S, Theme.Card); Theme.Stroke(g, card, 12 * S, playing ? Theme.Blue : Theme.Line, playing ? 2 : 1);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub))
            using (var blue = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            using (var nowrap = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            using (var right = new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap })
            {
                float cx = x + pad, cy = y + pad, cw = w - 2 * pad;
                g.DrawString(gm.Name + (playing ? "  ·  PLAYING NOW" : ""), Draw.fHead, ink, cx, cy);
                // best region over 7 days
                string bt = "";
                if (best != null)
                {
                    double[] wk; bool known = week.TryGetValue(best.Name, out wk) && wk[4] >= 1;
                    bt = known ? "Best for you over " + (int)wk[4] + (wk[4] == 1 ? " day: " : " days: ") + best.Name + " · " + Ms(wk[0]) + " ms average, " + wk[1].ToString("0.#", CultureInfo.InvariantCulture) + "% loss, trouble on " + (int)wk[3] + " evening" + (wk[3] == 1 ? "" : "s")
                               : "Best right now: " + best.Name + " - the week's verdict needs a few days of history";
                }
                var stop = new RectangleF(x + w - pad - 120 * S, cy, 120 * S, 22 * S);
                g.DrawString(L.T("Stop monitoring"), Draw.fSmall, blue, stop, right);
                var gm2 = gm; Act(gamerCanvas, stop, delegate { Games.SetMonitored(gm2.Id, false); gamerNote = "Stopped monitoring " + gm2.Name + ". Nothing more is sent to its servers."; gamerCanvas.Invalidate(); });
                var ed = new RectangleF(stop.X - (gm.Valve ? 260 : 110) * S, cy, 100 * S, 22 * S);
                g.DrawString(L.T("Edit servers"), Draw.fSmall, blue, ed, right);
                Act(gamerCanvas, ed, delegate { EditGame(gm2, false); });
                if (gm.Valve)
                {
                    var rf = new RectangleF(stop.X - 150 * S, cy, 140 * S, 22 * S);
                    g.DrawString(valveBusy ? "Refreshing..." : "Refresh Valve list", Draw.fSmall, blue, rf, right);
                    if (!valveBusy) Act(gamerCanvas, rf, delegate { RefreshValve(gm2); });
                }
                cy += 28 * S;
                g.DrawString(bt, Draw.fSmall, sub, new RectangleF(cx, cy, cw, 18 * S), nowrap); cy += 22 * S;
                if (rows.Count == 0) { g.DrawString(gm.Valve ? "No Valve servers yet - press Refresh Valve list (one request to Valve)." : "No servers yet - press Edit servers and add your server's address.", Draw.fBody, muted, cx, cy); }
                float c1 = cx + 230 * S, c2 = c1 + 90 * S, c3 = c2 + 80 * S, c4 = c3 + 70 * S, c5 = c4 + 110 * S;
                foreach (var s in rows)
                {
                    var st = GamePing.Me.Stat(gm.Id, s); var v = st.Window(60, 3600);
                    double avg = GameStat.Avg(v), p95 = GameStat.Pct(v, 0.95), jit = GameStat.Jitter(v), loss = GameStat.Loss(v), now = v.Length > 0 ? v[v.Length - 1] : double.NaN;
                    string grade = v.Length == 0 ? "Waiting" : GameStat.Grade(avg, jit, double.IsNaN(loss) ? 0 : loss);
                    bool isBest = best != null && best.Name == s.Name;
                    g.DrawString(s.Name + (isBest ? "  ★" : ""), Draw.fBodyB, ink, new RectangleF(cx, cy, 220 * S, 18 * S), nowrap);
                    string how = (s.Where ?? "") + " · " + (s.Kind == "valve" ? "Valve server" : s.Kind == "exact" ? "exact" : "region estimate") + " · " + (st.Used != "" ? st.Used : s.Method);
                    g.DrawString(how, Draw.fSmall, muted, new RectangleF(cx, cy + 17 * S, 220 * S, 16 * S), nowrap);
                    double ageS = st.LastAt == DateTime.MinValue ? 0 : (DateTime.Now - st.LastAt).TotalSeconds;   // a slow-paced server: say how old "now" is
                    using (var pb = new SolidBrush(GradeColor(grade))) g.DrawString(Ms(now) + " ms" + (v.Length > 0 && ageS >= 90 ? "  (" + (ageS < 3600 ? Math.Round(ageS / 60) + " min" : Math.Round(ageS / 3600) + " h") + " ago)" : ""), Draw.fBodyB, pb, c1, cy + 6 * S);
                    g.DrawString("avg " + Ms(avg) + " · 95% " + Ms(p95), Draw.fSmall, sub, new RectangleF(c2, cy + 8 * S, 160 * S, 16 * S), nowrap);
                    g.DrawString("jitter " + Ms(jit), Draw.fSmall, sub, c3 + 70 * S, cy + 8 * S);
                    g.DrawString("loss " + (double.IsNaN(loss) ? "-" : loss.ToString("0.#", CultureInfo.InvariantCulture) + "%"), Draw.fSmall, sub, c4 + 70 * S, cy + 8 * S);
                    // sparkline of the last minute
                    var sr = new RectangleF(c5 + 80 * S, cy + 4 * S, Math.Max(40 * S, x + w - pad - 110 * S - (c5 + 80 * S)), 24 * S);
                    var okv = v.Where(z => !double.IsNaN(z)).ToArray();
                    if (okv.Length > 1 && sr.Width > 40 * S)
                    {
                        double mx = Math.Max(okv.Max(), 20), mn = Math.Min(okv.Min(), 0);
                        var pts = new List<PointF>(); for (int i = 0; i < v.Length; i++) if (!double.IsNaN(v[i])) pts.Add(new PointF(sr.X + sr.Width * i / Math.Max(1, v.Length - 1), sr.Bottom - (float)((v[i] - mn) / (mx - mn)) * sr.Height));
                        if (pts.Count > 1) using (var pen = new Pen(GradeColor(grade), 1.5f * S)) g.DrawLines(pen, pts.ToArray());
                    }
                    var gr = new RectangleF(x + w - pad - 100 * S, cy + 5 * S, 100 * S, 20 * S);
                    Theme.Fill(g, gr, 6 * S, Color.FromArgb(Theme.Dark ? 60 : 36, GradeColor(grade)));
                    using (var gb = new SolidBrush(Theme.Dark ? Theme.Ink : GradeColor(grade))) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(grade, Draw.fSmall, gb, gr, c);
                    cy += rowH;
                }
                if (!string.IsNullOrEmpty(gm.Note)) g.DrawString(gm.Note, Draw.fSmall, muted, new RectangleF(cx, cy + 8 * S, cw, 18 * S), nowrap);
            }
            return top + h + 14 * S;
        }
        // the game's own conversation on the packet journey: its busiest UDP socket when Live capture is counting bytes,
        // otherwise its newest (the highest local port - the connection table has no traffic counts); else a TCP one
        void GameJourney(Game pg)
        {
            List<Conn> all; try { all = M.Connections(true); } catch (Exception e) { Program.Log("game journey: " + e.Message); gamerNote = "Couldn't read the connection table."; gamerCanvas.Invalidate(); return; }
            Func<Conn, bool> mine = c => pg.Exes.Any(x => string.Equals((c.Proc ?? "").Replace(".exe", ""), x, StringComparison.OrdinalIgnoreCase));
            var busy = new Dictionary<int, long>(); var lv = live.Running ? liveView : null;
            if (lv != null) foreach (var cv in lv.Top) if (cv.LocalPort > 0) { long b; busy.TryGetValue(cv.LocalPort, out b); busy[cv.LocalPort] = b + cv.Bytes; }
            Func<Conn, long> bytes = c => { long b; return busy.TryGetValue(c.LocalPort, out b) ? b : 0; };
            var c1 = all.Where(mine).OrderBy(c => c.Proto.StartsWith("UDP", StringComparison.Ordinal) ? 0 : c.State == "ESTABLISHED" && Watch.IsPublic(c.RemoteIp) ? 1 : 2).ThenByDescending(bytes).ThenByDescending(c => c.LocalPort).FirstOrDefault();
            if (c1 == null) { gamerNote = pg.Name + " has no network conversation open right now - join a match first."; gamerCanvas.Invalidate(); return; }
            OpenJourney(c1);
        }
    }
}
