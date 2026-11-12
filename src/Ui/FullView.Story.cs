// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;

namespace AdeeboMeter
{
    // Network story: one picture, ready when you open it.
    //   Pick what to follow (the busiest app is picked for you, or type a site) -> seven boxes from the app to the server,
    //   each with one value and a dot -> click a box for two lines and what's behind it -> "Time each step" runs the
    //   destination check and fills in the handshake bar; "Show real packets" needs Wireshark.
    // Drawing the picture sends nothing: it's built from the connection table and the pings already running.
    sealed partial class FullView
    {
        List<Conn> connSnap = new List<Conn>();
        string storyApp, storyTarget = "", storyIp = ""; int storyPort, storyBox = -1;

        static readonly string[] BoxName = { "App", "Windows", "Wi-Fi", "Router", "Provider", "Internet", "Server" };

        // pick the app's main conversation: the remote it talks to most, preferring 443
        void StoryPick(string app)
        {
            storyApp = app; storyBox = -1;
            var c = connSnap.Where(x => x.Proc == app && x.State == "ESTABLISHED" && Watch.IsPublic(x.RemoteIp))
                            .GroupBy(x => x.RemoteIp + ":" + x.RemotePort).OrderByDescending(gr => gr.Count() + (gr.First().RemotePort == 443 ? 100 : 0)).Select(gr => gr.First()).FirstOrDefault();
            if (c == null) return;
            storyIp = c.RemoteIp; storyPort = c.RemotePort; storyTarget = (c.Host != "" ? c.Host : c.RemoteIp) + (c.RemotePort != 443 ? ":" + c.RemotePort : "");
            travelBox.Box.Text = ""; travelDest = null; realPkts = null; realDecode = null; realErr = "";
            travelCanvas.Invalidate();
        }
        void StorySite()
        {
            string t = travelBox.Box.Text.Trim(); if (t == "") return;
            storyApp = null; storyTarget = t; storyIp = ""; storyPort = 0; storyBox = -1; realPkts = null; realDecode = null; realErr = "";
            FollowPacket();
        }

        float PaintStory(Graphics g, float x, float y, float w)
        {
            // apps using the internet right now
            var apps = connSnap.Where(c => c.State == "ESTABLISHED" && Watch.IsPublic(c.RemoteIp) && c.Proc != "" && c.Proc.IndexOf('(') < 0).GroupBy(c => c.Proc).OrderByDescending(gr => gr.Count()).Take(7).Select(gr => gr.Key).ToList();
            if (storyApp == null && storyTarget == "" && apps.Count > 0) StoryPick(apps[0]);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("FOLLOW"), Draw.fTiny, mb, x, y + 8 * S);
            float cx = x + 60 * S;
            foreach (var a in apps)
            {
                var ts = g.MeasureString(a, Draw.fBody); var cr = new RectangleF(cx, y, ts.Width + 22 * S, 28 * S); if (cr.Right > x + w) break;
                bool on = a == storyApp; Theme.Fill(g, cr, 14 * S, on ? Theme.Blue : Theme.Soft);
                using (var tb = new SolidBrush(on ? Color.White : Theme.Ink)) g.DrawString(a, Draw.fBody, tb, cx + 11 * S, y + (28 * S - ts.Height) / 2);
                string aa = a; Act(travelCanvas, cr, delegate { StoryPick(aa); }); cx += cr.Width + 8 * S;
            }
            if (apps.Count == 0) using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("Nothing is talking to the internet right now - type a site above."), Draw.fBody, mb, cx, y + 4 * S);
            y += 46 * S;
            if (storyTarget == "") return y;

