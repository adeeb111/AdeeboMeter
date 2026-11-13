// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Packet journey: one conversation's real packets
    // "Capture this conversation" records ONE flow (the connection you picked) with dumpcap: the capture filter runs in the
    // Npcap driver, so nothing else ever reaches us; snaplen 128 keeps headers only - no payload is kept. It stops by itself
    // at a packet cap or a time cap, whichever comes first. Our own reader turns the headers into records; the rules below
    // translate them into plain words and, when you press Explain, into an observation. No AI, nothing leaves the PC.
    static class Journey
    {
        public sealed class Rec
        {
            public double T; public bool Out; public int Len, IpLen, Proto, Flags, Win, PayLen, Ttl, Dscp; public uint Seq, Ack;
            public int IcmpType = -1, IcmpCode = -1, IcmpMtu; public bool FromRouter; public int LPort;   // this PC's port: tells one connection from another
            public bool Resend, DupAck, Before, Keepalive;   // Before: from the rewind buffer, before you clicked
            public double GapBefore;              // a stall right before this packet (seconds), 0 if none
            public string Say = "";
        }
        public sealed class Note { public int Lv; public string Title = "", Text = ""; public double At = double.NaN; }
        public sealed class Flow { public string Remote = "", Local = ""; public int RemotePort, LocalPort; public bool Udp; public string App = ""; }

        public const int QuickCap = 100, NormalCap = 1000, NormalSecs = 30, QuickSecs = 15;

        // ---------- the capture filter: the conversation, plus ICMP about that host ("too big", "unreachable")
        public static string Bpf(Flow f)
        {
            if (f.Remote == "" && f.Udp && f.LocalPort > 0) return "(udp port " + f.LocalPort + ") or icmp or icmp6";   // the far end isn't known yet
            IPAddress ip; if (!IPAddress.TryParse(f.Remote, out ip)) return null;
            ip = new IPAddress(ip.GetAddressBytes());   // no zone: capture filters can't take one
            bool v6 = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            string conv = "host " + ip + (f.RemotePort > 0 ? " and port " + f.RemotePort : "") + (f.LocalPort > 0 ? " and port " + f.LocalPort : "");
            return "(" + conv + ") or (" + (v6 ? "icmp6" : "icmp") + " and host " + ip + ")";
        }
        // one record per packet of the flow (ICMP about it included); everything else is dropped as soon as it's decoded
        public static bool Belongs(Pcap.Pkt k, Flow f)
        {
            if (k == null || k.Src == null || k.Dst == null) return false;
            string s = Plain(k.Src), d = Plain(k.Dst);
            if (f.Remote == "") return k.Proto == 1 || k.Proto == 58 || (k.Proto == 17 && (k.SrcPort == f.LocalPort || k.DstPort == f.LocalPort));
            if (k.Proto == 1 || k.Proto == 58) return s == f.Remote || d == f.Remote || IcmpAbout(k, f);
            if (s != f.Remote && d != f.Remote) return false;
            if (f.RemotePort > 0 && k.SrcPort != f.RemotePort && k.DstPort != f.RemotePort) return false;
            if (f.LocalPort > 0 && k.SrcPort != f.LocalPort && k.DstPort != f.LocalPort) return false;
            return true;
        }
        static string Plain(IPAddress a) { string s = a.ToString(); int z = s.IndexOf('%'); return z > 0 ? s.Substring(0, z) : s; }
        // a UDP socket talks to whoever: the far end is the address it exchanges the most packets with
        public static void FindRemote(Flow f, List<Pcap.Pkt> pk, HashSet<string> mine)
        {
            if (f.Remote != "") return;
            var peer = pk.Where(k => k.Proto == 17).Select(k => k.SrcPort == f.LocalPort && mine.Contains(Plain(k.Src)) ? Plain(k.Dst) + "|" + k.DstPort : k.DstPort == f.LocalPort ? Plain(k.Src) + "|" + k.SrcPort : null)
                         .Where(x => x != null).GroupBy(x => x).OrderByDescending(gr => gr.Count()).FirstOrDefault();
            if (peer == null || peer.Count() < 3) return;
            var parts = peer.Key.Split('|'); f.Remote = parts[0]; f.RemotePort = int.Parse(parts[1], CultureInfo.InvariantCulture);
        }
        // an ICMP error from a router on the way quotes the packet it's about - is that packet ours?
        static bool IcmpAbout(Pcap.Pkt k, Flow f)
        {
            var p = k.Payload; if (p == null || p.Length < 8 + 20) return false;
            int t = p[0]; bool err = k.Proto == 1 ? (t == 3 || t == 11 || t == 12) : (t >= 1 && t <= 4); if (!err) return false;
            try
            {
                if (k.Proto == 1 && (p[8] >> 4) == 4) return new IPAddress(new[] { p[24], p[25], p[26], p[27] }).ToString() == f.Remote;
                if (k.Proto == 58 && p.Length >= 8 + 40 && (p[8] >> 4) == 6) { var a = new byte[16]; Array.Copy(p, 8 + 24, a, 0, 16); return new IPAddress(a).ToString() == f.Remote; }
            }
            catch (ArgumentException) { }
            return false;
        }

        // ---------- headers -> records (+ resends, duplicate ACKs and stalls marked)
        public static List<Rec> Records(List<Pcap.Pkt> pk, Flow f, double t0)
        {
            var list = new List<Rec>();
            foreach (var k in pk.OrderBy(x => x.Abs))
            {
                if ((k.Proto == 1 || k.Proto == 58) && f.Remote != "" && !Belongs(k, f)) continue;   // ICMP kept before the far end was known, about someone else
                string s = Plain(k.Src);
                var r = new Rec { T = k.Abs - t0, Out = s != f.Remote, Len = k.Len, IpLen = k.IpLen, Proto = k.Proto, Flags = k.Flags, Win = k.Win, PayLen = k.PayLen, Ttl = k.Ttl, Dscp = k.Dscp, Seq = k.Seq, Ack = k.Ack };
                r.LPort = r.Out ? k.SrcPort : k.DstPort;
                if ((k.Proto == 1 || k.Proto == 58) && k.Payload.Length >= 4)
                {
                    r.IcmpType = k.Payload[0]; r.IcmpCode = k.Payload[1];
                    if (Plain(k.Dst) == f.Remote) { r.Out = true; r.FromRouter = false; }   // this PC's own ping, or its "port unreachable" back to the server
                    else { r.FromRouter = s != f.Remote; r.Out = false; }
                    if (k.Proto == 1 && r.IcmpType == 3 && r.IcmpCode == 4 && k.Payload.Length >= 8) r.IcmpMtu = k.Payload[6] << 8 | k.Payload[7];
                    if (k.Proto == 58 && r.IcmpType == 2 && k.Payload.Length >= 8) r.IcmpMtu = (int)Math.Min(65535u, (uint)(k.Payload[4] << 24 | k.Payload[5] << 16 | k.Payload[6] << 8 | k.Payload[7]));
                }
                list.Add(r);
            }
            Mark(list); return list;
        }
        static void Mark(List<Rec> list)
        {
            // TCP: data whose bytes were already sent is a resend; the same ACK three times with no data asks for a resend
            var seen = new Dictionary<bool, uint>(); var lastAck = new Dictionary<bool, uint>(); var ackRun = new Dictionary<bool, int>(); bool[] dirs = { true, false };
            foreach (var d in dirs) { ackRun[d] = 0; }
            foreach (var r in list.Where(x => x.Proto == 6))
            {
                uint hi0; bool have = seen.TryGetValue(r.Out, out hi0);
                // a keepalive: 0 or 1 byte at one before the next byte (RFC 9293) - "still here", not a lost packet
                if (r.PayLen <= 1 && have && r.Seq == hi0 - 1 && (r.Flags & 0x07) == 0) { r.Keepalive = true; continue; }
                if (r.PayLen > 0)
                {
                    uint end = r.Seq + (uint)r.PayLen;
                    if (have && (int)(r.Seq - hi0) < 0) r.Resend = true;   // starts below what was already sent: resent, even when only partly
                    if (!have || (int)(end - hi0) > 0) seen[r.Out] = end;
                }
                else if ((r.Flags & 0x02) != 0) seen[r.Out] = r.Seq + 1;   // SYN takes one sequence number
                if (r.PayLen == 0 && (r.Flags & 0x10) != 0 && (r.Flags & 0x07) == 0)
                {
                    // a duplicate ACK only means something while the other side has data out that isn't acknowledged yet
                    uint hiO; bool waiting = seen.TryGetValue(!r.Out, out hiO) && (int)(hiO - r.Ack) > 0;
                    uint la; if (waiting && lastAck.TryGetValue(r.Out, out la) && la == r.Ack) { ackRun[r.Out]++; if (ackRun[r.Out] >= 2) r.DupAck = true; } else ackRun[r.Out] = 0;
                    lastAck[r.Out] = r.Ack;
                }
                else if (r.PayLen > 0) ackRun[r.Out] = 0;
            }
            // stalls: on a steady stream, a gap far longer than usual (4x the median, at least 50 ms)
            foreach (var d in dirs)
            {
                var one = list.Where(x => x.Out == d && x.IcmpType < 0).ToList(); if (one.Count < 20) continue;
                var gaps = new List<double>(); for (int i = 1; i < one.Count; i++) gaps.Add(one[i].T - one[i - 1].T);
                double med = Median(gaps); if (med <= 0 || med > 0.25) continue;   // not a steady stream: gaps mean nothing
                double lim = Math.Max(0.05, med * 4);
                for (int i = 1; i < one.Count; i++) if (one[i].T - one[i - 1].T > lim) one[i].GapBefore = one[i].T - one[i - 1].T;
            }
        }
        public static double Median(List<double> v) { if (v.Count == 0) return double.NaN; var s = v.OrderBy(x => x).ToList(); return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2; }
        static double Pct(List<double> v, double p) { if (v.Count == 0) return double.NaN; var s = v.OrderBy(x => x).ToList(); return s[Math.Min(s.Count - 1, (int)Math.Floor(p * (s.Count - 1) + 0.5))]; }

        // ---------- what kind of conversation this is: decides which rules apply
        public static string Kind(Flow f, List<Rec> r)
        {
            int rp = f.RemotePort; IPAddress ip; if (!IPAddress.TryParse(f.Remote, out ip)) ip = null;
            if ((rp == 53 || rp == 5353) && f.Udp) return "dns";   // one question, one answer: plain DNS over UDP only - DNS over TCP and DoT (853) follow the TCP rules
            if (rp == 51820 || rp == 500 || rp == 4500 || rp == 1194 || rp == 1701 || (rp == 443 && f.App.IndexOf("vpn", StringComparison.OrdinalIgnoreCase) >= 0)) return "vpn";
            if (ip != null && !Watch.IsPublic(f.Remote)) return "local";
            var data = r.Where(x => x.IcmpType < 0).ToList(); double dur = data.Count > 1 ? data[data.Count - 1].T - data[0].T : 0;
            if (f.Udp && dur > 0.5)
            {
                int inN = data.Count(x => !x.Out), outN = data.Count(x => x.Out); double avg = data.Count > 0 ? data.Average(x => (double)x.IpLen) : 0;
                if (inN / dur >= 10 && outN / dur >= 10 && avg < 700) return "realtime";
            }
            if (rp == 3478 || rp == 3479 || rp == 3480 || rp == 3481 || (f.Udp && rp >= 50000)) return "realtime";
            if (!f.Udp && data.Where(x => x.PayLen > 0).Sum(x => (long)x.PayLen) > 1000000) return "bulk";
            if (rp == 443 || rp == 80 || rp == 8443 || rp == 8080) return "web";
            if (f.Udp) return "realtime";
            return "unknown";
        }
        public static string KindName(string k)
        {
            switch (k) { case "realtime": return "real-time (call, game or stream)"; case "bulk": return "download / upload"; case "web": return "web page or app"; case "dns": return "name lookup (DNS)"; case "vpn": return "VPN / tunnel"; case "local": return "a device on your network"; default: return "unknown kind - general checks only"; }
        }

        // ---------- live translation: one plain line per packet
        public static void Translate(List<Rec> list, string kind)
        {
            bool syn = false;
            foreach (var r in list)
            {
                string who = r.Out ? "This PC" : "Server";
                if (r.IcmpType >= 0) { r.Say = (r.FromRouter ? "A router on the way: " : "Server: ") + IcmpWords(r); continue; }
                if (r.Proto == 6)
                {
                    int fl = r.Flags;
                    if ((fl & 0x02) != 0 && (fl & 0x10) == 0) { r.Say = who + (syn && r.Out ? " asks again to connect (SYN resent - no answer yet)" : " asks to connect (SYN)"); syn = true; }
                    else if ((fl & 0x02) != 0) r.Say = who + " says yes, let's talk (SYN-ACK)";
                    else if ((fl & 0x04) != 0) r.Say = who + " hangs up abruptly (RST)";
                    else if ((fl & 0x01) != 0) r.Say = who + " says goodbye (FIN)";
                    else if (r.Keepalive) r.Say = who + " checks the line is still there (keepalive)";
                    else if (r.Resend) r.Say = who + " sends " + Bytes(r.PayLen) + " AGAIN - the first copy got lost or late (resend)";
                    else if (r.DupAck) r.Say = who + " asks again for the same data - something went missing (duplicate ACK)";
                    else if (r.PayLen > 0) r.Say = who + " sends " + Bytes(r.PayLen) + (kind == "web" && r.Out ? " (request or upload)" : "");
                    else if (r.Win == 0) r.Say = who + " says stop, my buffer is full (zero window)";
                    else r.Say = who + " says got it (ACK)";
                }
                else if (r.Proto == 17)
                {
                    string what = kind == "dns" ? (r.Out ? "asks for a name (DNS query)" : "answers the name (DNS reply)") : kind == "realtime" ? (r.Out ? "sends an update" : "sends an update") : kind == "vpn" ? "sends a wrapped (encrypted) packet" : "sends";
                    r.Say = who + " " + what + ", " + Bytes(r.PayLen);
                }
                else r.Say = who + " · protocol " + r.Proto + ", " + Bytes(r.IpLen);
                if (r.GapBefore > 0) r.Say = "after " + Ms(r.GapBefore) + " of silence: " + r.Say;
                if (r.Dscp > 0) r.Say += r.Dscp == 46 ? " · marked for priority (EF)" : r.Dscp == 34 ? " · marked video (AF41)" : "";
            }
        }
        static string IcmpWords(Rec r)
        {
            if (r.Proto == 1)
            {
                if (r.IcmpType == 3 && r.IcmpCode == 4) return "too big - this link carries at most " + (r.IcmpMtu > 0 ? r.IcmpMtu + " bytes" : "less") + " (fragmentation needed)";
                if (r.IcmpType == 3) return "can't deliver it (destination unreachable, code " + r.IcmpCode + (r.IcmpCode == 3 ? " - nothing listens on that port" : r.IcmpCode == 13 ? " - blocked by a filter" : r.IcmpCode == 1 ? " - no route to the host" : "") + ")";
                if (r.IcmpType == 11) return "the packet ran out of hops (time exceeded) - a routing loop or a trace";
                if (r.IcmpType == 0) return "ping reply"; if (r.IcmpType == 8) return "ping";
            }
            else
            {
                if (r.IcmpType == 2) return "too big - this link carries at most " + (r.IcmpMtu > 0 ? r.IcmpMtu + " bytes" : "less") + " (IPv6 packet too big)";
                if (r.IcmpType == 1) return "can't deliver it (destination unreachable, code " + r.IcmpCode + (r.IcmpCode == 4 ? " - nothing listens on that port" : r.IcmpCode == 1 ? " - blocked by policy" : "") + ")";
                if (r.IcmpType == 3) return "the packet ran out of hops (time exceeded)";
                if (r.IcmpType == 129) return "ping reply"; if (r.IcmpType == 128) return "ping";
            }
            return "ICMP type " + r.IcmpType + " code " + r.IcmpCode;
        }
        static string Bytes(int n) { return n.ToString("N0", CultureInfo.CurrentCulture) + (n == 1 ? " byte" : " bytes"); }
        public static string Ms(double s) { return (s * 1000).ToString(s < 0.01 ? "0.0" : "0", CultureInfo.CurrentCulture) + " ms"; }
        static string At(double t) { return t.ToString("0.0", CultureInfo.CurrentCulture) + " s"; }

        // ---------- Explain this capture: fixed rules, every claim with its evidence, and what we can't tell
        public static List<Note> Explain(Flow f, List<Rec> list, string kind, bool wifi)
        {
            var notes = new List<Note>(); var data = list.Where(x => x.IcmpType < 0).ToList();
            if (data.Count < 4) { notes.Add(N(3, "Too little to go on", data.Count + " packets of this conversation were seen - capture while it's busy (during the call, the match or the download).", double.NaN)); return notes; }
            double dur = data[data.Count - 1].T - data[0].T; var inb = data.Where(x => !x.Out).ToList(); var outb = data.Where(x => x.Out).ToList();
            var findings = new List<Note>();
            // shared rules: ICMP errors, route changes, this PC sending late
            foreach (var r in list.Where(x => x.IcmpType >= 0).GroupBy(x => x.IcmpType * 1000 + x.IcmpCode).Select(gr => gr.First()))
            {
                bool big = (r.Proto == 1 && r.IcmpType == 3 && r.IcmpCode == 4) || (r.Proto == 58 && r.IcmpType == 2);
                bool unreach = (r.Proto == 1 && r.IcmpType == 3) || (r.Proto == 58 && r.IcmpType == 1);
                if (big) findings.Add(N(2, "A packet was too large for a link on the way", "A router said the most it can carry is " + (r.IcmpMtu > 0 ? r.IcmpMtu + " bytes" : "less than we sent") + ". That's an MTU problem - usually a VPN or PPPoE link. Big packets vanish while small ones work, so pages hang half-loaded or calls freeze on screen shares.", r.T));
                else if (unreach) findings.Add(N(2, "Something refused delivery", (r.FromRouter ? "A router on the way" : "The server") + " said it " + IcmpWords(r) + ".", r.T));
            }
            var ttls = inb.Select(x => x.Ttl).Where(t => t > 0).ToList();
            if (ttls.Count >= 20)
            {
                int first = ttls[0]; int idx = inb.FindIndex(x => x.Ttl > 0 && Math.Abs(x.Ttl - first) >= 2);
                if (idx > 0 && inb.Skip(idx).Count(x => Math.Abs(x.Ttl - first) >= 2) >= 5) findings.Add(N(1, "The route changed during the capture", "Replies started arriving with a different hop count (TTL " + first + " → " + inb[idx].Ttl + ") - the internet moved this conversation to another road mid-way. A short spike or a few lost packets around " + At(inb[idx].T) + " are normal when that happens.", inb[idx].T));
            }
            // per kind
            if (kind == "realtime" || (kind == "unknown" && f.Udp)) RealTime(findings, inb, outb, dur, wifi);
            if (!f.Udp) TcpRules(findings, data, inb, outb, dur, kind);
            if (kind == "dns") Dns(findings, data);
            if (kind == "vpn") Vpn(findings, data);
            if (kind == "local") Local(findings, data);
            // the verdict comes first: the worst finding, or "looks healthy"
            var worst = findings.OrderByDescending(n => n.Lv == 2 ? 3 : n.Lv == 1 ? 2 : n.Lv == 0 ? 0 : 1).FirstOrDefault();
            int icmpN = list.Count - data.Count; string summary = list.Count + " packets over " + At(dur) + " · " + inb.Count + " in, " + outb.Count + " out" + (icmpN > 0 ? ", " + icmpN + " ICMP" : "") + " · " + KindName(kind);
            if (worst == null || (worst.Lv != 1 && worst.Lv != 2)) notes.Add(N(0, "Looks healthy", summary + ". Nothing in these packets points at a problem.", double.NaN));
            else notes.Add(N(worst.Lv, "In short: " + worst.Title.Substring(0, 1).ToLowerInvariant() + worst.Title.Substring(1), summary + ". The details are below.", worst.At));
            notes.AddRange(findings.OrderByDescending(n => n.Lv == 2 ? 3 : n.Lv == 1 ? 2 : n.Lv == 0 ? 1 : 0).ThenBy(n => double.IsNaN(n.At) ? 1e9 : n.At));
            notes.Add(N(3, "Can't tell from here", CantTell(kind, f.Udp)));
            return notes;
        }
        static Note N(int lv, string t, string x) { return N(lv, t, x, double.NaN); }
        static Note N(int lv, string t, string x, double at) { return new Note { Lv = lv, Title = t, Text = x, At = at }; }
        static string CantTell(string kind, bool udp)
        {
            string s = "What's inside the packets - they're encrypted and only headers were kept. ";
            if (udp && kind != "dns") s += "Exact loss: UDP carries no sequence numbers we can read, so a silence may be a lost packet or just nothing to say. ";
            s += "Which router on the way caused a delay - run MTR (Path) to the same server for that.";
            if (kind == "realtime") s += " The game's own ping and frame rate - we see the network, not the game.";
            return s;
        }
        static void RealTime(List<Note> n, List<Rec> inb, List<Rec> outb, double dur, bool wifi)
        {
            if (inb.Count < 10 || dur <= 0) return;
            var gaps = new List<double>(); for (int i = 1; i < inb.Count; i++) gaps.Add(inb[i].T - inb[i - 1].T);
            double med = Median(gaps), p95 = Pct(gaps, 0.95); double jit = 0; for (int i = 1; i < gaps.Count; i++) jit += Math.Abs(gaps[i] - gaps[i - 1]); jit = gaps.Count > 1 ? jit / (gaps.Count - 1) : 0;
            n.Add(N(3, "The rhythm", "From the server: one packet every " + Ms(med) + " on average (" + (1 / Math.Max(1e-4, med)).ToString("0", CultureInfo.CurrentCulture) + " a second); 95% arrive within " + Ms(p95) + " of the last. Jitter (how much that wobbles): " + Ms(jit) + ".", double.NaN));
            var stalls = inb.Where(x => x.GapBefore > 0).ToList(); int bunched = 0, silent = 0; var firstBad = stalls.FirstOrDefault();
            foreach (var s in stalls)
            {
                int i = inb.IndexOf(s); int quick = 0; for (int k = i + 1; k < inb.Count && k <= i + 6; k++) if (inb[k].T - inb[k - 1].T < med / 3) quick++;
                if (quick >= 3) bunched++; else silent++;
            }
            if (stalls.Count > 0)
            {
                string when = string.Join(", ", stalls.Take(4).Select(x => At(x.T))) + (stalls.Count > 4 ? "..." : "");
                if (bunched >= silent) n.Add(N(stalls.Count >= 5 ? 2 : 1, "Delayed and bunched up (jitter), not lost", stalls.Count + " times the stream stalled (longest " + Ms(stalls.Max(x => x.GapBefore)) + ", at " + when + ") and the late packets then arrived in a burst. That's a queue filling up somewhere - usually Wi-Fi or a busy upload at home - not packets being thrown away.", firstBad.T));
                else n.Add(N(stalls.Count >= 5 ? 2 : 1, "Silences with nothing after them", stalls.Count + " times the stream went quiet (longest " + Ms(stalls.Max(x => x.GapBefore)) + ", at " + when + ") and no burst followed - packets were probably lost or dropped on the way.", firstBad.T));
            }
            else if (jit < 0.01) n.Add(N(0, "Steady stream", "Packets arrived evenly - no stalls, low jitter.", double.NaN));
            else if (jit >= 0.03) n.Add(N(1, "Uneven arrival (jitter)", "No long stalls, but packets wobble by " + Ms(jit) + " on average - enough to feel in a call or a fast game.", double.NaN));
            // this PC's own sending
            if (outb.Count >= 20)
            {
                var og = new List<double>(); for (int i = 1; i < outb.Count; i++) og.Add(outb[i].T - outb[i - 1].T);
                double om = Median(og), late = og.Count(g => g > Math.Max(0.05, om * 4));
                if (om > 0 && om < 0.25 && late >= 3) n.Add(N(1, "This PC was late sending", late + " times your own updates left late (the PC paused, not the network)" + (wifi ? " - Wi-Fi power saving or a background scan can do this" : " - a busy CPU or a driver can do this") + ".", outb[1 + og.FindIndex(g => g > Math.Max(0.05, om * 4))].T));
                else if (om > 0 && om < 0.25) n.Add(N(0, "Your PC sent on time", "Your own updates left evenly - this PC isn't the cause.", double.NaN));
            }
            int marked = outb.Count(x => x.Dscp == 46 || x.Dscp == 34);
            if (marked > 0) n.Add(N(3, "Priority marking", marked + " of your packets carry a priority mark (" + (outb.Any(x => x.Dscp == 46) ? "EF" : "AF41") + ") - routers that honour it put them first. Many home and internet routers ignore it.", double.NaN));
        }
        static void TcpRules(List<Note> n, List<Rec> data, List<Rec> inb, List<Rec> outb, double dur, string kind)
        {
            var tcp = data.Where(x => x.Proto == 6).ToList(); if (tcp.Count == 0) return;
            var syn = tcp.FirstOrDefault(x => x.Out && (x.Flags & 0x12) == 0x02); var synack = tcp.FirstOrDefault(x => !x.Out && (x.Flags & 0x12) == 0x12);
            int synTries = tcp.Count(x => x.Out && (x.Flags & 0x12) == 0x02);
            if (syn != null && synack == null) n.Add(N(2, "The server never answered", "This PC asked to connect " + synTries + " time(s) and got no reply - a firewall dropping it, the server down, or the wrong port.", syn.T));
            else if (synTries > 1) n.Add(N(1, "The first try to connect was lost", "The connection request went " + synTries + " times before the server answered - each retry waits 1-3 s, which is the delay you saw.", syn.T));
            double rtt = syn != null && synack != null ? synack.T - syn.T : double.NaN;
            if (!double.IsNaN(rtt)) n.Add(N(rtt > 0.15 ? 1 : 3, "Round trip to the server", Ms(rtt) + " (from the connection handshake)" + (rtt > 0.15 ? " - far away or a slow path; every request waits at least this long." : "."), synack.T));
            int resIn = inb.Count(x => x.Resend), resOut = outb.Count(x => x.Resend), dupIn = inb.Count(x => x.DupAck), dupOut = outb.Count(x => x.DupAck);
            int dataIn = inb.Count(x => x.PayLen > 0 && !x.Keepalive), dataOut = outb.Count(x => x.PayLen > 0 && !x.Keepalive);
            if (resIn + resOut > 0)
            {
                double pctIn = dataIn > 0 ? 100.0 * resIn / dataIn : 0, pctOut = dataOut > 0 ? 100.0 * resOut / dataOut : 0; double pc = Math.Max(pctIn, pctOut);
                var first = data.First(x => x.Resend);
                n.Add(N(pc >= 2 ? 2 : 1, "Packets were lost and sent again", (resIn > 0 ? "The server resent " + resIn + " of " + dataIn + " packets (" + pctIn.ToString("0.#", CultureInfo.CurrentCulture) + "%)" : "") + (resIn > 0 && resOut > 0 ? "; " : "") + (resOut > 0 ? "this PC resent " + resOut + " of " + dataOut + " (" + pctOut.ToString("0.#", CultureInfo.CurrentCulture) + "%)" : "") + ". Loss is confirmed here - TCP only resends what didn't arrive." + (pc >= 2 ? " Above 1-2% a download slows down a lot." : ""), first.T));
            }
            else if (dataIn + dataOut >= 20) n.Add(N(0, "No resends", "Nothing had to be sent twice - no loss on this conversation.", double.NaN));
            if (dupIn + dupOut >= 3 && resIn + resOut == 0) n.Add(N(1, "Asked for missing data", (dupIn + dupOut) + " duplicate ACKs - one side kept asking for data that went missing or came out of order.", data.First(x => x.DupAck).T));
            var zw = data.FirstOrDefault(x => x.Win == 0 && (x.Flags & 0x04) == 0 && x.Proto == 6);
            if (zw != null) n.Add(N(1, (zw.Out ? "This PC" : "The server") + " was too busy to take more", "It said its buffer was full (zero window) - the slowdown is at " + (zw.Out ? "this PC (disk or app can't keep up)" : "the server") + ", not the network.", zw.T));
            var rst = data.FirstOrDefault(x => (x.Flags & 0x04) != 0);
            if (rst != null) n.Add(N(1, (rst.Out ? "This PC" : "The server") + " cut the connection (RST)", "An abrupt hang-up rather than a goodbye - a timeout, a firewall or an app closing it.", rst.T));
            if (kind == "web" && synack != null)
            {
                var req = outb.FirstOrDefault(x => x.PayLen > 0 && x.T > synack.T); if (req != null)
                {
                    var resp = inb.Skip(0).FirstOrDefault(x => x.PayLen > 0 && x.T > req.T);
                    var req2 = outb.Where(x => x.PayLen > 0 && x.T > req.T && (resp == null || x.T < resp.T)).LastOrDefault(); if (req2 != null) req = req2;
                    resp = inb.FirstOrDefault(x => x.PayLen > 0 && x.T > req.T);
                    if (resp != null) { double wait = resp.T - req.T; n.Add(N(wait > 1 ? 1 : 3, "Waiting for the server", "After the request the server took " + Ms(wait) + " to start answering" + (!double.IsNaN(rtt) && wait > rtt * 3 + 0.2 ? " - most of that is the server thinking, not the network (" + Ms(rtt) + " round trip)." : "."), resp.T)); }
                }
            }
            if (kind == "bulk" && dur > 1)
            {
                long inBytes = inb.Sum(x => (long)x.PayLen), outBytes = outb.Sum(x => (long)x.PayLen); bool down = inBytes >= outBytes; var dir = down ? inb : outb;
                double mbps = (down ? inBytes : outBytes) * 8 / dur / 1e6;
                var acks = down ? outb : inb; int maxWin = acks.Count > 0 ? acks.Max(x => x.Win) : 0;
                n.Add(N(3, down ? "Download speed" : "Upload speed", mbps.ToString("0.#", CultureInfo.CurrentCulture) + " Mbps on average over " + At(dur) + ".", double.NaN));
                if (!double.IsNaN(rtt) && rtt > 0 && maxWin > 0 && maxWin < 65535)
                {
                    double cap = maxWin * 8 / rtt / 1e6;   // without window scaling we can't see the real window; only flag the obvious
                    // faster than the raw window allows = the window is scaled (the SYN's scale option isn't read here), so the raw
                    // number says nothing: only a speed sitting right at that cap points at the window
                    if (mbps > cap * 0.7 && mbps <= cap * 1.1) n.Add(N(1, "Capped by the receive window", "The receiver offers about " + Bytes(maxWin) + " at a time over a " + Ms(rtt) + " round trip - that alone limits it to about " + cap.ToString("0.#", CultureInfo.CurrentCulture) + " Mbps whatever your plan is.", double.NaN));
                }
            }
        }
        static void Dns(List<Note> n, List<Rec> data)
        {
            var q = data.Where(x => x.Out).ToList(); var a = data.Where(x => !x.Out).ToList();
            if (q.Count > 0 && a.Count == 0) { n.Add(N(2, "The DNS server didn't answer", q.Count + " question(s), no answer - this server is down or blocked; Windows tries the next one after about a second.", q[0].T)); return; }
            if (q.Count > a.Count && a.Count > 0) n.Add(N(1, "Some questions were asked twice", (q.Count - a.Count) + " question(s) went unanswered and were repeated - each costs about a second.", q[0].T));
            var times = new List<double>(); foreach (var r in a) { var before = q.LastOrDefault(x => x.T <= r.T); if (before != null) times.Add(r.T - before.T); }
            if (times.Count > 0) { double m = Median(times); n.Add(N(m > 0.1 ? 1 : 0, "Answer time", Ms(m) + " typical" + (m > 0.1 ? " - slow; every new site waits this long first" : "") + ". (Each answer is paired with the question just before it - a resent question can make this look a little faster.)", double.NaN)); }
        }
        static void Vpn(List<Note> n, List<Rec> data)
        {
            int big = data.Count(x => x.Out && x.IpLen >= 1400), maxLen = big > 0 ? data.Where(x => x.Out).Max(x => x.IpLen) : 0;   /* the biggest of the same outbound packets it counts */
            if (big > 0) n.Add(N(1, "Tunnel packets near full size", big + " packets were up to " + maxLen + " bytes on the outside. With the tunnel's own headers that's close to a 1,500-byte link's limit - if some sites hang half-loaded, lower the VPN adapter's MTU (Packet stack in the Toolbox works out the number).", data.First(x => x.Out && x.IpLen >= 1400).T));
            var outb = data.Where(x => x.Out).ToList(); var gaps = new List<double>(); for (int i = 1; i < outb.Count; i++) gaps.Add(outb[i].T - outb[i - 1].T);
            if (outb.Count >= 3 && outb.All(x => x.IpLen < 200)) n.Add(N(3, "Only keepalives", "Just small packets - the tunnel is up but idle (keepalives every " + Ms(Median(gaps)) + ").", double.NaN));
        }
        static void Local(List<Note> n, List<Rec> data)
        {
            var times = new List<double>();
            // a request (data from this PC) and the device's next packet with data in it - a bare ACK is not an answer
            Func<Rec, bool> hasData = x => x.PayLen > 0 || x.Proto != 6;
            int j = 0;
            for (int i = 0; i < data.Count; i++)
            {
                if (!data[i].Out || !hasData(data[i])) continue;
                if (j <= i) j = i + 1; while (j < data.Count && (data[j].Out || !hasData(data[j]))) j++;
                if (j < data.Count) times.Add(data[j].T - data[i].T);
            }
            if (times.Count == 0)
            {
                if (data.Any(x => !x.Out && hasData(x))) n.Add(N(3, "How fast the device answers", "can't tell from this one - only the device sent data (a download), this PC just acknowledged it. Its speed shows in the transfer itself.", double.NaN));
                return;
            } double m = Median(times), mx = times.Max();
            n.Add(N(m > 0.2 ? 1 : 0, "How fast the device answers", Ms(m) + " typical, " + Ms(mx) + " at worst" + (m > 0.2 ? " - slow for a device on your own network; the device itself is busy or asleep (printers and NAS drives wake up slowly)" : " - the local network is fine") + ".", double.NaN));
        }

        // ---------- a made-up game conversation (screenshots and the example): steady 8 ms updates, three stalls with bursts
        public static List<Rec> Sample()
        {
            var l = new List<Rec>(); double tin = 0.004;
            for (int i = 0; i < 300; i++) l.Add(new Rec { T = i * 0.008, Out = true, IpLen = 118, PayLen = 90, Proto = 17, Ttl = 64 });
            for (int i = 0; i < 300; i++)
            {
                if (i == 100 || i == 180 || i == 250) { tin += 0.120; for (int k = 0; k < 5; k++) { l.Add(new Rec { T = tin, IpLen = 318, PayLen = 290, Proto = 17, Ttl = 52 }); tin += 0.0005; } }
                l.Add(new Rec { T = tin, IpLen = i % 20 == 0 ? 1200 : 318, PayLen = 290, Proto = 17, Ttl = 52 }); tin += 0.008;
            }
            l = l.OrderBy(r => r.T).ToList(); Mark(l); return l;
        }

        // ---------- the capture itself
        public sealed class Session
        {
            public readonly Flow F; public readonly int Cap, Secs; public volatile bool Done; public string Error = "", File = "";
            public double T0 = double.NaN; public List<Rec> Recs = new List<Rec>(); public int Before;
            readonly object k = new object(); Process p; volatile bool stop;
            public HashSet<string> Mine = new HashSet<string>();   // this PC's own addresses
            public Session(Flow f, List<Rec> made) : this(f, false) { Recs = made; Done = true; }   // a finished one, for the example
            public Session(Flow f, bool quick) { F = f; Cap = quick ? QuickCap : NormalCap; Secs = quick ? QuickSecs : NormalSecs; }
            public List<Rec> Snapshot() { lock (k) return Recs.ToList(); }
            public void Stop() { stop = true; }
            // runs on a worker thread; the UI reads Snapshot() while it records
            public void Run(string iface, Action changed)
            {
                try
                {
                    if (Policy.NoCapture) { Error = "Capturing packets is turned off by your IT team (AdeeboMeter.policy)."; return; }
                    string bpf = Bpf(F); if (bpf == null) { Error = "No address to follow."; return; }
                    var before = Rewind(); T0 = before.Count > 0 ? before[0].Abs : double.NaN;
                    File = Program.SafeCapturePath("journey_" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 4) + ".pcapng");
                    if (File == null) { Error = "Capture folder unavailable."; return; }
                    // -s 128: headers only (the payload is never written); -c / -a duration: stops by itself; -p: this PC's own traffic only
                    var psi = new ProcessStartInfo(LiveCapture.Dumpcap, "-i \"" + iface + "\" -p -q -s 128 -f \"" + bpf + "\" -c " + Cap.ToString(CultureInfo.InvariantCulture) + " -a duration:" + Secs.ToString(CultureInfo.InvariantCulture) + " -a filesize:20000 -w \"" + File + "\"") { WorkingDirectory = Path.GetDirectoryName(LiveCapture.Dumpcap), UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                    p = Process.Start(psi); Job.Add(p); var err = p.StandardError.ReadToEndAsync();
                    long lastLen = -1; var sw = Stopwatch.StartNew();
                    while (true)
                    {
                        bool exited = p.WaitForExit(500);
                        if (stop && !exited) { try { p.Kill(); p.WaitForExit(2000); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } exited = true; }
                        long len = -1; try { len = new FileInfo(File).Length; } catch (IOException) { }
                        if (len > 0 && len != lastLen) { lastLen = len; Read(before); if (changed != null) changed(); }
                        if (exited || sw.Elapsed.TotalSeconds > Secs + 10) break;
                    }
                    if (!p.HasExited) { try { p.Kill(); } catch (InvalidOperationException) { } }
                    Read(before);
                    if (Recs.Count == 0) { string e2 = ""; try { e2 = err.Wait(1000) ? err.Result : ""; } catch (AggregateException) { } Error = Before > 0 ? "" : e2.IndexOf("rror", StringComparison.Ordinal) >= 0 ? Draw.Trunc(e2.Trim(), 140) : "No packets of this conversation went by - is it still active? Start the call, the match or the download, then capture."; }
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Error = Draw.Trunc(e.Message, 120); Program.Log("journey capture: " + e); }
                finally { Done = true; if (changed != null) changed(); }
            }
            void Read(List<Pcap.Pkt> before)
            {
                byte[] b; try { using (var fs = new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { b = new byte[Math.Min(fs.Length, 32L * 1024 * 1024)]; int got = 0, n; while (got < b.Length && (n = fs.Read(b, got, b.Length - got)) > 0) got += n; if (got < b.Length) Array.Resize(ref b, got); } }
                catch (IOException) { return; } catch (UnauthorizedAccessException) { return; }
                var now = Strip(Pcap.Parse(b, x => Belongs(x, F)));
                if (F.Remote == "") { FindRemote(F, now, Mine); if (F.Remote == "") return; now = now.Where(x => Belongs(x, F)).ToList(); }
                var all = before.Concat(now).ToList(); if (all.Count == 0) return;
                if (double.IsNaN(T0)) T0 = all.Min(x => x.Abs);
                var recs = Records(all, F, T0); foreach (var r in recs.Take(before.Count)) r.Before = true;
                lock (k) { Recs = recs; Before = before.Count; }
            }
            // only ICMP keeps its bytes (the router's message); everything else drops the payload right away
            static List<Pcap.Pkt> Strip(List<Pcap.Pkt> l) { foreach (var x in l) if (x.Proto != 1 && x.Proto != 58) x.Payload = Array.Empty<byte>(); return l; }
            // the rewind buffer (Wireshark's ring, if it's running): the last 20 s of this conversation before you clicked
            List<Pcap.Pkt> Rewind()
            {
                var got = new List<Pcap.Pkt>();
                try
                {
                    if (F.Remote == "" || !Directory.Exists(LiveCapture.RingDir)) return got;
                    var ring = Directory.GetFiles(LiveCapture.RingDir, "*.pcapng").OrderByDescending(x => System.IO.File.GetLastWriteTimeUtc(x)).Take(2).ToList();
                    // the newest file; the one before too only when the newest began inside the last 20 s (two 20 MB reads at most, then let go)
                    var newest = ring.Take(ring.Count > 1 && (DateTime.UtcNow - System.IO.File.GetCreationTimeUtc(ring[0])).TotalSeconds < 20 ? 2 : 1).ToList();
                    foreach (var rf in newest)
                    {
                        byte[] b; using (var fs = new FileStream(rf, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { if (fs.Length > 32L * 1024 * 1024) continue; b = new byte[fs.Length]; int g = 0, n; while (g < b.Length && (n = fs.Read(b, g, b.Length - g)) > 0) g += n; }
                        got.AddRange(Strip(Pcap.Parse(b, x => Belongs(x, F))));
                    }
                    double now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                    got = got.Where(x => x.Abs > now - 20).OrderBy(x => x.Abs).ToList();
                    if (got.Count > Cap) got = got.Skip(got.Count - Cap).ToList();
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
                return got;
            }
        }
    }
}
