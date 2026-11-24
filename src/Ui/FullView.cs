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
    sealed partial class FullView : Form
    {
        readonly Monitor M;
        readonly DB header, pathPanel, side, footer, destCanvas, healthCanvas;
        readonly LiveCapture live = new LiveCapture(); LiveCapture.View liveView; readonly DB liveCanvas; readonly PillButton liveBtn; readonly Panel livePage; int liveTick;
        readonly DB changesCanvas; readonly Panel changesPage;
        readonly ConnList list; readonly Panel connPage, destPage, healthPage; readonly RoundBox search, destBox; readonly Label connStatus;
        readonly FlowLayoutPanel tabs, chips; readonly PillButton idle, checkBtn, healthBtn;
        readonly System.Windows.Forms.Timer timer;
        Snap snap; int lastVersion = -1;
        System.Threading.Timer watchdog; volatile bool Visible0; Rectangle lastBounds; DateTime lastPong = DateTime.UtcNow, lastWatch = DateTime.UtcNow; volatile bool pinged, frozenLogged; string frozeDoing = "";
        Panel leftPane; string tab = "overview", lastPage = "overview", curSec;
        readonly FlowLayoutPanel subTabs; readonly Dictionary<string, List<Control>> subBtns = new Dictionary<string, List<Control>>(); readonly Dictionary<string, string> lastIn = new Dictionary<string, string>();
        readonly string dumpcap, wireshark; readonly bool npcap;
        bool refreshing; DateTime lastNames = DateTime.MinValue, lastConns = DateTime.MinValue;
        DestResult dest; volatile bool destRunning;
        List<string[]> health; bool healthRunning; DateTime healthAt;
        double selfCpu; long selfMem; TimeSpan lastCpuT; DateTime lastCpuAt = DateTime.UtcNow;
        // Your connection
        readonly Panel linkPage; readonly DB linkCanvas; LinkSnap linkSnap; bool linkBusy; DateTime lastLink = DateTime.MinValue;
        LiveCapture.SwitchInfo switchInfo; volatile bool switchBusy; string switchErr = ""; DateTime switchStart;
        // What's running
        readonly Panel procPage; readonly DB procList, procInfo; readonly RoundBox procSearch;
        List<AppStat> procs = new List<AppStat>(); string procSel; string[] procFacts; string procPathSel = ""; DateTime lastProcs = DateTime.MinValue;
        readonly Dictionary<string, string> procWhere = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); bool whereBusy;
        // Settings
        readonly Panel settingsPage, aboutPage; readonly DB settingsCanvas, aboutCanvas; readonly SlideSwitch detailsBtn; readonly PillButton settingsBtn, aboutBtn, backBtn, onlineChip; readonly ViewButton viewBtn; readonly IconButton gearBtn, dotsBtn; readonly SearchPill headSearch; readonly ThemeSwitch themeSw; Panel tipBar;
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
            Draw.Use(Draw.ViewScale);   // the window's zoom (the corner widget keeps the screen's own scale)
            M = m; snap = m.Latest; Knowledge.Hold = true;
            // freeze watchdog: the window answers a ping every second; if it doesn't for 2 s, the log says what it was doing
            watchdog = new System.Threading.Timer(delegate
            {
                if (!IsHandleCreated || IsDisposed || Disposing) return;
                // 21 Nov (Adeeb: "messages keep popping" holding minimize; the card stuck after sleep) - two things look like a
                // freeze and aren't: the PC waking up (this timer itself ran late: the clock jumped by the sleep), and a mouse
                // button held on the window's frame (Windows keeps the window in its own loop until you let go)
                var now = DateTime.UtcNow; bool woke = (now - lastWatch).TotalSeconds > 5; lastWatch = now;
                bool held = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x02) & 0x8000) != 0;
                if (woke || held) { lastPong = now; FreezeCard.Hide(); if (woke) BuddyReact.Set("asleep", 20); return; }   /* just woke up: a sleepy face for a moment */
                var since = now - lastPong;
                if (since.TotalSeconds >= 2 && !frozenLogged) { frozenLogged = true; frozeDoing = UiActivity.Doing; Program.Log("window froze (" + since.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s so far) while " + frozeDoing); }
                if (since.TotalSeconds >= 2 && Visible0) FreezeCard.Show(lastBounds, (int)since.TotalSeconds, UiActivity.Doing);   // the card on its own thread: you see it even while this window is stuck
                if (!pinged) { pinged = true; try { BeginInvoke((Action)delegate { pinged = false; if (frozenLogged) { frozenLogged = false; Program.Log("window answered again after " + (DateTime.UtcNow - lastPong).TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s (" + frozeDoing + ")"); } lastPong = DateTime.UtcNow; FreezeCard.Hide(); Visible0 = Visible && WindowState != FormWindowState.Minimized; if (Visible0) lastBounds = Bounds; }); } catch (Exception) { pinged = false; } }   /* the window may be closing: an exception here would end the whole process (a timer thread) */
            }, null, 1000, 1000);
            Text = AdeeboMeter.L.T("AdeeboMeter - ") + Program.Tagline; Icon = icon; BackColor = Theme.Bg; Font = Draw.fBody;
            // Start at the size of the screen it opens on, so it fits even if maximizing is refused.
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            StartPosition = FormStartPosition.Manual; Bounds = wa; MinimumSize = new Size(Math.Min(Sc(980), wa.Width), Math.Min(Sc(640), wa.Height));
            WindowState = FormWindowState.Maximized; KeyPreview = true;
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            dumpcap = Path.Combine(pf, "Wireshark", "dumpcap.exe"); wireshark = Path.Combine(pf, "Wireshark", "Wireshark.exe");
            npcap = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Npcap", "wpcap.dll")) && File.Exists(dumpcap);

            header = new DB { Name = "header", Dock = DockStyle.Top, Height = Sc(60), BackColor = Theme.Card };
            header.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintHeader(e.Graphics, header.ClientRectangle); }; Wire(header);
            settingsBtn = new PillButton("Settings", PillButton.K.Chip);
            settingsBtn.Click += delegate { tab = tab == "settings" ? lastPage : "settings"; ShowTab(); };
            aboutBtn = new PillButton("About", PillButton.K.Chip);
            aboutBtn.Click += delegate { tab = tab == "about" ? lastPage : "about"; ShowTab(); };
            backBtn = new PillButton("Back to corner", PillButton.K.Primary);
            backBtn.Click += delegate { Close(); };
            viewBtn = new ViewButton(); SyncViewBtn();
            viewBtn.Click += delegate { ViewMenu(); };
            viewBtn.MouseEnter += delegate { SetTipKey("ov.VIEW"); }; viewBtn.MouseLeave += delegate { SetTipKey(null); };
            detailsBtn = new SlideSwitch("Details", Cfg.Details);   // a sliding on/off switch: the engineer view
            detailsBtn.Click += delegate { SetDetails(detailsBtn.On); };
            detailsBtn.MouseEnter += delegate { SetTipKey("ov.DETAILS"); }; detailsBtn.MouseLeave += delegate { SetTipKey(null); };
            onlineChip = new PillButton("Online lookups on", PillButton.K.Chip) { Visible = false };
            onlineChip.Click += delegate { Online.Stop(); SyncOnlineChip(); };
            onlineChip.MouseEnter += delegate { SetTipKey("ov.ONLINE"); }; onlineChip.MouseLeave += delegate { SetTipKey(null); };
            themeSw = new ThemeSwitch();
            themeSw.MouseEnter += delegate { SetTipKey("theme.toggle"); }; themeSw.MouseLeave += delegate { SetTipKey(null); };
            // the top line (Adeeb, 14 Nov): logo · your view · search · theme · buddy · Settings · ⋯ · Back to corner. Details,
            // the layout, points and About live in the ⋯ menu; the verdict moved down beside the path it explains.
            gearBtn = new IconButton("settings", "Settings"); gearBtn.Click += delegate { tab = tab == "settings" ? lastPage : "settings"; ShowTab(); };
            dotsBtn = new IconButton("more", "More: Details, layout, points, About"); dotsBtn.Click += delegate { TopMenu(); };
            headSearch = new SearchPill(); headSearch.Click += delegate { OpenPalette(); };
            header.Controls.Add(themeSw); header.Controls.Add(viewBtn); header.Controls.Add(headSearch); header.Controls.Add(gearBtn); header.Controls.Add(dotsBtn); header.Controls.Add(backBtn);
            MakeLangButton();   // the language button beside VIEW, and Restart when a switch needs one (21 Nov)
            EventHandler place = delegate { PlaceTop(); };
            header.Resize += place; viewBtn.Resize += place; backBtn.Resize += place;
            pathPanel = new DB { Name = "pathPanel", Dock = DockStyle.Top, Height = Sc(128), BackColor = Theme.Card };
            pathPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                Prep(e.Graphics);
                if (!Cfg.Details) { PaintPlainPath(e.Graphics, pathPanel.ClientRectangle); return; }   // Details off: three plain chips (PLAN 11.4)
                Begin(pathPanel);
                float vend = PaintVerdict(e.Graphics, pathPanel, Sc(20), Sc(18), pathPanel.Width * 0.62f);
                if (!string.IsNullOrEmpty(snap.Hint) && vend < pathPanel.Width * 0.62f + 1)
                    using (var hintBrush = new SolidBrush(Theme.LvText(1))) using (var sf = new StringFormat { Alignment = StringAlignment.Far, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                        e.Graphics.DrawString(AdeeboMeter.L.T("Why: ") + snap.Hint, Draw.fBodyB, hintBrush, new RectangleF(pathPanel.Width * 0.62f + Sc(16), Sc(9), pathPanel.Width * 0.38f - Sc(36) - (onlineChip.Visible ? onlineChip.Width + Sc(12) : 0), Sc(20)), sf);
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
            pathPanel.Controls.Add(onlineChip);   // online lookups: at the end of the path line, not crowding the top
            EventHandler placeChip = delegate { onlineChip.Location = new Point(pathPanel.Width - onlineChip.Width - Sc(20), Cfg.Details ? Sc(6) : (pathPanel.Height - onlineChip.Height) / 2); };
            pathPanel.Resize += placeChip; onlineChip.Resize += placeChip; onlineChip.VisibleChanged += placeChip;
            footer = new DB { Name = "footer", Dock = DockStyle.Bottom, Height = Sc(30), BackColor = Theme.Card };
            footer.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintFooter(e.Graphics, footer.ClientRectangle); };

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(Sc(14)), BackColor = Theme.Bg, Tag = "bg" };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Sc(390)));
            bodyGrid = body; body.Resize += delegate { SizeSide(); };
            Resize += delegate { if (WindowState == FormWindowState.Minimized) learnSeen = ""; }; VisibleChanged += delegate { if (!Visible) learnSeen = ""; };
            // the logo's needle sweeps each time the window opens (Motion board, 16 Nov)
            VisibleChanged += delegate { if (Visible && IsHandleCreated) BeginInvoke((Action)delegate { LogoAnim(0); }); };

            var left = leftPane = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Margin = new Padding(0, 0, Sc(14), 0) };
            tabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(48), BackColor = Theme.Card, Padding = new Padding(Sc(10), Sc(4), 0, 0), WrapContents = false };
            // six sections, each with its own pages underneath (room to grow without the tab row falling off the edge)
            // the bar (five sections + More, the user's order) is built by BuildNavBar (FullView.Nav) once the pages exist
            subTabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(48), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(5), 0, 0), WrapContents = false, Visible = false };
            var tabLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Track, Tag = "line" };
            // Small screens / high scaling: switch to short tab names instead of letting tabs fall off the edge.
            subTabs.Resize += delegate { FitTabs(); };
            ApplyMode();

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
            list.SniffRequested += Sniff; list.JourneyRequested += OpenJourney; list.Changed += UpdateConnStatus; list.HoverKey += SetTipKey;
            connStatus = new OneLineLabel { Dock = DockStyle.Bottom, Height = Sc(32), ForeColor = Theme.Muted, Padding = new Padding(Sc(14), Sc(8), 0, 0), BackColor = Theme.Card, Font = Draw.fSmall, AutoEllipsis = true };
            connStatus.Click += delegate { if (Program.LastCapture != "") KeepCapture(); };
            connPage.Controls.Add(list); connPage.Controls.Add(connStatus); connPage.Controls.Add(chips);
            StyleChips();

            // Destination page
            destPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var destBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(0, Sc(64)), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), Sc(8), Sc(6)), WrapContents = true };   // the chips wrap onto a second line instead of running off the side
            destBox = new RoundBox(Sc(440), Draw.fInput, "host, IP or URL - e.g. github.com:443");
            destBox.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; StartDest(); } };
            checkBtn = new PillButton("Check", PillButton.K.Primary); checkBtn.Click += delegate { StartDest(); };
            pinBtn = new PillButton("Pin", PillButton.K.Chip) { Margin = new Padding(Sc(8), Sc(3), 0, 0) };
            pinBtn.Click += delegate { TogglePin(); };
            destBox.Box.TextChanged += delegate { SyncPinBtn(); };
            destBar.Controls.Add(destBox); destBar.Controls.Add(checkBtn); destBar.Controls.Add(pinBtn);
            foreach (var t in new[] { "mtu|MTU per hop", "mtr|MTR", "ret|The way back", "owners|Who carries it" })
            {
                var p = t.Split('|'); var b = new PillButton(p[1], PillButton.K.Chip) { Tag = p[0], Margin = new Padding(Sc(8), Sc(3), 0, 0) };
                b.Click += delegate(object s, EventArgs e) { string id = (string)((Control)s).Tag; tab = "toolbox"; ShowTab(); SelectTool(id); };
                destBar.Controls.Add(b);
            }
            pinBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(42), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(4), 0, 0), WrapContents = false };
            destCanvas = new DB { Name = "destCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            destCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintDest(e.Graphics); };
            Wire(destCanvas);
            destPage.Controls.Add(destCanvas); destPage.Controls.Add(pinBar); destPage.Controls.Add(destBar);
            BuildPinBar();

            // Health page
            healthPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var hb = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            healthBtn = new PillButton("Run health check", PillButton.K.Primary); healthBtn.Click += delegate { StartHealth(); };
            hb.Controls.Add(healthBtn);
            foreach (var t in new[] { new[] { "precall", "Pre-call check" }, new[] { "dns", "DNS speed test" }, new[] { "vpn", "VPN map" }, new[] { "see", "What can AdeeboMeter see here?" } })
            {
                var tb = new PillButton(t[1], PillButton.K.Chip) { Tag = t[0], Margin = new Padding(Sc(8), Sc(3), 0, 0) };
                tb.Click += delegate(object s, EventArgs e) { RunTool((string)((Control)s).Tag, ((Control)s).Text); };
                hb.Controls.Add(tb);
            }
            // the PC checks (they used to sit in the Toolbox): one click each, the result shows here - mid-size tiles with icons (22 Nov)
            deeperCanvas = new DB { Name = "deeperCanvas", Dock = DockStyle.Top, Height = Sc(180), BackColor = Theme.Card };
            deeperCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintDeeper(e.Graphics); }; Wire(deeperCanvas);
            deeperCanvas.MouseMove += delegate(object s, MouseEventArgs e) { DeeperHover(e.Location); }; deeperCanvas.MouseLeave += delegate { DeeperHover(new Point(-1, -1)); };
            deeperCanvas.Resize += delegate { int h = DeeperHeight(); if (deeperCanvas.Height != h) deeperCanvas.Height = h; deeperCanvas.Invalidate(); };
            var hb2 = deeperCanvas;
            healthCanvas = new DB { Name = "healthCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            healthCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintHealth(e.Graphics); }; Wire(healthCanvas);
            healthPage.Controls.Add(healthCanvas); healthPage.Controls.Add(hb2); healthPage.Controls.Add(hb);

            // Live capture page
            livePage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var lb = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            liveBtn = new PillButton("Start live capture", PillButton.K.Primary); liveBtn.Click += delegate { ToggleLive(); };
            lb.Controls.Add(liveBtn);
            Func<string> liveText = () => Policy.NoCapture ? L.T("Live capture is turned off by your IT team (AdeeboMeter.policy).") : LiveCapture.Available ? "Wireshark's tshark decodes your PC's own traffic. Off until you press Start; stops when you leave this tab, close the view, or after " + Cfg.LiveMin + " min (Settings)." : "Needs Wireshark (tshark) and Npcap - not found on this PC.";
            var liveLbl = new Label { Text = liveText(), AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fSmall, Margin = new Padding(Sc(10), Sc(10), 0, 0) };
            liveLbl.VisibleChanged += delegate { if (liveLbl.Visible) liveLbl.Text = liveText(); };   // the minutes follow Settings
            lb.Controls.Add(liveLbl);
            liveBtn.Enabled = LiveCapture.Available && !Policy.NoCapture;
            liveCanvas = new DB { Name = "liveCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            liveCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintLive(e.Graphics); }; Wire(liveCanvas);
            livePage.Controls.Add(liveCanvas); livePage.Controls.Add(lb);

            // What changed page
            changesPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            changesCanvas = new DB { Name = "changesCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            changesCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintChanges(e.Graphics); }; Wire(changesCanvas);
            changesPage.Controls.Add(changesCanvas);

            // Your connection page
            linkPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            linkCanvas = new DB { Name = "linkCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            linkCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintLink(e.Graphics); }; Wire(linkCanvas);
            linkPage.Controls.Add(linkCanvas);
            BuildWalkBar();

            // What's running page
            procPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var pbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            procSearch = new RoundBox(Sc(300), Draw.fBody, "Find a process or app");
            procSearch.Box.TextChanged += delegate { procList.AutoScrollPosition = Point.Empty; procList.Invalidate(); };
            pbar.Controls.Add(procSearch);
            pbar.Controls.Add(new Label { Text = Voice.P("Click anything for the plain-English story. I only explain - I never close anything.", "Click a process for its explanation. AdeeboMeter never closes processes."), AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fSmall, Margin = new Padding(Sc(4), Sc(10), 0, 0) });
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Card };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            procList = new DB { Name = "procList", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            procList.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintProcList(e.Graphics); }; Wire(procList);
            // the pinned column header: a scroll moves the picture, so paint it all again or the header smears. Both events on purpose
            // (bug-hunt L-260): a wheel doesn't always raise Scroll here; two Invalidates before a paint merge into one paint
            procList.Scroll += delegate { procList.Invalidate(); }; procList.MouseWheel += delegate { procList.Invalidate(); };
            procInfo = new DB { Name = "procInfo", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            procInfo.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintProcInfo(e.Graphics); }; Wire(procInfo);
            split.Controls.Add(procList, 0, 0); split.Controls.Add(procInfo, 1, 0);
            procPage.Controls.Add(split); procPage.Controls.Add(pbar);

            // Settings page
            settingsPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            settingsCanvas = new DB { Name = "settingsCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            settingsCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintSettings(e.Graphics); }; Wire(settingsCanvas);
            settingsPage.Controls.Add(settingsCanvas);
            // search across every setting (Settings > the box at the top)
            var setTop = new Panel { Dock = DockStyle.Top, Height = Sc(56), BackColor = Theme.Card, Padding = new Padding(Sc(252), Sc(12), Sc(24), Sc(8)) };
            settingsSearch = new RoundBox(Sc(420), Draw.fBody, "Search settings - e.g. ping, history, widget") { Dock = DockStyle.Left };
            settingsSearch.Box.AccessibleName = "Search settings";
            settingsSearch.Box.TextChanged += delegate { settingsCanvas.AutoScrollPosition = Point.Empty; settingsCanvas.Invalidate(); };
            setTop.Controls.Add(settingsSearch); settingsPage.Controls.Add(setTop);

            // About page
            aboutPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            aboutCanvas = new DB { Name = "aboutCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            aboutCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintAbout(e.Graphics); }; Wire(aboutCanvas);
            aboutPage.Controls.Add(aboutCanvas);

            left.Controls.Add(BuildOverviewPage()); left.Controls.Add(BuildToolboxPage()); left.Controls.Add(BuildGuidePage()); left.Controls.Add(BuildReplayPage());
            left.Controls.Add(BuildTravelPage()); left.Controls.Add(BuildInsidePage()); left.Controls.Add(BuildLearnPage()); left.Controls.Add(BuildSecurityPage()); left.Controls.Add(BuildGamerPage()); left.Controls.Add(BuildStreamPage()); left.Controls.Add(BuildSportsPage()); left.Controls.Add(BuildTradePage()); left.Controls.Add(BuildOutlookPage()); left.Controls.Add(BuildShellPage()); left.Controls.Add(BuildLabPage());
            left.Controls.Add(connPage); left.Controls.Add(linkPage); left.Controls.Add(procPage); left.Controls.Add(settingsPage); left.Controls.Add(aboutPage); left.Controls.Add(BuildPointsPage()); left.Controls.Add(BuildPhonePage()); left.Controls.Add(BuildMinePage()); left.Controls.Add(destPage); left.Controls.Add(healthPage); left.Controls.Add(changesPage); left.Controls.Add(livePage); left.Controls.Add(tabLine); left.Controls.Add(subTabs); left.Controls.Add(tabs);
            side = new DB { Name = "side", Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0), Tag = "bg" };
            side.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintSide(e.Graphics, side.ClientRectangle); }; Wire(side);
            body.Controls.Add(left, 0, 0); body.Controls.Add(side, 1, 0);
            Controls.Add(body); Controls.Add(footer); Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Line, Tag = "line" }); Controls.Add(pathPanel); Controls.Add(header); Controls.Add(BuildSideNav());   // the sidebar docks last: full height, left of everything
            // docking adds the fill first, which would make Tab visit the page before its tabs: put reading order back
            header.TabIndex = 0; pathPanel.TabIndex = 1; body.TabIndex = 2; footer.TabIndex = 3; left.TabIndex = 0; side.TabIndex = 1;
            int ti = 0; foreach (var c in left.Controls.Cast<Control>().Where(c => c.Dock == DockStyle.Top).OrderBy(c => c.Top)) c.TabIndex = ti++;
            foreach (var c in left.Controls.Cast<Control>().Where(c => c.Dock != DockStyle.Top)) c.TabIndex = 100 + ti++;
            pathPanel.Height = PathHeight; ApplyLayout(); ApplyMode();
            if (StartTab != null) { tab = StartTab; StartTab = null; }   // built again after a zoom: the same page
            ShowTab();

            timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += delegate { OnTimer(); };
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                // zoom: Ctrl + / Ctrl - / Ctrl 0 (the main keyboard and the number pad)
                if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)) { e.SuppressKeyPress = true; ZoomBy(1); return; }
                if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)) { e.SuppressKeyPress = true; ZoomBy(-1); return; }
                if (e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0)) { e.SuppressKeyPress = true; ZoomTo(100); return; }
                if (term != null && term.Focused) return;   // AdeeboShell (and its nano/vim) owns every key: / ^K ^1-7 Esc are its own there
                if (e.KeyCode == Keys.Escape)
                {
                    if (langSheet != null && langSheet.Visible) { e.SuppressKeyPress = true; HideLangSheet(); return; }
                    if (topMenu != null && topMenu.Visible) { e.SuppressKeyPress = true; HideTopMenu(); return; }
                    if (buddySheet != null && buddySheet.Visible) { e.SuppressKeyPress = true; HideBuddySheet(); return; }
                    if (pageMenu != null && pageMenu.Visible) { e.SuppressKeyPress = true; HidePageMenu(); return; }
                    if (viewSheet != null && viewSheet.Visible) { e.SuppressKeyPress = true; if (switchingTo == null) HideViewSheet(); return; }   // the view sheet closes first
                    if (tbSearch != null && tbSearch.Box.Focused && tbSearch.Box.Text != "") return;   // its own handler clears it
                    if (tab == "toolbox" && tbSearch != null && tbSearch.Box.Text != "" && toolboxPage.ContainsFocus) { e.SuppressKeyPress = true; tbSearch.Box.Text = ""; return; }   // a filter is on: Esc clears it first
                    // typing in any box: Esc clears that box (and stays) - it never throws away a running check or a result
                    var tb = ActiveTextBox(); if (tb != null && tb.Text != "" && !tb.ReadOnly) { e.SuppressKeyPress = true; tb.Text = ""; return; }
                    // a tool still running: a stray Esc doesn't throw it away (the window's X still closes)
                    if (term != null && term.Focused) return;   // the shell's own Esc (clear the line, vim's normal mode) - never closes the window
                    if (tbBusy || sniffing || ShBusy) { e.SuppressKeyPress = true; return; }   // R2-127: the shell's runs and scripts too
                    Close();
                }
                else if (tab == "toolbox" && tbCanvas != null && e.Control && (e.KeyCode == Keys.PageDown || e.KeyCode == Keys.PageUp) && SnmpView(tbSel))
                {
                    // Ctrl+PageDown / PageUp: the next / previous SNMP view, from the keyboard
                    e.Handled = true; int k = Array.FindIndex(SnmpTabs, z => z[0] == tbSel); k = (k + (e.KeyCode == Keys.PageDown ? 1 : SnmpTabs.Length - 1)) % SnmpTabs.Length; SelectTool(SnmpTabs[k][0]);
                }
                else if (tab == "toolbox" && tbCanvas != null && e.Control && e.KeyCode == Keys.C && !(ActiveControl is TextBoxBase) && !tbSearch.Box.Focused && !tbBoxes.Any(b => b != null && b.Box.Focused) && (tbRows != null || (tbSel == "mtu" && mtuHops != null) || (tbSel == "mtr" && mtrHops != null)) && !Examples.IsExample(tbRows) && !tbBusy)
                {
                    e.Handled = true; try { Clipboard.SetText(ResultText()); tbCopied = DateTime.Now; tbCanvas.Invalidate(); } catch (Exception ce) { CopyFailed(ce); }   // Ctrl+C copies the result, like the link
                }   // Esc in a filled search box clears it (its own handler), it doesn't close the view
                else if (e.KeyCode == Keys.K && e.Control) { e.SuppressKeyPress = true; OpenPalette(); }
                else if (e.KeyCode == Keys.Oemtilde && e.Control) { e.SuppressKeyPress = true; OpenNav("shell"); }   // Ctrl+` : AdeeboShell from anywhere
                else if (e.KeyCode == Keys.OemQuestion && !e.Shift && !e.Control && !e.Alt && ActiveTextBox() == null) { e.SuppressKeyPress = true; OpenPalette(); }   // "/" = search anything
                else if (e.Control && !e.Alt && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D7) { var bar = NavBar(); int k = e.KeyCode - Keys.D1; if (k < bar.Count) { e.SuppressKeyPress = true; OpenNav(bar[k]); } }
                else if (e.KeyCode == Keys.F1)
                {
                    // F1: the explanation for what's under the mouse, or for the tool you're in - no hovering needed
                    // keyboard in the Toolbox (list or a box has focus): the tool you're on; otherwise what's under the mouse
                    bool inTb = tab == "toolbox" && tbCanvas != null && toolboxPage.ContainsFocus;
                    e.Handled = true; string k = inTb ? "tb." + tbSel : tipKey; bool kb = inTb || k == null;
                    if (k == null && tab == "toolbox" && tbCanvas != null) k = "tb." + tbSel;
                    var en = k != null ? TipEntry(k) : null; if (en == null) return;
                    var at = kb && tbCanvas != null ? tbCanvas.PointToScreen(new Point(Sc(20), Sc(20))) : Cursor.Position;
                    tipTimer.Stop(); tip.ShowAt(en, at, this);
                }
            };
            Shown += delegate
            {
                timer.Start(); RefreshConns(true);
                // warm the Toolbox while you look at the home page: its explanations are parsed off the UI thread, and the
                // page itself is built a moment later when the app is idle - so the first click on Toolbox is instant
                Task.Factory.StartNew(() => { try { Knowledge.Guide("terms"); } catch (Exception e) { Program.Log("warm: " + e.Message); } });
                var warm = new System.Windows.Forms.Timer { Interval = 2500 };
                warm.Tick += delegate { warm.Stop(); warm.Dispose(); if (!IsDisposed && tab != "toolbox") EnsureToolbox(); };
                warm.Start();
            };
            tipTimer.Tick += delegate
            {
                tipTimer.Stop();
                if (tipWatch) { if (!tip.Visible) { tipWatch = false; return; } if (tipKey == null || !TipStillWanted()) { tipKey = null; tip.Hide(); tipWatch = false; return; } tipTimer.Interval = 400; tipTimer.Start(); return; }
                ShowTip();
            };
            // Minimising means "get out of my way": same as Esc / Back to corner - the view closes, frees its memory, the widget keeps watching.
            // ...except while a tool or a capture runs: then it just stays minimised (a 60-minute MTR mustn't end because of Win+D)
            Resize += delegate { if (WindowState == FormWindowState.Minimized && Visible && !tbBusy && !sniffing) BeginInvoke((Action)Close); };
            if (!Cfg.SeenTip && Cfg.SetupDone) BuildTipBar();   // 23 Nov: first open shows the setup card alone; the tip waits for the next open
            Deactivate += delegate { SetTipKey(null); };
            FormClosed += delegate
            {
                if (histBmp != null) { histBmp.Dispose(); histBmp = null; }
                if (weekBmp != null) { weekBmp.Dispose(); weekBmp = null; }
                // WinForms makes a small copy of the window icon for each window; resetting the icon releases it now, not "someday".
                try { ShowIcon = false; Icon = null; } catch { }
                ReleaseExtras();   // the picture animation, calculator, journey, lab, replay and switch timers, and cached bitmaps (FullView.Export.cs)
                Knowledge.Hold = false; if (watchdog != null) watchdog.Dispose(); FreezeCard.Hide(); M.ProcsTab = false; timer.Stop(); timer.Dispose(); tipTimer.Stop(); tipTimer.Dispose();
                try { if (tip.IsHandleCreated) tip.Close(); } catch { } tip.Dispose(); live.Dispose(); list.ReleaseAll(); procs = new List<AppStat>(); pinResults = null;
                foreach (var l in subBtns.Values) foreach (var c in l) if (!c.IsDisposed) c.Dispose(); subBtns.Clear(); connSnap = new List<Conn>(); treeRows = null; treeBy = null; svcs = null; startups = null; realPkts = null; realDecode = null; chatPkts = null; learnPkts = null; m365Rows = null; routeTable = null; travelDest = null; guideRun = null; replayDay = null; if (tbRun0 != null) tbRun0.Stop = true; mtuHops = null; mtrHops = null; tbRows = null; if (replayBmp != null) { replayBmp.Dispose(); replayBmp = null; } };
            Load += delegate { ApplyTheme(); };
        }

        // the header chip that shows online lookups are on, with the minutes left; one click turns them off
        void SyncOnlineChip()
        {
            bool on = Online.On; string t = on ? "Online · " + Math.Max(1, (int)Math.Ceiling(Online.Left.TotalMinutes)) + " min left" : "";
            onlineChip.Visible = on;
            if (on && onlineChip.Text != t) onlineChip.Text = t;
        }
        // ---- self-test hooks (the sitting test): pretend the PC just slept, read the Time machine's "Opened from"
        internal void TestSlept() { lastWatch = DateTime.UtcNow.AddSeconds(-60); lastPong = DateTime.UtcNow.AddSeconds(-60); }
        internal string TestOpenedFrom { get { return replayFrom; } }
        internal void TestShow(string page) { tab = page; ShowTab(); }
        // the sitting test's width check (bug-hunt R2-001: AutoScrollMinSize is always 0 wide, so it measured nothing): the right edge
        // of the furthest button, link or hint painted on a canvas, in its client coordinates
        internal float TestWidest(Control c)
        {
            float m = 0; List<KeyValuePair<RectangleF, Action>> a; List<KeyValuePair<RectangleF, string>> h;
            if (acts.TryGetValue(c, out a)) foreach (var kv in a) m = Math.Max(m, kv.Key.Right);
            if (hints.TryGetValue(c, out h)) foreach (var kv in h) m = Math.Max(m, kv.Key.Right);
            return m;
        }
        internal List<DB> TestCanvases() { return Descendants(this).OfType<DB>().Where(c => c.Visible && c.Width > 300 && c.Height > 200 && IsOnScreen(c)).ToList(); }
        bool IsOnScreen(Control c) { for (var p = c; p != null && p != this; p = p.Parent) if (!p.Visible) return false; return true; }
        static System.Collections.Generic.IEnumerable<Control> Descendants(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in Descendants(c)) yield return d; } }
        static void Prep(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = Draw.WindowText; g.TextContrast = Draw.Contrast; }

        string bugPage = "overview"; DateTime bugCopied = DateTime.MinValue;   // the page a bug report names: the last one before About
        void ShowTab()
        {
            UiActivity.Doing = "switching to " + tab; if (tab != "replay") replayFrom = ""; if (tab != "about") bugPage = tab;   // leaving the Time machine ends an "Opened from" visit (review 21 Nov)
            if (tab == "points" || tab == "learn") System.Threading.ThreadPool.QueueUserWorkItem(delegate { try { LabLink.Pickup(); } catch (Exception e) { Program.Log("lab pickup: " + e.Message); } });   // Network Lab faults watched since (a folder check when nothing changed)
            SetTipKey(null);   // the old page's help card goes with it
            learnSeen = "";    // AM-CG-254: reading time restarts when you come back - time away isn't reading
            if (tab != "settings" && wpics.Count > 0) { foreach (var b in wpics.Values) if (b != null) b.Dispose(); wpics.Clear(); }   // the widget previews (~5 MB) only live while Settings is open
            if (tab != "stream" && tab != "trade") WatchKit.Drop();   // the 90 days of records only live while their page is open
            // one layout pass for the whole switch, not one per page shown or hidden
            if (leftPane != null) leftPane.SuspendLayout();
            try { ShowTab2(); FitBars(this); } finally { if (leftPane != null) leftPane.ResumeLayout(true); }
        }
        // Button bars and tab rows wrap onto a second line when they don't fit (a small screen, 175% scaling, French) instead
        // of cutting the last tabs off; each one grows to what it needs and never below its designed height.
        readonly HashSet<FlowLayoutPanel> fitted = new HashSet<FlowLayoutPanel>();
        void FitBars(Control root)
        {
            foreach (Control c in root.Controls)
            {
                var f = c as FlowLayoutPanel;
                if (f != null && f.Dock == DockStyle.Top && !f.WrapContents && fitted.Add(f))
                {
                    int minH = f.Height; var bar = f; bar.WrapContents = true;
                    bar.Layout += delegate
                    {
                        if (bar.Width <= 0) return;
                        int h = Math.Max(minH, bar.GetPreferredSize(new Size(bar.Width, 0)).Height);
                        if (Math.Abs(bar.Height - h) > 1) bar.Height = h;
                    };
                    bar.PerformLayout();
                }
                if (c.HasChildren && !(c is DB)) FitBars(c);
            }
        }
        void ShowTab2()
        {
            connPage.Visible = tab == "conn"; destPage.Visible = tab == "dest"; healthPage.Visible = tab == "health"; livePage.Visible = tab == "live"; changesPage.Visible = tab == "changes";
            procPage.Visible = tab == "procs"; settingsPage.Visible = tab == "settings"; settingsBtn.Selected = tab == "settings"; gearBtn.Selected = tab == "settings"; dotsBtn.Selected = tab == "about" || tab == "points"; M.ProcsTab = tab == "procs";
            aboutPage.Visible = tab == "about"; aboutBtn.Selected = tab == "about"; pointsPage.Visible = tab == "points"; if (phonePage != null) { phonePage.Visible = tab == "phone"; if (tab == "phone") phoneCanvas.Invalidate(); } if (tab == "points") pointsCanvas.Invalidate();
            linkPage.Visible = tab == "link"; if (tab == "link") RefreshLink(true);
            travelPage.Visible = tab == "travel"; insidePage.Visible = tab == "windows"; learnPage.Visible = tab == "learn";
            // Review-1: coming back to What's wrong? opens at the verdict, not halfway down the last result
            bool enterGuide = tab == "guide" && !guidePage.Visible;
            overviewPage.Visible = tab == "overview"; guidePage.Visible = tab == "guide"; if (enterGuide && guideCanvas != null) guideCanvas.AutoScrollPosition = Point.Empty; replayPage.Visible = tab == "replay"; if (tab == "toolbox") EnsureToolbox(); toolboxPage.Visible = tab == "toolbox"; securityPage.Visible = tab == "security"; gamerPage.Visible = tab == "gamer"; streamPage.Visible = tab == "stream"; sportsPage.Visible = tab == "sports"; tradePage.Visible = tab == "trade"; outlookPage.Visible = tab == "outlook"; shellPage.Visible = tab == "shell"; labPage.Visible = tab == "lab"; if (tab == "lab") LabPageShown();
            if (tab == "replay") ReplayOpen();
            if (tab != "settings" && tab != "about" && tab != "points") lastPage = tab;
            SyncPointsBtn(); WatchUse(tab);
            string sec = SecOf(tab); if (sec != "") lastIn[sec] = tab;
            if (sec != curSec) { curSec = sec; BuildSubTabs(sec); }
            if (tab == "windows" && treeRows == null) LoadInside();
            if (tab == "changes") changesSeen = DateTime.Now;
            if (tab == "procs") RefreshProcs();
            SetTipKey(null); ThemeScrollbars(leftPane ?? (Control)this);
            if (tab != "live" && live.Running) { live.Stop(); liveBtn.Text = AdeeboMeter.L.T("Start live capture"); liveView = null; }
            SyncNav(); if (minePage != null) minePage.Visible = tab == "mine";
            foreach (Control c in subTabs.Controls) ((PillButton)c).Selected = (string)c.Tag == tab;
            if (tab == "dest") destBox.Box.Focus();
        }
        // sections: id, name, then its pages in order (the first is where the section opens the first time)
        // 1.1.5: Home merges Overview, Monitor and Investigate (PLAN 11.11); Network story, Security and Check a destination
        // sit under More by default (FullView.Nav) - the order and what is on the bar are the user's
        static readonly string[][] Secs = {
            new[] { "home", "Home", "overview", "guide", "conn", "link", "procs", "live", "health" },
            new[] { "toolbox", "Toolbox", "toolbox" },
            new[] { "gamer", "Gamer", "gamer" },
            new[] { "outlook", "Outlook", "outlook" },
            new[] { "shell", "AdeeboShell", "shell" },
            new[] { "lab", "AdeeboLab", "lab" },
            new[] { "stream", "Streaming", "stream" },
            new[] { "sports", "Sports", "sports" },
            new[] { "trade", "Markets", "trade" },
            new[] { "time", "Time machine", "replay", "changes" },
            new[] { "learn", "Learn", "learn", "windows" },
            new[] { "story", "Network story", "travel" },
            new[] { "security", "Security", "security" },
            new[] { "dest", "Check a destination", "dest" } };
        // Simple mode shows only these sections; pages of the others still open when something links to them (a Fix a problem
        // answer that needs a Toolbox tool, say) - only their tab is hidden
        static readonly HashSet<string> SimpleSecs = new HashSet<string> { "home", "gamer", "stream", "trade", "time", "learn" };   // Learn is in every view
        void ApplyMode()
        {
            if (tabs == null) return;
            tabs.SuspendLayout();
            foreach (Control c in tabs.Controls) c.Visible = !Cfg.Simple || SimpleSecs.Contains(c.Tag as string ?? "") || !(c is PillButton) || (string)c.Tag == "more";
            tabs.ResumeLayout(true);
        }
        void SetModeTo(string m)
        {
            Cfg.Mode = m; Cfg.Save(); ApplyMode();
            if (overviewCanvas != null) overviewCanvas.Invalidate();
        }
        static string SecOf(string page) { foreach (var sc in Secs) for (int i = 2; i < sc.Length; i++) if (sc[i] == page) return sc[0]; return ""; }
        static readonly Dictionary<string, string[]> tabNames = new Dictionary<string, string[]> {
            { "overview", new[] { "Today", "Today" } },
            { "conn", new[] { "Connections", "Connections" } }, { "link", new[] { "Your Wi-Fi", "Wi-Fi" } }, { "procs", new[] { "What's running", "Running" } }, { "live", new[] { "Live capture", "Live" } },
            { "guide", new[] { "What's wrong?", "Problem" } }, { "dest", new[] { "Check a destination", "Check" } }, { "health", new[] { "Health", "Health" } }, { "toolbox", new[] { "Toolbox", "Toolbox" } }, { "gamer", new[] { "Gamer Zone", "Gamer" } }, { "stream", new[] { "Streaming", "Streaming" } }, { "sports", new[] { "Sports", "Sports" } }, { "trade", new[] { "Markets", "Markets" } }, { "outlook", new[] { "Outlook", "Outlook" } }, { "security", new[] { "Security", "Security" } },
            { "travel", new[] { "Network story", "Story" } },
            { "replay", new[] { "Replay", "Replay" } }, { "changes", new[] { "What changed", "Changes" } },
            { "learn", new[] { "Topics", "Topics" } }, { "windows", new[] { "Inside Windows", "Windows" } }, { "phone", new[] { "Your phone", "Phone" } } };
        void BuildSubTabs(string sec)
        {
            subTabs.SuspendLayout();
            subTabs.Controls.Clear();   // the buttons are kept per section (made once), so switching sections creates no new windows
            List<Control> btns;
            if (!subBtns.TryGetValue(sec, out btns))
            {
                btns = new List<Control>(); subBtns[sec] = btns;
                var def = Secs.FirstOrDefault(x => x[0] == sec);
                if (def != null && def.Length > 3)
                    for (int i = 2; i < def.Length; i++)
                    {
                        var b = new PillButton(tabNames[def[i]][shortTabs ? 1 : 0], PillButton.K.Tab) { Tag = def[i], Margin = new Padding(0, 0, Sc(4), 0) };   // underlined tabs, as the mockups
                        b.Click += delegate(object s, EventArgs e) { tab = (string)((Control)s).Tag; ShowTab(); };
                        btns.Add(b);
                    }
            }
            subTabs.Controls.AddRange(btns.ToArray());
            foreach (var c in btns) { string[] n; if (tabNames.TryGetValue((string)c.Tag, out n)) c.Text = n[shortTabs ? 1 : 0]; }
            subTabs.Visible = subTabs.Controls.Count > 0;
            subTabs.ResumeLayout(); UpdateBadges();
            subTabs.Invalidate(true);   // V01: the strip and every tab repaint after a swap - left alone, the last section's tab edges stayed as square lines
        }
        bool shortTabs;
        void FitTabs()
        {
            if (subTabs.Controls.Count == 0) return;
            int used = subTabs.Controls.Cast<Control>().Sum(c => c.Width + c.Margin.Horizontal) + subTabs.Padding.Horizontal;
            bool want = shortTabs ? used + Sc(220) > subTabs.ClientSize.Width : used > subTabs.ClientSize.Width;   // a little slack so it doesn't flip back and forth
            if (want == shortTabs) return;
            shortTabs = want;
            foreach (Control c in subTabs.Controls) { string[] n; if (tabNames.TryGetValue((string)c.Tag, out n)) c.Text = n[shortTabs ? 1 : 0]; }
        }
        void StyleChips()
        {
            foreach (Control c in chips.Controls) { var b = c as PillButton; if (b != null && b != idle) b.Selected = (string)b.Tag == list.Filter; }
        }

        string headKey = "", pathKey = "", sideKey = "", sideShape = "", sideH = "", sideP = "", sideT = "", footKey = "";
        string gamerVer = "";
        void OnTimer()
        {
            UiActivity.Doing = "timer on " + tab; var tsw = Stopwatch.StartNew(); try { OnTimer2(); } finally { if (tsw.ElapsedMilliseconds > 60) Program.Log("slow tick: " + tab + " " + tsw.ElapsedMilliseconds + " ms"); }
        }
        // after a reading lands: the needle rests where the connection is now; every 5 minutes it breathes once
        int logoLv = -9;
        void LogoTick()
        {
            if (header == null || snap == null) return;
            if (snap.Level != logoLv) { logoLv = snap.Level; header.Invalidate(new Rectangle(0, 0, Sc(60), header.Height)); }
            // 23 Nov (motion review): no 5-minute "breath" - a loop on a window that stays open is decoration; the needle still moves to the new level
        }
        // a health row's history in a few numbers - one pass for its low and high (bug-hunt ML-147: it was two scans per row)
        static string HistKey(double[] h)
        {
            if (h.Length == 0) return "";
            double mx = double.NaN, mn = double.NaN; foreach (var z in h) { if (double.IsNaN(z)) continue; if (double.IsNaN(mx) || z > mx) mx = z; if (double.IsNaN(mn) || z < mn) mn = z; }
            var ic = CultureInfo.InvariantCulture; return h.Length + ":" + h[0].ToString("0.#", ic) + ":" + h[h.Length - 1].ToString("0.#", ic) + ":" + (double.IsNaN(mx) ? 0 : mx).ToString("0.#", ic) + ":" + (double.IsNaN(mn) ? 0 : mn).ToString("0.#", ic);
        }
        void OnTimer2()
        {
            if (M.Version != lastVersion) { lastVersion = M.Version; snap = M.Latest; LogoTick(); string hk = snap.Level + "|" + snap.Headline + "|" + snap.Flow + "|" + DateTime.Now.Hour + "|" + Mood.Quip(snap);   /* the mood line changes on its own (excuses, koans) */ if (hk != headKey) { headKey = hk; pathPanel.Invalidate(); header.Invalidate(); }
                // the path strip and the side panel repaint only when what they print changed
                string pk = snap.Hint + "|" + string.Join(",", snap.Path) + "|" + string.Join("|", snap.PathLabel) + "|" + string.Join("|", snap.PathMain) + "|" + string.Join("|", snap.PathSub);
                if (pk != pathKey) { pathKey = pk; pathPanel.Invalidate(); }
                // the side panel: each card redraws alone when only its numbers moved; a new shape (rows, collapsed) redraws the panel
                string skH = string.Join("|", snap.Health.Select(h => h[1] + h[2])) + "|" + string.Join("|", snap.HealthHist.Select(HistKey)),
                       skP = Draw.Fmt(snap.Cpu, "0", "%") + snap.MemText + snap.DiskText + Draw.Fmt(snap.Down, "0.0", ""),
                       skT = string.Join(",", snap.TopMem.Take(6).Select(t => t.Name + Draw.MemStr(t.Mem))) + "|" + string.Join(",", snap.TopCpu.Take(6).Select(t => t.Name + t.Cpu.ToString("0.0", CultureInfo.InvariantCulture))),
                       skL = Cfg.SideOpen + "|" + snap.Health.Count + "|" + Math.Min(6, Math.Max(snap.TopMem.Count, snap.TopCpu.Count)) + "|" + side.ClientSize;
                if (skL != sideShape || !Cfg.SideOpen) { if (skL != sideShape || skH + skP + skT != sideKey) { sideShape = skL; sideKey = skH + skP + skT; sideH = skH; sideP = skP; sideT = skT; side.Invalidate(); } }
                else
                {
                    var due = new List<string>();
                    if (skH != sideH) due.Add("side.health"); if (skP != sideP) due.Add("side.pc"); if (skT != sideT) due.Add("side.top");
                    sideH = skH; sideP = skP; sideT = skT; sideKey = skH + skP + skT;
                    if (due.Count > 0 && !RedrawSpots(side, due.ToArray())) side.Invalidate();
                } if (tab == "changes") { var ne = M.Events.Recent(1); string ck = (ne.Count > 0 ? ne[0].T.Ticks + ne[0].Text : "") + "|" + DateTime.Now.Minute; if (ck != changesKey) { changesKey = ck; changesCanvas.Invalidate(); } } if (tab == "overview") OverviewTick(); NowTick(); }   /* What changed: only when an event arrives (and once a minute for its "x min ago") */
            if (pointsFlashUntil != DateTime.MinValue) { SyncPointsBtn(); if (DateTime.Now > pointsFlashUntil) pointsFlashUntil = DateTime.MinValue; }
            if (LearnPending && !learnTimed && (DateTime.Now - learnAt).TotalSeconds >= 15) { learnTimed = true; learnInfo.Invalidate(); }   // a topic being read: one repaint when the 15 s mark lands, not one a second
            if ((DateTime.UtcNow - lastConns).TotalSeconds >= (tab == "conn" || tab == "travel" || tab == "procs" ? 2 : 10)) RefreshConns(false);   // the full connection list only refreshes fast where it is shown
            if (++footTick % 2 == 0) { SelfStats(); string fk = selfCpu.ToString("0", CultureInfo.InvariantCulture) + "|" + (selfMem >> 22) + "|" + live.Running + (liveView != null ? liveView.TsharkMB.ToString(CultureInfo.InvariantCulture) : ""); if (fk != footKey) { footKey = fk; footer.Invalidate(); } }
            if ((destRunning || pinRunning) && footTick % 2 == 0) destCanvas.Invalidate();   // bug-hunt ML-145: once a second is enough for a running check (its results repaint as they land)
            if (tbBusy && tab == "toolbox" && tbCanvas != null) { var rr = tbRun0; string tk = (rr == null ? "" : rr.Status + "|" + rr.Count) + "|" + HopsPrint(mtrHops) + "|" + HopsPrint(mtuHops);   /* 21 Nov: no more "every 2 s anyway" (the page flickered) - MTR's and MTU's hop tables are part of the key instead */ if (tk != tbBusyKey) { tbBusyKey = tk; tbCanvas.Invalidate(); } }   // a running tool: repaint when its status or its chart moves, not twice a second regardless
            StreamTick(); SportsTick(); TradeTick(); OutlookTick(); GamerHudTick(); HeartTick();
            if (tab == "gamer" && footTick % 2 == 0 && (GamePing.Me.Playing != null || Games.Monitored().Count > 0)) { int sp; lock (GamePing.Me.Spikes) sp = GamePing.Me.Spikes.Count; var ss = GamePing.Me.Session; string gv = System.Threading.Interlocked.Read(ref GamePing.Ver).ToString(CultureInfo.InvariantCulture); string gk = GamePing.Me.Playing + "|" + sp + "|" + (ss != null && ss.Open ? ss.Rows.Count / 5 : -1) + "|" + DateTime.Now.Minute; if (gk != gamerKey) { gamerKey = gk; gamerVer = gv; gamerCanvas.Invalidate(); } else if (gv != gamerVer) { gamerVer = gv; if (!RedrawSpots(gamerCanvas, "gm.live")) gamerCanvas.Invalidate(); } }   /* a new sample only: just the cards and the map */   /* only when a new sample, a spike or the session moved (and once a minute for the "ago" times) */
            Online.Tick(); if (onlineChip != null) SyncOnlineChip();
            if (tab == "procs" && (DateTime.UtcNow - lastProcs).TotalSeconds >= 2) RefreshProcs();
            if (tab == "shell") ShStatusTick();
            { int bzMore; bool bzOn = Busy.Oldest(out bzMore) != null; if (bzOn || footBusy) footer.Invalidate(); footBusy = bzOn; }   // the footer's "waiting on" note ticks, then clears
            if (tab == "link") { RefreshLink(false); if (switchBusy || (linkSnap == null && linkBusy)) linkCanvas.Invalidate(); }   // the first read ticks its seconds
            if (tab == "changes") changesSeen = DateTime.Now;
            UpdateBadges();
            if (live.Running && ++liveTick % 2 == 0)
            {
                var map = list.PortToApp();
                double net = M.NetPing.Avg(10); double oneWay = double.IsNaN(net) ? 30 : net / 2;
                liveView = live.Snapshot(delegate(int port) { string a; return map.TryGetValue(port, out a) ? a : null; }, oneWay);
                liveCanvas.Invalidate();
            }
            else if (!live.Running && liveBtn.Text != L.T("Start live capture")) { liveBtn.Text = AdeeboMeter.L.T("Start live capture"); liveCanvas.Invalidate(); }
        }

        // bug-hunt H-3: stopping tshark/dumpcap can wait several seconds (process tree, kills, joins) - never on the window's
        // thread. The button is disabled and says what's happening until the background step lands.
        void ToggleLive()
        {
            if (!liveBtn.Enabled) return;
            bool stopping = live.Running; string iface; lock (M.L) iface = M.IfaceId;
            liveBtn.Enabled = false; liveBtn.Text = AdeeboMeter.L.T(stopping ? "Stopping..." : "Starting...");
            new System.Threading.Thread(() =>
            {
                bool started = false;
                try { if (stopping) live.Stop(); else started = live.Start(iface); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("live toggle: " + e.GetType().Name); }
                try
                {
                    if (!IsDisposed) BeginInvoke((Action)delegate
                    {
                        liveBtn.Enabled = true; liveBtn.Text = AdeeboMeter.L.T(live.Running ? "Stop live capture" : "Start live capture");
                        if (started) liveView = null; liveCanvas.Invalidate();
                    });
                }
                catch (Exception e) { if (!(e is InvalidOperationException || e is ObjectDisposedException)) throw; }
            }) { IsBackground = true, Name = "live-toggle" }.Start();
        }

        // Light / dark: recolour every control; the painted parts read the theme on their next paint.
        public void ApplyTheme()
        {
            SetTipKey(null); BackColor = Theme.Bg; Recolor(this); themedScroll.Clear(); foreach (var nb in navBmps.Values) nb.Dispose(); navBmps.Clear();   // icons are cached per colour; a new theme starts fresh ThemeScrollbars(this);
            try { int on = Theme.Dark ? 1 : 0; if (Native.DwmSetWindowAttribute(Handle, 20 /*immersive dark title bar*/, ref on, 4) != 0) Program.Log("dark title bar not supported here"); } catch { }
            Refresh();
        }
        string changesKey = "", tbBusyKey = "", travelKey = "";   // repaint only on change (What changed, a running tool, the story's connection list)
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
                else if (c is CheckBox || c is RadioButton) { c.BackColor = Theme.Card; c.ForeColor = Theme.Ink; }   // the Toolbox's safety tick must stay readable after a theme switch
                else if (!(c is ScrollBar)) c.BackColor = Theme.Card;
                Recolor(c); c.Invalidate();
            }
        }

        bool footBusy;
        void RefreshConns(bool force)
        {
            if (refreshing || IsDisposed) return;
            refreshing = true; lastConns = DateTime.UtcNow;
            bool names = force || (DateTime.UtcNow - lastNames).TotalSeconds > (tab == "conn" || tab == "travel" || tab == "procs" ? 15 : 60);   // every 15 s where names are shown, every minute for the rest (Calls, Connections() users)
            if (names) lastNames = DateTime.UtcNow;
            Task.Factory.StartNew(() =>
            { var __bz = Busy.Begin("Listing connections"); try { 
                List<Conn> l = null;
                try { if (names && Cfg.IpNames) M.RefreshIpNames(); l = M.Connections(true); } catch (Exception e) { Program.Log("conns: " + e.Message); }
                try { if (!IsDisposed) BeginInvoke((Action)delegate { refreshing = false; if (l != null && !IsDisposed) { list.Merge(l); connSnap = l; if (tab == "travel" && storyTarget == "") { int hs = 0; foreach (var z in l) hs = unchecked(hs + (z.Key + z.State + z.Host).GetHashCode()); string tvk = l.Count + "|" + hs;   /* bug-hunt ML-139: an order-free sum - no join or sort of every connection */ if (tvk != travelKey) { travelKey = tvk; travelCanvas.Invalidate(); } } } }); else refreshing = false; } catch { refreshing = false; }
            } finally { __bz.End(); } });
        }

        void UpdateConnStatus()
        {
            int syn = list.SynSent;
            string s = list.Shown + " shown · " + list.Established + " established" + (syn > 0 ? " · " + syn + Voice.P(" stuck knocking (SYN_SENT) - blocked or dead path?", " in SYN_SENT (blocked or dead path?)") : "");
            if (M.dnsCacheLocked && !live.Running) s += Voice.P("    |    Windows keeps its DNS cache admin-only here, so names show while Live capture runs", "    |    Host names: start Live capture (DNS cache is admin-only)");
            if (sniffStatus != "") s += "    |    " + sniffStatus;
            else if (!npcap) s += Voice.P("    |    Sniff needs Wireshark + Npcap - not on this PC.", "    |    Sniff needs Wireshark and Npcap.");
            else s += "    |    Sniff: a row's Sniff button captures it for " + Cfg.SniffSec + " s and opens Wireshark";
            bool keep = Program.LastCapture != "" && File.Exists(Program.LastCapture);
            if (keep) s += "    ·    Keep this capture (click)";
            connStatus.Text = s; connStatus.Cursor = keep ? Cursors.Hand : Cursors.Default;
        }
        // copies the newest capture to where you choose - the original is still deleted when AdeeboMeter exits
        void KeepCapture()
        {
            string f = Program.LastCapture; if (f == "" || !File.Exists(f)) return;
            using (var d = new SaveFileDialog { Title = "Keep this capture", Filter = "Wireshark capture (*.pcapng)|*.pcapng", FileName = Path.GetFileName(f), InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), OverwritePrompt = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { File.Copy(f, d.FileName, true); sniffStatus = "Kept: " + d.FileName; UpdateConnStatus(); }
                catch (Exception e) { Program.Log("keep capture: " + e.Message); sniffStatus = "Couldn't keep it: " + e.Message; UpdateConnStatus(); }
            }
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
        // the tool-run test (toolbox:<tool>:fillreal): the examples' documentation addresses (192.0.2.x, 198.51.100.x, 203.0.113.x)
        // never answer - this network's router stands in for them, so the tools measure something real
        void RealAddresses()
        {
            string gw; lock (M.L) gw = M.Gateway ?? ""; if (gw == "") return;
            foreach (var b in tbBoxes) if (b != null && !b.Box.UseSystemPasswordChar) { string v = b.Box.Text, nv = System.Text.RegularExpressions.Regex.Replace(v, @"\b(192\.0\.2|198\.51\.100|203\.0\.113)\.\d{1,3}\b", gw); if (nv != v) b.Box.Text = nv; }
        }
        public void Debug(string page, string tipK, string destIn)
        {
            string sub = null; int c = page.IndexOf(':'); if (c > 0) { sub = page.Substring(c + 1); page = page.Substring(0, c); }   // toolbox:dev or toolbox:snmpif
            // --view peek:toolbox writes what screen peek would send for that page to peek-test.jpg beside the exe
            if (page == "peek" && sub != null) { var jb = PeekPage(sub); if (jb != null) File.WriteAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "peek-test.jpg"), jb); page = sub; }
            // --view lang[:bn] opens the language card (with that tile under the mouse) - for screenshots
            if (page == "lang") { LangMenu(); if (sub != null) { langDebug = sub; langSheet.Invalidate(); } return; }
            var known = new HashSet<string>(tabNames.Keys) { "overview", "toolbox", "gamer", "stream", "sports", "trade", "replay", "learn", "security", "travel", "dest", "procs", "health", "link", "changes", "outlook", "conn", "windows", "phone", "live", "guide", "settings", "about", "points", "shell", "lab" };
            if (!known.Contains(page)) { Program.Log("--view: no page called \"" + page + "\" - opening Home"); Text = "AdeeboMeter - no page \"" + page + "\""; page = "overview"; }   // R2-196: a typo'd page name says so
            tab = page; ShowTab();
            if (page == "security" && sub == "run") SecRun(null);
            // --view phone:qr shows a code; phone:ask shows the Allow question as a phone would raise it (screenshots of the page)
            if (page == "phone" && sub == "qr" && PhoneHub.Start != null) PhoneHub.Start();
            if (page == "phone" && sub == "ask") System.Threading.Tasks.Task.Factory.StartNew(() => PhoneHub.Ask("allow", "Test phone", L.T("wants to watch this PC and start checks for up to 30 minutes."), "nectar · moon · drum · blade"));
            if (page == "phone" && sub == "askold") { sub = "ask"; System.Threading.Tasks.Task.Factory.StartNew(() => PhoneHub.Ask("allow", "Test phone", L.T("wants to watch this PC and start checks for up to 30 minutes."), "")); }
            if (page == "settings" && sub != null) { if (sub.StartsWith("q=", StringComparison.Ordinal)) settingsSearch.Box.Text = sub.Substring(2); else { var hit = SetSections.FirstOrDefault(z => z.StartsWith(sub, StringComparison.OrdinalIgnoreCase)); if (hit != null) setSec = hit; } settingsCanvas.Invalidate(); }
            if (page == "guide" && sub != null && Investigate.All.Any(f => f.Id == sub)) StartGuide(sub, false);
            if (page == "toolbox" && sub != null) { EnsureToolbox(); bool ex = sub.EndsWith(":ex", StringComparison.Ordinal), real = sub.EndsWith(":fillreal", StringComparison.Ordinal), fill = real || sub.EndsWith(":fill", StringComparison.Ordinal); if (ex) sub = sub.Substring(0, sub.Length - 3); if (fill) sub = sub.Substring(0, sub.Length - (real ? 9 : 5)); if (Sections.Any(z => z[0] == sub)) ShowSection(sub); else { OpenTool(sub); if (ex) ShowExample(); if (fill) FillExample(sub); if (real) RealAddresses(); } }   // toolbox:ping:ex opens its Example (screenshots); toolbox:ping:fill puts the example's inputs in the boxes (the tool-run test presses Run)
            if (page == "trade" && sub != null && MkTabs.Any(t => t[0] == sub)) mkTab = sub;   // trade:fx opens Markets › Currencies (links and screenshots)
            if (page == "sports" && sub != null) spTab = sub;
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
            if (page == "guide" && destIn != null) StartGuide(destIn, false);
            if (page == "travel" && destIn == "demo-journey")
            {
                // screenshot aid: a made-up game conversation on the packet journey, explained
                storyApp = "VALORANT-Win64-Shipping"; storyIp = "203.0.113.40"; storyPort = 7086; storyUdp = true; storyTarget = "203.0.113.40:7086";
                jFlow = new Journey.Flow { Remote = storyIp, RemotePort = 7086, LocalPort = 61000, Udp = true, App = storyApp };
                jSess = new Journey.Session(jFlow, Journey.Sample()); jKind = "realtime"; jPlay = 1.62; jPing = new[] { 31.0, 33, 30, 41, 32, 30, 55, 31, 30, 34 };
                jNotes = Journey.Explain(jFlow, jSess.Snapshot(), jKind, true); jScrollTo = true; travelCanvas.Invalidate();
            }
            else if (page == "travel" && destIn != null) { travelBox.Box.Text = destIn; StorySite(); }
            if (page == "toolbox" && destIn != null) { var parts = destIn.Split('|'); SelectTool(parts[0]); for (int i = 1; i < parts.Length && i - 1 < tbBoxes.Count; i++) if (tbBoxes[i - 1] != null) tbBoxes[i - 1].Box.Text = parts[i]; StartTool(); }
            if (page == "learn" && destIn != null) { learnSel = destIn.StartsWith("lab:", StringComparison.Ordinal) || destIn.StartsWith("game:", StringComparison.Ordinal) ? destIn : destIn == "home" ? "" : "topic:" + destIn; learnInfo.Invalidate(); learnList.Invalidate(); }
            if (page == "replay" && destIn != null) { double f; if (double.TryParse(destIn, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) ReplayAt(f); }
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
            tab = "overview"; ShowTab();
        }
        static string P0(long v) { return v.ToString(CultureInfo.InvariantCulture).PadLeft(5); }
        public string Perf()
        {
            var sb = new StringBuilder(); var sw = new Stopwatch();
            var pages = new[] { "overview", "conn", "link", "procs", "live", "guide", "dest", "health", "toolbox", "travel", "replay", "changes", "learn", "windows", "settings", "about", "points" };
            var canvas = new Dictionary<string, Control> { { "overview", overviewCanvas }, { "conn", list }, { "link", linkCanvas }, { "procs", procList }, { "live", liveCanvas }, { "guide", guideCanvas }, { "dest", destCanvas }, { "health", healthCanvas }, { "toolbox", toolboxPage },
                { "travel", travelCanvas }, { "replay", replayCanvas }, { "changes", changesCanvas }, { "learn", learnList }, { "windows", insideList }, { "settings", settingsCanvas }, { "gamer", gamerCanvas }, { "about", aboutCanvas }, { "points", pointsCanvas } };
            sb.AppendLine("window " + Width + "x" + Height);
            sb.AppendLine("page       first  again  (show+paint)  page-paint  scroll-step  tick   ms");
            var first = new Dictionary<string, long>();
            foreach (var t in pages) { sw.Restart(); tab = t; ShowTab(); Refresh(); Application.DoEvents(); first[t] = sw.ElapsedMilliseconds; for (int i = 0; i < 2; i++) { Thread.Sleep(150); Application.DoEvents(); } }
            // bug-hunt R2-019: the cold first paint is checked, not only recorded - --perf runs in a fresh process, so this is the
            // one place a cold Home (250-570 ms when the JIT warm-up went) can be caught. 1.5 s is far over normal.
            foreach (var kv in first) if (kv.Value > 1500) sb.AppendLine("SLOW FIRST PAINT: " + kv.Key + " " + kv.Value + " ms (over 1500)");
            foreach (var t in pages)
            {
                tab = "about"; ShowTab(); Refresh(); Application.DoEvents();
                sw.Restart(); tab = t; ShowTab(); long show = sw.ElapsedMilliseconds; Refresh(); long again = sw.ElapsedMilliseconds;
                Control c; if (!canvas.TryGetValue(t, out c) || c == null) { sb.AppendLine(t.PadRight(10) + P0(first[t]) + "  (no canvas to time)"); continue; }   // AM-CG-307: a page without a timing entry is reported, never a crash
                sw.Restart(); for (int i = 0; i < 5; i++) c.Refresh(); long pp = sw.ElapsedMilliseconds / 5;
                long scr = -1; var sc = c as ScrollableControl;
                if (sc != null && sc.AutoScroll && sc.AutoScrollMinSize.Height > sc.ClientSize.Height + 200)
                { sw.Restart(); for (int i = 1; i <= 5; i++) { sc.AutoScrollPosition = new Point(0, i * 60); sc.Update(); } scr = sw.ElapsedMilliseconds / 5; sc.AutoScrollPosition = Point.Empty; }
                sw.Restart(); for (int i = 0; i < 5; i++) { OnTimer2(); Application.DoEvents(); } long tick = sw.ElapsedMilliseconds / 5;
                Func<long, string> P = v => (v < 0 ? "-" : v.ToString(CultureInfo.InvariantCulture)).PadLeft(5);
                sb.AppendLine(t.PadRight(10) + P(first[t]) + "  " + P(again) + "  (" + P(show) + ")      " + P(pp) + "       " + P(scr) + "     " + P(tick));
            }
            // 23 Nov (Adeeb's PC): the window was closed during the run and this crashed on a disposed canvas - now it says so
            if (IsDisposed || overviewCanvas == null || overviewCanvas.IsDisposed) { sb.AppendLine("STOPPED: the window was closed during the run (Esc or Back to corner?) - the text timing was skipped. Leave the window alone while --perf runs."); return sb.ToString(); }
            // text: GDI+ (what the pages use now) vs GDI (TextRenderer), 300 lines each, on the real screen buffer
            using (var cg = overviewCanvas.CreateGraphics())
            using (var bg = BufferedGraphicsManager.Current.Allocate(cg, new Rectangle(0, 0, 900, 700)))
            {
                var g = bg.Graphics; Prep(g); var col = Theme.Ink;
                using (var br = new SolidBrush(col))
                {
                    sw.Restart(); for (int i = 0; i < 300; i++) g.DrawString("Your router answers in 3 ms · line " + i, Draw.fBody, br, 10, (i % 30) * 20); long gp = sw.ElapsedMilliseconds;
                    sw.Restart(); for (int i = 0; i < 300; i++) TextRenderer.DrawText(g, "Your router answers in 3 ms · line " + i, Draw.fBody, new Point(10, (i % 30) * 20), col, TextFormatFlags.NoPrefix); long gd = sw.ElapsedMilliseconds;
                    sw.Restart(); for (int i = 0; i < 300; i++) Draw.Measure("Your router answers in 3 ms, wrapped at a width · line " + i, Draw.fBody, 300); long gm = sw.ElapsedMilliseconds;
                    sb.AppendLine("text x300: GDI+ " + gp + " ms   GDI " + gd + " ms   measure(new) " + gm + " ms");
                }
            }
            tab = "overview"; ShowTab();
            return sb.ToString();
        }
        public int TestWalk()
        {
            int n = 0;
            foreach (var t in new[] { "overview", "conn", "link", "procs", "live", "guide", "dest", "health", "toolbox", "travel", "replay", "changes", "learn", "windows", "settings", "about", "points", "gamer", "stream", "trade", "sports", "phone", "shell", "security", "outlook" }) { tab = t; ShowTab(); Refresh(); Application.DoEvents(); n++; }   // R2-191: every page
            List<AppStat> l; lock (M.L) l = M.Apps;
            if (l.Count > 0) { tab = "procs"; ShowTab(); SelectProc(l[0]); Thread.Sleep(50); Application.DoEvents(); procInfo.Refresh(); }
            SetTipKey("path.DNS"); ShowTip(); Application.DoEvents(); SetTipKey(null);
            foreach (var w in Investigate.All) { StartGuide(w.Id, true); Application.DoEvents(); n++; }
            ReplayAt(0.5); guideCanvas.Refresh(); replayCanvas.Refresh();
            tab = "overview"; ShowTab();
            return n;
        }

        public void StopCaptures() { live.Stop(); if (jSess != null) jSess.Stop(); if (jTimer != null) jTimer.Enabled = false; }
        TextBox ActiveTextBox()
        {
            Control c = ActiveControl; var cc = c as ContainerControl;
            while (cc != null && cc.ActiveControl != null) { c = cc.ActiveControl; cc = c as ContainerControl; }
            return c as TextBox;
        }
        // ---------- hover explanations and clickable painted areas
        void Begin(Control c)
        {
            List<KeyValuePair<RectangleF, string>> h; if (hints.TryGetValue(c, out h)) h.Clear(); else hints[c] = new List<KeyValuePair<RectangleF, string>>();
            List<KeyValuePair<RectangleF, Action>> a; if (acts.TryGetValue(c, out a)) a.Clear(); else acts[c] = new List<KeyValuePair<RectangleF, Action>>();
        }
        // Live spots (21 Nov, "narrow the redraw everywhere"): a painter marks where a live number is drawn; when only that
        // number changed, only that spot is redrawn instead of the whole page. Rects are stored like Act's (client coords at
        // paint time); any scroll repaints the whole page anyway and records them again.
        readonly Dictionary<Control, Dictionary<string, RectangleF>> liveSpots = new Dictionary<Control, Dictionary<string, RectangleF>>();
        void LiveSpot(Control c, string id, RectangleF r)
        {
            if (spotOnly) return;   // a spot repainting itself: the page's records stay as the last full paint left them
            var sp = ScrollOf(c); r.Offset(sp.X, sp.Y);
            Dictionary<string, RectangleF> d; if (!liveSpots.TryGetValue(c, out d)) liveSpots[c] = d = new Dictionary<string, RectangleF>(StringComparer.Ordinal);
            d[id] = r;
        }
        // a spot that can paint itself (motion review, 23 Nov): its painter draws it straight onto the canvas over its background,
        // so a live number no longer walks the whole page's drawing - a narrow Invalidate still ran every card's text layout
        readonly Dictionary<Control, Dictionary<string, KeyValuePair<Color, Action<Graphics>>>> spotPainters = new Dictionary<Control, Dictionary<string, KeyValuePair<Color, Action<Graphics>>>>();
        bool spotOnly;   // true while a spot paints itself: Hint, Act and LiveSpot keep the full paint's records
        void LiveSpot(Control c, string id, RectangleF r, Color bg, Action<Graphics> paint)
        {
            if (spotOnly) return;
            LiveSpot(c, id, r);
            Dictionary<string, KeyValuePair<Color, Action<Graphics>>> d; if (!spotPainters.TryGetValue(c, out d)) spotPainters[c] = d = new Dictionary<string, KeyValuePair<Color, Action<Graphics>>>(StringComparer.Ordinal);
            d[id] = new KeyValuePair<Color, Action<Graphics>>(bg, paint);
        }
        // paint one spot now, double-buffered, straight onto the canvas; false: not possible now (the caller invalidates instead)
        bool PaintSpotNow(Control c, Rectangle area, Color bg, Action<Graphics> paint)
        {
            if (c == null || c.IsDisposed || !c.IsHandleCreated || !c.Visible || spotOnly || c.Focused) return false;   // focused: the keyboard ring is drawn by the full paint
            area.Intersect(c.ClientRectangle); if (area.Width <= 0 || area.Height <= 0) return true;   // scrolled out of sight: nothing to draw
            try
            {
                using (var cg = c.CreateGraphics())
                using (var buf = BufferedGraphicsManager.Current.Allocate(cg, area))
                {
                    // Clear, not an anti-aliased fill: the fill left a half-pixel dark seam round the spot (24 Nov: a line down the left
                    // of Home's readings and along their top, the same thing the problem tiles once had)
                    var g = buf.Graphics; g.Clear(bg); Prep(g); g.SetClip(area);
                    var sp = ScrollOf(c); g.TranslateTransform(sp.X, sp.Y);
                    spotOnly = true; try { paint(g); } finally { spotOnly = false; }
                    buf.Render();
                }
                var db = c as DB; if (db != null) { db.Paints++; db.PaintArea += (long)area.Width * area.Height; }   // the sitting test counts these too
                return true;
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("spot paint " + c.Name + ": " + e.Message); return false; }
        }
        // redraw just these spots; false when one isn't known yet (the caller then redraws the page)
        bool RedrawSpots(Control c, params string[] ids)
        {
            Dictionary<string, RectangleF> d; if (c == null || !liveSpots.TryGetValue(c, out d)) return false;
            var rs = new List<KeyValuePair<string, Rectangle>>();
            foreach (var id in ids) { RectangleF r; if (!d.TryGetValue(id, out r)) return false; if (r.Width > 0 && r.Height > 0) rs.Add(new KeyValuePair<string, Rectangle>(id, Rectangle.Inflate(Rectangle.Round(r), 3, 3))); }
            Dictionary<string, KeyValuePair<Color, Action<Graphics>>> pd; spotPainters.TryGetValue(c, out pd);
            foreach (var kv in rs)
            {
                KeyValuePair<Color, Action<Graphics>> p;
                if (pd != null && pd.TryGetValue(kv.Key, out p) && PaintSpotNow(c, kv.Value, p.Key, p.Value)) continue;
                c.Invalidate(kv.Value);
            }
            return true;
        }
        // a painted rectangle on a scrolling canvas, in the canvas's own (client) coordinates
        static RectangleF OnScreen(Control c, RectangleF r) { var sp = ScrollOf(c); r.Offset(sp.X, sp.Y); return r; }
        // what a hop table shows, as one short string: the page redraws when a hop's numbers move, not on a clock
        static string HopsPrint(List<PathTools.Hop> l)
        {
            if (l == null) return ""; int h = 17;
            try { foreach (var x in l.ToArray()) unchecked { h = h * 31 + x.N; h = h * 31 + (x.Ip ?? "").GetHashCode(); h = h * 31 + (int)x.Rtt; h = h * 31 + x.Sent; h = h * 31 + x.Lost; h = h * 31 + x.Mtu; h = h * 31 + (x.Reached ? 1 : 0); } }
            catch (ArgumentException) { return "busy"; }   // the tool thread was adding a hop just then: try again next tick
            return l.Count + ":" + h;
        }
        static Point ScrollOf(Control c) { var sc = c as ScrollableControl; return sc != null && sc.AutoScroll ? sc.AutoScrollPosition : Point.Empty; }
        void Hint(Control c, RectangleF r, string key)
        {
            if (key == null || spotOnly) return; var sp = ScrollOf(c); r.Offset(sp.X, sp.Y);
            List<KeyValuePair<RectangleF, string>> h; if (!hints.TryGetValue(c, out h)) hints[c] = h = new List<KeyValuePair<RectangleF, string>>();
            h.Add(new KeyValuePair<RectangleF, string>(r, key));
        }
        void Act(Control c, RectangleF r, Action a)
        {
            if (spotOnly) return;
            var sp = ScrollOf(c); r.Offset(sp.X, sp.Y);
            List<KeyValuePair<RectangleF, Action>> l; if (!acts.TryGetValue(c, out l)) acts[c] = l = new List<KeyValuePair<RectangleF, Action>>();
            l.Add(new KeyValuePair<RectangleF, Action>(r, a));
        }
        // keyboard: the painted actions of a page in reading order; Tab walks them, Enter presses
        readonly Dictionary<Control, int> actFocus = new Dictionary<Control, int>();
        List<KeyValuePair<RectangleF, Action>> ActsInOrder(Control c)
        {
            List<KeyValuePair<RectangleF, Action>> l; if (!acts.TryGetValue(c, out l)) return new List<KeyValuePair<RectangleF, Action>>();
            return l.Where(kv => kv.Key.Width > 0 && kv.Key.Height > 0).OrderBy(kv => (int)(kv.Key.Top / (8 * S))).ThenBy(kv => kv.Key.Left).ToList();
        }
        void WireKeys(DB db)
        {
            if (DB.Hop == null) DB.Hop = (c, k) => { var f = c.FindForm() as FullView; return f != null && !f.IsDisposed && f.SpatialMove(c, k); };   // the window the page is in - never an old, closed one
            if (db.KeyNav) return;   // lists with their own arrow keys
            Func<RectangleF> mark = () => { int i; var l = ActsInOrder(db); return actFocus.TryGetValue(db, out i) && i >= 0 && i < l.Count ? l[i].Key : RectangleF.Empty; };
            db.Mark = mark;
            db.LostFocus += delegate { actFocus.Remove(db); };   // a mark only after Tab is pressed - never on a click or at start
            db.TabStep = forward =>
            {
                var l = ActsInOrder(db); int i; if (!actFocus.TryGetValue(db, out i)) i = -1;
                i += forward ? 1 : -1; if (l.Count == 0 || i < 0 || i >= l.Count) { actFocus.Remove(db); db.Invalidate(); return false; }
                actFocus[db] = i; var r = l[i].Key;
                if (db.AutoScroll && (r.Top < 0 || r.Bottom > db.ClientSize.Height)) { var sp = db.AutoScrollPosition; db.AutoScrollPosition = new Point(-sp.X, Math.Max(0, (int)(-sp.Y + r.Top - db.ClientSize.Height / 3))); }
                // what a screen reader says: the explanation under that spot, if there is one
                List<KeyValuePair<RectangleF, string>> h; string say = "";
                if (hints.TryGetValue(db, out h)) { var c0 = new PointF(r.X + r.Width / 2, r.Y + r.Height / 2); var hit = h.LastOrDefault(kv => kv.Key.Contains(c0)); if (hit.Value != null) { var e = TipEntry(hit.Value); if (e != null) say = e.Friendly ?? ""; } }
                db.Announce(say != "" ? say : "item " + (i + 1) + " of " + l.Count);
                db.Invalidate(); return true;
            };
            db.ArrowStep = key =>
            {
                var l = ActsInOrder(db); if (l.Count == 0) return false; int i; bool had = actFocus.TryGetValue(db, out i) && i >= 0 && i < l.Count;
                int best = -1;
                if (!had)
                {   // nothing marked yet: the first one on screen
                    for (int k = 0; k < l.Count; k++) if (l[k].Key.Bottom > 0 && l[k].Key.Top < db.ClientSize.Height) { best = k; break; }
                    if (best < 0) best = 0;
                }
                else
                {
                    var cur = l[i].Key; float cx = cur.X + cur.Width / 2, cy = cur.Y + cur.Height / 2, bestScore = float.MaxValue;
                    for (int k = 0; k < l.Count; k++)
                    {
                        if (k == i) continue; var r = l[k].Key; float x = r.X + r.Width / 2, y = r.Y + r.Height / 2, dx = x - cx, dy = y - cy, along, across;
                        if (key == Keys.Right) { if (r.Left < cur.Right - 2 || dx <= 0) continue; along = dx; across = Math.Abs(dy); }
                        else if (key == Keys.Left) { if (r.Right > cur.Left + 2 || dx >= 0) continue; along = -dx; across = Math.Abs(dy); }
                        else if (key == Keys.Down) { if (r.Top < cur.Bottom - 2) continue; along = dy; across = Math.Abs(dx); }
                        else { if (r.Bottom > cur.Top + 2) continue; along = -dy; across = Math.Abs(dx); }
                        float score = along + across * 2.5f; if (score < bestScore) { bestScore = score; best = k; }
                    }
                    if (best < 0) return false;   // nothing further that way on the page: DB.Hop moves to the control beside it
                }
                actFocus[db] = best; var br = l[best].Key;
                if (db.AutoScroll && (br.Top < 0 || br.Bottom > db.ClientSize.Height)) { var sp = db.AutoScrollPosition; db.AutoScrollPosition = new Point(-sp.X, Math.Max(0, (int)(-sp.Y + br.Top - db.ClientSize.Height / 3))); }
                // the tip for the marked one, as if the mouse were there (a tool's card: what it does, you type, takes, sends)
                List<KeyValuePair<RectangleF, string>> hh; string hk = null;
                if (hints.TryGetValue(db, out hh)) { var c0 = new PointF(br.X + br.Width / 2, br.Y + br.Height / 2); var hit = hh.LastOrDefault(kv => kv.Key.Contains(c0)); if (hit.Value != null && TipEntry(hit.Value) != null) hk = hit.Value; }
                tipPoint = db.PointToScreen(new Point((int)br.Left, (int)br.Bottom - (int)(18 * S))); tipKey = null; SetTipKey(hk);
                db.Invalidate(); return true;
            };
            db.MouseDown += delegate { tipPoint = null; if (actFocus.Remove(db)) db.Invalidate(); };   // the mouse takes over from the keyboard
            db.Press = () => { var r = mark(); if (r.Width <= 0) return; var l = ActsInOrder(db); int i; if (actFocus.TryGetValue(db, out i) && i < l.Count) { UiActivity.Doing = "key on " + db.Name + " (" + tab + ")"; string usedKeyTab = tab; l[i].Value(); PageUsed(usedKeyTab); } };
        }
        void Wire(Control c)
        {
            var dbc = c as DB; if (dbc != null) WireKeys(dbc);
            c.MouseMove += delegate(object s, MouseEventArgs e)
            {
                tipPoint = null;
                List<KeyValuePair<RectangleF, Action>> l; bool click = acts.TryGetValue(c, out l) && l.Any(kv => kv.Key.Contains(e.Location));
                List<KeyValuePair<RectangleF, string>> h; string key = null;
                if (hints.TryGetValue(c, out h)) for (int i = h.Count - 1; i >= 0; i--) if (h[i].Key.Contains(e.Location)) { key = h[i].Value; tipArea = c.RectangleToScreen(Rectangle.Round(h[i].Key)); break; }
                if (key != null && TipEntry(key) == null) key = null;
                c.Cursor = click ? Cursors.Hand : key != null ? Cursors.Help : Cursors.Default;
                SetTipKey(key);
            };
            c.MouseLeave += delegate { if (!tip.Visible) SetTipKey(null); };   // an open card stays while the pointer moves onto it (the watch below closes it)
            c.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                List<KeyValuePair<RectangleF, Action>> l; if (!acts.TryGetValue(c, out l)) return;
                for (int i = l.Count - 1; i >= 0; i--) if (l[i].Key.Contains(e.Location)) { SetTipKey(null); UiActivity.Doing = "click on " + c.Name + " (" + tab + ")"; string usedTab = tab; l[i].Value(); PageUsed(usedTab); return; }   /* Adeebo Points: a page counts as used when something on it was really clicked */
            };
            c.MouseWheel += delegate(object s, MouseEventArgs e) { SetTipKey(null); if (ModifierKeys == Keys.Control && e.Delta != 0) ZoomBy(e.Delta > 0 ? 1 : -1); };   // Ctrl + wheel zooms
            c.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Right && !IsPopup(c)) PageMenu(c, e.Location); };
        }
        Point? tipPoint;   // set when the keyboard marked something: the tip opens there, not at the mouse
        void SetTipKey(string key)
        {
            if (key == tipKey) return;
            // 24 Nov (Adeeb: "it opens and closes within a second - looks like a bug"): a card opens only when the pointer rests on
            // the ? for 1.2 s, then stays while the pointer is on that spot or on the card, with a short grace when it slips off
            if (key == null && tip.Visible && tipWatch) return;   // the watch decides, with its grace
            tipKey = key; tipTimer.Stop();
            if (key == null) { if (tip.Visible) tip.Hide(); return; }
            tipTimer.Interval = tip.Visible ? 60 : 1200; tipTimer.Start();
        }
        void ShowTip()
        {
            if (IsDisposed || tipKey == null || !Visible || WindowState == FormWindowState.Minimized) return;
            var e = TipEntry(tipKey); if (e == null) return;
            var at = tipPoint ?? Cursor.Position; tip.ShowAt(e, at, this); tipOver = tipPoint != null ? WindowFromPoint(Cursor.Position) : WindowFromPoint(at);
            tipWatch = true; tipAway = 0; tipTimer.Interval = 400; tipTimer.Start();   // keep checking: a theme flip or a page change never fires MouseLeave
        }
        // V02 (r1 visual pass): the "Day or night" card stayed over What's running and Time machine - the pointer had left
        bool tipWatch; IntPtr tipOver;
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
        Rectangle tipArea; int tipAway;
        bool TipStillWanted()
        {
            var p = Cursor.Position;
            bool on = tipPoint != null ? WindowFromPoint(p) == tipOver : Rectangle.Inflate(tipArea, (int)(6 * S), (int)(6 * S)).Contains(p) || tip.Bounds.Contains(p);
            if (on) { tipAway = 0; return true; }
            return ++tipAway < 2;   // ~0.8 s off it before it closes
        }
        static Explain.Entry TipEntry(string key)
        {
            if (key.StartsWith("proc:", StringComparison.Ordinal)) return ProcEntry(key.Substring(5), null, null);
            if (key.StartsWith("hour:", StringComparison.Ordinal)) return HourEntry(key);
            var te = Explain.Term(key);
            if (te != null && key.StartsWith("tb.", StringComparison.Ordinal)) return ToolTip(key.Substring(3), te);
            return te;
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

        DateTime chgBadgeAt = DateTime.MinValue, chgBadgeSeen = DateTime.MinValue; string chgBadgeText = ""; int chgBadgeLv = -1;
        void PageBadge(string t, out string text, out int lv)
        {
            text = ""; lv = -1;
            if (t == "conn") { text = list.Shown > 0 ? list.Shown.ToString(CultureInfo.CurrentCulture) : ""; lv = list.SynSent > 0 ? 1 : -1; }
            else if (t == "procs") { int n; lock (M.L) n = M.Apps.Count; text = n > 0 ? n.ToString(CultureInfo.CurrentCulture) : ""; }
            else if (t == "changes")
            {
                // bug-hunt ML-140: worked out once every 2 seconds (and in one pass), not on every badge refresh
                if ((DateTime.UtcNow - chgBadgeAt).TotalSeconds >= 2 || chgBadgeSeen != changesSeen)
                {
                    chgBadgeAt = DateTime.UtcNow; chgBadgeSeen = changesSeen; int cnt = 0, mx = -1;
                    foreach (var e in M.Events.Recent(300)) if (e.T > changesSeen && e.Kind != "verdict") { cnt++; if (e.Lv > mx) mx = e.Lv; }
                    chgBadgeText = cnt > 0 ? cnt + " new" : ""; chgBadgeLv = cnt == 0 ? -1 : mx >= 2 ? 2 : mx == 1 ? 1 : 4;
                }
                text = chgBadgeText; lv = chgBadgeLv;
            }
            else if (t == "live") { text = live.Running ? "REC" : ""; lv = 2; }
            else if (t == "link") { var ls = linkSnap; text = ls != null && ls.Level > 0 ? "!" : ""; lv = ls != null ? ls.Level : -1; }
            else if (t == "dest") { text = pinRunning || destRunning ? "..." : ""; lv = 4; }
            else if (t == "guide") { text = guideBusy ? "..." : ""; lv = 4; }
        }
        void UpdateBadges()
        {
            bool wifi; lock (M.L) wifi = M.AdapterKind == "Wi-Fi";
            var names = wifi ? new[] { "Your Wi-Fi", "Wi-Fi" } : new[] { "Your cable", "Cable" };
            if (tabNames["link"][0] != names[0]) tabNames["link"] = names;
            foreach (Control c in subTabs.Controls)
            {
                var b = (PillButton)c; string t = (string)b.Tag, tx; int lv;
                if (t == "link" && b.Text != names[shortTabs ? 1 : 0]) b.Text = names[shortTabs ? 1 : 0];
                PageBadge(t, out tx, out lv); b.SetBadge(tx, lv);
            }
            // a section shows only what needs attention (not counts)
            foreach (Control c in tabs.Controls)
            {
                var b = c as PillButton; if (b == null) continue; var def = Secs.FirstOrDefault(x => x[0] == (string)b.Tag); if (def == null) continue; string bt = ""; int bl = -1;   /* More, Calculators, Reports and your own tabs carry no page badges */
                for (int i = 2; i < def.Length && bt == ""; i++) { if (def[i] == "conn" || def[i] == "procs") continue; PageBadge(def[i], out bt, out bl); }
                b.SetBadge(bt, bl);
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

        // ---------- actions
        void StartDest()
        {
            if (destRunning) return;
            string input = destBox.Box.Text;
            string rk = (input ?? "").Trim(); if (rk.Length > 0 && rk.Length < 120) { destRecent.Remove(rk); destRecent.Insert(0, rk); if (destRecent.Count > 6) destRecent.RemoveAt(6); }   // this session only, never saved
            dest = new DestResult(); pinResults = null; destRunning = true; checkBtn.Enabled = false; destCanvas.Invalidate();
            var r = dest;
            Task.Factory.StartNew(() =>
            { var __bz = Busy.Begin("Checking the destination"); try { 
                try { DestCheck.Run(input, r, delegate { }, M); }
                catch (Exception e) { r.Verdict = "Check failed: " + e.Message; r.Level = 1; r.Done = true; Program.Log("dest: " + e); }
                finally
                {
                    destRunning = false;
                    try { if (!IsDisposed) BeginInvoke((Action)delegate { checkBtn.Enabled = true; destCanvas.Invalidate(); }); } catch { }
                }
            } finally { __bz.End(); } }, TaskCreationOptions.LongRunning);
        }

        void StartHealth()
        {
            if (healthRunning) return;
            healthRunning = true; health = null; healthBtn.Enabled = false; healthCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            { var __bz = Busy.Begin("Running the health check"); try { 
                List<string[]> res = null;
                try { res = HealthCheck.Run(M); } catch (Exception e) { Program.Log("health: " + e); }
                try { if (!IsDisposed) BeginInvoke((Action)delegate { health = res ?? new List<string[]>(); healthAt = DateTime.Now; healthRunning = false; healthBtn.Enabled = true; healthBtn.Text = AdeeboMeter.L.T("Run again"); healthCanvas.Invalidate(); }); } catch { }
            } finally { __bz.End(); } }, TaskCreationOptions.LongRunning);
        }

        void Sniff(Conn c)
        {
            if (!npcap || sniffing || c == null) return;
            if (Policy.NoCapture) { sniffStatus = "Capturing packets is turned off by your IT team (AdeeboMeter.policy)."; UpdateConnStatus(); return; }
            if (live.Running && !live.RingActive && live.RingError != "") { sniffStatus = "The rewind buffer stopped (" + live.RingError + ") - capturing from now instead."; UpdateConnStatus(); }
            if (live.RingActive && !string.IsNullOrEmpty(c.RemoteIp) && c.RemotePort > 0)
            {
                IPAddress ip; if (!IPAddress.TryParse(c.RemoteIp, out ip)) return;
                ip = new IPAddress(ip.GetAddressBytes());   // fe80::1%12 -> fe80::1: capture filters have no zone
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
                filt = "host " + new IPAddress(rip.GetAddressBytes()) + " and port " + c.RemotePort + " and port " + c.LocalPort;
            else filt = (c.Proto == "UDP" ? "udp" : "tcp") + " port " + c.LocalPort;
            string iface; lock (M.L) iface = M.IfaceId;
            Guid gid; if (!Guid.TryParse((iface ?? "").Trim('{', '}'), out gid)) { sniffStatus = "No active adapter to capture on."; UpdateConnStatus(); return; }
            string safeApp = new string(c.Proc.Where(char.IsLetterOrDigit).Take(20).ToArray());
            string file = Program.SafeCapturePath("adebometer_" + safeApp + "_" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 4) + ".pcapng");
            var psi = new ProcessStartInfo(dumpcap, "-i \"\\Device\\NPF_{" + gid.ToString().ToUpperInvariant() + "}\" -p -f \"" + filt + "\" -a duration:" + Cfg.SniffSec + " -a filesize:200000 -w \"" + file + "\" -q");   /* 200 MB at most: a big download can't fill the disk */ psi.WorkingDirectory = Path.GetDirectoryName(dumpcap);
            psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardError = true;
            try
            {
                var p = Process.Start(psi); Job.Add(p);
                sniffing = true; sniffStatus = Voice.P("Listening in for " + Cfg.SniffSec + " s on ", "Capturing " + Cfg.SniffSec + " s: ") + filt;
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
                                Program.LastCapture = file;
                                sniffStatus = Voice.P("Got it - opening in Wireshark. (Deleted when AdeeboMeter exits - click Keep this capture to save a copy.)", "Saved; opening in Wireshark. Deleted on exit - click Keep this capture to save a copy.");
                                try { if (File.Exists(wireshark)) { var ws = new ProcessStartInfo(wireshark, "\"" + file + "\""); ws.WorkingDirectory = Path.GetDirectoryName(wireshark); ws.UseShellExecute = false; Process.Start(ws).Dispose(); } } catch (Exception ex) { sniffStatus = "Saved " + file + " (" + ex.Message + ")"; }
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
}
