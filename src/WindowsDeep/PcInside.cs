// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // ================================================================== 1.1.7 (D6): the PC's insides, read-only and without admin
    // DLL imports of a program, the USB tree with its power budget, handle / GDI / USER counts, environment variables and PATH,
    // PCIe links with a calculator, drive endurance, disk latency per disk and monitor EDIDs. Nothing is changed or written;
    // what Windows keeps to itself says "not readable here" instead of a guess.
    static class PcInside
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string F(double v, string f) { return v.ToString(f, CultureInfo.CurrentCulture); }
        static string N(long v) { return v.ToString("N0", CultureInfo.CurrentCulture); }

        // ---------------------------------------------------------------- DLL imports (static PE import table; the file is only read)
        static byte[] ReadAt(FileStream fs, long off, int n) { var b = new byte[n]; if (off < 0 || off >= fs.Length) return null; fs.Position = off; int got = 0; while (got < n) { int k = fs.Read(b, got, n - got); if (k <= 0) break; got += k; } return got == n ? b : null; }
        internal sealed class Pe { public bool Is64; public ulong ImageBase; public readonly List<string> Imports = new List<string>(), Delay = new List<string>(); public string Error = ""; }
        internal static Pe ReadPe(string path)
        {
            var pe = new Pe();
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var dos = ReadAt(fs, 0, 64); if (dos == null || dos[0] != 'M' || dos[1] != 'Z') { pe.Error = "not a Windows program or DLL (no MZ header)"; return pe; }
                    int lfa = BitConverter.ToInt32(dos, 0x3C); var nt = ReadAt(fs, lfa, 24); if (nt == null || nt[0] != 'P' || nt[1] != 'E' || nt[2] != 0 || nt[3] != 0) { pe.Error = "not a Windows program or DLL (no PE header)"; return pe; }
                    int nsec = BitConverter.ToUInt16(nt, 6), optSize = BitConverter.ToUInt16(nt, 20); var opt = ReadAt(fs, lfa + 24, optSize); if (opt == null || optSize < 96) { pe.Error = "the header is damaged"; return pe; }
                    int magic = BitConverter.ToUInt16(opt, 0); pe.Is64 = magic == 0x20b; if (magic != 0x10b && magic != 0x20b) { pe.Error = "unknown header type"; return pe; }
                    pe.ImageBase = pe.Is64 ? BitConverter.ToUInt64(opt, 24) : BitConverter.ToUInt32(opt, 28);
                    int dd = pe.Is64 ? 112 : 96, ndd = BitConverter.ToInt32(opt, dd - 4);
                    Func<int, uint[]> dir = i => i < ndd && dd + i * 8 + 8 <= optSize ? new[] { BitConverter.ToUInt32(opt, dd + i * 8), BitConverter.ToUInt32(opt, dd + i * 8 + 4) } : new uint[] { 0, 0 };
                    var secs = ReadAt(fs, lfa + 24 + optSize, nsec * 40) ?? new byte[0];
                    Func<ulong, long> off = rva => { for (int s = 0; s * 40 + 40 <= secs.Length; s++) { uint va = BitConverter.ToUInt32(secs, s * 40 + 12), vs = Math.Max(BitConverter.ToUInt32(secs, s * 40 + 8), BitConverter.ToUInt32(secs, s * 40 + 16)), raw = BitConverter.ToUInt32(secs, s * 40 + 20); if (rva >= va && rva < (ulong)va + vs) return (long)(rva - va + raw); } return -1; };
                    Func<ulong, string> name = rva => { long o = off(rva); if (o < 0) return null; var b = ReadAt(fs, o, (int)Math.Min(256, fs.Length - o)); if (b == null) return null; int z = Array.IndexOf(b, (byte)0); return Encoding.ASCII.GetString(b, 0, z < 0 ? b.Length : z); };
                    var imp = dir(1); if (imp[0] != 0) { long o = off(imp[0]); for (int i = 0; o >= 0 && i < 2000; i++) { var d = ReadAt(fs, o + i * 20, 20); if (d == null || d.All(x => x == 0)) break; string n = name(BitConverter.ToUInt32(d, 12)); if (!string.IsNullOrEmpty(n)) pe.Imports.Add(n); } }
                    var dly = dir(13); if (dly[0] != 0) { long o = off(dly[0]); for (int i = 0; o >= 0 && i < 2000; i++) { var d = ReadAt(fs, o + i * 32, 32); if (d == null || d.All(x => x == 0)) break; uint attr = BitConverter.ToUInt32(d, 0), nr = BitConverter.ToUInt32(d, 4); ulong rva = (attr & 1) == 0 && nr >= pe.ImageBase ? nr - pe.ImageBase : nr; string n = name(rva); if (!string.IsNullOrEmpty(n)) pe.Delay.Add(n); } }
                }
            }
            catch (UnauthorizedAccessException) { pe.Error = "Windows won't let this account read that file"; }
            catch (IOException e) { pe.Error = "couldn't read it: " + Draw.Trunc(e.Message, 80); }
            return pe;
        }
        static string Sys32(bool want64)
        {
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!Environment.Is64BitOperatingSystem) return Path.Combine(win, "System32");
            if (want64) return Path.Combine(win, Environment.Is64BitProcess ? "System32" : "Sysnative");   // a 32-bit process sees SysWOW64 under the name System32
            return Path.Combine(win, "SysWOW64");
        }
        static HashSet<string> KnownDlls()
        {
            var k = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\KnownDLLs")) if (key != null) foreach (var v in key.GetValueNames()) { string s = Convert.ToString(key.GetValue(v)); if (s.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) k.Add(s); } }
            catch (System.Security.SecurityException) { }
            return k;
        }
        internal static string FindProgram(string input)
        {
            string p = Environment.ExpandEnvironmentVariables((input ?? "").Trim().Trim('"'));
            if (p == "") return null; if (File.Exists(p)) return Path.GetFullPath(p);
            if (p.IndexOfAny(new[] { '\\', '/' }) >= 0) return null;
            if (!p.Contains('.')) p += ".exe";
            foreach (var d in new[] { Sys32(true) }.Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')))
            { try { if (d.Trim() == "") continue; string f = Path.Combine(Environment.ExpandEnvironmentVariables(d.Trim()), p); if (File.Exists(f)) return f; } catch (ArgumentException) { } }
            return null;
        }
        public static List<string[]> Dlls(string input)
        {
            var rows = new List<string[]>(); string path = FindProgram(input);
            if (path == null) { rows.Add(R(2, "File", string.IsNullOrWhiteSpace(input) ? "type a program or DLL - a full path, or a name like notepad.exe" : "couldn't find " + input, "A full path works best, e.g. C:\\Program Files\\App\\app.exe.", "u")); return rows; }
            var pe = ReadPe(path);
            if (pe.Error != "") { rows.Add(R(2, Path.GetFileName(path), pe.Error, "", "u")); return rows; }
            string dir = Path.GetDirectoryName(path), sys = Sys32(pe.Is64); var known = KnownDlls();
            var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Select(d => d.Trim()).Where(d => d != "").ToList();
            rows.Add(R(3, Path.GetFileName(path), (pe.Is64 ? "64-bit" : "32-bit") + " · " + pe.Imports.Count + " DLLs it needs at start" + (pe.Delay.Count > 0 ? " · " + pe.Delay.Count + " it loads later" : ""), path, "w"));
            int missing = 0;
            foreach (var grp in new[] { Tuple.Create(pe.Imports, false), Tuple.Create(pe.Delay, true) })
                foreach (var d in grp.Item1.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    string where = null;
                    if (d.StartsWith("api-ms-win-", StringComparison.OrdinalIgnoreCase) || d.StartsWith("ext-ms-", StringComparison.OrdinalIgnoreCase)) where = "a Windows API set - Windows maps it to a real DLL";
                    else if (known.Contains(d)) where = "a Windows core DLL (KnownDLLs)";
                    else if (File.Exists(Path.Combine(dir, d))) where = "next to the program";
                    else if (File.Exists(Path.Combine(sys, d))) where = "in Windows (" + Path.GetFileName(sys) + ")";
                    else foreach (var pd in pathDirs) { try { if (File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables(pd), d))) { where = "found through PATH: " + pd; break; } } catch (ArgumentException) { } }
                    if (where == null) missing++;
                    rows.Add(R(where == null ? (grp.Item2 ? 1 : 2) : 0, (grp.Item2 ? "later: " : "") + d, where ?? (grp.Item2 ? "not found - only matters when the program reaches the feature that uses it" : "not found - the program won't start without it"), "", "w"));
                }
            rows.Insert(1, R(missing == 0 ? 0 : 2, "Verdict", missing == 0 ? "every DLL it names directly is there" : missing + " DLL" + (missing == 1 ? "" : "s") + " not found",
                missing == 0 ? "Only the first level is checked - a DLL can still miss one of its own." : "Usually fixed by reinstalling the program or its runtime (Visual C++ Redistributable for msvcp / vcruntime names).", "w"));
            return rows;
        }

        // ---------------------------------------------------------------- USB: host controllers, hubs, ports, speed and the power budget
        [StructLayout(LayoutKind.Sequential)] struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] struct SP_DEVINFO_DATA { public int cbSize; public Guid ClassGuid; public int DevInst; public IntPtr Reserved; }
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevsW(ref Guid g, IntPtr enumerator, IntPtr parent, int flags);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevsW(IntPtr g, IntPtr enumerator, IntPtr parent, int flags);
        [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid g, int i, ref SP_DEVICE_INTERFACE_DATA d);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SP_DEVICE_INTERFACE_DATA d, IntPtr detail, int size, out int need, IntPtr info);
        [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInfo(IntPtr set, int i, ref SP_DEVINFO_DATA d);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref SP_DEVINFO_DATA d, int prop, out int type, byte[] buf, int size, out int need);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sa, uint disp, uint flags, IntPtr tmpl);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle h, uint code, byte[] inb, int inLen, byte[] outb, int outLen, out int got, IntPtr ov);
        static readonly Guid HostGuid = new Guid("3abf6f2d-71c4-462a-8a92-1e6861e6af27");
        const uint GetNodeInfo = 0x220408, GetRootHubName = 0x220408, GetDescriptor = 0x220410, GetConnName = 0x220414, GetDriverKey = 0x220420, GetConnInfoEx = 0x220448;
        static List<string> InterfacePaths(Guid g)
        {
            var o = new List<string>(); IntPtr set = SetupDiGetClassDevsW(ref g, IntPtr.Zero, IntPtr.Zero, 0x12 /* present | interface */); if (set == new IntPtr(-1)) return o;
            try
            {
                for (int i = 0; i < 64; i++)
                {
                    var d = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA)) }; if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d)) break;
                    int need; SetupDiGetDeviceInterfaceDetailW(set, ref d, IntPtr.Zero, 0, out need, IntPtr.Zero); if (need <= 0) continue;
                    IntPtr buf = Marshal.AllocHGlobal(need);
                    try { Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6); if (SetupDiGetDeviceInterfaceDetailW(set, ref d, buf, need, out need, IntPtr.Zero)) o.Add(Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4))); }
                    finally { Marshal.FreeHGlobal(buf); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return o;
        }
        // driver key ("{36fc9e60-...}\0012") -> the name Device Manager shows
        static Dictionary<string, string> DriverNames()
        {
            var m = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); IntPtr set = SetupDiGetClassDevsW(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0x6 /* all classes | present */); if (set == new IntPtr(-1)) return m;
            try
            {
                var buf = new byte[1024];
                for (int i = 0; i < 4000; i++)
                {
                    var d = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA)) }; if (!SetupDiEnumDeviceInfo(set, i, ref d)) break;
                    int t, need; Func<int, string> prop = p => SetupDiGetDeviceRegistryPropertyW(set, ref d, p, out t, buf, buf.Length, out need) ? Encoding.Unicode.GetString(buf, 0, Math.Max(0, need - 2)).TrimEnd('\0') : "";
                    string key = prop(9 /* SPDRP_DRIVER */); if (key == "") continue; string n = prop(12 /* friendly name */); if (n == "") n = prop(0 /* description */); m[key] = n;
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return m;
        }
        static Microsoft.Win32.SafeHandles.SafeFileHandle Open(string path) { return CreateFileW(path, 0x40000000 /* write - what the USB stack asks for */, 0x2, IntPtr.Zero, 3, 0, IntPtr.Zero); }
        static string NameIoctl(Microsoft.Win32.SafeHandles.SafeFileHandle h, uint code, int port, int skip)
        {
            var inb = new byte[512]; BitConverter.GetBytes(port).CopyTo(inb, 0); var outb = new byte[512]; int got;
            if (!DeviceIoControl(h, code, inb, port > 0 ? inb.Length : 0, outb, outb.Length, out got, IntPtr.Zero) || got <= skip) return null;
            return Encoding.Unicode.GetString(outb, skip, got - skip).TrimEnd('\0');
        }
        sealed class UsbPort { public int Port, Speed, Ma = -1; public bool Hub, Self; public string Name = "", Vid = ""; }
        static readonly string[] SpeedName = { "USB 1 low speed (1.5 Mb/s)", "USB 1 full speed (12 Mb/s)", "USB 2 (480 Mb/s)", "USB 3 (5 Gb/s or more)" };
        static void Hub(string hubName, int depth, Dictionary<string, string> names, List<string[]> rows, bool root, ref int over, int guard)
        {
            if (guard > 8) return;
            using (var h = Open(@"\\.\" + hubName))
            {
                if (h.IsInvalid) { rows.Add(R(3, new string(' ', depth * 2) + "Hub", "not readable here", "", "u")); return; }
                var info = new byte[128]; int got;
                if (!DeviceIoControl(h, GetNodeInfo, info, info.Length, info, info.Length, out got, IntPtr.Zero) || got < 76) { rows.Add(R(3, new string(' ', depth * 2) + "Hub", "Windows didn't describe it", "", "u")); return; }
                int ports = info[6]; bool bus = info[75] != 0; if (ports <= 0 || ports > 64) return;
                for (int p = 1; p <= ports; p++)
                {
                    var inb = new byte[512]; BitConverter.GetBytes(p).CopyTo(inb, 0);
                    if (!DeviceIoControl(h, GetConnInfoEx, inb, inb.Length, inb, inb.Length, out got, IntPtr.Zero) || got < 35) continue;
                    if (BitConverter.ToInt32(inb, 31) != 1 /* connected */) continue;
                    var u = new UsbPort { Port = p, Speed = inb[23], Hub = inb[24] != 0, Vid = BitConverter.ToUInt16(inb, 12).ToString("X4") + ":" + BitConverter.ToUInt16(inb, 14).ToString("X4") };
                    string key = NameIoctl(h, GetDriverKey, p, 8); string nm; if (key != null && names.TryGetValue(key, out nm) && nm != "") u.Name = nm; else u.Name = u.Hub ? "USB hub" : "USB device " + u.Vid;
                    // its configuration descriptor: bMaxPower (2 mA units, 8 mA on USB 3) and whether it brings its own power
                    var req = new byte[12 + 9]; BitConverter.GetBytes(p).CopyTo(req, 0); req[4] = 0x80; req[5] = 6; req[6] = 0; req[7] = 2; req[10] = 9;
                    if (DeviceIoControl(h, GetDescriptor, req, req.Length, req, req.Length, out got, IntPtr.Zero) && got >= 21 && req[13] == 2) { u.Self = (req[12 + 7] & 0x40) != 0; u.Ma = req[12 + 8] * (u.Speed >= 3 ? 8 : 2); }
                    int budget = root || !bus ? (u.Speed >= 3 ? 900 : 500) : 100, ma = u.Self ? 0 : u.Ma;
                    bool hot = ma > budget; if (hot) over++;
                    string val = (u.Speed < SpeedName.Length ? SpeedName[u.Speed] : "USB") + (u.Ma < 0 ? " · power not reported" : u.Self ? " · brings its own power" : " · asks for up to " + u.Ma + " mA of the port's " + budget + " mA");
                    rows.Add(R(hot ? 2 : 3, new string(' ', depth * 2 + 2) + "Port " + p + ": " + u.Name, val, hot ? "More than this port can give - plug it straight into the PC or use a hub with its own power supply." : "", "w"));
                    if (u.Hub) { string child = NameIoctl(h, GetConnName, p, 8); if (child != null) Hub(child, depth + 2, names, rows, false, ref over, guard + 1); }
                }
                if (!root) rows.Add(R(bus ? 1 : 3, new string(' ', depth * 2 + 2) + "This hub", bus ? "takes its power from the USB cable - 100 mA per port, 500 mA for all of them" : "has its own power supply - 500 mA per port (900 on USB 3)", "", "w"));
            }
        }
        public static List<string[]> Usb()
        {
            var rows = new List<string[]>(); int over = 0;
            try
            {
                var names = DriverNames(); var hosts = InterfacePaths(HostGuid);
                if (hosts.Count == 0) { rows.Add(R(3, "USB", "Windows didn't list any USB controllers here", "", "u")); return rows; }
                foreach (var hp in hosts)
                    using (var h = Open(hp))
                    {
                        if (h.IsInvalid) { rows.Add(R(3, "Controller", "not readable here", "", "u")); continue; }
                        string key = NameIoctl(h, 0x220424 /* HCD driver key */, 0, 4), cn; rows.Add(R(3, key != null && names.TryGetValue(key, out cn) ? cn : "USB controller", "", "", "w"));
                        string root = NameIoctl(h, GetRootHubName, 0, 4); if (root == null) { rows.Add(R(3, "  Root hub", "not readable here", "", "u")); continue; }
                        Hub(root, 0, names, rows, true, ref over, 0);
                    }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; rows.Add(R(3, "USB", "couldn't read: " + Draw.Trunc(e.Message, 80), "", "u")); }
            rows.Insert(0, R(over > 0 ? 2 : 0, "Power budget", over > 0 ? over + " device" + (over == 1 ? " asks" : "s ask") + " for more power than its port gives" : "every device fits its port's power", "Each device's own request (its descriptor), against what its port can give. A drive or webcam that keeps disconnecting on an unpowered hub is the classic case.", "w"));
            return rows;
        }

        // ---------------------------------------------------------------- handles, GDI and USER objects (counts only - the handle list needs admin)
        [DllImport("user32.dll")] static extern int GetGuiResources(IntPtr h, int kind);
        static Dictionary<int, long[]> Counts()
        {
            var m = new Dictionary<int, long[]>();
            foreach (var p in Process.GetProcesses())
                using (p)
                {
                    try
                    {
                        long hc = p.HandleCount, gdi = -1, usr = -1; IntPtr h = Native.OpenProcess(0x1000, false, p.Id);
                        if (h != IntPtr.Zero) try { gdi = GetGuiResources(h, 0); usr = GetGuiResources(h, 1); } finally { Native.CloseHandle(h); }
                        m[p.Id] = new[] { hc, gdi, usr };
                    }
                    catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                }
            return m;
        }
        public static List<string[]> Handles(Toolkit.Run run)
        {
            var rows = new List<string[]>(); var names = new Dictionary<int, string>();
            foreach (var p in Process.GetProcesses()) using (p) { try { names[p.Id] = p.ProcessName; } catch (InvalidOperationException) { } }
            run.Status = "Counting handles..."; var a = Counts();
            for (int i = 20; i > 0 && !run.Stop; i--) { run.Status = "Counting again in " + i + " s - to see who grows"; Thread.Sleep(1000); }
            var b = Counts(); string nm;
            Func<int, string> name = pid => (names.TryGetValue(pid, out nm) ? nm : "process") + " (" + pid + ")";
            var grow = b.Where(kv => a.ContainsKey(kv.Key)).Select(kv => Tuple.Create(kv.Key, kv.Value[0] - a[kv.Key][0], kv.Value[1] >= 0 && a[kv.Key][1] >= 0 ? kv.Value[1] - a[kv.Key][1] : 0)).Where(t => t.Item2 > 50 || t.Item3 > 20).OrderByDescending(t => t.Item2 + t.Item3 * 5).Take(6).ToList();
            rows.Add(R(grow.Count > 0 ? 1 : 0, "Growing in 20 s", grow.Count == 0 ? "nothing grew noticeably" : string.Join(" · ", grow.Select(t => name(t.Item1) + " +" + N(t.Item2) + " handles" + (t.Item3 > 0 ? ", +" + N(t.Item3) + " GDI" : ""))),
                grow.Count > 0 ? "Something that keeps growing for hours (and never falls back) is a leak. Run this again later: the same name growing again is the evidence." : "", "m"));
            foreach (var col in new[] { Tuple.Create(0, "Handles", 10000L), Tuple.Create(1, "GDI objects", 5000L), Tuple.Create(2, "USER objects", 5000L) })
            {
                var top = b.Where(kv => kv.Value[col.Item1] > 0).OrderByDescending(kv => kv.Value[col.Item1]).Take(5).ToList(); if (top.Count == 0) continue;
                bool hot = top[0].Value[col.Item1] > col.Item3;
                rows.Add(R(hot ? 1 : 3, "Most " + col.Item2, string.Join(" · ", top.Select(kv => name(kv.Key) + " " + N(kv.Value[col.Item1]))),
                    col.Item1 > 0 ? "A program dies or draws blank windows near 10,000 GDI or USER objects - Windows' limit per program." + (hot ? " The top one is getting close." : "") : hot ? "Tens of thousands of handles in one program is unusual - watch whether it keeps rising." : "", "m"));
            }
            int hidden = b.Count(kv => kv.Value[1] < 0);
            if (hidden > 0) rows.Add(R(3, "Not readable here", hidden + " programs run as another user or as Windows - their GDI and USER counts need admin", "", "u"));
            return rows;
        }

        // ---------------------------------------------------------------- environment variables and PATH
        static readonly string[] SecretWords = { "TOKEN", "SECRET", "PASSWORD", "PASSWD", "PWD", "KEY", "AUTH", "CREDENTIAL", "COOKIE" };
        internal static bool LooksSecret(string name) { string u = (name ?? "").ToUpperInvariant(); return u != "PWD" && SecretWords.Any(w => u.Contains(w)) && !u.EndsWith("PATH", StringComparison.Ordinal); }
        public static List<string[]> Env()
        {
            var rows = new List<string[]>();
            Func<EnvironmentVariableTarget, Dictionary<string, string>> get = t => { var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); try { foreach (DictionaryEntry e in Environment.GetEnvironmentVariables(t)) d[(string)e.Key] = Convert.ToString(e.Value); } catch (System.Security.SecurityException) { } return d; };
            var user = get(EnvironmentVariableTarget.User); var mach = get(EnvironmentVariableTarget.Machine); var proc = get(EnvironmentVariableTarget.Process);
            // PATH: system first, then yours - the order Windows searches
            int dead = 0, dup = 0; var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); string v;
            foreach (var src in new[] { Tuple.Create("System PATH", mach.TryGetValue("Path", out v) ? v : ""), Tuple.Create("Your PATH", user.TryGetValue("Path", out v) ? v : "") })
            {
                var parts = src.Item2.Split(';').Select(x => x.Trim()).Where(x => x != "").ToList(); if (parts.Count == 0) continue;
                rows.Add(R(src.Item2.Length > 2047 ? 1 : 3, src.Item1, parts.Count + " folders · " + N(src.Item2.Length) + " characters", src.Item2.Length > 2047 ? "Over 2,047 characters: some older installers and tools cut PATH there." : "", "w"));
                foreach (var p in parts)
                {
                    string ex = p; try { ex = Environment.ExpandEnvironmentVariables(p); } catch (ArgumentException) { }
                    bool there = false; try { there = Directory.Exists(ex); } catch (ArgumentException) { } catch (IOException) { }
                    bool twice = !seen.Add(ex.TrimEnd('\\'));
                    if (!there) dead++; if (twice) dup++;
                    rows.Add(R(!there ? 1 : twice ? 1 : 0, "  " + p, !there ? "folder doesn't exist" : twice ? "already listed above" : ex != p ? ex : "ok", "", "w"));
                }
            }
            rows.Insert(0, R(dead + dup > 0 ? 1 : 0, "PATH", dead + dup == 0 ? "every folder exists, none twice" : (dead > 0 ? dead + " missing folder" + (dead == 1 ? "" : "s") : "") + (dead > 0 && dup > 0 ? " · " : "") + (dup > 0 ? dup + " listed twice" : ""),
                dead + dup > 0 ? "Harmless mostly - leftovers of removed programs - but each one is searched every time a program looks for a file. Edit it in Settings → System → About → Advanced system settings → Environment Variables." : "", "w"));
            // the rest: yours, then the system's; values that look like secrets are hidden
            foreach (var t in new[] { Tuple.Create("Yours", user), Tuple.Create("System", mach) })
                foreach (var kv in t.Item2.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Where(x => !x.Key.Equals("Path", StringComparison.OrdinalIgnoreCase)))
                    rows.Add(R(3, t.Item1 + ": " + kv.Key, LooksSecret(kv.Key) ? "hidden here - the name looks like a secret" : Draw.Trunc(kv.Value, 120), "", "w"));
            // set since AdeeboMeter started: programs started before a change keep the old value until they restart
            var changed = user.Concat(mach).Where(kv => !kv.Key.Equals("Path", StringComparison.OrdinalIgnoreCase) && proc.ContainsKey(kv.Key) && Environment.ExpandEnvironmentVariables(kv.Value) != proc[kv.Key]).Select(kv => kv.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var added = user.Keys.Concat(mach.Keys).Where(k => !proc.ContainsKey(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (changed.Count + added.Count > 0) rows.Insert(1, R(1, "Changed since AdeeboMeter started", string.Join(", ", changed.Concat(added).Take(8)), "Programs that were already open still see the old value - restart them (or sign out and in) to pick up the change.", "w"));
            return rows;
        }

        // ---------------------------------------------------------------- PCIe links and the calculator
        internal static double LaneGBs(int gen) { switch (gen) { case 1: return 0.25; case 2: return 0.5; case 3: return 0.985; case 4: return 1.969; case 5: return 3.938; case 6: return 7.563; default: return 0; } }
        public static List<string[]> Pcie(string genIn, string lanesIn)
        {
            var rows = new List<string[]>(); int g, l;
            bool calc = int.TryParse((genIn ?? "").Trim().TrimEnd('.', '0').Trim('.'), out g) | int.TryParse((lanesIn ?? "").Trim().TrimStart('x', 'X'), out l);
            if (calc)
            {
                if (g < 1 || g > 6) g = 3; if (l != 1 && l != 2 && l != 4 && l != 8 && l != 16) l = 4;
                double gb = LaneGBs(g) * l;
                rows.Add(R(3, "PCIe " + g + ".0 x" + l, F(gb, "0.##") + " GB/s each way (" + F(gb * 8, "0.#") + " Gb/s)", "Real transfers land 5-15 % lower (packet overhead). Each generation doubles a lane; x16 is 16 lanes.", "i"));
                foreach (var x in new[] { Tuple.Create("SATA SSD", 0.55), Tuple.Create("PCIe 3.0 NVMe SSD", 3.5), Tuple.Create("PCIe 4.0 NVMe SSD", 7.0), Tuple.Create("PCIe 5.0 NVMe SSD", 12.0), Tuple.Create("10 Gb/s network card", 1.25), Tuple.Create("25 Gb/s network card", 3.1) })
                    rows.Add(R(gb + 0.01 >= x.Item2 ? 0 : 1, "  " + x.Item1, gb + 0.01 >= x.Item2 ? "fits (needs about " + F(x.Item2, "0.##") + " GB/s)" : "held back - needs about " + F(x.Item2, "0.##") + " GB/s", "", "i"));
            }
            try
            {
                var s = HwSpecs.Get(); int n = 0;
                foreach (var d in s.Disks.Where(d => d.MaxWidth > 0)) { n++; rows.Add(LinkRow("Drive: " + d.Name, d.LinkGen, d.LinkWidth, d.MaxGen, d.MaxWidth, false)); }
                foreach (var gp in s.Gpus.Where(x => x.MaxWidth > 0)) { n++; rows.Add(LinkRow("Graphics: " + gp.Name, gp.LinkGen, gp.LinkWidth, gp.MaxGen, gp.MaxWidth, true)); }
                if (n == 0) rows.Add(R(3, "This PC's links", "Windows didn't report a PCIe link for the drives or graphics here", "Type a generation and lane count above to use the calculator.", "u"));
            }
            catch (TimeoutException e) { rows.Add(R(3, "This PC's links", e.Message, "", "u")); }
            return rows;
        }
        static string[] LinkRow(string name, int gen, int w, int mg, int mw, bool gpu)
        {
            bool low = gen > 0 && w > 0 && (w < mw || gen < mg);
            return R(low ? 1 : 0, name, "PCIe " + gen + ".0 x" + w + " (" + F(LaneGBs(gen) * w, "0.#") + " GB/s) now · up to PCIe " + mg + ".0 x" + mw + " (" + F(LaneGBs(mg) * mw, "0.#") + " GB/s)",
                !low ? "" : gpu ? "Graphics cards drop to a slower link when idle to save power - check again during a game. Still narrow under load: it may sit in a slot with fewer lanes." : w < mw ? "Fewer lanes than it supports: it may be in a slot wired for fewer lanes, or sharing lanes with another slot (the motherboard manual has the table)." : "A slower generation than it supports: the slot or the CPU's lanes are older.", "w");
        }

        // ---------------------------------------------------------------- drive endurance
        public static List<string[]> Endurance(string tbwIn)
        {
            var rows = new List<string[]>(); double tbw; bool haveTbw = double.TryParse((tbwIn ?? "").Trim().Replace("TBW", "").Replace("tbw", ""), NumberStyles.Float, CultureInfo.CurrentCulture, out tbw) && tbw > 0;
            HwSpecs.Disk sd = null; try { sd = HwSpecs.SystemDisk(HwSpecs.Get()); } catch (TimeoutException) { }
            var wear = HwSpecs.Wear(sd != null && sd.DeviceId != "" ? sd.DeviceId : null);
            if (sd != null) rows.Add(R(3, "Windows drive", sd.Name + (sd.Media != "" ? " · " + sd.Media : ""), "", "w"));
            // writes since the last restart: Windows counts every byte written to each disk since boot
            double gbDay = double.NaN, days = double.NaN;
            try
            {
                double bytes; using (var pc = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true)) bytes = pc.RawValue;   // the raw value is every byte written since boot
                days = Math.Max(0.25, (DateTime.Now - Booted()).TotalDays);
                if (bytes > 0) gbDay = bytes / 1e9 / days;
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; }
            if (!double.IsNaN(gbDay)) rows.Add(R(gbDay > 100 ? 1 : 3, "Written lately", F(gbDay, "0.#") + " GB a day on average (all drives, since the restart " + F(days, "0.#") + " days ago)", gbDay > 100 ? "A lot for a home PC - a game library update, video work or a busy swap file can do it." : "", "m"));
            if (wear != null && wear[0] > 0)
            {
                double used = wear[0], poh = wear[1]; rows.Add(R(used > 80 ? 2 : used > 50 ? 1 : 0, "Life used", F(used, "0") + "% of its rated writes, by the drive's own count", "", "w"));
                if (poh > 0 && used < 100) { double hoursLeft = poh * (100 - used) / used; rows.Add(R(3, "At this pace", "about " + F(hoursLeft / 24 / 365, "0.#") + " more years of power-on time (" + N((long)poh) + " hours so far)", "An estimate: it assumes you keep writing the way you have so far. Most drives are replaced for other reasons first.", "i")); }
            }
            else rows.Add(R(3, "Life used", "the drive's own wear counter isn't readable here (Windows shows it only to admins, or the drive doesn't report it)", "", "u"));
            if (haveTbw && !double.IsNaN(gbDay) && gbDay > 0)
                rows.Add(R(3, "Rated " + F(tbw, "0") + " TBW", "about " + F(tbw * 1000 / gbDay / 365, "0") + " years at " + F(gbDay, "0.#") + " GB a day", "An estimate from your writes since the last restart and the rating you typed (TBW = terabytes written, from the spec sheet).", "i"));
            else if (!haveTbw) rows.Add(R(3, "Rated endurance", "type the drive's TBW from its spec sheet (e.g. 600) to see how many years that is at your pace", "", "i"));
            return rows;
        }
        static DateTime Booted() { try { using (var u = new PerformanceCounter("System", "System Up Time")) { u.NextValue(); return DateTime.Now.AddSeconds(-u.NextValue()); } } catch (Exception e) { if (e is OutOfMemoryException) throw; return DateTime.Now.AddMilliseconds(-(uint)Environment.TickCount); } }

        // ---------------------------------------------------------------- disk latency per disk, with stalls pinned
        public static List<string[]> DiskLatency(Toolkit.Run run)
        {
            var rows = new List<string[]>(); var cs = new List<PerformanceCounter[]>(); var names = new List<string>();
            try
            {
                foreach (var inst in new PerformanceCounterCategory("PhysicalDisk").GetInstanceNames().Where(i => i != "_Total").OrderBy(i => i))
                { names.Add(inst); cs.Add(new[] { new PerformanceCounter("PhysicalDisk", "Avg. Disk sec/Read", inst, true), new PerformanceCounter("PhysicalDisk", "Avg. Disk sec/Write", inst, true), new PerformanceCounter("PhysicalDisk", "Current Disk Queue Length", inst, true) }); }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; rows.Add(R(3, "Disks", "Windows' disk counters aren't readable here: " + Draw.Trunc(e.Message, 60), "", "u")); return rows; }
            if (cs.Count == 0) { rows.Add(R(3, "Disks", "no disks reported", "", "u")); return rows; }
            var rd = cs.Select(_ => new List<double>()).ToList(); var wr = cs.Select(_ => new List<double>()).ToList(); var qu = cs.Select(_ => new List<double>()).ToList(); var stalls = new List<string>();
            try
            {
                foreach (var c in cs) foreach (var k in c) k.NextValue();
                run.SeriesUnit = "ms";
                for (int s = 0; s < 30 && !run.Stop; s++)
                {
                    Thread.Sleep(1000); run.Status = "Watching the disks - " + (30 - s) + " s left (use the PC normally)"; double worst = 0;
                    for (int i = 0; i < cs.Count; i++)
                    {
                        double r = cs[i][0].NextValue() * 1000, w = cs[i][1].NextValue() * 1000, q = cs[i][2].NextValue(); rd[i].Add(r); wr[i].Add(w); qu[i].Add(q); worst = Math.Max(worst, Math.Max(r, w));
                        if (Math.Max(r, w) > 100) stalls.Add(DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + " " + names[i] + " " + F(Math.Max(r, w), "0") + " ms");
                    }
                    run.Add(worst);
                }
            }
            finally { foreach (var c in cs) foreach (var k in c) k.Dispose(); }
            for (int i = 0; i < cs.Count; i++)
            {
                Func<List<double>, double> p95 = l => { var o = l.Where(x => x > 0).OrderBy(x => x).ToList(); return o.Count == 0 ? 0 : o[(int)Math.Min(o.Count - 1, Math.Ceiling(o.Count * .95) - 1)]; };
                double r = p95(rd[i]), w = p95(wr[i]), q = qu[i].Count > 0 ? qu[i].Max() : 0, worst = Math.Max(r, w);
                rows.Add(R(worst > 100 ? 2 : worst > 25 ? 1 : 0, "Disk " + names[i], "reads " + F(r, "0.#") + " ms · writes " + F(w, "0.#") + " ms (95% of requests faster) · longest queue " + F(q, "0"),
                    worst > 100 ? "Requests waited over a tenth of a second - that's a freeze you feel. A hard disk under load, a failing drive or a full SSD do this." : worst > 25 ? "Busy: SSDs answer in under 5 ms, hard disks in 10-20 ms." : "", "m"));
            }
            rows.Insert(0, R(stalls.Count > 0 ? 2 : 0, "Stalls over 100 ms", stalls.Count == 0 ? "none in 30 s" : stalls.Count + " · " + string.Join(" · ", stalls.Take(4)), "", "m"));
            return rows;
        }

        // ---------------------------------------------------------------- monitor EDID (what each screen says about itself)
        internal sealed class Edid { public string Maker = "", Model = "", Name = ""; public int Product, Year, WidthCm, HeightCm, W, H; public double Hz, MinHz, MaxHz; public bool Hdr, Digital; public int Bits; }
        internal static Edid ParseEdid(byte[] e)
        {
            if (e == null || e.Length < 128 || e[0] != 0 || e[1] != 0xFF || e[7] != 0) return null;
            var d = new Edid(); int m = e[8] << 8 | e[9];
            d.Maker = new string(new[] { (char)('A' - 1 + (m >> 10 & 31)), (char)('A' - 1 + (m >> 5 & 31)), (char)('A' - 1 + (m & 31)) });
            d.Product = e[10] | e[11] << 8; d.Year = e[17] + 1990; d.WidthCm = e[21]; d.HeightCm = e[22]; d.Digital = (e[20] & 0x80) != 0;
            if (d.Digital && e[18] == 1 && e[19] >= 4) { int b = e[20] >> 4 & 7; d.Bits = b >= 1 && b <= 6 ? 4 + b * 2 : 0; }
            for (int o = 54; o < 126; o += 18)
            {
                int clk = e[o] | e[o + 1] << 8;
                if (clk != 0)
                {
                    if (d.W > 0) continue;   // the first timing is the native (preferred) mode
                    int ha = e[o + 2] | (e[o + 4] & 0xF0) << 4, hb = e[o + 3] | (e[o + 4] & 0x0F) << 8, va = e[o + 5] | (e[o + 7] & 0xF0) << 4, vb = e[o + 6] | (e[o + 7] & 0x0F) << 8;
                    d.W = ha; d.H = va; if ((ha + hb) * (va + vb) > 0) d.Hz = clk * 10000.0 / ((ha + hb) * (va + vb));
                }
                else
                {
                    int tag = e[o + 3]; string txt = Encoding.ASCII.GetString(e, o + 5, 13).Split('\n')[0].Trim();
                    if (tag == 0xFC) d.Name = txt; else if (tag == 0xFD) { d.MinHz = e[o + 5] + ((e[o + 4] & 1) != 0 ? 255 : 0); d.MaxHz = e[o + 6] + ((e[o + 4] & 2) != 0 ? 255 : 0); }
                }
            }
            // CTA-861 extensions: the HDR static metadata block (extended tag 6)
            for (int x = 128; x + 128 <= e.Length; x += 128)
            {
                if (e[x] != 0x02) continue; int end = Math.Min(x + 127, x + e[x + 2]);
                for (int p = x + 4; p < end && p < e.Length; ) { int len = e[p] & 31, tg = e[p] >> 5; if (tg == 7 && len >= 1 && p + 1 < e.Length && e[p + 1] == 6) d.Hdr = true; p += 1 + len; }
            }
            return d;
        }
        public static List<string[]> Edids()
        {
            var rows = new List<string[]>(); var live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { using (var s = new System.Management.ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_DesktopMonitor")) foreach (var o in s.Get()) using (o) { string id = Convert.ToString(o["PNPDeviceID"]); if (!string.IsNullOrEmpty(id)) live.Add(id); } }
            catch (Exception e) { if (e is OutOfMemoryException) throw; }
            try
            {
                using (var disp = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY"))
                {
                    if (disp == null) { rows.Add(R(3, "Screens", "not readable here", "", "u")); return rows; }
                    var seen = new HashSet<string>();
                    foreach (var model in disp.GetSubKeyNames())
                        using (var mk = disp.OpenSubKey(model))
                            foreach (var inst in mk == null ? new string[0] : mk.GetSubKeyNames())
                                using (var pk = mk.OpenSubKey(inst + @"\Device Parameters"))
                                {
                                    var raw = pk == null ? null : pk.GetValue("EDID") as byte[]; var d = ParseEdid(raw); if (d == null) continue;
                                    string id = @"DISPLAY\" + model + @"\" + inst; bool now = live.Contains(id);
                                    string sig = d.Maker + d.Product + d.W + d.H; if (!now && !seen.Add(sig)) continue; seen.Add(sig);
                                    double inch = Math.Sqrt(d.WidthCm * d.WidthCm + d.HeightCm * d.HeightCm) / 2.54;
                                    string title = (d.Name != "" ? d.Name : d.Maker + " " + d.Product.ToString("X4")) + (now ? "" : " (seen before)");
                                    rows.Add(R(now ? 0 : 3, title, string.Join(" · ", new[] {
                                        "maker " + d.Maker, d.W > 0 ? "native " + d.W + " × " + d.H + (d.Hz > 0 ? " at " + F(d.Hz, "0.##") + " Hz" : "") : null,
                                        d.MaxHz > 0 ? "refresh " + F(d.MinHz, "0") + "-" + F(d.MaxHz, "0") + " Hz" : null,
                                        inch > 5 ? F(inch, "0.#") + "\" (" + d.WidthCm + " × " + d.HeightCm + " cm)" : null,
                                        d.Bits > 0 ? d.Bits + "-bit colour" : null, d.Hdr ? "HDR" : null, d.Year > 1990 ? "made " + d.Year : null }.Where(x => x != null)),
                                        now && d.MaxHz > d.Hz + 1 && d.Hz > 0 ? "Its first listed mode is " + F(d.Hz, "0") + " Hz but it can go to " + F(d.MaxHz, "0") + " Hz - check Settings → Display → Advanced display that the higher rate is picked." : "", "w"));
                                }
                }
            }
            catch (System.Security.SecurityException) { rows.Add(R(3, "Screens", "not readable here", "", "u")); }
            if (rows.Count == 0) rows.Add(R(3, "Screens", "no EDID found - some laptop panels and remote sessions don't store one", "", "u"));
            return rows;
        }

        // ---------------------------------------------------------------- battery wear over time (the battery report's own history)
        internal static List<Tuple<DateTime, double, double>> CapacityHistory(string xml)
        {
            var o = new List<Tuple<DateTime, double, double>>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(xml ?? "", "<(?:Capacity)?HistoryEntry\\b([^>]*?)(?:/>|>(.*?)</(?:Capacity)?HistoryEntry>)", System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                // the report writes each entry's numbers as attributes; child elements are read too, in case a Windows version differs
                Func<string, string> at = n => { var mm = System.Text.RegularExpressions.Regex.Match(m.Groups[1].Value, "\\b" + n + "=\"([^\"]*)\""); if (mm.Success) return mm.Groups[1].Value; mm = System.Text.RegularExpressions.Regex.Match(m.Groups[2].Value, "<" + n + ">([^<]*)</" + n + ">"); return mm.Success ? mm.Groups[1].Value.Trim() : ""; };
                DateTime t; double full, des;
                string sd = at("StartDate"); if (sd == "") sd = at("LocalStartDate");
                if (DateTime.TryParse(sd, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out t) && double.TryParse(at("FullChargeCapacity"), NumberStyles.Float, CultureInfo.InvariantCulture, out full) && double.TryParse(at("DesignCapacity"), NumberStyles.Float, CultureInfo.InvariantCulture, out des) && des > 0 && full > 0)
                    o.Add(Tuple.Create(t, full, des));
            }
            return o;
        }
        internal static IEnumerable<string[]> WearRows(List<Tuple<DateTime, double, double>> h)
        {
            if (h.Count < 2) yield break;
            var first = h.First(); var last = h.Last(); double a = 100 * first.Item2 / first.Item3, b = 100 * last.Item2 / last.Item3, months = Math.Max(1, (last.Item1 - first.Item1).TotalDays / 30.4);
            yield return R(3, "Wear over time", F(a, "0") + "% in " + first.Item1.ToString("MMM yyyy", CultureInfo.CurrentCulture) + " → " + F(b, "0") + "% now · about " + F(Math.Max(0, a - b) / months, "0.#") + " points a month", "Windows' own weekly record. Batteries lose a few % a year normally; heat and sitting at 100% speed it up.", "w");
            foreach (var g in h.GroupBy(x => new DateTime(x.Item1.Year, x.Item1.Month < 7 ? 1 : 7, 1)).Select(g => g.Last()).Reverse().Take(6))
                yield return R(3, "  " + g.Item1.ToString("MMM yyyy", CultureInfo.CurrentCulture), F(100 * g.Item2 / g.Item3, "0") + "% (" + F(g.Item2 / 1000, "0.#") + " of " + F(g.Item3 / 1000, "0.#") + " Wh)", "", "w");
        }
    }
}
