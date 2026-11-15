// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace AdeeboMeter
{
    // ================================================================== for traders, server people and streamers (Adeeb, 15 Nov)
    // Market-open check (GO / HOLD before the bell), Service board (your hosts and ports, right now), Go-live check (is the upload
    // ready for your bitrate, and how far is the ingest server). Each one reuses what AdeeboMeter already measures; the new probes
    // are TCP handshakes only (connect, time it, close) - nothing is sent, nothing listens.
    static class Sectors
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string Ms(double v) { return double.IsNaN(v) ? "-" : v.ToString(v < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + " ms"; }

        // n timed TCP handshakes: average, min, max, jitter (mean change between tries), lost
        public sealed class Knocks { public List<double> Ms = new List<double>(); public int Lost; public double Avg { get { return Ms.Count == 0 ? double.NaN : Ms.Average(); } } public double Min { get { return Ms.Count == 0 ? double.NaN : Ms.Min(); } } public double Max { get { return Ms.Count == 0 ? double.NaN : Ms.Max(); } } public double Jit { get { return Ms.Count < 2 ? double.NaN : Enumerable.Range(1, Ms.Count - 1).Average(i => Math.Abs(Ms[i] - Ms[i - 1])); } } }
        public static Knocks Knock(string host, int port, int n, Toolkit.Run run)
        {
            var k = new Knocks();
            for (int i = 0; i < n && !run.Stop; i++)
            {
                var sw = Stopwatch.StartNew();
                using (var c = Toolkit.Open(host, port, 3000)) { if (c == null) k.Lost++; else k.Ms.Add(sw.Elapsed.TotalMilliseconds); }
                if (i < n - 1) System.Threading.Thread.Sleep(150);
            }
            return k;
        }
        static bool Split(string hp, int defPort, out string host, out int port)
        {
            host = (hp ?? "").Trim(); port = defPort; if (host == "") return false;
            host = System.Text.RegularExpressions.Regex.Replace(host, @"^[a-z]+://", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Split('/')[0];
            int c = host.LastIndexOf(':'); int p;
            if (c > 0 && host.IndexOf(':') == c && int.TryParse(host.Substring(c + 1), NumberStyles.None, CultureInfo.InvariantCulture, out p) && p > 0 && p < 65536) { port = p; host = host.Substring(0, c); }
            return Games.ValidHost(host);
        }
        static List<string[]> Pc(Monitor m, ref int warn)
        {
            var rows = new List<string[]>(); var s = m.Latest; string kind; int sig; lock (m.L) { kind = m.AdapterKind; sig = m.WifiSignal; }
            if (kind == "Wi-Fi") { bool weak = sig >= 0 && sig < 60; if (weak) warn++; rows.Add(R(weak ? 1 : 0, "Connection", "Wi-Fi · signal " + (sig < 0 ? "-" : sig + "%"), weak ? "Weak signal - a cable is the steadiest choice for this." : "A cable is steadier still.", "w")); }
            else rows.Add(R(0, "Connection", kind == "LAN" ? "cable" : kind, "", "w"));
            double gw = m.GwPing.Avg(10); bool gwSlow = !double.IsNaN(gw) && gw > 15; if (gwSlow) warn++;
            rows.Add(R(gwSlow ? 1 : 0, "Your router", Ms(gw), gwSlow ? "Slow for a home network - something on it is busy, or the Wi-Fi is." : "", "m"));
            if (!double.IsNaN(s.Cpu) && s.Cpu > 70) { warn++; var top = s.TopCpu.FirstOrDefault(); rows.Add(R(1, "CPU", Math.Round(s.Cpu) + "% busy" + (top != null ? " - most: " + top.Name : ""), "", "m")); }
            foreach (var u in GamerTools.Updaters)
            {
                Process[] ps; try { ps = Process.GetProcessesByName(u[0]); } catch (InvalidOperationException) { continue; }
                foreach (var p in ps) p.Dispose(); if (ps.Length == 0 || u[1] == "") continue;
                warn++; rows.Add(R(1, "Background", u[1], "Pause it - it can start a big transfer any second.", "w"));
            }
            return rows;
        }

        // ---------- traders: GO / HOLD before the bell
        public static List<string[]> MarketOpen(Monitor m, string broker, Toolkit.Run run)
        {
            var rows = new List<string[]>(); int warn = 0;
            string b = (broker ?? "").Trim(); if (b == "") b = Cfg.TradeHost; string host; int port;
            if (!Split(b, 443, out host, out port)) { rows.Add(R(2, "Broker", "type your broker's address (e.g. mybroker.com), or pick it on the Trading page", "", "u")); return rows; }
            rows.AddRange(Pc(m, ref warn));
            run.Status = "Timing 10 connections to " + host + "...";
            var k = Knock(host, port, 10, run);
            bool bad = k.Ms.Count == 0, slow = k.Avg > 120 || k.Jit > 30 || k.Lost > 0; if (bad || slow) warn++;
            rows.Add(R(bad ? 2 : slow ? 1 : 0, "Broker " + host, bad ? "no answer on port " + port : Ms(k.Avg) + " average · jitter " + Ms(k.Jit) + " · worst " + Ms(k.Max) + " · " + k.Lost + " of 10 lost",
                bad ? "The broker doesn't answer from here right now - check its status page, or your connection." : slow ? "Unsteady right now - orders may fill late. Close downloads and streams before the open." : "", "m"));
            run.Status = "Checking the clock (time.windows.com)...";
            var clock = Protocols.Ntp("time.windows.com").FirstOrDefault(r => r.Length > 2 && r[1] == "Your clock");
            if (clock != null) { int lv; if (!int.TryParse(clock[0], out lv)) lv = 3; if (lv >= 1) warn++; rows.Add(R(lv, "Clock", clock[2], lv >= 1 ? "Order and fill times are stamped by this clock - fix it in Settings > Time > Sync now." : "", "m")); }
            else rows.Add(R(3, "Clock", "couldn't ask the time server", "", "m"));
            var s = m.Latest; bool busy = !double.IsNaN(s.Down) && s.Down > 20 || !double.IsNaN(s.Up) && s.Up > 5; if (busy) warn++;
            rows.Add(R(busy ? 1 : 0, "This PC's traffic", SpeedTest.Mbps(s.Down) + " down · " + SpeedTest.Mbps(s.Up) + " up (Mbps)", busy ? "Something is downloading or uploading - it adds delay to every order." : "Quiet.", "w"));
            rows.Insert(0, R(warn == 0 ? 0 : bad ? 2 : 1, "Market-open check", warn == 0 ? "GO - nothing in the way" : (bad ? "HOLD - " : "CAREFUL - ") + warn + " thing" + (warn == 1 ? "" : "s") + " to fix first", "AdeeboMeter only checks - it never trades, closes or changes anything.", "i"));
            return rows;
        }

        // ---------- server people: your hosts and ports, right now
        static readonly char[] ListSep = { ',', ' ', ';', '\n' };
        public static List<string[]> Board(string list, Toolkit.Run run)
        {
            var rows = new List<string[]>(); var items = (list ?? "").Split(ListSep, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (items.Count == 0) { string[] pins; lock (Cfg.Pins) pins = Cfg.Pins.ToArray(); items = pins.Select(Security.HostOf).Where(x => x != null).Distinct().ToList(); }
            if (items.Count == 0) { rows.Add(R(3, "Service board", "type hosts and ports, e.g. nas.local:445, mail.example.com:587, 192.0.2.10:22", "Or pin sites in Investigate › Check a destination - they show here.", "u")); return rows; }
            int up = 0, n = 0;
            foreach (var it in items.Take(16))
            {
                if (run.Stop) break; string host; int port; if (!Split(it, 443, out host, out port)) { rows.Add(R(2, it, "not a host name or address", "", "u")); continue; }
                n++; run.Status = host + ":" + port + "...";
                var k = Knock(host, port, 5, run);
                if (k.Ms.Count == 0) { rows.Add(R(2, host + ":" + port, "down - no answer to 5 tries", "", "m")); continue; }
                up++; int lv = k.Lost > 0 || k.Avg > 200 ? 1 : 0;
                rows.Add(R(lv, host + ":" + port, Ms(k.Avg) + " · min " + Ms(k.Min) + " · max " + Ms(k.Max) + (k.Lost > 0 ? " · " + k.Lost + " of 5 lost" : ""), "", "m"));
            }
            rows.Insert(0, R(up == n ? 0 : up == 0 ? 2 : 1, "Up right now", up + " of " + n, "TCP handshakes only - connect, time it, close.", "m"));
            return rows;
        }

        // ---------- streamers: is the upload ready for your bitrate?
        static readonly string[][] Ingest = { new[] { "twitch", "Twitch", "live.twitch.tv" }, new[] { "youtube", "YouTube", "a.rtmp.youtube.com" }, new[] { "kick", "Kick", "fa723fc1b171.global-contribute.live-video.net" } };
        public static List<string[]> GoLive(Monitor m, string bitrate, string service, Toolkit.Run run)
        {
            var rows = new List<string[]>(); int warn = 0; var ic = CultureInfo.InvariantCulture;
            double kbps; if (!double.TryParse((bitrate ?? "").Trim().Replace("kbps", "").Trim(), NumberStyles.Float, ic, out kbps) || kbps <= 0) kbps = 6000;
            string sv = (service ?? "").Trim().ToLowerInvariant(); var ing = Ingest.FirstOrDefault(z => sv == "" ? z[0] == "twitch" : z[0] == sv || string.Equals(z[1], sv, StringComparison.OrdinalIgnoreCase));
            string host = ing != null ? ing[2] : sv, name = ing != null ? ing[1] : sv;
            rows.AddRange(Pc(m, ref warn));
            var spd = SpeedTest.Last; double upl = spd != null && (DateTime.Now - spd.T).TotalHours < 24 && !spd.NoUpload ? spd.Up : double.NaN, need = kbps / 1000.0;
            if (double.IsNaN(upl)) rows.Add(R(1, "Your upload", "not measured today", "Press Test my speed on the home page first - the check needs your real upload.", "u"));
            else { bool tight = upl < need * 1.5; if (tight) warn++; rows.Add(R(upl < need ? 2 : tight ? 1 : 0, "Your upload", SpeedTest.Mbps(upl) + " Mbps (measured " + spd.T.ToString("HH:mm", CultureInfo.CurrentCulture) + ")", tight ? "Too little room - a stream needs about 1.5x its bitrate to stay smooth." : "", "m")); }
            double safe = double.IsNaN(upl) ? double.NaN : Math.Floor(upl * 0.7 * 10) / 10;
            rows.Add(R(double.IsNaN(safe) ? 3 : safe * 1000 < kbps ? 2 : 0, "Your bitrate", kbps.ToString("0", ic) + " kbps", "", "u"));
            if (!double.IsNaN(safe)) rows.Add(R(safe * 1000 < kbps ? 1 : 0, "Safe bitrate", (safe * 1000).ToString("0", ic) + " kbps", safe * 1000 < kbps ? "Lower OBS's bitrate to this, or test again on a cable." : "Your bitrate fits.", "i"));
            var s = m.Latest; bool busyUp = !double.IsNaN(s.Up) && s.Up > 1; if (busyUp) warn++;
            rows.Add(R(busyUp ? 1 : 0, "Upload in use now", SpeedTest.Mbps(s.Up) + " Mbps", busyUp ? "Something on this PC is already uploading (a backup, cloud sync) - it competes with the stream." : "", "w"));
            if (Games.ValidHost(host))
            {
                run.Status = "Timing " + name + "'s ingest server...";
                var k = Knock(host, 1935, 5, run); bool bad = k.Ms.Count == 0, slow = k.Avg > 80 || k.Lost > 0; if (bad || slow) warn++;
                rows.Add(R(bad ? 2 : slow ? 1 : 0, name + " ingest", bad ? "no answer on RTMP 1935" : Ms(k.Avg) + " · jitter " + Ms(k.Jit) + (k.Lost > 0 ? " · " + k.Lost + " of 5 lost" : ""), bad ? "Blocked or down - try another ingest server in OBS (Settings > Stream > Server)." : "", "m"));
            }
            rows.Insert(0, R(warn == 0 ? 0 : 1, "Go-live check", warn == 0 ? "ready - go live" : warn + " thing" + (warn == 1 ? "" : "s") + " to fix first", "AdeeboMeter only checks - it never changes OBS or closes anything.", "i"));
            return rows;
        }
    }
}
