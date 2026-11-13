// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // Quick tidy (hand-off only), before/after snapshots, DNS compare, many ports, every IP behind a name, My sites,
    // two-PC compare, RDP / LDAP reachability, SMB shares, services that failed to start.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "result rows and small lists, built once per run")]
    static class MoreTools
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string[] Rf(int lv, string name, string val, string adv, string fix) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, "w", "fix:" + fix }; }
        static string I(long n) { return n.ToString(CultureInfo.InvariantCulture); }
        static string Mb(long b) { return b >= 1L << 30 ? (b / 1073741824.0).ToString("0.0", CultureInfo.CurrentCulture) + " GB" : (b / 1048576.0).ToString("0", CultureInfo.CurrentCulture) + " MB"; }

        // ================================================================== Quick tidy: finds and explains; you act in the Windows screen it opens
        [StructLayout(LayoutKind.Sequential)] struct RbInfo { public int Size; public long Bytes; public long Items; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHQueryRecycleBin(string root, ref RbInfo info);
        public static List<string[]> Tidy(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>();
            rows.Add(R(3, "How this works", "AdeeboMeter only looks - it deletes, ends and changes nothing", "Each line opens the Windows screen where you decide. Not here on purpose: memory \"boosters\", registry cleaners, cache wipers and service \"optimisers\" - they don't make Windows faster and can break it.", "w"));
            // temp files older than a week
            run.Status = "Looking at your temp folder..."; long tb = 0; int tn = 0; string temp = Path.GetTempPath(); var cut = DateTime.Now.AddDays(-7);
            try { foreach (var f in new DirectoryInfo(temp).EnumerateFiles("*", SearchOption.AllDirectories).Take(200000)) { if (run.Stop) break; try { if (f.LastWriteTime < cut) { tb += f.Length; tn++; } } catch (IOException) { } } }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            rows.Add(Rf(tb > 1L << 30 ? 1 : 3, "Your temp files over a week old", tn + " files · " + Mb(tb), tb > 0 ? "Safe to delete. Storage settings › Temporary files does it for you (no admin)." : "", "ms-settings:storagepolicies"));
            // recycle bin
            var rb = new RbInfo { Size = Marshal.SizeOf(typeof(RbInfo)) }; long rbb = -1;
            try { if (SHQueryRecycleBin(null, ref rb) == 0) rbb = rb.Bytes; } catch (EntryPointNotFoundException) { } catch (DllNotFoundException) { }
            if (rbb >= 0) rows.Add(Rf(rbb > 2L << 30 ? 1 : 3, "Recycle Bin", I(rb.Items) + " items · " + Mb(rbb), rbb > 0 ? "Emptying it frees the space - check nothing in it is still needed." : "", "shell:RecycleBinFolder"));
            // disk space
            try
            {
                var d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)); double free = d.TotalFreeSpace * 100.0 / d.TotalSize;
                rows.Add(Rf(free < 5 ? 2 : free < 10 ? 1 : 0, "Space on " + d.Name.TrimEnd('\\'), Mb(d.TotalFreeSpace) + " free (" + free.ToString("0", CultureInfo.CurrentCulture) + "%)", free < 10 ? "Under 10% free slows Windows and blocks updates. Storage settings shows what's big; Toolbox › Where did my space go? lists the folders." : "", "ms-settings:storagesense"));
            }
            catch (IOException) { } catch (ArgumentException) { }
            // startup programs
            int su = 0;
            foreach (var h in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                try { using (var b = RegistryKey.OpenBaseKey(h, RegistryView.Registry64)) using (var k = b.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) if (k != null) su += k.ValueCount; } catch (Exception rex) { if (!(rex is System.Security.SecurityException || rex is IOException || rex is UnauthorizedAccessException)) throw; }   /* one unreadable key must not stop the rest */
            try { su += Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Startup)).Count(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            rows.Add(Rf(su > 15 ? 1 : 3, "Programs that start with Windows", su + " start by themselves", su > 15 ? "Each one slows start-up a little. Turn off the ones you don't need at once (Startup apps shows their impact)." : "", "ms-settings:startupapps"));
            // frozen or memory-hungry programs
            run.Status = "Looking at running programs...";
            var frozen = new List<string>(); string hog = ""; long hogB = 0;
            foreach (var p in Process.GetProcesses())
                using (p)
                    try
                    {
                        if (p.MainWindowHandle != IntPtr.Zero && !p.Responding) frozen.Add(p.ProcessName);
                        long ws = p.WorkingSet64; if (ws > hogB && p.Id != Process.GetCurrentProcess().Id) { hogB = ws; hog = p.ProcessName; }
                    }
                    catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } catch (NotSupportedException) { }
            if (frozen.Count > 0) rows.Add(Rf(1, "Not responding", string.Join(", ", frozen.Distinct().Take(5)), "Give it a minute; if it stays frozen, end it in Task Manager (unsaved work in it is lost).", "taskmgr.exe"));
            if (hogB > 0) rows.Add(Rf(hogB > 3L << 30 ? 1 : 3, "Uses the most memory", hog + " · " + Mb(hogB), hogB > 3L << 30 ? "Closing and reopening it usually gives the memory back." : "", "taskmgr.exe"));
            // installed in the last week
            var recent = new List<string>();
            foreach (var h in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                try
                {
                    using (var b = RegistryKey.OpenBaseKey(h, RegistryView.Registry64)) using (var k = b.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                        if (k != null) foreach (var n in k.GetSubKeyNames().Take(2000)) using (var e = k.OpenSubKey(n)) { if (e == null) continue; DateTime dt; string dn = Convert.ToString(e.GetValue("DisplayName"), CultureInfo.InvariantCulture); if (!string.IsNullOrEmpty(dn) && DateTime.TryParseExact(Convert.ToString(e.GetValue("InstallDate"), CultureInfo.InvariantCulture), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) && dt > DateTime.Now.AddDays(-7)) recent.Add(dn); }
                }
                catch (Exception rex) { if (!(rex is System.Security.SecurityException || rex is IOException || rex is UnauthorizedAccessException)) throw; }   /* one unreadable key must not stop the rest */
            rows.Add(Rf(3, "Installed in the last 7 days", recent.Count == 0 ? "nothing" : string.Join(", ", recent.Distinct().Take(6)), recent.Count > 0 ? "If problems started this week, a new program is a good suspect - uninstall it in Installed apps (some need admin)." : "", "ms-settings:appsfeatures"));
            rows.Add(R(3, "Names stuck after a network change", "type  ipconfig /flushdns  in a Command Prompt", "Works without admin; Windows forgets cached names and asks again. AdeeboMeter doesn't run it for you.", "w"));
            return rows;
        }

        // ================================================================== Before / after snapshot (this session, in memory)
        sealed class Snap { public string Label; public DateTime At; public Dictionary<string, string> V = new Dictionary<string, string>(); }
        static readonly List<Snap> snaps = new List<Snap>();
        public static List<string[]> Snapshot(Monitor m, string label)
        {
            var rows = new List<string[]>(); label = Draw.Trunc((label ?? "").Trim(), 40); if (label == "") label = "snapshot " + (snaps.Count + 1);
            var s = new Snap { Label = label, At = DateTime.Now };
            NetworkInterface nic; bool vpn; string vpnN, gw, dns, ssid; lock (m.L) { nic = m.Primary; vpn = m.VpnUp; vpnN = m.VpnName; gw = m.Gateway; dns = m.DnsServer; ssid = m.Ssid; }
            s.V["Adapter"] = nic != null ? nic.Name + " (" + nic.NetworkInterfaceType + ")" : "none";
            s.V["Wi-Fi network"] = string.IsNullOrEmpty(ssid) ? "-" : ssid;
            if (nic != null)
            {
                var ip = nic.GetIPProperties();
                s.V["IPv4 address"] = string.Join(", ", ip.UnicastAddresses.Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork).Select(u => u.Address + "/" + u.PrefixLength));
                s.V["IPv6 (public)"] = ip.UnicastAddresses.Any(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6 && (u.Address.GetAddressBytes()[0] & 0xE0) == 0x20) ? "yes" : "no";
                try { s.V["MTU"] = I(ip.GetIPv4Properties() != null ? ip.GetIPv4Properties().Mtu : 0); } catch (NetworkInformationException) { }
            }
            s.V["Gateway"] = gw ?? ""; s.V["DNS servers"] = dns ?? ""; s.V["VPN"] = vpn ? "on (" + vpnN + ")" : "off";
            s.V["Router answers in"] = double.IsNaN(m.GwPing.Avg(6)) ? "no answer" : m.GwPing.Avg(6).ToString("0", CultureInfo.CurrentCulture) + " ms";
            s.V["Internet answers in"] = double.IsNaN(m.NetPing.Avg(6)) ? "no answer" : m.NetPing.Avg(6).ToString("0", CultureInfo.CurrentCulture) + " ms";
            s.V["Default routes"] = I(NetworkInterface.GetAllNetworkInterfaces().Count(n => n.OperationalStatus == OperationalStatus.Up && n.GetIPProperties().GatewayAddresses.Any(g => !g.Address.Equals(IPAddress.Any))));
            s.V["Proxy"] = Convert.ToString(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Internet Settings", "ProxyServer", ""), CultureInfo.InvariantCulture) ?? "";
            Snap prev; lock (snaps) { prev = snaps.LastOrDefault(); snaps.Add(s); if (snaps.Count > 6) snaps.RemoveAt(0); }
            if (prev == null)
            {
                rows.Add(R(0, "Saved \"" + label + "\"", s.At.ToString("HH:mm:ss", CultureInfo.CurrentCulture), "Now change one thing (VPN on, dock, Wi-Fi -> cable) and run this again with a new name - you'll see exactly what changed. Kept until AdeeboMeter closes, never saved to disk.", "w"));
                foreach (var kv in s.V) rows.Add(R(3, kv.Key, kv.Value, "", "w"));
                return rows;
            }
            var diff = s.V.Keys.Union(prev.V.Keys).Where(k => { string a, b; prev.V.TryGetValue(k, out a); s.V.TryGetValue(k, out b); return (a ?? "") != (b ?? ""); }).ToList();
            rows.Add(R(diff.Count == 0 ? 0 : 3, "\"" + prev.Label + "\" -> \"" + label + "\"", diff.Count == 0 ? "nothing changed" : diff.Count + " things changed", "Comparing with the snapshot taken at " + prev.At.ToString("HH:mm", CultureInfo.CurrentCulture) + ".", "w"));
            foreach (var k in diff) { string a, b; prev.V.TryGetValue(k, out a); s.V.TryGetValue(k, out b); rows.Add(R(1, k, (a ?? "-") + "  ->  " + (b ?? "-"), "", "w")); }
            foreach (var k in s.V.Keys.Except(diff)) rows.Add(R(3, k, s.V[k] + " (same)", "", "w"));
            return rows;
        }

        // ================================================================== DNS answer compare: Windows' DNS vs one you name
        public static List<string[]> DnsCompare(Monitor m, string name, string other)
        {
            var rows = new List<string[]>(); name = (name ?? "").Trim().TrimEnd('.');
            if (name == "" || name.Length > 253 || !Regex.IsMatch(name, @"^[\w.-]+$")) { rows.Add(R(2, "Name", "type a name - e.g. outlook.office.com", "", "u")); return rows; }
            string dns; lock (m.L) dns = m.DnsServer; IPAddress mine; if (!IPAddress.TryParse((dns ?? "").Split(',')[0].Trim(), out mine)) { rows.Add(R(2, "Your DNS", "no DNS server set", "", "u")); return rows; }
            string o = (other ?? "").Trim(); if (o == "") o = "1.1.1.1";
            Func<Toolkit.Msg, List<string>> ans = x => x == null ? null : x.Rrs.Where(r => r.Section == 1 && (r.Type == "A" || r.Type == "AAAA" || r.Type == "CNAME")).Select(r => r.Type + " " + r.Data).OrderBy(z => z, StringComparer.Ordinal).ToList();
            Toolkit.Msg a4 = Toolkit.Query(mine, name, 1, true), a6 = Toolkit.Query(mine, name, 28, true), b4, b6; string where;
            if (o.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) { Uri u; if (!Uri.TryCreate(o, UriKind.Absolute, out u)) { rows.Add(R(2, "Other server", "not a DoH address", "", "u")); return rows; } string w; b4 = Toolkit.QueryDoh(u, name, 1, out w); b6 = Toolkit.QueryDoh(u, name, 28, out w); where = u.Host + " (DoH)"; }
            else { IPAddress ob; if (!IPAddress.TryParse(o, out ob)) { rows.Add(R(2, "Other server", "an IP like 1.1.1.1, or a DoH address", "", "u")); return rows; } b4 = Toolkit.Query(ob, name, 1, true); b6 = Toolkit.Query(ob, name, 28, true); where = ob.ToString(); }
            var A = (ans(a4) ?? new List<string>()).Concat(ans(a6) ?? new List<string>()).Distinct().ToList(); var B = (ans(b4) ?? new List<string>()).Concat(ans(b6) ?? new List<string>()).Distinct().ToList();
            if (a4 == null && a6 == null) { rows.Add(R(2, "Your DNS (" + mine + ")", "no answer", "", "m")); return rows; }
            if (b4 == null && b6 == null) { rows.Add(R(2, where, "no answer", "Blocked here (company networks often allow only their own DNS), or the server is down.", "m")); return rows; }
            bool same = A.SequenceEqual(B), overlap = A.Intersect(B).Any();
            rows.Add(R(same ? 0 : overlap ? 3 : 1, name, same ? "the same answer from both" : overlap ? "partly the same" : "DIFFERENT answers", same ? "" : "Normal for big services (each DNS gets the nearest servers) and for company names only your DNS knows. Suspicious if a well-known site points somewhere odd.", "m"));
            rows.Add(R(3, "Your DNS (" + mine + ")", A.Count == 0 ? "no address" : string.Join(" · ", A.Take(12)), "", "m"));
            rows.Add(R(3, where, B.Count == 0 ? "no address" : string.Join(" · ", B.Take(12)), "", "m"));
            return rows;
        }

        // ================================================================== Many ports for one service
        static readonly Dictionary<string, int[]> Presets = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase) {
            { "web", new[] { 80, 443, 8080, 8443 } }, { "mail", new[] { 25, 465, 587, 110, 995, 143, 993 } }, { "ad", new[] { 53, 88, 135, 389, 445, 464, 636, 3268, 3269 } },
            { "sql", new[] { 1433, 3306, 5432, 1521 } }, { "rdp", new[] { 3389 } }, { "ssh", new[] { 22 } }, { "voip", new[] { 5060, 5061, 443 } }, { "print", new[] { 9100, 631, 515, 80, 443 } },
            { "smb", new[] { 445, 139 } }, { "vpn", new[] { 443, 500, 1194, 1723 } }, { "dns", new[] { 53, 853 } } };
        static readonly Dictionary<int, string> PortName = new Dictionary<int, string> { { 22, "SSH" }, { 25, "SMTP" }, { 53, "DNS" }, { 80, "HTTP" }, { 88, "Kerberos" }, { 110, "POP3" }, { 135, "RPC" }, { 139, "NetBIOS" }, { 143, "IMAP" }, { 389, "LDAP" }, { 443, "HTTPS" }, { 445, "SMB" }, { 464, "Kerberos pw" }, { 465, "SMTPS" }, { 500, "IKE" }, { 515, "LPD" }, { 587, "Submission" }, { 631, "IPP" }, { 636, "LDAPS" }, { 853, "DoT" }, { 993, "IMAPS" }, { 995, "POP3S" }, { 1194, "OpenVPN" }, { 1433, "SQL Server" }, { 1521, "Oracle" }, { 1723, "PPTP" }, { 3268, "Global catalog" }, { 3269, "GC over TLS" }, { 3306, "MySQL" }, { 3389, "RDP" }, { 5060, "SIP" }, { 5061, "SIP TLS" }, { 5432, "PostgreSQL" }, { 8080, "HTTP alt" }, { 8443, "HTTPS alt" }, { 9100, "Printer" } };
        static string Knock(IPAddress a, int port, out double ms)
        {
            var sw = Stopwatch.StartNew(); ms = double.NaN;
            using (var c = new TcpClient(a.AddressFamily))
            {
                try { if (c.ConnectAsync(a, port).Wait(2000)) { ms = sw.ElapsedMilliseconds; return "open"; } return "no answer"; }
                catch (AggregateException e) { var se = e.InnerException as SocketException; if (se != null && se.SocketErrorCode == SocketError.ConnectionRefused) { ms = sw.ElapsedMilliseconds; return "refused"; } return "failed"; }
            }
        }
        public static List<string[]> ManyPorts(Monitor m, string host, string what, string ok, Toolkit.Run run)
        {
            var rows = new List<string[]>(); if (!Lab.Allowed(m, ok, rows)) return rows; string h = (host ?? "").Trim();
            if (h == "" || h.Length > 253 || !Regex.IsMatch(h, @"^[\w.:%-]+$")) { rows.Add(R(2, "Host", "type a host or IP", "", "u")); return rows; }
            string w = (what ?? "").Trim(); if (w == "") w = "web"; int[] ports;
            if (!Presets.TryGetValue(w, out ports))
            {
                // whole numbers only: "100000" is a mistake, not port 10000
                var all = Regex.Matches(w, @"\d+").Cast<Match>().Select(x => x.Value).ToList();
                var badNum = all.FirstOrDefault(x => x.Length > 5 || int.Parse(x, CultureInfo.InvariantCulture) < 1 || int.Parse(x, CultureInfo.InvariantCulture) > 65535);
                if (badNum != null) { rows.Add(R(2, "Ports", badNum + " isn't a port (1-65535)", "", "u")); return rows; }
                var nums = all.Select(x => int.Parse(x, CultureInfo.InvariantCulture)).Distinct().ToArray();
                if (nums.Length == 0) { rows.Add(R(2, "Ports", "a service (" + string.Join(", ", Presets.Keys) + ") or up to 20 ports, e.g. 80,443,8443", "", "u")); return rows; }
                if (nums.Length > 20) { rows.Add(R(2, "Ports", "20 ports at most - this isn't a port scanner", "", "u")); return rows; }
                ports = nums;
            }
            var a = Lab.Addr(h); if (a == null) { rows.Add(R(2, h, "couldn't find it", "", "m")); return rows; }
            var res = new List<string[]>(); int open = 0;
            foreach (var p in ports)
            {
                if (run.Stop) break; run.Status = "Port " + p + "..."; double ms; string st = Knock(a, p, out ms); if (st == "open") open++;
                string n; PortName.TryGetValue(p, out n);
                res.Add(R(st == "open" ? 0 : st == "refused" ? 1 : 2, I(p) + (n != null ? " " + n : ""), st + (double.IsNaN(ms) ? "" : " · " + ms.ToString("0", CultureInfo.CurrentCulture) + " ms"), st == "refused" ? "The host answered: nothing listens there." : st == "no answer" ? "Dropped on the way - usually a firewall." : "", "m"));
            }
            bool stopped = res.Count < ports.Length; int tried = res.Count;
            rows.Add(R(open == tried ? 0 : open == 0 ? 2 : 1, a + (a.ToString() != h ? " (" + h + ")" : ""), open + " of " + tried + " ports open" + (stopped ? " (" + (ports.Length - tried) + " not tried)" : ""), "One TCP connection per port, closed at once - no data sent.", "m"));
            rows.AddRange(res); if (res.Count < ports.Length) rows.Add(R(3, "Stopped", "stopped by you - the untested ports are not counted", "", "m")); return rows;
        }

        // ================================================================== Every IP behind a name
        public static List<string[]> AllIps(string target, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string h; int port;
            if (!Lab.Split(target, 443, out h, out port)) { rows.Add(R(2, "Target", "type a name, or name:port - e.g. outlook.office.com:443", "", "u")); return rows; }
            IPAddress[] all; try { all = Dns.GetHostAddresses(h); } catch (SocketException) { rows.Add(R(2, h, "the name doesn't resolve", "", "m")); return rows; } catch (ArgumentException) { rows.Add(R(2, h, "not a valid name", "", "u")); return rows; }
            var res = new List<string[]>(); int ok = 0;
            foreach (var a in all.Take(16))
            {
                if (run.Stop) break; run.Status = a + "..."; double ms; string st = Knock(a, port, out ms); if (st == "open") ok++;
                res.Add(R(st == "open" ? 0 : 2, a.ToString(), st + (double.IsNaN(ms) ? "" : " · " + ms.ToString("0", CultureInfo.CurrentCulture) + " ms"), "", "m"));
            }
            if (run.Stop) rows.Add(R(1, "Stopped", "stopped by you after " + res.Count + " addresses", "", "m"));
            rows.Add(R(res.Count == 0 ? 3 : ok == res.Count ? 0 : ok == 0 ? 2 : 1, h + ":" + port, all.Length + " addresses · " + ok + " of " + res.Count + " answer", ok > 0 && ok < res.Count ? "Some addresses fail: apps that pick one at random fail now and then - the classic \"works on retry\". Tell the service's owner which ones." : all.Length > 16 ? "Only the first 16 tested." : "", "m"));
            rows.AddRange(res); return rows;
        }

        // ================================================================== My sites: the pinned sites, each walked once
        public static List<string[]> MySites(Toolkit.Run run)
        {
            var rows = new List<string[]>(); var hosts = Cfg.Pins.ToArray() /* a copy: the list is the UI thread's, this runs on a worker */.Select(Security.HostOf).Where(x => x != null).Distinct().Take(15).ToList();
            if (hosts.Count == 0) { rows.Add(R(3, "My sites", "no sites yet", "Pin the sites and services you care about in Investigate › Check a destination - they appear here, and the Security checkup checks their certificates.", "u")); return rows; }
            foreach (var h in hosts)
            {
                if (run.Stop) break; run.Status = h + "...";
                IPAddress[] a; try { a = Dns.GetHostAddresses(h); } catch (SocketException) { rows.Add(R(2, h, "name doesn't resolve", "", "m")); continue; } catch (ArgumentException) { rows.Add(R(2, h, "not a name", "", "m")); continue; }
                if (a.Length == 0) { rows.Add(R(2, h, "the name has no addresses", "", "m")); continue; }
                double ms; string st = Knock(a[0], 443, out ms);
                if (st != "open") { rows.Add(R(2, h, "HTTPS port " + st, "", "m")); continue; }
                X509Days(h, out ms);
                int days = double.IsNaN(ms) ? -1 : (int)ms;
                bool expired = !double.IsNaN(ms) && ms < 0;
                rows.Add(R(double.IsNaN(ms) ? 3 : expired ? 2 : days >= 0 && days < 14 ? 1 : 0, h, "reachable · " + a[0] + (expired ? " · certificate EXPIRED " + (-days) + " days ago" : !double.IsNaN(ms) ? " · certificate " + days + " days left" : " · certificate not read"), expired ? "Visitors get a security warning - renew it." : "", "m"));
            }
            return rows;
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5359", Justification = "reads the certificate's dates only - nothing is sent")]
        static void X509Days(string h, out double days)
        {
            days = double.NaN; System.Security.Cryptography.X509Certificates.X509Certificate2 got = null;
            try
            {
                using (var c = Toolkit.Open(h, 443, 4000)) { if (c == null) return; using (var ssl = new SslStream(c.GetStream(), false, (o, cert, ch, e) => { if (cert != null) got = new System.Security.Cryptography.X509Certificates.X509Certificate2(cert); return true; })) { var hs = System.Threading.Tasks.Task.Factory.StartNew(() => ssl.AuthenticateAsClient(h)); if (!hs.Wait(6000)) { c.Close(); try { hs.Wait(1000); } catch (AggregateException) { } } } }   /* close first: the stuck handshake ends before the stream is disposed */
            }
            catch (AggregateException) { } catch (SocketException) { } catch (IOException) { }
            if (got != null) using (got) days = Math.Floor((got.NotAfter - DateTime.Now).TotalDays);
        }

        // ================================================================== Two-PC compare: two saved AdeeboMeter results, line by line
        public static List<string[]> TwoPc(string fa, string fb)
        {
            var rows = new List<string[]>(); string[] la, lb; string err;
            if ((la = ReadSaved(fa, out err)) == null) { rows.Add(R(2, "First file", err, "Save a result on each PC (Save... on any Toolbox result), then put both files here.", "u")); return rows; }
            if ((lb = ReadSaved(fb, out err)) == null) { rows.Add(R(2, "Second file", err, "", "u")); return rows; }
            Func<string[], Dictionary<string, string>> kv = ls => { var d = new Dictionary<string, string>(); foreach (var l in ls) { int c = l.IndexOf(": ", StringComparison.Ordinal); if (c > 0 && c < 80) { string k = l.Substring(0, c).Trim(); string k2 = k; for (int n = 2; d.ContainsKey(k2); n++) k2 = k + " #" + n; d[k2] = l.Substring(c + 2).Trim(); } } return d; };   /* several rows with one name ("Checked"): each is compared, as name #2, #3... */
            var A = kv(la); var B = kv(lb); var keys = A.Keys.Union(B.Keys).ToList();
            var diff = keys.Where(k => { string x, y; A.TryGetValue(k, out x); B.TryGetValue(k, out y); return x != y; }).ToList();
            rows.Add(R(diff.Count == 0 ? 0 : 3, Path.GetFileName(fa.Trim().Trim('"')) + " vs " + Path.GetFileName(fb.Trim().Trim('"')), keys.Count + " rows · " + diff.Count + " differ", "Where the two PCs disagree is where to look - e.g. one gets a different DNS answer, route or certificate.", "w"));
            foreach (var k in diff.Take(200)) { string x, y; A.TryGetValue(k, out x); B.TryGetValue(k, out y); rows.Add(R(1, Draw.Trunc(k, 60), (x == null ? "(not on the first)" : Draw.Trunc(x, 140)) + "   |   " + (y == null ? "(not on the second)" : Draw.Trunc(y, 140)), "", "w")); }
            return rows;
        }
        static string[] ReadSaved(string p, out string err)
        {
            err = ""; p = (p ?? "").Trim().Trim('"');
            try
            {
                bool unc = Regex.IsMatch(p, @"^\\\\[^\\?.][^\\]*\\[^\\]+\\.");   // \\server\share\file.txt - a share is fine; \\?\ and \\.\ device paths are not
                if (p == "" || !Path.IsPathRooted(p) || (p.StartsWith(@"\\", StringComparison.Ordinal) && !unc)) { err = @"type the full path of a saved .txt result (C:\... or \\server\share\...)"; return null; }
                var fi = new FileInfo(p); if (!fi.Exists) { err = "not found"; return null; } if (fi.Length > 4 << 20) { err = "bigger than 4 MB - not a saved result"; return null; }
                return File.ReadAllLines(fi.FullName);
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)) throw; err = Draw.Trunc(e.Message, 80); return null; }
        }

        // ================================================================== RDP reachability: TCP, then the RDP negotiation - no sign-in
        public static List<string[]> Rdp(string target)
        {
            var rows = new List<string[]>(); string h; int port;
            if (!Lab.Split(target, 3389, out h, out port)) { rows.Add(R(2, "Host", "type a host or host:port (3389)", "", "u")); return rows; }
            var a = Lab.Addr(h); if (a == null) { rows.Add(R(2, h, "couldn't find it", "", "m")); return rows; }
            using (var c = new TcpClient(a.AddressFamily))
            {
                var sw = Stopwatch.StartNew();
                try { if (!c.ConnectAsync(a, port).Wait(3000)) { rows.Add(R(2, a + ":" + port, "no answer in 3 s", "A firewall drops it, or the PC is off / asleep.", "m")); return rows; } }
                catch (AggregateException ae)
                {
                    var se = ae.InnerException as SocketException;
                    if (se != null && se.SocketErrorCode == SocketError.ConnectionRefused) rows.Add(R(2, a + ":" + port, "refused - the PC is there, Remote Desktop isn't on (or another port)", "", "m"));
                    else rows.Add(R(2, a + ":" + port, "couldn't connect: " + Draw.Trunc(ae.InnerException != null ? ae.InnerException.Message : ae.Message, 80), "No route to it from here (another network, a VPN that's off), or a firewall rejects it.", "m"));
                    return rows;
                }
                rows.Add(R(0, a + ":" + port, "the port is open (" + sw.ElapsedMilliseconds + " ms)", "", "m"));
                // X.224 connection request with an RDP negotiation request: TLS + CredSSP (NLA) offered
                var req = new byte[] { 3, 0, 0, 19, 14, 0xE0, 0, 0, 0, 0, 0, 1, 0, 8, 0, 3, 0, 0, 0 };
                var s = c.GetStream(); s.ReadTimeout = 3000; var b = new byte[64]; int n = 0;
                try { s.Write(req, 0, req.Length); n = s.Read(b, 0, b.Length); } catch (IOException) { }
                if (n >= 19 && b[0] == 3 && b[5] == 0xD0)
                {
                    int type = b[11], proto = b[15] | b[16] << 8;
                    if (type == 2) rows.Add(R(0, "Remote Desktop", "answers · uses " + (proto == 2 ? "Network Level Authentication (CredSSP)" : proto == 1 ? "TLS - sign-in happens on the remote screen (no NLA)" : proto == 8 ? "NLA with early user check" : "protocol " + proto), proto == 1 ? "Without NLA the sign-in screen is shown before you prove who you are." : "", "m"));
                    else if (type == 3) rows.Add(R(1, "Remote Desktop", "answers, but refused the security offered (code " + b[15] + ")", b[15] == 5 ? "It demands NLA via a method this probe didn't offer." : "", "m"));
                    else rows.Add(R(3, "Remote Desktop", "answers (old-style, no negotiation)", "", "m"));
                }
                else if (n > 0) rows.Add(R(1, "Port " + port, "something answers, but it isn't Remote Desktop", "", "m"));
                else rows.Add(R(1, "Port " + port, "open, but no Remote Desktop answer", "", "m"));
            }
            return rows;
        }

        // ================================================================== LDAP / AD reachability: the rootDSE, read anonymously (what any client reads first)
        static byte[] Tlv(byte t, byte[] v) { return Snmp.Tlv(t, v); }
        static byte[] Cat(params byte[][] p) { return p.SelectMany(x => x).ToArray(); }
        static byte[] Os(string s) { return Tlv(4, Encoding.UTF8.GetBytes(s)); }
        static readonly string[] RootAttrs = { "defaultNamingContext", "dnsHostName", "serverName", "currentTime", "domainFunctionality", "supportedLDAPVersion", "isGlobalCatalogReady" };
        public static byte[] RootDseRequest()
        {
            var attrs = Tlv(0x30, RootAttrs.SelectMany(Os).ToArray());
            var search = Tlv(0x63, Cat(Os(""), Tlv(0x0A, new byte[] { 0 }), Tlv(0x0A, new byte[] { 0 }), Tlv(2, new byte[] { 0 }), Tlv(2, new byte[] { 5 }), Tlv(1, new byte[] { 0 }), Tlv(0x87, Encoding.ASCII.GetBytes("objectClass")), attrs));
            return Tlv(0x30, Cat(Tlv(2, new byte[] { 1 }), search));
        }
        // a whole SearchResultDone (op 0x65) has arrived - walking the messages, not looking for a 0x65 byte (that's an "e")
        static bool LdapDone(byte[] b, int n)
        {
            int pos = 0;
            while (pos + 2 <= n)
            {
                if (b[pos] != 0x30) return false;
                int lenAt = pos + 1, len = b[lenAt], hdr = 2;
                if ((len & 0x80) != 0) { int k = len & 0x7f; if (k < 1 || k > 3 || lenAt + k >= n) return false; len = 0; for (int i = 1; i <= k; i++) len = len << 8 | b[lenAt + i]; hdr = 2 + k; }
                if (pos + hdr + len > n) return false;
                int id = pos + hdr; if (id + 2 > n || b[id] != 0x02) return false;
                int op = id + 2 + b[id + 1]; if (op >= n) return false;
                if (b[op] == 0x65) return true;
                pos += hdr + len;
            }
            return false;
        }
        public static Dictionary<string, string> ParseRootDse(byte[] b, int n)
        {
            // pull "type" then its values: each attribute is SEQUENCE { OCTET type, SET { OCTET values } }
            var d = new Dictionary<string, string>(); int i = 0;
            while (i < n - 4)
            {
                if (b[i] == 0x04 && b[i + 1] < 0x80 && i + 2 + b[i + 1] <= n)
                {
                    string t = Encoding.UTF8.GetString(b, i + 2, b[i + 1]);
                    if (RootAttrs.Contains(t))
                    {
                        int j = i + 2 + b[i + 1]; var vals = new List<string>();
                        if (j + 2 < n && b[j] == 0x31) { int setLen = b[j + 1], k = j + 2; if (setLen >= 0x80) { k += setLen & 0x7F; setLen = 0; for (int q = 0; q < (b[j + 1] & 0x7F); q++) setLen = setLen << 8 | b[j + 2 + q]; } int end = Math.Min(n, k + setLen); while (k + 2 <= end && b[k] == 0x04) { int l = b[k + 1], o = k + 2; if (l >= 0x80) { int lb = l & 0x7F; l = 0; for (int q = 0; q < lb; q++) l = l << 8 | b[o + q]; o += lb; } if (o + l > end) break; vals.Add(Encoding.UTF8.GetString(b, o, l)); k = o + l; } }
                        d[t] = string.Join(", ", vals); i = j; continue;
                    }
                }
                i++;
            }
            return d;
        }
        public static List<string[]> Ldap(string target)
        {
            var rows = new List<string[]>(); string h = (target ?? "").Trim();
            if (h == "") h = Environment.GetEnvironmentVariable("USERDNSDOMAIN") ?? "";
            int port; if (!Lab.Split(h, 389, out h, out port)) { rows.Add(R(2, "Server", "type a domain controller or your domain - e.g. corp.example.com", "", "u")); return rows; }
            var a = Lab.Addr(h); if (a == null) { rows.Add(R(2, h, "couldn't find it", "", "m")); return rows; }
            using (var c = new TcpClient(a.AddressFamily))
            {
                var sw = Stopwatch.StartNew();
                try { if (!c.ConnectAsync(a, port).Wait(3000)) { rows.Add(R(2, a + ":" + port, "no answer in 3 s", "", "m")); return rows; } }
                catch (AggregateException ae) { var se = ae.InnerException as SocketException; rows.Add(R(2, a + ":" + port, se != null && se.SocketErrorCode == SocketError.ConnectionRefused ? "refused - nothing listens there (not an LDAP server)" : "couldn't connect: " + (ae.InnerException ?? ae).Message, "", "m")); return rows; }
                var s = c.GetStream(); s.ReadTimeout = 4000; var req = RootDseRequest(); var b = new byte[16384]; int n = 0;
                try { s.Write(req, 0, req.Length); var until = DateTime.UtcNow.AddSeconds(4); while (n < b.Length && DateTime.UtcNow < until) { int k = s.Read(b, n, b.Length - n); if (k <= 0) break; n += k; if (LdapDone(b, n)) break; } } catch (IOException) { }
                var d = ParseRootDse(b, n);
                if (d.Count == 0) { rows.Add(R(1, a + ":" + port, n > 0 ? "answered, but not with a directory root" : "connected, no LDAP answer", "", "m")); return rows; }
                rows.Add(R(0, a + ":" + port + " (" + sw.ElapsedMilliseconds + " ms)", "LDAP answers", "Read anonymously, as every client does first - no password is sent.", "m"));
                string[] fl = { "2000", "2003 interim", "2003", "2008", "2008 R2", "2012", "2012 R2", "2016", "", "", "2025" };   // 7 covers 2016-2022; 10 is 2025
                foreach (var kv in d)
                {
                    string v = kv.Value; int f;
                    if (kv.Key == "domainFunctionality" && int.TryParse(v, out f) && f >= 0 && f < fl.Length && fl[f] != "") v = "Windows Server " + fl[f] + " level";
                    if (kv.Key == "currentTime") { DateTime t; if (DateTime.TryParseExact(Regex.Replace(v, @"\.[0-9]+Z$", "Z"), "yyyyMMddHHmmssZ", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t)) { double skew = (DateTime.UtcNow - t).TotalSeconds; rows.Add(R(Math.Abs(skew) > 300 ? 2 : Math.Abs(skew) > 60 ? 1 : 0, "Clock difference", skew.ToString("0", CultureInfo.CurrentCulture) + " s", Math.Abs(skew) > 300 ? "Over 5 minutes breaks Kerberos sign-in." : "", "m")); continue; } }
                    rows.Add(R(3, kv.Key, Draw.Trunc(v, 200), "", "m"));
                }
            }
            foreach (var p in new[] { 636, 3268, 88 }) { double ms; string st = Knock(a, p, out ms); string n2; PortName.TryGetValue(p, out n2); rows.Add(R(st == "open" ? 0 : 1, I(p) + " " + n2, st, "", "m")); }
            return rows;
        }

        // ================================================================== SMB shares on a host (NetShareEnum, as you)
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct ShareInfo1 { public string Name; public uint Type; public string Remark; }
        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)] static extern int NetShareEnum(string server, int level, out IntPtr buf, int max, out int read, out int total, IntPtr resume);
        [DllImport("netapi32.dll")] static extern int NetApiBufferFree(IntPtr buf);
        public static List<string[]> Shares(string host)
        {
            var rows = new List<string[]>(); string h = (host ?? "").Trim().TrimStart('\\');
            IPAddress v6h; if (h.StartsWith("[", StringComparison.Ordinal) && h.EndsWith("]", StringComparison.Ordinal)) h = h.Substring(1, h.Length - 2);   // [2001:db8::1] - Windows takes the bare address
            bool isV6 = IPAddress.TryParse(h, out v6h) && v6h.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            if (h == "" || (!isV6 && !Regex.IsMatch(h, @"^[\w.-]{1,253}$"))) { rows.Add(R(2, "Host", "type a PC, NAS or server name - e.g. nas01 or 192.168.1.20", "", "u")); return rows; }
            IntPtr buf; int read, total, rc;
            try { rc = NetShareEnum(@"\\" + h, 1, out buf, -1, out read, out total, IntPtr.Zero); }
            catch (DllNotFoundException) { rows.Add(R(3, h, "not available on this system", "", "m")); return rows; } catch (EntryPointNotFoundException) { rows.Add(R(3, h, "not available on this system", "", "m")); return rows; }
            if (rc != 0) { rows.Add(R(2, @"\\" + h, rc == 5 ? "access denied - your account can't list its shares" : rc == 53 ? "not found, or file sharing (port 445) is blocked" : rc == 1219 ? "you're already connected with another account" : "Windows error " + rc, rc == 53 ? "Try Many ports with \"smb\" to see if 445 answers." : "", "m")); return rows; }
            var list = new List<ShareInfo1>();
            try { int sz = Marshal.SizeOf(typeof(ShareInfo1)); for (int i = 0; i < read; i++) list.Add((ShareInfo1)Marshal.PtrToStructure(buf + i * sz, typeof(ShareInfo1))); }
            finally { if (NetApiBufferFree(buf) != 0) Program.Log("share list: buffer not freed"); }
            var vis = list.Where(x => !x.Name.EndsWith("$", StringComparison.Ordinal)).ToList(); var hid = list.Count - vis.Count;
            rows.Add(R(0, @"\\" + h, vis.Count + " shared folders" + (hid > 0 ? " · " + hid + " hidden admin shares ($)" : ""), "Listed with your own Windows account - nothing is opened or read.", "m"));
            foreach (var x in vis.Concat(list.Where(z => z.Name.EndsWith("$", StringComparison.Ordinal))).Take(100)) rows.Add(R(3, x.Name, ((x.Type & 0xFF) == 0 ? "folder" : (x.Type & 0xFF) == 1 ? "printer" : (x.Type & 0xFF) == 3 ? "IPC" : "special") + (string.IsNullOrEmpty(x.Remark) ? "" : " · " + x.Remark), "", "m"));
            if (list.Count > 100) rows.Add(R(3, "Shown", "the first 100 of " + list.Count + " shares", "", "m"));
            return rows;
        }

        // ================================================================== Services that failed to start (System log, 7 days)
        public static List<string[]> FailedServices()
        {
            var rows = new List<string[]>(); string err;
            var evs = EvLog.Recent("System", "EventID=7000 or EventID=7001 or EventID=7009 or EventID=7011 or EventID=7022 or EventID=7023 or EventID=7024 or EventID=7031 or EventID=7034", 24 * 7, 500, out err);
            if (err != "") { rows.Add(R(3, "System log", err, "", "w")); return rows; }
            var by = evs.Select(e => new { e, Svc = EvLog.Field(e.Xml, "param1") }).Where(x => x.Svc != "").GroupBy(x => x.Svc).OrderByDescending(g => g.Count()).ToList();
            rows.Add(R(by.Count == 0 ? 0 : 1, "Last 7 days", by.Count == 0 ? "no service failed to start or stopped unexpectedly" : by.Count + " services had trouble" + (evs.Count >= 500 ? " (from the newest 500 events)" : ""), "From Windows' own System log (Service Control Manager).", "w"));
            foreach (var g in by.Take(25))
            {
                var f = g.First(); bool crash = g.Any(x => x.e.Id == 7031 || x.e.Id == 7034);
                rows.Add(R(crash ? 2 : 1, g.Key, g.Count() + "× · last " + f.e.T.ToString("ddd HH:mm", CultureInfo.CurrentCulture) + " · " + (crash ? "stopped unexpectedly" : f.e.Id == 7009 || f.e.Id == 7011 ? "timed out starting" : f.e.Id == 7001 ? "a service it needs failed" : f.e.Id == 7022 ? "hung while starting" : f.e.Id == 7023 || f.e.Id == 7024 ? "started, then stopped with an error" : "failed to start"), Draw.Trunc(f.e.Text, 200), "w"));
            }
            return rows;
        }
    }
}
