// AdeeboMeter - a small, quiet Windows companion for "is it me or the network?". Created by Adeeb Khan.
// .NET Framework 4.x, C# 5: builds with the csc.exe that ships with Windows (build_exe.cmd), no admin.
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
[assembly: SuppressMessage("Performance", "CA1865", Scope = "module", Justification = ".NET Framework has no string.StartsWith(char)")]
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
[assembly: System.Reflection.AssemblyTitle("AdeeboMeter")]
[assembly: System.Reflection.AssemblyProduct("AdeeboMeter")]
[assembly: System.Reflection.AssemblyDescription("Is it me or the network? An offline Windows network and PC companion.")]
[assembly: System.Reflection.AssemblyCopyright("MIT License")]
[assembly: System.Reflection.AssemblyVersion(AdeeboMeter.Program.FileVersion)]
[assembly: System.Reflection.AssemblyFileVersion(AdeeboMeter.Program.FileVersion)]
[assembly: System.Reflection.AssemblyInformationalVersion(AdeeboMeter.Program.Version)]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]   // web requests use Windows' TLS 1.2 / 1.3 defaults

namespace AdeeboMeter
{
    // ================================================================== entry
    static class Program
    {
        public static string Dir;
        public const string Version = "1.1.3";
        public const string FileVersion = "1.1.3.0";   // Windows wants four numbers - keep it in step with Version (the self-test checks)
        public static bool DllSafety, IsWine;   // true once DLLs can only come from System32
        public const string Tagline = "is it me or the network?";
        public static int ForceTheme = -1;
        public static string StartView, StartTip, StartDest;   // debug: --view <page> [--tip <key>] [--dest <host>] opens the engineer view there (used for screenshots)   // -1 follow Windows, 0 light, 1 dark
        public static bool WantDark() { return ForceTheme >= 0 ? ForceTheme == 1 : Theme.SystemDark(); }

