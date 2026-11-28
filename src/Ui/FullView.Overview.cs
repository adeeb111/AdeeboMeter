// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Overview: the start page. The verdict, the four numbers that matter, what needs attention, today in one line,
    // a way into Investigate, and a map of where everything else lives. Painted from what's already measured.
    sealed partial class FullView
    {
        Panel overviewPage; DB overviewCanvas; string ovKey = "";
        Toolkit.Run speedRun = new Toolkit.Run(); string speedErr = "", speedNote = "";
        // the speed test runs only when this button is pressed - one at a time, on its own thread
        void StartSpeed()
        {
            if (SpeedTest.Busy) return;
            speedRun = new Toolkit.Run { Status = "Starting..." }; speedErr = ""; speedNote = ""; var run = speedRun;
            var srv = SpeedTest.Pick(Cfg.SpeedServer) ?? SpeedTest.Pick("auto");
            new System.Threading.Thread(() => { var res = SpeedTest.Measure(run, srv); speedErr = res.Error; run.Status = ""; }) { IsBackground = true, Name = "speed test" }.Start();
            overviewCanvas.Invalidate();
        }
        void FindClosestSpeed()
        {
            if (SpeedTest.Busy || speedFinding) return;
            if (Policy.NoSpeed) { speedErr = L.T("Speed tests are turned off by your IT team."); overviewCanvas.Invalidate(); return; }   // the policy covers this too: it pings every server
            speedFinding = true; speedErr = ""; speedNote = ""; overviewCanvas.Invalidate();
            new System.Threading.Thread(() =>
            {
                string best = null; double bestMs = double.MaxValue;
                foreach (var sv in SpeedTest.All())
                {
                    try
                    {
                        var ip = System.Net.Dns.GetHostAddresses(sv.Host).FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork); if (ip == null || !SpeedTest.Reachable(ip)) continue;   // never into this PC or the LAN
                        var r = Icmp.Echo(ip, 32, 64, false, 800, false); double ms = r.Status == Icmp.Ok && r.Rtt >= 0 ? r.Rtt : GamePing.TcpMs(ip, 443, 800);
                        if (!double.IsNaN(ms) && ms < bestMs) { bestMs = ms; best = sv.Id; }
                    }
                    catch (System.Net.Sockets.SocketException) { } catch (ArgumentException) { }
                }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        speedFinding = false;
                        if (best != null) { Cfg.SpeedServer = best == "cloudflare" ? "auto" : best; Cfg.Save(); var bs = SpeedTest.Pick(best); speedNote = L.T("Closest:") + " " + (best == "cloudflare" ? "Cloudflare" : bs != null ? bs.Label : best) + " (" + Math.Round(bestMs).ToString(CultureInfo.CurrentCulture) + " ms) - " + L.T("picked. Press Test my speed."); } else speedErr = L.T("no server answered - is the internet down?");
                        overviewCanvas.Invalidate();
                    });
                }
                catch (InvalidOperationException) { }
            }) { IsBackground = true, Name = "closest speed server" }.Start();
        }
        volatile bool speedFinding;
        // repaint only when something on the page would look different (not every second)
        string ovFast = "";
        void OverviewTick()
        {
            var s = snap; Func<double, string> r = v => double.IsNaN(v) ? "-" : Math.Round(v).ToString(CultureInfo.InvariantCulture);
            int sig; lock (M.L) sig = M.WifiSignal;
            // what moves every second (the four numbers, the path) redraws only its own spots; anything else redraws the page
            string lsp; lock (M.L) lsp = M.LinkSpeed; string fast = r(M.GwPing.Avg(6)) + "|" + r(M.NetPing.Avg(6)) + "|" + r(M.Dns.Avg(6)) + "|" + Draw.Pair(M.Down.Last, M.Up.Last) + "|" + sig + "|" + lsp + "|" + r(s.Cpu / 10);
            string k = s.Headline + "|" + s.Level + "|" + weather + "|" + (DateTime.Now.Minute / 10) + "|" + M.Events.Recent(1).Select(e => e.T.Ticks).FirstOrDefault() + "|" + Cfg.Details + "|" + speedRun.Status + "|" + speedFinding + "|" + Cfg.SpeedServer + "|" + (SpeedTest.Last != null ? SpeedTest.Last.T.Ticks : 0);
            if (k != ovKey) { ovKey = k; ovFast = fast; overviewCanvas.Invalidate(); }
            else if (fast != ovFast) { ovFast = fast; if (!RedrawSpots(overviewCanvas, "ov.now")) overviewCanvas.Invalidate(); }
        }

        Panel BuildOverviewPage()
        {
            overviewPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            overviewCanvas = new DB { Name = "overviewCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            overviewCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintOverview(e.Graphics); }; Wire(overviewCanvas);
            overviewCanvas.MouseMove += delegate(object s, MouseEventArgs e) { SymMouse(e.Location); }; overviewCanvas.MouseLeave += delegate { SymMouse(new Point(-9999, -9999)); };
            overviewPage.Controls.Add(overviewCanvas);
            return overviewPage;
        }

        void GoGuide(string id) { tab = "guide"; ShowTab(); if (id != null) StartGuide(id, false); }
        // the symptoms people notice: { where it goes, what they see, a few words more }
        static readonly string[][] Symptoms = {
            new[] { "guide:whats", L.T("I have no internet"), "nothing opens - works even when you can't google it" },
            new[] { "case:slow", L.T("My internet is slow"), "pages take ages" },
            new[] { "case:drops", L.T("Internet stops and starts"), "works, then doesn't, then works" },
            new[] { "case:buffer", L.T("Videos keep buffering"), "YouTube, Netflix stop and spin" },
            new[] { "link", L.T("Can't find my Wi-Fi"), "the network isn't in the list" },
            new[] { "case:site", L.T("Only one website is broken"), "everything else works" },
            new[] { "case:calls", L.T("Calls are choppy"), L.T("Teams, Zoom, WhatsApp cut out") },
            new[] { "case:game", L.T("My game lags"), "ping spikes, rubber-banding" },
            new[] { "case:pc", L.T("Computer feels slow"), "everything takes a long time" },
            new[] { "tool:tidy", L.T("Running out of space"), "the disk is full, Windows nags" },
            new[] { "guide:pc", L.T("Fan is loud or it's hot"), "something is working hard" },
            new[] { "changes", L.T("It was fine yesterday"), "what changed since then?" } };
        // words people type -> { where to go, "sounds like ...", what I'll check }. Offline: common words, not sentences.
        // Whole words only ("work" never matches "working"); a word of 6+ letters also matches what starts with it
        // ("disconnect" -> "disconnecting"). What's wrong (a symptom) beats where it happens ("at work", "work laptop"): the
        // symptom rules come first, the place rule (VPN / work) and the catch-all "nothing works" after them.
        static readonly string[][] FixWords = {
            new[] { "vpn slow|slow vpn|vpn is slow|vpn lag|vpn lags|tunnel", "tool:vpn", "the VPN may be the slow part", "how much of the delay is the VPN tunnel, and whether everything goes through it" },
            new[] { "vpn|globalprotect|anyconnect|zscaler|netskope|forticlient", "guide:vpn", "it works at home but not on VPN or at work", "overlapping networks, proxy, the VPN and the port" },
            new[] { "lag|lagging|laggy|game|games|gaming|fortnite|valorant|steam|xbox|playstation", "gamer", "your game is lagging", "the game's servers, your Wi-Fi and your provider - in the Gamer Zone" },
            new[] { "buffer|buffering|youtube|netflix|stream|streaming|video|twitch|disney|prime", "guide:slow", "videos keep buffering", "your speed, your Wi-Fi and whether something else is using the internet" },
            new[] { "call|calls|zoom|teams|meet|whatsapp|skype|robot|robotic|choppy|voice|echo", "guide:calls", "calls are choppy", "jitter, loss, your upload and the VPN" },
            new[] { "disconnect|drop|drops|dropping|keeps going|cuts out|on and off|stops and starts|unstable", "guide:drops", "the connection keeps dropping", "today's drops, roams and what the signal was doing" },
            new[] { "slow pc|computer slow|laptop slow|pc is slow|computer is slow|laptop is slow|freeze|freezes|frozen|hang|hangs|hot|fan|noisy|crash|crashes|not responding", "guide:pc", "the computer itself is slow", "processor, memory, disk, heat and frozen apps" },
            new[] { "storage|disk full|space|full|gigabytes", "tool:tidy", "you're running out of storage", "temp files, the Recycle Bin and what takes the space" },
            new[] { "website|websites|site|sites|page|pages|link|url|won't open|wont open|can't open|cant open|can't get to|cant get to|email|outlook|mail|error", "guide:site", "a website or app won't open", "the name, the route, the door, the lock and the answer" },
            new[] { "slow|slowly|loading|takes ages|speed|high ping|ping is high|latency", "guide:slow", "the internet is slow", "where the delay starts: your PC, Wi-Fi, router, provider or DNS" },
            // bug-hunt ML-85: the words people type about the link itself
            new[] { "dns|nameserver|name server|resolve|resolving", "guide:site", "names aren't turning into addresses (DNS)", "the DNS servers, how fast they answer and whether a site's name resolves" },
            new[] { "work laptop|office network|company network|at work|at the office", "guide:vpn", "it works at home but not on VPN or at work", "overlapping networks, proxy, the VPN and the port" },
            new[] { "no internet|offline|nothing works|nothing opens|nothing loads|not connected|can't connect|cannot connect|won't connect|doesn't work|does not work|not working|isn't working|stopped working|hotspot|printer", "guide:whats", "there's no internet", "every layer, from this PC to the internet" },
            new[] { "yesterday|suddenly|since|changed|update", "changes", "something changed", "what changed on this PC and network since then" },
            // last: only when nothing more specific was said ("Wi-Fi works but nothing loads" is a no-internet problem, not the link)
            new[] { "wifi|wi fi|wireless|signal|router|modem|ethernet|cable|lan|network card|adapter", "link", "it's the link to your router", "your Wi-Fi or cable: signal, speed, channel and the neighbours" } };
        // the first rule whose word appears wins (most specific rules first); nothing found -> suggest checking everything
        internal static string[] FixMatch(string text)
        {
            // words separated by single spaces, punctuation gone (the apostrophe stays: "won't")
            var sb = new System.Text.StringBuilder(" ");
            foreach (char c in (text ?? "").ToLowerInvariant().Replace("\u2019", "'")) sb.Append(char.IsLetterOrDigit(c) || c == '\'' ? c : ' ');
            string t = System.Text.RegularExpressions.Regex.Replace(sb.Append(' ').ToString(), " {2,}", " ");
            foreach (var r in FixWords)
                foreach (var wd in r[0].Split('|'))
                    if (t.Contains(" " + wd + " ") || (wd.Length >= 6 && t.Contains(" " + wd))) return r;
            return null;
        }
        // ---------- the four checks people ask for most, one tap each (28 Nov)
        static readonly string[][] Quick = {
            new[] { "run:mypc", "pc", "Check my PC", "Speed, disk, battery, stability - one report" },
            new[] { "checkup", "wifi", "Check my connection", "Wi-Fi, router, provider, names - where it breaks" },   // 28 Nov: the full checkup - the card that did it is gone from Home
            new[] { "dest", "internet", "Check a website", "One site or app, end to end" },
            new[] { "tool:ispreport", "mail", "Report for my ISP", "Proof your provider can't brush off" } };
        float PaintQuickChecks(Graphics g, float x, float y, float w)
        {
            int cols = w > 900 * S ? 4 : 2; float gap = 10 * S, cw = (w - (cols - 1) * gap) / cols, chh = 78 * S;   /* 28 Nov: room for a second line - "one re..." at 125 %, every card cut in French */
            for (int i = 0; i < Quick.Length; i++)
            {
                var q = Quick[i]; var r = new RectangleF(x + (i % cols) * (cw + gap), y + (i / cols) * (chh + gap), cw, chh);
                Theme.Fill(g, r, 12 * S, i == 0 ? Theme.BlueSoft : Theme.Card); Theme.Stroke(g, r, 12 * S, i == 0 ? Theme.Blue : Theme.Line, i == 0 ? 1.5f : 1);
                Glyphs.Tile(g, q[1], new RectangleF(r.X + 12 * S, r.Y + (chh - 40 * S) / 2, 40 * S, 40 * S), Theme.Blue, 10 * S);
                bool rtl = L.IsRtl; float tx = r.X + 62 * S, tw = r.Width - 72 * S;
                using (var ink = new SolidBrush(Theme.Ink)) using (var mut = new SolidBrush(Theme.Muted)) using (var nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap | (rtl ? StringFormatFlags.DirectionRightToLeft : 0) })
                {
                    g.DrawString(L.T(q[2]), Draw.fBodyB, ink, new RectangleF(tx, r.Y + 12 * S, tw, 22 * S), nf);
                    using (var df = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit | (rtl ? StringFormatFlags.DirectionRightToLeft : 0) })
                        g.DrawString(L.T(q[3]), Draw.fSmall, mut, new RectangleF(tx, r.Y + 34 * S, tw, chh - 38 * S), df);   /* two lines at most */
                }
                string go = q[0]; Act(overviewCanvas, r, delegate { FixGo(go); });
            }
            return y + ((Quick.Length + cols - 1) / cols) * (chh + gap) + 8 * S;
        }
        void FixGo(string go)
        {
            if (go.StartsWith("case:", StringComparison.Ordinal)) { OpenCase(go.Substring(5), "home", "", null, null); return; }
            if (go == "checkup") { StartCheckup(); return; }
            if (go.StartsWith("guide:", StringComparison.Ordinal)) GoGuide(go.Substring(6));
            else if (go == "gamer" || go == "link" || go == "changes") { tab = go; ShowTab(); }
            else if (go.StartsWith("tool:", StringComparison.Ordinal) || go.StartsWith("run:", StringComparison.Ordinal))
            {
                // 28 Nov: "tool:mtr|1.1.1.1" opens it with the boxes filled; "run:" also starts it (a button that says Check runs the check)
                var p = go.Substring(go.IndexOf(':') + 1).Split('|'); tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(p[0]);
                for (int i = 1; i < p.Length && i - 1 < tbBoxes.Count; i++) if (tbBoxes[i - 1] != null) tbBoxes[i - 1].Box.Text = p[i];
                if (go.StartsWith("run:", StringComparison.Ordinal) && !tbBusy) StartTool();
            }
            else if (go != "") { tab = go; ShowTab(); SyncNav(); }   // any page by its id (doctor, health, dest...)
        }

        // the weather sentence reads the outage log (files, when History is on): never on the paint, never more than twice a minute
        string weather = ""; DateTime weatherAt = DateTime.MinValue; volatile bool weatherBusy;
        string WeatherLine()
        {
            if (!weatherBusy && (DateTime.UtcNow - weatherAt).TotalSeconds > 30)
            {
                weatherBusy = true; weatherAt = DateTime.UtcNow;
                Task.Factory.StartNew(() => { string w = ""; try { w = Picks.Weather(M); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("weather: " + e.Message); } try { BeginInvoke((Action)delegate { weatherBusy = false; if (w != weather) { weather = w; overviewCanvas.Invalidate(); } }); } catch (InvalidOperationException) { weatherBusy = false; } });
            }
            return weather;
        }
        // the first Home paint after a start, timed part by part and written to the log once (a real PC: 250-690 ms cold, 20-40 warm)
        System.Diagnostics.Stopwatch ovLap; System.Text.StringBuilder ovLaps; bool ovTimed;
        void OvLap(string part) { if (ovLaps == null) return; ovLaps.Append(part).Append(' ').Append(ovLap.ElapsedMilliseconds).Append(" · "); ovLap.Restart(); }
        // Home's live part: the four tiles and the connection line (drawn by the page, and alone by its live spot)
        void PaintOvNow(Graphics g, float ix, float ty, float iw, int cols, float tw, float th, float ny)
        {
            double gw = M.GwPing.Avg(6), net = M.NetPing.Avg(6), down = M.Down.Last, up = M.Up.Last, dns = M.Dns.Avg(6);
            string kind, ssid, speed; bool vpn; int sig;
            lock (M.L) { kind = M.AdapterKind; ssid = M.Ssid; speed = M.LinkSpeed; vpn = M.VpnUp; sig = M.WifiSignal; }
            Func<double, string> ms = v => double.IsNaN(v) ? L.T("no answer") : v.ToString("0", CultureInfo.CurrentCulture) + " ms";
            using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub))
            {
                // 23 Nov polish: one row of readings, This PC first - the same order as the chain (PC, router, internet, names) -
                // no boxes inside the card, no badges; where each number comes from is in the line under them
                var s0 = snap; int lvPc = s0.Path.Length > 0 && s0.Path[0] >= 0 && s0.Path[0] <= 2 ? s0.Path[0] : 3;
                bool memHot = !double.IsNaN(s0.Mem) && s0.Mem >= 85 && (double.IsNaN(s0.Cpu) || s0.Mem - 85 >= s0.Cpu - 85);
                string pcMain = memHot ? Draw.Fmt(s0.Mem, "0", "% ") + L.T("memory") : Draw.Fmt(s0.Cpu, "0", "% ") + Kit.Sent("CPU");
                string pcSub = memHot ? Kit.Sent("CPU") + " " + Draw.Fmt(s0.Cpu, "0", "%") + " · " + L.T("disk") + " " + Draw.Fmt(s0.Disk, "0", "%")
                                      : L.T("memory") + " " + Draw.Fmt(s0.Mem, "0", "%") + " · " + L.T("disk") + " " + Draw.Fmt(s0.Disk, "0", "%");
                if (lvPc >= 1 && lvPc <= 2 && pcHidden) pcSub = L.T("hidden while it stays like this");
                // 28 Nov (a user review: "hard to understand for non-networking audiences"): each reading says what it means for you,
                // in plain words, under the number; the engineer's numbers stay with Settings › Extra numbers for engineers
                Func<int, string, string, string, string> plain = (lv, ok, warn, bad) => lv == 0 ? L.T(ok) : lv == 1 ? L.T(warn) : lv == 2 ? L.T(bad) : "";
                int lvGw = M.GwPing.Values(6).Length == 0 ? 3 : LvMs(M.GwPing, gw, 15, 50), lvNet = M.NetPing.Values(6).Length == 0 ? 3 : LvMs(M.NetPing, net, 80, 150), lvDns = double.IsNaN(dns) ? 3 : dns > 300 ? 1 : 0;
                if (!Cfg.Details && !(lvPc >= 1 && lvPc <= 2 && pcHidden)) pcSub = plain(lvPc, "plenty of room to spare", "busy - things may feel slow", "very busy - close something heavy");
                var tiles = new[] {
                    new[] { "This PC", pcMain, pcSub, lvPc.ToString(CultureInfo.InvariantCulture), "path.PC" },
                    // hunters10 M-1 (R2-076): nothing measured yet (or the check is off) is grey "not yet" - red "no answer" only after pings came back empty
                    new[] { "Your router", M.GwPing.Values(6).Length == 0 ? L.T("not yet") : ms(gw), plain(lvGw, "your home network is fine", "a little slow at home", "home network trouble"), LvMs(M.GwPing, gw, 15, 50).ToString(CultureInfo.InvariantCulture), "path.LAN" },
                    new[] { "The internet", M.NetPing.Values(6).Length == 0 ? L.T("not yet") : ms(net), plain(lvNet, "the internet answers quickly", "slower than usual", "trouble reaching it"), LvMs(M.NetPing, net, 80, 150).ToString(CultureInfo.InvariantCulture), "path.Internet" },
                    new[] { "Name lookups", double.IsNaN(dns) ? L.T("not yet") : ms(dns), plain(lvDns, "sites open without waiting", "new sites are slow to start", "sites may not open"), double.IsNaN(dns) ? "3" : dns > 300 ? "1" : "0", "path.DNS" },
                    new[] { "Your traffic", Draw.Pair(down, up), L.T("what this PC uses right now"), "3", "ov.TRAFFIC" } };
                var rects = PaintReadings(g, overviewCanvas, tiles, ix, ty, iw, cols, th);
                if (rects.Length > 0) { var pr = rects[0]; Act(overviewCanvas, pr, delegate { if (pcHidden) UnhideProblem(); else { tab = "procs"; ShowTab(); } }); }
                string netLine = (kind == "Wi-Fi" ? L.T("On Wi-Fi") + (ssid != "" ? " \"" + ssid + "\"" : " " + L.T("(name hidden - location is off)")) + (sig >= 0 ? " · " + L.T("signal") + " " + sig + "%" : "") : L.T("On") + " " + kind) + (speed != "" ? " · " + L.T("link") + " " + speed : "") + (vpn ? " · " + L.T("VPN on") : "");
                netLine += ". " + L.T("Router, internet and names measured by AdeeboMeter; traffic as Windows reports it.");
                bool nrtl = L.RunsRtl(netLine);   /* 28 Nov: Arabic read left to right had "1.2" and "Gb/s" split apart - right to left, still starting at the card's edge */
                using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap | (nrtl ? StringFormatFlags.DirectionRightToLeft : 0), Alignment = nrtl ? StringAlignment.Far : StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter }) g.DrawString(netLine, Draw.fSmall, muted, new RectangleF(ix, ny + 4 * S, iw, 22 * S), nf);
            }
        }
        // a row of readings (Home's card, the Time machine's minute): a dot and a label, the number big with its unit small beside
        // it, an optional line under it; thin dividers between. tiles: { label, value, sub, level, hint key }. Returns each reading's box.
        RectangleF[] PaintReadings(Graphics g, Control on, string[][] tiles, float ix, float ty, float iw, int cols, float th)
        {
            int n = tiles.Length, per = cols >= 4 ? n : 3; float cw = iw / per; var rects = new RectangleF[n];
            using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var div = new Pen(Theme.Line))
            using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                for (int i = 0; i < n; i++)
                {
                    var t = tiles[i]; int rI = i / per, cI = i % per;
                    var tr = new RectangleF(ix + cI * cw, ty + rI * (th + 8 * S), cw, th); rects[i] = tr;
                    if (cI > 0) g.DrawLine(div, tr.X, tr.Y + 6 * S, tr.X, tr.Bottom - 6 * S);
                    float px = tr.X + (cI > 0 ? 16 * S : 0);
                    int lv = int.Parse(t[3], CultureInfo.InvariantCulture);
                    Draw.LvDot(g, lv, px, tr.Y + 10 * S, 8 * S, 8 * S);
                    g.DrawString(L.T(t[0]), Draw.fSmall, sub, new RectangleF(px + 14 * S, tr.Y + 5 * S, cw - 34 * S, 20 * S), nf);
                    // the number big, its unit small beside it (24 Nov mockup: "23 ms", "12 % CPU")
                    string num = t[1], unit = ""; int last = -1; for (int c = num.Length - 1; c >= 0; c--) if (char.IsDigit(num[c])) { last = c; break; }
                    if (last >= 0 && last < num.Length - 1) { unit = num.Substring(last + 1).Trim(); num = num.Substring(0, last + 1); }
                    using (var vb = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink))
                    {
                        var nfont = last >= 0 ? Draw.fCardNum : Draw.fBig; float nw = Math.Min(cw - 20 * S, g.MeasureString(num, nfont).Width);
                        g.DrawString(num, nfont, vb, new RectangleF(px, tr.Y + 22 * S, cw - 20 * S, 36 * S), nf);
                        if (unit != "") g.DrawString(unit, Draw.fSmall, sub, new RectangleF(px + nw - 2 * S, tr.Y + 22 * S + nfont.GetHeight(g) - Draw.fSmall.GetHeight(g) - 3 * S, Math.Max(10, cw - 20 * S - nw), 20 * S), nf);
                    }
                    if (t[2] != "") { var sfnt = g.MeasureString(t[2], Draw.fSmall).Width > cw - 20 * S ? Draw.fTiny : Draw.fSmall; g.DrawString(t[2], sfnt, muted, new RectangleF(px, tr.Y + 58 * S, cw - 20 * S, 20 * S), nf); }   /* 28 Nov: French "votre réseau domestique va bi..." - a size smaller before it cuts */
                    if (t.Length > 4 && t[4] != "") Hint(on, tr, t[4]);
                }
            return rects;
        }
        void PaintOverview(Graphics g)

        {
            if (!ovTimed && overviewCanvas.Width > 300) { ovTimed = true; ovLap = System.Diagnostics.Stopwatch.StartNew(); ovLaps = new System.Text.StringBuilder(); try { PaintOverview1(g); } finally { OvLap("rest"); double up = -1; try { using (var me = System.Diagnostics.Process.GetCurrentProcess()) up = (DateTime.Now - me.StartTime).TotalMilliseconds; } catch (Exception) { } Program.Log("first Home paint (ms): " + ovLaps.ToString().TrimEnd(' ', '·') + "  ·  " + (up / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " s after start"); ovLaps = null; } return; }
            PaintOverview1(g);
        }
        void PaintOverview1(Graphics g)
        {
            symRect = null;   // set again where the tiles are drawn; a page without them gets no hover
            g.TranslateTransform(overviewCanvas.AutoScrollPosition.X, overviewCanvas.AutoScrollPosition.Y);
            float x = 18 * S, y = 16 * S, w = overviewCanvas.ClientSize.Width - 36 * S;
            Begin(overviewCanvas);
            var s = snap;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
            {
                // once, after updating to 1.1.5: the sidebar is the new default; "Prefer the Classic tabs?" - kept or switched,
                // never shown again (PLAN 11.7d)
                // first open: the setup card alone, nothing else competing with it (23 Nov)
                if (!Cfg.SetupDone) { y = PaintSetup(g, x, y, w); SetNeed(overviewCanvas, new Size(0, (int)(y + 20 * S))); return; }
                if (Cfg.WelcomeStep >= 0 && Cfg.WelcomeStep < 4) y = PaintWelcome(g, x, y, w);   // 28 Nov: four short cards, once, after setup
                if (!Cfg.SidebarTried && Cfg.Layout == "sidebar")
                {
                    // hunters10 (display ML-1): the buttons are measured first and the question wraps into the room left, the card growing to fit -
                    // at the smallest window the second half of the sentence was cut off
                    string tt = L.T("Keep this"), nt = L.T("Classic tabs"), q = L.T("New look: sections down the left, search on top, a live meter. Prefer the Classic tabs?");
                    var ts = g.MeasureString(tt, Draw.fBtn); var ns = g.MeasureString(nt, Draw.fBtn);
                    float bw = ns.Width + 16 * S + ts.Width + 20 * S + 14 * S + 30 * S, qw = Math.Max(120 * S, w - 48 * S - bw - 12 * S);
                    float qh = g.MeasureString(q, Draw.fBodyB, (int)qw).Height, chh = Math.Max(58 * S, qh + 26 * S);
                    var cr = new RectangleF(x, y, w, chh); Theme.Fill(g, cr, 12 * S, Kit.Soft("home"));
                    Kit.Icon(g, "layout", new RectangleF(cr.X + 16 * S, cr.Y + (chh - 20 * S) / 2, 20 * S, 20 * S), Kit.Accent("home"));
                    using (var hb = new SolidBrush(Kit.Ink("home"))) g.DrawString(q, Draw.fBodyB, hb, new RectangleF(cr.X + 48 * S, cr.Y + (chh - qh) / 2, qw, qh + 2));
                    var nr = new RectangleF(cr.Right - ns.Width - 30 * S, cr.Y + (chh - 32 * S) / 2, ns.Width + 16 * S, 32 * S); var tr = new RectangleF(nr.X - ts.Width - 30 * S, nr.Y, ts.Width + 20 * S, 32 * S);
                    Theme.Fill(g, tr, 10 * S, Kit.Accent("home")); using (var wb = new SolidBrush(Theme.OnBlue)) g.DrawString(tt, Draw.fBtn, wb, tr.X + 10 * S, tr.Y + (32 * S - ts.Height) / 2);
                    using (var mb = new SolidBrush(Theme.Sub)) g.DrawString(nt, Draw.fBtn, mb, nr.X + 8 * S, nr.Y + (32 * S - ns.Height) / 2);
                    Act(overviewCanvas, tr, delegate { Cfg.SidebarTried = true; Cfg.Save(); overviewCanvas.Invalidate(); });
                    Act(overviewCanvas, nr, delegate { SetLayout("classic"); });
                    y += cr.Height + 14 * S;
                }
                // the verdict, and right under it the four numbers (Adeeb, 22 Nov: the path strip and a separate "Right now" row said the
                // same thing twice - one card now: the headline, the weather sentence, the four live tiles, the connection line)
                double gw = M.GwPing.Avg(6), net = M.NetPing.Avg(6), down = M.Down.Last, up = M.Up.Last, dns = M.Dns.Avg(6);
                string kind, ssid, adapter, ip, gate, dnsSrv, speed, vpnName; bool vpn; int sig, mtu;
                lock (M.L) { kind = M.AdapterKind; ssid = M.Ssid; adapter = M.Adapter; ip = M.LocalIp; gate = M.Gateway; dnsSrv = M.DnsServer; speed = M.LinkSpeed; vpn = M.VpnUp; vpnName = M.VpnName; sig = M.WifiSignal; mtu = M.PrimaryMtu; }
                Func<double, string> ms = v => double.IsNaN(v) ? L.T("no answer") : v.ToString("0", CultureInfo.CurrentCulture) + " ms";
                {
                    // 24 Nov (the approved mockup): the gauge on the left, the answer in big type beside it
                    float gaugeW = w > 760 * S ? 150 * S : 0, ix = x + 20 * S + gaugeW, iw = w - 40 * S - gaugeW;
                    // 23 Nov polish: the same card in every state - only the headline and the buttons change. A problem hidden
                    // with Hide says what it hid and carries a Show again button; the reading still shows it in colour.
                    bool hid = ProblemHidden(s); bool problem = (s.Level == 1 || s.Level == 2) && !hid;
                    double hv; string hk = hid ? ProblemKind(s, out hv) : null, kn = hid ? KindName(hk) : "";
                    string head = !hid ? s.Headline : string.Format(L.T("{0} hidden. Everything else looks fine."), kn.Substring(0, 1).ToUpper(CultureInfo.CurrentCulture) + kn.Substring(1));
                    float aw = problem || hid ? PaintVerdictActs(g, x + w - 20 * S, y + 14 * S, true) : 0;
                    float hw0 = Math.Max(120 * S, iw - aw - (aw > 0 ? 16 * S : 0));
                    float hh = Draw.Measure(head, Draw.fBig, (int)hw0).Height;
                    string wx = WeatherLine();   // the internet weather, one sentence - worked out off the paint, at most every 30 s
                    float wh = wx != "" ? Draw.Measure(wx, Draw.fBody, (int)iw).Height + 4 * S : 0;
                    int cols = iw > 700 * S ? 5 : 3; float tw = iw / cols, th = 80 * S;
                    float tilesH = (cols >= 5 ? 1 : 2) * (th + 8 * S) - 8 * S;
                    var vr = new RectangleF(x, y, w, 15 * S + hh + 6 * S + wh + 10 * S + tilesH + 12 * S + 22 * S + 12 * S);
                    Kit.Box(g, vr, 14 * S);
                    if (gaugeW > 0) Theme.Gauge(g, new RectangleF(x + 28 * S, y + 22 * S, gaugeW - 40 * S, 64 * S), problem ? s.Level : 0);
                    if (problem || hid) PaintVerdictActs(g, x + w - 20 * S, y + 14 * S, false);
                    using (var hb = new SolidBrush(Theme.Ink)) g.DrawString(head, Draw.fBig, hb, new RectangleF(ix, y + 15 * S, hw0, hh + 6));   // ink: the gauge and the reading carry the colour
                    if (wx != "") { g.DrawString(wx, Draw.fBody, sub, new RectangleF(ix, y + 21 * S + hh, hw0, wh)); Hint(overviewCanvas, new RectangleF(ix, y + 21 * S + hh, hw0, wh), "ov.WEATHER"); }
                    using (var dl = new Pen(Theme.Line2)) g.DrawLine(dl, ix, y + 15 * S + hh + 6 * S + wh + 4 * S, ix + iw, y + 15 * S + hh + 6 * S + wh + 4 * S);
                    float ty = y + 15 * S + hh + 6 * S + wh + 10 * S;
                    float ny = ty + tilesH + 12 * S;
                    PaintOvNow(g, ix, ty, iw, cols, tw, th, ny);
                    // the four live numbers and the Wi-Fi line: redrawn alone (OverviewTick), straight from their own painter - the rest of
                    // Home is not walked again for them (motion review, 23 Nov: a narrow redraw still ran the whole page's drawing, ~100-300 ms on a slow PC)
                    Color nowBg = Theme.Card; float nX = ix, nY = ty, nW = iw, nTw = tw, nTh = th, nNy = ny; int nCols = cols;
                    LiveSpot(overviewCanvas, "ov.now", new RectangleF(ix, ty, iw, ny + 22 * S - ty), nowBg, gg => PaintOvNow(gg, nX, nY, nW, nCols, nTw, nTh, nNy));
                    y += vr.Height + 18 * S; OvLap("verdict");
                }
                // then the do-everything button, then the front door (Adeeb, 22 Nov: verdict on top, Full checkup, then "What's going on?")
                y = PaintQuickChecks(g, x, y, w); OvLap("quick");   // 28 Nov (Adeeb): ready-made buttons - check my PC, my connection, a website, proof for my ISP
                // 28 Nov (Adeeb: fewer doors): the Full checkup card went - "Check my connection" above runs it
                y = PaintFrontDoor(g, x, y, w); OvLap("front");   // the problem tiles (FullView.Case.cs)

                // first open: Simple or Pro (asked once; Settings › General › Mode changes it)
                if (Cfg.Mode == "")
                {
                    g.DrawString(Kit.Sent("HOW WOULD YOU LIKE ADEEBOMETER?"), Draw.fTiny, muted, x, y); y += 22 * S;
                    var opts = new[] {
                        new[] { "simple", "Simple", L.T("Just tell me what's wrong. The dots, What's wrong?, a speed test, history and the Gamer Zone.") },
                        new[] { "pro", "Pro", L.T("All the engineer tools too: Monitor, Security, Toolbox, Network story and Learn.") } };
                    float ow = (w - 12 * S) / 2, oh = 84 * S;
                    for (int i = 0; i < 2; i++)
                    {
                        var orr = new RectangleF(x + i * (ow + 12 * S), y, ow, oh);
                        Theme.Fill(g, orr, 12 * S, i == 0 ? Theme.BlueSoft : Theme.Soft); Theme.Stroke(g, orr, 12 * S, i == 0 ? Theme.Blue : Theme.Line, i == 0 ? 2 : 1);
                        g.DrawString(L.T(opts[i][1]), Draw.fHead, ink, orr.X + 18 * S, orr.Y + 12 * S);
                        g.DrawString(L.T(opts[i][2]), Draw.fBody, muted, new RectangleF(orr.X + 18 * S, orr.Y + 40 * S, orr.Width - 36 * S, 40 * S));
                        string m = opts[i][0]; Act(overviewCanvas, orr, delegate { SetModeTo(m); });
                    }
                    y += oh + 8 * S;
                    g.DrawString(L.T("You can switch any time in Settings › General."), Draw.fSmall, muted, x, y); y += 30 * S;
                }

                y = PaintSpeedCard(g, x, y, w); OvLap("speed");   // the speed test: a gauge, the last result and the server (FullView.Speed.cs)
                string normal = Baseline.Normal(Brain.NetKey);
                if (normal != "") { g.DrawString(normal, Draw.fSmall, muted, new RectangleF(x, y, w, 36 * S)); y += Draw.Measure(normal, Draw.fSmall, (int)w).Height + 8 * S; }
                if (Cfg.Details)
                {
                    string raw = "Adapter " + adapter + "  ·  IP " + ip + "  ·  gateway " + gate + "  ·  DNS " + dnsSrv + (mtu > 0 ? "  ·  MTU " + mtu : "") + (vpn ? "  ·  VPN " + vpnName : "") + "  ·  jitter " + ms(M.NetPing.Jitter(10)) + "  ·  router worst " + ms(M.GwPing.Max(12));
                    float rh = Draw.Measure(raw, Draw.fMonoS, (int)w).Height;
                    g.DrawString(raw, Draw.fMonoS, muted, new RectangleF(x, y, w, rh + 4)); y += rh + 10 * S;
                }
                y += 10 * S;


                // a Windows update that lines up with a lasting slowdown (only when strong and still there)
                string ub = UpdateBreak.Strong; OvLap("fix");
                if (ub != "")
                {
                    var ur = new RectangleF(x, y, w, 34 * S); Theme.Fill(g, ur, 10 * S, Theme.LvSoft(1));
                    using (var tb = new SolidBrush(Theme.LvText(1))) using (var nw = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(ub + "  ›", Draw.fBodyB, tb, new RectangleF(x + 14 * S, y + 8 * S, w - 28 * S, 20 * S), nw);
                    Act(overviewCanvas, ur, delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool("winupdate"); }); y += 44 * S;
                }
                // 28 Nov (Adeeb): "Needs attention" is gone from Home - the verdict on top already says what's wrong now, and every
                // event is in History › What changed. A watched host that's down still shows here (it's yours to act on).
                int watching; y = PaintWatch(g, x, y, w, out watching); OvLap("watch");
                if (watching > 0) y += 14 * S;

                // your week, everything together (only once there's a record)
                y = PaintWeek(g, x, y, w); OvLap("week");

                // today
                string today = "Used today on this network: " + Usage.Mb(Usage.Today(Brain.NetKey));
                string lastCall = ""; lock (Calls.Summaries) if (Calls.Summaries.Count > 0) lastCall = "Last call (" + Calls.Summaries[0][0] + "): " + Calls.Summaries[0][1];
                g.DrawString(Kit.Sent("TODAY"), Draw.fTiny, muted, x, y); y += 22 * S;
                g.DrawString(today, Draw.fBody, sub, x, y); y += 24 * S;
                if (lastCall != "") { float lh = Draw.Measure(lastCall, Draw.fBody, (int)w).Height; g.DrawString(lastCall, Draw.fBody, sub, new RectangleF(x, y, w, lh + 4)); y += lh + 8 * S; }
                y += 14 * S;

                // where things live: big-icon tiles with a live line (FullView.Gateway.cs)
                y = PaintOutlookBody(g, overviewCanvas, x, y, w, false) + 10 * S; OvLap("outlook");   // the Outlook lives on Home now (22 Nov)
                y = PaintGateway(g, x, y, w); OvLap("gateway");
                g.DrawString(L.T("Ctrl+K jumps to anything.   Extra numbers for engineers: Settings > General."), Draw.fSmall, muted, x, y); y += 30 * S;
            }
            var need = new Size(0, (int)(y + 20 * S));
            SetNeed(overviewCanvas, need);
        }
    }
}
