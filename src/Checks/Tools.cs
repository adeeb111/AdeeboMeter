// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== the routing table (IPv4), read without admin
    static class Routes
    {
        public sealed class R { public uint Dest, Mask; public string Gateway = "", Iface = ""; public int IfIndex, Metric, Prefix; public bool OnLink, Vpn; }
        [DllImport("iphlpapi.dll")] static extern int GetIpForwardTable(IntPtr table, ref int size, bool order);
        [DllImport("iphlpapi.dll")] static extern int GetBestInterface(uint dest, out uint ifIndex);
        static uint U(IPAddress a) { var b = a.GetAddressBytes(); return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]); }
        public static string Ip(uint v) { return ((v >> 24) & 255) + "." + ((v >> 16) & 255) + "." + ((v >> 8) & 255) + "." + (v & 255); }
        public static Dictionary<int, NetworkInterface> Nics()
        {
            var d = new Dictionary<int, NetworkInterface>();
            try { foreach (var n in NetworkInterface.GetAllNetworkInterfaces()) try { var p = n.GetIPProperties().GetIPv4Properties(); if (p != null) d[p.Index] = n; } catch { } } catch { }
            return d;
        }
        public static List<R> Read()
        {
            var list = new List<R>(); int size = 0; var nics = Nics();
            if (GetIpForwardTable(IntPtr.Zero, ref size, true) != 122 || size <= 0) return list;
            size += 4096; IntPtr buf = Marshal.AllocHGlobal(size);
            try
            {
                if (GetIpForwardTable(buf, ref size, true) != 0) return list;
                int n = Marshal.ReadInt32(buf);
                for (int i = 0; i < n && i < 5000; i++)
                {
                    IntPtr r = buf + 4 + i * 56;   // MIB_IPFORWARDROW: 14 DWORDs
                    Func<int, uint> D = o => (uint)IPAddress.NetworkToHostOrder(Marshal.ReadInt32(r, o));
                    var row = new R { Dest = D(0), Mask = D(4), IfIndex = Marshal.ReadInt32(r, 16), Metric = Marshal.ReadInt32(r, 36) };
                    uint hop = D(12); row.OnLink = hop == 0 || Marshal.ReadInt32(r, 20) == 3; row.Gateway = row.OnLink ? "on this network" : Ip(hop);
                    uint m = row.Mask; int p = 0; while ((m & 0x80000000) != 0) { p++; m <<= 1; } row.Prefix = p;
                    NetworkInterface nic; if (nics.TryGetValue(row.IfIndex, out nic)) { row.Iface = nic.Name; row.Vpn = Monitor.IsVpn(nic); }
                    list.Add(row);
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
            return list;
        }
        // Which route wins for this address, and every route that matched (longest prefix first, then lowest metric).
        public static R Best(List<R> table, IPAddress a, out List<R> matched)
        {
            uint ip = U(a);
            matched = table.Where(r => (ip & r.Mask) == (r.Dest & r.Mask)).OrderByDescending(r => r.Prefix).ThenBy(r => r.Metric).ToList();
            return matched.FirstOrDefault();
        }
        // Windows' own answer for which interface it would use (same rules, plus things like VPN policy)
        public static int WindowsPick(IPAddress a)
        {
            try { uint idx; var b = a.GetAddressBytes(); return GetBestInterface(BitConverter.ToUInt32(b, 0), out idx) == 0 ? (int)idx : -1; } catch { return -1; }
        }
        public static string Plain(R r)
        {
            if (r == null) return "No route - Windows has nowhere to send it.";
            string what = r.Prefix == 0 ? "the default route (\"everything else\")" : r.Prefix == 32 ? "a route for exactly this one address" : "the route for " + Ip(r.Dest) + "/" + r.Prefix;
            return "Uses " + what + ", out of " + (r.Iface == "" ? "interface " + r.IfIndex : r.Iface) + (r.Vpn ? " (your VPN)" : "") + ", " + (r.OnLink ? "straight to the destination on this network" : "via the router " + r.Gateway) + " (metric " + r.Metric + ").";
        }
    }

    // ================================================================== on-demand tools on the Health page
    // Each runs only when you press its button, and stops by itself. Results are { level, name, detail, plain-English advice }.
    static class Tools
    {
        static readonly string[] NoWifi = { "3", "Wi-Fi name, signal and channel", "no Wi-Fi adapter", "" }, DnsFine = { "0", "Verdict", "Your DNS is as quick as the public ones", "Nothing to change." }, NoVpn = { "3", "VPN", "not connected", "Everything goes straight out through your own connection." };
        static readonly string[][] Public = { new[] { "1.1.1.1", "Cloudflare 1.1.1.1" }, new[] { "8.8.8.8", "Google 8.8.8.8" }, new[] { "9.9.9.9", "Quad9 9.9.9.9" } };
        // ---------- what this PC lets AdeeboMeter see (works without admin; on a work PC, IT decides some of these)
        public static List<string[]> Visibility(Monitor m)
        {
            var r = new List<string[]>();
            Action<bool, string, string, string, string> A = delegate(bool ok, string name, string yes, string no, string fix) { r.Add(new[] { ok ? "0" : "1", name, ok ? yes : no, ok ? "" : fix }); };
            NetworkInterface nic; lock (m.L) nic = m.Primary;
            string wl = ""; try { wl = Job.Run(Job.Netsh, "wlan show interfaces", 4000); } catch { }
            bool wifiHw = wl.IndexOf("There is no wireless interface", StringComparison.OrdinalIgnoreCase) < 0 && wl.Trim() != "";
            bool loc = !LinkInfo.Blocked(wl);
            if (!wifiHw) r.Add(NoWifi);
            else A(loc, "Wi-Fi name, signal and channel", "available", "hidden - location is off for desktop apps", "Settings → Privacy & security → Location → Let desktop apps access your location. On a work PC IT may lock this; Wi-Fi health still works from the router link, link speed and drops.");
            if (wifiHw) A(loc, "Nearby networks (channel map, advisor)", "available", "hidden - same location setting", "Same switch as above.");
            long sp = 0; try { if (nic != null) sp = nic.Speed; } catch { }
            A(sp > 0, "Link speed", sp > 0 ? (sp / 1e6).ToString("0", CultureInfo.CurrentCulture) + " Mb/s" : "", "not reported", "Some adapters (VPN, virtual) don't report one - nothing to fix.");
            bool st = false; try { if (nic != null) { nic.GetIPStatistics(); st = true; } } catch { }
            A(st, "Adapter traffic and error counters", "available", "not readable", "Unusual - restart AdeeboMeter.");
            string err; EvLog.WifiDrops(1, out err);
            A(err == "", "Windows' Wi-Fi drop log", "readable", "not readable" + (err != "" ? " (" + Draw.Trunc(err, 40) + ")" : ""), "Your IT may restrict event logs. Drops still show from AdeeboMeter's own watch.");
            int routes = 0; try { routes = Routes.Read().Count; } catch { }
            A(routes > 0, "Routing table", routes + " routes", "not readable", "Unusual - nothing to fix.");
            int arp = 0; try { arp = Arp.Table().Count; } catch { }
            A(arp > 0, "Devices your PC has talked to (ARP)", arp + " devices", "empty", "Normal right after connecting.");
            A(!m.dnsCacheLocked, "Names from Windows' DNS cache", "readable", "admin-only on this PC", "Names then come from live capture (with Wireshark). Nothing to fix.");
            int total = 0, hidden = 0;
            foreach (var p in Process.GetProcesses()) { try { total++; if (p.Id > 4 && Native.ExePath(p.Id) == "") hidden++; } catch { } finally { p.Dispose(); } }
            r.Add(new[] { "0", "Programs: where they run from", (total - hidden) + " of " + total + " visible", hidden > 0 ? hidden + " belong to Windows or other users and are hidden from normal users - that's normal, and they're still listed by name." : "" });
            int svc = 0; try { svc = Svc.List().Count; } catch { }
            A(svc > 0, "Windows services (Inside Windows)", svc + " services", "not readable", "Your IT may restrict this.");
            A(LiveCapture.Available, "Wireshark (live capture, real packets)", "installed", "not installed", "Optional. Install Wireshark with Npcap for live capture, real handshakes and switch-port lookup.");
            A(Native.CanNotify(), "Notifications right now", "allowed", "held back (presenting, full screen or Focus)", "Alerts wait until you're done.");
            return r;
        }

        // ---------- pre-call check: 20 seconds, your router and the internet, plus what's using your upload
        public static List<string[]> PreCall(Monitor m, Action<string> progress)
        {
            string gw, tgt; lock (m.L) { gw = m.Gateway; tgt = m.NetTarget; }
            var g = new List<double>(); var n = new List<double>(); var up = new List<double>(); var down = new List<double>();
            using (var p = new Ping())
                for (int i = 0; i < 20; i++)
                {
                    progress("Listening to your connection... " + (20 - i) + " s");
                    var t0 = DateTime.UtcNow;
                    if (gw != "") try { var rp = p.Send(gw, 800); g.Add(rp.Status == IPStatus.Success ? Math.Max(1, rp.RoundtripTime) : double.NaN); } catch { g.Add(double.NaN); }
                    try { var rp = p.Send(tgt, 900); n.Add(rp.Status == IPStatus.Success ? Math.Max(1, rp.RoundtripTime) : double.NaN); } catch { n.Add(double.NaN); }
                    up.Add(m.Up.Last); down.Add(m.Down.Last);
                    int wait = 1000 - (int)(DateTime.UtcNow - t0).TotalMilliseconds; if (wait > 0) Thread.Sleep(wait);
                }
            Func<List<double>, double> loss = l => l.Count == 0 ? 0 : 100.0 * l.Count(double.IsNaN) / l.Count;
            Func<List<double>, double> avg = l => { var o = l.Where(v => !double.IsNaN(v)).ToList(); return o.Count == 0 ? double.NaN : o.Average(); };
            Func<List<double>, double> jit = l => { var o = l.Where(v => !double.IsNaN(v)).ToList(); return o.Count < 3 ? double.NaN : o.Zip(o.Skip(1), (a, b) => Math.Abs(a - b)).Average(); };
            double nl = loss(n), na = avg(n), nj = jit(n), gl = loss(g), ga = avg(g), ua = up.Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).Average();
            var r = new List<string[]>();
            int lv = nl > 3 || nj > 30 || na > 250 ? 2 : nl > 0 || nj > 15 || na > 120 || ua > 3 ? 1 : 0;
            string verdict = lv == 0 ? "Good for a video call." : lv == 1 ? "OK for a call, with risks - see below." : "Not great for a call right now.";
            r.Add(new[] { lv.ToString(CultureInfo.InvariantCulture), "Verdict", verdict, ua > 3 ? "Something is uploading " + ua.ToString("0.#", CultureInfo.CurrentCulture) + " Mb/s - pause OneDrive / backups before the call." : gl > 0 ? "Your Wi-Fi itself is dropping packets - move closer to the router or use a cable." : nl > 0 ? "Packets get lost past your router - your provider or VPN. Audio may clip." : "" });
            r.Add(new[] { gl > 0 ? "1" : "0", "Your router", double.IsNaN(ga) ? "no answer" : ga.ToString("0", CultureInfo.CurrentCulture) + " ms · loss " + gl.ToString("0", CultureInfo.CurrentCulture) + "%", "" });
            r.Add(new[] { nl > 0 ? "1" : "0", "The internet (" + tgt + ")", double.IsNaN(na) ? "no answer" : na.ToString("0", CultureInfo.CurrentCulture) + " ms · jitter " + (double.IsNaN(nj) ? "-" : nj.ToString("0", CultureInfo.CurrentCulture)) + " ms · loss " + nl.ToString("0", CultureInfo.CurrentCulture) + "%", "Calls want under 150 ms, jitter under 30 ms and no loss." });
            r.Add(new[] { ua > 3 ? "1" : "0", "Your upload in use", ua.ToString("0.0", CultureInfo.CurrentCulture) + " Mb/s", "A video call needs about 1.5-4 Mb/s of upload to itself." });
            r.Add(new[] { "3", "Latency under load", Bloat.Story(), "" });
            return r;
        }

        // ---------- DNS speed test: your DNS against three public ones (only when you press it - about 40 small questions)
        static readonly string[] Names = { "github.com", "microsoft.com", "wikipedia.org", "youtube.com", "amazon.com", "apple.com", "cloudflare.com", "bbc.co.uk", "zoom.us", "office.com" };
        public static List<string[]> DnsBench(Monitor m, Action<string> progress)
        {
            string mine; lock (m.L) mine = m.DnsServer;
            var servers = new List<string[]>();
            if (mine != "") servers.Add(new[] { mine, "Your DNS (" + mine + ")" });
            servers.AddRange(Public);
            var res = new List<Tuple<string, double, int>>();
            foreach (var s in servers)
            {
                progress("Asking " + s[1] + "...");
                var times = new List<double>(); int fails = 0;
                foreach (var name in Names)
                {
                    string err; var sw = Stopwatch.StartNew();
                    var ans = DnsAsk.Ask(name, s[0], 2000, out err);
                    if (ans == null || ans.Count == 0) fails++; else times.Add(sw.Elapsed.TotalMilliseconds);
                }
                times.Sort();
                res.Add(Tuple.Create(s[1], times.Count > 0 ? times[times.Count / 2] : double.NaN, fails));
            }
            var r = new List<string[]>(); var best = res.Where(t => !double.IsNaN(t.Item2)).OrderBy(t => t.Item2).FirstOrDefault();
            var me = res.FirstOrDefault();
            foreach (var t in res)
                r.Add(new[] { double.IsNaN(t.Item2) ? "2" : t == best ? "0" : "3", t.Item1, double.IsNaN(t.Item2) ? "no answers" : t.Item2.ToString("0", CultureInfo.CurrentCulture) + " ms typical" + (t.Item3 > 0 ? " · " + t.Item3 + " failed" : ""), t == best ? "Fastest here." : "" });
            if (me != null && best != null && me != best && mine != "" && !double.IsNaN(me.Item2) && me.Item2 - best.Item2 > 15)
                r.Insert(0, new[] { "1", "Verdict", "Switching would save about " + (me.Item2 - best.Item2).ToString("0", CultureInfo.CurrentCulture) + " ms per new website", "Change DNS in Settings → Network → your connection → DNS server assignment (on a work PC, leave it: company DNS is often required)." });
            else r.Insert(0, DnsFine);
            return r;
        }

        // ---------- VPN map: which current connections go through the VPN, which go around it (split tunnel)
        public static List<string[]> VpnMap(Monitor m)
        {
            Dictionary<int, string> names; lock (m.L) names = m.PidNames;
            var conns = Tables.Tcp(false, names).Where(c => c.State == "ESTABLISHED" && Watch.IsPublic(c.RemoteIp)).ToList();
            var nics = Routes.Nics(); var tally = new Dictionary<string, int[]>();
            bool anyVpn = nics.Values.Any(n => n.OperationalStatus == OperationalStatus.Up && Monitor.IsVpn(n));
            foreach (var c in conns)
            {
                IPAddress a; if (!IPAddress.TryParse(c.RemoteIp, out a) || a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                int idx = Routes.WindowsPick(a); NetworkInterface nic; bool vpn = nics.TryGetValue(idx, out nic) && Monitor.IsVpn(nic);
                int[] t; if (!tally.TryGetValue(c.Proc, out t)) tally[c.Proc] = t = new int[2];
                t[vpn ? 0 : 1]++;
            }
            var r = new List<string[]>();
            if (!anyVpn) { r.Add(NoVpn); return r; }
            int v = tally.Values.Sum(t => t[0]), d = tally.Values.Sum(t => t[1]);
            r.Add(new[] { "0", "Verdict", v + " connections through the VPN · " + d + " straight out", d > 0 && v > 0 ? "Your VPN is a split tunnel: company traffic goes through it, the rest (often Teams media and Office 365) goes direct - that's normal and usually faster." : d == 0 ? "Everything goes through the VPN (full tunnel)." : "Nothing currently uses the VPN." });
            foreach (var kv in tally.OrderByDescending(x => x.Value[0] + x.Value[1]))
                r.Add(new[] { "3", kv.Key, (kv.Value[0] > 0 ? kv.Value[0] + " via VPN" : "") + (kv.Value[0] > 0 && kv.Value[1] > 0 ? " · " : "") + (kv.Value[1] > 0 ? kv.Value[1] + " direct" : ""), "" });
            return r;
        }
    }
}
