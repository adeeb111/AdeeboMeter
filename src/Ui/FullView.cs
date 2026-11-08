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
        System.Threading.Timer watchdog; DateTime lastPong = DateTime.UtcNow; volatile bool pinged, frozenLogged; string frozeDoing = "";
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
        readonly Panel settingsPage, aboutPage; readonly DB settingsCanvas, aboutCanvas; readonly PillButton settingsBtn, aboutBtn, backBtn, detailsBtn, onlineChip; readonly ThemeSwitch themeSw; Panel tipBar;
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
            M = m; snap = m.Latest; Knowledge.Hold = true;
            // freeze watchdog: the window answers a ping every second; if it doesn't for 2 s, the log says what it was doing
            watchdog = new System.Threading.Timer(delegate
            {
                if (!IsHandleCreated || IsDisposed) return;
                var since = DateTime.UtcNow - lastPong;
                if (since.TotalSeconds >= 2 && !frozenLogged) { frozenLogged = true; frozeDoing = UiActivity.Doing; Program.Log("window froze (" + since.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s so far) while " + frozeDoing); }
                if (!pinged) { pinged = true; try { BeginInvoke((Action)delegate { pinged = false; if (frozenLogged) { frozenLogged = false; Program.Log("window answered again after " + (DateTime.UtcNow - lastPong).TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s (" + frozeDoing + ")"); } lastPong = DateTime.UtcNow; }); } catch (InvalidOperationException) { pinged = false; } }
            }, null, 1000, 1000);
            Text = "AdeeboMeter - " + Program.Tagline; Icon = icon; BackColor = Theme.Bg; Font = Draw.fBody;
            // Start at the size of the screen it opens on, so it fits even if maximizing is refused.
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            StartPosition = FormStartPosition.Manual; Bounds = wa; MinimumSize = new Size(Math.Min(Sc(980), wa.Width), Math.Min(Sc(640), wa.Height));
            WindowState = FormWindowState.Maximized; KeyPreview = true;
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            dumpcap = Path.Combine(pf, "Wireshark", "dumpcap.exe"); wireshark = Path.Combine(pf, "Wireshark", "Wireshark.exe");
            npcap = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Npcap", "wpcap.dll")) && File.Exists(dumpcap);

            header = new DB { Name = "header", Dock = DockStyle.Top, Height = Sc(60), BackColor = Theme.Card };
            header.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintHeader(e.Graphics, header.ClientRectangle); };
            settingsBtn = new PillButton("Settings", PillButton.K.Chip);
            settingsBtn.Click += delegate { tab = tab == "settings" ? lastPage : "settings"; ShowTab(); };
            aboutBtn = new PillButton("About", PillButton.K.Chip);
            aboutBtn.Click += delegate { tab = tab == "about" ? lastPage : "about"; ShowTab(); };
            backBtn = new PillButton("Back to corner", PillButton.K.Primary);
            backBtn.Click += delegate { Close(); };
            detailsBtn = new PillButton("Details", PillButton.K.Chip) { Selected = Cfg.Details };
            detailsBtn.Click += delegate { Cfg.Details = !Cfg.Details; detailsBtn.Selected = Cfg.Details; Cfg.Save(); Refresh(); foreach (Control c in Descendants(this)) c.Invalidate(); };
            detailsBtn.MouseEnter += delegate { SetTipKey("ov.DETAILS"); }; detailsBtn.MouseLeave += delegate { SetTipKey(null); };
            onlineChip = new PillButton("Online lookups on", PillButton.K.Chip) { Visible = false };
            onlineChip.Click += delegate { Online.Stop(); SyncOnlineChip(); };
            onlineChip.MouseEnter += delegate { SetTipKey("ov.ONLINE"); }; onlineChip.MouseLeave += delegate { SetTipKey(null); };
            themeSw = new ThemeSwitch();
            themeSw.MouseEnter += delegate { SetTipKey("theme.toggle"); }; themeSw.MouseLeave += delegate { SetTipKey(null); };
            header.Controls.Add(themeSw); header.Controls.Add(onlineChip); header.Controls.Add(detailsBtn); header.Controls.Add(aboutBtn); header.Controls.Add(settingsBtn); header.Controls.Add(backBtn);
            EventHandler place = delegate
            {
                int xr = header.Width - Sc(20);
                foreach (var b in new Control[] { backBtn, settingsBtn, aboutBtn, detailsBtn, onlineChip, themeSw }) { if (!b.Visible && b == onlineChip) continue; xr -= b.Width; b.Location = new Point(xr, (header.Height - b.Height) / 2); xr -= Sc(b == detailsBtn ? 14 : 8); }
            };
            header.Resize += place; detailsBtn.Resize += place; onlineChip.Resize += place; onlineChip.VisibleChanged += place; settingsBtn.Resize += place; aboutBtn.Resize += place; backBtn.Resize += place;
            pathPanel = new DB { Name = "pathPanel", Dock = DockStyle.Top, Height = Sc(128), BackColor = Theme.Card };
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
            footer = new DB { Name = "footer", Dock = DockStyle.Bottom, Height = Sc(30), BackColor = Theme.Card };
            footer.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintFooter(e.Graphics, footer.ClientRectangle); };

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(Sc(14)), BackColor = Theme.Bg, Tag = "bg" };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Sc(390)));
            body.Resize += delegate { float sw = Math.Max(Sc(300), Math.Min(Sc(390), body.Width * 0.30f)); if (Math.Abs(body.ColumnStyles[1].Width - sw) > 1) body.ColumnStyles[1].Width = sw; };

            var left = leftPane = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Margin = new Padding(0, 0, Sc(14), 0) };
            tabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(48), BackColor = Theme.Card, Padding = new Padding(Sc(10), Sc(4), 0, 0), WrapContents = false };
            // six sections, each with its own pages underneath (room to grow without the tab row falling off the edge)
            foreach (var sec in Secs)
            {
                var b = new PillButton(sec[1], PillButton.K.Tab) { Tag = sec[0] };
                b.Click += delegate(object s, EventArgs e) { string id = (string)((Control)s).Tag; string pg; tab = lastIn.TryGetValue(id, out pg) ? pg : Secs.First(x => x[0] == id)[2]; ShowTab(); };
                tabs.Controls.Add(b);
            }
            subTabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(44), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(6), 0, 0), WrapContents = false, Visible = false };
            var tabLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Track, Tag = "line" };
            // Small screens / high scaling: switch to short tab names instead of letting tabs fall off the edge.
            subTabs.Resize += delegate { FitTabs(); };

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
            healthCanvas = new DB { Name = "healthCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            healthCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintHealth(e.Graphics); }; Wire(healthCanvas);
            healthPage.Controls.Add(healthCanvas); healthPage.Controls.Add(hb);

            // Live capture page
            livePage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var lb = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            liveBtn = new PillButton("Start live capture", PillButton.K.Primary); liveBtn.Click += delegate { ToggleLive(); };
            lb.Controls.Add(liveBtn);
            lb.Controls.Add(new Label { Text = LiveCapture.Available ? "Wireshark's tshark decodes your PC's own traffic. Off until you press Start; stops when you leave this tab, close the view, or after " + Cfg.LiveMin + " min (Settings)." : "Needs Wireshark (tshark) and Npcap - not found on this PC.", AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fSmall, Margin = new Padding(Sc(10), Sc(10), 0, 0) });
            liveBtn.Enabled = LiveCapture.Available;
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
            procInfo = new DB { Name = "procInfo", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            procInfo.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintProcInfo(e.Graphics); }; Wire(procInfo);
            split.Controls.Add(procList, 0, 0); split.Controls.Add(procInfo, 1, 0);
            procPage.Controls.Add(split); procPage.Controls.Add(pbar);

            // Settings page
            settingsPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            settingsCanvas = new DB { Name = "settingsCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            settingsCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintSettings(e.Graphics); }; Wire(settingsCanvas);
            settingsPage.Controls.Add(settingsCanvas);

            // About page
            aboutPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            aboutCanvas = new DB { Name = "aboutCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            aboutCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintAbout(e.Graphics); }; Wire(aboutCanvas);
            aboutPage.Controls.Add(aboutCanvas);

            left.Controls.Add(BuildOverviewPage()); left.Controls.Add(BuildToolboxPage()); left.Controls.Add(BuildGuidePage()); left.Controls.Add(BuildReplayPage());
            left.Controls.Add(BuildTravelPage()); left.Controls.Add(BuildInsidePage()); left.Controls.Add(BuildLearnPage());
            left.Controls.Add(connPage); left.Controls.Add(linkPage); left.Controls.Add(procPage); left.Controls.Add(settingsPage); left.Controls.Add(aboutPage); left.Controls.Add(destPage); left.Controls.Add(healthPage); left.Controls.Add(changesPage); left.Controls.Add(livePage); left.Controls.Add(tabLine); left.Controls.Add(subTabs); left.Controls.Add(tabs);
            side = new DB { Name = "side", Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0), Tag = "bg" };
            side.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintSide(e.Graphics, side.ClientRectangle); }; Wire(side);
            body.Controls.Add(left, 0, 0); body.Controls.Add(side, 1, 0);
            Controls.Add(body); Controls.Add(footer); Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Line, Tag = "line" }); Controls.Add(pathPanel); Controls.Add(header);
            ShowTab();

            timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += delegate { OnTimer(); };
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
                else if (e.KeyCode == Keys.K && e.Control) { e.SuppressKeyPress = true; OpenPalette(); }
            };
            Shown += delegate { timer.Start(); RefreshConns(true); };
            tipTimer.Tick += delegate { tipTimer.Stop(); ShowTip(); };
            // Minimising means "get out of my way": same as Esc / Back to corner - the view closes, frees its memory, the widget keeps watching.
            Resize += delegate { if (WindowState == FormWindowState.Minimized && Visible) BeginInvoke((Action)Close); };
            if (!Cfg.SeenTip) BuildTipBar();
            Deactivate += delegate { SetTipKey(null); };
            FormClosed += delegate
            {
                if (histBmp != null) { histBmp.Dispose(); histBmp = null; }
                if (weekBmp != null) { weekBmp.Dispose(); weekBmp = null; }
                // WinForms makes a small copy of the window icon for each window; resetting the icon releases it now, not "someday".
                try { ShowIcon = false; Icon = null; } catch { }
                Knowledge.Hold = false; if (watchdog != null) watchdog.Dispose(); M.ProcsTab = false; timer.Stop(); timer.Dispose(); tipTimer.Stop(); tipTimer.Dispose();
                try { if (tip.IsHandleCreated) tip.Close(); } catch { } tip.Dispose(); live.Stop(); list.ReleaseAll(); procs = new List<AppStat>(); pinResults = null;
                foreach (var l in subBtns.Values) foreach (var c in l) if (!c.IsDisposed) c.Dispose(); subBtns.Clear(); connSnap = new List<Conn>(); treeRows = null; treeBy = null; svcs = null; startups = null; realPkts = null; realDecode = null; chatPkts = null; learnPkts = null; m365Rows = null; routeTable = null; travelDest = null; guideRun = null; replayDay = null; if (tbRun0 != null) tbRun0.Stop = true; mtuHops = null; mtrHops = null; tbRows = null; if (replayBmp != null) { replayBmp.Dispose(); replayBmp = null; } };
            Load += delegate { ApplyTheme(); };
        }

        // the header chip that shows online lookups are on, with the minutes left; one click turns them off
        void SyncOnlineChip()
        {
            bool on = Online.On; string t = on ? "Online · " + Math.Max(1, (int)Math.Ceiling(Online.Left.TotalMinutes)) + " min left" : "";
            if (onlineChip.Visible != on) onlineChip.Visible = on;
            if (on && onlineChip.Text != t) onlineChip.Text = t;
        }
        static System.Collections.Generic.IEnumerable<Control> Descendants(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in Descendants(c)) yield return d; } }
        static void Prep(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; }

        void ShowTab()
        {
            UiActivity.Doing = "switching to " + tab;
            // one layout pass for the whole switch, not one per page shown or hidden
            if (leftPane != null) leftPane.SuspendLayout();
            try { ShowTab2(); } finally { if (leftPane != null) leftPane.ResumeLayout(true); }
        }
        void ShowTab2()
        {
            connPage.Visible = tab == "conn"; destPage.Visible = tab == "dest"; healthPage.Visible = tab == "health"; livePage.Visible = tab == "live"; changesPage.Visible = tab == "changes";
            procPage.Visible = tab == "procs"; settingsPage.Visible = tab == "settings"; settingsBtn.Selected = tab == "settings"; M.ProcsTab = tab == "procs";
            aboutPage.Visible = tab == "about"; aboutBtn.Selected = tab == "about";
            linkPage.Visible = tab == "link"; if (tab == "link") RefreshLink(true);
            travelPage.Visible = tab == "travel"; insidePage.Visible = tab == "windows"; learnPage.Visible = tab == "learn";
            overviewPage.Visible = tab == "overview"; guidePage.Visible = tab == "guide"; replayPage.Visible = tab == "replay"; if (tab == "toolbox") EnsureToolbox(); toolboxPage.Visible = tab == "toolbox";
            if (tab == "replay") ReplayOpen();
            if (tab != "settings" && tab != "about") lastPage = tab;
            string sec = SecOf(tab); if (sec != "") lastIn[sec] = tab;
            if (sec != curSec) { curSec = sec; BuildSubTabs(sec); }
            if (tab == "windows" && treeRows == null) LoadInside();
            if (tab == "changes") changesSeen = DateTime.Now;
            if (tab == "procs") RefreshProcs();
            SetTipKey(null); ThemeScrollbars(leftPane ?? (Control)this);
            if (tab != "live" && live.Running) { live.Stop(); liveBtn.Text = "Start live capture"; liveView = null; }
            foreach (Control c in tabs.Controls) ((PillButton)c).Selected = (string)c.Tag == sec;
            foreach (Control c in subTabs.Controls) ((PillButton)c).Selected = (string)c.Tag == tab;
            if (tab == "dest") destBox.Box.Focus();
        }
        // sections: id, name, then its pages in order (the first is where the section opens the first time)
        static readonly string[][] Secs = {
            new[] { "overview", "Overview", "overview" },
            new[] { "monitor", "Monitor", "conn", "link", "procs", "live" },
            new[] { "investigate", "Investigate", "guide", "dest", "health" },
            new[] { "toolbox", "Toolbox", "toolbox" },
            new[] { "story", "Network story", "travel" },
            new[] { "time", "Time machine", "replay", "changes" },
            new[] { "learn", "Learn", "learn", "windows" } };
        static string SecOf(string page) { foreach (var sc in Secs) for (int i = 2; i < sc.Length; i++) if (sc[i] == page) return sc[0]; return ""; }
        static readonly Dictionary<string, string[]> tabNames = new Dictionary<string, string[]> {
            { "overview", new[] { "Overview", "Overview" } },
            { "conn", new[] { "Connections", "Connections" } }, { "link", new[] { "Your Wi-Fi", "Wi-Fi" } }, { "procs", new[] { "What's running", "Running" } }, { "live", new[] { "Live capture", "Live" } },
            { "guide", new[] { "Investigate a problem", "Problem" } }, { "dest", new[] { "Check a destination", "Check" } }, { "health", new[] { "Health", "Health" } }, { "toolbox", new[] { "Toolbox", "Toolbox" } },
            { "travel", new[] { "Network story", "Story" } },
            { "replay", new[] { "Replay", "Replay" } }, { "changes", new[] { "What changed", "Changes" } },
            { "learn", new[] { "Topics", "Topics" } }, { "windows", new[] { "Inside Windows", "Windows" } } };
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
                        var b = new PillButton(tabNames[def[i]][shortTabs ? 1 : 0], PillButton.K.Chip) { Tag = def[i], Margin = new Padding(0, 0, Sc(6), 0) };
                        b.Click += delegate(object s, EventArgs e) { tab = (string)((Control)s).Tag; ShowTab(); };
                        btns.Add(b);
                    }
            }
            subTabs.Controls.AddRange(btns.ToArray());
            foreach (var c in btns) { string[] n; if (tabNames.TryGetValue((string)c.Tag, out n)) c.Text = n[shortTabs ? 1 : 0]; }
            subTabs.Visible = subTabs.Controls.Count > 0;
            subTabs.ResumeLayout(); UpdateBadges();
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

        void OnTimer()
        {
            UiActivity.Doing = "timer on " + tab; var tsw = Stopwatch.StartNew(); try { OnTimer2(); } finally { if (tsw.ElapsedMilliseconds > 60) Program.Log("slow tick: " + tab + " " + tsw.ElapsedMilliseconds + " ms"); }
        }
        void OnTimer2()
        {
            if (M.Version != lastVersion) { lastVersion = M.Version; snap = M.Latest; header.Invalidate(); pathPanel.Invalidate(); side.Invalidate(); if (tab == "changes") changesCanvas.Invalidate(); if (tab == "overview") OverviewTick(); }
            if ((DateTime.UtcNow - lastConns).TotalSeconds >= (tab == "conn" || tab == "travel" || tab == "procs" ? 2 : 10)) RefreshConns(false);   // the full connection list only refreshes fast where it is shown
            if (++footTick % 2 == 0) { SelfStats(); footer.Invalidate(); }
            if (destRunning || pinRunning) destCanvas.Invalidate();
            if (tbBusy && tab == "toolbox" && tbCanvas != null) tbCanvas.Invalidate();
            Online.Tick(); if (onlineChip != null) SyncOnlineChip();
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
                try { if (!IsDisposed) BeginInvoke((Action)delegate { refreshing = false; if (l != null && !IsDisposed) { list.Merge(l); connSnap = l; if (tab == "travel" && storyTarget == "") travelCanvas.Invalidate(); } }); else refreshing = false; } catch { refreshing = false; }
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
            if (page == "guide" && destIn != null) StartGuide(destIn, false);
            if (page == "travel" && destIn != null) { travelBox.Box.Text = destIn; StorySite(); }
            if (page == "toolbox" && destIn != null) { var parts = destIn.Split('|'); SelectTool(parts[0]); for (int i = 1; i < parts.Length && i - 1 < tbBoxes.Count; i++) if (tbBoxes[i - 1] != null) tbBoxes[i - 1].Box.Text = parts[i]; StartTool(); }
            if (page == "learn" && destIn != null) { learnSel = "topic:" + destIn; learnInfo.Invalidate(); learnList.Invalidate(); }
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
        public string Perf()
        {
            var sb = new StringBuilder(); var sw = new Stopwatch();
            var pages = new[] { "overview", "conn", "link", "procs", "live", "guide", "dest", "health", "toolbox", "travel", "replay", "changes", "learn", "windows", "settings", "about" };
            var canvas = new Dictionary<string, Control> { { "overview", overviewCanvas }, { "conn", list }, { "link", linkCanvas }, { "procs", procList }, { "live", liveCanvas }, { "guide", guideCanvas }, { "dest", destCanvas }, { "health", healthCanvas }, { "toolbox", toolboxPage },
                { "travel", travelCanvas }, { "replay", replayCanvas }, { "changes", changesCanvas }, { "learn", learnList }, { "windows", insideList }, { "settings", settingsCanvas }, { "about", aboutCanvas } };
            sb.AppendLine("window " + Width + "x" + Height);
            sb.AppendLine("page       first  again  (show+paint)  page-paint  scroll-step  tick   ms");
            var first = new Dictionary<string, long>();
            foreach (var t in pages) { sw.Restart(); tab = t; ShowTab(); Refresh(); Application.DoEvents(); first[t] = sw.ElapsedMilliseconds; for (int i = 0; i < 2; i++) { Thread.Sleep(150); Application.DoEvents(); } }
            foreach (var t in pages)
            {
                tab = "about"; ShowTab(); Refresh(); Application.DoEvents();
                sw.Restart(); tab = t; ShowTab(); long show = sw.ElapsedMilliseconds; Refresh(); long again = sw.ElapsedMilliseconds;
                var c = canvas[t]; sw.Restart(); for (int i = 0; i < 5; i++) c.Refresh(); long pp = sw.ElapsedMilliseconds / 5;
                long scr = -1; var sc = c as ScrollableControl;
                if (sc != null && sc.AutoScroll && sc.AutoScrollMinSize.Height > sc.ClientSize.Height + 200)
                { sw.Restart(); for (int i = 1; i <= 5; i++) { sc.AutoScrollPosition = new Point(0, i * 60); sc.Update(); } scr = sw.ElapsedMilliseconds / 5; sc.AutoScrollPosition = Point.Empty; }
                sw.Restart(); for (int i = 0; i < 5; i++) { OnTimer2(); Application.DoEvents(); } long tick = sw.ElapsedMilliseconds / 5;
                Func<long, string> P = v => (v < 0 ? "-" : v.ToString(CultureInfo.InvariantCulture)).PadLeft(5);
                sb.AppendLine(t.PadRight(10) + P(first[t]) + "  " + P(again) + "  (" + P(show) + ")      " + P(pp) + "       " + P(scr) + "     " + P(tick));
            }
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
            foreach (var t in new[] { "overview", "conn", "link", "procs", "live", "guide", "dest", "health", "toolbox", "travel", "replay", "changes", "learn", "windows", "settings", "about" }) { tab = t; ShowTab(); Refresh(); Application.DoEvents(); n++; }
            List<AppStat> l; lock (M.L) l = M.Apps;
            if (l.Count > 0) { tab = "procs"; ShowTab(); SelectProc(l[0]); Thread.Sleep(50); Application.DoEvents(); procInfo.Refresh(); }
            SetTipKey("path.DNS"); ShowTip(); Application.DoEvents(); SetTipKey(null);
            foreach (var w in Investigate.All) { StartGuide(w.Id, true); Application.DoEvents(); n++; }
            ReplayAt(0.5); guideCanvas.Refresh(); replayCanvas.Refresh();
            tab = "overview"; ShowTab();
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
                for (int i = l.Count - 1; i >= 0; i--) if (l[i].Key.Contains(e.Location)) { SetTipKey(null); UiActivity.Doing = "click on " + c.Name + " (" + tab + ")"; l[i].Value(); return; }
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

        void PageBadge(string t, out string text, out int lv)
        {
            text = ""; lv = -1;
            if (t == "conn") { text = list.Shown > 0 ? list.Shown.ToString(CultureInfo.CurrentCulture) : ""; lv = list.SynSent > 0 ? 1 : -1; }
            else if (t == "procs") { int n; lock (M.L) n = M.Apps.Count; text = n > 0 ? n.ToString(CultureInfo.CurrentCulture) : ""; }
            else if (t == "changes")
            {
                var fresh = M.Events.Recent(300).Where(e => e.T > changesSeen && e.Kind != "verdict").ToList();
                text = fresh.Count > 0 ? fresh.Count + " new" : ""; lv = fresh.Count == 0 ? -1 : fresh.Max(e => e.Lv) >= 2 ? 2 : fresh.Max(e => e.Lv) == 1 ? 1 : 4;
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
                var b = (PillButton)c; var def = Secs.First(x => x[0] == (string)b.Tag); string bt = ""; int bl = -1;
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
                try { if (!IsDisposed) BeginInvoke((Action)delegate { health = res ?? new List<string[]>(); healthAt = DateTime.Now; healthRunning = false; healthBtn.Enabled = true; healthBtn.Text = "Run again"; healthCanvas.Invalidate(); }); } catch { }
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
            var psi = new ProcessStartInfo(dumpcap, "-i \"\\Device\\NPF_{" + gid.ToString().ToUpperInvariant() + "}\" -f \"" + filt + "\" -a duration:" + Cfg.SniffSec + " -w \"" + file + "\" -q"); psi.WorkingDirectory = Path.GetDirectoryName(dumpcap);
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
