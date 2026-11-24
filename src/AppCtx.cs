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
        public static volatile Snap Rewind; public static DateTime RewindUntil;   // the Time machine's minute, shown on the widget while you drag
        readonly Control marshal = new Control();
        readonly Icon appIcon;
        FullView full;
        readonly Dictionary<string, RectangleF> hits = new Dictionary<string, RectangleF>();
        bool covered, hovered, revealed, widgetOn = true, autoStart = Setup.RunningInstalled;   /* a downloaded or portable copy never starts with Windows unless you ask */ volatile bool quitting; bool openFirst; string crashNote, balloonTool; HotKey slowKey; Rectangle foldedAt; DateTime insideAt;
        DateTime dwellStart = DateTime.MinValue; double dwell;
        // move mode (Adeeb, 24 Nov): the widget stays click-through until you rest on it 1.5 s, hold Ctrl over it, or pick
        // Corner widget > Move widget. Then a dashed outline shows; drag moves it, a click opens the full window. It locks
        // again when the mouse leaves or after 8 s untouched.
        bool moveOn, moveSpent, moveByTray, dragging, ghost; Point? dragFrom; Point dragOrigin, lastCur;
        DateTime hoverSince = DateTime.MinValue, moveTouched;
        DateTime lastCover = DateTime.MinValue;
        string hoverKey = ""; bool presenting; int uiTick, drawnVersion = -1; string drawnKey = "", lastChip = "", lastSpeed = "", lastOther = ""; int lastLevel = -1; DateTime speedAt = DateTime.MinValue, otherAt = DateTime.MinValue;   // the strip's two clocks (2 s / 5 s)
        ToolStripMenuItem miWidget, miBig;
        readonly string settingsPath;
        EventWaitHandle openEvt, ackEvt, quitEvt;
        HotKey hotkey; UserPreferenceChangedEventHandler prefHandler;
        readonly bool forcedByArgs = Program.ForceTheme >= 0;
        static byte FADE { get { return (byte)Cfg.Fade; } }      // 30 % by default (Settings)
        static int DWELL_MS { get { return Cfg.DwellMs; } }     // rest the mouse this long on the folded chip to open it

        public AppCtx()
        {
            float sc; using (var g = Graphics.FromHwnd(IntPtr.Zero)) sc = g.DpiX / 96f;
            Draw.Init(sc); Draw.Base = sc;
            appIcon = Theme.MakeIcon(32);
            marshal.CreateControl(); var h = marshal.Handle;
            settingsPath = Path.Combine(Program.WriteDir, "AdeeboMeter.settings");
            try { string old = Path.Combine(Program.WriteDir, OldName + ".settings"); if (!File.Exists(settingsPath) && File.Exists(old)) File.Copy(old, settingsPath); } catch (Exception e) { Program.Log("settings migrate: " + e.Message); }
            Cfg.ApplyPreset("balanced");
            LoadSettings(); ApplyAutoStart();
            Draw.UseLanguageFonts();
            if (!Cfg.SidebarTried) Cfg.Layout = "sidebar";   // 1.1.5: the sidebar is the default look; whoever chose a layout keeps theirs (Classic is in ⋯ and Settings)
            Cfg.Save = SaveSettings; Cfg.SetTheme = SetTheme; Cfg.GetTheme = () => Program.ForceTheme;
            Cfg.SetAutoStart = delegate(bool on) { if (Program.Packaged) { StoreStartup.Open(); return; } autoStart = on; ApplyAutoStart(); SaveSettings(); }; Cfg.GetAutoStart = () => Program.Packaged ? StoreStartup.On : autoStart;
            Theme.Apply(Program.WantDark());
            Program.CleanCaptures(); FileCrypt.SweepParts();
            Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;
            M.Start(); GamePing.Me.Start(M);   /* the Gamer Zone sends nothing until a game is clicked */

            // 23 Nov polish: seven items with the verdict on top, in AdeeboMeter's colours (Menus.cs). Theme, Serious mode and
            // Start with Windows live in Settings - set once, not daily; the widget's choices sit under Corner widget ›.
            var menu = Menus.Make(); menu.RightToLeft = L.IsRtl ? RightToLeft.Yes : RightToLeft.No;
            var miFull = new ToolStripMenuItem(L.T("Open AdeeboMeter")); miFull.Font = Draw.fBodyB; miFull.Click += delegate { OpenFull(); };
            var miSettings = new ToolStripMenuItem(L.T("Settings...")); miSettings.Click += delegate { OpenFull(); if (full != null) full.ShowPage("settings"); };
            miWidget = new ToolStripMenuItem(L.T("Hide widget (keep tray icon)")); miWidget.Click += delegate { widgetOn = !widgetOn; SaveSettings(); drawnKey = ""; };
            miBig = new ToolStripMenuItem(L.T("Bigger widget")); miBig.Click += delegate { Cfg.BigWidget = !Cfg.BigWidget; SaveSettings(); drawnKey = ""; };
            var miMove = new ToolStripMenuItem(L.T("Move widget")); miMove.Click += delegate { bool was = widgetOn; widgetOn = true; if (!was) SaveSettings(); moveByTray = true; trayUnlockAt = Clock.Mono; Unlock(); };   /* M-4: a re-shown widget stays shown */
            var miReset = new ToolStripMenuItem(L.T("Reset widget position")); miReset.Click += delegate { ResetPos(); };
            var miHold = new ToolStripMenuItem(L.T("Hold this")); miHold.Click += delegate { if (Corner.Holding) Corner.Release(); else { var ls = M.Latest; Corner.Hold(ls != null && ls.Call); } drawnKey = ""; };
            var miBack = new ToolStripMenuItem(L.T("Back to my widget")); miBack.Click += delegate { Corner.Back(); drawnKey = ""; };
            var miScreen = new ToolStripMenuItem(L.T("Widget on screen"));
            miScreen.DropDownOpening += delegate
            {
                miScreen.DropDownItems.Clear(); int n = 0;
                foreach (var sc2 in Screen.AllScreens)
                {
                    n++; string dn = sc2.DeviceName;
                    var it = new ToolStripMenuItem((sc2.Primary ? L.T("Main screen") : L.T("Screen ") + n) + "  (" + sc2.Bounds.Width + " x " + sc2.Bounds.Height + ")") { Checked = WidgetScreen().DeviceName == dn };
                    it.Click += delegate { Cfg.WidgetScreen = Screen.AllScreens.Any(z => z.DeviceName == dn && z.Primary) ? "" : dn; SaveSettings(); drawnKey = ""; lastCover = DateTime.MinValue; };
                    miScreen.DropDownItems.Add(it);
                }
            };
            miScreen.DropDownItems.Add("...");
            var miStyle = new ToolStripMenuItem(L.T("Widget style"));   // the same eight as Settings > Widget, with pictures there
            foreach (var st in new[] { new[] { "speed", "Speed only" }, new[] { "speedstatus", "Speed + status" }, new[] { "speedapps", "Speed + apps" }, null, new[] { "everyday", "Everyday" }, new[] { "gamer", "Gamer" }, new[] { "calls", "Calls" }, new[] { "engineer", "Engineer" }, new[] { "auto", "Auto" }, null, new[] { "markets", "Markets" }, new[] { "sports", "Sports" }, new[] { "data", "Data use" }, new[] { "weather", "Weather" }, new[] { "mine", "My widget" } })
            {
                if (st == null) { miStyle.DropDownItems.Add(new ToolStripSeparator()); continue; }
                string id = st[0]; var it = new ToolStripMenuItem(L.T(st[1])) { Tag = id }; it.Click += delegate { Cfg.WidgetStyle = id; SaveSettings(); drawnKey = ""; };
                miStyle.DropDownItems.Add(it);
            }
            miStyle.DropDownOpening += delegate { foreach (ToolStripItem it in miStyle.DropDownItems) { var mi = it as ToolStripMenuItem; if (mi != null) mi.Checked = (string)mi.Tag == Cfg.WidgetStyle; } };
            // "Check now" is the full checkup everywhere (review 24 Nov: it named three different actions); Ctrl+Alt+S stays the
            // "it feels slow right now" recorder
            var miSlow = new ToolStripMenuItem(L.T("Check now")); miSlow.Click += delegate { OpenFull(); if (full != null && !full.IsDisposed) full.StartCheckup(); };
            var miIsp = new ToolStripMenuItem(L.T("Send to my ISP")); miIsp.Click += delegate { OpenFull(); if (full != null && !full.IsDisposed) full.ShowTool("ispreport", false); };
            var miPhone = new ToolStripMenuItem(L.T("Your phone...")); miPhone.Click += delegate { OpenRemote(); };
            PhoneHub.Start = delegate { try { marshal.BeginInvoke((Action)OpenRemote); } catch (InvalidOperationException) { } };
            PhoneHub.Show = kind => { try { marshal.BeginInvoke((Action)ShowPhonePage); } catch (InvalidOperationException) { } };
            try { slowKey = new HotKey(Keys.S, FeltSlow); if (!slowKey.Ok) Program.Log("Ctrl+Alt+S is taken by another app"); } catch (Exception e) { Program.Log("slow hotkey: " + e.Message); }   // taken = just the menu item
            var miRemove = new ToolStripMenuItem(L.T("Uninstall AdeeboMeter..."));
            miRemove.Click += delegate { try { using (Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--uninstall") { UseShellExecute = false })) { } } catch (Exception e) { Program.Log("uninstall: " + e.Message); } };
            var miExit = new ToolStripMenuItem(L.T("Exit AdeeboMeter")) { Tag = "exit" }; miExit.Click += delegate { Quit(); };
            var miState = Menus.Add(menu, "AdeeboMeter " + Program.Version, "", null); miState.Enabled = false;
            var miCorner = new ToolStripMenuItem(L.T("Corner widget"));
            miCorner.DropDownItems.Add(miStyle); miCorner.DropDownItems.Add(miScreen); miCorner.DropDownItems.Add(miBig); miCorner.DropDownItems.Add(new ToolStripSeparator()); miCorner.DropDownItems.Add(miMove); miCorner.DropDownItems.Add(miReset); miCorner.DropDownItems.Add(miHold); miCorner.DropDownItems.Add(miBack); miCorner.DropDownItems.Add(new ToolStripSeparator()); miCorner.DropDownItems.Add(miWidget);
            foreach (var sub in new[] { miCorner, miStyle, miScreen }) Menus.Style(sub.DropDown);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miFull); menu.Items.Add(miSlow); menu.Items.Add(miIsp); menu.Items.Add(miPhone); menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miCorner); menu.Items.Add(miSettings); menu.Items.Add(new ToolStripSeparator()); if (Setup.RunningInstalled) menu.Items.Add(miRemove); menu.Items.Add(miExit);
            menu.Opened += delegate { LayeredForm.HoldTop = true; }; menu.Closed += delegate { LayeredForm.HoldTop = false; };
            menu.Opening += delegate { var ls = M.Latest; miState.Text = "AdeeboMeter " + Program.Version; ((Menus.Row)miState).Line = ls == null ? "" : (ls.Level == 1 || ls.Level == 2 ? "● " : "") + ls.Headline;
                bool past = ls != null && ls.Path.Length > 2 && (ls.Path[0] == 0 || ls.Path[0] == 3) && ls.Path.Skip(2).Any(p => p == 1 || p == 2); miIsp.Visible = past;
                miHold.Text = Corner.Holding ? L.T("Stop holding") : L.T("Hold this"); miBack.Visible = Corner.TakenOver || Corner.Holding; miWidget.Text = widgetOn ? L.T("Hide widget (keep tray icon)") : L.T("Show widget"); miBig.Checked = Cfg.BigWidget; };

            tray.Icon = Theme.MakeIcon(Math.Max(16, (int)(16 * sc)));
            tray.Text = "AdeeboMeter"; tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) OpenFull(); };
            tray.Visible = true;
            // Alerts: only when there's something to do; never at night (Settings) or while you present / play full screen.
            Alerts.CanShow = delegate { int hr = DateTime.Now.Hour; return !(Cfg.Quiet && (hr >= 22 || hr < 7)) && DateTime.UtcNow >= Remote.MuteUntil && Native.CanNotify(); };
            Alerts.Show = delegate(string title, string text)
            {
                int hour = DateTime.Now.Hour;
                if (Cfg.Quiet && (hour >= 22 || hour < 7)) return;
                if (DateTime.UtcNow < Remote.MuteUntil) return;   // muted from the phone
                if (!Native.CanNotify()) return;
                try { marshal.BeginInvoke((Action)delegate { if (quitting) return; BalloonIs(null, null); tray.ShowBalloonTip(8000, title, text, ToolTipIcon.Info); }); } catch { }
            };
            // round 6 ML-1: a click acts on the balloon that was clicked - each balloon sets its own action, and one that closes
            // unclicked takes its action with it (a crash note no longer hijacks the next alert's click)
            tray.BalloonTipClosed += delegate { BalloonIs(null, null); };
            tray.BalloonTipClicked += delegate
            {
                // a crash note: the report box, with the note in it - the person reads it and presses Send
                if (crashNote != null) { string n = crashNote; crashNote = null; OpenFull(); if (full != null && !full.IsDisposed) full.ReportDialog("bug", "It closed unexpectedly. I was doing: \n\n---\nCrash note (no user names or folders):\n" + n); return; }
                if (balloonTool != null) { string t = balloonTool; balloonTool = null; OpenFull(); if (full != null && !full.IsDisposed) full.ShowTool(t, true); return; }
                OpenFull();
            };
            Relay.Flush();   // reports written while offline go now
            Task.Factory.StartNew(() => History.Tidy());
            Task.Factory.StartNew(() => { Remote.Sweep(); OverTime.SweepFriend(); });
            // A crash last time left a note next to the exe: offer to report it (the report box, with the note; you see it before sending).
            try
            {
                if (File.Exists(Crash.FilePath))
                {
                    string note; using (var cr = new StreamReader(Crash.FilePath)) { var cb = new char[6000]; int cn = cr.ReadBlock(cb, 0, cb.Length); note = new string(cb, 0, cn); }   /* L-48: only what's shown is read */ File.Delete(Crash.FilePath + ".seen"); File.Move(Crash.FilePath, Crash.FilePath + ".seen");
                    var once = new System.Windows.Forms.Timer { Interval = 4000 };
                    once.Tick += delegate { once.Stop(); once.Dispose(); BalloonIs(note.Length > 6000 ? note.Substring(0, 6000) : note, null); tray.ShowBalloonTip(15000, "AdeeboMeter closed unexpectedly last time", "Click here to report it - you'll see exactly what is sent before anything goes. No account needed.", ToolTipIcon.Warning); };
                    once.Start();
                }
            }
            catch (Exception e) { Program.Log("crash note: " + e.Message); }

            widget.Show(); detailWin.Show(); widget.Vanish(); detailWin.Vanish();
            widget.MouseDown += (s2, e2) => { if (!moveOn) return; if (e2.Button != MouseButtons.Left) { Lock(); return; } dragFrom = Cursor.Position; dragOrigin = widget.Placed.Location; dragging = false; dragGame = Corner.UseGamePos; moveTouched = Clock.Mono; };
            widget.MouseWheel += (s2, e2) => { if (moveOn && dragFrom == null) Lock(); };
            widget.MouseMove += (s2, e2) => WidgetDrag(); ResetWidget = ResetPos;
            widget.MouseUp += (s2, e2) => WidgetDrop();
            widget.MouseCaptureChanged += (s2, e2) => { if (dragFrom != null && Control.MouseButtons != MouseButtons.Left) WidgetDrop(); };

            // Ctrl+Alt+A opens / closes the engineer view from anywhere
            try { hotkey = new HotKey(ToggleFull); if (!hotkey.Ok) Program.Log("Ctrl+Alt+A is taken by another app"); } catch (Exception e) { Program.Log("hotkey: " + e.Message); }
            miFull.ShortcutKeyDisplayString = hotkey != null && hotkey.Ok ? "Ctrl+Alt+A" : "(Ctrl+Alt+A taken)";

            // Light / dark follows Windows and switches live
            prefHandler = delegate
            {
                // Windows may raise this on its own thread: the theme switch always happens on the UI thread
                if (marshal.InvokeRequired) { try { marshal.BeginInvoke((Action)delegate { bool d2 = Program.WantDark(); if (d2 != Theme.Dark || Theme.ContrastChanged()) { Theme.Apply(d2); drawnKey = ""; if (full != null && !full.IsDisposed) full.ApplyTheme(); } }); } catch (InvalidOperationException) { } return; }
                bool d = Program.WantDark();
                if (d != Theme.Dark || Theme.ContrastChanged()) { Theme.Apply(d); drawnKey = ""; if (full != null && !full.IsDisposed) full.ApplyTheme(); }
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
                            if (w == 0) marshal.BeginInvoke((Action)delegate { ackEvt.Set(); OpenFull(); OpenHandedFile(); });
                            else if (w == 1) { Program.Log("asked to quit (update or removal)"); marshal.BeginInvoke((Action)Quit); }
                        }
                        catch { }
                    }
                });
                t.IsBackground = true; t.Start();
            }
            catch (Exception e) { Program.Log("single: " + e.Message); }

            if (openFirst && !Program.Restarted && Program.StartView == null && Program.StartFile == null)
            {
                var hello = new System.Windows.Forms.Timer { Interval = 1500 };
                hello.Tick += delegate { hello.Stop(); hello.Dispose(); OpenFull(); };
                hello.Start();
            }
            else if (Program.Restarted && Program.StartView == null)
            {
                var again = new System.Windows.Forms.Timer { Interval = 800 };
                again.Tick += delegate { again.Stop(); again.Dispose(); OpenFull(); };
                again.Start();
            }
            else if (Program.StartView != null)
            {
                var once = new System.Windows.Forms.Timer { Interval = Program.StartViewMs };
                once.Tick += delegate { once.Stop(); once.Dispose(); OpenFull(); if (full != null) full.Debug(Program.StartView, Program.StartTip, Program.StartDest); };
                once.Start();
            }
            else if (Program.StartFile != null)
            {
                var once = new System.Windows.Forms.Timer { Interval = 1200 };
                once.Tick += delegate { once.Stop(); once.Dispose(); OpenFull(); if (full != null && !full.IsDisposed) full.OpenLocked(Program.StartFile); };
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
            bool first = !File.Exists(settingsPath); int keys = 0, parsed = 0; bool unreadable = false;   // keys: lines other than the installer's own "autostart"
            try
            {
                if (!first)
                    foreach (var line in SafeFile.ReadText(settingsPath).Split('\n').Select(x => x.TrimEnd('\r')))   // bug-hunt L-48: capped at 1 MB - a bigger one is treated as damaged
                    {
                        int eq = line.IndexOf('='); if (eq <= 0) continue; parsed++;
                        var kv = new[] { line.Substring(0, eq), line.Substring(eq + 1) };
                        if (kv[0] != "autostart") keys++;
                        if (kv[0] == "autostart") autoStart = kv[1] == "1";
                        else if (kv[0] == "widget") widgetOn = kv[1] == "1";
                        else if (kv[0] == "serious") Voice.Serious = kv[1] == "1";
                        else if (kv[0] == "theme" && !forcedByArgs) { int pi = Array.IndexOf(Theme.Palettes, kv[1]); Program.ForceTheme = pi >= 0 ? pi + 2 : kv[1] == "dark" ? 1 : kv[1] == "light" ? 0 : -1; }
                        else Cfg.Read(kv[0], kv[1]);
                    }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; unreadable = true; Program.Log("settings unreadable: " + e.GetType().Name); }
            // bug-hunt ML-134: a damaged settings file is kept aside (AdeeboMeter.settings.damaged) and said in the log, never
            // silently overwritten with defaults as if this were a first start
            try { if (!first && (unreadable || (parsed == 0 && new FileInfo(settingsPath).Length > 0))) { File.Copy(settingsPath, settingsPath + ".damaged", true); Program.Log("settings file damaged - kept a copy as " + Path.GetFileName(settingsPath) + ".damaged, starting from defaults"); } }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            // 22 Nov: Sports became its own page - once, it joins a saved bar right after Streaming
            Func<string, string> sportsNav = nav0 => { var l = nav0.Split(',').ToList(); if (l.Remove("toolbox")) l.Insert(Math.Min(1, l.Count), "toolbox"); int si = l.IndexOf("stream"); if (si >= 0 && !l.Contains("sports") && l.Count(z => z.Trim() != "") < 8) l.Insert(si + 1, "sports"); else if (si >= 0 && !l.Contains("sports")) Program.Log("nav: Sports not added - the bar is full (it's in More)");   /* R2-042: a full bar keeps every tab you chose */ return Cfg.NavClean(string.Join(",", l)); };
            if (!first && !Cfg.NavSports) { Cfg.Nav = sportsNav(Cfg.Nav); Views.MigrateNav(sportsNav); }   // review 1.1.6 P1 #16: kept views too   // and Toolbox right after Home (Adeeb, 22 Nov) - once; Settings › Navigation can move it again
            Cfg.NavSports = true;
            if (!first && keys == 0) first = true;   // only what the installer wrote (autostart): this is a first start, not an upgrade
            // review 1.1.6 P1 #1: a file that says setupdone=0 is a fresh install whose questions are still open - not an upgrade
            bool pendingSetup = !first && !Cfg.SetupDone;
            // review 1.1.6 D1 #9 (and Adeeb: "it should open on that language"): a first start speaks the Windows display language
            // when AdeeboMeter has it; the language button at the top switches back in one click
            if (first) { string ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; if (L.Languages.Any(z => z[0] == ui)) Cfg.Lang = ui; }
            if (!first && !pendingSetup && Cfg.Mode == "") Cfg.Mode = "pro";   // upgrading: keep every tab it already had
            // a named preset means its timings: an upgrade picks up what the preset is now. Your own mix ("custom") is kept; a named preset sets the game timings too.
            if (Cfg.PresetSaved && Cfg.Preset != "custom") Cfg.ApplyPreset(Cfg.Preset);   // the Gamer Zone rows switch to "custom" when changed, so a named preset means all its timings
            Policy.Load(); Policy.Apply();
            L.Use(Cfg.Lang);   // an IT team's AdeeboMeter.policy wins over the user's settings (it can only switch things off)
            if (first) { Cfg.SetupDone = false; ApplyAutoStart(); SaveSettings(); }
            else if (!pendingSetup) { Views.EnsureStarted(); }
            if (first) openFirst = true;   // review 1.1.6 D1 #15: the first start shows the window once, so the setup questions are seen   // 1.1.5 views: an upgrade keeps exactly what it had, as "My setup"   // a fresh install gets the 3-question setup on Home; an upgrade never does
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { GameHistory.TidyNow(); });   // old game days go even when nothing writes new ones
        }
        void SaveSettings()
        {
            try
            {
                Views.Remember();   // the view in use keeps what was just changed
                string tmp = settingsPath + ".tmp";
                File.WriteAllLines(tmp, new[] { "autostart=" + (autoStart ? 1 : 0), "widget=" + (widgetOn ? 1 : 0), "serious=" + (Voice.Serious ? 1 : 0), "theme=" + (Program.ForceTheme >= 2 && Program.ForceTheme < Theme.Palettes.Length + 2 ? Theme.Palettes[Program.ForceTheme - 2] : Program.ForceTheme == 1 ? "dark" : Program.ForceTheme == 0 ? "light" : "auto") }.Concat(Cfg.Lines()));
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

        // ---------- phone remote: one session, shown on the "Your phone" page, and the checks it may start
        // (Adeeb, 16 Nov: one place for the phone - the old small window and its Yes/No boxes are gone; the page asks instead)
        FullView remoteRunner;
        static readonly string[] NotStarted = { "2", "Didn't start", "the PC is closing", "" };
        void OpenRemote()
        {
            if (quitting) return;
            var now = Remote.Now;
            if (now == null || now.St == Remote.State.Ended)
            {
                StopStandby();
                var r = Remote.Start(M); Wire(r);
            }
            ShowPhonePage();
        }
        void ShowPhonePage() { if (quitting) return; OpenFull(); if (full != null && !full.IsDisposed) full.ShowPage("phone"); }
        static string SizeText(long size) { return size >= 1048576 ? (size / 1048576.0).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + " MB" : Math.Max(1, size / 1024) + " KB"; }
        T OnUi<T>(Func<T> f, int ms)
        {
            T v = default(T); var done = new ManualResetEventSlim(false);
            try { marshal.BeginInvoke((Action)delegate { try { v = f(); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("remote ui: " + e.Message); } finally { done.Set(); } }); }
            catch (InvalidOperationException) { return v; }
            return done.Wait(ms) ? v : default(T);
        }
        void Wire(Remote r)
        {
                r.Ask = phone => PhoneHub.Ask("allow", phone, L.T("wants to watch this PC and start checks for up to 30 minutes."), r.Words);
                r.Changed = () => { try { marshal.BeginInvoke((Action)delegate { if (r.St != Remote.State.Waiting && r.St != Remote.State.Connecting && !string.IsNullOrEmpty(r.Link)) try { if (Clipboard.ContainsText() && Clipboard.GetText() == r.Link) Clipboard.Clear(); } catch (System.Runtime.InteropServices.ExternalException) { }   /* review 1.1.6: a copied pairing link doesn't outlive its code */ if (r.Known != null) KnownChanged(r); if (r.St == Remote.State.Ended && PhoneHub.Pending != null && (Remote.Now == null || Remote.Now == r)) PhoneHub.Answer(false); PhoneHub.Fire(); drawnKey = ""; if (r.St == Remote.State.Ended && remoteRunner != null && (Remote.Now == null || Remote.Now == r || Remote.Now.St == Remote.State.Ended)) { remoteRunner.Dispose(); remoteRunner = null; } }); } catch (InvalidOperationException) { } };
                r.FeltSlow = () => FeltSlow();
                // a file from the phone: asked on the page first, No by default
                r.AskFile = (phone, name, size) => PhoneHub.Ask("file", phone, L.T("wants to send a file:") + " " + name + " (" + SizeText(size) + "). " + L.T("It will be saved in Downloads\\AdeeboMeter."));
                r.GotText = s => { PhoneHub.Add(new PhoneHub.Got { Text = s ?? "" }); if (PhoneHub.AutoClip) try { marshal.BeginInvoke((Action)delegate { try { Clipboard.SetText(s); } catch (System.Runtime.InteropServices.ExternalException) { } }); } catch (InvalidOperationException) { } };
                r.FileSaved = p => PhoneHub.Add(new PhoneHub.Got { File = p ?? "" });
                r.SendState = msg => { PhoneHub.Xfer = msg ?? ""; PhoneHub.Fire(); };
                // checks run in a view that's never shown, so a game in front isn't interrupted; it also decides what's allowed
                Func<FullView> runner = () => { if (remoteRunner == null || remoteRunner.IsDisposed) { remoteRunner = new FullView(appIcon, M); var h = remoteRunner.Handle; } return remoteRunner; };
                // bug-hunt L-50: asked of the window thread without blocking on it - a busy or closing window answers "nothing" after 10 s
                r.Peek = page => OnUi(() => runner().PeekPage(page), 10000);
                r.CanRun = id => OnUi(() => runner().RemoteAllowed(id), 10000);
                r.Catalog = () => OnUi(() => runner().RemoteCatalog(), 10000);
                r.RunTool = (id, a, run, done) =>
                {
                    if (id == "checkup") { FullView.RemoteCheckup(M, done); return; }
                    try { marshal.BeginInvoke((Action)delegate { runner().RemoteRun(id, a, run, done); }); } catch (InvalidOperationException) { done(id, new List<string[]> { (string[])NotStarted.Clone() }, null); }
                };
            r.AskRemember = phone => PhoneHub.Ask("remember", phone, L.T("asks to be remembered. With Face ID or a fingerprint it can then reconnect to this PC without a new code - from anywhere, while AdeeboMeter runs here. The key is kept for your Windows account only; remove it any time on this page.") + RemotePhones.Replacing());
        }

        // ---------- remembered phones: one waiting session per phone while no code session is open (RemoteKnown.cs)
        readonly Dictionary<string, Remote> standby = new Dictionary<string, Remote>(); DateTime standbyAt = DateTime.MinValue;
        // bug-hunt L-234 (by design): one waiting room per remembered phone (3 at most) - each phone can only find its own room,
        // and a sleeping room costs nothing at the relay (hibernation); the switch on the phone page turns them all off
        void StandbyTick()
        {
            if (quitting || Remote.Relay == "" || (DateTime.UtcNow - standbyAt).TotalSeconds < 15) return;
            standbyAt = DateTime.UtcNow;
            if (!Cfg.PhoneStandby || Policy.NoRemote) { if (standby.Count > 0) StopStandby(); if (Policy.NoRemote && Remote.Now != null && Remote.Now.St != Remote.State.Ended) Remote.Now.End("turned off by your IT team"); return; }   // review 1.1.6: phone_remote = off (or online_lookups = off) ends it all   // bug-hunt ML-111: your switch on the Phone page
            var now = Remote.Now; if (now != null && now.St != Remote.State.Ended && now.Known == null) { StopStandby(); return; }   // a code session is open
            if (now != null && now.Known != null && now.St == Remote.State.Paired) return;                                       // a remembered phone is connected
            List<RemotePhones.P> phones; try { phones = RemotePhones.All(); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("phones: " + e.Message); return; }
            foreach (var id in standby.Keys.Where(k => phones.All(p => p.Id != k)).ToList()) { standby[id].End("forgotten"); standby.Remove(id); }
            if (phones.Count == 0) return;   // round 6 TA6-1: no remembered phone, no waiting room - and no hourly clock check either
            long slot = RemotePhones.Slot;
            foreach (var p in phones)
            {
                Remote s; standby.TryGetValue(p.Id, out s);
                if (s != null && s.St != Remote.State.Ended && s.Slot == slot) continue;
                int next = s != null && s.Slot == slot ? s.Attempt + 1 : 0;      // a room that closed (used, or timed out) can't be reused
                if (s != null && s.St != Remote.State.Ended) s.End("a new room");
                if (next > 11) continue;                                      // wait for the next slot (bug-hunt ML-112: 12 tries a slot, one every 15 s at most - 3 minutes of room changes, not 75 s)
                var r = Remote.Standby(M, p, slot, next); Wire(r); standby[p.Id] = r;
            }
        }
        void StopStandby() { foreach (var s in standby.Values) if (s.St != Remote.State.Ended && s.St != Remote.State.Paired) s.End("a code session started"); standby.Clear(); }
        // a remembered phone got in: it becomes the session and the others stop waiting
        void KnownChanged(Remote r)
        {
            if (r.St != Remote.State.Paired || Remote.Now == r) return;
            // bug-hunt ML-113: one live session at a time - a code session that's open (linking or linked) keeps the place
            var cur = Remote.Now; if (cur != null && cur.Known == null && cur.St != Remote.State.Ended) { r.End("another phone is linked with a code"); return; }
            Remote.Now = r;
            foreach (var s in standby.Values) if (s != r && s.St != Remote.State.Ended) s.End("another phone connected");
            PhoneHub.Fire();   // the link button turns green with the phone's name; Your phone has End
            try { BalloonIs(null, null); tray.ShowBalloonTip(5000, "AdeeboMeter", r.PhoneName + " " + L.T("connected (remembered phone). The link button at the top shows it; Your phone > End now stops it."), ToolTipIcon.Info); } catch (InvalidOperationException) { }
        }
        // a locked file double-clicked while AdeeboMeter already ran: the new copy left its path here and asked us to open
        void OpenHandedFile()
        {
            string f = Path.Combine(Program.DataDir, "open-file.txt"), path = null;
            try { if (File.Exists(f)) { path = File.ReadAllText(f).Trim(); File.Delete(f); } } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            if (path != null && path.EndsWith(".adeebo", StringComparison.OrdinalIgnoreCase) && File.Exists(path) && full != null && !full.IsDisposed) full.OpenLocked(path);
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
            full.Show();
            FullView.ReopenAsked = delegate
            {
                // a new zoom: the window is built again at the new size, on the same page
                if (full == null || full.IsDisposed) return;
                var old = full; full = null; old.Close(); OpenFull();
            }; if (full.WindowState == FormWindowState.Minimized) full.WindowState = FormWindowState.Maximized;
            full.Activate();
        }

        void SetTheme(int choice)
        {
            Program.ForceTheme = choice; SaveSettings();
            bool d = Program.WantDark();
            Theme.Apply(d); drawnKey = ""; if (full != null && !full.IsDisposed) full.ApplyTheme();   // always: Neon and Midnight are both dark but not the same
        }

        void ToggleFull()
        {
            if (full != null && !full.IsDisposed && full.Visible && Form.ActiveForm == full) full.Close();
            else OpenFull();
        }

        DateTime lastTop, lastCard; string lastHead = ""; volatile bool locked;   // locked: the lock screen is up
        void OnScreens(object s, EventArgs e) { drawnKey = ""; lastCover = DateTime.MinValue; }
        void OnPower(object s, Microsoft.Win32.PowerModeChangedEventArgs e) { if (e.Mode == Microsoft.Win32.PowerModes.Resume) { M.Resumed(); drawnKey = ""; lastCover = DateTime.MinValue; dwellStart = DateTime.MinValue; hoverSince = DateTime.MinValue; } }   /* L-8: a rest before sleep doesn't count after it */
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
            // bug-hunt ML-38: at most 3 s for the UI thread - a blocked one doesn't stop the cleanup below
            try { if (marshal.InvokeRequired) { var ar = marshal.BeginInvoke(stop); if (!ar.AsyncWaitHandle.WaitOne(3000)) Program.Log("session end: the window didn't answer - cleaning up without it"); } else stop(); } catch (InvalidOperationException ex) { Program.Log("session end: " + ex.Message); }
            Program.CleanCaptures();
            try { Usage.Save(); } catch (Exception ex) { if (ex is OutOfMemoryException) throw; Program.Log("session end: usage " + ex.Message); }   // Windows ends us after this: keep the last minutes of data used
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
            if (!quitting && full != null && !full.IsDisposed && full.IsHandleCreated && !IsWindowEnabled(full.Handle)) { quitTries = 0; Program.Log("quit: a dialog is still open - quit abandoned"); try { System.IO.File.WriteAllText(System.IO.Path.Combine(Program.DataDir, "quit-busy.txt"), DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture)); } catch (Exception e) { if (e is OutOfMemoryException) throw; } try { full.Activate(); } catch (InvalidOperationException) { } return; }   /* round 6 H6-L12: the copy waiting to take over is told why */
            if (quitWait != null) { quitWait.Stop(); quitWait.Dispose(); quitWait = null; }
            Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnScreens; Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPower; Microsoft.Win32.SystemEvents.SessionSwitch -= OnSession;   // static events: always let go
            if (quitting) return;
            quitting = true; ui.Stop(); GamePing.Me.Stop(); M.Stop(); Usage.Save();
            PhoneHub.Answer(false); var rn = Remote.Now; if (rn != null && rn.St != Remote.State.Ended) rn.End("AdeeboMeter closed"); StopStandby();
            try { SystemEvents.UserPreferenceChanged -= prefHandler; } catch { }
            if (hotkey != null) hotkey.Dispose(); if (slowKey != null) slowKey.Dispose();
            tray.Visible = false; tray.Dispose();
            // a dialog open over the view (Save, Export, Ctrl+K) goes first, so nothing runs on a disposed view when it returns
            if (full != null && !full.IsDisposed) { foreach (var f in Application.OpenForms.Cast<Form>().Where(f => f != full && f.Modal).ToList()) try { f.Close(); } catch (InvalidOperationException) { } var fv = full; fv.Close(); fv.Dispose(); }   // AM-CG-101: closing clears 'full' (FormClosed) - dispose the one we held
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
            var wa = scr.WorkingArea;   /* hunters12 L-8: the widget's own scale, not whatever the full view painted last */
            int w = (int)((Cfg.BigWidget ? 343 : 264) * Draw.Base), h = (int)(300 * Draw.Base);
            var z = new Rectangle(WidgetPos.At(wa, w, h, Draw.Base), new Size(w, h));   /* the corner it's anchored to, opening away from it */
            var p = widget != null ? widget.Placed : Rectangle.Empty;
            return p.Width > 1 && p.Height > 1 ? Rectangle.Union(z, p) : z;
        }

        void BalloonIs(string crash, string tool) { crashNote = crash; balloonTool = tool; }
        // a takeover arrives with one short fade (150 ms, three steps) - nothing else on the widget animates
        string lastEff, sinceLine = ""; bool wasHovered; DateTime fadeAt = DateTime.MinValue, presentAt = DateTime.MinValue;
        bool Fading { get { return (Clock.Mono - fadeAt).TotalMilliseconds < 160; } }
        int FadeStep { get { return Fading ? (int)((Clock.Mono - fadeAt).TotalMilliseconds / 50) : 9; } }
        float FadeK { get { return Fading ? (float)Math.Min(1, 0.25 + (Clock.Mono - fadeAt).TotalMilliseconds / 160 * 0.75) : 1f; } }
        // the data a tile preset shows changed (quotes, scores, the forecast, the minute for the clock and the data counters)
        static string TileVer()
        {
            if (!Corner.IsTile(Corner.Effective)) return "";
            return MarketData.Ver + "." + System.Threading.Interlocked.Read(ref Sports.Ver) + "." + Weather.Fetched.Ticks + "." + DateTime.Now.ToString("HHmm", CultureInfo.InvariantCulture);
        }

        // ---------- move mode (hunters12 widget-input fixes, 24 Nov)
        Point restAt; bool arrivedByMouse, dragGame; DateTime ctrlSince = DateTime.MinValue, trayUnlockAt = DateTime.MinValue, onItAt = DateTime.MinValue;
        void Unlock()
        {
            if (moveOn) { moveTouched = Clock.Mono; return; }
            moveOn = true; ghost = false; moveTouched = Clock.Mono; lastCur = Cursor.Position; onItAt = Clock.Mono;
            widget.SetClickThrough(false); drawnKey = ""; if (ui.Interval != 50) ui.Interval = 50;
        }
        void Lock()
        {
            if (dragging) SaveDrop();   // L-4: the full view opening, a presentation or the lock screen mid-drag keeps the drag
            dragFrom = null; dragging = false; moveByTray = false; hoverSince = DateTime.MinValue; ctrlSince = DateTime.MinValue;
            if (!moveOn) return;
            moveOn = false; widget.SetClickThrough(true); drawnKey = "";
        }
        // back to the default: the bottom-right corner of the main screen - and the gaming spot back to "same as usual" (M-5)
        public void ResetPos() { Lock(); Cfg.WPos = ""; Cfg.WidgetScreen = ""; Cfg.WPosGame = ""; SaveSettings(); drawnKey = ""; lastCover = DateTime.MinValue; }
        public static Action ResetWidget;   // Settings > Widget position > Reset
        // each tick near the widget. Unlocking:
        //  - resting: the mouse came onto it by moving (a finger tap teleports the cursor - M-2) and has then stayed still for
        //    1.5 s (moving across the card to read it restarts the wait - M-1);
        //  - Ctrl: held alone for 0.7 s while on it (a quick Ctrl+click for the app underneath never unlocks - M-3), and never
        //    while a game you added is in front (Ctrl is crouch);
        //  - the tray: unlocked for 15 s to reach it.
        // Locking: the mouse leaves the widget itself (with a short grace), 8 s without moving on it, or another button / the
        // wheel (they were meant for the app underneath - L-6).
        void MoveMode(Point cur, bool inside)
        {
            bool onIt = widget.Placed.Width > 1 && widget.Placed.Contains(cur);
            var nearR = widget.Placed; nearR.Inflate((int)(120 * Draw.Base), (int)(120 * Draw.Base));
            bool near = widget.Placed.Width > 1 && nearR.Contains(cur);
            if (!near) arrivedByMouse = false;
            else if (cur != lastCur && nearR.Contains(lastCur) && Math.Abs(cur.X - lastCur.X) + Math.Abs(cur.Y - lastCur.Y) < 200 * Draw.Base) arrivedByMouse = true;   // seen moving in, not jumping in
            bool moved = cur != lastCur; lastCur = cur;
            if (onIt) onItAt = Clock.Mono;
            if (moveOn)
            {
                if (moved && onIt) moveTouched = Clock.Mono;
                if (onIt) moveByTray = false;
                bool buttons = (Control.MouseButtons & (MouseButtons.Right | MouseButtons.Middle)) != 0;
                bool tray = moveByTray && (Clock.Mono - trayUnlockAt).TotalSeconds < 15;
                bool gone = !tray && (Clock.Mono - onItAt).TotalMilliseconds > 350, idle = (Clock.Mono - moveTouched).TotalSeconds > 8 && !tray;
                if (dragFrom == null && (gone || idle || buttons)) { Lock(); moveSpent = onIt; }   // locked while resting on it: not again until you leave
                else { hovered = true; return; }
            }
            if (!onIt) { hoverSince = DateTime.MinValue; moveSpent = false; ctrlSince = DateTime.MinValue; }
            var ghostR = widget.Placed; ghostR.Inflate((int)(24 * Draw.Base), (int)(24 * Draw.Base));
            bool g = Cfg.WHideNear && !covered && widget.Placed.Width > 1 && ghostR.Contains(cur);   // ML-2: a covered corner still opens by resting on it
            bool ctrl = (Native.GetAsyncKeyState(0x11) & 0x8000) != 0 && Control.MouseButtons == MouseButtons.None && (Native.GetAsyncKeyState(0x10) & 0x8000) == 0 && (Native.GetAsyncKeyState(0x12) & 0x8000) == 0;
            if (onIt && ctrl && !GameFinder.PlayingInFront())
            {
                if (ctrlSince == DateTime.MinValue) ctrlSince = DateTime.UtcNow;
                else if ((DateTime.UtcNow - ctrlSince).TotalMilliseconds >= 700) { Unlock(); hovered = true; return; }
            }
            else ctrlSince = DateTime.MinValue;
            if (g != ghost) { ghost = g; drawnKey = ""; }
            if (ghost) { hovered = false; revealed = false; dwell = 0; dwellStart = DateTime.MinValue; hoverSince = DateTime.MinValue; return; }
            if (onIt && hovered && !moveSpent && arrivedByMouse)
            {
                if (hoverSince == DateTime.MinValue || Math.Abs(cur.X - restAt.X) + Math.Abs(cur.Y - restAt.Y) > 3 * Draw.Base) { hoverSince = DateTime.UtcNow; restAt = cur; }   // moving: the wait starts again
                else if ((DateTime.UtcNow - hoverSince).TotalMilliseconds >= 1500) { Unlock(); hovered = true; }
            }
            else hoverSince = DateTime.MinValue;
        }
        void WidgetDrag()
        {
            if (dragFrom == null || !moveOn) return;
            if (Control.MouseButtons != MouseButtons.Left) { WidgetDrop(); return; }
            if ((Native.GetAsyncKeyState(0x1B) & 0x8000) != 0) { CancelDrag(); return; }   // ML-6: Esc puts it back
            var c = Cursor.Position; int dx = c.X - dragFrom.Value.X, dy = c.Y - dragFrom.Value.Y; moveTouched = Clock.Mono;
            if (!dragging && Math.Abs(dx) + Math.Abs(dy) > 4 * Draw.Base) { dragging = true; if (detailWin.Placed.Width > 1) detailWin.Vanish(); }
            if (dragging) widget.MoveTo(dragOrigin.X + dx, dragOrigin.Y + dy);
        }
        void CancelDrag() { if (dragging) widget.MoveTo(dragOrigin.X, dragOrigin.Y); dragFrom = null; dragging = false; drawnKey = ""; }
        void WidgetDrop()
        {
            if (dragFrom == null) return;
            bool moved = dragging; dragFrom = null; dragging = false; moveTouched = Clock.Mono;
            if (!moved) { Lock(); OpenFull(); return; }   // a click, no drag: the full window
            SaveDrop(true);
        }
        // the drop's place: into the slot the drag started in (a game starting or ending mid-drag doesn't move it - L-3)
        void SaveDrop() { SaveDrop(dragging); }
        void SaveDrop(bool moved)
        {
            if (!moved) return;
            dragging = false;
            var r = widget.Placed; var scr = Screen.FromRectangle(r);
            Cfg.WidgetScreen = scr.Primary ? "" : scr.DeviceName;
            string pos = WidgetPos.From(r, scr.WorkingArea, Draw.Base);
            if (dragGame) Cfg.WPosGame = pos == "" ? "br|0|0" : pos;   // dragged while gaming: the gaming corner
            else Cfg.WPos = pos;
            SaveSettings(); drawnKey = ""; lastCover = DateTime.MinValue;
        }
        // the dashed outline: this widget can move now
        void Outline(Bitmap bmp)
        {
            using (var g = Graphics.FromImage(bmp))
            using (var pen = new Pen(Theme.Blue, Math.Max(1.5f, 1.5f * Draw.S)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float i = pen.Width; var r = new RectangleF(i, i, bmp.Width - 2 * i, bmp.Height - 2 * i);
                using (var p = Theme.Round(r, Math.Min(r.Height / 2f, 14 * Draw.S))) g.DrawPath(pen, p);
            }
        }
        void OnUi()
        {
            StandbyTick();
            try
            {
                uiTick++;
                if ((uiTick & 15) == 0) { Weather.Tick(); Sectors.WatchTick(); try { Sports.ReminderTick(); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("kickoff: " + e.Message); } }   // looks once a minute at most; asks Open-Meteo only when the forecast runs short (Weather.cs)
                var wscr = WidgetScreen();   // bug-hunt L-49: asked once per tick - a screen unplugged between two calls can't null the second
                if (wscr == null) { Lock(); hovered = false; M.HoverOpen = false; hoverKey = ""; if (widget.Placed.Width > 1) { widget.Vanish(); detailWin.Vanish(); drawnKey = ""; } ui.Interval = 1000; return; }   // no screen: nothing to draw on, nothing to log
                // the view hides the widget only when it's open on the widget's own screen (on two screens it may be elsewhere or buried)
                bool viewUp = full != null && !full.IsDisposed && full.Visible && full.WindowState != FormWindowState.Minimized && Screen.FromControl(full).DeviceName == wscr.DeviceName && Form.ActiveForm == full;
                if ((Clock.Mono - lastTop).TotalSeconds > 30) { lastTop = Clock.Mono; widget.ClaimTop(); }   // every 30 s: claim the top layer back - no redraw
                // 24 Nov: the ladder ticks even while the widget is hidden (a game closing while the full view is open still gets its recap)
                { var cs = M.Latest; if (cs != null) Corner.Tick(cs); }
                if ((Clock.Mono - presentAt).TotalMilliseconds >= 450) { presentAt = Clock.Mono; presenting = Native.Presenting() && !(Cfg.GameWidget && GameFinder.PlayingInFront()); }   /* 24 Nov: a game you added, in front (borderless): the widget stays over it. hunters12 ML-7: twice a second by the clock, not every 8th tick */
                if (!widgetOn || viewUp || presenting || locked) { Lock(); hovered = false; revealed = false; dwell = 0; dwellStart = DateTime.MinValue; M.HoverOpen = false; hoverKey = ""; if (widget.Placed.Width > 1) { widget.Vanish(); detailWin.Vanish(); drawnKey = ""; } ui.Interval = !widgetOn || locked ? 2000 : 500; return; }   // hidden or locked: nothing to draw, a slow tick only
                // 24 Nov: the ladder (a game, a match, the weather, the day plan) and the feeds for what may show
                { Corner.Feeds(); if (Corner.Effective != lastEff) { if (lastEff != null) fadeAt = Clock.Mono; lastEff = Corner.Effective; drawnKey = ""; } }
                // Poll fast only when the mouse is near the corner; otherwise 4 times a second is plenty.
                var near = Zone(); near.Inflate((int)(120 * Draw.Base), (int)(120 * Draw.Base));
                int want = near.Contains(Cursor.Position) || hovered || dwell > 0 ? 50 : Cfg.Preset == "penny" ? 1000 : 500;   // the mouse far away: twice a second, once on Penny
                if (moveOn || Fading) want = 50;
                if (ui.Interval != want) ui.Interval = want;
                if (dragging && Control.MouseButtons != MouseButtons.Left) WidgetDrop();   // ML-3: the button came up somewhere else (Alt+Tab, UAC) - the drag ends here
                if (dragging) { if ((Native.GetAsyncKeyState(0x1B) & 0x8000) != 0) CancelDrag(); return; }   // the window follows the mouse by itself; drawn again on the drop
                if ((Clock.Mono - lastCover).TotalMilliseconds >= (want == 50 ? 800 : 2000) && !hovered)   // AM-CG-214; bug-hunt ML-146: the window walk every 0.8 s near the corner, not 0.4   // AM-CG-192: re-judged when folded again, not by the open card's size
                {
                    lastCover = Clock.Mono;
                    bool c = WindowOver(Zone());
                    if (c != covered) { covered = c; revealed = false; dwellStart = DateTime.MinValue; drawnKey = ""; }
                }
                var cur = Cursor.Position; var zone = widget.Placed;
                if (hovered)
                {
                    if (foldedAt.Width > 1) zone = Rectangle.Union(zone, foldedAt);
                    if (detailWin.Placed.Width > 1) zone = Rectangle.Union(zone, detailWin.Placed);   // and the corridor to it (the union spans the gap)
                }
                bool inside = zone.Contains(cur);
                if (inside) insideAt = Clock.Mono; else if (hovered && (Clock.Mono - insideAt).TotalMilliseconds < 350) inside = true;   // grace: no fold on a passing wobble
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
                MoveMode(cur, inside);
                // the hint line on the card, by the settings: unlocked - what a drag and a click do; resting on it - that it will unlock
                if (hovered && !wasHovered) sinceLine = Corner.Since(M.Latest ?? new Snap()); else if (!hovered && wasHovered) { Corner.Looked(); sinceLine = ""; }
                wasHovered = hovered;
                // hunters12: one blue line, in this order - unlocked: how to move; what changed since you looked (it stays for the whole
                // hover, the move hint no longer replaces it - ML-4); why this card is showing (a takeover, the day plan, a hold - ML-3);
                // then, resting still, that it's about to unlock. Move hints off hides only the move lines (ML-5).
                string why = hovered ? Corner.WhyLine() : "";
                string wh = moveOn ? (!Cfg.WHint ? "" : Cfg.WHideNear ? L.T("Drag to move · click opens AdeeboMeter · fades again when you leave") : L.T("Drag to move · click opens AdeeboMeter"))
                    : !hovered ? "" : sinceLine != "" ? sinceLine : why != "" && !(hoverSince != DateTime.MinValue && (DateTime.UtcNow - hoverSince).TotalMilliseconds >= 400) ? why
                    : Cfg.WHint && hoverSince != DateTime.MinValue && (DateTime.UtcNow - hoverSince).TotalMilliseconds >= 400 ? L.T("Keep resting to move it, or hold Ctrl") : why;
                if (wh != Draw.WidgetHint) { Draw.WidgetHint = wh; drawnKey = ""; }
                M.HoverOpen = hovered;
                string key = "";
                if (hovered && !moveOn) foreach (var kv in hits) { var r = kv.Value; if (new RectangleF(widget.Placed.X + r.X, widget.Placed.Y + r.Y, r.Width, r.Height).Contains(cur)) { key = kv.Key; break; } }
                hoverKey = key;
                var rw = Rewind; if (rw != null && Clock.Mono > RewindUntil) { Rewind = null; rw = null; }
                Snap s = rw ?? M.Latest; bool compact = !hovered && (covered || Draw.SpeedStyle || (s.Level == 0 && !s.Call));   // a speed widget stays a strip until you hover it
                string state = compact + "|" + hovered + "|" + key + "|" + Corner.Effective + "|" + FadeStep + "|" + TileVer() + "|" + covered + "|" + ((int)(dwell * 20)) + "|" + Voice.Serious + "|" + Cfg.BigWidget + "|" + Mood.Name + "|" + Cfg.WidgetStyle + "|" + Cfg.WDir + "|" + moveOn + "|" + ghost + "|" + Cfg.WPos + "|" + Cfg.WidgetScreen + "|" + Cfg.WUnits + "|" + Cfg.WGraph + "|" + Theme.Palette + "|" + Theme.Dark + "|" + (Cfg.Buddy ? Draw.BuddyMood(s) : "") + "|" + (rw != null ? rw.Chip : "") + "|" + Moments.FaceFor(s);   /* a moment's face shows and goes */
                bool fresh = M.Version != drawnVersion;
                // The folded chip redraws only when its words or colour change; the open card once a second while you look at it,
                // and unhovered only when its level or headline changes (or every 10 s for the numbers)
                bool cardDue = !compact && (hovered || s.Level != lastLevel || s.Headline != lastHead || (Clock.Mono - lastCard).TotalSeconds >= 10);
                // the folded strip: speeds at most every 2 s, everything else at most every 5 s, a new level at once (Adeeb, 21 Nov)
                bool chipDue = compact && (s.Level != lastLevel || (Draw.ChipSpeedKey(s) != lastSpeed && (Clock.Mono - speedAt).TotalSeconds >= 2) || (Draw.ChipOtherKey(s) != lastOther && (Clock.Mono - otherAt).TotalSeconds >= 5));
                if (state != drawnKey || (fresh && (cardDue || chipDue)))
                {
                    Draw.CardHovered = hovered; Render(s, compact); if (compact) foldedAt = widget.Placed; drawnKey = state; drawnVersion = M.Version; if (!compact) { lastCard = Clock.Mono; lastHead = s.Headline; }
                }
            }
            catch (Exception e) { Program.Log("ui: " + e.Message); }
        }

        void Render(Snap s, bool compact)
        {
            float keep = Draw.S; Draw.Use(Draw.Base);   // the corner widget is drawn at the screen's own scale - the window's zoom isn't for it
            try { RenderAt(s, compact); } finally { Draw.Use(keep); }
        }
        void RenderAt(Snap s, bool compact)
        {
            var ws0 = WidgetScreen(); if (ws0 == null) return;   // L-49
            var wa = ws0.WorkingArea;
            using (var bmp = compact ? Draw.Chip(s, dwell) : Draw.Card(s, hits))
            {
                if (compact) hits.Clear();
                if (moveOn) Outline(bmp);
                var at = WidgetPos.At(wa, bmp.Width, bmp.Height, Draw.S);   // AM-CG-201: inside the work area
                byte a0 = ghost ? (byte)0 : hovered ? (byte)255 : FADE; widget.Present(bmp, (byte)(a0 * FadeK), at.X, at.Y);
            }
            lastChip = Draw.ChipKey(s); lastLevel = s.Level;
            string sk = Draw.ChipSpeedKey(s), ok = Draw.ChipOtherKey(s);
            if (sk != lastSpeed) { lastSpeed = sk; speedAt = Clock.Mono; }
            if (ok != lastOther) { lastOther = ok; otherAt = Clock.Mono; }
            Detail d = null;
            if (!moveOn && !compact && hovered && hoverKey != "") s.Details.TryGetValue(hoverKey, out d);
            if (d != null)
                using (var db = Draw.DetailCard(d))
                {
                    // the side facing the screen: left of a widget on the right, right of one on the left; level with its outer edge
                    bool right = WidgetPos.Right, bottom = WidgetPos.Bottom; var p = widget.Placed; int gap = (int)(8 * Draw.S);
                    int x = right ? p.X - db.Width - gap : p.Right + gap, y = bottom ? p.Bottom - db.Height : p.Y;
                    detailWin.Present(db, 255, Math.Max(wa.Left, Math.Min(x, wa.Right - db.Width)), Math.Max(wa.Top, Math.Min(y, wa.Bottom - db.Height)));   /* on this screen, never past its edges */
                }
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
        // "It feels slow right now": freeze the last 2 minutes, judge them, say who it was in a note (click = the whole picture)
        void FeltSlow()
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    var c = PcRecord.FeltSlow();
                    marshal.BeginInvoke((Action)delegate
                    {
                        if (quitting) return;
                        // the view already open, or Windows won't show a note (notifications off, Focus assist): the answer opens right away
                        bool open = full != null && !full.IsDisposed && full.Visible && full.WindowState != FormWindowState.Minimized;
                        if (open || !Native.CanNotify()) { OpenFull(); if (full != null && !full.IsDisposed) full.ShowTool("feltslow", true); return; }
                        BalloonIs(null, "feltslow"); tray.ShowBalloonTip(10000, c.Who == "pc" ? "It's this PC" : c.Who == "internet" ? "It's the internet" : c.Who == "both" ? "This PC and the internet" : c.Who == "app" ? "Not the PC, not the internet" : c.Who == "notpc" ? "Not this PC" : "AdeeboMeter", Draw.Trunc(c.Text, 230), ToolTipIcon.Info);
                    });
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("felt slow: " + e.Message); }
            });
        }
    }

    // Where the corner widget sits (24 Nov): one corner plus a distance in from it, in 96-DPI pixels, so it keeps its place
    // at any scaling and a bigger card or chip grows away from that corner. "" is the default, bottom right.
    static class WidgetPos
    {
        static readonly string[] Corners = { "br", "bl", "tr", "tl" };
        public static string Clean(string v) { return Clean(v, false); }
        public static string Clean(string v, bool keepDefault)   // keepDefault: the gaming corner can be bottom right on purpose ("" there = same as usual)
        {
            var p = (v ?? "").Split('|'); int dx, dy;
            if (p.Length != 3 || Array.IndexOf(Corners, p[0]) < 0 || !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out dx) || !int.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out dy)) return "";
            dx = Math.Min(dx, 20000); dy = Math.Min(dy, 20000);
            return p[0] == "br" && dx == 0 && dy == 0 && !keepDefault ? "" : p[0] + "|" + dx + "|" + dy;
        }
        // the saved place - the default when its screen is gone
        static void Get(out string c, out int dx, out int dy)
        {
            c = "br"; dx = dy = 0; var v = AdeeboMeter.Corner.UseGamePos ? Cfg.WPosGame : Cfg.WPos; if (v == "") return;
            var w = Cfg.WidgetScreen; if (w != "" && !Screen.AllScreens.Any(x => x.DeviceName == w)) return;
            var p = v.Split('|'); if (p.Length != 3) return;
            c = p[0]; int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out dx); int.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out dy);
        }
        public static int CornerIndex { get { string c; int a, b; Get(out c, out a, out b); return Array.IndexOf(Corners, c); } }
        public static bool Right { get { string c; int a, b; Get(out c, out a, out b); return c[1] == 'r'; } }
        public static bool Bottom { get { string c; int a, b; Get(out c, out a, out b); return c[0] == 'b'; } }
        public static Point At(Rectangle wa, int w, int h, float s)
        {
            string c; int dx, dy; Get(out c, out dx, out dy);
            int m = (int)(10 * s), ox = (int)(dx * s), oy = (int)(dy * s);
            int x = c[1] == 'r' ? wa.Right - m - ox - w : wa.Left + m + ox, y = c[0] == 'b' ? wa.Bottom - m - oy - h : wa.Top + m + oy;
            return new Point(Math.Max(wa.Left, Math.Min(x, wa.Right - w)), Math.Max(wa.Top, Math.Min(y, wa.Bottom - h)));
        }
        // after a drag: the nearest corner, and within 24 px of an edge it snaps to it
        public static string From(Rectangle r, Rectangle wa, float s)
        {
            int m = (int)(10 * s), cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            bool right = cx >= wa.X + wa.Width / 2, bottom = cy >= wa.Y + wa.Height / 2;
            int dx = (int)Math.Round((right ? wa.Right - m - r.Right : r.Left - wa.Left - m) / s), dy = (int)Math.Round((bottom ? wa.Bottom - m - r.Bottom : r.Top - wa.Top - m) / s);
            if (dx < 24) dx = 0; if (dy < 24) dy = 0;
            return Clean((bottom ? "b" : "t") + (right ? "r" : "l") + "|" + dx + "|" + dy);
        }
        // hunters12 L-1: picking the corner it's already in keeps the distance; another corner puts it right in that corner
        public static string Pick(int corner)
        {
            var p = (Cfg.WPos == "" ? "br|0|0" : Cfg.WPos).Split('|');
            if (corner >= 0 && corner < Corners.Length && p.Length == 3 && p[0] == Corners[corner]) return Clean(Cfg.WPos);
            return corner <= 0 ? "" : Corners[corner] + "|0|0";
        }
        // the normal position's corner, whatever a game is doing (Settings shows and edits the same one - M-6)
        public static int NormalCornerIndex { get { var v = Cfg.WPos; if (v == "") return 0; var p = v.Split('|'); return Math.Max(0, Array.IndexOf(Corners, p[0])); } }
        public static string PickGame(int i) { return i <= 0 ? "" : Corners[i - 1] + "|0|0"; }   // 0 = same as usual, then the four corners
        public static int GameIndex { get { var v = Cfg.WPosGame; if (v == "") return 0; var p = v.Split('|'); int i = Array.IndexOf(Corners, p[0]); return i < 0 ? 0 : i + 1; } }
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
