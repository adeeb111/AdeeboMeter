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
    sealed class TermView : Control
    {
        public struct Seg { public string T; public Color C; public Seg(string t, Color c) { T = t; C = c; } }
        sealed class Line { public readonly List<Seg> S = new List<Seg>(); public int Len { get { int n = 0; foreach (var s in S) n += s.T.Length; return n; } } public string Text { get { var sb = new StringBuilder(); foreach (var s in S) sb.Append(s.T); return sb.ToString(); } } }
        readonly List<Line> lines = new List<Line> { new Line() };
        const int MaxLines = 8000;
        int liveAt = -1;                 // first line of the live region (a running tool's table or status), or -1
        public List<Seg> PromptSegs = new List<Seg>();
        public string Input = ""; public int Caret; public bool ShowInput = true; public bool Secret;
        public Color Bg = Color.FromArgb(12, 16, 22), Fg = Color.FromArgb(201, 209, 217), CaretColor = Color.FromArgb(63, 230, 120), SelColor = Color.FromArgb(70, 88, 166, 255);
        public string Title = "";
        public TermEditor Editor;   // nano / vim: while set, it owns the screen and the keys
        Font mono; float cw, chh; int scroll;   // scroll: visual rows up from the bottom (0 = following the output)
        readonly Timer blink; bool caretOn = true; DateTime typedAt;
        Point selA = new Point(-1, -1), selB = new Point(-1, -1); bool selecting;
        public event KeyEventHandler TermKey; public event KeyPressEventHandler TermChar;

        public TermView(Font f)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            mono = f; TabStop = true; Cursor = Cursors.IBeam;
            using (var g = CreateGraphics()) { var a = TextRenderer.MeasureText(g, new string('M', 40), mono, Size.Empty, TextFormatFlags.NoPadding); cw = a.Width / 40f; chh = (float)Math.Ceiling(mono.GetHeight(g) * 1.18f); }
            blink = new Timer { Interval = 530 }; blink.Tick += delegate { caretOn = !caretOn || (DateTime.UtcNow - typedAt).TotalMilliseconds < 500; InvalidateCaret(); }; blink.Start();
        }
        protected override void Dispose(bool disposing) { if (disposing) blink.Dispose(); base.Dispose(disposing); }
        protected override bool IsInputKey(Keys k) { var b = k & Keys.KeyCode; return b == Keys.Tab || b == Keys.Escape || b == Keys.Enter || b == Keys.Up || b == Keys.Down || b == Keys.Left || b == Keys.Right || b == Keys.Home || b == Keys.End || b == Keys.PageUp || b == Keys.PageDown || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { typedAt = DateTime.UtcNow; caretOn = true; if (Editor != null) { Editor.Key(e); if (!e.Handled) { e.SuppressKeyPress = false; } Invalidate(); base.OnKeyDown(e); return; } if (TermKey != null) TermKey(this, e); base.OnKeyDown(e); }
        protected override void OnKeyPress(KeyPressEventArgs e) { if (Editor != null) { Editor.Char(e.KeyChar); e.Handled = true; Invalidate(); return; } if (TermChar != null) TermChar(this, e); base.OnKeyPress(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        // ---------- writing
        public void Write(string s, Color c)
        {
            if (string.IsNullOrEmpty(s)) return;
            var parts = s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) lines.Add(new Line());
                if (parts[i].Length > 0) lines[lines.Count - 1].S.Add(new Seg(parts[i].Replace('\t', ' '), c));
            }
            Trim(); Changed();
        }
        public void Clear() { lines.Clear(); lines.Add(new Line()); liveAt = -1; scroll = 0; ClearSel(); Changed(); }
        // a live region: these lines replace the last live ones (a status line, a hop table) until EndLive
        public void Live(List<List<Seg>> rows)
        {
            if (liveAt < 0) { if (lines[lines.Count - 1].Len > 0) lines.Add(new Line()); liveAt = lines.Count - 1; }
            lines.RemoveRange(liveAt, lines.Count - liveAt);
            if (rows.Count == 0) lines.Add(new Line());
            foreach (var r in rows) { var l = new Line(); l.S.AddRange(r); lines.Add(l); }
            Changed();
        }
        public void EndLive() { if (liveAt < 0) return; lines.RemoveRange(liveAt, lines.Count - liveAt); lines.Add(new Line()); liveAt = -1; Changed(); }
        public bool AtLineStart { get { return lines[lines.Count - 1].Len == 0; } }
        void Trim() { if (lines.Count > MaxLines) { int n = lines.Count - MaxLines; lines.RemoveRange(0, n); if (liveAt >= 0) liveAt = Math.Max(0, liveAt - n); ClearSel(); } }
        void Changed() { if (scroll == 0 || selecting) { } Invalidate(); }
        public string AllText() { return string.Join("\n", lines.Select(l => l.Text)); }

        // ---------- layout: every logical line wrapped into rows of cols characters
        int Cols { get { return Math.Max(10, (int)((ClientSize.Width - 2 * Pad) / cw)); } }
        float Pad { get { return 14 * Draw.S; } }
        float TopY { get { return 40 * Draw.S; } }   // the window bar
        sealed class Row { public List<Seg> S = new List<Seg>(); public int Line, From; public bool IsInput; }
        List<Row> Rows()
        {
            int cols = Cols; var rows = new List<Row>();
            for (int li = 0; li < lines.Count; li++)
            {
                bool last = li == lines.Count - 1; var segs = new List<Seg>(lines[li].S);
                int inputFrom = -1;
                if (last && ShowInput) { inputFrom = segs.Sum(z => z.T.Length) + PromptSegs.Sum(z => z.T.Length); segs.AddRange(PromptSegs); segs.Add(new Seg(Secret ? new string('*', Input.Length) : Input, Fg)); }
                // cut into rows
                var row = new Row { Line = li, From = 0, IsInput = last && ShowInput }; int used = 0, pos = 0;
                foreach (var sg in segs)
                {
                    string t = sg.T; int i = 0;
                    while (i < t.Length)
                    {
                        int take = Math.Min(cols - used, t.Length - i);
                        if (take <= 0) { rows.Add(row); row = new Row { Line = li, From = pos, IsInput = row.IsInput }; used = 0; continue; }
                        row.S.Add(new Seg(t.Substring(i, take), sg.C)); used += take; i += take; pos += take;
                    }
                }
                rows.Add(row);
                if (last && ShowInput) { caretLine = li; caretCol = inputFrom + Caret; }
            }
            return rows;
        }
        int caretLine, caretCol;
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

            if (Editor != null) { Editor.Bg = Bg; Editor.Fg = Fg; Editor.Paint(g, mono, cw, chh, Pad, TopY, ClientSize, caretOn, Focused); return; }
            var rows = Rows(); int vis = VisibleRows; int maxScroll = Math.Max(0, rows.Count - vis); if (scroll > maxScroll) scroll = maxScroll;
            int first = Math.Max(0, rows.Count - vis - scroll);
            float y = TopY; var sel = SelRange();
            for (int r = first; r < rows.Count && r < first + vis; r++, y += chh)
            {
                float x = Pad; var row = rows[r]; int col = 0;
                // selection band
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
                if (row.IsInput && row.Line == caretLine && caretCol >= row.From && (caretCol < row.From + Cols || r == rows.Count - 1) && caretCol - row.From <= Cols)
                {
                    float cx = Pad + (caretCol - row.From) * cw;
                    if (caretOn || !Focused)
                    {
                        if (Focused) using (var glow = new SolidBrush(Color.FromArgb(50, CaretColor))) g.FillRectangle(glow, cx - 2 * S, y - 1 * S, cw + 4 * S, chh + 2 * S);
                        if (Focused) using (var cb = new SolidBrush(CaretColor)) g.FillRectangle(cb, cx, y + 1 * S, cw, chh - 2 * S);
                        else using (var cp = new Pen(CaretColor, 1)) g.DrawRectangle(cp, cx, y + 1 * S, cw, chh - 3 * S);
                        string under = caretCol - inputStartCol() >= 0 && caretCol - inputStartCol() < Input.Length ? Input.Substring(caretCol - inputStartCol(), 1) : "";
                        if (under != "" && Focused) TextRenderer.DrawText(g, Secret ? "*" : under, mono, new Point((int)cx, (int)y), Bg, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                    }
                }
            }
            if (scroll > 0)
            {
                string s = "↓ " + scroll + " more - scroll down or type";
                using (var b = new SolidBrush(Color.FromArgb(200, 31, 111, 235))) { var sz = g.MeasureString(s, Draw.fSmall); var rr = new RectangleF(Width - sz.Width - 30 * S, Height - sz.Height - 14 * S, sz.Width + 16 * S, sz.Height + 6 * S); g.FillRectangle(b, rr); g.DrawString(s, Draw.fSmall, Brushes.White, rr.X + 8 * S, rr.Y + 3 * S); }
            }
        }
        int inputStartCol() { return lines[lines.Count - 1].Len + PromptSegs.Sum(z => z.T.Length); }
        void InvalidateCaret() { if (ShowInput || Editor != null) Invalidate(); }

        // ---------- scrolling and selection
        protected override void OnMouseWheel(MouseEventArgs e) { if (Editor != null) return; var rows = Rows().Count; scroll = Math.Max(0, Math.Min(Math.Max(0, rows - VisibleRows), scroll + (e.Delta > 0 ? 3 : -3))); Invalidate(); }
        public void ScrollPage(int dir) { var rows = Rows().Count; scroll = Math.Max(0, Math.Min(Math.Max(0, rows - VisibleRows), scroll + dir * (VisibleRows - 2))); Invalidate(); }
        public void Follow() { scroll = 0; }
        Point CellAt(Point p)
        {
            var rows = Rows(); int vis = VisibleRows; int first = Math.Max(0, rows.Count - vis - scroll);
            int r = first + (int)Math.Floor((p.Y - TopY) / chh); r = Math.Max(0, Math.Min(rows.Count - 1, r));
            int c = (int)Math.Round((p.X - Pad) / cw); c = Math.Max(0, Math.Min(rows[r].S.Sum(z => z.T.Length), c));
            return new Point(rows[r].From + c, rows[r].Line);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus(); if (Editor != null) return;
            if (e.Button == MouseButtons.Right) { if (SelectedText() != "") { Copy(); } else if (Paste != null) Paste(); return; }
            if (e.Button == MouseButtons.Left) { selA = selB = CellAt(e.Location); selecting = true; Capture = true; Invalidate(); }
        }
        protected override void OnMouseMove(MouseEventArgs e) { if (selecting) { selB = CellAt(e.Location); Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { if (selecting) { selecting = false; Capture = false; if (selA == selB) ClearSel(); Invalidate(); } }
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
