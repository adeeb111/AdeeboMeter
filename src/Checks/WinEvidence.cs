// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // Windows' own evidence, read locally without admin: address conflicts, the network profile and firewall, adapter
    // settings that cause drops, DHCP trouble, and why network sign-in (802.1X) failed.
    static class WinEvidence
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string First(string s) { s = (s ?? "").Replace("\r", ""); int n = s.IndexOf('\n'); return Draw.Trunc(n > 0 ? s.Substring(0, n) : s, 160); }

        public static List<string[]> All(Monitor m)
        {
            var rows = new List<string[]>(); string err;
            // address conflicts
            var conf = EvLog.Recent("System", "EventID=4199", 24 * 7, 5, out err);
            rows.Add(conf.Count > 0 ? R(2, "Address conflict", conf.Count + " in the last 7 days, last " + conf[0].T.ToString("ddd HH:mm", CultureInfo.CurrentCulture), "Another device used your IP address - the connection drops for both. Usually a device with a fixed address inside the router's DHCP range.", "w")
                                    : R(err == "" ? 0 : 3, "Address conflict", err == "" ? "none in the last 7 days" : err, "", err == "" ? "w" : "u"));
            // network profile + firewall
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\NetworkList\Profiles"))
                {
                    var profs = new List<Tuple<string, int, DateTime>>();
                    if (k != null) foreach (var s in k.GetSubKeyNames()) using (var p = k.OpenSubKey(s))
                            {
                                if (p == null) continue; var b = p.GetValue("DateLastConnected") as byte[]; DateTime t = DateTime.MinValue;
                                if (b != null && b.Length >= 16) try { t = new DateTime(BitConverter.ToUInt16(b, 0), BitConverter.ToUInt16(b, 2), BitConverter.ToUInt16(b, 6), BitConverter.ToUInt16(b, 8), BitConverter.ToUInt16(b, 10), BitConverter.ToUInt16(b, 12)); } catch { }
                                profs.Add(Tuple.Create(Convert.ToString(p.GetValue("ProfileName", "?"), CultureInfo.InvariantCulture), Convert.ToInt32(p.GetValue("Category", 0), CultureInfo.InvariantCulture), t));
                            }
                    foreach (var p in profs.OrderByDescending(x => x.Item3).Take(3))
                        rows.Add(R(3, "Network \"" + Draw.Trunc(p.Item1, 30) + "\"", (p.Item2 == 1 ? "Private" : p.Item2 == 2 ? "Domain (company)" : "Public") + (p.Item3 > DateTime.MinValue ? " · last used " + p.Item3.ToString("d MMM", CultureInfo.CurrentCulture) : ""),
                            p.Item2 == 0 ? "Public hides your PC and blocks printers / shares on that network - right for cafés, wrong for home. Settings > Network > the network > Private." : "", "w"));
                }
            }
            catch (Exception e) { rows.Add(R(3, "Network profile", "couldn't read: " + e.Message, "", "u")); }
            foreach (var fp in new[] { new[] { "DomainProfile", "Company" }, new[] { "StandardProfile", "Private" }, new[] { "PublicProfile", "Public" } })
                try
                {
                    object v = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsFirewall\" + fp[0], "EnableFirewall", null) ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\" + fp[0], "EnableFirewall", null);
                    if (v != null) { bool on = Convert.ToInt32(v, CultureInfo.InvariantCulture) == 1; rows.Add(R(on ? 0 : 1, "Firewall (" + fp[1] + " networks)", on ? "on" : "OFF", on ? "" : "Windows Firewall is off for these networks - turn it back on unless IT manages another firewall.", "w")); }
                }
                catch (Exception e) { Program.Log("fw: " + e.Message); }
            // the active adapter: power saving, speed / duplex, driver age
            NetworkInterface nic; lock (m.L) nic = m.Primary;
            if (nic != null)
            {
                try
                {
                    using (var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}"))
                        if (cls != null) foreach (var s in cls.GetSubKeyNames()) using (var a = SafeOpen(cls, s))
                                {
                                    if (a == null || !string.Equals(Convert.ToString(a.GetValue("NetCfgInstanceId", ""), CultureInfo.InvariantCulture), nic.Id, StringComparison.OrdinalIgnoreCase)) continue;
                                    int pnp = Convert.ToInt32(a.GetValue("PnPCapabilities", 0), CultureInfo.InvariantCulture);
                                    bool saver = (pnp & 0x18) == 0;
                                    rows.Add(R(saver ? 1 : 0, "Adapter power saving", saver ? "Windows may switch the adapter off to save power" : "off", saver ? "On laptops this is a common cause of drops after idle. Device Manager > the adapter > Power Management > untick 'Allow the computer to turn off this device'." : "", "w"));
                                    string sd = Convert.ToString(a.GetValue("*SpeedDuplex", ""), CultureInfo.InvariantCulture);
                                    if (sd != "" && nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet) rows.Add(R(sd == "0" ? 0 : 1, "Speed and duplex", sd == "0" ? "automatic" : "fixed by hand (setting " + sd + ")", sd == "0" ? "" : "A fixed speed / duplex that doesn't match the switch causes errors and slow transfers. Usually it should be automatic.", "w"));
                                }
                }
                catch (Exception e) { Program.Log("adapter reg: " + e.Message); }
                rows.Add(R(3, "Link speed", nic.Speed > 0 ? (nic.Speed / 1e6).ToString("0", CultureInfo.CurrentCulture) + " Mb/s" + (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet && nic.Speed == 100000000 ? " - a gigabit port running at 100 often means a bad cable" : "") : "-", "", "w"));
                try { var d = DriverInfo.Of(nic.Id); if (d != null && d.Length > 1) rows.Add(R(3, "Adapter driver", string.Join(" · ", d.Where(x => !string.IsNullOrEmpty(x))), "", "w")); } catch (Exception e) { Program.Log("driver: " + e.Message); }
                // DHCP
                try
                {
                    foreach (var u in nic.GetIPProperties().UnicastAddresses.Where(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                    {
                        string ip = u.Address.ToString();
                        if (ip.StartsWith("169.254.", StringComparison.Ordinal)) rows.Add(R(2, "Address", ip + " - made up by Windows", "No DHCP server answered: the router didn't give an address. Restart the router, or check the cable / Wi-Fi password.", "w"));
                        else if (u.PrefixOrigin == PrefixOrigin.Dhcp) { var left = TimeSpan.FromSeconds(u.DhcpLeaseLifetime); rows.Add(R(left.TotalMinutes < 10 ? 1 : 0, "DHCP lease", ip + " · " + (left.TotalHours >= 1 ? left.TotalHours.ToString("0", CultureInfo.CurrentCulture) + " h" : left.TotalMinutes.ToString("0", CultureInfo.CurrentCulture) + " min") + " left", "", "w")); }
                    }
                }
                catch (Exception e) { Program.Log("lease: " + e.Message); }
            }
            var dh = EvLog.Recent("Microsoft-Windows-Dhcp-Client/Admin", "", 24 * 7, 5, out err);
            if (dh.Count > 0) rows.Add(R(1, "DHCP trouble", dh.Count + " events in 7 days - last: " + First(dh[0].Text), "", "w"));
            return rows;
        }
        static RegistryKey SafeOpen(RegistryKey k, string s) { try { return k.OpenSubKey(s); } catch { return null; } }

        // ---------- why network sign-in (802.1X / Wi-Fi) failed - failure events only (12012 and 15501 are the successes)
        public static List<string[]> Auth()
        {
            var rows = new List<string[]>(); string e1, e2;
            var wl = EvLog.Recent("Microsoft-Windows-WLAN-AutoConfig/Operational", "EventID=8002 or EventID=12013 or EventID=11006", 24 * 7, 8, out e1);
            var wd = EvLog.Recent("Microsoft-Windows-Wired-AutoConfig/Operational", "EventID=15502 or EventID=15514", 24 * 7, 8, out e2);
            foreach (var e in wl.Concat(wd).OrderByDescending(x => x.T).Take(10))
            {
                string reason = Reason(e.Text);
                rows.Add(R(2, e.T.ToString("ddd HH:mm", CultureInfo.CurrentCulture), Draw.Trunc(reason != "" ? reason : First(e.Text), 140), Hint(e.Text), "w"));
            }
            if (rows.Count == 0) rows.Add(R(e1 != "" && e2 != "" ? 1 : 0, "Network sign-in", e1 != "" && e2 != "" ? e1 : "no failed sign-ins in the last 7 days (" + (e1 != "" ? "wired 802.1X - the Wi-Fi log couldn't be read" : e2 != "" ? "Wi-Fi - the wired 802.1X log couldn't be read" : "Wi-Fi or wired 802.1X") + ")", "", e1 != "" || e2 != "" ? "u" : "w"));
            return rows;
        }
        static string Reason(string t)
        {
            var m = Regex.Match(t ?? "", @"(Failure Reason|Reason Code|Reason|Error)\s*:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[2].Value.Trim() : "";
        }
        static string Hint(string t)
        {
            t = (t ?? "").ToLowerInvariant();
            if (t.Contains("certificate") && t.Contains("expired")) return "Your certificate (or the server's) has expired - IT needs to renew it.";
            if (t.Contains("certificate") || t.Contains("trust")) return "The PC doesn't trust the sign-in server's certificate, or has no valid certificate of its own - IT setting.";
            if (t.Contains("credential") || t.Contains("password") || t.Contains("logon")) return "Wrong or expired user name / password for the network.";
            if (t.Contains("timeout") || t.Contains("no response")) return "The sign-in server (RADIUS) didn't answer - the network side, not your PC.";
            return "";
        }
    }
}
