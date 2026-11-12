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
        // repaint only when something on the page would look different (not every second)
        void OverviewTick()
        {
            var s = snap; Func<double, string> r = v => double.IsNaN(v) ? "-" : Math.Round(v).ToString(CultureInfo.InvariantCulture);
            string k = s.Headline + "|" + s.Level + "|" + r(M.GwPing.Avg(6)) + "|" + r(M.NetPing.Avg(6)) + "|" + r(M.Dns.Avg(6)) + "|" + r(M.Down.Last * 10) + "|" + r(M.Up.Last * 10) + "|" + M.Events.Recent(1).Select(e => e.T.Ticks).FirstOrDefault() + "|" + Cfg.Details;
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
        void FixGo(string go)
        {
            if (go.StartsWith("guide:", StringComparison.Ordinal)) GoGuide(go.Substring(6));
            else if (go == "gamer") { tab = "gamer"; ShowTab(); }
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
                string normal = Baseline.Normal(Brain.NetKey);
                if (normal != "") { g.DrawString(normal, Draw.fSmall, muted, new RectangleF(x, y, w, 36 * S)); y += Draw.Measure(normal, Draw.fSmall, (int)w).Height + 8 * S; }
                if (Cfg.Details)
                {
                    string raw = "Adapter " + adapter + "  ·  IP " + ip + "  ·  gateway " + gate + "  ·  DNS " + dnsSrv + (mtu > 0 ? "  ·  MTU " + mtu : "") + (vpn ? "  ·  VPN " + vpnName : "") + "  ·  jitter " + ms(M.NetPing.Jitter(10)) + "  ·  router worst " + ms(M.GwPing.Max(12));
                    float rh = Draw.Measure(raw, Draw.fMonoS, (int)w).Height;
                    g.DrawString(raw, Draw.fMonoS, muted, new RectangleF(x, y, w, rh + 4)); y += rh + 10 * S;
                }
                y += 10 * S;

                // Fix a problem: the things people recognise, each opening the right guided check
                {
                    var card = new RectangleF(x, y, w, 0);
                    float cy = y + 16 * S, cw = w - 32 * S, cx0 = x + 16 * S;
                    var probs = FixProblems; int fcols = Math.Max(1, Math.Min(4, (int)(cw / (220 * S)))); int frows = (probs.Length + fcols - 1) / fcols;
                    float bh = 40 * S, bw = (cw - (fcols - 1) * 10 * S) / fcols;
                    card.Height = 16 * S + 28 * S + 26 * S + frows * (bh + 10 * S) + 34 * S;
                    Theme.Fill(g, card, 12 * S, Theme.Soft); Theme.Stroke(g, card, 12 * S, Theme.Line, 1);
                    g.DrawString("Fix a problem", Draw.fTitle, ink, cx0, cy); cy += 28 * S;
                    g.DrawString("Pick what's happening. AdeeboMeter shows what it finds and what to check next.", Draw.fBody, muted, cx0, cy); cy += 26 * S;
                    using (var nowrapF = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center })
                        for (int i = 0; i < probs.Length; i++)
                        {
                            var br = new RectangleF(cx0 + (i % fcols) * (bw + 10 * S), cy + (i / fcols) * (bh + 10 * S), bw, bh);
                            bool first = i == 0;
                            Theme.Fill(g, br, 10 * S, first ? Theme.Blue : Theme.Card); if (!first) Theme.Stroke(g, br, 10 * S, Theme.Track, 1);
                            using (var tb = new SolidBrush(first ? Color.White : Theme.Ink)) g.DrawString(probs[i][1], first ? Draw.fBodyB : Draw.fBody, tb, new RectangleF(br.X + 14 * S, br.Y, br.Width - 20 * S, br.Height), nowrapF);
                            string go = probs[i][0]; Act(overviewCanvas, br, delegate { FixGo(go); });
                            if (go.StartsWith("guide:", StringComparison.Ordinal)) Hint(overviewCanvas, br, "guide." + go.Substring(6));
                        }
                    cy += frows * (bh + 10 * S) + 4 * S;
                    using (var bl = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
                    {
                        var all = new RectangleF(cx0, cy, 200 * S, 22 * S); g.DrawString("See all problems  >", Draw.fBodyB, bl, all.X, all.Y);
                        Act(overviewCanvas, all, delegate { GoGuide(null); });
                    }
                    y = card.Bottom + 16 * S;
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
