// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // Routing as a PC can see it. Routers keep their protocols to themselves - but some of it shows from the edge:
    // the gateway being a virtual router (VRRP / HSRP / GLBP) and failing over, IPv6 routers announcing themselves,
    // which route Windows picks for a destination, and - only with packet capture - routing hellos leaking onto your segment.
    // AdeeboMeter never sends a routing protocol packet of its own.
    static class RoutingTools
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }

        // ---------- virtual router MAC addresses
        public static string Virtual(string mac, out string group)
        {
            group = ""; var m = (mac ?? "").Replace('-', ':').ToLowerInvariant(); if (m.Length != 17) return "";
            if (m.StartsWith("00:00:5e:00:01:", StringComparison.Ordinal)) { group = Convert.ToInt32(m.Substring(15), 16).ToString(CultureInfo.InvariantCulture); return "VRRP"; }
            if (m.StartsWith("00:00:5e:00:02:", StringComparison.Ordinal)) { group = Convert.ToInt32(m.Substring(15), 16).ToString(CultureInfo.InvariantCulture); return "VRRP (IPv6)"; }
            if (m.StartsWith("00:00:0c:07:ac:", StringComparison.Ordinal)) { group = Convert.ToInt32(m.Substring(15), 16).ToString(CultureInfo.InvariantCulture); return "HSRP"; }
            if (m.StartsWith("00:00:0c:9f:f", StringComparison.Ordinal)) { group = Convert.ToInt32(m.Substring(13).Replace(":", ""), 16).ToString(CultureInfo.InvariantCulture); return "HSRP version 2"; }
            if (m.StartsWith("00:07:b4:", StringComparison.Ordinal)) return "GLBP";
            return "";
        }

        // ---------- the gateway watch (every 30 s from the Brain): the ARP entry for the gateway changing MAC = a failover
        static string lastGw = "", lastMac = ""; public static string LastFailover = "";
        public static void GwTick(Monitor m)
        {
            string gw; lock (m.L) gw = m.Gateway; if (gw == "") return;
            string mac = Arp.MacOf(gw); if (mac == "") return;
            if (gw == lastGw && lastMac != "" && mac != lastMac)
            {
                string g, kind = Virtual(mac, out g);
                LastFailover = DateTime.Now.ToString("ddd HH:mm", CultureInfo.CurrentCulture) + " - " + lastMac + " → " + mac;
                m.Events.Add(1, "gw", "The gateway " + gw + " moved to another device (" + lastMac + " → " + mac + ")" + (kind == "" ? " - a router failover or a replaced router" : ""));
            }
            if (gw != lastGw && lastGw != "") LastFailover = "";   // another network: a takeover seen on the old one isn't this one's
            lastGw = gw; lastMac = mac;
        }

        public static List<string[]> Gateway(Monitor m)
        {
            var rows = new List<string[]>(); string gw, wait; lock (m.L) { gw = m.Gateway; wait = m.LinkWait; }
            if (gw == "") { rows.Add(R(2, "Gateway", "this PC has no default gateway right now", "", "m")); return rows; }
            PathTools.Connect(IPAddress.Parse(gw), 1, 300);   // a refused connect is enough to make sure the ARP entry is fresh
            string mac = Arp.MacOf(gw), g = "", kind = Virtual(mac, out g);
            rows.Add(R(3, "Your gateway", gw + (mac != "" ? "  ·  " + mac + " " + Toolkit.Vendor(mac) : ""), "", "m"));
            if (kind != "") rows.Add(R(0, "Redundant gateway", kind + (g != "" ? " group " + g : "") + " - a virtual address shared by two or more routers", "If one router fails the other takes over in about 1-3 s (seconds of loss, then fine). AdeeboMeter watches the MAC address and notes a takeover on the timeline.", "m"));
            else if (mac != "") rows.Add(R(3, "Redundant gateway", "no sign of VRRP / HSRP / GLBP - the gateway's own address", "Either a single router (most homes), or redundancy that keeps the real MAC address (some vendors and clustered firewalls do).", "i"));
            rows.Add(R(LastFailover != "" ? 1 : 3, "Takeovers seen", LastFailover != "" ? LastFailover : "none since AdeeboMeter started", "", "m"));
            rows.Add(R(wait != "" ? 1 : 3, "Plug-in to network", wait != "" ? wait : "nothing slow seen (it's measured when a cable is plugged in while AdeeboMeter runs)", wait != "" ? "Around 30 s of silence after plugging in is classic spanning tree on a switch port without 'port fast' / 'edge port'. The network team can turn it on for user ports." : "", "i"));
            var others = Arp.Table().Where(e => Virtual(e.Mac, out g) != "" && e.Ip != gw).ToList();
            foreach (var o in others.Take(4)) rows.Add(R(3, "Another virtual router", o.Ip + "  ·  " + Virtual(o.Mac, out g) + (g != "" ? " group " + g : ""), "", "m"));
            return rows;
        }

        // ---------- who this PC has talked to on the local network (the ARP table) - nothing is sent
        public static List<string[]> Neighbours(Monitor m)
        {
            var rows = new List<string[]>(); string gw; lock (m.L) gw = m.Gateway;
            List<Arp.Entry> t; try { t = Arp.Table(); } catch { t = new List<Arp.Entry>(); }
            var names = new Dictionary<int, string>();
            try { foreach (var n in NetworkInterface.GetAllNetworkInterfaces()) try { names[n.GetIPProperties().GetIPv4Properties().Index] = n.Name; } catch { } } catch { }
            var list = t.OrderBy(e => e.IfIndex).ThenBy(e => { IPAddress a; return IPAddress.TryParse(e.Ip, out a) ? BitConverter.ToUInt32(a.GetAddressBytes().Reverse().ToArray(), 0) : 0; }).ToList();
            rows.Add(R(3, "Neighbours", list.Count + " devices this PC has talked to recently", "Windows keeps each for a few minutes after the last packet. A ping sweep (Network devices) fills it with everyone who answers.", "w"));
            foreach (var e in list.Take(200))
            {
                string g, v = Virtual(e.Mac, out g), name; names.TryGetValue(e.IfIndex, out name);
                string what = e.Ip == gw ? "your gateway" + (v != "" ? " (" + v + (g != "" ? " group " + g : "") + ")" : "") : v != "" ? v + " virtual router" : Arp.Randomised(e.Mac) ? "private (random) address - a phone or laptop" : Toolkit.Vendor(e.Mac).Trim('(', ')');
                rows.Add(R(e.Ip == gw ? 0 : 3, e.Ip, e.Mac + (what != "" ? "  ·  " + what : "") + (e.Dynamic ? "" : "  ·  static entry") + (name != null && names.Count > 1 ? "  ·  " + name : ""), "", "w"));
            }
            return rows;
        }

        // ---------- IPv6 routers announcing themselves (router advertisements, read from Windows' own state)
        public static List<string[]> Ipv6Routers()
        {
            var rows = new List<string[]>();
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            {
                IPInterfaceProperties p; try { p = n.GetIPProperties(); } catch { continue; }
                var routers = p.GatewayAddresses.Select(x => x.Address).Where(a => a.AddressFamily == AddressFamily.InterNetworkV6 && !a.Equals(IPAddress.IPv6Any)).Select(a => a.ToString()).Distinct().ToList();
                var ra = p.UnicastAddresses.Where(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6 && u.PrefixOrigin == PrefixOrigin.RouterAdvertisement).ToList();
                if (routers.Count == 0 && ra.Count == 0) continue;
                var prefixes = ra.Select(u => Prefix(u.Address, u.PrefixLength)).Distinct().ToList();
                rows.Add(R(routers.Count > 1 ? 1 : 0, n.Name + " · routers", routers.Count == 0 ? "addresses from a router, but no default route" : routers.Count + ": " + string.Join(", ", routers.Take(4)), routers.Count > 1 ? "More than one IPv6 router on this network. Fine if the network was built that way; on a home or office LAN it's often a device or VM sending router advertisements by mistake (a 'rogue RA') - traffic may go to it." : "", "m"));
                if (prefixes.Count > 0) rows.Add(R(prefixes.Count > 2 ? 1 : 3, n.Name + " · prefixes", string.Join(", ", prefixes.Take(5)) + (prefixes.Any(x => x.StartsWith("fd", StringComparison.OrdinalIgnoreCase)) ? "  (fd… = private, local-only)" : ""), "", "m"));
            }
            if (rows.Count == 0) rows.Add(R(3, "IPv6 routers", "none - no router on your networks announces IPv6", "IPv6 is simply off here; apps use IPv4.", "m"));
            return rows;
        }
        static string Prefix(IPAddress a, int len)
        {
            if (len <= 0 || len > 128) len = 64; var b = a.GetAddressBytes();
            for (int i = 0; i < 16; i++) { int keep = Math.Max(0, Math.Min(8, len - i * 8)); b[i] &= (byte)(0xFF << (8 - keep)); }
            return new IPAddress(b) + "/" + len;
        }

        // ---------- which route does this destination take?
        [StructLayout(LayoutKind.Sequential)] struct FwdRow { public uint Dest, Mask, Policy, NextHop, IfIndex, Type, Proto, Age, NextHopAs, M1, M2, M3, M4, M5; }
        [DllImport("iphlpapi.dll")] static extern int GetBestRoute(uint dest, uint src, out FwdRow row);
        public static List<string[]> RouteFor(string target)
        {
            var rows = new List<string[]>(); var dst = Icmp.Resolve4(target);
            if (dst == null) { rows.Add(R(2, "Destination", "couldn't find an IPv4 address for " + target, "", "u")); return rows; }
            var b = dst.GetAddressBytes(); uint be = (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
            FwdRow best; int rc = GetBestRoute(BitConverter.ToUInt32(b, 0), 0, out best);
            string ifName = ""; bool vpn = false;
            if (rc == 0)
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                    try { if (n.GetIPProperties().GetIPv4Properties().Index == (int)best.IfIndex) { ifName = n.Name; vpn = Monitor.IsVpn(n); } } catch { }
            List<Routes.R> table; try { table = Routes.Read(); } catch { table = new List<Routes.R>(); }
            var match = table.Where(r => (be & r.Mask) == r.Dest).OrderByDescending(r => r.Prefix).ThenBy(r => r.Metric).ToList();
            var chosen = match.FirstOrDefault();
            rows.Add(R(rc == 0 ? 0 : 2, dst.ToString(), rc == 0 ? "leaves through " + (ifName != "" ? ifName : "interface " + best.IfIndex) + (vpn ? " (the VPN)" : "") + (best.NextHop != 0 && new IPAddress(best.NextHop).ToString() != dst.ToString() ? " via " + new IPAddress(best.NextHop) : " (directly on that network)") : "Windows has no route to it", vpn ? "This traffic goes into the VPN tunnel." : match.Any(r => r.Vpn) ? "" : "", "m"));
            if (chosen != null) rows.Add(R(3, "Because of", Routes.Ip(chosen.Dest) + "/" + chosen.Prefix + " · metric " + chosen.Metric + (chosen.Vpn ? " · VPN" : ""), "The most specific route wins; among equals, the lowest metric.", "m"));
            foreach (var r in match.Skip(1).Take(4)) rows.Add(R(3, "Also matches", Routes.Ip(r.Dest) + "/" + r.Prefix + " · metric " + r.Metric + " · " + (r.Iface != "" ? r.Iface : "if " + r.IfIndex) + (r.Vpn ? " (VPN)" : ""), "", "m"));
            if (chosen != null && chosen.Prefix == 0 && table.Count(r => r.Prefix == 0) > 1) rows.Add(R(3, "Split tunnel?", "two default routes - the lower metric one wins (" + string.Join(", ", table.Where(r => r.Prefix == 0).OrderBy(r => r.Metric).Select(r => (r.Iface != "" ? r.Iface : "if " + r.IfIndex) + " " + r.Metric).Take(3)) + ")", "", "m"));
            return rows;
        }

        // ---------- listen for routing / switching control traffic on this segment (needs Wireshark + Npcap, nothing sent, nothing saved)
        const string Filter = "ip proto 89 or ip proto 88 or ip proto 103 or ip proto 112 or ip proto 2 or udp port 520 or udp port 521 or udp port 1985 or udp port 3222 or udp port 646 or udp port 3784 or udp port 67 or udp port 546 or udp port 547 or (icmp6 and ip6[40] == 134) or ether proto 0x88cc or ether dst 01:00:0c:cc:cc:cc or ether dst 01:00:0c:cc:cc:cd or ether dst 01:80:c2:00:00:00 or ether dst 01:80:c2:00:00:14 or ether dst 01:80:c2:00:00:15 or ether dst 09:00:2b:00:00:05";
        static readonly string[] Fields = { "frame.protocols", "eth.src", "ip.src", "ipv6.src", "ospf.area_id", "vrrp.virt_rtr_id", "hsrp.group", "eigrp.as", "stp.root.hw", "stp.flags.tc", "dhcp.option.dhcp_server_id" };
        // the "who announces itself" preset: mDNS, SSDP / UPnP, LLMNR and NetBIOS - names and devices, not routing
        const string DiscFilter = "udp port 5353 or udp port 1900 or udp port 5355 or udp port 137 or udp port 138";
        static readonly string[] DiscFields = { "frame.protocols", "eth.src", "ip.src", "ipv6.src", "dns.qry.name", "nbns.name", "http.server" };
        public static List<string[]> Listen(Monitor m, string secsIn, Toolkit.Run run) { return Listen(m, secsIn, run, false); }
        public static List<string[]> Listen(Monitor m, string secsIn, Toolkit.Run run, bool discovery)
        {
            var rows = new List<string[]>(); string id; lock (m.L) id = m.IfaceId;
            if (!LiveCapture.Available) { rows.Add(R(3, "Needs packet capture", "Wireshark (tshark) and Npcap aren't installed on this PC", "Installing them usually needs admin - on a locked-down work PC this tool can't run. Everything else in Path & routing works without it.", "u")); return rows; }
            Guid g; if (!Guid.TryParse((id ?? "").Trim('{', '}'), out g)) { rows.Add(R(2, "Adapter", "no active wired or Wi-Fi adapter", "", "u")); return rows; }
            int secs; if (!int.TryParse((secsIn ?? "").Trim(), out secs)) secs = 60; secs = Math.Max(10, Math.Min(120, secs));
            run.Status = "Listening for " + secs + " s (only " + (discovery ? "discovery and name" : "routing and switch control") + " traffic, nothing saved)...";
            string filter = discovery ? DiscFilter : Filter; var fields = discovery ? DiscFields : Fields;
            string ifc = "\\Device\\NPF_{" + g.ToString().ToUpperInvariant() + "}";
            // in pieces of up to 15 s, so Stop is honoured within one piece (tshark itself can't be told to stop early)
            Func<string[], int, string> go1 = (f, n) => { try { return Job.Run(LiveCapture.Tshark, "-i \"" + ifc + "\" -n -a duration:" + n.ToString(CultureInfo.InvariantCulture) + " -f \"" + filter + "\" -T fields -E separator=| " + string.Join(" ", f.Select(x => "-e " + x)), (n + 15) * 1000) ?? ""; } catch (Exception e) { return "ERR " + e.Message; } };
            Func<string[], string> go = f =>
            {
                var sb = new StringBuilder(); int left = secs;
                while (left > 0 && !run.Stop)
                {
                    int n = Math.Min(15, left); string part = go1(f, n); left -= n;
                    if (part.StartsWith("ERR ", StringComparison.Ordinal) || part.IndexOf("aren't valid", StringComparison.OrdinalIgnoreCase) >= 0) return sb.Length == 0 ? part : sb.ToString();
                    sb.Append(part); if (!part.EndsWith("\n", StringComparison.Ordinal) && part != "") sb.Append('\n');
                    run.Status = "Listening... " + (secs - left) + " of " + secs + " s";
                }
                return sb.ToString();
            };
            string text = go(fields);
            // older Wireshark refuses some field names: only then once more with fewer (a quiet network is not a reason to listen twice)
            if (text.IndexOf("aren't valid", StringComparison.OrdinalIgnoreCase) >= 0) text = go(fields.Take(4).ToArray());
            if (text.StartsWith("ERR ", StringComparison.Ordinal)) { rows.Add(R(2, "Capture", Draw.Trunc(text.Substring(4), 90), "", "u")); return rows; }
            var seen = new Dictionary<string, List<string[]>>(); var heard = new Dictionary<string, int>();   // every packet counted; the first 200 kept
            foreach (var line in text.Split('\n'))
            {
                var f = line.TrimEnd('\r').Split('|'); if (f.Length < 4 || f[0] == "") continue;
                string k = discovery ? DiscKind(f[0]) : Kind(f[0]); if (k == "") continue;
                List<string[]> l; if (!seen.TryGetValue(k, out l)) { l = new List<string[]>(); seen[k] = l; } if (l.Count < 200) l.Add(f); int hc; heard.TryGetValue(k, out hc); heard[k] = hc + 1;
            }
            if (seen.Count == 0) rows.Add(discovery ? R(0, "Heard nothing", "no mDNS, SSDP, LLMNR or NetBIOS in " + secs + " s", "A quiet, well-run network - or one that blocks multicast between devices (common on guest Wi-Fi).", "m") : R(0, "Heard nothing", "no routing protocol, redundancy or switch control traffic in " + secs + " s", "That's how a user segment should look: routers keep their protocols on their own links.", "m"));
            foreach (var kv in seen.OrderBy(x => Order(x.Key)))
            {
                var from = kv.Value.Select(x => x[2] != "" ? x[2] : x[3] != "" ? x[3] : x[1]).Distinct().ToList();
                // DHCP: clients renewing send from their own address too - only distinct server ids (OFFER / ACK) are servers
                int sources = kv.Key == "DHCP" ? kv.Value.Where(x => x.Length > 10 && x[10].Trim() != "").Select(x => x[10].Trim()).Distinct().Count() : from.Count;
                string extra = discovery ? DiscExtra(kv.Value) : Extra(kv.Key, kv.Value); int lv; string adv = discovery ? DiscAdvice(kv.Key, out lv) : Advice(kv.Key, sources, out lv);
                rows.Add(R(lv, kv.Key, heard[kv.Key] + " packets from " + string.Join(", ", from.Take(4)) + (from.Count > 4 ? " +" + (from.Count - 4) : "") + extra, adv, "m"));
            }
            return rows;
        }
        static string Kind(string protos)
        {
            var p = ":" + protos + ":";
            if (p.Contains(":ospf:")) return "OSPF"; if (p.Contains(":eigrp:")) return "EIGRP"; if (p.Contains(":rip:")) return "RIP"; if (p.Contains(":ripng:")) return "RIPng"; if (p.Contains(":pim:")) return "PIM";
            if (p.Contains(":isis:") || p.Contains(":osi:")) return "IS-IS"; if (p.Contains(":ldp:")) return "LDP"; if (p.Contains(":bfd")) return "BFD";
            if (p.Contains(":vrrp:")) return "VRRP"; if (p.Contains(":hsrp")) return "HSRP"; if (p.Contains(":glbp:")) return "GLBP";
            if (p.Contains(":igmp:")) return "IGMP"; if (p.Contains(":icmpv6:")) return "IPv6 router advertisement"; if (p.Contains(":dhcpv6:")) return "DHCPv6"; if (p.Contains(":dhcp:") || p.Contains(":bootp:")) return "DHCP";
            if (p.Contains(":lldp:")) return "LLDP"; if (p.Contains(":cdp:")) return "CDP"; if (p.Contains(":stp:")) return "Spanning tree"; if (p.Contains(":dtp:")) return "DTP"; if (p.Contains(":vtp:")) return "VTP";
            return "";
        }
        static string DiscKind(string protos)
        {
            var p = ":" + protos + ":";
            if (p.Contains(":mdns:")) return "mDNS (Bonjour)"; if (p.Contains(":ssdp:")) return "SSDP / UPnP"; if (p.Contains(":llmnr:")) return "LLMNR"; if (p.Contains(":nbns:")) return "NetBIOS names"; if (p.Contains(":nbdgm:") || p.Contains(":browser:")) return "NetBIOS browser";
            return "";
        }
        static string DiscExtra(List<string[]> l)
        {
            var names = l.SelectMany(x => x.Skip(4).Take(3)).SelectMany(x => x.Split(',')).Select(x => x.Trim()).Where(x => x != "").Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();
            return names.Count > 0 ? " · " + string.Join(", ", names) : "";
        }
        static string DiscAdvice(string k, out int lv)
        {
            lv = 3;
            switch (k)
            {
                case "mDNS (Bonjour)": return "Printers, Chromecasts, Apple devices and smart-home gear announcing themselves and their services. Normal at home.";
                case "SSDP / UPnP": return "Devices offering themselves to the network (TVs, routers, media servers). A router that answers UPnP may let devices open ports from inside.";
                case "LLMNR": case "NetBIOS names": lv = 1; return "Old-style name lookups when DNS has no answer. Any device on the segment can answer them falsely and collect sign-in hashes - companies usually turn them off by Group Policy.";
                case "NetBIOS browser": return "Old Windows network browsing - harmless but a sign of very old settings or devices.";
                default: return "";
            }
        }
        static int Order(string k) { var o = new[] { "OSPF", "EIGRP", "RIP", "RIPng", "IS-IS", "PIM", "LDP", "BFD", "DTP", "VTP", "VRRP", "HSRP", "GLBP", "IPv6 router advertisement", "DHCP", "Spanning tree", "LLDP", "CDP", "IGMP" }; int i = Array.IndexOf(o, k); return i < 0 ? 99 : i; }
        static string Extra(string k, List<string[]> l)
        {
            Func<int, string> vals = i => string.Join(", ", l.Where(x => x.Length > i && x[i] != "").Select(x => x[i]).Distinct().Take(3));
            switch (k)
            {
                case "OSPF": { var v = vals(4); return v != "" ? " · area " + v : ""; }
                case "VRRP": { var v = vals(5); return v != "" ? " · group " + v : ""; }
                case "HSRP": { var v = vals(6); return v != "" ? " · group " + v : ""; }
                case "EIGRP": { var v = vals(7); return v != "" ? " · AS " + v : ""; }
                case "Spanning tree": { var v = vals(8); int tc = l.Count(x => x.Length > 9 && (x[9] == "1" || x[9] == "True")); return (v != "" ? " · root bridge " + v : "") + (tc > 0 ? " · " + tc + " topology changes" : ""); }
                case "DHCP": { var v = vals(10); return v != "" ? " · servers " + v : ""; }
                default: return "";
            }
        }
        static string Advice(string k, int sources, out int lv)
        {
            lv = 3;
            switch (k)
            {
                case "OSPF": case "EIGRP": case "RIP": case "RIPng": case "IS-IS": case "LDP":
                    lv = 1; return "A router speaks " + k + " on the segment your PC is on. On user networks that interface should be passive - otherwise any device here could form a neighbourship or inject routes. Worth telling the network team.";
                case "PIM": lv = 3; return "Multicast routing is on here - normal where there is IPTV, video or market-data multicast.";
                case "BFD": lv = 3; return "Fast failure detection between routers on this segment.";
                case "DTP": case "VTP": lv = 1; return "The switch port offers trunking negotiation to your PC. User ports are normally fixed access ports with this off.";
                case "VRRP": case "HSRP": case "GLBP": return "Redundant gateway routers keeping each other alive - this is normal and good.";
                case "IPv6 router advertisement": lv = sources > 1 ? 1 : 3; return sources > 1 ? "More than one device announces itself as the IPv6 router - one may be rogue." : "The IPv6 router announcing the network.";
                case "DHCP": lv = sources > 1 ? 1 : 3; return sources > 1 ? "More than one DHCP server answers on this network - a second router or a misconfigured device can hand out wrong addresses." : "";
                case "Spanning tree": return "The switch protects against loops. Topology changes make switches flush their tables - a few are normal, many mean a port somewhere is flapping.";
                case "LLDP": case "CDP": return "The switch announces itself - Monitor > Link shows which switch and port you're plugged into.";
                case "IGMP": return "A multicast querier (usually the router or switch) checks who wants multicast.";
                default: return "";
            }
        }
    }
}
