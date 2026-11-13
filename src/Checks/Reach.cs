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
        // an address as shown in results: any user:password in it is left out (results get copied and saved)
        internal static string NoUser(Uri u) { if (u == null) return ""; if (u.UserInfo == "") return u.ToString(); var b = new UriBuilder(u) { UserName = "", Password = "" }; return b.Uri.ToString(); }
        internal static string NoUser(string s) { Uri u; return Uri.TryCreate(s, UriKind.Absolute, out u) ? NoUser(u) : System.Text.RegularExpressions.Regex.Replace(s ?? "", @"//[^/@\s]*@", "//"); }

        // ---------- HTTP request builder: method, headers and body you choose; redirects shown, not followed
        static readonly char[] BadHdr = { '\r', '\n', '\0' };
        static readonly string[] Methods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS" };
        public static List<string[]> Http(string url, string method, string headers, string body) { return Http(url, method, headers, body, null); }
        public static List<string[]> Http(string url, string method, string headers, string body, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string u = Toolkit.OneSlash(url); if (u != "" && !u.Contains("://")) u = "https://" + u;
            Uri uri; if (!Uri.TryCreate(u, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") || u.Length > 2000) { rows.Add(R(2, "URL", "type a web address - e.g. https://example.com/api/health", "", "u")); return rows; }
            string m = (method ?? "").Trim().ToUpperInvariant(); if (m == "") m = (body ?? "") != "" ? "POST" : "GET";
            if (Array.IndexOf(Methods, m) < 0) { rows.Add(R(2, "Method", "one of " + string.Join(", ", Methods), "", "u")); return rows; }
            HttpWebRequest q;
            try { q = (HttpWebRequest)WebRequest.Create(uri); } catch (NotSupportedException) { rows.Add(R(2, "URL", "not a web address", "", "u")); return rows; }
            q.Method = m; q.AllowAutoRedirect = false; q.Timeout = 15000; q.ReadWriteTimeout = 15000; q.UserAgent = "AdeeboMeter/" + Program.Version;
            var skipped = new List<string>();
            // headers are separated by ";" - but a value may hold one too (Content-Type: application/json; charset=utf-8): a
            // piece without its own "Name:" belongs to the header before it
            var parts = new List<string>();
            foreach (var piece in (headers ?? "").Split(';'))
            {
                int pc = piece.IndexOf(':'), pe = piece.IndexOf('=');
                if (parts.Count > 0 && (pc <= 0 || (pe >= 0 && pe < pc))) parts[parts.Count - 1] += ";" + piece; else parts.Add(piece);
            }
            foreach (var part in parts)
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
            if (Encoding.UTF8.GetByteCount(body ?? "") > 65536) { rows.Add(R(2, "Body", "64 KB at most", "", "u")); return rows; }   // bytes on the wire, not characters
            var data = Encoding.UTF8.GetBytes(body ?? "");
            if (data.Length > 0 && (m == "GET" || m == "HEAD")) rows.Add(R(1, "Body", "not sent - a " + m + " request has no body (use POST or PUT)", "", "u"));
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
                // the body: 64 KB at most, 20 s at most in all (a server dripping a byte at a time can't keep the Toolbox busy), Stop ends it
                var buf = new MemoryStream(); bool cut = false; var dl = Stopwatch.StartNew();
                try { using (var s = r.GetResponseStream()) { var b = new byte[8192]; int k; while (buf.Length < 65536 && (k = s.Read(b, 0, b.Length)) > 0) { buf.Write(b, 0, k); if (dl.ElapsedMilliseconds > 20000 || (run != null && run.Stop)) { cut = true; break; } } } } catch (IOException) { } catch (WebException) { }
                if (cut) rows.Add(R(1, "Body", "stopped reading after " + I((int)buf.Length) + " bytes - the server was sending very slowly (or you pressed Stop)", "", "m"));
                int code = (int)r.StatusCode;
                rows.Add(R(code >= 500 ? 2 : code >= 400 ? 1 : 0, m + " " + Draw.Trunc(uri.ToString(), 80), I(code) + " " + r.StatusDescription + " · " + sw.ElapsedMilliseconds + " ms · " + I(buf.Length) + (buf.Length >= 65536 ? "+" : "") + " bytes",
                    (code >= 300 && code < 400 ? "A redirect - shown, not followed. " : "") + (skipped.Count > 0 ? "Not sent (Windows sets these itself, or not a valid header): " + string.Join(", ", skipped) + ". " : "") + "Sent through your proxy settings, like a browser.", "m"));
                if (r.Headers.Count > 80) rows.Add(R(3, "Headers", r.Headers.Count + " - the first 80 shown", "", "m"));
                foreach (string k in r.Headers.AllKeys.Take(80)) rows.Add(R(3, k, string.Equals(k, "Set-Cookie", StringComparison.OrdinalIgnoreCase) ? "(" + (r.Headers[k] ?? "").Length + " characters)" : Draw.Trunc(r.Headers[k], 300), string.Equals(k, "Set-Cookie", StringComparison.OrdinalIgnoreCase) ? "Cookie values are never shown or copied - they can be a signed-in session." : "", "m"));
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
            var tid = new byte[12]; using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(tid);   // never two calls with one ID
            var req = new byte[20]; req[1] = 1; req[4] = 0x21; req[5] = 0x12; req[6] = 0xA4; req[7] = 0x42; Array.Copy(tid, 0, req, 8, 12);
            bool bad = false;
            using (var u = new UdpClient(a.AddressFamily))
            {
                u.Client.ReceiveTimeout = 1500; u.Connect(a, port); int local = ((IPEndPoint)u.Client.LocalEndPoint).Port;
                for (int t = 0; t < 3; t++)
                {
                    var sw = Stopwatch.StartNew(); u.Send(req, req.Length); byte[] r;
                    try { var ep = new IPEndPoint(IPAddress.Any, 0); r = u.Receive(ref ep); } catch (SocketException) { continue; }
                    IPEndPoint mapped = ParseStun(r, tid);
                    if (mapped == null) { bad = true; continue; }   // a stray or malformed datagram: the other tries still count
                    rows.Add(R(0, "The internet sees you as", mapped.ToString(), "From " + a + ":" + port + " in " + sw.ElapsedMilliseconds + " ms.", "m"));
                    rows.Add(R(3, "Your side", "UDP port " + local + (mapped.Port == local ? " - kept the same through NAT" : " - changed to " + mapped.Port + " by NAT"), mapped.Port == local ? "A NAT that keeps ports is friendlier to calls." : "Normal for most routers; calls still work when the NAT keeps the same mapping for each destination - the Teams UDP test checks that.", "m"));
                    return rows;
                }
            }
            rows.Add(bad ? R(2, a + ":" + port, "answered, but not with an address", "", "m") : R(2, a + ":" + port, "no answer to 3 tries", "UDP to that port is blocked here, or it isn't a STUN server.", "m"));
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
            if (name.Any(ch => ch > 127)) try { name = new System.Globalization.IdnMapping().GetAscii(name.TrimEnd('.')); } catch (ArgumentException) { }
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
            try { var hn = Native.PtrName(a.ToString()); if (hn != "") { any = true; rows.Add(R(3, "DNS name (PTR)", hn, "From your DNS server, which may know it from DHCP.", "m")); } } catch (SocketException) { } catch (ArgumentException) { }
            if (!any) rows.Add(R(1, a.ToString(), "no name from NetBIOS, LLMNR, mDNS or DNS", "It may block these on its firewall, be off, or not have a name. SNMP or its web page may tell you more.", "m"));
            return rows;
        }

        // (Find devices - asking the network over mDNS / SSDP / WS-Discovery - was removed in 1.1.5: it had to keep a socket
        // open for the answers, and AdeeboMeter never listens. Devices on this network and Announcements cover it passively.)
        // same /24 as this PC (the common home and office size); used only to keep the neighbour table to this network
        public static bool SameNet(string ip, string mine)
        {
            IPAddress a, b; if (!IPAddress.TryParse(ip ?? "", out a) || !IPAddress.TryParse(mine ?? "", out b) || a.AddressFamily != AddressFamily.InterNetwork || b.AddressFamily != AddressFamily.InterNetwork) return false;
            var x = a.GetAddressBytes(); var y = b.GetAddressBytes(); return x[0] == y[0] && x[1] == y[1] && x[2] == y[2] && x[3] != 255 && !a.Equals(b);
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
            rows.Add(Cmd("Port open?", "Test-NetConnection " + h.Trim('[', ']') + " -Port " + p, "PowerShell's own port test."));
            if (curl) rows.Add(Cmd("Web page headers", "curl.exe -vkI https://" + h + "/", "-k skips the certificate check - for devices with self-signed certificates only."));
            if (curl) rows.Add(Cmd("Banner (telnet-style)", "curl.exe -v telnet://" + h + ":" + p, "What the port says when you connect - Ctrl+C to leave."));
            return rows;
        }
    }
}
