// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // ================================================================== Network engineer pack
    // Certificate chain compare · 802.1X failure decoder · "Why didn't this PC join the network?" · RADIUS client lab with
    // before / after · SMTP delivery path. One-shot, nothing listens, no admin. Secrets and passwords are used for the one
    // request and wiped - never shown, kept, logged or copied. (The capture replays are in Replay.cs.)
    static class EngPack
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string Clean(string s, int max) { s = new string((s ?? "").Where(c => !char.IsControl(c) && c != '|' && c != '~' && c != ';').ToArray()).Trim(); return s.Length > max ? s.Substring(0, max) : s; }

        // ---------- 1. Certificate chain compare: the same site from two places (VPN on / off, home / office, proxy / direct)
        public sealed class CertSnap { public string Host = "", Name = "", Trust = ""; public DateTime T; public List<string[]> Chain = new List<string[]>(); }   // chain: subject, issuer, sha256, expires
        static string CertFile { get { return Path.Combine(Program.DataDir, "cert-compare.txt"); } }
        public static string CertLine(CertSnap s) { return "snap|" + s.Host + "|" + Clean(s.Name, 40) + "|" + s.T.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + "|" + Clean(s.Trust, 80) + "|" + string.Join(";", s.Chain.Select(c => string.Join("~", c.Select(x => Clean(x, 120))))); }
        public static CertSnap ParseCert(string l)
        {
            var p = (l ?? "").Split('|'); if (p.Length != 6 || p[0] != "snap") return null;
            // host, or host:port for a site on another port - checked apart
            string hh; int pp; if (!Lab.Split(p[1], 443, out hh, out pp) || !Games.ValidHost(hh)) return null;
            DateTime t; if (!DateTime.TryParseExact(p[3], "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return null;
            var s = new CertSnap { Host = p[1], Name = p[2], T = t, Trust = p[4] };
            foreach (var c in p[5].Split(';').Take(8)) { var f = c.Split('~'); if (f.Length == 4) s.Chain.Add(new[] { f[0], f[1], f[2], f[3], "" }); else if (f.Length == 5) s.Chain.Add(f); }
            return s.Chain.Count == 0 ? null : s;
        }
        public static List<string[]> CertCompare(string target, string name)
        {
            var rows = new List<string[]>(); string t = (target ?? "").Trim(); if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) { Uri u; if (Uri.TryCreate(t, UriKind.Absolute, out u)) t = u.Host + (u.IsDefaultPort ? "" : ":" + u.Port); }
            string h; int port; if (!Lab.Split(t, 443, out h, out port) || !Games.ValidHost(h)) { rows.Add(R(2, "Site", "type a site - e.g. outlook.office.com or https://intranet.example.com", "", "")); return rows; }
            var snap = new CertSnap { Host = h + (port != 443 ? ":" + port : ""), Name = string.IsNullOrWhiteSpace(name) ? "run " + DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture) : Clean(name, 40), T = DateTime.Now };
            SslPolicyErrors errs = SslPolicyErrors.None;
            try
            {
                using (var tc = Toolkit.Tcp())
                {
                    if (!tc.ConnectAsync(h, port).Wait(6000)) { rows.Add(R(2, snap.Host, "couldn't connect in 6 s", "", "m")); return rows; }
                    using (var ssl = new SslStream(tc.GetStream(), false, (o, c, ch, e) =>
                    {
                        errs = e;
                        if (ch != null) foreach (var el in ch.ChainElements) { var x = el.Certificate; snap.Chain.Add(new[] { x.GetNameInfo(X509NameType.SimpleName, false), x.GetNameInfo(X509NameType.SimpleName, true), Hex(Sha256(x.RawData)), x.NotAfter.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), snap.Chain.Count == 0 ? Sans(x) : "" }); }
                        if (snap.Chain.Count == 0 && c != null) { var x = new X509Certificate2(c); snap.Chain.Add(new[] { x.GetNameInfo(X509NameType.SimpleName, false), x.GetNameInfo(X509NameType.SimpleName, true), Hex(Sha256(x.RawData)), x.NotAfter.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Sans(x) }); }
                        return e == SslPolicyErrors.None;   // an untrusted certificate is still recorded above - then the handshake stops
                    }))
                    {
                        var hs = System.Threading.Tasks.Task.Factory.StartNew(() => ssl.AuthenticateAsClient(h)); if (!hs.Wait(8000)) throw new TimeoutException("no TLS answer in 8 s");
                    }
                }
            }
            catch (Exception e) { if (!(e is SocketException || e is IOException || e is AggregateException || e is TimeoutException || e is System.Security.Authentication.AuthenticationException)) throw; if (snap.Chain.Count == 0) { rows.Add(R(2, snap.Host, "TLS failed: " + Draw.Trunc(e.Message, 100), "", "m")); return rows; } }
            snap.Trust = errs == SslPolicyErrors.None ? "trusted by Windows" : "NOT trusted: " + errs;
            var all = new List<CertSnap>(); try { if (File.Exists(CertFile)) all = File.ReadAllLines(CertFile).Take(200).Select(ParseCert).Where(x => x != null).ToList(); } catch (IOException) { }
            var prev = all.Where(x => x.Host == snap.Host).LastOrDefault();
            all.Add(snap); all = all.Skip(Math.Max(0, all.Count - 40)).ToList();
            try { File.WriteAllLines(CertFile, all.Select(CertLine)); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            rows.Add(R(errs == SslPolicyErrors.None ? 0 : 2, snap.Name, snap.Trust, "", "m"));
            for (int i = 0; i < snap.Chain.Count; i++) rows.Add(R(3, i == 0 ? "Site certificate" : i == snap.Chain.Count - 1 ? "Root" : "Intermediate", snap.Chain[i][0] + " · from " + snap.Chain[i][1] + " · expires " + snap.Chain[i][3], "SHA-256 " + snap.Chain[i][2] + (i == 0 && snap.Chain[i].Length > 4 && snap.Chain[i][4] != "" ? " · names: " + snap.Chain[i][4] : ""), "m"));
            if (prev == null) { rows.Add(R(3, "Next", "now change one thing - VPN on / off, another network, the proxy - and run again with another name", "", "")); return rows; }
            rows.Add(R(-1, "COMPARED WITH \"" + prev.Name.ToUpperInvariant() + "\" (" + prev.T.ToString("d MMM HH:mm", CultureInfo.CurrentCulture) + ")", "", "", "m"));
            rows.Add(CertVerdict(prev, snap));
            string sa = prev.Chain[0].Length > 4 ? prev.Chain[0][4] : "", sb = snap.Chain[0].Length > 4 ? snap.Chain[0][4] : "";
            if (sa != "" && sb != "" && sa != sb) rows.Add(R(1, "Names it covers differ", "\"" + prev.Name + "\": " + sa + "  vs  \"" + snap.Name + "\": " + sb, "A certificate made for other names on one path - a different server (DNS points elsewhere there) or a proxy's own certificate.", "i"));
            if (prev.Chain.Count != snap.Chain.Count) rows.Add(R(1, "Chain length differs", prev.Chain.Count + " certificates vs " + snap.Chain.Count, "One path adds (or leaves out) an intermediate - a missing intermediate breaks some clients but not browsers that fetch it themselves.", "i"));
            return rows;
        }
        public static string[] CertVerdict(CertSnap a, CertSnap b)
        {
            if (a.Chain.Count == 0 || b.Chain.Count == 0) return R(3, "Compared", "-", "", "i");
            if (a.Chain[0][2] == b.Chain[0][2]) return R(0, "Same certificate", "both runs got exactly the same site certificate (" + a.Chain[0][1] + ")", "Nothing in the middle changes this site's encryption on either path.", "i");
            if (a.Chain[0][1] != b.Chain[0][1])
            {
                bool bUntrusted = !b.Trust.StartsWith("trusted", StringComparison.Ordinal), aUntrusted = !a.Trust.StartsWith("trusted", StringComparison.Ordinal);
                return R(2, "Different issuer", "\"" + a.Name + "\" got one from " + a.Chain[0][1] + ", \"" + b.Name + "\" from " + b.Chain[0][1],
                    "Something on one path re-signs the site: a company proxy doing TLS inspection (normal if IT said so), antivirus web scanning, or - on a network you don't control - an interception. " + (aUntrusted || bUntrusted ? "One of them isn't trusted by Windows - don't sign in on that path." : "Both are trusted by Windows, so the re-signing one has its root installed on this PC (usually by IT)."), "i");
            }
            return R(1, "Same issuer, different certificate", "the site uses more than one certificate (load balancers or a renewal in between)", "Usually harmless - big sites rotate certificates. It matters only if one of them is expired or not trusted.", "i");
        }
        static string Sans(X509Certificate2 x)
        {
            var san = x.Extensions.Cast<X509Extension>().FirstOrDefault(e => e.Oid != null && e.Oid.Value == "2.5.29.17");
            if (san == null) return "";
            var names = Regex.Matches(san.Format(false), @"(?:DNS Name|DNS|IP Address)[=:]\s*([^,\r\n]+)").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).OrderBy(n => n, StringComparer.Ordinal).Take(12);
            return Clean(string.Join(",", names), 200);
        }
        [SuppressMessage("Security", "CA5350", Justification = "SHA-256 fingerprint for display")]
        static byte[] Sha256(byte[] d) { using (var s = SHA256.Create()) return s.ComputeHash(d); }
        static string Hex(byte[] b) { return string.Join("", b.Select(x => x.ToString("X2", CultureInfo.InvariantCulture))); }

        // ---------- 2. 802.1X failure decoder: the sign-in events as a timeline, and where it stopped
        public static List<string[]> Dot1x()
        {
            var rows = new List<string[]>(); string e1, e2;
            var wl = EvLog.Recent("Microsoft-Windows-WLAN-AutoConfig/Operational", "EventID=8001 or EventID=8002 or EventID=8003 or EventID=11005 or EventID=11006 or EventID=12011 or EventID=12012 or EventID=12013", 24 * 2, 40, out e1);
            var wd = EvLog.Recent("Microsoft-Windows-Wired-AutoConfig/Operational", "EventID=15500 or EventID=15501 or EventID=15502 or EventID=15503 or EventID=15510 or EventID=15514", 24 * 2, 40, out e2);
            var all = wl.Select(e => Tuple.Create("Wi-Fi", e)).Concat(wd.Select(e => Tuple.Create("Cable", e))).OrderBy(x => x.Item2.T).ToList();
            if (all.Count == 0)
            {
                rows.Add(R(3, "Network sign-in (802.1X)", e1 != "" && e2 != "" ? "Windows wouldn't show its sign-in log here: " + e1 : "no 802.1X sign-in in the last 2 days", "Home networks with a Wi-Fi password don't use 802.1X - company networks (WPA2-Enterprise, or a cable that asks you to sign in) do.", e1 != "" ? "u" : "w"));
                return rows;
            }
            rows.Add(R(-1, "TIMELINE · LAST 2 DAYS", all.Count + " events", "", "w"));
            foreach (var x in all.Skip(Math.Max(0, all.Count - 25)))
            {
                var e = x.Item2; string kind = Kind(e); int lv = kind == "failed" ? 2 : kind == "succeeded" ? 0 : 3;
                rows.Add(R(lv, e.T.ToString("ddd HH:mm:ss", CultureInfo.CurrentCulture), x.Item1 + " · " + kind + (Reason(e.Text) != "" ? " · " + Draw.Trunc(Reason(e.Text), 110) : ""), kind == "failed" ? Hint(e.Text) : "", "w"));
            }
            var last = all.Last(); string lk = Kind(last.Item2);
            string verdict = lk == "failed" ? "The last sign-in failed: " + (Hint(last.Item2.Text) != "" ? Hint(last.Item2.Text) : Draw.Trunc(Reason(last.Item2.Text), 140))
                : lk == "succeeded" ? "The last 802.1X sign-in worked. If there's still no network, the next step is DHCP - run \"Why didn't this PC join the network?\"."
                : "The last event was the start of a sign-in with no result after it - the sign-in server (RADIUS) may not be answering the switch / access point.";
            rows.Insert(0, R(lk == "failed" ? 2 : lk == "succeeded" ? 0 : 1, "Where it stopped", verdict, "", "w"));
            string cat = lk == "succeeded" ? "dhcp" : Category(last.Item2.Text);
            rows.Insert(1, R(3, "Points to", cat == "profile" ? "profile selection - the PC picked the wrong (or no) network profile" : cat == "cert" ? "certificate validation" : cat == "reject" ? "the sign-in server refusing (policy, account or password)" : cat == "timeout" ? "the sign-in server not answering" : cat == "dhcp" ? "after sign-in: getting an address (DHCP)" : "not clear from the event text", "", "i"));
            if (cat == "cert") rows.Insert(2, Next("check the certificate: inspect the file, or compare what the server sends", "tool:certfile"));
            else if (cat == "dhcp") rows.Insert(2, Next("walk the rest of the way in: Why didn't this PC join the network?", "tool:join"));
            else if (cat == "reject" || cat == "timeout") rows.Insert(2, Next("test the RADIUS server directly with a test account", "tool:radiuslab"));
            return rows;
        }
        static string Kind(EvLog.Ev e)
        {
            string t = (e.Text ?? "").ToLowerInvariant();
            if (e.Id == 8002 || e.Id == 12013 || e.Id == 15502 || e.Id == 11006 || t.Contains("failed") || t.Contains("failure")) return "failed";
            if (e.Id == 8001 || e.Id == 12012 || e.Id == 15501 || t.Contains("succeeded") || t.Contains("successfully")) return "succeeded";
            if (e.Id == 8003) return "disconnected";
            return "started";
        }
        static string[] Next(string text, string act) { return new[] { "3", "Next", text, "", "", act }; }
        static string Category(string t)
        {
            t = (t ?? "").ToLowerInvariant();
            if (t.Contains("profile")) return "profile";
            if (t.Contains("certificate") || t.Contains("trust") || t.Contains("0x80092013") || t.Contains("0x800b0109")) return "cert";
            if (t.Contains("timeout") || t.Contains("timed out") || t.Contains("no response") || t.Contains("did not respond")) return "timeout";
            if (t.Contains("reject") || t.Contains("denied") || t.Contains("credential") || t.Contains("password")) return "reject";
            return "";
        }
        static string Reason(string t) { var m = Regex.Match(t ?? "", @"(Failure Reason|Reason Code|Reason|Error)\s*:\s*([^\r\n]+)", RegexOptions.IgnoreCase); return m.Success ? m.Groups[2].Value.Trim() : ""; }
        static string Hint(string t)
        {
            t = (t ?? "").ToLowerInvariant();
            if (t.Contains("certificate") && t.Contains("expired")) return "a certificate has expired (this PC's or the sign-in server's) - IT needs to renew it.";
            if (t.Contains("certificate") || t.Contains("trust") || t.Contains("0x80092013") || t.Contains("0x800b0109")) return "the PC doesn't trust the sign-in server's certificate, or has no valid certificate of its own - an IT setting.";
            if (t.Contains("credential") || t.Contains("password") || t.Contains("logon") || t.Contains("0x8009030c")) return "wrong or expired user name / password for the network.";
            if (t.Contains("timeout") || t.Contains("timed out") || t.Contains("no response") || t.Contains("did not respond")) return "the sign-in server (RADIUS) didn't answer - the network side, not this PC.";
            if (t.Contains("reject") || t.Contains("denied")) return "the sign-in server said no - a policy on the server (group, time of day, device) refused this PC or user.";
            if (t.Contains("profile")) return "there's no matching network profile on this PC, or it's set up for a different sign-in method.";
            return "";
        }

        // ---------- 3. Why didn't this PC join the network? link > 802.1X > DHCP > DNS > internet - and where the evidence ends
        public static List<string[]> Join(Monitor m)
        {
            var rows = new List<string[]>(); string ifId, gw, dnsSrv; lock (m.L) { ifId = m.IfaceId; gw = m.Gateway; dnsSrv = m.DnsServer; }
            NetworkInterface nic = null;
            try { nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == ifId) ?? NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && !Monitor.IsVpn(n)); }
            catch (NetworkInformationException) { }
            string stopAt = "";
            // 1. link
            bool link = nic != null && nic.OperationalStatus == OperationalStatus.Up;
            rows.Add(R(link ? 0 : 2, "1. Link", link ? (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi connected" : "cable connected") + " · " + nic.Description : "no connected adapter - cable out, Wi-Fi off, or the adapter disabled", "", "w"));
            if (!link) stopAt = "the link";
            // 2. 802.1X (only if this network uses it)
            if (stopAt == "")
            {
                string e1; var ev = EvLog.Recent("Microsoft-Windows-Wired-AutoConfig/Operational", "EventID=15501 or EventID=15502", 6, 3, out e1).Concat(EvLog.Recent("Microsoft-Windows-WLAN-AutoConfig/Operational", "EventID=12012 or EventID=12013", 6, 3, out e1)).OrderByDescending(e => e.T).FirstOrDefault();
                if (ev == null) rows.Add(R(3, "2. Network sign-in (802.1X)", "not used here (or not in the last 6 hours)", "", "w"));
                else { bool ok = Kind(ev) == "succeeded"; rows.Add(R(ok ? 0 : 2, "2. Network sign-in (802.1X)", (ok ? "succeeded" : "FAILED") + " at " + ev.T.ToString("HH:mm", CultureInfo.CurrentCulture), ok ? "" : Hint(ev.Text), "w")); if (!ok) stopAt = "the network sign-in (802.1X) - see 802.1X failure decoder"; }
            }
            // 3. DHCP
            if (stopAt == "")
            {
                var v4 = nic.GetIPProperties().UnicastAddresses.Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork).Select(u => u.Address).ToList();
                bool apipa = v4.Any(a => a.GetAddressBytes()[0] == 169 && a.GetAddressBytes()[1] == 254) && !v4.Any(a => a.GetAddressBytes()[0] != 169);
                if (v4.Count == 0) { rows.Add(R(2, "3. Address (DHCP)", "no IPv4 address", "", "w")); stopAt = "getting an address (DHCP)"; }
                else if (apipa) { rows.Add(R(2, "3. Address (DHCP)", v4[0] + " - a self-made 169.254 address: no DHCP server answered", "Wrong VLAN, the relay (ip helper) missing, the server's pool full, or 802.1X hasn't opened the port. A capture + DHCP lease replay shows which.", "w")); stopAt = "getting an address (DHCP)"; }
                else rows.Add(R(0, "3. Address (DHCP)", string.Join(", ", v4) + (gw != "" ? " · router " + gw : " · NO default gateway"), "", "w"));
                if (stopAt == "" && gw == "") stopAt = "the default gateway (DHCP gave an address but no router)";
            }
            // 4. router
            if (stopAt == "")
            {
                double g = m.GwPing.Avg(3);
                rows.Add(R(double.IsNaN(g) ? 1 : 0, "4. Router", double.IsNaN(g) ? "doesn't answer ping (some routers never do)" : Math.Round(g) + " ms", "", "m"));
            }
            // 5. DNS
            if (stopAt == "")
            {
                double d = m.Dns.Avg(3); bool fail; lock (m.L) fail = m.DnsFailedLast;
                rows.Add(R(fail ? 2 : double.IsNaN(d) ? 3 : 0, "5. Names (DNS)", fail ? "the last lookup failed" + (dnsSrv != "" ? " (" + dnsSrv + ")" : "") : double.IsNaN(d) ? "not checked yet" : Math.Round(d) + " ms" + (dnsSrv != "" ? " · " + dnsSrv : ""), "", "m"));
                if (fail) stopAt = "name lookups (DNS)";
            }
            // 6. internet
            if (stopAt == "")
            {
                double n = m.NetPing.Avg(3); bool tcp; lock (m.L) tcp = m.NetViaTcp;
                rows.Add(R(double.IsNaN(n) ? 2 : 0, "6. Internet", double.IsNaN(n) ? "no answer from the internet check" : Math.Round(n) + " ms" + (tcp ? " (TCP - ping is blocked here)" : ""), double.IsNaN(n) ? "A company network may only allow the internet through a proxy - see VPN and company proxy." : "", "m"));
                if (double.IsNaN(n)) stopAt = "the way out to the internet";
            }
            rows.Insert(0, R(stopAt == "" ? 0 : 2, "Verdict", stopAt == "" ? "This PC joined the network and reaches the internet - every step checks out." : "It stops at " + stopAt + ". The steps after it can't work until that one does.", "", "i"));
            return rows;
        }

        // ---------- 4. RADIUS client lab (PAP) with before / after
        // "testuser@server[:port]", the shared secret, the test account's password. One Access-Request, signed with a
        // Message-Authenticator; the reply is checked, decoded and saved (without any secret) to compare with the next run.
        static string AaaFile { get { return Path.Combine(Program.DataDir, "aaa-runs.txt"); } }
        [SuppressMessage("Security", "CA5351", Justification = "RADIUS (RFC 2865) defines MD5 for User-Password hiding; nothing else interoperates")]
        public static byte[] HidePassword(byte[] pw, byte[] secret, byte[] reqAuth)
        {
            int n = Math.Max(16, (pw.Length + 15) / 16 * 16); var p = new byte[n]; Array.Copy(pw, p, Math.Min(pw.Length, 128)); var c = new byte[n]; var prev = reqAuth;
            using (var md5 = MD5.Create())
                for (int i = 0; i < n; i += 16)
                {
                    var buf = new byte[secret.Length + 16]; Array.Copy(secret, buf, secret.Length); Array.Copy(prev, 0, buf, secret.Length, 16);
                    var h = md5.ComputeHash(buf); Array.Clear(buf, 0, buf.Length);
                    for (int j = 0; j < 16; j++) c[i + j] = (byte)(p[i + j] ^ h[j]);
                    prev = new byte[16]; Array.Copy(c, i, prev, 0, 16);
                }
            Array.Clear(p, 0, p.Length); return c;
        }
        public static List<string[]> DecodeAttrs(byte[] r)
        {
            var rows = new List<string[]>(); int rl = Math.Min(r.Length, r[2] << 8 | r[3]);
            for (int p = 20; p + 2 <= rl;)
            {
                int t = r[p], l = r[p + 1]; if (l < 2 || p + l > rl) break; var v = new byte[l - 2]; Array.Copy(r, p + 2, v, 0, l - 2);
                switch (t)
                {
                    case 18: rows.Add(R(3, "Reply-Message", Clean(Encoding.UTF8.GetString(v), 120), "", "m")); break;
                    case 27: if (v.Length == 4) rows.Add(R(3, "Session-Timeout", ((uint)(v[0] << 24 | v[1] << 16 | v[2] << 8 | v[3])) + " s", "", "m")); break;
                    case 81: rows.Add(R(3, "VLAN (Tunnel-Private-Group-Id)", Clean(Encoding.ASCII.GetString(v.Length > 0 && v[0] < 0x20 ? v.Skip(1).ToArray() : v), 40), "The VLAN the switch puts this user in.", "m")); break;
                    case 64: if (v.Length == 4) rows.Add(R(3, "Tunnel-Type", v[3] == 13 ? "VLAN" : v[3].ToString(CultureInfo.InvariantCulture), "", "m")); break;
                    case 11: rows.Add(R(3, "Filter-Id", Clean(Encoding.ASCII.GetString(v), 60), "An access list or policy the switch / firewall applies.", "m")); break;
                    case 8: if (v.Length == 4) rows.Add(R(3, "Framed-IP-Address", new IPAddress(v).ToString(), "", "m")); break;
                    case 25: rows.Add(R(3, "Class", v.Length + " bytes (kept by the switch for accounting)", "", "m")); break;
                    case 26: if (v.Length >= 4) rows.Add(R(3, "Vendor-Specific", "vendor " + ((uint)(v[0] << 24 | v[1] << 16 | v[2] << 8 | v[3])) + Vendor(v), "", "m")); break;
                    case 79: rows.Add(R(3, "EAP-Message", v.Length + " bytes", "", "m")); break;
                }
                p += l;
            }
            return rows;
        }
        static string Vendor(byte[] v) { uint id = (uint)(v[0] << 24 | v[1] << 16 | v[2] << 8 | v[3]); return id == 9 ? " (Cisco)" : id == 311 ? " (Microsoft)" : id == 14179 ? " (Cisco Airespace)" : id == 25506 ? " (HPE / H3C)" : id == 14823 ? " (Aruba)" : id == 2636 ? " (Juniper)" : ""; }
        [SuppressMessage("Security", "CA5351", Justification = "RADIUS Message-Authenticator (RFC 3579) is HMAC-MD5")]
        public static List<string[]> RadiusLab(string who, string secret, string password, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string w = (who ?? "").Trim(); int at = w.LastIndexOf('@');
            if (at <= 0 || at == w.Length - 1) { rows.Add(R(2, "Account and server", "type testuser@server - e.g. labtest@10.0.0.5 or labtest@radius.lab:1812", "Use a TEST account made for this, never your own sign-in.", "")); return rows; }
            string user = Clean(w.Substring(0, at), 64); string h; int port;
            if (!Lab.Split(w.Substring(at + 1), 1812, out h, out port) || !Games.ValidHost(h)) { rows.Add(R(2, "Server", "type the RADIUS server's address after the @", "", "")); return rows; }
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(password)) { rows.Add(R(2, "Secret and password", "type the shared secret and the test account's password", "Both are used for this one request and wiped - never shown, saved, logged or copied.", "")); return rows; }
            var a = Lab.Addr(h); if (a == null) { rows.Add(R(2, "Server", "couldn't find " + h, "", "m")); return rows; }
            if (Encoding.UTF8.GetByteCount(password) > 128) { rows.Add(R(2, "Password", "longer than 128 bytes - RADIUS can't carry it (RFC 2865)", "Use a shorter test password.", "")); return rows; }
            var sec = Encoding.UTF8.GetBytes(secret); var pw = Encoding.UTF8.GetBytes(password); var auth = new byte[16]; byte id;
            using (var g = RandomNumberGenerator.Create()) { g.GetBytes(auth); var idb = new byte[1]; g.GetBytes(idb); id = idb[0]; }
            byte[] reply = null; long ms = -1; string error = "", srcIp = "";
            try
            {
                var attrs = new List<byte>();
                Action<int, byte[]> add = (t, v) => { attrs.Add((byte)t); attrs.Add((byte)(v.Length + 2)); attrs.AddRange(v); };
                add(80, new byte[16]);   // Message-Authenticator first (servers hardened against BlastRADIUS require it)
                add(1, Encoding.UTF8.GetBytes(user)); var hidden = HidePassword(pw, sec, auth); add(2, hidden); Array.Clear(hidden, 0, hidden.Length);
                add(32, Encoding.ASCII.GetBytes("AdeeboMeter")); add(61, new byte[] { 0, 0, 0, 5 });   // NAS-Identifier, NAS-Port-Type Virtual
                var pkt = new byte[20 + attrs.Count]; pkt[0] = 1; pkt[1] = id; pkt[2] = (byte)(pkt.Length >> 8); pkt[3] = (byte)pkt.Length; Array.Copy(auth, 0, pkt, 4, 16); attrs.CopyTo(pkt, 20);
                using (var hm = new HMACMD5(sec)) { var mac = hm.ComputeHash(pkt); Array.Copy(mac, 0, pkt, 22, 16); }
                attrs.Clear();
                Program.Log("radius lab: Access-Request to " + a + ":" + port);
                using (var u = new UdpClient(a.AddressFamily))
                {
                    u.Client.ReceiveTimeout = 4000; u.Connect(a, port); var lep = u.Client.LocalEndPoint as IPEndPoint; if (lep != null) srcIp = lep.Address.ToString();
                    var sw = System.Diagnostics.Stopwatch.StartNew(); u.Send(pkt, pkt.Length); Array.Clear(pkt, 0, pkt.Length);
                    var from = new IPEndPoint(IPAddress.Any, 0); reply = u.Receive(ref from); ms = sw.ElapsedMilliseconds;
                }
            }
            catch (SocketException e) { error = e.SocketErrorCode == SocketError.TimedOut ? "no answer in 4 s" : e.SocketErrorCode == SocketError.ConnectionReset ? "refused - nothing listens on UDP " + port : e.Message; }
            bool hasMa = false, signed = reply != null && Protocols.ReplyOk(reply, id, auth, sec, out hasMa);
            Array.Clear(sec, 0, sec.Length); Array.Clear(pw, 0, pw.Length);
            string code; var attrRows = new List<string[]>();
            if (srcIp != "") rows.Add(R(3, "Sent from", srcIp + " (the address Windows picked for this route)", "The server must list this address as a client (NAS) with this shared secret - on a VPN or a PC with two networks it may not be the one you expect.", "w"));
            if (reply == null) { code = "Timeout"; rows.Add(R(2, h + ":" + port, error, "Silence usually means: this PC isn't a client (NAS) on the server, the shared secret is wrong (many servers drop those silently), or UDP " + port + " is blocked on the way.", "m")); }
            else if (reply.Length < 20) { code = "Junk"; rows.Add(R(2, h + ":" + port, "answered " + reply.Length + " bytes - not RADIUS", "", "m")); }
            else
            {
                code = reply[0] == 2 ? "Access-Accept" : reply[0] == 3 ? "Access-Reject" : reply[0] == 11 ? "Access-Challenge" : "code " + reply[0];
                rows.Add(R(!signed ? 2 : reply[0] == 2 ? 0 : 1, code + " in " + ms + " ms", !signed ? "but the reply isn't signed with this shared secret - the secret on the server differs" : reply[0] == 2 ? "the server accepted the test account" : reply[0] == 3 ? "the server refused the test account" : reply[0] == 11 ? "the server wants a second step (EAP or a one-time code) - PAP alone can't finish it" : "",
                    reply[0] == 3 ? "Wrong password, a locked or disabled account, or a policy that doesn't match (this NAS, the time, the group). The server's own log says which rule refused it." : "", "m"));
                if (signed) { attrRows = DecodeAttrs(reply); rows.AddRange(attrRows); }
                if (signed && !hasMa) rows.Add(R(1, "Message-Authenticator", "the reply doesn't carry one", "Its signature is only the older Response Authenticator - not hardened against BlastRADIUS (CVE-2024-3596). Turn on require_message_authenticator on the server, or update it.", "m"));
            }
            // before / after: this run is kept (without any secret) to compare with the next one
            string line = "aaa|" + h + ":" + port + "|" + DateTime.Now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + "|" + code + "|" + ms + "|" + string.Join(";", attrRows.Select(r => Clean(r[1], 40) + "=" + Clean(r[2], 60)));
            var hist = new List<string>(); try { if (File.Exists(AaaFile)) hist = File.ReadAllLines(AaaFile).Where(l => l.StartsWith("aaa|", StringComparison.Ordinal)).ToList(); } catch (IOException) { }
            var prevSame = hist.LastOrDefault(l => l.Split('|')[1] == h + ":" + port); var prevOther = hist.LastOrDefault(l => l.Split('|')[1] != h + ":" + port);
            hist.Add(line); try { File.WriteAllLines(AaaFile, hist.Skip(Math.Max(0, hist.Count - 30))); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            foreach (var prev in new[] { prevSame, prevOther }.Where(x => x != null))
            {
                var p = prev.Split('|'); if (p.Length < 6) continue;
                rows.Add(R(-1, (prev == prevSame ? "BEFORE / AFTER · " : "THE OTHER SERVER · ") + p[1], "earlier run " + When(p[2]), "", "m"));
                rows.Add(R(p[3] == code ? 0 : 1, "Answer", p[3] + " (" + p[4] + " ms)  >  " + code + " (" + ms + " ms)", p[3] != code ? "The answer changed - a policy, the account or the server changed in between." : "", "m"));
                var before = p[5] == "" ? Array.Empty<string>() : p[5].Split(';'); var after = attrRows.Select(r => Clean(r[1], 40) + "=" + Clean(r[2], 60)).ToArray();
                var gone = before.Except(after).ToList(); var added = after.Except(before).ToList();
                if (gone.Count + added.Count > 0) rows.Add(R(1, "Attributes changed", (gone.Count > 0 ? "no longer: " + string.Join(", ", gone) : "") + (gone.Count > 0 && added.Count > 0 ? " · " : "") + (added.Count > 0 ? "new: " + string.Join(", ", added) : ""), "", "m"));
                else rows.Add(R(0, "Attributes", "the same", "", "m"));
            }
            rows.Add(R(-1, "TACACS+ ON THE SAME SERVER", "", "", "m"));
            rows.AddRange(Protocols.Tacacs(h));
            return rows;
        }

        // a saved run's time, or "an earlier run" when the saved line is damaged
        static string When(string stamp) { DateTime t; return DateTime.TryParseExact(stamp ?? "", "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) ? t.ToString("d MMM HH:mm", CultureInfo.CurrentCulture) : "(time not readable)"; }
        // MX found > connected > greeting > EHLO > STARTTLS > certificate - from the rows the mail check produced
        public static string HowFar(List<string[]> r)
        {
            // read from what the mail check actually recorded (row names and levels), never from words in a server's banner
            Func<string, string[]> row = name => r.FirstOrDefault(x => x[1].IndexOf(name, StringComparison.Ordinal) >= 0);
            var fail = r.FirstOrDefault(x => x[0] == "2" && x[1].IndexOf(':') > 0 && x[1].IndexOf(' ') < 0);   // "host:port" - the step that threw
            var steps = new List<string> { "MX found" };
            var greet = row("greeting");
            if (greet == null) return string.Join(" > ", steps) + " > connection FAILED";
            steps.Add("connected"); steps.Add("greeting");
            if (row("Offers") != null) steps.Add("EHLO");
            var tls = r.FirstOrDefault(x => x[1] == "STARTTLS");
            if (fail != null && (tls == null || tls[0] != "0")) return string.Join(" > ", steps) + " > stopped (" + Draw.Trunc(fail[2], 50) + ")";
            if (tls != null && tls[0] == "0") steps.Add("STARTTLS"); else if (tls != null) return string.Join(" > ", steps) + " > no STARTTLS";
            var cert = r.FirstOrDefault(x => x[1] == "Certificate");
            if (cert != null && cert[0] == "0" && fail == null) steps.Add("certificate checked");
            else if (fail != null || (cert != null && cert[0] == "2")) return string.Join(" > ", steps) + " > certificate FAILED";
            return string.Join(" > ", steps);
        }
        // ---------- 5. SMTP delivery path: MX > connect > EHLO > STARTTLS + certificate > QUIT (never MAIL FROM)
        public static List<string[]> SmtpPath(Monitor m, string domain, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string d = (domain ?? "").Trim().TrimEnd('.'); int at = d.LastIndexOf('@'); if (at >= 0) d = d.Substring(at + 1);
            if (!Games.ValidHost(d) || d.IndexOf('.') < 0) { rows.Add(R(2, "Domain", "type a mail domain - e.g. example.com (or an address: someone@example.com)", "", "")); return rows; }
            string ds; lock (m.L) ds = m.DnsServer; IPAddress dns; if (!IPAddress.TryParse((ds ?? "").Split(',')[0].Trim(), out dns)) dns = null;
            if (dns == null) { rows.Add(R(2, "DNS", "this PC has no DNS server to ask", "", "w")); return rows; }
            run.Status = "Asking for " + d + "'s mail servers (MX)...";
            var msg = Toolkit.Query(dns, d, 15, true);
            var mx = msg == null ? new List<string[]>() : msg.Rrs.Where(r => r.Type == "MX" && r.Section == 1).Select(r => r.Data.Split(' ')).Where(x => x.Length == 2).OrderBy(x => int.Parse(x[0], CultureInfo.InvariantCulture)).ToList();
            if (mx.Count == 0) { rows.Add(R(2, "MX", msg == null ? "no answer from " + dns : "no MX records - mail to " + d + " goes to the domain itself, or nowhere", "", "m")); return rows; }
            rows.Add(R(0, "MX", string.Join(" · ", mx.Take(5).Select(x => x[1].TrimEnd('.') + " (" + x[0] + ")")), "Senders try the lowest number first.", "m"));
            foreach (var x in mx.Take(2))
            {
                if (run.Stop) break;
                string host = x[1].TrimEnd('.'); run.Status = "Talking to " + host + " on port 25 (stops before sending anything)...";
                rows.Add(R(-1, host.ToUpperInvariant() + " · PORT 25", "", "", "m"));
                var r = Protocols.Mail(host + ":25", run);
                string got = HowFar(r);
                r.Insert(0, R(got.EndsWith("certificate checked", StringComparison.Ordinal) ? 0 : 1, "Got as far as", got, "", "m"));
                if (got.EndsWith("connection FAILED", StringComparison.Ordinal)) r.Add(R(1, "Port 25 from here", "most home providers block outgoing port 25", "Normal on home lines - only mail servers talk on 25. Run this from a server, or test your own mail with port 587 in the Mail server tool.", "m"));
                rows.AddRange(r);
            }
            return rows;
        }
    }
}
