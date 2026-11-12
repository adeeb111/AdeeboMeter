// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;

namespace AdeeboMeter
{
    // ================================================================== A small capture-file reader (no Wireshark needed)
    // Reads .pcap and .pcapng files - Ethernet (with VLAN tags), raw IP and Linux "cooked" captures - and splits each packet
    // into IPv4 / IPv6, UDP and ICMP / ICMPv6. Enough for the DHCP, Router Advertisement and ICMP replays. Files are only read;
    // anything malformed is skipped, never trusted: every length is checked against what's really there. 64 MB / 200k packets max.
    static class Pcap
    {
        public sealed class Pkt
        {
            public double T; public string SrcMac = "";
            public IPAddress Src, Dst; public int Proto = -1, SrcPort, DstPort, Ttl;
            public byte[] Payload = Array.Empty<byte>();   // UDP payload, or the ICMP / ICMPv6 message (type, code, checksum, rest)
            public bool V6;
        }
        public const long MaxBytes = 64L * 1024 * 1024; public const int MaxPackets = 200000;

        public static List<Pkt> Read(string path, out string error)
        {
            error = ""; var list = new List<Pkt>();
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) { error = "file not found"; return list; }
                if (fi.Length > MaxBytes) { error = "bigger than 64 MB - filter it first in Wireshark (File › Export Specified Packets)"; return list; }
                var b = File.ReadAllBytes(path);
                if (b.Length >= 24 && (U32(b, 0, true) == 0xa1b2c3d4 || U32(b, 0, true) == 0xa1b23c4d || U32(b, 0, false) == 0xa1b2c3d4 || U32(b, 0, false) == 0xa1b23c4d)) Classic(b, list);
                else if (b.Length >= 28 && U32(b, 0, true) == 0x0A0D0D0A) Ng(b, list);
                else error = "not a pcap or pcapng file";
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is NotSupportedException || e is ArgumentException)) throw; error = e.Message; }
            return list;
        }
        public static List<Pkt> Parse(byte[] b)
        {
            var list = new List<Pkt>();
            if (b.Length >= 24 && (U32(b, 0, true) == 0xa1b2c3d4 || U32(b, 0, false) == 0xa1b2c3d4 || U32(b, 0, true) == 0xa1b23c4d || U32(b, 0, false) == 0xa1b23c4d)) Classic(b, list);
            else if (b.Length >= 28 && U32(b, 0, true) == 0x0A0D0D0A) Ng(b, list);
            return list;
        }
        static uint U32(byte[] b, int o, bool le) { return le ? (uint)(b[o] | b[o + 1] << 8 | b[o + 2] << 16 | b[o + 3] << 24) : (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]); }
        static int U16(byte[] b, int o, bool le) { return le ? b[o] | b[o + 1] << 8 : b[o] << 8 | b[o + 1]; }
        static void Classic(byte[] b, List<Pkt> list)
        {
            bool le = U32(b, 0, true) == 0xa1b2c3d4 || U32(b, 0, true) == 0xa1b23c4d; bool ns = U32(b, 0, le) == 0xa1b23c4d;
            int link = (int)(U32(b, 20, le) & 0xFFFF); int p = 24; double t0 = -1;
            while (p + 16 <= b.Length && list.Count < MaxPackets)
            {
                double t = U32(b, p, le) + U32(b, p + 4, le) / (ns ? 1e9 : 1e6); int len = (int)Math.Min(U32(b, p + 8, le), 262144u);
                p += 16; if (len < 0 || p + len > b.Length) break;
                if (t0 < 0) t0 = t;
                var k = Decode(b, p, len, link); if (k != null) { k.T = t - t0; list.Add(k); }
                p += len;
            }
        }
        static void Ng(byte[] b, List<Pkt> list)
        {
            int p = 0; bool le = true; var links = new List<int>(); double t0 = -1;
            while (p + 12 <= b.Length && list.Count < MaxPackets)
            {
                uint type = U32(b, p, le);
                if (type == 0x0A0D0D0A) { if (p + 12 > b.Length) break; le = U32(b, p + 8, true) == 0x1A2B3C4D; links.Clear(); }
                int blen = (int)U32(b, p + 4, le); if (blen < 12 || blen % 4 != 0 || p + blen > b.Length) break;
                if (type == 1 && blen >= 20) links.Add(U16(b, p + 8, le));
                else if (type == 6 && blen >= 32)
                {
                    int ifc = (int)U32(b, p + 8, le); long ts = ((long)U32(b, p + 12, le) << 32) | U32(b, p + 16, le); int cap = (int)U32(b, p + 20, le);
                    if (cap >= 0 && 28 + cap <= blen && ifc < links.Count)
                    {
                        double t = ts / 1e6; if (t0 < 0) t0 = t;   // the default (and by far the most common) resolution: microseconds
                        var k = Decode(b, p + 28, cap, links[ifc]); if (k != null) { k.T = t - t0; list.Add(k); }
                    }
                }
                else if (type == 3 && blen >= 16 && links.Count > 0)
                {
                    int cap = Math.Min(blen - 16, (int)U32(b, p + 8, le)); if (cap > 0) { var k = Decode(b, p + 12, cap, links[0]); if (k != null) { k.T = list.Count > 0 ? list[list.Count - 1].T : 0; list.Add(k); } }
                }
                p += blen;
            }
        }
        // link layer -> IP
        static Pkt Decode(byte[] b, int o, int len, int link)
        {
            int end = o + len; string mac = ""; int et, ip;
            if (link == 1)
            {
                if (len < 14) return null;
                mac = string.Join(":", Enumerable.Range(o + 6, 6).Select(i => b[i].ToString("x2", CultureInfo.InvariantCulture)));
                et = b[o + 12] << 8 | b[o + 13]; ip = o + 14;
                while ((et == 0x8100 || et == 0x88a8) && ip + 4 <= end) { et = b[ip + 2] << 8 | b[ip + 3]; ip += 4; }
            }
            else if (link == 101 || link == 12 || link == 14) { if (len < 1) return null; et = (b[o] >> 4) == 6 ? 0x86dd : 0x0800; ip = o; }
            else if (link == 113) { if (len < 16) return null; et = b[o + 14] << 8 | b[o + 15]; ip = o + 16; }
            else if (link == 276) { if (len < 20) return null; et = b[o] << 8 | b[o + 1]; ip = o + 20; }
            else return null;
            Pkt k = et == 0x0800 ? V4(b, ip, end) : et == 0x86dd ? V6(b, ip, end) : null;
            if (k != null) k.SrcMac = mac;
            return k;
        }
        static Pkt V4(byte[] b, int o, int end)
        {
            if (o + 20 > end || (b[o] >> 4) != 4) return null;
            int ihl = (b[o] & 15) * 4, tot = b[o + 2] << 8 | b[o + 3]; if (ihl < 20 || o + ihl > end) return null;
            int stop = Math.Min(end, o + Math.Max(tot, ihl));
            var k = new Pkt { Proto = b[o + 9], Ttl = b[o + 8], Src = new IPAddress(Sub(b, o + 12, 4)), Dst = new IPAddress(Sub(b, o + 16, 4)) };
            if (((b[o + 6] & 0x1F) << 8 | b[o + 7]) != 0) return k;   // a later fragment: no transport header in it
            L4(k, b, o + ihl, stop); return k;
        }
        static Pkt V6(byte[] b, int o, int end)
        {
            if (o + 40 > end || (b[o] >> 4) != 6) return null;
            int stop = Math.Min(end, o + 40 + (b[o + 4] << 8 | b[o + 5])); int nh = b[o + 6], p = o + 40;
            var k = new Pkt { V6 = true, Ttl = b[o + 7], Src = new IPAddress(Sub(b, o + 8, 16)), Dst = new IPAddress(Sub(b, o + 24, 16)) };
            for (int guard = 0; guard < 8 && (nh == 0 || nh == 43 || nh == 60 || nh == 44); guard++)
            {
                if (p + 8 > stop) return k;
                int n2 = b[p]; int l = nh == 44 ? 8 : (b[p + 1] + 1) * 8; if (nh == 44 && ((b[p + 2] << 8 | b[p + 3]) & 0xFFF8) != 0) { k.Proto = 44; return k; }
                nh = n2; p += l;
            }
            k.Proto = nh; L4(k, b, p, stop); return k;
        }
        static void L4(Pkt k, byte[] b, int o, int stop)
        {
            if (o > stop) return;
            if (k.Proto == 17 && o + 8 <= stop) { k.SrcPort = b[o] << 8 | b[o + 1]; k.DstPort = b[o + 2] << 8 | b[o + 3]; k.Payload = Sub(b, o + 8, stop - o - 8); }
            else if (k.Proto == 6 && o + 20 <= stop) { k.SrcPort = b[o] << 8 | b[o + 1]; k.DstPort = b[o + 2] << 8 | b[o + 3]; }
            else if ((k.Proto == 1 || k.Proto == 58) && o + 4 <= stop) k.Payload = Sub(b, o, stop - o);
        }
        static byte[] Sub(byte[] b, int o, int n) { n = Math.Max(0, Math.Min(n, b.Length - o)); var r = new byte[n]; Array.Copy(b, o, r, 0, n); return r; }

        // ---------- writing (the sample captures and the self-test): classic pcap, Ethernet
        public sealed class Writer
        {
            readonly List<byte> ms = new List<byte>(); double t;
            public Writer() { W(0xa1b2c3d4); W16(2); W16(4); W(0); W(0); W(65535); W(1); }
            void W(uint v) { ms.Add((byte)v); ms.Add((byte)(v >> 8)); ms.Add((byte)(v >> 16)); ms.Add((byte)(v >> 24)); }
            void W16(int v) { ms.Add((byte)v); ms.Add((byte)(v >> 8)); }
            public void Add(double gap, byte[] frame) { t += gap; W((uint)(1762900000 + (long)t)); W((uint)((t - Math.Floor(t)) * 1e6)); W((uint)frame.Length); W((uint)frame.Length); ms.AddRange(frame); }
            public byte[] Bytes() { return ms.ToArray(); }
        }
        public static byte[] Mac(string s) { return s.Split(':').Select(x => Convert.ToByte(x, 16)).ToArray(); }
        public static byte[] Eth(string dst, string src, int type, byte[] body) { var f = new List<byte>(); f.AddRange(Mac(dst)); f.AddRange(Mac(src)); f.Add((byte)(type >> 8)); f.Add((byte)type); f.AddRange(body); return f.ToArray(); }
        static ushort Sum(IEnumerable<byte> data)
        {
            long s = 0; byte hi = 0; bool odd = false;
            foreach (var x in data) { if (!odd) hi = x; else s += hi << 8 | x; odd = !odd; }
            if (odd) s += hi << 8; while ((s >> 16) != 0) s = (s & 0xFFFF) + (s >> 16); return (ushort)~s;
        }
        public static byte[] Ip4(string src, string dst, int proto, int ttl, byte[] body)
        {
            var h = new byte[20]; h[0] = 0x45; int tot = 20 + body.Length; h[2] = (byte)(tot >> 8); h[3] = (byte)tot; h[8] = (byte)ttl; h[9] = (byte)proto;
            Array.Copy(IPAddress.Parse(src).GetAddressBytes(), 0, h, 12, 4); Array.Copy(IPAddress.Parse(dst).GetAddressBytes(), 0, h, 16, 4);
            var c = Sum(h); h[10] = (byte)(c >> 8); h[11] = (byte)c; return h.Concat(body).ToArray();
        }
        public static byte[] Udp(int sp, int dp, byte[] body) { int l = 8 + body.Length; return new[] { (byte)(sp >> 8), (byte)sp, (byte)(dp >> 8), (byte)dp, (byte)(l >> 8), (byte)l, (byte)0, (byte)0 }.Concat(body).ToArray(); }
        public static byte[] Icmp4(int type, int code, byte[] rest) { var m = new byte[] { (byte)type, (byte)code, 0, 0 }.Concat(rest).ToArray(); var c = Sum(m); m[2] = (byte)(c >> 8); m[3] = (byte)c; return m; }
        public static byte[] Ip6Icmp(string src, string dst, int hop, int type, int code, byte[] rest)
        {
            var msg = new byte[] { (byte)type, (byte)code, 0, 0 }.Concat(rest).ToArray();
            byte[] s = IPAddress.Parse(src).GetAddressBytes(), d = IPAddress.Parse(dst).GetAddressBytes();
            var pseudo = s.Concat(d).Concat(new[] { (byte)(msg.Length >> 24), (byte)(msg.Length >> 16), (byte)(msg.Length >> 8), (byte)msg.Length, (byte)0, (byte)0, (byte)0, (byte)58 }).Concat(msg);
            var c = Sum(pseudo); msg[2] = (byte)(c >> 8); msg[3] = (byte)c;
            var h = new byte[40]; h[0] = 0x60; h[4] = (byte)(msg.Length >> 8); h[5] = (byte)msg.Length; h[6] = 58; h[7] = (byte)hop; Array.Copy(s, 0, h, 8, 16); Array.Copy(d, 0, h, 24, 16);
            return h.Concat(msg).ToArray();
        }
    }
}
