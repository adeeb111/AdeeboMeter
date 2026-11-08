// AdeeboMeter 1.0 - a small, quiet Windows companion for "is it me or the network?".
// Created by Adeeb Khan. Single file, .NET Framework 4.x, C# 5: builds with the csc.exe that ships with Windows, no admin.
// No cloud. No telemetry. Never listens. Every packet it sends is listed in About - and you can switch each one off.
// (It never listens on any port, never updates itself, never downloads anything.)
// The only packets it sends (Balanced, the default - Settings changes the pace): a ping to your gateway every 5 s, a ping to 1.1.1.1
// and one DNS lookup every 30 s (every 2 s only while the engineer view is open or a Teams call is running), and whatever you ask
// Check a destination to test - plus, only when such a check fails, Windows' own captive-portal probe (msftconnecttest.com).
// Everything it keeps (settings, log) lives next to the exe; captures go to %LOCALAPPDATA%\AdeeboMeter and are deleted on exit.

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

// System DLLs (iphlpapi, dnsapi, dwmapi...) load from System32 only - never from the exe's own folder (DLL planting).
// These three suggest APIs that .NET Framework 4.x (what Windows' own csc.exe builds against) does not have.
[assembly: SuppressMessage("Usage", "CA2249", Scope = "module", Justification = ".NET Framework has no string.Contains(string, StringComparison)")]
[assembly: SuppressMessage("Performance", "CA1847", Scope = "module", Justification = ".NET Framework has no string.Contains(char)")]
[assembly: SuppressMessage("Performance", "CA1846", Scope = "module", Justification = ".NET Framework has no AsSpan")]
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace AdeeboMeter
{
    // ================================================================== entry
    static class Program
    {
        public static string Dir;
        public const string Version = "1.0";
        public const string Tagline = "is it me or the network?";
        public static int ForceTheme = -1;
        public static string StartView, StartTip, StartDest;   // debug: --view <page> [--tip <key>] [--dest <host>] opens the engineer view there (used for screenshots)   // -1 follow Windows, 0 light, 1 dark
        public static bool WantDark() { return ForceTheme >= 0 ? ForceTheme == 1 : Theme.SystemDark(); }

        [STAThread]
        static int Main(string[] args)
        {
            Dir = Path.GetDirectoryName(Application.ExecutablePath);
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log("UI error: " + e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log("Fatal: " + e.ExceptionObject); };

            if (args.Any(a => string.Equals(a, "--dark", StringComparison.OrdinalIgnoreCase))) ForceTheme = 1;
            if (args.Any(a => string.Equals(a, "--light", StringComparison.OrdinalIgnoreCase))) ForceTheme = 0;
            if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))) return SelfTest.Run();
            int vi = Array.FindIndex(args, a => a == "--view"); if (vi >= 0 && vi + 1 < args.Length) StartView = args[vi + 1];
            int tpi = Array.FindIndex(args, a => a == "--tip"); if (tpi >= 0 && tpi + 1 < args.Length) StartTip = args[tpi + 1];
            int dsi = Array.FindIndex(args, a => a == "--dest"); if (dsi >= 0 && dsi + 1 < args.Length) StartDest = args[dsi + 1];
            int ci = Array.FindIndex(args, a => string.Equals(a, "--check", StringComparison.OrdinalIgnoreCase));
            if (ci >= 0 && ci + 1 < args.Length) return SelfTest.CheckOnce(args[ci + 1]);
            int pi = Array.FindIndex(args, a => string.Equals(a, "--parse-test", StringComparison.OrdinalIgnoreCase));
            if (pi >= 0 && pi + 1 < args.Length) return SelfTest.ParseTest(args[pi + 1]);
            int rf = Array.FindIndex(args, a => a == "--remove-folder"); if (rf >= 0 && rf + 1 < args.Length) return Setup.RemoveFolder(args[rf + 1]);
            int wi = Array.FindIndex(args, a => a == "--write-icon"); if (wi >= 0 && wi + 1 < args.Length) { File.WriteAllBytes(args[wi + 1], Theme.IcoBytes()); return 0; }

            // A downloaded copy offers to install itself; --uninstall comes from Settings > Apps or the tray menu.
            bool uninstall = args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase));
            if (uninstall || ((args.Length == 0 || args.Any(a => a == "--offer")) && Setup.ShouldOffer()) || args.Any(a => a == "--offer"))
            {
                bool here;
                using (var w = new SetupWindow(uninstall)) { Application.Run(w); here = w.RunHere; }
                if (!here) return 0;
            }

            bool created;
            var mtx = new Mutex(true, "Local\\AdeeboMeter.single", out created);
            if (!created)
            {
                if (Single.AskRunningToOpen()) return 0;
                var r = MessageBox.Show("AdeeboMeter is already running but isn't answering.\n\nClose the stuck one and start fresh?",
                    "AdeeboMeter", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return 0;
                Single.KillOthers();
                try { if (!mtx.WaitOne(5000)) return 0; } catch (AbandonedMutexException) { }
            }
            Log("Start " + Version);
            Application.Run(new AppCtx());
            Log("Exit");
            GC.KeepAlive(mtx);
            return 0;
        }

        static readonly object logLock = new object();
        public static void Log(string m)
        {
            try
            {
                lock (logLock)
                {
                    string p = Path.Combine(Dir, "AdeeboMeter.log");
                    if (File.Exists(p) && new FileInfo(p).Length > 256 * 1024) File.Delete(p);
                    File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture) + "  " + m + Environment.NewLine);
                }
            }
            catch { }
        }

        static string DataDir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdeeboMeter");
                try { Directory.CreateDirectory(d); } catch { }
                return d;
            }
        }
        public static string CaptureDir { get { string d = Path.Combine(DataDir, "captures"); try { Directory.CreateDirectory(d); } catch { } return d; } }
        // Every capture path must resolve inside AdeeboMeter's own capture folder (guards against path traversal).
        public static string SafeCapturePath(string fileName)
        {
            string root = Path.GetFullPath(CaptureDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(root, fileName));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("capture path outside the capture folder");
            return full;
        }
        public static void CleanCaptures()
        {
            try { foreach (var f in Directory.GetFiles(CaptureDir, "*.pcapng")) try { File.Delete(f); } catch { } } catch { }
        }
    }

    // Second launch asks the running copy to open its engineer view; a stuck copy can be replaced.
    // ================================================================== Settings the user controls (Settings page / tray). 0 seconds = off.
    static class Cfg
    {
        // background work while only the widget runs
        public static volatile int GwSec = 5, NetSec = 30, WifiSec = 30, AppsSec = 10;
        public static volatile bool Call = true;
        // engineer view (only while it's open)
        public static volatile bool IpNames = true, Portal = true, SeenTip, History;
        public static volatile int LiveMin = 15, SniffSec = 30;
        // widget
        public static volatile int Fade = 77, DwellMs = 450;
        public static string Preset = "balanced";
        public static readonly List<string> Pins = new List<string>();   // saved destinations, max 5, UI thread only
        public static Action Save = delegate { };
        public static Action<int> SetTheme = delegate { }; public static Func<int> GetTheme = () => -1;
        public static Action<bool> SetAutoStart = delegate { }; public static Func<bool> GetAutoStart = () => false;

        public static void ApplyPreset(string p)
        {
            Preset = p;
            if (p == "penny") { GwSec = 10; NetSec = 60; WifiSec = 120; AppsSec = 30; Call = false; }
            else if (p == "engineer") { GwSec = 2; NetSec = 15; WifiSec = 10; AppsSec = 5; Call = true; }
            else { Preset = "balanced"; GwSec = 5; NetSec = 30; WifiSec = 30; AppsSec = 10; Call = true; }
        }
        // background checks per minute, on top of the once-a-second counter read the widget always does
        public static int JobsPerMinute()
        {
            Func<int, double> per = delegate(int sec) { return sec > 0 ? 60.0 / sec : 0; };
            return (int)Math.Round(6 /*adapters*/ + per(GwSec) + 2 * per(NetSec) + per(WifiSec) + 2 * per(AppsSec));
        }
        public static IEnumerable<string> Lines()
        {
            yield return "preset=" + Preset;
            yield return "gw=" + GwSec; yield return "net=" + NetSec; yield return "wifi=" + WifiSec; yield return "apps=" + AppsSec;
            yield return "call=" + (Call ? 1 : 0); yield return "ipnames=" + (IpNames ? 1 : 0); yield return "portal=" + (Portal ? 1 : 0); yield return "seentip=" + (SeenTip ? 1 : 0); yield return "history=" + (History ? 1 : 0);
            yield return "livemin=" + LiveMin; yield return "sniffsec=" + SniffSec; yield return "fade=" + Fade; yield return "dwell=" + DwellMs;
            foreach (var p in Pins) yield return "pin=" + p;
        }
        static int Pick(string v, int def, params int[] allowed) { int x; return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out x) && allowed.Contains(x) ? x : def; }
        public static void Read(string k, string v)
        {
            switch (k)
            {
                case "preset": Preset = v == "penny" || v == "engineer" || v == "custom" ? v : "balanced"; break;
                case "gw": GwSec = Pick(v, 5, 2, 5, 10, 0); break;
                case "net": NetSec = Pick(v, 30, 15, 30, 60, 0); break;
                case "wifi": WifiSec = Pick(v, 30, 10, 30, 120, 0); break;
                case "apps": AppsSec = Pick(v, 10, 5, 10, 30, 0); break;
                case "call": Call = v == "1"; break;
                case "ipnames": IpNames = v == "1"; break;
                case "portal": Portal = v != "0"; break;
                case "seentip": SeenTip = v == "1"; break;
                case "history": History = v == "1"; break;
                case "livemin": LiveMin = Pick(v, 15, 5, 15, 30); break;
                case "sniffsec": SniffSec = Pick(v, 30, 30, 60); break;
                case "fade": Fade = Pick(v, 77, 51, 77, 128); break;
                case "dwell": DwellMs = Pick(v, 450, 300, 450, 800); break;
                case "pin":
                    string h; int pt; bool t; string e;
                    if (Pins.Count < 5 && DestCheck.Parse(v, out h, out pt, out t, out e) && !Pins.Contains(v, StringComparer.OrdinalIgnoreCase)) Pins.Add(v.Trim());
                    break;
            }
        }
    }

    static class Single
    {
        public const string OpenName = "Local\\AdeeboMeter.open", AckName = "Local\\AdeeboMeter.ack", QuitName = "Local\\AdeeboMeter.quit";
        public static bool AskRunningToOpen()
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using (var o = EventWaitHandle.OpenExisting(OpenName))
                    using (var a = EventWaitHandle.OpenExisting(AckName))
                    {
                        a.Reset(); o.Set();
                        return a.WaitOne(2000);
                    }
                }
                catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(700); }
                catch { return false; }
            }
            return false;
        }
        public static void KillOthers()
        {
            int me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName("AdeeboMeter"))
            {
                try { if (p.Id != me) { p.Kill(); p.WaitForExit(3000); } } catch { } finally { p.Dispose(); }
            }
        }
    }

    // Child programs (netsh, dumpcap) live in a job that Windows closes with us - even after a crash or a kill.
    static class Job
    {
        static IntPtr h = IntPtr.Zero;
        static readonly object k = new object();
        public static void Add(Process p)
        {
            try
            {
                lock (k)
                {
                    if (h == IntPtr.Zero)
                    {
                        h = Native.CreateJobObject(IntPtr.Zero, null);
                        var info = new Native.JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                        info.BasicLimitInformation.LimitFlags = 0x2000; // KILL_ON_JOB_CLOSE
                        int len = Marshal.SizeOf(typeof(Native.JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                        IntPtr buf = Marshal.AllocHGlobal(len);
                        try { Marshal.StructureToPtr(info, buf, false); Native.SetInformationJobObject(h, 9, buf, (uint)len); }
                        finally { Marshal.FreeHGlobal(buf); }
                    }
                }
                Native.AssignProcessToJobObject(h, p.Handle);
            }
            catch { }
        }

        // Full path, so a netsh.exe planted somewhere on PATH can never be picked up instead.
        public static readonly string Netsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "netsh.exe");

        // Runs a fixed program with fixed arguments (never through cmd or PowerShell) and returns its output.
        public static string Run(string exe, string args, int timeoutMs)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true; psi.CreateNoWindow = true;
            using (var p = Process.Start(psi))
            {
                Add(p);
                var t = p.StandardOutput.ReadToEndAsync();
                p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return ""; }
                return t.Wait(1000) ? t.Result : "";
            }
        }
    }

    // The companion voice. First line may be light; the fix is always plain. Serious mode drops the jokes.
    static class Voice
    {
        public static volatile bool Serious;
        public static string P(string fun, string plain) { return Serious ? plain : fun; }
        public static string Greet()
        {
            int h = DateTime.Now.Hour;
            return h < 5 ? "Still up, boss?" : h < 12 ? "Good morning, boss." : h < 17 ? "Good afternoon, boss." : "Good evening, boss.";
        }
    }

    // ================================================================== Win32
    static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
        [DllImport("kernel32.dll")] public static extern ulong GetTickCount64();
        [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] public static extern bool SetProcessWorkingSetSize(IntPtr p, IntPtr min, IntPtr max);

        [StructLayout(LayoutKind.Sequential)]
        public sealed class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }
        [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX b);

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }
        [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int CX, CY; public SIZE(int x, int y) { CX = x; CY = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int idx);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, [Out] char[] s, int n);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);

        [DllImport("iphlpapi.dll")] public static extern uint GetExtendedTcpTable(IntPtr t, ref int len, bool sort, int af, int cls, uint res);
        [DllImport("iphlpapi.dll")] public static extern uint GetExtendedUdpTable(IntPtr t, ref int len, bool sort, int af, int cls, uint res);

        [DllImport("dnsapi.dll", EntryPoint = "DnsQuery_W", CharSet = CharSet.Unicode)] public static extern int DnsQuery(string name, short type, int options, IntPtr extra, out IntPtr results, IntPtr reserved);
        [DllImport("dnsapi.dll")] public static extern void DnsRecordListFree(IntPtr list, int freeType);
        [DllImport("dnsapi.dll")] public static extern bool DnsGetCacheDataTable(out IntPtr table);
        [DllImport("dnsapi.dll")] public static extern void DnsFree(IntPtr p, int freeType);

        [DllImport("user32.dll")] public static extern int GetGuiResources(IntPtr proc, int flags);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr h, string app, string idList);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(int access, bool inherit, int pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool QueryFullProcessImageName(IntPtr h, int flags, [Out] char[] name, ref int size);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool CloseHandle(IntPtr h);
        // Full path of a process's exe without admin (PROCESS_QUERY_LIMITED_INFORMATION); "" when Windows says no.
        public static string ExePath(int pid)
        {
            if (pid <= 4) return "";
            IntPtr h = OpenProcess(0x1000, false, pid); if (h == IntPtr.Zero) return "";
            try { var buf = new char[1024]; int n = buf.Length; return QueryFullProcessImageName(h, 0, buf, ref n) ? new string(buf, 0, n) : ""; }
            finally { CloseHandle(h); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateJobObject(IntPtr attrs, string name);
        [DllImport("kernel32.dll")] public static extern bool SetInformationJobObject(IntPtr job, int cls, IntPtr info, uint len);
        [DllImport("kernel32.dll")] public static extern bool AssignProcessToJobObject(IntPtr job, IntPtr proc);
        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit;
            public UIntPtr Affinity; public uint PriorityClass, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)] public struct IO_COUNTERS { public ulong R, W, O, RB, WB, OB; }
        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }
    }

    // ================================================================== small data types
    sealed class Ring
    {
        readonly double[] a; int n, head; readonly object k = new object();
        public Ring(int size) { a = new double[size]; }
        public void Add(double v) { lock (k) { a[head] = v; head = (head + 1) % a.Length; if (n < a.Length) n++; } }
        double At(int back) { return a[((head - 1 - back) % a.Length + a.Length) % a.Length]; }
        public double Last { get { lock (k) return n == 0 ? double.NaN : At(0); } }
        public double[] Values(int max)
        {
            lock (k)
            {
                int c = Math.Min(n, max); double[] r = new double[c];
                for (int i = 0; i < c; i++) r[i] = At(c - 1 - i);
                return r;
            }
        }
        public double Avg(int last)
        {
            lock (k)
            {
                int m = Math.Min(last, n), c = 0; double s = 0;
                for (int i = 0; i < m; i++) { double v = At(i); if (!double.IsNaN(v)) { s += v; c++; } }
                return c == 0 ? double.NaN : s / c;
            }
        }
        public double Max(int last)
        {
            lock (k)
            {
                int m = Math.Min(last, n); double r = double.NaN;
                for (int i = 0; i < m; i++) { double v = At(i); if (!double.IsNaN(v) && (double.IsNaN(r) || v > r)) r = v; }
                return r;
            }
        }
        public double Sum(int last)
        {
            lock (k)
            {
                int m = Math.Min(last, n); double s = 0;
                for (int i = 0; i < m; i++) { double v = At(i); if (!double.IsNaN(v)) s += v; }
                return s;
            }
        }
        public double Jitter(int last)
        {
            lock (k)
            {
                int m = Math.Min(last, n), c = 0; double s = 0, prev = double.NaN;
                for (int i = m - 1; i >= 0; i--)
                {
                    double v = At(i); if (double.IsNaN(v)) continue;
                    if (!double.IsNaN(prev)) { s += Math.Abs(v - prev); c++; }
                    prev = v;
                }
                return c == 0 ? double.NaN : s / c;
            }
        }
    }

    sealed class AppStat { public string Name; public double Cpu; public long Mem; public int Count, Pid; }

    sealed class Conn
    {
        public string Key, Proc, Proto, Local, Remote, State, RemoteIp, Host = "", HostFrom = "";
        public int Pid, LocalPort, RemotePort;
    }

    sealed class Detail
    {
        public string Title = "", Note = "", TopTitle = "";
        public List<string[]> Lines = new List<string[]>(), Top = new List<string[]>();
    }

    // A small "what changed" log: only changes are recorded, kept for 2 hours (max 300 lines), in memory only.
    sealed class Timeline
    {
        public sealed class Ev { public DateTime T; public int Lv; public string Kind, Text; }
        readonly List<Ev> list = new List<Ev>(); readonly object k = new object();
        public void Add(int lv, string kind, string text)
        {
            lock (k)
            {
                var now = DateTime.Now;
                if (list.Count > 0 && list[list.Count - 1].Text == text && (now - list[list.Count - 1].T).TotalSeconds < 120) return;
                list.Add(new Ev { T = now, Lv = lv, Kind = kind, Text = text });
                History.Event(lv, kind, text);
                var cut = now.AddHours(-2); list.RemoveAll(e => e.T < cut);
                if (list.Count > 300) list.RemoveRange(0, list.Count - 300);
            }
        }
        public List<Ev> Recent(int n) { lock (k) { var r = list.Skip(Math.Max(0, list.Count - n)).ToList(); r.Reverse(); return r; } }
        public static string Ago(DateTime t)
        {
            var d = DateTime.Now - t;
            return d.TotalSeconds < 60 ? ((int)d.TotalSeconds) + " s" : d.TotalMinutes < 60 ? ((int)d.TotalMinutes) + " min" : ((int)d.TotalHours) + " h " + d.Minutes + " min";
        }
    }

    sealed class Snap
    {
        public string Hint = "";
        public List<Timeline.Ev> Recent = new List<Timeline.Ev>();
        public int Level; public string Headline = "Waking up... give me ten seconds, boss.", Chip = "All quiet"; public int Countdown;
        public int[] Path = { 3, 3, 3, 3, 3 };
        public string[] PathLabel = { "PC", "LAN", "ISP", "DNS", "Internet" }, PathMain = new string[5], PathSub = new string[5];
        public double Cpu = double.NaN, Mem = double.NaN, Disk = double.NaN, Down = double.NaN, Up = double.NaN, Ping, Jitter, LossPct, DnsMs;
        public double[] DownHist = Array.Empty<double>(), UpHist = Array.Empty<double>();
        public bool Call;
        public string MemText = "", DiskText = "";
        public Dictionary<string, Detail> Details = new Dictionary<string, Detail>();
        public List<string[]> Health = new List<string[]>();
        public List<double[]> HealthHist = new List<double[]>();
        public List<AppStat> TopMem = new List<AppStat>(), TopCpu = new List<AppStat>();
    }

    // ================================================================== Monitor: all reading happens here, off the UI thread
    sealed class Monitor : IDisposable
    {
        public void Dispose() { Stop(); }   // the sampler/probe threads release their own counters and event as they exit
        public readonly object L = new object();
        public Ring Cpu = new Ring(120), Mem = new Ring(120), Commit = new Ring(120), Disk = new Ring(120);
        public Ring Down = new Ring(120), Up = new Ring(120);
        public Ring GwPing = new Ring(30), NetPing = new Ring(30), Loss = new Ring(20), Dns = new Ring(12);
        public Ring Retx = new Ring(120), Rst = new Ring(120), Fail = new Ring(120), Unreach = new Ring(120), UdpDrop = new Ring(120);

        public int Cores = Environment.ProcessorCount;
        public double TotalGB, UsedGB, CommitLimitGB, CommitUsedGB;
        public double DiskReadMBs = double.NaN, DiskWriteMBs = double.NaN;
        public string CFree = "";
        public NetworkInterface Primary;
        public string Adapter = "", AdapterKind = "LAN", LinkSpeed = "", Gateway = "", DnsServer = "", IfaceId = "";
        public bool VpnUp; public string VpnName = ""; public int VpnMtu, PrimaryMtu;
        public bool HasWifi; public int WifiSignal = -1; public string Ssid = "", WifiChannel = "";
        public bool GwEverReplied, NetEverReplied, DnsFailedLast;
        public string NetTarget = "1.1.1.1";
        public double DownBytesTotal;
        public List<AppStat> Apps = new List<AppStat>();
        public Dictionary<int, string> PidNames = new Dictionary<int, string>();
        public Dictionary<string, string> IpNames = new Dictionary<string, string>();
        public bool CallLikely;
        public string LocalIp = "", Bssid = "", WifiState = ""; public int RxRate = -1;
        public readonly Timeline Events = new Timeline();

        // change tracking for the timeline (probe / sampler threads only)
        string pAdapter, pGw, pDns, pIp, pState, pBssid = "", pCh = ""; bool pVpn; int pRate = -1, pSig = -1;
        DateTime lastRoam = DateTime.MinValue, lastSigDrop = DateTime.MinValue, netDownSince, lastConnT = DateTime.UtcNow;
        int netDown, prevLevel = -1; bool gwFlag, prevCall; double gwBase = double.NaN; string hint = "";
        HashSet<string> prevKeys; readonly Dictionary<string, DateTime> synFirst = new Dictionary<string, DateTime>();
        readonly Dictionary<string, DateTime> suppress = new Dictionary<string, DateTime>();
        volatile bool netChanged;
        NetworkAddressChangedEventHandler addrHandler; NetworkAvailabilityChangedEventHandler availHandler;

        public volatile bool ViewOpen, ProcsTab;   // ProcsTab: the What's running tab is showing - keep the process list at 2 s       // engineer view open: probe faster, read processes more often
        public volatile Snap Latest = new Snap();
        public int Version;
        int countdown = 3; int level; string headline = "Waking up... give me ten seconds, boss.";
        int[] path = { 3, 3, 3, 3, 3 };

        long pIdle, pKern, pUser; bool cpuInit;
        long pRx = -1, pTx = -1; DateTime pNetT;
        long pRetx = -1, pRst, pFail, pUnr, pUdp;
        PerformanceCounter pcIdle, pcRead, pcWrite;
        volatile bool stop;
        readonly AutoResetEvent wake = new AutoResetEvent(false);

        public void Start()
        {
            Events.Add(3, "app", "AdeeboMeter started");
            addrHandler = delegate { netChanged = true; wake.Set(); };
            availHandler = delegate(object o, NetworkAvailabilityEventArgs e) { Events.Add(e.IsAvailable ? 0 : 2, "net", e.IsAvailable ? "Network is back" : Voice.P("Every network connection just dropped.", "All network connections dropped.")); netChanged = true; wake.Set(); };
            try { NetworkChange.NetworkAddressChanged += addrHandler; NetworkChange.NetworkAvailabilityChanged += availHandler; } catch { }
            RefreshInterfaces();
            var s = new Thread(SamplerLoop); s.IsBackground = true; s.Name = "sampler"; s.Priority = ThreadPriority.BelowNormal; s.Start();
            var p = new Thread(ProbeLoop); p.IsBackground = true; p.Name = "probe"; p.Priority = ThreadPriority.BelowNormal; p.Start();
        }
        public void Stop()
        {
            stop = true; wake.Set();
            try { NetworkChange.NetworkAddressChanged -= addrHandler; NetworkChange.NetworkAvailabilityChanged -= availHandler; } catch { }
        }
        void Once(string key, int seconds, int lv, string kind, string text)
        {
            DateTime t;
            lock (suppress)
            {
                if (suppress.TryGetValue(key, out t) && (DateTime.Now - t).TotalSeconds < seconds) return;
                if (suppress.Count > 500) suppress.Clear();
                suppress[key] = DateTime.Now;
            }
            Events.Add(lv, kind, text);
        }
        public void Nudge() { wake.Set(); }

        // ---------- sampler: every second
        void SamplerLoop()
        {
            try
            {
                pcIdle = new PerformanceCounter("PhysicalDisk", "% Idle Time", "_Total");
                pcRead = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                pcWrite = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
                pcIdle.NextValue(); pcRead.NextValue(); pcWrite.NextValue();
            }
            catch (Exception e) { pcIdle = null; Program.Log("Disk counters unavailable: " + e.Message); }
            while (!stop)
            {
                var t0 = DateTime.UtcNow;
                try
                {
                    Tick();
                    if (--countdown <= 0)
                    {
                        Judge(); countdown = 10;
                        if (Cfg.History) { int sig, lvl; lock (L) { sig = WifiSignal; lvl = level; } History.Sample(lvl, Cpu.Last, Mem.Last, Down.Last, GwPing.Last, NetPing.Last, sig); }
                    }
                    Latest = BuildSnap();
                    Interlocked.Increment(ref Version);
                }
                catch (Exception e) { Program.Log("sampler: " + e.Message); }
                int wait = 1000 - (int)(DateTime.UtcNow - t0).TotalMilliseconds;
                if (wait > 0) Thread.Sleep(wait);
            }
            foreach (var pc in new[] { pcIdle, pcRead, pcWrite }) if (pc != null) try { pc.Dispose(); } catch { }
        }

        public void Tick()
        {
            long idle, kern, user;
            if (Native.GetSystemTimes(out idle, out kern, out user))
            {
                if (cpuInit) { long di = idle - pIdle, tot = (kern - pKern) + (user - pUser); Cpu.Add(tot > 0 ? Clamp((tot - di) * 100.0 / tot) : 0); }
                pIdle = idle; pKern = kern; pUser = user; cpuInit = true;
            }
            var ms = new Native.MEMORYSTATUSEX();
            if (Native.GlobalMemoryStatusEx(ms))
            {
                TotalGB = ms.ullTotalPhys / 1073741824.0; UsedGB = (ms.ullTotalPhys - ms.ullAvailPhys) / 1073741824.0;
                CommitLimitGB = ms.ullTotalPageFile / 1073741824.0; CommitUsedGB = (ms.ullTotalPageFile - ms.ullAvailPageFile) / 1073741824.0;
                Mem.Add(ms.dwMemoryLoad); Commit.Add(CommitLimitGB > 0 ? CommitUsedGB * 100 / CommitLimitGB : double.NaN);
            }
            try
            {
                if (pcIdle != null) { Disk.Add(Clamp(100 - pcIdle.NextValue())); DiskReadMBs = pcRead.NextValue() / 1048576.0; DiskWriteMBs = pcWrite.NextValue() / 1048576.0; }
                else Disk.Add(double.NaN);
            }
            catch { Disk.Add(double.NaN); }
            NetworkInterface ni; lock (L) ni = Primary;
            try
            {
                if (ni != null)
                {
                    var st = ni.GetIPStatistics(); long rx = st.BytesReceived, tx = st.BytesSent; DateTime now = DateTime.UtcNow;
                    if (pRx >= 0 && rx >= pRx && tx >= pTx)
                    {
                        double sec = Math.Max(0.2, (now - pNetT).TotalSeconds);
                        Down.Add((rx - pRx) * 8 / sec / 1e6); Up.Add((tx - pTx) * 8 / sec / 1e6); DownBytesTotal += rx - pRx;
                    }
                    pRx = rx; pTx = tx; pNetT = now;
                }
            }
            catch { pRx = -1; }
            try
            {
                var gp = IPGlobalProperties.GetIPGlobalProperties();
                var t4 = gp.GetTcpIPv4Statistics();
                long retx = t4.SegmentsResent, rst = t4.ResetsSent, fail = t4.FailedConnectionAttempts;
                try { var t6 = gp.GetTcpIPv6Statistics(); retx += t6.SegmentsResent; rst += t6.ResetsSent; fail += t6.FailedConnectionAttempts; } catch { }
                long unr = gp.GetIcmpV4Statistics().DestinationUnreachableMessagesReceived;
                long udp = gp.GetUdpIPv4Statistics().IncomingDatagramsDiscarded;
                if (pRetx >= 0) { Retx.Add(Math.Max(0, retx - pRetx)); Rst.Add(Math.Max(0, rst - pRst)); Fail.Add(Math.Max(0, fail - pFail)); Unreach.Add(Math.Max(0, unr - pUnr)); UdpDrop.Add(Math.Max(0, udp - pUdp)); }
                pRetx = retx; pRst = rst; pFail = fail; pUnr = unr; pUdp = udp;
            }
            catch { }
        }
        static double Clamp(double v) { return v < 0 ? 0 : v > 100 ? 100 : v; }

        // ---------- probe: pings, DNS, Wi-Fi, processes, call detection
        void ProbeLoop()
        {
            int netFails = 0, ti = 0; string[] targets = { "1.1.1.1", "8.8.8.8" };
            var lastCpu = new Dictionary<int, TimeSpan>(); DateTime lastProcT = DateTime.UtcNow, lastNet = DateTime.MinValue, lastProc = DateTime.MinValue, lastWifi = DateTime.MinValue, lastIf = DateTime.UtcNow, lastGw = DateTime.MinValue;
            while (!stop)
            {
                DateTime t0 = DateTime.UtcNow;
                try
                {
                    bool fast = ViewOpen || CallLikely;
                    // Every interval comes from Settings; the open engineer view (or a call) always gets the fast pace.
                    int gwSec = ViewOpen ? 2 : Cfg.GwSec, netSec = fast ? 2 : Cfg.NetSec, wifiSec = ViewOpen ? 10 : Cfg.WifiSec, appsSec = ViewOpen ? 2 : Cfg.AppsSec, procSec = ViewOpen ? (ProcsTab ? 2 : 4) : Cfg.AppsSec;
                    if (gwSec > 0 && (t0 - lastGw).TotalSeconds >= gwSec - 0.2)
                    {
                        lastGw = t0;
                        string gw; lock (L) gw = Gateway;
                        double g = PingMs(gw);
                        lock (L) { GwPing.Add(g); if (!double.IsNaN(g)) GwEverReplied = true; }
                    }
                    if (netSec > 0 && (t0 - lastNet).TotalSeconds >= netSec - 0.2)
                    {
                        lastNet = t0;
                        string tgt; lock (L) tgt = NetTarget;
                        double n = PingMs(tgt);
                        lock (L) { NetPing.Add(n); if (!double.IsNaN(n)) NetEverReplied = true; if (NetEverReplied) Loss.Add(double.IsNaN(n) ? 1 : 0); }
                        if (NetEverReplied)
                        {
                            if (double.IsNaN(n)) { if (++netDown == 3) { netDownSince = DateTime.Now; Events.Add(2, "inet", Voice.P("The internet went quiet - no replies from " + tgt + ".", "Internet unreachable (no reply from " + tgt + ").")); } }
                            else { if (netDown >= 3) Events.Add(0, "inet", "Internet back after " + Timeline.Ago(netDownSince)); netDown = 0; }
                        }
                        if (!NetEverReplied && double.IsNaN(n) && ++netFails >= 2) { netFails = 0; ti = (ti + 1) % targets.Length; lock (L) NetTarget = targets[ti]; }
                        ProbeDns();
                    }
                    if (netChanged) { netChanged = false; lastIf = t0; lastWifi = t0; RefreshInterfaces(); if (wifiSec > 0) ProbeWifi(); }
                    if ((t0 - lastIf).TotalSeconds >= 10) { lastIf = t0; RefreshInterfaces(); WatchGatewayMac(); LearnIsp(); }
                    if (wifiSec > 0 && (t0 - lastWifi).TotalSeconds >= wifiSec - 0.2) { lastWifi = t0; ProbeWifi(); }
                    if (appsSec > 0 && (t0 - lastConnT).TotalSeconds >= appsSec - 0.2) ProbeConns();
                    if (procSec > 0 && (t0 - lastProc).TotalSeconds >= procSec - 0.2) { lastProc = t0; ProbeProcesses(lastCpu, ref lastProcT); }
                    if (Cfg.Call || ViewOpen) ProbeCall(); else lock (L) CallLikely = false;
                }
                catch (Exception e) { Program.Log("probe: " + e.Message); }
                // Sleep until the next job is due (2 s with the view open; up to 10 s in Penny mode).
                int pace = ViewOpen || CallLikely ? 2 : new[] { Cfg.GwSec, Cfg.NetSec, Cfg.WifiSec, Cfg.AppsSec, 10 }.Where(v => v > 0).Min();
                int wait = Math.Max(2, pace) * 1000 - (int)(DateTime.UtcNow - t0).TotalMilliseconds;
                if (wait > 0) wake.WaitOne(wait);
            }
        }

        public static double PingMs(string host)
        {
            if (string.IsNullOrEmpty(host)) return double.NaN;
            try { using (var p = new Ping()) { var r = p.Send(host, 1000); return r.Status == IPStatus.Success ? Math.Max(1, r.RoundtripTime) : double.NaN; } }
            catch { return double.NaN; }
        }

        static readonly string[] vpnWords = { "vpn", "anyconnect", "globalprotect", "pangp", "juniper", "pulse", "fortinet", "forti", "wireguard", "tap-windows", "zscaler", "openvpn", "netskope", "f5 ", "ivanti", "checkpoint", "check point" };
        public static bool IsVpn(NetworkInterface n)
        {
            if (n.NetworkInterfaceType == NetworkInterfaceType.Ppp) return true;
            string d = (n.Description + " " + n.Name).ToLowerInvariant();
            foreach (var w in vpnWords) if (d.Contains(w)) return true;
            return false;
        }

        public void RefreshInterfaces()
        {
            try
            {
                NetworkInterface best = null, vpn = null; string gw = "";
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (n.OperationalStatus != OperationalStatus.Up) continue;
                    if (n.NetworkInterfaceType == NetworkInterfaceType.Loopback || n.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    if (IsVpn(n)) { if (vpn == null) vpn = n; continue; }
                    string g = "";
                    foreach (var ga in n.GetIPProperties().GatewayAddresses)
                        if (ga.Address.AddressFamily == AddressFamily.InterNetwork && !ga.Address.Equals(IPAddress.Any)) { g = ga.Address.ToString(); break; }
                    if (g == "") continue;
                    if (best == null || (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet && best.NetworkInterfaceType != NetworkInterfaceType.Ethernet)) { best = n; gw = g; }
                }
                lock (L)
                {
                    if (best == null || Primary == null || best.Id != Primary.Id) pRx = -1;
                    Primary = best;
                    if (best != null)
                    {
                        Adapter = best.Name; AdapterKind = best.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "LAN";
                        LinkSpeed = best.Speed > 0 ? (best.Speed >= 1000000000 ? (best.Speed / 1e9).ToString("0.#", CultureInfo.CurrentCulture) + " Gb/s" : (best.Speed / 1e6).ToString("0", CultureInfo.CurrentCulture) + " Mb/s") : "";
                        Gateway = gw; IfaceId = best.Id;
                        try { PrimaryMtu = best.GetIPProperties().GetIPv4Properties().Mtu; } catch { PrimaryMtu = 0; }
                    }
                    else { Adapter = ""; Gateway = ""; LinkSpeed = ""; IfaceId = ""; }
                    VpnUp = vpn != null; VpnName = vpn != null ? vpn.Description : ""; VpnMtu = 0;
                    if (vpn != null) try { VpnMtu = vpn.GetIPProperties().GetIPv4Properties().Mtu; } catch { }
                    var dnsFrom = vpn ?? best; DnsServer = "";
                    if (dnsFrom != null)
                        foreach (var d in dnsFrom.GetIPProperties().DnsAddresses)
                            if (d.AddressFamily == AddressFamily.InterNetwork) { DnsServer = d.ToString(); break; }
                    LocalIp = "";
                    if (best != null)
                        foreach (var ua in best.GetIPProperties().UnicastAddresses)
                            if (ua.Address.AddressFamily == AddressFamily.InterNetwork) { LocalIp = ua.Address.ToString(); break; }
                }
                string a2, k2, g2, d2, v2, ip2; bool vp2; lock (L) { a2 = Adapter; k2 = AdapterKind; g2 = Gateway; d2 = DnsServer; vp2 = VpnUp; v2 = VpnName; ip2 = LocalIp; }
                IfEvents(a2, k2, g2, d2, vp2, v2, ip2);
                try { var c = new DriveInfo("C"); string cf = (c.AvailableFreeSpace / 1073741824.0).ToString("0", CultureInfo.CurrentCulture) + " GB free of " + (c.TotalSize / 1073741824.0).ToString("0", CultureInfo.CurrentCulture) + " GB"; lock (L) CFree = cf; } catch { }
            }
            catch (Exception e) { Program.Log("interfaces: " + e.Message); }
        }

        void IfEvents(string adapter, string kind, string gw, string dns, bool vpn, string vpnName, string ip)
        {
            if (pAdapter == null)
            {
                Events.Add(adapter == "" ? 2 : 3, "net", adapter == "" ? "No network connection" : "On " + kind + " (" + adapter + ")" + (gw != "" ? ", gateway " + gw : "") + (vpn ? ", VPN up" : ""));
            }
            else
            {
                if (adapter != pAdapter) Events.Add(adapter == "" ? 2 : 1, "net", adapter == "" ? Voice.P("Lost the network - you're offline, boss.", "Network connection lost.") : "Switched to " + kind + " (" + adapter + ")");
                else
                {
                    if (ip != "" && ip != pIp) Events.Add(1, "net", "IP address changed" + (pIp != "" ? " " + pIp + " → " + ip : " to " + ip));
                    if (gw != "" && gw != pGw) Events.Add(1, "net", "Gateway changed to " + gw);
                }
                if (vpn != pVpn) Events.Add(vpn ? 0 : 1, "vpn", vpn ? "VPN connected (" + Draw.Trunc(vpnName, 30) + ")" : Voice.P("VPN dropped.", "VPN disconnected."));
                if (dns != "" && dns != pDns) Events.Add(1, "dns", "DNS server changed to " + dns + (vpn ? " (via VPN)" : ""));
            }
            pAdapter = adapter; pGw = gw; pDns = dns; pVpn = vpn; pIp = ip;
        }

        // Self-test only: replays a realistic sequence of changes through the same code paths.
        public string SimulateForTest(StringBuilder log)
        {
            IfEvents("Wi-Fi", "Wi-Fi", "10.0.0.1", "64.71.255.204", false, "", "10.0.0.82");
            IfEvents("Wi-Fi", "Wi-Fi", "10.0.0.1", "10.8.0.53", true, "Example VPN Client Adapter", "10.0.0.82");
            IfEvents("Wi-Fi", "Wi-Fi", "10.0.0.1", "10.8.0.53", false, "", "10.0.0.90");
            IfEvents("Ethernet", "LAN", "192.168.1.1", "192.168.1.1", false, "", "192.168.1.20");
            WifiEvents("connected", "aa:bb:cc:00:00:01", "36", 80, 866);
            WifiEvents("connected", "aa:bb:cc:00:00:02", "36", 78, 866);
            WifiEvents("connected", "aa:bb:cc:00:00:02", "36", 40, 144);
            WifiEvents("disconnected", "", "", -1, -1);
            lock (L) { HasWifi = true; WifiSignal = 40; }
            for (int i = 0; i < 10; i++) Retx.Add(12);
            Judge();
            foreach (var e in Events.Recent(50)) log.AppendLine("      [" + e.Kind + "] " + e.Text);
            return hint;
        }

        void ProbeDns()
        {
            double ms; int rc = DnsTime("www.microsoft.com", out ms);
            lock (L) { DnsFailedLast = rc != 0 && rc != 9003 && rc != 9501; Dns.Add(DnsFailedLast ? double.NaN : ms); }
        }
        // A real lookup that skips the local cache, so it measures the DNS server itself.
        public static int DnsTime(string name, out double ms)
        {
            IntPtr res; var sw = Stopwatch.StartNew();
            int rc = Native.DnsQuery(name, 1, 0x8, IntPtr.Zero, out res, IntPtr.Zero);
            ms = sw.Elapsed.TotalMilliseconds;
            if (res != IntPtr.Zero) Native.DnsRecordListFree(res, 1);
            return rc;
        }

        void ProbeWifi()
        {
            bool wifi; lock (L) wifi = AdapterKind == "Wi-Fi";
            if (!wifi) { lock (L) { HasWifi = false; WifiSignal = -1; } return; }
            try
            {
                string outp = Job.Run(Job.Netsh, "wlan show interfaces", 3000);
                int sig = -1, rate = -1; string ssid = "", ch = "", bssid = "", state = "";
                foreach (var line in outp.Split('\n'))
                {
                    var m = Regex.Match(line, @"^\s*([^:]+?)\s*:\s*(.+?)\s*$"); if (!m.Success) continue;
                    string k = m.Groups[1].Value, v = m.Groups[2].Value;
                    if (k == "Signal") { int x; if (int.TryParse(v.TrimEnd('%'), out x)) sig = x; }
                    else if (k == "SSID") ssid = v; else if (k == "Channel") ch = v;
                    else if (k == "BSSID" || k == "AP BSSID") bssid = v;
                    else if (k == "State") state = v.ToLowerInvariant();
                    else if (k.StartsWith("Receive rate", StringComparison.Ordinal)) { double r; if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r)) rate = (int)r; }
                }
                lock (L) { HasWifi = sig >= 0; WifiSignal = sig; Ssid = ssid; WifiChannel = ch; Bssid = bssid; WifiState = state; RxRate = rate; }
                WifiEvents(state, bssid, ch, sig, rate);
            }
            catch { lock (L) HasWifi = false; }
        }

        // ARP spoofing alarm: the router's hardware address should not change while you stay on the same network.
        string gwMacKey = "", gwMac = "";
        // Your provider's name: the first public router after your own is theirs, and its reverse-DNS name usually says so
        // (what "nslookup <hop>" would show). Learned once per network: a 6-hop trace toward 1.1.1.1 and one name lookup.
        public volatile string Isp = ""; string ispKey = ""; volatile bool ispBusy;
        void LearnIsp()
        {
            string key; bool vpn; lock (L) { key = IfaceId + "|" + Gateway + "|" + Ssid; vpn = VpnUp; }
            if (key == ispKey || ispBusy || Cfg.NetSec == 0 || vpn || Gateway == "") return;
            ispKey = key; ispBusy = true; Isp = "";
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var hops = DestCheck.TraceHops(IPAddress.Parse("1.1.1.1"), 6);
                    var first = hops.Select(h => h.Item1).FirstOrDefault(ip => ip != null && DestCheck.Kind(ip) == "public");
                    if (first != null)
                    {
                        string name = ""; var t = Task.Factory.StartNew(() => { try { return System.Net.Dns.GetHostEntry(IPAddress.Parse(first)).HostName; } catch { return ""; } });
                        if (t.Wait(3000)) name = t.Result;
                        string owner = name != first ? DestCheck.OwnerOf(name) : null;
                        if (owner != null && owner != "cloudflare.com" && owner != "Cloudflare") { Isp = owner; Program.Log("provider: " + owner); }
                    }
                }
                catch (Exception e) { Program.Log("isp: " + e.Message); }
                ispBusy = false;
            });
        }
        void WatchGatewayMac()
        {
            string gw, key; lock (L) { gw = Gateway; key = IfaceId + "|" + Gateway + "|" + Ssid; }
            if (gw == "") { gwMacKey = ""; gwMac = ""; return; }
            string mac = ""; try { mac = Arp.MacOf(gw); } catch { }
            if (mac == "") return;
            if (key != gwMacKey) { gwMacKey = key; gwMac = mac; return; }   // new network: just learn it
            if (mac != gwMac)
            {
                Events.Add(2, "net", Voice.P("Your router's hardware address just changed (" + gwMac + " → " + mac + ") while you stayed on the same network. If nobody swapped the router, someone may be pretending to be it (ARP spoofing). Use the VPN until you know.",
                                             "Gateway MAC changed " + gwMac + " → " + mac + " on the same network - possible ARP spoofing."));
                gwMac = mac;
            }
        }
        // After a drop, ask Windows' own log why (a few seconds later, so the event is written).
        void ExplainWifiDrop()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(4000);
                string err; var rows = EvLog.WifiDrops(0.05, out err);
                if (rows.Count > 0) Events.Add(1, "wifi", "Why: " + rows[0][1]);
            });
        }
        void WifiEvents(string state, string bssid, string ch, int sig, int rate)
        {
            var now = DateTime.Now;
            if (pState != null)
            {
                if (state != pState && state.StartsWith("disconnected", StringComparison.Ordinal)) { Events.Add(2, "wifi", Voice.P("Wi-Fi dropped.", "Wi-Fi disconnected.")); ExplainWifiDrop(); }
                else if (state != pState && pState.StartsWith("disconnected", StringComparison.Ordinal) && state == "connected") Events.Add(0, "wifi", "Wi-Fi reconnected");
                if (bssid != "" && pBssid != "" && bssid != pBssid) { lastRoam = now; Events.Add(1, "wifi", Voice.P("Wi-Fi hopped to another access point", "Wi-Fi roamed to another access point") + " (ch " + ch + ", signal " + sig + "%)"); }
                else if (ch != "" && pCh != "" && ch != pCh) Events.Add(1, "wifi", "Wi-Fi channel changed " + pCh + " → " + ch);
                if (rate > 0 && pRate >= 50 && rate < pRate * 0.5) Events.Add(1, "wifi", "Wi-Fi link rate dropped " + pRate + " → " + rate + " Mb/s");
                if (sig >= 0 && pSig >= 0 && pSig - sig >= 25) { lastSigDrop = now; Events.Add(1, "wifi", "Wi-Fi signal fell to " + sig + "% (was " + pSig + "%)"); }
            }
            pState = state; pBssid = bssid; pCh = ch; pRate = rate; pSig = sig;
        }

        // Connection bursts and connections stuck knocking (SYN_SENT), from the socket table - no capture.
        void ProbeConns()
        {
            var now = DateTime.UtcNow; double sec = Math.Max(1, (now - lastConnT).TotalSeconds); lastConnT = now;
            Dictionary<int, string> names; Dictionary<string, string> ipn; lock (L) { names = PidNames; ipn = IpNames; }
            List<Conn> list; try { list = Tables.Tcp(false, names); } catch { return; }
            if (prevKeys != null)
                foreach (var g in list.Where(c => !prevKeys.Contains(c.Key)).GroupBy(c => c.Proc))
                {
                    int n = g.Count();
                    if (n >= 30) Once("burst|" + g.Key, 300, 1, "conn", g.Key + Voice.P(" went on a spree: ", " opened ") + n + " new connections in " + sec.ToString("0", CultureInfo.CurrentCulture) + " s");
                }
            var syn = new HashSet<string>();
            foreach (var c in list.Where(x => x.State == "SYN_SENT"))
            {
                syn.Add(c.Key); DateTime first;
                if (!synFirst.TryGetValue(c.Key, out first)) synFirst[c.Key] = now;
                else if ((now - first).TotalSeconds >= 6)
                {
                    string h; string host = !string.IsNullOrEmpty(c.RemoteIp) && ipn.TryGetValue(c.RemoteIp, out h) ? h : c.RemoteIp;
                    Once("syn|" + c.Proc + "|" + c.RemoteIp + "|" + c.RemotePort, 300, 1, "conn", c.Proc + " is stuck connecting to " + host + ":" + c.RemotePort + Voice.P(" - knocking, nobody answers.", " (SYN_SENT)."));
                }
            }
            foreach (var k in synFirst.Keys.Where(x => !syn.Contains(x)).ToList()) synFirst.Remove(k);
            prevKeys = new HashSet<string>(list.Select(c => c.Key));
        }

        static readonly Dictionary<string, string> friendly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ms-teams", "Teams" }, { "teams", "Teams" }, { "chrome", "Chrome" }, { "msedge", "Edge" }, { "outlook", "Outlook" }, { "olk", "Outlook" },
            { "onedrive", "OneDrive" }, { "MsMpEng", "Defender" }, { "firefox", "Firefox" }, { "Code", "VS Code" }, { "WINWORD", "Word" },
            { "EXCEL", "Excel" }, { "POWERPNT", "PowerPoint" }, { "putty", "PuTTY" }, { "explorer", "Explorer" }, { "slack", "Slack" },
            { "Zoom", "Zoom" }, { "pycharm64", "PyCharm" }, { "AdeeboMeter", "AdeeboMeter" }, { "FoxitPDFReader", "Foxit PDF" }
        };
        public static string Friendly(string n) { string f; return friendly.TryGetValue(n, out f) ? f : n; }
        public static string RawOf(string fn) { foreach (var kv in friendly) if (kv.Value == fn) return kv.Key; return fn; }

        void ProbeProcesses(Dictionary<int, TimeSpan> lastCpu, ref DateTime lastT)
        {
            DateTime now = DateTime.UtcNow; double el = (now - lastT).TotalSeconds; lastT = now;
            var groups = new Dictionary<string, AppStat>(); var names = new Dictionary<int, string>(); var cur = new Dictionary<int, TimeSpan>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == 0) continue;
                    string fn = Friendly(p.ProcessName); names[p.Id] = fn;
                    AppStat a; if (!groups.TryGetValue(fn, out a)) { a = new AppStat { Name = fn }; groups[fn] = a; }
                    a.Count++; if (a.Pid == 0) a.Pid = p.Id;
                    try { a.Mem += p.WorkingSet64; } catch { }
                    try { TimeSpan t = p.TotalProcessorTime; cur[p.Id] = t; TimeSpan pt; if (el > 0 && lastCpu.TryGetValue(p.Id, out pt)) a.Cpu += Math.Max(0, (t - pt).TotalSeconds / el / Cores * 100); } catch { }
                }
                catch { }
                finally { p.Dispose(); }
            }
            lastCpu.Clear(); foreach (var kv in cur) lastCpu[kv.Key] = kv.Value;
            lock (L) { Apps = groups.Values.ToList(); PidNames = names; }
        }

        void ProbeCall()
        {
            // Teams opens UDP media sockets (default 50000-50059) only while a call is running.
            bool call = false; Dictionary<int, string> names; lock (L) names = PidNames;
            if (!names.ContainsValue("Teams")) { lock (L) CallLikely = false; return; }
            foreach (var u in Tables.Udp())
            {
                string n; if (!names.TryGetValue(u.Value, out n) || n != "Teams") continue;
                if (u.Key >= 50000 && u.Key <= 50059) { call = true; break; }
            }
            lock (L) CallLikely = call;
            if (call != prevCall) Events.Add(0, "call", call ? "Teams call started" : "Teams call ended");
            prevCall = call;
        }

        // Names for remote IPs, read from Windows' own DNS cache - no network traffic.
        public void RefreshIpNames()
        {
            var map = new Dictionary<string, string>();
            IntPtr table;
            try
            {
                if (!Native.DnsGetCacheDataTable(out table)) return;
                IntPtr e = table; int count = 0; int P = IntPtr.Size;
                while (e != IntPtr.Zero)
                {
                    IntPtr next = Marshal.ReadIntPtr(e, 0), pName = Marshal.ReadIntPtr(e, P);
                    int type = Marshal.ReadInt16(e, 2 * P) & 0xFFFF;
                    string name = pName != IntPtr.Zero ? Marshal.PtrToStringUni(pName) : null;
                    if (name != null && (type == 1 || type == 28) && count++ < 3000) LookupCached(name, (short)type, map);
                    if (pName != IntPtr.Zero) Native.DnsFree(pName, 0);
                    Native.DnsFree(e, 0);
                    e = next;
                }
            }
            catch (Exception ex) { Program.Log("dns cache: " + ex.Message); }
            // Some Windows builds hand back an empty table from that API. Then read the same cache the way Windows' own
            // ipconfig /displaydns shows it - still offline, only while the engineer view is open, at most every 30 s.
            if (map.Count == 0 && !dnsCacheLocked && (DateTime.UtcNow - lastDisplayDns).TotalSeconds >= 30)
            {
                lastDisplayDns = DateTime.UtcNow;
                try
                {
                    string outp = Job.Run(Path.Combine(Environment.SystemDirectory, "ipconfig.exe"), "/displaydns", 5000) ?? "";
                    // Some Windows builds keep the DNS cache admin-only. Then stop asking: names come from live capture instead.
                    if (outp.IndexOf("elevation", StringComparison.OrdinalIgnoreCase) >= 0) { dnsCacheLocked = true; Program.Log("DNS cache is admin-only on this PC"); }
                    else ParseDisplayDns(outp, map);
                }
                catch (Exception ex) { Program.Log("displaydns: " + ex.Message); }
            }
            if (map.Count == 0) { lock (L) if (IpNames.Count > 0) return; }   // keep the last good names rather than blanking them
            lock (L) IpNames = map;
        }
        DateTime lastDisplayDns = DateTime.MinValue; public volatile bool dnsCacheLocked;
        static readonly char[] IpChars = { '.', ':' };
        // Each block starts with the queried name over a dashed line; any record line whose value is an IP maps that IP to the name.
        public static void ParseDisplayDns(string text, Dictionary<string, string> map)
        {
            if (string.IsNullOrEmpty(text)) return;
            string header = null, prev = null; int n = 0;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("----", StringComparison.Ordinal)) { header = prev; continue; }
                if (line != "") prev = line;
                if (header == null || header.Length > 253) continue;
                int c = line.LastIndexOf(" : ", StringComparison.Ordinal); if (c < 0) continue;
                string v = line.Substring(c + 3).Trim(); IPAddress ip;
                if (v.IndexOfAny(IpChars) < 0 || !IPAddress.TryParse(v, out ip)) continue;
                string k = ip.ToString();
                if (!map.ContainsKey(k)) map[k] = header.TrimEnd('.');
                if (++n > 5000) break;
            }
        }
        static void LookupCached(string name, short type, Dictionary<string, string> map)
        {
            IntPtr res;
            if (Native.DnsQuery(name, type, 0x10 /*NO_WIRE_QUERY: cache only*/, IntPtr.Zero, out res, IntPtr.Zero) != 0 || res == IntPtr.Zero) return;
            try
            {
                int P = IntPtr.Size, typeOff = 2 * P, dataOff = 2 * P + 16;
                for (IntPtr r = res; r != IntPtr.Zero; r = Marshal.ReadIntPtr(r, 0))
                {
                    int t = Marshal.ReadInt16(r, typeOff) & 0xFFFF;
                    string ip = null;
                    if (t == 1) ip = new IPAddress((uint)Marshal.ReadInt32(r, dataOff)).ToString();
                    else if (t == 28) { var b = new byte[16]; Marshal.Copy(r + dataOff, b, 0, 16); ip = new IPAddress(b).ToString(); }
                    if (ip != null && !map.ContainsKey(ip)) map[ip] = name;
                }
            }
            finally { Native.DnsRecordListFree(res, 1); }
        }

        public List<Conn> Connections(bool includeIdle)
        {
            Dictionary<int, string> names; Dictionary<string, string> ipn; lock (L) { names = PidNames; ipn = IpNames; }
            var list = Tables.Tcp(includeIdle, names);
            if (includeIdle)
                foreach (var u in Tables.Udp())
                {
                    string n; names.TryGetValue(u.Value, out n);
                    list.Add(new Conn { Key = "U|" + u.Key + "|" + u.Value, Proto = "UDP", State = "BOUND", Local = "*:" + u.Key, Remote = "*", RemoteIp = "", LocalPort = u.Key, Pid = u.Value, Proc = n ?? "pid " + u.Value });
                }
            foreach (var c in list)
            {
                string h;
                if (!string.IsNullOrEmpty(c.RemoteIp) && ipn.TryGetValue(c.RemoteIp, out h)) { c.Host = h; c.HostFrom = "DNS cache"; }
                else if (c.State == "LISTEN") c.Host = "listening";
                else if (c.State == "BOUND") c.Host = "bound, not connected";
                else c.Host = c.RemoteIp;
            }
            return list;
        }

        // ---------- verdict, every 10 s
        public List<AppStat> TopApps(bool byCpu, int n, bool closableOnly)
        {
            List<AppStat> apps; lock (L) apps = Apps;
            return apps.Where(a => a.Name != "Idle" && (!closableOnly || !notClosable.Contains(a.Name)))
                       .OrderByDescending(a => byCpu ? a.Cpu : a.Mem).Take(n).ToList();
        }
        static readonly HashSet<string> notClosable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Idle", "System", "Registry", "Memory Compression", "svchost", "Defender", "csrss", "dwm", "Explorer", "lsass", "AdeeboMeter", "smss", "wininit", "services", "MsSense", "SearchHost", "Secure System", "MsMpEng" };

        void Judge()
        {
            double cpu = Cpu.Avg(10), mem = Mem.Avg(10), com = Commit.Avg(10);
            double gw, net, dns, loss, jit; bool hasWifi, vpn, netEver, gwEver, call, dnsFail; int sig;
            lock (L)
            {
                gw = GwPing.Avg(5); net = NetPing.Avg(3); dns = Dns.Last; loss = NetEverReplied ? Loss.Avg(10) * 100 : double.NaN; jit = NetPing.Jitter(10);
                hasWifi = HasWifi; sig = WifiSignal; vpn = VpnUp; netEver = NetEverReplied; gwEver = GwEverReplied; call = CallLikely; dnsFail = DnsFailedLast;
            }
            int pc = (cpu > 88 || mem > 90 || com > 92) ? 2 : (cpu > 60 || mem > 80) ? 1 : 0;
            int link = 3;
            if (hasWifi) link = sig < 30 ? 2 : sig < 55 ? 1 : 0;
            if (!double.IsNaN(gw)) link = Math.Max(link == 3 ? 0 : link, gw > 50 ? 2 : gw > 15 ? 1 : 0);
            else if (!hasWifi) link = gwEver ? 2 : 3;
            int isp = 3;
            if (!double.IsNaN(net) && !double.IsNaN(gw)) { double d = net - gw; isp = d > 100 ? 2 : d > 40 ? 1 : 0; }
            else if (!double.IsNaN(net)) isp = net > 150 ? 2 : net > 60 ? 1 : 0;
            int dn = dnsFail ? 2 : double.IsNaN(dns) ? 3 : dns > 300 ? 2 : dns > 100 ? 1 : 0;
            int inet = !netEver ? 3 : loss >= 20 ? 2 : loss >= 10 ? 1 : 0;
            if (link == 2) { isp = 3; dn = 3; inet = 3; }
            path = new[] { pc, link, isp, dn, inet };

            var top = TopApps(false, 2, true); var topC = TopApps(true, 1, true);
            string t1 = top.Count > 0 ? top[0].Name + " (" + Draw.MemStr(top[0].Mem) + ")" : "your apps", t2 = top.Count > 1 ? top[1].Name : "";
            string hogs = t1 + (t2 != "" ? " and " + t2 : "");
            string linkName = hasWifi ? "Wi-Fi" : "local network";
            if (link == 2 && hasWifi && sig < 30) Set(2, Voice.P("Boss, your Wi-Fi is whispering (signal " + sig + "%). Not the PC's fault - move closer or plug in a cable.", "Wi-Fi signal is weak (" + sig + "%). Move closer to the access point or use a cable."));
            else if (link == 2) Set(2, Voice.P("Found it: your " + linkName + " is dragging (gateway " + Draw.Fmt(gw, "0", " ms") + "). The PC is innocent.", "The " + linkName + " is slow (gateway " + Draw.Fmt(gw, "0", " ms") + "). The PC is not the cause."));
            else if (pc == 2 && (mem > 90 || com > 92)) Set(2, Voice.P("Your PC is cooked, boss - memory is full. " + hogs + " are the hungry ones; close something.", "Memory is almost full. " + hogs + " use the most; close something."));
            else if (pc == 2) Set(2, Voice.P("CPU is running a marathon (" + cpu.ToString("0", CultureInfo.CurrentCulture) + "%). " + (topC.Count > 0 ? topC[0].Name : "Something") + " is doing the running.", "CPU is pegged (" + cpu.ToString("0", CultureInfo.CurrentCulture) + "%). " + (topC.Count > 0 ? topC[0].Name : "An app") + " uses the most."));
            else if (call && (jit > 30 || loss > 2)) Set(1, Voice.P("Call getting wobbly (jitter " + Draw.Fmt(jit, "0", " ms") + "). If faces freeze, turn video off - audio will survive.", "Call quality dropping (jitter " + Draw.Fmt(jit, "0", " ms") + "). Turn video off if it stutters."));
            else if (isp == 2 || inet == 2) Set(2, Voice.P("Not you, not your PC - the internet past your router" + (vpn ? " / VPN" : "") + " is having a bad day.", "The internet past your router" + (vpn ? " / VPN" : "") + " is slow. Your PC and " + linkName + " are fine."));
            else if (dn == 2) Set(2, dnsFail ? Voice.P("The internet's there but DNS lost the phone book - sites won't open by name.", "DNS lookups are failing; sites won't open by name.") : Voice.P("DNS is taking the scenic route (" + dns.ToString("0", CultureInfo.CurrentCulture) + " ms) - pages will be slow to start.", "DNS is slow (" + dns.ToString("0", CultureInfo.CurrentCulture) + " ms); pages will be slow to start."));
            else if (call) Set(0, Voice.P("Call looks smooth. Go impress them.", "Call quality is good."));
            else if (pc == 1) Set(1, Voice.P("Busy, but holding. Maybe don't open another big app just yet.", "Busy but fine. Avoid opening big apps for now."));
            else if (link == 1 || isp == 1 || dn == 1 || inet == 1) Set(1, Voice.P("Network's a bit sleepy; your PC is fine.", "Network a bit slow; your PC is fine."));
            else Set(0, Voice.P("All quiet. Nothing to fix - go get coffee.", "All quiet."));

            // gateway latency jumps (against its own calm baseline)
            if (!double.IsNaN(gw))
            {
                if (double.IsNaN(gwBase)) gwBase = gw;
                if (!gwFlag && gw > Math.Max(30, gwBase * 3)) { gwFlag = true; Events.Add(1, "gw", "Gateway latency jumped to " + gw.ToString("0", CultureInfo.CurrentCulture) + " ms (normally " + gwBase.ToString("0", CultureInfo.CurrentCulture) + " ms)"); }
                else if (gwFlag && gw < Math.Max(15, gwBase * 2)) { gwFlag = false; Events.Add(0, "gw", "Gateway latency back to " + gw.ToString("0", CultureInfo.CurrentCulture) + " ms"); }
                if (!gwFlag) gwBase = gwBase * 0.9 + gw * 0.1;
            }
            // red in / out
            if (prevLevel >= 0 && level == 2 && prevLevel != 2) Events.Add(2, "verdict", headline);
            else if (prevLevel == 2 && level != 2) Events.Add(0, "verdict", Voice.P("Back to normal.", "Recovered.") + " " + headline);
            prevLevel = level;

            // cause hints: put two facts side by side, only when both are true now
            double retx = Retx.Avg(10); var now = DateTime.Now; string h = "";
            bool roamed = (now - lastRoam).TotalSeconds < 90, sigDropped = (now - lastSigDrop).TotalSeconds < 90;
            if (retx >= 5)
            {
                if (hasWifi && (sig < 50 || sigDropped)) h = "TCP retransmits (" + retx.ToString("0", CultureInfo.CurrentCulture) + "/s) started when Wi-Fi signal fell to " + sig + "%.";
                else if (roamed) h = "TCP retransmits (" + retx.ToString("0", CultureInfo.CurrentCulture) + "/s) right after a Wi-Fi roam - should settle in a few seconds.";
                else if (!double.IsNaN(gw) && gw > 30) h = "TCP retransmits with a slow gateway (" + gw.ToString("0", CultureInfo.CurrentCulture) + " ms): the local link is struggling, not the internet.";
                else if (cpu > 88) h = "TCP retransmits while the CPU is pegged: the PC may be too busy to keep up.";
                else if (!double.IsNaN(gw) && gw < 15) h = "TCP retransmits (" + retx.ToString("0", CultureInfo.CurrentCulture) + "/s) with a healthy local link: the loss is further out (ISP, VPN or the server).";
            }
            else if (dn >= 1 && vpn) h = "DNS goes through the VPN - the slow lookups are on the VPN side.";
            else if (inet >= 1 && roamed) h = "Packet loss right after a Wi-Fi roam - should settle in a few seconds.";
            else if (call && hasWifi && sig < 50) h = "Teams call on weak Wi-Fi (" + sig + "%): a cable or moving closer helps the most.";
            if (h != "" && h != hint) Once("hint|" + h.Substring(0, Math.Min(30, h.Length)), 600, 1, "hint", h);
            hint = h;
        }
        void Set(int l, string h) { level = l; headline = h; }

        Snap BuildSnap()
        {
            var s = new Snap { Level = level, Headline = headline, Countdown = countdown, Path = path, Hint = hint, Recent = Events.Recent(3) };
            s.Chip = level == 2 ? Voice.P("Needs you", "Problem") : level == 1 ? "Busy" : Voice.P("All quiet", "OK");
            s.Cpu = Cpu.Last; s.Mem = Mem.Last; s.Disk = Disk.Last; s.Down = Down.Last; s.Up = Up.Last;
            s.DownHist = Down.Values(60); s.UpHist = Up.Values(60);
            s.MemText = UsedGB.ToString("0.0", CultureInfo.CurrentCulture) + " / " + TotalGB.ToString("0", CultureInfo.CurrentCulture) + " GB";
            s.DiskText = Draw.Fmt(s.Disk, "0", "% active");
            string gwS, adapter, speed, dnsSrv, vpnName, ssid, ch, target, cfree; bool hasWifi, vpn, netEver; int sig, vmtu, pmtu;
            double gw, net, dns, loss, jit, readMB, writeMB, downTot;
            lock (L)
            {
                gwS = Gateway; adapter = Adapter; speed = LinkSpeed; dnsSrv = DnsServer; vpnName = VpnName; ssid = Ssid; ch = WifiChannel; target = NetTarget; cfree = CFree;
                hasWifi = HasWifi; vpn = VpnUp; netEver = NetEverReplied; sig = WifiSignal; vmtu = VpnMtu; pmtu = PrimaryMtu;
                gw = GwPing.Last; net = NetPing.Last; dns = Dns.Last; loss = NetEverReplied ? Loss.Avg(10) * 100 : double.NaN; jit = NetPing.Jitter(10);
                s.Call = CallLikely; readMB = DiskReadMBs; writeMB = DiskWriteMBs; downTot = DownBytesTotal;
            }
            s.Ping = net; s.Jitter = jit; s.LossPct = loss; s.DnsMs = dns;
            s.PathLabel = new[] { "PC", hasWifi ? "Wi-Fi" : "LAN", vpn ? "VPN" : "ISP", "DNS", "Internet" };
            string[] word = { "OK", "Slow", "Problem here", "Unknown" };
            s.PathMain = new[]
            {
                "CPU " + Draw.Fmt(s.Cpu, "0", "%") + " · RAM " + Draw.Fmt(s.Mem, "0", "%"),
                (hasWifi ? "signal " + sig + "% · " : "") + "gw " + Draw.Fmt(gw, "0", " ms"),
                !double.IsNaN(net) && !double.IsNaN(gw) ? "+" + Math.Max(0, net - gw).ToString("0", CultureInfo.CurrentCulture) + " ms past router" : (vpn ? "tunnel up" : "-"),
                Draw.Fmt(dns, "0", " ms"),
                netEver ? Draw.Fmt(net, "0", " ms") + " · loss " + Draw.Fmt(loss, "0", "%") : "no ICMP reply yet"
            };
            s.PathSub = new[]
            {
                word[path[0]] + " · " + Cores + " threads · " + TotalGB.ToString("0", CultureInfo.CurrentCulture) + " GB",
                word[path[1]] + " · " + (hasWifi ? (ssid != "" ? ssid : "Wi-Fi") + (ch != "" ? " ch " + ch : "") : adapter) + (gwS != "" ? " · " + gwS : ""),
                word[path[2]] + " · " + (vpn ? Draw.Trunc(vpnName, 24) + (vmtu > 0 ? " · MTU " + vmtu : "") : (Isp != "" ? Draw.Trunc(Isp, 22) : "no VPN") + (pmtu > 0 ? " · MTU " + pmtu : "")),
                word[path[3]] + " · " + (dnsSrv != "" ? dnsSrv : "no DNS server"),
                word[path[4]] + " · probe " + target
            };
            s.TopMem = TopApps(false, 6, false); s.TopCpu = TopApps(true, 6, false);
            Func<List<AppStat>, bool, List<string[]>> rows = delegate(List<AppStat> l, bool c) { return l.Take(3).Select(a => new[] { a.Name + (a.Count > 1 ? " (" + a.Count + ")" : ""), c ? a.Cpu.ToString("0", CultureInfo.CurrentCulture) + "%" : Draw.MemStr(a.Mem) }).ToList(); };
            s.Details["cpu"] = new Detail { Title = "CPU", TopTitle = "TOP BY CPU", Top = rows(s.TopCpu, true), Note = Voice.P("Above 88% for 10 s and I start nagging.", "Above 88% for 10 s turns AdeeboMeter red."),
                Lines = new List<string[]> { new[] { "Now", Draw.Fmt(s.Cpu, "0", "%") }, new[] { "10 s average", Draw.Fmt(Cpu.Avg(10), "0", "%") }, new[] { "10 s peak", Draw.Fmt(Cpu.Max(10), "0", "%") }, new[] { "Logical cores", Cores.ToString(CultureInfo.CurrentCulture) } } };
            s.Details["ram"] = new Detail { Title = "Memory", TopTitle = "TOP BY MEMORY", Top = rows(s.TopMem, false), Note = "Commit is the real \"can I open another app\" number.",
                Lines = new List<string[]> { new[] { "In use", UsedGB.ToString("0.0", CultureInfo.CurrentCulture) + " of " + TotalGB.ToString("0.0", CultureInfo.CurrentCulture) + " GB" }, new[] { "Commit", CommitUsedGB.ToString("0.0", CultureInfo.CurrentCulture) + " of " + CommitLimitGB.ToString("0.0", CultureInfo.CurrentCulture) + " GB" }, new[] { "Available", (TotalGB - UsedGB).ToString("0.0", CultureInfo.CurrentCulture) + " GB" }, new[] { "10 s peak", Draw.Fmt(Mem.Max(10), "0", "%") } } };
            s.Details["disk"] = new Detail { Title = "Disk", Note = "A busy disk often explains slow app launches.",
                Lines = new List<string[]> { new[] { "Active time", Draw.Fmt(s.Disk, "0", "%") }, new[] { "Read / write", Draw.Fmt(readMB, "0.0", "") + " / " + Draw.Fmt(writeMB, "0.0", " MB/s") }, new[] { "C:", cfree } } };
            s.Details["net"] = new Detail { Title = "Traffic", Note = "Who's talking to whom: click the tray icon.",
                Lines = new List<string[]> { new[] { "Adapter", Draw.Trunc(adapter, 24) }, new[] { "Link speed", speed == "" ? "-" : speed }, new[] { "Down / up now", Draw.Fmt(s.Down, "0.0", "") + " / " + Draw.Fmt(s.Up, "0.0", " Mb/s") }, new[] { "Since start", Draw.MemStr((long)downTot) + " down" } } };
            var pd = new Detail { Title = "Is it me or the network?", Note = "Gateway fast but internet slow = past your PC and " + (hasWifi ? "Wi-Fi" : "LAN") + ". Gateway slow = Wi-Fi or cable." };
            for (int i = 0; i < 5; i++) pd.Lines.Add(new[] { s.PathLabel[i], s.PathMain[i], path[i].ToString(CultureInfo.CurrentCulture) });
            pd.TopTitle = "WHAT CHANGED"; foreach (var e in s.Recent) pd.Top.Add(new[] { e.Text, e.T.ToString("HH:mm", CultureInfo.CurrentCulture) });
            s.Details["path"] = pd;
            s.Details["call"] = new Detail { Title = "Meeting mode", Note = "On only while Teams has call media sockets open (UDP 50000-50059).",
                Lines = new List<string[]> { new[] { "Upload", Draw.Fmt(s.Up, "0.0", " Mb/s") }, new[] { "Jitter", Draw.Fmt(jit, "0", " ms") }, new[] { "Loss", Draw.Fmt(loss, "0", "%") } } };
            AddHealth(s, "TCP retransmits", Retx, true, " /s", 2, 8);
            AddHealth(s, "TCP resets sent", Rst, true, " /s", 1, 3);
            AddHealth(s, "Failed connection attempts", Fail, false, " /min", 5, 20);
            AddHealth(s, "ICMP unreachable received", Unreach, false, " /min", 5, 20);
            AddHealth(s, "UDP datagrams dropped", UdpDrop, false, " /min", 10, 50);
            return s;
        }
        static void AddHealth(Snap s, string label, Ring r, bool perSec, string unit, double yl, double rl)
        {
            double v = perSec ? r.Avg(5) : r.Sum(60);
            int lv = double.IsNaN(v) ? 3 : v >= rl ? 2 : v >= yl ? 1 : 0;
            s.Health.Add(new[] { label, Draw.Fmt(v, perSec ? "0.0" : "0", unit), lv.ToString(CultureInfo.CurrentCulture) });
            s.HealthHist.Add(r.Values(60));
        }
    }

    // ================================================================== socket tables (no admin)
    static class Tables
    {
        static int Port(uint p) { return (int)(((p & 0xFF) << 8) | ((p >> 8) & 0xFF)); }
        static string State(int st)
        {
            switch (st)
            {
                case 1: return "CLOSED"; case 2: return "LISTEN"; case 3: return "SYN_SENT"; case 4: return "SYN_RCVD"; case 5: return "ESTABLISHED";
                case 6: return "FIN_WAIT1"; case 7: return "FIN_WAIT2"; case 8: return "CLOSE_WAIT"; case 9: return "CLOSING"; case 10: return "LAST_ACK";
                case 11: return "TIME_WAIT"; case 12: return "DELETE_TCB"; case 100: return "BOUND";
                default:
                    lock (oddStates) if (oddStates.Add(st)) Program.Log("TCP state number " + st + " seen (not in Windows' documented list)");
                    return "STATE " + st;
            }
        }
        static IntPtr Get(bool tcp, int af)
        {
            int len = 0, cls = tcp ? 5 : 1;
            uint size = tcp ? Native.GetExtendedTcpTable(IntPtr.Zero, ref len, true, af, cls, 0) : Native.GetExtendedUdpTable(IntPtr.Zero, ref len, true, af, cls, 0);
            if (size != 0 && size != 122 /*ERROR_INSUFFICIENT_BUFFER*/) return IntPtr.Zero;
            for (int i = 0; i < 3; i++)
            {
                len += 4096; IntPtr buf = Marshal.AllocHGlobal(len);
                uint rc = tcp ? Native.GetExtendedTcpTable(buf, ref len, true, af, cls, 0) : Native.GetExtendedUdpTable(buf, ref len, true, af, cls, 0);
                if (rc == 0) return buf;
                Marshal.FreeHGlobal(buf);
            }
            return IntPtr.Zero;
        }
        public static List<KeyValuePair<int, int>> Udp()
        {
            var r = new List<KeyValuePair<int, int>>();
            foreach (int af in new[] { 2, 23 })
            {
                IntPtr b = Get(false, af); if (b == IntPtr.Zero) continue;
                try
                {
                    int n = Marshal.ReadInt32(b), size = af == 2 ? 12 : 28;
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr row = b + 4 + i * size;
                        r.Add(new KeyValuePair<int, int>(Port((uint)Marshal.ReadInt32(row, af == 2 ? 4 : 20)), Marshal.ReadInt32(row, af == 2 ? 8 : 24)));
                    }
                }
                finally { Marshal.FreeHGlobal(b); }
            }
            return r;
        }
        public static List<Conn> Tcp(bool includeIdle, Dictionary<int, string> names)
        {
            var list = new List<Conn>();
            Func<int, string> nm = delegate(int pid) { string s; return names.TryGetValue(pid, out s) ? s : (pid == 4 ? "System" : pid == 0 ? "Windows (no owner)" : "pid " + pid); };
            IntPtr b = Get(true, 2);   // v4 row: state, laddr, lport, raddr, rport, pid
            if (b != IntPtr.Zero)
                try
                {
                    int n = Marshal.ReadInt32(b);
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr row = b + 4 + i * 24; int st = Marshal.ReadInt32(row, 0);
                        if (st == 0 || (st == 1 && Marshal.ReadInt32(row, 12) == 0)) st = 100;   // undocumented 0 (or CLOSED) with no remote end: an idle bound socket
                        bool idle = st == 2 || st == 100;
                        if (!includeIdle && idle) continue;
                        var la = new IPAddress((uint)Marshal.ReadInt32(row, 4)); var ra = new IPAddress((uint)Marshal.ReadInt32(row, 12));
                        int lp = Port((uint)Marshal.ReadInt32(row, 8)), rp = Port((uint)Marshal.ReadInt32(row, 16)), pid = Marshal.ReadInt32(row, 20);
                        if (!idle && IPAddress.IsLoopback(ra)) continue;
                        list.Add(new Conn { Key = "T|" + la + ":" + lp + "|" + ra + ":" + rp + "|" + pid, Proto = "TCP", State = State(st), Local = la + ":" + lp, Remote = idle ? "*" : ra + ":" + rp, RemoteIp = idle ? "" : ra.ToString(), RemotePort = rp, LocalPort = lp, Pid = pid, Proc = nm(pid) });
                    }
                }
                finally { Marshal.FreeHGlobal(b); }
            b = Get(true, 23);         // v6 row: laddr16, scope, lport, raddr16, scope, rport, state, pid (56 bytes)
            if (b != IntPtr.Zero)
                try
                {
                    int n = Marshal.ReadInt32(b);
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr row = b + 4 + i * 56; int st = Marshal.ReadInt32(row, 48);
                        if (st == 0 || (st == 1 && Marshal.ReadInt64(row, 24) == 0 && Marshal.ReadInt64(row, 32) == 0)) st = 100;
                        bool idle = st == 2 || st == 100;
                        if (!includeIdle && idle) continue;
                        byte[] l = new byte[16], r = new byte[16]; Marshal.Copy(row, l, 0, 16); Marshal.Copy(row + 24, r, 0, 16);
                        var la = new IPAddress(l); var ra = new IPAddress(r);
                        int lp = Port((uint)Marshal.ReadInt32(row, 20)), rp = Port((uint)Marshal.ReadInt32(row, 44)), pid = Marshal.ReadInt32(row, 52);
                        if (!idle && IPAddress.IsLoopback(ra)) continue;
                        list.Add(new Conn { Key = "T6|" + la + "|" + lp + "|" + ra + "|" + rp + "|" + pid, Proto = "TCP6", State = State(st), Local = "[" + la + "]:" + lp, Remote = idle ? "*" : "[" + ra + "]:" + rp, RemoteIp = idle ? "" : ra.ToString(), RemotePort = rp, LocalPort = lp, Pid = pid, Proc = nm(pid) });
                    }
                }
                finally { Marshal.FreeHGlobal(b); }
            return list;
        }
        public static readonly HashSet<int> oddStates = new HashSet<int>();
        public static bool OwnListener()
        {
            int me; using (var p = Process.GetCurrentProcess()) me = p.Id;
            return Tcp(true, new Dictionary<int, string>()).Any(c => c.Pid == me && c.State == "LISTEN");
        }
    }

    // ================================================================== drawing
    static class Theme
    {
        // Colour tokens. Light and dark follow Windows' own "app mode" setting and switch live.
        public static bool Dark;
        public static Color Ink, Muted, Sub, Line, Line2, Track, Soft, Card, Bg, Rail, UpText, BlueSoft, BlueDark, RowNew, RowGone, RowSel, RowHot, Grey;
        public static readonly Color Blue = C("#1F6FEB"), BlueHot = C("#1A5FCC"), Orange = C("#E36209"), Green = C("#2DA44E"), Amber = C("#BF8700"), Red = C("#CF222E");
        static readonly Color[] lvText = new Color[4], lvSoft = new Color[4];
        static Theme() { Apply(false); }
        public static Color C(string h) { return ColorTranslator.FromHtml(h); }
        public static bool SystemDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                { var v = k == null ? null : k.GetValue("AppsUseLightTheme"); return v is int && (int)v == 0; }
            }
            catch { return false; }
        }
        public static void Apply(bool dark)
        {
            Dark = dark;
            if (!dark)
            {
                Ink = C("#1B2330"); Muted = C("#5B6B7C"); Sub = C("#3D4B5A"); Line = C("#D6DEE8"); Line2 = C("#EEF2F6"); Track = C("#E6ECF3"); Soft = C("#F4F7FB");
                Card = C("#FFFFFF"); Bg = C("#F2F5F9"); Rail = C("#C5CED8"); UpText = C("#9A4A00"); BlueSoft = C("#EEF4FF"); BlueDark = C("#0B4FB3");
                RowNew = C("#E6F7EC"); RowGone = C("#FFEFEF"); RowSel = C("#E8F0FE"); RowHot = C("#F7FAFF"); Grey = C("#AEB8C2");
                lvText[0] = C("#116329"); lvText[1] = C("#7D4E00"); lvText[2] = C("#B42318");
                lvSoft[0] = C("#DAFBE1"); lvSoft[1] = C("#FFF8C5"); lvSoft[2] = C("#FFEBE9");
            }
            else
            {
                Ink = C("#E6EDF3"); Muted = C("#9AA7B4"); Sub = C("#C2CCD6"); Line = C("#2E3A46"); Line2 = C("#222B35"); Track = C("#2A3542"); Soft = C("#1B232D");
                Card = C("#161D26"); Bg = C("#0E1319"); Rail = C("#3A4757"); UpText = C("#F0A060"); BlueSoft = C("#13284A"); BlueDark = C("#9CC2FF");
                RowNew = C("#11301D"); RowGone = C("#3B1B1E"); RowSel = C("#1B2B44"); RowHot = C("#1A222C"); Grey = C("#4A5868");
                lvText[0] = C("#7EE2A8"); lvText[1] = C("#F2CC60"); lvText[2] = C("#FFA198");
                lvSoft[0] = C("#12301C"); lvSoft[1] = C("#3A2F0B"); lvSoft[2] = C("#3A1A1D");
            }
            lvText[3] = Muted; lvSoft[3] = Line2;
        }
        public static Color Lv(int l) { return l == 2 ? Red : l == 1 ? Amber : l == 0 ? Green : Grey; }
        public static Color LvText(int l) { return lvText[l < 0 || l > 3 ? 3 : l]; }
        public static Color LvSoft(int l) { return lvSoft[l < 0 || l > 3 ? 3 : l]; }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath(); float d = Math.Max(1, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
        public static void Fill(Graphics g, RectangleF r, float rad, Color c) { using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p); }
        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w) { using (var p = Round(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p); }

        public static void Logo(Graphics g, RectangleF r, bool arrows)
        {
            float k = r.Width / 64f;
            Func<float, float> X = delegate(float v) { return r.X + v * k; };
            Func<float, float> Y = delegate(float v) { return r.Y + v * k; };
            Fill(g, new RectangleF(X(2), Y(2), 60 * k, 60 * k), 14 * k, Blue);
            float w = (r.Width < 20 ? 8 : 7) * k;
            using (var p1 = new Pen(C("#3FB950"), w)) g.DrawArc(p1, X(12), Y(20), 40 * k, 40 * k, 180, 60);
            using (var p2 = new Pen(C("#F2CC60"), w)) g.DrawArc(p2, X(12), Y(20), 40 * k, 40 * k, 240, 60);
            using (var p3 = new Pen(C("#FF7B72"), w)) g.DrawArc(p3, X(12), Y(20), 40 * k, 40 * k, 300, 60);
            using (var pn = new Pen(Color.White, (r.Width < 20 ? 6 : 5) * k)) { pn.StartCap = LineCap.Round; pn.EndCap = LineCap.Round; g.DrawLine(pn, X(32), Y(40), X(43), Y(27)); }
            if (r.Width >= 20) using (var wb = new SolidBrush(Color.White)) g.FillEllipse(wb, X(27), Y(35), 10 * k, 10 * k);
            if (arrows && r.Width >= 28)
                using (var pa = new Pen(Color.FromArgb(220, 255, 255, 255), 3 * k))
                {
                    pa.LineJoin = LineJoin.Round; pa.StartCap = pa.EndCap = LineCap.Round;
                    g.DrawLines(pa, new[] { new PointF(X(22), Y(50)), new PointF(X(26), Y(54)), new PointF(X(30), Y(50)) });
                    g.DrawLines(pa, new[] { new PointF(X(34), Y(54)), new PointF(X(38), Y(50)), new PointF(X(42), Y(54)) });
                }
        }
        public static Icon MakeIcon(int size)
        {
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); Logo(g, new RectangleF(0, 0, size, size), false); }
                IntPtr h = bmp.GetHicon();
                try { using (var tmp = Icon.FromHandle(h)) return (Icon)tmp.Clone(); }
                finally { Native.DestroyIcon(h); }
            }
        }
        // A real Windows .ico of the logo (PNG images inside, 16 to 256 px) - for the exe, the Start menu and Settings > Apps.
        public static byte[] IcoBytes()
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            var pngs = new List<byte[]>();
            foreach (int sz in sizes)
                using (var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); Logo(g, new RectangleF(0, 0, sz, sz), sz >= 48); }
                    using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
                }
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int off = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32); w.Write(pngs[i].Length); w.Write(off); off += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
                w.Flush(); return ms.ToArray();
            }
        }
        public static void Pill(Graphics g, string text, Font f, float x, float y, int level, float S)
        {
            var sz = g.MeasureString(text, f);
            Fill(g, new RectangleF(x, y, sz.Width + 8 * S, sz.Height + 1 * S), (sz.Height + S) / 2, LvSoft(level));
            using (var b = new SolidBrush(LvText(level))) g.DrawString(text, f, b, x + 4 * S, y + 0.5f * S);
        }
        public static void Spark(Graphics g, double[] v, RectangleF r, Color c, double cap, bool fill)
        {
            if (v == null || v.Length < 2) return;
            double mx = cap;
            if (mx <= 0) { mx = 1; foreach (var x in v) if (!double.IsNaN(x) && x > mx) mx = x; mx *= 1.15; }
            var pts = new PointF[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                double y = double.IsNaN(v[i]) ? 0 : Math.Min(1, v[i] / mx);
                pts[i] = new PointF(r.X + (float)i / (v.Length - 1) * r.Width, r.Bottom - 1 - (float)y * (r.Height - 2));
            }
            if (fill)
            {
                var poly = new List<PointF>(pts); poly.Add(new PointF(r.Right, r.Bottom)); poly.Add(new PointF(r.X, r.Bottom));
                using (var b = new SolidBrush(Color.FromArgb(56, c))) g.FillPolygon(b, poly.ToArray());
            }
            using (var p = new Pen(c, 1.4f)) g.DrawLines(p, pts);
        }
    }

    // All fonts made once; nothing is created per paint (a lesson from topogram).
    static class Draw
    {
        public static float S = 1;
        public static Font fName, fHead, fBody, fBodyB, fMono, fMonoS, fTiny, fPill, fSmall, fBig, fTitle, fInput;
        static readonly Bitmap measureBmp = new Bitmap(1, 1);
        static Graphics measureG;
        public static Font fHero, fValue;
        public static string Fam(params string[] names)
        {
            try
            {
                using (var ifc = new InstalledFontCollection())
                {
                    var have = new HashSet<string>(ifc.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
                    foreach (var n in names) if (have.Contains(n)) return n;
                }
            }
            catch (Exception e) { Program.Log("fonts: " + e.Message); }
            return names[names.Length - 1];
        }
        public static void Init(float s)
        {
            if (fName != null) return;
            S = s;
            // Windows 11 type (Segoe UI Variable, Cascadia Mono) when it's there; Windows 10 falls back to Segoe UI / Consolas.
            string text = Fam("Segoe UI Variable Text", "Segoe UI"), disp = Fam("Segoe UI Variable Display", "Segoe UI"), semi = Fam("Segoe UI Semibold", "Segoe UI"), mono = Fam("Cascadia Mono", "Consolas");
            FontStyle semiStyle = semi == "Segoe UI" ? FontStyle.Bold : FontStyle.Regular;
            fName = new Font(disp, 12.5f * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fHead = new Font(semi, 13.5f * S, semiStyle, GraphicsUnit.Pixel);
            fBody = new Font(text, 12.5f * S, FontStyle.Regular, GraphicsUnit.Pixel);
            fBodyB = new Font(semi, 12.5f * S, semiStyle, GraphicsUnit.Pixel);
            fSmall = new Font(text, 11 * S, FontStyle.Regular, GraphicsUnit.Pixel);
            fMono = new Font(mono, 11.5f * S, FontStyle.Regular, GraphicsUnit.Pixel);
            fMonoS = new Font(mono, 10.5f * S, FontStyle.Regular, GraphicsUnit.Pixel);
            fTiny = new Font(text, 9.5f * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fPill = new Font(text, 10 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fBig = new Font(disp, 20 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fTitle = new Font(disp, 16 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fHero = new Font(disp, 30 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fValue = new Font(disp, 14 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fInput = new Font(mono, 13 * S, FontStyle.Regular, GraphicsUnit.Pixel);
            measureG = Graphics.FromImage(measureBmp); measureG.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }
        public static SizeF Measure(string t, Font f, int width) { lock (measureBmp) return width > 0 ? measureG.MeasureString(t ?? "", f, width) : measureG.MeasureString(t ?? "", f); }
        static Graphics Prep(Bitmap b) { var g = Graphics.FromImage(b); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; g.Clear(Color.Transparent); return g; }
        public static string Fmt(double v, string f, string unit) { return double.IsNaN(v) ? "-" : v.ToString(f, CultureInfo.CurrentCulture) + unit; }
        public static string Trunc(string s, int n) { if (s == null) return ""; return s.Length <= n ? s : s.Substring(0, n - 1) + "…"; }
        public static string MemStr(long b) { return b >= 1073741824 ? (b / 1073741824.0).ToString("0.0", CultureInfo.CurrentCulture) + " GB" : (b / 1048576.0).ToString("0", CultureInfo.CurrentCulture) + " MB"; }
        public static string Word(int l) { return l == 2 ? Voice.P("Act", "Problem") : l == 1 ? "Busy" : "OK"; }

        public static Bitmap Chip(Snap s, double dwell)
        {
            var ts = Measure(s.Chip, fSmall, 0);
            int W = (int)(9 * S + 16 * S + 6 * S + 8 * S + 6 * S + ts.Width + 9 * S), H = (int)(26 * S);
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var g = Prep(bmp))
            {
                var r = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, r, H / 2f, Color.FromArgb(246, Theme.Card)); Theme.Stroke(g, r, H / 2f, Theme.Line, 1);
                float x = 9 * S;
                Theme.Logo(g, new RectangleF(x, (H - 16 * S) / 2, 16 * S, 16 * S), false); x += 22 * S;
                using (var b = new SolidBrush(Theme.Lv(s.Level))) g.FillEllipse(b, x, (H - 8 * S) / 2, 8 * S, 8 * S); x += 14 * S;
                using (var b = new SolidBrush(Theme.Ink)) g.DrawString(s.Chip, fSmall, b, x, (H - ts.Height) / 2);
                if (dwell > 0)
                {
                    var tr = new RectangleF(10 * S, H - 4 * S, W - 20 * S, 2 * S);
                    using (var b = new SolidBrush(Theme.Track)) g.FillRectangle(b, tr);
                    using (var b = new SolidBrush(Theme.Blue)) g.FillRectangle(b, tr.X, tr.Y, tr.Width * (float)Math.Min(1, dwell), tr.Height);
                }
            }
            return bmp;
        }

        public static Bitmap Card(Snap s, Dictionary<string, RectangleF> hits)
        {
            hits.Clear();
            int W = (int)(264 * S); float pad = 12 * S, inner = W - 2 * pad;
            float headH = Measure(s.Headline, fHead, (int)inner).Height;
            float hintH = string.IsNullOrEmpty(s.Hint) ? 0 : Measure("Why: " + s.Hint, fSmall, (int)inner).Height + 4 * S;
            float h = pad + 24 * S + headH + hintH + 10 * S + 52 * S + (s.Call ? 44 * S : 0) + 3 * 20 * S + 6 * S + 17 * S + 34 * S + pad;
            int H = (int)Math.Ceiling(h);
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var g = Prep(bmp))
            using (var ink = new SolidBrush(Theme.Ink))
            using (var muted = new SolidBrush(Theme.Muted))
            {
                var cr = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, cr, 12 * S, Color.FromArgb(250, Theme.Card));
                Theme.Stroke(g, cr, 12 * S, s.Level == 2 ? Theme.Red : Theme.Line, s.Level == 2 ? 1.5f : 1f);
                float y = pad;
                Theme.Logo(g, new RectangleF(pad, y, 18 * S, 18 * S), false);
                g.DrawString("AdeeboMeter", fName, ink, pad + 24 * S, y + 1 * S);
                float nw = Measure("AdeeboMeter", fName, 0).Width;
                Theme.Pill(g, Word(s.Level), fPill, pad + 24 * S + nw + 2 * S, y + 2 * S, s.Level, S);
                string cd = s.Countdown + "s"; var cs = Measure(cd, fSmall, 0);
                g.DrawString(cd, fSmall, muted, W - pad - cs.Width, y + 2 * S);
                y += 24 * S;
                using (var hb = new SolidBrush(s.Level == 0 ? Theme.Ink : Theme.LvText(s.Level))) g.DrawString(s.Headline, fHead, hb, new RectangleF(pad, y, inner, headH + 2));
                y += headH;
                if (hintH > 0) { using (var wb = new SolidBrush(Theme.Sub)) g.DrawString("Why: " + s.Hint, fSmall, wb, new RectangleF(pad, y + 4 * S, inner, hintH)); y += hintH; }
                y += 10 * S;
                hits["path"] = new RectangleF(pad - 4 * S, y - 4 * S, inner + 8 * S, 52 * S);
                g.DrawString("IS IT ME OR THE NETWORK?", fTiny, muted, pad, y);
                y += 15 * S;
                PathStrip(g, s, new RectangleF(pad, y, inner, 32 * S), false);
                y += 37 * S;
                if (s.Call)
                {
                    bool bad = s.Jitter > 30 || s.LossPct > 2;
                    var r = new RectangleF(pad, y, inner, 38 * S); hits["call"] = r;
                    Theme.Fill(g, r, 7 * S, bad ? Theme.LvSoft(1) : Theme.BlueSoft);
                    using (var b = new SolidBrush(bad ? Theme.LvText(1) : Theme.BlueDark))
                    {
                        g.DrawString("MEETING MODE · TEAMS CALL", fTiny, b, r.X + 8 * S, r.Y + 4 * S);
                        g.DrawString("up " + Fmt(s.Up, "0.0", " Mb/s") + "   jitter " + Fmt(s.Jitter, "0", " ms") + "   loss " + Fmt(s.LossPct, "0", "%"), fSmall, b, r.X + 8 * S, r.Y + 19 * S);
                    }
                    y += 44 * S;
                }
                Bar(g, "cpu", "CPU", s.Cpu, Fmt(s.Cpu, "0", "%"), 60, 88, pad, ref y, inner, hits);
                Bar(g, "ram", "Memory", s.Mem, Fmt(s.Mem, "0", "%"), 80, 90, pad, ref y, inner, hits);
                Bar(g, "disk", "Disk", s.Disk, Fmt(s.Disk, "0", "%"), 60, 90, pad, ref y, inner, hits);
                y += 6 * S;
                hits["net"] = new RectangleF(pad - 4 * S, y - 3 * S, inner + 8 * S, 56 * S);
                using (var b = new SolidBrush(Theme.Blue)) g.DrawString("↓ " + Fmt(s.Down, "0.0", " Mb/s"), fMono, b, pad, y);
                string up = "↑ " + Fmt(s.Up, "0.0", " Mb/s"); var us = Measure(up, fMono, 0);
                using (var b = new SolidBrush(Theme.UpText)) g.DrawString(up, fMono, b, W - pad - us.Width, y);
                y += 17 * S;
                var gr = new RectangleF(pad, y, inner, 34 * S);
                Theme.Fill(g, gr, 5 * S, Theme.Soft);
                double cap = 1; foreach (var v in s.DownHist) if (!double.IsNaN(v) && v > cap) cap = v; foreach (var v in s.UpHist) if (!double.IsNaN(v) && v > cap) cap = v;
                Theme.Spark(g, s.DownHist, gr, Theme.Blue, cap * 1.15, true);
                Theme.Spark(g, s.UpHist, gr, Theme.Orange, cap * 1.15, false);
            }
            return bmp;
        }

        public static void Bar(Graphics g, string key, string label, double v, string txt, double yl, double rl, float x, ref float y, float inner, Dictionary<string, RectangleF> hits)
        {
            if (hits != null) hits[key] = new RectangleF(x - 4 * S, y - 2 * S, inner + 8 * S, 19 * S);
            int lv = double.IsNaN(v) ? 3 : v >= rl ? 2 : v >= yl ? 1 : 0;
            using (var b = new SolidBrush(Theme.Muted)) g.DrawString(label, fBody, b, x, y);
            var ts = Measure(txt, fMono, 0);
            float tw = Math.Max(44 * S, ts.Width + 6 * S);
            var tr = new RectangleF(x + 66 * S, y + 6 * S, Math.Max(10, inner - 66 * S - tw), 7 * S);
            Theme.Fill(g, tr, 3.5f * S, Theme.Track);
            if (!double.IsNaN(v)) Theme.Fill(g, new RectangleF(tr.X, tr.Y, Math.Max(7 * S, tr.Width * (float)Math.Min(100, v) / 100f), tr.Height), 3.5f * S, Theme.Lv(lv));
            using (var b = new SolidBrush(lv == 1 || lv == 2 ? Theme.Lv(lv) : Theme.Ink)) g.DrawString(txt, fMono, b, x + inner - ts.Width, y);
            y += 20 * S;
        }

        public static void PathStrip(Graphics g, Snap s, RectangleF r, bool big)
        {
            int n = 5; float slot = r.Width / n, dot = (big ? 18 : 12) * S; Font lf = big ? fName : fSmall;
            using (var line = new Pen(Theme.Rail, 2 * S))
                for (int i = 0; i < n - 1; i++) g.DrawLine(line, r.X + slot * i + slot / 2 + dot / 2 + 2 * S, r.Y + dot / 2, r.X + slot * (i + 1) + slot / 2 - dot / 2 - 2 * S, r.Y + dot / 2);
            for (int i = 0; i < n; i++)
            {
                float cx = r.X + slot * i + slot / 2; int lv = s.Path[i];
                using (var halo = new SolidBrush(Color.FromArgb(lv == 2 ? 70 : 38, Theme.Lv(lv)))) g.FillEllipse(halo, cx - dot / 2 - 5 * S, r.Y - 5 * S, dot + 10 * S, dot + 10 * S);
                using (var b = new SolidBrush(Theme.Lv(lv))) g.FillEllipse(b, cx - dot / 2, r.Y, dot, dot);
                if (big) using (var hi = new SolidBrush(Color.FromArgb(90, Color.White))) g.FillEllipse(hi, cx - dot / 4, r.Y + dot / 5, dot / 2, dot / 3);
                string lab = s.PathLabel[i] ?? ""; var ls = Measure(lab, lf, 0);
                using (var b = new SolidBrush(lv == 2 ? Theme.LvText(2) : Theme.Ink)) g.DrawString(lab, lf, b, cx - ls.Width / 2, r.Y + dot + 3 * S);
                if (big)
                {
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    {
                        using (var b = new SolidBrush(Theme.Sub)) g.DrawString(s.PathMain[i] ?? "", fBody, b, new RectangleF(cx - slot / 2 + 4 * S, r.Y + dot + 22 * S, slot - 8 * S, 18 * S), sf);
                        using (var b = new SolidBrush(Theme.Muted)) g.DrawString(s.PathSub[i] ?? "", fSmall, b, new RectangleF(cx - slot / 2 + 4 * S, r.Y + dot + 41 * S, slot - 8 * S, 18 * S), sf);
                    }
                }
            }
        }

        public static Bitmap DetailCard(Detail d)
        {
            int W = (int)(280 * S); float pad = 14 * S, inner = W - 2 * pad;
            float noteH = string.IsNullOrEmpty(d.Note) ? 0 : Measure(d.Note, fSmall, (int)inner).Height;
            float h = pad + 22 * S + d.Lines.Count * 18 * S + (d.Top.Count > 0 ? 19 * S + d.Top.Count * 20 * S : 0) + (noteH > 0 ? noteH + 6 * S : 0) + pad;
            int H = (int)Math.Ceiling(h);
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var g = Prep(bmp))
            using (var ink = new SolidBrush(Theme.Ink))
            using (var muted = new SolidBrush(Theme.Muted))
            {
                var cr = new RectangleF(0.5f, 0.5f, W - 1, H - 1);
                Theme.Fill(g, cr, 12 * S, Theme.Card); Theme.Stroke(g, cr, 12 * S, Theme.Line, 1);
                float y = pad;
                g.DrawString(d.Title, fName, ink, pad, y); y += 22 * S;
                foreach (var l in d.Lines)
                {
                    g.DrawString(l[0], fBody, muted, pad, y);
                    string v = Trunc(l[1], 30); var vs = Measure(v, fMono, 0);
                    using (var vb = new SolidBrush(l.Length > 2 ? Theme.LvText(int.Parse(l[2], CultureInfo.InvariantCulture)) : Theme.Ink)) g.DrawString(v, fMono, vb, W - pad - vs.Width, y);
                    y += 18 * S;
                }
                if (d.Top.Count > 0)
                {
                    y += 4 * S; g.DrawString(d.TopTitle, fTiny, muted, pad, y); y += 15 * S;
                    using (var lp = new Pen(Theme.Line2))
                        foreach (var t in d.Top)
                        {
                            g.DrawLine(lp, pad, y, W - pad, y);
                            var vs = Measure(t[1], fMono, 0);
                            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                                g.DrawString(t[0], fBody, ink, new RectangleF(pad, y + 3 * S, inner - vs.Width - 8 * S, 18 * S), sf);
                            g.DrawString(t[1], fMono, ink, W - pad - vs.Width, y + 3 * S);
                            y += 20 * S;
                        }
                }
                if (noteH > 0) { y += 6 * S; g.DrawString(d.Note, fSmall, muted, new RectangleF(pad, y, inner, noteH + 2)); }
            }
            return bmp;
        }
    }

    // A window drawn with per-pixel alpha that never takes the mouse (clicks go to whatever is below).
    sealed class LayeredForm : Form
    {
        public LayeredForm() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true; }
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x08000000 | 0x8; return cp; }
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        public Rectangle Placed { get; private set; }
        public void Present(Bitmap bmp, byte alpha, int x, int y)
        {
            IntPtr screen = Native.GetDC(IntPtr.Zero), mem = Native.CreateCompatibleDC(screen), hb = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hb = bmp.GetHbitmap(Color.FromArgb(0)); old = Native.SelectObject(mem, hb);
                var size = new Native.SIZE(bmp.Width, bmp.Height); var src = new Native.POINT(0, 0); var dst = new Native.POINT(x, y);
                var bf = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
                Native.UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref bf, 2);
                Placed = new Rectangle(x, y, bmp.Width, bmp.Height);
            }
            finally
            {
                if (Native.ReleaseDC(IntPtr.Zero, screen) == 0) Program.Log("ReleaseDC failed");
                if (hb != IntPtr.Zero) { Native.SelectObject(mem, old); Native.DeleteObject(hb); }
                Native.DeleteDC(mem);
            }
        }
        public void Vanish() { using (var b = new Bitmap(1, 1, PixelFormat.Format32bppArgb)) Present(b, 0, -10, -10); Placed = Rectangle.Empty; }
    }

    // ================================================================== own controls (no stock Windows look - the topogram lesson)
    sealed class DB : Panel { public DB() { DoubleBuffered = true; ResizeRedraw = true; } }

    // Rounded, owner-drawn button: Primary (blue), Chip (filter / toggle) or Tab. Keyboard: Tab to focus, Enter / Space to press.
    sealed class PillButton : Control
    {
        public enum K { Primary, Chip, Tab }
        public readonly K Kind; bool sel, hot, down;
        public bool Selected { get { return sel; } set { if (sel != value) { sel = value; Invalidate(); } } }
        string badge = ""; int badgeLv = -1;
        // A small count or flag at the end of a tab ("16", "3 new", "REC"). lv: -1 neutral, 0-2 green/amber/red, 4 blue.
        public void SetBadge(string b, int lv) { b = b ?? ""; if (b == badge && lv == badgeLv) return; badge = b; badgeLv = lv; Fit(); Invalidate(); }
        int BadgeW { get { return badge == "" ? 0 : TextRenderer.MeasureText(badge, Draw.fTiny).Width + (int)(16 * Draw.S); } }
        void Fit() { var sz = TextRenderer.MeasureText(Text, Font); Width = sz.Width + (int)((Kind == K.Chip ? 22 : 30) * Draw.S) + BadgeW; }
        public PillButton(string text, K kind)
        {
            Kind = kind; Text = text;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            Font = kind == K.Tab ? Draw.fBodyB : kind == K.Primary ? Draw.fBodyB : Draw.fBody;
            Cursor = Cursors.Hand; TabStop = true; BackColor = Theme.Card;
            var sz = TextRenderer.MeasureText(text, Font);
            float S = Draw.S;
            Size = new Size(sz.Width + (int)((kind == K.Chip ? 22 : 30) * S), (int)((kind == K.Tab ? 42 : kind == K.Primary ? 36 : 30) * S));
            Margin = new Padding(0, 0, (int)((kind == K.Tab ? 2 : 6) * S), 0);
            AccessibleRole = AccessibleRole.PushButton; AccessibleName = text;
        }
        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; OnClick(EventArgs.Empty); } base.OnKeyDown(e); }
        protected override void OnTextChanged(EventArgs e) { Fit(); Invalidate(); base.OnTextChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float S = Draw.S; var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (var b = new SolidBrush(Parent != null ? Parent.BackColor : BackColor)) g.FillRectangle(b, ClientRectangle);
            Color fg;
            if (Kind == K.Primary)
            {
                Color bg = !Enabled ? Theme.Grey : down ? Theme.BlueDark : hot ? Theme.BlueHot : Theme.Blue;
                Theme.Fill(g, r, 8 * S, bg); fg = Color.White;
            }
            else if (Kind == K.Chip)
            {
                Theme.Fill(g, r, r.Height / 2, sel ? Theme.BlueSoft : hot ? Theme.Soft : Theme.Card);
                Theme.Stroke(g, r, r.Height / 2, sel ? Theme.Blue : Theme.Line, 1);
                fg = sel ? Theme.BlueDark : Theme.Sub;
            }
            else
            {
                if (hot && !sel) Theme.Fill(g, new RectangleF(2, 4 * S, Width - 4, Height - 10 * S), 6 * S, Theme.Soft);
                fg = sel ? Theme.Blue : Theme.Sub;
                if (sel) using (var b = new SolidBrush(Theme.Blue)) g.FillRectangle(b, 6 * S, Height - 3 * S, Width - 12 * S, 3 * S);
            }
            int bw = BadgeW;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, Kind == K.Tab ? -(int)(1 * S) : 0, Width - bw, Height), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (bw > 0)
            {
                var br = new RectangleF(Width - bw - 6 * S, Height / 2f - 9 * S, bw - 4 * S, 18 * S);
                Color bb = badgeLv == 2 ? Theme.Red : badgeLv == 1 ? Theme.Amber : badgeLv == 0 ? Theme.Green : badgeLv == 4 ? Theme.Blue : Theme.Line2;
                Color bf = badgeLv >= 0 ? Color.White : Theme.Sub;
                Theme.Fill(g, br, 9 * S, bb);
                TextRenderer.DrawText(g, badge, Draw.fTiny, Rectangle.Round(br), bf, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            if (Focused && ShowFocusCues) Theme.Stroke(g, new RectangleF(1.5f, 1.5f, Width - 3, Height - 3), (Kind == K.Chip ? r.Height / 2 : 8 * S), Theme.BlueDark, 2);
        }
    }

    // A text box inside a rounded frame (the stock 3D border is gone).
    sealed class RoundBox : Panel
    {
        public readonly TextBox Box = new TextBox();
        public RoundBox(int width, Font font, string cue)
        {
            float S = Draw.S;
            DoubleBuffered = true; BackColor = Theme.Card;
            Size = new Size(width, (int)(36 * S)); Margin = new Padding(0, 0, (int)(10 * S), 0);
            Padding = new Padding((int)(12 * S), (int)((36 * S - font.Height) / 2), (int)(12 * S), 0);
            Box.BorderStyle = BorderStyle.None; Box.Font = font; Box.Dock = DockStyle.Fill; Box.BackColor = Theme.Card; Box.ForeColor = Theme.Ink;
            Box.GotFocus += delegate { Invalidate(); }; Box.LostFocus += delegate { Invalidate(); };
            var cueLbl = new Label { Text = cue, Dock = DockStyle.Fill, Font = font, ForeColor = Theme.Muted, BackColor = Theme.Card, AutoSize = false, Cursor = Cursors.IBeam, UseMnemonic = false };
            cueLbl.Click += delegate { Box.Focus(); };
            Controls.Add(cueLbl); Controls.Add(Box); cueLbl.BringToFront();
            Action sync = delegate { cueLbl.Visible = !Box.Focused && Box.TextLength == 0; };
            Box.GotFocus += delegate { sync(); }; Box.LostFocus += delegate { sync(); }; Box.TextChanged += delegate { sync(); };
            Click += delegate { Box.Focus(); };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Card)) g.FillRectangle(b, ClientRectangle);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Theme.Fill(g, r, 8 * Draw.S, Theme.Card);
            Theme.Stroke(g, r, 8 * Draw.S, Box.Focused ? Theme.Blue : Theme.Line, Box.Focused ? 2 : 1);
        }
    }

    // ================================================================== Check a destination
    sealed class DestResult
    {
        public static readonly string[] Steps = { "DNS", "Route", "Ping", "TCP", "TLS", "HTTP", "Path MTU", "Path" };
        public string Target = "", Verdict = "", Input = ""; public volatile string Note; public volatile List<string[]> Story, Findings; public volatile int Level = -1, Running = -1; public volatile bool Done; public DateTime At = DateTime.Now;
        public string[] Val = new string[8]; public int[] Lv = { -1, -1, -1, -1, -1, -1, -1, -1 };
        public readonly List<KeyValuePair<string, List<string[]>>> Cards = new List<KeyValuePair<string, List<string[]>>>();
        public void AddCard(string title, List<string[]> rows) { lock (Cards) Cards.Add(new KeyValuePair<string, List<string[]>>(title, rows)); }
        public List<KeyValuePair<string, List<string[]>>> CardsCopy() { lock (Cards) return Cards.ToList(); }
    }

    static class DestCheck
    {
        static readonly string[] NoHop = { "No hop answered", "routers on the way filter ICMP" }, Nobody = { "Nobody", "no app is connected to it now" }, MtuMethod = { "Method", "ping, don't fragment" };
        static readonly Regex hostRx = new Regex(@"^(?=.{1,253}$)([A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)*[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?$");

        // Only a host name, an IP address or an http(s) URL gets through. Nothing typed ever reaches a shell.
        public static bool Parse(string input, out string host, out int port, out bool tls, out string err)
        {
            host = ""; port = 443; tls = true; err = "";
            string s = (input ?? "").Trim();
            if (s.Length == 0 || s.Length > 300) { err = Voice.P("Give me a host, an IP or a URL first.", "Type a host, IP or URL."); return false; }
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Uri u; if (!Uri.TryCreate(s, UriKind.Absolute, out u) || string.IsNullOrEmpty(u.Host)) { err = "That URL doesn't parse."; return false; }
                host = u.IdnHost.Trim('[', ']'); port = u.Port; tls = u.Scheme == "https";
            }
            else
            {
                var m6 = Regex.Match(s, @"^\[([0-9A-Fa-f:.]+)\](?::(\d{1,5}))?$");
                IPAddress ip;
                if (m6.Success) { host = m6.Groups[1].Value; if (m6.Groups[2].Success) port = int.Parse(m6.Groups[2].Value, CultureInfo.InvariantCulture); }
                else if (IPAddress.TryParse(s, out ip) && s.Count(c => c == ':') != 1) host = s;
                else
                {
                    int colon = s.LastIndexOf(':');
                    if (colon > 0) { int p; if (!int.TryParse(s.Substring(colon + 1), out p)) { err = "The port after ':' must be a number."; return false; } port = p; s = s.Substring(0, colon); }
                    host = s;
                }
                tls = port != 80;
                if (tls && port != 443 && port != 8443 && port != 993 && port != 995 && port != 465 && port != 636 && port != 853) tls = false;
            }
            IPAddress tmp;
            if (port < 1 || port > 65535) { err = "Ports go from 1 to 65535."; return false; }
            if (!IPAddress.TryParse(host, out tmp) && !hostRx.IsMatch(host)) { err = Voice.P("That doesn't look like a host, an IP or a URL, boss.", "That is not a valid host, IP or URL."); return false; }
            return true;
        }

        public static void Run(string input, DestResult r, Action changed, Monitor m)
        {
            r.Input = (input ?? "").Trim();
            try
            {
                RunSteps(input, r, changed, m);
                // Only when something failed: is a Wi-Fi sign-in page (captive portal) the real reason?
                if (r.Level >= 1 && r.Target != "" && Cfg.Portal) Portal(r, changed);
            }
            finally { r.Running = -1; r.Done = true; changed(); }
        }

        // Same plain-HTTP probe Windows itself sends to decide "No internet / sign in required" (NCSI), so no new destination
        // leaves the PC. Only the status line and the Location host are read; the redirect is never followed.
        const string NcsiHost = "www.msftconnecttest.com", NcsiBody = "Microsoft Connect Test";
        static readonly string[] PortalAsked = { "Asked", "msftconnecttest.com (as Windows does)" }, HeadEnd = { "\r\n\r\n" };
        static void Portal(DestResult r, Action changed)
        {
            r.Note = Voice.P("One more thing - checking whether a Wi-Fi sign-in page is in the way...", "Checking for a captive portal (sign-in page)..."); changed();
            string resp = null, why = "";
            try
            {
                var dns = Task.Factory.StartNew(() => Dns.GetHostAddresses(NcsiHost));
                if (!dns.Wait(4000)) why = "no DNS answer";
                else
                {
                    var ip = dns.Result.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? dns.Result.FirstOrDefault();
                    if (ip == null) why = "no address";
                    else using (var sock = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                    {
                        var ar = sock.BeginConnect(ip, 80, null, null);
                        if (!ar.AsyncWaitHandle.WaitOne(4000)) why = "port 80 blocked";
                        else
                        {
                            sock.EndConnect(ar); sock.ReceiveTimeout = 4000;
                            var req = Encoding.ASCII.GetBytes("GET /connecttest.txt HTTP/1.1\r\nHost: " + NcsiHost + "\r\nUser-Agent: AdeeboMeter\r\nConnection: close\r\n\r\n");
                            sock.Send(req);
                            var buf = new byte[4096]; int got = 0, n;
                            try { while (got < buf.Length && (n = sock.Receive(buf, got, buf.Length - got, SocketFlags.None)) > 0) got += n; } catch (SocketException) { }
                            resp = Encoding.ASCII.GetString(buf, 0, got);
                        }
                    }
                }
            }
            catch (Exception e) { why = e is SocketException ? ((SocketException)e).SocketErrorCode.ToString() : "failed"; }
            string where; int kind = ClassifyPortal(resp, out where);   // 0 open, 1 portal, 2 can't tell
            if (resp == null) kind = 2;
            r.AddCard("SIGN-IN PAGE (CAPTIVE PORTAL)", new List<string[]> {
                PortalAsked,
                new[] { "Result", kind == 0 ? "none - internet is open" : kind == 1 ? "found" + (where != "" ? ": " + where : "") : "can't tell - " + (why != "" ? why : "odd answer"), kind == 1 ? "2" : "0" } });
            if (kind == 1)
                r.Verdict = Voice.P("Found the real culprit: a Wi-Fi sign-in page is holding everything hostage" + (where != "" ? " (" + where + ")" : "") + ". Open a browser, sign in or accept the terms, then check again.",
                                    "Captive portal detected" + (where != "" ? " (" + where + ")" : "") + ". Sign in through a browser, then retry.");
            if (kind == 1) r.Level = 2;
            r.Note = null;
        }

        // 0 = the expected answer (no portal), 1 = something else answered for it (portal), 2 = unknown
        public static int ClassifyPortal(string resp, out string where)
        {
            where = "";
            if (string.IsNullOrEmpty(resp)) return 2;
            var head = resp.Split(HeadEnd, 2, StringSplitOptions.None);
            var lines = head[0].Split('\n');
            var m = Regex.Match(lines[0].Trim(), @"^HTTP/\d(?:\.\d)?\s+(\d{3})");
            if (!m.Success) return 2;
            int status = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            string body = head.Length > 1 ? head[1].Trim() : "";
            if (status == 200 && body.StartsWith(NcsiBody, StringComparison.Ordinal)) return 0;
            if (status >= 300 && status < 400)
            {
                foreach (var l in lines)
                    if (l.StartsWith("Location:", StringComparison.OrdinalIgnoreCase))
                    {
                        Uri u; if (Uri.TryCreate(l.Substring(9).Trim(), UriKind.Absolute, out u) && (u.Scheme == "http" || u.Scheme == "https")) where = Draw.Trunc(u.Host, 40);
                    }
                return 1;
            }
            if (status == 200 || status == 511) return 1;   // page replaced, or "network authentication required"
            return 2;
        }

        static void RunSteps(string input, DestResult r, Action changed, Monitor m)
        {
            var deadline = DateTime.UtcNow.AddSeconds(45);
            string host; int port; bool tls; string err;
            if (!Parse(input, out host, out port, out tls, out err)) { Finish(r, 1, err); changed(); return; }
            r.Target = host + ":" + port;
            Action<int, int, string> step = delegate(int i, int lv, string val) { r.Val[i] = val; r.Lv[i] = lv; r.Running = i + 1; changed(); };
            Action<int> skipRest = delegate(int from) { for (int i = from; i < 8; i++) if (r.Lv[i] < 0) { r.Val[i] = "skipped"; r.Lv[i] = 3; } };
            r.Running = 0; changed();

            // 1. DNS (real lookup, skipping the cache)
            var addrs = new List<IPAddress>(); IPAddress lit; string dnsSrv; bool vpn; lock (m.L) { dnsSrv = m.DnsServer; vpn = m.VpnUp; }
            if (IPAddress.TryParse(host, out lit)) { addrs.Add(lit); step(0, 0, "IP given"); }
            else
            {
                var sw = Stopwatch.StartNew(); int rc4 = QueryAll(host, 1, addrs); QueryAll(host, 28, addrs); double ms = sw.Elapsed.TotalMilliseconds;
                if (addrs.Count == 0)
                {
                    step(0, 2, rc4 == 9003 ? "no such name" : rc4 == 1460 ? "timed out" : "failed (" + rc4 + ")");
                    r.AddCard("DNS", new List<string[]> { new[] { "Server", (dnsSrv != "" ? dnsSrv : "system default") + (vpn ? " (VPN)" : "") }, new[] { "Answer", rc4 == 9003 ? "NXDOMAIN - no such name" : "error " + rc4, "2" } });
                    skipRest(1);
                    CompareDns(r, host, new List<string>());
                    bool filtered = r.Findings != null && r.Findings.Any(f => f[1] == "2");
                    if (filtered) { Finish(r, 2, Voice.P("Your DNS says " + host + " doesn't exist - but a public resolver finds it. A DNS filter (AdGuard, a company filter or the VPN's DNS) is blocking this name.", "Blocked by your DNS: the name resolves on a public resolver.")); changed(); return; }
                    Finish(r, 2, rc4 == 9003 ? Voice.P("Can't even find its address, boss - the DNS server says that name doesn't exist. Typo, or an internal name that needs the VPN?", "DNS says the name does not exist. Check the spelling, or connect the VPN for internal names.")
                                             : Voice.P("DNS didn't answer for " + host + ". The server (" + dnsSrv + ") is slow or unreachable.", "DNS lookup failed for " + host + " (server " + dnsSrv + ")."));
                    changed(); return;
                }
                step(0, ms > 300 ? 1 : 0, ms.ToString("0", CultureInfo.CurrentCulture) + " ms");
                var dc = new List<string[]> { new[] { "Server", (dnsSrv != "" ? dnsSrv : "system default") + (vpn ? " (VPN)" : "") }, new[] { "Time", ms.ToString("0", CultureInfo.CurrentCulture) + " ms" } };
                foreach (var a in addrs.Take(4)) dc.Add(new[] { a.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4", a.ToString() });
                r.AddCard("DNS", dc);
            }
            var target = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addrs[0];
            if (!IPAddress.TryParse(host, out lit)) CompareDns(r, host, addrs.Where(a => a.AddressFamily == AddressFamily.InterNetwork).Select(a => a.ToString()).ToList());

            // 2. Route: which adapter the OS would use (a UDP "connect" sends nothing)
            string via = "?"; bool viaVpn = false; string localIp = "";
            try
            {
                using (var u = new Socket(target.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
                {
                    u.Connect(target, port); localIp = ((IPEndPoint)u.LocalEndPoint).Address.ToString();
                    foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                        foreach (var ua in n.GetIPProperties().UnicastAddresses)
                            if (ua.Address.ToString() == localIp) { via = n.Name; viaVpn = Monitor.IsVpn(n); }
                }
                step(1, 0, viaVpn ? "VPN" : Draw.Trunc(via, 14));
            }
            catch { step(1, 2, "no route"); }
            r.AddCard("ROUTE", new List<string[]> { new[] { "Leaves via", via + (viaVpn ? " (VPN)" : "") }, new[] { "Local address", localIp }, new[] { "Proxy (browsers)", ProxyInfo() } });

            // 3. Ping
            int ok = 0; double sum = 0;
            for (int i = 0; i < 4 && DateTime.UtcNow < deadline; i++) { double t = Monitor.PingMs(target.ToString()); if (!double.IsNaN(t)) { ok++; sum += t; } }
            bool icmp = ok > 0;
            step(2, icmp ? (ok < 4 ? 1 : 0) : 3, icmp ? (sum / ok).ToString("0", CultureInfo.CurrentCulture) + " ms · " + ((4 - ok) * 25) + "%" : "no reply");

            // 4. TCP connect
            Socket sock = null; string tcpErr = ""; double tcpMs = 0, tcpFailMs = 0; Stopwatch tcpSw = null;
            try
            {
                sock = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                tcpSw = Stopwatch.StartNew(); var ar = sock.BeginConnect(target, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(4000)) { tcpErr = "timeout"; sock.Close(); sock = null; }
                else { sock.EndConnect(ar); tcpMs = tcpSw.Elapsed.TotalMilliseconds; }
            }
            catch (SocketException se) { tcpFailMs = tcpSw != null ? tcpSw.Elapsed.TotalMilliseconds : 0; tcpErr = se.SocketErrorCode == SocketError.ConnectionRefused ? "refused" : se.SocketErrorCode.ToString(); if (sock != null) sock.Close(); sock = null; }
            catch (ObjectDisposedException) { tcpErr = "timeout"; sock = null; }
            if (sock == null)
            {
                step(3, 2, tcpErr); skipRest(4);
                // Even when the port fails, the path tells us a lot (firewall or not) - trace it and tell the story.
                if (target.AddressFamily == AddressFamily.InterNetwork && DateTime.UtcNow < deadline)
                {
                    r.Note = Voice.P("Port's shut - tracing the path to see who shut it...", "Tracing the path..."); changed();
                    var fh = Trace(target, 20); int fl = fh.FindIndex(hp => hp.Item1 == target.ToString());
                    Story(r, target, fh, fl, localIp, viaVpn, 0, tcpErr, 0, tcpFailMs, icmp ? sum / ok : double.NaN, port);
                    r.Note = null;
                }
                Finish(r, 2, tcpErr == "refused"
                    ? Voice.P("The server is there but slammed the door on port " + port + " (connection refused). Wrong port, or the service isn't running.", "Connection refused on port " + port + ". Wrong port or service not running.")
                    : Voice.P("Address found, but nobody answers on port " + port + ". A firewall is quietly eating the packets, or the service is down.", "No answer on port " + port + " (timeout). Likely a firewall or the service is down."));
                Who(r, m, addrs); changed(); return;
            }
            step(3, tcpMs > 300 ? 1 : 0, tcpMs.ToString("0", CultureInfo.CurrentCulture) + " ms");

            // 5. TLS - TLS 1.2 / 1.3 only, and real Windows certificate validation (never TLS 1.0/1.1, never skip certificate checks).
            // The callback only *records* the certificate and the verdict so they can be reported; it returns Windows' own answer,
            // so an untrusted certificate ends the handshake and nothing at all is sent to that server.
            bool tlsStalled = false, tlsUntrusted = false, tlsFailed = false, tlsLegacy = false; Stream stream = new NetworkStream(sock, true);
            if (tls)
            {
                SslPolicyErrors errs = SslPolicyErrors.None; X509Certificate2 cert = null;
                var ssl = new SslStream(stream, false, delegate(object o, X509Certificate c, X509Chain ch, SslPolicyErrors e)
                {
                    errs = e; if (c != null) cert = new X509Certificate2(c);
                    return e == SslPolicyErrors.None;
                });
                ssl.ReadTimeout = 6000; ssl.WriteTimeout = 6000;
                var sw = Stopwatch.StartNew(); string sni = IPAddress.TryParse(host, out lit) ? target.ToString() : host;
                var allowed = SslProtocols.Tls12 | (Tls13Client() ? (SslProtocols)12288 : 0);
                var task = Task.Factory.StartNew(() => ssl.AuthenticateAsClient(sni, null, allowed, true));
                bool done = false; string tErr = "";
                try { done = task.Wait(8000); } catch (AggregateException ae) { Exception ie = ae; while (ie.InnerException != null) ie = ie.InnerException; tErr = ie.Message; done = true; }
                tlsUntrusted = cert != null && errs != SslPolicyErrors.None;
                if (!done) { tlsStalled = true; step(4, 2, "stalls"); try { ssl.Dispose(); } catch { } stream = Stream.Null; }
                else if (tlsUntrusted || tErr == "")
                {
                    string proto = tlsUntrusted ? "-" : ProtoName(ssl.SslProtocol);
                    int days = cert != null ? (int)(cert.NotAfter - DateTime.Now).TotalDays : 0;
                    step(4, tlsUntrusted ? 2 : days < 14 ? 1 : 0, tlsUntrusted ? "untrusted" : proto + " · " + sw.Elapsed.TotalMilliseconds.ToString("0", CultureInfo.CurrentCulture) + " ms");
                    if (cert != null)
                        r.AddCard("TLS CERTIFICATE", new List<string[]> {
                            new[] { "Name", Draw.Trunc(cert.GetNameInfo(X509NameType.SimpleName, false), 32) },
                            new[] { "Issuer", Draw.Trunc(cert.GetNameInfo(X509NameType.SimpleName, true), 32), tlsUntrusted ? "2" : "0" },
                            new[] { "Expires", days < 0 ? "expired " + (-days) + " days ago" : "in " + days + " days", days < 14 ? "2" : "0" },
                            new[] { "Version", tlsUntrusted ? "not completed" : proto },
                            new[] { "Trusted by Windows", tlsUntrusted ? "no - " + errs : "yes", tlsUntrusted ? "2" : "0" } });
                    if (tlsUntrusted) { try { ssl.Dispose(); } catch { } stream = Stream.Null; }
                    else stream = ssl;
                }
                else
                {
                    // handshake refused before any certificate: most often the server only speaks TLS 1.0 / 1.1
                    tlsLegacy = cert == null && (tErr.IndexOf("algorithm", StringComparison.OrdinalIgnoreCase) >= 0 || tErr.IndexOf("protocol", StringComparison.OrdinalIgnoreCase) >= 0 || tErr.IndexOf("handshake", StringComparison.OrdinalIgnoreCase) >= 0);
                    tlsFailed = true; step(4, 2, tlsLegacy ? "legacy TLS?" : "failed");
                    r.AddCard("TLS", new List<string[]> { new[] { "Error", Draw.Trunc(tErr, 40), "2" }, new[] { "Offered by AdeeboMeter", Tls13Client() ? "TLS 1.2, 1.3" : "TLS 1.2" } });
                    try { ssl.Dispose(); } catch { } stream = Stream.Null;
                }
            }
            else step(4, 3, "not TLS");

            // 6. HTTP (one HEAD request, nothing else sent)
            int status = 0;
            if ((port == 80 || port == 8080 || tls) && stream != Stream.Null && stream.CanWrite)
            {
                try
                {
                    var req = Encoding.ASCII.GetBytes("HEAD / HTTP/1.1\r\nHost: " + host + "\r\nUser-Agent: AdeeboMeter\r\nConnection: close\r\n\r\n");
                    var sw = Stopwatch.StartNew(); stream.ReadTimeout = 6000; stream.Write(req, 0, req.Length);
                    var buf = new byte[256]; int n = stream.Read(buf, 0, buf.Length); double ttfb = sw.Elapsed.TotalMilliseconds;
                    var line = Encoding.ASCII.GetString(buf, 0, Math.Max(0, n)).Split('\n')[0].Trim();
                    var mm = Regex.Match(line, @"^HTTP/\d(\.\d)?\s+(\d{3})");
                    if (mm.Success)
                    {
                        status = int.Parse(mm.Groups[2].Value, CultureInfo.InvariantCulture);
                        step(5, status >= 500 ? 2 : status >= 400 ? 1 : 0, status + " · " + ttfb.ToString("0", CultureInfo.CurrentCulture) + " ms");
                        r.AddCard("HTTP", new List<string[]> { new[] { "Status", Draw.Trunc(line, 32) }, new[] { "Time to first byte", ttfb.ToString("0", CultureInfo.CurrentCulture) + " ms" } });
                    }
                    else step(5, 3, "not HTTP");
                }
                catch { step(5, 1, "no answer"); }
            }
            else step(5, 3, "skipped");
            try { stream.Dispose(); } catch { }
            try { sock.Close(); } catch { }

            // 7. Path MTU (IPv4, don't-fragment pings)
            int mtu = 0;
            if (target.AddressFamily == AddressFamily.InterNetwork && icmp && DateTime.UtcNow < deadline)
            {
                if (DfPing(target, 1472)) mtu = 1500;
                else
                {
                    int lo = 1172, hi = 1471; if (!DfPing(target, lo)) lo = 548;
                    for (int i = 0; i < 9 && lo < hi; i++) { int mid = (lo + hi + 1) / 2; if (DfPing(target, mid)) lo = mid; else hi = mid - 1; }
                    mtu = lo + 28;
                }
                int lmtu; lock (m.L) lmtu = viaVpn && m.VpnMtu > 0 ? m.VpnMtu : m.PrimaryMtu;
                step(6, mtu < 1500 && tlsStalled ? 2 : mtu < 1500 ? 1 : 0, mtu.ToString(CultureInfo.CurrentCulture));
                r.AddCard("PATH MTU", new List<string[]> { new[] { "Largest that passes", mtu + " bytes", mtu < 1500 ? "1" : "0" }, new[] { "Your adapter sends", lmtu > 0 ? lmtu + " bytes" : "?" }, MtuMethod });
            }
            else step(6, 3, icmp ? "IPv6" : "ICMP blocked");

            // 8. Path: MTR-style, 3 rounds per hop, all hops at once (~3 s)
            if (target.AddressFamily == AddressFamily.InterNetwork && DateTime.UtcNow < deadline)
            {
                var multi = new List<int>();
                var hops = Trace(target, 20, multi);
                int last = hops.FindIndex(hp => hp.Item1 == target.ToString());
                var rows = new List<string[]>();
                int upto = last >= 0 ? last : Math.Min(hops.Count, 12) - 1;
                for (int i = 0; i <= upto && i < hops.Count; i++)
                    rows.Add(new[] { (i + 1) + "  " + (hops[i].Item1 ?? "*"), hops[i].Item1 == null ? "no reply" : hops[i].Item2.ToString("0", CultureInfo.CurrentCulture) + " ms · loss " + hops[i].Item3 + "%", hops[i].Item3 > 0 && i == upto ? "1" : "0" });
                if (hops.All(hp => hp.Item1 == null)) rows = new List<string[]> { NoHop };
                if (rows.Count > 10) rows = rows.Take(4).Concat(new[] { new[] { "...", (rows.Count - 7) + " more hops" } }).Concat(rows.Skip(rows.Count - 3)).ToList();
                r.AddCard("PATH (3 ROUNDS PER HOP)", rows);
                step(7, last >= 0 ? (hops[last].Item3 > 0 ? 1 : 0) : 3, last >= 0 ? (last + 1) + " hops" : "not traced");
                r.Note = Voice.P("Reading the router names along the way...", "Naming the hops..."); changed();
                Story(r, target, hops, last, localIp, viaVpn, mtu, "", tcpMs, 0, icmp ? sum / ok : double.NaN, port, null, multi);
                r.Note = null;
            }
            else step(7, 3, "skipped");

            Who(r, m, addrs);
            if (tlsStalled && mtu > 0 && mtu < 1500) Finish(r, 2, Voice.P("Classic MTU black hole: small packets walk through, big ones vanish. Path MTU is " + mtu + " - TLS stalls at the certificate.", "Path MTU is " + mtu + "; large packets are dropped, so TLS stalls. Check MSS clamping on the path."));
            else if (tlsStalled) Finish(r, 2, Voice.P("TCP connects, then TLS just... stops. Something in the middle is holding the handshake - a proxy, a firewall, or an MTU problem.", "TLS handshake stalls after TCP connects (proxy, firewall or MTU)."));
            else if (tlsUntrusted) Finish(r, 2, Voice.P("Gets to the door but the bouncer won't let you in: Windows doesn't trust the certificate (often an inspection proxy).", "TLS works but the certificate is not trusted by Windows (often an inspection proxy)."));
            else if (tlsFailed && tlsLegacy) Finish(r, 2, Voice.P("TCP is fine, but the server won't do TLS 1.2 or newer - it's stuck in the past (TLS 1.0 / 1.1). That's on the server.", "TLS handshake failed: the server likely supports only TLS 1.0/1.1."));
            else if (tlsFailed) Finish(r, 2, Voice.P("TCP is fine, TLS fails. The two sides can't agree on how to talk securely.", "TLS handshake failed."));
            else if (status == 401 || status == 403 || status == 407) Finish(r, 1, Voice.P("The network's fine and the server answers - it just won't let you in (HTTP " + status + "). Sign in, VPN, or a proxy rule.", "Reachable, but access is refused (HTTP " + status + ")."));
            else if (status >= 500) Finish(r, 2, Voice.P("The network's fine - the site itself is broken (HTTP " + status + "). Not you, boss.", "Network is fine; the server returns HTTP " + status + "."));
            else
            {
                int worst = r.Lv.Max(x => x == 3 ? 0 : x);
                string ttfbTxt = r.Lv[5] == 0 && r.Val[5] != null && r.Val[5].Contains("·") ? " - " + r.Val[5].Split('·').Last().Trim() + " to first byte" : "";
                Finish(r, worst >= 1 ? 1 : 0, Voice.P(host + " is happy to see you" + ttfbTxt + ". If it feels slow, it isn't the network.", host + " is reachable" + ttfbTxt + "."));
            }
            changed();
        }


        // ================================================================ The path story
        // Offline, AdeeboMeter can't look up AS numbers - but routers usually have names (reverse DNS), and the name says who runs
        // them. Where the owner changes, your traffic crossed from one network to another: that's a BGP peering / transit handoff.
        // Names are looked up only here, only for a check you started, through your normal DNS server.
        static readonly string[][] Carriers =
        {
            new[] { "rogers.com", "Rogers" }, new[] { "bell.ca", "Bell" }, new[] { "bell.net", "Bell" }, new[] { "telus.net", "TELUS" }, new[] { "telus.com", "TELUS" },
            new[] { "videotron.ca", "Videotron" }, new[] { "shawcable.net", "Shaw / Rogers" }, new[] { "cogentco.com", "Cogent" }, new[] { "ntt.net", "NTT" },
            new[] { "level3.net", "Lumen (Level 3)" }, new[] { "lumen.com", "Lumen" }, new[] { "he.net", "Hurricane Electric" }, new[] { "telia.net", "Arelion (Telia)" },
            new[] { "arelion.net", "Arelion" }, new[] { "zayo.com", "Zayo" }, new[] { "gtt.net", "GTT" }, new[] { "comcast.net", "Comcast" }, new[] { "alter.net", "Verizon" },
            new[] { "verizon-gni.net", "Verizon" }, new[] { "att.net", "AT&T" }, new[] { "cox.net", "Cox" }, new[] { "rr.com", "Charter Spectrum" }, new[] { "charter.com", "Charter Spectrum" },
            new[] { "1e100.net", "Google" }, new[] { "google.com", "Google" }, new[] { "amazonaws.com", "Amazon AWS" }, new[] { "msn.net", "Microsoft" },
            new[] { "microsoft.com", "Microsoft" }, new[] { "akamaitechnologies.com", "Akamai" }, new[] { "akamai.net", "Akamai" }, new[] { "cloudflare.com", "Cloudflare" },
            new[] { "fastly.net", "Fastly" }, new[] { "as6453.net", "Tata Communications" }, new[] { "tatacommunications.com", "Tata Communications" },
            new[] { "seabone.net", "Sparkle" }, new[] { "pccwglobal.net", "PCCW Global" }, new[] { "telstraglobal.net", "Telstra" }, new[] { "starlinkisp.net", "Starlink" },
            new[] { "github.com", "GitHub" }, new[] { "facebook.com", "Meta" }, new[] { "fbcdn.net", "Meta" }, new[] { "torix.ca", "TorIX (internet exchange)" },
            new[] { "ottix.net", "OttIX (internet exchange)" }, new[] { "equinix.com", "Equinix (exchange)" }, new[] { "de-cix.net", "DE-CIX (exchange)" }, new[] { "ams-ix.net", "AMS-IX (exchange)" }
        };
        static readonly string[][] Cities =
        {
            new[] { "yyz", "Toronto" }, new[] { "tor", "Toronto" }, new[] { "toronto", "Toronto" }, new[] { "yow", "Ottawa" }, new[] { "ott", "Ottawa" }, new[] { "ottawa", "Ottawa" },
            new[] { "yul", "Montreal" }, new[] { "mtl", "Montreal" }, new[] { "mon", "Montreal" }, new[] { "yvr", "Vancouver" }, new[] { "van", "Vancouver" }, new[] { "yyc", "Calgary" },
            new[] { "nyc", "New York" }, new[] { "jfk", "New York" }, new[] { "ewr", "Newark" }, new[] { "iad", "Washington DC" }, new[] { "ash", "Ashburn" }, new[] { "wdc", "Washington DC" },
            new[] { "ord", "Chicago" }, new[] { "chi", "Chicago" }, new[] { "dfw", "Dallas" }, new[] { "dal", "Dallas" }, new[] { "lax", "Los Angeles" }, new[] { "sjc", "San Jose" },
            new[] { "sfo", "San Francisco" }, new[] { "sea", "Seattle" }, new[] { "atl", "Atlanta" }, new[] { "mia", "Miami" }, new[] { "bos", "Boston" }, new[] { "den", "Denver" },
            new[] { "lhr", "London" }, new[] { "lon", "London" }, new[] { "fra", "Frankfurt" }, new[] { "ams", "Amsterdam" }, new[] { "par", "Paris" }, new[] { "cdg", "Paris" },
            new[] { "sin", "Singapore" }, new[] { "hkg", "Hong Kong" }, new[] { "nrt", "Tokyo" }, new[] { "tyo", "Tokyo" }, new[] { "syd", "Sydney" }, new[] { "dxb", "Dubai" },
            new[] { "bom", "Mumbai" }, new[] { "del", "Delhi" }, new[] { "dac", "Dhaka" }, new[] { "mrs", "Marseille" }
        };
        static readonly string[] TwoLevel = { "co.uk", "com.au", "net.au", "co.jp", "ne.jp", "co.nz", "com.br", "com.cn", "net.cn", "co.in", "net.in", "com.bd", "net.bd", "com.sg" };
        static readonly char[] LabelSplit = { '.', '-' };

        public static string OwnerOf(string ptr)
        {
            if (string.IsNullOrEmpty(ptr)) return null;
            var parts = ptr.TrimEnd('.').ToLowerInvariant().Split('.');
            if (parts.Length < 2) return null;
            string two = parts[parts.Length - 2] + "." + parts[parts.Length - 1];
            string dom = TwoLevel.Contains(two) && parts.Length >= 3 ? parts[parts.Length - 3] + "." + two : two;
            foreach (var c in Carriers) if (dom == c[0]) return c[1];
            return dom;
        }
        public static string CityOf(string ptr)
        {
            if (string.IsNullOrEmpty(ptr)) return null;
            var labels = ptr.ToLowerInvariant().Split(LabelSplit);
            foreach (var l in labels.Take(Math.Max(0, labels.Length - 2)))   // skip the domain itself
            {
                string t = Regex.Replace(l, @"\d+$", "");
                foreach (var c in Cities) if (t == c[0]) return c[1];
            }
            return null;
        }
        public static string Kind(string ip)
        {
            IPAddress a; if (ip == null || !IPAddress.TryParse(ip, out a) || a.AddressFamily != AddressFamily.InterNetwork) return "public";
            var b = a.GetAddressBytes();
            if (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168)) return "private";
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return "cgnat";
            if (b[0] == 169 && b[1] == 254) return "linklocal";
            return "public";
        }
        static Dictionary<string, string> Ptrs(IEnumerable<string> ips)
        {
            var tasks = ips.Distinct().Select(ip => Task.Factory.StartNew(() =>
            {
                try { var h = Dns.GetHostEntry(IPAddress.Parse(ip)).HostName; return Tuple.Create(ip, h != ip ? h : null); }
                catch { return Tuple.Create(ip, (string)null); }
            })).ToArray();
            try { Task.WaitAll(tasks, 3000); } catch (AggregateException) { }
            return tasks.Where(t => t.Status == TaskStatus.RanToCompletion && t.Result.Item2 != null).ToDictionary(t => t.Result.Item1, t => t.Result.Item2);
        }
        // Initial TTL guess: replies start at 64 (Linux, most servers), 128 (Windows) or 255 (network gear).
        static int ReplyTtl(IPAddress target)
        {
            try { using (var p = new Ping()) { var rep = p.Send(target, 1000); return rep.Status == IPStatus.Success && rep.Options != null ? rep.Options.Ttl : -1; } }
            catch { return -1; }
        }

        static string[] Row(params string[] v) { return v; }
        // Ask a public resolver the same question. Different answers are usually a CDN picking a nearby server;
        // "blocked" answers (0.0.0.0, 127.x, NXDOMAIN while the public resolver finds it) mean a filter is in the way.
        static void CompareDns(DestResult r, string host, List<string> mine)
        {
            string err; var pub = DnsAsk.Ask(host, "1.1.1.1", 2500, out err);
            if (err != "") pub = DnsAsk.Ask(host, "8.8.8.8", 2500, out err);
            if (err != "") { r.AddCard("DNS SECOND OPINION", new List<string[]> { Row("Public resolver", "couldn't ask (" + err + ") - the network may only allow its own DNS") }); return; }
            bool pubNx = pub.Count == 1 && pub[0] == "NXDOMAIN"; var pubIps = pub.Where(x => x != "NXDOMAIN").ToList();
            bool blocked = mine.Count > 0 && mine.All(ip => ip == "0.0.0.0" || ip.StartsWith("127.", StringComparison.Ordinal)) && pubIps.Count > 0;
            bool missing = mine.Count == 0 && pubIps.Count > 0;
            bool same = mine.Intersect(pubIps).Any();
            string verdict = blocked || missing ? "your DNS blocks this name" : pubNx ? "public DNS doesn't know it (internal name?)" : same ? "same answer - nothing fishy" : "different answer (usually a nearby CDN server)";
            r.AddCard("DNS SECOND OPINION", new List<string[]> { Row("Public resolver (1.1.1.1)", pubNx ? "no such name" : string.Join(", ", pubIps.Take(3))), Row("Verdict", verdict, blocked || missing ? "2" : same || pubNx ? "0" : "1") });
            var f = r.Findings ?? new List<string[]>();
            if (blocked || missing) f.Add(Row("Your DNS answered " + (missing ? "'no such name'" : string.Join(", ", mine.Take(2))) + ", but a public resolver finds " + string.Join(", ", pubIps.Take(2)) + ". Something is filtering names - a DNS blocker (like AdGuard), a corporate filter, or the VPN's DNS.", "2"));
            else if (!same && !pubNx && mine.Count > 0) f.Add(Row("Your DNS and a public resolver gave different addresses. That's normal for big sites (each DNS gets a nearby server). If the TLS check below passes, the answer is genuine - certificates can't be faked that easily.", "-1"));
            r.Findings = f;
        }

        public static void Story(DestResult r, IPAddress target, List<Tuple<string, double, int>> hops, int last, string localIp, bool viaVpn, int mtu, string tcpErr, double tcpMs, double tcpFailMs, double pingAvg, int port, Dictionary<string, string> names = null, List<int> multi = null)
        {
            var seg = new List<string[]>(); var find = new List<string[]>(r.Findings ?? new List<string[]>());   // keep what earlier steps found (DNS)
            int upto = last >= 0 ? last : Math.Min(hops.Count, 15) - 1;
            while (upto >= 0 && hops[upto].Item1 == null) upto--;
            var answered = hops.Take(upto + 1).Where(h => h.Item1 != null).Select(h => h.Item1).ToList();
            var ptr = names ?? Ptrs(answered);
            Func<string, string> P = ip => { string v; return ip != null && ptr.TryGetValue(ip, out v) ? v : null; };
            Func<double, string> ms = v => v.ToString("0", CultureInfo.CurrentCulture) + " ms";

            // 1. walk the hops into segments
            seg.Add(new[] { "Your PC", localIp == "" ? "this laptop" : localIp, "you" });
            string curKey = null; int curFirst = -1, curCount = 0; double curStart = 0, curEnd = 0; string curCity = null, curCityEnd = null;
            Action flush = delegate
            {
                if (curKey == null) return;
                string kind = curKey == "Your network" ? "lan" : curKey == "Carrier-grade NAT" ? "cgnat" : curKey == "Unnamed routers" ? "hidden" : "net";
                string sub = curCount + (curCount == 1 ? " hop" : " hops") + (curEnd > curStart + 1 ? " · +" + ms(curEnd - curStart) : "") + (curCity != null ? " · " + curCity + "?" + (curCityEnd != null && curCityEnd != curCity ? " → " + curCityEnd + "?" : "") : curCityEnd != null ? " · " + curCityEnd + "?" : "");
                seg.Add(new[] { curKey, sub, kind, (curFirst + 1).ToString(CultureInfo.InvariantCulture) });
            };
            double prevMs = 0; string bigFrom = null, bigTo = null; double bigJump = 0; int privateHops = 0; bool sawCgnat = false, sawPublic = false; int silentMid = 0;
            for (int i = 0; i <= upto; i++)
            {
                var h = hops[i];
                if (h.Item1 == null) { if (i < upto) silentMid++; continue; }
                string kind = Kind(h.Item1), name = P(h.Item1), owner;
                if (kind == "private" || kind == "linklocal") { owner = sawPublic ? "Unnamed routers" : "Your network"; if (!sawPublic) privateHops++; }
                else if (kind == "cgnat") { owner = "Carrier-grade NAT"; sawCgnat = true; }
                else { sawPublic = true; owner = OwnerOf(name) ?? "Unnamed routers"; }
                if (i == last) owner = OwnerOf(name) ?? "Destination";
                if (owner != curKey)
                {
                    if (curKey != null && owner != "Unnamed routers" && curKey != "Unnamed routers" && curKey != "Your network" && curKey != "Carrier-grade NAT" && owner != "Destination")
                        find.Add(new[] { "Handoff at hop " + (i + 1) + ": " + curKey + " → " + owner + ". Two networks meet here - this is where BGP decides your route (peering or paid transit).", "-1" });
                    flush(); curKey = owner; curFirst = i; curCount = 0; curStart = prevMs; curCity = null; curCityEnd = null;
                }
                curCount++; curEnd = h.Item2; string cty = CityOf(name); if (curCity == null) curCity = cty; else if (cty != null) curCityEnd = cty;
                if (prevMs > 0 && h.Item2 - prevMs > bigJump) { bigJump = h.Item2 - prevMs; bigFrom = (i) + ""; bigTo = (i + 1) + ""; }
                prevMs = Math.Max(prevMs, h.Item2);
            }
            flush();
            if (last < 0) seg.Add(Row("Destination", "didn't answer the trace", "dest", ""));
            else seg[seg.Count - 1][2] = "dest";

            // 2. what's in the way
            bool localPrivate = Kind(localIp) == "private";
            if (viaVpn) find.Add(Row("Tunnel: your traffic is wrapped in the VPN. The routers inside the tunnel are invisible from here; the first hops you see are on the company side.", "-1"));
            if (localPrivate && privateHops >= 2 && !viaVpn) find.Add(new[] { "Double NAT: " + privateHops + " routers with private addresses before the internet - usually your provider's modem plus your own router. It works, but can upset VPNs, games and calls.", "1" });
            else if (localPrivate && !viaVpn) find.Add(new[] { "NAT: your router swaps your private address (" + localIp + ") for its public one on the way out. Normal for home and office networks.", "0" });
            if (sawCgnat) find.Add(Row("Carrier-grade NAT: your provider shares one public address between many customers (100.64.x.x hops). Outgoing traffic is fine; nobody can reach you directly.", "1"));
            if (mtu > 0 && mtu < 1500)
                find.Add(new[] { mtu == 1492 ? "Packet size 1492: a PPPoE link (typical for DSL / fibre logins) - 8 bytes of wrapping." : mtu >= 1400 ? "Packet size " + mtu + ": something on the path wraps your traffic (VPN, tunnel or LTE / satellite link)." : "Packet size " + mtu + ": several layers of tunnels on the path - big packets get split or dropped.", "1" });
            if (multi != null && multi.Count > 0)
            {
                var hs = multi.Where(h => h <= upto + 1).OrderBy(h => h).ToList();
                if (hs.Count > 0) find.Add(Row((hs.Count == 1 ? "Hop " + hs[0] + " answered" : "Hops " + string.Join(", ", hs) + " answered") + " from different routers on each try: the network spreads traffic over parallel links (ECMP load balancing). Normal for big networks - but it means each connection may take a slightly different road.", "-1"));
            }
            if (silentMid > 0) find.Add(new[] { silentMid + (silentMid == 1 ? " router" : " routers") + " on the way stayed silent. Normal - many routers don't answer traceroute, and it doesn't slow your traffic.", "-1" });
            if (bigJump >= 30 && bigFrom != null) find.Add(new[] { "Most of the delay (+" + ms(bigJump) + ") is added between hop " + bigFrom + " and " + bigTo + " - usually distance (a long fibre run or an ocean), sometimes a busy link.", bigJump >= 100 ? "1" : "-1" });

            // firewalls
            if (tcpErr == "refused" && !double.IsNaN(pingAvg) && tcpFailMs > 0 && tcpFailMs < pingAvg * 0.5)
                find.Add(new[] { "The 'no' came back in " + ms(tcpFailMs) + ", but the server is " + ms(pingAvg) + " away. Something closer than the server rejected port " + port + " - most likely a firewall or proxy on the way.", "2" });
            else if (tcpErr == "refused") find.Add(new[] { "The server itself answered 'nothing listens on port " + port + "' - not a firewall.", "1" });
            else if (tcpErr == "timeout" && last >= 0) find.Add(new[] { "The server answers ping, but port " + port + " gets silence - a firewall is quietly dropping that port.", "2" });
            else if (tcpErr == "timeout") find.Add(new[] { "Silence on port " + port + " and the trace never reached the server - it's down, or a firewall hides it completely.", "2" });
            else if (last < 0 && tcpMs > 0) find.Add(Row("The trace stops before the server, yet your port connects fine: a firewall near the server blocks traceroute but lets real traffic through.", "0"));

            // return path
            if (last >= 0 && names == null)
            {
                int ttl = ReplyTtl(target);
                if (ttl > 0)
                {
                    int init = ttl <= 64 ? 64 : ttl <= 128 ? 128 : 255, back = init - ttl + 1, fwd = last + 1;
                    string os = init == 64 ? "Linux / most servers" : init == 128 ? "Windows" : "network equipment";
                    if (Math.Abs(back - fwd) >= 3) find.Add(new[] { "The way back is different: " + fwd + " hops there, about " + back + " hops back. Internet routes are often one-way streets - normal, but it means a trace only shows half the story.", "-1" });
                    find.Add(new[] { "The reply started life with TTL " + init + " - the far end looks like " + os + ".", "-1" });
                }
            }
            r.Story = seg; r.Findings = find;
        }

        // Windows' TLS 1.3 client arrived with Windows 11 / Server 2022 (build 20348+); older builds get TLS 1.2 only.
        static bool Tls13Client()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                { int b; return k != null && int.TryParse(k.GetValue("CurrentBuildNumber") as string, out b) && b >= 20348; }
            }
            catch { return false; }
        }

        static string ProtoName(SslProtocols p)
        {
            int v = (int)p;
            if (v == 12288) return "TLS 1.3"; if (v == 3072) return "TLS 1.2"; if (v == 768) return "TLS 1.1"; if (v == 192) return "TLS 1.0";
            return p.ToString();
        }
        static void Finish(DestResult r, int level, string verdict) { r.Verdict = verdict; r.Level = level; r.Running = -1; }   // Run() marks it done

        static void Who(DestResult r, Monitor m, List<IPAddress> addrs)
        {
            try
            {
                var set = new HashSet<string>(addrs.Select(a => a.ToString()));
                var who = m.Connections(false).Where(c => set.Contains(c.RemoteIp)).GroupBy(c => c.Proc).Select(g => new[] { g.Key, g.Count() + " connection" + (g.Count() > 1 ? "s" : "") }).ToList();
                if (who.Count == 0) who.Add(Nobody);
                r.AddCard("WHO'S TALKING TO IT", who);
            }
            catch { }
        }

        static int QueryAll(string host, short type, List<IPAddress> into)
        {
            IntPtr res; int rc = Native.DnsQuery(host, type, 0x8, IntPtr.Zero, out res, IntPtr.Zero);
            if (rc != 0 || res == IntPtr.Zero) { if (res != IntPtr.Zero) Native.DnsRecordListFree(res, 1); return rc; }
            try
            {
                int P = IntPtr.Size, typeOff = 2 * P, dataOff = 2 * P + 16;
                for (IntPtr r = res; r != IntPtr.Zero; r = Marshal.ReadIntPtr(r, 0))
                {
                    int t = Marshal.ReadInt16(r, typeOff) & 0xFFFF;
                    if (t == 1) into.Add(new IPAddress((uint)Marshal.ReadInt32(r, dataOff)));
                    else if (t == 28) { var b = new byte[16]; Marshal.Copy(r + dataOff, b, 0, 16); into.Add(new IPAddress(b)); }
                }
            }
            finally { Native.DnsRecordListFree(res, 1); }
            return 0;
        }

        static bool DfPing(IPAddress ip, int payload)
        {
            try { using (var p = new Ping()) { var r = p.Send(ip, 1000, new byte[payload], new PingOptions(64, true)); return r.Status == IPStatus.Success; } }
            catch { return false; }
        }

        // hop -> (address, avg ms, loss %)
        static List<Tuple<string, double, int>> Trace(IPAddress target, int maxHops) { return Trace(target, maxHops, null); }
        public static List<Tuple<string, double, int>> TraceHops(IPAddress target, int maxHops) { return Trace(target, maxHops, null); }
        // multi: hop numbers (1-based) that answered from more than one router across the rounds = load-balanced paths (ECMP)
        static List<Tuple<string, double, int>> Trace(IPAddress target, int maxHops, List<int> multi)
        {
            var tasks = new List<Task<Tuple<string, double, int>>>();
            for (int ttl = 1; ttl <= maxHops; ttl++)
            {
                int t = ttl;
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    string addr = null; double sum = 0; int got = 0; var seen = new HashSet<string>();
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            using (var p = new Ping())
                            {
                                var sw = Stopwatch.StartNew(); var rep = p.Send(target, 1000, new byte[32], new PingOptions(t, true)); double ms = sw.Elapsed.TotalMilliseconds;
                                if (rep.Status == IPStatus.TtlExpired || rep.Status == IPStatus.Success) { addr = rep.Address.ToString(); seen.Add(addr); sum += ms; got++; }
                            }
                        }
                        catch { }
                    }
                    if (seen.Count > 1 && multi != null) lock (multi) multi.Add(t);
                    return Tuple.Create(addr, got > 0 ? sum / got : 0, (3 - got) * 100 / 3);
                }, TaskCreationOptions.LongRunning));
            }
            Task.WaitAll(tasks.ToArray(), 6000);
            return tasks.Select(x => x.Status == TaskStatus.RanToCompletion ? x.Result : Tuple.Create((string)null, 0.0, 100)).ToList();
        }

        public static string ProxyInfo()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (k == null) return "none (direct)";
                    var pac = k.GetValue("AutoConfigURL") as string; var en = k.GetValue("ProxyEnable"); var srv = k.GetValue("ProxyServer") as string;
                    if (!string.IsNullOrEmpty(pac)) return "PAC " + Draw.Trunc(pac, 30);
                    if (en is int && (int)en == 1 && !string.IsNullOrEmpty(srv)) return Draw.Trunc(srv, 30);
                    return "none (direct)";
                }
            }
            catch { return "?"; }
        }
    }

    // ================================================================== Health check (runs once when asked)
    static class HealthCheck
    {
        public static List<string[]> Run(Monitor m)
        {
            var rows = new List<string[]>(); // level, name, value, advice
            Action<int, string, string, string> add = delegate(int l, string n, string v, string a) { rows.Add(new[] { l.ToString(CultureInfo.CurrentCulture), n, v, a }); };
            try
            {
                double days = Native.GetTickCount64() / 86400000.0;
                add(days > 7 ? 1 : 0, "Uptime", days < 1 ? (days * 24).ToString("0", CultureInfo.CurrentCulture) + " hours" : days.ToString("0", CultureInfo.CurrentCulture) + " days",
                    days > 7 ? Voice.P(days.ToString("0", CultureInfo.CurrentCulture) + " days without a nap. A restart clears leaked memory and finishes pending updates.", "Consider a restart: it clears leaked memory and finishes updates.") : Voice.P("Fresh enough.", "Recently restarted."));
            }
            catch { }
            bool pending = KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") || KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            add(pending ? 1 : 0, "Pending reboot", pending ? "yes" : "no", pending ? Voice.P("Windows is waiting for a restart. Some network fixes only land after it.", "A restart is pending; some fixes apply only after it.") : "Nothing waiting.");
            try
            {
                foreach (var d in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed && x.IsReady))
                {
                    double free = d.AvailableFreeSpace * 100.0 / Math.Max(1, d.TotalSize);
                    add(free < 10 ? 2 : free < 20 ? 1 : 0, "Disk " + d.Name.TrimEnd('\\'), (d.AvailableFreeSpace / 1073741824.0).ToString("0", CultureInfo.CurrentCulture) + " GB free of " + (d.TotalSize / 1073741824.0).ToString("0", CultureInfo.CurrentCulture) + " GB",
                        free < 10 ? Voice.P("Nearly full - Windows gets grumpy below 10%. Empty Downloads or old captures.", "Nearly full; free some space.")
                        : free < 20 ? Voice.P("Getting tight (" + free.ToString("0", CultureInfo.CurrentCulture) + "% free). Worth a clean-up before it bites.", "Below 20% free; plan a clean-up.") : "Plenty of room.");
                }
            }
            catch { }
            NetworkInterface ni; string kind, ch, dns, vpnName; bool vpn; int sig, vmtu, pmtu;
            lock (m.L) { ni = m.Primary; kind = m.AdapterKind; ch = m.WifiChannel; sig = m.WifiSignal; vpn = m.VpnUp; vmtu = m.VpnMtu; pmtu = m.PrimaryMtu; dns = m.DnsServer; vpnName = m.VpnName; }
            if (ni != null)
            {
                try
                {
                    var st = ni.GetIPStatistics();
                    long errs = st.IncomingPacketsWithErrors + st.OutgoingPacketsWithErrors, disc = st.IncomingPacketsDiscarded + st.OutgoingPacketsDiscarded;
                    add(errs > 100 ? 1 : 0, "Adapter errors", Draw.Trunc(ni.Name, 16) + ": " + errs + " errors · " + disc + " discards",
                        errs > 100 ? Voice.P("The adapter itself is coughing. Try another cable or port, or update the driver.", "Adapter errors: try another cable or port, or update the driver.") : "The adapter is clean.");
                }
                catch { }
            }
            if (kind == "Wi-Fi")
            {
                string outp = ""; try { outp = Job.Run(Job.Netsh, "wlan show networks mode=bssid", 5000); } catch { }
                if (outp.IndexOf("BSSID", StringComparison.OrdinalIgnoreCase) < 0)
                    add(3, "Wi-Fi neighbourhood", "not available", "Windows only shares the Wi-Fi scan with apps that have location permission. Skipped.");
                else
                {
                    int same = Math.Max(0, Regex.Matches(outp, @"^\s*Channel\s*:\s*(\d+)", RegexOptions.Multiline).Cast<Match>().Count(x => x.Groups[1].Value == ch) - 1);
                    int lv = (sig >= 0 && sig < 40) || same >= 5 ? 1 : 0;
                    add(lv, "Wi-Fi", "signal " + (sig >= 0 ? sig + "%" : "?") + " · ch " + ch + " · " + same + " other AP" + (same == 1 ? "" : "s") + " on it",
                        lv == 1 ? Voice.P("Crowded or weak. Move closer, or switch band (5 GHz near the router, 2.4 GHz far away).", "Crowded or weak channel; move closer or change band.") : Voice.P("Your channel is calm.", "Channel is clear."));
                }
            }
            string proxy = DestCheck.ProxyInfo();
            add(proxy.StartsWith("none", StringComparison.Ordinal) ? 0 : 3, "Proxy", proxy, proxy.StartsWith("none", StringComparison.Ordinal) ? "Browsers go direct." : "Browsers go through this. If a site passes AdeeboMeter's check but fails in the browser, the proxy is the suspect.");
            add(0, "VPN", vpn ? Draw.Trunc(vpnName, 30) + (vmtu > 0 ? " · MTU " + vmtu : "") : "not connected", vpn ? "Company traffic goes through the tunnel (MTU " + (vmtu > 0 ? vmtu.ToString(CultureInfo.CurrentCulture) : "?") + " vs " + pmtu + " on the adapter)." : "Direct to the internet.");
            add(0, "DNS server", dns == "" ? "none" : dns, "The server your PC asks for names" + (vpn ? " (via VPN)." : "."));
            var top = m.TopApps(false, 2, true);
            double com = m.Commit.Avg(5);
            add(com > 90 ? 2 : com > 80 ? 1 : 0, "Memory", Draw.Fmt(com, "0", "% committed") + (top.Count > 0 ? " · " + top[0].Name + " " + Draw.MemStr(top[0].Mem) : ""),
                com > 80 ? Voice.P((top.Count > 0 ? top[0].Name : "Something") + " is hoarding memory. Close what you forgot about.", "Memory is high; close unused apps.") : "Room to spare.");
            Native.SYSTEM_POWER_STATUS ps;
            if (Native.GetSystemPowerStatus(out ps))
            {
                bool batt = ps.ACLineStatus == 0, saver = ps.SystemStatusFlag == 1;
                add(saver ? 1 : 0, "Power", (batt ? "on battery " + ps.BatteryLifePercent + "%" : "plugged in") + (saver ? " · battery saver ON" : ""),
                    saver ? Voice.P("Battery saver is slowing things down on purpose. Plug in if it feels sluggish.", "Battery saver is on and throttles performance.") : "Not throttled.");
            }
            return rows;
        }
        static bool KeyExists(string path) { try { using (var k = Registry.LocalMachine.OpenSubKey(path)) return k != null; } catch { return false; } }
    }

    // ================================================================== connection list (owner-drawn, only visible rows painted)
    sealed class ConnList : Control
    {
        sealed class Row { public Conn C; public DateTime First, Gone = DateTime.MinValue; public bool Initial; }
        readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
        List<Row> view = new List<Row>();
        readonly VScrollBar sb = new VScrollBar();
        int hover = -1; string selKey;
        public string Filter = "All", Query = ""; public bool ShowIdle;
        public bool CanSniff;
        public event Action<Conn> SniffRequested;
        public event Action Changed;
        public event Action<string> HoverKey;   // "proc:<app>" or "state.<STATE>" under the mouse, null when none
        string lastKey;
        string KeyAt(Point p)
        {
            int i = RowAt(p.Y); var cols = Cols(Width); float x = 14 * S;
            if (i < 0) return null;
            float appX = x + cols[0] + 8 * S, stX = appX + cols[1] + 8 * S + cols[2] + 8 * S + cols[3] + 8 * S;
            if (p.X >= x && p.X < appX + cols[1]) return "proc:" + view[i].C.Proc;
            if (p.X >= stX && p.X < stX + cols[4]) return "state." + view[i].C.State;
            return null;
        }
        bool loaded;
        static readonly HashSet<string> named = new HashSet<string> { "Teams", "Chrome", "Edge", "Outlook", "OneDrive" };
        static float S { get { return Draw.S; } }
        static int RowH { get { return (int)(40 * S); } }
        static int HeadH { get { return (int)(28 * S); } }

        public ConnList()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Theme.Card; sb.Dock = DockStyle.Right; sb.Scroll += delegate { Invalidate(); }; Controls.Add(sb);
            TabStop = true;
        }

        public void Merge(List<Conn> fresh)
        {
            var now = DateTime.Now; var seen = new HashSet<string>();
            foreach (var c in fresh)
            {
                seen.Add(c.Key); Row r;
                if (rows.TryGetValue(c.Key, out r)) { r.C = c; r.Gone = DateTime.MinValue; }
                else rows[c.Key] = new Row { C = c, First = now, Initial = !loaded };
            }
            foreach (var r in rows.Values) if (!seen.Contains(r.C.Key) && r.Gone == DateTime.MinValue) r.Gone = now;
            foreach (var k in rows.Where(kv => kv.Value.Gone != DateTime.MinValue && (now - kv.Value.Gone).TotalSeconds > 3).Select(kv => kv.Key).ToList()) rows.Remove(k);
            loaded = true; Rebuild();
        }
        public void Rebuild()
        {
            string q = (Query ?? "").Trim().ToLowerInvariant();
            view = rows.Values.Where(r =>
            {
                var c = r.C;
                if (!ShowIdle && (c.State == "LISTEN" || c.State == "BOUND")) return false;
                if (Filter != "All" && (Filter == "Other" ? named.Contains(c.Proc) : c.Proc != Filter)) return false;
                if (q != "" && (c.Proc + " " + c.Host + " " + c.Remote + " " + c.Local + " " + c.Pid).ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0) return false;
                return true;
            }).OrderBy(r => r.C.Proc, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.C.State == "ESTABLISHED" ? 0 : 1).ThenBy(r => r.C.Host).ToList();
            int content = view.Count * RowH, visible = Math.Max(1, Height - HeadH);
            sb.Maximum = Math.Max(0, content); sb.LargeChange = visible; sb.SmallChange = RowH; sb.Enabled = content > visible;
            int maxV = Math.Max(0, content - visible); if (sb.Value > maxV) sb.Value = maxV;
            Invalidate();
            if (Changed != null) Changed();
        }
        public int Shown { get { return view.Count; } }
        public int Established { get { return view.Count(r => r.C.State == "ESTABLISHED"); } }
        public int SynSent { get { return view.Count(r => r.C.State == "SYN_SENT"); } }
        public void ReleaseAll() { rows.Clear(); view = new List<Row>(); }
        public Dictionary<int, string> PortToApp()
        {
            var m = new Dictionary<int, string>();
            foreach (var r in rows.Values) if (r.C.LocalPort > 0 && !m.ContainsKey(r.C.LocalPort)) m[r.C.LocalPort] = r.C.Proc;
            return m;
        }

        static readonly Color[] palette = { Theme.C("#0B7A75"), Theme.C("#C2410C"), Theme.C("#4F46E5"), Theme.C("#DC2626"), Theme.C("#2563EB"), Theme.C("#B91C1C"), Theme.C("#0369A1"), Theme.C("#15803D"), Theme.C("#A16207"), Theme.C("#7C3AED") };
        static Color AppColor(string n) { int h = 0; foreach (char ch in n) h = unchecked(h * 31 + ch); return palette[(h & 0x7fffffff) % palette.Length]; }
        static Color[] StateColors(string st)
        {
            switch (st)
            {
                case "ESTABLISHED": return new[] { Theme.LvSoft(0), Theme.LvText(0) };
                case "SYN_SENT": return new[] { Theme.LvSoft(2), Theme.LvText(2) };
                case "CLOSE_WAIT": case "FIN_WAIT1": case "FIN_WAIT2": case "LAST_ACK": return new[] { Theme.LvSoft(1), Theme.LvText(1) };
                case "LISTEN": return new[] { Theme.BlueSoft, Theme.BlueDark };
                default: return new[] { Theme.Line2, Theme.Muted };
            }
        }
        int[] Cols(int w)
        {
            // badge, app, proto, remote, state, seen, sniff
            int fixedW = (int)((34 + 62 + 116 + 64 + 70 + 6 * 8) * S), flex = Math.Max((int)(240 * S), w - fixedW - sb.Width - (int)(28 * S));
            return new[] { (int)(34 * S), (int)(flex * 0.3), (int)(62 * S), (int)(flex * 0.7), (int)(116 * S), (int)(64 * S), (int)(70 * S) };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            int w = Width - sb.Width; var cols = Cols(Width); float x0 = 14 * S;
            using (var muted = new SolidBrush(Theme.Muted)) using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub))
            using (var line = new Pen(Theme.Line2))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                string[] heads = { "", "APP", "PROTO", "REMOTE", "STATE", "SEEN", "" };
                float x = x0; for (int i = 0; i < heads.Length; i++) { g.DrawString(heads[i], Draw.fTiny, muted, x, 8 * S); x += cols[i] + 8 * S; }
                g.DrawLine(line, 0, HeadH - 1, w, HeadH - 1);
                g.SetClip(new Rectangle(0, HeadH, w, Height - HeadH));
                int first = sb.Value / RowH, top = HeadH - sb.Value % RowH;
                var now = DateTime.Now;
                for (int i = first, y = top; i < view.Count && y < Height; i++, y += RowH)
                {
                    var r = view[i]; var c = r.C;
                    Color bg = Theme.Card;
                    if (r.Gone != DateTime.MinValue) bg = Theme.RowGone;
                    else if (!r.Initial && (now - r.First).TotalSeconds < 3) bg = Theme.RowNew;
                    else if (c.Key == selKey) bg = Theme.RowSel;
                    else if (i == hover) bg = Theme.RowHot;
                    using (var b = new SolidBrush(bg)) g.FillRectangle(b, 0, y, w, RowH);
                    g.DrawLine(line, 0, y + RowH - 1, w, y + RowH - 1);
                    x = x0; float cy = y + RowH / 2f;
                    var badge = new RectangleF(x, cy - 13 * S, 26 * S, 26 * S);
                    Theme.Fill(g, badge, 7 * S, AppColor(c.Proc));
                    using (var wb = new SolidBrush(Color.White)) g.DrawString(c.Proc.Length > 0 ? c.Proc.Substring(0, 1).ToUpperInvariant() : "?", Draw.fPill, wb, badge, center);
                    x += cols[0] + 8 * S;
                    Text2(g, c.Proc, c.Pid == 0 ? (c.State == "TIME_WAIT" ? "connection being closed" : "owner not reported") : "PID " + c.Pid, x, y, cols[1], ink, muted, Draw.fBodyB, Draw.fSmall);
                    x += cols[1] + 8 * S;
                    var ps = g.MeasureString(c.Proto, Draw.fMonoS); Theme.Fill(g, new RectangleF(x, cy - ps.Height / 2 - 2 * S, ps.Width + 6 * S, ps.Height + 4 * S), 5 * S, Theme.Line2);
                    g.DrawString(c.Proto, Draw.fMonoS, sub, x + 3 * S, cy - ps.Height / 2);
                    x += cols[2] + 8 * S;
                    string addr = c.Remote == "*" ? c.Local : c.Remote; if (c.HostFrom != "") addr += "  ·  name from " + c.HostFrom;
                    Text2(g, c.Host, addr, x, y, cols[3], ink, muted, Draw.fBodyB, Draw.fMonoS);
                    x += cols[3] + 8 * S;
                    var sc = StateColors(c.State); var ss = g.MeasureString(c.State, Draw.fMonoS);
                    Theme.Fill(g, new RectangleF(x, cy - ss.Height / 2 - 3 * S, ss.Width + 8 * S, ss.Height + 6 * S), 5 * S, sc[0]);
                    using (var b = new SolidBrush(sc[1])) g.DrawString(c.State, Draw.fMonoS, b, x + 4 * S, cy - ss.Height / 2);
                    x += cols[4] + 8 * S;
                    g.DrawString(Age(r, now), Draw.fMonoS, sub, x, cy - 7 * S);
                    x += cols[5] + 8 * S;
                    bool can = CanSniff && c.State != "BOUND";
                    var br = new RectangleF(x, cy - 13 * S, 58 * S, 26 * S);
                    Theme.Fill(g, br, 6 * S, can ? Theme.Blue : Theme.Line2);
                    using (var b = new SolidBrush(can ? Color.White : Theme.Muted)) g.DrawString("Sniff", Draw.fSmall, b, br, center);
                }
                g.ResetClip();
                if (view.Count == 0) g.DrawString(Voice.P("Nothing matches. Either the filter's too picky or it's really quiet out there.", "No connections match."), Draw.fBody, muted, x0, HeadH + 14 * S);
            }
        }
        static void Text2(Graphics g, string a, string b, float x, float y, int w, Brush ink, Brush muted, Font fa, Font fb)
        {
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(a ?? "", fa, ink, new RectangleF(x, y + 3 * S, w, 18 * S), sf);
                g.DrawString(b ?? "", fb, muted, new RectangleF(x, y + 21 * S, w, 16 * S), sf);
            }
        }
        static string Age(Row r, DateTime now)
        {
            var t = now - r.First;
            string s = t.TotalMinutes < 1 ? ((int)t.TotalSeconds) + "s" : t.TotalHours < 1 ? ((int)t.TotalMinutes) + "m" : ((int)t.TotalHours) + "h " + t.Minutes + "m";
            return r.Initial ? s + "+" : s;
        }
        int RowAt(int y) { if (y < HeadH) return -1; int i = (y - HeadH + sb.Value) / RowH; return i >= 0 && i < view.Count ? i : -1; }
        bool OnSniffButton(int x) { var cols = Cols(Width); float sx = 14 * S; for (int i = 0; i < 6; i++) sx += cols[i] + 8 * S; return x >= sx && x <= sx + 58 * S; }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = RowAt(e.Y); if (h != hover) { hover = h; Invalidate(); }
            string k = KeyAt(e.Location);
            Cursor = h >= 0 && OnSniffButton(e.X) ? Cursors.Hand : k != null ? Cursors.Help : Cursors.Default;
            if (k != lastKey) { lastKey = k; if (HoverKey != null) HoverKey(k); }
        }
        protected override void OnMouseLeave(EventArgs e) { if (hover != -1) { hover = -1; Invalidate(); } lastKey = null; if (HoverKey != null) HoverKey(null); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus(); int i = RowAt(e.Y); if (i < 0) return;
            selKey = view[i].C.Key; Invalidate();
            if (OnSniffButton(e.X) && CanSniff && view[i].C.State != "BOUND" && SniffRequested != null) SniffRequested(view[i].C);
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e) { int i = RowAt(e.Y); if (i >= 0 && CanSniff && view[i].C.State != "BOUND" && SniffRequested != null) SniffRequested(view[i].C); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int max = Math.Max(0, sb.Maximum - sb.LargeChange + 1);
            sb.Value = Math.Max(0, Math.Min(max, sb.Value - e.Delta / 120 * RowH * 3)); Invalidate();
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if (IsHandleCreated) Rebuild(); }
    }


    // ================================================================== Ctrl+Alt+A from anywhere (RegisterHotKey: no admin, no keyboard hook)
    sealed class HotKey : NativeWindow, IDisposable
    {
        readonly Action press; public readonly bool Ok;
        public HotKey(Action onPress)
        {
            press = onPress; CreateHandle(new CreateParams());
            Ok = Native.RegisterHotKey(Handle, 1, 0x0001 /*ALT*/ | 0x0002 /*CTRL*/ | 0x4000 /*NOREPEAT*/, (uint)Keys.A);
        }
        protected override void WndProc(ref Message m) { if (m.Msg == 0x0312) { try { press(); } catch { } } base.WndProc(ref m); }
        public void Dispose() { if (Handle != IntPtr.Zero) { Native.UnregisterHotKey(Handle, 1); DestroyHandle(); } }
    }

    // ================================================================== Live capture: Wireshark's tshark, only while you press Start
    // Runs only on the Live capture tab, stops when you press Stop, leave the tab, close the view or after 15 minutes.
    // tshark decodes (-n: no name lookups, -p: only this PC's own traffic, -M: session reset to keep memory flat);
    // dumpcap keeps a small ring buffer (5 x 20 MB) so Sniff can include what happened before you clicked.
    sealed class LiveCapture
    {
        static readonly char[] Comma = { ',' };
        static readonly string[] TestLocal = { "10.0.0.82", "172.17.0.2", "192.0.2.2" };
        static int Num(string f) { int v; return int.TryParse(First(f), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; }
        public sealed class Conv { public string Key, Local = "", Remote = "", Proto = "", Host = "", L4 = ""; public int LocalPort, RemotePort; public long Bytes, Pkts, Prev; public int Retx; public DateTime Last; public double Rate; }
        public sealed class View
        {
            public List<string[]> Phs = new List<string[]>();     // depth, name, pct, bytes
            public List<Conv> Top = new List<Conv>();
            public List<string[]> Expert = new List<string[]>();  // level, text, count
            public long Pkts, Bytes; public double Pps; public string State = ""; public long TsharkMB;
            public List<string[]> ByApp = new List<string[]>();   // app, bytes, rate Mb/s, share %
            public List<string[]> Calls = new List<string[]>();   // direction + far end, kind, packets, loss, jitter, MOS, level
            public List<string> DhcpServers = new List<string>(); public bool Extras;
        }
        sealed class Rtp { public string Dir = "", Remote = ""; public long Count, BaseExt = -1, MaxExt, Cycles, LastSeq = -1; public double FirstT, FirstTs, PrevT, PrevTs, Rate, Jitter; }
        readonly Dictionary<string, Rtp> rtp = new Dictionary<string, Rtp>(); readonly HashSet<string> dhcp = new HashSet<string>();
        static bool? extrasOk; bool usingExtras; string startGuid = "";
        static readonly string[] extraFields = { "frame.time_relative", "rtp.ssrc", "rtp.seq", "rtp.timestamp", "dhcp.option.dhcp_server_id" };
        static readonly double[] Clocks = { 8000, 16000, 24000, 32000, 48000, 90000 };
        public static readonly string WsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Wireshark");
        public static string Tshark { get { return Path.Combine(WsDir, "tshark.exe"); } }
        public static string Dumpcap { get { return Path.Combine(WsDir, "dumpcap.exe"); } }
        public static string Mergecap { get { return Path.Combine(WsDir, "mergecap.exe"); } }
        public static bool Npcap { get { return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Npcap", "wpcap.dll")); } }
        public static bool Available { get { return Npcap && File.Exists(Tshark) && File.Exists(Dumpcap); } }
        public static string RingDir { get { return Path.Combine(Program.CaptureDir, "ring"); } }

        readonly object k = new object();
        Process ts, ring; volatile bool running; DateTime started, lastView = DateTime.UtcNow; string iface = "";
        Dictionary<string, long[]> phs = new Dictionary<string, long[]>();
        Dictionary<string, Conv> convs = new Dictionary<string, Conv>();
        Dictionary<string, string> names = new Dictionary<string, string>();
        Dictionary<string, int> expert = new Dictionary<string, int>();
        HashSet<string> local = new HashSet<string>();
        long pkts, bytes, prevPkts; public string Error = "";
        public bool Running { get { return running; } }
        public bool RingActive { get { return running && ring != null && !ring.HasExited; } }
        public TimeSpan Elapsed { get { return running ? DateTime.UtcNow - started : TimeSpan.Zero; } }
        const int MaxConvs = 3000, MaxNames = 5000; static int MaxMinutes { get { return Cfg.LiveMin; } }

        static readonly string[] fields = { "frame.len", "frame.protocols", "ip.src", "ip.dst", "ipv6.src", "ipv6.dst", "tcp.srcport", "tcp.dstport", "udp.srcport", "udp.dstport",
            "tls.handshake.extensions_server_name", "dns.qry.name", "dns.a", "dns.aaaa", "_ws.expert.message", "_ws.col.Protocol" };

        public bool Start(string ifaceGuid)
        {
            Stop();
            Guid g; if (!Guid.TryParse((ifaceGuid ?? "").Trim('{', '}'), out g)) { Error = "No active adapter to capture on."; return false; }
            if (!Available) { Error = Voice.P("Live capture needs Wireshark (tshark) and Npcap - not on this PC.", "Live capture needs Wireshark and Npcap."); return false; }
            iface = "\\Device\\NPF_{" + g.ToString().ToUpperInvariant() + "}"; startGuid = g.ToString();
            lock (k) { phs.Clear(); convs.Clear(); expert.Clear(); rtp.Clear(); dhcp.Clear(); pkts = bytes = prevPkts = 0; }
            local = new HashSet<string>();
            try { foreach (var n in NetworkInterface.GetAllNetworkInterfaces()) foreach (var ua in n.GetIPProperties().UnicastAddresses) local.Add(ua.Address.ToString().Split('%')[0]); } catch { }
            Error = ""; started = DateTime.UtcNow; running = true;
            try
            {
                var sb = new StringBuilder("-i \"" + iface + "\" -p -n -l -M 100000 -T fields -E separator=/t -E occurrence=a -E aggregator=, -E quote=n");
                foreach (var f in fields) sb.Append(" -e ").Append(f);
                // Extra fields for call quality and DHCP. Older Wireshark versions may not know them - then tshark refuses
                // to start, and we quietly start again without them.
                usingExtras = extrasOk != false;
                if (usingExtras) { foreach (var f in extraFields) sb.Append(" -e ").Append(f); sb.Append(" --enable-heuristic rtp_udp"); }
                var psi = new ProcessStartInfo(Tshark, sb.ToString()) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                ts = Process.Start(psi); Job.Add(ts);
                try { ts.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                ts.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null || e.Data.IndexOf("Capturing on", StringComparison.Ordinal) >= 0 || e.Data.Trim() == "") return;
                    string d = e.Data;
                    if (usingExtras && (d.IndexOf("valid", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("unrecognized", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("heuristic", StringComparison.OrdinalIgnoreCase) >= 0))
                    { extrasOk = false; usingExtras = false; string gid = startGuid; Program.Log("tshark: extra fields not supported, restarting without them"); ThreadPool.QueueUserWorkItem(delegate { Thread.Sleep(300); Start(gid); }); return; }
                    Error = Draw.Trunc(d.Trim(), 140);
                };
                ts.BeginErrorReadLine();
                var t = new Thread(Reader) { IsBackground = true, Name = "tshark", Priority = ThreadPriority.BelowNormal }; t.Start();
                // ring buffer for Sniff
                try { Directory.CreateDirectory(RingDir); foreach (var f in Directory.GetFiles(RingDir)) try { File.Delete(f); } catch { } } catch { }
                var rp = new ProcessStartInfo(Dumpcap, "-i \"" + iface + "\" -p -q -b filesize:20000 -b files:5 -w \"" + Path.Combine(RingDir, "ring.pcapng") + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                ring = Process.Start(rp); Job.Add(ring); ring.BeginErrorReadLine();
                try { ring.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                Program.Log("Live capture started");
                return true;
            }
            catch (Exception e) { Error = "Could not start tshark: " + e.Message; Stop(); return false; }
        }

        public void Stop()
        {
            bool was = running; running = false;
            foreach (var p in new[] { ts, ring })
                if (p != null) { try { if (!p.HasExited) { p.Kill(); p.WaitForExit(2000); } } catch { } try { p.Dispose(); } catch { } }
            ts = null; ring = null;
            try { if (Directory.Exists(RingDir)) foreach (var f in Directory.GetFiles(RingDir)) try { File.Delete(f); } catch { } } catch { }
            lock (k) { convs.Clear(); names.Clear(); phs.Clear(); expert.Clear(); rtp.Clear(); dhcp.Clear(); }
            if (was) Program.Log("Live capture stopped");
        }

        void Reader()
        {
            var p = ts; if (p == null) return;
            try
            {
                string line;
                while (running && (line = p.StandardOutput.ReadLine()) != null)
                {
                    try { Parse(line); } catch { }
                    if ((DateTime.UtcNow - started).TotalMinutes > MaxMinutes) { Error = Voice.P("Stopped after " + MaxMinutes + " minutes so it doesn't nibble your CPU forever. Press Start for more.", "Stopped automatically after " + MaxMinutes + " minutes."); running = false; break; }
                }
            }
            catch { }
            if (!running) ThreadPool.QueueUserWorkItem(delegate { Stop(); });
        }

        public void FeedForTest(string line) { if (local.Count == 0) local = new HashSet<string>(TestLocal); Parse(line); }
        public void SetLocalForTest(IEnumerable<string> ips) { local = new HashSet<string>(ips); }

        static string First(string v) { if (string.IsNullOrEmpty(v)) return ""; int i = v.IndexOf(','); return i < 0 ? v : v.Substring(0, i); }

        void Parse(string line)
        {
            var f = line.Split('\t'); if (f.Length < 16) return;
            long len; if (!long.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out len)) len = 0;
            string src = First(f[2]), dst = First(f[3]); if (src == "") { src = First(f[4]); dst = First(f[5]); }
            int et = f[1].IndexOf("ethertype:", StringComparison.Ordinal);
            string proto = et >= 0 ? f[1].Substring(et + 10) : f[1];
            int sp = 0, dp = 0; string l4 = "";
            if (f[6] != "") { sp = Num(f[6]); dp = Num(f[7]); l4 = "TCP"; }
            else if (f[8] != "") { sp = Num(f[8]); dp = Num(f[9]); l4 = "UDP"; }
            lock (k)
            {
                pkts++; bytes += len;
                // protocol hierarchy (same idea as tshark -z io,phs)
                var segs = proto.Split(':'); string path = "";
                for (int i = 0; i < segs.Length && i < 6; i++)
                {
                    if (segs[i] == "data" && i > 0) break;
                    path = path == "" ? segs[i] : path + ":" + segs[i];
                    long[] c; if (!phs.TryGetValue(path, out c)) { if (phs.Count > 400) break; c = new long[2]; phs[path] = c; }
                    c[0]++; c[1] += len;
                }
                // names: TLS server name and DNS answers - learned from the traffic itself, nothing looked up
                if (f[11] != "" && names.Count < MaxNames)
                    foreach (var ip in (f[12] + "," + f[13]).Split(Comma, StringSplitOptions.RemoveEmptyEntries)) names[ip] = First(f[11]);
                // expert info, Wireshark's own diagnosis
                bool retx = false;
                if (f[14] != "")
                {
                    string m = f[14], cat = null;
                    if (m.IndexOf("retransmission", StringComparison.OrdinalIgnoreCase) >= 0) { cat = "TCP retransmissions"; retx = true; }
                    else if (m.IndexOf("Duplicate ACK", StringComparison.OrdinalIgnoreCase) >= 0) cat = "Duplicate ACKs";
                    else if (m.IndexOf("zero window", StringComparison.OrdinalIgnoreCase) >= 0) cat = "Zero window";
                    else if (m.IndexOf("Previous segment not captured", StringComparison.OrdinalIgnoreCase) >= 0) cat = "Segments lost before capture";
                    else if (m.IndexOf("out-of-order", StringComparison.OrdinalIgnoreCase) >= 0) cat = "Out of order";
                    else if (m.IndexOf("Connection reset", StringComparison.OrdinalIgnoreCase) >= 0) cat = "Connection resets (RST)";
                    else if (m.IndexOf("Alert", StringComparison.OrdinalIgnoreCase) >= 0 && proto.Contains("tls")) cat = "TLS alerts";
                    if (cat != null) { int n; expert.TryGetValue(cat, out n); expert[cat] = n + 1; }
                }
                if (f.Length >= 21)
                {
                    if (extrasOk == null && pkts > 20) extrasOk = true;
                    string ds = First(f[20]); if (ds != "" && dhcp.Count < 16) dhcp.Add(ds);
                    if (f[17] != "" && f[18] != "" && src != "") AddRtp(First(f[17]), local.Contains(dst) ? "in" : "out", local.Contains(dst) ? src : dst, f[16], First(f[18]), First(f[19]));
                }
                if (src == "" || l4 == "") return;
                if (src.StartsWith("127.", StringComparison.Ordinal) || dst.StartsWith("127.", StringComparison.Ordinal) || src == "::1" || dst == "::1") return;
                bool srcLocal = local.Contains(src);
                string lo = srcLocal ? src : dst, re = srcLocal ? dst : src; int lp = srcLocal ? sp : dp, rp = srcLocal ? dp : sp;
                string key = l4 + "|" + lo + ":" + lp + "|" + re + ":" + rp;
                Conv cv;
                if (!convs.TryGetValue(key, out cv))
                {
                    if (convs.Count >= MaxConvs) { var oldest = convs.Values.OrderBy(x => x.Last).Take(300).Select(x => x.Key).ToList(); foreach (var o in oldest) convs.Remove(o); }
                    cv = new Conv { Key = key, Local = lo, Remote = re, LocalPort = lp, RemotePort = rp, L4 = l4 }; convs[key] = cv;
                }
                cv.Bytes += len; cv.Pkts++; cv.Last = DateTime.UtcNow; if (retx) cv.Retx++;
                // keep the most telling protocol name: a bare ACK ("TCP") must not overwrite "TLSv1.3"
                if (f[15] != "") { string np = First(f[15]); if (cv.Proto == "" || (np != "TCP" && np != "UDP")) cv.Proto = np; }
                string sni = First(f[10]); if (sni != "") { cv.Host = sni; if (names.Count < MaxNames) names[re] = sni; }
            }
        }

        // RTP: sequence numbers give loss; arrival time vs. RTP timestamp gives jitter (RFC 3550), with the media clock
        // worked out from the stream itself and snapped to the usual rates.
        void AddRtp(string ssrc, string dir, string remote, string tRel, string seqS, string tsS)
        {
            double t; long seq, ts;
            if (!double.TryParse(tRel, NumberStyles.Float, CultureInfo.InvariantCulture, out t) || !long.TryParse(seqS, NumberStyles.Integer, CultureInfo.InvariantCulture, out seq) || !long.TryParse(tsS, NumberStyles.Integer, CultureInfo.InvariantCulture, out ts)) return;
            string key = dir + "|" + ssrc; Rtp r;
            if (!rtp.TryGetValue(key, out r)) { if (rtp.Count >= 60) return; r = new Rtp { Dir = dir, Remote = remote, FirstT = t, FirstTs = ts }; rtp[key] = r; }
            if (r.LastSeq >= 0 && seq < r.LastSeq && r.LastSeq - seq > 30000) r.Cycles++;
            long ext = r.Cycles * 65536 + seq;
            if (r.BaseExt < 0) r.BaseExt = ext;
            if (ext > r.MaxExt) r.MaxExt = ext;
            if (r.Count == 60 && t > r.FirstT + 0.5)
            {
                double est = (ts - r.FirstTs) / (t - r.FirstT); double best = 0, bd = double.MaxValue;
                foreach (var c in Clocks) { double dd = Math.Abs(c - est); if (dd < bd) { bd = dd; best = c; } }
                r.Rate = best;
            }
            if (r.Rate > 0 && r.Count > 0)
            {
                double d = (t - r.PrevT) - (ts - r.PrevTs) / r.Rate;
                if (Math.Abs(d) < 1) r.Jitter += (Math.Abs(d) - r.Jitter) / 16;
            }
            r.Count++; r.LastSeq = seq; r.PrevT = t; r.PrevTs = ts;
        }
        // ITU-T G.107 E-model, simplified: one-way delay plus jitter buffer and packet loss -> R factor -> MOS (1-4.5).
        public static double Mos(double delayMs, double jitterMs, double lossPct)
        {
            double d = delayMs + 2 * jitterMs + 10;
            double id = 0.024 * d + (d > 177.3 ? 0.11 * (d - 177.3) : 0);
            double ie = 95 * lossPct / (lossPct + 10);
            double r = 93.2 - id - ie;
            return r <= 0 ? 1 : r >= 100 ? 4.5 : Math.Min(4.5, 1 + 0.035 * r + 7e-6 * r * (r - 60) * (100 - r));
        }

        // Called once a second by the view; computes rates and copies what the screen needs.
        public View Snapshot(Func<int, string> appByLocalPort) { return Snapshot(appByLocalPort, 30); }
        public View Snapshot(Func<int, string> appByLocalPort, double oneWayMs)
        {
            var v = new View();
            var now = DateTime.UtcNow; double dt = Math.Max(0.2, (now - lastView).TotalSeconds); lastView = now;
            lock (k)
            {
                v.Pkts = pkts; v.Bytes = bytes; v.Pps = (pkts - prevPkts) / dt; prevPkts = pkts;
                foreach (var c in convs.Values) { c.Rate = (c.Bytes - c.Prev) * 8 / dt / 1e6 * 0.6 + c.Rate * 0.4; c.Prev = c.Bytes; string h; if (c.Host == "" && names.TryGetValue(c.Remote, out h)) c.Host = h; }
                v.Top = convs.Values.Where(c => (now - c.Last).TotalSeconds < 30).OrderByDescending(c => c.Rate).ThenByDescending(c => c.Bytes).Take(12)
                    .Select(c => new Conv { Key = c.Key, Local = c.Local, Remote = c.Remote, LocalPort = c.LocalPort, RemotePort = c.RemotePort, Proto = c.Proto, L4 = c.L4, Host = c.Host != "" ? c.Host : c.Remote, Bytes = c.Bytes, Pkts = c.Pkts, Retx = c.Retx, Rate = c.Rate }).ToList();
                double total = Math.Max(1, pkts);
                foreach (var kv in phs.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    double pct = kv.Value[0] * 100.0 / total; if (pct < 0.5) continue;
                    int depth = kv.Key.Count(ch => ch == ':'); string name = kv.Key.Substring(kv.Key.LastIndexOf(':') + 1);
                    v.Phs.Add(new[] { depth.ToString(CultureInfo.CurrentCulture), name, pct.ToString("0.0", CultureInfo.CurrentCulture) + "%", Draw.MemStr(kv.Value[1]) });
                    if (v.Phs.Count >= 18) break;
                }
                // bytes per app: every conversation, owned by the app holding its local port
                if (appByLocalPort != null)
                {
                    var agg = new Dictionary<string, double[]>();
                    foreach (var c in convs.Values)
                    {
                        string a = appByLocalPort(c.LocalPort) ?? "other / closed";
                        double[] x; if (!agg.TryGetValue(a, out x)) { x = new double[2]; agg[a] = x; }
                        x[0] += c.Bytes; if ((now - c.Last).TotalSeconds < 5) x[1] += c.Rate;
                    }
                    double all = Math.Max(1, agg.Values.Sum(x => x[0]));
                    v.ByApp = agg.OrderByDescending(kv => kv.Value[0]).Take(8).Select(kv => new[] { kv.Key, Draw.MemStr((long)kv.Value[0]), kv.Value[1].ToString("0.00", CultureInfo.CurrentCulture), (kv.Value[0] * 100 / all).ToString("0", CultureInfo.CurrentCulture) }).ToList();
                }
                foreach (var r in rtp.Values.Where(x => x.Count >= 100).OrderByDescending(x => x.Count).Take(6))
                {
                    long expected = r.MaxExt - r.BaseExt + 1; double loss = expected > 0 ? Math.Max(0, (expected - r.Count) * 100.0 / expected) : 0;
                    double jit = r.Jitter * 1000, mos = Mos(oneWayMs, jit, loss);
                    int lv = mos >= 4.0 ? 0 : mos >= 3.6 ? 1 : 2;
                    v.Calls.Add(new[] { (r.Dir == "in" ? "from " : "to ") + r.Remote, r.Rate >= 90000 ? "video" : r.Rate > 0 ? "audio" : "media", r.Count.ToString("N0", CultureInfo.CurrentCulture),
                        loss.ToString("0.0", CultureInfo.CurrentCulture) + "%", r.Rate > 0 ? jit.ToString("0.0", CultureInfo.CurrentCulture) + " ms" : "-", mos.ToString("0.0", CultureInfo.CurrentCulture), lv.ToString(CultureInfo.InvariantCulture) });
                }
                v.DhcpServers = dhcp.ToList(); v.Extras = usingExtras;
                foreach (var cat in new[] { "TCP retransmissions", "Duplicate ACKs", "Segments lost before capture", "Out of order", "Zero window", "Connection resets (RST)", "TLS alerts" })
                {
                    int n; expert.TryGetValue(cat, out n);
                    int lv = n == 0 ? 3 : cat == "TCP retransmissions" || cat == "Zero window" ? (n > 50 ? 2 : 1) : 1;
                    v.Expert.Add(new[] { lv.ToString(CultureInfo.CurrentCulture), cat, n.ToString(CultureInfo.CurrentCulture) });
                }
            }
            foreach (var c in v.Top) { string a = appByLocalPort != null ? appByLocalPort(c.LocalPort) : null; c.Proto = (a ?? "?") + " · " + (c.Proto != "" ? c.Proto : c.L4); }
            try { if (ts != null && !ts.HasExited) { ts.Refresh(); v.TsharkMB = ts.PrivateMemorySize64 / 1048576; if (v.TsharkMB > 300) { Error = "tshark grew past 300 MB - restarted it."; string i = iface; ThreadPool.QueueUserWorkItem(delegate { Start(i.Substring(i.IndexOf('{'))); }); } } } catch { }
            v.State = running ? "capturing " + ((int)Elapsed.TotalMinutes) + ":" + Elapsed.Seconds.ToString("00", CultureInfo.CurrentCulture) + " of " + MaxMinutes + ":00" : "stopped";
            return v;
        }


        // Which switch port am I plugged into? Switches announce themselves every 30-60 s (LLDP / CDP) - we just listen.
        public sealed class SwitchInfo { public string Name = "", Port = "", PortDesc = "", Vlan = "", Model = "", Mgmt = "", Proto = ""; }
        public static SwitchInfo FindSwitch(string ifaceGuid, int seconds, out string err)
        {
            err = ""; Guid g;
            if (!Guid.TryParse((ifaceGuid ?? "").Trim('{', '}'), out g)) { err = "No active adapter."; return null; }
            if (!Available) { err = "Needs Wireshark (tshark) and Npcap."; return null; }
            string ifc = "\\Device\\NPF_{" + g.ToString().ToUpperInvariant() + "}";
            // promiscuous on purpose: some network cards hide these link-local multicasts otherwise
            string args = "-i \"" + ifc + "\" -n -c 1 -a duration:" + seconds.ToString(CultureInfo.InvariantCulture) + " -f \"ether proto 0x88cc or ether dst 01:00:0c:cc:cc:cc\" -V";
            string text = "";
            try { text = Job.Run(Tshark, args, (seconds + 10) * 1000) ?? ""; }
            catch (Exception e) { err = Draw.Trunc(e.Message, 80); return null; }
            var si = ParseSwitch(text);
            if (si == null) err = "No switch announced itself in " + seconds + " s. The switch may have LLDP/CDP turned off, or you're on Wi-Fi / behind a dock that doesn't pass it.";
            return si;
        }
        public static SwitchInfo ParseSwitch(string text)
        {
            var si = new SwitchInfo(); bool any = false;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                string l = raw.Trim(); Match m;
                if ((m = Regex.Match(l, @"^System Name = (.+)$")).Success) { si.Name = m.Groups[1].Value; si.Proto = "LLDP"; any = true; }
                else if ((m = Regex.Match(l, @"^Port Subtype = .*?, Id: (.+)$")).Success) { si.Port = m.Groups[1].Value; si.Proto = "LLDP"; any = true; }
                else if ((m = Regex.Match(l, @"^Port Description = (.+)$")).Success) si.PortDesc = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^System Description = (.+)$")).Success && si.Model == "") si.Model = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^Port VLAN Identifier: (\d+)")).Success) si.Vlan = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^Management Address: ([0-9a-fA-F.:]+)$")).Success && si.Mgmt == "") si.Mgmt = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^Device ID: (.+)$")).Success) { si.Name = m.Groups[1].Value; si.Proto = "CDP"; any = true; }
                else if ((m = Regex.Match(l, @"^Port ID: (.+)$")).Success) { si.Port = m.Groups[1].Value; si.Proto = "CDP"; any = true; }
                else if ((m = Regex.Match(l, @"^Native VLAN: (\d+)")).Success) si.Vlan = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^Platform: (.+)$")).Success) si.Model = m.Groups[1].Value;
                else if ((m = Regex.Match(l, @"^IP [Aa]ddress: ([0-9.]+)$")).Success && si.Mgmt == "") si.Mgmt = m.Groups[1].Value;
            }
            if (si.Model.Length > 80) si.Model = si.Model.Substring(0, 79) + "…";
            return any ? si : null;
        }

        // Sniff with history: the ring buffer filtered to one flow, plus 15 s more, merged and opened in Wireshark.
        public void SniffWithHistory(Conn c, string filt, string dfilt, Action<string> status)
        {
            string dir = Program.SafeCapturePath("sniff_" + DateTime.Now.ToString("HHmmss", CultureInfo.CurrentCulture)); Directory.CreateDirectory(dir);
            var parts = new List<string>();
            try
            {
                status(Voice.P("Digging through the last few minutes and listening 15 s more...", "Collecting history + 15 s..."));
                var fwd = Path.Combine(dir, "z_after.pcapng");
                var fp = Process.Start(new ProcessStartInfo(Dumpcap, "-i \"" + iface + "\" -p -q -f \"" + filt + "\" -a duration:15 -w \"" + fwd + "\"") { UseShellExecute = false, CreateNoWindow = true }); Job.Add(fp);
                int i = 0;
                foreach (var rf in Directory.GetFiles(RingDir, "*.pcapng").OrderBy(x => File.GetLastWriteTimeUtc(x)))
                {
                    string copy = Path.Combine(dir, "ring" + i + ".pcapng"), outp = Path.Combine(dir, "a_part" + i + ".pcapng"); i++;
                    try { File.Copy(rf, copy, true); } catch { continue; }
                    Job.Run(Tshark, "-r \"" + copy + "\" -Y \"" + dfilt + "\" -w \"" + outp + "\"", 60000);
                    try { File.Delete(copy); } catch { }
                    if (File.Exists(outp) && new FileInfo(outp).Length > 0) parts.Add(outp);
                }
                fp.WaitForExit(20000); if (File.Exists(fwd)) parts.Add(fwd);
                string final = Program.SafeCapturePath("adebometer_" + new string(c.Proc.Where(char.IsLetterOrDigit).Take(20).ToArray()) + "_" + DateTime.Now.ToString("HHmmss", CultureInfo.CurrentCulture) + ".pcapng");
                if (parts.Count == 0) { status("Nothing captured for that flow."); return; }
                Job.Run(Mergecap, "-w \"" + final + "\" " + string.Join(" ", parts.Select(x => "\"" + x + "\"")), 60000);
                string ws = Path.Combine(WsDir, "Wireshark.exe");
                if (File.Exists(final)) { if (File.Exists(ws)) { var psi = new ProcessStartInfo(ws, "\"" + final + "\"") { UseShellExecute = false }; Process.Start(psi).Dispose(); } status(Voice.P("Got it, with history - opening in Wireshark. (Deleted when AdeeboMeter exits; Save As to keep it.)", "Saved with history; opening in Wireshark.")); }
                else status("Merging failed.");
            }
            catch (Exception e) { status("Sniff failed: " + e.Message); Program.Log("sniff history: " + e); }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }

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
        public static string Plain(string reason)
        {
            string r = reason.ToLowerInvariant();
            if (r.Contains("disconnected by the user")) return reason + " (you or an app chose to disconnect)";
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

        public sealed class Bss { public string Bssid = "", Ssid = ""; public int Rssi, FreqMHz, Channel, Width = 20; public bool K, V, R; public string Band = ""; }
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
            int sl = Math.Min(32, Marshal.ReadInt32(e, 0)); var sb = new byte[sl]; Marshal.Copy(e + 4, sb, 0, sl); b.Ssid = Encoding.UTF8.GetString(sb);
            var mac = new byte[6]; Marshal.Copy(e + 40, mac, 0, 6); b.Bssid = string.Join(":", mac.Select(x => x.ToString("x2", CultureInfo.InvariantCulture)));
            b.Rssi = Marshal.ReadInt32(e, 56);
            b.FreqMHz = Marshal.ReadInt32(e, 92) / 1000;
            int ieOff = Marshal.ReadInt32(e, 352), ieSize = Marshal.ReadInt32(e, 356);
            byte[] ies = new byte[Math.Max(0, Math.Min(ieSize, 4096))];
            if (ies.Length > 0) Marshal.Copy(e + ieOff, ies, 0, ies.Length);
            FromFreq(b); ReadIes(ies, b);
            return b;
        }
        public static void FromFreq(Bss b)
        {
            int f = b.FreqMHz;
            if (f >= 2400 && f < 2500) { b.Band = "2.4 GHz"; b.Channel = f == 2484 ? 14 : (f - 2407) / 5; }
            else if (f >= 5150 && f < 5925) { b.Band = "5 GHz"; b.Channel = (f - 5000) / 5; }
            else if (f >= 5925 && f < 7125) { b.Band = "6 GHz"; b.Channel = (f - 5950) / 5; }
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
        public static bool Randomised(string mac) { if (mac.Length < 2) return false; int b = Convert.ToInt32(mac.Substring(0, 2), 16); return (b & 0x02) != 0; }
        public static string MacOf(string ip) { var e = Table().FirstOrDefault(x => x.Ip == ip); return e != null ? e.Mac : ""; }
    }

    // Ask one specific DNS server (used only during a check you start, to spot filtered or tampered answers).
    static class DnsAsk
    {
        public static List<string> Ask(string name, string server, int timeoutMs, out string err)
        {
            err = ""; IPAddress srv;
            if (!IPAddress.TryParse(server, out srv) || srv.AddressFamily != AddressFamily.InterNetwork) { err = "bad server"; return new List<string>(); }
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
            DateTime d; return DateTime.TryParseExact(date, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? (DateTime.Now - d).TotalDays / 365.25 : -1;
        }
    }

    // Optional history (off unless you switch it on): one line every 10 s and every event, one file per day, 7 days kept.
    static class History
    {
        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdeeboMeter", "history"); } }
        static readonly object k = new object(); static DateTime lastPrune = DateTime.MinValue; static readonly char[] CommaSep = { ',' };
        static string FileFor(DateTime d, string kind) { return Path.Combine(Dir, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + (kind == "ev" ? "-events" : "") + ".csv"); }
        static string N(double v) { return double.IsNaN(v) ? "" : v.ToString("0.#", CultureInfo.InvariantCulture); }
        public static void Sample(int level, double cpu, double mem, double down, double gw, double net, int wifi)
        {
            if (!Cfg.History) return;
            Write(FileFor(DateTime.Now, "m"), string.Join(",", DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture), level.ToString(CultureInfo.InvariantCulture), N(cpu), N(mem), N(down), N(gw), N(net), wifi.ToString(CultureInfo.InvariantCulture)));
        }
        public static void Event(int lv, string kind, string text)
        {
            if (!Cfg.History) return;
            Write(FileFor(DateTime.Now, "ev"), DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "," + lv.ToString(CultureInfo.InvariantCulture) + "," + kind + "," + (text ?? "").Replace('\n', ' ').Replace("\r", ""));
        }
        static void Write(string file, string line)
        {
            lock (k)
            {
                try
                {
                    Directory.CreateDirectory(Dir); File.AppendAllText(file, line + "\r\n");
                    if ((DateTime.UtcNow - lastPrune).TotalHours > 6) { lastPrune = DateTime.UtcNow; Prune(7); }
                }
                catch (Exception e) { Program.Log("history: " + e.Message); }
            }
        }
        public static void Prune(int days)
        {
            try { foreach (var f in Directory.GetFiles(Dir, "*.csv")) if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-days)) File.Delete(f); } catch { }
        }
        public static void Clear() { lock (k) { try { if (Directory.Exists(Dir)) foreach (var f in Directory.GetFiles(Dir, "*.csv")) File.Delete(f); } catch (Exception e) { Program.Log("history clear: " + e.Message); } } }
        public static List<DateTime> Days()
        {
            var r = new List<DateTime>();
            try
            {
                if (!Directory.Exists(Dir)) return r;
                foreach (var f in Directory.GetFiles(Dir, "????-??-??.csv"))
                { DateTime d; if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) r.Add(d); }
            }
            catch { }
            return r.OrderByDescending(d => d).ToList();
        }
        public sealed class Day { public List<double[]> M = new List<double[]>(); public List<Timeline.Ev> E = new List<Timeline.Ev>(); }   // M: seconds-of-day, level, cpu, mem, down, gw, net, wifi
        public static Day Load(DateTime d)
        {
            var day = new Day();
            Func<string, double> P = s => { double v; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; };
            try
            {
                string mf = FileFor(d, "m"), ef = FileFor(d, "ev");
                if (File.Exists(mf))
                    foreach (var line in File.ReadLines(mf).Take(10000))
                    {
                        var f = line.Split(','); TimeSpan t; if (f.Length < 8 || !TimeSpan.TryParse(f[0], CultureInfo.InvariantCulture, out t)) continue;
                        day.M.Add(new[] { t.TotalSeconds, P(f[1]), P(f[2]), P(f[3]), P(f[4]), P(f[5]), P(f[6]), P(f[7]) });
                    }
                if (File.Exists(ef))
                    foreach (var line in File.ReadLines(ef).Take(2000))
                    {
                        var f = line.Split(CommaSep, 4); TimeSpan t; int lv; if (f.Length < 4 || !TimeSpan.TryParse(f[0], CultureInfo.InvariantCulture, out t) || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv)) continue;
                        day.E.Add(new Timeline.Ev { T = d.Date + t, Lv = lv, Kind = f[2], Text = f[3] });
                    }
            }
            catch (Exception e) { Program.Log("history load: " + e.Message); }
            day.E.Reverse();
            return day;
        }
    }

    // ================================================================== Your connection: Wi-Fi radio, neighbours, cable, addressing
    // Everything comes from Windows itself (netsh and the adapter statistics) - nothing is sent anywhere. Collected only while
    // the "Your connection" page is open, every 10 s.
    sealed class Ap { public string Ssid = "", Bssid = "", Band = "", Radio = ""; public int Signal = -1, Channel = -1, Util = -1, Stations = -1, Rssi, Width; }
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
            foreach (var line in (text ?? "").Split('\n')) { var m = kv.Match(line); if (m.Success && !d.ContainsKey(m.Groups[1].Value)) d[m.Groups[1].Value] = m.Groups[2].Value; }
            return d;
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
                string k = m.Groups[1].Value, v = m.Groups[2].Value; int x;
                if (Regex.IsMatch(k, @"^SSID \d+$")) { ssid = v; cur = null; }
                else if (Regex.IsMatch(k, @"^BSSID \d+$")) { cur = new Ap { Ssid = ssid == "" ? "(hidden network)" : ssid, Bssid = v }; list.Add(cur); }
                else if (cur == null) continue;
                else if (k == "Signal" && int.TryParse(v.TrimEnd('%', ' '), NumberStyles.Integer, CultureInfo.InvariantCulture, out x)) cur.Signal = x;
                else if (k == "Channel" && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out x)) cur.Channel = x;
                else if (k == "Band") cur.Band = v;
                else if (k == "Radio type") cur.Radio = v;
                else if (k == "Connected Stations" && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out x)) cur.Stations = x;
                else if (k == "Channel Utilization") { var um = Regex.Match(v, @"\((\d+)\s*%\)"); if (um.Success) cur.Util = int.Parse(um.Groups[1].Value, CultureInfo.InvariantCulture); }
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
            int lv = 0; var notes = new List<string>();

            // ---- radio (Wi-Fi) or cable
            if (s.Wifi)
            {
                string iface = "", nets = "";
                try { iface = Job.Run(Job.Netsh, "wlan show interfaces", 4000); } catch { }
                try { nets = Job.Run(Job.Netsh, "wlan show networks mode=bssid", 6000); } catch { }
                if (Blocked(iface) || Blocked(nets)) s.LocationBlocked = true;
                var f = Fields(iface); string v; int ch = -1, sig = -1;
                if (f.TryGetValue("Channel", out v)) int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out ch);
                if (f.TryGetValue("Signal", out v)) int.TryParse(v.TrimEnd('%', ' '), NumberStyles.Integer, CultureInfo.InvariantCulture, out sig);
                string band = BandOf(ch, f.TryGetValue("Band", out v) ? v : ""), radio = f.TryGetValue("Radio type", out v) ? v : "";
                string bssid = f.TryGetValue("AP BSSID", out v) ? v : f.TryGetValue("BSSID", out v) ? v : "";
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
                    foreach (var a in s.Aps) { var m2 = bss.FirstOrDefault(x => string.Equals(x.Bssid, a.Bssid, StringComparison.OrdinalIgnoreCase)); if (m2 != null) { a.Rssi = m2.Rssi; a.Width = m2.Width; } }
                    var me = bss.FirstOrDefault(x => string.Equals(x.Bssid, bssid, StringComparison.OrdinalIgnoreCase));
                    if (me != null)
                    {
                        s.Radio.Insert(4, new[] { "Channel width", me.Width + " MHz" + (me.Width >= 80 ? " - a wide, fast lane" : me.Width == 40 ? "" : " - narrow, so slower but sturdier"), "0" });
                        var help = new List<string>(); if (me.K) help.Add("11k"); if (me.V) help.Add("11v"); if (me.R) help.Add("11r fast roaming");
                        s.Radio.Add(new[] { "Roaming help", help.Count == 0 ? "none advertised - roaming is up to your laptop alone" : string.Join(" · ", help), help.Count == 0 ? "1" : "0" });
                        int idx = s.Radio.FindIndex(r => r[0] == "Signal");
                        if (idx >= 0 && me.Rssi < 0) s.Radio[idx] = new[] { "Signal", sig + "%  ·  " + me.Rssi + " dBm", me.Rssi < -75 ? "2" : me.Rssi < -67 ? "1" : "0" };
                    }
                    if (band.StartsWith("2.4", StringComparison.Ordinal) && s.Aps.Any(a => a.Ssid == F("SSID") && !a.Band.StartsWith("2.4", StringComparison.Ordinal)))
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
                s.Address.Add(new[] { "DNS servers", string.Join(", ", ip.DnsAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).Select(a => a.ToString()).Take(3)) });
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
            int realDrops = s.Drops.Count(d => d[1].IndexOf("by the user", StringComparison.OrdinalIgnoreCase) < 0);
            if (realDrops >= 3) { lv = Math.Max(lv, 1); notes.Add(Voice.P("Your Wi-Fi dropped " + realDrops + " times in the last day - the reasons are below.", realDrops + " Wi-Fi drops in 24 h.")); }
            // ---- devices on your network (the ARP table - nothing is scanned), names from your router's DNS
            try
            {
                int ifIndex = nic.GetIPProperties().GetIPv4Properties().Index;
                var arp = Arp.Table().Where(e => e.IfIndex == ifIndex && e.Dynamic && DestCheck.Kind(e.Ip) != "public").Take(40).ToList();
                var ptr = new Dictionary<string, string>();
                if ((DateTime.UtcNow - lanAt).TotalMinutes < 5) lock (lanNames) foreach (var kv in lanNames) ptr[kv.Key] = kv.Value;
                var tasks = arp.Where(e => !ptr.ContainsKey(e.Ip)).Select(e => Task.Factory.StartNew(() => { try { var h = Dns.GetHostEntry(IPAddress.Parse(e.Ip)).HostName; return Tuple.Create(e.Ip, h != e.Ip ? h : ""); } catch { return Tuple.Create(e.Ip, ""); } })).ToArray();
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
            s.Verdict = notes.Count > 0 ? string.Join("\n", notes.Take(3)) : s.Wifi ? Voice.P("Your Wi-Fi is in great shape - strong signal, clean air, quick router.", "Wi-Fi link looks healthy.") : Voice.P("Cable, full speed, no errors. The steadiest link there is.", "Wired link looks healthy.");
            if (s.LocationBlocked) { s.Level = Math.Max(s.Level, 1); s.Verdict = Voice.P("Windows is hiding the Wi-Fi details until you allow location for desktop apps (Settings > Privacy & security > Location > Let desktop apps access your location). Annoying, but that's how Windows 11 protects where you are.", "Wi-Fi details need location access: Settings > Privacy & security > Location > Let desktop apps access your location.") + (notes.Count > 0 ? "\n" + notes[0] : ""); }
            return s;
        }
    }

    // ================================================================== What's running: a plain-English guide to the processes on this PC
    // Static text only. Nothing here closes, changes or uploads anything - AdeeboMeter only explains.
    static class Explain
    {
        public sealed class Entry { public string Friendly, Cat, Tag, What, How, Why, Without; public int Close; }
        // Close: 0 safe to close · 1 leave it, OK to restart if it misbehaves · 2 don't - Windows needs it · 3 managed by IT
        public static readonly string[] CloseText =
        {
            "Safe to close. It's an app - you only lose what's open in it.",
            "Leave it be. If it misbehaves, closing it is OK - Windows (or the app) brings it back.",
            "Don't close it. Windows needs it - closing it can sign you out or crash the PC.",
            "Managed by your IT team. It's protected on purpose, so leave it running."
        };
        public static readonly string[] CloseShort = { "safe to close", "leave it", "Windows needs it", "managed by IT" };
        public const string Core = "Windows core", Sec = "Security", It = "Company / IT tools", App = "Your apps", Help = "Windows helpers", Other = "Other";
        public static readonly string[] CatOrder = { App, It, Sec, Help, Core, Other };

        static readonly Dictionary<string, Entry> map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        static void A(string names, string friendly, string cat, int close, string tag, string what, string how, string why, string without)
        {
            var e = new Entry { Friendly = friendly, Cat = cat, Close = close, Tag = tag, What = what, How = how, Why = why, Without = without };
            foreach (var n in names.Split('|')) map[n] = e;
        }
        public static Entry Find(string raw) { Entry e; return raw != null && map.TryGetValue(raw, out e) ? e : null; }
        public static int Count { get { return map.Values.Distinct().Count(); } }

        static Explain()
        {
            Terms();
            // ---------- the core of Windows
            A("System", "Windows kernel", Core, 2, "The engine room. Everything else rides on it.",
              "Runs the heart of Windows: memory, disks, network and every driver work through here.",
              "It isn't an app at all - it's the kernel itself plus the drivers that talk straight to your hardware. Its CPU and memory are really your drivers' work.",
              "No kernel, no Windows. Every file you open and every packet you send passes through it.",
              "It can't be stopped - if it fails you get a blue screen. If it's busy, a driver is struggling (Wi-Fi, disk, or the antivirus checking files).");
            A("Idle|System Idle Process", "System Idle", Core, 2, "Not a program - it's how much of your CPU is free.",
              "Shows the CPU time nobody is using.",
              "When there's nothing to do, the processor runs this placeholder and naps to save power and heat.",
              "Pure bookkeeping, so the numbers add up to 100%.",
              "Nothing to remove. A big 'Idle' number is good news.");
            A("Registry", "Registry", Core, 2, "Windows' settings book, kept in memory.",
              "Holds the registry - the giant database of settings for Windows and every app.",
              "The kernel keeps the registry in this process's memory so lookups are instant, and saves it to files on disk.",
              "Every app setting, driver option and policy your IT team pushes lives there.",
              "Windows couldn't remember anything - it wouldn't even boot.");
            A("Memory Compression", "Memory Compression", Core, 2, "Zips your RAM so it lasts longer.",
              "Squeezes memory that apps aren't using right now, instead of writing it out to the disk.",
              "Pages that go quiet get compressed in RAM. When the app needs them back, they're unzipped in microseconds - far faster than reading the page file.",
              "It makes your memory feel bigger when Outlook, Teams and Edge all want room at once.",
              "Windows would hit the disk sooner and everything would feel slower. If it's large, your RAM is simply full - close an app or two.");
            A("Secure System", "Secure System", Sec, 2, "A locked vault inside your PC.",
              "Runs Windows' virtualization-based security - a separate, protected world for secrets.",
              "The CPU's virtualization splits memory in two, so even malware with admin rights can't read what's inside the vault.",
              "It guards your sign-in secrets and keeps the kernel honest (Credential Guard, memory integrity).",
              "Stealing passwords would be much easier. On a work laptop IT turns this on deliberately.");
            A("LsaIso", "Credential Guard", Sec, 2, "Your passwords' bodyguard.",
              "Keeps your sign-in secrets inside the Secure System vault.",
              "lsass asks it to do the secret work; the actual keys never leave the protected world.",
              "Even an attacker with admin rights can't copy your credentials out of memory.",
              "Credentials would sit in normal memory - the classic target for attackers.");
            A("smss", "Session Manager", Core, 2, "The first thing Windows starts - the stage manager.",
              "Builds each session when Windows boots or someone signs in.",
              "It starts csrss and wininit / winlogon for every session, sets up the page file, then mostly sleeps.",
              "Every session needs its stage built before the actors arrive.",
              "Windows would never finish starting.");
            A("csrss", "Client / Server Runtime", Core, 2, "Old but vital plumbing for windows and consoles.",
              "Handles parts of window management, console windows and shutdown - one copy per session.",
              "The kernel hands it the jobs that have to run outside the kernel but still be trusted.",
              "Apps couldn't open console windows and Windows couldn't shut down cleanly.",
              "Windows marks it critical - killing it blue-screens the PC.");
            A("wininit", "Windows Start-Up", Core, 2, "Starts the background world, once, at boot.",
              "Launches services, lsass and the rest of the system's background side.",
              "It runs once at boot, starts its children, then waits quietly in case one needs restarting.",
              "It's the parent of everything that runs in the background.",
              "Critical - stopping it crashes Windows.");
            A("winlogon", "Sign-in (Winlogon)", Core, 2, "The front door. Ctrl+Alt+Del lands here.",
              "Handles sign-in, locking, sign-out and the Ctrl+Alt+Del screen.",
              "It shows the lock screen, passes what you type to lsass to check, then starts your desktop.",
              "No front door means no way in - and no safe way to lock the PC when you walk away.",
              "You'd be signed out on the spot.");
            A("services", "Service Control Manager", Core, 2, "The conductor of every background service.",
              "Starts, stops and watches every Windows service - networking, updates, printing, VPN agents.",
              "At boot it reads the list of services, starts each one when it's needed, and restarts the ones that crash.",
              "Hundreds of background jobs need one boss to keep them in order.",
              "No network, no sound, no updates - Windows would grind to a halt.");
            A("lsass", "Local Security Authority", Sec, 2, "The bouncer who checks every ID.",
              "Checks passwords, PINs and smart cards, and hands out the 'tickets' (Kerberos) your PC shows to company servers.",
              "When you sign in or open a file share, it proves who you are and holds your session's security tokens.",
              "Every 'are you allowed in?' question at work - Wi-Fi, VPN, file shares, Outlook - starts here.",
              "Windows restarts the whole PC within a minute. It's also attackers' favourite target, which is why it's so well guarded.");
            A("svchost", "Service Host", Core, 1, "An apartment building for Windows services.",
              "Each copy is home to one or more Windows services: DHCP (your IP address), DNS client (name lookups), Wi-Fi, Windows Update, time sync, Bluetooth and dozens more.",
              "Services are written as libraries, not programs, so they need a host to live in. With enough RAM, Windows gives most services their own copy so one crash can't take the others down.",
              "Your IP address, name lookups and Wi-Fi all live inside svchost.",
              "Kill the wrong copy and the network, the sound or the whole PC goes with it. Seeing 50-90 copies is completely normal.");
            A("dwm", "Desktop Window Manager", Core, 2, "The artist painting every pixel you see.",
              "Draws your whole screen - windows, shadows, transparency, animations.",
              "Each app paints into its own off-screen picture. dwm stacks them together on the graphics chip and sends the finished frame to the monitor.",
              "It's why windows don't smear when you drag them and the screen stays smooth.",
              "The screen flashes black and Windows restarts it. A busy dwm means lots of video, animation or extra monitors.");
            A("explorer", "Explorer (desktop and taskbar)", Core, 1, "Your desktop, taskbar and Start button live here.",
              "Runs the taskbar, the desktop icons, the system tray and every File Explorer window.",
              "One copy runs the 'shell' - the whole desktop experience. Folder windows may add more copies.",
              "It's your home screen.",
              "The taskbar and desktop vanish (your apps keep running). Task Manager > Run new task > explorer brings it back.");
            A("fontdrvhost", "Font Driver Host", Help, 2, "Draws the letters - inside a safe box.",
              "Turns font files into the letters you see.",
              "Fonts are processed in a locked-down process, so a booby-trapped font file can't attack the system.",
              "Every word on your screen goes through it.",
              "Text drawing breaks until Windows restarts it.");
            A("sihost", "Shell Infrastructure Host", Help, 1, "Backstage crew for notifications and the taskbar.",
              "Supports the newer parts of the desktop: notifications, the action centre, parts of Start and the taskbar.",
              "Starts when you sign in and serves those pieces whenever they're needed.",
              "Without it, notifications and parts of the taskbar stop working.",
              "Windows restarts it; the desktop may flicker.");
            A("taskhostw", "Task Host", Help, 1, "Runs Windows' little scheduled chores.",
              "Hosts tasks that run at sign-in, on a timer or after an event - syncing settings, cleaning up.",
              "Task Scheduler starts it with whatever chore is due, then it goes back to sleep.",
              "Lots of quiet housekeeping happens through it.",
              "Those chores just wait until next time.");
            A("ctfmon", "Text Input (CTF)", Help, 1, "Lets you type - in any language, on any keyboard.",
              "Handles keyboard layouts, the emoji panel, handwriting and the touch keyboard.",
              "It sits between your keyboard and your apps, translating input methods.",
              "Typing in the search box and some apps depends on it.",
              "Some text boxes stop accepting typing until it restarts.");
            A("RuntimeBroker", "Runtime Broker", Help, 1, "The permission desk for modern apps.",
              "Checks whether modern apps (Photos, Settings, widgets) may use your camera, microphone, location or files.",
              "One copy per app that needs it; it asks you once and remembers.",
              "It keeps those apps inside their sandbox.",
              "Those apps lose access to what they asked for; Windows restarts it.");
            A("SearchHost|SearchApp", "Windows Search box", Help, 1, "The search box on your taskbar.",
              "Shows the search panel and its results.",
              "It asks the search index (SearchIndexer) for matches and draws the results.",
              "That's how Start search finds apps, files and settings.",
              "Search does nothing until it restarts.");
            A("SearchIndexer|SearchProtocolHost|SearchFilterHost", "Search Indexer", Help, 1, "The librarian who reads everything so you can find it fast.",
              "Builds the index of your files, your Outlook mail and your settings.",
              "When you're not busy it reads new and changed files and notes what's inside. The helper copies open the files; the indexer keeps the catalogue.",
              "It makes search instant instead of crawling the whole disk.",
              "Search becomes slow or empty. It's busy for a while after a big Outlook sync - that's normal.");
            A("StartMenuExperienceHost", "Start menu", Help, 1, "The Start menu itself.",
              "Draws Start.",
              "It starts at sign-in and waits, so Start pops open instantly.",
              "It's how you launch apps.",
              "Start won't open until it restarts.");
            A("ShellExperienceHost", "Taskbar pop-ups", Help, 1, "The clock, calendar and network pop-ups.",
              "Draws the flyouts from the corner of the taskbar.",
              "A modern app that wakes up when you click those icons.",
              "That's where you pick Wi-Fi and check the date.",
              "Those pop-ups don't open until it restarts.");
            A("TextInputHost", "Emoji and clipboard panel", Help, 1, "The emoji panel, clipboard history and touch keyboard.",
              "Shows the emoji panel (Win + .), clipboard history (Win + V) and the touch keyboard.",
              "Wakes up when you ask for input help.",
              "Handy shortcuts for typing.",
              "Those panels don't appear.");
            A("conhost", "Console Host", Help, 0, "The window behind every black command box.",
              "Draws the window for cmd, PowerShell and command-line tools - including tshark and ping when AdeeboMeter runs them.",
              "One copy per command-line program; it handles the text and the keyboard, even when the window is hidden.",
              "Command-line tools need a window, even an invisible one.",
              "The tool attached to it closes with it.");
            A("dllhost", "COM Surrogate", Help, 1, "A disposable box for risky little jobs.",
              "Runs small pieces of code - like thumbnail makers - outside Explorer.",
              "If a thumbnail maker crashes on a weird file, only this box dies, not your Explorer.",
              "It keeps Explorer stable.",
              "Thumbnails or a feature stop briefly; it comes back when needed.");
            A("WmiPrvSE", "WMI Provider Host", Help, 1, "Answers 'tell me about this PC' questions.",
              "Answers questions that apps and IT tools ask about the PC - hardware, software, settings, battery.",
              "Management tools, antivirus and scripts query WMI; this process fetches the answers.",
              "It's how IT and many apps see what's on your laptop.",
              "Those tools get no answers until it restarts. Constant high CPU means some tool keeps asking questions.");
            A("spoolsv", "Print Spooler", Help, 1, "The print queue.",
              "Queues print jobs and talks to printer drivers.",
              "Apps hand it pages; it feeds them to the printer at the printer's pace.",
              "So you can keep working while a 40-page PDF prints.",
              "No printing - and on some PCs no 'Print to PDF'.");
            A("audiodg", "Windows Audio Engine", Help, 1, "Every sound you hear is mixed here.",
              "Mixes and processes audio - Teams calls, videos, notification dings.",
              "Sound effects and enhancements run in this isolated process so a bad audio driver can't take down Windows.",
              "Sound, plus headset effects and noise suppression.",
              "Sound stops for a moment, then comes back. High CPU here usually means a headset 'enhancement' driver.");
            A("LockApp", "Lock screen", Help, 1, "The picture and clock on your lock screen.",
              "Draws the lock screen.",
              "Starts when you lock the PC and idles otherwise.",
              "It's what people see when you step away.",
              "Windows restarts it next time you lock.");
            A("ApplicationFrameHost", "App Frame Host", Help, 1, "The window frame around modern apps.",
              "Draws the title bar and border for modern apps like Settings and Calculator.",
              "The app draws its content; this process draws the frame around it.",
              "Moving and resizing those apps.",
              "Those apps' windows close.");
            A("backgroundTaskHost", "Background Task Host", Help, 1, "Short errands for modern apps.",
              "Runs quick background jobs for modern apps - live tiles, notifications, syncing.",
              "Starts, does the job, and exits.",
              "Keeps those apps up to date without opening them.",
              "The errand waits until next time.");
            A("dasHost", "Device Association", Help, 1, "Pairs Bluetooth and network devices.",
              "Finds and pairs devices like Bluetooth headsets and network printers.",
              "Listens for devices the PC already knows and connects them.",
              "Your headset connecting by itself.",
              "Devices may not reconnect automatically.");
            A("WUDFHost", "User-Mode Driver Host", Help, 2, "Runs some drivers in a safe box.",
              "Runs drivers for things like fingerprint readers, sensors and some USB devices.",
              "These drivers run outside the kernel, so a crash takes down the device - not the PC.",
              "Windows Hello fingerprint and many gadgets depend on it.",
              "That device stops working until Windows restarts it.");
            A("TrustedInstaller|TiWorker", "Windows Modules Installer", Help, 2, "Installs Windows updates.",
              "Installs and removes Windows updates and features.",
              "It unpacks update packages and swaps system files safely - the only process allowed to change them.",
              "Security patches.",
              "Updates fail half-way. It's busy after Patch Tuesday - let it finish.");
            A("MoUsoCoreWorker|usoclient", "Update Orchestrator", Help, 1, "Decides when to check for updates.",
              "Checks for Windows updates and schedules installs and restarts.",
              "Wakes up on a schedule, asks Windows Update (or your company's update server), then hands work to the installer.",
              "Keeps the laptop patched.",
              "Updates are delayed until it runs again.");
            A("smartscreen", "SmartScreen", Sec, 1, "Checks downloads before you run them.",
              "Warns about unknown or unsafe apps and files.",
              "When you launch something new it checks the file's reputation.",
              "It stops a lot of malware right at the door.",
              "No warnings on dodgy downloads.");

            // ---------- security
            A("MsMpEng", "Microsoft Defender Antivirus", Sec, 3, "The guard who checks every file at the door.",
              "Scans files as they're opened, downloaded or written, and runs full scans when the PC is idle.",
              "A filter driver hands it each file before the app gets it; Defender checks it against signatures and behaviour rules.",
              "Your first line of defence against malware - and required by company policy.",
              "It can't be stopped (tamper protection). High CPU usually means a scan or a big file copy - it settles down.");
            A("MpDefenderCoreService", "Defender Core Service", Sec, 3, "Defender's helper.",
              "Supports Defender's engine and keeps it updated.",
              "Runs alongside MsMpEng.",
              "Keeps protection current.",
              "Protected - managed by Windows and IT.");
            A("NisSrv", "Defender Network Inspection", Sec, 3, "Watches traffic for known attacks.",
              "Inspects network traffic for known exploit patterns.",
              "Matches what flows in and out against attack signatures.",
              "Catches attacks that arrive over the network.",
              "Protected - managed by Windows and IT.");
            A("MsSense|SenseIR|SenseCncProxy|SenseNdr", "Defender for Endpoint", It, 3, "The company's security camera on your laptop.",
              "Records security events on this PC and reports them to your company's security team.",
              "It watches program starts, network connections and file changes, and raises alerts in Microsoft Defender for Endpoint.",
              "The security team can spot and stop an attack across every company PC.",
              "Protected and managed by IT - don't try to stop it.");
            A("SecurityHealthService|SecurityHealthSystray", "Windows Security", Sec, 3, "The shield icon and the dashboard behind it.",
              "Shows your protection status.",
              "Collects status from Defender, the firewall and Windows Update.",
              "It tells you when something is off.",
              "The icon disappears; the protection itself keeps running.");
            A("SgrmBroker", "System Guard", Sec, 3, "Checks that Windows started untampered.",
              "Measures that the system booted cleanly.",
              "Uses the CPU's security features to check the boot chain.",
              "Lets company systems trust that your laptop is clean.",
              "Protected.");

            // ---------- company / IT tools
            A("CcmExec", "Configuration Manager (SCCM)", It, 3, "IT's remote hands.",
              "Installs software and patches your company pushes, and reports what's installed.",
              "It checks in with IT's servers on a schedule and runs the jobs it's given.",
              "That's how approved software and updates reach your laptop.",
              "Managed by IT - leave it running.");
            A("IntuneManagementExtension|AgentExecutor", "Intune Management", It, 3, "IT's remote hands, cloud edition.",
              "Applies company settings, scripts and apps from Microsoft Intune.",
              "Checks in with Intune, then installs or configures what IT assigned.",
              "It keeps the laptop compliant with company policy.",
              "Managed by IT - leave it running.");
            A("OfficeClickToRun", "Office Updater", It, 1, "Keeps Office installed and up to date.",
              "Installs and updates Word, Excel, Outlook and the rest.",
              "Downloads updates in the background and applies them when the apps are closed.",
              "Security fixes for Office.",
              "Office stops updating and some Office apps may not start.");
            A("vpnagent|csc_ui|vpnui|acwebhelper", "Cisco Secure Client (VPN)", It, 3, "Your tunnel into the company network.",
              "Builds and holds the VPN tunnel.",
              "It creates a virtual network adapter and encrypts traffic to the company's VPN gateway (TLS / DTLS). The window you click is only its face; the agent does the work.",
              "Internal sites, lab servers and internal names only work through it.",
              "The VPN drops and internal names stop resolving.");
            A("CSFalconService|CSFalconContainer", "CrowdStrike Falcon", It, 3, "A company security sensor.",
              "Watches the PC for attacks and reports to the security team.",
              "A kernel sensor records activity and sends detections to CrowdStrike's cloud.",
              "Protects the company's network from a compromised laptop.",
              "Protected and managed by IT.");
            A("ZSATunnel|ZSAService|ZSATray|ZSATrayManager", "Zscaler", It, 3, "The company's internet security gateway.",
              "Sends your web traffic through the company's cloud security service.",
              "A local tunnel forwards traffic to Zscaler, which applies company policy before it reaches the internet.",
              "The same protection on any network - home, hotel or office.",
              "Managed by IT. If websites fail but ping works, it's worth a look here.");

            // ---------- your apps
            A("msedgewebview2", "Edge WebView2", App, 1, "A mini Edge inside other apps.",
              "Lets apps like the new Outlook, Teams and Widgets show web content.",
              "Each app gets its own small copies of the Edge engine.",
              "Much of modern Office is really web pages inside a window.",
              "The app that owns it shows blank panels or closes. Many copies is normal.");
            A("msedge", "Microsoft Edge", App, 0, "Your browser. Every tab is its own process.",
              "The web browser.",
              "Edge splits tabs, extensions and graphics into separate processes, so one bad tab can't crash the rest. That's why you see so many copies.",
              "Browsing, SharePoint, web apps.",
              "Safe to close - you lose your tabs (Edge can restore them).");
            A("chrome", "Google Chrome", App, 0, "A browser. Every tab is its own process.",
              "The web browser.",
              "Like Edge, Chrome runs each tab and extension in its own process for safety and stability.",
              "Browsing.",
              "Safe to close - you lose your tabs (Chrome can restore them).");
            A("firefox", "Firefox", App, 0, "A browser.",
              "The web browser.",
              "Splits content into a handful of processes for safety.",
              "Browsing.",
              "Safe to close.");
            A("ms-teams|teams|msteams", "Microsoft Teams", App, 0, "Chat, calls and meetings.",
              "Teams chat, calls and meetings.",
              "Built on web technology (WebView2). Calls send audio and video as UDP streams - ports 50000-50059 by default - straight to Microsoft's media relays.",
              "Calls and chat at work.",
              "Safe to close, but you'll miss calls. During a call Teams is the biggest user of your network and CPU.");
            A("outlook", "Outlook (classic)", App, 0, "Mail and calendar.",
              "Email, calendar and contacts.",
              "Keeps a copy of your mailbox on disk (an OST file) and syncs it with the mail server.",
              "Mail.",
              "Safe to close - new mail waits on the server. Busy CPU and disk right after opening means it's syncing.");
            A("olk", "Outlook (new)", App, 0, "Mail and calendar, web-based.",
              "Email, calendar and contacts.",
              "The new Outlook is the web version of Outlook inside a window (WebView2).",
              "Mail.",
              "Safe to close.");
            A("onedrive", "OneDrive", App, 0, "Keeps your files in the cloud and in sync.",
              "Syncs your OneDrive and SharePoint files.",
              "It watches your folders, uploads changes, and downloads files only when you open them.",
              "Backup and sharing.",
              "Closing pauses syncing; your files stay safe and sync when it's back. Heavy upload means a big sync.");
            A("WINWORD", "Word", App, 0, "Documents.", "Microsoft Word.", "A classic desktop app; files can live on your disk or in OneDrive.", "Writing.", "Safe to close - save first.");
            A("EXCEL", "Excel", App, 0, "Spreadsheets.", "Microsoft Excel.", "A classic desktop app; big workbooks can use a lot of memory and CPU while recalculating.", "Numbers.", "Safe to close - save first.");
            A("POWERPNT", "PowerPoint", App, 0, "Slides.", "Microsoft PowerPoint.", "A classic desktop app; video and images in decks use memory.", "Presentations.", "Safe to close - save first.");
            A("wireshark", "Wireshark", App, 0, "The packet microscope.", "Shows captured packets.", "Reads capture files or live captures made by dumpcap.", "Seeing exactly what's on the wire.", "Safe to close.");
            A("dumpcap", "Wireshark capture engine", App, 0, "The recorder behind Wireshark.", "Writes packets from the network card to a file.", "Npcap hands it a copy of every packet; it writes them to pcapng files. AdeeboMeter uses it for Sniff - only when you ask.", "Captures.", "Safe to close - the capture stops.");
            A("tshark", "tshark (Wireshark's brain)", App, 0, "Wireshark without the window.", "Decodes packets into protocols.", "AdeeboMeter runs it only while Live capture is on, and stops it when you leave that tab.", "Live protocol breakdown.", "Safe to close - live capture stops.");
            A("putty", "PuTTY", App, 0, "Your SSH and serial terminal.", "Connects to routers and servers.", "Opens SSH, Telnet or serial sessions in a window.", "Lab and router work.", "Safe to close - the session ends.");
            A("Code", "VS Code", App, 0, "The code editor.", "Edits code and text.", "Built on web technology, so it runs several helper processes.", "Scripts and configs.", "Safe to close - save first.");
            A("cmd|powershell|pwsh|WindowsTerminal|OpenConsole", "Command shell", App, 0, "Where you type commands.", "A command prompt or PowerShell window.", "Runs the commands you type; its window is drawn by conhost or Windows Terminal.", "Scripting and troubleshooting.", "Safe to close - anything running in it stops.");
            A("SystemSettings", "Settings", App, 0, "The Settings app.", "Windows settings.", "A modern app.", "Changing settings.", "Safe to close.");
            A("Widgets|WidgetService", "Widgets", App, 0, "News and weather on the taskbar.", "The Widgets board.", "A web-based panel that refreshes in the background.", "Glanceable news and weather.", "Safe to close; it comes back when you open Widgets.");
            A("PhoneExperienceHost", "Phone Link", App, 0, "Your phone, on your PC.", "Links an Android phone or iPhone to the PC.", "Talks to your phone over Bluetooth and the network.", "Texts and calls on the PC.", "Safe to close.");
            A("msiexec", "Windows Installer", Help, 1, "Installs and repairs apps.", "Runs .msi installers.", "Starts when something is being installed, repaired or removed.", "Software installs.", "The install in progress fails.");
            A("AdeeboMeter", "AdeeboMeter", App, 0, "That's me, boss.",
              "Watches your PC and network so you don't have to open Task Manager.",
              "Reads Windows' own counters once a second, never listens on the network, and only sends small probes when it checks your path.",
              "So you know whether it's you or the network.",
              "Safe to close - and when it's closed, nothing of it keeps running.");
        }


        // ---------- glossary: every number and label in AdeeboMeter, explained (What · How · Why it matters · When it looks bad)
        static readonly Dictionary<string, Entry> terms = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        static void G(string keys, string title, string tag, string what, string how, string why, string bad)
        {
            var e = new Entry { Friendly = title, Cat = "term", Tag = tag, What = what, How = how, Why = why, Without = bad, Close = -1 };
            foreach (var k in keys.Split('|')) terms[k] = e;
        }
        public static Entry Term(string key) { Entry e; return key != null && terms.TryGetValue(key, out e) ? e : null; }
        public static IEnumerable<Entry> AllTerms { get { return terms.Values.Distinct(); } }

        static void Terms()
        {
            // the path strip
            G("path.PC", "Your PC", "Step one: is the problem on this laptop?",
              "Checks whether the laptop itself is the bottleneck - CPU, memory and threads.",
              "Reads Windows' own counters once a second. No network involved.",
              "If the PC is choking, every website feels slow - and that's not the network's fault.",
              "Red here means close something heavy. The other dots can be green and it still feels slow.");
            G("path.Wi-Fi|path.LAN", "Your local link", "Step two: the hop from you to your router.",
              "Shows how well you reach your router (the gateway) - over Wi-Fi or a cable.",
              "Pings the gateway every few seconds and reads Wi-Fi signal, channel and speed from Windows.",
              "This is where most home and hotel problems live: weak signal, crowded channel, a busy router.",
              "Slow or lost gateway pings mean the trouble is in the room - move closer, plug in a cable, or restart the router.");
            G("path.ISP|path.VPN", "Your provider / VPN", "Step three: from your router out to the world.",
              "Shows the jump from your router into your internet provider - or into the company VPN.",
              "Compares the time to the internet with the time to your gateway; the difference is what the provider (or VPN) adds.",
              "When this step is slow, rebooting your laptop won't help.",
              "A big jump past the router means the provider or the VPN gateway is slow - not you.");
            G("path.DNS", "DNS", "The internet's phone book.",
              "Turns names like github.com into addresses computers can dial.",
              "Times a real lookup to the DNS server Windows is using, skipping the cache.",
              "If DNS is slow, every new website waits before it even starts loading.",
              "Slow or failing DNS feels like 'the internet is broken' even when the link is fine. On VPN, DNS goes through the tunnel.");
            G("path.Internet", "The internet", "The last step: can you reach the outside world?",
              "Checks that a well-known public address answers.",
              "One small ping to a public DNS server (1.1.1.1 or 8.8.8.8) every 30 seconds - more often while this window is open.",
              "Tells 'my Wi-Fi is fine' apart from 'the internet is down'.",
              "No reply with a healthy gateway means the outage is past your router. Some networks block ping - then this stays grey.");
            // TCP / IP health counters
            G("TCP retransmits", "TCP retransmits", "Packets that had to be sent twice.",
              "Counts TCP segments Windows had to send again because no acknowledgement came back.",
              "Read from Windows' TCP statistics every second - the same numbers 'netstat -s' shows.",
              "A few is normal. Lots means packets are being lost somewhere - weak Wi-Fi, a congested link, a bad cable.",
              "Steady retransmits make downloads crawl and calls stutter. Check the 'Why:' line - it says when they started.");
            G("TCP resets sent", "TCP resets sent", "Connections slammed shut.",
              "Counts TCP resets (RST) - a hard 'go away' instead of a polite goodbye.",
              "Read from Windows' TCP statistics.",
              "Bursts happen when apps close or a firewall cuts sessions.",
              "A sudden flood means something is killing connections - a VPN reconnect, a firewall, or an app crashing.");
            G("Failed connection attempts", "Failed connection attempts", "Knocks on doors that never opened.",
              "Counts connections that never got established.",
              "Read from Windows' TCP statistics, totalled over the last minute.",
              "Shows when apps are trying to reach things they can't.",
              "Many failures mean a blocked port, a dead server, a proxy problem or a VPN that dropped.");
            G("ICMP unreachable received", "ICMP unreachable", "Routers saying 'can't get there from here'.",
              "Counts 'destination unreachable' messages routers and hosts send back.",
              "Read from Windows' ICMP statistics.",
              "Tells you a route or a port is closed somewhere along the way.",
              "A stream of these usually means a routing problem or a firewall rejecting traffic.");
            G("UDP datagrams dropped", "UDP datagrams dropped", "UDP packets that arrived with nowhere to go.",
              "Counts UDP packets Windows had to throw away - no app listening, or a full buffer.",
              "Read from Windows' UDP statistics.",
              "UDP carries DNS, Teams media and VPN (DTLS) traffic.",
              "Lots of drops during a call can mean the PC is too busy to keep up.");
            // this PC
            G("CPU", "CPU", "How hard the processor is working.",
              "Share of processor time in use across all cores.",
              "Read from Windows (GetSystemTimes) every second.",
              "When it's pegged, everything waits in line - typing, video, calls.",
              "Over 85% for long: look at 'Top by CPU' and close the hungriest app.");
            G("Memory", "Memory", "Your desk space.",
              "How much of your RAM is in use. 'Committed' is everything apps have asked for, including what spilled to the page file.",
              "Read from Windows (GlobalMemoryStatusEx).",
              "When memory runs out, Windows shuffles to disk and everything slows to a crawl.",
              "Above 90%: close a big app - 'Top by memory' shows who.");
            G("Disk", "Disk", "How busy the drive is.",
              "Share of time the disk is busy reading or writing.",
              "Windows' PhysicalDisk performance counters.",
              "A busy disk makes apps freeze for a moment when they open files.",
              "Pinned at 100% means updates, antivirus scans, OneDrive or Outlook syncs - usually it passes.");
            G("Download", "Download", "How much is flowing in right now.",
              "Megabits per second arriving on your active network adapter.",
              "Windows' adapter byte counters, read every second.",
              "Tells you whether a slow site is slow - or your line is simply full.",
              "Near your line's limit: something is downloading (updates, OneDrive) and everything else waits.");
            G("Top by memory", "Top by memory", "Who's using your desk space.",
              "The apps holding the most RAM, with all their processes added up.",
              "Process list read every 10 seconds (every 2 s with this window open).",
              "The quickest way to know what to close.",
              "Browsers and Teams add up fast - each tab or panel is its own process.");
            G("Top by CPU", "Top by CPU", "Who's keeping the processor busy.",
              "The apps using the most processor time right now, all their processes added up.",
              "Process CPU time compared between two reads.",
              "Finds the culprit when the fan spins up.",
              "Antivirus and indexing spikes come and go; an app stuck high is the one to close.");
            // check a destination
            G("step.DNS", "DNS lookup", "Find the address.", "Asks your DNS server for the name's address (IPv4 and IPv6), skipping the cache.", "A real query through Windows' DNS client, timed.", "No address, no connection - everything else depends on this.", "'No such name' = typo or an internal name that needs the VPN. 'Timed out' = DNS server trouble.");
            G("step.Route", "Route", "Which door it leaves by.", "Shows which adapter Windows would use to reach that address - Wi-Fi, cable or VPN.", "Asks Windows' routing table by opening a UDP socket - nothing is sent.", "Catches 'it's going around the VPN' (or through it) surprises.", "'No route' means Windows has no path at all - adapter down or VPN routes missing.");
            G("step.Ping", "Ping", "Is anyone home?", "Four ICMP echo requests to the address.", "Measures the round trip and how many replies came back.", "A quick health check of the path.", "Many servers block ping on purpose - 'no reply' alone doesn't mean it's down.");
            G("step.TCP", "TCP connect", "Knock on the door.", "Opens a TCP connection to the port.", "The three-way handshake: SYN, SYN-ACK, ACK - timed.", "If TCP connects, the network path and the firewall are fine.", "'Refused' = server is there but nothing listens on that port. 'Timeout' = a firewall is silently dropping, or the server is down.");
            G("step.TLS", "TLS handshake", "Agree on a secret and check ID.", "Sets up encryption and checks the server's certificate with Windows.", "TLS 1.2 or 1.3 only. Windows validates the certificate chain; if it isn't trusted, AdeeboMeter stops and sends nothing.", "This is where inspection proxies, expired certificates and MTU black holes show up.", "'Untrusted' = certificate problem or a proxy in the middle. 'Stalls' = something drops the big certificate packets (often MTU).");
            G("step.HTTP", "HTTP", "Does the website answer?", "Sends one HEAD request - like asking 'are you there?' without downloading the page.", "Reads only the status line (200 OK, 404, 503...) and the time to first byte.", "Separates 'the network is fine' from 'the site itself is broken'.", "4xx = you're not allowed or the page is gone. 5xx = the server is broken - not you.");
            G("step.Path MTU", "Path MTU", "The biggest packet that fits through the pipe.", "Finds the largest packet that reaches the server without being split.", "Sends pings with 'don't fragment' set and narrows the size down.", "Tunnels (VPN, LTE, Starlink) shrink the pipe. Big packets that don't fit vanish.", "Below 1500 is normal on VPN. Trouble starts when big packets disappear silently - pages hang half-loaded.");
            G("step.Path", "Path (traceroute)", "Every router on the way.", "Lists the routers between you and the server, with time and loss at each.", "MTR-style: three rounds of probes with growing hop limits, all at once.", "Shows where along the way the delay or loss begins.", "Loss on a middle hop that doesn't continue to the end is just a router ignoring pings - only loss that carries on to the last hop matters.");
            G("card.SIGN-IN PAGE (CAPTIVE PORTAL)", "Sign-in page check", "Hotel and airport Wi-Fi, caught red-handed.", "Asks the same tiny test page Windows uses to decide 'sign-in required'.", "One plain HTTP request to msftconnecttest.com; if anything other than Microsoft's answer comes back, a portal is in the way. The redirect is never followed.", "A sign-in page blocks everything until you accept it - and makes every other test look broken.", "'Found' = open a browser and sign in, then check again.");
            G("card.WHO'S TALKING TO IT", "Who's talking to it", "Apps on this PC connected to that destination.", "Lists the apps on your PC with open connections to that address right now.", "Read from Windows' connection table - no capture needed.", "Answers 'which app is it?' in one glance.", "Nobody = no app is connected to it at the moment.");
            // connection states
            G("state.ESTABLISHED", "ESTABLISHED", "A live conversation.", "Both sides are connected and can send data.", "The TCP handshake finished.", "This is what working connections look like.", "Lots of them to one place is normal for browsers and Teams.");
            G("state.SYN_SENT", "SYN_SENT", "Knocked - waiting for an answer.", "Your PC asked to connect and is waiting for the reply.", "The first packet of the TCP handshake went out; no SYN-ACK yet.", "A few for a split second is normal.", "Stuck for seconds = blocked by a firewall, a dead server, or a route that goes nowhere.");
            G("state.TIME_WAIT", "TIME_WAIT", "Hung up - waiting a moment to be sure.", "The connection closed; Windows keeps the slot for a minute or two.", "So late packets from the old conversation can't confuse a new one. Windows shows these as owned by process 0.", "Normal after browsing.", "Thousands at once means an app is opening and closing connections very fast.");
            G("state.CLOSE_WAIT", "CLOSE_WAIT", "The other side hung up; this app hasn't.", "The server closed the connection and the app on your PC hasn't closed its end yet.", "Waiting for the app to call close().", "Usually brief.", "Many piling up = an app isn't cleaning up after itself (a bug).");
            G("state.LISTEN|state.BOUND", "LISTEN / BOUND", "Waiting for calls, or holding a number.", "LISTEN = an app waits for incoming connections. BOUND = a port reserved but not in use.", "Shown only when you turn on 'Bound / listening'.", "Tells you which apps accept connections into your PC.", "AdeeboMeter itself never listens - it checks that about itself every time it starts.");
            G("state.SYN_RCVD", "SYN_RCVD", "Someone knocked on your door.", "Another machine asked to connect to an app on your PC, and your PC answered.", "The middle step of the TCP handshake, seen only on ports an app listens on.", "Rare on a laptop.", "Lots of them = something is trying to connect in (a scan, or a misconfigured app).");
            G("state.CLOSED|state.DELETE_TCB", "CLOSED", "Finished - about to disappear.", "The connection is fully closed and Windows is tidying up.", "The last step after a goodbye or a reset; the row vanishes moments later.", "Normal and short-lived.", "Nothing to do.");
            G("state.FIN_WAIT1|state.FIN_WAIT2|state.LAST_ACK|state.CLOSING", "Closing states", "Saying goodbye.", "The connection is in the middle of closing.", "TCP closes in a polite four-step goodbye; these are the steps.", "Normal and short-lived.", "Lots stuck here = a firewall dropping the goodbye packets.");
            // live capture
            G("live.PROTOCOL HIERARCHY", "Protocol hierarchy", "What your traffic is made of.", "Breaks every packet down into its layers: Ethernet, IP, TCP/UDP, then TLS, DNS, QUIC and so on.", "tshark (Wireshark's engine) decodes each packet; AdeeboMeter counts them.", "Shows at a glance what's eating the line.", "Lots of QUIC/UDP = video or Teams; lots of TLS = normal web traffic.");
            G("live.EXPERT INFO", "Expert info", "Wireshark's own warnings.", "The problems Wireshark spots in the traffic, grouped.", "tshark's expert analysis, counted per category.", "These point at real network trouble.", "Retransmissions and duplicate ACKs together = packet loss on the path.");
            G("live.TOP CONVERSATIONS", "Top conversations", "Who you're talking to, and how much.", "The busiest flows right now, named from the TLS server name or the DNS answer in the traffic.", "Built from tshark's decode - names come from the packets themselves.", "Finds the bandwidth hog by name, not just by IP.", "A conversation with retransmissions is the one suffering.");
            G("TCP retransmissions", "TCP retransmissions", "Packets sent again.", "Wireshark saw the same data sent twice.", "It spots repeated sequence numbers.", "Each one costs time.", "Many = loss on the path (Wi-Fi, congestion).");
            G("Duplicate ACKs", "Duplicate ACKs", "'I'm still missing a piece!'", "The receiver keeps acknowledging the same point because a packet went missing.", "Wireshark sees the same ACK number repeated.", "The early warning of loss.", "Three in a row trigger a fast retransmit.");
            G("Segments lost before capture", "Lost before capture", "A gap in the stream.", "Data that never reached your PC (or the capture missed).", "Wireshark sees a jump in sequence numbers.", "Points at upstream loss.", "Many = the path is dropping packets.");
            G("Out of order", "Out of order", "Packets arriving shuffled.", "Packets came in a different order than they were sent.", "Wireshark compares sequence numbers.", "Some is normal on busy paths.", "Lots can mean multiple paths or a struggling link.");
            G("Zero window", "Zero window", "'Stop - I'm full!'", "The receiver's buffer is full and it asked the sender to pause.", "TCP window size reached zero.", "Means the slow part is an app or PC, not the network.", "Seen from your side = your PC or app can't keep up.");
            G("Connection resets (RST)", "Connection resets", "Hard hang-ups.", "Connections cut with a reset instead of a normal close.", "Wireshark counts TCP RST flags.", "Firewalls and crashing apps cause them.", "Floods of resets = something is actively killing sessions.");
            G("TLS alerts", "TLS alerts", "The encryption layer complaining.", "TLS sent an alert - often a certificate or version problem.", "Decoded from the TLS records.", "Explains 'site can't be reached' errors.", "Look for an inspection proxy or an old server.");
            // health check
            G("hc.Uptime", "Uptime", "How long since the last restart.", "Time since Windows last started.", "Read from Windows.", "Many problems vanish after a restart - and 'Fast startup' means shutting down isn't a real restart.", "Over a week: restart before you blame the network.");
            G("hc.Pending reboot", "Pending reboot", "Windows is waiting for you.", "Whether updates are waiting for a restart.", "Checks the registry flags Windows Update and installers set.", "Half-installed updates can break networking and VPN.", "Yes = restart when you can.");
            G("hc.Disk", "Disk space", "Room left on the drive.", "Free space on each drive.", "Read from Windows.", "Windows, Outlook and updates need room to work.", "Under 10% things start failing; under 20% it's getting tight.");
            G("hc.Adapter errors", "Adapter errors", "Damaged packets at the network card.", "Errors and discards counted by your network adapter.", "Read from Windows' adapter statistics.", "Points at cables, docks, drivers or radio trouble.", "Climbing errors on a cable = try another cable or dock port.");
            G("hc.Wi-Fi neighbourhood|hc.Wi-Fi", "Wi-Fi", "Your radio and its neighbours.", "Signal, channel and how many other access points share your channel.", "Read from Windows (netsh) - no scanning by AdeeboMeter.", "Crowded channels and weak signal are the top cause of slow Wi-Fi.", "Signal under 50% or many neighbours on your channel: move closer, or use 5 GHz.");
            G("hc.Proxy", "Proxy", "A middleman for your web traffic.", "Whether browsers send traffic through a proxy.", "Read from Windows' internet settings.", "Proxies can block, slow or inspect traffic.", "If a site works in AdeeboMeter's check but not in the browser, look here.");
            G("hc.VPN", "VPN", "Your tunnel into the company.", "Whether a VPN is up, and its packet size (MTU).", "Read from the network adapters.", "Company sites need it; it also changes DNS and routes.", "Internal names failing = check the VPN first.");
            G("hc.DNS server", "DNS server", "Who answers your name lookups.", "The DNS server Windows is using.", "Read from the active adapter.", "Wrong or slow DNS breaks everything at once.", "On VPN it should be the company's server.");
            G("hc.Memory", "Memory (committed)", "Everything apps have asked for.", "Committed memory as a share of what Windows can give.", "Read from Windows.", "Past 90% Windows starts struggling.", "Close the top app shown.");
            G("hc.Power", "Power", "Battery and power saving.", "Whether you're on battery or battery saver.", "Read from Windows.", "Battery saver throttles CPU and Wi-Fi.", "Plug in before a big call.");
            // your connection
            G("link.RADIO", "Your Wi-Fi radio", "Everything Windows knows about your Wi-Fi link.", "The network, the access point, band, channel, speed and security of your Wi-Fi.", "Read from Windows (netsh wlan) only while this page is open. Nothing is sent anywhere.", "Most 'slow internet' at home or in a hotel is really a Wi-Fi problem - this card tells you which one.", "Weak signal, a crowded channel or 2.4 GHz are the usual suspects.");
            G("link.CABLE", "Your cable", "The wired link.", "Your Ethernet adapter and the speed it agreed with the switch or dock.", "Read from the adapter itself.", "Cable is the steadiest link there is - if it's slow, the cable, port or dock is the suspect.", "Anything below 1 Gb/s on a modern network means a worn cable, an old port or a cheap dock.");
            G("link.Network (SSID)", "SSID", "The network's name.", "The name of the Wi-Fi network you joined.", "Broadcast by the access point.", "Several access points can share one name - you hop between them as you move.", "Joined the guest network by mistake? It's often slower and filtered.");
            G("link.Access point (BSSID)", "BSSID", "The exact access point you're talking to.", "The hardware address of the specific access point radio.", "Every access point radio has its own BSSID, even when they share the network name.", "When it changes, you roamed to another access point.", "Changing often while you sit still = two access points fighting over you.");
            G("link.Band", "Band", "The radio 'lane' you're on.", "2.4 GHz, 5 GHz or 6 GHz.", "2.4 GHz reaches far but is slow and crowded; 5 GHz is faster; 6 GHz (Wi-Fi 6E / 7) is the fast, empty lane.", "The band decides your top speed and how much interference you get.", "Stuck on 2.4 GHz near the access point? Your network or laptop may be forcing it.");
            G("link.Channel", "Channel", "Your slice of the band.", "The radio channel the access point uses.", "Everyone on the same channel takes turns to talk. On 2.4 GHz neighbouring channels overlap too - only 1, 6 and 11 are truly separate.", "A shared channel means shared air time - and slower Wi-Fi for everyone.", "Many neighbours on your channel: ask IT (or your router settings) for a quieter channel.");
            G("link.Standard", "Wi-Fi standard", "How modern the radio is.", "The Wi-Fi generation in use: Wi-Fi 4 (n), 5 (ac), 6/6E (ax) or 7 (be).", "Both your laptop and the access point must support a generation to use it.", "Newer generations are faster and cope better with crowds.", "Wi-Fi 4 or older on a modern laptop = old access point.");
            G("link.Signal", "Signal", "How loud the access point sounds.", "Signal strength as a percentage, and in dBm (closer to 0 is stronger: -50 great, -70 OK, -80 poor).", "Measured by your Wi-Fi card on every packet.", "Weak signal forces slower speeds and more retries.", "Below about 40%: move closer, or plug in.");
            G("link.Receive rate|link.Transmit rate", "Link rate", "The speed the radio agreed on.", "How fast your laptop and the access point currently talk, each direction.", "Chosen many times a second based on signal and noise.", "Your real internet speed can't be faster than this.", "A rate that drops suddenly means signal or interference trouble.");
            G("link.Security", "Security", "How your Wi-Fi is locked.", "The authentication (WPA2 / WPA3) and encryption (CCMP / GCMP) in use.", "Agreed when you connect.", "Open or WEP networks let others read your traffic.", "Open network? Treat it as public - use the VPN.");
            G("link.Sharing your channel", "Sharing your channel", "Neighbours on your air.", "Other access points on the same (or overlapping) channel.", "Counted from Windows' own Wi-Fi scan list.", "Every one of them competes for the same air time.", "Three or more strong neighbours = noticeably slower Wi-Fi.");
            G("link.Air time used", "Air time used", "How busy the channel is.", "The share of time the channel is busy, as reported by your access point (BSS load).", "Access points that support it announce this in their beacons; Windows passes it on.", "A busy channel means waiting to talk.", "Above ~60% the air is congested - everyone slows down.");
            G("link.Connection|link.Adapter", "Adapter", "The hardware you're using.", "Your network adapter.", "Reported by Windows.", "Driver and hardware decide what speeds are possible.", "USB and dock adapters are a common weak spot.");
            G("link.Link speed", "Link speed", "The speed your cable agreed on.", "The speed your adapter and the switch negotiated.", "Both ends test the cable and pick the fastest they can both do.", "This is the ceiling for everything you do on the wire.", "100 Mb/s instead of 1 Gb/s = damaged cable, old port or dock.");
            G("link.IPv4 address", "IPv4 address", "Your address on this network.", "The address your PC uses on this network, and its mask (how big the network is).", "Usually handed out by the router (DHCP).", "No address, no conversations.", "169.254.x.x = nobody gave you an address - DHCP failed.");
            G("link.IPv6", "IPv6", "The newer address system.", "Your public-style IPv6 address, if the network offers one.", "Assigned by the router or provider alongside IPv4.", "Many sites prefer IPv6 when it's there.", "A broken IPv6 path can make some sites slow to open while others are fine.");
            G("link.Gateway (router)", "Gateway", "Your door to everything else.", "The router your PC sends all outside traffic to.", "Learned from DHCP or set by hand.", "If the gateway is slow or missing, nothing beyond it works.", "No gateway = you can talk to the local network only.");
            G("link.DNS servers", "DNS servers", "Your phone book.", "The DNS servers this adapter uses to turn names into addresses.", "Learned from DHCP, the VPN, or set by hand.", "Slow or wrong DNS feels like 'the internet is down'.", "On VPN they should be the company's servers.");
            G("link.Address from", "DHCP", "Who gave you your address.", "Whether the address came automatically (DHCP) - and from which server - or was typed in by hand.", "DHCP leases an address for a while and renews it quietly.", "Wrong DHCP server = wrong network settings.", "A DHCP server you don't recognise can be a rogue device.");
            G("link.Packet size (MTU)", "MTU", "The biggest packet your adapter sends.", "The largest packet this adapter puts on the wire.", "1500 bytes is standard for Ethernet and Wi-Fi.", "Paths that can't carry it split or drop big packets.", "Check a destination tells you the size the whole path can carry.");
            G("link.Hardware address (MAC)", "MAC address", "Your adapter's serial number on the network.", "The hardware address of your adapter (Windows may randomise it per Wi-Fi network for privacy).", "Used on the local network only - it never leaves your router.", "Network admins use it to recognise devices.", "Changes per network if 'random hardware addresses' is on - that's a privacy feature.");
            G("link.Received|link.Sent", "Traffic", "How much has moved.", "Bytes and packets through this adapter since it last started.", "Counted by the adapter.", "Context for the error counts below.", "-");
            G("link.Damaged packets (errors)", "Errors", "Packets that arrived broken.", "Packets the adapter received or sent with errors.", "Checksums didn't match - the data was damaged on the way.", "Each one has to be sent again.", "Climbing on a cable = cable or port. On Wi-Fi = interference or driver.");
            G("link.Dropped packets (discards)", "Discards", "Packets thrown away.", "Packets the adapter discarded, usually because buffers were full or the packet wasn't wanted.", "Counted by the adapter and driver.", "Some is normal.", "Fast-growing discards during slowness point at the adapter or driver.");
            G("link.LINK TO YOUR ROUTER", "Link to your router", "How healthy the first hop is.", "Latency, jitter and loss to your router, from the pings AdeeboMeter already sends.", "Taken from the last 30 router pings - no extra traffic.", "The router is the first hop for everything; if this is bad, everything is bad.", "Loss or high jitter here means trouble in the room - Wi-Fi or cable.");
            G("link.Router answers in", "Router latency", "How quickly the router replies.", "Best, typical and worst reply time from your router.", "From the router pings.", "Should be a few milliseconds on cable, under ~10 ms on good Wi-Fi.", "Tens of milliseconds = busy Wi-Fi or a busy router.");
            G("link.Jitter", "Jitter", "How uneven the replies are.", "The average change in delay from one ping to the next.", "Computed from the router pings.", "Calls and games hate jitter more than slow but steady delay.", "Above ~20 ms, calls start to sound choppy.");
            G("link.Loss", "Loss", "Pings that never came back.", "The share of router pings that got no reply.", "From the router pings.", "Any loss this close to you is a local problem.", "Even 2-3% loss makes calls and downloads suffer.");
            G("link.Based on", "Sample", "How much data the numbers use.", "How many router pings these numbers are based on.", "The last 30 pings.", "More pings = more trustworthy numbers.", "Router ping is off in Settings? Then there's nothing to measure.");
            G("link.ADDRESSES", "Addresses", "Who you are on the network.", "Your IP addresses, router, DNS servers and how you got them.", "Read from the adapter.", "Most 'can't connect' problems are an address problem.", "169.254 or no gateway = start here.");
            G("link.TRAFFIC SINCE THE ADAPTER STARTED", "Traffic and errors", "The adapter's own counters.", "What has moved through the adapter and how much of it was damaged or dropped.", "Read from the adapter statistics.", "Errors point at cables, docks, radios and drivers.", "Growing errors = hardware or interference.");
            G("link.CHANNEL MAP", "Channel map", "Who else is on the air.", "Every channel in your band that has access points on it. Bar height = the strongest signal heard there; the number = how many access points.", "From Windows' own Wi-Fi scan list - AdeeboMeter doesn't scan by itself.", "Blue is you; amber channels overlap yours and steal your air time.", "A tall amber or crowded blue column = interference. A quieter channel exists if you see empty space.");
            G("link.NEIGHBOURS", "Strongest neighbours", "The loudest access points around you.", "The access points your laptop hears best, with band, channel, generation and signal.", "From Windows' scan list.", "Amber ones share your air.", "Your own network's other access points appear here too - that's where you'd roam.");
            // deeper connection details
            G("theme.toggle", "Day or night", "Switch between the light and dark look.", "Flips AdeeboMeter between day (light) and night (dark).", "Click it, or Tab to it and press Space. Settings > Theme can also follow Windows automatically.", "Dark is easier on the eyes at night; light reads better in bright rooms.", "-");
            G("link.Channel width", "Channel width", "How wide your lane is.", "How much radio spectrum your connection uses: 20, 40, 80 or 160 MHz.", "Read from the access point's own announcements (beacon information elements) through Windows' Wi-Fi API.", "Wider lanes carry more data - double the width, roughly double the top speed.", "20 MHz on 5 GHz is slow; wide channels are also more likely to bump into neighbours.");
            G("link.Roaming help", "Roaming help", "How smoothly you can move between access points.", "Whether the network supports 802.11k (shares a list of nearby access points), 802.11v (suggests a better one) and 802.11r (fast hand-over).", "Read from the access point's announcements.", "With these, walking from room to room barely blips a call.", "None advertised: your laptop decides alone and may cling to a weak access point ('sticky client').");
            G("link.Driver", "Network driver", "The software that runs your network card.", "Driver version and date for this adapter.", "Read from the Windows registry - no admin needed.", "Old drivers are a top cause of random drops and slow Wi-Fi.", "Older than ~3 years: check the laptop maker's support page for an update.");
            G("link.DROPS", "Wi-Fi drops", "Every disconnect in the last day, and why.", "Wi-Fi disconnects and failed connections from Windows' own WLAN log, with the reason Windows recorded.", "Read from the Microsoft-Windows-WLAN-AutoConfig event log - offline, no admin.", "Turns 'my Wi-Fi keeps dropping' into a reason you can act on.", "Many 'driver/AP disconnected' reasons = signal, driver or access-point trouble. 'Authentication' = password or certificate.");
            G("link.DHCP", "DHCP trouble", "Problems getting an address.", "Warnings and errors from Windows' DHCP client in the last day.", "Read from the Microsoft-Windows-Dhcp-Client event log.", "No address renewal = no network, often in short bursts.", "Repeated failures point at the router's DHCP server or the Wi-Fi link to it.");
            G("link.LAN", "Devices on your network", "Who your PC has met on the local network.", "Devices in Windows' ARP table on this network - your router, printers, other PCs - with names if your router knows them.", "Read from Windows' own table; names come from your router's DNS. Nothing is scanned.", "Helps you spot an unknown device, or check the router's address.", "A device you don't recognise on a home network is worth a look. Random addresses are normal for phones.");
            G("link.SWITCH", "Your switch port", "Which switch, port and VLAN your cable lands on.", "The name, port, VLAN and model your switch announces with LLDP or CDP.", "Switches broadcast this every 30-60 seconds on the cable; AdeeboMeter listens with Wireshark's engine for up to 65 s. Nothing is sent.", "Tells IT exactly which port to look at - no more tracing cables.", "Nothing heard: LLDP/CDP is off on the switch, or a dock or Wi-Fi is in the way.");
            G("live.BY APP", "Bytes per app", "Who used your bandwidth during the capture.", "Every conversation in the capture, added up by the app that owns it.", "Each packet's local port is matched to the app holding that port in Windows' connection table.", "Without admin rights this is the only way to see real per-app traffic.", "'other / closed' = connections that ended before we could match them.");
            G("live.CALL QUALITY|live.MOS", "Call quality", "How your calls really sound - measured, not guessed.", "Loss, jitter and an estimated MOS score (1-4.5) for each call media stream (Teams and other RTP calls).", "Sequence numbers show lost packets; timestamps vs. arrival show jitter (RFC 3550); the E-model (ITU-T G.107) turns them, plus your internet delay, into a MOS. Encrypted audio stays encrypted - only headers are read.", "MOS above 4 is great, 3.6-4 is fine, below 3.6 people start to notice.", "High loss = network trouble on the way; high jitter = a busy link or Wi-Fi. Fix the first red thing on the path.");
            G("live.DHCP", "Rogue DHCP", "More than one address giver on your network.", "Two or more DHCP servers answered on this network during the capture.", "Seen in the DHCP packets in your own traffic.", "A second DHCP server can hand out wrong settings - by accident (a home router plugged in backwards) or on purpose.", "Tell IT, or check for a second router on your network.");
            G("hist.CHART", "The day at a glance", "A whole day on one line.", "The verdict as a coloured band, your router's delay (blue) and Wi-Fi signal (green), and dots for warnings.", "From the optional 7-day history - one sample every 10 s.", "Lets you answer 'what was wrong at 2:15 yesterday?'", "Red stretches line up with the events listed below.");
            G("card.DNS SECOND OPINION", "DNS second opinion", "Is your DNS telling the truth?", "Asks a public resolver (1.1.1.1) the same question and compares answers.", "One extra DNS query during a check you start.", "Catches DNS blockers, filters and tampering.", "Different addresses are usually just a nearby CDN server - if the TLS check passes, the answer is genuine.");
            // the path story
            G("card.PATH STORY", "The path story", "Who carries your traffic, step by step.", "Your route grouped by who runs each stretch - your network, your provider, the carriers in between, the destination.", "Router names (reverse DNS) reveal their owner; where the owner changes, your traffic crossed between networks - a BGP peering or transit handoff. Only for checks you start.", "Tells you whose network the delay or the block is in - the first question any support desk asks.", "Unnamed routers just don't publish a name. 'Handoff' lines show where networks meet.");
            G("story.lan", "Your network", "Your side of the fence.", "Routers with private addresses before the internet - your router, maybe your provider's modem.", "Private addresses (10.x, 172.16-31.x, 192.168.x) never travel on the internet; NAT swaps them for a public one.", "Two or more here = double NAT.", "Slow already here = Wi-Fi, cable or your own router.");
            G("story.cgnat", "Carrier-grade NAT", "Your provider's shared address.", "Your provider puts many customers behind one public address (100.64.x.x addresses).", "Common with mobile, satellite and some fibre providers.", "Outgoing traffic works; incoming connections to you can't.", "Hosting a game or a server from home? This is why it doesn't work.");
            G("story.hidden", "Unnamed routers", "Routers without a published name.", "Public routers that didn't publish a reverse-DNS name.", "Owners simply chose not to name them.", "We can't tell whose they are offline.", "Nothing to do - it's not a fault.");
            G("story.net", "A network on the way", "One organisation's stretch of the route.", "Consecutive routers run by the same company, with how many hops and how much delay they add.", "Named from the routers' reverse-DNS names; the city is a guess from codes like 'tor' or 'yyz' in those names.", "Shows whose network adds the delay.", "A big delay inside one carrier is usually distance - or their congestion.");
            // what changed
            G("kind.net", "Network change", "Adapters, addresses and gateways.", "Your network adapter, IP address or gateway changed.", "Windows tells AdeeboMeter the moment addresses change.", "Changes explain sudden drops.", "A change right before trouble is a strong clue.");
            G("kind.wifi", "Wi-Fi change", "Roams, drops and signal swings.", "Wi-Fi connected, dropped, hopped to another access point, changed channel or lost speed.", "Read from Windows (netsh) every 30 seconds and on every network change.", "Roams and signal drops cause short freezes in calls.", "Several roams in a few minutes = you're between two access points.");
            G("kind.vpn", "VPN change", "The tunnel came or went.", "The VPN connected or dropped.", "Watched through the network adapters.", "Everything internal depends on it.", "Drops = check the VPN client.");
            G("kind.dns", "DNS change", "A different phone book.", "Your DNS server changed.", "Watched through the adapters.", "Usually happens with VPN or a new network.", "Unexpected changes can break internal names.");
            G("kind.inet", "Internet reachability", "Out and back.", "The internet stopped or started answering.", "From the small reachability ping.", "Tells you when an outage began and ended.", "Down while the gateway is fine = provider problem.");
            G("kind.gw", "Gateway", "Your router's response time.", "The router got much slower than usual, or stopped answering.", "From the gateway pings.", "The first sign of local trouble.", "Slow gateway = busy or weak Wi-Fi.");
            G("kind.app", "App behaviour", "An app doing something unusual.", "An app opened a burst of connections, or got stuck connecting.", "From the connection table every 10 seconds.", "Finds the app behind a slowdown.", "Stuck connects = that app's server or port is blocked.");
            G("kind.call", "Teams call", "A call started or ended.", "Teams opened or closed its call ports.", "Teams uses UDP ports 50000-50059 only during calls.", "AdeeboMeter watches more closely during calls.", "Problems during a call show up here with their time.");
            G("kind.verdict", "Verdict", "AdeeboMeter's mood changed.", "The overall verdict went up or down a level.", "Judged every 10 seconds from everything above.", "A quick history of when things got worse.", "Look at what changed just before.");
        }

        // ---------- what the file itself says, for anything not in the guide (read once per path, cached)
        static readonly Dictionary<string, string[]> fileInfo = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        public static string[] FileFacts(string path)   // { description, company, product, where }
        {
            if (string.IsNullOrEmpty(path)) return new[] { "", "", "", "" };
            lock (fileInfo)
            {
                string[] f; if (fileInfo.TryGetValue(path, out f)) return f;
                string d = "", c = "", p = "";
                try { var v = FileVersionInfo.GetVersionInfo(path); d = (v.FileDescription ?? "").Trim(); c = (v.CompanyName ?? "").Trim(); p = (v.ProductName ?? "").Trim(); } catch { }
                f = new[] { d, c, p, Where(path) };
                if (fileInfo.Count > 400) fileInfo.Clear();
                fileInfo[path] = f; return f;
            }
        }
        public static string Where(string path)
        {
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows), pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), temp = Path.GetTempPath();
            Func<string, bool> under = delegate(string root) { return root != "" && path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase); };
            if (under(temp)) return "temp";
            if (under(win)) return "windows";
            if (under(pf) || under(pf86)) return "programfiles";
            if (under(local) || under(roam) || under(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))) return "user";
            return "elsewhere";
        }
        public static string WhereText(string where)
        {
            switch (where)
            {
                case "windows": return "It lives in the Windows folder, so it ships with Windows (or a driver).";
                case "programfiles": return "It lives in Program Files - installed for everyone on this PC, usually by IT.";
                case "user": return "It lives in your own profile (just for you, no admin needed). Normal for Teams, OneDrive, Zoom and tools you built yourself - but if you don't recognise it, it's worth asking IT.";
                case "temp": return "It's running from a Temp folder. Installers do this briefly; anything that stays there is worth reporting to IT.";
                case "elsewhere": return "It runs from an unusual folder. If you don't recognise it, ask IT.";
                default: return "Windows didn't let a normal user see where it lives - usually a protected system process.";
            }
        }
    }

    // Shared painter for an explanation: title, tagline, then What / How / Why / Without (and "Can I close it?" for processes).
    static class ExplainPaint
    {
        public static float Paint(Graphics g, Explain.Entry e, float x, float y, float w, bool compact)
        {
            float S = Draw.S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var blue = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            {
                var th = Draw.Measure(e.Friendly, compact ? Draw.fHead : Draw.fTitle, (int)w).Height;
                if (g != null) g.DrawString(e.Friendly, compact ? Draw.fHead : Draw.fTitle, ink, new RectangleF(x, y, w, th + 4)); y += th + 2 * S;
                if (!string.IsNullOrEmpty(e.Tag)) { var tg = Draw.Measure(e.Tag, Draw.fBodyB, (int)w).Height; if (g != null) g.DrawString(e.Tag, Draw.fBodyB, blue, new RectangleF(x, y, w, tg + 4)); y += tg + 8 * S; }
            }
            bool proc = e.Close >= 0 || e.Cat != "term";
            var rows = new List<string[]>();
            if (!string.IsNullOrEmpty(e.What)) rows.Add(new[] { proc ? "WHAT IT DOES" : "WHAT IT IS", e.What });
            if (!string.IsNullOrEmpty(e.How)) rows.Add(new[] { "HOW IT WORKS", e.How });
            if (!string.IsNullOrEmpty(e.Why)) rows.Add(new[] { proc ? "WHY YOU NEED IT" : "WHY IT MATTERS", e.Why });
            if (!string.IsNullOrEmpty(e.Without)) rows.Add(new[] { proc ? (e.Close >= 0 ? "IF IT WEREN'T THERE" : "WHAT TO DO") : "WHEN IT LOOKS BAD", e.Without });
            if (e.Close >= 0) rows.Add(new[] { "CAN I CLOSE IT?", Explain.CloseText[e.Close], e.Close == 0 ? "0" : e.Close == 2 ? "2" : "1" });
            foreach (var r in rows)
            {
                using (var mb = new SolidBrush(Theme.Muted)) if (g != null) g.DrawString(r[0], Draw.fTiny, mb, x, y);
                y += 16 * S;
                var font = compact ? Draw.fSmall : Draw.fBody;
                var h = Draw.Measure(r[1], font, (int)w).Height;
                using (var b = new SolidBrush(r.Length > 2 ? Theme.LvText(int.Parse(r[2], CultureInfo.InvariantCulture)) : Theme.Sub)) if (g != null) g.DrawString(r[1], font, b, new RectangleF(x, y, w, h + 4));
                y += h + (compact ? 6 : 10) * S;
            }
            return y;
        }
        public static float Section(Graphics g, string title, List<string[]> rows, float x, float y, float w)
        {
            float S = Draw.S;
            using (var mb = new SolidBrush(Theme.Muted)) using (var ink = new SolidBrush(Theme.Ink)) using (var line = new Pen(Theme.Line))
            {
                g.DrawLine(line, x, y, x + w, y); y += 10 * S;
                g.DrawString(title, Draw.fTiny, mb, x, y); y += 18 * S;
                foreach (var r in rows)
                {
                    g.DrawString(r[0], Draw.fSmall, mb, x, y);
                    var h = Draw.Measure(r[1], Draw.fSmall, (int)(w - 150 * S)).Height;
                    g.DrawString(r[1], Draw.fSmall, ink, new RectangleF(x + 150 * S, y, w - 150 * S, h + 4));
                    y += Math.Max(18 * S, h) + 6 * S;
                }
            }
            return y;
        }
    }


    // Day / night switch in the header: a sun and a moon on a little track. Click flips it; Settings keeps "Follow Windows".
    sealed class ThemeSwitch : Control
    {
        public ThemeSwitch()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size((int)(64 * Draw.S), (int)(32 * Draw.S)); Cursor = Cursors.Hand; TabStop = true; AccessibleName = "Day or night theme";
        }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Cfg.SetTheme(Theme.Dark ? 0 : 1); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(e); base.OnKeyDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; float S = Draw.S;
            using (var bg = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Card)) g.FillRectangle(bg, ClientRectangle);
            var tr = new RectangleF(1, 2 * S, Width - 2, Height - 4 * S);
            Theme.Fill(g, tr, tr.Height / 2, Theme.Dark ? Theme.C("#2B3445") : Theme.C("#E8EEF7"));
            Theme.Stroke(g, tr, tr.Height / 2, Focused ? Theme.Blue : Theme.Line, Focused ? 2 : 1);
            float k = tr.Height - 6 * S, kx = Theme.Dark ? tr.Right - k - 3 * S : tr.X + 3 * S, ky = tr.Y + 3 * S;
            using (var kb = new SolidBrush(Theme.Dark ? Theme.C("#0F1622") : Color.White)) g.FillEllipse(kb, kx, ky, k, k);
            float cx = kx + k / 2, cy = ky + k / 2;
            if (!Theme.Dark)
            {   // sun
                using (var sun = new SolidBrush(Theme.C("#F5A524"))) g.FillEllipse(sun, cx - k * 0.2f, cy - k * 0.2f, k * 0.4f, k * 0.4f);
                using (var ray = new Pen(Theme.C("#F5A524"), 1.6f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(ray, cx + (float)Math.Cos(a) * k * 0.3f, cy + (float)Math.Sin(a) * k * 0.3f, cx + (float)Math.Cos(a) * k * 0.4f, cy + (float)Math.Sin(a) * k * 0.4f); }
            }
            else
            {   // moon: a disc with a bite taken out
                using (var moon = new SolidBrush(Theme.C("#C9D5FF"))) g.FillEllipse(moon, cx - k * 0.3f, cy - k * 0.3f, k * 0.6f, k * 0.6f);
                using (var bite = new SolidBrush(Theme.C("#0F1622"))) g.FillEllipse(bite, cx - k * 0.12f, cy - k * 0.38f, k * 0.56f, k * 0.56f);
            }
            // the other icon, faint, on the empty side
            float ox = Theme.Dark ? tr.X + 3 * S + k / 2 : tr.Right - 3 * S - k / 2;
            using (var faint = new SolidBrush(Color.FromArgb(110, Theme.Muted)))
                if (Theme.Dark) g.FillEllipse(faint, ox - k * 0.16f, cy - k * 0.16f, k * 0.32f, k * 0.32f);
                else { g.FillEllipse(faint, ox - k * 0.26f, cy - k * 0.26f, k * 0.52f, k * 0.52f); using (var b2 = new SolidBrush(Theme.C("#E8EEF7"))) g.FillEllipse(b2, ox - k * 0.1f, cy - k * 0.36f, k * 0.5f, k * 0.5f); }
        }
    }

    // Hover card for explanations. Never takes focus, never steals clicks, one instance per engineer view.
    sealed class TipWin : Form
    {
        Explain.Entry e;
        public TipWin()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 /*NOACTIVATE*/ | 0x80 /*TOOLWINDOW*/ | 0x20 /*TRANSPARENT*/; return cp; } }
        protected override void OnHandleCreated(EventArgs ev)
        {
            base.OnHandleCreated(ev);
            try { int round = 2; if (Native.DwmSetWindowAttribute(Handle, 33 /*corner preference: round*/, ref round, 4) != 0) Program.Log("rounded tips not supported here"); } catch { }
        }
        public void ShowAt(Explain.Entry en, Point at, Form owner)
        {
            e = en; float S = Draw.S; int w = (int)(400 * S);
            int h = (int)(ExplainPaint.Paint(null, e, 16 * S, 14 * S, w - 32 * S, true) + 10 * S);
            var wa = Screen.FromPoint(at).WorkingArea;
            int x = at.X + (int)(18 * S), y = at.Y + (int)(22 * S);
            if (x + w > wa.Right) x = Math.Max(wa.Left, at.X - w - (int)(12 * S));
            if (y + h > wa.Bottom) y = Math.Max(wa.Top, at.Y - h - (int)(12 * S));
            BackColor = Theme.Card;
            Bounds = new Rectangle(x, y, w, h);
            if (!Visible) { Owner = owner; Show(); }
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (var b = new SolidBrush(Theme.Card)) g.FillRectangle(b, ClientRectangle);
            using (var p = new Pen(Theme.Line)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            using (var a = new SolidBrush(Theme.Blue)) g.FillRectangle(a, 0, 0, 4 * Draw.S, Height);
            if (e != null) ExplainPaint.Paint(g, e, 16 * Draw.S, 14 * Draw.S, Width - 32 * Draw.S, true);
        }
    }

    sealed class FullView : Form
    {
        readonly Monitor M;
        readonly DB header, pathPanel, side, footer, destCanvas, healthCanvas;
        readonly LiveCapture live = new LiveCapture(); LiveCapture.View liveView; readonly DB liveCanvas; readonly PillButton liveBtn; readonly Panel livePage; int liveTick;
        readonly DB changesCanvas; readonly Panel changesPage;
        readonly ConnList list; readonly Panel connPage, destPage, healthPage; readonly RoundBox search, destBox; readonly Label connStatus;
        readonly FlowLayoutPanel tabs, chips; readonly PillButton idle, checkBtn, healthBtn;
        readonly System.Windows.Forms.Timer timer;
        Snap snap; int lastVersion = -1;
        string tab = "conn";
        readonly string dumpcap, wireshark; readonly bool npcap;
        bool refreshing; DateTime lastNames = DateTime.MinValue, lastConns = DateTime.MinValue;
        DestResult dest; volatile bool destRunning;
        List<string[]> health; bool healthRunning;
        double selfCpu; long selfMem; TimeSpan lastCpuT; DateTime lastCpuAt = DateTime.UtcNow;
        // Your connection
        readonly Panel linkPage; readonly DB linkCanvas; LinkSnap linkSnap; bool linkBusy; DateTime lastLink = DateTime.MinValue;
        LiveCapture.SwitchInfo switchInfo; volatile bool switchBusy; string switchErr = ""; DateTime switchStart;
        // What's running
        readonly Panel procPage; readonly DB procList, procInfo; readonly RoundBox procSearch;
        List<AppStat> procs = new List<AppStat>(); string procSel; string[] procFacts; string procPathSel = ""; DateTime lastProcs = DateTime.MinValue;
        readonly Dictionary<string, string> procWhere = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); bool whereBusy;
        // Settings
        readonly Panel settingsPage, aboutPage; readonly DB settingsCanvas, aboutCanvas; readonly PillButton settingsBtn, aboutBtn, backBtn; readonly ThemeSwitch themeSw; Panel tipBar;
        // Saved destinations
        readonly FlowLayoutPanel pinBar; readonly PillButton pinBtn; List<DestResult> pinResults; volatile bool pinRunning;
        // hover explanations + clickable painted areas, rebuilt on every paint
        readonly Dictionary<Control, List<KeyValuePair<RectangleF, string>>> hints = new Dictionary<Control, List<KeyValuePair<RectangleF, string>>>();
        readonly Dictionary<Control, List<KeyValuePair<RectangleF, Action>>> acts = new Dictionary<Control, List<KeyValuePair<RectangleF, Action>>>();
        readonly TipWin tip = new TipWin(); string tipKey; readonly System.Windows.Forms.Timer tipTimer = new System.Windows.Forms.Timer { Interval = 350 };
        DateTime changesSeen = DateTime.Now; readonly HashSet<Control> themedScroll = new HashSet<Control>();
        string sniffStatus = ""; bool sniffing; int footTick;
        static float S { get { return Draw.S; } }
        static int Sc(float v) { return (int)(v * Draw.S); }

        public FullView(Icon icon, Monitor m)
        {
            M = m; snap = m.Latest;
            Text = "AdeeboMeter - " + Program.Tagline; Icon = icon; BackColor = Theme.Bg; Font = Draw.fBody;
            // Start at the size of the screen it opens on, so it fits even if maximizing is refused.
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            StartPosition = FormStartPosition.Manual; Bounds = wa; MinimumSize = new Size(Math.Min(Sc(980), wa.Width), Math.Min(Sc(640), wa.Height));
            WindowState = FormWindowState.Maximized; KeyPreview = true;
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            dumpcap = Path.Combine(pf, "Wireshark", "dumpcap.exe"); wireshark = Path.Combine(pf, "Wireshark", "Wireshark.exe");
            npcap = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Npcap", "wpcap.dll")) && File.Exists(dumpcap);

            header = new DB { Dock = DockStyle.Top, Height = Sc(60), BackColor = Theme.Card };
            header.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintHeader(e.Graphics, header.ClientRectangle); };
            settingsBtn = new PillButton("Settings", PillButton.K.Chip);
            settingsBtn.Click += delegate { tab = tab == "settings" ? "conn" : "settings"; ShowTab(); };
            aboutBtn = new PillButton("About", PillButton.K.Chip);
            aboutBtn.Click += delegate { tab = tab == "about" ? "conn" : "about"; ShowTab(); };
            backBtn = new PillButton("Back to corner", PillButton.K.Primary);
            backBtn.Click += delegate { Close(); };
            themeSw = new ThemeSwitch();
            themeSw.MouseEnter += delegate { SetTipKey("theme.toggle"); }; themeSw.MouseLeave += delegate { SetTipKey(null); };
            header.Controls.Add(themeSw); header.Controls.Add(aboutBtn); header.Controls.Add(settingsBtn); header.Controls.Add(backBtn);
            EventHandler place = delegate
            {
                int xr = header.Width - Sc(20);
                foreach (var b in new Control[] { backBtn, settingsBtn, aboutBtn, themeSw }) { xr -= b.Width; b.Location = new Point(xr, (header.Height - b.Height) / 2); xr -= Sc(b == aboutBtn ? 14 : 8); }
            };
            header.Resize += place; settingsBtn.Resize += place; aboutBtn.Resize += place; backBtn.Resize += place;
            pathPanel = new DB { Dock = DockStyle.Top, Height = Sc(128), BackColor = Theme.Card };
            pathPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                Prep(e.Graphics);
                using (var mb = new SolidBrush(Theme.Muted)) e.Graphics.DrawString("IS IT ME OR THE NETWORK?   ·   CLICK A DOT FOR DETAILS", Draw.fTiny, mb, Sc(20), Sc(12));
                if (!string.IsNullOrEmpty(snap.Hint))
                    using (var hintBrush = new SolidBrush(Theme.LvText(1))) using (var sf = new StringFormat { Alignment = StringAlignment.Far, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                        e.Graphics.DrawString("Why: " + snap.Hint, Draw.fBodyB, hintBrush, new RectangleF(Sc(220), Sc(9), pathPanel.Width - Sc(240), Sc(20)), sf);
                Begin(pathPanel);
                var pr = new RectangleF(Sc(20), Sc(36), pathPanel.Width - Sc(40), Sc(86));
                Draw.PathStrip(e.Graphics, snap, pr, true);
                float slot = pr.Width / 5;
                for (int i = 0; i < 5 && i < snap.PathLabel.Length; i++)
                {
                    var hr = new RectangleF(pr.X + i * slot, pr.Y - 6 * S, slot, pr.Height + 6 * S);
                    Hint(pathPanel, hr, "path." + snap.PathLabel[i]);
                    string go = i == 0 ? "procs" : i == 1 ? "link" : "dest";
                    Act(pathPanel, hr, delegate { tab = go; ShowTab(); });
                }
            };
            Wire(pathPanel);
            footer = new DB { Dock = DockStyle.Bottom, Height = Sc(30), BackColor = Theme.Card };
            footer.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintFooter(e.Graphics, footer.ClientRectangle); };

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(Sc(14)), BackColor = Theme.Bg, Tag = "bg" };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Sc(390)));
            body.Resize += delegate { float sw = Math.Max(Sc(300), Math.Min(Sc(390), body.Width * 0.30f)); if (Math.Abs(body.ColumnStyles[1].Width - sw) > 1) body.ColumnStyles[1].Width = sw; };

            var left = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Margin = new Padding(0, 0, Sc(14), 0) };
            tabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(48), BackColor = Theme.Card, Padding = new Padding(Sc(10), Sc(4), 0, 0), WrapContents = false };
            foreach (var t in new[] { new[] { "conn", "Connections" }, new[] { "link", "Your Wi-Fi" }, new[] { "procs", "What's running" }, new[] { "dest", "Check a destination" }, new[] { "health", "Health check" }, new[] { "changes", "What changed" }, new[] { "live", "Live capture" } })
            {
                var b = new PillButton(t[1], PillButton.K.Tab) { Tag = t[0] };
                b.Click += delegate(object s, EventArgs e) { tab = (string)((Control)s).Tag; ShowTab(); };
                tabs.Controls.Add(b);
            }
            var tabLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Track, Tag = "line" };
            // Small screens / high scaling: switch to short tab names instead of letting tabs fall off the edge.
            tabs.Resize += delegate { FitTabs(); };

            // Connections page
            connPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card };
            chips = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(52), BackColor = Theme.Card, Padding = new Padding(Sc(12), Sc(8), 0, 0), WrapContents = false };
            search = new RoundBox(Sc(300), Draw.fBody, "Filter by app, host, IP or port");
            search.Box.TextChanged += delegate { list.Query = search.Box.Text; list.Rebuild(); };
            chips.Controls.Add(search);
            foreach (var f in new[] { "All", "Teams", "Edge", "Chrome", "Outlook", "OneDrive", "Other" })
            {
                var b = new PillButton(f, PillButton.K.Chip) { Tag = f };
                b.Margin = new Padding(0, Sc(3), Sc(6), 0);
                b.Click += delegate(object s, EventArgs e) { list.Filter = (string)((Control)s).Tag; StyleChips(); list.Rebuild(); };
                chips.Controls.Add(b);
            }
            idle = new PillButton("Bound / listening", PillButton.K.Chip) { Margin = new Padding(Sc(10), Sc(3), 0, 0) };
            idle.Click += delegate { idle.Selected = !idle.Selected; list.ShowIdle = idle.Selected; list.Rebuild(); };
            chips.Controls.Add(idle);
            chips.Resize += delegate
            {
                // shrink the filter box first, then hide app chips from the right (typing in the box still filters by any app).
                // (Visible can't be read back here - it reports false until the window is shown - so hidden chips are tracked in a set.)
                var apps = chips.Controls.Cast<Control>().Where(c => c is PillButton && c != idle && (string)c.Tag != "All").ToList();
                var hide = new HashSet<Control>();
                Func<int> others = () => chips.Controls.Cast<Control>().Where(c => c != search && !hide.Contains(c)).Sum(c => c.Width + c.Margin.Horizontal);
                int room = chips.ClientSize.Width - chips.Padding.Horizontal - search.Margin.Horizontal - Sc(8);
                for (int i = apps.Count - 1; i >= 0 && room - others() < Sc(140); i--) if ((string)apps[i].Tag != list.Filter) hide.Add(apps[i]);
                foreach (var c in apps) c.Visible = !hide.Contains(c);
                int wanted = Math.Max(Sc(140), Math.Min(Sc(300), room - others()));
                if (search.Width != wanted) search.Width = wanted;
            };
            list = new ConnList { Dock = DockStyle.Fill, CanSniff = npcap };
            list.SniffRequested += Sniff; list.Changed += UpdateConnStatus; list.HoverKey += SetTipKey;
            connStatus = new Label { Dock = DockStyle.Bottom, Height = Sc(32), ForeColor = Theme.Muted, Padding = new Padding(Sc(14), Sc(8), 0, 0), BackColor = Theme.Card, Font = Draw.fSmall };
            connPage.Controls.Add(list); connPage.Controls.Add(connStatus); connPage.Controls.Add(chips);
            StyleChips();

            // Destination page
            destPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var destBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            destBox = new RoundBox(Sc(440), Draw.fInput, "host, IP or URL - e.g. github.com:443");
            destBox.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; StartDest(); } };
            checkBtn = new PillButton("Check", PillButton.K.Primary); checkBtn.Click += delegate { StartDest(); };
            pinBtn = new PillButton("Pin", PillButton.K.Chip) { Margin = new Padding(Sc(8), Sc(3), 0, 0) };
            pinBtn.Click += delegate { TogglePin(); };
            destBox.Box.TextChanged += delegate { SyncPinBtn(); };
            destBar.Controls.Add(destBox); destBar.Controls.Add(checkBtn); destBar.Controls.Add(pinBtn);
            pinBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(42), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(4), 0, 0), WrapContents = false };
            destCanvas = new DB { Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            destCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintDest(e.Graphics); };
            Wire(destCanvas);
            destPage.Controls.Add(destCanvas); destPage.Controls.Add(pinBar); destPage.Controls.Add(destBar);
            BuildPinBar();

            // Health page
            healthPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var hb = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            healthBtn = new PillButton("Run health check", PillButton.K.Primary); healthBtn.Click += delegate { StartHealth(); };
            hb.Controls.Add(healthBtn);
            hb.Controls.Add(new Label { Text = "Runs once when you press it - nothing keeps running afterwards.", AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fSmall, Margin = new Padding(Sc(10), Sc(10), 0, 0) });
            healthCanvas = new DB { Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            healthCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintHealth(e.Graphics); }; Wire(healthCanvas);
            healthPage.Controls.Add(healthCanvas); healthPage.Controls.Add(hb);

            // Live capture page
            livePage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var lb = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            liveBtn = new PillButton("Start live capture", PillButton.K.Primary); liveBtn.Click += delegate { ToggleLive(); };
            lb.Controls.Add(liveBtn);
            lb.Controls.Add(new Label { Text = LiveCapture.Available ? "Wireshark's tshark decodes your PC's own traffic. Off until you press Start; stops when you leave this tab, close the view, or after " + Cfg.LiveMin + " min (Settings)." : "Needs Wireshark (tshark) and Npcap - not found on this PC.", AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fSmall, Margin = new Padding(Sc(10), Sc(10), 0, 0) });
            liveBtn.Enabled = LiveCapture.Available;
            liveCanvas = new DB { Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            liveCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintLive(e.Graphics); }; Wire(liveCanvas);
            livePage.Controls.Add(liveCanvas); livePage.Controls.Add(lb);

            // What changed page
            changesPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            changesCanvas = new DB { Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            changesCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintChanges(e.Graphics); }; Wire(changesCanvas);
            changesPage.Controls.Add(changesCanvas);

            // Your connection page
            linkPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            linkCanvas = new DB { Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            linkCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintLink(e.Graphics); }; Wire(linkCanvas);
            linkPage.Controls.Add(linkCanvas);

            // What's running page
            procPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var pbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            procSearch = new RoundBox(Sc(300), Draw.fBody, "Find a process or app");
            procSearch.Box.TextChanged += delegate { procList.AutoScrollPosition = Point.Empty; procList.Invalidate(); };
            pbar.Controls.Add(procSearch);
            pbar.Controls.Add(new Label { Text = Voice.P("Click anything for the plain-English story. I only explain - I never close anything.", "Click a process for its explanation. AdeeboMeter never closes processes."), AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fSmall, Margin = new Padding(Sc(4), Sc(10), 0, 0) });
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Card };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            procList = new DB { Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            procList.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintProcList(e.Graphics); }; Wire(procList);
            procInfo = new DB { Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            procInfo.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintProcInfo(e.Graphics); }; Wire(procInfo);
            split.Controls.Add(procList, 0, 0); split.Controls.Add(procInfo, 1, 0);
            procPage.Controls.Add(split); procPage.Controls.Add(pbar);

            // Settings page
            settingsPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            settingsCanvas = new DB { Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            settingsCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintSettings(e.Graphics); }; Wire(settingsCanvas);
            settingsPage.Controls.Add(settingsCanvas);

            // About page
            aboutPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            aboutCanvas = new DB { Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            aboutCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintAbout(e.Graphics); }; Wire(aboutCanvas);
            aboutPage.Controls.Add(aboutCanvas);

            left.Controls.Add(connPage); left.Controls.Add(linkPage); left.Controls.Add(procPage); left.Controls.Add(settingsPage); left.Controls.Add(aboutPage); left.Controls.Add(destPage); left.Controls.Add(healthPage); left.Controls.Add(changesPage); left.Controls.Add(livePage); left.Controls.Add(tabLine); left.Controls.Add(tabs);
            side = new DB { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0), Tag = "bg" };
            side.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintSide(e.Graphics, side.ClientRectangle); }; Wire(side);
            body.Controls.Add(left, 0, 0); body.Controls.Add(side, 1, 0);
            Controls.Add(body); Controls.Add(footer); Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Line, Tag = "line" }); Controls.Add(pathPanel); Controls.Add(header);
            ShowTab();

            timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += delegate { OnTimer(); };
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            Shown += delegate { timer.Start(); RefreshConns(true); };
            tipTimer.Tick += delegate { tipTimer.Stop(); ShowTip(); };
            // Minimising means "get out of my way": same as Esc / Back to corner - the view closes, frees its memory, the widget keeps watching.
            Resize += delegate { if (WindowState == FormWindowState.Minimized && Visible) BeginInvoke((Action)Close); };
            if (!Cfg.SeenTip) BuildTipBar();
            Deactivate += delegate { SetTipKey(null); };
            FormClosed += delegate
            {
                if (histBmp != null) { histBmp.Dispose(); histBmp = null; }
                // WinForms makes a small copy of the window icon for each window; resetting the icon releases it now, not "someday".
                try { ShowIcon = false; Icon = null; } catch { }
                M.ProcsTab = false; timer.Stop(); timer.Dispose(); tipTimer.Stop(); tipTimer.Dispose();
                try { if (tip.IsHandleCreated) tip.Close(); } catch { } tip.Dispose(); live.Stop(); list.ReleaseAll(); procs = new List<AppStat>(); pinResults = null; };
            Load += delegate { ApplyTheme(); };
        }

        static void Prep(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; }

        void ShowTab()
        {
            connPage.Visible = tab == "conn"; destPage.Visible = tab == "dest"; healthPage.Visible = tab == "health"; livePage.Visible = tab == "live"; changesPage.Visible = tab == "changes";
            procPage.Visible = tab == "procs"; settingsPage.Visible = tab == "settings"; settingsBtn.Selected = tab == "settings"; M.ProcsTab = tab == "procs";
            aboutPage.Visible = tab == "about"; aboutBtn.Selected = tab == "about";
            linkPage.Visible = tab == "link"; if (tab == "link") RefreshLink(true);
            if (tab == "changes") changesSeen = DateTime.Now;
            if (tab == "procs") RefreshProcs();
            SetTipKey(null); ThemeScrollbars(this);
            if (tab != "live" && live.Running) { live.Stop(); liveBtn.Text = "Start live capture"; liveView = null; }
            foreach (Control c in tabs.Controls) ((PillButton)c).Selected = (string)c.Tag == tab;
            if (tab == "dest") destBox.Box.Focus();
        }
        static readonly Dictionary<string, string[]> tabNames = new Dictionary<string, string[]> {
            { "conn", new[] { "Connections", "Connections" } }, { "link", new[] { "Your Wi-Fi", "Wi-Fi" } }, { "procs", new[] { "What's running", "Running" } }, { "dest", new[] { "Check a destination", "Check" } },
            { "health", new[] { "Health check", "Health" } }, { "changes", new[] { "What changed", "Changes" } }, { "live", new[] { "Live capture", "Live" } } };
        bool shortTabs;
        void FitTabs()
        {
            if (tabs.Controls.Count == 0) return;
            int used = tabs.Controls.Cast<Control>().Sum(c => c.Width + c.Margin.Horizontal) + tabs.Padding.Horizontal;
            bool want = shortTabs ? used + Sc(220) > tabs.ClientSize.Width : used > tabs.ClientSize.Width;   // a little slack so it doesn't flip back and forth
            if (want == shortTabs) return;
            shortTabs = want;
            foreach (Control c in tabs.Controls) { string[] n; if (tabNames.TryGetValue((string)c.Tag, out n)) c.Text = n[shortTabs ? 1 : 0]; }
        }
        void StyleChips()
        {
            foreach (Control c in chips.Controls) { var b = c as PillButton; if (b != null && b != idle) b.Selected = (string)b.Tag == list.Filter; }
        }

        void OnTimer()
        {
            if (M.Version != lastVersion) { lastVersion = M.Version; snap = M.Latest; header.Invalidate(); pathPanel.Invalidate(); side.Invalidate(); if (tab == "changes") changesCanvas.Invalidate(); }
            if ((DateTime.UtcNow - lastConns).TotalSeconds >= 2) RefreshConns(false);
            if (++footTick % 2 == 0) { SelfStats(); footer.Invalidate(); }
            if (destRunning || pinRunning) destCanvas.Invalidate();
            if (tab == "procs" && (DateTime.UtcNow - lastProcs).TotalSeconds >= 2) RefreshProcs();
            if (tab == "link") { RefreshLink(false); if (switchBusy) linkCanvas.Invalidate(); }
            if (tab == "changes") changesSeen = DateTime.Now;
            UpdateBadges();
            if (live.Running && ++liveTick % 2 == 0)
            {
                var map = list.PortToApp();
                double net = M.NetPing.Avg(10); double oneWay = double.IsNaN(net) ? 30 : net / 2;
                liveView = live.Snapshot(delegate(int port) { string a; return map.TryGetValue(port, out a) ? a : null; }, oneWay);
                liveCanvas.Invalidate();
            }
            else if (!live.Running && liveBtn.Text != "Start live capture") { liveBtn.Text = "Start live capture"; liveCanvas.Invalidate(); }
        }

        void ToggleLive()
        {
            if (live.Running) { live.Stop(); liveBtn.Text = "Start live capture"; liveCanvas.Invalidate(); return; }
            string iface; lock (M.L) iface = M.IfaceId;
            if (live.Start(iface)) { liveBtn.Text = "Stop live capture"; liveView = null; }
            liveCanvas.Invalidate();
        }

        // Light / dark: recolour every control; the painted parts read the theme on their next paint.
        public void ApplyTheme()
        {
            BackColor = Theme.Bg; Recolor(this); themedScroll.Clear(); ThemeScrollbars(this);
            try { int on = Theme.Dark ? 1 : 0; if (Native.DwmSetWindowAttribute(Handle, 20 /*immersive dark title bar*/, ref on, 4) != 0) Program.Log("dark title bar not supported here"); } catch { }
            Refresh();
        }
        static void Recolor(Control root)
        {
            foreach (Control c in root.Controls)
            {
                string tag = c.Tag as string;
                if (tag == "bg") c.BackColor = Theme.Bg;
                else if (tag == "tip") { c.BackColor = Theme.BlueSoft; c.ForeColor = Theme.Dark ? Theme.Ink : Theme.BlueDark; }
                else if (tag == "line") c.BackColor = Theme.Line;
                else if (c is TextBox) { c.BackColor = Theme.Card; c.ForeColor = Theme.Ink; }
                else if (c is Label) { c.BackColor = Theme.Card; c.ForeColor = Theme.Muted; }
                else if (!(c is ScrollBar)) c.BackColor = Theme.Card;
                Recolor(c); c.Invalidate();
            }
        }

        void RefreshConns(bool force)
        {
            if (refreshing || IsDisposed) return;
            refreshing = true; lastConns = DateTime.UtcNow;
            bool names = force || (DateTime.UtcNow - lastNames).TotalSeconds > 15;
            if (names) lastNames = DateTime.UtcNow;
            Task.Factory.StartNew(() =>
            {
                List<Conn> l = null;
                try { if (names && Cfg.IpNames) M.RefreshIpNames(); l = M.Connections(true); } catch (Exception e) { Program.Log("conns: " + e.Message); }
                try { if (!IsDisposed) BeginInvoke((Action)delegate { refreshing = false; if (l != null && !IsDisposed) list.Merge(l); }); else refreshing = false; } catch { refreshing = false; }
            });
        }

        void UpdateConnStatus()
        {
            int syn = list.SynSent;
            string s = list.Shown + " shown · " + list.Established + " established" + (syn > 0 ? " · " + syn + Voice.P(" stuck knocking (SYN_SENT) - blocked or dead path?", " in SYN_SENT (blocked or dead path?)") : "");
            if (M.dnsCacheLocked && !live.Running) s += Voice.P("    |    Windows keeps its DNS cache admin-only here, so names show while Live capture runs", "    |    Host names: start Live capture (DNS cache is admin-only)");
            if (sniffStatus != "") s += "    |    " + sniffStatus;
            else if (!npcap) s += Voice.P("    |    Sniff needs Wireshark + Npcap - not on this PC.", "    |    Sniff needs Wireshark and Npcap.");
            else s += "    |    Sniff: a row's Sniff button captures it for " + Cfg.SniffSec + " s and opens Wireshark";
            connStatus.Text = s;
        }

        void SelfStats()
        {
            try
            {
                using (var p = Process.GetCurrentProcess())
                {
                    var now = DateTime.UtcNow; var t = p.TotalProcessorTime; double el = (now - lastCpuAt).TotalSeconds;
                    if (el > 0.2 && lastCpuT != TimeSpan.Zero) selfCpu = (t - lastCpuT).TotalSeconds / el / Environment.ProcessorCount * 100;
                    lastCpuT = t; lastCpuAt = now; selfMem = p.PrivateMemorySize64;
                }
            }
            catch { }
        }

        // ---------- painting
        void PaintHeader(Graphics g, Rectangle r)
        {
            float x = Sc(18), cy = r.Height / 2f;
            Theme.Logo(g, new RectangleF(x, cy - 15 * S, 30 * S, 30 * S), true); x += 40 * S;
            using (var b = new SolidBrush(Theme.Ink)) using (var mb = new SolidBrush(Theme.Muted))
            {
                var ts = g.MeasureString("AdeeboMeter", Draw.fBig); var tg = g.MeasureString(Program.Tagline, Draw.fSmall);
                g.DrawString("AdeeboMeter", Draw.fBig, b, x, cy - ts.Height / 2 - 8 * S);
                g.DrawString(Program.Tagline, Draw.fSmall, mb, x + 1 * S, cy + ts.Height / 2 - 6 * S);
                x += Math.Max(ts.Width, tg.Width) + 10 * S;
            }
            string w = Draw.Word(snap.Level);
            Theme.Pill(g, w, Draw.fPill, x, cy - 8 * S, snap.Level, S); x += g.MeasureString(w, Draw.fPill).Width + 22 * S;
            string head = (snap.Level == 0 && !Voice.Serious ? Voice.Greet() + " " : "") + snap.Headline;
            using (var hb = new SolidBrush(snap.Level == 0 ? Theme.Ink : Theme.LvText(snap.Level)))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(head, Draw.fHead, hb, new RectangleF(x, cy - 10 * S, Math.Max(0, themeSw.Left - x - 16 * S), 22 * S), sf);
        }

        void PaintFooter(Graphics g, Rectangle r)
        {
            using (var line = new Pen(Theme.Line)) g.DrawLine(line, 0, 0, r.Width, 0);
            float x = Sc(20), y = 7 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString("AdeeboMeter itself", Draw.fBodyB, ink, x, y - 1 * S); x += g.MeasureString("AdeeboMeter itself", Draw.fBodyB).Width + 14 * S;
                string st = selfCpu.ToString("0.0", CultureInfo.CurrentCulture) + "% CPU    " + (selfMem / 1048576.0).ToString("0", CultureInfo.CurrentCulture) + " MB of 64 MB budget" + (live.Running && liveView != null ? "    + tshark " + liveView.TsharkMB + " MB (live capture on)" : "");
                using (var b = new SolidBrush(selfMem > 64L * 1048576 ? Theme.Red : Theme.Ink)) g.DrawString(st, Draw.fMono, b, x, y);
                string right = Program.Version + " · no cloud · no telemetry · never listens · hover the ? cursor to learn · Esc = back to the corner";
                var rs = g.MeasureString(right, Draw.fSmall); g.DrawString(right, Draw.fSmall, muted, r.Width - rs.Width - 20 * S, y);
            }
        }

        void PaintSide(Graphics g, Rectangle r)
        {
            float x = 0, y = 0, w = r.Width;
            Begin(side);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub))
            {
                float h1 = 16 * S + 22 * S + snap.Health.Count * 24 * S + 8 * S;
                var c1 = new RectangleF(x, y, w - 1, h1); Theme.Fill(g, c1, 12 * S, Theme.Card); Theme.Stroke(g, c1, 12 * S, Theme.Line, 1);
                float yy = y + 14 * S, xx = x + 14 * S, ww = w - 28 * S;
                g.DrawString("TCP / IP HEALTH", Draw.fTiny, muted, xx, yy);
                var ns = g.MeasureString("Windows counters · no admin", Draw.fSmall); g.DrawString("Windows counters · no admin", Draw.fSmall, muted, xx + ww - ns.Width, yy - 2 * S);
                yy += 22 * S;
                for (int i = 0; i < snap.Health.Count && i < snap.HealthHist.Count; i++)
                {
                    var hrow = snap.Health[i]; int lv = int.Parse(hrow[2], CultureInfo.InvariantCulture);
                    Hint(side, new RectangleF(xx, yy - 2 * S, ww, 22 * S), hrow[0]);
                    using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                        g.DrawString(hrow[0], Draw.fBody, sub, new RectangleF(xx, yy, ww - 170 * S, 20 * S), sf);
                    Theme.Spark(g, snap.HealthHist[i], new RectangleF(xx + ww - 160 * S, yy + 2 * S, 84 * S, 16 * S), Theme.Lv(lv), 0, false);
                    var vs = g.MeasureString(hrow[1], Draw.fMono);
                    using (var b = new SolidBrush(lv == 1 || lv == 2 ? Theme.Lv(lv) : Theme.Ink)) g.DrawString(hrow[1], Draw.fMono, b, xx + ww - vs.Width, yy);
                    yy += 24 * S;
                }
                y += h1 + 14 * S;
                float h2 = 16 * S + 20 * S + 4 * 20 * S + 8 * S;
                var c2 = new RectangleF(x, y, w - 1, h2); Theme.Fill(g, c2, 12 * S, Theme.Card); Theme.Stroke(g, c2, 12 * S, Theme.Line, 1);
                yy = y + 14 * S; g.DrawString("THIS PC", Draw.fTiny, muted, xx, yy); yy += 20 * S;
                float yb = yy; Draw.Bar(g, "", "CPU", snap.Cpu, Draw.Fmt(snap.Cpu, "0", "%"), 60, 88, xx, ref yy, ww, null); Hint(side, new RectangleF(xx, yb, ww, yy - yb), "CPU");
                yb = yy; Draw.Bar(g, "", "Memory", snap.Mem, snap.MemText, 80, 90, xx, ref yy, ww, null); Hint(side, new RectangleF(xx, yb, ww, yy - yb), "Memory");
                yb = yy; Draw.Bar(g, "", "Disk", snap.Disk, snap.DiskText, 60, 90, xx, ref yy, ww, null); Hint(side, new RectangleF(xx, yb, ww, yy - yb), "Disk");
                yb = yy; double dn = snap.Down; Draw.Bar(g, "", "Download", double.IsNaN(dn) ? double.NaN : Math.Min(100, dn), Draw.Fmt(dn, "0.0", " Mb/s"), 200, 200, xx, ref yy, ww, null); Hint(side, new RectangleF(xx, yb, ww, yy - yb), "Download");
                y += h2 + 14 * S;
                int fit = (int)((r.Height - y - 16 * S - 18 * S - 8 * S) / (20 * S));   // fewer rows on short / high-DPI screens instead of clipping
                int n = Math.Max(0, Math.Min(Math.Min(6, fit), Math.Max(snap.TopMem.Count, snap.TopCpu.Count)));
                if (n == 0) return;
                float h3 = 16 * S + 18 * S + n * 20 * S + 8 * S;
                var c3 = new RectangleF(x, y, w - 1, h3); Theme.Fill(g, c3, 12 * S, Theme.Card); Theme.Stroke(g, c3, 12 * S, Theme.Line, 1);
                yy = y + 14 * S; float colW = (ww - 16 * S) / 2;
                g.DrawString("TOP BY MEMORY", Draw.fTiny, muted, xx, yy); g.DrawString("TOP BY CPU", Draw.fTiny, muted, xx + colW + 16 * S, yy);
                Hint(side, new RectangleF(xx, yy, colW, 16 * S), "Top by memory"); Hint(side, new RectangleF(xx + colW + 16 * S, yy, colW, 16 * S), "Top by CPU"); yy += 18 * S;
                for (int i = 0; i < n; i++)
                {
                    if (i < snap.TopMem.Count) Hint(side, new RectangleF(xx, yy, colW, 20 * S), "proc:" + snap.TopMem[i].Name);
                    if (i < snap.TopCpu.Count) Hint(side, new RectangleF(xx + colW + 16 * S, yy, colW, 20 * S), "proc:" + snap.TopCpu[i].Name);
                    if (i < snap.TopMem.Count) { var a = snap.TopMem[i]; g.DrawString(Draw.Trunc(a.Name, 15), Draw.fBody, ink, xx, yy); var v = Draw.MemStr(a.Mem); var vs = g.MeasureString(v, Draw.fMono); g.DrawString(v, Draw.fMono, ink, xx + colW - vs.Width, yy); }
                    if (i < snap.TopCpu.Count) { var a = snap.TopCpu[i]; g.DrawString(Draw.Trunc(a.Name, 15), Draw.fBody, ink, xx + colW + 16 * S, yy); var v = a.Cpu.ToString("0.0", CultureInfo.CurrentCulture) + "%"; var vs = g.MeasureString(v, Draw.fMono); g.DrawString(v, Draw.fMono, ink, xx + ww - vs.Width, yy); }
                    yy += 20 * S;
                }
            }
        }

        void PaintDest(Graphics g)
        {
            g.TranslateTransform(destCanvas.AutoScrollPosition.X, destCanvas.AutoScrollPosition.Y);
            float x = 14 * S, y = 6 * S, w = destCanvas.ClientSize.Width - 28 * S;
            Begin(destCanvas);
            var pr = pinResults;
            if (pr != null) y = PaintPinTable(g, pr, x, y, w);
            var d = dest;
            if (d == null && pr != null)
            {
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Voice.P("Click a row for the full story.", "Click a row for details."), Draw.fBody, mb, x, y);
                var need0 = new Size(0, (int)(y + 40 * S)); if (destCanvas.AutoScrollMinSize != need0) destCanvas.AutoScrollMinSize = need0;
                return;
            }
            bool done = d != null && d.Done;
            int lv = d == null ? 3 : done ? d.Level : -2;
            string title = d == null ? "READY" : done ? "RESULT · " + d.Target : "CHECKING " + d.Target;
            string verdict = d == null ? Voice.P("Give me a host, an IP or a URL and press Check. Nothing runs until you do.", "Type a host, IP or URL and press Check.")
                                           + (Cfg.Pins.Count == 0 ? Voice.P("\nTip: press Pin to save places you check often - the VPN head-end, Jira, the lab jump host - then run them all at once.", "\nTip: Pin saves a destination; Run all checks every saved one.") : "")
                           : done ? d.Verdict : (d.Note ?? Voice.P("Working through DNS, route, ping, TCP, TLS, HTTP, MTU and path...", "Checking..."));
            float vh = Draw.Measure(verdict, Draw.fTitle, (int)(w - 32 * S)).Height;
            var vb = new RectangleF(x, y, w, vh + 38 * S);
            Color bg = lv == -2 ? Theme.BlueSoft : lv == 3 ? Theme.Soft : Theme.LvSoft(lv), fg = lv == -2 ? Theme.BlueDark : lv == 3 ? Theme.Sub : Theme.LvText(lv);
            Theme.Fill(g, vb, 10 * S, bg);
            using (var b = new SolidBrush(fg)) { g.DrawString(title, Draw.fTiny, b, x + 16 * S, y + 10 * S); g.DrawString(verdict, Draw.fTitle, b, new RectangleF(x + 16 * S, y + 26 * S, w - 32 * S, vh + 4)); }
            y += vb.Height + 20 * S;
            int n = DestResult.Steps.Length; float slot = w / n, dot = 18 * S;
            using (var line = new Pen(Theme.Rail, 2 * S)) for (int i = 0; i < n - 1; i++) g.DrawLine(line, x + slot * i + slot / 2 + dot / 2 + 2 * S, y + dot / 2, x + slot * (i + 1) + slot / 2 - dot / 2 - 2 * S, y + dot / 2);
            using (var center = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                for (int i = 0; i < n; i++)
                {
                    float cx = x + slot * i + slot / 2;
                    int sl = d == null ? -1 : d.Lv[i]; bool cur = d != null && !done && d.Running == i;
                    if (cur) { using (var b = new SolidBrush(Theme.Card)) g.FillEllipse(b, cx - dot / 2, y, dot, dot); using (var p = new Pen(Theme.Blue, 3 * S)) g.DrawEllipse(p, cx - dot / 2, y, dot, dot); }
                    else using (var b = new SolidBrush(sl < 0 ? Theme.Track : Theme.Lv(sl))) g.FillEllipse(b, cx - dot / 2, y, dot, dot);
                    using (var b = new SolidBrush(Theme.Ink)) g.DrawString(DestResult.Steps[i], Draw.fName, b, new RectangleF(cx - slot / 2, y + dot + 3 * S, slot, 18 * S), center);
                    Hint(destCanvas, new RectangleF(cx - slot / 2, y - 4 * S, slot, dot + 44 * S), "step." + DestResult.Steps[i]);
                    string v = d == null ? "-" : cur ? "checking..." : d.Val[i] ?? "-";
                    using (var b = new SolidBrush(Theme.Sub)) g.DrawString(v, Draw.fMonoS, b, new RectangleF(cx - slot / 2 + 2 * S, y + dot + 21 * S, slot - 4 * S, 16 * S), center);
                }
            y += dot + 48 * S;
            if (d != null && (d.Story != null || (d.Findings != null && d.Findings.Count > 0))) y = PaintStory(g, d, x, y, w);
            var cards = d != null ? d.CardsCopy() : new List<KeyValuePair<string, List<string[]>>>();
            if (cards.Count > 0)
            {
                int per = Math.Max(1, (int)(w / (300 * S))); float cw = (w - (per - 1) * 12 * S) / per;
                for (int i = 0; i < cards.Count; i += per)
                {
                    float rowH = 0;
                    for (int j = i; j < Math.Min(cards.Count, i + per); j++) rowH = Math.Max(rowH, 38 * S + cards[j].Value.Count * 19 * S);
                    for (int j = i; j < Math.Min(cards.Count, i + per); j++)
                    {
                        var cr = new RectangleF(x + (j - i) * (cw + 12 * S), y, cw, rowH);
                        Theme.Stroke(g, cr, 10 * S, Theme.Track, 1);
                        using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(cards[j].Key, Draw.fTiny, mb, cr.X + 14 * S, cr.Y + 12 * S);
                        Hint(destCanvas, new RectangleF(cr.X, cr.Y, cr.Width, 26 * S), CardKey(cards[j].Key));
                        float ry = cr.Y + 30 * S;
                        foreach (var row in cards[j].Value)
                        {
                            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Draw.Trunc(row[0], 24), Draw.fBody, mb, cr.X + 14 * S, ry);
                            string v = Draw.Trunc(row.Length > 1 ? row[1] : "", 34); var vs = g.MeasureString(v, Draw.fMonoS);
                            using (var vb2 = new SolidBrush(row.Length > 2 ? Theme.LvText(int.Parse(row[2], CultureInfo.InvariantCulture)) : Theme.Ink)) g.DrawString(v, Draw.fMonoS, vb2, cr.Right - 14 * S - vs.Width, ry + 1 * S);
                            ry += 19 * S;
                        }
                    }
                    y += rowH + 12 * S;
                }
            }
            var need = new Size(0, (int)(y + 20 * S));
            if (destCanvas.AutoScrollMinSize != need) destCanvas.AutoScrollMinSize = need;
        }

        void PaintHealth(Graphics g)
        {
            g.TranslateTransform(healthCanvas.AutoScrollPosition.X, healthCanvas.AutoScrollPosition.Y);
            float x = 14 * S, y = 4 * S, w = healthCanvas.ClientSize.Width - 28 * S;
            Begin(healthCanvas);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (health == null)
                {
                    g.DrawString(healthRunning ? Voice.P("Poking around...", "Running...") : Voice.P("Press the button and I'll give your PC a quick once-over.", "Press Run health check."), Draw.fBody, muted, x, y + 6 * S);
                    return;
                }
                float nameW = 250 * S;
                foreach (var row in health)
                {
                    int lv = int.Parse(row[0], CultureInfo.InvariantCulture);
                    float ah = Draw.Measure(row[3], Draw.fBody, (int)(w - nameW - 30 * S)).Height;
                    float rh = Math.Max(46 * S, ah + 18 * S);
                    using (var b = new SolidBrush(Theme.Lv(lv))) g.FillEllipse(b, x, y + 12 * S, 13 * S, 13 * S);
                    g.DrawString(row[1], Draw.fBodyB, ink, x + 26 * S, y + 6 * S);
                    Hint(healthCanvas, new RectangleF(x, y, nameW, rh), "hc." + (row[1].StartsWith("Disk", StringComparison.Ordinal) ? "Disk" : row[1]));
                    g.DrawString(row[2], Draw.fMonoS, sub, new RectangleF(x + 26 * S, y + 25 * S, nameW - 30 * S, 16 * S), nowrap);
                    g.DrawString(row[3], Draw.fBody, sub, new RectangleF(x + nameW + 10 * S, y + 8 * S, w - nameW - 20 * S, ah + 4));
                    y += rh; g.DrawLine(line, x, y, x + w, y); y += 4 * S;
                }
                var need = new Size(0, (int)(y + 20 * S));
                if (healthCanvas.AutoScrollMinSize != need) healthCanvas.AutoScrollMinSize = need;
            }
        }

        void PaintChanges(Graphics g)
        {
            g.TranslateTransform(changesCanvas.AutoScrollPosition.X, changesCanvas.AutoScrollPosition.Y);
            float x = 14 * S, y = 12 * S, w = changesCanvas.ClientSize.Width - 28 * S;
            Begin(changesCanvas);
            y = PaintDayPicker(g, x, y, w);
            var evs = histDay == null ? M.Events.Recent(300) : (histData != null ? histData.E : new List<Timeline.Ev>());
            if (histDay != null && histData != null)
            {
                float ch = 196 * S; string key = histDay.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "|" + (int)w + "|" + Theme.Dark;
                if (histBmp == null || histKey != key)
                {
                    if (histBmp != null) histBmp.Dispose();
                    histBmp = new Bitmap(Math.Max(1, (int)w), Math.Max(1, (int)ch)); histKey = key;
                    using (var bg = Graphics.FromImage(histBmp)) { Prep(bg); bg.Clear(Theme.Card); PaintDayChart(bg, histData, 0, 0, w); }
                }
                g.DrawImageUnscaled(histBmp, (int)x, (int)y);
                Hint(changesCanvas, new RectangleF(x, y, w, ch), "hist.CHART");
                y += ch + 14 * S;
            }
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var line = new Pen(Theme.Line2))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(histDay == null ? "LAST 2 HOURS · NEWEST FIRST · ONLY CHANGES ARE RECORDED" + (Cfg.History ? " · ALSO SAVED TO HISTORY" : ", IN MEMORY ONLY") : "EVERYTHING THAT CHANGED ON " + histDay.Value.ToString("dddd d MMMM", CultureInfo.CurrentCulture).ToUpperInvariant(), Draw.fTiny, muted, x, y); y += 22 * S;
                if (evs.Count <= (histDay == null ? 1 : 0)) g.DrawString(Voice.P("Nothing has changed yet. Boring is good.", "No changes recorded yet."), Draw.fBody, muted, x, y + 4 * S);
                foreach (var e in evs)
                {
                    g.DrawString(e.T.ToString("HH:mm:ss", CultureInfo.CurrentCulture), Draw.fMonoS, muted, x, y + 4 * S);
                    using (var b = new SolidBrush(Theme.Lv(e.Lv))) g.FillEllipse(b, x + 76 * S, y + 7 * S, 10 * S, 10 * S);
                    using (var tb = new SolidBrush(e.Lv == 2 ? Theme.LvText(2) : Theme.Ink)) g.DrawString(e.Text, e.Lv == 2 ? Draw.fBodyB : Draw.fBody, tb, new RectangleF(x + 96 * S, y + 2 * S, w - 200 * S, 20 * S), nowrap);
                    var ks = g.MeasureString(e.Kind, Draw.fSmall); g.DrawString(e.Kind, Draw.fSmall, muted, x + w - ks.Width, y + 4 * S);
                    Hint(changesCanvas, new RectangleF(x + 76 * S, y, w - 76 * S, 26 * S), "kind." + e.Kind);
                    y += 26 * S; g.DrawLine(line, x, y, x + w, y);
                }
            }
            var need = new Size(0, (int)(y + 20 * S));
            if (changesCanvas.AutoScrollMinSize != need) changesCanvas.AutoScrollMinSize = need;
        }

        DateTime? histDay; History.Day histData; Bitmap histBmp; string histKey = ""; List<DateTime> histDays; DateTime histDaysAt = DateTime.MinValue;
        float PaintDayPicker(Graphics g, float x, float y, float w)
        {
            if (histDays == null || (DateTime.UtcNow - histDaysAt).TotalSeconds > 30) { histDays = History.Days(); histDaysAt = DateTime.UtcNow; }
            var days = histDays;
            if (days.Count == 0)
            {
                string t = Cfg.History ? "History is on - the first day appears here after a few minutes." : "Want to look back at yesterday? Turn on 7-day history in Settings  >";
                using (var b = new SolidBrush(Cfg.History ? Theme.Muted : (Theme.Dark ? Theme.BlueDark : Theme.Blue))) g.DrawString(t, Draw.fBody, b, x, y);
                if (!Cfg.History) Act(changesCanvas, new RectangleF(x, y, g.MeasureString(t, Draw.fBody).Width, 20 * S), delegate { tab = "settings"; ShowTab(); });
                return y + 32 * S;
            }
            float cx = x;
            var opts = new List<KeyValuePair<string, DateTime?>> { new KeyValuePair<string, DateTime?>("Now (2 h)", null) };
            foreach (var d in days.Take(7)) opts.Add(new KeyValuePair<string, DateTime?>(d.Date == DateTime.Today ? "Today" : d.Date == DateTime.Today.AddDays(-1) ? "Yesterday" : d.ToString("ddd d MMM", CultureInfo.CurrentCulture), d));
            foreach (var o in opts)
            {
                float tw = g.MeasureString(o.Key, Draw.fBody).Width + 26 * S; var r = new RectangleF(cx, y, tw, 30 * S);
                bool on = histDay == o.Value;
                Theme.Fill(g, r, 15 * S, on ? Theme.Blue : Theme.Soft); if (!on) Theme.Stroke(g, r, 15 * S, Theme.Line, 1);
                using (var b = new SolidBrush(on ? Color.White : Theme.Sub)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(o.Key, Draw.fBody, b, r, c);
                var pick = o.Value; Act(changesCanvas, r, delegate { histDay = pick; histData = pick == null ? null : History.Load(pick.Value); changesCanvas.AutoScrollPosition = Point.Empty; changesCanvas.Invalidate(); });
                cx += tw + 8 * S; if (cx > x + w - 120 * S) break;
            }
            return y + 44 * S;
        }
        // One day at a glance: the verdict as a coloured band, router latency and Wi-Fi signal as lines, events as ticks.
        static float PaintDayChart(Graphics g, History.Day d, float x, float y, float w)
        {
            float h = 150 * S; var card = new RectangleF(x, y, w, h + 46 * S);
            Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString("THE DAY AT A GLANCE  ·  BAND = VERDICT, BLUE = ROUTER DELAY (MS), GREEN = WI-FI SIGNAL (%)", Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            float px = x + 40 * S, pw = w - 60 * S, top = y + 34 * S, bandY = top + h - 18 * S, ph = h - 30 * S;
            Func<double, float> X = sec => px + (float)(sec / 86400.0) * pw;
            using (var axis = new Pen(Theme.Line)) using (var mb = new SolidBrush(Theme.Muted))
                for (int hr = 0; hr <= 24; hr += 3) { float xx = X(hr * 3600); g.DrawLine(axis, xx, top, xx, bandY); g.DrawString(hr.ToString("00", CultureInfo.InvariantCulture), Draw.fSmall, mb, xx - 7 * S, bandY + 10 * S); }
            double gmax = Math.Max(20, d.M.Where(m => !double.IsNaN(m[5])).Select(m => m[5]).DefaultIfEmpty(20).Max());
            foreach (var m in d.M) using (var b = new SolidBrush(Theme.Lv(double.IsNaN(m[1]) ? 0 : (int)m[1]))) g.FillRectangle(b, X(m[0]), bandY, Math.Max(1.5f, pw / 8640f), 8 * S);
            Action<int, double, Color> lineOf = delegate(int col, double max, Color c)
            {
                using (var pen = new Pen(c, 1.5f * S))
                {
                    PointF? prev = null; double prevT = -1;
                    foreach (var m in d.M)
                    {
                        if (double.IsNaN(m[col]) || m[col] < 0) { prev = null; continue; }
                        var pt = new PointF(X(m[0]), top + ph - (float)(Math.Min(m[col], max) / max) * ph);
                        if (prev != null && m[0] - prevT < 120) g.DrawLine(pen, prev.Value, pt);
                        prev = pt; prevT = m[0];
                    }
                }
            };
            lineOf(7, 100, Theme.Green); lineOf(5, gmax, Theme.Blue);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(gmax.ToString("0", CultureInfo.CurrentCulture) + " ms", Draw.fSmall, mb, x + 6 * S, top - 2 * S);
            foreach (var e in d.E.Where(e => e.Lv >= 1)) using (var b = new SolidBrush(Theme.Lv(e.Lv))) g.FillEllipse(b, X(e.T.TimeOfDay.TotalSeconds) - 3 * S, top - 4 * S, 6 * S, 6 * S);
            return y + card.Height + 14 * S;
        }

        void PaintLive(Graphics g)
        {
            g.TranslateTransform(liveCanvas.AutoScrollPosition.X, liveCanvas.AutoScrollPosition.Y);
            float x = 14 * S, y = 4 * S, w = liveCanvas.ClientSize.Width - 28 * S;
            Begin(liveCanvas);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (live.Error != "") { using (var eb = new SolidBrush(Theme.LvText(1))) g.DrawString(live.Error, Draw.fBody, eb, new RectangleF(x, y, w, 40 * S)); y += 30 * S; }
                var v = liveView;
                if (!live.Running || v == null)
                {
                    g.DrawString(live.Running ? Voice.P("Warming up tshark...", "Starting...") : Voice.P("Press Start and I'll put Wireshark's brain on your traffic. Nothing runs until you do.", "Press Start live capture."), Draw.fBody, muted, x, y + 6 * S);
                    return;
                }
                g.DrawString(v.State + "   ·   " + v.Pkts.ToString("N0", CultureInfo.CurrentCulture) + " packets   ·   " + Draw.MemStr(v.Bytes) + "   ·   " + v.Pps.ToString("0", CultureInfo.CurrentCulture) + " pkt/s", Draw.fMonoS, sub, x, y); y += 24 * S;
                if (v.DhcpServers.Count > 1)
                {
                    string warn = Voice.P("Two DHCP servers are handing out addresses here (" + string.Join(", ", v.DhcpServers) + "). Only one should - the other may be a rogue device or a misconfigured router.", "Multiple DHCP servers seen: " + string.Join(", ", v.DhcpServers) + ".");
                    var wh = Draw.Measure(warn, Draw.fBodyB, (int)(w - 28 * S)).Height; var wr = new RectangleF(x, y, w, wh + 20 * S);
                    Theme.Fill(g, wr, 10 * S, Theme.LvSoft(2)); using (var wb = new SolidBrush(Theme.LvText(2))) g.DrawString(warn, Draw.fBodyB, wb, new RectangleF(x + 14 * S, y + 10 * S, w - 28 * S, wh + 4));
                    Hint(liveCanvas, wr, "live.DHCP"); y += wr.Height + 12 * S;
                }
                float colW = (w - 24 * S) / 2, cardH = 0;
                // protocol hierarchy
                var c1 = new RectangleF(x, y, colW, 34 * S + Math.Max(1, v.Phs.Count) * 20 * S);
                Theme.Stroke(g, c1, 10 * S, Theme.Track, 1);
                g.DrawString("PROTOCOL HIERARCHY (BY PACKETS)", Draw.fTiny, muted, c1.X + 14 * S, c1.Y + 12 * S); Hint(liveCanvas, new RectangleF(c1.X, c1.Y, c1.Width, 26 * S), "live.PROTOCOL HIERARCHY");
                float yy = c1.Y + 30 * S;
                foreach (var r in v.Phs)
                {
                    int depth = int.Parse(r[0], CultureInfo.InvariantCulture); float pct = float.Parse(r[2].TrimEnd('%'), System.Globalization.CultureInfo.CurrentCulture);
                    g.DrawString(r[1], Draw.fMonoS, depth == 0 ? muted : ink, c1.X + 14 * S + depth * 12 * S, yy);
                    var tr = new RectangleF(c1.Right - 190 * S, yy + 5 * S, 110 * S, 7 * S); Theme.Fill(g, tr, 3.5f * S, Theme.Track);
                    Theme.Fill(g, new RectangleF(tr.X, tr.Y, Math.Max(4 * S, tr.Width * pct / 100f), tr.Height), 3.5f * S, depth <= 1 ? Theme.Blue : depth == 2 ? Theme.C("#8250DF") : Theme.Orange);
                    var ps = g.MeasureString(r[2], Draw.fMonoS); g.DrawString(r[2], Draw.fMonoS, ink, c1.Right - 14 * S - ps.Width, yy);
                    yy += 20 * S;
                }
                cardH = c1.Height;
                // expert info
                var c3 = new RectangleF(x + colW + 24 * S, y, colW, 34 * S + v.Expert.Count * 20 * S);
                Theme.Stroke(g, c3, 10 * S, Theme.Track, 1);
                g.DrawString("EXPERT INFO (WIRESHARK)", Draw.fTiny, muted, c3.X + 14 * S, c3.Y + 12 * S); Hint(liveCanvas, new RectangleF(c3.X, c3.Y, c3.Width, 26 * S), "live.EXPERT INFO");
                yy = c3.Y + 30 * S;
                foreach (var e in v.Expert)
                {
                    using (var b = new SolidBrush(Theme.Lv(int.Parse(e[0], CultureInfo.InvariantCulture)))) g.FillEllipse(b, c3.X + 14 * S, yy + 4 * S, 9 * S, 9 * S);
                    g.DrawString(e[1], Draw.fBody, ink, c3.X + 30 * S, yy); Hint(liveCanvas, new RectangleF(c3.X, yy, c3.Width, 20 * S), e[1]);
                    var ns = g.MeasureString(e[2], Draw.fMono); g.DrawString(e[2], Draw.fMono, ink, c3.Right - 14 * S - ns.Width, yy);
                    yy += 20 * S;
                }
                cardH = Math.Max(cardH, c3.Height);
                y += cardH + 14 * S;
                // bytes per app + call quality
                var c4 = new RectangleF(x, y, colW, 34 * S + Math.Max(1, v.ByApp.Count) * 20 * S);
                Theme.Stroke(g, c4, 10 * S, Theme.Track, 1);
                g.DrawString("BYTES PER APP (SINCE START)", Draw.fTiny, muted, c4.X + 14 * S, c4.Y + 12 * S); Hint(liveCanvas, new RectangleF(c4.X, c4.Y, c4.Width, 26 * S), "live.BY APP");
                yy = c4.Y + 30 * S;
                if (v.ByApp.Count == 0) g.DrawString("-", Draw.fBody, muted, c4.X + 14 * S, yy);
                foreach (var a in v.ByApp)
                {
                    g.DrawString(a[0], Draw.fBody, ink, new RectangleF(c4.X + 14 * S, yy, colW * 0.4f, 18 * S), nowrap);
                    float pct = float.Parse(a[3], CultureInfo.CurrentCulture);
                    var tr = new RectangleF(c4.X + colW * 0.42f, yy + 6 * S, colW * 0.25f, 7 * S); Theme.Fill(g, tr, 3.5f * S, Theme.Track);
                    Theme.Fill(g, new RectangleF(tr.X, tr.Y, Math.Max(4 * S, tr.Width * pct / 100f), tr.Height), 3.5f * S, Theme.Blue);
                    string t = a[1] + "  " + a[2] + " Mb/s"; var ts2 = g.MeasureString(t, Draw.fMonoS); g.DrawString(t, Draw.fMonoS, ink, c4.Right - 14 * S - ts2.Width, yy + 1 * S);
                    yy += 20 * S;
                }
                var c5 = new RectangleF(x + colW + 24 * S, y, colW, 34 * S + Math.Max(2, v.Calls.Count) * 20 * S);
                Theme.Stroke(g, c5, 10 * S, Theme.Track, 1);
                g.DrawString("CALL QUALITY (TEAMS AND OTHER CALLS)", Draw.fTiny, muted, c5.X + 14 * S, c5.Y + 12 * S); Hint(liveCanvas, new RectangleF(c5.X, c5.Y, c5.Width, 26 * S), "live.CALL QUALITY");
                yy = c5.Y + 30 * S;
                if (!v.Extras) g.DrawString("Needs a newer Wireshark (3.0 or later).", Draw.fBody, muted, new RectangleF(c5.X + 14 * S, yy, colW - 28 * S, 40 * S));
                else if (v.Calls.Count == 0) g.DrawString(Voice.P("No call running. Start a Teams call and I'll grade it live.", "No call media seen yet."), Draw.fBody, muted, new RectangleF(c5.X + 14 * S, yy, colW - 28 * S, 40 * S));
                foreach (var cl in v.Calls)
                {
                    int lv = int.Parse(cl[6], CultureInfo.InvariantCulture);
                    using (var b = new SolidBrush(Theme.Lv(lv))) g.FillEllipse(b, c5.X + 14 * S, yy + 4 * S, 9 * S, 9 * S);
                    g.DrawString(cl[1] + " " + cl[0], Draw.fBody, ink, new RectangleF(c5.X + 30 * S, yy, colW * 0.42f, 18 * S), nowrap);
                    string t = "loss " + cl[3] + " · jitter " + cl[4] + " · MOS " + cl[5]; var ts3 = g.MeasureString(t, Draw.fMonoS);
                    using (var tb = new SolidBrush(Theme.LvText(lv))) g.DrawString(t, Draw.fMonoS, tb, c5.Right - 14 * S - ts3.Width, yy + 1 * S);
                    Hint(liveCanvas, new RectangleF(c5.X, yy, c5.Width, 20 * S), "live.MOS");
                    yy += 20 * S;
                }
                y += Math.Max(c4.Height, c5.Height) + 14 * S;
                // conversations
                var c2 = new RectangleF(x, y, w, 34 * S + Math.Max(1, v.Top.Count) * 36 * S);
                Theme.Stroke(g, c2, 10 * S, Theme.Track, 1);
                g.DrawString("TOP CONVERSATIONS · NAMED FROM TLS SERVER NAME / DNS IN THE TRAFFIC", Draw.fTiny, muted, c2.X + 14 * S, c2.Y + 12 * S); Hint(liveCanvas, new RectangleF(c2.X, c2.Y, c2.Width, 26 * S), "live.TOP CONVERSATIONS");
                yy = c2.Y + 30 * S;
                if (v.Top.Count == 0) g.DrawString(Voice.P("Quiet out there.", "No traffic yet."), Draw.fBody, muted, c2.X + 14 * S, yy);
                foreach (var c in v.Top)
                {
                    g.DrawString(c.Host, Draw.fBodyB, ink, new RectangleF(c2.X + 14 * S, yy, w * 0.55f, 18 * S), nowrap);
                    g.DrawString(c.Proto + "   " + c.Remote + ":" + c.RemotePort, Draw.fMonoS, muted, new RectangleF(c2.X + 14 * S, yy + 17 * S, w * 0.6f, 16 * S), nowrap);
                    string rate = c.Rate >= 0.05 ? c.Rate.ToString("0.0", CultureInfo.CurrentCulture) + " Mb/s" : (c.Rate * 1000).ToString("0", CultureInfo.CurrentCulture) + " kb/s";
                    var rs = g.MeasureString(rate, Draw.fMono); g.DrawString(rate, Draw.fMono, ink, c2.Right - 130 * S - rs.Width, yy + 6 * S);
                    string flag = c.Retx > 0 ? c.Retx + " retrans" : "clean";
                    using (var fb = new SolidBrush(c.Retx > 0 ? Theme.LvText(1) : Theme.LvText(0))) { var fs = g.MeasureString(flag, Draw.fSmall); g.DrawString(flag, Draw.fSmall, fb, c2.Right - 14 * S - fs.Width, yy + 7 * S); }
                    yy += 36 * S;
                }
                y += c2.Height + 20 * S;
            }
            var need = new Size(0, (int)(y + 20 * S));
            if (liveCanvas.AutoScrollMinSize != need) liveCanvas.AutoScrollMinSize = need;
        }

        public void ShowPage(string page) { tab = page; ShowTab(); }
        void BuildTipBar()
        {
            tipBar = new Panel { Dock = DockStyle.Top, Height = Sc(46), BackColor = Theme.BlueSoft, Tag = "tip" };
            var msg = new Label { Text = Voice.P("Quick tip, boss: Esc, minimise, the X or Back to corner all take you back to the corner widget - I keep watching. Exit lives in the tray icon (right-click).",
                                                 "Esc, minimise, the X or Back to corner return you to the corner widget, which keeps watching. To quit, right-click the tray icon and choose Exit."),
                                  AutoSize = false, Dock = DockStyle.Fill, Font = Draw.fBody, ForeColor = Theme.Dark ? Theme.Ink : Theme.BlueDark, BackColor = Theme.BlueSoft, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(Sc(20), 0, 0, 0), Tag = "tip" };
            var ok = new PillButton("Got it", PillButton.K.Chip) { Anchor = AnchorStyles.Right | AnchorStyles.Top };
            ok.Click += delegate { Cfg.SeenTip = true; Cfg.Save(); var t = tipBar; tipBar = null; Controls.Remove(t); t.Dispose(); };
            tipBar.Controls.Add(msg); tipBar.Controls.Add(ok); ok.BringToFront();
            tipBar.Resize += delegate { ok.Location = new Point(tipBar.Width - ok.Width - Sc(20), (tipBar.Height - ok.Height) / 2); };
            Controls.Add(tipBar); tipBar.BringToFront();
            // keep the header above the tip
            foreach (Control c in Controls) if (c == header) { c.SendToBack(); break; }
            tipBar.SendToBack(); header.SendToBack();
        }
        public void Debug(string page, string tipK, string destIn)
        {
            tab = page; ShowTab();
            if (destIn == "demo-story" && page == "dest")
            {
                // screenshot aid: a typical home route, with made-up router names
                Func<string, double, Tuple<string, double, int>> H = (ip, ms) => Tuple.Create(ip, ms, 0);
                var hops = new List<Tuple<string, double, int>> { H("192.168.0.1", 2), H("10.0.0.1", 4), H("24.156.1.1", 12), Tuple.Create((string)null, 0.0, 100), H("209.148.1.1", 14), H("129.250.2.1", 16), H("129.250.3.1", 88), H("140.82.114.4", 90) };
                var names = new Dictionary<string, string> { { "24.156.1.1", "8080-dgw01.ym.rogers.com" }, { "209.148.1.1", "3020-cgw01.tor.rogers.com" }, { "129.250.2.1", "ae-5.r20.tor01.ca.bb.gin.ntt.net" }, { "129.250.3.1", "ae-1.r24.iad02.us.bb.gin.ntt.net" }, { "140.82.114.4", "lb-140-82-114-4-iad.github.com" } };
                var r = new DestResult { Input = "github.com", Target = "github.com", Level = 0, Verdict = "github.com is happy to see you - 42 ms to first byte. If it feels slow, it isn't the network." };
                for (int i = 0; i < 8; i++) { r.Lv[i] = 0; r.Val[i] = new[] { "17 ms", "Wi-Fi", "90 ms · 0%", "91 ms", "TLS 1.3 · 180 ms", "200 · 42 ms", "1492", "8 hops" }[i]; }
                DestCheck.Story(r, IPAddress.Parse("140.82.114.4"), hops, 7, "192.168.0.50", false, 1492, "", 91, 0, 90, 443, names);
                r.Done = true; dest = r; destCanvas.Invalidate();
            }
            else if (destIn != null && page == "dest") { destBox.Box.Text = destIn; StartDest(); }
            if (page == "changes" && destIn == "today") { histDay = DateTime.Today; histData = History.Load(DateTime.Today); changesCanvas.Invalidate(); }
            if (page == "procs")
            {
                var t = new System.Windows.Forms.Timer { Interval = 2500 };
                t.Tick += delegate { t.Stop(); t.Dispose(); List<AppStat> l; lock (M.L) l = M.Apps; var a = l.FirstOrDefault(x => x.Name == "svchost") ?? l.FirstOrDefault(); if (a != null) SelectProc(a); };
                t.Start();
            }
            if (tipK != null) { var t2 = new System.Windows.Forms.Timer { Interval = 1200 }; t2.Tick += delegate { t2.Stop(); t2.Dispose(); Cursor.Position = new Point(Width / 2, Height / 3); tipKey = tipK; ShowTip(); }; t2.Start(); }
        }

        // Self-test: visit every page (incl. Settings), open an explanation and a hover card, then go back.
        // Leak hunt: visit just these pages (and optionally show a hover card) before closing.
        public void TestPages(string[] pages, bool withTip)
        {
            foreach (var t in pages) { tab = t; ShowTab(); Refresh(); Application.DoEvents(); }
            if (withTip) { SetTipKey("path.DNS"); ShowTip(); Application.DoEvents(); SetTipKey(null); }
            tab = "conn"; ShowTab();
        }
        public int TestWalk()
        {
            int n = 0;
            foreach (var t in new[] { "conn", "link", "procs", "dest", "health", "changes", "live", "settings", "about" }) { tab = t; ShowTab(); Refresh(); Application.DoEvents(); n++; }
            List<AppStat> l; lock (M.L) l = M.Apps;
            if (l.Count > 0) { tab = "procs"; ShowTab(); SelectProc(l[0]); Thread.Sleep(50); Application.DoEvents(); procInfo.Refresh(); }
            SetTipKey("path.DNS"); ShowTip(); Application.DoEvents(); SetTipKey(null);
            tab = "conn"; ShowTab();
            return n;
        }

        // ---------- hover explanations and clickable painted areas
        void Begin(Control c)
        {
            List<KeyValuePair<RectangleF, string>> h; if (hints.TryGetValue(c, out h)) h.Clear(); else hints[c] = new List<KeyValuePair<RectangleF, string>>();
            List<KeyValuePair<RectangleF, Action>> a; if (acts.TryGetValue(c, out a)) a.Clear(); else acts[c] = new List<KeyValuePair<RectangleF, Action>>();
        }
        static Point ScrollOf(Control c) { var sc = c as ScrollableControl; return sc != null && sc.AutoScroll ? sc.AutoScrollPosition : Point.Empty; }
        void Hint(Control c, RectangleF r, string key)
        {
            if (key == null) return; var sp = ScrollOf(c); r.Offset(sp.X, sp.Y);
            List<KeyValuePair<RectangleF, string>> h; if (!hints.TryGetValue(c, out h)) hints[c] = h = new List<KeyValuePair<RectangleF, string>>();
            h.Add(new KeyValuePair<RectangleF, string>(r, key));
        }
        void Act(Control c, RectangleF r, Action a)
        {
            var sp = ScrollOf(c); r.Offset(sp.X, sp.Y);
            List<KeyValuePair<RectangleF, Action>> l; if (!acts.TryGetValue(c, out l)) acts[c] = l = new List<KeyValuePair<RectangleF, Action>>();
            l.Add(new KeyValuePair<RectangleF, Action>(r, a));
        }
        void Wire(Control c)
        {
            c.MouseMove += delegate(object s, MouseEventArgs e)
            {
                List<KeyValuePair<RectangleF, Action>> l; bool click = acts.TryGetValue(c, out l) && l.Any(kv => kv.Key.Contains(e.Location));
                List<KeyValuePair<RectangleF, string>> h; string key = null;
                if (hints.TryGetValue(c, out h)) for (int i = h.Count - 1; i >= 0; i--) if (h[i].Key.Contains(e.Location)) { key = h[i].Value; break; }
                if (key != null && TipEntry(key) == null) key = null;
                c.Cursor = click ? Cursors.Hand : key != null ? Cursors.Help : Cursors.Default;
                SetTipKey(key);
            };
            c.MouseLeave += delegate { SetTipKey(null); };
            c.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                List<KeyValuePair<RectangleF, Action>> l; if (!acts.TryGetValue(c, out l)) return;
                for (int i = l.Count - 1; i >= 0; i--) if (l[i].Key.Contains(e.Location)) { SetTipKey(null); l[i].Value(); return; }
            };
            c.MouseWheel += delegate { SetTipKey(null); };
        }
        void SetTipKey(string key)
        {
            if (key == tipKey) return;
            tipKey = key; tipTimer.Stop();
            if (key == null) { if (tip.Visible) tip.Hide(); return; }
            tipTimer.Interval = tip.Visible ? 60 : 350; tipTimer.Start();
        }
        void ShowTip()
        {
            if (IsDisposed || tipKey == null || !Visible || WindowState == FormWindowState.Minimized) return;
            var e = TipEntry(tipKey); if (e == null) return;
            tip.ShowAt(e, Cursor.Position, this);
        }
        static Explain.Entry TipEntry(string key)
        {
            if (key.StartsWith("proc:", StringComparison.Ordinal)) return ProcEntry(key.Substring(5), null, null);
            return Explain.Term(key);
        }
        static string CardKey(string title)
        {
            switch (title)
            {
                case "DNS": return "step.DNS";
                case "ROUTE": return "step.Route";
                case "TLS": case "TLS CERTIFICATE": return "step.TLS";
                case "HTTP": return "step.HTTP";
                case "PATH MTU": return "step.Path MTU";
                case "PATH (3 ROUNDS PER HOP)": return "step.Path";
                default: return "card." + title;
            }
        }
        // Process explanation: AdeeboMeter's guide first, then what the exe file says about itself.
        static Explain.Entry ProcEntry(string name, string[] facts, string path)
        {
            var e = Explain.Find(name) ?? Explain.Find(Monitor.RawOf(name));
            if (e != null) return e;
            string desc = facts != null ? facts[0] : "", comp = facts != null ? facts[1] : "", prod = facts != null ? facts[2] : "", where = facts != null ? facts[3] : "";
            return new Explain.Entry
            {
                Friendly = desc != "" ? desc : name, Cat = Explain.Other, Close = -1,
                Tag = comp != "" ? "Made by " + comp + "." : Voice.P("Not in my guide - here's what Windows knows.", "Not in AdeeboMeter's guide."),
                What = desc != "" ? "The file calls itself '" + desc + "'" + (prod != "" && prod != desc ? ", part of " + prod : "") + "." : facts == null ? "Open What's running and click it - I'll read what the file says about itself." : "The file doesn't describe itself.",
                How = facts == null ? "" : Explain.WhereText(path == "" ? "" : where),
                Why = "AdeeboMeter's guide doesn't know this one by heart, so this is what the program file itself says.",
                Without = "If it's an app you opened, closing it is safe. If you don't recognise it and it isn't in Program Files or the Windows folder, ask IT before touching it."
            };
        }

        void UpdateBadges()
        {
            foreach (Control c in tabs.Controls)
            {
                var b = (PillButton)c; string t = (string)b.Tag;
                if (t == "conn") b.SetBadge(list.Shown > 0 ? list.Shown.ToString(CultureInfo.CurrentCulture) : "", list.SynSent > 0 ? 1 : -1);
                else if (t == "procs") { int n; lock (M.L) n = M.Apps.Count; b.SetBadge(n > 0 ? n.ToString(CultureInfo.CurrentCulture) : "", -1); }
                else if (t == "changes")
                {
                    var fresh = M.Events.Recent(300).Where(e => e.T > changesSeen && e.Kind != "verdict").ToList();
                    b.SetBadge(fresh.Count > 0 ? fresh.Count + " new" : "", fresh.Count == 0 ? -1 : fresh.Max(e => e.Lv) >= 2 ? 2 : fresh.Max(e => e.Lv) == 1 ? 1 : 4);
                }
                else if (t == "live") b.SetBadge(live.Running ? "REC" : "", 2);
                else if (t == "link")
                {
                    bool wifi; lock (M.L) wifi = M.AdapterKind == "Wi-Fi";
                    var names = wifi ? new[] { "Your Wi-Fi", "Wi-Fi" } : new[] { "Your cable", "Cable" };
                    if (tabNames["link"][0] != names[0]) { tabNames["link"] = names; b.Text = names[shortTabs ? 1 : 0]; }
                    var ls = linkSnap; b.SetBadge(ls != null && ls.Level > 0 ? "!" : "", ls != null ? ls.Level : -1);
                }
                else if (t == "dest") b.SetBadge(pinRunning || destRunning ? "..." : "", 4);
            }
            FitTabs();
        }

        // Dark scrollbars: Windows draws themed scrollbars when a control opts in to the Explorer dark theme.
        void ThemeScrollbars(Control root)
        {
            foreach (Control c in root.Controls)
            {
                var sc = c as ScrollableControl;
                if ((c is ScrollBar || (sc != null && sc.AutoScroll)) && !themedScroll.Contains(c))
                {
                    if (c.IsHandleCreated) { themedScroll.Add(c); try { if (Native.SetWindowTheme(c.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer", null) != 0) Program.Log("scrollbar theme not applied"); } catch { } }
                }
                ThemeScrollbars(c);
            }
        }

        // ---------- What's running
        void RefreshProcs()
        {
            lastProcs = DateTime.UtcNow;
            List<AppStat> l; lock (M.L) l = M.Apps;
            procs = l ?? new List<AppStat>();
            procList.Invalidate(); procInfo.Invalidate();
            // where unknown programs live (once per name, off the UI thread)
            var todo = procs.Where(a => Explain.Find(a.Name) == null && Explain.Find(Monitor.RawOf(a.Name)) == null && !procWhere.ContainsKey(a.Name)).Select(a => new KeyValuePair<string, int>(a.Name, a.Pid)).ToList();
            if (todo.Count == 0 || whereBusy) return;
            whereBusy = true;
            Task.Factory.StartNew(() =>
            {
                var res = new Dictionary<string, string>();
                foreach (var t in todo) { string path = ""; try { path = Native.ExePath(t.Value); } catch { } res[t.Key] = path == "" ? "" : Explain.Where(path); }
                try { BeginInvoke((Action)delegate { foreach (var kv in res) procWhere[kv.Key] = kv.Value; whereBusy = false; procList.Invalidate(); }); } catch { whereBusy = false; }
            });
        }
        string CatOf(AppStat a)
        {
            var e = Explain.Find(a.Name) ?? Explain.Find(Monitor.RawOf(a.Name));
            if (e != null) return e.Cat;
            string w; procWhere.TryGetValue(a.Name, out w);
            return w == "windows" ? Explain.Help : w == "programfiles" || w == "user" ? Explain.App : Explain.Other;
        }
        void SelectProc(AppStat a)
        {
            procSel = a.Name; procFacts = null; procPathSel = ""; procInfo.AutoScrollPosition = Point.Empty; procList.Invalidate(); procInfo.Invalidate();
            string name = a.Name; int pid = a.Pid;
            Task.Factory.StartNew(() =>
            {
                string path = ""; try { path = Native.ExePath(pid); } catch { }
                var f = Explain.FileFacts(path);
                try { BeginInvoke((Action)delegate { if (procSel == name) { procFacts = f; procPathSel = path; procInfo.Invalidate(); } }); } catch { }
            });
        }
        void PaintProcList(Graphics g)
        {
            g.TranslateTransform(procList.AutoScrollPosition.X, procList.AutoScrollPosition.Y);
            Begin(procList);
            float x = 14 * S, y = 4 * S, w = procList.ClientSize.Width - 22 * S, rh = 42 * S;
            string q = procSearch.Box.Text.Trim();
            var items = procs.Where(a => a.Name != "Idle").Select(a => new { A = a, E = Explain.Find(a.Name) ?? Explain.Find(Monitor.RawOf(a.Name)), C = CatOf(a) })
                             .Where(i => q == "" || i.A.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || (i.E != null && (i.E.Friendly.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || i.E.Tag.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))).ToList();
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (items.Count == 0) g.DrawString(procs.Count == 0 ? Voice.P("Reading the process list...", "Loading...") : "Nothing matches.", Draw.fBody, muted, x, y + 8 * S);
                foreach (var cat in Explain.CatOrder)
                {
                    var rows = items.Where(i => i.C == cat).OrderByDescending(i => i.A.Mem).ToList();
                    if (rows.Count == 0) continue;
                    y += 8 * S;
                    g.DrawString(cat.ToUpperInvariant() + "  ·  " + rows.Count, Draw.fTiny, muted, x, y); y += 20 * S;
                    foreach (var i in rows)
                    {
                        var a = i.A; var rr = new RectangleF(0, y, w + 8 * S, rh);
                        if (a.Name == procSel) using (var b = new SolidBrush(Theme.RowSel)) g.FillRectangle(b, rr);
                        Theme.Fill(g, new RectangleF(x, y + rh / 2 - 5 * S, 10 * S, 10 * S), 5 * S, i.E == null ? Theme.Grey : i.E.Close == 2 ? Theme.Red : i.E.Close == 3 ? Theme.C("#8250DF") : i.E.Close == 1 ? Theme.Amber : Theme.Green);
                        string title = i.E != null ? i.E.Friendly : a.Name;
                        string subl = (i.E != null ? i.E.Tag : "not in the guide - click to see what the file says") + (a.Count > 1 ? "   ·   " + a.Count + " copies" : "");
                        float tx = x + 20 * S, tw = w - 20 * S - 150 * S;
                        g.DrawString(title + (i.E != null && !string.Equals(i.E.Friendly, a.Name, StringComparison.OrdinalIgnoreCase) ? "  (" + a.Name + ")" : ""), Draw.fBodyB, ink, new RectangleF(tx, y + 4 * S, tw, 18 * S), nowrap);
                        g.DrawString(subl, Draw.fSmall, muted, new RectangleF(tx, y + 22 * S, tw, 16 * S), nowrap);
                        string mem = Draw.MemStr(a.Mem), cpu = a.Cpu.ToString("0.0", CultureInfo.CurrentCulture) + "%";
                        var ms = g.MeasureString(mem, Draw.fMono); g.DrawString(mem, Draw.fMono, ink, x + w - 64 * S - ms.Width, y + 12 * S);
                        var cs = g.MeasureString(cpu, Draw.fMono); g.DrawString(cpu, Draw.fMono, sub, x + w - cs.Width, y + 12 * S);
                        g.DrawLine(line, x, y + rh - 1, x + w, y + rh - 1);
                        var captured = a; Act(procList, rr, delegate { SelectProc(captured); });
                        y += rh;
                    }
                }
            }
            var need = new Size(0, (int)(y + 20 * S)); if (procList.AutoScrollMinSize != need) procList.AutoScrollMinSize = need;
        }
        void PaintProcInfo(Graphics g)
        {
            g.TranslateTransform(procInfo.AutoScrollPosition.X, procInfo.AutoScrollPosition.Y);
            Begin(procInfo);
            float x = 20 * S, y = 14 * S, w = procInfo.ClientSize.Width - 40 * S;
            using (var line = new Pen(Theme.Line)) g.DrawLine(line, 0, 0, 0, Math.Max(procInfo.ClientSize.Height, procInfo.AutoScrollMinSize.Height));
            var a = procs.FirstOrDefault(p => p.Name == procSel);
            if (a == null)
            {
                int total = procs.Sum(p => p.Count), known = procs.Count(p => Explain.Find(p.Name) != null || Explain.Find(Monitor.RawOf(p.Name)) != null);
                var intro = new Explain.Entry
                {
                    Friendly = Voice.P("Meet the crew running your PC.", "What's running on this PC"), Close = -1,
                    Tag = total + " processes, grouped into " + procs.Count + " programs. I know " + known + " of them by heart.",
                    What = "Every program on your PC is one or more 'processes' - a running copy with its own memory. Windows itself is dozens of them, each with one job.",
                    How = "Click any row on the left. I'll tell you what it does, how it works, why it's there, what would happen without it - and whether it's safe to close.",
                    Why = "Once you know who's who, Task Manager stops being scary - and you'll know exactly what to close when the PC is struggling.",
                    Without = "Coloured dots: green = safe to close, amber = leave it (OK to restart), red = Windows needs it, purple = managed by IT, grey = not in my guide."
                };
                y = ExplainPaint.Paint(g, intro, x, y, w, false);
            }
            else
            {
                var e = ProcEntry(a.Name, procFacts, procPathSel);
                string facts = a.Count + (a.Count == 1 ? " copy" : " copies") + "   ·   " + Draw.MemStr(a.Mem) + " memory   ·   " + a.Cpu.ToString("0.0", CultureInfo.CurrentCulture) + "% CPU";
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(facts, Draw.fMonoS, mb, x, y); y += 22 * S;
                y = ExplainPaint.Paint(g, e, x, y, w, false);
                y += 10 * S;
                var rows = new List<string[]>();
                rows.Add(new[] { "Program file", procFacts == null ? "reading..." : procPathSel != "" ? procPathSel : "hidden by Windows (protected process)" });
                if (procFacts != null && procFacts[0] != "") rows.Add(new[] { "Describes itself as", procFacts[0] });
                if (procFacts != null && procFacts[1] != "") rows.Add(new[] { "Publisher", procFacts[1] });
                if (procFacts != null) rows.Add(new[] { "Where it lives", Explain.WhereText(procPathSel == "" ? "" : procFacts[3]) });
                y = ExplainPaint.Section(g, "ON THIS PC", rows, x, y, w);
            }
            var need = new Size(0, (int)(y + 30 * S)); if (procInfo.AutoScrollMinSize != need) procInfo.AutoScrollMinSize = need;
        }

        // ---------- Settings
        static readonly string[] SetS0 = { "balanced", "Balanced", "The default. Notices trouble within seconds and still costs next to nothing." };
        static readonly string[] SetS1 = { "engineer", "Engineer", "Watches everything closely - for when something is wrong and you want every clue." };
        static readonly string[] SetS2 = { "2 s", "5 s", "10 s", "Off" };
        static readonly int[] SetN3 = { 2, 5, 10, 0 };
        static readonly string[] SetS4 = { "15 s", "30 s", "60 s", "Off" };
        static readonly int[] SetN5 = { 15, 30, 60, 0 };
        static readonly string[] SetS6 = { "10 s", "30 s", "2 min", "Off" };
        static readonly int[] SetN7 = { 10, 30, 120, 0 };
        static readonly string[] SetS8 = { "5 s", "10 s", "30 s", "Off" };
        static readonly int[] SetN9 = { 5, 10, 30, 0 };
        static readonly string[] SetS10 = { "On", "Off" };
        static readonly string[] SetS11 = { "5 min", "15 min", "30 min" };
        static readonly int[] SetN12 = { 5, 15, 30 };
        static readonly string[] SetS13 = { "30 s", "60 s" };
        static readonly string[] SetS14 = { "20%", "30%", "50%" };
        static readonly int[] SetN15 = { 51, 77, 128 };
        static readonly string[] SetS16 = { "0.3 s", "0.45 s", "0.8 s" };
        static readonly int[] SetN17 = { 300, 450, 800 };
        static readonly string[] SetS18 = { "Follow Windows", "Light", "Dark" };
        static readonly string[] SetS19 = { "Companion", "Serious" };
        void PaintSettings(Graphics g)
        {
            g.TranslateTransform(settingsCanvas.AutoScrollPosition.X, settingsCanvas.AutoScrollPosition.Y);
            Begin(settingsCanvas);
            float x = 24 * S, y = 18 * S, w = Math.Min(settingsCanvas.ClientSize.Width - 48 * S, 1100 * S);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub))
            {
                g.DrawString(Voice.P("How hard should I work, boss?", "How hard should AdeeboMeter work?"), Draw.fTitle, ink, x, y); y += 30 * S;
                string lead = "The widget is always a penny app. The deep stuff (live capture, the full connection list, path checks) only runs while this window is open. These settings decide how much watching happens in the background.";
                var lh = Draw.Measure(lead, Draw.fBody, (int)w).Height; g.DrawString(lead, Draw.fBody, sub, new RectangleF(x, y, w, lh + 4)); y += lh + 14 * S;

                // presets
                string[][] presets = { new[] { "penny", "Penny", "The lightest touch. Checks less often, Wi-Fi watch every 2 min, no call watching." },
                                       SetS0,
                                       SetS1 };
                float pw = (w - 24 * S) / 3, ph = 96 * S;
                for (int i = 0; i < 3; i++)
                {
                    var r = new RectangleF(x + i * (pw + 12 * S), y, pw, ph); bool on = Cfg.Preset == presets[i][0];
                    Theme.Fill(g, r, 12 * S, on ? Theme.BlueSoft : Theme.Soft); Theme.Stroke(g, r, 12 * S, on ? Theme.Blue : Theme.Line, on ? 2 : 1);
                    using (var tb = new SolidBrush(on ? Theme.BlueDark : Theme.Ink)) g.DrawString(presets[i][1], Draw.fHead, tb, r.X + 14 * S, r.Y + 12 * S);
                    string cost = "~" + JobsFor(presets[i][0]) + " checks a minute";
                    var cs = g.MeasureString(cost, Draw.fSmall); g.DrawString(cost, Draw.fSmall, muted, r.Right - 14 * S - cs.Width, r.Y + 15 * S);
                    g.DrawString(presets[i][2], Draw.fSmall, sub, new RectangleF(r.X + 14 * S, r.Y + 40 * S, r.Width - 28 * S, ph - 46 * S));
                    string key = presets[i][0]; Act(settingsCanvas, r, delegate { Cfg.ApplyPreset(key); Cfg.Save(); M.Nudge(); settingsCanvas.Invalidate(); });
                }
                y += ph + 10 * S;
                string now = "Right now: about " + Cfg.JobsPerMinute() + " checks a minute in the background" + (Cfg.Preset == "custom" ? " (your own mix)" : "") + ", on top of the widget's once-a-second read of Windows' own counters. Each check is one tiny probe or one quick read - a few milliseconds of work.";
                var nh = Draw.Measure(now, Draw.fSmall, (int)w).Height; g.DrawString(now, Draw.fSmall, muted, new RectangleF(x, y, w, nh + 4)); y += nh + 18 * S;

                y = SetHead(g, "IN THE BACKGROUND  ·  WHILE ONLY THE WIDGET RUNS", x, y, w);
                y = SetRow(g, x, y, w, "Router ping", Cfg.GwSec == 0 ? "Off: the LAN / Wi-Fi dot goes grey and slow-router alerts stop." : "Pings your router every " + Cfg.GwSec + " s - tells Wi-Fi trouble apart from internet trouble.",
                    SetS2, Array.IndexOf(SetN3, Cfg.GwSec), delegate(int i) { Cfg.GwSec = SetN3[i]; Custom(); });
                y = SetRow(g, x, y, w, "Internet and DNS check", Cfg.NetSec == 0 ? "Off: no 'internet went quiet' alerts; the Internet and DNS dots go grey." : "One tiny ping to a public server and one name lookup every " + Cfg.NetSec + " s.",
                    SetS4, Array.IndexOf(SetN5, Cfg.NetSec), delegate(int i) { Cfg.NetSec = SetN5[i]; Custom(); });
                y = SetRow(g, x, y, w, "Wi-Fi watch", Cfg.WifiSec == 0 ? "Off: no roam, signal or channel alerts." : "Asks Windows for signal, channel and access point every " + (Cfg.WifiSec >= 60 ? Cfg.WifiSec / 60 + " min" : Cfg.WifiSec + " s") + ". The priciest job here - it starts a small Windows tool (netsh) each time.",
                    SetS6, Array.IndexOf(SetN7, Cfg.WifiSec), delegate(int i) { Cfg.WifiSec = SetN7[i]; Custom(); });
                y = SetRow(g, x, y, w, "App and connection watch", Cfg.AppsSec == 0 ? "Off: the verdict can't name the app to close, and connection bursts go unnoticed." : "Reads the process list and connection table every " + Cfg.AppsSec + " s - powers 'top apps', connection bursts and stuck-connection alerts.",
                    SetS8, Array.IndexOf(SetN9, Cfg.AppsSec), delegate(int i) { Cfg.AppsSec = SetN9[i]; Custom(); });
                y = SetRow(g, x, y, w, "Teams call detection", Cfg.Call ? "Notices when a Teams call starts and watches the network more closely during it." : "Off: calls get no extra attention.",
                    SetS10, Cfg.Call ? 0 : 1, delegate(int i) { Cfg.Call = i == 0; Custom(); });

                y = SetRow(g, x, y, w, "Keep 7 days of history", Cfg.History ? "Saves one small line every 10 s plus every change, in your profile (about 100 KB a day), so What changed can show past days. Deleted after 7 days." : "Off: nothing is written to disk - What changed only remembers the last 2 hours, in memory.",
                    SetOnOff, Cfg.History ? 0 : 1, delegate(int i) { Cfg.History = i == 0; Cfg.Save(); });
                if (History.Days().Count > 0)
                {
                    var clr = new RectangleF(x, y - 4 * S, 220 * S, 22 * S);
                    using (var cb = new SolidBrush(Theme.LvText(2))) g.DrawString("Delete all saved history now", Draw.fBodyB, cb, clr.X, clr.Y);
                    Act(settingsCanvas, clr, delegate { History.Clear(); histDay = null; histData = null; histDays = null; settingsCanvas.Invalidate(); });
                    y += 26 * S;
                }
                y = SetHead(g, "IN THIS WINDOW  ·  ONLY WHILE IT'S OPEN", x, y, w);
                y = SetRow(g, x, y, w, "Sign-in page check", Cfg.Portal ? "Only when a check you start fails: one tiny request to the same test page Windows uses, to catch hotel and airport sign-in pages." : "Off: a failed check won't look for a Wi-Fi sign-in page.",
                    SetOnOff, Cfg.Portal ? 0 : 1, delegate(int i) { Cfg.Portal = i == 0; Cfg.Save(); });
                y = SetRow(g, x, y, w, "Names for IP addresses", Cfg.IpNames ? "Reads names from Windows' own DNS cache - no network traffic." : "Off: connections show bare IP addresses.",
                    SetS10, Cfg.IpNames ? 0 : 1, delegate(int i) { Cfg.IpNames = i == 0; Cfg.Save(); });
                y = SetRow(g, x, y, w, "Live capture stops after", "tshark is the heaviest thing AdeeboMeter can run, so it always stops by itself.",
                    SetS11, Array.IndexOf(SetN12, Cfg.LiveMin), delegate(int i) { Cfg.LiveMin = SetN12[i]; Cfg.Save(); });
                y = SetRow(g, x, y, w, "Sniff records for", "How long a row's Sniff button captures before opening Wireshark.",
                    SetS13, Cfg.SniffSec == 60 ? 1 : 0, delegate(int i) { Cfg.SniffSec = i == 1 ? 60 : 30; Cfg.Save(); });

                y = SetHead(g, "THE WIDGET", x, y, w);
                y = SetRow(g, x, y, w, "How visible", "How strongly the corner widget shows when you're not hovering it.",
                    SetS14, Array.IndexOf(SetN15, Cfg.Fade), delegate(int i) { Cfg.Fade = SetN15[i]; Cfg.Save(); });
                y = SetRow(g, x, y, w, "Hover wait on the folded chip", "How long to rest the mouse on the folded chip before the card opens.",
                    SetS16, Array.IndexOf(SetN17, Cfg.DwellMs), delegate(int i) { Cfg.DwellMs = SetN17[i]; Cfg.Save(); });

                y = SetHead(g, "GENERAL", x, y, w);
                int th = Cfg.GetTheme();
                y = SetRow(g, x, y, w, "Theme", "Follow Windows switches light and dark with your Windows setting.",
                    SetS18, th < 0 ? 0 : th == 0 ? 1 : 2, delegate(int i) { Cfg.SetTheme(i == 0 ? -1 : i == 1 ? 0 : 1); });
                y = SetRow(g, x, y, w, "Voice", Voice.Serious ? "Plain, serious wording." : "The companion - a joke first, then the fix.",
                    SetS19, Voice.Serious ? 1 : 0, delegate(int i) { Voice.Serious = i == 1; Cfg.Save(); Invalidate(true); });
                y = SetRow(g, x, y, w, "Start with Windows", "Starts quietly when you sign in. Exit from the tray stops it completely - nothing stays behind.",
                    SetS10, Cfg.GetAutoStart() ? 0 : 1, delegate(int i) { Cfg.SetAutoStart(i == 0); });
                y += 10 * S;
                g.DrawString("Settings are saved next to AdeeboMeter.exe (AdeeboMeter.settings). Nothing leaves this PC.", Draw.fSmall, muted, x, y); y += 24 * S;
            }
            var need = new Size(0, (int)(y + 20 * S)); if (settingsCanvas.AutoScrollMinSize != need) settingsCanvas.AutoScrollMinSize = need;
        }
        static readonly string[] SetOnOff = { "On", "Off" };
        static int JobsFor(string preset)
        {
            int gw = Cfg.GwSec, net = Cfg.NetSec, wifi = Cfg.WifiSec, apps = Cfg.AppsSec; bool call = Cfg.Call; string p = Cfg.Preset;
            Cfg.ApplyPreset(preset); int n = Cfg.JobsPerMinute();
            Cfg.GwSec = gw; Cfg.NetSec = net; Cfg.WifiSec = wifi; Cfg.AppsSec = apps; Cfg.Call = call; Cfg.Preset = p;
            return n;
        }
        void Custom() { Cfg.Preset = "custom"; Cfg.Save(); M.Nudge(); }
        static float SetHead(Graphics g, string t, float x, float y, float w)
        {
            y += 8 * S;
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(t, Draw.fTiny, mb, x, y);
            using (var line = new Pen(Theme.Line)) g.DrawLine(line, x, y + 20 * S, x + w, y + 20 * S);
            return y + 28 * S;
        }
        float SetRow(Graphics g, float x, float y, float w, string label, string help, string[] opts, int cur, Action<int> set)
        {
            float ow = 0; var widths = opts.Select(o => g.MeasureString(o, Draw.fSmall).Width + 24 * S).ToArray(); ow = widths.Sum() + 2 * S;
            float tw = w - ow - 24 * S;
            float hh = Draw.Measure(help, Draw.fSmall, (int)tw).Height;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString(label, Draw.fBodyB, ink, x, y);
                g.DrawString(help, Draw.fSmall, muted, new RectangleF(x, y + 20 * S, tw, hh + 4));
            }
            var seg = new RectangleF(x + w - ow, y + 4 * S, ow, 30 * S);
            Theme.Fill(g, seg, 15 * S, Theme.Soft); Theme.Stroke(g, seg, 15 * S, Theme.Line, 1);
            float sx = seg.X + 1 * S;
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                for (int i = 0; i < opts.Length; i++)
                {
                    var r = new RectangleF(sx, seg.Y + 2 * S, widths[i], seg.Height - 4 * S);
                    if (i == cur) Theme.Fill(g, r, 13 * S, Theme.Blue);
                    using (var b = new SolidBrush(i == cur ? Color.White : Theme.Sub)) g.DrawString(opts[i], Draw.fSmall, b, r, center);
                    int idx = i; Act(settingsCanvas, r, delegate { set(idx); settingsCanvas.Invalidate(); });
                    sx += widths[i];
                }
            return y + Math.Max(44 * S, 26 * S + hh) + 10 * S;
        }

        // ---------- About: the whole story, told simply
        static readonly string[] LeaveChecks = { "Checks you start", "Check a destination sends only what you ask for, to where you ask, when you press Check.", "1" };
        static readonly string[] LeaveLive = { "Live capture", "Listens to your own adapter only while you press Start. Nothing is sent; captures are deleted when AdeeboMeter exits.", "-1" };
        static readonly string[] LeaveNever = { "Never", "No cloud, no account, no telemetry, no updates, no ads, no crash reports. It never listens on a port - and checks that about itself every time it starts.", "0" };
        static readonly string[][] AboutNav = {
                    new[] { "Open this view", "Click the tray icon, or press Ctrl+Alt+A from anywhere." },
                    new[] { "Back to the corner", "Esc, minimise, the X or the Back to corner button - they all do the same thing: this view closes, gives its memory back, and the widget keeps watching." },
                    new[] { "Quit completely", "Right-click the tray icon and choose Exit. Nothing keeps running - not even a helper." },
                    new[] { "Learn anything", "When the mouse turns into a ? cursor, rest it there for a moment and an explanation appears." },
                    new[] { "Tune it", "Settings decides how much AdeeboMeter watches in the background - from Penny to Engineer." } };
        static readonly string[][] AboutTrust = {
                    new[] { "One readable file", "The whole app is a single source file you can read before you run it, built with the compiler that already ships inside Windows." },
                    new[] { "No admin rights", "Everything it shows comes from what Windows already lets a normal user see. If something is locked, it simply doesn't show." },
                    new[] { "Secure by default", "Checks use TLS 1.2 or newer with Windows' own certificate validation - it never skips a certificate check. Inputs are validated; no shell is ever used." },
                    new[] { "Light as a feather", "A few dozen small checks a minute in the background. The heavy tools only run while this window is open, and always stop by themselves." },
                    new[] { "Leaves no trace", "Settings and a small log live next to the exe. Captures are deleted on exit. Window titles and traffic content are never logged. The 7-day history is off unless you switch it on - and one click deletes it." } };
        static readonly string[][] AboutTabs =
        {
            new[] { "Connections", "Every conversation your PC is having right now - which app, with whom, on which port, in what state. New ones flash in, closed ones fade out. Hover a state to learn what it means; press Sniff on a row to record just that conversation for Wireshark." },
            new[] { "Your Wi-Fi / cable", "Everything about your link: band, channel, signal in dBm, speeds, security, who else shares your air (with a channel map), addresses, errors, and how healthy the hop to your router is. Click the Wi-Fi dot to get here." },
            new[] { "What's running", "Every program on your PC, in plain English: what it does, how it works, why it's there, what happens without it - and whether it's safe to close. AdeeboMeter only explains; it never closes anything." },
            new[] { "Check a destination", "Type a name, an IP or a URL. AdeeboMeter walks the whole path like an engineer would - DNS, route, ping, TCP, TLS, HTTP, packet size, every router on the way - and tells you in one sentence where it breaks. It also tells the path's story: who carries your traffic, where networks hand over (that's where BGP decides), NAT, tunnels and firewalls. Pin the places you check often and run them all at once." },
            new[] { "Health check", "A one-click check-up of the things that quietly cause trouble: days since restart, pending updates, disk space, adapter errors, crowded Wi-Fi, proxies, VPN, DNS, memory and power." },
            new[] { "What changed", "The diary. Every roam, drop, DNS change, VPN flap and app hiccup in the last two hours, with the time it happened - so 'it was slow at 2:15' finally has an answer." },
            new[] { "Live capture", "Wireshark's engine (tshark), only if you have Wireshark installed and only while you press Start. It shows what your traffic is made of, who you talk to by name, and Wireshark's own warnings. It stops by itself." }
        };
        void PaintAbout(Graphics g)
        {
            g.TranslateTransform(aboutCanvas.AutoScrollPosition.X, aboutCanvas.AutoScrollPosition.Y);
            Begin(aboutCanvas);
            float W = aboutCanvas.ClientSize.Width, w = Math.Min(W - 64 * S, 860 * S), x = (W - w) / 2, y = 34 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            using (var blue = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            {
                Theme.Logo(g, new RectangleF(x, y, 56 * S, 56 * S), true);
                g.DrawString("AdeeboMeter", Draw.fHero, ink, x + 70 * S, y - 2 * S);
                g.DrawString("Is it me or the network? One glance tells you.", Draw.fHead, blue, x + 72 * S, y + 38 * S);
                y += 84 * S;
                y = Para(g, "When the internet feels slow, most of us open Task Manager, then a command prompt, then ping, then guess. AdeeboMeter does all of that for you - quietly, every second - and tells you in one plain sentence what's wrong and what to do about it.", x, y, w, sub, Draw.fBody);
                y = Para(g, "No cloud. No telemetry. Never listens. Every packet it sends is listed below - and you can switch each one off.", x, y, w, ink, Draw.fBodyB);

                y = AboutHead(g, "1", "The corner widget", x, y, w);
                y = Para(g, "A small card in the bottom-right corner, faded to 30% so it never distracts. It shows the one thing that matters - is everything OK - plus CPU, memory, disk and network at a glance. It never takes clicks, so whatever is underneath keeps working. When a window covers the corner it folds into a tiny chip; rest your mouse on it for half a second and it opens.", x, y, w, sub, Draw.fBody);

                y = AboutHead(g, "2", "Getting around", x, y, w);
                y = Rows(g, AboutNav, x, y, w);

                y = AboutHead(g, "3", "Is it me or the network?", x, y, w);
                y = Para(g, "The five dots at the top follow your traffic out of the building: your PC, your Wi-Fi or cable to the router, your provider (or VPN), DNS - the internet's phone book - and the internet itself. The first dot that isn't green is where the trouble starts. Everything before it is fine; everything after it is a victim, not a cause. That's the whole trick.", x, y, w, sub, Draw.fBody);

                y = AboutHead(g, "4", "What each tab does", x, y, w);
                y = Rows(g, AboutTabs, x, y, w);

                y = AboutHead(g, "5", "What leaves your PC - and what never does", x, y, w);
                Func<int, string> every = sec => sec <= 0 ? "Off" : sec >= 60 ? "every " + sec / 60 + " min" : "every " + sec + " s";
                var leaves = new List<string[]> {
                    new[] { "Router ping", Cfg.GwSec > 0 ? "A tiny ping to your own router, " + every(Cfg.GwSec) + ". Stays on your local network." : "Off.", Cfg.GwSec > 0 ? "0" : "-1" },
                    new[] { "Internet and DNS check", Cfg.NetSec > 0 ? "One ping to 1.1.1.1 (or 8.8.8.8) and one name lookup, " + every(Cfg.NetSec) + "." : "Off - nothing goes to the internet in the background.", Cfg.NetSec > 0 ? "1" : "-1" },
                    new[] { "Provider name", Cfg.NetSec > 0 ? "Once per network: a short trace (6 hops) toward 1.1.1.1 and one name lookup, to put your provider's name on the ISP dot." : "Off (follows the internet check).", Cfg.NetSec > 0 ? "1" : "-1" },
                    new[] { "Wi-Fi watch", Cfg.WifiSec > 0 ? "Asks Windows about your Wi-Fi, " + every(Cfg.WifiSec) + ". Nothing is sent anywhere." : "Off.", "-1" },
                    LeaveChecks,
                    new[] { "Sign-in page check", Cfg.Portal ? "Only if a check you started fails: one request to the same test page Windows itself uses (msftconnecttest.com)." : "Off.", Cfg.Portal ? "1" : "-1" },
                    LeaveLive,
                    LeaveNever };
                y = Rows(g, leaves, x, y, w);
                var setLink = new RectangleF(x, y, 260 * S, 22 * S);
                g.DrawString("Change any of these in Settings  >", Draw.fBodyB, blue, setLink.X, setLink.Y);
                Act(aboutCanvas, setLink, delegate { tab = "settings"; ShowTab(); });
                y += 34 * S;

                y = AboutHead(g, "6", "Built to be trusted", x, y, w);
                y = Rows(g, AboutTrust, x, y, w);

                y += 10 * S;
                using (var line = new Pen(Theme.Line)) g.DrawLine(line, x, y, x + w, y); y += 14 * S;
                g.DrawString("AdeeboMeter " + Program.Version + "  ·  made by Adeeb Khan  ·  " + Program.Tagline, Draw.fSmall, muted, x, y); y += 30 * S;
            }
            var need = new Size(0, (int)(y + 20 * S)); if (aboutCanvas.AutoScrollMinSize != need) aboutCanvas.AutoScrollMinSize = need;
        }
        static float Para(Graphics g, string t, float x, float y, float w, Brush b, Font f)
        {
            var h = Draw.Measure(t, f, (int)w).Height; g.DrawString(t, f, b, new RectangleF(x, y, w, h + 4)); return y + h + 12 * Draw.S;
        }
        static float AboutHead(Graphics g, string num, string title, float x, float y, float w)
        {
            float S = Draw.S; y += 18 * S;
            var c = new RectangleF(x, y, 26 * S, 26 * S);
            Theme.Fill(g, c, 13 * S, Theme.BlueSoft);
            using (var b = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.BlueDark)) using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(num, Draw.fBodyB, b, c, sf);
            using (var b = new SolidBrush(Theme.Ink)) g.DrawString(title, Draw.fTitle, b, x + 38 * S, y + 1 * S);
            return y + 38 * S;
        }
        static float Rows(Graphics g, IEnumerable<string[]> rows, float x, float y, float w)
        {
            float S = Draw.S, lw = 190 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
                foreach (var r in rows)
                {
                    float tx = x, h = Draw.Measure(r[1], Draw.fBody, (int)(w - lw)).Height;
                    if (r.Length > 2) { int lv = int.Parse(r[2], CultureInfo.InvariantCulture); using (var d = new SolidBrush(lv < 0 ? Theme.Grey : Theme.Lv(lv))) g.FillEllipse(d, x, y + 5 * S, 9 * S, 9 * S); tx += 16 * S; }
                    g.DrawString(r[0], Draw.fBodyB, ink, new RectangleF(tx, y, lw - (tx - x) - 8 * S, 40 * S));
                    g.DrawString(r[1], Draw.fBody, sub, new RectangleF(x + lw, y, w - lw, h + 4));
                    y += Math.Max(h, 20 * S) + 10 * S;
                    g.DrawLine(line, x, y - 5 * S, x + w, y - 5 * S);
                }
            return y + 4 * S;
        }

        float PaintStory(Graphics g, DestResult d, float x, float y, float w)
        {
            var seg = d.Story ?? new List<string[]>(); var find = d.Findings ?? new List<string[]>();
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(seg.Count > 0 ? "THE PATH STORY  ·  WHO CARRIES YOUR TRAFFIC, FROM ROUTER NAMES" : "WHAT WE FOUND", Draw.fTiny, mb, x, y);
            Hint(destCanvas, new RectangleF(x, y, w, 16 * S), "card.PATH STORY"); y += 22 * S;
            // chain of boxes, wrapping to the next line when needed
            float bx = x, bh = seg.Count > 0 ? 52 * S : -14 * S, gap = 26 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var arrow = new Pen(Theme.Rail, 2 * S))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                for (int i = 0; i < seg.Count; i++)
                {
                    var sgm = seg[i];
                    float bw = Math.Min(230 * S, Math.Max(g.MeasureString(sgm[0], Draw.fBodyB).Width, g.MeasureString(sgm[1], Draw.fSmall).Width) + 30 * S);
                    if (bx + bw > x + w && bx > x) { bx = x; y += bh + 14 * S; }
                    var br = new RectangleF(bx, y, bw, bh);
                    Color fill = sgm[2] == "you" || sgm[2] == "dest" ? Theme.BlueSoft : sgm[2] == "cgnat" ? Color.FromArgb(40, Theme.Amber) : sgm[2] == "hidden" ? Theme.Soft : Theme.Soft;
                    Theme.Fill(g, br, 10 * S, fill); Theme.Stroke(g, br, 10 * S, sgm[2] == "dest" || sgm[2] == "you" ? Theme.Blue : Theme.Line, 1);
                    g.DrawString(sgm[0], Draw.fBodyB, ink, new RectangleF(br.X + 12 * S, br.Y + 8 * S, bw - 20 * S, 18 * S), nowrap);
                    g.DrawString(sgm[1], Draw.fSmall, muted, new RectangleF(br.X + 12 * S, br.Y + 28 * S, bw - 20 * S, 16 * S), nowrap);
                    string key = sgm[2] == "lan" ? "story.lan" : sgm[2] == "cgnat" ? "story.cgnat" : sgm[2] == "hidden" ? "story.hidden" : sgm[2] == "net" ? "story.net" : null;
                    if (key != null) Hint(destCanvas, br, key);
                    bx += bw;
                    if (i < seg.Count - 1 && bx + gap < x + w) { g.DrawLine(arrow, bx + 4 * S, y + bh / 2, bx + gap - 6 * S, y + bh / 2); g.DrawLine(arrow, bx + gap - 11 * S, y + bh / 2 - 4 * S, bx + gap - 6 * S, y + bh / 2); g.DrawLine(arrow, bx + gap - 11 * S, y + bh / 2 + 4 * S, bx + gap - 6 * S, y + bh / 2); }
                    bx += gap;
                }
            y += bh + 14 * S;
            using (var sub = new SolidBrush(Theme.Sub))
                foreach (var f in find)
                {
                    int lv = int.Parse(f[1], CultureInfo.InvariantCulture);
                    var h = Draw.Measure(f[0], Draw.fBody, (int)(w - 22 * S)).Height;
                    using (var dot = new SolidBrush(lv < 0 ? Theme.Blue : Theme.Lv(lv))) g.FillEllipse(dot, x + 2 * S, y + 6 * S, 8 * S, 8 * S);
                    using (var tb = new SolidBrush(lv == 2 ? Theme.LvText(2) : Theme.Sub)) g.DrawString(f[0], Draw.fBody, tb, new RectangleF(x + 20 * S, y, w - 22 * S, h + 4));
                    y += h + 8 * S;
                }
            return y + 14 * S;
        }

        // ---------- Your connection
        void RefreshLink(bool now)
        {
            if (linkBusy || (!now && (DateTime.UtcNow - lastLink).TotalSeconds < 10)) return;
            linkBusy = true; lastLink = DateTime.UtcNow;
            Task.Factory.StartNew(() =>
            {
                if (IsDisposed) { linkBusy = false; return; }
                LinkSnap ls = null; try { ls = LinkInfo.Collect(M); } catch (Exception e) { Program.Log("link: " + e.Message); }
                if (IsDisposed || !IsHandleCreated) { linkBusy = false; return; }
                try { BeginInvoke((Action)delegate { linkBusy = false; if (ls != null && !IsDisposed) { linkSnap = ls; linkCanvas.Invalidate(); } }); } catch { linkBusy = false; }
            });
        }
        float RowsCard(Graphics g, string title, List<string[]> rows, RectangleF cr, string keyPrefix)
        {
            Theme.Stroke(g, cr, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(title, Draw.fTiny, mb, cr.X + 14 * S, cr.Y + 12 * S);
            Hint(linkCanvas, new RectangleF(cr.X, cr.Y, cr.Width, 26 * S), keyPrefix + title);
            float ry = cr.Y + 34 * S, lw = Math.Min(170 * S, cr.Width * 0.42f);
            using (var muted = new SolidBrush(Theme.Muted))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                foreach (var row in rows)
                {
                    g.DrawString(row[0], Draw.fBody, muted, new RectangleF(cr.X + 14 * S, ry, lw, 20 * S), nowrap);
                    using (var vb = new SolidBrush(row.Length > 2 && row[2] != "0" ? Theme.LvText(int.Parse(row[2], CultureInfo.InvariantCulture)) : Theme.Ink))
                        g.DrawString(row[1], Draw.fBodyB, vb, new RectangleF(cr.X + 14 * S + lw, ry, cr.Width - lw - 28 * S, 20 * S), nowrap);
                    Hint(linkCanvas, new RectangleF(cr.X, ry - 2 * S, cr.Width, 22 * S), "link." + row[0]);
                    ry += 22 * S;
                }
            return ry;
        }
        void PaintLink(Graphics g)
        {
            g.TranslateTransform(linkCanvas.AutoScrollPosition.X, linkCanvas.AutoScrollPosition.Y);
            Begin(linkCanvas);
            float x = 14 * S, y = 6 * S, w = linkCanvas.ClientSize.Width - 28 * S;
            var ls = linkSnap;
            if (ls == null) { using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Voice.P("Asking Windows about your connection...", "Reading..."), Draw.fBody, mb, x, y + 10 * S); return; }
            string title = (ls.Wifi ? "YOUR WI-FI" : "YOUR CABLE") + "  ·  " + Draw.Trunc(ls.Adapter, 50) + "  ·  UPDATED " + ls.At.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            float vh = Draw.Measure(ls.Verdict, Draw.fTitle, (int)(w - 32 * S)).Height;
            var vb = new RectangleF(x, y, w, vh + 44 * S);
            Theme.Fill(g, vb, 12 * S, Theme.LvSoft(ls.Level));
            using (var b = new SolidBrush(Theme.LvText(ls.Level))) { g.DrawString(title, Draw.fTiny, b, x + 16 * S, y + 10 * S); g.DrawString(ls.Verdict, Draw.fTitle, b, new RectangleF(x + 16 * S, y + 26 * S, w - 32 * S, vh + 4)); }
            y += vb.Height + 16 * S;
            if (ls.Radio.Count + ls.Address.Count + ls.Traffic.Count == 0) return;   // nothing connected: the message says it all
            float cw = (w - 14 * S) / 2;
            Func<List<string[]>, float> hOf = rows => 44 * S + rows.Count * 22 * S;
            float h1 = Math.Max(hOf(ls.Radio), hOf(ls.Quality)), h2 = Math.Max(hOf(ls.Address), hOf(ls.Traffic));
            RowsCard(g, ls.Wifi ? "RADIO" : "CABLE", ls.Radio, new RectangleF(x, y, cw, h1), "link.");
            RowsCard(g, "LINK TO YOUR ROUTER", ls.Quality, new RectangleF(x + cw + 14 * S, y, cw, h1), "link."); y += h1 + 14 * S;
            RowsCard(g, "ADDRESSES", ls.Address, new RectangleF(x, y, cw, h2), "link.");
            RowsCard(g, "TRAFFIC SINCE THE ADAPTER STARTED", ls.Traffic, new RectangleF(x + cw + 14 * S, y, cw, h2), "link."); y += h2 + 18 * S;

            if (!ls.Wifi) y = PaintSwitch(g, x, y, w);
            if (ls.Wifi) y = ListCard(g, "WI-FI DROPS IN THE LAST 24 HOURS  ·  FROM WINDOWS' OWN LOG", ls.Drops, ls.DropsErr != "" ? ls.DropsErr : Voice.P("Not a single drop. Rock solid.", "No drops in 24 h."), "link.DROPS", x, y, w);
            y = ListCard(g, "ADDRESS (DHCP) TROUBLE IN THE LAST 24 HOURS", ls.Dhcp, ls.DhcpErr != "" ? ls.DhcpErr : "None - your address renewed without a hitch.", "link.DHCP", x, y, w);
            y = ListCard(g, "DEVICES ON YOUR NETWORK  ·  THE ONES YOUR PC HAS TALKED TO (NOTHING IS SCANNED)", ls.Lan.Select(r => new[] { r[0], r[1] + (r[2] != "" ? "  ·  " + r[2] : ""), r[2] == "your router" ? "-1" : "0" }).ToList(), "None in Windows' table yet.", "link.LAN", x, y, w);
            if (ls.Wifi && ls.MyChannel > 0 && ls.Aps.Count > 0) y = PaintChannelMap(g, ls, x, y, w);
            else if (ls.Wifi && ls.Aps.Count == 0 && !ls.LocationBlocked)
                using (var mb = new SolidBrush(Theme.Muted)) { g.DrawString("Windows didn't list any neighbouring networks this time - it refreshes its scan every minute or so.", Draw.fBody, mb, x, y); y += 30 * S; }
            if (!ls.Wifi)
            {
                string tip = Voice.P("Cable is the gold standard: no interference, no roaming, no neighbours. If calls still stutter, the problem is further out - check a destination.", "Wired links have no radio interference; look further along the path if problems persist.");
                var th = Draw.Measure(tip, Draw.fBody, (int)w).Height;
                using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(tip, Draw.fBody, sb, new RectangleF(x, y, w, th + 4)); y += th + 20 * S;
            }
            var need = new Size(0, (int)(y + 20 * S)); if (linkCanvas.AutoScrollMinSize != need) linkCanvas.AutoScrollMinSize = need;
        }
        float ListCard(Graphics g, string title, List<string[]> rows, string empty, string key, float x, float y, float w)
        {
            float rh = 22 * S; var card = new RectangleF(x, y, w, 40 * S + Math.Max(1, rows.Count) * rh);
            Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(title, Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(linkCanvas, new RectangleF(x, y, w, 26 * S), key);
            float ry = y + 34 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (rows.Count == 0) g.DrawString(empty, Draw.fBody, muted, new RectangleF(x + 14 * S, ry, w - 28 * S, 20 * S), nowrap);
                foreach (var r in rows)
                {
                    int lv = r.Length > 2 ? int.Parse(r[2], CultureInfo.InvariantCulture) : 0;
                    using (var d = new SolidBrush(lv < 0 ? Theme.Blue : Theme.Lv(lv))) g.FillEllipse(d, x + 14 * S, ry + 6 * S, 8 * S, 8 * S);
                    g.DrawString(r[0], Draw.fBodyB, ink, new RectangleF(x + 30 * S, ry, w * 0.30f, 20 * S), nowrap);
                    g.DrawString(r[1], Draw.fBody, muted, new RectangleF(x + 30 * S + w * 0.31f, ry, w * 0.69f - 44 * S, 20 * S), nowrap);
                    ry += rh;
                }
            }
            return y + card.Height + 14 * S;
        }
        float PaintSwitch(Graphics g, float x, float y, float w)
        {
            var card = new RectangleF(x, y, w, switchInfo != null ? 150 * S : 86 * S);
            Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString("YOUR SWITCH PORT  ·  LLDP / CDP, LISTENED FOR PASSIVELY", Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(linkCanvas, new RectangleF(x, y, w, 26 * S), "link.SWITCH");
            var btn = new RectangleF(x + 14 * S, y + 36 * S, 210 * S, 32 * S);
            bool can = LiveCapture.Available && !switchBusy;
            Theme.Fill(g, btn, 16 * S, can ? Theme.Blue : Theme.Track);
            string bt = switchBusy ? "Listening... " + (int)(DateTime.UtcNow - switchStart).TotalSeconds + " s" : "Find my switch port";
            using (var bb = new SolidBrush(can ? Color.White : Theme.Muted)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(bt, Draw.fBodyB, bb, btn, c);
            if (can) Act(linkCanvas, btn, StartSwitch);
            string note = !LiveCapture.Available ? "Needs Wireshark and Npcap on this PC." : switchErr != "" ? switchErr : "Switches announce themselves every 30-60 s. I listen for up to 65 s - nothing is sent.";
            using (var nb = new SolidBrush(switchErr != "" ? Theme.LvText(1) : Theme.Muted)) g.DrawString(note, Draw.fSmall, nb, new RectangleF(btn.Right + 14 * S, btn.Y, w - btn.Width - 56 * S, 34 * S));
            var si = switchInfo;
            if (si != null)
            {
                float ry = y + 82 * S;
                var rows = new[] { new[] { "Switch", si.Name + (si.Mgmt != "" ? "  (" + si.Mgmt + ")" : "") }, new[] { "Port", si.Port + (si.PortDesc != "" && si.PortDesc != si.Port ? "  ·  " + si.PortDesc : "") }, new[] { "VLAN", si.Vlan != "" ? si.Vlan : "not announced" }, new[] { "Model", (si.Model != "" ? si.Model : "-") + "  ·  via " + si.Proto } };
                using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    for (int i = 0; i < rows.Length; i++)
                    {
                        float cx = x + 14 * S + (i % 2) * (w / 2), cy = ry + (i / 2) * 26 * S;
                        g.DrawString(rows[i][0], Draw.fBody, muted, cx, cy);
                        g.DrawString(rows[i][1], Draw.fBodyB, ink, new RectangleF(cx + 70 * S, cy, w / 2 - 100 * S, 20 * S), nowrap);
                    }
            }
            return y + card.Height + 14 * S;
        }
        void StartSwitch()
        {
            if (switchBusy) return;
            string iface; lock (M.L) iface = M.IfaceId;
            switchBusy = true; switchErr = ""; switchInfo = null; switchStart = DateTime.UtcNow; linkCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            {
                string err; var si = LiveCapture.FindSwitch(iface, 65, out err);
                try { BeginInvoke((Action)delegate { switchBusy = false; switchInfo = si; switchErr = err; linkCanvas.Invalidate(); }); } catch { switchBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }
        static readonly string[] Legend = { "your channel", "overlaps yours", "other channels (bar = strongest signal, number = access points)" };
        float PaintChannelMap(Graphics g, LinkSnap ls, float x, float y, float w)
        {
            string band = ls.MyBand;
            var inBand = ls.Aps.Where(a => a.Band == band && a.Channel > 0).ToList();
            var chans = inBand.Select(a => a.Channel).Concat(new[] { ls.MyChannel }).Distinct().OrderBy(c => c).ToList();
            float ch = 170 * S; var card = new RectangleF(x, y, w, ch + 70 * S);
            Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString("WHO ELSE IS ON THE AIR  ·  " + band.ToUpperInvariant() + "  ·  " + inBand.Count + " ACCESS POINTS NEARBY", Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(linkCanvas, new RectangleF(x, y, w, 26 * S), "link.CHANNEL MAP");
            float plotX = x + 20 * S, plotW = w - 40 * S, base0 = y + 34 * S + ch - 30 * S, colW = Math.Min(64 * S, plotW / Math.Max(1, chans.Count));
            using (var axis = new Pen(Theme.Line)) g.DrawLine(axis, plotX, base0, plotX + plotW, base0);
            using (var muted = new SolidBrush(Theme.Muted)) using (var center = new StringFormat { Alignment = StringAlignment.Center })
                for (int i = 0; i < chans.Count; i++)
                {
                    int c = chans[i]; var here = inBand.Where(a => a.Channel == c).ToList();
                    bool mine = c == ls.MyChannel, overlap = !mine && LinkInfo.Overlaps(c, ls.MyChannel, band);
                    int best = here.Count > 0 ? here.Max(a => a.Signal) : 0;
                    float cx = plotX + i * colW + colW / 2, bh = Math.Max(4 * S, (ch - 50 * S) * best / 100f);
                    var bar = new RectangleF(cx - colW * 0.3f, base0 - bh, colW * 0.6f, bh);
                    Theme.Fill(g, bar, 5 * S, mine ? Theme.Blue : overlap ? Theme.Amber : Theme.Rail);
                    g.DrawString(here.Count.ToString(CultureInfo.CurrentCulture), Draw.fSmall, muted, new RectangleF(cx - colW / 2, bar.Y - 17 * S, colW, 16 * S), center);
                    using (var lb = new SolidBrush(mine ? Theme.Blue : Theme.Ink)) g.DrawString(c.ToString(CultureInfo.CurrentCulture), mine ? Draw.fBodyB : Draw.fSmall, lb, new RectangleF(cx - colW / 2, base0 + 4 * S, colW, 18 * S), center);
                    Hint(linkCanvas, new RectangleF(cx - colW / 2, y + 30 * S, colW, ch), "link.CHANNEL MAP");
                }
            float ly = y + ch + 36 * S, lx = x + 20 * S;
            var colors = new[] { Theme.Blue, Theme.Amber, Theme.Rail };
            using (var muted = new SolidBrush(Theme.Muted))
                for (int i = 0; i < 3; i++)
                { Theme.Fill(g, new RectangleF(lx, ly + 3 * S, 10 * S, 10 * S), 3 * S, colors[i]); g.DrawString(Legend[i], Draw.fSmall, muted, lx + 16 * S, ly); lx += g.MeasureString(Legend[i], Draw.fSmall).Width + 34 * S; }
            y += card.Height + 16 * S;

            var top = ls.Aps.OrderByDescending(a => a.Signal).Take(12).ToList();
            float rh = 24 * S; var card2 = new RectangleF(x, y, w, 40 * S + top.Count * rh);
            Theme.Stroke(g, card2, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString("STRONGEST NEIGHBOURS", Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(linkCanvas, new RectangleF(x, y, w, 26 * S), "link.NEIGHBOURS");
            float ry = y + 34 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                foreach (var a in top)
                {
                    bool me = string.Equals(a.Bssid, ls.MyBssid, StringComparison.OrdinalIgnoreCase);
                    bool clash = !me && a.Band == band && LinkInfo.Overlaps(a.Channel, ls.MyChannel, band);
                    using (var d = new SolidBrush(me ? Theme.Blue : clash ? Theme.Amber : Theme.Grey)) g.FillEllipse(d, x + 14 * S, ry + 6 * S, 8 * S, 8 * S);
                    g.DrawString(a.Ssid + (me ? "  (you)" : ""), me ? Draw.fBodyB : Draw.fBody, ink, new RectangleF(x + 30 * S, ry, w * 0.30f, 20 * S), nowrap);
                    g.DrawString(a.Band + " · ch " + a.Channel + (a.Radio != "" ? " · " + LinkInfo.Gen(a.Radio) : ""), Draw.fSmall, muted, new RectangleF(x + 30 * S + w * 0.31f, ry + 2 * S, w * 0.30f, 18 * S), nowrap);
                    float bx = x + w * 0.66f, bw = w * 0.20f;
                    Theme.Fill(g, new RectangleF(bx, ry + 7 * S, bw, 6 * S), 3 * S, Theme.Track);
                    Theme.Fill(g, new RectangleF(bx, ry + 7 * S, bw * Math.Max(0, a.Signal) / 100f, 6 * S), 3 * S, me ? Theme.Blue : clash ? Theme.Amber : Theme.Rail);
                    g.DrawString(a.Signal + "%" + (a.Rssi < 0 ? " · " + a.Rssi + " dBm" : "") + (a.Width > 20 ? " · " + a.Width + " MHz" : "") + (a.Util >= 0 ? " · air " + a.Util + "%" : ""), Draw.fSmall, muted, bx + bw + 10 * S, ry + 2 * S);
                    ry += rh;
                }
            return y + card2.Height + 16 * S;
        }

        // ---------- Saved destinations
        static string Norm(string t) { return (t ?? "").Trim(); }
        void SyncPinBtn()
        {
            string t = Norm(destBox.Box.Text); bool pinned = Cfg.Pins.Contains(t, StringComparer.OrdinalIgnoreCase);
            pinBtn.Text = pinned ? "Unpin" : Cfg.Pins.Count >= 5 ? "Saved list is full (5)" : "Pin";
            pinBtn.Enabled = pinned || (Cfg.Pins.Count < 5 && t != "");
        }
        void TogglePin()
        {
            string t = Norm(destBox.Box.Text);
            int i = Cfg.Pins.FindIndex(p => string.Equals(p, t, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) Cfg.Pins.RemoveAt(i);
            else
            {
                string h; int pt; bool tl; string err;
                if (Cfg.Pins.Count >= 5 || !DestCheck.Parse(t, out h, out pt, out tl, out err)) { SyncPinBtn(); return; }
                Cfg.Pins.Add(t);
            }
            Cfg.Save(); BuildPinBar(); SyncPinBtn();
        }
        void BuildPinBar()
        {
            pinBar.SuspendLayout();
            foreach (Control c in pinBar.Controls.Cast<Control>().ToList()) { pinBar.Controls.Remove(c); c.Dispose(); }   // no leaked handles
            if (Cfg.Pins.Count > 0)
            {
                pinBar.Controls.Add(new Label { Text = "SAVED", AutoSize = true, Font = Draw.fTiny, ForeColor = Theme.Muted, BackColor = Theme.Card, Margin = new Padding(0, Sc(10), Sc(8), 0) });
                foreach (var p in Cfg.Pins)
                {
                    var b = new PillButton(Draw.Trunc(p, 28), PillButton.K.Chip) { Tag = p, Margin = new Padding(0, Sc(2), Sc(6), 0) };
                    b.Click += delegate(object s, EventArgs e) { destBox.Box.Text = (string)((Control)s).Tag; StartDest(); };
                    pinBar.Controls.Add(b);
                }
                var all = new PillButton(Cfg.Pins.Count == 1 ? "Run it" : "Run all " + Cfg.Pins.Count, PillButton.K.Primary) { Margin = new Padding(Sc(6), 0, 0, 0) };
                all.Height = Sc(32); all.Click += delegate { RunAllPins(); };
                pinBar.Controls.Add(all);
            }
            pinBar.Visible = Cfg.Pins.Count > 0;
            pinBar.ResumeLayout();
            Recolor(pinBar);
        }
        void RunAllPins()
        {
            if (pinRunning || Cfg.Pins.Count == 0) return;
            var list2 = Cfg.Pins.Select(p => new DestResult { Input = p, Target = p }).ToList();
            pinResults = list2; dest = null; pinRunning = true; destCanvas.Invalidate();
            var tasks = list2.Select(r => Task.Factory.StartNew(() =>
            {
                try { DestCheck.Run(r.Input, r, delegate { }, M); }
                catch (Exception e) { r.Verdict = "Check failed: " + e.Message; r.Level = 1; r.Done = true; Program.Log("pin: " + e.Message); }
            }, TaskCreationOptions.LongRunning)).ToArray();
            Task.Factory.ContinueWhenAll(tasks, delegate { try { BeginInvoke((Action)delegate { pinRunning = false; destCanvas.Invalidate(); }); } catch { } });
        }
        float PaintPinTable(Graphics g, List<DestResult> pr, float x, float y, float w)
        {
            float rh = 40 * S, h = 34 * S + pr.Count * rh;
            var card = new RectangleF(x, y, w, h); Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var line = new Pen(Theme.Line2))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString("SAVED DESTINATIONS" + (pinRunning ? "  ·  CHECKING ALL AT ONCE..." : "  ·  CHECKED " + pr[0].At.ToString("HH:mm", CultureInfo.CurrentCulture)), Draw.fTiny, muted, x + 14 * S, y + 12 * S);
                float yy = y + 32 * S, dotsW = 8 * 14 * S;
                foreach (var r in pr)
                {
                    var row = new RectangleF(x + 1, yy, w - 2, rh);
                    if (r == dest) using (var b = new SolidBrush(Theme.RowSel)) g.FillRectangle(b, row);
                    bool done = r.Done;
                    if (done) using (var b = new SolidBrush(Theme.Lv(r.Level))) g.FillEllipse(b, x + 14 * S, yy + rh / 2 - 6 * S, 12 * S, 12 * S);
                    else using (var p = new Pen(Theme.Blue, 2 * S)) g.DrawEllipse(p, x + 14 * S, yy + rh / 2 - 6 * S, 12 * S, 12 * S);
                    g.DrawString(r.Input, Draw.fBodyB, ink, new RectangleF(x + 36 * S, yy + 11 * S, 220 * S, 20 * S), nowrap);
                    float dx = x + 266 * S;
                    for (int i = 0; i < 8; i++)
                        using (var b = new SolidBrush(r.Lv[i] < 0 ? Theme.Track : Theme.Lv(r.Lv[i]))) g.FillEllipse(b, dx + i * 14 * S, yy + rh / 2 - 4 * S, 8 * S, 8 * S);
                    string v = done ? r.Verdict : r.Note ?? "checking...";
                    using (var vb = new SolidBrush(done ? Theme.LvText(r.Level) : Theme.Sub)) g.DrawString(v, Draw.fBody, vb, new RectangleF(dx + dotsW + 12 * S, yy + 11 * S, w - (dx - x) - dotsW - 26 * S, 20 * S), nowrap);
                    g.DrawLine(line, x + 1, yy + rh, x + w - 1, yy + rh);
                    var captured = r; Act(destCanvas, row, delegate { dest = captured; destBox.Box.Text = captured.Input; destCanvas.Invalidate(); });
                    yy += rh;
                }
            }
            return y + h + 16 * S;
        }

        // ---------- actions
        void StartDest()
        {
            if (destRunning) return;
            string input = destBox.Box.Text;
            dest = new DestResult(); pinResults = null; destRunning = true; checkBtn.Enabled = false; destCanvas.Invalidate();
            var r = dest;
            Task.Factory.StartNew(() =>
            {
                try { DestCheck.Run(input, r, delegate { }, M); }
                catch (Exception e) { r.Verdict = "Check failed: " + e.Message; r.Level = 1; r.Done = true; Program.Log("dest: " + e); }
                finally
                {
                    destRunning = false;
                    try { if (!IsDisposed) BeginInvoke((Action)delegate { checkBtn.Enabled = true; destCanvas.Invalidate(); }); } catch { }
                }
            }, TaskCreationOptions.LongRunning);
        }

        void StartHealth()
        {
            if (healthRunning) return;
            healthRunning = true; health = null; healthBtn.Enabled = false; healthCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            {
                List<string[]> res = null;
                try { res = HealthCheck.Run(M); } catch (Exception e) { Program.Log("health: " + e); }
                try { if (!IsDisposed) BeginInvoke((Action)delegate { health = res ?? new List<string[]>(); healthRunning = false; healthBtn.Enabled = true; healthBtn.Text = "Run again"; healthCanvas.Invalidate(); }); } catch { }
            }, TaskCreationOptions.LongRunning);
        }

        void Sniff(Conn c)
        {
            if (!npcap || sniffing || c == null) return;
            if (live.RingActive && !string.IsNullOrEmpty(c.RemoteIp) && c.RemotePort > 0)
            {
                IPAddress ip; if (!IPAddress.TryParse(c.RemoteIp, out ip)) return;
                string l4 = c.Proto.StartsWith("UDP", StringComparison.Ordinal) ? "udp" : "tcp";
                string bpf = "host " + ip + " and port " + c.RemotePort + " and port " + c.LocalPort;
                string df = (ip.AddressFamily == AddressFamily.InterNetworkV6 ? "ipv6.addr==" : "ip.addr==") + ip + " && " + l4 + ".port==" + c.RemotePort + " && " + l4 + ".port==" + c.LocalPort;
                sniffing = true; sniffStatus = Voice.P("Rewinding the tape...", "Collecting..."); UpdateConnStatus();
                Task.Factory.StartNew(() => live.SniffWithHistory(c, bpf, df, delegate(string st) { try { BeginInvoke((Action)delegate { sniffStatus = st; UpdateConnStatus(); }); } catch { } }), TaskCreationOptions.LongRunning)
                    .ContinueWith(t => { try { BeginInvoke((Action)delegate { sniffing = false; }); } catch { } });
                return;
            }
            string filt; IPAddress rip;
            if (!string.IsNullOrEmpty(c.RemoteIp) && IPAddress.TryParse(c.RemoteIp, out rip) && c.RemotePort > 0)
                filt = "host " + rip + " and port " + c.RemotePort + " and port " + c.LocalPort;
            else filt = (c.Proto == "UDP" ? "udp" : "tcp") + " port " + c.LocalPort;
            string iface; lock (M.L) iface = M.IfaceId;
            Guid gid; if (!Guid.TryParse((iface ?? "").Trim('{', '}'), out gid)) { sniffStatus = "No active adapter to capture on."; UpdateConnStatus(); return; }
            string safeApp = new string(c.Proc.Where(char.IsLetterOrDigit).Take(20).ToArray());
            string file = Program.SafeCapturePath("adebometer_" + safeApp + "_" + DateTime.Now.ToString("HHmmss", CultureInfo.CurrentCulture) + ".pcapng");
            var psi = new ProcessStartInfo(dumpcap, "-i \"\\Device\\NPF_{" + gid.ToString().ToUpperInvariant() + "}\" -f \"" + filt + "\" -a duration:" + Cfg.SniffSec + " -w \"" + file + "\" -q");
            psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardError = true;
            try
            {
                var p = Process.Start(psi); Job.Add(p);
                sniffing = true; sniffStatus = Voice.P("Listening in for 30 s on ", "Capturing 30 s: ") + filt;
                UpdateConnStatus();
                var err = new StringBuilder();
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
                p.BeginErrorReadLine(); p.EnableRaisingEvents = true;
                p.Exited += delegate
                {
                    try
                    {
                        if (IsDisposed) return;
                        BeginInvoke((Action)delegate
                        {
                            sniffing = false;
                            if (File.Exists(file) && new FileInfo(file).Length > 0)
                            {
                                sniffStatus = Voice.P("Got it - opening in Wireshark. (Deleted when AdeeboMeter exits; Save As in Wireshark to keep it.)", "Saved; opening in Wireshark. Deleted on exit unless saved elsewhere.");
                                try { if (File.Exists(wireshark)) { var ws = new ProcessStartInfo(wireshark, "\"" + file + "\""); ws.UseShellExecute = false; Process.Start(ws).Dispose(); } } catch (Exception ex) { sniffStatus = "Saved " + file + " (" + ex.Message + ")"; }
                            }
                            else { string e2; lock (err) e2 = err.ToString().Trim(); sniffStatus = "Capture failed: " + Draw.Trunc(e2.Replace(Environment.NewLine, " "), 120); Program.Log("dumpcap: " + e2); }
                            UpdateConnStatus();
                        });
                    }
                    catch { }
                };
            }
            catch (Exception ex) { sniffing = false; sniffStatus = "Could not start dumpcap: " + ex.Message; UpdateConnStatus(); }
        }
    }

    // ================================================================== the app: widget, tray, hover
    sealed class AppCtx : ApplicationContext
    {
        readonly Monitor M = new Monitor();
        readonly LayeredForm widget = new LayeredForm(), detailWin = new LayeredForm();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly System.Windows.Forms.Timer ui = new System.Windows.Forms.Timer();
        readonly Control marshal = new Control();
        readonly Icon appIcon;
        FullView full;
        readonly Dictionary<string, RectangleF> hits = new Dictionary<string, RectangleF>();
        bool covered, hovered, revealed, widgetOn = true, autoStart = true; volatile bool quitting;
        DateTime dwellStart = DateTime.MinValue; double dwell;
        DateTime lastCover = DateTime.MinValue;
        string hoverKey = ""; int uiTick, drawnVersion = -1; string drawnKey = "", lastChip = ""; int lastLevel = -1;
        ToolStripMenuItem miWidget, miAuto, miSerious;
        readonly string settingsPath;
        EventWaitHandle openEvt, ackEvt, quitEvt;
        HotKey hotkey; UserPreferenceChangedEventHandler prefHandler;
        readonly bool forcedByArgs = Program.ForceTheme >= 0;
        ToolStripMenuItem miThemeAuto, miThemeLight, miThemeDark;
        static byte FADE { get { return (byte)Cfg.Fade; } }      // 30 % by default (Settings)
        static int DWELL_MS { get { return Cfg.DwellMs; } }     // rest the mouse this long on the folded chip to open it

        public AppCtx()
        {
            float sc; using (var g = Graphics.FromHwnd(IntPtr.Zero)) sc = g.DpiX / 96f;
            Draw.Init(sc);
            appIcon = Theme.MakeIcon(32);
            marshal.CreateControl(); var h = marshal.Handle;
            settingsPath = Path.Combine(Program.Dir, "AdeeboMeter.settings");
            try { string old = Path.Combine(Program.Dir, OldName + ".settings"); if (!File.Exists(settingsPath) && File.Exists(old)) File.Copy(old, settingsPath); } catch (Exception e) { Program.Log("settings migrate: " + e.Message); }
            Cfg.ApplyPreset("balanced");
            LoadSettings(); ApplyAutoStart();
            Cfg.Save = SaveSettings; Cfg.SetTheme = SetTheme; Cfg.GetTheme = () => Program.ForceTheme;
            Cfg.SetAutoStart = delegate(bool on) { autoStart = on; ApplyAutoStart(); SaveSettings(); }; Cfg.GetAutoStart = () => autoStart;
            Theme.Apply(Program.WantDark());
            Program.CleanCaptures();
            M.Start();

            var menu = new ContextMenuStrip();
            var miFull = new ToolStripMenuItem("Open engineer view"); miFull.Font = new Font(miFull.Font, FontStyle.Bold); miFull.Click += delegate { OpenFull(); };
            var miSettings = new ToolStripMenuItem("Settings..."); miSettings.Click += delegate { OpenFull(); if (full != null) full.ShowPage("settings"); };
            miWidget = new ToolStripMenuItem("Hide widget (keep tray icon)"); miWidget.Click += delegate { widgetOn = !widgetOn; SaveSettings(); drawnKey = ""; };
            miSerious = new ToolStripMenuItem("Serious mode (no jokes)"); miSerious.Click += delegate { Voice.Serious = !Voice.Serious; SaveSettings(); drawnKey = ""; };
            var miTheme = new ToolStripMenuItem("Theme");
            miThemeAuto = new ToolStripMenuItem("Follow Windows"); miThemeLight = new ToolStripMenuItem("Light"); miThemeDark = new ToolStripMenuItem("Dark");
            miThemeAuto.Click += delegate { SetTheme(-1); }; miThemeLight.Click += delegate { SetTheme(0); }; miThemeDark.Click += delegate { SetTheme(1); };
            miTheme.DropDownItems.Add(miThemeAuto); miTheme.DropDownItems.Add(miThemeLight); miTheme.DropDownItems.Add(miThemeDark);
            miAuto = new ToolStripMenuItem("Start with Windows"); miAuto.Click += delegate { autoStart = !autoStart; ApplyAutoStart(); SaveSettings(); };
            var miRemove = new ToolStripMenuItem("Uninstall AdeeboMeter...");
            miRemove.Click += delegate { try { using (Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--uninstall") { UseShellExecute = false })) { } } catch (Exception e) { Program.Log("uninstall: " + e.Message); } };
            var miExit = new ToolStripMenuItem("Exit AdeeboMeter"); miExit.ForeColor = Theme.Red; miExit.Click += delegate { Quit(); };
            menu.Items.Add(new ToolStripLabel("AdeeboMeter " + Program.Version + " · " + Program.Tagline) { ForeColor = Theme.Muted });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miFull); menu.Items.Add(miSettings); menu.Items.Add(miWidget); menu.Items.Add(miTheme); menu.Items.Add(miSerious); menu.Items.Add(miAuto); menu.Items.Add(new ToolStripSeparator()); if (Setup.RunningInstalled) menu.Items.Add(miRemove); menu.Items.Add(miExit);
            menu.Opening += delegate { miWidget.Text = widgetOn ? "Hide widget (keep tray icon)" : "Show widget"; miAuto.Checked = autoStart; miSerious.Checked = Voice.Serious;
                miThemeAuto.Checked = Program.ForceTheme < 0; miThemeLight.Checked = Program.ForceTheme == 0; miThemeDark.Checked = Program.ForceTheme == 1; };

            tray.Icon = Theme.MakeIcon(Math.Max(16, (int)(16 * sc)));
            tray.Text = "AdeeboMeter"; tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) OpenFull(); };
            tray.Visible = true;

            widget.Show(); detailWin.Show(); widget.Vanish(); detailWin.Vanish();

            // Ctrl+Alt+A opens / closes the engineer view from anywhere
            try { hotkey = new HotKey(ToggleFull); if (!hotkey.Ok) Program.Log("Ctrl+Alt+A is taken by another app"); } catch (Exception e) { Program.Log("hotkey: " + e.Message); }
            miFull.ShortcutKeyDisplayString = hotkey != null && hotkey.Ok ? "Ctrl+Alt+A" : "(Ctrl+Alt+A taken)";

            // Light / dark follows Windows and switches live
            prefHandler = delegate
            {
                bool d = Program.WantDark();
                if (d != Theme.Dark) { Theme.Apply(d); drawnKey = ""; if (full != null && !full.IsDisposed) full.ApplyTheme(); }
            };
            SystemEvents.UserPreferenceChanged += prefHandler;
            ui.Interval = 50; ui.Tick += delegate { OnUi(); }; ui.Start();

            // Second launch -> open the engineer view here. The ack is sent from the UI thread, so a hung UI is detected as hung.
            try
            {
                openEvt = new EventWaitHandle(false, EventResetMode.AutoReset, Single.OpenName);
                ackEvt = new EventWaitHandle(false, EventResetMode.ManualReset, Single.AckName);
                quitEvt = new EventWaitHandle(false, EventResetMode.AutoReset, Single.QuitName);   // an update or removal asks us to leave
                var t = new Thread(() =>
                {
                    var both = new WaitHandle[] { openEvt, quitEvt };
                    while (!quitting)
                    {
                        try
                        {
                            int w = WaitHandle.WaitAny(both, 1000);
                            if (quitting) break;
                            if (w == 0) marshal.BeginInvoke((Action)delegate { ackEvt.Set(); OpenFull(); });
                            else if (w == 1) { Program.Log("asked to quit (update or removal)"); marshal.BeginInvoke((Action)Quit); }
                        }
                        catch { }
                    }
                });
                t.IsBackground = true; t.Start();
            }
            catch (Exception e) { Program.Log("single: " + e.Message); }

            if (Program.StartView != null)
            {
                var once = new System.Windows.Forms.Timer { Interval = 1500 };
                once.Tick += delegate { once.Stop(); once.Dispose(); OpenFull(); if (full != null) full.Debug(Program.StartView, Program.StartTip, Program.StartDest); };
                once.Start();
            }

            // Safety net: AdeeboMeter must never listen on a port. If it ever does, it leaves.
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(5000);
                if (!quitting && Tables.OwnListener()) { Program.Log("Self-check failed: found a listening socket - exiting"); try { marshal.BeginInvoke((Action)Quit); } catch { } }
            });
        }

        // ---------- settings (written atomically: temp file + replace)
        void LoadSettings()
        {
            bool first = !File.Exists(settingsPath);
            try
            {
                if (!first)
                    foreach (var line in File.ReadAllLines(settingsPath))
                    {
                        int eq = line.IndexOf('='); if (eq <= 0) continue;
                        var kv = new[] { line.Substring(0, eq), line.Substring(eq + 1) };
                        if (kv[0] == "autostart") autoStart = kv[1] == "1";
                        else if (kv[0] == "widget") widgetOn = kv[1] == "1";
                        else if (kv[0] == "serious") Voice.Serious = kv[1] == "1";
                        else if (kv[0] == "theme" && !forcedByArgs) Program.ForceTheme = kv[1] == "dark" ? 1 : kv[1] == "light" ? 0 : -1;
                        else Cfg.Read(kv[0], kv[1]);
                    }
            }
            catch { }
            if (first) { ApplyAutoStart(); SaveSettings(); }
        }
        void SaveSettings()
        {
            try
            {
                string tmp = settingsPath + ".tmp";
                File.WriteAllLines(tmp, new[] { "autostart=" + (autoStart ? 1 : 0), "widget=" + (widgetOn ? 1 : 0), "serious=" + (Voice.Serious ? 1 : 0), "theme=" + (Program.ForceTheme == 1 ? "dark" : Program.ForceTheme == 0 ? "light" : "auto") }.Concat(Cfg.Lines()));
                if (File.Exists(settingsPath)) File.Replace(tmp, settingsPath, null); else File.Move(tmp, settingsPath);
            }
            catch (Exception e) { Program.Log("settings: " + e.Message); }
        }
        const string OldName = "Ade" + "boMeter";
        void ApplyAutoStart()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k == null) return;
                    if (k.GetValue(OldName) != null) k.DeleteValue(OldName);   // the app used to be called AdeboMeter - don't start the old copy
                    if (autoStart) k.SetValue("AdeeboMeter", "\"" + Application.ExecutablePath + "\"");
                    else if (k.GetValue("AdeeboMeter") != null) k.DeleteValue("AdeeboMeter");
                }
            }
            catch (Exception e) { Program.Log("autostart: " + e.Message); }
        }

        void OpenFull()
        {
            if (quitting) return;
            if (full == null || full.IsDisposed)
            {
                full = new FullView(appIcon, M);
                full.FormClosed += delegate
                {
                    full = null; M.ViewOpen = false; drawnKey = "";
                    // closing the view frees its memory
                    GC.Collect(); GC.WaitForPendingFinalizers();
                    try { Native.SetProcessWorkingSetSize(Native.GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1)); } catch { }
                };
            }
            M.ViewOpen = true; M.Nudge();
            full.Show(); if (full.WindowState == FormWindowState.Minimized) full.WindowState = FormWindowState.Maximized;
            full.Activate();
        }

        void SetTheme(int choice)
        {
            Program.ForceTheme = choice; SaveSettings();
            bool d = Program.WantDark();
            if (d != Theme.Dark) { Theme.Apply(d); drawnKey = ""; if (full != null && !full.IsDisposed) full.ApplyTheme(); }
        }

        void ToggleFull()
        {
            if (full != null && !full.IsDisposed && full.Visible && Form.ActiveForm == full) full.Close();
            else OpenFull();
        }

        void Quit()
        {
            if (quitting) return;
            quitting = true; ui.Stop(); M.Stop();
            try { SystemEvents.UserPreferenceChanged -= prefHandler; } catch { }
            if (hotkey != null) hotkey.Dispose();
            tray.Visible = false; tray.Dispose();
            if (full != null && !full.IsDisposed) { full.Close(); full.Dispose(); }
            widget.Dispose(); detailWin.Dispose();
            Program.CleanCaptures();
            ExitThread();
        }

        // ---------- 50 ms: hover, dwell, cover check - and draw only when something changed
        static Rectangle Zone()
        {
            var wa = Screen.PrimaryScreen.WorkingArea; int m = (int)(10 * Draw.S);
            int w = (int)(264 * Draw.S), h = (int)(300 * Draw.S);
            return new Rectangle(wa.Right - m - w, wa.Bottom - m - h, w, h);
        }

        void OnUi()
        {
            try
            {
                uiTick++;
                bool viewUp = full != null && !full.IsDisposed && full.Visible && full.WindowState != FormWindowState.Minimized;
                if (!widgetOn || viewUp) { if (widget.Placed.Width > 1) { widget.Vanish(); detailWin.Vanish(); drawnKey = ""; } ui.Interval = 500; return; }
                // Poll fast only when the mouse is near the corner; otherwise 4 times a second is plenty.
                var near = Zone(); near.Inflate((int)(120 * Draw.S), (int)(120 * Draw.S));
                int want = near.Contains(Cursor.Position) || hovered || dwell > 0 ? 50 : 250;
                if (ui.Interval != want) ui.Interval = want;
                if ((DateTime.UtcNow - lastCover).TotalMilliseconds >= 400)
                {
                    lastCover = DateTime.UtcNow;
                    bool c = WindowOver(Zone());
                    if (c != covered) { covered = c; revealed = false; dwellStart = DateTime.MinValue; drawnKey = ""; }
                }
                var cur = Cursor.Position; bool inside = widget.Placed.Contains(cur);
                if (covered)
                {
                    if (inside)
                    {
                        if (!revealed)
                        {
                            if (dwellStart == DateTime.MinValue) dwellStart = DateTime.UtcNow;
                            dwell = (DateTime.UtcNow - dwellStart).TotalMilliseconds / DWELL_MS;
                            if (dwell >= 1) { revealed = true; dwell = 0; }
                        }
                    }
                    else { revealed = false; dwellStart = DateTime.MinValue; dwell = 0; }
                    hovered = revealed;
                }
                else { hovered = inside; dwell = 0; }
                string key = "";
                if (hovered) foreach (var kv in hits) { var r = kv.Value; if (new RectangleF(widget.Placed.X + r.X, widget.Placed.Y + r.Y, r.Width, r.Height).Contains(cur)) { key = kv.Key; break; } }
                hoverKey = key;
                Snap s = M.Latest; bool compact = !hovered && (covered || (s.Level == 0 && !s.Call));
                string state = compact + "|" + hovered + "|" + key + "|" + covered + "|" + ((int)(dwell * 20)) + "|" + Voice.Serious;
                bool fresh = M.Version != drawnVersion;
                // The folded chip redraws only when its words or colour change; the open card once a second.
                if (state != drawnKey || (fresh && (!compact || s.Chip != lastChip || s.Level != lastLevel)))
                {
                    Render(s, compact); drawnKey = state; drawnVersion = M.Version;
                }
            }
            catch (Exception e) { Program.Log("ui: " + e.Message); }
        }

        void Render(Snap s, bool compact)
        {
            var wa = Screen.PrimaryScreen.WorkingArea; int m = (int)(10 * Draw.S);
            using (var bmp = compact ? Draw.Chip(s, dwell) : Draw.Card(s, hits))
            {
                if (compact) hits.Clear();
                widget.Present(bmp, hovered ? (byte)255 : FADE, wa.Right - m - bmp.Width, wa.Bottom - m - bmp.Height);
            }
            lastChip = s.Chip; lastLevel = s.Level;
            Detail d;
            if (!compact && hovered && hoverKey != "" && s.Details.TryGetValue(hoverKey, out d))
                using (var db = Draw.DetailCard(d)) detailWin.Present(db, 255, widget.Placed.X - db.Width - (int)(8 * Draw.S), wa.Bottom - m - db.Height);
            else if (detailWin.Placed.Width > 1) detailWin.Vanish();
            string tip = "AdeeboMeter - " + s.Chip.ToLowerInvariant();
            if (tray.Text != tip) tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        // Any normal window over the widget's corner? (Desktop icons are ignored by design.)
        bool WindowOver(Rectangle zone)
        {
            bool found = false;
            IntPtr me1 = widget.Handle, me2 = detailWin.Handle;
            var clsBuf = new char[64];
            Native.EnumWindows(delegate(IntPtr hw, IntPtr l)
            {
                if (hw == me1 || hw == me2 || !Native.IsWindowVisible(hw) || Native.IsIconic(hw)) return true;
                int ex = Native.GetWindowLong(hw, -20);
                if ((ex & 0x80) != 0) return true;                          // tool windows: tooltips, menus, toasts
                if ((ex & 0x20) != 0 && (ex & 0x80000) != 0) return true;   // click-through overlays like us
                int cloaked; if (Native.DwmGetWindowAttribute(hw, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
                int cn = Native.GetClassName(hw, clsBuf, clsBuf.Length); string cls = cn > 0 ? new string(clsBuf, 0, cn) : "";
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" || cls == "NotifyIconOverflowWindow" || cls == "TopLevelWindowForOverflowXamlIsland" || cls == "XamlExplorerHostIslandWindow") return true;
                Native.RECT r;
                if (Native.DwmGetWindowAttribute(hw, 9, out r, Marshal.SizeOf(typeof(Native.RECT))) != 0) Native.GetWindowRect(hw, out r);
                var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                if (rect.Width < 8 || rect.Height < 8 || !rect.IntersectsWith(zone)) return true;
                found = true; return false;
            }, IntPtr.Zero);
            return found;
        }
    }

    // ================================================================== install / update / remove - the exe is its own installer
    // Double-click a downloaded AdeeboMeter.exe and it offers to install itself for this Windows user only:
    // %LOCALAPPDATA%\Programs\AdeeboMeter\<version>, a Start menu entry, an entry in Settings > Apps (so it can be removed
    // like any app), your settings carried over. No administrator rights, no shell, no extra files.
    static class Setup
    {
        public static string Base { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AdeeboMeter"); } }
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AdeeboMeter";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static string Shortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "AdeeboMeter.lnk"); } }

        public static bool Under(string path, string root)
        {
            try
            {
                string p = Path.GetFullPath(path).TrimEnd('\\') + "\\", r = Path.GetFullPath(root).TrimEnd('\\') + "\\";
                return p.StartsWith(r, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
        public static bool RunningInstalled { get { return Under(Program.Dir, Base); } }

        // Offer to install only on a first start of a copy that isn't installed and was never used from where it is.
        public static bool ShouldOffer()
        {
            if (RunningInstalled) return false;
            return !File.Exists(Path.Combine(Program.Dir, "AdeeboMeter.settings")) && !File.Exists(Path.Combine(Program.Dir, "Ade" + "boMeter.settings"));
        }

        // The installed copy: its version and exe (from Settings > Apps entry, else the newest folder under Base).
        public static void Installed(out string version, out string exe)
        {
            version = null; exe = null;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                    if (k != null)
                    {
                        string loc = k.GetValue("InstallLocation") as string, v = k.GetValue("DisplayVersion") as string;
                        if (loc != null && Under(loc, Base) && File.Exists(Path.Combine(loc, "AdeeboMeter.exe"))) { version = v; exe = Path.Combine(loc, "AdeeboMeter.exe"); return; }
                    }
                if (Directory.Exists(Base))
                    foreach (var d in Directory.GetDirectories(Base).OrderByDescending(Directory.GetLastWriteTimeUtc))
                        if (File.Exists(Path.Combine(d, "AdeeboMeter.exe"))) { version = Path.GetFileName(d); exe = Path.Combine(d, "AdeeboMeter.exe"); return; }
            }
            catch (Exception e) { Program.Log("installed: " + e.Message); }
        }

        // key=value settings line: replaced if there, added if not
        public static void SetLine(string file, string key, string value)
        {
            var lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : new List<string>();
            int i = lines.FindIndex(l => l.StartsWith(key + "=", StringComparison.Ordinal));
            if (i >= 0) lines[i] = key + "=" + value; else lines.Insert(0, key + "=" + value);
            File.WriteAllLines(file, lines);
        }

        // Ask running copies to leave; anything that doesn't answer (older versions) is stopped.
        public static void StopOthers()
        {
            try { using (var q = EventWaitHandle.OpenExisting(Single.QuitName)) q.Set(); } catch { }
            int me = Process.GetCurrentProcess().Id;
            var until = DateTime.UtcNow.AddSeconds(4);
            while (DateTime.UtcNow < until)
            {
                var ps = Process.GetProcessesByName("AdeeboMeter"); bool any = false;
                foreach (var p in ps) { if (p.Id != me) any = true; p.Dispose(); }
                if (!any) return;
                Thread.Sleep(200);
            }
            Single.KillOthers();
        }

        static void MakeShortcut(string exe)
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return;
            object sh = Activator.CreateInstance(t), lnk = null;
            try
            {
                lnk = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { Shortcut }, CultureInfo.InvariantCulture);
                var lt = lnk.GetType();
                Action<string, object> set = delegate(string n, object v) { lt.InvokeMember(n, System.Reflection.BindingFlags.SetProperty, null, lnk, new[] { v }, CultureInfo.InvariantCulture); };
                set("TargetPath", exe); set("WorkingDirectory", Path.GetDirectoryName(exe));
                set("Description", "AdeeboMeter - is it me or the network?"); set("IconLocation", exe + ",0");
                lt.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, lnk, null, CultureInfo.InvariantCulture);
            }
            finally { if (lnk != null) Marshal.FinalReleaseComObject(lnk); Marshal.FinalReleaseComObject(sh); }
        }

        // The install itself. step(text, 0..1) reports progress. Returns the installed exe.
        public static string Install(bool autoStart, Action<string, float> step)
        {
            step("Making room...", 0.1f);
            StopOthers();
            string dst = Path.Combine(Base, Program.Version), exe = Path.Combine(dst, "AdeeboMeter.exe");
            Directory.CreateDirectory(dst);

            step("Copying AdeeboMeter...", 0.3f);
            string self = Application.ExecutablePath;
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase)) File.Copy(self, exe, true);

            step("Bringing your settings along...", 0.5f);
            string settings = Path.Combine(dst, "AdeeboMeter.settings");
            if (!File.Exists(settings))
            {
                string from = null;
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    {
                        var v = k == null ? null : k.GetValue("AdeeboMeter") as string;
                        if (v != null) { string d = Path.GetDirectoryName(v.Trim('"')); if (File.Exists(Path.Combine(d, "AdeeboMeter.settings"))) from = Path.Combine(d, "AdeeboMeter.settings"); }
                    }
                }
                catch { }
                if (from == null && Directory.Exists(Base))
                    from = Directory.GetDirectories(Base).Select(d => Path.Combine(d, "AdeeboMeter.settings")).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (from != null) File.Copy(from, settings, false);
            }
            SetLine(settings, "autostart", autoStart ? "1" : "0");

            step("Adding it to Start and to Settings > Apps...", 0.7f);
            try { MakeShortcut(exe); } catch (Exception e) { Program.Log("shortcut: " + e.Message); }
            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "AdeeboMeter");
                k.SetValue("DisplayVersion", Program.Version);
                k.SetValue("Publisher", "Adeeb Khan");
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", dst);
                k.SetValue("UninstallString", "\"" + exe + "\" --uninstall");
                k.SetValue("URLInfoAbout", "https://github.com/adeeb111/AdeeboMeter");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024 + 1), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord); k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
            if (!autoStart)
                try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (k != null && k.GetValue("AdeeboMeter") != null) k.DeleteValue("AdeeboMeter"); } catch { }

            step("Tidying up older versions...", 0.9f);
            foreach (var d in Directory.GetDirectories(Base))
                if (!string.Equals(Path.GetFullPath(d).TrimEnd('\\'), Path.GetFullPath(dst).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    try { Directory.Delete(d, true); } catch (Exception e) { Program.Log("old version left: " + d + " (" + e.Message + ")"); }
            Program.Log("installed " + Program.Version + " to " + dst);
            step("Done", 1f);
            return exe;
        }

        public static void Uninstall(Action<string, float> step)
        {
            step("Closing AdeeboMeter...", 0.2f);
            StopOthers();
            step("Removing it from Start and Settings > Apps...", 0.5f);
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (k != null) { var v = k.GetValue("AdeeboMeter") as string; if (v != null && Under(v.Trim('"'), Base)) k.DeleteValue("AdeeboMeter"); }
            }
            catch { }
            try { if (File.Exists(Shortcut)) File.Delete(Shortcut); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey); } catch { }
            step("Removing your history and captures...", 0.75f);
            try { string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdeeboMeter"); if (Directory.Exists(data)) Directory.Delete(data, true); } catch { }
            // The running exe can't delete its own folder: a copy in %TEMP% waits for us to exit, then removes it.
            if (RunningInstalled || Directory.Exists(Base))
            {
                string tmp = Path.Combine(Path.GetTempPath(), "AdeeboMeter-remove.exe");
                try
                {
                    File.Copy(Application.ExecutablePath, tmp, true);
                    var psi = new ProcessStartInfo(tmp, "--remove-folder " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetTempPath() };
                    using (Process.Start(psi)) { }
                }
                catch (Exception e) { Program.Log("remove folder: " + e.Message); }
            }
            step("Done", 1f);
        }

        // --remove-folder <pid>: wait for the uninstaller to exit, then delete the install folder (only ever Base).
        public static int RemoveFolder(string pid)
        {
            int id;
            if (int.TryParse(pid, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                try { using (var p = Process.GetProcessById(id)) p.WaitForExit(600000); } catch { }
            for (int i = 0; i < 10 && Directory.Exists(Base); i++)
            {
                try { Directory.Delete(Base, true); } catch { Thread.Sleep(500); }
            }
            return 0;
        }
    }

    // The install / remove window: one card, one question, one button.
    sealed class SetupWindow : Form
    {
        enum St { Offer, Working, Done, Error, AskRemove, Removed }
        St st;
        bool autoStart = true, startHere;
        string instVer, instExe, stepText = "", err = "";
        float progress;
        readonly float S;
        readonly Font fTitle, fTag, fBody, fBtn, fSmall;
        readonly Dictionary<string, RectangleF> hot = new Dictionary<string, RectangleF>();
        string hover = "";
        public bool RunHere { get { return startHere; } }

        public SetupWindow(bool remove)
        {

            using (var g = Graphics.FromHwnd(IntPtr.Zero)) S = g.DpiX / 96f;
            Theme.Apply(Program.WantDark());
            string disp = Draw.Fam("Segoe UI Variable Display", "Segoe UI"), text = Draw.Fam("Segoe UI Variable Text", "Segoe UI"), semi = Draw.Fam("Segoe UI Semibold", "Segoe UI");
            FontStyle semiStyle = semi == "Segoe UI" ? FontStyle.Bold : FontStyle.Regular;
            fTitle = new Font(disp, 30 * S, FontStyle.Bold, GraphicsUnit.Pixel);
            fTag = new Font(semi, 16 * S, semiStyle, GraphicsUnit.Pixel);
            fBody = new Font(text, 13.5f * S, FontStyle.Regular, GraphicsUnit.Pixel);
            fBtn = new Font(semi, 14 * S, semiStyle, GraphicsUnit.Pixel);
            fSmall = new Font(text, 11.5f * S, FontStyle.Regular, GraphicsUnit.Pixel);
            Setup.Installed(out instVer, out instExe);
            st = remove ? St.AskRemove : St.Offer;
            Text = remove ? "Remove AdeeboMeter" : "AdeeboMeter";
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size((int)(460 * S), (int)((remove ? 400 : 540) * S));
            BackColor = Theme.Card; ShowInTaskbar = true; KeyPreview = true;
            try { Icon = Theme.MakeIcon(32); } catch { }
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Default;
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; /* drop shadow */ return cp; } }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int round = 2;
            bool ok = false;
            try { ok = Native.DwmSetWindowAttribute(Handle, 33, ref round, 4) == 0; } catch { }
            if (!ok) using (var p = Theme.Round(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 14 * S)) Region = new Region(p);
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            fTitle.Dispose(); fTag.Dispose(); fBody.Dispose(); fBtn.Dispose(); fSmall.Dispose();
            var ic = Icon; Icon = null; ShowIcon = false; if (ic != null) ic.Dispose();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && st != St.Working) Close();
            if (e.KeyCode == Keys.Enter) Press("main");
            base.OnKeyDown(e);
        }

        bool Upgrade { get { return instVer != null && instVer != Program.Version; } }
        bool Same { get { return instVer != null && instVer == Program.Version; } }

        void Btn(Graphics g, string key, RectangleF r, string label, Color bg, Color fg, bool filled)
        {
            hot[key] = r;
            bool h = hover == key;
            if (filled) Theme.Fill(g, r, 10 * S, h ? ControlPaint.Dark(bg, 0.08f) : bg);
            else if (h) Theme.Fill(g, r, 10 * S, Theme.Soft);
            TextRenderer.DrawText(g, label, filled ? fBtn : fBody, Rectangle.Round(r), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        static void Center(Graphics g, string s, Font f, Color c, float y, float w)
        {
            TextRenderer.DrawText(g, s, f, new Rectangle(0, (int)y, (int)w, (int)(f.Height * 3)), c, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            float W = ClientSize.Width, H = ClientSize.Height;
            hot.Clear();
            using (var bg = new SolidBrush(Theme.Card)) g.FillRectangle(bg, ClientRectangle);
            // a soft blue wash behind the logo
            using (var wash = new LinearGradientBrush(new RectangleF(0, 0, W, 190 * S), Theme.Dark ? Color.FromArgb(28, 31, 111, 235) : Theme.BlueSoft, Theme.Card, 90f))
                g.FillRectangle(wash, 0, 0, W, 190 * S);
            if (!Theme.Dark) using (var pen = new Pen(Theme.Line)) g.DrawRectangle(pen, 0, 0, W - 1, H - 1);

            // close
            var xr = new RectangleF(W - 40 * S, 12 * S, 28 * S, 28 * S); hot["close"] = xr;
            if (st != St.Working)
            {
                if (hover == "close") Theme.Fill(g, xr, 8 * S, Theme.Soft);
                using (var p = new Pen(Theme.Muted, 1.6f * S)) { float c = 7 * S, cx = xr.X + xr.Width / 2, cy = xr.Y + xr.Height / 2; g.DrawLine(p, cx - c / 1.4f, cy - c / 1.4f, cx + c / 1.4f, cy + c / 1.4f); g.DrawLine(p, cx + c / 1.4f, cy - c / 1.4f, cx - c / 1.4f, cy + c / 1.4f); }
            }

            float logo = 84 * S, y = 40 * S;
            Theme.Logo(g, new RectangleF((W - logo) / 2, y, logo, logo), true); y += logo + 18 * S;
            Center(g, "AdeeboMeter", fTitle, Theme.Ink, y, W); y += fTitle.Height + 4 * S;

            if (st == St.Offer)
            {
                Center(g, "Is it me or the network?", fTag, Theme.Blue, y, W); y += fTag.Height + 26 * S;
                string[] lines = {
                    "Lives quietly in the corner of your screen.",
                    "No cloud. No telemetry. Never listens.",
                    "Installs just for you — no administrator rights." };
                Color[] dots = { Theme.Green, Theme.Blue, Theme.Amber };
                float bx = 58 * S;
                for (int i = 0; i < 3; i++)
                {
                    using (var b = new SolidBrush(dots[i])) g.FillEllipse(b, bx, y + 5 * S, 9 * S, 9 * S);
                    TextRenderer.DrawText(g, lines[i], fBody, new Point((int)(bx + 22 * S), (int)y), Theme.Sub, TextFormatFlags.NoPadding);
                    y += 28 * S;
                }
                y += 14 * S;
                // Start with Windows - a switch
                var sw = new RectangleF(W - 58 * S - 42 * S, y, 42 * S, 24 * S); hot["auto"] = new RectangleF(bx, y - 4 * S, W - 2 * bx, 32 * S);
                TextRenderer.DrawText(g, "Start with Windows", fBody, new Point((int)bx, (int)(y + 2 * S)), Theme.Ink, TextFormatFlags.NoPadding);
                Theme.Fill(g, sw, 12 * S, autoStart ? Theme.Green : Theme.Rail);
                using (var kb = new SolidBrush(Color.White)) g.FillEllipse(kb, autoStart ? sw.Right - 21 * S : sw.X + 3 * S, sw.Y + 3 * S, 18 * S, 18 * S);
                y += 52 * S;
                string main = Same ? "Open AdeeboMeter" : Upgrade ? "Update to " + Program.Version : "Install";
                Btn(g, "main", new RectangleF(bx, y, W - 2 * bx, 46 * S), main, Theme.Blue, Color.White, true); y += 54 * S;
                Btn(g, "here", new RectangleF(bx, y, W - 2 * bx, 32 * S), Same ? "Reinstall" : "Just run it from here", Theme.Card, Theme.Muted, false);
                string foot = Upgrade ? "You have " + instVer + ". Your settings come along." : "Remove it anytime from Settings > Apps.";
                Center(g, foot, fSmall, Theme.Muted, H - 34 * S, W);
            }
            else if (st == St.Working)
            {
                Center(g, stepText, fBody, Theme.Sub, y + 30 * S, W);
                var track = new RectangleF(70 * S, y + 70 * S, W - 140 * S, 6 * S);
                Theme.Fill(g, track, 3 * S, Theme.Track);
                Theme.Fill(g, new RectangleF(track.X, track.Y, Math.Max(6 * S, track.Width * progress), track.Height), 3 * S, Theme.Blue);
            }
            else if (st == St.Done)
            {
                Center(g, "You're all set.", fTag, Theme.Green, y, W); y += fTag.Height + 22 * S;
                Center(g, "AdeeboMeter is now in the bottom-right corner\nof your screen, and in your Start menu.", fBody, Theme.Sub, y, W); y += 70 * S;
                Center(g, "Click its tray icon, or press Ctrl+Alt+A, for the full story.", fSmall, Theme.Muted, y, W); y += 34 * S;
                // where to look: a little screen with the card in its corner
                var scr = new RectangleF((W - 150 * S) / 2, y, 150 * S, 92 * S);
                Theme.Fill(g, scr, 8 * S, Theme.Soft); Theme.Stroke(g, scr, 8 * S, Theme.Line, 1.5f * S);
                using (var bar = new SolidBrush(Theme.Line)) g.FillRectangle(bar, scr.X + 1, scr.Bottom - 12 * S, scr.Width - 2, 11 * S);
                Theme.Fill(g, new RectangleF(scr.Right - 46 * S, scr.Bottom - 46 * S, 38 * S, 28 * S), 4 * S, Theme.Blue);
                using (var dot = new SolidBrush(Theme.Green)) g.FillEllipse(dot, scr.Right - 41 * S, scr.Bottom - 41 * S, 6 * S, 6 * S);
                Btn(g, "main", new RectangleF(58 * S, H - 86 * S, W - 116 * S, 46 * S), "Done", Theme.Blue, Color.White, true);
            }
            else if (st == St.Error)
            {
                Center(g, "That didn't work.", fTag, Theme.Red, y, W); y += fTag.Height + 18 * S;
                TextRenderer.DrawText(g, err, fBody, new Rectangle((int)(50 * S), (int)y, (int)(W - 100 * S), (int)(120 * S)), Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
                Btn(g, "main", new RectangleF(58 * S, H - 86 * S, W - 116 * S, 46 * S), "Close", Theme.Blue, Color.White, true);
            }
            else if (st == St.AskRemove)
            {
                Center(g, "Remove AdeeboMeter?", fTag, Theme.Ink, y, W); y += fTag.Height + 16 * S;
                Center(g, "It stops, leaves your Start menu and stops starting\nwith Windows. Its settings and history go too.", fBody, Theme.Sub, y, W);
                Btn(g, "main", new RectangleF(58 * S, H - 104 * S, W - 116 * S, 46 * S), "Remove", Theme.Red, Color.White, true);
                Btn(g, "close2", new RectangleF(58 * S, H - 52 * S, W - 116 * S, 32 * S), "Keep it", Theme.Card, Theme.Muted, false);
            }
            else if (st == St.Removed)
            {
                Center(g, "AdeeboMeter is removed.", fTag, Theme.Ink, y, W); y += fTag.Height + 16 * S;
                Center(g, "Thanks for giving it a try.", fBody, Theme.Sub, y, W);
                Btn(g, "main", new RectangleF(58 * S, H - 86 * S, W - 116 * S, 46 * S), "Close", Theme.Blue, Color.White, true);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            string h = hot.Where(kv => kv.Value.Contains(e.Location)).Select(kv => kv.Key).FirstOrDefault() ?? "";
            if (h != hover) { hover = h; Cursor = h == "" ? Cursors.Default : Cursors.Hand; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { if (hover != "") { hover = ""; Invalidate(); } base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            string h = hot.Where(kv => kv.Value.Contains(e.Location)).Select(kv => kv.Key).FirstOrDefault();
            if (h != null) { Press(h); return; }
            // drag the card by anywhere else
            Capture = false; var m = Message.Create(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, (IntPtr)2 /*HTCAPTION*/, IntPtr.Zero); WndProc(ref m);
        }

        void Press(string key)
        {
            if (st == St.Working) return;
            if (key == "close" || key == "close2") { Close(); return; }
            if (key == "auto" && st == St.Offer) { autoStart = !autoStart; Invalidate(); return; }
            if (key == "here" && st == St.Offer)
            {
                if (Same) { Run(false); return; }
                startHere = true; Close(); return;
            }
            if (key != "main") return;
            if (st == St.Offer)
            {
                if (Same) { StartExe(instExe); Close(); return; }
                Run(false);
            }
            else if (st == St.AskRemove) Run(true);
            else Close();
        }

        void Run(bool remove)
        {
            st = St.Working; progress = 0; stepText = remove ? "Removing..." : "Installing..."; Invalidate();
            Action<string, float> step = delegate(string t, float p)
            {
                try { BeginInvoke((Action)delegate { stepText = t; progress = p; Invalidate(); }); } catch { }
                Thread.Sleep(220);      // long enough to read each step
            };
            Task.Factory.StartNew(() =>
            {
                string exe = null, fail = null;
                try { if (remove) Setup.Uninstall(step); else exe = Setup.Install(autoStart, step); }
                catch (Exception ex) { fail = ex.Message; Program.Log("setup: " + ex); }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (fail != null) { st = St.Error; err = fail; }
                        else if (remove) st = St.Removed;
                        else { st = St.Done; StartExe(exe); }
                        Invalidate();
                    });
                }
                catch { }
            });
        }

        static void StartExe(string exe)
        {
            if (exe == null || !File.Exists(exe)) return;
            try { using (Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) })) { } }
            catch (Exception e) { Program.Log("start installed: " + e.Message); }
        }
    }

    // ================================================================== self-test: AdeeboMeter.exe --selftest
    // Exercises every part once on this PC and writes AdeeboMeter-selftest.txt next to the exe.
    // Network use: pings to your gateway and one HEAD request to it on port 80 - nothing on the internet.
    [SuppressMessage("Performance", "CA1861", Justification = "one-shot self-test data")]
    static class SelfTest
    {
        public static int Run()
        {
            var sb = new StringBuilder(); int fails = 0;
            Action<string, Action> T = delegate(string name, Action a)
            {
                var sw = Stopwatch.StartNew();
                try { a(); sb.AppendLine("PASS  " + name.PadRight(40) + sw.ElapsedMilliseconds + " ms"); }
                catch (Exception e) { fails++; sb.AppendLine("FAIL  " + name.PadRight(40) + e.GetType().Name + ": " + e.Message); }
            };
            sb.AppendLine("AdeeboMeter " + Program.Version + " self-test  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture) + "  " + Environment.OSVersion + "  CLR " + Environment.Version + "  " + (Environment.Is64BitProcess ? "x64" : "x86"));
            var M = new Monitor();
            float sc; using (var g = Graphics.FromHwnd(IntPtr.Zero)) sc = g.DpiX / 96f;
            sb.AppendLine("      display scale " + (sc * 100).ToString("0", CultureInfo.CurrentCulture) + "%");
            T("fonts / drawing init", () => Draw.Init(sc));
            T("interfaces", () => { M.RefreshInterfaces(); lock (M.L) sb.AppendLine("      adapter=" + M.AdapterKind + " gateway=" + (M.Gateway != "" ? "yes" : "none") + " vpn=" + M.VpnUp + " mtu=" + M.PrimaryMtu); });
            T("counters x3", () => { for (int i = 0; i < 3; i++) { M.Tick(); Thread.Sleep(300); } sb.AppendLine("      cpu=" + Draw.Fmt(M.Cpu.Last, "0", "%") + " mem=" + Draw.Fmt(M.Mem.Last, "0", "%") + " disk=" + Draw.Fmt(M.Disk.Last, "0", "%")); });
            T("tcp table (every state known)", () =>
            {
                var l = Tables.Tcp(true, new Dictionary<int, string>());
                sb.AppendLine("      " + l.Count + " sockets, states: " + string.Join(",", l.Select(c => c.State).Distinct()));
                var odd = l.Where(c => c.State.StartsWith("STATE ", StringComparison.Ordinal)).ToList();
                foreach (var c in odd.Take(5)) sb.AppendLine("      odd: " + c.State + "  " + c.Proto + " " + c.Local + " -> " + c.Remote + "  pid " + c.Pid);
                if (odd.Count > 0) throw new InvalidOperationException(odd.Count + " socket(s) with a state number Windows doesn't document");
            });
            T("udp table", () => sb.AppendLine("      " + Tables.Udp().Count + " sockets"));
            T("dns cache names (offline)", () =>
            {
                M.RefreshIpNames(); lock (M.L) sb.AppendLine("      " + M.IpNames.Count + " cached addresses");
                try
                {
                    string dd = Job.Run(Path.Combine(Environment.SystemDirectory, "ipconfig.exe"), "/displaydns", 5000) ?? "";
                    var probe = new Dictionary<string, string>(); Monitor.ParseDisplayDns(dd, probe);
                    var firstLines = dd.Split('\n').Select(x => x.Trim()).Where(x => x != "").Take(3).Select(x => Draw.Trunc(x, 50));
                    sb.AppendLine("      ipconfig /displaydns: " + dd.Split('\n').Length + " lines, " + probe.Count + " addresses · starts: " + string.Join(" | ", firstLines));
                }
                catch (Exception e) { sb.AppendLine("      ipconfig /displaydns failed: " + e.Message); }
                var t = new Dictionary<string, string>();
                Monitor.ParseDisplayDns("Windows IP Configuration\r\n\r\n    github.com\r\n    ----------------------------------------\r\n    Record Name . . . . . : github.com\r\n    Record Type . . . . . : 1\r\n    Time To Live  . . . . : 41\r\n    Data Length . . . . . : 4\r\n    Section . . . . . . . : Answer\r\n    A (Host) Record . . . : 140.82.112.4\r\n\r\n    www.example.com\r\n    ----------------------------------------\r\n    AAAA Record . . . . . : 2606:2800:21f:cb07:6820:80da:af6b:8b2c\r\n", t);
                string v1, v2; t.TryGetValue("140.82.112.4", out v1); t.TryGetValue("2606:2800:21f:cb07:6820:80da:af6b:8b2c", out v2);
                if (v1 != "github.com" || v2 != "www.example.com" || t.Count != 2) throw new InvalidOperationException("ipconfig /displaydns parser wrong");
            });
            T("never listens", () => { if (Tables.OwnListener()) throw new InvalidOperationException("AdeeboMeter owns a listening socket"); });
            T("destination input validation", () =>
            {
                string h; int p; bool t; string e;
                string[] good = { "github.com", "github.com:443", "https://example.com/x", "10.0.0.1", "[2001:db8::1]:443", "http://host:8080", "2001:db8::1" };
                string[] bad = { "", "a b", "github.com:99999", "x;rm -rf", "$(calc)", "host:abc", new string('a', 400), "http://", "\"quoted\"", "a&b", "..", "-bad.com" };
                foreach (var s in good) if (!DestCheck.Parse(s, out h, out p, out t, out e)) throw new InvalidOperationException("rejected good input: " + s);
                foreach (var s in bad) if (DestCheck.Parse(s, out h, out p, out t, out e)) throw new InvalidOperationException("accepted bad input: " + s);
            });
            T("gateway ping", () => { string gw; lock (M.L) gw = M.Gateway; sb.AppendLine("      " + Draw.Fmt(Monitor.PingMs(gw), "0", " ms")); });
            T("health check", () => { var r = HealthCheck.Run(M); sb.AppendLine("      " + r.Count + " checks: " + string.Join(", ", r.Select(x => x[1] + "=" + x[0]))); });
            T("destination check on gateway (local only)", () =>
            {
                string gw; lock (M.L) gw = M.Gateway; if (gw == "") { sb.AppendLine("      skipped: no gateway"); return; }
                var r = new DestResult(); DestCheck.Run(gw + ":80", r, delegate { }, M);
                if (!r.Done) throw new InvalidOperationException("did not finish");
                sb.AppendLine("      steps=" + string.Join(" ", r.Lv) + "  verdict=" + Draw.Trunc(r.Verdict, 70));
            });
            T("render chip / card / detail", () =>
            {
                var snap = new Snap(); snap.Details["x"] = new Detail { Title = "t", Note = "n" }; snap.Details["x"].Lines.Add(new[] { "a", "b", "0" });
                using (Draw.Chip(snap, 0.5)) { } using (Draw.Card(snap, new Dictionary<string, RectangleF>())) { } using (Draw.DetailCard(snap.Details["x"])) { }
            });
            T("engineer view opens in under 1 s", () =>
            {
                var sw = Stopwatch.StartNew();
                using (var icon = Theme.MakeIcon(32))
                {
                    var fv = new FullView(icon, M); fv.WindowState = FormWindowState.Normal; fv.Opacity = 0; fv.ShowInTaskbar = false; fv.Show(); Application.DoEvents();
                    long ms = sw.ElapsedMilliseconds; sw.Restart();
                    fv.Close(); fv.Dispose(); Application.DoEvents();
                    sb.AppendLine("      opened in " + ms + " ms, closed in " + sw.ElapsedMilliseconds + " ms");
                    if (ms > 1000) throw new InvalidOperationException("took " + ms + " ms");
                }
            });
            if (Environment.GetCommandLineArgs().Any(a => a == "--samples"))
                T("sample images (light + dark)", () =>
                {
                    var snap = new Snap { Level = 2, Headline = "Your PC is cooked, boss - memory is full. Edge (3.7 GB) and Teams are the hungry ones; close something.", Countdown = 7, Call = true, Cpu = 43, Mem = 93, Disk = 12, Down = 12.4, Up = 2.1, Jitter = 12, LossPct = 0 };
                    snap.Path = new[] { 2, 0, 0, 1, 0 }; snap.PathLabel = new[] { "PC", "Wi-Fi", "ISP", "DNS", "Internet" };
                    snap.PathMain = new[] { "CPU 43% · RAM 93%", "signal 91% · gw 2 ms", "+12 ms past router", "118 ms", "14 ms · loss 0%" };
                    snap.PathSub = new[] { "Problem here · 16 threads · 16 GB", "OK · Adeeb ch 157 · 10.0.0.1", "OK · no VPN · MTU 1500", "Slow · 64.71.255.204", "OK · probe 1.1.1.1" };
                    var rnd = new Random(3); snap.DownHist = Enumerable.Range(0, 60).Select(i => 5 + 10 * Math.Abs(Math.Sin(i / 6.0)) + rnd.NextDouble() * 3).ToArray(); snap.UpHist = snap.DownHist.Select(v => v / 5).ToArray();
                    var det = new Detail { Title = "Memory", TopTitle = "TOP BY MEMORY", Note = "Commit is the real \"can I open another app\" number." };
                    det.Lines.Add(new[] { "In use", "14.9 of 16.0 GB" }); det.Lines.Add(new[] { "Commit", "21.2 of 24.0 GB", "2" }); det.Top.Add(new[] { "Edge (31)", "3.7 GB" }); det.Top.Add(new[] { "Teams (6)", "1.6 GB" });
                    foreach (var dark in new[] { false, true })
                    {
                        Theme.Apply(dark); string tag = dark ? "dark" : "light";
                        using (var b = Draw.Chip(snap, 0.6)) b.Save(Path.Combine(Program.Dir, "sample_chip_" + tag + ".png"));
                        using (var b = Draw.Card(snap, new Dictionary<string, RectangleF>())) b.Save(Path.Combine(Program.Dir, "sample_card_" + tag + ".png"));
                        using (var b = Draw.DetailCard(det)) b.Save(Path.Combine(Program.Dir, "sample_detail_" + tag + ".png"));
                    }
                    Theme.Apply(false);
                });
            T("no leaks: open + close the view 30 times", () =>
            {
                IntPtr me = Native.GetCurrentProcess();
                Func<string> res = () => "GDI " + Native.GetGuiResources(me, 0) + ", USER " + Native.GetGuiResources(me, 1);
                Action<FullView> cycle = null;
                using (var icon = Theme.MakeIcon(32))
                {
                    cycle = delegate(FullView unused)
                    {
                        var fv = new FullView(icon, M); fv.WindowState = FormWindowState.Normal; fv.Opacity = 0; fv.ShowInTaskbar = false; fv.Show(); Application.DoEvents();
                        fv.TestWalk();
                        fv.Close(); fv.Dispose(); Application.DoEvents();
                    };
                    cycle(null); cycle(null);   // warm-up: Windows and WinForms create some one-time handles (cursors, caches) on first use
                    GC.Collect(); GC.WaitForPendingFinalizers(); Application.DoEvents();
                    int gdi0 = Native.GetGuiResources(me, 0), usr0 = Native.GetGuiResources(me, 1); string before = res();
                    // Windows frees window handles a moment after a window closes, so short runs swing +/- a few handles.
                    // A real leak grows with every open: measure a long run, after a pause, and allow 1 handle per 3 opens.
                    for (int i = 0; i < 30; i++) cycle(null);
                    Thread.Sleep(1500); Application.DoEvents();
                    GC.Collect(); GC.WaitForPendingFinalizers(); Application.DoEvents();
                    int gdi1 = Native.GetGuiResources(me, 0), usr1 = Native.GetGuiResources(me, 1);
                    sb.AppendLine("      after warm-up: " + before + "   after 30 more: " + res());
                    if (gdi0 > 0 && (gdi1 - gdi0 > 10 || usr1 - usr0 > 10))
                    {
                        // Find the culprit: 4 opens per variant, each visiting one page (or just the hover card).
                        var variants = new[] { "open+close only", "no icon", "in taskbar", "link", "procs", "changes", "hover card" };
                        foreach (var vname in variants)
                        {
                            int g0 = Native.GetGuiResources(me, 0), u0 = Native.GetGuiResources(me, 1);
                            for (int i = 0; i < 10; i++)
                            {
                                var fv = new FullView(vname == "no icon" ? null : icon, M); fv.WindowState = FormWindowState.Normal; fv.Opacity = 0; fv.ShowInTaskbar = vname == "in taskbar"; fv.Show(); Application.DoEvents();
                                if (vname == "hover card") fv.TestPages(Array.Empty<string>(), true);
                                else if (vname == "link" || vname == "procs" || vname == "changes") { fv.TestPages(new[] { vname }, false); Thread.Sleep(300); Application.DoEvents(); }
                                fv.Close(); fv.Dispose(); Application.DoEvents();
                            }
                            Thread.Sleep(1500); Application.DoEvents();   // let background page work finish
                            GC.Collect(); GC.WaitForPendingFinalizers(); Application.DoEvents();
                            int g1 = Native.GetGuiResources(me, 0) - g0, u1 = Native.GetGuiResources(me, 1) - u0;
                            sb.AppendLine("      leak hunt · " + vname.PadRight(16) + " GDI " + (g1 >= 0 ? "+" : "") + g1 + "  USER " + (u1 >= 0 ? "+" : "") + u1 + "  (10 opens)");
                        }
                        throw new InvalidOperationException("handles grew by " + (gdi1 - gdi0) + " GDI / " + (usr1 - usr0) + " USER over 30 opens");
                    }
                }
            });
            T("plain-English guide covers every label", () =>
            {
                var keys = new List<string> { "path.PC", "path.Wi-Fi", "path.LAN", "path.ISP", "path.VPN", "path.DNS", "path.Internet", "CPU", "Memory", "Disk", "Download", "Top by memory", "Top by CPU",
                    "TCP retransmits", "TCP resets sent", "Failed connection attempts", "ICMP unreachable received", "UDP datagrams dropped", "live.PROTOCOL HIERARCHY", "live.EXPERT INFO", "live.TOP CONVERSATIONS",
                    "TCP retransmissions", "Duplicate ACKs", "Segments lost before capture", "Out of order", "Zero window", "Connection resets (RST)", "TLS alerts",
                    "card.SIGN-IN PAGE (CAPTIVE PORTAL)", "card.WHO'S TALKING TO IT", "card.PATH STORY", "story.lan", "story.cgnat", "story.hidden", "story.net",
                    "link.RADIO", "link.CABLE", "link.LINK TO YOUR ROUTER", "link.ADDRESSES", "link.TRAFFIC SINCE THE ADAPTER STARTED", "link.CHANNEL MAP", "link.NEIGHBOURS",
                    "link.Network (SSID)", "link.Access point (BSSID)", "link.Band", "link.Channel", "link.Standard", "link.Signal", "link.Receive rate", "link.Transmit rate", "link.Security",
                    "link.Sharing your channel", "link.Air time used", "link.Connection", "link.Adapter", "link.Link speed", "link.IPv4 address", "link.IPv6", "link.Gateway (router)",
                    "link.DNS servers", "link.Address from", "link.Packet size (MTU)", "link.Hardware address (MAC)", "link.Received", "link.Sent", "link.Damaged packets (errors)",
                    "link.Dropped packets (discards)", "link.Router answers in", "link.Jitter", "link.Loss", "link.Based on",
                    "theme.toggle", "link.Channel width", "link.Roaming help", "link.Driver", "link.DROPS", "link.DHCP", "link.LAN", "link.SWITCH", "live.BY APP", "live.CALL QUALITY", "live.MOS", "live.DHCP", "hist.CHART", "card.DNS SECOND OPINION" };
                keys.AddRange(DestResult.Steps.Select(x => "step." + x));
                keys.AddRange(new[] { "Uptime", "Pending reboot", "Disk", "Adapter errors", "Wi-Fi neighbourhood", "Wi-Fi", "Proxy", "VPN", "DNS server", "Memory", "Power" }.Select(x => "hc." + x));
                keys.AddRange(new[] { "net", "wifi", "vpn", "dns", "inet", "gw", "app", "call", "verdict" }.Select(x => "kind." + x));
                keys.AddRange(new[] { "ESTABLISHED", "SYN_SENT", "SYN_RCVD", "TIME_WAIT", "CLOSE_WAIT", "LISTEN", "BOUND", "FIN_WAIT1", "FIN_WAIT2", "LAST_ACK", "CLOSING", "CLOSED", "DELETE_TCB" }.Select(x => "state." + x));
                var missing = keys.Where(k => Explain.Term(k) == null).ToList();
                if (missing.Count > 0) throw new InvalidOperationException("no explanation for: " + string.Join(", ", missing));
                foreach (var e in Explain.AllTerms) if (new[] { e.Friendly, e.Tag, e.What, e.How, e.Why, e.Without }.Any(string.IsNullOrEmpty)) throw new InvalidOperationException("empty field in " + e.Friendly);
                foreach (var n in new[] { "svchost", "lsass", "dwm", "explorer", "MsMpEng", "ms-teams", "Teams", "Edge", "OneDrive", "Outlook" })
                    if (Explain.Find(n) == null && Explain.Find(Monitor.RawOf(n)) == null) throw new InvalidOperationException("process not in guide: " + n);
                sb.AppendLine("      " + Explain.Count + " processes and " + Explain.AllTerms.Count() + " terms explained");
            });
            T("captive portal: tell a sign-in page from the real answer", () =>
            {
                string w;
                if (DestCheck.ClassifyPortal("HTTP/1.1 200 OK\r\nContent-Length: 22\r\n\r\nMicrosoft Connect Test", out w) != 0) throw new InvalidOperationException("real answer not recognised");
                if (DestCheck.ClassifyPortal("HTTP/1.1 302 Found\r\nLocation: https://login.hotelwifi.example/portal?x=1\r\n\r\n", out w) != 1 || w != "login.hotelwifi.example") throw new InvalidOperationException("redirect portal missed: " + w);
                if (DestCheck.ClassifyPortal("HTTP/1.1 200 OK\r\n\r\n<html>Welcome! Accept terms</html>", out w) != 1) throw new InvalidOperationException("replaced page missed");
                if (DestCheck.ClassifyPortal("HTTP/1.1 511 Network Authentication Required\r\n\r\n", out w) != 1) throw new InvalidOperationException("511 missed");
                if (DestCheck.ClassifyPortal("garbage", out w) != 2 || DestCheck.ClassifyPortal(null, out w) != 2) throw new InvalidOperationException("junk not 'can't tell'");
                if (DestCheck.ClassifyPortal("HTTP/1.1 302 Found\r\nLocation: javascript:alert(1)\r\n\r\n", out w) != 1 || w != "") throw new InvalidOperationException("odd Location shown: " + w);
            });
            T("settings round trip + presets", () =>
            {
                Cfg.ApplyPreset("penny"); int penny = Cfg.JobsPerMinute(); Cfg.ApplyPreset("engineer"); int eng = Cfg.JobsPerMinute(); Cfg.ApplyPreset("balanced"); int bal = Cfg.JobsPerMinute();
                if (!(penny < bal && bal < eng)) throw new InvalidOperationException("presets not ordered: " + penny + "/" + bal + "/" + eng);
                Cfg.Pins.Clear(); Cfg.Read("pin", "github.com"); Cfg.Read("pin", "rm -rf /"); Cfg.Read("pin", "https://jira.example.com/x?a=b"); Cfg.Read("gw", "999"); Cfg.Read("fade", "128");
                if (Cfg.Pins.Count != 2 || Cfg.GwSec != 5 || Cfg.Fade != 128) throw new InvalidOperationException("settings read wrong: pins=" + Cfg.Pins.Count + " gw=" + Cfg.GwSec + " fade=" + Cfg.Fade);
                var lines = Cfg.Lines().ToList(); Cfg.Pins.Clear(); Cfg.Fade = 77;
                foreach (var l in lines) { int eq = l.IndexOf('='); Cfg.Read(l.Substring(0, eq), l.Substring(eq + 1)); }
                if (Cfg.Pins.Count != 2 || Cfg.Fade != 128) throw new InvalidOperationException("round trip lost data");
                Cfg.Pins.Clear(); Cfg.Fade = 77;
                sb.AppendLine("      jobs a minute: penny " + penny + ", balanced " + bal + ", engineer " + eng);
            });
            T("Wi-Fi neighbours and path story parsers", () =>
            {
                string nets = "SSID 1 : HomeNet\r\n    Network type            : Infrastructure\r\n    Authentication          : WPA2-Personal\r\n    BSSID 1                 : aa:bb:cc:dd:ee:01\r\n         Signal             : 88%\r\n         Radio type         : 802.11ax\r\n         Band               : 5 GHz\r\n         Channel            : 36\r\n         Bss Load:\r\n             Connected Stations:        4\r\n             Channel Utilization:       52 (20 %)\r\n    BSSID 2                 : aa:bb:cc:dd:ee:02\r\n         Signal             : 40%\r\n         Channel            : 6\r\nSSID 2 : \r\n    BSSID 1                 : 11:22:33:44:55:66\r\n         Signal             : 70%\r\n         Channel            : 6\r\n";
                var aps = LinkInfo.ParseNetworks(nets);
                if (aps.Count != 3 || aps[0].Util != 20 || aps[0].Stations != 4 || aps[0].Band != "5 GHz" || aps[1].Band != "2.4 GHz" || aps[2].Ssid != "(hidden network)") throw new InvalidOperationException("netsh networks parser wrong");
                if (!LinkInfo.Overlaps(6, 9, "2.4 GHz") || LinkInfo.Overlaps(1, 6, "2.4 GHz") || LinkInfo.Overlaps(36, 40, "5 GHz")) throw new InvalidOperationException("channel overlap wrong");
                if (!LinkInfo.Blocked("Network shell commands need location permission to access WLAN information.")) throw new InvalidOperationException("location block not detected");
                if (DestCheck.OwnerOf("ae-1.r21.tor01.ca.bb.gin.ntt.net") != "NTT" || DestCheck.CityOf("ae-1.r21.tor01.ca.bb.gin.ntt.net") != "Toronto") throw new InvalidOperationException("router name reading wrong");
                if (DestCheck.OwnerOf("lo-0.gw1.example.co.uk") != "example.co.uk" || DestCheck.Kind("100.72.1.1") != "cgnat" || DestCheck.Kind("192.168.0.1") != "private" || DestCheck.Kind("8.8.8.8") != "public") throw new InvalidOperationException("address kinds wrong");
            });
            T("path story: NAT, carriers, handoffs, firewall", () =>
            {
                Func<string, double, Tuple<string, double, int>> H = (ip, ms) => Tuple.Create(ip, ms, 0);
                var hops = new List<Tuple<string, double, int>> { H("192.168.0.1", 2), H("10.0.0.1", 4), H("100.72.1.1", 9), H("24.156.1.1", 12), Tuple.Create((string)null, 0.0, 100),
                                                                  H("209.148.1.1", 14), H("129.250.2.1", 16), H("129.250.3.1", 88), H("140.82.114.4", 90) };
                var names = new Dictionary<string, string> { { "24.156.1.1", "8080-dgw01.ym.rogers.com" }, { "209.148.1.1", "3020-cgw01.tor.rogers.com" },
                    { "129.250.2.1", "ae-5.r20.tor01.ca.bb.gin.ntt.net" }, { "129.250.3.1", "ae-1.r24.asbnva02.us.bb.gin.ntt.net" }, { "140.82.114.4", "lb-140-82-114-4-iad.github.com" } };
                var r = new DestResult();
                DestCheck.Story(r, IPAddress.Parse("140.82.114.4"), hops, 8, "192.168.0.50", false, 1492, "", 20, 0, 90, 443, names);
                var chain = string.Join(" > ", r.Story.Select(x => x[0])); var all = string.Join(" | ", r.Findings.Select(f => f[0]));
                sb.AppendLine("      " + chain);
                foreach (var f in r.Findings) sb.AppendLine("      * " + Draw.Trunc(f[0], 110));
                if (chain != "Your PC > Your network > Carrier-grade NAT > Rogers > NTT > GitHub") throw new InvalidOperationException("segments wrong: " + chain);
                foreach (var want in new[] { "Double NAT", "Carrier-grade NAT", "Handoff at hop 7: Rogers → NTT", "PPPoE", "stayed silent", "between hop 7 and 8" })
                    if (all.IndexOf(want, StringComparison.Ordinal) < 0) throw new InvalidOperationException("missing finding: " + want);
                var r2 = new DestResult();
                DestCheck.Story(r2, IPAddress.Parse("140.82.114.4"), hops.Take(4).ToList(), -1, "192.168.0.50", false, 0, "refused", 0, 3, 90, 22, names);
                if (!r2.Findings.Any(f => f[0].Contains("closer than the server"))) throw new InvalidOperationException("nearby firewall reject not spotted");
            });
            T("deep parsers: Wi-Fi IEs, switch, event log, call quality", () =>
            {
                // HT 40 MHz + VHT 80 MHz + RM caps (11k) + mobility domain (11r) + ext caps with BSS transition (11v)
                var ies = new byte[] { 61, 22, 36, 0x05 }.Concat(new byte[20]).Concat(new byte[] { 192, 5, 1, 42, 0, 0, 0, 70, 5, 0, 0, 0, 0, 0, 54, 3, 0x12, 0x34, 0x01, 127, 3, 0, 0, 0x08 }).ToArray();
                var b = new WlanApi.Bss { FreqMHz = 5180 }; WlanApi.FromFreq(b); WlanApi.ReadIes(ies, b);
                if (b.Channel != 36 || b.Band != "5 GHz" || b.Width != 80 || !b.K || !b.V || !b.R) throw new InvalidOperationException("IE parser: ch " + b.Channel + " w " + b.Width + " k" + b.K + " v" + b.V + " r" + b.R);
                var b6 = new WlanApi.Bss { FreqMHz = 5975 }; WlanApi.FromFreq(b6); if (b6.Band != "6 GHz" || b6.Channel != 5) throw new InvalidOperationException("6 GHz channel wrong");
                var lldp = "Link Layer Discovery Protocol\r\n    Chassis Subtype = MAC address, Id: 00:11:22:33:44:55\r\n    Port Subtype = Interface name, Id: Gi1/0/12\r\n    Port Description = Desk 4B\r\n    System Name = floor3-sw1\r\n    System Description = Cisco IOS Software, C9300\r\n    Port VLAN Identifier: 120 (0x0078)\r\n";
                var si = LiveCapture.ParseSwitch(lldp);
                if (si == null || si.Name != "floor3-sw1" || si.Port != "Gi1/0/12" || si.Vlan != "120" || si.Proto != "LLDP") throw new InvalidOperationException("LLDP parser wrong");
                var cdp = LiveCapture.ParseSwitch("Cisco Discovery Protocol\r\n    Device ID: lab-sw2.example\r\n    Port ID: GigabitEthernet0/7\r\n    Native VLAN: 30\r\n    Platform: cisco WS-C2960X\r\n");
                if (cdp == null || cdp.Port != "GigabitEthernet0/7" || cdp.Vlan != "30" || cdp.Proto != "CDP") throw new InvalidOperationException("CDP parser wrong");
                if (LiveCapture.ParseSwitch("nothing here") != null) throw new InvalidOperationException("switch parser invented a switch");
                if (EvLog.Field("<EventData><Data Name='SSID'>Home &amp; Co</Data><Data Name='Reason'>The network is disconnected by the user.</Data></EventData>", "Reason") != "The network is disconnected by the user.") throw new InvalidOperationException("event field reader wrong");
                if (EvLog.Field("<Data Name=\"SSID\">Home &amp; Co</Data>", "SSID") != "Home & Co") throw new InvalidOperationException("event field decode wrong");
                if (LiveCapture.Mos(20, 2, 0) < 4.2 || LiveCapture.Mos(150, 40, 10) > 3.2) throw new InvalidOperationException("MOS model off");
                // 300 RTP packets at 50/s, 16 kHz clock, 3 lost -> ~1% loss, near-zero jitter
                var lc = new LiveCapture(); lc.SetLocalForTest(new[] { "10.0.0.82" });
                for (int i = 0; i < 300; i++)
                {
                    if (i == 100 || i == 101 || i == 200) continue;
                    var f = new string[21]; for (int j = 0; j < 21; j++) f[j] = "";
                    f[0] = "200"; f[1] = "eth:ethertype:ip:udp:rtp"; f[2] = "52.112.0.5"; f[3] = "10.0.0.82"; f[8] = "3480"; f[9] = "50020"; f[15] = "RTP";
                    f[16] = (i * 0.02).ToString("0.000", CultureInfo.InvariantCulture); f[17] = "0x1234"; f[18] = ((65500 + i) % 65536).ToString(CultureInfo.InvariantCulture); f[19] = (1000 + i * 320).ToString(CultureInfo.InvariantCulture);
                    lc.FeedForTest(string.Join("\t", f));
                }
                var v = lc.Snapshot(null);
                if (v.Calls.Count != 1) throw new InvalidOperationException("RTP stream not found");
                var c = v.Calls[0]; double loss = double.Parse(c[3].TrimEnd('%'), CultureInfo.CurrentCulture);
                sb.AppendLine("      call: " + string.Join(" · ", c.Take(6)));
                if (Math.Abs(loss - 1.0) > 0.2 || c[1] != "audio") throw new InvalidOperationException("RTP loss/kind wrong: " + string.Join(",", c));
            });
            T("process file paths without admin", () =>
            {
                int pid; using (var me = Process.GetCurrentProcess()) pid = me.Id;
                string path = Native.ExePath(pid); var f = Explain.FileFacts(path);
                sb.AppendLine("      own exe: " + (path != "" ? Path.GetFileName(path) : "(hidden)") + " · lives in: " + f[3]);
                if (path == "") throw new InvalidOperationException("could not read own path");
            });
            T("timeline, Wi-Fi roam and hints (simulated)", () =>
            {
                var sim = new Monitor(); var log = new StringBuilder();
                string h = sim.SimulateForTest(log); sb.Append(log); sb.AppendLine("      hint: " + h);
                var all = string.Join("\n", sim.Events.Recent(50).Select(e => e.Text));
                foreach (var must in new[] { "VPN connected", "VPN d", "DNS server changed", "IP address changed", "Switched to LAN", "another access point", "signal fell to 40%", "link rate dropped 866", "Wi-Fi d" })
                    if (all.IndexOf(must, StringComparison.Ordinal) < 0) throw new InvalidOperationException("missing event: " + must);
                if (h.IndexOf("Wi-Fi signal fell to 40%", StringComparison.Ordinal) < 0) throw new InvalidOperationException("wrong hint: " + h);
            });
            T("Ctrl+Alt+A shortcut is free", () =>
            {
                bool running = Process.GetProcessesByName("AdeeboMeter").Length > 1;
                if (running) { sb.AppendLine("      skipped: AdeeboMeter is running and holds it"); return; }
                using (var hk = new HotKey(delegate { })) if (!hk.Ok) throw new InvalidOperationException("another app already uses Ctrl+Alt+A");
            });
            T("memory after everything", () =>
            {
                GC.Collect(); GC.WaitForPendingFinalizers();
                using (var p = Process.GetCurrentProcess()) { long mb = p.PrivateMemorySize64 / 1048576; sb.AppendLine("      private " + mb + " MB (budget 64)"); if (mb > 96) throw new InvalidOperationException(mb + " MB"); }
            });
            T("install: paths, settings line, icon", () =>
            {
                if (!Setup.Under(Path.Combine(Setup.Base, "1.0"), Setup.Base) || Setup.Under(Path.GetTempPath(), Setup.Base)) throw new InvalidOperationException("Under()");
                if (Setup.Under(Setup.Base + "-evil", Setup.Base)) throw new InvalidOperationException("prefix trick accepted");
                string f = Path.Combine(Path.GetTempPath(), "AdeeboMeter-test-" + Guid.NewGuid().ToString("N") + ".settings");
                try
                {
                    File.WriteAllLines(f, new[] { "widget=1", "autostart=1" });
                    Setup.SetLine(f, "autostart", "0"); Setup.SetLine(f, "serious", "1");
                    var l = File.ReadAllLines(f);
                    if (!l.Contains("autostart=0") || !l.Contains("serious=1") || !l.Contains("widget=1") || l.Length != 3) throw new InvalidOperationException(string.Join("|", l));
                }
                finally { try { File.Delete(f); } catch { } }
                var ico = Theme.IcoBytes();
                if (ico.Length < 2000 || ico[2] != 1 || ico[4] != 9) throw new InvalidOperationException("ico header");
                sb.AppendLine("      installs to " + Setup.Base + " · icon " + ico.Length / 1024 + " KB");
            });
            T("GDI / user handles", () =>
            {
                using (var p = Process.GetCurrentProcess()) sb.AppendLine("      handles=" + p.HandleCount);
            });
            sb.AppendLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
            try { File.WriteAllText(Path.Combine(Program.Dir, "AdeeboMeter-selftest.txt"), sb.ToString()); } catch { }
            M.Stop();
            return fails;
        }

        static void AppendStory(StringBuilder sb, DestResult r)
        {
            if (r.Story == null) return;
            sb.AppendLine("  [PATH STORY]");
            sb.AppendLine("     " + string.Join("  ->  ", r.Story.Select(x => x[0] + " (" + x[1] + ")")));
            foreach (var f in r.Findings ?? new List<string[]>()) sb.AppendLine("     * " + f[0]);
        }
        public static int CheckOnce(string dest)
        {
            var M = new Monitor(); M.RefreshInterfaces(); var r = new DestResult();
            DestCheck.Run(dest, r, delegate { }, M);
            var sb = new StringBuilder(dest + "\nverdict: " + r.Verdict + "\n");
            for (int i = 0; i < 8; i++) sb.AppendLine("  " + DestResult.Steps[i].PadRight(9) + " lv=" + r.Lv[i] + "  " + r.Val[i]);
            foreach (var c in r.CardsCopy()) { sb.AppendLine("  [" + c.Key + "]"); foreach (var row in c.Value) sb.AppendLine("     " + row[0] + ": " + (row.Length > 1 ? row[1] : "")); }
            AppendStory(sb, r);
            File.AppendAllText(Path.Combine(Program.Dir, "AdeeboMeter-check.txt"), sb.ToString() + "\n");
            return 0;
        }

        // Feeds tshark field lines from a file through the live-capture parser and prints what the Live tab would show.
        public static int ParseTest(string file)
        {
            var lc = new LiveCapture(); var sb = new StringBuilder(); var sw = Stopwatch.StartNew(); int n = 0;
            foreach (var line in File.ReadLines(file)) { lc.FeedForTest(line); n++; }
            var v = lc.Snapshot(null);
            sb.AppendLine(n + " lines parsed in " + sw.ElapsedMilliseconds + " ms");
            foreach (var r in v.Phs) sb.AppendLine(new string(' ', int.Parse(r[0], CultureInfo.InvariantCulture) * 2) + r[1] + "  " + r[2] + "  " + r[3]);
            sb.AppendLine("-- top conversations");
            foreach (var c in v.Top) sb.AppendLine(c.Host + "  " + c.Proto + "  " + c.Remote + ":" + c.RemotePort + "  bytes=" + c.Bytes + " retx=" + c.Retx);
            sb.AppendLine("-- expert");
            foreach (var e in v.Expert) sb.AppendLine(e[1] + " = " + e[2]);
            File.WriteAllText(Path.Combine(Program.Dir, "AdeeboMeter-parsetest.txt"), sb.ToString());
            return 0;
        }
    }
}
