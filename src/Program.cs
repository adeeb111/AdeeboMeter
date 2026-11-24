// AdeeboMeter - a small, quiet Windows companion for "is it me or the network?". Created by Adeeb Khan.
// .NET Framework 4.x, C# 5: builds with the csc.exe that ships with Windows (build_exe.cmd), no admin.
// No account. No telemetry. Never listens. Every packet it sends is listed in About ("What leaves your PC") - and you can switch
// each background one off. (It never listens on any port, never updates itself, never downloads anything.)
// In the background (Balanced, the default - Settings changes the pace): a ping to your gateway every 2 s; a ping to 1.1.1.1 every 15 s and
// one DNS lookup a minute at most (TCP 443 to it instead when ping never answers); once per network a 6-hop trace toward 1.1.1.1 with
// reverse-DNS names (to name your provider); and, only if you turn them on, one ping to your first pinned destination every 30 s
// and your added games' servers. Everything else - every Toolbox tool, the speed test, the ISP report, the Steam server list,
// Check a destination - runs only when you press it; when a check you started fails, Windows' own test page
// (msftconnecttest.com) is asked once (Settings can turn that off).
// Settings and the log live next to the exe (a Store copy: in its data folder; a portable copy: next to the exe too); history, usage, baselines, known
// devices and seen apps live in %LOCALAPPDATA%\AdeeboMeter (a portable copy: its data folder; History settings delete them); captures are deleted on exit.

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
[assembly: System.Reflection.AssemblyDescription("Know what's wrong. Show anyone. An offline Windows network and PC companion.")]
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
        public static readonly DateTime Started = DateTime.Now;   // the greeting lines speak for the first minute
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
                    // the Downloads folder and the Desktop themselves: where loose files arrive from anywhere. A folder unzipped inside them
                    // holds only what came in that zip - its AdeeboMeter.portable marker is honoured (round-3 #5)
                    var dl = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
                    { var v = k == null ? null : k.GetValue("{374DE290-123F-4565-9164-39C4925E467B}") as string; if (!string.IsNullOrEmpty(v)) dl.Add(Environment.ExpandEnvironmentVariables(v)); }
                    string here = Path.GetFullPath(Dir).TrimEnd('\\', '/');
                    if (dl.Where(d => !string.IsNullOrEmpty(d)).Select(d => Path.GetFullPath(d).TrimEnd('\\', '/')).Any(d => string.Equals(d, here, StringComparison.OrdinalIgnoreCase))) inDownloads = 1;
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
        public const string Version = "1.1.6";
        public const string FileVersion = "1.1.6.0";   // Windows wants four numbers - keep it in step with Version (the self-test checks)
        public static bool DllSafety, IsWine;   // true once DLLs can only come from System32
        public const string Tagline = "Know what's wrong. Show anyone.";
        public static int ForceTheme = -1;
        public static bool Restarted;   // started by the Restart button: open the big window again once running
        public static int StartViewMs = 1500;
        public static readonly int MyPid = System.Diagnostics.Process.GetCurrentProcess().Id;
        public static string StartView, StartTip, StartDest, StartFile;   // StartFile: a locked .adeebo file to open (bug-hunt ML-127)   // debug: --view <page> [--tip <key>] [--dest <host>] opens the engineer view there (used for screenshots)   // -1 follow Windows, 0 light, 1 dark
        public static bool WantDark() { return ForceTheme >= 2 && ForceTheme < Theme.Palettes.Length + 2 ? Theme.PaletteDark[ForceTheme - 2] : ForceTheme >= 0 ? ForceTheme == 1 : Theme.SystemDark(); }   // 0 light, 1 dark, 2+ the named themes

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
            if (ri >= 0) { if (ri + 1 >= args.Length) return 4;   /* no file name: the report couldn't be made */ int si = Array.FindIndex(args, a => string.Equals(a, "--seconds", StringComparison.OrdinalIgnoreCase)), sec; if (si < 0 || si + 1 >= args.Length || !int.TryParse(args[si + 1], out sec)) sec = 15; return Report.Run(args[ri + 1], sec); }
            int mt = Array.FindIndex(args, a => string.Equals(a, "--mibtest", StringComparison.OrdinalIgnoreCase));
            if (mt >= 0 && mt + 1 < args.Length) return SelfTest.MibTest(args[mt + 1]);
            if (args.Any(a => string.Equals(a, "--deeptest", StringComparison.OrdinalIgnoreCase)))
            {
                int mi = Array.FindIndex(args, a => string.Equals(a, "--minutes", StringComparison.OrdinalIgnoreCase)), min; if (mi < 0 || mi + 1 >= args.Length || !int.TryParse(args[mi + 1], out min)) min = 20;
                return SelfTest.Deep(!args.Any(a => string.Equals(a, "--local", StringComparison.OrdinalIgnoreCase)), Math.Max(2, Math.Min(60, min)));
            }
            int vi = Array.FindIndex(args, a => string.Equals(a, "--view", StringComparison.OrdinalIgnoreCase)); if (vi >= 0 && vi + 1 < args.Length) StartView = args[vi + 1];
            // --view-after <seconds>: wait for real measurements before opening the view (screenshots; 1.5 s when not given, at most 10 min)
            int va = Array.FindIndex(args, a => string.Equals(a, "--view-after", StringComparison.OrdinalIgnoreCase)); int vs; if (va >= 0 && va + 1 < args.Length && int.TryParse(args[va + 1], out vs)) StartViewMs = Math.Max(1500, Math.Min(600000, vs * 1000));
            int tpi = Array.FindIndex(args, a => string.Equals(a, "--tip", StringComparison.OrdinalIgnoreCase)); if (tpi >= 0 && tpi + 1 < args.Length) StartTip = args[tpi + 1];
            int dsi = Array.FindIndex(args, a => string.Equals(a, "--dest", StringComparison.OrdinalIgnoreCase)); if (dsi >= 0 && dsi + 1 < args.Length) StartDest = args[dsi + 1];
            // a double-clicked .adeebo file ("--open <file>", or the bare path the Store's file association passes)
            int opi = Array.FindIndex(args, a => string.Equals(a, "--open", StringComparison.OrdinalIgnoreCase));
            string of = opi >= 0 && opi + 1 < args.Length ? args[opi + 1] : args.FirstOrDefault(a => a.EndsWith(".adeebo", StringComparison.OrdinalIgnoreCase));
            if (of != null && of.EndsWith(".adeebo", StringComparison.OrdinalIgnoreCase) && File.Exists(of)) StartFile = Path.GetFullPath(of);
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
            // the Restart button: this copy was started by the running one, which is closing - wait for it to let go (20 s at most)
            Restarted = args.Any(a => string.Equals(a, "--restart", StringComparison.OrdinalIgnoreCase));
            if (!created && Restarted) { try { created = mtx.WaitOne(20000); } catch (AbandonedMutexException) { created = true; } }
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
                if (StartFile != null) try { File.WriteAllText(Path.Combine(DataDir, "open-file.txt"), StartFile); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }   // the running copy picks it up when asked to open
                if (Single.AskRunningToOpen()) return 0;
                // round 6 H6-L12: the running copy gave up stepping aside because a dialog of yours is open - it isn't stuck
                try
                {
                    string qb = Path.Combine(DataDir, "quit-busy.txt"); DateTime at;
                    if (File.Exists(qb) && DateTime.TryParse(File.ReadAllText(qb), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out at) && (DateTime.UtcNow - at.ToUniversalTime()).TotalSeconds < 30)
                    { File.Delete(qb); MessageBox.Show("The AdeeboMeter that's running has a window open that's waiting for you (a dialog), so it didn't step aside.\n\nClose that window, then open this copy again.", "AdeeboMeter", MessageBoxButtons.OK, MessageBoxIcon.Information); return 0; }
                }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
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
        // tmp -> dst in one step where the drive allows it; on shares and USB sticks where File.Replace isn't supported, the
        // old file is moved aside first and put back if the new one can't take its place - the old file is never deleted
        // before the new one is in (a scanner holding it open used to lose points.txt or the key list)
        public static void ReplaceFile(string tmp, string dst)
        {
            if (!File.Exists(dst)) { File.Move(tmp, dst); return; }
            try { File.Replace(tmp, dst, null); return; }
            catch (IOException) { } catch (PlatformNotSupportedException) { }
            string bak = dst + ".bak"; if (File.Exists(bak)) { try { File.Delete(bak); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; bak = dst + "." + Guid.NewGuid().ToString("N").Substring(0, 6) + ".bak"; } }   // bug-hunt ML-37: a locked old .bak doesn't stop the save
            File.Move(dst, bak);   // if this fails, nothing moved: the old file stays and the error goes up
            try { File.Move(tmp, dst); }
            catch (Exception) { try { if (!File.Exists(dst)) File.Move(bak, dst); } catch (IOException) { } catch (UnauthorizedAccessException) { } throw; }
            try { File.Delete(bak); } catch (IOException) { } catch (UnauthorizedAccessException) { }
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
        public static volatile int GwSec = 2, NetSec = 15, WifiSec = 30, AppsSec = 5;   /* Wi-Fi every 30 s with the window closed (every 10 s while it is open) - one quick Wi-Fi service query each (netsh only as a fallback) */ /* Balanced (Adeeb, 2026-11-12: Balanced takes the old Engineer timings, Penny is the light plan). The internet check is on from a fresh install (Adeeb, 2026-11-09): it pings 1.1.1.1 / 8.8.8.8 - outbound only, nothing is ever accepted; 0 in Settings turns it fully off */
        public static volatile bool Call = true;
        // engineer view (only while it's open)
        public static volatile string WidgetScreen = "";   // the screen the widget sits on ("" = the main screen); falls back to the main one if it's gone
        // 23 Nov: a problem hidden on Home - "kind|mode|value|level|yyyyMMdd" (mode: day or while); HideNever: kinds never shown on Home
        public static string HideProblem = "", HideNever = "";
        public static int SetupStep;   // review 24 Nov (L-6): which first-run question to come back to if the window closed mid-setup
        public static volatile bool IpNames = true, Portal = true, SeenTip, Details, BigWidget;   // BigWidget: the corner widget drawn 1.3x   // Details: the engineer toggle - raw values, sources and times on every finding
        // history on by default (kept HistoryDays, rolling), alerts with quiet hours, watch-and-suggest, data cap, pinned watch
        public static volatile bool History = true, Alerts = true, Quiet = true, Watch = true, PinWatch;
        public static volatile bool Streams;   // AM-CG-133: off until turned on (Streaming page)
        // whole sections someone doesn't want: hidden from the bar and More, and their background work stops
        public static volatile string View = "";
        public static volatile string Corner = "moments";
        // Weather (Adeeb, 14 Nov): off until you turn it on; the place is one you typed (never located); "lat;lon;name"
        public static volatile bool WeatherOn, WeatherF, WWeather = true; public static volatile string WeatherPlace = "", WeatherAcc = "normal", WeatherGeo = "", WeatherApi = "";   // WeatherGeo/Api: another server than Open-Meteo's (24 Nov); empty = Open-Meteo
        public static volatile int Zoom = 100;   // the window's zoom in percent (80-175): Ctrl + / Ctrl - / Ctrl 0, Ctrl+wheel, right-click   // how the corner behaves: quiet (never changes by itself) / moments / view (Moments.cs)   // the view in use (Views.cs): everyday / gamer / streamer / trader / engineer / my-N
        public static volatile string HiddenSecs = ""; public static readonly string[] Hideable = { "stream", "trade", "gamer" };
        public static bool Off(string sec) { return ("," + HiddenSecs + ",").Contains("," + sec + ","); }
        static readonly char[] CommaOnly = { ',' };
        public static void SetOff(string sec, bool off) { var l = HiddenSecs.Split(CommaOnly, StringSplitOptions.RemoveEmptyEntries).Where(z => z != sec && Hideable.Contains(z)).ToList(); if (off) l.Add(sec); HiddenSecs = string.Join(",", l); }
        // Trading: the live broker panel is opt-in (a check every 2 s in market hours); end-of-day currencies on by default
        public static volatile bool TradeLive, TradeCrypto, TradeAlert = true, TradeFx, TradeEvening, Sports; public static volatile string TradeVenue = "both", SportTeams = "", FxList = MarketData.DefaultFx, Paper = "app"; /* Paper: "app" (24 Nov, the app's own look) or the newspaper - "cream", "white", "theme" */ public static volatile bool KickoffRemind, CasualFriday, CasualMoves, PhoneStandby = true; /* 1.1.6: the markets page's currencies; the Sports/Markets paper (cream, theme, white); reminders off until ticked */ public static volatile string TradeHost = "", TradeMode = "active";   // Streaming: notice a stream (by its server names) and keep an eye on it while it plays
        /* hunt-5: DevAlerts, LineAlertOpen and StreamLinks removed - nothing used them */
        public static volatile int HistoryDays = 3, CapGB, OnlineMin = 30;   // OnlineMin: online lookups switch themselves off after this
        public static volatile int LiveMin = 15, SniffSec = 30;
        // widget
        public static volatile int Fade = 77, DwellMs = 450;
        // Gamer Zone: GameIds = games the user clicked (consent to contact their servers); background ping every GameBgSec
        // to up to GameBgServers servers; every second while a game is open (GameFast); history kept GameDays
        public static string GameIds = "";
        public static volatile int GameBgSec = 30, GameBgServers = 3, GameDays = 14;
        public static volatile bool GameFast = true, GameAuto = true, GameWidget = true, Buddy = true; public static int OutageBest; public static volatile string BuddyHat = "";   // Buddy: the widget shows a little face that feels what the network feels
        public static volatile string WidgetStyle = "auto";
        // Simple (the dots, What's wrong?, speed, history, Gamer Zone) or Pro (every tab). "" = not chosen yet (a new install:
        // shown as Simple with the choice on the home page); an upgrade from 1.1.4 keeps Pro - nothing it had disappears
        // navigation (PLAN 11.11): the tabs on the bar in order (the rest are under More), Classic tabs or the Sidebar, the
        // right-hand strip open or slim, the user's own tabs ("name=item;item" separated by "|"), the one-time Sidebar card seen
        public static volatile string Nav = "home,toolbox,stream,sports,trade,gamer,time,learn", Layout = "sidebar", MyTabs = "", Searches = "", BuddyFace = "drawn", WUnits = "bits", WDir = "both", FmpEvery = "15";   // the speed widget: Mb/s or MB/s; both directions, download or upload
        public static volatile bool WGraph = true, WTotal = true, SetupDone = true;   // SetupDone: false only on a fresh install until the 3 questions are answered or skipped   // Searches: the last few "Search anything" words, newest first, "|" between
        public static volatile bool SideOpen = true, SidebarTried; static bool mood2Seen;
        public static volatile string TbPins = "";   // your own Toolbox pins (Every tool), tool ids, up to 12
        public static volatile string Mode = "", Lang = "en", TbView = "list", LearnView = "goal", OverlayAt = "";   /* OverlayAt: where the stream overlay was left ("x,y") */   // TbView: the Toolbox by question ("q") or the classic list ("list")   // Lang: the UI language (L.Languages)
        public static volatile bool Points = true;   // Adeebo Points (Settings): on from day 1; off hides the button and pauses earning, the total is kept
        public static volatile string SpeedServer = "auto";   // the speed test's server: "auto" (Cloudflare, nearest) or an id from the list
        public static bool Simple { get { return Mode != "pro"; } }   // auto (follows what you are doing) / everyday / gamer
        public static string Preset = "balanced";
        public static readonly List<string> Pins = new List<string>();   // saved destinations, max 5. Changed on the UI thread under lock (Pins); readers on other threads copy it under the same lock
        public static Action Save = delegate { };
        public static Action<int> SetTheme = delegate { }; public static Func<int> GetTheme = () => -1;
        public static Action<bool> SetAutoStart = delegate { }; public static Func<bool> GetAutoStart = () => false;

        public static void ApplyPreset(string p)
        {
            Preset = p;
            // Penny: the lightest touch. Balanced (the default; Engineer was merged into it - the same timings): every check close. Gamer: the same, plus more game servers, more often.
            if (p == "penny") { GwSec = 30; NetSec = 300; WifiSec = 120; AppsSec = 30; Call = false; GameBgSec = 60; GameBgServers = 3; }
            else if (p == "gamer") { GwSec = 2; NetSec = 15; WifiSec = 10; AppsSec = 5; Call = true; GameBgSec = 10; GameBgServers = 5; }
            else { Preset = "balanced"; GwSec = 2; NetSec = 15; WifiSec = 30; AppsSec = 5; Call = true; GameBgSec = 30; GameBgServers = 3; }
        }
        // background checks per minute, on top of the once-a-second counter read the widget always does
        public static int JobsPerMinute() { return JobsPerMinute(GwSec, NetSec, WifiSec, AppsSec, Preset == "penny" ? 60 : 10); }
        static int JobsPerMinute(int gw, int net, int wifi, int apps, int ifs)
        {
            Func<int, double> per = delegate(int sec) { return sec > 0 ? 60.0 / sec : 0; };
            return (int)Math.Round(per(ifs) /*adapters*/ + per(gw) + per(net) + (net > 0 ? per(Math.Max(net, 60)) : 0) /*DNS: at most once a minute*/ + per(wifi) + 2 * per(apps));
        }
        // what a preset would cost, worked out without touching the live settings the monitor reads
        // game pings in the background, per minute, once a game is watched (more while one is played) - shown next to the checks
        public static int GameJobsPerMinute() { return Games.Monitored().Count == 0 || GameBgSec <= 0 ? 0 : GameBgServers * 60 / GameBgSec; }
        public static int GameJobsForPreset(string p) { return p == "penny" ? 3 * 60 / 60 : p == "gamer" ? 5 * 60 / 10 : 3 * 60 / 30; }
        public static bool PresetSaved;   // the settings file named a preset (an old file without one keeps its own intervals)
        public static int JobsForPreset(string p)
        {
            if (p == "penny") return JobsPerMinute(30, 300, 120, 30, 60);
            return JobsPerMinute(2, 15, p == "gamer" ? 10 : 30, 5, 10);   // Balanced checks Wi-Fi every 30 s, Gamer every 10 s
        }
        public static IEnumerable<string> Lines()
        {
            yield return "preset=" + Preset;
            yield return "gw=" + GwSec; yield return "net=" + Policy.Keep("net", NetSec.ToString(CultureInfo.InvariantCulture)); yield return "wifi=" + WifiSec; yield return "apps=" + AppsSec;
            yield return "streams2=" + (Streams ? 1 : 0); yield return "hidden=" + HiddenSecs; yield return "tradelive=" + (TradeLive ? 1 : 0); yield return "tradecrypto=" + (TradeCrypto ? 1 : 0); yield return "tradealert=" + (TradeAlert ? 1 : 0); yield return "tradefx2=" + (TradeFx ? 1 : 0); yield return "tradevenue=" + TradeVenue; yield return "sports=" + (Sports ? 1 : 0); yield return "sportteams=" + (SportTeams ?? "").Replace("\n", " "); yield return "tradeeve=" + (TradeEvening ? 1 : 0); yield return "fxlist=" + FxList; yield return "paper=" + Paper; yield return "kickremind=" + (KickoffRemind ? 1 : 0); yield return "phonestandby=" + (PhoneStandby ? 1 : 0); yield return "casualfri=" + (CasualFriday ? 1 : 0); yield return "casualmoves=" + (CasualMoves ? 1 : 0); yield return "view=" + View; yield return "corner=" + Corner; yield return "weather=" + (WeatherOn ? 1 : 0); yield return "wplace=" + WeatherPlace; yield return "wgeo=" + WeatherGeo; yield return "wapi=" + WeatherApi; yield return "wacc=" + WeatherAcc; yield return "wtempf=" + (WeatherF ? 1 : 0); yield return "wweather=" + (WWeather ? 1 : 0); yield return "zoom=" + Zoom.ToString(CultureInfo.InvariantCulture); yield return "views=" + Views.Line(); yield return "tradehost=" + TradeHost; yield return "trademode=" + TradeMode; yield return "call=" + (Call ? 1 : 0); yield return "ipnames=" + (IpNames ? 1 : 0); yield return "mood=" + Mood.Name; yield return "crlrev=" + (CertCfg.Revocation ? 1 : 0); yield return "portal=" + (Portal ? 1 : 0); yield return "seentip=" + (SeenTip ? 1 : 0); yield return "hideprob=" + (HideProblem ?? "").Replace("\n", " "); yield return "hidenever=" + (HideNever ?? "").Replace("\n", " "); yield return "setupstep=" + SetupStep; yield return "details=" + (Details ? 1 : 0); yield return "bigwidget=" + (BigWidget ? 1 : 0); yield return "history2=" + (History ? 1 : 0); yield return "histdays=" + Policy.Keep("histdays", HistoryDays.ToString(CultureInfo.InvariantCulture));
            yield return "alerts=" + (Alerts ? 1 : 0); yield return "quiet=" + (Quiet ? 1 : 0); yield return "watch=" + (Watch ? 1 : 0); yield return "capgb=" + CapGB; yield return "onlinemin=" + OnlineMin; yield return "pinwatch=" + (PinWatch ? 1 : 0);
            yield return "wscreen=" + WidgetScreen;
            yield return "gmon=" + Policy.Keep("gmon", GameIds); yield return "gbg=" + GameBgSec; yield return "gsrv=" + GameBgServers; yield return "gdays=" + GameDays;
            yield return "gfast=" + (GameFast ? 1 : 0); yield return "buddy=" + (Buddy ? 1 : 0); yield return "outagebest=" + OutageBest; yield return "hat=" + BuddyHat; yield return "gauto=" + (GameAuto ? 1 : 0); yield return "gwidget=" + (GameWidget ? 1 : 0); yield return "wstyle=" + WidgetStyle; yield return "mode=" + Policy.Keep("mode", Mode); yield return "lang=" + Lang; yield return "tbview2=" + TbView; yield return "tbpins=" + TbPins; yield return "lview=" + LearnView; yield return "ovat=" + OverlayAt; yield return "speedsrv=" + SpeedServer; yield return "points=" + (Points ? 1 : 0);
            yield return "fmpevery=" + FmpEvery;   // 21 Nov: how often your FMP key is asked (60, 15, 5 or 1 minutes; 5 and 1 need a paid plan)
            yield return "nav=" + Nav; yield return "navsports=1"; yield return "layout=" + Layout; yield return "sideopen=" + (SideOpen ? 1 : 0); yield return "mytabs=" + MyTabs; yield return "sbtried=" + (SidebarTried ? 1 : 0); yield return "searches=" + Searches; yield return "mood2=" + AdeeboMeter.Lines.Mood; yield return "persona=" + AdeeboMeter.Lines.Persona; yield return "addr=" + AdeeboMeter.Lines.Addr; yield return "addrname=" + AdeeboMeter.Lines.Name; yield return "chatty=" + AdeeboMeter.Lines.Chat; yield return "faith=" + AdeeboMeter.Lines.Faith; yield return "faithlast=" + AdeeboMeter.Lines.LastFaith; yield return "bface=" + BuddyFace; yield return "wunits=" + WUnits; yield return "wdir=" + WDir; yield return "wgraph=" + (WGraph ? 1 : 0); yield return "wtotal=" + (WTotal ? 1 : 0); yield return "setupdone=" + (SetupDone ? 1 : 0);
            yield return "livemin=" + LiveMin; yield return "sniffsec=" + SniffSec; yield return "fade=" + Fade; yield return "dwell=" + DwellMs;
            string[] pins; lock (Pins) pins = Pins.ToArray();
            foreach (var p in pins) yield return "pin=" + p;
        }
        public static readonly int[] OnlineChoices = { 5, 15, 30, 60, 120, 180 }, HistDayChoices = { 1, 3, 7, 14, 30, 60, 90, 180 }, CapChoices = { 0, 10, 25, 50, 100, 250, 500 };
        public static readonly HashSet<string> Known = new HashSet<string> { "navsports", "tbview2", "fmpevery", "streams", "streams2", "faithlast", "hidden", "tradelive", "tradecrypto", "tradealert", "tradefx", "tradefx2", "tradevenue", "sports", "sportteams", "tradeeve", "view", "views", "corner", "weather", "wplace", "wacc", "wtempf", "wweather", "zoom", "tradehost", "trademode", "nav", "layout", "sideopen", "mytabs", "sbtried", "searches", "mood2", "persona", "addr", "addrname", "chatty", "faith", "bface", "wunits", "wdir", "wgraph", "wtotal", "setupdone", "mood", "crlrev", "buddy", "hat", "outagebest", "ovat", "lview", "preset", "gw", "net", "wifi", "apps", "call", "ipnames", "portal", "seentip", "details", "bigwidget", "wscreen", "history2", "histdays", "alerts", "quiet", "devalerts", "watch", "capgb", "onlinemin", "pinwatch", "livemin", "sniffsec", "fade", "dwell", "pin", "gmon", "gbg", "gsrv", "gdays", "gfast", "gauto", "gwidget", "wstyle", "mode", "lang", "tbview", "tbpins", "speedsrv", "points", "autostart", "widget", "serious", "theme", "fxlist", "paper", "kickremind", "phonestandby", "linealert", "casualfri", "casualmoves", "hideprob", "hidenever", "setupstep", "wgeo", "wapi" };   // 1.1.6: Markets and Sports
        // the bar: known section ids only, no repeats, Home always first, at most 7 (the rest go to More by themselves)
        public static readonly string[] NavIds = { "home", "toolbox", "stream", "sports", "trade", "gamer", "time", "learn", "story", "security", "dest", "calc", "lab", "shell" };   // 22 Nov: Outlook is on Home, Reports in the Toolbox ("Make a report") - not pages of their own
        public static bool NavSports;   // Sports became its own page (22 Nov): added once after Streaming in a saved bar
        public static string NavClean(string v)
        {
            var l = new List<string> { "home" };
            var given = (v ?? "").Split(',').Select(p => p.Trim()).ToList();
            foreach (var t in given) { if ((NavIds.Contains(t) || (t.StartsWith("mine", StringComparison.Ordinal) && t.Length == 5 && t[4] >= '1' && t[4] <= '3')) && !l.Contains(t) && l.Count < 8) l.Add(t); }
            return string.Join(",", l);
        }
        // a host name or an IP only: letters, digits, dots and dashes (a pasted URL loses its scheme and path)
        static readonly char[] HostCut = { '/', '?', '#' };
        // a broker's address as people paste it: the scheme, any user:password@ (AM-CG-109 - never the host), the path go;
        // a port the user typed stays (AM-CG-110), and an IPv6 address keeps its brackets: "host", "host:8443", "[v6]:8443"
        public static string CleanHost(string v)
        {
            v = (v ?? "").Trim().ToLowerInvariant(); int i = v.IndexOf("://", StringComparison.Ordinal); if (i >= 0) v = v.Substring(i + 3);
            i = v.IndexOfAny(HostCut); if (i >= 0) v = v.Substring(0, i);
            i = v.LastIndexOf('@'); if (i >= 0) v = v.Substring(i + 1);
            string h; int p; if (v.Length == 0 || v.Length > 110 || !Lab.Split(v, 443, out h, out p)) return "";
            System.Net.IPAddress ip; bool isIp = System.Net.IPAddress.TryParse(h, out ip);
            if (!isIp && !(h.Length <= 100 && h.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '-'))) return "";
            return (h.Contains(":") ? "[" + h + "]" : h) + (p == 443 ? "" : ":" + p.ToString(CultureInfo.InvariantCulture));
        }
        static int Pick(string v, int def, params int[] allowed) { int x; return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out x) && allowed.Contains(x) ? x : def; }
        public static void Read(string k, string v)
        {
            switch (k)
            {
                case "preset": PresetSaved = true; Preset = v == "penny" || v == "gamer" || v == "custom" ? v : "balanced";   /* "engineer" (merged): Balanced */ break;
                case "gw": GwSec = Pick(v, 2, 2, 5, 10, 30, 0); break;
                case "net": NetSec = Pick(v, 15, 15, 30, 60, 300, 0); break;
                case "wifi": WifiSec = Pick(v, 10, 10, 30, 120, 0); break;
                case "apps": AppsSec = Pick(v, 5, 5, 10, 30, 0); break;
                case "call": Call = v == "1"; break;
                case "ipnames": IpNames = v == "1"; break;
                case "streams2": Streams = v == "1"; break;   // (the old "streams" key was on by default and is ignored)
                case "hidden": HiddenSecs = string.Join(",", (v ?? "").Split(',').Where(z => Hideable.Contains(z)).Distinct()); break;
                case "tradelive": TradeLive = v == "1"; break;
                case "tradecrypto": TradeCrypto = v == "1"; break;
                case "tradealert": TradeAlert = v != "0"; break;
                case "tradefx2": TradeFx = v == "1"; break;   // 1.1.5 r4: off until turned on (the old "tradefx" key was on by default and is ignored)
                case "tradevenue": TradeVenue = v == "us" || v == "ca" ? v : "both"; break;
                case "sports": Sports = v == "1"; break;
                case "sportteams": SportTeams = v.Length > 300 ? v.Substring(0, 300) : v; break;
                case "tradeeve": TradeEvening = v == "1"; break;
                case "fxlist": FxList = v.Length > 200 ? v.Substring(0, 200) : v; break;   // R2-150: capped like its neighbours
                case "paper": Paper = v == "white" || v == "theme" || v == "cream" ? v : "app"; break;
                case "kickremind": KickoffRemind = v == "1"; break;
                case "phonestandby": PhoneStandby = v != "0"; break;
                case "linealert": break;   // hunt-5: a 1.1.5 setting nothing reads any more - an old file still loads
                case "casualfri": CasualFriday = v == "1"; break;
                case "casualmoves": CasualMoves = v == "1"; break;
                case "view": View = v.Length <= 24 && (Views.IsBuiltIn(v) || v.StartsWith("my-", StringComparison.Ordinal)) ? v : ""; break;
                case "views": Views.Load(v); break;
                case "corner": Corner = v == "quiet" || v == "view" ? v : "moments"; break;
                case "weather": WeatherOn = v == "1"; break;
                case "wplace": WeatherPlace = Weather.CleanPlace(v); break;
                case "wgeo": WeatherGeo = Weather.CleanServer(v); break;
                case "wapi": WeatherApi = Weather.CleanServer(v); break;
                case "wacc": WeatherAcc = v == "better" ? "better" : "normal"; break;
                case "wtempf": WeatherF = v == "1"; break;
                case "wweather": WWeather = v != "0"; break;
                case "zoom": { int z; if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out z)) Zoom = Math.Max(80, Math.Min(175, z)); } break;
                case "tradehost": TradeHost = CleanHost(v); break;
                case "trademode": TradeMode = v == "casual" ? "casual" : "active"; break;
                case "mood": if (Array.IndexOf(Mood.Ids, v) >= 0) { Mood.Name = v; if (!mood2Seen) AdeeboMeter.Lines.Mood = v == "sarcastic" ? "roast" : v == "anxious" ? "emo" : v == "brutal" ? "bossy" : "chill"; } break;   // a 1.1.4 voice carries over to the closest 1.1.5 mood
                case "crlrev": CertCfg.Revocation = v == "1"; break;
                case "details": Details = v == "1"; break;
                case "bigwidget": BigWidget = v == "1"; break;
                case "wscreen": WidgetScreen = v.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase) && v.Length < 40 ? v : ""; break;
                case "portal": Portal = v != "0"; break;
                case "seentip": SeenTip = v == "1"; break;
                case "hideprob": HideProblem = v; break;
                case "hidenever": HideNever = v; break;
                case "setupstep": { int ss; if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out ss)) SetupStep = Math.Max(0, Math.Min(3, ss)); } break;
                case "history2": History = v == "1"; break;     // (the old "history" key from the first preview is ignored: history is on by default)
                case "histdays": HistoryDays = Pick(v, 3, HistDayChoices); break;
                case "alerts": Alerts = v != "0"; break;
                case "quiet": Quiet = v != "0"; break;
                case "devalerts": break;   // hunt-5: its pop-ups were removed - read and ignored
                case "watch": Watch = v != "0"; break;
                case "capgb": CapGB = Pick(v, 0, CapChoices); break;
                case "onlinemin": OnlineMin = Pick(v, 30, OnlineChoices); break;
                case "pinwatch": PinWatch = v == "1"; break;
                case "gmon": GameIds = string.Join(",", v.Split(',').Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0 && x.Length <= 40 && x.All(ch => char.IsLetterOrDigit(ch) || ch == '-')).Distinct().Take(20)); break;
                case "gbg": GameBgSec = Pick(v, 30, 10, 30, 60, 0); break;
                case "gsrv": GameBgServers = Pick(v, 3, 1, 3, 5); break;
                case "gdays": GameDays = Pick(v, 14, 7, 14, 30); break;
                case "gfast": GameFast = v != "0"; break;
                case "buddy": Buddy = v != "0"; break;
                case "hat": BuddyHat = Draw.Hats.Contains(v) ? v : ""; break;
                case "outagebest": int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out OutageBest); OutageBest = Math.Max(0, Math.Min(99999, OutageBest)); break;
                case "gauto": GameAuto = v != "0"; break;
                case "gwidget": GameWidget = v != "0"; break;
                case "points": Points = v == "1"; break;
                case "speedsrv": SpeedServer = System.Text.RegularExpressions.Regex.IsMatch(v ?? "", "^[a-z0-9-]{1,40}$") ? v : "auto"; break;
                case "tbview": break;   // before 22 Nov 2026: everyone starts again on All tools ("tbview2")
                case "tbview2": TbView = v == "list" ? "list" : v == "topic" ? "topic" : "q"; break;
                case "tbpins": TbPins = string.Join(",", (v ?? "").Split(',').Select(s => s.Trim()).Where(s => System.Text.RegularExpressions.Regex.IsMatch(s, "^[a-z0-9]{1,24}$")).Distinct().Take(12)); break;
                case "nav": Nav = NavClean(v); break;
                case "navsports": NavSports = v == "1"; break;
                case "layout": Layout = v == "sidebar" ? "sidebar" : "classic"; break;
                case "sideopen": SideOpen = v != "0"; break;
                case "mytabs": MyTabs = v.Length > 2000 ? "" : Program.Plain(v); break;   // SEC-14: no bidi/format characters in tab names
                case "sbtried": SidebarTried = v == "1"; break;
                case "searches": Searches = v.Length > 400 ? "" : v; break;
                // the companion (1.1.5): mood, who talks, how to address you, how chatty - each checked against its list
                case "mood2": if (Array.IndexOf(AdeeboMeter.Lines.Moods, v) >= 0) { AdeeboMeter.Lines.Mood = v; mood2Seen = true; } break;
                case "persona": if (AdeeboMeter.Lines.PersonaKnown(v)) AdeeboMeter.Lines.Persona = v; break;
                case "addr": if (Array.IndexOf(AdeeboMeter.Lines.Addrs, v) >= 0) AdeeboMeter.Lines.Addr = v; break;
                case "addrname": AdeeboMeter.Lines.Name = new string(Program.Plain(v).Take(24).ToArray()).Trim(); break;   // SEC-14: every bidi control, isolates too
                case "chatty": if (Array.IndexOf(AdeeboMeter.Lines.Chatty, v) >= 0) AdeeboMeter.Lines.Chat = v; break;
                case "fmpevery": FmpEvery = v == "60" || v == "5" || v == "1" ? v : "15"; break;
                case "wunits": WUnits = v == "bytes" ? "bytes" : "bits"; break;
                case "wdir": WDir = v == "down" || v == "up" ? v : "both"; break;
                case "wgraph": WGraph = v != "0"; break;
                case "wtotal": WTotal = v != "0"; break;
                case "setupdone": SetupDone = v != "0"; break;
                case "bface": BuddyFace = v == "emoji" ? "emoji" : "drawn"; break;
                case "faith": if (Array.IndexOf(AdeeboMeter.Lines.Faiths, v) >= 0) AdeeboMeter.Lines.Faith = v; break;
                case "faithlast": if (Array.IndexOf(AdeeboMeter.Lines.Faiths, v) >= 0 && v != "off") AdeeboMeter.Lines.LastFaith = v; break;
                case "lview": LearnView = v == "all" ? "all" : "goal"; break;
                case "ovat": OverlayAt = System.Text.RegularExpressions.Regex.IsMatch(v, "^-?[0-9]{1,6},-?[0-9]{1,6}$") ? v : ""; break;
                case "lang": Lang = L.Languages.Any(l => l[0] == v) ? v : "en"; break;
                case "mode": Mode = v == "simple" || v == "pro" ? v : ""; break;
                case "wstyle": WidgetStyle = v == "everyday" || v == "gamer" || v == "calls" || v == "engineer" || v == "speed" || v == "speedstatus" || v == "speedapps" ? v : "auto"; break;
                case "livemin": LiveMin = Pick(v, 15, 5, 15, 30); break;
                case "sniffsec": SniffSec = Pick(v, 30, 30, 60); break;
                case "fade": Fade = Pick(v, 77, 51, 77, 128, 191, 255); break;
                case "dwell": DwellMs = Pick(v, 450, 300, 450, 800); break;
                case "pin":
                    string h; int pt; bool t; string e;
                    if (Pins.Count < 5 && DestCheck.Parse(v, out h, out pt, out t, out e) && !Pins.Contains(v, StringComparer.OrdinalIgnoreCase)) lock (Pins) Pins.Add(v.Trim());
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
                try { ours = p.Id != me.Id && p.SessionId == me.SessionId && p.ProcessName.StartsWith("AdeeboMeter", StringComparison.OrdinalIgnoreCase) && !p.ProcessName.EndsWith("-remove", StringComparison.OrdinalIgnoreCase); }   /* never the uninstall helper */ catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
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
        static volatile bool jobWarned;
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
                if (!Native.AssignProcessToJobObject(h, p.Handle) && !jobWarned) { jobWarned = true; Program.Log("job: couldn't attach a helper to the kill-on-exit job (this app may run inside another job) - helpers may outlive a crash"); }
            }
            catch { }
        }

        // reads a helper's stream to the end, keeping at most `cap` characters (the rest is read and dropped)
        static System.Threading.Tasks.Task<string> Drain(StreamReader r, int cap)
        {
            return System.Threading.Tasks.Task.Factory.StartNew(() => { try { return ReadCapped(r, cap); } catch (IOException) { return ""; } catch (ObjectDisposedException) { return ""; } }, System.Threading.Tasks.TaskCreationOptions.LongRunning);
        }
        public static string ReadCapped(TextReader r, int cap)
        {
            var sb = new System.Text.StringBuilder(); var buf = new char[8192]; int n;
            while ((n = r.Read(buf, 0, buf.Length)) > 0) if (sb.Length < cap) sb.Append(buf, 0, Math.Min(n, cap - sb.Length));
            return sb.ToString();
        }

        // Full path, so a netsh.exe planted somewhere on PATH can never be picked up instead.
        public static readonly string Netsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "netsh.exe");
        public static string Sys(string exe) { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exe); }

        // Runs a fixed program with fixed arguments (never through cmd or PowerShell) and returns its output.
        // review 22 Nov: output is capped (a runaway helper - tshark on a busy capture - can't fill the 64 MB budget), a timed-out
        // helper is killed and reaped, and the caller can tell "timed out" from "printed nothing"
        public const int MaxOutput = 8 * 1024 * 1024;   // characters kept; the rest is read and dropped so the helper never blocks on a full pipe
        public static string Run(string exe, string args, int timeoutMs) { bool to; string o = Run(exe, args, timeoutMs, out to); if (to) { Program.Log("helper timed out after " + timeoutMs + " ms: " + System.IO.Path.GetFileName(exe) + " " + (args ?? "").Split(' ')[0]); LastTimedOut = true; } else LastTimedOut = false; return o; }   // R2-138: said in the log, and readable right after the call
        [ThreadStatic] public static bool LastTimedOut;
        public static string Run(string exe, string args, int timeoutMs, out bool timedOut)
        {
            timedOut = false;
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true; psi.CreateNoWindow = true;
            psi.WorkingDirectory = Path.GetDirectoryName(exe);   // never the folder AdeeboMeter runs from (Downloads): a child could load a planted DLL from there
            if (Path.GetFileName(exe).Equals("tshark.exe", StringComparison.OrdinalIgnoreCase)) { psi.StandardOutputEncoding = System.Text.Encoding.UTF8; psi.StandardErrorEncoding = System.Text.Encoding.UTF8; }   // tshark writes UTF-8 (→, names); Windows' own tools write the console code page
            else if (Path.GetFileName(exe).Equals("netsh.exe", StringComparison.OrdinalIgnoreCase))
                try { psi.StandardOutputEncoding = System.Text.Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); } catch (ArgumentException) { } catch (NotSupportedException) { }   // netsh: the OEM page (a Wi-Fi name with é), not ANSI
            var bz = Busy.Begin(Busy.Label(exe, args));
            try {
            using (var p = Process.Start(psi))
            {
                Add(p);
                var t = Drain(p.StandardOutput, MaxOutput);
                var et = Drain(p.StandardError, 64 * 1024);   // bug-hunt L-72: drained and observed - kept now, never left unwatched
                if (!p.WaitForExit(timeoutMs))
                {
                    timedOut = true;
                    try { p.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                    try { p.WaitForExit(3000); } catch (InvalidOperationException) { }   // reaped, so its pipes close and the readers end
                    t.Wait(1000); et.Wait(1000); return "";
                }
                p.WaitForExit();   // the redirected streams reach their end
                bool done = t.Wait(10000); et.Wait(1000);   // L-72: a long output gets time to finish, not 1 s
                return done ? t.Result : "";
            }
            } finally { bz.End(); }
        }
    }

    // What AdeeboMeter is waiting on right now. Anything that has taken 5 s or more shows in the footer of the full view with a
    // spinner and its seconds, then disappears when it ends (Adeeb, 1.1.6 test day: "anything that takes more than 5-10 seconds should
    // show a message and disappear" - quiet work looked stuck). English on purpose: it names programs and commands.
    static class Busy
    {
        public sealed class Item { public string What; public DateTime At = DateTime.UtcNow; public void End() { lock (all) all.Remove(this); } }
        static readonly List<Item> all = new List<Item>();
        public static Item Begin(string what) { var i = new Item { What = what ?? "" }; lock (all) all.Add(i); return i; }
        public const int ShowAfterSec = 5;
        // the longest-waiting item past the threshold, and how many others are past it too
        public static Item Oldest(out int others)
        {
            others = 0; Item o = null; var cut = DateTime.UtcNow.AddSeconds(-ShowAfterSec);
            lock (all) foreach (var i in all) if (i.At <= cut) { if (o == null || i.At < o.At) o = i; others++; }
            if (o != null) others--; return o;
        }
        public static string Label(string exe, string args)
        {
            string n = Path.GetFileNameWithoutExtension(exe ?? "").ToLowerInvariant(), a = (args ?? "").Trim(); if (a.Length > 60) a = a.Substring(0, 59) + "…";
            switch (n)
            {
                case "netsh": return "Waiting on Windows' network settings (netsh " + a + ")";
                case "tshark": case "dumpcap": return "Waiting on Wireshark (" + n + ")";
                case "wevtutil": return "Reading the Windows event log";
                case "powershell": case "pwsh": return "Waiting on Windows PowerShell";
                case "ping": case "tracert": case "pathping": return "Waiting on " + n + " " + a;
                default: return "Waiting on Windows (" + n + (a == "" ? "" : " " + a) + ")";
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