        [STAThread]
        static int Main(string[] args)
        {
            // DLL planting: a downloaded exe often runs from Downloads, where a file named like a Windows DLL could be
            // waiting. From here on, Windows loads DLLs for this process only from System32 (the app's own folder is never searched).
            // (skipped under Wine, whose system DLLs don't live in a real System32 - only the test lab runs there)
            try { IsWine = Native.GetProcAddress(Native.GetModuleHandle("ntdll.dll"), "wine_get_version") != IntPtr.Zero; } catch { }
            if (!IsWine) try { DllSafety = Native.SetDefaultDllDirectories(0x800 /*LOAD_LIBRARY_SEARCH_SYSTEM32*/) && Native.SetDllDirectory(""); } catch { DllSafety = false; }
            Dir = Path.GetDirectoryName(Application.ExecutablePath);
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log("UI error: " + e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log("Fatal: " + e.ExceptionObject); Crash.Write(e.ExceptionObject); };

            if (args.Any(a => string.Equals(a, "--dark", StringComparison.OrdinalIgnoreCase))) ForceTheme = 1;
            if (args.Any(a => string.Equals(a, "--light", StringComparison.OrdinalIgnoreCase))) ForceTheme = 0;
            if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))) return SelfTest.Run();
            if (args.Any(a => string.Equals(a, "--perf", StringComparison.OrdinalIgnoreCase))) return SelfTest.Perf();
            int mt = Array.FindIndex(args, a => string.Equals(a, "--mibtest", StringComparison.OrdinalIgnoreCase));
            if (mt >= 0 && mt + 1 < args.Length) return SelfTest.MibTest(args[mt + 1]);
            if (args.Any(a => string.Equals(a, "--deeptest", StringComparison.OrdinalIgnoreCase)))
            {
                int mi = Array.FindIndex(args, a => a == "--minutes"), min; if (mi < 0 || mi + 1 >= args.Length || !int.TryParse(args[mi + 1], out min)) min = 20;
                return SelfTest.Deep(!args.Any(a => string.Equals(a, "--local", StringComparison.OrdinalIgnoreCase)), Math.Max(2, Math.Min(60, min)));
            }
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

        public static string DataDir
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
        public static volatile string LastCapture = "";   // the newest capture this session - "Keep this capture" copies it out before exit deletes it
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
        public static volatile bool IpNames = true, Portal = true, SeenTip, Details, BigWidget;   // BigWidget: the corner widget drawn 1.3x   // Details: the engineer toggle - raw values, sources and times on every finding
        // history on by default (kept HistoryDays, rolling), alerts with quiet hours, watch-and-suggest, data cap, pinned watch
        public static volatile bool History = true, Alerts = true, Quiet = true, Watch = true, PinWatch;
        public static volatile int DevAlerts;   // new-device pop-ups: 0 off (default - they're still listed), 1 home networks only
        public static volatile int HistoryDays = 3, CapGB, OnlineMin = 30;   // OnlineMin: online lookups switch themselves off after this
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
            yield return "call=" + (Call ? 1 : 0); yield return "ipnames=" + (IpNames ? 1 : 0); yield return "portal=" + (Portal ? 1 : 0); yield return "seentip=" + (SeenTip ? 1 : 0); yield return "details=" + (Details ? 1 : 0); yield return "bigwidget=" + (BigWidget ? 1 : 0); yield return "history2=" + (History ? 1 : 0); yield return "histdays=" + HistoryDays;
            yield return "alerts=" + (Alerts ? 1 : 0); yield return "quiet=" + (Quiet ? 1 : 0); yield return "devalerts=" + DevAlerts; yield return "watch=" + (Watch ? 1 : 0); yield return "capgb=" + CapGB; yield return "onlinemin=" + OnlineMin; yield return "pinwatch=" + (PinWatch ? 1 : 0);
            yield return "livemin=" + LiveMin; yield return "sniffsec=" + SniffSec; yield return "fade=" + Fade; yield return "dwell=" + DwellMs;
            foreach (var p in Pins) yield return "pin=" + p;
        }
        public static readonly int[] OnlineChoices = { 5, 15, 30, 60, 120, 180 }, HistDayChoices = { 1, 3, 7, 14, 30, 60, 90 }, CapChoices = { 0, 10, 25, 50, 100, 250, 500 };
        public static readonly HashSet<string> Known = new HashSet<string> { "preset", "gw", "net", "wifi", "apps", "call", "ipnames", "portal", "seentip", "details", "bigwidget", "history2", "histdays", "alerts", "quiet", "devalerts", "watch", "capgb", "onlinemin", "pinwatch", "livemin", "sniffsec", "fade", "dwell", "pin", "autostart", "widget", "serious", "theme" };
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
                case "details": Details = v == "1"; break;
                case "bigwidget": BigWidget = v == "1"; break;
                case "portal": Portal = v != "0"; break;
                case "seentip": SeenTip = v == "1"; break;
                case "history2": History = v == "1"; break;     // (the old "history" key from the first preview is ignored: history is on by default)
                case "histdays": HistoryDays = Pick(v, 3, HistDayChoices); break;
                case "alerts": Alerts = v != "0"; break;
                case "quiet": Quiet = v != "0"; break;
                case "devalerts": DevAlerts = v == "1" ? 1 : 0; break;
                case "watch": Watch = v != "0"; break;
                case "capgb": CapGB = Pick(v, 0, CapChoices); break;
                case "onlinemin": OnlineMin = Pick(v, 30, OnlineChoices); break;
                case "pinwatch": PinWatch = v == "1"; break;
                case "livemin": LiveMin = Pick(v, 15, 5, 15, 30); break;
                case "sniffsec": SniffSec = Pick(v, 30, 30, 60); break;
                case "fade": Fade = Pick(v, 77, 51, 77, 128, 191, 255); break;
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
        public static string Sys(string exe) { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exe); }

        // Runs a fixed program with fixed arguments (never through cmd or PowerShell) and returns its output.
        public static string Run(string exe, string args, int timeoutMs)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true; psi.CreateNoWindow = true;
            psi.WorkingDirectory = Path.GetDirectoryName(exe);   // never the folder AdeeboMeter runs from (Downloads): a child could load a planted DLL from there
            if (Path.GetFileName(exe).Equals("tshark.exe", StringComparison.OrdinalIgnoreCase)) { psi.StandardOutputEncoding = System.Text.Encoding.UTF8; psi.StandardErrorEncoding = System.Text.Encoding.UTF8; }   // tshark writes UTF-8 (→, names); Windows' own tools write the console code page
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
        public static string Red(string fun, string plain) { return plain; }
        public static string ByLevel(int level, string fun, string plain) { return level >= 2 ? plain : P(fun, plain); }   // a real problem is never a joke - the plain words, whatever the voice
        public static string Greet()
        {
            int h = DateTime.Now.Hour;
            return h < 5 ? "Still up, boss?" : h < 12 ? "Good morning, boss." : h < 17 ? "Good afternoon, boss." : "Good evening, boss.";
        }
    }
}
