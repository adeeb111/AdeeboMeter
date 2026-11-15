// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // ================================================================== tool pictures, wave 3 (Adeeb, 15 Nov): packets going back and forth
    // Every exchange is drawn as it happens on the wire: who sends what to whom, what the packet says, which port, and how
    // long the answer took - one packet travelling at a time, then the next. Races run side by side; a full line queues
    // the small packets behind the big ones. From the boxes, the Example or your result; nothing is sent to draw it.
    sealed partial class FullView
    {
        static readonly HashSet<string> Pictured3 = new HashSet<string> { "httptime", "doh", "dhcp", "dupip", "arpwho", "ntp", "radius", "tacacs", "dnsrace", "ip46", "gamecompare", "allips", "bloat", "lag", "arp", "sweep", "inventory", "route", "chain", "overlap", "space", "crash", "battery", "hotel" };
        static readonly HashSet<string> Animated3 = new HashSet<string> { "httptime", "doh", "dhcp", "dupip", "arpwho", "ntp", "radius", "tacacs", "dnsrace", "ip46", "gamecompare", "allips", "bloat", "lag", "arp", "sweep", "inventory", "route", "dig", "http" };
        static double RowMs(string t) { return VNum(t ?? "", @"([0-9]+(?:\.[0-9]+)?)\s*ms"); }

        float Wave3(Graphics g, float x, float y, float w, string st)
        {
            if (Deep3.Contains(tbSel)) return DeepExchange(g, x, y, w, st);
            switch (tbSel)
            {
                case "dnsrace": case "ip46": case "gamecompare": case "allips": return VisRace(g, x, y, w, st);
                case "bloat": return VisQueue(g, x, y, w, st);
                case "lag": return VisBlame(g, x, y, w, st);
                case "arp": case "sweep": case "inventory": return VisHood(g, x, y, w, st);
                case "route": return VisFork(g, x, y, w, st);
                case "chain": return VisChain(g, x, y, w, st);
                case "overlap": return VisOverlap(g, x, y, w, st);
                case "space": return VisSpace(g, x, y, w, st);
                case "crash": return VisCrash(g, x, y, w, st);
                case "battery": return VisBattery(g, x, y, w, st);
                case "hotel": return VisHotel(g, x, y, w, st);
            }
            return Wave4(g, x, y, w, st);
        }

        // ================================================================== the exchange: a packet at a time, with what it says
        sealed class XStep { public int From, To; public string Label = "", Detail = ""; public double Ms = double.NaN; public bool Sealed, All; public int Lv = -1; }
        static XStep X(int from, int to, string label, string detail, double ms) { return new XStep { From = from, To = to, Label = label, Detail = detail, Ms = ms }; }
        // returns the bottom; steps already done are solid with their words, the one in flight is a packet with its bubble,
        // the ones to come are faint. One packet ~0.8-2 s, then a pause, then it starts over.
        float Exchange(Graphics g, RectangleF card, float top, string[] actors, List<XStep> steps)
        {
            float x0 = card.X + 70 * S, x1 = card.Right - 110 * S, colW = (x1 - x0) / Math.Max(1, actors.Length - 1);
            Func<int, float> AX = i => actors.Length == 1 ? (x0 + x1) / 2 : x0 + colW * i;
            float rowH = 44 * S, headY = top, lifeTop = top + 30 * S, bottom = lifeTop + 16 * S + steps.Count * rowH;
            // who's talking: a name on top, a dashed life line down
            for (int i = 0; i < actors.Length; i++)
            {
                var ts = g.MeasureString(actors[i], Draw.fBodyB); var hr = new RectangleF(AX(i) - ts.Width / 2 - 10 * S, headY, ts.Width + 20 * S, 26 * S);
                Theme.Fill(g, hr, 8 * S, Theme.Soft); using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(actors[i], Draw.fBodyB, ib, hr.X + 10 * S, hr.Y + (26 * S - ts.Height) / 2);
                using (var p = new Pen(Theme.Line, 1.5f * S) { DashStyle = DashStyle.Dash }) g.DrawLine(p, AX(i), lifeTop, AX(i), bottom);
            }
            // where we are in the loop
            bool live = WindowsAnimates() && Animates3(tbSel);
            var dur = steps.Select(s => 0.8 + Math.Min(1.2, double.IsNaN(s.Ms) ? 0.3 : s.Ms / 150.0)).ToList(); double total = dur.Sum() + 1.6, t = live ? AnimT % total : total;
            int cur = steps.Count; double into = 0; { double acc = 0; for (int i = 0; i < steps.Count; i++) { if (t < acc + dur[i]) { cur = i; into = (t - acc) / dur[i]; break; } acc += dur[i]; } }
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i]; float ry = lifeTop + 16 * S + i * rowH + 16 * S, fx = AX(s.From), tx = s.All ? AX(actors.Length - 1) : AX(s.To); bool back = tx < fx;
                Color col = s.Lv == 2 ? Theme.Red : s.Lv == 1 ? Theme.Amber : back ? Theme.Green : Theme.Blue;
                bool done = i < cur, now = i == cur;
                using (var p = new Pen(done ? col : Color.FromArgb(70, col), done ? 2.2f * S : 1.5f * S) { DashStyle = done ? (s.All ? DashStyle.Dash : DashStyle.Solid) : DashStyle.Dot }) { p.CustomEndCap = Cap45; g.DrawLine(p, fx, ry, tx + (back ? 6 * S : -6 * S), ry); }
                if (!now)
                    using (var lb = new SolidBrush(done ? Theme.Ink : Theme.Muted)) using (var db = new SolidBrush(done ? Theme.Sub : Theme.Muted)) using (var c = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    {
                        float l = Math.Min(fx, tx), wd = Math.Max(140 * S, Math.Abs(tx - fx));
                        g.DrawString(s.Label, Draw.fSmall, lb, new RectangleF(l + (Math.Abs(tx - fx) - wd) / 2, ry - 19 * S, wd, 17 * S), c);
                        if (done && s.Detail != "") g.DrawString(s.Detail, Draw.fTiny, db, new RectangleF(l + (Math.Abs(tx - fx) - wd) / 2, ry + 4 * S, wd, 15 * S), c);
                    }
                if (done && !double.IsNaN(s.Ms)) using (var mb = new SolidBrush(Theme.Sub)) g.DrawString(s.Ms.ToString(s.Ms < 10 ? "0.#" : "0", CultureInfo.CurrentCulture) + " ms", Draw.fBodyB, mb, card.Right - 96 * S, ry - 9 * S);
                if (now)
                {
                    // the packet itself - an envelope (sealed: a padlock on it), and a bubble saying what's inside
                    float px = fx + (float)(into < 0.15 ? 0 : Math.Min(1, (into - 0.15) / 0.85)) * (tx - fx);
                    var pr = new RectangleF(px - 11 * S, ry - 8 * S, 22 * S, 16 * S); Theme.Fill(g, pr, 3 * S, col);
                    using (var wp = new Pen(Color.White, 1.4f * S)) { g.DrawLine(wp, pr.X + 2 * S, pr.Y + 2 * S, pr.X + pr.Width / 2, pr.Y + pr.Height * 0.6f); g.DrawLine(wp, pr.Right - 2 * S, pr.Y + 2 * S, pr.X + pr.Width / 2, pr.Y + pr.Height * 0.6f); }
                    if (s.Sealed) Kit.Icon(g, "lock", new RectangleF(pr.Right - 4 * S, pr.Y - 10 * S, 14 * S, 14 * S), Theme.Ink);
                    string bt = s.Label + (s.Detail != "" ? "  ·  " + s.Detail : ""); var bs = g.MeasureString(bt, Draw.fSmall);
                    float bx = Math.Max(card.X + 8 * S, Math.Min(card.Right - bs.Width - 24 * S, px - bs.Width / 2 - 8 * S));
                    var br = new RectangleF(bx, ry + 9 * S, bs.Width + 16 * S, 22 * S); Theme.Fill(g, br, 8 * S, Theme.Ink);
                    using (var wb = new SolidBrush(Theme.Card)) g.DrawString(bt, Draw.fSmall, wb, br.X + 8 * S, br.Y + (22 * S - bs.Height) / 2);
                }
            }
            g.SmoothingMode = sm;
            return bottom + 6 * S;
        }
        float ExchangeCard(Graphics g, float x, float y, float w, string st, string caption, string[] actors, List<XStep> steps, string foot)
        {
            float h = 44 * S + 30 * S + 16 * S + steps.Count * 44 * S + 12 * S + (foot != "" ? 40 * S : 0);
            var r = VisCard(g, x, y, w, h, caption, st);
            Exchange(g, r, r.Y + 40 * S, actors, steps);
            if (foot != "") VFoot(g, r, foot);
            return r.Bottom + 14 * S;
        }


        // ================================================================== races: packets out and back, side by side
        float VisRace(Graphics g, float x, float y, float w, string st)
        {
            var lanes = new List<Tuple<string, double, double, int>>();   // name, ms, second ms (or NaN), level
            string cap;
            switch (tbSel)
            {
                case "dnsrace": cap = "DNS SERVERS, RACING (A NAME THEY KNOW / A NEW ONE)"; foreach (var o in VisRows().Where(o => o[1] != "Winner")) { var ms = Regex.Matches(o[2], @"([0-9.]+)\s*ms").Cast<Match>().Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList(); if (ms.Count > 0) lanes.Add(Tuple.Create(o[1], ms[0], ms.Count > 1 ? ms[1] : double.NaN, 0)); } break;
                case "ip46": cap = "IPv4 AND IPv6, RACING TO THE SAME SITE"; foreach (var o in VisRows().Where(o => o[1] == "IPv4" || o[1] == "IPv6")) lanes.Add(Tuple.Create(o[1] + "  " + o[2].Split(' ')[0], RowMs(o[2]), double.NaN, 0)); break;
                case "allips": cap = "EVERY ADDRESS BEHIND THE NAME, KNOCKED ON"; foreach (var o in VisRows().Where(o => Regex.IsMatch(o[1], @"^[0-9a-f:.]+$", RegexOptions.IgnoreCase))) lanes.Add(Tuple.Create(o[1], RowMs(o[2]), double.NaN, int.Parse(o[0], CultureInfo.InvariantCulture) == 2 ? 2 : 0)); break;
                default: cap = "THE SAME GAME, SIDE BY SIDE"; foreach (var o in VisRows().Where(o => o[2].Contains(" ms") || Regex.IsMatch(o[2], @":\s*\d+ ms"))) { double m1 = VNum(o[2], @":\s*([0-9.]+)\s*ms"), p95 = VNum(o[2], @"95%\s*([0-9.]+)"); if (!double.IsNaN(m1)) lanes.Add(Tuple.Create(o[1], m1, p95, o[0] == "1" ? 1 : 0)); } break;
            }
            if (lanes.Count == 0) lanes = tbSel == "ip46" ? new List<Tuple<string, double, double, int>> { Tuple.Create("IPv4", 21.0, double.NaN, 0), Tuple.Create("IPv6", 19.0, double.NaN, 0) } : new List<Tuple<string, double, double, int>> { Tuple.Create(L.T("your DNS"), 8.0, 30.0, 0), Tuple.Create("1.1.1.1", 12.0, 24.0, 0), Tuple.Create("8.8.8.8", 14.0, 40.0, 0) };
            return RaceCard(g, x, y, w, st, cap, lanes, tbSel == "dnsrace" ? "Blue: a name the server already knows (its memory). Purple: a new name it has to look up. Shorter is faster." : tbSel == "gamecompare" ? "Blue: the usual ping. Purple: the worst 5% - what you feel as lag. Steadier wins, not just lower." : "Each dot goes there and back; the first one home is the fastest.");
        }

        // lanes racing: a packet out and back on each, the fastest home first (wave 3's races, and wave 5's DNS, cloud, VPN, legs)
        static float RaceCard(Graphics g, float x, float y, float w, string st, string cap, List<Tuple<string, double, double, int>> lanes, string foot)
        {
            bool two = lanes.Any(l => !double.IsNaN(l.Item3));
            var r = VisCard(g, x, y, w, 60 * S + lanes.Count * 40 * S + 56 * S, cap, st);
            float nx = r.X + 18 * S, lx = r.X + 210 * S, lw = r.Width - 340 * S, ly = r.Y + 48 * S; double max = lanes.Max(l => Math.Max(l.Item2, double.IsNaN(l.Item3) ? 0 : l.Item3)) * 1.1; if (double.IsNaN(max) || max <= 0) max = 50;
            double best = lanes.Where(l => !double.IsNaN(l.Item2)).Select(l => l.Item2).DefaultIfEmpty(0).Min();
            bool live = WindowsAnimates(); double cyc = 2.4, ph = live ? AnimT % cyc / cyc : 1;
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var l in lanes)
            {
                using (var ib = new SolidBrush(Theme.Ink)) using (var nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(l.Item1, l.Item2 == best ? Draw.fBodyB : Draw.fBody, ib, new RectangleF(nx, ly + 4 * S, lx - nx - 10 * S, 20 * S), nf);
                var track = new RectangleF(lx, ly + 6 * S, lw, 14 * S); Theme.Fill(g, track, 7 * S, Theme.Soft);
                for (int k = 0; k < (two ? 2 : 1); k++)
                {
                    double ms = k == 0 ? l.Item2 : l.Item3; if (double.IsNaN(ms)) continue;
                    float end = lx + (float)(ms / max) * lw; Color c = l.Item4 == 2 ? Theme.Red : l.Item4 == 1 ? Theme.Amber : k == 0 ? Theme.Blue : VPurple;
                    using (var p = new Pen(Color.FromArgb(120, c), 2 * S)) g.DrawLine(p, lx, ly + 13 * S + (k == 0 ? -3 : 3) * S, end, ly + 13 * S + (k == 0 ? -3 : 3) * S);
                    // the packet: out to the server and back, faster lanes come home first
                    double p0 = Math.Min(1, ph * max / Math.Max(1, ms) * 0.45); float px = p0 < 0.5 ? lx + (end - lx) * (float)(p0 * 2) : end - (end - lx) * (float)((p0 - 0.5) * 2);
                    if (p0 < 1) Theme.Fill(g, new RectangleF(px - 6 * S, ly + 7 * S + (k == 0 ? -3 : 3) * S, 12 * S, 12 * S), 6 * S, p0 < 0.5 ? c : Theme.Green);
                    else Kit.Icon(g, "check", new RectangleF(lx - 2 * S, ly + 6 * S, 14 * S, 14 * S), Theme.Green);
                }
                using (var vb = new SolidBrush(l.Item2 == best ? Theme.LvText(0) : Theme.Sub)) g.DrawString(l.Item2.ToString("0", CultureInfo.CurrentCulture) + " ms" + (two && !double.IsNaN(l.Item3) ? "  /  " + l.Item3.ToString("0", CultureInfo.CurrentCulture) : ""), Draw.fBodyB, vb, lx + lw + 12 * S, ly + 3 * S);
                ly += 40 * S;
            }
            g.SmoothingMode = sm;
            VFoot(g, r, foot);
            return r.Bottom + 14 * S;
        }

        // ================================================================== bufferbloat: small packets stuck behind big ones
        float VisQueue(Graphics g, float x, float y, float w, string st)
        {
            double idle = RowMs(Big("Idle")), down = RowMs(Big("Download busy")), up = RowMs(Big("Upload busy")); if (double.IsNaN(idle)) { idle = 12; down = 40; up = 150; }
            var r = VisCard(g, x, y, w, 250 * S, "YOUR PING, QUIET AND WHILE THE LINE IS FULL", st);
            var rows = new[] { new object[] { L.T("Quiet"), idle, 0 }, new object[] { L.T("Downloading"), down, 1 }, new object[] { L.T("Uploading"), up, 2 } };
            float lx = r.X + 140 * S, lw = r.Width - 280 * S, ly = r.Y + 50 * S; bool live = WindowsAnimates();
            foreach (var row in rows)
            {
                double ms = (double)row[1]; int busy = (int)row[2]; if (double.IsNaN(ms)) continue;
                using (var ib = new SolidBrush(Theme.Ink)) g.DrawString((string)row[0], Draw.fBodyB, ib, r.X + 18 * S, ly + 10 * S);
                var pipe = new RectangleF(lx, ly, lw, 40 * S); Theme.Fill(g, pipe, 10 * S, Theme.Soft);
                var old = g.Clip; g.SetClip(pipe); double t = live ? AnimT : 0;
                // the big download / upload blocks fill the pipe and the router's queue
                if (busy > 0) using (var bb = new SolidBrush(Color.FromArgb(150, busy == 1 ? Theme.Blue : Theme.Orange))) for (float k = 0; k < lw + 50 * S; k += 46 * S) g.FillRectangle(bb, lx + Wrap(k + (float)(t * 120 * S), lw + 46 * S) - 46 * S, ly + 6 * S, 40 * S, 28 * S);
                // your ping packets: they ride free when it's quiet, and wait in line behind the blocks when it's not
                float speed = (float)(lw / Math.Max(0.25, ms / 40.0));
                for (int k = 0; k < 3; k++) { float px = lx + Wrap((float)(t * speed) + k * lw / 3, lw); Theme.Fill(g, new RectangleF(px, ly + 14 * S, 12 * S, 12 * S), 6 * S, Theme.Green); }
                g.Clip = old;
                int lv = ms - idle > 100 ? 2 : ms - idle > 30 ? 1 : 0;
                using (var vb = new SolidBrush(busy == 0 ? Theme.Ink : Theme.LvText(lv))) g.DrawString(ms.ToString("0", CultureInfo.CurrentCulture) + " ms" + (busy > 0 ? "  (+" + (ms - idle).ToString("0", CultureInfo.CurrentCulture) + ")" : ""), Draw.fBodyB, vb, lx + lw + 12 * S, ly + 10 * S);
                ly += 52 * S;
            }
            VFoot(g, r, "Green dots are your pings. When the line is full they queue behind the big blocks in the router - that wait is bufferbloat. SQM / Smart Queue on the router fixes it.");
            return r.Bottom + 14 * S;
        }

        // ================================================================== why is my game lagging: the path, each leg graded
        float VisBlame(Graphics g, float x, float y, float w, string st)
        {
            var legs = VisRows().Where(o => o[1].Contains("→")).ToList();
            string verdict = Big("Verdict"); var a = VisIn(); string game = a[0] != "" ? a[0] : L.T("the game");
            var r = VisCard(g, x, y, w, 210 * S, "THE WAY TO THE GAME'S SERVER, LEG BY LEG", st);
            string[] names = { L.T("This PC"), L.T("Router"), L.T("Internet"), L.T("Game server") }; string[] icons = { "devices", "route", "traffic", "game" };
            float x0 = r.X + 60 * S, x1 = r.Right - 60 * S, cy = r.Y + 80 * S, step = (x1 - x0) / 3; bool live = WindowsAnimates();
            for (int i = 0; i < 3; i++)
            {
                int lv = i < legs.Count ? int.Parse(legs[i][0], CultureInfo.InvariantCulture) : -1; Color c = lv == 2 ? Theme.Red : lv == 1 ? Theme.Amber : lv == 0 ? Theme.Green : Theme.Line;
                float a0 = x0 + step * i + 22 * S, a1 = x0 + step * (i + 1) - 22 * S;
                using (var p = new Pen(c, lv == 2 ? 6 * S : 4 * S)) g.DrawLine(p, a0, cy, a1, cy);
                // packets travelling the leg; a bad leg stutters and drops one
                if (live) for (int k = 0; k < 3; k++) { float f = Wrap((float)(AnimT * (lv == 2 ? 0.35 : 0.7)) + k / 3f, 1f); if (lv == 2 && k == 1 && f > 0.6f) continue; Theme.Fill(g, new RectangleF(a0 + (a1 - a0) * f - 5 * S, cy - 5 * S, 10 * S, 10 * S), 5 * S, Theme.Ink); }
                if (i < legs.Count) VTextC(g, Draw.Trunc(legs[i][2], 46), Draw.fSmall, lv >= 1 ? Theme.LvText(lv) : Theme.Sub, (a0 + a1) / 2, cy + 14 * S, a1 - a0 + 30 * S);
            }
            for (int i = 0; i < 4; i++) { VNode(g, x0 + step * i, cy, 20 * S, icons[i], Theme.Blue, true); VTextC(g, i == 3 ? game : names[i], Draw.fBodyB, Theme.Ink, x0 + step * i, cy - 46 * S, step); }
            using (var vb = new SolidBrush(verdict == "" ? Theme.Sub : Theme.Ink)) g.DrawString(verdict == "" ? L.T("Run it while the game is open - each leg gets its own ping, jitter and loss.") : verdict, Draw.fBodyB, vb, new RectangleF(r.X + 18 * S, cy + 46 * S, r.Width - 36 * S, 40 * S));
            return r.Bottom + 14 * S;
        }

        // ================================================================== your neighbourhood: the router in the middle, devices around it
        float VisHood(Graphics g, float x, float y, float w, string st)
        {
            var devs = new List<Tuple<string, string, bool>>();   // label, detail, here now
            foreach (var o in VisRows())
            {
                if (tbSel == "inventory") { if (o[1] == "Home" || o[2] == "") continue; devs.Add(Tuple.Create(o[1], o[2].Split(DotSep2, StringSplitOptions.None)[0], o[2].Contains("here now"))); }
                else if (Regex.IsMatch(o[1], @"^\d+\.\d+\.\d+\.\d+$")) { string[] p = o[2].Split(DotSep1, StringSplitOptions.None); devs.Add(Tuple.Create(p.Length > 1 && !Regex.IsMatch(p[1], @"^\d+ ms$") ? p[1] : o[1], o[1], true)); }
            }
            if (devs.Count == 0) devs = HoodDefault.Select(z => Tuple.Create(L.T(z), "", true)).ToList();
            var r = VisCard(g, x, y, w, 300 * S, tbSel == "sweep" ? "A KNOCK ON EVERY ADDRESS - WHO ANSWERED" : "WHO'S ON YOUR NETWORK", st);
            float cx = r.X + r.Width / 2, cy = r.Y + 150 * S, rad = Math.Min(110 * S, r.Width / 4); int n = Math.Min(devs.Count, 10); bool live = WindowsAnimates();
            // the question ripples out from the router, the answers come back
            if (live) { float rp = Wrap((float)AnimT, 2.4f) / 2.4f; using (var p = new Pen(Color.FromArgb((int)(140 * (1 - rp)), Theme.Blue), 2 * S)) g.DrawEllipse(p, cx - rad * rp, cy - rad * rp, 2 * rad * rp, 2 * rad * rp); }
            for (int i = 0; i < n; i++)
            {
                double ang = -Math.PI / 2 + 2 * Math.PI * i / n; float dx = cx + (float)Math.Cos(ang) * rad * 1.35f, dy = cy + (float)Math.Sin(ang) * rad;
                using (var p = new Pen(Theme.Line, 1.5f * S) { DashStyle = DashStyle.Dot }) g.DrawLine(p, cx, cy, dx, dy);
                if (live) { float f = Wrap((float)AnimT * 0.6f + i * 0.13f, 1f); if (f > 0.5f) Theme.Fill(g, new RectangleF(dx + (cx - dx) * (f - 0.5f) * 2 - 4 * S, dy + (cy - dy) * (f - 0.5f) * 2 - 4 * S, 8 * S, 8 * S), 4 * S, Theme.Green); }
                string lab = devs[i].Item1; string ic = Regex.IsMatch(lab, "router", RegexOptions.IgnoreCase) ? "route" : Regex.IsMatch(lab, "phone|random|hiding", RegexOptions.IgnoreCase) ? "phone" : Regex.IsMatch(lab, "TV|screen|Chromecast|Roku", RegexOptions.IgnoreCase) ? "app" : Regex.IsMatch(lab, "console|Xbox|PlayStation|Switch", RegexOptions.IgnoreCase) ? "game" : "devices";
                VNode(g, dx, dy, 16 * S, ic, devs[i].Item3 ? Theme.Green : Theme.Muted, devs[i].Item3);
                VTextC(g, Draw.Trunc(lab, 24), Draw.fSmall, Theme.Ink, dx, dy + 18 * S, 150 * S); if (devs[i].Item2 != "" && devs[i].Item2 != lab) VTextC(g, devs[i].Item2, Draw.fTiny, Theme.Sub, dx, dy + 34 * S, 150 * S);
            }
            VNode(g, cx, cy, 24 * S, "route", Theme.Blue, true); VTextC(g, L.T("your router"), Draw.fBodyB, Theme.Ink, cx, cy + 26 * S, 140 * S);
            if (devs.Count > n) VText(g, "+" + (devs.Count - n) + " " + L.T("more"), Draw.fSmall, Theme.Sub, r.X + 18 * S, r.Bottom - 26 * S);
            return r.Bottom + 14 * S;
        }

        // ================================================================== which route: the fork in the road
        static readonly string[] DotSep2 = { "  ·  " }, DotSep1 = { " · " }, HoodDefault = { "phone", "TV", "printer", "laptop", "console" },
            HotelDefault = { "Wi-Fi lock", "Sign-in page", "DNS", "Windows treats it as", "Other devices seen", "VPN" };
        float VisFork(Graphics g, float x, float y, float w, string st)
        {
            string goes = Big("Goes out"), why = Big("Why this way"); string dst = VisIn()[0] != "" ? VisIn()[0] : "198.51.100.40"; bool vpn = Regex.IsMatch(goes, "vpn|tunnel|wireguard|tap|tun", RegexOptions.IgnoreCase);
            var r = VisCard(g, x, y, w, 230 * S, "WHICH WAY A PACKET TO " + dst + " GOES", st);
            float sx = r.X + 60 * S, fx = r.X + r.Width * 0.38f, ex = r.Right - 80 * S, cy = r.Y + 110 * S, upY = cy - 50 * S, dnY = cy + 50 * S;
            VNode(g, sx, cy, 20 * S, "devices", Theme.Blue, true);
            using (var p = new Pen(Theme.Ink, 3 * S)) g.DrawLine(p, sx + 22 * S, cy, fx, cy);
            using (var p1 = new Pen(vpn ? Theme.Line : Theme.Green, vpn ? 2 * S : 4 * S)) g.DrawBezier(p1, fx, cy, fx + 60 * S, cy, ex - 120 * S, upY, ex - 20 * S, upY);
            using (var p2 = new Pen(vpn ? VPurple : Theme.Line, vpn ? 4 * S : 2 * S)) g.DrawBezier(p2, fx, cy, fx + 60 * S, cy, ex - 120 * S, dnY, ex - 20 * S, dnY);
            VNode(g, ex, upY, 18 * S, "route", vpn ? Theme.Muted : Theme.Green, !vpn); VText(g, L.T("your router → the internet"), Draw.fSmall, Theme.Sub, ex - 210 * S, upY - 34 * S);
            VNode(g, ex, dnY, 18 * S, "lock", vpn ? VPurple : Theme.Muted, vpn); VText(g, L.T("the VPN tunnel → the company"), Draw.fSmall, Theme.Sub, ex - 210 * S, dnY - 42 * S);
            if (WindowsAnimates())
            {
                // the packet: along the main road, then the branch it was given
                float f = Wrap((float)AnimT * 0.5f, 1f); PointF pt;
                if (f < 0.4f) pt = new PointF(sx + (fx - sx) * f / 0.4f, cy);
                else { float u = (f - 0.4f) / 0.6f, ty = vpn ? dnY : upY; pt = Bez(new PointF(fx, cy), new PointF(fx + 60 * S, cy), new PointF(ex - 120 * S, ty), new PointF(ex - 20 * S, ty), u); }
                Theme.Fill(g, new RectangleF(pt.X - 8 * S, pt.Y - 6 * S, 16 * S, 12 * S), 3 * S, Theme.Orange);
            }
            VTextC(g, L.T("the most specific route wins"), Draw.fTiny, Theme.Muted, fx, cy + 8 * S, 160 * S);
            using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(goes == "" ? L.T("Type an address - see which road Windows picks for it.") : goes + (why != "" ? "  ·  " + why : ""), Draw.fBodyB, ib, new RectangleF(r.X + 18 * S, r.Bottom - 46 * S, r.Width - 36 * S, 40 * S));
            return r.Bottom + 14 * S;
        }
        static PointF Bez(PointF a, PointF b, PointF c, PointF d, float t) { float u = 1 - t; return new PointF(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X, u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y); }

        // ================================================================== the ladder of trust
        float VisChain(Graphics g, float x, float y, float w, string st)
        {
            var links = VisRows().Where(o => o[0] != "-1" && (o[1].Contains("root") || o[1].Contains("middle") || o[1].Contains("your certificate") || o[1].Contains("intermediate"))).ToList();
            if (links.Count == 0) links = new List<string[]> { new[] { "3", "root", L.T("a root your PC trusts"), "", "m" }, new[] { "3", "middle (intermediate)", L.T("signed by the root"), "", "m" }, new[] { "3", "your certificate", L.T("the site's own, signed by the middle"), "", "m" } };
            var r = VisCard(g, x, y, w, 60 * S + links.Count * 76 * S, "THE LADDER OF TRUST - EACH ONE SIGNS THE ONE BELOW", st);
            float bx = r.X + 60 * S, bw = r.Width - 120 * S, by = r.Y + 48 * S;
            for (int i = 0; i < links.Count; i++)
            {
                var o = links[i]; int lv = o[0] == "2" ? 2 : o[0] == "1" ? 1 : o[0] == "0" ? 0 : -1; var br = new RectangleF(bx + i * 24 * S, by, bw - i * 48 * S, 50 * S);
                Theme.Fill(g, br, 10 * S, lv >= 0 ? Theme.LvSoft(lv) : Theme.Soft); Theme.Fill(g, new RectangleF(br.X, br.Y, 6 * S, br.Height), 3 * S, lv == 2 ? Theme.Red : lv == 1 ? Theme.Amber : lv == 0 ? Theme.Green : Theme.Line);
                Kit.Icon(g, lv == 2 ? "question" : "lock", new RectangleF(br.X + 16 * S, br.Y + 15 * S, 20 * S, 20 * S), lv == 2 ? Theme.Red : Theme.Ink);
                string nm = Regex.Replace(o[1], @"^[^a-zA-Z]+", ""); VText(g, nm, Draw.fBodyB, Theme.Ink, br.X + 46 * S, br.Y + 6 * S);
                using (var sb = new SolidBrush(lv >= 1 ? Theme.LvText(lv) : Theme.Sub)) using (var nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(o[2], Draw.fSmall, sb, new RectangleF(br.X + 46 * S, br.Y + 27 * S, br.Width - 60 * S, 18 * S), nf);
                if (i < links.Count - 1) VArrow(g, br.X + br.Width / 2, br.Bottom + 2 * S, br.X + br.Width / 2, br.Bottom + 22 * S, Theme.Muted, false);
                by += 76 * S;
            }
            return r.Bottom + 14 * S;
        }
        // ================================================================== overlapping networks: two ranges, one clash
        float VisOverlap(Graphics g, float x, float y, float w, string st)
        {
            string home = Big("Home network").Split(' ')[0], vpnR = Big("VPN routes"); var vpns = vpnR.Split(',').Select(z => z.Trim().Split(' ')[0]).Where(z => z.Contains("/")).ToList();
            if (home == "") { home = "10.0.0.0/24"; vpns = new List<string> { "10.0.0.0/8" }; }
            var r = VisCard(g, x, y, w, 170 * S, "YOUR HOME RANGE AND THE VPN'S RANGES", st);
            uint hip; int hp; V4Mask(home, out hip, out hp); float bx = r.X + 140 * S, bw = r.Width - 170 * S, by = r.Y + 56 * S;
            var vp = vpns.Select(z => { uint ip; int p; return V4Mask(z, out ip, out p) && p >= 0 ? new { S = z, Net = ip & (p == 0 ? 0 : 0xFFFFFFFFu << (32 - p)), Size = Math.Pow(2, 32 - p) } : null; }).Where(z => z != null).ToList();
            double lo = vp.Count > 0 ? vp.Min(z => (double)z.Net) : hip, hi = vp.Count > 0 ? vp.Max(z => z.Net + z.Size) : hip + 256; if (hi <= lo) hi = lo + 256;
            Func<double, float> X = v => bx + (float)((v - lo) / (hi - lo)) * bw;
            VText(g, L.T("VPN"), Draw.fBodyB, Theme.Ink, r.X + 18 * S, by + 2 * S); foreach (var v in vp) { Theme.Fill(g, new RectangleF(X(v.Net), by, Math.Max(4 * S, X(v.Net + v.Size) - X(v.Net)), 22 * S), 5 * S, VPurple); VText(g, v.S, Draw.fTiny, Color.White, X(v.Net) + 6 * S, by + 4 * S); }
            uint hn = hip & (hp <= 0 ? 0 : 0xFFFFFFFFu << (32 - hp)); double hs = Math.Pow(2, 32 - Math.Max(0, hp)); bool inside = vp.Any(v => hn >= v.Net && hn < v.Net + v.Size);
            VText(g, L.T("Home"), Draw.fBodyB, Theme.Ink, r.X + 18 * S, by + 42 * S); float hx = X(hn); Theme.Fill(g, new RectangleF(Math.Max(bx, Math.Min(bx + bw - 8 * S, hx)), by + 40 * S, Math.Max(8 * S, X(hn + hs) - hx), 22 * S), 5 * S, inside ? Theme.Red : Theme.Green);
            VText(g, home + (inside ? "  -  " + L.T("inside the VPN's range: those addresses go home instead") : "  -  " + L.T("no overlap")), Draw.fSmall, inside ? Theme.LvText(2) : Theme.LvText(0), bx, by + 68 * S);
            VFoot(g, r, "Change your router's range (e.g. to 192.168.77.0/24) and the clash goes away.");
            return r.Bottom + 14 * S;
        }
        // ================================================================== where did my space go: a treemap
        float VisSpace(Graphics g, float x, float y, float w, string st)
        {
            var items = VisRows().Skip(1).Select(o => new { N = o[1], V = VNum(o[2], @"([0-9.]+)\s*GB") }).Where(z => !double.IsNaN(z.V) && z.V > 0).OrderByDescending(z => z.V).Take(8).ToList();
            var disk = VisRows().FirstOrDefault(); double used = disk == null ? double.NaN : VNum(disk[2], @"([0-9.]+)\s*GB");
            var r = VisCard(g, x, y, w, 220 * S, "WHERE THE SPACE WENT - BIGGER BOX, BIGGER FOLDER", st);
            if (items.Count == 0) { VText(g, L.T("Run it to see your biggest folders as boxes."), Draw.fBody, Theme.Muted, r.X + 18 * S, r.Y + 60 * S); return r.Bottom + 14 * S; }
            double tot = Math.Max(items.Sum(z => z.V), double.IsNaN(used) ? 0 : used); var area = new RectangleF(r.X + 18 * S, r.Y + 44 * S, r.Width - 36 * S, 160 * S);
            float ax = area.X; int k = 0;
            foreach (var it in items)
            {
                float ww = (float)(area.Width * it.V / tot); var b = new RectangleF(ax, area.Y, Math.Max(2, ww - 3 * S), area.Height); Theme.Fill(g, b, 6 * S, Lane[k++ % Lane.Length]);
                if (ww > 70 * S) using (var wb = new SolidBrush(Color.White)) { g.DrawString(Draw.Trunc(it.N.Split('\\').Last(), 18), Draw.fSmall, wb, new RectangleF(b.X + 6 * S, b.Y + 6 * S, b.Width - 10 * S, 34 * S)); g.DrawString(it.V.ToString("0", CultureInfo.CurrentCulture) + " GB", Draw.fBodyB, wb, b.X + 6 * S, b.Bottom - 26 * S); }
                ax += ww;
            }
            if (ax < area.Right - 4 * S) { Theme.Fill(g, new RectangleF(ax, area.Y, area.Right - ax, area.Height), 6 * S, Theme.Soft); VText(g, L.T("the rest"), Draw.fSmall, Theme.Sub, ax + 6 * S, area.Y + 6 * S); }
            return r.Bottom + 14 * S;
        }
        // ================================================================== crashes: a week as a line, each crash a dot
        float VisCrash(Graphics g, float x, float y, float w, string st)
        {
            var r = VisCard(g, x, y, w, 150 * S, "THE LAST 7 DAYS - EACH DOT A CRASH", st);
            float bx = r.X + 30 * S, bw = r.Width - 60 * S, ly = r.Y + 70 * S; string all = VisText();
            using (var p = new Pen(Theme.Line, 3 * S)) g.DrawLine(p, bx, ly, bx + bw, ly);
            for (int d = 0; d < 7; d++)
            {
                var day = DateTime.Today.AddDays(d - 6); float dx = bx + bw * d / 6f; string dn = day.ToString("dddd", CultureInfo.InvariantCulture);
                VTextC(g, day.ToString("ddd", CultureInfo.CurrentCulture), Draw.fTiny, Theme.Muted, dx, ly + 10 * S, 60 * S);
                if (all.IndexOf(dn, StringComparison.OrdinalIgnoreCase) >= 0) Theme.Fill(g, new RectangleF(dx - 8 * S, ly - 8 * S, 16 * S, 16 * S), 8 * S, Theme.Red);
                else Theme.Fill(g, new RectangleF(dx - 4 * S, ly - 4 * S, 8 * S, 8 * S), 4 * S, Theme.Line);
            }
            string c = Big("Crashes (7 days)"); VText(g, c == "" ? L.T("Red dots are the days an app crashed.") : c, Draw.fBodyB, c == "" ? Theme.Sub : Theme.Ink, r.X + 18 * S, r.Bottom - 40 * S);
            return r.Bottom + 14 * S;
        }
        // ================================================================== battery: today's capacity against new
        float VisBattery(Graphics g, float x, float y, float w, string st)
        {
            string h = Big("Battery health"); double now = VNum(h, @"\(([0-9.]+) of"), design = VNum(h, @"of ([0-9.]+) Wh"); double pct = double.IsNaN(now) || double.IsNaN(design) || design <= 0 ? double.NaN : now / design;
            var r = VisCard(g, x, y, w, 150 * S, "WHAT THE BATTERY HOLDS NOW, AGAINST NEW", st);
            float bx = r.X + 18 * S, bw = Math.Min(420 * S, r.Width * 0.6f), by = r.Y + 52 * S;
            Theme.Stroke(g, new RectangleF(bx, by, bw, 46 * S), 8 * S, Theme.Ink, 2.5f * S); Theme.Fill(g, new RectangleF(bx + bw, by + 14 * S, 7 * S, 18 * S), 2 * S, Theme.Ink);
            if (!double.IsNaN(pct)) { Theme.Fill(g, new RectangleF(bx + 4 * S, by + 4 * S, (float)((bw - 8 * S) * pct), 38 * S), 5 * S, pct > 0.8 ? Theme.Green : pct > 0.6 ? Theme.Amber : Theme.Red); using (var p = new Pen(Theme.Ink, 1.5f * S) { DashStyle = DashStyle.Dash }) g.DrawLine(p, bx + bw - 4 * S, by - 6 * S, bx + bw - 4 * S, by + 52 * S); }
            VText(g, h == "" ? L.T("Run it to see how much the battery has worn.") : h, Draw.fBodyB, Theme.Ink, bx + bw + 30 * S, by + 12 * S);
            VFoot(g, r, "Batteries lose capacity with charge cycles and heat; under about 80% is when people notice.");
            return r.Bottom + 14 * S;
        }
        // ================================================================== hotel / cafe Wi-Fi: the safety checklist
        float VisHotel(Graphics g, float x, float y, float w, string st)
        {
            var items = VisRows().Where(o => o[1] != "Verdict").ToList();
            if (items.Count == 0) items = HotelDefault.Select(n => new[] { "3", n, "", "", "i" }).ToList();
            int cols = 3, rowsN = (items.Count + cols - 1) / cols; var r = VisCard(g, x, y, w, 60 * S + rowsN * 64 * S, "IS THIS WI-FI OK TO USE? - EACH CHECK", st);
            float cw = (r.Width - 36 * S - (cols - 1) * 10 * S) / cols;
            for (int i = 0; i < items.Count; i++)
            {
                var o = items[i]; int lv = o[0] == "2" ? 2 : o[0] == "1" ? 1 : o[0] == "0" ? 0 : -1; var b = new RectangleF(r.X + 18 * S + (i % cols) * (cw + 10 * S), r.Y + 46 * S + (i / cols) * 64 * S, cw, 54 * S);
                Theme.Fill(g, b, 10 * S, lv >= 0 ? Theme.LvSoft(lv) : Theme.Soft); Draw.LvDot(g, lv < 0 ? 3 : lv, b.X + 12 * S, b.Y + 12 * S, 10 * S, 10 * S);
                VText(g, L.T(o[1]), Draw.fBodyB, Theme.Ink, b.X + 30 * S, b.Y + 6 * S);
                using (var sb = new SolidBrush(lv >= 1 ? Theme.LvText(lv) : Theme.Sub)) using (var nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(o[2], Draw.fSmall, sb, new RectangleF(b.X + 12 * S, b.Y + 30 * S, b.Width - 20 * S, 18 * S), nf);
            }
            return r.Bottom + 14 * S;
        }
    }
}
