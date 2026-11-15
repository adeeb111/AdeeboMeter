// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // ================================================================== tool pictures, wave 5 (Adeeb, 15 Nov) - and wave 3's exchanges, from the result
    // Same rule as wave 4: the picture is the result, drawn. Paths hop by hop (who owns each stretch, where the delay starts, the
    // way out vs the way back), lanes that race (DNS servers, cloud regions, the VPN, a console's legs), the staircase a PC climbs to
    // join a network (and the step it stopped on), the doors open on this PC, certificates as ID cards, what changed since a
    // snapshot. Wave 3's exchanges (dig, DoH, DHCP, NTP, ARP, RADIUS/TACACS+, HTTP timing) now carry every value of the result too.
    sealed partial class FullView
    {
        static readonly string[] Arrow = { "→" }, OspfDefault = { "3", "OSPF hello", "from 192.168.1.1 · area 0 · every 10 s" };
        static readonly HashSet<string> Deep3 = new HashSet<string> { "dig", "doh", "dhcp", "ntp", "radius", "tacacs", "dupip", "arpwho", "httptime" };
        static readonly HashSet<string> Pictured5 = new HashSet<string> {
            "ret", "owners", "gpn", "console", "cloud", "dnscmp", "vpn", "mysites", "manyports", "listen", "shares", "gpo", "fw", "join", "ipv6score",
            "rlisten", "gateway", "dnsrules", "icmpexplain", "dc", "hosts", "certfile", "pfx", "csr", "certmatch", "netsnap" };
        static readonly HashSet<string> Still5 = new HashSet<string> { "mysites", "manyports", "listen", "shares", "gpo", "certfile", "pfx", "csr", "certmatch", "netsnap" };

        // ================================================================== wave 3's exchanges, from the whole result
        float DeepExchange(Graphics g, float x, float y, float w, string st)
        {
            var s = new List<XStep>(); string cap = "", foot = ""; string[] who; var res = Res();
            switch (tbSel)
            {
                case "dig":
                    {
                        var a = VisIn(); string name = Or(a[0], "example.com"), type = Or(a.Length > 1 ? a[1] : "", "A").ToUpperInvariant(); if (type == "TRACE") type = "A";
                        string asked = RV("Asked"), srv = a.Length > 2 && a[2] != "" ? a[2] : Or(P(asked, 0), L.T("Your DNS server")); if (srv.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) srv = "DoH " + srv.Substring(8).Split('/')[0];
                        bool signed = asked.Contains("DNSSEC"); var answers = res.Where(o => Regex.IsMatch(o[1], @"^(A|AAAA|CNAME|MX|TXT|NS|SOA|PTR|SRV|CAA|DS|DNSKEY|HTTPS)$")).ToList();
                        var trace = res.Where(o => Regex.IsMatch(o[1], @"^(\.|root|[a-z0-9-]+\.)$", RegexOptions.IgnoreCase) || o[1].StartsWith("Step", StringComparison.OrdinalIgnoreCase)).ToList();
                        string zone = name.Contains(".") ? name.Substring(name.LastIndexOf('.')) : ".com";
                        who = new[] { L.T("This PC"), Cut(srv, 22), L.T("root and") + " " + zone, Cut(name, 18) + L.T("'s servers") };
                        s.Add(X(0, 1, type + " " + L.T("for") + " " + name + "?", "DNS · UDP 53" + (signed ? " · DO (DNSSEC wanted)" : ""), double.NaN));
                        if (trace.Count > 0) foreach (var o in trace.Take(4)) { s.Add(X(1, 2, L.T("ask") + " " + o[1], "", double.NaN)); s.Add(X(2, 1, Cut(o[2], 46), L.T("a referral, not the answer"), RowMs(o[2]))); }
                        else { s.Add(X(1, 2, L.T("who knows") + " " + name + "?", L.T("only if not remembered: the root, then") + " " + zone, double.NaN)); s.Add(X(2, 1, L.T("ask its own servers"), L.T("a referral"), double.NaN)); s.Add(X(1, 3, type + " " + name + "?", "", double.NaN)); }
                        if (answers.Count == 0) s.Add(Rd(X(1, 0, res.Count > 0 ? Cut(res[0][2], 44) : L.T("the answer"), L.T("with how long to keep it (TTL)"), RowMs(asked)), res.Count > 0 ? Lv(res[0]) : -1));
                        foreach (var o in answers.Take(5)) { var z = X(1, 0, o[1] + "  " + Cut(P(o[2], 0), 40), Or(P(o[2], 1), "") + (signed ? " · " + L.T("signature checks out") : ""), s.Count == 1 + (trace.Count > 0 ? trace.Count * 2 : 3) ? RowMs(asked) : double.NaN); z.Sealed = signed; s.Add(Rd(z, Lv(o))); }
                        cap = L.T("WHO ASKS WHOM -") + " " + name.ToUpperInvariant(); foot = (asked != "" ? L.T("Asked") + " " + asked + ". " : "") + L.T("A server that remembers the answer skips the middle steps - repeat lookups take 1-2 ms.");
                    }
                    break;
                case "doh":
                    {
                        string enc = RV("Encrypted DNS"), br = RV("Browser"); bool on = Regex.IsMatch(enc, @"uses|on\b", RegexOptions.IgnoreCase) && !Regex.IsMatch(enc, @"\bnot\b|\boff\b", RegexOptions.IgnoreCase);
                        string srv = Or(Regex.Match(enc, @"\bto\s+([\w.:-]+)").Groups[1].Value, L.T("DNS server")); string how = Regex.IsMatch(enc, "TLS|DoT", RegexOptions.IgnoreCase) ? "DNS over TLS · TCP 853" : "DNS over HTTPS · TCP 443";
                        who = new[] { L.T("This PC"), L.T("Wi-Fi, provider - on the way"), srv };
                        if (on) { s.Add(Lk(X(0, 2, L.T("(sealed question)"), how, double.NaN))); s.Add(Lk(X(2, 0, L.T("(sealed answer)"), L.T("nobody on the way can read or change it"), double.NaN))); }
                        else { s.Add(Rd(X(0, 2, L.T("where is example.com?"), L.T("plain DNS · UDP 53 - readable on the way"), double.NaN), 1)); s.Add(Rd(X(2, 0, L.T("its address"), L.T("readable - and changeable - on the way"), double.NaN), 1)); }
                        if (br != "") { var b = X(0, 2, L.T("the browser asks too"), Cut(br, 70), double.NaN); b.Sealed = br.IndexOf(" on", StringComparison.OrdinalIgnoreCase) >= 0; s.Add(b); }
                        cap = on ? L.T("YOUR PC ASKS IN A SEALED ENVELOPE") : L.T("YOUR PC ASKS ON A POSTCARD"); foot = Or(enc, "Plain DNS shows every name you look up to anyone on the way; encrypted DNS (DoH/DoT) seals it.");
                    }
                    break;
                case "dhcp":
                    {
                        string lease = RV("IPv4 lease"), v6 = RV("IPv6"), gw = RV("Gateway"), dns = RV("DNS"); string ip = Or(P(lease, 0), "192.168.1.20"), srv = Or(Regex.Match(RV("DHCP server") + " " + lease, @"from\s+([\d.]+)|^([\d.]+)").Value.Replace("from ", "").Trim(), L.T("DHCP server (your router)"));
                        who = new[] { L.T("This PC"), srv };
                        s.Add(Bc(0, 1, L.T("Discover: can anyone give me an address?"), L.T("broadcast to everyone · UDP 68 → 67")));
                        s.Add(X(1, 0, L.T("Offer") + ": " + ip, Cut(Or((gw != "" ? L.T("gateway") + " " + gw + " · " : "") + (dns != "" ? "DNS " + dns : ""), L.T("with a mask, a gateway and DNS servers")), 70), double.NaN));
                        s.Add(X(0, 1, L.T("Request: yes please,") + " " + ip, L.T("still broadcast - other servers learn it's taken"), double.NaN));
                        s.Add(Rd(X(1, 0, L.T("Ack: it's yours"), Or(P(lease, 1), L.T("for a lease time - renewed at half time")), double.NaN), RLv("IPv4 lease")));
                        if (v6 != "") { s.Add(Bc(0, 1, L.T("IPv6: Router Solicitation"), "ICMPv6 133")); s.Add(Rd(X(1, 0, P(v6, 0), Cut(P(v6, 1), 60), double.NaN), RLv("IPv6"))); }
                        cap = L.T("HOW THIS PC GOT") + " " + ip; foot = "Halfway through the lease the PC quietly asks to keep it. No answer by the end, and it falls back to a 169.254 address - no internet.";
                    }
                    break;
                case "ntp":
                    {
                        string ans = RV("Time server"), clock = RV("Your clock"); string srv = Or(VisIn()[0], "time.windows.com"); string strat = Regex.Match(ans, @"stratum\s*\d+").Value;
                        who = new[] { L.T("This PC"), Cut(srv, 24), L.T("its own clock source") };
                        s.Add(X(0, 1, L.T("what time is it? (sent at T1)"), "NTP · UDP 123", double.NaN));
                        s.Add(X(1, 2, L.T("it follows a better clock"), Or(strat, "stratum") + " = " + L.T("steps from an atomic clock"), double.NaN));
                        s.Add(Rd(X(1, 0, L.T("got it at T2, answered at T3"), Cut(Or(ans, ""), 60), RowMs(ans)), RLv("Time server")));
                        s.Add(Rd(X(0, 0, Cut(Or(clock, L.T("arrived at T4 - offset worked out")), 44), L.T("offset = ((T2-T1) + (T3-T4)) / 2 - the trip cancels out"), double.NaN), RLv("Your clock")));
                        cap = L.T("ASKING") + " " + srv.ToUpperInvariant() + " " + L.T("THE TIME"); foot = "Sign-ins, certificates and logs break when the clock is more than a few minutes off - Kerberos allows 5.";
                    }
                    break;
                case "radius": case "tacacs":
                    {
                        bool rad = tbSel == "radius"; var row = RRow(rad ? "RADIUS" : "TACACS+"); string v = row != null ? row[2] : ""; string srv = Or(Regex.Match(v, @"^[\w.:-]+").Value, Or(VisIn()[0], rad ? "192.0.2.30" : "192.0.2.31"));
                        bool none = Regex.IsMatch(v, "no answer|timed out|refused", RegexOptions.IgnoreCase); who = new[] { L.T("This PC"), srv };
                        if (rad)
                        {
                            s.Add(X(0, 1, L.T("Access-Request: a test name"), L.T("UDP 1812 · signed with the shared secret"), double.NaN));
                            string got = Regex.Match(v, @"Access-(Accept|Reject|Challenge)").Value;
                            s.Add(Rd(X(1, 0, none ? L.T("(silence)") : Or(got, L.T("an answer")), none ? L.T("wrong shared secret, not a known client, or a firewall") : L.T("any answer means it's alive - a Reject for a made-up name is right"), RowMs(v)), none ? 2 : Lv(row)));
                        }
                        else
                        {
                            s.Add(X(0, 1, L.T("hello (SYN)"), "TCP " + Or(Regex.Match(v, @":(\d+)").Groups[1].Value, "49"), double.NaN));
                            s.Add(Rd(X(1, 0, none ? L.T("(no answer)") : L.T("hello back (SYN-ACK)"), Cut(Or(v, L.T("it accepts connections - the server is there")), 60), RowMs(v)), none ? 2 : Lv(row)));
                            if (!none) s.Add(X(0, 1, L.T("(no sign-in sent)"), L.T("AdeeboMeter stops here - no password"), double.NaN));
                        }
                        cap = rad ? L.T("A RADIUS SERVER, ASKED") : L.T("A TACACS+ SERVER, KNOCKED ON");
                        foot = rad ? "No answer at all usually means a wrong shared secret (the server ignores you) or a firewall - not a down server." : "TACACS+ runs over TCP, so 'is it there' is just a handshake.";
                    }
                    break;
                case "dupip": case "arpwho":
                    {
                        bool clash = tbSel == "dupip"; string me = Or(RV("Your address now"), Or(VisIn()[0], "192.0.2.37")); var ans = RRow("Answered"); string maker = RV("Maker");
                        var events = res.Where(o => Regex.IsMatch(o[1], @"^\d{1,2} \w{3}") && Regex.IsMatch(o[2], @"([0-9A-Fa-f]{2}[-:]){5}")).Take(3).ToList();
                        who = new[] { L.T("This PC"), clash ? L.T("another device") : (ans != null ? Regex.Match(ans[2], @"^[\d.]+").Value : me), L.T("everyone else") };
                        s.Add(Bc(0, 2, L.T("who has") + " " + (clash ? me : Or(ans != null ? Regex.Match(ans[2], @"^[\d.]+").Value : "", me)) + "?", L.T("ARP · broadcast to every device here")));
                        if (clash)
                        {
                            if (events.Count == 0) events.Add(new[] { "2", L.T("one day"), "00-00-5E-00-53-12" });
                            foreach (var e in events) { s.Add(Rd(X(1, 0, L.T("me!") + " " + Regex.Match(e[2], @"([0-9A-Fa-f]{2}[-:]){5}[0-9A-Fa-f]{2}").Value, e[1] + " - " + L.T("another device answers for YOUR address"), double.NaN), 2)); }
                            s.Add(Rd(X(0, 1, L.T("conflict - Windows warns and may stop using it"), Cut(Or(RV("Address conflicts"), ""), 60), double.NaN), RLv("Address conflicts")));
                        }
                        else s.Add(Rd(X(1, 0, L.T("me - at") + " " + Or(Regex.Match(ans != null ? ans[2] : "", @"([0-9A-Fa-f]{2}[-:]){5}[0-9A-Fa-f]{2}").Value, "00:11:32:ab:cd:ef"), Or(maker, "ARP reply"), RowMs(ans != null ? ans[2] : "")), Lv(ans)));
                        cap = clash ? L.T("TWO DEVICES, ONE ADDRESS") + " (" + me + ")" : L.T("ASKING THE NETWORK WHO HAS AN ADDRESS");
                        foot = clash ? "Give the other device an address outside the router's DHCP range, or a reservation." : "ARP turns an IP address into a MAC address - only on your own network; beyond the router it's the router's job.";
                    }
                    break;
                default:   // httptime
                    {
                        var a = VisIn(); string host = a[0] != "" ? Regex.Replace(a[0], @"^[a-z]+://", "", RegexOptions.IgnoreCase).Split('/', ':')[0] : "example.com"; string tries = a.Length > 1 && a[1] != "" ? a[1] : "5";
                        string tot = RV("Total"); double dns = RowMs(RV("DNS")), con = RowMs(RV("Connect")), tls = RowMs(RV("TLS")), fb = RowMs(RV("First byte"));
                        who = new[] { L.T("This PC"), L.T("DNS server"), host };
                        s.Add(X(0, 1, L.T("where is") + " " + host + "?", "DNS · UDP 53", double.NaN)); s.Add(Rd(X(1, 0, L.T("its address"), L.T("DNS answer"), dns), RLv("DNS")));
                        s.Add(X(0, 2, L.T("hello (SYN)"), "TCP 443", double.NaN)); s.Add(Rd(X(2, 0, L.T("hello back (SYN-ACK)"), L.T("the connection is open"), con), RLv("Connect")));
                        s.Add(Lk(X(0, 2, L.T("TLS hello - name") + ": " + host, "ClientHello", double.NaN))); s.Add(Rd(Lk(X(2, 0, L.T("certificate + keys"), "ServerHello · TLS", tls)), RLv("TLS")));
                        s.Add(Lk(X(0, 2, "GET /", L.T("sealed from here on"), double.NaN))); s.Add(Rd(Lk(X(2, 0, L.T("first byte of the page"), L.T("after the server thought about it"), fb)), RLv("First byte")));
                        double sum = new[] { dns, con, tls, fb }.Where(z => !double.IsNaN(z)).Sum(); string biggest = sum <= 0 ? "" : new[] { Tuple.Create(L.T("the name"), dns), Tuple.Create(L.T("the connection"), con), Tuple.Create(L.T("the lock"), tls), Tuple.Create(L.T("the server thinking"), fb) }.Where(z => !double.IsNaN(z.Item2)).OrderByDescending(z => z.Item2).First().Item1;
                        cap = L.T("ONE PAGE LOAD, PACKET BY PACKET") + " · " + tries + " " + L.T("tries");
                        foot = (tot != "" ? L.T("Total") + ": " + tot + ". " : "") + (biggest != "" ? L.T("The biggest share is") + " " + biggest + "." : "A slow first byte is the server thinking, not your line.");
                    }
                    break;
            }
            return ExchangeCard(g, x, y, w, st, cap, who, s, foot);
        }

        // ================================================================== wave 5
        float Wave5(Graphics g, float x, float y, float w, string st)
        {
            var res = Res(); var s = new List<XStep>(); string[] who; string cap, foot;
            switch (tbSel)
            {
                // ---------------------------------------------------------------- roads: hop by hop
                case "owners": case "gpn":
                    {
                        var hops = new List<Tuple<string, string, double, string, int>>();   // hop label, address, ms, owner (band), level
                        foreach (var o in res.Where(o => o[1].StartsWith("Hop ", StringComparison.Ordinal)))
                        {
                            string addr = P(o[1], 1), num = P(o[1], 0).Replace("Hop ", "");
                            if (tbSel == "owners") hops.Add(Tuple.Create(num, addr, double.NaN, Regex.Replace(o[2], @"\s*\(.*\)$", ""), Lv(o)));
                            else hops.Add(Tuple.Create(num, addr, RowMs(o[2]), Or(o.Length > 3 ? Regex.Replace(o[3], @"\s*-.*$", "") : "", "the internet"), Lv(o)));
                        }
                        if (hops.Count == 0) hops = new List<Tuple<string, string, double, string, int>> { Tuple.Create("1", "192.168.1.1", 2.0, "your router", -1), Tuple.Create("2", "198.51.100.1", 8.0, "your provider", -1), Tuple.Create("5", "198.51.100.22", 11.0, "the internet (transit)", -1), Tuple.Create("8", "203.0.113.10", 21.0, "the server", -1) };
                        string why = tbSel == "owners" ? RV("Who to call") : RV("Would a GPN"); var gr = RRow("Would a GPN");
                        return HopRoad(g, x, y, w, st, tbSel == "owners" ? "WHO CARRIES YOUR TRAFFIC, STRETCH BY STRETCH" : Or(res.Where(o => o[0] == "-1").Select(o => o[1]).FirstOrDefault(), "THE WAY TO THE GAME, HOP BY HOP"), hops,
                            tbSel == "owners" ? Or(why, "Each colour is one network (AS). Problems are usually where one hands over to the next.") : (gr != null && gr.Length > 3 ? gr[3] : "The red hop is where the delay starts and carries on - a GPN takes another road around it."));
                    }
                case "ret":
                    {
                        string o1 = RV("Out"), b1 = RV("Back"), diff = RV("Different"); int ho = (int)VNum(o1, @"(\d+)\s*hops"), hb = (int)VNum(b1, @"(\d+)\s*hops");
                        return TwoWays(g, x, y, w, st, ho > 0 ? ho : 9, hb > 0 ? hb : 11, Or(P(o1, 1), L.T("leaves through your provider")), Or(P(b1, 1), L.T("comes back another way")), RLv("Different") >= 1,
                            Or(diff, "The reply often takes another road back. A normal trace only shows the way out - a slow way back hides from it."));
                    }

                // ---------------------------------------------------------------- lanes that race
                case "cloud": case "dnscmp": case "vpn": case "console":
                    {
                        var lanes = new List<Tuple<string, double, double, int>>(); string c2, f2;
                        if (tbSel == "cloud") { foreach (var o in res) lanes.Add(Tuple.Create(P(o[2], 0), RowMs(o[2]), double.NaN, Lv(o))); c2 = "CLOUD REGIONS, RACING FROM HERE"; f2 = "The nearest region answers first - pick it for games, calls and remote desktops."; }
                        else if (tbSel == "dnscmp") { foreach (var o in res.Where(o => RowMs(o[2]) > 0)) lanes.Add(Tuple.Create(o[1] + "  →  " + P(o[2], 0), RowMs(o[2]), double.NaN, Lv(o))); c2 = "TWO DNS SERVERS, THE SAME QUESTION"; f2 = Or(RV("Same answer") != "" ? L.T("Same answer:") + " " + RV("Same answer") : RV("Different"), "Different answers aren't always wrong - big sites answer with the server nearest to whoever asks."); }
                        else if (tbSel == "vpn")
                        {
                            string ins = RV("Inside"); double with = RowMs(ins), without = VNum(ins, @"\(([0-9.]+)\s*ms\s*without");
                            if (!double.IsNaN(without)) lanes.Add(Tuple.Create(L.T("without the VPN"), without, double.NaN, -1));
                            if (!double.IsNaN(with)) lanes.Add(Tuple.Create(L.T("through the VPN"), with, double.NaN, RLv("VPN")));
                            c2 = Or(P(RV("VPN"), 1), "THE VPN").ToUpperInvariant(); f2 = Or(RV("What it means"), "The difference is what the VPN costs every packet.");
                            if (RV("Company proxy") != "") f2 = L.T("Web goes via") + " " + RV("Company proxy").Split(' ')[0] + ". " + f2;
                        }
                        else { foreach (var o in res.Where(o => RowMs(o[2]) > 0 && o[0] != "-1")) lanes.Add(Tuple.Create(o[1], RowMs(o[2]), VNum(o[2], @"jitter\s*([0-9.]+)"), Lv(o))); c2 = "THE FOUR LEGS TO THE GAME"; f2 = Or(RV("Console verdict"), "Blue: the ping. Purple: the jitter (how much it wobbles)."); }
                        return RaceCard(g, x, y, w, st, c2, lanes, f2);
                    }

                // ---------------------------------------------------------------- staircases: how far it got
                case "join": case "ipv6score":
                    {
                        var steps = tbSel == "join" ? res.Where(o => Regex.IsMatch(o[1], @"^\d\.")).ToList() : res.Skip(1).ToList();
                        string head = tbSel == "join" ? RV("Verdict") : RV("IPv6 readiness");
                        return Stairs(g, x, y, w, st, tbSel == "join" ? "HOW FAR THIS PC GOT JOINING THE NETWORK" : "IPV6, STEP BY STEP - " + Or(P(head, 0), "").ToUpperInvariant(), steps, Or(head, tbSel == "join" ? "Each step needs the one before: link, sign-in, address, router." : "Each step needs the one before it."));
                    }

                // ---------------------------------------------------------------- doors and tiles
                case "manyports": case "mysites": case "listen": case "shares": case "gpo":
                    {
                        var tiles = new List<string[]>();   // title, detail, level, group
                        if (tbSel == "manyports") foreach (var o in res.Skip(1)) tiles.Add(new[] { o[1], o[2], Lv(o).ToString(CultureInfo.InvariantCulture), "" });
                        else if (tbSel == "mysites") foreach (var o in res) tiles.Add(new[] { o[1], o[2], Lv(o).ToString(CultureInfo.InvariantCulture), "" });
                        else if (tbSel == "listen") foreach (var o in res.Where(o => o[1].Contains(":"))) { bool all = o[1].StartsWith("0.0.0.0", StringComparison.Ordinal) || o[1].StartsWith("[::]", StringComparison.Ordinal); tiles.Add(new[] { o[1], o[2], all ? "1" : "-1", all ? L.T("open to the network") : L.T("this PC only") }); }
                        else if (tbSel == "shares") foreach (var o in res.Where(o => o[1].StartsWith("\\\\", StringComparison.Ordinal))) tiles.Add(new[] { o[1], o[2], o[1].EndsWith("$", StringComparison.Ordinal) ? "-1" : "0", "" });
                        else foreach (var o in res.Where(o => o[1] == "Computer" || o[1] == "User")) foreach (var p in o[2].Split(Dot, StringSplitOptions.RemoveEmptyEntries)) tiles.Add(new[] { p.Trim(), "", "-1", o[1] == "Computer" ? L.T("for the computer (at start-up)") : L.T("for you (at sign-in)") });
                        string tc = tbSel == "manyports" ? Or(P(RV("Mail ports") + RV(res.Count > 0 ? res[0][1] : "x"), 0), "PORTS, ONE BY ONE") : tbSel == "mysites" ? "YOUR SITES, RIGHT NOW" : tbSel == "listen" ? "THE DOORS OPEN ON THIS PC" : tbSel == "shares" ? "SHARED FOLDERS ON " + Or(VisIn()[0], "THE DEVICE") : "GROUP POLICIES THAT APPLIED - " + Or(RV("Policies applied"), "");
                        string tf = tbSel == "listen" ? "Orange doors answer anyone on the network (firewall permitting); grey ones only this PC. A door you don't recognise: look up the program." : tbSel == "shares" ? "Folders ending in $ are hidden admin shares. You see the names even without access - opening them needs permission." : tbSel == "gpo" ? "Computer policies apply at start-up, user policies at sign-in - gpupdate /force applies both now." : tbSel == "manyports" ? Or(res.Where(o => Lv(o) == 2 && o.Length > 3).Select(o => o[3]).FirstOrDefault(), "Green answers, red is blocked somewhere on the way.") : "Red: doesn't answer from here - check the reason beside it.";
                        return Tiles(g, x, y, w, st, tc.ToUpperInvariant(), tiles, tf);
                    }

                // ---------------------------------------------------------------- certificates as ID cards
                case "certfile": case "pfx": case "csr": case "certmatch":
                    return CertCard(g, x, y, w, st);
                case "netsnap":
                    return Diff(g, x, y, w, st);

                // ---------------------------------------------------------------- exchanges
                case "rlisten":
                    {
                        var heard = res.Skip(1).Take(4).ToList(); who = new[] { L.T("This PC (listening)"), L.T("your segment") }.Concat(heard.Select(o => Or(Regex.Match(o[2], @"from\s+([\d.]+)|master\s+([\d.]+)").Value.Replace("from ", "").Replace("master ", ""), o[1]))).Distinct().Take(4).ToArray();
                        if (heard.Count == 0) { who = new[] { L.T("This PC (listening)"), L.T("your segment"), "192.168.1.1" }; heard.Add(OspfDefault); }
                        foreach (var o in heard) { string src = Regex.Match(o[2], @"from\s+([\d.]+)|master\s+([\d.]+)").Value.Replace("from ", "").Replace("master ", ""); int f = Math.Max(2, Array.IndexOf(who, src)); if (f >= who.Length) f = who.Length - 1; s.Add(Rd(X(f, 1, o[1], Cut(o[2], 60), double.NaN), Lv(o) < 0 && o.Length > 3 && o[3] != "" ? 1 : Lv(o))); s.Add(X(1, 0, L.T("heard here"), o.Length > 3 ? Cut(o[3], 60) : L.T("multicast reaches every device on the segment"), double.NaN)); }
                        cap = Or(RV("Heard in"), "ROUTING PROTOCOLS ON THIS SEGMENT").ToUpperInvariant(); foot = "Listening only - nothing is sent. OSPF, RIP or VRRP visible to a PC means anything plugged in here could join in.";
                    }
                    break;
                case "gateway":
                    {
                        string gw = RV("Your gateway"), bk = RV("Backup"), v6 = RV("IPv6 gateway"); string ip = Or(P(gw, 0), "192.168.1.1");
                        who = new[] { L.T("This PC"), ip + (P(gw, 2) != "" ? " · " + P(gw, 2) : ""), L.T("the internet") };
                        s.Add(Bc(0, 1, L.T("who has") + " " + ip + "?", L.T("ARP - finding the router's MAC")));
                        s.Add(X(1, 0, L.T("me"), L.T("its MAC address"), double.NaN));
                        s.Add(X(0, 1, L.T("ping"), "ICMP", double.NaN)); s.Add(Rd(X(1, 0, L.T("reply"), Or(P(gw, 1), ""), RowMs(gw)), RLv("Your gateway")));
                        s.Add(X(0, 2, L.T("everything not local goes via") + " " + ip, L.T("default route 0.0.0.0/0"), double.NaN));
                        if (v6 != "") s.Add(X(0, 1, "IPv6: " + Cut(v6, 40), L.T("a second way out, for IPv6"), double.NaN));
                        cap = "YOUR WAY OUT: " + ip; foot = Or(bk != "" ? L.T("Backup way out:") + " " + bk : "", "One router, one line - if it stops, everything stops.");
                    }
                    break;
                case "dnsrules":
                    {
                        string hj = RV("DNS hijack"), nr = RV("VPN name rules"); bool hijack = RLv("DNS hijack") == 2;
                        var rm = Regex.Match(nr, @"^(\S+)\s+goes to\s+([\w.:]+)"); who = rm.Success ? new[] { L.T("This PC"), L.T("your DNS server"), L.T("VPN DNS") + " " + rm.Groups[2].Value } : new[] { L.T("This PC"), L.T("your DNS server") };
                        s.Add(X(0, 1, L.T("where is") + " no-such-name-7f3a.example?", L.T("a made-up name, on purpose"), double.NaN));
                        s.Add(Rd(X(1, 0, hijack ? L.T("an address! (it shouldn't have one)") : L.T("no such name"), Cut(Or(hj, L.T("empty - as it should be")), 60), double.NaN), hijack ? 2 : RLv("DNS hijack")));
                        if (rm.Success) { s.Add(X(0, 2, L.T("where is") + " " + rm.Groups[1].Value.Replace("*.", "intranet.") + "?", L.T("matches a VPN name rule"), double.NaN)); s.Add(X(2, 0, L.T("the company's own answer"), L.T("Name Resolution Policy"), double.NaN)); }
                        cap = "WHO ANSWERS YOUR NAMES"; foot = "A server that answers made-up names is redirecting you (ads, a captive portal, a filter). VPN rules send company names to company DNS.";
                    }
                    break;
                case "icmpexplain":
                    {
                        string code = Or(VisIn()[0], "3/4"); var row = res.FirstOrDefault(); string text = row != null ? row[2] : "";
                        who = new[] { L.T("This PC"), L.T("a router on the way"), L.T("destination") };
                        s.Add(X(0, 1, L.T("your packet"), "", double.NaN));
                        s.Add(Rd(X(1, 0, "ICMP " + code + " - " + Cut(Regex.Replace(text, @"\s*-.*$", ""), 34), Cut(Regex.Match(text, @"-\s*(.*?)(\.|$)").Groups[1].Value, 70), double.NaN), row != null ? Lv(row) : 1));
                        if (code.StartsWith("3/4", StringComparison.Ordinal)) { s.Add(X(0, 2, L.T("the same, smaller"), L.T("fits the size the router named"), double.NaN)); s.Add(X(2, 0, L.T("answer"), "", double.NaN)); }
                        cap = "ICMP " + code + ": " + L.T("WHAT THE ROUTER WAS TELLING YOU"); foot = Cut(text, 200);
                    }
                    break;
                case "dc":
                    {
                        string dom = RV("Your domain"), via = RV("Signed in through"); string dname = Or(Regex.Match(dom, @"\(([^)]+)\)").Groups[1].Value, "example.net"), dc = Or(P(via, 0), "dc01." + dname);
                        who = new[] { L.T("This PC"), L.T("DNS"), dc + (P(via, 1) != "" ? " · " + P(via, 1) : "") };
                        s.Add(X(0, 1, "_ldap._tcp." + Or(Regex.Replace(P(via, 2), "^site ", ""), "Default-First-Site") + "._sites.dc._msdcs." + dname + "?", L.T("DCs in my site first"), double.NaN));
                        s.Add(Rd(X(1, 0, dc, Or(P(via, 2), ""), double.NaN), RLv("Signed in through")));
                        s.Add(X(0, 2, L.T("LDAP ping - are you there, which site am I in?"), "UDP 389", double.NaN)); s.Add(X(2, 0, L.T("yes -") + " " + Or(P(via, 2), L.T("your site")), "", double.NaN));
                        s.Add(Lk(X(0, 2, L.T("Kerberos: sign me in"), "TCP/UDP 88", double.NaN))); s.Add(Lk(X(2, 0, L.T("a ticket"), L.T("good for 10 h"), double.NaN)));
                        cap = "HOW THIS PC FINDS ITS DOMAIN CONTROLLER - " + Or(dom, "").ToUpperInvariant(); foot = "A DC in another site works but is slower; a PC that can't find one signs in with cached credentials only.";
                    }
                    break;
                case "fw":
                    {
                        string app = Or(VisIn()[0], "the app"); var main = res.FirstOrDefault(); string blk = RV("Blocked"); who = new[] { L.T("the network"), L.T("Windows Firewall"), app };
                        s.Add(X(0, 1, L.T("a connection in"), L.T("for") + " " + app, double.NaN));
                        s.Add(Rd(X(1, 2, main != null && Lv(main) == 2 ? L.T("blocked") : L.T("allowed"), Cut(main != null ? main[2] : L.T("a rule matches"), 70), double.NaN), main != null ? Lv(main) : -1));
                        if (blk != "" && !blk.StartsWith("none", StringComparison.OrdinalIgnoreCase)) s.Add(Rd(X(1, 0, L.T("blocked"), Cut(blk, 70), double.NaN), 2));
                        s.Add(X(2, 0, L.T("its answers go out"), L.T("outgoing is allowed unless a rule says no"), double.NaN));
                        cap = "WINDOWS FIREWALL AND " + app.ToUpperInvariant(); foot = "Rules are per network type: Private, Domain, Public. An app allowed on Private is still blocked on café Wi-Fi (Public).";
                    }
                    break;
                default:   // hosts
                    {
                        string name = Or(VisIn()[0], "example.com"), hf = RV("Hosts file"); var ents = res.Where(o => Regex.IsMatch(o[1], @"^[\d.:a-f]+$")).ToList(); var hit = ents.FirstOrDefault(o => o[2].Split(' ').Contains(name));
                        who = new[] { L.T("This PC"), L.T("hosts file"), L.T("DNS cache"), L.T("DNS server") };
                        s.Add(X(0, 1, L.T("anything for") + " " + name + "?", Cut(Or(hf, ""), 60), double.NaN));
                        if (hit != null) s.Add(Rd(X(1, 0, L.T("yes:") + " " + hit[1], L.T("DNS is never asked - this line wins"), double.NaN), 1));
                        else { s.Add(X(1, 0, L.T("nothing"), L.T("entries:") + " " + Cut(string.Join(", ", ents.Select(o => o[2]).ToArray()), 50), double.NaN)); s.Add(X(0, 2, L.T("remembered?"), "", double.NaN)); s.Add(X(0, 3, L.T("where is") + " " + name + "?", "UDP 53", double.NaN)); s.Add(X(3, 0, L.T("its address"), "", double.NaN)); }
                        cap = "WHERE WINDOWS LOOKS FIRST"; foot = "The hosts file beats DNS. Malware and old test setups leave lines here that quietly send a site somewhere else.";
                    }
                    break;
            }
            return ExchangeCard(g, x, y, w, st, cap, who, s, foot);
        }

        // ================================================================== the drawings wave 5 adds
        // a road of hops: the ms above each one, a coloured band per owner, the hop where the delay starts in red, a packet driving it
        static float HopRoad(Graphics g, float x, float y, float w, string st, string cap, List<Tuple<string, string, double, string, int>> hops, string foot)
        {
            PhoneP = Tuple.Create(cap, hops.ToList()); PhoneKind = "p";
            hops = hops.Take(10).ToList(); bool hasMs = hops.Any(h => !double.IsNaN(h.Item3)); var r = VisCard(g, x, y, w, (hasMs ? 250 : 190) * S, cap, st);
            float x0 = r.X + 50 * S, x1 = r.Right - 50 * S, cy = r.Y + (hasMs ? 120 : 64) * S, step = (x1 - x0) / Math.Max(1, hops.Count - 1);
            double max = hasMs ? hops.Where(h => !double.IsNaN(h.Item3)).Max(h => h.Item3) : 1; if (max <= 0) max = 1;
            var owners = hops.Select(h => h.Item4).Distinct().ToList(); Color[] band = { Theme.Blue, VPurple, Theme.Orange, Theme.Green, VSlate, Theme.Amber };
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < hops.Count - 1; i++) using (var p = new Pen(Color.FromArgb(150, band[owners.IndexOf(hops[i + 1].Item4) % band.Length]), 8 * S)) g.DrawLine(p, x0 + step * i, cy, x0 + step * (i + 1), cy);
            for (int i = 0; i < hops.Count; i++)
            {
                var h = hops[i]; float hx = x0 + step * i; Color c = h.Item5 == 2 ? Theme.Red : h.Item5 == 1 ? Theme.Amber : band[owners.IndexOf(h.Item4) % band.Length];
                if (!double.IsNaN(h.Item3)) { float bh = (float)(h.Item3 / max) * 60 * S; Theme.Fill(g, new RectangleF(hx - 7 * S, cy - 22 * S - bh, 14 * S, bh), 3 * S, Color.FromArgb(h.Item5 == 2 ? 230 : 150, c)); VTextC(g, h.Item3.ToString("0", CultureInfo.CurrentCulture) + " ms", Draw.fTiny, h.Item5 == 2 ? Theme.Red : Theme.Sub, hx, cy - 38 * S - bh, 60 * S); }
                using (var b = new SolidBrush(Theme.Card)) g.FillEllipse(b, hx - 9 * S, cy - 9 * S, 18 * S, 18 * S); using (var p = new Pen(c, 3 * S)) g.DrawEllipse(p, hx - 9 * S, cy - 9 * S, 18 * S, 18 * S);
                VTextC(g, h.Item1, Draw.fTiny, Theme.Ink, hx, cy - 6 * S, 30 * S);
                VTextC(g, h.Item2, Draw.fTiny, Theme.Sub, hx, cy + 14 * S + (i % 2) * 13 * S, step + 30 * S);
            }
            // the owners, as a legend with drawn swatches
            float lx = r.X + 18 * S, ly = cy + 50 * S;
            foreach (var o in owners) { var c = band[owners.IndexOf(o) % band.Length]; Theme.Fill(g, new RectangleF(lx, ly + 3 * S, 14 * S, 8 * S), 3 * S, c); var ts = g.MeasureString(o, Draw.fSmall); VText(g, o, Draw.fSmall, Theme.Sub, lx + 18 * S, ly - 1 * S); lx += 32 * S + ts.Width; if (lx > r.Right - 160 * S) { lx = r.X + 18 * S; ly += 18 * S; } }
            if (WindowsAnimates() && hops.Count > 1)
            {
                float f = Wrap((float)AnimT * 0.35f, 1f) * (hops.Count - 1); int seg = Math.Min(hops.Count - 2, (int)f); float px = x0 + step * f;
                Theme.Fill(g, new RectangleF(px - 7 * S, cy - 5 * S, 14 * S, 10 * S), 3 * S, hops[seg + 1].Item5 == 2 ? Theme.Red : Theme.Ink);
            }
            g.SmoothingMode = sm; VFoot(g, r, foot); return r.Bottom + 14 * S;
        }
        // the way out and the way back: two roads that differ, a packet out on one and home on the other
        static float TwoWays(Graphics g, float x, float y, float w, string st, int hopsOut, int hopsBack, string outNote, string backNote, bool differ, string foot)
        {
            var r = VisCard(g, x, y, w, 220 * S, "THE WAY THERE AND THE WAY BACK", st);
            float x0 = r.X + 70 * S, x1 = r.Right - 70 * S, cy = r.Y + 100 * S, up = cy - 34 * S, dn = cy + 34 * S;
            VNode(g, x0, cy, 18 * S, "devices", Theme.Blue, true); VNode(g, x1, cy, 18 * S, "dest", Theme.Green, true);
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(Theme.Blue, 3 * S)) g.DrawBezier(p, x0 + 20 * S, cy, x0 + 80 * S, up, x1 - 80 * S, up, x1 - 20 * S, cy);
            using (var p = new Pen(differ ? Theme.Amber : Theme.Green, 3 * S)) g.DrawBezier(p, x1 - 20 * S, cy, x1 - 80 * S, dn, x0 + 80 * S, dn, x0 + 20 * S, cy);
            for (int i = 1; i <= hopsOut; i++) { var pt = Bez(new PointF(x0 + 20 * S, cy), new PointF(x0 + 80 * S, up), new PointF(x1 - 80 * S, up), new PointF(x1 - 20 * S, cy), i / (float)(hopsOut + 1)); using (var b = new SolidBrush(Theme.Blue)) g.FillEllipse(b, pt.X - 3 * S, pt.Y - 3 * S, 6 * S, 6 * S); }
            for (int i = 1; i <= hopsBack; i++) { var pt = Bez(new PointF(x1 - 20 * S, cy), new PointF(x1 - 80 * S, dn), new PointF(x0 + 80 * S, dn), new PointF(x0 + 20 * S, cy), i / (float)(hopsBack + 1)); using (var b = new SolidBrush(differ ? Theme.Amber : Theme.Green)) g.FillEllipse(b, pt.X - 3 * S, pt.Y - 3 * S, 6 * S, 6 * S); }
            VTextC(g, L.T("out:") + " " + hopsOut + " " + L.T("hops") + " · " + outNote, Draw.fSmall, Theme.Ink, (x0 + x1) / 2, up - 26 * S, x1 - x0);
            VTextC(g, L.T("back:") + " " + hopsBack + " " + L.T("hops") + " · " + backNote, Draw.fSmall, Theme.Ink, (x0 + x1) / 2, dn + 10 * S, x1 - x0);
            if (WindowsAnimates())
            {
                float f = Wrap((float)AnimT * 0.4f, 2f); PointF pt = f < 1 ? Bez(new PointF(x0 + 20 * S, cy), new PointF(x0 + 80 * S, up), new PointF(x1 - 80 * S, up), new PointF(x1 - 20 * S, cy), f) : Bez(new PointF(x1 - 20 * S, cy), new PointF(x1 - 80 * S, dn), new PointF(x0 + 80 * S, dn), new PointF(x0 + 20 * S, cy), f - 1);
                Theme.Fill(g, new RectangleF(pt.X - 7 * S, pt.Y - 5 * S, 14 * S, 10 * S), 3 * S, Theme.Ink);
            }
            g.SmoothingMode = sm; VFoot(g, r, foot); return r.Bottom + 14 * S;
        }
        // a staircase: each step needs the one below; the climber stops at the first red step, the steps above stay grey
        static float Stairs(Graphics g, float x, float y, float w, string st, string cap, List<string[]> steps, string foot)
        {
            if (steps.Count == 0) steps = new List<string[]> { new[] { "0", L.T("1. Link"), "" }, new[] { "0", L.T("2. Sign-in"), "" }, new[] { "0", L.T("3. Address"), "" }, new[] { "0", L.T("4. Router"), "" } };
            steps = steps.Take(6).ToList(); var r = VisCard(g, x, y, w, 70 * S + steps.Count * 40 * S + 44 * S, cap, st);
            int stop = steps.FindIndex(o => Lv(o) == 2); float sx = r.X + 24 * S, sw = (r.Width - 48 * S) * 0.32f, top = r.Y + 44 * S, n = steps.Count;
            bool live = WindowsAnimates(); float climb = live ? Wrap((float)AnimT * 0.8f, n + 1.5f) : n;
            int at = (int)Math.Min(climb, stop < 0 ? n - 1 : stop);
            for (int i = 0; i < n; i++)
            {
                var o = steps[i]; int lv = Lv(o); bool reached = stop < 0 || i <= stop; Color c = !reached ? Theme.Line : lv == 2 ? Theme.Red : lv == 1 ? Theme.Amber : Theme.Green;
                float ry = top + (n - 1 - i) * 40 * S, rx = sx + i * 18 * S;
                Theme.Fill(g, new RectangleF(rx, ry, sw, 32 * S), 6 * S, Color.FromArgb(reached ? 60 : 25, c)); using (var p = new Pen(c, 2 * S)) g.DrawLine(p, rx, ry + 32 * S, rx + sw, ry + 32 * S);
                VText(g, o[1], Draw.fBodyB, reached ? Theme.Ink : Theme.Muted, rx + 10 * S, ry + 7 * S);
                using (var b = new SolidBrush(reached ? (lv == 2 ? Theme.Red : Theme.Sub) : Theme.Muted)) g.DrawString(Cut(o[2], 90), Draw.fSmall, b, new RectangleF(rx + sw + 14 * S, ry + 8 * S, r.Right - rx - sw - 30 * S, 20 * S));
                if (reached && i == at) Kit.Icon(g, "devices", new RectangleF(rx + sw - 26 * S, ry + 7 * S, 18 * S, 18 * S), c);
            }
            VFoot(g, r, foot); return r.Bottom + 14 * S;
        }
        // tiles: one per port / door / site / folder / policy, grouped when the result groups them
        static float Tiles(Graphics g, float x, float y, float w, string st, string cap, List<string[]> tiles, string foot)
        {
            if (tiles.Count == 0) tiles.Add(new[] { L.T("(run it to see them)"), "", "-1", "" });
            tiles = tiles.Take(18).ToList(); var groups = tiles.Select(t => t[3]).Distinct().ToList(); int cols = 3; float tw = (w - 36 * S - (cols - 1) * 10 * S) / cols;
            int rowsN = groups.Sum(gr => (tiles.Count(t => t[3] == gr) + cols - 1) / cols); float h = 48 * S + rowsN * 62 * S + groups.Count(z => z != "") * 22 * S + 44 * S;
            var r = VisCard(g, x, y, w, h, cap, st); float ty = r.Y + 42 * S;
            foreach (var gr in groups)
            {
                if (gr != "") { VText(g, gr.ToUpperInvariant(), Draw.fTiny, Theme.Sub, r.X + 18 * S, ty); ty += 20 * S; }
                int i = 0;
                foreach (var t in tiles.Where(z => z[3] == gr))
                {
                    int lv; if (!int.TryParse(t[2], out lv)) lv = -1; Color c = lv == 2 ? Theme.Red : lv == 1 ? Theme.Orange : lv == 0 ? Theme.Green : VSlate;
                    var tr = new RectangleF(r.X + 18 * S + (i % cols) * (tw + 10 * S), ty + (i / cols) * 62 * S, tw, 54 * S);
                    Theme.Fill(g, tr, 8 * S, Color.FromArgb(28, c)); Theme.Fill(g, new RectangleF(tr.X, tr.Y, 4 * S, tr.Height), 2 * S, c);
                    using (var b = new SolidBrush(Theme.Ink)) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(t[0], Draw.fBodyB, b, new RectangleF(tr.X + 12 * S, tr.Y + 7 * S, tr.Width - 18 * S, 20 * S), sf);
                    using (var b = new SolidBrush(lv == 2 ? Theme.Red : Theme.Sub)) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter }) g.DrawString(t[1], Draw.fSmall, b, new RectangleF(tr.X + 12 * S, tr.Y + 27 * S, tr.Width - 18 * S, 26 * S), sf);
                    i++;
                }
                ty += (i + cols - 1) / cols * 62 * S;
            }
            VFoot(g, r, foot); return r.Bottom + 14 * S;
        }
        // a certificate (or a request, a PFX, a key match) as an ID card: who, who vouches, the dates as a bar with today on it
        float CertCard(Graphics g, float x, float y, float w, string st)
        {
            var res = Res(); string subj = Or(RV("Subject"), Or(P(RV("CSR"), 0), Or(P(RV("Certificate"), 0), "CN=www.example.com"))), iss = RV("Issued by"), valid = Or(RV("Valid"), RV("Certificate")), key = Or(RV("Key"), P(RV("CSR"), 1)), san = Or(RV("Names"), "");
            bool csr = tbSel == "csr", match = tbSel == "certmatch", pfx = tbSel == "pfx";
            var r = VisCard(g, x, y, w, 250 * S, csr ? "THE REQUEST - WHAT YOU'RE ASKING A CA TO SIGN" : match ? "DOES THIS CERTIFICATE BELONG TO YOUR KEY?" : pfx ? "INSIDE THE PFX" : "THE CERTIFICATE AS AN ID CARD", st);
            var card = new RectangleF(r.X + 18 * S, r.Y + 40 * S, r.Width * 0.55f, 150 * S); Theme.Fill(g, card, 12 * S, Theme.Soft); Theme.Fill(g, new RectangleF(card.X, card.Y, card.Width, 30 * S), 12 * S, csr ? VSlate : VPurple);
            VText(g, csr ? L.T("REQUEST (not signed yet)") : L.T("CERTIFICATE"), Draw.fBodyB, Color.White, card.X + 14 * S, card.Y + 6 * S);
            Kit.Icon(g, "lock", new RectangleF(card.Right - 30 * S, card.Y + 6 * S, 18 * S, 18 * S), Color.White);
            float ly = card.Y + 38 * S; Action<string, string> line = (k, v) => { if (v == "") return; VText(g, k, Draw.fTiny, Theme.Sub, card.X + 14 * S, ly + 2 * S); using (var b = new SolidBrush(Theme.Ink)) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(v, Draw.fSmall, b, new RectangleF(card.X + 90 * S, ly, card.Width - 100 * S, 18 * S), sf); ly += 21 * S; };
            line(L.T("for"), subj); line(L.T("names"), san); line(L.T("vouched by"), iss != "" ? iss : csr ? L.T("nobody yet - a CA signs it") : ""); line(L.T("key"), key);
            line(pfx ? L.T("inside") : L.T("status"), pfx ? Or(P(RV("PFX"), 1), "") + (RV("Chain inside") != "" ? " · " + RV("Chain inside") : "") : match ? RV("Match") : csr ? P(RV("CSR"), 2) : "");
            // the dates as a bar, today marked
            var dm = Regex.Matches(valid, @"(\d{4}-\d{2}-\d{2})"); DateTime d0, d1; float bx = card.Right + 24 * S, bw = r.Right - bx - 24 * S, by = card.Y + 40 * S;
            if (dm.Count >= 2 && DateTime.TryParse(dm[0].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out d0) && DateTime.TryParse(dm[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out d1) && d1 > d0)
            {
                VText(g, L.T("valid"), Draw.fTiny, Theme.Sub, bx, by - 18 * S); Theme.Fill(g, new RectangleF(bx, by, bw, 12 * S), 6 * S, Theme.Line);
                float f = (float)Math.Max(0, Math.Min(1, (DateTime.Now - d0).TotalDays / (d1 - d0).TotalDays)); double left = (d1 - DateTime.Now).TotalDays;
                Theme.Fill(g, new RectangleF(bx, by, bw * f, 12 * S), 6 * S, left < 0 ? Theme.Red : left < 30 ? Theme.Amber : Theme.Green);
                using (var p = new Pen(Theme.Ink, 2 * S)) g.DrawLine(p, bx + bw * f, by - 4 * S, bx + bw * f, by + 16 * S);
                VText(g, dm[0].Value, Draw.fTiny, Theme.Sub, bx, by + 18 * S); var e = g.MeasureString(dm[1].Value, Draw.fTiny); VText(g, dm[1].Value, Draw.fTiny, Theme.Sub, bx + bw - e.Width, by + 18 * S);
                VTextC(g, left < 0 ? L.T("expired") : ((int)left).ToString(CultureInfo.CurrentCulture) + " " + L.T("days left"), Draw.fBodyB, left < 30 ? Theme.Red : Theme.Ink, bx + bw / 2, by + 40 * S, bw);
            }
            else
            {
                var head = res.FirstOrDefault(); int lv = Lv(head);
                if (head != null) { Theme.Fill(g, new RectangleF(bx, by - 10 * S, bw, 70 * S), 10 * S, Color.FromArgb(30, lv == 2 ? Theme.Red : lv == 1 ? Theme.Amber : Theme.Green)); using (var b = new SolidBrush(Theme.Ink)) g.DrawString(head[1] + ": " + head[2], Draw.fSmall, b, new RectangleF(bx + 10 * S, by - 2 * S, bw - 20 * S, 60 * S)); }
            }
            if (match) { using (var p = new Pen(Theme.Green, 2 * S) { DashStyle = DashStyle.Dash }) g.DrawLine(p, card.Right, card.Y + 75 * S, bx, by + 100 * S); Kit.Icon(g, "key", new RectangleF(bx, by + 90 * S, 22 * S, 22 * S), RLv("Match") == 2 ? Theme.Red : Theme.Green); VText(g, L.T("your key"), Draw.fSmall, Theme.Sub, bx + 28 * S, by + 93 * S); }
            VFoot(g, r, csr ? "A CSR carries your public key and the names you want; the CA checks you own them and signs. The private key never leaves your PC." : match ? "A certificate belongs to a key when its public key is that key's public half." : Or(res.Where(o => o[1] == "Certificate" || o[1] == "PFX").Select(o => o[2]).FirstOrDefault(), "The name, the dates and who vouches - an app checks all three."));
            return r.Bottom + 14 * S;
        }
        // what changed since a snapshot: before → after, a row each
        float Diff(Graphics g, float x, float y, float w, string st)
        {
            var ch = Res().Where(o => o[2].Contains("→")).Take(6).ToList(); string head = RV("Since the snapshot");
            if (ch.Count == 0) ch.Add(new[] { "3", L.T("(no snapshot yet)"), L.T("save one")  + " → " + L.T("compare later") });
            var r = VisCard(g, x, y, w, 56 * S + ch.Count * 44 * S + 44 * S, "WHAT CHANGED SINCE THE SNAPSHOT", st); float ry = r.Y + 44 * S, cw = (r.Width - 36 * S - 40 * S) / 2;
            foreach (var o in ch)
            {
                var parts = o[2].Split(Arrow, StringSplitOptions.None); int lv = Lv(o);
                VText(g, o[1], Draw.fTiny, Theme.Sub, r.X + 18 * S, ry - 2 * S);
                var a = new RectangleF(r.X + 18 * S, ry + 12 * S, cw, 24 * S); var b = new RectangleF(a.Right + 40 * S, ry + 12 * S, cw, 24 * S);
                Theme.Fill(g, a, 6 * S, Theme.Soft); Theme.Fill(g, b, 6 * S, Color.FromArgb(40, lv >= 1 ? Theme.Amber : Theme.Green));
                VText(g, Cut(parts[0].Trim(), 40), Draw.fSmall, Theme.Sub, a.X + 8 * S, a.Y + 4 * S); VText(g, Cut(parts.Length > 1 ? parts[1].Trim() : "", 40), Draw.fBodyB, Theme.Ink, b.X + 8 * S, b.Y + 3 * S);
                using (var p = new Pen(Theme.Ink, 2 * S)) { p.CustomEndCap = Cap45; g.DrawLine(p, a.Right + 8 * S, a.Y + 12 * S, b.X - 8 * S, b.Y + 12 * S); }
                ry += 44 * S;
            }
            VFoot(g, r, Or(head, "Settings at the snapshot on the left, now on the right.")); return r.Bottom + 14 * S;
        }
    }
}
