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
        public sealed class Run { public volatile string Status = ""; public volatile bool Stop; public readonly List<double> Series = new List<double>(); public string SeriesUnit = ""; public void Add(double v) { lock (Series) Series.Add(v); } public List<double> Copy() { lock (Series) return Series.ToList(); } }

        // ================================================================== iperf3 client
        const int Cookie = 37;
        // iperf3 options, the same spelling as iperf3 itself: -R -P n -t n -u -b 50M -l 1400 -w 256K -S 0xB8 -O 2 -M 1400 -p 5201 -4 -6, plus --both
        public sealed class Opt { public bool Udp, Reverse, Both, V6, V4; public int Par = 1, Secs = 10, Omit, Mss, Tos, Port = 5201; public long Rate = 50000000, Len, Window; public List<string> Ignored = new List<string>(); }
        public static Opt ParseOpts(string text)
        {
            var o = new Opt(); var t = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).ToList();
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
                    case "-p": case "--port": if (int.TryParse(v, out n)) { o.Port = n; i++; } break;
                    case "--parallel": if (int.TryParse(v, out n)) { o.Par = Math.Max(1, Math.Min(16, n)); i++; } break;
                    case "-t": case "--time": if (int.TryParse(v, out n)) { o.Secs = Math.Max(1, Math.Min(120, n)); i++; } break;
                    case "-o": case "--omit": if (int.TryParse(v, out n)) { o.Omit = Math.Max(0, Math.Min(10, n)); i++; } break;
                    case "-m": case "--set-mss": if (int.TryParse(v, out n)) { o.Mss = Math.Max(0, Math.Min(9000, n)); i++; } break;
                    case "-b": case "--bitrate": case "--bandwidth": { long r = size(v); if (r > 0) { o.Rate = r; i++; } break; }
                    case "-l": case "--length": { long r = size(v); if (r > 0) { o.Len = Math.Min(r, 1 << 20); i++; } break; }
                    case "-w": case "--window": { long r = size(v); if (r > 0) { o.Window = Math.Min(r, 16 << 20); i++; } break; }
                    case "--tos": if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? int.TryParse(v.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n) : int.TryParse(v, out n)) { o.Tos = n & 255; i++; } break;
                    default:
                        var m = System.Text.RegularExpressions.Regex.Match(a, @"^(\d+)m$");
                        if (m.Success && o.Udp) o.Rate = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 1000000; else o.Ignored.Add(t[i]);
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
            int c = host.LastIndexOf(':'); if (c > 0 && host.IndexOf(':') == c) { int pp; if (int.TryParse(host.Substring(c + 1), out pp)) o.Port = pp; host = host.Substring(0, c); }
            if (host == "") { rows.Add(R(2, "Server", "type your iperf3 server, e.g. 10.0.0.5 or lab.example.com:5201", "On the other machine run:  iperf3 -s   (AdeeboMeter is only the client - it never listens)", "u")); return rows; }
            run.SeriesUnit = "Mb/s";
            rows.Add(R(3, "Settings", (o.Udp ? "UDP at " + (o.Rate / 1e6).ToString("0.#", CultureInfo.CurrentCulture) + " Mb/s" : "TCP") + " · " + o.Par + " stream" + (o.Par == 1 ? "" : "s") + " · " + o.Secs + " s" + (o.Omit > 0 ? " (+" + o.Omit + " s warm-up not counted)" : "") + (o.Len > 0 ? " · " + o.Len + "-byte writes" : "") + (o.Window > 0 ? " · window " + (o.Window / 1024) + " KB" : "") + (o.Mss > 0 ? " · MSS " + o.Mss : "") + (o.Tos > 0 ? " · TOS 0x" + o.Tos.ToString("X2", CultureInfo.InvariantCulture) : "") + " · port " + o.Port,
                o.Ignored.Count > 0 ? "Not understood and ignored: " + string.Join(" ", o.Ignored) + " (supported: -R -P -t -u -b -l -w -S -O -M -p -4 -6 --both)" : o.Tos > 0 ? "Windows usually ignores DSCP / TOS set by a program unless a QoS policy allows it - check with a capture." : "", "i"));
            foreach (var rev in o.Both ? new[] { false, true } : new[] { o.Reverse })
            {
                run.Status = (rev ? "Download" : "Upload") + " test to " + host + "...";
                try { rows.AddRange(IperfOne(host, o, rev, run)); }
                catch (Exception e) { rows.Add(R(2, rev ? "Download" : "Upload", "failed: " + Draw.Trunc(e.Message, 90), "Is  iperf3 -s  running there, and is TCP " + o.Port + " allowed through its firewall?", "u")); }
                if (run.Stop) break;
            }
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
                long udpSeqMax = 0, atOmit = 0; double lastTransit = double.NaN; var lockU = new object();
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
                                var s = new Socket(addr.AddressFamily, SocketType.Dgram, ProtocolType.Udp); Tune(s, o); s.Connect(addr, port); s.ReceiveTimeout = 3000;
                                s.Send(new byte[] { 0x39, 0x38, 0x37, 0x36 });   // UDP_CONNECT_MSG 0x36373839, sent in the server's (little-endian) byte order var rb = new byte[4]; try { s.Receive(rb); } catch (SocketException) { throw new InvalidOperationException("UDP to the server is blocked (no reply on UDP " + port + ")"); }
                                streams.Add(s);
                            }
                            else { var s = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp); Tune(s, o); s.Connect(addr, port); s.Send(cookie); streams.Add(s); }
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
                                                lock (lockU) { pkts++; if (sq > udpSeqMax + 1) lost += sq - udpSeqMax - 1; if (sq > udpSeqMax) udpSeqMax = sq; if (!double.IsNaN(lastTransit)) jit += (Math.Abs(transit - lastTransit) - jit) / 16; lastTransit = transit; }
                                            }
                                        }
                                    }
                                }
                                catch (SocketException) { }
                                catch (ObjectDisposedException) { }
                            }) { IsBackground = true };
                            threads.Add(t); t.Start();
                        }
                        long prev = 0;
                        for (int i = 0; i < secs && !run.Stop; i++) { Thread.Sleep(1000); long b = Interlocked.Read(ref bytes[0]); run.Add((b - prev) * 8 / 1e6); prev = b; if (i + 1 == o.Omit) atOmit = b; run.Status = (reverse ? "Download " : "Upload ") + (i + 1) + " / " + secs + " s"; }
                        foreach (var t in threads) t.Join(3000);
                        sw.Stop();
                        cs.WriteByte(4);   // TEST_END
                    }
                    else if (st == 13)   // EXCHANGE_RESULTS
                    {
                        var sb = new StringBuilder("{\"cpu_util_total\":0,\"cpu_util_user\":0,\"cpu_util_system\":0,\"sender_has_retransmits\":-1,\"streams\":[");
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
                        rows.Add(R(0, dir, mbps.ToString("0.0", CultureInfo.CurrentCulture) + " Mb/s" + (par > 1 ? " over " + par + " streams" : "") + (udp ? "" : " (TCP)"), Explain(mbps), "m"));
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
                foreach (var s in streams) try { s.Close(); } catch { }
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
        static readonly Dictionary<string, int> Types = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "A", 1 }, { "NS", 2 }, { "CNAME", 5 }, { "SOA", 6 }, { "PTR", 12 }, { "MX", 15 }, { "TXT", 16 }, { "AAAA", 28 }, { "SRV", 33 }, { "CAA", 257 } };
        public sealed class Rr { public string Name = "", Type = "", Data = ""; public int Ttl; public int Section; }
        public sealed class Msg { public int Rcode; public bool Aa, Ad, Tc; public List<Rr> Rrs = new List<Rr>(); public string From = ""; public long Ms; }
        public static List<string[]> Dig(string name, string type, string server, Monitor m)
        {
            var rows = new List<string[]>(); name = (name ?? "").Trim().TrimEnd('.'); type = string.IsNullOrWhiteSpace(type) ? "A" : type.Trim().ToUpperInvariant();
            bool trace = type == "TRACE"; if (trace) type = "A";
            int t; if (!Types.TryGetValue(type, out t)) { rows.Add(R(2, "Type", type + " isn't supported - use A, AAAA, MX, TXT, NS, CNAME, SOA, SRV, CAA, PTR or TRACE", "", "u")); return rows; }
            if (name == "" || name.Length > 253) { rows.Add(R(2, "Name", "type a name, e.g. github.com", "", "u")); return rows; }
            IPAddress ipq; if (t == 12 && IPAddress.TryParse(name, out ipq) && ipq.AddressFamily == AddressFamily.InterNetwork) name = string.Join(".", ipq.GetAddressBytes().Reverse()) + ".in-addr.arpa";
            if (trace) return Trace(name, rows);
            IPAddress srv; string dns; lock (m.L) dns = m.DnsServer;
            if (!IPAddress.TryParse((server ?? "").Trim().TrimStart('@'), out srv) && !IPAddress.TryParse((dns ?? "").Split(',')[0].Trim(), out srv)) { rows.Add(R(2, "Server", "no DNS server to ask - type one, e.g. 1.1.1.1", "", "u")); return rows; }
            var msg = Query(srv, name, t, true);
            if (msg == null) { rows.Add(R(2, "Answer", "no answer from " + srv + " in 3 s", "", "m")); return rows; }
            rows.Add(R(msg.Rcode == 0 ? 0 : msg.Rcode == 3 ? 1 : 2, "Status", (msg.Rcode == 0 ? "NOERROR" : msg.Rcode == 3 ? "NXDOMAIN - the name doesn't exist" : msg.Rcode == 2 ? "SERVFAIL - the server couldn't get an answer" : "error " + msg.Rcode) + " from " + srv + " in " + msg.Ms + " ms" + (msg.Aa ? " · authoritative" : "") + (msg.Ad ? " · DNSSEC validated (AD)" : ""), msg.Rcode == 2 ? "Often a DNSSEC failure or the domain's own servers being down." : "", "m"));
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
            var q = new List<byte> { id[0], id[1], (byte)(rd ? 1 : 0), 0, 0, 1, 0, 0, 0, 0, 0, 1 };
            foreach (var label in name.Split('.')) { var b = Encoding.ASCII.GetBytes(label); if (b.Length == 0 || b.Length > 63) return null; q.Add((byte)b.Length); q.AddRange(b); }
            q.Add(0); q.Add((byte)(type >> 8)); q.Add((byte)type); q.Add(0); q.Add(1);
            q.AddRange(new byte[] { 0, 0, 41, 0x10, 0, 0, 0, 0x80, 0, 0, 0 });   // EDNS0, 4096 bytes, DO bit
            var sw = Stopwatch.StartNew(); byte[] resp = null;
            try
            {
                using (var u = new UdpClient(server.AddressFamily))
                {
                    u.Client.ReceiveTimeout = 3000; u.Send(q.ToArray(), q.Count, new IPEndPoint(server, 53));
                    var from = new IPEndPoint(IPAddress.Any, 0); resp = u.Receive(ref from);
                    if (resp.Length < 12 || resp[0] != id[0] || resp[1] != id[1]) return null;
                }
                if ((resp[2] & 2) != 0)   // truncated: ask again over TCP
                    using (var tc = new TcpClient(server.AddressFamily))
                    {
                        if (!tc.ConnectAsync(server, 53).Wait(3000)) return null; var s = tc.GetStream(); s.ReadTimeout = 3000;
                        var l = new[] { (byte)(q.Count >> 8), (byte)q.Count }; s.Write(l, 0, 2); s.Write(q.ToArray(), 0, q.Count);
                        var lb = ReadN(s, 2); resp = ReadN(s, lb[0] << 8 | lb[1]);
                    }
            }
            catch (SocketException) { return null; }
            catch (InvalidOperationException) { return null; }
            var msg = new Msg { Ms = sw.ElapsedMilliseconds, From = server.ToString(), Aa = (resp[2] & 4) != 0, Tc = (resp[2] & 2) != 0, Ad = (resp[3] & 0x20) != 0, Rcode = resp[3] & 15 };
            int qd = resp[4] << 8 | resp[5], an = resp[6] << 8 | resp[7], ns = resp[8] << 8 | resp[9], ar = resp[10] << 8 | resp[11], p = 12;
            try
            {
                for (int i = 0; i < qd; i++) { Name(resp, ref p); p += 4; }
                for (int sec = 1; sec <= 3; sec++)
                    for (int i = 0; i < (sec == 1 ? an : sec == 2 ? ns : ar); i++)
                    {
                        var r = new Rr { Section = sec, Name = Name(resp, ref p) }; int ty = resp[p] << 8 | resp[p + 1]; r.Ttl = resp[p + 4] << 24 | resp[p + 5] << 16 | resp[p + 6] << 8 | resp[p + 7]; int len = resp[p + 8] << 8 | resp[p + 9]; p += 10; int d = p;
                        r.Type = Types.FirstOrDefault(x => x.Value == ty).Key ?? (ty == 41 ? "OPT" : ty == 46 ? "RRSIG" : "type" + ty);
                        switch (ty)
                        {
                            case 1: r.Data = new IPAddress(new[] { resp[d], resp[d + 1], resp[d + 2], resp[d + 3] }).ToString(); break;
                            case 28: { var b = new byte[16]; Array.Copy(resp, d, b, 0, 16); r.Data = new IPAddress(b).ToString(); break; }
                            case 2: case 5: case 12: { int x = d; r.Data = Name(resp, ref x); break; }
                            case 15: { int x = d + 2; r.Data = (resp[d] << 8 | resp[d + 1]) + " " + Name(resp, ref x); break; }
                            case 33: { int x = d + 6; r.Data = "priority " + (resp[d] << 8 | resp[d + 1]) + " weight " + (resp[d + 2] << 8 | resp[d + 3]) + " port " + (resp[d + 4] << 8 | resp[d + 5]) + " → " + Name(resp, ref x); break; }
                            case 6: { int x = d; string mn = Name(resp, ref x), rn = Name(resp, ref x); r.Data = mn + " " + rn + " serial " + ((uint)(resp[x] << 24 | resp[x + 1] << 16 | resp[x + 2] << 8 | resp[x + 3])); break; }
                            case 16: { var sb = new StringBuilder(); int x = d; while (x < d + len) { int l = resp[x++]; sb.Append('"').Append(Encoding.UTF8.GetString(resp, x, Math.Min(l, d + len - x))).Append("\" "); x += l; } r.Data = sb.ToString().Trim(); break; }
                            case 257: { int tl = resp[d + 1]; r.Data = (resp[d] != 0 ? "critical " : "") + Encoding.ASCII.GetString(resp, d + 2, tl) + " \"" + Encoding.ASCII.GetString(resp, d + 2 + tl, len - 2 - tl) + "\""; break; }
                            default: r.Data = len + " bytes"; break;
                        }
                        p = d + len; if (ty != 41) msg.Rrs.Add(r);
                    }
            }
            catch (IndexOutOfRangeException) { }
            catch (ArgumentException) { }
            return msg;
        }
        static string Name(byte[] b, ref int p)
        {
            var parts = new List<string>(); int jumps = 0, pos = p; bool jumped = false;
            while (pos < b.Length && jumps < 20)
            {
                int l = b[pos];
                if (l == 0) { pos++; break; }
                if ((l & 0xC0) == 0xC0) { if (!jumped) p = pos + 2; pos = (l & 0x3F) << 8 | b[pos + 1]; jumped = true; jumps++; continue; }
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
                        foreach (var h in new[] { "Server", "Strict-Transport-Security", "Alt-Svc", "Content-Type", "Cache-Control", "Via", "X-Cache", "CF-Ray" })
                            if (!string.IsNullOrEmpty(resp.Headers[h])) rows.Add(R(3, h, Draw.Trunc(resp.Headers[h], 120), h == "Alt-Svc" && resp.Headers[h].Contains("h3") ? "The site also offers HTTP/3 (QUIC over UDP 443) - browsers switch to it after the first visit." : h == "Strict-Transport-Security" ? "HSTS: browsers will only ever use HTTPS for this site." : "", "m"));
                        break;
                    }
                }
                catch (Exception e) { rows.Add(R(2, "Request", Draw.Trunc(e.Message, 100), "", "m")); break; }
            }
            if (u.Scheme == "https") rows.AddRange(Tls(u.Host, u.Port));
            return rows;
        }
        static List<string[]> Tls(string host, int port)
        {
            var rows = new List<string[]>(); X509Certificate2 cert = null; SslPolicyErrors errs = SslPolicyErrors.None;
            foreach (var proto in new[] { Tuple.Create("TLS 1.3", (SslProtocols)12288), Tuple.Create("TLS 1.2", SslProtocols.Tls12) })
            {
                try
                {
                    using (var tc = new TcpClient())
                    {
                        if (!tc.ConnectAsync(host, port).Wait(5000)) { rows.Add(R(2, proto.Item1, "couldn't connect", "", "m")); continue; }
                        using (var ssl = new SslStream(tc.GetStream(), false, (s, c, ch, e) => { if (cert == null && c != null) cert = new X509Certificate2(c); errs = e; return e == SslPolicyErrors.None; }))
                        { ssl.ReadTimeout = 5000; ssl.AuthenticateAsClient(host, null, proto.Item2, true); rows.Add(R(0, proto.Item1, "supported · " + ssl.CipherAlgorithm + " " + ssl.CipherStrength + "-bit", "", "m")); }
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
            { rows.Add(R(1, "Company network?", "this PC is on a company domain or VPN", "Sweeping a company network can trip the security team's alarms. Type  yes  in the second box and press Run if you're sure (home networks are fine).", "i")); return rows; }
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
            foreach (var th in workers) th.Join(60000);
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
        public static List<string[]> Wake(string macIn)
        {
            var rows = new List<string[]>(); string hex = new string((macIn ?? "").Where(Uri.IsHexDigit).ToArray());
            if (hex.Length != 12) { rows.Add(R(2, "Hardware address", "type the PC's MAC, e.g. 3C:84:6A:12:34:56", "Your Wi-Fi > devices on your network lists the ones your PC has seen.", "u")); return rows; }
            var mac = Enumerable.Range(0, 6).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
            var pkt = new byte[102]; for (int i = 0; i < 6; i++) pkt[i] = 0xFF; for (int i = 1; i <= 16; i++) Array.Copy(mac, 0, pkt, i * 6, 6);
            using (var u = new UdpClient()) { u.EnableBroadcast = true; u.Send(pkt, pkt.Length, new IPEndPoint(IPAddress.Broadcast, 9)); u.Send(pkt, pkt.Length, new IPEndPoint(IPAddress.Broadcast, 7)); }
            rows.Add(R(0, "Sent", "magic packet for " + string.Join(":", mac.Select(b => b.ToString("X2", CultureInfo.InvariantCulture))) + " to your network", "It only wakes PCs whose network card and BIOS allow Wake-on-LAN, on the same network (not over the internet or most VPNs).", "m"));
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
            else rows.Add(R(3, "How the internet reaches you", "turn on Online lookups (Settings > Privacy) to see the AS paths other networks use to reach you", "", "u"));
            string me = Online.On ? Online.MyIp() : "";
            rows.Add(R(3, "From the other side", "on a server you control:  mtr -rwc 20 " + (me != "" ? me : "<your public IP>") + "    or    tracepath " + (me != "" ? me : "<your public IP>"), "The only way to see every router on the way back is to trace from the far end toward you.", "i"));
            return rows;
        }
    }
}
