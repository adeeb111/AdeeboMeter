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
                case "ifconfig": case "ipconfig": return IpAddr(c, null);
                case "route": { var dest = a.FirstOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal) && z != "print"); if (dest != null) return ToolAs("route", new List<string> { dest }, c, null); return IpRoute(c); }   // route 8.8.8.8: the Toolbox's "which route does this take?"
                case "ss": case "netstat": return Ss(a, c);
                case "lsblk": return Lsblk(c);
                case "lscpu": return Lscpu(c);
                case "nproc": OL(c, Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)); return 0;
                case "id": OL(c, "uid=" + ShUser + " (" + Environment.UserDomainName + "\\" + Environment.UserName + ")"); return 0;
                case "who": case "w": OL(c, ShUser.PadRight(12) + "console      " + BootTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)); if (cmd == "w") OL(c, UptimeLine(), ShDim); return 0;
                case "watch": return ShWatch(a, c);
                case "cut": return Cut(a, c);
                case "tr": return Tr(a, c);
                case "sed": return Sed(a, c);
                case "awk": return Awk(a, c);
                case "xargs": return Xargs(a, c);
                case "less": case "more": return Builtin("cat", a, c);
                case "rev": { var l = InLines(a, c); if (l == null) return 1; foreach (var x in l) OL(c, new string(x.Reverse().ToArray())); return 0; }
                case "nl": { var l = InLines(a, c); if (l == null) return 1; int n = 0; foreach (var x in l) OL(c, (x.Trim() == "" ? "" : (++n).ToString(CultureInfo.InvariantCulture)).PadLeft(6) + "  " + x); return 0; }
                case "shuf": { var l = InLines(a, c); if (l == null) return 1; var rnd = new Random(); foreach (var x in l.OrderBy(z => rnd.Next())) OL(c, x); return 0; }
                case "yes": { string w = a.Count > 0 ? string.Join(" ", a) : "y"; for (int i = 0; i < 1000 && !c.Job.Cancel; i++) OL(c, w); return 0; }
                case "base64": return B64(a, c);
                case "md5sum": case "sha1sum": case "sha256sum": return HashSum(cmd, a, c);
                // the Linux names for AdeeboMeter's own tools
                case "traceroute": case "tracert": case "tracepath": return ToolAs("mtr", new List<string> { a.LastOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal)) ?? "", "1" }, c, "traceroute here is MTR for a minute: every hop, its loss and its delay");
                case "nslookup": case "host": { var w = a.Where(z => !z.StartsWith("-", StringComparison.Ordinal)).ToList(); return ToolAs("dig", w.Take(2).ToList(), c, null); }
                case "curl": case "wget": { var u = a.LastOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal)); return ToolAs("http", new List<string> { u ?? "" }, c, "curl here looks at the answer - status, headers, TLS, timing - and downloads nothing"); }
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

        // ---------- disks
        int Df(List<string> a, ShCtx c)
        {
            bool kb = a.Contains("-k"), typ = a.Contains("-T");
            var rows = new List<string[]> { typ ? new[] { "Filesystem", "Type", kb ? "1K-blocks" : "Size", "Used", "Avail", "Use%", "Mounted on" } : new[] { "Filesystem", kb ? "1K-blocks" : "Size", "Used", "Avail", "Use%", "Mounted on" } };
            foreach (var d in DriveInfo.GetDrives())
            {
                long tot = 0, free = 0; string fs = "-", label = "";
                try { if (!d.IsReady) continue; tot = d.TotalSize; free = d.AvailableFreeSpace; fs = d.DriveFormat; label = d.VolumeLabel; } catch (IOException) { continue; } catch (UnauthorizedAccessException) { continue; }
                long used = tot - d.TotalFreeSpace; Func<long, string> f = v => kb ? (v / 1024).ToString(CultureInfo.InvariantCulture) : Human(v);
                string name = d.Name.TrimEnd('\\') + (label != "" ? " " + label : "");
                string pct = tot > 0 ? Math.Ceiling(100.0 * used / tot).ToString("0", CultureInfo.InvariantCulture) + "%" : "-";
                rows.Add(typ ? new[] { name, fs.ToLowerInvariant(), f(tot), f(used), f(free), pct, MntOf(d) } : new[] { name, f(tot), f(used), f(free), pct, MntOf(d) });
            }
            Table(c, rows, ShBlue, false, typ, true, true, true, true);
            return 0;
        }
        int Lsblk(ShCtx c)
        {
            var rows = new List<string[]> { new[] { "NAME", "SIZE", "TYPE", "FSTYPE", "LABEL", "MOUNTPOINT" } };
            try
            {
                using (var q = new System.Management.ManagementObjectSearcher("SELECT Index, Model, Size, MediaType, InterfaceType FROM Win32_DiskDrive"))
                    foreach (System.Management.ManagementObject o in q.Get())
                        using (o) { ulong sz = 0; try { sz = Convert.ToUInt64(o["Size"] ?? 0UL, CultureInfo.InvariantCulture); } catch (FormatException) { } rows.Add(new[] { "disk" + o["Index"], Human(sz), "disk", Convert.ToString(o["InterfaceType"], CultureInfo.InvariantCulture), Convert.ToString(o["Model"], CultureInfo.InvariantCulture), "" }); }
            }
            catch (Exception e) { if (!(e is System.Management.ManagementException || e is UnauthorizedAccessException || e is COMException)) throw; }
            foreach (var d in DriveInfo.GetDrives())
            {
                string fs = "", lab = ""; long sz = 0; bool ready = false;
                try { ready = d.IsReady; if (ready) { fs = d.DriveFormat.ToLowerInvariant(); lab = d.VolumeLabel; sz = d.TotalSize; } } catch (IOException) { }
                rows.Add(new[] { "└─" + d.Name.Substring(0, 2).ToLowerInvariant(), ready ? Human(sz) : "-", d.DriveType == DriveType.Fixed ? "part" : d.DriveType.ToString().ToLowerInvariant(), fs, lab, ready ? MntOf(d) : "(not ready)" });
            }
            Table(c, rows, ShBlue, false, true);
            return 0;
        }
        int Du(List<string> a, ShCtx c)
        {
            bool sum = a.Any(z => z.StartsWith("-", StringComparison.Ordinal) && z.Contains("s")); int depth = 1;
            var md = a.FirstOrDefault(z => z.StartsWith("--max-depth=", StringComparison.Ordinal) || z.StartsWith("-d", StringComparison.Ordinal)); if (md != null) int.TryParse(md.Replace("--max-depth=", "").Replace("-d", ""), out depth);
            var paths = a.Where(z => !z.StartsWith("-", StringComparison.Ordinal)).DefaultIfEmpty(".").ToList(); int files = 0; var sw = Stopwatch.StartNew();
            Func<string, long> size = null;
            size = dir =>
            {
                long t = 0; if (c.Job.Cancel || files > 500000) return 0;
                try { foreach (var f in Directory.EnumerateFiles(dir)) { files++; try { t += new FileInfo(f).Length; } catch (IOException) { } } foreach (var d in Directory.EnumerateDirectories(dir)) { try { if ((new DirectoryInfo(d).Attributes & FileAttributes.ReparsePoint) != 0) continue; } catch (IOException) { continue; } t += size(d); } }
                catch (UnauthorizedAccessException) { } catch (IOException) { }
                if (c.Out.Screen && !c.Job.Bg && sw.ElapsedMilliseconds > 400) { sw.Restart(); int f0 = files; Ui(delegate { term.Live(new List<List<TermView.Seg>> { new List<TermView.Seg> { new TermView.Seg("du  ", ShCyan), new TermView.Seg(f0.ToString("N0", CultureInfo.CurrentCulture) + " files counted · Ctrl+C stops", ShDim) } }); }); }
                return t;
            };
            foreach (var p in paths)
            {
                string r = Real(Abs(p)); if (r == null || (!Directory.Exists(r) && !File.Exists(r))) { Err(c, "du: " + p + ": not a folder or file on a disk"); continue; }
                if (File.Exists(r)) { OL(c, Human(new FileInfo(r).Length).PadRight(8) + p); continue; }
                var kids = new List<KeyValuePair<string, long>>();
                if (!sum && depth >= 1) { try { foreach (var d in Directory.EnumerateDirectories(r)) { if (c.Job.Cancel) break; kids.Add(new KeyValuePair<string, long>(d, size(d))); } } catch (UnauthorizedAccessException) { } }
                long total = sum || depth < 1 ? size(r) : kids.Sum(k => k.Value) + FilesHere(r);
                if (c.Out.Screen && !c.Job.Bg) Ui(delegate { term.EndLive(); });
                foreach (var k in kids.OrderBy(k => k.Value)) OL(c, Human(k.Value).PadRight(8) + (p.TrimEnd('/') + "/" + Path.GetFileName(k.Key)), ShFg);
                OL(c, Human(total).PadRight(8) + p, ShCyan);
            }
            if (files > 500000) OL(c, "(stopped counting at 500,000 files)", ShDim);
            return c.Job.Cancel ? 130 : 0;
        }
        static long FilesHere(string dir) { long t = 0; try { foreach (var f in Directory.EnumerateFiles(dir)) try { t += new FileInfo(f).Length; } catch (IOException) { } } catch (UnauthorizedAccessException) { } catch (IOException) { } return t; }

        // ---------- memory, time, processor
        int Free(List<string> a, ShCtx c)
        {
            var ms = new Native.MEMORYSTATUSEX(); if (!Native.GlobalMemoryStatusEx(ms)) { Err(c, "free: Windows didn't say"); return 1; }
            Func<ulong, string> f = v => a.Contains("-b") ? v.ToString(CultureInfo.InvariantCulture) : a.Contains("-k") ? (v / 1024).ToString(CultureInfo.InvariantCulture) : a.Contains("-m") ? (v / 1048576).ToString(CultureInfo.InvariantCulture) : a.Contains("-g") ? (v / 1073741824).ToString(CultureInfo.InvariantCulture) : Human(v);
            ulong swapT = ms.ullTotalPageFile > ms.ullTotalPhys ? ms.ullTotalPageFile - ms.ullTotalPhys : 0, swapF = ms.ullAvailPageFile > ms.ullAvailPhys ? Math.Min(swapT, ms.ullAvailPageFile - ms.ullAvailPhys) : 0;
            Table(c, new List<string[]> {
                new[] { "", "total", "used", "free", "available" },
                new[] { "Mem:", f(ms.ullTotalPhys), f(ms.ullTotalPhys - ms.ullAvailPhys), f(ms.ullAvailPhys), f(ms.ullAvailPhys) },
                new[] { "Swap:", f(swapT), f(swapT - swapF), f(swapF), "" } }, ShBlue, false, true, true, true, true);
            if (c.Out.Screen) OL(c, "Swap is Windows' page file beyond the RAM. In use: " + ms.dwMemoryLoad + "% of memory.", ShDim);
            return 0;
        }
        static DateTime BootTime() { try { return DateTime.Now - TimeSpan.FromMilliseconds(Native.GetTickCount64()); } catch (EntryPointNotFoundException) { return DateTime.Now; } }
        string UptimeLine()
        {
            var up = DateTime.Now - BootTime(); double cpu = snap != null ? snap.Cpu : double.NaN;
            return " " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " up " + (up.Days > 0 ? up.Days + " day" + (up.Days == 1 ? "" : "s") + ", " : "") + up.Hours.ToString("00", CultureInfo.InvariantCulture) + ":" + up.Minutes.ToString("00", CultureInfo.InvariantCulture) + ",  1 user,  cpu " + (double.IsNaN(cpu) ? "?" : cpu.ToString("0", CultureInfo.InvariantCulture) + "%");
        }
        int Lscpu(ShCtx c)
        {
            string name = "", mhz = "";
            try { using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")) if (k != null) { name = Convert.ToString(k.GetValue("ProcessorNameString"), CultureInfo.InvariantCulture).Trim(); mhz = Convert.ToString(k.GetValue("~MHz"), CultureInfo.InvariantCulture); } }
            catch (Exception e) { if (!(e is System.Security.SecurityException || e is UnauthorizedAccessException || e is IOException)) throw; }
            Action<string, string> kv = (k, v) => { O(c, (k + ":").PadRight(22), ShBlue); OL(c, v); };
            kv("Architecture", Environment.Is64BitOperatingSystem ? (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "x86_64").Replace("AMD64", "x86_64") : "x86");
            kv("CPU(s)", Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture) + " (logical)");
            kv("Model name", name == "" ? "-" : name); if (mhz != "") kv("CPU MHz", mhz);
            kv("Identifier", Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "-");
            return 0;
        }

        // ---------- processes: what's running, what it costs (read only - nothing is stopped from here)
        sealed class PsRow { public int Pid, Threads; public string Name; public long Mem; public double Cpu = double.NaN; public TimeSpan Time; }
        List<PsRow> PsSample(int ms, ShCtx c)
        {
            var first = new Dictionary<int, TimeSpan>(); var procs = Process.GetProcesses();
            try
            {
                foreach (var p in procs) try { first[p.Id] = p.TotalProcessorTime; } catch (Exception e) { if (!(e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is NotSupportedException)) throw; }
                var sw = Stopwatch.StartNew(); while (sw.ElapsedMilliseconds < ms && !c.Job.Cancel) Thread.Sleep(50); double secs = Math.Max(0.05, sw.Elapsed.TotalSeconds) * Environment.ProcessorCount;
                var l = new List<PsRow>();
                foreach (var p in procs)
                {
                    var r = new PsRow { Pid = p.Id };
                    try { r.Name = p.ProcessName; r.Mem = p.WorkingSet64; r.Threads = p.Threads.Count; } catch (Exception e) { if (!(e is InvalidOperationException || e is System.ComponentModel.Win32Exception || e is NotSupportedException)) throw; if (r.Name == null) continue; }
                    try { p.Refresh(); var t = p.TotalProcessorTime; r.Time = t; TimeSpan t0; if (first.TryGetValue(p.Id, out t0)) r.Cpu = Math.Max(0, 100 * (t - t0).TotalSeconds / secs); } catch (Exception e) { if (!(e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is NotSupportedException)) throw; }
                    l.Add(r);
                }
                return l;
            }
            finally { foreach (var p in procs) p.Dispose(); }
        }
        static string PsTime(TimeSpan t) { return ((int)t.TotalMinutes).ToString(CultureInfo.InvariantCulture) + ":" + t.Seconds.ToString("00", CultureInfo.InvariantCulture); }
        int Ps(List<string> a, ShCtx c)
        {
            string sort = a.FirstOrDefault(z => z.StartsWith("--sort", StringComparison.Ordinal)) ?? ""; var l = PsSample(500, c);
            IEnumerable<PsRow> s = sort.Contains("cpu") ? l.OrderByDescending(r => double.IsNaN(r.Cpu) ? -1 : r.Cpu) : sort.Contains("mem") || sort.Contains("rss") ? l.OrderByDescending(r => r.Mem) : l.OrderBy(r => r.Pid);
            var rows = new List<string[]> { new[] { "PID", "%CPU", "RSS", "THR", "TIME", "COMMAND" } };
            foreach (var r in s) rows.Add(new[] { r.Pid.ToString(CultureInfo.InvariantCulture), double.IsNaN(r.Cpu) ? "-" : r.Cpu.ToString("0.0", CultureInfo.InvariantCulture), Human(r.Mem), r.Threads.ToString(CultureInfo.InvariantCulture), PsTime(r.Time), r.Name });
            Table(c, rows, ShBlue, true, true, true, true, true);
            return 0;
        }
        List<List<TermView.Seg>> TopFrame(List<PsRow> l, int n)
        {
            var f = new List<List<TermView.Seg>>(); var ms = new Native.MEMORYSTATUSEX(); Native.GlobalMemoryStatusEx(ms);
            f.Add(new List<TermView.Seg> { new TermView.Seg("top - " + UptimeLine().Trim(), ShFg) });
            f.Add(new List<TermView.Seg> { new TermView.Seg("Tasks: " + l.Count + " total · threads " + l.Sum(r => r.Threads), ShFg) });
            f.Add(new List<TermView.Seg> { new TermView.Seg("Mem: " + Human(ms.ullTotalPhys) + " total, " + Human(ms.ullAvailPhys) + " free, " + Human(ms.ullTotalPhys - ms.ullAvailPhys) + " used (" + ms.dwMemoryLoad + "%)", ShFg) });
            f.Add(new List<TermView.Seg> { new TermView.Seg("", ShFg) });
            f.Add(new List<TermView.Seg> { new TermView.Seg("    PID  %CPU      RSS  THR      TIME  COMMAND", ShBlue) });
            foreach (var r in l.OrderByDescending(r => double.IsNaN(r.Cpu) ? -1 : r.Cpu).ThenByDescending(r => r.Mem).Take(n))
                f.Add(new List<TermView.Seg> { new TermView.Seg(r.Pid.ToString(CultureInfo.InvariantCulture).PadLeft(7) + (double.IsNaN(r.Cpu) ? "-" : r.Cpu.ToString("0.0", CultureInfo.InvariantCulture)).PadLeft(6) + Human(r.Mem).PadLeft(9) + r.Threads.ToString(CultureInfo.InvariantCulture).PadLeft(5) + PsTime(r.Time).PadLeft(10) + "  " + r.Name, r.Cpu >= 50 ? ShRed : r.Cpu >= 10 ? ShAmber : ShFg) });
            f.Add(new List<TermView.Seg> { new TermView.Seg("every 2 s · Ctrl+C to leave (reads only - nothing is stopped from here)", ShDim) });
            return f;
        }
        int ShTop(List<string> a, ShCtx c)
        {
            int iters = 0, ni = a.IndexOf("-n"); if (ni >= 0 && ni + 1 < a.Count) int.TryParse(a[ni + 1], out iters);
            if (!c.Out.Screen || c.Job.Bg || a.Contains("-b")) { foreach (var row in TopFrame(PsSample(1000, c), 30)) { foreach (var sg in row) O(c, sg.T, sg.C); O(c, "\n", ShFg); } return 0; }
            for (int i = 0; !c.Job.Cancel && (iters <= 0 || i < iters); i++) { var fr = TopFrame(PsSample(i == 0 ? 700 : 2000, c), 25); if (c.Job.Cancel) break; Ui(delegate { term.Live(fr); }); }
            Ui(delegate { term.EndLive(); }); return 0;
        }
        int ShWatch(List<string> a, ShCtx c)
        {
            double every = 2; int k = 0;
            while (k < a.Count && a[k].StartsWith("-", StringComparison.Ordinal)) { if ((a[k] == "-n" || a[k] == "--interval") && k + 1 < a.Count) { double.TryParse(a[k + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out every); k += 2; } else k++; }
            string line = string.Join(" ", a.Skip(k).Select(z => z.Contains(" ") ? "\"" + z + "\"" : z)); every = Math.Max(0.5, Math.Min(3600, every));
            if (line == "") { Err(c, "usage: watch [-n secs] command"); return 2; }
            if (!c.Out.Screen || c.Job.Bg) { Err(c, "watch: needs the screen - not in a pipe or the background"); return 1; }
            while (!c.Job.Cancel)
            {
                var cap = new ShOut { Cap = new StringBuilder(), Job = c.Job }; ShRunText(line, new ShCtx { Job = c.Job, Out = cap, Args = c.Args, Yes = c.Yes, Script = true });
                var fr = new List<List<TermView.Seg>> { new List<TermView.Seg> { new TermView.Seg("Every " + every.ToString("0.#", CultureInfo.InvariantCulture) + "s: " + line, ShCyan), new TermView.Seg("    " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " · Ctrl+C to leave", ShDim) }, new List<TermView.Seg>() };
                foreach (var l in cap.Cap.ToString().Replace("\r", "").TrimEnd('\n').Split('\n').Take(60)) fr.Add(new List<TermView.Seg> { new TermView.Seg(l, ShFg) });
                Ui(delegate { term.Live(fr); });
                var until = DateTime.UtcNow.AddSeconds(every); while (!c.Job.Cancel && DateTime.UtcNow < until) Thread.Sleep(50);
            }
            Ui(delegate { term.EndLive(); }); return 0;
        }

        // ---------- network: addresses, routes, sockets
        int Ip(List<string> a, ShCtx c)
        {
            string sub = a.FirstOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal)) ?? "addr";
            switch (sub) { case "a": case "addr": case "address": return IpAddr(c, null); case "l": case "link": return IpAddr(c, "link"); case "r": case "route": case "ro": return IpRoute(c); case "n": case "neigh": case "neighbour": case "neighbor": return ToolAs("arp", new List<string>(), c, null); case "-s": return IpAddr(c, "stats"); }
            Err(c, "ip: try  ip a · ip link · ip route · ip neigh"); return 2;
        }
        int IpAddr(ShCtx c, string mode)
        {
            int n = 0;
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties ip; try { ip = ni.GetIPProperties(); } catch (NetworkInformationException) { continue; }
                int idx = -1, mtu = 0; try { var v4 = ip.GetIPv4Properties(); if (v4 != null) { idx = v4.Index; mtu = v4.Mtu; } } catch (NetworkInformationException) { }
                bool up = ni.OperationalStatus == OperationalStatus.Up; string mac = string.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("x2")));
                O(c, (++n) + ": ", ShFg); O(c, ni.Name, ShCyan); OL(c, ": <" + (up ? "UP,LOWER_UP" : "DOWN") + "> mtu " + mtu + " state " + (up ? "UP" : "DOWN") + "  (" + ni.NetworkInterfaceType + (idx >= 0 ? ", if " + idx : "") + ")", up ? ShFg : ShDim);
                OL(c, "    link/" + (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ? "loopback" : "ether") + " " + (mac == "" ? "00:00:00:00:00:00" : mac) + "  " + ni.Description, ShDim);
                if (mode == "link") continue;
                foreach (var ua in ip.UnicastAddresses)
                {
                    bool v6 = ua.Address.AddressFamily == AddressFamily.InterNetworkV6; int pl = 0;
                    try { pl = ua.PrefixLength; } catch (NotImplementedException) { if (ua.IPv4Mask != null) pl = ua.IPv4Mask.GetAddressBytes().Sum(b => Convert.ToString(b, 2).Count(ch => ch == '1')); }
                    O(c, "    " + (v6 ? "inet6 " : "inet ") , ShFg); OL(c, ua.Address.ToString().Split('%')[0] + "/" + pl + (ua.Address.IsIPv6LinkLocal ? " scope link" : " scope global"), v6 ? ShFg : ShGreen);
                }
                if (mode == "stats" || mode == null) { try { var st = ni.GetIPStatistics(); OL(c, "    RX " + Human(st.BytesReceived) + "  TX " + Human(st.BytesSent), ShDim); } catch (Exception e) { if (!(e is NetworkInformationException || e is PlatformNotSupportedException)) throw; } }
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
        int Ss(List<string> a, ShCtx c)
        {
            string fl = string.Concat(a.Where(z => z.StartsWith("-", StringComparison.Ordinal) && !z.StartsWith("--", StringComparison.Ordinal)).Select(z => z.Substring(1)));
            bool listen = fl.Contains("l"), all = fl.Contains("a"), udp = fl.Contains("u"), tcp = fl.Contains("t") || !udp, proc = fl.Contains("p") || true;
            var names = new Dictionary<int, string>(); foreach (var p in Process.GetProcesses()) using (p) try { names[p.Id] = p.ProcessName; } catch (InvalidOperationException) { }
            var rows = new List<string[]> { new[] { "Netid", "State", "Local Address:Port", "Peer Address:Port", "Process" } };
            if (tcp)
            {
                List<Conn> l; try { l = Tables.Tcp(true, names); } catch (InvalidOperationException e) { Err(c, "ss: " + e.Message); return 1; }
                foreach (var x in l.Where(x => listen ? x.State == "LISTEN" : all || x.State != "LISTEN").OrderBy(x => x.State).ThenBy(x => x.LocalPort))
                    rows.Add(new[] { "tcp", x.State, x.Local, x.State == "LISTEN" ? "*:*" : x.Remote, proc ? (x.Proc ?? "") + " pid=" + x.Pid : "" });
            }
            if (udp && (listen || all || !fl.Contains("t")))
                foreach (var u in Tables.Udp().OrderBy(u => u.Key)) { string nm; names.TryGetValue(u.Value, out nm); rows.Add(new[] { "udp", "UNCONN", "*:" + u.Key, "*:*", (nm ?? "") + " pid=" + u.Value }); }
            Table(c, rows, ShBlue);
            return 0;
        }

        // ---------- /proc: this PC as files (cat /proc/meminfo)
        static readonly string[] ProcFiles = { "cpuinfo", "meminfo", "uptime", "version", "mounts", "loadavg" };
        string ProcFile(string name)
        {
            var sb = new StringBuilder(); var ms = new Native.MEMORYSTATUSEX(); Native.GlobalMemoryStatusEx(ms);
            switch (name)
            {
                case "meminfo": sb.AppendLine("MemTotal:       " + (ms.ullTotalPhys / 1024) + " kB").AppendLine("MemFree:        " + (ms.ullAvailPhys / 1024) + " kB").AppendLine("MemAvailable:   " + (ms.ullAvailPhys / 1024) + " kB").AppendLine("CommitLimit:    " + (ms.ullTotalPageFile / 1024) + " kB").AppendLine("Committed_AS:   " + ((ms.ullTotalPageFile - ms.ullAvailPageFile) / 1024) + " kB"); break;
                case "uptime": sb.AppendLine(((DateTime.Now - BootTime()).TotalSeconds).ToString("0.00", CultureInfo.InvariantCulture) + " 0.00"); break;
                case "version": sb.AppendLine("Windows " + Environment.OSVersion.Version + " (" + Environment.OSVersion + ") · AdeeboShell " + Program.Version); break;
                case "loadavg": { double cpu = snap != null ? snap.Cpu : double.NaN; string l = double.IsNaN(cpu) ? "0.00" : (cpu / 100 * Environment.ProcessorCount).ToString("0.00", CultureInfo.InvariantCulture); sb.AppendLine(l + " " + l + " " + l); break; }
                case "mounts": foreach (var d in DriveInfo.GetDrives()) { string fs = "-"; try { if (d.IsReady) fs = d.DriveFormat.ToLowerInvariant(); } catch (IOException) { } sb.AppendLine(d.Name.TrimEnd('\\') + " " + MntOf(d) + " " + fs + " rw 0 0"); } break;
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
        int Cut(List<string> a, ShCtx c)
        {
            string d = "\t", f = null, ch = null; var files = new List<string>();
            for (int i = 0; i < a.Count; i++)
            {
                string w = a[i];
                if (w == "-d" && i + 1 < a.Count) { d = a[++i]; continue; } if (w.StartsWith("-d", StringComparison.Ordinal)) { d = w.Substring(2); continue; }
                if (w == "-f" && i + 1 < a.Count) { f = a[++i]; continue; } if (w.StartsWith("-f", StringComparison.Ordinal)) { f = w.Substring(2); continue; }
                if (w == "-c" && i + 1 < a.Count) { ch = a[++i]; continue; } if (w.StartsWith("-c", StringComparison.Ordinal)) { ch = w.Substring(2); continue; }
                files.Add(w);
            }
            if (f == null && ch == null) { Err(c, "usage: cut -d ',' -f 1,3   or   cut -c 1-10"); return 2; }
            if (d == "") d = "\t";
            var l = InLines(files, c); if (l == null) return 1;
            Func<string, int, List<int>> pick = (spec, max) => { var r = new List<int>(); foreach (var part in spec.Split(',')) { var m = Regex.Match(part, @"^(\d*)-(\d*)$"); int x, y; if (m.Success) { x = m.Groups[1].Value == "" ? 1 : int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); y = m.Groups[2].Value == "" ? max : int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); for (int k = x; k <= Math.Min(y, max); k++) r.Add(k); } else if (int.TryParse(part, out x)) r.Add(x); } return r; };
            foreach (var x in l)
            {
                if (ch != null) { OL(c, string.Concat(pick(ch, x.Length).Where(k => k >= 1 && k <= x.Length).Select(k => x[k - 1]))); continue; }
                var parts = x.Split(new[] { d[0] }); if (parts.Length == 1 && !a.Contains("-s")) { OL(c, x); continue; }
                OL(c, string.Join(d[0].ToString(), pick(f, parts.Length).Where(k => k >= 1 && k <= parts.Length).Select(k => parts[k - 1])));
            }
            return 0;
        }
        static string TrSet(string s)
        {
            s = s.Replace("[:upper:]", "A-Z").Replace("[:lower:]", "a-z").Replace("[:digit:]", "0-9").Replace("[:space:]", " \t\n").Replace("\\n", "\n").Replace("\\t", "\t");
            var sb = new StringBuilder(); for (int i = 0; i < s.Length; i++) { if (i + 2 < s.Length && s[i + 1] == '-') { for (char x = s[i]; x <= s[i + 2]; x++) sb.Append(x); i += 2; } else sb.Append(s[i]); }
            return sb.ToString();
        }
        int Tr(List<string> a, ShCtx c)
        {
            bool del = a.Contains("-d"), sq = a.Contains("-s"); var sets = a.Where(z => !z.StartsWith("-", StringComparison.Ordinal) || z.Length == 1).Select(TrSet).ToList();
            if (sets.Count == 0) { Err(c, "usage: tr 'a-z' 'A-Z'  ·  tr -d '\\r'  ·  tr -s ' '"); return 2; }
            string t = c.Stdin ?? ""; var sb = new StringBuilder(); char last = '\0';
            foreach (char x in t)
            {
                int k = sets[0].IndexOf(x); char o = x;
                if (k >= 0) { if (del) continue; if (sets.Count > 1) o = sets[1][Math.Min(k, sets[1].Length - 1)]; }
                if (sq && o == last && (sets.Count > 1 ? sets[1] : sets[0]).IndexOf(o) >= 0) continue;
                sb.Append(o); last = o;
            }
            O(c, sb.ToString(), ShFg); return 0;
        }
        int Sed(List<string> a, ShCtx c)
        {
            var progs = new List<string>(); var files = new List<string>(); bool quiet = false;
            for (int i = 0; i < a.Count; i++) { if (a[i] == "-e" && i + 1 < a.Count) { progs.Add(a[++i]); continue; } if (a[i] == "-n") { quiet = true; continue; } if (a[i] == "-E" || a[i] == "-r") continue; if (progs.Count == 0) progs.Add(a[i]); else files.Add(a[i]); }
            if (progs.Count == 0) { Err(c, "usage: sed 's/old/new/g' [file]   ·   sed -n '2,5p'   ·   sed '/re/d'"); return 2; }
            var l = InLines(files, c); if (l == null) return 1;
            for (int n = 0; n < l.Count; n++)
            {
                string x = l[n]; bool del = false, print = false;
                foreach (var pr in progs)
                {
                    var sm = Regex.Match(pr, @"^s(.)(.*?)(?<!\\)\1(.*?)(?<!\\)\1([gip]*)$");
                    if (sm.Success) { var rx = new Regex(sm.Groups[2].Value.Replace("\\" + sm.Groups[1].Value, sm.Groups[1].Value), sm.Groups[4].Value.Contains("i") ? RegexOptions.IgnoreCase : RegexOptions.None, TimeSpan.FromSeconds(2)); string rep = Regex.Replace(sm.Groups[3].Value, @"\\(\d)", "$$$1").Replace("&", "$0"); string y = sm.Groups[4].Value.Contains("g") ? rx.Replace(x, rep) : rx.Replace(x, rep, 1); if (y != x && sm.Groups[4].Value.Contains("p")) print = true; x = y; continue; }
                    var dm = Regex.Match(pr, @"^/(.*)/([dp])$"); if (dm.Success) { if (Regex.IsMatch(x, dm.Groups[1].Value)) { if (dm.Groups[2].Value == "d") del = true; else print = true; } continue; }
                    var nm = Regex.Match(pr, @"^(\d+|\$)(?:,(\d+|\$))?([dp])$"); if (nm.Success) { int from = nm.Groups[1].Value == "$" ? l.Count : int.Parse(nm.Groups[1].Value, CultureInfo.InvariantCulture), to = nm.Groups[2].Success ? (nm.Groups[2].Value == "$" ? l.Count : int.Parse(nm.Groups[2].Value, CultureInfo.InvariantCulture)) : from; if (n + 1 >= from && n + 1 <= to) { if (nm.Groups[3].Value == "d") del = true; else print = true; } continue; }
                    Err(c, "sed: this shell's sed knows s/old/new/[gip], /re/d, /re/p, N,Md and N,Mp - not '" + pr + "'"); return 2;
                }
                if (del) continue; if (!quiet) OL(c, x); if (print) OL(c, x);
            }
            return 0;
        }
        // awk: [-F sep] '[/re/ | NR>1 | $3>100] { print $1, $3 }', and the sums: '{ s += $2 } END { print s }'
        int Awk(List<string> a, ShCtx c)
        {
            string sep = null, prog = null; var files = new List<string>();
            for (int i = 0; i < a.Count; i++) { if (a[i] == "-F" && i + 1 < a.Count) { sep = a[++i]; continue; } if (a[i].StartsWith("-F", StringComparison.Ordinal)) { sep = a[i].Substring(2); continue; } if (prog == null) prog = a[i]; else files.Add(a[i]); }
            if (prog == null) { Err(c, "usage: awk '{print $1}'  ·  awk -F, 'NR>1 {print $2, $5}'  ·  awk '{s+=$3} END {print s}'"); return 2; }
            var l = InLines(files, c); if (l == null) return 1;
            var m = Regex.Match(prog.Trim(), @"^(?<pat>[^{]*?)\s*(\{(?<body>[^}]*)\})?\s*(END\s*\{(?<end>[^}]*)\})?$");
            if (!m.Success) { Err(c, "awk: this shell's awk reads  pattern { print ... }  and  { s += $N } END { print s }"); return 2; }
            string pat = m.Groups["pat"].Value.Trim(), body = m.Groups["body"].Success ? m.Groups["body"].Value.Trim() : "print", end = m.Groups["end"].Value.Trim();
            var vars = new Dictionary<string, double>(); int nr = 0;
            Func<string[], string, string> field = (f, e) => { e = e.Trim(); if (e == "NR") return nr.ToString(CultureInfo.InvariantCulture); if (e == "NF") return (f.Length - 1).ToString(CultureInfo.InvariantCulture); if (e.StartsWith("\"", StringComparison.Ordinal)) return e.Trim('"'); var fm = Regex.Match(e, @"^\$(\d+|NF)$"); if (fm.Success) { int k = fm.Groups[1].Value == "NF" ? f.Length - 1 : int.Parse(fm.Groups[1].Value, CultureInfo.InvariantCulture); return k < f.Length ? f[k] : ""; } double v; if (vars.TryGetValue(e, out v)) return v.ToString(CultureInfo.InvariantCulture); return e; };
            Func<string, double> num = s0 => { double v; return double.TryParse(s0, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0; };
            Action<string[], string> run = (f, stmts) =>
            {
                foreach (var st0 in stmts.Split(';'))
                {
                    string st = st0.Trim(); if (st == "") continue;
                    var am = Regex.Match(st, @"^([A-Za-z_]\w*)\s*(\+=|-=|=)\s*(.+)$");
                    if (am.Success) { double cur; vars.TryGetValue(am.Groups[1].Value, out cur); double v = num(field(f, am.Groups[3].Value)); vars[am.Groups[1].Value] = am.Groups[2].Value == "+=" ? cur + v : am.Groups[2].Value == "-=" ? cur - v : v; continue; }
                    if (Regex.IsMatch(st, @"^\w+\+\+$")) { double cur; string nm = st.TrimEnd('+'); vars.TryGetValue(nm, out cur); vars[nm] = cur + 1; continue; }
                    if (st.StartsWith("print", StringComparison.Ordinal)) { string rest = st.Substring(5).Trim(); OL(c, rest == "" ? f[0] : string.Join(" ", Regex.Matches(rest, "\"[^\"]*\"|[^,]+").Cast<Match>().Select(x => field(f, x.Value)))); continue; }
                }
            };
            foreach (var x in l)
            {
                nr++; var parts = sep == null ? x.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries) : x.Split(new[] { sep }, StringSplitOptions.None);
                var f = new[] { x }.Concat(parts).ToArray();
                if (pat != "" && pat != "BEGIN")
                {
                    bool ok;
                    var rm = Regex.Match(pat, @"^/(.*)/$"); var cm = Regex.Match(pat, @"^(\S+)\s*(==|!=|>=|<=|>|<)\s*(\S+)$");
                    if (rm.Success) ok = Regex.IsMatch(x, rm.Groups[1].Value);
                    else if (cm.Success) { string l0 = field(f, cm.Groups[1].Value), r0 = field(f, cm.Groups[3].Value); double p, q; bool n0 = double.TryParse(l0, NumberStyles.Float, CultureInfo.InvariantCulture, out p) & double.TryParse(r0, NumberStyles.Float, CultureInfo.InvariantCulture, out q); switch (cm.Groups[2].Value) { case "==": ok = n0 ? p == q : l0 == r0; break; case "!=": ok = n0 ? p != q : l0 != r0; break; case ">": ok = n0 && p > q; break; case "<": ok = n0 && p < q; break; case ">=": ok = n0 && p >= q; break; default: ok = n0 && p <= q; break; } }
                    else { Err(c, "awk: pattern '" + pat + "' - use /regex/ or a comparison like NR>1 or $3>100"); return 2; }
                    if (!ok) continue;
                }
                run(f, body);
            }
            if (end != "") run(new[] { "" }, end);
            return 0;
        }
        int Xargs(List<string> a, ShCtx c)
        {
            int per = 0, ni = a.IndexOf("-n"); if (ni >= 0 && ni + 1 < a.Count) { int.TryParse(a[ni + 1], out per); a.RemoveRange(ni, 2); }
            var items = Regex.Matches(c.Stdin ?? "", @"\S+").Cast<Match>().Select(m => m.Value).ToList(); if (items.Count == 0) return 0;
            string cmd = a.Count == 0 ? "echo" : string.Join(" ", a.Select(z => z.Contains(" ") ? "\"" + z + "\"" : z)); int worst = 0;
            foreach (var chunk in per > 0 ? items.Select((v, i) => new { v, i }).GroupBy(z => z.i / per).Select(g => g.Select(z => z.v).ToList()) : new[] { items })
            {
                if (c.Job.Cancel) return 130;
                worst = Math.Max(worst, ShRunText(cmd + " " + string.Join(" ", chunk.Select(z => "\"" + z.Replace("\"", "\\\"") + "\"")), new ShCtx { Job = c.Job, Out = c.Out, Args = c.Args, Yes = c.Yes, Script = c.Script }));
            }
            return worst;
        }
        int B64(List<string> a, ShCtx c)
        {
            bool dec = a.Contains("-d") || a.Contains("--decode"); var f = a.FirstOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal));
            byte[] data;
            if (f != null) { string r = Real(Abs(f)); if (r == null || !File.Exists(r)) { Err(c, "base64: " + f + ": no such file"); return 1; } if (new FileInfo(r).Length > 20 * 1048576) { Err(c, "base64: over 20 MB"); return 1; } data = File.ReadAllBytes(r); }
            else data = Encoding.UTF8.GetBytes(c.Stdin ?? "");
            if (!dec) { OL(c, Convert.ToBase64String(data)); return 0; }
            try { O(c, Encoding.UTF8.GetString(Convert.FromBase64String(Regex.Replace(Encoding.UTF8.GetString(data), @"\s", ""))), ShFg); return 0; }
            catch (FormatException) { Err(c, "base64: that isn't base64"); return 1; }
        }
        int HashSum(string cmd, List<string> a, ShCtx c)
        {
            Func<System.Security.Cryptography.HashAlgorithm> mk = () => cmd == "md5sum" ? (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.MD5.Create() : cmd == "sha1sum" ? (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.SHA1.Create() : System.Security.Cryptography.SHA256.Create();
            var files = a.Where(z => !z.StartsWith("-", StringComparison.Ordinal)).ToList();
            if (files.Count == 0) { using (var h = mk()) OL(c, BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(c.Stdin ?? ""))).Replace("-", "").ToLowerInvariant() + "  -"); return 0; }
            int ex = 0;
            foreach (var f in files) { string r = Real(Abs(f)); if (r == null || !File.Exists(r)) { Err(c, cmd + ": " + f + ": no such file"); ex = 1; continue; } using (var h = mk()) using (var fs = File.OpenRead(r)) OL(c, BitConverter.ToString(h.ComputeHash(fs)).Replace("-", "").ToLowerInvariant() + "  " + f); }
            return ex;
        }
    }
}