            // the seven boxes
            var r = travelDest != null && travelDest.Input == storyTarget ? travelDest : null;
            string gw, kind, ssid, ch, localIp; int rx, sig; bool vpn; NetworkInterface nic;
            lock (M.L) { gw = M.Gateway; kind = M.AdapterKind; ssid = M.Ssid; ch = M.WifiChannel; rx = M.RxRate; sig = M.WifiSignal; vpn = M.VpnUp; nic = M.Primary; localIp = M.LocalIp; }
            double gms = M.GwPing.Avg(6), nms = M.NetPing.Avg(6);
            Func<double, string> ms = v => double.IsNaN(v) ? "no answer" : v.ToString("0", CultureInfo.CurrentCulture) + " ms";
            var mine = storyApp == null ? new List<Conn>() : connSnap.Where(c => c.Proc == storyApp).ToList();
            var conv = mine.FirstOrDefault(c => c.RemoteIp == storyIp && c.RemotePort == storyPort);
            string role = ""; try { if (storyIp != "") role = M365.Role(conv != null ? conv.Host : "", storyIp, storyPort); } catch (Exception e) { Program.Log("role: " + e.Message); }
            int hops = r != null && r.Story != null ? r.Story.Count : 0;
            string isp = M.Isp;
            var box = new[] {
                new[] { storyApp ?? "Your app", storyApp != null ? mine.Count + " connection" + (mine.Count == 1 ? "" : "s") : "you asked for it", "3" },
                new[] { conv != null ? conv.Proto.Split(' ')[0] + " :" + conv.LocalPort : "a socket", conv != null ? "to port " + conv.RemotePort : "port " + (storyPort > 0 ? storyPort : 443), "3" },
                new[] { kind == "Wi-Fi" ? (ch != "" ? "channel " + ch : "radio") : (nic != null && nic.Speed > 0 ? (nic.Speed / 1e6).ToString("0", CultureInfo.CurrentCulture) + " Mb/s" : kind), kind == "Wi-Fi" ? (sig >= 0 ? sig + "% signal" : rx > 0 ? rx + " Mb/s link" : "signal hidden") : "to the switch", kind == "Wi-Fi" && sig >= 0 && sig < 40 ? "1" : "0" },
                new[] { ms(gms), gw != "" ? gw : "router", double.IsNaN(gms) ? "2" : gms > 50 ? "2" : gms > 15 ? "1" : "0" },
                new[] { double.IsNaN(nms) || double.IsNaN(gms) ? "-" : "+" + Math.Max(0, nms - gms).ToString("0", CultureInfo.CurrentCulture) + " ms", vpn ? "VPN" : isp != "" ? Draw.Trunc(isp, 16) : "your provider", double.IsNaN(nms) ? "2" : nms - gms > 120 ? "1" : "0" },
                new[] { hops > 0 ? hops + " networks" : ms(nms), hops > 0 ? "on the way" : "to the internet", "3" },
                new[] { Draw.Trunc(conv != null && conv.Host != "" ? conv.Host : storyTarget, 22), role != "" ? Draw.Trunc(role, 24) : storyIp != "" ? storyIp : "", r != null && r.Level >= 0 ? r.Level.ToString(CultureInfo.InvariantCulture) : "3" } };
            float bw = (w - 6 * 14 * S) / 7, bh = 84 * S;
            for (int i = 0; i < 7; i++)
            {
                var br = new RectangleF(x + i * (bw + 14 * S), y, bw, bh); bool on = storyBox == i; int lv = int.Parse(box[i][2], CultureInfo.InvariantCulture);
                Theme.Fill(g, br, 12 * S, on ? Theme.BlueSoft : Theme.Soft); if (on) Theme.Stroke(g, br, 12 * S, Theme.Blue, 2);
                Draw.LvDot(g, lv, br.X + 10 * S, br.Y + 12 * S, 9 * S, 9 * S);
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString((i == 2 && kind != "Wi-Fi" ? "Cable" : BoxName[i]).ToUpperInvariant(), Draw.fTiny, mb, br.X + 24 * S, br.Y + 9 * S);
                using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    using (var ib = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(box[i][0], Draw.fBodyB, ib, new RectangleF(br.X + 10 * S, br.Y + 32 * S, bw - 16 * S, 20 * S), nowrap);
                    using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(box[i][1], Draw.fSmall, sb, new RectangleF(br.X + 10 * S, br.Y + 54 * S, bw - 16 * S, 18 * S), nowrap);
                }
                if (i < 6) using (var ap = new Pen(Theme.Rail, 2 * S)) { ap.CustomEndCap = new System.Drawing.Drawing2D.AdjustableArrowCap(3, 4); g.DrawLine(ap, br.Right + 2 * S, br.Y + bh / 2, br.Right + 12 * S, br.Y + bh / 2); }
                int ii = i; Act(travelCanvas, br, delegate { storyBox = storyBox == ii ? -1 : ii; if (ii == 3 && routeTable == null) LoadRoutes(); if (ii == 0 && m365Rows == null) LoadApps(); travelCanvas.Invalidate(); });
            }
            y += bh + 8 * S;
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(storyBox < 0 ? "Click a box to see what happens there." : "", Draw.fSmall, mb, x, y);
            y += 18 * S;

