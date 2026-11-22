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
        // the server menu: Auto (Cloudflare picks its nearest data centre), every city by provider, and "find the closest"
        // (one ping - or a timed connection - to each server, on this click only)
        void SpeedMenu(Point at)
        {
            var menu = new ContextMenuStrip { ShowImageMargin = false, Font = Draw.fBody };
            Action<ToolStripItemCollection, SpeedTest.Server, string> item = (into, sv, text) =>
            {
                var it = new ToolStripMenuItem(text) { Checked = Cfg.SpeedServer == sv.Id || (sv.Id == "cloudflare" && Cfg.SpeedServer == "auto") };
                string id = sv.Id; it.Click += delegate { Cfg.SpeedServer = id == "cloudflare" ? "auto" : id; Cfg.Save(); speedErr = ""; speedNote = ""; overviewCanvas.Invalidate(); };
                into.Add(it);
            };
            var l = SpeedTest.All();
            item(menu.Items, l[0], "Auto - Cloudflare, its nearest data centre (download and upload)");
            var find = new ToolStripMenuItem("Find the closest server for me"); find.Click += delegate { FindClosestSpeed(); }; menu.Items.Add(find);
            menu.Items.Add(new ToolStripSeparator());
            foreach (var g in l.Skip(1).GroupBy(z => z.Provider))
            {
                var sub = new ToolStripMenuItem(g.Key + (g.First().Up == "" ? "  (download only)" : ""));
                ((ToolStripDropDownMenu)sub.DropDown).ShowImageMargin = false;
                foreach (var sv in g) item(sub.DropDownItems, sv, sv.Where);
                menu.Items.Add(sub);
            }
            menu.Items.Add(new ToolStripSeparator());
            var more = new ToolStripMenuItem("Add my own server..."); more.Click += delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); if (tbBusy) { BusyNote(); return; } OpenTool("speedtest"); if (tbSel == "speedtest" && tbBoxes.Count > 0) tbBoxes[0].Box.Text = AdeeboMeter.L.T("list"); };   // never into a running tool's boxes menu.Items.Add(more);
            menu.Closed += delegate { try { if (!IsDisposed) BeginInvoke((Action)menu.Dispose); else menu.Dispose(); } catch (InvalidOperationException) { menu.Dispose(); } };
            menu.Show(overviewCanvas, at);
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
                        if (best != null) { Cfg.SpeedServer = best == "cloudflare" ? "auto" : best; Cfg.Save(); var bs = SpeedTest.Pick(best); speedNote = L.T("Closest:") + " " + (best == "cloudflare" ? "Cloudflare" : bs != null ? bs.Label : best) + " (" + Math.Round(bestMs).ToString(CultureInfo.CurrentCulture) + " ms) - " + L.T("picked. Press Test my speed."); } else speedErr = "no server answered - is the internet down?";
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
            else if (fast != ovFast) { ovFast = fast; if (!RedrawSpots(overviewCanvas, "ov.now", "ov.path")) overviewCanvas.Invalidate(); }
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
        // Fix a problem (home screen): { where it goes, what the button says }. "I can't name it" stays first - it's the catch-all.
        static readonly string[][] FixProblems = {
            new[] { "guide:whats", "Not sure what's wrong" },
            new[] { "guide:pc", "My PC is slow" },
            new[] { "guide:drops", "My Wi-Fi keeps dropping" },
            new[] { "guide:site", "A website or app won't open" },
            new[] { "gamer", "My game is lagging" },
            new[] { "guide:calls", "My calls keep freezing" },
            new[] { "guide:slow", "The internet is slow" },
            new[] { "tool:tidy", "I'm running out of storage" } };
        // the symptoms people notice: { where it goes, what they see, a few words more }
        static readonly string[][] Symptoms = {
            new[] { "guide:whats", "I have no internet", "nothing opens - works even when you can't google it" },
            new[] { "case:slow", "My internet is slow", "pages take ages" },
            new[] { "case:drops", "Internet stops and starts", "works, then doesn't, then works" },
            new[] { "case:buffer", "Videos keep buffering", "YouTube, Netflix stop and spin" },
            new[] { "link", "Can't find my Wi-Fi", "the network isn't in the list" },
            new[] { "case:site", "Only one website is broken", "everything else works" },
            new[] { "case:calls", "Calls are choppy", "Teams, Zoom, WhatsApp cut out" },
            new[] { "case:game", "My game lags", "ping spikes, rubber-banding" },
            new[] { "case:pc", "Computer feels slow", "everything takes a long time" },
            new[] { "tool:tidy", "Running out of space", "the disk is full, Windows nags" },
            new[] { "guide:pc", "Fan is loud or it's hot", "something is working hard" },
            new[] { "changes", "It was fine yesterday", "what changed since then?" } };
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
        RoundBox fixBox; string fixHint = "", fixGo;
        void EnsureFixBox()
        {
            if (fixBox != null) return;
            fixBox = new RoundBox(Sc(400), Draw.fInput, "Describe it - e.g. youtube keeps stopping, calls freeze, laptop is hot");
            fixBox.Box.AccessibleName = "Describe the problem"; fixBox.Box.MaxLength = 200;
            fixBox.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; FixFind(); } };
            overviewCanvas.Controls.Add(fixBox);
        }
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
        void FixFind()
        {
            string q = fixBox == null ? "" : fixBox.Box.Text.Trim();
            if (q.Length == 0) { fixHint = "Type what you notice, or press Find what's wrong."; fixGo = null; }
            else
            {
                var r = FixMatch(q);
                if (r == null) { fixHint = "I didn't recognise that - press here and I'll check everything, layer by layer."; fixGo = "guide:whats"; }
                else { fixHint = "Sounds like " + r[2] + ". I'll check " + r[3] + ".   Start  >"; fixGo = r[1]; }
            }
            overviewCanvas.Invalidate();
        }
        void FixGo(string go)
        {
            if (go.StartsWith("case:", StringComparison.Ordinal)) { OpenCase(go.Substring(5), "home", "", null, null); return; }
            if (go.StartsWith("guide:", StringComparison.Ordinal)) GoGuide(go.Substring(6));
            else if (go == "gamer" || go == "link" || go == "changes") { tab = go; ShowTab(); }
            else if (go.StartsWith("tool:", StringComparison.Ordinal)) { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(go.Substring(5)); }
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
                if (!Cfg.SetupDone) y = PaintSetup(g, x, y, w);
                else if (!Cfg.SidebarTried && Cfg.Layout == "sidebar")
                {
                    var cr = new RectangleF(x, y, w, 58 * S); Theme.Fill(g, cr, 12 * S, Kit.Soft("home"));
                    Kit.Icon(g, "layout", new RectangleF(cr.X + 16 * S, cr.Y + 19 * S, 20 * S, 20 * S), Kit.Accent("home"));
                    using (var hb = new SolidBrush(Kit.Ink("home"))) g.DrawString(L.T("New look: sections down the left, search on top, a live meter. Prefer the Classic tabs?"), Draw.fBodyB, hb, new RectangleF(cr.X + 48 * S, cr.Y + 19 * S, cr.Width - 300 * S, 22 * S));
                    string tt = L.T("Keep this"), nt = L.T("Classic tabs"); var ts = g.MeasureString(tt, Draw.fBtn); var ns = g.MeasureString(nt, Draw.fBtn);
                    var nr = new RectangleF(cr.Right - ns.Width - 30 * S, cr.Y + 13 * S, ns.Width + 16 * S, 32 * S); var tr = new RectangleF(nr.X - ts.Width - 30 * S, nr.Y, ts.Width + 20 * S, 32 * S);
                    Theme.Fill(g, tr, 10 * S, Kit.Accent("home")); using (var wb = new SolidBrush(Theme.OnBlue)) g.DrawString(tt, Draw.fBtn, wb, tr.X + 10 * S, tr.Y + (32 * S - ts.Height) / 2);
                    using (var mb = new SolidBrush(Theme.Sub)) g.DrawString(nt, Draw.fBtn, mb, nr.X + 8 * S, nr.Y + (32 * S - ns.Height) / 2);
                    Act(overviewCanvas, tr, delegate { Cfg.SidebarTried = true; Cfg.Save(); overviewCanvas.Invalidate(); });
                    Act(overviewCanvas, nr, delegate { SetLayout("classic"); });
                    y += cr.Height + 14 * S;
                }
                // the verdict
                float hh = Draw.Measure(s.Headline, Draw.fName, (int)(w - 40 * S)).Height;
                string wx = WeatherLine();   // the internet weather, one sentence - worked out off the paint, at most every 30 s
                float wh = wx != "" ? Draw.Measure(wx, Draw.fBody, (int)(w - 40 * S)).Height + 4 * S : 0;
                float ph = 76 * S;   // the path as big icons, a detail under each (FullView.Gateway.cs)
                var vr = new RectangleF(x, y, w, hh + 30 * S + wh + ph);
                Theme.Fill(g, vr, 14 * S, Theme.LvSoft(s.Level));
                PathStrip(g, x + 20 * S, y + 22 * S + hh + wh, w - 40 * S); LiveSpot(overviewCanvas, "ov.path", new RectangleF(x + 10 * S, y + 18 * S + hh + wh, w - 20 * S, ph));
                using (var hb = new SolidBrush(s.Level == 0 || s.Level == 3 ? Theme.Ink : Theme.LvText(s.Level))) g.DrawString(s.Headline, Draw.fName, hb, new RectangleF(x + 20 * S, y + 15 * S, w - 40 * S, hh + 6));
                if (wx != "") { g.DrawString(wx, Draw.fBody, sub, new RectangleF(x + 20 * S, y + 17 * S + hh, w - 40 * S, wh)); Hint(overviewCanvas, new RectangleF(x + 20 * S, y + 17 * S + hh, w - 40 * S, wh), "ov.WEATHER"); }
                y += vr.Height + 18 * S; OvLap("verdict");
                // then the do-everything button, then the front door (Adeeb, 22 Nov: verdict on top, Full checkup, then "What's going on?")
                y = PaintCheckupCard(g, x, y, w); OvLap("checkup");
                y = PaintFrontDoor(g, x, y, w); OvLap("front");   // the problem tiles (FullView.Case.cs)

                // first open: Simple or Pro (asked once; Settings › General › Mode changes it)
                if (Cfg.Mode == "")
                {
                    g.DrawString(L.T("HOW WOULD YOU LIKE ADEEBOMETER?"), Draw.fTiny, muted, x, y); y += 22 * S;
                    var opts = new[] {
                        new[] { "simple", "Simple", "Just tell me what's wrong. The dots, What's wrong?, a speed test, history and the Gamer Zone." },
                        new[] { "pro", "Pro", "All the engineer tools too: Monitor, Security, Toolbox, Network story and Learn." } };
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

                // the four numbers
                g.DrawString(L.T("RIGHT NOW"), Draw.fTiny, muted, x, y); y += 22 * S;
                double gw = M.GwPing.Avg(6), net = M.NetPing.Avg(6), down = M.Down.Last, up = M.Up.Last, dns = M.Dns.Avg(6);
                string kind, ssid, adapter, ip, gate, dnsSrv, speed, vpnName; bool vpn; int sig, mtu;
                lock (M.L) { kind = M.AdapterKind; ssid = M.Ssid; adapter = M.Adapter; ip = M.LocalIp; gate = M.Gateway; dnsSrv = M.DnsServer; speed = M.LinkSpeed; vpn = M.VpnUp; vpnName = M.VpnName; sig = M.WifiSignal; mtu = M.PrimaryMtu; }
                Func<double, string> ms = v => double.IsNaN(v) ? "no answer" : v.ToString("0", CultureInfo.CurrentCulture) + " ms";
                var tiles = new[] {
                    new[] { "Your router", ms(gw), "m", double.IsNaN(gw) ? "2" : gw > 50 ? "2" : gw > 15 ? "1" : "0", "path.LAN" },
                    new[] { "The internet", ms(net), "m", double.IsNaN(net) ? "2" : net > 150 ? "2" : net > 80 ? "1" : "0", "path.Internet" },
                    new[] { "Name lookups", double.IsNaN(dns) ? "not yet" : ms(dns), double.IsNaN(dns) ? "u" : "m", double.IsNaN(dns) ? "3" : dns > 300 ? "1" : "0", "path.DNS" },
                    new[] { "Your traffic", Draw.Pair(down, up), "w", "3", "ov.TRAFFIC" } };
                int cols = w > 760 * S ? 4 : 2; float tw = (w - (cols - 1) * 12 * S) / cols, th = 92 * S;
                for (int i = 0; i < tiles.Length; i++)
                {
                    var t = tiles[i]; var tr = new RectangleF(x + (i % cols) * (tw + 12 * S), y + (i / cols) * (th + 12 * S), tw, th);
                    Kit.Box(g, tr);
                    int lv = int.Parse(t[3], CultureInfo.InvariantCulture);
                    Draw.LvDot(g, lv, tr.X + 14 * S, tr.Y + 17 * S, 9 * S, 9 * S);
                    Glyphs.Tile(g, new[] { "cpe", "internet", "dns", "chart" }[i], new RectangleF(tr.Right - 54 * S, tr.Y + 12 * S, 42 * S, 42 * S), lv == 1 || lv == 2 ? Theme.LvText(lv) : Kit.Accent("home"), 10 * S);
                    g.DrawString(t[0].ToUpperInvariant(), Draw.fTiny, muted, tr.X + 30 * S, tr.Y + 14 * S);
                    using (var vb = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(t[1], Draw.fName, vb, tr.X + 14 * S, tr.Y + 34 * S);
                    Evidence.Pill(g, t[2], tr.X + 14 * S, tr.Bottom - 26 * S, DateTime.Now);
                    Hint(overviewCanvas, tr, t[4]);
                }
                LiveSpot(overviewCanvas, "ov.now", new RectangleF(x, y, w, ((tiles.Length + cols - 1) / cols) * (th + 12 * S) + 32 * S));   /* + the Wi-Fi line under them (its signal % moves too) */   // the four live numbers: redrawn alone (OverviewTick)
                y += ((tiles.Length + cols - 1) / cols) * (th + 12 * S) + 8 * S;
                string netLine = (kind == "Wi-Fi" ? "On Wi-Fi" + (ssid != "" ? " \"" + ssid + "\"" : " (name hidden - location is off)") + (sig >= 0 ? " · signal " + sig + "%" : "") : "On " + kind) + (speed != "" ? " · link " + speed : "") + (vpn ? " · VPN on" : "");
                g.DrawString(netLine, Draw.fBody, sub, x, y); y += 24 * S; OvLap("tiles");
                // speed: only when you press the button (it talks to Cloudflare's speed server; the line says so)
                {
                    var last = SpeedTest.Last; bool busy = SpeedTest.Busy;
                    string bt = L.T(busy ? "Stop" : "Test my speed"); var bsz2 = g.MeasureString(bt, Draw.fBodyB); var sb2 = new RectangleF(x, y + 2 * S, bsz2.Width + 28 * S, 32 * S);
                    Theme.Fill(g, sb2, 16 * S, busy ? Theme.Soft : Theme.BlueSoft); using (var tb = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.BlueDark)) g.DrawString(bt, Draw.fBodyB, tb, sb2.X + 14 * S, sb2.Y + (32 * S - bsz2.Height) / 2);
                    Act(overviewCanvas, sb2, delegate { if (SpeedTest.Busy) speedRun.Stop = true; else StartSpeed(); });
                    Hint(overviewCanvas, sb2, "tb.speedtest");
                    // which server - shown, and a click to change it
                    var cur = SpeedTest.Pick(Cfg.SpeedServer) ?? SpeedTest.Pick("auto");
                    string ct = speedFinding ? "Finding the closest..." : "Server: " + (cur.Id == "cloudflare" ? "Cloudflare, nearest" : cur.Label) + "  ·  change"; var csz = g.MeasureString(ct, Draw.fBody);
                    var cr = new RectangleF(sb2.Right + 8 * S, sb2.Y, csz.Width + 24 * S, 32 * S);
                    Kit.Box(g, cr, cr.Height / 2); using (var tb = new SolidBrush(Theme.Ink)) g.DrawString(ct, Draw.fBody, tb, cr.X + 12 * S, cr.Y + (32 * S - csz.Height) / 2);
                    Act(overviewCanvas, cr, delegate { if (!SpeedTest.Busy) SpeedMenu(new Point((int)cr.X, (int)(cr.Bottom + overviewCanvas.AutoScrollPosition.Y))); });
                    sb2 = new RectangleF(sb2.X, sb2.Y, cr.Right - sb2.X, sb2.Height);
                    string st = busy ? speedRun.Status : speedErr != "" ? "Speed test: " + speedErr : speedNote != "" ? speedNote : last != null ? last.T.ToString("HH:mm", CultureInfo.CurrentCulture) + " · " + (last.Where != "" && last.Where.StartsWith("data centre", StringComparison.Ordinal) ? last.Where.Substring(12) + " · " : "") + SpeedTest.Mbps(last.Down) + " down" + (last.NoUpload ? "" : " / " + SpeedTest.Mbps(last.Up) + " up") + " Mb/s · ping " + (double.IsNaN(last.Idle) ? "-" : Math.Round(last.Idle).ToString(CultureInfo.CurrentCulture) + " ms") + (last.Grade != "" ? " · when busy " + last.Grade : "")
                        : "About 20 seconds, up to " + (SpeedTest.MaxDownMB + SpeedTest.MaxUpMB) + " MB. Never runs by itself.";
                    using (var stb = new SolidBrush(speedErr != "" && !busy ? Theme.LvText(1) : Theme.Sub)) g.DrawString(st, Draw.fBody, stb, new RectangleF(sb2.Right + 12 * S, sb2.Y + 6 * S, w - sb2.Width - 12 * S, 24 * S));
                    y += 44 * S;
                }
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
                // needs attention
                var attAll = M.Events.Recent(300).Where(e => e.Lv >= 1 && e.Kind != "verdict" && e.T > DateTime.Now.AddHours(-2)).ToList(); var att = attAll.Take(4).ToList();
                g.DrawString(L.T("NEEDS ATTENTION  ·  LAST 2 HOURS"), Draw.fTiny, muted, x, y); y += 22 * S;
                int watching; y = PaintWatch(g, x, y, w, out watching); OvLap("watch");   // a board host down 3 checks in a row, a certificate inside 30 days
                if (att.Count == 0 && watching == 0) { g.DrawString(Voice.P("Nothing. Boring is good.", "Nothing in the last 2 hours."), Draw.fBody, muted, x, y); y += 28 * S; }
                using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    foreach (var e in att)
                    {
                        g.DrawString(e.T.ToString("HH:mm", CultureInfo.CurrentCulture), Draw.fMonoS, muted, x, y + 4 * S);
                        Draw.LvDot(g, e.Lv, x + 52 * S, y + 7 * S, 10 * S, 10 * S);
                        float tmw = Cfg.History ? 150 * S : 0;   // "See 14:05 ›" on every event: the Time machine at that minute
                        g.DrawString(e.Text, Draw.fBody, ink, new RectangleF(x + 72 * S, y + 2 * S, w - 72 * S - tmw, 20 * S), nowrap);
                        if (Cfg.History)
                        {
                            var ev = e; string tl = L.T("See") + " " + e.T.ToString("HH:mm", CultureInfo.CurrentCulture) + " ›"; var tsz = g.MeasureString(tl, Draw.fSmall);
                            var tlr = new RectangleF(x + w - tsz.Width, y + 4 * S, tsz.Width, 20 * S);
                            using (var bl = new SolidBrush(Theme.Blue)) g.DrawString(tl, Draw.fSmall, bl, tlr.X, tlr.Y);
                            Act(overviewCanvas, tlr, delegate { OpenTimeAt(ev.T, L.T("Home") + " - " + ev.Text); });
                        }
                        var er = new RectangleF(x, y, w - tmw, 26 * S); Act(overviewCanvas, er, delegate { tab = "changes"; ShowTab(); }); Hint(overviewCanvas, er, "kind." + e.Kind);
                        y += 26 * S; g.DrawLine(line, x, y, x + w, y);
                    }
                if (attAll.Count > att.Count)   // bug-hunt L-169: the rest are counted, one click away
                {
                    string more = L.T("and") + " " + (attAll.Count - att.Count) + " " + L.T("more in What changed");
                    using (var lb = new SolidBrush(Theme.Blue)) g.DrawString(more, Draw.fSmall, lb, x, y + 6 * S);
                    var mr2 = new RectangleF(x, y + 2 * S, g.MeasureString(more, Draw.fSmall).Width, 22 * S); Act(overviewCanvas, mr2, delegate { tab = "changes"; ShowTab(); }); y += 26 * S;
                }
                y += 14 * S;

                // your week, everything together (only once there's a record)
                y = PaintWeek(g, x, y, w); OvLap("week");

                // today
                string today = "Used today on this network: " + Usage.Mb(Usage.Today(Brain.NetKey));
                string lastCall = ""; lock (Calls.Summaries) if (Calls.Summaries.Count > 0) lastCall = "Last call (" + Calls.Summaries[0][0] + "): " + Calls.Summaries[0][1];
                g.DrawString(L.T("TODAY"), Draw.fTiny, muted, x, y); y += 22 * S;
                g.DrawString(today, Draw.fBody, sub, x, y); y += 24 * S;
                if (lastCall != "") { float lh = Draw.Measure(lastCall, Draw.fBody, (int)w).Height; g.DrawString(lastCall, Draw.fBody, sub, new RectangleF(x, y, w, lh + 4)); y += lh + 8 * S; }
                y += 14 * S;

                // where things live: big-icon tiles with a live line (FullView.Gateway.cs)
                y = PaintOutlookBody(g, overviewCanvas, x, y, w, false) + 10 * S; OvLap("outlook");   // the Outlook lives on Home now (22 Nov)
                y = PaintGateway(g, x, y, w); OvLap("gateway");
                g.DrawString(L.T("Ctrl+K jumps to anything.   Details (top right) shows raw values, sources and times."), Draw.fSmall, muted, x, y); y += 30 * S;
            }
            var need = new Size(0, (int)(y + 20 * S));
            SetNeed(overviewCanvas, need);
        }
    }
}
