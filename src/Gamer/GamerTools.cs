// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace AdeeboMeter
{
    // 1.1.5 Gamer tools: would a GPN help me, the pre-match checklist, the best time to play, which game plays best tonight,
    // and console mode. They read the Gamer Zone's own pings and history; the GPN verdict and console mode send a few pings.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "result rows and small lists, built once per run")]
    static class GamerTools
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string Ms(double v) { return double.IsNaN(v) ? "-" : Math.Round(v).ToString(CultureInfo.CurrentCulture) + " ms"; }
        static string Pc(double v) { return double.IsNaN(v) ? "-" : v.ToString("0.#", CultureInfo.CurrentCulture) + "%"; }

        // the game asked for, else the one running, else the first one watched
        public static Game Pick(string which, out string err)
        {
            err = ""; var games = Games.All; string q = (which ?? "").Trim();
            Game g = q != "" ? games.FirstOrDefault(x => x.Id.Equals(q, StringComparison.OrdinalIgnoreCase) || x.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) : null;
            if (g == null && q == "" && GamePing.Me.Playing != null) g = games.FirstOrDefault(x => x.Id == GamePing.Me.Playing);
            if (g == null && q == "") { var mon = Games.Monitored(); g = games.FirstOrDefault(x => mon.Contains(x.Id)); }
            if (g == null) err = q != "" ? "\"" + q + "\" isn't in the Gamer Zone - add it there first (it knows the game's servers)" : "no game watched yet - click a game in the Gamer Zone first";
            return g;
        }
        static GameServer Best(Game g) { return GamePing.Me.Best(g) ?? (g.Servers.Count > 0 ? g.Servers[0] : null); }
        static IPAddress Resolve(GameServer s)
        {
            IPAddress ip; if (IPAddress.TryParse(s.Host, out ip)) return ip;
            try { return Dns.GetHostAddresses(s.Host).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork); } catch (SocketException) { return null; } catch (ArgumentException) { return null; }
        }

        // ---------- would a GPN (a paid "ping reducer") help me? Only where the trouble is on the internet between your provider
        // and the game - never your Wi-Fi, never your PC, never the game's own server. The spikes say where; a short trace finds the hop.
        public static List<string[]> Gpn(Monitor m, string which, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string err; var g = Pick(which, out err);
            if (g == null) { rows.Add(R(2, "Game", err, "", "u")); return rows; }
            var srv = Best(g); if (srv == null) { rows.Add(R(2, "Server", g.Name + " has no servers to check", "", "u")); return rows; }
            List<Spike> spikes; lock (GamePing.Me.Spikes) spikes = GamePing.Me.Spikes.Where(z => z.Game == g.Id).ToList();
            int home = spikes.Count(z => z.Blame.StartsWith("your Wi-Fi", StringComparison.Ordinal) || z.Blame.StartsWith("your home", StringComparison.Ordinal)), isp = spikes.Count(z => z.Blame == "your provider"), far = spikes.Count - home - isp;
            if (spikes.Count > 0) rows.Add(R(3, "Spikes this run", spikes.Count + " · " + home + " at home · " + isp + " at your provider · " + far + " past it", "From the Gamer Zone's own pings while AdeeboMeter ran.", "m"));
            var ip = Resolve(srv); if (ip == null) { rows.Add(R(2, "Server", "couldn't find " + srv.Name, "", "m")); return rows; }
            run.Status = "Tracing the way to " + srv.Name + "...";
            var hops = PathTools.Trace(ip, 20, () => run.Stop).Where(h => h.Ip != "").ToList();
            if (hops.Count == 0) { rows.Add(R(1, "Path", "no router on the way answered - can't tell", "", "m")); return rows; }
            // five pings per hop: where does the delay or the loss start, and does it carry on to the end?
            var stats = new List<Tuple<PathTools.Hop, double, double>>();
            using (var p = new Ping())
                foreach (var h in hops.Take(16))
                {
                    if (run.Stop) break; run.Status = "Timing hop " + h.N + " of " + hops.Count + "...";
                    var v = new List<double>(); IPAddress hip; if (!IPAddress.TryParse(h.Ip, out hip)) continue;
                    for (int i = 0; i < 5; i++) { try { var r = p.Send(hip, 1000); v.Add(r.Status == IPStatus.Success ? r.RoundtripTime : double.NaN); } catch (PingException) { v.Add(double.NaN); } Thread.Sleep(120); }
                    stats.Add(Tuple.Create(h, GameStat.Avg(v.ToArray()), GameStat.Loss(v.ToArray())));
                }
            int firstPublic = stats.FindIndex(s => { IPAddress a; return IPAddress.TryParse(s.Item1.Ip, out a) && !IspReport.Private(a); });
            int ispEnd = firstPublic < 0 ? stats.Count - 1 : Math.Min(stats.Count - 1, firstPublic + 1);   // your provider: up to the hop after its first public router
            double endAvg = stats.Count > 0 ? stats[stats.Count - 1].Item2 : double.NaN;
            int jump = -1;
            for (int i = 1; i < stats.Count; i++)
            {
                double prev = stats.Take(i).Select(s => s.Item2).Where(x => !double.IsNaN(x)).DefaultIfEmpty(double.NaN).Max(), cur = stats[i].Item2;
                bool carries = stats.Skip(i).Where(s => !double.IsNaN(s.Item2)).All(s => s.Item2 >= cur - 10);
                bool lossCarries = stats[i].Item3 >= 20 && stats.Skip(i).All(s => s.Item3 >= 20);
                if ((!double.IsNaN(prev) && !double.IsNaN(cur) && cur > prev + 30 && carries) || lossCarries) { jump = i; break; }
            }
            rows.Add(R(-1, "THE WAY TO " + srv.Name.ToUpperInvariant() + (srv.Kind == "estimate" ? " (A REGION ESTIMATE)" : ""), stats.Count + " hops", "", "m"));
            for (int i = 0; i < stats.Count; i++)
            {
                var s = stats[i]; string who = i == 0 ? "your router" : i <= ispEnd ? "your provider" : i == stats.Count - 1 && s.Item1.Reached ? "the server" : "the internet (transit)";
                rows.Add(R(i == jump ? 2 : 3, "Hop " + s.Item1.N + " · " + s.Item1.Ip, Ms(s.Item2) + " · loss " + Pc(s.Item3), who + (i == jump ? " - the delay starts here and carries on" : ""), "m"));
            }
            string verdict, why; int lv;
            if (home > spikes.Count / 2 && home > 0) { lv = 2; verdict = "No - the trouble is at home"; why = "Most spikes started on your own Wi-Fi or network. A GPN starts after your router, so it can't fix this. A cable, a better Wi-Fi spot or stopping a download will."; }
            else if (jump < 0) { lv = 0; verdict = "No - your path is clean"; why = "No hop adds delay or loss that carries on to the server. A GPN can't beat the speed of light; " + Ms(endAvg) + " is about what the distance costs."; }
            else if (jump <= ispEnd) { lv = 1; verdict = "Probably not - it's inside your provider"; why = "The delay starts at hop " + stats[jump].Item1.N + ", still your provider's network. A GPN still rides those first hops; call your provider (Report for my ISP builds the evidence)."; }
            else if (jump == stats.Count - 1) { lv = 1; verdict = "No - it's the game's own server"; why = "The delay only appears at the last hop: the server or its network is busy. A GPN arrives at the same server."; }
            else { lv = 0; verdict = "Maybe - this is what a GPN is for"; why = "The delay starts at hop " + stats[jump].Item1.N + " (" + stats[jump].Item1.Ip + "), between your provider and the game. A GPN takes another road around it. Try one on a free trial and compare with AdeeboMeter running."; }
            rows.Insert(0, R(lv, "Would a GPN help?", verdict, why, "i"));
            if (srv.Kind == "estimate") rows.Add(R(3, "Note", "this traced the cloud region " + g.Name + " runs in, not your match server", "Find my match server (Gamer Zone, needs Wireshark) gives the real one.", "i"));
            return rows;
        }

        // ---------- before you queue: what could spoil the match right now. Suggestions only - nothing is closed for you.
        static readonly string[][] Updaters = { new[] { "TiWorker", "Windows Update is installing" }, new[] { "MoUsoCoreWorker", "Windows Update is working" }, new[] { "OneDrive", "OneDrive is running (it syncs in the background)" }, new[] { "steamwebhelper", "" }, new[] { "EpicGamesLauncher", "Epic Games Launcher is open (it downloads updates)" }, new[] { "Battle.net", "Battle.net is open (it downloads updates)" }, new[] { "Dropbox", "Dropbox is running" }, new[] { "GoogleDriveFS", "Google Drive is syncing" }, new[] { "qbittorrent", "a torrent program is open" }, new[] { "utorrent", "a torrent program is open" } };
        public static List<string[]> PreMatch(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>(); var s = m.Latest; int warn = 0;
            string kind; int sig; lock (m.L) { kind = m.AdapterKind; sig = m.WifiSignal; }
            if (kind == "Wi-Fi") { bool weak = sig >= 0 && sig < 60; if (weak) warn++; rows.Add(R(weak ? 1 : 0, "Connection", "Wi-Fi · signal " + (sig < 0 ? "-" : sig + "%"), weak ? "Weak. A cable is the single best fix for lag; or move closer to the router." : "A cable is still steadier for games.", "w")); }
            else rows.Add(R(0, "Connection", kind == "LAN" ? "cable - the best there is" : kind, "", "w"));
            double gw = m.GwPing.Avg(10), net = m.NetPing.Avg(10);
            bool gwSlow = !double.IsNaN(gw) && gw > 15; if (gwSlow) warn++;
            rows.Add(R(gwSlow ? 1 : 0, "Router right now", Ms(gw), gwSlow ? "Slow for a home network - something on it is busy, or the Wi-Fi is." : "", "m"));
            rows.Add(R(3, "Internet right now", Ms(net), "", "m"));
            bool busyNet = !double.IsNaN(s.Down) && s.Down > 20 || !double.IsNaN(s.Up) && s.Up > 5; if (busyNet) warn++;
            rows.Add(R(busyNet ? 1 : 0, "This PC's traffic", Pc0(s.Down) + " down · " + Pc0(s.Up) + " up (Mbps)", busyNet ? "Something is downloading or uploading - an upload hurts game ping most." : "Quiet.", "w"));
            run.Status = "Looking for updaters and sync apps...";
            foreach (var u in Updaters)
            {
                Process[] ps; try { ps = Process.GetProcessesByName(u[0]); } catch (InvalidOperationException) { continue; }
                foreach (var p in ps) p.Dispose(); if (ps.Length == 0 || u[1] == "") continue;
                warn++; rows.Add(R(1, "Background", u[1], "Pause it before you play - it can start a download any second.", "w"));
            }
            if (!double.IsNaN(s.Cpu) && s.Cpu > 60) { warn++; var top = s.TopCpu.FirstOrDefault(); rows.Add(R(1, "CPU", Math.Round(s.Cpu) + "% busy" + (top != null ? " - most: " + top.Name + " (" + top.Cpu.ToString("0", CultureInfo.CurrentCulture) + "%)" : ""), "A busy CPU adds input lag and stutter that looks like network lag.", "w")); }
            if (!double.IsNaN(s.Mem) && s.Mem > 85) { warn++; var top = s.TopMem.FirstOrDefault(); rows.Add(R(1, "Memory", Math.Round(s.Mem) + "% used" + (top != null ? " - most: " + top.Name : ""), "Close a browser with many tabs before you play.", "w")); }
            string e; var g = Pick("", out e);
            if (g != null) { var b = Best(g); if (b != null) { var v = GamePing.Me.Stat(g.Id, b).Recent(30); rows.Add(R(3, "Best server now", g.Name + " · " + b.Name + " · " + Ms(GameStat.Avg(v)) + (GameStat.Loss(v) > 0 ? " · loss " + Pc(GameStat.Loss(v)) : ""), b.Kind == "estimate" ? "A region estimate - the game picks the real server." : "", "m")); } }
            rows.Insert(0, R(warn == 0 ? 0 : 1, "Ready to play?", warn == 0 ? "yes - nothing in the way" : warn + " thing" + (warn == 1 ? "" : "s") + " could spoil the match", "AdeeboMeter only suggests - it never closes a program for you.", "i"));
            return rows;
        }
        static string Pc0(double v) { return double.IsNaN(v) ? "-" : v.ToString("0.#", CultureInfo.CurrentCulture); }

        // ---------- the best time to play: the Gamer Zone's history by weekday and hour
        public static List<string[]> BestTime(string which)
        {
            var rows = new List<string[]>(); string err; var g = Pick(which, out err);
            if (g == null) { rows.Add(R(2, "Game", err, "", "u")); return rows; }
            if (!Cfg.History) { rows.Add(R(1, "History", "off", "Turn on History in Settings - the Gamer Zone then keeps a line a minute for each server.", "u")); return rows; }
            var all = new List<GameHistory.Row>(); for (int i = 0; i < Cfg.GameDays; i++) all.AddRange(GameHistory.Read(DateTime.Today.AddDays(-i), g.Id));
            if (all.Count < 60) { rows.Add(R(1, "Not enough yet", all.Count + " minutes recorded for " + g.Name, "Leave the Gamer Zone watching it for a few days (background game ping in Settings).", "u")); return rows; }
            var server = all.GroupBy(x => x.Server).OrderBy(x => GameStat.Avg(x.Select(y => y.Avg).ToArray())).First();
            var slots = server.GroupBy(x => new { D = (int)x.T.DayOfWeek, H = x.T.Hour }).Where(x => x.Count() >= 5)
                .Select(x => new { x.Key.D, x.Key.H, Avg = GameStat.Avg(x.Select(y => y.Avg).ToArray()), P95 = GameStat.Avg(x.Select(y => y.P95).ToArray()), Loss = GameStat.Avg(x.Select(y => y.Loss).ToArray()) })
                .Select(x => new { x.D, x.H, x.Avg, x.Loss, Score = x.P95 + 20 * (double.IsNaN(x.Loss) ? 0 : x.Loss) }).Where(x => !double.IsNaN(x.Score)).ToList();
            if (slots.Count < 3) { rows.Add(R(1, "Not enough yet", "need a few different hours recorded", "", "u")); return rows; }
            var c = CultureInfo.CurrentCulture; var dn = c.DateTimeFormat.DayNames;
            Func<int, string> hr = h => h.ToString("00", c) + ":00";
            rows.Add(R(3, "Server", server.Key + " · " + Ms(GameStat.Avg(server.Select(y => y.Avg).ToArray())) + " on average", g.Name + "'s best server over " + Cfg.GameDays + " days.", "m"));
            rows.Add(R(-1, "BEST HOURS", "", "", "m"));
            foreach (var x in slots.OrderBy(x => x.Score).Take(3)) rows.Add(R(0, dn[x.D] + " " + hr(x.H), Ms(x.Avg) + (x.Loss > 0 ? " · loss " + Pc(x.Loss) : ""), "", "m"));
            rows.Add(R(-1, "WORST HOURS", "", "", "m"));
            foreach (var x in slots.OrderByDescending(x => x.Score).Take(3)) rows.Add(R(x.Score > slots.Min(z => z.Score) * 1.5 ? 1 : 3, dn[x.D] + " " + hr(x.H), Ms(x.Avg) + (x.Loss > 0 ? " · loss " + Pc(x.Loss) : ""), "", "m"));
            var ev = slots.Where(x => x.H >= 19 && x.H <= 22).ToList();
            if (ev.Count > 0 && ev.Average(x => x.Score) > slots.Average(x => x.Score) * 1.3) rows.Add(R(1, "Evenings", "worse than the rest of the day", "Peak time on your provider's network. Not much a player can do - except avoid big downloads at home then.", "i"));
            return rows;
        }

        // ---------- which game plays best tonight: every watched game's best server, right now
        public static List<string[]> Tonight()
        {
            var rows = new List<string[]>(); var mon = Games.Monitored(); var gp = GamePing.Me;
            var list = Games.All.Where(g => mon.Contains(g.Id) && !g.Soon).Select(g =>
            {
                var b = Best(g); var v = b == null ? Array.Empty<double>() : gp.Stat(g.Id, b).Recent(60);
                return new { G = g, B = b, Avg = GameStat.Avg(v), Loss = GameStat.Loss(v), Jit = GameStat.Jitter(v), N = v.Count(x => !double.IsNaN(x)) };
            }).Where(x => x.B != null && x.N > 0).OrderBy(x => x.Avg + 20 * (double.IsNaN(x.Loss) ? 0 : x.Loss) + 2 * (double.IsNaN(x.Jit) ? 0 : x.Jit)).ToList();
            if (list.Count == 0) { rows.Add(R(1, "No pings yet", "watch at least one game in the Gamer Zone - background game ping then measures its best region", "", "u")); return rows; }
            int i = 0;
            foreach (var x in list)
                rows.Add(R(i++ == 0 ? 0 : 3, (i == 1 ? "★ " : i + ". ") + x.G.Name, x.B.Name + " · " + Ms(x.Avg) + " · jitter " + Ms(x.Jit) + (x.Loss > 0 ? " · loss " + Pc(x.Loss) : ""), i == 1 ? "Smoothest right now." : "", "m"));
            if (list.Any(x => x.B.Kind == "estimate")) rows.Add(R(3, "Note", "for most games these are the cloud regions they run in, not your match server", "", "i"));
            return rows;
        }

        // ---------- console mode: a PlayStation / Xbox / Switch on your network. The PC can't see the console's own game traffic,
        // so it times the console's link (from here), your router, the internet and the game's region - and says which leg is off.
        public static List<string[]> ConsoleCheck(Monitor m, string ipText, string which, string secsText, Toolkit.Run run)
        {
            var rows = new List<string[]>(); IPAddress ip; string mine; lock (m.L) mine = m.LocalIp;
            if (!IPAddress.TryParse((ipText ?? "").Trim(), out ip) || ip.AddressFamily != AddressFamily.InterNetwork || !IspReport.Private(ip) || (mine != "" && !Reach.SameNet(ip.ToString(), mine)))
            { rows.Add(R(2, "Console", "type the console's address on your network - e.g. 192.168.1.40", "PlayStation: Settings > Network > Connection status. Xbox: Settings > General > Network settings > Advanced. Switch: System Settings > Internet.", "u")); return rows; }
            int secs; if (!int.TryParse((secsText ?? "").Trim(), out secs)) secs = 30; secs = Math.Max(10, Math.Min(120, secs));
            string err; var g = Pick(which, out err); var srv = g != null ? Best(g) : null; var sip = srv != null ? Resolve(srv) : null;
            string gw; lock (m.L) gw = m.Gateway;
            var con = new List<double>(); var gws = new List<double>(); var nets = new List<double>(); var gms = new List<double>();
            using (var p = new Ping())
                for (int i = 0; i < secs && !run.Stop; i++)
                {
                    var sw = Stopwatch.StartNew(); run.Status = "Second " + (i + 1) + " of " + secs + "...";
                    try { var r = p.Send(ip, 900); con.Add(r.Status == IPStatus.Success ? r.RoundtripTime : double.NaN); } catch (PingException) { con.Add(double.NaN); }
                    gws.Add(m.GwPing.Last); nets.Add(m.NetPing.Last);
                    if (sip != null && i % 5 == 0) gms.Add(GamePing.Probe(srv, out err));
                    run.Add(con[con.Count - 1]);
                    int wait = 1000 - (int)sw.ElapsedMilliseconds; if (wait > 0) Thread.Sleep(wait);
                }
            var ca = con.ToArray(); double cAvg = GameStat.Avg(ca), cLoss = GameStat.Loss(ca), cJit = GameStat.Jitter(ca);
            rows.Add(R(-1, "THE FOUR LEGS", secs + " seconds", "", "m"));
            bool conBad = double.IsNaN(cAvg) || cLoss >= 5 || cAvg > 20 || cJit > 10;
            rows.Add(R(double.IsNaN(cAvg) ? 2 : conBad ? 1 : 0, "PC → console", double.IsNaN(cAvg) ? "no answer" : Ms(cAvg) + " · jitter " + Ms(cJit) + " · loss " + Pc(cLoss), double.IsNaN(cAvg) ? "Consoles often ignore ping when asleep; wake it, or check the address." : "Your home network to the console - includes the console's Wi-Fi.", "m"));
            double ga = GameStat.Avg(gws.ToArray()), na = GameStat.Avg(nets.ToArray()), sa = GameStat.Avg(gms.ToArray());
            rows.Add(R(3, "PC → router", Ms(ga) + (gw != "" ? " (" + gw + ")" : ""), "", "m"));
            rows.Add(R(3, "Internet", Ms(na), "", "m"));
            if (srv != null) rows.Add(R(3, g.Name + " · " + srv.Name, Ms(sa), srv.Kind == "estimate" ? "The cloud region the game runs in, timed from this PC." : "", "m"));
            string v; int lv;
            if (double.IsNaN(cAvg)) { lv = 1; v = "the console didn't answer - can't time its link"; }
            else if (conBad && !double.IsNaN(ga) && ga < 10) { lv = 2; v = "the console's own link: the router answers this PC quickly, the console doesn't - move it closer, use 5 GHz, or a cable"; }
            else if (!double.IsNaN(ga) && ga > 20) { lv = 1; v = "your home network is slow for everyone right now - something is downloading, or the router is busy"; }
            else if (!double.IsNaN(na) && na > 80) { lv = 1; v = "your internet connection is slow right now - a provider problem if it lasts"; }
            else { lv = 0; v = "home, router and internet look fine - lag now would be the game server or the console itself"; }
            rows.Insert(0, R(lv, "Console verdict", v, "", "i"));
            return rows;
        }
    }
}
