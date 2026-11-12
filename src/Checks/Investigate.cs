// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // guided investigations. Start from the problem, not the protocol. Each one chains checks AdeeboMeter already has and
    // ends the same way: Finding · What I observed (each with where it came from) · Not established · Next test.
    // Nothing new is sent: they reuse the router / internet pings already running, plus the destination check or pre-call
    // check you could press yourself.
    sealed class GuideRun
    {
        public string Id = "", Title = "", Input = ""; public volatile string Status = ""; public volatile bool Done;
        public int Lv = -1; public string Finding = "";
        public readonly List<string[]> Observed = new List<string[]>();   // { evidence, level, what, value, HH:mm:ss }
        public readonly List<string> NotEst = new List<string>(), Next = new List<string>();
        public string GoTab = "", GoLabel = "";                          // one button to the page that helps next
        public DateTime Started = DateTime.Now;
        public void Obs(string ev, int lv, string what, string val)
        {
            lock (Observed) Observed.Add(new[] { ev, lv.ToString(CultureInfo.InvariantCulture), what, val ?? "", DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) });
        }
        public List<string[]> ObservedCopy() { lock (Observed) return Observed.ToList(); }
        public void Conclude(int lv, string finding) { Lv = lv; Finding = finding; }
    }

    static partial class Investigate
    {
        public sealed class Flow { public string Id, Title, Ask; }
        public static readonly Flow[] All = {
            new Flow { Id = "whats", Title = "What's wrong? (I can't name it)", Ask = "Goes layer by layer - this PC, the link, your router, the internet, names, the way in - and stops at the first one that's broken. Only what's stale is re-tested: a few pings (1.1.1.1 if needed), one DNS question and Windows' own connectivity test page - only when you press it." },
            new Flow { Id = "slow", Title = "The internet is slow", Ask = "Finds where the delay starts: your PC, your Wi-Fi, your router, your provider or DNS." },
            new Flow { Id = "site", Title = "A website or app won't open", Ask = "Walks the name, the route, the door (TCP), the lock (TLS) and the answer (HTTP) to find the first step that fails." },
            new Flow { Id = "calls", Title = "Calls freeze or sound robotic", Ask = "Checks jitter, loss, your upload, your router under load, Wi-Fi and the VPN - what calls care about." },
            new Flow { Id = "drops", Title = "I keep disconnecting", Ask = "Reads today's drops, roams and changes and what the signal was doing just before." },
            new Flow { Id = "vpn", Title = "Works at home, not on VPN or at work", Ask = "Overlapping networks, proxy, the VPN's name rules, the MTU on the way, and the port itself." },
            new Flow { Id = "pc", Title = "My PC is slow", Ask = "Processor, memory pressure, disk, heat, drivers, frozen apps and background jobs - in that order." } };

        static string F(double v, string unit) { return double.IsNaN(v) ? "no answer" : v.ToString("0", CultureInfo.CurrentCulture) + unit; }

        public static void Run(string id, Monitor m, GuideRun r, Action changed)
        {
            r.Id = id; var flow = All.FirstOrDefault(f => f.Id == id); r.Title = flow != null ? flow.Title : id;
            try
            {
                if (id == "whats") Whats(m, r, changed);
                else if (id == "slow") Slow(m, r);
                else if (id == "site") Site(m, r, changed);
                else if (id == "calls") Calls(m, r, changed);
                else if (id == "drops") Drops(m, r);
                else if (id == "vpn") Vpn(m, r, changed);
                else if (id == "pc") Pc(m, r, changed);
            }
            catch (Exception e) { Program.Log("investigate " + id + ": " + e); if (r.Finding == "") r.Conclude(3, "Couldn't finish: " + e.Message); }
            r.Status = ""; r.Done = true; changed();
        }

        // ---------- the internet is slow
        static void Slow(Monitor m, GuideRun r)
        {
            var s = m.Latest;
            double cpu = m.Cpu.Avg(10), mem = s.Mem, gw = m.GwPing.Avg(12), gwMax = m.GwPing.Max(12), net = m.NetPing.Avg(10), jit = m.NetPing.Jitter(10), dns = m.Dns.Avg(6);
            double loss = s.LossPct, up = m.Up.Avg(10), down = m.Down.Avg(10);
            bool wifi; int sig, rate; string kind, vpnName; bool vpn;
            lock (m.L) { wifi = m.AdapterKind == "Wi-Fi"; sig = m.WifiSignal; rate = m.RxRate; kind = m.AdapterKind; vpn = m.VpnUp; vpnName = m.VpnName; }
            int nb; double bloat = Bloat.Extra(out nb);
            string normal = Baseline.Normal(Brain.NetKey);

            r.Obs("w", cpu > 90 ? 2 : cpu > 75 ? 1 : 0, "Your PC: processor", F(cpu, "% busy (10 s average)"));
            if (!double.IsNaN(mem)) r.Obs("w", mem > 90 ? 2 : mem > 80 ? 1 : 0, "Your PC: memory", F(mem, "% in use"));
            if (wifi) r.Obs(sig < 0 ? "u" : "w", sig >= 0 && sig < 40 ? 1 : sig < 0 ? 3 : 0, "Wi-Fi signal and link rate", sig < 0 ? "hidden by Windows (location is off)" + (rate > 0 ? " · link " + rate + " Mb/s" : "") : sig + "%" + (rate > 0 ? " · link " + rate + " Mb/s" : ""));
            else r.Obs("w", 0, "Connection", kind);
            r.Obs("m", double.IsNaN(gw) ? 2 : gw > 50 ? 2 : gw > 15 ? 1 : 0, "Your router answers in", double.IsNaN(gw) ? "no answer" : F(gw, " ms") + " (worst " + F(gwMax, " ms") + ")");
            r.Obs("m", double.IsNaN(net) ? 2 : net > 150 ? 2 : net > 80 ? 1 : 0, "The internet answers in", F(net, " ms") + (double.IsNaN(jit) ? "" : " · jitter " + F(jit, " ms")));
            if (!double.IsNaN(loss)) r.Obs("m", loss > 5 ? 2 : loss > 1 ? 1 : 0, "Lost replies", loss.ToString("0.#", CultureInfo.CurrentCulture) + "%");
            r.Obs(double.IsNaN(dns) ? "u" : "m", double.IsNaN(dns) ? 3 : dns > 300 ? 2 : dns > 120 ? 1 : 0, "Name lookups (DNS)", double.IsNaN(dns) ? "not measured yet" : F(dns, " ms"));
            r.Obs("w", up > 3 ? 1 : 0, "Your PC is using", (double.IsNaN(down) ? "?" : down.ToString("0.0", CultureInfo.CurrentCulture)) + " Mb/s down · " + (double.IsNaN(up) ? "?" : up.ToString("0.0", CultureInfo.CurrentCulture)) + " Mb/s up");
            if (!double.IsNaN(bloat)) r.Obs("i", bloat > 80 ? 2 : bloat > 20 ? 1 : 0, "Your router under load adds", F(bloat, " ms"));
            if (vpn) r.Obs("w", 0, "VPN", "connected (" + Draw.Trunc(vpnName, 30) + ") - company traffic takes the long way");
            if (normal != "") r.Obs("i", 3, "Normal for this network", normal);

            // the first place it goes wrong, from your PC outwards
            if (cpu > 90) { r.Conclude(1, "Your PC itself is the bottleneck right now - the processor is " + F(cpu, "%") + " busy, so everything feels slow, including the network."); r.Next.Add("Open What's running and sort by CPU - see what's busy before you close anything."); r.GoTab = "procs"; r.GoLabel = "Open What's running"; }
            else if (double.IsNaN(gw) || gw > 50 || (wifi && sig >= 0 && sig < 35))
            {
                r.Conclude(2, double.IsNaN(gw) ? "Your router isn't answering at all" + (wifi ? " over Wi-Fi" : "") + " - the problem is right next to you: the " + (wifi ? "Wi-Fi link" : "cable or the router") + ", before anything else is involved." : wifi ? "The slowness starts between your PC and your router - on the Wi-Fi. Everything after it has to wait for this first hop." : "Your router is slow to answer (" + F(gw, " ms") + "), even over a cable. Restart the router, or check what else on the network is busy.");
                if (wifi) { r.Next.Add("Move closer to the router, or switch to 5 GHz if you're on 2.4 GHz."); r.Next.Add("Open Your Wi-Fi for the advisor (quieter channel, power saving) and the walk test."); r.GoTab = "link"; r.GoLabel = "Open Your Wi-Fi"; }
            }
            else if (!double.IsNaN(bloat) && bloat > 80 && up > 2) { r.Conclude(1, "Your own uploads are filling the line - the router adds " + F(bloat, " ms") + " while your PC sends. Pause OneDrive or the big upload and it clears."); r.Next.Add("Check what's uploading in What's running (bytes per app in Live capture)."); r.GoTab = "procs"; r.GoLabel = "Open What's running"; }
            else if (double.IsNaN(net) || net > 150 || (!double.IsNaN(loss) && loss > 3)) { r.Conclude(2, "Your PC and router are fine (" + F(gw, " ms") + "); the delay starts beyond the router - your provider or the internet."); r.Next.Add("Run Check a destination on the site that's slow to see which hop adds the time."); r.Next.Add("If it lasts, restart the modem; then call your provider with the times shown here."); r.GoTab = "dest"; r.GoLabel = "Check a destination"; }
            else if (!double.IsNaN(dns) && dns > 300) { r.Conclude(1, "Pages start slowly because name lookups take " + F(dns, " ms") + ". Once a page starts, the network itself is fine."); r.Next.Add("Run the DNS speed test (Health and tools) to see if another DNS server answers faster."); r.GoTab = "health"; r.GoLabel = "Open Health and tools"; }
            else if (r.ObservedCopy().Any(o => o[1] == "1"))
            {
                var amber = r.ObservedCopy().Where(o => o[1] == "1").Select(o => o[2] + " " + o[3]).Take(3).ToList();
                r.Conclude(1, "Nothing is badly slow, but not everything is clean: " + string.Join("; ", amber) + ". That's enough to notice on calls or games; on browsing it usually isn't.");
                r.Next.Add("Run it again when it feels slow - if the same thing is worse then, that's the cause.");
            }
            else { r.Conclude(0, "Nothing on the path is slow right now: router " + F(gw, " ms") + ", internet " + F(net, " ms") + ". If one site or app feels slow, it's most likely that service, not your network."); r.Next.Add("Run A website or app won't open with that site's name - it times each step."); r.GoTab = "dest"; r.GoLabel = "Check a destination"; }

            r.NotEst.Add("Your provider's speed plan - this check sends almost nothing; the Speed test in the Toolbox measures it when you ask.");
            r.NotEst.Add("What the slow site's own servers are doing.");
            if (wifi && sig < 0) r.NotEst.Add("Wi-Fi signal and channel crowding - Windows hides them without location permission.");
            if (double.IsNaN(bloat)) r.NotEst.Add("Latency under load - not enough busy and quiet minutes yet.");
        }

        // ---------- a website or app won't open
        static void Site(Monitor m, GuideRun r, Action changed)
        {
            string input = (r.Input ?? "").Trim(); if (input == "") input = "github.com";
            r.Input = input; r.Status = "Checking " + input + " step by step..."; changed();
            var d = new DestResult { Input = input };
            DestCheck.Run(input, d, delegate { changed(); }, m);
            string[] what = { "1. Name (DNS)", "2. Route", "3. Ping", "4. Door (TCP)", "5. Lock (TLS)", "6. Answer (HTTP)" };
            string[] ev = { "m", "w", "m", "m", "m", "m" };
            int first = -1;
            for (int i = 0; i < 6; i++)
            {
                int lv = d.Lv[i]; string v = d.Val[i] ?? "";
                if (lv < 0) { r.Obs("u", 3, what[i], "not reached"); continue; }
                r.Obs(ev[i], lv, what[i], v == "" ? (lv == 0 ? "ok" : "-") : v);
                if (lv == 2 && first < 0 && i != 2) first = i;   // ping is often blocked on purpose - not a failure by itself
            }
            if (d.Inspected != "") r.Obs("i", 1, "Certificate re-signed by", d.Inspected);
            string hostOnly = d.Target != "" ? d.Target : input;
            var pin = WhyFails.Hosts(hostOnly).FirstOrDefault(x => x[1] == "Pins your site"); if (pin != null) r.Obs("w", 2, "Hosts file", pin[2]);
            var px = WhyFails.Proxy(hostOnly).FirstOrDefault(); if (px != null) r.Obs("w", 3, "Proxy", px[2]);
            var clk = WhyFails.Clock().FirstOrDefault(); if (clk != null && clk[0] != "0") r.Obs(clk[4], int.Parse(clk[0], CultureInfo.InvariantCulture), "Your clock", clk[2]);
            string t = d.Target != "" ? d.Target : input;
            if (pin != null && (first < 0 || first >= 3)) { r.Conclude(2, "Your hosts file pins " + hostOnly + " to a fixed address (" + pin[2] + ") - this PC ignores DNS for it. If that address is old, the site breaks only here."); r.Next.Add("Remove the line from the hosts file (needs admin, or ask IT)."); r.GoTab = "toolbox"; r.GoLabel = "Open the Toolbox"; first = 99; }
            if (clk != null && clk[0] == "2" && first == 4) { r.Conclude(2, "The lock fails because your clock is wrong (" + clk[2] + ") - certificates look not-yet-valid or expired."); r.Next.Add("Settings > Time > Sync now."); first = 99; }
            switch (first)
            {
                case 99: break;
                case 0: r.Conclude(2, "The name " + t + " doesn't turn into an address - DNS fails. Check the spelling; if it's right, your DNS server or a company filter may be blocking it."); r.Next.Add("Run the DNS speed test to compare your DNS with public ones."); r.GoTab = "health"; r.GoLabel = "Open Health and tools"; break;
                case 1: r.Conclude(2, "Windows has no route to " + t + " - often a VPN that's half connected, or no internet at all."); r.Next.Add("Open Network story > Routing table to see which route Windows picks."); r.GoTab = "travel"; r.GoLabel = "Open Network story"; break;
                case 3: r.Conclude(2, "The address answers but the door is shut: the TCP connection to port " + (d.Port > 0 ? d.Port.ToString(CultureInfo.CurrentCulture) : "443") + " fails. A firewall (company or the site's) blocks it, or the service is down."); r.Next.Add("Try it from your phone on mobile data: if it works there, it's this network's firewall."); r.GoTab = "dest"; r.GoLabel = "See the full check"; break;
                case 4: r.Conclude(2, "The connection opens but the secure lock fails (TLS)" + (d.Inspected != "" ? " - and the certificate was re-signed by " + d.Inspected + ", a company proxy or something in the middle." : " - an expired or wrong certificate, or something in the middle.")); r.Next.Add("Open Network story > Layer by layer to see the certificate the PC received."); r.GoTab = "travel"; r.GoLabel = "Open Network story"; break;
                case 5: r.Conclude(1, "The network path works all the way; the server itself answered with an error (" + (d.Val[5] ?? "") + "). The problem is the site or your account, not your network."); r.Next.Add("Try again later or check the service's status page."); r.GoTab = "dest"; r.GoLabel = "See the full check"; break;
                default: r.Conclude(0, "The network path to " + t + " works: name, route, door, lock and answer all fine" + (double.IsNaN(d.TtfbMs) ? "." : " (first byte in " + F(d.TtfbMs, " ms") + ").") + " If the app still fails, it's the app, the sign-in or the account."); r.Next.Add("Sign out and in again, or clear the app's cache."); r.GoTab = "dest"; r.GoLabel = "See the full check"; break;
            }
            r.NotEst.Add("Whether the site works for other people right now.");
            r.NotEst.Add("Anything after the first page - sign-in, the app's own API calls.");
            if (d.Lv[2] == 2 && first < 0) r.NotEst.Add("Ping got no reply, but many servers ignore ping on purpose - the steps after it worked.");
        }

        // ---------- calls freeze or sound robotic
        static void Calls(Monitor m, GuideRun r, Action changed)
        {
            r.Status = "Running the pre-call check..."; changed();
            var pre = Tools.PreCall(m, delegate(string st) { r.Status = st; changed(); });
            double jit = m.NetPing.Jitter(20), loss = m.Latest.LossPct, up = m.Up.Avg(10);
            int nb; double bloat = Bloat.Extra(out nb);
            bool wifi, vpn; int sig; lock (m.L) { wifi = m.AdapterKind == "Wi-Fi"; vpn = m.VpnUp; sig = m.WifiSignal; }
            foreach (var row in pre) if (row[1] != "Verdict") { int lv; int.TryParse(row[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv); r.Obs(Evidence.For("precall", row), lv, row[1], row[2]); }
            r.Obs("m", double.IsNaN(jit) ? 3 : jit > 30 ? 2 : jit > 15 ? 1 : 0, "Jitter (how uneven the delay is)", F(jit, " ms"));
            if (wifi) r.Obs(sig < 0 ? "u" : "w", sig >= 0 && sig < 45 ? 1 : sig < 0 ? 3 : 0, "Wi-Fi signal", sig < 0 ? "hidden by Windows (location is off)" : sig + "%");
            if (vpn) r.Obs("w", 1, "VPN", "on - call media may go through the company first");
            string fb = ""; try { fb = M365.TcpFallback(m, M365.Connections(m)); } catch (Exception e) { Program.Log("fallback: " + e.Message); }
            if (fb != "") r.Obs("i", fb.Contains("over TCP 443") && !fb.Contains("no call") ? 2 : 0, "Teams media path", fb);
            changed(); var stun = WhyFails.TeamsUdp(); bool udpOk = stun.Any(x => x[0] == "0" && x[1].StartsWith("UDP", StringComparison.Ordinal));
            r.Obs("m", udpOk ? 0 : 2, "UDP 3478 to Teams' relays", udpOk ? "works" : "blocked - calls go over TCP 443");
            var nat = stun.FirstOrDefault(x => x[1] == "NAT type"); if (nat != null) r.Obs("i", nat[0] == "0" ? 0 : 1, "NAT type", nat[2]);
            string last = ""; lock (global::AdeeboMeter.Calls.Summaries) if (global::AdeeboMeter.Calls.Summaries.Count > 0) last = global::AdeeboMeter.Calls.Summaries[0][1];
            if (last != "") r.Obs("i", 3, "Your last call", last);

            if (!double.IsNaN(loss) && loss > 2) { r.Conclude(2, "Packets are getting lost (" + loss.ToString("0.#", CultureInfo.CurrentCulture) + "%) - calls fill the gaps with robot sounds. " + (wifi ? "On Wi-Fi this is usually signal or interference." : "Restart the modem; if it stays, it's your provider.")); r.GoTab = wifi ? "link" : "dest"; r.GoLabel = wifi ? "Open Your Wi-Fi" : "Check a destination"; }
            else if (!double.IsNaN(bloat) && bloat > 80 && up > 2) { r.Conclude(2, "Your own upload fills the line while you talk - the router adds " + F(bloat, " ms") + ". Pause OneDrive / backups during calls."); r.GoTab = "procs"; r.GoLabel = "Open What's running"; }
            else if (!double.IsNaN(jit) && jit > 30) { r.Conclude(1, "The delay jumps around (jitter " + F(jit, " ms") + ") - that's what makes voices freeze and catch up. " + (wifi ? "Wi-Fi is the usual cause; a cable fixes it." : "Usually congestion at your provider.")); r.GoTab = wifi ? "link" : "dest"; r.GoLabel = wifi ? "Open Your Wi-Fi" : "Check a destination"; }
            else if (!udpOk) { r.Conclude(2, "UDP to Teams is blocked on this network - every call is squeezed into TCP 443, which stutters as soon as anything is lost or busy. Ask IT to allow UDP 3478-3481."); r.GoTab = "toolbox"; r.GoLabel = "Open the Toolbox"; }
            else if (fb.Contains("over TCP 443") && !fb.Contains("no call")) { r.Conclude(1, "Teams is carrying the call over TCP 443 because UDP is blocked - fine on a quiet line, stuttery under load. Ask IT to allow UDP 3478-3481."); r.GoTab = "travel"; r.GoLabel = "Open Network story"; }
            else { r.Conclude(0, "Your end looks fine for calls right now: low jitter, no loss" + (double.IsNaN(bloat) ? "." : ", and your router keeps up under load.") + " If calls still break up, it's likely the other person's connection."); r.GoTab = "live"; r.GoLabel = "Open Live capture"; }
            r.Next.Add("During the next call, start Live capture - it measures the real call stream (jitter, loss, MOS).");
            if (wifi) r.Next.Add("Try one call on a cable: if it's clean, it was the Wi-Fi.");
            r.NotEst.Add("The other person's network - AdeeboMeter only sees your end.");
            r.NotEst.Add("The call stream itself - that needs Live capture during a call.");
        }

        // ---------- Wi-Fi roams timeline (Toolbox): today's Wi-Fi events in order, with ping-pong between two access points called out
        static readonly System.Text.RegularExpressions.Regex RxRoam = new System.Text.RegularExpressions.Regex(@"from (\S+) to (\S+)\)");
        static readonly string[] RoamsHead = { "6", "", "time      event", "", "" };
        public static List<string[]> Roams(Monitor m)
        {
            var rows = new List<string[]>();
            var evs = new List<Timeline.Ev>(m.Events.Recent(300));
            foreach (var day in new[] { DateTime.Today.AddDays(-1), DateTime.Today })   // 24 hours reach into yesterday
                try { foreach (var e in History.Load(day).E) if (!evs.Any(x => Math.Abs((x.T - e.T).TotalSeconds) < 2 && x.Text == e.Text)) evs.Add(e); } catch (Exception e) { Program.Log("roams history: " + e.Message); }
            var since = DateTime.Now.AddHours(-24); evs = evs.Where(e => e.Kind == "wifi" && e.T >= since).OrderBy(e => e.T).ToList();
            var roams = evs.Where(e => e.Text.Contains("access point")).ToList();
            bool onWifi; lock (m.L) onWifi = m.AdapterKind == "Wi-Fi";
            if (evs.Count == 0) { rows.Add(new[] { "0", "Wi-Fi events", "none in the last 24 hours" + (onWifi ? "" : " - this PC isn't on Wi-Fi now"), "Roams, drops, channel changes and signal falls appear here as AdeeboMeter sees them.", "w" }); return rows; }
            // back-and-forth: straight back to the access point it just left, within 2 minutes (needs the from / to recorded since 1.1.2)
            Func<Timeline.Ev, Timeline.Ev, bool> back = (a, b) => { var ma = RxRoam.Match(a.Text); var mb = RxRoam.Match(b.Text); return ma.Success && mb.Success && mb.Groups[1].Value == ma.Groups[2].Value && mb.Groups[2].Value == ma.Groups[1].Value && (b.T - a.T).TotalMinutes < 2; };
            int pingPong = 0; for (int i = 1; i < roams.Count; i++) if (back(roams[i - 1], roams[i])) pingPong++;
            int drops = evs.Count(e => e.Lv == 2);
            rows.Add(new[] { roams.Count > 10 || pingPong > 3 ? "1" : "0", "Last 24 hours", roams.Count + (roams.Count == 1 ? " roam" : " roams") + " · " + drops + (drops == 1 ? " drop" : " drops") + (pingPong > 0 ? " · " + pingPong + " back-and-forth within 2 min" : ""),
                pingPong > 3 ? "Back-and-forth roams mean two access points look equally strong here. Moving the laptop, or asking IT to lower one AP's power or raise the roaming threshold, usually fixes it." : roams.Count > 10 ? "Many roams - normal while walking around an office, not while sitting still." : "", "w" });
            rows.Add(RoamsHead.ToArray());
            Timeline.Ev prev = null;
            foreach (var e in evs.Skip(Math.Max(0, evs.Count - 120)))
            {
                bool isRoam = e.Text.Contains("access point"), pp = isRoam && prev != null && back(prev, e);
                if (isRoam) prev = e;
                rows.Add(new[] { e.Lv == 2 || pp ? "7" : "6", "", e.T.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  " + e.Text + (pp ? "   <- back again within 2 min" : ""), "", "" });
            }
            return rows;
        }

        // ---------- I keep disconnecting
        static void Drops(Monitor m, GuideRun r)
        {
            var evs = new List<Timeline.Ev>(m.Events.Recent(300));
            try { var today = History.Load(DateTime.Today).E; foreach (var e in today) if (!evs.Any(x => Math.Abs((x.T - e.T).TotalSeconds) < 2 && x.Text == e.Text)) evs.Add(e); } catch (Exception e) { Program.Log("drops history: " + e.Message); }
            var since = DateTime.Now.AddHours(-24); evs = evs.Where(e => e.T >= since).OrderBy(e => e.T).ToList();
            Func<Timeline.Ev, bool> drop = e => (e.Kind == "wifi" && e.Lv == 2) || e.Kind == "inet" && e.Lv == 2;
            var drops = evs.Where(drop).ToList();
            int roams = evs.Count(e => e.Kind == "wifi" && e.Text.Contains("access point"));
            int weak = evs.Count(e => e.Kind == "wifi" && e.Text.StartsWith("Wi-Fi signal fell", StringComparison.Ordinal));
            int vpnDrops = evs.Count(e => e.Kind == "vpn" && e.Text.Contains("dropped"));
            int netSwitch = evs.Count(e => e.Kind == "net");
            bool wifi; int sig, rate; lock (m.L) { wifi = m.AdapterKind == "Wi-Fi"; sig = m.WifiSignal; rate = m.RxRate; }
            // was the signal weak in the 2 minutes before each drop?
            int weakBefore = drops.Count(d => evs.Any(e => e.T <= d.T && (d.T - e.T).TotalMinutes <= 2 && e.Kind == "wifi" && (e.Text.StartsWith("Wi-Fi signal fell", StringComparison.Ordinal) || e.Text.Contains("link rate dropped"))));
            string why = evs.Where(e => e.Kind == "wifi" && e.Text.StartsWith("Why:", StringComparison.Ordinal)).Select(e => e.Text.Substring(4).Trim()).LastOrDefault() ?? "";

            r.Obs("w", drops.Count > 2 ? 2 : drops.Count > 0 ? 1 : 0, "Drops in the last 24 h", drops.Count == 0 ? "none recorded" : drops.Count + " (last at " + drops[drops.Count - 1].T.ToString("HH:mm", CultureInfo.CurrentCulture) + ")");
            r.Obs("w", roams > 10 ? 1 : 0, "Hops between access points", roams.ToString(CultureInfo.CurrentCulture));
            r.Obs("w", weak > 3 ? 1 : 0, "Times the signal fell sharply", weak.ToString(CultureInfo.CurrentCulture));
            if (drops.Count > 0) r.Obs("i", weakBefore > drops.Count / 2 ? 1 : 0, "Drops right after the signal fell", weakBefore + " of " + drops.Count);
            if (vpnDrops > 0) r.Obs("w", 1, "VPN drops", vpnDrops.ToString(CultureInfo.CurrentCulture));
            if (netSwitch > 0) r.Obs("w", 0, "Network / address changes", netSwitch.ToString(CultureInfo.CurrentCulture));
            if (why != "") r.Obs("w", 1, "Windows' own reason for the last drop", why);
            if (wifi) r.Obs(sig < 0 ? "u" : "w", sig >= 0 && sig < 40 ? 1 : sig < 0 ? 3 : 0, "Signal now", sig < 0 ? "hidden by Windows (location is off)" + (rate > 0 ? " · link " + rate + " Mb/s" : "") : sig + "%" + (rate > 0 ? " · link " + rate + " Mb/s" : ""));
            if (!Cfg.History) r.Obs("u", 3, "History", "off - only the last 2 hours are known");

            if (drops.Count == 0 && vpnDrops == 0) { r.Conclude(0, "No drops in the last 24 hours" + (Cfg.History ? "" : " (history is off, so only the last 2 hours)") + ". If it happens again, AdeeboMeter will catch it and record why."); r.GoTab = "changes"; r.GoLabel = "Open What changed"; }
            else if (drops.Count == 0) { r.Conclude(1, "The network stays up; the VPN is what drops (" + vpnDrops + " times). That's the VPN client or the company end, not your Wi-Fi."); r.GoTab = "changes"; r.GoLabel = "Open What changed"; }
            else if (weakBefore > drops.Count / 2) { r.Conclude(2, "Most drops come right after the signal fell - distance, walls or interference. The router is too far for where you sit."); r.Next.Add("Do the walk test on Your Wi-Fi to find the dead spots."); r.Next.Add("A mesh point or moving the router fixes this; a new laptop won't."); r.GoTab = "link"; r.GoLabel = "Open Your Wi-Fi"; }
            else { r.Conclude(2, "Wi-Fi drops even with a good signal - that points at the adapter (driver or power saving) or the router, not distance." + (why != "" ? " Windows says: " + why : "")); r.Next.Add("Open Your Wi-Fi: the advisor shows whether Windows' Wi-Fi power saving is on."); r.Next.Add("Update the Wi-Fi driver, then restart the router."); r.GoTab = "link"; r.GoLabel = "Open Your Wi-Fi"; }
            if (roams > 10) r.Next.Add("Many access-point hops: your laptop keeps switching between two routers / mesh points of similar strength.");
            r.NotEst.Add("The router's side of the story - its own log lives on the router.");
            if (wifi && sig < 0) r.NotEst.Add("Signal strength history - Windows hides it without location permission; drops and link rate still count.");
        }

        // ---------- works at home, not on VPN / at work
        static void Vpn(Monitor m, GuideRun r, Action changed)
        {
            string target = (r.Input ?? "").Trim(); bool vpn; lock (m.L) vpn = m.VpnUp;
            r.Obs("w", 3, "VPN", vpn ? "connected" : "not connected right now");
            r.Status = "Looking for overlapping networks..."; changed();
            var ov = PathTools.Overlap(); var bad = ov.FirstOrDefault(x => x[0] == "2" || x[0] == "1");
            r.Obs("w", bad != null ? int.Parse(bad[0], CultureInfo.InvariantCulture) : 0, "Overlapping networks", bad != null ? bad[2] : "none");
            var rules = WhyFails.DnsRules(); var hij = rules.First(); r.Obs(hij[4], int.Parse(hij[0], CultureInfo.InvariantCulture), "DNS for made-up names", hij[2]);
            int nrpt = rules.Count(x => x[1] == "Name rule"); r.Obs("w", 3, "VPN name rules", nrpt > 0 ? nrpt + " rule(s) send company names to the VPN's DNS" : "none");
            string verdict = ""; int lv = 0;
            if (target != "")
            {
                var px = WhyFails.Proxy(target).FirstOrDefault(); if (px != null) r.Obs("w", 3, "Proxy for " + target, px[2]);
                r.Status = "Testing " + target + "..."; changed();
                // "https://intranet.example.com/x", "host:8443" or a bare name: the port test gets host:port, the rest the host
                string tgt = Toolkit.UrlToTarget(target, true), host = Toolkit.HostOnly(target);
                string port = tgt.Contains(":") && !tgt.EndsWith("]", StringComparison.Ordinal) ? tgt : tgt + ":443"; var pt = WhyFails.Port(port).First();
                r.Obs("m", int.Parse(pt[0], CultureInfo.InvariantCulture), "Door (TCP) to " + port, pt[2]);
                // the door's verdict stands on its own - it must not depend on the packet-size step below finding the address
                if (pt[0] == "2") { verdict = pt[1] == "Name" || pt[1] == "Input" ? "Couldn't test " + target + ": " + pt[2] + "." : "The door to " + port + " doesn't open from this network (" + pt[2] + ") - a firewall on the way (yours, the VPN's or theirs) blocks it, or the server is down. At home the path is different, so it may work there."; lv = 2; }
                var dst = Icmp.Resolve4(host);
                if (dst != null)
                {
                    r.Status = "Checking the packet size on the way..."; changed();
                    var hops = PathTools.Trace(dst, 20, () => false); var lastH = hops.LastOrDefault(h => h.Ip != "");
                    if (lastH != null) { PathTools.MtuAt(dst, lastH, 1500); r.Obs("m", lastH.Mtu > 0 && lastH.Mtu < 1400 ? 1 : lastH.BlackHole ? 2 : 0, "Biggest packet that gets there", lastH.Mtu > 0 ? lastH.Mtu + " bytes - " + lastH.MtuNote : lastH.MtuNote); if (lastH.BlackHole) { verdict = "Big packets vanish on the way to " + target + " (an MTU black hole at " + lastH.Mtu + " bytes): small requests work, anything bigger hangs - pages half-load, file shares stall. The VPN or a firewall needs to lower the size (MSS clamping) - tell IT this number."; lv = 2; } }
                }
            }
            if (verdict == "" && bad != null && bad[0] == "2") { verdict = bad[2] + " - addresses in that range can only be reached on one of them, so company servers there look 'down' from home. Change the home router's network (e.g. to 192.168.77.x)."; lv = 2; }
            if (verdict == "" && hij[0] == "2" && nrpt == 0 && vpn) { verdict = "Your DNS answers names that don't exist and the VPN sets no name rules, so company names may land on the provider's page instead of the company server."; lv = 1; }
            if (verdict == "") { verdict = target == "" ? "Nothing obvious on this PC. Type the server or site that fails in the box and run this again - it then tests the door, the proxy and the packet size to it." : "The path to " + target + " looks fine from here: the door opens and full packets get through. The difference is likely in the server's own rules (who it accepts) or your account."; lv = 0; }
            r.Conclude(lv, verdict);
            r.Next.Add("Toolbox > MTU per hop shows exactly where the size drops."); r.Next.Add("Toolbox > Which proxy does this URL use? for browser-only failures.");
            r.NotEst.Add("The company firewall's rules - only IT can see those.");
            r.NotEst.Add("What the VPN does inside its tunnel.");
            r.GoTab = "toolbox"; r.GoLabel = "Open the Toolbox";
        }

        // ---------- my PC is slow
        static void Pc(Monitor m, GuideRun r, Action changed)
        {
            r.Status = "Reading the PC's counters..."; changed();
            foreach (var row in PcHealth.Pressure(m)) r.Obs(row[4], int.Parse(row[0], CultureInfo.InvariantCulture), row[1], row[2]);
            double cpu = m.Cpu.Avg(10);
            r.Obs("w", cpu > 90 ? 2 : cpu > 75 ? 1 : 0, "Processor busy", cpu.ToString("0", CultureInfo.CurrentCulture) + "%");
            r.Status = "Checking background jobs..."; changed();
            var bg = PcHealth.Background(); foreach (var row in bg.Where(x => x[0] != "0" || x[1] != "Background jobs").Take(4)) r.Obs(row[4], int.Parse(row[0], CultureInfo.InvariantCulture), row[1], row[2]);
            var hung = m.Events.Recent(300).Where(e => e.Kind == "pc" && e.T > DateTime.Now.AddHours(-2)).ToList();
            r.Obs("w", hung.Count > 0 ? 1 : 0, "Frozen windows (2 h)", hung.Count == 0 ? "none" : string.Join("; ", hung.Take(3).Select(e => e.Text)));
            var obs = r.ObservedCopy(); Func<string, int> L = name => obs.Where(o => o[2].StartsWith(name, StringComparison.Ordinal)).Select(o => int.Parse(o[1], CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max();
            if (L("Memory read back") == 2 || L("Committed memory") == 2) { r.Conclude(2, "Memory is full: Windows keeps reading programs back from the disk, which is what makes everything feel slow. Close the heaviest app (What's running, sorted by memory)."); r.GoTab = "procs"; r.GoLabel = "Open What's running"; }
            else if (L("Disk latency") == 2) { r.Conclude(2, "The disk is the bottleneck - each request takes far longer than normal. Toolbox > Disk shows who is using it and whether Windows sees the disk failing."); r.GoTab = "toolbox"; r.GoLabel = "Open the Toolbox"; }
            else if (cpu > 90) { r.Conclude(2, "The processor is maxed out (" + cpu.ToString("0", CultureInfo.CurrentCulture) + "%) - see which program in What's running."); r.GoTab = "procs"; r.GoLabel = "Open What's running"; }
            else if (L("Processor speed") >= 1) { r.Conclude(1, "The processor is being held back (heat, battery saver or the power plan) - it runs well below its speed."); r.GoTab = "toolbox"; r.GoLabel = "Open the Toolbox"; }
            else if (L("Driver interrupts") >= 1) { r.Conclude(1, "A driver keeps the processor busy with interrupts - the classic cause of stutter and crackling audio. Update Wi-Fi, audio and graphics drivers."); r.GoTab = "toolbox"; r.GoLabel = "Open the Toolbox"; }
            else if (bg.Any(x => x[0] == "1")) { r.Conclude(1, "A background job is busy right now (" + bg.First(x => x[0] == "1")[1] + ") - it finishes on its own."); r.GoTab = "toolbox"; r.GoLabel = "Open the Toolbox"; }
            else if (hung.Count > 0) { r.Conclude(1, "The PC itself is fine, but a program keeps freezing (" + hung[0].Text + ")."); r.GoTab = "toolbox"; r.GoLabel = "Open the Toolbox"; }
            else { r.Conclude(0, "Nothing on the PC is slow right now - processor, memory, disk and drivers are all fine. If one app is slow, it's that app (or the network: try The internet is slow)."); r.GoTab = "toolbox"; r.GoLabel = "Open the Toolbox"; }
            r.Next.Add("Toolbox > Crashes and hangs, Slow leaks and What changed in 7 days - if it got slow recently, start there.");
            r.Next.Add("Time machine > Replay now shows disk and memory for every minute: find when it was slow.");
            r.NotEst.Add("Why Windows took long to start - that log is admin-only.");
            r.NotEst.Add("Which files a program is writing - that needs a system trace (admin).");
        }
    }
}
