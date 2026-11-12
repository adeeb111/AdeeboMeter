// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // The system engineer's side: IPv6 ping / trace, domain controllers, Group Policy, expiring certificates,
    // firewall rules for an app, what's listening, the event log and the time source. All read-only, no admin.
    static class SysTools
    {
        static string Run(string exe, string args, int timeout) { try { return Job.Run(Job.Sys(exe), args, timeout); } catch (Exception e) { Program.Log("tool " + exe + ": " + e.Message); return ""; } }   // missing on some editions
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }

        // ---------- IPv6 ping and trace
        public static List<string[]> Ping6(string target, Toolkit.Run run)
        {
            var rows = new List<string[]>(); var a = Resolve6(target);
            if (a == null) { rows.Add(R(2, "IPv6 address", "couldn't find an IPv6 address for " + target, HasV6() ? "The name may have no AAAA record." : "This PC has no public IPv6 address - your network or provider doesn't give one.", "m")); return rows; }
            run.SeriesUnit = "ms"; int ok = 0;
            for (int i = 0; i < 10 && !run.Stop; i++) { var r = Icmp.Echo6(a, 32, 128, 2000); if (r.Status == Icmp.Ok) { ok++; run.Add(r.Rtt); } else run.Add(double.NaN); run.Status = "Ping " + (i + 1) + " / 10 to " + a; System.Threading.Thread.Sleep(300); }
            var s = run.Copy().Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
            rows.Add(R(ok == 10 ? 0 : ok > 0 ? 1 : 2, "Ping " + a, ok + " of 10 answered" + (s.Count > 0 ? " · min " + s[0] + " ms · median " + s[s.Count / 2] + " ms" : ""), "", "m"));
            // IPv6 path MTU: routers never fragment IPv6 - they send "Packet Too Big" and Windows remembers the size per destination.
            // Three full-size pings make any router on the way say so; then Windows' own destination cache tells the size it learned.
            if (ok > 0 && !run.Stop)
            {
                run.Status = "IPv6 path MTU..."; int big = 0, tooBig = 0; for (int i = 0; i < 3; i++) { var st = Icmp.Echo6(a, 1452, 128, 2000).Status; if (st == Icmp.Ok) big++; else if (st == Icmp.TooBig) tooBig++; }
                int pmtu = PathMtu6(a);
                if (pmtu > 0) rows.Add(R(pmtu < 1280 ? 2 : pmtu < 1500 ? 1 : 0, "Path MTU (IPv6)", pmtu + " bytes" + (tooBig > 0 ? " · a router answered Packet Too Big" : big == 0 ? " · full-size pings got no answer" : ""), pmtu < 1500 ? "Something on the way (a tunnel, PPPoE or a VPN) carries less than 1500 bytes. Fine as long as Packet Too Big messages get through - if they're blocked, big downloads stall." : "Full-size packets pass all the way.", "m"));
                else rows.Add(R(big == 3 ? 0 : 1, "Path MTU (IPv6)", big == 3 ? "full-size (1500-byte) pings pass" : big + " of 3 full-size pings answered", big < 3 ? "Windows didn't record a smaller size. If small pings work and big ones don't, Packet Too Big messages are being dropped - a classic cause of stalled downloads over IPv6." : "", "m"));
            }
            run.Status = "Tracing over IPv6...";
            int silent = 0;
            for (int ttl = 1; ttl <= 30 && !run.Stop && silent < 5; ttl++)
            {
                Icmp.Res best = null; for (int k = 0; k < 2; k++) { var r = Icmp.Echo6(a, 32, ttl, 1500); if (r.Status == Icmp.Ok || r.Status == Icmp.TtlExpired) { best = r; break; } }
                if (best == null) { silent++; rows.Add(R(3, "hop " + ttl, "*", "", "m")); continue; }
                silent = 0; rows.Add(R(3, "hop " + ttl, best.From + "   " + best.Rtt + " ms", "", "m"));
                if (best.Status == Icmp.Ok) break;
            }
            return rows;
        }
        // the size Windows learned for this destination ("netsh interface ipv6 show destinationcache"): PMTU is the first number on its line
        static int PathMtu6(IPAddress a)
        {
            string o = Run("netsh.exe", "interface ipv6 show destinationcache address=" + a, 5000), want = a.ToString();
            foreach (var l in o.Split('\n'))
            {
                var f = l.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries); int v;
                if (f.Length >= 2 && f.Skip(1).Any(x => string.Equals(x, want, StringComparison.OrdinalIgnoreCase)) && int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v >= 576 && v <= 65535) return v;
            }
            return 0;
        }
        // ---------- DHCP: the lease Windows holds right now (registry, readable without admin) and the IPv6 address lifetimes
        public static List<string[]> Dhcp(Monitor m)
        {
            var rows = new List<string[]>(); string id; lock (m.L) id = m.IfaceId;
            NetworkInterface nic = null; try { nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == id); } catch (NetworkInformationException) { }
            if (nic == null) { rows.Add(R(2, "Adapter", "no active adapter", "", "w")); return rows; }
            rows.Add(R(3, "Adapter", nic.Name + " · " + nic.Description, "", "w"));
            bool dhcp4 = false; try { var p4 = nic.GetIPProperties().GetIPv4Properties(); dhcp4 = p4 != null && p4.IsDhcpEnabled; } catch (NetworkInformationException) { }
            if (!dhcp4) rows.Add(R(3, "IPv4", "set by hand (static) - no DHCP lease", "", "w"));
            else
                try
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + id))
                    {
                        if (k == null) rows.Add(R(1, "IPv4 lease", "Windows has no lease details for this adapter", "", "w"));
                        else
                        {
                            Func<string, string> str = n => { var v = k.GetValue(n); var a2 = v as string[]; return a2 != null ? string.Join(" ", a2) : (v ?? "").ToString().Trim(); };
                            Func<string, DateTime> time = n => { var v = k.GetValue(n); long t; return v != null && long.TryParse(v.ToString(), out t) && t > 0 ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(t).ToLocalTime() : DateTime.MinValue; };
                            DateTime got = time("LeaseObtainedTime"), t1 = time("T1"), t2 = time("T2"), end = time("LeaseTerminatesTime"), now = DateTime.Now;
                            string ip = str("DhcpIPAddress");
                            rows.Add(R(ip == "" || ip.StartsWith("169.254", StringComparison.Ordinal) ? 2 : 0, "Address", ip == "" ? "none from DHCP" : ip + " / " + str("DhcpSubnetMask"), ip.StartsWith("169.254", StringComparison.Ordinal) ? "169.254 means no DHCP server answered - Windows made up an address. Nothing past this PC will work." : "", "w"));
                            rows.Add(R(3, "DHCP server", str("DhcpServer"), "", "w"));
                            if (got > DateTime.MinValue) rows.Add(R(3, "Obtained", got.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture) + " (" + Ago(now - got) + " ago)", "", "w"));
                            if (t1 > DateTime.MinValue) rows.Add(R(3, "Renews (T1)", t1.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture) + (t1 > now ? " (in " + Ago(t1 - now) + ")" : " (due - renewing)"), "Halfway through the lease Windows asks the same server to extend it.", "w"));
                            if (t2 > DateTime.MinValue) rows.Add(R(3, "Rebinds (T2)", t2.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture), "If renewing failed, Windows asks any DHCP server.", "w"));
                            if (end > DateTime.MinValue) rows.Add(R(end < now ? 2 : (end - now).TotalMinutes < 10 ? 1 : 0, "Expires", end.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture) + (end > now ? " (in " + Ago(end - now) + ")" : " (expired)"), got > DateTime.MinValue && (end - got).TotalMinutes < 60 ? "A short lease (under an hour) - common on guest Wi-Fi; renewals happen often." : "", "w"));
                            string gw = str("DhcpDefaultGateway"), dns = str("DhcpNameServer"), dom = str("DhcpDomain");
                            if (gw != "") rows.Add(R(3, "Router (option 3)", gw, "", "w"));
                            if (dns != "") rows.Add(R(3, "DNS (option 6)", dns, "", "w"));
                            if (dom != "") rows.Add(R(3, "Domain (option 15)", dom, "", "w"));
                        }
                    }
                }
                catch (System.Security.SecurityException) { rows.Add(R(1, "IPv4 lease", "Windows didn't let a normal user read it", "", "w")); }
            // IPv6: where each address came from and how long it stays
            try
            {
                foreach (var u in nic.GetIPProperties().UnicastAddresses.Where(x => x.Address.AddressFamily == AddressFamily.InterNetworkV6 && !x.Address.IsIPv6LinkLocal).Take(6))
                {
                    string from = u.PrefixOrigin == PrefixOrigin.Dhcp ? "DHCPv6" : u.PrefixOrigin == PrefixOrigin.RouterAdvertisement ? (u.SuffixOrigin == SuffixOrigin.Random ? "router advert (temporary, private)" : "router advert (SLAAC)") : u.PrefixOrigin == PrefixOrigin.Manual ? "set by hand" : u.PrefixOrigin.ToString();
                    long valid = u.AddressValidLifetime, pref = u.AddressPreferredLifetime;
                    rows.Add(R(3, "IPv6 " + from, u.Address + "  · valid " + Life(valid) + " · preferred " + Life(pref), "", "w"));
                }
            }
            catch (NetworkInformationException) { }
            catch (PlatformNotSupportedException) { }
            return rows;
        }
        static string Life(long s) { return s <= 0 ? "0" : s >= uint.MaxValue ? "forever" : Ago(TimeSpan.FromSeconds(s)); }
        static string Ago(TimeSpan t) { return t.TotalMinutes < 1 ? (int)t.TotalSeconds + " s" : t.TotalHours < 1 ? (int)t.TotalMinutes + " min" : t.TotalDays < 2 ? t.TotalHours.ToString("0.#", CultureInfo.CurrentCulture) + " h" : t.TotalDays.ToString("0", CultureInfo.CurrentCulture) + " days"; }
        static bool HasV6() { try { return NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up).SelectMany(n => n.GetIPProperties().UnicastAddresses).Any(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6 && !u.Address.IsIPv6LinkLocal && !IPAddress.IsLoopback(u.Address) && !u.Address.ToString().StartsWith("fd", StringComparison.OrdinalIgnoreCase)); } catch { return false; } }
        static IPAddress Resolve6(string host)
        {
            var h = (host ?? "").Trim().Trim('[', ']'); IPAddress a;
            if (IPAddress.TryParse(h, out a)) return a.AddressFamily == AddressFamily.InterNetworkV6 ? a : null;
            if (h.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || h.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) { Uri u; if (Uri.TryCreate(h, UriKind.Absolute, out u)) h = u.Host; }
            try { return Dns.GetHostAddresses(h).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetworkV6); } catch { return null; }
        }

        // ---------- domain controllers
        public static List<string[]> DomainControllers(Monitor m)
        {
            var rows = new List<string[]>(); string dom = "";
            try { dom = IPGlobalProperties.GetIPGlobalProperties().DomainName; } catch { }
            if (string.IsNullOrEmpty(dom)) { rows.Add(R(3, "Domain", "this PC isn't joined to a domain", "Home and Entra-only (cloud) PCs have no domain controller to find.", "w")); return rows; }
            rows.Add(R(3, "Domain", dom, "", "w"));
            var nl = Run("nltest.exe", "/dsgetdc:" + dom, 15000);
            var dcm = Regex.Match(nl, @"DC:\s*\\\\(\S+)"); var addr = Regex.Match(nl, @"Address:\s*\\\\(\S+)"); var site = Regex.Match(nl, @"Our Site Name:\s*(\S+)"); var dsite = Regex.Match(nl, @"Dc Site Name:\s*(\S+)");
            if (dcm.Success) rows.Add(R(0, "Windows found", dcm.Groups[1].Value + (addr.Success ? " (" + addr.Groups[1].Value + ")" : "") + (dsite.Success ? " · site " + dsite.Groups[1].Value : ""), site.Success && dsite.Success && site.Groups[1].Value != dsite.Groups[1].Value ? "Your PC is in site " + site.Groups[1].Value + " but uses a domain controller in another site - sign-in and Group Policy will be slower." : "", "w"));
            else rows.Add(R(2, "Windows found", "no domain controller (" + Draw.Trunc(nl.Trim().Split('\n').LastOrDefault() ?? "", 80) + ")", "On VPN? The domain controllers are only reachable once the VPN is up. Off VPN this is normal.", "w"));
            IPAddress srv; string dns; lock (m.L) dns = m.DnsServer;
            var dcs = new List<Tuple<string, IPAddress>>();
            if (IPAddress.TryParse((dns ?? "").Split(',')[0].Trim(), out srv))
            {
                var msg = Toolkit.Query(srv, "_ldap._tcp.dc._msdcs." + dom, 33, true);
                if (msg == null || msg.Rcode != 0) rows.Add(R(2, "DNS record for domain controllers", "your DNS server (" + srv + ") doesn't know _ldap._tcp.dc._msdcs." + dom, "Your PC asks a DNS server that doesn't serve the domain - usually a VPN DNS setting.", "m"));
                else
                {
                    var targets = msg.Rrs.Where(x => x.Type == "SRV").Select(x => x.Data.Split(' ').Last().TrimEnd('.')).Distinct().Take(6).ToList();
                    rows.Add(R(0, "DNS record for domain controllers", targets.Count + " listed: " + string.Join(", ", targets), "", "m"));
                    foreach (var t in targets.Take(3)) { try { var ip = Dns.GetHostAddresses(t).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork); if (ip != null) dcs.Add(Tuple.Create(t, ip)); } catch { } }
                }
            }
            var ports = new[] { new[] { "88", "Kerberos" }, new[] { "389", "LDAP" }, new[] { "636", "LDAPS" }, new[] { "445", "SMB (Group Policy files)" }, new[] { "135", "RPC" }, new[] { "3268", "Global catalog" } };
            foreach (var dc in dcs)
            {
                var res = ports.Select(p => new { p, t = PathTools.Connect(dc.Item2, int.Parse(p[0], CultureInfo.InvariantCulture), 2000) }).ToList();
                var bad = res.Where(x => double.IsNaN(x.t)).Select(x => x.p[1] + " " + x.p[0]).ToList();
                rows.Add(R(bad.Count == 0 ? 0 : bad.Count == res.Count ? 2 : 1, dc.Item1, bad.Count == 0 ? "all 6 ports answer · " + Math.Round(res.Min(x => x.t)) + " ms" : "not answering: " + string.Join(", ", bad), bad.Count > 0 && bad.Count < res.Count ? "A firewall between you and the domain controller blocks part of what sign-in needs." : "", "m"));
            }
            return rows;
        }

        // ---------- Group Policy (what applied to you)
        public static List<string[]> GroupPolicy()
        {
            var rows = new List<string[]>();
            // the XML report first - its element names are the same in every language (the /r text isn't). Read as plain
            // text with simple patterns: no XML parser, no DTDs, nothing fetched.
            string xf = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "adeebometer-gp-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                Run("gpresult.exe", "/scope user /x \"" + xf + "\" /f", 30000);
                if (System.IO.File.Exists(xf) && new System.IO.FileInfo(xf).Length < 20000000)
                {
                    string x = System.IO.File.ReadAllText(xf);   // gpresult writes UTF-16 with a byte-order mark
                    var um = Regex.Match(x, @"<UserResults>([\s\S]*?)</UserResults>");
                    if (um.Success)
                    {
                        string u = um.Groups[1].Value;
                        Func<string, string, string> val = (blk, n) => { var mm = Regex.Match(blk, "<" + n + @"(?:\s[^>]*)?>([^<]*)</" + n + ">"); return mm.Success ? System.Net.WebUtility.HtmlDecode(mm.Groups[1].Value).Trim() : ""; };
                        var rt = Regex.Match(x, @"<ReadTime>([^<]+)</ReadTime>"); DateTime t;
                        if (rt.Success && DateTime.TryParse(rt.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t)) rows.Add(R(3, "Last read", t.ToLocalTime().ToString("g", CultureInfo.CurrentCulture), "", "w"));
                        var gpos = Regex.Matches(u, @"<GPO>([\s\S]*?)</GPO>").Cast<Match>().Select(mm => mm.Groups[1].Value).ToList();
                        var applied = gpos.Where(g => val(g, "FilterAllowed") == "true" && val(g, "AccessDenied") != "true" && val(g, "Enabled") != "false").Select(g => val(g, "Name")).Where(n => n != "").Distinct().ToList();
                        var filtered = gpos.Where(g => val(g, "FilterAllowed") == "false" || val(g, "AccessDenied") == "true").Select(g => val(g, "Name")).Where(n => n != "").Distinct().ToList();
                        if (applied.Count > 0) rows.Add(R(3, "Applied", string.Join(" · ", applied.Take(30)), "", "w"));
                        if (filtered.Count > 0) rows.Add(R(3, "Filtered out", string.Join(" · ", filtered.Take(30)), "", "w"));
                        var grp = Regex.Matches(u, @"<SecurityGroup>([\s\S]*?)</SecurityGroup>").Cast<Match>().Select(mm => val(mm.Groups[1].Value, "Name")).Where(n => n != "").ToList();
                        if (grp.Count > 0) rows.Add(R(3, "Your security groups", grp.Count + ": " + Draw.Trunc(string.Join(", ", grp), 300), "", "w"));
                        if (rows.Count > 0) return rows;
                    }
                }
            }
            catch (Exception e) { if (!(e is System.IO.IOException || e is UnauthorizedAccessException)) throw; Program.Log("gpresult xml: " + e.Message); }
            finally { try { if (System.IO.File.Exists(xf)) System.IO.File.Delete(xf); } catch (System.IO.IOException) { } }
            var o = Run("gpresult.exe", "/r /scope user", 30000);
            if (o.Trim() == "") { rows.Add(R(3, "Group Policy", "gpresult gave no answer (not on a domain, or it timed out)", "", "w")); return rows; }
            var last = Regex.Match(o, @"Last time Group Policy was applied:\s*(.+)"); var from = Regex.Match(o, @"Group Policy was applied from:\s*(.+)");
            if (last.Success) rows.Add(R(3, "Last applied", last.Groups[1].Value.Trim(), "", "w"));
            if (from.Success) rows.Add(R(3, "From", from.Groups[1].Value.Trim(), "", "w"));
            foreach (var sect in new[] { new[] { "Applied Group Policy Objects", "Applied" }, new[] { "The following GPOs were not applied because they were filtered out", "Filtered out" } })
            {
                int i = o.IndexOf(sect[0], StringComparison.OrdinalIgnoreCase); if (i < 0) continue;
                var lines = o.Substring(i).Split('\n').Skip(2).Select(l => l.TrimEnd('\r')).TakeWhile(l => l.Trim() != "" || false).Select(l => l.Trim()).Where(l => l != "" && !l.StartsWith("-", StringComparison.Ordinal) && !l.StartsWith("Filtering", StringComparison.OrdinalIgnoreCase)).Take(20).ToList();
                if (lines.Count > 0) rows.Add(R(3, sect[1], string.Join(" · ", lines), "", "w"));
            }
            var groups = Regex.Match(o, @"The user is a part of the following security groups\s*-+\s*([\s\S]*?)(\r?\n\s*\r?\n|$)");
            if (groups.Success) { var g = groups.Groups[1].Value.Split('\n').Select(x => x.Trim()).Where(x => x != "").ToList(); rows.Add(R(3, "Your security groups", g.Count + ": " + Draw.Trunc(string.Join(", ", g.Take(15)), 300), "", "w")); }
            if (rows.Count == 0) rows.Add(R(3, "Group Policy", Draw.Trunc(o.Trim(), 200), "", "w"));
            return rows;
        }

        // ---------- certificates that expire soon
        public static List<string[]> Certs()
        {
            var rows = new List<string[]>();
            foreach (var loc in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
            {
                X509Store st = null;
                try
                {
                    st = new X509Store(StoreName.My, loc); st.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    foreach (var c in st.Certificates.Cast<X509Certificate2>().OrderBy(c => c.NotAfter))
                    {
                        double days = (c.NotAfter - DateTime.Now).TotalDays; string name = c.GetNameInfo(X509NameType.SimpleName, false);
                        string use = string.Join(", ", c.Extensions.OfType<X509EnhancedKeyUsageExtension>().SelectMany(e => e.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>()).Select(x => x.FriendlyName ?? x.Value).Take(3));
                        int lv = days < 0 ? 2 : days < 30 ? 1 : 0;
                        if (lv == 0 && rows.Count > 25) continue;
                        rows.Add(R(lv, (loc == StoreLocation.CurrentUser ? "Yours · " : "This PC · ") + Draw.Trunc(name, 40), (days < 0 ? "EXPIRED " : "expires ") + c.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + (days >= 0 ? " (" + (int)days + " days)" : "") + (use != "" ? " · " + use : "") + (c.HasPrivateKey ? "" : " · no private key"),
                            lv > 0 && use.Contains("Client Authentication") ? "Wi-Fi / VPN sign-in with a certificate stops working when it expires - your IT renews it, often by connecting to the company network." : "", "w"));
                        c.Dispose();
                    }
                }
                catch (Exception e) { rows.Add(R(3, loc == StoreLocation.CurrentUser ? "Your certificates" : "This PC's certificates", "couldn't read (" + Draw.Trunc(e.Message, 60) + ")", "", "w")); }
                finally { if (st != null) st.Close(); }
            }
            if (rows.Count == 0) rows.Add(R(0, "Certificates", "no personal certificates on this PC", "", "w"));
            return rows;
        }

        // ---------- Windows Firewall rules that mention an app (read through the firewall's own COM object)
        public static List<string[]> Firewall(string app)
        {
            var rows = new List<string[]>(); app = (app ?? "").Trim();
            if (app == "") { rows.Add(R(2, "App", "type part of a program name or path, e.g. teams or chrome.exe", "", "u")); return rows; }
            try
            {
                var t = Type.GetTypeFromProgID("HNetCfg.FwPolicy2"); if (t == null) { rows.Add(R(3, "Firewall", "Windows Firewall isn't available here", "", "w")); return rows; }
                object pol = Activator.CreateInstance(t);
                Func<object, string, object> get = (o, n) => o.GetType().InvokeMember(n, BindingFlags.GetProperty, null, o, null, CultureInfo.InvariantCulture);
                var rules = (IEnumerable)get(pol, "Rules"); var sw = Stopwatch.StartNew(); int seen = 0, hits = 0;
                foreach (object r in rules)
                {
                    if (++seen > 5000 || sw.ElapsedMilliseconds > 10000) break;
                    string name = (get(r, "Name") ?? "").ToString(), path = (get(r, "ApplicationName") ?? "").ToString(), svc = (get(r, "ServiceName") ?? "").ToString();
                    if (name.IndexOf(app, StringComparison.OrdinalIgnoreCase) < 0 && path.IndexOf(app, StringComparison.OrdinalIgnoreCase) < 0 && svc.IndexOf(app, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (++hits > 30) continue;
                    bool en = Convert.ToBoolean(get(r, "Enabled"), CultureInfo.InvariantCulture); int dir = Convert.ToInt32(get(r, "Direction"), CultureInfo.InvariantCulture), act = Convert.ToInt32(get(r, "Action"), CultureInfo.InvariantCulture), prof = Convert.ToInt32(get(r, "Profiles"), CultureInfo.InvariantCulture), proto = Convert.ToInt32(get(r, "Protocol"), CultureInfo.InvariantCulture);
                    string ports = (get(r, dir == 1 ? "LocalPorts" : "RemotePorts") ?? "").ToString();
                    var profs = new List<string>(); if ((prof & 1) != 0) profs.Add("domain"); if ((prof & 2) != 0) profs.Add("private"); if ((prof & 4) != 0) profs.Add("public"); if (prof == 0x7FFFFFFF) { profs.Clear(); profs.Add("all networks"); }
                    rows.Add(R(!en ? 3 : act == 0 ? 1 : 0, Draw.Trunc(name, 50), (en ? "" : "(off) ") + (act == 0 ? "BLOCK " : "allow ") + (dir == 1 ? "in" : "out") + " · " + (proto == 6 ? "TCP" : proto == 17 ? "UDP" : proto == 256 ? "any protocol" : "protocol " + proto) + (ports != "" && ports != "*" ? " " + ports : "") + " · " + string.Join(", ", profs) + (path != "" ? " · " + Draw.Trunc(System.IO.Path.GetFileName(path), 30) : ""), "", "w"));
                }
                rows.Insert(0, R(hits == 0 ? 3 : 0, "Rules for \"" + app + "\"", hits + " of " + seen + " rules" + (hits > 30 ? " (first 30 shown)" : ""), hits == 0 ? "No rule names this app - Windows' default applies (outgoing allowed, incoming blocked). Company firewalls may still block it elsewhere." : "A block rule wins over an allow rule. 'Public' rules apply when Windows thinks you're on a public network.", "w"));
            }
            catch (Exception e) { rows.Add(R(3, "Firewall", "couldn't read the rules (" + Draw.Trunc(e.Message, 60) + ")", "", "w")); }
            return rows;
        }

        // ---------- what's listening on this PC
        public static List<string[]> Listening(Monitor m)
        {
            var rows = new List<string[]>(); List<Conn> all; try { all = m.Connections(true); } catch { all = new List<Conn>(); }
            // IPv4 and IPv6 listeners alike (a service bound only to [::] is open too); loopback = 127.x or [::1]
            var tcp = all.Where(c => (c.Proto == "TCP" || c.Proto == "TCP6") && c.State == "LISTEN").ToList(); var udp = all.Where(c => c.Proto == "UDP" || c.Proto == "UDP6").ToList();
            Func<Conn, bool> loop = c => c.Local.StartsWith("127.", StringComparison.Ordinal) || c.Local.StartsWith("[::1]", StringComparison.Ordinal);
            var exposed = tcp.Where(c => !loop(c)).OrderBy(c => c.LocalPort).ToList();
            var local = tcp.Where(loop).ToList();
            rows.Add(R(3, "Listening", exposed.Count + " TCP ports open to the network · " + local.Count + " only to this PC · " + udp.Count + " UDP", "Ports open to the network can be reached by other devices if the firewall lets them. AdeeboMeter itself never listens.", "w"));
            foreach (var g in exposed.GroupBy(c => c.Proc).OrderBy(g => g.Min(c => c.LocalPort)).Take(25))
            {
                var ports = g.Select(c => c.LocalPort).Distinct().OrderBy(p => p).ToList();
                string known = string.Join(", ", ports.Select(p => Known(p)).Where(k => k != "").Distinct());
                rows.Add(R(3, Draw.Trunc(g.Key, 30), "TCP " + string.Join(", ", ports.Take(8)) + (ports.Count > 8 ? " +" + (ports.Count - 8) : "") + (g.First().Local.StartsWith("0.0.0.0", StringComparison.Ordinal) ? " · every network" : " · " + g.First().Local.Split(':')[0]) + (known != "" ? " · " + known : ""), "", "w"));
            }
            if (local.Count > 0) rows.Add(R(3, "Only to this PC (127.0.0.1)", string.Join(", ", local.GroupBy(c => c.Proc).Select(g => g.Key + " " + string.Join("/", g.Select(c => c.LocalPort).Distinct().Take(3))).Take(12)), "Loopback ports can't be reached from the network.", "w"));
            return rows;
        }
        static string Known(int p)
        {
            switch (p) { case 135: return "RPC"; case 139: case 445: return "file sharing"; case 3389: return "Remote Desktop"; case 5040: return "Windows (CDP)"; case 5357: return "network discovery"; case 7680: return "Delivery Optimization"; case 2179: return "Hyper-V"; case 22: return "SSH"; case 80: case 443: case 8080: return "web server"; case 49664: case 49665: case 49666: case 49667: case 49668: case 49669: case 49670: return "Windows RPC"; default: return ""; }
        }

        // ---------- event log: errors and warnings in 24 h, grouped, with what the common ones mean
        static readonly Dictionary<string, string> Hints = new Dictionary<string, string>
        {
            { "41", "The PC lost power or was forced off without shutting down." }, { "6008", "The previous shutdown was unexpected." },
            { "1014", "A DNS name lookup timed out." }, { "4199", "Another device uses this PC's IP address." }, { "4227", "TCP ran out of ports - something opens connections too fast." },
            { "7000", "A service failed to start." }, { "7009", "A service took too long to start." }, { "7031", "A service crashed and was restarted." }, { "7034", "A service crashed." },
            { "10016", "A DCOM permission note - Windows logs it constantly; harmless." }, { "129", "The disk controller was reset - a disk or driver problem." }, { "153", "Disk operations had to be retried - the disk may be failing." },
            { "51", "Error while paging to disk." }, { "7", "The disk has bad blocks." }, { "27", "The network cable or link went down." }, { "10400", "The network adapter reset." }, { "1000", "A program crashed." }, { "1002", "A program stopped responding." },
            { "1001", "Windows Error Reporting saved a crash report." }, { "36887", "A TLS connection failed (the other side sent an alert)." }, { "36874", "A TLS connection failed - no shared protocol or cipher." }, { "5719", "This PC couldn't reach a domain controller." }, { "1129", "Group Policy couldn't reach the domain (network)." }, { "1058", "Group Policy couldn't read a policy file." }
        };
        public static List<string[]> EventSearch(string filter)
        {
            var rows = new List<string[]>(); filter = (filter ?? "").Trim(); int total = 0;
            foreach (var log in new[] { "System", "Application" })
            {
                string err; var evs = EvLog.Recent(log, "Level=1 or Level=2 or Level=3", 24, 400, out err);
                if (err != "") { rows.Add(R(3, log, err, "", "w")); continue; }
                var withP = evs.Select(e => new { e, P = Regex.Match(e.Xml, @"<Provider Name=['""]([^'""]+)['""]").Groups[1].Value })
                    .Where(x => filter == "" || x.P.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || x.e.Text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || x.e.Id.ToString(CultureInfo.InvariantCulture) == filter).ToList();
                total += withP.Count;
                foreach (var g in withP.GroupBy(x => x.P + "|" + x.e.Id).OrderByDescending(g => g.Min(x => x.e.Level) <= 2 ? 1 : 0).ThenByDescending(g => g.Count()).Take(12))
                {
                    var f = g.First(); string id = f.e.Id.ToString(CultureInfo.InvariantCulture), hint; Hints.TryGetValue(id, out hint);
                    int lv = id == "10016" ? 3 : g.Min(x => x.e.Level) <= 2 ? 2 : 1;
                    rows.Add(R(lv, log + " · " + Draw.Trunc(f.P.Replace("Microsoft-Windows-", ""), 30) + " " + id, g.Count() + "× · last " + f.e.T.ToString("HH:mm", CultureInfo.CurrentCulture) + " · " + Draw.Trunc(Regex.Replace(f.e.Text, @"\s+", " "), 140), hint ?? "", "w"));
                }
            }
            rows.Insert(0, R(3, "Errors and warnings, last 24 h", total + (filter != "" ? " matching \"" + filter + "\"" : ""), "Every Windows PC logs some errors - look for ones that repeat around the time the problem happened.", "w"));
            return rows;
        }

        // ---------- where this PC gets its time
        public static List<string[]> TimeSource()
        {
            var rows = new List<string[]>();
            var src = Run("w32tm.exe", "/query /source", 8000).Trim();
            string type = "", server = "";
            try { using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\W32Time\Parameters")) if (k != null) { type = (k.GetValue("Type") ?? "").ToString(); server = (k.GetValue("NtpServer") ?? "").ToString(); } } catch { }
            string how = type == "NT5DS" ? "from the domain (a domain controller)" : type == "NTP" ? "from an internet / company NTP server" : type == "NoSync" ? "not syncing at all" : type == "AllSync" ? "from the domain or NTP, whichever works" : type;
            rows.Add(R(type == "NoSync" ? 2 : 0, "How", how == "" ? "unknown" : how, "", "w"));
            // "The following error occurred: The service has not been started. (0x80070426)" is an error, not a source
            bool w32err = src.IndexOf("0x8007", StringComparison.OrdinalIgnoreCase) >= 0 || src.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || src.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0;
            if (w32err) { rows.Add(R(2, "Current source", Draw.Trunc(src.Replace("\r", " ").Replace("\n", " "), 140), src.IndexOf("0x80070426", StringComparison.OrdinalIgnoreCase) >= 0 ? "The Windows Time service isn't running, so the clock isn't being synced. It starts on its own on most PCs; on others it's started by the domain." : "", "w")); src = ""; }
            if (src != "") rows.Add(R(src.IndexOf("Local CMOS", StringComparison.OrdinalIgnoreCase) >= 0 || src.IndexOf("Free-running", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0, "Current source", src, src.IndexOf("Local CMOS", StringComparison.OrdinalIgnoreCase) >= 0 ? "It runs on its own clock - no server has set it recently. Settings > Time > Sync now." : src.IndexOf("access is denied", StringComparison.OrdinalIgnoreCase) >= 0 ? "" : "", "w"));
            if (server != "") rows.Add(R(3, "Configured server", server.Replace(",0x9", "").Replace(",0x1", "").Replace(",0x8", ""), "", "w"));
            rows.Add(R(3, "Check it", "Use the NTP tool on the same server to see how far off this PC is.", "", "i"));
            return rows;
        }
    }
}
