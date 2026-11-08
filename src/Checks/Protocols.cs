// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // Protocol tests engineers run every day - each one a client that asks and reads the answer. Nothing listens,
    // nothing logs in. TCP ping, HTTP timing, encrypted DNS, NTP, mail, SSH banner, SNMP, syslog, RADIUS Status-Server,
    // TACACS+, TWAMP-light, the hairpin test and IPv6 ping / trace.
    static class Protocols
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string Ms(double v) { return double.IsNaN(v) ? "-" : v.ToString(v < 10 ? "0.0" : "0", CultureInfo.CurrentCulture) + " ms"; }
        static bool Split(string target, int defPort, out string host, out int port)
        {
            host = (target ?? "").Trim(); port = defPort;
            if (host.StartsWith("[", StringComparison.Ordinal)) { int e = host.IndexOf(']'); if (e > 0) { int p; if (host.Length > e + 2 && int.TryParse(host.Substring(e + 2), out p)) port = p; host = host.Substring(1, e - 1); } }
            else { int c = host.LastIndexOf(':'); if (c > 0 && host.IndexOf(':') == c) { int p; if (int.TryParse(host.Substring(c + 1), out p)) port = p; host = host.Substring(0, c); } }
            return host != "" && port > 0 && port < 65536;
        }
        static IPAddress Addr(string host) { IPAddress a; if (IPAddress.TryParse(host, out a)) return a; try { return Dns.GetHostAddresses(host).OrderBy(x => x.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).FirstOrDefault(); } catch { return null; } }
        static int Int(string s, int def, int lo, int hi) { int v; return int.TryParse((s ?? "").Trim(), out v) ? Math.Max(lo, Math.Min(hi, v)) : def; }

        // A TLS handshake with a deadline: a server that accepts TCP and then stalls can't hold the tool forever.
        // On timeout the socket is closed first, so the stuck handshake ends too.
        public static void Tls(SslStream ssl, string host, IDisposable sock, int ms)
        {
            var t = System.Threading.Tasks.Task.Factory.StartNew(() => ssl.AuthenticateAsClient(host));
            bool done; try { done = t.Wait(ms); } catch (AggregateException e) { throw e.InnerException ?? e; }
            if (!done) { try { sock.Dispose(); } catch { } try { t.Wait(1000); } catch { } throw new TimeoutException("the TLS handshake took more than " + ms / 1000 + " s"); }
        }

        // ---------- TCP ping
        public static List<string[]> TcpPing(string target, string countIn, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string h; int p;
            if (!Split(target, 443, out h, out p)) { rows.Add(R(2, "Target", "type host:port, e.g. github.com:443", "", "u")); return rows; }
            var a = Addr(h); if (a == null) { rows.Add(R(2, "Name", "couldn't find " + h, "", "m")); return rows; }
            int n = Int(countIn, 20, 1, 500); run.SeriesUnit = "ms";
            for (int i = 0; i < n && !run.Stop; i++)
            {
                var sw = Stopwatch.StartNew(); double t = PathTools.Connect(a, p, 2000); run.Add(t); run.Status = (i + 1) + " / " + n + " connects to " + a + ":" + p;
                int wait = 1000 - (int)sw.ElapsedMilliseconds; if (wait > 0 && i < n - 1) Thread.Sleep(wait);
            }
            var s = run.Copy(); var ok = s.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList(); int lost = s.Count - ok.Count;
            rows.Add(R(lost > 0 ? (lost * 10 > s.Count ? 2 : 1) : 0, a + ":" + p, ok.Count + " of " + s.Count + " connected" + (ok.Count > 0 ? " · min " + Ms(ok[0]) + " · median " + Ms(ok[ok.Count / 2]) + " · max " + Ms(ok.Last()) : ""), "A TCP handshake each time - it works where ping (ICMP) is blocked, and shows what an app actually feels.", "m"));
            return rows;
        }

        // ---------- HTTP timing, repeated (like curl -w)
        public static List<string[]> HttpTiming(string url, string countIn, Toolkit.Run run)
        {
            var rows = new List<string[]>(); if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url.Trim();
            Uri u; if (!Uri.TryCreate(url, UriKind.Absolute, out u)) { rows.Add(R(2, "URL", "not a web address", "", "u")); return rows; }
            int n = Int(countIn, 5, 1, 50); run.SeriesUnit = "ms"; var all = new List<double[]>();
            for (int i = 0; i < n && !run.Stop; i++)
            {
                run.Status = "Request " + (i + 1) + " / " + n; var t = new double[5]; string status = "";
                try
                {
                    var sw = Stopwatch.StartNew(); var a = Dns.GetHostAddresses(u.Host).First(); t[0] = sw.Elapsed.TotalMilliseconds;
                    using (var tc = new TcpClient(a.AddressFamily))
                    {
                        if (!tc.ConnectAsync(a, u.Port).Wait(5000)) throw new TimeoutException("connect timed out");
                        t[1] = sw.Elapsed.TotalMilliseconds; Stream st = tc.GetStream(); st.ReadTimeout = 8000;
                        if (u.Scheme == "https") { var ssl = new SslStream(st, false); Tls(ssl, u.Host, tc, 8000); st = ssl; }
                        t[2] = sw.Elapsed.TotalMilliseconds;
                        var req = Encoding.ASCII.GetBytes("GET " + u.PathAndQuery + " HTTP/1.1\r\nHost: " + u.Host + "\r\nUser-Agent: AdeeboMeter/" + Program.Version + "\r\nAccept: */*\r\nConnection: close\r\n\r\n");
                        st.Write(req, 0, req.Length);
                        var buf = new byte[16384]; int r = st.Read(buf, 0, buf.Length); t[3] = sw.Elapsed.TotalMilliseconds;
                        status = r > 12 ? Encoding.ASCII.GetString(buf, 0, Math.Min(r, 40)).Split('\r')[0] : "";
                        long total = r; while (r > 0 && total < 4 << 20) { r = st.Read(buf, 0, buf.Length); total += r; }
                        t[4] = sw.Elapsed.TotalMilliseconds; st.Dispose();
                    }
                    all.Add(t); run.Add(t[4]);
                }
                catch (Exception e) { run.Add(double.NaN); rows.Add(R(2, "Request " + (i + 1), Draw.Trunc(e.Message, 90), "", "m")); }
                if (i == 0 && status != "") rows.Add(R(status.Contains(" 2") || status.Contains(" 3") ? 0 : 1, "Answer", status, "", "m"));
            }
            if (all.Count > 0)
            {
                Func<int, string> col = k => { var v = all.Select(x => k == 0 ? x[0] : x[k] - x[k - 1]).OrderBy(x => x).ToList(); return "min " + Ms(v[0]) + " · median " + Ms(v[v.Count / 2]) + " · max " + Ms(v.Last()); };
                rows.Add(R(3, "Name (DNS)", col(0), "", "m")); rows.Add(R(3, "Connect (TCP)", col(1), "", "m")); rows.Add(R(3, "Lock (TLS)", col(2), "", "m"));
                rows.Add(R(3, "First byte (server thinking)", col(3), "", "m")); rows.Add(R(3, "Download the rest", col(4), "", "m"));
                var tot = all.Select(x => x[4]).OrderBy(x => x).ToList();
                rows.Insert(0, R(tot[tot.Count / 2] > 1500 ? 1 : 0, "Total", "median " + Ms(tot[tot.Count / 2]) + " over " + all.Count + " requests", "Each step's spread tells you where it varies: name (DNS cache), connect (distance / loss), lock, first byte (the server), the rest (bandwidth).", "m"));
            }
            return rows;
        }

        // ---------- encrypted DNS: DNS-over-HTTPS and DNS-over-TLS
        public static List<string[]> EncryptedDns()
        {
            var rows = new List<string[]>();
            foreach (var doh in new[] { "https://cloudflare-dns.com/dns-query?name=example.com&type=A", "https://dns.google/resolve?name=example.com&type=A" })
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(doh); req.Accept = "application/dns-json"; req.Timeout = 6000; req.UserAgent = "AdeeboMeter"; req.Proxy = WebRequest.GetSystemWebProxy();
                    using (var resp = req.GetResponse()) using (var r = new StreamReader(resp.GetResponseStream())) { var j = r.ReadToEnd(); bool ok = j.Contains("\"Status\":0") || j.Contains("\"Status\": 0"); rows.Add(R(ok ? 0 : 1, "DNS over HTTPS · " + new Uri(doh).Host, ok ? "works - " + sw.ElapsedMilliseconds + " ms" : "answered, but not with a name", "", "m")); }
                }
                catch (Exception e) { rows.Add(R(1, "DNS over HTTPS · " + new Uri(doh).Host, "blocked or failed (" + Draw.Trunc(e.Message, 50) + ")", "Company networks often block encrypted DNS so their filters keep working.", "m")); }
            }
            var a = IPAddress.Parse("1.1.1.1"); var s2 = Stopwatch.StartNew();
            try
            {
                using (var tc = new TcpClient())
                {
                    if (!tc.ConnectAsync(a, 853).Wait(4000)) throw new TimeoutException("TCP 853 doesn't answer");
                    using (var ssl = new SslStream(tc.GetStream(), false))
                    {
                        ssl.ReadTimeout = 4000; Tls(ssl, "cloudflare-dns.com", tc, 6000);
                        var q = new List<byte> { 0xAB, 0xCD, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 7 }; q.AddRange(Encoding.ASCII.GetBytes("example")); q.Add(3); q.AddRange(Encoding.ASCII.GetBytes("com")); q.AddRange(new byte[] { 0, 0, 1, 0, 1 });
                        var msg = new[] { (byte)(q.Count >> 8), (byte)q.Count }.Concat(q).ToArray(); ssl.Write(msg, 0, msg.Length);
                        var lb = new byte[2]; int got = ssl.Read(lb, 0, 2);
                        rows.Add(R(got == 2 ? 0 : 1, "DNS over TLS · 1.1.1.1:853", got == 2 ? "works - " + s2.ElapsedMilliseconds + " ms" : "connected, no answer", "", "m"));
                    }
                }
            }
            catch (Exception e) { rows.Add(R(1, "DNS over TLS · 1.1.1.1:853", "blocked or failed (" + Draw.Trunc(e.Message, 50) + ")", "", "m")); }
            return rows;
        }

        // ---------- NTP (SNTP v4 client)
        public static List<string[]> Ntp(string server)
        {
            var rows = new List<string[]>(); string h; int p; if (!Split(string.IsNullOrWhiteSpace(server) ? "time.windows.com" : server, 123, out h, out p)) { rows.Add(R(2, "Server", "type an NTP server", "", "u")); return rows; }
            var a = Addr(h); if (a == null) { rows.Add(R(2, "Name", "couldn't find " + h, "", "m")); return rows; }
            try
            {
                using (var u = new UdpClient(a.AddressFamily))
                {
                    u.Client.ReceiveTimeout = 3000; u.Connect(a, p);   // connected: replies from anyone else are dropped by Windows
                    var q = new byte[48]; q[0] = 0x23;   // LI 0, version 4, client
                    var t1 = DateTime.UtcNow; WriteNtp(q, 40, t1); u.Send(q, 48);
                    var from = new IPEndPoint(IPAddress.Any, 0); var r = u.Receive(ref from); var t4 = DateTime.UtcNow;
                    if (r.Length < 48) throw new InvalidOperationException("short answer");
                    bool same = true; for (int i = 0; i < 8; i++) if (r[24 + i] != q[40 + i]) same = false;
                    if (!same || (r[0] & 7) != 4) throw new InvalidOperationException("the answer isn't a reply to this question");
                    if (r[1] == 0) { rows.Add(R(2, h, "refused - 'kiss of death' (" + Encoding.ASCII.GetString(r, 12, 4).TrimEnd('\0') + ")", "The server tells you to stop asking - usually rate limiting. Try another server.", "m")); return rows; }
                    if ((r[0] >> 6) == 3) { rows.Add(R(2, h, "answers, but says its own clock isn't synchronised (alarm)", "Don't trust this server's time.", "m")); return rows; }
                    int stratum = r[1]; var t2 = ReadNtp(r, 32); var t3 = ReadNtp(r, 40);
                    double offset = ((t2 - t1).TotalMilliseconds + (t3 - t4).TotalMilliseconds) / 2, delay = (t4 - t1).TotalMilliseconds - (t3 - t2).TotalMilliseconds;
                    string refid = stratum <= 1 ? Encoding.ASCII.GetString(r, 12, 4).TrimEnd('\0') : new IPAddress(new[] { r[12], r[13], r[14], r[15] }).ToString();
                    if (delay < 0 || delay > 10000 || t3 < t2) { rows.Add(R(2, h, "answered with timestamps that don't add up (delay " + Ms(delay) + ")", "This server's time can't be used.", "m")); return rows; }
                    rows.Add(R(stratum == 0 || stratum > 15 ? 2 : 0, h, "answers · stratum " + stratum + (stratum == 1 ? " (has its own clock: " + refid + ")" : " (gets time from " + refid + ")") + " · round trip " + Ms(delay), stratum == 0 ? "Stratum 0 here means 'kiss of death' - the server refuses to serve you." : "", "m"));
                    rows.Add(R(Math.Abs(offset) > 5000 ? 2 : Math.Abs(offset) > 500 ? 1 : 0, "Your clock vs this server (an estimate from one server)", (offset >= 0 ? "behind by " : "ahead by ") + Ms(Math.Abs(offset)), Math.Abs(offset) > 500 ? "Settings > Time > Sync now. More than 5 minutes breaks sign-in and certificates." : "", "m"));
                }
            }
            catch (Exception e) { rows.Add(R(2, h + ":" + p, "no answer (" + Draw.Trunc(e.Message, 50) + ")", "UDP 123 may be blocked on this network - company networks often allow only their own time servers.", "m")); }
            return rows;
        }
        static void WriteNtp(byte[] b, int o, DateTime t) { double s = (t - new DateTime(1900, 1, 1)).TotalSeconds; uint sec = (uint)s, frac = (uint)((s - sec) * 4294967296.0); b[o] = (byte)(sec >> 24); b[o + 1] = (byte)(sec >> 16); b[o + 2] = (byte)(sec >> 8); b[o + 3] = (byte)sec; b[o + 4] = (byte)(frac >> 24); b[o + 5] = (byte)(frac >> 16); b[o + 6] = (byte)(frac >> 8); b[o + 7] = (byte)frac; }
        static DateTime ReadNtp(byte[] b, int o) { uint sec = (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]); uint frac = (uint)(b[o + 4] << 24 | b[o + 5] << 16 | b[o + 6] << 8 | b[o + 7]); return new DateTime(1900, 1, 1).AddSeconds(sec + frac / 4294967296.0); }

        // ---------- mail: greeting, capabilities, STARTTLS / TLS certificate (no login)
        public static List<string[]> Mail(string target)
        {
            var rows = new List<string[]>(); string h; int p; if (!Split(target, 587, out h, out p)) { rows.Add(R(2, "Server", "type host or host:port, e.g. smtp.office365.com:587", "", "u")); return rows; }
            bool implicitTls = p == 465 || p == 993 || p == 995; string kind = p == 143 || p == 993 ? "IMAP" : p == 110 || p == 995 ? "POP" : "SMTP";
            try
            {
                using (var tc = new TcpClient())
                {
                    var sw = Stopwatch.StartNew(); if (!tc.ConnectAsync(h, p).Wait(6000)) throw new TimeoutException("no answer on port " + p);
                    Stream st = tc.GetStream(); st.ReadTimeout = 8000; X509Certificate2 cert = null;
                    if (implicitTls) { var ssl = new SslStream(st, false, (s, c, ch, e) => { if (c != null) cert = new X509Certificate2(c); return e == SslPolicyErrors.None; }); Tls(ssl, h, tc, 8000); st = ssl; }
                    var rd = new StreamReader(st, Encoding.ASCII); var wr = new StreamWriter(st, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                    string banner = rd.ReadLine() ?? "";
                    rows.Add(R(banner.StartsWith("220", StringComparison.Ordinal) || banner.StartsWith("* OK", StringComparison.Ordinal) || banner.StartsWith("+OK", StringComparison.Ordinal) ? 0 : 1, kind + " greeting (" + sw.ElapsedMilliseconds + " ms)", Draw.Trunc(banner, 140), "", "m"));
                    if (kind == "SMTP")
                    {
                        wr.WriteLine("EHLO adeebometer.local"); var caps = new List<string>(); string l; while ((l = rd.ReadLine()) != null) { caps.Add(l.Length > 4 ? l.Substring(4) : l); if (l.Length < 4 || l[3] != '-') break; }
                        rows.Add(R(3, "Offers", string.Join(" · ", caps.Skip(1).Take(12)), "", "m"));
                        bool starttls = caps.Any(c => c.StartsWith("STARTTLS", StringComparison.OrdinalIgnoreCase));
                        if (!implicitTls) rows.Add(R(starttls ? 0 : 1, "STARTTLS", starttls ? "offered" : "not offered - mail to this server would travel unencrypted", "", "m"));
                        if (starttls && !implicitTls)
                        {
                            wr.WriteLine("STARTTLS"); string ok = rd.ReadLine() ?? "";
                            if (ok.StartsWith("220", StringComparison.Ordinal)) { var ssl = new SslStream(tc.GetStream(), false, (s, c, ch, e) => { if (c != null) cert = new X509Certificate2(c); return e == SslPolicyErrors.None; }); Tls(ssl, h, tc, 8000); var w2 = new StreamWriter(ssl, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true }; w2.WriteLine("QUIT"); }
                        }
                        else wr.WriteLine("QUIT");
                    }
                    else if (kind == "IMAP") { wr.WriteLine("a1 CAPABILITY"); string l = rd.ReadLine() ?? ""; rows.Add(R(3, "Offers", Draw.Trunc(l, 160), "", "m")); wr.WriteLine("a2 LOGOUT"); }
                    else { wr.WriteLine("CAPA"); var caps = new List<string>(); string l; while ((l = rd.ReadLine()) != null && l != "." && caps.Count < 20) caps.Add(l); rows.Add(R(3, "Offers", string.Join(" · ", caps.Skip(1)), "", "m")); wr.WriteLine("QUIT"); }
                    if (cert != null) using (cert) { double days = (cert.NotAfter - DateTime.Now).TotalDays; rows.Add(R(days < 0 ? 2 : days < 14 ? 1 : 0, "Certificate", cert.GetNameInfo(X509NameType.SimpleName, false) + " · expires " + cert.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture), "", "m")); }
                }
            }
            catch (Exception e) { rows.Add(R(2, h + ":" + p, Draw.Trunc(e.Message, 100), p == 25 ? "Home providers usually block outgoing port 25 - use 587." : "", "m")); }
            return rows;
        }

        // ---------- SSH banner (no login)
        public static List<string[]> Ssh(string target)
        {
            var rows = new List<string[]>(); string h; int p; if (!Split(target, 22, out h, out p)) { rows.Add(R(2, "Server", "type host or host:port", "", "u")); return rows; }
            try
            {
                using (var tc = new TcpClient())
                {
                    var sw = Stopwatch.StartNew(); if (!tc.ConnectAsync(h, p).Wait(5000)) throw new TimeoutException("no answer on port " + p);
                    var st = tc.GetStream(); st.ReadTimeout = 5000; var buf = new byte[256]; int n = st.Read(buf, 0, buf.Length);
                    string b = Encoding.ASCII.GetString(buf, 0, Math.Max(0, n)).Split('\r', '\n')[0];
                    rows.Add(R(b.StartsWith("SSH-", StringComparison.Ordinal) ? 0 : 1, h + ":" + p + " (" + sw.ElapsedMilliseconds + " ms)", b.StartsWith("SSH-", StringComparison.Ordinal) ? b : "connected, but it isn't SSH: " + Draw.Trunc(b, 80), b.StartsWith("SSH-1", StringComparison.Ordinal) ? "SSH version 1 is broken and should be off." : "", "m"));
                }
            }
            catch (Exception e) { rows.Add(R(2, h + ":" + p, Draw.Trunc(e.Message, 90), "", "m")); }
            return rows;
        }

        // ---------- SNMP v2c GET (system group)
        static readonly string[][] SysOids = { new[] { "1.3.6.1.2.1.1.5.0", "Name" }, new[] { "1.3.6.1.2.1.1.1.0", "Description" }, new[] { "1.3.6.1.2.1.1.3.0", "Up for" }, new[] { "1.3.6.1.2.1.1.6.0", "Location" }, new[] { "1.3.6.1.2.1.1.4.0", "Contact" } };
        public static List<string[]> Snmp(string target, string community)
        {
            var rows = new List<string[]>(); string h; int p; if (!Split(target, 161, out h, out p)) { rows.Add(R(2, "Device", "type the device's address", "", "u")); return rows; }
            var a = Addr(h); if (a == null) { rows.Add(R(2, "Name", "couldn't find " + h, "", "m")); return rows; }
            string comm = string.IsNullOrWhiteSpace(community) ? "public" : community.Trim();
            var pdu = new List<byte>(); foreach (var o in SysOids) pdu.AddRange(Tlv(0x30, Oid(o[0]).Concat(new byte[] { 5, 0 }).ToArray()));
            var reqId = new byte[] { 2, 2, 0x12, 0x34 };
            var getReq = Tlv(0xA0, reqId.Concat(new byte[] { 2, 1, 0, 2, 1, 0 }).Concat(Tlv(0x30, pdu.ToArray())).ToArray());
            var msg = Tlv(0x30, new byte[] { 2, 1, 1 }.Concat(Tlv(4, Encoding.ASCII.GetBytes(comm))).Concat(getReq).ToArray());
            try
            {
                using (var u = new UdpClient(a.AddressFamily))
                {
                    u.Client.ReceiveTimeout = 3000; u.Connect(a, p); var sw = Stopwatch.StartNew(); u.Send(msg, msg.Length);
                    var from = new IPEndPoint(IPAddress.Any, 0); var r = u.Receive(ref from);
                    string why = SnmpCheck(r, reqId[2], reqId[3], Encoding.ASCII.GetBytes(comm));
                    if (why != "") { rows.Add(R(2, h, "answered, but " + why, "", "m")); return rows; }
                    var vals = new List<string>(); ParseVarbinds(r, vals);
                    rows.Add(R(0, h + " (" + sw.ElapsedMilliseconds + " ms)", "answered SNMP v2c", "The community string travels in clear text - SNMP v3 is the safe choice on shared networks.", "m"));
                    for (int i = 0; i < SysOids.Length && i < vals.Count; i++) rows.Add(R(3, SysOids[i][1], vals[i], "", "m"));
                }
            }
            catch (SocketException) { rows.Add(R(2, h + ":" + p, "no answer in 3 s", "Wrong community string, SNMP off, an access list that doesn't include your PC, or UDP 161 blocked - a device stays silent in all four cases.", "m")); }
            return rows;
        }
        // A real answer to our GET: SEQUENCE { version 1, community, GetResponse { request-id, error-status 0, ... } }
        static string SnmpCheck(byte[] r, byte id1, byte id2, byte[] community)
        {
            int t, hl, len, p = 0;
            if (!TryTlv(r, 0, r.Length, out t, out hl, out len) || t != 0x30) return "not with an SNMP message";
            p = hl; int end = hl + len;
            if (!TryTlv(r, p, end, out t, out hl, out len) || t != 2 || len != 1 || r[p + hl] != 1) return "not with SNMP v2c";
            p += hl + len; if (!TryTlv(r, p, end, out t, out hl, out len) || t != 4) return "not with an SNMP message";
            if (community != null && (len != community.Length || !r.Skip(p + hl).Take(len).SequenceEqual(community))) return "with a different community";
            p += hl + len; if (!TryTlv(r, p, end, out t, out hl, out len) || t != 0xA2) return "not with a reply to a GET (PDU " + t.ToString("X2", CultureInfo.InvariantCulture) + ")";
            p += hl; int pend = p + len;
            if (!TryTlv(r, p, pend, out t, out hl, out len) || t != 2 || len != 2 || r[p + hl] != id1 || r[p + hl + 1] != id2) return "to a different request";
            p += hl + len; if (!TryTlv(r, p, pend, out t, out hl, out len) || t != 2) return "with a broken reply";
            int err = len == 1 ? r[p + hl] : -1; if (err != 0) return "with SNMP error " + err + (err == 2 ? " (noSuchName)" : err == 5 ? " (genErr)" : "");
            return "";
        }
        static byte[] Tlv(byte tag, byte[] v) { var l = v.Length < 128 ? new[] { (byte)v.Length } : v.Length < 256 ? new byte[] { 0x81, (byte)v.Length } : new byte[] { 0x82, (byte)(v.Length >> 8), (byte)v.Length }; return new[] { tag }.Concat(l).Concat(v).ToArray(); }
        static byte[] Oid(string s)
        {
            var p = s.Split('.').Select(x => long.Parse(x, CultureInfo.InvariantCulture)).ToArray(); var b = new List<byte> { (byte)(p[0] * 40 + p[1]) };
            for (int i = 2; i < p.Length; i++) { var st = new List<byte>(); long v = p[i]; st.Add((byte)(v & 0x7F)); v >>= 7; while (v > 0) { st.Add((byte)(0x80 | (v & 0x7F))); v >>= 7; } st.Reverse(); b.AddRange(st); }
            return Tlv(6, b.ToArray());
        }
        // Walks the BER tree of an SNMP reply and collects the value of every varbind (SEQUENCE { OID, value }).
        // The reply comes from the network, so every length is checked before it is used: lengths are at most 3 bytes
        // long, never negative, never past the end of the element that holds them, and the walk always moves forward.
        public static void ParseVarbinds(byte[] r, List<string> vals)
        {
            int depth = 0;
            Action<int, int> walk = null;
            walk = (pos, end) =>
            {
                if (++depth > 16) return;
                while (pos < end && vals.Count < 64)
                {
                    int tag, hl, len; if (!TryTlv(r, pos, end, out tag, out hl, out len)) return;
                    int v = pos + hl, vend = v + len;
                    if (tag == 0x30 && len >= 2 && r[v] == 6)
                    {
                        int ot, ohl, ol; if (!TryTlv(r, v, vend, out ot, out ohl, out ol)) return;
                        int vp = v + ohl + ol; int vt, vhl, vl;
                        if (TryTlv(r, vp, vend, out vt, out vhl, out vl)) vals.Add(Val(r, (byte)vt, vp + vhl, vl));
                    }
                    else if (tag == 0x30 || (tag >= 0xA0 && tag <= 0xA8)) walk(v, vend);
                    pos = vend;   // len >= 0, hl >= 2: always forward
                }
                depth--;
            };
            walk(0, r.Length);
        }
        static bool TryTlv(byte[] r, int pos, int end, out int tag, out int hl, out int len)
        {
            tag = 0; hl = 0; len = 0;
            if (pos < 0 || pos + 2 > end || end > r.Length) return false;
            tag = r[pos]; len = r[pos + 1]; hl = 2;
            if ((len & 0x80) != 0)
            {
                int nb = len & 0x7F; if (nb == 0 || nb > 3 || pos + 2 + nb > end) return false;   // no indefinite or huge lengths
                len = 0; for (int i = 0; i < nb; i++) len = len << 8 | r[pos + 2 + i]; hl = 2 + nb;
            }
            return len >= 0 && pos + hl + len <= end;
        }
        static string Val(byte[] r, byte t, int o, int l)
        {
            if (o < 0 || l < 0 || o + l > r.Length) return "(malformed)";
            if ((t == 0x43 || t == 2 || t == 0x41 || t == 0x42) && l > 8) return "(number too long)";
            if (t == 4) return Draw.Trunc(Regex.Replace(Encoding.UTF8.GetString(r, o, l), @"\s+", " "), 160);
            if (t == 0x43 || t == 2 || t == 0x41 || t == 0x42) { long v = 0; for (int i = 0; i < l; i++) v = v << 8 | r[o + i]; if (t == 0x43) { if (v < 0 || v > uint.MaxValue) return "(out of range)"; var ts = TimeSpan.FromMilliseconds(v * 10); return ts.Days + " days " + ts.Hours + " h " + ts.Minutes + " min"; } return v.ToString(CultureInfo.InvariantCulture); }
            if (t == 0x80 || t == 0x81) return "(not available on this device)";
            return "(type 0x" + t.ToString("X2", CultureInfo.InvariantCulture) + ")";
        }

        // ---------- syslog: one test message
        public static List<string[]> Syslog(string target, string text)
        {
            var rows = new List<string[]>(); string h; int p; if (!Split(target, 514, out h, out p)) { rows.Add(R(2, "Collector", "type the syslog server's address", "", "u")); return rows; }
            var a = Addr(h); if (a == null) { rows.Add(R(2, "Name", "couldn't find " + h, "", "m")); return rows; }
            string msg = "<14>1 " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) + " " + Environment.MachineName + " AdeeboMeter - - - " + (string.IsNullOrWhiteSpace(text) ? "AdeeboMeter test message" : Regex.Replace(text.Trim(), @"[\r\n\t\x00-\x1f]", " ").Substring(0, Math.Min(400, text.Trim().Length)));
            using (var u = new UdpClient(a.AddressFamily)) { var b = Encoding.UTF8.GetBytes(msg); u.Send(b, b.Length, new IPEndPoint(a, p)); }
            rows.Add(R(0, "Sent to " + a + ":" + p + " (UDP)", msg, "Syslog over UDP never confirms - look for it on the collector (facility user, severity info).", "m"));
            return rows;
        }

        // ---------- RADIUS Status-Server (RFC 5997) - checks the server is alive and the shared secret matches; no user credentials
        [SuppressMessage("Security", "CA5351", Justification = "RADIUS (RFC 2865 / 3579) defines MD5 and HMAC-MD5; nothing else interoperates")]
        public static List<string[]> Radius(string target, string secret)
        {
            var rows = new List<string[]>(); string h; int p; if (!Split(target, 1812, out h, out p)) { rows.Add(R(2, "Server", "type the RADIUS server's address", "", "u")); return rows; }
            if (string.IsNullOrEmpty(secret)) { rows.Add(R(2, "Shared secret", "type the shared secret (it is used to sign the request, never sent)", "", "u")); return rows; }
            var a = Addr(h); if (a == null) { rows.Add(R(2, "Name", "couldn't find " + h, "", "m")); return rows; }
            var sec = Encoding.UTF8.GetBytes(secret); var auth = new byte[16]; using (var g = RandomNumberGenerator.Create()) g.GetBytes(auth);
            var pkt = new byte[20 + 18]; pkt[0] = 12; pkt[1] = (byte)(auth[0] ^ 0x5A); pkt[2] = 0; pkt[3] = (byte)pkt.Length; Array.Copy(auth, 0, pkt, 4, 16);
            pkt[20] = 80; pkt[21] = 18;   // Message-Authenticator, HMAC-MD5 over the packet with the field zeroed
            using (var hm = new HMACMD5(sec)) { var mac = hm.ComputeHash(pkt); Array.Copy(mac, 0, pkt, 22, 16); }
            try
            {
                using (var u = new UdpClient(a.AddressFamily))
                {
                    u.Client.ReceiveTimeout = 3000; u.Connect(a, p); var sw = Stopwatch.StartNew(); u.Send(pkt, pkt.Length);
                    var from = new IPEndPoint(IPAddress.Any, 0); var r = u.Receive(ref from);
                    if (r.Length < 20) { rows.Add(R(2, h + ":" + p, "answered with " + r.Length + " bytes - not a RADIUS reply", "", "m")); return rows; }
                    bool good = ReplyOk(r, pkt[1], auth, sec); Array.Clear(sec, 0, sec.Length);
                    string code = r[0] == 2 ? "Access-Accept" : r[0] == 3 ? "Access-Reject" : r[0] == 5 ? "Accounting-Response" : "code " + r[0];
                    bool expected = r[0] == 2 || r[0] == 5;   // RFC 5997: Access-Accept on the authentication port, Accounting-Response on accounting
                    rows.Add(R(good ? (expected ? 0 : 1) : 1, h + ":" + p + " (" + sw.ElapsedMilliseconds + " ms)", code + (good ? (expected ? " - the server is alive and the shared secret matches" : " - signed with this secret (so it matches), but not the answer Status-Server expects") : " - but the reply isn't signed with this secret"),
                        !good ? "The shared secret differs between this PC's entry and the server." : expected ? "" : "The server may handle Status-Server as a normal request - check that status_server is enabled (FreeRADIUS) and that this PC is a client.", "m"));
                }
            }
            catch (SocketException) { rows.Add(R(2, h + ":" + p, "no answer in 3 s", "RADIUS servers stay silent for: a wrong shared secret, a client (NAS) entry that doesn't include this PC's address, Status-Server not enabled, or UDP " + p + " blocked. FreeRADIUS: status_server = yes and a client entry for this PC.", "m")); }
            return rows;
        }

        // Response Authenticator (RFC 2865 section 3): MD5(Code + Identifier + Length + Request Authenticator + Attributes + Secret),
        // over the length the reply declares. Checked against the RFC's own example in the self-test.
        [SuppressMessage("Security", "CA5351", Justification = "RADIUS (RFC 2865) defines MD5; nothing else interoperates")]
        public static bool ReplyOk(byte[] r, byte id, byte[] reqAuth, byte[] secret)
        {
            if (r == null || r.Length < 20 || reqAuth == null || reqAuth.Length != 16 || secret == null) return false;
            int rl = r[2] << 8 | r[3]; if (rl < 20 || rl > r.Length || r[1] != id) return false;
            var chk = new byte[rl + secret.Length]; Array.Copy(r, chk, rl); Array.Copy(reqAuth, 0, chk, 4, 16); Array.Copy(secret, 0, chk, rl, secret.Length);
            using (var md5 = MD5.Create()) { var exp = md5.ComputeHash(chk); for (int i = 0; i < 16; i++) if (exp[i] != r[4 + i]) return false; }
            return true;
        }

        // ---------- TACACS+ reachability (TCP 49)
        public static List<string[]> Tacacs(string target)
        {
            var rows = new List<string[]>(); string h; int p; if (!Split(target, 49, out h, out p)) { rows.Add(R(2, "Server", "type the TACACS+ server's address", "", "u")); return rows; }
            var a = Addr(h); if (a == null) { rows.Add(R(2, "Name", "couldn't find " + h, "", "m")); return rows; }
            var times = Enumerable.Range(0, 3).Select(i => PathTools.Connect(a, p, 3000)).ToList(); var ok = times.Where(x => !double.IsNaN(x)).ToList();
            rows.Add(R(ok.Count == 3 ? 0 : ok.Count > 0 ? 1 : 2, a + ":" + p, ok.Count > 0 ? "accepts TCP - " + Ms(ok.Min()) + " (" + ok.Count + " of 3)" : "no TCP answer", ok.Count > 0 ? "The daemon is listening. Whether it accepts a device also depends on the key and its client list - check its log for this address." : "Down, a firewall, or tac_plus not running.", "m"));
            return rows;
        }

        // ---------- TWAMP-light sender (RFC 5357, unauthenticated) to a reflector you choose
        public static List<string[]> Twamp(string target, string countIn, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string h; int p; if (!Split(target, 862, out h, out p)) { rows.Add(R(2, "Reflector", "type the reflector's address (port 862, or host:port)", "On SR OS: a TWAMP-light reflector on the router; on Linux: any TWAMP-light reflector.", "u")); return rows; }
            var a = Addr(h); if (a == null) { rows.Add(R(2, "Name", "couldn't find " + h, "", "m")); return rows; }
            int n = Int(countIn, 100, 10, 2000); run.SeriesUnit = "ms";
            var res = new List<double[]>();   // sseq, t1, t2, t3, t4, rseq (ms since 1900)
            using (var u = new UdpClient(a.AddressFamily))
            {
                u.Client.ReceiveTimeout = 1000; u.Connect(a, p);
                for (int i = 0; i < n && !run.Stop; i++)
                {
                    var pkt = new byte[41]; pkt[0] = (byte)(i >> 24); pkt[1] = (byte)(i >> 16); pkt[2] = (byte)(i >> 8); pkt[3] = (byte)i;
                    var t1 = DateTime.UtcNow; WriteNtp(pkt, 4, t1); pkt[12] = 0; pkt[13] = 1;   // error estimate (RFC 4656): S bit 0 = this PC can't promise its clock is synced to UTC; scale 0, multiplier 1 (a multiplier of 0 isn't allowed)
                    u.Send(pkt, pkt.Length); var sw = Stopwatch.StartNew();
                    try
                    {
                        while (sw.ElapsedMilliseconds < 1000)
                        {
                            var from = new IPEndPoint(IPAddress.Any, 0); var r = u.Receive(ref from); var t4 = DateTime.UtcNow;
                            if (r.Length < 40) continue;
                            int sseq = r[24] << 24 | r[25] << 16 | r[26] << 8 | r[27]; if (sseq != i) continue;
                            bool echoed = true; for (int k = 0; k < 8 && 28 + k < r.Length; k++) if (r[28 + k] != pkt[4 + k]) echoed = false;
                            if (!echoed) continue;   // a late duplicate of an earlier packet
                            int rseq = r[0] << 24 | r[1] << 16 | r[2] << 8 | r[3];
                            var t3 = ReadNtp(r, 4); var t2 = ReadNtp(r, 16);
                            res.Add(new double[] { i, 0, (t2 - t1).TotalMilliseconds, (t4 - t3).TotalMilliseconds, ((t4 - t1) - (t3 - t2)).TotalMilliseconds, rseq });
                            run.Add(((t4 - t1) - (t3 - t2)).TotalMilliseconds); break;
                        }
                    }
                    catch (SocketException) { run.Add(double.NaN); }
                    if (res.Count == 0 || res.Last()[0] != i) { if (run.Copy().Count <= i) run.Add(double.NaN); }
                    run.Status = (i + 1) + " / " + n + " packets to " + a + ":" + p;
                    int wait = 100 - (int)sw.ElapsedMilliseconds; if (wait > 0) Thread.Sleep(wait);
                }
            }
            if (res.Count == 0) { rows.Add(R(2, a + ":" + p, "no reflected packets", "Is a TWAMP-light reflector running there on UDP " + p + ", and does its prefix list include this PC's address?", "m")); return rows; }
            // loss per direction: the reflector numbers what it reflected, so a gap there means lost on the way out
            int sent = n, got = res.Count, lostOut = 0, lostBack = 0;
            for (int i = 1; i < res.Count; i++) { int ds = (int)(res[i][0] - res[i - 1][0]), dr = (int)(res[i][5] - res[i - 1][5]); if (ds > 1) { int miss = ds - 1, reflected = Math.Max(0, dr - 1); lostBack += Math.Min(miss, reflected); lostOut += miss - Math.Min(miss, reflected); } }
            var rtt = res.Select(x => x[4]).OrderBy(x => x).ToList();
            Func<int, double> ipdv = k => { double s = 0; for (int i = 1; i < res.Count; i++) s += Math.Abs(res[i][k] - res[i - 1][k]); return res.Count > 1 ? s / (res.Count - 1) : 0; };
            int unknown = sent - got - lostOut - lostBack;
            rows.Add(R(got < sent ? 1 : 0, "Reflected", got + " of " + sent + " · lost on the way out " + lostOut + " · on the way back " + lostBack + (unknown > 0 ? " · " + unknown + " before the first or after the last reply (direction unknown)" : ""), "The reflector numbers what it sends back, so AdeeboMeter can tell which direction lost each packet.", "m"));
            rows.Add(R(0, "Round trip (reflector time removed)", "min " + Ms(rtt[0]) + " · median " + Ms(rtt[rtt.Count / 2]) + " · max " + Ms(rtt.Last()), "", "m"));
            rows.Add(R(ipdv(2) > 10 || ipdv(3) > 10 ? 1 : 0, "Jitter each way", "out " + Ms(ipdv(2)) + " · back " + Ms(ipdv(3)), "Jitter per direction needs no synced clocks - the clock offset cancels out.", "m"));
            double fo = res.Select(x => x[2]).OrderBy(x => x).ElementAt(res.Count / 2), bo = res.Select(x => x[3]).OrderBy(x => x).ElementAt(res.Count / 2);
            rows.Add(R(3, "One-way delay", "out " + Ms(fo) + " · back " + Ms(bo), "Only true if this PC's and the reflector's clocks are synced (PTP / NTP); otherwise the two numbers include the clock difference and only their sum (the round trip) is exact.", "i"));
            return rows;
        }

        // ---------- your own router: the public address and NAT loopback (hairpin), through STUN
        public static List<string[]> Hairpin()
        {
            var rows = new List<string[]>(); IPAddress relay;
            try { relay = Dns.GetHostAddresses("worldaz.relay.teams.microsoft.com").First(x => x.AddressFamily == AddressFamily.InterNetwork); }
            catch (Exception e) { rows.Add(R(2, "STUN server", "couldn't find it: " + e.Message, "", "m")); return rows; }
            using (var a = new UdpClient(0, AddressFamily.InterNetwork)) using (var b = new UdpClient(0, AddressFamily.InterNetwork))
            {
                a.Client.ReceiveTimeout = 1500; b.Client.ReceiveTimeout = 1500;
                var ma = Stun(a, relay); var mb = Stun(b, relay);
                if (ma == null || mb == null) { rows.Add(R(2, "Your public address", "no STUN answer (UDP 3478 blocked?)", "", "m")); return rows; }
                rows.Add(R(3, "Your public address", ma.Address.ToString(), Watch.IsPublic(ma.Address.ToString()) ? "" : "That's not a public address - your provider adds its own NAT (CGNAT).", "m"));
                double rt = double.NaN; var r = Icmp.Echo(ma.Address, 32, 64, false, 1500, false); if (r.Status == Icmp.Ok) rt = r.Rtt;
                rows.Add(R(3, "Ping your own public address", double.IsNaN(rt) ? "no answer (many routers ignore ping on their outside)" : Ms(rt) + " - answered by your router itself", "", "m"));
                var probe = Encoding.ASCII.GetBytes("adeebometer-hairpin"); bool back = false;
                try { a.Send(probe, probe.Length, mb); var from = new IPEndPoint(IPAddress.Any, 0); var got = b.Receive(ref from); back = got.SequenceEqual(probe); } catch (SocketException) { }
                rows.Add(R(back ? 0 : 1, "Hairpin (NAT loopback)", back ? "works - a packet sent to your own public address came back in" : "doesn't loop back", back ? "Devices at home can reach each other (a camera, a NAS, a game server) using the public address or a dynamic-DNS name." : "Using your public address or dynamic-DNS name from inside your home won't work - use the local address at home, or turn on 'NAT loopback' / 'NAT reflection' in the router.", "m"));
                int inner = ((IPEndPoint)a.Client.LocalEndPoint).Port;
                rows.Add(R(3, "Port kept?", inner + " inside → " + ma.Port + " outside" + (inner == ma.Port ? " (kept)" : " (changed by the router)"), "", "m"));
            }
            return rows;
        }
        static IPEndPoint Stun(UdpClient u, IPAddress relay)
        {
            var tid = new byte[12]; using (var g = RandomNumberGenerator.Create()) g.GetBytes(tid);
            var req = new byte[20]; req[1] = 1; req[4] = 0x21; req[5] = 0x12; req[6] = 0xA4; req[7] = 0x42; Array.Copy(tid, 0, req, 8, 12);
            for (int t = 0; t < 3; t++)
                try
                {
                    u.Send(req, req.Length, new IPEndPoint(relay, 3478)); var from = new IPEndPoint(IPAddress.Any, 0); var m = u.Receive(ref from);
                    if (!from.Address.Equals(relay) || m.Length < 20 || m[0] != 1 || m[1] != 1) continue;   // a Binding Success from the server we asked
                    bool mine = m[4] == 0x21 && m[5] == 0x12 && m[6] == 0xA4 && m[7] == 0x42; for (int k = 0; k < 12; k++) if (m[8 + k] != tid[k]) mine = false;
                    if (!mine || 20 + (m[2] << 8 | m[3]) > m.Length) continue;
                    for (int p = 20; p + 4 <= m.Length;)
                    {
                        int type = m[p] << 8 | m[p + 1], len = m[p + 2] << 8 | m[p + 3], v = p + 4;
                        if (v + len > m.Length) break;
                        if ((type == 0x20 || type == 1) && len >= 8 && m[v + 1] == 1) { int port = m[v + 2] << 8 | m[v + 3]; var ip = new[] { m[v + 4], m[v + 5], m[v + 6], m[v + 7] }; if (type == 0x20) { port ^= 0x2112; ip[0] ^= 0x21; ip[1] ^= 0x12; ip[2] ^= 0xA4; ip[3] ^= 0x42; } return new IPEndPoint(new IPAddress(ip), port); }
                        p = v + ((len + 3) & ~3);
                    }
                }
                catch (SocketException) { }
            return null;
        }
    }
}
