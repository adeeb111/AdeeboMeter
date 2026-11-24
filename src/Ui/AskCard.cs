using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Every question and notice the app asks (Adeeb, 22 Nov: "make the pop-up visually better, everywhere"): the app's own card
    // instead of Windows' grey box - its colours, rounded, an icon in a soft circle, the words in the app's font and the app's
    // buttons (the main answer blue). Same calls and answers as MessageBox.Show; Enter picks the default, Esc the safe one.
    sealed class AskCard : Form
    {
        public static DialogResult Show(IWin32Window owner, string text, string caption) { return Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons b, MessageBoxIcon i) { return Show(owner, text, caption, b, i, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons b, MessageBoxIcon i, MessageBoxDefaultButton d)
        {
            try { using (var a = new AskCard(text, caption, b, i, d)) { var o = owner as Control; if (o != null && (o.IsDisposed || !o.IsHandleCreated)) owner = null; return owner != null ? a.ShowDialog(owner) : a.ShowDialog(); } }
            catch (Exception e) { if (e is OutOfMemoryException || e is StackOverflowException) throw; Program.Log("ask card: " + e.Message); return MessageBox.Show(owner, text, caption, b, i, d); }   // never lose the question
        }

        readonly string msg, cap; readonly MessageBoxIcon icon; readonly List<PillButton> btns = new List<PillButton>(); DialogResult esc;
        static float S { get { return Draw.S; } }
        AskCard(string text, string caption, MessageBoxButtons b, MessageBoxIcon i, MessageBoxDefaultButton d)
        {
            msg = text ?? ""; cap = string.IsNullOrEmpty(caption) || caption == "AdeeboMeter" ? "" : caption; icon = i;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; KeyPreview = true;
            BackColor = Theme.Card; Font = Draw.fBody; Text = string.IsNullOrEmpty(caption) ? "AdeeboMeter" : caption; DoubleBuffered = true;
            var answers = b == MessageBoxButtons.YesNo ? new[] { DialogResult.Yes, DialogResult.No } : b == MessageBoxButtons.YesNoCancel ? new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel }
                        : b == MessageBoxButtons.OKCancel ? new[] { DialogResult.OK, DialogResult.Cancel } : b == MessageBoxButtons.RetryCancel ? new[] { DialogResult.Retry, DialogResult.Cancel }
                        : b == MessageBoxButtons.AbortRetryIgnore ? new[] { DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore } : new[] { DialogResult.OK };
            esc = answers.Length == 1 ? answers[0] : Array.IndexOf(answers, DialogResult.Cancel) >= 0 ? DialogResult.Cancel : Array.IndexOf(answers, DialogResult.Abort) >= 0 ? DialogResult.Abort : answers[answers.Length - 1];   // R2-160: Esc is the safe answer - Abort, never Ignore
            int def = d == MessageBoxDefaultButton.Button2 ? 1 : d == MessageBoxDefaultButton.Button3 ? 2 : 0; if (def >= answers.Length) def = 0;
            for (int k = 0; k < answers.Length; k++)
            {
                var r = answers[k]; var pb = new PillButton(L.T(r == DialogResult.OK ? "OK" : r.ToString()), k == def ? PillButton.K.Primary : PillButton.K.Chip);
                pb.Click += delegate { DialogResult = r; Close(); }; pb.Tag = r; btns.Add(pb); Controls.Add(pb);
            }
            // size: the message wraps at about 440 px; the card fits it
            int w = (int)(520 * S); float tw = w - 104 * S;   // R2-052: the width OnPaint draws at (82 left, 22 right)
            float th = Draw.Measure(msg, Draw.fBody, (int)tw).Height, ch = cap == "" ? 0 : Draw.Measure(cap, Draw.fHead, (int)tw).Height + 6 * S;
            int bh = btns.Count > 0 ? btns[0].PreferredSize.Height : (int)(36 * S);
            int h = (int)(28 * S + ch + Math.Max(th, 44 * S) + 26 * S + bh + 22 * S);
            /* review 1.1.6 U1 #5: never taller than the screen - a long message is cut with a note, so OK / Cancel stay on screen */
            int maxH = (int)(Screen.FromPoint(Cursor.Position).WorkingArea.Height * 0.85);
            if (h > maxH)
            {
                var lines = msg.Replace("\r\n", "\n").Split('\n').ToList(); int cut = 0;
                while (lines.Count > 3 && h > maxH)
                {
                    lines.RemoveAt(lines.Count - 1); cut++;
                    msg = string.Join("\n", lines) + "\n… (" + cut + " " + L.T("more lines") + ")";
                    th = Draw.Measure(msg, Draw.fBody, (int)tw).Height; h = (int)(28 * S + ch + Math.Max(th, 44 * S) + 26 * S + bh + 22 * S);
                }
                h = Math.Min(h, maxH);
            }
            ClientSize = new Size(w, h);
            int bx = w - (int)(22 * S);
            for (int k = btns.Count - 1; k >= 0; k--) { var pb = btns[k]; var ps = pb.PreferredSize; bx -= Math.Max(ps.Width, (int)(92 * S)); pb.Bounds = new Rectangle(bx, h - (int)(20 * S) - bh, Math.Max(ps.Width, (int)(92 * S)), bh); bx -= (int)(10 * S); }
            using (var p = Theme.Round(new RectangleF(0, 0, w, h), 16 * S)) Region = new Region(p);
            Shown += delegate { if (def < btns.Count) btns[def].Focus(); };
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x00020000; return cp; } }   // CS_DROPSHADOW
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = esc; Close(); e.Handled = true; return; }
            if (e.KeyCode == Keys.Enter) { var f = btns.Find(x => x.Focused) ?? btns.Find(x => x.Kind == PillButton.K.Primary); if (f != null) { DialogResult = (DialogResult)f.Tag; Close(); } e.Handled = true; return; }
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) { int i = btns.FindIndex(x => x.Focused); if (i >= 0) btns[(i + (e.KeyCode == Keys.Right ? 1 : btns.Count - 1)) % btns.Count].Focus(); e.Handled = true; return; }
            base.OnKeyDown(e);
        }
        // drag it anywhere by the card
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } base.OnMouseDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = Draw.WindowText; g.TextContrast = Draw.Contrast;
            var all = new RectangleF(0.5f, 0.5f, ClientSize.Width - 1, ClientSize.Height - 1);
            Theme.Stroke(g, all, 16 * S, Theme.Dark ? Theme.Line : Theme.Line2, 1);
            Color c = icon == MessageBoxIcon.Warning ? Theme.Amber : icon == MessageBoxIcon.Error ? Theme.Red : Theme.Blue;
            string ic = icon == MessageBoxIcon.Question ? "question" : icon == MessageBoxIcon.Warning || icon == MessageBoxIcon.Error ? "shield" : "check";
            var cr = new RectangleF(22 * S, 26 * S, 44 * S, 44 * S);
            using (var sb = new SolidBrush(Color.FromArgb(Theme.Dark ? 50 : 32, c))) g.FillEllipse(sb, cr);
            Kit.Icon(g, ic, new RectangleF(cr.X + 11 * S, cr.Y + 11 * S, 22 * S, 22 * S), c);
            float tx = 82 * S, tw = ClientSize.Width - tx - 22 * S, y = 26 * S;
            if (cap != "") using (var ib = new SolidBrush(Theme.Ink)) using (var cf = new StringFormat(L.RunsRtl(cap) ? StringFormatFlags.DirectionRightToLeft : 0)) { var hs = Draw.Measure(cap, Draw.fHead, (int)tw); g.DrawString(cap, Draw.fHead, ib, new RectangleF(tx, y, tw, hs.Height + 2), cf); y += hs.Height + 6 * S; }
            var ms = Draw.Measure(msg, Draw.fBody, (int)tw);
            /* review 1.1.6 U1 #17: Arabic reads right to left */
            using (var sf = new StringFormat(L.RunsRtl(msg) ? StringFormatFlags.DirectionRightToLeft : 0))
            using (var sb = new SolidBrush(cap == "" ? Theme.Ink : Theme.Sub)) g.DrawString(msg, Draw.fBody, sb, new RectangleF(tx, y + (cap == "" ? 2 * S : 0), tw, Math.Min(ms.Height + 4, ClientSize.Height - y - 70 * S)), sf);
        }
    }
}
