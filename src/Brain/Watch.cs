// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    // ================================================================== alerts: only when there's something to do
    // A Windows notification from the tray icon. Each kind at most once per cooldown; quiet hours and presenting are honoured
    // by whoever shows it (AppCtx). Never information for its own sake.
    static class Alerts
    {
        public static Action<string, string> Show = delegate { };     // (title, text) - set by AppCtx
        static readonly Dictionary<string, DateTime> last = new Dictionary<string, DateTime>();
        public static void Raise(string kind, string text, int cooldownMin)
        {
            if (!Cfg.Alerts) return;
            lock (last)
            {
                DateTime t; if (last.TryGetValue(kind, out t) && (DateTime.UtcNow - t).TotalMinutes < cooldownMin) return;
                last[kind] = DateTime.UtcNow;
            }
            try { Show("AdeeboMeter", text); } catch { }
        }
        // Windows drops in a row, internet gone, router hardware changed: learned from the timeline as it happens
        static readonly List<DateTime> drops = new List<DateTime>();
        public static void OnEvent(Timeline.Ev e)
        {
            if (e.Kind == "wifi" && e.Lv == 2)
            {
                lock (drops) { drops.Add(e.T); drops.RemoveAll(t => (e.T - t).TotalMinutes > 10); if (drops.Count >= 3) Raise("drops", "Wi-Fi dropped " + drops.Count + " times in 10 minutes. Move closer to the router, or switch to 5 GHz if it's offered.", 30); }
            }
            else if (e.Kind == "net" && e.Lv == 2 && e.Text.IndexOf("hardware address", StringComparison.OrdinalIgnoreCase) >= 0) Raise("arp", "Your router's hardware address changed on the same network - possible spoofing. Use the VPN until you know why.", 60);
            else if (e.Kind == "inet" && e.Lv == 2) Raise("inet", Monitor.GwAnswering ? "The internet went quiet. Your router still answers - it's your provider or the line, not your PC." : "The internet went quiet, and your router isn't answering either - check the Wi-Fi or cable first, then the router.", 20);
            else if (e.Kind == "inet" && e.Lv == 0 && e.Text.StartsWith("Internet back", StringComparison.Ordinal)) Raise("inetback", e.Text + ". Every outage is listed in Time machine.", 1);
        }
    }

    // ================================================================== meeting replay - numbers only, in memory, never saved
    // While a Teams call runs it keeps one small row a second (delay, loss, signal, link rate, traffic) and the network events.
    // When the call ends it says, in one sentence, what (if anything) went wrong - then forgets the rows. Nothing about the call
    // is written to disk; the sentence lives only in this session's memory.
    static class Calls
    {
        sealed class S { public DateTime T; public double Gw, Net, Down, Up; public int Sig, Rate; }
        static readonly List<S> rows = new List<S>(); static readonly List<Timeline.Ev> evs = new List<Timeline.Ev>();
        static bool inCall; static DateTime started;
        public static readonly List<string[]> Summaries = new List<string[]>();   // { when, sentence, level } newest first, max 10
        static readonly object k = new object();
        public static void Second(Monitor m, bool call, double gw, double net, int sig, int rate, double down, double up)
        {
            lock (k)
            {
                if (call)
                {
                    if (!inCall) { inCall = true; started = DateTime.Now; rows.Clear(); evs.Clear(); }
                    if (rows.Count < 4 * 3600) rows.Add(new S { T = DateTime.Now, Gw = gw, Net = net, Sig = sig, Rate = rate, Down = down, Up = up });
                }
                else if (inCall) { inCall = false; Summarise(); rows.Clear(); evs.Clear(); }
            }
        }
        public static void OnEvent(Timeline.Ev e) { lock (k) if (inCall && (e.Kind == "wifi" || e.Kind == "net" || e.Kind == "inet" || e.Kind == "gw" || e.Kind == "vpn")) evs.Add(e); }
        static void Summarise()
        {
            if (rows.Count < 20) return;
            var mins = (int)Math.Round((DateTime.Now - started).TotalMinutes);
            var lost = rows.Count(r => double.IsNaN(r.Net)); double lossPct = 100.0 * lost / rows.Count;
            var worst = rows.Where(r => !double.IsNaN(r.Gw)).OrderByDescending(r => r.Gw).FirstOrDefault();
            string when = started.ToString("HH:mm", CultureInfo.CurrentCulture);
            string s; int lv = 0;
            var roam = evs.FirstOrDefault(e => e.Text.IndexOf("roam", StringComparison.OrdinalIgnoreCase) >= 0 || e.Text.IndexOf("hopped", StringComparison.OrdinalIgnoreCase) >= 0);
            var drop = evs.FirstOrDefault(e => e.Lv == 2);
            double upBusy = rows.Count(r => r.Up > 3) * 100.0 / rows.Count;
            if (drop != null) { lv = 2; s = "Your " + mins + "-minute call at " + when + " broke at " + drop.T.ToString("HH:mm", CultureInfo.CurrentCulture) + ": " + drop.Text; }
            else if (roam != null && worst != null && worst.Gw > 80) { lv = 1; s = "Audio likely broke at " + roam.T.ToString("HH:mm", CultureInfo.CurrentCulture) + " - Wi-Fi roamed to another access point mid-call."; }
            else if (upBusy > 20) { lv = 1; s = "Something was uploading for " + upBusy.ToString("0", CultureInfo.CurrentCulture) + "% of the call (more than 3 Mb/s) - OneDrive or a backup competing with your call."; }
            else if (lossPct > 2) { lv = 1; s = "Your " + mins + "-minute call lost " + lossPct.ToString("0.#", CultureInfo.CurrentCulture) + "% of internet checks - the trouble was past your router (provider or VPN)."; }
            else if (worst != null && worst.Gw > 100) { lv = 1; s = "The link to your router spiked to " + worst.Gw.ToString("0", CultureInfo.CurrentCulture) + " ms at " + worst.T.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + " - Wi-Fi trouble around then."; }
            else s = "Your " + mins + "-minute call at " + when + " had a clean network the whole way.";
            lock (Summaries)   // readers lock the list itself (History page, Investigate) - so must the writer
            {
                Summaries.Insert(0, new[] { DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture), s, lv.ToString(CultureInfo.InvariantCulture) });
                if (Summaries.Count > 10) Summaries.RemoveAt(Summaries.Count - 1);
            }
            if (lv > 0) Alerts.Raise("call", "After your call: " + s, 5);
        }
        public static bool InCall { get { lock (k) return inCall; } }
    }

    // ================================================================== watch and suggest (never blocks - that needs admin)
    static class Watch
    {
        public sealed class Sug { public DateTime T; public string App = "", Path = "", Why = "", How = ""; public int Lv; }
        public static readonly List<Sug> List = new List<Sug>();
        static Dictionary<string, DateTime> seen; static HashSet<string> night; static DateTime learnStart; static readonly object k = new object(); static DateTime saved = DateTime.MinValue;
        static void Load()
        {
            if (seen != null) return;
            seen = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase); night = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in Store.Read("seen-apps.txt"))
            {
                DateTime t; if (!DateTime.TryParse(r[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t)) continue;
                if (r[0] == "#learn") learnStart = t; else seen[r[0]] = t;
                if (r.Length > 2 && r[2] == "n") night.Add(r[0]);
            }
            if (learnStart == DateTime.MinValue) learnStart = DateTime.UtcNow;
        }
        static void Save()
        {
            var rows = new List<string[]> { new[] { "#learn", learnStart.ToString("o", CultureInfo.InvariantCulture) } };
            rows.AddRange(seen.Select(kv => new[] { kv.Key, kv.Value.ToString("o", CultureInfo.InvariantCulture), night.Contains(kv.Key) ? "n" : "" }));
            Store.Write("seen-apps.txt", rows);
        }
        static bool Learning { get { return (DateTime.UtcNow - learnStart).TotalHours < 24; } }
        static void Add(int lv, string app, string path, string why, string how)
        {
            lock (List)
            {
                if (List.Any(s => s.Path == path && s.Why == why)) return;
                List.Insert(0, new Sug { T = DateTime.Now, App = app, Path = path, Why = why, How = how, Lv = lv });
                if (List.Count > 50) List.RemoveAt(List.Count - 1);
            }
            Alerts.Raise("watch|" + path, app + ": " + why, 24 * 60);
        }
        public static string HowToBlock(string path)
        {
            return "To block it: Windows Security → Firewall & network protection → Allow an app through firewall → untick it, or add an outbound rule for " + System.IO.Path.GetFileName(path) + " (needs admin - on a work PC, ask IT). AdeeboMeter only suggests; it never blocks anything.";
        }

        static readonly System.Text.RegularExpressions.Regex VerSeg = new System.Text.RegularExpressions.Regex(@"\d+(\.\d+){1,3}", System.Text.RegularExpressions.RegexOptions.Compiled);
        // a path with its version numbers taken out of the FOLDER names (the file name stays): one key per app across updates
        public static string Unversioned(string path)
        {
            string dir = System.IO.Path.GetDirectoryName(path) ?? "", file = System.IO.Path.GetFileName(path);
            return System.IO.Path.Combine(VerSeg.Replace(dir, "*"), file);
        }
        // From the Monitor's connection read (every few seconds): which programs talk to the internet, and is that new?
        public static void Conns(Monitor m, List<Conn> list)
        {
            if (!Cfg.Watch) return;
            lock (k)
            {
                Load(); bool changed = false; var now = DateTime.Now; bool atNight = now.Hour < 5;
                foreach (var g in list.Where(c => c.State == "ESTABLISHED" && IsPublic(c.RemoteIp)).GroupBy(c => c.Pid))
                {
                    string path = Native.ExePath(g.Key); if (path == "") continue;
                    var c0 = g.First();
                    // versioned folders (Teams, Discord, updaters: "app-1.0.9", "MSTeams_24123.1.2_x64") are the same app after an update
                    string key = Unversioned(path); DateTime was; if (key != path && seen.TryGetValue(path, out was) && !seen.ContainsKey(key)) seen[key] = was;
                    bool known = seen.ContainsKey(key) || seen.ContainsKey(path); path = key;
                    if (!known)
                    {
                        if (seen.Count > 2000) { foreach (var old in seen.OrderBy(x => x.Value).Take(200).Select(x => x.Key).ToList()) { seen.Remove(old); night.Remove(old); } }   // capped: the oldest go
                        seen[path] = DateTime.UtcNow; changed = true;
                        string[] facts = Explain.FileFacts(path); string where = Explain.Where(path);
                        bool odd = where == "downloads" || where == "temp" || path.IndexOf("\\Downloads\\", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("\\Temp\\", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool noPublisher = facts.Length > 1 && string.IsNullOrEmpty(facts[1]);
                        // temp-folder updaters are normal while it's still learning this PC: amber then, red afterwards
                        if (odd) Add(Learning ? 1 : 2, c0.Proc, path, "connects to the internet from " + (path.IndexOf("Downloads", StringComparison.OrdinalIgnoreCase) >= 0 ? "your Downloads folder" : "a temporary folder") + " - programs normally live in Program Files.", HowToBlock(path));
                        else if (!Learning) Add(noPublisher ? 2 : 1, c0.Proc, path, "connected to the internet for the first time" + (noPublisher ? ", and its file names no publisher" : "") + " (" + Draw.Trunc(c0.Host != "" ? c0.Host : c0.RemoteIp, 40) + ").", noPublisher ? HowToBlock(path) : "If you just installed or updated it, that's expected. If not, look it up before trusting it. " + HowToBlock(path));
                    }
                    if (atNight && !night.Contains(path))
                    {
                        night.Add(path); changed = true;
                        if (!Learning && known) Add(1, c0.Proc, path, "was talking to the internet at " + now.ToString("HH:mm", CultureInfo.CurrentCulture) + " - first time at night. Updates do this; unknown programs shouldn't.", HowToBlock(path));
                    }
                }
                if (changed && (DateTime.UtcNow - saved).TotalSeconds > 30) { saved = DateTime.UtcNow; Save(); }
            }
        }
        // a big upload you didn't start (not during a call): flagged with the programs that were connected
        static int busySec; static DateTime upWarned = DateTime.MinValue;
        public static void Upload(Monitor m, double upMbps, bool call)
        {
            if (!Cfg.Watch) return;
            busySec = !double.IsNaN(upMbps) && upMbps > 5 && !call ? busySec + 1 : 0;
            if (busySec == 60 && (DateTime.UtcNow - upWarned).TotalMinutes > 30)
            {
                upWarned = DateTime.UtcNow;
                List<AppStat> apps; lock (m.L) apps = m.Apps.OrderByDescending(a => a.Cpu).Take(3).ToList();
                string who = string.Join(", ", apps.Select(a => a.Name));
                Add(1, "Upload", "upload|" + DateTime.Now.ToString("yyyyMMddHH", CultureInfo.InvariantCulture), "your PC has been uploading more than 5 Mb/s for a minute. Busiest programs right now: " + who + ". Backups and OneDrive do this; if you didn't expect it, check them.", "Live capture (with Wireshark) shows exactly which program is uploading and where to.");
            }
        }
        public static bool IsPublic(string ip)
        {
            IPAddress a; if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out a)) return false;
            if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && a.IsIPv4MappedToIPv6) a = a.MapToIPv4();   // ::ffff:192.168.1.1 is 192.168.1.1
            if (IPAddress.IsLoopback(a) || a.Equals(IPAddress.IPv6Any) || a.Equals(IPAddress.IPv6None)) return false;
            var b = a.GetAddressBytes();
            if (b.Length == 4) return !(b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] < 32) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] >= 64 && b[1] < 128) || b[0] >= 224 || b[0] == 0);
            return !(a.IsIPv6LinkLocal || a.IsIPv6SiteLocal || a.IsIPv6Multicast || (b[0] & 0xfe) == 0xfc);
        }
    }

    // ================================================================== new device on your network (from the ARP table - nothing is scanned)
    static class Devices
    {
        static Dictionary<string, HashSet<string>> known; static readonly object k = new object();
        public static readonly List<string[]> Joined = new List<string[]>();   // { when, network, ip, mac, note }
        public static DateTime QuietUntil = DateTime.MinValue;   // after a ping sweep: learn what it found, without "new device" alerts
        static Dictionary<string, DateTime> netSince; static readonly Dictionary<string, DateTime> lastPop = new Dictionary<string, DateTime>();
        // Every new device is listed (What changed). A pop-up only when you asked for it, on a home network AdeeboMeter
        // has known for a day, for a device with a real (not private / random) hardware address, at most once an hour.
        // Phones change their private address per network and over time, and office networks are full of other people's
        // devices - announcing those would be noise.
        public static void Check(Monitor m)
        {
            string key = Brain.NetKey, label = Brain.NetLabel; if (key == "" || key == "offline") return;
            List<Arp.Entry> table; try { table = Arp.Table(); } catch { return; }
            string gw; bool vpn; lock (m.L) { gw = m.Gateway; vpn = m.VpnUp; }
            try { Picks.SeeDevices(key, table, gw); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("inventory: " + e.Message); }
            bool company = vpn || !string.Equals(Environment.UserDomainName, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
            var announce = new List<Arp.Entry>();
            lock (k)
            {
                if (known == null) { known = new Dictionary<string, HashSet<string>>(); foreach (var r in Store.Read("devices.txt")) { HashSet<string> s; if (!known.TryGetValue(r[0], out s)) known[r[0]] = s = new HashSet<string>(); s.Add(r[1]); } }
                if (netSince == null) { netSince = new Dictionary<string, DateTime>(); foreach (var r in Store.Read("device-nets.txt")) { DateTime t; if (r.Length > 1 && DateTime.TryParse(r[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t)) netSince[r[0]] = t; } }
                HashSet<string> set; bool first = !known.TryGetValue(key, out set); if (first) known[key] = set = new HashSet<string>();
                DateTime since; if (!netSince.TryGetValue(key, out since)) { since = DateTime.Now; netSince[key] = since; Store.Write("device-nets.txt", netSince.Select(kv => new[] { kv.Key, kv.Value.ToString("o", CultureInfo.InvariantCulture) }).ToList()); }
                bool quiet = first || DateTime.UtcNow < QuietUntil || (DateTime.Now - since).TotalHours < 24;   // learning: the first day on a network, and after a ping sweep
                bool changed = false; int newHere = 0;
                foreach (var e in table.Where(x => x.Dynamic && x.Ip != gw))
                {
                    if (!set.Add(e.Mac)) continue;
                    changed = true;
                    if (first) continue;   // the first visit to a network is its baseline, not a list of "new" devices
                    if (++newHere > 10) continue;   // after a sweep: ten listed, the rest counted below
                    // always listed in What changed and on the timeline; the learning period only holds back the pop-up
                    bool rnd = Arp.Randomised(e.Mac);
                    string note = rnd ? "uses a private (random) hardware address - phones and laptops change it, so it may not be new" : "hardware address " + e.Mac;
                    if (quiet) note += " · learning this network (no alert)";
                    lock (Joined) { Joined.Insert(0, new[] { DateTime.Now.ToString("ddd HH:mm", CultureInfo.CurrentCulture), label, e.Ip, e.Mac, note }); if (Joined.Count > 30) Joined.RemoveAt(Joined.Count - 1); }
                    m.Events.Add(3, "lan", "New on " + label + ": " + e.Ip + " (" + note + ")");
                    if (!rnd && !quiet) announce.Add(e);
                }
                if (newHere > 10) m.Events.Add(3, "lan", (newHere - 10) + " more devices seen for the first time on " + label + " (after a sweep, or a busy network)");
                if (changed) Store.Write("devices.txt", known.SelectMany(kv => kv.Value.Select(mac => new[] { kv.Key, mac })).ToList());
                DateTime last; lastPop.TryGetValue(key, out last);
                if (announce.Count == 0 || Cfg.DevAlerts == 0 || company || set.Count > 30 || (DateTime.Now - last).TotalMinutes < 60) return;
                lastPop[key] = DateTime.Now;
            }
            Alerts.Raise("dev|" + key, (announce.Count == 1 ? "A new device joined " + label + ": " + announce[0].Ip : announce.Count + " new devices joined " + label + ": " + string.Join(", ", announce.Take(3).Select(e => e.Ip))) + ". If you don't recognise it, check your router's device list.", 60);
        }
    }

    // ================================================================== watch a pinned destination all day (opt-in)
    static class Pinned
    {
        public static readonly Ring Ms = new Ring(2880);   // 24 h at one ping per 30 s (NaN = no answer)
        public static volatile string Host = ""; static DateTime last = DateTime.MinValue; static volatile bool busy;
        public static void Tick()
        {
            string pin; try { pin = Cfg.Pins.FirstOrDefault(); } catch (InvalidOperationException) { return; } catch (ArgumentOutOfRangeException) { return; }   // the list belongs to the UI thread: read once, never Count then [0]
            if (!Cfg.PinWatch || pin == null) { Host = ""; return; }
            if ((Clock.Mono - last).TotalSeconds < 30 || busy) return;
            last = Clock.Mono; busy = true; string h; int port; bool tls; string err;
            if (!DestCheck.Parse(pin, out h, out port, out tls, out err)) { busy = false; return; }
            if (h != Host) { Host = h; }
            Task.Factory.StartNew(() => { try { Ms.Add(Monitor.PingMs(h)); } finally { busy = false; } });
        }
    }

    // ================================================================== a crash leaves a note; the next start offers to report it
    static class Crash
    {
        public static string FilePath { get { return Path.Combine(Program.WriteDir, "AdeeboMeter-crash.txt"); } }
        public static void Write(object ex)
        {
            try { File.WriteAllText(FilePath, "AdeeboMeter " + Program.Version + " crashed " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\r\nWindows " + Environment.OSVersion.Version + ", CLR " + Environment.Version + "\r\n\r\n" + Clean(Convert.ToString(ex, CultureInfo.InvariantCulture))); }
            catch { }
        }
        // the report never carries your user name or folders
        public static string Clean(string s)
        {
            if (s == null) return "";
            string user = Environment.UserName, home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home)) s = s.Replace(home, "%USERPROFILE%");
            if (!string.IsNullOrEmpty(user) && user.Length > 2) s = s.Replace(user, "<user>");
            return s.Length > 6000 ? s.Substring(0, 6000) : s;
        }
        // GitHub's "new issue" page with the crash filled in. Opens in your browser; nothing is sent until you press Submit there.
        public static string IssueUrl(string text)
        {
            // the whole address stays under ~7,000 characters (GitHub refuses much longer ones): shorten the note until it fits
            string note = text ?? ""; string url;
            do
            {
                string body = "**What were you doing when it crashed?**\n\n\n**Crash note** (no user names or folders):\n```\n" + note + "\n```";
                url = "https://github.com/adeeb111/AdeeboMeter/issues/new?labels=bug&title=" + Uri.EscapeDataString("Crash in " + Program.Version) + "&body=" + Uri.EscapeDataString(body);
                note = note.Length > 200 ? note.Substring(0, note.Length * 3 / 4) : "";
            } while (url.Length > 7000 && note != "");
            return url;
        }
        // only AdeeboMeter's own pages (GitHub, the website) and Adeeb's PayPal donation link ever open
        static readonly char[] Slash = { '/' };
        public static bool Allowed(string url)
        {
            // parsed, then host and path compared exactly - "AdeeboMeterEvil" or "AdeeboMeter@evil" never pass a prefix test
            Uri u; if (!Uri.TryCreate(url ?? "", UriKind.Absolute, out u) || u.Scheme != Uri.UriSchemeHttps || u.UserInfo != "" || !u.IsDefaultPort) return false;
            var seg = u.AbsolutePath.Split(Slash, StringSplitOptions.RemoveEmptyEntries);
            string host = u.Host.ToLowerInvariant();
            if (host == "github.com") return seg.Length >= 2 && seg[0] == "adeeb111" && string.Equals(seg[1], "AdeeboMeter", StringComparison.OrdinalIgnoreCase);
            if (host == "adeeb111.github.io") return seg.Length >= 1 && seg[0] == "AdeeboMeter";
            if (host == "paypal.me") return seg.Length >= 1 && seg[0] == "adeeb1111" && seg.Length <= 2;
            return false;
        }
        public static void OpenUrl(string url)
        {
            if (!Allowed(url)) return;   // only our own pages
            try { using (Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })) { } } catch (Exception e) { Program.Log("open: " + e.Message); }
        }
    }
}
