// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AdeeboMeter
{
    // "Why does this one thing fail?" - proxy, hosts file, DNS hijack and VPN name rules, port test, Teams UDP (STUN), clock.
    // Rows: { level, name, value, advice, evidence }.
    static class WhyFails
    {
        [StructLayout(LayoutKind.Sequential)] struct WinHttpProxyInfo { public uint AccessType; public IntPtr Proxy, Bypass; }
        [DllImport("winhttp.dll", SetLastError = true)] static extern bool WinHttpGetDefaultProxyConfiguration(ref WinHttpProxyInfo info);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr h);
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }

        // ---------- which proxy a URL uses
        public static List<string[]> Proxy(string url)
        {
            var rows = new List<string[]>(); url = Toolkit.OneSlash(url);
            if (!(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))) url = "https://" + url;
            Uri u; if (!Uri.TryCreate(url, UriKind.Absolute, out u)) { rows.Add(R(2, "Address", "not a web address", "", "u")); return rows; }
            try
            {
                var p = WebRequest.GetSystemWebProxy(); var via = p.GetProxy(u); bool direct = p.IsBypassed(u) || via == u;
                rows.Add(R(direct ? 0 : 3, "This URL goes", direct ? "direct - no proxy" : "through the proxy " + via.Authority, direct ? "" : "Browsers and most apps send this site to the proxy first. If the site fails only here, the proxy is the suspect.", "w"));
            }
            catch (Exception e) { rows.Add(R(1, "This URL goes", "couldn't ask Windows: " + e.Message, "", "u")); }
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (k != null)
                    {
                        bool on = Convert.ToInt32(k.GetValue("ProxyEnable", 0), CultureInfo.InvariantCulture) == 1;
                        string srv = Convert.ToString(k.GetValue("ProxyServer", ""), CultureInfo.InvariantCulture), pac = Convert.ToString(k.GetValue("AutoConfigURL", ""), CultureInfo.InvariantCulture);
                        rows.Add(R(3, "Fixed proxy (browser / Windows settings)", on ? srv : "off", "", "w"));
                        rows.Add(R(3, "PAC file (automatic script)", pac != "" ? pac : "none", pac != "" ? "A script decides per site - company networks use this to send some sites direct and others through the proxy." : "", "w"));
                    }
                    using (var c = k == null ? null : k.OpenSubKey("Connections"))
                    {
                        var b = c == null ? null : c.GetValue("DefaultConnectionSettings") as byte[];
                        if (b != null && b.Length > 8) rows.Add(R(3, "Detect settings automatically (WPAD)", (b[8] & 8) != 0 ? "on" : "off", (b[8] & 8) != 0 ? "Windows looks for a proxy on the network by itself. On untrusted Wi-Fi this is a known risk." : "", "w"));
                    }
                }
            }
            catch (Exception e) { Program.Log("proxy reg: " + e.Message); }
            // WinHTTP's own setting from the API (netsh's words are translated on non-English Windows)
            string whText = "unknown";
            try
            {
                var pi = new WinHttpProxyInfo();
                if (WinHttpGetDefaultProxyConfiguration(ref pi))
                    try { whText = pi.AccessType == 3 && pi.Proxy != IntPtr.Zero ? Marshal.PtrToStringUni(pi.Proxy) : "direct"; }
                    finally { if (pi.Proxy != IntPtr.Zero && GlobalFree(pi.Proxy) != IntPtr.Zero) Program.Log("winhttp free"); if (pi.Bypass != IntPtr.Zero && GlobalFree(pi.Bypass) != IntPtr.Zero) Program.Log("winhttp free"); }
            }
            catch (Exception e) { if (!(e is DllNotFoundException || e is EntryPointNotFoundException)) throw; }
            rows.Add(R(3, "Windows services (WinHTTP)", whText, "Windows Update, Teams sign-in and other background services use this one - it can differ from the browser's.", "w"));
            return rows;
        }

        // ---------- the hosts file
        public static List<string[]> Hosts(string name)
        {
            var rows = new List<string[]>(); string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
            try
            {
                var lines = File.ReadAllLines(f).Select(l => { int c = l.IndexOf('#'); return (c >= 0 ? l.Substring(0, c) : l).Trim(); }).Where(l => l != "").ToList();
                if (lines.Count == 0) rows.Add(R(0, "Hosts file", "empty - every name goes to DNS", "", "w"));
                // every line is checked for your site (an ad-block hosts file has thousands); only the first 40 others are listed
                Func<string, bool> pins = l => name != "" && l.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Skip(1).Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
                var show = lines.Where(pins).Take(10).Concat(lines.Where(l => !pins(l)).Take(40)).ToList();
                if (lines.Count > show.Count) rows.Add(R(3, "Hosts file", lines.Count.ToString(CultureInfo.CurrentCulture) + " entries - every one checked" + (name != "" ? " for " + name : "") + ", the first 40 listed", "", "w"));
                foreach (var l in show)
                {
                    bool hit = pins(l);
                    rows.Add(R(hit ? 2 : 3, hit ? "Pins your site" : "Entry", l, hit ? "This line overrides DNS for " + name + " - if the address is old, the site breaks only on this PC. Remove the line (needs admin, or ask IT)." : "", "w"));
                }
            }
            catch (Exception e) { rows.Add(R(1, "Hosts file", "couldn't read: " + e.Message, "", "u")); }
            return rows;
        }

        // ---------- DNS hijack (made-up name) and the VPN's name rules (NRPT)
        public static List<string[]> DnsRules()
        {
            var rows = new List<string[]>();
            var rnd = new byte[6]; using (var g = RandomNumberGenerator.Create()) g.GetBytes(rnd);
            string fake = "adeebometer-" + string.Concat(rnd.Select(b => b.ToString("x2", CultureInfo.InvariantCulture))) + ".com";
            try { var a = Dns.GetHostAddresses(fake); rows.Add(R(2, "Made-up name", fake + " → " + string.Join(", ", a.Select(x => x.ToString()).Take(3)), "Your DNS answers names that don't exist (usually your provider's ad page). Typos land on their page, and some apps get confused. Your router or provider settings can turn it off - or use another DNS.", "m")); }
            catch (SocketException se) { if (se.SocketErrorCode == SocketError.HostNotFound) rows.Add(R(0, "Made-up name", "correctly not found", "Your DNS tells the truth about names that don't exist.", "m")); else rows.Add(R(3, "Made-up name", "couldn't tell - DNS didn't answer (" + se.SocketErrorCode + ")", "A timeout isn't a clean bill - try again when DNS works.", "m")); }
            catch (Exception e) { rows.Add(R(1, "Made-up name", e.Message, "", "u")); }
            int n = 0;
            foreach (var path in new[] { @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient\DnsPolicyConfig", @"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DnsPolicyConfig" })
                try
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(path))
                    {
                        if (k == null) continue;
                        var subs = k.GetSubKeyNames(); if (subs.Length > 20) rows.Add(R(3, "Name rules (NRPT)", subs.Length + " rules - the first 20 shown", "", "w"));
                        foreach (var sub in subs.Take(20))
                            using (var s = k.OpenSubKey(sub))
                            {
                                if (s == null) continue;
                                var names = s.GetValue("Name") as string[]; string srv = Convert.ToString(s.GetValue("GenericDNSServers", ""), CultureInfo.InvariantCulture);
                                rows.Add(R(3, "Name rule", (names != null ? string.Join(", ", names) : "?") + " → " + (srv != "" ? srv : "the VPN's DNS"), "Names ending like this are always asked of these servers (set by your VPN or IT).", "w")); n++;
                            }
                    }
                }
                catch (Exception e) { Program.Log("nrpt: " + e.Message); }
            if (n == 0) rows.Add(R(3, "Name rules (NRPT)", "none - every name goes to your adapters' DNS servers", "", "w"));
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces().Where(i => i.OperationalStatus == OperationalStatus.Up && i.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                try { var d = ni.GetIPProperties().DnsAddresses.Select(x => x.ToString()).Take(3).ToList(); if (d.Count > 0) rows.Add(R(3, "DNS on " + Draw.Trunc(ni.Name, 24) + (Monitor.IsVpn(ni) ? " (VPN)" : ""), string.Join(", ", d), "", "w")); } catch { }
            return rows;
        }

        // ---------- can I reach host:port?
        public static List<string[]> Port(string target)
        {
            var rows = new List<string[]>(); string h; int p; bool t; string e;
            if (!DestCheck.Parse(target, out h, out p, out t, out e)) { rows.Add(R(2, "Input", e, "Type host:port, e.g. mail.example.com:587", "u")); return rows; }
            IPAddress a; try { a = Dns.GetHostAddresses(h).First(); } catch (Exception ex) { rows.Add(R(2, "Name", h + ": " + ex.Message, "", "m")); return rows; }
            var sw = Stopwatch.StartNew(); string res; int lv;
            try
            {
                using (var s = new Socket(a.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                {
                    if (!Toolkit.ConnectWithin(s, a, p, 4000)) { res = "no answer in 4 s - silently dropped (a firewall on the way, or the server's)"; lv = 2; }
                    else { res = "open - connected in " + sw.ElapsedMilliseconds + " ms"; lv = 0; }
                }
            }
            catch (SocketException se) { res = se.SocketErrorCode == SocketError.ConnectionRefused ? "refused - the server is there but nothing listens on port " + p : Toolkit.SockWords(se.SocketErrorCode); lv = se.SocketErrorCode == SocketError.ConnectionRefused ? 1 : 2; }
            rows.Add(R(lv, h + ":" + p.ToString(CultureInfo.InvariantCulture), res, lv == 2 ? "Dropped means a firewall ate it - refused means the server answered 'not here'. Try from your phone on mobile data to tell your network's firewall from theirs." : "", "m"));
            rows.Add(R(3, "Address", a.ToString(), "", "m"));
            return rows;
        }

        // ---------- Teams UDP: STUN binding request to Microsoft's call relays; NAT type from two answers
        public static List<string[]> TeamsUdp()
        {
            var rows = new List<string[]>(); IPAddress[] relays;
            try { relays = Dns.GetHostAddresses("worldaz.relay.teams.microsoft.com").Where(x => x.AddressFamily == AddressFamily.InterNetwork).Take(2).ToList().ToArray(); }
            catch (Exception e) { rows.Add(R(2, "Teams relay", "couldn't find it: " + e.Message, "", "m")); return rows; }
            if (relays.Length == 0) { rows.Add(R(2, "Teams relay", "no IPv4 address", "", "m")); return rows; }
            var maps = new List<IPEndPoint>(); var mapFrom = new List<IPAddress>();
            using (var u = new UdpClient(0, AddressFamily.InterNetwork))
            {
                u.Client.ReceiveTimeout = 1500;
                foreach (var r in relays.Distinct().Take(2))
                {
                    var tid = new byte[12]; using (var g = RandomNumberGenerator.Create()) g.GetBytes(tid);
                    var req = new byte[20]; req[1] = 1; req[4] = 0x21; req[5] = 0x12; req[6] = 0xA4; req[7] = 0x42; Array.Copy(tid, 0, req, 8, 12);
                    IPEndPoint got = null; var sw = Stopwatch.StartNew();
                    for (int t = 0; t < 3 && got == null; t++)
                    {
                        try
                        {
                            // one socket for both relays (the NAT test needs the same local port), so every answer is checked: from that relay, port 3478, our transaction
                            sw.Restart(); u.Send(req, req.Length, new IPEndPoint(r, 3478)); var until = DateTime.UtcNow.AddMilliseconds(1500);
                            while (got == null && DateTime.UtcNow < until)
                            {
                                var from = new IPEndPoint(IPAddress.Any, 0); var resp = u.Receive(ref from);
                                if (from.Address.Equals(r) && from.Port == 3478) got = Mapped(resp, tid);
                            }
                        }
                        catch (SocketException) { }
                    }
                    rows.Add(R(got != null ? 0 : 2, "UDP 3478 to " + r, got != null ? "works - answer in " + sw.ElapsedMilliseconds + " ms; you're seen as " + got : "no answer", got != null ? "" : "UDP to Teams' relays is blocked here - calls will fall back to TCP 443 and stutter more. Ask IT to allow UDP 3478-3481 to Microsoft's Teams ranges.", "m"));
                    if (got != null && !mapFrom.Contains(r)) { maps.Add(got); mapFrom.Add(r); }   // the NAT test needs two different servers
                }
            }
            if (maps.Count < 2 && relays.Length == 1) rows.Add(R(3, "NAT type", "not tested - only one relay answered DNS, and the test needs two different servers", "", "m"));
            if (maps.Count >= 2)
            {
                bool same = maps[0].Port == maps[1].Port && maps[0].Address.Equals(maps[1].Address);
                rows.Add(R(same ? 0 : 1, "NAT type", same ? "keeps the same outside port for every server (easy for calls)" : "changes the outside port per server (symmetric NAT)", same ? "" : "Calls can't go straight between PCs - they always go through Microsoft's relays. Works, a little more delay.", "i"));
            }
            return rows;
        }
        static IPEndPoint Mapped(byte[] m, byte[] tid)
        {
            if (m == null || m.Length < 20 || m[0] != 1 || m[1] != 1 || m[4] != 0x21 || m[5] != 0x12 || m[6] != 0xA4 || m[7] != 0x42) return null;   // binding success, with the magic cookie
            for (int i = 0; i < 12; i++) if (m[8 + i] != tid[i]) return null;
            for (int p = 20; p + 4 <= m.Length;)
            {
                int type = m[p] << 8 | m[p + 1], len = m[p + 2] << 8 | m[p + 3]; int v = p + 4;
                if ((type == 0x0020 || type == 0x0001) && len >= 8 && v + 8 <= m.Length && m[v + 1] == 1)
                {
                    int port = m[v + 2] << 8 | m[v + 3]; var ip = new byte[] { m[v + 4], m[v + 5], m[v + 6], m[v + 7] };
                    if (type == 0x0020) { port ^= 0x2112; ip[0] ^= 0x21; ip[1] ^= 0x12; ip[2] ^= 0xA4; ip[3] ^= 0x42; }
                    return new IPEndPoint(new IPAddress(ip), port);
                }
                p = v + ((len + 3) & ~3);
            }
            return null;
        }

        // ---------- clock check against the Date of a server answer (the page Windows itself uses to test the internet)
        public static List<string[]> Clock() { return Clock(false); }
        // asked: the Clock check tool itself (you pressed it) - the policy still wins, the Sign-in page switch doesn't apply
        public static List<string[]> Clock(bool asked)
        {
            var rows = new List<string[]>();
            // the clock comparison asks msftconnecttest.com for its time - the same request as the sign-in page check, so the same
            // switches turn it off (Settings › Sign-in page check, or the policy's online_lookups). Then only this PC's own time shows.
            if ((!asked && !Cfg.Portal) || Policy.NoOnline) rows.Add(R(3, "Clock", "not compared online - " + (Policy.NoOnline ? "your IT team switched online lookups off" : "the sign-in page check is off in Settings"), "", "i"));
            else
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://www.msftconnecttest.com/connecttest.txt");
                req.Method = "HEAD"; req.Timeout = 5000; req.UserAgent = "AdeeboMeter"; req.Proxy = WebRequest.GetSystemWebProxy();
                var sw = Stopwatch.StartNew();
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    double half = sw.Elapsed.TotalSeconds / 2; DateTime srv;
                    if (!DateTime.TryParse(resp.Headers["Date"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out srv)) { rows.Add(R(1, "Clock", "the server sent no time", "", "u")); return rows; }
                    double off = (DateTime.UtcNow - srv).TotalSeconds - half;
                    int lv = Math.Abs(off) > 300 ? 2 : Math.Abs(off) > 60 ? 1 : 0;
                    rows.Add(R(lv, "Your clock", Math.Abs(off) < 2 ? "right (within a second or two)" : (off > 0 ? "ahead by " : "behind by ") + Dur(Math.Abs(off)), lv == 0 ? "" : "Certificates and company sign-in (Kerberos) fail when the clock is more than about 5 minutes off. Settings > Time > Sync now.", "m"));
                }
            }
            catch (Exception e) { rows.Add(R(1, "Clock", "couldn't compare: " + Draw.Trunc(e.Message, 60), "", "u")); }
            rows.Add(R(3, "This PC's time", DateTime.Now.ToString("ddd d MMM yyyy, HH:mm:ss", CultureInfo.CurrentCulture) + " (" + TimeZoneInfo.Local.StandardName + ")", "Certificates have a start and an end date: a wrong clock breaks HTTPS before the network does.", "w"));
            rows.AddRange(WindowsTime());
            return rows;
        }
        // Windows Time: where the clock syncs from and when it last did (w32tm /query /status needs no admin; when Windows
        // refuses, or answers in a language these labels don't cover, the rows are simply left out)
        static List<string[]> WindowsTime()
        {
            var rows = new List<string[]>();
            try
            {
                string o = Job.Run(Job.Sys("w32tm.exe"), "/query /status", 6000) ?? "";
                var src = Regex.Match(o, @"^\s*Source:\s*(.+?)\s*$", RegexOptions.Multiline); var last = Regex.Match(o, @"^\s*Last Successful Sync Time:\s*(.+?)\s*$", RegexOptions.Multiline);
                if (src.Success) rows.Add(R(3, "Syncs from", Regex.Replace(src.Groups[1].Value, @",0x[0-9a-fA-F]+", ""), src.Groups[1].Value.IndexOf("Local CMOS Clock", StringComparison.OrdinalIgnoreCase) >= 0 || src.Groups[1].Value.IndexOf("Free-running", StringComparison.OrdinalIgnoreCase) >= 0 ? "It isn't syncing with any time server - only the PC's own clock chip, which drifts." : "", "w"));
                if (last.Success) rows.Add(R(3, "Last sync", last.Groups[1].Value, "", "w"));
            }
            catch (Exception e) { if (!(e is InvalidOperationException || e is System.ComponentModel.Win32Exception || e is IOException || e is TimeoutException)) throw; }
            return rows;
        }
        static string Dur(double s) { return s >= 120 ? (s / 60).ToString("0", CultureInfo.CurrentCulture) + " minutes" : s.ToString("0", CultureInfo.CurrentCulture) + " seconds"; }
    }
}
