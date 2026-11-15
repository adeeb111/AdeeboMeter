// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;

namespace AdeeboMeter
{
    // ================================================================== Streaming (PLAN-modes.md 2)
    // Finds a stream by the service's own server names (from Windows' DNS cache - the same names Connections shows), in an
    // app or a browser alike; never reads tabs or window titles. While it plays: one look at the whole path every 5 s and
    // a TCP knock on the stream's server every 10 s; each dip gets a verdict (WatchKit). The session is saved when it ends.
    static class Streams
    {
        public sealed class Service { public string Name; public string[] Hosts; public Service(string n, params string[] h) { Name = n; Hosts = h; } }
        // video servers first: a page of the service's menu is not a stream until the download rate says so as well
        public static readonly Service[] Known =
        {
            new Service("YouTube", "googlevideo.com", "youtube.com", "ytimg.com"),
            new Service("Netflix", "nflxvideo.net", "netflix.com", "nflxso.net"),
            new Service("Prime Video", "aiv-cdn.net", "aiv-delivery.net", "pv-cdn.net", "primevideo.com"),
            new Service("Twitch", "ttvnw.net", "twitch.tv", "jtvnw.net"),
            new Service("Hulu", "hulustream.com", "hulu.com", "huluim.com"),
            new Service("Disney+", "dssott.com", "disneyplus.com", "bamgrid.com"),
            new Service("Max", "hbomaxcdn.com", "max.com", "hbomax.com"),
            new Service("Apple TV+", "tv.apple.com", "hls.itunes.apple.com", "play-edge.itunes.apple.com"),
            new Service("Paramount+", "paramountplus.com", "cbsaavideo.com", "cbsivideo.com"),
            new Service("Peacock", "peacocktv.com", "peacock.com"),
            new Service("Crave", "crave.ca", "bellmedia.ca"),
            new Service("DAZN", "dazn.com", "indazn.com"),
            new Service("Tubi", "tubi.video", "tubitv.com"),
            new Service("Crunchyroll", "crunchyroll.com", "vrv.co"),
            new Service("Hotstar", "hotstar.com"),
            new Service("Sportsnet+", "sportsnet.ca", "sportsnetplus.ca"),
            new Service("TSN", "tsn.ca"),
            new Service("ESPN", "espn.com", "espncdn.com"),
            new Service("Vimeo", "vimeocdn.com", "vimeo.com"),
            new Service("Kick", "kick.com")
        };
        // player apps that stream from anywhere (IPTV playlists, network video): named by the app, never by the provider
        static readonly string[][] Players = { new[] { "vlc", "VLC" }, new[] { "kodi", "Kodi" }, new[] { "mpv", "mpv" }, new[] { "potplayermini64", "PotPlayer" }, new[] { "potplayermini", "PotPlayer" }, new[] { "myiptv", "MyIPTV Player" }, new[] { "iptvsmarters", "IPTV Smarters" }, new[] { "perfectplayer", "Perfect Player" } };
        static readonly Dictionary<string, string> browsers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "chrome", "Chrome" }, { "msedge", "Edge" }, { "firefox", "Firefox" }, { "brave", "Brave" }, { "opera", "Opera" }, { "vivaldi", "Vivaldi" } };

        public static string ServiceOf(string host)
        {
            if (string.IsNullOrEmpty(host)) return null; host = host.TrimEnd('.').ToLowerInvariant();
            foreach (var s in Known) foreach (var h in s.Hosts) if (host == h || host.EndsWith("." + h, StringComparison.Ordinal)) return s.Name;
            return null;
        }
        public static string PlayerOf(string proc)
        {
            string p = (proc ?? "").Replace(" ", "").ToLowerInvariant(); if (p.EndsWith(".exe", StringComparison.Ordinal)) p = p.Substring(0, p.Length - 4);
            foreach (var x in Players) if (p == x[0] || (x[0].Length > 5 && p.StartsWith(x[0], StringComparison.Ordinal))) return x[1];
            return p.Contains("iptv") ? proc : null;
        }
        public static string Via(string proc) { string b; return browsers.TryGetValue(proc ?? "", out b) ? b : Monitor.Friendly(proc ?? ""); }

        // ---------- live state (probe thread writes, the window reads under k)
        public static readonly object k = new object();
        public sealed class Live
        {
            public WatchKit.Session S; public string FarIp = ""; public int FarPort = 443; public DateTime LastSeen, LastFar = DateTime.MinValue, LastSample = DateTime.MinValue;
            public List<WatchKit.Sample> Samples = new List<WatchKit.Sample>(); public WatchKit.DipMaker Maker = new WatchKit.DipMaker();
            public List<double> FarAll = new List<double>(); public int FarFails; public double MinuteDown, MinuteFar; public int MinuteN, MinuteFarN, MinuteIdx = -1;
            public double SumDown; public int NDown;                     // AM-CG-146: the whole session's average, not the last buffer's
            public DateTime StallFrom = DateTime.MinValue;              // AM-CG-144: the service still connected, but no video arriving
            public bool NewServer;                                      // AM-CG-145: the server changed - its normal starts again
        }
        public static Live Now;            // the stream playing now (null: none)
        public static string Manual = "";  // "I'm watching..." when nothing was recognised
        public const string FarName = "the stream's server";
        public static long Ver;            // bumps on every change the page should repaint for
        static DateTime lastNames = DateTime.MinValue;
        public static bool Active { get { lock (k) return Now != null; } }
        // the preset's pace: Penny looks every 30 s, everything else every 5 s (the server knock every 10 s)
        public static int SampleSec { get { return Cfg.Preset == "penny" ? 30 : 5; } }
        public static int FarSec { get { return Cfg.Preset == "penny" ? 30 : 10; } }

        // From Monitor.ProbeConns (every few seconds): is something streaming, and from where?
        public static void Conns(Monitor m, List<Conn> list)
        {
            if (!Cfg.Streams || Cfg.Off("stream")) { End(DateTime.Now); return; }
            if (SpeedTest.Busy) return;   // AM-CG-150: a speed test's own traffic is never mistaken for a stream
            // public servers, plus a known video player talking to the local network (a LAN IPTV box, Plex at home - AM-CG-143)
            var est = list.Where(c => c.State == "ESTABLISHED" && (Watch.IsPublic(c.RemoteIp) || PlayerOf(c.Proc) != null)).ToList();
            if (est.Count == 0 && Manual == "") { Expire(); return; }   // AM-CG-135: a stream you named needs no TCP (QUIC is UDP)
            // names: Windows' DNS cache, re-read at most once a minute (nothing goes on the network)
            if (Cfg.IpNames && (DateTime.UtcNow - lastNames).TotalSeconds >= 60) { lastNames = DateTime.UtcNow; try { m.RefreshIpNames(); } catch (Exception e) { Program.Log("streams names: " + e.Message); } }
            Dictionary<string, string> ipn; lock (m.L) ipn = m.IpNames;
            double down; lock (m.L) down = Avg(m.Down.Values(15));
            string svc = null, via = "", far = "", proc = ""; int port = 443, best = 0;
            foreach (var g in est.GroupBy(c => { string h; string hn = ipn.TryGetValue(c.RemoteIp, out h) ? h : ""; return ServiceOf(hn) ?? (PlayerOf(c.Proc) != null ? "player:" + PlayerOf(c.Proc) : ""); }).Where(gr => gr.Key != ""))
            {
                int n = g.Count(); if (n <= best) continue;
                best = n; svc = g.Key.StartsWith("player:", StringComparison.Ordinal) ? g.Key.Substring(7) + " (network video)" : g.Key;
                // the busiest server of that service, and the app holding THOSE connections - never an address from one app and a name from another (AM-CG-142)
                var top = g.GroupBy(c => c.RemoteIp + ":" + c.RemotePort).OrderByDescending(x => x.Count()).First().First();
                far = top.RemoteIp; port = top.RemotePort; proc = top.Proc; via = Via(top.Proc);
            }
            if (svc == null && Manual != "") { svc = Manual; via = "you said so"; far = ""; }
            // AM-CG-134: an open YouTube tab while another app downloads isn't a stream - when another program is the one
            // writing that much to the disk, the traffic is its download
            string other = PcHealth.TopIo(); long otherBytes = other == "" ? 0 : PcHealth.IoLastMinute(other);
            bool someoneElse = other != "" && proc != "" && !other.Equals(proc, StringComparison.OrdinalIgnoreCase) && !double.IsNaN(down) && otherBytes > down / 8 * 1048576 * 60 * 0.5;
            // a stream pulls video: at least 1.5 Mb/s over the last 15 s (a menu page, a paused video or a closed tab doesn't)
            bool playing = svc != null && !someoneElse && (down >= 1.5 || (svc == Manual && down >= 0.3));
            lock (k)
            {
                if (playing)
                {
                    if (Now != null && Now.S.What != svc) End(DateTime.Now);
                    if (Now == null) { Now = new Live { S = new WatchKit.Session { Mode = "stream", What = svc, Via = via, Start = DateTime.Now, End = DateTime.Now } }; Ver++; m.Events.Add(0, "stream", svc + " started streaming" + (via != "" ? " in " + via : "")); }
                    if (Now.StallFrom != DateTime.MinValue) { Now.S.Dips.Add(new WatchKit.Dip { T = Now.StallFrom, Secs = (int)(DateTime.Now - Now.StallFrom).TotalSeconds, Blame = "unsure", Evidence = "no video arrived - the service stayed connected" }); Now.StallFrom = DateTime.MinValue; Ver++; }
                    Now.LastSeen = DateTime.Now;
                    // AM-CG-136/145: a new address or port is a new server - its delay is judged against its own normal, and it gets its own six tries
                    if (far != "" && (far != Now.FarIp || port != Now.FarPort)) { Now.NewServer = Now.FarIp != ""; Now.FarIp = far; Now.FarPort = port; Now.FarFails = 0; Now.FarAll.Clear(); }
                }
                else if (Now != null && svc != null && svc == Now.S.What && !someoneElse)
                {
                    // AM-CG-144: still connected but nothing arriving - a stall, not the end: the time counts, as a dip, for up to 5 minutes
                    if (Now.StallFrom == DateTime.MinValue) Now.StallFrom = DateTime.Now;
                    Now.LastSeen = DateTime.Now;
                    if ((DateTime.Now - Now.StallFrom).TotalMinutes > 5) { Now.S.Dips.Add(new WatchKit.Dip { T = Now.StallFrom, Secs = (int)(DateTime.Now - Now.StallFrom).TotalSeconds, Blame = "unsure", Evidence = "no video arrived for over 5 minutes - the stream may have stopped" }); End(DateTime.Now); }
                    return;
                }
            }
            if (!playing) Expire();
        }
        static void Expire() { lock (k) if (Now != null && (DateTime.Now - Now.LastSeen).TotalSeconds > 90) End(Now.LastSeen); }

        // From Monitor.ProbeLoop while a stream plays: a 5 s look at the path, the server every 10 s.
        public static void Tick(Monitor m)
        {
            Live l; lock (k) l = Now; if (l == null) return;
            var now = DateTime.Now; if ((now - l.LastSample).TotalSeconds < SampleSec - 0.2) return;
            l.LastSample = now;
            var s = new WatchKit.Sample { T = now };
            lock (m.L)
            {
                s.Router = m.GwPing.Last; s.RouterLost = m.GwEverReplied && double.IsNaN(s.Router); s.Net = m.NetPing.Last; s.NetLost = m.NetEverReplied && double.IsNaN(s.Net);
                s.Down = Avg(m.Down.Values(5)); s.Cpu = Avg(m.Cpu.Values(5)); s.Wifi = m.AdapterKind == "Wi-Fi"; s.Signal = m.WifiSignal;
                var busy = m.Apps.OrderByDescending(a => a.Cpu).FirstOrDefault(); if (busy != null && busy.Cpu >= 30) s.Busy = busy.Name;
            }
            // a server that never answers a knock (some block them) gets six tries, then no more - each miss holds this loop 1.5 s
            if (l.FarIp != "" && !(l.FarFails >= 6 && l.FarAll.Count == 0) && (now - l.LastFar).TotalSeconds >= FarSec - 0.2)
            {
                l.LastFar = now; IPAddress ip;
                if (IPAddress.TryParse(l.FarIp, out ip)) { s.Far = GamePing.TcpMs(ip, l.FarPort > 0 ? l.FarPort : 443, 1500); s.FarLost = double.IsNaN(s.Far); if (s.FarLost) l.FarFails++; }
            }
            lock (k)
            {
                if (Now != l) return;
                if (!double.IsNaN(s.Far)) { l.FarAll.Add(s.Far); if (l.FarAll.Count > 400) l.FarAll.RemoveAt(0); }
                double farNormal = Median(l.FarAll);
                string blame = WatchKit.Judge(s, farNormal);
                if (blame == "far" && s.FarLost && l.FarAll.Count == 0) blame = null;   // a server that never answered a knock (some block it): no verdict on it
                var d = l.Maker.Add(s, blame, SampleSec, FarName); if (d != null) { l.S.Dips.Add(d); Ver++; }
                l.Samples.Add(s); if (l.Samples.Count > 180) l.Samples.RemoveAt(0);   // the last 15 minutes for the live chart
                int mi = (int)(now - l.S.Start).TotalMinutes;
                if (mi != l.MinuteIdx && l.MinuteIdx >= 0 && l.MinuteN > 0) Point(l);
                if (mi != l.MinuteIdx) { l.MinuteIdx = mi; l.MinuteDown = 0; l.MinuteFar = 0; l.MinuteN = 0; l.MinuteFarN = 0; }
                if (!double.IsNaN(s.Down)) { l.MinuteDown += s.Down; l.MinuteN++; l.SumDown += s.Down; l.NDown++; }
                if (!double.IsNaN(s.Far)) { l.MinuteFar += s.Far; l.MinuteFarN++; }
                l.S.End = now; l.S.FarNormal = farNormal; Ver++;
            }
        }
        static void Point(Live l)
        {
            // AM-CG-147: past 600 points the record keeps its whole length - neighbouring points merge (half the detail), never dropped
            if (l.S.Line.Count >= 600)
            {
                var m = new List<float[]>(); for (int i = 0; i + 1 < l.S.Line.Count; i += 2) { var a = l.S.Line[i]; var b = l.S.Line[i + 1]; m.Add(new[] { a[0], (a[1] + b[1]) / 2, a[2] < 0 ? b[2] : b[2] < 0 ? a[2] : (a[2] + b[2]) / 2 }); }
                if (l.S.Line.Count % 2 == 1) m.Add(l.S.Line[l.S.Line.Count - 1]); l.S.Line = m;
            }
            l.S.Line.Add(new[] { (float)l.MinuteIdx, (float)(l.MinuteDown / Math.Max(1, l.MinuteN)), l.MinuteFarN > 0 ? (float)(l.MinuteFar / l.MinuteFarN) : -1f });
        }
        public static void End(DateTime at)
        {
            Live l;
            lock (k) { l = Now; Now = null; if (l == null) return; Ver++; }
            var d = l.Maker.Close(FarName); if (d != null) l.S.Dips.Add(d);
            if (l.MinuteN > 0) Point(l);
            l.S.End = at > l.S.Start ? at : l.S.End;
            l.S.AvgDown = l.NDown > 0 ? l.SumDown / l.NDown : double.NaN;   // the whole session (AM-CG-146)
            if (l.S.Length.TotalMinutes >= 2) WatchKit.Save(l.S);   // a two-minute clip isn't worth a record
            Moments.StreamEnded(l.S.What, l.S.Length.TotalMinutes, l.S.Dips.Count);
        }
        public static void SetManual(string what) { lock (k) { Manual = (what ?? "").Trim(); if (Manual.Length > 60) Manual = Manual.Substring(0, 60); Ver++; } }

        static double Avg(double[] v) { var x = v.Where(z => !double.IsNaN(z)).ToList(); return x.Count == 0 ? double.NaN : x.Average(); }
        // a true median: the middle two averaged when there's an even number (AM-CG-137: 10 and 90 ms is 50, not 90)
        public static double Median(List<double> v) { if (v.Count == 0) return double.NaN; var o = v.OrderBy(z => z).ToList(); int n = o.Count; return n % 2 == 1 ? o[n / 2] : (o[n / 2 - 1] + o[n / 2]) / 2; }

        // ---------- "Can I stream 4K right now?": what each quality needs, judged on a measured speed and the path's health
        public static readonly string[][] Qualities = { new[] { "SD", "3" }, new[] { "HD", "5" }, new[] { "Full HD", "8" }, new[] { "4K", "25" } };
        public static string[] Verdict4K(double mbps, double gwMs, int signal, bool wifi)
        {
            var r = new List<string>();
            foreach (var q in Qualities)
            {
                double need = double.Parse(q[1], CultureInfo.InvariantCulture);
                r.Add(q[0] + "|" + (double.IsNaN(mbps) ? "unknown" : mbps >= need * 1.6 ? "safe" : mbps >= need ? "risky" : "no") + "|" + need);
            }
            // AM-CG-138: enough speed isn't enough when the path is shaky - weak Wi-Fi or a slow router make 4K "risky" whatever the speed
            bool weakWifi = wifi && signal >= 0 && signal < 50, slowRouter = !double.IsNaN(gwMs) && gwMs > 60;
            if (weakWifi || slowRouter) for (int i = 0; i < r.Count; i++) if (r[i].StartsWith("4K|", StringComparison.Ordinal) || r[i].StartsWith("Full HD|", StringComparison.Ordinal)) r[i] = r[i].Replace("|safe|", "|risky|");
            string path = weakWifi ? "your Wi-Fi signal is weak (" + signal + "%)" : slowRouter ? "your router is slow to answer (" + gwMs.ToString("0", CultureInfo.InvariantCulture) + " ms)" : "";
            // AM-CG-139: a low number has several possible causes - say which ones, don't pick one
            string why = double.IsNaN(mbps) ? "The speed test didn't finish."
                : mbps >= 40 ? (path == "" ? "Enough speed for 4K, and the path looked steady." : "Enough speed for 4K, but " + path + " - expect quality to drop now and then.")
                : path != "" ? "Under what 4K needs, and " + path + " - start there: a cable or a seat nearer the router."
                : "Under what 4K needs. The test can't tell why: your plan, a busy evening on your provider's network, other devices using the line, or the test server itself. Test again later, and from a cable, to narrow it down.";
            r.Add(why);
            return r.ToArray();
        }
    }
}
