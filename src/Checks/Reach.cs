// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // HTTP request builder, STUN, a device's name, active device discovery, and the remote-access commands page.
    static class Reach
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string I(long n) { return n.ToString(CultureInfo.InvariantCulture); }

        // ---------- HTTP request builder: method, headers and body you choose; redirects shown, not followed
        static readonly char[] BadHdr = { '\r', '\n', '\0' };
        static readonly string[] Methods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS" };
        public static List<string[]> Http(string url, string method, string headers, string body)
        {
            var rows = new List<string[]>(); string u = (url ?? "").Trim(); if (u != "" && !u.Contains("://")) u = "https://" + u;
            Uri uri; if (!Uri.TryCreate(u, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") || u.Length > 2000) { rows.Add(R(2, "URL", "type a web address - e.g. https://example.com/api/health", "", "u")); return rows; }
            string m = (method ?? "").Trim().ToUpperInvariant(); if (m == "") m = (body ?? "") != "" ? "POST" : "GET";
            if (Array.IndexOf(Methods, m) < 0) { rows.Add(R(2, "Method", "one of " + string.Join(", ", Methods), "", "u")); return rows; }
            HttpWebRequest q;
            try { q = (HttpWebRequest)WebRequest.Create(uri); } catch (NotSupportedException) { rows.Add(R(2, "URL", "not a web address", "", "u")); return rows; }
            q.Method = m; q.AllowAutoRedirect = false; q.Timeout = 15000; q.ReadWriteTimeout = 15000; q.UserAgent = "AdeeboMeter/" + Program.Version;
            var skipped = new List<string>();
            foreach (var part in (headers ?? "").Split(';'))
            {
                int c = part.IndexOf(':'); if (c <= 0) continue; string n = part.Substring(0, c).Trim(), v = part.Substring(c + 1).Trim();
                if (!Regex.IsMatch(n, @"^[!#$%&'*+.^_`|~0-9A-Za-z-]{1,64}$") || v.IndexOfAny(BadHdr) >= 0) { skipped.Add(n); continue; }
                try
                {
                    switch (n.ToLowerInvariant())
                    {
                        case "user-agent": q.UserAgent = v; break; case "accept": q.Accept = v; break; case "content-type": q.ContentType = v; break; case "referer": q.Referer = v; break;
                        case "host": case "content-length": case "connection": case "expect": case "date": case "transfer-encoding": skipped.Add(n); break;
                        default: q.Headers[n] = v; break;
                    }
                }
                catch (ArgumentException) { skipped.Add(n); }
            }
            if ((body ?? "").Length > 65536) { rows.Add(R(2, "Body", "64 KB at most", "", "u")); return rows; }
            var data = Encoding.UTF8.GetBytes(body ?? "");
            var sw = Stopwatch.StartNew(); HttpWebResponse r = null;
            try
            {
                if (data.Length > 0 && m != "GET" && m != "HEAD") { if (q.ContentType == null) q.ContentType = data.Length > 0 && (body.TrimStart().StartsWith("{", StringComparison.Ordinal) || body.TrimStart().StartsWith("[", StringComparison.Ordinal)) ? "application/json" : "text/plain; charset=utf-8"; q.ContentLength = data.Length; using (var s = q.GetRequestStream()) s.Write(data, 0, data.Length); }
                r = (HttpWebResponse)q.GetResponse();
            }
            catch (WebException e)
            {
                r = e.Response as HttpWebResponse;
                if (r == null) { rows.Add(R(2, uri.Host, e.Status == WebExceptionStatus.TrustFailure ? "the site's certificate isn't trusted" : e.Status == WebExceptionStatus.NameResolutionFailure ? "couldn't find " + uri.Host : e.Status == WebExceptionStatus.Timeout ? "no answer in 15 s" : Draw.Trunc(e.Message, 100), e.Status == WebExceptionStatus.TrustFailure ? "HTTP and TLS inspector shows the certificate and why." : "", "m")); return rows; }
            }
            catch (System.IO.IOException e) { rows.Add(R(2, uri.Host, Draw.Trunc(e.Message, 100), "", "m")); return rows; }
            using (r)
            {
                var buf = new MemoryStream(); try { using (var s = r.GetResponseStream()) { var b = new byte[8192]; int k; while (buf.Length < 65536 && (k = s.Read(b, 0, b.Length)) > 0) buf.Write(b, 0, k); } } catch (IOException) { } catch (WebException) { }
                int code = (int)r.StatusCode;
                rows.Add(R(code >= 500 ? 2 : code >= 400 ? 1 : 0, m + " " + Draw.Trunc(uri.ToString(), 80), I(code) + " " + r.StatusDescription + " · " + sw.ElapsedMilliseconds + " ms · " + I(buf.Length) + (buf.Length >= 65536 ? "+" : "") + " bytes",
                    (code >= 300 && code < 400 ? "A redirect - shown, not followed. " : "") + (skipped.Count > 0 ? "Not sent (Windows sets these itself, or not a valid header): " + string.Join(", ", skipped) + ". " : "") + "Sent through your proxy settings, like a browser.", "m"));
                foreach (string k in r.Headers.AllKeys.Take(80)) rows.Add(R(3, k, Draw.Trunc(r.Headers[k], 300), "", "m"));
                var bytes = buf.ToArray(); if (bytes.Length == 0) return rows;
                bool text = bytes.Take(2000).All(x => x == 9 || x == 10 || x == 13 || x >= 32);
                rows.Add(R(4, "Body", text ? Draw.Trunc(Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 3000)), 3000) : string.Join(" ", bytes.Take(96).Select(x => x.ToString("x2", CultureInfo.InvariantCulture))) + " …", "", "m"));
            }
            return rows;
        }

        // ---------- STUN: what the internet sees as this PC's address and port (one binding request, RFC 5389)
        public static List<string[]> Stun(string server)
        {
            var rows = new List<string[]>(); string h; int port;
            if (!Lab.Split(server, 3478, out h, out port)) { rows.Add(R(2, "Server", "type a STUN server - e.g. stun.example.com:3478", "Teams, Zoom and most VoIP providers run one; your own SBC or PBX may too.", "u")); return rows; }
            var a = Lab.Addr(h); if (a == null) { rows.Add(R(2, "Server", "couldn't find " + h, "", "m")); return rows; }
            var tid = new byte[12]; new Random().NextBytes(tid);
            var req = new byte[20]; req[1] = 1; req[4] = 0x21; req[5] = 0x12; req[6] = 0xA4; req[7] = 0x42; Array.Copy(tid, 0, req, 8, 12);
            using (var u = new UdpClient(a.AddressFamily))
            {
                u.Client.ReceiveTimeout = 1500; u.Connect(a, port); int local = ((IPEndPoint)u.Client.LocalEndPoint).Port;
                for (int t = 0; t < 3; t++)
                {
                    var sw = Stopwatch.StartNew(); u.Send(req, req.Length); byte[] r;
                    try { var ep = new IPEndPoint(IPAddress.Any, 0); r = u.Receive(ref ep); } catch (SocketException) { continue; }
                    IPEndPoint mapped = ParseStun(r, tid);
                    if (mapped == null) { rows.Add(R(2, a + ":" + port, "answered, but not with an address", "", "m")); return rows; }
                    rows.Add(R(0, "The internet sees you as", mapped.ToString(), "From " + a + ":" + port + " in " + sw.ElapsedMilliseconds + " ms.", "m"));
                    rows.Add(R(3, "Your side", "UDP port " + local + (mapped.Port == local ? " - kept the same through NAT" : " - changed to " + mapped.Port + " by NAT"), mapped.Port == local ? "A NAT that keeps ports is friendlier to calls." : "Normal for most routers; calls still work when the NAT keeps the same mapping for each destination - the Teams UDP test checks that.", "m"));
                    return rows;
                }
            }
            rows.Add(R(2, a + ":" + port, "no answer to 3 tries", "UDP to that port is blocked here, or it isn't a STUN server.", "m"));
            return rows;
        }
        public static IPEndPoint ParseStun(byte[] r, byte[] tid)
        {
            if (r == null || r.Length < 20 || r[0] != 1 || r[1] != 1) return null;
            for (int k = 0; k < 12; k++) if (r[8 + k] != tid[k]) return null;
            int p = 20, end = Math.Min(r.Length, 20 + (r[2] << 8 | r[3])); IPEndPoint plain = null;
            while (p + 4 <= end)
            {
                int ty = r[p] << 8 | r[p + 1], len = r[p + 2] << 8 | r[p + 3], v = p + 4; if (v + len > end) break;
                if ((ty == 0x0020 || ty == 0x0001) && len >= 8 && r[v + 1] == 1)
                {
                    int port = r[v + 2] << 8 | r[v + 3]; var ip = new byte[4]; Array.Copy(r, v + 4, ip, 0, 4);
                    if (ty == 0x0020) { port ^= 0x2112; ip[0] ^= 0x21; ip[1] ^= 0x12; ip[2] ^= 0xA4; ip[3] ^= 0x42; return new IPEndPoint(new IPAddress(ip), port); }
                    plain = new IPEndPoint(new IPAddress(ip), port);
                }
                p = v + ((len + 3) & ~3);
            }
            return plain;
        }

        // ---------- Name of a device: NetBIOS node status, then LLMNR and mDNS reverse lookups - asked of that one device
        static byte[] DnsQ(string name, int type, ushort id)
        {
            var b = new List<byte> { (byte)(id >> 8), (byte)id, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
            foreach (var l in name.Split('.')) { b.Add((byte)l.Length); b.AddRange(Encoding.ASCII.GetBytes(l)); }
            b.AddRange(new byte[] { 0, 0, (byte)type, 0, 1 }); return b.ToArray();
        }
        static string FirstPtr(byte[] r)
        {
            try
            {
                if (r.Length < 12) return null; int qd = r[4] << 8 | r[5], an = r[6] << 8 | r[7], p = 12;
                for (int i = 0; i < qd; i++) { Toolkit.Name(r, ref p); p += 4; }
                for (int i = 0; i < an && p + 10 <= r.Length; i++) { Toolkit.Name(r, ref p); int ty = r[p] << 8 | r[p + 1], len = r[p + 8] << 8 | r[p + 9]; p += 10; if (ty == 12) { int q = p; return Toolkit.Name(r, ref q); } p += len; }
            }
            catch (IndexOutOfRangeException) { }
            return null;
        }
        static byte[] Ask(IPAddress a, int port, byte[] q, int ms)
        {
            using (var u = new UdpClient(a.AddressFamily))
            {
                u.Client.ReceiveTimeout = ms;
                try { u.Send(q, q.Length, new IPEndPoint(a, port)); var ep = new IPEndPoint(IPAddress.Any, 0); var r = u.Receive(ref ep); return ep.Address.Equals(a) ? r : null; } catch (SocketException) { return null; }
            }
        }
        public static List<string[]> NameOf(string ipIn)
        {
            var rows = new List<string[]>(); IPAddress a;
            if (!IPAddress.TryParse((ipIn ?? "").Trim(), out a)) { rows.Add(R(2, "IP", "type the device's IP address - e.g. 192.168.1.23", "", "u")); return rows; }
            Program.Log("name of " + a);
            bool any = false;
            if (a.AddressFamily == AddressFamily.InterNetwork)
            {
                var nb = new List<byte> { 0x13, 0x37, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0x20, (byte)('C'), (byte)('K') };
                for (int i = 1; i < 16; i++) { nb.Add((byte)'A'); nb.Add((byte)'A'); }
                nb.AddRange(new byte[] { 0, 0, 0x21, 0, 1 });
                var r = Ask(a, 137, nb.ToArray(), 1500);
                if (r != null && r.Length > 57)
                {
                    int n = r[56], p = 57; var names = new List<string>(); string group = null;
                    for (int i = 0; i < n && p + 18 <= r.Length; i++, p += 18)
                    {
                        string nm = Encoding.ASCII.GetString(r, p, 15).Trim(); int suffix = r[p + 15]; bool grp = (r[p + 16] & 0x80) != 0;
                        if (grp && suffix == 0) group = nm; else if (!grp && suffix == 0 && !names.Contains(nm)) names.Add(nm);
                    }
                    string mac = p + 6 <= r.Length ? string.Join(":", r.Skip(p).Take(6).Select(x => x.ToString("x2", CultureInfo.InvariantCulture))) : "";
                    if (names.Count > 0) { any = true; rows.Add(R(0, "NetBIOS name", string.Join(", ", names) + (group != null ? " · workgroup / domain " + group : "") + (mac != "" && mac != "00:00:00:00:00:00" ? " · MAC " + mac : ""), "Windows PCs, NAS boxes and printers usually answer this.", "m")); }
                }
            }
            string rev = a.AddressFamily == AddressFamily.InterNetwork ? string.Join(".", a.GetAddressBytes().Reverse()) + ".in-addr.arpa" : string.Join(".", a.GetAddressBytes().Reverse().SelectMany(x => new[] { (x & 15).ToString("x", CultureInfo.InvariantCulture), (x >> 4).ToString("x", CultureInfo.InvariantCulture) })) + ".ip6.arpa";
            var ll = Ask(a, 5355, DnsQ(rev, 12, 0x4242), 1000); string ln = ll != null ? FirstPtr(ll) : null;
            if (ln != null) { any = true; rows.Add(R(0, "LLMNR name", ln, "Windows answers this on home and work networks unless LLMNR is turned off by policy.", "m")); }
            var md = Ask(a, 5353, DnsQ(rev, 12, 0), 1000); string mn = md != null ? FirstPtr(md) : null;
            if (mn != null) { any = true; rows.Add(R(0, "mDNS (Bonjour) name", mn, "Apple devices, printers, smart TVs and Linux usually answer this.", "m")); }
            try { var e = Dns.GetHostEntry(a); if (e.HostName != a.ToString()) { any = true; rows.Add(R(3, "DNS name (PTR)", e.HostName, "From your DNS server, which may know it from DHCP.", "m")); } } catch (SocketException) { } catch (ArgumentException) { }
            if (!any) rows.Add(R(1, a.ToString(), "no name from NetBIOS, LLMNR, mDNS or DNS", "It may block these on its firewall, be off, or not have a name. SNMP or its web page may tell you more.", "m"));
            return rows;
        }

        // ---------- Find devices: ask once over mDNS, SSDP and WS-Discovery, collect answers for 3 seconds
        public static List<string[]> Find(Monitor m, string ok, Toolkit.Run run)
        {
            var rows = new List<string[]>(); if (!Lab.Allowed(m, ok, rows)) return rows;
            Program.Log("find devices (mdns, ssdp, ws-discovery)");
            var found = new Dictionary<string, SortedSet<string>>();
            Action<IPAddress, string> add = (ip, what) => { if (string.IsNullOrWhiteSpace(what)) return; string k = ip.ToString(); SortedSet<string> s; if (!found.TryGetValue(k, out s)) found[k] = s = new SortedSet<string>(); if (s.Count < 12) s.Add(Draw.Trunc(what.Trim(), 120)); };
            var socks = new List<Tuple<UdpClient, string>>();
            try
            {
                var md = new UdpClient(AddressFamily.InterNetwork); socks.Add(Tuple.Create(md, "mdns")); var q = DnsQ("_services._dns-sd._udp.local", 12, 0); md.Send(q, q.Length, new IPEndPoint(IPAddress.Parse("224.0.0.251"), 5353));
                var ss = new UdpClient(AddressFamily.InterNetwork); socks.Add(Tuple.Create(ss, "ssdp")); var s1 = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: ssdp:all\r\n\r\n"); ss.Send(s1, s1.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));
                var ws = new UdpClient(AddressFamily.InterNetwork); socks.Add(Tuple.Create(ws, "wsd"));   /* owned before any send can throw */
                var probe = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:wsa=\"http://schemas.xmlsoap.org/ws/2004/08/addressing\" xmlns:wsd=\"http://schemas.xmlsoap.org/ws/2005/04/discovery\"><soap:Header><wsa:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</wsa:To><wsa:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</wsa:Action><wsa:MessageID>urn:uuid:" + Guid.NewGuid() + "</wsa:MessageID></soap:Header><soap:Body><wsd:Probe/></soap:Body></soap:Envelope>");
                ws.Send(probe, probe.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702));
                var until = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < until && !run.Stop)
                {
                    bool got = false;
                    foreach (var s in socks)
                    {
                        if (s.Item1.Available == 0) continue; got = true; var ep = new IPEndPoint(IPAddress.Any, 0); byte[] r; try { r = s.Item1.Receive(ref ep); } catch (SocketException) { continue; }
                        if (s.Item2 == "mdns") { foreach (var n in Ptrs(r)) add(ep.Address, "mDNS " + n.Replace("._tcp.local", "").Replace("._udp.local", "").TrimStart('_')); }
                        else if (s.Item2 == "ssdp") { string t = Encoding.ASCII.GetString(r); var sv = Regex.Match(t, @"(?im)^SERVER:\s*(.+)$"); var st = Regex.Match(t, @"(?im)^ST:\s*(.+)$"); add(ep.Address, "SSDP " + (sv.Success ? sv.Groups[1].Value.Trim() : "") + (st.Success && st.Groups[1].Value.Contains("device:") ? " · " + Regex.Replace(st.Groups[1].Value.Trim(), @"^urn:[^:]+:device:", "") : "")); }
                        else { string t = Encoding.UTF8.GetString(r); var ty = Regex.Match(t, @"<[^>]*Types>([^<]+)<"); add(ep.Address, "WS-Discovery " + (ty.Success ? Regex.Replace(ty.Groups[1].Value, @"\b\w+:", "") : "device")); }
                    }
                    if (!got) System.Threading.Thread.Sleep(50);
                    run.Status = found.Count + " devices answered...";
                }
            }
            catch (SocketException e) { rows.Add(R(2, "Send", Draw.Trunc(e.Message, 100), "", "m")); return rows; }
            finally { foreach (var s in socks) s.Item1.Close(); }
            rows.Add(R(found.Count > 0 ? 0 : 1, "Answered", I(found.Count) + " devices in 3 s", "Asked once each over mDNS (Apple, printers, TVs), SSDP (UPnP routers, media) and WS-Discovery (Windows, printers, scanners). Devices that don't speak any of them stay silent - Ping sweep finds those.", "m"));
            foreach (var kv in found.OrderBy(k => k.Key.Length).ThenBy(k => k.Key, StringComparer.Ordinal)) rows.Add(R(3, kv.Key, string.Join(" · ", kv.Value), "", "m"));
            return rows;
        }
        static List<string> Ptrs(byte[] r)
        {
            var l = new List<string>();
            try
            {
                int qd = r[4] << 8 | r[5], n = (r[6] << 8 | r[7]) + (r[8] << 8 | r[9]) + (r[10] << 8 | r[11]), p = 12;
                for (int i = 0; i < qd; i++) { Toolkit.Name(r, ref p); p += 4; }
                for (int i = 0; i < n && p + 10 <= r.Length && l.Count < 12; i++) { Toolkit.Name(r, ref p); int ty = r[p] << 8 | r[p + 1], len = r[p + 8] << 8 | r[p + 9]; p += 10; if (ty == 12) { int q = p; l.Add(Toolkit.Name(r, ref q)); } p += len; }
            }
            catch (IndexOutOfRangeException) { }
            return l;
        }

        // ---------- Remote access: the commands for Windows' own OpenSSH, curl and PowerShell, filled in - click one to copy it
        public static bool SafeHost(string h) { return Regex.IsMatch(h ?? "", @"^[A-Za-z0-9]([A-Za-z0-9._:-]{0,252})$|^\[[0-9A-Fa-f:.]+\]$"); }
        public static bool SafeUser(string u) { return Regex.IsMatch(u ?? "", @"^[A-Za-z0-9][A-Za-z0-9._\\@-]{0,63}$"); }
        static string[] Cmd(string name, string cmd, string why) { return new[] { "3", name, cmd, why, "", "copy:" + cmd }; }
        public static List<string[]> Remote(string host, string user, string port)
        {
            var rows = new List<string[]>(); string h = (host ?? "").Trim(), u = (user ?? "").Trim(), ps = (port ?? "").Trim(); int p = 22;
            if (h == "") h = "HOST"; else if (!SafeHost(h)) { rows.Add(R(2, "Host", "letters, digits, dots, dashes and colons only", "", "u")); return rows; }
            if (u != "" && !SafeUser(u)) { rows.Add(R(2, "User", "letters, digits and . _ - \\ @ only", "", "u")); return rows; }
            if (ps != "" && (!int.TryParse(ps, out p) || p < 1 || p > 65535)) { rows.Add(R(2, "Port", "1-65535", "", "u")); return rows; }
            string sys = Environment.GetFolderPath(Environment.SpecialFolder.System); bool ssh = File.Exists(Path.Combine(sys, "OpenSSH", "ssh.exe")), curl = File.Exists(Path.Combine(sys, "curl.exe"));
            string at = (u != "" ? u + "@" : "") + h, pf = p != 22 ? " -p " + p : "", pc = p != 22 ? " -P " + p : "";
            rows.Add(new[] { ssh ? "0" : "1", "Windows OpenSSH", ssh ? "installed" : "not installed", ssh ? "Click a command to copy it, then paste it into PowerShell. Passwords are typed there, never here - AdeeboMeter doesn't keep or see them. Click this line to open PowerShell." : "Settings › System › Optional features › OpenSSH Client (may need IT on a company PC).", "w", "ps:" });
            rows.Add(R(4, "Connect", "", "", "m"));
            rows.Add(Cmd("Log in", "ssh" + pf + " " + at, "The first time, check the fingerprint it shows against the one below."));
            rows.Add(Cmd("Log in, old device", "ssh" + pf + " -o KexAlgorithms=+diffie-hellman-group14-sha1 -o HostKeyAlgorithms=+ssh-rsa " + at, "For old switches that only offer outdated algorithms - only on your own network."));
            rows.Add(Cmd("Run one command", "ssh" + pf + " " + at + " \"show version\"", "Runs it and comes back - handy to copy output."));
            rows.Add(Cmd("Tunnel a web page", "ssh" + pf + " -L 8443:127.0.0.1:443 " + at, "Then open https://localhost:8443 - the device's web page through SSH."));
            rows.Add(R(4, "Copy files", "", "", "m"));
            rows.Add(Cmd("Upload", "scp" + pc + " .\\file.txt " + at + ":/tmp/", "Copies a file from this PC to the device."));
            rows.Add(Cmd("Download", "scp" + pc + " " + at + ":/tmp/file.txt .\\", "Copies a file from the device to this PC's current folder."));
            rows.Add(Cmd("Browse (SFTP)", "sftp" + pc + " " + at, "ls, cd, get, put - like a file manager in text."));
            rows.Add(R(4, "Keys and fingerprints", "", "", "m"));
            rows.Add(Cmd("Its host key", "ssh-keyscan" + pf + " " + h, "What the device presents - compare with what its console shows."));
            rows.Add(Cmd("Fingerprint I trusted", "ssh-keygen -F " + (p != 22 ? "[" + h + "]:" + p : h) + " -l", "What this PC remembered the first time."));
            rows.Add(Cmd("Forget a changed key", "ssh-keygen -R " + (p != 22 ? "[" + h + "]:" + p : h), "Only after you've confirmed the device really was replaced or re-keyed."));
            rows.Add(Cmd("Make my own key", "ssh-keygen -t ed25519", "Then put the .pub file on the device to log in without a password."));
            rows.Add(R(4, "Is it there?", "", "", "m"));
            rows.Add(Cmd("Port open?", "Test-NetConnection " + h + " -Port " + p, "PowerShell's own port test."));
            if (curl) rows.Add(Cmd("Web page headers", "curl.exe -vkI https://" + h + "/", "-k skips the certificate check - for devices with self-signed certificates only."));
            rows.Add(Cmd("Banner (telnet-style)", "curl.exe -v telnet://" + h + ":" + p, "What the port says when you connect - Ctrl+C to leave."));
            return rows;
        }
    }
}
