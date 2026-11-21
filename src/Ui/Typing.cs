// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // The buddy types its line (Adeeb, 16 Nov: "buddy and the fun line should look like typing and take 2 seconds at most").
    // A new line for a slot starts typing; the same line again stays whole. 28 ms a letter, never longer than 2 s in all.
    // Off when Windows' "Show animations" is off. Layout always measures the whole line - only the drawing types - so
    // nothing around it moves while it types. One shared 33 ms timer repaints what's typing, and stops when nothing is.
    static class Typing
    {
        const double PerChar = 28, MaxMs = 2000;
        static readonly Dictionary<string, KeyValuePair<string, DateTime>> seen = new Dictionary<string, KeyValuePair<string, DateTime>>(StringComparer.Ordinal);
        static readonly HashSet<Control> wake = new HashSet<Control>();
        static readonly Dictionary<string, DateTime> changed = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        const double Calm = 15;   // seconds a slot must stay unchanged before its next new line types again
        static Timer t;
        public static bool Hold;   // screen peek: draw every line whole, so a still picture never shows half a sentence
        public static bool On { get { try { return SystemInformation.UIEffectsEnabled; } catch (Exception e) { if (e is OutOfMemoryException) throw; return true; } } }
        // what to draw now for this slot's text (UI thread only)
        public static string Show(string slot, string text, Control c)
        {
            if (string.IsNullOrEmpty(text) || !On || Hold) return text ?? "";
            KeyValuePair<string, DateTime> p;
            if (!seen.TryGetValue(slot, out p) || p.Key != text)
            {
                // a line that changes again soon after the last change (a live number, trouble that keeps updating) is shown
                // whole: typing it again each time looked like it never stopped (Adeeb, 21 Nov). Only a line that stood still
                // for a while types itself out.
                DateTime prev; bool busy = changed.TryGetValue(slot, out prev) && (DateTime.UtcNow - prev).TotalSeconds < Calm;
                changed[slot] = DateTime.UtcNow;
                p = new KeyValuePair<string, DateTime>(text, busy ? DateTime.MinValue : DateTime.UtcNow); seen[slot] = p;
            }
            double dur = Math.Min(MaxMs, text.Length * PerChar), el = p.Value == DateTime.MinValue ? double.MaxValue : (DateTime.UtcNow - p.Value).TotalMilliseconds;
            if (el >= dur) return text;
            int n = Math.Max(0, Math.Min(text.Length, (int)Math.Ceiling(text.Length * el / dur)));
            if (c != null) { wake.Add(c); Tick(); }
            return text.Substring(0, n) + "|";
        }
        static void Tick()
        {
            if (t != null) return;
            t = new Timer { Interval = 33 };
            t.Tick += delegate
            {
                var l = wake.ToList(); wake.Clear();
                foreach (var c in l) if (!c.IsDisposed && c.Visible) c.Invalidate();
                if (wake.Count == 0 && l.Count == 0) { t.Stop(); t.Dispose(); t = null; }
            };
            t.Start();
        }
    }
}
