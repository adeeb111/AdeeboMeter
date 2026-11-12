// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;

namespace AdeeboMeter
{
    // ================================================================== Teams and Office, end to end: which process does what, and what each sends where
    // Microsoft's official address list changes monthly and lives online; AdeeboMeter stays offline, so it carries the stable,
    // well-known names and ranges as of this version and says "role unknown" rather than guessing.
    static class M365
    {
        public const string AsOf = "as of AdeeboMeter 1.1";
        // Teams media relays (Transport Relays) - Microsoft's published, long-stable ranges
        static readonly string[] MediaRanges = { "52.112.0.0/14", "52.122.0.0/15", "52.120.0.0/14" };
        static readonly string[][] Hosts =
        {
            new[] { "login.microsoftonline.com", "Sign-in" }, new[] { "login.live.com", "Sign-in" }, new[] { "login.windows.net", "Sign-in" }, new[] { "aadcdn.", "Sign-in" }, new[] { "msauth", "Sign-in" },
            new[] { "teams.microsoft.com", "Signalling" }, new[] { "teams.live.com", "Signalling" }, new[] { "skype.com", "Signalling" }, new[] { "trouter", "Signalling" }, new[] { "teams.cdn.office.net", "App content" },
            new[] { "outlook.office365.com", "Mail" }, new[] { "outlook.office.com", "Mail" }, new[] { "outlook.live.com", "Mail" }, new[] { "outlook.cloud.microsoft", "Mail" }, new[] { "substrate.office.com", "Mail and search" },
            new[] { "sharepoint.com", "Files (OneDrive / SharePoint)" }, new[] { "onedrive.live.com", "Files (OneDrive)" }, new[] { "1drv.com", "Files (OneDrive)" }, new[] { "storage.live.com", "Files (OneDrive)" },
            new[] { "officecdn.microsoft.com", "Updates" }, new[] { "officeapps.live.com", "Office apps" }, new[] { "office.net", "Office content" }, new[] { "office.com", "Office" }, new[] { "cloud.microsoft", "Office" },
            new[] { "events.data.microsoft.com", "Telemetry" }, new[] { "aria.microsoft.com", "Telemetry" }, new[] { "vortex", "Telemetry" }, new[] { "self.events", "Telemetry" },
            new[] { "graph.microsoft.com", "Microsoft Graph (your data)" }, new[] { "config.teams", "Settings" }, new[] { "config.office", "Settings" },
        };
        static readonly string[] MsDomains = { "microsoft.com", "microsoftonline.com", "msauth.net", "msftauth.net", "live.com", "live.net", "office.com", "office.net", "office365.com", "skype.com", "sharepoint.com", "cloud.microsoft", "windows.net", "azureedge.net", "msedge.net", "trafficmanager.net", "msidentity.com", "teams.microsoft" };
        public static string Role(string host, string ip, int port)
        {
            host = (host ?? "").ToLowerInvariant();
            // a name ending in one of these domains (on a label boundary) - "msauth.evil.example" or "notskype.com.attacker.net" is nobody's
            // Microsoft. Pieces like "trouter" or "aadcdn." count only inside a Microsoft-owned domain.
            bool ms = MsDomains.Any(d => host == d || host.EndsWith("." + d, StringComparison.Ordinal));
            foreach (var h in Hosts)
            {
                bool domain = h[0].IndexOf('.') > 0 && !h[0].EndsWith(".", StringComparison.Ordinal) && h[0] != "self.events" && !h[0].StartsWith("config.", StringComparison.Ordinal);
                if (domain ? host == h[0] || host.EndsWith("." + h[0], StringComparison.Ordinal) : ms && host.Contains(h[0])) return h[1];
            }
            IPAddress a;
            if (IPAddress.TryParse(ip ?? "", out a) && MediaRanges.Any(r => InRange(a, r))) return port == 443 ? "Teams media relay (over TCP 443)" : "Teams media relay";
            if (host.EndsWith(".microsoft.com", StringComparison.Ordinal) || host.EndsWith(".office.com", StringComparison.Ordinal) || host.EndsWith(".msedge.net", StringComparison.Ordinal) || host.EndsWith(".azureedge.net", StringComparison.Ordinal)) return "Microsoft, role unknown";
            return "";
        }
        public static bool InRange(IPAddress a, string cidr)
        {
            var p = cidr.Split('/'); IPAddress n; int bits;
            if (!IPAddress.TryParse(p[0], out n) || !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out bits) || a.AddressFamily != n.AddressFamily) return false;
            var x = a.GetAddressBytes(); var y = n.GetAddressBytes();
            for (int i = 0; i < x.Length && bits > 0; i++, bits -= 8) { int m = bits >= 8 ? 0xff : (0xff << (8 - bits)) & 0xff; if ((x[i] & m) != (y[i] & m)) return false; }
            return true;
        }
        // the process families, by exe name
        public static readonly string[][] Families =
        {
            new[] { "Teams", "ms-teams|msteams|teams" }, new[] { "Outlook", "outlook|olk" }, new[] { "OneDrive", "onedrive|filecoauth" },
            new[] { "Word, Excel, PowerPoint", "winword|excel|powerpnt|onenote" }, new[] { "Sign-in broker (WAM)", "microsoft.aad.brokerplugin|tokenbroker" },
            new[] { "Office updater (Click-to-Run)", "officeclicktorun|officec2rclient" }, new[] { "WebView2 (the screens inside Teams and new Outlook)", "msedgewebview2" },
        };

