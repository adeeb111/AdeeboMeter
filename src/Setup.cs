// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
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

namespace AdeeboMeter
{
    // ================================================================== install / update / remove - the exe is its own installer
    // Double-click a downloaded AdeeboMeter.exe and it offers to install itself for this Windows user only:
    // %LOCALAPPDATA%\Programs\AdeeboMeter\<version>, a Start menu entry, an entry in Settings > Apps (so it can be removed
    // like any app), your settings carried over. No administrator rights, no shell, no extra files.
    static class Setup
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] static extern bool DeleteFileW(string path);
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
        // the installed copy's "Start with Windows" choice (its settings file), on when unknown
        public static bool InstalledAutoStart(string exe)
        {
            try
            {
                if (string.IsNullOrEmpty(exe)) return true;
                string f = Path.Combine(Path.GetDirectoryName(exe), "AdeeboMeter.settings"); if (!File.Exists(f)) return true;
                foreach (var l in File.ReadAllLines(f)) if (l.StartsWith("autostart=", StringComparison.Ordinal)) return l.Trim() != "autostart=0";
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException)) throw; }
            return true;
        }
        // a Run value that starts some AdeeboMeter*.exe (installed, downloaded or renamed)
        public static bool OurRunValue(string v) { try { string f = Path.GetFileName((v ?? "").Trim().Trim('"')); return f.StartsWith("AdeeboMeter", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase); } catch (ArgumentException) { return false; } }

        // Offer to install only on a first start of a copy that isn't installed and was never used from where it is.
        public static bool ShouldOffer()
        {
            if (Program.Packaged || RunningInstalled) return false;
            return !File.Exists(Path.Combine(Program.WriteDir, "AdeeboMeter.settings")) && !File.Exists(Path.Combine(Program.Dir, "AdeeboMeter.settings")) && !File.Exists(Path.Combine(Program.Dir, "Ade" + "boMeter.settings"));
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
            var until = DateTime.UtcNow.AddSeconds(4);
            while (DateTime.UtcNow < until)
            {
                var ps = Single.Others(); bool any = ps.Count > 0;
                foreach (var p in ps) p.Dispose();
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
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(self, exe, true);
                // the installed copy is the one you chose to keep: it doesn't need the downloaded copy's "from the internet" mark
                // (Mark-of-the-Web) - that would warn again at every start from the new place
                if (!DeleteFileW(exe + ":Zone.Identifier")) Program.Log("install: no download mark to clear");
            }

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
                    if (k != null) { var v = k.GetValue("AdeeboMeter") as string; if (v != null && OurRunValue(v)) k.DeleteValue("AdeeboMeter"); }   // removed means it never starts again, wherever the copy was
            }
            catch { }
            try { if (File.Exists(Shortcut)) File.Delete(Shortcut); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey); } catch { }
            step("Removing your history and captures (your encryption keys stay)...", 0.75f);
            // Everything goes except what opens files sent to you: the P-384 key list, saved recipients and key backups. The keys
            // themselves live in Windows' key store, which an uninstall doesn't touch - deleting the list would leave every
            // .adeebo addressed to you unopenable after a reinstall. They're small; delete the folder by hand to wipe them.
            try
            {
                string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdeeboMeter");
                if (Directory.Exists(data))
                {
                    var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "keys-ec.txt", "recipients", "certs" };
                    foreach (var f in Directory.GetFiles(data)) if (!keep.Contains(Path.GetFileName(f))) try { File.Delete(f); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    foreach (var d in Directory.GetDirectories(data)) if (!keep.Contains(Path.GetFileName(d))) try { Directory.Delete(d, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
            catch { }
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
            autoStart = Setup.InstalledAutoStart(instExe);   // an update keeps your choice; a first install starts on
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
        // Alt+F4 or the taskbar's Close while it copies or removes: wait - a half install (or half removal) is worse than a second's wait
        protected override void OnFormClosing(FormClosingEventArgs e) { if (st == St.Working && e.CloseReason == CloseReason.UserClosing) e.Cancel = true; base.OnFormClosing(e); }
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

        bool Upgrade { get { return instVer != null && instVer != Program.Version && !Newer; } }
        // the installed copy is newer than this one (an old download double-clicked): never "update" backwards
        bool Newer { get { Version a, b; return instVer != null && Version.TryParse(instVer, out a) && Version.TryParse(Program.Version, out b) && a > b; } }
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
                string main = Same ? "Open AdeeboMeter" : Newer ? "Open AdeeboMeter " + instVer : Upgrade ? "Update to " + Program.Version : "Install";
                Btn(g, "main", new RectangleF(bx, y, W - 2 * bx, 46 * S), main, Theme.Blue, Color.White, true); y += 54 * S;
                Btn(g, "here", new RectangleF(bx, y, W - 2 * bx, 32 * S), Same ? "Reinstall" : "Just run it from here", Theme.Card, Theme.Muted, false);
                string foot = Newer ? "You already have a newer version (" + instVer + ") - this download is older." : Upgrade ? "You have " + instVer + ". Your settings come along." : "Remove it anytime from Settings > Apps.";
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
                // the Start with Windows switch counts here too: written next to this copy before it starts
                try { Setup.SetLine(Path.Combine(Program.WriteDir, "AdeeboMeter.settings"), "autostart", autoStart ? "1" : "0"); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("run here: " + e.Message); }
                startHere = true; Close(); return;
            }
            if (key != "main") return;
            if (st == St.Offer)
            {
                if (Same || Newer) { StartExe(instExe); Close(); return; }
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
}
