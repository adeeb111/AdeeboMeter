// The freeze guard (PLAN 11.8a): if the window doesn't answer for 2 s, a small "Still with you - catching up (3 s)" card
// shows over it. It lives on its OWN thread with its own message loop and its own fonts, so it appears even while the
// window thread is stuck, and it goes away the moment the window answers. Never looks hung, never silent.
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AdeeboMeter
{
    static class FreezeCard
    {
        static Thread thread; static CardForm card; static readonly object k = new object();
        static bool wanted;   // 21 Nov: a Hide that lands before the card's window exists still closes it (after sleep the card stayed up)
        public static volatile int Shown;   // how many times it appeared (the self-test reads it)

        // called from the watchdog thread every second while the window is frozen
        public static void Show(Rectangle over, int seconds, string doing)
        {
            string line = Lines.Speaks("problem") ? Lines.Line("freeze", seconds + " s") : "";
            if (line == "") line = "Still working - the window is catching up (" + seconds + " s).";
            lock (k)
            {
                if (card != null) { var c = card; try { c.BeginInvoke((Action)delegate { c.Set(line); }); } catch (InvalidOperationException) { } return; }
                if (wanted) return;   // already on its way
                Shown++; wanted = true;
                var t = new Thread(() =>
                {
                    var f = new CardForm(line, over);
                    lock (k) { if (!wanted) f.CloseAsap = true; else card = f; }
                    try { Application.Run(f); }
                    catch (Exception e) { Program.Log("freeze card: " + e.Message); }
                    finally { lock (k) { if (card == f) { card = null; wanted = false; } } f.Dispose(); }
                }) { IsBackground = true, Name = "freeze card" };
                t.SetApartmentState(ApartmentState.STA); thread = t; t.Start();
            }
        }
        public static void Hide()
        {
            lock (k)
            {
                wanted = false;
                var c = card; if (c == null) return; card = null;
                c.CloseAsap = true;   // bug-hunt ML-169: asked to go before its window exists - it closes itself the moment it's shown
                try { if (c.IsHandleCreated) c.BeginInvoke((Action)delegate { c.Close(); }); } catch (InvalidOperationException) { }
            }
        }
        public static bool Visible { get { lock (k) return card != null; } }

        sealed class CardForm : Form
        {
            string text; readonly Font f = new Font("Segoe UI", 10f), fb = new Font("Segoe UI", 8f, FontStyle.Bold);
            public volatile bool CloseAsap;
            protected override void OnShown(EventArgs e) { base.OnShown(e); if (CloseAsap) Close(); }
            readonly System.Windows.Forms.Timer dot = new System.Windows.Forms.Timer { Interval = 400 }; int phase;
            public CardForm(string t, Rectangle over)
            {
                text = t; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
                BackColor = Theme.Dark ? Color.FromArgb(32, 38, 48) : Color.White; DoubleBuffered = true;
                int w = 420, h = 64; if (over.Width <= 0) over = Screen.PrimaryScreen.WorkingArea;
                Bounds = new Rectangle(over.X + (over.Width - w) / 2, over.Bottom - h - 60, w, h);
                dot.Tick += delegate { phase = (phase + 1) % 3; Invalidate(); }; dot.Start();   // the only thing that moves: it says "alive"
            }
            protected override bool ShowWithoutActivation { get { return true; } }   // never steals focus from what you're typing in
            protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x80; cp.ClassStyle |= 0x20000; return cp; } }   // no-activate, tool window, drop shadow
            public void Set(string t) { if (t != text) { text = t; Invalidate(); } }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (var p = new Pen(Color.FromArgb(124, 92, 214), 2)) g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
                using (var b = new SolidBrush(Color.FromArgb(124, 92, 214))) for (int i = 0; i < 3; i++) { int d = i == phase ? 9 : 6; g.FillEllipse(b, 18 + i * 13 - d / 2f, Height / 2f - d / 2f, d, d); }
                using (var m = new SolidBrush(Color.FromArgb(124, 92, 214))) g.DrawString(AdeeboMeter.L.T("ADEEBOMETER"), fb, m, 62, 10);
                using (var b = new SolidBrush(Theme.Dark ? Color.FromArgb(230, 235, 240) : Color.FromArgb(30, 38, 50))) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(text, f, b, new RectangleF(62, 28, Width - 74, 24), sf);
            }
            protected override void Dispose(bool disposing) { if (disposing) { dot.Dispose(); f.Dispose(); fb.Dispose(); } base.Dispose(disposing); }
        }
    }
}
