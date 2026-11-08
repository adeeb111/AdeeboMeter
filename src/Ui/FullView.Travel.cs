// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // "How it travels": your own connection, one OSI / TCP-IP layer at a time. Everything runs only when you press a button.
    sealed partial class FullView
    {
        Panel travelPage; DB travelCanvas; RoundBox travelBox; PillButton travelGo;
        string travelStatus = ""; volatile bool travelBusy;
        DestResult travelDest; List<Routes.R> routeTable; List<M365.Row> m365Rows; Dictionary<int, string> m365Fam; string m365Fallback = ""; List<string[]> outlookRows;
        List<Packets.Pkt> realPkts; string realFile = "", realErr = "", realHost = ""; int realSel = -1; List<KeyValuePair<string, List<string>>> realDecode; HashSet<string> myIps = new HashSet<string>();
        List<Packets.Pkt> chatPkts; string chatErr = "";

        Panel BuildTravelPage()
        {
            travelPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            travelBox = new RoundBox(Sc(320), Draw.fInput, "Or type a site - e.g. github.com");
            travelBox.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; StorySite(); } };
            travelGo = new PillButton("Follow", PillButton.K.Primary); travelGo.Click += delegate { StorySite(); };
            bar.Controls.Add(travelBox); bar.Controls.Add(travelGo);
            travelCanvas = new DB { Name = "travelCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            travelCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintTravel(e.Graphics); }; Wire(travelCanvas);
            travelPage.Controls.Add(travelCanvas); travelPage.Controls.Add(bar);   // the five mode buttons are gone - the boxes lead to each part
            return travelPage;
        }

        void FollowPacket()
        {
            if (travelBusy) return;
            string input = travelBox.Box.Text.Trim(); if (input == "") input = destBox.Box.Text.Trim(); if (input == "") input = "github.com";
            travelBox.Box.Text = input;
            var r = new DestResult { Input = input }; travelDest = r; travelBusy = true; travelStatus = "Following the packet to " + input + "..."; travelCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            {
                try { DestCheck.Run(input, r, delegate { try { BeginInvoke((Action)travelCanvas.Invalidate); } catch { } }, M); routeTable = Routes.Read(); }
                catch (Exception e) { Program.Log("journey: " + e); }
                try { BeginInvoke((Action)delegate { travelBusy = false; travelStatus = ""; travelCanvas.Invalidate(); }); } catch { travelBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }
        void LoadRoutes() { Task.Factory.StartNew(() => { var t = Routes.Read(); try { BeginInvoke((Action)delegate { routeTable = t; travelCanvas.Invalidate(); }); } catch { } }); }
        void LoadApps()
        {
            Task.Factory.StartNew(() =>
            {
                List<M365.Row> rows = null; Dictionary<int, string> fam = null; string fb = ""; List<string[]> ol = null;
                try { fam = M365.FamilyByPid(); rows = M365.Connections(M); fb = M365.TcpFallback(M, rows); ol = M365.OutlookSlow(M, rows); } catch (Exception e) { Program.Log("m365: " + e.Message); }
                try { BeginInvoke((Action)delegate { m365Rows = rows; m365Fam = fam; m365Fallback = fb; outlookRows = ol; travelCanvas.Invalidate(); }); } catch { }
            });
        }

        void PaintTravel(Graphics g)
        {
            g.TranslateTransform(travelCanvas.AutoScrollPosition.X, travelCanvas.AutoScrollPosition.Y);
            Begin(travelCanvas);
            float x = 14 * S, y = 8 * S, w = travelCanvas.ClientSize.Width - 28 * S;
            y = PaintStory(g, x, y, w);
            var need = new Size(0, (int)(y + 30 * S)); if (travelCanvas.AutoScrollMinSize != need) { travelCanvas.AutoScrollMinSize = need; travelCanvas.Invalidate(); }
        }
        static float Lead(Graphics g, string t, float x, float y, float w, bool muted)
        {
            var h = Draw.Measure(t, Draw.fBody, (int)w).Height;
            using (var b = new SolidBrush(muted ? Theme.Muted : Theme.Sub)) g.DrawString(t, Draw.fBody, b, new RectangleF(x, y, w, h + 4));
            return y + h + 10 * S;
        }
        float Head(Graphics g, string t, float x, float y, float w, string key)
        {
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(t, Draw.fTiny, mb, x, y);
            if (key != null) Hint(travelCanvas, new RectangleF(x, y - 2 * S, w, 18 * S), key);
            return y + 22 * S;
        }

        // ---------- layer by layer
        static readonly string[] NoFamilies = { "-", "Teams and Office aren't running right now.", "3" }, NoRoles = { "-", "No live connections from these apps right now.", "3" };
        static readonly Color[] LayerCol = { Theme.C("#8250DF"), Theme.C("#1F6FEB"), Theme.C("#0E8A9E"), Theme.C("#2DA44E"), Theme.C("#BF8700"), Theme.C("#CF222E"), Theme.C("#57606A") };
        float PaintAnatomy(Graphics g, float x, float y, float w, int mtu, bool vpn, int vpnMtu)
        {
            y = Head(g, "PACKET ANATOMY  ·  WHAT'S INSIDE ONE FULL-SIZE PACKET ON YOUR LINK", x, y, w, "travel.ANATOMY");
            int payload = Math.Max(0, mtu - 20 - 20 - 5);
            var parts = new List<Tuple<string, int, Color>> { Tuple.Create("Ethernet 14", 14, LayerCol[4]), Tuple.Create("IP 20", 20, LayerCol[3]), Tuple.Create("TCP 20", 20, LayerCol[2]), Tuple.Create("TLS 5", 5, LayerCol[1]), Tuple.Create("your data " + payload, payload, LayerCol[0]) };
            float total = parts.Sum(p => p.Item2), bx = x, bh = 30 * S, min = 54 * S;
            float scale = (w - min * 4) / total;
            foreach (var p in parts)
            {
                float pw = p.Item2 == payload ? Math.Max(min, p.Item2 * scale) : min;
                Theme.Fill(g, new RectangleF(bx, y, pw - 3 * S, bh), 6 * S, p.Item3);
                using (var wb = new SolidBrush(Color.White)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(p.Item1, Draw.fSmall, wb, new RectangleF(bx, y, pw - 3 * S, bh), c);
                bx += pw;
            }
            y += bh + 10 * S;
            string t = "Your link carries packets of up to " + mtu + " bytes (the MTU). Headers take " + (mtu - payload) + " of them, so each packet holds " + payload + " bytes of your data. " +
                       (mtu == 1492 ? "1492 means PPPoE (DSL / some fibre): the provider's tunnel takes 8 bytes. " : "") +
                       (vpn ? "Through your VPN the usable size drops to " + (vpnMtu > 0 ? vpnMtu.ToString(CultureInfo.CurrentCulture) : "about 1400") + ": IPsec adds ~60 bytes, SSL VPNs ~80. " : "") +
                       "Tunnels add more: GRE 24, VXLAN 50. If something on the way drops big packets without saying so (a path-MTU \"black hole\"), small pages load but big downloads hang - Check a destination tests for that.";
            return Lead(g, t, x, y, w, false);
        }

        // actors across the top; steps = { fromIndex, toIndex, label, time }
        static float DrawLadder(Graphics g, Control host, string[] actors, List<string[]> steps, float x, float y, float w)
        {
            float colW = w / actors.Length, rowH = 30 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var c = new StringFormat { Alignment = StringAlignment.Center })
                for (int i = 0; i < actors.Length; i++) g.DrawString(Draw.Trunc(actors[i], 26), Draw.fBodyB, ink, new RectangleF(x + i * colW, y, colW, 20 * S), c);
            float top = y + 24 * S, bottom = top + steps.Count * rowH + 8 * S;
            using (var lp = new Pen(Theme.Line, 2 * S)) for (int i = 0; i < actors.Length; i++) { float cx = x + i * colW + colW / 2; g.DrawLine(lp, cx, top, cx, bottom); }
            float sy = top + 16 * S;
            foreach (var st in steps)
            {
                int a = int.Parse(st[0], CultureInfo.InvariantCulture), b = int.Parse(st[1], CultureInfo.InvariantCulture);
                float x1 = x + a * colW + colW / 2, x2 = x + b * colW + colW / 2;
                using (var p = new Pen(a < b ? Theme.Blue : Theme.Green, 2 * S)) { p.CustomEndCap = new AdjustableArrowCap(4, 5); g.DrawLine(p, x1, sy, x2 + (a < b ? -4 * S : 4 * S), sy); }
                using (var sub = new SolidBrush(Theme.Sub)) using (var c = new StringFormat { Alignment = StringAlignment.Center })
                    g.DrawString(st[2], Draw.fSmall, sub, new RectangleF(Math.Min(x1, x2), sy - 17 * S, Math.Abs(x2 - x1), 16 * S), c);
                if (st[3] != "") using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(st[3], Draw.fMonoS, mb, Math.Max(x1, x2) + 8 * S, sy - 8 * S);
                sy += rowH;
            }
            return bottom + 14 * S;
        }

        // ---------- routing table, explained
        float PaintRoutes(Graphics g, float x, float y, float w)
        {
            if (routeTable == null) { LoadRoutes(); return Lead(g, "Reading the routing table...", x, y, w, true); }
            y = Lead(g, "Every packet that leaves your PC is matched against this table. The most specific match wins (the longest prefix - a /24 beats a /0); if two are equally specific, the lowest metric wins. Type an address in the box above and press Enter on this page to see which route it takes.", x, y + 4 * S, w, false);
            IPAddress ask;
            if (IPAddress.TryParse(storyIp, out ask) && ask.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                List<Routes.R> m; var b = Routes.Best(routeTable, ask, out m);
                int pick = Routes.WindowsPick(ask); var nic = Routes.Nics(); NetworkInterface ni;
                var rows = new List<string[]> { new[] { "Winner", Routes.Plain(b), "0" } };
                foreach (var o in m.Skip(1).Take(4)) rows.Add(new[] { "Also matched", Routes.Ip(o.Dest) + "/" + o.Prefix + " via " + o.Gateway + " (metric " + o.Metric + ") - less specific or costlier", "3" });
                rows.Add(new[] { "Windows' own pick", pick > 0 && nic.TryGetValue(pick, out ni) ? ni.Name + (Monitor.IsVpn(ni) ? " (VPN)" : "") : "-", "3" });
                y = InfoCardOn(travelCanvas, g, "WHICH ROUTE WINS FOR " + ask, rows, "travel.WINS", x, y, w);
            }
            y = Head(g, "ACTIVE ROUTES (IPv4)  ·  " + routeTable.Count, x, y, w, "travel.ROUTES");
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var line = new Pen(Theme.Line2))
            {
                foreach (var r in routeTable.Where(r => r.Dest >> 28 != 14 && r.Dest != 0xFFFFFFFF).OrderBy(r => r.Prefix).ThenBy(r => r.Metric).Take(60))
                {
                    string what = r.Prefix == 0 ? "default route - everything not listed below" : r.Prefix == 32 ? (r.Dest >> 24 == 127 ? "this PC itself" : "one address") : (r.Dest >> 24) == 127 ? "loopback (this PC)" : "a network of " + Math.Pow(2, 32 - r.Prefix).ToString("N0", CultureInfo.CurrentCulture) + " addresses";
                    g.DrawString(Routes.Ip(r.Dest) + "/" + r.Prefix, Draw.fMono, ink, x, y);
                    g.DrawString((r.OnLink ? "on this network" : "via " + r.Gateway) + " · " + Draw.Trunc(r.Iface, 22) + (r.Vpn ? " (VPN)" : "") + " · metric " + r.Metric, Draw.fSmall, muted, x + 190 * S, y + 2 * S);
                    g.DrawString(what, Draw.fSmall, muted, x + w * 0.66f, y + 2 * S);
                    y += 22 * S; g.DrawLine(line, x, y - 2 * S, x + w, y - 2 * S);
                }
            }
            // addresses, DHCP lease, IPv6
            NetworkInterface pn; lock (M.L) pn = M.Primary;
            if (pn != null)
            {
                var rows = new List<string[]>();
                try
                {
                    var ip = pn.GetIPProperties();
                    foreach (var u in ip.UnicastAddresses)
                    {
                        bool v6 = u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
                        string how = !v6 ? (u.PrefixOrigin == PrefixOrigin.Dhcp ? "from your router by DHCP" + (u.DhcpLeaseLifetime > 0 ? " · lease " + TimeSpan.FromSeconds(u.DhcpLeaseLifetime).TotalHours.ToString("0.#", CultureInfo.CurrentCulture) + " h" : "") : u.PrefixOrigin == PrefixOrigin.Manual ? "set by hand" : u.PrefixOrigin.ToString())
                                          : u.Address.IsIPv6LinkLocal ? "link-local - every IPv6 adapter has one, only for this network" : u.SuffixOrigin == SuffixOrigin.Random ? "temporary (private) - changes daily so websites can't track this PC" : u.PrefixOrigin == PrefixOrigin.RouterAdvertisement ? "built from your router's advertisement (SLAAC)" : u.PrefixOrigin == PrefixOrigin.Dhcp ? "from DHCPv6" : u.PrefixOrigin.ToString();
                        rows.Add(new[] { v6 ? "IPv6" : "IPv4", u.Address + "  ·  " + how, "3" });
                    }
                    var dhcp = ip.DhcpServerAddresses.Select(a => a.ToString()).ToList();
                    if (dhcp.Count > 0) rows.Add(new[] { "DHCP server", string.Join(", ", dhcp), "3" });
                    rows.Add(new[] { "DNS servers", string.Join(", ", ip.DnsAddresses.Select(a => a.ToString()).Take(4)), "3" });
                    if (ip.DnsSuffix != "") rows.Add(new[] { "DNS suffix", ip.DnsSuffix + " - short names like 'printer' are tried as 'printer." + ip.DnsSuffix + "'", "3" });
                }
                catch (Exception e) { rows.Add(new[] { "Addresses", e.Message, "1" }); }
                y = InfoCardOn(travelCanvas, g, "HOW YOUR PC GOT ITS ADDRESSES  ·  " + Draw.Trunc(pn.Name, 30).ToUpperInvariant(), rows, "travel.ADDR", x, y + 10 * S, w);
            }
            return y;
        }

        // ---------- Teams and Office, end to end
        float PaintApps(Graphics g, float x, float y, float w)
        {
            if (m365Rows == null) { LoadApps(); return Lead(g, "Looking at Teams and Office...", x, y + 4 * S, w, true); }
            y = Lead(g, "Which program does what, and what each one sends where - live from this PC. Roles come from server names and Microsoft's published Teams ranges (" + M365.AsOf + "); anything else from Microsoft shows as \"role unknown\" rather than a guess.", x, y + 4 * S, w, false);
            var famCount = m365Fam == null ? new List<string[]>() : m365Fam.Values.GroupBy(v => v).Select(gr => new[] { gr.Key, gr.Count() + (gr.Count() == 1 ? " process" : " processes"), "3" }).ToList();
            if (famCount.Count == 0) famCount.Add(NoFamilies);
            y = InfoCardOn(travelCanvas, g, "THE PROCESS FAMILIES", famCount, "travel.FAMILIES", x, y, w);
            var roles = m365Rows.GroupBy(r => r.App + "|" + (r.Role == "" ? "other" : r.Role)).Select(gr => new[] { gr.First().App, (gr.First().Role == "" ? "Other (not Microsoft)" : gr.First().Role) + " · " + gr.Count() + " connection" + (gr.Count() == 1 ? "" : "s") + " · e.g. " + Draw.Trunc(gr.First().Host != "" ? gr.First().Host : gr.First().Remote, 40), "3" }).OrderBy(r => r[0]).ToList();
            if (roles.Count == 0) roles.Add(NoRoles);
            y = InfoCardOn(travelCanvas, g, "WHAT THEY'RE TALKING TO  ·  BY ROLE", roles, "travel.ROLES", x, y, w);
            y = InfoCardOn(travelCanvas, g, "TEAMS CALLS: UDP OR TCP?", new List<string[]> { new[] { "Right now", m365Fallback, m365Fallback.StartsWith("Teams seems", StringComparison.Ordinal) ? "1" : "3" } }, "travel.FALLBACK", x, y, w);
            var call = Knowledge.Topic("teams-call");
            if (call != null && call["ladder"] != "") { y = Head(g, "HOW A TEAMS CALL CONNECTS", x, y, w, "travel.CALLFLOW"); y = DrawKnowledgeLadder(g, travelCanvas, call["ladder"], x, y, w); }
            if (outlookRows != null) y = InfoCardOn(travelCanvas, g, "WHY IS OUTLOOK SLOW?  ·  THE USUAL SUSPECTS, CHECKED", outlookRows.Select(r => new[] { r[1], r[2] + (r[3] != "" ? " - " + r[3] : ""), r[0] }).ToList(), "travel.OUTLOOK", x, y, w);
            var o = Knowledge.Topic("outlook-flow");
            if (o != null && o["ladder"] != "") { y = Head(g, "HOW OUTLOOK GETS YOUR MAIL", x, y, w, null); y = DrawKnowledgeLadder(g, travelCanvas, o["ladder"], x, y, w); }
            var refresh = new RectangleF(x, y, 120 * S, 22 * S);
            using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString("Refresh  ↻", Draw.fBodyB, lb, refresh.X, refresh.Y);
            Act(travelCanvas, refresh, delegate { m365Rows = null; travelCanvas.Invalidate(); });
            return y + 30 * S;
        }
        // "A > B: text | B > A: text" from the knowledge pack
        static float DrawKnowledgeLadder(Graphics g, Control host, string spec, float x, float y, float w)
        {
            var actors = new List<string>(); var steps = new List<string[]>();
            foreach (var part in spec.Split('|'))
            {
                int c = part.IndexOf(':'); if (c < 0) continue;
                var ends = part.Substring(0, c).Split('>'); if (ends.Length != 2) continue;
                string a = ends[0].Trim(), b = ends[1].Trim();
                if (!actors.Contains(a)) actors.Add(a); if (!actors.Contains(b)) actors.Add(b);
                steps.Add(new[] { actors.IndexOf(a).ToString(CultureInfo.InvariantCulture), actors.IndexOf(b).ToString(CultureInfo.InvariantCulture), part.Substring(c + 1).Trim(), "" });
            }
            return steps.Count == 0 ? y : DrawLadder(g, host, actors.ToArray(), steps, x, y, w);
        }

        // ---------- real packets (Wireshark): the actual handshake, and any packet explained
        float PaintReal(Graphics g, float x, float y, float w)
        {
            if (!LiveCapture.Available) return Lead(g, "Real packets need Wireshark (with Npcap). Everything else on this tab works without it.", x, y + 6 * S, w, true);
            string host = storyTarget != "" ? storyTarget : "github.com";
            y = Lead(g, "Records 6 seconds of your PC's traffic to " + host + " while AdeeboMeter opens one connection to it (handshake only - no data is sent), then shows the real packets as a ladder. Click any packet to see every layer explained. The recording is deleted when AdeeboMeter exits.", x, y + 4 * S, w, false);
            var go = new RectangleF(x, y, 360 * S, 24 * S);
            using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(travelBusy ? travelStatus : "Capture the real handshake with " + host + "  →", Draw.fBodyB, lb, go.X, go.Y);
            if (!travelBusy) Act(travelCanvas, go, delegate { CaptureReal(host); });
            y += 34 * S;
            if (realErr != "") y = Lead(g, realErr, x, y, w, true);
            if (realPkts == null) return y;
            var shown = realPkts.Take(40).ToList();
            y = Head(g, "THE REAL PACKETS  ·  " + realPkts.Count + " RECORDED  ·  CLICK ONE TO EXPLAIN IT", x, y, w, "travel.REAL");
            float colA = x + 90 * S, colB = x + w - 90 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString("Your PC", Draw.fBodyB, ink, colA - 30 * S, y); g.DrawString(realHost, Draw.fBodyB, ink, colB - 60 * S, y); y += 24 * S;
                float top = y;
                using (var lp = new Pen(Theme.Line, 2 * S)) { g.DrawLine(lp, colA, top, colA, top + shown.Count * 26 * S); g.DrawLine(lp, colB, top, colB, top + shown.Count * 26 * S); }
                foreach (var p in shown)
                {
                    bool outb = myIps.Contains(p.Src); float yy = y + 13 * S;
                    var rowR = new RectangleF(x, y, w, 26 * S);
                    if (p.No == realSel) Theme.Fill(g, rowR, 6 * S, Theme.RowSel);
                    using (var pen = new Pen(outb ? Theme.Blue : Theme.Green, 2 * S)) { pen.CustomEndCap = new AdjustableArrowCap(4, 5); if (outb) g.DrawLine(pen, colA, yy, colB - 4 * S, yy); else g.DrawLine(pen, colB, yy, colA + 4 * S, yy); }
                    g.DrawString(p.T.ToString("0.000", CultureInfo.InvariantCulture), Draw.fMonoS, muted, x, y + 5 * S);
                    using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    using (var sub = new SolidBrush(Theme.Sub)) g.DrawString(p.Proto + "  " + p.Info, Draw.fSmall, sub, new RectangleF(colA + 10 * S, y - 1 * S, colB - colA - 20 * S, 16 * S), nowrap);
                    int no = p.No; Act(travelCanvas, rowR, delegate { ExplainPacket(no); });
                    y += 26 * S;
                }
            }
            if (realDecode != null)
            {
                y += 14 * S; y = Head(g, "PACKET " + realSel + ", EVERY LAYER  ·  WIRESHARK'S DECODE WITH WHAT IT MEANS", x, y, w, "travel.DECODE");
                foreach (var layer in realDecode)
                {
                    string story = Packets.LayerStory(layer.Key);
                    var rows = new List<string[]>(); if (story != "") rows.Add(new[] { "What it is", story, "3" });
                    rows.AddRange(layer.Value.Take(12).Select(f => { int c = f.IndexOf(':'); return c > 0 ? new[] { Draw.Trunc(f.Substring(0, c), 28), f.Substring(c + 1).Trim(), "3" } : new[] { "", f, "3" }; }));
                    y = InfoCardOn(travelCanvas, g, Draw.Trunc(layer.Key, 110).ToUpperInvariant(), rows, null, x, y, w);
                }
            }
            return y;
        }
        void CaptureReal(string host)
        {
            if (travelBusy) return;
            travelBusy = true; travelStatus = "Recording 6 seconds..."; realErr = ""; realPkts = null; realDecode = null; realSel = -1; travelCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            {
                string err = "", file = ""; List<Packets.Pkt> pk = null; string h = host; int port = 443;
                try
                {
                    string hh; int pp; bool t; string e;
                    if (DestCheck.Parse(host, out hh, out pp, out t, out e)) { h = hh; port = pp; }
                    var addr = Dns.GetHostAddresses(h).FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (addr == null) err = "Couldn't find " + h + ".";
                    else
                    {
                        var mine = new HashSet<string>(); foreach (var n in NetworkInterface.GetAllNetworkInterfaces()) try { foreach (var u in n.GetIPProperties().UnicastAddresses) mine.Add(u.Address.ToString()); } catch { }
                        myIps = mine;
                        pk = Packets.Capture(M, "host " + addr, 6, delegate { Packets.Handshake(addr.ToString(), port); }, out file, out err);
                    }
                }
                catch (Exception e) { err = Draw.Trunc(e.Message, 80); }
                try { BeginInvoke((Action)delegate { travelBusy = false; realPkts = pk; realErr = err != "" ? err : pk != null && pk.Count == 0 ? "No packets were seen - is a VPN or proxy carrying this traffic?" : ""; realFile = file; realHost = h; travelCanvas.Invalidate(); }); } catch { travelBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }
        void ExplainPacket(int no)
        {
            realSel = no; realDecode = null; travelCanvas.Invalidate(); string f = realFile;
            Task.Factory.StartNew(() => { var d = Packets.Decode(f, no); try { BeginInvoke((Action)delegate { if (realSel == no) { realDecode = d; travelCanvas.Invalidate(); } }); } catch { } });
        }

        // ---------- what my PC is saying: protocols, background chatter, call priority
        float PaintChat(Graphics g, float x, float y, float w)
        {
            if (!LiveCapture.Available) return Lead(g, "This needs Wireshark (with Npcap) - it listens to your PC's own traffic for 10 seconds.", x, y + 6 * S, w, true);
            y = Lead(g, "Listens to your PC's own traffic for 10 seconds and tells you what it's made of: modern or old protocols, IPv4 or IPv6, and the background chatter Windows sends to your local network. Nothing is kept.", x, y + 4 * S, w, false);
            var go = new RectangleF(x, y, 300 * S, 24 * S);
            using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(travelBusy ? travelStatus : "Listen for 10 seconds  →", Draw.fBodyB, lb, go.X, go.Y);
            if (!travelBusy) Act(travelCanvas, go, delegate
            {
                travelBusy = true; travelStatus = "Listening..."; chatPkts = null; chatErr = ""; travelCanvas.Invalidate();
                Task.Factory.StartNew(() =>
                {
                    string err, file; var pk = Packets.Capture(M, "", 10, null, out file, out err);
                    try { BeginInvoke((Action)delegate { travelBusy = false; chatPkts = pk; chatErr = err; travelCanvas.Invalidate(); }); } catch { travelBusy = false; }
                }, TaskCreationOptions.LongRunning);
            });
            y += 34 * S;
            if (chatErr != "") y = Lead(g, chatErr, x, y, w, true);
            if (chatPkts == null || chatPkts.Count == 0) return y;
            int n = chatPkts.Count;
            var byProto = chatPkts.GroupBy(p => p.Proto).OrderByDescending(gr => gr.Count()).Take(10).Select(gr => new[] { gr.Key, gr.Count() + " packets (" + (100.0 * gr.Count() / n).ToString("0", CultureInfo.CurrentCulture) + "%)", "3" }).ToList();
            y = InfoCardOn(travelCanvas, g, "WHAT YOUR TRAFFIC IS MADE OF  ·  " + n + " PACKETS", byProto, "travel.CHAT", x, y, w);
            int v6 = chatPkts.Count(p => p.IpVer == "6" || p.Src.Contains(":")), quic = chatPkts.Count(p => p.Proto == "QUIC");
            var notes = new List<string[]>
            {
                new[] { "IPv6", v6 == 0 ? "not used - everything is IPv4" : (100.0 * v6 / n).ToString("0", CultureInfo.CurrentCulture) + "% of packets - your network has IPv6", "3" },
                new[] { "HTTP/3 (QUIC)", quic > 0 ? "yes - " + quic + " packets: the modern, faster web over UDP" : "none in these 10 s", "3" },
            };
            foreach (var chat in new[] { new[] { "LLMNR", "LLMNR - old local name lookup. A known attack target (responder tools); IT usually turns it off." }, new[] { "NBNS", "NetBIOS name service - very old Windows networking chatter. Same advice as LLMNR." }, new[] { "MDNS", "mDNS - devices announcing themselves (printers, TVs, phones). Harmless at home." }, new[] { "SSDP", "SSDP / UPnP - finding media devices and routers. Harmless at home; noisy on big networks." } })
            {
                int c = chatPkts.Count(p => string.Equals(p.Proto, chat[0], StringComparison.OrdinalIgnoreCase));
                if (c > 0) notes.Add(new[] { chat[0], c + " packets · " + chat[1], chat[0] == "LLMNR" || chat[0] == "NBNS" ? "1" : "3" });
            }
            var callPk = chatPkts.Where(p => { int a, b; int.TryParse(p.SPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out a); int.TryParse(p.DPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out b); return (a >= 3478 && a <= 3481) || (b >= 3478 && b <= 3481) || (a >= 50000 && a <= 50059); }).ToList();
            if (callPk.Count > 0)
            {
                int ef = callPk.Count(p => p.Dscp == "46"), af = callPk.Count(p => p.Dscp == "34");
                notes.Add(new[] { "Call priority (DSCP)", ef + af > 0 ? (ef > 0 ? ef + " audio packets marked EF (46) - top priority" : "") + (af > 0 ? " · " + af + " video packets AF41 (34)" : "") + ". Routers that honour it put your call first." : "Your call packets carry no priority mark - Windows only marks Teams traffic when IT sets a QoS policy. Fine at home; on busy office networks calls may suffer.", ef + af > 0 ? "0" : "1" });
            }
            return InfoCardOn(travelCanvas, g, "WHAT IT MEANS", notes, "travel.CHATNOTES", x, y, w);
        }

        // InfoCard drawn on any canvas (rows: { label, text, level })
        float InfoCardOn(Control host, Graphics g, string title, List<string[]> rows, string key, float x, float y, float w)
        {
            float lw = 190 * S, tw = w - lw - 28 * S; var hs = rows.Select(r => Math.Max(22 * S, Draw.Measure(r[1], Draw.fBody, (int)tw).Height + 6 * S)).ToList();
            var card = new RectangleF(x, y, w, 40 * S + hs.Sum());
            Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(title, Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            if (key != null) Hint(host, new RectangleF(x, y, w, 26 * S), key);
            float ry = y + 34 * S;
            using (var muted = new SolidBrush(Theme.Muted))
                for (int i = 0; i < rows.Count; i++)
                {
                    int l = 3; if (rows[i].Length > 2) int.TryParse(rows[i][2], NumberStyles.Integer, CultureInfo.InvariantCulture, out l);
                    g.DrawString(rows[i][0], Draw.fBody, muted, new RectangleF(x + 14 * S, ry, lw - 8 * S, 20 * S));
                    using (var tb = new SolidBrush(l == 1 || l == 2 ? Theme.LvText(l) : Theme.Ink)) g.DrawString(rows[i][1], Draw.fBody, tb, new RectangleF(x + 14 * S + lw, ry, tw, hs[i]));
                    ry += hs[i];
                }
            return y + card.Height + 16 * S;
        }
    }
}
