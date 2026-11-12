// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

#pragma warning disable CA1861   // the steppers' value lists: small, built only while a lab is painted
namespace AdeeboMeter
{
    // Learn › Labs: play with how networks behave, fed with this PC's own numbers wherever there are some.
    // Every lab is local arithmetic (LabSim) - only "DNS, for real" and "Check for CGNAT" send anything, and only when
    // pressed. Each lab can open full screen. Nothing runs while a lab isn't on screen.
    sealed partial class FullView
    {
        static readonly string[][] Labs = {
            new[] { "tcp", "TCP congestion control", "Watch the window grow, back off and recover - Reno and CUBIC." },
            new[] { "mathis", "Loss → the most TCP can do", "Why 1% loss caps a download, whatever your plan." },
            new[] { "bloat", "Bufferbloat", "A full queue: the download is fine, the ping isn't." },
            new[] { "jitter", "A call's jitter buffer", "Too small cuts the sound, too big adds delay." },
            new[] { "airtime", "Wi-Fi airtime", "Why one slow device slows everyone." },
            new[] { "dns", "How a name gets found", "Root, .com, the site's own server - and the cache." },
            new[] { "nat", "NAT, and why inbound fails", "Your private address, the router's public one, CGNAT." },
            new[] { "mtu", "MTU and black holes", "When big packets vanish and small ones work." },
            new[] { "route", "Which route wins", "Longest prefix first - on your own route table." },
            new[] { "game", "The game latency budget", "Why a 30 ms ping can feel like 80." },
            new[] { "qos", "QoS queues", "Why a marked call packet jumps the line - or doesn't." },
            new[] { "quiz", "Subnetting practice", "Network, broadcast, hosts - check yourself." } };

        readonly Dictionary<string, double> labV = new Dictionary<string, double>(); readonly Dictionary<string, string> labS = new Dictionary<string, string>();
        double LV(string k, double d) { double v; return labV.TryGetValue(k, out v) ? v : d; }
        string LS(string k, string d) { string v; return labS.TryGetValue(k, out v) ? v : d; }
        Control labHost; Form labWin; System.Windows.Forms.Timer labTimer; int labShown = -1;   // TCP: rounds drawn so far while playing (-1 = all)
        readonly Dictionary<int, string> tcpEvents = new Dictionary<int, string>();
        List<string[]> labDns; bool labBusy; string labCg = ""; int quizSeed = Environment.TickCount & 0xFFFF, quizRight, quizAll; string quizPick = "";

        void LabRefresh() { if (labHost != null && !labHost.IsDisposed) labHost.Invalidate(); if (learnInfo != null) learnInfo.Invalidate(); }

        float PaintLabList(Graphics g, float x, float y, float w, Brush ink, Brush muted, StringFormat nowrap, Func<string, bool> hit)
        {
            var list = Labs.Where(l => hit(l[1]) || hit(l[2]) || hit("lab")).ToList(); if (list.Count == 0) return y;
            g.DrawString(L.T("LABS - PLAY WITH IT"), Draw.fTiny, muted, x, y + 6 * S); y += 26 * S;
            foreach (var l in list) y = LearnRow(g, "lab:" + l[0], l[1], l[2], x, y, w, ink, muted, nowrap);
            return y + 8 * S;
        }

