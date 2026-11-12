// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AdeeboMeter
{
    // What's wrong? - for when you can't name the symptom. Layer by layer from this PC outwards, stopping at the first
    // broken one: this PC -> the link -> your router -> the internet -> names -> the way in. It reuses what Monitor already
    // measures and only re-tests what is stale or unknown (a few pings, one TCP connect, one DNS question, Windows' own
    // connectivity test). It never invents a cause and never fixes anything.
    static partial class Investigate
    {
        static double PingNow(IPAddress a, int n)
        {
            if (a == null) return double.NaN; var t = new System.Collections.Generic.List<long>();
            using (var p = new Ping()) for (int i = 0; i < n; i++) { try { var r = p.Send(a, 1000); if (r.Status == IPStatus.Success) t.Add(r.RoundtripTime); } catch (PingException) { } catch (InvalidOperationException) { } }
            return t.Count == 0 ? double.NaN : t.Average();
        }
        // TCP reachability: an answer of any kind (accepted or refused) proves the host is there - for when ping is blocked
        // Windows' own connectivity test page: a sign-in page or proxy answers it with something else.
        // portal = what came back instead ("" when the page is right); err = why it couldn't run ("" when it ran)
        static void PortalTest(out string portal, out string portalErr)
        {
            portal = ""; portalErr = "";
            if (!Cfg.Portal) { portalErr = "off"; return; }   // Settings › Sign-in page check is off: nothing is sent
            try
            {
                var q = (HttpWebRequest)WebRequest.Create("http://www.msftconnecttest.com/connecttest.txt"); q.Timeout = 5000; q.AllowAutoRedirect = false;
                using (var resp = (HttpWebResponse)q.GetResponse()) using (var rd = new System.IO.StreamReader(resp.GetResponseStream())) { var body = rd.ReadToEnd(); if ((int)resp.StatusCode != 200 || !body.StartsWith("Microsoft Connect Test", StringComparison.Ordinal)) portal = (int)resp.StatusCode + " instead of Windows' test page"; }
            }
            catch (WebException e) { var resp = e.Response as HttpWebResponse; portal = resp != null ? ((int)resp.StatusCode >= 300 && (int)resp.StatusCode < 400 ? "redirected to " + Draw.Trunc(resp.Headers["Location"] ?? "", 60) : "error " + (int)resp.StatusCode) : e.Status == WebExceptionStatus.Timeout ? "no answer (a proxy may be required)" : ""; if (resp == null && e.Status != WebExceptionStatus.Timeout) portalErr = e.Status.ToString(); if (resp != null) resp.Dispose(); }
        }
        public static double TcpReach(IPAddress a, int port, int ms)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var c = new TcpClient(a.AddressFamily))
            {
                try { if (c.ConnectAsync(a, port).Wait(ms)) return sw.ElapsedMilliseconds; return double.NaN; }
                catch (AggregateException e) { var se = e.InnerException as SocketException; return se != null && se.SocketErrorCode == SocketError.ConnectionRefused ? sw.ElapsedMilliseconds : double.NaN; }
            }
        }
        static void Whats(Monitor m, GuideRun r, Action changed)
        {
            var s = m.Latest; double cpu = m.Cpu.Avg(10), mem = s.Mem, gw = m.GwPing.Avg(6), net = m.NetPing.Avg(6), loss = s.LossPct;
            bool wifi, vpn; int sig; string adapter, gws, dnsSrv, target; NetworkInterface nic;
            lock (m.L) { wifi = m.AdapterKind == "Wi-Fi"; sig = m.WifiSignal; adapter = m.Adapter; gws = m.Gateway; dnsSrv = m.DnsServer; vpn = m.VpnUp; target = m.NetTarget; nic = m.Primary; }
            string normal = Baseline.Normal(Brain.NetKey);
            Action<string> step = t => { r.Status = t; changed(); };

            // the timeline around now: labelled as things that happened, never as causes
            Action around = () =>
            {
                var evs = m.Events.Recent(40).Where(e => e.T > DateTime.Now.AddMinutes(-30) && e.Lv >= 1 && (e.Text ?? "").IndexOf("AdeeboMeter", StringComparison.Ordinal) < 0).OrderByDescending(e => e.T).Take(4).ToList();
                foreach (var e in evs) r.Obs("m", 3, "Around then, " + e.T.ToString("HH:mm", CultureInfo.CurrentCulture), e.Text + " (happened then - not necessarily the cause)");
            };

            // 1. this PC
            step("Checking this PC...");
            r.Obs("w", cpu > 95 ? 2 : cpu > 80 ? 1 : 0, "1. This PC: processor", F(cpu, "% busy"));
            if (!double.IsNaN(mem)) r.Obs("w", mem > 95 ? 2 : mem > 88 ? 1 : 0, "1. This PC: memory", F(mem, "% in use"));
            if (cpu > 95 || mem > 95)
            {
                r.Conclude(1, "This PC itself is overloaded (" + (cpu > 95 ? "processor " + F(cpu, "%") : "memory " + F(mem, "%")) + ") - that makes everything slow, including the network, before the network is even involved.");
                r.Next.Add("Run \"My PC is slow\" for what's using it."); r.GoTab = "guide:pc"; r.GoLabel = "Run My PC is slow"; around(); return;
            }
            // 2. the link
            step("Checking the link...");
            if ((nic == null || string.IsNullOrEmpty(adapter)) && !NetworkInterface.GetIsNetworkAvailable())
            {
                r.Obs("w", 2, "2. The link", "no connected network adapter");
                r.Conclude(2, "This PC isn't connected to any network - " + (wifi ? "Wi-Fi is disconnected." : "no Wi-Fi joined and no cable with a link.") + " Nothing beyond this point can work.");
                r.Next.Add("Join a Wi-Fi network or check the cable - the light on the port should be on."); r.GoTab = "link"; r.GoLabel = "Open Your Wi-Fi"; around(); return;
            }
            r.Obs("w", wifi && sig >= 0 && sig < 30 ? 1 : 0, "2. The link", (string.IsNullOrEmpty(adapter) ? "connected" : adapter) + (wifi ? (sig >= 0 ? " · signal " + sig + "%" : " · signal hidden by Windows") : ""));
            // 3. the router (re-test when stale or never answered)
            step("Asking your router...");
            IPAddress g; if (!IPAddress.TryParse((gws ?? "").Trim(), out g)) g = null;
            if (double.IsNaN(gw) && g != null) gw = PingNow(g, 3);
            double gwTcp = double.NaN;
            if (double.IsNaN(gw) && g != null) foreach (var p in new[] { 53, 80, 443 }) { gwTcp = TcpReach(g, p, 800); if (!double.IsNaN(gwTcp)) break; }
            if (g == null) r.Obs("w", vpn ? 3 : 2, "3. Your router", vpn ? "no local router (the VPN carries everything)" : "no default gateway - Windows has no way out");
            bool gwSilent = g != null && double.IsNaN(gw) && double.IsNaN(gwTcp);   // judged after the internet test: many routers just ignore ping and TCP
            if (g != null) r.Obs("m", gwSilent ? 1 : gw > 60 ? 1 : 0, "3. Your router (" + g + ")", !double.IsNaN(gw) ? F(gw, " ms") : !double.IsNaN(gwTcp) ? "doesn't answer ping, but answers on TCP in " + F(gwTcp, " ms") + " - it's there" : "no answer to ping or TCP");
            if (g == null && !vpn) { r.Conclude(2, "Windows has no default gateway on " + (string.IsNullOrEmpty(adapter) ? "this connection" : adapter) + " - it doesn't know where to send anything that isn't local. Usually DHCP didn't finish."); r.Next.Add("Disconnect and reconnect; if it stays, DHCP lease and IPv6 lifetimes shows what the network gave you."); r.GoTab = "toolbox"; r.GoLabel = "Open the Toolbox"; around(); return; }
            // 4. the internet (ping, or TCP 443 when ping is blocked)
            step("Testing the internet...");
            IPAddress t1; if (!IPAddress.TryParse((target ?? "").Trim(), out t1)) t1 = IPAddress.Parse("1.1.1.1");
            bool retested = false; if (double.IsNaN(net)) { net = PingNow(t1, 3); retested = true; }
            double netTcp = double.NaN; if (double.IsNaN(net)) { netTcp = TcpReach(t1, 443, 2000); if (double.IsNaN(netTcp)) netTcp = TcpReach(IPAddress.Parse("8.8.8.8"), 443, 2000); }
            r.Obs("m", double.IsNaN(net) && double.IsNaN(netTcp) ? 2 : net > 200 || (!double.IsNaN(loss) && loss > 5) ? 1 : 0, "4. The internet",
                !double.IsNaN(net) ? F(net, " ms") + (!double.IsNaN(loss) && !retested ? " · " + loss.ToString("0.#", CultureInfo.CurrentCulture) + "% lost" : "") + (Cfg.NetSec == 0 ? " (your internet check is off - tested once now)" : "") : !double.IsNaN(netTcp) ? "ping is blocked here, but a TCP connection works (" + F(netTcp, " ms") + ")" : "no answer to ping or TCP 443");
            if (normal != "") r.Obs("i", 3, "Normal for this network", normal);
            if (double.IsNaN(net) && double.IsNaN(netTcp))
            {
                // before blaming the router or the provider: a hotel / airport sign-in page blocks everything until you sign in
                step("Checking for a sign-in page..."); string sp, spErr; PortalTest(out sp, out spErr);
                if (sp != "" && sp != "no answer (a proxy may be required)")
                {
                    r.Obs("m", 1, "The way in", "Windows' connectivity test got " + sp);
                    r.Conclude(1, "Your router works, but the internet is blocked until you sign in (" + sp + ") - a hotel / airport / guest network sign-in page.");
                    r.Next.Add("Open any http:// site in the browser to get the sign-in page."); r.GoTab = "guide:vpn"; r.GoLabel = "Run Works at home, not at work"; around(); return;
                }
            }
            if (gwSilent && double.IsNaN(net) && double.IsNaN(netTcp))
            {
                // the router said nothing AND nothing beyond it answers: now it's fair to point at the link or the router
                r.Conclude(2, "Your router (" + g + ") doesn't answer at all, and nothing beyond it does either - the problem is right next to you: the " + (wifi ? "Wi-Fi link" : "cable") + " or the router itself.");
                r.Next.Add(wifi ? "Move closer, or turn Wi-Fi off and on." : "Check the cable and the router's lights."); r.Next.Add("Restart the router if nothing else on this network works either.");
                r.GoTab = "guide:drops"; r.GoLabel = "Run I keep disconnecting"; around(); return;
            }
            if (gwSilent) r.Obs("i", 3, "Your router is quiet", "it ignores ping and TCP from this PC, but traffic passes through it - that's a setting on the router, not a fault");
            if (double.IsNaN(net) && double.IsNaN(netTcp))
            {
                r.Conclude(2, "Your router answers, but nothing beyond it does - the break is between your router and the internet: your provider, the modem, or " + (vpn ? "the VPN." : "a company firewall."));
                r.Next.Add("Check the modem's lights; restart it if other devices are offline too."); r.Next.Add("If only this PC is affected, try another network to rule out a block on this one.");
                r.GoTab = "guide:slow"; r.GoLabel = "Run The internet is slow"; around(); return;
            }
            // 5. names
            step("Asking your DNS server...");
            IPAddress ds; double dnsNow = double.NaN; string dnsErr = "";
            if (IPAddress.TryParse((dnsSrv ?? "").Split(',')[0].Trim(), out ds))
            {
                var sw = System.Diagnostics.Stopwatch.StartNew(); var q = Toolkit.Query(ds, "www.microsoft.com", 1, true);
                if (q != null && q.Rcode == 0 && q.Rrs.Any(x => x.Section == 1)) dnsNow = sw.ElapsedMilliseconds; else dnsErr = q == null ? "no answer from " + ds : "answered with an error";
            }
            else dnsErr = "no DNS server set";
            r.Obs("m", double.IsNaN(dnsNow) ? 2 : dnsNow > 500 ? 1 : 0, "5. Names (DNS)", !double.IsNaN(dnsNow) ? F(dnsNow, " ms") + " from " + ds : dnsErr);
            if (double.IsNaN(dnsNow))
            {
                r.Conclude(2, "The internet answers, but names don't turn into addresses (" + dnsErr + ") - so every site and app fails, even though the connection is fine.");
                r.Next.Add("Disconnect and reconnect to get DNS again; or set a public DNS (1.1.1.1) in Settings › Network."); r.GoTab = "guide:site"; r.GoLabel = "Run A website won't open"; around(); return;
            }
            // 6. the way in: Windows' own connectivity test (a sign-in page answers it with something else)
            step("Checking for a sign-in page...");
            string portal, portalErr; PortalTest(out portal, out portalErr);
            if (portalErr == "off") { r.NotEst.Add("Whether a sign-in page stands in the way - the sign-in page check is off in Settings."); portalErr = ""; }
            else r.Obs("m", portal != "" ? 1 : portalErr != "" ? 3 : 0, "6. The way in", portal != "" ? "Windows' connectivity test got " + portal : portalErr != "" ? "Windows' connectivity test couldn't run (" + portalErr + ") - web traffic may need a proxy here" : "Windows' connectivity test passes" + (vpn ? " · VPN on" : ""));
            if (portalErr != "") r.NotEst.Add("Whether a sign-in page or proxy stands in the way - the web test couldn't connect (" + portalErr + ").");
            if (portal != "")
            {
                r.Conclude(1, "Everything up to the internet works, but web traffic is intercepted (" + portal + ") - a hotel / airport sign-in page, or a proxy that needs you to sign in.");
                r.Next.Add("Open any http:// site in the browser to get the sign-in page."); r.GoTab = "guide:vpn"; r.GoLabel = "Run Works at home, not at work"; around(); return;
            }
            // nothing broken - but something slow or weak is still worth saying, never "all clear"
            var weak = r.ObservedCopy().Where(o => o[1] == "1" && o[2].Length > 2 && char.IsDigit(o[2][0])).ToList();
            if (weak.Count > 0)
            {
                var w = weak[0]; r.Conclude(1, "Nothing is broken, but " + w[2].Substring(3).ToLowerInvariant() + " is weak: " + w[3] + ". That's where to look if things feel slow or unreliable.");
                r.Next.Add("Run The internet is slow - it digs into exactly this."); r.GoTab = "guide:slow"; r.GoLabel = "Run The internet is slow";
                r.NotEst.Add("Problems that come and go - this is a check of right now."); around(); return;
            }
            // all fine: say what was checked, hand to the guided checks for a named symptom
            r.Conclude(0, "Nothing is wrong right now - your PC, the " + (wifi ? "Wi-Fi" : "link") + ", your router, the internet, names and the way in all work. If something still fails, it's one site or app, or it comes and goes.");
            r.Next.Add("If one site or app fails: run A website or app won't open with its address.");
            r.Next.Add("If it comes and goes: leave AdeeboMeter running - Time machine shows what happened when it breaks.");
            r.GoTab = "guide:site"; r.GoLabel = "Run A website won't open";
            r.NotEst.Add("Problems that come and go - this is a check of right now.");
            r.NotEst.Add("Whether a particular site or app's own servers are working.");
            around();
        }
    }
}
