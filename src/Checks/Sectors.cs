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
    static partial class Sectors
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
        static readonly Dictionary<string, int> SchemePorts = new Dictionary<string, int> { { "http", 80 }, { "https", 443 }, { "ssh", 22 }, { "ftp", 21 }, { "smtp", 25 }, { "imaps", 993 }, { "ldap", 389 }, { "ldaps", 636 }, { "rdp", 3389 }, { "rtmp", 1935 } };
        static bool Split(string hp, int defPort, out string host, out int port)
        {
            host = (hp ?? "").Trim(); port = defPort; if (host == "") return false;
            var sm = System.Text.RegularExpressions.Regex.Match(host, @"^([a-z][a-z0-9+.-]*)://", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (sm.Success) { int sp; if (SchemePorts.TryGetValue(sm.Groups[1].Value.ToLowerInvariant(), out sp)) port = sp; host = host.Substring(sm.Length); }
            host = host.Split('/')[0];
            var v6 = System.Text.RegularExpressions.Regex.Match(host, @"^\[([0-9a-fA-F:.%]+)\](?::(\d{1,5}))?$"); int p;
            if (v6.Success) { if (v6.Groups[2].Success) { if (!int.TryParse(v6.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out p) || p < 1 || p > 65535) return false; port = p; } host = v6.Groups[1].Value;   /* R2-04: a port that was typed and is wrong is refused, never swapped for the default */ return Games.ValidHost(host); }
            int c = host.LastIndexOf(':');
            if (c > 0 && host.IndexOf(':') == c) { if (!int.TryParse(host.Substring(c + 1), NumberStyles.None, CultureInfo.InvariantCulture, out p) || p < 1 || p > 65535) return false; port = p; host = host.Substring(0, c); }
            return Games.ValidHost(host);
        }
        static List<string[]> Pc(Monitor m, ref int warn)
        {
            var rows = new List<string[]>(); var s = m.Latest; string kind; int sig; lock (m.L) { kind = m.AdapterKind; sig = m.WifiSignal; }
            if (kind == "Wi-Fi") { bool weak = sig >= 0 && sig < 60; if (weak) warn++; rows.Add(R(weak ? 1 : sig < 0 ? 3 : 0, "Connection", "Wi-Fi · signal " + (sig < 0 ? "hidden by Windows" : sig + "%"), weak ? "Weak signal - a cable is the steadiest choice for this." : "A cable is steadier still.", "w")); }
            else rows.Add(R(0, "Connection", kind == "LAN" ? "cable" : kind, "", "w"));
            double gw = m.GwPing.Avg(10); bool gwSlow = !double.IsNaN(gw) && gw > 15; if (gwSlow) warn++;
            rows.Add(R(double.IsNaN(gw) ? 3 : gwSlow ? 1 : 0, "Your router", double.IsNaN(gw) ? "not measured" : Ms(gw), gwSlow ? "Slow for a home network - something on it is busy, or the Wi-Fi is." : "", "m"));   // AM-CG-132: unknown stays unknown
            if (!double.IsNaN(s.Cpu) && s.Cpu > 70) { warn++; var top = s.TopCpu.FirstOrDefault(); rows.Add(R(1, "CPU", Math.Round(s.Cpu) + "% busy" + (top != null ? " - most: " + top.Name : ""), "", "m")); }
            foreach (var u in GamerTools.Updaters)
            {
                Process[] ps; try { ps = Process.GetProcessesByName(u[0]); } catch (InvalidOperationException) { continue; } catch (System.ComponentModel.Win32Exception) { continue; }
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
            var clock = Protocols.Ntp("time.windows.com").FirstOrDefault(r => r.Length > 2 && r[1].StartsWith("Your clock", StringComparison.Ordinal));   // review 1.1.6 P3 #4: the row is "Your clock vs this server ..."
            if (clock != null) { int lv; if (!int.TryParse(clock[0], out lv)) lv = 3; if (lv >= 1) warn++; rows.Add(R(lv, "Clock", clock[2], lv >= 1 ? "Order and fill times are stamped by this clock - fix it in Settings > Time > Sync now." : "", "m")); }
            else { warn++; rows.Add(R(1, "Clock", "couldn't ask the time server - unchecked", "Order times are stamped by this clock; check it once (Settings > Time > Sync now).", "m")); }
            var s = m.Latest; bool busy = !double.IsNaN(s.Down) && s.Down > 20 || !double.IsNaN(s.Up) && s.Up > 5, noTraffic = double.IsNaN(s.Down) && double.IsNaN(s.Up); if (busy) warn++;
            rows.Add(R(noTraffic ? 3 : busy ? 1 : 0, "This PC's traffic", noTraffic ? "not measured" : SpeedTest.Mbps(s.Down) + " down · " + SpeedTest.Mbps(s.Up) + " up (Mbps)", busy ? "Something is downloading or uploading - it adds delay to every order." : noTraffic ? "" : "Quiet.", "w"));
            // AM-CG-128: this checks the connection, not the market - the session is reported on its own line
            var et = Trading.EasternNow(); bool mo = Trading.MarketOpen(et); string hol = Markets.TodayNote(et);
            rows.Add(R(3, "Market", (mo ? L.T(Trading.VenueName) + " " + L.T("open now") : L.T(Trading.VenueName) + " " + L.T("closed now")) + (hol != "" ? " · " + hol : ""), mo ? "" : "The connection can be ready while the market isn't open.", "m"));
            rows.Insert(0, R(warn == 0 ? 0 : bad ? 2 : 1, "Market-open check", warn == 0 ? "Connection ready - nothing in the way (broker under 120 ms, steady)" : (bad ? "HOLD - " : "CAREFUL - ") + warn + " thing" + (warn == 1 ? "" : "s") + " to fix first", "AdeeboMeter only checks - it never trades, closes or changes anything.", "i"));
            return rows;
        }

        // ---------- server people: your hosts and ports, right now
        static readonly char[] ListSep = { ',', ' ', ';', '\n' };
        public static List<string[]> Board(string list, Toolkit.Run run) { return Board(list, "", run); }
        public static List<string[]> Board(string list, string every, Toolkit.Run run)
        {
            string ev = (every ?? "").Trim().ToLowerInvariant(); int mins;
            var rows = new List<string[]>();
            if (ev == "off" || ev == "stop") { if (SetWatch(0, "")) rows.Add(R(0, "Watching", "stopped - no more background checks", "", "i")); else rows.Add(R(2, "Watching", "couldn't stop it - the watch file can't be changed, so checks carry on", "Check that AdeeboMeter's data folder isn't read-only, then try Off again.", "i")); return rows; }
            // bug-hunt ML-104: a schedule with nothing to watch isn't saved
            bool nothing = (list ?? "").Split(ListSep, StringSplitOptions.RemoveEmptyEntries).Length == 0; if (nothing) lock (Cfg.Pins) nothing = Cfg.Pins.Count == 0;
            if (ev != "" && ev != "off" && ev != "stop" && nothing) { rows.Add(R(1, "Watching", "nothing to watch - type hosts and ports first, then the minutes", "", "u")); return rows; }
            if (ev != "") { if (int.TryParse(ev, NumberStyles.None, CultureInfo.InvariantCulture, out mins) && mins >= 1) { if (!SetWatch(Math.Min(1440, mins), list ?? "")) rows.Add(R(2, "Watching", "couldn't save the schedule - the data folder can't be written", "", "i")); } else rows.Add(R(1, "Watching", "didn't understand \"" + every + "\" - type minutes (1-1440) or off; watching is unchanged", "", "u")); } var items = (list ?? "").Split(ListSep, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (items.Count == 0) { string[] pins; lock (Cfg.Pins) pins = Cfg.Pins.ToArray(); items = pins.Select(Security.HostOf).Where(x => x != null).Distinct().ToList(); }
            if (items.Count == 0) { rows.Add(R(3, "Service board", "type hosts and ports, e.g. nas.local:445, mail.example.com:587, 192.0.2.10:22", "Or pin sites in Investigate › Check a destination - they show here.", "u")); return rows; }
            int up = 0, n = 0;
            if (items.Count > 16) rows.Add(R(1, "Too many", (items.Count - 16) + " more not checked - the board takes 16 at a time", "", "u"));
            foreach (var it in items.Take(16))
            {
                if (run.Stop) break; string host; int port; if (!Split(it, 443, out host, out port)) { rows.Add(R(2, it, "not a host name or address", "", "u")); continue; }
                n++; run.Status = host + ":" + port + "...";
                var k = Knock(host, port, 5, run); if (run.Stop) { n--; break; }   // stopped mid-way: not a result, not logged as down, not counted (the tool run, 22 Nov: "0 of 3")
                Log(host + ":" + port, k, false);
                if (k.Ms.Count == 0) { rows.Add(R(2, host + ":" + port, "down - no answer to 5 tries", "", "m")); continue; }
                up++; int lv = k.Lost > 0 || k.Avg > 200 ? 1 : 0;
                rows.Add(R(lv, host + ":" + port, Ms(k.Avg) + " · min " + Ms(k.Min) + " · max " + Ms(k.Max) + (k.Lost > 0 ? " · " + k.Lost + " of 5 lost" : ""), "", "m"));
            }
            bool stopped = run.Stop;
            rows.Insert(0, R(stopped && n == 0 ? 3 : n == 0 ? 2 : up == n ? 0 : up == 0 ? 2 : 1, "Up right now", stopped ? (n == 0 ? "stopped before any answer" : up + " of the " + n + " checked before you stopped") : n == 0 ? "nothing checked - no valid host:port" : up + " of " + n, "TCP handshakes only - connect, time it, close.", "m"));
            int wm; string wl; Watch(out wm, out wl); rows.Add(R(3, "Watching", wm > 0 ? "every " + wm + " min while AdeeboMeter runs - see Uptime report" : "off - type a number of minutes in the second box to keep watching", "", "i"));
            return rows;
        }

        // ---------- streamers: is the upload ready for your bitrate?
        static readonly string[][] Ingest = { new[] { "twitch", "Twitch", "live.twitch.tv" }, new[] { "youtube", "YouTube", "a.rtmp.youtube.com" }, new[] { "kick", "Kick", "fa723fc1b171.global-contribute.live-video.net" } };
        public static List<string[]> GoLive(Monitor m, string bitrate, string service, Toolkit.Run run)
        {
            var rows = new List<string[]>(); int warn = 0; var ic = CultureInfo.InvariantCulture;
            // bug-hunt ML-102: empty means the usual 6000; anything else that isn't a bitrate is said, not swapped silently
            string bt = (bitrate ?? "").Trim(); double kbps;
            if (bt == "") kbps = 6000;
            else if (!double.TryParse(System.Text.RegularExpressions.Regex.Replace(bt, @"\s*(kbps|kb/s|kbit/s|k)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase), NumberStyles.Float, ic, out kbps) || double.IsNaN(kbps) || double.IsInfinity(kbps) || kbps <= 0 || kbps > 500000) { rows.Add(R(2, "Bitrate", "type the stream's bitrate in kbps, e.g. 6000", "", "u")); return rows; }   // R2-06: "NaN" and "Infinity" parse as numbers - not a bitrate
            string sv = (service ?? "").Trim().ToLowerInvariant(); var ing = Ingest.FirstOrDefault(z => sv == "" ? z[0] == "twitch" : z[0] == sv || string.Equals(z[1], sv, StringComparison.OrdinalIgnoreCase));
            string host = ing != null ? ing[2] : sv, name = ing != null ? ing[1] : sv;
            rows.AddRange(Pc(m, ref warn));
            var spd = SpeedTest.Last; double upl = spd != null && (DateTime.Now - spd.T).TotalHours < 24 && !spd.NoUpload ? spd.Up : double.NaN, need = kbps / 1000.0;
            if (double.IsNaN(upl)) { warn++; rows.Add(R(1, "Your upload", "not measured today", "Press Test my speed on the home page first - the check needs your real upload.", "u")); }
            else { bool tight = upl < need * 1.5; if (tight) warn++; rows.Add(R(upl < need ? 2 : tight ? 1 : 0, "Your upload", SpeedTest.Mbps(upl) + " Mbps (measured " + spd.T.ToString("HH:mm", CultureInfo.CurrentCulture) + ")", tight ? "Too little room - a stream needs about 1.5x its bitrate to stay smooth." : "", "m")); }
            double safe = double.IsNaN(upl) ? double.NaN : Math.Floor(upl * 0.7 * 10) / 10;
            rows.Add(R(double.IsNaN(safe) ? 3 : safe * 1000 < kbps ? 2 : 0, "Your bitrate", kbps.ToString("0", ic) + " kbps", "", "u"));
            if (!double.IsNaN(safe)) rows.Add(R(safe * 1000 < kbps ? 1 : 0, "Safe bitrate", (safe * 1000).ToString("0", ic) + " kbps", safe * 1000 < kbps ? "Lower OBS's bitrate to this, or test again on a cable." : "Your bitrate fits.", "i"));
            var s = m.Latest; bool busyUp = !double.IsNaN(s.Up) && s.Up > 1; if (busyUp) warn++;
            rows.Add(R(busyUp ? 1 : 0, "Upload in use now", SpeedTest.Mbps(s.Up) + " Mbps", busyUp ? "Something on this PC is already uploading (a backup, cloud sync) - it competes with the stream." : "", "w"));
            if (!Games.ValidHost(host)) { warn++; rows.Add(R(1, "Ingest server", "\"" + service + "\" isn't Twitch, YouTube, Kick or a host name - not checked", "", "u")); }
            else
            {
                run.Status = "Timing " + name + "'s ingest server...";
                var k = Knock(host, 1935, 5, run); bool bad = k.Ms.Count == 0, slow = k.Avg > 80 || k.Lost > 0; if (bad || slow) warn++;
                rows.Add(R(bad ? 2 : slow ? 1 : 0, name + " ingest", bad ? "no answer on RTMP 1935" : Ms(k.Avg) + " · jitter " + Ms(k.Jit) + (k.Lost > 0 ? " · " + k.Lost + " of 5 lost" : ""), bad ? "Blocked or down - try another ingest server in OBS (Settings > Stream > Server)." : "", "m"));
            }
            int worstLv = rows.Where(r => r.Length > 0 && r[0] == "2").Any() ? 2 : 1;   // bug-hunt L-206: red when something is red (an upload below the bitrate), not always amber
            rows.Insert(0, R(warn == 0 ? 0 : worstLv, "Go-live check", warn == 0 ? "ready - go live" : warn + " thing" + (warn == 1 ? "" : "s") + " to fix first", "AdeeboMeter only checks - it never changes OBS or closes anything.", "i"));
            return rows;
        }
    }
}
