// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // ================================================================== VPN and company proxy awareness
    // Recognises the common VPN and "zero trust" clients by their running programs and adapters, says whether everything goes
    // through the tunnel (full tunnel) or only company traffic (split), whether a proxy or PAC script is set - and measures the
    // tunnel as its own layer: how long it takes just to reach the far side of the VPN, against the whole trip to the internet.
    // Read-only: nothing is changed, and the only packets are a few pings (or timed connections) to the VPN's own DNS server.
    static class VpnAware
    {
        // program name (lower case, no .exe) -> product, kind
        static readonly Dictionary<string, string[]> Clients = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) {
            { "pangps", new[] { "GlobalProtect (Palo Alto)", "VPN" } }, { "pangpa", new[] { "GlobalProtect (Palo Alto)", "VPN" } },
            { "vpnagent", new[] { "Cisco Secure Client (AnyConnect)", "VPN" } }, { "csc_ui", new[] { "Cisco Secure Client (AnyConnect)", "VPN" } }, { "vpnui", new[] { "Cisco Secure Client (AnyConnect)", "VPN" } },
            { "zsatunnel", new[] { "Zscaler Client Connector", "zero trust (cloud proxy)" } }, { "zsatray", new[] { "Zscaler Client Connector", "zero trust (cloud proxy)" } },
            { "stagentsvc", new[] { "Netskope Client", "zero trust (cloud proxy)" } }, { "stagentui", new[] { "Netskope Client", "zero trust (cloud proxy)" } },
            { "forticlient", new[] { "FortiClient (Fortinet)", "VPN" } }, { "fortitray", new[] { "FortiClient (Fortinet)", "VPN" } }, { "fortisslvpndaemon", new[] { "FortiClient (Fortinet)", "VPN" } },
            { "pulsesecureservice", new[] { "Ivanti Secure Access (Pulse)", "VPN" } }, { "pulse", new[] { "Ivanti Secure Access (Pulse)", "VPN" } },
            { "tracsrvwrapper", new[] { "Check Point VPN", "VPN" } }, { "trgui", new[] { "Check Point VPN", "VPN" } },
            { "warp-svc", new[] { "Cloudflare WARP / Zero Trust", "zero trust (cloud proxy)" } },
            { "tailscaled", new[] { "Tailscale", "mesh VPN" } }, { "zerotier-one_x64", new[] { "ZeroTier", "mesh VPN" } },
            { "wireguard", new[] { "WireGuard", "VPN" } }, { "openvpn", new[] { "OpenVPN", "VPN" } }, { "openvpn-gui", new[] { "OpenVPN", "VPN" } }, { "openvpnconnect", new[] { "OpenVPN Connect", "VPN" } },
            { "nordvpn", new[] { "NordVPN", "VPN (privacy)" } }, { "nordvpn-service", new[] { "NordVPN", "VPN (privacy)" } }, { "protonvpn", new[] { "Proton VPN", "VPN (privacy)" } }, { "protonvpnservice", new[] { "Proton VPN", "VPN (privacy)" } },
            { "expressvpn", new[] { "ExpressVPN", "VPN (privacy)" } }, { "expressvpnservice", new[] { "ExpressVPN", "VPN (privacy)" } }, { "surfshark", new[] { "Surfshark", "VPN (privacy)" } },
            { "azurevpn", new[] { "Azure VPN Client", "VPN" } }, { "f5fpclientw", new[] { "F5 BIG-IP Edge Client", "VPN" } } };
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }

        public sealed class Info { public List<string> Products = new List<string>(); public string Kind = "", Adapter = "", Proxy = "", Pac = ""; public bool AdapterUp, FullTunnel, Zero; public IPAddress FarSide; }
        public static Info Detect()
        {
            var i = new Info();
            var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses()) { try { running.Add(p.ProcessName); } catch (InvalidOperationException) { } finally { p.Dispose(); } }
            foreach (var kv in Clients) if (running.Contains(kv.Key) && !i.Products.Contains(kv.Value[0])) { i.Products.Add(kv.Value[0]); if (i.Kind == "" || kv.Value[1].StartsWith("zero", StringComparison.Ordinal)) i.Kind = kv.Value[1]; }
            i.Zero = i.Kind.StartsWith("zero", StringComparison.Ordinal);
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus != OperationalStatus.Up || !Monitor.IsVpn(n)) continue;
                i.AdapterUp = true; i.Adapter = n.Description;
                var ip = n.GetIPProperties();
                i.FullTunnel = ip.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                // WireGuard and OpenVPN full tunnels often have no adapter gateway: a default route (0/0, or 0/1 + 128/1) through it says it
                if (!i.FullTunnel)
                    try
                    {
                        var v4 = ip.GetIPv4Properties(); int idx = v4 != null ? v4.Index : -1;
                        if (idx >= 0) i.FullTunnel = Routes.Read().Any(r => r.IfIndex == idx && (r.Prefix == 0 || (r.Prefix == 1 && (r.Dest == 0 || r.Dest == 0x80000000u))));
                    }
                    catch (NetworkInformationException) { }
                i.FarSide = ip.DnsAddresses.FirstOrDefault(d => d.AddressFamily == AddressFamily.InterNetwork) ?? ip.GatewayAddresses.Select(g => g.Address).FirstOrDefault(g => g.AddressFamily == AddressFamily.InterNetwork && !g.Equals(IPAddress.Any));
                break;
            }
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                    if (k != null)
                    {
                        if (Convert.ToInt32(k.GetValue("ProxyEnable", 0), CultureInfo.InvariantCulture) == 1) i.Proxy = Convert.ToString(k.GetValue("ProxyServer", ""), CultureInfo.InvariantCulture);
                        i.Pac = Convert.ToString(k.GetValue("AutoConfigURL", ""), CultureInfo.InvariantCulture);
                    }
            }
            catch (System.Security.SecurityException) { }
            return i;
        }
        static double Ms(IPAddress ip)
        {
            var r = Icmp.Echo(ip, 32, 64, false, 1500, false); if (r.Status == Icmp.Ok && r.Rtt >= 0) return Math.Max(0.5, r.Rtt);
            double t = GamePing.TcpMs(ip, 53, 1500); return t;   // DNS servers usually answer TCP 53 when they ignore ping
        }
        public static double Median(IPAddress ip, int n, Func<bool> stop) { var l = new List<double>(); for (int k = 0; k < n && !stop(); k++) { double v = Ms(ip); if (!double.IsNaN(v)) l.Add(v); System.Threading.Thread.Sleep(200); } return l.Count == 0 ? double.NaN : l.OrderBy(x => x).ElementAt(l.Count / 2); }

        public static List<string[]> Run(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>(); run.Status = "Looking for VPN and zero-trust clients...";
            var i = Detect();
            bool any = i.Products.Count > 0 || i.AdapterUp || i.Proxy != "" || i.Pac != "";
            if (!any) { rows.Add(R(0, "VPN or company proxy", "none - this PC goes straight to the internet", "", "w")); return rows; }
            if (i.Products.Count > 0) rows.Add(R(3, "Client running", string.Join(", ", i.Products) + " · " + i.Kind, i.Zero ? "Zero-trust clients send your traffic through the company's cloud security service - every site goes the long way round, by design." : "", "w"));
            rows.Add(R(3, "Tunnel adapter", i.AdapterUp ? i.Adapter : "none up" + (i.Zero ? " (normal for zero-trust clients - they capture traffic without a classic tunnel)" : " - the VPN isn't connected right now"), "", "w"));
            if (i.AdapterUp) rows.Add(R(3, "What goes through it", i.FullTunnel ? "everything (full tunnel)" : "company traffic only (split tunnel)", i.FullTunnel ? "Your internet goes to the company first and out from there - YouTube and games included. That adds distance to everything." : "Internet sites go direct; only company addresses use the tunnel.", "w"));
            if (i.Proxy != "") rows.Add(R(3, "Proxy", i.Proxy, "Web traffic goes to this proxy first.", "w"));
            if (i.Pac != "") rows.Add(R(3, "PAC script", i.Pac, "A script picks a proxy per site.", "w"));
            // the tunnel as its own layer
            double net = m.NetPing.Avg(6), gw = m.GwPing.Avg(6);
            if (i.AdapterUp && i.FarSide != null)
            {
                run.Status = "Timing the tunnel (to " + i.FarSide + ")...";
                double far = Median(i.FarSide, 7, () => run.Stop);
                rows.Add(R(3, "-", "", "", "m"));
                rows.Add(R(double.IsNaN(gw) ? 3 : gw > 20 ? 1 : 0, "Your router", double.IsNaN(gw) ? "-" : Math.Round(gw) + " ms", "", "m"));
                rows.Add(R(double.IsNaN(far) ? 3 : far > 80 ? 2 : far > 40 ? 1 : 0, "The VPN's far side", double.IsNaN(far) ? "no answer from " + i.FarSide : Math.Round(far) + " ms (" + i.FarSide + ", the VPN's DNS server)", "", "m"));
                rows.Add(R(double.IsNaN(net) ? 3 : 0, "The whole trip to the internet", double.IsNaN(net) ? "-" : Math.Round(net) + " ms", "", "m"));
                string verdict = Verdict(gw, far, net, i.FullTunnel);
                if (verdict != "") rows.Add(R(verdict.StartsWith("Your VPN", StringComparison.Ordinal) ? 1 : 0, "What it means", verdict, "", "i"));
            }
            else if (i.Zero) rows.Add(R(3, "What it means", "With a zero-trust client the extra time is inside the service - compare the same site with the client paused (if your company allows it) using Before / after.", "", "i"));
            return rows;
        }
        // where the time goes: router → tunnel → internet
        public static string Verdict(double gw, double far, double net, bool full)
        {
            if (double.IsNaN(far)) return "";
            double tunnel = Math.Max(0, far - (double.IsNaN(gw) ? 0 : gw));
            if (tunnel >= 40 && (double.IsNaN(net) || tunnel >= net * 0.5)) return "Your VPN is the slow part: " + Math.Round(tunnel) + " ms just to reach its far side" + (full ? " - and with a full tunnel every site pays it." : ".") + " Try a nearer VPN gateway if your client offers a choice, or ask IT about split tunnelling for internet sites.";
            if (tunnel < 20) return "The tunnel itself is quick (" + Math.Round(tunnel) + " ms) - if things are slow, the delay is past the VPN, at the company's internet exit or the site.";
            return "The tunnel adds about " + Math.Round(tunnel) + " ms - noticeable on calls, not the main problem.";
        }
    }
}
