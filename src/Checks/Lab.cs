// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // Traffic Lab: test packets you build yourself, to one target, small and slow - never a flood, never a scan.
    // Caps for every sender: one target, at most 10 packets a second and 1,000 in all; each send is logged; Stop works.
    // Nothing here needs admin: ordinary sockets, Windows' own ping, ARP and DHCP calls. Nothing listens on a port.
    static class Lab
    {
        public const int MaxPackets = 1000, MinGapMs = 100;
        public const string Caps = "Capped at 10 packets a second and 1,000 in all, to this one target.";
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string I(long n) { return n.ToString(CultureInfo.InvariantCulture); }

        // on a company domain or VPN a deliberate tick is needed first, as for the ping sweep
        public static bool Company(Monitor m)
        {
            bool vpn; lock (m.L) vpn = m.VpnUp; string machineDomain = "";
            try { machineDomain = IPGlobalProperties.GetIPGlobalProperties().DomainName ?? ""; } catch (NetworkInformationException) { }
            return vpn || machineDomain != "" || !string.Equals(Environment.UserDomainName, Environment.MachineName, StringComparison.OrdinalIgnoreCase);   /* the PC's domain too - a local account on a company PC counts */
        }
        public static bool Allowed(Monitor m, string ok, List<string[]> rows)
        {
            if (Policy.NoLab) { rows.Add(R(2, "Traffic Lab", "turned off by your IT team (AdeeboMeter.policy)", "", "u")); return false; }
            if (string.Equals((ok ?? "").Trim(), "yes", StringComparison.OrdinalIgnoreCase) || !Company(m)) return true;
            rows.Add(R(1, "Company network?", "this PC is on a company domain or VPN", "Test traffic on a company network can trip the security team's alarms. Tick \"This is my own network, or I'm allowed to send test traffic to it\" and run again.", "u"));
            return false;
        }
        static void Log(string what) { Program.Log("traffic lab: " + what); }
        public static bool Split(string s, int defPort, out string host, out int port)
        {
            host = (s ?? "").Trim(); port = defPort;
            int rb = host.IndexOf("]:", StringComparison.Ordinal), c = host.LastIndexOf(':');
            if (host.StartsWith("[", StringComparison.Ordinal) && rb > 0) { if (!int.TryParse(host.Substring(rb + 2), out port)) return false; host = host.Substring(1, rb - 1); }
            else if (c > 0 && host.IndexOf(':') == c) { if (!int.TryParse(host.Substring(c + 1), out port)) return false; host = host.Substring(0, c); }
            return host != "" && host.Length <= 253 && port >= 1 && port <= 65535 && Regex.IsMatch(host, @"^[\w.:%-]+$");
        }
        public static IPAddress Addr(string h)
        {
            IPAddress a; if (IPAddress.TryParse(h, out a)) return a;
            try { return Dns.GetHostAddresses(h).OrderBy(x => x.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).FirstOrDefault(); } catch (SocketException) { return null; } catch (ArgumentException) { return null; }
        }
        static int Count(string s, int def) { int n; if (!int.TryParse((s ?? "").Trim(), out n)) n = def; return Math.Max(1, Math.Min(MaxPackets, n)); }
        // "hex:0a0b0c" or "hex 0a 0b", else text with \n \r \t
        public static byte[] Payload(string s)
        {
            s = s ?? "";
            if (s.StartsWith("hex:", StringComparison.OrdinalIgnoreCase) || s.StartsWith("hex ", StringComparison.OrdinalIgnoreCase))
            {
                var h = new string(s.Substring(4).Where(ch => !char.IsWhiteSpace(ch)).ToArray());
                if (h.Length % 2 != 0 || h.Any(ch => !Uri.IsHexDigit(ch))) return null;
                return Enumerable.Range(0, Math.Min(h.Length / 2, 1400)).Select(i => Convert.ToByte(h.Substring(i * 2, 2), 16)).ToArray();
            }
            var t = Encoding.UTF8.GetBytes(s.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\t", "\t"));
            return t.Length > 1400 ? t.Take(1400).ToArray() : t;
        }
        static string Show(byte[] b, int n)
        {
            n = Math.Min(n, b.Length); if (n == 0) return "(empty)";
            bool text = b.Take(n).All(x => x == 9 || x == 10 || x == 13 || (x >= 32 && x < 127));
            if (text) return Regex.Replace(Encoding.ASCII.GetString(b, 0, n), @"\s+", " ").Trim();
            return string.Join(" ", b.Take(Math.Min(n, 64)).Select(x => x.ToString("x2", CultureInfo.InvariantCulture))) + (n > 64 ? " …" : "");
        }

        // ---------- ICMP builder: size, don't-fragment, TTL, count and gap - IPv4 or IPv6, through Windows' own ping (no admin)
        public static List<string[]> Icmp(Monitor m, string target, string count, string opts, string ok, Toolkit.Run run)
        {
            var rows = new List<string[]>(); if (!Allowed(m, ok, rows)) return rows;
            string h = (target ?? "").Trim(); if (h == "" || !Regex.IsMatch(h, @"^[\w.:%-]+$") || h.Length > 253) { rows.Add(R(2, "Target", "type an address or name", "", "u")); return rows; }
            var a = Addr(h); if (a == null) { rows.Add(R(2, "Target", "couldn't find " + h, "", "m")); return rows; }
            int n = Count(count, 10), size = 32, ttl = 128, gap = 1000, dscp = -1; bool df = false, cmp = false; string o = (opts ?? "").ToLowerInvariant(), bad = "";
            foreach (Match mm in Regex.Matches(o, @"(size|ttl|every|gap)\s*=?\s*(\d+)|\b(df)\b|\bdscp\s*=?\s*([a-z]{2}\d{0,2}|\d{1,2})\b|\b(compare)\b"))
            {
                if (mm.Groups[3].Success) { df = true; continue; }
                if (mm.Groups[5].Success) { cmp = true; continue; }
                if (mm.Groups[4].Success) { int d; string dn = mm.Groups[4].Value.ToUpperInvariant(); if (Toolkit.Dscp.TryGetValue(dn, out d) || (int.TryParse(dn, out d) && d >= 0 && d <= 63)) dscp = d; else bad = "DSCP is a name (EF, AF41, CS6...) or 0-63"; continue; }
                int v = int.Parse(mm.Groups[2].Value.Length > 6 ? "999999" : mm.Groups[2].Value, CultureInfo.InvariantCulture);
                switch (mm.Groups[1].Value) { case "size": size = v; break; case "ttl": ttl = v; break; default: gap = v; break; }
            }
            if (size > 65500) bad = "size is at most 65,500 bytes"; if (ttl < 1 || ttl > 255) bad = "TTL is 1-255";
            if (cmp && dscp < 0) bad = "compare needs a marking - e.g. dscp ef compare";
            if (dscp >= 0 && a.AddressFamily != AddressFamily.InterNetwork) bad = "DSCP marking works with IPv4 targets here";
            if (bad != "") { rows.Add(R(2, "Options", bad, "e.g. size 1472 df ttl 5 every 200 dscp ef compare", "u")); return rows; }
            gap = Math.Max(MinGapMs, Math.Min(10000, gap));
            Log("icmp " + n + " x " + size + " B to " + a + (df ? " df" : "") + " ttl " + ttl + (dscp >= 0 ? " dscp " + dscp + (cmp ? " compare" : "") : ""));
            var buf = new byte[size]; for (int i = 0; i < size; i++) buf[i] = (byte)('a' + i % 23);
            // two buckets: [0] as asked (marked when dscp is set), [1] unmarked - only used with compare, every other packet
            var times = new[] { new List<long>(), new List<long>() }; var sentB = new int[2]; var lostB = new int[2];
            var froms = new Dictionary<string, int>(); int sent = 0, frag = 0;
            using (var p = new Ping())
                for (int i = 0; i < n && !run.Stop; i++)
                {
                    var sw = Stopwatch.StartNew(); sent++; int b = cmp && i % 2 == 1 ? 1 : 0; sentB[b]++;
                    try
                    {
                        IPStatus st; long rtt; string from;
                        if (dscp >= 0)
                        {
                            var r = AdeeboMeter.Icmp.Echo(a, size, ttl, df, 2000, false, b == 0 ? (byte)(dscp << 2) : (byte)0);
                            st = r.Status == AdeeboMeter.Icmp.Ok ? IPStatus.Success : r.Status == AdeeboMeter.Icmp.TooBig ? IPStatus.PacketTooBig : r.Status == AdeeboMeter.Icmp.TtlExpired ? IPStatus.TtlExpired : IPStatus.TimedOut; rtt = Math.Max(0, r.Rtt); from = r.From;
                        }
                        else { var r = p.Send(a, 2000, buf, new PingOptions(ttl, df)); st = r.Status; rtt = r.RoundtripTime; from = r.Address != null ? r.Address.ToString() : ""; }
                        if (st == IPStatus.Success) times[b].Add(rtt);
                        else if (st == IPStatus.PacketTooBig) frag++;
                        else if (st == IPStatus.TtlExpired || st == IPStatus.TimeExceeded) { string k = from + " (TTL ran out there)"; int c; froms.TryGetValue(k, out c); froms[k] = c + 1; }
                        else lostB[b]++;
                    }
                    catch (PingException) { lostB[b]++; }
                    catch (InvalidOperationException) { lostB[b]++; }
                    run.Status = "Sent " + sent + " of " + n + (cmp ? (b == 0 ? " (marked)" : " (unmarked)") : "") + "..."; run.Add(times[b].Count > 0 ? times[b].Last() : 0);
                    int wait = gap - (int)sw.ElapsedMilliseconds; while (wait > 0 && !run.Stop && i < n - 1) { Thread.Sleep(Math.Min(wait, 100)); wait -= 100; }
                }
            Func<int, string> line = k => I(sentB[k]) + " sent · " + I(times[k].Count) + " answered" + (times[k].Count > 0 ? " · " + times[k].Min() + " / " + (long)times[k].Average() + " / " + times[k].Max() + " ms (min / avg / max)" : "");
            int got = times[0].Count + times[1].Count, lost = lostB[0] + lostB[1];
            string mark = dscp >= 0 ? "DSCP " + dscp + (Toolkit.Dscp.Any(x => x.Value == dscp) ? " (" + Toolkit.Dscp.First(x => x.Value == dscp).Key + ")" : "") : "";
            rows.Add(R(got == 0 && froms.Count == 0 ? 2 : lostB[0] > 0 ? 1 : 0, a + (a.ToString() != h ? " (" + h + ")" : "") + (cmp ? " · " + mark : ""), line(0),
                I(size) + "-byte payload" + (df ? ", don't fragment" : "") + ", TTL " + ttl + (mark != "" ? ", " + mark : "") + ", every " + gap + " ms. " + Caps, "m"));
            if (cmp)
            {
                rows.Add(R(lostB[1] > 0 ? 1 : 0, "Same, unmarked", line(1), "Every other packet left without a marking - the same size, TTL and timing.", "m"));
                double a0 = times[0].Count > 0 ? times[0].Average() : double.NaN, a1 = times[1].Count > 0 ? times[1].Average() : double.NaN;
                double l0 = sentB[0] > 0 ? 100.0 * lostB[0] / sentB[0] : 0, l1 = sentB[1] > 0 ? 100.0 * lostB[1] / sentB[1] : 0;
                string verdict = l0 > l1 + 10 ? "Marked packets are dropped more - something on the way polices or drops that class." :
                                 l1 > l0 + 10 ? "Unmarked packets are dropped more - the marked class is protected (it gets priority)." :
                                 !double.IsNaN(a0) && !double.IsNaN(a1) && a0 + 5 < a1 ? "Marked packets come back faster - the marking is honoured somewhere on the way." :
                                 "No difference - the marking is ignored, cleared on the way, or the path isn't busy enough to show it.";
                rows.Add(R(3, "Compared", verdict, "Windows clears the marking a program asks for unless a QoS policy allows it - Live capture with filter icmp shows whether it left marked.", "i"));
            }
            if (frag > 0) rows.Add(R(1, "Too big", I(frag) + " replies said the packet needs fragmenting", "With don't-fragment set, a link on the way has a smaller MTU than " + I(size + (a.AddressFamily == AddressFamily.InterNetwork ? 28 : 48)) + " bytes.", "m"));
            foreach (var kv in froms) rows.Add(R(3, "Answered by", kv.Key + " × " + kv.Value, "With a small TTL this shows the router at that hop - a manual traceroute.", "m"));
            if (lost > 0) rows.Add(R(1, "No answer", I(lost) + " of " + I(sent), "Loss, or the target or a firewall on the way drops ping.", "m"));
            if (run.Stop) rows.Insert(0, R(1, "Stopped", "stopped by you after " + sent + " packets", "", "m"));
            return rows;
        }

        // ---------- UDP sender: a payload you write, N times, then any answer for a second
        public static List<string[]> Udp(Monitor m, string target, string payload, string count, string ok, Toolkit.Run run)
        {
            var rows = new List<string[]>(); if (!Allowed(m, ok, rows)) return rows;
            string h; int port; if (!Split(target, 0, out h, out port) || port == 0) { rows.Add(R(2, "Target", "type host:port - e.g. 10.0.0.5:5060", "", "u")); return rows; }
            var a = Addr(h); if (a == null) { rows.Add(R(2, "Target", "couldn't find " + h, "", "m")); return rows; }
            var data = Payload(payload); if (data == null) { rows.Add(R(2, "Payload", "hex: needs pairs of 0-9 a-f", "e.g. hex:0a0b0c or plain text", "u")); return rows; }
            int n = Count(count, 1); Log("udp " + n + " x " + data.Length + " B to " + a + ":" + port);
            var answers = new List<string>(); int sent = 0, refused = 0;
            using (var u = new UdpClient(a.AddressFamily))
            {
                u.Client.ReceiveTimeout = 200; u.Connect(a, port);
                for (int i = 0; i < n && !run.Stop; i++)
                {
                    try { u.Send(data, data.Length); sent++; } catch (SocketException) { refused++; }
                    run.Status = "Sent " + sent + " of " + n + "...";
                    var until = DateTime.UtcNow.AddMilliseconds(i == n - 1 ? 1000 : MinGapMs);
                    while (DateTime.UtcNow < until && !run.Stop)
                    {
                        try { var ep = new IPEndPoint(IPAddress.Any, 0); var b = u.Receive(ref ep); if (answers.Count < 20) answers.Add(I(b.Length) + " B: " + Show(b, 200)); }
                        catch (SocketException e) { if (e.SocketErrorCode == SocketError.ConnectionReset) { refused++; break; } }
                    }
                }
            }
            rows.Add(R(answers.Count > 0 ? 0 : refused > 0 ? 1 : 3, "Sent to " + a + ":" + port + " (UDP)", I(sent) + " × " + I(data.Length) + " bytes · " + (answers.Count > 0 ? I(answers.Count) + " answers" : refused > 0 ? "port closed (the host said so)" : "no answer"),
                (answers.Count == 0 && refused == 0 ? "UDP has no handshake: silence means delivered and ignored, or dropped on the way - the two look the same. " : "") + Caps, "m"));
            foreach (var s in answers) rows.Add(R(3, "Answer", s, "", "m"));
            return rows;
        }

        // ---------- TCP sender: connect, send what you wrote, show what comes back (up to 4 KB, 3 seconds)
        public static List<string[]> Tcp(Monitor m, string target, string payload, string ok, Toolkit.Run run)
        {
            var rows = new List<string[]>(); if (!Allowed(m, ok, rows)) return rows;
            string h; int port; if (!Split(target, 0, out h, out port) || port == 0) { rows.Add(R(2, "Target", "type host:port - e.g. 10.0.0.5:8080", "", "u")); return rows; }
            var a = Addr(h); if (a == null) { rows.Add(R(2, "Target", "couldn't find " + h, "", "m")); return rows; }
            var data = Payload(payload); if (data == null) { rows.Add(R(2, "Payload", "hex: needs pairs of 0-9 a-f", "", "u")); return rows; }
            Log("tcp " + data.Length + " B to " + a + ":" + port);
            using (var c = new TcpClient(a.AddressFamily))
            {
                var sw = Stopwatch.StartNew();
                try { if (!c.ConnectAsync(a, port).Wait(3000)) { rows.Add(R(2, a + ":" + port, "no answer to connect in 3 s", "Filtered by a firewall, or nothing there.", "m")); return rows; } }
                catch (AggregateException e) { rows.Add(R(2, a + ":" + port, e.InnerException is SocketException && ((SocketException)e.InnerException).SocketErrorCode == SocketError.ConnectionRefused ? "refused - the host is there, nothing listens on this port" : Draw.Trunc(e.InnerException != null ? e.InnerException.Message : e.Message, 90), "", "m")); return rows; }
                rows.Add(R(0, a + ":" + port, "connected in " + sw.ElapsedMilliseconds + " ms", Caps, "m"));
                var s = c.GetStream(); s.ReadTimeout = 3000; var got = new List<byte>();
                try
                {
                    if (data.Length > 0) s.Write(data, 0, data.Length);
                    var buf = new byte[4096]; var until = DateTime.UtcNow.AddSeconds(3);
                    while (got.Count < 4096 && DateTime.UtcNow < until && !run.Stop) { if (!s.DataAvailable) { if (c.Client.Poll(0, SelectMode.SelectRead) && c.Client.Available == 0) break; Thread.Sleep(50); continue; }   /* closed by the far side: done */ int k = s.Read(buf, 0, Math.Min(buf.Length, 4096 - got.Count)); if (k <= 0) break; got.AddRange(buf.Take(k)); }
                }
                catch (System.IO.IOException) { }
                rows.Add(R(3, data.Length > 0 ? "Sent " + data.Length + " bytes, got back" : "Got (without sending)", got.Count == 0 ? "nothing in 3 s" : I(got.Count) + " B: " + Show(got.ToArray(), 1500), got.Count == 0 && data.Length > 0 ? "Many protocols wait for a full request - end text lines with \\r\\n." : "", "m"));
            }
            return rows;
        }

        // ---------- ARP who-has: one ARP request from Windows itself (SendARP) - the MAC and how fast it answered
        public static List<string[]> Arp(Monitor m, string ipIn, string ok)
        {
            var rows = new List<string[]>(); if (!Allowed(m, ok, rows)) return rows;
            IPAddress ip; if (!IPAddress.TryParse((ipIn ?? "").Trim(), out ip) || ip.AddressFamily != AddressFamily.InterNetwork) { rows.Add(R(2, "IP", "type an IPv4 address on your own network", "ARP only works on your own subnet; IPv6 uses neighbour discovery instead.", "u")); return rows; }
            Log("arp who-has " + ip);
            var mac = new byte[6]; int len = 6; var sw = Stopwatch.StartNew();
            uint res; try { res = Native.SendARP(BitConverter.ToInt32(ip.GetAddressBytes(), 0), 0, mac, ref len); } catch (EntryPointNotFoundException) { res = 1; }
            if (res != 0 || len < 6) { rows.Add(R(2, ip.ToString(), "no ARP answer", res == 67 || res == 31 ? "Nothing answered at that address - it's off, or not on your subnet (then your router would answer for it only if it's the gateway)." : "Windows error " + res + ".", "m")); return rows; }
            string ms = string.Join(":", mac.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
            rows.Add(R(0, ip.ToString(), "is at " + ms + " (" + sw.ElapsedMilliseconds + " ms)", (mac[0] & 2) != 0 ? "A locally administered MAC - often a phone or PC with private (random) addresses, or a virtual machine." : "If this answer came back in 0 ms, Windows already had it cached.", "m"));
            return rows;
        }

        // ---------- DHCP options: ask the DHCP server (DHCPINFORM, through Windows) for the options it hands out
        static readonly int[] DhcpWant = { 1, 3, 6, 15, 42, 44, 51, 54, 66, 67, 119, 121, 150, 252, 43 };
        static readonly Dictionary<int, string> DhcpName = new Dictionary<int, string> { { 1, "Subnet mask" }, { 3, "Router" }, { 6, "DNS servers" }, { 15, "Domain name" }, { 42, "NTP servers" }, { 44, "WINS servers" }, { 51, "Lease time" }, { 54, "DHCP server" }, { 66, "TFTP server name" }, { 67, "Boot file" }, { 119, "DNS search list" }, { 121, "Classless routes" }, { 150, "TFTP servers (150)" }, { 252, "Proxy auto-config (WPAD)" }, { 43, "Vendor options" } };
        public static string DhcpText(int id, byte[] d)
        {
            Func<int, string> ip = o => o + 4 <= d.Length ? d[o] + "." + d[o + 1] + "." + d[o + 2] + "." + d[o + 3] : "?";
            switch (id)
            {
                case 1: case 3: case 6: case 42: case 44: case 54: case 150: return string.Join(", ", Enumerable.Range(0, d.Length / 4).Select(k => ip(k * 4)));
                case 51: { if (d.Length < 4) return "?"; long s = (long)d[0] << 24 | (long)d[1] << 16 | (long)d[2] << 8 | d[3]; return TimeSpan.FromSeconds(s).TotalHours >= 1 ? (s / 3600.0).ToString("0.#", CultureInfo.InvariantCulture) + " h" : I(s) + " s"; }
                case 15: case 66: case 67: case 252: return Encoding.ASCII.GetString(d).TrimEnd('\0');
                case 119: { var names = new List<string>(); int p = 0; while (p < d.Length) { var parts = new List<string>(); while (p < d.Length && d[p] != 0 && (d[p] & 0xC0) == 0) { int l = d[p]; if (p + 1 + l > d.Length) { p = d.Length; break; } parts.Add(Encoding.ASCII.GetString(d, p + 1, l)); p += l + 1; } if (p < d.Length && (d[p] & 0xC0) == 0xC0) p += 2; else p++; if (parts.Count > 0) names.Add(string.Join(".", parts)); } return string.Join(", ", names); }
                case 121:
                    {
                        var r = new List<string>(); int p = 0;
                        while (p < d.Length) { int w = d[p++]; int nb = (w + 7) / 8; if (w > 32 || p + nb + 4 > d.Length) break; var net = new byte[4]; Array.Copy(d, p, net, 0, nb); p += nb; r.Add(new IPAddress(net) + "/" + w + " via " + ip(p)); p += 4; }
                        return string.Join(", ", r);
                    }
                default: return string.Join(" ", d.Take(48).Select(x => x.ToString("x2", CultureInfo.InvariantCulture))) + (d.Length > 48 ? " …" : "");
            }
        }
        public static List<string[]> DhcpOptions(Monitor m, string ok)
        {
            var rows = new List<string[]>(); if (!Allowed(m, ok, rows)) return rows;
            NetworkInterface nic; lock (m.L) nic = m.Primary;
            if (nic == null) { rows.Add(R(2, "Adapter", "no active adapter", "", "u")); return rows; }
            var v4 = nic.GetIPProperties().GetIPv4Properties();
            if (v4 == null || !v4.IsDhcpEnabled) { rows.Add(R(3, nic.Name, "uses a fixed address - no DHCP server to ask", "", "m")); return rows; }
            Log("dhcp inform on " + nic.Name);
            const int sz = 32; IntPtr arr = Marshal.AllocHGlobal(sz * DhcpWant.Length), buf = IntPtr.Zero;
            try
            {
                for (int i = 0; i < DhcpWant.Length; i++) { var b = arr + i * sz; Marshal.WriteInt32(b, 0); Marshal.WriteInt32(b, 4, DhcpWant[i]); Marshal.WriteInt32(b, 8, 0); Marshal.WriteIntPtr(b, IntPtr.Size == 8 ? 16 : 12, IntPtr.Zero); Marshal.WriteInt32(b, IntPtr.Size == 8 ? 24 : 16, 0); }
                uint ver; uint res;
                try { if (Native.DhcpCApiInitialize(out ver) != 0) { rows.Add(R(2, "DHCP", "Windows' DHCP client interface didn't start", "", "m")); return rows; } } catch (EntryPointNotFoundException) { rows.Add(R(2, "DHCP", "Windows' DHCP client interface isn't available here", "", "m")); return rows; } catch (DllNotFoundException) { rows.Add(R(2, "DHCP", "Windows' DHCP client interface isn't available here", "", "m")); return rows; }
                int size = 4096; buf = Marshal.AllocHGlobal(size);
                var send = new Native.DhcpParamsArray { N = 0, P = IntPtr.Zero }; var recv = new Native.DhcpParamsArray { N = (uint)DhcpWant.Length, P = arr };
                var sw = Stopwatch.StartNew();
                res = Native.DhcpRequestParams(2, IntPtr.Zero, nic.Id, IntPtr.Zero, send, recv, buf, ref size, null);
                if (res == 234) { Marshal.FreeHGlobal(buf); buf = Marshal.AllocHGlobal(size); res = Native.DhcpRequestParams(2, IntPtr.Zero, nic.Id, IntPtr.Zero, send, recv, buf, ref size, null); }
                if (res != 0) { rows.Add(R(2, nic.Name, "the DHCP server didn't answer (Windows error " + res + ")", "Some networks only answer full DHCP requests, not an information request; your current lease is in DHCP lease and IPv6 lifetimes.", "m")); return rows; }
                rows.Add(R(0, nic.Name, "the DHCP server answered in " + sw.ElapsedMilliseconds + " ms", "Asked with a DHCP information request - your address and lease are not touched.", "m"));
                for (int i = 0; i < DhcpWant.Length; i++)
                {
                    var b = arr + i * sz; IntPtr data = Marshal.ReadIntPtr(b, IntPtr.Size == 8 ? 16 : 12); int n = Marshal.ReadInt32(b, IntPtr.Size == 8 ? 24 : 16);
                    if (data == IntPtr.Zero || n <= 0 || n > 1500) continue;
                    var d = new byte[n]; Marshal.Copy(data, d, 0, n);
                    rows.Add(R(3, DhcpName[DhcpWant[i]] + " (" + DhcpWant[i] + ")", Draw.Trunc(DhcpText(DhcpWant[i], d), 300), "", "m"));
                }
            }
            finally { Marshal.FreeHGlobal(arr); if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf); }
            return rows;
        }

        // ---------- SNMP test trap: one v2c notification to your trap receiver (send only - AdeeboMeter never receives traps)
        static byte[] Tlv(byte t, byte[] v) { return Snmp.Tlv(t, v); }
        static byte[] Cat(params byte[][] p) { return p.SelectMany(x => x).ToArray(); }
        static byte[] Int(long v) { var b = new List<byte>(); do { b.Insert(0, (byte)v); v >>= 8; } while (v != 0 && v != -1); if (v == 0 && (b[0] & 0x80) != 0) b.Insert(0, 0); return Tlv(2, b.ToArray()); }
        static byte[] Vb(string oid, byte[] val) { return Tlv(0x30, Cat(Tlv(6, Snmp.OidBytes(oid)), val)); }
        public static byte[] TrapPacket(string community, uint ticks, int reqId, string text)
        {
            var up = BitConverter.GetBytes(ticks).Reverse().ToArray();
            var vbs = Tlv(0x30, Cat(Vb("1.3.6.1.2.1.1.3.0", Tlv(0x43, up.SkipWhile((x, i) => x == 0 && i < 3).ToArray())), Vb("1.3.6.1.6.3.1.1.4.1.0", Tlv(6, Snmp.OidBytes("1.3.6.1.4.1.8072.2.3.0.1"))), Vb("1.3.6.1.2.1.1.1.0", Tlv(4, Encoding.UTF8.GetBytes(text)))));
            var pdu = Tlv(0xA7, Cat(Int(reqId), Int(0), Int(0), vbs));
            return Tlv(0x30, Cat(Int(1), Tlv(4, Encoding.UTF8.GetBytes(community)), pdu));
        }
        public static List<string[]> Trap(Monitor m, string target, string community, string ok)
        {
            var rows = new List<string[]>(); if (!Allowed(m, ok, rows)) return rows;
            string h; int port; if (!Split(target, 162, out h, out port)) { rows.Add(R(2, "Receiver", "type the trap receiver - host or host:port (UDP 162)", "", "u")); return rows; }
            var a = Addr(h); if (a == null) { rows.Add(R(2, "Receiver", "couldn't find " + h, "", "m")); return rows; }
            string cm = (community ?? "").Trim(); if (cm == "") cm = "public";
            string text = "AdeeboMeter test trap from " + Environment.MachineName + " at " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            var pkt = TrapPacket(cm, (uint)(Environment.TickCount & 0x7FFFFFFF) / 10, new Random().Next(1, int.MaxValue), text);
            using (var u = new UdpClient(a.AddressFamily)) u.Send(pkt, pkt.Length, new IPEndPoint(a, port));
            Log("snmp trap to " + a + ":" + port);
            rows.Add(R(0, "Sent to " + a + ":" + port + " (UDP)", "SNMPv2c trap · heartbeat test (1.3.6.1.4.1.8072.2.3.0.1) · \"" + text + "\"", "Traps are never confirmed - look for it on the receiver. Nothing on a device is changed, and AdeeboMeter never listens for traps.", "m"));
            return rows;
        }

        // ---------- NetFlow v5 test: one flow record, so you can see the collector receives and decodes this PC
        public static byte[] FlowPacket(IPAddress src, IPAddress dst, uint uptimeMs, uint seq)
        {
            var p = new byte[24 + 48]; long unix = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            Action<int, uint> u32 = (o, v) => { p[o] = (byte)(v >> 24); p[o + 1] = (byte)(v >> 16); p[o + 2] = (byte)(v >> 8); p[o + 3] = (byte)v; };
            Action<int, int> u16 = (o, v) => { p[o] = (byte)(v >> 8); p[o + 1] = (byte)v; };
            u16(0, 5); u16(2, 1); u32(4, uptimeMs); u32(8, (uint)unix); u32(12, 0); u32(16, seq);
            int r = 24; Array.Copy(src.GetAddressBytes(), 0, p, r, 4); Array.Copy(dst.GetAddressBytes(), 0, p, r + 4, 4);
            u32(r + 16, 1); u32(r + 20, 100); u32(r + 24, uptimeMs > 1000 ? uptimeMs - 1000 : 0); u32(r + 28, uptimeMs); u16(r + 32, 50000); u16(r + 34, 9); p[r + 38] = 17;   // 1 packet, 100 bytes, UDP to the discard port
            return p;
        }
        public static List<string[]> Flow(Monitor m, string target, string ok)
        {
            var rows = new List<string[]>(); if (!Allowed(m, ok, rows)) return rows;
            string h; int port; if (!Split(target, 2055, out h, out port)) { rows.Add(R(2, "Collector", "type the collector - host or host:port (UDP 2055)", "", "u")); return rows; }
            var a = Addr(h); if (a == null || a.AddressFamily != AddressFamily.InterNetwork) { rows.Add(R(2, "Collector", a == null ? "couldn't find " + h : "NetFlow v5 needs an IPv4 collector", "", "m")); return rows; }
            IPAddress me; using (var s = new UdpClient(AddressFamily.InterNetwork)) { s.Connect(a, port); me = ((IPEndPoint)s.Client.LocalEndPoint).Address; }
            var dst = IPAddress.Parse("192.0.2.10");
            var pkt = FlowPacket(me, dst, (uint)(Environment.TickCount & 0x7FFFFFFF), 1);
            using (var u = new UdpClient(AddressFamily.InterNetwork)) u.Send(pkt, pkt.Length, new IPEndPoint(a, port));
            Log("netflow v5 test to " + a + ":" + port);
            rows.Add(R(0, "Sent to " + a + ":" + port + " (UDP)", "NetFlow v5 · 1 flow: " + me + ":50000 -> " + dst + ":9 UDP, 1 packet, 100 bytes", "Look for that flow on the collector. 192.0.2.10 is a documentation address - nothing real is involved. Never confirmed by the collector.", "m"));
            return rows;
        }
    }
}
