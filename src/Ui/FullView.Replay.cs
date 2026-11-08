// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Time machine > Replay: drag through a day of history and see the network as it was at that minute -
    // the five dots, the numbers (each labelled with where it came from), what changed around then, and how it
    // compares with the rest of that day. Reads the history files only; nothing is measured for it.
    sealed partial class FullView
    {
        Panel replayPage; DB replayCanvas; DateTime? replayDay; History.Day replayData; int replayIdx = -1; Bitmap replayBmp; string replayKey = "";
        RectangleF replayTrack; bool replayDrag, replayLoading; double pendingAt = -1; double[] replayMed = new double[History.Cols];

        Panel BuildReplayPage()
        {
            replayPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            replayCanvas = new DB { Name = "replayCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            replayCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintReplay(e.Graphics); }; Wire(replayCanvas);
            MouseEventHandler seek = delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) { replayDrag = false; return; }
                var sp = replayCanvas.AutoScrollPosition; var pt = new PointF(e.X - sp.X, e.Y - sp.Y);
                var hit = replayTrack; hit.Inflate(0, 10 * S);
                if (e.Clicks > 0 || replayDrag) { if (!hit.Contains(pt) && !replayDrag) return; replayDrag = true; }
                else return;
                if (replayTrack.Width > 0) ReplayAt((pt.X - replayTrack.X) / replayTrack.Width);
            };
            replayCanvas.MouseDown += seek; replayCanvas.MouseMove += seek;
            replayCanvas.MouseUp += delegate { replayDrag = false; };
            replayPage.Controls.Add(replayCanvas);
            return replayPage;
        }

        void ReplayOpen()
        {
            if (replayDay == null) { replayDay = DateTime.Today; replayData = null; }
            if (replayData == null && !replayLoading)
            {
                // read the day's file off the UI thread - a full day is ~1,400 lines
                replayLoading = true; var day = replayDay.Value;
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    History.Day d = null; var med = new double[History.Cols];
                    try { d = History.Load(day); for (int c = 0; c < History.Cols; c++) med[c] = Median(d, c); } catch (Exception e) { Program.Log("replay: " + e.Message); }
                    try { BeginInvoke((Action)delegate { replayLoading = false; if (replayDay != day || d == null) { if (replayDay != day) ReplayOpen(); return; } replayData = d; replayMed = med; replayIdx = d.M.Count - 1; if (pendingAt >= 0) { double f = pendingAt; pendingAt = -1; ReplayAt(f); } replayCanvas.Invalidate(); }); } catch { replayLoading = false; }
                });
            }
            replayCanvas.Invalidate();
        }
        // f = 0..1 across the day (by time of day, like the chart)
        void ReplayAt(double f)
        {
            if (replayData == null) { pendingAt = f; ReplayOpen(); return; }
            var d = replayData; if (d.M.Count == 0) return;
            double sec = Math.Max(0, Math.Min(1, f)) * 86400; int best = 0; double bd = double.MaxValue;
            for (int i = 0; i < d.M.Count; i++) { double dd = Math.Abs(d.M[i][0] - sec); if (dd < bd) { bd = dd; best = i; } }
            if (best != replayIdx) { replayIdx = best; replayCanvas.Invalidate(); }
        }
        void ReplayStep(int by) { var d = replayData; if (d == null || d.M.Count == 0) return; replayIdx = Math.Max(0, Math.Min(d.M.Count - 1, replayIdx + by)); replayCanvas.Invalidate(); }
        void ReplayNextProblem(int dir)
        {
            var d = replayData; if (d == null) return;
            for (int i = replayIdx + dir; i >= 0 && i < d.M.Count; i += dir)
                if (!double.IsNaN(d.M[i][1]) && d.M[i][1] >= 1 && (i - dir < 0 || i - dir >= d.M.Count || double.IsNaN(d.M[i - dir][1]) || d.M[i - dir][1] < 1)) { replayIdx = i; replayCanvas.Invalidate(); return; }
        }

        static double Median(History.Day d, int col) { var v = d.M.Select(r => r[col]).Where(x => !double.IsNaN(x) && x >= 0).OrderBy(x => x).ToList(); return v.Count == 0 ? double.NaN : v[v.Count / 2]; }

        void PaintReplay(Graphics g)
        {
            g.TranslateTransform(replayCanvas.AutoScrollPosition.X, replayCanvas.AutoScrollPosition.Y);
            float x = 14 * S, y = 12 * S, w = replayCanvas.ClientSize.Width - 28 * S;
            Begin(replayCanvas);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
            {
                // day chips
                if (histDays == null || (DateTime.UtcNow - histDaysAt).TotalSeconds > 30) { histDays = History.Days(); histDaysAt = DateTime.UtcNow; }
                var days = histDays.ToList(); if (replayDay != null && !days.Contains(replayDay.Value)) days.Insert(0, replayDay.Value);
                float cx = x;
                foreach (var d0 in days.Take(8))
                {
                    var dd = d0; string t = dd == DateTime.Today ? "Today" : dd == DateTime.Today.AddDays(-1) ? "Yesterday" : dd.ToString("ddd d MMM", CultureInfo.CurrentCulture);
                    var ts = g.MeasureString(t, Draw.fBody); var cr = new RectangleF(cx, y, ts.Width + 22 * S, 28 * S); bool on = replayDay == dd;
                    Theme.Fill(g, cr, 14 * S, on ? Theme.Blue : Theme.Soft);
                    using (var tb = new SolidBrush(on ? Color.White : Theme.Ink)) g.DrawString(t, Draw.fBody, tb, cx + 11 * S, y + (28 * S - ts.Height) / 2);
                    Act(replayCanvas, cr, delegate { replayDay = dd; replayData = null; ReplayOpen(); });
                    cx += cr.Width + 8 * S;
                }
                y += 42 * S;
                var data = replayData;
                if (data == null || data.M.Count == 0)
                {
                    string t = replayLoading || (data == null && Cfg.History) ? "Reading that day..." : Cfg.History ? Voice.P("Nothing recorded for this day yet - history writes one line a minute, so give it a few minutes.", "No history for this day yet (one line is written per minute).") : Voice.P("History is off, so there's nothing to replay. Turn it on in Settings - it stays on this PC and deletes itself after the days you choose.", "History is off. Enable it in Settings to replay past minutes.");
                    g.DrawString(t, Draw.fBody, muted, new RectangleF(x, y, w, 60 * S));
                    replayTrack = RectangleF.Empty;
                    var n0 = new Size(0, (int)(y + 80 * S)); if (replayCanvas.AutoScrollMinSize != n0) replayCanvas.AutoScrollMinSize = n0;
                    return;
                }
                if (replayIdx < 0 || replayIdx >= data.M.Count) replayIdx = data.M.Count - 1;
                var row = data.M[replayIdx]; double sec = row[0];
                var at = replayDay.Value.Date.AddSeconds(sec);

                // the day chart with a cursor
                float chh = 196 * S; string key = replayDay.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "|" + (int)w + "|" + Theme.Dark + "|" + data.M.Count;
                if (replayBmp == null || replayKey != key)
                {
                    if (replayBmp != null) replayBmp.Dispose();
                    replayBmp = new Bitmap(Math.Max(1, (int)w), Math.Max(1, (int)chh)); replayKey = key;
                    using (var bg = Graphics.FromImage(replayBmp)) { Prep(bg); bg.Clear(Theme.Card); PaintDayChart(bg, data, 0, 0, w); }
                }
                g.DrawImageUnscaled(replayBmp, (int)x, (int)y);
                float px = x + 40 * S, pw = w - 60 * S, top = y + 34 * S, bandY = top + 150 * S - 18 * S;
                float cxr = px + (float)(sec / 86400.0) * pw;
                using (var cur = new Pen(Theme.Ink, 2 * S)) g.DrawLine(cur, cxr, top - 6 * S, cxr, bandY + 10 * S);
                y += chh + 4 * S;
                // the slider
                replayTrack = new RectangleF(px, y + 8 * S, pw, 6 * S);
                Theme.Fill(g, replayTrack, 3 * S, Theme.Track);
                Theme.Fill(g, new RectangleF(px, y + 8 * S, cxr - px, 6 * S), 3 * S, Theme.Blue);
                using (var kb = new SolidBrush(Theme.Card)) using (var kp = new Pen(Theme.Blue, 2.5f * S)) { g.FillEllipse(kb, cxr - 9 * S, y + 2 * S, 18 * S, 18 * S); g.DrawEllipse(kp, cxr - 9 * S, y + 2 * S, 18 * S, 18 * S); }
                Hint(replayCanvas, new RectangleF(px, y - 4 * S, pw, 30 * S), "replay.SLIDER");
                y += 32 * S;
                // step buttons
                cx = x;
                foreach (var b in new[] { new object[] { "< problem", 0 }, new object[] { "< 10 min", -10 }, new object[] { "< 1 min", -1 }, new object[] { "1 min >", 1 }, new object[] { "10 min >", 10 }, new object[] { "problem >", 0 } })
                {
                    string t = (string)b[0]; int by = (int)b[1]; var ts = g.MeasureString(t, Draw.fSmall); var br = new RectangleF(cx, y, ts.Width + 18 * S, 26 * S);
                    Theme.Stroke(g, br, 13 * S, Theme.Track, 1); g.DrawString(t, Draw.fSmall, ink, cx + 9 * S, y + (26 * S - ts.Height) / 2);
                    if (t.EndsWith("problem", StringComparison.Ordinal)) Act(replayCanvas, br, delegate { ReplayNextProblem(-1); });
                    else if (t.StartsWith("problem", StringComparison.Ordinal)) Act(replayCanvas, br, delegate { ReplayNextProblem(1); });
                    else Act(replayCanvas, br, delegate { ReplayStep(by); });
                    cx += br.Width + 6 * S;
                }
                y += 42 * S;

                // the minute itself
                Func<int, double> V = c => c < row.Length ? row[c] : double.NaN;
                int lvAll = double.IsNaN(V(1)) ? 3 : (int)V(1);
                double cpu = V(2), mem = V(3), down = V(4), gw = V(5), net = V(6), wifi = V(7), up = V(8), gwMax = V(9), netMax = V(10), loss = V(11), rx = V(12), mbD = V(13), mbU = V(14);
                string netName = replayIdx < data.Net.Count ? data.Net[replayIdx] : "";
                g.DrawString("AT " + at.ToString("HH:mm", CultureInfo.CurrentCulture) + " ON " + at.ToString("dddd d MMMM", CultureInfo.CurrentCulture).ToUpperInvariant() + (netName != "" && Cfg.Details ? "  ·  ROUTER " + Draw.Trunc(netName, 24).ToUpperInvariant() : ""), Draw.fTiny, muted, x, y); y += 22 * S;
                Func<double, string> ms = v => double.IsNaN(v) ? "no answer" : v.ToString("0", CultureInfo.CurrentCulture) + " ms";
                int pcLv = (cpu > 90 || mem > 92) ? 2 : (cpu > 75 || mem > 85) ? 1 : double.IsNaN(cpu) ? 3 : 0;
                int lanLv = double.IsNaN(gw) ? 2 : gw > 50 ? 2 : gw > 15 ? 1 : (wifi >= 0 && wifi < 35) ? 1 : 0;
                int netLv = double.IsNaN(net) ? 2 : (net > 150 || loss > 5) ? 2 : (net > 80 || loss > 1) ? 1 : 0;
                var snapAt = new Snap { Level = lvAll };
                snapAt.Path = new[] { pcLv, lanLv, netLv, 3, netLv };
                snapAt.PathMain = new[] { double.IsNaN(cpu) ? "-" : cpu.ToString("0", CultureInfo.CurrentCulture) + "% CPU", ms(gw), double.IsNaN(loss) ? "-" : loss.ToString("0.#", CultureInfo.CurrentCulture) + "% lost", "not kept", ms(net) };
                snapAt.PathSub = new[] { double.IsNaN(mem) ? "" : mem.ToString("0", CultureInfo.CurrentCulture) + "% memory", wifi >= 0 ? "Wi-Fi " + wifi.ToString("0", CultureInfo.CurrentCulture) + "%" : "router", "provider", "in history", "internet" };
                Draw.PathStrip(g, snapAt, new RectangleF(x, y + 6 * S, w, 80 * S), true);
                y += 100 * S;

                // in plain words
                string plain = lvAll == 0 ? "Everything was fine at " + at.ToString("HH:mm", CultureInfo.CurrentCulture) + ": router " + ms(gw) + ", internet " + ms(net) + "."
                    : pcLv == 2 ? "Your PC was the busy one at " + at.ToString("HH:mm", CultureInfo.CurrentCulture) + " (CPU " + cpu.ToString("0", CultureInfo.CurrentCulture) + "%)."
                    : lanLv >= 1 && lanLv >= netLv ? "The trouble at " + at.ToString("HH:mm", CultureInfo.CurrentCulture) + " was close to you - the router took " + ms(gw) + (wifi >= 0 ? " with Wi-Fi at " + wifi.ToString("0", CultureInfo.CurrentCulture) + "%" : "") + "."
                    : netLv >= 1 ? "Your side was fine at " + at.ToString("HH:mm", CultureInfo.CurrentCulture) + " (router " + ms(gw) + "); the internet was slow or dropping (" + ms(net) + (double.IsNaN(loss) ? "" : ", " + loss.ToString("0.#", CultureInfo.CurrentCulture) + "% lost") + ")."
                    : "Something needed attention at " + at.ToString("HH:mm", CultureInfo.CurrentCulture) + " - see what changed around then below.";
                float plh = Draw.Measure(plain, Draw.fName, (int)(w - 36 * S)).Height;
                var pr = new RectangleF(x, y, w, plh + 24 * S); Theme.Fill(g, pr, 12 * S, Theme.LvSoft(lvAll));
                using (var pb = new SolidBrush(lvAll == 0 || lvAll == 3 ? Theme.Ink : Theme.LvText(lvAll))) g.DrawString(plain, Draw.fName, pb, new RectangleF(x + 18 * S, y + 12 * S, w - 36 * S, plh + 6));
                y += pr.Height + 16 * S;

                // the numbers, each with where it came from; compared with the rest of that day
                g.DrawString("THAT MINUTE  ·  COMPARED WITH THE DAY'S MIDDLE VALUE", Draw.fTiny, muted, x, y); y += 22 * S;
                var rows = new List<object[]> {
                    new object[] { "Router answered in", ms(gw) + (Cfg.Details && !double.IsNaN(gwMax) ? " (worst " + ms(gwMax) + ")" : ""), "m", replayMed[5], gw, " ms" },
                    new object[] { "Internet answered in", ms(net) + (Cfg.Details && !double.IsNaN(netMax) ? " (worst " + ms(netMax) + ")" : ""), "m", replayMed[6], net, " ms" },
                    new object[] { "Lost replies", double.IsNaN(loss) ? "-" : loss.ToString("0.#", CultureInfo.CurrentCulture) + "%", "m", replayMed[11], loss, "%" },
                    new object[] { "Wi-Fi signal", wifi >= 0 ? wifi.ToString("0", CultureInfo.CurrentCulture) + "%" : "not on Wi-Fi / hidden", wifi >= 0 ? "w" : "u", replayMed[7], wifi, "%" },
                    new object[] { "Your traffic", (double.IsNaN(down) ? "?" : down.ToString("0.0", CultureInfo.CurrentCulture)) + " Mb/s down · " + (double.IsNaN(up) ? "?" : up.ToString("0.0", CultureInfo.CurrentCulture)) + " Mb/s up", "w", double.NaN, double.NaN, "" },
                    new object[] { "Processor / memory", (double.IsNaN(cpu) ? "?" : cpu.ToString("0", CultureInfo.CurrentCulture)) + "% / " + (double.IsNaN(mem) ? "?" : mem.ToString("0", CultureInfo.CurrentCulture)) + "%", "w", replayMed[2], cpu, "%" } };
                if (Cfg.Details) { rows.Add(new object[] { "Wi-Fi link rate", double.IsNaN(rx) ? "-" : rx.ToString("0", CultureInfo.CurrentCulture) + " Mb/s", "w", replayMed[12], rx, " Mb/s" }); rows.Add(new object[] { "Data that minute", (double.IsNaN(mbD) ? "?" : mbD.ToString("0.0", CultureInfo.CurrentCulture)) + " MB down · " + (double.IsNaN(mbU) ? "?" : mbU.ToString("0.0", CultureInfo.CurrentCulture)) + " MB up", "w", double.NaN, double.NaN, "" }); }
                float nameW = Math.Min(230 * S, w * 0.3f), cmpW = Math.Min(220 * S, w * 0.28f);
                foreach (var rr in rows)
                {
                    g.DrawString((string)rr[0], Draw.fBodyB, ink, x, y + 5 * S);
                    g.DrawString((string)rr[1], Draw.fBody, sub, x + nameW, y + 5 * S);
                    double med = (double)rr[3], val = (double)rr[4];
                    if (!double.IsNaN(med) && !double.IsNaN(val) && val >= 0)
                    {
                        string c = "day: " + med.ToString("0", CultureInfo.CurrentCulture) + ((string)rr[5]).TrimStart() ;
                        bool worse = ((string)rr[0]).StartsWith("Wi-Fi", StringComparison.Ordinal) ? val < med * 0.7 : val > Math.Max(med * 2, med + 10);
                        using (var cb = new SolidBrush(worse ? Theme.LvText(1) : Theme.Muted)) g.DrawString(c + (worse ? "  - worse than usual" : ""), Draw.fSmall, cb, x + w - cmpW - 140 * S, y + 7 * S);
                    }
                    Evidence.Pill(g, (string)rr[2], x + w - 130 * S, y + 6 * S, at);
                    Hint(replayCanvas, new RectangleF(x + w - 130 * S, y, 130 * S, 28 * S), Evidence.Key((string)rr[2]));
                    y += 30 * S; g.DrawLine(line, x, y, x + w, y);
                }
                y += 16 * S;

                // what changed around then
                var near = data.E.Where(e => Math.Abs((e.T - at).TotalMinutes) <= 15).OrderBy(e => e.T).ToList();
                g.DrawString("WHAT CHANGED 15 MINUTES EITHER SIDE", Draw.fTiny, muted, x, y); y += 22 * S;
                if (near.Count == 0) { g.DrawString(Voice.P("Nothing changed around then. Quiet minute.", "No changes within 15 minutes."), Draw.fBody, muted, x, y); y += 26 * S; }
                using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    foreach (var e in near)
                    {
                        g.DrawString(e.T.ToString("HH:mm:ss", CultureInfo.CurrentCulture), Draw.fMonoS, muted, x, y + 4 * S);
                        using (var b = new SolidBrush(Theme.Lv(e.Lv))) g.FillEllipse(b, x + 76 * S, y + 7 * S, 10 * S, 10 * S);
                        g.DrawString(e.Text, Draw.fBody, ink, new RectangleF(x + 96 * S, y + 2 * S, w - 96 * S, 20 * S), nowrap);
                        Hint(replayCanvas, new RectangleF(x + 76 * S, y, w - 76 * S, 26 * S), "kind." + e.Kind);
                        var te = e.T; Act(replayCanvas, new RectangleF(x, y, 70 * S, 26 * S), delegate { ReplayAt(te.TimeOfDay.TotalSeconds / 86400.0); });
                        y += 26 * S; g.DrawLine(line, x, y, x + w, y);
                    }
                y += 14 * S;
                g.DrawString("DNS is not kept in history (the grey dot). Every number is AdeeboMeter's own one-minute average from that day's file on this PC.", Draw.fSmall, muted, new RectangleF(x, y, w, 36 * S));
                y += 40 * S;
            }
            var need = new Size(0, (int)(y + 20 * S));
            if (replayCanvas.AutoScrollMinSize != need) replayCanvas.AutoScrollMinSize = need;
        }
    }
}
