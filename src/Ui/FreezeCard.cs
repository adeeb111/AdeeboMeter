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
        // 28 Nov (Adeeb: "Rerouting power (6 s)" stayed up over a video): a watchdog tick already running when the window closed
        // could show a card after the last Hide, and nothing hid it again. Now the card only lives while it's being fed: no
        // Show for 3 s means the window answered or went away, and it closes by itself. A closed window also refuses new cards.
        // Counted per window, not one flag: a second big window closing (the self-test's language pass, a window replaced by a
        // new one) used to switch the card off for the one still open - its freezes then showed nothing.
        static long lastFed; static int windows;
        public static void Off() { int n = Interlocked.Decrement(ref windows); if (n < 0) Interlocked.CompareExchange(ref windows, 0, n); if (n <= 0) Hide(); }
        public static void On() { Interlocked.Increment(ref windows); }

        // called from the watchdog thread every second while the window is frozen
        public static void Show(Rectangle over, int seconds, string doing)
        {
            if (Volatile.Read(ref windows) <= 0) return;
            Interlocked.Exchange(ref lastFed, DateTime.UtcNow.Ticks);
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
            string text; readonly Font f, fb; readonly float k;   /* review 1.1.6 U1 #15/#16: pixel fonts and a box scaled together, in the language's font */
            public volatile bool CloseAsap;
            protected override void OnShown(EventArgs e) { base.OnShown(e); if (CloseAsap) Close(); }
            readonly System.Windows.Forms.Timer dot = new System.Windows.Forms.Timer { Interval = 400 }; int phase;
            public CardForm(string t, Rectangle over)
            {
                text = t; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
                BackColor = Theme.Dark ? Color.FromArgb(32, 38, 48) : Color.White; DoubleBuffered = true;
                using (var gg = CreateGraphics()) k = Math.Max(1f, gg.DpiX / 96f);
                f = new Font(Draw.LangFamily, 13.5f * k, FontStyle.Regular, GraphicsUnit.Pixel); fb = new Font(Draw.LangFamily, 11f * k, FontStyle.Bold, GraphicsUnit.Pixel);
                int w = (int)(420 * k), h = (int)(64 * k); if (over.Width <= 0) over = Screen.PrimaryScreen.WorkingArea;
                Bounds = new Rectangle(over.X + (over.Width - w) / 2, over.Bottom - h - (int)(60 * k), w, h);
                dot.Tick += delegate
                {
                    phase = (phase + 1) % 3; Invalidate();
                    if (Volatile.Read(ref windows) <= 0 || DateTime.UtcNow.Ticks - Interlocked.Read(ref lastFed) > TimeSpan.TicksPerSecond * 3) Close();   // not fed for 3 s: gone
                }; dot.Start();   // the only thing that moves: it says "alive"
            }
            protected override bool ShowWithoutActivation { get { return true; } }   // never steals focus from what you're typing in
            protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x80; cp.ClassStyle |= 0x20000; return cp; } }   // no-activate, tool window, drop shadow
            public void Set(string t) { if (t != text) { text = t; Invalidate(); } }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.TextRenderingHint = Draw.ImageText; g.TextContrast = Draw.Contrast;
                using (var p = new Pen(Color.FromArgb(124, 92, 214), 2)) g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
                using (var b = new SolidBrush(Color.FromArgb(124, 92, 214))) for (int i = 0; i < 3; i++) { float d = (i == phase ? 9 : 6) * k; g.FillEllipse(b, (18 + i * 13) * k - d / 2f, Height / 2f - d / 2f, d, d); }
                using (var m = new SolidBrush(Color.FromArgb(124, 92, 214))) g.DrawString(AdeeboMeter.Kit.Sent("ADEEBOMETER"), fb, m, 62 * k, 10 * k);
                using (var b = new SolidBrush(Theme.Dark ? Color.FromArgb(230, 235, 240) : Color.FromArgb(30, 38, 50))) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(text, f, b, new RectangleF(62 * k, 28 * k, Width - 74 * k, Height - 30 * k), sf);
            }
            protected override void Dispose(bool disposing) { if (disposing) { dot.Dispose(); f.Dispose(); fb.Dispose(); } base.Dispose(disposing); }
        }
    }
}
