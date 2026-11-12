// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AdeeboMeter
{
    // Learn › Games: an escape room, catch the thief, packet detective and two quick-fire rounds. Everything here is made up:
    // a simulated network answers the simulated commands - nothing real is sent, read or changed. The first solve of each
    // scenario earns Adeebo Points (game.<kind>.<id>), so replaying can't farm them.
    static class LearnGames
    {
        public static readonly string[][] All = {
            new[] { "room", "Network escape room", "Something is broken. Ten minutes, a console, and your wits.", "10 scenarios" },
            new[] { "thief", "Catch the thief", "Someone is on your network who shouldn't be. Read the evidence, point at them.", "5 cases" },
            new[] { "detective", "Packet detective", "One packet in the conversation is wrong. Spot it.", "10 puzzles" },
            new[] { "ports", "Port quick-fire", "Ten questions, sixty seconds: which port does it use?", "8 right to win" },
            new[] { "addr", "Address quick-fire", "Private, public, loopback, CGNAT? Ten addresses, sixty seconds.", "8 right to win" } };
        public static int Pts(string key) { return key.StartsWith("game.room.", StringComparison.Ordinal) ? 4 : 3; }
        public static IEnumerable<string> Keys()
        {
            foreach (var f in Faults) yield return "game.room." + f.Id;
            foreach (var c in ThiefCases) yield return "game.thief." + c;
            for (int i = 0; i < Detective.Length; i++) yield return "game.detective." + (i + 1).ToString(CultureInfo.InvariantCulture);
            yield return "game.quick.ports"; yield return "game.quick.addr";
        }
        public static bool Known(string key) { return Keys().Contains(key); }
        static string[] Shuffle(Random r, IEnumerable<string> s) { return s.OrderBy(x => r.Next()).ToArray(); }

        // ================================================================== escape room
        public sealed class Fault { public string Id, Name, Fix, Hint1, Hint2; public string[] WrongFixes; }
        public static readonly Fault[] Faults = {
            new Fault { Id = "dns", Name = "The DNS server is down", Fix = "Use another DNS server (1.1.1.1, for example) - or restart the router, which is the DNS server here", Hint1 = "Addresses work. Names don't.", Hint2 = "Compare ping 1.1.1.1 with ping example.com, then ask nslookup.", WrongFixes = new[] { "Lower the MTU", "Remove a line from the hosts file", "Move closer to the Wi-Fi" } },
            new Fault { Id = "hosts", Name = "A hosts-file line sends one site to the wrong place", Fix = "Remove the line from the hosts file (Notepad as administrator)", Hint1 = "nslookup asks the DNS server. ping uses Windows' own list first.", Hint2 = "Open the hosts file.", WrongFixes = new[] { "Change the DNS server", "Turn off the proxy", "Renew the DHCP lease" } },
            new Fault { Id = "mtu", Name = "An MTU black hole: big packets vanish", Fix = "Lower the MTU (e.g. 1400) or ask the provider - small packets pass, big ones are dropped silently", Hint1 = "Small things work. Big things hang.", Hint2 = "Try the don't-fragment ping at full size.", WrongFixes = new[] { "Change the DNS server", "Turn off the proxy", "Restart the PC" } },
            new Fault { Id = "rogue", Name = "A rogue DHCP server handed out the addresses", Fix = "Find and unplug the device at that address - a second router or a misconfigured box giving out leases", Hint1 = "Who gave you your router and DNS addresses?", Hint2 = "Look at ipconfig's DHCP server, then arp -a.", WrongFixes = new[] { "Change the Wi-Fi channel", "Lower the MTU", "Remove a line from the hosts file" } },
            new Fault { Id = "dupip", Name = "Another device has the same IP address", Fix = "Give the other device an address outside the DHCP range (or a reservation) - then renew", Hint1 = "Something answers some of the time.", Hint2 = "The event log keeps a note when Windows sees it.", WrongFixes = new[] { "Change the DNS server", "Move closer to the Wi-Fi", "Turn off the proxy" } },
            new Fault { Id = "gateway", Name = "The default gateway is set wrong", Fix = "Set the gateway to the real router (or switch the adapter back to automatic / DHCP)", Hint1 = "The router you're told to use doesn't answer.", Hint2 = "Compare ipconfig's gateway with what arp -a sees.", WrongFixes = new[] { "Change the DNS server", "Lower the MTU", "Remove a line from the hosts file" } },
            new Fault { Id = "apipa", Name = "DHCP failed - no address from the router", Fix = "Restart the router (or check its DHCP server is on), then renew the address", Hint1 = "Look at the address itself.", Hint2 = "169.254 means Windows gave itself one.", WrongFixes = new[] { "Change the DNS server", "Turn off the proxy", "Lower the MTU" } },
            new Fault { Id = "portal", Name = "A sign-in page (captive portal) is in the way", Fix = "Open a browser to an http:// site and accept the network's sign-in page", Hint1 = "Pings work. The web shows something else.", Hint2 = "Open the website and read what comes back.", WrongFixes = new[] { "Change the DNS server", "Lower the MTU", "Renew the DHCP lease" } },
            new Fault { Id = "proxy", Name = "A proxy that doesn't exist is set", Fix = "Turn the proxy off (Settings > Network > Proxy) - and check for adware that set it", Hint1 = "Everything answers - except the browser.", Hint2 = "Look at the proxy settings.", WrongFixes = new[] { "Change the DNS server", "Remove a line from the hosts file", "Lower the MTU" } },
            new Fault { Id = "wifi", Name = "Weak, crowded Wi-Fi", Fix = "Move closer, switch to 5 GHz, or use a cable", Hint1 = "Even the router is slow and drops pings.", Hint2 = "Read the Wi-Fi status.", WrongFixes = new[] { "Change the DNS server", "Turn off the proxy", "Remove a line from the hosts file" } } };
        public static readonly string[][] Commands = {
            new[] { "ipconfig", "ipconfig /all" }, new[] { "pinggw", "ping the router" }, new[] { "ping1", "ping 1.1.1.1" }, new[] { "pingname", "ping example.com" },
            new[] { "nslookup", "nslookup example.com" }, new[] { "tracert", "tracert 1.1.1.1" }, new[] { "pingdf", "ping -f -l 1472 1.1.1.1" }, new[] { "arp", "arp -a" },
            new[] { "wifi", "Wi-Fi status" }, new[] { "proxy", "proxy settings" }, new[] { "hosts", "hosts file" }, new[] { "events", "event log (network)" }, new[] { "web", "open https://example.com" } };
        public sealed class Room
        {
            public Fault F; public Random Rnd; public int Net, Pc, Gw, Rogue; public string PcMac, GwMac, RogueMac; public double Base;
            public string[] FaultChoices, FixChoices;
            public string Ip(int host) { return "192.168." + Net + "." + host; }
        }
        public static Room NewRoom(int seed, string faultId)
        {
            var r = new Random(seed); var f = Faults.FirstOrDefault(z => z.Id == faultId) ?? Faults[r.Next(Faults.Length)];
            var nets = new[] { 0, 1, 10, 50, 68, 178 };
            var room = new Room { F = f, Rnd = r, Net = nets[r.Next(nets.Length)], Pc = 20 + r.Next(170), Gw = r.Next(3) == 0 ? 254 : 1, Base = 9 + r.Next(25) };
            room.Rogue = room.Gw == 1 ? 66 + r.Next(30) : 100 + r.Next(30);
            room.PcMac = Mac(r, "3c:52:82"); room.GwMac = Mac(r, "f4:f2:6d"); room.RogueMac = Mac(r, "b8:27:eb");
            var others = Faults.Where(z => z.Id != f.Id).OrderBy(z => r.Next()).Take(4).Select(z => z.Name).ToList(); others.Add(f.Name);
            room.FaultChoices = Shuffle(r, others); room.FixChoices = Shuffle(r, f.WrongFixes.Concat(new[] { f.Fix }));
            return room;
        }
        static string Mac(Random r, string oui) { return oui + ":" + r.Next(256).ToString("x2", CultureInfo.InvariantCulture) + ":" + r.Next(256).ToString("x2", CultureInfo.InvariantCulture) + ":" + r.Next(256).ToString("x2", CultureInfo.InvariantCulture); }
        // the simulated console: what the command prints on this broken network
        public static string Run(Room m, string cmd)
        {
            var r = m.Rnd; var sb = new StringBuilder(); string id = m.F.Id;
            string gw = id == "gateway" ? m.Ip(m.Gw == 1 ? 254 : 1) : id == "rogue" ? m.Ip(m.Rogue) : m.Ip(m.Gw);
            string pc = id == "apipa" ? "169.254." + (m.Pc % 200 + 20) + "." + (m.Pc * 7 % 250 + 2) : m.Ip(m.Pc);
            string dns = id == "rogue" ? m.Ip(m.Rogue) : m.Ip(m.Gw);
            Func<double, double, string> pings = (ms, loss) =>
            {
                var t = new StringBuilder(); int ok = 0;
                for (int i = 0; i < 4; i++) { if (r.NextDouble() < loss) t.Append("Request timed out.\n"); else { ok++; t.Append("Reply: bytes=32 time=" + Math.Max(1, (int)(ms + r.Next(-2, (int)(ms / 4) + 3))) + "ms TTL=" + (ms < 5 ? 64 : 57) + "\n"); } }
                t.Append("Packets: Sent = 4, Received = " + ok + ", Lost = " + (4 - ok) + " (" + (4 - ok) * 25 + "% loss)"); return t.ToString();
            };
            Func<string, string> pingTo = target =>
            {
                bool lan = target.StartsWith("192.168.", StringComparison.Ordinal);
                if (id == "apipa") return "PING: transmit failed. General failure.";
                if (id == "gateway" && !lan) return "Request timed out.\nRequest timed out.\nRequest timed out.\nRequest timed out.\nPackets: Sent = 4, Received = 0, Lost = 4 (100% loss)";
                if (id == "wifi") return pings(lan ? 40 + r.Next(60) : m.Base + 70 + r.Next(80), 0.3);
                if (id == "dupip") return pings(lan ? 2 : m.Base, 0.45);
                return pings(lan ? 1 + r.Next(3) : m.Base, 0);
            };
            switch (cmd)
            {
                case "ipconfig":
                    sb.Append("Wireless LAN adapter Wi-Fi:\n   Physical Address . . . : " + m.PcMac.Replace(':', '-').ToUpperInvariant() + "\n   DHCP Enabled . . . . . : " + (id == "gateway" ? "No" : "Yes") + "\n");
                    sb.Append("   IPv4 Address . . . . . : " + pc + (id == "apipa" ? "(Preferred)\n   Autoconfiguration . . . : Yes" : "(Preferred)") + "\n   Subnet Mask . . . . . . : 255." + (id == "apipa" ? "255.0.0" : "255.255.0") + "\n");
                    sb.Append("   Default Gateway . . . . : " + (id == "apipa" ? "" : gw) + "\n");
                    if (id != "apipa" && id != "gateway") sb.Append("   DHCP Server . . . . . . : " + (id == "rogue" ? m.Ip(m.Rogue) : m.Ip(m.Gw)) + "\n");
                    sb.Append("   DNS Servers . . . . . . : " + (id == "apipa" ? "fec0:0:0:ffff::1%1" : id == "dns" ? m.Ip(m.Gw) : dns));
                    return sb.ToString();
                case "pinggw": return "Pinging " + (id == "apipa" ? "(no gateway)" : gw) + ":\n" + (id == "apipa" ? "PING: transmit failed. General failure." : id == "gateway" ? "Request timed out.\nRequest timed out.\nRequest timed out.\nRequest timed out.\nPackets: Sent = 4, Received = 0, Lost = 4 (100% loss)" : pingTo(gw));
                case "ping1": return "Pinging 1.1.1.1:\n" + pingTo("1.1.1.1");
                case "pingname":
                    if (id == "dns" || id == "apipa") return "Ping request could not find host example.com. Please check the name and try again.";
                    if (id == "hosts") return "Pinging example.com [10.13.37.2]:\nRequest timed out.\nRequest timed out.\nRequest timed out.\nRequest timed out.\nPackets: Sent = 4, Received = 0, Lost = 4 (100% loss)";
                    return "Pinging example.com [93.184.215.14]:\n" + pingTo("93.184.215.14");
                case "nslookup":
                    if (id == "dns") return "DNS request timed out.\n    timeout was 2 seconds.\nServer:  UnKnown\nAddress:  " + m.Ip(m.Gw) + "\n*** Request to UnKnown timed-out";
                    if (id == "apipa") return "*** Can't find server name for address fec0:0:0:ffff::1: No response from server";
                    return "Server:  " + (id == "rogue" ? "UnKnown" : "router.home") + "\nAddress:  " + dns + "\n\nNon-authoritative answer:\nName:    example.com\nAddress:  93.184.215.14";
                case "tracert":
                    if (id == "apipa") return "Unable to contact IP driver. General failure.";
                    if (id == "gateway") return "  1     *        *        *     Request timed out.\n  2     *        *        *     Request timed out.\n  3     *        *        *     Request timed out.";
                    sb.Append("  1    " + (id == "wifi" ? 60 + r.Next(40) : 1) + " ms  " + gw + "\n");
                    if (id == "rogue") sb.Append("  2     " + (2 + r.Next(3)) + " ms  " + m.Ip(m.Gw) + "\n");
                    sb.Append("  " + (id == "rogue" ? 3 : 2) + "    " + (m.Base - 3) + " ms  100.64." + r.Next(255) + ".1\n  " + (id == "rogue" ? 4 : 3) + "    " + (m.Base - 1) + " ms  198.51.100." + r.Next(255) + "\n  " + (id == "rogue" ? 5 : 4) + "    " + m.Base + " ms  one.one.one.one [1.1.1.1]");
                    return sb.ToString();
                case "pingdf":
                    if (id == "mtu") return "Pinging 1.1.1.1 with 1472 bytes of data:\nRequest timed out.\nRequest timed out.\nRequest timed out.\nRequest timed out.\nPackets: Sent = 4, Received = 0, Lost = 4 (100% loss)\n(32-byte pings to the same address work fine.)";
                    if (id == "apipa" || id == "gateway") return "Pinging 1.1.1.1 with 1472 bytes of data:\nRequest timed out.\nPackets: Sent = 4, Received = 0, Lost = 4 (100% loss)";
                    return "Pinging 1.1.1.1 with 1472 bytes of data:\n" + pingTo("1.1.1.1");
                case "arp":
                    sb.Append("Interface: " + pc + "\n  Internet Address      Physical Address      Type\n");
                    if (id != "apipa") sb.Append("  " + m.Ip(m.Gw).PadRight(22) + m.GwMac.Replace(':', '-').PadRight(22) + "dynamic\n");
                    if (id == "rogue") sb.Append("  " + m.Ip(m.Rogue).PadRight(22) + m.RogueMac.Replace(':', '-').PadRight(22) + "dynamic   (maker: Raspberry Pi)\n");
                    if (id == "dupip") sb.Append("  (Windows saw " + m.Ip(m.Pc) + " answered by another device: " + m.RogueMac.Replace(':', '-') + ")\n");
                    sb.Append("  " + m.Ip(255).PadRight(22) + "ff-ff-ff-ff-ff-ff     static");
                    return sb.ToString();
                case "wifi":
                    return "State: connected\nSSID: HomeNet\nRadio type: 802.11" + (id == "wifi" ? "n\nBand: 2.4 GHz\nChannel: 6\nSignal: " + (18 + r.Next(14)) + "%\nReceive rate: 6.5 Mbps\nNetworks around on channel 6: " + (9 + r.Next(8)) : "ax\nBand: 5 GHz\nChannel: 44\nSignal: " + (78 + r.Next(20)) + "%\nReceive rate: 866 Mbps\nNetworks around on channel 44: 1");
                case "proxy":
                    return id == "proxy" ? "Use a proxy server: On\nAddress: 127.0.0.1   Port: 8899\nSet by: (unknown program)" : "Use a proxy server: Off\nAutomatically detect settings: On";
                case "hosts":
                    return "# Copyright (c) 1993-2009 Microsoft Corp.\n# localhost name resolution is handled within DNS itself.\n#\t127.0.0.1       localhost" + (id == "hosts" ? "\n10.13.37.2      example.com" : "");
                case "events":
                    if (id == "dupip") return "Tcpip 4199  The system detected an address conflict for IP address " + m.Ip(m.Pc) + " with the system having network hardware address " + m.RogueMac.Replace(':', '-').ToUpperInvariant() + ".";
                    if (id == "apipa") return "Dhcp-Client 1001  Your computer was not assigned an address from the network (by the DHCP Server) for the Network Card.";
                    if (id == "wifi") return "WLAN-AutoConfig 8003  WLAN AutoConfig service has successfully disconnected from a wireless network.\nWLAN-AutoConfig 8001  ... has successfully connected (x" + (5 + r.Next(6)) + " today)";
                    return "No network errors in the last 24 hours.";
                case "web":
                    if (id == "dns" || id == "apipa") return "This site can't be reached. DNS_PROBE_FINISHED_NO_INTERNET";
                    if (id == "gateway") return "This site can't be reached. ERR_NETWORK_CHANGED / ERR_CONNECTION_TIMED_OUT";
                    if (id == "hosts") return "This site can't be reached. example.com took too long to respond. ERR_CONNECTION_TIMED_OUT";
                    if (id == "mtu") return "Connecting... (the page starts, then hangs on 'Waiting for example.com'. Small sites sometimes load.)";
                    if (id == "portal") return "Your connection is not private. NET::ERR_CERT_COMMON_NAME_INVALID\nThe certificate is for: login.hotspot-portal.example";
                    if (id == "proxy") return "No internet. There is something wrong with the proxy server. ERR_PROXY_CONNECTION_FAILED";
                    if (id == "wifi") return "The page loads, slowly - images trickle in. Video calls freeze.";
                    if (id == "dupip") return "Sometimes it loads. Sometimes ERR_CONNECTION_RESET.";
                    return "Example Domain - the page loaded.";
            }
            return "";
        }

        // ================================================================== catch the thief
        public static readonly string[] ThiefCases = { "night", "spoof", "dhcp", "disguise", "share" };
        public sealed class Device { public string Name, Ip, Mac, Maker, First, Data; public bool Thief; }
        public sealed class Case { public string Id, Story; public List<Device> Devices = new List<Device>(); public List<string> Log = new List<string>(); public string[] Clues; public string Explain; }
        public static Case NewCase(int seed, string id)
        {
            var r = new Random(seed); id = ThiefCases.Contains(id) ? id : ThiefCases[r.Next(ThiefCases.Length)];
            int net = new[] { 0, 1, 10, 68 }[r.Next(4)]; Func<int, string> ip = h => "192.168." + net + "." + h;
            var c = new Case { Id = id };
            Func<string, string, string, string, string, string, Device> D = (n, h, mac, mk, first, data) => new Device { Name = n, Ip = ip(int.Parse(h, CultureInfo.InvariantCulture)), Mac = mac, Maker = mk, First = first, Data = data };
            var fam = new List<Device> {
                D("Router", "1", "f4:f2:6d:4a:10:01", "TP-Link", "always", "-"),
                D("Mum's laptop", (20 + r.Next(10)).ToString(CultureInfo.InvariantCulture), "3c:52:82:" + H(r) + ":" + H(r) + ":" + H(r), "HP", "8 months ago", N(r, 1, 3) + " GB"),
                D("Anna's iPhone", (30 + r.Next(10)).ToString(CultureInfo.InvariantCulture), "a6:" + H(r) + ":" + H(r) + ":" + H(r) + ":" + H(r) + ":" + H(r), "(private address)", "every day", N(r, 1, 4) + " GB"),
                D("Living room TV", (40 + r.Next(10)).ToString(CultureInfo.InvariantCulture), "8c:79:f5:" + H(r) + ":" + H(r) + ":" + H(r), "Samsung", "1 year ago", N(r, 6, 14) + " GB"),
                D("HP-Printer", (50 + r.Next(10)).ToString(CultureInfo.InvariantCulture), "9c:8e:99:" + H(r) + ":" + H(r) + ":" + H(r), "HP", "1 year ago", "0.01 GB"),
                D("Dad's PC", (60 + r.Next(10)).ToString(CultureInfo.InvariantCulture), "00:d8:61:" + H(r) + ":" + H(r) + ":" + H(r), "Micro-Star (MSI)", "2 years ago", N(r, 2, 8) + " GB") };
            switch (id)
            {
                case "night":
                    fam.Add(new Device { Name = "android-" + H(r) + H(r), Ip = ip(100 + r.Next(50)), Mac = "de:" + H(r) + ":" + H(r) + ":" + H(r) + ":" + H(r) + ":" + H(r), Maker = "(private address)", First = "6 days ago", Data = N(r, 18, 30) + " GB", Thief = true });
                    c.Story = "The internet has been slow every night this week, and the data cap is nearly gone. Nobody at home stays up past midnight.";
                    c.Log.Add("02:13  DHCP lease  android-xxxx  (new device)"); c.Log.Add("02:14  Wi-Fi  joined with the home password"); c.Log.Add("04:50  Wi-Fi  left");
                    c.Clues = new[] { "Two devices have private (random) hardware addresses. That alone is normal - phones do it.", "One of them has been here every day for months. The other appeared 6 days ago.", "The new one joins only between 1 and 5 in the morning - and moves the most data." };
                    c.Explain = "A neighbour has the Wi-Fi password. Random MACs are normal, so the tell was the timing and the data, not the address. Change the Wi-Fi password and turn on WPA3 if the router has it.";
                    break;
                case "spoof":
                    { var t = fam[5]; t.Thief = true; c.Log.Add("19:02  ARP  " + ip(1) + " is at " + t.Mac + "   (was f4:f2:6d:4a:10:01)"); c.Log.Add("19:02  ARP  " + ip(1) + " is at " + t.Mac); }
                    c.Story = "Since this evening, sites are slow and some show certificate warnings. Every device is affected.";
                    c.Clues = new[] { "Look at which hardware address claims to be the router.", "The router's real address starts f4:f2:6d (TP-Link).", "One PC on the network now says 'I am the router' - every packet goes through it first." };
                    c.Explain = "ARP spoofing: a PC answered for the router's address so traffic flows through it (a man in the middle). Usually malware or a 'network tool' left running. Unplug it and scan it.";
                    break;
                case "dhcp":
                    fam.Add(new Device { Name = "raspberrypi", Ip = ip(77), Mac = "b8:27:eb:" + H(r) + ":" + H(r) + ":" + H(r), Maker = "Raspberry Pi", First = "yesterday", Data = "0.4 GB", Thief = true });
                    c.Story = "Some devices lost the internet after a new gadget arrived. Others are fine. Restarting fixes it - sometimes.";
                    c.Log.Add("08:30  DHCP OFFER  from " + ip(1) + "  (router)"); c.Log.Add("08:30  DHCP OFFER  from " + ip(77) + "  gateway " + ip(77) + ", DNS " + ip(77)); c.Log.Add("08:31  Dad's PC took the offer from " + ip(77));
                    c.Clues = new[] { "Two DHCP servers answer. Only the router should.", "Devices that take the second offer get the wrong gateway.", "The second offer comes from a device that arrived yesterday." };
                    c.Explain = "A rogue DHCP server: a Raspberry Pi set up as a router answers before the real one. Turn off its DHCP server. Switches with DHCP snooping stop this at work.";
                    break;
                case "disguise":
                    { var d = fam[4]; d.Maker = "Xiaomi"; d.Mac = "64:09:80:" + H(r) + ":" + H(r) + ":" + H(r); d.First = "3 days ago"; d.Data = N(r, 9, 15) + " GB"; d.Thief = true; }
                    fam.Add(D("HP-Printer", "88", "9c:8e:99:" + H(r) + ":" + H(r) + ":" + H(r), "HP", "1 year ago", "0.01 GB"));
                    c.Story = "Someone set up a device to look like part of the furniture. It's been busy.";
                    c.Log.Add("21:40  DHCP lease  HP-Printer  (new hardware address)");
                    c.Clues = new[] { "There are two printers. You own one.", "A printer moving 10 GB? Printers barely use the network.", "Its maker (from the hardware address) isn't HP at all." };
                    c.Explain = "A name is just a label anyone can type. The maker comes from the hardware address, and the data use gave it away. Block it in the router and change the Wi-Fi password.";
                    break;
                default:
                    fam.Add(new Device { Name = "Galaxy-S23", Ip = ip(120 + r.Next(30)), Mac = "4c:66:41:" + H(r) + ":" + H(r) + ":" + H(r), Maker = "Samsung", First = "2 weeks ago", Data = N(r, 25, 40) + " GB", Thief = true });
                    c.Story = "Gaming has lagged in the evenings for two weeks. The Wi-Fi password was given to a 'friend of a friend' once.";
                    c.Log.Add("18:05  Wi-Fi  Galaxy-S23 joined"); c.Log.Add("18:06  Upload  4 Mbps for 3 hours (video upload?)"); c.Log.Add("23:30  Wi-Fi  Galaxy-S23 left");
                    c.Clues = new[] { "The family has no Samsung phone.", "It joins at 18:00 every day and leaves before midnight.", "It uploads for hours - uploads hurt game ping most." };
                    c.Explain = "A guest who kept the password. Change it, and give visitors the router's guest network next time.";
                    break;
            }
            c.Devices = fam.OrderBy(d => r.Next()).ToList();
            return c;
        }
        static string H(Random r) { return r.Next(256).ToString("x2", CultureInfo.InvariantCulture); }
        static string N(Random r, int a, int b) { return (a + r.NextDouble() * (b - a)).ToString("0.#", CultureInfo.CurrentCulture); }

        // ================================================================== packet detective: one packet is wrong
        // each: title, the packets (time | from > to | what), the wrong one (0-based), why
        public static readonly string[][] Detective = {
            new[] { "Opening a web connection", "0.000|PC > Server|SYN - let's talk, my sequence starts at 1000~0.021|Server > PC|ACK 1001 - got it~0.021|Server > PC|SYN-ACK - fine, mine starts at 5000, ack 1001~0.022|PC > Server|ACK 5001", "1", "The server can't acknowledge alone before it says SYN: the second step is one packet, SYN-ACK. Here a lone ACK came first." },
            new[] { "Getting an address (DHCP)", "0.000|PC > Everyone|DISCOVER - any DHCP server?~0.004|PC > Router|REQUEST - I'll take 192.168.1.37~0.010|Router > PC|OFFER - you can have 192.168.1.37~0.012|Router > PC|ACK - it's yours for 24 h", "1", "The order is Discover, Offer, Request, Ack. The PC asked for an address before anyone offered one." },
            new[] { "Finding a name (DNS)", "0.000|PC > 192.168.1.1|Query: where is example.com? (id 0x3a1f)~0.012|8.8.8.8 > PC|Answer: 93.184.215.14 (id 0x3a1f)~0.013|PC > 93.184.215.14|SYN to port 443", "1", "The PC asked the router, but the answer came from 8.8.8.8. A real answer comes from the server you asked - this is how DNS spoofing looks." },
            new[] { "A secure page (TLS)", "0.000|PC > Server|TCP handshake done~0.001|Server > PC|ServerHello - let's use TLS 1.3, here's my key~0.002|PC > Server|ClientHello - I speak TLS 1.3 and 1.2~0.040|Server > PC|Certificate, Finished", "1", "The client always speaks first in TLS: ClientHello, then ServerHello. A server can't answer a question nobody asked." },
            new[] { "A traceroute", "0.000|PC > 1.1.1.1|ping, TTL 1~0.002|192.168.1.1 > PC|Time exceeded (TTL ran out here)~0.010|PC > 1.1.1.1|ping, TTL 2~0.011|PC > 1.1.1.1|ping, TTL 2 - reply from 1.1.1.1 directly, TTL 64~0.020|100.64.0.1 > PC|Time exceeded", "3", "With TTL 2 the packet dies at the second router, which says 'time exceeded'. 1.1.1.1 can't answer a packet that never reached it." },
            new[] { "A big packet, don't fragment", "0.000|PC > Server|1500-byte packet, don't fragment~0.010|Router > PC|ICMP: fragmentation needed, next-hop MTU 1600~0.011|PC > Server|1400-byte packet, don't fragment~0.030|Server > PC|ACK", "1", "A router only sends 'fragmentation needed' when its next link is smaller than the packet. An MTU of 1600 would have carried 1500 bytes." },
            new[] { "Who has this address? (ARP)", "0.000|PC > Everyone|ARP: who has 192.168.1.1? Tell 192.168.1.37~0.001|Router > PC|ARP: 192.168.1.1 is at f4:f2:6d:4a:10:01~0.002|Laptop > Everyone|ARP: 192.168.1.1 is at 00:d8:61:aa:bb:cc~0.003|PC > Router|data to f4:f2:6d:4a:10:01", "2", "A second device claims the router's address with its own hardware address, unasked - a gratuitous ARP spoof. The real router already answered." },
            new[] { "Closing a connection", "0.000|PC > Server|FIN - I'm done sending~0.020|Server > PC|ACK~0.021|Server > PC|data: 4 KB~0.022|PC > Server|SYN - start a new connection on the same ports", "3", "After a FIN the PC waits for the server's own FIN (and then TIME_WAIT) before reusing the same ports. A SYN on the same pair mid-close is wrong." },
            new[] { "A ping", "0.000|PC > 1.1.1.1|Echo request, id 7, seq 1~0.012|1.1.1.1 > PC|Echo reply, id 7, seq 1~1.000|PC > 1.1.1.1|Echo request, id 7, seq 2~1.013|1.1.1.1 > PC|Echo reply, id 9, seq 2", "3", "A reply carries the same id as its request. id 9 belongs to someone else's ping - Windows would ignore it." },
            new[] { "A UDP game packet", "0.000|PC:52001 > Server:7777|UDP game input (40 bytes)~0.030|Server:7777 > PC:52001|UDP world update (900 bytes)~0.031|PC:52001 > Server:7777|TCP ACK for the update~0.046|PC:52001 > Server:7777|UDP game input", "2", "UDP has no ACKs - nothing is acknowledged at the transport layer. A game that needs one builds it into its own messages." } };

        // ================================================================== quick-fire rounds
        static readonly string[][] PortPool = {
            new[] { "a web page over HTTPS", "443" }, new[] { "plain HTTP", "80" }, new[] { "DNS", "53" }, new[] { "SSH", "22" }, new[] { "Remote Desktop", "3389" }, new[] { "mail from a mail app (submission)", "587" },
            new[] { "IMAP over TLS", "993" }, new[] { "NTP (the clock)", "123" }, new[] { "a DHCP server", "67" }, new[] { "SNMP queries", "161" }, new[] { "SNMP traps", "162" }, new[] { "LDAP", "389" },
            new[] { "Windows file sharing (SMB)", "445" }, new[] { "RADIUS sign-in", "1812" }, new[] { "syslog", "514" }, new[] { "TACACS+", "49" }, new[] { "BGP", "179" }, new[] { "Telnet", "23" },
            new[] { "an iperf3 server", "5201" }, new[] { "STUN (calls finding their public address)", "3478" }, new[] { "FTP (control)", "21" }, new[] { "mail between servers (SMTP)", "25" }, new[] { "Kerberos", "88" }, new[] { "MySQL", "3306" } };
        public sealed class Quick { public string Kind; public List<string[]> Q = new List<string[]>(); public int At, Right; public DateTime Start; public string Last = ""; }   // Q: question, right, a, b, c, d
        public static Quick NewQuick(string kind, int seed)
        {
            var r = new Random(seed); var q = new Quick { Kind = kind, Start = DateTime.UtcNow };
            if (kind == "ports")
                foreach (var p in PortPool.OrderBy(x => r.Next()).Take(10))
                {
                    var opts = PortPool.Where(x => x[1] != p[1]).Select(x => x[1]).Distinct().OrderBy(x => r.Next()).Take(3).Concat(new[] { p[1] }).OrderBy(x => r.Next()).ToArray();
                    q.Q.Add(new[] { "Which port: " + p[0] + "?", p[1] }.Concat(opts).ToArray());
                }
            else
                for (int i = 0; i < 10; i++)
                {
                    string a, cls; Addr(r, out a, out cls);
                    var opts = AddrKinds.Where(k => k != cls).OrderBy(x => r.Next()).Take(3).Concat(new[] { cls }).OrderBy(x => r.Next()).ToArray();
                    q.Q.Add(new[] { "What kind of address is " + a + "?", cls }.Concat(opts).ToArray());
                }
            return q;
        }
        public static readonly string[] AddrKinds = { "private (home or office)", "public (the internet)", "loopback (this PC)", "link-local (no DHCP answer)", "shared by your provider (CGNAT)", "multicast (a group)", "documentation (examples only)" };
        public static void Addr(Random r, out string a, out string cls)
        {
            int k = r.Next(AddrKinds.Length); cls = AddrKinds[k];
            Func<int> b = () => r.Next(1, 255);
            switch (k)
            {
                case 0: a = r.Next(3) == 0 ? "10." + b() + "." + b() + "." + b() : r.Next(2) == 0 ? "192.168." + r.Next(0, 255) + "." + b() : "172." + r.Next(16, 32) + "." + b() + "." + b(); break;
                case 1: a = new[] { 8, 23, 52, 81, 104, 142, 151, 185, 213 }[r.Next(9)] + "." + b() + "." + b() + "." + b(); break;
                case 2: a = "127." + r.Next(0, 3) + "." + r.Next(0, 3) + "." + r.Next(1, 9); break;
                case 3: a = "169.254." + b() + "." + b(); break;
                case 4: a = "100." + r.Next(64, 128) + "." + b() + "." + b(); break;
                case 5: a = new[] { "224.0.0.251", "239.255.255.250", "224.0.0.1", "224.0.0.22", "239.1.2.3" }[r.Next(5)]; break;
                default: a = new[] { "192.0.2.", "198.51.100.", "203.0.113." }[r.Next(3)] + b(); break;
            }
        }
        // the quick-fire rule: 8 of 10 within 60 s
        public static bool Won(Quick q) { return q.Right >= 8; }   // eight right within the minute wins - all ten don't have to be answered
        public static int SecondsLeft(Quick q) { return Math.Max(0, 60 - (int)(DateTime.UtcNow - q.Start).TotalSeconds); }
    }
}
