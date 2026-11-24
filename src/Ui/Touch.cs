using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Touch and pen (Adeeb, 24 Nov: "the whole app should work with finger touch, touchscreen and pen").
    // Windows already turns a tap into a click, press-and-hold into a right-click, and a pen into a mouse. What it can't do for a
    // custom-drawn window is scroll it with a finger: the drag arrives as a pan gesture (WM_GESTURE), and a window that doesn't
    // answer it doesn't move. This filter answers it once for the whole app: the drag scrolls whatever is under the finger -
    // a control that knows how to scroll itself (ITouchPan), else the nearest scrolling panel. Windows adds the fling (inertia).
    interface ITouchPan { bool PanBy(int dx, int dy); }   // true: moved (or would have) - the gesture is used up here
    // a control that is dragged, not scrolled, where the finger lands (the Time machine's slider, the stream overlay): it says so
    // on touch-down and then follows the finger (screen coordinates)
    interface ITouchDrag { bool Grab(Point screen); void DragTo(Point screen); }

    sealed class TouchPan : IMessageFilter
    {
        const int WM_GESTURE = 0x0119, GID_PAN = 4, GF_BEGIN = 1;
        [StructLayout(LayoutKind.Sequential)] struct GESTUREINFO { public int cbSize, dwFlags, dwID; public IntPtr hwndTarget; public short x, y; public int dwInstanceID, dwSequenceID; public long ullArguments; public int cbExtraArgs; }
        [DllImport("user32.dll")] static extern bool GetGestureInfo(IntPtr h, ref GESTUREINFO gi);
        [DllImport("user32.dll")] static extern bool CloseGestureInfoHandle(IntPtr h);
        static bool installed; Point last; Control target; ITouchDrag grabbed;

        public static void Install()
        {
            if (installed) return; installed = true;
            try { if (Environment.OSVersion.Version >= new Version(6, 1)) Application.AddMessageFilter(new TouchPan()); } catch (Exception e) { Program.Log("touch: " + e.Message); }
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_GESTURE) return false;
            var gi = new GESTUREINFO { cbSize = Marshal.SizeOf(typeof(GESTUREINFO)) };
            try
            {
                if (!GetGestureInfo(m.LParam, ref gi)) return false;
                if (gi.dwID != GID_PAN) return false;   // zoom, rotate, two-finger tap, begin/end: Windows' default
                var p = new Point(gi.x, gi.y);
                if ((gi.dwFlags & GF_BEGIN) != 0 || target == null || target.IsDisposed)
                {
                    last = p; target = Control.FromChildHandle(m.HWnd); grabbed = null;
                    for (var c = target; c != null; c = c.Parent) { var td = c as ITouchDrag; if (td != null && td.Grab(p)) { grabbed = td; return Used(m); } }
                    if (target is ListView || target is TextBoxBase || target is ComboBox || target is TreeView || target is WebBrowser) target = null;   // they pan by themselves
                    if (target == null) return false;
                    return Used(m);
                }
                if (grabbed != null) { grabbed.DragTo(p); last = p; return Used(m); }
                int dx = last.X - p.X, dy = last.Y - p.Y; last = p;
                if (dx == 0 && dy == 0) return Used(m);
                for (var c = target; c != null; c = c.Parent)
                {
                    var tp = c as ITouchPan; if (tp != null && tp.PanBy(dx, dy)) return Used(m);
                    var sc = c as ScrollableControl;
                    if (sc != null && sc.AutoScroll && (sc.VerticalScroll.Visible && dy != 0 || sc.HorizontalScroll.Visible && dx != 0))
                    {
                        var at = sc.AutoScrollPosition; sc.AutoScrollPosition = new Point(-at.X + (sc.HorizontalScroll.Visible ? dx : 0), -at.Y + (sc.VerticalScroll.Visible ? dy : 0));
                        sc.Invalidate(); return Used(m);
                    }
                }
                return false;   // nothing scrolls here (a ListView, a text box): their own touch handling
            }
            catch (Exception e) { Program.Log("touch pan: " + e.Message); return false; }
        }
        // a gesture answered here is closed here; one passed on is closed by Windows' default handling
        static bool Used(Message m) { try { CloseGestureInfoHandle(m.LParam); } catch (Exception) { } return true; }
    }
}
