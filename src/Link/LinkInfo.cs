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
    // ================================================================== engineer view
    // ================================================================== Deeper sources - all offline, all without admin
    // Windows' own event log: why Wi-Fi dropped, why DHCP failed. Read only when asked (Wi-Fi tab, or right after a drop).
    static class EvLog
    {
        [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr EvtQuery(IntPtr session, string path, string query, int flags);
        [DllImport("wevtapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool EvtNext(IntPtr results, int count, [Out] IntPtr[] events, int timeout, int flags, out int returned);
        [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool EvtRender(IntPtr context, IntPtr fragment, int flags, int bufferSize, [Out] char[] buffer, out int used, out int props);
        [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr EvtOpenPublisherMetadata(IntPtr session, string publisher, string logFile, int locale, int flags);
        [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool EvtFormatMessage(IntPtr publisher, IntPtr evt, int messageId, int valueCount, IntPtr values, int flags, int bufferSize, [Out] char[] buffer, out int used);
        [DllImport("wevtapi.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool EvtClose(IntPtr h);

        public sealed class Ev { public DateTime T; public int Id, Level; public string Text = "", Xml = ""; }
        static string Render(IntPtr e)
        {
            int used, props; var buf = new char[16384];
            return EvtRender(IntPtr.Zero, e, 1 /*EvtRenderEventXml*/, buf.Length * 2, buf, out used, out props) ? new string(buf, 0, Math.Max(0, used / 2 - 1)) : "";
        }
        static string Format(IntPtr pub, IntPtr e)
        {
            if (pub == IntPtr.Zero) return "";
            int used; var buf = new char[8192];
            return EvtFormatMessage(pub, e, 0, 0, IntPtr.Zero, 1 /*EvtFormatMessageEvent*/, buf.Length, buf, out used) ? new string(buf, 0, Math.Max(0, used - 1)) : "";
        }
        // The event XML is read with plain patterns, not an XML parser (nothing to resolve, nothing to expand).
        public static string Field(string xml, string name)
        {
            var m = Regex.Match(xml ?? "", "<Data Name=['\"]" + Regex.Escape(name) + "['\"]>([^<]*)</Data>");
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : "";
        }
        public static List<Ev> Recent(string log, string ids, double hours, int max, out string err)
        {
            var list = new List<Ev>(); err = "";
            string q = "*[System[" + (ids != "" ? "(" + ids + ") and " : "") + "TimeCreated[timediff(@SystemTime) <= " + ((long)(hours * 3600000)).ToString(CultureInfo.InvariantCulture) + "]]]";
            IntPtr res = IntPtr.Zero; var pubs = new Dictionary<string, IntPtr>();
            try
            {
                res = EvtQuery(IntPtr.Zero, log, q, 1 /*channel path*/ | 0x200 /*newest first*/);
                if (res == IntPtr.Zero) { int e = Marshal.GetLastWin32Error(); err = e == 5 ? "Windows doesn't let normal users read this log on this PC." : e == 15007 ? "This log doesn't exist on this PC." : "Couldn't read the log (" + e + ")."; return list; }
                var evs = new IntPtr[1]; int got;
                while (list.Count < max && EvtNext(res, 1, evs, 1000, 0, out got) && got == 1)
                {
                    try
                    {
                        string xml = Render(evs[0]);
                        var ev = new Ev { Xml = xml };
                        var idm = Regex.Match(xml, @"<EventID[^>]*>(\d+)</EventID>"); if (idm.Success) ev.Id = int.Parse(idm.Groups[1].Value, CultureInfo.InvariantCulture);
                        var lvm = Regex.Match(xml, @"<Level>(\d+)</Level>"); if (lvm.Success) ev.Level = int.Parse(lvm.Groups[1].Value, CultureInfo.InvariantCulture);
                        var tm = Regex.Match(xml, @"SystemTime=['""]([^'""]+)['""]"); DateTime t;
                        if (tm.Success && DateTime.TryParse(tm.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t)) ev.T = t.ToLocalTime();
                        var pm = Regex.Match(xml, @"<Provider Name=['""]([^'""]+)['""]");
                        if (pm.Success)
                        {
                            IntPtr ph; if (!pubs.TryGetValue(pm.Groups[1].Value, out ph)) { ph = EvtOpenPublisherMetadata(IntPtr.Zero, pm.Groups[1].Value, null, 0, 0); pubs[pm.Groups[1].Value] = ph; }
                            ev.Text = Format(ph, evs[0]);
                        }
                        list.Add(ev);
                    }
                    finally { EvtClose(evs[0]); }
                }
            }
            catch (Exception e) { err = Draw.Trunc(e.Message, 80); }
            finally { if (res != IntPtr.Zero) EvtClose(res); foreach (var ph in pubs.Values) if (ph != IntPtr.Zero) EvtClose(ph); }
            return list;
        }
        // "Reason: The network is disconnected by the user." -> the part after the label, first line only.
        public static string ReasonOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var m = Regex.Match(text, @"(?:Failure Reason|Reason)\s*:\s*(.+)", RegexOptions.IgnoreCase);
            string r = m.Success ? m.Groups[1].Value : text.Split('\n')[0];
            return Draw.Trunc(r.Trim().TrimEnd('.'), 110);
        }
        public static List<string[]> WifiDrops(double hours, out string err)
        {
            var rows = new List<string[]>();
            foreach (var e in Recent("Microsoft-Windows-WLAN-AutoConfig/Operational", "EventID=8003 or EventID=8002", hours, 12, out err))
            {
                string why = Field(e.Xml, e.Id == 8002 ? "FailureReason" : "Reason"); if (why == "") why = ReasonOf(e.Text);
                string ssid = Field(e.Xml, "SSID");
                rows.Add(new[] { e.T.ToString("ddd HH:mm", CultureInfo.CurrentCulture), (e.Id == 8002 ? "Couldn't connect" : "Dropped") + (ssid != "" ? " from " + ssid : "") + ": " + Plain(Draw.Trunc(why.TrimEnd('.'), 110)), e.Id == 8002 ? "2" : "1" });
            }
            return rows;
        }
        public static List<string[]> DhcpProblems(double hours, out string err)
        {
            var rows = new List<string[]>();
            foreach (var e in Recent("Microsoft-Windows-Dhcp-Client/Admin", "Level=2 or Level=3", hours, 8, out err))
                rows.Add(new[] { e.T.ToString("ddd HH:mm", CultureInfo.CurrentCulture), Draw.Trunc((e.Text != "" ? e.Text : "DHCP event " + e.Id).Split('\n')[0].Trim(), 110), "1" });
            return rows;
        }
        // Windows' wording is technical; add the plain meaning for the common ones.
        // "disconnected by the user" in the languages Windows writes it in (the reason is a translated sentence)
        static readonly string[] UserWords = { "by the user", "vom benutzer", "par l'utilisateur", "par l’utilisateur", "por el usuario", "dall'utente", "pelo usuário", "pelo utilizador", "door de gebruiker", "przez użytkownika", "пользователем", "ユーザーによって", "用户", "使用者", "사용자", "kullanıcı tarafından", "بواسطة المستخدم" };
        public static bool ByUser(string reason) { string r = (reason ?? "").ToLowerInvariant(); return UserWords.Any(w => r.Contains(w)); }
        public static string Plain(string reason)
        {
            string r = reason.ToLowerInvariant();
            if (ByUser(reason)) return reason + " (you or an app chose to disconnect)";
            if (r.Contains("driver disconnected") || r.Contains("deauth") || r.Contains("disassociat")) return reason + " (the access point or the driver dropped the link)";
            if (r.Contains("roam")) return reason + " (moved to another access point)";
            if (r.Contains("authentication") || r.Contains("security")) return reason + " (sign-in to the network failed - password or certificate)";
            if (r.Contains("not found") || r.Contains("out of range") || r.Contains("no longer")) return reason + " (the network went out of range)";
            return reason;
        }
    }

    // Windows' Native Wifi API: what netsh doesn't show - channel width, roaming features, real dBm per access point.
    static class WlanApi
    {
        [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(int clientVersion, IntPtr reserved, out int negotiated, out IntPtr handle);
        [DllImport("wlanapi.dll")] static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);
        [DllImport("wlanapi.dll")] static extern int WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);
        [DllImport("wlanapi.dll")] static extern int WlanGetNetworkBssList(IntPtr handle, ref Guid iface, IntPtr ssid, int bssType, [MarshalAs(UnmanagedType.Bool)] bool secOnly, IntPtr reserved, out IntPtr list);
        [DllImport("wlanapi.dll")] static extern int WlanScan(IntPtr handle, ref Guid iface, IntPtr ssid, IntPtr ie, IntPtr reserved);
        [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr p);
        [DllImport("wlanapi.dll")] static extern int WlanQueryInterface(IntPtr handle, ref Guid iface, int opCode, IntPtr reserved, out int size, out IntPtr data, IntPtr opType);

        [DllImport("wlanapi.dll")] static extern int WlanGetProfileList(IntPtr handle, ref Guid iface, IntPtr reserved, out IntPtr list);
        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)] static extern int WlanGetProfile(IntPtr handle, ref Guid iface, string name, IntPtr reserved, out IntPtr xml, ref int flags, out int access);

        // Saved Wi-Fi profiles as Windows stores them (XML, the same words in every language): name, open (no security),
        // joins by itself. null when the Wi-Fi service isn't there. Passwords are never asked for (no plaintext flag).
        public static List<Tuple<string, bool, bool>> Profiles()
        {
            IntPtr h = IntPtr.Zero, ifl = IntPtr.Zero; var res = new List<Tuple<string, bool, bool>>(); var seen = new HashSet<string>();
            try
            {
                int ver; if (WlanOpenHandle(2, IntPtr.Zero, out ver, out h) != 0) return null;
                if (WlanEnumInterfaces(h, IntPtr.Zero, out ifl) != 0) return null;
                int n = Marshal.ReadInt32(ifl);
                for (int i = 0; i < n && i < 8; i++)
                {
                    var gb = new byte[16]; Marshal.Copy(ifl + 8 + i * 532, gb, 0, 16); var g = new Guid(gb);
                    IntPtr pl; if (WlanGetProfileList(h, ref g, IntPtr.Zero, out pl) != 0) continue;
                    try
                    {
                        int cnt = Marshal.ReadInt32(pl);
                        for (int j = 0; j < cnt && j < 100; j++)
                        {
                            string name = Marshal.PtrToStringUni(pl + 8 + j * 516);   // WLAN_PROFILE_INFO: WCHAR[256] + flags
                            if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                            IntPtr x; int flags = 0, acc;
                            if (WlanGetProfile(h, ref g, name, IntPtr.Zero, out x, ref flags, out acc) != 0) continue;
                            string xml; try { xml = Marshal.PtrToStringUni(x) ?? ""; } finally { WlanFreeMemory(x); }
                            bool open = Regex.IsMatch(xml, @"<authentication>\s*open\s*</authentication>", RegexOptions.IgnoreCase) && Regex.IsMatch(xml, @"<encryption>\s*none\s*</encryption>", RegexOptions.IgnoreCase);
                            bool auto = Regex.IsMatch(xml, @"<connectionMode>\s*auto\s*</connectionMode>", RegexOptions.IgnoreCase);
                            res.Add(Tuple.Create(Program.Plain(name), open, auto));
                        }
                    }
                    finally { WlanFreeMemory(pl); }
                }
            }
            catch (Exception e) { if (!(e is DllNotFoundException || e is EntryPointNotFoundException)) throw; return null; }
            finally { if (ifl != IntPtr.Zero) WlanFreeMemory(ifl); if (h != IntPtr.Zero && WlanCloseHandle(h, IntPtr.Zero) != 0) Program.Log("Wi-Fi handle close failed"); }
            return res;
        }

        // The Wi-Fi you're on, straight from Windows' Wi-Fi service: no netsh.exe to start (and no console host with it), the
        // same numbers netsh prints - signal quality %, receive rate, channel, the access point - and labels in no language.
        // rc: 0 read; 5 Windows hides it (location access is off); anything else: this Windows can't say (netsh is used then).
        // One handle per call, closed at once. The connected adapter wins; with none connected, the first one's state.
        public sealed class Now { public int Signal = -1, Rate = -1; public string Ssid = "", Bssid = "", Channel = "", State = ""; }
        static readonly string[] States = { "not ready", "connected", "ad hoc", "disconnecting", "disconnected", "associating", "discovering", "authenticating" };
        public static int Current(out Now now) { return Current(out now, null); }
        // prefer: the adapter Windows routes through (NetworkInterface.Id, "{guid}") - with two Wi-Fi adapters, that one's state counts.
        // -2: no Wi-Fi adapter right now (asleep, unplugged) - not "this Windows can't".
        public static int Current(out Now now, string prefer)
        {
            now = null; IntPtr h = IntPtr.Zero, ifl = IntPtr.Zero;
            try
            {
                int ver; if (WlanOpenHandle(2, IntPtr.Zero, out ver, out h) != 0) return -1;
                if (WlanEnumInterfaces(h, IntPtr.Zero, out ifl) != 0) return -1;
                int n = Marshal.ReadInt32(ifl); if (n <= 0) return -2;
                Guid want; bool hasWant = Guid.TryParse(prefer ?? "", out want);
                int pick = -1;
                for (int i = 0; i < n && i < 8 && hasWant; i++) { var gi = new byte[16]; Marshal.Copy(ifl + 8 + i * 532, gi, 0, 16); if (new Guid(gi) == want) { pick = i; break; } }
                for (int i = 0; i < n && i < 8 && pick < 0; i++) if (Marshal.ReadInt32(ifl + 8 + i * 532 + 528) == 1) pick = i;   // WLAN_INTERFACE_INFO.isState: 1 = connected
                for (int i = 0; i < n && i < 8 && pick < 0; i++) if (Marshal.ReadInt32(ifl + 8 + i * 532 + 528) != 4) pick = i;   // reconnecting (associating...) beats an idle one that's disconnected
                if (pick < 0) pick = 0;
                var gb = new byte[16]; Marshal.Copy(ifl + 8 + pick * 532, gb, 0, 16); var g = new Guid(gb);
                int st = Marshal.ReadInt32(ifl + 8 + pick * 532 + 528);
                var r = new Now { State = st >= 0 && st < States.Length ? States[st] : "" };
                if (st != 1) { now = r; return 0; }   // not connected: nothing more to read
                int size; IntPtr data;
                int rc = WlanQueryInterface(h, ref g, 7 /*current_connection*/, IntPtr.Zero, out size, out data, IntPtr.Zero);
                if (rc != 0) return rc;
                try
                {
                    if (size < 588) return -1;
                    // WLAN_CONNECTION_ATTRIBUTES: state, mode, profile name WCHAR[256] (8..520), then the association:
                    // SSID length + 32 bytes (520), BSS type (556), BSSID (560), PHY type (568), PHY index (572), signal quality (576), rx kbps (580), tx kbps (584)
                    int sl = Math.Min(32, Math.Max(0, Marshal.ReadInt32(data, 520))); var sb = new byte[sl]; Marshal.Copy(data + 524, sb, 0, sl); r.Ssid = Program.Plain(Encoding.UTF8.GetString(sb));
                    var mac = new byte[6]; Marshal.Copy(data + 560, mac, 0, 6); r.Bssid = string.Join(":", mac.Select(x => x.ToString("x2", CultureInfo.InvariantCulture)));
                    int q = Marshal.ReadInt32(data, 576); r.Signal = q >= 0 && q <= 100 ? q : -1;
                    long rx = (uint)Marshal.ReadInt32(data, 580); r.Rate = rx > 0 ? (int)(rx / 1000) : -1;
                }
                finally { WlanFreeMemory(data); }
                if (WlanQueryInterface(h, ref g, 8 /*channel_number*/, IntPtr.Zero, out size, out data, IntPtr.Zero) == 0)
                    try { if (size >= 4) { int c = Marshal.ReadInt32(data); if (c > 0 && c < 300) r.Channel = c.ToString(CultureInfo.InvariantCulture); } } finally { WlanFreeMemory(data); }
                now = r; return 0;
            }
            catch (Exception e) { if (!(e is DllNotFoundException || e is EntryPointNotFoundException)) throw; return -1; }
            finally { if (ifl != IntPtr.Zero) WlanFreeMemory(ifl); if (h != IntPtr.Zero && WlanCloseHandle(h, IntPtr.Zero) != 0) Program.Log("Wi-Fi handle close failed"); }
        }

        public sealed class Bss { public string Bssid = "", Ssid = "", Sec = ""; public int Rssi, FreqMHz, Channel, Width = 20; public bool K, V, R; public string Band = ""; }
        static DateTime lastScan = DateTime.MinValue;

        public static List<Bss> List(bool scan, out string err)
        {
            var res = new List<Bss>(); err = ""; IntPtr h = IntPtr.Zero, ifl = IntPtr.Zero;
            try
            {
                int ver; if (WlanOpenHandle(2, IntPtr.Zero, out ver, out h) != 0) { err = "Wi-Fi service not available"; return res; }
                if (WlanEnumInterfaces(h, IntPtr.Zero, out ifl) != 0) { err = "no Wi-Fi adapter"; return res; }
                int n = Marshal.ReadInt32(ifl);
                for (int i = 0; i < n; i++)
                {
                    IntPtr info = ifl + 8 + i * 532;   // WLAN_INTERFACE_INFO: GUID(16) + WCHAR[256] + state
                    var gb = new byte[16]; Marshal.Copy(info, gb, 0, 16); var g = new Guid(gb);
                    if (scan && (DateTime.UtcNow - lastScan).TotalSeconds > 60) { lastScan = DateTime.UtcNow; if (WlanScan(h, ref g, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) != 0) Program.Log("Wi-Fi scan request refused"); }   // fresh results next time
                    IntPtr bl; int rc = WlanGetNetworkBssList(h, ref g, IntPtr.Zero, 1 /*infrastructure*/, false, IntPtr.Zero, out bl);
                    if (rc != 0) { err = rc == 5 ? "Windows needs location access for Wi-Fi details" : "Wi-Fi list unavailable (" + rc + ")"; continue; }
                    try
                    {
                        int cnt = Marshal.ReadInt32(bl, 4);
                        for (int j = 0; j < cnt && j < 400; j++) res.Add(ParseEntry(bl + 8 + j * 360));   // WLAN_BSS_ENTRY is 360 bytes
                    }
                    finally { WlanFreeMemory(bl); }
                }
            }
            catch (Exception e) { err = Draw.Trunc(e.Message, 60); }
            finally { if (ifl != IntPtr.Zero) WlanFreeMemory(ifl); if (h != IntPtr.Zero && WlanCloseHandle(h, IntPtr.Zero) != 0) Program.Log("Wi-Fi handle close failed"); }
            return res;
        }
        static Bss ParseEntry(IntPtr e)
        {
            var b = new Bss();
            int sl = Math.Min(32, Marshal.ReadInt32(e, 0)); var sb = new byte[sl]; Marshal.Copy(e + 4, sb, 0, sl); b.Ssid = Program.Plain(Encoding.UTF8.GetString(sb));
            var mac = new byte[6]; Marshal.Copy(e + 40, mac, 0, 6); b.Bssid = string.Join(":", mac.Select(x => x.ToString("x2", CultureInfo.InvariantCulture)));
            b.Rssi = Marshal.ReadInt32(e, 56);
            b.FreqMHz = Marshal.ReadInt32(e, 92) / 1000;
            int ieOff = Marshal.ReadInt32(e, 352), ieSize = Marshal.ReadInt32(e, 356);
            byte[] ies = new byte[Math.Max(0, Math.Min(ieSize, 4096))];
            if (ies.Length > 0) Marshal.Copy(e + ieOff, ies, 0, ies.Length);
            FromFreq(b); ReadIes(ies, b);
            if (b.Sec == "") b.Sec = (Marshal.ReadInt16(e, 88) & 0x0010) != 0 ? "WEP" : "Open";   // no RSN / WPA element: the privacy bit says WEP or nothing
            return b;
        }
        // RSN element: version (2), group cipher (4), pairwise count + suites, AKM count + suites - the AKM says how you sign in
        static string Rsn(byte[] ies, int p, int len)
        {
            int end = p + len, q = p + 2 + 4; if (q + 2 > end) return "WPA2";
            int pc = ies[q] | ies[q + 1] << 8; q += 2 + 4 * pc; if (q + 2 > end) return "WPA2";
            int ac = ies[q] | ies[q + 1] << 8; q += 2; bool sae = false, psk = false, ent = false, owe = false, ent3 = false, sha256only = false, ent1 = false;
            for (int i = 0; i < ac && q + 4 <= end; i++, q += 4)
            {
                if (ies[q] != 0x00 || ies[q + 1] != 0x0F || ies[q + 2] != 0xAC) continue; int t = ies[q + 3];
                if (t == 8 || t == 9 || t == 24 || t == 25) sae = true;
                else if (t == 2 || t == 4 || t == 6) psk = true;
                else if (t == 12 || t == 13) ent3 = true;
                else if (t == 5) { ent = true; sha256only = true; }
                else if (t == 1 || t == 3 || t == 11 || t == 14 || t == 15) { ent = true; ent1 = true; }   // 14/15: FILS - a fast 802.1X sign-in
                else if (t == 18) owe = true;
            }
            // RSN capabilities after the AKM list: bit 6 = management frame protection required. 802.1X-SHA256 alone with it
            // required is WPA3-Enterprise ("only" mode)
            bool mfpr = q + 2 <= end && (ies[q] & 0x40) != 0;
            if (ent3 || (sha256only && !ent1 && mfpr)) return "WPA3-Enterprise"; if (ent) return "WPA2-Enterprise"; if (sae && psk) return "WPA2/WPA3"; if (sae) return "WPA3"; if (owe) return "Enhanced Open"; return "WPA2";
        }
        public static bool Weak(string sec) { return sec == "Open" || sec == "WEP" || sec == "WPA (old)"; }
        // the least crowded channel in a band: each neighbour weighs by its signal (squared - a strong one hurts far more),
        // counted on every channel it overlaps
        static readonly int[] Upper5 = { 149, 153, 157, 161 }, Dfs5 = { 52, 56, 60, 64, 100, 104, 108, 112, 116, 120, 124, 128, 132, 136, 140, 144 };
        public static int[] LeastCrowded(List<Ap> aps, string band, int mine, string myBssid)
        {
            int[] cand = band.StartsWith("2.4", StringComparison.Ordinal) ? new[] { 1, 6, 11 } : band.StartsWith("5", StringComparison.Ordinal) ? new[] { 36, 40, 44, 48 } : band.StartsWith("6", StringComparison.Ordinal) ? new[] { 5, 21, 37, 53, 69, 85 } : Array.Empty<int>();   // 6 GHz: the preferred scanning channels of UNII-5, allowed wherever 6 GHz Wi-Fi is
            // 5 GHz: 36-48 work everywhere. The upper block (149-161, not allowed in much of Europe and Japan) and the radar (DFS) channels
            // 52-144 are offered only when your router or the networks around already use them - proof that they are allowed here
            if (cand.Length == 4)
            {
                var seen = new HashSet<int>(aps.Where(a => a.Band == band && a.Channel > 0).Select(a => a.Channel)); seen.Add(mine);
                var extra = new List<int>();
                if (seen.Any(c => c >= 149)) extra.AddRange(Upper5);
                if (seen.Any(c => c >= 52 && c <= 144)) extra.AddRange(Dfs5.Where(c => seen.Contains(c) || seen.Any(z => Math.Abs(z - c) <= 12)));
                cand = cand.Concat(extra).Distinct().ToArray();
            }
            if (cand.Length == 0) return null;
            Func<int, double> load = c => aps.Where(a => a.Band == band && a.Channel > 0 && !string.Equals(a.Bssid, myBssid, StringComparison.OrdinalIgnoreCase) && (a.Channel == c || LinkInfo.Overlaps(a.Channel, c, band))).Sum(a => Math.Pow(Math.Max(0, a.Signal) / 100.0, 2));
            int best = cand.OrderBy(c => load(c)).ThenBy(c => c == mine ? 0 : 1).First();
            return new[] { best, (int)Math.Round(load(best) * 100), (int)Math.Round(load(mine) * 100) };
        }
        public static void FromFreq(Bss b)
        {
            int f = b.FreqMHz;
            if (f >= 2400 && f < 2500) { b.Band = "2.4 GHz"; b.Channel = f == 2484 ? 14 : (f - 2407) / 5; }
            else if (f >= 5150 && f < 5925) { b.Band = "5 GHz"; b.Channel = (f - 5000) / 5; }
            else if (f >= 5925 && f < 7125) { b.Band = "6 GHz"; b.Channel = f == 5935 ? 2 : Math.Max(1, (f - 5950) / 5); }   // 5935 MHz is 6 GHz channel 2 (the one below the regular grid)
        }
        // Information elements: HT (61), VHT (192) and HE 6 GHz operation give the channel width;
        // RM capabilities (70) = 802.11k, extended capability bit 19 = 802.11v BSS transition, mobility domain (54) = 802.11r.
        public static void ReadIes(byte[] ies, Bss b)
        {
            for (int i = 0; i + 2 <= ies.Length; )
            {
                int id = ies[i], len = ies[i + 1], p = i + 2; if (p + len > ies.Length) break;
                if (id == 61 && len >= 2 && (ies[p + 1] & 0x04) != 0) b.Width = Math.Max(b.Width, 40);
                else if (id == 192 && len >= 3)
                {
                    int w = ies[p], c0 = ies[p + 1], c1 = ies[p + 2];
                    if (w == 1) b.Width = Math.Max(b.Width, c1 != 0 && Math.Abs(c1 - c0) == 8 ? 160 : 80);
                    else if (w == 2 || w == 3) b.Width = Math.Max(b.Width, 160);
                }
                else if (id == 255 && len >= 7 && ies[p] == 36)   // HE Operation
                {
                    int prm = ies[p + 1] | (ies[p + 2] << 8) | (ies[p + 3] << 16);
                    int q = p + 1 + 3 + 1 + 2;                        // params, BSS colour, basic HE-MCS
                    if ((prm & (1 << 14)) != 0) q += 3;               // VHT operation info present
                    if ((prm & (1 << 15)) != 0) q += 1;               // co-hosted BSS
                    if ((prm & (1 << 17)) != 0 && q + 2 <= p + len)   // 6 GHz operation info
                    { int cw = ies[q + 1] & 0x03; b.Width = Math.Max(b.Width, cw == 0 ? 20 : cw == 1 ? 40 : cw == 2 ? 80 : 160); }
                }
                else if (id == 255 && len >= 7 && ies[p] == 106)   // EHT Operation (Wi-Fi 7): the width, 320 MHz included
                {
                    int prm = ies[p + 1], q = p + 1 + 1 + 4;          // params, basic EHT-MCS and NSS set
                    if ((prm & 0x01) != 0 && q < p + len) { int cw = ies[q] & 0x07; b.Width = Math.Max(b.Width, cw == 0 ? 20 : cw == 1 ? 40 : cw == 2 ? 80 : cw == 3 ? 160 : cw == 4 ? 320 : 20); }
                }
                else if (id == 48 && len >= 8) b.Sec = Rsn(ies, p, len);
                else if (id == 221 && len >= 4 && ies[p] == 0x00 && ies[p + 1] == 0x50 && ies[p + 2] == 0xF2 && ies[p + 3] == 1 && b.Sec == "") b.Sec = "WPA (old)";
                else if (id == 70) b.K = true;
                else if (id == 54) b.R = true;
                else if (id == 127 && len >= 3 && (ies[p + 2] & 0x08) != 0) b.V = true;
                i = p + len;
            }
        }
    }

    // The ARP table: devices your PC has talked to on the local network (nothing is scanned).
    static class Arp
    {
        [DllImport("iphlpapi.dll")] static extern int GetIpNetTable(IntPtr table, ref int size, bool order);
        public sealed class Entry { public string Ip = "", Mac = ""; public int IfIndex; public bool Dynamic; }
        public static List<Entry> Table()
        {
            var list = new List<Entry>(); int size = 0;
            int rc0 = GetIpNetTable(IntPtr.Zero, ref size, true);
            if (rc0 != 122 && rc0 != 0) return list;
            if (size <= 0) return list;
            size += 1024; IntPtr buf = Marshal.AllocHGlobal(size);
            try
            {
                if (GetIpNetTable(buf, ref size, true) != 0) return list;
                int n = Marshal.ReadInt32(buf);
                for (int i = 0; i < n && i < 4000; i++)
                {
                    IntPtr r = buf + 4 + i * 24;   // MIB_IPNETROW: index, physLen, phys[8], addr, type
                    int len = Math.Min(8, Marshal.ReadInt32(r, 4)); var mac = new byte[len]; Marshal.Copy(r + 8, mac, 0, len);
                    var ip = new IPAddress((uint)Marshal.ReadInt32(r, 16)); int type = Marshal.ReadInt32(r, 20);
                    if (len != 6 || mac.All(x => x == 0) || mac.All(x => x == 0xff) || (mac[0] & 1) != 0) continue;   // skip empty, broadcast, multicast
                    list.Add(new Entry { Ip = ip.ToString(), Mac = string.Join(":", mac.Select(x => x.ToString("x2", CultureInfo.InvariantCulture))), IfIndex = Marshal.ReadInt32(r, 0), Dynamic = type == 3 });
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
            return list;
        }
        // Phones and laptops often use a random ("locally administered") address per network for privacy.
        public static bool Randomised(string mac) { int b; if (mac == null || mac.Length < 2 || !int.TryParse(mac.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b)) return false; return (b & 0x02) != 0; }
        public static string MacOf(string ip) { var e = Table().FirstOrDefault(x => x.Ip == ip); return e != null ? e.Mac : ""; }
    }

    // Ask one specific DNS server (used only during a check you start, to spot filtered or tampered answers).
    static class DnsAsk
    {
        public static List<string> Ask(string name, string server, int timeoutMs, out string err)
        {
            err = ""; IPAddress srv;
            if (!IPAddress.TryParse(server, out srv)) { err = "bad server"; return new List<string>(); }
            if (srv.AddressFamily == AddressFamily.InterNetworkV6)   // Windows' DnsQuery takes IPv4 servers only: ask an IPv6 one ourselves
            {
                var t6 = Task.Factory.StartNew(() => Toolkit.Query(srv, name, 1, true));
                if (!t6.Wait(timeoutMs) || t6.Result == null) { err = "no answer"; return new List<string>(); }
                if (t6.Result.Rcode == 3) return new List<string> { "NXDOMAIN" };
                return t6.Result.Rrs.Where(r => r.Section == 1 && r.Type == "A").Select(r => r.Data).ToList();
            }
            var t = Task.Factory.StartNew(() =>
            {
                var res = new List<string>(); IntPtr arr = Marshal.AllocHGlobal(8), recs = IntPtr.Zero;
                try
                {
                    Marshal.WriteInt32(arr, 0, 1); Marshal.Copy(srv.GetAddressBytes(), 0, arr + 4, 4);   // IP4_ARRAY { count, address }
                    int rc = Native.DnsQuery(name, 1, 0x8 | 0x100 /*bypass cache, wire only*/, arr, out recs, IntPtr.Zero);
                    if (rc == 9003) { res.Add("NXDOMAIN"); return res; }
                    if (rc != 0 || recs == IntPtr.Zero) { res.Add("ERR" + rc); return res; }
                    int P = IntPtr.Size;
                    for (IntPtr r = recs; r != IntPtr.Zero; r = Marshal.ReadIntPtr(r, 0))
                        if ((Marshal.ReadInt16(r, 2 * P) & 0xFFFF) == 1) res.Add(new IPAddress((uint)Marshal.ReadInt32(r, 2 * P + 16)).ToString());
                }
                finally { if (recs != IntPtr.Zero) Native.DnsRecordListFree(recs, 1); Marshal.FreeHGlobal(arr); }
                return res;
            });
            if (!t.Wait(timeoutMs)) { err = "no answer"; return new List<string>(); }
            if (t.Result.Count == 1 && t.Result[0].StartsWith("ERR", StringComparison.Ordinal)) { err = "no answer"; return new List<string>(); }
            return t.Result;
        }
    }

    // Driver version and date for the network adapter, from the registry (readable without admin).
    static class DriverInfo
    {
        public static string[] Of(string nicId)
        {
            try
            {
                using (var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}"))
                {
                    if (cls == null) return null;
                    foreach (var sub in cls.GetSubKeyNames())
                    {
                        if (sub.Length != 4) continue;
                        try
                        {
                            using (var k = cls.OpenSubKey(sub))
                            {
                                if (k == null || !string.Equals(k.GetValue("NetCfgInstanceId") as string, nicId, StringComparison.OrdinalIgnoreCase)) continue;
                                return new[] { k.GetValue("DriverVersion") as string ?? "", k.GetValue("DriverDate") as string ?? "", k.GetValue("ProviderName") as string ?? "" };
                            }
                        }
                        catch (System.Security.SecurityException) { }
                    }
                }
            }
            catch (Exception e) { Program.Log("driver: " + e.Message); }
            return null;
        }
        static readonly string[] DateFormats = { "M-d-yyyy", "MM-dd-yyyy" };
        public static double AgeYears(string date)
        {
            DateTime d; return DateTime.TryParseExact(date, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? Math.Max(-1, (DateTime.Now - d).TotalDays / 365.25) : -1;   /* dated in the future: unknown (-1), never "this year" */
        }
    }

    // ================================================================== Your connection: Wi-Fi radio, neighbours, cable, addressing
    // Everything comes from Windows itself (netsh and the adapter statistics) - nothing is sent anywhere. Collected only while
    // the "Your connection" page is open, every 10 s.
    sealed class Ap { public string Ssid = "", Bssid = "", Band = "", Radio = "", Sec = ""; public int Signal = -1, Channel = -1, Util = -1, Stations = -1, Rssi, Width; }

    sealed class LinkSnap
    {
        public bool Wifi, LocationBlocked; public string Verdict = "", Adapter = ""; public int Level;
        public List<string[]> Radio = new List<string[]>(), Quality = new List<string[]>(), Address = new List<string[]>(), Traffic = new List<string[]>();
        public List<Ap> Aps = new List<Ap>(); public int MyChannel = -1; public string MyBand = "", MyBssid = "";
        public List<string[]> Drops = new List<string[]>(), Dhcp = new List<string[]>(), Lan = new List<string[]>(); public string DropsErr = "", DhcpErr = "";
        public DateTime At = DateTime.Now;
    }

    static class LinkInfo
    {
        static string[] Row(params string[] v) { return v; }
        static readonly Regex kv = new Regex(@"^\s*([^:]+?)\s*:\s*(.*?)\s*$");
        public static Dictionary<string, string> Fields(string text)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int rates = 0;
            foreach (var line in (text ?? "").Split('\n'))
            {
                var m = kv.Match(line); if (!m.Success) continue; string k = NormKey(m.Groups[1].Value);
                if (k != "Receive rate (Mbps)" && k != "Transmit rate (Mbps)") { int rk = RateKind(m.Groups[1].Value); if (rk == 3) rk = rates == 0 ? 1 : 2; if (rk > 0) { rates++; k = rk == 1 ? "Receive rate (Mbps)" : "Transmit rate (Mbps)"; } }
                if (!d.ContainsKey(k)) d[k] = m.Groups[2].Value;
            }
            return d;
        }
        // netsh's labels are translated: the common languages' words for the ones read here become the English label
        // (English, French, German, Spanish, Italian, Portuguese, Dutch, Polish; anything else is kept as it is)
        static readonly Dictionary<string, string> KeyWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Russian, Japanese, Chinese (simplified and traditional), Korean, Arabic, Turkish - the per-network labels
            { "сигнал", "Signal" }, { "シグナル", "Signal" }, { "信号", "Signal" }, { "訊號", "Signal" }, { "信號", "Signal" }, { "신호", "Signal" }, { "الإشارة", "Signal" }, { "الاشارة", "Signal" }, { "sinyal", "Signal" },
            { "канал", "Channel" }, { "チャネル", "Channel" }, { "チャンネル", "Channel" }, { "信道", "Channel" }, { "通道", "Channel" }, { "채널", "Channel" }, { "القناة", "Channel" },
            { "диапазон", "Band" }, { "バンド", "Band" }, { "波段", "Band" }, { "频带", "Band" }, { "頻帶", "Band" }, { "대역", "Band" }, { "النطاق", "Band" }, { "bant", "Band" },
            { "тип радиомодуля", "Radio type" }, { "無線の種類", "Radio type" }, { "无线电类型", "Radio type" }, { "無線電類型", "Radio type" }, { "무선 유형", "Radio type" }, { "نوع الراديو", "Radio type" }, { "radyo türü", "Radio type" }, { "tipo frequenza radio", "Radio type" },
            { "проверка подлинности", "Authentication" }, { "認証", "Authentication" }, { "身份验证", "Authentication" }, { "驗證", "Authentication" }, { "인증", "Authentication" }, { "المصادقة", "Authentication" }, { "kimlik doğrulama", "Authentication" },
            { "шифр", "Cipher" }, { "шифрование", "Cipher" }, { "暗号", "Cipher" }, { "密码", "Cipher" }, { "加密", "Cipher" }, { "암호", "Cipher" }, { "التشفير", "Cipher" }, { "şifre", "Cipher" },
            { "signal", "Signal" }, { "señal", "Signal" }, { "segnale", "Signal" }, { "sinal", "Signal" }, { "signaal", "Signal" }, { "sygnał", "Signal" },
            { "channel", "Channel" }, { "canal", "Channel" }, { "kanal", "Channel" }, { "canale", "Channel" }, { "kanaal", "Channel" }, { "kanał", "Channel" },
            { "band", "Band" }, { "bande", "Band" }, { "banda", "Band" }, { "pasmo", "Band" },
            { "radio type", "Radio type" }, { "type de radio", "Radio type" }, { "funktyp", "Radio type" }, { "tipo de radio", "Radio type" }, { "tipo radio", "Radio type" }, { "tipo de rádio", "Radio type" }, { "radiotype", "Radio type" }, { "typ radia", "Radio type" },
            { "authentication", "Authentication" }, { "authentification", "Authentication" }, { "authentifizierung", "Authentication" }, { "autenticación", "Authentication" }, { "autenticazione", "Authentication" }, { "autenticação", "Authentication" }, { "verificatie", "Authentication" }, { "uwierzytelnianie", "Authentication" },
            { "cipher", "Cipher" }, { "chiffrement", "Cipher" }, { "verschlüsselung", "Cipher" }, { "cifrado", "Cipher" }, { "crittografia", "Cipher" }, { "codificação", "Cipher" }, { "cifra", "Cipher" }, { "codering", "Cipher" }, { "szyfr", "Cipher" }
        };
        // the receive / transmit rate labels in any language: a rate line has its unit in the label ("(Mbps)", "(Mbit/s)", "(Мбит/с)")
        // and a word for which way; 0 = not a rate line, 3 = a rate line that doesn't say which way (the caller takes receive first)
        static readonly string[] RxWords = { "receive", "réception", "empfang", "recepción", "ricezione", "recepção", "ontvang", "odbier", "прием", "приём", "受信", "接收", "수신", "استقبال", "alma" };
        static readonly string[] TxWords = { "transmit", "transmission", "übertragung", "transmisión", "trasmissione", "transmissão", "verzend", "wysył", "передач", "送信", "传输", "發送", "傳送", "송신", "إرسال", "gönderme", "iletim" };
        internal static int RateKind(string label)
        {
            string l = (label ?? "").ToLowerInvariant();
            bool unit = l.Contains("(mb") || l.Contains("mbps") || l.Contains("mbit") || l.Contains("мбит") || l.Contains("mb/s");
            if (RxWords.Any(w => l.Contains(w))) return unit || l.Contains("rate") ? 1 : 0;
            if (TxWords.Any(w => l.Contains(w))) return unit || l.Contains("rate") ? 2 : 0;
            return unit ? 3 : 0;
        }
        internal static double RateOf(string v) { var m = Regex.Match(v ?? "", @"\d+(?:[.,]\d+)?"); double r; return m.Success && double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : -1; }
        // "2,4 GHz", "2.4 GHz", "2,4 ГГц", "5 GHz" -> "2.4 GHz" / "5 GHz" / "6 GHz": the band by its leading digit, in any language
        public static string NormBand(string v)
        {
            var m = Regex.Match(v ?? "", @"^\s*(2\s*[.,]\s*4|5|6)(?![0-9])");
            if (!m.Success) return "";
            return m.Groups[1].Value.StartsWith("2", StringComparison.Ordinal) ? "2.4 GHz" : m.Groups[1].Value + " GHz";
        }
        internal static string NormKey(string k)
        {
            string t = (k ?? "").Trim(), w; if (KeyWords.TryGetValue(t, out w)) return w;
            string l = t.ToLowerInvariant();
            if (l.StartsWith("receive rate", StringComparison.Ordinal) || l.StartsWith("vitesse de réception", StringComparison.Ordinal) || l.StartsWith("empfangsrate", StringComparison.Ordinal) || l.StartsWith("velocidad de recepción", StringComparison.Ordinal) || l.StartsWith("taxa de recepção", StringComparison.Ordinal) || l.StartsWith("velocità di ricezione", StringComparison.Ordinal)) return "Receive rate (Mbps)";
            if (l.StartsWith("transmit rate", StringComparison.Ordinal) || l.StartsWith("vitesse de transmission", StringComparison.Ordinal) || l.StartsWith("übertragungsrate", StringComparison.Ordinal) || l.StartsWith("velocidad de transmisión", StringComparison.Ordinal) || l.StartsWith("taxa de transmissão", StringComparison.Ordinal) || l.StartsWith("velocità di trasmissione", StringComparison.Ordinal)) return "Transmit rate (Mbps)";
            return t;
        }
        // netsh lists each Wi-Fi adapter as its own block (blank line between): keep the one that's connected - the one with
        // a signal %, or whose state reads connected in any language
        internal static string ActiveBlock(string text)
        {
            var blocks = Regex.Split(text ?? "", @"\r?\n\s*\r?\n").Where(b => b.IndexOf(':') >= 0).ToList();
            if (blocks.Count <= 1) return text ?? "";
            var best = blocks.FirstOrDefault(b => Regex.IsMatch(b, @":\s*(\d{1,3}\s*%|%\s*\d{1,3})", RegexOptions.Multiline)) ?? blocks.FirstOrDefault(b => b.Split('\n').Any(l => { int c = l.IndexOf(':'); return c > 0 && Monitor.NormState(l.Substring(c + 1)) == "connected"; }));
            return best ?? text;
        }
        public static bool Blocked(string text) { return text != null && text.IndexOf("location", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0; }
        public static string BandOf(int ch, string band)
        {
            if (!string.IsNullOrEmpty(band)) return band;
            if (ch >= 1 && ch <= 14) return "2.4 GHz"; if (ch >= 32 && ch <= 177) return "5 GHz"; return "";
        }
        public static string Gen(string radio)
        {
            radio = (radio ?? "").ToLowerInvariant();
            if (radio.Contains("be")) return "Wi-Fi 7"; if (radio.Contains("ax")) return "Wi-Fi 6 / 6E"; if (radio.Contains("ac")) return "Wi-Fi 5";
            if (radio.Contains("802.11n")) return "Wi-Fi 4"; if (radio != "") return "older Wi-Fi"; return "";
        }
        // netsh wlan show networks mode=bssid -> one Ap per BSSID
        public static List<Ap> ParseNetworks(string text)
        {
            var list = new List<Ap>(); string ssid = ""; Ap cur = null;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var m = kv.Match(raw); if (!m.Success) continue;
                string k = NormKey(m.Groups[1].Value), v = m.Groups[2].Value; int x;
                if (Regex.IsMatch(k, @"^SSID \d+$")) { ssid = Program.Plain(v); cur = null; }
                else if (Regex.IsMatch(k, @"^BSSID \d+$")) { cur = new Ap { Ssid = ssid == "" ? "(hidden network)" : ssid, Bssid = v }; list.Add(cur); }
                else if (cur == null) continue;
                else if (k == "Signal" && int.TryParse(v.TrimEnd('%', ' ', '\u00A0', '\u202F', '\t'), NumberStyles.Integer, CultureInfo.InvariantCulture, out x)) cur.Signal = x;
                else if (k == "Channel" && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out x)) cur.Channel = x;
                else if (k == "Band") cur.Band = NormBand(v);
                else if (cur.Signal < 0 && Regex.IsMatch(v, @"^(\d{1,3}\s*%|%\s*\d{1,3})$")) { var pm = Regex.Match(v, @"\d{1,3}"); int.TryParse(pm.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out x); if (x <= 100) cur.Signal = x; }   // a label we don't know: a "74%" is the signal
                else if (cur.Band == "" && Regex.IsMatch(v, @"^\s*(2\s*[.,]\s*4|5|6)\s*(GHz|ГГц|ギガヘルツ|千兆赫)", RegexOptions.IgnoreCase)) cur.Band = NormBand(v);
                else if (k == "Radio type") cur.Radio = v;
                else if (k == "Connected Stations" && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out x)) cur.Stations = x;
                else if (k == "Channel Utilization") { var um = Regex.Match(v, @"\((\d+)\s*%\)"); int u; if (um.Success && int.TryParse(um.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out u)) cur.Util = Math.Min(100, u); }
                if (cur != null && cur.Band == "" && cur.Channel > 0) cur.Band = BandOf(cur.Channel, "");
            }
            if (list.Count > 300) list = list.OrderByDescending(a => a.Signal).Take(300).ToList();
            return list;
        }
        public static bool Overlaps(int a, int b, string band)
        {
            if (a <= 0 || b <= 0) return false;
            return band.StartsWith("2.4", StringComparison.Ordinal) ? Math.Abs(a - b) < 5 : a == b;
        }
        static string Rate(long bps) { return bps <= 0 ? "-" : bps >= 1000000000 ? (bps / 1e9).ToString("0.#", CultureInfo.CurrentCulture) + " Gb/s" : (bps / 1e6).ToString("0", CultureInfo.CurrentCulture) + " Mb/s"; }
        static string Count(long v) { return v.ToString("N0", CultureInfo.CurrentCulture); }

        static readonly Dictionary<string, string> lanNames = new Dictionary<string, string>(); static DateTime lanAt = DateTime.MinValue;
        public static LinkSnap Collect(Monitor m)
        {
            var s = new LinkSnap(); NetworkInterface nic; string kind, gw;
            lock (m.L) { nic = m.Primary; kind = m.AdapterKind; gw = m.Gateway; }
            bool noGw = false;
            if (nic == null)
            {
                try
                {
                    nic = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel && !Monitor.IsVpn(n))
                        .OrderByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211).FirstOrDefault();
                    if (nic != null) { noGw = true; kind = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "LAN"; }
                }
                catch { nic = null; }
            }
            if (nic == null) { s.Verdict = Voice.P("No network connection at all, boss. Plug in a cable or pick a Wi-Fi network.", "Not connected."); s.Level = 2; return s; }
            s.Wifi = kind == "Wi-Fi"; s.Adapter = nic.Description;
            int lv = 0; var notes = new List<string>(); bool unknownAir = false;

            // ---- radio (Wi-Fi) or cable
            if (s.Wifi)
            {
                string iface = "", nets = "";
                try { iface = Job.Run(Job.Netsh, "wlan show interfaces", 4000); } catch { }
                try { nets = Job.Run(Job.Netsh, "wlan show networks mode=bssid", 6000); } catch { }
                if (Blocked(iface) || Blocked(nets) || m.WifiBlocked) s.LocationBlocked = true;   // m.WifiBlocked: Windows' Wi-Fi service said so - in any language
                iface = ActiveBlock(iface);   // two Wi-Fi adapters: the connected one's lines only, never a mix of both
                var f = Fields(iface); string v; int ch = -1, sig = -1;
                if (f.TryGetValue("Channel", out v)) int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out ch);
                if (f.TryGetValue("Signal", out v)) int.TryParse(v.TrimEnd('%', ' ', '\u00A0', '\u202F', '\t'), NumberStyles.Integer, CultureInfo.InvariantCulture, out sig);
                // Windows in another language: the same reading the corner widget uses (it finds the % and the channel by shape)
                int psig, prate; string pssid, pch, pbssid, pstate; Monitor.ParseWifi(iface, out psig, out pssid, out pch, out pbssid, out pstate, out prate);
                if (sig < 0) sig = psig; if (ch < 0) int.TryParse(pch, NumberStyles.Integer, CultureInfo.InvariantCulture, out ch); if (ch == 0) ch = -1;
                // the band from Windows' Wi-Fi API when it knows this access point (6 GHz channel 5 isn't 2.4 GHz channel 5)
                string apiBand = "";
                string myB = f.TryGetValue("AP BSSID", out v) ? v : f.TryGetValue("BSSID", out v) ? v : pbssid;
                if ((!f.ContainsKey("Band") || NormBand(f["Band"]) == "") && myB != "") { string werr; var me = WlanApi.List(false, out werr).FirstOrDefault(b => string.Equals(b.Bssid, myB.Replace('-', ':'), StringComparison.OrdinalIgnoreCase)); if (me != null) { apiBand = me.Band; if (ch < 0) ch = me.Channel; } }
                string band = BandOf(ch, f.TryGetValue("Band", out v) && NormBand(v) != "" ? NormBand(v) : apiBand), radio = f.TryGetValue("Radio type", out v) ? v : "";
                string bssid = f.TryGetValue("AP BSSID", out v) ? v : f.TryGetValue("BSSID", out v) ? v : pbssid;
                if (s.Wifi && sig < 0 && ch < 0) unknownAir = true;
                s.MyChannel = ch; s.MyBand = band; s.MyBssid = bssid;
                Func<string, string> F = k => { string x; return f.TryGetValue(k, out x) && x != "" ? x : "-"; };
                string rssi = f.TryGetValue("Rssi", out v) ? v + " dBm" : sig >= 0 ? "about " + (sig / 2 - 100) + " dBm" : "-";
                s.Radio.Add(new[] { "Network (SSID)", F("SSID") });
                s.Radio.Add(new[] { "Access point (BSSID)", bssid == "" ? "-" : bssid });
                s.Radio.Add(new[] { "Band", band == "" ? "-" : band, band.StartsWith("2.4", StringComparison.Ordinal) ? "1" : "0" });
                s.Radio.Add(new[] { "Channel", ch > 0 ? ch.ToString(CultureInfo.CurrentCulture) : "-" });
                s.Radio.Add(new[] { "Standard", radio == "" ? "-" : radio + (Gen(radio) != "" ? " (" + Gen(radio) + ")" : "") });
                s.Radio.Add(new[] { "Signal", sig >= 0 ? sig + "%  ·  " + rssi : "-", sig < 0 ? "0" : sig < 40 ? "2" : sig < 60 ? "1" : "0" });
                s.Radio.Add(new[] { "Receive rate", F("Receive rate (Mbps)") == "-" ? "-" : F("Receive rate (Mbps)") + " Mb/s" });
                s.Radio.Add(new[] { "Transmit rate", F("Transmit rate (Mbps)") == "-" ? "-" : F("Transmit rate (Mbps)") + " Mb/s" });
                s.Radio.Add(new[] { "Security", F("Authentication") + " · " + F("Cipher"), F("Authentication").IndexOf("WEP", StringComparison.OrdinalIgnoreCase) >= 0 || F("Authentication") == "Open" ? "2" : "0" });
                if (sig >= 0 && sig < 40) { lv = 2; notes.Add(Voice.P("Your Wi-Fi signal is weak (" + sig + "%). Walls are winning - move closer to the access point or plug in a cable.", "Weak Wi-Fi signal (" + sig + "%).")); }
                else if (sig >= 0 && sig < 60) { lv = Math.Max(lv, 1); notes.Add("Signal is so-so (" + sig + "%). Fine for browsing; calls may stutter."); }

                s.Aps = ParseNetworks(nets);
                if (ch > 0)
                {
                    var same = s.Aps.Where(a => !string.Equals(a.Bssid, bssid, StringComparison.OrdinalIgnoreCase) && a.Band == band && Overlaps(a.Channel, ch, band)).ToList();
                    var mine = s.Aps.FirstOrDefault(a => string.Equals(a.Bssid, bssid, StringComparison.OrdinalIgnoreCase));
                    int strong = same.Count(a => a.Signal >= 50);
                    s.Radio.Add(new[] { "Sharing your channel", same.Count == 0 ? "nobody - clean air" : same.Count + " other access point" + (same.Count > 1 ? "s" : "") + (strong > 0 ? " (" + strong + " strong)" : ""), strong >= 3 ? "2" : same.Count >= 3 ? "1" : "0" });
                    if (mine != null && mine.Util >= 0) s.Radio.Add(new[] { "Air time used", mine.Util + "% of the channel" + (mine.Stations >= 0 ? " · " + mine.Stations + " devices on this AP" : ""), mine.Util >= 70 ? "2" : mine.Util >= 45 ? "1" : "0" });
                    if (strong >= 3) { lv = Math.Max(lv, 1); notes.Add(Voice.P("Your channel is a crowded room: " + same.Count + " neighbours share it. Everyone takes turns, so everyone is slower.", same.Count + " access points share your channel.")); }
                    // Windows' Native Wifi API: channel width, roaming helpers, exact dBm
                    string werr; var bss = WlanApi.List(true, out werr);
                    foreach (var a in s.Aps) { var m2 = bss.FirstOrDefault(x => string.Equals(x.Bssid, a.Bssid, StringComparison.OrdinalIgnoreCase)); if (m2 != null) { a.Rssi = m2.Rssi; a.Width = m2.Width; a.Sec = m2.Sec; } }
                    var me = bss.FirstOrDefault(x => string.Equals(x.Bssid, bssid, StringComparison.OrdinalIgnoreCase));
                    if (me != null)
                    {
                        s.Radio.Insert(4, new[] { "Channel width", me.Width + " MHz" + (me.Width >= 80 ? " - a wide, fast lane" : me.Width == 40 ? "" : " - narrow, so slower but sturdier"), "0" });
                        var help = new List<string>(); if (me.K) help.Add("11k"); if (me.V) help.Add("11v"); if (me.R) help.Add("11r fast roaming");
                        s.Radio.Add(new[] { "Roaming help", help.Count == 0 ? "none advertised - roaming is up to your laptop alone" : string.Join(" · ", help), help.Count == 0 ? "1" : "0" });
                        int idx = s.Radio.FindIndex(r => r[0] == "Signal");
                        if (idx >= 0 && me.Rssi < 0) s.Radio[idx] = new[] { "Signal", sig + "%  ·  " + me.Rssi + " dBm", me.Rssi < -75 ? "2" : me.Rssi < -67 ? "1" : "0" };
                    }
                    if (band.StartsWith("2.4", StringComparison.Ordinal) && s.Aps.Any(a => a.Ssid == F("SSID") && a.Band != "" && !a.Band.StartsWith("2.4", StringComparison.Ordinal)))
                        notes.Add(Voice.P("You're on 2.4 GHz, but your network also offers a faster band. Moving a bit closer usually lets Windows switch.", "Your network also has 5/6 GHz; 2.4 GHz is slower and more crowded."));
                }
            }
            else
            {
                s.Radio.Add(Row("Connection", "Cable (Ethernet)"));
                s.Radio.Add(new[] { "Adapter", nic.Description });
                s.Radio.Add(new[] { "Link speed", Rate(nic.Speed), nic.Speed > 0 && nic.Speed < 1000000000 ? "1" : "0" });
                if (nic.Speed > 0 && nic.Speed <= 100000000) { lv = Math.Max(lv, 1); notes.Add(Voice.P("Your cable only negotiated " + Rate(nic.Speed) + ". A worn cable, an old switch port or a dock is holding you back - gigabit is normal.", "Link negotiated at " + Rate(nic.Speed) + " (gigabit expected).")); }
            }

            // ---- addressing
            try
            {
                var ip = nic.GetIPProperties();
                var v4 = ip.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                var v6 = ip.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6 && !a.Address.IsIPv6LinkLocal).Select(a => a.Address.ToString()).FirstOrDefault();
                s.Address.Add(new[] { "IPv4 address", v4 == null ? "-" : v4.Address + (v4.IPv4Mask != null ? " / " + v4.IPv4Mask : "") });
                s.Address.Add(new[] { "IPv6", v6 ?? "none (IPv4 only)" });
                s.Address.Add(new[] { "Gateway (router)", gw == "" ? "-" : gw });
                s.Address.Add(new[] { "DNS servers", string.Join(", ", ip.DnsAddresses.OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).Select(a => a.ToString()).Take(4))   /* IPv4 first, then IPv6 */ });
                var p4 = ip.GetIPv4Properties();
                s.Address.Add(new[] { "Address from", p4 != null && p4.IsDhcpEnabled ? "DHCP" + (ip.DhcpServerAddresses.Count > 0 ? " (" + ip.DhcpServerAddresses[0] + ")" : "") : "set by hand (static)" });
                s.Address.Add(new[] { "Packet size (MTU)", p4 != null ? p4.Mtu + " bytes" : "-" });
                string mac = string.Join(":", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
                s.Address.Add(new[] { "Hardware address (MAC)", mac == "" ? "-" : mac });
                if (v4 != null && v4.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal)) { lv = 2; notes.Insert(0, Voice.P("Windows gave itself a 169.254 address - nobody handed you a real one. The router's DHCP isn't answering.", "No DHCP address (169.254.x.x).")); }
            }
            catch (Exception e) { Program.Log("link addr: " + e.Message); }

            // ---- traffic and errors
            try
            {
                var st = nic.GetIPStatistics();
                s.Traffic.Add(new[] { "Received", Draw.MemStr(st.BytesReceived) + " · " + Count(st.UnicastPacketsReceived) + " packets" });
                s.Traffic.Add(new[] { "Sent", Draw.MemStr(st.BytesSent) + " · " + Count(st.UnicastPacketsSent) + " packets" });
                long err = st.IncomingPacketsWithErrors + st.OutgoingPacketsWithErrors, disc = st.IncomingPacketsDiscarded + st.OutgoingPacketsDiscarded;
                s.Traffic.Add(new[] { "Damaged packets (errors)", Count(err), err > 100 ? "1" : "0" });
                s.Traffic.Add(new[] { "Dropped packets (discards)", Count(disc), disc > 1000 ? "1" : "0" });
                if (err > 100) { lv = Math.Max(lv, 1); notes.Add(s.Wifi ? "The adapter has counted " + Count(err) + " damaged packets - radio interference or a driver issue." : "The adapter has counted " + Count(err) + " damaged packets - try another cable or dock port."); }
            }
            catch (Exception e) { Program.Log("link stats: " + e.Message); }

            // ---- driver age
            var drv = DriverInfo.Of(nic.Id);
            if (drv != null && drv[0] != "")
            {
                double age = DriverInfo.AgeYears(drv[1]);
                s.Radio.Add(new[] { "Driver", drv[0] + (drv[1] != "" ? " · " + drv[1] : "") + (age >= 0 ? " (" + (age < 1 ? "this year" : age.ToString("0", CultureInfo.CurrentCulture) + (age < 1.5 ? " year old" : " years old")) + ")" : ""), age >= 3 ? "1" : "0" });
                if (age >= 3) notes.Add(Voice.P("Your " + (s.Wifi ? "Wi-Fi" : "network") + " driver is " + age.ToString("0", CultureInfo.CurrentCulture) + " years old. Old drivers are a top cause of random drops - worth an update from the laptop maker.", "Network driver is " + age.ToString("0", CultureInfo.CurrentCulture) + " years old."));
            }
            // ---- Windows' own diary: drops and DHCP trouble in the last 24 h
            string de;
            if (s.Wifi) { s.Drops = EvLog.WifiDrops(24, out de); s.DropsErr = de; }
            s.Dhcp = EvLog.DhcpProblems(24, out de); s.DhcpErr = de;
            int realDrops = s.Drops.Count(d => !EvLog.ByUser(d[1]));
            if (realDrops >= 3) { lv = Math.Max(lv, 1); notes.Add(Voice.P("Your Wi-Fi dropped " + realDrops + " times in the last day - the reasons are below.", realDrops + " Wi-Fi drops in 24 h.")); }
            // ---- devices on your network (the ARP table - nothing is scanned), names from your router's DNS
            try
            {
                int ifIndex = nic.GetIPProperties().GetIPv4Properties().Index;
                var arp = Arp.Table().Where(e => e.IfIndex == ifIndex && e.Dynamic && DestCheck.Kind(e.Ip) != "public").Take(40).ToList();
                var ptr = new Dictionary<string, string>();
                if ((DateTime.UtcNow - lanAt).TotalMinutes < 5) lock (lanNames) foreach (var kv in lanNames) ptr[kv.Key] = kv.Value;
                var tasks = arp.Where(e => !ptr.ContainsKey(e.Ip)).Select(e => Task.Factory.StartNew(() => { try { return Tuple.Create(e.Ip, Native.PtrName(e.Ip)); } catch { return Tuple.Create(e.Ip, ""); } })).ToArray();
                try { Task.WaitAll(tasks, 2500); } catch (AggregateException) { }
                foreach (var t in tasks) if (t.Status == TaskStatus.RanToCompletion) ptr[t.Result.Item1] = t.Result.Item2;
                lock (lanNames) { if ((DateTime.UtcNow - lanAt).TotalMinutes >= 5) { lanNames.Clear(); lanAt = DateTime.UtcNow; } foreach (var kv in ptr) lanNames[kv.Key] = kv.Value; }
                foreach (var e in arp.OrderBy(e => e.Ip == gw ? 0 : 1).ThenBy(e => e.Ip.Split('.').Select(x => x.PadLeft(3, '0')).Aggregate((a, b) => a + b), StringComparer.Ordinal))
                {
                    string nm; ptr.TryGetValue(e.Ip, out nm);
                    string what = e.Ip == gw ? "your router" : Arp.Randomised(e.Mac) ? "private (random) address - usually a phone or laptop" : "";
                    s.Lan.Add(new[] { string.IsNullOrEmpty(nm) ? e.Ip : nm.Split('.')[0] + "  (" + e.Ip + ")", e.Mac, what });
                }
            }
            catch (Exception ex) { Program.Log("lan: " + ex.Message); }

            // ---- link quality to the router (from the gateway pings already running)
            var pings = m.GwPing.Values(30);
            var good = pings.Where(x => !double.IsNaN(x)).ToArray();
            if (pings.Length > 0)
            {
                double loss = 100.0 * (pings.Length - good.Length) / pings.Length;
                double jit = 0; for (int i = 1; i < good.Length; i++) jit += Math.Abs(good[i] - good[i - 1]); jit = good.Length > 1 ? jit / (good.Length - 1) : 0;
                s.Quality.Add(new[] { "Router answers in", good.Length > 0 ? good.Min().ToString("0", CultureInfo.CurrentCulture) + " / " + good.Average().ToString("0", CultureInfo.CurrentCulture) + " / " + good.Max().ToString("0", CultureInfo.CurrentCulture) + " ms (best / typical / worst)" : "no answer", good.Length > 0 && good.Average() > 30 ? "1" : "0" });
                s.Quality.Add(new[] { "Jitter", jit.ToString("0.0", CultureInfo.CurrentCulture) + " ms", jit > 20 ? "2" : jit > 8 ? "1" : "0" });
                s.Quality.Add(new[] { "Loss", loss.ToString("0", CultureInfo.CurrentCulture) + "%", loss >= 5 ? "2" : loss > 0 ? "1" : "0" });
                s.Quality.Add(new[] { "Based on", good.Length + " of the last " + pings.Length + " router pings" });
                if (loss >= 5) { lv = 2; notes.Insert(0, Voice.P("Packets are getting lost between you and the router (" + loss.ToString("0", CultureInfo.CurrentCulture) + "%). The problem is in the room, not on the internet.", "Loss to the router: " + loss.ToString("0", CultureInfo.CurrentCulture) + "%.")); }
                else if (jit > 20) { lv = Math.Max(lv, 1); notes.Add("Jitter to the router is high (" + jit.ToString("0", CultureInfo.CurrentCulture) + " ms) - calls will feel choppy."); }
            }
            else s.Quality.Add(Row("Router answers in", "router ping is off (Settings)"));

            if (noGw) { lv = 2; notes.Insert(0, Voice.P("You're connected to the network, but there's no router (gateway) to reach anything beyond it. Usually DHCP failed, or the network is local-only.", "Connected, but no default gateway.")); }
            s.Level = lv;
            s.Verdict = notes.Count > 0 ? string.Join("\n", notes.Take(3)) : unknownAir ? "Windows didn't say the signal or channel, so the air can't be judged - only the router's answers above are measured." : s.Wifi ? Voice.P("Your Wi-Fi is in great shape - strong signal, clean air, quick router.", "Wi-Fi link looks healthy.") : Voice.P("Cable, full speed, no errors. The steadiest link there is.", "Wired link looks healthy.");
            if (s.LocationBlocked) { s.Level = Math.Max(s.Level, 1); s.Verdict = Voice.P("Windows is hiding the Wi-Fi details until you allow location for desktop apps (Settings > Privacy & security > Location > Let desktop apps access your location). Annoying, but that's how Windows 11 protects where you are.", "Wi-Fi details need location access: Settings > Privacy & security > Location > Let desktop apps access your location.") + (notes.Count > 0 ? "\n" + notes[0] : ""); }
            return s;
        }
    }
}