        // ---------- the painted controls every lab uses
        float LabHead(Control h, Graphics g, string id, float x, float y, float w)
        {
            var lab = Labs.First(l => l[0] == id);
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(L.T(lab[1]), Draw.fTitle, ink, x, y);
            if (h == learnInfo)
            {
                string fs = "Full screen"; var fz = g.MeasureString(fs, Draw.fBodyB); var fr = new RectangleF(x + w - fz.Width - 24 * S, y + 2 * S, fz.Width + 24 * S, 30 * S);
                Theme.Fill(g, fr, 15 * S, Theme.Soft); using (var tb = new SolidBrush(Theme.Ink)) g.DrawString(fs, Draw.fBodyB, tb, fr.X + 12 * S, fr.Y + (30 * S - fz.Height) / 2);
                Act(h, fr, delegate { LabFull(id); });
            }
            y += 38 * S;
            using (var sub = new SolidBrush(Theme.Sub)) g.DrawString(L.T(lab[2]), Draw.fBody, sub, x, y);
            return y + 30 * S;
        }
        // a value with − and + : steps through the list
        float Stepper(Control h, Graphics g, string key, string label, double[] steps, double def, Func<double, string> fmt, float x, float y)
        {
            double v = LV(key, def); int i = Array.IndexOf(steps, v); if (i < 0) { i = 0; for (int k = 0; k < steps.Length; k++) if (Math.Abs(steps[k] - v) < Math.Abs(steps[i] - v)) i = k; }
            string val = fmt(v); float lw = g.MeasureString(L.T(label), Draw.fSmall).Width, vw = Math.Max(54 * S, g.MeasureString(val, Draw.fBodyB).Width + 10 * S);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T(label), Draw.fSmall, mb, x, y);
            float by = y + 18 * S; var minus = new RectangleF(x, by, 26 * S, 26 * S); var plus = new RectangleF(x + 26 * S + vw, by, 26 * S, 26 * S);
            Theme.Fill(g, minus, 6 * S, Theme.Soft); Theme.Fill(g, plus, 6 * S, Theme.Soft);
            using (var tb = new SolidBrush(Theme.Ink)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            { g.DrawString("−", Draw.fBodyB, tb, minus, c); g.DrawString("+", Draw.fBodyB, tb, plus, c); g.DrawString(val, Draw.fBodyB, tb, new RectangleF(minus.Right, by, vw, 26 * S), c); }
            int ii = i; Act(h, minus, delegate { labV[key] = steps[Math.Max(0, ii - 1)]; labShown = -1; LabRefresh(); }); Act(h, plus, delegate { labV[key] = steps[Math.Min(steps.Length - 1, ii + 1)]; labShown = -1; LabRefresh(); });
            return Math.Max(lw, plus.Right - x) + 22 * S;
        }
        float Choice(Control h, Graphics g, string key, string label, string[][] opts, string def, float x, float y)
        {
            string cur = LS(key, def); float cx = x;
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T(label), Draw.fSmall, mb, x, y);
            foreach (var o in opts)
            {
                string t = L.T(o[1]); var ts = g.MeasureString(t, Draw.fSmall); var r = new RectangleF(cx, y + 18 * S, ts.Width + 20 * S, 26 * S); bool on = o[0] == cur;
                Theme.Fill(g, r, 13 * S, on ? Theme.BlueSoft : Theme.Soft); if (on) Theme.Stroke(g, r, 13 * S, Theme.Blue, 1);
                using (var tb = new SolidBrush(on ? Theme.Ink : Theme.Sub)) g.DrawString(t, Draw.fSmall, tb, cx + 10 * S, r.Y + (26 * S - ts.Height) / 2);
                string v = o[0]; Act(h, r, delegate { labS[key] = v; labShown = -1; LabRefresh(); }); cx = r.Right + 6 * S;
            }
            return cx - x + 16 * S;
        }
        float Button(Control h, Graphics g, string t, bool primary, Action a, float x, float y)
        {
            t = L.T(t); var ts = g.MeasureString(t, Draw.fBodyB); var r = new RectangleF(x, y, ts.Width + 28 * S, 32 * S);
            Theme.Fill(g, r, 16 * S, primary ? Theme.Blue : Theme.Soft); using (var tb = new SolidBrush(primary ? Color.White : Theme.Ink)) g.DrawString(t, Draw.fBodyB, tb, x + 14 * S, y + (32 * S - ts.Height) / 2);
            if (a != null) Act(h, r, a); return r.Width + 10 * S;
        }
        static float Big(Graphics g, string t, int lv, float x, float y, float w) { using (var b = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : lv == 0 ? Theme.LvText(0) : Theme.Ink)) g.DrawString(t, Draw.fName, b, new RectangleF(x, y, w, 40 * S)); return y + Draw.Measure(t, Draw.fName, (int)w).Height + 8 * S; }
        static string N0(double v) { return double.IsInfinity(v) ? "no limit" : v.ToString(v < 10 ? "0.#" : "N0", CultureInfo.CurrentCulture); }

        // ---------- full screen: the same lab in its own maximised window (Esc closes it)
        void LabFull(string id)
        {
            if (labWin != null && !labWin.IsDisposed) { labWin.Activate(); return; }
            var f = new Form { Text = "AdeeboMeter · " + Labs.First(l => l[0] == id)[1], WindowState = FormWindowState.Maximized, BackColor = Theme.Card, KeyPreview = true, Icon = Icon, ShowInTaskbar = true };
            var db = new DB { Name = "labCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            db.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); e.Graphics.TranslateTransform(db.AutoScrollPosition.X, db.AutoScrollPosition.Y); Begin(db); float yy = PaintLab(db, e.Graphics, id, 28 * S, 20 * S, db.ClientSize.Width - 56 * S); Need(db, yy + 20 * S); };
            Wire(db); f.Controls.Add(db); labHost = db; labWin = f;
            f.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) f.Close(); };
            f.FormClosed += delegate { labHost = null; labWin = null; f.Dispose(); };
            f.Show(this);
        }
        void LabPlay(bool on)
        {
            if (labTimer == null) { labTimer = new System.Windows.Forms.Timer { Interval = 66 }; labTimer.Tick += delegate { bool vis = (learnPage.Visible && Visible && WindowState != FormWindowState.Minimized) || (labWin != null && !labWin.IsDisposed && labWin.Visible); if (!vis || labShown < 0) { labTimer.Enabled = false; return; } labShown++; if (labShown >= (int)LV("tcp.rounds", 120)) { labShown = -1; labTimer.Enabled = false; } LabRefresh(); }; }
            labShown = on ? 0 : -1; labTimer.Enabled = on; LabRefresh();
        }

        float PaintLab(Control h, Graphics g, string id, float x, float y, float w)
        {
            y = LabHead(h, g, id, x, y, w);
            switch (id)
            {
                case "tcp": return LabTcp(h, g, x, y, w);
                case "mathis": return LabMathis(h, g, x, y, w);
                case "bloat": return LabBloat(h, g, x, y, w);
                case "jitter": return LabJitter(h, g, x, y, w);
                case "airtime": return LabAir(h, g, x, y, w);
                case "dns": return LabDns(h, g, x, y, w);
                case "nat": return LabNat(h, g, x, y, w);
                case "mtu": return LabMtu(h, g, x, y, w);
                case "route": return LabRoute(h, g, x, y, w);
                case "game": return LabGame(h, g, x, y, w);
                case "qos": return LabQos(h, g, x, y, w);
                case "quiz": return LabQuiz(h, g, x, y, w);
            }
            return y;
        }

        // ================================================================== TCP congestion control
        float LabTcp(Control h, Graphics g, float x, float y, float w)
        {
            float cx = x;
            cx += Choice(h, g, "tcp.algo", "Algorithm", new[] { new[] { "cubic", "CUBIC (Windows' default)" }, new[] { "reno", "Reno" }, new[] { "both", "Both" } }, "cubic", cx, y);
            cx += Choice(h, g, "tcp.click", "A click on the graph adds", new[] { new[] { "soft", "soft loss (3 dup ACKs)" }, new[] { "hard", "hard loss (timeout)" } }, "soft", cx, y);
            y += 56 * S; cx = x;
            cx += Stepper(h, g, "tcp.mbps", "Link", new double[] { 1, 5, 10, 20, 50, 100, 200, 500, 1000 }, 100, v => N0(v) + " Mbps", cx, y);
            cx += Stepper(h, g, "tcp.rtt", "Round trip", new double[] { 5, 10, 20, 40, 80, 150, 300 }, 40, v => N0(v) + " ms", cx, y);
            cx += Stepper(h, g, "tcp.buf", "Router buffer", new double[] { 0.1, 0.25, 0.5, 1, 2, 4, 8 }, 1, v => v.ToString("0.##", CultureInfo.CurrentCulture) + "× BDP", cx, y);
            cx += Stepper(h, g, "tcp.loss", "Random loss", new double[] { 0, 0.001, 0.01, 0.1, 0.5, 1, 2 }, 0, v => v.ToString("0.###", CultureInfo.CurrentCulture) + "%", cx, y);
            cx += Stepper(h, g, "tcp.rounds", "Length", new double[] { 60, 120, 240, 480 }, 120, v => N0(v) + " RTTs", cx, y);
            y += 58 * S;
            string algo = LS("tcp.algo", "cubic"); int rounds = (int)LV("tcp.rounds", 120);
            Func<string, List<LabSim.Step>> run = a => { var p = new LabSim.TcpIn { Algo = a, Mbps = LV("tcp.mbps", 100), RttMs = LV("tcp.rtt", 40), BufferBdp = LV("tcp.buf", 1), LossPct = LV("tcp.loss", 0), Rounds = rounds }; foreach (var kv in tcpEvents) p.Events[kv.Key] = kv.Value; return LabSim.Tcp(p); };
            var main = run(algo == "reno" ? "reno" : "cubic"); var other = algo == "both" ? run("reno") : null;
            double bdp = LabSim.Bdp(LV("tcp.mbps", 100), LV("tcp.rtt", 40)), cap = bdp * (1 + LV("tcp.buf", 1));
            int shown = labShown < 0 ? rounds : Math.Min(rounds, labShown + 1);
            var real = LabSim.LastRealCwnd;
            double ymax = Math.Max(cap * 1.15, Math.Max(main.Max(s => s.Cwnd), other != null ? other.Max(s => s.Cwnd) : 0)) * 1.05;
            var plot = new RectangleF(x + 46 * S, y, w - 56 * S, Math.Max(260 * S, Math.Min(460 * S, w * 0.38f)));
            Func<double, float> X = r => plot.X + (float)(r / Math.Max(1, rounds - 1)) * plot.Width; Func<double, float> Y = v => plot.Bottom - (float)(Math.Min(v, ymax) / ymax) * plot.Height;
            // phases shaded under the main algorithm
            for (int i = 0; i < shown; i++)
            {
                var st = main[i]; Color pc = st.Phase == "slow start" ? Theme.C("#8250DF") : st.Phase == "fast recovery" ? Theme.Amber : Theme.Blue;
                using (var b = new SolidBrush(Color.FromArgb(Theme.Dark ? 40 : 22, pc))) g.FillRectangle(b, X(i) - plot.Width / rounds / 2, plot.Y, plot.Width / Math.Max(1, rounds - 1), plot.Height);
            }
            using (var ax = new Pen(Theme.Line)) { g.DrawLine(ax, plot.X, plot.Bottom, plot.Right, plot.Bottom); g.DrawLine(ax, plot.X, plot.Y, plot.X, plot.Bottom); }
            using (var dash = new Pen(Theme.Green, 1.5f * S) { DashStyle = DashStyle.Dash }) g.DrawLine(dash, plot.X, Y(bdp), plot.Right, Y(bdp));
            using (var dash = new Pen(Theme.Red, 1.5f * S) { DashStyle = DashStyle.Dot }) g.DrawLine(dash, plot.X, Y(cap), plot.Right, Y(cap));
            using (var mb = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("link full (bandwidth × round trip)") + " · " + N0(bdp) + " " + L.T("packets"), Draw.fTiny, mb, plot.X + 6 * S, Y(bdp) - 14 * S);
                g.DrawString(L.T("buffer full - packets dropped"), Draw.fTiny, mb, plot.X + 6 * S, Y(cap) - 14 * S);
                g.DrawString(N0(ymax), Draw.fTiny, mb, x, plot.Y); g.DrawString("0", Draw.fTiny, mb, x + 30 * S, plot.Bottom - 10 * S);
                g.DrawString(L.T("window (packets)"), Draw.fTiny, mb, x, plot.Y - 16 * S); g.DrawString(L.T("round trips"), Draw.fTiny, mb, plot.Right - 80 * S, plot.Bottom + 4 * S);
            }
            Action<List<LabSim.Step>, Color> line = (s, c) =>
            {
                var pts = s.Take(shown).Select(st => new PointF(X(st.Round), Y(st.Cwnd))).ToArray(); if (pts.Length > 1) using (var p = new Pen(c, 2.5f * S)) g.DrawLines(p, pts);
                var ssPts = s.Take(shown).Where(st => !double.IsInfinity(st.Ssthresh)).Select(st => new PointF(X(st.Round), Y(st.Ssthresh))).ToArray();
                if (ssPts.Length > 1) using (var p = new Pen(Color.FromArgb(150, c), 1f * S) { DashStyle = DashStyle.Dash }) g.DrawLines(p, ssPts);
                foreach (var st in s.Take(shown).Where(z => z.Loss != "")) using (var b = new SolidBrush(st.Loss == "hard" ? Theme.Red : Theme.Amber)) g.FillEllipse(b, X(st.Round) - 4 * S, Y(st.Cwnd) - 4 * S, 8 * S, 8 * S);
            };
            if (other != null) line(other, Theme.C("#0E8A9E"));
            line(main, algo == "reno" ? Theme.C("#0E8A9E") : Theme.C("#8250DF"));
            if (real != null && real.Count > 1)
            {
                var pts = real.Take(rounds).Select((v, i) => new PointF(X(i), Y(v))).ToArray();
                using (var p = new Pen(Theme.Green, 2f * S)) g.DrawLines(p, pts);
            }
            // a click adds a loss at that round trip
            var hitR = plot; Act(h, hitR, delegate { var pt = h.PointToClient(Cursor.Position); var sc = h is ScrollableControl ? ((ScrollableControl)h).AutoScrollPosition : Point.Empty; int r = (int)Math.Round((pt.X - sc.X - plot.X) / plot.Width * (rounds - 1)); if (r >= 0 && r < rounds) { if (!tcpEvents.Remove(r)) tcpEvents[r] = LS("tcp.click", "soft"); } LabRefresh(); });
            y = plot.Bottom + 26 * S;
            // legend and controls
            float lx = x;
            using (var mb = new SolidBrush(Theme.Sub))
                foreach (var it in new[] { Tuple.Create(algo == "reno" ? "Reno" : "CUBIC", algo == "reno" ? Theme.C("#0E8A9E") : Theme.C("#8250DF")), Tuple.Create(other != null ? "Reno" : "", Theme.C("#0E8A9E")), Tuple.Create(real != null && real.Count > 1 ? "your real download (estimated)" : "", Theme.Green), Tuple.Create("slow start", Theme.C("#8250DF")), Tuple.Create("fast recovery", Theme.Amber), Tuple.Create("loss: soft / hard", Theme.Red) })
                {
                    if (it.Item1 == "") continue; using (var b = new SolidBrush(it.Item2)) g.FillRectangle(b, lx, y + 4 * S, 12 * S, 8 * S); g.DrawString(L.T(it.Item1), Draw.fSmall, mb, lx + 16 * S, y); lx += g.MeasureString(L.T(it.Item1), Draw.fSmall).Width + 34 * S;
                }
            y += 26 * S; float bx = x;
            bx += Button(h, g, labShown >= 0 ? "Show all" : "Play", true, delegate { LabPlay(labShown < 0); }, bx, y);
            bx += Button(h, g, "Clear my losses", false, delegate { tcpEvents.Clear(); LabRefresh(); }, bx, y);
            y += 44 * S;
            // what it means: totals, and the phase at the end of what's drawn
            var last = main[Math.Max(0, shown - 1)]; double util = main.Take(shown).Average(s => s.Util), q = main.Take(shown).Average(s => s.QueueMs); int losses = main.Take(shown).Count(s => s.Loss != "");
            var rows = new List<string[]> {
                new[] { "Throughput", (util * 100).ToString("0", CultureInfo.CurrentCulture) + "% of the link (" + (util * LV("tcp.mbps", 100)).ToString("0.#", CultureInfo.CurrentCulture) + " Mbps on average)", util < 0.7 ? "1" : "0" },
                new[] { "Extra delay from the queue", q.ToString("0", CultureInfo.CurrentCulture) + " ms on average" + (q > LV("tcp.rtt", 40) ? " - more than the round trip itself: bufferbloat" : ""), q > LV("tcp.rtt", 40) ? "1" : "3" },
                new[] { "Losses", losses + (losses == 0 ? "" : " - each one cuts the window"), "3" },
                new[] { "Now: " + last.Phase, last.Phase == "slow start" ? "The window doubles every round trip until it hits the threshold or a loss. Fast, but it overshoots." : last.Phase == "fast recovery" ? "Three duplicate ACKs said one packet went missing. The window is cut (Reno halves it, CUBIC keeps 70%) and the missing packet is resent - no restart." : (algo == "reno" ? "Reno adds one packet per round trip - the straight slope of the sawtooth." : "CUBIC grows by time since the last loss: fast far from the old maximum, slow and careful near it, then probes past it."), "3" } };
            if (tcpEvents.ContainsValue("hard")) rows.Add(new[] { "A timeout", "No ACKs at all, so TCP assumes the worst: the window goes back to 1 and slow start begins again. Much more costly than a soft loss.", "1" });
            if (real != null && real.Count > 1) rows.Add(new[] { "Your real download", "Estimated from the packet journey's last capture: data sent but not yet acknowledged, per round trip (" + N0(LabSim.LastRealRttMs) + " ms). Seen from one end, so it's a close guess, not the sender's own number.", "3" });
            return InfoCardOn(h, g, "WHAT IT SHOWS", rows, null, x, y, w);
        }

        // ================================================================== loss -> the most TCP can do
        float LabMathis(Control h, Graphics g, float x, float y, float w)
        {
            double myRtt = M.NetPing.Avg(30), myLoss = 100 * M.Loss.Avg(20);
            float cx = x;
            cx += Stepper(h, g, "m.rtt", "Round trip", new double[] { 5, 10, 20, 40, 80, 150, 300 }, 40, v => N0(v) + " ms", cx, y);
            cx += Stepper(h, g, "m.loss", "Loss", new double[] { 0.001, 0.01, 0.05, 0.1, 0.5, 1, 2, 5 }, 1, v => v.ToString("0.###", CultureInfo.CurrentCulture) + "%", cx, y);
            y += 60 * S;
            double rtt = LV("m.rtt", 40), loss = LV("m.loss", 1), mb = LabSim.MathisMbps(rtt, loss);
            y = Big(g, "At most " + N0(mb) + " Mbps per download", mb < 25 ? 2 : mb < 100 ? 1 : 0, x, y, w);
            y = Lead2(g, "One TCP connection can't go faster than about MSS ÷ round trip × 1.22 ÷ √loss, however fast your plan is - every loss halves its window and it climbs back one packet per round trip. Farther servers suffer more, which is why the same loss hurts a far download more than a near one.", x, y, w);
            // the curve: max speed against loss, for this round trip
            var plot = new RectangleF(x + 46 * S, y, w - 56 * S, 200 * S); double[] ls = { 0.001, 0.003, 0.01, 0.03, 0.1, 0.3, 1, 3, 10 };
            double top = LabSim.MathisMbps(rtt, 0.001); Func<double, float> Y = v => plot.Bottom - (float)(Math.Log10(Math.Max(0.1, v)) + 1) / (float)(Math.Log10(top) + 1) * plot.Height;
            Func<double, float> X = l => plot.X + (float)((Math.Log10(l) + 3) / 4) * plot.Width;
            using (var ax = new Pen(Theme.Line)) { g.DrawLine(ax, plot.X, plot.Bottom, plot.Right, plot.Bottom); g.DrawLine(ax, plot.X, plot.Y, plot.X, plot.Bottom); }
            var pts = ls.Select(l => new PointF(X(l), Y(LabSim.MathisMbps(rtt, l)))).ToArray(); using (var p = new Pen(Theme.Blue, 2.5f * S)) g.DrawLines(p, pts);
            using (var b = new SolidBrush(Theme.Red)) g.FillEllipse(b, X(loss) - 5 * S, Y(mb) - 5 * S, 10 * S, 10 * S);
            using (var mbr = new SolidBrush(Theme.Muted)) { foreach (var l in new[] { 0.001, 0.01, 0.1, 1, 10 }) g.DrawString(l.ToString("0.###", CultureInfo.CurrentCulture) + "%", Draw.fTiny, mbr, X(l) - 10 * S, plot.Bottom + 4 * S); g.DrawString(N0(top) + " Mbps", Draw.fTiny, mbr, x, plot.Y); g.DrawString(L.T("loss"), Draw.fTiny, mbr, plot.Right - 40 * S, plot.Bottom + 18 * S); }
            y = plot.Bottom + 40 * S;
            if (!double.IsNaN(myRtt)) y = InfoCardOn(h, g, "YOUR LINE RIGHT NOW", new List<string[]> { new[] { "Round trip / loss", myRtt.ToString("0", CultureInfo.CurrentCulture) + " ms · " + (double.IsNaN(myLoss) ? "-" : myLoss.ToString("0.#", CultureInfo.CurrentCulture) + "%") + " (to the internet check)", "3" }, new[] { "Most one download can do", double.IsNaN(myLoss) || myLoss <= 0 ? "no loss seen, so no limit from loss" : N0(LabSim.MathisMbps(myRtt, myLoss)) + " Mbps - quote this in the ISP report if it's below your plan", double.IsNaN(myLoss) || myLoss <= 0 ? "0" : "1" } }, null, x, y, w);
            return y;
        }

        // ================================================================== bufferbloat
        float LabBloat(Control h, Graphics g, float x, float y, float w)
        {
            float cx = x;
            cx += Stepper(h, g, "b.link", "Your upload", new double[] { 5, 10, 20, 50, 100, 500 }, 20, v => N0(v) + " Mbps", cx, y);
            cx += Stepper(h, g, "b.load", "A backup sends", new double[] { 0, 10, 50, 90, 100, 150 }, 100, v => N0(v) + "% of it", cx, y);
            cx += Stepper(h, g, "b.buf", "Router buffer holds", new double[] { 10, 50, 100, 250, 500, 1000, 2000 }, 500, v => N0(v) + " ms", cx, y);
            cx += Choice(h, g, "b.smart", "Queue", new[] { new[] { "fifo", "first come, first served" }, new[] { "smart", "smart queue (fq_codel / SQM)" } }, "fifo", cx, y);
            y += 60 * S;
            double link = LV("b.link", 20), load = link * LV("b.load", 100) / 100, buf = LV("b.buf", 500); bool smart = LS("b.smart", "fifo") == "smart";
            var o = LabSim.Bloat(link, load, buf, 20, smart);
            // the queue as a tube filling up
            var tube = new RectangleF(x, y, w * 0.6f, 40 * S); Theme.Stroke(g, tube, 8 * S, Theme.Line, 1.5f);
            float fill = (float)Math.Min(1, o.QueueMs / Math.Max(1, buf)); Theme.Fill(g, new RectangleF(tube.X + 2, tube.Y + 2, Math.Max(0, (tube.Width - 4) * fill), tube.Height - 4), 6 * S, smart ? Theme.Green : fill > 0.6 ? Theme.Red : Theme.Amber);
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(L.T("the queue in your router") + " · " + N0(o.QueueMs) + " ms " + L.T("of waiting"), Draw.fBodyB, ink, tube.Right + 14 * S, y + 10 * S);
            y += 56 * S;
            y = Big(g, "A ping now takes " + N0(o.PingMs) + " ms (20 ms when idle)", o.PingMs > 100 ? 2 : o.PingMs > 40 ? 1 : 0, x, y, w);
            y = Lead2(g, smart ? "A smart queue keeps the line nearly as busy but never lets packets wait long, and gives small flows like a game or a call their own turn. Most routers can turn this on (SQM, Smart Queue, Adaptive QoS) at about 90% of your real speed." : o.QueueMs > 50 ? "The backup still gets its speed - but every other packet waits behind its queue. Calls stutter, games lag, pages feel slow, and nothing looks wrong on a speed test. That's bufferbloat. A bigger buffer only makes it worse." : "Below the link speed nothing queues - no extra delay.", x, y, w);
            var last = SpeedTest.Last;
            if (last != null && !double.IsNaN(last.Idle) && !double.IsNaN(last.LoadUp)) y = InfoCardOn(h, g, "YOUR LAST SPEED TEST", new List<string[]> { new[] { "Ping idle → while uploading", last.Idle.ToString("0", CultureInfo.CurrentCulture) + " → " + last.LoadUp.ToString("0", CultureInfo.CurrentCulture) + " ms", last.LoadUp - last.Idle > 100 ? "2" : last.LoadUp - last.Idle > 30 ? "1" : "0" } }, null, x, y, w);
            Button(h, g, "Test my line under load  ›", false, delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool("bloat"); }, x, y);
            return y + 44 * S;
        }

        // ================================================================== a call's jitter buffer
        float LabJitter(Control h, Graphics g, float x, float y, float w)
        {
            float cx = x;
            cx += Stepper(h, g, "j.jit", "Network jitter", new double[] { 2, 5, 10, 20, 30, 50, 80 }, 20, v => N0(v) + " ms", cx, y);
            cx += Stepper(h, g, "j.buf", "Jitter buffer", new double[] { 10, 20, 40, 60, 80, 120, 200 }, 60, v => N0(v) + " ms", cx, y);
            cx += Stepper(h, g, "j.base", "Network delay", new double[] { 10, 30, 60, 100, 150, 250 }, 60, v => N0(v) + " ms", cx, y);
            cx += Stepper(h, g, "j.loss", "Loss", new double[] { 0, 0.5, 1, 2, 5 }, 0, v => v.ToString("0.#", CultureInfo.CurrentCulture) + "%", cx, y);
            y += 60 * S;
            double jit = LV("j.jit", 20), buf = LV("j.buf", 60), bas = LV("j.base", 60), loss = LV("j.loss", 0);
            var arr = LabSim.Arrivals(jit, 2000, 11); var o = LabSim.Jitter(arr, bas, buf, loss);
            // histogram of how late packets arrive, with the buffer's edge
            var plot = new RectangleF(x, y, w, 150 * S); int bins = 40; double maxD = Math.Max(buf * 1.4, jit * 5); var cnt = new int[bins];
            foreach (var d in arr) cnt[Math.Min(bins - 1, (int)(d / maxD * bins))]++; int cm = Math.Max(1, cnt.Max());
            for (int i = 0; i < bins; i++) { float bh = plot.Height * cnt[i] / cm; bool late = (i + 0.5) / bins * maxD > buf; using (var b = new SolidBrush(late ? Theme.Red : Theme.Blue)) g.FillRectangle(b, plot.X + i * plot.Width / bins, plot.Bottom - bh, plot.Width / bins - 2, bh); }
            float bxl = plot.X + (float)(buf / maxD) * plot.Width; using (var p = new Pen(Theme.Ink, 2 * S) { DashStyle = DashStyle.Dash }) g.DrawLine(p, bxl, plot.Y - 6 * S, bxl, plot.Bottom);
            using (var mb = new SolidBrush(Theme.Muted)) { g.DrawString(L.T("buffer edge - later than this is too late to play"), Draw.fTiny, mb, Math.Min(bxl + 6 * S, plot.Right - 260 * S), plot.Y - 4 * S); g.DrawString(L.T("how late each packet arrives"), Draw.fTiny, mb, plot.X, plot.Bottom + 4 * S); }
            y = plot.Bottom + 28 * S;
            int lv = o.Mos >= 4 ? 0 : o.Mos >= 3.6 ? 1 : 2;
            y = Big(g, "Call score " + o.Mos.ToString("0.0", CultureInfo.CurrentCulture) + " / 4.5  ·  " + o.LatePct.ToString("0.#", CultureInfo.CurrentCulture) + "% too late  ·  " + N0(o.MouthToEarMs) + " ms mouth to ear", lv, x, y, w);
            y = Lead2(g, "Packets of a call leave every 20 ms but arrive unevenly. The jitter buffer holds them a little so they play smoothly: a packet later than the buffer is thrown away (a gap in the sound); a bigger buffer catches more of them but delays the whole conversation (people start talking over each other above about 250 ms). Teams and Zoom size it themselves - jitter is what forces them to choose.", x, y, w);
            return y;
        }

        // ================================================================== Wi-Fi airtime
        float LabAir(Control h, Graphics g, float x, float y, float w)
        {
            int neighbours = 0; string ch = ""; lock (M.L) ch = M.WifiChannel;
            var ls = linkSnap; if (ls != null && ls.MyChannel > 0) neighbours = ls.Aps.Count(a => a.Band == ls.MyBand && a.Channel == ls.MyChannel && !string.Equals(a.Bssid, ls.MyBssid, StringComparison.OrdinalIgnoreCase) && a.Signal >= 40);
            if (!labV.ContainsKey("a.nb")) labV["a.nb"] = Math.Min(6, neighbours);
            float cx = x;
            cx += Stepper(h, g, "a.slow", "A far-away old device", new double[] { 0, 1, 2 }, 1, v => v == 0 ? "none" : N0(v), cx, y);
            cx += Stepper(h, g, "a.fast", "Devices near the router", new double[] { 1, 2, 3, 5 }, 2, v => N0(v), cx, y);
            cx += Stepper(h, g, "a.nb", "Busy neighbours on your channel", new double[] { 0, 1, 2, 3, 4, 6 }, 0, v => N0(v), cx, y);
            cx += Choice(h, g, "a.fair", "Router", new[] { new[] { "turns", "equal turns" }, new[] { "fair", "airtime fairness" } }, "turns", cx, y);
            y += 60 * S;
            var devs = new List<LabSim.AirDev>();
            for (int i = 0; i < (int)LV("a.fast", 2); i++) devs.Add(new LabSim.AirDev { Name = "Near laptop " + (i + 1), RateMbps = 600 });
            for (int i = 0; i < (int)LV("a.slow", 1); i++) devs.Add(new LabSim.AirDev { Name = "Old device far away " + (i + 1), RateMbps = 6 });
            LabSim.Airtime(devs, LS("a.fair", "turns") == "fair", (int)LV("a.nb", 0));
            float barW = w - 260 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub))
                foreach (var d in devs)
                {
                    g.DrawString(d.Name, Draw.fBody, ink, x, y + 4 * S); g.DrawString(N0(d.RateMbps) + " Mbps link", Draw.fSmall, sub, x, y + 22 * S);
                    Theme.Fill(g, new RectangleF(x + 170 * S, y + 6 * S, barW, 14 * S), 4 * S, Theme.Track); Theme.Fill(g, new RectangleF(x + 170 * S, y + 6 * S, Math.Max(2, barW * (float)d.ShareAir), 14 * S), 4 * S, d.RateMbps < 50 ? Theme.Amber : Theme.Blue);
                    g.DrawString((d.ShareAir * 100).ToString("0", CultureInfo.CurrentCulture) + "% of the air · gets " + N0(d.Mbps) + " Mbps", Draw.fSmall, sub, x + 170 * S, y + 24 * S);
                    y += 46 * S;
                }
            if (LV("a.nb", 0) > 0) y = Lead2(g, (int)LV("a.nb", 0) + " busy neighbour" + (LV("a.nb", 0) > 1 ? "s" : "") + " on the same channel take their turns too - everyone above gets less.", x, y, w);
            y = Lead2(g, LS("a.fair", "turns") == "turns" && LV("a.slow", 1) > 0 ? "Wi-Fi is one shared radio: devices take turns. With equal turns, the slow device's turn lasts 100 times longer, so it eats most of the air and the fast laptops crawl. Move it closer, put it on 2.4 GHz alone, or turn on airtime fairness if the router has it." : "With airtime fairness, each device gets an equal share of time, not of turns - the slow one stays slow, but stops dragging everyone else down.", x, y, w);
            if (neighbours > 0) y = Lead2(g, "On your Wi-Fi right now: " + neighbours + " other strong access point" + (neighbours > 1 ? "s" : "") + " on channel " + ch + " (set as the starting value above).", x, y, w);
            return y;
        }

        // ================================================================== how a name gets found
        float LabDns(Control h, Graphics g, float x, float y, float w)
        {
            string name = LS("d.name", "www.wikipedia.org"); float cx = x;
            cx += Choice(h, g, "d.name", "Name", new[] { new[] { "www.wikipedia.org", "wikipedia.org" }, new[] { "github.com", "github.com" }, new[] { "outlook.office.com", "outlook.office.com" } }, "www.wikipedia.org", cx, y);
            y += 60 * S;
            string[] actors = { "This PC", "Your DNS server", "Root (.)", "." + name.Split('.').Last() + " servers", name.Substring(name.IndexOf('.') + 1) + " servers" };
            var steps = new List<string[]> {
                new[] { "0", "1", "Where is " + name + "? (if Windows remembers, it stops here)", "" }, new[] { "1", "2", "Where is " + name + "?", "" }, new[] { "2", "1", "Don't know - ask the ." + name.Split('.').Last() + " servers", "" },
                new[] { "1", "3", "Where is " + name + "?", "" }, new[] { "3", "1", "Ask " + name.Substring(name.IndexOf('.') + 1) + "'s own servers", "" },
                new[] { "1", "4", "Where is " + name + "?", "" }, new[] { "4", "1", "Here: the address (and how long to remember it)", "" }, new[] { "1", "0", "The address - remembered for next time", "" } };
            y = DrawLadder(g, h, actors, steps, x, y, w);
            y = Lead2(g, "Your DNS server does the walking and keeps the answer for as long as the site allows (its TTL), so the second person to ask gets it at once - that's why a site you just visited answers in 1 ms and a new one in 50.", x, y, w);
            Button(h, g, labBusy ? "Asking..." : "Do it for real for " + name + "  ›", true, labBusy ? (Action)null : delegate
            {
                labBusy = true; labDns = null; LabRefresh(); string n = name;
                Task.Factory.StartNew(() => { List<string[]> r; try { r = Toolkit.Dig(n, "TRACE", "", M); } catch (Exception e) { if (e is OutOfMemoryException) throw; r = new List<string[]> { new[] { "2", "Error", e.Message, "", "u" } }; } try { BeginInvoke((Action)delegate { labBusy = false; labDns = r; LabRefresh(); }); } catch (InvalidOperationException) { labBusy = false; } });
            }, x, y);
            y += 44 * S;
            if (labDns != null) y = InfoCardOn(h, g, "THE REAL WALK (ASKED STRAIGHT FROM THIS PC)", labDns.Select(r => new[] { r[1], r[2], r[0] }).ToList(), null, x, y, w);
            return y;
        }

        // ================================================================== NAT
        float LabNat(Control h, Graphics g, float x, float y, float w)
        {
            string me, gw; lock (M.L) { me = M.LocalIp; gw = M.Gateway; }
            var conns = connSnap.Where(c => c.State == "ESTABLISHED" && Watch.IsPublic(c.RemoteIp)).Take(6).ToList();
            string[] actors = { "This PC " + (me != "" ? me : ""), "Your router " + (gw != "" ? gw : ""), "A server on the internet" };
            var c0 = conns.FirstOrDefault(); string lp = c0 != null ? c0.LocalPort.ToString(CultureInfo.InvariantCulture) : "51234", srv = c0 != null ? c0.RemoteIp + ":" + c0.RemotePort : "203.0.113.40:443";
            var steps = new List<string[]> {
                new[] { "0", "1", "from " + (me != "" ? me : "192.168.1.20") + ":" + lp + " to " + srv, "" },
                new[] { "1", "2", "from YOUR-PUBLIC-IP:40001 (the router swapped it and wrote it down)", "" },
                new[] { "2", "1", "reply to YOUR-PUBLIC-IP:40001", "" },
                new[] { "1", "0", "the router looks it up: 40001 = " + (me != "" ? me : "192.168.1.20") + ":" + lp, "" },
                new[] { "2", "1", "a packet nobody asked for → no entry in the table → dropped", "" } };
            y = DrawLadder(g, h, actors, steps, x, y, w);
            y = Lead2(g, "Your devices share one public address. The router remembers every conversation that started inside, so replies find their way back - and anything that starts outside has nowhere to go. That's why hosting a game or reaching your PC from outside needs port forwarding (or UPnP), and why it fails completely behind CGNAT, where your provider adds a second NAT you don't control.", x, y, w);
            if (conns.Count > 0) y = InfoCardOn(h, g, "YOUR CONVERSATIONS THE ROUTER IS REMEMBERING RIGHT NOW (A FEW)", conns.Select(c => new[] { (c.Proc ?? "") + " :" + c.LocalPort, "→ " + (c.Host != "" ? c.Host : c.RemoteIp) + ":" + c.RemotePort, "3" }).ToList(), null, x, y, w);
            Button(h, g, labBusy ? "Tracing..." : "Check for CGNAT or double NAT (one short trace)  ›", true, labBusy ? (Action)null : delegate
            {
                labBusy = true; labCg = ""; LabRefresh();
                Task.Factory.StartNew(() =>
                {
                    string res;
                    try
                    {
                        var hops = PathTools.Trace(IPAddress.Parse("1.1.1.1"), 5, () => false).Where(z => z.Ip != "").ToList();
                        Func<string, bool> cg = ip => { IPAddress a; if (!IPAddress.TryParse(ip, out a)) return false; var b = a.GetAddressBytes(); return b.Length == 4 && b[0] == 100 && b[1] >= 64 && b[1] < 128; };
                        Func<string, bool> priv = ip => !Watch.IsPublic(ip) && !cg(ip);
                        var after = hops.Skip(1).Take(3).ToList();
                        res = after.Any(z => cg(z.Ip)) ? "CGNAT: a hop right after your router uses 100.64-100.127 - your provider shares one public address between customers. Port forwarding can't work; ask them for a public IP (often free or a few dollars)." : after.Take(1).Any(z => priv(z.Ip)) ? "Double NAT: the hop after your router is another private address (" + after[0].Ip + ") - a modem-router in front of your own router. Put the modem in bridge mode, or forward ports on both." : hops.Count == 0 ? "No hop answered - couldn't tell." : "Neither: your router's next hop is your provider's public network - port forwarding on your router is all you'd need.";
                    }
                    catch (Exception e) { if (e is OutOfMemoryException) throw; res = "Couldn't trace: " + e.Message; }
                    try { BeginInvoke((Action)delegate { labBusy = false; labCg = res; LabRefresh(); }); } catch (InvalidOperationException) { labBusy = false; }
                });
            }, x, y);
            y += 44 * S;
            if (labCg != "") y = Lead2(g, labCg, x, y, w);
            return y;
        }

        // ================================================================== MTU and black holes
        float LabMtu(Control h, Graphics g, float x, float y, float w)
        {
            int myMtu; lock (M.L) myMtu = M.PrimaryMtu;
            float cx = x;
            cx += Stepper(h, g, "u.size", "Packet you send", new double[] { 576, 1200, 1400, 1420, 1450, 1472, 1492, 1500 }, 1500, v => N0(v) + " bytes", cx, y);
            cx += Choice(h, g, "u.way", "On the way", new[] { new[] { "none", "nothing" }, new[] { "pppoe", "PPPoE (-8)" }, new[] { "wg", "WireGuard VPN (-60)" }, new[] { "ipsec", "IPsec VPN (-66)" } }, "wg", cx, y);
            y += 56 * S; cx = x;
            cx += Choice(h, g, "u.df", "Don't Fragment", new[] { new[] { "on", "on (TCP, most traffic)" }, new[] { "off", "off" } }, "on", cx, y);
            cx += Choice(h, g, "u.icmp", "The tunnel's router", new[] { new[] { "says", "says \"too big\"" }, new[] { "silent", "is blocked from saying it" } }, "says", cx, y);
            y += 60 * S;
            double size = LV("u.size", 1500); string way = LS("u.way", "wg"); int over = way == "pppoe" ? 8 : way == "wg" ? 60 : way == "ipsec" ? 66 : 0; int narrow = 1500 - over;
            bool fits = size <= narrow, df = LS("u.df", "on") == "on", says = LS("u.icmp", "says") == "says";
            string verdict; int lv;
            if (fits) { verdict = "Fits - " + N0(size) + " ≤ " + narrow + " bytes all the way"; lv = 0; }
            else if (!df) { verdict = "Split into two pieces at the narrow link - works, but slower and fragile (one lost piece loses both)"; lv = 1; }
            else if (says) { verdict = "Dropped, but the router says \"too big, use " + narrow + "\" - Windows learns it and resends smaller (Path MTU discovery). One hiccup, then fine."; lv = 1; }
            else { verdict = "BLACK HOLE: dropped and nobody says why. Small packets work, big ones vanish - pages hang half-loaded, VPN apps freeze on file copies."; lv = 2; }
            // the path with a narrow pipe in the middle
            float py = y + 10 * S, pw = w; var segs = new[] { Tuple.Create("This PC", 1500), Tuple.Create(way == "none" ? "Your link" : way == "pppoe" ? "PPPoE link" : "VPN tunnel", narrow), Tuple.Create("Server", 1500) };
            for (int i = 0; i < 3; i++)
            {
                float sx = x + i * pw / 3, sh = 30 * S * Math.Max(0.35f, 1 - (1500 - segs[i].Item2) / 160f);   /* narrowed on purpose so a 60-byte tunnel shows */ var r = new RectangleF(sx + 10 * S, py + (34 * S - sh) / 2, pw / 3 - 20 * S, sh);
                Theme.Fill(g, r, 6 * S, i == 1 && !fits ? Theme.LvSoft(lv) : Theme.Track);
                using (var mb = new SolidBrush(Theme.Sub)) g.DrawString(segs[i].Item1 + " · " + segs[i].Item2, Draw.fSmall, mb, sx + 10 * S, py + 40 * S);
            }
            float blk = (float)(size / 1500) * 30 * S; using (var b = new SolidBrush(lv == 2 ? Theme.Red : lv == 1 ? Theme.Amber : Theme.Blue)) g.FillRectangle(b, x + (fits ? pw * 0.85f : pw * 0.3f), py + (34 * S - blk) / 2, 30 * S, blk);
            y = py + 70 * S;
            y = Big(g, verdict, lv, x, y, w);
            y = Lead2(g, "Fix for a black hole: lower the MTU of the VPN adapter (or the router's MSS clamp) to " + (narrow) + " - the Packet stack calculator in the Toolbox works out the exact number for any mix of tunnels." + (myMtu > 0 ? " Your active adapter's MTU is " + myMtu + "." : ""), x, y, w);
            Button(h, g, "Find the real MTU of my path  ›", false, delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool("mtu"); }, x, y);
            return y + 44 * S;
        }

        // ================================================================== which route wins
        float LabRoute(Control h, Graphics g, float x, float y, float w)
        {
            if (routeTable == null) { LoadRoutes(); return Lead2(g, "Reading your route table...", x, y, w); }
            string gw; lock (M.L) gw = M.Gateway; string target = LS("r.dst", "8.8.8.8");
            var opts = new List<string[]> { new[] { "8.8.8.8", "8.8.8.8 (internet)" }, new[] { gw != "" ? gw : "192.168.1.1", "your router" }, new[] { "10.20.30.40", "10.20.30.40" }, new[] { "172.16.5.5", "172.16.5.5" }, new[] { "224.0.0.251", "224.0.0.251 (multicast)" } };
            if (storyIp != "" && IPAddress.Parse(storyIp).AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) opts.Add(new[] { storyIp, "the Network story's server" });
            Choice(h, g, "r.dst", "Where to", opts.ToArray(), "8.8.8.8", x, y); y += 60 * S;
            IPAddress ip; if (!IPAddress.TryParse(target, out ip)) return y;
            List<Routes.R> matched; var best = Routes.Best(routeTable, ip, out matched);
            y = Lead2(g, "Every route that covers " + target + " is a candidate; the most specific one (the longest prefix) wins, and only a tie goes to the lowest metric. A VPN takes over by adding routes more specific than yours - or two /1 routes that beat your /0 default.", x, y, w);
            var rows = routeTable.OrderByDescending(r => r.Prefix).ThenBy(r => r.Metric).Take(40).Select(r => { bool m = matched != null && matched.Contains(r), win = best == r; return new[] { Routes.Ip(r.Dest) + "/" + r.Prefix, (win ? "WINS  " : m ? "covers it  " : "") + "via " + (r.OnLink ? "on-link" : r.Gateway) + " · " + r.Iface + " · metric " + r.Metric + (r.Vpn ? " · VPN" : ""), win ? "0" : m ? "1" : "3" }; }).ToList();
            return InfoCardOn(h, g, "YOUR ROUTE TABLE  ·  " + (best != null ? target + " LEAVES VIA " + (best.OnLink ? "THE LOCAL LINK" : best.Gateway) : "NO ROUTE FOR " + target), rows, null, x, y, w);
        }

        // ================================================================== the game latency budget
        float LabGame(Control h, Graphics g, float x, float y, float w)
        {
            double real = double.NaN; var gp = GamePing.Me; if (gp.Playing != null) { var gm = Games.All.FirstOrDefault(z => z.Id == gp.Playing); if (gm != null) { var b = gp.Best(gm); if (b != null) real = GameStat.Avg(gp.Stat(gm.Id, b).Recent(30)); } }
            if (!labV.ContainsKey("g.ping") && !double.IsNaN(real)) labV["g.ping"] = new double[] { 10, 20, 30, 50, 80, 120, 200 }.OrderBy(v => Math.Abs(v - real)).First();
            float cx = x;
            cx += Stepper(h, g, "g.ping", "Ping", new double[] { 10, 20, 30, 50, 80, 120, 200 }, 30, v => N0(v) + " ms", cx, y);
            cx += Stepper(h, g, "g.tick", "Server tick rate", new double[] { 20, 30, 64, 128 }, 64, v => N0(v) + " Hz", cx, y);
            cx += Stepper(h, g, "g.interp", "Interpolation", new double[] { 1, 2, 3 }, 2, v => N0(v) + " ticks", cx, y);
            cx += Stepper(h, g, "g.fps", "Frame rate", new double[] { 30, 60, 144, 240 }, 144, v => N0(v) + " fps", cx, y);
            cx += Stepper(h, g, "g.disp", "Screen", new double[] { 3, 8, 15, 30 }, 8, v => N0(v) + " ms", cx, y);
            y += 60 * S;
            var b2 = LabSim.GameBudget(LV("g.ping", 30), (int)LV("g.tick", 64), LV("g.interp", 2), (int)LV("g.fps", 144), LV("g.disp", 8));
            string[] names = { "to the server", "waiting for its tick", "interpolation (smoothing)", "drawing the frame", "the screen", "back to you" };
            Color[] cols = { LayerCol[1], LayerCol[5], LayerCol[0], LayerCol[3], LayerCol[4], LayerCol[2] };
            double tot = b2.Sum(); float bx = x;
            for (int i = 0; i < b2.Length; i++) { float bw = (float)(b2[i] / tot) * w; using (var br = new SolidBrush(cols[i])) g.FillRectangle(br, bx, y, Math.Max(1, bw - 2 * S), 30 * S); bx += bw; }
            y += 40 * S;
            using (var sub = new SolidBrush(Theme.Sub)) for (int i = 0; i < b2.Length; i++) { using (var br = new SolidBrush(cols[i])) g.FillRectangle(br, x, y + 4 * S, 10 * S, 10 * S); g.DrawString(names[i] + "  " + b2[i].ToString("0", CultureInfo.CurrentCulture) + " ms", Draw.fBody, sub, x + 16 * S, y); y += 22 * S; }
            y += 6 * S;
            y = Big(g, "Feels like about " + tot.ToString("0", CultureInfo.CurrentCulture) + " ms from your click to seeing the result (ping " + N0(LV("g.ping", 30)) + ")", tot > 150 ? 2 : tot > 90 ? 1 : 0, x, y, w);
            return Lead2(g, "The ping is only part of it. The server works in ticks (64 a second = every 15.6 ms), your game shows the world a couple of ticks in the past to keep movement smooth, then your PC draws it and the screen shows it." + (double.IsNaN(real) ? "" : " Your game's server answers in about " + real.ToString("0", CultureInfo.CurrentCulture) + " ms right now (the Gamer Zone's probe), set as the starting ping."), x, y, w);
        }

        // ================================================================== QoS queues
        float LabQos(Control h, Graphics g, float x, float y, float w)
        {
            float cx = x;
            cx += Stepper(h, g, "q.link", "Upload", new double[] { 2, 5, 10, 20, 50, 100 }, 10, v => N0(v) + " Mbps", cx, y);
            cx += Stepper(h, g, "q.burst", "Big packets already queued", new double[] { 5, 20, 50, 100, 300 }, 50, v => N0(v), cx, y);
            y += 60 * S;
            double link = LV("q.link", 10); int burst = (int)LV("q.burst", 50);
            var modes = new[] { new[] { "fifo", "First come, first served (most home routers)" }, new[] { "fair", "Fair queuing (each flow takes turns)" }, new[] { "priority", "Priority for marked calls (EF)" } };
            double worst = LabSim.VoiceWaitMs("fifo", link, burst);
            foreach (var m in modes)
            {
                double v = LabSim.VoiceWaitMs(m[0], link, burst); float bw = (float)(v / Math.Max(1, worst)) * (w - 330 * S);
                using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(L.T(m[1]), Draw.fBody, ink, x, y + 2 * S);
                Theme.Fill(g, new RectangleF(x + 320 * S, y + 4 * S, Math.Max(3, bw), 16 * S), 4 * S, v > 30 ? Theme.Red : v > 10 ? Theme.Amber : Theme.Green);
                using (var sub = new SolidBrush(Theme.Sub)) g.DrawString(v.ToString("0.#", CultureInfo.CurrentCulture) + " ms", Draw.fSmall, sub, x + 326 * S + Math.Max(3, bw), y + 3 * S);
                y += 30 * S;
            }
            y += 6 * S;
            return Lead2(g, "How long one voice packet waits behind " + burst + " full-size packets on a " + N0(link) + " Mbps upload. Teams and Zoom mark their audio as EF (DSCP 46) - but a mark only helps where a router honours it: company networks often do, most home routers and the internet ignore it. At home, a smart queue (see Bufferbloat) gets most of the benefit without any marks.", x, y, w);
        }

        // ================================================================== subnetting practice
        float LabQuiz(Control h, Graphics g, float x, float y, float w)
        {
            var q = LabSim.Subnet(quizSeed);
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(q.Question, Draw.fHead, ink, new RectangleF(x, y, w, 30 * S)); y += 44 * S;
            float ow = Math.Min(260 * S, (w - 30 * S) / 2);
            for (int i = 0; i < q.Options.Length; i++)
            {
                string o = q.Options[i]; var r = new RectangleF(x + (i % 2) * (ow + 14 * S), y + (i / 2) * 52 * S, ow, 42 * S);
                bool picked = quizPick == o, right = o == q.Answer, done = quizPick != "";
                Theme.Fill(g, r, 10 * S, done && right ? Theme.LvSoft(0) : picked ? Theme.LvSoft(2) : Theme.Soft); if (done && right) Theme.Stroke(g, r, 10 * S, Theme.Green, 2);
                using (var tb = new SolidBrush(Theme.Ink)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(o, Draw.fBodyB, tb, r, c);
                if (!done) Act(h, r, delegate { quizPick = o; quizAll++; if (o == q.Answer) quizRight++; LabRefresh(); });
            }
            y += 112 * S;
            if (quizPick != "") { y = Lead2(g, quizPick == q.Answer ? "Right." : "Not quite - it's " + q.Answer + ". The Subnet calculator in the Toolbox shows every step.", x, y, w); Button(h, g, "Next question  ›", true, delegate { quizSeed++; quizPick = ""; LabRefresh(); }, x, y); y += 44 * S; }
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(quizAll == 0 ? L.T("Pick an answer.") : quizRight + " " + L.T("of") + " " + quizAll + " " + L.T("right this session"), Draw.fSmall, mb, x, y);
            return y + 26 * S;
        }
    }
}
