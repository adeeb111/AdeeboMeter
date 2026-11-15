// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // ================================================================== tool pictures, wave 4 (Adeeb, 15 Nov): every protocol, talking - from your result
    // The packet-by-packet exchange of wave 3 (Exchange in Visuals3.cs) for the protocol, lab, SNMP, sign-in and IPv6 tools, built
    // from the result itself: the banner the server really sent, the address STUN saw, each 802.1X event in the order Windows
    // logged it, every router that announced itself, each SNMP value that came back, the step that failed in red. Before a run
    // (empty result) the same exchange shows how the protocol works. Nothing is sent to draw it.
    sealed partial class FullView
    {
        static readonly HashSet<string> Pictured4 = new HashSet<string> {
            "mail", "smtppath", "ssh", "stun", "teams", "ldapcheck", "rdpcheck", "syslog", "trap", "netflow", "dhcpopt", "icmpb", "udpsend", "tcpsend", "httpreq",
            "twamp", "dscp", "wol", "proxy", "vpnleak", "ping6", "v6routers", "rareplay", "discover", "nbname", "clock", "timesrc", "hairpin", "dot1x", "auth", "eapreplay",
            "dhcpreplay", "udpmtu", "iperf", "https", "certlive", "radiuslab",
            "snmp", "snmpif", "snmptraffic", "snmpcpu", "snmplldp", "snmpsens", "snmpsnap", "snmpmac", "snmpvlan", "snmppoe", "snmplag", "snmpprn", "snmpups", "snmpwalk" };
        static bool InWave3(string id) { return Pictured3.Contains(id) || Pictured4.Contains(id); }
        static bool Animates3(string id) { return Animated3.Contains(id) || Pictured4.Contains(id); }

        // ---------- reading the result
        static readonly string[] Dot = { " · " }, EapSep = { "→", " · " };
        List<string[]> Res() { return VisRows().Where(o => o.Length > 2 && o[0] != "-1").ToList(); }
        string[] RRow(string prefix) { return Res().FirstOrDefault(o => o[1].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)); }
        string RV(string prefix) { var r = RRow(prefix); return r == null ? "" : r[2]; }
        int RLv(string prefix) { var r = RRow(prefix); int v; return r != null && int.TryParse(r[0], out v) && v >= 1 && v <= 2 ? v : -1; }
        static int Lv(string[] r) { int v; return r != null && int.TryParse(r[0], out v) && v >= 1 && v <= 2 ? v : -1; }
        static string P(string v, int i) { var p = (v ?? "").Split(Dot, StringSplitOptions.None); return i < p.Length ? p[i].Trim() : ""; }
        static string Or(string a, string b) { return string.IsNullOrEmpty(a) ? b : a; }
        static string Cut(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n - 1) + "…"; }
        string Tg(string def) { foreach (var a in VisIn()) { var t = Regex.Replace(a ?? "", @"^[a-z]+://", "", RegexOptions.IgnoreCase).Split('/', ' ', '|')[0]; if (Regex.IsMatch(t, @"^[\w.\-\[\]:@]+$") && t.Length > 2 && !Regex.IsMatch(t, @"^\d{1,5}$")) return t; } return def; }
        static string HostOf(string t) { t = t.Contains("@") ? t.Substring(t.IndexOf('@') + 1) : t; return t.Count(c => c == ':') == 1 ? t.Split(':')[0] : t; }
        static string PortOf(string t, string def) { var m = Regex.Match(t ?? "", @"^[^:\[]+:(\d+)$"); return m.Success ? m.Groups[1].Value : def; }
        static XStep Bc(int from, int to, string label, string detail) { return new XStep { From = from, To = to, Label = label, Detail = detail, All = true }; }
        static XStep Lk(XStep s) { s.Sealed = true; return s; }
        static XStep Rd(XStep s, int lv) { if (lv > s.Lv) s.Lv = lv; return s; }

        float Wave4(Graphics g, float x, float y, float w, string st)
        {
            string id = tbSel; var res = Res(); bool ran = res.Count > 0; List<XStep> s = new List<XStep>(); string cap = "", foot = ""; string[] who;
            switch (id)
            {
                // ---------------------------------------------------------------- mail, SSH, RDP, LDAP: the server's own words
                case "mail":
                    {
                        string t = Tg("mail.example.com:587"), h = HostOf(t), port = PortOf(t, "587"); var b = Res().FirstOrDefault(o => Regex.IsMatch(o[1], @"^(SMTP|IMAP|POP)", RegexOptions.IgnoreCase));
                        string proto = b != null ? b[1] : "SMTP " + port, banner = b != null ? P(b[2], 0) : "220 " + h + " ESMTP", tls = RV("STARTTLS"), auth = RV("Login");
                        who = new[] { L.T("This PC"), h };
                        s.Add(X(0, 1, L.T("hello (SYN)"), "TCP " + Regex.Replace(proto, @"\D", ""), double.NaN));
                        s.Add(Rd(X(1, 0, Cut(banner, 46), L.T("the server's greeting - its name and software"), RowMs(b != null ? b[2] : "")), Lv(b)));
                        s.Add(X(0, 1, proto.StartsWith("SMTP", StringComparison.OrdinalIgnoreCase) ? "EHLO" : "CAPABILITY", L.T("what can you do?"), double.NaN));
                        s.Add(X(1, 0, "STARTTLS" + (auth != "" ? " · " + Cut(auth, 30) : ""), L.T("what it offers"), double.NaN));
                        s.Add(X(0, 1, "STARTTLS", L.T("let's seal the line first"), double.NaN));
                        s.Add(Rd(Lk(X(1, 0, Cut(Or(P(tls, 1), L.T("certificate + keys")), 30), Cut(Or(P(tls, 2), L.T("sealed from here - no password is sent")), 60), double.NaN)), RLv("STARTTLS")));
                        cap = "KNOCKING ON " + h.ToUpperInvariant(); foot = "AdeeboMeter stops before any password. Port 25 is often blocked on home lines; 587 and 465 are for apps.";
                    }
                    break;
                case "smtppath":
                    {
                        string d = HostOf(Tg("example.com")), mx = RV("MX"), con = RV("Connects"), tls = RV("STARTTLS"); string mxh = Or(Regex.Replace(mx, @"\s*\(.*$", ""), "mx1." + d);
                        who = new[] { L.T("This PC"), L.T("DNS server"), mxh };
                        s.Add(X(0, 1, L.T("who takes mail for") + " " + d + "?", "DNS · MX", double.NaN));
                        s.Add(Rd(X(1, 0, Or(mx, mxh + " (priority 10)"), L.T("lowest priority number is tried first"), double.NaN), RLv("MX")));
                        s.Add(X(0, 2, L.T("hello (SYN)"), Or(P(con, 0), "TCP 25"), double.NaN));
                        s.Add(Rd(X(2, 0, Or(P(con, 1), "220 " + L.T("ready")), L.T("the receiving server's greeting"), RowMs(con)), RLv("Connects")));
                        s.Add(X(0, 2, "EHLO", "", double.NaN));
                        s.Add(Rd(X(2, 0, "STARTTLS " + Or(P(tls, 0), "offered"), Or(P(tls, 1), L.T("encryption between mail servers")), double.NaN), RLv("STARTTLS")));
                        cap = "HOW MAIL FOR " + d.ToUpperInvariant() + " GETS DELIVERED"; foot = Or(RV("How far"), "Senders look up MX, then talk to that server on port 25.");
                    }
                    break;
                case "ssh":
                    {
                        string h = HostOf(Tg("192.0.2.10")), v = RV("SSH"), key = RV("Host key"); who = new[] { L.T("This PC"), h };
                        s.Add(X(0, 1, L.T("hello (SYN)"), "TCP " + PortOf(Tg(""), "22"), double.NaN));
                        s.Add(Rd(X(1, 0, L.T("hello back (SYN-ACK)"), L.T("the port is open"), RowMs(v)), RLv("SSH")));
                        s.Add(X(1, 0, Or(P(v, 0), "SSH-2.0-OpenSSH_9.6"), L.T("the server says its version first"), double.NaN));
                        s.Add(X(0, 1, L.T("our version + key exchange"), "KEXINIT", double.NaN));
                        s.Add(X(1, 0, L.T("host key") + " " + Cut(Or(key, "ED25519 SHA256:…"), 34), L.T("write it down - if it ever changes, ask why"), double.NaN));
                        s.Add(X(0, 1, L.T("goodbye"), L.T("no sign-in, no password"), double.NaN));
                        cap = "READING AN SSH SERVER'S BANNER"; foot = "The banner shows it's really SSH and how old the software is. A changed host key means a new server - or someone in the middle.";
                    }
                    break;
                case "rdpcheck":
                    {
                        string h = HostOf(Tg("pc01.example.net")), v = RV("Remote Desktop"); bool nla = v.IndexOf("NLA", StringComparison.OrdinalIgnoreCase) >= 0 || !ran; who = new[] { L.T("This PC"), h };
                        s.Add(X(0, 1, L.T("hello (SYN)"), "TCP 3389", double.NaN));
                        s.Add(Rd(X(1, 0, Or(P(v, 0), L.T("3389 open")), L.T("something listens on 3389"), RowMs(v)), RLv("Remote Desktop")));
                        s.Add(X(0, 1, L.T("Remote Desktop, please"), "X.224 Connection Request", double.NaN));
                        s.Add(X(1, 0, nla ? L.T("yes - sign in first (NLA)") : L.T("yes - plain RDP security"), "X.224 Connection Confirm", double.NaN));
                        if (!nla) s[3].Lv = 1;
                        cap = "IS REMOTE DESKTOP REALLY THERE?"; foot = "An open port isn't proof; the X.224 answer is. NLA asks for a password before the desktop shows - safer. AdeeboMeter stops here.";
                    }
                    break;
                case "ldapcheck":
                    {
                        string d = HostOf(Tg("example.net")), dcs = RV("Domain controllers"), ld = RV("LDAP"), kb = RV("Kerberos"); string dc1 = Or(dcs.Split(',')[0].Trim(), "dc01." + d);
                        who = new[] { L.T("This PC"), L.T("DNS server"), dc1 };
                        s.Add(X(0, 1, "_ldap._tcp.dc._msdcs." + d + "?", "DNS · SRV", double.NaN));
                        s.Add(Rd(X(1, 0, Cut(Or(dcs, dc1), 48), L.T("the servers that run the directory"), double.NaN), RLv("Domain controllers")));
                        s.Add(X(0, 2, L.T("hello"), "TCP 389 · 636 (LDAPS)", double.NaN));
                        s.Add(Rd(X(2, 0, Or(P(ld, 0), L.T("answers")), L.T("the directory is reachable"), RowMs(ld)), RLv("LDAP")));
                        s.Add(X(0, 2, L.T("hello"), "TCP 88 · Kerberos", double.NaN));
                        s.Add(Rd(X(2, 0, Or(kb, L.T("answers")), L.T("sign-in tickets come from here"), double.NaN), RLv("Kerberos")));
                        cap = "FINDING AND KNOCKING ON THE DOMAIN CONTROLLERS"; foot = "Sign-ins, Group Policy and mapped drives all start here. If DNS can't find _ldap, nothing else works.";
                    }
                    break;

                // ---------------------------------------------------------------- NAT: what the outside sees
                case "stun":
                    {
                        string pub = RV("Your public"), nat = RV("NAT"); who = new[] { L.T("This PC"), L.T("your router (NAT)"), HostOf(Tg("stun.example.com")) };
                        s.Add(X(0, 1, L.T("Binding request"), L.T("from your private address") + " · UDP " + PortOf(Tg(""), "3478"), double.NaN));
                        s.Add(X(1, 2, L.T("same packet, new return address"), L.T("the router swaps in its public address and a port"), double.NaN));
                        s.Add(Rd(X(2, 1, L.T("you look like") + " " + Or(pub, "203.0.113.50:51234"), "XOR-MAPPED-ADDRESS", double.NaN), RLv("Your public")));
                        s.Add(Rd(X(1, 0, L.T("passed back in to you"), Cut(Or(nat, L.T("now the PC knows its outside address")), 70), double.NaN), RLv("NAT")));
                        cap = "WHAT YOU LOOK LIKE FROM OUTSIDE"; foot = "Calls and games use this address to reach each other. If the port changes for every destination (symmetric NAT), they need a relay.";
                    }
                    break;
                case "teams":
                    {
                        string u = RV("UDP"), nat = RV("NAT"); who = new[] { L.T("This PC"), L.T("your router (NAT)"), L.T("Teams media relay") };
                        s.Add(X(0, 2, L.T("media test packet"), "UDP 3478-3481", double.NaN));
                        s.Add(Rd(X(2, 0, Or(P(u, 0), L.T("echo - UDP gets through")), L.T("calls can use UDP: smooth"), RowMs(u)), RLv("UDP")));
                        s.Add(X(0, 2, L.T("second test, another port"), L.T("does the router keep the same outside port?"), double.NaN));
                        s.Add(Rd(X(2, 0, Cut(Or(nat, L.T("NAT type")), 60), L.T("cone = direct calls; symmetric = through a relay"), double.NaN), RLv("NAT")));
                        cap = "CAN TEAMS CALLS USE UDP HERE?"; foot = "Without UDP, Teams falls back to TCP 443 - it works, but audio and video stutter more.";
                    }
                    break;
                case "hairpin":
                    {
                        var own = RRow("Your own"); string loc = RV("Same service"); string pub = own != null ? P(own[2], 0).Split(' ')[0] : "203.0.113.50:443", lan = Or(loc.Split(' ')[0], "192.168.1.40:443");
                        bool broke = Lv(own) == 2; who = new[] { L.T("This PC"), L.T("your router"), L.T("server inside") };
                        s.Add(X(0, 1, L.T("to your public address") + " " + pub, L.T("from inside your own network"), double.NaN));
                        if (broke) s.Add(Rd(X(1, 0, L.T("(nothing - the router won't turn it around)"), L.T("no NAT loopback / hairpin"), double.NaN), 2));
                        else { s.Add(X(1, 2, L.T("turned around (hairpin NAT)"), L.T("forwarded to") + " " + lan, double.NaN)); s.Add(X(2, 0, L.T("answer, back through the router"), "", double.NaN)); }
                        s.Add(X(0, 2, L.T("same service, its inside address") + " " + lan, L.T("straight across your network"), double.NaN));
                        s.Add(Rd(X(2, 0, L.T("answer"), Or(loc, ""), RowMs(loc)), loc == "" ? -1 : 0));
                        cap = "REACHING YOUR OWN SERVER BY ITS OUTSIDE ADDRESS"; foot = broke ? "From outside it probably works. Inside, use the local address or name - or turn on NAT loopback on the router." : "Your router loops traffic back, so the outside address works from inside too.";
                    }
                    break;

                // ---------------------------------------------------------------- one-way: sent, and never answered
                case "syslog": case "trap": case "netflow":
                    {
                        string sent = RV("Sent"), h = HostOf(Tg("192.0.2.20")); string port = id == "syslog" ? "UDP 514" : id == "trap" ? "UDP 162" : "UDP 2055";
                        var m = Regex.Match(sent, @":(\d+)"); if (m.Success) port = "UDP " + m.Groups[1].Value;
                        string what = id == "syslog" ? "<" + L.T("priority") + "> " + Or(VisIn().Length > 1 ? VisIn()[1] : "", "AdeeboMeter test") : id == "trap" ? L.T("SNMP test trap") : L.T("NetFlow test record");
                        who = new[] { L.T("This PC"), h };
                        s.Add(X(0, 1, Cut(what, 40), port + " · " + Cut(Or(sent, L.T("one packet, one way")), 60), double.NaN));
                        s.Add(X(1, 1, L.T("(no reply - this protocol never answers)"), Cut(Or(RV("Check"), L.T("look on the collector to see it arrived")), 70), double.NaN));
                        cap = id == "syslog" ? "A TEST LOG LINE, SENT" : id == "trap" ? "A TEST TRAP, SENT" : "A TEST FLOW RECORD, SENT";
                        foot = "UDP sends and forgets: 'sent' only means it left this PC. Only the server's log or collector can say it arrived.";
                    }
                    break;
                case "wol":
                    {
                        string sent = RV("Sent"); string mac = Regex.Match(sent, @"([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}").Value; if (mac == "") mac = Or(VisIn()[0], "02:00:5E:12:34:56");
                        string bc = Regex.Match(sent, @"\d+\.\d+\.\d+\.\d+:\d+").Value; who = new[] { L.T("This PC"), L.T("the sleeping PC"), L.T("everyone else") };
                        s.Add(Bc(0, 2, L.T("magic packet: FF×6, then") + " " + mac + " ×16", L.T("broadcast") + " · " + Or(bc, "UDP 9")));
                        s.Add(X(0, 1, L.T("its sleeping network card sees its own MAC"), L.T("and wakes the PC"), double.NaN));
                        s.Add(X(1, 0, L.T("(no reply - it's booting)"), Cut(Or(RV("Next"), L.T("try a ping in a minute")), 70), double.NaN));
                        cap = "WAKING " + mac + " OVER THE NETWORK"; foot = "Same network only (a router doesn't pass broadcasts). Wake-on-LAN must be on in the BIOS and on the network card.";
                    }
                    break;

                // ---------------------------------------------------------------- your own packets
                case "icmpb":
                    {
                        string h = HostOf(Tg("192.0.2.10")), shape = VisIn().Length > 2 ? VisIn()[2] : "size 1472 df", rep = RV("Replies"), tm = RV("Time"); bool bad = RLv("Replies") == 2;
                        who = new[] { L.T("This PC"), L.T("a router on the way"), h };
                        s.Add(X(0, 2, L.T("Echo request") + " · " + Or(shape, "size 1472 df"), L.T("the size, DF and TTL you chose"), double.NaN));
                        if (!bad) s.Add(X(2, 0, L.T("Echo reply") + " · " + Cut(Or(P(rep, 0), L.T("came back")), 30), Cut(Or(P(rep, 1), ""), 60), RowMs(tm)));
                        else { s.Add(Rd(X(1, 0, Cut(Or(P(rep, 0), L.T("too big - Fragmentation needed")), 40), Cut(Or(P(rep, 1), L.T("or 'Time exceeded' if the TTL ran out")), 60), double.NaN), 2)); }
                        cap = "AN ICMP PACKET YOU SHAPED YOURSELF"; foot = "Raise the size with DF on until it fails - that's the path MTU. A small TTL shows which router is that many hops away.";
                    }
                    break;
                case "udpsend": case "tcpsend":
                    {
                        string t = Tg("192.0.2.10:8080"), h = HostOf(t), port = PortOf(t, id == "udpsend" ? "5060" : "8080"), pay = VisIn().Length > 1 ? VisIn()[1].Replace("\\r\\n", " ↵ ").Trim() : "";
                        var rep = RRow("Reply"); string con = RV("Connected"); who = new[] { L.T("This PC"), h };
                        if (id == "tcpsend") { s.Add(X(0, 1, "SYN", "TCP " + port, double.NaN)); s.Add(Rd(X(1, 0, "SYN-ACK", L.T("connected"), RowMs(con)), RLv("Connected"))); s.Add(X(0, 1, "ACK", L.T("open"), double.NaN)); }
                        s.Add(X(0, 1, Cut(Or(pay, L.T("your message")), 44), (id == "udpsend" ? "UDP " : "TCP ") + port + " · " + Or(RV("Sent"), L.T("your bytes")), double.NaN));
                        s.Add(Rd(X(1, 0, Cut(rep != null ? P(rep[2], 0) : L.T("its reply"), 44), id == "udpsend" ? L.T("no reply would mean filtered - or it ignores this question") : L.T("first bytes shown"), RowMs(rep != null ? rep[2] : "")), Lv(rep)));
                        if (id == "tcpsend") s.Add(X(0, 1, "FIN", L.T("done - closed politely"), double.NaN));
                        cap = id == "udpsend" ? "ONE UDP PACKET, AND WHAT CAME BACK" : "A TCP CONVERSATION, START TO FINISH";
                        foot = id == "udpsend" ? "UDP has no handshake: the reply (or an ICMP 'port unreachable') is the only sign anything is there." : "RST instead of SYN-ACK = closed. No answer at all = a firewall dropped it.";
                    }
                    break;
                case "httpreq":
                    {
                        string url = VisIn()[0] != "" ? VisIn()[0] : "https://example.com/api/health", meth = VisIn().Length > 1 && VisIn()[1] != "" ? VisIn()[1] : "GET";
                        var um = Regex.Match(url, @"^(https?)://([^/:]+)(?::\d+)?(/.*)?$", RegexOptions.IgnoreCase); string h = um.Success ? um.Groups[2].Value : url, path = um.Success && um.Groups[3].Value != "" ? um.Groups[3].Value : "/";
                        bool tls = !url.StartsWith("http:", StringComparison.OrdinalIgnoreCase); var stl = res.FirstOrDefault(o => Regex.IsMatch(o[1], @"^\d{3}\b")); string ct = RV("Content-Type"), body = RV("Body");
                        who = new[] { L.T("This PC"), h };
                        s.Add(X(0, 1, "SYN → SYN-ACK", "TCP " + (tls ? "443" : "80"), double.NaN));
                        if (tls) s.Add(Lk(X(0, 1, L.T("TLS hello - name") + ": " + h, "SNI", double.NaN)));
                        var rq = X(0, 1, meth + " " + Cut(path, 40), L.T("method, path, headers"), double.NaN); rq.Sealed = tls; s.Add(rq);
                        var rp = X(1, 0, stl != null ? stl[1] + " · " + P(stl[2], 0) : L.T("status line + headers"), Cut(Or(ct + (body != "" ? "  " + body : ""), "200 / 301 / 403 / 404 / 500"), 70), RowMs(stl != null ? stl[2] : "")); rp.Sealed = tls; s.Add(Rd(rp, Lv(stl)));
                        cap = "YOUR HTTP REQUEST, AS SENT"; foot = "2xx = fine · 3xx = go elsewhere · 4xx = your request or access · 5xx = the server broke.";
                    }
                    break;
                case "dhcpopt":
                    {
                        string srv = Or(RV("DHCP server"), L.T("DHCP server")), opt = RV("Options"); who = new[] { L.T("This PC"), srv };
                        s.Add(Bc(0, 1, L.T("Inform: I have an address - just tell me the options"), L.T("broadcast · UDP 68 → 67")));
                        s.Add(Rd(X(1, 0, L.T("Ack with options"), Cut(Or(opt, "3 router · 6 DNS · 15 domain · 51 lease"), 80), double.NaN), RLv("Options")));
                        foreach (var o in (opt == "" ? Array.Empty<string>() : opt.Split(Dot, StringSplitOptions.None)).Take(4)) s.Add(X(1, 1, o.Trim(), OptNo(o), double.NaN));
                        cap = "WHAT THE DHCP SERVER HANDS OUT"; foot = "Phones and PXE boot read more options: 66/67 (boot server and file), 150 (Cisco TFTP), 43 (vendor).";
                    }
                    break;

                // ---------------------------------------------------------------- speed and quality, measured
                case "twamp":
                    {
                        string rt = RV("Round trip"), ow = RV("One way"); double o1 = VNum(ow, @"out\s*([0-9.]+)"), b1 = VNum(ow, @"back\s*([0-9.]+)");
                        who = new[] { L.T("This PC (sender)"), L.T("reflector") + " " + HostOf(Tg("192.0.2.7")) };
                        s.Add(X(0, 1, L.T("test packet, stamped T1"), "UDP 862 · TWAMP-light", o1));
                        s.Add(X(1, 1, L.T("received T2, sent back T3"), L.T("the reflector stamps both - its own delay is taken out"), double.NaN));
                        s.Add(Rd(X(1, 0, L.T("back at T4"), Cut(Or(rt, L.T("round trip = (T4-T1) - (T3-T2)")), 60), b1), RLv("Round trip")));
                        cap = "TWAMP: TIMESTAMPS BOTH WAYS"; foot = "The time on the first arrow is out, on the last one back - each direction on its own (needs synced clocks for exact numbers).";
                    }
                    break;
                case "dscp":
                    {
                        var mk = res.FirstOrDefault(o => o[1].StartsWith("Marked", StringComparison.OrdinalIgnoreCase)); string un = RV("Unmarked"), mean = RV("What it means");
                        string tag = mk != null ? Regex.Replace(mk[1], "^Marked ", "") : "EF (46)"; who = new[] { L.T("This PC"), L.T("your router"), L.T("provider"), HostOf(Tg("192.0.2.1")) };
                        s.Add(X(0, 1, L.T("ping marked") + " " + tag, L.T("'please treat me as voice'"), double.NaN));
                        s.Add(X(1, 2, L.T("mark kept on your side"), "", double.NaN));
                        s.Add(Rd(X(2, 3, L.T("mark may be reset to 0 here"), L.T("providers usually ignore your marks"), double.NaN), 1));
                        s.Add(Rd(X(3, 0, L.T("marked replies"), Cut(Or(mk != null ? mk[2] : "", ""), 50), VNum(mk != null ? mk[2] : "", @"avg\s*([0-9.]+)")), Lv(mk)));
                        s.Add(X(0, 3, L.T("same ping, unmarked"), L.T("for comparison"), double.NaN));
                        s.Add(X(3, 0, L.T("unmarked replies"), Cut(un, 50), VNum(un, @"avg\s*([0-9.]+)")));
                        cap = "DOES ANYONE HONOUR YOUR PRIORITY MARK?"; foot = Or(mean, "Marks matter on your own network (Wi-Fi WMM, company QoS). Across the internet they rarely survive.");
                    }
                    break;
                case "iperf":
                    {
                        string dl = RV("Download"), ul = RV("Upload"), sv = RV("Server"); who = new[] { L.T("This PC"), "iperf3 " + Or(P(sv, 0), HostOf(Tg("192.0.2.5")) + ":5201") };
                        s.Add(X(0, 1, L.T("control: test for") + " " + Or(P(sv, 1), "10 s"), "TCP 5201", double.NaN));
                        s.Add(X(1, 0, L.T("ready"), "", double.NaN));
                        if (ul != "" || !ran) s.Add(Rd(X(0, 1, L.T("upload at full speed") + " - " + Or(P(ul, 0), "…"), Or(P(ul, 1), L.T("as fast as the line allows")), double.NaN), RLv("Upload")));
                        if (dl != "" || !ran) s.Add(Rd(X(1, 0, L.T("download at full speed") + " - " + Or(P(dl, 0), "…"), Or(P(dl, 1), L.T("retransmits = packets sent twice")), double.NaN), RLv("Download")));
                        s.Add(X(1, 0, L.T("results"), L.T("Mbit/s each way, retransmits"), double.NaN));
                        cap = "IPERF3: A SPEED TEST TO YOUR OWN SERVER"; foot = "Your own server takes the internet out of it - perfect for Wi-Fi, a VPN, or a link between offices. Many retransmits = loss on the way.";
                    }
                    break;
                case "udpmtu":
                    {
                        string big = RV("Biggest"), how = RV("How"); double n = VNum(big, @"([0-9]+)\s*bytes"); if (double.IsNaN(n)) n = 1452;
                        who = new[] { L.T("This PC"), L.T("the path"), HostOf(Tg("example.com")) };
                        s.Add(X(0, 2, "1500 " + L.T("bytes, Don't Fragment"), Cut(Or(P(how, 0), "QUIC · UDP 443"), 50), double.NaN));
                        if (n < 1500) s.Add(Rd(X(1, 0, L.T("too big - dropped"), L.T("or 'fragmentation needed' from a router"), double.NaN), 1));
                        s.Add(X(0, 2, ((int)((1500 + n) / 2)).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + L.T("bytes"), L.T("halfway - and narrowing in"), double.NaN));
                        s.Add(X(0, 2, ((int)n).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + L.T("bytes"), L.T("the biggest that fits"), double.NaN));
                        s.Add(Rd(X(2, 0, L.T("answer") + " · " + Cut(Or(big, ""), 40), Cut(Or(P(how, 2), ""), 40), double.NaN), RLv("Biggest")));
                        cap = "THE BIGGEST UDP PACKET THAT FITS"; foot = Or(Res().Where(o => o[1] == "Biggest UDP packet through").Select(o => o.Length > 3 ? o[3] : "").FirstOrDefault(), "QUIC (HTTP/3) and big DNS answers fail quietly when the path is smaller than they think.");
                    }
                    break;

                // ---------------------------------------------------------------- the proxy and the VPN: which way
                case "proxy":
                    {
                        string goes = RV("This URL goes"), set = RV("Windows proxy"), why = RV("Why"); string u = HostOf(Tg("www.example.com"));
                        bool direct = goes.StartsWith("DIRECT", StringComparison.OrdinalIgnoreCase) || (!ran); var pm = Regex.Match(goes + " " + why, @"([\w.-]+:\d{2,5})"); string px = pm.Success ? pm.Groups[1].Value : "proxy:8080";
                        string pac = Regex.Match(set, @"https?://[^\s)]+").Value;
                        who = direct ? new[] { L.T("This PC"), L.T("proxy script (PAC)"), u } : new[] { L.T("This PC"), L.T("proxy script (PAC)"), px, u };
                        s.Add(X(0, 1, L.T("which way for") + " " + u + "?", Cut(Or(pac, L.T("the PAC file Windows was given")), 60), double.NaN));
                        s.Add(X(1, 0, direct ? "DIRECT" : "PROXY " + px, Cut(Or(why, ""), 70), double.NaN));
                        if (direct) s.Add(Lk(X(0, 2, L.T("straight to the site"), "TCP 443 · TLS", double.NaN)));
                        else { s.Add(X(0, 2, "CONNECT " + u + ":443", "", double.NaN)); s.Add(X(2, 3, L.T("the proxy connects for you"), "", double.NaN)); s.Add(X(2, 0, "200 Connection established", L.T("407 here = the proxy wants a sign-in"), double.NaN)); s.Add(Lk(X(0, 3, L.T("TLS straight through"), L.T("the proxy sees the name, not the page"), double.NaN))); }
                        cap = "WHICH WAY " + u.ToUpperInvariant() + " GOES"; foot = Or(set, "No proxy set - everything goes direct.");
                    }
                    break;
                case "vpnleak":
                    {
                        who = new[] { L.T("This PC"), Cut(Or(res.Where(o => o[1] == "VPN").Select(o => o[2]).FirstOrDefault(), L.T("VPN tunnel")), 22), L.T("your provider"), L.T("the internet") };
                        foreach (var o in res.Where(o => Regex.IsMatch(o[1], @"^(IPv4|IPv6|DNS|WebRTC)", RegexOptions.IgnoreCase)))
                        {
                            bool leak = Lv(o) == 2; string what = Regex.Replace(o[1], @"\s*outside the tunnel", "");
                            if (leak) { s.Add(Rd(X(0, 2, what + " - " + L.T("outside the tunnel"), Cut(o[2], 60), double.NaN), 2)); s.Add(Rd(X(2, 3, L.T("from your real address"), L.T("your provider sees it"), double.NaN), 2)); }
                            else { s.Add(Lk(X(0, 1, what + " - " + L.T("into the tunnel"), Cut(o[2], 60), double.NaN))); }
                        }
                        if (s.Count == 0) { s.Add(Lk(X(0, 1, L.T("IPv4 - into the tunnel"), L.T("hidden from your provider"), double.NaN))); s.Add(X(0, 2, L.T("IPv6 or DNS outside the tunnel?"), L.T("that would be a leak"), double.NaN)); s[1].Lv = 1; }
                        else s.Add(Lk(X(1, 3, L.T("out from the VPN's exit"), "", double.NaN)));
                        string seen = RV("The internet sees"); if (seen != "") s.Add(Rd(X(3, 0, L.T("the internet sees you as") + " " + seen, Cut(RRow("The internet sees").Length > 3 ? RRow("The internet sees")[3] : "", 60), double.NaN), RLv("The internet sees")));
                        cap = "WHAT GOES IN THE TUNNEL - AND WHAT SLIPS OUT"; foot = "Leaks are usually IPv6 (the VPN carries only IPv4) or DNS sent to the home router instead of the VPN's.";
                    }
                    break;

                // ---------------------------------------------------------------- IPv6 and neighbours
                case "ping6":
                    {
                        string r6 = RV("IPv6 reply"), path = RV("Path"); string addr = Or(P(r6, 0), "2001:db8::10"); who = new[] { L.T("This PC"), L.T("router") + " fe80::1", HostOf(Tg("example.com")) };
                        s.Add(Bc(0, 1, L.T("Neighbour Solicitation: who has fe80::1?"), "ICMPv6 135 · multicast"));
                        s.Add(X(1, 0, L.T("Neighbour Advertisement: me"), L.T("IPv6's version of ARP"), double.NaN));
                        s.Add(X(0, 2, L.T("Echo request to") + " " + addr, "ICMPv6 128" + (path != "" ? " · " + path : ""), double.NaN));
                        s.Add(Rd(X(2, 0, L.T("Echo reply") + (P(r6, 2) != "" ? " · " + P(r6, 2) : ""), "ICMPv6 129", RowMs(r6)), RLv("IPv6 reply")));
                        cap = "AN IPV6 PING, FROM THE FIRST HOP"; foot = "No ARP in IPv6 - neighbour discovery does it. Blocking all ICMPv6 breaks IPv6 itself.";
                    }
                    break;
                case "v6routers": case "rareplay":
                    {
                        var rs = res.Where(o => o[1].StartsWith("fe80", StringComparison.OrdinalIgnoreCase)).ToList();
                        if (rs.Count == 0) { string seen = RV("IPv6 routers seen"), pre = RV("Prefix"); foreach (var a in (seen == "" ? "1 · fe80::1 (your router)" : seen).Split(Dot, StringSplitOptions.None).Skip(1)) rs.Add(new[] { "0", a.Split(' ')[0], (pre != "" ? "prefix " + pre.Split(' ')[0] : "") + " " + Regex.Match(a, @"\(.*\)").Value }); }
                        rs = rs.Take(3).ToList(); who = new[] { L.T("This PC") }.Concat(rs.Select(o => o[1])).ToArray();
                        s.Add(Bc(0, who.Length - 1, L.T("Router Solicitation: any routers here?"), "ICMPv6 133 · ff02::2"));
                        for (int i = 0; i < rs.Count; i++) s.Add(Rd(X(i + 1, 0, L.T("Router Advertisement") + (Lv(rs[i]) == 2 ? " - " + L.T("a second router!") : ""), Cut(rs[i][2], 70), double.NaN), Lv(rs[i])));
                        cap = id == "rareplay" ? "THE ROUTER ANNOUNCEMENTS IN YOUR CAPTURE" : "HOW IPV6 FINDS ITS ROUTER";
                        foot = rs.Any(o => Lv(o) == 2) ? "Windows may send IPv6 to every router that announces itself. Find the extra one (a hotspot, a misconfigured box) and turn its RA off." : "Every router that answers becomes a way out - one is what you want.";
                    }
                    break;
                case "discover":
                    {
                        var devs = res.Where(o => Regex.IsMatch(o[1], @"^\d+\.\d+\.\d+\.\d+$")).Take(4).ToList(); who = new[] { L.T("This PC"), L.T("everyone (multicast)") };
                        s.Add(Bc(0, 1, L.T("who's here? (mDNS, SSDP, NetBIOS)"), "224.0.0.251:5353 · 239.255.255.250:1900 · UDP 137"));
                        foreach (var d in devs) s.Add(X(1, 0, d[1] + " - " + P(d[2], 1), P(d[2], 0) + " · " + P(d[2], 2), double.NaN));
                        if (devs.Count == 0) { s.Add(X(1, 0, L.T("me: Office printer"), "mDNS · _ipp._tcp", double.NaN)); s.Add(X(1, 0, L.T("me: a media renderer"), "SSDP", double.NaN)); }
                        cap = Or(RV("Heard in"), "DEVICES ANNOUNCING THEMSELVES").ToUpperInvariant(); foot = "These never leave your own network - the router doesn't pass them on. Guest Wi-Fi often blocks them.";
                    }
                    break;
                case "nbname":
                    {
                        string h = HostOf(Tg("192.0.2.40")), nm = RV("Name"), wg = RV("Workgroup"); who = new[] { L.T("This PC"), h };
                        s.Add(X(0, 1, L.T("what's your name?"), "NetBIOS node status · UDP 137", double.NaN));
                        s.Add(Rd(X(1, 0, Cut(Or(P(nm, 0), "NAS01 (NetBIOS)"), 40), wg != "" ? L.T("workgroup") + " " + wg : "", double.NaN), RLv("Name")));
                        s.Add(X(0, 1, L.T("reverse name?"), "mDNS · 224.0.0.251:5353", double.NaN));
                        s.Add(X(1, 0, Cut(Or(P(nm, 1), "nas01.local (mDNS)"), 40), L.T("Apple/Linux-style name"), double.NaN));
                        cap = "ASKING " + h + " ITS NAME"; foot = "Old Windows boxes answer NetBIOS, newer ones LLMNR or mDNS. Guest networks usually block all three.";
                    }
                    break;

                // ---------------------------------------------------------------- time
                case "clock": case "timesrc":
                    {
                        string src = RV("Time comes from"), off = RV("Clock"), tz = RV("Time zone"); string srcName = Or(src.Split(' ')[0], "time.windows.com");
                        who = new[] { L.T("This PC"), srcName };
                        s.Add(X(0, 1, L.T("what time is it? (sent at T1)"), "NTP · UDP 123", double.NaN));
                        s.Add(X(1, 1, L.T("got it T2, answered T3"), L.T("stratum: steps away from an atomic clock"), double.NaN));
                        s.Add(X(1, 0, L.T("its time"), Or(P(src, 1), ""), double.NaN));
                        s.Add(Rd(X(0, 0, Cut(Or(off, L.T("arrived T4 - offset worked out")), 44), Or(tz, L.T("offset = ((T2-T1) + (T3-T4)) / 2")), double.NaN), RLv("Clock")));
                        cap = id == "timesrc" ? "WHERE THIS PC GETS ITS TIME" : "IS THIS PC'S CLOCK RIGHT?"; foot = "Domain PCs follow a domain controller; home PCs time.windows.com. More than 5 minutes off and sign-ins (Kerberos) fail.";
                    }
                    break;

                // ---------------------------------------------------------------- network sign-in: 802.1X, RADIUS, certificates
                case "dot1x": case "eapreplay": case "auth":
                    {
                        who = new[] { L.T("This PC"), L.T("switch / access point"), L.T("RADIUS server") };
                        var ev = res.Where(o => Regex.IsMatch(o[1], @"^\d{1,2}:\d{2}")).ToList();
                        if (ev.Count > 0) foreach (var o in ev.Take(8)) s.Add(Eap(o[1] + " " + Cut(o[2], 46), o.Length > 3 ? Cut(o[3], 60) : "", o[2], Lv(o)));
                        else if (RV("EAP") != "")
                        {
                            foreach (var part in RV("EAP").Split(EapSep, StringSplitOptions.RemoveEmptyEntries)) s.Add(Eap(part.Trim(), "", part, -1));
                            var r = RRow("Result"); if (r != null) s.Add(Eap(r[2], r.Length > 3 ? Cut(r[3], 60) : "", r[2], Lv(r)));
                        }
                        else
                        {
                            s.Add(X(0, 1, "EAPOL-Start", L.T("I'd like to join"), double.NaN)); s.Add(X(1, 0, L.T("Identity?"), "EAP-Request", double.NaN)); s.Add(X(0, 1, "host/PC01.corp.example.com", "EAP-Response", double.NaN));
                            s.Add(X(1, 2, "Access-Request", "UDP 1812", double.NaN)); s.Add(Lk(X(2, 0, L.T("TLS: certificates both ways"), "Access-Challenge × n", double.NaN)));
                            var last = RRow("Last network sign-in"); bool fail = Lv(last) == 2;
                            if (fail) s[4] = Rd(Lk(X(2, 0, L.T("TLS: certificates - failed"), Cut(last.Length > 3 ? last[3] : last[2], 60), double.NaN)), 2);
                            s.Add(Rd(X(2, 1, fail ? "Access-Reject" : "Access-Accept + VLAN", "", double.NaN), fail ? 2 : -1)); s.Add(Rd(X(1, 0, fail ? "EAP-Failure" : "EAP-Success", fail ? L.T("the port stays shut") : L.T("the port opens"), double.NaN), fail ? 2 : -1));
                        }
                        cap = id == "eapreplay" ? "THE 802.1X EXCHANGE IN YOUR CAPTURE" : "802.1X: HOW THIS PC GETS LET IN"; foot = Or(RV("Category"), "Most failures are at the certificate step (expired, untrusted CA, wrong name) or a Reject for the wrong group.");
                    }
                    break;
                case "radiuslab":
                    {
                        var r = res.FirstOrDefault(o => o[1].StartsWith("Access-", StringComparison.OrdinalIgnoreCase)); string from = RV("Sent from"), attr = RV("Attributes");
                        string t = Tg("labtest@192.0.2.30"), user = t.Contains("@") ? t.Split('@')[0] : "labtest"; who = new[] { L.T("This PC") + (from != "" ? " " + from.Split(' ')[0] : ""), HostOf(t) };
                        s.Add(X(0, 1, "Access-Request: " + user, L.T("UDP 1812 · password hidden with the shared secret"), double.NaN));
                        s.Add(Rd(X(1, 0, r != null ? r[1] : "Access-Accept / Reject", Cut(r != null ? r[2] : L.T("silence = wrong secret or not a known client"), 60), RowMs(r != null ? r[2] : "")), Lv(r)));
                        if (attr != "") s.Add(X(1, 0, L.T("attributes"), Cut(attr, 70), double.NaN));
                        cap = "A TEST SIGN-IN AGAINST RADIUS"; foot = from != "" ? Cut(from, 110) : "Silence = wrong shared secret or firewall. Reject = the server works, the account or policy doesn't.";
                    }
                    break;
                case "https": case "certlive":
                    {
                        string h = HostOf(Tg("www.example.com")); who = new[] { L.T("This PC"), h }; string ver = RV("TLS version");
                        s.Add(X(0, 1, L.T("TLS hello - name") + ": " + h, "ClientHello · TCP 443", double.NaN));
                        s.Add(X(1, 0, Or(P(ver, 0), "ServerHello") + (RV("Key agreement") != "" ? " · " + RV("Key agreement") : ""), Or(P(ver, 1), ""), RowMs(ver)));
                        var chain = res.Where(o => o[1].Contains("missing") || o[1].Contains("certificate") || o[1].Contains("root") || o[1].Contains("intermediate")).Take(3).ToList();
                        foreach (var c in chain) s.Add(Rd(X(1, 0, Cut(Regex.Replace(c[1], @"^[^\w]+", "").Trim() + ": " + c[2], 50), c.Length > 3 ? Cut(c[3], 60) : "", double.NaN), Lv(c)));
                        if (chain.Count == 0) s.Add(Rd(X(1, 0, L.T("certificate chain"), Cut(Or(RV("Different"), RV("File")), 60), double.NaN), RLv("Different")));
                        s.Add(Rd(X(0, 0, L.T("checked: name") + " " + Or(P(RV("Name matches"), 0), "?") + " · " + L.T("expires") + " " + Or(RV("Expires"), "?"), L.T("is it trusted by Windows?"), double.NaN), Math.Max(RLv("Name matches"), RLv("Expires"))));
                        s.Add(Lk(X(0, 1, L.T("keys agreed"), Or(RV("Encryption"), L.T("sealed from here")), double.NaN)));
                        cap = id == "certlive" ? "WHAT THE SERVER REALLY SENDS" : "THE HTTPS HANDSHAKE, CHECKED"; foot = Or(RV("In plain words"), "A missing middle certificate works in some browsers and fails in apps. The name must match exactly.");
                    }
                    break;
                case "dhcpreplay":
                    {
                        string lease = RV("Lease"), off = RV("Offer"); var im = Regex.Match(lease, @"(\d+\.\d+\.\d+\.\d+) from (\d+\.\d+\.\d+\.\d+)"); string ip = im.Success ? im.Groups[1].Value : "192.168.1.20", sv = im.Success ? im.Groups[2].Value : L.T("DHCP server");
                        double ms = VNum(lease, @"in\s*([0-9.]+)\s*ms"); who = new[] { L.T("the PC in the capture"), sv };
                        s.Add(Bc(0, 1, "Discover", L.T("broadcast · UDP 68 → 67")));
                        s.Add(X(1, 0, "Offer: " + ip, Cut(Or(off, L.T("mask, gateway, DNS")), 70), double.NaN));
                        s.Add(X(0, 1, "Request: " + ip, L.T("still broadcast"), double.NaN));
                        s.Add(Rd(X(1, 0, "ACK", L.T("whole exchange") + (double.IsNaN(ms) ? "" : " " + ms + " ms"), ms), RLv("Lease")));
                        cap = "THE DHCP EXCHANGE IN YOUR CAPTURE"; foot = "Missing Offer = the server never answered (or a relay is wrong). NAK = the address asked for isn't valid here.";
                    }
                    break;

                // ---------------------------------------------------------------- SNMP: the question, then every value that came back
                default:
                    {
                        string[] q; if (!SnmpAsk.TryGetValue(id, out q)) return y;
                        string t = HostOf(Tg("192.0.2.2")), com = VisIn().Length > 1 ? VisIn()[1] : ""; bool v3 = com.Contains(":") || com.StartsWith("v3", StringComparison.OrdinalIgnoreCase);
                        who = new[] { L.T("This PC"), t };
                        var vals = res.Take(6).ToList();
                        if (id == "snmpwalk")
                        {
                            string start = VisIn().Length > 2 && VisIn()[2] != "" ? VisIn()[2] : "system";
                            s.Add(Rd(X(0, 1, "GetNext " + start, "UDP 161 · " + (v3 ? "v3" : "v2c"), double.NaN), -1));
                            foreach (var o in vals.Take(4)) { s.Add(X(1, 0, Cut(o[1] + " = " + o[2], 50), "Response", double.NaN)); s.Add(X(0, 1, "GetNext " + Cut(o[1], 30), "", double.NaN)); }
                            s.Add(X(1, 0, vals.Count > 0 ? L.T("... until the branch ends") : "sysDescr.0 = ...", "endOfMibView", double.NaN));
                        }
                        else
                        {
                            s.Add(X(0, 1, "GetRequest - " + L.T(q[1]), q[0] + " · UDP 161 · " + (v3 ? "SNMPv3" : L.T("community") + " " + (com == "" ? "public" : new string('•', Math.Min(8, com.Length)))), double.NaN));
                            if (id == "snmptraffic") s.Add(X(0, 1, L.T("the same, 5 s later"), L.T("the difference is the speed"), double.NaN));
                            if (vals.Count == 0) s.Add(X(1, 0, "Response", L.T("the values, one per object"), double.NaN));
                            foreach (var o in vals) s.Add(Rd(X(1, 0, Cut(o[1] + ": " + o[2], 54), o.Length > 3 ? Cut(o[3], 60) : "", double.NaN), Lv(o)));
                        }
                        if (v3) foreach (var z in s) z.Sealed = true;
                        cap = q[1].ToUpperInvariant() + " - " + t; foot = "No answer = wrong community/user, an ACL on the device, or SNMP off - SNMP never says which. v3 is signed and encrypted; v2c is plain text.";
                    }
                    break;
            }
            return ExchangeCard(g, x, y, w, st, cap, who, s, foot);
        }

        // an 802.1X event (as Windows logged it, or as the capture shows it) placed on the right arrow
        static XStep Eap(string label, string detail, string raw, int lv)
        {
            string r = raw.ToLowerInvariant();
            int from = 0, to = 1;
            if (r.Contains("association")) { from = 0; to = 1; }
            else if (r.Contains("identity")) { from = 0; to = 2; }
            else if (r.Contains("peap") || r.Contains("tls") || r.Contains("certificate")) { from = 2; to = 0; }
            else if (r.Contains("fail") || r.Contains("reject")) { from = 2; to = 0; }
            else if (r.Contains("success") || r.Contains("accept")) { from = 2; to = 0; }
            var s = X(from, to, label, detail, double.NaN); if (r.Contains("tls") || r.Contains("peap")) s.Sealed = true; return Rd(s, lv);
        }
        static string OptNo(string o)
        {
            string l = o.Trim().ToLowerInvariant();
            return l.StartsWith("router", StringComparison.Ordinal) ? "option 3" : l.StartsWith("dns", StringComparison.Ordinal) ? "option 6" : l.StartsWith("domain", StringComparison.Ordinal) ? "option 15" : l.StartsWith("lease", StringComparison.Ordinal) ? "option 51" : l.StartsWith("ntp", StringComparison.Ordinal) ? "option 42" : l.StartsWith("tftp", StringComparison.Ordinal) || l.StartsWith("boot", StringComparison.Ordinal) ? "option 66/67" : "";
        }
        static readonly Dictionary<string, string[]> SnmpAsk = new Dictionary<string, string[]> {
            { "snmp", new[] { "sysDescr, sysName, sysUpTime", "what are you?" } }, { "snmpif", new[] { "ifOperStatus, ifInErrors", "how are your ports?" } },
            { "snmptraffic", new[] { "ifHCInOctets, ifHCOutOctets", "how many bytes so far?" } }, { "snmpcpu", new[] { "hrProcessorLoad, hrStorageUsed", "how busy are you?" } },
            { "snmplldp", new[] { "lldpRemSysName, lldpRemPortId", "who is plugged into you?" } }, { "snmpsens", new[] { "entPhySensorValue", "how warm, how bright?" } },
            { "snmpsnap", new[] { "system, interfaces, sensors, LLDP", "tell me everything" } }, { "snmpmac", new[] { "dot1dTpFdbPort", "which port has this MAC?" } },
            { "snmpvlan", new[] { "dot1qVlanStaticName, egress ports", "which VLANs, on which ports?" } }, { "snmppoe", new[] { "pethPsePortPowerConsumption", "who draws power?" } },
            { "snmplag", new[] { "dot3adAggPortAttachedAggID", "which links are bundled?" } }, { "snmpprn", new[] { "prtMarkerSuppliesLevel", "how much toner?" } },
            { "snmpups", new[] { "upsEstimatedMinutesRemaining", "how long on battery?" } }, { "snmpwalk", new[] { "GetNext, GetNext, GetNext...", "the next one after this?" } } };
    }
}
