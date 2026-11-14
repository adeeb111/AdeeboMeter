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
            public List<double> FarAll = new List<double>(); public double MinuteDown, MinuteFar; public int MinuteN, MinuteFarN, MinuteIdx = -1;
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
            var est = list.Where(c => c.State == "ESTABLISHED" && Watch.IsPublic(c.RemoteIp)).ToList();
            if (est.Count == 0) { Expire(); return; }
            // names: Windows' DNS cache, re-read at most once a minute (nothing goes on the network)
            if (Cfg.IpNames && (DateTime.UtcNow - lastNames).TotalSeconds >= 60) { lastNames = DateTime.UtcNow; try { m.RefreshIpNames(); } catch (Exception e) { Program.Log("streams names: " + e.Message); } }
            Dictionary<string, string> ipn; lock (m.L) ipn = m.IpNames;
            double down; lock (m.L) down = Avg(m.Down.Values(15));
            string svc = null, via = "", far = ""; int port = 443, best = 0;
            foreach (var g in est.GroupBy(c => { string h; string hn = ipn.TryGetValue(c.RemoteIp, out h) ? h : ""; return ServiceOf(hn) ?? (PlayerOf(c.Proc) != null ? "player:" + PlayerOf(c.Proc) : ""); }).Where(gr => gr.Key != ""))
            {
                int n = g.Count(); if (n <= best) continue;
                best = n; svc = g.Key.StartsWith("player:", StringComparison.Ordinal) ? g.Key.Substring(7) + " (network video)" : g.Key;
                var top = g.GroupBy(c => c.RemoteIp).OrderByDescending(x => x.Count()).First().First();
                far = top.RemoteIp; port = top.RemotePort; via = Via(g.GroupBy(c => c.Proc).OrderByDescending(x => x.Count()).First().Key);
            }
            if (svc == null && Manual != "") { svc = Manual; via = "you said so"; far = ""; }
            // a stream pulls video: at least 1.5 Mb/s over the last 15 s (a menu page, a paused video or a closed tab doesn't)
            bool playing = svc != null && (down >= 1.5 || Manual != "");
            lock (k)
            {
                if (playing)
                {
                    if (Now != null && Now.S.What != svc) End(DateTime.Now);
                    if (Now == null) { Now = new Live { S = new WatchKit.Session { Mode = "stream", What = svc, Via = via, Start = DateTime.Now, End = DateTime.Now } }; Ver++; m.Events.Add(0, "stream", svc + " started streaming" + (via != "" ? " in " + via : "")); }
                    Now.LastSeen = DateTime.Now; if (far != "") { Now.FarIp = far; Now.FarPort = port; }
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
            if (l.FarIp != "" && (now - l.LastFar).TotalSeconds >= FarSec - 0.2)
            {
                l.LastFar = now; IPAddress ip;
                if (IPAddress.TryParse(l.FarIp, out ip)) { s.Far = GamePing.TcpMs(ip, l.FarPort > 0 ? l.FarPort : 443, 1500); s.FarLost = double.IsNaN(s.Far); }
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
                if (!double.IsNaN(s.Down)) { l.MinuteDown += s.Down; l.MinuteN++; }
                if (!double.IsNaN(s.Far)) { l.MinuteFar += s.Far; l.MinuteFarN++; }
                l.S.End = now; l.S.FarNormal = farNormal; Ver++;
            }
        }
        static void Point(Live l)
        {
            if (l.S.Line.Count >= 600) return;   // 10 hours of minutes is plenty for one record
            l.S.Line.Add(new[] { (float)l.MinuteIdx, (float)(l.MinuteDown / Math.Max(1, l.MinuteN)), l.MinuteFarN > 0 ? (float)(l.MinuteFar / l.MinuteFarN) : -1f });
        }
        public static void End(DateTime at)
        {
            Live l;
            lock (k) { l = Now; Now = null; if (l == null) return; Ver++; }
            var d = l.Maker.Close(FarName); if (d != null) l.S.Dips.Add(d);
            if (l.MinuteN > 0) Point(l);
            l.S.End = at > l.S.Start ? at : l.S.End;
            var downs = l.Samples.Where(x => !double.IsNaN(x.Down)).Select(x => x.Down).ToList(); l.S.AvgDown = downs.Count > 0 ? downs.Average() : double.NaN;
            if (l.S.Length.TotalMinutes >= 2) WatchKit.Save(l.S);   // a two-minute clip isn't worth a record
        }
        public static void SetManual(string what) { lock (k) { Manual = (what ?? "").Trim(); if (Manual.Length > 60) Manual = Manual.Substring(0, 60); Ver++; } }

        static double Avg(double[] v) { var x = v.Where(z => !double.IsNaN(z)).ToList(); return x.Count == 0 ? double.NaN : x.Average(); }
        public static double Median(List<double> v) { if (v.Count == 0) return double.NaN; var o = v.OrderBy(z => z).ToList(); return o[o.Count / 2]; }

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
            string why = double.IsNaN(mbps) ? "The speed test didn't finish." : mbps >= 40 ? "Plenty of room." : wifi && signal >= 0 && signal < 50 ? "Your Wi-Fi is the limit here (signal " + signal + "%). A cable or a seat nearer the router would likely help." : !double.IsNaN(gwMs) && gwMs > 30 ? "Your home network is slow to answer (" + gwMs.ToString("0", CultureInfo.InvariantCulture) + " ms to the router)." : "That's your internet plan or your provider tonight.";
            r.Add(why);
            return r.ToArray();
        }
    }
}