        static readonly string[] NotRunning = { "3", "Outlook", "not running", "" };
        public sealed class Row { public string App = "", Remote = "", Host = "", Role = "", State = ""; public int Pid, Port; }
        // every live connection of these apps, labelled
        public static List<Row> Connections(Monitor m)
        {
            Dictionary<int, string> names; Dictionary<string, string> ipn; lock (m.L) { names = m.PidNames; ipn = m.IpNames; }
            var r = new List<Row>(); var fam = FamilyByPid();
            foreach (var c in Tables.Tcp(false, names).Where(c => c.State == "ESTABLISHED" && Watch.IsPublic(c.RemoteIp)))
            {
                string f; if (!fam.TryGetValue(c.Pid, out f)) continue;
                string host; if (!ipn.TryGetValue(c.RemoteIp, out host)) host = c.Host ?? "";
                r.Add(new Row { App = f, Pid = c.Pid, Remote = c.RemoteIp, Port = c.RemotePort, Host = host, Role = Role(host, c.RemoteIp, c.RemotePort), State = c.State });
            }
            return r;
        }
        // pid → family name, including WebView2 helpers whose parent is Teams or Outlook
        public static Dictionary<int, string> FamilyByPid()
        {
            Dictionary<int, ProcTree.P> by; ProcTree.Snapshot(out by); var d = new Dictionary<int, string>();
            foreach (var p in by.Values)
            {
                string n = Path.GetFileNameWithoutExtension(p.Name ?? "").ToLowerInvariant();
                foreach (var f in Families) if (f[1].Split('|').Contains(n)) { d[p.Pid] = f[0]; break; }
            }
            foreach (var p in by.Values)
            {
                string n = Path.GetFileNameWithoutExtension(p.Name ?? "").ToLowerInvariant(); string pf;
                if (n == "msedgewebview2")
                {
                    // walk up: a WebView2 helper belongs to whichever app started its tree
                    ProcTree.P cur = p; int guard = 0;
                    while (cur != null && guard++ < 6) { if (d.TryGetValue(cur.Parent, out pf) && pf != Families[6][0]) { d[p.Pid] = pf + " (WebView2)"; break; } ProcTree.P up; cur = by.TryGetValue(cur.Parent, out up) ? up : null; }
                }
            }
            return d;
        }
        // "Teams fell back to TCP": media to the relays on TCP 443 and no UDP media sockets
        public static string TcpFallback(Monitor m, List<Row> conns)
        {
            bool media443 = conns.Any(c => c.App.StartsWith("Teams", StringComparison.Ordinal) && c.Role.StartsWith("Teams media relay (over TCP", StringComparison.Ordinal));
            bool call; lock (m.L) call = m.CallLikely;
            if (call) return "Teams is in a call with UDP media - the best path. Good.";
            double up = m.Up.Last, down = m.Down.Last;
            if (media443 && (up > 0.2 || down > 0.2)) return "Teams seems to be carrying a call over TCP 443 - that happens when your network blocks UDP 3478-3481. Audio over TCP stutters more under loss. Ask IT to allow UDP 3478-3481 to Microsoft's Teams ranges.";
            return media443 ? "Teams has a connection to its media relays on TCP 443 (no call traffic right now)." : "No Teams call traffic right now.";
        }
        // "Why is Outlook slow?" - the usual suspects, checked
        public static List<string[]> OutlookSlow(Monitor m, List<Row> conns)
        {
            var r = new List<string[]>(); List<AppStat> apps; lock (m.L) apps = m.Apps.ToList();
            var o = apps.FirstOrDefault(a => a.Name == "Outlook");
            if (o == null) { r.Add(NotRunning); return r; }
            r.Add(new[] { o.Cpu > 25 ? "1" : "0", "Outlook itself", o.Cpu.ToString("0", CultureInfo.CurrentCulture) + "% CPU · " + (o.Mem / 1048576) + " MB", o.Cpu > 25 ? "Busy on your PC: indexing, a big mailbox, or an add-in. File → Options → Add-ins → disable the ones you don't use." : "" });
            long ost = 0; try { string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Outlook"); if (Directory.Exists(d)) ost = Directory.GetFiles(d, "*.ost").Sum(f => new FileInfo(f).Length); } catch { }
            if (ost > 0) r.Add(new[] { ost > 25L << 30 ? "1" : "0", "Mailbox kept on this PC (cached mode)", (ost / 1073741824.0).ToString("0.0", CultureInfo.CurrentCulture) + " GB", ost > 25L << 30 ? "A very large local mailbox slows search and start-up. Keep fewer months offline: File → Account settings → your account → 'Download email for the past'." : "" });
            int mail = conns.Count(c => c.App == "Outlook" && c.Role.StartsWith("Mail", StringComparison.Ordinal));
            r.Add(new[] { mail > 0 ? "0" : "1", "Connected to Exchange Online", mail > 0 ? mail + " connections" : "none right now", mail > 0 ? "" : "Outlook isn't talking to the mail server - check the status bar for 'Disconnected' / 'Needs password'." });
            double net = m.NetPing.Avg(10);
            r.Add(new[] { !double.IsNaN(net) && net > 150 ? "1" : "0", "The internet", double.IsNaN(net) ? "-" : net.ToString("0", CultureInfo.CurrentCulture) + " ms", !double.IsNaN(net) && net > 150 ? "Slow path to the internet - Outlook waits on every click in online mode." : "" });
            bool od = apps.Any(a => a.Name == "OneDrive" && a.Cpu > 5) && m.Up.Last > 2;
            r.Add(new[] { od ? "1" : "0", "OneDrive competing for upload", od ? "yes - " + m.Up.Last.ToString("0.#", CultureInfo.CurrentCulture) + " Mb/s up" : "no", od ? "Pause OneDrive syncing for a while (tray icon → Pause)." : "" });
            return r;
        }
    }
}
