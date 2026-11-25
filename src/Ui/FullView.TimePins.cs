// Pin this moment (1.1.7 idea from PR #29's snapshots, done inside the existing Time machine): pin a minute you want to
// remember - "before the router swap", "the evening it was bad" - and later compare that pinned minute with now.
// The minute's numbers already live in history, so a pin is only a time; nothing else is stored.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AdeeboMeter
{
    sealed partial class FullView
    {
        const int TmPinMax = 64;
        static List<DateTime> tmPins;
        static string TmPinFile { get { return Path.Combine(Program.DataDir, "time-pins.txt"); } }
        static List<DateTime> TmPins()
        {
            if (tmPins != null) return tmPins;
            tmPins = new List<DateTime>();
            try
            {
                if (File.Exists(TmPinFile) && new FileInfo(TmPinFile).Length < 64 * 1024)
                    foreach (var l in File.ReadAllLines(TmPinFile))
                    {
                        DateTime t; if (DateTime.TryParseExact(l.Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) && !tmPins.Contains(t)) tmPins.Add(t);
                    }
            }
            catch (Exception e) { Program.Log("tmPins: " + e.Message); }
            tmPins = tmPins.OrderBy(t => t).Skip(Math.Max(0, tmPins.Count - TmPinMax)).ToList();
            return tmPins;
        }
        static void SaveTmPins()
        {
            try { SafeFile.WriteText(TmPinFile, string.Join("\r\n", TmPins().Select(t => t.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).ToArray())); }
            catch (Exception e) { Program.Log("tmPins: " + e.Message); }
        }
        static DateTime TmMinute(DateTime t) { return new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0); }

        // "now" for the comparison: the newest minute history wrote today, re-read at most once a minute
        static History.Day tmNowDay; static DateTime tmNowAt = DateTime.MinValue;
        static double[] TmPinNow(out string net, out DateTime at)
        {
            net = ""; at = DateTime.MinValue;
            if ((DateTime.UtcNow - tmNowAt).TotalSeconds > 60 || tmNowDay == null) { tmNowDay = History.Load(DateTime.Today); tmNowAt = DateTime.UtcNow; }
            var d = tmNowDay; if (d == null || d.M.Count == 0) return null;
            int i = d.M.Count - 1; net = i < d.Net.Count ? d.Net[i] : ""; at = DateTime.Today.AddSeconds(d.M[i][0]); return d.M[i];
        }

        // drawn under the plain-words box of the Time machine; returns the new y
        float PaintTmPins(Graphics g, float x, float y, float w, DateTime at, double[] row, string netName)
        {
            if (replayExample) return y;
            var ps = TmPins(); var m = TmMinute(at); bool pinned = ps.Contains(m);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Kit.Sent("PINNED MOMENTS"), Draw.fTiny, mb, x, y + 8 * S);
            float cx = x + 140 * S;
            // pin / unpin this minute
            string pt = pinned ? L.T("Unpin") + " " + m.ToString("HH:mm", CultureInfo.CurrentCulture) : L.T("Pin this minute");
            var pts = g.MeasureString(pt, Draw.fBodyB); var pr = new RectangleF(cx, y, pts.Width + 50 * S, 30 * S);
            Theme.Fill(g, pr, 15 * S, pinned ? Theme.BlueSoft : Theme.Soft);
            Glyphs.Draw(g, "star", new RectangleF(cx + 10 * S, y + 5 * S, 20 * S, 20 * S), pinned ? Theme.Blue : Theme.Sub, 1.8f);
            using (var tb = new SolidBrush(Theme.Ink)) g.DrawString(pt, Draw.fBodyB, tb, cx + 36 * S, y + (30 * S - pts.Height) / 2);
            Act(replayCanvas, pr, delegate { var l = TmPins(); if (l.Contains(m)) l.Remove(m); else { l.Add(m); l.Sort(); while (l.Count > TmPinMax) l.RemoveAt(0); } SaveTmPins(); replayCanvas.Invalidate(); });
            cx += pr.Width + 10 * S;
            // the other tmPins: click to go there (newest first)
            foreach (var p in ps.Where(t => t != m).OrderByDescending(t => t).Take(8))
            {
                string ct = p.Date == DateTime.Today ? p.ToString("HH:mm", CultureInfo.CurrentCulture) : p.ToString("d MMM HH:mm", CultureInfo.CurrentCulture);
                var cs = g.MeasureString(ct, Draw.fSmall); if (cx + cs.Width + 18 * S > x + w) break;
                var cr = new RectangleF(cx, y + 3 * S, cs.Width + 18 * S, 24 * S); Theme.Stroke(g, cr, 12 * S, Theme.Line, 1);
                using (var tb = new SolidBrush(Theme.Ink)) g.DrawString(ct, Draw.fSmall, tb, cx + 9 * S, y + 3 * S + (24 * S - cs.Height) / 2);
                var pp = p; Act(replayCanvas, cr, delegate { string keep = replayFrom, back = replayBack; replayDay = pp.Date; replayData = null; ReplayAt(pp.TimeOfDay.TotalSeconds / 86400.0); replayFrom = keep; replayBack = back; replayCanvas.Invalidate(); });
                cx += cr.Width + 6 * S;
            }
            y += 40 * S;
            if (!pinned) return y;

            // a pinned minute compared with now: only what history kept, changes marked
            string netNow; DateTime nowAt; var now = TmPinNow(out netNow, out nowAt);
            if (now == null || nowAt <= m) return y;
            Func<double[], int, double> V = (r, i) => r != null && i < r.Length ? r[i] : double.NaN;
            Func<double, string, string> F = (v, u) => double.IsNaN(v) ? "-" : v.ToString("0", CultureInfo.CurrentCulture) + u;
            var rows = new List<string[]> {
                new[] { "Router answered in", F(V(row, 5), " ms"), F(V(now, 5), " ms"), TmWorse(V(row, 5), V(now, 5), 10, true) },
                new[] { "Internet answered in", F(V(row, 6), " ms"), F(V(now, 6), " ms"), TmWorse(V(row, 6), V(now, 6), 20, true) },
                new[] { "Lost replies", F(V(row, 11), "%"), F(V(now, 11), "%"), TmWorse(V(row, 11), V(now, 11), 1, true) },
                new[] { "Wi-Fi signal", V(row, 7) >= 0 ? F(V(row, 7), "%") : "-", V(now, 7) >= 0 ? F(V(now, 7), "%") : "-", TmWorse(V(row, 7), V(now, 7), 10, false) },
                new[] { "Wi-Fi link rate", F(V(row, 12), " Mb/s"), F(V(now, 12), " Mb/s"), TmWorse(V(row, 12), V(now, 12), 50, false) },
                new[] { "Processor", F(V(row, 2), "%"), F(V(now, 2), "%"), TmWorse(V(row, 2), V(now, 2), 20, true) } };
            if (Cfg.Details) rows.Add(new[] { "Network", Draw.Trunc(netName ?? "", 22), Draw.Trunc(netNow, 22), (netName ?? "") != netNow && netName != "" && netNow != "" ? "c" : "" });
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Kit.Sent("PINNED") + " " + m.ToString("d MMM HH:mm", CultureInfo.CurrentCulture) + "  ·  " + Kit.Sent("NOW") + " " + nowAt.ToString("HH:mm", CultureInfo.CurrentCulture), Draw.fTiny, mb, x, y); y += 22 * S;
            Kit.Box(g, new RectangleF(x, y, w, rows.Count * 28 * S + 10 * S)); y += 5 * S;
            float c1 = x + 16 * S, c2 = x + Math.Min(260 * S, w * 0.4f), c3 = c2 + Math.Min(170 * S, w * 0.25f), c4 = c3 + Math.Min(170 * S, w * 0.25f);
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub))
                foreach (var r in rows)
                {
                    g.DrawString(L.T(r[0]), Draw.fBody, sub, c1, y + 4 * S); g.DrawString(r[1], Draw.fBody, ink, c2, y + 4 * S);
                    Color nc = r[3] == "w" ? Theme.LvText(2) : r[3] == "b" ? Theme.LvText(0) : Theme.Ink;
                    using (var nb = new SolidBrush(nc)) g.DrawString(r[2], Draw.fBodyB, nb, c3, y + 4 * S);
                    if (r[3] != "") using (var nb = new SolidBrush(nc)) g.DrawString(L.T(r[3] == "w" ? "worse now" : r[3] == "b" ? "better now" : "changed"), Draw.fSmall, nb, c4, y + 6 * S);
                    y += 28 * S;
                }
            return y + 14 * S;
        }
        // "w" worse now, "b" better now, "" about the same (a change smaller than the step doesn't count)
        internal static string TmWorse(double then, double now, double step, bool higherIsWorse)
        {
            if (double.IsNaN(then) || double.IsNaN(now) || then < 0 || now < 0 || Math.Abs(now - then) < step) return "";
            return (now > then) == higherIsWorse ? "w" : "b";
        }
    }
}
