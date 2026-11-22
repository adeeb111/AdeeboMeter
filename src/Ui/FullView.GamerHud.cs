using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // Gamer Zone's HUD (Adeeb, 22 Nov: "it doesn't show enough - not the gamer vibe"): a dark, neon panel on top - your ping as a
    // big gauge with its grade, the last minute as a line with the spikes marked, jitter / loss / link / background, and one
    // verdict: ready to queue, or what to fix first. Redrawn on its own each second (only the panel).
    sealed partial class FullView
    {
        static readonly Color HudBg = Color.FromArgb(11, 15, 26), HudLine = Color.FromArgb(36, 44, 66), HudInk = Color.FromArgb(230, 237, 247), HudDim = Color.FromArgb(128, 140, 168),
            HudCyan = Color.FromArgb(0, 229, 255), HudPink = Color.FromArgb(255, 61, 165), HudGreen = Color.FromArgb(57, 255, 136), HudAmber = Color.FromArgb(255, 196, 0), HudRed = Color.FromArgb(255, 77, 77);
        string hudKey = "";
        void GamerHudTick()
        {
            if (tab != "gamer" || gamerCanvas == null) return;
            var s = snap; string bg = HudBackground();
            string k = Ms(s.Ping) + "|" + Ms(s.Jitter) + "|" + Math.Round(s.LossPct) + "|" + bg + "|" + (s.NetHist.Length > 0 ? s.NetHist[s.NetHist.Length - 1].ToString(CultureInfo.InvariantCulture) : "");
            if (k != hudKey) { hudKey = k; if (!RedrawSpots(gamerCanvas, "gm.hud")) gamerCanvas.Invalidate(); }
        }
        static DateTime hudBgAt; static string hudBg = "";
        static string HudBackground()
        {
            if ((DateTime.UtcNow - hudBgAt).TotalSeconds < 10) return hudBg; hudBgAt = DateTime.UtcNow;
            try
            {
                var names = System.Diagnostics.Process.GetProcesses().Select(p => { try { return p.ProcessName; } catch (InvalidOperationException) { return ""; } finally { p.Dispose(); } }).ToList();
                var hit = GamerTools.Updaters.FirstOrDefault(u => names.Any(n => string.Equals(n, u[0], StringComparison.OrdinalIgnoreCase)));
                hudBg = hit != null ? hit[1] : "";
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; hudBg = ""; }
            return hudBg;
        }
        float PaintGamerHud(System.Drawing.Graphics g, float x, float y, float w)
        {
            var s = snap; float h = 168 * S; var r = new RectangleF(x, y, w, h);
            Theme.Fill(g, r, 16 * S, HudBg);
            using (var p = Theme.Round(r, 16 * S)) using (var lg = new LinearGradientBrush(new PointF(r.X, r.Y), new PointF(r.Right, r.Y), Color.FromArgb(60, HudPink), Color.FromArgb(60, HudCyan))) using (var pen = new Pen(lg, 1.5f * S)) g.DrawPath(pen, p);
            double ping = s.Ping; string grade = double.IsNaN(ping) ? "NO SIGNAL" : ping < 40 ? "COMPETITIVE" : ping < 80 ? "CASUAL" : "LAGGY";
            Color gc = double.IsNaN(ping) ? HudDim : ping < 40 ? HudGreen : ping < 80 ? HudAmber : HudRed;
            // the gauge: 0-150 ms on a 240° arc
            float gd = 120 * S; var gr = new RectangleF(x + 22 * S, y + 22 * S, gd, gd);
            using (var bp = new Pen(HudLine, 10 * S) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(bp, gr, 150, 240);
            float frac = double.IsNaN(ping) ? 0 : (float)Math.Min(1, ping / 150.0);
            if (frac > 0) using (var vp = new Pen(gc, 10 * S) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(vp, gr, 150, Math.Max(2, 240 * frac));
            using (var cf = new StringFormat { Alignment = StringAlignment.Center }) using (var ib = new SolidBrush(HudInk)) using (var db = new SolidBrush(HudDim)) using (var gb = new SolidBrush(gc))
            {
                g.DrawString(double.IsNaN(ping) ? "--" : Ms(ping), Draw.fCardNum, ib, new RectangleF(gr.X, gr.Y + gd * 0.30f, gd, 40 * S), cf);
                g.DrawString("ms PING", Draw.fTiny, db, new RectangleF(gr.X, gr.Y + gd * 0.62f, gd, 16 * S), cf);
                g.DrawString(grade, Draw.fTiny, gb, new RectangleF(gr.X - 10 * S, gr.Bottom - 6 * S, gd + 20 * S, 16 * S), cf);
            }
            // the last minute, spikes marked
            float lx = gr.Right + 28 * S, lw = Math.Max(120 * S, w * 0.42f), ly = y + 26 * S, lh = 86 * S; var hist = s.NetHist ?? new double[0];
            using (var db = new SolidBrush(HudDim)) g.DrawString("LAST 60 SECONDS", Draw.fTiny, db, lx, y + 12 * S);
            using (var gp = new Pen(HudLine, 1)) { g.DrawLine(gp, lx, ly + lh, lx + lw, ly + lh); g.DrawLine(gp, lx, ly + lh / 2, lx + lw, ly + lh / 2); }
            var ok = hist.Where(v => !double.IsNaN(v)).ToList();
            if (ok.Count > 1)
            {
                double top = Math.Max(60, ok.Max() * 1.15), med = ok.OrderBy(v => v).ElementAt(ok.Count / 2);
                var pts = hist.Select((v, i) => double.IsNaN(v) ? PointF.Empty : new PointF(lx + lw * i / Math.Max(1, hist.Length - 1), ly + lh - (float)(lh * Math.Min(1, v / top)))).ToArray();
                using (var lp = new Pen(HudCyan, 2 * S) { LineJoin = LineJoin.Round })
                    for (int i = 1; i < pts.Length; i++) if (!pts[i].IsEmpty && !pts[i - 1].IsEmpty) g.DrawLine(lp, pts[i - 1], pts[i]);
                using (var sb = new SolidBrush(HudPink)) for (int i = 0; i < hist.Length; i++) { if (double.IsNaN(hist[i])) { g.FillRectangle(sb, pts.Length > i ? lx + lw * i / Math.Max(1, hist.Length - 1) - 1.5f * S : lx, ly + lh - 8 * S, 3 * S, 8 * S); continue; } if (hist[i] > med * 2 && hist[i] > med + 25) g.FillEllipse(sb, pts[i].X - 3.5f * S, pts[i].Y - 3.5f * S, 7 * S, 7 * S); }
            }
            else using (var db = new SolidBrush(HudDim)) g.DrawString("warming up...", Draw.fSmall, db, lx, ly + lh / 2 - 8 * S);
            // stats
            string kind; int sig; lock (M.L) { kind = M.AdapterKind; sig = M.WifiSignal; }
            string bg = HudBackground(); bool busy = !double.IsNaN(s.Down) && s.Down > 20 || !double.IsNaN(s.Up) && s.Up > 5;
            var stats = new[] {
                new object[] { "JITTER", double.IsNaN(s.Jitter) ? "-" : Ms(s.Jitter) + " ms", double.IsNaN(s.Jitter) ? HudDim : s.Jitter > 20 ? HudRed : s.Jitter > 8 ? HudAmber : HudGreen },
                new object[] { "LOSS", double.IsNaN(s.LossPct) ? "-" : s.LossPct.ToString("0.#", CultureInfo.InvariantCulture) + "%", s.LossPct > 2 ? HudRed : s.LossPct > 0 ? HudAmber : HudGreen },
                new object[] { "LINK", kind == "Wi-Fi" ? "Wi-Fi " + (sig >= 0 ? sig + "%" : "") : kind == "LAN" ? "Cable" : kind, kind == "Wi-Fi" && sig >= 0 && sig < 60 ? HudAmber : HudGreen },
                new object[] { "BACKGROUND", bg != "" ? "updating" : busy ? "busy line" : "quiet", bg != "" || busy ? HudAmber : HudGreen } };
            float sx = lx + lw + 26 * S, sw = x + w - 20 * S - sx, sh = 30 * S;
            if (sw > 120 * S)
                for (int i = 0; i < stats.Length; i++)
                {
                    float yy = y + 16 * S + i * (sh + 4 * S);
                    using (var db = new SolidBrush(HudDim)) g.DrawString((string)stats[i][0], Draw.fTiny, db, sx, yy + 7 * S);
                    using (var vb = new SolidBrush((Color)stats[i][2])) using (var rf = new StringFormat { Alignment = StringAlignment.Far }) g.DrawString((string)stats[i][1], Draw.fBodyB, vb, new RectangleF(sx, yy + 3 * S, sw, sh), rf);
                }
            // the verdict
            string verdict = double.IsNaN(ping) ? "NO CONNECTION - check Home first" : bg != "" ? "WAIT - " + bg.ToUpperInvariant() : busy ? "WAIT - SOMETHING IS USING YOUR LINE" : ping >= 80 || s.LossPct > 2 ? "EXPECT LAG - " + (s.LossPct > 2 ? "PACKETS ARE BEING LOST" : "THE LINE IS SLOW RIGHT NOW") : "READY TO QUEUE · GG";
            Color vc = verdict.StartsWith("READY", StringComparison.Ordinal) ? HudGreen : verdict.StartsWith("NO", StringComparison.Ordinal) ? HudRed : HudAmber;
            using (var vb = new SolidBrush(vc)) g.DrawString("▶ " + verdict, Draw.fBodyB, vb, lx, ly + lh + 16 * S);
            LiveSpot(gamerCanvas, "gm.hud", r);
            return y + h + 16 * S;
        }
    }
}
