// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Report for my ISP
    // What a provider's support desk asks for, in the order they ask: where the problem starts, hop by hop with the "loss that
    // doesn't carry on isn't real" rule, the hour-of-day pattern over 7 days, the same moment over two routes, the line itself
    // (cable modem levels when the modem shows them), what was ruled out, the worst moments - and a message to paste.
    // Everything is measured from this PC when you press Run; the report file is written to AdeeboMeter's own folder.
    static class IspReport
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string F(double v, string unit) { return double.IsNaN(v) ? "-" : (v < 10 ? v.ToString("0.0", CultureInfo.InvariantCulture) : Math.Round(v).ToString(CultureInfo.InvariantCulture)) + unit; }
        public static bool Private(IPAddress a)
        {
            if (a == null) return true;
            if (a.AddressFamily == AddressFamily.InterNetworkV6) return a.IsIPv6LinkLocal || a.IsIPv6SiteLocal || (a.GetAddressBytes()[0] & 0xFE) == 0xFC;
            var b = a.GetAddressBytes();
            return b[0] == 10 || b[0] == 127 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
        }
        static double Avg(PathTools.Hop h) { int got = h.Sent - h.Lost; return got > 0 ? h.Sum / got : double.NaN; }
        static double Loss(PathTools.Hop h) { return h.Sent > 0 ? 100.0 * h.Lost / h.Sent : double.NaN; }

        sealed class Route { public bool V6Only; public string Label, Host; public IPAddress Ip; public List<PathTools.Hop> Hops = new List<PathTools.Hop>(); }

        // input: a game you added ("valorant"), a host name or an IP; minutes: 1, 2 or 5
        public static List<string[]> Run(Monitor m, string what, string minutes, Toolkit.Run run) { return Run(m, what, minutes, "", run); }
        // withIp: "yes" adds your public address (one HTTPS request to Cloudflare's trace page) - off unless you type yes
        public static List<string[]> Run(Monitor m, string what, string minutes, string withIp, Toolkit.Run run)
        {
            var rows = new List<string[]>();
            string q = (what ?? "").Trim(); int min; if (!int.TryParse((minutes ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out min) || (min != 1 && min != 2 && min != 5)) min = 2;
            // which target(s): a game -> its best region + the second-best (two routes); otherwise the host you typed (and 1.1.1.1 for comparison)
            var games = Games.All; var mon = Games.Monitored();
            var game = q == "" ? games.FirstOrDefault(z => z.Id == GamePing.Me.Playing) ?? games.FirstOrDefault(z => mon.Contains(z.Id))
                               : games.FirstOrDefault(z => string.Equals(z.Id, q, StringComparison.OrdinalIgnoreCase) || string.Equals(z.Name, q, StringComparison.OrdinalIgnoreCase));
            var routes = new List<Route>();
            if (game != null && game.Servers.Count > 0)
            {
                var best = GamePing.Me.Best(game) ?? game.Servers[0];
                routes.Add(new Route { Label = game.Name + " " + best.Name, Host = best.Host });
                var second = game.Servers.Where(z => z.Name != best.Name).Select(z => new { z, a = GameStat.Avg(GamePing.Me.Stat(game.Id, z).Recent(60)) }).OrderBy(z => double.IsNaN(z.a) ? 9999 : z.a).Select(z => z.z).FirstOrDefault();
                if (second != null) routes.Add(new Route { Label = game.Name + " " + second.Name, Host = second.Host });
            }
            else if (q != "")
            {
                if (!Games.ValidHost(q)) { rows.Add(R(2, "Target", "type a game you added, a host name or an IP", "", "")); return rows; }
                routes.Add(new Route { Label = q, Host = q }); routes.Add(new Route { Label = "Reference (1.1.1.1)", Host = "1.1.1.1" });
            }
            else { rows.Add(R(2, "Target", "add a game in the Gamer Zone, or type a host name or IP", "", "")); return rows; }
            foreach (var r in routes)
            {
                try { IPAddress ip; var all = IPAddress.TryParse(r.Host, out ip) ? new[] { ip } : Dns.GetHostAddresses(r.Host); r.Ip = all.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork); if (r.Ip == null && all.Length > 0) r.V6Only = true; }
                catch (SocketException) { r.Ip = null; } catch (ArgumentException) { r.Ip = null; }
            }
            // this report measures IPv4 (the hop probes are IPv4 ICMP): an IPv6 address, or a name with only IPv6 ones, says so
            if (routes.Count > 0 && routes[0].Ip == null && routes[0].V6Only) { rows.Add(R(3, "Target", routes[0].Host + " has only IPv6 addresses - this report measures the IPv4 path only", "", "u")); return rows; }
            routes.RemoveAll(r => r.Ip == null);
            if (routes.Count == 0) { rows.Add(R(2, "Target", "the name doesn't resolve - check the spelling, or DNS is down (try What's wrong?)", "", "m")); return rows; }

            // 1. trace, then MTR on every route at the same time
            run.Status = "Tracing the path..."; Func<bool> stop = () => run.Stop;
            foreach (var r in routes) { r.Hops = PathTools.Trace(r.Ip, 24, stop); if (run.Stop) return rows; }
            run.Status = "Measuring every hop for " + min + " min (one probe a second each)...";
            var threads = routes.Select(r => new Thread(() => PathTools.Mtr(r.Ip, r.Hops, min * 60, stop, delegate { })) { IsBackground = true }).ToList();
            threads.ForEach(t => t.Start()); threads.ForEach(t => t.Join(min * 60 * 1000 + 30000));
            if (run.Stop) { rows.Add(R(3, "Stopped", "the report needs the full run - press Run again", "", "m")); return rows; }
            run.Status = "Looking up router names...";
            var names = new Dictionary<string, string>();
            var look = routes.SelectMany(r => r.Hops).Where(h => h.Ip != "").Select(h => h.Ip).Distinct().Take(60)
                .Select(ip => System.Threading.Tasks.Task.Factory.StartNew(() => { try { var h = Native.PtrName(ip); return Tuple.Create(ip, h != "" ? h : ip); } catch (SocketException) { return Tuple.Create(ip, ip); } catch (ArgumentException) { return Tuple.Create(ip, ip); } })).ToArray();
            System.Threading.Tasks.Task.WaitAll(look, 5000);
            foreach (var t in look) if (t.Status == System.Threading.Tasks.TaskStatus.RanToCompletion) names[t.Result.Item1] = t.Result.Item2;
            // a private hop's own name (Johns-MacBook.lan) stays out of a report you send: just its address, which is redacted
            Func<string, string> nm = ip => { string n; IPAddress pa; return ip == "" ? "(no answer)" : IPAddress.TryParse(ip, out pa) && Private(pa) ? ip : names.TryGetValue(ip, out n) && n != ip ? n : ip; };

            var main = routes[0];
            // where the provider starts: the first hop after your router. Providers often number their first hops from 10.x or
            // CGNAT 100.64.x space, so "private" alone isn't "home" - only the router itself and 192.168.x hops behind it are
            // (a provider's modem in front of your own router, double NAT)
            string gwIp; lock (m.L) gwIp = m.Gateway;
            int gwIdx = main.Hops.FindIndex(h => h.Ip != "" && h.Ip == gwIp);
            int firstIsp = gwIdx < 0 ? main.Hops.FindIndex(h => { IPAddress a; return IPAddress.TryParse(h.Ip, out a) && !Private(a); })   // router not seen on the path (a VPN): the first public hop
                : main.Hops.FindIndex(gwIdx + 1, h => { IPAddress a; if (!IPAddress.TryParse(h.Ip, out a)) return false; var b = a.GetAddressBytes(); return !(b.Length == 4 && b[0] == 192 && b[1] == 168) && !IPAddress.IsLoopback(a); });
            string verdict = PathTools.MtrVerdict(main.Hops);
            int start = StartOfLoss(main.Hops);
            var router = main.Hops.FirstOrDefault(h => h.Sent > 0 && h.Lost < h.Sent); var end = main.Hops.LastOrDefault(h => h.Sent > 0 && h.Lost < h.Sent);
            bool silentEnd = !main.Hops.Any(h => h.Reached && h.Sent > 0 && h.Lost < h.Sent);
            bool wifi; string ssid, dns; lock (m.L) { wifi = m.AdapterKind.IndexOf("Wi", StringComparison.OrdinalIgnoreCase) >= 0; ssid = m.Ssid; dns = m.DnsServer; }

            // finding in one line
            string finding;
            if (start >= 0 && firstIsp >= 0 && start >= firstIsp) finding = "Loss starts at hop " + main.Hops[start].N + " (" + nm(main.Hops[start].Ip) + "), inside or past the provider's network, and carries through to the destination. The home network was clean.";
            else if (start >= 0) finding = "Loss starts at hop " + main.Hops[start].N + " - before the provider (" + (wifi ? "Wi-Fi or the router" : "the home network") + "). Fix the home side first.";
            else if (end != null && Loss(end) >= 1 && !silentEnd) finding = "Only the destination drops some probes - it rate-limits pings; the path itself is clean.";
            else finding = (silentEnd && end != null ? "The destination doesn't answer pings, so this is judged up to the last router that does (hop " + end.N + "). " : "") + "No real loss on the path during the " + min + "-minute run. If it's an evening problem, run it again in the evening - the 7-day pattern below shows when.";
            rows.Add(R(start >= 0 ? 2 : 0, "Finding", finding, verdict, "m"));

            // each part of the path
            rows.Add(R(-1, "EACH PART OF THE PATH", main.Label + " · " + main.Hops.Sum(h => h.Sent) + " probes", "", "m"));
            if (router != null) rows.Add(R(Shown(Loss(router), start >= 0 && router.N >= main.Hops[start].N), "Home router (hop " + router.N + ")", F(Avg(router), " ms") + " avg · worst " + F(router.Worst, " ms") + " · jitter " + F(router.Jit, " ms") + " · loss " + F(Loss(router), "%"), "", "m"));
            if (firstIsp >= 0) { var h = main.Hops[firstIsp]; rows.Add(R(Shown(Loss(h), start >= 0 && h.N >= main.Hops[start].N), "Provider, first hop (" + h.N + ")", F(Avg(h), " ms") + " avg · worst " + F(h.Worst, " ms") + " · loss " + F(Loss(h), "%") + " · " + nm(h.Ip), "", "m")); }
            if (start >= 0) { var h = main.Hops[start]; rows.Add(R(2, "Where loss starts (hop " + h.N + ")", F(Avg(h), " ms") + " avg · worst " + F(h.Worst, " ms") + " · loss " + F(Loss(h), "%") + " · " + nm(h.Ip), "", "m")); }
            if (end != null) rows.Add(R(Shown(Loss(end), start >= 0 && end.N >= main.Hops[start].N), (silentEnd ? "Last router that answers (hop " : "Destination (hop ") + end.N + ")", F(Avg(end), " ms") + " avg · worst " + F(end.Worst, " ms") + " · jitter " + F(end.Jit, " ms") + " · loss " + F(Loss(end), "%"), "", "m"));

            // hop by hop
            // who owns each public hop: only with online lookups switched on (public addresses go to RIPEstat) - otherwise said
            var owners = new Dictionary<string, string>();
            if (Online.On)
            {
                run.Status = "Looking up who owns each network (online lookups are on)...";
                foreach (var ip in main.Hops.Select(h => h.Ip).Where(Online.Shareable).Distinct().Take(30)) { var o = Online.Owner(ip); if (o.Asn != "") owners[ip] = "AS" + o.Asn + (o.Name != "" ? " " + o.Name : ""); }
            }
            rows.Add(R(-1, "HOP BY HOP", "loss at one hop that doesn't continue to the next ones is that router ignoring probes - not a problem" + (Online.On ? "" : " · turn on online lookups (Settings) to name who owns each network"), "", "m"));
            foreach (var h in main.Hops)
            {
                double l = Loss(h); bool real = start >= 0 && h.N >= main.Hops[start].N; string own; owners.TryGetValue(h.Ip, out own);
                rows.Add(R(h.Ip == "" ? 3 : real ? 2 : l >= 3 ? 1 : 0, "Hop " + h.N, nm(h.Ip) + (h.Ip != "" && nm(h.Ip) != h.Ip ? " (" + h.Ip + ")" : "") + (own != null ? " · " + own : "") + " · avg " + F(Avg(h), " ms") + " · worst " + F(h.Worst, " ms") + " · loss " + F(l, "%"), l >= 3 && !real ? "doesn't carry on - not real" : "", "m"));
            }

            // the same moment over two routes
            if (routes.Count > 1)
            {
                rows.Add(R(-1, "SAME TIME, TWO ROUTES", "", "", "m"));
                foreach (var r in routes)
                {
                    var e = r.Hops.LastOrDefault(h => h.Sent > 0); int s = StartOfLoss(r.Hops);
                    string handoff = HandOff(r.Hops, nm);
                    rows.Add(R(e == null ? 3 : Lv(Loss(e)), r.Label, (e == null ? "no reply" : F(Avg(e), " ms") + " avg · loss " + F(Loss(e), "%")) + (handoff != "" ? " · leaves the provider at " + handoff : "") + (s >= 0 ? " · loss from hop " + r.Hops[s].N : ""), "", "m"));
                }
                var a0 = routes[0].Hops.LastOrDefault(h => h.Sent > 0); var a1 = routes[1].Hops.LastOrDefault(h => h.Sent > 0);
                string h0 = HandOff(routes[0].Hops, nm), h1 = HandOff(routes[1].Hops, nm);
                if (a0 != null && a1 != null && h0 != "" && h1 != "" && h0 != h1 && Math.Abs(Loss(a0) - Loss(a1)) >= 1)
                    rows.Add(R(1, "What it means", "Only the route that leaves at " + (Loss(a0) > Loss(a1) ? h0 : h1) + " loses packets - the problem is on that hand-off, not your line. Your provider can steer that traffic another way.", "", "i"));
                else rows.Add(R(3, "What it means", h0 == h1 && h0 != "" ? "Both routes leave the provider at the same place, so this comparison can't single out a hand-off." : "No clear difference between the routes in this run.", "", "i"));
            }

            // 7 days by hour of day
            rows.Add(R(-1, "LOSS BY HOUR OF DAY · LAST 7 DAYS", game != null ? game.Name + " history" : "internet check history", "", "h"));
            var heat = Heat(game, routes[0]);
            if (heat.Count == 0) rows.Add(R(3, "Pattern", "no history yet - it builds up while AdeeboMeter runs (History must be on)", "", "h"));
            else
            {
                var bad = heat.Where(kv => kv.Value >= 1).Select(kv => kv.Key).OrderBy(k => k).ToList();
                rows.Add(R(bad.Count > 0 ? 1 : 0, "Hours with loss of 1% or more", bad.Count == 0 ? "none" : string.Join(", ", bad.Select(hr => hr.ToString("00", CultureInfo.InvariantCulture) + ":00")), bad.Count > 0 && bad.All(hr => hr >= 17 && hr <= 23) ? "Evenings only - the classic sign of a busy (congested) link at peak time." : "", "h"));
                for (int hr = 0; hr < 24; hr += 6) rows.Add(R(-2, hr.ToString("00", CultureInfo.InvariantCulture) + "-" + (hr + 5).ToString("00", CultureInfo.InvariantCulture), string.Join("  ", Enumerable.Range(hr, 6).Select(x => { double v; return x.ToString("00", CultureInfo.InvariantCulture) + ":" + (heat.TryGetValue(x, out v) ? v.ToString("0.0", CultureInfo.InvariantCulture) + "%" : "-"); })), "", "h"));
            }

            // the line
            rows.Add(R(-1, "THE LINE", "", "", "m"));
            rows.Add(R(wifi ? 1 : 0, "Connection", wifi ? "Wi-Fi - a provider will ask you to test on a cable first" : "Ethernet (cable)", "", "w"));
            rows.Add(R(3, "DNS", string.IsNullOrEmpty(dns) ? "-" : dns, "", "w"));
            int pmtu; lock (m.L) pmtu = m.PrimaryMtu;
            bool v6 = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Any(n => { try { return n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && n.GetIPProperties().UnicastAddresses.Any(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6 && !u.Address.IsIPv6LinkLocal && !Private(u.Address)); } catch (System.Net.NetworkInformation.NetworkInformationException) { return false; } });   /* one failing adapter must not sink the report */
            rows.Add(R(3, "Addresses", "IPv4" + (v6 ? " and IPv6 (a public IPv6 address)" : " only - no public IPv6") + (pmtu > 0 ? " · adapter MTU " + pmtu : ""), "", "w"));
            var spd = SpeedTest.Last;
            if (spd != null && (DateTime.Now - spd.T).TotalHours < 24) rows.Add(R(spd.Grade == "" || spd.Grade.StartsWith("A", StringComparison.Ordinal) ? 0 : spd.Grade == "B" ? 1 : 2, "Speed and queueing", SpeedTest.Mbps(spd.Down) + " down" + (spd.NoUpload ? "" : " / " + SpeedTest.Mbps(spd.Up) + " up") + " Mbps · ping " + F(spd.Idle, " ms") + " idle, " + (double.IsNaN(spd.LoadDown) && double.IsNaN(spd.LoadUp) ? "-" : F(Math.Max(double.IsNaN(spd.LoadDown) ? 0 : spd.LoadDown, double.IsNaN(spd.LoadUp) ? 0 : spd.LoadUp), " ms")) + " busy · grade " + (spd.Grade == "" ? "-" : spd.Grade) + " · " + spd.T.ToString("HH:mm", CultureInfo.CurrentCulture) + " to " + spd.Server, spd.Grade != "" && !spd.Grade.StartsWith("A", StringComparison.Ordinal) ? "The delay grows a lot when the line is full (bufferbloat) - that is the router's queue, usually fixed with Smart Queue / SQM on the router, not by the provider." : "", "m"));
            else rows.Add(R(3, "Speed and queueing", "not measured today - press Test my speed on the home page, then make the report again", "", "u"));
            if (string.Equals((withIp ?? "").Trim(), "yes", StringComparison.OrdinalIgnoreCase))
            {
                run.Status = "Asking Cloudflare for your public address (you said yes)...";
                string pub = PublicIp(); rows.Add(R(3, "Your public address", pub != "" ? pub : "couldn't get it", "Your provider can find your line by it. Leave it out if you'll post the report publicly.", "m"));
            }
            run.Status = "Reading the cable modem (if it shows its levels)...";
            foreach (var l in Modem()) rows.Add(l);

            // worst moments: spikes and outages
            rows.Add(R(-1, "WORST MOMENTS", "", "", "m"));
            List<Spike> sp; lock (GamePing.Me.Spikes) sp = GamePing.Me.Spikes.Where(z => game != null && z.Game == game.Id)   /* a typed host: other games' spikes aren't about it */.OrderByDescending(z => z.Ms).Take(5).ToList();
            foreach (var s in sp) rows.Add(R(1, s.T.ToString("ddd HH:mm:ss", CultureInfo.CurrentCulture), s.Server + " " + F(s.Normal, "") + " -> " + F(s.Ms, " ms") + " · " + s.Blame, "", "m"));
            foreach (var o in Outages.Last(m, 7).Take(5)) rows.Add(R(2, "Internet outage", Outages.Text(o), "", "h"));
            if (sp.Count == 0) rows.Add(R(3, "Spikes", "none recorded yet", "", "m"));

            // what's already ruled out - the first things a support desk asks you to check
            var ruled = new List<string>();
            var hop1 = main.Hops.FirstOrDefault();
            if (hop1 != null && hop1.Sent > 0 && Loss(hop1) < 1 && Avg(hop1) < 10) ruled.Add("the home network - the router answered every probe, in " + F(Avg(hop1), " ms"));
            int sig; lock (m.L) sig = m.WifiSignal;
            if (wifi && sig >= 60) ruled.Add("Wi-Fi signal (" + sig + "%)"); else if (!wifi) ruled.Add("Wi-Fi - measured on a cable");
            double dnsMs = m.Dns.Avg(10); if (!double.IsNaN(dnsMs) && dnsMs < 100) ruled.Add("DNS - names look up in " + Math.Round(dnsMs) + " ms");
            var snap = m.Latest; if (snap != null && !double.IsNaN(snap.Cpu) && snap.Cpu < 70) ruled.Add("this PC - CPU " + Math.Round(snap.Cpu) + "%");
            if (spd != null && (DateTime.Now - spd.T).TotalHours < 24 && spd.Grade.StartsWith("A", StringComparison.Ordinal)) ruled.Add("the router's queue - grade " + spd.Grade + " under load");
            rows.Add(R(-1, "ALREADY RULED OUT", "", "", "m"));
            if (ruled.Count == 0) rows.Add(R(3, "Nothing yet", "fix what the report found on the home side first, then run it again", "", "m"));
            foreach (var r in ruled) rows.Add(R(0, "Checked", r, "", "m"));

            // the message to paste, and the report file
            string msg = Message(finding, routes, main, start, firstIsp, wifi, nm, heat);
            rows.Add(R(-1, "MESSAGE FOR YOUR PROVIDER'S SUPPORT CHAT", "click to copy", "", "i"));
            rows.Add(new[] { "3", "Message", msg, "", "i", "copy:" + msg });
            string file = Save(rows, ssid);
            if (file != null) rows.Add(new[] { "0", "Report saved", Path.GetFileName(file) + " - open it in your browser and Print > Save as PDF to attach it", "", "m", "show:" + file });
            string csv = SaveCsv(routes, file);
            if (csv != null) rows.Add(new[] { "3", "Raw data", Path.GetFileName(csv) + " - every hop of every route, for a network engineer", "", "m", "show:" + csv });
            return rows;
        }
        // loss at a hop that doesn't carry on is that router ignoring probes: amber at most, never red
        static int Shown(double loss, bool real) { int l = Lv(loss); return real || l < 2 ? l : 1; }
        static int Lv(double loss) { return double.IsNaN(loss) ? 3 : loss >= 3 ? 2 : loss >= 1 ? 1 : 0; }
        // the first hop whose loss carries on to the end (the same rule as MtrVerdict)
        public static int StartOfLoss(List<PathTools.Hop> hops) { return PathTools.LossStart(hops); }
        // where the route leaves the provider: the last hop whose name shares the first public hop's domain (rDNS), else "-"
        static string HandOff(List<PathTools.Hop> hops, Func<string, string> nm)
        {
            var pub = hops.Where(h => { IPAddress a; return IPAddress.TryParse(h.Ip, out a) && !Private(a); }).ToList(); if (pub.Count < 2) return "";
            Func<string, string> dom = n => { var p = n.Split('.'); return p.Length >= 2 && !IsIp(n) ? p[p.Length - 2] + "." + p[p.Length - 1] : ""; };
            string isp = dom(nm(pub[0].Ip)); if (isp == "") return "";
            var outHop = pub.FirstOrDefault(h => dom(nm(h.Ip)) != isp && dom(nm(h.Ip)) != "");
            return outHop == null ? "" : nm(outHop.Ip);
        }
        static bool IsIp(string n) { IPAddress a; return IPAddress.TryParse(n, out a); }
        // hour of day -> average loss % over 7 days
        static Dictionary<int, double> Heat(Game game, Route main)
        {
            var sum = new Dictionary<int, List<double>>();
            for (int d = 0; d < 7; d++)
            {
                var day = DateTime.Today.AddDays(-d);
                if (game != null)
                {
                    var srv = game.Servers.FirstOrDefault(z => z.Host == main.Host);
                    foreach (var r in GameHistory.Read(day, game.Id).Where(z => srv == null || z.Server == srv.Name))
                        if (!double.IsNaN(r.Loss)) { List<double> l; if (!sum.TryGetValue(r.T.Hour, out l)) sum[r.T.Hour] = l = new List<double>(); l.Add(r.Loss); }
                }
                else if (Cfg.History)
                {
                    var hd = History.Load(day);
                    foreach (var row in hd.M) if (row.Length > 11 && !double.IsNaN(row[11])) { int hr = (int)(row[0] / 3600); List<double> l; if (!sum.TryGetValue(hr, out l)) sum[hr] = l = new List<double>(); l.Add(row[11]); }
                }
            }
            return sum.ToDictionary(kv => kv.Key, kv => kv.Value.Average());
        }
        // cable modems (DOCSIS) often show their signal page at 192.168.100.1 without a password; read it once, 3 s each, never log in
        static List<string[]> Modem()
        {
            var r = new List<string[]>();
            foreach (var path in new[] { "/cmSignalData.htm", "/cmconnectionstatus.html", "/RgConnect.asp", "/" })
            {
                string body = Get("http://192.168.100.1" + path); if (body == null) continue;
                var snr = Nums(body, @"(?i)SNR[^0-9-]{0,80}?(-?\d{1,2}(?:\.\d)?)\s*dB"); var pwr = Nums(body, @"(?i)Power(?: Level)?[^0-9-]{0,80}?(-?\d{1,2}(?:\.\d)?)\s*dBmV");
                var unc = Nums(body, @"(?i)Uncorrect\w*[^0-9]{0,80}?(\d{1,12})");
                if (snr.Count == 0 && pwr.Count == 0) continue;
                // the page lists both directions: downstream power sits around 0 (-7 to +7 dBmV is normal), upstream (what the modem
                // transmits) around +35 to +51 - judged apart. Uncorrectable errors are real data loss: thousands of them is a line problem.
                var down = pwr.Where(v => v < 20).ToList(); var upP = pwr.Where(v => v >= 20).ToList(); pwr = down.Count > 0 ? down : pwr;
                bool okSnr = snr.Count == 0 || snr.Min() >= 33, okPwr = (down.Count == 0 || (down.Min() >= -7 && down.Max() <= 7)) && (upP.Count == 0 || (upP.Min() >= 35 && upP.Max() <= 51)), okUnc = unc.Count == 0 || unc.Sum() < 10000;
                r.Add(R(okSnr && okPwr && okUnc ? 0 : 1, "Cable modem (192.168.100.1)", "SNR " + (snr.Count > 0 ? snr.Min().ToString("0.#", CultureInfo.InvariantCulture) + " dB (lowest)" : "-") + " · power " + (pwr.Count > 0 ? pwr.Min().ToString("0.#", CultureInfo.InvariantCulture) + " to " + pwr.Max().ToString("0.#", CultureInfo.InvariantCulture) + " dBmV" : "-") + " · uncorrectable " + (unc.Count > 0 ? unc.Sum().ToString("0", CultureInfo.InvariantCulture) : "-"), !okSnr || !okPwr ? "Signal levels are outside the usual range - worth telling the provider (they can see the same numbers)." : !okUnc ? "Levels are fine, but thousands of uncorrectable errors mean data was lost on the line - tell the provider." : "The line's signal is in the normal range.", "m"));
                return r;
            }
            r.Add(R(3, "Cable modem", "not readable - fibre / DSL, a locked modem, or no modem page at 192.168.100.1", "", "m"));
            return r;
        }
        static List<double> Nums(string body, string rx) { return Regex.Matches(body, rx).Cast<Match>().Take(64).Select(mm => { double v; return double.TryParse(mm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }).Where(v => !double.IsNaN(v)).ToList(); }
        static string Get(string url)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url); req.Timeout = 3000; req.ReadWriteTimeout = 3000; req.AllowAutoRedirect = false; req.Proxy = null;
                using (var resp = (HttpWebResponse)req.GetResponse()) using (var s = resp.GetResponseStream()) using (var rd = new StreamReader(s))
                {
                    var buf = new char[512 * 1024]; int n = 0, got; while (n < buf.Length && (got = rd.Read(buf, n, buf.Length - n)) > 0) n += got;
                    return new string(buf, 0, n);
                }
            }
            catch (Exception e) { if (!(e is WebException || e is IOException || e is NotSupportedException || e is UriFormatException)) throw; return null; }
        }
        static string Message(string finding, List<Route> routes, Route main, int start, int firstIsp, bool wifi, Func<string, string> nm, Dictionary<int, double> heat)
        {
            var sb = new StringBuilder("Hi - ");
            var bad = heat.Where(kv => kv.Value >= 1).Select(kv => kv.Key).OrderBy(k => k).ToList();
            if (bad.Count > 0) sb.Append("I see packet loss " + (bad.All(h => h >= 17 && h <= 23) ? "in the evenings (" + bad.First().ToString("00", CultureInfo.InvariantCulture) + ":00-" + (bad.Last() + 1).ToString("00", CultureInfo.InvariantCulture) + ":00)" : "at " + string.Join(", ", bad.Select(h => h.ToString("00", CultureInfo.InvariantCulture) + ":00"))) + " over the last week. ");
            sb.Append(wifi ? "This was measured on Wi-Fi. " : "This was measured on a cable, with my router answering normally. ");
            if (start >= 0) sb.Append("The loss starts at hop " + main.Hops[start].N + " (" + nm(main.Hops[start].Ip) + ") and continues to the destination (" + main.Label + "). ");
            if (routes.Count > 1) { var e0 = routes[0].Hops.LastOrDefault(h => h.Sent > 0); var e1 = routes[1].Hops.LastOrDefault(h => h.Sent > 0); if (e0 != null && e1 != null) sb.Append("At the same time " + routes[1].Label + " had " + F(Loss(e1), "%") + " loss against " + F(Loss(e0), "%") + ". "); }
            sb.Append(start >= 0 && firstIsp >= 0 && start >= firstIsp ? "Could your network team check that link, or route this traffic another way? " : "Could you check my line? ");
            sb.Append("I have the full report and raw data if you'd like them.");
            return sb.ToString();
        }
        // a self-contained web page in AdeeboMeter's own folder (data\reports); internal addresses, hardware addresses and the Wi-Fi name removed
        static string Save(List<string[]> rows, string ssid)
        {
            try
            {
                string dir = Path.Combine(Program.DataDir, "reports"); Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "ISP-report-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".html");
                var sb = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Connection quality report</title><style>body{font:14px/1.5 'Segoe UI',sans-serif;color:#1B2330;max-width:900px;margin:32px auto;padding:0 16px}h1{font-size:24px;margin:0 0 4px}h2{font-size:13px;letter-spacing:.06em;color:#4A5568;margin:22px 0 6px;border-bottom:1px solid #E3E8EF;padding-bottom:4px}table{border-collapse:collapse;width:100%}td{padding:4px 8px;border-bottom:1px solid #F0F2F5;vertical-align:top}td.n{font-weight:600;width:240px}.l2{color:#B42318}.l1{color:#8A5A00}.m{color:#4A5568;font-size:12px}.box{background:#F3F5F9;border-radius:8px;padding:12px}@media print{body{margin:0}}</style></head><body>");
                sb.Append("<h1>Connection quality report</h1><div class=\"m\">Measured from the customer's PC with AdeeboMeter (free, open source) on " + Esc(DateTime.Now.ToString("ddd d MMM yyyy HH:mm", CultureInfo.CurrentCulture)) + ". Pings and TCP connects from this PC - not the game's own data.</div><table>");
                foreach (var r in rows)
                {
                    // redacted as text first, then made HTML (a Wi-Fi name "AT&T" is found before it becomes AT&amp;T, and a name like "td" can't break the tags)
                    Func<string, string> X = z => Esc(GameSession.Redact(z ?? "", ssid));
                    if (r[0] == "-1") { sb.Append("</table><h2>" + X(r[1]) + "</h2>" + (r[2] != "" && r[2] != "click to copy" ? "<div class=\"m\">" + X(r[2]) + "</div>" : "") + "<table>"); continue; }
                    if (r.Length > 5 && r[5].StartsWith("show:", StringComparison.Ordinal)) continue;
                    if (r[1] == "Message") { sb.Append("</table><div class=\"box\">" + X(r[2]) + "</div><table>"); continue; }
                    sb.Append("<tr><td class=\"n\">" + X(r[1]) + "</td><td class=\"l" + Esc(r[0]) + "\">" + X(r[2]) + (r[3] != "" ? "<div class=\"m\">" + X(r[3]) + "</div>" : "") + "</td></tr>");
                }
                sb.Append("</table></body></html>");
                File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
                return file;
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("isp report: " + e.Message); return null; }
        }
        // every hop of every route, comma-separated (opens in Excel); private addresses are kept out like in the report
        static string SaveCsv(List<Route> routes, string html)
        {
            if (html == null) return null;
            try
            {
                var sb = new StringBuilder("route,hop,address,sent,lost,loss_pct,avg_ms,best_ms,worst_ms,jitter_ms\r\n");
                foreach (var r in routes)
                    foreach (var h in r.Hops)
                    {
                        IPAddress a; string ip = IPAddress.TryParse(h.Ip, out a) && Private(a) ? "(private)" : h.Ip;
                        sb.Append(string.Join(",", new[] { "\"" + r.Label.Replace("\"", "'") + "\"", h.N.ToString(CultureInfo.InvariantCulture), ip, h.Sent.ToString(CultureInfo.InvariantCulture), h.Lost.ToString(CultureInfo.InvariantCulture), Num(Loss(h)), Num(Avg(h)), Num(h.Best), Num(h.Worst), Num(h.Jit) })).Append("\r\n");
                    }
                string file = Path.ChangeExtension(html, ".csv"); File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false)); return file;
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("isp csv: " + e.Message); return null; }
        }
        static string Num(double v) { return double.IsNaN(v) ? "" : v.ToString("0.#", CultureInfo.InvariantCulture); }
        // your public address, from Cloudflare's plain-text trace page - only when you asked for it
        static string PublicIp()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("https://www.cloudflare.com/cdn-cgi/trace"); req.Timeout = 6000; req.ReadWriteTimeout = 6000; req.UserAgent = "AdeeboMeter/" + Program.Version; req.Proxy = WebRequest.GetSystemWebProxy();
                using (var resp = req.GetResponse()) using (var rd = new StreamReader(resp.GetResponseStream()))
                {
                    var buf = new char[4096]; int n = rd.Read(buf, 0, buf.Length);
                    var mm = Regex.Match(new string(buf, 0, n), @"(?m)^ip=([0-9a-fA-F:.]{2,45})\s*$"); IPAddress a;
                    return mm.Success && IPAddress.TryParse(mm.Groups[1].Value, out a) ? a.ToString() : "";
                }
            }
            catch (Exception e) { if (!(e is WebException || e is IOException)) throw; return ""; }
        }
        static string Esc(string s) { return System.Net.WebUtility.HtmlEncode(s ?? ""); }
    }
}
