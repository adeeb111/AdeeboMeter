// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Your Wi-Fi: the advisor, Wi-Fi health (works with location off), look-alike networks and the walk test.
    sealed partial class FullView
    {
        WifiAdvisor.Extra linkExtra;
        // ---------- walk test: only runs while you record a spot (20 pings to your own router in 10 s)
        sealed class Spot { public string Room; public double Avg, Jitter, Loss; public long Speed; public int Signal; }
        readonly List<Spot> spots = new List<Spot>(); FlowLayoutPanel walkBar; RoundBox walkRoom; PillButton walkRec; Label walkStatus; volatile bool walking; string walkRename;   // set while a room is being renamed

        void BuildWalkBar()
        {
            walkBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(0, Sc(64)), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), Sc(8), Sc(6)), WrapContents = true, Visible = false };
            walkRoom = new RoundBox(Sc(300), Draw.fBody, "Room name - e.g. Kitchen");
            walkRec = new PillButton("Record this spot (10 s)", PillButton.K.Primary); walkRec.Click += delegate { RecordSpot(); };
            walkRoom.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RecordSpot(); } };
            var done = new PillButton("Finish walk test", PillButton.K.Chip) { Margin = new Padding(Sc(8), Sc(3), 0, 0) };
            done.Click += delegate { EndRename(); walkBar.Visible = false; linkCanvas.Invalidate(); };
            walkStatus = new Label { AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fSmall, Margin = new Padding(Sc(10), Sc(10), 0, 0), Text = "Walk to a room, type its name, press Record. Nothing else runs meanwhile." };
            walkBar.Controls.Add(walkRoom); walkBar.Controls.Add(walkRec); walkBar.Controls.Add(done); walkBar.Controls.Add(walkStatus);
            linkPage.Controls.Add(walkBar);
        }
        void EndRename() { walkRename = null; walkRec.Text = "Record this spot (10 s)"; }
        void RecordSpot()
        {
            if (walking) return;
            if (walkRename != null)
            {
                string to = walkRoom.Box.Text.Trim(); var sp0 = spots.FirstOrDefault(z => z.Room == walkRename);
                if (to != "" && sp0 != null) { spots.RemoveAll(z => z.Room == to && z != sp0); sp0.Room = to; walkStatus.Text = "Renamed " + walkRename + " to " + to + "."; }
                EndRename(); walkRoom.Box.Text = ""; linkCanvas.Invalidate(); return;
            }
            string room = walkRoom.Box.Text.Trim(); if (room == "") room = "Spot " + (spots.Count + 1);
            string gw; NetworkInterface nic; lock (M.L) { gw = M.Gateway; nic = M.Primary; }
            if (gw == "") { walkStatus.Text = "No router to measure - are you connected?"; return; }
            walking = true; walkRec.Enabled = false; walkStatus.Text = "Measuring " + room + "... stay still for 10 seconds.";
            Task.Factory.StartNew(() =>
            {
                var ms = new List<double>();
                using (var p = new Ping())
                    for (int i = 0; i < 20; i++)
                    {
                        var t0 = DateTime.UtcNow;
                        try { var r = p.Send(gw, 800); ms.Add(r.Status == IPStatus.Success ? Math.Max(1, r.RoundtripTime) : double.NaN); } catch { ms.Add(double.NaN); }
                        int wait = 500 - (int)(DateTime.UtcNow - t0).TotalMilliseconds; if (wait > 0) System.Threading.Thread.Sleep(wait);
                    }
                var ok = ms.Where(v => !double.IsNaN(v)).ToList();
                long speed = 0; try { if (nic != null) speed = nic.Speed; } catch { }
                int sig; lock (M.L) sig = M.WifiSignal;
                var sp = new Spot { Room = room, Avg = ok.Count > 0 ? ok.Average() : double.NaN, Jitter = ok.Count > 2 ? ok.Zip(ok.Skip(1), (a, b) => Math.Abs(a - b)).Average() : double.NaN, Loss = 100.0 * (ms.Count - ok.Count) / ms.Count, Speed = speed, Signal = sig };
                try { BeginInvoke((Action)delegate { walking = false; walkRec.Enabled = true; spots.RemoveAll(s => s.Room == sp.Room); spots.Add(sp); walkRoom.Box.Text = ""; walkStatus.Text = "Recorded " + sp.Room + ". Walk to the next room."; linkCanvas.Invalidate(); }); } catch { walking = false; }
            });
        }
        static int SpotScore(Spot s)
        {
            int v = 100;
            if (double.IsNaN(s.Avg)) return 0;
            if (s.Avg > 50) v -= 40; else if (s.Avg > 15) v -= 20;
            if (!double.IsNaN(s.Jitter) && s.Jitter > 10) v -= 15;
            if (s.Loss > 0) v -= (int)Math.Min(40, s.Loss * 4);
            if (s.Speed > 0 && s.Speed < 50000000) v -= 15;
            if (s.Signal >= 0 && s.Signal < 40) v -= 15;
            return Math.Max(0, v);
        }

        float PaintWifiExtras(Graphics g, LinkSnap ls, float x, float y, float w)
        {
            var ex = linkExtra; if (ex == null) return y;
            // advisor
            var adv = ex.Advice.Select(a => new[] { a[0] == "3" ? "Location off" : a[0] == "0" ? "All good" : "Try this", a[1], a[0] == "3" ? "0" : a[0] }).ToList();
            y = TextCard(g, "WI-FI ADVISOR", adv, "link.ADVISOR", x, y, w);
            // health score (always; the only Wi-Fi view when location is off)
            var hcard = new RectangleF(x, y, w, 70 * S + ex.Health.Count * 22 * S);
            Theme.Stroke(g, hcard, 10 * S, Theme.Track, 1);
            int lv = ex.Score >= 80 ? 0 : ex.Score >= 55 ? 1 : 2;
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString("WI-FI HEALTH  ·  FROM WHAT WINDOWS NEVER HIDES (WORKS WITH LOCATION OFF)", Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(linkCanvas, new RectangleF(x, y, w, 26 * S), "link.HEALTH");
            using (var b = new SolidBrush(Theme.LvText(lv))) { g.DrawString(ex.Score + " / 100", Draw.fBig, b, x + 14 * S, y + 30 * S); g.DrawString(ex.ScoreText, Draw.fBodyB, b, x + 120 * S, y + 36 * S); }
            float ry = y + 66 * S, lw = Math.Min(260 * S, w * 0.45f);
            using (var muted = new SolidBrush(Theme.Muted))
                foreach (var r in ex.Health)
                {
                    g.DrawString(r[0], Draw.fBody, muted, x + 14 * S, ry);
                    using (var vb = new SolidBrush(r[2] != "0" ? Theme.LvText(int.Parse(r[2], CultureInfo.InvariantCulture)) : Theme.Ink)) g.DrawString(r[1], Draw.fBodyB, vb, x + 14 * S + lw, ry);
                    ry += 22 * S;
                }
            y += hcard.Height + 16 * S;
            if (ex.Twins.Count > 0) y = TextCard(g, "LOOK-ALIKE NETWORKS NEARBY", ex.Twins.Select(t => new[] { t[0] + "  ·  " + t[1], t[2], "1" }).ToList(), "link.TWINS", x, y, w);
            // walk test
            var wr = new RectangleF(x, y, w, (spots.Count == 0 ? 74 : 92 + spots.Count * 30) * S);
            Theme.Stroke(g, wr, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString("WALK TEST  ·  WHICH ROOMS ARE GOOD, WHICH AREN'T", Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(linkCanvas, new RectangleF(x, y, w, 26 * S), "link.WALK");
            var start = new RectangleF(x + 14 * S, y + 36 * S, 260 * S, 22 * S);
            using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(walkBar.Visible ? "Walk test is on - use the bar at the top" : spots.Count == 0 ? "Start walk test →" : "Add more rooms →", Draw.fBodyB, lb, start.X, start.Y);
            Act(linkCanvas, start, delegate { walkBar.Visible = true; walkRoom.Box.Focus(); linkCanvas.Invalidate(); });
            if (spots.Count > 0)
            {
                float sy = y + 66 * S, nameW = 120 * S;
                foreach (var sp in spots) nameW = Math.Max(nameW, g.MeasureString(sp.Room, Draw.fBodyB).Width + 16 * S);
                nameW = Math.Min(nameW, w * 0.35f);
                float actW = 150 * S, bx = x + 14 * S + nameW, bw = Math.Max(80 * S, (w - nameW - actW) * 0.40f);
                using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                using (var link = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
                foreach (var sp in spots.OrderByDescending(SpotScore).ToList())
                {
                    int sc = SpotScore(sp); int l = sc >= 80 ? 0 : sc >= 55 ? 1 : 2;
                    using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(sp.Room, Draw.fBodyB, ink, new RectangleF(x + 14 * S, sy + 4 * S, nameW - 8 * S, 20 * S), nowrap);
                    Hint(linkCanvas, new RectangleF(x + 14 * S, sy, nameW, 26 * S), "link.WALK");
                    Theme.Fill(g, new RectangleF(bx, sy + 6 * S, bw, 14 * S), 7 * S, Theme.Track);
                    Theme.Fill(g, new RectangleF(bx, sy + 6 * S, Math.Max(8 * S, bw * sc / 100f), 14 * S), 7 * S, Theme.Lv(l));
                    string facts = (double.IsNaN(sp.Avg) ? "no answer" : sp.Avg.ToString("0", CultureInfo.CurrentCulture) + " ms") + (double.IsNaN(sp.Jitter) ? "" : " · ±" + sp.Jitter.ToString("0", CultureInfo.CurrentCulture)) + " · loss " + sp.Loss.ToString("0", CultureInfo.CurrentCulture) + "%" + (sp.Speed > 0 ? " · " + (sp.Speed / 1e6).ToString("0", CultureInfo.CurrentCulture) + " Mb/s" : "") + (sp.Signal >= 0 ? " · " + sp.Signal + "%" : "");
                    float fx = bx + bw + 10 * S;
                    using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(facts, Draw.fSmall, mb, new RectangleF(fx, sy + 5 * S, x + w - actW - fx, 18 * S), nowrap);
                    // rename / remove
                    var spot = sp; float ax = x + w - actW + 10 * S;
                    var rn = g.MeasureString("Rename", Draw.fSmall); var rr = new RectangleF(ax, sy + 4 * S, rn.Width, 20 * S);
                    g.DrawString("Rename", Draw.fSmall, link, ax, sy + 5 * S);
                    Act(linkCanvas, rr, delegate { walkRename = spot.Room; walkBar.Visible = true; walkRoom.Box.Text = spot.Room; walkRoom.Box.Focus(); walkRoom.Box.SelectAll(); walkRec.Text = "Save the new name"; walkStatus.Text = "Type the new name for " + spot.Room + ", then Save."; linkCanvas.Invalidate(); });
                    var dr = new RectangleF(ax + rn.Width + 14 * S, sy + 4 * S, g.MeasureString("Remove", Draw.fSmall).Width, 20 * S);
                    using (var red = new SolidBrush(Theme.LvText(2))) g.DrawString("Remove", Draw.fSmall, red, dr.X, sy + 5 * S);
                    Act(linkCanvas, dr, delegate { spots.Remove(spot); if (walkRename == spot.Room) EndRename(); if (walkStatus != null) walkStatus.Text = "Removed " + spot.Room + "."; linkCanvas.Invalidate(); });
                    sy += 30 * S;
                }
                if (spots.Count >= 2)
                {
                    var best = spots.OrderByDescending(SpotScore).First(); var worst = spots.OrderBy(SpotScore).First();
                    string verdict = SpotScore(worst) >= 80 ? "Every room you measured is good." : "Best: " + best.Room + ". Weakest: " + worst.Room + " - move the router toward it, or add a mesh point / extender between them.";
                    using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(verdict, Draw.fBody, sb, x + 14 * S, sy + 2 * S);
                }
            }
            return y + wr.Height + 16 * S;
        }
        // a card of { label, text, level } rows where the text wraps
        float TextCard(Graphics g, string title, List<string[]> rows, string key, float x, float y, float w)
        {
            float tw = w - 160 * S; var hs = rows.Select(r => Draw.Measure(r[1], Draw.fBody, (int)tw).Height + 10 * S).ToList();
            var card = new RectangleF(x, y, w, 40 * S + hs.Sum());
            Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(title, Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(linkCanvas, new RectangleF(x, y, w, 26 * S), key);
            float ry = y + 34 * S;
            for (int i = 0; i < rows.Count; i++)
            {
                int l; int.TryParse(rows[i][2], NumberStyles.Integer, CultureInfo.InvariantCulture, out l);
                using (var lb = new SolidBrush(Theme.LvText(l))) g.DrawString(rows[i][0], Draw.fBodyB, lb, new RectangleF(x + 14 * S, ry, 130 * S, 20 * S));
                using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(rows[i][1], Draw.fBody, ink, new RectangleF(x + 146 * S, ry, tw, hs[i]));
                ry += hs[i];
            }
            return y + card.Height + 16 * S;
        }
    }
}
