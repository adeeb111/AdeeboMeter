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
        bool covered, hovered, revealed, widgetOn = true, autoStart = Setup.RunningInstalled;   /* a downloaded or portable copy never starts with Windows unless you ask */ volatile bool quitting; string balloonUrl;
        DateTime dwellStart = DateTime.MinValue; double dwell;
        DateTime lastCover = DateTime.MinValue;
        string hoverKey = ""; bool presenting; int uiTick, drawnVersion = -1; string drawnKey = "", lastChip = ""; int lastLevel = -1;
        ToolStripMenuItem miWidget, miAuto, miSerious, miBig;
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
            settingsPath = Path.Combine(Program.WriteDir, "AdeeboMeter.settings");
            try { string old = Path.Combine(Program.WriteDir, OldName + ".settings"); if (!File.Exists(settingsPath) && File.Exists(old)) File.Copy(old, settingsPath); } catch (Exception e) { Program.Log("settings migrate: " + e.Message); }
            Cfg.ApplyPreset("balanced");
            LoadSettings(); ApplyAutoStart();
            Cfg.Save = SaveSettings; Cfg.SetTheme = SetTheme; Cfg.GetTheme = () => Program.ForceTheme;
            Cfg.SetAutoStart = delegate(bool on) { if (Program.Packaged) { StoreStartup.Open(); return; } autoStart = on; ApplyAutoStart(); SaveSettings(); }; Cfg.GetAutoStart = () => Program.Packaged ? StoreStartup.On : autoStart;
            Theme.Apply(Program.WantDark());
            Program.CleanCaptures(); FileCrypt.SweepParts();
            Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;
            M.Start(); GamePing.Me.Start(M);   /* the Gamer Zone sends nothing until a game is clicked */

            var menu = new ContextMenuStrip();
            var miFull = new ToolStripMenuItem("Open engineer view"); miFull.Font = new Font(miFull.Font, FontStyle.Bold); miFull.Click += delegate { OpenFull(); };
            var miSettings = new ToolStripMenuItem("Settings..."); miSettings.Click += delegate { OpenFull(); if (full != null) full.ShowPage("settings"); };
            miWidget = new ToolStripMenuItem("Hide widget (keep tray icon)"); miWidget.Click += delegate { widgetOn = !widgetOn; SaveSettings(); drawnKey = ""; };
            miBig = new ToolStripMenuItem("Bigger widget"); miBig.Click += delegate { Cfg.BigWidget = !Cfg.BigWidget; SaveSettings(); drawnKey = ""; };
            miSerious = new ToolStripMenuItem("Serious mode (no jokes)"); miSerious.Click += delegate { Voice.Serious = !Voice.Serious; SaveSettings(); drawnKey = ""; };
            var miScreen = new ToolStripMenuItem("Widget on screen");
            miScreen.DropDownOpening += delegate
            {
                miScreen.DropDownItems.Clear(); int n = 0;
                foreach (var sc2 in Screen.AllScreens)
                {
                    n++; string dn = sc2.DeviceName;
                    var it = new ToolStripMenuItem((sc2.Primary ? "Main screen" : "Screen " + n) + "  (" + sc2.Bounds.Width + " x " + sc2.Bounds.Height + ")") { Checked = WidgetScreen().DeviceName == dn };
                    it.Click += delegate { Cfg.WidgetScreen = Screen.AllScreens.Any(z => z.DeviceName == dn && z.Primary) ? "" : dn; SaveSettings(); drawnKey = ""; lastCover = DateTime.MinValue; };
                    miScreen.DropDownItems.Add(it);
                }
            };
            miScreen.DropDownItems.Add("...");
            var miTheme = new ToolStripMenuItem("Theme");
            miThemeAuto = new ToolStripMenuItem("Follow Windows"); miThemeLight = new ToolStripMenuItem("Light"); miThemeDark = new ToolStripMenuItem("Dark");
            miThemeAuto.Click += delegate { SetTheme(-1); }; miThemeLight.Click += delegate { SetTheme(0); }; miThemeDark.Click += delegate { SetTheme(1); };
            miTheme.DropDownItems.Add(miThemeAuto); miTheme.DropDownItems.Add(miThemeLight); miTheme.DropDownItems.Add(miThemeDark);
            miAuto = new ToolStripMenuItem("Start with Windows"); miAuto.Click += delegate { Cfg.SetAutoStart(!Cfg.GetAutoStart()); };
            var miRemove = new ToolStripMenuItem("Uninstall AdeeboMeter...");
            miRemove.Click += delegate { try { using (Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--uninstall") { UseShellExecute = false })) { } } catch (Exception e) { Program.Log("uninstall: " + e.Message); } };
            var miExit = new ToolStripMenuItem("Exit AdeeboMeter"); miExit.ForeColor = Theme.Red; miExit.Click += delegate { Quit(); };
            menu.Items.Add(new ToolStripLabel("AdeeboMeter " + Program.Version + " · " + Program.Tagline) { ForeColor = Theme.Muted });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miFull); menu.Items.Add(miSettings); menu.Items.Add(miWidget); menu.Items.Add(miScreen); menu.Items.Add(miBig); menu.Items.Add(miTheme); menu.Items.Add(miSerious); menu.Items.Add(miAuto); menu.Items.Add(new ToolStripSeparator()); if (Setup.RunningInstalled) menu.Items.Add(miRemove); menu.Items.Add(miExit);
            menu.Opened += delegate { LayeredForm.HoldTop = true; }; menu.Closed += delegate { LayeredForm.HoldTop = false; };
            menu.Opening += delegate { miWidget.Text = widgetOn ? "Hide widget (keep tray icon)" : "Show widget"; miAuto.Checked = Cfg.GetAutoStart(); miSerious.Checked = Voice.Serious; miBig.Checked = Cfg.BigWidget;
                miThemeAuto.Checked = Program.ForceTheme < 0; miThemeLight.Checked = Program.ForceTheme == 0; miThemeDark.Checked = Program.ForceTheme == 1; };

            tray.Icon = Theme.MakeIcon(Math.Max(16, (int)(16 * sc)));
            tray.Text = "AdeeboMeter"; tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) OpenFull(); };
            tray.Visible = true;
            // Alerts: only when there's something to do; never at night (Settings) or while you present / play full screen.
            Alerts.Show = delegate(string title, string text)
            {
                int hour = DateTime.Now.Hour;
                if (Cfg.Quiet && (hour >= 22 || hour < 7)) return;
                if (!Native.CanNotify()) return;
                try { marshal.BeginInvoke((Action)delegate { if (quitting) return; balloonUrl = null; tray.ShowBalloonTip(8000, title, text, ToolTipIcon.Info); }); } catch { }
            };
            tray.BalloonTipClicked += delegate { if (balloonUrl != null) { Crash.OpenUrl(balloonUrl); balloonUrl = null; } else OpenFull(); };
            Task.Factory.StartNew(() => History.Tidy());
            // A crash last time left a note next to the exe: offer to report it (opens GitHub in your browser; you see it before sending).
            try
            {
                if (File.Exists(Crash.FilePath))
                {
                    string note = File.ReadAllText(Crash.FilePath); File.Delete(Crash.FilePath + ".seen"); File.Move(Crash.FilePath, Crash.FilePath + ".seen");
                    var once = new System.Windows.Forms.Timer { Interval = 4000 };
                    once.Tick += delegate { once.Stop(); once.Dispose(); balloonUrl = Crash.IssueUrl(note); tray.ShowBalloonTip(15000, "AdeeboMeter closed unexpectedly last time", "Click here to report it on GitHub - you'll see exactly what is sent before anything goes.", ToolTipIcon.Warning); };
                    once.Start();
                }
            }
            catch (Exception e) { Program.Log("crash note: " + e.Message); }

            widget.Show(); detailWin.Show(); widget.Vanish(); detailWin.Vanish();

            // Ctrl+Alt+A opens / closes the engineer view from anywhere
            try { hotkey = new HotKey(ToggleFull); if (!hotkey.Ok) Program.Log("Ctrl+Alt+A is taken by another app"); } catch (Exception e) { Program.Log("hotkey: " + e.Message); }
            miFull.ShortcutKeyDisplayString = hotkey != null && hotkey.Ok ? "Ctrl+Alt+A" : "(Ctrl+Alt+A taken)";

            // Light / dark follows Windows and switches live
            prefHandler = delegate
            {
                // Windows may raise this on its own thread: the theme switch always happens on the UI thread
                if (marshal.InvokeRequired) { try { marshal.BeginInvoke((Action)delegate { bool d2 = Program.WantDark(); if (d2 != Theme.Dark) { Theme.Apply(d2); drawnKey = ""; if (full != null && !full.IsDisposed) full.ApplyTheme(); } }); } catch (InvalidOperationException) { } return; }
                bool d = Program.WantDark();
                if (d != Theme.Dark) { Theme.Apply(d); drawnKey = ""; if (full != null && !full.IsDisposed) full.ApplyTheme(); }
            };
            SystemEvents.UserPreferenceChanged += prefHandler;
            ui.Interval = 50; ui.Tick += delegate { OnUi(); }; ui.Start();
            // after sleep, unlock or a screen change: draw again from scratch (place, layer and size can all have changed)
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnScreens; Microsoft.Win32.SystemEvents.PowerModeChanged += OnPower; Microsoft.Win32.SystemEvents.SessionSwitch += OnSession;

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
                            int w = WaitHandle.WaitAny(both, 30000);   // woken by either event; the timeout only re-checks quitting (a background thread ends with the app anyway)
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
            if (!first && Cfg.Mode == "") Cfg.Mode = "pro";   // upgrading: keep every tab it already had
            // a named preset means its timings: an upgrade picks up what the preset is now. Your own mix ("custom") and the game settings are kept.
            if (Cfg.PresetSaved && Cfg.Preset != "custom") Cfg.ApplyPreset(Cfg.Preset);   // the Gamer Zone rows switch to "custom" when changed, so a named preset means all its timings
            Policy.Load(); Policy.Apply();
            L.Use(Cfg.Lang);   // an IT team's AdeeboMeter.policy wins over the user's settings (it can only switch things off)
            if (first) { ApplyAutoStart(); SaveSettings(); }
        }
        void SaveSettings()
        {
            try
            {
                string tmp = settingsPath + ".tmp";
                File.WriteAllLines(tmp, new[] { "autostart=" + (autoStart ? 1 : 0), "widget=" + (widgetOn ? 1 : 0), "serious=" + (Voice.Serious ? 1 : 0), "theme=" + (Program.ForceTheme == 1 ? "dark" : Program.ForceTheme == 0 ? "light" : "auto") }.Concat(Cfg.Lines()));
                Program.ReplaceFile(tmp, settingsPath);
            }
            catch (Exception e) { Program.Log("settings: " + e.Message); }
        }
        const string OldName = "Ade" + "boMeter";
        void ApplyAutoStart()
        {
            if (Program.Packaged) return;   // a Store copy starts through its package's startup entry (StoreStartup), never the Run key
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k == null) return;
                    if (k.GetValue(OldName) != null) k.DeleteValue(OldName);   // the app used to be called AdeboMeter - don't start the old copy
                    // a copy only ever changes its own entry: a downloaded copy never takes over (or removes) the installed copy's
                    var cur = k.GetValue("AdeeboMeter") as string; string me = "\"" + Application.ExecutablePath + "\"";
                    bool installedOwns = cur != null && !Setup.RunningInstalled && Setup.Under(cur.Trim().Trim('"'), Setup.Base) && File.Exists(cur.Trim().Trim('"'));
                    if (autoStart) { if (installedOwns) Program.Log("autostart: the installed copy already starts with Windows - left as it is"); else if (cur != me) k.SetValue("AdeeboMeter", me); }
                    else if (cur != null && string.Equals(cur.Trim().Trim('"'), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase)) k.DeleteValue("AdeeboMeter");
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

        DateTime lastTop, lastCard; string lastHead = ""; volatile bool locked;   // locked: the lock screen is up
        void OnScreens(object s, EventArgs e) { drawnKey = ""; lastCover = DateTime.MinValue; }
        void OnPower(object s, Microsoft.Win32.PowerModeChangedEventArgs e) { if (e.Mode == Microsoft.Win32.PowerModes.Resume) { M.Resumed(); drawnKey = ""; lastCover = DateTime.MinValue; } }
        void OnSession(object s, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock || e.Reason == Microsoft.Win32.SessionSwitchReason.ConsoleDisconnect) locked = true;   // the lock screen: the widget rests
            if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock || e.Reason == Microsoft.Win32.SessionSwitchReason.ConsoleConnect || e.Reason == Microsoft.Win32.SessionSwitchReason.RemoteConnect) locked = false;
            if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock || e.Reason == Microsoft.Win32.SessionSwitchReason.ConsoleConnect || e.Reason == Microsoft.Win32.SessionSwitchReason.RemoteConnect) { drawnKey = ""; lastCover = DateTime.MinValue; } }
        // logoff or shutdown: no live capture left on disk (a Quit may never come)
        // SystemEvents raise this on their own thread: the view (its timers, its captures) is only touched on the UI thread -
        // synchronously, so it's done before Windows logs off
        void OnSessionEnding(object s, Microsoft.Win32.SessionEndingEventArgs e)
        {
            Action stop = delegate { try { if (full != null && !full.IsDisposed) full.StopCaptures(); } catch (Exception ex) { Program.Log("session end: " + ex.Message); } };
            try { if (marshal.InvokeRequired) marshal.Invoke(stop); else stop(); } catch (InvalidOperationException ex) { Program.Log("session end: " + ex.Message); }
            Program.CleanCaptures();
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr h);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetLastActivePopup(IntPtr h);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        System.Windows.Forms.Timer quitWait; int quitTries;
        void Quit()
        {
            // a dialog is open over the view (Ctrl+K, Save, Export - a form or a Windows file dialog): close it and come back once
            // it has returned, so nothing runs on a disposed view after ShowDialog
            if (!quitting && full != null && !full.IsDisposed && full.IsHandleCreated && !IsWindowEnabled(full.Handle) && quitTries < 30)
            {
                quitTries++;
                foreach (var f in Application.OpenForms.Cast<Form>().Where(f => f != full && f.Modal).ToList()) try { f.Close(); } catch (InvalidOperationException) { }
                var pop = GetLastActivePopup(full.Handle); if (pop != IntPtr.Zero && pop != full.Handle) PostMessage(pop, 0x0010 /*WM_CLOSE*/, IntPtr.Zero, IntPtr.Zero);
                if (quitWait == null) { quitWait = new System.Windows.Forms.Timer { Interval = 100 }; quitWait.Tick += delegate { quitWait.Stop(); Quit(); }; }
                quitWait.Start(); return;
            }
            // a dialog that refused to close in 3 s: quitting now would dispose the view under it - give up this quit instead
            if (!quitting && full != null && !full.IsDisposed && full.IsHandleCreated && !IsWindowEnabled(full.Handle)) { quitTries = 0; Program.Log("quit: a dialog is still open - quit abandoned"); return; }
            if (quitWait != null) { quitWait.Stop(); quitWait.Dispose(); quitWait = null; }
            Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnScreens; Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPower; Microsoft.Win32.SystemEvents.SessionSwitch -= OnSession;   // static events: always let go
            if (quitting) return;
            quitting = true; ui.Stop(); GamePing.Me.Stop(); M.Stop(); Usage.Save();
            try { SystemEvents.UserPreferenceChanged -= prefHandler; } catch { }
            if (hotkey != null) hotkey.Dispose();
            tray.Visible = false; tray.Dispose();
            // a dialog open over the view (Save, Export, Ctrl+K) goes first, so nothing runs on a disposed view when it returns
            if (full != null && !full.IsDisposed) { foreach (var f in Application.OpenForms.Cast<Form>().Where(f => f != full && f.Modal).ToList()) try { f.Close(); } catch (InvalidOperationException) { } full.Close(); full.Dispose(); }
            widget.Dispose(); detailWin.Dispose();
            Program.CleanCaptures();
            ExitThread();
        }

        // ---------- 50 ms: hover, dwell, cover check - and draw only when something changed
        // the chosen screen's work area - the main screen when none is chosen or it was unplugged
        // null when Windows has no screen at all right now (a disconnected Remote Desktop session, every display off)
        static Screen WidgetScreen() { var w = Cfg.WidgetScreen; var all = Screen.AllScreens; return (w == "" ? null : all.FirstOrDefault(x => x.DeviceName == w)) ?? Screen.PrimaryScreen ?? all.FirstOrDefault(); }
        // where the card is: what's actually on screen (a bigger widget, a taller card), else the corner it opens into
        Rectangle Zone()
        {
            var scr = WidgetScreen(); if (scr == null) return Rectangle.Empty;
            var wa = scr.WorkingArea; int m = (int)(10 * Draw.S);
            int w = (int)((Cfg.BigWidget ? 343 : 264) * Draw.S), h = (int)(300 * Draw.S);
            var z = new Rectangle(wa.Right - m - w, wa.Bottom - m - h, w, h);
            var p = widget != null ? widget.Placed : Rectangle.Empty;
            return p.Width > 1 && p.Height > 1 ? Rectangle.Union(z, p) : z;
        }

        void OnUi()
        {
            try
            {
                uiTick++;
                if (WidgetScreen() == null) { if (widget.Placed.Width > 1) { widget.Vanish(); detailWin.Vanish(); drawnKey = ""; } ui.Interval = 1000; return; }   // no screen: nothing to draw on, nothing to log
                // the view hides the widget only when it's open on the widget's own screen (on two screens it may be elsewhere or buried)
                bool viewUp = full != null && !full.IsDisposed && full.Visible && full.WindowState != FormWindowState.Minimized && Screen.FromControl(full).DeviceName == WidgetScreen().DeviceName && Form.ActiveForm == full;
                if ((Clock.Mono - lastTop).TotalSeconds > 30) { lastTop = Clock.Mono; widget.ClaimTop(); }   // every 30 s: claim the top layer back - no redraw
                if ((uiTick & 7) == 0) presenting = Native.Presenting();   // checked twice a second at most
                if (!widgetOn || viewUp || presenting || locked) { if (widget.Placed.Width > 1) { widget.Vanish(); detailWin.Vanish(); drawnKey = ""; } ui.Interval = !widgetOn || locked ? 2000 : 500; return; }   // hidden or locked: nothing to draw, a slow tick only
                // Poll fast only when the mouse is near the corner; otherwise 4 times a second is plenty.
                var near = Zone(); near.Inflate((int)(120 * Draw.S), (int)(120 * Draw.S));
                int want = near.Contains(Cursor.Position) || hovered || dwell > 0 ? 50 : Cfg.Preset == "penny" ? 1000 : 500;   // the mouse far away: twice a second, once on Penny
                if (ui.Interval != want) ui.Interval = want;
                if ((Clock.Mono - lastCover).TotalMilliseconds >= 400)
                {
                    lastCover = Clock.Mono;
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
                string state = compact + "|" + hovered + "|" + key + "|" + covered + "|" + ((int)(dwell * 20)) + "|" + Voice.Serious + "|" + Cfg.BigWidget;
                bool fresh = M.Version != drawnVersion;
                // The folded chip redraws only when its words or colour change; the open card once a second while you look at it,
                // and unhovered only when its level or headline changes (or every 10 s for the numbers)
                bool cardDue = !compact && (hovered || s.Level != lastLevel || s.Headline != lastHead || (Clock.Mono - lastCard).TotalSeconds >= 10);
                if (state != drawnKey || (fresh && (cardDue || (compact && (s.Chip != lastChip || s.Level != lastLevel)))))
                {
                    Draw.CardHovered = hovered; Render(s, compact); drawnKey = state; drawnVersion = M.Version; if (!compact) { lastCard = Clock.Mono; lastHead = s.Headline; }
                }
            }
            catch (Exception e) { Program.Log("ui: " + e.Message); }
        }

        void Render(Snap s, bool compact)
        {
            var wa = WidgetScreen().WorkingArea; int m = (int)(10 * Draw.S);
            using (var bmp = compact ? Draw.Chip(s, dwell) : Draw.Card(s, hits))
            {
                if (compact) hits.Clear();
                widget.Present(bmp, hovered ? (byte)255 : FADE, wa.Right - m - bmp.Width, wa.Bottom - m - bmp.Height);
            }
            lastChip = s.Chip; lastLevel = s.Level;
            Detail d;
            if (!compact && hovered && hoverKey != "" && s.Details.TryGetValue(hoverKey, out d))
                using (var db = Draw.DetailCard(d)) detailWin.Present(db, 255, Math.Max(wa.Left, widget.Placed.X - db.Width - (int)(8 * Draw.S)), Math.Max(wa.Top, wa.Bottom - m - db.Height));   /* on this screen, never above its top */
            else if (detailWin.Placed.Width > 1) detailWin.Vanish();
            string tip = "AdeeboMeter - " + s.Chip.ToLowerInvariant();
            if (tip.Length > 63) tip = tip.Substring(0, 63);   // Windows keeps 63 characters: compare what it holds, or it is rewritten every second
            if (tray.Text != tip) tray.Text = tip;
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

    // The Store copy's "Start with Windows": its package declares a startup entry (Package.appxmanifest, TaskId AdeeboMeter).
    // Windows keeps the on / off switch; we only read it (HKCU, read-only) and hand the user to Settings > Apps > Startup.
    static class StoreStartup
    {
        public static bool On
        {
            get
            {
                try
                {
                    int n = 0;
                    if (Native.GetCurrentPackageFamilyName(ref n, null) != 122 || n <= 0 || n > 300) return false;
                    var buf = new char[n]; if (Native.GetCurrentPackageFamilyName(ref n, buf) != 0) return false;
                    string pfn = new string(buf, 0, Math.Max(0, n - 1));   /* n counts the closing NUL */
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\" + pfn + @"\AdeeboMeter"))
                    {
                        object v = k == null ? null : k.GetValue("State");
                        if (!(v is int)) return true;   /* never touched yet: the manifest's own default, on */
                        return (int)v == 2 || (int)v == 4;   /* 2 enabled, 4 enabled by policy; 0 / 1 / 3 off */
                    }
                }
                catch (Exception e) { if (!(e is System.Security.SecurityException || e is UnauthorizedAccessException || e is System.IO.IOException || e is EntryPointNotFoundException)) throw; return false; }
            }
        }
        public static void Open()
        {
            try { using (Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true })) { } }
            catch (Exception e) { if (!(e is System.ComponentModel.Win32Exception || e is InvalidOperationException)) throw; Program.Log("startup apps: " + e.Message); }
        }
    }
}
