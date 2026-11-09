// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace AdeeboMeter
{
    // The engineer's toolbox: iperf3 client, sustained ping, dig (+ follow the delegation), HTTP / TLS inspector,
    // nearest cloud, ping sweep, Wake-on-LAN, subnet calculator. All on press, all outbound, nothing listens.
    static class Toolkit
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        public sealed class Run { public volatile string Status = ""; public volatile bool Stop; public DateTime Deadline; public readonly List<double> Series = new List<double>(); public string SeriesUnit = ""; public void Add(double v) { lock (Series) Series.Add(v); } public List<double> Copy() { lock (Series) return Series.ToList(); } }

        // ================================================================== iperf3 client
        const int Cookie = 37;
        // iperf3 options, the same spelling as iperf3 itself: -R -P n -t n -u -b 50M -l 1400 -w 256K -S 0xB8 -O 2 -M 1400 -p 5201 -4 -6, plus --both
        public sealed class Opt { public bool Udp, Reverse, Both, V6, V4; public int Par = 1, Secs = 10, Omit, Mss, Tos, Port = 5201; public long Rate = 50000000, Len, Window; public List<string> Ignored = new List<string>(); }
        public static Opt ParseOpts(string text)
        {
            // attached forms iperf3 also accepts: -t20, --time=20, -O3, --omit=3, -P4, -b50M ... become "-t 20" and so on
            text = System.Text.RegularExpressions.Regex.Replace(" " + (text ?? "") + " ", @"(?<=\s)(--[a-z-]+)=(\S+)(?=\s)", "$1 $2");
            // capitals first: the switch below lower-cases, and -P (streams) is not -p (port)
            foreach (var cap in new[] { new[] { "P", "--parallel" }, new[] { "O", "--omit" }, new[] { "S", "--tos" }, new[] { "M", "--set-mss" } })
                text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<=\s)-" + cap[0] + @"(\S+)(?=\s)", cap[1] + " $1");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<=\s)-([tblwp])(\S+)(?=\s)", "-$1 $2");
            var o = new Opt(); var t = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).ToList();
            Func<string, long> size = v => { v = v.Trim().ToUpperInvariant(); long mul = v.EndsWith("G", StringComparison.Ordinal) ? 1000000000 : v.EndsWith("M", StringComparison.Ordinal) ? 1000000 : v.EndsWith("K", StringComparison.Ordinal) ? 1000 : 1; double d; return double.TryParse(v.TrimEnd('G', 'M', 'K'), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? (long)(d * mul) : -1; };
            for (int i = 0; i < t.Count; i++)
            {
                string a = t[i].ToLowerInvariant(), v = i + 1 < t.Count ? t[i + 1] : ""; int n;
                switch (a)
                {
                    case "-r": case "--reverse": case "down": o.Reverse = true; break;
                    case "up": o.Reverse = false; break;
                    case "--both": case "both": o.Both = true; break;
                    case "-u": case "--udp": case "udp": o.Udp = true; break;
                    case "-4": o.V4 = true; break; case "-6": o.V6 = true; break;
                    case "x4": o.Par = 4; break;
                    case "-p": case "--port": if (int.TryParse(v, out n) && n > 0 && n < 65536) { o.Port = n; i++; } break;
                    case "--parallel": if (int.TryParse(v, out n)) { o.Par = Math.Max(1, Math.Min(16, n)); i++; } break;
                    case "-t": case "--time": if (int.TryParse(v, out n)) { o.Secs = Math.Max(1, Math.Min(120, n)); i++; } break;
                    case "-o": case "--omit": if (int.TryParse(v, out n)) { o.Omit = Math.Max(0, Math.Min(10, n)); i++; } break;
                    case "-m": case "--set-mss": if (int.TryParse(v, out n)) { o.Mss = Math.Max(0, Math.Min(9000, n)); i++; } break;
                    case "-b": case "--bitrate": case "--bandwidth": { long r = size(v); if (r > 0) { o.Rate = Math.Min(r, 10000000000L); i++; } break; }   // at most 10 Gb/s - a typo can't flood the link
                    case "-l": case "--length": { long r = size(v); if (r > 0) { o.Len = Math.Min(r, 1 << 20); i++; } break; }
                    case "-w": case "--window": { long r = size(v); if (r > 0) { o.Window = Math.Min(r, 16 << 20); i++; } break; }
                    case "--tos": if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? int.TryParse(v.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n) : int.TryParse(v, out n)) { o.Tos = n & 255; i++; } break;
                    default:
                        var m = System.Text.RegularExpressions.Regex.Match(a, @"^(\d+)m$");
                        long mr; if (m.Success && o.Udp && long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out mr) && mr > 0 && mr <= 10000) o.Rate = mr * 1000000; else o.Ignored.Add(t[i]);   // same 10 Gb/s cap as -b
                        break;
                }
            }
            return o;
        }
        public static List<string[]> Iperf(string server, string options, Run run)
        {
            var rows = new List<string[]>(); string host = (server ?? "").Trim();
            // -P is parallel and -p is the port: switch() lower-cases, so read -P separately
            var raw = (options ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).ToList();
            var fixedOpts = string.Join(" ", raw.Select(x => x == "-P" ? "--parallel" : x == "-S" ? "--tos" : x == "-R" ? "--reverse" : x == "-O" ? "--omit" : x == "-M" ? "--set-mss" : x));
            var o = ParseOpts(fixedOpts);
            int c = host.LastIndexOf(':'); if (c > 0 && host.IndexOf(':') == c) { int pp; if (int.TryParse(host.Substring(c + 1), out pp) && pp > 0 && pp < 65536) o.Port = pp; else { rows.Add(R(2, "Server", "port must be 1-65535", "", "u")); return rows; } host = host.Substring(0, c); }
            if (host == "") { rows.Add(R(2, "Server", "type your iperf3 server, e.g. 10.0.0.5 or lab.example.com:5201", "On the other machine run:  iperf3 -s   (AdeeboMeter is only the client - it never listens)", "u")); return rows; }
            run.SeriesUnit = "Mb/s";
            rows.Add(R(3, "Settings", (o.Udp ? "UDP at " + (o.Rate / 1e6).ToString("0.#", CultureInfo.CurrentCulture) + " Mb/s" : "TCP") + " · " + o.Par + " stream" + (o.Par == 1 ? "" : "s") + " · " + o.Secs + " s" + (o.Omit > 0 ? " (+" + o.Omit + " s warm-up not counted)" : "") + (o.Len > 0 ? " · " + o.Len + "-byte writes" : "") + (o.Window > 0 ? " · window " + (o.Window / 1024) + " KB" : "") + (o.Mss > 0 ? " · MSS " + o.Mss : "") + (o.Tos > 0 ? " · TOS 0x" + o.Tos.ToString("X2", CultureInfo.InvariantCulture) : "") + " · port " + o.Port,
                o.Ignored.Count > 0 ? "Not understood and ignored: " + string.Join(" ", o.Ignored) + " (supported: -R -P -t -u -b -l -w -S -O -M -p -4 -6 --both)" : o.Tos > 0 ? "Windows usually ignores DSCP / TOS set by a program unless a QoS policy allows it - check with a capture." : "", "i"));
            foreach (var rev in o.Both ? new[] { false, true } : new[] { o.Reverse })
            {
                run.Status = (rev ? "Download" : "Upload") + " test to " + host + "...";
                try { rows.AddRange(IperfOne(host, o, rev, run)); }
                catch (Exception e) { var ie = e is AggregateException && e.InnerException != null ? e.InnerException : e; rows.Add(R(2, rev ? "Download" : "Upload", "failed: " + Draw.Trunc(ie.Message.Replace("\r", " ").Replace("\n", " ").Trim(), 90), "Is  iperf3 -s  running there, and is TCP " + o.Port + " allowed through its firewall?", "u")); }
                if (run.Stop) break;
            }
            return rows;
        }
        // ---------- DSCP ping: marked and unmarked pings in turn, so the difference is the marking and not the minute
        static readonly Dictionary<string, int> Dscp = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "EF", 46 }, { "AF41", 34 }, { "AF42", 36 }, { "AF31", 26 }, { "AF21", 18 }, { "AF11", 10 }, { "CS1", 8 }, { "CS2", 16 }, { "CS3", 24 }, { "CS4", 32 }, { "CS5", 40 }, { "CS6", 48 }, { "CS7", 56 }, { "BE", 0 } };
        public static List<string[]> DscpPing(string target, string dscp, string count, Run run)
        {
            var rows = new List<string[]>(); var dst = Icmp.Resolve4(target); if (dst == null) { rows.Add(R(2, "Target", "couldn't find " + target + " (IPv4)", "", "u")); return rows; }
            int d; string dn = (dscp ?? "").Trim(); if (!Dscp.TryGetValue(dn, out d) && !(int.TryParse(dn, out d) && d >= 0 && d <= 63)) { rows.Add(R(2, "DSCP", "use a name (EF, AF41, CS6...) or 0-63", "", "u")); return rows; }
            int n; if (!int.TryParse(count, out n)) n = 20; n = Math.Max(2, Math.Min(200, n));
            var mk = new List<double>(); var pl = new List<double>(); run.SeriesUnit = "ms";
            for (int i = 0; i < n && !run.Stop; i++)
            {
                bool marked = i % 2 == 0; var sw = Stopwatch.StartNew();
                var r = Icmp.Echo(dst, 64, 128, false, 1000, false, marked ? (byte)(d << 2) : (byte)0); double v = r.Status == Icmp.Ok ? Math.Max(r.Rtt, 0.5) : double.NaN;
                (marked ? mk : pl).Add(v); if (marked) run.Add(v); run.Status = "Ping " + (i + 1) + " / " + n + (marked ? " (DSCP " + d + ")" : " (unmarked)");
                int w = 500 - (int)sw.ElapsedMilliseconds; if (w > 0) Thread.Sleep(w);
            }
            Func<List<double>, string> sum = l => { var ok = l.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList(); return ok.Count == 0 ? "no replies" : "median " + ok[ok.Count / 2] + " ms · " + (l.Count - ok.Count) + " of " + l.Count + " lost"; };
            string name = Dscp.FirstOrDefault(x => x.Value == d).Key;
            rows.Add(R(mk.Count(double.IsNaN) > pl.Count(double.IsNaN) + 1 ? 1 : 0, "DSCP " + d + (name != null ? " (" + name + ")" : ""), sum(mk), "", "m"));
            rows.Add(R(0, "Unmarked (0)", sum(pl), "", "m"));
            rows.Add(R(3, "Was it really marked?", "only a capture can say", "Windows clears the marking a program asks for unless a QoS policy (Group Policy > Policy-based QoS) allows it. To check: Live capture with filter  icmp , look at the DSCP field of the request. Marked pings that are dropped while unmarked ones pass mean something on the way polices that class.", "i"));
            return rows;
        }

        // ---------- latency under load (bufferbloat): ping while your own iperf3 server fills the line, one direction at a time
        public static List<string[]> Loaded(string server, string target, Run run)
        {
            var rows = new List<string[]>(); if ((server ?? "").Trim() == "") { rows.Add(R(2, "Server", "type your iperf3 server", "It has to be on the far side of the link you want to test - for your internet line, a server out on the internet (iperf3 -s).", "u")); return rows; }
            // ping target: the one you typed; otherwise the iperf3 server itself (its queue is the one being filled), or 1.1.1.1 if it doesn't answer pings
            bool typed = !string.IsNullOrWhiteSpace(target);
            string srvHost = server.Trim(); int sc = srvHost.LastIndexOf(':'); if (sc > 0 && srvHost.IndexOf(':') == sc) srvHost = srvHost.Substring(0, sc);
            var dst = Icmp.Resolve4(typed ? target : srvHost); if (dst == null && typed) { rows.Add(R(2, "Ping target", "couldn't find " + target, "", "u")); return rows; }
            run.SeriesUnit = "ms";
            Func<int, Func<bool>, List<double>> pings = (max, stop) =>
            {
                var l = new List<double>();
                for (int i = 0; i < max && !stop() && !run.Stop; i++) { if (dst == null) break; var sw = Stopwatch.StartNew(); var r = Icmp.Echo(dst, 32, 128, false, 1000, false); double v = r.Status == Icmp.Ok ? Math.Max(r.Rtt, 0.5) : double.NaN; l.Add(v); run.Add(v); int w = 250 - (int)sw.ElapsedMilliseconds; if (w > 0) Thread.Sleep(w); }
                return l;
            };
            Func<List<double>, double> med = l => { var v = l.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList(); return v.Count == 0 ? double.NaN : v[v.Count / 2]; };
            run.Status = "Idle latency to " + dst + "...";
            double idle = med(pings(20, () => false));
            // the server doesn't answer pings: pinging somewhere else could measure a different path (lab server, split-tunnel VPN) - say so instead
            if (double.IsNaN(idle) && !typed) { rows.Add(R(1, "Bufferbloat grade", "inconclusive - " + srvHost + " doesn't answer pings", "Type a ping target that sits beyond the same link (for your internet line: e.g. 1.1.1.1) and run it again.", "i")); return rows; }
            if (double.IsNaN(idle)) { rows.Add(R(2, "Idle", dst + " doesn't answer pings - pick another target", "", "m")); return rows; }
            rows.Add(R(0, "Idle", idle.ToString("0", CultureInfo.CurrentCulture) + " ms to " + dst, "", "m"));
            double worst = 0; bool loaded = true, lost = false;
            foreach (bool down in new[] { true, false })
            {
                if (run.Stop) break;
                run.Status = (down ? "Download" : "Upload") + " load + ping...";
                bool done = false; List<double> during = null; var load = new Run();   // its own Run: iperf3's Mb/s stay out of this chart in ms
                var pt = new Thread(() => { Thread.Sleep(1500); during = pings(400, () => { if (run.Stop) load.Stop = true; return done; }); }) { IsBackground = true, Name = "bloat ping" }; pt.Start();   // skip TCP's first second-and-a-half (slow start)
                List<string[]> ip; try { ip = Iperf(server, (down ? "-R" : "") + " -t 10 -P 4", load); } finally { done = true; }
                pt.Join(3000);
                List<double> got = pt.IsAlive ? null : during;   // a ping thread that hasn't finished is never read while it writes
                string want = down ? "Download" : "Upload";
                var thr = ip.FirstOrDefault(x => x[0] == "0" && x[1].StartsWith(want, StringComparison.Ordinal) && x[2].Contains("Mb/s"));   // IsAlive below is what keeps the list safe
                if (thr == null)
                {
                    // no load, no verdict: pings on an idle line would grade it A for nothing
                    var why = ip.FirstOrDefault(x => x[0] == "2"); loaded = false;
                    rows.Add(R(2, "During " + want.ToLowerInvariant(), "no load - " + (why != null ? Draw.Trunc(why[2], 90) : run.Stop ? "stopped" : "iperf3 gave no result"), why != null ? why[3] : "", "m"));
                    continue;
                }
                int sent = got != null ? got.Count : 0, okN = got != null ? got.Count(x => !double.IsNaN(x)) : 0;
                double ld = okN >= Math.Max(5, sent / 2) ? med(got) : double.NaN, add = ld - idle;   // under half the pings back: no delay figure is trustworthy
                if (!double.IsNaN(add)) worst = Math.Max(worst, add); else lost = true;
                string lossTxt = sent > 0 ? " · " + (sent - okN) + " of " + sent + " pings lost" : "";
                rows.Add(R(double.IsNaN(add) ? 2 : add > 200 ? 2 : add > 60 ? 1 : 0, "During " + want.ToLowerInvariant(), (double.IsNaN(ld) ? "too few pings came back to measure" : ld.ToString("0", CultureInfo.CurrentCulture) + " ms (+" + Math.Max(0, add).ToString("0", CultureInfo.CurrentCulture) + " ms)") + lossTxt + " · " + thr[2], "", "m"));
            }
            if (!loaded || run.Stop) { rows.Insert(0, R(1, "Bufferbloat grade", run.Stop ? "stopped - no grade" : "no grade - the line was never loaded in both directions", "The grade needs iperf3 to fill the line; see the row below for why it didn't.", "i")); return rows; }
            if (lost) { rows.Insert(0, R(2, "Bufferbloat grade", "inconclusive - most pings were lost while the line was full, so no delay could be measured", "Losing every ping under load is worse than any grade: the router's queue drops or the line can't carry the load. Try a lower load (iperf3 -b) or SQM on the router.", "i")); return rows; }
            string grade = worst < 5 ? "A+" : worst < 30 ? "A" : worst < 60 ? "B" : worst < 200 ? "C" : worst < 400 ? "D" : "F";
            rows.Insert(0, R(worst >= 200 ? 2 : worst >= 60 ? 1 : 0, "Bufferbloat grade", grade + " · latency rises up to " + worst.ToString("0", CultureInfo.CurrentCulture) + " ms when the line is full",
                (worst >= 60 ? "Calls and games stutter while someone uploads or downloads. The fix is on the router: turn on SQM / Smart Queue / fq_codel / cake, set a little under your real speed." : "Busy line or not, calls stay smooth.")
                + " The grade assumes iperf3 filled the line: if the speeds below are well under what you pay for, the server or this PC was the limit and the line wasn't really full.", "i"));
            return rows;
        }
        static List<string[]> IperfOne(string host, Opt o, bool reverse, Run run)
        {
            bool udp = o.Udp; int port = o.Port, par = o.Par, secs = o.Secs + o.Omit; long udpRate = o.Rate; int len = (int)(o.Len > 0 ? o.Len : udp ? 1400 : 131072);
            var rows = new List<string[]>();
            var addr = Dns.GetHostAddresses(host).First(a => o.V6 ? a.AddressFamily == AddressFamily.InterNetworkV6 : o.V4 ? a.AddressFamily == AddressFamily.InterNetwork : a.AddressFamily == AddressFamily.InterNetwork || a.AddressFamily == AddressFamily.InterNetworkV6);
            var rnd = new byte[36]; using (var g = RandomNumberGenerator.Create()) g.GetBytes(rnd);
            var cookie = new byte[Cookie]; const string abc = "abcdefghijklmnopqrstuvwxyz234567"; for (int i = 0; i < 36; i++) cookie[i] = (byte)abc[rnd[i] % 32];
            using (var ctl = new TcpClient(addr.AddressFamily))
            {
                ctl.ReceiveTimeout = (secs + 20) * 1000; ctl.SendTimeout = 10000;
                if (!ctl.ConnectAsync(addr, port).Wait(5000)) throw new TimeoutException("no answer on TCP " + port);
                var cs = ctl.GetStream(); cs.Write(cookie, 0, Cookie);
                var streams = new List<Socket>(); long[] bytes = new long[1]; long pkts = 0, lost = 0; double jit = 0; var sw = new Stopwatch(); var threads = new List<Thread>();
                long atOmit = 0, jitN = 0; double jitSum = 0; var lockU = new object();
                // Stop (or a test that runs far past its length) closes every socket - that breaks any read or write that is waiting
                bool finished = false; var deadline = DateTime.UtcNow.AddSeconds(secs + 60);
                var guard = new Thread(() => { while (!finished) { if (run.Stop || DateTime.UtcNow > deadline) { try { ctl.Close(); } catch { } lock (streams) foreach (var s in streams) try { s.Close(); } catch { } return; } Thread.Sleep(200); } }) { IsBackground = true, Name = "iperf guard" };
                guard.Start();
                try
                {
                for (; ; )
                {
                    int st = cs.ReadByte(); if (st < 0) throw new InvalidOperationException("server closed the connection");
                    if (st == 9)   // PARAM_EXCHANGE
                    {
                        string js = "{" + (udp ? "\"udp\":true,\"bandwidth\":" + udpRate + "," : "\"tcp\":true,") + "\"len\":" + len + "," + (o.Window > 0 ? "\"window\":" + o.Window + "," : "") + (o.Mss > 0 ? "\"MSS\":" + o.Mss + "," : "") + (o.Tos > 0 ? "\"TOS\":" + o.Tos + "," : "") + "\"omit\":" + o.Omit + ",\"time\":" + o.Secs + ",\"num\":0,\"blockcount\":0,\"parallel\":" + par + (reverse ? ",\"reverse\":true" : "") + ",\"pacing_timer\":1000,\"client_version\":\"3.16\"}";
                        SendJson(cs, js);
                    }
                    else if (st == 10)   // CREATE_STREAMS
                    {
                        for (int i = 0; i < par; i++)
                        {
                            if (udp)
                            {
                                if (run.Stop) break;
                                var s = new Socket(addr.AddressFamily, SocketType.Dgram, ProtocolType.Udp); Tune(s, o); s.Connect(addr, port); s.ReceiveTimeout = 3000;
                                s.Send(new byte[] { 0x39, 0x38, 0x37, 0x36 });   // UDP_CONNECT_MSG 0x36373839, sent in the server's (little-endian) byte order
                                var rb = new byte[4]; int rn; try { rn = s.Receive(rb); } catch (SocketException) { throw new InvalidOperationException("UDP to the server is blocked (no reply on UDP " + port + ")"); }
                                bool okReply = rn == 4 && ((rb[0] == 0x36 && rb[1] == 0x37 && rb[2] == 0x38 && rb[3] == 0x39) || (rb[0] == 0xB1 && rb[1] == 0x68 && rb[2] == 0xDE && rb[3] == 0x3A));   // UDP_CONNECT_REPLY 0x39383736, or the legacy 987654321
                                if (!okReply) throw new InvalidOperationException("the server answered the UDP setup with something unexpected");
                                lock (streams) streams.Add(s);
                            }
                            else
                            {
                                if (run.Stop) break;
                                var s = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp); Tune(s, o);
                                var ca = s.BeginConnect(addr, port, null, null); if (!ca.AsyncWaitHandle.WaitOne(5000)) { s.Close(); throw new TimeoutException("stream " + (streams.Count + 1) + " couldn't connect in 5 s"); }
                                s.EndConnect(ca); s.Send(cookie); lock (streams) streams.Add(s);
                            }
                        }
                    }
                    else if (st == 1) { }   // TEST_START
                    else if (st == 2)   // TEST_RUNNING: go
                    {
                        sw.Start(); var end = DateTime.UtcNow.AddSeconds(secs);
                        foreach (var s0 in streams)
                        {
                            var s = s0;
                            var t = new Thread(() =>
                            {
                                var buf = new byte[len]; long seq = 0; var pace = Stopwatch.StartNew(); double perStream = udpRate / 8.0 / streams.Count;
                                long myMax = 0, myPk = 0, myLost = 0; double myJit = 0, myLast = double.NaN;   // each stream counts its own sequence numbers (iperf3 does too)
                                try
                                {
                                    s.ReceiveTimeout = 2000;
                                    while (DateTime.UtcNow < end && !run.Stop)
                                    {
                                        if (!reverse)
                                        {
                                            if (udp)
                                            {
                                                if (Interlocked.Read(ref bytes[0]) > perStream * streams.Count * pace.Elapsed.TotalSeconds) { Thread.Sleep(1); continue; }
                                                var now = DateTime.UtcNow - new DateTime(1970, 1, 1); long sec = (long)now.TotalSeconds; long us = (long)((now.TotalSeconds - sec) * 1e6); seq++;
                                                Be(buf, 0, (uint)sec); Be(buf, 4, (uint)us); Be(buf, 8, (uint)seq);
                                            }
                                            int n = s.Send(buf); Interlocked.Add(ref bytes[0], n);
                                        }
                                        else
                                        {
                                            int n; try { n = s.Receive(buf); } catch (SocketException) { continue; }
                                            if (n <= 0) break; Interlocked.Add(ref bytes[0], n);
                                            if (udp && n >= 12)
                                            {
                                                uint sec = Ub(buf, 0), us = Ub(buf, 4); long sq = Ub(buf, 8);
                                                double sent = sec + us / 1e6, arr = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds, transit = arr - sent;
                                                myPk++; if (sq > myMax + 1) myLost += sq - myMax - 1; if (sq > myMax) myMax = sq; if (!double.IsNaN(myLast)) myJit += (Math.Abs(transit - myLast) - myJit) / 16; myLast = transit;
                                            }
                                        }
                                    }
                                }
                                catch (SocketException) { }
                                catch (ObjectDisposedException) { }
                                if (myPk > 0) lock (lockU) { pkts += myPk; lost += myLost; jitSum += myJit; jitN++; jit = jitSum / jitN; }
                            }) { IsBackground = true };
                            threads.Add(t); t.Start();
                        }
                        long prev = 0;
                        for (int i = 0; i < secs && !run.Stop; i++) { Thread.Sleep(1000); long b = Interlocked.Read(ref bytes[0]); run.Add((b - prev) * 8 / 1e6); prev = b; if (i + 1 == o.Omit) atOmit = b; run.Status = (reverse ? "Download " : "Upload ") + (i + 1) + " / " + secs + " s"; }
                        var joinBy = DateTime.UtcNow.AddSeconds(5); foreach (var t in threads) t.Join(Math.Max(0, (int)(joinBy - DateTime.UtcNow).TotalMilliseconds));   // one deadline for all streams
                        sw.Stop();
                        cs.WriteByte(4);   // TEST_END
                    }
                    else if (st == 13)   // EXCHANGE_RESULTS
                    {
                        var sb = new StringBuilder("{\"cpu_util_total\":0,\"cpu_util_user\":0,\"cpu_util_system\":0,\"sender_has_retransmits\":-1,\"streams\":[");
                        // stream ids 1, 3, 4, 5...: the same numbering iperf3 itself uses (id 2 is skipped on purpose - it matches what the server expects)
                        for (int i = 0; i < streams.Count; i++) sb.Append(i > 0 ? "," : "").Append("{\"id\":" + (i == 0 ? 1 : i + 2) + ",\"bytes\":" + (Interlocked.Read(ref bytes[0]) / streams.Count) + ",\"retransmits\":-1,\"jitter\":" + (jit).ToString("0.######", CultureInfo.InvariantCulture) + ",\"errors\":" + lost + ",\"omitted_errors\":0,\"packets\":" + pkts + ",\"omitted_packets\":0,\"start_time\":0,\"end_time\":" + sw.Elapsed.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "}");
                        sb.Append("]}"); SendJson(cs, sb.ToString());
                        var sj = Json.Parse(ReadJson(cs)) as Dictionary<string, object>;
                        double sBytes = 0, sJit = 0, sErr = 0, sPk = 0;
                        object so = null; var ss = sj != null && sj.TryGetValue("streams", out so) ? so as List<object> : null;
                        if (ss != null) foreach (Dictionary<string, object> x in ss.OfType<Dictionary<string, object>>()) { sBytes += Num(x, "bytes"); sJit = Math.Max(sJit, Num(x, "jitter")); sErr += Num(x, "errors"); sPk += Num(x, "packets"); }
                        double secsDone = Math.Max(0.5, sw.Elapsed.TotalSeconds - o.Omit), mine = Interlocked.Read(ref bytes[0]) - atOmit;
                        double recvBytes = reverse ? mine : (sBytes > 0 ? sBytes : mine);
                        double mbps = recvBytes * 8 / 1e6 / secsDone;
                        string dir = reverse ? "Download (server → you)" : "Upload (you → server)";
                        rows.Add(R(0, dir, mbps.ToString("0.0", CultureInfo.CurrentCulture) + " Mb/s" + (par > 1 ? " over " + par + " streams" : "") + (udp ? "" : " (TCP)"), udp ? (mbps >= udpRate / 1e6 * 0.95 ? "The rate you asked for (-b) - raise it to find the limit; loss below shows when the line can't keep up." : "Less than the " + (udpRate / 1e6).ToString("0.#", CultureInfo.CurrentCulture) + " Mb/s asked for - this PC, the line or the server couldn't send that fast.") : Explain(mbps), "m"));
                        if (udp)
                        {
                            double lossPk = reverse ? lost : sErr, total = reverse ? pkts + lost : Math.Max(sPk, 1), jms = (reverse ? jit : sJit) * 1000;
                            if (!reverse && sPk > 0) total = sPk;
                            double lossPct = total > 0 ? 100 * lossPk / total : 0;
                            rows.Add(R(lossPct > 2 ? 2 : lossPct > 0.5 ? 1 : 0, "UDP loss / jitter", lossPct.ToString("0.##", CultureInfo.CurrentCulture) + "% lost · jitter " + jms.ToString("0.0", CultureInfo.CurrentCulture) + " ms at " + (udpRate / 1e6).ToString("0", CultureInfo.CurrentCulture) + " Mb/s", lossPct > 0.5 ? "Loss at this rate means the path can't carry it cleanly - lower the rate to find what it can (e.g. mode 'udp 20m')." : "Clean at this rate.", "m"));
                        }
                    }
                    else if (st == 14) { cs.WriteByte(16); break; }   // DISPLAY_RESULTS -> IPERF_DONE
                    else if (st == 255 || st == 254) throw new InvalidOperationException(st == 255 ? "the server refused (busy with another test?)" : "server error");
                    else if (st == 11 || st == 15) throw new InvalidOperationException("server ended the test");
                }
                }
                catch (Exception) { if (!run.Stop) throw; rows.Clear(); rows.Add(R(1, "Stopped", "the test was stopped - no result", "", "m")); }
                finally { finished = true; lock (streams) foreach (var s in streams) try { s.Close(); } catch { } }
            }
            return rows;
        }
        static void Tune(Socket s, Opt o)
        {
            if (o.Window > 0) { s.SendBufferSize = (int)o.Window; s.ReceiveBufferSize = (int)o.Window; }
            if (o.Tos > 0 && s.AddressFamily == AddressFamily.InterNetwork) try { s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.TypeOfService, o.Tos); } catch (SocketException) { }
        }
        static string Explain(double mbps)
        {
            return mbps > 800 ? "Gigabit-class - the link and both machines keep up." : mbps > 300 ? "Fast. Typical for good Wi-Fi 5/6 close to the router, or a cable." : mbps > 80 ? "Fine for everything including 4K and big downloads; on Wi-Fi, distance and walls take the rest."
                : mbps > 20 ? "Enough for calls and HD video; downloads feel slow." : "Slow - on Wi-Fi move closer or plug in; if it's this slow on a cable, check the link speed and the switch.";
        }
        static double Num(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v is double ? (double)v : 0; }
        static void SendJson(NetworkStream s, string js) { var b = Encoding.UTF8.GetBytes(js); var l = new byte[4]; Be(l, 0, (uint)b.Length); s.Write(l, 0, 4); s.Write(b, 0, b.Length); }
        static string ReadJson(NetworkStream s) { var l = ReadN(s, 4); int n = (int)Ub(l, 0); if (n <= 0 || n > 1 << 20) throw new InvalidOperationException("bad results from the server"); return Encoding.UTF8.GetString(ReadN(s, n)); }
        static byte[] ReadN(NetworkStream s, int n) { var b = new byte[n]; int got = 0; while (got < n) { int r = s.Read(b, got, n - got); if (r <= 0) throw new InvalidOperationException("server closed the connection"); got += r; } return b; }
        static void Be(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
        static uint Ub(byte[] b, int o) { return (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]); }

        // ================================================================== sustained ping
        public static List<string[]> Sustained(string target, string countIn, string ivIn, Run run)
        {
            var rows = new List<string[]>(); var a = Icmp.Resolve4(target);
            if (a == null) { rows.Add(R(2, "Target", "couldn't find " + target + " (IPv4)", "", "u")); return rows; }
            int n, iv; if (!int.TryParse(countIn, out n)) n = 200; if (!int.TryParse(ivIn, out iv)) iv = 200; n = Math.Max(10, Math.Min(2000, n)); iv = Math.Max(20, Math.Min(5000, iv));
            run.SeriesUnit = "ms";
            for (int i = 0; i < n && !run.Stop; i++)
            {
                var sw = Stopwatch.StartNew(); var r = Icmp.Echo(a, 32, 128, false, Math.Max(1000, iv * 3), false);
                run.Add(r.Status == Icmp.Ok ? r.Rtt : double.NaN); run.Status = (i + 1) + " / " + n + " pings to " + a;
                int wait = iv - (int)sw.ElapsedMilliseconds; if (wait > 0) Thread.Sleep(wait);
            }
            var s = run.Copy(); var ok = s.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
            int lostN = s.Count - ok.Count, burst = 0, cur = 0; foreach (var x in s) { cur = double.IsNaN(x) ? cur + 1 : 0; burst = Math.Max(burst, cur); }
            double jit = 0; for (int i = 1; i < s.Count; i++) if (!double.IsNaN(s[i]) && !double.IsNaN(s[i - 1])) jit += Math.Abs(s[i] - s[i - 1]); int pairs = Math.Max(1, s.Count - 1 - lostN); jit /= pairs;
            double loss = s.Count > 0 ? 100.0 * lostN / s.Count : 0;
            rows.Add(R(loss > 2 ? 2 : loss > 0 ? 1 : 0, "Lost", lostN + " of " + s.Count + " (" + loss.ToString("0.#", CultureInfo.CurrentCulture) + "%)" + (burst > 1 ? " · longest gap " + burst + " in a row (" + (burst * iv / 1000.0).ToString("0.#", CultureInfo.CurrentCulture) + " s)" : ""), burst > 3 ? "Losses come in bursts - typical of Wi-Fi roaming, interference or a link flapping, not of a busy line." : "", "m"));
            if (ok.Count > 0)
            {
                rows.Add(R(0, "Delay", "min " + ok[0].ToString("0", CultureInfo.CurrentCulture) + " · median " + ok[ok.Count / 2].ToString("0", CultureInfo.CurrentCulture) + " · 95% under " + ok[(int)(ok.Count * 0.95) < ok.Count ? (int)(ok.Count * 0.95) : ok.Count - 1].ToString("0", CultureInfo.CurrentCulture) + " · max " + ok.Last().ToString("0", CultureInfo.CurrentCulture) + " ms", "", "m"));
                rows.Add(R(jit > 30 ? 2 : jit > 10 ? 1 : 0, "Jitter", jit.ToString("0.0", CultureInfo.CurrentCulture) + " ms between pings", jit > 30 ? "Calls will stutter at this jitter." : "", "m"));
            }
            return rows;
        }

        // ================================================================== dig
        static readonly Dictionary<string, int> Types = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "A", 1 }, { "NS", 2 }, { "CNAME", 5 }, { "SOA", 6 }, { "PTR", 12 }, { "MX", 15 }, { "TXT", 16 }, { "AAAA", 28 }, { "SRV", 33 }, { "CAA", 257 }, { "DS", 43 }, { "SSHFP", 44 }, { "DNSKEY", 48 }, { "TLSA", 52 }, { "SVCB", 64 }, { "HTTPS", 65 } };
        public sealed class Rr { public string Name = "", Type = "", Data = ""; public int Ttl; public int Section; }
        public sealed class Msg { public int Rcode; public bool Aa, Ad, Tc; public List<Rr> Rrs = new List<Rr>(); public string From = ""; public long Ms; }
        public static List<string[]> Dig(string name, string type, string server, Monitor m)
        {
            var rows = new List<string[]>(); name = (name ?? "").Trim().TrimEnd('.'); type = string.IsNullOrWhiteSpace(type) ? "A" : type.Trim().ToUpperInvariant();
            bool trace = type == "TRACE"; if (trace) type = "A";
            int t; if (!Types.TryGetValue(type, out t)) { rows.Add(R(2, "Type", type + " isn't supported - use A, AAAA, MX, TXT, NS, CNAME, SOA, SRV, CAA, PTR, HTTPS, SVCB, DS, DNSKEY, TLSA, SSHFP or TRACE", "", "u")); return rows; }
            if (name == "" || name.Length > 253) { rows.Add(R(2, "Name", "type a name, e.g. github.com", "", "u")); return rows; }
            IPAddress ipq; if (t == 12 && IPAddress.TryParse(name, out ipq)) name = PtrName(ipq);
            if (trace) return Trace(name, rows);
            IPAddress srv; string dns; lock (m.L) dns = m.DnsServer; Msg msg; object srvName;
            string sv = (server ?? "").Trim().TrimStart('@');
            if (sv.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // over HTTPS (RFC 8484): the same question, POSTed to the DoH address you typed
                Uri du; if (!Uri.TryCreate(sv, UriKind.Absolute, out du)) { rows.Add(R(2, "Server", "not a DoH address - e.g. https://cloudflare-dns.com/dns-query", "", "u")); return rows; }
                if (du.AbsolutePath == "/") du = new Uri(du, "/dns-query");
                string why; msg = QueryDoh(du, name, t, out why); srvName = du.Host + " (DoH)";
                if (msg == null) { rows.Add(R(2, "Answer", "no DNS answer from " + du + " - " + why, "", "m")); return rows; }
            }
            else
            {
                if (!IPAddress.TryParse(sv, out srv) && !IPAddress.TryParse((dns ?? "").Split(',')[0].Trim(), out srv)) { rows.Add(R(2, "Server", "no DNS server to ask - type one, e.g. 1.1.1.1, or a DoH address", "", "u")); return rows; }
                msg = Query(srv, name, t, true); srvName = srv;
                if (msg == null) { rows.Add(R(2, "Answer", "no answer from " + srv + " in 3 s", "", "m")); return rows; }
            }
            object srv2 = srvName;
            rows.Add(R(msg.Rcode == 0 ? 0 : msg.Rcode == 3 ? 1 : 2, "Status", (msg.Rcode == 0 ? "NOERROR" : msg.Rcode == 3 ? "NXDOMAIN - the name doesn't exist" : msg.Rcode == 2 ? "SERVFAIL - the server couldn't get an answer" : "error " + msg.Rcode) + " from " + srv2 + " in " + msg.Ms + " ms" + (msg.Aa ? " · authoritative" : "") + (msg.Ad ? " · DNSSEC validated (AD)" : ""), msg.Rcode == 2 ? "Often a DNSSEC failure or the domain's own servers being down." : "", "m"));
            foreach (var r in msg.Rrs.Where(x => x.Section == 1)) rows.Add(R(3, r.Type + "  " + r.Name, r.Data + "   (TTL " + r.Ttl + " s)", "", "m"));
            if (!msg.Rrs.Any(x => x.Section == 1) && msg.Rcode == 0) rows.Add(R(1, "Answer", "no " + type + " records for this name", "", "m"));
            foreach (var r in msg.Rrs.Where(x => x.Section == 2).Take(4)) rows.Add(R(3, "authority  " + r.Type, r.Name + " → " + r.Data, "", "m"));
            return rows;
        }
        static List<string[]> Trace(string name, List<string[]> rows)
        {
            var server = IPAddress.Parse("198.41.0.4"); string sname = "a.root-servers.net";
            for (int step = 0; step < 10; step++)
            {
                var msg = Query(server, name, 1, false);
                if (msg == null) { rows.Add(R(2, "step " + (step + 1), sname + " (" + server + ") didn't answer", "", "m")); break; }
                var ans = msg.Rrs.Where(x => x.Section == 1).ToList();
                if (ans.Count > 0 || msg.Aa || msg.Rcode != 0)
                {
                    rows.Add(R(msg.Rcode == 0 ? 0 : 1, "step " + (step + 1), sname + " (" + msg.Ms + " ms): " + (ans.Count > 0 ? string.Join(", ", ans.Select(a => a.Type + " " + a.Data).Take(4)) : msg.Rcode == 3 ? "the name doesn't exist" : "no address"), msg.Aa ? "This server is the domain's own (authoritative) server - the final answer." : "", "m"));
                    break;
                }
                var ns = msg.Rrs.Where(x => x.Section == 2 && x.Type == "NS").Select(x => x.Data).ToList();
                if (ns.Count == 0) { rows.Add(R(1, "step " + (step + 1), sname + " gave no referral", "", "m")); break; }
                string zone = msg.Rrs.First(x => x.Section == 2 && x.Type == "NS").Name;
                rows.Add(R(3, "step " + (step + 1), sname + " (" + msg.Ms + " ms) says: ask the servers for " + (zone == "" ? "." : zone) + " - " + string.Join(", ", ns.Take(3)), step == 0 ? "The root servers only know who runs each top-level domain." : "", "m"));
                var glue = msg.Rrs.FirstOrDefault(x => x.Section == 3 && x.Type == "A" && ns.Contains(x.Name, StringComparer.OrdinalIgnoreCase));
                IPAddress next = null; if (glue != null && !IPAddress.TryParse(glue.Data, out next)) next = null;
                if (next == null) try { next = Dns.GetHostAddresses(ns[0]).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork); } catch { }
                if (next == null) { rows.Add(R(1, "next", "couldn't find the address of " + ns[0], "", "m")); break; }
                server = next; sname = glue != null ? glue.Name : ns[0];
            }
            return rows;
        }
        public static Msg Query(IPAddress server, string name, int type, bool rd)
        {
            var id = new byte[2]; using (var g = RandomNumberGenerator.Create()) g.GetBytes(id);
            var qa = Question(name, type, rd, id); if (qa == null) return null; var q = new List<byte>(qa);
            var sw = Stopwatch.StartNew(); byte[] resp = null;
            try
            {
                using (var u = new UdpClient(server.AddressFamily))
                {
                    u.Client.ReceiveTimeout = 3000; u.Connect(server, 53); u.Send(q.ToArray(), q.Count);   // connected: only the server we asked can answer
                    var from = new IPEndPoint(IPAddress.Any, 0); resp = u.Receive(ref from);
                    if (resp.Length < 12 || resp[0] != id[0] || resp[1] != id[1]) return null;
                }
                if ((resp[2] & 2) != 0)   // truncated: ask again over TCP
                    using (var tc = new TcpClient(server.AddressFamily))
                    {
                        if (!tc.ConnectAsync(server, 53).Wait(3000)) return null; var s = tc.GetStream(); s.ReadTimeout = 3000;
                        var l = new[] { (byte)(q.Count >> 8), (byte)q.Count }; s.Write(l, 0, 2); s.Write(q.ToArray(), 0, q.Count);
                        var lb = ReadN(s, 2); resp = ReadN(s, lb[0] << 8 | lb[1]);
                        if (resp == null || resp.Length < 12 || resp[0] != id[0] || resp[1] != id[1]) return null;
                    }
            }
            catch (SocketException) { return null; }
            catch (InvalidOperationException) { return null; }
            return Parse(resp, sw.ElapsedMilliseconds, server.ToString());
        }
        // ip -> the reverse name: 4.3.2.1.in-addr.arpa, or 32 nibbles .ip6.arpa
        public static string PtrName(IPAddress ip)
        {
            var b = ip.GetAddressBytes();
            if (ip.AddressFamily == AddressFamily.InterNetwork) return string.Join(".", b.Reverse()) + ".in-addr.arpa";
            var sb = new StringBuilder(); for (int i = b.Length - 1; i >= 0; i--) sb.Append("0123456789abcdef"[b[i] & 15]).Append('.').Append("0123456789abcdef"[b[i] >> 4]).Append('.');
            return sb + "ip6.arpa";
        }
        static byte[] Question(string name, int type, bool rd, byte[] id)
        {
            var q = new List<byte> { id[0], id[1], (byte)(rd ? 1 : 0), 0, 0, 1, 0, 0, 0, 0, 0, 1 };
            foreach (var label in name.Split('.')) { var b = Encoding.ASCII.GetBytes(label); if (b.Length == 0 || b.Length > 63) return null; q.Add((byte)b.Length); q.AddRange(b); }
            q.Add(0); q.Add((byte)(type >> 8)); q.Add((byte)type); q.Add(0); q.Add(1);
            q.AddRange(new byte[] { 0, 0, 41, 0x10, 0, 0, 0, 0x80, 0, 0, 0 });   // EDNS0, 4096 bytes, DO bit
            return q.ToArray();
        }
        // DNS over HTTPS: the wire question POSTed as application/dns-message; id 0 as RFC 8484 asks; answer capped at 64 KB
        public static Msg QueryDoh(Uri url, string name, int type, out string why)
        {
            why = ""; var q = Question(name, type, true, new byte[2]); if (q == null) { why = "bad name"; return null; }
            var sw = Stopwatch.StartNew();
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url); req.Method = "POST"; req.ContentType = "application/dns-message"; req.Accept = "application/dns-message";
                req.Timeout = 6000; req.ReadWriteTimeout = 6000; req.AllowAutoRedirect = false; req.UserAgent = "AdeeboMeter/" + Program.Version; req.Proxy = WebRequest.GetSystemWebProxy(); req.ContentLength = q.Length;
                using (var rs = req.GetRequestStream()) rs.Write(q, 0, q.Length);
                using (var resp = (HttpWebResponse)req.GetResponse()) using (var st = resp.GetResponseStream())
                {
                    if (!(resp.ContentType ?? "").StartsWith("application/dns-message", StringComparison.OrdinalIgnoreCase)) { why = "the server answered " + (int)resp.StatusCode + " " + Draw.Trunc(resp.ContentType ?? "", 40) + ", not DNS"; return null; }
                    var ms = new System.IO.MemoryStream(); var buf = new byte[4096]; int n;
                    while ((n = st.Read(buf, 0, buf.Length)) > 0) { ms.Write(buf, 0, n); if (ms.Length > 65535) { why = "answer too big"; return null; } }
                    var b = ms.ToArray(); if (b.Length < 12) { why = "short answer"; return null; }
                    return Parse(b, sw.ElapsedMilliseconds, url.Host);
                }
            }
            catch (WebException e) { why = Draw.Trunc(e.Message, 80); return null; }
            catch (System.IO.IOException e) { why = Draw.Trunc(e.Message, 80); return null; }
        }
        static string Hex(byte[] b, int o, int n) { var sb = new StringBuilder(); for (int i = 0; i < n && o + i < b.Length; i++) sb.Append(b[o + i].ToString("X2", CultureInfo.InvariantCulture)); return sb.ToString(); }
        static readonly Dictionary<int, string> DnsAlg = new Dictionary<int, string> { { 5, "RSASHA1" }, { 7, "RSASHA1-NSEC3" }, { 8, "RSASHA256" }, { 10, "RSASHA512" }, { 13, "ECDSAP256SHA256" }, { 14, "ECDSAP384SHA384" }, { 15, "ED25519" }, { 16, "ED448" } };
        static string Alg(int a) { string n; return DnsAlg.TryGetValue(a, out n) ? n : "alg " + a; }
        static readonly string[] SvcKeys = { "mandatory", "alpn", "no-default-alpn", "port", "ipv4hint", "ech", "ipv6hint" };
        // HTTPS / SVCB (RFC 9460): priority, target, then the parameters a browser uses (alpn h3 = HTTP/3, ech = Encrypted Client Hello)
        static string Svcb(byte[] b, int d, int len)
        {
            int x = d + 2, end = d + len; string tgt = Name(b, ref x); var sb = new StringBuilder();
            int pri = b[d] << 8 | b[d + 1]; sb.Append(pri == 0 ? "alias of " : "priority " + pri + " ").Append(tgt == "" ? "." : tgt);
            while (x + 4 <= end)
            {
                int k = b[x] << 8 | b[x + 1], l = b[x + 2] << 8 | b[x + 3]; x += 4; if (x + l > end) break;
                string kn = k < SvcKeys.Length ? SvcKeys[k] : "key" + k, v;
                if (k == 1) { var a = new List<string>(); int y = x; while (y < x + l) { int al = b[y++]; a.Add(Encoding.ASCII.GetString(b, y, Math.Min(al, x + l - y))); y += al; } v = string.Join(",", a); }
                else if (k == 3 && l == 2) v = (b[x] << 8 | b[x + 1]).ToString(CultureInfo.InvariantCulture);
                else if (k == 4) { var a = new List<string>(); for (int y = x; y + 4 <= x + l; y += 4) a.Add(new IPAddress(new[] { b[y], b[y + 1], b[y + 2], b[y + 3] }).ToString()); v = string.Join(",", a); }
                else if (k == 6) { var a = new List<string>(); for (int y = x; y + 16 <= x + l; y += 16) { var z = new byte[16]; Array.Copy(b, y, z, 0, 16); a.Add(new IPAddress(z).ToString()); } v = string.Join(",", a); }
                else if (k == 5) v = "(" + l + " bytes - Encrypted Client Hello is on)";
                else v = l == 0 ? "" : Hex(b, x, Math.Min(l, 16)) + (l > 16 ? "..." : "");
                sb.Append(' ').Append(kn).Append(v != "" ? "=" + v : ""); x += l;
            }
            return sb.ToString();
        }
        public static Msg Parse(byte[] resp, long ms, string from)
        {
            var msg = new Msg { Ms = ms, From = from, Aa = (resp[2] & 4) != 0, Tc = (resp[2] & 2) != 0, Ad = (resp[3] & 0x20) != 0, Rcode = resp[3] & 15 };
            int qd = resp[4] << 8 | resp[5], an = resp[6] << 8 | resp[7], ns = resp[8] << 8 | resp[9], ar = resp[10] << 8 | resp[11], p = 12;
            try
            {
                for (int i = 0; i < qd; i++) { Name(resp, ref p); p += 4; }
                for (int sec = 1; sec <= 3; sec++)
                    for (int i = 0; i < (sec == 1 ? an : sec == 2 ? ns : ar); i++)
                    {
                        var r = new Rr { Section = sec, Name = Name(resp, ref p) }; int ty = resp[p] << 8 | resp[p + 1]; r.Ttl = resp[p + 4] << 24 | resp[p + 5] << 16 | resp[p + 6] << 8 | resp[p + 7]; int len = resp[p + 8] << 8 | resp[p + 9]; p += 10; int d = p;
                        if (d + len > resp.Length) break;
                        r.Type = Types.FirstOrDefault(x => x.Value == ty).Key ?? (ty == 41 ? "OPT" : ty == 46 ? "RRSIG" : "type" + ty);
                        int need = ty == 1 ? 4 : ty == 28 ? 16 : ty == 15 ? 3 : ty == 33 ? 7 : ty == 257 ? 2 : ty == 43 ? 5 : ty == 48 ? 5 : ty == 44 ? 3 : ty == 52 ? 4 : ty == 64 || ty == 65 ? 3 : 0;
                        if (len < need) { r.Data = "(short record - " + len + " bytes)"; p = d + len; if (ty != 41) msg.Rrs.Add(r); continue; }   // one bad record never loses the rest
                        switch (ty)
                        {
                            case 1: r.Data = new IPAddress(new[] { resp[d], resp[d + 1], resp[d + 2], resp[d + 3] }).ToString(); break;
                            case 28: { var b = new byte[16]; Array.Copy(resp, d, b, 0, 16); r.Data = new IPAddress(b).ToString(); break; }
                            case 2: case 5: case 12: { int x = d; r.Data = Name(resp, ref x); break; }
                            case 15: { int x = d + 2; r.Data = (resp[d] << 8 | resp[d + 1]) + " " + Name(resp, ref x); break; }
                            case 33: { int x = d + 6; r.Data = "priority " + (resp[d] << 8 | resp[d + 1]) + " weight " + (resp[d + 2] << 8 | resp[d + 3]) + " port " + (resp[d + 4] << 8 | resp[d + 5]) + " → " + Name(resp, ref x); break; }
                            case 6: { int x = d; string mn = Name(resp, ref x), rn = Name(resp, ref x); r.Data = mn + " " + rn + (x + 4 <= d + len ? " serial " + ((uint)(resp[x] << 24 | resp[x + 1] << 16 | resp[x + 2] << 8 | resp[x + 3])) : " (short record)"); break; }
                            case 16: { var sb = new StringBuilder(); int x = d; while (x < d + len) { int l = resp[x++]; sb.Append('"').Append(Encoding.UTF8.GetString(resp, x, Math.Min(l, d + len - x))).Append("\" "); x += l; } r.Data = sb.ToString().Trim(); break; }
                            case 257: { int tl = Math.Min(resp[d + 1], len - 2); r.Data = (resp[d] != 0 ? "critical " : "") + Encoding.ASCII.GetString(resp, d + 2, tl) + " \"" + Encoding.ASCII.GetString(resp, d + 2 + tl, len - 2 - tl) + "\""; break; }
                            case 43: r.Data = "key tag " + (resp[d] << 8 | resp[d + 1]) + " " + Alg(resp[d + 2]) + " digest " + (resp[d + 3] == 2 ? "SHA-256" : resp[d + 3] == 4 ? "SHA-384" : resp[d + 3] == 1 ? "SHA-1" : "type " + resp[d + 3]) + " " + Hex(resp, d + 4, len - 4); break;
                            case 48: { int fl = resp[d] << 8 | resp[d + 1]; uint ac = 0; for (int i2 = 0; i2 < len; i2++) ac += (i2 & 1) != 0 ? resp[d + i2] : (uint)(resp[d + i2] << 8); ac += ac >> 16 & 0xFFFF;
                                    r.Data = ((fl & 1) != 0 ? "KSK (signs the keys)" : "ZSK (signs the records)") + " · " + Alg(resp[d + 3]) + " · key tag " + (ac & 0xFFFF) + " · " + (len - 4) + "-byte key"; break; }   // key tag: RFC 4034 appendix B
                            case 44: r.Data = (resp[d] == 1 ? "RSA" : resp[d] == 3 ? "ECDSA" : resp[d] == 4 ? "Ed25519" : "alg " + resp[d]) + " " + (resp[d + 1] == 2 ? "SHA-256" : resp[d + 1] == 1 ? "SHA-1" : "fp " + resp[d + 1]) + " " + Hex(resp, d + 2, len - 2); break;
                            case 52: r.Data = "usage " + resp[d] + (resp[d] == 3 ? " (this exact cert)" : resp[d] == 2 ? " (this CA)" : "") + " · selector " + (resp[d + 1] == 1 ? "public key" : "full cert") + " · " + (resp[d + 2] == 1 ? "SHA-256" : resp[d + 2] == 2 ? "SHA-512" : "exact") + " " + Hex(resp, d + 3, len - 3); break;
                            case 64: case 65: r.Data = Svcb(resp, d, len); break;
                            default: r.Data = len + " bytes"; break;
                        }
                        p = d + len; if (ty != 41) msg.Rrs.Add(r);
                    }
            }
            catch (IndexOutOfRangeException) { }
            catch (ArgumentException) { }
            return msg;
        }
        internal static string Name(byte[] b, ref int p)
        {
            var parts = new List<string>(); int jumps = 0, pos = p; bool jumped = false;
            while (pos < b.Length && jumps < 20)
            {
                int l = b[pos];
                if (l == 0) { pos++; break; }
                if ((l & 0xC0) == 0xC0) { if (pos + 1 >= b.Length) { pos = b.Length; break; } if (!jumped) p = pos + 2; pos = (l & 0x3F) << 8 | b[pos + 1]; jumped = true; jumps++; continue; }
                if (pos + 1 + l > b.Length) { parts.Add("(cut)"); pos = b.Length; break; }   // a name cut short ends the name, never the whole answer
                parts.Add(Encoding.ASCII.GetString(b, pos + 1, l)); pos += l + 1;
            }
            if (!jumped) p = pos;
            return string.Join(".", parts);
        }

        // ================================================================== HTTP / TLS inspector
        public static List<string[]> Http(string url)
        {
            var rows = new List<string[]>(); if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url.Trim();
            Uri u; if (!Uri.TryCreate(url, UriKind.Absolute, out u) || (u.Scheme != "https" && u.Scheme != "http")) { rows.Add(R(2, "Address", "not a web address", "", "u")); return rows; }
            for (int hop = 0; hop < 8; hop++)
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(u); req.AllowAutoRedirect = false; req.Method = "GET"; req.Timeout = 8000; req.UserAgent = "AdeeboMeter/" + Program.Version; req.Proxy = WebRequest.GetSystemWebProxy();
                    var sw = Stopwatch.StartNew(); HttpWebResponse resp;
                    try { resp = (HttpWebResponse)req.GetResponse(); } catch (WebException we) { resp = we.Response as HttpWebResponse; if (resp == null) throw; }
                    using (resp)
                    {
                        int code = (int)resp.StatusCode; string loc = resp.Headers["Location"] ?? "";
                        rows.Add(R(code >= 500 ? 2 : code >= 400 ? 1 : 0, (hop == 0 ? "" : "then ") + code + " " + resp.StatusDescription, u + " · " + sw.ElapsedMilliseconds + " ms" + (loc != "" ? " → " + loc : ""), "", "m"));
                        if (code >= 300 && code < 400 && loc != "") { Uri n; if (Uri.TryCreate(u, loc, out n)) { u = n; continue; } }
                        foreach (var h in new[] { "Server", "Alt-Svc", "Content-Type", "Cache-Control", "Via", "X-Cache", "CF-Ray" })
                            if (!string.IsNullOrEmpty(resp.Headers[h])) rows.Add(R(3, h, Draw.Trunc(resp.Headers[h], 120), h == "Alt-Svc" && resp.Headers[h].Contains("h3") ? "The site also offers HTTP/3 (QUIC over UDP 443) - browsers switch to it after the first visit." : "", "m"));
                        rows.AddRange(SecurityHeaders(resp.Headers, u.Scheme == "https"));
                        // every header, as the server sent it (cookies shortened - they're often long and never needed here)
                        rows.Add(R(3, "All headers", resp.Headers.Count + " from the final answer" + (resp.Headers.Count > 80 ? " (the first 80 shown)" : ""), "Cookie values are never shown or copied - they are often sign-in sessions; only their length is.", "m"));
                        foreach (string k in resp.Headers.AllKeys.Take(80)) { string v = resp.Headers[k] ?? ""; if (k.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)) v = "(" + v.Length + " characters)"; rows.Add(new[] { "6", "", (k + ": ").PadRight(28) + Draw.Trunc(v, 300), "", "" }); }
                        break;
                    }
                }
                catch (Exception e) { rows.Add(R(2, "Request", Draw.Trunc(e.Message, 100), "", "m")); break; }
            }
            if (u.Scheme == "https") rows.AddRange(Tls(u.Host, u.Port));
            return rows;
        }
        // the headers that make a site safer in a browser - present or missing, each in one plain line
        static readonly string[][] SecHdr = {
            new[] { "Strict-Transport-Security", "HSTS", "browsers only ever use HTTPS for this site - no downgrade to plain HTTP" },
            new[] { "Content-Security-Policy", "CSP", "limits which scripts may run - the main defence against injected scripts" },
            new[] { "X-Content-Type-Options", "nosniff", "the browser won't guess a file's type (stops some disguised scripts)" },
            new[] { "X-Frame-Options", "framing", "other sites can't show this one inside a frame (clickjacking)" },
            new[] { "Referrer-Policy", "referrer", "how much of the address is told to the next site you go to" },
            new[] { "Permissions-Policy", "permissions", "which browser features (camera, location...) the page may ask for" } };
        public static List<string[]> SecurityHeaders(WebHeaderCollection h, bool https)
        {
            var rows = new List<string[]>(); var missing = new List<string>();
            foreach (var s in SecHdr)
            {
                string v = h[s[0]];
                if (s[0] == "X-Frame-Options" && string.IsNullOrEmpty(v) && (h["Content-Security-Policy"] ?? "").Contains("frame-ancestors")) v = "via CSP frame-ancestors";
                if (string.IsNullOrEmpty(v)) { if (https || s[1] != "HSTS") missing.Add(s[1]); continue; }
                rows.Add(R(3, s[0], Draw.Trunc(v, 140), s[2] + ".", "m"));
            }
            if (missing.Count > 0) rows.Add(R(3, "Security headers missing", string.Join(", ", missing), "Not a fault in your network - these are the site's own choices. Pages for sign-in or payments usually send all of them.", "m"));
            return rows;
        }
        // revocation is checked only when you press it: Windows then asks the certificate authority's OCSP / CRL servers
        public static List<string[]> Revocation(string host, int port)
        {
            var rows = new List<string[]>(); X509Certificate2 cert = null;
            try
            {
                using (var tc = new TcpClient())
                {
                    if (!tc.ConnectAsync(host, port).Wait(5000)) { rows.Add(R(2, "Revocation", "couldn't connect to " + host, "", "m")); return rows; }
                    using (var ssl = new SslStream(tc.GetStream(), false, (s, c, ch, e) => { if (cert == null && c != null) cert = new X509Certificate2(c); return e == SslPolicyErrors.None; }))
                    { ssl.ReadTimeout = 5000; var hs = System.Threading.Tasks.Task.Factory.StartNew(() => ssl.AuthenticateAsClient(host)); bool hd; try { hd = hs.Wait(8000); } catch (AggregateException) { hd = cert != null; } if (!hd && cert == null) { rows.Add(R(2, "Revocation", "no TLS answer in 8 s", "", "m")); return rows; } }
                }
                if (cert == null) { rows.Add(R(2, "Revocation", "no certificate", "", "m")); return rows; }
                using (cert) using (var chain = new X509Chain())
                {
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online; chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain; chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(10);
                    var sw = Stopwatch.StartNew(); chain.Build(cert);
                    var bad = chain.ChainStatus.Where(x => x.Status != X509ChainStatusFlags.NoError).ToList();
                    bool revoked = bad.Any(x => x.Status == X509ChainStatusFlags.Revoked), unknown = bad.Any(x => x.Status == X509ChainStatusFlags.RevocationStatusUnknown || x.Status == X509ChainStatusFlags.OfflineRevocation);
                    var other = bad.Where(x => x.Status != X509ChainStatusFlags.Revoked && x.Status != X509ChainStatusFlags.RevocationStatusUnknown && x.Status != X509ChainStatusFlags.OfflineRevocation).Select(x => x.Status.ToString()).Distinct().ToList();
                    if (other.Count > 0) rows.Add(R(1, "Chain problem", string.Join(", ", other), "The certificate chain fails for another reason, so revocation alone doesn't make it trustworthy.", "m"));
                    rows.Add(R(revoked ? 2 : unknown ? 1 : 0, "Revocation (OCSP / CRL)", revoked ? "REVOKED - the authority has withdrawn this certificate" : unknown ? "couldn't be checked - the authority's server didn't answer" : "not revoked · checked in " + sw.ElapsedMilliseconds + " ms", revoked ? "Don't trust this site. Browsers block it." : unknown ? "A firewall or proxy often blocks the CA's OCSP / CRL servers. Browsers then usually carry on anyway." : "", "m"));
                    foreach (var oid in new[] { "1.3.6.1.5.5.7.1.1", "2.5.29.31" })
                    {
                        var ext = cert.Extensions.Cast<X509Extension>().FirstOrDefault(x => x.Oid != null && x.Oid.Value == oid); if (ext == null) continue;
                        var urls = System.Text.RegularExpressions.Regex.Matches(ext.Format(false), @"https?://[^\s,)]+").Cast<System.Text.RegularExpressions.Match>().Select(mm => mm.Value).Distinct().Take(3).ToList();
                        if (urls.Count > 0) rows.Add(R(3, oid == "2.5.29.31" ? "CRL from" : "OCSP / issuer from", string.Join("  ", urls), "", "m"));
                    }
                }
            }
            catch (Exception e) { rows.Add(R(2, "Revocation", Draw.Trunc(e.Message, 100), "", "m")); }
            return rows;
        }
        static List<string[]> Tls(string host, int port)
        {
            var rows = new List<string[]>(); X509Certificate2 cert = null; SslPolicyErrors errs = SslPolicyErrors.None; List<string[]> chainRows = null;
            foreach (var proto in new[] { Tuple.Create("TLS 1.3", (SslProtocols)12288), Tuple.Create("TLS 1.2", SslProtocols.Tls12) })
            {
                try
                {
                    using (var tc = new TcpClient())
                    {
                        if (!tc.ConnectAsync(host, port).Wait(5000)) { rows.Add(R(2, proto.Item1, "couldn't connect", "", "m")); continue; }
                        using (var ssl = new SslStream(tc.GetStream(), false, (s, c, ch, e) => { if (cert == null && c != null) cert = new X509Certificate2(c); if (chainRows == null && ch != null) chainRows = Chain(ch); errs = e; return e == SslPolicyErrors.None; }))
                        { ssl.ReadTimeout = 5000; var hs = System.Threading.Tasks.Task.Factory.StartNew(() => ssl.AuthenticateAsClient(host, null, proto.Item2, false)); bool hd; try { hd = hs.Wait(8000); } catch (AggregateException ae) { throw ae.InnerException ?? ae; } if (!hd) { tc.Close(); throw new TimeoutException("no TLS answer in 8 s"); } rows.Add(R(0, proto.Item1, "supported · " + ssl.CipherAlgorithm + " " + ssl.CipherStrength + "-bit", "", "m")); }
                    }
                }
                catch (Exception e) { rows.Add(R(3, proto.Item1, errs != SslPolicyErrors.None ? "certificate not trusted (" + errs + ")" : "not offered (" + Draw.Trunc(e.Message, 50) + ")", "", "m")); }
            }
            if (cert != null)
                using (cert)
                {
                    double days = (cert.NotAfter - DateTime.Now).TotalDays;
                    rows.Add(R(days < 0 ? 2 : days < 14 ? 1 : 0, "Certificate", cert.GetNameInfo(X509NameType.SimpleName, false) + " · from " + cert.GetNameInfo(X509NameType.SimpleName, true) + " · expires " + cert.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + (days < 0 ? " (EXPIRED)" : " (" + days.ToString("0", CultureInfo.CurrentCulture) + " days)"), errs != SslPolicyErrors.None ? "Windows doesn't trust it: " + errs + "." : "", "m"));
                    var san = cert.Extensions.Cast<X509Extension>().FirstOrDefault(x => x.Oid != null && x.Oid.Value == "2.5.29.17");
                    if (san != null) rows.Add(R(3, "Names it covers", Draw.Trunc(san.Format(false).Replace("DNS Name=", ""), 200), "", "m"));
                    string ins = ""; try { ins = Inspection.InspectedBy(cert); } catch (Exception e) { Program.Log("inspect: " + e.Message); }
                    if (ins != "") rows.Add(R(1, "Inspected on the way", "re-signed by " + ins, "A company proxy (or something in the middle) decrypts and re-encrypts this connection.", "i"));
                    if (cert.NotBefore > DateTime.Now) rows.Add(R(2, "Not valid yet", "valid from " + cert.NotBefore.ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture), "Either the site's new certificate went live early, or this PC's clock is behind.", "m"));
                    if (chainRows != null) rows.AddRange(chainRows);
                    rows.Add(new[] { "3", "Revocation", "not checked - press to ask the authority (OCSP / CRL)", "This is the only part that contacts a server other than the site: the certificate authority's own.", "m", "revoke:" + host + ":" + port.ToString(CultureInfo.InvariantCulture) });
                }
            return rows;
        }
        // the whole chain, leaf to root: who, valid from-to, key and signature
        static List<string[]> Chain(X509Chain ch)
        {
            var rows = new List<string[]>(); int i = 0;
            foreach (var el in ch.ChainElements)
            {
                var c = el.Certificate; if (c == null) break;
                if (i > 6) { rows.Add(R(3, "Chain", "+" + (ch.ChainElements.Count - i) + " more certificates not shown", "", "m")); break; }
                string key = c.PublicKey.Oid.FriendlyName ?? c.PublicKey.Oid.Value; int bits = 0;
                try { if (c.PublicKey.Oid.Value == "1.2.840.113549.1.1.1") using (var rsa = c.GetRSAPublicKey()) bits = rsa.KeySize; else if (c.PublicKey.Oid.Value == "1.2.840.10045.2.1") { int kl = c.PublicKey.EncodedKeyValue.RawData.Length; bits = kl >= 133 ? 521 : kl >= 97 ? 384 : kl >= 65 ? 256 : 0; } } catch (CryptographicException) { }
                string name = i == 0 ? "Chain: the site" : i == ch.ChainElements.Count - 1 ? "Chain: root" : "Chain: issuer " + i;
                rows.Add(R(3, name, c.GetNameInfo(X509NameType.SimpleName, false) + " · " + c.NotBefore.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + " to " + c.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture),
                    (key == "RSA" || key == "ECC" || key == "1.2.840.10045.2.1" ? (key == "1.2.840.10045.2.1" ? "ECC" : key) : key) + (bits > 0 ? " " + bits + "-bit" : "") + " key · signed with " + (c.SignatureAlgorithm.FriendlyName ?? c.SignatureAlgorithm.Value) + (el.ChainElementStatus.Length > 0 ? " · " + string.Join(", ", el.ChainElementStatus.Select(z => z.Status.ToString())) : ""), "m"));
                i++;
            }
            return rows;
        }

        // ================================================================== nearest cloud
        static readonly string[][] Clouds = {
            new[] { "Microsoft 365 (Outlook)", "outlook.office365.com" }, new[] { "Teams", "teams.microsoft.com" }, new[] { "Microsoft sign-in", "login.microsoftonline.com" },
            new[] { "Google", "www.google.com" }, new[] { "Cloudflare", "1.1.1.1" }, new[] { "AWS Canada (Montreal)", "ec2.ca-central-1.amazonaws.com" },
            new[] { "AWS US East (Virginia)", "ec2.us-east-1.amazonaws.com" }, new[] { "AWS US West (Oregon)", "ec2.us-west-2.amazonaws.com" }, new[] { "AWS Europe (Ireland)", "ec2.eu-west-1.amazonaws.com" } };
        public static List<string[]> Nearest(Run run)
        {
            var rows = new List<string[]>();
            foreach (var c in Clouds)
            {
                if (run.Stop) break; run.Status = "Timing " + c[0] + "...";
                double best = double.NaN; IPAddress a = null;
                try { a = Dns.GetHostAddresses(c[1]).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork); } catch { }
                if (a != null) for (int i = 0; i < 3; i++) { double t = PathTools.Connect(a, 443, 3000); if (!double.IsNaN(t) && (double.IsNaN(best) || t < best)) best = t; }
                rows.Add(R(double.IsNaN(best) ? 2 : best > 100 ? 1 : 0, c[0], double.IsNaN(best) ? "couldn't connect" : best.ToString("0", CultureInfo.CurrentCulture) + " ms (" + a + ")", "", "m"));
            }
            var m365 = rows.Take(3).Select(r => { double v; return double.TryParse(r[2].Split(' ')[0], NumberStyles.Float, CultureInfo.CurrentCulture, out v) ? v : double.NaN; }).Where(v => !double.IsNaN(v)).ToList();
            if (m365.Count > 0 && m365.Average() > 60) rows.Insert(0, R(1, "Microsoft 365", "far away (" + m365.Average().ToString("0", CultureInfo.CurrentCulture) + " ms)", "Microsoft's front doors are usually within 10-30 ms in a city. Far means your traffic leaves elsewhere - typically a VPN or a company proxy in another city. Outlook and Teams feel it.", "i"));
            return rows;
        }

        // ================================================================== ping sweep (your own subnet, up to 254 addresses)
        public static List<string[]> Sweep(Monitor m, string confirm, Run run)
        {
            var rows = new List<string[]>(); NetworkInterface nic; bool vpn; lock (m.L) { nic = m.Primary; vpn = m.VpnUp; }
            bool company = vpn || !string.Equals(Environment.UserDomainName, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
            if (company && !string.Equals((confirm ?? "").Trim(), "yes", StringComparison.OrdinalIgnoreCase))
            { rows.Add(R(1, "Company network?", "this PC is on a company domain or VPN", "Sweeping a company network can trip the security team's alarms. Tick \"This is my own network, or I'm allowed to scan it\" above and press Run if you're sure (home networks are fine).", "i")); return rows; }
            if (nic == null) { rows.Add(R(2, "Network", "no active adapter", "", "u")); return rows; }
            Devices.QuietUntil = DateTime.UtcNow.AddMinutes(10);   // the sweep fills the ARP table - those devices were already there, they didn't just join
            var u = nic.GetIPProperties().UnicastAddresses.FirstOrDefault(x => x.Address.AddressFamily == AddressFamily.InterNetwork && x.IPv4Mask != null);
            if (u == null) { rows.Add(R(2, "Network", "no IPv4 address", "", "u")); return rows; }
            var ip = u.Address.GetAddressBytes(); var mask = u.IPv4Mask.GetAddressBytes();
            var targets = new List<IPAddress>();
            bool wide = mask[2] != 255;   // bigger than a /24: sweep only my own /24
            for (int i = 1; i < 255; i++)
            {
                var b = new[] { ip[0], ip[1], ip[2], (byte)i };
                if (!wide) { bool inNet = true; for (int j = 0; j < 4; j++) if ((b[j] & mask[j]) != (ip[j] & mask[j])) inNet = false; if (!inNet) continue; }
                targets.Add(new IPAddress(b));
            }
            var up = new List<Tuple<IPAddress, long>>(); int done = 0;
            // 16 threads of its own, never the shared thread pool (which also runs AdeeboMeter's own timers)
            int next = -1; var workers = new List<Thread>();
            for (int w = 0; w < 16; w++)
            {
                var th = new Thread(() =>
                {
                    for (int i = Interlocked.Increment(ref next); i < targets.Count && !run.Stop; i = Interlocked.Increment(ref next))
                    { var r = Icmp.Echo(targets[i], 32, 64, false, 500, false); if (r.Status == Icmp.Ok) lock (up) up.Add(Tuple.Create(targets[i], r.Rtt)); int d = Interlocked.Increment(ref done); run.Status = d + " / " + targets.Count + " addresses"; }
                }) { IsBackground = true, Name = "AdeeboMeter sweep" };
                workers.Add(th); th.Start();
            }
            var allBy = DateTime.UtcNow.AddSeconds(90); foreach (var th in workers) th.Join(Math.Max(0, (int)(allBy - DateTime.UtcNow).TotalMilliseconds));   // one deadline for the whole sweep
            var arp = new Dictionary<string, string>(); try { foreach (var a in Arp.Table()) arp[a.Ip] = a.Mac; } catch (Exception e) { Program.Log("sweep arp: " + e.Message); }
            foreach (var x in up.OrderBy(x => BitConverter.ToUInt32(x.Item1.GetAddressBytes().Reverse().ToArray(), 0)))
            {
                string mac; arp.TryGetValue(x.Item1.ToString(), out mac);
                rows.Add(R(3, x.Item1.ToString(), (mac != null ? mac + "  " + Vendor(mac) : "") + (x.Item1.ToString() == u.Address.ToString() ? "  (this PC)" : "") + " · " + x.Item2 + " ms", "", "m"));
            }
            rows.Insert(0, R(0, "Answered", up.Count + " of " + targets.Count + (wide ? " (your /24 only - the network is bigger)" : ""), "Devices that ignore ping (many phones, printers in sleep) don't show - the ARP table on Your Wi-Fi may know more.", "m"));
            return rows;
        }
        static readonly Dictionary<string, string> Oui = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "00:1A:11", "Google" }, { "F4:F5:D8", "Google" }, { "3C:5A:B4", "Google" }, { "00:17:88", "Philips Hue" }, { "B8:27:EB", "Raspberry Pi" }, { "DC:A6:32", "Raspberry Pi" }, { "E4:5F:01", "Raspberry Pi" },
            { "00:0C:29", "VMware" }, { "00:50:56", "VMware" }, { "08:00:27", "VirtualBox" }, { "00:15:5D", "Hyper-V" }, { "3C:84:6A", "TP-Link" }, { "50:C7:BF", "TP-Link" }, { "F4:EC:38", "TP-Link" },
            { "00:1B:63", "Apple" }, { "F0:18:98", "Apple" }, { "AC:BC:32", "Apple" }, { "A4:83:E7", "Apple" }, { "FC:FB:FB", "Cisco" }, { "00:1E:BD", "Cisco" }, { "00:24:D4", "Free / Freebox" },
            { "44:65:0D", "Amazon" }, { "F0:27:2D", "Amazon" }, { "68:54:FD", "Amazon" }, { "B0:BE:76", "TP-Link" }, { "00:11:32", "Synology" }, { "00:90:A9", "Western Digital" }, { "00:1D:73", "Buffalo" },
            { "30:B5:C2", "TP-Link" }, { "C8:3A:35", "Tenda" }, { "00:14:BF", "Linksys" }, { "C0:56:27", "Belkin" }, { "00:26:F2", "Netgear" }, { "A0:63:91", "Netgear" }, { "E8:FC:AF", "Netgear" },
            { "00:1F:3B", "Intel" }, { "3C:A9:F4", "Intel" }, { "A4:4E:31", "Intel" }, { "00:23:24", "Lenovo" }, { "54:EE:75", "Wistron / laptop" }, { "D4:BE:D9", "Dell" }, { "F8:BC:12", "Dell" },
            { "00:21:5A", "HP" }, { "3C:D9:2B", "HP" }, { "00:80:77", "Brother" }, { "00:00:48", "Epson" }, { "5C:AA:FD", "Sonos" }, { "00:0E:58", "Sonos" }, { "D0:52:A8", "SmartThings" },
            { "18:B4:30", "Nest" }, { "64:16:66", "Nest" }, { "B4:7C:9C", "Amazon" }, { "8C:85:90", "Apple" }, { "28:6C:07", "Xiaomi" }, { "7C:49:EB", "Xiaomi" }, { "00:E0:4C", "Realtek" },
            { "E8:6F:38", "Hitron (Rogers modem)" }, { "00:FC:8D", "Hitron" }, { "CC:03:FA", "Technicolor" }, { "10:56:11", "Arris" }, { "00:1D:D0", "Arris" }, { "BC:30:7D", "Wistron" } };
        public static string Vendor(string mac)
        {
            if (string.IsNullOrEmpty(mac) || mac.Length < 8) return ""; string p = mac.Substring(0, 8).Replace('-', ':').ToUpperInvariant();
            int first = Convert.ToInt32(p.Substring(0, 2), 16); if ((first & 2) != 0) return "(random address - a phone or laptop hiding its identity)";
            string v; return Oui.TryGetValue(p, out v) ? "(" + v + ")" : "";
        }

        // ================================================================== Wake-on-LAN
        public static List<string[]> Wake(string macIn) { return Wake(macIn, ""); }
        public static List<string[]> Wake(string macIn, string secureOn)
        {
            var rows = new List<string[]>(); string hex = new string((macIn ?? "").Where(Uri.IsHexDigit).ToArray());
            if (hex.Length != 12) { rows.Add(R(2, "Hardware address", "type the PC's MAC, e.g. 3C:84:6A:12:34:56", "Your Wi-Fi > devices on your network lists the ones your PC has seen.", "u")); return rows; }
            var mac = Enumerable.Range(0, 6).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
            string pw = new string((secureOn ?? "").Where(Uri.IsHexDigit).ToArray());
            if ((secureOn ?? "").Trim() != "" && pw.Length != 12) { rows.Add(R(2, "SecureOn password", "6 bytes written like a MAC, e.g. 01:23:45:67:89:AB", "Set in the PC's BIOS or network card; most PCs don't use one - leave it empty.", "u")); return rows; }
            var pkt = new byte[pw.Length == 12 ? 108 : 102]; for (int i = 0; i < 6; i++) pkt[i] = 0xFF; for (int i = 1; i <= 16; i++) Array.Copy(mac, 0, pkt, i * 6, 6);
            if (pw.Length == 12) for (int i = 0; i < 6; i++) pkt[102 + i] = Convert.ToByte(pw.Substring(i * 2, 2), 16);
            using (var u = new UdpClient()) { u.EnableBroadcast = true; u.Send(pkt, pkt.Length, new IPEndPoint(IPAddress.Broadcast, 9)); u.Send(pkt, pkt.Length, new IPEndPoint(IPAddress.Broadcast, 7)); }
            rows.Add(R(3, "Sent (no reply is possible)", "magic packet for " + string.Join(":", mac.Select(b => b.ToString("X2", CultureInfo.InvariantCulture))) + " to your network" + (pw.Length == 12 ? " (with SecureOn password)" : ""), "It only wakes PCs whose network card and BIOS allow Wake-on-LAN, on the same network (not over the internet or most VPNs).", "m"));
            return rows;
        }

        // ================================================================== subnet calculator
        public static List<string[]> Subnet(string input, string inside)
        {
            var rows = new List<string[]>(); string s = (input ?? "").Trim(); int prefix = -1; string ipPart = s;
            int slash = s.IndexOf('/'); var sp = s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (slash > 0) { ipPart = s.Substring(0, slash); if (!int.TryParse(s.Substring(slash + 1), out prefix)) prefix = -2; }
            else if (sp.Length == 2) { ipPart = sp[0]; IPAddress mk; if (IPAddress.TryParse(sp[1], out mk)) prefix = MaskBits(mk.GetAddressBytes()); }
            IPAddress ip; if (!IPAddress.TryParse(ipPart, out ip)) { rows.Add(R(2, "Input", "type e.g. 10.1.2.3/22, 10.1.2.3 255.255.252.0 or 2001:db8::1/64", "", "u")); return rows; }
            int bits = ip.AddressFamily == AddressFamily.InterNetwork ? 32 : 128; if (prefix < 0) prefix = bits; if (prefix > bits) { rows.Add(R(2, "Prefix", "/" + prefix + " is too long", "", "u")); return rows; }
            var b = ip.GetAddressBytes(); var mask = new byte[b.Length]; for (int i = 0; i < prefix; i++) mask[i / 8] |= (byte)(0x80 >> (i % 8));
            var net = b.Select((x, i) => (byte)(x & mask[i])).ToArray(); var bc = b.Select((x, i) => (byte)(x | ~mask[i])).ToArray();
            long count = bits == 32 ? 1L << (32 - prefix) : 0;
            rows.Add(R(3, "Network", new IPAddress(net) + "/" + prefix, "", "i"));
            if (bits == 32)
            {
                rows.Add(R(3, "Mask / wildcard", new IPAddress(mask) + "  /  " + new IPAddress(mask.Select(x => (byte)~x).ToArray()), "", "i"));
                if (prefix <= 30) { var f = (byte[])net.Clone(); f[3]++; var l = (byte[])bc.Clone(); l[3]--; rows.Add(R(3, "Usable hosts", new IPAddress(f) + " - " + new IPAddress(l) + "  (" + (count - 2) + ")", "", "i")); rows.Add(R(3, "Broadcast", new IPAddress(bc).ToString(), "", "i")); }
                else rows.Add(R(3, "Addresses", count + (prefix == 31 ? " (point-to-point link, RFC 3021)" : " (one host)"), "", "i"));
                rows.Add(R(3, "In binary", string.Join(".", b.Select(x => Convert.ToString(x, 2).PadLeft(8, '0'))), "", "i"));
            }
            else
            {
                rows.Add(R(3, "Expanded", string.Join(":", Enumerable.Range(0, 8).Select(i => (b[i * 2] << 8 | b[i * 2 + 1]).ToString("x4", CultureInfo.InvariantCulture))), "", "i"));
                rows.Add(R(3, "Compressed", ip.ToString(), "", "i"));
                rows.Add(R(3, "Addresses in it", prefix >= 64 && prefix <= 64 ? "2^64 (a standard LAN)" : "2^" + (bits - prefix), "", "i"));
            }
            IPAddress q; if (!string.IsNullOrWhiteSpace(inside) && IPAddress.TryParse(inside.Trim(), out q) && q.AddressFamily == ip.AddressFamily)
            { var qb = q.GetAddressBytes(); bool inNet = qb.Select((x, i) => (byte)(x & mask[i])).SequenceEqual(net); rows.Add(R(inNet ? 0 : 1, q + " inside?", inNet ? "yes" : "no", "", "i")); }
            return rows;
        }
        static int MaskBits(byte[] m) { int n = 0; foreach (var x in m) for (int i = 7; i >= 0; i--) if ((x >> i & 1) == 1) n++; else return n; return n; }

        // ================================================================== the return path, as far as one PC can know it
        public static List<string[]> ReturnPath(string target, Run run)
        {
            var rows = new List<string[]>(); var a = Icmp.Resolve4(target);
            if (a == null) { rows.Add(R(2, "Target", "couldn't find " + target + " (IPv4)", "", "u")); return rows; }
            run.Status = "Tracing to " + a + "...";
            var hops = PathTools.Trace(a, 30, () => run.Stop);
            foreach (var h in hops)
            {
                int back = PathTools.BackHops(h.ReplyTtl);
                string note = h.Ip == "" ? "no answer" : h.Ip + " · out " + h.N + " hops, back ~" + back;
                int lv = h.Ip != "" && back > 0 && Math.Abs(back - h.N) >= 3 ? 1 : 3;
                rows.Add(R(lv, "hop " + h.N, note + (lv == 1 ? "  ← the way back differs here" : ""), "", "m"));
            }
            var asym = hops.Where(h => h.Ip != "" && PathTools.BackHops(h.ReplyTtl) > 0).ToList();
            if (asym.Count > 0)
            {
                int diffs = asym.Count(h => Math.Abs(PathTools.BackHops(h.ReplyTtl) - h.N) >= 3);
                rows.Insert(0, R(diffs > 0 ? 1 : 0, "Reading the reply TTLs", diffs > 0 ? "From " + diffs + " hop(s) the replies come back over a clearly different number of routers - the way back is a different road." : "Replies come back over about as many routers as the way out - probably the same road.", "Each reply starts with a TTL of 64, 128 or 255 and loses one per router; what's left tells how far it travelled back.", "i"));
            }
            run.Status = "Record Route...";
            rows.Add(R(3, "Record Route to " + a, "", "", "m")); rows.AddRange(PathTools.RecordRoute(a));
            var firstPublic = hops.FirstOrDefault(h => h.Ip != "" && Watch.IsPublic(h.Ip)); IPAddress fp;
            if (firstPublic != null && IPAddress.TryParse(firstPublic.Ip, out fp) && !fp.Equals(a)) { rows.Add(R(3, "Record Route to your provider (" + fp + ")", "", "", "m")); rows.AddRange(PathTools.RecordRoute(fp)); }
            if (Online.On) { run.Status = "How the internet reaches you..."; rows.AddRange(Online.ReachMe()); }
            else rows.Add(R(3, "How the internet reaches you", "turn on Online lookups (Settings > Online lookups) to see the AS paths other networks use to reach you", "", "u"));
            string me = Online.On ? Online.MyIp() : "";
            rows.Add(R(3, "From the other side", "on a server you control:  mtr -rwc 20 " + (me != "" ? me : "<your public IP>") + "    or    tracepath " + (me != "" ? me : "<your public IP>"), "The only way to see every router on the way back is to trace from the far end toward you.", "i"));
            return rows;
        }
    }
}
