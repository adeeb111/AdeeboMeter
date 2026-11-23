// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // AdeeboShell's screen (Adeeb, 22 Nov: "top to bottom writing, it should look cool, maybe a blinking cursor"): a real
    // terminal surface - the text flows down from the top, the prompt and what you type sit right after the last line, a blinking
    // block cursor, a live region a running tool redraws in place, scroll back with the wheel, drag to select, right-click pastes.
    //
    // Shell scrutiny #3: wrapping is cached - only new lines and the live region are wrapped again, so a repaint (the caret blinks
    // twice a second) costs the visible rows, not the whole scrollback. One line is kept to MaxLine characters on screen (a 20 MB
    // base64 line can't freeze the window; > file still gets all of it). #64: tabs go to the next 8-column stop. #65: trimming the
    // oldest lines leaves a marker saying so.
    sealed class TermView : Control
    {
        public struct Seg { public string T; public Color C; public Seg(string t, Color c) { T = t; C = c; } }
        sealed class Line { public readonly List<Seg> S = new List<Seg>(); public int Len; public string Text { get { var sb = new StringBuilder(); foreach (var s in S) sb.Append(s.T); return sb.ToString(); } } public void Add(Seg s) { S.Add(s); Len += s.T.Length; } }
        readonly List<Line> lines = new List<Line> { new Line() };
        const int MaxLines = 8000, MaxLine = 20000;
        int liveAt = -1;                 // first line of the live region (a running tool's table or status), or -1
        public List<Seg> PromptSegs = new List<Seg>();
        public string Input = ""; public int Caret; public bool ShowInput = true; public bool Secret;
        public Color Bg = Color.FromArgb(12, 16, 22), Fg = Color.FromArgb(201, 209, 217), CaretColor = Color.FromArgb(63, 230, 120), SelColor = Color.FromArgb(70, 88, 166, 255);
        public string Title = "";
        public TermEditor Editor;   // nano / vim / less: while set, it owns the screen and the keys
        Font mono; float cw, chh; int scroll;   // scroll: visual rows up from the bottom (0 = following the output)
        readonly Timer blink; bool caretOn = true; DateTime typedAt;
        Point selA = new Point(-1, -1), selB = new Point(-1, -1); bool selecting;
        public event KeyEventHandler TermKey; public event KeyPressEventHandler TermChar;

        public TermView(Font f)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            mono = f; TabStop = true; Cursor = Cursors.IBeam;
            using (var g = CreateGraphics()) { var a = TextRenderer.MeasureText(g, new string('M', 40), mono, Size.Empty, TextFormatFlags.NoPadding); cw = a.Width / 40f; chh = (float)Math.Ceiling(mono.GetHeight(g) * 1.18f); }
            blink = new Timer { Interval = 530 }; blink.Tick += delegate { if (!Visible) return; caretOn = !caretOn || (DateTime.UtcNow - typedAt).TotalMilliseconds < 500; InvalidateCaret(); }; blink.Start();
        }
        protected override void Dispose(bool disposing) { if (disposing) blink.Dispose(); base.Dispose(disposing); }
        protected override bool IsInputKey(Keys k) { var b = k & Keys.KeyCode; return b == Keys.Tab || b == Keys.Escape || b == Keys.Enter || b == Keys.Up || b == Keys.Down || b == Keys.Left || b == Keys.Right || b == Keys.Home || b == Keys.End || b == Keys.PageUp || b == Keys.PageDown || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { typedAt = DateTime.UtcNow; caretOn = true; if (Editor != null) { Editor.Key(e); Invalidate(); base.OnKeyDown(e); return; } if (TermKey != null) TermKey(this, e); base.OnKeyDown(e); }
        protected override void OnKeyPress(KeyPressEventArgs e) { if (Editor != null) { Editor.Char(e.KeyChar); e.Handled = true; Invalidate(); return; } if (TermChar != null) TermChar(this, e); base.OnKeyPress(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        // ---------- writing
        public void Write(string s, Color c)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (statusOn) EndLive();   // the passing "what I'm doing" line steps aside for real output
            LastWrite = DateTime.UtcNow;
            var parts = s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) lines.Add(new Line());
                var ln = lines[lines.Count - 1]; string t = parts[i];
                if (t.Length == 0 || ln.Len > MaxLine) continue;
                if (t.IndexOf('\t') >= 0) { var sb = new StringBuilder(); int col = ln.Len; foreach (char ch in t) { if (ch == '\t') { int n = 8 - (col % 8); sb.Append(' ', n); col += n; } else { sb.Append(ch); col++; } } t = sb.ToString(); }
                if (ln.Len + t.Length > MaxLine) { int keep = Math.Max(0, MaxLine - ln.Len); ln.Add(new Seg(t.Substring(0, keep), c)); ln.Add(new Seg("  … (" + (t.Length - keep).ToString("N0") + " more characters on this line - not shown; > file keeps them all)", Color.FromArgb(125, 133, 144))); ln.Len = MaxLine + 1; continue; }
                ln.Add(new Seg(t, c));
            }
            Trim(); Changed();
        }
        public void Clear() { lines.Clear(); lines.Add(new Line()); liveAt = -1; scroll = 0; ClearSel(); ResetCache(); Changed(); }
        // a live region: these lines replace the last live ones (a status line, a hop table) until EndLive
        // a passing status line ("running: ping 1.1.1.1 · 12 s"): shown only while nothing else is live and the line is empty,
        // gone the moment real output or a tool's own live table comes (1.1.6 test: a script into a file looked stuck)
        public DateTime LastWrite = DateTime.UtcNow;
        bool statusOn; public bool StatusOn { get { return statusOn; } }
        public bool CanStatus { get { return statusOn || (liveAt < 0 && AtLineStart && Editor == null); } }
        public void Status(List<Seg> row) { if (!CanStatus) return; Live(new List<List<Seg>> { row }); statusOn = true; }
        public void ClearStatus() { if (statusOn) EndLive(); }
        public void Live(List<List<Seg>> rows)
        {
            statusOn = false;
            if (liveAt < 0) { if (lines[lines.Count - 1].Len > 0) lines.Add(new Line()); liveAt = lines.Count - 1; }
            DirtyFrom(liveAt); lines.RemoveRange(liveAt, lines.Count - liveAt);
            if (rows.Count == 0) lines.Add(new Line());
            foreach (var r in rows) { var l = new Line(); foreach (var sg in r) l.Add(sg); lines.Add(l); }
            Changed();
        }
        public void EndLive() { statusOn = false; if (liveAt < 0) return; DirtyFrom(liveAt); lines.RemoveRange(liveAt, lines.Count - liveAt); lines.Add(new Line()); liveAt = -1; Changed(); }
        public bool AtLineStart { get { return lines[lines.Count - 1].Len == 0; } }
        void Trim()
        {
            if (lines.Count <= MaxLines + 500) return;   // in batches, so the cache isn't thrown away on every line
            int n = lines.Count - MaxLines; lines.RemoveRange(0, n);
            var note = new Line(); note.Add(new Seg("… older lines dropped - the last " + MaxLines.ToString("N0") + " are kept here (> file keeps everything)", Color.FromArgb(125, 133, 144))); lines.Insert(0, note);
            if (liveAt >= 0) liveAt = Math.Max(1, liveAt - n + 1); ClearSel(); ResetCache();
        }
        void Changed() { Invalidate(); }
        public string AllText() { return string.Join("\n", lines.Select(l => l.Text)); }

        // ---------- layout: every logical line wrapped into rows of Cols characters; all but the last line cached
        int Cols { get { return Math.Max(10, (int)((ClientSize.Width - 2 * Pad) / cw)); } }
        float Pad { get { return 14 * Draw.S; } }
        float TopY { get { return 40 * Draw.S; } }   // the window bar
        sealed class Row { public List<Seg> S = new List<Seg>(); public int Line, From; public bool IsInput; }
        readonly List<Row> cache = new List<Row>(); int cachedLines, cacheCols = -1;
        List<Row> tail = new List<Row>();
        void ResetCache() { cache.Clear(); cachedLines = 0; }
        void DirtyFrom(int line)
        {
            if (line >= cachedLines) return;
            cachedLines = line; int k = cache.FindIndex(r => r.Line >= line); if (k >= 0) cache.RemoveRange(k, cache.Count - k);
        }
        List<Row> Wrap(int li, List<Seg> segs, bool input, int cols)
        {
            var rows = new List<Row>(); var row = new Row { Line = li, From = 0, IsInput = input }; int used = 0, pos = 0;
            foreach (var sg in segs)
            {
                string t = sg.T; int i = 0;
                while (i < t.Length)
                {
                    int take = Math.Min(cols - used, t.Length - i);
                    if (take <= 0) { rows.Add(row); row = new Row { Line = li, From = pos, IsInput = input }; used = 0; continue; }
                    row.S.Add(new Seg(t.Substring(i, take), sg.C)); used += take; i += take; pos += take;
                }
            }
            rows.Add(row); return rows;
        }
        int caretLine, caretCol;
        // brings the cache up to date and wraps the last line (with the prompt and what you're typing); returns the row count
        int Relayout()
        {
            int cols = Cols; if (cols != cacheCols) { ResetCache(); cacheCols = cols; }
            while (cachedLines < lines.Count - 1) { cache.AddRange(Wrap(cachedLines, lines[cachedLines].S, false, cols)); cachedLines++; }
            int li = lines.Count - 1; var segs = new List<Seg>(lines[li].S);
            if (ShowInput) { int inputFrom = lines[li].Len + PromptSegs.Sum(z => z.T.Length); segs.AddRange(PromptSegs); segs.Add(new Seg(Secret ? new string('*', Input.Length) : Input, Fg)); caretLine = li; caretCol = inputFrom + Caret; }
            tail = Wrap(li, segs, ShowInput, cols);
            return cache.Count + tail.Count;
        }
        Row RowAt(int i) { return i < cache.Count ? cache[i] : tail[i - cache.Count]; }
        int VisibleRows { get { return Math.Max(1, (int)((ClientSize.Height - TopY - 8 * Draw.S) / chh)); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (var bg = new LinearGradientBrush(ClientRectangle.Width > 0 && ClientRectangle.Height > 0 ? ClientRectangle : new Rectangle(0, 0, 1, 1), Bg, Color.FromArgb(Bg.R + 4, Bg.G + 6, Bg.B + 12), 90f)) g.FillRectangle(bg, ClientRectangle);
            float S = Draw.S;
            // the window bar: three dots and the title
            using (var bar = new SolidBrush(Color.FromArgb(22, 27, 34))) g.FillRectangle(bar, 0, 0, Width, 30 * S);
            using (var line = new Pen(Color.FromArgb(40, 48, 58))) g.DrawLine(line, 0, 30 * S, Width, 30 * S);
            var dots = new[] { Color.FromArgb(255, 95, 86), Color.FromArgb(255, 189, 46), Color.FromArgb(39, 201, 63) };
            for (int i = 0; i < 3; i++) using (var b = new SolidBrush(dots[i])) g.FillEllipse(b, 14 * S + i * 18 * S, 10 * S, 11 * S, 11 * S);
            using (var tb = new SolidBrush(Color.FromArgb(139, 148, 158))) using (var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(Title, Draw.fSmall, tb, new RectangleF(0, 0, Width, 30 * S), cf);
            PaintBarButtons(g);
            if (Editor != null) { Editor.Bg = Bg; Editor.Fg = Fg; Editor.Paint(g, mono, cw, chh, Pad, TopY, ClientSize, caretOn, Focused); return; }

            int count = Relayout(), vis = VisibleRows;
            // scrolled up: new output must not drag what you're reading away (1.1.6 test - alltools' stream made scrolling back impossible)
            if (scroll > 0 && lastCount >= 0 && count > lastCount) scroll += count - lastCount; lastCount = count;
            int maxScroll = Math.Max(0, count - vis); if (scroll > maxScroll) scroll = maxScroll;
            int first = Math.Max(0, count - vis - scroll), cols = Cols;
            float y = TopY; var sel = SelRange();
            for (int r = first; r < count && r < first + vis; r++, y += chh)
            {
                float x = Pad; var row = RowAt(r); int col = 0;
                if (sel != null)
                {
                    int a = Math.Max(row.From, RowSelFrom(sel, row)), b = RowSelTo(sel, row);
                    if (b > a) using (var sb = new SolidBrush(SelColor)) g.FillRectangle(sb, Pad + (a - row.From) * cw, y, (b - a) * cw, chh);
                }
                foreach (var sg in row.S)
                {
                    TextRenderer.DrawText(g, sg.T, mono, new Point((int)(x + col * cw), (int)y), sg.C, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
                    col += sg.T.Length;
                }
                // the cursor: a glowing block
                if (row.IsInput && row.Line == caretLine && caretCol >= row.From && (caretCol < row.From + cols || r == count - 1) && caretCol - row.From <= cols)
                {
                    float cx = Pad + (caretCol - row.From) * cw;
                    if (caretOn || !Focused)
                    {
                        if (Focused) using (var glow = new SolidBrush(Color.FromArgb(50, CaretColor))) g.FillRectangle(glow, cx - 2 * S, y - 1 * S, cw + 4 * S, chh + 2 * S);
                        if (Focused) using (var cb = new SolidBrush(CaretColor)) g.FillRectangle(cb, cx, y + 1 * S, cw, chh - 2 * S);
                        else using (var cp = new Pen(CaretColor, 1)) g.DrawRectangle(cp, cx, y + 1 * S, cw, chh - 3 * S);
                        int ic = caretCol - InputStartCol();
                        string under = ic >= 0 && ic < Input.Length ? Input.Substring(ic, 1) : "";
                        if (under != "" && Focused) TextRenderer.DrawText(g, Secret ? "*" : under, mono, new Point((int)cx, (int)y), Bg, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                    }
                }
            }
            var th = Thumb(count, vis);
            if (th.Height > 0) { using (var tb = new SolidBrush(Color.FromArgb(28, 255, 255, 255))) g.FillRectangle(tb, Track()); using (var tb = new SolidBrush(Color.FromArgb(dragging || scroll > 0 ? 150 : 90, 139, 148, 158))) g.FillRectangle(tb, th); }
            if (scroll > 0)
            {
                string s = "↓ " + scroll + " more - scroll down or type";
                using (var b = new SolidBrush(Color.FromArgb(200, 31, 111, 235))) { var sz = g.MeasureString(s, Draw.fSmall); var rr = new RectangleF(Width - sz.Width - 30 * S, Height - sz.Height - 14 * S, sz.Width + 16 * S, sz.Height + 6 * S); g.FillRectangle(b, rr); g.DrawString(s, Draw.fSmall, Brushes.White, rr.X + 8 * S, rr.Y + 3 * S); }
            }
        }
        int InputStartCol() { return lines[lines.Count - 1].Len + PromptSegs.Sum(z => z.T.Length); }
        // the blink only repaints the caret's own rows
        void InvalidateCaret()
        {
            if (Editor != null) { Invalidate(); return; }
            if (!ShowInput || scroll > 0) return;
            int count = Relayout(), vis = VisibleRows, first = Math.Max(0, count - vis);
            // RG-9: every row of the input, however many it wraps to (walked back from the end while they're input rows)
            for (int r = count - 1; r >= first; r--) { if (!RowAt(r).IsInput) { if (r < count - 1) break; continue; } Invalidate(new Rectangle(0, (int)(TopY + (r - first) * chh) - 2, Width, (int)chh + 4)); }
        }

        // ---------- scrolling and selection
        protected override void OnMouseWheel(MouseEventArgs e) { if (Editor != null) { Editor.Wheel(e.Delta > 0 ? -3 : 3); Invalidate(); return; } int rows = Relayout(); scroll = Math.Max(0, Math.Min(Math.Max(0, rows - VisibleRows), scroll + (e.Delta > 0 ? 3 : -3))); Invalidate(); }
        public void ScrollPage(int dir) { int rows = Relayout(); scroll = Math.Max(0, Math.Min(Math.Max(0, rows - VisibleRows), scroll + dir * (VisibleRows - 2))); Invalidate(); }
        public void Follow() { scroll = 0; }
        // ---------- two buttons in the window bar: Copy (the whole session, or the selection) and Save log (Adeeb, 1.1.6 test day)
        public Action CopyAll, SaveLog; string barFlash = ""; DateTime barFlashAt;
        readonly RectangleF[] barBtns = new RectangleF[2];
        public void Flash(string s) { barFlash = s ?? ""; barFlashAt = DateTime.UtcNow; Invalidate(new Rectangle(0, 0, Width, (int)(31 * Draw.S))); var t = new Timer { Interval = 2600 }; t.Tick += delegate { t.Stop(); t.Dispose(); if (!IsDisposed) Invalidate(new Rectangle(0, 0, Width, (int)(31 * Draw.S))); }; t.Start(); }
        void PaintBarButtons(Graphics g)
        {
            float S = Draw.S, x = ClientSize.Width - 14 * S; var labels = new[] { SelectedText() != "" ? "Copy selection" : "Copy all", "Save log" };
            for (int i = labels.Length - 1; i >= 0; i--)
            {
                var sz = g.MeasureString(labels[i], Draw.fSmall); var r = new RectangleF(x - sz.Width - 16 * S, 5 * S, sz.Width + 16 * S, 20 * S); x = r.X - 8 * S; barBtns[i] = r;
                using (var b = new SolidBrush(Color.FromArgb(33, 40, 50))) g.FillRectangle(b, r);
                using (var pn = new Pen(Color.FromArgb(55, 65, 78))) g.DrawRectangle(pn, r.X, r.Y, r.Width, r.Height);
                using (var tb = new SolidBrush(Color.FromArgb(201, 209, 217))) using (var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(labels[i], Draw.fSmall, tb, r, cf);
            }
            if (barFlash != "" && (DateTime.UtcNow - barFlashAt).TotalSeconds < 2.5)
                using (var fb = new SolidBrush(Color.FromArgb(63, 185, 80))) using (var cf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(barFlash, Draw.fSmall, fb, new RectangleF(70 * S, 0, Math.Max(1, x - 70 * S), 30 * S), cf);
        }
        // ---------- a scroll bar on the right: drag it, or click above/below the thumb to page
        int lastCount = -1; bool dragging; float dragOff;
        RectangleF Track() { float S = Draw.S; return new RectangleF(ClientSize.Width - 9 * S, TopY, 6 * S, Math.Max(1, ClientSize.Height - TopY - 8 * Draw.S)); }
        RectangleF Thumb(int count, int vis)
        {
            if (count <= vis) return RectangleF.Empty;
            var t = Track(); float h = Math.Max(28 * Draw.S, t.Height * vis / count), top = count - vis - scroll;
            return new RectangleF(t.X, t.Y + (t.Height - h) * top / (count - vis), t.Width, h);
        }
        void DragTo(float y)
        {
            int count = Relayout(), vis = VisibleRows; if (count <= vis) return; var t = Track(); var th = Thumb(count, vis);
            float span = Math.Max(1, t.Height - th.Height), top = Math.Max(0, Math.Min(span, y - dragOff - t.Y)) / span * (count - vis);
            scroll = Math.Max(0, Math.Min(count - vis, count - vis - (int)Math.Round(top))); Invalidate();
        }
        bool OnBar(Point p) { return Editor == null && p.X >= ClientSize.Width - 16 * Draw.S && p.Y >= TopY; }
        Point CellAt(Point p)
        {
            int count = Relayout(), vis = VisibleRows; int first = Math.Max(0, count - vis - scroll);
            int r = first + (int)Math.Floor((p.Y - TopY) / chh); r = Math.Max(0, Math.Min(count - 1, r));
            var row = RowAt(r); int c = (int)Math.Round((p.X - Pad) / cw); c = Math.Max(0, Math.Min(row.S.Sum(z => z.T.Length), c));
            return new Point(row.From + c, row.Line);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus(); if (Editor != null) return;
            if (e.Button == MouseButtons.Left && e.Y < 30 * Draw.S) { if (barBtns[0].Contains(e.Location) && CopyAll != null) CopyAll(); else if (barBtns[1].Contains(e.Location) && SaveLog != null) SaveLog(); return; }
            if (e.Button == MouseButtons.Right) { if (SelectedText() != "") { Copy(); } else if (Paste != null) Paste(); return; }
            if (e.Button == MouseButtons.Left && OnBar(e.Location))
            {
                int count = Relayout(), vis = VisibleRows; var th = Thumb(count, vis); if (th.Height <= 0) return;
                if (e.Y >= th.Top && e.Y <= th.Bottom) { dragging = true; dragOff = e.Y - th.Top; Capture = true; Invalidate(); }
                else ScrollPage(e.Y < th.Top ? 1 : -1);
                return;
            }
            if (e.Button == MouseButtons.Left) { selA = selB = CellAt(e.Location); selecting = true; Capture = true; Invalidate(); }
        }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) { DragTo(e.Y); return; } Cursor = OnBar(e.Location) ? Cursors.Default : Cursors.IBeam; if (selecting) { selB = CellAt(e.Location); Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { if (dragging) { dragging = false; Capture = false; Invalidate(); return; } if (selecting) { selecting = false; Capture = false; if (selA == selB) ClearSel(); Invalidate(); } }
        public Action Paste;
        public void ClearSel() { selA = selB = new Point(-1, -1); }
        Point[] SelRange()
        {
            if (selA.X < 0 || selA == selB) return null;
            bool aFirst = selA.Y < selB.Y || (selA.Y == selB.Y && selA.X <= selB.X);
            return aFirst ? new[] { selA, selB } : new[] { selB, selA };
        }
        int RowSelFrom(Point[] s, Row r) { if (r.Line < s[0].Y || r.Line > s[1].Y) return int.MaxValue; return r.Line == s[0].Y ? s[0].X : 0; }
        int RowSelTo(Point[] s, Row r) { if (r.Line < s[0].Y || r.Line > s[1].Y) return int.MinValue; int end = r.From + r.S.Sum(z => z.T.Length); return Math.Min(end, r.Line == s[1].Y ? s[1].X : int.MaxValue); }
        public string SelectedText()
        {
            var s = SelRange(); if (s == null) return "";
            var sb = new StringBuilder();
            for (int li = s[0].Y; li <= s[1].Y && li < lines.Count; li++)
            {
                string t = lines[li].Text; int a = li == s[0].Y ? Math.Min(t.Length, s[0].X) : 0, b = li == s[1].Y ? Math.Min(t.Length, s[1].X) : t.Length;
                if (b > a) sb.Append(t, a, b - a); if (li < s[1].Y) sb.Append('\n');
            }
            return sb.ToString();
        }
        public bool Copy() { string t = SelectedText(); if (t == "") return false; try { Clipboard.SetText(t); } catch (Exception e) { if (!(e is System.Runtime.InteropServices.ExternalException || e is System.Threading.ThreadStateException)) throw; } ClearSel(); Invalidate(); return true; }
    }
}