            // what happens in the chosen box: two lines, then what's behind it
            if (storyBox >= 0)
            {
                string[] two = {
                    "The app asks Windows for a connection - it never touches the network itself." + (storyApp != null ? " " + storyApp + " has " + mine.Count + " open right now." : ""),
                    "Windows wraps the data: TCP or UDP with port numbers, then the IP addresses " + (localIp != "" ? localIp : "of your PC") + " → " + (storyIp != "" ? storyIp : storyTarget) + ".",
                    kind == "Wi-Fi" ? "The packet goes over the radio to your access point" + (ssid != "" ? " (" + ssid + ")" : "") + ". Signal and channel decide how fast and how reliably." : "The packet goes over the cable to the switch and your router.",
                    "Your router picks the way out (the default route) and swaps your private address for its public one (NAT). It answers in " + ms(gms) + ".",
                    vpn ? "The VPN wraps the packet again and sends it to your company first." : "Your provider carries it into the internet" + (isp != "" ? " (" + isp + ")" : "") + " - it adds about " + (double.IsNaN(nms) || double.IsNaN(gms) ? "?" : Math.Max(0, nms - gms).ToString("0", CultureInfo.CurrentCulture)) + " ms here.",
                    "Each network hands it to the next; where two meet, BGP decides the route." + (hops > 0 ? "" : " Press Time each step to see the networks on the way."),
                    "The server unwraps every layer and answers the same way back." + (role != "" ? " For " + (storyApp ?? "this app") + " this server is: " + role + "." : "") };
                float th = Draw.Measure(two[storyBox], Draw.fBody, (int)(w - 28 * S)).Height;
                var tr = new RectangleF(x, y, w, th + 22 * S); Theme.Fill(g, tr, 10 * S, Theme.Soft);
                using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(two[storyBox], Draw.fBody, ib, new RectangleF(x + 14 * S, y + 11 * S, w - 28 * S, th + 4));
                y += tr.Height + 14 * S;
                switch (storyBox)
                {
                    case 0:
                        if (storyApp != null && (storyApp.StartsWith("Teams", StringComparison.OrdinalIgnoreCase) || storyApp.StartsWith("ms-teams", StringComparison.OrdinalIgnoreCase) || storyApp.StartsWith("Outlook", StringComparison.OrdinalIgnoreCase) || storyApp.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase) || storyApp.StartsWith("olk", StringComparison.OrdinalIgnoreCase)))
                            y = PaintApps(g, x, y, w);
                        else if (mine.Count > 0)
                            y = InfoCardOn(travelCanvas, g, "WHO " + storyApp.ToUpperInvariant() + " IS TALKING TO", mine.Where(c => c.RemoteIp != "").GroupBy(c => c.Host != "" ? c.Host : c.RemoteIp).OrderByDescending(gr => gr.Count()).Take(8).Select(gr => new[] { Draw.Trunc(gr.Key, 40), gr.Count() + " connection" + (gr.Count() == 1 ? "" : "s") + " · port " + gr.First().RemotePort, "3" }).ToList(), null, x, y, w);
                        break;
                    case 1:
                        { var t = Knowledge.Topic("apps-internet"); if (t != null && t["ladder"] != "") y = DrawKnowledgeLadder(g, travelCanvas, t["ladder"], x, y, w); }
                        if (Cfg.Details || chatPkts != null || LiveCapture.Available) { y += 10 * S; y = PaintChat(g, x, y, w); }
                        break;
                    case 2:
                        { int mtu, vmtu; lock (M.L) { mtu = M.PrimaryMtu; vmtu = M.VpnMtu; } y = PaintAnatomy(g, x, y, w, mtu > 0 ? mtu : 1500, vpn, vmtu); }
                        break;
                    case 3: y = PaintRoutes(g, x, y, w); break;
                    case 4: case 5:
                        if (r != null && r.Story != null) y = InfoCardOn(travelCanvas, g, "THE WAY TO " + r.Target.ToUpperInvariant(), r.Story.Select(s => new[] { s[0], s.Length > 1 ? s[1] : "", "3" }).ToList(), "card.PATH STORY", x, y, w);
                        break;
                    case 6:
                        if (r != null && r.TlsProto != "") y = InfoCardOn(travelCanvas, g, "THE LOCK (TLS)", new List<string[]> { new[] { "Version", r.TlsProto, "3" }, new[] { "Certificate from", r.TlsIssuer, "3" }, new[] { "Inspected on the way?", r.Inspected != "" ? "yes - re-signed by " + r.Inspected : "no", r.Inspected != "" ? "1" : "0" } }, "travel.L6·5", x, y, w);
                        break;
                }
                y += 6 * S;
            }

            // the timed handshake
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("HOW LONG EACH STEP TAKES"), Draw.fTiny, mb, x, y); Hint(travelCanvas, new RectangleF(x, y - 2 * S, w, 18 * S), "travel.LADDER");
            y += 22 * S;
            if (r == null || (!r.Done && !travelBusy))
            {
                string lab = "Time each step to " + Draw.Trunc(storyTarget, 40) + "  >"; var ls = g.MeasureString(lab, Draw.fBodyB); var lr = new RectangleF(x, y, ls.Width + 28 * S, 34 * S);
                Theme.Fill(g, lr, 17 * S, Theme.Blue); using (var wb = new SolidBrush(Color.White)) g.DrawString(lab, Draw.fBodyB, wb, x + 14 * S, y + (34 * S - ls.Height) / 2);
                Act(travelCanvas, lr, delegate { travelBox.Box.Text = storyTarget; FollowPacket(); travelBox.Box.Text = ""; });
                y += 42 * S;
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("Runs the same check as Check a destination: name, connect, lock, first byte and the way there. Nothing else is sent."), Draw.fSmall, mb, new RectangleF(x, y, w, 34 * S));
                return y + 34 * S;
            }
            if (travelBusy && !r.Done) { using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(travelStatus, Draw.fBody, mb, x, y); y += 26 * S; }
            var parts = new[] { Tuple.Create("Name (DNS)", r.DnsMs, Theme.C("#8250DF")), Tuple.Create("Connect (TCP)", r.TcpMs, Theme.C("#1F6FEB")), Tuple.Create("Lock (TLS)", r.TlsMs, Theme.C("#0E8A9E")), Tuple.Create("First byte", r.TtfbMs, Theme.C("#2DA44E")) }.Where(p => !double.IsNaN(p.Item2) && p.Item2 >= 0).ToList();
            double total = parts.Sum(p => p.Item2);
            if (parts.Count > 0 && total > 0)
            {
                float bx = x, barH = 30 * S;
                foreach (var p in parts)
                {
                    float pw = (float)(p.Item2 / total) * w; var pr = new RectangleF(bx, y, Math.Max(2, pw - 2 * S), barH);
                    using (var b = new SolidBrush(p.Item3)) g.FillRectangle(b, pr);
                    bx += pw;
                }
                y += barH + 8 * S;
                float lx = x;   // legend under the bar, so tiny steps stay readable
                foreach (var p in parts)
                {
                    string t = p.Item1 + "  " + ms(p.Item2); var ts = g.MeasureString(t, Draw.fSmall);
                    using (var b = new SolidBrush(p.Item3)) g.FillRectangle(b, lx, y + 4 * S, 10 * S, 10 * S);
                    using (var mb = new SolidBrush(Theme.Sub)) g.DrawString(t, Draw.fSmall, mb, lx + 14 * S, y);
                    lx += ts.Width + 30 * S;
                }
                y += 26 * S;
                var slow = parts.OrderByDescending(p => p.Item2).First();
                using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(ms(total) + " from name to first byte. Most of it: " + slow.Item1 + (slow.Item1.StartsWith("Connect", StringComparison.Ordinal) ? " - that's distance to the server." : slow.Item1.StartsWith("First", StringComparison.Ordinal) ? " - the server thinking." : "."), Draw.fBody, ib, new RectangleF(x, y, w, 40 * S));
                y += 30 * S;
            }
            if (r.Done && LiveCapture.Available) { y += 6 * S; y = PaintReal(g, x, y, w); }
            return y;
        }
    }
}
