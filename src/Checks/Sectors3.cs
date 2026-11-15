// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    // ================================================================== item 3 (Adeeb, 15 Nov): watch the board, the uptime report, the certificate
    // calendar, and the OBS link. The board's log (data\board-log.txt) holds one line per check: when, host:port, ms or "down".
    // Watching re-runs the board's list every N minutes while AdeeboMeter runs (data\board-watch.txt); nothing runs when it's closed.
    static partial class Sectors
    {
        static readonly object wk = new object(); static DateTime lastWatch = DateTime.MinValue; static volatile bool watching;
        static string WatchFile { get { return Path.Combine(Program.DataDir, "board-watch.txt"); } }
        static string LogFile { get { return Path.Combine(Program.DataDir, "board-log.txt"); } }
        public static void Watch(out int minutes, out string list)
        {
            minutes = 0; list = "";
            try { if (!File.Exists(WatchFile)) return; var l = File.ReadAllLines(WatchFile); if (l.Length > 0) int.TryParse(l[0], NumberStyles.None, CultureInfo.InvariantCulture, out minutes); if (l.Length > 1) list = l[1]; }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        // a file written whole: next to it first, then swapped in - a crash or a full disk never leaves half a file
        internal static void WriteWhole(string path, string text)
        {
            FullView.WriteSafe(path, text, false);
        }
        static void SetWatch(int minutes, string list)
        {
            try { if (minutes <= 0) { if (File.Exists(WatchFile)) File.Delete(WatchFile); } else WriteWhole(WatchFile, minutes.ToString(CultureInfo.InvariantCulture) + "\n" + (list ?? "").Replace("\n", " ") + "\n"); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        static void Log(string target, Knocks k) { Log(target, k, true); }
        static void Log(string target, Knocks k, bool auto)
        {
            lock (wk)
                try { File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\t" + target + "\t" + (k.Ms.Count == 0 ? "down" : Math.Round(k.Avg, 1).ToString(CultureInfo.InvariantCulture)) + "\t" + (auto ? "auto" : "manual") + "\n"); }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        // called once a minute from the UI timer: when the interval is up, one quiet background pass (3 tries per target)
        public static void WatchTick()
        {
            if (watching) return; int m; string list; Watch(out m, out list); if (m <= 0 || (DateTime.Now - lastWatch).TotalMinutes < m) return;
            lastWatch = DateTime.Now; watching = true;
            new Thread(() =>
            {
                try
                {
                    var items = list.Split(ListSep, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (items.Count == 0) { string[] pins; lock (Cfg.Pins) pins = Cfg.Pins.ToArray(); items = pins.Select(Security.HostOf).Where(x => x != null).Distinct().ToList(); }
                    var run = new Toolkit.Run();
                    foreach (var it in items.Take(16)) { int still; string sl; Watch(out still, out sl); if (still <= 0) break; string h; int p; if (Split(it, 443, out h, out p)) Log(h + ":" + p, Knock(h, p, 3, run)); }
                    Trim();
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("board watch: " + e.Message); }
                finally { watching = false; }
            }) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
        }
        // the log keeps 35 days
        static void Trim()
        {
            lock (wk)
                try
                {
                    if (!File.Exists(LogFile)) return; string cut = DateTime.Now.AddDays(-35).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    string first; using (var rd = new StreamReader(LogFile)) first = rd.ReadLine() ?? "";
                    if (string.CompareOrdinal(first, cut) >= 0 && new FileInfo(LogFile).Length < 2000000) return;
                    WriteWhole(LogFile, string.Join("\n", File.ReadAllLines(LogFile).Where(l => string.CompareOrdinal(l, cut) >= 0).ToArray()) + "\n");
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        // ---------- servers: the uptime report from the board's log
        public static List<string[]> Uptime(string days)
        {
            var rows = new List<string[]>(); int d; if (!int.TryParse((days ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out d) || d < 1) d = 7; d = Math.Min(35, d);
            var since = DateTime.Now.AddDays(-d); var by = new Dictionary<string, List<Tuple<DateTime, double>>>(StringComparer.OrdinalIgnoreCase);
            lock (wk)
                try
                {
                    if (File.Exists(LogFile))
                        foreach (var l in File.ReadAllLines(LogFile))
                        {
                            var p = l.Split(Tab); DateTime t; if (p.Length < 3 || !DateTime.TryParseExact(p[0], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) || t < since) continue;
                            double ms; if (p[2] == "down") ms = double.NaN; else if (!double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out ms)) continue;
                            List<Tuple<DateTime, double>> x; if (!by.TryGetValue(p[1], out x)) by[p[1]] = x = new List<Tuple<DateTime, double>>(); x.Add(Tuple.Create(t, ms));
                        }
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            int wm; string wl; Watch(out wm, out wl);
            if (by.Count == 0) { rows.Add(R(3, "Uptime report", "no checks recorded yet", "Run the Service board with a number of minutes in its second box - it keeps watching while AdeeboMeter runs and logs every check here.", "u")); return rows; }
            rows.Add(R(3, "Period", "last " + d + " day" + (d == 1 ? "" : "s") + " · " + by.Values.Sum(v => v.Count) + " checks" + (wm > 0 ? " · watching every " + wm + " min" : " · watching is off"), "", "m"));
            foreach (var kv in by.OrderBy(z => z.Key, StringComparer.OrdinalIgnoreCase))
            {
                var ok = kv.Value.Where(z => !double.IsNaN(z.Item2)).Select(z => z.Item2).OrderBy(z => z).ToList(); double up = 100.0 * ok.Count / kv.Value.Count;
                var lastDown = kv.Value.LastOrDefault(z => double.IsNaN(z.Item2));
                string p50 = ok.Count > 0 ? Math.Round(ok.Count % 2 == 1 ? ok[ok.Count / 2] : (ok[ok.Count / 2 - 1] + ok[ok.Count / 2]) / 2).ToString(CultureInfo.InvariantCulture) : "-", p95 = ok.Count > 0 ? Math.Round(ok[Math.Min(ok.Count - 1, (int)(ok.Count * 0.95))]).ToString(CultureInfo.InvariantCulture) : "-";
                rows.Add(R(up >= 99.9 ? 0 : up >= 99 ? 1 : 2, kv.Key, (ok.Count == kv.Value.Count ? "100" : Math.Min(99.99, Math.Floor(up * 100) / 100).ToString(up >= 99.95 ? "0.00" : "0.0", CultureInfo.InvariantCulture)) + "% up · " + kv.Value.Count + " checks · p50 " + p50 + " ms · p95 " + p95 + " ms" + (lastDown != null ? " · last down " + lastDown.Item1.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture) : ""), "", "m"));
            }
            return rows;
        }

        // ---------- servers: every certificate you watch, and the days it has left
        public static List<string[]> CertCalendar(string list, Toolkit.Run run)
        {
            var rows = new List<string[]>(); var items = (list ?? "").Split(ListSep, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (items.Count == 0)
            {
                string[] pins; lock (Cfg.Pins) pins = Cfg.Pins.ToArray(); items.AddRange(pins.Select(Security.HostOf).Where(x => x != null));
                int m; string wl; Watch(out m, out wl); items.AddRange(wl.Split(ListSep, StringSplitOptions.RemoveEmptyEntries));
                try { string dir = Path.Combine(Program.DataDir, "servers"); if (Directory.Exists(dir)) items.AddRange(Directory.GetFiles(dir, "*.txt").Select(f => Path.GetFileNameWithoutExtension(f))); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            var hosts = items.Select(i => { string h; int p; return Split(i, 443, out h, out p) ? (p == 443 ? h : h + ":" + p.ToString(CultureInfo.InvariantCulture)) : null; }).Where(h => h != null).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
            if (hosts.Count == 0) { rows.Add(R(3, "Certificate calendar", "type the sites to watch, e.g. mail.example.com, vpn.example.com", "Or pin sites, or add them to the Service board - they show here by themselves.", "u")); return rows; }
            var got = new List<Tuple<string, int, string>>();
            foreach (var h in hosts)
            {
                if (run.Stop) break; run.Status = h + "..."; string hh; int hp; Split(h, 443, out hh, out hp); string iss, exp, fp; DateTime na; Cert(hh, hp, out iss, out exp, out fp, out na);
                if (na == DateTime.MinValue) { rows.Add(R(3, h, "no certificate read (no TLS on port " + hp + ")", "", "m")); continue; }
                var left = na - DateTime.Now; int days = left.Ticks < 0 ? -(int)Math.Ceiling(-left.TotalDays) : (int)Math.Floor(left.TotalDays);   // expired an hour ago = expired, not "0 days left"
                got.Add(Tuple.Create(h, left.Ticks < 0 && days == 0 ? -1 : days, iss));
            }
            foreach (var g in got.OrderBy(z => z.Item2))
                rows.Insert(rows.Count - rows.Count(r => r[2].StartsWith("no certificate", StringComparison.Ordinal)), R(g.Item2 < 0 ? 2 : g.Item2 < 14 ? 2 : g.Item2 < 30 ? 1 : 0, g.Item1, (g.Item2 < 0 ? "expired" + (g.Item2 <= -2 ? " " + (-g.Item2) + " days ago" : "") : g.Item2 == 0 ? "expires today" : "expires in " + g.Item2 + " days") + " · " + g.Item3, g.Item2 < 30 && g.Item2 >= 0 ? "Renew it now - automatic renewals usually run 30 days ahead; if this one didn't, check why." : "", "m"));
            return rows;
        }

        // ---------- streamers: OBS on this PC, through its own websocket (Tools > WebSocket Server Settings in OBS) - AdeeboMeter
        // connects out to 127.0.0.1:4455 as a client and only asks; it never starts, stops or changes anything in OBS.
        public static List<string[]> Obs(Monitor m, string password, string port, Toolkit.Run run)
        {
            var rows = new List<string[]>(); int p; if (!int.TryParse((port ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out p) || p <= 0 || p > 65535) p = 4455;
            using (var ws = new ClientWebSocket())
            {
                try
                {
                    run.Status = "Connecting to OBS on this PC..."; var cts = new CancellationTokenSource(6000);
                    ws.ConnectAsync(new Uri("ws://127.0.0.1:" + p.ToString(CultureInfo.InvariantCulture)), cts.Token).Wait();
                    string hello = Recv(ws, run); if (!Op(hello, 0)) throw new InvalidOperationException("not OBS");
                    string auth = "";
                    var ch = Regex.Match(hello, "\"challenge\"\\s*:\\s*\"([^\"]+)\""); var sa = Regex.Match(hello, "\"salt\"\\s*:\\s*\"([^\"]+)\"");
                    if (ch.Success && sa.Success)
                    {
                        if (string.IsNullOrEmpty(password)) { rows.Add(R(1, "OBS", "asks for a password", "Type the password from OBS > Tools > WebSocket Server Settings in the first box. It isn't saved.", "u")); return rows; }
                        using (var sha = SHA256.Create()) { string secret = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(password + sa.Groups[1].Value))); auth = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(secret + ch.Groups[1].Value))); }
                    }
                    Send(ws, "{\"op\":1,\"d\":{\"rpcVersion\":1" + (auth != "" ? ",\"authentication\":\"" + auth + "\"" : "") + ",\"eventSubscriptions\":0}}");
                    string id = Recv(ws, run); if (!Op(id, 2)) { rows.Add(R(2, "OBS", "refused the password", "Check it in OBS > Tools > WebSocket Server Settings > Show connect info.", "u")); return rows; }
                    run.Status = "Reading OBS's stream and stats...";
                    string s1 = Ask(ws, "GetStreamStatus", "1", run), st1 = Ask(ws, "GetStats", "2", run); for (int w = 0; w < 30 && !run.Stop; w++) Thread.Sleep(100); string s2 = Ask(ws, "GetStreamStatus", "3", run);
                    if (run.Stop) { rows.Add(R(3, "OBS link", "stopped", "", "i")); return rows; }
                    if (!Ok(s1) || !Ok(st1) || !Ok(s2)) { rows.Add(R(1, "OBS link", "connected, but OBS didn't return its numbers", "Update OBS to 28 or newer, then run it again.", "u")); return rows; }
                    bool live = Regex.IsMatch(s1, "\"outputActive\"\\s*:\\s*true");
                    double cpu = J(st1, "cpuUsage"), fps = J(st1, "activeFps"), rLag = J(st1, "renderSkippedFrames"), rTot = J(st1, "renderTotalFrames"), eLag = J(st1, "outputSkippedFrames"), eTot = J(st1, "outputTotalFrames");
                    double sk = J(s2, "outputSkippedFrames"), tot = J(s2, "outputTotalFrames"), cong = J(s2, "outputCongestion"), b1 = J(s1, "outputBytes"), b2 = J(s2, "outputBytes");
                    double kbps = !double.IsNaN(b1) && !double.IsNaN(b2) && b2 > b1 ? (b2 - b1) * 8 / 3.0 / 1000 : double.NaN;
                    int warn = 0; var s = m.Latest;
                    rows.Add(R(live ? 0 : 3, "Streaming", live ? "live" + (!double.IsNaN(kbps) ? " · sending " + Math.Round(kbps) + " kbps right now" : "") : "not live right now (the numbers below are OBS's since it started)", "", "m"));
                    double netDrop = tot > 0 ? 100 * sk / tot : double.NaN; bool nd = netDrop >= 1; if (nd) warn++;
                    rows.Add(R(double.IsNaN(netDrop) ? 3 : nd ? 2 : 0, "Dropped (network)", double.IsNaN(netDrop) ? "-" : netDrop.ToString("0.0", CultureInfo.InvariantCulture) + "% of frames" + (!double.IsNaN(cong) ? " · congestion " + Math.Round(cong * 100) + "%" : ""), nd ? "The upload can't keep up: lower the bitrate (Ready to stream? suggests a safe one), use a cable, or pause uploads." : "", "m"));
                    double encDrop = eTot > 0 ? 100 * eLag / eTot : double.NaN; bool ed = encDrop >= 1; if (ed) warn++;
                    rows.Add(R(double.IsNaN(encDrop) ? 3 : ed ? 1 : 0, "Skipped (encoding)", double.IsNaN(encDrop) ? "-" : encDrop.ToString("0.0", CultureInfo.InvariantCulture) + "% of frames", ed ? "The encoder can't keep up - not the network: use the GPU encoder (NVENC, QuickSync, AMF) or a faster preset." : "", "m"));
                    double renDrop = rTot > 0 ? 100 * rLag / rTot : double.NaN; bool rd = renDrop >= 1; if (rd) warn++;
                    rows.Add(R(double.IsNaN(renDrop) ? 3 : rd ? 1 : 0, "Lagged (rendering)", double.IsNaN(renDrop) ? "-" : renDrop.ToString("0.0", CultureInfo.InvariantCulture) + "% of frames" + (!double.IsNaN(fps) ? " · " + Math.Round(fps) + " fps" : ""), rd ? "The GPU is busy (often the game): cap the game's frame rate, or lower OBS's canvas." : "", "m"));
                    rows.Add(R(!double.IsNaN(cpu) && cpu > 80 ? 1 : 3, "OBS's CPU", double.IsNaN(cpu) ? "-" : Math.Round(cpu) + "%", "", "m"));
                    rows.Add(R(3, "Your network right then", "upload " + SpeedTest.Mbps(s.Up) + " Mbps · router " + (double.IsNaN(s.GwMs) ? "-" : Math.Round(s.GwMs) + " ms") + " · internet " + Math.Round(s.Ping) + " ms", "", "m"));
                    bool known = !double.IsNaN(netDrop) || !double.IsNaN(encDrop) || !double.IsNaN(renDrop);
                    rows.Insert(0, R(!known ? 3 : warn == 0 ? 0 : 1, "OBS link", !known ? "connected, but no frame counts yet - start streaming or recording, then run it again" : warn == 0 ? "connected - no drops worth worrying about" : warn + " thing" + (warn == 1 ? "" : "s") + " dropping frames - each has its cause below", "Only asks OBS; never starts, stops or changes anything.", "i"));
                }
                catch (Exception e)
                {
                    if (e is OutOfMemoryException) throw;
                    rows.Clear(); if (run.Stop) { rows.Add(R(3, "OBS link", "stopped", "", "i")); return rows; }
                    rows.Add(R(1, "OBS link", "can't reach OBS on this PC (port " + p + ")", "Open OBS, then Tools > WebSocket Server Settings > Enable WebSocket server. OBS 28 or newer has it built in.", "u"));
                }
            }
            return rows;
        }
        // whitespace-tolerant JSON checks: "op": 2 and "op":2 are the same message
        static bool Op(string json, int op) { return Regex.IsMatch(json ?? "", "\"op\"\\s*:\\s*" + op.ToString(CultureInfo.InvariantCulture) + "\\b"); }
        static bool Ok(string json) { return Regex.IsMatch(json ?? "", "\"result\"\\s*:\\s*true"); }
        static string Recv(ClientWebSocket ws, Toolkit.Run run)
        {
            var buf = new byte[65536]; var sb = new StringBuilder(); WebSocketReceiveResult r;
            do { if (run.Stop) throw new OperationCanceledException(); Task<WebSocketReceiveResult> t; using (var cts = new CancellationTokenSource(5000)) { t = ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token); while (!t.Wait(100)) if (run.Stop) { cts.Cancel(); throw new OperationCanceledException(); } r = t.Result; } sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count)); } while (!r.EndOfMessage);
            return sb.ToString();
        }
        static void Send(ClientWebSocket ws, string json) { var b = Encoding.UTF8.GetBytes(json); ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, new CancellationTokenSource(5000).Token).Wait(); }
        static string Ask(ClientWebSocket ws, string type, string id, Toolkit.Run run) { Send(ws, "{\"op\":6,\"d\":{\"requestType\":\"" + type + "\",\"requestId\":\"" + id + "\"}}"); for (int i = 0; i < 5; i++) { string r = Recv(ws, run); if (Regex.IsMatch(r, "\"requestId\"\\s*:\\s*\"" + id + "\"")) return r; } return ""; }
        static double J(string json, string key) { var mm = Regex.Match(json ?? "", "\"" + key + "\"\\s*:\\s*(-?[0-9.eE+-]+)"); double v; return mm.Success && double.TryParse(mm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }
    }
}
