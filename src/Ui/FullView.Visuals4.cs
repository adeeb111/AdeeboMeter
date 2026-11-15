// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // ================================================================== tool pictures, wave 4 (Adeeb, 15 Nov): every protocol, talking
    // The same packet-by-packet exchange as wave 3 (Exchange in Visuals3.cs) for the protocol, lab, SNMP, Windows sign-in and
    // IPv6 tools: who sends what, to which port, and what comes back - or why nothing does (UDP syslog, traps, NetFlow never
    // answer). The target comes from the boxes; the ms from the result; a red result turns the step that failed red.
    sealed partial class FullView
    {
        static readonly HashSet<string> Pictured4 = new HashSet<string> {
            "mail", "smtppath", "ssh", "stun", "teams", "ldapcheck", "rdpcheck", "syslog", "trap", "netflow", "dhcpopt", "icmpb", "udpsend", "tcpsend", "httpreq",
            "twamp", "dscp", "wol", "proxy", "vpnleak", "ping6", "v6routers", "rareplay", "discover", "nbname", "clock", "timesrc", "hairpin", "dot1x", "auth", "eapreplay",
            "dhcpreplay", "udpmtu", "iperf", "https", "certlive", "radiuslab",
            "snmp", "snmpif", "snmptraffic", "snmpcpu", "snmplldp", "snmpsens", "snmpsnap", "snmpmac", "snmpvlan", "snmppoe", "snmplag", "snmpprn", "snmpups", "snmpwalk" };
        static bool InWave3(string id) { return Pictured3.Contains(id) || Pictured4.Contains(id); }
        static bool Animates3(string id) { return Animated3.Contains(id) || Pictured4.Contains(id); }

        // the target from the boxes (first one that looks like a name or address), the first ms in the result, and whether it went red
        string Tg(string def) { foreach (var a in VisIn()) { var t = Regex.Replace(a ?? "", @"^[a-z]+://", "", RegexOptions.IgnoreCase).Split('/', ' ')[0]; if (Regex.IsMatch(t, @"^[\w.\-\[\]:]+$") && t.Length > 2 && !Regex.IsMatch(t, @"^\d{1,5}$")) return t; } return def; }
        double Ms1() { foreach (var o in VisRows()) { double m = RowMs(o[2]); if (!double.IsNaN(m)) return m; } return double.NaN; }
        bool Red() { return VisRows().Any(o => o[0] == "2"); }
        static XStep Bc(int from, int to, string label, string detail) { return new XStep { From = from, To = to, Label = label, Detail = detail, All = true }; }
        static XStep Lk(XStep s) { s.Sealed = true; return s; }
        static readonly Dictionary<string, string[]> SnmpAsk = new Dictionary<string, string[]> {
            { "snmp", new[] { "sysDescr, sysName, sysUpTime", "what are you?" } }, { "snmpif", new[] { "ifOperStatus, ifInErrors", "how are your ports?" } },
            { "snmptraffic", new[] { "ifHCInOctets (twice, 5 s apart)", "how many bytes so far?" } }, { "snmpcpu", new[] { "hrProcessorLoad, hrStorageUsed", "how busy are you?" } },
            { "snmplldp", new[] { "lldpRemSysName, lldpRemPortId", "who is plugged into you?" } }, { "snmpsens", new[] { "entPhySensorValue", "how warm, how bright?" } },
            { "snmpsnap", new[] { "system, interfaces, sensors, neighbours", "tell me everything" } }, { "snmpmac", new[] { "dot1dTpFdbPort", "which port has this MAC?" } },
            { "snmpvlan", new[] { "dot1qVlanStaticName, egress ports", "which VLANs, on which ports?" } }, { "snmppoe", new[] { "pethPsePortDetectionStatus, power", "who draws power?" } },
            { "snmplag", new[] { "dot3adAggPortAttachedAggID", "which links are bundled?" } }, { "snmpprn", new[] { "prtMarkerSuppliesLevel", "how much toner?" } },
            { "snmpups", new[] { "upsEstimatedMinutesRemaining", "how long on battery?" } }, { "snmpwalk", new[] { "GetNext, GetNext, GetNext...", "the next one after this?" } } };

        float Wave4(Graphics g, float x, float y, float w, string st)
        {
            string id = tbSel, t; double ms = Ms1(); List<XStep> s; string cap, foot; string[] who; int fail = -1;
            switch (id)
            {
                case "mail":
                    t = Tg("mail.example.com"); who = new[] { L.T("This PC"), t };
                    s = new List<XStep> { X(0, 1, L.T("hello (SYN)"), "TCP 587 / 465 / 993 / 995", double.NaN), X(1, 0, "220 " + L.T("ready - the server's greeting"), L.T("its name and software"), ms),
                        X(0, 1, "EHLO", L.T("what can you do?"), double.NaN), X(1, 0, "250 STARTTLS, AUTH", L.T("the list of what it offers"), double.NaN),
                        X(0, 1, "STARTTLS", L.T("let's seal the line first"), double.NaN), Lk(X(1, 0, L.T("certificate + keys"), L.T("sealed from here - AdeeboMeter stops before any password"), double.NaN)) };
                    cap = "KNOCKING ON A MAIL SERVER"; foot = "Mail apps fail here when the port is blocked (many networks block 25), or when the certificate's name doesn't match."; fail = 1; break;
                case "smtppath":
                    t = Tg("example.com"); who = new[] { L.T("This PC"), L.T("DNS server"), "MX " + L.T("server") };
                    s = new List<XStep> { X(0, 1, L.T("who takes mail for") + " " + t + "?", "DNS · MX", double.NaN), X(1, 0, "mx1." + t + " " + L.T("(priority 10)"), L.T("lowest number is tried first"), double.NaN),
                        X(0, 2, L.T("hello (SYN)"), "TCP 25", double.NaN), X(2, 0, "220 " + L.T("ready"), L.T("the receiving server's greeting"), ms),
                        X(0, 2, "EHLO", "", double.NaN), X(2, 0, "250 STARTTLS", L.T("offers encryption between mail servers"), double.NaN) };
                    cap = "HOW MAIL FOR A DOMAIN GETS DELIVERED"; foot = "Senders look up MX, then talk to that server on port 25. Home networks often can't reach 25 - that's normal."; fail = 3; break;
                case "ssh":
                    t = Tg("192.0.2.22"); who = new[] { L.T("This PC"), t };
                    s = new List<XStep> { X(0, 1, L.T("hello (SYN)"), "TCP 22", double.NaN), X(1, 0, L.T("hello back (SYN-ACK)"), L.T("the port is open"), ms),
                        X(1, 0, "SSH-2.0-OpenSSH_9.6", L.T("the server says its version first"), double.NaN), X(0, 1, L.T("our version, and goodbye"), L.T("no keys, no password - just the banner"), double.NaN) };
                    cap = "READING AN SSH SERVER'S BANNER"; foot = "The banner tells you the server is really SSH and how old its software is - nothing is signed in."; fail = 1; break;
                case "stun":
                    who = new[] { L.T("This PC"), L.T("your router (NAT)"), L.T("STUN server") };
                    s = new List<XStep> { X(0, 1, L.T("Binding request"), L.T("from") + " 192.168.1.20:50000 · UDP 3478", double.NaN), X(1, 2, L.T("same packet, new return address"), L.T("the router swaps in its public address and a port"), double.NaN),
                        X(2, 1, L.T("you look like") + " 203.0.113.7:61234", "XOR-MAPPED-ADDRESS", ms), X(1, 0, L.T("passed back in to you"), L.T("now the PC knows its outside address"), double.NaN) };
                    cap = "WHAT YOU LOOK LIKE FROM OUTSIDE"; foot = "Calls and games use this to find a path to each other. If the port changes per destination, direct calls are harder (symmetric NAT)."; fail = 2; break;
                case "teams":
                    who = new[] { L.T("This PC"), L.T("your router (NAT)"), L.T("Teams media relay") };
                    s = new List<XStep> { X(0, 2, L.T("media test packet"), "UDP 3478-3481", double.NaN), X(2, 0, L.T("echo - UDP gets through"), L.T("calls can use UDP: smooth"), ms),
                        X(0, 2, L.T("second test from another port"), L.T("does the router keep the same outside port?"), double.NaN), X(2, 0, L.T("NAT type"), L.T("same port = easy; changing = relay needed"), double.NaN) };
                    cap = "CAN TEAMS CALLS USE UDP HERE?"; foot = "Without UDP, Teams falls back to TCP 443 - it works, but audio and video stutter more."; fail = 1; break;
                case "ldapcheck":
                    t = Tg("corp.example.com"); who = new[] { L.T("This PC"), L.T("DNS server"), L.T("domain controller") };
                    s = new List<XStep> { X(0, 1, "_ldap._tcp." + t + "?", "DNS · SRV", double.NaN), X(1, 0, "dc1." + t + " :389", L.T("which servers run the directory"), double.NaN),
                        X(0, 2, L.T("hello (SYN)"), "TCP 389 / 636", double.NaN), X(2, 0, L.T("hello back"), L.T("the directory is reachable"), ms),
                        X(0, 2, L.T("what are you? (rootDSE)"), L.T("anonymous - no sign-in"), double.NaN), X(2, 0, "DC=corp, DC=example, DC=com", L.T("its naming context"), double.NaN) };
                    cap = "FINDING AND KNOCKING ON A DOMAIN CONTROLLER"; foot = "Sign-ins, Group Policy and drive mappings all start here. If DNS can't find _ldap, nothing else will work."; fail = 3; break;
                case "rdpcheck":
                    t = Tg("192.0.2.50"); who = new[] { L.T("This PC"), t };
                    s = new List<XStep> { X(0, 1, L.T("hello (SYN)"), "TCP 3389", double.NaN), X(1, 0, L.T("hello back (SYN-ACK)"), L.T("something listens on 3389"), ms),
                        X(0, 1, L.T("Remote Desktop, please"), "X.224 Connection Request", double.NaN), X(1, 0, L.T("yes - TLS and sign-in first (NLA)"), "Connection Confirm", double.NaN) };
                    cap = "IS REMOTE DESKTOP REALLY THERE?"; foot = "An open port isn't proof; the X.224 answer is. AdeeboMeter stops before any sign-in."; fail = 1; break;
                case "syslog": case "trap": case "netflow":
                    {
                        string port = id == "syslog" ? "UDP 514" : id == "trap" ? "UDP 162" : "UDP 2055";
                        string what = id == "syslog" ? "<134> AdeeboMeter test" : id == "trap" ? L.T("SNMP trap: coldStart (test)") : L.T("NetFlow v5 record (test)");
                        t = Tg("192.0.2.60"); who = new[] { L.T("This PC"), t };
                        s = new List<XStep> { X(0, 1, what, port + " · " + L.T("one packet, one way"), double.NaN), X(1, 1, L.T("(no reply - this protocol never answers)"), L.T("look on the collector to see it arrived"), double.NaN) };
                        cap = id == "syslog" ? "A TEST LOG LINE, SENT" : id == "trap" ? "A TEST TRAP, SENT" : "A TEST FLOW RECORD, SENT";
                        foot = "UDP sends and forgets. 'Sent' only means it left this PC - check the server's log or collector for it.";
                    }
                    break;
                case "dhcpopt":
                    who = new[] { L.T("This PC"), L.T("DHCP server") };
                    s = new List<XStep> { Bc(0, 1, L.T("Inform: I have an address - just tell me the options"), L.T("broadcast · UDP 68 → 67")),
                        X(1, 0, L.T("Ack with options"), "3 router · 6 DNS · 15 domain · 42 NTP · 66/67 boot", ms) };
                    cap = "ASKING THE DHCP SERVER WHAT IT HANDS OUT"; foot = "Phones and PXE boot use options too: 66/67 (boot server and file), 150 (Cisco TFTP), 43 (vendor)."; fail = 1; break;
                case "icmpb":
                    t = Tg("192.0.2.1"); who = new[] { L.T("This PC"), L.T("a router on the way"), t };
                    s = new List<XStep> { X(0, 2, L.T("Echo request"), L.T("your size, DF, TTL"), double.NaN), X(2, 0, L.T("Echo reply"), L.T("the size made it"), ms),
                        X(0, 1, L.T("too big, DF set"), "", double.NaN), X(1, 0, L.T("Fragmentation needed - MTU here"), L.T("or 'Time exceeded' when the TTL runs out"), double.NaN) };
                    s[2].Lv = s[3].Lv = 1; cap = "AN ICMP PACKET YOU SHAPED YOURSELF"; foot = "Raise the size with DF on until it fails - that's the path MTU. A small TTL shows which router is that many hops away."; fail = 1; break;
                case "udpsend":
                    t = Tg("192.0.2.70"); who = new[] { L.T("This PC"), t };
                    s = new List<XStep> { X(0, 1, L.T("your UDP datagram"), L.T("to the port you chose"), double.NaN), X(1, 0, L.T("a reply - something listens"), L.T("or: 'port unreachable' (closed) or silence (filtered, or no reply by design)"), ms) };
                    cap = "ONE UDP PACKET, AND WHAT CAME BACK"; foot = "Silence is not failure with UDP: many services only answer the right question."; fail = 1; break;
                case "tcpsend":
                    t = Tg("192.0.2.80"); who = new[] { L.T("This PC"), t };
                    s = new List<XStep> { X(0, 1, "SYN", L.T("may I?"), double.NaN), X(1, 0, "SYN-ACK", L.T("yes"), ms), X(0, 1, "ACK", L.T("open"), double.NaN),
                        X(0, 1, L.T("your text"), L.T("data"), double.NaN), X(1, 0, L.T("its reply"), L.T("first bytes shown"), double.NaN), X(0, 1, "FIN", L.T("done - closed politely"), double.NaN) };
                    cap = "A TCP CONVERSATION, START TO FINISH"; foot = "RST instead of SYN-ACK = closed. No answer at all = a firewall dropped it."; fail = 1; break;
                case "httpreq":
                    t = Tg("example.com"); who = new[] { L.T("This PC"), t };
                    s = new List<XStep> { X(0, 1, "SYN → SYN-ACK", "TCP 443", double.NaN), Lk(X(0, 1, L.T("TLS hello - name") + ": " + t, "SNI", double.NaN)),
                        Lk(X(0, 1, L.T("your request"), L.T("method, path, headers"), double.NaN)), Lk(X(1, 0, L.T("status line + headers"), "200 / 301 / 403 / 404 / 500", ms)) };
                    cap = "YOUR HTTP REQUEST, AS SENT"; foot = "3xx = go elsewhere, 4xx = your request (or access), 5xx = the server broke."; fail = 3; break;
                case "twamp":
                    t = Tg("192.0.2.90"); who = new[] { L.T("This PC (sender)"), L.T("reflector") + " " + t };
                    s = new List<XStep> { X(0, 1, L.T("test packet, stamped T1"), "UDP 862 · TWAMP-light", double.NaN), X(1, 1, L.T("received T2, sent back T3"), L.T("the reflector stamps both"), double.NaN),
                        X(1, 0, L.T("back at T4"), L.T("round trip = (T4-T1) - (T3-T2)"), ms) };
                    cap = "TWAMP: TIMESTAMPS BOTH WAYS"; foot = "Taking the reflector's own time out gives the true network delay - with synced clocks, each direction separately."; fail = 2; break;
                case "dscp":
                    t = Tg("192.0.2.1"); who = new[] { L.T("This PC"), L.T("your router"), L.T("provider"), t };
                    s = new List<XStep> { X(0, 1, L.T("ping marked EF (46)"), L.T("'please treat as voice'"), double.NaN), X(1, 2, L.T("mark kept"), "", double.NaN),
                        X(2, 3, L.T("mark often reset to 0 here"), L.T("providers usually ignore your marks"), double.NaN), X(3, 0, L.T("reply"), L.T("compare: marked vs unmarked time"), ms) };
                    s[2].Lv = 1; cap = "DOES ANYONE HONOUR YOUR PRIORITY MARK?"; foot = "Marks matter on your own network (Wi-Fi WMM, company QoS). Across the internet they rarely survive."; fail = 3; break;
                case "wol":
                    who = new[] { L.T("This PC"), L.T("the sleeping PC"), L.T("everyone else") };
                    s = new List<XStep> { Bc(0, 2, L.T("magic packet: FF×6 then its MAC ×16"), L.T("broadcast · UDP 9")), X(0, 1, L.T("the sleeping network card sees its MAC"), L.T("and wakes the PC"), double.NaN),
                        X(1, 0, L.T("(no reply - it's booting)"), L.T("try a ping in a minute"), double.NaN) };
                    cap = "WAKING A PC OVER THE NETWORK"; foot = "Works on the same network; across a router it needs a directed broadcast. Wake-on-LAN must be on in BIOS and the adapter."; break;
                case "proxy":
                    t = Tg("example.com"); who = new[] { L.T("This PC"), L.T("PAC / WPAD"), L.T("proxy"), t };
                    s = new List<XStep> { X(0, 1, L.T("which way for") + " " + t + "?", L.T("the proxy script (PAC)"), double.NaN), X(1, 0, "PROXY proxy:8080", L.T("or DIRECT"), double.NaN),
                        X(0, 2, "CONNECT " + t + ":443", "", double.NaN), X(2, 3, L.T("the proxy connects for you"), "", double.NaN), X(2, 0, "200 Connection established", "", ms),
                        Lk(X(0, 3, L.T("TLS straight through"), L.T("the proxy sees the name, not the page"), double.NaN)) };
                    cap = "WHICH WAY A URL GOES - DIRECT OR VIA THE PROXY"; foot = "407 = the proxy wants a sign-in. A slow PAC download slows every first visit."; fail = 4; break;
                case "vpnleak":
                    who = new[] { L.T("This PC"), L.T("VPN tunnel"), L.T("your provider"), L.T("DNS / sites") };
                    s = new List<XStep> { Lk(X(0, 1, L.T("IPv4 traffic"), L.T("into the tunnel - hidden"), double.NaN)), Lk(X(1, 3, L.T("out from the VPN's address"), "", double.NaN)),
                        X(0, 2, L.T("IPv6 or DNS outside the tunnel?"), L.T("a leak: your provider sees it"), double.NaN), X(2, 3, L.T("from your real address"), "", double.NaN) };
                    s[2].Lv = s[3].Lv = 1; cap = "WHAT GOES IN THE TUNNEL - AND WHAT SLIPS OUT"; foot = "Leaks are usually IPv6 (VPN only covers IPv4) or DNS sent to the home router instead of the VPN's."; break;
                case "ping6":
                    t = Tg("2001:db8::1"); who = new[] { L.T("This PC"), L.T("router"), t };
                    s = new List<XStep> { Bc(0, 1, L.T("Neighbour Solicitation: who has fe80::1?"), "ICMPv6 135 · multicast"), X(1, 0, L.T("Neighbour Advertisement: me"), L.T("IPv6's ARP"), double.NaN),
                        X(0, 2, L.T("Echo request"), "ICMPv6 128", double.NaN), X(2, 0, L.T("Echo reply"), "ICMPv6 129", ms) };
                    cap = "AN IPV6 PING, FROM THE FIRST HOP"; foot = "No ARP in IPv6 - neighbour discovery does it. Blocking all ICMPv6 breaks IPv6 itself."; fail = 3; break;
                case "v6routers": case "rareplay":
                    who = new[] { L.T("This PC"), L.T("your router"), L.T("another router?") };
                    s = new List<XStep> { Bc(0, 1, L.T("Router Solicitation: any routers?"), "ICMPv6 133 · ff02::2"), X(1, 0, L.T("Router Advertisement"), L.T("prefix 2001:db8:1::/64 · lifetime · DNS"), ms),
                        X(2, 0, L.T("a second Advertisement"), L.T("a second router - or a rogue one"), double.NaN) };
                    s[2].Lv = 1; cap = "HOW IPV6 FINDS ITS ROUTER"; foot = "Every router that answers becomes a way out. An unexpected one (a phone hotspot, a misconfigured box) can steal traffic."; break;
                case "discover": case "nbname":
                    who = new[] { L.T("This PC"), L.T("everyone here"), L.T("the device") };
                    s = new List<XStep> { Bc(0, 1, id == "nbname" ? L.T("who is 192.168.1.40? (NetBIOS, LLMNR, mDNS)") : L.T("who offers printers? (_ipp._tcp.local)"), id == "nbname" ? "UDP 137 / 5355 / 5353" : "mDNS · 224.0.0.251:5353"),
                        X(2, 0, id == "nbname" ? L.T("I'm LIVINGROOM-TV") : L.T("me: Office printer"), L.T("each device answers for itself"), ms),
                        Bc(0, 1, "M-SEARCH ssdp:all", "SSDP · 239.255.255.250:1900"), X(2, 0, L.T("I'm a media renderer"), L.T("TVs, speakers, routers"), double.NaN) };
                    cap = id == "nbname" ? "ASKING A DEVICE ITS NAME" : "DEVICES ANNOUNCING THEMSELVES"; foot = "These only reach your own network - the router doesn't pass them on. Guest Wi-Fi often blocks them."; break;
                case "clock": case "timesrc":
                    who = new[] { L.T("This PC"), id == "timesrc" ? L.T("domain controller") : L.T("time server"), L.T("its time source") };
                    s = new List<XStep> { X(0, 1, L.T("what time is it? (T1)"), "NTP · UDP 123", double.NaN), X(1, 2, L.T("it follows its own source"), L.T("stratum: steps from an atomic clock"), double.NaN),
                        X(1, 0, L.T("T2 got it, T3 answered"), "", ms), X(0, 0, L.T("arrived T4 - offset worked out"), L.T("the trip is cancelled out"), double.NaN) };
                    cap = id == "timesrc" ? "WHERE THIS PC GETS ITS TIME" : "IS THIS PC'S CLOCK RIGHT?"; foot = "Domain PCs take time from a domain controller; home PCs from time.windows.com. More than 5 minutes off and sign-ins fail."; fail = 2; break;
                case "hairpin":
                    who = new[] { L.T("This PC"), L.T("your router"), L.T("NAS / server inside") };
                    s = new List<XStep> { X(0, 1, L.T("to your public address") + " 203.0.113.7:443", L.T("from inside"), double.NaN), X(1, 2, L.T("turned around (hairpin NAT)"), L.T("forwarded to") + " 192.168.1.10", double.NaN),
                        X(2, 1, L.T("answer"), "", double.NaN), X(1, 0, L.T("back to you"), L.T("works from inside and outside"), ms) };
                    cap = "REACHING YOUR OWN SERVER BY ITS OUTSIDE ADDRESS"; foot = "Many routers can't hairpin: from outside it works, from inside it times out. Fix: a local DNS name or turn on NAT loopback."; fail = 1; break;
                case "dot1x": case "auth": case "eapreplay":
                    who = new[] { L.T("This PC"), L.T("switch / access point"), L.T("RADIUS server") };
                    s = new List<XStep> { X(0, 1, "EAPOL-Start", L.T("I'd like to join"), double.NaN), X(1, 0, L.T("Identity?"), "EAP-Request", double.NaN), X(0, 1, "host/PC01.corp.example.com", "EAP-Response", double.NaN),
                        X(1, 2, "Access-Request", "UDP 1812", double.NaN), Lk(X(2, 0, L.T("TLS: certificates both ways"), "Access-Challenge × n", double.NaN)), X(2, 1, "Access-Accept + VLAN", "", ms), X(1, 0, "EAP-Success", L.T("the port opens"), double.NaN) };
                    cap = "802.1X: HOW THIS PC GETS LET IN"; foot = "Most failures are at the certificate step (expired, untrusted CA, wrong name) or a Reject for the wrong group."; fail = 5; break;
                case "dhcpreplay": return VisDhcp(g, x, y, w, st);
                case "udpmtu":
                    t = Tg("192.0.2.1"); who = new[] { L.T("This PC"), L.T("a link with a small MTU"), t };
                    s = new List<XStep> { X(0, 1, L.T("1472-byte UDP, Don't Fragment"), "", double.NaN), X(1, 0, L.T("too big - MTU 1400 here"), L.T("ICMP 'fragmentation needed'"), double.NaN),
                        X(0, 2, L.T("1372 bytes"), L.T("fits"), double.NaN), X(2, 0, L.T("answer"), L.T("QUIC and DNS use this size"), ms) };
                    s[1].Lv = 1; cap = "THE BIGGEST UDP PACKET THAT FITS"; foot = "QUIC (HTTP/3) and big DNS answers fail quietly when the path's MTU is smaller and the ICMP message is blocked."; fail = 3; break;
                case "iperf":
                    t = Tg("192.0.2.5"); who = new[] { L.T("This PC"), "iperf3 " + t };
                    s = new List<XStep> { X(0, 1, L.T("control: test for 10 s"), "TCP 5201", double.NaN), X(1, 0, L.T("ready"), "", double.NaN),
                        X(0, 1, L.T("data at full speed"), L.T("as fast as the line allows"), double.NaN), X(1, 0, L.T("results: Mbit/s, retransmits"), "", ms) };
                    cap = "IPERF3: A SPEED TEST TO YOUR OWN SERVER"; foot = "Your own server takes the internet out of it - perfect for testing Wi-Fi or a link between offices."; fail = 3; break;
                case "https": case "certlive":
                    t = Tg("example.com"); who = new[] { L.T("This PC"), t };
                    s = new List<XStep> { X(0, 1, L.T("TLS hello - name") + ": " + t, "ClientHello · TCP 443", double.NaN), X(1, 0, L.T("certificate chain"), "ServerHello", ms),
                        X(0, 0, L.T("checked: name, dates, issuer"), L.T("is it trusted by Windows?"), double.NaN), Lk(X(0, 1, L.T("keys agreed"), L.T("sealed from here"), double.NaN)) };
                    cap = id == "certlive" ? "WHAT THE SERVER REALLY SENDS" : "THE HTTPS HANDSHAKE, CHECKED"; foot = "A missing middle certificate works in some browsers and fails in apps. The name must match exactly."; fail = 2; break;
                case "radiuslab":
                    t = Tg("192.0.2.30"); who = new[] { L.T("This PC"), t };
                    s = new List<XStep> { X(0, 1, L.T("Access-Request: test user"), L.T("UDP 1812 · password hidden with the secret"), double.NaN), X(1, 0, "Access-Accept / Reject", L.T("and the attributes it returns"), ms) };
                    cap = "A TEST SIGN-IN AGAINST RADIUS"; foot = "Silence = wrong shared secret or firewall. Reject = the server works, the account or policy doesn't."; fail = 1; break;
                default:
                    {
                        string[] q; if (!SnmpAsk.TryGetValue(id, out q)) return y;
                        t = Tg("192.0.2.10"); who = new[] { L.T("This PC"), t };
                        if (id == "snmpwalk")
                            s = new List<XStep> { X(0, 1, "GetNext 1.3.6.1.2.1.1", "UDP 161", double.NaN), X(1, 0, "sysDescr = ...", "", ms), X(0, 1, "GetNext sysDescr", "", double.NaN),
                                X(1, 0, "sysObjectID = ...", "", double.NaN), X(0, 1, L.T("... and on"), "", double.NaN), X(1, 0, "endOfMibView", L.T("the end of the branch"), double.NaN) };
                        else
                            s = new List<XStep> { X(0, 1, "GetRequest - " + L.T(q[1]), q[0] + " · UDP 161", double.NaN), X(1, 0, "Response", L.T("the values, one per object"), ms) };
                        cap = "ASKING A DEVICE OVER SNMP"; foot = "No answer = wrong community/user, an ACL on the device, or SNMP turned off - SNMP never says which."; fail = 1;
                    }
                    break;
            }
            if (fail >= 0 && fail < s.Count && Red()) s[fail].Lv = 2;
            return ExchangeCard(g, x, y, w, st, cap, who, s, foot);
        }
    }
}
