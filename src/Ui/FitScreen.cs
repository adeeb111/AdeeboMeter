using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdeeboMeter
{
    static partial class Draw
    {
        // hunters10 (display ML-2, ML-3, L-3): a dialog sized in scaled units can be bigger than a small screen at 150-200% -
        // its title bar and buttons then sit off-screen. Once it is shown: no bigger than the working area, and fully on it.
        // A fixed dialog that had to shrink becomes resizable, so nothing is left out of reach.
        public static void FitScreen(Form f, bool shrink)
        {
            if (f == null || f.IsDisposed) return;
            var wa = Screen.FromRectangle(f.Bounds).WorkingArea;
            if (shrink && (f.Width > wa.Width || f.Height > wa.Height))
            {
                if (f.FormBorderStyle == FormBorderStyle.FixedDialog || f.FormBorderStyle == FormBorderStyle.FixedSingle) f.FormBorderStyle = FormBorderStyle.Sizable;
                f.Size = new Size(Math.Min(f.Width, wa.Width), Math.Min(f.Height, wa.Height));
                f.AutoScroll = true;   /* hunters11: what no longer fits scrolls, rather than being cut */
            }
            int x = Math.Max(wa.Left, Math.Min(f.Left, wa.Right - f.Width)), y = Math.Max(wa.Top, Math.Min(f.Top, wa.Bottom - f.Height));
            if (x != f.Left || y != f.Top) f.Location = new Point(x, y);
        }
        public static void FitOnShow(Form f, bool shrink) { f.Shown += delegate { FitScreen(f, shrink); }; }
    }
}
