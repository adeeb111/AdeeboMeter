// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Investigate > Toolbox: every on-demand tool in one place - pick one on the left, fill in at most three boxes, Run.
    // Results are rows with where each fact came from; some tools add a chart (iperf, sustained ping) or a live table
    // (MTU per hop, MTR). Nothing runs until you press Run, and Stop always works.
    sealed partial class FullView
    {
        sealed class Tool { public string Id, Group, Title; public string[] Inputs = Array.Empty<string>(); public Func<string[], Toolkit.Run, List<string[]>> Do; }
        Panel toolboxPage; DB tbList, tbCanvas; FlowLayoutPanel tbBar; PillButton tbRun; readonly List<RoundBox> tbBoxes = new List<RoundBox>(); readonly RoundBox[] tbPool = new RoundBox[4]; CheckBox tbSweepOk; string tbIperfSrv = ""; readonly List<string> tbVisible = new List<string>(); readonly Dictionary<string, float> tbRowY = new Dictionary<string, float>();
        string tbSel = "mtu", tbRowsKey = ""; List<string[]> tbRows; Toolkit.Run tbRun0; volatile bool tbBusy; string tbTitle = ""; DateTime tbAt;
        List<PathTools.Hop> mtuHops; IPAddress mtuDst; volatile bool mtuBusy;

        List<Tool> tools;
        List<Tool> Tools2()
        {
            if (tools != null) return tools;
            Func<string, string, string, string, Func<string[], Toolkit.Run, List<string[]>>, Tool> T = (id, g, t, ins, f) => new Tool { Id = id, Group = g, Title = t, Inputs = ins == null ? Array.Empty<string>() : ins.Split('|'), Do = f };   // inputs: "first|second|third"
            tools = new List<Tool> {
                T("mtu", "PATH", "MTU per hop", "Destination - e.g. github.com", null),
                T("udpmtu", "PATH", "MTU over UDP (QUIC / DNS)", "Target - a website (port 443) or a DNS server (port 53)|Port - 443 or 53", (a, r) => UdpMtu.Run(a[0], a[1], r)),
                T("mtr", "PATH", "MTR - loss per hop over time", "Destination|Minutes (1-10)", (a, r) => { var dst = Icmp.Resolve4(a[0]); if (dst == null) return Bad("couldn't find " + a[0]); int min; if (!int.TryParse(a[1], out min)) min = 1; min = Math.Max(1, Math.Min(10, min)); r.Status = "Tracing..."; mtrHops = PathTools.Trace(dst, 30, () => r.Stop); PathTools.Mtr(dst, mtrHops, min * 60, () => r.Stop, delegate { r.Status = "Probing every hop once a second..."; }); var res = new List<string[]> { new[] { "0", "Verdict", PathTools.MtrVerdict(mtrHops), "", "i" } }; res.AddRange(PathTools.Flags(mtrHops)); return res; }),
                T("ret", "PATH", "The way back (return path)", "Destination", (a, r) => Toolkit.ReturnPath(a[0], r)),
                T("owners", "PATH", "Who carries my traffic (BGP)", "Destination", (a, r) => { if (!Online.On) return OnlineOff(); var dst = Icmp.Resolve4(a[0]); if (dst == null) return Bad("couldn't find " + a[0]); r.Status = "Tracing..."; var h = PathTools.Trace(dst, 30, () => r.Stop); r.Status = "Looking up each network..."; return Online.PathOwners(h, a[0]); }),
                T("route", "PATH", "Which route does this take?", "Destination - e.g. 10.20.30.40 or a name", (a, r) => RoutingTools.RouteFor(Host(a[0]))),
                T("gateway", "PATH", "Your gateway and failover", null, (a, r) => RoutingTools.Gateway(M)),
                T("v6routers", "PATH", "IPv6 routers on this network", null, (a, r) => RoutingTools.Ipv6Routers()),
                T("rlisten", "PATH", "Routing protocols on my segment", "Seconds (10-120)", (a, r) => RoutingTools.Listen(M, a[0], r)),
                T("discover", "PATH", "Announcements (mDNS, SSDP, LLMNR, NetBIOS)", "Seconds (10-120)", (a, r) => RoutingTools.Listen(M, a[0], r, true)),
                T("ping6", "PATH", "IPv6 ping and trace", "Destination - e.g. www.google.com", (a, r) => SysTools.Ping6(a[0], r)),
                T("overlap", "PATH", "Overlapping networks (home vs VPN)", null, (a, r) => PathTools.Overlap()),
                T("ip46", "PATH", "IPv6 vs IPv4", "Site - e.g. www.google.com", (a, r) => PathTools.Ip46(Host(a[0]))),
                T("vpn", "WHY DOES IT FAIL?", "VPN and company proxy - is it the slow part?", null, (a, r) => VpnAware.Run(M, r)),
                T("proxy", "WHY DOES IT FAIL?", "Which proxy does this URL use?", "URL - e.g. https://outlook.office.com", (a, r) => WhyFails.Proxy(a[0])),
                T("hosts", "WHY DOES IT FAIL?", "Hosts file", "Name to look for (optional)", (a, r) => WhyFails.Hosts(Host(a[0]))),
                T("dnsrules", "WHY DOES IT FAIL?", "DNS hijack and VPN name rules", null, (a, r) => WhyFails.DnsRules()),
                T("port", "WHY DOES IT FAIL?", "Port test", "host:port - e.g. smtp.office365.com:587", (a, r) => WhyFails.Port(a[0])),
                T("teams", "WHY DOES IT FAIL?", "Teams UDP test (and NAT type)", null, (a, r) => WhyFails.TeamsUdp()),
                T("clock", "WHY DOES IT FAIL?", "Clock check", null, (a, r) => WhyFails.Clock()),
                T("iperf", "SPEED", "iperf3 to your server", "Your iperf3 server - e.g. 10.0.0.5|Direction - both, upload or download|Seconds (1-60)|Advanced (optional) - iperf3 options: -P 4 -u -b 50M -l -w -S -O -M", (a, r) => { var o = IperfOpts(a); return o == null ? Bad("Direction: type both, upload or download") : Toolkit.Iperf(a[0], o, r); }),
                T("bloat", "SPEED", "Latency under load (bufferbloat)", "Your iperf3 server - on the far side of the line|Ping target (optional) - the server itself, or e.g. 1.1.1.1", (a, r) => Toolkit.Loaded(a[0], a[1], r)),
                T("ping", "SPEED", "Sustained ping", "Target|How many (10-2000)|Every ms (20-5000)", (a, r) => Toolkit.Sustained(a[0], a[1], a[2], r)),
                T("dscp", "SPEED", "DSCP-marked ping", "Target|DSCP - EF, AF41, CS6 or 0-63|How many (2-200)", (a, r) => Toolkit.DscpPing(a[0], a[1], a[2], r)),
                T("tcpping", "SPEED", "TCP ping", "host:port - e.g. github.com:443|How many (1-500)", (a, r) => Protocols.TcpPing(a[0], a[1], r)),
                T("twamp", "SPEED", "TWAMP-light to a reflector", "Reflector - host or host:port (UDP 862)|Packets (10-2000)", (a, r) => Protocols.Twamp(a[0], a[1], r)),
                T("cloud", "SPEED", "Nearest cloud", null, (a, r) => Toolkit.Nearest(r)),
                T("dig", "DNS AND WEB", "dig - any DNS record", "Name - e.g. github.com or an IP|Type - A, AAAA, MX, TXT, NS, SRV, CAA, PTR, HTTPS, DS, DNSKEY, TLSA, SSHFP or TRACE|Server (optional) - 1.1.1.1 or https://cloudflare-dns.com/dns-query", (a, r) => Toolkit.Dig(a[0], a[1], a[2], M)),
                T("http", "DNS AND WEB", "HTTP and TLS inspector", "URL - e.g. github.com or https://203.0.113.5|Name to ask for (optional, SNI) - e.g. www.example.com", (a, r) => Toolkit.Http(a[0], a[1])),
                T("httptime", "DNS AND WEB", "HTTP timing, repeated", "URL - e.g. outlook.office.com|How many (1-50)", (a, r) => Protocols.HttpTiming(a[0], a[1], r)),
                T("doh", "DNS AND WEB", "Encrypted DNS (DoH / DoT)", null, (a, r) => Protocols.EncryptedDns()),
                T("ntp", "PROTOCOLS", "NTP - ask a time server", "Server - e.g. time.windows.com or pool.ntp.org", (a, r) => Protocols.Ntp(a[0])),
                T("mail", "PROTOCOLS", "Mail server (SMTP / IMAP / POP)", "host:port - 25 / 587 / 465 SMTP, 143 / 993 IMAP, 110 / 995 POP", (a, r) => Protocols.Mail(a[0])),
                T("ssh", "PROTOCOLS", "SSH banner", "host or host:port", (a, r) => Protocols.Ssh(a[0])),
                T("snmp", "SNMP", "SNMP - what is this device?", SnmpIn, (a, r) => SnmpTools.System(a[0], a[1])),
                T("snmpif", "SNMP", "SNMP - interfaces (status, errors)", SnmpIn, (a, r) => SnmpTools.Interfaces(a[0], a[1], r)),
                T("snmptraffic", "SNMP", "SNMP - traffic per port", SnmpIn, (a, r) => SnmpTools.Traffic(a[0], a[1], r)),
                T("snmpcpu", "SNMP", "SNMP - CPU and memory", SnmpIn, (a, r) => SnmpTools.CpuMem(a[0], a[1], r)),
                T("snmplldp", "SNMP", "SNMP - neighbours (LLDP)", SnmpIn, (a, r) => SnmpTools.Lldp(a[0], a[1], r)),
                T("snmpsens", "SNMP", "SNMP - sensors (temperature, optics)", SnmpIn, (a, r) => SnmpTools.Sensors(a[0], a[1], r)),
                T("snmpsnap", "SNMP", "SNMP - device snapshot (everything at once)", SnmpIn, (a, r) => SnmpTools.Snapshot(a[0], a[1], r)),
                T("snmpmac", "SNMP", "SNMP - find a MAC on a switch", SnmpIn + "|MAC or IP to find - empty lists the whole table", (a, r) => SnmpTools.FindMac(a[0], a[1], a[2], r)),
                T("snmpvlan", "SNMP", "SNMP - VLANs and their ports", SnmpIn, (a, r) => SnmpTools.Vlans(a[0], a[1], r)),
                T("snmppoe", "SNMP", "SNMP - PoE power per port", SnmpIn, (a, r) => SnmpTools.Poe(a[0], a[1], r)),
                T("snmplag", "SNMP", "SNMP - link aggregation (LACP)", SnmpIn, (a, r) => SnmpTools.Lag(a[0], a[1], r)),
                T("snmpprn", "SNMP", "SNMP - printer status and toner", SnmpIn, (a, r) => SnmpTools.Printer(a[0], a[1], r)),
                T("snmpups", "SNMP", "SNMP - UPS battery and runtime", SnmpIn, (a, r) => SnmpTools.Ups(a[0], a[1], r)),
                T("snmpwalk", "SNMP", "SNMP - walk any OID or name", SnmpIn + "|OID or MIB name - e.g. ifTable", (a, r) => SnmpTools.Walk(a[0], a[1], a[2], r)),
                T("mibs", "SNMP", "MIB folder and search", "Search your MIBs - e.g. temperature, optic, fan", (a, r) => SnmpTools.Search(a[0], r)),
                T("syslog", "PROTOCOLS", "Syslog - send a test message", "Collector - host or host:port (UDP 514)|Text (optional)|Level (optional) - e.g. local7.warning", (a, r) => { var gate = new List<string[]>(); return Lab.Allowed(M, a[3], gate) ? Protocols.Syslog(a[0], a[1], a[2]) : gate; }),
                T("icmpb", "LAB", "ICMP builder (size, DF, TTL)", "Target - IPv4 or IPv6|How many (1-1000)|Options - e.g. size 1472 df ttl 5 every 200 dscp ef compare", (a, r) => Lab.Icmp(M, a[0], a[1], a[2], a[3], r)),
                T("udpsend", "LAB", "UDP sender", "host:port - e.g. 10.0.0.5:5060|Payload - text, or hex:0a0b0c|How many (1-1000)", (a, r) => Lab.Udp(M, a[0], a[1], a[2], a[3], r)),
                T("tcpsend", "LAB", "TCP sender (send, see the reply)", "host:port - e.g. 10.0.0.5:8080|Payload - text with \\r\\n, or hex:0a0b0c", (a, r) => Lab.Tcp(M, a[0], a[1], a[3], r)),
                T("arpwho", "LAB", "ARP who-has", "IP on your network - e.g. 192.168.1.23", (a, r) => Lab.Arp(M, a[0], a[3])),
                T("dhcpopt", "LAB", "DHCP options from the server", null, (a, r) => Lab.DhcpOptions(M, a[3])),
                T("trap", "LAB", "SNMP test trap (send only)", "Trap receiver - host or host:port (UDP 162)|Community (default public)", (a, r) => Lab.Trap(M, a[0], a[1], a[3])),
                T("netflow", "LAB", "NetFlow test record", "Collector - host or host:port (UDP 2055)", (a, r) => Lab.Flow(M, a[0], a[3])),
                T("httpreq", "WEB", "HTTP request builder", "URL - e.g. https://example.com/api/health|Method - GET, HEAD, POST, PUT, PATCH, DELETE, OPTIONS|Headers (optional) - Name: value; Name: value|Body (optional)", (a, r) => Reach.Http(a[0], a[1], a[2], a[3])),
                T("stun", "WEB", "STUN - my public address and port", "STUN server - e.g. stun.example.com:3478", (a, r) => Reach.Stun(a[0])),
                T("nbname", "YOUR NETWORK", "Name of a device (NetBIOS, LLMNR, mDNS)", "IP - e.g. 192.168.1.23", (a, r) => Reach.NameOf(a[0])),
                T("finddev", "YOUR NETWORK", "Find devices (mDNS, SSDP, WS-Discovery)", null, (a, r) => Reach.Find(M, a[3], r)),
                T("remote", "REMOTE", "SSH, SCP and remote commands", "Host - e.g. 10.0.0.1|User (optional)|Port (optional, 22)", (a, r) => Reach.Remote(a[0], a[1], a[2])),
                T("manyports", "WEB", "Many ports for one service", "Host - e.g. dc01 or mail.example.com|Service - web, mail, ad, sql, rdp, ssh, voip, print, smb, vpn, dns - or ports 80,443", (a, r) => MoreTools.ManyPorts(M, a[0], a[1], a[3], r)),
                T("allips", "WEB", "Every IP behind a name", "name:port - e.g. outlook.office.com:443", (a, r) => MoreTools.AllIps(a[0], r)),
                T("rdpcheck", "WEB", "Remote Desktop reachable?", "PC - host or host:port (3389)", (a, r) => MoreTools.Rdp(a[0])),
                T("ldapcheck", "WEB", "Active Directory / LDAP reachable?", "Domain controller or domain (empty = your domain)", (a, r) => MoreTools.Ldap(a[0])),
                T("dnscmp", "WEB", "DNS answer compare", "Name - e.g. outlook.office.com|Other DNS - e.g. 1.1.1.1 or a DoH address (default 1.1.1.1)", (a, r) => MoreTools.DnsCompare(M, a[0], a[1])),
                T("beforeafter", "WEB", "Before / after snapshot", "Name this moment - e.g. VPN off", (a, r) => MoreTools.Snapshot(M, a[0])),
                T("twopc", "WEB", "Compare two saved results (two PCs)", "First saved result (.txt)|Second saved result (.txt)", (a, r) => MoreTools.TwoPc(a[0], a[1])),
                T("mysites", "WEB", "My sites", null, (a, r) => MoreTools.MySites(r)),
                T("shares", "YOUR NETWORK", "Shared folders on a PC or NAS (SMB)", "Host - e.g. nas01 or 192.168.1.20", (a, r) => MoreTools.Shares(a[0])),
                T("svcfail", "WINDOWS", "Services that failed to start (7 days)", null, (a, r) => MoreTools.FailedServices()),
                T("speedtest", "SPEED", "Speed test", "Server - empty = your usual, auto, a city (vultr toronto), a file URL - or list / check", (a, r) => SpeedTest.Run(a[0], r)),
                T("ispreport", "SPEED", "Report for my ISP", "A game you added, or a host / IP (empty = the game you play)|How long - 1, 2 or 5 minutes|Include my public IP? type yes (it asks Cloudflare once) - no by default", (a, r) => IspReport.Run(M, a[0], a[1], a[2], r)),
                T("tidy", "THE PC", "Quick tidy (you do it, I show where)", null, (a, r) => MoreTools.Tidy(M, r)),
                T("certfile", "CERTS", "Inspect a certificate file", "File path, or paste the PEM text", (a, r) => CertTools.Inspect(a[0])),
                T("pfx", "CERTS", "Check a PFX / P12", "File path|Password - used once, never kept", (a, r) => CertTools.Pfx(a[0], a[1])),
                T("csr", "CERTS", "Decode a CSR", "File path, or paste the PEM text", (a, r) => CertTools.Csr(a[0])),
                T("certconv", "CERTS", "Convert a certificate (PEM / DER)", "File path - the new file goes next to it", (a, r) => CertTools.ConvertCert(a[0])),
                T("hashf", "CERTS", "Hash a file and compare", "File path|Expected hash (optional) - SHA-256, SHA-1 or MD5", (a, r) => CertTools.Hash(a[0], a[1], r)),
                T("b64", "CERTS", "Decode Base64 or a JWT", "Paste the text", (a, r) => CertTools.Decode(a[0])),
                T("certcmds", "CERTS", "Certificate commands (certutil, openssl)", "File path (optional) - fills in the commands", (a, r) => CertTools.Commands(a[0])),
                T("radius", "PROTOCOLS", "RADIUS - is the server alive?", "Server - host or host:port (1812)|Shared secret", (a, r) => Protocols.Radius(a[0], a[1])),
                T("tacacs", "PROTOCOLS", "TACACS+ - is the server there?", "Server - host or host:port (49)", (a, r) => Protocols.Tacacs(a[0])),
                T("arp", "YOUR NETWORK", "Neighbours (ARP table)", null, (a, r) => RoutingTools.Neighbours(M)),
                T("hairpin", "YOUR NETWORK", "Your own router (hairpin)", null, (a, r) => Protocols.Hairpin()),
                T("sweep", "YOUR NETWORK", "Ping sweep", null, (a, r) => Toolkit.Sweep(M, a[3], r)),
                T("roams", "YOUR NETWORK", "Wi-Fi roams and drops (24 h)", null, (a, r) => Investigate.Roams(M)),
                T("dhcp", "YOUR NETWORK", "DHCP lease and IPv6 lifetimes", null, (a, r) => SysTools.Dhcp(M)),
                T("wol", "YOUR NETWORK", "Wake-on-LAN", "MAC - e.g. 3C:84:6A:12:34:56|Password (optional) - SecureOn, 6 bytes like a MAC", (a, r) => Toolkit.Wake(a[0], a[1])),
                T("subnet", "YOUR NETWORK", "Subnet calculator", "e.g. 10.1.2.3/22 or 2001:db8::1/64|Is this IP inside? (optional)", (a, r) => Toolkit.Subnet(a[0], a[1])),
                T("winev", "WINDOWS", "Windows' own evidence", null, (a, r) => WinEvidence.All(M)),
                T("auth", "WINDOWS", "Why network sign-in failed (802.1X)", null, (a, r) => WinEvidence.Auth()),
                T("dc", "WINDOWS", "Domain controllers", null, (a, r) => SysTools.DomainControllers(M)),
                T("gpo", "WINDOWS", "Group Policy - what applied", null, (a, r) => SysTools.GroupPolicy()),
                T("certs", "WINDOWS", "Certificates that expire soon", null, (a, r) => SysTools.Certs()),
                T("fw", "WINDOWS", "Firewall rules for an app", "App - e.g. teams or chrome.exe", (a, r) => SysTools.Firewall(a[0])),
                T("listen", "WINDOWS", "What's listening on this PC", null, (a, r) => SysTools.Listening(M)),
                T("events", "WINDOWS", "Event log - errors in 24 h", "Filter (optional) - e.g. DNS, Tcpip or an event ID", (a, r) => SysTools.EventSearch(a[0])),
                T("timesrc", "WINDOWS", "Where the PC gets its time", null, (a, r) => SysTools.TimeSource()),
                T("pressure", "THE PC", "Memory, disk, heat, drivers", null, (a, r) => PcHealth.Pressure(M)),
                T("disk", "THE PC", "Disk: who, health, space", null, (a, r) => PcHealth.Disk(M)),
                T("space", "THE PC", "Where did my space go?", null, (a, r) => PcHealth.Space(s => r.Status = s)),
                T("bg", "THE PC", "Background jobs right now", null, (a, r) => PcHealth.Background()),
                T("crash", "THE PC", "Crashes and hangs (7 days)", null, (a, r) => PcHealth.Crashes()),
                T("leaks", "THE PC", "Slow leaks", null, (a, r) => PcHealth.Leaks()),
                T("gpu", "THE PC", "Graphics card per app", null, (a, r) => PcHealth.Gpu()),
                T("changed", "THE PC", "What changed in 7 days", null, (a, r) => PcHealth.Changes()),
                T("battery", "THE PC", "Battery health", null, (a, r) => PcHealth.Battery()) };
            // grouped by the question you're asking, not by technology
            var pos = new Dictionary<string, int>();
            foreach (var grp in TbLayout) for (int i = 1; i < grp.Length; i++) pos[grp[i]] = pos.Count;
            foreach (var t in tools) { var grp = TbLayout.FirstOrDefault(l => Array.IndexOf(l, t.Id, 1) > 0); if (grp != null) t.Group = grp[0]; }
            tools = tools.OrderBy(t => { int p; return pos.TryGetValue(t.Id, out p) ? p : 999; }).ToList();
            return tools;
        }
        List<PathTools.Hop> mtrHops;
        // three sections, each grouped by the question you're asking, not by technology
        static readonly string[][] TbLayout = {
            new[] { "PATH & ROUTING", "mtu", "udpmtu", "mtr", "ret", "route", "gateway", "v6routers", "rlisten", "ping6", "ip46", "overlap", "owners" },
            new[] { "IS IT REACHABLE?", "port", "tcpping", "http", "httptime", "httpreq", "stun", "manyports", "allips", "rdpcheck", "ldapcheck", "proxy", "vpn", "teams", "ssh", "mail", "radius", "tacacs" },
            new[] { "SPEED & QUALITY", "speedtest", "ping", "twamp", "cloud", "ispreport" },
            new[] { "COMPARE & WATCH", "beforeafter", "twopc", "mysites" },
            new[] { "NAMES & TIME", "dig", "dnscmp", "doh", "hosts", "dnsrules", "ntp", "clock", "timesrc" },
            new[] { "SWITCHES & ROUTERS (SNMP)", "snmp", "snmpif", "snmptraffic", "snmpcpu", "snmplldp", "snmpsens", "snmpsnap", "snmpmac", "snmpvlan", "snmppoe", "snmplag", "snmpprn", "snmpups", "snmpwalk", "mibs" },
            new[] { "ON YOUR NETWORK", "arp", "finddev", "nbname", "shares", "discover", "dhcp", "roams", "sweep", "hairpin", "subnet" },
            new[] { "REMOTE ACCESS", "remote" },
            new[] { "BUILD A PACKET", "icmpb", "udpsend", "tcpsend", "arpwho", "dhcpopt", "wol" },
            new[] { "TEST A COLLECTOR", "syslog", "trap", "netflow" },
            new[] { "LOAD & QUEUES", "iperf", "bloat", "dscp" },
            new[] { "WINDOWS", "winev", "auth", "dc", "gpo", "certs", "fw", "listen", "events", "svcfail" },
            new[] { "CERTIFICATES & FILES", "certfile", "pfx", "csr", "certconv", "hashf", "b64", "certcmds" },
            new[] { "THE PC", "tidy", "pressure", "disk", "space", "bg", "crash", "leaks", "gpu", "changed", "battery" } };
        static readonly Dictionary<string, string> GroupSection = new Dictionary<string, string> {
            { "PATH & ROUTING", "net" }, { "IS IT REACHABLE?", "net" }, { "SPEED & QUALITY", "net" }, { "NAMES & TIME", "net" }, { "COMPARE & WATCH", "net" },
            { "SWITCHES & ROUTERS (SNMP)", "dev" }, { "ON YOUR NETWORK", "dev" }, { "REMOTE ACCESS", "dev" }, { "BUILD A PACKET", "lab" }, { "TEST A COLLECTOR", "lab" }, { "LOAD & QUEUES", "lab" }, { "CERTIFICATES & FILES", "pc" }, { "WINDOWS", "pc" }, { "THE PC", "health" } };   // the PC checks live in Investigate › Health
        static readonly string[][] Sections = { new[] { "net", "Network", "The path, reachability, speed, names and time - from this PC to anywhere." }, new[] { "dev", "Devices", "Switches and routers over SNMP, what else is on your network, and remote access." }, new[] { "lab", "Traffic Lab", "Test traffic you build yourself - to one target, small and capped (10 packets a second, 1,000 at most), never a flood or a scan." }, new[] { "pc", "Windows", "Windows' own evidence: sign-in, the domain, policy, certificates and files, firewall and the event log. The PC itself (memory, disk, crashes...) is in Investigate › Health." } };
        // the SNMP views are one tool with tabs: one device and one set of credentials, many questions
        static readonly string[][] SnmpTabs = { new[] { "snmp", "Overview" }, new[] { "snmpsnap", "Snapshot" }, new[] { "snmpif", "Interfaces" }, new[] { "snmptraffic", "Traffic" }, new[] { "snmpcpu", "CPU & memory" }, new[] { "snmplldp", "Neighbours" }, new[] { "snmpsens", "Sensors" }, new[] { "snmpmac", "Find a MAC" }, new[] { "snmpvlan", "VLANs" }, new[] { "snmppoe", "PoE" }, new[] { "snmplag", "LAG" }, new[] { "snmpprn", "Printer" }, new[] { "snmpups", "UPS" }, new[] { "snmpwalk", "Walk" }, new[] { "mibs", "MIBs" } };
        static readonly string[][] QuickTools = { new[] { "snmp", "SNMP" }, new[] { "snmpif", "Switch ports" }, new[] { "mtr", "MTR" }, new[] { "port", "Port test" }, new[] { "dig", "dig" }, new[] { "http", "HTTP & TLS" }, new[] { "iperf", "iperf3" }, new[] { "sweep", "Ping sweep" } };
        static readonly HashSet<string> LabGate = new HashSet<string> { "icmpb", "udpsend", "tcpsend", "arpwho", "dhcpopt", "trap", "netflow", "finddev", "manyports", "syslog" };   // need the tick on a company network
        // Traffic Lab tools that don't send packets they build (or that came with their own rules before the Lab) - the self-test fails if a new Lab tool is in neither list
        public static readonly HashSet<string> LabExempt = new HashSet<string> { "wol", "iperf", "bloat", "dscp" };
        public static IEnumerable<string> LabToolsMissingGate() { string sec; return TbLayout.Where(l => GroupSection.TryGetValue(l[0], out sec) && sec == "lab").SelectMany(l => l.Skip(1)).Where(id => !LabGate.Contains(id) && !LabExempt.Contains(id)).Concat(LabGate.Where(id => !TbLayout.Any(l => l.Skip(1).Contains(id)))); }
        static bool SecretInput(string label) { return label.StartsWith("Shared secret", StringComparison.Ordinal) || label.StartsWith("Community", StringComparison.Ordinal) || label.StartsWith("Password", StringComparison.Ordinal); }
        static bool SnmpView(string id) { return IsSnmp(id) || id == "mibs"; }
        string tbSection = "net"; bool tbHome = true; PillButton[] tbChips;
        static string SectionOf(Tool t) { string s; return GroupSection.TryGetValue(t.Group, out s) ? s : "net"; }
        static string SectionName(string s) { if (s == "health") return "Investigate › Health"; var x = Sections.FirstOrDefault(z => z[0] == s); return x != null ? x[1] : s; }
        // open a tool from the list, search, a card or Recent: PC checks run in Health, everything else here
        void OpenTool(string id)
        {
            var t = Tools2().FirstOrDefault(z => z.Id == id); if (t == null) return;
            if (SectionOf(t) == "health") { tab = "health"; ShowTab(); RunTool("tb:" + id, t.Title); return; }
            SelectTool(id);
        }
        void ShowSection(string s)
        {
            if (tbBusy) { if (tbRun0 != null) tbRun0.Status = "still running - press Stop first to change section"; tbCanvas.Invalidate(); return; }   // say why the click did nothing
            tbSection = s; tbHome = true; tbBar.Visible = false; foreach (var c in tbChips) c.Selected = (string)c.Tag == s;
            foreach (var t in Tools2().Where(x => SectionOf(x) == s)) tbOpen.Add(t.Group);   // short lists: every group starts open
            tbList.AutoScrollPosition = Point.Empty; tbCanvas.AutoScrollPosition = Point.Empty; tbList.Invalidate(); tbCanvas.Invalidate();
        }
        // tools whose first box is "the thing you're looking at" - they share one target, so you type it once
        static readonly HashSet<string> TargetTools = new HashSet<string> { "mtu", "mtr", "ret", "route", "ping6", "ip46", "owners", "port", "tcpping", "http", "httptime", "proxy", "ssh", "mail", "radius", "tacacs", "dig" };   // not ping or SNMP: those start at your gateway
        string tbTarget = "", tbFilter = ""; readonly List<string> tbRecent = new List<string>(); readonly HashSet<string> tbOpen = new HashSet<string>();
        RoundBox tbSearch; bool tbCompare;
        sealed class PrevRun { public string Inputs; public DateTime At; public List<string[]> Rows; public string[] Shown; }
        string[] tbShown;   // the inputs the result on screen was run with - Copy and Save use these, never what's in the boxes now
        const string SnmpIn = "Device - e.g. 10.0.0.1 or host:161|Community - or v3:user[:authpass[:privpass]]";
        string snmpDev = "", snmpCred = "";   // the device and credentials typed in one SNMP view carry to the next - this session only, never saved
        static bool IsSnmp(string id) { return id.StartsWith("snmp", StringComparison.Ordinal); }
        DateTime tbCopied;
        // a clickable result row: walk this object, or open the MIB folder
        void RowAction(string act)
        {
            if (tbBusy) return;
            if (act.StartsWith("walk:", StringComparison.Ordinal))
            {
                SelectTool("snmpwalk"); if (tbBoxes.Count > 2) tbBoxes[2].Box.Text = act.Substring(5);
                if (snmpDev != "" || tbBoxes[0].Box.Text != "") StartTool(); else tbBoxes[0].Box.Focus();
            }
            else if (act.StartsWith("revoke:", StringComparison.Ordinal))
            {
                // runs on press, then its rows replace the "not checked" row in the same result
                string hp = act.Substring(7); int c = hp.LastIndexOf(':'), port; if (c < 0 || !int.TryParse(hp.Substring(c + 1), out port)) return;
                string host = hp.Substring(0, c); var rows0 = tbRows; string sel = tbSel; if (rows0 == null) return;
                tbBusy = true; var run = new Toolkit.Run { Status = "Asking the certificate authority..." }; tbRun0 = run; tbRun.Text = "Stop"; tbCanvas.Invalidate();
                Task.Factory.StartNew(() =>
                {
                    List<string[]> got; try { got = Toolkit.Revocation(host, port); } catch (Exception e) { got = Bad(e.Message); }
                    try { BeginInvoke((Action)delegate { tbBusy = false; tbRun.Text = "Run"; if (tbSel == sel && tbRows == rows0) { var n = new List<string[]>(); foreach (var o in rows0) { if (o.Length > 5 && o[5] == act) n.AddRange(got); else n.Add(o); } tbRows = n; PrevRun l; if (tbLast.TryGetValue(sel, out l)) l.Rows = n; } tbCanvas.Invalidate(); }); } catch { tbBusy = false; }
                }, TaskCreationOptions.LongRunning);
            }
            else if (act.StartsWith("fix:", StringComparison.Ordinal)) SecFix(act.Substring(4));
            else if (act.StartsWith("copy:", StringComparison.Ordinal)) { try { Clipboard.SetText(act.Substring(5)); tbCopied = DateTime.Now; } catch (Exception ce) { Program.Log("copy: " + ce.Message); } tbCanvas.Invalidate(); }
            else if (act == "ps:")
            {
                // an empty PowerShell window - you paste the command; nothing is typed or run for you
                try { using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"), "-NoLogo -NoExit") { UseShellExecute = false, WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) })) { } }
                catch (Exception e) { Program.Log("open powershell: " + e.Message); }
            }
            else if (act.StartsWith("show:", StringComparison.Ordinal))
            {
                try { using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "/select,\"" + act.Substring(5) + "\"") { UseShellExecute = false })) { } }
                catch (Exception e) { Program.Log("show file: " + e.Message); }
            }
            else if (act.StartsWith("open:", StringComparison.Ordinal))
            {
                try
                {
                    string dir = act.Substring(5); System.IO.Directory.CreateDirectory(dir);
                    using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "\"" + dir + "\"") { UseShellExecute = false })) { }
                }
                catch (Exception e) { Program.Log("open mib folder: " + e.Message); }
            }
        }
        // Save: only where you choose, only when you press it - .txt as Copy gives it, or .csv for a spreadsheet
        void SaveResult()
        {
            var t = Tools2().First(x => x.Id == tbSel); bool csvOk = tbRows != null;
            using (var d = new SaveFileDialog { Title = "Save the result", Filter = csvOk ? "Text (*.txt)|*.txt|CSV for Excel (*.csv)|*.csv" : "Text (*.txt)|*.txt", FileName = "AdeeboMeter-" + t.Id + "-" + tbAt.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture), InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), OverwritePrompt = true, AddExtension = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    bool csv = csvOk && d.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
                    System.IO.File.WriteAllText(d.FileName, csv ? ResultCsv() : ResultText(), new System.Text.UTF8Encoding(true));
                    tbSaved = DateTime.Now; tbCanvas.Invalidate();
                }
                catch (Exception e) { Program.Log("save result: " + e.Message); MessageBox.Show(this, "Couldn't save: " + e.Message, "AdeeboMeter"); }
            }
        }
        DateTime tbSaved;
        string ResultCsv()
        {
            Func<string, string> q = v => { v = v ?? ""; var lead = v.TrimStart(' ', '\t', '\r', '\n'); if (lead.Length > 0 && "=+-@".IndexOf(lead[0]) >= 0 || v.Length > 0 && v[0] == '\t') v = "'" + v; return "\"" + v.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\""; };   // a leading = + - @ can't become a formula
            var sb = new System.Text.StringBuilder(); sb.AppendLine("Level,Item,Value,Notes");
            foreach (var o in tbRows)
            {
                int lv; int.TryParse(o[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv);
                string word = lv == 2 || lv == 7 ? "problem" : lv == 1 ? "warning" : lv == 0 ? "ok" : "info";
                sb.Append(q(word)).Append(',').Append(q(o.Length > 1 ? o[1] : "")).Append(',').Append(q(o.Length > 2 ? o[2] : "")).Append(',').Append(q(o.Length > 3 ? o[3] : "")).AppendLine();
            }
            return sb.ToString();
        }
        // the result as plain text, for a ticket or a chat - built only when you press Copy; nothing is saved
        string ResultText()
        {
            var sb = new System.Text.StringBuilder(); var t = Tools2().First(x => x.Id == tbSel);
            sb.AppendLine("AdeeboMeter " + Program.Version + " · " + t.Title + " · " + tbAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            var a = tbShown ?? Inputs(); for (int i = 0; i < t.Inputs.Length && i < 4; i++) if (t.Inputs[i] != "" && a[i] != "" && !t.Inputs[i].StartsWith("Shared secret", StringComparison.Ordinal) && !t.Inputs[i].StartsWith("Password", StringComparison.Ordinal) && !t.Inputs[i].StartsWith("Community", StringComparison.Ordinal)) sb.AppendLine(t.Inputs[i].Split('-')[0].Trim() + ": " + a[i]);
            sb.AppendLine();
            if (tbSel == "mtu" && mtuHops != null) foreach (var h in mtuHops) sb.AppendLine(("hop " + h.N).PadRight(8) + (h.Ip == "" ? "*" : h.Ip).PadRight(40) + (h.Mtu > 0 ? h.Mtu + "  " + h.MtuNote : h.MtuNote));
            if (tbSel == "mtr" && mtrHops != null)
            {
                sb.AppendLine("hop     router                                   loss  sent  avg   best  worst  jitter");
                foreach (var h in mtrHops) { double loss = h.Sent > 0 ? 100.0 * h.Lost / h.Sent : 0, avg = h.Sent - h.Lost > 0 ? h.Sum / (h.Sent - h.Lost) : double.NaN; sb.AppendLine(h.N.ToString(CultureInfo.InvariantCulture).PadRight(8) + (h.Ip == "" ? "*" : h.Ip).PadRight(40) + (loss.ToString("0", CultureInfo.InvariantCulture) + "%").PadLeft(5) + h.Sent.ToString(CultureInfo.InvariantCulture).PadLeft(6) + PathTools.F(avg).PadLeft(6) + PathTools.F(h.Best).PadLeft(6) + PathTools.F(h.Worst).PadLeft(7) + PathTools.F(h.Jit).PadLeft(8)); }
                sb.AppendLine();
            }
            if (tbRows != null)
                foreach (var o in tbRows)
                {
                    if (o[0] == "6" || o[0] == "7" || o[0] == "8") { sb.AppendLine((o[0] == "7" ? "! " : "  ") + o[2]); continue; }   // table lines, as they are
                    string lv = o[0] == "2" ? "[!!] " : o[0] == "1" ? "[!]  " : o[0] == "0" ? "[ok] " : "     ";
                    sb.AppendLine(lv + o[1] + ": " + o[2]);
                    if (o.Length > 3 && o[3] != "") sb.AppendLine("      " + o[3]);
                }
            return sb.ToString();
        }
        readonly Dictionary<string, PrevRun> tbPrev = new Dictionary<string, PrevRun>(), tbLast = new Dictionary<string, PrevRun>();   // the run before, per tool - this session only, never saved
        static List<string[]> OnlineOff() { var r = new List<string[]>(); r.Add(Row5("3", "Online lookups", "off - this needs them (Settings > Online lookups, or the link below)", "", "u")); return r; }
        static string[] Row5(params string[] a) { return a; }
        static List<string[]> Bad(string msg) { return new List<string[]> { new[] { "2", "Input", msg, "", "u" } }; }
        static string Host(string s) { s = (s ?? "").Trim(); Uri u; if (s.StartsWith("http", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(s, UriKind.Absolute, out u)) return u.Host; int c = s.IndexOf('/'); return c > 0 ? s.Substring(0, c) : s; }

        // the plain iperf3 controls become iperf3 options; anything typed under Advanced comes last
        static string IperfOpts(string[] a)
        {
            string adv = " " + (a[3] ?? "").Trim() + " ", al = adv.ToLowerInvariant();
            // -t20, -t 20 and --time=20 all count (iperf3 accepts each)
            bool advDir = System.Text.RegularExpressions.Regex.IsMatch(al, @"\s(-r|--reverse|--both)(\s|$)"), advTime = System.Text.RegularExpressions.Regex.IsMatch(al, @"\s(-t|--time)(\s|=|\d)");
            string d = (a[1] ?? "").Trim().ToLowerInvariant(), o;
            if (advDir) o = "";
            else if (d.StartsWith("down", StringComparison.Ordinal) || d == "-r") o = "-R";
            else if (d.StartsWith("up", StringComparison.Ordinal) || d == "" && adv.Trim() != "") o = "";
            else if (d == "" || d == "both" || d == "--both") o = "--both";
            else return null;   // a word we don't know - say so rather than guess a direction
            int sec; if (!advTime && int.TryParse(a[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out sec)) o += " -t " + Math.Max(1, Math.Min(60, sec));
            return (o + adv).Trim();
        }
        // how long a run takes, said before Run is pressed
        static string Duration(string id, string[] a)
        {
            int n, m;
            switch (id)
            {
                case "mtu": return "a few seconds to trace, then about 8 small pings per hop you measure";
                case "mtr": if (!int.TryParse(a[1], out n)) n = 1; n = Math.Max(1, Math.Min(10, n)); return n + (n == 1 ? " minute" : " minutes") + ", after 5-30 seconds to find the path";
                case "rlisten": case "discover": if (!int.TryParse(a[0], out n)) n = 60; return Math.Max(10, Math.Min(120, n)) + " seconds";
                case "ping": if (!int.TryParse(a[1], out n)) n = 50; if (!int.TryParse(a[2], out m)) m = 200; n = Math.Max(10, Math.Min(2000, n)); m = Math.Max(20, Math.Min(5000, m)); return RunSecs(n * m / 1000) + " if it answers - up to " + RunSecs(n * Math.Max(m, 1000) / 1000) + " if it doesn't";
                case "tcpping": if (!int.TryParse(a[1], out n)) n = 20; return RunSecs(Math.Max(1, Math.Min(500, n)));
                case "httptime": if (!int.TryParse(a[1], out n)) n = 5; return "about " + RunSecs(Math.Max(1, Math.Min(50, n))) + ", longer for a slow site";
                case "iperf":
                    {
                        // from the options that will really run: both directions are two tests, each its seconds plus any -O
                        var o = IperfOpts(a); if (o == null) return "- first type both, upload or download as the direction";
                        var po = Toolkit.ParseOpts(o);   // the same reading the run uses - attached -O3 / -t20 included
                        return "about " + RunSecs((po.Both ? 2 : 1) * (po.Secs + po.Omit) + 2);
                    }
                case "sweep": return "about 20 seconds, never more than 90";
                case "bloat": return "about 30 seconds (idle, 10 s download, 10 s upload)";
                case "vpn": return "a few seconds - 7 pings to the VPN's own DNS server";
                case "udpmtu": return "about 10-20 seconds · about 12 sizes, two small tries each, to the one target";
                case "speedtest": return a.Length > 0 && (a[0].Trim() == "list") ? "instant - nothing is sent" : a.Length > 0 && a[0].Trim() == "check" ? "about a minute - 1 KB from each server" : "about 20 seconds · up to " + SpeedTest.MaxDownMB + " MB down and " + SpeedTest.MaxUpMB + " MB up - nothing runs until you press Run";
                case "ispreport": return (int.TryParse(a.Length > 1 ? a[1].Trim() : "", out m) && (m == 1 || m == 5) ? m : 2) + " min of measuring, plus about 30 seconds to trace and name the routers";
                case "dscp": if (!int.TryParse(a[2], out n)) n = 20; return RunSecs(Math.Max(1, Math.Min(200, n)) / 2 + 1);
                case "snmptraffic": return "about 10 seconds (two readings 5 seconds apart)";
                case "snmpwalk": return "a second to a minute - stops at 2,000 values";
                case "snmpsnap": return "up to 3 minutes on a big switch";
                case "finddev": return "about 3 seconds";
                case "mysites": return "a few seconds per pinned site";
                case "tidy": return "up to a minute with a big temp folder";
                case "manyports": return "up to 2 seconds per port";
                case "allips": return "up to 2 seconds per address";
                case "rdpcheck": case "ldapcheck": case "nbname": return "a few seconds";
                case "svcfail": return "a few seconds";
                case "icmpb": { if (!int.TryParse(a[1], out n)) n = 10; n = Math.Max(1, Math.Min(1000, n)); var mg = System.Text.RegularExpressions.Regex.Match(a[2] ?? "", @"(?:every|gap)\s*=?\s*(\d{1,5})"); int gp = mg.Success ? Math.Max(100, int.Parse(mg.Groups[1].Value, CultureInfo.InvariantCulture)) : 1000; return RunSecs(n * gp / 1000 + 1); }
                case "udpsend": if (!int.TryParse(a[2], out n)) n = 1; return RunSecs(Math.Max(1, Math.Min(1000, n)) / 10 + 1);
                case "cloud": case "owners": case "ret": return "about 20-40 seconds";
                case "ping6": return "about 10-30 seconds";
                case "space": return "up to a minute on a big disk";
                case "teams": return "about 10 seconds";
                case "mibs": return "a moment - the first search after you add MIB files reads them once (a minute or two)";
                default: return "a few seconds";
            }
        }
        static readonly char[] LabelStops = { ':', '(' };
        static string RunSecs(int s) { return s < 90 ? s + " seconds" : (s + 30) / 60 + " minutes"; }
        // words people type for a tool that its own text doesn't use
        // everyday words -> the tools they mean, best first (ids, not more words - so "wifi" finds the Wi-Fi tool, not every page that says "speed")
        static readonly Dictionary<string, string> Synonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "wifi", "roams dhcp arp" }, { "wi-fi", "roams dhcp arp" }, { "wireless", "roams" }, { "roaming", "roams" }, { "slow", "bloat iperf ping mtr cloud" }, { "speed", "iperf bloat cloud" },
            { "bandwidth", "iperf bloat" }, { "throughput", "iperf" }, { "lag", "ping bloat mtr snmplag" }, { "lacp", "snmplag" }, { "etherchannel", "snmplag" }, { "portchannel", "snmplag" }, { "bond", "snmplag" }, { "vlan", "snmpvlan" }, { "trunk", "snmpvlan snmplag" }, { "poe", "snmppoe" }, { "mac", "snmpmac arp" }, { "toner", "snmpprn" }, { "ups", "snmpups" }, { "snapshot", "snmpsnap beforeafter" }, { "latency", "ping tcpping bloat mtr" }, { "jitter", "ping twamp" }, { "drops", "ping mtr roams" }, { "loss", "ping mtr twamp" },
            { "traceroute", "mtr mtu ret" }, { "tracert", "mtr mtu ret" }, { "trace", "mtr mtu ret" }, { "pathping", "mtr" }, { "nslookup", "dig" }, { "dns", "dig doh dnsrules hosts dnscmp" },
            { "cert", "http certs certfile" }, { "certificate", "http certs certfile pfx csr" }, { "ssl", "http certs" }, { "https", "http httptime" }, { "web", "http httptime proxy" },
            { "switch", "snmpif snmplldp snmp" }, { "router", "gateway snmp hairpin" }, { "printer", "sweep snmpprn arp discover" }, { "devices", "sweep arp discover snmp" }, { "scan", "sweep" },
            { "ip", "subnet arp dhcp allips" }, { "lease", "dhcp" }, { "vpn", "vpn overlap route proxy dnsrules" }, { "email", "mail" }, { "smtp", "mail" }, { "outlook", "mail http proxy" },
            { "time", "ntp clock timesrc" }, { "temperature", "snmpsens" }, { "fan", "snmpsens mibs" }, { "optic", "snmpsens mibs" }, { "sfp", "snmpsens mibs" }, { "interface", "snmpif" }, { "port", "port snmpif tcpping" },
            { "bgp", "owners" }, { "ospf", "rlisten" }, { "vrrp", "gateway rlisten" }, { "hsrp", "gateway rlisten" }, { "stp", "rlisten" }, { "lldp", "snmplldp rlisten" }, { "mdns", "discover finddev" }, { "bonjour", "discover finddev" }, { "upnp", "discover finddev" }, { "llmnr", "discover nbname" },
            { "qos", "dscp" }, { "dscp", "dscp" }, { "bufferbloat", "bloat" }, { "storage", "space disk" }, { "crash", "crash" }, { "freeze", "crash bg" }, { "login", "auth radius tacacs" }, { "8021x", "auth" }, { "802.1x", "auth" }, { "packet", "icmpb udpsend tcpsend" }, { "craft", "icmpb udpsend tcpsend" }, { "udp", "udpsend" }, { "tcp", "tcpsend" }, { "icmp", "icmpb" }, { "df", "icmpb mtu" }, { "ttl", "icmpb" }, { "trap", "trap" }, { "netflow", "netflow" }, { "flow", "netflow" }, { "ipfix", "netflow" }, { "dhcp", "dhcpopt" }, { "option", "dhcpopt" }, { "stun", "stun" }, { "nat", "stun teams" }, { "public", "stun" }, { "curl", "httpreq remote" }, { "api", "httpreq" }, { "rest", "httpreq" }, { "post", "httpreq" }, { "netbios", "nbname" }, { "hostname", "nbname" }, { "name", "nbname" }, { "discover", "finddev" }, { "ssdp", "finddev" }, { "wsd", "finddev" }, { "ssh", "remote" }, { "scp", "remote" }, { "sftp", "remote" }, { "putty", "remote" }, { "telnet", "remote" }, { "pfx", "pfx" }, { "p12", "pfx" }, { "csr", "csr" }, { "pem", "certfile certconv" }, { "der", "certconv certfile" }, { "hash", "hashf" }, { "sha256", "hashf" }, { "checksum", "hashf" }, { "md5", "hashf" }, { "jwt", "b64" }, { "base64", "b64" }, { "token", "b64" }, { "openssl", "certcmds" }, { "certutil", "certcmds" }, { "syslog", "syslog" }, { "wake", "wol" }, { "iperf", "iperf" }, { "rdp", "rdpcheck remote" }, { "ldap", "ldapcheck" }, { "ad", "ldapcheck dc manyports" }, { "kerberos", "ldapcheck" }, { "smb", "shares manyports" }, { "share", "shares" }, { "nas", "shares" }, { "service", "svcfail" }, { "services", "svcfail" }, { "clean", "tidy" }, { "cleanup", "tidy" }, { "temp", "tidy" }, { "junk", "tidy" }, { "recycle", "tidy" }, { "ports", "manyports" }, { "compare", "beforeafter twopc dnscmp" }, { "before", "beforeafter" }, { "after", "beforeafter" }, { "sites", "mysites" }, { "watchlist", "mysites" }, { "arp", "arpwho" },
        };
        Panel BuildToolboxPage()
        {
            toolboxPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            return toolboxPage;
        }
        // built the first time it's shown, so opening the engineer view stays fast
        void EnsureToolbox()
        {
            if (tbCanvas != null) return;
            var osw = System.Diagnostics.Stopwatch.StartNew(); try { EnsureToolbox2(); } finally { Program.Log("toolbox opened in " + osw.ElapsedMilliseconds + " ms"); }
        }
        void EnsureToolbox2()
        {
            toolboxPage.SuspendLayout();
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Card };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Sc(290))); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tbList = new DB { Name = "tbList", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            tbList.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintTbList(e.Graphics); }; Wire(tbList);
            // the list works from the keyboard too: arrows move, Enter goes to the first box, Home/End, typing jumps to search
            tbList.KeyNav = true; tbList.AccessibleName = "Tools"; tbList.AccessibleRole = AccessibleRole.List;
            tbList.GotFocus += delegate { tbList.Invalidate(); }; tbList.LostFocus += delegate { tbList.Invalidate(); };
            tbList.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Down) { e.Handled = true; MoveTool(1); }
                else if (e.KeyCode == Keys.Up) { e.Handled = true; if (tbVisible.IndexOf(tbSel) <= 0) tbSearch.Box.Focus(); else MoveTool(-1); }
                else if (e.KeyCode == Keys.PageDown) { e.Handled = true; MoveTool(8); }
                else if (e.KeyCode == Keys.PageUp) { e.Handled = true; MoveTool(-8); }
                else if (e.KeyCode == Keys.Home) { e.Handled = true; MoveTool(-9999); }
                else if (e.KeyCode == Keys.End) { e.Handled = true; MoveTool(9999); }
                else if (e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; var b0 = tbBoxes.FirstOrDefault(b => b != null); if (b0 != null) b0.Box.Focus(); else if (!tbBusy) StartTool(); }
            };
            tbList.KeyPress += delegate(object s, KeyPressEventArgs e) { if (!char.IsControl(e.KeyChar)) { e.Handled = true; tbSearch.Box.Focus(); tbSearch.Box.Text += e.KeyChar; tbSearch.Box.SelectionStart = tbSearch.Box.TextLength; } };
            var left = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Margin = new Padding(0) };
            // top bar: Network · Devices · This PC, and one search across all of them
            var topBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Card, Padding = new Padding(Sc(10), Sc(10), Sc(10), Sc(6)), WrapContents = true };
            tbChips = Sections.Select(sec => { var c = new PillButton(sec[1], PillButton.K.Chip) { Tag = sec[0], Margin = new Padding(0, Sc(3), Sc(8), 0) }; string id = sec[0]; c.Click += delegate { tbSearch.Box.Text = ""; ShowSection(id); }; return c; }).ToArray();
            foreach (var c in tbChips) topBar.Controls.Add(c);
            tbSearch = new RoundBox(Sc(320), Draw.fInput, "Find any tool (mtu, dns, wifi, slow...)") { Margin = new Padding(Sc(16), 0, 0, 0) };
            tbSearch.Box.TextChanged += delegate { tbFilter = tbSearch.Box.Text.Trim(); tbList.AutoScrollPosition = Point.Empty; tbList.Invalidate(); };
            tbSearch.Box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; var first = Matches().FirstOrDefault(); if (first != null) { OpenTool(first.Id); if (tab != "toolbox") return; var b0 = tbBoxes.FirstOrDefault(b => b != null); if (b0 != null) b0.Box.Focus(); } }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; tbSearch.Box.Text = ""; }
                else if (e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; tbList.Focus(); MoveTool(tbVisible.IndexOf(tbSel) < 0 ? 0 : 1); }
            };
            topBar.Controls.Add(tbSearch); topBar.SetFlowBreak(tbSearch, true);
            // one click to the tools people reach for most - from any section
            topBar.Controls.Add(new Label { Text = "QUICK", AutoSize = true, Font = Draw.fTiny, ForeColor = Theme.Muted, BackColor = Theme.Card, Margin = new Padding(Sc(2), Sc(13), Sc(8), 0) });
            foreach (var q in QuickTools)
            {
                var qb = new PillButton(q[1], PillButton.K.Chip) { Tag = q[0], Margin = new Padding(0, Sc(6), Sc(8), 0) };
                qb.Click += delegate(object s2, EventArgs e2) { tbSearch.Box.Text = ""; OpenTool((string)((Control)s2).Tag); };
                topBar.Controls.Add(qb);
            }
            left.Controls.Add(tbList);
            var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Margin = new Padding(0) };
            tbBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(0, Sc(60)), BackColor = Theme.Card, Padding = new Padding(Sc(10), Sc(12), Sc(8), Sc(6)), WrapContents = true };
            tbRun = new PillButton("Run", PillButton.K.Primary); tbRun.Click += delegate { if (tbBusy) { if (tbRun0 != null) tbRun0.Stop = true; } else StartTool(); };
            for (int i = 0; i < tbPool.Length; i++)
            {
                var rb = new RoundBox(Sc(200), Draw.fInput, "") { Visible = false, Label = " " }; tbPool[i] = rb;
                rb.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (!tbBusy) StartTool(); } };
                rb.Box.TextChanged += delegate { if (tbRows == null && !tbBusy) tbCanvas.Invalidate(); };   // the expected duration follows the inputs
                tbBar.Controls.Add(rb);
            }
            // a sweep on a company network needs a deliberate tick - not a typed word
            tbSweepOk = new CheckBox { Text = "This is my own network, or I'm allowed to scan it", AutoSize = true, Visible = false, FlatStyle = FlatStyle.Flat, Font = Draw.fBody, ForeColor = Theme.Ink, BackColor = Theme.Card, Margin = new Padding(0, Sc(24), Sc(14), 0), UseMnemonic = false };
            tbBar.Controls.Add(tbSweepOk);
            tbRun.Margin = new Padding(0, Sc(18), 0, 0);
            tbBar.Controls.Add(tbRun);
            tbCanvas = new DB { Name = "tbCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            tbCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintTb(e.Graphics); }; Wire(tbCanvas);
            right.Controls.Add(tbCanvas); right.Controls.Add(tbBar);
            split.Controls.Add(left, 0, 0); split.Controls.Add(right, 1, 0);
            toolboxPage.Controls.Add(split); toolboxPage.Controls.Add(topBar);
            toolboxPage.ResumeLayout(true);
            SelectTool(tbSel); ShowSection("net"); ThemeScrollbars(toolboxPage);
        }
        void SelectTool(string id)
        {
            EnsureToolbox(); if (tbBusy) return;
            if (SnmpView(tbSel) && SnmpView(id) && tbBoxes.Count > 1 && IsSnmp(tbSel)) { snmpDev = tbBoxes[0].Box.Text.Trim(); snmpCred = tbBoxes[1].Box.Text.Trim(); }   // switching SNMP tabs keeps the device you typed
            var t = Tools2().FirstOrDefault(x => x.Id == id) ?? Tools2()[0];
            tbHome = false; tbBar.Visible = true; if (tbSection != SectionOf(t) && SectionOf(t) != "health") { tbSection = SectionOf(t); foreach (var c in tbChips) c.Selected = (string)c.Tag == tbSection; foreach (var o in Tools2().Where(z => SectionOf(z) == tbSection)) tbOpen.Add(o.Group); } tbSel = t.Id; tbRows = null; tbRun0 = null; tbTitle = t.Title; tbRowsKey = "";
            tbShown = null; PrevRun last; if (tbLast.TryGetValue(t.Id, out last)) { tbRows = last.Rows; tbAt = last.At; tbRowsKey = last.Inputs; tbShown = last.Shown; }   // coming back shows the last result
            // three input boxes made once and reused - switching tools creates no new windows
            tbBar.SuspendLayout(); tbBoxes.Clear();
            for (int i = 0; i < tbPool.Length; i++)
            {
                var rb = tbPool[i]; string inp = i < t.Inputs.Length ? t.Inputs[i] : "";
                rb.Box.Text = "";
                if (inp == "") { rb.Visible = false; if (i < t.Inputs.Length) tbBoxes.Add(null); continue; }
                bool secret = SecretInput(inp);   // never shown, never saved, never logged
                if (rb.Box.UseSystemPasswordChar != secret) rb.Box.UseSystemPasswordChar = secret;
                // the name stays above the box while you type; the example stays inside until you do
                int dash = inp.IndexOf(" - ", StringComparison.Ordinal); string lab = dash > 0 ? inp.Substring(0, dash) : inp, cue = dash > 0 ? inp.Substring(dash + 3) : "";
                if (cue == "" && lab.Length > 34) { cue = lab; int sp = lab.IndexOfAny(LabelStops); lab = sp > 0 ? lab.Substring(0, sp).Trim() : "Input"; }
                rb.Label = lab; rb.Cue = cue; rb.Tag = inp; rb.Box.AccessibleName = lab;
                rb.Width = Math.Min(Sc(380), Math.Max(Sc(180), Math.Max(TextRenderer.MeasureText(cue, Draw.fInput).Width + Sc(34), TextRenderer.MeasureText(lab, Draw.fSmall).Width + Sc(10))));   // wide enough to show the whole hint
                rb.Visible = true; tbBoxes.Add(rb);
            }
            if (tbTarget == "" && destBox.Box.Text.Trim() != "") tbTarget = destBox.Box.Text.Trim();
            tbOpen.Add(t.Group);
            if (t.Id == "iperf" && tbBoxes.Count > 2) { tbBoxes[1].Box.Text = "both"; tbBoxes[2].Box.Text = "10"; }
            tbSweepOk.Visible = t.Id == "sweep" || LabGate.Contains(t.Id); tbSweepOk.Checked = false; tbSweepOk.Text = t.Id == "sweep" ? "This is my own network, or I'm allowed to scan it" : "This is my own network, or I'm allowed to send test traffic to it";
            if (t.Id == "ping" && tbBoxes.Count > 2) { string gw; lock (M.L) gw = M.Gateway; tbBoxes[0].Box.Text = gw != "" ? gw : "1.1.1.1"; tbBoxes[1].Box.Text = "50"; tbBoxes[2].Box.Text = "200"; }   // 10 seconds; type more for longer
            if (t.Id == "mtr" && tbBoxes.Count > 1) tbBoxes[1].Box.Text = "1";
            if (t.Id == "dig" && tbBoxes.Count > 1) tbBoxes[1].Box.Text = "A";
            if (t.Id == "tcpping" && tbBoxes.Count > 1) tbBoxes[1].Box.Text = "20";
            if (t.Id == "dscp" && tbBoxes.Count > 2) { string gw; lock (M.L) gw = M.Gateway; tbBoxes[0].Box.Text = gw; tbBoxes[1].Box.Text = "EF"; tbBoxes[2].Box.Text = "20"; }
            if ((t.Id == "iperf" || t.Id == "bloat") && tbBoxes.Count > 0 && tbIperfSrv != "") tbBoxes[0].Box.Text = tbIperfSrv;
            if (t.Id == "httptime" && tbBoxes.Count > 1) tbBoxes[1].Box.Text = "5";
            if (t.Id == "twamp" && tbBoxes.Count > 1) tbBoxes[1].Box.Text = "100";
            if (t.Id == "ntp" && tbBoxes.Count > 0) tbBoxes[0].Box.Text = "time.windows.com";
            if ((t.Id == "snmp" || t.Id == "ssh" || t.Id == "syslog") && tbBoxes.Count > 0) { string gw; lock (M.L) gw = M.Gateway; tbBoxes[0].Box.Text = gw; }
            if ((t.Id == "rlisten" || t.Id == "discover") && tbBoxes.Count > 0) tbBoxes[0].Box.Text = "60";
            if (IsSnmp(t.Id) && tbBoxes.Count > 1) { string gw; lock (M.L) gw = M.Gateway; tbBoxes[0].Box.Text = snmpDev != "" ? snmpDev : gw; tbBoxes[1].Box.Text = snmpCred; }
            if (TargetTools.Contains(t.Id) && tbTarget != "" && tbBoxes.Count > 0 && tbBoxes[0] != null) tbBoxes[0].Box.Text = tbTarget;   // the shared target wins over a tool's own default
            tbRun.Text = "Run";
            tbBar.ResumeLayout();
            if (t.Id != "mtu") { mtuHops = null; }
            tbList.Refresh(); tbCanvas.Invalidate();
            float ry; string rid = SnmpView(t.Id) ? "snmp" : t.Id;   // opened from a card, search or Recent: bring its row into view
            if (tbRowY.TryGetValue(rid, out ry)) { int top = -tbList.AutoScrollPosition.Y, h = tbList.ClientSize.Height; if (ry < top || ry + 46 * S > top + h) { tbList.AutoScrollPosition = new Point(0, Math.Max(0, (int)ry - Sc(40))); tbList.Invalidate(); } }
        }
        string[] Inputs() { var a = new string[4]; for (int i = 0; i < 4; i++) a[i] = i < tbBoxes.Count && tbBoxes[i] != null ? tbBoxes[i].Box.Text.Trim() : ""; return a; }

        void StartTool()
        {
            var t = Tools2().First(x => x.Id == tbSel); var a = Inputs(); UiActivity.Doing = "starting tool " + t.Id;
            tbRecent.Remove(t.Id); tbRecent.Insert(0, t.Id); if (tbRecent.Count > 5) tbRecent.RemoveAt(5);
            if (TargetTools.Contains(t.Id) && a[0] != "") tbTarget = a[0];
            if (t.Id == "mtu") { StartMtu(a[0]); return; }
            if (IsSnmp(t.Id)) { snmpDev = a[0]; snmpCred = a[1]; }
            if (t.Id == "sweep" || LabGate.Contains(t.Id)) a[3] = tbSweepOk.Checked ? "yes" : "";
            if ((t.Id == "iperf" || t.Id == "bloat") && a[0] != "") tbIperfSrv = a[0];
            // what's kept after the run (Copy, Save, Compare, coming back to the tool) never holds a secret: secret boxes are blanked,
            // HTTP sign-in headers hidden and a request body left out
            var shown = (string[])a.Clone();
            for (int i = 0; i < t.Inputs.Length && i < shown.Length; i++) if (SecretInput(t.Inputs[i])) shown[i] = "";
            if (t.Id == "httpreq") { shown[2] = Regex.Replace(shown[2] ?? "", @"(?i)\b(authorization|proxy-authorization|cookie|x-api-key|api-key|x-auth-token|x-amz-security-token)\s*:[^;]*", "$1: (hidden)"); if (!string.IsNullOrEmpty(shown[3])) shown[3] = "(" + shown[3].Length + " characters - not kept)"; }
            string key = string.Join("|", IsSnmp(t.Id) ? new[] { shown[0], shown[2] } : shown); var run = new Toolkit.Run(); tbRun0 = run; tbRows = null; tbRowsKey = ""; tbBusy = true; tbRun.Text = "Stop"; tbCanvas.AutoScrollPosition = Point.Empty; tbCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            {
                List<string[]> res;
                try { res = t.Do(a, run); } catch (Exception e) { Program.Log("tool " + t.Id + ": " + e); res = new List<string[]> { new[] { "2", "Couldn't finish", Draw.Trunc(e.Message, 100), "", "u" } }; }
                if (IsSnmp(t.Id)) { var part = SnmpTools.TakePartial(); if (part != null && res != null) res.Insert(0, new[] { "1", part.StartsWith("stopped by you", StringComparison.Ordinal) ? "Stopped - partial result" : "Partial result", part, "The device stopped answering part-way: anything missing below may simply not have arrived - run it again before trusting a gap.", "m" }); }
                try { BeginInvoke((Action)delegate { tbRows = res; tbRowsKey = key; tbAt = DateTime.Now; tbShown = shown; PrevRun was; if (tbLast.TryGetValue(t.Id, out was)) tbPrev[t.Id] = was; tbLast[t.Id] = new PrevRun { Inputs = key, At = tbAt, Rows = res, Shown = shown }; tbBusy = false; tbRun.Text = "Run"; tbCanvas.Invalidate(); tbList.Invalidate(); }); } catch { tbBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }

        // ---------- MTU per hop: trace, then measure hop by hop (Next hop / a row / all)
        void StartMtu(string target)
        {
            tbShown = new[] { target, "", "", "" };
            if (mtuBusy) return; mtuBusy = true; tbBusy = true; tbRun.Text = "Stop"; tbRows = null; mtuHops = null; var run = new Toolkit.Run(); tbRun0 = run; run.Status = "Tracing to " + target + "..."; tbCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            {
                var dst = Icmp.Resolve4(target); List<PathTools.Hop> hops = null;
                if (dst != null) hops = PathTools.Trace(dst, 30, () => run.Stop);
                try { BeginInvoke((Action)delegate { tbAt = DateTime.Now; mtuDst = dst; mtuHops = hops; mtuBusy = false; tbBusy = false; tbRun.Text = "Run"; if (dst == null) tbRows = Bad("couldn't find " + target + " (IPv4)"); tbCanvas.Invalidate(); }); } catch { mtuBusy = false; tbBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }
        void MeasureHops(List<PathTools.Hop> which)
        {
            if (mtuBusy || mtuHops == null || mtuDst == null || which.Count == 0) return;
            mtuBusy = true; var run = new Toolkit.Run(); tbRun0 = run; tbBusy = true; tbRun.Text = "Stop"; tbCanvas.Invalidate();
            var hops = mtuHops; var dst = mtuDst;
            Task.Factory.StartNew(() =>
            {
                foreach (var h in which)
                {
                    if (run.Stop) break;
                    run.Status = "Measuring hop " + h.N + "...";
                    int local; lock (M.L) local = M.PrimaryMtu; if (local < 576) local = 1500;
                    int hi = hops.Where(x => x.N < h.N && x.Mtu > 0).Select(x => x.Mtu).DefaultIfEmpty(Math.Min(9216, local)).Min();   // starts at your adapter's own MTU (jumbo if set), then can only stay or shrink
                    try { PathTools.MtuAt(dst, h, hi); } catch (Exception e) { Program.Log("mtu: " + e.Message); }
                    try { BeginInvoke((Action)tbCanvas.Invalidate); } catch { }
                }
                try { BeginInvoke((Action)delegate { mtuBusy = false; tbBusy = false; tbRun.Text = "Run"; tbCanvas.Invalidate(); }); } catch { mtuBusy = false; tbBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }

        // ---------- painting
        // search: every word must appear in the tool's name, its group or its explanation
        // search: every word must match somewhere; the results are ranked - the tool's own name first, then the everyday word's tools, then explanations
        List<Tool> Matches()
        {
            var words = tbFilter.ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim(SearchTrim)).Where(w => w != "").ToArray();
            var all = Tools2(); if (words.Length == 0) return all;
            var scored = new List<KeyValuePair<Tool, int>>();
            for (int k = 0; k < all.Count; k++)
            {
                var t = all[k]; var e = Explain.Term("tb." + t.Id);
                string title = (t.Id + " " + t.Title).ToLowerInvariant(), text = ((e != null ? e.Friendly + " " + e.Tag + " " + e.What + " " + e.Why : "") + " " + t.Group).ToLowerInvariant();
                int total = 0; bool ok = true;
                foreach (var w0 in words)
                {
                    int best = 0;
                    foreach (var w in new[] { w0, w0.Length > 3 && w0.EndsWith("s", StringComparison.Ordinal) ? w0.Substring(0, w0.Length - 1) : w0 })   // printers -> printer
                    {
                        if (t.Id == w) best = Math.Max(best, 100);
                        if (System.Text.RegularExpressions.Regex.IsMatch(title, @"(^|[^a-z0-9])" + System.Text.RegularExpressions.Regex.Escape(w) + @"($|[^a-z0-9])")) best = Math.Max(best, 70); else if (title.Contains(w)) best = Math.Max(best, 50);
                        string syn; if (Synonyms.TryGetValue(w, out syn)) { int pos = Array.IndexOf(syn.Split(' '), t.Id); if (pos >= 0) best = Math.Max(best, 90 - pos * 5); }
                        if (best == 0 && text.Contains(w)) best = 10;
                    }
                    if (best == 0) { ok = false; break; }
                    total += best;
                }
                if (ok) scored.Add(new KeyValuePair<Tool, int>(t, total * 1000 - k));   // ties keep the list's order: reordering Tools2 / TbLayout changes which tool a search opens first (the deep test pins a few)
            }
            return scored.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
        }
        static readonly char[] TitleSplit = { ' ', '-', '(', ')', ',', '/' };
        string DidYouMean(string q)
        {
            q = (q ?? "").Trim().ToLowerInvariant(); if (q.Length < 3 || q.Contains(" ")) return null;
            var words = new List<string>(Synonyms.Keys);
            foreach (var t in Tools2()) { words.Add(t.Id); words.AddRange(t.Title.ToLowerInvariant().Split(TitleSplit, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 3)); }
            string best = null; int bd = q.Length <= 4 ? 1 : 2;
            foreach (var w in words.Distinct()) { int d = Edit(q, w.ToLowerInvariant(), bd); if (d <= bd && (best == null || d < bd)) { best = w.ToLowerInvariant(); bd = d; } }
            return best;
        }
        // edit distance, giving up past "max"
        static int Edit(string a, string b, int max)
        {
            if (Math.Abs(a.Length - b.Length) > max) return max + 1;
            var prev = new int[b.Length + 1]; var cur = new int[b.Length + 1]; for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i; int rowMin = i;
                for (int j = 1; j <= b.Length; j++) { cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1)); rowMin = Math.Min(rowMin, cur[j]); }
                if (rowMin > max) return max + 1; var t = prev; prev = cur; cur = t;
            }
            return prev[b.Length];
        }
        static readonly char[] SearchTrim = { '.', ',', ';', ':', '!', '?', '"', '\'', '(', ')' };
        void MoveTool(int by)
        {
            if (tbVisible.Count == 0) return;
            int i = Math.Max(0, Math.Min(tbVisible.Count - 1, tbVisible.IndexOf(tbSel) + by)); string id = tbVisible[i];
            if (id == tbSel && tbVisible.IndexOf(tbSel) >= 0) return;
            SelectTool(id);
            float y; if (tbRowY.TryGetValue(id, out y))
            {
                int top = -tbList.AutoScrollPosition.Y, h = tbList.ClientSize.Height;
                if (y < top) tbList.AutoScrollPosition = new Point(0, (int)y - Sc(30)); else if (y + 46 * S > top + h) tbList.AutoScrollPosition = new Point(0, (int)(y + 46 * S) - h + Sc(10));
            }
        }
        void PaintTbList(Graphics g)
        {
            g.TranslateTransform(tbList.AutoScrollPosition.X, tbList.AutoScrollPosition.Y); Begin(tbList);
            float x = 14 * S, y = 4 * S, w = tbList.ClientSize.Width - 20 * S; bool searching = tbFilter != "";
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                tbVisible.Clear(); tbRowY.Clear();
                Action<Tool> item = t =>
                {
                    // two lines: the tool, and what it answers in plain words
                    var te = Explain.Term("tb." + t.Id); string tag = te != null ? te.Tag : ""; float rh = tag != "" ? 44 * S : 28 * S;
                    bool snmpRow = !searching && t.Id == "snmp", on = !tbHome && (t.Id == tbSel || snmpRow && SnmpView(tbSel));
                    string title = snmpRow ? "SNMP - ask a switch or router" : t.Title; if (snmpRow) tag = "Ports, traffic, CPU, neighbours, sensors, any OID.";
                    if (searching && SectionOf(t) != tbSection) tag = SectionName(SectionOf(t)) + " · " + tag;   // a result from another section says where it lives
                    var rr = new RectangleF(0, y, tbList.ClientSize.Width, rh);
                    if (on) using (var b = new SolidBrush(Theme.RowSel)) g.FillRectangle(b, rr);
                    if (on && tbList.Focused) using (var fp = new Pen(Theme.Blue, Math.Max(1, 2 * S))) g.DrawRectangle(fp, 1 * S, y + 1 * S, tbList.ClientSize.Width - 3 * S, rh - 2 * S);   // where the keyboard is
                    g.DrawString(title, Draw.fBody, ink, new RectangleF(x + 4 * S, y + 5 * S, w - 4 * S, 18 * S), nowrap);
                    if (tag != "") g.DrawString(tag, Draw.fSmall, muted, new RectangleF(x + 4 * S, y + 23 * S, w - 4 * S, 17 * S), nowrap);
                    if (tbLast.ContainsKey(t.Id)) using (var db = new SolidBrush(Theme.Green)) g.FillEllipse(db, x - 6 * S, y + 12 * S, 5 * S, 5 * S);   // run this session
                    string id = t.Id; Act(tbList, rr, delegate { OpenTool(id); }); Hint(tbList, rr, "tb." + id);
                    if (!tbRowY.ContainsKey(id)) { tbVisible.Add(id); tbRowY[id] = y; }
                    y += rh;
                };
                if (searching)
                {
                    var found = Matches();
                    g.DrawString(found.Count == 0 ? "NOTHING MATCHES - TRY ANOTHER WORD" : found.Count + (found.Count == 1 ? " TOOL" : " TOOLS") + "  ·  ENTER OPENS THE FIRST", Draw.fTiny, muted, x, y + 4 * S); y += 24 * S;
                    if (found.Count == 0)
                    {
                        // did you mean: the closest tool name or everyday word, one or two letters off
                        string dym = DidYouMean(tbFilter);
                        if (dym != null) using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) { string tx = "Did you mean  " + dym + " ?"; var ts = g.MeasureString(tx, Draw.fBody); g.DrawString(tx, Draw.fBody, lb, x, y); string q = dym; Act(tbList, new RectangleF(x, y, ts.Width, ts.Height), delegate { tbSearch.Box.Text = q; tbSearch.Box.SelectionStart = q.Length; }); y += 26 * S; }
                    }
                    foreach (var t in found) item(t);
                }
                else
                {
                    // this section only; SNMP is one row (its views are tabs on the right); recent tools live on the section's home page
                    foreach (var grp in Tools2().Where(t => SectionOf(t) == tbSection && (!SnmpView(t.Id) || t.Id == "snmp")).GroupBy(t => t.Group))
                    {
                        bool open = tbOpen.Contains(grp.Key); var hr = new RectangleF(0, y, tbList.ClientSize.Width, 30 * S);
                        float cx = x + 1 * S, cy = y + 15 * S;   // a drawn chevron - not every font has the arrow characters
                        var tri = open ? new[] { new PointF(cx - 4 * S, cy - 2 * S), new PointF(cx + 4 * S, cy - 2 * S), new PointF(cx, cy + 3 * S) } : new[] { new PointF(cx - 2 * S, cy - 4 * S), new PointF(cx + 3 * S, cy), new PointF(cx - 2 * S, cy + 4 * S) };
                        g.FillPolygon(open ? ink : muted, tri);
                        g.DrawString(grp.Key + (grp.Count() > 1 ? "   " + grp.Count() : ""), Draw.fTiny, open ? ink : muted, x + 10 * S, y + 9 * S);
                        string key = grp.Key; Act(tbList, hr, delegate { if (!tbOpen.Remove(key)) tbOpen.Add(key); tbList.Invalidate(); });
                        y += 30 * S;
                        if (open) { foreach (var t in grp) item(t); y += 4 * S; }
                    }
                }
            }
            Need(tbList, y + 10 * S);
        }
        // a section's home page: what it's for, your recent tools, and a card per group with its first tools
        void PaintHome(Graphics g, float x, float y, float w)
        {
            var sec = Sections.First(z => z[0] == tbSection);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            {
                y += 6 * S; g.DrawString(sec[1], Draw.fHead, ink, x, y); y += 30 * S;
                g.DrawString(sec[2] + "  Pick a tool, or type what you need in Find any tool.", Draw.fBody, sub, new RectangleF(x, y, w, 40 * S)); y += Draw.Measure(sec[2] + "  Pick a tool, or type what you need in Find any tool.", Draw.fBody, (int)w).Height + 14 * S;
                var recent = tbRecent.Select(id => Tools2().FirstOrDefault(z => z.Id == id)).Where(t => t != null).ToList();
                if (recent.Count > 0)
                {
                    g.DrawString("RECENT", Draw.fTiny, muted, x, y + 3 * S); float rx = x + g.MeasureString("RECENT", Draw.fTiny).Width + 12 * S;
                    foreach (var t in recent)
                    {
                        var ts = g.MeasureString(t.Title, Draw.fSmall); if (rx + ts.Width > x + w) break;
                        g.DrawString(t.Title, Draw.fSmall, lb, rx, y); string id = t.Id; Act(tbCanvas, new RectangleF(rx, y, ts.Width, ts.Height), delegate { OpenTool(id); }); rx += ts.Width + 18 * S;
                    }
                    y += 30 * S;
                }
                int cols = w > 900 * S ? 3 : w > 520 * S ? 2 : 1; float gap = 14 * S, cw = (w - gap * (cols - 1)) / cols, rowY = y, rowH = 0; int col = 0;
                foreach (var grp in Tools2().Where(t => SectionOf(t) == tbSection).GroupBy(t => t.Group))
                {
                    bool snmp = grp.Any(t => t.Id == "snmp");
                    var items = snmp ? SnmpTabs.Take(5).Select(z => new[] { z[0], z[1] }).ToList() : grp.Take(4).Select(t => new[] { t.Id, t.Title }).ToList();
                    float cx = x + col * (cw + gap), ch = 46 * S + items.Count * 26 * S + 28 * S;
                    var r = new RectangleF(cx, rowY, cw, ch);
                    Theme.Fill(g, r, 10 * S, Theme.Card); Theme.Stroke(g, r, 10 * S, Theme.Line, 1);
                    g.DrawString(grp.Key, Draw.fTiny, muted, cx + 14 * S, rowY + 14 * S);
                    float iy = rowY + 38 * S;
                    foreach (var it in items)
                    {
                        using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                            g.DrawString(it[1], Draw.fBody, ink, new RectangleF(cx + 14 * S, iy, cw - 28 * S, 22 * S), nf);
                        string id = it[0]; var ir = new RectangleF(cx, iy - 2 * S, cw, 24 * S); Act(tbCanvas, ir, delegate { SelectTool(id); }); Hint(tbCanvas, ir, "tb." + id);
                        iy += 26 * S;
                    }
                    int n = snmp ? SnmpTabs.Length : grp.Count(); string more = (snmp ? "All " + n + " views" : "All " + n + " tools") + "  ›";
                    var ms = g.MeasureString(more, Draw.fSmall); g.DrawString(more, Draw.fSmall, lb, cx + 14 * S, iy + 4 * S);
                    string first = grp.First().Id, gk = grp.Key; Act(tbCanvas, new RectangleF(cx + 14 * S, iy + 4 * S, ms.Width, ms.Height), delegate { tbOpen.Add(gk); tbList.Refresh(); float gy; if (tbRowY.TryGetValue(first, out gy)) tbList.AutoScrollPosition = new Point(0, Math.Max(0, (int)gy - Sc(40))); tbList.Focus(); tbList.Invalidate(); });   // opens the group in the list
                    rowH = Math.Max(rowH, ch);
                    if (++col == cols) { col = 0; rowY += rowH + gap; rowH = 0; }
                }
                Need(tbCanvas, rowY + rowH + 20 * S);
            }
        }
        void PaintTb(Graphics g)
        {
            g.TranslateTransform(tbCanvas.AutoScrollPosition.X, tbCanvas.AutoScrollPosition.Y); Begin(tbCanvas);
            float x = 14 * S, y = 8 * S, w = tbCanvas.ClientSize.Width - 28 * S; var run = tbRun0;
            if (tbHome) { PaintHome(g, x, y, w); return; }
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
            {
                // where you are: back to the section's home, and the group
                var cur = Tools2().First(z => z.Id == tbSel);
                using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
                {
                    string back = "‹  " + SectionName(tbSection); var bs = g.MeasureString(back, Draw.fSmall);
                    g.DrawString(back, Draw.fSmall, lb, x, y); Act(tbCanvas, new RectangleF(x, y, bs.Width, bs.Height), delegate { ShowSection(tbSection); });
                    g.DrawString("·  " + cur.Group, Draw.fTiny, muted, x + bs.Width + 6 * S, y + 2 * S);
                    y += 26 * S;
                    if (SnmpView(tbSel))
                    {
                        // SNMP: one device, many questions - the views as tabs
                        float tx = x; foreach (var tab0 in SnmpTabs)
                        {
                            // each view a button: filled when it's the one you're on
                            bool on = tab0[0] == tbSel; var ts = g.MeasureString(tab0[1], Draw.fBody); var pr = new RectangleF(tx, y, ts.Width + 26 * S, 32 * S);
                            if (pr.Right > x + w) { tx = x; y += 40 * S; pr = new RectangleF(tx, y, ts.Width + 26 * S, 32 * S); }
                            Theme.Fill(g, pr, 16 * S, on ? Theme.Blue : Theme.Card); if (!on) Theme.Stroke(g, pr, 16 * S, Theme.Line, 1);
                            using (var tb2 = new SolidBrush(on ? Color.White : Theme.Ink)) g.DrawString(tab0[1], Draw.fBody, tb2, tx + 13 * S, y + (32 * S - ts.Height) / 2);
                            string tid = tab0[0]; if (!on) Act(tbCanvas, pr, delegate { SelectTool(tid); });
                            tx += pr.Width + 8 * S;
                        }
                        y += 46 * S; g.DrawLine(line, x, y - 6 * S, x + w, y - 6 * S);
                    }
                }
                g.DrawString(tbTitle.ToUpperInvariant() + (tbBusy && run != null ? "  ·  " + run.Status : tbRows != null ? "  ·  DONE " + tbAt.ToString("HH:mm", CultureInfo.CurrentCulture) : ""), Draw.fTiny, muted, x, y);
                PrevRun prev = null; bool canCompare = !tbBusy && tbRows != null && tbPrev.TryGetValue(tbSel, out prev) && prev.Inputs == tbRowsKey && prev.Rows != tbRows;
                if (!canCompare) prev = null;
                if (!tbBusy && (tbRows != null || (tbSel == "mtu" && mtuHops != null) || (tbSel == "mtr" && mtrHops != null)))
                {
                    var cs = g.MeasureString("Copy as text", Draw.fSmall); float cxr = x + w - cs.Width - (prev != null ? g.MeasureString("Compare with the 00:00 run", Draw.fSmall).Width + 18 * S : 0);
                    var cr = new RectangleF(cxr, y - 2 * S, cs.Width, cs.Height);
                    string sv = tbSaved > DateTime.Now.AddSeconds(-3) ? "Saved ✓" : "Save..."; var ss = g.MeasureString(sv, Draw.fSmall); var sr = new RectangleF(cxr - ss.Width - 16 * S, y - 2 * S, ss.Width, ss.Height);
                    using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(sv, Draw.fSmall, lb, sr.X, sr.Y);
                    Act(tbCanvas, sr, SaveResult);
                    using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(tbCopied > DateTime.Now.AddSeconds(-3) ? "Copied ✓" : "Copy as text", Draw.fSmall, lb, cr.X, cr.Y);
                    Act(tbCanvas, cr, delegate { try { Clipboard.SetText(ResultText()); tbCopied = DateTime.Now; } catch (Exception ce) { Program.Log("copy: " + ce.Message); } tbCanvas.Invalidate(); });
                }
                if (prev != null)
                {
                    string lk = tbCompare ? "Hide the " + prev.At.ToString("HH:mm", CultureInfo.CurrentCulture) + " run" : "Compare with the " + prev.At.ToString("HH:mm", CultureInfo.CurrentCulture) + " run"; var ls = g.MeasureString(lk, Draw.fSmall);
                    var lr = new RectangleF(x + w - ls.Width, y - 2 * S, ls.Width, ls.Height);
                    using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(lk, Draw.fSmall, lb, lr.X, lr.Y);
                    Act(tbCanvas, lr, delegate { tbCompare = !tbCompare; tbCanvas.Invalidate(); });
                }
                y += 22 * S;
                var e = Explain.Term("tb." + tbSel);
                if (tbRows == null && !tbBusy && mtuHops == null && e != null) { float h0 = Draw.Measure(e.Tag + " " + e.What, Draw.fBody, (int)w).Height; g.DrawString(e.Tag + " " + e.What, Draw.fBody, sub, new RectangleF(x, y, w, h0 + 4)); y += h0 + 12 * S; }
                if (tbRows == null && !tbBusy && mtuHops == null)
                {
                    string d = Duration(tbSel, Inputs());
                    if (d != "") { string dl = "Takes " + d + ".  Press F1 on anything for what it means."; float dh = Draw.Measure(dl, Draw.fSmall, (int)w).Height; g.DrawString(dl, Draw.fSmall, muted, new RectangleF(x, y, w, dh + 4)); y += dh + 10 * S; }
                }
                if (tbSel == "owners" && !Online.On)
                {
                    string lk = "Turn on online lookups for " + Cfg.OnlineMin + " min  >"; var ls = g.MeasureString(lk, Draw.fBodyB);
                    using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(lk, Draw.fBodyB, lb, x, y);
                    Act(tbCanvas, new RectangleF(x, y, ls.Width, ls.Height), delegate { Online.Start(); tbCanvas.Invalidate(); header.Invalidate(); });
                    y += 30 * S;
                    g.DrawString("Sends only public IP addresses to RIPEstat and PeeringDB, then switches itself off. Settings > Online lookups has the timer.", Draw.fSmall, muted, new RectangleF(x, y, w, 34 * S)); y += 36 * S;
                }
                if (tbSel == "mtu") y = PaintMtu(g, x, y, w);
                if (tbSel == "mtr" && mtrHops != null) y = PaintMtr(g, x, y, w);
                if (run != null) { var s = run.Copy(); if (s.Count > 1) y = PaintSeries(g, s, run.SeriesUnit, x, y, w); }
                if (tbRows != null)
                {
                    float nameW = Math.Min(260 * S, w * 0.32f), pillW = Cfg.Details ? 170 * S : 120 * S;
                    foreach (var o in tbRows)
                    {
                        int lv; int.TryParse(o[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv);
                        if (lv == 8) continue;   // Copy as text only
                        if (lv == 6 || lv == 7)
                        {
                            // a table line: fixed width, one line, clipped at the edge (Copy as text has all of it)
                            float lh = 19 * S;
                            if (lv == 7) using (var tb = new SolidBrush(Theme.LvSoft(2))) g.FillRectangle(tb, x - 4 * S, y, w + 8 * S, lh);
                            using (var lb = new SolidBrush(lv == 7 ? Theme.LvText(2) : Theme.Ink)) using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.None })
                                g.DrawString(o[2], Draw.fMonoS, lb, new RectangleF(x, y + 2 * S, w, lh), nf);
                            y += lh; continue;
                        }
                        string adv = o.Length > 3 ? o[3] : "", ev = o.Length > 4 ? o[4] : "";
                        string was = "";
                        if (tbCompare && prev != null) { var pr = prev.Rows.FirstOrDefault(z => z.Length > 2 && z[1] == o[1]); was = pr == null ? "new since " + prev.At.ToString("HH:mm", CultureInfo.CurrentCulture) : pr[2] != o[2] ? "at " + prev.At.ToString("HH:mm", CultureInfo.CurrentCulture) + ": " + pr[2] : ""; }
                        if (was != "") adv = was + (adv != "" ? "\n" + adv : "");
                        float tw = w - nameW - pillW - 10 * S, vh = Draw.Measure(o[2], Draw.fBody, (int)tw).Height, ah = adv == "" ? 0 : Draw.Measure(adv, Draw.fSmall, (int)tw).Height + 4 * S, rh = Math.Max(Math.Max(30 * S, vh + ah + 12 * S), Draw.Measure(o[1], Draw.fBodyB, (int)(nameW - 24 * S)).Height + 12 * S);
                        if (lv == 4) using (var hb = new SolidBrush(Theme.BlueSoft)) g.FillRectangle(hb, x - 4 * S, y, w + 8 * S, rh);
                        using (var b = new SolidBrush(Theme.Lv(lv == 4 ? 3 : lv))) g.FillEllipse(b, x, y + 9 * S, 10 * S, 10 * S);
                        g.DrawString(o[1], Draw.fBodyB, ink, new RectangleF(x + 20 * S, y + 5 * S, nameW - 24 * S, rh));
                        using (var vb = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(o[2], Draw.fBody, vb, new RectangleF(x + nameW, y + 5 * S, tw, vh + 4));
                        if (adv != "") g.DrawString(adv, Draw.fSmall, sub, new RectangleF(x + nameW, y + 7 * S + vh, tw, ah));
                        if (ev != "") { float px = x + w - pillW + 4 * S; Evidence.Pill(g, ev, px, y + 6 * S, tbAt); Hint(tbCanvas, new RectangleF(px, y, pillW, rh), Evidence.Key(ev)); }
                        if (o.Length > 5 && o[5] != "") { var act = o[5]; Act(tbCanvas, new RectangleF(x, y, w, rh), delegate { RowAction(act); }); }
                        y += rh; g.DrawLine(line, x, y, x + w, y);
                    }
                }
            }
            Need(tbCanvas, y + 20 * S);
        }
        float PaintMtu(Graphics g, float x, float y, float w)
        {
            var hops = mtuHops;
            if (hops == null) { if (!tbBusy) using (var mb = new SolidBrush(Theme.Muted)) g.DrawString("Type a destination and press Run: AdeeboMeter traces the path, then you measure hop by hop.", Draw.fBody, mb, new RectangleF(x, y, w, 40 * S)); return y + 40 * S; }
            float cx = x;
            foreach (var b in new[] { "Next hop  >", "Probe all", "Start again" })
            {
                var ts = g.MeasureString(b, Draw.fBodyB); var br = new RectangleF(cx, y, ts.Width + 26 * S, 32 * S); bool prim = b.StartsWith("Next", StringComparison.Ordinal);
                Theme.Fill(g, br, 16 * S, prim ? Theme.Blue : Theme.Soft); using (var tb = new SolidBrush(prim ? Color.White : Theme.Ink)) g.DrawString(b, Draw.fBodyB, tb, cx + 13 * S, y + (32 * S - ts.Height) / 2);
                string bb = b;
                Act(tbCanvas, br, delegate
                {
                    if (bb.StartsWith("Next", StringComparison.Ordinal)) { var n = hops.FirstOrDefault(h => h.Mtu == -1 && h.MtuNote == "" && h.Ip != ""); if (n != null) MeasureHops(new List<PathTools.Hop> { n }); }
                    else if (bb.StartsWith("Probe", StringComparison.Ordinal)) MeasureHops(hops.Where(h => h.Mtu == -1 && h.MtuNote == "" && h.Ip != "").ToList());
                    else { foreach (var h in hops) { h.Mtu = -1; h.MtuNote = ""; h.Said = h.BlackHole = false; } tbCanvas.Invalidate(); }
                });
                cx += br.Width + 8 * S;
            }
            y += 46 * S;
            using (var muted = new SolidBrush(Theme.Muted)) g.DrawString("HOP   ROUTER                                     BIGGEST PACKET THAT GETS THERE  ·  CLICK A HOP TO MEASURE IT", Draw.fTiny, muted, x, y);
            y += 22 * S; int prev = 99999; float barX = x + w * 0.45f, barW = w * 0.25f;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
                foreach (var h in hops)
                {
                    var rr = new RectangleF(x, y, w, 30 * S);
                    g.DrawString(h.N.ToString(CultureInfo.CurrentCulture), Draw.fMono, sub, x, y + 6 * S);
                    g.DrawString(h.Ip == "" ? "·  no answer" : h.Ip + (h.Reached ? "  (destination)" : ""), Draw.fBody, h.Ip == "" ? sub : ink, x + 40 * S, y + 6 * S);
                    if (h.Mtu > 0)
                    {
                        bool drop = h.Mtu < prev; float bw = barW * Math.Min(1f, (h.Mtu - 500) / (h.Mtu > 1500 ? 8716f : 1000f));
                        Theme.Fill(g, new RectangleF(barX, y + 9 * S, Math.Max(4, bw), 12 * S), 4 * S, drop ? Theme.Amber : Theme.Green);
                        using (var vb = new SolidBrush(drop ? Theme.LvText(1) : Theme.Ink)) g.DrawString(h.Mtu + (drop ? "  ← drops here: " : "  ") + (drop || h.BlackHole ? h.MtuNote : ""), drop ? Draw.fBodyB : Draw.fBody, vb, new RectangleF(barX + barW + 10 * S, y + 6 * S, w - (barX - x) - barW - 10 * S, 20 * S));
                        prev = Math.Min(prev, h.Mtu);
                    }
                    else if (h.MtuNote != "") g.DrawString(h.MtuNote, Draw.fBody, sub, barX, y + 6 * S);
                    else if (h.Ip != "") { var hh = h; Act(tbCanvas, rr, delegate { MeasureHops(new List<PathTools.Hop> { hh }); }); g.DrawString("measure", Draw.fSmall, sub, barX, y + 8 * S); }
                    y += 30 * S; g.DrawLine(line, x, y, x + w, y);
                }
            var measured = hops.Where(h => h.Mtu > 0).ToList();
            if (measured.Count > 0)
            {
                var low = measured.OrderBy(h => h.Mtu).First();
                string sum = low.Mtu >= 1500 ? (low.Mtu > 1500 ? "Jumbo packets (" + low.Mtu + ") get" : "Full-size packets (1500) get") + " as far as hop " + measured.Max(h => h.N) + " - no MTU problem so far." : "The path shrinks to " + low.Mtu + " at hop " + low.N + " - " + PathTools.Kind(low.Mtu) + "." + (low.BlackHole ? " That hop drops big packets silently: apps that send full-size packets hang (pages half-load, VPN stalls) until something lowers the size." : "");
                y += 10 * S; float sh = Draw.Measure(sum, Draw.fBody, (int)(w - 28 * S)).Height; var sr = new RectangleF(x, y, w, sh + 20 * S);
                Theme.Fill(g, sr, 10 * S, Theme.LvSoft(low.Mtu >= 1500 ? 0 : low.BlackHole ? 2 : 1)); using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(sum, Draw.fBody, ib, new RectangleF(x + 14 * S, y + 10 * S, w - 28 * S, sh + 4));
                y += sr.Height + 10 * S;
            }
            return y + 6 * S;
        }
        float PaintMtr(Graphics g, float x, float y, float w)
        {
            var hops = mtrHops;
            // fixed columns, numbers right-aligned under their headings
            string[] head = { "LOSS", "SENT", "LAST", "AVG", "BEST", "WORST", "JITTER" };
            float c0 = x + Math.Max(300 * S, w * 0.38f), cw = (x + w - c0) / head.Length;
            using (var muted = new SolidBrush(Theme.Muted)) using (var right = new StringFormat { Alignment = StringAlignment.Far })
            {
                g.DrawString("HOP", Draw.fTiny, muted, x, y); g.DrawString("ROUTER", Draw.fTiny, muted, x + 40 * S, y);
                for (int i = 0; i < head.Length; i++) g.DrawString(head[i] + (i >= 2 ? " ms" : ""), Draw.fTiny, muted, new RectangleF(c0 + i * cw, y, cw - 8 * S, 16 * S), right);
                y += 22 * S;
                using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
                    foreach (var h in hops.ToList())
                    {
                        double loss = h.Sent > 0 ? 100.0 * h.Lost / h.Sent : 0; double avg = h.Sent - h.Lost > 0 ? h.Sum / (h.Sent - h.Lost) : double.NaN;
                        g.DrawString(h.N.ToString(CultureInfo.CurrentCulture), Draw.fMono, sub, x, y + 5 * S);
                        g.DrawString(h.Ip == "" ? "·  no answer" : h.Ip + (h.Owner != "" ? "  " + h.Owner : ""), Draw.fBody, h.Ip == "" ? sub : ink, new RectangleF(x + 40 * S, y + 5 * S, c0 - x - 44 * S, 20 * S));
                        var cells = new[] { loss.ToString("0", CultureInfo.CurrentCulture) + "%", h.Sent.ToString(CultureInfo.CurrentCulture), PathTools.F(h.Last), PathTools.F(avg), PathTools.F(h.Best), PathTools.F(h.Worst), PathTools.F(h.Jit) };
                        for (int i = 0; i < cells.Length; i++)
                            using (var cb = new SolidBrush(i == 0 && loss >= 3 ? Theme.LvText(loss >= 10 ? 2 : 1) : i == 0 ? Theme.Ink : Theme.Sub))
                                g.DrawString(cells[i], Draw.fMono, cb, new RectangleF(c0 + i * cw, y + 5 * S, cw - 8 * S, 20 * S), right);
                        y += 26 * S; g.DrawLine(line, x, y, x + w, y);
                    }
            }
            return y + 12 * S;
        }
        static float PaintSeries(Graphics g, List<double> s, string unit, float x, float y, float w)
        {
            float h = 120 * S; var r = new RectangleF(x, y, w, h); Theme.Stroke(g, r, 8 * S, Theme.Track, 1);
            var ok = s.Where(v => !double.IsNaN(v)).ToList(); double max = ok.Count > 0 ? Math.Max(1, ok.Max()) * 1.1 : 1;
            float bw = (w - 20 * S) / Math.Max(1, s.Count), bx = x + 10 * S, base0 = y + h - 10 * S, ph = h - 26 * S;
            using (var bb = new SolidBrush(unit == "ms" ? Theme.Blue : Theme.Green)) using (var lost = new SolidBrush(Theme.Red))
                for (int i = 0; i < s.Count; i++)
                {
                    if (double.IsNaN(s[i])) { g.FillRectangle(lost, bx + i * bw, y + 16 * S, Math.Max(1, bw - 1), ph); continue; }
                    float bh = (float)(s[i] / max) * ph; g.FillRectangle(bb, bx + i * bw, base0 - bh, Math.Max(1, bw - (bw > 4 ? 1 : 0)), bh);
                }
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString("max " + max.ToString(max > 10 ? "0" : "0.#", CultureInfo.CurrentCulture) + " " + unit + (unit == "ms" ? "  ·  red = no answer" : "  ·  one bar per second"), Draw.fTiny, mb, x + 10 * S, y + 4 * S);
            return y + h + 14 * S;
        }
    }
}
