// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace AdeeboMeter
{
    // ================================================================== Replays from a capture file (network engineer pack)
    // DHCP lease replay, IPv6 Router Advertisement replay and the ICMP error translator - all read a .pcap / .pcapng you
    // already have (Wireshark, tcpdump, a switch's mirror port) with AdeeboMeter's own reader; nothing is sent or captured.
    // The sample lab writes small example captures - a good lease and the classic failures - so you can learn the patterns.
    static class Replay
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string T(double t) { return t.ToString("0.000", CultureInfo.InvariantCulture) + " s"; }
        static List<Pcap.Pkt> Load(string path, List<string[]> rows)
        {
            string p = (path ?? "").Trim().Trim('"');
            if (p == "") { rows.Add(R(2, "Capture file", "type or paste the path of a .pcap or .pcapng file - or try the Sample lab", "", "u")); return null; }
            string err; var l = Pcap.Read(p, out err);
            if (err != "") { rows.Add(R(2, "Capture file", err, "", "u")); return null; }
            rows.Add(R(3, "Read", l.Count.ToString("N0", CultureInfo.CurrentCulture) + " packets from " + Path.GetFileName(p), "Read on this PC with AdeeboMeter's own reader - the file isn't changed or sent anywhere.", "m"));
            return l;
        }

        // ---------- DHCP: Discover > Offer > Request > ACK, per transaction
        public sealed class Dhcp
        {
            public double T; public int Type; public uint Xid; public string Client = "", Server = "", Your = "", Relay = "", Host = "", Requested = "";
            public bool Opt82; public Dictionary<int, string> Opts = new Dictionary<int, string>();
        }
        static readonly string[] DhcpNames = { "", "Discover", "Offer", "Request", "Decline", "ACK", "NAK", "Release", "Inform" };
        public static Dhcp ParseDhcp(byte[] b, double t)
        {
            if (b == null || b.Length < 240 || b[236] != 99 || b[237] != 130 || b[238] != 83 || b[239] != 99) return null;
            var d = new Dhcp { T = t, Xid = (uint)(b[4] << 24 | b[5] << 16 | b[6] << 8 | b[7]) };
            d.Your = new IPAddress(new[] { b[16], b[17], b[18], b[19] }).ToString(); var gi = new IPAddress(new[] { b[24], b[25], b[26], b[27] }); if (!gi.Equals(IPAddress.Any)) d.Relay = gi.ToString();
            d.Client = string.Join(":", Enumerable.Range(28, 6).Select(i => b[i].ToString("x2", CultureInfo.InvariantCulture)));
            for (int p = 240; p < b.Length;)
            {
                int code = b[p]; if (code == 255) break; if (code == 0) { p++; continue; }
                if (p + 2 > b.Length) break; int len = b[p + 1]; if (p + 2 + len > b.Length) break;
                var v = new byte[len]; Array.Copy(b, p + 2, v, 0, len);
                switch (code)
                {
                    case 53: if (len >= 1) d.Type = v[0]; break;
                    case 54: if (len == 4) d.Server = new IPAddress(v).ToString(); break;
                    case 50: if (len == 4) d.Requested = new IPAddress(v).ToString(); break;
                    case 12: d.Host = Clean(Encoding.ASCII.GetString(v)); break;
                    case 82: d.Opt82 = true; d.Opts[82] = "relay agent info (" + len + " bytes)"; break;
                    case 1: case 3: case 6: case 28: case 42: if (len % 4 == 0 && len > 0) d.Opts[code] = string.Join(", ", Enumerable.Range(0, len / 4).Select(i => new IPAddress(new[] { v[i * 4], v[i * 4 + 1], v[i * 4 + 2], v[i * 4 + 3] }).ToString())); break;
                    case 51: case 58: case 59: if (len == 4) d.Opts[code] = Dur((uint)(v[0] << 24 | v[1] << 16 | v[2] << 8 | v[3])); break;
                    case 15: case 66: case 67: d.Opts[code] = Clean(Encoding.ASCII.GetString(v)); break;
                    case 26: if (len == 2) d.Opts[code] = (v[0] << 8 | v[1]).ToString(CultureInfo.InvariantCulture); break;
                }
                p += 2 + len;
            }
            return d.Type >= 1 && d.Type <= 8 ? d : null;
        }
        static string Clean(string s) { s = new string((s ?? "").Where(c => c >= 32 && c < 127).ToArray()); return s.Length > 60 ? s.Substring(0, 60) : s; }
        static string Dur(uint s) { return s == 0xFFFFFFFF ? "forever" : s >= 86400 && s % 3600 == 0 ? s / 3600 + " h" : s >= 3600 ? (s / 3600.0).ToString("0.#", CultureInfo.InvariantCulture) + " h" : s + " s"; }
        static readonly Dictionary<int, string> OptName = new Dictionary<int, string> { { 1, "mask" }, { 3, "router" }, { 6, "DNS" }, { 15, "domain" }, { 26, "MTU" }, { 28, "broadcast" }, { 42, "NTP" }, { 51, "lease" }, { 58, "renew" }, { 59, "rebind" }, { 66, "TFTP server" }, { 67, "boot file" }, { 82, "option 82" } };
        static string Opt(Dhcp d, int k) { string v; return d.Opts.TryGetValue(k, out v) ? v : "(none)"; }
        static string Opts(Dhcp d) { return string.Join(" · ", d.Opts.Where(kv => kv.Key != 58 && kv.Key != 59).OrderBy(kv => kv.Key).Select(kv => OptName[kv.Key] + " " + kv.Value)); }

        public static List<string[]> DhcpReplay(string path) { var rows = new List<string[]>(); var l = Load(path, rows); return l == null ? rows : DhcpRows(l, rows); }
        public static List<string[]> DhcpRows(List<Pcap.Pkt> pkts, List<string[]> rows)
        {
            var msgs = pkts.Where(k => k.Proto == 17 && (k.SrcPort == 67 || k.SrcPort == 68) && (k.DstPort == 67 || k.DstPort == 68)).Select(k => ParseDhcp(k.Payload, k.T)).Where(d => d != null).ToList();
            if (msgs.Count == 0) { rows.Add(R(1, "DHCP", "no DHCP in this capture", "Capture while the PC connects: Wireshark filter  udp port 67 or udp port 68 , then unplug / plug the cable or reconnect the Wi-Fi.", "m")); return rows; }
            var findings = new List<string[]>();
            foreach (var g in msgs.GroupBy(d => d.Client))
            {
                rows.Add(R(-1, "CLIENT " + g.Key + (g.Any(d => d.Host != "") ? " · " + g.First(d => d.Host != "").Host : ""), "", "", "m"));
                foreach (var d in g.OrderBy(d => d.T).Take(60))
                {
                    string what = DhcpNames[d.Type] + (d.Type == 2 || d.Type == 5 ? " " + d.Your + " from " + (d.Server != "" ? d.Server : "?") : d.Type == 3 && d.Requested != "" ? " for " + d.Requested + (d.Server != "" ? " (accepting " + d.Server + "'s offer)" : "") : d.Type == 6 ? " from " + d.Server : "");
                    rows.Add(R(d.Type == 6 || d.Type == 4 ? 2 : 3, T(d.T), what, (d.Type == 2 || d.Type == 5 ? Opts(d) : "") + (d.Relay != "" ? (d.Type == 2 || d.Type == 5 ? " · " : "") + "relayed by " + d.Relay : ""), "m"));
                }
                // what went wrong (or right)
                var offers = g.Where(d => d.Type == 2).ToList(); var servers = offers.Select(d => d.Server).Where(s => s != "").Distinct().ToList();
                if (servers.Count > 1) findings.Add(R(2, "Two DHCP servers answered " + g.Key, string.Join(" and ", servers) + " both offered addresses", "One of them shouldn't be there: a second router, a hotspot or a VM host handing out addresses (a rogue DHCP server). The client takes whichever answers first - which is why it works sometimes and not others. DHCP snooping on the switch stops it.", "m"));
                if (g.Any(d => d.Type == 1) && offers.Count == 0) findings.Add(R(2, "No offer for " + g.Key, g.Count(d => d.Type == 1) + " Discover(s), no answer", "No DHCP server heard it: the VLAN has no DHCP server, the relay (ip helper-address) is missing or wrong, the server's pool is full, or 802.1X hasn't let the port through yet.", "m"));
                if (g.Any(d => d.Type == 6)) findings.Add(R(2, "Refused (NAK) " + g.Key, "the server said no to " + (g.LastOrDefault(d => d.Type == 3 && d.Requested != "") != null ? g.Last(d => d.Type == 3 && d.Requested != "").Requested : "the address asked for"), "Usually the PC asked for its old address on a different network (after moving, or a VLAN change) - it starts over with Discover, which is normal. Repeated NAKs mean two servers disagree about the same network.", "m"));
                if (offers.Count > 0 && !g.Any(d => d.Type == 5) && !g.Any(d => d.Type == 6)) findings.Add(R(1, "No ACK for " + g.Key, "offered, but never confirmed", "The Request didn't reach the server, or its answer didn't come back - a relay that forwards one way only, or a firewall.", "m"));
                var acks = g.Where(d => d.Type == 5).ToList();
                if (acks.Count >= 2)
                {
                    var a = acks[acks.Count - 2]; var b = acks[acks.Count - 1];
                    var changed = a.Opts.Keys.Union(b.Opts.Keys).Where(k => k != 51 && k != 58 && k != 59 && (!a.Opts.ContainsKey(k) || !b.Opts.ContainsKey(k) || a.Opts[k] != b.Opts[k])).ToList();
                    if (a.Your != b.Your) changed.Insert(0, -1);
                    if (changed.Count > 0) findings.Add(R(1, "What changed between leases", string.Join(" · ", changed.Select(k => k == -1 ? "address " + a.Your + " > " + b.Your : OptName[k] + " " + Opt(a, k) + " > " + Opt(b, k))), "Compare a good and a bad lease: a different router or DNS server is the usual reason one works and the other doesn't.", "m"));
                }
                if (g.Any(d => d.Opt82)) findings.Add(R(3, "Option 82", "the relay added its own information (which switch port the client is on)", "Normal on enterprise networks - the server can use it to pick the pool.", "m"));
                if (acks.Count > 0 && servers.Count <= 1 && !g.Any(d => d.Type == 6)) { var last = acks.Last(); findings.Add(R(0, "Lease for " + g.Key, last.Your + " from " + last.Server + " in " + T(last.T - g.Min(d => d.T)), "", "m")); }
            }
            rows.Insert(1, R(-1, "WHAT IT SHOWS", "", "", "m"));
            rows.InsertRange(2, findings);
            return rows;
        }

        // ---------- IPv6 Router Advertisements
        public static List<string[]> RaReplay(string path) { var rows = new List<string[]>(); var l = Load(path, rows); return l == null ? rows : RaRows(l, rows); }
        public static List<string[]> RaRows(List<Pcap.Pkt> pkts, List<string[]> rows)
        {
            var ras = pkts.Where(k => k.V6 && k.Proto == 58 && k.Payload.Length >= 16 && k.Payload[0] == 134).ToList();
            if (ras.Count == 0) { rows.Add(R(1, "Router Advertisements", "none in this capture", "Capture for a minute with filter  icmpv6.type == 134  (routers send one every few minutes, or when a PC asks with type 133).", "m")); return rows; }
            var routers = ras.GroupBy(k => k.Src.ToString()).ToList();
            rows.Add(R(-1, "WHAT IT SHOWS", "", "", "m"));
            if (routers.Count > 1) rows.Add(R(2, "More than one router", routers.Count + " routers announce themselves: " + string.Join(", ", routers.Select(r => r.Key)), "If you have one router, the others are rogue - a phone hotspot, a VM host or a misconfigured device; PCs may send IPv6 traffic to it. RA Guard on the switch stops them.", "m"));
            foreach (var r in routers)
            {
                var k = r.Last(); var p = k.Payload; int life = p[6] << 8 | p[7]; bool mFlag = (p[5] & 0x80) != 0, oFlag = (p[5] & 0x40) != 0;
                var prefixes = new List<string>(); string mtu = "", dns = "";
                for (int o = 16; o + 8 <= p.Length;)
                {
                    int type = p[o], len = p[o + 1] * 8; if (len == 0 || o + len > p.Length) break;
                    if (type == 3 && len >= 32) { var pre = new byte[16]; Array.Copy(p, o + 16, pre, 0, 16); uint valid = (uint)(p[o + 4] << 24 | p[o + 5] << 16 | p[o + 6] << 8 | p[o + 7]); prefixes.Add(new IPAddress(pre) + "/" + p[o + 2] + ((p[o + 3] & 0x40) != 0 ? " (addresses made from it)" : "") + (valid == 0 ? " - WITHDRAWN" : "")); }
                    else if (type == 5 && len >= 8) mtu = ((uint)(p[o + 4] << 24 | p[o + 5] << 16 | p[o + 6] << 8 | p[o + 7])).ToString(CultureInfo.InvariantCulture);
                    else if (type == 25 && len >= 24) dns = string.Join(", ", Enumerable.Range(0, (len - 8) / 16).Select(i => { var a = new byte[16]; Array.Copy(p, o + 8 + i * 16, a, 0, 16); return new IPAddress(a).ToString(); }));
                    o += len;
                }
                rows.Add(R(life == 0 ? 1 : 3, "Router " + r.Key, r.Count() + " RA(s) · default router for " + (life == 0 ? "0 s - it says it is NOT a default router" : life + " s") + (prefixes.Count > 0 ? " · prefix " + string.Join(", ", prefixes) : " · no prefix"),
                    (mFlag ? "M flag: get addresses from DHCPv6. " : "") + (oFlag ? "O flag: other settings from DHCPv6. " : "") + (mtu != "" ? "MTU " + mtu + ". " : "") + (dns != "" ? "DNS " + dns + "." : "") + (life == 0 ? " A lifetime of 0 removes this router as the way out - fine for a second router, a problem if it's the only one." : ""), "m"));
            }
            return rows;
        }

        // ---------- ICMP errors in plain words
        static readonly Dictionary<string, string> Icmp4Text = new Dictionary<string, string> {
            { "3/0", "Network unreachable - a router has no route to that network." }, { "3/1", "Host unreachable - the last router can't reach that address (it's off, or ARP got no answer)." },
            { "3/2", "Protocol unreachable - the host doesn't speak that protocol." }, { "3/3", "Port unreachable - nothing listens on that UDP port (the host is up)." },
            { "3/4", "Fragmentation needed - the packet is too big for a link on the way and don't-fragment is set. The MTU is in the message. If these are blocked, big transfers stall (a PMTU black hole)." },
            { "3/9", "Network administratively prohibited." }, { "3/10", "Host administratively prohibited." }, { "3/13", "Administratively prohibited - a firewall or ACL says no." },
            { "5/0", "Redirect - a router says 'use that other router on your network for this destination'." }, { "5/1", "Redirect for this host - use another router." },
            { "11/0", "TTL exceeded in transit - the packet ran out of hops: traceroute does this on purpose; otherwise it's a routing loop." }, { "11/1", "Fragment reassembly time exceeded - some fragments never arrived." },
            { "12/0", "Parameter problem - something in the IP header was wrong." } };
        static readonly Dictionary<string, string> Icmp6Text = new Dictionary<string, string> {
            { "1/0", "No route to destination." }, { "1/1", "Administratively prohibited - a firewall or ACL." }, { "1/3", "Address unreachable - the last router couldn't find that host (Neighbour Discovery got no answer)." },
            { "1/4", "Port unreachable - nothing listens on that UDP port." }, { "1/5", "Source address failed policy." }, { "1/6", "Reject route to destination." },
            { "2/0", "Packet too big - IPv6 routers never fragment; the sender must use the MTU in this message. If these are blocked, IPv6 sites stall." },
            { "3/0", "Hop limit exceeded - traceroute, or a routing loop." }, { "3/1", "Fragment reassembly time exceeded." }, { "4/0", "Parameter problem - erroneous header field." }, { "4/1", "Parameter problem - unknown next header." } };
        public static string Explain(bool v6, int type, int code)
        {
            string s; var t = v6 ? Icmp6Text : Icmp4Text;
            if (t.TryGetValue(type + "/" + code, out s)) return s;
            if (!v6 && type == 3) return "Destination unreachable (code " + code + ").";
            return (v6 ? "ICMPv6" : "ICMP") + " type " + type + " code " + code + " - not an error message this tool explains.";
        }
        // input: a capture file, or a type/code like 3/4 (add v6 for ICMPv6: v6 2/0)
        public static List<string[]> IcmpExplain(string input)
        {
            var rows = new List<string[]>(); string s = (input ?? "").Trim(); bool v6 = s.StartsWith("v6", StringComparison.OrdinalIgnoreCase); if (v6) s = s.Substring(2).Trim();
            var m = System.Text.RegularExpressions.Regex.Match(s, @"^(\d{1,3})\s*[/ ]\s*(\d{1,3})$");
            if (m.Success) { int ty = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), co = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); rows.Add(R(3, (v6 ? "ICMPv6 " : "ICMP ") + ty + "/" + co, Explain(v6, ty, co), "", "u")); return rows; }
            var l = Load(input, rows); if (l == null) return rows;
            var errs = l.Where(k => (k.Proto == 1 && k.Payload.Length >= 8 && (k.Payload[0] == 3 || k.Payload[0] == 5 || k.Payload[0] == 11 || k.Payload[0] == 12)) || (k.Proto == 58 && k.Payload.Length >= 8 && k.Payload[0] >= 1 && k.Payload[0] <= 4)).ToList();
            if (errs.Count == 0) { rows.Add(R(0, "ICMP errors", "none in this capture", "", "m")); return rows; }
            rows.Add(R(-1, "WHAT IT SHOWS", errs.Count + " error messages", "", "m"));
            foreach (var g in errs.GroupBy(k => (k.V6 ? "v6 " : "") + k.Payload[0] + "/" + k.Payload[1] + " from " + k.Src).OrderByDescending(g => g.Count()).Take(30))
            {
                var k = g.First(); int ty = k.Payload[0], co = k.Payload[1];
                string inner = Inner(k), extra = "";
                if (!k.V6 && ty == 3 && co == 4) extra = " · MTU " + (k.Payload[6] << 8 | k.Payload[7]);
                if (k.V6 && ty == 2) extra = " · MTU " + ((uint)(k.Payload[4] << 24 | k.Payload[5] << 16 | k.Payload[6] << 8 | k.Payload[7]));
                rows.Add(R(ty == 11 || (k.V6 && ty == 3) ? 3 : 1, (k.V6 ? "ICMPv6 " : "ICMP ") + ty + "/" + co + " from " + k.Src + " × " + g.Count(), Explain(k.V6, ty, co) + extra, inner != "" ? "About: " + inner : "", "m"));
            }
            return rows;
        }
        // the packet that caused the error, as the router quoted it back
        static string Inner(Pcap.Pkt k)
        {
            var p = k.Payload; int o = 8;
            try
            {
                if (!k.V6 && p.Length >= o + 20 && (p[o] >> 4) == 4)
                {
                    int ihl = (p[o] & 15) * 4, proto = p[o + 9]; var dst = new IPAddress(new[] { p[o + 16], p[o + 17], p[o + 18], p[o + 19] });
                    string port = (proto == 6 || proto == 17) && p.Length >= o + ihl + 4 ? ":" + (p[o + ihl + 2] << 8 | p[o + ihl + 3]) : "";
                    return (proto == 6 ? "TCP" : proto == 17 ? "UDP" : proto == 1 ? "ICMP" : "protocol " + proto) + " to " + dst + port;
                }
                if (k.V6 && p.Length >= o + 40 && (p[o] >> 4) == 6)
                {
                    int nh = p[o + 6]; var d = new byte[16]; Array.Copy(p, o + 24, d, 0, 16);
                    string port = (nh == 6 || nh == 17) && p.Length >= o + 44 ? ":" + (p[o + 42] << 8 | p[o + 43]) : "";
                    return (nh == 6 ? "TCP" : nh == 17 ? "UDP" : nh == 58 ? "ICMPv6" : "next header " + nh) + " to " + new IPAddress(d) + port;
                }
            }
            catch (ArgumentException) { }
            return "";
        }

        // ---------- the sample lab: small captures written by AdeeboMeter, good and broken
        public static readonly string[][] Samples = {
            new[] { "dhcp-good", "A normal lease: Discover, Offer, Request, ACK", "dhcp" },
            new[] { "dhcp-two-servers", "Two DHCP servers answer (one is rogue)", "dhcp" },
            new[] { "dhcp-no-answer", "Discover after Discover - nobody answers", "dhcp" },
            new[] { "dhcp-nak", "The server refuses the old address (NAK), then a fresh lease", "dhcp" },
            new[] { "ra-rogue", "Two IPv6 routers, one of them a stranger", "ra" },
            new[] { "icmp-errors", "Fragmentation needed, port unreachable, TTL exceeded", "icmp" } };
        public static List<string[]> Lab(string which)
        {
            var rows = new List<string[]>(); string w = (which ?? "").Trim().ToLowerInvariant();
            var s = Samples.FirstOrDefault(x => x[0] == w);
            if (s == null)
            {
                rows.Add(R(3, "Samples", "type a name to open it here and save it for Wireshark", "", "u"));
                foreach (var x in Samples) rows.Add(new[] { "3", x[0], x[1], "", "u", "copy:" + x[0] });
                return rows;
            }
            var bytes = Build(s[0]); string dir = Path.Combine(Program.DataDir, "samples"), file = Path.Combine(dir, s[0] + ".pcap");
            try { Directory.CreateDirectory(dir); File.WriteAllBytes(file, bytes); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; file = null; }
            rows.Add(R(3, "Sample", s[1], "Made by AdeeboMeter - addresses are documentation examples, not real devices.", "u"));
            if (file != null) rows.Add(new[] { "3", "Saved", Path.GetFileName(file) + " - open it in Wireshark to see the same packets", "", "m", "show:" + file });
            var pk = Pcap.Parse(bytes); rows.Add(R(3, "Read", pk.Count + " packets", "", "m"));
            return s[2] == "dhcp" ? DhcpRows(pk, rows) : s[2] == "ra" ? RaRows(pk, rows) : IcmpRowsFromPackets(pk, rows);
        }
        static List<string[]> IcmpRowsFromPackets(List<Pcap.Pkt> pk, List<string[]> rows)
        {
            foreach (var k in pk.Where(k => k.Proto == 1 || k.Proto == 58))
                rows.Add(R(1, (k.V6 ? "ICMPv6 " : "ICMP ") + k.Payload[0] + "/" + k.Payload[1] + " from " + k.Src, Explain(k.V6, k.Payload[0], k.Payload[1]) + (!k.V6 && k.Payload[0] == 3 && k.Payload[1] == 4 ? " · MTU " + (k.Payload[6] << 8 | k.Payload[7]) : ""), Inner(k) != "" ? "About: " + Inner(k) : "", "m"));
            return rows;
        }
        // DHCP message builder for the samples
        static byte[] DhcpMsg(int op, uint xid, string mac, string your, string relay, int type, string server, string requested, string host, bool full)
        {
            var b = new byte[240]; b[0] = (byte)op; b[1] = 1; b[2] = 6; b[4] = (byte)(xid >> 24); b[5] = (byte)(xid >> 16); b[6] = (byte)(xid >> 8); b[7] = (byte)xid;
            Array.Copy(IPAddress.Parse(your).GetAddressBytes(), 0, b, 16, 4); Array.Copy(IPAddress.Parse(relay).GetAddressBytes(), 0, b, 24, 4);
            Array.Copy(Pcap.Mac(mac), 0, b, 28, 6); b[236] = 99; b[237] = 130; b[238] = 83; b[239] = 99;
            var o = new List<byte>(b) { 53, 1, (byte)type };
            Action<int, string> ip = (c, v) => { o.Add((byte)c); o.Add(4); o.AddRange(IPAddress.Parse(v).GetAddressBytes()); };
            if (server != "") ip(54, server); if (requested != "") ip(50, requested);
            if (host != "") { o.Add(12); o.Add((byte)host.Length); o.AddRange(Encoding.ASCII.GetBytes(host)); }
            if (full) { ip(1, "255.255.255.0"); ip(3, server); ip(6, server); o.AddRange(new byte[] { 51, 4, 0, 1, 0x51, 0x80 }); }
            o.Add(255); return o.ToArray();
        }
        static byte[] DhcpFrame(bool fromClient, string srcMac, string srcIp, string dstIp, byte[] msg)
        {
            return Pcap.Eth("ff:ff:ff:ff:ff:ff", srcMac, 0x0800, Pcap.Ip4(srcIp, dstIp, 17, 64, Pcap.Udp(fromClient ? 68 : 67, fromClient ? 67 : 68, msg)));
        }
        public static byte[] Build(string name)
        {
            var w = new Pcap.Writer(); string pc = "02:00:5e:10:00:01", rt = "02:00:5e:20:00:01", rogue = "02:00:5e:30:00:01"; uint x = 0x5a17c0de;
            Func<int, string, string, bool, byte[]> cli = (type, req, srv, f) => DhcpFrame(true, pc, "0.0.0.0", "255.255.255.255", DhcpMsg(1, x, pc, "0.0.0.0", "0.0.0.0", type, srv, req, "LAPTOP-ADEEB", false));
            Func<int, string, string, string, byte[]> srvMsg = (type, your, srv, mac) => DhcpFrame(false, mac, srv, "255.255.255.255", DhcpMsg(2, x, pc, your, "0.0.0.0", type, srv, "", "", type != 6));
            switch (name)
            {
                case "dhcp-good":
                    w.Add(0, cli(1, "", "", false)); w.Add(0.004, srvMsg(2, "192.0.2.50", "192.0.2.1", rt)); w.Add(0.002, cli(3, "192.0.2.50", "192.0.2.1", false)); w.Add(0.003, srvMsg(5, "192.0.2.50", "192.0.2.1", rt)); break;
                case "dhcp-two-servers":
                    w.Add(0, cli(1, "", "", false)); w.Add(0.003, srvMsg(2, "192.168.137.20", "192.168.137.1", rogue)); w.Add(0.004, srvMsg(2, "192.0.2.50", "192.0.2.1", rt));
                    w.Add(0.002, cli(3, "192.168.137.20", "192.168.137.1", false)); w.Add(0.003, srvMsg(5, "192.168.137.20", "192.168.137.1", rogue)); break;
                case "dhcp-no-answer":
                    w.Add(0, cli(1, "", "", false)); w.Add(4, cli(1, "", "", false)); w.Add(8, cli(1, "", "", false)); w.Add(16, cli(1, "", "", false)); break;
                case "dhcp-nak":
                    w.Add(0, cli(3, "10.20.30.40", "", false)); w.Add(0.003, srvMsg(6, "0.0.0.0", "192.0.2.1", rt));
                    w.Add(0.5, cli(1, "", "", false)); w.Add(0.004, srvMsg(2, "192.0.2.51", "192.0.2.1", rt)); w.Add(0.002, cli(3, "192.0.2.51", "192.0.2.1", false)); w.Add(0.003, srvMsg(5, "192.0.2.51", "192.0.2.1", rt)); break;
                case "ra-rogue":
                    w.Add(0, Pcap.Eth("33:33:00:00:00:01", rt, 0x86dd, Pcap.Ip6Icmp("fe80::1", "ff02::1", 255, 134, 0, Ra(1800, false, "2001:db8:1::", "2001:db8:1::53"))));
                    w.Add(3, Pcap.Eth("33:33:00:00:00:01", rogue, 0x86dd, Pcap.Ip6Icmp("fe80::bad:1", "ff02::1", 255, 134, 0, Ra(9000, false, "fd00:bad::", ""))));
                    w.Add(200, Pcap.Eth("33:33:00:00:00:01", rt, 0x86dd, Pcap.Ip6Icmp("fe80::1", "ff02::1", 255, 134, 0, Ra(1800, false, "2001:db8:1::", "2001:db8:1::53")))); break;
                default:   // icmp-errors
                    var orig = Pcap.Ip4("192.0.2.50", "198.51.100.10", 6, 64, new byte[] { 0xc3, 0x50, 0x01, 0xbb, 0, 0, 0, 1 });
                    w.Add(0, Pcap.Eth(pc, rt, 0x0800, Pcap.Ip4("192.0.2.1", "192.0.2.50", 1, 255, Pcap.Icmp4(3, 4, new byte[] { 0, 0, 0x05, 0x78 }.Concat(orig.Take(28)).ToArray()))));
                    var udp = Pcap.Ip4("192.0.2.50", "198.51.100.53", 17, 64, Pcap.Udp(50000, 5353, new byte[4]));
                    w.Add(1, Pcap.Eth(pc, rt, 0x0800, Pcap.Ip4("198.51.100.53", "192.0.2.50", 1, 60, Pcap.Icmp4(3, 3, new byte[4].Concat(udp.Take(28)).ToArray()))));
                    var tr = Pcap.Ip4("192.0.2.50", "203.0.113.9", 17, 1, Pcap.Udp(50001, 33434, new byte[4]));
                    w.Add(1, Pcap.Eth(pc, rt, 0x0800, Pcap.Ip4("192.0.2.1", "192.0.2.50", 1, 255, Pcap.Icmp4(11, 0, new byte[4].Concat(tr.Take(28)).ToArray())))); break;
            }
            return w.Bytes();
        }
        static byte[] Ra(int life, bool m, string prefix, string dns)
        {
            var r = new List<byte> { 64, (byte)(m ? 0x80 : 0), (byte)(life >> 8), (byte)life, 0, 0, 0, 0, 0, 0, 0, 0 };
            r.AddRange(new byte[] { 3, 4, 64, 0xc0, 0, 0x27, 0x8d, 0, 0, 0x09, 0x3a, 0x80, 0, 0, 0, 0 }); r.AddRange(IPAddress.Parse(prefix).GetAddressBytes());
            r.AddRange(new byte[] { 5, 1, 0, 0, 0, 0, 0x05, 0xdc });
            if (dns != "") { r.AddRange(new byte[] { 25, 3, 0, 0, 0, 0, 0x0e, 0x10 }); r.AddRange(IPAddress.Parse(dns).GetAddressBytes()); }
            return r.ToArray();
        }
    }
}
