// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // ================================================================== Inside Windows: who started whom, what each one does right now
    // Everything here is read-only and works without admin. Where Windows keeps a system process to itself, we say so.
    static class ProcTree
    {
        public sealed class P { public int Pid, Parent; public string Name = "", Path = ""; public int Threads; public List<P> Kids = new List<P>(); public bool Orphan; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PROCESSENTRY32 { public int dwSize, cntUsage, th32ProcessID; public IntPtr th32DefaultHeapID; public int th32ModuleID, cntThreads, th32ParentProcessID, pcPriClassBase, dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile; }
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(int flags, int pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32FirstW(IntPtr snap, ref PROCESSENTRY32 e);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32NextW(IntPtr snap, ref PROCESSENTRY32 e);

        [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr h, out long created, out long exited, out long kernel, out long user);
        static long Started(int pid)
        {
            if (pid <= 4) return 0; IntPtr h = Native.OpenProcess(0x1000 /*query limited*/, false, pid); if (h == IntPtr.Zero) return 0;
            try { long c, e, k, u; return GetProcessTimes(h, out c, out e, out k, out u) ? c : 0; } finally { Native.CloseHandle(h); }
        }
        // every process with its parent; roots are processes whose parent is gone (Windows reuses ids, so a parent must also be older)
        public static List<P> Snapshot(out Dictionary<int, P> byPid)
        {
            byPid = new Dictionary<int, P>(); var all = new List<P>();
            IntPtr h = CreateToolhelp32Snapshot(2, 0);
            if (h == IntPtr.Zero || h == new IntPtr(-1)) return all;
            try
            {
                var e = new PROCESSENTRY32 { dwSize = Marshal.SizeOf(typeof(PROCESSENTRY32)) };
                if (Process32FirstW(h, ref e))
                    do { var p = new P { Pid = e.th32ProcessID, Parent = e.th32ParentProcessID, Name = e.szExeFile, Threads = e.cntThreads }; all.Add(p); byPid[p.Pid] = p; }
                    while (Process32NextW(h, ref e) && all.Count < 5000);
            }
            finally { Native.CloseHandle(h); }
            // Windows reuses process ids: a "parent" that started after its child is a newcomer with the old parent's id
            var born = new Dictionary<int, long>();
            foreach (var p in all) { long t = Started(p.Pid); if (t > 0) born[p.Pid] = t; }
            var roots = new List<P>();
            foreach (var p in all)
            {
                P par; long tp, tc;
                bool older = !born.TryGetValue(p.Parent, out tp) || !born.TryGetValue(p.Pid, out tc) || tp <= tc;
                if (p.Pid != p.Parent && p.Pid != 0 && older && byPid.TryGetValue(p.Parent, out par)) par.Kids.Add(p);
                else { p.Orphan = p.Pid > 4; roots.Add(p); }
            }
            // anything still not reachable from a root sits in a loop of reused ids: break it there, never lose a process
            var seen = new HashSet<P>(); var stack = new Stack<P>(roots);
            while (stack.Count > 0) { var x = stack.Pop(); if (!seen.Add(x)) continue; foreach (var k in x.Kids) stack.Push(k); }
            foreach (var p in all.Where(z => !seen.Contains(z)).ToList())
            {
                if (seen.Contains(p)) continue;
                P par; if (byPid.TryGetValue(p.Parent, out par)) par.Kids.Remove(p);
                p.Orphan = true; roots.Add(p);
                stack.Push(p); while (stack.Count > 0) { var x = stack.Pop(); if (!seen.Add(x)) continue; foreach (var k in x.Kids) stack.Push(k); }
            }
            return roots;
        }
        // "Started by explorer.exe (you, from the Start menu or taskbar)" etc.
        public static string WhyStarted(P p, Dictionary<int, P> by, Dictionary<string, string> startup)
        {
            string n = (p.Name ?? "").ToLowerInvariant(); P par; by.TryGetValue(p.Parent, out par);
            string pn = par != null ? par.Name.ToLowerInvariant() : "";
            string where; if (startup != null && startup.TryGetValue(n, out where)) return "Starts with Windows - added in " + where + ".";
            if (pn == "services.exe") return "A Windows service - started by the Service Control Manager (services.exe).";
            if (pn == "svchost.exe") return "Started by a Windows service (svchost) - often a scheduled task or a background helper.";
            if (pn == "explorer.exe") return "You started it (Start menu, taskbar, a shortcut or a double-click) - Explorer is its parent.";
            if (pn == "winlogon.exe" || pn == "userinit.exe") return "Part of signing in to Windows.";
            if (pn == "wininit.exe" || pn == "smss.exe") return "Part of Windows starting up.";
            if (pn == "taskeng.exe" || pn == "taskhostw.exe") return "A scheduled task.";
            if (par != null) return "Started by " + par.Name + ".";
            return p.Pid <= 4 ? "Part of Windows itself." : "Its parent has already exited.";
        }
    }

    // ---------- disk reads and writes per program (PROCESS_QUERY_LIMITED_INFORMATION, no admin)
    static class ProcIo
    {
        [StructLayout(LayoutKind.Sequential)] struct IO_COUNTERS { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
        [DllImport("kernel32.dll")] static extern bool GetProcessIoCounters(IntPtr h, out IO_COUNTERS c);
        // total bytes read and written since the program started, or null when Windows says no
        public static ulong[] Of(int pid)
        {
            IntPtr h = Native.OpenProcess(0x1000, false, pid); if (h == IntPtr.Zero) return null;
            try { IO_COUNTERS c; return GetProcessIoCounters(h, out c) ? new[] { c.ReadBytes, c.WriteBytes } : null; }
            finally { Native.CloseHandle(h); }
        }
    }

    // ---------- Windows services and the process each one lives in (Service Control Manager, enumerate rights only)
    static class Svc
    {
        public sealed class S { public string Name = "", Display = ""; public int Pid, State; }
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr OpenSCManager(string machine, string db, int access);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool EnumServicesStatusEx(IntPtr sc, int level, int type, int state, IntPtr buf, int size, out int needed, out int count, ref int resume, string group);
        [DllImport("advapi32.dll")] static extern bool CloseServiceHandle(IntPtr h);
        public static List<S> List()
        {
            var r = new List<S>();
            IntPtr sc = OpenSCManager(null, null, 0x0004 /* ENUMERATE_SERVICE */); if (sc == IntPtr.Zero) return r;
            try
            {
                int need, count, resume = 0;
                EnumServicesStatusEx(sc, 0, 0x30 /* WIN32 */, 3 /* all */, IntPtr.Zero, 0, out need, out count, ref resume, null);
                if (need <= 0) return r;
                IntPtr buf = Marshal.AllocHGlobal(need + 4096);
                try
                {
                    resume = 0;
                    if (!EnumServicesStatusEx(sc, 0, 0x30, 3, buf, need + 4096, out need, out count, ref resume, null)) return r;
                    int sz = IntPtr.Size == 8 ? 56 : 44;   // ENUM_SERVICE_STATUS_PROCESS: two string pointers + SERVICE_STATUS_PROCESS (9 DWORDs), padded to pointer size
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr e = buf + i * sz;
                        r.Add(new S { Name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(e)), Display = Marshal.PtrToStringUni(Marshal.ReadIntPtr(e + IntPtr.Size)), State = Marshal.ReadInt32(e + IntPtr.Size * 2 + 4), Pid = Marshal.ReadInt32(e + IntPtr.Size * 2 + 28) });
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            finally { CloseServiceHandle(sc); }
            return r;
        }
        // service name → what it's for, for the ones people meet in svchost (the knowledge pack has the long stories)
        static readonly Dictionary<string, string> what = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "wuauserv", "Windows Update - downloads and installs updates" }, { "BITS", "Background transfers - downloads updates gently in the background" },
            { "DoSvc", "Delivery Optimization - shares update pieces with other PCs" }, { "Dnscache", "DNS Client - remembers names it has looked up" },
            { "Dhcp", "DHCP Client - gets your IP address from the router" }, { "WlanSvc", "WLAN AutoConfig - joins and keeps your Wi-Fi" },
            { "NlaSvc", "Network Location Awareness - decides 'Home' vs 'Public' network" }, { "netprofm", "Network List - names your networks" },
            { "LanmanWorkstation", "Workstation - lets you open shared folders" }, { "LanmanServer", "Server - shares this PC's folders" },
            { "EventLog", "Event Log - Windows' diary" }, { "Schedule", "Task Scheduler - runs scheduled tasks" }, { "Winmgmt", "WMI - lets tools ask Windows about itself" },
            { "AudioSrv", "Windows Audio" }, { "AudioEndpointBuilder", "Audio devices" }, { "Spooler", "Print Spooler - printing" },
            { "CryptSvc", "Cryptographic Services - checks signatures and certificates" }, { "WinHttpAutoProxySvc", "Finds your proxy settings (WPAD / PAC)" },
            { "iphlpsvc", "IP Helper - IPv6 transition tech and tunnels" }, { "SysMain", "SysMain (Superfetch) - preloads apps you use often" },
            { "WSearch", "Windows Search - indexes files for search" }, { "TokenBroker", "Web Account Manager - signs apps in to Microsoft 365" },
            { "mpssvc", "Windows Defender Firewall" }, { "BFE", "Base Filtering Engine - the firewall's rule engine" }, { "Wcmsvc", "Connection Manager - picks Wi-Fi vs cable" },
            { "WinDefend", "Microsoft Defender Antivirus" }, { "RasMan", "Remote Access - VPN connections" }, { "IKEEXT", "IKE and AuthIP - IPsec VPN keys" },
            { "dot3svc", "Wired AutoConfig - 802.1X sign-in on network ports" }, { "Themes", "Themes - the look of Windows" }, { "UsoSvc", "Update Orchestrator - schedules updates" },
            { "CDPSvc", "Connected Devices Platform - phone link, nearby sharing" }, { "DPS", "Diagnostic Policy - detects and fixes problems" }, { "lmhosts", "NetBIOS name helper (old Windows networking)" },
            { "SSDPSRV", "SSDP Discovery - finds media devices on your network" }, { "FDResPub", "Publishes this PC on the network" }, { "PlugPlay", "Plug and Play - devices" },
            { "Power", "Power - sleep, battery" }, { "ProfSvc", "User Profile - your account's files and settings" }, { "Appinfo", "Application Information - the 'Run as administrator' prompt" },
        };
        public static string What(string name) { string w; return what.TryGetValue(name ?? "", out w) ? w : ""; }
    }

    // ---------- what starts with Windows, and who put it there (read-only)
    static class Startup
    {
        public sealed class Item { public string Name = "", Command = "", Where = "", Exe = ""; }
        public static List<Item> List()
        {
            var r = new List<Item>();
            Action<RegistryKey, string, string> reg = delegate(RegistryKey root, string path, string label)
            {
                try
                {
                    using (var k = root.OpenSubKey(path))
                        if (k != null) foreach (var n in k.GetValueNames()) { string cmd; try { cmd = Convert.ToString(k.GetValue(n), CultureInfo.InvariantCulture); } catch (Exception ve) { if (!(ve is System.Security.SecurityException || ve is IOException || ve is UnauthorizedAccessException)) throw; cmd = "(couldn't read)"; } r.Add(new Item { Name = n, Command = cmd, Where = label, Exe = ExeOf(cmd) }); }
                }
                catch { }
            };
            reg(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "your Run list (HKCU) - you or an app you installed");
            reg(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "the PC's Run list (HKLM) - an installer or IT");
            reg(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "the PC's Run list (32-bit apps)");
            foreach (var dir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) })
                try { if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase))) r.Add(new Item { Name = Path.GetFileNameWithoutExtension(f), Command = f, Where = dir.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase) ? "your Startup folder" : "the shared Startup folder", Exe = Path.GetFileName(f) }); }
                catch { }
            // whether Windows' own Startup apps switch is off for it (Task Manager → Startup apps)
            foreach (var it in r)
                foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
                    try
                    {
                        using (var k = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"))
                        {
                            var v = k == null ? null : k.GetValue(it.Name) as byte[];
                            if (v != null && v.Length > 0 && (v[0] & 1) == 1) it.Where += " · switched off in Task Manager";
                        }
                    }
                    catch { }
            return r;
        }
        static string ExeOf(string cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return "";
            cmd = cmd.Trim(); string exe = cmd.StartsWith("\"", StringComparison.Ordinal) ? cmd.Substring(1, Math.Max(0, cmd.IndexOf('"', 1) - 1)) : cmd.Split(' ')[0];
            // a stray quote or tab in a Run value must not hide it (and every entry after it): take the last part by hand
            exe = exe.Trim('"', ' ', '\t'); int cut = Math.Max(exe.LastIndexOf('\\'), exe.LastIndexOf('/'));
            return cut >= 0 ? exe.Substring(cut + 1) : exe;
        }
        // exe name (lower case) → where it's started from, for "why did it start?"
        public static Dictionary<string, string> ByExe()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in List()) if (it.Exe != "" && !d.ContainsKey(it.Exe.ToLowerInvariant())) d[it.Exe.ToLowerInvariant()] = it.Where;
            return d;
        }
    }
}
