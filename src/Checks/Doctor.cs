// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // Network Doctor (PLAN-1.1.7 D1 + D6, 28 Nov): "it broke after something changed". What changed on this PC (Windows
    // updates, drivers, hardware coming and going, restarts and crashes, and a daily snapshot of the network settings,
    // apps, services, startup items and scheduled tasks), lined up against how each day went (typical ping, drops). The
    // change that lines up best is named as a SUSPECT with its evidence - never as a fact. No admin rights: what Windows
    // hides is simply not in the snapshot. Everything stays on this PC, and only while History is on.
    static class Doctor
    {
        public sealed class Item { public DateTime T; public string Kind = "", What = ""; public UpdateBreak.Finding F; }
        public sealed class Result
        {
            public List<UpdateBreak.DayStat> Days = new List<UpdateBreak.DayStat>();
            public List<Item> Changes = new List<Item>();
            public Item Suspect; public string Why = "";
            public string WhyT; public int DataDays;   /* 28 Nov: Why with {0} in place of the day count - the page translates this one (Why stays English for reports) */
            public DateTime At = DateTime.Now; public int Snapshots;
        }

        static string Dir { get { return Path.Combine(Program.DataDir, "doctor"); } }
        static string SnapFile(DateTime d) { return Path.Combine(Dir, "snap-" + d.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".txt"); }

        // ---------- the daily snapshot ("Windows Git"): one sorted text file a day, "section | key | value"
        public static List<string> Snapshot()
        {
            var l = new List<string>();
            Action<string, string, string> add = (sec, key, val) => { key = Clean(key); if (key != "") l.Add(sec + " | " + key + " | " + Clean(val)); };
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    var p = ni.GetIPProperties();
                    add("Adapter", ni.Description, ni.OperationalStatus + (ni.Speed > 0 ? " " + (ni.Speed / 1000000).ToString(CultureInfo.InvariantCulture) + " Mb/s" : ""));
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var g in p.GatewayAddresses) add("Router", ni.Description, g.Address.ToString());
                    foreach (var d in p.DnsAddresses) add("DNS", ni.Description + " " + d.ToString(), "in use");
                }
            }
            catch (Exception e) { if (!(e is NetworkInformationException || e is InvalidOperationException)) throw; }
            Reg(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", (k, n, v) => { if (n == "ProxyEnable" || n == "ProxyServer" || n == "AutoConfigURL") add("Proxy", n, v); });
            try { string hosts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts"); if (File.Exists(hosts)) foreach (var line in File.ReadAllLines(hosts)) { var t = line.Trim(); if (t != "" && !t.StartsWith("#", StringComparison.Ordinal)) add("Hosts file", t, "entry"); } }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            foreach (var prof in new[] { "DomainProfile", "StandardProfile", "PublicProfile" })
                Reg(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\" + prof, (k, n, v) => { if (n == "EnableFirewall") add("Firewall", prof.Replace("Profile", "") == "Standard" ? "Private" : prof.Replace("Profile", ""), v == "1" ? "on" : "off"); });
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
                foreach (var path in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
                    Subkeys(root, path, k => { string n = Convert.ToString(k.GetValue("DisplayName", ""), CultureInfo.InvariantCulture); if (n != "" && k.GetValue("SystemComponent") == null) add("App", n, Convert.ToString(k.GetValue("DisplayVersion", ""), CultureInfo.InvariantCulture)); });
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
                Reg(root, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", (k, n, v) => add("Starts with Windows", n, Path.GetFileName(Regex.Match(v ?? "", "^\"?([^\"]+?\\.exe)").Groups[1].Value)));
            // services, from the registry (no extra library, no admin): Win32 services only (Type 0x10 / 0x20), with how they start
            Subkeys(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services", k =>
            {
                int type, start; try { type = Convert.ToInt32(k.GetValue("Type", 0), CultureInfo.InvariantCulture); start = Convert.ToInt32(k.GetValue("Start", 3), CultureInfo.InvariantCulture); } catch (FormatException) { return; } catch (InvalidCastException) { return; } catch (OverflowException) { return; }
                if ((type & 0x30) == 0) return;
                string dn = Convert.ToString(k.GetValue("DisplayName", ""), CultureInfo.InvariantCulture); if (dn == "" || dn.StartsWith("@", StringComparison.Ordinal)) dn = Path.GetFileName(k.Name);
                add("Service", dn, start == 2 ? "automatic" : start == 3 ? "manual" : start == 4 ? "disabled" : "boot");
            });
            // scheduled tasks: the task folder lists the ones this user may read (no admin) - only names, never their contents
            try { string tdir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\Tasks"); if (Directory.Exists(tdir)) foreach (var f in Directory.EnumerateFiles(tdir, "*", SearchOption.TopDirectoryOnly).Take(300)) add("Scheduled task", Path.GetFileName(f), "present"); }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            l.Sort(StringComparer.OrdinalIgnoreCase);
            return l.Distinct().ToList();
        }
        static string Clean(string s) { return Regex.Replace(s ?? "", @"[\r\n|]+", " ").Trim(); }
        static void Reg(RegistryKey root, string path, Action<RegistryKey, string, string> each)
        {
            try { using (var k = root.OpenSubKey(path)) if (k != null) foreach (var n in k.GetValueNames()) each(k, n, Convert.ToString(k.GetValue(n), CultureInfo.InvariantCulture)); }
            catch (Exception e) { if (!(e is System.Security.SecurityException || e is UnauthorizedAccessException || e is IOException)) throw; }
        }
        static void Subkeys(RegistryKey root, string path, Action<RegistryKey> each)
        {
            try { using (var k = root.OpenSubKey(path)) if (k != null) foreach (var n in k.GetSubKeyNames()) using (var s = k.OpenSubKey(n)) if (s != null) each(s); }
            catch (Exception e) { if (!(e is System.Security.SecurityException || e is UnauthorizedAccessException || e is IOException)) throw; }
        }
        // once a day while History is on (the Doctor page asks too); 30 days kept
        public static void SnapToday()
        {
            if (!Cfg.History) return;
            try
            {
                string f = SnapFile(DateTime.Today); if (File.Exists(f)) return;
                Directory.CreateDirectory(Dir); SafeFile.WriteText(f, string.Join("\n", Snapshot()));
                foreach (var old in new DirectoryInfo(Dir).GetFiles("snap-*.txt").Where(x => x.LastWriteTime < DateTime.Now.AddDays(-30))) old.Delete();
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("doctor snapshot: " + e.Message); }
        }
        public static List<DateTime> SnapDays() { try { return Directory.Exists(Dir) ? new DirectoryInfo(Dir).GetFiles("snap-*.txt").Select(f => { DateTime d; return DateTime.TryParseExact(f.Name.Substring(5, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d : DateTime.MinValue; }).Where(d => d > DateTime.MinValue).OrderBy(d => d).ToList() : new List<DateTime>(); } catch (IOException) { return new List<DateTime>(); } }
        public static List<string> Load(DateTime d) { try { string f = SnapFile(d); return File.Exists(f) ? File.ReadAllLines(f).Where(z => z != "").ToList() : null; } catch (IOException) { return null; } }
        // a diff: "+ line" new, "- line" gone, "~ section | key | old -> new" changed
        public static List<string> Diff(List<string> a, List<string> b)
        {
            var out1 = new List<string>(); if (a == null || b == null) return out1;
            Func<string, string> key = s => { int i = s.LastIndexOf(" | ", StringComparison.Ordinal); return i > 0 ? s.Substring(0, i) : s; };
            Func<string, string> val = s => { int i = s.LastIndexOf(" | ", StringComparison.Ordinal); return i > 0 ? s.Substring(i + 3) : ""; };
            var ka = a.GroupBy(key).ToDictionary(g => g.Key, g => g.First()); var kb = b.GroupBy(key).ToDictionary(g => g.Key, g => g.First());
            foreach (var kv in kb) { string old; if (!ka.TryGetValue(kv.Key, out old)) out1.Add("+ " + kv.Value); else if (val(old) != val(kv.Value)) out1.Add("~ " + kv.Key + " | " + val(old) + " -> " + val(kv.Value)); }
            foreach (var kv in ka) if (!kb.ContainsKey(kv.Key)) out1.Add("- " + kv.Value);
            return out1.OrderBy(z => z.Substring(2), StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static string Plain(string d)
        {
            if (d.Length < 3) return d; var p = d.Substring(2).Split(new[] { " | " }, StringSplitOptions.None);
            string sec = p.Length > 0 ? p[0] : "", what = p.Length > 1 ? p[1] : "", v = p.Length > 2 ? p[2] : "";
            return d[0] == '+' ? sec + " added: " + what + (v != "" && v != "present" && v != "entry" && v != "in use" ? " (" + v + ")" : "")
                 : d[0] == '-' ? sec + " removed: " + what
                 : sec + " changed: " + what + " - " + v;
        }

        // ---------- everything put together, off the window thread
        public static Result Analyse(int days)
        {
            var r = new Result();
            for (int i = days - 1; i >= 0; i--) r.Days.Add(UpdateBreak.Day(DateTime.Today.AddDays(-i)));
            foreach (var c in UpdateBreak.Changes(days)) r.Changes.Add(new Item { T = c.T, Kind = c.Kind, What = c.What });
            string err;
            // restarts and crashes: 6005 the event log started (a boot), 6008 the shutdown before it wasn't clean, 41 power lost
            foreach (var e in EvLog.Recent("System", "EventID=6008 or EventID=41", days * 24, 40, out err)) r.Changes.Add(new Item { T = e.T, Kind = "crash", What = e.Id == 41 ? "the PC lost power or stopped responding" : "the PC didn't shut down cleanly" });
            int boots = 0; foreach (var e in EvLog.Recent("System", "EventID=6005", days * 24, 60, out err)) { boots++; if (boots <= 40) r.Changes.Add(new Item { T = e.T, Kind = "restart", What = "Windows started" }); }
            // hardware arriving or leaving (Kernel-PnP 410/430 configure, UserPnp 20003 driver for a new device)
            foreach (var e in EvLog.Recent("System", "Provider[@Name='Microsoft-Windows-UserPnp'] and EventID=20003", days * 24, 40, out err)) r.Changes.Add(new Item { T = e.T, Kind = "hardware", What = "new device: " + Draw.Trunc(Regex.Replace(e.Text ?? "", @"\s+", " "), 90) });
            // what the daily snapshots say changed, day to day
            var sd = SnapDays().Where(d => d >= DateTime.Today.AddDays(-days)).ToList(); r.Snapshots = sd.Count;
            for (int i = 1; i < sd.Count; i++)
            {
                var diff = Diff(Load(sd[i - 1]), Load(sd[i]));
                foreach (var grp in diff.GroupBy(z => (z.Length > 2 ? z.Substring(2) : z).Split(new[] { " | " }, StringSplitOptions.None)[0]))
                {
                    var list = grp.ToList(); string sec = grp.Key;
                    r.Changes.Add(new Item { T = sd[i].AddHours(12), Kind = "settings", What = list.Count == 1 ? Plain(list[0]) : sec + ": " + list.Count + " changes (" + Plain(list[0]) + "...)" });
                }
            }
            r.Changes = r.Changes.OrderByDescending(c => c.T).ToList();
            // the suspect: a change after which things got worse, starting the day after (UpdateBreak's before/after rule). Restarts
            // are context, not suspects.
            foreach (var c in r.Changes.Where(z => z.Kind != "restart")) c.F = UpdateBreak.Compare(new UpdateBreak.Change { T = c.T, What = c.What, Kind = c.Kind }, UpdateBreak.Day);
            r.Suspect = r.Changes.Where(c => c.F != null && c.F.Lv >= 1 && c.F.Step).OrderByDescending(c => c.F.Lv).ThenByDescending(c => c.F.Stayed).ThenByDescending(c => c.T).FirstOrDefault();
            int dataDays = r.Days.Count(d => !double.IsNaN(d.Ping)); r.DataDays = dataDays;
            const string fewDays = "Not enough history yet: {0} of the 4 days it needs. The Doctor compares a few days before and after a change.";
            if (Cfg.History && dataDays < 4) r.WhyT = fewDays;
            r.Why = !Cfg.History ? "History is off, so there's nothing to line changes up against. Turn it on in Settings › General."
                  : dataDays < 4 ? fewDays.Replace("{0}", dataDays.ToString(CultureInfo.InvariantCulture))
                  : r.Suspect == null ? (r.Days.Skip(Math.Max(0, r.Days.Count - 3)).Any(d => d.Drops > 0) ? "Things went wrong, but no change came just before it - this looks like the line or the provider, not this PC." : "Nothing changed just before things got worse - no suspect.")
                  : "";
            return r;
        }
    }
}
