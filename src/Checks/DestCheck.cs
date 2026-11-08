// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // ================================================================== Check a destination
    sealed class DestResult
    {
        public static readonly string[] Steps = { "DNS", "Route", "Ping", "TCP", "TLS", "HTTP", "Path MTU", "Path" };
        public string Target = "", Verdict = "", Input = ""; public volatile string Note; public volatile List<string[]> Story, Findings; public volatile int Level = -1, Running = -1; public volatile bool Done; public DateTime At = DateTime.Now;
        public string[] Val = new string[8]; public int[] Lv = { -1, -1, -1, -1, -1, -1, -1, -1 };
        // for "How it travels": the handshake timings and what the TLS step saw
        public double DnsMs = double.NaN, TcpMs = double.NaN, TlsMs = double.NaN, TtfbMs = double.NaN; public string Ip = "", TlsProto = "", TlsIssuer = "", Inspected = ""; public int Port;
        public readonly List<KeyValuePair<string, List<string[]>>> Cards = new List<KeyValuePair<string, List<string[]>>>();
        public void AddCard(string title, List<string[]> rows) { lock (Cards) Cards.Add(new KeyValuePair<string, List<string[]>>(title, rows)); }
        public List<KeyValuePair<string, List<string[]>>> CardsCopy() { lock (Cards) return Cards.ToList(); }
    }

    static class DestCheck
    {
        static readonly string[] NoHop = { "No hop answered", "routers on the way filter ICMP" }, Nobody = { "Nobody", "no app is connected to it now" }, MtuMethod = { "Method", "ping, don't fragment" };
        static readonly Regex hostRx = new Regex(@"^(?=.{1,253}$)([A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)*[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?$");

        // Only a host name, an IP address or an http(s) URL gets through. Nothing typed ever reaches a shell.
        public static bool Parse(string input, out string host, out int port, out bool tls, out string err)
        {
            host = ""; port = 443; tls = true; err = "";
            string s = (input ?? "").Trim();
            if (s.Length == 0 || s.Length > 300) { err = Voice.P("Give me a host, an IP or a URL first.", "Type a host, IP or URL."); return false; }
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Uri u; if (!Uri.TryCreate(s, UriKind.Absolute, out u) || string.IsNullOrEmpty(u.Host)) { err = "That URL doesn't parse."; return false; }
                host = u.IdnHost.Trim('[', ']'); port = u.Port; tls = u.Scheme == "https";
            }
            else
            {
                var m6 = Regex.Match(s, @"^\[([0-9A-Fa-f:.]+)\](?::(\d{1,5}))?$");
                IPAddress ip;
                if (m6.Success) { host = m6.Groups[1].Value; if (m6.Groups[2].Success) port = int.Parse(m6.Groups[2].Value, CultureInfo.InvariantCulture); }
                else if (IPAddress.TryParse(s, out ip) && s.Count(c => c == ':') != 1) host = s;
                else
                {
                    int colon = s.LastIndexOf(':');
                    if (colon > 0) { int p; if (!int.TryParse(s.Substring(colon + 1), out p)) { err = "The port after ':' must be a number."; return false; } port = p; s = s.Substring(0, colon); }
                    host = s;
                }
                tls = port != 80;
                if (tls && port != 443 && port != 8443 && port != 993 && port != 995 && port != 465 && port != 636 && port != 853) tls = false;
            }
            IPAddress tmp;
            if (port < 1 || port > 65535) { err = "Ports go from 1 to 65535."; return false; }
            if (!IPAddress.TryParse(host, out tmp) && !hostRx.IsMatch(host)) { err = Voice.P("That doesn't look like a host, an IP or a URL, boss.", "That is not a valid host, IP or URL."); return false; }
            return true;
        }

        public static void Run(string input, DestResult r, Action changed, Monitor m)
        {
            r.Input = (input ?? "").Trim();
            try
            {
                RunSteps(input, r, changed, m);
                // Only when something failed: is a Wi-Fi sign-in page (captive portal) the real reason?
                if (r.Level >= 1 && r.Target != "" && Cfg.Portal) Portal(r, changed);
            }
            finally { r.Running = -1; r.Done = true; changed(); }
        }

        // Same plain-HTTP probe Windows itself sends to decide "No internet / sign in required" (NCSI), so no new destination
        // leaves the PC. Only the status line and the Location host are read; the redirect is never followed.
        const string NcsiHost = "www.msftconnecttest.com", NcsiBody = "Microsoft Connect Test";
        static readonly string[] PortalAsked = { "Asked", "msftconnecttest.com (as Windows does)" }, HeadEnd = { "\r\n\r\n" };
        static void Portal(DestResult r, Action changed)
        {
            r.Note = Voice.P("One more thing - checking whether a Wi-Fi sign-in page is in the way...", "Checking for a captive portal (sign-in page)..."); changed();
            string resp = null, why = "";
            try
            {
                var dns = Task.Factory.StartNew(() => Dns.GetHostAddresses(NcsiHost));
                if (!dns.Wait(4000)) why = "no DNS answer";
                else
                {
                    var ip = dns.Result.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? dns.Result.FirstOrDefault();
                    if (ip == null) why = "no address";
                    else using (var sock = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                    {
                        var ar = sock.BeginConnect(ip, 80, null, null);
                        if (!ar.AsyncWaitHandle.WaitOne(4000)) why = "port 80 blocked";
                        else
                        {
                            sock.EndConnect(ar); sock.ReceiveTimeout = 4000;
                            var req = Encoding.ASCII.GetBytes("GET /connecttest.txt HTTP/1.1\r\nHost: " + NcsiHost + "\r\nUser-Agent: AdeeboMeter\r\nConnection: close\r\n\r\n");
                            sock.Send(req);
                            var buf = new byte[4096]; int got = 0, n;
                            try { while (got < buf.Length && (n = sock.Receive(buf, got, buf.Length - got, SocketFlags.None)) > 0) got += n; } catch (SocketException) { }
                            resp = Encoding.ASCII.GetString(buf, 0, got);
                        }
                    }
                }
            }
            catch (Exception e) { why = e is SocketException ? ((SocketException)e).SocketErrorCode.ToString() : "failed"; }
            string where; int kind = ClassifyPortal(resp, out where);   // 0 open, 1 portal, 2 can't tell
            if (resp == null) kind = 2;
            r.AddCard("SIGN-IN PAGE (CAPTIVE PORTAL)", new List<string[]> {
                PortalAsked,
                new[] { "Result", kind == 0 ? "none - internet is open" : kind == 1 ? "found" + (where != "" ? ": " + where : "") : "can't tell - " + (why != "" ? why : "odd answer"), kind == 1 ? "2" : "0" } });
            if (kind == 1)
                r.Verdict = Voice.P("Found the real culprit: a Wi-Fi sign-in page is holding everything hostage" + (where != "" ? " (" + where + ")" : "") + ". Open a browser, sign in or accept the terms, then check again.",
                                    "Captive portal detected" + (where != "" ? " (" + where + ")" : "") + ". Sign in through a browser, then retry.");
            if (kind == 1) r.Level = 2;
            r.Note = null;
        }

        // 0 = the expected answer (no portal), 1 = something else answered for it (portal), 2 = unknown
        public static int ClassifyPortal(string resp, out string where)
        {
            where = "";
            if (string.IsNullOrEmpty(resp)) return 2;
            var head = resp.Split(HeadEnd, 2, StringSplitOptions.None);
            var lines = head[0].Split('\n');
            var m = Regex.Match(lines[0].Trim(), @"^HTTP/\d(?:\.\d)?\s+(\d{3})");
            if (!m.Success) return 2;
            int status = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            string body = head.Length > 1 ? head[1].Trim() : "";
            if (status == 200 && body.StartsWith(NcsiBody, StringComparison.Ordinal)) return 0;
            if (status >= 300 && status < 400)
            {
                foreach (var l in lines)
                    if (l.StartsWith("Location:", StringComparison.OrdinalIgnoreCase))
                    {
                        Uri u; if (Uri.TryCreate(l.Substring(9).Trim(), UriKind.Absolute, out u) && (u.Scheme == "http" || u.Scheme == "https")) where = Draw.Trunc(u.Host, 40);
                    }
                return 1;
            }
            if (status == 200 || status == 511) return 1;   // page replaced, or "network authentication required"
            return 2;
        }

        static void RunSteps(string input, DestResult r, Action changed, Monitor m)
        {
            var deadline = DateTime.UtcNow.AddSeconds(45);
            string host; int port; bool tls; string err;
            if (!Parse(input, out host, out port, out tls, out err)) { Finish(r, 1, err); changed(); return; }
            r.Target = host + ":" + port;
            Action<int, int, string> step = delegate(int i, int lv, string val) { r.Val[i] = val; r.Lv[i] = lv; r.Running = i + 1; changed(); };
            Action<int> skipRest = delegate(int from) { for (int i = from; i < 8; i++) if (r.Lv[i] < 0) { r.Val[i] = "skipped"; r.Lv[i] = 3; } };
            r.Running = 0; changed();

            // 1. DNS (real lookup, skipping the cache)
            var addrs = new List<IPAddress>(); IPAddress lit; string dnsSrv; bool vpn; lock (m.L) { dnsSrv = m.DnsServer; vpn = m.VpnUp; }
            if (IPAddress.TryParse(host, out lit)) { addrs.Add(lit); step(0, 0, "IP given"); }
            else
            {
                var sw = Stopwatch.StartNew(); int rc4 = QueryAll(host, 1, addrs); QueryAll(host, 28, addrs); double ms = sw.Elapsed.TotalMilliseconds; r.DnsMs = ms;
                if (addrs.Count == 0)
                {
                    step(0, 2, rc4 == 9003 ? "no such name" : rc4 == 1460 ? "timed out" : "failed (" + rc4 + ")");
                    r.AddCard("DNS", new List<string[]> { new[] { "Server", (dnsSrv != "" ? dnsSrv : "system default") + (vpn ? " (VPN)" : "") }, new[] { "Answer", rc4 == 9003 ? "NXDOMAIN - no such name" : "error " + rc4, "2" } });
                    skipRest(1);
                    CompareDns(r, host, new List<string>());
                    bool filtered = r.Findings != null && r.Findings.Any(f => f[1] == "2");
                    if (filtered) { Finish(r, 2, Voice.P("Your DNS says " + host + " doesn't exist - but a public resolver finds it. A DNS filter (AdGuard, a company filter or the VPN's DNS) is blocking this name.", "Blocked by your DNS: the name resolves on a public resolver.")); changed(); return; }
                    Finish(r, 2, rc4 == 9003 ? Voice.P("Can't even find its address, boss - the DNS server says that name doesn't exist. Typo, or an internal name that needs the VPN?", "DNS says the name does not exist. Check the spelling, or connect the VPN for internal names.")
                                             : Voice.P("DNS didn't answer for " + host + ". The server (" + dnsSrv + ") is slow or unreachable.", "DNS lookup failed for " + host + " (server " + dnsSrv + ")."));
                    changed(); return;
                }
                step(0, ms > 300 ? 1 : 0, ms.ToString("0", CultureInfo.CurrentCulture) + " ms");
                var dc = new List<string[]> { new[] { "Server", (dnsSrv != "" ? dnsSrv : "system default") + (vpn ? " (VPN)" : "") }, new[] { "Time", ms.ToString("0", CultureInfo.CurrentCulture) + " ms" } };
                foreach (var a in addrs.Take(4)) dc.Add(new[] { a.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4", a.ToString() });
                r.AddCard("DNS", dc);
            }
            var target = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addrs[0];
            if (!IPAddress.TryParse(host, out lit)) CompareDns(r, host, addrs.Where(a => a.AddressFamily == AddressFamily.InterNetwork).Select(a => a.ToString()).ToList());

            // 2. Route: which adapter the OS would use (a UDP "connect" sends nothing)
            string via = "?"; bool viaVpn = false; string localIp = "";
            try
            {
                using (var u = new Socket(target.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
                {
                    u.Connect(target, port); localIp = ((IPEndPoint)u.LocalEndPoint).Address.ToString();
                    foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                        foreach (var ua in n.GetIPProperties().UnicastAddresses)
                            if (ua.Address.ToString() == localIp) { via = n.Name; viaVpn = Monitor.IsVpn(n); }
                }
                step(1, 0, viaVpn ? "VPN" : Draw.Trunc(via, 14));
            }
            catch { step(1, 2, "no route"); }
            r.AddCard("ROUTE", new List<string[]> { new[] { "Leaves via", via + (viaVpn ? " (VPN)" : "") }, new[] { "Local address", localIp }, new[] { "Proxy (browsers)", ProxyInfo() } });

            // 3. Ping
            int ok = 0; double sum = 0;
            for (int i = 0; i < 4 && DateTime.UtcNow < deadline; i++) { double t = Monitor.PingMs(target.ToString()); if (!double.IsNaN(t)) { ok++; sum += t; } }
            bool icmp = ok > 0;
            step(2, icmp ? (ok < 4 ? 1 : 0) : 3, icmp ? (sum / ok).ToString("0", CultureInfo.CurrentCulture) + " ms · " + ((4 - ok) * 25) + "%" : "no reply");

            // 4. TCP connect
            Socket sock = null; string tcpErr = ""; double tcpMs = 0, tcpFailMs = 0; Stopwatch tcpSw = null;
            try
            {
                sock = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                tcpSw = Stopwatch.StartNew(); var ar = sock.BeginConnect(target, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(4000)) { tcpErr = "timeout"; sock.Close(); sock = null; }
                else { sock.EndConnect(ar); tcpMs = tcpSw.Elapsed.TotalMilliseconds; r.TcpMs = tcpMs; r.Ip = target.ToString(); r.Port = port; }
            }
            catch (SocketException se) { tcpFailMs = tcpSw != null ? tcpSw.Elapsed.TotalMilliseconds : 0; tcpErr = se.SocketErrorCode == SocketError.ConnectionRefused ? "refused" : se.SocketErrorCode.ToString(); if (sock != null) sock.Close(); sock = null; }
            catch (ObjectDisposedException) { tcpErr = "timeout"; sock = null; }
            if (sock == null)
            {
                step(3, 2, tcpErr); skipRest(4);
                // Even when the port fails, the path tells us a lot (firewall or not) - trace it and tell the story.
                if (target.AddressFamily == AddressFamily.InterNetwork && DateTime.UtcNow < deadline)
                {
                    r.Note = Voice.P("Port's shut - tracing the path to see who shut it...", "Tracing the path..."); changed();
                    var fh = Trace(target, 20); int fl = fh.FindIndex(hp => hp.Item1 == target.ToString());
                    Story(r, target, fh, fl, localIp, viaVpn, 0, tcpErr, 0, tcpFailMs, icmp ? sum / ok : double.NaN, port);
                    r.Note = null;
                }
                Finish(r, 2, tcpErr == "refused"
                    ? Voice.P("The server is there but slammed the door on port " + port + " (connection refused). Wrong port, or the service isn't running.", "Connection refused on port " + port + ". Wrong port or service not running.")
                    : Voice.P("Address found, but nobody answers on port " + port + ". A firewall is quietly eating the packets, or the service is down.", "No answer on port " + port + " (timeout). Likely a firewall or the service is down."));
                Who(r, m, addrs); changed(); return;
            }
            step(3, tcpMs > 300 ? 1 : 0, tcpMs.ToString("0", CultureInfo.CurrentCulture) + " ms");

            // 5. TLS - TLS 1.2 / 1.3 only, and real Windows certificate validation (never TLS 1.0/1.1, never skip certificate checks).
            // The callback only *records* the certificate and the verdict so they can be reported; it returns Windows' own answer,
            // so an untrusted certificate ends the handshake and nothing at all is sent to that server.
            bool tlsStalled = false, tlsUntrusted = false, tlsFailed = false, tlsLegacy = false; Stream stream = new NetworkStream(sock, true);
            if (tls)
            {
                SslPolicyErrors errs = SslPolicyErrors.None; X509Certificate2 cert = null;
                var ssl = new SslStream(stream, false, delegate(object o, X509Certificate c, X509Chain ch, SslPolicyErrors e)
                {
                    errs = e; if (c != null) cert = new X509Certificate2(c);
                    return e == SslPolicyErrors.None;
                });
                ssl.ReadTimeout = 6000; ssl.WriteTimeout = 6000;
                var sw = Stopwatch.StartNew(); string sni = IPAddress.TryParse(host, out lit) ? target.ToString() : host;
                var allowed = SslProtocols.Tls12 | (Tls13Client() ? (SslProtocols)12288 : 0);
                var task = Task.Factory.StartNew(() => ssl.AuthenticateAsClient(sni, null, allowed, true));
                bool done = false; string tErr = "";
                try { done = task.Wait(8000); } catch (AggregateException ae) { Exception ie = ae; while (ie.InnerException != null) ie = ie.InnerException; tErr = ie.Message; done = true; }
                tlsUntrusted = cert != null && errs != SslPolicyErrors.None;
                if (!done) { tlsStalled = true; step(4, 2, "stalls"); try { ssl.Dispose(); } catch { } stream = Stream.Null; }
                else if (tlsUntrusted || tErr == "")
                {
                    string proto = tlsUntrusted ? "-" : ProtoName(ssl.SslProtocol);
                    int days = cert != null ? (int)(cert.NotAfter - DateTime.Now).TotalDays : 0;
                    r.TlsMs = sw.Elapsed.TotalMilliseconds; r.TlsProto = proto; r.TlsIssuer = cert != null ? cert.GetNameInfo(X509NameType.SimpleName, true) : "";
                    if (cert != null && !tlsUntrusted) r.Inspected = Inspection.InspectedBy(cert);
                    step(4, tlsUntrusted ? 2 : days < 14 ? 1 : 0, tlsUntrusted ? "untrusted" : proto + " · " + sw.Elapsed.TotalMilliseconds.ToString("0", CultureInfo.CurrentCulture) + " ms");
                    if (cert != null)
                        r.AddCard("TLS CERTIFICATE", new List<string[]> {
                            new[] { "Name", Draw.Trunc(cert.GetNameInfo(X509NameType.SimpleName, false), 32) },
                            new[] { "Issuer", Draw.Trunc(cert.GetNameInfo(X509NameType.SimpleName, true), 32), tlsUntrusted ? "2" : "0" },
                            new[] { "Expires", days < 0 ? "expired " + (-days) + " days ago" : "in " + days + " days", days < 14 ? "2" : "0" },
                            new[] { "Version", tlsUntrusted ? "not completed" : proto },
                            new[] { "Trusted by Windows", tlsUntrusted ? "no - " + errs : "yes", tlsUntrusted ? "2" : "0" },
                            new[] { "Inspected on the way", r.Inspected == "" ? "no - straight to the server" : "yes - by " + Draw.Trunc(r.Inspected, 26), r.Inspected == "" ? "0" : "1" } });
                    if (tlsUntrusted) { try { ssl.Dispose(); } catch { } stream = Stream.Null; }
                    else stream = ssl;
                }
                else
                {
                    // handshake refused before any certificate: most often the server only speaks TLS 1.0 / 1.1
                    tlsLegacy = cert == null && (tErr.IndexOf("algorithm", StringComparison.OrdinalIgnoreCase) >= 0 || tErr.IndexOf("protocol", StringComparison.OrdinalIgnoreCase) >= 0 || tErr.IndexOf("handshake", StringComparison.OrdinalIgnoreCase) >= 0);
                    tlsFailed = true; step(4, 2, tlsLegacy ? "legacy TLS?" : "failed");
                    r.AddCard("TLS", new List<string[]> { new[] { "Error", Draw.Trunc(tErr, 40), "2" }, new[] { "Offered by AdeeboMeter", Tls13Client() ? "TLS 1.2, 1.3" : "TLS 1.2" } });
                    try { ssl.Dispose(); } catch { } stream = Stream.Null;
                }
            }
            else step(4, 3, "not TLS");

            // 6. HTTP (one HEAD request, nothing else sent)
            int status = 0;
            if ((port == 80 || port == 8080 || tls) && stream != Stream.Null && stream.CanWrite)
            {
                try
                {
                    var req = Encoding.ASCII.GetBytes("HEAD / HTTP/1.1\r\nHost: " + host + "\r\nUser-Agent: AdeeboMeter\r\nConnection: close\r\n\r\n");
                    var sw = Stopwatch.StartNew(); stream.ReadTimeout = 6000; stream.Write(req, 0, req.Length);
                    var buf = new byte[256]; int n = stream.Read(buf, 0, buf.Length); double ttfb = sw.Elapsed.TotalMilliseconds; r.TtfbMs = ttfb;
                    var line = Encoding.ASCII.GetString(buf, 0, Math.Max(0, n)).Split('\n')[0].Trim();
                    var mm = Regex.Match(line, @"^HTTP/\d(\.\d)?\s+(\d{3})");
                    if (mm.Success)
                    {
                        status = int.Parse(mm.Groups[2].Value, CultureInfo.InvariantCulture);
                        step(5, status >= 500 ? 2 : status >= 400 ? 1 : 0, status + " · " + ttfb.ToString("0", CultureInfo.CurrentCulture) + " ms");
                        r.AddCard("HTTP", new List<string[]> { new[] { "Status", Draw.Trunc(line, 32) }, new[] { "Time to first byte", ttfb.ToString("0", CultureInfo.CurrentCulture) + " ms" } });
                    }
                    else step(5, 3, "not HTTP");
                }
                catch { step(5, 1, "no answer"); }
            }
            else step(5, 3, "skipped");
            try { stream.Dispose(); } catch { }
            try { sock.Close(); } catch { }

            // 7. Path MTU (IPv4, don't-fragment pings)
            int mtu = 0;
            if (target.AddressFamily == AddressFamily.InterNetwork && icmp && DateTime.UtcNow < deadline)
            {
                if (DfPing(target, 1472)) mtu = 1500;
                else
                {
                    int lo = 1172, hi = 1471; if (!DfPing(target, lo)) lo = 548;
                    for (int i = 0; i < 9 && lo < hi; i++) { int mid = (lo + hi + 1) / 2; if (DfPing(target, mid)) lo = mid; else hi = mid - 1; }
                    mtu = lo + 28;
                }
                int lmtu; lock (m.L) lmtu = viaVpn && m.VpnMtu > 0 ? m.VpnMtu : m.PrimaryMtu;
                step(6, mtu < 1500 && tlsStalled ? 2 : mtu < 1500 ? 1 : 0, mtu.ToString(CultureInfo.CurrentCulture));
                r.AddCard("PATH MTU", new List<string[]> { new[] { "Largest that passes", mtu + " bytes", mtu < 1500 ? "1" : "0" }, new[] { "Your adapter sends", lmtu > 0 ? lmtu + " bytes" : "?" }, MtuMethod });
            }
            else step(6, 3, icmp ? "IPv6" : "ICMP blocked");

            // 8. Path: MTR-style, 3 rounds per hop, all hops at once (~3 s)
            if (target.AddressFamily == AddressFamily.InterNetwork && DateTime.UtcNow < deadline)
            {
                var multi = new List<int>();
                var hops = Trace(target, 20, multi);
                int last = hops.FindIndex(hp => hp.Item1 == target.ToString());
                var rows = new List<string[]>();
                int upto = last >= 0 ? last : Math.Min(hops.Count, 12) - 1;
                for (int i = 0; i <= upto && i < hops.Count; i++)
                    rows.Add(new[] { (i + 1) + "  " + (hops[i].Item1 ?? "*"), hops[i].Item1 == null ? "no reply" : hops[i].Item2.ToString("0", CultureInfo.CurrentCulture) + " ms · loss " + hops[i].Item3 + "%", hops[i].Item3 > 0 && i == upto ? "1" : "0" });
                if (hops.All(hp => hp.Item1 == null)) rows = new List<string[]> { NoHop };
                if (rows.Count > 10) rows = rows.Take(4).Concat(new[] { new[] { "...", (rows.Count - 7) + " more hops" } }).Concat(rows.Skip(rows.Count - 3)).ToList();
                r.AddCard("PATH (3 ROUNDS PER HOP)", rows);
                step(7, last >= 0 ? (hops[last].Item3 > 0 ? 1 : 0) : 3, last >= 0 ? (last + 1) + " hops" : "not traced");
                r.Note = Voice.P("Reading the router names along the way...", "Naming the hops..."); changed();
                Story(r, target, hops, last, localIp, viaVpn, mtu, "", tcpMs, 0, icmp ? sum / ok : double.NaN, port, null, multi);
                r.Note = null;
            }
            else step(7, 3, "skipped");

            Who(r, m, addrs);
            if (tlsStalled && mtu > 0 && mtu < 1500) Finish(r, 2, Voice.P("Classic MTU black hole: small packets walk through, big ones vanish. Path MTU is " + mtu + " - TLS stalls at the certificate.", "Path MTU is " + mtu + "; large packets are dropped, so TLS stalls. Check MSS clamping on the path."));
            else if (tlsStalled) Finish(r, 2, Voice.P("TCP connects, then TLS just... stops. Something in the middle is holding the handshake - a proxy, a firewall, or an MTU problem.", "TLS handshake stalls after TCP connects (proxy, firewall or MTU)."));
            else if (tlsUntrusted) Finish(r, 2, Voice.P("Gets to the door but the bouncer won't let you in: Windows doesn't trust the certificate (often an inspection proxy).", "TLS works but the certificate is not trusted by Windows (often an inspection proxy)."));
            else if (tlsFailed && tlsLegacy) Finish(r, 2, Voice.P("TCP is fine, but the server won't do TLS 1.2 or newer - it's stuck in the past (TLS 1.0 / 1.1). That's on the server.", "TLS handshake failed: the server likely supports only TLS 1.0/1.1."));
            else if (tlsFailed) Finish(r, 2, Voice.P("TCP is fine, TLS fails. The two sides can't agree on how to talk securely.", "TLS handshake failed."));
            else if (status == 401 || status == 403 || status == 407) Finish(r, 1, Voice.P("The network's fine and the server answers - it just won't let you in (HTTP " + status + "). Sign in, VPN, or a proxy rule.", "Reachable, but access is refused (HTTP " + status + ")."));
            else if (status >= 500) Finish(r, 2, Voice.P("The network's fine - the site itself is broken (HTTP " + status + "). Not you, boss.", "Network is fine; the server returns HTTP " + status + "."));
            else
            {
                int worst = r.Lv.Max(x => x == 3 ? 0 : x);
                string ttfbTxt = r.Lv[5] == 0 && r.Val[5] != null && r.Val[5].Contains("·") ? " - " + r.Val[5].Split('·').Last().Trim() + " to first byte" : "";
                Finish(r, worst >= 1 ? 1 : 0, Voice.P(host + " is happy to see you" + ttfbTxt + ". If it feels slow, it isn't the network.", host + " is reachable" + ttfbTxt + "."));
            }
            changed();
        }


        // ================================================================ The path story
        // Offline, AdeeboMeter can't look up AS numbers - but routers usually have names (reverse DNS), and the name says who runs
        // them. Where the owner changes, your traffic crossed from one network to another: that's a BGP peering / transit handoff.
        // Names are looked up only here, only for a check you started, through your normal DNS server.
        static readonly string[][] Carriers =
        {
            new[] { "rogers.com", "Rogers" }, new[] { "bell.ca", "Bell" }, new[] { "bell.net", "Bell" }, new[] { "telus.net", "TELUS" }, new[] { "telus.com", "TELUS" },
            new[] { "videotron.ca", "Videotron" }, new[] { "shawcable.net", "Shaw / Rogers" }, new[] { "cogentco.com", "Cogent" }, new[] { "ntt.net", "NTT" },
            new[] { "level3.net", "Lumen (Level 3)" }, new[] { "lumen.com", "Lumen" }, new[] { "he.net", "Hurricane Electric" }, new[] { "telia.net", "Arelion (Telia)" },
            new[] { "arelion.net", "Arelion" }, new[] { "zayo.com", "Zayo" }, new[] { "gtt.net", "GTT" }, new[] { "comcast.net", "Comcast" }, new[] { "alter.net", "Verizon" },
            new[] { "verizon-gni.net", "Verizon" }, new[] { "att.net", "AT&T" }, new[] { "cox.net", "Cox" }, new[] { "rr.com", "Charter Spectrum" }, new[] { "charter.com", "Charter Spectrum" },
            new[] { "1e100.net", "Google" }, new[] { "google.com", "Google" }, new[] { "amazonaws.com", "Amazon AWS" }, new[] { "msn.net", "Microsoft" },
            new[] { "microsoft.com", "Microsoft" }, new[] { "akamaitechnologies.com", "Akamai" }, new[] { "akamai.net", "Akamai" }, new[] { "cloudflare.com", "Cloudflare" },
            new[] { "fastly.net", "Fastly" }, new[] { "as6453.net", "Tata Communications" }, new[] { "tatacommunications.com", "Tata Communications" },
            new[] { "seabone.net", "Sparkle" }, new[] { "pccwglobal.net", "PCCW Global" }, new[] { "telstraglobal.net", "Telstra" }, new[] { "starlinkisp.net", "Starlink" },
            new[] { "github.com", "GitHub" }, new[] { "facebook.com", "Meta" }, new[] { "fbcdn.net", "Meta" }, new[] { "torix.ca", "TorIX (internet exchange)" },
            new[] { "ottix.net", "OttIX (internet exchange)" }, new[] { "equinix.com", "Equinix (exchange)" }, new[] { "de-cix.net", "DE-CIX (exchange)" }, new[] { "ams-ix.net", "AMS-IX (exchange)" }
        };
        static readonly string[][] Cities =
        {
            new[] { "yyz", "Toronto" }, new[] { "tor", "Toronto" }, new[] { "toronto", "Toronto" }, new[] { "yow", "Ottawa" }, new[] { "ott", "Ottawa" }, new[] { "ottawa", "Ottawa" },
            new[] { "yul", "Montreal" }, new[] { "mtl", "Montreal" }, new[] { "mon", "Montreal" }, new[] { "yvr", "Vancouver" }, new[] { "van", "Vancouver" }, new[] { "yyc", "Calgary" },
            new[] { "nyc", "New York" }, new[] { "jfk", "New York" }, new[] { "ewr", "Newark" }, new[] { "iad", "Washington DC" }, new[] { "ash", "Ashburn" }, new[] { "wdc", "Washington DC" },
            new[] { "ord", "Chicago" }, new[] { "chi", "Chicago" }, new[] { "dfw", "Dallas" }, new[] { "dal", "Dallas" }, new[] { "lax", "Los Angeles" }, new[] { "sjc", "San Jose" },
            new[] { "sfo", "San Francisco" }, new[] { "sea", "Seattle" }, new[] { "atl", "Atlanta" }, new[] { "mia", "Miami" }, new[] { "bos", "Boston" }, new[] { "den", "Denver" },
            new[] { "lhr", "London" }, new[] { "lon", "London" }, new[] { "fra", "Frankfurt" }, new[] { "ams", "Amsterdam" }, new[] { "par", "Paris" }, new[] { "cdg", "Paris" },
            new[] { "sin", "Singapore" }, new[] { "hkg", "Hong Kong" }, new[] { "nrt", "Tokyo" }, new[] { "tyo", "Tokyo" }, new[] { "syd", "Sydney" }, new[] { "dxb", "Dubai" },
            new[] { "bom", "Mumbai" }, new[] { "del", "Delhi" }, new[] { "dac", "Dhaka" }, new[] { "mrs", "Marseille" }
        };
        static readonly string[] TwoLevel = { "co.uk", "com.au", "net.au", "co.jp", "ne.jp", "co.nz", "com.br", "com.cn", "net.cn", "co.in", "net.in", "com.bd", "net.bd", "com.sg" };
        static readonly char[] LabelSplit = { '.', '-' };

        public static string OwnerOf(string ptr)
        {
            if (string.IsNullOrEmpty(ptr)) return null;
            var parts = ptr.TrimEnd('.').ToLowerInvariant().Split('.');
            if (parts.Length < 2) return null;
            string two = parts[parts.Length - 2] + "." + parts[parts.Length - 1];
            string dom = TwoLevel.Contains(two) && parts.Length >= 3 ? parts[parts.Length - 3] + "." + two : two;
            foreach (var c in Carriers) if (dom == c[0]) return c[1];
            return dom;
        }
        public static string CityOf(string ptr)
        {
            if (string.IsNullOrEmpty(ptr)) return null;
            var labels = ptr.ToLowerInvariant().Split(LabelSplit);
            foreach (var l in labels.Take(Math.Max(0, labels.Length - 2)))   // skip the domain itself
            {
                string t = Regex.Replace(l, @"\d+$", "");
                foreach (var c in Cities) if (t == c[0]) return c[1];
            }
            return null;
        }
        public static string Kind(string ip)
        {
            IPAddress a; if (ip == null || !IPAddress.TryParse(ip, out a) || a.AddressFamily != AddressFamily.InterNetwork) return "public";
            var b = a.GetAddressBytes();
            if (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168)) return "private";
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return "cgnat";
            if (b[0] == 169 && b[1] == 254) return "linklocal";
            return "public";
        }
        static Dictionary<string, string> Ptrs(IEnumerable<string> ips)
        {
            var tasks = ips.Distinct().Select(ip => Task.Factory.StartNew(() =>
            {
                try { var h = Dns.GetHostEntry(IPAddress.Parse(ip)).HostName; return Tuple.Create(ip, h != ip ? h : null); }
                catch { return Tuple.Create(ip, (string)null); }
            })).ToArray();
            try { Task.WaitAll(tasks, 3000); } catch (AggregateException) { }
            return tasks.Where(t => t.Status == TaskStatus.RanToCompletion && t.Result.Item2 != null).ToDictionary(t => t.Result.Item1, t => t.Result.Item2);
        }
        // Initial TTL guess: replies start at 64 (Linux, most servers), 128 (Windows) or 255 (network gear).
        static int ReplyTtl(IPAddress target)
        {
            try { using (var p = new Ping()) { var rep = p.Send(target, 1000); return rep.Status == IPStatus.Success && rep.Options != null ? rep.Options.Ttl : -1; } }
            catch { return -1; }
        }

        static string[] Row(params string[] v) { return v; }
        // Ask a public resolver the same question. Different answers are usually a CDN picking a nearby server;
        // "blocked" answers (0.0.0.0, 127.x, NXDOMAIN while the public resolver finds it) mean a filter is in the way.
        static void CompareDns(DestResult r, string host, List<string> mine)
        {
            string err; var pub = DnsAsk.Ask(host, "1.1.1.1", 2500, out err);
            if (err != "") pub = DnsAsk.Ask(host, "8.8.8.8", 2500, out err);
            if (err != "") { r.AddCard("DNS SECOND OPINION", new List<string[]> { Row("Public resolver", "couldn't ask (" + err + ") - the network may only allow its own DNS") }); return; }
            bool pubNx = pub.Count == 1 && pub[0] == "NXDOMAIN"; var pubIps = pub.Where(x => x != "NXDOMAIN").ToList();
            bool blocked = mine.Count > 0 && mine.All(ip => ip == "0.0.0.0" || ip.StartsWith("127.", StringComparison.Ordinal)) && pubIps.Count > 0;
            bool missing = mine.Count == 0 && pubIps.Count > 0;
            bool same = mine.Intersect(pubIps).Any();
            string verdict = blocked || missing ? "your DNS blocks this name" : pubNx ? "public DNS doesn't know it (internal name?)" : same ? "same answer - nothing fishy" : "different answer (usually a nearby CDN server)";
            r.AddCard("DNS SECOND OPINION", new List<string[]> { Row("Public resolver (1.1.1.1)", pubNx ? "no such name" : string.Join(", ", pubIps.Take(3))), Row("Verdict", verdict, blocked || missing ? "2" : same || pubNx ? "0" : "1") });
            var f = r.Findings ?? new List<string[]>();
            if (blocked || missing) f.Add(Row("Your DNS answered " + (missing ? "'no such name'" : string.Join(", ", mine.Take(2))) + ", but a public resolver finds " + string.Join(", ", pubIps.Take(2)) + ". Something is filtering names - a DNS blocker (like AdGuard), a corporate filter, or the VPN's DNS.", "2"));
            else if (!same && !pubNx && mine.Count > 0) f.Add(Row("Your DNS and a public resolver gave different addresses. That's normal for big sites (each DNS gets a nearby server). If the TLS check below passes, the answer is genuine - certificates can't be faked that easily.", "-1"));
            r.Findings = f;
        }

        public static void Story(DestResult r, IPAddress target, List<Tuple<string, double, int>> hops, int last, string localIp, bool viaVpn, int mtu, string tcpErr, double tcpMs, double tcpFailMs, double pingAvg, int port, Dictionary<string, string> names = null, List<int> multi = null)
        {
            var seg = new List<string[]>(); var find = new List<string[]>(r.Findings ?? new List<string[]>());   // keep what earlier steps found (DNS)
            int upto = last >= 0 ? last : Math.Min(hops.Count, 15) - 1;
            while (upto >= 0 && hops[upto].Item1 == null) upto--;
            var answered = hops.Take(upto + 1).Where(h => h.Item1 != null).Select(h => h.Item1).ToList();
            var ptr = names ?? Ptrs(answered);
            Func<string, string> P = ip => { string v; return ip != null && ptr.TryGetValue(ip, out v) ? v : null; };
            Func<double, string> ms = v => v.ToString("0", CultureInfo.CurrentCulture) + " ms";

            // 1. walk the hops into segments
            seg.Add(new[] { "Your PC", localIp == "" ? "this laptop" : localIp, "you" });
            string curKey = null; int curFirst = -1, curCount = 0; double curStart = 0, curEnd = 0; string curCity = null, curCityEnd = null;
            Action flush = delegate
            {
                if (curKey == null) return;
                string kind = curKey == "Your network" ? "lan" : curKey == "Carrier-grade NAT" ? "cgnat" : curKey == "Unnamed routers" ? "hidden" : "net";
                string sub = curCount + (curCount == 1 ? " hop" : " hops") + (curEnd > curStart + 1 ? " · +" + ms(curEnd - curStart) : "") + (curCity != null ? " · " + curCity + "?" + (curCityEnd != null && curCityEnd != curCity ? " → " + curCityEnd + "?" : "") : curCityEnd != null ? " · " + curCityEnd + "?" : "");
                seg.Add(new[] { curKey, sub, kind, (curFirst + 1).ToString(CultureInfo.InvariantCulture) });
            };
            double prevMs = 0; string bigFrom = null, bigTo = null; double bigJump = 0; int privateHops = 0; bool sawCgnat = false, sawPublic = false; int silentMid = 0;
            for (int i = 0; i <= upto; i++)
            {
                var h = hops[i];
                if (h.Item1 == null) { if (i < upto) silentMid++; continue; }
                string kind = Kind(h.Item1), name = P(h.Item1), owner;
                if (kind == "private" || kind == "linklocal") { owner = sawPublic ? "Unnamed routers" : "Your network"; if (!sawPublic) privateHops++; }
                else if (kind == "cgnat") { owner = "Carrier-grade NAT"; sawCgnat = true; }
                else { sawPublic = true; owner = OwnerOf(name) ?? "Unnamed routers"; }
                if (i == last) owner = OwnerOf(name) ?? "Destination";
                if (owner != curKey)
                {
                    if (curKey != null && owner != "Unnamed routers" && curKey != "Unnamed routers" && curKey != "Your network" && curKey != "Carrier-grade NAT" && owner != "Destination")
                        find.Add(new[] { "Handoff at hop " + (i + 1) + ": " + curKey + " → " + owner + ". Two networks meet here - this is where BGP decides your route (peering or paid transit).", "-1" });
                    flush(); curKey = owner; curFirst = i; curCount = 0; curStart = prevMs; curCity = null; curCityEnd = null;
                }
                curCount++; curEnd = h.Item2; string cty = CityOf(name); if (curCity == null) curCity = cty; else if (cty != null) curCityEnd = cty;
                if (prevMs > 0 && h.Item2 - prevMs > bigJump) { bigJump = h.Item2 - prevMs; bigFrom = (i) + ""; bigTo = (i + 1) + ""; }
                prevMs = Math.Max(prevMs, h.Item2);
            }
            flush();
            if (last < 0) seg.Add(Row("Destination", "didn't answer the trace", "dest", ""));
            else seg[seg.Count - 1][2] = "dest";

            // 2. what's in the way
            bool localPrivate = Kind(localIp) == "private";
            if (viaVpn) find.Add(Row("Tunnel: your traffic is wrapped in the VPN. The routers inside the tunnel are invisible from here; the first hops you see are on the company side.", "-1"));
            if (localPrivate && privateHops >= 2 && !viaVpn) find.Add(new[] { "Double NAT: " + privateHops + " routers with private addresses before the internet - usually your provider's modem plus your own router. It works, but can upset VPNs, games and calls.", "1" });
            else if (localPrivate && !viaVpn) find.Add(new[] { "NAT: your router swaps your private address (" + localIp + ") for its public one on the way out. Normal for home and office networks.", "0" });
            if (sawCgnat) find.Add(Row("Carrier-grade NAT: your provider shares one public address between many customers (100.64.x.x hops). Outgoing traffic is fine; nobody can reach you directly.", "1"));
            if (mtu > 0 && mtu < 1500)
                find.Add(new[] { mtu == 1492 ? "Packet size 1492: a PPPoE link (typical for DSL / fibre logins) - 8 bytes of wrapping." : mtu >= 1400 ? "Packet size " + mtu + ": something on the path wraps your traffic (VPN, tunnel or LTE / satellite link)." : "Packet size " + mtu + ": several layers of tunnels on the path - big packets get split or dropped.", "1" });
            if (multi != null && multi.Count > 0)
            {
                var hs = multi.Where(h => h <= upto + 1).OrderBy(h => h).ToList();
                if (hs.Count > 0) find.Add(Row((hs.Count == 1 ? "Hop " + hs[0] + " answered" : "Hops " + string.Join(", ", hs) + " answered") + " from different routers on each try: the network spreads traffic over parallel links (ECMP load balancing). Normal for big networks - but it means each connection may take a slightly different road.", "-1"));
            }
            if (silentMid > 0) find.Add(new[] { silentMid + (silentMid == 1 ? " router" : " routers") + " on the way stayed silent. Normal - many routers don't answer traceroute, and it doesn't slow your traffic.", "-1" });
            if (bigJump >= 30 && bigFrom != null) find.Add(new[] { "Most of the delay (+" + ms(bigJump) + ") is added between hop " + bigFrom + " and " + bigTo + " - usually distance (a long fibre run or an ocean), sometimes a busy link.", bigJump >= 100 ? "1" : "-1" });

            // firewalls
            if (tcpErr == "refused" && !double.IsNaN(pingAvg) && tcpFailMs > 0 && tcpFailMs < pingAvg * 0.5)
                find.Add(new[] { "The 'no' came back in " + ms(tcpFailMs) + ", but the server is " + ms(pingAvg) + " away. Something closer than the server rejected port " + port + " - most likely a firewall or proxy on the way.", "2" });
            else if (tcpErr == "refused") find.Add(new[] { "The server itself answered 'nothing listens on port " + port + "' - not a firewall.", "1" });
            else if (tcpErr == "timeout" && last >= 0) find.Add(new[] { "The server answers ping, but port " + port + " gets silence - a firewall is quietly dropping that port.", "2" });
            else if (tcpErr == "timeout") find.Add(new[] { "Silence on port " + port + " and the trace never reached the server - it's down, or a firewall hides it completely.", "2" });
            else if (last < 0 && tcpMs > 0) find.Add(Row("The trace stops before the server, yet your port connects fine: a firewall near the server blocks traceroute but lets real traffic through.", "0"));

            // return path
            if (last >= 0 && names == null)
            {
                int ttl = ReplyTtl(target);
                if (ttl > 0)
                {
                    int init = ttl <= 64 ? 64 : ttl <= 128 ? 128 : 255, back = init - ttl + 1, fwd = last + 1;
                    string os = init == 64 ? "Linux / most servers" : init == 128 ? "Windows" : "network equipment";
                    if (Math.Abs(back - fwd) >= 3) find.Add(new[] { "The way back is different: " + fwd + " hops there, about " + back + " hops back. Internet routes are often one-way streets - normal, but it means a trace only shows half the story.", "-1" });
                    find.Add(new[] { "The reply started life with TTL " + init + " - the far end looks like " + os + ".", "-1" });
                }
            }
            r.Story = seg; r.Findings = find;
        }

        // Windows' TLS 1.3 client arrived with Windows 11 / Server 2022 (build 20348+); older builds get TLS 1.2 only.
        static bool Tls13Client()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                { int b; return k != null && int.TryParse(k.GetValue("CurrentBuildNumber") as string, out b) && b >= 20348; }
            }
            catch { return false; }
        }

        static string ProtoName(SslProtocols p)
        {
            int v = (int)p;
            if (v == 12288) return "TLS 1.3"; if (v == 3072) return "TLS 1.2"; if (v == 768) return "TLS 1.1"; if (v == 192) return "TLS 1.0";
            return p.ToString();
        }
        static void Finish(DestResult r, int level, string verdict) { r.Verdict = verdict; r.Level = level; r.Running = -1; }   // Run() marks it done

        static void Who(DestResult r, Monitor m, List<IPAddress> addrs)
        {
            try
            {
                var set = new HashSet<string>(addrs.Select(a => a.ToString()));
                var who = m.Connections(false).Where(c => set.Contains(c.RemoteIp)).GroupBy(c => c.Proc).Select(g => new[] { g.Key, g.Count() + " connection" + (g.Count() > 1 ? "s" : "") }).ToList();
                if (who.Count == 0) who.Add(Nobody);
                r.AddCard("WHO'S TALKING TO IT", who);
            }
            catch { }
        }

        static int QueryAll(string host, short type, List<IPAddress> into)
        {
            IntPtr res; int rc = Native.DnsQuery(host, type, 0x8, IntPtr.Zero, out res, IntPtr.Zero);
            if (rc != 0 || res == IntPtr.Zero) { if (res != IntPtr.Zero) Native.DnsRecordListFree(res, 1); return rc; }
            try
            {
                int P = IntPtr.Size, typeOff = 2 * P, dataOff = 2 * P + 16;
                for (IntPtr r = res; r != IntPtr.Zero; r = Marshal.ReadIntPtr(r, 0))
                {
                    int t = Marshal.ReadInt16(r, typeOff) & 0xFFFF;
                    if (t == 1) into.Add(new IPAddress((uint)Marshal.ReadInt32(r, dataOff)));
                    else if (t == 28) { var b = new byte[16]; Marshal.Copy(r + dataOff, b, 0, 16); into.Add(new IPAddress(b)); }
                }
            }
            finally { Native.DnsRecordListFree(res, 1); }
            return 0;
        }

        static bool DfPing(IPAddress ip, int payload)
        {
            try { using (var p = new Ping()) { var r = p.Send(ip, 1000, new byte[payload], new PingOptions(64, true)); return r.Status == IPStatus.Success; } }
            catch { return false; }
        }

        // hop -> (address, avg ms, loss %)
        static List<Tuple<string, double, int>> Trace(IPAddress target, int maxHops) { return Trace(target, maxHops, null); }
        public static List<Tuple<string, double, int>> TraceHops(IPAddress target, int maxHops) { return Trace(target, maxHops, null); }
        // multi: hop numbers (1-based) that answered from more than one router across the rounds = load-balanced paths (ECMP)
        static List<Tuple<string, double, int>> Trace(IPAddress target, int maxHops, List<int> multi)
        {
            var tasks = new List<Task<Tuple<string, double, int>>>();
            for (int ttl = 1; ttl <= maxHops; ttl++)
            {
                int t = ttl;
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    string addr = null; double sum = 0; int got = 0; var seen = new HashSet<string>();
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            using (var p = new Ping())
                            {
                                var sw = Stopwatch.StartNew(); var rep = p.Send(target, 1000, new byte[32], new PingOptions(t, true)); double ms = sw.Elapsed.TotalMilliseconds;
                                if (rep.Status == IPStatus.TtlExpired || rep.Status == IPStatus.Success) { addr = rep.Address.ToString(); seen.Add(addr); sum += ms; got++; }
                            }
                        }
                        catch { }
                    }
                    if (seen.Count > 1 && multi != null) lock (multi) multi.Add(t);
                    return Tuple.Create(addr, got > 0 ? sum / got : 0, (3 - got) * 100 / 3);
                }, TaskCreationOptions.LongRunning));
            }
            Task.WaitAll(tasks.ToArray(), 6000);
            return tasks.Select(x => x.Status == TaskStatus.RanToCompletion ? x.Result : Tuple.Create((string)null, 0.0, 100)).ToList();
        }

        public static string ProxyInfo()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (k == null) return "none (direct)";
                    var pac = k.GetValue("AutoConfigURL") as string; var en = k.GetValue("ProxyEnable"); var srv = k.GetValue("ProxyServer") as string;
                    if (!string.IsNullOrEmpty(pac)) return "PAC " + Draw.Trunc(pac, 30);
                    if (en is int && (int)en == 1 && !string.IsNullOrEmpty(srv)) return Draw.Trunc(srv, 30);
                    return "none (direct)";
                }
            }
            catch { return "?"; }
        }
    }
}
