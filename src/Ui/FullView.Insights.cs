// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    // What changed: when is my network bad (7 x 24), data usage, what's normal here, calls, suggestions, new devices.
    sealed partial class FullView
    {
        Bitmap weekBmp; string weekKey = ""; int[,] weekLv; double[,] weekGw; static string[,] weekWhy; DateTime weekAt = DateTime.MinValue; volatile bool weekBusy;
        // a square's hover: the hour, and the worst thing logged in it - so a red hour says whether it was the PC or the line
        static Explain.Entry HourEntry(string key)
        {
            var p = key.Split(':'); int d, h; string[,] wy = weekWhy;
            if (p.Length < 3 || wy == null || !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out d) || !int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out h) || d < 0 || d > 6 || h < 0 || h > 23 || wy[d, h] == null) return Explain.Term("hist.WEEK");
            var kt = wy[d, h].Split('\u0001'); string kind = kt[0], text = kt.Length > 1 ? kt[1] : "";
            bool pc = Regex.IsMatch(kind + " " + text, "(?i)memory|processor|cpu|disk|pc\\b|this pc|frozen|\\bram\\b");
            var day = DateTime.Today.AddDays(d - 6);
            return new Explain.Entry
            {
                Friendly = day.ToString("ddd d MMM", CultureInfo.CurrentCulture) + ", " + new DateTime(2000, 1, 1, h, 0, 0).ToString("t", CultureInfo.CurrentCulture),
                Cat = Explain.Other, Close = -1, Tag = pc ? L.T("The cause was this PC, not the internet.") : L.T("The worst thing logged in this hour:"),
                What = text, How = "", Why = "", Without = L.T("Click the square to replay that hour.")
            };
        }
        float PaintInsights(Graphics g, float x, float y, float w)
        {
            // ---- the week at a glance (from history; rebuilt at most every 10 minutes, drawn once into an image)
            if (Cfg.History && !weekBusy && ((weekLv == null && (DateTime.UtcNow - weekAt).TotalMinutes > 1) || (DateTime.UtcNow - weekAt).TotalMinutes > 10))   // a failed read waits a minute, never a read per paint
            {
                weekBusy = true;
                Task.Factory.StartNew(() => { double[,] gw = null; int[,] lv = null; string[,] wy = null; try { lv = History.Week(out gw, out wy); } catch (Exception e) { Program.Log("week: " + e.Message); } try { BeginInvoke((Action)delegate { weekLv = lv; weekGw = gw; weekWhy = wy; weekAt = DateTime.UtcNow; weekBusy = false; weekKey = ""; changesCanvas.Invalidate(); }); } catch { weekBusy = false; } });
            }
            float hh = 7 * 22 * S + 52 * S;
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Kit.Sent(L.T("WHEN WERE THERE PROBLEMS?  ·  LAST 7 DAYS × 24 HOURS  ·  POINT AT A SQUARE FOR THE CAUSE")), Draw.fTiny, mb, x, y);
            Hint(changesCanvas, new RectangleF(x, y - 2 * S, w, 18 * S), "hist.WEEK");
            y += 20 * S;
            if (weekLv != null)
            {
                string key = (int)w + "|" + Theme.Dark + "|" + weekAt.Ticks;
                if (weekBmp == null || weekKey != key)
                {
                    if (weekBmp != null) weekBmp.Dispose();
                    weekBmp = new Bitmap(Math.Max(1, (int)w), Math.Max(1, (int)hh)); weekKey = key;
                    using (var bg = Graphics.FromImage(weekBmp)) { Prep(bg); bg.Clear(Theme.Card); DrawWeek(bg, w); }
                }
                g.DrawImageUnscaled(weekBmp, (int)x, (int)y);
                // hover a square: that hour's story
                float lw = 70 * S, cw = (w - lw) / 24, rh = 22 * S;
                for (int d = 0; d < 7; d++) for (int h = 0; h < 24; h++)
                {
                    var sq = new RectangleF(x + lw + h * cw, y + d * rh, cw, rh); Hint(changesCanvas, sq, weekWhy != null && weekWhy[d, h] != null ? "hour:" + d + ":" + h : "hist.WEEK");
                    if (weekLv[d, h] < 0) continue;
                    var day = DateTime.Today.AddDays(d - 6); double at = (h + 0.5) / 24.0;
                    Act(changesCanvas, sq, delegate { replayDay = day; replayData = null; tab = "replay"; ShowTab(); ReplayAt(at); });   // click a square: replay that hour
                }
                y += hh;
            }
            else
            {
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Cfg.History ? "Reading history..." : "Turn on history in Settings to see your week.", Draw.fBody, mb, x, y);
                y += 30 * S;
            }

            // ---- data usage and what's normal here
            string net = Brain.NetLabel;
            var rows = new List<string[]>
            {
                new[] { "Today", Usage.Mb(Usage.Today(null)) + (net != "" ? "  ·  " + Usage.Mb(Usage.Today(Brain.NetKey)) + " on " + net : "") },
                new[] { "This month", Usage.Mb(Usage.Month(null)) + (Cfg.CapGB > 0 ? "  of " + Cfg.CapGB + " GB  (" + (Usage.Month(null) / 1000 / Cfg.CapGB * 100).ToString("0", CultureInfo.CurrentCulture) + "%)" : "") },
            };
            foreach (var r in Usage.MonthByNetwork().Take(4)) rows.Add(new[] { "  " + Draw.Trunc(r[0], 26), Usage.Mb(Store.D(r[1])) + " down · " + Usage.Mb(Store.D(r[2])) + " up" });
            string normal = Baseline.Normal(Brain.NetKey);
            if (normal != "") rows.Add(new[] { "What's normal here", normal });
            rows.Add(new[] { "Latency under load", Bloat.Story() });
            y = InfoCard(g, "DATA USAGE AND WHAT'S NORMAL  ·  COUNTED ON THIS PC, NEVER SENT", rows, "hist.USAGE", x, y, w);

            // ---- after your calls (memory only)
            List<string[]> calls; lock (Calls.Summaries) calls = Calls.Summaries.ToList();   // a copy: a call can end while this paints
            if (calls.Count > 0)
                y = InfoCard(g, "AFTER YOUR CALLS  ·  NUMBERS ONLY, KEPT IN MEMORY UNTIL YOU QUIT - NOTHING IS RECORDED", calls.Select(c => new[] { c[0], c[1], c[2] }).ToList(), "hist.CALLS", x, y, w);
            // ---- watch and suggest
            List<Watch.Sug> sugs; lock (Watch.List) sugs = Watch.List.Take(8).ToList();
            if (sugs.Count > 0)
                y = InfoCard(g, "WORTH A LOOK  ·  AdeeboMeter ONLY SUGGESTS - IT NEVER BLOCKS ANYTHING", sugs.Select(s => new[] { s.T.ToString("ddd HH:mm", CultureInfo.CurrentCulture) + "  " + Draw.Trunc(s.App, 18), s.Why + "  " + s.How, s.Lv.ToString(CultureInfo.InvariantCulture) }).ToList(), "hist.WATCH", x, y, w);
            List<string[]> joined; lock (Devices.Joined) joined = Devices.Joined.ToList();
            if (joined.Count > 0)
                y = InfoCard(g, "NEW DEVICES ON YOUR NETWORK", joined.Take(6).Select(d => new[] { d[0], d[2] + " on " + d[1] + " - " + d[4], "1" }).ToList(), "hist.DEVICES", x, y, w);
            return y + 6 * S;
        }
        void DrawWeek(Graphics g, float w)
        {
            float lw = 70 * S, cw = (w - lw) / 24, rh = 22 * S;
            using (var mb = new SolidBrush(Theme.Muted))
            {
                for (int d = 0; d < 7; d++)
                {
                    var dd = DateTime.Today.AddDays(d - 6);
                    g.DrawString(dd.ToString("ddd d", CultureInfo.CurrentCulture), Draw.fSmall, mb, 0, d * rh + 3 * S);
                    if (UpdateBreak.ChangeDays.Contains(dd)) using (var ub = new SolidBrush(Theme.Blue)) g.FillEllipse(ub, lw - 12 * S, d * rh + 7 * S, 7 * S, 7 * S);   // something was installed that day
                    for (int h = 0; h < 24; h++)
                    {
                        int l = weekLv[d, h];
                        var c = l < 0 ? Theme.Line2 : l == 0 ? Theme.LvSoft(0) : l == 1 ? Theme.Amber : Theme.Red;
                        if (l == 0 && !double.IsNaN(weekGw[d, h]) && weekGw[d, h] > 20) c = Theme.LvText(0);
                        Theme.Fill(g, new RectangleF(lw + h * cw + 1, d * rh + 2, cw - 2, rh - 4), 3 * S, c);
                    }
                }
                for (int h = 0; h < 24; h += 3) g.DrawString(h.ToString("00", CultureInfo.CurrentCulture), Draw.fSmall, mb, lw + h * cw, 7 * rh + 4 * S);
                float lx = lw; float ly = 7 * rh + 24 * S;
                var keys = new[] { new KeyValuePair<Color, string>(Theme.LvSoft(0), "all good"), new KeyValuePair<Color, string>(Theme.LvText(0), "fine, router a bit slow"), new KeyValuePair<Color, string>(Theme.Amber, "busy / slow"), new KeyValuePair<Color, string>(Theme.Red, "problem"), new KeyValuePair<Color, string>(Theme.Line2, "PC off / no history") };
                foreach (var kv in keys) { Theme.Fill(g, new RectangleF(lx, ly + 3 * S, 10 * S, 10 * S), 3 * S, kv.Key); g.DrawString(kv.Value, Draw.fSmall, mb, lx + 14 * S, ly); lx += g.MeasureString(kv.Value, Draw.fSmall).Width + 30 * S; }
            }
        }
        // { label, text, [level] } rows; the text wraps
        float InfoCard(Graphics g, string title, List<string[]> rows, string key, float x, float y, float w)
        {
            float lw = 170 * S, tw = w - lw - 36 * S; var hs = rows.Select(r => Math.Max(28 * S, Math.Max(Draw.Measure(r[1], Draw.fBody, (int)tw).Height, Draw.Measure(r[0], Draw.fBody, (int)(lw - 8 * S)).Height) + 10 * S)).ToList();
            // 24 Nov (v8 screenshots): the title in heading type and sentence case, like the other cards - was tiny grey capitals
            var card = new RectangleF(x, y, w, 16 * S + 34 * S + hs.Sum() + 12 * S);
            Kit.Box(g, card, 14 * S);
            using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(Kit.Sent(title), Draw.fHead, ib, x + 18 * S, y + 16 * S);
            Hint(changesCanvas, new RectangleF(x, y, w, 40 * S), key);
            float ry = y + 16 * S + 34 * S;
            using (var muted = new SolidBrush(Theme.Muted))
                for (int i = 0; i < rows.Count; i++)
                {
                    int l = 3; if (rows[i].Length > 2) int.TryParse(rows[i][2], NumberStyles.Integer, CultureInfo.InvariantCulture, out l);
                    g.DrawString(rows[i][0], Draw.fBody, muted, new RectangleF(x + 18 * S, ry, lw - 8 * S, hs[i]));
                    using (var tb = new SolidBrush(l >= 0 && l <= 2 && l != 0 ? Theme.LvText(l) : Theme.Ink)) g.DrawString(rows[i][1], Draw.fBody, tb, new RectangleF(x + 18 * S + lw, ry, tw, hs[i]));
                    ry += hs[i];
                }
            return y + card.Height + 20 * S;
        }
    }
}
