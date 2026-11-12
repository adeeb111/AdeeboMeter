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
    // Packet journey, under the Network story: any conversation (a Journey button on every Connections row, or the app
    // picked above). Four parts, top to bottom:
    //   the label stack at each stop · where the time goes (latency and jitter, measured) · the real packets played along
    //   the path, sized by bytes, resends and stalls marked, with a running translation · Explain this capture.
    // Recording is on press only, one conversation, headers only, and stops by itself (Quick 100 packets, Normal 1,000 or
    // 30 s). The animation runs only while it's on screen and only repaints its own strip.
    sealed partial class FullView
    {
        Journey.Flow jFlow; Journey.Session jSess; bool jQuick; List<Journey.Note> jNotes; string jKind = "";
        double jPlay; bool jPlaying; int jSlow = 10; DateTime jTick; System.Windows.Forms.Timer jTimer; RectangleF jLane = RectangleF.Empty;
        double[] jPing; bool jPinging; DateTime jCopied = DateTime.MinValue;
        const double JTravel = 1.6;   // seconds on screen for one trip along the path

        // a Connections row's Journey button: open the story on that conversation
        void OpenJourney(Conn c)
        {
            if (jSess != null && !jSess.Done) jSess.Stop();
            bool udp = c.Proto.StartsWith("UDP", StringComparison.Ordinal);
            jFlow = new Journey.Flow { Remote = c.RemoteIp ?? "", RemotePort = c.RemotePort, LocalPort = c.LocalPort, Udp = udp, App = c.Proc ?? "" };
            JReset();
            storyApp = c.Proc; storyIp = c.RemoteIp ?? ""; storyPort = c.RemotePort; storyBox = -1; realPkts = null; realDecode = null; realErr = "";
            storyTarget = storyIp != "" ? (c.Host != "" && c.Host != c.RemoteIp ? c.Host : c.RemoteIp) + (c.RemotePort != 443 ? ":" + c.RemotePort : "") : (c.Proc ?? "app") + " (UDP " + c.LocalPort + ")";
            ShowPage("travel"); jScrollTo = true; travelCanvas.Invalidate();
        }
        bool jScrollTo;
        void JReset() { jSess = null; jNotes = null; jKind = ""; jPlay = 0; jPlaying = false; jPing = null; JTimer(false); }

        // the conversation the story is showing becomes the journey's, unless one was picked from Connections
        Journey.Flow JFlowFromStory()
        {
            if (jFlow != null && (jFlow.Remote == storyIp || (jFlow.Remote == "" && storyIp == "") || (jSess != null && jSess.F == jFlow))) return jFlow;
            if (storyIp == "") return null;
            var conv = connSnap.FirstOrDefault(c => c.RemoteIp == storyIp && c.RemotePort == storyPort && (storyApp == null || c.Proc == storyApp));
            jFlow = new Journey.Flow { Remote = storyIp, RemotePort = storyPort, LocalPort = conv != null ? conv.LocalPort : 0, Udp = conv != null && conv.Proto.StartsWith("UDP", StringComparison.Ordinal), App = storyApp ?? "" };
            JReset(); return jFlow;
        }

        float PaintJourney(Graphics g, float x, float y, float w)
        {
            var f = JFlowFromStory();
            y += 10 * S; float top = y;
            y = Head(g, "PACKET JOURNEY" + (f != null ? "  ·  " + (f.App != "" ? f.App.ToUpperInvariant() + "  ·  " : "") + (f.Remote != "" ? f.Remote + (f.RemotePort > 0 ? ":" + f.RemotePort : "") : "UDP PORT " + f.LocalPort) : ""), x, y, w, "travel.JOURNEY");
            if (f == null) return Lead(g, "Pick a conversation: an app above, or the Journey button on any row in Connections (a game, a call, a download, a printer - anything).", x, y, w, true);
            if (jScrollTo) { jScrollTo = false; int want = (int)top - Sc(20); BeginInvoke((Action)delegate { travelCanvas.AutoScrollPosition = new Point(0, Math.Max(0, want)); }); }
            y = PaintJStack(g, x, y, w, f);
            y = PaintJTime(g, x, y, w, f);
            y = PaintJCapture(g, x, y, w, f);
            return y;
        }

        // ---------- 1. the label stack at each stop
        float PaintJStack(Graphics g, float x, float y, float w, Journey.Flow f)
        {
            string kind; bool vpn; int mtu; lock (M.L) { kind = M.AdapterKind; vpn = M.VpnUp; mtu = M.PrimaryMtu; }
            bool v6 = f.Remote.Contains(":"); string l4 = f.Udp ? "udp" : "tcp", ip = v6 ? "ipv6" : "ipv4";
            string top = f.RemotePort == 443 && !f.Udp ? "tls " : f.RemotePort == 443 && f.Udp ? "quic " : f.RemotePort == 53 ? "dns " : "";
            string link = kind == "Wi-Fi" ? "wifi" : "ethernet";
            var stops = new List<string[]> {
                new[] { "This PC", top + l4 + " " + ip + (vpn ? " wireguard" : "") + " " + link, "Windows wraps the app's data: " + (f.Udp ? "UDP" : "TCP") + " ports, then IP addresses" + (vpn ? ", then the VPN wraps it all again" : "") + ", then the " + (kind == "Wi-Fi" ? "Wi-Fi" : "Ethernet") + " frame." },
                new[] { "Router", top + l4 + " " + ip + (vpn ? " wireguard" : "") + " ethernet", "The router takes off the " + (kind == "Wi-Fi" ? "Wi-Fi" : "Ethernet") + " frame, swaps your private address for its public one (NAT) and puts on a new frame for the provider's line." },
                new[] { "Provider", top + l4 + " " + ip + (vpn ? " wireguard" : "") + " ethernet", "Your provider reads only the outer IP address" + (vpn ? " - the VPN tunnel" : "") + ". Inside their network they may add labels (MPLS) or tags you never see; they come off before the packet leaves." },
                new[] { "Internet", top + l4 + " " + ip + (vpn ? " wireguard" : "") + " ethernet", "Each network hands it on; every router lowers the hop count (TTL) by one and builds a new frame for the next link. The IP packet itself stays the same." },
                new[] { "Server", top + l4 + " " + ip + " ethernet", (vpn ? "The VPN server unwraps the tunnel first. " : "") + "The server takes off each layer in reverse and hands the data to its app, then answers the same way back." } };
            float colW = (w - 4 * 10 * S) / 5, chipH = 20 * S; float maxY = y;
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
                for (int i = 0; i < stops.Count; i++)
                {
                    float cx = x + i * (colW + 10 * S), cy = y; string err; var layers = Calc.Parse(stops[i][1], out err);
                    g.DrawString(stops[i][0].ToUpperInvariant(), Draw.fTiny, muted, cx, cy); cy += 18 * S;
                    foreach (var l in layers)
                    {
                        var cr = new RectangleF(cx, cy, colW, chipH); var col = LayerColor(l.Key);
                        using (var b = new SolidBrush(Color.FromArgb(Theme.Dark ? 90 : 40, col))) g.FillRectangle(b, cr);
                        using (var p = new Pen(col, 1)) g.DrawRectangle(p, cr.X, cr.Y, cr.Width, cr.Height);
                        g.DrawString(l.Name + "  " + (l.Approx ? "~" : "") + l.Bytes, Draw.fSmall, ink, cr, nowrap);
                        cy += chipH + 2 * S;
                    }
                    int over = layers.Where(l => l.Kind != "f").Sum(l => l.Bytes);
                    g.DrawString((over > 0 ? "~" + over + " bytes of headers" : ""), Draw.fSmall, muted, new RectangleF(cx, cy, colW, 18 * S), nowrap); cy += 20 * S;
                    var tr = new RectangleF(cx, y, colW, cy - y); string tip = stops[i][2]; Act(travelCanvas, tr, delegate { jStackTip = jStackTip == tip ? null : tip; travelCanvas.Invalidate(); });
                    maxY = Math.Max(maxY, cy);
                }
            y = maxY + 4 * S;
            if (jStackTip != null) y = Lead(g, jStackTip, x, y, w, false);
            else { using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("Click a stop to see what happens to the labels there. Sizes are the usual ones; Packet stack in the Toolbox works out any mix."), Draw.fSmall, mb, x, y); y += 22 * S; }
            return y + 6 * S;
        }
        string jStackTip;
        static Color LayerColor(string key)
        {
            switch (key)
            {
                case "tls": case "quic": case "dns": case "rtp": return LayerCol[0];
                case "tcp": case "udp": case "icmp": return LayerCol[1];
                case "ipv4": case "ipv6": return LayerCol[3];
                case "wireguard": case "wireguard6": case "ipsec": case "ipsec-natt": case "gre": case "vxlan": case "gtpu": case "sslvpn": return LayerCol[5];
                default: return LayerCol[4];
            }
        }

        // ---------- 2. where the time goes: latency split by leg, and what jitter is
        float PaintJTime(Graphics g, float x, float y, float w, Journey.Flow f)
        {
            y = Head(g, "WHERE THE TIME GOES", x, y, w, "travel.JTIME");
            double gwMin = MinOf(M.GwPing), netMin = MinOf(M.NetPing); string kind; lock (M.L) kind = M.AdapterKind;
            if (jPing == null)
            {
                string lab = jPinging ? "Measuring - 10 pings..." : "Measure to " + (f.Remote != "" ? f.Remote : "the server") + " (10 pings)  >";
                var ls = g.MeasureString(lab, Draw.fBodyB); var lr = new RectangleF(x, y, ls.Width + 28 * S, 32 * S);
                Theme.Fill(g, lr, 16 * S, jPinging || f.Remote == "" ? Theme.Soft : Theme.Blue); using (var wb = new SolidBrush(jPinging || f.Remote == "" ? Theme.Muted : Color.White)) g.DrawString(lab, Draw.fBodyB, wb, x + 14 * S, y + (32 * S - ls.Height) / 2);
                if (!jPinging && f.Remote != "") Act(travelCanvas, lr, JMeasure);
                y += 40 * S;
                return Lead(g, f.Remote == "" ? "Capture first - the far end of a UDP conversation shows up in its packets." : "Latency is distance plus waiting in queues. Jitter is how much the waiting changes from one packet to the next. Ten pings to the same server split it up (one small packet a second).", x, y, w, true);
            }
            var v = jPing.Where(d => !double.IsNaN(d)).ToList();
            if (v.Count == 0) return Lead(g, "The server didn't answer pings - many game and call servers ignore them. Capture the conversation instead: its own packets show the rhythm and the stalls.", x, y, w, true);
            double tMin = v.Min(), tAvg = v.Average(); double jit = 0; for (int i = 1; i < v.Count; i++) jit += Math.Abs(v[i] - v[i - 1]); jit = v.Count > 1 ? jit / (v.Count - 1) : 0;
            double a = double.IsNaN(gwMin) ? 0 : Math.Min(gwMin, tMin), b = double.IsNaN(netMin) ? 0 : Math.Max(0, Math.Min(netMin, tMin) - a), c = Math.Max(0, tMin - a - b), q = Math.Max(0, tAvg - tMin);
            var parts = new[] { Tuple.Create(kind == "Wi-Fi" ? "Your Wi-Fi to the router" : "Your cable to the router", a, LayerCol[4], "The first hop. Wi-Fi adds a few ms and is where most home jitter starts."),
                                Tuple.Create("Your provider", b, LayerCol[3], "From your router into the internet. Evening slowdowns usually live here."),
                                Tuple.Create("Distance to the server", c, LayerCol[1], "Light in fibre covers about 200 km per ms - this part only shrinks with a closer server."),
                                Tuple.Create("Waiting in queues", q, LayerCol[5], "Packets queued behind others somewhere (a busy upload, Wi-Fi airtime, a crowded link). It changes from packet to packet - that change is jitter.") };
            double total = parts.Sum(p => p.Item2); if (total <= 0) total = 1;
            float bx = x, bh = 26 * S;
            foreach (var p in parts) { float pw = (float)(p.Item2 / total) * w; if (pw > 0.5f) using (var br = new SolidBrush(p.Item3)) g.FillRectangle(br, bx, y, Math.Max(1, pw - 2 * S), bh); bx += pw; }
            y += bh + 6 * S;
            using (var sub = new SolidBrush(Theme.Sub)) using (var ink = new SolidBrush(Theme.Ink))
            {
                foreach (var p in parts)
                {
                    using (var br = new SolidBrush(p.Item3)) g.FillRectangle(br, x, y + 4 * S, 10 * S, 10 * S);
                    g.DrawString(p.Item1 + "  " + p.Item2.ToString("0.#", CultureInfo.CurrentCulture) + " ms", Draw.fBodyB, ink, x + 16 * S, y);
                    float hh = Draw.Measure(p.Item4, Draw.fSmall, (int)(w - 300 * S)).Height;
                    g.DrawString(p.Item4, Draw.fSmall, sub, new RectangleF(x + 290 * S, y + 1 * S, w - 300 * S, hh + 2)); y += Math.Max(22 * S, hh + 6 * S);
                }
                string verdict = "Round trip " + tMin.ToString("0", CultureInfo.CurrentCulture) + " ms at best, " + tAvg.ToString("0", CultureInfo.CurrentCulture) + " ms on average; jitter " + jit.ToString("0.#", CultureInfo.CurrentCulture) + " ms. " + (jit > 15 ? "That much jitter is felt in calls and fast games - the queues are the part to fix, not the distance." : q > tMin ? "Most of the time is waiting, not distance." : "Most of it is distance - that's physics, not a fault.");
                y = Lead(g, verdict, x, y + 4 * S, w, false);
            }
            var again = new RectangleF(x, y - 4 * S, 140 * S, 20 * S); using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(L.T("Measure again"), Draw.fSmall, lb, again.X, again.Y); Act(travelCanvas, again, delegate { jPing = null; JMeasure(); });
            return y + 18 * S;
        }
        static double MinOf(Ring r) { var v = r.Values(30).Where(d => !double.IsNaN(d)).ToList(); return v.Count == 0 ? double.NaN : v.Min(); }
        void JMeasure()
        {
            var f = jFlow; if (jPinging || f == null || f.Remote == "") return; jPinging = true; travelCanvas.Invalidate();
            string host = f.Remote;
            Task.Factory.StartNew(() =>
            {
                var res = new double[10];
                for (int i = 0; i < res.Length; i++) { res[i] = Monitor.PingMs(host); if (i < res.Length - 1) System.Threading.Thread.Sleep(1000); }
                try { BeginInvoke((Action)delegate { jPinging = false; if (jFlow == f) jPing = res; travelCanvas.Invalidate(); }); } catch (InvalidOperationException) { jPinging = false; }
            }, TaskCreationOptions.LongRunning);
        }

        // ---------- 3. the real packets: capture, play along the path, translate, explain
        float PaintJCapture(Graphics g, float x, float y, float w, Journey.Flow f)
        {
            y = Head(g, "THE REAL PACKETS", x, y, w, "travel.JREAL");
            bool rec = jSess != null && !jSess.Done, can = LiveCapture.Available && !Policy.NoCapture;
            if (!can && (jSess == null || jSess.Snapshot().Count == 0)) return Lead(g, Policy.NoCapture ? "Capturing packets is turned off by your IT team (AdeeboMeter.policy). The stack and the timing above still work." : "Recording the real packets needs Wireshark (with Npcap). The stack and the timing above work without it.", x, y, w, true);
            // controls: mode, capture / stop
            float cx = x;
            foreach (var m in new[] { new[] { "q", "Quick · 100 packets" }, new[] { "n", "Normal · 1,000 or 30 s" } })
            {
                bool on = (m[0] == "q") == jQuick; var ts = g.MeasureString(L.T(m[1]), Draw.fSmall); var mr = new RectangleF(cx, y + 4 * S, ts.Width + 20 * S, 26 * S);
                Theme.Fill(g, mr, 13 * S, on ? Theme.BlueSoft : Theme.Soft); if (on) Theme.Stroke(g, mr, 13 * S, Theme.Blue, 1);
                using (var tb = new SolidBrush(on ? Theme.Ink : Theme.Sub)) g.DrawString(L.T(m[1]), Draw.fSmall, tb, cx + 10 * S, y + 4 * S + (26 * S - ts.Height) / 2);
                if (!rec) { bool q = m[0] == "q"; Act(travelCanvas, mr, delegate { jQuick = q; travelCanvas.Invalidate(); }); }
                cx = mr.Right + 6 * S;
            }
            string lab = rec ? "Stop" : jSess != null ? "Capture again" : "Capture this conversation";
            var ls = g.MeasureString(L.T(lab), Draw.fBodyB); var lr = new RectangleF(cx + 10 * S, y, ls.Width + 28 * S, 34 * S);
            Theme.Fill(g, lr, 17 * S, !can ? Theme.Soft : rec ? Theme.Red : Theme.Blue); using (var wb = new SolidBrush(can ? Color.White : Theme.Muted)) g.DrawString(L.T(lab), Draw.fBodyB, wb, lr.X + 14 * S, y + (34 * S - ls.Height) / 2);
            if (can) Act(travelCanvas, lr, delegate { if (jSess != null && !jSess.Done) jSess.Stop(); else JCapture(); });
            y += 44 * S;
            var recs = jSess != null ? jSess.Snapshot() : new List<Journey.Rec>();
            if (jSess == null) return Lead(g, "Records only this conversation (the filter runs in the capture driver - nothing else reaches AdeeboMeter), headers only, and stops by itself. Then it plays the packets along the path above, slowed down, sized by their bytes, with resends and stalls marked, and translates each one into plain words. Start the call, the match or the download first.", x, y, w, true);
            string status = rec ? "Recording... " + recs.Count + " packets" + (jSess.Before > 0 ? " (" + jSess.Before + " from just before you clicked)" : "") + " · stops by itself at " + jSess.Cap.ToString("N0", CultureInfo.CurrentCulture) + " packets or " + jSess.Secs + " s"
                          : jSess.Error != "" ? jSess.Error : recs.Count + " packets recorded" + (jSess.Before > 0 ? ", " + jSess.Before + " of them from the rewind buffer" : "") + " · nothing runs now";
            y = Lead(g, status, x, y, w, jSess.Error == "");
            if (recs.Count == 0) return y;
            if (jKind == "" || rec) { jKind = Journey.Kind(jSess.F, recs); }
            Journey.Translate(recs, jKind);
            double end = recs[recs.Count - 1].T;
            if (rec) jPlay = Math.Max(0, end - 0.001);   // while recording, the strip follows the newest packets live
            y = PaintJTimeline(g, x, y, w, recs, end);
            jLane = new RectangleF(x, y, w, 120 * S);
            PaintJLane(g, jLane, recs, rec);
            y += jLane.Height + 8 * S;
            // play controls
            if (!rec)
            {
                float px = x;
                string pl = jPlaying ? "Pause" : jPlay >= end ? "Play again" : "Play";
                var ps = g.MeasureString(L.T(pl), Draw.fBodyB); var pr = new RectangleF(px, y, ps.Width + 24 * S, 28 * S);
                Theme.Fill(g, pr, 14 * S, Theme.Soft); using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(L.T(pl), Draw.fBodyB, ib, px + 12 * S, y + (28 * S - ps.Height) / 2);
                Act(travelCanvas, pr, delegate { if (jPlaying) jPlaying = false; else { if (jPlay >= end) jPlay = Math.Max(0, recs[0].T - 0.05); jPlaying = true; jTick = DateTime.UtcNow; } JTimer(jPlaying); travelCanvas.Invalidate(); });
                px = pr.Right + 12 * S;
                foreach (int sl in new[] { 1, 10, 50 })
                {
                    string t = sl == 1 ? "real speed" : sl + "x slower"; var ts = g.MeasureString(L.T(t), Draw.fSmall); var sr = new RectangleF(px, y + 2 * S, ts.Width + 16 * S, 24 * S); bool on = jSlow == sl;
                    Theme.Fill(g, sr, 12 * S, on ? Theme.BlueSoft : Theme.Card); if (on) Theme.Stroke(g, sr, 12 * S, Theme.Blue, 1);
                    using (var tb = new SolidBrush(on ? Theme.Ink : Theme.Sub)) g.DrawString(L.T(t), Draw.fSmall, tb, px + 8 * S, y + 2 * S + (24 * S - ts.Height) / 2);
                    int s2 = sl; Act(travelCanvas, sr, delegate { jSlow = s2; travelCanvas.Invalidate(); }); px = sr.Right + 6 * S;
                }
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("Block width = packet size · amber = sent again · red = an error from the network · a red mark = a stall"), Draw.fSmall, mb, px + 10 * S, y + 6 * S);
                y += 38 * S;
            }
            y = PaintJTranslate(g, x, y, w, recs, rec);
            if (!rec) y = PaintJExplain(g, x, y, w, recs);
            return y;
        }
        void JCapture()
        {
            var f = jFlow; if (f == null) return; string ifc = Packets.Iface(M); if (ifc == "") return;
            if (f.Udp && f.Remote != "" && f.LocalPort == 0) f.LocalPort = 0;
            var sess = new Journey.Session(f, jQuick);
            try { foreach (var n in NetworkInterface.GetAllNetworkInterfaces()) foreach (var u in n.GetIPProperties().UnicastAddresses) { string a = u.Address.ToString(); int z = a.IndexOf('%'); sess.Mine.Add(z > 0 ? a.Substring(0, z) : a); } } catch (NetworkInformationException) { }
            jSess = sess; jNotes = null; jKind = ""; jPlay = 0; jPlaying = false; JTimer(false);   // while recording, each new batch repaints (twice a second) - no timer
            Task.Factory.StartNew(() => sess.Run(ifc, delegate { try { BeginInvoke((Action)delegate { if (jSess == sess) { if (sess.Done) { JTimer(false); jPlay = 0; } travelCanvas.Invalidate(); } }); } catch (InvalidOperationException) { } }), TaskCreationOptions.LongRunning);
            travelCanvas.Invalidate();
        }
        // only while a replay plays and the journey is on screen
        void JTimer(bool on)
        {
            if (jTimer == null) { jTimer = new System.Windows.Forms.Timer { Interval = 33 }; jTimer.Tick += delegate { JStep(); }; }
            jTimer.Enabled = on; if (on) jTick = DateTime.UtcNow;
        }
        void JStep()
        {
            if (!travelPage.Visible || !Visible || WindowState == FormWindowState.Minimized || !jPlaying || jSess == null) { jPlaying = false; JTimer(false); return; }
            var now = DateTime.UtcNow; double dt = Math.Min(0.2, (now - jTick).TotalSeconds); jTick = now;
            var recs = jSess.Snapshot(); double end = recs.Count > 0 ? recs[recs.Count - 1].T : 0;
            jPlay += dt / jSlow;
            // long silences (an idle connection) are skipped, not watched in slow motion: jump to just before the next packet
            if (!recs.Any(r => r.T <= jPlay && jPlay - r.T <= JTrip())) { var nx = recs.FirstOrDefault(r => r.T > jPlay); if (nx != null && nx.T - jPlay > JTrip()) jPlay = nx.T - JTrip() * 0.1; }
            if (jPlay > end + JTrip()) { jPlay = end; jPlaying = false; JTimer(false); travelCanvas.Invalidate(); return; }
            if (jLane.Width > 0) { var r = jLane; r.Offset(travelCanvas.AutoScrollPosition.X, travelCanvas.AutoScrollPosition.Y); travelCanvas.Invalidate(Rectangle.Ceiling(r)); }
        }
        // capture-seconds one trip along the path takes on screen (real speed: a short streak - a real trip is too quick to see)
        double JTrip() { return jSlow == 1 ? 0.25 : JTravel / jSlow; }
        // the play head moved: repaint only the strip, not the whole page
        bool JLaneOnly(PaintEventArgs e)
        {
            if (jSess == null || jLane.Width <= 0 || !jPlaying) return false;
            var r = jLane; r.Offset(travelCanvas.AutoScrollPosition.X, travelCanvas.AutoScrollPosition.Y);
            if (!Rectangle.Ceiling(r).Contains(e.ClipRectangle)) return false;
            var g = e.Graphics; g.TranslateTransform(travelCanvas.AutoScrollPosition.X, travelCanvas.AutoScrollPosition.Y);
            PaintJLane(g, jLane, jSess.Snapshot(), false); return true;
        }
        // the whole capture as a strip: where the stalls, resends and errors are; click to jump there
        float PaintJTimeline(Graphics g, float x, float y, float w, List<Journey.Rec> recs, double end)
        {
            double t0 = recs[0].T, span = Math.Max(0.001, end - t0); var bar = new RectangleF(x, y, w, 18 * S);
            Theme.Fill(g, bar, 4 * S, Theme.Soft);
            foreach (var r in recs)
            {
                float px = x + (float)((r.T - t0) / span) * w; Color c = r.IcmpType >= 0 ? Theme.Red : r.Resend || r.DupAck ? Theme.Amber : r.GapBefore > 0 ? Theme.Red : Color.Empty;
                if (r.Before) using (var bp = new Pen(Theme.Line2, 1)) g.DrawLine(bp, px, y + 2 * S, px, y + 16 * S);
                if (c != Color.Empty) using (var p = new Pen(c, 2 * S)) g.DrawLine(p, px, y, px, y + 18 * S);
            }
            float pp = x + (float)((Math.Min(Math.Max(jPlay, t0), end) - t0) / span) * w; using (var p = new Pen(Theme.Ink, 2 * S)) g.DrawLine(p, pp, y - 2 * S, pp, y + 20 * S);
            Act(travelCanvas, bar, delegate { var pt = travelCanvas.PointToClient(Cursor.Position); double at = t0 + (pt.X - travelCanvas.AutoScrollPosition.X - x) / w * span; jPlay = Math.Max(t0, Math.Min(end, at)); jPlaying = false; JTimer(false); travelCanvas.Invalidate(); });
            using (var mb = new SolidBrush(Theme.Muted)) { g.DrawString("0 s", Draw.fTiny, mb, x, y + 20 * S); string e = span.ToString("0.0", CultureInfo.CurrentCulture) + " s"; g.DrawString(e, Draw.fTiny, mb, x + w - g.MeasureString(e, Draw.fTiny).Width, y + 20 * S); }
            return y + 38 * S;
        }
        // the path with the packets on it: out goes right, replies come back left; a block's width is its size
        void PaintJLane(Graphics g, RectangleF lane, List<Journey.Rec> recs, bool rec)
        {
            Theme.Fill(g, lane, 10 * S, Theme.Soft);
            string[] stops = { "This PC", "Router", "Provider", "Internet", "Server" };
            // where the stops sit: by measured time when we have it, evenly otherwise
            float[] at = { 0f, 0.15f, 0.35f, 0.6f, 1f };
            if (jPing != null) { var v = jPing.Where(d => !double.IsNaN(d)).ToList(); double gw = MinOf(M.GwPing), net = MinOf(M.NetPing); if (v.Count > 0 && !double.IsNaN(gw) && !double.IsNaN(net) && v.Min() > net) { double t = v.Min(); at = new[] { 0f, (float)Math.Max(0.08, gw / t), (float)Math.Max(0.16, Math.Min(0.8, net / t)), (float)Math.Max(0.2, Math.Min(0.9, (net + (t - net) / 2) / t)), 1f }; for (int i = 1; i < at.Length; i++) at[i] = Math.Max(at[i], at[i - 1] + 0.08f); for (int i = 0; i < at.Length; i++) at[i] = Math.Min(1f, at[i]); } }
            float lx = lane.X + 60 * S, rx = lane.Right - 60 * S, mid = lane.Y + lane.Height / 2;
            using (var rail = new Pen(Theme.Rail, 2 * S)) g.DrawLine(rail, lx, mid, rx, mid);
            using (var mb = new SolidBrush(Theme.Muted)) using (var ce = new StringFormat { Alignment = StringAlignment.Center })
                for (int i = 0; i < stops.Length; i++) { float sx = lx + at[i] * (rx - lx); using (var nb = new SolidBrush(Theme.Card)) g.FillEllipse(nb, sx - 7 * S, mid - 7 * S, 14 * S, 14 * S); using (var np = new Pen(Theme.Sub, 2 * S)) g.DrawEllipse(np, sx - 7 * S, mid - 7 * S, 14 * S, 14 * S); g.DrawString(L.T(stops[i]), Draw.fTiny, mb, new RectangleF(sx - 50 * S, lane.Bottom - 18 * S, 100 * S, 16 * S), ce); }
            // packets in flight at the play head: a packet starts at its own time and takes JTravel (screen seconds) to cross
            double trip = rec ? 0.4 : JTrip();
            var fly = new List<Journey.Rec>();
            for (int i = recs.Count - 1; i >= 0 && fly.Count < 60; i--) { var r = recs[i]; if (r.T > jPlay) continue; if (jPlay - r.T > trip) break; fly.Add(r); }
            foreach (var r in fly)
            {
                double prog = (jPlay - r.T) / trip; if (prog < 0 || prog > 1) continue;
                float bw = Math.Max(4 * S, Math.Min(46 * S, (4 + 42f * Math.Min(1500, Math.Max(40, r.IpLen)) / 1500f) * S)), bh = 16 * S;
                float px; float py = r.Out ? mid - 22 * S : mid + 6 * S;
                if (r.IcmpType >= 0 && r.FromRouter) { float from = lx + at[3] * (rx - lx); px = from - (float)prog * (from - lx); }   // a router's message comes from somewhere on the way
                else px = r.Out ? lx + (float)prog * (rx - lx) : rx - (float)prog * (rx - lx);
                Color c = r.IcmpType >= 0 ? Theme.Red : r.Resend ? Theme.Amber : r.Out ? Theme.Blue : Theme.Green;
                var br = new RectangleF(px - bw / 2, py, bw, bh);
                using (var b = new SolidBrush(c)) g.FillRectangle(b, br);
                if (r.DupAck) using (var p = new Pen(Theme.Amber, 2 * S)) g.DrawRectangle(p, br.X, br.Y, br.Width, br.Height);
                if (r.Resend) using (var wb = new SolidBrush(Color.White)) g.DrawString("again", Draw.fTiny, wb, br.X + 1, br.Y + 1);
            }
            // stalls just before the play head: a red mark where the reply should have been
            var stall = recs.LastOrDefault(r => r.GapBefore > 0 && r.T <= jPlay && jPlay - r.T < 0.6);
            if (stall != null) using (var rb = new SolidBrush(Theme.Red)) g.DrawString(L.T("stall") + " " + Journey.Ms(stall.GapBefore), Draw.fSmall, rb, stall.Out ? lx + 10 * S : rx - 110 * S, lane.Y + 6 * S);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString((rec ? L.T("live") : jSlow == 1 ? L.T("real speed") : jSlow + L.T("x slower")) + "  ·  " + jPlay.ToString("0.00", CultureInfo.CurrentCulture) + " s", Draw.fTiny, mb, lane.X + 10 * S, lane.Y + 6 * S);
        }
        // the running translation: one plain line per packet, newest last
        float PaintJTranslate(Graphics g, float x, float y, float w, List<Journey.Rec> recs, bool rec)
        {
            y = Head(g, "IN PLAIN WORDS" + (rec ? "  ·  LIVE" : ""), x, y, w, "travel.JSAY");
            int upto = recs.FindLastIndex(r => r.T <= jPlay + 1e-6); if (upto < 0) upto = rec ? recs.Count - 1 : Math.Min(recs.Count - 1, 9);
            var shown = recs.Skip(Math.Max(0, upto - 9)).Take(Math.Min(10, upto + 1)).ToList();
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (var muted = new SolidBrush(Theme.Muted))
                foreach (var r in shown)
                {
                    int lv = r.IcmpType >= 0 || r.GapBefore > 0 ? 2 : r.Resend || r.DupAck ? 1 : 3;
                    g.DrawString(r.T.ToString("0.000", CultureInfo.InvariantCulture), Draw.fMonoS, muted, x, y + 2 * S);
                    using (var ap = new Pen(r.Out ? Theme.Blue : Theme.Green, 2 * S)) { ap.CustomEndCap = new AdjustableArrowCap(3, 4); float ay = y + 10 * S; if (r.Out) g.DrawLine(ap, x + 62 * S, ay, x + 78 * S, ay); else g.DrawLine(ap, x + 78 * S, ay, x + 62 * S, ay); }   // drawn, not a font glyph
                    using (var tb = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(r.Say, Draw.fBody, tb, new RectangleF(x + 86 * S, y, w - 86 * S, 20 * S), nowrap);
                    y += 21 * S;
                }
            return y + 8 * S;
        }
        // Explain this capture: our own rules, every finding with the moment it's about (click to jump there)
        float PaintJExplain(Graphics g, float x, float y, float w, List<Journey.Rec> recs)
        {
            if (jNotes == null)
            {
                var ls = g.MeasureString(L.T("Explain this capture"), Draw.fBodyB); var lr = new RectangleF(x, y, ls.Width + 28 * S, 34 * S);
                Theme.Fill(g, lr, 17 * S, Theme.Blue); using (var wb = new SolidBrush(Color.White)) g.DrawString(L.T("Explain this capture"), Draw.fBodyB, wb, x + 14 * S, y + (34 * S - ls.Height) / 2);
                Act(travelCanvas, lr, delegate { string k; lock (M.L) k = M.AdapterKind; var rs = jSess.Snapshot(); jNotes = Journey.Explain(jSess.F, rs, jKind, k == "Wi-Fi"); JKeepCwnd(rs); if (rs.Count > 0) EarnPts("journey.explain", true, true); /* Adeebo Points: the first capture explained */ travelCanvas.Invalidate(); });
                y += 42 * S;
                return Lead(g, "Reads these packets with fixed rules - the same every time, nothing guessed, nothing sent anywhere - and says what they show, with the moment each finding is about, and what they can't show.", x, y, w, true);
            }
            y = Head(g, "WHAT THESE PACKETS SHOW", x, y, w, "travel.JEXPLAIN");
            foreach (var n in jNotes)
            {
                float th = Draw.Measure(n.Text, Draw.fBody, (int)(w - 40 * S)).Height; var card = new RectangleF(x, y, w, th + 40 * S);
                Theme.Fill(g, card, 10 * S, Theme.Soft); Draw.LvDot(g, n.Lv, x + 12 * S, y + 13 * S, 9 * S, 9 * S);
                using (var tb = new SolidBrush(n.Lv == 1 || n.Lv == 2 ? Theme.LvText(n.Lv) : Theme.Ink)) g.DrawString(n.Title, Draw.fBodyB, tb, x + 28 * S, y + 8 * S);
                using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(n.Text, Draw.fBody, sb, new RectangleF(x + 28 * S, y + 30 * S, w - 40 * S, th + 4));
                if (!double.IsNaN(n.At))
                {
                    string jt = "at " + n.At.ToString("0.0", CultureInfo.CurrentCulture) + " s  ›"; var js = g.MeasureString(jt, Draw.fSmall); var jr = new RectangleF(card.Right - js.Width - 14 * S, y + 9 * S, js.Width + 4, js.Height);
                    using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(jt, Draw.fSmall, lb, jr.X, jr.Y);
                    double at = n.At; Act(travelCanvas, jr, delegate { jPlay = Math.Max(0, at - 0.3); jPlaying = true; jTick = DateTime.UtcNow; JTimer(true); travelCanvas.Invalidate(); });
                }
                y += card.Height + 8 * S;
            }
            if (!jSess.F.Udp && (jKind == "bulk" || jKind == "web"))
            {
                var tr = new RectangleF(x, y, 360 * S, 22 * S);
                using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(L.T("See how TCP's window grew - in the TCP lab  ›"), Draw.fBodyB, lb, x, y);
                Act(travelCanvas, tr, delegate { tab = "learn"; ShowTab(); learnSel = "lab:tcp"; learnList.Invalidate(); learnInfo.Invalidate(); });
                y += 30 * S;
            }
            string cp = (DateTime.Now - jCopied).TotalSeconds < 3 ? L.T("Copied - internal addresses removed") : L.T("Copy the explanation");
            var cr = new RectangleF(x, y, g.MeasureString(cp, Draw.fBodyB).Width + 4, 22 * S);
            using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(cp, Draw.fBodyB, lb, x, y);
            Act(travelCanvas, cr, delegate
            {
                try
                {
                    string ssid; lock (M.L) ssid = M.Ssid;
                    string text = "Packet journey - " + Journey.KindName(jKind) + Environment.NewLine + string.Join(Environment.NewLine, jNotes.Select(n => "- " + n.Title + ": " + n.Text + (double.IsNaN(n.At) ? "" : " (at " + n.At.ToString("0.0", CultureInfo.InvariantCulture) + " s)"))) + Environment.NewLine + "Read from packet headers by AdeeboMeter's rules; nothing was sent anywhere.";
                    Clipboard.SetText(GameSession.Redact(text, ssid)); jCopied = DateTime.Now;
                }
                catch (Exception ce) { CopyFailed(ce); }
                travelCanvas.Invalidate();
            });
            return y + 32 * S;
        }
            // a captured TCP conversation: its window, estimated per round trip, goes to the TCP lab next to the simulation
        static void JKeepCwnd(List<Journey.Rec> rs)
        {
            var syn = rs.FirstOrDefault(r => r.Out && r.Proto == 6 && (r.Flags & 0x12) == 0x02); var sa = rs.FirstOrDefault(r => !r.Out && r.Proto == 6 && (r.Flags & 0x12) == 0x12);
            LabSim.LastRealCwnd = null;   // a capture without a handshake never leaves the previous capture's line on the graph
            if (syn == null || sa == null || sa.T <= syn.T) return;
            var c = LabSim.CwndFromCapture(rs.Where(r => r.LPort == syn.LPort).ToList(), sa.T - syn.T); if (c.Count < 3) return;
            LabSim.LastRealCwnd = c; LabSim.LastRealRttMs = (sa.T - syn.T) * 1000;
        }
    }
}
