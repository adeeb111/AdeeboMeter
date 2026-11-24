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
        bool storyAllApps;   // the app chips: the busiest seven, or every app with an internet connection
        string storyApp, storyTarget = "", storyIp = "", storyNote = ""; int storyPort, storyLocal, storyBox = -1; bool storyUdp;   // storyUdp: kept from the connection, so a closed one still knows (bug-hunt ML-75)

        // an IPv6 address keeps its two ends (2607:f8b0:…:200e) - the middle is what a box can lose; names are cut as before
        static string ShortIp(string s, int max)
        {
            if (s == null || s.Length <= max) return s ?? "";
            var p = s.Trim('[', ']').Split(':'); if (p.Length >= 4 && s.IndexOf('.') < 0) return p[0] + ":" + p[1] + ":…:" + p[p.Length - 1];
            return Draw.Trunc(s, max);
        }
        static readonly string[] BoxName = { "App", "Windows", "Wi-Fi", "Router", "Provider", "Internet", "Server" };

        // pick the app's main conversation: the remote it talks to most, preferring 443
        void StoryPick(string app)
        {
            storyApp = app; storyBox = -1;
            var c = connSnap.Where(x => x.Proc == app && x.State == "ESTABLISHED" && Watch.IsPublic(x.RemoteIp))
                            .GroupBy(x => x.RemoteIp + ":" + x.RemotePort).OrderByDescending(gr => gr.Count() + (gr.First().RemotePort == 443 ? 100 : 0)).Select(gr => gr.First()).FirstOrDefault();
            if (c == null) return;
            StoryFollow(c);
        }
        // follow one exact connection of the picked app (1.1.7 idea from PR #29's X-Ray, done in the story itself)
        void StoryFollow(Conn c)
        {
            storyBox = -1;
            storyIp = c.RemoteIp; storyPort = c.RemotePort; storyLocal = c.LocalPort; storyNote = c.RemoteIp.Contains(":") ? "The app talks over IPv6: the route card covers IPv4 only - the trace still runs." : ""; /* bug-hunt ML-234 */ storyUdp = (c.Proto ?? "").StartsWith("UDP", StringComparison.Ordinal);
            string h = c.Host != "" ? c.Host : c.RemoteIp.Contains(":") ? "[" + c.RemoteIp + "]" : c.RemoteIp;   // AM-CG-261: an IPv6 address with a port is written [addr]:port
            storyTarget = h + (c.RemotePort != 443 ? ":" + c.RemotePort : "");
            travelBox.Box.Text = ""; travelDest = null; realPkts = null; realDecode = null; realErr = "";
            travelCanvas.Invalidate();
        }
        void StorySite()
        {
            string t = travelBox.Box.Text.Trim(); if (t == "") return;
            storyApp = null; storyTarget = t; storyIp = ""; storyPort = 0; storyLocal = 0; storyUdp = false; storyNote = ""; storyBox = -1; realPkts = null; realDecode = null; realErr = "";
            // a plain IPv4 address: the route card answers it straight away (the routing table is local - nothing is sent for it)
            System.Net.IPAddress typed; if (System.Net.IPAddress.TryParse(t, out typed) && typed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && t.Count(ch => ch == '.') == 3) storyIp = typed.ToString();
            else if (typed != null && typed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) storyNote = "An IPv6 address: the route card covers IPv4 only - the trace still runs.";
            FollowPacket();
        }

        static readonly string[] StoryPoints = { "The app and the port it uses", "Your Wi-Fi or cable, the router and your provider", "How many networks it crosses and who answers at the end" };
        float PaintStory(Graphics g, float x, float y, float w)
        {
            // apps using the internet right now
            var apps = connSnap.Where(c => c.State == "ESTABLISHED" && Watch.IsPublic(c.RemoteIp) && c.Proc != "" && c.Proc.IndexOf('(') < 0).GroupBy(c => c.Proc).OrderByDescending(gr => gr.Count()).Select(gr => gr.Key).ToList();
            int moreApps = storyAllApps ? 0 : Math.Max(0, apps.Count - 7); if (!storyAllApps) apps = apps.Take(7).ToList();
            if (storyApp != null && !apps.Contains(storyApp) && connSnap.Any(c => c.Proc == storyApp)) apps.Add(storyApp);   // the one you picked stays visible
            if (storyApp == null && storyTarget == "" && apps.Count > 0) StoryPick(apps[0]);
            // 24 Nov (Adeeb: "Network story page looks same old"): the page in cards like every other one - what to follow,
            // the way there, the timing and the packet's labels - with room between them
            float cardTop = y, ox = x, ow = w; x += 18 * S; w -= 36 * S; y += 16 * S;
            using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(L.T("Follow an app"), Draw.fHead, ib, x, y);
            y += 30 * S;
            float cx = x;
            foreach (var a in apps)
            {
                string an = a.Length > 28 ? a.Substring(0, 27) + "…" : a; var ts = g.MeasureString(an, Draw.fBody); if (cx > x && cx + ts.Width + 24 * S > x + w) { cx = x; y += 40 * S; }   // AM-CG-262: wraps, never silently drops an app
                var cr = new RectangleF(cx, y, ts.Width + 24 * S, 30 * S);
                bool on = a == storyApp; Theme.Fill(g, cr, 15 * S, on ? Theme.Blue : Theme.Soft);
                using (var tb = new SolidBrush(on ? Theme.OnBlue : Theme.Ink)) g.DrawString(an, Draw.fBody, tb, cx + 12 * S, y + (30 * S - ts.Height) / 2);
                string aa = a; Act(travelCanvas, cr, delegate { StoryPick(aa); }); cx += cr.Width + 8 * S;
            }
            if (moreApps > 0 || storyAllApps)   // every app, not only the busiest seven
            {
                string mt = storyAllApps ? L.T("Fewer") : "+" + moreApps + " " + L.T("more"); var ms0 = g.MeasureString(mt, Draw.fBody);
                if (cx > x && cx + ms0.Width + 24 * S > x + w) { cx = x; y += 40 * S; }
                var mr = new RectangleF(cx, y, ms0.Width + 24 * S, 30 * S); Theme.Stroke(g, mr, 15 * S, Theme.Line, 1);
                using (var tb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(mt, Draw.fBody, tb, cx + 12 * S, y + (30 * S - ms0.Height) / 2);
                Act(travelCanvas, mr, delegate { storyAllApps = !storyAllApps; travelCanvas.Invalidate(); }); cx += mr.Width + 8 * S;
            }
            if (apps.Count == 0) using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("No app has an open TCP connection to the internet right now - type a site above."), Draw.fBody, mb, cx, y + 5 * S);
            y += 30 * S + 18 * S;
            // the picked app's connections: each one can be the story (chips, the followed one highlighted)
            if (storyApp != null)
            {
                var its = connSnap.Where(c => c.Proc == storyApp && c.RemoteIp != "" && Watch.IsPublic(c.RemoteIp)).GroupBy(c => c.RemoteIp + "|" + c.RemotePort)
                    .Select(gr => gr.First()).OrderByDescending(c => c.State == "ESTABLISHED").ThenBy(c => c.Host == "" ? c.RemoteIp : c.Host).ToList();
                if (its.Count > 1)
                {
                    using (var mb = new SolidBrush(Theme.Sub)) g.DrawString(Kit.Sent(L.T("ITS CONNECTIONS")) + " (" + its.Count + ")", Draw.fBodyB, mb, x, y);
                    y += 26 * S; float kx = x; int shown = 0;
                    foreach (var c in its)
                    {
                        if (shown == 12) { using (var mb = new SolidBrush(Theme.Sub)) g.DrawString("+" + (its.Count - 12) + " " + L.T("in Connections"), Draw.fSmall, mb, kx, y + 6 * S); break; }
                        string ct = Draw.Trunc(c.Host != "" ? c.Host : c.RemoteIp, 30) + (c.RemotePort != 443 ? ":" + c.RemotePort : "") + (c.State == "ESTABLISHED" || c.State == "" ? "" : " · " + c.State.ToLowerInvariant());
                        var cs = g.MeasureString(ct, Draw.fSmall); if (kx > x && kx + cs.Width + 20 * S > x + w) { kx = x; y += 36 * S; }
                        var kr = new RectangleF(kx, y, cs.Width + 20 * S, 28 * S); bool on = c.RemoteIp == storyIp && c.RemotePort == storyPort;
                        Theme.Fill(g, kr, 14 * S, on ? Theme.BlueSoft : Theme.Soft); if (on) Theme.Stroke(g, kr, 14 * S, Theme.Blue, 1);
                        using (var tb = new SolidBrush(on ? (Theme.Dark ? Theme.Ink : Theme.BlueDark) : Theme.Ink)) g.DrawString(ct, Draw.fSmall, tb, kx + 10 * S, y + (28 * S - cs.Height) / 2);
                        var cc = c; Act(travelCanvas, kr, delegate { StoryFollow(cc); }); kx += kr.Width + 8 * S; shown++;
                    }
                    y += 28 * S + 18 * S;
                }
            }
            if (storyNote != "") using (var nb = new SolidBrush(Theme.Sub)) { float nh = Draw.Measure(L.T(storyNote), Draw.fSmall, (int)w).Height; g.DrawString(L.T(storyNote), Draw.fSmall, nb, new RectangleF(x, y, w, nh + 2)); y += nh + 10 * S; }
            // AM-CG-259: what this picker can see, and where the rest is - the card's footnote
            {
                string seen = L.T("Shows apps with open TCP connections to internet addresses. UDP (calls, games), local devices and closed ones are in Connections") + "  ›"; float sh = Draw.Measure(seen, Draw.fSmall, (int)w).Height;
                using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(seen, Draw.fSmall, lb, new RectangleF(x, y, w, sh + 2));
                Act(travelCanvas, new RectangleF(x, y, w, sh), delegate { ShowPage("conn"); });
                y += sh + 16 * S;
            }
            Kit.Frame(g, new RectangleF(ox, cardTop, ow, y - cardTop), 14 * S);
            x = ox; w = ow; y += 20 * S;
            if (storyTarget == "") return Kit.Empty(g, x, y, Math.Min(w, 760 * S), "story", "home", "Follow one connection, box by box", "Pick an app above, or type a site, and I'll walk its traffic from this PC out to the far end - seven boxes, each one clickable.", StoryPoints);

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
                new[] { conv != null ? conv.Proto.Split(' ')[0] : "a socket", conv != null ? "port " + conv.LocalPort + " → " + conv.RemotePort : "port " + (storyPort > 0 ? storyPort : 443), "3" },
                new[] { kind == "Wi-Fi" ? (ch != "" ? "channel " + ch : "radio") : (nic != null && nic.Speed > 0 ? (nic.Speed / 1e6).ToString("0", CultureInfo.CurrentCulture) + " Mb/s" : kind), kind == "Wi-Fi" ? (sig >= 0 ? sig + "% signal" : rx > 0 ? rx + " Mb/s link" : "signal hidden") : "to the switch", kind == "Wi-Fi" && sig >= 0 && sig < 40 ? "1" : "0" },
                new[] { ms(gms), gw != "" ? gw : "router", double.IsNaN(gms) ? "2" : gms > 50 ? "2" : gms > 15 ? "1" : "0" },
                new[] { double.IsNaN(nms) || double.IsNaN(gms) ? "-" : "+" + Math.Max(0, nms - gms).ToString("0", CultureInfo.CurrentCulture) + " ms", vpn ? "VPN" : "router → internet gap", double.IsNaN(nms) ? "2" : nms - gms > 120 ? "1" : "0" },
                new[] { hops > 0 ? hops + " networks" : ms(nms), hops > 0 ? "on the way" : "to the internet", "3" },
                new[] { ShortIp(conv != null && conv.Host != "" ? conv.Host : storyTarget, 22), role != "" ? Draw.Trunc(role, 24) : storyIp != "" ? ShortIp(storyIp, 24) : "", r != null && r.Level >= 0 ? r.Level.ToString(CultureInfo.InvariantCulture) : "3" } };
            cardTop = y; x += 18 * S; w -= 36 * S; y += 16 * S;
            using (var ib = new SolidBrush(Theme.Ink)) using (var nw0 = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(L.T("The way to") + " " + (storyApp ?? Draw.Trunc(storyTarget, 40)), Draw.fHead, ib, new RectangleF(x, y, w, 24 * S), nw0);
            y += 34 * S;
            // narrow window or large text: two rows of boxes (4 + 3) instead of seven squeezed ones
            int per = (w - 6 * 14 * S) / 7 < 96 * S ? 4 : 7;
            float bw = (w - (per - 1) * 14 * S) / per, bh = 88 * S;
            for (int i = 0; i < 7; i++)
            {
                var br = new RectangleF(x + (i % per) * (bw + 14 * S), y + (i / per) * (bh + 14 * S), bw, bh); bool on = storyBox == i; int lv = int.Parse(box[i][2], CultureInfo.InvariantCulture);
                // tiles inside the card (a card in a card is one box too many): soft fill, the picked one blue
                Theme.Fill(g, br, 12 * S, on ? Theme.BlueSoft : Theme.Soft); if (on) Theme.Stroke(g, br, 12 * S, Theme.Blue, 2);
                Draw.LvDot(g, lv, br.X + 12 * S, br.Y + 15 * S, 9 * S, 9 * S);
                using (var mb = new SolidBrush(Theme.Sub)) using (var one = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(L.T(i == 2 && kind != "Wi-Fi" ? "Cable" : BoxName[i]), Draw.fSmall, mb, new RectangleF(br.X + 27 * S, br.Y + 11 * S, Math.Max(10 * S, bw - 35 * S), 20 * S), one);   /* "…" in a narrow tile (review 24 Nov L-6) */
                using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    using (var ib = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(box[i][0], Draw.fBodyB, ib, new RectangleF(br.X + 12 * S, br.Y + 36 * S, bw - 20 * S, 22 * S), nowrap);
                    using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(box[i][1], Draw.fSmall, sb, new RectangleF(br.X + 12 * S, br.Y + 60 * S, bw - 20 * S, 20 * S), nowrap);
                }
                if (i < 6 && (i + 1) % per != 0) using (var ap = new Pen(Theme.Rail, 2 * S)) { ap.CustomEndCap = Cap34; g.DrawLine(ap, br.Right + 2 * S, br.Y + bh / 2, br.Right + 12 * S, br.Y + bh / 2); }
                int ii = i; Act(travelCanvas, br, delegate { storyBox = storyBox == ii ? -1 : ii; if (ii == 3 && routeTable == null) LoadRoutes(); if (ii == 0 && m365Rows == null) LoadApps(); travelCanvas.Invalidate(); });
                // Review-1: the server's full address (an IPv6 one never fits) - Copy on the box once it's picked
                string full = i == 6 ? (storyIp != "" ? storyIp : conv != null && conv.Host != "" ? conv.Host : storyTarget) : "";
                if (on && full != "")
                {
                    string cp = L.T("Copy"); var cz = g.MeasureString(cp, Draw.fTiny); var cr = new RectangleF(br.Right - cz.Width - 14 * S, br.Y + 6 * S, cz.Width + 8 * S, 18 * S);
                    using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(cp, Draw.fTiny, lb, cr.X + 4 * S, cr.Y + 2 * S);
                    Act(travelCanvas, cr, delegate { try { System.Windows.Forms.Clipboard.SetText(full); storyNote = L.T("Copied") + " " + full; } catch (System.Runtime.InteropServices.ExternalException) { } travelCanvas.Invalidate(); });
                }
            }
            y += bh + 12 * S + (per < 7 ? bh + 14 * S : 0);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(storyBox < 0 ? L.T("Click any step to see what happens to your data there.") : L.T("Click the step again to close it."), Draw.fSmall, mb, x, y);
            y += 26 * S;
            if (storyApp != null && storyIp != "")
            {
                // AM-CG-260: the app's own connection and the separate test are two different things - both shown
                string ep = (storyIp.Contains(":") ? "[" + storyIp + "]" : storyIp) + ":" + storyPort;
                string tested = r != null ? r.Ip ?? "" : "";
                string two = L.T("The app's own connection:") + " " + ep + (storyLocal > 0 ? " (" + L.T("from local port") + " " + storyLocal + ")" : "") + ".  " + L.T("The trace and timing test reach") + " " + storyTarget + (tested != "" && tested != storyIp ? " = " + tested + " - " + L.T("a different server than the app's") : " " + L.T("on their own - they can land on a different server"));
                float th = Draw.Measure(two, Draw.fSmall, (int)w).Height; using (var sb2 = new SolidBrush(Theme.Muted)) g.DrawString(two, Draw.fSmall, sb2, new RectangleF(x, y, w, th + 2)); y += th + 8 * S;
            }
            y += 8 * S; Kit.Frame(g, new RectangleF(ox, cardTop, ow, y - cardTop), 14 * S);
            x = ox; w = ow; y += 20 * S;

            // what happens in the chosen box: two lines, then what's behind it
            if (storyBox >= 0)
            {
                string[] two = {
                    "The app asks Windows for a connection - it never touches the network itself." + (storyApp != null ? " " + storyApp + " has " + mine.Count + " open right now." : ""),
                    "Windows wraps the data: TCP or UDP with port numbers, then the IP addresses " + (localIp != "" ? localIp : "of your PC") + " → " + (storyIp != "" ? storyIp : storyTarget) + ".",
                    kind == "Wi-Fi" ? "The packet goes over the radio to your access point" + (ssid != "" ? " (" + ssid + ")" : "") + ". Signal and channel decide how fast and how reliably." : "The packet goes over the cable to the switch and your router.",
                    "Your router picks the way out (the default route) and swaps your private address for its public one (NAT). It answers in " + ms(gms) + ".",
                    vpn ? "The VPN wraps the packet again and sends it to your company first." : "Your provider carries it into the internet" + (isp != "" ? " (" + isp + ")" : "") + ". The internet test answers about " + (double.IsNaN(nms) || double.IsNaN(gms) ? "?" : Math.Max(0, nms - gms).ToString("0", CultureInfo.CurrentCulture)) + " ms later than the router - that gap covers your provider and everything past it, so it isn't the provider's share alone.",
                    "Each network hands it to the next; where two meet, BGP decides the route." + (hops > 0 ? "" : " Press Time each step to see the networks on the way."),
                    "The server unwraps every layer and answers the same way back." + (role != "" ? " For " + (storyApp ?? "this app") + " this server is: " + role + "." : "") };
                float th = Draw.Measure(two[storyBox], Draw.fBody, (int)(w - 28 * S)).Height;
                var tr = new RectangleF(x, y, w, th + 22 * S); Theme.Fill(g, tr, 10 * S, Theme.Soft);
                using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(two[storyBox], Draw.fBody, ib, new RectangleF(x + 14 * S, y + 11 * S, w - 28 * S, th + 4));
                y += tr.Height + 16 * S;
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
                y += 20 * S;
            }

            // the timed handshake - its own card
            cardTop = y; x += 18 * S; w -= 36 * S; y += 16 * S;
            using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(Kit.Sent(L.T("HOW LONG EACH STEP TAKES")), Draw.fHead, ib, x, y); Hint(travelCanvas, new RectangleF(x, y - 2 * S, w, 24 * S), "travel.LADDER");
            y += 34 * S;
            if (r == null || (!r.Done && !travelBusy))
            {
                string lab = (storyApp != null ? L.T("Time each step to") + " " + storyApp + L.T("'s server") : L.T("Time each step to") + " " + Draw.Trunc(storyTarget, 40)) + "  >"; var ls = g.MeasureString(lab, Draw.fBodyB); var lr = new RectangleF(x, y, ls.Width + 28 * S, 34 * S);
                Theme.Fill(g, lr, 17 * S, Theme.Blue); using (var wb = new SolidBrush(Theme.OnBlue)) g.DrawString(lab, Draw.fBodyB, wb, x + 14 * S, y + (34 * S - ls.Height) / 2);
                Act(travelCanvas, lr, delegate { travelBox.Box.Text = storyTarget; FollowPacket(); travelBox.Box.Text = ""; });
                y += 46 * S;
                string why = L.T("This runs the same check as Check a destination: it looks up the name, connects, secures the connection and waits for the first reply. Nothing else is sent."); float wh = Draw.Measure(why, Draw.fSmall, (int)w).Height;
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(why, Draw.fSmall, mb, new RectangleF(x, y, w, wh + 2));
                y += wh + 16 * S; Kit.Frame(g, new RectangleF(ox, cardTop, ow, y - cardTop), 14 * S);
                return y + 20 * S;
            }
            if (travelBusy && !r.Done) { using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(travelStatus, Draw.fBody, mb, x, y); y += 30 * S; }
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
                y += barH + 12 * S;
                float lx = x;   // legend under the bar, so tiny steps stay readable
                foreach (var p in parts)
                {
                    string t = p.Item1 + "  " + ms(p.Item2); var ts = g.MeasureString(t, Draw.fSmall);
                    using (var b = new SolidBrush(p.Item3)) g.FillRectangle(b, lx, y + 4 * S, 10 * S, 10 * S);
                    using (var mb = new SolidBrush(Theme.Sub)) g.DrawString(t, Draw.fSmall, mb, lx + 14 * S, y);
                    lx += ts.Width + 30 * S;
                }
                y += 30 * S;
                var slow = parts.OrderByDescending(p => p.Item2).First();
                using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(ms(total) + " from name to first byte. Most of it: " + slow.Item1 + (slow.Item1.StartsWith("Connect", StringComparison.Ordinal) ? " - that's distance to the server." : slow.Item1.StartsWith("First", StringComparison.Ordinal) ? " - the server thinking." : "."), Draw.fBody, ib, new RectangleF(x, y, w, 40 * S));
                y += 30 * S;
            }
            y += 10 * S; Kit.Frame(g, new RectangleF(ox, cardTop, ow, y - cardTop), 14 * S);
            x = ox; w = ow; y += 20 * S;
            if (r.Done && LiveCapture.Available) { y = PaintReal(g, x, y, w); y += 10 * S; }
            return y;
        }
    }
}
