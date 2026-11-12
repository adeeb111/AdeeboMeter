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
    // A window drawn with per-pixel alpha that never takes the mouse (clicks go to whatever is below).
    sealed class LayeredForm : Form
    {
        public LayeredForm() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true; }
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x08000000 | 0x8; return cp; }
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        // never in a screen share or recording: the hover card lists your apps by name (Windows 10 2004+; older ignore it)
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); try { if (!Native.SetWindowDisplayAffinity(Handle, 0x11 /*WDA_EXCLUDEFROMCAPTURE*/)) Program.Log("widget: capture exclusion not available"); } catch (EntryPointNotFoundException) { } }
        public Rectangle Placed { get; private set; }
        public static volatile bool HoldTop;   // the tray menu is open: don't climb over it
        DateTime failLogged;
        public void Present(Bitmap bmp, byte alpha, int x, int y)
        {
            IntPtr screen = Native.GetDC(IntPtr.Zero), mem = Native.CreateCompatibleDC(screen), hb = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hb = bmp.GetHbitmap(Color.FromArgb(0)); old = Native.SelectObject(mem, hb);
                var size = new Native.SIZE(bmp.Width, bmp.Height); var src = new Native.POINT(0, 0); var dst = new Native.POINT(x, y);
                var bf = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
                if (!Native.UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref bf, 2))
                {
                    // after sleep or a screen change the call can fail; say so (once a minute) and let the next tick try again
                    if ((DateTime.UtcNow - failLogged).TotalSeconds > 60) { failLogged = DateTime.UtcNow; Program.Log("widget draw failed: error " + System.Runtime.InteropServices.Marshal.GetLastWin32Error()); }
                    Placed = Rectangle.Empty; return;
                }
                Placed = new Rectangle(x, y, bmp.Width, bmp.Height);
                // Windows can drop a small always-on-top window out of the top layer (sleep, unlock, monitors reconnecting) -
                // then a maximised window hides it. Claim the top again, without taking focus.
                if (alpha > 0 && !HoldTop) Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
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
    // what the window was doing last - read by the freeze watchdog so "it hangs" comes with a cause in the log
    static class UiActivity { public static volatile string Doing = ""; }
    sealed class DB : Panel
    {
        public DB() { DoubleBuffered = true; ResizeRedraw = true; }
        // KeyNav: the panel takes focus (click or Tab) and gets the arrow keys - for owner-drawn lists
        bool keyNav;
        public bool KeyNav { get { return keyNav; } set { keyNav = value; SetStyle(ControlStyles.Selectable, value); TabStop = value; } }
        protected override bool IsInputKey(Keys k) { var c = k & Keys.KeyCode; return keyNav && (c == Keys.Up || c == Keys.Down || c == Keys.PageUp || c == Keys.PageDown || c == Keys.Home || c == Keys.End || c == Keys.Enter) || base.IsInputKey(k); }
        protected override void OnMouseDown(MouseEventArgs e) { if (keyNav && !Focused) Focus(); base.OnMouseDown(e); }
        // Painted buttons and links reached from the keyboard: Tab / Shift+Tab step through them (then on to the next control),
        // Enter or Space presses the marked one. FullView supplies the steps; the mark is drawn after the page paints.
        Func<bool, bool> tabStep; public Action Press; public Func<RectangleF> Mark;
        public Func<bool, bool> TabStep { get { return tabStep; } set { tabStep = value; if (value != null && !keyNav) { SetStyle(ControlStyles.Selectable, true); TabStop = true; } } }
        protected override bool ProcessDialogKey(Keys k)
        {
            var c = k & Keys.KeyCode; var mods = k & Keys.Modifiers;
            if (Focused && tabStep != null && c == Keys.Tab && (mods & (Keys.Control | Keys.Alt)) == 0 && tabStep((mods & Keys.Shift) == 0)) return true;
            if (Focused && Press != null && (c == Keys.Enter || c == Keys.Space) && mods == 0) { Press(); return true; }
            return base.ProcessDialogKey(k);
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        public void Announce(string name) { AccessibleName = name; AccessibilityNotifyClients(AccessibleEvents.NameChange, -1); AccessibilityNotifyClients(AccessibleEvents.Focus, -1); }
        // a paint slower than 40 ms is written to the log (at most once a minute per panel), so "it feels laggy" has numbers
        static readonly System.Collections.Generic.Dictionary<string, DateTime> slowAt = new System.Collections.Generic.Dictionary<string, DateTime>();
        protected override void OnPaint(PaintEventArgs e)
        {
            UiActivity.Doing = "drawing " + Name; var sw = System.Diagnostics.Stopwatch.StartNew(); base.OnPaint(e); sw.Stop();
            if (Focused && ShowFocusCues && Mark != null) { var r = Mark(); if (r.Width > 0) { e.Graphics.ResetTransform(); ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(Rectangle.Round(r), 2, 2)); } }
            if (sw.ElapsedMilliseconds > 40)
            {
                DateTime t; string n = Name ?? "";
                if (!slowAt.TryGetValue(n, out t) || (DateTime.UtcNow - t).TotalSeconds > 60) { slowAt[n] = DateTime.UtcNow; Program.Log("slow paint: " + n + " " + sw.ElapsedMilliseconds + " ms"); }
            }
        }
    }

    // Rounded, owner-drawn button: Primary (blue), Chip (filter / toggle) or Tab. Keyboard: Tab to focus, Enter / Space to press.
    sealed class PillButton : Control
    {
        public enum K { Primary, Chip, Tab }
        public readonly K Kind; bool sel, hot, down;
        public bool Selected { get { return sel; } set { if (sel != value) { sel = value; AccessibleDescription = value ? "selected" : null; AccessibilityNotifyClients(AccessibleEvents.StateChange, -1); Invalidate(); } } }
        string badge = ""; int badgeLv = -1;
        // A small count or flag at the end of a tab ("16", "3 new", "REC"). lv: -1 neutral, 0-2 green/amber/red, 4 blue.
        public void SetBadge(string b, int lv) { b = b ?? ""; if (b == badge && lv == badgeLv) return; badge = b; badgeLv = lv; Fit(); Invalidate(); }
        int BadgeW { get { return badge == "" ? 0 : TextRenderer.MeasureText(badge, Draw.fTiny).Width + (int)(16 * Draw.S); } }
        void Fit() { var sz = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPrefix); Width = sz.Width + (int)((Kind == K.Chip ? 22 : 30) * Draw.S) + BadgeW; }
        public override string Text { get { return base.Text; } set { base.Text = L.T(value); } }   // every caption goes through the language table
        public PillButton(string text, K kind)
        {
            Kind = kind; Text = text;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            Font = kind == K.Tab ? Draw.fBodyB : kind == K.Primary ? Draw.fBodyB : Draw.fBody;
            Cursor = Cursors.Hand; TabStop = true; BackColor = Theme.Card;
            var sz = TextRenderer.MeasureText(Text, Font);   // the translated caption, not the English
            float S = Draw.S;
            Size = new Size(sz.Width + (int)((kind == K.Chip ? 22 : 30) * S), (int)((kind == K.Tab ? 42 : kind == K.Primary ? 36 : 30) * S));
            Margin = new Padding(0, 0, (int)((kind == K.Tab ? 2 : 6) * S), 0);
            AccessibleRole = AccessibleRole.PushButton; AccessibleName = Text;
        }
        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { held = false; Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k); }
        // a held key repeats KeyDown: press once per press, not once per repeat (a held Enter mustn't start and stop a capture 30 times)
        bool held;
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; if (!held) { held = true; OnClick(EventArgs.Empty); } } base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { held = false; base.OnKeyUp(e); }
        protected override void OnTextChanged(EventArgs e) { Fit(); Invalidate(); AccessibleName = Text; AccessibilityNotifyClients(AccessibleEvents.NameChange, -1); base.OnTextChanged(e); }   // "Start live capture" -> "Stop" is announced
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float S = Draw.S; var r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
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
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, Kind == K.Tab ? -(int)(1 * S) : 0, Width - bw, Height), fg, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
        public readonly TextBox Box = new TextBox(); readonly Label cueLbl;
        public string Cue { get { return cueLbl.Text; } set { cueLbl.Text = value; if (string.IsNullOrEmpty(Box.AccessibleName) || Box.AccessibleName == lastCueName) { Box.AccessibleName = value; lastCueName = value; } } }   // a screen reader says the hint, not just "edit"
        string lastCueName;
        // Label: a name drawn above the box that stays while you type (the cue inside disappears)
        string label = "";
        public string Label
        {
            get { return label; }
            set
            {
                bool had = label != ""; label = value ?? ""; float S = Draw.S; int lh = label != "" ? (int)(18 * S) : 0;
                if (had != (label != "")) { Height = (int)(36 * S) + lh; Padding = new Padding((int)(12 * S), Math.Max(2, (int)((36 * S - Box.PreferredHeight) / 2) + 1) + lh, (int)(12 * S), (int)(3 * S)); }
                Invalidate();
            }
        }
        public RoundBox(int width, Font font, string cue)
        {
            cue = L.T(cue);
            float S = Draw.S;
            DoubleBuffered = true; BackColor = Theme.Card;
            Size = new Size(width, (int)(36 * S)); Margin = new Padding(0, 0, (int)(10 * S), 0);
            Box.BorderStyle = BorderStyle.None; Box.Font = font;
            Padding = new Padding((int)(12 * S), Math.Max(2, (int)((36 * S - Box.PreferredHeight) / 2) + 1), (int)(12 * S), (int)(3 * S));   // a bottom gap: the hint label must never paint over the border
            Box.Dock = DockStyle.Fill; Box.BackColor = Theme.Card; Box.ForeColor = Theme.Ink;
            Box.GotFocus += delegate { Invalidate(); }; Box.LostFocus += delegate { Invalidate(); };
            cueLbl = new Label { Text = cue, Dock = DockStyle.Fill, Font = font, ForeColor = Theme.Muted, BackColor = Theme.Card, AutoSize = false, AutoEllipsis = true, Cursor = Cursors.IBeam, UseMnemonic = false };   // one line, never half a second line
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
            float lh = label != "" ? 18 * Draw.S : 0;
            if (lh > 0) using (var lb = new SolidBrush(Theme.Sub)) using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter }) g.DrawString(label, Draw.fSmall, lb, new RectangleF(2, 0, Width - 4, lh), nf);
            var r = new RectangleF(0.5f, 0.5f + lh, Width - 2, Height - 2 - lh);
            Theme.Fill(g, r, 8 * Draw.S, Theme.Card);
            Theme.Stroke(g, r, 8 * Draw.S, Box.Focused ? Theme.Blue : Theme.Line, Box.Focused ? 2 : 1);
        }
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
        public event Action<Conn> SniffRequested, JourneyRequested;
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
            // badge, app, proto, remote, state, seen, journey + sniff
            int fixedW = (int)((34 + 62 + 116 + 64 + 140 + 6 * 8) * S), flex = Math.Max((int)(240 * S), w - fixedW - sb.Width - (int)(28 * S));
            return new[] { (int)(34 * S), (int)(flex * 0.3), (int)(62 * S), (int)(flex * 0.7), (int)(116 * S), (int)(64 * S), (int)(140 * S) };
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
                    bool jn = Journeyable(c); var jr = new RectangleF(x, cy - 13 * S, 66 * S, 26 * S);
                    Theme.Fill(g, jr, 6 * S, jn ? Theme.Soft : Theme.Line2); Theme.Stroke(g, jr, 6 * S, jn ? Theme.Blue : Theme.Line2, 1);
                    using (var b = new SolidBrush(jn ? (Theme.Dark ? Theme.BlueDark : Theme.Blue) : Theme.Muted)) g.DrawString(L.T("Journey"), Draw.fSmall, b, jr, center);
                    x += 72 * S;
                    var br = new RectangleF(x, cy - 13 * S, 58 * S, 26 * S);
                    Theme.Fill(g, br, 6 * S, can ? Theme.Blue : Theme.Line2);
                    using (var b = new SolidBrush(can ? Color.White : Theme.Muted)) g.DrawString(L.T("Sniff"), Draw.fSmall, b, br, center);
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
        bool OnSniffButton(int x) { var cols = Cols(Width); float sx = 14 * S; for (int i = 0; i < 6; i++) sx += cols[i] + 8 * S; sx += 72 * S; return x >= sx && x <= sx + 58 * S; }
        bool OnJourneyButton(int x) { var cols = Cols(Width); float sx = 14 * S; for (int i = 0; i < 6; i++) sx += cols[i] + 8 * S; return x >= sx && x <= sx + 66 * S; }
        // the packet journey needs a conversation: a remote address and port
        // (a UDP socket has no fixed far end: the journey finds it from the packets)
        static bool Journeyable(Conn c) { return c.State != "LISTEN" && ((!string.IsNullOrEmpty(c.RemoteIp) && c.RemotePort > 0) || (c.Proto.StartsWith("UDP", StringComparison.Ordinal) && c.LocalPort > 0)); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = RowAt(e.Y); if (h != hover) { hover = h; Invalidate(); }
            string k = KeyAt(e.Location);
            Cursor = h >= 0 && (OnSniffButton(e.X) || OnJourneyButton(e.X)) ? Cursors.Hand : k != null ? Cursors.Help : Cursors.Default;
            if (k != lastKey) { lastKey = k; if (HoverKey != null) HoverKey(k); }
        }
        protected override void OnMouseLeave(EventArgs e) { if (hover != -1) { hover = -1; Invalidate(); } lastKey = null; if (HoverKey != null) HoverKey(null); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus(); int i = RowAt(e.Y); if (i < 0) return;
            selKey = view[i].C.Key; Invalidate();
            if (OnSniffButton(e.X) && CanSniff && view[i].C.State != "BOUND" && SniffRequested != null) SniffRequested(view[i].C);
            else if (OnJourneyButton(e.X) && Journeyable(view[i].C) && JourneyRequested != null) JourneyRequested(view[i].C);
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e) { int i = RowAt(e.Y); if (i >= 0 && CanSniff && view[i].C.State != "BOUND" && SniffRequested != null) SniffRequested(view[i].C); }
        // the keyboard: Up / Down / Page / Home / End move the selection (and scroll to it), Enter sniffs that flow
        protected override bool IsInputKey(Keys k) { var c = k & Keys.KeyCode; return c == Keys.Up || c == Keys.Down || c == Keys.PageUp || c == Keys.PageDown || c == Keys.Home || c == Keys.End || c == Keys.Enter || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e); if (view.Count == 0) return;
            int cur = view.FindIndex(r => r.C.Key == selKey), page = Math.Max(1, (Height - HeadH) / RowH - 1), n = cur;
            switch (e.KeyCode)
            {
                case Keys.Up: n = cur < 0 ? 0 : cur - 1; break;
                case Keys.Down: n = cur + 1; break;
                case Keys.PageUp: n = cur - page; break;
                case Keys.PageDown: n = cur + page; break;
                case Keys.Home: n = 0; break;
                case Keys.End: n = view.Count - 1; break;
                case Keys.Enter: if (cur >= 0 && CanSniff && view[cur].C.State != "BOUND" && SniffRequested != null) SniffRequested(view[cur].C); e.Handled = true; return;
                default: return;
            }
            n = Math.Max(0, Math.Min(view.Count - 1, n)); selKey = view[n].C.Key; e.Handled = true;
            int top = n * RowH, max = Math.Max(0, sb.Maximum - sb.LargeChange + 1);
            if (top < sb.Value) sb.Value = Math.Min(max, top); else if (top + RowH > sb.Value + Height - HeadH) sb.Value = Math.Max(0, Math.Min(max, top + RowH - (Height - HeadH)));
            AccessibilityNotifyClients(AccessibleEvents.Selection, n);
            Invalidate();
        }
        int wheelRest;
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int max = Math.Max(0, sb.Maximum - sb.LargeChange + 1);
            wheelRest += e.Delta * RowH * 3; int px = wheelRest / 120; wheelRest -= px * 120;   /* touchpads send small steps: keep the remainder */
            sb.Value = Math.Max(0, Math.Min(max, sb.Value - px)); Invalidate();
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
            // not on a keyboard where AltGr+A types a letter: the hotkey would eat that letter in every app
            Ok = !Native.AltGrTypes(Keys.A) && Native.RegisterHotKey(Handle, 1, 0x0001 /*ALT*/ | 0x0002 /*CTRL*/ | 0x4000 /*NOREPEAT*/, (uint)Keys.A);
        }
        protected override void WndProc(ref Message m) { if (m.Msg == 0x0312) { try { press(); } catch { } } base.WndProc(ref m); }
        public void Dispose() { if (Handle != IntPtr.Zero) { Native.UnregisterHotKey(Handle, 1); DestroyHandle(); } }
    }

    // Day / night switch in the header: a sun and a moon on a little track. Click flips it; Settings keeps "Follow Windows".
    // A labelled on/off slider ("Details  ( o)"): click, Space or Enter flips it; the knob slides in a few frames.
    sealed class SlideSwitch : Control
    {
        protected override AccessibleObject CreateAccessibilityInstance() { return new SwitchAccess(this, () => on); }
        bool on; float knob; readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer { Interval = 15 };
        public bool On { get { return on; } set { if (on != value) { on = value; anim.Start(); Invalidate(); AccessibilityNotifyClients(AccessibleEvents.StateChange, -1); } } }
        public SlideSwitch(string label, bool start)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Text = label; on = start; knob = start ? 1 : 0; Cursor = Cursors.Hand; TabStop = true; AccessibleName = label; AccessibleRole = AccessibleRole.CheckButton;
            anim.Tick += delegate { float to = on ? 1 : 0; knob += (to - knob) * 0.45f; if (Math.Abs(to - knob) < 0.02f) { knob = to; anim.Stop(); } Invalidate(); };
            FitSize();
        }
        void FitSize() { float S = Draw.S; Size = new Size((int)(TextRenderer.MeasureText(L.T(Text), Draw.fBody).Width + 52 * S), (int)(32 * S)); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); FitSize(); }
        protected override void OnClick(EventArgs e) { On = !On; base.OnClick(e); }
        bool held;
        protected override void OnKeyDown(KeyEventArgs e) { if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && !held) { held = true; OnClick(e); } base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { held = false; base.OnKeyUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { held = false; Invalidate(); base.OnLostFocus(e); }
        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; float S = Draw.S;
            using (var bg = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Card)) g.FillRectangle(bg, ClientRectangle);
            string t = L.T(Text); var ts = g.MeasureString(t, Draw.fBody);
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(t, Draw.fBody, ink, 0, (Height - ts.Height) / 2);
            var tr = new RectangleF(Width - 40 * S, (Height - 20 * S) / 2, 38 * S, 20 * S);
            Color off = Theme.Dark ? Theme.C("#3A4456") : Theme.C("#C9D1DC");
            Color fill = Color.FromArgb((int)(off.R + (Theme.Blue.R - off.R) * knob), (int)(off.G + (Theme.Blue.G - off.G) * knob), (int)(off.B + (Theme.Blue.B - off.B) * knob));
            Theme.Fill(g, tr, tr.Height / 2, fill);
            if (Focused && ShowFocusCues) Theme.Stroke(g, tr, tr.Height / 2, Theme.Blue, 2);
            float k = tr.Height - 6 * S, kx = tr.X + 3 * S + (tr.Width - k - 6 * S) * knob;
            using (var kb = new SolidBrush(Color.White)) g.FillEllipse(kb, kx, tr.Y + 3 * S, k, k);
        }
    }

    // an on/off switch as a screen reader should hear it: a check button that says whether it's checked
    sealed class SwitchAccess : Control.ControlAccessibleObject
    {
        readonly Func<bool> on;
        public SwitchAccess(Control c, Func<bool> on) : base(c) { this.on = on; }
        public override AccessibleRole Role { get { return AccessibleRole.CheckButton; } }
        public override AccessibleStates State { get { var st = base.State | AccessibleStates.Focusable; if (on()) st |= AccessibleStates.Checked; return st; } }
        public override string DefaultAction { get { return on() ? "Switch off" : "Switch on"; } }
    }
    sealed class ThemeSwitch : Control
    {
        protected override AccessibleObject CreateAccessibilityInstance() { return new SwitchAccess(this, () => Theme.Dark); }
        public ThemeSwitch()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size((int)(64 * Draw.S), (int)(32 * Draw.S)); Cursor = Cursors.Hand; TabStop = true; AccessibleName = "Night theme";
        }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Cfg.SetTheme(Theme.Dark ? 0 : 1); }
        bool held;
        protected override void OnKeyDown(KeyEventArgs e) { if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && !held) { held = true; OnClick(e); } base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { held = false; base.OnKeyUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { held = false; Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; float S = Draw.S;
            using (var bg = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Card)) g.FillRectangle(bg, ClientRectangle);
            var tr = new RectangleF(1, 2 * S, Width - 2, Height - 4 * S);
            Theme.Fill(g, tr, tr.Height / 2, Theme.Dark ? Theme.C("#2B3445") : Theme.C("#E8EEF7"));
            bool ring = Focused && ShowFocusCues; Theme.Stroke(g, tr, tr.Height / 2, ring ? Theme.Blue : Theme.Line, ring ? 2 : 1);
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
            h = Math.Min(h, wa.Height); if (y + h > wa.Bottom) y = wa.Bottom - h;   // a tip taller than the room left: as much as fits, never off the screen
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
}
