using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // Gamer Zone's panel on top: your ping, jitter / lost / link / background, and one verdict - ready to queue, or what to
    // fix first. Redrawn on its own each second (only the panel).
    sealed partial class FullView
    {
        string hudKey = "";
        void GamerHudTick()
        {
            if (tab != "gamer" || gamerCanvas == null) return;
            var s = snap; string bg = HudBackground();
            string k = Ms(s.Ping) + "|" + Ms(s.Jitter) + "|" + (double.IsNaN(s.LossPct) ? "-" : s.LossPct.ToString("0.#", CultureInfo.InvariantCulture)) /* R2-154: the tenth the panel shows */ + "|" + bg + "|" + (s.NetHist.Length > 0 ? s.NetHist[s.NetHist.Length - 1].ToString(CultureInfo.InvariantCulture) : "");
            if (k != hudKey) { hudKey = k; if (!RedrawSpots(gamerCanvas, "gm.hud")) gamerCanvas.Invalidate(); }
        }
        static DateTime hudBgAt; static volatile string hudBg = ""; static int hudBgBusy;
        // R2-047: the process list is read on a pool thread every 10 s - the paint and the tick only read the last answer
        // R2-048: a process that won't give its name (protected, elevated) is skipped, never the whole list
        static string HudBackground()
        {
            if ((DateTime.UtcNow - hudBgAt).TotalSeconds < 10 || System.Threading.Interlocked.CompareExchange(ref hudBgBusy, 1, 0) != 0) return hudBg;
            hudBgAt = DateTime.UtcNow;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var names = System.Diagnostics.Process.GetProcesses().Select(p => { try { return p.ProcessName; } catch (Exception e) { if (e is OutOfMemoryException) throw; return ""; } finally { p.Dispose(); } }).ToList();
                    var hit = GamerTools.Updaters.FirstOrDefault(u => names.Any(n => string.Equals(n, u[0], StringComparison.OrdinalIgnoreCase)));
                    hudBg = hit != null ? hit[1] : "";
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; hudBg = ""; }
                finally { System.Threading.Interlocked.Exchange(ref hudBgBusy, 0); }
            });
            return hudBg;
        }
        // 24 Nov (Adeeb, v8: "the black screen doesn't go well with the flow"): the panel is a card like every other one, laid
        // out as the approved mockup - your ping big on the left with the verdict under it, then jitter, lost, link and
        // background in columns. No gauge, no neon, no line chart: the numbers and one coloured word say it.
        float PaintGamerHud(System.Drawing.Graphics g, float x, float y, float w)
        {
            var s = snap; float h = 112 * S; var r = new RectangleF(x, y, w, h);
            Kit.Box(g, r, 14 * S);
            double ping = s.Ping;
            int plv = double.IsNaN(ping) ? 2 : ping < 40 ? 0 : ping < 80 ? 1 : 2;
            string kind; int sig; lock (M.L) { kind = M.AdapterKind; sig = M.WifiSignal; }
            string bg = HudBackground(); bool busy = !double.IsNaN(s.Down) && s.Down > 20 || !double.IsNaN(s.Up) && s.Up > 5;
            // the verdict
            string verdict = double.IsNaN(ping) ? "NO CONNECTION - check Home first" : bg != "" ? "WAIT - " + bg : busy ? "WAIT - SOMETHING IS USING YOUR LINE" : ping >= 80 || s.LossPct > 2 ? "EXPECT LAG - " + (s.LossPct > 2 ? "PACKETS ARE BEING LOST" : "THE LINE IS SLOW RIGHT NOW") : "READY TO QUEUE · GG";
            int vlv = verdict.StartsWith("READY", StringComparison.Ordinal) ? 0 : verdict.StartsWith("NO", StringComparison.Ordinal) ? 2 : 1;
            // shown in the app's language: each part translated on its own ("WAIT - " + a program's name stays a program's name)
            // sentence case like the rest of the app (review 24 Nov); the program's own name keeps its spelling
            string shown = verdict.StartsWith("WAIT - ", StringComparison.Ordinal) && verdict != "WAIT - SOMETHING IS USING YOUR LINE" ? Kit.Sent(L.T("WAIT - ")) + verdict.Substring(7)
                : verdict.StartsWith("EXPECT LAG - ", StringComparison.Ordinal) ? Kit.Sent(L.T("EXPECT LAG - ") + L.T(verdict.Substring(13))) : Kit.Sent(L.T(verdict));
            float px = x + 22 * S;
            using (var sub = new SolidBrush(Theme.Sub)) using (var ink = new SolidBrush(plv == 0 ? Theme.Ink : Theme.LvText(plv)))
            {
                g.DrawString(L.T("Your ping right now"), Draw.fSmall, sub, px, y + 16 * S);
                string pv = double.IsNaN(ping) ? "--" : Ms(ping); var ps = g.MeasureString(pv, Draw.fHero);
                g.DrawString(pv, Draw.fHero, ink, px - 2 * S, y + 32 * S);
                g.DrawString(L.T("ms"), Draw.fBody, sub, px + ps.Width - 4 * S, y + 32 * S + ps.Height - g.MeasureString("ms", Draw.fBody).Height - 6 * S);
            }
            float leftW = 230 * S;
            Draw.LvDot(g, vlv, px, y + h - 26 * S, 8 * S, 8 * S);
            using (var vb = new SolidBrush(Theme.LvText(vlv))) using (var ef = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                g.DrawString(shown, Draw.fBodyB, vb, new RectangleF(px + 14 * S, y + h - 31 * S, leftW - 14 * S, 20 * S), ef);
            // the four readings in columns, each a label over its value; a value is coloured only when it isn't fine
            var stats = new[] {
                Tuple.Create("Jitter", double.IsNaN(s.Jitter) ? "-" : Ms(s.Jitter) + " ms", double.IsNaN(s.Jitter) ? 3 : s.Jitter > 20 ? 2 : s.Jitter > 8 ? 1 : 0),
                Tuple.Create("Lost", double.IsNaN(s.LossPct) ? "-" : s.LossPct.ToString("0.#", CultureInfo.InvariantCulture) + "%", double.IsNaN(s.LossPct) ? 3 : s.LossPct > 2 ? 2 : s.LossPct > 0 ? 1 : 0),
                Tuple.Create(kind == "Wi-Fi" ? "Wi-Fi" : "Link", kind == "Wi-Fi" ? (sig >= 0 ? sig + "%" : L.T("signal hidden")) : kind == "LAN" ? L.T("Cable") : (string.IsNullOrEmpty(kind) ? "-" : kind), string.IsNullOrEmpty(kind) ? 3 : kind == "Wi-Fi" && sig >= 0 && sig < 60 ? 1 : 0),
                Tuple.Create("Background", bg != "" ? L.T("updating") : busy ? L.T("busy line") : L.T("quiet"), bg != "" || busy ? 1 : 0) };
            float sx = x + leftW + 24 * S, sw = (x + w - 22 * S - sx) / stats.Length;
            if (sw > 70 * S)
                using (var sub = new SolidBrush(Theme.Sub)) using (var nw = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    for (int i = 0; i < stats.Length; i++)
                    {
                        float cx = sx + i * sw;
                        g.DrawString(L.T(stats[i].Item1), Draw.fSmall, sub, new RectangleF(cx, y + 30 * S, sw - 10 * S, 20 * S), nw);
                        using (var vb = new SolidBrush(stats[i].Item3 == 1 || stats[i].Item3 == 2 ? Theme.LvText(stats[i].Item3) : Theme.Ink)) g.DrawString(stats[i].Item2, Draw.fTitle, vb, new RectangleF(cx, y + 50 * S, sw - 10 * S, 28 * S), nw);
                    }
            LiveSpot(gamerCanvas, "gm.hud", r);
            return y + h + 16 * S;
        }
    }
}
