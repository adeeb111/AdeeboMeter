// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Overview: the start page. The verdict, the four numbers that matter, what needs attention, today in one line,
    // a way into Investigate, and a map of where everything else lives. Painted from what's already measured.
    sealed partial class FullView
    {
        Panel overviewPage; DB overviewCanvas; string ovKey = "";
        Toolkit.Run speedRun = new Toolkit.Run(); string speedErr = "";
        // the speed test runs only when this button is pressed - one at a time, on its own thread
        void StartSpeed()
        {
            if (SpeedTest.Busy) return;
            speedRun = new Toolkit.Run { Status = "Starting..." }; speedErr = ""; var run = speedRun;
            new System.Threading.Thread(() => { var res = SpeedTest.Measure(run); speedErr = res.Error; run.Status = ""; }) { IsBackground = true, Name = "speed test" }.Start();
            overviewCanvas.Invalidate();
        }
        // repaint only when something on the page would look different (not every second)
        void OverviewTick()
        {
            var s = snap; Func<double, string> r = v => double.IsNaN(v) ? "-" : Math.Round(v).ToString(CultureInfo.InvariantCulture);
            string k = s.Headline + "|" + s.Level + "|" + r(M.GwPing.Avg(6)) + "|" + r(M.NetPing.Avg(6)) + "|" + r(M.Dns.Avg(6)) + "|" + r(M.Down.Last * 10) + "|" + r(M.Up.Last * 10) + "|" + M.Events.Recent(1).Select(e => e.T.Ticks).FirstOrDefault() + "|" + Cfg.Details + "|" + speedRun.Status + "|" + (SpeedTest.Last != null ? SpeedTest.Last.T.Ticks : 0);
            if (k != ovKey) { ovKey = k; overviewCanvas.Invalidate(); }
        }

        Panel BuildOverviewPage()
        {
            overviewPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            overviewCanvas = new DB { Name = "overviewCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            overviewCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintOverview(e.Graphics); }; Wire(overviewCanvas);
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
            new[] { "guide:slow", "My internet is slow", "pages take ages, videos buffer" },
            new[] { "guide:whats", "Wi-Fi says \"No internet\"", "connected, but nothing works" },
            new[] { "guide:drops", "Internet stops and starts", "works, then doesn't, then works" },
            new[] { "guide:slow", "Videos keep buffering", "YouTube, Netflix stop and spin" },
            new[] { "link", "Can't find my Wi-Fi", "the network isn't in the list" },
            new[] { "guide:calls", "Calls are choppy", "Teams, Zoom, WhatsApp cut out" },
            new[] { "guide:pc", "Computer feels slow", "everything takes a long time" },
            new[] { "guide:pc", "An app freezes", "it stops responding" },
            new[] { "guide:pc", "Fan is loud or it's hot", "something is working hard" },
            new[] { "changes", "It was fine yesterday", "what changed since then?" },
            new[] { "guide:site", "Only one website is broken", "everything else works" } };
        // words people type -> { where to go, "sounds like ...", what I'll check }. Offline: common words, not sentences.
        static readonly string[][] FixWords = {
            new[] { "buffer|youtube|netflix|stream|video|twitch|disney|prime", "guide:slow", "videos keep buffering", "your speed, your Wi-Fi and whether something else is using the internet" },
            new[] { "lag|ping|game|gaming|fortnite|valorant|steam|xbox|playstation", "gamer", "your game is lagging", "the game's servers, your Wi-Fi and your provider - in the Gamer Zone" },
            new[] { "call|zoom|teams|meet|whatsapp|skype|robot|choppy|voice|echo", "guide:calls", "calls are choppy", "jitter, loss, your upload and the VPN" },
            new[] { "disconnect|drop|drops|dropping|keeps going|cuts out|on and off|stops and starts|unstable", "guide:drops", "the connection keeps dropping", "today's drops, roams and what the signal was doing" },
            new[] { "no internet|offline|nothing works|nothing opens|not connected|can't connect|cannot connect", "guide:whats", "there's no internet", "every layer, from this PC to the internet" },
            new[] { "website|site|page|link|url|app won|won't open|wont open|can't open|cant open|error", "guide:site", "a website or app won't open", "the name, the route, the door, the lock and the answer" },
            new[] { "vpn|work|office|company", "guide:vpn", "it works at home but not on VPN or at work", "overlapping networks, proxy, the VPN and the port" },
            new[] { "slow pc|computer slow|laptop slow|freeze|freezes|frozen|hang|hot|fan|noisy|crash|not responding", "guide:pc", "the computer itself is slow", "processor, memory, disk, heat and frozen apps" },
            new[] { "storage|disk full|space|full|gigabytes", "tool:tidy", "you're running out of storage", "temp files, the Recycle Bin and what takes the space" },
            new[] { "slow|slowly|loading|takes ages|speed", "guide:slow", "the internet is slow", "where the delay starts: your PC, Wi-Fi, router, provider or DNS" },
            new[] { "yesterday|suddenly|since|changed|update", "changes", "something changed", "what changed on this PC and network since then" } };
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
            string t = " " + (text ?? "").ToLowerInvariant().Replace("’", "'") + " ";
            foreach (var r in FixWords)
                foreach (var wd in r[0].Split('|'))
                    if (t.Contains(wd.Contains(" ") ? wd : " " + wd) || (wd.Length >= 6 && t.Contains(wd))) return r;
            return null;
        }
        void FixFind()
        {
            string q = fixBox == null ? "" : fixBox.Box.Text.Trim();
            if (q.Length == 0) { fixHint = "Type what you notice, or press Check everything for me."; fixGo = null; }
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
            if (go.StartsWith("guide:", StringComparison.Ordinal)) GoGuide(go.Substring(6));
            else if (go == "gamer" || go == "link" || go == "changes") { tab = go; ShowTab(); }
            else if (go.StartsWith("tool:", StringComparison.Ordinal)) { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(go.Substring(5)); }
        }

        void PaintOverview(Graphics g)
        {
            g.TranslateTransform(overviewCanvas.AutoScrollPosition.X, overviewCanvas.AutoScrollPosition.Y);
            float x = 18 * S, y = 16 * S, w = overviewCanvas.ClientSize.Width - 36 * S;
            Begin(overviewCanvas);
            var s = snap;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
            {
                // the verdict
                float hh = Draw.Measure(s.Headline, Draw.fName, (int)(w - 40 * S)).Height;
                var vr = new RectangleF(x, y, w, hh + 30 * S);
                Theme.Fill(g, vr, 14 * S, Theme.LvSoft(s.Level));
                using (var hb = new SolidBrush(s.Level == 0 || s.Level == 3 ? Theme.Ink : Theme.LvText(s.Level))) g.DrawString(s.Headline, Draw.fName, hb, new RectangleF(x + 20 * S, y + 15 * S, w - 40 * S, hh + 6));
                y += vr.Height + 18 * S;

                // first open: Simple or Pro (asked once; Settings › General › Mode changes it)
                if (Cfg.Mode == "")
                {
                    g.DrawString("HOW WOULD YOU LIKE ADEEBOMETER?", Draw.fTiny, muted, x, y); y += 22 * S;
                    var opts = new[] {
                        new[] { "simple", "Simple", "Just tell me what's wrong. The dots, What's wrong?, a speed test, history and the Gamer Zone." },
                        new[] { "pro", "Pro", "All the engineer tools too: Monitor, Security, Toolbox, Network story and Learn." } };
                    float ow = (w - 12 * S) / 2, oh = 84 * S;
                    for (int i = 0; i < 2; i++)
                    {
                        var orr = new RectangleF(x + i * (ow + 12 * S), y, ow, oh);
                        Theme.Fill(g, orr, 12 * S, i == 0 ? Theme.BlueSoft : Theme.Soft); Theme.Stroke(g, orr, 12 * S, i == 0 ? Theme.Blue : Theme.Line, i == 0 ? 2 : 1);
                        g.DrawString(opts[i][1], Draw.fHead, ink, orr.X + 18 * S, orr.Y + 12 * S);
                        g.DrawString(opts[i][2], Draw.fBody, muted, new RectangleF(orr.X + 18 * S, orr.Y + 40 * S, orr.Width - 36 * S, 40 * S));
                        string m = opts[i][0]; Act(overviewCanvas, orr, delegate { SetModeTo(m); });
                    }
                    y += oh + 8 * S;
                    g.DrawString("You can switch any time in Settings › General.", Draw.fSmall, muted, x, y); y += 30 * S;
                }

                // the four numbers
                g.DrawString("RIGHT NOW", Draw.fTiny, muted, x, y); y += 22 * S;
                double gw = M.GwPing.Avg(6), net = M.NetPing.Avg(6), down = M.Down.Last, up = M.Up.Last, dns = M.Dns.Avg(6);
                string kind, ssid, adapter, ip, gate, dnsSrv, speed, vpnName; bool vpn; int sig, mtu;
                lock (M.L) { kind = M.AdapterKind; ssid = M.Ssid; adapter = M.Adapter; ip = M.LocalIp; gate = M.Gateway; dnsSrv = M.DnsServer; speed = M.LinkSpeed; vpn = M.VpnUp; vpnName = M.VpnName; sig = M.WifiSignal; mtu = M.PrimaryMtu; }
                Func<double, string> ms = v => double.IsNaN(v) ? "no answer" : v.ToString("0", CultureInfo.CurrentCulture) + " ms";
                var tiles = new[] {
                    new[] { "Your router", ms(gw), "m", double.IsNaN(gw) ? "2" : gw > 50 ? "2" : gw > 15 ? "1" : "0", "path.LAN" },
                    new[] { "The internet", ms(net), "m", double.IsNaN(net) ? "2" : net > 150 ? "2" : net > 80 ? "1" : "0", "path.Internet" },
                    new[] { "Name lookups", double.IsNaN(dns) ? "not yet" : ms(dns), double.IsNaN(dns) ? "u" : "m", double.IsNaN(dns) ? "3" : dns > 300 ? "1" : "0", "path.DNS" },
                    new[] { "Your traffic", (double.IsNaN(down) ? "-" : down.ToString("0.0", CultureInfo.CurrentCulture)) + " / " + (double.IsNaN(up) ? "-" : up.ToString("0.0", CultureInfo.CurrentCulture)) + " Mb/s", "w", "3", "ov.TRAFFIC" } };
                int cols = w > 760 * S ? 4 : 2; float tw = (w - (cols - 1) * 12 * S) / cols, th = 92 * S;
                for (int i = 0; i < tiles.Length; i++)
                {
                    var t = tiles[i]; var tr = new RectangleF(x + (i % cols) * (tw + 12 * S), y + (i / cols) * (th + 12 * S), tw, th);
                    Theme.Stroke(g, tr, 12 * S, Theme.Track, 1);
                    int lv = int.Parse(t[3], CultureInfo.InvariantCulture);
                    using (var b = new SolidBrush(Theme.Lv(lv))) g.FillEllipse(b, tr.X + 14 * S, tr.Y + 17 * S, 9 * S, 9 * S);
                    g.DrawString(t[0].ToUpperInvariant(), Draw.fTiny, muted, tr.X + 30 * S, tr.Y + 14 * S);
                    using (var vb = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(t[1], Draw.fName, vb, tr.X + 14 * S, tr.Y + 34 * S);
                    Evidence.Pill(g, t[2], tr.X + 14 * S, tr.Bottom - 26 * S, DateTime.Now);
                    Hint(overviewCanvas, tr, t[4]);
                }
                y += ((tiles.Length + cols - 1) / cols) * (th + 12 * S) + 8 * S;
                string netLine = (kind == "Wi-Fi" ? "On Wi-Fi" + (ssid != "" ? " \"" + ssid + "\"" : " (name hidden - location is off)") + (sig >= 0 ? " · signal " + sig + "%" : "") : "On " + kind) + (speed != "" ? " · link " + speed : "") + (vpn ? " · VPN on" : "");
                g.DrawString(netLine, Draw.fBody, sub, x, y); y += 24 * S;
                // speed: only when you press the button (it talks to Cloudflare's speed server; the line says so)
                {
                    var last = SpeedTest.Last; bool busy = SpeedTest.Busy;
                    string bt = busy ? "Stop" : "Test my speed"; var bsz2 = g.MeasureString(bt, Draw.fBodyB); var sb2 = new RectangleF(x, y + 2 * S, bsz2.Width + 28 * S, 32 * S);
                    Theme.Fill(g, sb2, 16 * S, busy ? Theme.Soft : Theme.BlueSoft); using (var tb = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.BlueDark)) g.DrawString(bt, Draw.fBodyB, tb, sb2.X + 14 * S, sb2.Y + (32 * S - bsz2.Height) / 2);
                    Act(overviewCanvas, sb2, delegate { if (SpeedTest.Busy) speedRun.Stop = true; else StartSpeed(); });
                    Hint(overviewCanvas, sb2, "tb.speedtest");
                    string st = busy ? speedRun.Status : speedErr != "" ? "Speed test: " + speedErr : last != null ? "Last test " + last.T.ToString("HH:mm", CultureInfo.CurrentCulture) + " · " + SpeedTest.Mbps(last.Down) + " down / " + SpeedTest.Mbps(last.Up) + " up Mb/s · ping " + (double.IsNaN(last.Idle) ? "-" : Math.Round(last.Idle).ToString(CultureInfo.CurrentCulture) + " ms") + (last.Grade != "" ? " · when busy " + last.Grade : "")
                        : "About 20 seconds with Cloudflare's speed server (up to " + (SpeedTest.MaxDownMB + SpeedTest.MaxUpMB) + " MB). Never runs by itself.";
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

                // Fix a problem. First "Not sure what's wrong?" - big, in everyday words: one button that checks everything, a box to
                // describe it in your own words, and the symptoms people actually notice. Then the named problems, smaller.
                {
                    float cx0 = x + 20 * S, cw = w - 40 * S, cy = y + 20 * S;
                    var card = new RectangleF(x, y, w, 0);
                    int sc = Math.Max(1, Math.Min(3, (int)(cw / (240 * S)))); int sr = (Symptoms.Length + sc - 1) / sc; float sbh = 58 * S, sbw = (cw - (sc - 1) * 10 * S) / sc;
                    card.Height = 20 * S + 62 * S + 56 * S + (fixHint != "" ? 40 * S : 0) + 26 * S + sr * (sbh + 10 * S) + 10 * S;
                    Theme.Fill(g, card, 14 * S, Theme.Soft); Theme.Stroke(g, card, 14 * S, Theme.Line, 1);
                    // the big button, right
                    string big = "Check everything for me"; var bsz = g.MeasureString(big, Draw.fName); var bbr = new RectangleF(x + w - 20 * S - bsz.Width - 40 * S, cy + 4 * S, bsz.Width + 40 * S, 50 * S);
                    Theme.Fill(g, bbr, 12 * S, Theme.Blue); using (var wb = new SolidBrush(Color.White)) g.DrawString(big, Draw.fName, wb, bbr.X + 20 * S, bbr.Y + (bbr.Height - bsz.Height) / 2);
                    Act(overviewCanvas, bbr, delegate { GoGuide("whats"); });
                    float tw0 = bbr.X - cx0 - 16 * S;
                    g.DrawString("Not sure what's wrong?", Draw.fTitle, ink, new RectangleF(cx0, cy, tw0, 30 * S));
                    g.DrawString("That's fine. Tell me what you notice, or tap the closest one. All of this works offline - even when you can't google it.", Draw.fBody, muted, new RectangleF(cx0, cy + 32 * S, tw0, 40 * S));
                    cy += 62 * S;
                    // the describe-it box is a real text box, kept in place under the heading
                    EnsureFixBox();
                    var want = new Point((int)cx0, (int)(cy + overviewCanvas.AutoScrollPosition.Y)); int wantW = (int)(cw - 120 * S);
                    if (fixBox.Location != want) fixBox.Location = want; if (fixBox.Width != wantW) fixBox.Width = wantW;
                    var fb = new RectangleF(cx0 + wantW + 10 * S, cy, 110 * S, 40 * S);
                    Theme.Fill(g, fb, 10 * S, Theme.BlueSoft); using (var tb = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.BlueDark)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString("Find it", Draw.fBodyB, tb, fb, c);
                    Act(overviewCanvas, fb, delegate { FixFind(); });
                    cy += 56 * S;
                    if (fixHint != "")
                    {
                        var hr = new RectangleF(cx0, cy - 6 * S, cw, 34 * S); Theme.Fill(g, hr, 8 * S, Theme.BlueSoft);
                        using (var tb = new SolidBrush(Theme.Ink)) using (var nf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter }) g.DrawString(fixHint, Draw.fBody, tb, new RectangleF(hr.X + 12 * S, hr.Y, hr.Width - 24 * S, hr.Height), nf);
                        if (fixGo != null) { string go = fixGo; Act(overviewCanvas, hr, delegate { FixGo(go); }); }
                        cy += 40 * S;
                    }
                    g.DrawString("OR TAP WHAT YOU SEE", Draw.fTiny, muted, cx0, cy); cy += 22 * S;
                    for (int i = 0; i < Symptoms.Length; i++)
                    {
                        var br = new RectangleF(cx0 + (i % sc) * (sbw + 10 * S), cy + (i / sc) * (sbh + 10 * S), sbw, sbh);
                        Theme.Fill(g, br, 10 * S, Theme.Card); Theme.Stroke(g, br, 10 * S, Theme.Track, 1);
                        g.DrawString(Symptoms[i][1], Draw.fBodyB, ink, new RectangleF(br.X + 14 * S, br.Y + 9 * S, br.Width - 20 * S, 20 * S));
                        g.DrawString(Symptoms[i][2], Draw.fSmall, muted, new RectangleF(br.X + 14 * S, br.Y + 31 * S, br.Width - 20 * S, 18 * S));
                        string go = Symptoms[i][0]; Act(overviewCanvas, br, delegate { FixGo(go); });
                    }
                    y = card.Bottom + 14 * S;

                    // I know what it is
                    g.DrawString("I KNOW WHAT IT IS", Draw.fTiny, muted, x, y); y += 22 * S;
                    float kx = x; var probs = FixProblems;
                    for (int i = 1; i < probs.Length; i++)
                    {
                        var ks = g.MeasureString(probs[i][1], Draw.fBody); var kr = new RectangleF(kx, y, ks.Width + 26 * S, 34 * S);
                        if (kr.Right > x + w && kx > x) { kx = x; y += 42 * S; kr = new RectangleF(kx, y, ks.Width + 26 * S, 34 * S); }
                        Theme.Stroke(g, kr, 17 * S, Theme.Track, 1); g.DrawString(probs[i][1], Draw.fBody, ink, kr.X + 13 * S, kr.Y + (34 * S - ks.Height) / 2);
                        string go = probs[i][0]; Act(overviewCanvas, kr, delegate { FixGo(go); });
                        if (go.StartsWith("guide:", StringComparison.Ordinal)) Hint(overviewCanvas, kr, "guide." + go.Substring(6));
                        kx += kr.Width + 8 * S;
                    }
                    using (var bl = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
                    {
                        string all = "See all problems  >"; var asz = g.MeasureString(all, Draw.fBodyB);
                        if (kx + asz.Width > x + w) { kx = x; y += 42 * S; }
                        var ar = new RectangleF(kx + 6 * S, y + (34 * S - asz.Height) / 2, asz.Width, asz.Height); g.DrawString(all, Draw.fBodyB, bl, ar.X, ar.Y);
                        Act(overviewCanvas, ar, delegate { GoGuide(null); });
                    }
                    y += 52 * S;
                }

                // needs attention
                var att = M.Events.Recent(300).Where(e => e.Lv >= 1 && e.Kind != "verdict" && e.T > DateTime.Now.AddHours(-2)).Take(4).ToList();
                g.DrawString("NEEDS ATTENTION  ·  LAST 2 HOURS", Draw.fTiny, muted, x, y); y += 22 * S;
                if (att.Count == 0) { g.DrawString(Voice.P("Nothing. Boring is good.", "Nothing in the last 2 hours."), Draw.fBody, muted, x, y); y += 28 * S; }
                using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    foreach (var e in att)
                    {
                        g.DrawString(e.T.ToString("HH:mm", CultureInfo.CurrentCulture), Draw.fMonoS, muted, x, y + 4 * S);
                        using (var b = new SolidBrush(Theme.Lv(e.Lv))) g.FillEllipse(b, x + 52 * S, y + 7 * S, 10 * S, 10 * S);
                        g.DrawString(e.Text, Draw.fBody, ink, new RectangleF(x + 72 * S, y + 2 * S, w - 72 * S, 20 * S), nowrap);
                        var er = new RectangleF(x, y, w, 26 * S); Act(overviewCanvas, er, delegate { tab = "changes"; ShowTab(); }); Hint(overviewCanvas, er, "kind." + e.Kind);
                        y += 26 * S; g.DrawLine(line, x, y, x + w, y);
                    }
                y += 14 * S;

                // today
                string today = "Used today on this network: " + Usage.Mb(Usage.Today(Brain.NetKey));
                string lastCall = ""; lock (Calls.Summaries) if (Calls.Summaries.Count > 0) lastCall = "Last call (" + Calls.Summaries[0][0] + "): " + Calls.Summaries[0][1];
                g.DrawString("TODAY", Draw.fTiny, muted, x, y); y += 22 * S;
                g.DrawString(today, Draw.fBody, sub, x, y); y += 24 * S;
                if (lastCall != "") { float lh = Draw.Measure(lastCall, Draw.fBody, (int)w).Height; g.DrawString(lastCall, Draw.fBody, sub, new RectangleF(x, y, w, lh + 4)); y += lh + 8 * S; }
                y += 14 * S;

                // where things live
                g.DrawString("WHERE EVERYTHING LIVES", Draw.fTiny, muted, x, y); y += 24 * S;
                var map = new[] {
                    new[] { "monitor", "Monitor", "Connections, your Wi-Fi or cable, what's running, live capture." },
                    new[] { "investigate", "Investigate", "Guided problems, check a destination, health and tools." },
                    new[] { "story", "Network story", "Your traffic layer by layer, routing, Teams & Office, real packets." },
                    new[] { "time", "Time machine", "Replay any minute, what changed, the week, usage, calls." },
                    new[] { "learn", "Learn", "Protocols, Teams and Office, inside Windows, every term." } };
                int mc = w > 900 * S ? 5 : w > 560 * S ? 3 : 2; float mw = (w - (mc - 1) * 10 * S) / mc, mh = 92 * S;
                for (int i = 0; i < map.Length; i++)
                {
                    var m = map[i]; var mr = new RectangleF(x + (i % mc) * (mw + 10 * S), y + (i / mc) * (mh + 10 * S), mw, mh);
                    Theme.Fill(g, mr, 12 * S, Theme.Soft);
                    g.DrawString(m[1], Draw.fBodyB, ink, mr.X + 14 * S, mr.Y + 12 * S);
                    g.DrawString(m[2], Draw.fSmall, sub, new RectangleF(mr.X + 14 * S, mr.Y + 34 * S, mr.Width - 24 * S, mr.Height - 38 * S));
                    string id = m[0]; Act(overviewCanvas, mr, delegate { string pg; tab = lastIn.TryGetValue(id, out pg) ? pg : Secs.First(z => z[0] == id)[2]; ShowTab(); });
                }
                y += ((map.Length + mc - 1) / mc) * (mh + 10 * S) + 10 * S;
                g.DrawString("Ctrl+K jumps to anything.   Details (top right) shows raw values, sources and times.", Draw.fSmall, muted, x, y); y += 30 * S;
            }
            var need = new Size(0, (int)(y + 20 * S));
            if (overviewCanvas.AutoScrollMinSize != need) { overviewCanvas.AutoScrollMinSize = need; overviewCanvas.Invalidate(); }
        }
    }
}
