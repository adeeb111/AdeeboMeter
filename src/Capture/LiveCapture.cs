// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // ================================================================== Live capture: Wireshark's tshark, only while you press Start
    // Runs only on the Live capture tab, stops when you press Stop, leave the tab, close the view or after 15 minutes.
    // tshark decodes (-n: no name lookups, -p: only this PC's own traffic, -M: session reset to keep memory flat);
    // dumpcap keeps a small ring buffer (5 x 20 MB) so Sniff can include what happened before you clicked.
    sealed class LiveCapture : IDisposable
    {
        static readonly char[] Comma = { ',' };
        static readonly string[] TestLocal = { "10.0.0.82", "172.17.0.2", "192.0.2.2" };
        long bad;   // malformed lines dropped
        public long Dropped { get { lock (k) return bad; } }
        static int Num(string f) { int v; return int.TryParse(First(f), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; }
        public sealed class Conv { public string Key, Local = "", Remote = "", Proto = "", Host = "", L4 = ""; public int LocalPort, RemotePort; public long Bytes, Pkts, Prev; public int Retx; public DateTime Last; public double Rate; }
        public sealed class View
        {
            public List<string[]> Phs = new List<string[]>();     // depth, name, pct, bytes
            public List<Conv> Top = new List<Conv>();
            public List<string[]> Expert = new List<string[]>();  // level, text, count
            public long Pkts, Bytes; public double Pps; public string State = ""; public long TsharkMB;
            public List<string[]> ByApp = new List<string[]>();   // app, bytes, rate Mb/s, share %
            public List<string[]> Calls = new List<string[]>();   // direction + far end, kind, packets, loss, jitter, MOS, level
            public List<string> DhcpServers = new List<string>(); public bool Extras;
        }
        sealed class Rtp { public string Dir = "", Remote = ""; public long Count, BaseExt = -1, MaxExt, Cycles, LastSeq = -1; public double FirstT, FirstTs, PrevT, PrevTs, Rate, Jitter; }
        readonly Dictionary<string, Rtp> rtp = new Dictionary<string, Rtp>(); readonly HashSet<string> dhcp = new HashSet<string>();
        static bool? extrasOk; bool usingExtras; string startGuid = "";
        static readonly string[] extraFields = { "frame.time_relative", "rtp.ssrc", "rtp.seq", "rtp.timestamp", "dhcp.option.dhcp_server_id" };
        static readonly double[] Clocks = { 8000, 16000, 24000, 32000, 48000, 90000 };
        public static readonly string WsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Wireshark");
        public static string Tshark { get { return Path.Combine(WsDir, "tshark.exe"); } }
        public static string Dumpcap { get { return Path.Combine(WsDir, "dumpcap.exe"); } }
        public static string Mergecap { get { return Path.Combine(WsDir, "mergecap.exe"); } }
        public static bool Npcap { get { return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Npcap", "wpcap.dll")); } }
        public static bool Available { get { return Npcap && File.Exists(Tshark) && File.Exists(Dumpcap); } }
        public static string RingDir { get { return Path.Combine(Program.CaptureDir, "ring"); } }

        readonly object k = new object();
        Process ts, ring; volatile bool running; DateTime started, lastView = DateTime.UtcNow; string iface = "";
        Dictionary<string, long[]> phs = new Dictionary<string, long[]>();
        Dictionary<string, Conv> convs = new Dictionary<string, Conv>();
        Dictionary<string, string> names = new Dictionary<string, string>();
        Dictionary<string, int> expert = new Dictionary<string, int>();
        HashSet<string> local = new HashSet<string>();
        long pkts, bytes, prevPkts; public string Error = "";
        public bool Running { get { return running; } }
        public bool RingActive { get { return running && ring != null && !ring.HasExited; } }
        public TimeSpan Elapsed { get { return running ? DateTime.UtcNow - started : TimeSpan.Zero; } }
        const int MaxConvs = 3000, MaxNames = 5000; static int MaxMinutes { get { return Cfg.LiveMin; } }

        static readonly string[] fields = { "frame.len", "frame.protocols", "ip.src", "ip.dst", "ipv6.src", "ipv6.dst", "tcp.srcport", "tcp.dstport", "udp.srcport", "udp.dstport",
            "tls.handshake.extensions_server_name", "dns.qry.name", "dns.a", "dns.aaaa", "_ws.expert.message", "_ws.col.Protocol" };

        readonly object life = new object(); Thread reader;   // Start and Stop never overlap (a restart can come from three places)
        public bool Start(string ifaceGuid) { if (Policy.NoCapture) { Program.Log("live capture: off by policy"); return false; } lock (life) return Start2(ifaceGuid); }
        bool Start2(string ifaceGuid)
        {
            Stop();
            Guid g; if (!Guid.TryParse((ifaceGuid ?? "").Trim('{', '}'), out g)) { Error = "No active adapter to capture on."; return false; }
            if (!Available) { Error = Voice.P("Live capture needs Wireshark (tshark) and Npcap - not on this PC.", "Live capture needs Wireshark and Npcap."); return false; }
            iface = "\\Device\\NPF_{" + g.ToString().ToUpperInvariant() + "}"; startGuid = g.ToString();
            lock (k) { phs.Clear(); convs.Clear(); expert.Clear(); rtp.Clear(); dhcp.Clear(); pkts = bytes = prevPkts = 0; }
            local = new HashSet<string>();
            try { foreach (var n in NetworkInterface.GetAllNetworkInterfaces()) foreach (var ua in n.GetIPProperties().UnicastAddresses) local.Add(ua.Address.ToString().Split('%')[0]); } catch { }
            Error = ""; RingError = ""; started = DateTime.UtcNow; running = true;
            try
            {
                var sb = new StringBuilder("-i \"" + iface + "\" -p -n -l -M 100000 -T fields -E separator=/t -E occurrence=a -E aggregator=, -E quote=n");
                foreach (var f in fields) sb.Append(" -e ").Append(f);
                // Extra fields for call quality and DHCP. Older Wireshark versions may not know them - then tshark refuses
                // to start, and we quietly start again without them.
                usingExtras = extrasOk != false;
                if (usingExtras) { foreach (var f in extraFields) sb.Append(" -e ").Append(f); sb.Append(" --enable-heuristic rtp_udp"); }
                var psi = new ProcessStartInfo(Tshark, sb.ToString()) { WorkingDirectory = WsDir, StandardOutputEncoding = System.Text.Encoding.UTF8, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                ts = Process.Start(psi); Job.Add(ts);
                try { ts.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                ts.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null || e.Data.IndexOf("Capturing on", StringComparison.Ordinal) >= 0 || e.Data.Trim() == "") return;
                    string d = e.Data;
                    // only tshark's own "unknown field / heuristic" complaints - an "Invalid argument" or "not a valid interface" must not
                    // switch call quality and DHCP detection off for the session
                    if (usingExtras && (d.IndexOf("aren't valid", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("isn't a valid field", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("are not valid", StringComparison.OrdinalIgnoreCase) >= 0 || (d.IndexOf("heuristic", StringComparison.OrdinalIgnoreCase) >= 0 && d.IndexOf("not", StringComparison.OrdinalIgnoreCase) >= 0) || d.IndexOf("unrecognized option", StringComparison.OrdinalIgnoreCase) >= 0))
                    { extrasOk = false; usingExtras = false; string gid = startGuid; Program.Log("tshark: extra fields not supported, restarting without them"); Restart(gid); return; }
                    Error = Draw.Trunc(d.Trim(), 140);
                };
                ts.BeginErrorReadLine();
                var t = new Thread(Reader) { IsBackground = true, Name = "tshark", Priority = ThreadPriority.BelowNormal }; reader = t; t.Start();
                // ring buffer for Sniff
                try { Directory.CreateDirectory(RingDir); foreach (var f in Directory.GetFiles(RingDir)) try { File.Delete(f); } catch { } } catch { }
                var rp = new ProcessStartInfo(Dumpcap, "-i \"" + iface + "\" -p -q -b filesize:20000 -b files:5 -w \"" + Path.Combine(RingDir, "ring.pcapng") + "\"") { WorkingDirectory = Path.GetDirectoryName(Dumpcap), UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                ring = Process.Start(rp); Job.Add(ring);
                ring.ErrorDataReceived += delegate(object s2, DataReceivedEventArgs e2)
                {
                    string d2 = e2.Data ?? ""; if (d2.Trim() == "" || d2.IndexOf("Capturing on", StringComparison.Ordinal) >= 0 || d2.IndexOf("File:", StringComparison.Ordinal) >= 0) return;
                    if (d2.IndexOf("rror", StringComparison.Ordinal) >= 0 || d2.IndexOf("space", StringComparison.OrdinalIgnoreCase) >= 0) { RingError = Draw.Trunc(d2.Trim(), 140); Program.Log("dumpcap ring: " + d2); }
                };
                ring.BeginErrorReadLine();
                if (limit != null) limit.Dispose(); limit = new System.Threading.Timer(delegate { try { CheckLimit(); } catch (Exception ex) { Program.Log("capture limit: " + ex.Message); } }, null, 30000, 30000);
                try { ring.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                Program.Log("Live capture started");
                return true;
            }
            catch (Exception e) { Error = "Could not start tshark: " + e.Message; Stop(); return false; }
        }

        public volatile string RingError = "";   // the Sniff ring buffer's own trouble (disk full, adapter gone) - Sniff says it instead of switching path quietly
        public void Stop() { lock (life) Stop2(); }
        public void Dispose() { Stop(); }
        volatile int gen;   // bumped by every stop: a delayed self-restart from before it is dropped, so a capture you stopped stays stopped
        void Restart(string guid) { int g = gen; ThreadPool.QueueUserWorkItem(delegate { Thread.Sleep(300); lock (life) { if (g == gen) Start2(guid); } }); }
        void Stop2()
        {
            gen++;
            bool was = running; running = false;
            if (limit != null) { limit.Dispose(); limit = null; }
            // tshark runs its own dumpcap: stop that child too, or it keeps the adapter until its next write fails
            var kids = new List<int>();
            if (ts != null) try { int tid = ts.Id; Dictionary<int, ProcTree.P> by; ProcTree.Snapshot(out by); kids.AddRange(by.Values.Where(x => x.Parent == tid && string.Equals(x.Name, "dumpcap.exe", StringComparison.OrdinalIgnoreCase)).Select(x => x.Pid)); } catch (InvalidOperationException) { }
            foreach (var p in new[] { ts, ring })
                if (p != null) { try { if (!p.HasExited) { p.Kill(); p.WaitForExit(2000); } } catch { } try { p.Dispose(); } catch { } }
            foreach (int k2 in kids) try { using (var kp = Process.GetProcessById(k2)) if (string.Equals(kp.ProcessName, "dumpcap", StringComparison.OrdinalIgnoreCase) && !kp.HasExited) { kp.Kill(); kp.WaitForExit(1000); } } catch (ArgumentException) { } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
            var rd = reader; reader = null; if (rd != null && rd != Thread.CurrentThread) rd.Join(2000);   // tshark is gone, so its reader ends
            ts = null; ring = null;
            try { if (Directory.Exists(RingDir)) foreach (var f in Directory.GetFiles(RingDir)) try { File.Delete(f); } catch { } } catch { }
            lock (k) { convs.Clear(); names.Clear(); phs.Clear(); expert.Clear(); rtp.Clear(); dhcp.Clear(); }
            if (was) Program.Log("Live capture stopped");
        }

        void Reader()
        {
            var p = ts; if (p == null) return;
            try
            {
                string line;
                while (running && (line = p.StandardOutput.ReadLine()) != null)
                {
                    try { Parse(line); } catch { }
                    if ((DateTime.UtcNow - started).TotalMinutes > MaxMinutes) { Error = Voice.P("Stopped after " + MaxMinutes + " minutes so it doesn't nibble your CPU forever. Press Start for more.", "Stopped automatically after " + MaxMinutes + " minutes."); running = false; break; }
                }
            }
            catch { }
            if (!running) ThreadPool.QueueUserWorkItem(delegate { Stop(); });
            // tshark ended on its own (sleep, an adapter reset, Wi-Fi gone) while we still think we're capturing: say so and stop,
            // never "capturing 47:12" with nothing coming in. (A restart without the extra fields swaps ts first - not this.)
            else if (p == ts) { Error = "Capture stopped by itself - the adapter went away or the PC slept. Press Start again."; ThreadPool.QueueUserWorkItem(delegate { Stop(); }); }
        }
        // the time limit holds even when no packets arrive (the reader only wakes up on a line)
        System.Threading.Timer limit;
        void CheckLimit()
        {
            if (running && (DateTime.UtcNow - started).TotalMinutes > MaxMinutes) { Error = "Stopped after " + MaxMinutes + " minutes so it doesn't nibble your CPU forever. Press Start for more."; Stop(); }
        }

        public void FeedForTest(string line) { if (local.Count == 0) local = new HashSet<string>(TestLocal); Parse(line); }
        public void SetLocalForTest(IEnumerable<string> ips) { local = new HashSet<string>(ips); }

        static string First(string v) { if (string.IsNullOrEmpty(v)) return ""; int i = v.IndexOf(','); return i < 0 ? v : v.Substring(0, i); }

        void Parse(string line)
        {
            var f = line.Split('\t'); if (f.Length < 16) return;
            long len; if (!long.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out len)) len = 0;
            string src = First(f[2]), dst = First(f[3]); if (src == "") { src = First(f[4]); dst = First(f[5]); }
            int et = f[1].IndexOf("ethertype:", StringComparison.Ordinal);
            string proto = et >= 0 ? f[1].Substring(et + 10) : f[1];
            int sp = 0, dp = 0; string l4 = "";
            if (f[6] != "") { sp = Num(f[6]); dp = Num(f[7]); l4 = "TCP"; }
            else if (f[8] != "") { sp = Num(f[8]); dp = Num(f[9]); l4 = "UDP"; }
            // a line that can't be a real frame is dropped, never counted: frame size, addresses and ports must make sense
            IPAddress ia;
            if (len < 0 || len > 262144 || (src != "" && !IPAddress.TryParse(src, out ia)) || (dst != "" && !IPAddress.TryParse(dst, out ia)) || sp < 0 || sp > 65535 || dp < 0 || dp > 65535) { lock (k) bad++; return; }
            lock (k)
            {
                pkts++; bytes += len;
                // protocol hierarchy (same idea as tshark -z io,phs)
                var segs = proto.Split(':'); string path = "";
                for (int i = 0; i < segs.Length && i < 6; i++)
                {
                    if (segs[i] == "data" && i > 0) break;
                    path = path == "" ? segs[i] : path + ":" + segs[i];
                    long[] c; if (!phs.TryGetValue(path, out c)) { if (phs.Count > 400) break; c = new long[2]; phs[path] = c; }
                    c[0]++; c[1] += len;
                }
                // names: TLS server name and DNS answers - learned from the traffic itself, nothing looked up
                if (f[11] != "" && names.Count < MaxNames)
                    foreach (var ip in (f[12] + "," + f[13]).Split(Comma, StringSplitOptions.RemoveEmptyEntries)) names[ip] = First(f[11]);
                // expert info, Wireshark's own diagnosis
                bool retx = false;
                if (f[14] != "")
                {
                    string m = f[14], cat = null;
                    if (m.IndexOf("retransmission", StringComparison.OrdinalIgnoreCase) >= 0) { cat = "TCP retransmissions"; retx = true; }
                    else if (m.IndexOf("Duplicate ACK", StringComparison.OrdinalIgnoreCase) >= 0) cat = "Duplicate ACKs";
                    else if (m.IndexOf("zero window", StringComparison.OrdinalIgnoreCase) >= 0) cat = "Zero window";
                    else if (m.IndexOf("Previous segment not captured", StringComparison.OrdinalIgnoreCase) >= 0) cat = "Segments lost before capture";
                    else if (m.IndexOf("out-of-order", StringComparison.OrdinalIgnoreCase) >= 0) cat = "Out of order";
                    else if (m.IndexOf("Connection reset", StringComparison.OrdinalIgnoreCase) >= 0) cat = "Connection resets (RST)";
                    else if (m.IndexOf("Alert", StringComparison.OrdinalIgnoreCase) >= 0 && proto.Contains("tls")) cat = "TLS alerts";
                    if (cat != null) { int n; expert.TryGetValue(cat, out n); expert[cat] = n + 1; }
                }
                if (f.Length >= 21)
                {
                    if (extrasOk == null && pkts > 20) extrasOk = true;
                    string ds = First(f[20]); if (ds != "" && dhcp.Count < 16) dhcp.Add(ds);
                    if (f[17] != "" && f[18] != "" && src != "") AddRtp(First(f[17]), local.Contains(dst) ? "in" : "out", local.Contains(dst) ? src : dst, f[16], First(f[18]), First(f[19]));
                }
                if (src == "" || l4 == "") return;
                if (src.StartsWith("127.", StringComparison.Ordinal) || dst.StartsWith("127.", StringComparison.Ordinal) || src == "::1" || dst == "::1") return;
                bool srcLocal = local.Contains(src);
                string lo = srcLocal ? src : dst, re = srcLocal ? dst : src; int lp = srcLocal ? sp : dp, rp = srcLocal ? dp : sp;
                string key = l4 + "|" + lo + ":" + lp + "|" + re + ":" + rp;
                Conv cv;
                if (!convs.TryGetValue(key, out cv))
                {
                    if (convs.Count >= MaxConvs) { var oldest = convs.Values.OrderBy(x => x.Last).Take(300).Select(x => x.Key).ToList(); foreach (var o in oldest) convs.Remove(o); }
                    cv = new Conv { Key = key, Local = lo, Remote = re, LocalPort = lp, RemotePort = rp, L4 = l4 }; convs[key] = cv;
                }
                cv.Bytes += len; cv.Pkts++; cv.Last = DateTime.UtcNow; if (retx) cv.Retx++;
                // keep the most telling protocol name: a bare ACK ("TCP") must not overwrite "TLSv1.3"
                if (f[15] != "") { string np = First(f[15]); if (cv.Proto == "" || (np != "TCP" && np != "UDP")) cv.Proto = np; }
                string sni = First(f[10]); if (sni != "") { cv.Host = sni; if (names.Count < MaxNames) names[re] = sni; }
            }
        }

        // RTP: sequence numbers give loss; arrival time vs. RTP timestamp gives jitter (RFC 3550), with the media clock
        // worked out from the stream itself and snapped to the usual rates.
        void AddRtp(string ssrc, string dir, string remote, string tRel, string seqS, string tsS)
        {
            double t; long seq, ts;
            if (!double.TryParse(tRel, NumberStyles.Float, CultureInfo.InvariantCulture, out t) || !long.TryParse(seqS, NumberStyles.Integer, CultureInfo.InvariantCulture, out seq) || !long.TryParse(tsS, NumberStyles.Integer, CultureInfo.InvariantCulture, out ts)) return;
            string key = dir + "|" + ssrc; Rtp r;
            if (!rtp.TryGetValue(key, out r)) { if (rtp.Count >= 60) return; r = new Rtp { Dir = dir, Remote = remote, FirstT = t, FirstTs = ts }; rtp[key] = r; }
            // a real wrap: the old number was near the top (65535) and the new one near the bottom. Anything else that goes
            // backwards a long way is the sender starting its numbering again - not 65,536 lost packets
            if (r.LastSeq >= 0 && seq < r.LastSeq && r.LastSeq - seq > 30000)
            {
                if (r.LastSeq > 60000 && seq < 5536) r.Cycles++;
                else { r.BaseExt = r.Cycles * 65536 + seq - (r.Count > 0 ? r.Count : 0); r.MaxExt = -1; }
            }
            long ext = r.Cycles * 65536 + seq;
            if (r.BaseExt < 0) r.BaseExt = ext;
            if (ext > r.MaxExt) r.MaxExt = ext;
            // the clock rate, from at least half a second of timestamps (a 30 fps video sends 60 packets well inside that,
            // so it waits for the time, not for the 60th packet)
            if (r.Rate == 0 && r.Count >= 60 && t > r.FirstT + 0.5)
            {
                double est = (ts - r.FirstTs) / (t - r.FirstT); double best = 0, bd = double.MaxValue;
                foreach (var c in Clocks) { double dd = Math.Abs(c - est); if (dd < bd) { bd = dd; best = c; } }
                r.Rate = best;
            }
            if (r.Rate > 0 && r.Count > 0)
            {
                double d = (t - r.PrevT) - (ts - r.PrevTs) / r.Rate;
                if (Math.Abs(d) < 1) r.Jitter += (Math.Abs(d) - r.Jitter) / 16;
            }
            r.Count++; r.LastSeq = seq; r.PrevT = t; r.PrevTs = ts;
        }
        // ITU-T G.107 E-model, simplified: one-way delay plus jitter buffer and packet loss -> R factor -> MOS (1-4.5).
        public static double Mos(double delayMs, double jitterMs, double lossPct)
        {
            double d = delayMs + 2 * jitterMs + 10;
            double id = 0.024 * d + (d > 177.3 ? 0.11 * (d - 177.3) : 0);
            double ie = 95 * lossPct / (lossPct + 10);
            double r = 93.2 - id - ie;
            return r <= 0 ? 1 : r >= 100 ? 4.5 : Math.Min(4.5, 1 + 0.035 * r + 7e-6 * r * (r - 60) * (100 - r));
        }

        // Called once a second by the view; computes rates and copies what the screen needs.
        public View Snapshot(Func<int, string> appByLocalPort) { return Snapshot(appByLocalPort, 30); }
        public View Snapshot(Func<int, string> appByLocalPort, double oneWayMs)
        {
            var v = new View();
            var now = DateTime.UtcNow; double dt = Math.Max(0.2, (now - lastView).TotalSeconds); lastView = now;
            lock (k)
            {
                v.Pkts = pkts; v.Bytes = bytes; v.Pps = (pkts - prevPkts) / dt; prevPkts = pkts;
                foreach (var c in convs.Values) { c.Rate = (c.Bytes - c.Prev) * 8 / dt / 1e6 * 0.6 + c.Rate * 0.4; c.Prev = c.Bytes; string h; if (c.Host == "" && names.TryGetValue(c.Remote, out h)) c.Host = h; }
                v.Top = convs.Values.Where(c => (now - c.Last).TotalSeconds < 30).OrderByDescending(c => c.Rate).ThenByDescending(c => c.Bytes).Take(12)
                    .Select(c => new Conv { Key = c.Key, Local = c.Local, Remote = c.Remote, LocalPort = c.LocalPort, RemotePort = c.RemotePort, Proto = c.Proto, L4 = c.L4, Host = c.Host != "" ? c.Host : c.Remote, Bytes = c.Bytes, Pkts = c.Pkts, Retx = c.Retx, Rate = c.Rate }).ToList();
                double total = Math.Max(1, pkts);
                foreach (var kv in phs.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    double pct = kv.Value[0] * 100.0 / total; if (pct < 0.5) continue;
                    int depth = kv.Key.Count(ch => ch == ':'); string name = kv.Key.Substring(kv.Key.LastIndexOf(':') + 1);
                    v.Phs.Add(new[] { depth.ToString(CultureInfo.CurrentCulture), name, pct.ToString("0.0", CultureInfo.CurrentCulture) + "%", Draw.MemStr(kv.Value[1]) });
                    if (v.Phs.Count >= 18) break;
                }
                // bytes per app: every conversation, owned by the app holding its local port
                if (appByLocalPort != null)
                {
                    var agg = new Dictionary<string, double[]>();
                    foreach (var c in convs.Values)
                    {
                        string a = appByLocalPort(c.LocalPort) ?? "other / closed";
                        double[] x; if (!agg.TryGetValue(a, out x)) { x = new double[2]; agg[a] = x; }
                        x[0] += c.Bytes; if ((now - c.Last).TotalSeconds < 5) x[1] += c.Rate;
                    }
                    double all = Math.Max(1, agg.Values.Sum(x => x[0]));
                    v.ByApp = agg.OrderByDescending(kv => kv.Value[0]).Take(8).Select(kv => new[] { kv.Key, Draw.MemStr((long)kv.Value[0]), kv.Value[1].ToString("0.00", CultureInfo.CurrentCulture), (kv.Value[0] * 100 / all).ToString("0", CultureInfo.CurrentCulture) }).ToList();
                }
                foreach (var r in rtp.Values.Where(x => x.Count >= 100).OrderByDescending(x => x.Count).Take(6))
                {
                    long expected = r.MaxExt - r.BaseExt + 1; double loss = expected > 0 ? Math.Max(0, (expected - r.Count) * 100.0 / expected) : 0;
                    double jit = r.Jitter * 1000, mos = Mos(oneWayMs, jit, loss);
                    int lv = mos >= 4.0 ? 0 : mos >= 3.6 ? 1 : 2;
                    v.Calls.Add(new[] { (r.Dir == "in" ? "from " : "to ") + r.Remote, r.Rate >= 90000 ? "video" : r.Rate > 0 ? "audio" : "media", r.Count.ToString("N0", CultureInfo.CurrentCulture),
                        loss.ToString("0.0", CultureInfo.CurrentCulture) + "%", r.Rate > 0 ? jit.ToString("0.0", CultureInfo.CurrentCulture) + " ms" : "-", mos.ToString("0.0", CultureInfo.CurrentCulture), lv.ToString(CultureInfo.InvariantCulture) });
                }
                v.DhcpServers = dhcp.ToList(); v.Extras = usingExtras;
                foreach (var cat in new[] { "TCP retransmissions", "Duplicate ACKs", "Segments lost before capture", "Out of order", "Zero window", "Connection resets (RST)", "TLS alerts" })
                {
                    int n; expert.TryGetValue(cat, out n);
                    int lv = n == 0 ? 3 : cat == "TCP retransmissions" || cat == "Zero window" ? (n > 50 ? 2 : 1) : 1;
                    v.Expert.Add(new[] { lv.ToString(CultureInfo.CurrentCulture), cat, n.ToString(CultureInfo.CurrentCulture) });
                }
            }
            foreach (var c in v.Top) { string a = appByLocalPort != null ? appByLocalPort(c.LocalPort) : null; c.Proto = (a ?? "?") + " · " + (c.Proto != "" ? c.Proto : c.L4); }
            try { if (ts != null && !ts.HasExited) { ts.Refresh(); v.TsharkMB = ts.PrivateMemorySize64 / 1048576; if (v.TsharkMB > 300) { Error = "tshark grew past 300 MB - restarted it."; string i = iface; Restart(i.Substring(i.IndexOf('{'))); } } } catch { }
            v.State = running ? "capturing " + ((int)Elapsed.TotalMinutes) + ":" + Elapsed.Seconds.ToString("00", CultureInfo.CurrentCulture) + " of " + MaxMinutes + ":00" : "stopped";
            return v;
        }


        // Which switch port am I plugged into? Switches announce themselves every 30-60 s (LLDP / CDP) - we just listen.
        public sealed class SwitchInfo { public string Name = "", Port = "", PortDesc = "", Vlan = "", Model = "", Mgmt = "", Proto = ""; }
        public static SwitchInfo FindSwitch(string ifaceGuid, int seconds, out string err)
        {
            err = ""; Guid g;
            if (!Guid.TryParse((ifaceGuid ?? "").Trim('{', '}'), out g)) { err = "No active adapter."; return null; }
            if (!Available) { err = "Needs Wireshark (tshark) and Npcap."; return null; }
            string ifc = "\\Device\\NPF_{" + g.ToString().ToUpperInvariant() + "}";
            // promiscuous on purpose: some network cards hide these link-local multicasts otherwise
            string args = "-i \"" + ifc + "\" -n -c 1 -a duration:" + seconds.ToString(CultureInfo.InvariantCulture) + " -f \"ether proto 0x88cc or ether dst 01:00:0c:cc:cc:cc\" -V";
            string text = "";
            try { text = Job.Run(Tshark, args, (seconds + 10) * 1000) ?? ""; }
            catch (Exception e) { err = Draw.Trunc(e.Message, 80); return null; }
            var si = ParseSwitch(text);
            if (si == null) err = "No switch announced itself in " + seconds + " s. The switch may have LLDP/CDP turned off, or you're on Wi-Fi / behind a dock that doesn't pass it.";
            return si;
        }
        public static SwitchInfo ParseSwitch(string text)
        {
            var si = new SwitchInfo(); bool any = false;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                string l = raw.Trim(); Match m;
                if ((m = Regex.Match(l, @"^System Name = (.+)$")).Success) { si.Name = m.Groups[1].Value; si.Proto = "LLDP"; any = true; }
                else if ((m = Regex.Match(l, @"^Port Subtype = .*?, Id: (.+)$")).Success) { si.Port = m.Groups[1].Value; si.Proto = "LLDP"; any = true; }
                else if ((m = Regex.Match(l, @"^Port Description = (.+)$")).Success) si.PortDesc = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^System Description = (.+)$")).Success && si.Model == "") si.Model = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^Port VLAN Identifier: (\d+)")).Success) si.Vlan = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^Management Address: ([0-9a-fA-F.:]+)$")).Success && si.Mgmt == "") si.Mgmt = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^Device ID: (.+)$")).Success) { si.Name = m.Groups[1].Value; si.Proto = "CDP"; any = true; }
                else if ((m = Regex.Match(l, @"^Port ID: (.+)$")).Success) { si.Port = m.Groups[1].Value; si.Proto = "CDP"; any = true; }
                else if ((m = Regex.Match(l, @"^Native VLAN: (\d+)")).Success) si.Vlan = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^Platform: (.+)$")).Success) si.Model = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^IP [Aa]ddress: ([0-9.]+)$")).Success && si.Mgmt == "") si.Mgmt = m.Groups[1].Value;
            }
            if (si.Model.Length > 80) si.Model = si.Model.Substring(0, 79) + "…";
            return any ? si : null;
        }

        // Sniff with history: the ring buffer filtered to one flow, plus 15 s more, merged and opened in Wireshark.
        public void SniffWithHistory(Conn c, string filt, string dfilt, Action<string> status)
        {
            // the time plus a few random letters: two sniffs in one second each get their own folder
            string dir = Program.SafeCapturePath("sniff_" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            var parts = new List<string>();
            try
            {
                Directory.CreateDirectory(dir);
                status(Voice.P("Digging through the last few minutes and listening 15 s more...", "Collecting history + 15 s..."));
                var fwd = Path.Combine(dir, "z_after.pcapng");
                var fp = Process.Start(new ProcessStartInfo(Dumpcap, "-i \"" + iface + "\" -p -q -f \"" + filt + "\" -a duration:15 -a filesize:200000 -w \"" + fwd + "\"") { WorkingDirectory = Path.GetDirectoryName(Dumpcap), UseShellExecute = false, CreateNoWindow = true }); Job.Add(fp);
                int i = 0;
                foreach (var rf in Directory.GetFiles(RingDir, "*.pcapng").OrderBy(x => File.GetLastWriteTimeUtc(x)))
                {
                    string copy = Path.Combine(dir, "ring" + i + ".pcapng"), outp = Path.Combine(dir, "a_part" + i + ".pcapng"); i++;
                    try { File.Copy(rf, copy, true); } catch { continue; }
                    Job.Run(Tshark, "-r \"" + copy + "\" -Y \"" + dfilt + "\" -w \"" + outp + "\"", 60000);
                    try { File.Delete(copy); } catch { }
                    if (File.Exists(outp) && new FileInfo(outp).Length > 0) parts.Add(outp);
                }
                if (!fp.WaitForExit(20000)) { try { fp.Kill(); fp.WaitForExit(2000); } catch { } }   // never left running
                if (File.Exists(fwd)) parts.Add(fwd);
                string final = Program.SafeCapturePath("adebometer_" + new string(c.Proc.Where(char.IsLetterOrDigit).Take(20).ToArray()) + "_" + DateTime.Now.ToString("HHmmss", CultureInfo.CurrentCulture) + ".pcapng");
                if (parts.Count == 0) { status("Nothing captured for that flow."); return; }
                Job.Run(Mergecap, "-w \"" + final + "\" " + string.Join(" ", parts.Select(x => "\"" + x + "\"")), 60000);
                string ws = Path.Combine(WsDir, "Wireshark.exe");
                if (File.Exists(final)) { Program.LastCapture = final; if (File.Exists(ws)) { var psi = new ProcessStartInfo(ws, "\"" + final + "\"") { WorkingDirectory = Path.GetDirectoryName(ws), UseShellExecute = false }; Process.Start(psi).Dispose(); } status(Voice.P("Got it, with history - opening in Wireshark. (Deleted when AdeeboMeter exits - click Keep this capture to save a copy.)", "Saved with history; opening in Wireshark.")); }
                else status("Merging failed.");
            }
            catch (Exception e) { status("Sniff failed: " + e.Message); Program.Log("sniff history: " + e); }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }
}
