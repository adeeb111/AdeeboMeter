// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace AdeeboMeter
{
    // ================================================================== MTU over UDP
    // The biggest UDP packet that gets through, for paths that drop ping (VPNs, some company networks) and for the UDP
    // traffic itself (QUIC, calls, games, DNS). Don't-fragment is set, sizes are searched by halving, and every size needs an
    // answer from the far end:
    //   port 443 - a QUIC packet with an unknown version; QUIC servers answer it with a short "versions I speak" list
    //              (RFC 9000 section 6) - no connection is made
    //   port 53  - a DNS question for "." padded to the size (EDNS padding, RFC 7830); the DNS server answers it
    // About 12 sizes, two tries each, at most ~40 small packets to the one target you typed.
    static class UdpMtu
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }

        public static List<string[]> Run(string target, string port, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string h = (target ?? "").Trim(); int p;
            if ((port ?? "").Trim() == "") p = 443; else if (!int.TryParse(port.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out p)) p = -1;   // "abc" is an error, not 443
            if (p != 443 && p != 53) { rows.Add(R(2, "Port", "443 (a QUIC server - most big websites) or 53 (a DNS server)", "", "")); return rows; }
            if (h == "") h = p == 53 ? "1.1.1.1" : "cloudflare.com";
            if (!Games.ValidHost(h)) { rows.Add(R(2, "Target", "a host name or an IP", "", "")); return rows; }
            var a = Lab.Addr(h); if (a == null) { rows.Add(R(2, "Target", "couldn't find " + h, "", "m")); return rows; }
            if (a.AddressFamily != AddressFamily.InterNetwork) { rows.Add(R(2, "Target", "IPv4 only for now - type an IPv4 address", "", "")); return rows; }
            string notOne = Lab.NotOne(a); if (notOne != "") { rows.Add(R(2, "Target", notOne, "This test talks to one server only - never to a whole network.", "")); return rows; }
            Program.Log("udp mtu to " + a + ":" + p);
            int hdr = 28;   // IPv4 20 + UDP 8
            int lo = 0, hi = 1500 - hdr, tries = 0; bool local = false;
            // the smallest first: if even that gets no answer, the server doesn't speak the protocol (or UDP is blocked)
            using (var u = new UdpClient(AddressFamily.InterNetwork))
            {
                u.Client.DontFragment = true; u.Client.ReceiveTimeout = 700; u.Connect(a, p);
                Func<int, int> probe = size =>   // 1 answered, 0 no answer, -1 refused locally (too big for this PC's own link)
                {
                    for (int t = 0; t < 2 && !run.Stop; t++)
                    {
                        tries++; byte[] pkt = p == 443 ? Quic(size) : DnsPad(size);
                        try { u.Send(pkt, pkt.Length); }
                        catch (SocketException e) { if (e.SocketErrorCode == SocketError.MessageSize) return -1; throw; }
                        var until = DateTime.UtcNow.AddMilliseconds(700);
                        while (DateTime.UtcNow < until)
                        {
                            try { var ep = new IPEndPoint(IPAddress.Any, 0); var r = u.Receive(ref ep); if (p == 443 ? IsVn(r) : IsDns(r, pkt)) return 1; }
                            catch (SocketException e) { if (e.SocketErrorCode == SocketError.TimedOut) break; if (e.SocketErrorCode == SocketError.ConnectionReset) return 2; throw; }   /* "port unreachable": the packet arrived (nothing listens) - the size got through */
                        }
                    }
                    return 0;
                };
                try
                {
                    run.Status = "Checking the server answers...";
                    int gate = probe(p == 443 ? 1200 : 100);   /* QUIC: the first datagram must be at least 1200 bytes of UDP payload (RFC 9000 14.1) */
                    if (run.Stop) { rows.Add(R(1, "Stopped", "", "", "m")); return rows; }
                    if (gate == 2) { rows.Add(R(2, a + ":" + p, "refused - nothing listens on UDP " + p + " there", "", "m")); return rows; }
                    if (gate != 1)
                    {
                        if (p == 443) { rows.Add(R(2, a + ":" + p, "no QUIC answer", "Either this server doesn't speak QUIC (HTTP/3), or UDP 443 is blocked on the way - many company networks block it so browsers fall back to TCP. Try cloudflare.com or google.com, or port 53 with a DNS server.", "m")); return rows; }
                        if (p == 53) { rows.Add(R(2, a + ":" + p, "no DNS answer", "This isn't a DNS server, or UDP 53 to it is blocked. Try 1.1.1.1 or your own DNS server.", "m")); return rows; }
                    }
                    lo = p == 443 ? 1200 : 100;
                    while (hi - lo > 8 && !run.Stop)
                    {
                        int mid = (lo + hi) / 2; run.Status = "Trying " + (mid + hdr) + " bytes...";
                        int r = probe(mid); if (r == 1 || r == 2) lo = mid; else { hi = mid; if (r == -1) local = true; }
                    }
                }
                catch (SocketException e) { rows.Add(R(2, "Send", Draw.Trunc(e.Message, 100), "", "m")); return rows; }
            }
            if (run.Stop) { rows.Add(R(1, "Stopped", "", "", "m")); return rows; }
            int mtu = lo + hdr;
            rows.Add(R(mtu >= 1492 ? 0 : mtu >= 1400 ? 1 : 2, "Biggest UDP packet through", "about " + mtu + " bytes (UDP payload " + lo + ")", mtu >= 1492 ? "Full size - nothing on the way shrinks UDP." : "Smaller than a normal 1500-byte network - a VPN, PPPoE or tunnel on the way. Calls and games are fine; big QUIC or VPN packets above this are dropped, which shows as some sites stalling.", "m"));
            if (local) rows.Add(R(3, "Limit known to Windows", "this PC already knew bigger packets can't leave (its path MTU)", "", "w"));
            rows.Add(R(3, "How", (p == 443 ? "QUIC version check" : "padded DNS question") + " to " + a + ":" + p + " · don't-fragment · " + tries + " packets", "Accurate to about 8 bytes. A size that gets no answer is counted as too big - on a lossy line run it twice.", "m"));
            return rows;
        }

        // one Random for every packet: two made in the same millisecond on .NET Framework share a seed (and their "random" ids)
        static readonly Random Rnd = new Random();
        // QUIC long header with a reserved (never used) version - servers must reply with Version Negotiation
        public static byte[] Quic(int size)
        {
            var b = new byte[Math.Max(size, 30)]; var rnd = Rnd; lock (rnd) {
            b[0] = (byte)(0xC0 | rnd.Next(16)); b[1] = 0x1a; b[2] = 0x2a; b[3] = 0x3a; b[4] = 0x4a;   // version 0x?a?a?a?a is reserved for this
            b[5] = 8; for (int i = 0; i < 8; i++) b[6 + i] = (byte)rnd.Next(256);    // destination connection id
            b[14] = 8; for (int i = 0; i < 8; i++) b[15 + i] = (byte)rnd.Next(256);  // source connection id; the rest is zero padding
            }
            return b;
        }
        public static bool IsVn(byte[] r) { return r != null && r.Length >= 7 && (r[0] & 0x80) != 0 && r[1] == 0 && r[2] == 0 && r[3] == 0 && r[4] == 0; }
        // DNS: "." NS with an EDNS OPT record carrying a padding option that makes the datagram exactly size bytes
        public static byte[] DnsPad(int size)
        {
            int id0; lock (Rnd) id0 = Rnd.Next(65536); var id = (ushort)id0;
            var head = new List<byte> { (byte)(id >> 8), (byte)id, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 2, 0, 1 };   // RD, 1 question, 1 additional; "." NS IN
            var opt = new List<byte> { 0, 0, 41, 0x10, 0x00, 0, 0, 0, 0 };   // root, OPT, UDP size 4096, no flags
            int pad = Math.Max(0, size - head.Count - opt.Count - 2 - 4);
            opt.Add((byte)((pad + 4) >> 8)); opt.Add((byte)(pad + 4));
            opt.AddRange(new byte[] { 0, 12, (byte)(pad >> 8), (byte)pad }); opt.AddRange(new byte[pad]);
            return head.Concat(opt).ToArray();
        }
        public static bool IsDns(byte[] r, byte[] q) { return r != null && r.Length >= 12 && r[0] == q[0] && r[1] == q[1] && (r[2] & 0x80) != 0; }
    }
}
