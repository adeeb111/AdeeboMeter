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
    // ================================================================== Health check (runs once when asked)
    static class HealthCheck
    {
        public static List<string[]> Run(Monitor m)
        {
            var rows = new List<string[]>(); // level, name, value, advice
            Action<int, string, string, string> add = delegate(int l, string n, string v, string a) { rows.Add(new[] { l.ToString(CultureInfo.CurrentCulture), n, v, a }); };
            try
            {
                double days = Native.GetTickCount64() / 86400000.0;
                add(days > 7 ? 1 : 0, "Uptime", days < 1 ? (days * 24).ToString("0", CultureInfo.CurrentCulture) + " hours" : days.ToString("0", CultureInfo.CurrentCulture) + " days",
                    days > 7 ? Voice.P(days.ToString("0", CultureInfo.CurrentCulture) + " days without a nap. A restart clears leaked memory and finishes pending updates.", "Consider a restart: it clears leaked memory and finishes updates.") : Voice.P("Fresh enough.", "Recently restarted."));
            }
            catch { }
            bool pending = KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") || KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            add(pending ? 1 : 0, "Pending reboot", pending ? "yes" : "no", pending ? Voice.P("Windows is waiting for a restart. Some network fixes only land after it.", "A restart is pending; some fixes apply only after it.") : "Nothing waiting.");
            try
            {
                foreach (var d in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed && x.IsReady))
                {
                    double free = d.AvailableFreeSpace * 100.0 / Math.Max(1, d.TotalSize);
                    add(free < 10 ? 2 : free < 20 ? 1 : 0, "Disk " + d.Name.TrimEnd('\\'), (d.AvailableFreeSpace / 1073741824.0).ToString("0", CultureInfo.CurrentCulture) + " GB free of " + (d.TotalSize / 1073741824.0).ToString("0", CultureInfo.CurrentCulture) + " GB",
                        free < 10 ? Voice.P("Nearly full - Windows gets grumpy below 10%. Empty Downloads or old captures.", "Nearly full; free some space.")
                        : free < 20 ? Voice.P("Getting tight (" + free.ToString("0", CultureInfo.CurrentCulture) + "% free). Worth a clean-up before it bites.", "Below 20% free; plan a clean-up.") : "Plenty of room.");
                }
            }
            catch { }
            NetworkInterface ni; string kind, ch, dns, vpnName; bool vpn; int sig, vmtu, pmtu;
            lock (m.L) { ni = m.Primary; kind = m.AdapterKind; ch = m.WifiChannel; sig = m.WifiSignal; vpn = m.VpnUp; vmtu = m.VpnMtu; pmtu = m.PrimaryMtu; dns = m.DnsServer; vpnName = m.VpnName; }
            if (ni != null)
            {
                try
                {
                    var st = ni.GetIPStatistics();
                    long errs = st.IncomingPacketsWithErrors + st.OutgoingPacketsWithErrors, disc = st.IncomingPacketsDiscarded + st.OutgoingPacketsDiscarded;
                    add(errs > 100 ? 1 : 0, "Adapter errors", Draw.Trunc(ni.Name, 16) + ": " + errs + " errors · " + disc + " discards",
                        errs > 100 ? Voice.P("The adapter itself is coughing. Try another cable or port, or update the driver.", "Adapter errors: try another cable or port, or update the driver.") : "The adapter is clean.");
                }
                catch { }
            }
            if (kind == "Wi-Fi")
            {
                string outp = ""; try { outp = Job.Run(Job.Netsh, "wlan show networks mode=bssid", 5000); } catch { }
                if (outp.IndexOf("BSSID", StringComparison.OrdinalIgnoreCase) < 0)
                    add(3, "Wi-Fi neighbourhood", "not available", "Windows only shares the Wi-Fi scan with apps that have location permission. Skipped.");
                else
                {
                    int same = Math.Max(0, Regex.Matches(outp, @"^\s*Channel\s*:\s*(\d+)", RegexOptions.Multiline).Cast<Match>().Count(x => x.Groups[1].Value == ch) - 1);
                    int lv = (sig >= 0 && sig < 40) || same >= 5 ? 1 : 0;
                    add(lv, "Wi-Fi", "signal " + (sig >= 0 ? sig + "%" : "?") + " · ch " + ch + " · " + same + " other AP" + (same == 1 ? "" : "s") + " on it",
                        lv == 1 ? Voice.P("Crowded or weak. Move closer, or switch band (5 GHz near the router, 2.4 GHz far away).", "Crowded or weak channel; move closer or change band.") : Voice.P("Your channel is calm.", "Channel is clear."));
                }
            }
            string proxy = DestCheck.ProxyInfo();
            add(proxy.StartsWith("none", StringComparison.Ordinal) ? 0 : 3, "Proxy", proxy, proxy.StartsWith("none", StringComparison.Ordinal) ? "Browsers go direct." : "Browsers go through this. If a site passes AdeeboMeter's check but fails in the browser, the proxy is the suspect.");
            add(0, "VPN", vpn ? Draw.Trunc(vpnName, 30) + (vmtu > 0 ? " · MTU " + vmtu : "") : "not connected", vpn ? "Company traffic goes through the tunnel (MTU " + (vmtu > 0 ? vmtu.ToString(CultureInfo.CurrentCulture) : "?") + " vs " + pmtu + " on the adapter)." : "Direct to the internet.");
            add(0, "DNS server", dns == "" ? "none" : dns, "The server your PC asks for names" + (vpn ? " (via VPN)." : "."));
            var top = m.TopApps(false, 2, true);
            double com = m.Commit.Avg(5);
            add(com > 90 ? 2 : com > 80 ? 1 : 0, "Memory", Draw.Fmt(com, "0", "% committed") + (top.Count > 0 ? " · " + top[0].Name + " " + Draw.MemStr(top[0].Mem) : ""),
                com > 80 ? Voice.P((top.Count > 0 ? top[0].Name : "Something") + " is hoarding memory. Close what you forgot about.", "Memory is high; close unused apps.") : "Room to spare.");
            Native.SYSTEM_POWER_STATUS ps;
            if (Native.GetSystemPowerStatus(out ps))
            {
                bool batt = ps.ACLineStatus == 0, saver = ps.SystemStatusFlag == 1;
                add(saver ? 1 : 0, "Power", (batt ? "on battery " + ps.BatteryLifePercent + "%" : "plugged in") + (saver ? " · battery saver ON" : ""),
                    saver ? Voice.P("Battery saver is slowing things down on purpose. Plug in if it feels sluggish.", "Battery saver is on and throttles performance.") : "Not throttled.");
            }
            return rows;
        }
        static bool KeyExists(string path) { try { using (var k = Registry.LocalMachine.OpenSubKey(path)) return k != null; } catch { return false; } }
    }
}
