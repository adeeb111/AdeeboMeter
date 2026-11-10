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
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // Security checkup: defensive only. It reads this PC's settings and looks at YOUR network and YOUR sites.
    // It never tries a password, never exploits anything, never scans beyond your own router. Every fix is a
    // hand-off: a click opens the Windows screen where you change it yourself. No admin needed for any check.
    // A row: { level, title, value, advice, evidence, action } - level 0 good, 1 worth a look, 2 risky, 3 can't tell / info.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "result rows and small lists, built once per check")]
    static class Security
    {
        public sealed class Check { public string Id, Group, Title; public Func<Monitor, List<string>, List<string[]>> Run; }
        static string[] R(int lv, string title, string val, string adv, string fix) { return new[] { lv.ToString(CultureInfo.InvariantCulture), title, val, adv, "m", fix == null ? "" : "fix:" + fix }; }
        static List<string[]> One(int lv, string title, string val, string adv, string fix) { return new List<string[]> { R(lv, title, val, adv, fix) }; }
        static string I(long n) { return n.ToString(CultureInfo.InvariantCulture); }

        static object Reg(RegistryHive hive, string path, string name)
        {
            try { using (var b = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)) using (var k = b.OpenSubKey(path)) return k != null ? k.GetValue(name) : null; }
            catch (System.Security.SecurityException) { return null; } catch (UnauthorizedAccessException) { return null; } catch (IOException) { return null; }
        }
        static int RegInt(RegistryHive h, string p, string n, int def) { var v = Reg(h, p, n); try { return v == null ? def : Convert.ToInt32(v, CultureInfo.InvariantCulture); } catch (FormatException) { return def; } catch (InvalidCastException) { return def; } catch (OverflowException) { return def; } }
        static bool KeyExists(string path) { try { using (var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)) using (var k = b.OpenSubKey(path)) return k != null; } catch (System.Security.SecurityException) { return false; } }
        const RegistryHive LM = RegistryHive.LocalMachine, CU = RegistryHive.CurrentUser;
        static List<Dictionary<string, object>> Wmi(string scope, string query)
        {
            var l = new List<Dictionary<string, object>>();
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher(scope, query)) { s.Options.Timeout = TimeSpan.FromSeconds(10); foreach (System.Management.ManagementBaseObject o in s.Get()) using (o) { var d = new Dictionary<string, object>(); foreach (var p in o.Properties) d[p.Name] = p.Value; l.Add(d); } }
            }
            catch (System.Management.ManagementException) { return null; } catch (UnauthorizedAccessException) { return null; } catch (System.Runtime.InteropServices.COMException) { return null; }
            return l;
        }
        static string Run(string exe, string args, int ms)
        {
            try
            {
                var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, exe), args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = Encoding.Default };
                using (var p = Process.Start(psi)) { var t = p.StandardOutput.ReadToEndAsync(); if (!p.WaitForExit(ms)) { try { p.Kill(); } catch (InvalidOperationException) { } return null; } return t.Result; }
            }
            catch (System.ComponentModel.Win32Exception) { return null; } catch (InvalidOperationException) { return null; }
        }
        static bool TcpOpen(IPAddress a, int port, int ms)
        {
            using (var c = new TcpClient(a.AddressFamily)) { try { return c.ConnectAsync(a, port).Wait(ms) && c.Connected; } catch (AggregateException) { return false; } }
        }
        static IPAddress Gateway(Monitor m) { string g; lock (m.L) g = m.Gateway; IPAddress a; return IPAddress.TryParse((g ?? "").Trim(), out a) ? a : null; }
        static bool Company(Monitor m) { return Lab.Company(m); }

        // ================================================================== THIS PC
        static List<string[]> Antivirus(Monitor m, List<string> s)
        {
            var av = Wmi(@"root\SecurityCenter2", "SELECT displayName, productState FROM AntiVirusProduct");
            if (av == null) return One(3, "Antivirus", "Windows doesn't say here (Security Center isn't on servers)", "", "windowsdefender:");
            if (av.Count == 0) return One(2, "Antivirus", "none registered", "Turn on Microsoft Defender Antivirus in Windows Security.", "windowsdefender:");
            var rows = new List<string[]>();
            foreach (var a in av)
            {
                long st = Convert.ToInt64(a["productState"] ?? 0, CultureInfo.InvariantCulture); bool on = ((st >> 12) & 0xF) == 1, fresh = ((st >> 4) & 0xF) == 0;
                rows.Add(R(!on ? (av.Count > 1 ? 3 : 2) : !fresh ? 1 : 0, "Antivirus", a["displayName"] + (on ? " · on" : " · off") + (fresh ? " · up to date" : " · OUT OF DATE"), !on && av.Count > 1 ? "Off because another antivirus is on - that's normal." : !fresh ? "Update it in Windows Security (or your antivirus)." : "", "windowsdefender:"));
            }
            return rows;
        }
        static List<string[]> Firewall(Monitor m, List<string> s)
        {
            const string b = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\", pb = @"SOFTWARE\Policies\Microsoft\WindowsFirewall\";
            var off = new List<string>();
            foreach (var p in new[] { new[] { "DomainProfile", "work (domain)" }, new[] { "StandardProfile", "private" }, new[] { "PublicProfile", "public" } })
            {
                int pol = RegInt(LM, pb + p[0], "EnableFirewall", -1), own = RegInt(LM, b + p[0], "EnableFirewall", -1);
                if ((pol >= 0 ? pol : own) == 0) off.Add(p[1]);
            }
            int known = 0; foreach (var p2 in new[] { "DomainProfile", "StandardProfile", "PublicProfile" }) if (RegInt(LM, pb + p2, "EnableFirewall", -1) >= 0 || RegInt(LM, b + p2, "EnableFirewall", -1) >= 0) known++;
            if (known == 0) return One(3, "Firewall", "Windows didn't say (the firewall settings couldn't be read here)", "Windows Security › Firewall & network protection shows it.", "windowsdefender://network");
            var third = Wmi(@"root\SecurityCenter2", "SELECT displayName, productState FROM FirewallProduct");
            string other = third != null ? string.Join(", ", third.Where(x => ((Convert.ToInt64(x["productState"] ?? 0, CultureInfo.InvariantCulture) >> 12) & 0xF) == 1).Select(x => x["displayName"])) : "";   /* registered AND on */
            if (off.Count == 0) return One(0, "Firewall", "Windows Firewall on for every network type", "", "windowsdefender://network");
            return One(other != "" ? 3 : off.Contains("public") ? 2 : 1, "Firewall", "Windows Firewall OFF for " + string.Join(", ", off) + " networks" + (other != "" ? " · " + other + " is on instead" : ""), other != "" ? "Another firewall is in charge - fine if it's on." : "Turn it back on in Windows Security › Firewall & network protection.", "windowsdefender://network");
        }
        static List<string[]> BitLocker(Monitor m, List<string> s)
        {
            int v = -1;
            try
            {
                var t = Type.GetTypeFromProgID("Shell.Application"); object sh = Activator.CreateInstance(t);
                object ns = t.InvokeMember("NameSpace", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { Path.GetPathRoot(Environment.SystemDirectory) }, CultureInfo.InvariantCulture);
                object self = ns.GetType().InvokeMember("Self", System.Reflection.BindingFlags.GetProperty, null, ns, null, CultureInfo.InvariantCulture);
                object p = self.GetType().InvokeMember("ExtendedProperty", System.Reflection.BindingFlags.InvokeMethod, null, self, new object[] { "System.Volume.BitLockerProtection" }, CultureInfo.InvariantCulture);
                if (p != null) v = Convert.ToInt32(p, CultureInfo.InvariantCulture);
            }
            catch (Exception e) { if (!(e is System.Runtime.InteropServices.COMException || e is System.Reflection.TargetInvocationException || e is ArgumentException || e is NullReferenceException || e is FormatException || e is InvalidCastException || e is MissingMethodException || e is NotImplementedException)) throw; }
            string drive = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
            if (v == 1 || v == 3 || v == 5) return One(0, "Drive encryption", "BitLocker on for " + drive + (v == 3 ? " (encrypting now)" : ""), "", null);
            if (v == 2 || v == 4 || v == 6) return One(1, "Drive encryption", "BitLocker OFF for " + drive, "A lost or stolen laptop gives away everything on it. Home editions may have Device encryption instead (Settings › Privacy & security).", "ms-settings:deviceencryption");
            return One(3, "Drive encryption", "Windows doesn't say for " + drive, "Settings › Privacy & security › Device encryption, or BitLocker in Control Panel.", "ms-settings:deviceencryption");
        }
        static List<string[]> SecureBoot(Monitor m, List<string> s)
        {
            int v = RegInt(LM, @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled", -1);
            return v == 1 ? One(0, "Secure Boot", "on", "", null) : v == 0 ? One(1, "Secure Boot", "off", "Secure Boot stops tampered start-up code. It's turned on in the PC's firmware (UEFI) settings - ask IT on a company PC.", null) : One(3, "Secure Boot", "not supported, or an older BIOS start-up", "", null);
        }
        static List<string[]> Uac(Monitor m, List<string> s)
        {
            const string p = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
            int lua = RegInt(LM, p, "EnableLUA", 1), cp = RegInt(LM, p, "ConsentPromptBehaviorAdmin", 5), sd = RegInt(LM, p, "PromptOnSecureDesktop", 1);
            if (lua == 0) return One(2, "User Account Control", "OFF", "Every program runs with full rights without asking. Turn it back on.", "UserAccountControlSettings.exe");
            if (cp == 0) return One(2, "User Account Control", "never asks (elevates silently)", "Programs can take admin rights without you seeing it.", "UserAccountControlSettings.exe");
            return One(sd == 0 ? 1 : 0, "User Account Control", sd == 0 ? "on, but the prompt isn't on the secure desktop" : "on", sd == 0 ? "Other programs could click the prompt for you; the default (dimmed screen) is safer." : "", "UserAccountControlSettings.exe");
        }
        static List<string[]> Updates(Monitor m, List<string> s)
        {
            var hf = Wmi(@"root\cimv2", "SELECT InstalledOn FROM Win32_QuickFixEngineering");
            DateTime last = DateTime.MinValue;
            if (hf != null) foreach (var h in hf) { DateTime d; var t = Convert.ToString(h["InstalledOn"], CultureInfo.InvariantCulture); if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out d) || DateTime.TryParse(t, CultureInfo.CurrentCulture, DateTimeStyles.None, out d)) if (d > last && d <= DateTime.Now.AddDays(1)) last = d; }
            bool pending = KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") || KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            int days = last == DateTime.MinValue ? -1 : (int)(DateTime.Now - last).TotalDays;
            int lv = days < 0 ? 3 : days > 60 ? 2 : days > 35 ? 1 : 0; if (pending && lv == 0) lv = 1;
            return One(lv, "Windows Update", (days < 0 ? "last update unknown" : "last update installed " + days + " days ago (" + last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ")") + (pending ? " · RESTART NEEDED to finish updating" : ""), lv >= 1 ? "Security fixes come every month; a restart is what actually installs them." : "", "ms-settings:windowsupdate");
        }
        // end of security fixes, from Microsoft's lifecycle pages as of this release: build -> { name, Home/Pro end, Enterprise/Education end }
        static readonly string[][] Life = {
            new[] { "19045", "Windows 10 22H2", "2025-10-14", "2025-10-14" }, new[] { "22000", "Windows 11 21H2", "2023-10-10", "2024-10-08" }, new[] { "22621", "Windows 11 22H2", "2024-10-08", "2025-10-14" },
            new[] { "22631", "Windows 11 23H2", "2025-11-11", "2026-11-10" }, new[] { "26100", "Windows 11 24H2", "2026-10-13", "2027-10-12" }, new[] { "26200", "Windows 11 25H2", "2027-10-12", "2028-10-10" } };
        public static List<string[]> Version(Monitor m, List<string> s)
        {
            const string p = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string build = Convert.ToString(Reg(LM, p, "CurrentBuild"), CultureInfo.InvariantCulture) ?? "", ed = Convert.ToString(Reg(LM, p, "EditionID"), CultureInfo.InvariantCulture) ?? "", dv = Convert.ToString(Reg(LM, p, "DisplayVersion"), CultureInfo.InvariantCulture) ?? "";
            return VersionVerdict(build, ed, dv, DateTime.Today);
        }
        public static List<string[]> VersionVerdict(string build, string edition, string dv, DateTime today)
        {
            bool ent = Regex.IsMatch(edition ?? "", "Enterprise|Education", RegexOptions.IgnoreCase), ltsc = (edition ?? "").IndexOf("LTSC", StringComparison.OrdinalIgnoreCase) >= 0 || (edition ?? "").EndsWith("S", StringComparison.Ordinal) && ent;
            var row = Life.FirstOrDefault(r => r[0] == build);
            if (ltsc) return One(3, "Windows version", "Windows LTSC build " + build, "Long-term servicing editions have their own, longer support dates.", "ms-settings:windowsupdate");
            if (row == null)
            {
                int b; if (!int.TryParse(build, out b)) b = 0;
                if (b > 26200) return One(3, "Windows version", "build " + build + " - newer than this release knows", "Its support dates aren't built in yet - Windows Update › Windows release information shows them.", "ms-settings:windowsupdate");
                return One(b > 0 && b < 19045 ? 2 : 3, "Windows version", (b > 0 ? "build " + build : "unknown build") + (dv != "" ? " (" + dv + ")" : ""), b > 0 && b < 19045 ? "No longer gets security fixes - update to a supported version." : "", "ms-settings:windowsupdate");
            }
            var end = DateTime.Parse(ent ? row[3] : row[2], CultureInfo.InvariantCulture); int left = (int)(end - today).TotalDays;
            return One(left < 0 ? 2 : left < 90 ? 1 : 0, "Windows version", row[1] + (ent ? " (Enterprise / Education)" : "") + (left < 0 ? " · security fixes ENDED " + end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : " · fixes until " + end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                left < 0 ? (row[1].StartsWith("Windows 10", StringComparison.Ordinal) ? "Upgrade to Windows 11, or enrol in Extended Security Updates if you have them." : "Update to the newest Windows 11 version in Windows Update.") : left < 90 ? "Plan the feature update before then." : "", "ms-settings:windowsupdate");
        }
        static List<string[]> Smb1(Monitor m, List<string> s)
        {
            int cl = RegInt(LM, @"SYSTEM\CurrentControlSet\Services\mrxsmb10", "Start", -1), sv = RegInt(LM, @"SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters", "SMB1", -1);
            bool client = cl >= 0 && cl != 4, server = sv == 1;
            if (!client && !server) return One(0, "SMBv1 (WannaCry's protocol)", "off", "", null);
            return One(2, "SMBv1 (WannaCry's protocol)", "ON" + (client ? " (client)" : "") + (server ? " (server)" : ""), "SMBv1 is 30 years old and was how WannaCry spread. Turn off \"SMB 1.0/CIFS File Sharing Support\" unless an old NAS or printer needs it.", "optionalfeatures.exe");
        }
        static List<string[]> SmbSign(Monitor m, List<string> s)
        {
            int c = RegInt(LM, @"SYSTEM\CurrentControlSet\Services\LanmanWorkstation\Parameters", "RequireSecuritySignature", 0), v = RegInt(LM, @"SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters", "RequireSecuritySignature", 0);
            return One(c == 1 ? 0 : 3, "SMB signing", (c == 1 ? "required when this PC opens shares" : "not required when this PC opens shares") + (v == 1 ? " · required for its own shares" : ""), c == 1 ? "" : "Signing stops someone on the network from changing file-share traffic. Windows 11 24H2 requires it by default; on older PCs it's set by policy (IT).", null);
        }
        static List<string[]> Rdp(Monitor m, List<string> s)
        {
            int deny = RegInt(LM, @"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections", 1), nla = RegInt(LM, @"SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp", "UserAuthentication", 1);
            if (deny == 1) return One(0, "Remote Desktop", "off", "", "ms-settings:remotedesktop");
            return One(nla == 1 ? (Company(m) ? 3 : 1) : 2, "Remote Desktop", "ON" + (nla == 1 ? " · sign-in required first (NLA)" : " · WITHOUT network-level sign-in"), nla == 1 ? (Company(m) ? "Usually set by IT on company PCs." : "Fine if you use it - turn it off if you don't; RDP is a favourite target.") : "Without NLA anyone can reach the Windows sign-in screen over the network. Turn on \"Require devices to use Network Level Authentication\".", "ms-settings:remotedesktop");
        }
        static List<string[]> RemoteAssist(Monitor m, List<string> s)
        {
            int v = RegInt(LM, @"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp", 0);
            return One(v == 1 ? 3 : 0, "Remote Assistance", v == 1 ? "invitations allowed" : "off", v == 1 ? "Only matters if someone gets an invitation file from you. Off if you never use it." : "", "SystemPropertiesRemote.exe");
        }
        static List<string[]> NameServices(Monitor m, List<string> s)
        {
            bool llmnr = RegInt(LM, @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", "EnableMulticast", 1) != 0, mdns = RegInt(LM, @"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters", "EnableMDNS", 1) != 0;
            NetworkInterface nic; lock (m.L) nic = m.Primary; bool nb = true;
            if (nic != null) { int o = RegInt(LM, @"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces\Tcpip_" + nic.Id, "NetbiosOptions", 0); nb = o != 2; }
            var on = new List<string>(); if (llmnr) on.Add("LLMNR"); if (nb) on.Add("NetBIOS"); if (mdns) on.Add("mDNS");
            return One(on.Count == 0 ? 0 : 3, "Name broadcasts", on.Count == 0 ? "LLMNR, NetBIOS and mDNS all off" : string.Join(", ", on) + " on · this PC answers to \"" + Environment.MachineName + "\" on the local network", on.Count == 0 ? "" : "Handy at home; on public Wi-Fi an attacker can answer these name questions falsely and catch sign-in attempts. Companies often turn LLMNR and NetBIOS off by policy.", "ncpa.cpl");
        }
        static List<string[]> Wpad(Monitor m, List<string> s)
        {
            var v = Reg(CU, @"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections", "DefaultConnectionSettings") as byte[];
            bool auto = v != null && v.Length > 8 && (v[8] & 0x08) != 0;
            return One(auto ? 3 : 0, "Automatic proxy detection (WPAD)", auto ? "on - Windows looks for a proxy script on every network" : "off", auto ? "On company networks this is how the proxy is found. On public Wi-Fi a false WPAD answer could route your web traffic through someone else - turn it off if you're not on a company network." : "", "ms-settings:network-proxy");
        }
        static readonly int[] WinPorts = { 135, 139, 445, 5040, 5357, 7680, 2869, 5353, 5355 };
        static readonly Dictionary<int, string> Servers = new Dictionary<int, string> { { 21, "FTP" }, { 22, "SSH" }, { 23, "Telnet" }, { 80, "web" }, { 443, "web" }, { 3389, "Remote Desktop" }, { 5985, "WinRM" }, { 5986, "WinRM" }, { 5900, "VNC" }, { 3306, "MySQL" }, { 1433, "SQL Server" }, { 8080, "web" }, { 8443, "web" }, { 5432, "PostgreSQL" }, { 6379, "Redis" }, { 27017, "MongoDB" } };
        static List<string[]> OpenPorts(Monitor m, List<string> s)
        {
            IPEndPoint[] ls; try { ls = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners(); } catch (NetworkInformationException) { return One(3, "Open to the network", "Windows didn't say", "", null); }
            var open = ls.Where(e => !IPAddress.IsLoopback(e.Address) && !WinPorts.Contains(e.Port) && e.Port < 49152).Select(e => e.Port).Distinct().OrderBy(p => p).ToList();
            if (open.Count == 0) return One(0, "Open to the network", "nothing beyond Windows' own file sharing and services", "", null);
            var named = open.Select(p => { string n; return Servers.TryGetValue(p, out n) ? p + " (" + n + ")" : I(p); }).ToList();
            bool server = open.Any(p => Servers.ContainsKey(p));
            return One(server ? 1 : 3, "Open to the network (TCP)", string.Join(", ", named.Take(20)) + (open.Count > 20 ? " …" : ""), (server ? "A server runs on this PC and listens on every network. " : "") + "The firewall may still block it from outside - Toolbox › What's listening shows which program each one is.", "windowsdefender://network");
        }
        static readonly string[] Interceptors = { "fiddler", "charles", "mitmproxy", "burp", "portswigger", "zscaler", "netskope", "fortinet", "fortigate", "palo alto", "blue coat", "bluecoat", "sophos", "kaspersky", "avast", "avg ", "eset", "bitdefender", "cisco umbrella", "forcepoint", "websense", "superfish" };
        static List<string[]> Roots(Monitor m, List<string> s)
        {
            var mine = new List<X509Certificate2>(); var lm = new HashSet<string>();
            try
            {
                using (var st = new X509Store(StoreName.Root, StoreLocation.LocalMachine)) { st.Open(OpenFlags.ReadOnly); foreach (var c in st.Certificates) { lm.Add(c.Thumbprint); c.Dispose(); } }
                using (var st = new X509Store(StoreName.Root, StoreLocation.CurrentUser)) { st.Open(OpenFlags.ReadOnly); foreach (var c in st.Certificates) mine.Add(c); }
            }
            catch (System.Security.Cryptography.CryptographicException) { return One(3, "Trusted root certificates", "couldn't read the stores", "", "certmgr.msc"); }
            var extra = mine.Where(c => !lm.Contains(c.Thumbprint)).ToList();
            var hits = extra.Where(c => Interceptors.Any(n => c.Subject.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            var res = extra.Count == 0 ? One(0, "Trusted root certificates", "only the ones Windows and your company put there", "", "certmgr.msc")
                : One(hits.Count > 0 ? 1 : 3, "Trusted root certificates", extra.Count + " added just for you: " + string.Join("; ", extra.Take(5).Select(c => c.GetNameInfo(X509NameType.SimpleName, false))), hits.Count > 0 ? "One belongs to a tool that reads your encrypted web traffic (a debugging proxy or an antivirus's web shield). Fine if you installed it on purpose; otherwise remove it in certmgr." : "Added roots let whoever owns them make certificates your PC trusts. Remove any you don't recognise.", "certmgr.msc");
            foreach (var c in mine) c.Dispose();
            return res;
        }
        static readonly string[] BigSites = { "microsoft.com", "windowsupdate.com", "office.com", "office365.com", "live.com", "google.com", "googleapis.com", "apple.com", "amazon.com", "paypal.com", "facebook.com", "github.com", "bank" };
        static List<string[]> Hosts(Monitor m, List<string> s)
        {
            string f = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts"); string[] lines;
            try { lines = File.ReadAllLines(f).Take(5000).ToArray(); } catch (IOException) { return One(3, "Hosts file", "couldn't read it", "", null); } catch (UnauthorizedAccessException) { return One(3, "Hosts file", "couldn't read it", "", null); }
            var redirect = new List<string>(); int blocked = 0;
            foreach (var l in lines)
            {
                var t = Regex.Replace(l, "#.*", "").Trim(); if (t == "") continue; var p = t.Split((char[])null, StringSplitOptions.RemoveEmptyEntries); if (p.Length < 2) continue;
                bool sink = p[0] == "0.0.0.0" || p[0] == "127.0.0.1" || p[0] == "::1" || p[0] == "::";
                foreach (var h in p.Skip(1)) { if (sink) blocked++; else if (BigSites.Any(b => h.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0)) redirect.Add(h + " -> " + p[0]); }
            }
            if (redirect.Count > 0) return One(2, "Hosts file", "sends " + string.Join(", ", redirect.Take(5)) + " elsewhere", "Malware does this to fake sign-in pages or block updates. Unless you added it for testing, remove those lines (Notepad as administrator).", null);
            return One(0, "Hosts file", blocked > 0 ? blocked + " names blocked (pointed at nothing), nothing big redirected" : "clean", blocked > 0 ? "Blocking lists (ad blockers) do this - fine if you added it." : "", null);
        }
        static List<string[]> Proxy(Monitor m, List<string> s)
        {
            const string p = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
            bool machine = RegInt(LM, @"SOFTWARE\Policies\Microsoft\Windows\CurrentVersion\Internet Settings", "ProxySettingsPerUser", 1) == 0;   /* one proxy for the whole machine */
            var hv = machine ? LM : CU; string pp = machine ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings" : p;
            int en = RegInt(hv, pp, "ProxyEnable", 0); string srv = Convert.ToString(Reg(hv, pp, "ProxyServer"), CultureInfo.InvariantCulture) ?? "", pac = Convert.ToString(Reg(hv, pp, "AutoConfigURL"), CultureInfo.InvariantCulture) ?? "";
            bool policy = Reg(LM, @"SOFTWARE\Policies\Microsoft\Windows\CurrentVersion\Internet Settings", "ProxySettingsPerUser") != null || Reg(CU, @"Software\Policies\Microsoft\Internet Explorer\Control Panel", "Proxy") != null;
            if (en == 0 && pac == "") return One(0, "Proxy", "none - straight to the internet", "", "ms-settings:network-proxy");
            string what = (en != 0 ? "proxy " + srv : "") + (pac != "" ? (en != 0 ? " · " : "") + "script " + pac : "");
            bool local = Regex.IsMatch(srv + " " + pac, @"127\.0\.0\.1|localhost|\[::1\]");
            return One(policy || Company(m) ? 3 : local ? 1 : 1, "Proxy", what, policy || Company(m) ? "Set by your company - expected." : local ? "A program on this PC sees your web traffic first (a debugging tool, an antivirus, or adware). Fine if you know which." : "Your web traffic goes through this server. If you didn't set it, remove it.", "ms-settings:network-proxy");
        }
        static List<string[]> Startup(Monitor m, List<string> s)
        {
            var items = new List<Tuple<string, string>>();
            foreach (var h in new[] { CU, LM })
                try { using (var b = RegistryKey.OpenBaseKey(h, RegistryView.Registry64)) using (var k = b.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) if (k != null) foreach (var n in k.GetValueNames()) items.Add(Tuple.Create(n, Convert.ToString(k.GetValue(n), CultureInfo.InvariantCulture) ?? "")); }
                catch (System.Security.SecurityException) { }
            foreach (var d in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) })
                try { if (Directory.Exists(d)) foreach (var f in Directory.GetFiles(d).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase))) items.Add(Tuple.Create(Path.GetFileName(f), f)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            var unsigned = new List<string>();
            foreach (var it in items)
            {
                var mm = Regex.Match(it.Item2, "^\\s*\"([^\"]+)\"|^\\s*(\\S+\\.exe)", RegexOptions.IgnoreCase); string exe = mm.Success ? (mm.Groups[1].Success ? mm.Groups[1].Value : mm.Groups[2].Value) : it.Item2;
                exe = Environment.ExpandEnvironmentVariables(exe); if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(exe)) continue;
                if (string.Equals(Path.GetFileName(exe), "AdeeboMeter.exe", StringComparison.OrdinalIgnoreCase)) continue;   /* this app: unsigned, and you chose to start it */
                try { using (X509Certificate.CreateFromSignedFile(exe)) { } } catch (System.Security.Cryptography.CryptographicException) { unsigned.Add(it.Item1 + " (" + Path.GetFileName(exe) + ")"); }
            }
            return One(unsigned.Count == 0 ? 0 : 1, "Programs that start with Windows", items.Count + " start automatically" + (unsigned.Count > 0 ? " · " + unsigned.Count + " without a publisher's signature: " + string.Join(", ", unsigned.Take(5)) : " · all .exe programs signed"), unsigned.Count > 0 ? "Unsigned isn't proof of harm (small tools often aren't signed), but malware usually isn't signed either. Turn off any you don't recognise in Startup apps." : "", "ms-settings:startupapps");
        }
        static List<string[]> Tasks(Monitor m, List<string> s)
        {
            var o = Run("schtasks.exe", "/query /fo csv /v", 25000);
            if (o == null) return One(3, "Scheduled tasks", "couldn't list them", "", "taskschd.msc");
            var bad = new List<string>(); int n = 0;
            foreach (var l in o.Split('\n'))
            {
                var c = Regex.Matches(l, "\"([^\"]*)\"").Cast<Match>().Select(x => x.Groups[1].Value).ToList(); if (c.Count < 9 || c[1] == "TaskName") continue; n++;
                string run = c[8].ToLowerInvariant();
                if (Regex.IsMatch(run, @"\\(temp|downloads)\\|\\appdata\\roaming\\[^\\]+\.exe|\\users\\public\\") && !bad.Contains(c[1])) bad.Add(c[1]);
            }
            return One(bad.Count == 0 ? 0 : 1, "Scheduled tasks", bad.Count == 0 ? "none run from Temp, Downloads or other folders anyone can write to" : bad.Count + " run from a folder anyone can write to: " + string.Join(", ", bad.Take(4)), bad.Count > 0 ? "A common trick to keep malware running. Check them in Task Scheduler; delete any you don't recognise." : "", "taskschd.msc");
        }
        static List<string[]> Lsa(Monitor m, List<string> s)
        {
            int ppl = RegInt(LM, @"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL", 0);
            var dg = Wmi(@"root\Microsoft\Windows\DeviceGuard", "SELECT SecurityServicesRunning FROM Win32_DeviceGuard");
            var arr = dg != null && dg.Count > 0 ? dg[0]["SecurityServicesRunning"] as Array : null;   /* WMI gives uint32[]; read any number type */
            var run = arr == null ? Array.Empty<int>() : arr.Cast<object>().Select(x => Convert.ToInt32(x, CultureInfo.InvariantCulture)).ToArray();
            bool cg = run.Contains(1);
            return One(ppl >= 1 || cg ? 0 : 1, "Sign-in secrets protection", (ppl >= 1 ? "LSA protection on" : "LSA protection off") + " · " + (cg ? "Credential Guard on" : "Credential Guard off"), ppl >= 1 || cg ? "" : "These stop password-stealing tools from reading sign-in secrets from memory. Windows Security › Device security › Core isolation (LSA protection).", "windowsdefender://devicesecurity");
        }
        static List<string[]> Hvci(Monitor m, List<string> s)
        {
            int v = RegInt(LM, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0);
            return One(v == 1 ? 0 : 1, "Memory integrity", v == 1 ? "on" : "off", v == 1 ? "" : "Stops malicious drivers loading. Windows Security › Device security › Core isolation; an old driver can block it - Windows lists which.", "windowsdefender://devicesecurity");
        }
        static List<string[]> SmartScreen(Monitor m, List<string> s)
        {
            int pol = RegInt(LM, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", -1); string own = Convert.ToString(Reg(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled"), CultureInfo.InvariantCulture) ?? "";
            bool off = pol == 0 || (pol < 0 && own.Equals("Off", StringComparison.OrdinalIgnoreCase));
            return One(off ? 1 : 0, "SmartScreen", off ? "off for downloaded programs" : "on", off ? "SmartScreen warns before you run a program known to be bad or never seen before." : "", "windowsdefender://appbrowser");
        }
        static List<string[]> AutoPlay(Monitor m, List<string> s)
        {
            int dis = RegInt(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", 0), nd = RegInt(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 0x91);
            bool off = dis == 1 || (nd & 0x04) != 0 && (nd & 0x10) != 0 && nd == 0xFF;
            return One(0, "AutoPlay", off ? "off" : "on - Windows offers what to do when a USB drive goes in", off ? "" : "Windows no longer runs programs from USB drives by itself; AutoPlay only asks. Turning it off removes the temptation.", "ms-settings:autoplay");
        }
        static List<string[]> AdminDaily(Monitor m, List<string> s)
        {
            int t = Native.ElevationType();
            bool admin; using (var id = WindowsIdentity.GetCurrent()) admin = t == 3 || t == 2 || new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            return One(admin ? (Company(m) ? 3 : 1) : 0, "Everyday account", admin ? "you sign in as an administrator" : "a standard account", admin ? "Malware you run by mistake gets admin rights after one click on Yes. A standard account for every day, and admin only when needed, is safer." : "", "netplwiz.exe");
        }
        static List<string[]> ScreenLock(Monitor m, List<string> s)
        {
            int pol = RegInt(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "InactivityTimeoutSecs", 0);
            int act = Convert.ToInt32(Convert.ToString(Reg(CU, @"Control Panel\Desktop", "ScreenSaveActive"), CultureInfo.InvariantCulture) == "1" ? 1 : 0), sec = Convert.ToString(Reg(CU, @"Control Panel\Desktop", "ScreenSaverIsSecure"), CultureInfo.InvariantCulture) == "1" ? 1 : 0;
            int to; if (!int.TryParse(Convert.ToString(Reg(CU, @"Control Panel\Desktop", "ScreenSaveTimeOut"), CultureInfo.InvariantCulture), out to)) to = 0;
            if (pol > 0) return One(pol <= 900 ? 0 : 1, "Screen lock", "locks after " + pol / 60 + " min idle (company policy)", "", "ms-settings:lockscreen");
            if (act == 1 && sec == 1 && to > 0) return One(to <= 900 ? 0 : 1, "Screen lock", "locks after " + to / 60 + " min idle", to > 900 ? "15 minutes or less is the usual advice." : "", "ms-settings:lockscreen");
            return One(3, "Screen lock", "no idle lock set here", "Windows may still lock on sleep (Settings › Accounts › Sign-in options › \"If you've been away\"). Win+L locks it when you walk away.", "ms-settings:signinoptions");
        }
        static List<string[]> OpenWifi(Monitor m, List<string> s)
        {
            var o = Run("netsh.exe", "wlan show profiles", 10000);
            if (o == null || !o.Contains(":")) return One(3, "Saved Wi-Fi networks", "no Wi-Fi here, or Windows didn't list them", "", "ms-settings:network-wifisettings");
            var names = Regex.Matches(o, @"(?m)^\s+[^:\r\n]*Profile\s*:\s*(.+?)\s*$").Cast<Match>().Select(x => x.Groups[1].Value).Where(x => x.Length > 0 && x.Length < 64 && !x.Contains("\"")).Take(40).ToList();
            var open = new List<string>();
            foreach (var n in names)
            {
                var p = Run("netsh.exe", "wlan show profile name=\"" + n + "\"", 5000); if (p == null) continue;
                if (Regex.IsMatch(p, @"Authentication\s*:\s*Open", RegexOptions.IgnoreCase) && Regex.IsMatch(p, @"Connection mode\s*:\s*Connect automatically", RegexOptions.IgnoreCase)) open.Add(n);
            }
            return One(open.Count == 0 ? 0 : 1, "Saved Wi-Fi networks", names.Count + " saved" + (open.Count > 0 ? " · " + open.Count + " OPEN and join by themselves: " + string.Join(", ", open.Take(5)) : " · none open and auto-joining"), open.Count > 0 ? "Anyone can set up a hotspot with the same name and your PC will join it. Untick \"Connect automatically\" or forget them." : "", "ms-settings:network-wifisettings");
        }
        static List<string[]> PsPolicy(Monitor m, List<string> s)
        {
            string p = Convert.ToString(Reg(LM, @"SOFTWARE\Policies\Microsoft\Windows\PowerShell", "ExecutionPolicy") ?? Reg(CU, @"Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell", "ExecutionPolicy") ?? Reg(LM, @"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell", "ExecutionPolicy"), CultureInfo.InvariantCulture) ?? "Restricted (Windows' default)";
            bool loose = p.Equals("Unrestricted", StringComparison.OrdinalIgnoreCase) || p.Equals("Bypass", StringComparison.OrdinalIgnoreCase);
            return One(loose ? 1 : 0, "PowerShell script policy", p, loose ? "Any script runs without a warning. RemoteSigned is the common middle ground. (This policy is a seat belt, not a security wall.)" : "", null);
        }

        // ================================================================== YOUR NETWORK
        static List<string[]> WifiSec(Monitor m, List<string> s)
        {
            var o = Run("netsh.exe", "wlan show interfaces", 8000);
            var a = o == null ? null : Regex.Match(o, @"Authentication\s*:\s*(.+?)\s*$", RegexOptions.Multiline);
            if (a == null || !a.Success) return One(3, "This Wi-Fi's security", "not on Wi-Fi, or Windows doesn't show it", "", null);
            string v = a.Groups[1].Value; bool open = Regex.IsMatch(v, "^Open|None", RegexOptions.IgnoreCase), wep = v.IndexOf("WEP", StringComparison.OrdinalIgnoreCase) >= 0, wpa3 = v.IndexOf("WPA3", StringComparison.OrdinalIgnoreCase) >= 0;
            return One(open || wep ? 2 : wpa3 ? 0 : (v.IndexOf("WPA2", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1), "This Wi-Fi's security", v, open ? "An open network: anyone nearby can see unencrypted traffic. Websites with https are still private; use a VPN for the rest." : wep ? "WEP can be broken in minutes - change the router to WPA2 or WPA3." : wpa3 ? "" : v.IndexOf("WPA2", StringComparison.OrdinalIgnoreCase) >= 0 ? "Good. WPA3 is better if your router and devices support it." : "Old WPA (TKIP) is weak - use WPA2 or WPA3.", "ms-settings:network-wifi");
        }
        static List<string[]> DnsHijack(Monitor m, List<string> s)
        {
            string dns; lock (m.L) dns = m.DnsServer; IPAddress srv;
            if (!IPAddress.TryParse((dns ?? "").Split(',')[0].Trim(), out srv)) return One(3, "DNS honesty", "no DNS server to ask", "", null);
            var rows = new List<string[]>();
            string fake = "adeebometer-check-" + Guid.NewGuid().ToString("N").Substring(0, 10) + ".com";
            var r = Toolkit.Query(srv, fake, 1, true);
            if (r == null) rows.Add(R(3, "DNS honesty", "your DNS server didn't answer", "", null));
            else { var ans = r.Rrs.Where(x => x.Section == 1 && x.Type == "A").Select(x => x.Data).ToList(); rows.Add(ans.Count > 0 ? R(1, "DNS honesty", "a name that doesn't exist got an answer (" + string.Join(", ", ans.Take(2)) + ")", "Your DNS server (often the ISP's) sends typos to its own page. Harmless-ish, but it means it rewrites answers - an encrypted DNS (Settings › Network › DNS) avoids it.", "ms-settings:network-status") : R(0, "DNS honesty", "names that don't exist get \"doesn't exist\" - no rewriting", "", null)); }
            if (!Online.On) { rows.Add(R(3, "DNS vs an encrypted resolver", "not compared - online lookups are off", "Turn on online lookups (Settings) to compare your DNS with Cloudflare's encrypted DNS.", null)); return rows; }
            string why; var w = Toolkit.Query(srv, "www.microsoft.com", 1, true); var d = Toolkit.QueryDoh(new Uri("https://cloudflare-dns.com/dns-query"), "www.microsoft.com", 1, out why);
            if (w == null || d == null) { rows.Add(R(3, "DNS vs an encrypted resolver", "one of them didn't answer", "", null)); return rows; }
            Func<Toolkit.Msg, List<string>> names = x => x.Rrs.Where(z => z.Section == 1 && z.Type == "CNAME").Select(z => z.Data.TrimEnd('.').ToLowerInvariant()).ToList();
            bool same = names(w).Intersect(names(d)).Any() || w.Rrs.Where(z => z.Type == "A").Select(z => z.Data).Intersect(d.Rrs.Where(z => z.Type == "A").Select(z => z.Data)).Any();
            rows.Add(same ? R(0, "DNS vs an encrypted resolver", "the same answer for www.microsoft.com", "", null) : R(1, "DNS vs an encrypted resolver", "different answers for www.microsoft.com", "Can be normal (company DNS, a content network picking a nearer server); suspicious if you're on home or public Wi-Fi.", null));
            return rows;
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5359", Justification = "only looks at who signed the certificate - nothing is sent over this connection")]
        static List<string[]> TlsIntercept(Monitor m, List<string> s)
        {
            if (!Online.On) return One(3, "Encrypted traffic inspected?", "not checked - online lookups are off", "Turn on online lookups (Settings) to check whether something on the way decrypts your https traffic.", null);
            string root = null;
            try
            {
                using (var c = new TcpClient())
                {
                    if (!c.ConnectAsync("www.microsoft.com", 443).Wait(5000)) return One(3, "Encrypted traffic inspected?", "couldn't reach www.microsoft.com", "", null);
                    using (var ssl = new SslStream(c.GetStream(), false, (o, cert, ch, e) => { if (ch != null && ch.ChainElements.Count > 0) root = ch.ChainElements[ch.ChainElements.Count - 1].Certificate.Subject; return true; }))
                        if (!System.Threading.Tasks.Task.Factory.StartNew(() => ssl.AuthenticateAsClient("www.microsoft.com")).Wait(8000)) return One(3, "Encrypted traffic inspected?", "no TLS answer in 8 s", "", null);
                }
            }
            catch (AggregateException) { return One(3, "Encrypted traffic inspected?", "couldn't check", "", null); }
            catch (SocketException) { return One(3, "Encrypted traffic inspected?", "couldn't reach www.microsoft.com", "", null); }
            if (root == null) return One(3, "Encrypted traffic inspected?", "couldn't see the chain", "", null);
            bool known = Regex.IsMatch(root, "Microsoft|DigiCert|Baltimore|GlobalSign|Sectigo|Comodo|USERTrust|ISRG|Let's Encrypt|Entrust|GoDaddy|Starfield|Amazon|Google Trust|IdenTrust|Certum|QuoVadis", RegexOptions.IgnoreCase);
            return known ? One(0, "Encrypted traffic inspected?", "no - www.microsoft.com's certificate comes from a public authority", "", null)
                : One(Company(m) ? 3 : 1, "Encrypted traffic inspected?", "YES - signed by \"" + Regex.Match(root, "CN=([^,]+)").Groups[1].Value + "\", not a public authority", Company(m) ? "Your company's web filter decrypts https to inspect it - common and expected at work." : "Something between you and the internet (an antivirus web shield, a proxy, or worse) decrypts your https traffic. Find out what installed that root certificate.", "certmgr.msc");
        }
        static List<string[]> ArpSpoof(Monitor m, List<string> s)
        {
            List<Arp.Entry> t; try { t = Arp.Table(); } catch (DllNotFoundException) { return One(3, "ARP (who's who on the LAN)", "couldn't read the table", "", null); }
            var gw = Gateway(m); string gws = gw != null ? gw.ToString() : "";
            var dup = t.Where(e => e.Dynamic).GroupBy(e => e.Mac).Where(g => g.Select(x => x.Ip).Distinct().Count() > 1).ToList();
            var gwDup = dup.FirstOrDefault(g => g.Any(x => x.Ip == gws));
            if (gwDup != null) return One(2, "ARP (who's who on the LAN)", "your router's MAC " + gwDup.Key + " is also claimed by " + string.Join(", ", gwDup.Select(x => x.Ip).Where(x => x != gws).Take(3)), "A classic sign of ARP spoofing (someone reading your traffic), or a router with several addresses. On a network you don't control, disconnect and use a VPN.", null);
            if (dup.Count > 0) return One(3, "ARP (who's who on the LAN)", dup.Count + " devices answer for more than one IP", "Usually a router, a virtual machine host or a printer with two addresses.", null);
            return One(0, "ARP (who's who on the LAN)", t.Count + " neighbours, one address each" + (gws != "" ? " · router " + gws + " = " + (t.Where(x => x.Ip == gws).Select(x => x.Mac).FirstOrDefault() ?? "not in the table yet") : ""), "", null);
        }
        static List<string[]> SecondDhcp(Monitor m, List<string> s)
        {
            NetworkInterface nic; lock (m.L) nic = m.Primary; if (nic == null) return One(3, "DHCP server", "no active adapter", "", null);
            var lease = nic.GetIPProperties().DhcpServerAddresses.Select(x => x.ToString()).ToList();
            if (lease.Count == 0) return One(3, "DHCP server", "this adapter has a fixed address", "", null);
            var opts = Lab.DhcpOptions(m, "yes"); var id = opts.FirstOrDefault(r => r[1].StartsWith("DHCP server", StringComparison.Ordinal));
            if (id == null) return One(3, "DHCP server", "your lease is from " + string.Join(", ", lease) + " · the server didn't answer a second question", "", null);
            bool same = lease.Contains(id[2].Trim());
            return One(same ? 0 : 1, "DHCP server", same ? "one server (" + id[2] + ") - the one you got your lease from" : "your lease came from " + string.Join(", ", lease) + " but " + id[2] + " answered now", same ? "" : "Two DHCP servers can hand out wrong addresses - an extra home router plugged in the wrong way, or a rogue server. Find out what " + id[2] + " is.", null);
        }
        static List<string[]> Upnp(Monitor m, List<string> s)
        {
            var gw = Gateway(m); if (gw == null || gw.AddressFamily != AddressFamily.InterNetwork) return One(3, "Router UPnP", "no router to ask", "", null);
            bool on = false;
            try
            {
                using (var u = new UdpClient(AddressFamily.InterNetwork))
                {
                    var q = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 1\r\nST: urn:schemas-upnp-org:device:InternetGatewayDevice:1\r\n\r\n");
                    u.Send(q, q.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900)); u.Client.ReceiveTimeout = 500; var until = DateTime.UtcNow.AddSeconds(2);
                    while (DateTime.UtcNow < until && !on) { try { var ep = new IPEndPoint(IPAddress.Any, 0); u.Receive(ref ep); if (ep.Address.Equals(gw)) on = true; } catch (SocketException) { } }
                }
            }
            catch (SocketException) { return One(3, "Router UPnP", "couldn't ask", "", null); }
            return One(on ? 1 : 0, "Router UPnP", on ? "ON - programs on your network can open ports in the router by themselves" : "the router didn't answer UPnP (off, or not offered)", on ? "Games and consoles like it; malware likes it too. If nothing needs it, turn UPnP off in the router's settings." : "", on ? "url:http://" + gw + "/" : null);
        }
        static List<string[]> RouterAdmin(Monitor m, List<string> s)
        {
            var gw = Gateway(m); if (gw == null) return One(3, "Router admin page", "no router", "", null);
            bool h80 = TcpOpen(gw, 80, 800), h443 = TcpOpen(gw, 443, 800);
            if (!h80 && !h443) return One(0, "Router admin page", "not on the usual web ports (or not reachable from here)", "", null);
            bool redirect = false;
            if (h80) try { var q = (HttpWebRequest)WebRequest.Create("http://" + gw + "/"); q.AllowAutoRedirect = false; q.Timeout = 3000; q.Proxy = null; using (var r = (HttpWebResponse)q.GetResponse()) redirect = ((int)r.StatusCode >= 300 && (int)r.StatusCode < 400 && (r.Headers["Location"] ?? "").StartsWith("https", StringComparison.OrdinalIgnoreCase)); } catch (WebException e) { var r = e.Response as HttpWebResponse; if (r != null) using (r) redirect = (int)r.StatusCode >= 300 && (int)r.StatusCode < 400 && (r.Headers["Location"] ?? "").StartsWith("https", StringComparison.OrdinalIgnoreCase); }
            if (!h80 || redirect) return One(0, "Router admin page", "https" + (redirect ? " (plain http sends you to https)" : ""), "", "url:https://" + gw + "/");
            return One(1, "Router admin page", "plain http (no encryption)" + (h443 ? " - https also offered" : ""), "The router's password crosses the network readable. Use https://" + gw + " if offered, and don't sign in to it over shared Wi-Fi.", "url:http://" + gw + "/");
        }
        static readonly int[][] OldPorts = { new[] { 21, 2 }, new[] { 23, 2 }, new[] { 7547, 1 }, new[] { 8080, 3 }, new[] { 22, 3 } };
        static List<string[]> RouterOld(Monitor m, List<string> s)
        {
            var gw = Gateway(m); if (gw == null) return One(3, "Router old services", "no router", "", null);
            var open = OldPorts.Where(p => TcpOpen(gw, p[0], 700)).ToList();
            var bad = open.Where(p => p[1] < 3).ToList();
            string names = string.Join(", ", open.Select(p => p[0] == 21 ? "FTP (21)" : p[0] == 23 ? "Telnet (23)" : p[0] == 7547 ? "TR-069 (7547)" : p[0] == 8080 ? "web (8080)" : "SSH (22)"));
            return One(bad.Count == 0 ? 0 : bad.Any(p => p[1] == 2) ? 2 : 1, "Router old services", open.Count == 0 ? "no FTP, Telnet or remote-management port open toward you" : names + " open on " + gw, bad.Any(p => p[0] == 23 || p[0] == 21) ? "Telnet and FTP send passwords in clear text - turn them off in the router." : bad.Count > 0 ? "TR-069 is the ISP's remote management - normal for ISP routers, but it shouldn't answer from your side." : "", null);
        }
        static List<string[]> PublicV6(Monitor m, List<string> s)
        {
            NetworkInterface nic; lock (m.L) nic = m.Primary; if (nic == null) return One(3, "Public IPv6 address", "no adapter", "", null);
            var g = nic.GetIPProperties().UnicastAddresses.Select(u => u.Address).Where(a => a.AddressFamily == AddressFamily.InterNetworkV6 && (a.GetAddressBytes()[0] & 0xE0) == 0x20).ToList();
            return One(3, "Public IPv6 address", g.Count == 0 ? "none - only local IPv6" : "yes (" + g.Count + ") - this PC has an address reachable from the internet", g.Count == 0 ? "" : "Normal. Your router's IPv6 firewall should block incoming connections, and Windows Firewall does too - keep both on.", null);
        }
        static List<string[]> DnsLeak(Monitor m, List<string> s)
        {
            bool vpn; string dns; lock (m.L) { vpn = m.VpnUp; dns = m.DnsServer; }
            if (!vpn) return One(3, "DNS while on VPN", "no VPN on right now", "Run the checkup again while connected to your VPN.", null);
            var vnics = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && Monitor.IsVpn(n)).ToList();
            var vdns = new HashSet<string>(vnics.SelectMany(n => n.GetIPProperties().DnsAddresses.Select(a => a.ToString())));
            string first = (dns ?? "").Split(',')[0].Trim();
            if (vdns.Count == 0) return One(3, "DNS while on VPN", "the VPN didn't set its own DNS servers", "Your names are asked of " + first + " - fine if that's reached through the VPN.", "ms-settings:network-vpn");
            bool inside = vdns.Contains(first);
            return One(inside ? 0 : 1, "DNS while on VPN", inside ? "names go to the VPN's DNS (" + first + ")" : "names go to " + first + ", not the VPN's DNS (" + string.Join(", ", vdns.Take(2)) + ")", inside ? "" : "A DNS leak: the network you're on can see which sites you visit. Split-tunnel VPNs do this on purpose for local names - check with IT.", "ms-settings:network-vpn");
        }
        static List<string[]> NewDevices(Monitor m, List<string> s)
        {
            List<string[]> j; lock (Devices.Joined) j = Devices.Joined.ToList();
            var week = j.Where(r => r.Length > 3).ToList();   /* the list holds this session's joins (stored as "ddd HH:mm") */
            return One(week.Count == 0 ? 0 : 3, "New devices", week.Count == 0 ? "none joined while AdeeboMeter was running this session" : week.Count + " joined this session: " + string.Join(", ", week.Take(6).Select(r => r[2])), week.Count == 0 ? "AdeeboMeter notices devices only while it runs." : "Recognise them? Time machine › What changed lists each with its time; Name of a device asks one what it's called.", null);
        }

        // ================================================================== YOUR SITES
        public static string HostOf(string site)
        {
            string t = (site ?? "").Trim(); if (t == "") return null; if (!t.Contains("://")) t = "https://" + t;
            Uri u; return Uri.TryCreate(t, UriKind.Absolute, out u) && Regex.IsMatch(u.Host, @"^[a-z0-9.-]+\.[a-z]{2,}$", RegexOptions.IgnoreCase) ? u.Host.ToLowerInvariant() : null;
        }
        static List<string[]> PerSite(List<string> sites, string title, Func<string, string[]> f)
        {
            var hosts = sites.Select(HostOf).Where(h => h != null).Distinct().Take(10).ToList();
            if (hosts.Count == 0) return One(3, title, "no sites to check", "Type your website or mail domain in the box above, or pin sites in Investigate › Check a destination.", null);
            return hosts.Select(f).ToList();
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5359", Justification = "reads the certificate to report on it - nothing is sent")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5397", Justification = "asking whether a site still accepts old TLS is the point of the check")]
        static X509Certificate2 SiteCert(string host, SslProtocols proto, out string err)
        {
            err = ""; X509Certificate2 got = null;
            try
            {
                using (var c = new TcpClient())
                {
                    if (!c.ConnectAsync(host, 443).Wait(5000)) { err = "no answer on 443"; return null; }
                    using (var ssl = new SslStream(c.GetStream(), false, (o, cert, ch, e) => { if (cert != null) got = new X509Certificate2(cert); return true; }))
                    {
                        var t = System.Threading.Tasks.Task.Factory.StartNew(() => ssl.AuthenticateAsClient(host, null, proto, false));
                        if (!t.Wait(8000)) { err = "no TLS answer"; return null; }
                    }
                }
            }
            catch (AggregateException e) { err = e.InnerException != null ? e.InnerException.Message : e.Message; return null; }
            catch (SocketException e) { err = e.Message; return null; }
            return got;
        }
        static List<string[]> CertExpiry(Monitor m, List<string> sites)
        {
            return PerSite(sites, "Certificate", h =>
            {
                string err; var c = SiteCert(h, SslProtocols.Tls12 | (SslProtocols)12288, out err);
                if (c == null) return R(3, "Certificate · " + h, "couldn't check - " + Draw.Trunc(err, 60), "", null);
                using (c) { int d = (int)(c.NotAfter - DateTime.Now).TotalDays; return R(d < 0 ? 2 : d < 14 ? 2 : d < 30 ? 1 : 0, "Certificate · " + h, d < 0 ? "EXPIRED " + c.NotAfter.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : d + " days left (" + c.NotAfter.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ")", d < 30 ? "Renew it before visitors get a warning." : "", null); }
            });
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5397", Justification = "asking whether a site still accepts old TLS is the point of the check")]
        static List<string[]> OldTls(Monitor m, List<string> sites)
        {
            return PerSite(sites, "Old TLS", h =>
            {
                string e1, e2; var a = SiteCert(h, SslProtocols.Tls, out e1); var b = a == null ? SiteCert(h, (SslProtocols)768, out e2) : null;
                bool old = a != null || b != null; if (a != null) a.Dispose(); if (b != null) b.Dispose();
                return R(old ? 1 : 0, "Old TLS · " + h, old ? "TLS " + (a != null ? "1.0" : "1.1") + " still accepted" : "TLS 1.0 / 1.1 refused (or turned off on this PC)", old ? "Browsers dropped TLS 1.0 and 1.1 in 2020; servers should too." : "", null);
            });
        }
        static List<string[]> Headers(Monitor m, List<string> sites)
        {
            return PerSite(sites, "Security headers", h =>
            {
                try
                {
                    var q = (HttpWebRequest)WebRequest.Create("https://" + h + "/"); q.Timeout = 10000; q.Method = "GET"; q.UserAgent = "AdeeboMeter/" + Program.Version;
                    HttpWebResponse r; try { r = (HttpWebResponse)q.GetResponse(); } catch (WebException e) { r = e.Response as HttpWebResponse; if (r == null) return R(3, "Security headers · " + h, "couldn't load https://" + h, "", null); }
                    using (r)
                    {
                        var miss = new List<string>(); var hd = r.Headers;
                        if (hd["Strict-Transport-Security"] == null) miss.Add("HSTS"); if (hd["Content-Security-Policy"] == null) miss.Add("CSP");
                        if (hd["X-Content-Type-Options"] == null) miss.Add("X-Content-Type-Options"); if (hd["X-Frame-Options"] == null && (hd["Content-Security-Policy"] ?? "").IndexOf("frame-ancestors", StringComparison.OrdinalIgnoreCase) < 0) miss.Add("framing protection");
                        if (hd["Referrer-Policy"] == null) miss.Add("Referrer-Policy");
                        return R(miss.Count == 0 ? 0 : miss.Contains("HSTS") || miss.Count >= 3 ? 1 : 3, "Security headers · " + h, miss.Count == 0 ? "all five common ones present" : "missing: " + string.Join(", ", miss), miss.Count == 0 ? "" : "These tell browsers to refuse downgrades, script injection and framing. Your web host or CMS can add them.", null);
                    }
                }
                catch (NotSupportedException) { return R(3, "Security headers · " + h, "couldn't check", "", null); }
            });
        }
        static readonly string[] Selectors = { "selector1", "selector2", "google", "default", "k1", "s1", "s2", "dkim", "mail" };
        static List<string[]> Mail(Monitor m, List<string> sites)
        {
            string dns; lock (m.L) dns = m.DnsServer; IPAddress srv;
            if (!IPAddress.TryParse((dns ?? "").Split(',')[0].Trim(), out srv)) return One(3, "Mail protection", "no DNS server to ask", "", null);
            return PerSite(sites, "Mail protection", h =>
            {
                string d = h.StartsWith("www.", StringComparison.Ordinal) ? h.Substring(4) : h;
                bool noAnswer = false;
                Func<string, List<string>> txt = n => { var r = Toolkit.Query(srv, n, 16, true); if (r == null) { noAnswer = true; return new List<string>(); } return r.Rrs.Where(x => x.Section == 1 && x.Type == "TXT").Select(x => x.Data).ToList(); };
                var spf = txt(d).FirstOrDefault(x => x.Contains("v=spf1")); var dm = txt("_dmarc." + d).FirstOrDefault(x => x.Contains("v=DMARC1"));
                string pol = dm != null ? Regex.Match(dm, @"p=(\w+)").Groups[1].Value : "";
                string dk = Selectors.FirstOrDefault(sel => txt(sel + "._domainkey." + d).Any(x => x.Contains("p=")));
                if (noAnswer) return R(3, "Mail protection · " + d, "couldn't check - your DNS server didn't answer every question", "", null);
                var miss = new List<string>(); if (spf == null) miss.Add("SPF"); if (dm == null) miss.Add("DMARC");
                string val = (spf != null ? "SPF " + (spf.Contains("-all") ? "strict (-all)" : spf.Contains("~all") ? "soft (~all)" : "set") : "no SPF") + " · " + (dm != null ? "DMARC p=" + pol : "no DMARC") + " · " + (dk != null ? "DKIM found (" + dk + ")" : "DKIM not found under common selectors");
                int lv = miss.Count > 0 ? (miss.Count == 2 ? 2 : 1) : pol == "none" ? 1 : 0;
                return R(lv, "Mail protection · " + d, val, lv == 0 ? "" : "Without SPF and DMARC (p=quarantine or reject) anyone can send email that looks like it's from " + d + ". Your mail provider's admin page has the records to add.", null);
            });
        }

        public static readonly List<Check> All = new List<Check>
        {
            C("av", "pc", "Antivirus", Antivirus), C("fw", "pc", "Firewall", Firewall), C("bitlocker", "pc", "Drive encryption", BitLocker), C("secureboot", "pc", "Secure Boot", SecureBoot),
            C("uac", "pc", "User Account Control", Uac), C("updates", "pc", "Windows Update", Updates), C("version", "pc", "Windows version still supported", Version), C("smb1", "pc", "SMBv1", Smb1),
            C("smbsign", "pc", "SMB signing", SmbSign), C("rdp", "pc", "Remote Desktop", Rdp), C("ra", "pc", "Remote Assistance", RemoteAssist), C("names", "pc", "Name broadcasts (LLMNR, NetBIOS, mDNS)", NameServices),
            C("wpad", "pc", "Automatic proxy detection (WPAD)", Wpad), C("ports", "pc", "Open to the network", OpenPorts), C("roots", "pc", "Trusted root certificates", Roots), C("hosts", "pc", "Hosts file", Hosts),
            C("proxy", "pc", "Proxy", Proxy), C("startup", "pc", "Programs that start with Windows", Startup), C("tasks", "pc", "Scheduled tasks", Tasks), C("lsa", "pc", "Sign-in secrets protection", Lsa),
            C("hvci", "pc", "Memory integrity", Hvci), C("smartscreen", "pc", "SmartScreen", SmartScreen), C("autoplay", "pc", "AutoPlay", AutoPlay), C("admin", "pc", "Everyday account", AdminDaily),
            C("lock", "pc", "Screen lock", ScreenLock), C("openwifi", "pc", "Saved open Wi-Fi networks", OpenWifi), C("pspolicy", "pc", "PowerShell script policy", PsPolicy),
            C("wifisec", "net", "This Wi-Fi's security", WifiSec), C("dnshijack", "net", "DNS honesty", DnsHijack), C("tlsint", "net", "Encrypted traffic inspected?", TlsIntercept), C("arp", "net", "ARP spoofing signs", ArpSpoof),
            C("dhcp2", "net", "A second DHCP server", SecondDhcp), C("upnp", "net", "Router UPnP", Upnp), C("routeradmin", "net", "Router admin page over http", RouterAdmin), C("routerold", "net", "Router old services", RouterOld),
            C("v6pub", "net", "Public IPv6 address", PublicV6), C("dnsleak", "net", "DNS leak on VPN", DnsLeak), C("newdev", "net", "New devices", NewDevices),
            C("certexp", "sites", "Certificate expiring", CertExpiry), C("oldtls", "sites", "Old TLS still accepted", OldTls), C("headers", "sites", "Security headers", Headers), C("mailsec", "sites", "SPF, DKIM and DMARC", Mail)
        };
        static Check C(string id, string g, string t, Func<Monitor, List<string>, List<string[]>> f) { return new Check { Id = id, Group = g, Title = t, Run = f }; }
        public static List<string[]> RunOne(Check c, Monitor m, List<string> sites)
        {
            try { var r = c.Run(m, sites ?? new List<string>()); return r != null && r.Count > 0 ? r : One(3, c.Title, "no result", "", null); }
            catch (Exception e) { Program.Log("security " + c.Id + ": " + e); return One(3, c.Title, "couldn't check - " + Draw.Trunc(e.Message, 80), "", null); }
        }
    }
}
