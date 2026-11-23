// AdeeboMeter - on-demand Gamer incident review. Native WinForms/GDI+, no extra probes.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AdeeboMeter
{
    sealed partial class FullView
    {
        // Four connected experiences: session replay, lag vs local load, instant evidence,
        // and a chronological incident story. All operate on the existing bounded session.
        void GamerReview(bool evidenceOnly)
        {
            var gp = GamePing.Me;
            var ss = gp.Session ?? gp.LastSession;
            if (ss == null)
            {
                MessageBox.Show(this, "Start a game session first. No historical measurements are available.",
                    "Gamer evidence", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var rows = ss.All();
            List<Spike> spikes;
            DateTime end = ss.Open ? DateTime.Now : ss.End;
            lock (gp.Spikes)
                spikes = gp.Spikes.Where(z => z.Game == ss.GameId && z.T >= ss.Start && z.T <= end).ToList();
            var marks = ss.Marks.ToList();
            using (var f = new Form
            {
                Text = "AdeeboMeter - " + (evidenceOnly ? "Lag evidence" : "Game session replay"),
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(Sc(980), Sc(720)),
                MinimumSize = new Size(Sc(660), Sc(490)),
                BackColor = Theme.Card, ForeColor = Theme.Ink, Font = Draw.fBody
            })
            {
                var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(Sc(14)), BackColor = Theme.Card };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Sc(60)));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Sc(48)));
                var header = new Label
                {
                    Dock = DockStyle.Fill, Text = ss.GameName + "  /  " + ss.Server + "\r\n" +
                        ss.Start.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture) + " - " +
                        (ss.Open ? "NOW" : end.ToString("HH:mm", CultureInfo.CurrentCulture)) +
                        "  |  " + rows.Count + " samples  |  " + spikes.Count + " recorded spikes  |  " + marks.Count + " marks",
                    ForeColor = Theme.Ink, Font = Draw.fBodyB
                };
                root.Controls.Add(header, 0, 0);
                var chart = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card };
                int selected = -1;
                var detail = new TextBox
                {
                    Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                    BackColor = Theme.Card, ForeColor = Theme.Ink, Font = new Font("Consolas", 10),
                    BorderStyle = BorderStyle.FixedSingle
                };
                Action update = delegate
                {
                    detail.Text = GamerIncidentStory(ss, rows, spikes, marks, selected);
                    chart.Invalidate();
                };
                chart.Paint += delegate(object sender, PaintEventArgs e)
                {
                    DrawGamerReplay(e.Graphics, chart.ClientRectangle, rows, spikes, marks, selected);
                };
                chart.MouseClick += delegate(object sender, MouseEventArgs e)
                {
                    if (rows.Count == 0) return;
                    int left = Sc(52), right = Math.Max(left + 1, chart.ClientSize.Width - Sc(18));
                    double fraction = Math.Max(0, Math.Min(1, (double)(e.X - left) / (right - left)));
                    selected = Math.Max(0, Math.Min(rows.Count - 1, (int)Math.Round(fraction * (rows.Count - 1))));
                    update();
                };
                root.Controls.Add(chart, 0, 1);
                root.Controls.Add(detail, 0, 2);
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
                var copy = new Button { Text = "Copy redacted evidence", AutoSize = true };
                copy.Click += delegate
                {
                    try
                    {
                        string ssid; lock (M.L) ssid = M.Ssid;
                        Clipboard.SetText(GameSession.Redact(detail.Text, ssid));
                    }
                    catch (Exception ex) { CopyFailed(ex); }
                };
                var summary = new Button { Text = "Entire session", AutoSize = true };
                summary.Click += delegate { selected = -1; update(); };
                var close = new Button { Text = "Close", AutoSize = true };
                close.Click += delegate { f.Close(); };
                actions.Controls.Add(copy); actions.Controls.Add(summary); actions.Controls.Add(close);
                root.Controls.Add(actions, 0, 3);
                f.Controls.Add(root);
                update();
                f.ShowDialog(this);
            }
        }

        static string GamerIncidentStory(GameSession ss, List<GameSession.Sec> rows,
            List<Spike> spikes, List<DateTime> marks, int selected)
        {
            var sb = new StringBuilder();
            sb.AppendLine("GAME SESSION / INCIDENT STORY");
            sb.AppendLine("Source: AdeeboMeter's measured endpoint probes and PC counters.");
            sb.AppendLine("Endpoint probes are not in-game telemetry. No frame-time/FPS sensor is connected.");
            sb.AppendLine();
            if (rows.Count == 0)
            {
                sb.AppendLine("No samples recorded yet. A conclusion cannot be made.");
                return sb.ToString();
            }
            var game = rows.Select(r => r.Game).ToArray();
            var router = rows.Select(r => r.Router).ToArray();
            sb.AppendLine("Samples: " + rows.Count + "  |  Game endpoint avg " + GamerFmt(GameStat.Avg(game)) +
                " ms, p95 " + GamerFmt(GameStat.Pct(game, 0.95)) + " ms");
            sb.AppendLine("Router avg " + GamerFmt(GameStat.Avg(router)) + " ms; endpoint loss " +
                GamerFmt(GameStat.Loss(game)) + "%");
            sb.AppendLine("CPU is measured; GPU load, FPS and frame times are NOT measured.");
            sb.AppendLine();
            if (selected >= 0 && selected < rows.Count)
            {
                var r = rows[selected];
                sb.AppendLine("SELECTED " + r.T.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
                sb.AppendLine("Game endpoint " + GamerFmt(r.Game) + " ms  |  Router " +
                    GamerFmt(r.Router) + " ms  |  Internet check " + GamerFmt(r.Net) + " ms");
                sb.AppendLine("CPU " + GamerFmt(r.Cpu) + "%  |  Wi-Fi signal " +
                    (r.Signal < 0 ? "not available" : r.Signal + "%") +
                    "  |  Traffic down/up " + GamerFmt(r.Down) + "/" + GamerFmt(r.Up) + " Mb/s");
                var near = spikes.OrderBy(z => Math.Abs((z.T - r.T).TotalSeconds)).FirstOrDefault();
                if (near != null && Math.Abs((near.T - r.T).TotalSeconds) <= 3)
                    sb.AppendLine("Nearby spike: " + near.Blame + " (" + GamerFmt(near.Normal) + " -> " + GamerFmt(near.Ms) + " ms)");
                sb.AppendLine();
            }
            sb.AppendLine("LAG OR FPS? / EVIDENCE-BASED TRIAGE");
            double cpu = rows.Where(r => !double.IsNaN(r.Cpu)).Select(r => r.Cpu).DefaultIfEmpty(double.NaN).Max();
            if (spikes.Count > 0)
                sb.AppendLine("Network-side endpoint degradation was recorded (" + spikes.Count + " spikes). " +
                    "This supports a connection issue, but does not prove the game rendered slowly.");
            else
                sb.AppendLine("No endpoint spikes recorded in this session. This does NOT rule out game-server or network issues.");
            if (!double.IsNaN(cpu) && cpu >= 90)
                sb.AppendLine("High measured CPU load (peak " + GamerFmt(cpu) + "%) could contribute to local stutter; correlation is not causation.");
            else
                sb.AppendLine("CPU peak: " + GamerFmt(cpu) + "%. FPS drops cannot be diagnosed without actual frame-time measurements.");
            sb.AppendLine("Verdict: FPS vs network lag is INDETERMINATE without frame-time telemetry.");
            sb.AppendLine();
            sb.AppendLine("INCIDENT TIMELINE (recorded, not inferred)");
            var events = new List<Tuple<DateTime, string>>();
            foreach (var z in spikes)
                events.Add(Tuple.Create(z.T, "Endpoint spike " + GamerFmt(z.Normal) + " -> " +
                    GamerFmt(z.Ms) + " ms; classification: " + z.Blame));
            foreach (var t in marks) events.Add(Tuple.Create(t, "Player marked lag"));
            if (events.Count == 0) sb.AppendLine("No marked incidents or classified spikes in this session.");
            foreach (var ev in events.OrderBy(z => z.Item1).Take(100))
                sb.AppendLine(ev.Item1.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  " + ev.Item2);
            if (events.Count > 100) sb.AppendLine("Additional incidents omitted from this view.");
            sb.AppendLine();
            sb.AppendLine("Interpretation: simultaneous changes are clues, not proof of a single root cause.");
            return sb.ToString();
        }

        static string GamerFmt(double v)
        {
            return double.IsNaN(v) || double.IsInfinity(v) ? "n/a" :
                v.ToString("0.#", CultureInfo.CurrentCulture);
        }

        void DrawGamerReplay(Graphics g, Rectangle bounds, List<GameSession.Sec> rows,
            List<Spike> spikes, List<DateTime> marks, int selected)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Card);
            int left = Sc(52), top = Sc(25), right = Math.Max(left + 1, bounds.Width - Sc(18));
            int bottom = Math.Max(top + 1, bounds.Height - Sc(32));
            using (var grid = new Pen(Theme.Line))
            using (var ink = new SolidBrush(Theme.Sub))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = top + (bottom - top) * i / 4;
                    g.DrawLine(grid, left, y, right, y);
                }
                g.DrawString("Endpoint latency (ms)  |  red: spike  |  amber: mark",
                    Draw.fSmall, ink, left, 2);
                if (rows.Count == 0) { g.DrawString("No session samples yet", Draw.fBody, ink, left, top + Sc(24)); return; }
                var valid = rows.Select(z => z.Game).Where(z => !double.IsNaN(z) && !double.IsInfinity(z) && z >= 0).ToArray();
                double cap = valid.Length == 0 ? 100 : Math.Max(20, Math.Min(2000, valid.Max() * 1.15));
                g.DrawString(GamerFmt(cap), Draw.fSmall, ink, 1, top);
                g.DrawString("0", Draw.fSmall, ink, 15, bottom - Sc(14));
                using (var line = new Pen(Theme.Blue, Math.Max(1, Sc(2))))
                {
                    PointF? last = null;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        double v = rows[i].Game;
                        if (double.IsNaN(v) || double.IsInfinity(v) || v < 0) { last = null; continue; }
                        float x = left + (right - left) * (float)i / Math.Max(1, rows.Count - 1);
                        float y = bottom - (bottom - top) * (float)Math.Min(1, v / cap);
                        var point = new PointF(x, y);
                        if (last.HasValue) g.DrawLine(line, last.Value, point);
                        last = point;
                    }
                }
                DateTime start = rows[0].T, end = rows[rows.Count - 1].T;
                double span = Math.Max(1, (end - start).TotalSeconds);
                Action<DateTime, Color> marker = (t, color) =>
                {
                    if (t < start || t > end) return;
                    float x = left + (right - left) * (float)((t - start).TotalSeconds / span);
                    using (var pen = new Pen(color, Math.Max(1, Sc(2)))) g.DrawLine(pen, x, top, x, bottom);
                };
                foreach (var z in spikes.Take(300)) marker(z.T, Theme.Red);
                foreach (var t in marks.Take(300)) marker(t, Theme.Amber);
                if (selected >= 0 && selected < rows.Count) marker(rows[selected].T, Theme.Green);
                g.DrawString(start.ToString("HH:mm:ss", CultureInfo.CurrentCulture), Draw.fSmall, ink, left, bottom + Sc(4));
                var endLabel = end.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
                var size = g.MeasureString(endLabel, Draw.fSmall);
                g.DrawString(endLabel, Draw.fSmall, ink, right - size.Width, bottom + Sc(4));
            }
        }
    }
}
