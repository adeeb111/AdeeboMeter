// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // Calculators: packet stack against an MTU, VLSM planner, bandwidth / transfer time, decimal / hex / binary.
    // Pure arithmetic - nothing is sent, nothing is read from the PC. The packet stack is also the engine the packet
    // journey draws its label stacks with.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "result rows and small tables, built once")]
    static class Calc
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string I(long n) { return n.ToString("N0", CultureInfo.CurrentCulture); }
        static string F(double d, string fmt) { return d.ToString(fmt, CultureInfo.CurrentCulture); }

        // ================================================================== packet stack
        // kind: p = payload side (inside the inner IP), i = IP, t = tunnel (its whole outer cost rides inside the link MTU),
        // l = link shim inside the Ethernet payload (PPPoE), f = frame (outside the MTU: tags, labels, Ethernet itself),
        // r = per record, not per packet (TLS: 22 bytes per up-to-16 KB record) - shown, never subtracted from the MTU
        public sealed class Layer { public string Key, Name, Kind, Note; public int Bytes; public bool Approx; }
        static readonly Layer[] Known = {
            L("tcp", "TCP", "p", 20, "20 bytes without options; +12 with timestamps (most Linux and macOS hosts)"),
            L("udp", "UDP", "p", 8, ""),
            L("icmp", "ICMP echo", "p", 8, ""),
            L("tls", "TLS 1.3 record", "r", 22, "5-byte header + 1 content type + 16-byte tag per record of up to 16 KB - spread over about 11 packets, so it doesn't change the MSS or the MTU", true),
            L("quic", "QUIC short header", "p", 25, "1 + connection ID (often 8) + packet number (1-4) + 16-byte tag; rides on UDP", true),
            L("rtp", "RTP", "p", 12, "voice / video; +4 per contributing source"),
            L("dns", "DNS header", "p", 12, ""),
            L("ipv4", "IPv4", "i", 20, "20 bytes without options"),
            L("ipv6", "IPv6", "i", 40, "40 bytes; extension headers add more"),
            L("wireguard", "WireGuard (outer IPv4)", "t", 60, "20 IPv4 + 8 UDP + 16 header + 16 tag"),
            L("wireguard6", "WireGuard (outer IPv6)", "t", 80, "40 IPv6 + 8 UDP + 16 header + 16 tag"),
            L("ipsec", "IPsec ESP tunnel, AES-GCM", "t", 58, "20 outer IPv4 + 8 ESP + 8 IV + 2 trailer + 16 ICV + up to 3 padding (worst case 4-byte alignment)", true),
            L("ipsec-natt", "IPsec ESP over NAT (UDP 4500)", "t", 66, "ESP tunnel + 8 UDP, used when there's NAT on the way", true),
            L("gre", "GRE (outer IPv4)", "t", 24, "20 outer IPv4 + 4 GRE; +4 with a key"),
            L("vxlan", "VXLAN (outer IPv4)", "t", 50, "20 IPv4 + 8 UDP + 8 VXLAN + 14 inner Ethernet"),
            L("gtpu", "GTP-U (mobile user plane)", "t", 36, "20 IPv4 + 8 UDP + 8 GTP; +4 with options"),
            L("sslvpn", "SSL VPN over TLS (typical)", "t", 82, "varies by product - 20 IPv4 + 20 TCP or 8 UDP/DTLS + record and tag; check the client's own MTU", true),
            L("pppoe", "PPPoE", "l", 8, "6 PPPoE + 2 PPP - DSL and many fibre links; leaves 1492 for IP"),
            L("vlan", "802.1Q VLAN tag", "f", 4, "outside the IP MTU; the switch must allow 1522-byte frames"),
            L("qinq", "Q-in-Q (two tags)", "f", 8, "outside the IP MTU"),
            L("mpls", "MPLS label", "f", 4, "per label - outside the customer's IP MTU but inside the core link's; raise the core MTU by 4 per label"),
            L("ethernet", "Ethernet", "f", 18, "14 header + 4 FCS; on the wire also 8 preamble + 12 gap"),
            L("wifi", "Wi-Fi (802.11 data)", "f", 36, "about 24-30 header + 8 LLC/SNAP + 4 FCS; QoS and security add more", true) };
        static Layer L(string k, string n, string kind, int b, string note) { return L(k, n, kind, b, note, false); }
        static Layer L(string k, string n, string kind, int b, string note, bool approx) { return new Layer { Key = k, Name = n, Kind = kind, Bytes = b, Note = note, Approx = approx }; }
        static readonly Dictionary<string, string> Alias = new Dictionary<string, string> { { "ip", "ipv4" }, { "v4", "ipv4" }, { "v6", "ipv6" }, { "wg", "wireguard" }, { "esp", "ipsec" }, { "nat-t", "ipsec-natt" }, { "natt", "ipsec-natt" }, { "eth", "ethernet" }, { "802.1q", "vlan" }, { "dot1q", "vlan" }, { "label", "mpls" }, { "gtp", "gtpu" }, { "https", "tls" }, { "ping", "icmp" }, { "wlan", "wifi" }, { "ssl", "sslvpn" } };
        static readonly string[][] Presets = {
            new[] { "web", "tls tcp ipv4 ethernet" }, new[] { "game", "udp ipv4 ethernet" }, new[] { "call", "rtp udp ipv4 ethernet" },
            new[] { "dsl", "tcp ipv4 pppoe ethernet" }, new[] { "vpn", "tcp ipv4 wireguard ethernet" }, new[] { "office vpn", "tcp ipv4 ipsec-natt ethernet" } };

        // parses "tcp ipv4 wireguard pppoe ethernet" (top to bottom; "mpls x2" repeats); error = the word it didn't know
        public static List<Layer> Parse(string text, out string error)
        {
            error = null; string t = (text ?? "").Trim().ToLowerInvariant();
            foreach (var p in Presets) if (t == p[0]) t = p[1];
            if (t == "") t = "tcp ipv4 ethernet";
            var list = new List<Layer>(); var words = t.Split(new[] { ' ', ',', '>', '/', '+', '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i]; if (w == "over" || w == "on" || w == "in") continue;
                var m = Regex.Match(w, @"^x([0-9])$"); if (m.Success && list.Count > 0) { int n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); for (int k = 1; k < n && k < 8; k++) list.Add(list[list.Count - 1]); continue; }
                string key; if (Alias.TryGetValue(w, out key)) w = key;
                var l = Known.FirstOrDefault(x => x.Key == w);
                if (l == null) { error = w; return list; }
                list.Add(l);
            }
            return list;
        }
        public static IEnumerable<string> LayerKeys { get { return Known.Select(k => k.Key); } }

        public static List<string[]> Stack(string layers, string mtuText, string payloadText)
        {
            var rows = new List<string[]>(); string err; var st = Parse(layers, out err);
            if (err != null) { rows.Add(R(2, "Layers", "\"" + err + "\" isn't a layer I know - try: " + string.Join(", ", Known.Select(k => k.Key)) + ", or a preset: " + string.Join(", ", Presets.Select(p => p[0])), "", "u")); return rows; }
            int mtu = 1500; if (!string.IsNullOrWhiteSpace(mtuText) && (!int.TryParse(mtuText.Trim().Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out mtu) || mtu < 576 || mtu > 9216)) { rows.Add(R(2, "MTU", "type a link MTU between 576 and 9216 (1500 = normal Ethernet)", "", "u")); return rows; }
            int payload = -1; if (!string.IsNullOrWhiteSpace(payloadText) && !int.TryParse(payloadText.Trim().Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out payload)) { rows.Add(R(2, "Payload", "type the data size in bytes, e.g. 1400", "", "u")); return rows; }
            int ips = st.Count(x => x.Kind == "i"); if (ips == 0) { rows.Add(R(2, "Layers", "add an IP layer (ipv4 or ipv6) - the MTU is counted against it", "", "u")); return rows; }
            int pay = st.Where(x => x.Kind == "p").Sum(x => x.Bytes), ip = st.Where(x => x.Kind == "i").Sum(x => x.Bytes), tun = st.Where(x => x.Kind == "t").Sum(x => x.Bytes), shim = st.Where(x => x.Kind == "l").Sum(x => x.Bytes), frame = st.Where(x => x.Kind == "f").Sum(x => x.Bytes);
            bool approx = st.Any(x => x.Approx && x.Kind != "r"); string ap = approx ? "~" : "";
            int innerMtu = mtu - shim - tun;          // what the inner IP packet may be
            int maxData = innerMtu - ip - pay;        // data per packet without splitting
            rows.Add(R(3, "Stack", string.Join(" > ", st.Select(x => x.Name)), "Top is what the app sends; bottom is what goes on the wire.", "i"));
            foreach (var x in st) rows.Add(R(3, "  " + x.Name, (x.Approx ? "~" : "") + x.Bytes + " bytes" + (x.Kind == "f" ? " (outside the MTU)" : x.Kind == "r" ? " per record, not per packet" : ""), x.Note, "i"));
            if (maxData <= 0) { rows.Add(R(2, "Fits?", "the headers alone are bigger than an MTU of " + mtu, "", "i")); return rows; }
            bool tcp = st.Any(x => x.Key == "tcp");
            rows.Add(R(0, tcp ? "Biggest segment (MSS)" : "Biggest data per packet", ap + I(maxData) + " bytes", tcp ? "What TCP should advertise so nothing splits: link MTU " + mtu + " - " + (shim + tun > 0 ? (shim + tun) + " tunnel/PPPoE - " : "") + ip + " IP - " + pay + " above." : "", "i"));
            if (tun + shim > 0) rows.Add(R(1, "Set the inner MTU to", ap + I(innerMtu), "The tunnel (or PPPoE) eats " + ap + (tun + shim) + " bytes of the " + mtu + " - set the VPN adapter's MTU (or the router's MSS clamp) to this, or big packets split or vanish.", "i"));
            int wire = mtu + frame;
            int full = maxData + pay + ip + tun + shim + frame; double eff = 100.0 * maxData / Math.Max(1, full + (st.Any(x => x.Key == "ethernet") ? 20 : 0));
            rows.Add(R(3, "Frame on the wire", ap + I(wire) + " bytes" + (st.Any(x => x.Key == "ethernet") ? " (+20 preamble and gap)" : ""), "", "i"));
            rows.Add(R(3, "Efficiency", ap + F(eff, "0.0") + "% of the line carries your data at full size", "Small packets carry far less: a 100-byte game update is mostly headers.", "i"));
            if (payload >= 0)
            {
                if (payload <= maxData) rows.Add(R(0, I(payload) + " bytes of data", "fits in one packet (" + ap + I(payload + pay + ip + tun + shim) + " of " + mtu + ")", "", "i"));
                else
                {
                    // TCP: segments of maxData. Fragments: each carries a multiple of 8 bytes of the inner packet's payload, and
                    // the UDP/ICMP header rides only in the first one - so count the whole datagram, not data per packet
                    int fragData = (innerMtu - ip) / 8 * 8;
                    int n = tcp ? (int)Math.Ceiling(payload / (double)maxData) : (int)Math.Ceiling((payload + pay) / (double)Math.Max(8, fragData));
                    if (tcp) rows.Add(R(3, I(payload) + " bytes of data", "TCP sends it as " + n + " segments - normal", "", "i"));
                    else rows.Add(R(1, I(payload) + " bytes of data", "too big for one packet: IP splits it into " + n + " fragments, or drops it if Don't Fragment is set (IPv6 never splits on the way)", "Fragments get lost or blocked by firewalls more often - keep UDP data under " + maxData + ".", "i"));
                }
            }
            return rows;
        }

        // ================================================================== VLSM planner
        public static List<string[]> Vlsm(string block, string sizes)
        {
            var rows = new List<string[]>(); string b = (block ?? "").Trim(); int slash = b.IndexOf('/'); IPAddress ip; int pre;
            if (slash < 0 || (ip = V4(b.Substring(0, slash))) == null || !int.TryParse(b.Substring(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out pre) || pre > 30)
            { rows.Add(R(2, "Network", "type an IPv4 block, e.g. 10.0.0.0/22", "", "u")); return rows; }
            var need = new List<int>(); foreach (var w in (sizes ?? "").Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)) { int n; if (!int.TryParse(w, NumberStyles.None, CultureInfo.InvariantCulture, out n) || n < 1 || n > 16777214) { rows.Add(R(2, "Sizes", "\"" + w + "\" isn't a host count - type e.g. 200, 60, 12, 2", "", "u")); return rows; } need.Add(n); }
            if (need.Count == 0) { rows.Add(R(2, "Sizes", "type how many hosts each subnet needs, e.g. 200, 60, 12, 2", "", "u")); return rows; }
            if (need.Count > 64) { rows.Add(R(2, "Sizes", "64 subnets at most", "", "u")); return rows; }
            uint start = Be(ip.GetAddressBytes()); uint bmask = pre == 0 ? 0 : 0xFFFFFFFFu << (32 - pre); start &= bmask; ulong end = (ulong)start + (1UL << (32 - pre));
            rows.Add(R(3, "Block", U(start) + "/" + pre + "  (" + I(1L << (32 - pre)) + " addresses)", "Biggest first, so every subnet lands on its own boundary.", "i"));
            var order = need.Select((n, i) => new { n, i }).OrderByDescending(x => x.n).ThenBy(x => x.i).ToList();
            ulong at = start; long used = 0; bool full = false;
            foreach (var x in order)
            {
                int p = 32; while (p > 0 && (1L << (32 - p)) - (p <= 30 ? 2 : 0) < x.n) p--;   // /31 and /32 have no network/broadcast
                if (x.n <= 2) p = 30;   // one or two hosts: a /30, so there is a gateway and a host (a /31 works on point-to-point links)
                ulong size = 1UL << (32 - p);
                ulong a = at % size != 0 ? at + size - at % size : at;   // its boundary - kept only if it fits, so smaller ones can still use the room
                if (a + size > end) { rows.Add(R(2, "Subnet " + (x.i + 1) + " (" + I(x.n) + " hosts)", "doesn't fit - not enough room left in the block", "", "i")); full = true; continue; }
                at = a;
                uint net = (uint)at, bc = (uint)(at + size - 1);
                string range = p <= 30 ? U(net + 1) + " - " + U(bc - 1) : U(net) + " - " + U(bc);
                long usable = p <= 30 ? (long)size - 2 : (long)size;
                rows.Add(R(0, "Subnet " + (x.i + 1) + " (" + I(x.n) + " hosts)", U(net) + "/" + p + "  ·  " + range + "  ·  gateway " + U(net + 1) + (p <= 30 ? "  ·  broadcast " + U(bc) : ""), I(usable) + " usable, " + I(usable - x.n) + " spare · mask " + U(p == 0 ? 0 : 0xFFFFFFFFu << (32 - p)), "i"));
                at += size; used += (long)size;
            }
            long total = 1L << (32 - pre);
            rows.Add(R(full ? 2 : 3, "Used", I(used) + " of " + I(total) + " addresses" + (at < end && !full ? " · free from " + U((uint)at) : ""), full ? "Use a bigger block (a shorter prefix) or fewer hosts." : "", "i"));
            return rows;
        }
        // a dotted IPv4 address read the way people mean it: four decimal numbers 0-255 ("010.001.002.003" is 10.1.2.3,
        // never octal as IPAddress.Parse reads it; "0x0A..." is refused)
        static IPAddress V4(string t)
        {
            var m = Regex.Match((t ?? "").Trim(), @"^([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})$"); if (!m.Success) return null;
            var b = new byte[4]; for (int i = 0; i < 4; i++) { int v = int.Parse(m.Groups[i + 1].Value, CultureInfo.InvariantCulture); if (v > 255) return null; b[i] = (byte)v; }
            return new IPAddress(b);
        }
        static uint Be(byte[] b) { return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]); }
        static string U(uint v) { return new IPAddress(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }).ToString(); }

        // ================================================================== bandwidth and transfer time
        // any two of size / speed / time give the third; speed alone gives the conversions
        public static List<string[]> Bandwidth(string sizeText, string speedText, string timeText)
        {
            var rows = new List<string[]>(); double bytes = Size(sizeText), bps = Speed(speedText), secs = Time(timeText);
            if (double.IsNaN(bytes) && !string.IsNullOrWhiteSpace(sizeText)) { rows.Add(R(2, "Size", "type e.g. 4.7 GB, 300 MB or 1.2 TB", "", "u")); return rows; }
            if (double.IsNaN(bps) && !string.IsNullOrWhiteSpace(speedText)) { rows.Add(R(2, "Speed", "type e.g. 100 Mbps, 1 Gbps or 12 MB/s", "", "u")); return rows; }
            if (double.IsNaN(secs) && !string.IsNullOrWhiteSpace(timeText)) { rows.Add(R(2, "Time", "type e.g. 90 s, 10 min or 2 h", "", "u")); return rows; }
            int have = (double.IsNaN(bytes) ? 0 : 1) + (double.IsNaN(bps) ? 0 : 1) + (double.IsNaN(secs) ? 0 : 1);
            if (have == 0) { rows.Add(R(2, "Input", "type a size and a speed (how long?), a size and a time (how fast?), or just a speed (conversions)", "", "u")); return rows; }
            if (bps == 0 || secs == 0) { rows.Add(R(2, bps == 0 ? "Speed" : "Time", "has to be more than 0", "", "u")); return rows; }
            if (!double.IsNaN(bytes) && !double.IsNaN(bps) && bps > 0)
            {
                double t = bytes * 8 / bps;
                rows.Add(R(0, "Time", Dur(t) + " at the full " + Bits(bps), "", "i"));
                rows.Add(R(3, "Realistic", Dur(t / 0.94) + " - TCP and IP headers take about 5-6%; Wi-Fi usually gets half to two thirds of its link rate", "", "i"));
            }
            else if (!double.IsNaN(bytes) && !double.IsNaN(secs) && secs > 0)
            { bps = bytes * 8 / secs; rows.Add(R(0, "Speed needed", Bits(bps) + "  (" + ByteRate(bps) + ")", "Plus about 6% for headers: " + Bits(bps / 0.94) + " on the line.", "i")); }
            else if (!double.IsNaN(bps) && !double.IsNaN(secs)) { rows.Add(R(0, "Moves", ByteSize(bps * secs / 8) + " in " + Dur(secs), "", "i")); }
            if (!double.IsNaN(bps) && bps > 0)
            {
                rows.Add(R(3, "Same speed", Bits(bps) + " = " + ByteRate(bps) + " = " + ByteSize(bps / 8 * 3600) + " an hour", "Network speeds are in bits; file sizes in bytes (8 bits). Sizes here are decimal (1 MB = 1,000,000 bytes), like ISPs and Windows' network pages.", "i"));
                rows.Add(R(3, "Full-size packets", I((long)(bps / 8 / 1538)) + " a second (1500-byte Ethernet)  ·  " + F(1538 * 8 / bps * 1e6, "0.##") + " µs each on the wire", "", "i"));
            }
            return rows;
        }
        // "1,500 MB" = 1500 (a comma before exactly 3 digits is a thousands separator); "4,7 GB" = 4.7 (a decimal comma)
        static readonly Regex Num = new Regex(@"^\s*([0-9]{1,3}(?:,[0-9]{3})+(?:\.[0-9]+)?|[0-9]+(?:[.,][0-9]+)?)\s*([a-zA-Z/µ]*)\s*$");
        static double Parse2(string t, out string unit)
        {
            unit = ""; var m = Num.Match(t ?? ""); if (!m.Success) return double.NaN; unit = m.Groups[2].Value; string n = m.Groups[1].Value;
            n = Regex.IsMatch(n, @"^[0-9]{1,3}(,[0-9]{3})+(\.[0-9]+)?$") ? n.Replace(",", "") : n.Replace(',', '.');
            double v; return double.TryParse(n, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out v) && !double.IsInfinity(v) ? v : double.NaN;
        }
        static double Size(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return double.NaN; string u; double v = Parse2(t, out u); if (double.IsNaN(v)) return v;
            switch (u.ToLowerInvariant()) { case "": case "b": case "bytes": return v; case "kb": return v * 1e3; case "mb": return v * 1e6; case "gb": return v * 1e9; case "tb": return v * 1e12; case "kib": return v * 1024; case "mib": return v * 1048576; case "gib": return v * 1073741824.0; case "tib": return v * 1099511627776.0; default: return double.NaN; }
        }
        static double Speed(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return double.NaN; string u; double v = Parse2(t, out u); if (double.IsNaN(v)) return v;
            string k = u.Replace("/s", "ps"); if (k == "") return v * 1e6;   // a bare number is Mbps, how ISPs sell it
            switch (k) { case "bps": return v; case "kbps": case "Kbps": case "kbit": case "kbitps": return v * 1e3; case "Mbps": case "mbps": case "Mbit": case "mbit": case "Mbitps": case "mbitps": return v * 1e6; case "Gbps": case "gbps": case "Gbit": case "gbit": case "Gbitps": case "gbitps": return v * 1e9; case "Bps": return v * 8; case "KBps": case "kBps": return v * 8e3; case "MBps": return v * 8e6; case "GBps": return v * 8e9; case "KiBps": return v * 8 * 1024; case "MiBps": return v * 8 * 1048576; case "GiBps": return v * 8 * 1073741824.0; default: return double.NaN; }
        }
        static double Time(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return double.NaN; string u; double v = Parse2(t, out u); if (double.IsNaN(v)) return v;
            switch (u.ToLowerInvariant()) { case "": case "s": case "sec": case "secs": return v; case "ms": return v / 1000; case "m": case "min": case "mins": return v * 60; case "h": case "hr": case "hrs": case "hour": case "hours": return v * 3600; case "d": case "day": case "days": return v * 86400; default: return double.NaN; }
        }
        static string Bits(double bps) { return bps >= 1e9 ? F(bps / 1e9, "0.##") + " Gbps" : bps >= 1e6 ? F(bps / 1e6, "0.##") + " Mbps" : bps >= 1e3 ? F(bps / 1e3, "0.##") + " kbps" : F(bps, "0") + " bps"; }
        static string ByteRate(double bps) { return ByteSize(bps / 8) + "/s"; }
        static string ByteSize(double b) { return b >= 1e12 ? F(b / 1e12, "0.##") + " TB" : b >= 1e9 ? F(b / 1e9, "0.##") + " GB" : b >= 1e6 ? F(b / 1e6, "0.##") + " MB" : b >= 1e3 ? F(b / 1e3, "0.##") + " kB" : F(b, "0") + " bytes"; }
        static string Dur(double s) { if (s < 0.001) return F(s * 1e6, "0.#") + " µs"; if (s < 1) return F(s * 1000, "0.#") + " ms"; if (s < 90) return F(s, "0.#") + " s"; if (s < 5400) return F(s / 60, "0.#") + " min"; if (s < 172800) return F(s / 3600, "0.#") + " h"; return F(s / 86400, "0.#") + " days"; }

        // ================================================================== decimal / hex / binary
        public static List<string[]> Number(string text)
        {
            var rows = new List<string[]>(); string t = (text ?? "").Trim().Replace("_", ""); ulong v; string from;
            if (!ParseNum(t, out v, out from)) { rows.Add(R(2, "Input", "type a number: 200, 0xC8, 0b11001000, C8h, an IPv4 address (10.1.2.3) or a MAC (00:11:22:aa:bb:cc)", "", "u")); return rows; }
            rows.Add(R(3, "Read as", from, "", "i"));
            rows.Add(R(0, "Decimal", v.ToString(CultureInfo.InvariantCulture), "", "i"));
            rows.Add(R(0, "Hex", "0x" + v.ToString("X", CultureInfo.InvariantCulture), "", "i"));
            string bin = Convert.ToString(unchecked((long)v), 2);   // a negative long prints its 64-bit pattern - exactly v's bits
            int width = bin.Length <= 8 ? 8 : bin.Length <= 16 ? 16 : bin.Length <= 32 ? 32 : bin.Length <= 48 ? 48 : 64; bin = bin.PadLeft(width, '0');
            rows.Add(R(0, "Binary", string.Join(" ", Enumerable.Range(0, width / 8).Select(i => bin.Substring(i * 8, 8))), width + " bits shown; " + Need(v) + " needed", "i"));
            if (v <= uint.MaxValue)
            {
                uint u = (uint)v; rows.Add(R(3, "As an IPv4 address", U(u), "", "i"));
                int ones = 0; bool contiguous = true; for (int i = 31; i >= 0; i--) { if ((u >> i & 1) == 1) { if (ones != 31 - i) contiguous = false; ones++; } }
                if (contiguous && ones > 0 && from.StartsWith("IPv4", StringComparison.Ordinal)) rows.Add(R(3, "As a mask", "/" + ones + "  ·  wildcard " + U(~u), "", "i"));
                if (v <= 65535) rows.Add(R(3, "As a port / VLAN / ID", v <= 4094 && v >= 1 ? "valid VLAN ID and port number" : "valid port number", "", "i"));
                if (v <= 63) rows.Add(R(3, "As DSCP", "DSCP " + v + "  ·  ToS byte 0x" + (v << 2).ToString("X2", CultureInfo.InvariantCulture) + Dscp((int)v), "", "i"));
            }
            if (v <= 0xFFFFFFFFFFFFUL && v > uint.MaxValue || from.StartsWith("MAC", StringComparison.Ordinal)) rows.Add(R(3, "As a MAC", string.Join(":", Enumerable.Range(0, 6).Select(i => ((v >> (40 - i * 8)) & 0xFF).ToString("x2", CultureInfo.InvariantCulture))), "", "i"));
            return rows;
        }
        static int Need(ulong v) { int n = 0; for (ulong x = v; x > 0; x >>= 1) n++; return Math.Max(1, n); }
        static string Dscp(int d) { switch (d) { case 46: return " (EF - voice)"; case 34: return " (AF41 - video)"; case 26: return " (AF31)"; case 18: return " (AF21)"; case 10: return " (AF11)"; case 48: return " (CS6 - network control)"; case 8: return " (CS1 - low priority)"; case 0: return " (best effort)"; default: return ""; } }
        static bool ParseNum(string t, out ulong v, out string from)
        {
            v = 0; from = ""; if (t == "") return false;
            var ip = V4(t); if (ip != null) { v = Be(ip.GetAddressBytes()); from = "IPv4 address"; return true; }
            if (Regex.IsMatch(t, @"^([0-9a-fA-F]{2}[:-]){5}[0-9a-fA-F]{2}$") || Regex.IsMatch(t, @"^[0-9a-fA-F]{4}\.[0-9a-fA-F]{4}\.[0-9a-fA-F]{4}$")) { v = ulong.Parse(Regex.Replace(t, "[^0-9a-fA-F]", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture); from = "MAC address"; return true; }
            string s = t.Replace(" ", "");
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) { from = "hex"; return ulong.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v); }
            if (s.EndsWith("h", StringComparison.OrdinalIgnoreCase) && s.Length > 1) { from = "hex"; return ulong.TryParse(s.Substring(0, s.Length - 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v); }
            // binary only when it says so (0b... or ...b): "100000000" is one hundred million, not 256
            if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(s, "^[01]+[bB]$")) { string bits = s.StartsWith("0b", StringComparison.OrdinalIgnoreCase) ? s.Substring(2) : s.Substring(0, s.Length - 1); if (bits.Length == 0 || bits.Length > 64 || !Regex.IsMatch(bits, "^[01]+$")) return false; from = "binary"; v = Convert.ToUInt64(bits, 2); return true; }
            if (ulong.TryParse(s.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out v)) { from = "decimal"; return true; }
            if (Regex.IsMatch(s, "^[0-9a-fA-F]+$") && s.Length <= 16) { from = "hex (it has letters)"; return ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v); }
            return false;
        }
    }
}
