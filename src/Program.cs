// AdeeboMeter - a small, quiet Windows companion for "is it me or the network?". Created by Adeeb Khan.
// .NET Framework 4.x, C# 5: builds with the csc.exe that ships with Windows (build_exe.cmd), no admin.
// No cloud. No telemetry. Never listens. Every packet it sends is listed in About ("What leaves your PC") - and you can switch
// each background one off. (It never listens on any port, never updates itself, never downloads anything.)
// In the background (Balanced, the default - Settings changes the pace): a ping to your gateway every 5 s; a ping to 1.1.1.1 and
// one DNS lookup every 30 s (TCP 443 to it instead when ping never answers); once per network a 6-hop trace toward 1.1.1.1 with
// reverse-DNS names (to name your provider); and, only if you turn them on, one ping to your first pinned destination every 30 s
// and your added games' servers. Everything else - every Toolbox tool, the speed test, the ISP report, the Steam server list,
// Check a destination - runs only when you press it; when a check you started fails, Windows' own test page
// (msftconnecttest.com) is asked once (Settings can turn that off).
// Settings and the log live next to the exe (a Store or portable copy: in its data folder); history, usage, baselines, known
// devices and seen apps live in %LOCALAPPDATA%\AdeeboMeter (History settings delete them); captures are deleted on exit.

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
    // A clock for intervals that never jumps: setting the PC's clock back (dual boot, NTP after sleep, by hand) must not
    // freeze the probes or the sampler. Use it for "how long since" and "when is the next run" - never as a time of day.
    static class Clock
    {
        static readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        static readonly DateTime start = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static DateTime Mono { get { return start + sw.Elapsed; } }
    }
    // ================================================================== entry
    static class Program
    {
        public static string Dir;
        // Installed from the Microsoft Store (an MSIX package): its own folder is read-only and Windows / the Store install,
        // update, start and remove it - so settings and reports go to AppData and the self-install / Run key are left alone.
        public static bool Packaged;
        // where settings and the log go: next to the exe when that folder can be written; a Store copy, or an exe in a
        // read-only place (Program Files, a network share, a locked-down PC), uses %LOCALAPPDATA%\AdeeboMeter instead -
        // so settings are kept and the install offer doesn't come back at every start
        static string writeDir; static int dirWritable = -1;
        static bool DirWritable { get { if (dirWritable < 0) dirWritable = CanWrite(Dir) ? 1 : 0; return dirWritable == 1; } }
        public static string WriteDir { get { if (writeDir == null) writeDir = Packaged || !DirWritable || InDownloads ? LocalDir : Dir; return writeDir; } }
        // An exe still sitting in Downloads: anything else in that folder may have come from any website, so files beside it
        // (a portable marker, a settings file with game "consent") aren't trusted - settings go to AppData instead.
        static int inDownloads = -1;
        public static bool InDownloads
        {
            get
            {
                if (inDownloads >= 0) return inDownloads == 1;
                inDownloads = 0;
                try
                {
                    if (string.IsNullOrEmpty(Dir)) return false;
                    var dl = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") };
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
                    { var v = k == null ? null : k.GetValue("{374DE290-123F-4565-9164-39C4925E467B}") as string; if (!string.IsNullOrEmpty(v)) dl.Add(Environment.ExpandEnvironmentVariables(v)); }
                    string here = Path.GetFullPath(Dir).TrimEnd('\\', '/');
                    if (dl.Any(d => string.Equals(Path.GetFullPath(d).TrimEnd('\\', '/'), here, StringComparison.OrdinalIgnoreCase))) inDownloads = 1;
                }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is ArgumentException || e is NotSupportedException)) throw; }
                return inDownloads == 1;
            }
        }
        static string LocalDir { get { string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdeeboMeter"); try { Directory.CreateDirectory(d); } catch (IOException) { } catch (UnauthorizedAccessException) { } return d; } }
        public static bool CanWrite(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            if (dir.Length > 200) return false;   // a deep folder: the settings, log and their .tmp files would pass Windows' 260-character limit
            try { string f = Path.Combine(dir, ".adeebometer-write-test-" + Guid.NewGuid().ToString("N")); File.WriteAllText(f, ""); File.Delete(f); return true; }
            catch (Exception e) { if (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException || e is System.Security.SecurityException) return false; throw; }
        }
        static bool DetectPackaged()
        {
            try { int n = 0; return Native.GetCurrentPackageFullName(ref n, null) == 122; }   /* 122 = buffer too small: there IS a package; 15700 = none */
            catch (EntryPointNotFoundException) { return false; }
        }
        public const string Version = "1.1.4";
        public const string FileVersion = "1.1.4.0";   // Windows wants four numbers - keep it in step with Version (the self-test checks)
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
            Dir = Path.GetDirectoryName(Application.ExecutablePath); Packaged = DetectPackaged();
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log("UI error: " + e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log("Fatal: " + e.ExceptionObject); Crash.Write(e.ExceptionObject); };

            if (args.Any(a => string.Equals(a, "--dark", StringComparison.OrdinalIgnoreCase))) ForceTheme = 1;
            if (args.Any(a => string.Equals(a, "--light", StringComparison.OrdinalIgnoreCase))) ForceTheme = 0;
            if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))) return SelfTest.Run();
            if (args.Any(a => string.Equals(a, "--perf", StringComparison.OrdinalIgnoreCase))) return SelfTest.Perf();
            int ri = Array.FindIndex(args, a => string.Equals(a, "--report", StringComparison.OrdinalIgnoreCase));
            if (ri >= 0) { if (ri + 1 >= args.Length) return 3; int si = Array.FindIndex(args, a => string.Equals(a, "--seconds", StringComparison.OrdinalIgnoreCase)), sec; if (si < 0 || si + 1 >= args.Length || !int.TryParse(args[si + 1], out sec)) sec = 15; return Report.Run(args[ri + 1], sec); }
            int mt = Array.FindIndex(args, a => string.Equals(a, "--mibtest", StringComparison.OrdinalIgnoreCase));
            if (mt >= 0 && mt + 1 < args.Length) return SelfTest.MibTest(args[mt + 1]);
            if (args.Any(a => string.Equals(a, "--deeptest", StringComparison.OrdinalIgnoreCase)))
            {
                int mi = Array.FindIndex(args, a => string.Equals(a, "--minutes", StringComparison.OrdinalIgnoreCase)), min; if (mi < 0 || mi + 1 >= args.Length || !int.TryParse(args[mi + 1], out min)) min = 20;
                return SelfTest.Deep(!args.Any(a => string.Equals(a, "--local", StringComparison.OrdinalIgnoreCase)), Math.Max(2, Math.Min(60, min)));
            }
            int vi = Array.FindIndex(args, a => string.Equals(a, "--view", StringComparison.OrdinalIgnoreCase)); if (vi >= 0 && vi + 1 < args.Length) StartView = args[vi + 1];
            int tpi = Array.FindIndex(args, a => string.Equals(a, "--tip", StringComparison.OrdinalIgnoreCase)); if (tpi >= 0 && tpi + 1 < args.Length) StartTip = args[tpi + 1];
            int dsi = Array.FindIndex(args, a => string.Equals(a, "--dest", StringComparison.OrdinalIgnoreCase)); if (dsi >= 0 && dsi + 1 < args.Length) StartDest = args[dsi + 1];
            int ci = Array.FindIndex(args, a => string.Equals(a, "--check", StringComparison.OrdinalIgnoreCase));
            if (ci >= 0 && ci + 1 < args.Length) return SelfTest.CheckOnce(args[ci + 1]);
            int pi = Array.FindIndex(args, a => string.Equals(a, "--parse-test", StringComparison.OrdinalIgnoreCase));
            if (pi >= 0 && pi + 1 < args.Length) return SelfTest.ParseTest(args[pi + 1]);
            int rf = Array.FindIndex(args, a => string.Equals(a, "--remove-folder", StringComparison.OrdinalIgnoreCase)); if (rf >= 0 && rf + 1 < args.Length) return Setup.RemoveFolder(args[rf + 1]);
            // the uninstaller's helper can't delete itself: any later start tidies it away
            try { string helper = Path.Combine(Path.GetTempPath(), "AdeeboMeter-remove.exe"); if (File.Exists(helper)) File.Delete(helper); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            // test lab: draw the corner widget's card in a given style to a PNG (no window) - --render-card calls out.png
            int rc = Array.FindIndex(args, a => string.Equals(a, "--render-card", StringComparison.OrdinalIgnoreCase));
            if (rc >= 0 && rc + 2 < args.Length && args[rc + 2].EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                Cfg.WidgetStyle = args[rc + 1]; Cfg.Mode = "pro"; Draw.Init(1f); var mon = new Monitor(); mon.RefreshInterfaces(); mon.Start(); Thread.Sleep(8000);
                var snap = mon.Latest ?? new Snap(); using (var bmp = Draw.Card(snap, new Dictionary<string, System.Drawing.RectangleF>())) bmp.Save(args[rc + 2], System.Drawing.Imaging.ImageFormat.Png);
                mon.Stop(); return 0;
            }
            int wi = Array.FindIndex(args, a => string.Equals(a, "--write-icon", StringComparison.OrdinalIgnoreCase)); if (wi >= 0 && wi + 1 < args.Length) { try { File.WriteAllBytes(args[wi + 1], Theme.IcoBytes()); return 0; } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)) throw; Console.Error.WriteLine("--write-icon: " + e.Message); return 2; } }

            // A downloaded copy offers to install itself; --uninstall comes from Settings > Apps or the tray menu.
            bool uninstall = args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase));
            if (!Packaged && (uninstall || ((args.Length == 0 || args.Any(a => string.Equals(a, "--offer", StringComparison.OrdinalIgnoreCase))) && Setup.ShouldOffer()) || args.Any(a => string.Equals(a, "--offer", StringComparison.OrdinalIgnoreCase))))
            {
                bool here;
                using (var w = new SetupWindow(uninstall)) { Application.Run(w); here = w.RunHere; }
                if (!here) return 0;
            }

            bool created;
            var mtx = new Mutex(true, "Local\\AdeeboMeter.single", out created);
            if (!created)
            {
                // an OLDER copy is running and this one is newer (a fresh download, maybe renamed "AdeeboMeter (1).exe"):
                // ask it to step aside and take over, instead of opening the old one and leaving you thinking you updated
                if (Single.RunningIsOlder())
                {
                    try { using (var q = EventWaitHandle.OpenExisting(Single.QuitName)) q.Set(); } catch (WaitHandleCannotBeOpenedException) { } catch (UnauthorizedAccessException) { }
                    try { if (mtx.WaitOne(6000)) created = true; } catch (AbandonedMutexException) { created = true; }
                }
            }
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
        // The log: one line per message (newlines from network text flattened), invariant dates, the same message at most
        // once a minute (a stuck error can't write 20 lines a second), and at 256 KB it rolls to .log.1 instead of losing everything
        // tmp -> dst in one step where the drive allows it; on shares and USB sticks where File.Replace isn't supported,
        // delete and move instead (a moment without the file beats never saving)
        public static void ReplaceFile(string tmp, string dst)
        {
            if (!File.Exists(dst)) { File.Move(tmp, dst); return; }
            try { File.Replace(tmp, dst, null); }
            catch (IOException) { File.Delete(dst); File.Move(tmp, dst); }
            catch (PlatformNotSupportedException) { File.Delete(dst); File.Move(tmp, dst); }
        }
        static string lastLogMsg = ""; static DateTime lastLogAt = DateTime.MinValue; static int lastLogRepeats;
        // Text that came from outside (a Wi-Fi name, a program's description): without invisible format characters - a
        // right-to-left override or zero-width joiner can make one name look like another, or move "(you)" to the wrong row
        public static string Plain(string t)
        {
            if (string.IsNullOrEmpty(t)) return t ?? "";
            bool any = false; foreach (char c in t) if (char.GetUnicodeCategory(c) == UnicodeCategory.Format || (char.IsControl(c) && c != '\t')) { any = true; break; }
            return any ? new string(t.Where(c => char.GetUnicodeCategory(c) != UnicodeCategory.Format && !char.IsControl(c)).ToArray()) : t;
        }
        // Copy a secret (a generated code): kept out of Win+V history and Cloud Clipboard, and out of clipboard monitors
        public static void CopySecret(string text)
        {
            var d = new System.Windows.Forms.DataObject(); d.SetData(System.Windows.Forms.DataFormats.UnicodeText, text);
            d.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[] { 1, 0, 0, 0 }));
            d.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4]));
            d.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4]));
            System.Windows.Forms.Clipboard.SetDataObject(d, false);
        }
        public static void Log(string m)
        {
            try
            {
                m = (m ?? "").Replace("\r", " ").Replace("\n", " ");
                lock (logLock)
                {
                    if (m == lastLogMsg && (Clock.Mono - lastLogAt).TotalSeconds < 60) { lastLogRepeats++; return; }
                    string extra = lastLogRepeats > 0 ? " (the line before repeated " + lastLogRepeats + " more times)" : "";
                    lastLogMsg = m; lastLogAt = Clock.Mono; lastLogRepeats = 0;
                    string p = Path.Combine(WriteDir, "AdeeboMeter.log");
                    if (File.Exists(p) && new FileInfo(p).Length > 256 * 1024) { string old = p + ".1"; if (File.Exists(old)) File.Delete(old); File.Move(p, old); }
                    File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + m + extra + Environment.NewLine);
                }
            }
            catch { }
        }

        static int portable = -1;
        public static bool Portable { get { if (portable < 0) { try { portable = !string.IsNullOrEmpty(Dir) && !InDownloads && File.Exists(Path.Combine(Dir, "AdeeboMeter.portable")) ? 1 : 0; } catch (IOException) { portable = 0; } } return portable == 1; } }
        public static string DataDir
        {
            get
            {
                // portable / test copy: an empty file named AdeeboMeter.portable next to the exe keeps all data in a "data"
                // folder beside it - a second copy (a USB stick, a test build) never mixes with the installed one's history
                string d = Portable && DirWritable ? Path.Combine(Dir ?? "", "data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdeeboMeter");   // a portable copy on read-only media falls back too
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
        // every capture left from before - the ring, sniff folders, journeys - even after a crash, a kill, a power cut or a
        // logoff (full-payload traffic must not sit on disk). Runs at start and when Windows is ending the session.
        public static void CleanCaptures()
        {
            try
            {
                foreach (var f in Directory.GetFiles(CaptureDir, "*.pcap*", SearchOption.AllDirectories)) try { File.Delete(f); } catch { }
                foreach (var d in Directory.GetDirectories(CaptureDir)) try { if (!Directory.EnumerateFileSystemEntries(d, "*", SearchOption.AllDirectories).Any(x => File.Exists(x))) Directory.Delete(d, true); } catch { }
            }
            catch { }
        }
    }

    // Second launch asks the running copy to open its engineer view; a stuck copy can be replaced.
    // ================================================================== Settings the user controls (Settings page / tray). 0 seconds = off.
    static class Cfg
    {
        // background work while only the widget runs
        public static volatile int GwSec = 5, NetSec = 30, WifiSec = 30, AppsSec = 10;   /* NetSec 30 on a fresh install is deliberate (Adeeb, 2026-11-09): the Internet dot pings 1.1.1.1 / 8.8.8.8 - outbound only, nothing is ever accepted; 0 in Settings turns it fully off */
        public static volatile bool Call = true;
        // engineer view (only while it's open)
        public static volatile string WidgetScreen = "";   // the screen the widget sits on ("" = the main screen); falls back to the main one if it's gone
        public static volatile bool IpNames = true, Portal = true, SeenTip, Details, BigWidget;   // BigWidget: the corner widget drawn 1.3x   // Details: the engineer toggle - raw values, sources and times on every finding
        // history on by default (kept HistoryDays, rolling), alerts with quiet hours, watch-and-suggest, data cap, pinned watch
        public static volatile bool History = true, Alerts = true, Quiet = true, Watch = true, PinWatch;
        public static volatile int DevAlerts;   // new-device pop-ups: 0 off (default - they're still listed), 1 home networks only
        public static volatile int HistoryDays = 3, CapGB, OnlineMin = 30;   // OnlineMin: online lookups switch themselves off after this
        public static volatile int LiveMin = 15, SniffSec = 30;
        // widget
        public static volatile int Fade = 77, DwellMs = 450;
        // Gamer Zone: GameIds = games the user clicked (consent to contact their servers); background ping every GameBgSec
        // to up to GameBgServers servers; every second while a game is open (GameFast); history kept GameDays
        public static string GameIds = "";
        public static volatile int GameBgSec = 30, GameBgServers = 3, GameDays = 14;
        public static volatile bool GameFast = true, GameAuto = true, GameWidget = true;
        public static volatile string WidgetStyle = "auto";
        // Simple (the dots, What's wrong?, speed, history, Gamer Zone) or Pro (every tab). "" = not chosen yet (a new install:
        // shown as Simple with the choice on the home page); an upgrade from 1.1.4 keeps Pro - nothing it had disappears
        public static volatile string Mode = "", Lang = "en", TbView = "q";   // TbView: the Toolbox by question ("q") or the classic list ("list")   // Lang: the UI language (L.Languages)
        public static volatile bool Points = true;   // Adeebo Points (Settings): on from day 1; off hides the button and pauses earning, the total is kept
        public static volatile string SpeedServer = "auto";   // the speed test's server: "auto" (Cloudflare, nearest) or an id from the list
        public static bool Simple { get { return Mode != "pro"; } }   // auto (follows what you are doing) / everyday / gamer
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
        public static int JobsPerMinute() { return JobsPerMinute(GwSec, NetSec, WifiSec, AppsSec); }
        static int JobsPerMinute(int gw, int net, int wifi, int apps)
        {
            Func<int, double> per = delegate(int sec) { return sec > 0 ? 60.0 / sec : 0; };
            return (int)Math.Round(6 /*adapters*/ + per(gw) + 2 * per(net) + per(wifi) + 2 * per(apps));
        }
        // what a preset would cost, worked out without touching the live settings the monitor reads
        public static int JobsForPreset(string p)
        {
            if (p == "penny") return JobsPerMinute(10, 60, 120, 30);
            if (p == "engineer") return JobsPerMinute(2, 15, 10, 5);
            return JobsPerMinute(5, 30, 30, 10);
        }
        public static IEnumerable<string> Lines()
        {
            yield return "preset=" + Preset;
            yield return "gw=" + GwSec; yield return "net=" + Policy.Keep("net", NetSec.ToString(CultureInfo.InvariantCulture)); yield return "wifi=" + WifiSec; yield return "apps=" + AppsSec;
            yield return "call=" + (Call ? 1 : 0); yield return "ipnames=" + (IpNames ? 1 : 0); yield return "portal=" + (Portal ? 1 : 0); yield return "seentip=" + (SeenTip ? 1 : 0); yield return "details=" + (Details ? 1 : 0); yield return "bigwidget=" + (BigWidget ? 1 : 0); yield return "history2=" + (History ? 1 : 0); yield return "histdays=" + Policy.Keep("histdays", HistoryDays.ToString(CultureInfo.InvariantCulture));
            yield return "alerts=" + (Alerts ? 1 : 0); yield return "quiet=" + (Quiet ? 1 : 0); yield return "devalerts=" + DevAlerts; yield return "watch=" + (Watch ? 1 : 0); yield return "capgb=" + CapGB; yield return "onlinemin=" + OnlineMin; yield return "pinwatch=" + (PinWatch ? 1 : 0);
            yield return "wscreen=" + WidgetScreen;
            yield return "gmon=" + Policy.Keep("gmon", GameIds); yield return "gbg=" + GameBgSec; yield return "gsrv=" + GameBgServers; yield return "gdays=" + GameDays;
            yield return "gfast=" + (GameFast ? 1 : 0); yield return "gauto=" + (GameAuto ? 1 : 0); yield return "gwidget=" + (GameWidget ? 1 : 0); yield return "wstyle=" + WidgetStyle; yield return "mode=" + Policy.Keep("mode", Mode); yield return "lang=" + Lang; yield return "tbview=" + TbView; yield return "speedsrv=" + SpeedServer; yield return "points=" + (Points ? 1 : 0);
            yield return "livemin=" + LiveMin; yield return "sniffsec=" + SniffSec; yield return "fade=" + Fade; yield return "dwell=" + DwellMs;
            foreach (var p in Pins) yield return "pin=" + p;
        }
        public static readonly int[] OnlineChoices = { 5, 15, 30, 60, 120, 180 }, HistDayChoices = { 1, 3, 7, 14, 30, 60, 90 }, CapChoices = { 0, 10, 25, 50, 100, 250, 500 };
        public static readonly HashSet<string> Known = new HashSet<string> { "preset", "gw", "net", "wifi", "apps", "call", "ipnames", "portal", "seentip", "details", "bigwidget", "wscreen", "history2", "histdays", "alerts", "quiet", "devalerts", "watch", "capgb", "onlinemin", "pinwatch", "livemin", "sniffsec", "fade", "dwell", "pin", "gmon", "gbg", "gsrv", "gdays", "gfast", "gauto", "gwidget", "wstyle", "mode", "lang", "tbview", "speedsrv", "points", "autostart", "widget", "serious", "theme" };
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
                case "wscreen": WidgetScreen = v.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase) && v.Length < 40 ? v : ""; break;
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
                case "gmon": GameIds = string.Join(",", v.Split(',').Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0 && x.Length <= 40 && x.All(ch => char.IsLetterOrDigit(ch) || ch == '-')).Distinct().Take(20)); break;
                case "gbg": GameBgSec = Pick(v, 30, 10, 30, 60, 0); break;
                case "gsrv": GameBgServers = Pick(v, 3, 1, 3, 5); break;
                case "gdays": GameDays = Pick(v, 14, 7, 14, 30); break;
                case "gfast": GameFast = v != "0"; break;
                case "gauto": GameAuto = v != "0"; break;
                case "gwidget": GameWidget = v != "0"; break;
                case "points": Points = v == "1"; break;
                case "speedsrv": SpeedServer = System.Text.RegularExpressions.Regex.IsMatch(v ?? "", "^[a-z0-9-]{1,40}$") ? v : "auto"; break;
                case "tbview": TbView = v == "list" ? "list" : "q"; break;
                case "lang": Lang = L.Languages.Any(l => l[0] == v) ? v : "en"; break;
                case "mode": Mode = v == "simple" || v == "pro" ? v : ""; break;
                case "wstyle": WidgetStyle = v == "everyday" || v == "gamer" || v == "calls" || v == "engineer" ? v : "auto"; break;
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
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);
        public static bool AskRunningToOpen()
        {
            // we were just started by the user, so we may bring a window forward - pass that right on to the running copy,
            // or its window opens behind everything and only the taskbar flashes
            try { AllowSetForegroundWindow(-1 /*ASFW_ANY*/); } catch (EntryPointNotFoundException) { }
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
        // is a running copy older than this one? (its exe's file version, read from disk - nothing is asked of it)
        public static bool RunningIsOlder()
        {
            Version mine; if (!Version.TryParse(Program.FileVersion, out mine)) return false;
            foreach (var p in Others())
                using (p)
                    try
                    {
                        Version v; var fv = FileVersionInfo.GetVersionInfo(p.MainModule.FileName).FileVersion;
                        if (Version.TryParse(fv, out v) && v < mine && !p.ProcessName.EndsWith("-remove", StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    catch (System.ComponentModel.Win32Exception) { } catch (InvalidOperationException) { } catch (ArgumentException) { }
            return false;
        }
        // other AdeeboMeter copies in THIS Windows session - renamed ones too ("AdeeboMeter (1).exe"); never another user's
        public static List<Process> Others()
        {
            var me = Process.GetCurrentProcess(); var r = new List<Process>();
            foreach (var p in Process.GetProcesses())
            {
                bool ours = false;
                try { ours = p.Id != me.Id && p.SessionId == me.SessionId && p.ProcessName.StartsWith("AdeeboMeter", StringComparison.OrdinalIgnoreCase); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                if (ours) r.Add(p); else p.Dispose();
            }
            return r;
        }
        public static void KillOthers()
        {
            foreach (var p in Others())
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { } finally { p.Dispose(); }
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
            else if (Path.GetFileName(exe).Equals("netsh.exe", StringComparison.OrdinalIgnoreCase))
                try { psi.StandardOutputEncoding = System.Text.Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); } catch (ArgumentException) { } catch (NotSupportedException) { }   // netsh: the OEM page (a Wi-Fi name with é), not ANSI
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
