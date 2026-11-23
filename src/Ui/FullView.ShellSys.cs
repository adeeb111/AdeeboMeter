// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // AdeeboShell, the "look at the machine" half (Adeeb, 22 Nov: "the go-to place for every Linux user to see disk and everything
    // as they do from the command line"): df du free ps top uptime ip ifconfig ss netstat lsblk lscpu nproc id who watch, /proc as
    // files, and the text tools people pipe into (cut tr sed awk xargs). All of it only reads - nothing here changes Windows,
    // stops a program or starts one.
    //
    // Shell scrutiny (22 Nov): numbers are measured, not dressed up - where Windows has no exact twin of a Linux figure the
    // nearest honest one is used and the help says which (load average, %CPU, "free" memory). Units, column order and spellings
    // follow the Linux tools, so scripts that parse them keep working.
    sealed partial class FullView
    {
        int SysCmd(string cmd, List<string> a, ShCtx c)
        {
            switch (cmd)
            {
                case "df": return Df(a, c);
                case "du": return Du(a, c);
                case "free": return Free(a, c);
                case "uptime": if (a.Any(z => Regex.IsMatch(z, @"^\d+$"))) return ToolAs("uptime", a, c, "the Toolbox's internet uptime report, for that many days (plain  uptime  is this PC's)"); OL(c, UptimeLine()); return 0;
                case "ps": return Ps(a, c);
                case "top": case "htop": return ShTop(a, c);
                case "ip": return Ip(a, c);
                case "ifconfig": case "ipconfig": return IpAddr(c, null, a.Contains("-s") || cmd == "ifconfig");
                case "route": { var dest = a.FirstOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal) && z != "print"); if (dest != null) return ToolAs("route", new List<string> { dest }, c, null); return IpRoute(c); }   // route 8.8.8.8: the Toolbox's "which route does this take?"
                case "ss": return Ss(a, c, false);
                case "netstat": return Ss(a, c, true);
                case "lsblk": return Lsblk(c);
                case "lscpu": return Lscpu(c);
                case "nproc": OL(c, (a.Contains("--all") ? Environment.ProcessorCount : UsableCpus()).ToString(CultureInfo.InvariantCulture)); return 0;   // #85
                case "id": return Id(c);
                case "who": case "w": return Who(cmd == "w", c);
                case "watch": return ShWatch(a, c);
                case "cut": return Cut(a, c);
                case "tr": return Tr(a, c);
                case "sed": return Sed(a, c);
                case "awk": case "gawk": return Awk(a, c);
                case "xargs": return Xargs(a, c);
                case "rev": { var l = InLines(Files(a), c); if (l == null) return 1; foreach (var x in l) { var si = StringInfo.GetTextElementEnumerator(x); var els = new List<string>(); while (si.MoveNext()) els.Add(si.GetTextElement()); els.Reverse(); OL(c, string.Concat(els)); } return 0; }   // #71: characters, not UTF-16 halves
                case "nl": { var l = InLines(Files(a), c); if (l == null) return 1; int n = 0; foreach (var x in l) OL(c, (x.Trim() == "" ? "" : (++n).ToString(CultureInfo.InvariantCulture)).PadLeft(6) + (x.Trim() == "" ? "" : "\t" + x)); return 0; }
                case "shuf": { var l = InLines(Files(a), c); if (l == null) return 1; var rnd = new Random(); foreach (var x in l.OrderBy(z => rnd.Next())) OL(c, x); return 0; }
                case "yes": { string w = a.Count > 0 ? string.Join(" ", a) : "y"; for (int i = 0; i < 10000 && !c.Job.Cancel; i++) OL(c, w); return 0; }
                case "base64": return B64(a, c);
                case "md5sum": case "sha1sum": case "sha256sum": case "sha512sum": return HashSum(cmd, a, c);
                // the Linux names for AdeeboMeter's own tools - #89: their arguments land where they belong, or the command says no
                case "traceroute": case "tracert": case "tracepath": { var h = a.LastOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal)); if (h == null) { Err(c, cmd + ": which address?"); return 2; } return ToolAs("mtr", new List<string> { h, "1" }, c, "traceroute here is MTR for a minute: every hop, its loss and its delay"); }
                case "nslookup": case "host": return Lookup(cmd, a, c);
                case "curl": case "wget": return Curl(cmd, a, c);
                case "arp": return ToolAs("arp", new List<string>(), c, null);
            }
            return int.MinValue;
        }
        int ToolAs(string id, List<string> args, ShCtx c, string note)
        {
            var t = shTools.FirstOrDefault(x => x.Id == id); if (t == null) { Err(c, id + ": not in this build"); return 127; }
            if (note != null && c.Out.Screen) OL(c, note, ShDim);
            return RunTool(t, args, c);
        }
        int Lookup(string cmd, List<string> a, ShCtx c)
        {
            string type = null; var plain = new List<string>();
            for (int i = 0; i < a.Count; i++)
            {
                string w = a[i];
                if (w == "-t" && i + 1 < a.Count) { type = a[++i]; continue; }
                if (w.StartsWith("-type=", StringComparison.OrdinalIgnoreCase) || w.StartsWith("-query=", StringComparison.OrdinalIgnoreCase) || w.StartsWith("-q=", StringComparison.OrdinalIgnoreCase)) { type = w.Substring(w.IndexOf('=') + 1); continue; }
                if (w.StartsWith("-", StringComparison.Ordinal)) { Err(c, cmd + ": " + w + " isn't supported here - name and -t TYPE (or -type=TYPE)"); return 2; }
                plain.Add(w);
            }
            if (plain.Count == 0) { Err(c, "usage: " + cmd + " name [-t MX]"); return 2; }
            if (plain.Count > 1) { Err(c, cmd + ": asking a chosen server (" + plain[1] + ") isn't supported here - dig " + plain[0] + " " + (type ?? "A") + " asks your own"); return 2; }
            return ToolAs("dig", new List<string> { plain[0], type ?? "A" }, c, null);
        }
        int Curl(string cmd, List<string> a, ShCtx c)
        {
            var takesValue = new HashSet<string> { "-o", "-O", "-H", "-d", "-X", "-A", "-u", "-e", "-w", "--output", "--header", "--data", "--request", "--user-agent", "--user", "--referer", "-T", "--upload-file", "-F", "--form", "-b", "--cookie", "-c", "--cookie-jar", "--max-time", "-m", "--connect-timeout", "-x", "--proxy" };
            string url = null; var ignored = new List<string>();
            for (int i = 0; i < a.Count; i++)
            {
                string w = a[i];
                if (takesValue.Contains(w)) { ignored.Add(w + " " + (i + 1 < a.Count ? a[++i] : "")); continue; }
                if (w.StartsWith("-", StringComparison.Ordinal)) { if (w != "-I" && w != "-s" && w != "-L" && w != "-v" && w != "-i" && w != "-sS" && w != "--head" && w != "--silent" && w != "--location") ignored.Add(w); continue; }
                if (url == null) url = w; else ignored.Add(w);
            }
            if (url == null) { Err(c, cmd + ": which address? (" + cmd + " https://example.com)"); return 2; }
            if (ignored.Count > 0) Err(c, cmd + ": here it looks at the answer (status, headers, TLS, timing) and downloads nothing - left out: " + string.Join(" ", ignored));
            return ToolAs("http", new List<string> { url }, c, ignored.Count == 0 ? cmd + " here looks at the answer - status, headers, TLS, timing - and downloads nothing" : null);
        }
        static string Human(double bytes)
        {
            string[] u = { "B", "K", "M", "G", "T", "P" }; int i = 0; while (bytes >= 1024 && i < u.Length - 1) { bytes /= 1024; i++; }
            return (i == 0 ? bytes.ToString("0", CultureInfo.InvariantCulture) : bytes < 10 ? bytes.ToString("0.0", CultureInfo.InvariantCulture) : bytes.ToString("0", CultureInfo.InvariantCulture)) + u[i];
        }
        static string MntOf(DriveInfo d) { return "/mnt/" + d.Name.Substring(0, 1).ToLowerInvariant(); }
        void Table(ShCtx c, List<string[]> rows, Color head, params bool[] right)
        {
            if (rows.Count == 0) return; int n = rows.Max(r => r.Length); var w = new int[n];
            foreach (var r in rows) for (int i = 0; i < r.Length; i++) w[i] = Math.Max(w[i], (r[i] ?? "").Length);
            for (int k = 0; k < rows.Count; k++)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < rows[k].Length; i++) { string v = rows[k][i] ?? ""; bool rt = i < right.Length && right[i]; if (i == rows[k].Length - 1 && !rt) sb.Append(v); else sb.Append(rt ? v.PadLeft(w[i]) : v.PadRight(w[i])).Append("  "); }
                OL(c, sb.ToString().TrimEnd(), k == 0 ? head : ShFg);
            }
        }

        // ---------- the machine's own counters: CPU busy and idle time (GetSystemTimes), a Linux-style load average
        // #4: Windows keeps no load average. This one is CPU demand (busy share × logical CPUs), sampled every 5 s from the first
        // time the shell needs it and averaged with Linux's own 1/5/15-minute decay - three different numbers, honestly made.
        // RG-7: the sampler stops by itself 15 minutes after the shell last asked (it's started again on the next ask) - the app
        // runs when pressed, not a counter forever
        static readonly object loadLock = new object(); static Timer loadTimer; static double[] loadAvg; static long loadIdle0, loadTotal0; static DateTime loadAsked;
        static double[] LoadAvg()
        {
            lock (loadLock)
            {
                loadAsked = DateTime.UtcNow;
                if (loadTimer == null)
                {
                    long i0, k0, u0; if (Native.GetSystemTimes(out i0, out k0, out u0)) { loadIdle0 = i0; loadTotal0 = k0 + u0; }
                    loadAvg = new[] { double.NaN, double.NaN, double.NaN };
                    loadTimer = new Timer(delegate
                    {
                        long i1, k1, u1; if (!Native.GetSystemTimes(out i1, out k1, out u1)) return;
                        lock (loadLock)
                        {
                            if (DateTime.UtcNow - loadAsked > TimeSpan.FromMinutes(15)) { if (loadTimer != null) { loadTimer.Dispose(); loadTimer = null; } return; }
                            long di = i1 - loadIdle0, dt = (k1 + u1) - loadTotal0; loadIdle0 = i1; loadTotal0 = k1 + u1; if (dt <= 0) return;
                            double now = Math.Max(0, 1 - (double)di / dt) * Environment.ProcessorCount; var e = new[] { Math.Exp(-5.0 / 60), Math.Exp(-5.0 / 300), Math.Exp(-5.0 / 900) };
                            for (int k = 0; k < 3; k++) loadAvg[k] = double.IsNaN(loadAvg[k]) ? now : loadAvg[k] * e[k] + now * (1 - e[k]);
                        }
                    }, null, 5000, 5000);
                }
                return loadAvg.ToArray();
            }
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool GetVolumeInformationW(string root, IntPtr name, int nameLen, out uint serial, out uint maxLen, out uint flags, IntPtr fs, int fsLen);
        // SH-S11: a volume Windows marks read-only (FILE_READ_ONLY_VOLUME) - a locked SD card, a write-protected stick, a mounted ISO
        static bool ReadOnlyVol(string root) { uint sn, ml, fl; try { return GetVolumeInformationW(root, IntPtr.Zero, 0, out sn, out ml, out fl, IntPtr.Zero, 0) && (fl & 0x00080000) != 0; } catch (EntryPointNotFoundException) { return false; } }
        static string LoadText(string sep) { var l = LoadAvg(); return string.Join(sep, l.Select(v => double.IsNaN(v) ? "0.00" : v.ToString("0.00", CultureInfo.InvariantCulture))); }
        static DateTime BootTime() { try { return DateTime.Now - TimeSpan.FromMilliseconds(Native.GetTickCount64()); } catch (EntryPointNotFoundException) { return DateTime.Now; } }

        // ---------- sessions (who, w, uptime's user count) - #17: real sessions and real logon times, from Windows' own list
        [DllImport("wtsapi32.dll")] static extern bool WTSEnumerateSessions(IntPtr server, int reserved, int version, out IntPtr info, out int count);
        // SH-S8: the Unicode entry point, always - without it .NET binds the ANSI one, whose names are bytes and whose WTSINFOA puts
        // LogonTime at 160, not 200. WTSINFOW: 8 ints (32) + WinStationName[32] (64) + Domain[17] (34) + UserName[21] (42) = 172,
        // padded to 176: ConnectTime 176, DisconnectTime 184, LastInputTime 192, LogonTime 200, CurrentTime 208 - 216 bytes
        [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "WTSQuerySessionInformationW")] static extern bool WTSQuerySessionInformation(IntPtr server, int session, int cls, out IntPtr buf, out int bytes);
        [DllImport("wtsapi32.dll")] static extern void WTSFreeMemory(IntPtr p);
        sealed class Sess { public string User, Station, State; public DateTime Logon; }
        static List<Sess> Sessions()
        {
            var l = new List<Sess>(); IntPtr info; int n; int ptr = Marshal.SizeOf(typeof(IntPtr));
            try
            {
                if (!WTSEnumerateSessions(IntPtr.Zero, 0, 1, out info, out n)) return l;
                try
                {
                    int size = ptr == 8 ? 24 : 12;   // WTS_SESSION_INFO: id, station name pointer, state
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr row = info + i * size; int id = Marshal.ReadInt32(row), st = Marshal.ReadInt32(row, ptr == 8 ? 16 : 8);
                        string station = Marshal.PtrToStringUni(Marshal.ReadIntPtr(row, ptr == 8 ? 8 : 4)) ?? "";
                        IntPtr b; int bytes; string user = "";
                        if (WTSQuerySessionInformation(IntPtr.Zero, id, 5, out b, out bytes)) { user = Marshal.PtrToStringUni(b) ?? ""; WTSFreeMemory(b); }   // WTSUserName
                        if (user == "") continue;
                        DateTime logon = DateTime.MinValue;
                        if (WTSQuerySessionInformation(IntPtr.Zero, id, 24, out b, out bytes)) { try { if (bytes >= 216) { long ft = Marshal.ReadInt64(b, 200); if (ft > 0) logon = DateTime.FromFileTime(ft); } } finally { WTSFreeMemory(b); } }   // WTSSessionInfo: LogonTime
                        l.Add(new Sess { User = user.ToLowerInvariant(), Station = station == "" ? "session" + id : station.ToLowerInvariant(), State = st == 0 ? "active" : st == 4 ? "disconnected" : "other", Logon = logon });
                    }
                }
                finally { WTSFreeMemory(info); }
            }
            catch (Exception e) { if (!(e is DllNotFoundException || e is EntryPointNotFoundException)) throw; }
            return l;
        }
        string UptimeLine()
        {
            var up = DateTime.Now - BootTime(); int users = Math.Max(1, Sessions().Count);
            return " " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " up " + (up.Days > 0 ? up.Days + " day" + (up.Days == 1 ? "" : "s") + ", " : "") + up.Hours.ToString("00", CultureInfo.InvariantCulture) + ":" + up.Minutes.ToString("00", CultureInfo.InvariantCulture) + ",  " + users + " user" + (users == 1 ? "" : "s") + ",  load average: " + LoadText(", ");
        }
        int Who(bool w, ShCtx c)
        {
            var s = Sessions(); if (s.Count == 0) s.Add(new Sess { User = ShUser, Station = "console", State = "active", Logon = DateTime.MinValue });
            if (w) OL(c, UptimeLine());
            foreach (var x in s) OL(c, x.User.PadRight(12) + x.Station.PadRight(14) + (x.Logon == DateTime.MinValue ? "-" : x.Logon.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)) + (x.State != "active" ? "  (" + x.State + ")" : ""));
            return 0;
        }
        // #41: uid, gid and groups are numbers (the last part of each Windows SID), with their names
        int Id(ShCtx c)
        {
            using (var me = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                Func<System.Security.Principal.SecurityIdentifier, string> rid = sid => sid.Value.Substring(sid.Value.LastIndexOf('-') + 1);
                Func<System.Security.Principal.IdentityReference, string> name = r => { try { var n = r.Translate(typeof(System.Security.Principal.NTAccount)).Value; return n.Substring(n.IndexOf('\\') + 1); } catch (Exception e) { if (!(e is System.Security.Principal.IdentityNotMappedException || e is SystemException)) throw; return r.Value; } };
                var groups = (me.Groups ?? new System.Security.Principal.IdentityReferenceCollection()).OfType<System.Security.Principal.SecurityIdentifier>().Take(24).ToList();
                var prim = groups.FirstOrDefault(g => g.Value.EndsWith("-513", StringComparison.Ordinal)) ?? groups.FirstOrDefault();
                OL(c, "uid=" + rid(me.User) + "(" + ShUser + ")" + (prim != null ? " gid=" + rid(prim) + "(" + name(prim) + ")" : "") + " groups=" + string.Join(",", groups.Select(g => rid(g) + "(" + name(g) + ")")));
                if (c.Out.Screen) OL(c, "(Windows: " + me.Name + " · the numbers are the last part of each SID)", ShDim);
            }
            return 0;
        }
        static int UsableCpus() { try { using (var p = Process.GetCurrentProcess()) { long m = p.ProcessorAffinity.ToInt64(); int n = 0; while (m != 0) { n += (int)(m & 1); m = (long)((ulong)m >> 1); } return Math.Max(1, n); } } catch (Exception e) { if (!(e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is NotSupportedException)) throw; return Environment.ProcessorCount; } }

        // ---------- disks
        // #75/#76: like GNU - 1K blocks unless -h, the Filesystem column one word (C:), Use% = used / (used + avail) rounded up
        int Df(List<string> a, ShCtx c)
        {
            string fl = Flags(a); bool h = fl.Contains('h'), m = fl.Contains('m'), typ = fl.Contains('T');
            string unitHead = h ? "Size" : m ? "1M-blocks" : "1K-blocks";
            var rows = new List<string[]> { typ ? new[] { "Filesystem", "Type", unitHead, "Used", "Avail", "Use%", "Mounted on" } : new[] { "Filesystem", unitHead, "Used", "Avail", "Use%", "Mounted on" } };
            foreach (var d in DriveInfo.GetDrives())
            {
                long tot, avail, free; string fs;
                try { if (!d.IsReady) continue; tot = d.TotalSize; avail = d.AvailableFreeSpace; free = d.TotalFreeSpace; fs = d.DriveFormat; } catch (IOException) { continue; } catch (UnauthorizedAccessException) { continue; }
                long used = tot - free; Func<long, string> f = v => h ? Human(v) : m ? (v / 1048576).ToString(CultureInfo.InvariantCulture) : (v / 1024).ToString(CultureInfo.InvariantCulture);
                string pct = used + avail > 0 ? Math.Ceiling(100.0 * used / (used + avail)).ToString("0", CultureInfo.InvariantCulture) + "%" : "-";
                string name = d.Name.TrimEnd('\\');
                rows.Add(typ ? new[] { name, fs.ToLowerInvariant(), f(tot), f(used), f(avail), pct, MntOf(d) } : new[] { name, f(tot), f(used), f(avail), pct, MntOf(d) });
            }
            Table(c, rows, ShBlue, false, typ, true, true, true, true);
            return 0;
        }
        // #13/#14/#83/#84: disks, then their own partitions under them (Windows' disk → partition → drive-letter links); drives that
        // aren't on a fixed disk (DVD, card reader, network) listed by themselves; a missing physical-disk half says so
        int Lsblk(ShCtx c)
        {
            var rows = new List<string[]> { new[] { "NAME", "SIZE", "TYPE", "FSTYPE", "LABEL", "MOUNTPOINT", "MODEL" } }; var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase); string wmiErr = null;
            try
            {
                using (var q = new System.Management.ManagementObjectSearcher("SELECT DeviceID, Index, Model, Size FROM Win32_DiskDrive"))
                    foreach (var disk in q.Get().Cast<System.Management.ManagementObject>().OrderBy(o => Convert.ToInt32(o["Index"], CultureInfo.InvariantCulture)))
                        using (disk)
                        {
                            ulong sz = 0; try { sz = Convert.ToUInt64(disk["Size"] ?? 0UL, CultureInfo.InvariantCulture); } catch (FormatException) { }
                            string dn = "disk" + disk["Index"];
                            rows.Add(new[] { dn, Human(sz), "disk", "", "", "", Convert.ToString(disk["Model"], CultureInfo.InvariantCulture).Trim() });
                            try   // 1.1.6 PC test: one disk WMI can't follow (a card reader, a virtual disk: "Not found") no longer hides the others
                            {
                            using (var pq = new System.Management.ManagementObjectSearcher("ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + Convert.ToString(disk["DeviceID"], CultureInfo.InvariantCulture).Replace("\\", "\\\\") + "'} WHERE AssocClass = Win32_DiskDriveToDiskPartition"))
                            {
                                var parts = pq.Get().Cast<System.Management.ManagementObject>().OrderBy(p => Convert.ToInt32(p["Index"], CultureInfo.InvariantCulture)).ToList();
                                for (int i = 0; i < parts.Count; i++)
                                    using (var part = parts[i])
                                    {
                                        bool last = i == parts.Count - 1; ulong psz = 0; try { psz = Convert.ToUInt64(part["Size"] ?? 0UL, CultureInfo.InvariantCulture); } catch (FormatException) { }
                                        string letter = "", fs = "", label = "";
                                        using (var lq = new System.Management.ManagementObjectSearcher("ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + Convert.ToString(part["DeviceID"], CultureInfo.InvariantCulture) + "'} WHERE AssocClass = Win32_LogicalDiskToPartition"))
                                            foreach (System.Management.ManagementObject ld in lq.Get()) using (ld) { letter = Convert.ToString(ld["DeviceID"], CultureInfo.InvariantCulture); fs = Convert.ToString(ld["FileSystem"], CultureInfo.InvariantCulture).ToLowerInvariant(); label = Convert.ToString(ld["VolumeName"], CultureInfo.InvariantCulture); }
                                        if (letter != "") placed.Add(letter);
                                        rows.Add(new[] { (last ? "└─" : "├─") + dn + "p" + (i + 1), Human(psz), "part", fs, label, letter != "" ? "/mnt/" + letter.Substring(0, 1).ToLowerInvariant() : "", "" });
                                    }
                            }
                            }
                            catch (System.Management.ManagementException pe) { wmiErr = dn + ": " + pe.Message; }
                        }
            }
            catch (Exception e) { if (!(e is System.Management.ManagementException || e is UnauthorizedAccessException || e is COMException)) throw; wmiErr = e.Message; }
            foreach (var d in DriveInfo.GetDrives().Where(d => !placed.Contains(d.Name.Substring(0, 2))))
            {
                string fs = "", lab = ""; long sz = 0; bool ready = false;
                try { ready = d.IsReady; if (ready) { fs = d.DriveFormat.ToLowerInvariant(); lab = d.VolumeLabel; sz = d.TotalSize; } } catch (IOException) { }
                string type = d.DriveType == DriveType.CDRom ? "rom" : d.DriveType == DriveType.Network ? "net" : d.DriveType == DriveType.Removable ? "removable" : d.DriveType == DriveType.Ram ? "ram" : "volume";
                rows.Add(new[] { d.Name.Substring(0, 2).ToLowerInvariant(), ready ? Human(sz) : "-", type, fs, lab, ready ? MntOf(d) : "(not ready)", "" });
            }
            Table(c, rows, ShBlue, false, true);
            if (wmiErr != null) Err(c, "lsblk: Windows didn't list the physical disks (" + wmiErr.Trim() + ") - only drive letters are shown");
            return 0;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern uint GetCompressedFileSizeW(string name, out uint high);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle h, out ByHandleInfo info);
        [StructLayout(LayoutKind.Sequential)] struct ByHandleInfo { public uint Attrs, CLo, CHi, ALo, AHi, WLo, WHi, Volume, SizeHi, SizeLo, Links, IdxHi, IdxLo; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CreateHardLinkW(string link, string target, IntPtr sec);
        // the self-test: one 64 KB file with two names is 64 for du, 128 for du -l (null = right)
        internal string TestDuLinks()
        {
            string dir = Path.Combine(Path.GetTempPath(), "am-du-" + Guid.NewGuid().ToString("N").Substring(0, 8)); Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllBytes(Path.Combine(dir, "a.bin"), new byte[65536]);
                if (!CreateHardLinkW(Path.Combine(dir, "b.bin"), Path.Combine(dir, "a.bin"), IntPtr.Zero)) return null;   // a disk without hard links: nothing to check
                var o = new ShOut { Cap = new StringBuilder(), Job = new ShJob() }; var c = new ShCtx { Job = o.Job, Out = o, Script = true };
                Du(new List<string> { "-s", "--apparent-size", dir }, c); string one = o.Cap.ToString().Trim(); o.Cap.Clear();
                Du(new List<string> { "-sl", "--apparent-size", dir }, c); string both = o.Cap.ToString().Trim();
                return one.StartsWith("64 ", StringComparison.Ordinal) && both.StartsWith("128 ", StringComparison.Ordinal) ? null : "a hard-linked 64 KB file gave '" + one + "' and with -l '" + both + "'";
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        // a file with more than one name (a hard link): its volume and file number, so du counts it once like GNU du; null for an ordinary file
        static string LinkId(string f)
        {
            try { return LinkId1(f); } catch (DllNotFoundException) { return null; } catch (EntryPointNotFoundException) { return null; }
        }
        static string LinkId1(string f)
        {
            using (var hd = CreateFileW(f, 0x80 /* read attributes */, 7, IntPtr.Zero, 3 /* existing */, 0x02000000 | 0x00200000 /* backup semantics, the link itself */, IntPtr.Zero))
            {
                ByHandleInfo bi; if (hd.IsInvalid || !GetFileInformationByHandle(hd, out bi) || bi.Links < 2) return null;
                return bi.Volume.ToString("x8", CultureInfo.InvariantCulture) + ":" + bi.IdxHi.ToString("x8", CultureInfo.InvariantCulture) + bi.IdxLo.ToString("x8", CultureInfo.InvariantCulture);
            }
        }
        // #16/#75/#77/#78: GNU-style - 1K blocks unless -h (or -m), space actually taken on disk (compressed and sparse files count
        // what they use; --apparent-size gives the lengths), junctions never followed, and every folder it couldn't read named.
        // A hard-linked file counts once, wherever its first name is (-l / --count-links: every name)
        int Du(List<string> a, ShCtx c)
        {
            string fl = Flags(a); bool sum = fl.Contains('s'), h = fl.Contains('h'), mb = fl.Contains('m'), apparent = a.Contains("--apparent-size") || fl.Contains('b'), allLinks = fl.Contains('l') || a.Contains("--count-links"); int depth = sum ? 0 : int.MaxValue;
            foreach (var x in a) { var m = Regex.Match(x, @"^(?:--max-depth=|-d)(\d+)$"); if (m.Success) depth = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); }
            int di = a.IndexOf("-d"); if (di >= 0 && di + 1 < a.Count) int.TryParse(a[di + 1], out depth);
            var paths = a.Where((z, i) => !z.StartsWith("-", StringComparison.Ordinal) && !(i > 0 && a[i - 1] == "-d")).DefaultIfEmpty(".").ToList();
            int files = 0, denied = 0; var sw = Stopwatch.StartNew(); bool ex1 = false;
            Func<long, string> unit = v => h ? Human(v) : mb ? ((v + 1048575) / 1048576).ToString(CultureInfo.InvariantCulture) : ((v + 1023) / 1024).ToString(CultureInfo.InvariantCulture);
            Func<string, long> fileSize = f => { if (!apparent) { uint hi; uint lo = GetCompressedFileSizeW(f, out hi); if (!(lo == 0xFFFFFFFF && Marshal.GetLastWin32Error() != 0)) return ((long)hi << 32) | lo; } try { return new FileInfo(f).Length; } catch (IOException) { return 0; } };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Func<string, long> once = f => { if (!allLinks) { string id = LinkId(f); if (id != null && !seen.Add(id)) return 0; } return fileSize(f); };
            Func<string, string, int, long> walk = null;
            walk = (dir, shown, level) =>
            {
                long t = 0; if (c.Job.Cancel || files > 1000000) return 0;
                try
                {
                    foreach (var f in Directory.EnumerateFiles(dir)) { files++; t += once(f); }
                    foreach (var d in Directory.EnumerateDirectories(dir)) { if (IsLink(d)) continue; t += walk(d, shown.TrimEnd('/') + "/" + Path.GetFileName(d), level + 1); }
                }
                catch (UnauthorizedAccessException) { denied++; if (denied <= 5) Err(c, "du: cannot read '" + shown + "': permission denied"); ex1 = true; }
                catch (IOException e) { denied++; if (denied <= 5) Err(c, "du: cannot read '" + shown + "': " + e.Message.Trim()); ex1 = true; }
                if (level <= depth && level > 0) OL(c, unit(t).PadRight(8) + shown);
                if (c.Out.Screen && !c.Job.Bg && sw.ElapsedMilliseconds > 400) { sw.Restart(); int f0 = files; Ui(delegate { term.Live(new List<List<TermView.Seg>> { new List<TermView.Seg> { new TermView.Seg("du  ", ShCyan), new TermView.Seg(f0.ToString("N0", CultureInfo.CurrentCulture) + " files counted · Ctrl+C stops", ShDim) } }); }); }
                return t;
            };
            foreach (var p in paths)
            {
                string r = Real(Abs(p)); if (r == null || (!Directory.Exists(r) && !File.Exists(r))) { Err(c, "du: " + p + ": not a folder or file on a disk"); ex1 = true; continue; }
                if (File.Exists(r)) { OL(c, unit(fileSize(r)).PadRight(8) + p); continue; }
                long total = walk(r, p, 0);
                if (c.Out.Screen && !c.Job.Bg) Ui(delegate { term.EndLive(); });
                OL(c, unit(total).PadRight(8) + p, ShCyan);
            }
            if (denied > 5) Err(c, "du: " + denied + " folders couldn't be read in all - the totals leave them out");
            if (files > 1000000) Err(c, "du: stopped counting at 1,000,000 files - the totals are partial");
            return c.Job.Cancel ? 130 : ex1 ? 1 : 0;
        }

        // ---------- memory, processor
        // #39: the Linux columns - free is memory nothing holds (Windows' free + zeroed pages), buff/cache what the standby cache
        // holds and gives back on demand, available both together; used is the rest
        static long FreePages()
        {
            try { using (var pc = new PerformanceCounter("Memory", "Free & Zero Page List Bytes", true)) return (long)pc.NextValue(); }
            catch (Exception e) { if (!(e is InvalidOperationException || e is UnauthorizedAccessException || e is System.ComponentModel.Win32Exception || e is FormatException)) throw; return -1; }
        }
        int Free(List<string> a, ShCtx c)
        {
            var ms = new Native.MEMORYSTATUSEX(); if (!Native.GlobalMemoryStatusEx(ms)) { Err(c, "free: Windows didn't say"); return 1; }
            string fl = Flags(a);
            Func<ulong, string> f = v => fl.Contains('b') ? v.ToString(CultureInfo.InvariantCulture) : fl.Contains('m') ? (v / 1048576).ToString(CultureInfo.InvariantCulture) : fl.Contains('g') ? (v / 1073741824).ToString(CultureInfo.InvariantCulture) : fl.Contains('h') ? Human(v) : (v / 1024).ToString(CultureInfo.InvariantCulture);
            long fp = FreePages(); ulong avail = ms.ullAvailPhys, free = fp >= 0 ? Math.Min(avail, (ulong)fp) : avail, cache = avail - free;
            ulong swapT = ms.ullTotalPageFile > ms.ullTotalPhys ? ms.ullTotalPageFile - ms.ullTotalPhys : 0, swapF = ms.ullAvailPageFile > ms.ullAvailPhys ? Math.Min(swapT, ms.ullAvailPageFile - ms.ullAvailPhys) : 0;
            Table(c, new List<string[]> {
                new[] { "", "total", "used", "free", "shared", "buff/cache", "available" },
                new[] { "Mem:", f(ms.ullTotalPhys), f(ms.ullTotalPhys - avail), f(free), "-", fp >= 0 ? f(cache) : "-", f(avail) },
                new[] { "Swap:", f(swapT), f(swapT - swapF), f(swapF) } }, ShBlue, false, true, true, true, true, true, true);
            if (c.Out.Screen) OL(c, "buff/cache is Windows' standby list (given back on demand) · Swap is the page file beyond the RAM", ShDim);
            return 0;
        }
        int Lscpu(ShCtx c)
        {
            string name = "", mhz = "";
            try { using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")) if (k != null) { name = Convert.ToString(k.GetValue("ProcessorNameString"), CultureInfo.InvariantCulture).Trim(); mhz = Convert.ToString(k.GetValue("~MHz"), CultureInfo.InvariantCulture); } }
            catch (Exception e) { if (!(e is System.Security.SecurityException || e is UnauthorizedAccessException || e is IOException)) throw; }
            Action<string, string> kv = (k, v) => { O(c, (k + ":").PadRight(24), ShBlue); OL(c, v); };
            string arch = (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ?? Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "").ToUpperInvariant();
            kv("Architecture", arch == "AMD64" ? "x86_64" : arch == "ARM64" ? "aarch64" : arch == "X86" ? "i686" : arch.ToLowerInvariant());   // #79
            kv("CPU(s)", Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)); kv("On-line CPU(s) usable", UsableCpus().ToString(CultureInfo.InvariantCulture));
            kv("Model name", name == "" ? "-" : name); if (mhz != "") kv("CPU rated MHz", mhz + "  (the rated speed - it boosts and throttles around it)");
            kv("Identifier", Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "-");
            return 0;
        }

        // ---------- processes: what's running, what it costs (read only - nothing is stopped from here)
        // #12: %CPU like Linux - 100 = one whole core (a busy 4-thread program can show 400). ps: since it started (CPU time ÷
        // time alive, as ps does); top: over its own refresh window, timed per process (#46). #42: TIME is [D-]HH:MM:SS.
        sealed class PsRow { public int Pid, Threads; public string Name; public long Mem; public double Cpu = double.NaN; public TimeSpan Time; }
        static string PsTime(TimeSpan t) { return (t.Days > 0 ? t.Days + "-" : "") + t.Hours.ToString("00", CultureInfo.InvariantCulture) + ":" + t.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + t.Seconds.ToString("00", CultureInfo.InvariantCulture); }
        static bool ProcErr(Exception e) { return e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is NotSupportedException; }
        List<PsRow> PsLifetime()
        {
            var l = new List<PsRow>(); var now = DateTime.Now;
            foreach (var p in Process.GetProcesses())
                using (p)
                {
                    var r = new PsRow { Pid = p.Id };
                    try { r.Name = p.ProcessName; r.Mem = p.WorkingSet64; r.Threads = p.Threads.Count; } catch (Exception e) { if (!ProcErr(e)) throw; if (r.Name == null) continue; }
                    try { r.Time = p.TotalProcessorTime; double alive = (now - p.StartTime).TotalSeconds; if (alive > 0.5) r.Cpu = 100 * r.Time.TotalSeconds / alive; } catch (Exception e) { if (!ProcErr(e)) throw; }
                    l.Add(r);
                }
            return l;
        }
        // a window: every process timed at both ends of it with its own clock; the whole machine's busy share from the same window
        List<PsRow> PsWindow(int ms, ShCtx c, out double machine)
        {
            var first = new Dictionary<int, KeyValuePair<TimeSpan, long>>(); var procs = Process.GetProcesses(); machine = double.NaN;
            try
            {
                long i0, k0, u0; bool st = Native.GetSystemTimes(out i0, out k0, out u0);
                foreach (var p in procs) try { first[p.Id] = new KeyValuePair<TimeSpan, long>(p.TotalProcessorTime, Stopwatch.GetTimestamp()); } catch (Exception e) { if (!ProcErr(e)) throw; }
                var sw = Stopwatch.StartNew(); while (sw.ElapsedMilliseconds < ms && !c.Job.Cancel) Thread.Sleep(50);
                long i1, k1, u1; if (st && Native.GetSystemTimes(out i1, out k1, out u1) && (k1 + u1) - (k0 + u0) > 0) machine = 100 * Math.Max(0, 1 - (double)(i1 - i0) / ((k1 + u1) - (k0 + u0)));
                var l = new List<PsRow>();
                foreach (var p in procs)
                {
                    var r = new PsRow { Pid = p.Id };
                    try { r.Name = p.ProcessName; r.Mem = p.WorkingSet64; r.Threads = p.Threads.Count; } catch (Exception e) { if (!ProcErr(e)) throw; if (r.Name == null) continue; }
                    try { p.Refresh(); var t = p.TotalProcessorTime; long now = Stopwatch.GetTimestamp(); r.Time = t; KeyValuePair<TimeSpan, long> t0; if (first.TryGetValue(p.Id, out t0)) { double win = (now - t0.Value) / (double)Stopwatch.Frequency; if (win > 0.05) r.Cpu = Math.Max(0, 100 * (t - t0.Key).TotalSeconds / win); } } catch (Exception e) { if (!ProcErr(e)) throw; }
                    l.Add(r);
                }
                return l;
            }
            finally { foreach (var p in procs) p.Dispose(); }
        }
        int Ps(List<string> a, ShCtx c)
        {
            string sort = a.FirstOrDefault(z => z.StartsWith("--sort", StringComparison.Ordinal)) ?? ""; var l = PsLifetime();
            IEnumerable<PsRow> s = sort.Contains("cpu") ? l.OrderByDescending(r => double.IsNaN(r.Cpu) ? -1 : r.Cpu) : sort.Contains("mem") || sort.Contains("rss") ? l.OrderByDescending(r => r.Mem) : l.OrderBy(r => r.Pid);
            var rows = new List<string[]> { new[] { "PID", "%CPU", "RSS", "THR", "TIME", "COMMAND" } };
            foreach (var r in s) rows.Add(new[] { r.Pid.ToString(CultureInfo.InvariantCulture), double.IsNaN(r.Cpu) ? "-" : r.Cpu.ToString("0.0", CultureInfo.InvariantCulture), (r.Mem / 1024).ToString(CultureInfo.InvariantCulture), r.Threads.ToString(CultureInfo.InvariantCulture), PsTime(r.Time), r.Name });
            Table(c, rows, ShBlue, true, true, true, true, true);
            return 0;
        }
        List<List<TermView.Seg>> TopFrame(List<PsRow> l, double machine, int n, double secs)
        {
            var f = new List<List<TermView.Seg>>(); var ms = new Native.MEMORYSTATUSEX(); Native.GlobalMemoryStatusEx(ms);
            Func<string, Color, List<TermView.Seg>> one = (t, col) => new List<TermView.Seg> { new TermView.Seg(t, col) };
            f.Add(one("top - " + UptimeLine().Trim(), ShFg));
            f.Add(one("Tasks: " + l.Count + " total · threads " + l.Sum(r => r.Threads), ShFg));
            f.Add(one("%Cpu(s): " + (double.IsNaN(machine) ? "?" : machine.ToString("0.0", CultureInfo.InvariantCulture)) + " used of " + Environment.ProcessorCount + " CPUs, over the same " + secs.ToString("0.#", CultureInfo.InvariantCulture) + " s as the rows", ShFg));
            f.Add(one("MiB Mem: " + (ms.ullTotalPhys / 1048576) + " total, " + (ms.ullAvailPhys / 1048576) + " available, " + ((ms.ullTotalPhys - ms.ullAvailPhys) / 1048576) + " used (" + ms.dwMemoryLoad + "%)", ShFg));
            f.Add(one("", ShFg));
            f.Add(one("    PID   %CPU      RES  THR      TIME  COMMAND", ShBlue));
            foreach (var r in l.OrderByDescending(r => double.IsNaN(r.Cpu) ? -1 : r.Cpu).ThenByDescending(r => r.Mem).Take(n))
                f.Add(one(r.Pid.ToString(CultureInfo.InvariantCulture).PadLeft(7) + (double.IsNaN(r.Cpu) ? "-" : r.Cpu.ToString("0.0", CultureInfo.InvariantCulture)).PadLeft(7) + Human(r.Mem).PadLeft(9) + r.Threads.ToString(CultureInfo.InvariantCulture).PadLeft(5) + PsTime(r.Time).PadLeft(10) + "  " + r.Name, r.Cpu >= 90 ? ShRed : r.Cpu >= 25 ? ShAmber : ShFg));
            f.Add(one("%CPU: 100 = one core · every " + secs.ToString("0.#", CultureInfo.InvariantCulture) + " s · Ctrl+C to leave (reads only - nothing is stopped from here)", ShDim));
            return f;
        }
        int ShTop(List<string> a, ShCtx c)
        {
            int iters = 0, ni = a.IndexOf("-n"); if (ni >= 0 && ni + 1 < a.Count) int.TryParse(a[ni + 1], out iters);
            double delay = 2; int di = a.IndexOf("-d"); if (di >= 0 && di + 1 < a.Count) double.TryParse(a[di + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out delay); delay = Math.Max(0.5, Math.Min(60, delay));
            double machine;
            if (!c.Out.Screen || c.Job.Bg || a.Contains("-b")) { var once = PsWindow((int)(delay * 1000), c, out machine); foreach (var row in TopFrame(once, machine, 30, delay)) { foreach (var sg in row) O(c, sg.T, sg.C); O(c, "\n", ShFg); } return 0; }   // #86: the footer says the window
            for (int i = 0; !c.Job.Cancel && (iters <= 0 || i < iters); i++) { var l = PsWindow((int)(delay * 1000), c, out machine); if (c.Job.Cancel) break; var fr = TopFrame(l, machine, 25, delay); Ui(delegate { term.Live(fr); }); }
            Ui(delegate { term.EndLive(); }); return 0;
        }
        // #87: every N seconds from one start to the next (not after the command plus N); -n 5, -n5 and --interval=5
        int ShWatch(List<string> a, ShCtx c)
        {
            double every = 2; int k = 0;
            while (k < a.Count && a[k].StartsWith("-", StringComparison.Ordinal))
            {
                string v; if ((a[k] == "-n" || a[k] == "--interval") && k + 1 < a.Count) { v = a[k + 1]; k += 2; } else if (a[k].StartsWith("-n", StringComparison.Ordinal) && a[k].Length > 2) { v = a[k].Substring(2); k++; } else if (a[k].StartsWith("--interval=", StringComparison.Ordinal)) { v = a[k].Substring(11); k++; } else { Err(c, "watch: " + a[k] + ": this watch knows -n SECONDS"); return 2; }
                if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out every)) { Err(c, "watch: " + v + ": not a number of seconds"); return 2; }
            }
            string line = string.Join(" ", a.Skip(k).Select(z => z.Contains(" ") ? ShQuote(z) : z)); every = Math.Max(0.5, Math.Min(3600, every));
            if (line == "") { Err(c, "usage: watch [-n secs] command"); return 2; }
            if (!c.Out.Screen || c.Job.Bg) { Err(c, "watch: needs the screen - not in a pipe or the background"); return 1; }
            var env = Env.Clone();
            while (!c.Job.Cancel)
            {
                var start = DateTime.UtcNow; var cap = new ShOut { Cap = new StringBuilder(), Job = c.Job }; cap.ErrCap = cap.Cap;
                var saved = tEnv; tEnv = env.Clone(); try { ShRunText(line, new ShCtx { Job = c.Job, Out = cap, Args = c.Args, Yes = c.Yes, Script = true }); } finally { tEnv = saved; }
                var fr = new List<List<TermView.Seg>> { new List<TermView.Seg> { new TermView.Seg("Every " + every.ToString("0.#", CultureInfo.InvariantCulture) + "s: " + line, ShCyan), new TermView.Seg("    " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " · Ctrl+C to leave", ShDim) }, new List<TermView.Seg>() };
                foreach (var l in SplitLines(cap.Cap.ToString()).Take(60)) fr.Add(new List<TermView.Seg> { new TermView.Seg(l, ShFg) });
                Ui(delegate { term.Live(fr); });
                var until = start.AddSeconds(every); while (!c.Job.Cancel && DateTime.UtcNow < until) Thread.Sleep(50);
            }
            Ui(delegate { term.EndLive(); }); return 0;
        }

        // ---------- network: addresses, routes, sockets
        int Ip(List<string> a, ShCtx c)
        {
            bool stats = a.Contains("-s"); string sub = a.FirstOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal)) ?? "addr";
            switch (sub) { case "a": case "addr": case "address": return IpAddr(c, null, stats); case "l": case "link": return IpAddr(c, "link", stats); case "r": case "route": case "ro": return IpRoute(c); case "n": case "neigh": case "neighbour": case "neighbor": return ToolAs("arp", new List<string>(), c, null); }
            Err(c, "ip: try  ip a · ip -s link · ip route · ip neigh"); return 2;
        }
        // #80: the MTU from IPv6 when there's no IPv4, link/none for an interface with no hardware address, counters only with -s
        int IpAddr(ShCtx c, string mode, bool stats)
        {
            int n = 0;
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties ip; try { ip = ni.GetIPProperties(); } catch (NetworkInformationException) { continue; }
                int idx = -1, mtu = 0;
                try { var v4 = ip.GetIPv4Properties(); if (v4 != null) { idx = v4.Index; mtu = v4.Mtu; } } catch (NetworkInformationException) { }
                if (mtu <= 0) try { var v6 = ip.GetIPv6Properties(); if (v6 != null) { if (idx < 0) idx = v6.Index; mtu = v6.Mtu; } } catch (NetworkInformationException) { }
                bool up = ni.OperationalStatus == OperationalStatus.Up; var hw = ni.GetPhysicalAddress().GetAddressBytes(); string mac = string.Join(":", hw.Select(b => b.ToString("x2")));
                O(c, (++n) + ": ", ShFg); O(c, ni.Name, ShCyan); OL(c, ": <" + (up ? "UP,LOWER_UP" : "DOWN") + ">" + (mtu > 0 ? " mtu " + mtu : "") + " state " + (up ? "UP" : "DOWN") + "  (" + ni.NetworkInterfaceType + (idx >= 0 ? ", if " + idx : "") + ")", up ? ShFg : ShDim);
                OL(c, "    " + (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ? "link/loopback 00:00:00:00:00:00" : hw.Length == 0 ? "link/none" : "link/ether " + mac) + "  " + ni.Description, ShDim);
                if (mode != "link")
                    foreach (var ua in ip.UnicastAddresses)
                    {
                        bool v6 = ua.Address.AddressFamily == AddressFamily.InterNetworkV6; int pl = 0;
                        try { pl = ua.PrefixLength; } catch (NotImplementedException) { if (ua.IPv4Mask != null) pl = ua.IPv4Mask.GetAddressBytes().Sum(b => Convert.ToString(b, 2).Count(ch => ch == '1')); }
                        O(c, "    " + (v6 ? "inet6 " : "inet "), ShFg); OL(c, ua.Address.ToString().Split('%')[0] + "/" + pl + (ua.Address.IsIPv6LinkLocal ? " scope link" : IPAddress.IsLoopback(ua.Address) ? " scope host" : " scope global"), v6 ? ShFg : ShGreen);
                    }
                if (stats) { try { var st = ni.GetIPStatistics(); OL(c, "    RX: " + st.BytesReceived + " bytes  " + st.UnicastPacketsReceived + " packets  " + st.IncomingPacketsDiscarded + " dropped  " + st.IncomingPacketsWithErrors + " errors", ShDim); OL(c, "    TX: " + st.BytesSent + " bytes  " + st.UnicastPacketsSent + " packets  " + st.OutgoingPacketsDiscarded + " dropped  " + st.OutgoingPacketsWithErrors + " errors", ShDim); } catch (Exception e) { if (!(e is NetworkInformationException || e is PlatformNotSupportedException)) throw; } }
            }
            return 0;
        }
        [StructLayout(LayoutKind.Sequential)] struct ShFwdRow { public uint Dest, Mask, Policy, NextHop, IfIndex, Type, Proto, Age, NextHopAs, M1, M2, M3, M4, M5; }
        [DllImport("iphlpapi.dll")] static extern int GetIpForwardTable(IntPtr table, ref int size, bool sort);
        int IpRoute(ShCtx c)
        {
            var names = new Dictionary<uint, string>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) try { var v4 = ni.GetIPProperties().GetIPv4Properties(); if (v4 != null) names[(uint)v4.Index] = ni.Name; } catch (NetworkInformationException) { }
            int size = 0; GetIpForwardTable(IntPtr.Zero, ref size, true); if (size <= 0) { Err(c, "ip route: Windows didn't hand over the routing table"); return 1; }
            IntPtr b = Marshal.AllocHGlobal(size);
            try
            {
                if (GetIpForwardTable(b, ref size, true) != 0) { Err(c, "ip route: Windows didn't hand over the routing table"); return 1; }
                int n = Marshal.ReadInt32(b), rs = Marshal.SizeOf(typeof(ShFwdRow));
                var rows = new List<ShFwdRow>(); for (int i = 0; i < n; i++) rows.Add((ShFwdRow)Marshal.PtrToStructure(b + 4 + i * rs, typeof(ShFwdRow)));
                foreach (var r in rows.OrderBy(r => r.M1).ThenBy(r => (uint)IPAddress.NetworkToHostOrder((int)r.Dest)))
                {
                    int pl = Convert.ToString(r.Mask, 2).Count(ch => ch == '1'); string dest = r.Dest == 0 && r.Mask == 0 ? "default" : new IPAddress(r.Dest) + "/" + pl;
                    if (pl == 32 && (new IPAddress(r.Dest).ToString().EndsWith(".255", StringComparison.Ordinal))) continue;   // the broadcast host routes Windows adds - noise
                    string dev; names.TryGetValue(r.IfIndex, out dev);
                    OL(c, dest + (r.NextHop != 0 && r.NextHop != r.Dest ? " via " + new IPAddress(r.NextHop) : "") + " dev " + (dev ?? "if" + r.IfIndex) + " metric " + r.M1 + (r.Type == 3 ? " scope link" : ""), dest == "default" ? ShGreen : ShFg);
                }
            }
            finally { Marshal.FreeHGlobal(b); }
            return 0;
        }
        // #43: ss spells states the way ss does (ESTAB, TIME-WAIT...); netstat keeps netstat's (ESTABLISHED, TIME_WAIT)
        static string SsState(string s)
        {
            switch (s) { case "ESTABLISHED": return "ESTAB"; case "TIME_WAIT": return "TIME-WAIT"; case "FIN_WAIT1": return "FIN-WAIT-1"; case "FIN_WAIT2": return "FIN-WAIT-2"; case "CLOSE_WAIT": return "CLOSE-WAIT"; case "LAST_ACK": return "LAST-ACK"; case "SYN_SENT": return "SYN-SENT"; case "SYN_RCVD": return "SYN-RECV"; case "BOUND": case "CLOSED": return "UNCONN"; case "DELETE_TCB": return "CLOSED"; }
            return s;
        }
        // #5 #15 #44 #81 #82: UDP with the address it's really bound to, this PC talking to itself included, -p honoured, -a and
        // -u include UDP; netstat says tcp6/udp6
        int Ss(List<string> a, ShCtx c, bool netstat)
        {
            string fl = Flags(a);
            bool listen = fl.Contains('l'), all = fl.Contains('a'), udpF = fl.Contains('u'), tcpF = fl.Contains('t'), proc = fl.Contains('p');
            bool tcp = tcpF || !udpF, udp = udpF || (!tcpF && (all || listen));
            var names = new Dictionary<int, string>(); foreach (var p in Process.GetProcesses()) using (p) try { names[p.Id] = p.ProcessName; } catch (InvalidOperationException) { }
            var head = netstat ? new List<string> { "Proto", "Local Address", "Foreign Address", "State" } : new List<string> { "Netid", "State", "Local Address:Port", "Peer Address:Port" };
            if (proc) head.Add(netstat ? "PID/Program name" : "Process");
            var rows = new List<string[]> { head.ToArray() };
            Func<string, int, string> pr = (nm, pid) => netstat ? pid + "/" + nm : "users:((\"" + nm + "\",pid=" + pid + "))";
            if (tcp)
            {
                List<Conn> l; try { l = Tables.Tcp(true, names, true); } catch (InvalidOperationException e) { Err(c, (netstat ? "netstat: " : "ss: ") + e.Message); return 1; }
                foreach (var x in l.Where(x => listen ? x.State == "LISTEN" : all || (x.State != "LISTEN" && x.State != "BOUND")).OrderBy(x => x.State).ThenBy(x => x.LocalPort))
                {
                    bool v6 = x.Proto == "TCP6"; string st = netstat ? x.State : SsState(x.State), peer = x.State == "LISTEN" || x.State == "BOUND" ? (v6 ? "[::]:*" : "0.0.0.0:*") : x.Remote;
                    var row = netstat ? new List<string> { v6 ? "tcp6" : "tcp", x.Local, peer, st } : new List<string> { "tcp", st, x.Local, peer };
                    if (proc) row.Add(pr(x.Proc ?? "", x.Pid)); rows.Add(row.ToArray());
                }
            }
            if (udp)
                foreach (var u in Tables.UdpFull().OrderBy(u => u.Port))
                {
                    string nm; names.TryGetValue(u.Pid, out nm); string local = u.V6 ? "[" + u.Addr + "]:" + u.Port : u.Addr + ":" + u.Port, peer = u.V6 ? "[::]:*" : "0.0.0.0:*";
                    var row = netstat ? new List<string> { u.V6 ? "udp6" : "udp", local, peer, "" } : new List<string> { "udp", "UNCONN", local, peer };
                    if (proc) row.Add(pr(nm ?? "", u.Pid)); rows.Add(row.ToArray());
                }
            Table(c, rows, ShBlue);
            // SH-S28: GNU's bare ss lists only connected UDP sockets (ESTAB) - Windows doesn't say which UDP sockets are connected,
            // so none are guessed; where to find them is said instead
            if (!udp && !tcpF && !netstat) OL(c, "(UDP sockets: ss -ua - Windows doesn't say which ones are connected, so bare ss leaves them out)", ShDim);
            return 0;
        }

        // ---------- /proc: this PC as files (cat /proc/meminfo) - #4 #36 #37 #38: every number measured
        static readonly string[] ProcFiles = { "cpuinfo", "meminfo", "uptime", "version", "mounts", "loadavg" };
        string ProcFile(string name)
        {
            var sb = new StringBuilder(); var ms = new Native.MEMORYSTATUSEX(); Native.GlobalMemoryStatusEx(ms);
            switch (name)
            {
                case "meminfo":
                    {
                        long fp = FreePages(); ulong avail = ms.ullAvailPhys, free = fp >= 0 ? Math.Min(avail, (ulong)fp) : avail;
                        ulong swapT = ms.ullTotalPageFile > ms.ullTotalPhys ? ms.ullTotalPageFile - ms.ullTotalPhys : 0, swapF = ms.ullAvailPageFile > ms.ullAvailPhys ? Math.Min(swapT, ms.ullAvailPageFile - ms.ullAvailPhys) : 0;
                        Action<string, ulong> kb = (k, v) => sb.AppendLine((k + ":").PadRight(16) + (v / 1024).ToString(CultureInfo.InvariantCulture).PadLeft(10) + " kB");
                        kb("MemTotal", ms.ullTotalPhys); kb("MemFree", free); kb("MemAvailable", avail); kb("Cached", avail - free); kb("SwapTotal", swapT); kb("SwapFree", swapF); kb("CommitLimit", ms.ullTotalPageFile); kb("Committed_AS", ms.ullTotalPageFile - ms.ullAvailPageFile);
                        break;
                    }
                case "uptime":
                    {
                        long idle, k0, u0; double idleSecs = Native.GetSystemTimes(out idle, out k0, out u0) ? idle / 1e7 : 0;   // idle time summed over every CPU, as Linux counts it
                        sb.AppendLine((DateTime.Now - BootTime()).TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " " + idleSecs.ToString("0.00", CultureInfo.InvariantCulture)); break;
                    }
                case "version": sb.AppendLine("Windows " + Environment.OSVersion.Version + " (" + Environment.OSVersion + ") · AdeeboShell " + Program.Version); break;
                case "loadavg":
                    {
                        int count = 0, newest = 0; DateTime newestAt = DateTime.MinValue;
                        foreach (var p in Process.GetProcesses()) using (p) { count++; try { var s0 = p.StartTime; if (s0 > newestAt) { newestAt = s0; newest = p.Id; } } catch (Exception e) { if (!ProcErr(e)) throw; } }
                        int runnable = 0; try { using (var pc = new PerformanceCounter("System", "Processor Queue Length", true)) runnable = (int)pc.NextValue(); } catch (Exception e) { if (!(e is InvalidOperationException || e is UnauthorizedAccessException || e is System.ComponentModel.Win32Exception)) throw; }
                        sb.AppendLine(LoadText(" ") + " " + runnable + "/" + count + " " + newest); break;
                    }
                case "mounts":
                    foreach (var d in DriveInfo.GetDrives()) { string fs = "-"; try { if (d.IsReady) fs = d.DriveFormat.ToLowerInvariant(); } catch (IOException) { } sb.AppendLine(d.Name.TrimEnd('\\') + " " + MntOf(d) + " " + fs + " " + (d.DriveType == DriveType.CDRom || ReadOnlyVol(d.Name) ? "ro" : "rw") + " 0 0"); }
                    break;
                case "cpuinfo":
                    {
                        string model = ""; try { using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")) if (k != null) model = Convert.ToString(k.GetValue("ProcessorNameString"), CultureInfo.InvariantCulture).Trim(); } catch (Exception e) { if (!(e is System.Security.SecurityException || e is UnauthorizedAccessException || e is IOException)) throw; }
                        for (int i = 0; i < Environment.ProcessorCount; i++) sb.AppendLine("processor\t: " + i).AppendLine("model name\t: " + model).AppendLine();
                        break;
                    }
                default: return null;
            }
            return sb.ToString();
        }

        // ---------- text tools
        // #60: fields come out in the line's own order, each once
        int Cut(List<string> a, ShCtx c)
        {
            string d = "\t", f = null, ch = null; bool onlyDelim = false; var files = new List<string>();
            for (int i = 0; i < a.Count; i++)
            {
                string w = a[i];
                if (w == "-d" && i + 1 < a.Count) { d = a[++i]; continue; } if (w.StartsWith("-d", StringComparison.Ordinal)) { d = w.Substring(2); continue; }
                if (w == "-f" && i + 1 < a.Count) { f = a[++i]; continue; } if (w.StartsWith("-f", StringComparison.Ordinal)) { f = w.Substring(2); continue; }
                if ((w == "-c" || w == "-b") && i + 1 < a.Count) { ch = a[++i]; continue; } if (w.StartsWith("-c", StringComparison.Ordinal) || w.StartsWith("-b", StringComparison.Ordinal)) { ch = w.Substring(2); continue; }
                if (w == "-s") { onlyDelim = true; continue; }
                files.Add(w);
            }
            if (f == null && ch == null) { Err(c, "usage: cut -d ',' -f 1,3   or   cut -c 1-10"); return 2; }
            if (d.Length != 1) { Err(c, "cut: the delimiter must be one character"); return 2; }
            var l = InLines(files, c); if (l == null) return 1;
            Func<string, int, List<int>> pick = (spec, max) => { var r = new SortedSet<int>(); foreach (var part in spec.Split(',')) { var m = Regex.Match(part, @"^(\d*)-(\d*)$"); int x, y; if (m.Success) { x = m.Groups[1].Value == "" ? 1 : int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); y = m.Groups[2].Value == "" ? max : int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); for (int k = x; k <= Math.Min(y, max); k++) r.Add(k); } else if (int.TryParse(part, out x)) r.Add(x); } return r.ToList(); };
            foreach (var x in l)
            {
                if (ch != null) { OL(c, string.Concat(pick(ch, x.Length).Where(k => k >= 1 && k <= x.Length).Select(k => x[k - 1]))); continue; }
                var parts = x.Split(d[0]); if (parts.Length == 1) { if (!onlyDelim) OL(c, x); continue; }
                OL(c, string.Join(d, pick(f, parts.Length).Where(k => k >= 1 && k <= parts.Length).Select(k => parts[k - 1])));
            }
            return 0;
        }
        static string TrSet(string s)
        {
            s = s.Replace("[:upper:]", "A-Z").Replace("[:lower:]", "a-z").Replace("[:digit:]", "0-9").Replace("[:alpha:]", "A-Za-z").Replace("[:alnum:]", "A-Za-z0-9").Replace("[:space:]", " \t\n\r\f\v").Replace("[:punct:]", "!-/:-@[-`{-~");
            s = s.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\\\", "\\");
            var sb = new StringBuilder(); for (int i = 0; i < s.Length; i++) { if (i + 2 < s.Length && s[i + 1] == '-' && s[i] <= s[i + 2]) { for (char x = s[i]; x <= s[i + 2]; x++) sb.Append(x); i += 2; } else sb.Append(s[i]); }
            return sb.ToString();
        }
        // #30: -c (complement), -d, -s and their mixes (-cd, -ds); a third word is refused - tr reads only from a pipe
        int Tr(List<string> a, ShCtx c)
        {
            string fl = string.Concat(a.Where(z => Regex.IsMatch(z, "^-[cCds]+$")).Select(z => z.Substring(1))); bool comp = fl.IndexOf('c') >= 0 || fl.IndexOf('C') >= 0, del = fl.Contains('d'), sq = fl.Contains('s');
            var sets = a.Where(z => !Regex.IsMatch(z, "^-[cCds]+$")).ToList();
            int want = del && !sq ? 1 : sq && !del && sets.Count == 1 ? 1 : 2;
            if (sets.Count == 0 || sets.Count > 2 || (sets.Count > want && del && !sq)) { Err(c, sets.Count > want ? "tr: tr reads from a pipe, not a file - cat " + sets[sets.Count - 1] + " | tr ..." : "usage: tr 'a-z' 'A-Z'  ·  tr -d '\\r'  ·  tr -s ' '  ·  tr -cd '0-9'"); return 2; }
            string s1 = TrSet(sets[0]), s2 = sets.Count > 1 ? TrSet(sets[1]) : null;
            Func<char, bool> inS1 = x => (s1.IndexOf(x) >= 0) != comp;
            string t = c.Stdin ?? ""; var sb = new StringBuilder(); char last = '\0'; bool lastSq = false;
            foreach (char x in t)
            {
                char o = x; bool member = inS1(x);
                if (del && member) continue;
                if (!del && member && s2 != null && s2.Length > 0) o = comp ? s2[s2.Length - 1] : s2[Math.Min(s1.IndexOf(x), s2.Length - 1)];
                bool sqMember = sq && (del ? (s2 != null && s2.IndexOf(o) >= 0) : s2 != null ? s2.IndexOf(o) >= 0 : inS1(o));
                if (sqMember && lastSq && o == last) continue;
                sb.Append(o); last = o; lastSq = sqMember;
            }
            O(c, sb.ToString(), ShFg); return 0;
        }
        // #27 #66: each expression compiled once; basic regular expressions unless -E; addresses timed out like the rest; -i edits
        // the file itself (its encoding and line endings kept, written whole, then swapped in)
        int Sed(List<string> a, ShCtx c)
        {
            var progs = new List<string>(); var files = new List<string>(); bool quiet = false, ere = false, inPlace = false;
            for (int i = 0; i < a.Count; i++) { string w = a[i]; if (w == "-e" && i + 1 < a.Count) { progs.Add(a[++i]); continue; } if (Regex.IsMatch(w, "^-[nEri]+$")) { quiet |= w.Contains("n"); ere |= w.Contains("E") || w.Contains("r"); inPlace |= w.Contains("i"); continue; } if (progs.Count == 0) progs.Add(w); else files.Add(w); }
            if (progs.Count == 0) { Err(c, "usage: sed 's/old/new/g' [file]   ·   sed -n '2,5p'   ·   sed '/re/d'   ·   sed -i 's/a/b/' file"); return 2; }
            var steps = new List<Func<string, int, int, string>>();   // (line, number, total) -> the line, null = deleted, "\u0000p" in front = print it too
            foreach (var pr in progs.SelectMany(z => z.Split('\n')).Select(z => z.Trim()).Where(z => z != ""))
            {
                var sm = Regex.Match(pr, @"^s(.)(.*?)(?<!\\)\1(.*?)(?<!\\)\1([gipI0-9]*)$");
                if (sm.Success)
                {
                    string sep = sm.Groups[1].Value, flags = sm.Groups[4].Value, pat = sm.Groups[2].Value.Replace("\\" + sep, sep);
                    Regex rx; try { rx = new Regex(ere ? pat : BreToNet(pat), flags.IndexOf('i') >= 0 || flags.IndexOf('I') >= 0 ? RegexOptions.IgnoreCase : RegexOptions.None, TimeSpan.FromSeconds(2)); } catch (ArgumentException) { Err(c, "sed: " + pat + ": not a pattern"); return 2; }
                    string rep = Regex.Replace(sm.Groups[3].Value.Replace("\\" + sep, sep).Replace("$", "$$"), @"\\(\d)", "$${$1}").Replace("\\&", "\u0002").Replace("&", "$0").Replace("\u0002", "&").Replace("\\n", "\n").Replace("\\t", "\t");
                    bool g = flags.Contains("g"), p = flags.Contains("p"); int nth; int.TryParse(Regex.Match(flags, @"\d+").Value, out nth);
                    steps.Add((x, n, tot) => { string y; if (g) y = rx.Replace(x, rep); else if (nth > 1) { int k = 0; y = rx.Replace(x, m => ++k == nth ? m.Result(rep) : m.Value); } else y = rx.Replace(x, rep, 1); return p && y != x ? "\u0000p" + y : y; });
                    continue;
                }
                var dm = Regex.Match(pr, @"^/(.*)/(I?)([dp])$");
                if (dm.Success) { Regex rx; try { rx = new Regex(ere ? dm.Groups[1].Value : BreToNet(dm.Groups[1].Value), dm.Groups[2].Value == "I" ? RegexOptions.IgnoreCase : RegexOptions.None, TimeSpan.FromSeconds(2)); } catch (ArgumentException) { Err(c, "sed: " + dm.Groups[1].Value + ": not a pattern"); return 2; } bool del = dm.Groups[3].Value == "d"; steps.Add((x, n, tot) => rx.IsMatch(x) ? (del ? null : "\u0000p" + x) : x); continue; }
                var nm = Regex.Match(pr, @"^(\d+|\$)(?:,(\d+|\$))?([dp])$");
                if (nm.Success) { string f0 = nm.Groups[1].Value, t0 = nm.Groups[2].Success ? nm.Groups[2].Value : f0; bool del = nm.Groups[3].Value == "d"; steps.Add((x, n, tot) => { int from = f0 == "$" ? tot : int.Parse(f0, CultureInfo.InvariantCulture), to = t0 == "$" ? tot : int.Parse(t0, CultureInfo.InvariantCulture); return n >= from && n <= to ? (del ? null : "\u0000p" + x) : x; }); continue; }
                Err(c, "sed: this sed knows s/old/new/[g i p N], /re/d, /re/p, N,Md and N,Mp - not '" + pr + "'"); return 2;
            }
            if (inPlace && files.Count == 0) { Err(c, "sed -i: which file?"); return 2; }
            foreach (var target in inPlace ? files : new List<string> { null })
            {
                // RG-1: sed -i reads the file the way it will write it back (its own encoding, BOM, CRLF) - an ANSI é stays é
                TextShape shape = null; List<string> l;
                if (target != null)
                {
                    string rp = Real(Abs(target)); if (rp == null || !File.Exists(rp)) { Err(c, "sed -i: " + target + ": not a file on a disk"); return 1; }
                    shape = new TextShape(); try { l = SplitLines(ReadShaped(rp, shape)); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, "sed -i: " + target + ": " + e.Message); return 1; }
                }
                else { l = InLines(files, c); if (l == null) return 1; }
                var outp = new StringBuilder();
                try
                {
                    for (int n = 0; n < l.Count; n++)
                    {
                        string x = l[n]; bool print = false;
                        foreach (var st in steps) { x = st(x, n + 1, l.Count); if (x == null) break; if (x.StartsWith("\u0000p", StringComparison.Ordinal)) { print = true; x = x.Substring(2); } }
                        if (x == null) continue;
                        if (!quiet) outp.Append(x).Append('\n'); if (print) outp.Append(x).Append('\n');
                        if (outp.Length > ShMaxCap) throw new ShTooBig();
                    }
                }
                catch (RegexMatchTimeoutException) { Err(c, "sed: the pattern took too long - stopped"); return 2; }
                if (target == null) { O(c, outp.ToString(), ShFg); continue; }
                string r = Real(Abs(target)); try { WriteShaped(r, outp.ToString(), shape); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, "sed -i: " + target + ": " + e.Message); return 1; }
            }
            return 0;
        }
        int Xargs(List<string> a, ShCtx c)
        {
            int per = 0, ni = a.IndexOf("-n"); if (ni >= 0 && ni + 1 < a.Count) { if (!int.TryParse(a[ni + 1], out per)) { Err(c, "xargs: -n needs a number"); return 2; } a.RemoveRange(ni, 2); }
            var items = Regex.Matches(c.Stdin ?? "", @"\S+").Cast<Match>().Select(m => m.Value).ToList(); if (items.Count == 0) return 0;
            var cmd = a.Count == 0 ? new List<string> { "echo" } : a; int worst = 0;
            foreach (var chunk in per > 0 ? items.Select((v, i) => new { v, i }).GroupBy(z => z.i / per).Select(g => g.Select(z => z.v).ToList()) : new[] { items })
            {
                if (c.Job.Cancel) return 130;
                worst = Math.Max(worst, ShRunText(string.Join(" ", cmd.Concat(chunk).Select(ShQuote)), new ShCtx { Job = c.Job, Out = c.Out, Args = c.Args, Yes = c.Yes, Script = c.Script }));
            }
            return worst;
        }
        // #70: wrapped at 76 like GNU (-w 0: one line)
        int B64(List<string> a, ShCtx c)
        {
            bool dec = a.Contains("-d") || a.Contains("--decode"); int wrap = 76; int wi = a.IndexOf("-w"); if (wi >= 0 && wi + 1 < a.Count) int.TryParse(a[wi + 1], out wrap);
            var f = a.Where((z, i) => !z.StartsWith("-", StringComparison.Ordinal) && !(i > 0 && a[i - 1] == "-w")).FirstOrDefault();
            byte[] data;
            if (f != null && f != "-") { string r = Real(Abs(f)); if (r == null || !File.Exists(r)) { Err(c, "base64: " + f + ": no such file"); return 1; } if (new FileInfo(r).Length > 20 * 1048576) { Err(c, "base64: over 20 MB"); return 1; } data = File.ReadAllBytes(r); }
            else data = Encoding.UTF8.GetBytes(c.Stdin ?? "");
            if (!dec) { string s = Convert.ToBase64String(data); if (wrap > 0) { var sb = new StringBuilder(); for (int i = 0; i < s.Length; i += wrap) sb.Append(s, i, Math.Min(wrap, s.Length - i)).Append('\n'); O(c, sb.ToString(), ShFg); } else OL(c, s); return 0; }
            try { O(c, Encoding.UTF8.GetString(Convert.FromBase64String(Regex.Replace(Encoding.UTF8.GetString(data), @"\s", ""))), ShFg); return 0; }
            catch (FormatException) { Err(c, "base64: that isn't base64"); return 1; }
        }
        // #11: -c really checks - every listed file is hashed and compared, OK / FAILED each, exit 1 if any failed
        int HashSum(string cmd, List<string> a, ShCtx c)
        {
            Func<System.Security.Cryptography.HashAlgorithm> mk = () => cmd == "md5sum" ? (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.MD5.Create() : cmd == "sha1sum" ? (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.SHA1.Create() : cmd == "sha512sum" ? (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.SHA512.Create() : System.Security.Cryptography.SHA256.Create();
            Func<string, string> ofFile = r => { using (var h = mk()) using (var fs = new FileStream(r, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) return BitConverter.ToString(h.ComputeHash(fs)).Replace("-", "").ToLowerInvariant(); };
            var files = Files(a); bool check = Flags(a).Contains('c') || a.Contains("--check"), quiet = a.Contains("--quiet");
            if (check)
            {
                string sums = files.Count == 0 ? c.Stdin ?? "" : ReadAll(files[0], c); if (sums == null) return 1;
                int bad = 0, missing = 0, ok = 0; string baseDir = files.Count == 0 || files[0] == "-" ? "" : (files[0].Contains("/") ? files[0].Substring(0, files[0].LastIndexOf('/') + 1) : "");
                foreach (var line in SplitLines(sums).Where(z => z.Trim() != ""))
                {
                    var m = Regex.Match(line.Trim(), @"^([0-9a-fA-F]{32,128})\s+\*?(.+)$"); if (!m.Success) { Err(c, cmd + ": a line that isn't 'hash  name': " + line); bad++; continue; }
                    string name = m.Groups[2].Value.Trim(), r = Real(Abs(name.StartsWith("/", StringComparison.Ordinal) ? name : baseDir + name));
                    if (r == null || !File.Exists(r)) { OL(c, name + ": FAILED open or read", ShRed); missing++; continue; }
                    bool same = string.Equals(ofFile(r), m.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
                    if (same) { ok++; if (!quiet) OL(c, name + ": OK", ShGreen); } else { bad++; OL(c, name + ": FAILED", ShRed); }
                }
                if (bad > 0) Err(c, cmd + ": WARNING: " + bad + " computed checksum" + (bad == 1 ? " did" : "s did") + " NOT match");
                if (missing > 0) Err(c, cmd + ": WARNING: " + missing + " listed file" + (missing == 1 ? "" : "s") + " could not be read");
                if (ok + bad + missing == 0) { Err(c, cmd + ": no checksum lines found"); return 1; }
                return bad + missing > 0 ? 1 : 0;
            }
            if (files.Count == 0 || (files.Count == 1 && files[0] == "-")) { using (var h = mk()) OL(c, BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(c.Stdin ?? ""))).Replace("-", "").ToLowerInvariant() + "  -"); return 0; }
            int ex = 0;
            foreach (var f in files) { string r = Real(Abs(f)); if (r == null || !File.Exists(r)) { Err(c, cmd + ": " + f + ": no such file"); ex = 1; continue; } OL(c, ofFile(r) + "  " + f); }
            return ex;
        }
    }
}
