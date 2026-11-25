// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== calculator pictures (wave 2)
    // Each calculator draws what its numbers mean - from the form (or an Example's inputs), never from anything sent.
    // A few move (a radio sending, a train of timeslots, a line filling): only the picture's rectangle repaints, only
    // while you can see it (FullView.CalcForm.cs, AnimTick).
    sealed partial class FullView
    {
        static readonly Color[] Lane = { Theme.C("#1F6FEB"), Theme.C("#2DA44E"), Theme.C("#8250DF"), Theme.C("#E36209"), Theme.C("#BF3989"), Theme.C("#1B7C83"), Theme.C("#9A6700"), Theme.C("#57606A") };
        // the form as it stands for what's on screen (an Example's own inputs when one shows)
        CalcSpec VSpec() { var sp = SpecOf(tbSel); if (sp != null) FillSpec(sp, VisIn()); return sp; }
        static double NV(CF f) { if (f == null) return double.NaN; var m = numRx.Match((f.Text ?? "").Replace(",", "")); double v; return m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }   // the first number: "$151.00 (+0.67%...)" -> 151
        static string Opt(CF f) { return f == null || f.Opts.Length == 0 ? "" : f.Opts[Math.Max(0, Math.Min(f.Sel, f.Opts.Length - 1))]; }
        string Big(string name) { var r = VRow(name); return r == null ? "" : r[2]; }
        static void VText(Graphics g, string t, Font f, Color c, float x, float y) { using (var b = new SolidBrush(c)) g.DrawString(t, f, b, x, y); }
        static void VTextC(Graphics g, string t, Font f, Color c, float cx, float y, float w) { using (var b = new SolidBrush(c)) using (var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(t, f, b, new RectangleF(cx - w / 2, y, w, 18 * S), sf); }
        static void VFoot(Graphics g, RectangleF r, string t) { using (var b = new SolidBrush(Theme.Sub)) g.DrawString(L.T(t), Draw.fSmall, b, new RectangleF(r.X + 18 * S, r.Bottom - 36 * S, r.Width - 36 * S, 34 * S)); }
        static float Wrap(float v, float len) { float m = v % len; return m < 0 ? m + len : m; }

        float CalcPicture(Graphics g, float x, float y, float w, string st)
        {
            switch (tbSel)
            {
                case "cellular": case "wifirate": return VisRoad(g, x, y, w, st);
                case "tdm": return VisTdm(g, x, y, w, st);
                case "plan": return VisPlan(g, x, y, w, st);
                case "bandwidth": case "backup": return VisTransfer(g, x, y, w, st);
                case "bdp": return VisBdp(g, x, y, w, st);
                case "linkbudget": return VisLink(g, x, y, w, st);
                case "jitterbuf": return VisJitter(g, x, y, w, st);
                case "raid": return VisRaid(g, x, y, w, st);
                case "ups": return VisUps(g, x, y, w, st);
                case "sla": return VisSla(g, x, y, w, st);
                case "growth": case "compound": return VisCurve(g, x, y, w, st);
                case "vlsm": return VisVlsm(g, x, y, w, st);
                case "binhex": case "wildcard": return VisBits(g, x, y, w, st);
                case "v6split": return VisV6(g, x, y, w, st);
                case "stream": case "datause": return VisShare(g, x, y, w, st);
                case "units": return VisUnits(g, x, y, w, st);
                case "fxconv": return VisFx(g, x, y, w, st);
                case "pnl": case "possize": return VisPrice(g, x, y, w, st);
                case "avgcost": return VisAvg(g, x, y, w, st);
                case "pctback": return VisBack(g, x, y, w, st);
                case "tradelat": case "tickrate": return VisTicks(g, x, y, w, st);
                case "comunits": case "perunit": case "futures": return VisCommodity(g, x, y, w, st);
            }
            return y;
        }

        // ---------- LTE / 5G and Wi-Fi: a radio and a phone, lanes of parcels between them
        // lanes = antennas (MIMO streams), lane width = channel bandwidth, parcel size = bits per symbol (modulation)
        float VisRoad(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); bool cell = tbSel == "cellular"; int lanes, bits; double mhz;
            if (cell) { string mm = Opt(sp.F[2]); lanes = Math.Max(1, Math.Min(mm[0] - '0', mm[mm.Length - 1] - '0')); string md = Opt(sp.F[3]); bits = md == "QPSK" ? 2 : md == "16QAM" ? 4 : md == "64QAM" ? 6 : md == "256QAM" ? 8 : 10; mhz = NV(new CF { Text = Opt(sp.F[1]).Split(' ')[0] }); }
            else { int gen = int.Parse(Opt(sp.F[0]), CultureInfo.InvariantCulture); lanes = int.Parse(Opt(sp.F[2]), CultureInfo.InvariantCulture); bits = gen == 4 ? 6 : gen == 5 ? 8 : gen == 6 ? 10 : 12; mhz = NV(new CF { Text = Opt(sp.F[1]) }); }
            if (double.IsNaN(mhz)) mhz = 20;
            float laneH = (float)(10 * S + 16 * S * Math.Log(Math.Max(5, mhz) / 5, 2) / Math.Log(64, 2));
            var r = VisCard(g, x, y, w, 96 * S + lanes * (laneH + 6 * S) + 64 * S, cell ? "A CELL TOWER SENDING TO YOUR PHONE" : "YOUR ROUTER SENDING TO YOUR LAPTOP", st);
            float x0 = r.X + 90 * S, x1 = r.Right - 110 * S, top = r.Y + 52 * S, len = x1 - x0, mid = top + lanes * (laneH + 6 * S) / 2;
            // the radio: a mast with waves (or a router), and the phone
            using (var p = new Pen(Theme.Ink, 2.5f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float tx = r.X + 46 * S;
                if (cell) { g.DrawLine(p, tx, mid - 30 * S, tx - 14 * S, mid + 30 * S); g.DrawLine(p, tx, mid - 30 * S, tx + 14 * S, mid + 30 * S); g.DrawLine(p, tx - 8 * S, mid + 6 * S, tx + 8 * S, mid + 6 * S); }
                else { Theme.Stroke(g, new RectangleF(tx - 20 * S, mid - 8 * S, 40 * S, 18 * S), 4 * S, Theme.Ink, 2 * S); g.DrawLine(p, tx - 12 * S, mid - 8 * S, tx - 16 * S, mid - 24 * S); g.DrawLine(p, tx + 12 * S, mid - 8 * S, tx + 16 * S, mid - 24 * S); }
                var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 1; i <= 3; i++) using (var wp = new Pen(Color.FromArgb(200 - i * 50, Theme.Blue), 2 * S)) g.DrawArc(wp, tx - i * 9 * S, mid - 40 * S - i * 9 * S, i * 18 * S, i * 18 * S, -60, 120);
                float px = r.Right - 70 * S; Theme.Stroke(g, cell ? new RectangleF(px - 12 * S, mid - 22 * S, 24 * S, 44 * S) : new RectangleF(px - 24 * S, mid - 16 * S, 48 * S, 30 * S), 5 * S, Theme.Ink, 2 * S);
                if (!cell) g.DrawLine(p, px - 30 * S, mid + 17 * S, px + 30 * S, mid + 17 * S);
                g.SmoothingMode = sm;
            }
            VTextC(g, cell ? L.T("tower") : L.T("router"), Draw.fSmall, Theme.Sub, r.X + 46 * S, mid + 36 * S, 90 * S);
            VTextC(g, cell ? L.T("phone") : L.T("laptop"), Draw.fSmall, Theme.Sub, r.Right - 70 * S, mid + 28 * S, 90 * S);
            // the lanes, and the parcels riding them: each parcel's size is the bits one symbol carries
            double t = AnimT; float pw = 4 * S + bits * 1.8f * S, gap = pw + 10 * S, ly = top;
            for (int l = 0; l < lanes; l++)
            {
                var lr = new RectangleF(x0, ly, len, laneH); Theme.Fill(g, lr, laneH / 2, Theme.Soft);
                var old = g.Clip; g.SetClip(lr);
                using (var pb = new SolidBrush(Lane[l % Lane.Length]))
                    for (float k = 0; k < len + gap - 1; k += gap) { float px = x0 + Wrap(k + (float)(t * 60 * S) + l * 7 * S, len + gap) - gap; g.FillRectangle(pb, px, ly + laneH * 0.2f, pw, laneH * 0.6f); }
                g.Clip = old; ly += laneH + 6 * S;
            }
            string res = cell ? Big("Peak") : Big("Link rate");
            using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(res == "" ? "" : res, Draw.fBig, ib, x0, ly + 4 * S);
            VText(g, lanes + " " + L.T(lanes == 1 ? "lane" : "lanes") + " = " + (cell ? Opt(sp.F[2]) + " MIMO" : lanes + " " + L.T("streams")) + "   ·   " + L.T("lane width") + " = " + mhz.ToString("0", CultureInfo.CurrentCulture) + " MHz   ·   " + L.T("parcel") + " = " + bits + " " + L.T("bits a symbol") + (cell ? " (" + Opt(sp.F[3]) + ")" : ""), Draw.fSmall, Theme.Sub, x0, ly + 36 * S);
            return r.Bottom + 14 * S;
        }

        // ---------- E1 / T1: a train of timeslots, one frame every 125 µs
        float VisTdm(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); bool e1 = Opt(sp.F[0]) == "E1"; int n = sp.F[1].Step, slots = e1 ? 32 : 24;
            var r = VisCard(g, x, y, w, 196 * S, "ONE FRAME EVERY 125 µs - 8,000 A SECOND", st);
            float x0 = r.X + 18 * S, len = r.Width - 36 * S, cw = Math.Max(14 * S, Math.Min(28 * S, (len - 20 * S) / (slots + (e1 ? 0 : 1)))), fw = cw * (slots + (e1 ? 0 : 1)) + 10 * S, ty = r.Y + 60 * S;
            // which slots are yours: from 1 up, skipping framing (0) and signalling (16) on E1
            var mine = new HashSet<int>(); for (int s = e1 ? 1 : 0; mine.Count < n && s < slots; s++) { if (e1 && s == 16 && n <= 30) continue; mine.Add(s); }   // bug-hunt ML-177: T1 has no framing slot - its 24 channels are 0-23 (the F bit sits in front)
            var old = g.Clip; g.SetClip(new RectangleF(x0, ty - 4 * S, len, 50 * S));
            float off = (float)(AnimT * 40 * S) % fw;
            using (var wb = new SolidBrush(Color.White)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                for (float fx = x0 - fw + off; fx < x0 + len; fx += fw)
                {
                    float cx = fx;
                    if (!e1) { Theme.Fill(g, new RectangleF(cx, ty, cw - 2 * S, 34 * S), 4 * S, VSlate); g.DrawString("F", Draw.fTiny, wb, new RectangleF(cx, ty, cw - 2 * S, 34 * S), c); cx += cw; }
                    for (int s = 0; s < slots; s++)
                    {
                        bool sync = e1 && s == 0, sig = e1 && s == 16 && !mine.Contains(16), me = mine.Contains(s);
                        Color col = sync ? VSlate : sig ? Theme.Amber : me ? Theme.Blue : Theme.Line;
                        Theme.Fill(g, new RectangleF(cx, ty, cw - 2 * S, 34 * S), 4 * S, col);
                        if (cw > 18 * S) using (var tb = new SolidBrush(me || sync || sig ? Color.White : Theme.Sub)) g.DrawString(s.ToString(CultureInfo.InvariantCulture), Draw.fTiny, tb, new RectangleF(cx, ty, cw - 2 * S, 34 * S), c);
                        cx += cw;
                    }
                }
            g.Clip = old;
            { string tw0 = L.T("travels this way"); VText(g, tw0, Draw.fTiny, Theme.Muted, x0, ty - 18 * S); float aw = g.MeasureString(tw0, Draw.fTiny).Width; VArrow(g, x0 + aw + 4 * S, ty - 11 * S, x0 + aw + 30 * S, ty - 11 * S, Theme.Muted, false); }
            float ly = ty + 50 * S, lx = x0;
            foreach (var k in new[] { new object[] { VSlate, e1 ? "slot 0: framing (keeps the ends in step)" : "F: one framing bit" }, new object[] { Theme.Amber, "slot 16: signalling" }, new object[] { Theme.Blue, "yours" } })
            {
                if (!e1 && (string)k[1] == "slot 16: signalling") continue;
                Theme.Fill(g, new RectangleF(lx, ly + 4 * S, 10 * S, 10 * S), 3 * S, (Color)k[0]); string t = L.T((string)k[1]); VText(g, t, Draw.fSmall, Theme.Sub, lx + 14 * S, ly); lx += 28 * S + g.MeasureString(t, Draw.fSmall).Width;
            }
            VText(g, n + " × 64 kb/s = " + (n * 64).ToString("N0", CultureInfo.CurrentCulture) + " kb/s", Draw.fBig, Theme.Ink, x0, ly + 24 * S);
            VFoot(g, r, "Each slot carries 8 bits in every frame: 8 bits × 8,000 frames a second = 64 kb/s - one uncompressed phone call.");
            return r.Bottom + 14 * S;
        }

        // ---------- is my plan fast enough: what's busy at once, filling the line
        static readonly Dictionary<string, double[]> PlanNeeds = new Dictionary<string, double[]> { { "4k", new[] { 25.0, 0.2 } }, { "hd", new[] { 5.0, 0.1 } }, { "call", new[] { 4.0, 4.0 } }, { "game", new[] { 3.0, 1.0 } }, { "stream", new[] { 1.0, 8.0 } }, { "download", new[] { 50.0, 1.0 } } };
        float VisPlan(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); double down = NV(sp.F[0]) * (sp.F[0].Unit == 1 ? 1000 : 1), up = NV(sp.F[1]) * (sp.F[1].Unit == 1 ? 1000 : 1); var k = sp.F[2];
            var r = VisCard(g, x, y, w, 236 * S, "YOUR LINE, AND EVERYTHING BUSY AT ONCE", st);
            float y0 = r.Y + 50 * S;
            for (int dir = 0; dir < 2; dir++)
            {
                double cap = dir == 0 ? down : up; if (double.IsNaN(cap) || cap <= 0) { VText(g, dir == 0 ? L.T("Type your plan's download") : L.T("Upload: type your plan's to check it"), Draw.fSmall, Theme.Muted, r.X + 18 * S, y0); y0 += 64 * S; continue; }
                double need = 0; for (int i = 0; i < k.Tokens.Length; i++) need += k.Counts[i] * PlanNeeds[k.Tokens[i]][dir];
                double scale = Math.Max(cap, need) * 1.05; float bx = r.X + 110 * S, bw = r.Width - 128 * S, ph = 26 * S;
                VText(g, dir == 0 ? L.T("Download") : L.T("Upload"), Draw.fBodyB, Theme.Ink, r.X + 18 * S, y0 + 4 * S);
                var pipe = new RectangleF(bx, y0, (float)(bw * cap / scale), ph); Theme.Fill(g, pipe, 8 * S, Theme.Soft); Theme.Stroke(g, pipe, 8 * S, Theme.Line, 1.5f * S);
                float cx = bx; double t = AnimT;
                for (int i = 0; i < k.Tokens.Length; i++)
                {
                    double v = k.Counts[i] * PlanNeeds[k.Tokens[i]][dir]; if (v <= 0) continue; float sw = (float)(bw * v / scale);
                    var seg = new RectangleF(cx, y0 + 4 * S, Math.Max(2, sw - 2 * S), ph - 8 * S); Theme.Fill(g, seg, 5 * S, Lane[i]);
                    var old = g.Clip; g.SetClip(seg);   // the flow moving along
                    using (var wb = new SolidBrush(Color.FromArgb(70, Color.White))) for (float d = 0; d < sw + 16 * S - 1; d += 16 * S) g.FillRectangle(wb, cx + Wrap(d + (float)(t * 40 * S), sw + 16 * S) - 8 * S, seg.Y, 6 * S, seg.Height);
                    g.Clip = old;
                    if (sw > 50 * S) VTextC(g, k.Counts[i] + " " + L.T(k.Names[i]), Draw.fTiny, Color.White, cx + sw / 2, y0 + 7 * S, sw);
                    cx += sw;
                }
                if (need > cap) { var over = new RectangleF(pipe.Right, y0 - 2 * S, cx - pipe.Right, ph + 4 * S); using (var hb = new HatchBrush(HatchStyle.WideUpwardDiagonal, Theme.Red, Color.Transparent)) g.FillRectangle(hb, over); }
                int lv = need > cap ? 2 : need * 1.5 > cap ? 1 : 0;   // bug-hunt L-341: "plenty" only with a third spare, as the foot says
                VText(g, need.ToString("0.#", CultureInfo.CurrentCulture) + " " + L.T("of") + " " + cap.ToString("0", CultureInfo.CurrentCulture) + " Mb/s" + (lv == 2 ? " - " + L.T("too much at once") : lv == 1 ? " - " + L.T("tight") : " - " + L.T("plenty of room")), Draw.fSmall, Theme.LvText(lv), bx, y0 + ph + 4 * S);
                y0 += 70 * S;
            }
            VFoot(g, r, "Each colour is one thing at its smooth-running speed. Wi-Fi usually delivers less than the plan - keep about a third spare.");
            return r.Bottom + 14 * S;
        }

        // ---------- bandwidth / backup: blocks travelling down the line into the disk, with the time it takes
        float VisTransfer(Graphics g, float x, float y, float w, string st)
        {
            bool bk = tbSel == "backup"; string time = bk ? Big("Full copy") : Big("Time"); if (time == "") time = Big("Realistic");
            var r = VisCard(g, x, y, w, 170 * S, bk ? "A FULL BACKUP, DOWN THE LINE" : "A FILE, DOWN THE LINE", st);
            float cy = r.Y + 74 * S, x0 = r.X + 70 * S, x1 = r.Right - 70 * S;
            VNode(g, r.X + 40 * S, cy, 20 * S, "doc", Theme.Blue, true); VNode(g, r.Right - 40 * S, cy, 20 * S, bk ? "shield" : "devices", Theme.Green, true);
            var pipe = new RectangleF(x0, cy - 10 * S, x1 - x0, 20 * S); Theme.Fill(g, pipe, 10 * S, Theme.Soft);
            var old = g.Clip; g.SetClip(pipe); float gap = 26 * S; double t = AnimT;
            using (var bb = new SolidBrush(Theme.Blue)) for (float k = 0; k < pipe.Width + gap - 1; k += gap) g.FillRectangle(bb, x0 + Wrap(k + (float)(t * 70 * S), pipe.Width + gap) - gap, cy - 6 * S, 16 * S, 12 * S);
            g.Clip = old;
            VTextC(g, time == "" ? L.T("type any two: size, speed, time") : time, Draw.fBodyB, Theme.Ink, (x0 + x1) / 2, cy + 18 * S, x1 - x0);
            VFoot(g, r, "A byte is 8 bits: 100 Mb/s moves 12.5 MB a second at best - real transfers lose 5-10% to headers and more on Wi-Fi.");
            return r.Bottom + 14 * S;
        }

        // ---------- BDP: the pipe's length is the round trip; the window is how much may be in it at once
        float VisBdp(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); double mbps = NV(sp.F[0]) * (sp.F[0].Unit == 0 ? 1000 : 1), rtt = NV(sp.F[1]), win = NV(sp.F[2]) * (sp.F[2].Unit == 1 ? 1024 : 1);
            double bdpKB = double.IsNaN(mbps) || double.IsNaN(rtt) ? double.NaN : mbps * 1e6 * rtt / 1000 / 8 / 1024; double fill = double.IsNaN(win) || double.IsNaN(bdpKB) ? 1 : Math.Min(1, win / bdpKB);
            var r = VisCard(g, x, y, w, 176 * S, "DATA IN FLIGHT - THE PIPE IS AS LONG AS THE ROUND TRIP", st);
            float cy = r.Y + 70 * S, x0 = r.X + 70 * S, x1 = r.Right - 70 * S, len = x1 - x0;
            VNode(g, r.X + 40 * S, cy, 18 * S, "devices", Theme.Blue, true); VNode(g, r.Right - 40 * S, cy, 18 * S, "dest", Theme.Green, true);
            var pipe = new RectangleF(x0, cy - 12 * S, len, 24 * S); Theme.Fill(g, pipe, 12 * S, Theme.Soft);
            // packets for the window's share of each trip, then a gap while the sender waits for acknowledgements
            var old = g.Clip; g.SetClip(pipe); float cyc = len, ph = Wrap((float)(AnimT * 80 * S), cyc);
            // bug-hunt L-340: no window typed - the packets are grey ("how full is unknown"), never a full blue train that implies it fills
            bool winKnown = !double.IsNaN(win);
            using (var bb = new SolidBrush(!winKnown ? Theme.Muted : fill < 1 ? Theme.Amber : Theme.Blue)) for (float k = 0; k < cyc * (float)(winKnown ? fill : 0.5); k += 14 * S) { float px = x0 + Wrap(ph + k, cyc); g.FillRectangle(bb, px, cy - 7 * S, 10 * S, 14 * S); }
            g.Clip = old;
            VTextC(g, double.IsNaN(bdpKB) ? L.T("type a speed and a round trip") : L.T("The pipe holds") + " " + (bdpKB >= 1024 ? (bdpKB / 1024).ToString("0.#", CultureInfo.CurrentCulture) + " MB" : bdpKB.ToString("0", CultureInfo.CurrentCulture) + " KB") + (fill < 1 ? " - " + L.T("your window fills only") + " " + (fill * 100).ToString("0", CultureInfo.CurrentCulture) + "% " + L.T("of it: the gaps are the sender waiting") : ""), Draw.fBodyB, fill < 1 ? Theme.LvText(1) : Theme.Ink, (x0 + x1) / 2, cy + 20 * S, len);
            VFoot(g, r, "TCP may only have one window of data unacknowledged. A window smaller than the pipe caps one connection at window ÷ round trip, however fast the line.");
            return r.Bottom + 14 * S;
        }

        // ---------- link budget: the signal's level step by step, against what the receiver needs
        float VisLink(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); double tx = NV(sp.F[0]), gt = NV(sp.F[1]), gr = NV(sp.F[2]), d = NV(sp.F[3]), f = NV(sp.F[4]), sens = NV(sp.F[5]);
            if (sp.F[0].Unit == 1 && !double.IsNaN(tx)) tx = 10 * Math.Log10(tx); else if (sp.F[0].Unit == 2 && !double.IsNaN(tx)) tx = 10 * Math.Log10(tx * 1000);
            double km = d * (sp.F[3].Unit == 1 ? 0.001 : sp.F[3].Unit == 2 ? 1.609 : 1), mhz = f * (sp.F[4].Unit == 0 ? 1000 : 1);
            var r = VisCard(g, x, y, w, 236 * S, "THE SIGNAL'S LEVEL, STEP BY STEP (dBm)", st);
            if (new[] { tx, gt, gr, km, mhz }.Any(double.IsNaN) || km <= 0 || mhz <= 0) { VText(g, L.T("Fill the boxes to see the steps."), Draw.fBody, Theme.Muted, r.X + 18 * S, r.Y + 60 * S); return r.Bottom + 14 * S; }
            double fspl = 20 * Math.Log10(km) + 20 * Math.Log10(mhz) + 32.44; var lv = new[] { tx, tx + gt, tx + gt - fspl, tx + gt - fspl + gr };
            string[] nm = { "sent", "+ antenna", "- the distance", "+ antenna = received" };
            double hi = Math.Max(lv.Max(), 0) + 10, lo = Math.Min(lv.Min(), double.IsNaN(sens) ? lv.Min() : sens) - 10; float top = r.Y + 48 * S, h = 130 * S, bx = r.X + 60 * S, sw = (r.Width - 90 * S) / 4;
            Func<double, float> Y = v => top + (float)((hi - v) / (hi - lo)) * h;
            using (var ax = new Pen(Theme.Line2, 1 * S)) g.DrawLine(ax, bx, Y(0), r.Right - 24 * S, Y(0));
            VText(g, "0 dBm", Draw.fTiny, Theme.Muted, r.X + 14 * S, Y(0) - 7 * S);
            for (int i = 0; i < 4; i++)
            {
                float yy = Y(lv[i]), x1 = bx + i * sw; Color c = i == 2 ? Theme.Orange : i == 3 ? (double.IsNaN(sens) || lv[3] - sens >= 10 ? Theme.Green : lv[3] >= sens ? Theme.Amber : Theme.Red) : Theme.Blue;
                Theme.Fill(g, new RectangleF(x1 + 6 * S, yy - 4 * S, sw - 12 * S, 8 * S), 4 * S, c);
                if (i > 0) using (var p = new Pen(Theme.Muted, 1.5f * S) { DashStyle = DashStyle.Dot }) g.DrawLine(p, x1 + 6 * S, Y(lv[i - 1]), x1 + 6 * S, yy);
                VTextC(g, lv[i].ToString("0", CultureInfo.CurrentCulture) + " dBm", Draw.fBodyB, Theme.Ink, x1 + sw / 2, yy - 24 * S, sw);
                VTextC(g, L.T(nm[i]) + (i == 2 ? " (" + fspl.ToString("0", CultureInfo.CurrentCulture) + " dB)" : ""), Draw.fTiny, Theme.Sub, x1 + sw / 2, yy + 8 * S, sw);
            }
            if (!double.IsNaN(sens)) { float sy = Y(sens); using (var p = new Pen(Theme.Red, 1.5f * S) { DashStyle = DashStyle.Dash }) g.DrawLine(p, bx, sy, r.Right - 24 * S, sy); VText(g, L.T("the receiver needs") + " " + sens.ToString("0", CultureInfo.CurrentCulture) + " dBm", Draw.fTiny, Theme.LvText(2), bx, sy + 3 * S); }
            VFoot(g, r, "Each doubling of distance or frequency costs 6 dB. Keep 10 dB or more above the receiver's line for rain, leaves and people walking by.");
            return r.Bottom + 14 * S;
        }

        // ---------- jitter buffer: packets leave evenly, arrive unevenly, the buffer plays them out evenly again
        float VisJitter(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); double lo = NV(sp.F[0]), hi = NV(sp.F[1]), pt = NV(new CF { Text = Opt(sp.F[2]) }); if (double.IsNaN(lo)) lo = 0; if (double.IsNaN(hi)) hi = lo + 30; if (double.IsNaN(pt)) pt = 20;
            // bug-hunt ML-179: the buffer the result gives (1.5 x the variation, whole packets) - its own "Buffer" row when there is one
            double buf = Math.Max(pt, Math.Ceiling((hi - lo) * 1.5 / pt) * pt); var bm = System.Text.RegularExpressions.Regex.Match(Big("Buffer"), @"^([0-9]+)"); double bv; if (bm.Success && double.TryParse(bm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out bv)) buf = bv;
            var r = VisCard(g, x, y, w, 210 * S, "SENT EVENLY, ARRIVING UNEVENLY, PLAYED EVENLY", st);
            float x0 = r.X + 120 * S, x1 = r.Right - 20 * S, len = x1 - x0; double pxPerMs = len / 600.0, t = AnimT * 1000 % 600;
            string[] rows = { "sent", "arrives", "played" }; float[] ys = { r.Y + 56 * S, r.Y + 96 * S, r.Y + 136 * S };
            for (int i = 0; i < 3; i++) { VText(g, L.T(rows[i]), Draw.fSmall, Theme.Sub, r.X + 18 * S, ys[i] - 2 * S); using (var p = new Pen(Theme.Line2, 1 * S)) g.DrawLine(p, x0, ys[i] + 7 * S, x1, ys[i] + 7 * S); }
            var old = g.Clip; g.SetClip(new RectangleF(x0, r.Y + 40 * S, len, 120 * S));
            for (int k = -40; k < 40; k++)
            {
                double sent = k * pt - t, jit = lo + (hi - lo) * (0.5 + 0.5 * Math.Sin(k * 2.3) * Math.Cos(k * 0.7)), arr = sent + jit, play = sent + lo + buf;
                bool late = jit > lo + buf;
                using (var b1 = new SolidBrush(Theme.Blue)) g.FillRectangle(b1, x0 + (float)(sent * pxPerMs) + len / 2, ys[0], 6 * S, 14 * S);
                using (var b2 = new SolidBrush(late ? Theme.Red : Theme.Amber)) g.FillRectangle(b2, x0 + (float)(arr * pxPerMs) + len / 2, ys[1], 6 * S, 14 * S);
                if (!late) using (var b3 = new SolidBrush(Theme.Green)) g.FillRectangle(b3, x0 + (float)(play * pxPerMs) + len / 2, ys[2], 6 * S, 14 * S);
            }
            g.Clip = old;
            VText(g, L.T("Buffer") + " " + buf.ToString("0", CultureInfo.CurrentCulture) + " ms" + "  -  " + L.T("every packet waits until its turn, so the voice comes out smooth"), Draw.fBodyB, Theme.Ink, r.X + 18 * S, r.Y + 160 * S);
            VFoot(g, r, "");
            return r.Bottom + 14 * S;
        }

        // ---------- RAID: the disks, and what each holds
        float VisRaid(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); string lvl = Opt(sp.F[0]); int n = sp.F[1].Step, shown = Math.Min(n, 12);
            var r = VisCard(g, x, y, w, 210 * S, "RAID " + lvl + " ACROSS " + n + " DISKS", st);
            float dw = Math.Min(64 * S, (r.Width - 36 * S) / shown - 8 * S), x0 = r.X + 18 * S, top = r.Y + 52 * S, bh = 22 * S;
            for (int dk = 0; dk < shown; dk++)
            {
                float dx = x0 + dk * (dw + 8 * S); Theme.Stroke(g, new RectangleF(dx, top - 4 * S, dw, 4 * bh + 12 * S), 6 * S, Theme.Line, 1.5f * S);
                for (int row = 0; row < 4; row++)
                {
                    string kind = "d";
                    if (lvl == "1") kind = dk == 0 ? "d" : "m";   // bug-hunt ML-180: RAID 1 over 3+ disks is one copy and the rest mirrors
                    else if (lvl == "10") kind = dk % 2 == 1 ? "m" : "d";
                    else if (lvl == "5") kind = dk == (n - 1 - row % n + n) % n ? "p" : "d";
                    else if (lvl == "6") kind = dk == (n - 1 - row % n + n) % n || dk == (n - 2 - row % n + 2 * n) % n ? "p" : "d";
                    Color c = kind == "p" ? Theme.Amber : kind == "m" ? VPurple : Theme.Blue;
                    Theme.Fill(g, new RectangleF(dx + 4 * S, top + row * bh, dw - 8 * S, bh - 4 * S), 3 * S, c);
                }
            }
            if (n > shown) VText(g, "+" + (n - shown), Draw.fBodyB, Theme.Muted, x0 + shown * (dw + 8 * S), top + 40 * S);
            float ly = top + 4 * bh + 20 * S, lx = x0;
            foreach (var k in new[] { new object[] { Theme.Blue, "your data" }, new object[] { Theme.Amber, "parity (rebuilds a lost disk)" }, new object[] { VPurple, "mirror copy" } })
            { Theme.Fill(g, new RectangleF(lx, ly + 4 * S, 10 * S, 10 * S), 3 * S, (Color)k[0]); string t = L.T((string)k[1]); VText(g, t, Draw.fSmall, Theme.Sub, lx + 14 * S, ly); lx += 28 * S + g.MeasureString(t, Draw.fSmall).Width; }
            VText(g, Big("Usable"), Draw.fBodyB, Theme.Ink, x0, ly + 24 * S);
            return r.Bottom + 14 * S;
        }

        // ---------- UPS: the battery and the load
        float VisUps(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); int n = sp.F[0].Step; double v = NV(sp.F[1]), ah = NV(sp.F[2]), load = NV(sp.F[3]), eff = NV(sp.F[4]); if (double.IsNaN(eff)) eff = 85;
            double wh = n * v * ah, min = wh * eff / 100 / Math.Max(1, load) * 60;
            var r = VisCard(g, x, y, w, 170 * S, "THE BATTERY AGAINST THE LOAD", st);
            float bx = r.X + 18 * S, by = r.Y + 52 * S, bw = Math.Min(360 * S, r.Width * 0.5f), bh = 56 * S;
            Theme.Stroke(g, new RectangleF(bx, by, bw, bh), 8 * S, Theme.Ink, 2.5f * S); Theme.Fill(g, new RectangleF(bx + bw, by + bh / 3, 8 * S, bh / 3), 2 * S, Theme.Ink);
            double frac = 1 - (AnimT % 6) / 6 * 0.85; Theme.Fill(g, new RectangleF(bx + 5 * S, by + 5 * S, (float)((bw - 10 * S) * frac), bh - 10 * S), 5 * S, frac > 0.3 ? Theme.Green : Theme.Amber);
            VText(g, double.IsNaN(wh) ? "-" : wh.ToString("0", CultureInfo.CurrentCulture) + " Wh", Draw.fBodyB, Theme.Ink, bx + 12 * S, by + bh + 8 * S);
            float tx = bx + bw + 40 * S;
            VText(g, double.IsNaN(load) ? "" : L.T("Load") + " " + load.ToString("0", CultureInfo.CurrentCulture) + " W", Draw.fBody, Theme.Sub, tx, by);
            VText(g, double.IsNaN(min) || double.IsInfinity(min) ? "" : L.T("about") + " " + min.ToString("0", CultureInfo.CurrentCulture) + " " + L.T("min on paper"), Draw.fBig, Theme.Ink, tx, by + 22 * S);
            VFoot(g, r, "Batteries give less when the load is high, when they're cold and as they age - plan for about half the paper figure after 3 years.");
            return r.Bottom + 14 * S;
        }

        // ---------- SLA: one year as a bar, the downtime slice in red
        float VisSla(Graphics g, float x, float y, float w, string st)
        {
            // bug-hunt ML-178: the uptime the result worked out (a downtime typed in, "4 h a year", is turned into one there)
            double up = double.NaN; var um = System.Text.RegularExpressions.Regex.Match(Big("Uptime"), @"^([0-9]+(?:[.,][0-9]+)?)%"); if (um.Success) double.TryParse(um.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out up);
            if (double.IsNaN(up)) up = NV(new CF { Text = (VisIn()[0] ?? "").Replace("%", "") }); var dy = Big("Down a year");
            var r = VisCard(g, x, y, w, 150 * S, "ONE YEAR - HOW MUCH OF IT MAY BE DOWN", st);
            float bx = r.X + 18 * S, bw = r.Width - 36 * S, by = r.Y + 56 * S; Theme.Fill(g, new RectangleF(bx, by, bw, 28 * S), 8 * S, Theme.LvSoft(0));
            if (!double.IsNaN(up) && up < 100) { float dw = Math.Max(3 * S, (float)(bw * (100 - up) / 100)); Theme.Fill(g, new RectangleF(bx + bw - dw, by, dw, 28 * S), 3 * S, Theme.Red); }
            for (int m = 0; m <= 12; m++) using (var p = new Pen(Theme.Line2, 1 * S)) g.DrawLine(p, bx + bw * m / 12, by + 30 * S, bx + bw * m / 12, by + 36 * S);
            VText(g, dy == "" ? L.T("type an uptime like 99.9%") : L.T("down") + " " + dy + " " + L.T("a year"), Draw.fBodyB, Theme.Ink, bx, by + 42 * S);
            VFoot(g, r, "");
            return r.Bottom + 14 * S;
        }

        // ---------- storage growth and compound growth: a curve over the years
        float VisCurve(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); bool money = tbSel == "compound"; var pts = new List<double>(); var put = new List<double>(); double cap = double.NaN; int years;
            if (money) { double s0 = NV(sp.F[0]), m0 = NV(sp.F[1]), rt = NV(sp.F[2]); years = sp.F[3].Step; if (double.IsNaN(s0)) s0 = 0; if (double.IsNaN(m0)) m0 = 0; if (double.IsNaN(rt)) rt = 0; pts = Calc.Compound(s0, m0, rt, years).ToList(); for (int i = 0; i <= years; i++) put.Add(s0 + m0 * 12 * i); }
            else
            {
                double now = NV(sp.F[0]), gr = NV(sp.F[1]); cap = NV(sp.F[2]); years = 5; if (double.IsNaN(now) || double.IsNaN(gr)) { now = 10; gr = 3; }
                for (int i = 0; i <= years * 12; i += 12) { double v = sp.F[1].Unit == 0 ? now * Math.Pow(1 + gr / 100, i) : now + gr * (sp.F[1].Unit == 1 ? i / 12.0 : i); pts.Add(v); }
            }
            var r = VisCard(g, x, y, w, 220 * S, money ? "WHAT YOU PUT IN, AND WHAT IT GREW (A STEADY RATE ASSUMED)" : "STORAGE OVER THE NEXT 5 YEARS", st);
            float cx0 = r.X + 60 * S, cw = r.Width - 90 * S, cy0 = r.Y + 50 * S, ch = 120 * S; double max = Math.Max(pts.Max(), double.IsNaN(cap) ? 0 : cap) * 1.08; if (max <= 0) max = 1;
            Func<int, float> X = i => cx0 + cw * i / Math.Max(1, pts.Count - 1); Func<double, float> Y = v => cy0 + ch - (float)(v / max) * ch;
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            var area = new List<PointF> { new PointF(X(0), cy0 + ch) }; for (int i = 0; i < pts.Count; i++) area.Add(new PointF(X(i), Y(pts[i]))); area.Add(new PointF(X(pts.Count - 1), cy0 + ch));
            using (var ab = new SolidBrush(Color.FromArgb(70, Theme.Green))) g.FillPolygon(ab, area.ToArray());
            if (money) { var pa = new List<PointF> { new PointF(X(0), cy0 + ch) }; for (int i = 0; i < put.Count; i++) pa.Add(new PointF(X(i), Y(put[i]))); pa.Add(new PointF(X(put.Count - 1), cy0 + ch)); using (var pb = new SolidBrush(Color.FromArgb(110, Theme.Blue))) g.FillPolygon(pb, pa.ToArray()); }
            using (var p = new Pen(Theme.Green, 2.5f * S)) g.DrawLines(p, area.Skip(1).Take(pts.Count).ToArray());
            if (!double.IsNaN(cap)) using (var p = new Pen(Theme.Red, 1.5f * S) { DashStyle = DashStyle.Dash }) { g.DrawLine(p, cx0, Y(cap), cx0 + cw, Y(cap)); VText(g, L.T("full"), Draw.fTiny, Theme.LvText(2), cx0 + cw - 30 * S, Y(cap) - 16 * S); }
            g.SmoothingMode = sm;
            for (int i = 0; i < pts.Count; i += Math.Max(1, pts.Count / 6)) VTextC(g, (money ? i : i).ToString(CultureInfo.CurrentCulture) + (i == 0 ? "" : " " + L.T("yr")), Draw.fTiny, Theme.Muted, X(i), cy0 + ch + 4 * S, 40 * S);
            VText(g, money ? M2(pts.Last()) : pts.Last().ToString("0.#", CultureInfo.CurrentCulture) + " " + sp.F[0].Units[sp.F[0].Unit], Draw.fBodyB, Theme.Ink, cx0 + cw - 120 * S, cy0 - 4 * S);
            if (money) { float lx = cx0; foreach (var k in new[] { new object[] { Theme.Blue, "what you put in" }, new object[] { Theme.Green, "growth" } }) { Theme.Fill(g, new RectangleF(lx, r.Bottom - 26 * S, 10 * S, 10 * S), 3 * S, (Color)k[0]); string t = L.T((string)k[1]); VText(g, t, Draw.fSmall, Theme.Sub, lx + 14 * S, r.Bottom - 30 * S); lx += 30 * S + g.MeasureString(t, Draw.fSmall).Width; } }
            return r.Bottom + 14 * S;
        }
        static string M2(double v) { return "$" + v.ToString("N0", CultureInfo.CurrentCulture); }

        // ---------- VLSM: the block as one bar, cut into the subnets, biggest first
        float VisVlsm(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); var pre = System.Text.RegularExpressions.Regex.Match(sp.F[0].Text ?? "", @"/(\d+)"); int bp = pre.Success ? int.Parse(pre.Groups[1].Value, CultureInfo.InvariantCulture) : 24; if (bp > 32) bp = 32;   /* L-337: a /31 is drawn as a /31 */
            double total = Math.Pow(2, 32 - bp); var need = sp.F[1].Items.Select(z => NV(new CF { Text = z })).Where(z => !double.IsNaN(z) && z > 0).OrderByDescending(z => z).ToList();
            var r = VisCard(g, x, y, w, 150 * S, "THE BLOCK /" + bp + " (" + total.ToString("N0", CultureInfo.CurrentCulture) + " ADDRESSES), CUT BIGGEST FIRST", st);
            float bx = r.X + 18 * S, bw = r.Width - 36 * S, by = r.Y + 56 * S; Theme.Fill(g, new RectangleF(bx, by, bw, 34 * S), 6 * S, Theme.Soft);
            double at = 0; int k = 0, noFit = 0;
            foreach (var n in need)
            {
                int p = 32; while (p > 0 && Math.Pow(2, 32 - p) - (p <= 30 ? 2 : 0) < n) p--; double size = Math.Pow(2, 32 - p); if (at + size > total) { noFit++; continue; }   // bug-hunt L-337: counted below, not just dropped
                float sx = bx + (float)(bw * at / total), sw = (float)(bw * size / total); Theme.Fill(g, new RectangleF(sx + 1 * S, by, Math.Max(2, sw - 2 * S), 34 * S), 4 * S, Lane[k % Lane.Length]);
                if (sw > 34 * S) VTextC(g, "/" + p, Draw.fSmall, Color.White, sx + sw / 2, by + 9 * S, sw); at += size; k++;
            }
            VText(g, (total - at).ToString("N0", CultureInfo.CurrentCulture) + " " + L.T("addresses left over") + (noFit > 0 ? "  ·  " + noFit + " " + L.T(noFit == 1 ? "subnet doesn't fit" : "subnets don't fit") : ""), Draw.fSmall, noFit > 0 ? Theme.LvText(2) : Theme.Sub, bx, by + 42 * S);
            VFoot(g, r, "");
            return r.Bottom + 14 * S;
        }

        // ---------- binary / hex and wildcard: the bits as boxes
        float VisBits(Graphics g, float x, float y, float w, string st)
        {
            string v = (VisIn()[0] ?? "").Trim(); var bits = new List<int>(); bool mask = tbSel == "wildcard";
            System.Net.IPAddress ip; long n;
            if (v.StartsWith("/", StringComparison.Ordinal) && int.TryParse(v.Substring(1), out n0)) { for (int i = 0; i < 32; i++) bits.Add(i < n0 ? 1 : 0); }
            else if (System.Net.IPAddress.TryParse(v, out ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) foreach (var b in ip.GetAddressBytes()) for (int i = 7; i >= 0; i--) bits.Add((b >> i) & 1);
            else if (TryNum(v, out n) && n >= 0) { int len = n < 256 ? 8 : n < 65536 ? 16 : 32; for (int i = len - 1; i >= 0; i--) bits.Add((int)((n >> i) & 1)); }
            // bug-hunt ML-181: a wildcard typed in (0.0.3.255 - first bit 0, not all zero) goes on the wildcard row, its mask above it
            if (mask && bits.Count == 32 && bits[0] == 0 && bits.Contains(1)) for (int i = 0; i < 32; i++) bits[i] = 1 - bits[i];
            var r = VisCard(g, x, y, w, mask ? 190 * S : 150 * S, mask ? "THE MASK AND ITS WILDCARD - ONE IS THE OTHER FLIPPED" : "THE SAME NUMBER, AS BITS", st);
            if (bits.Count == 0) { VText(g, L.T("Type a number, an IPv4 address or a mask."), Draw.fBody, Theme.Muted, r.X + 18 * S, r.Y + 60 * S); return r.Bottom + 14 * S; }
            float bx = r.X + 18 * S, gap = 6 * S, cell = Math.Min(30 * S, (r.Width - 36 * S - (bits.Count / 8 - 1) * gap) / bits.Count);
            for (int row = 0; row < (mask ? 2 : 1); row++)
                using (var wb = new SolidBrush(Color.White)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    for (int i = 0; i < bits.Count; i++)
                    {
                        int b = row == 0 ? bits[i] : 1 - bits[i]; float cx = bx + i * cell + (i / 8) * gap, cy = r.Y + 56 * S + row * 50 * S;
                        Theme.Fill(g, new RectangleF(cx + 1 * S, cy, cell - 2 * S, 24 * S), 3 * S, b == 1 ? (row == 0 ? Theme.Blue : Theme.Orange) : Theme.Line);
                        g.DrawString(b.ToString(CultureInfo.InvariantCulture), Draw.fTiny, wb, new RectangleF(cx, cy, cell, 24 * S), c);
                        if (!mask && i % 8 == 0 && bits.Count > 8) VText(g, (1L << (bits.Count - 1 - i)).ToString("N0", CultureInfo.CurrentCulture), Draw.fTiny, Theme.Muted, cx, cy + 28 * S);
                    }
            if (mask) { VText(g, L.T("mask: 1 = network part"), Draw.fSmall, Theme.Sub, bx, r.Y + 82 * S); VText(g, L.T("wildcard: 1 = may differ (what ACLs and OSPF use)"), Draw.fSmall, Theme.Sub, bx, r.Y + 132 * S); }
            return r.Bottom + 14 * S;
        }
        int n0;
        static bool TryNum(string v, out long n)
        {
            n = 0; v = v.Trim().ToLowerInvariant();
            try { if (v.StartsWith("0x", StringComparison.Ordinal)) { n = Convert.ToInt64(v.Substring(2), 16); return true; } if (v.StartsWith("0b", StringComparison.Ordinal)) { n = Convert.ToInt64(v.Substring(2), 2); return true; } }
            catch (Exception e) { if (!(e is FormatException || e is OverflowException || e is ArgumentException)) throw; return false; }
            return long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
        }

        // ---------- IPv6 split: the prefix and the pieces it makes
        float VisV6(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); var m = System.Text.RegularExpressions.Regex.Match(sp.F[0].Text ?? "", @"/(\d+)"); int p = m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 48; string to = Opt(sp.F[1]);
            int np = to.StartsWith("/", StringComparison.Ordinal) ? int.Parse(to.Substring(1), CultureInfo.InvariantCulture) : p + (int)Math.Ceiling(Math.Log(Math.Max(1, int.Parse(to, CultureInfo.InvariantCulture)), 2));
            int bitsMore = Math.Max(0, np - p); double parts = Math.Pow(2, bitsMore); int drawn = (int)Math.Min(parts, 64);
            var r = VisCard(g, x, y, w, 150 * S, "/" + p + " SPLIT INTO /" + np + " - " + parts.ToString("N0", CultureInfo.CurrentCulture) + " PIECES", st);
            float bx = r.X + 18 * S, bw = r.Width - 36 * S, by = r.Y + 56 * S, pw = bw / drawn;
            for (int i = 0; i < drawn; i++) Theme.Fill(g, new RectangleF(bx + i * pw + 0.5f, by, Math.Max(1, pw - 1.5f), 34 * S), 3 * S, Lane[i % 2 == 0 ? 0 : 2]);
            VText(g, parts > drawn ? L.T("showing") + " " + drawn + " " + L.T("of them") : "", Draw.fSmall, Theme.Sub, bx, by + 42 * S);
            // bug-hunt L-338: past /64 the picture says so, as the tool does
            VFoot(g, r, np > 64 ? "Smaller than /64: SLAAC and most home devices need a whole /64 per LAN - pieces this small only suit point-to-point links (/127) or loopbacks (/128)." : "Each extra bit of prefix doubles the pieces and halves their size. A /64 is the size of one LAN - never split it further.");
            return r.Bottom + 14 * S;
        }

        // ---------- streaming bitrate and data use: shares of a pipe or of a month
        float VisShare(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); bool str = tbSel == "stream";
            var r = VisCard(g, x, y, w, 150 * S, str ? "YOUR STREAM IN YOUR UPLOAD" : "A MONTH OF DATA", st);
            float bx = r.X + 18 * S, bw = r.Width - 36 * S, by = r.Y + 56 * S;
            if (str)
            {
                double up = NV(sp.F[2]), br = NV(new CF { Text = Big("Bitrate") }); Theme.Fill(g, new RectangleF(bx, by, bw, 30 * S), 8 * S, Theme.Soft);
                double scale = Math.Max(double.IsNaN(up) ? 0 : up, double.IsNaN(br) ? 0 : br * 1.3) * 1.05; if (scale <= 0) scale = 10;
                if (!double.IsNaN(br)) { var seg = new RectangleF(bx, by + 4 * S, (float)(bw * br / scale), 22 * S); Theme.Fill(g, seg, 5 * S, Theme.Blue); string bt = Big("Bitrate"); var bsz = g.MeasureString(bt, Draw.fSmall); if (bsz.Width + 12 * S <= seg.Width) VText(g, bt, Draw.fSmall, Color.White, bx + 6 * S, by + 7 * S); else VText(g, bt, Draw.fSmall, Theme.Ink, seg.Right + 6 * S, by + 7 * S); }   /* L-342: outside the bar when it doesn't fit inside */
                if (!double.IsNaN(up)) { float ux = bx + (float)(bw * up / scale); using (var p = new Pen(Theme.Ink, 2 * S)) g.DrawLine(p, ux, by - 6 * S, ux, by + 36 * S); VText(g, L.T("your upload") + " " + up.ToString("0", CultureInfo.CurrentCulture) + " Mb/s", Draw.fSmall, Theme.Ink, Math.Max(bx, ux - 120 * S), by + 40 * S); }
            }
            else
            {
                var parts = new[] { new object[] { "Video", Big("Video") }, new object[] { "Calls", Big("Calls") }, new object[] { "Games", Big("Games") } }.Select(z => new { N = (string)z[0], V = NV(new CF { Text = (string)z[1] }) }).Where(z => !double.IsNaN(z.V)).ToList();
                double tot = parts.Sum(z => z.V); float cx = bx; int i = 0;
                foreach (var p in parts) { float sw = tot > 0 ? (float)(bw * p.V / tot) : 0; Theme.Fill(g, new RectangleF(cx, by, Math.Max(2, sw - 2 * S), 30 * S), 5 * S, Lane[i++]); if (sw > 80 * S) VText(g, L.T(p.N) + " " + p.V.ToString("0", CultureInfo.CurrentCulture) + " GB", Draw.fSmall, Color.White, cx + 6 * S, by + 7 * S); cx += sw; }
                VText(g, L.T("A month") + ": " + Big("A month"), Draw.fBodyB, Theme.Ink, bx, by + 40 * S);
            }
            VFoot(g, r, "");
            return r.Bottom + 14 * S;
        }

        // ---------- bits and bytes: one byte is 8 bits, then each step is 1,000 (or 1,024)
        static float VisUnits(Graphics g, float x, float y, float w, string st)
        {
            var r = VisCard(g, x, y, w, 150 * S, "8 BITS MAKE A BYTE - THEN EACH STEP IS 1,000", st);
            float bx = r.X + 18 * S, by = r.Y + 52 * S;
            for (int i = 0; i < 8; i++) Theme.Fill(g, new RectangleF(bx + i * 16 * S, by, 13 * S, 26 * S), 3 * S, Theme.Blue);
            VText(g, "= 1 byte", Draw.fBodyB, Theme.Ink, bx + 8 * 16 * S + 6 * S, by + 4 * S);
            string[] steps = { "kB", "MB", "GB", "TB" }; float sx = bx + 230 * S;
            for (int i = 0; i < steps.Length; i++) { float s = 22 * S + i * 10 * S; Theme.Fill(g, new RectangleF(sx, by + 26 * S - s, s, s), 4 * S, Lane[i + 1]); VTextC(g, steps[i], Draw.fTiny, Theme.Sub, sx + s / 2, by + 30 * S, 50 * S); sx += s + 30 * S; }
            VFoot(g, r, "Speeds are in bits (Mb/s), files in bytes (MB): divide by 8. Windows counts in 1,024s (GiB), disk makers in 1,000s - that's the 'missing' space.");
            return r.Bottom + 14 * S;
        }

        // ---------- currency: the official rate against what a bank gives
        float VisFx(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); double mk = NV(sp.F[3]); if (double.IsNaN(mk)) mk = 2.5; string to = Opt(sp.F[2]);
            var r = VisCard(g, x, y, w, 150 * S, "WHAT YOU GET - AND WHAT THE BANK KEEPS", st);
            float bx = r.X + 150 * S, bw = r.Width - 300 * S, by = r.Y + 52 * S;
            VText(g, L.T("Official rate"), Draw.fBodyB, Theme.Ink, r.X + 18 * S, by + 2 * S); Theme.Fill(g, new RectangleF(bx, by, bw, 22 * S), 6 * S, Theme.Blue);
            mk = Math.Max(0, Math.Min(100, mk));   // bug-hunt L-336: the tool refuses outside 0-100 - the picture never draws a bar backwards
            float kw = (float)(bw * (1 - mk / 100));
            VText(g, L.T("At a bank"), Draw.fBodyB, Theme.Ink, r.X + 18 * S, by + 36 * S); Theme.Fill(g, new RectangleF(bx, by + 34 * S, kw, 22 * S), 6 * S, Theme.Green); Theme.Fill(g, new RectangleF(bx + kw, by + 34 * S, bw - kw, 22 * S), 3 * S, Theme.Red);
            using (var kf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter }) using (var kb = new SolidBrush(Theme.LvText(2))) g.DrawString(mk.ToString("0.##", CultureInfo.CurrentCulture) + "% " + L.T("kept"), Draw.fSmall, kb, new RectangleF(bx + bw + 8 * S, by + 37 * S, r.Right - (bx + bw + 8 * S) - 8 * S, 20 * S), kf);
            VText(g, Big("At the official rate"), Draw.fSmall, Theme.Sub, bx, by + 64 * S);
            return r.Bottom + 14 * S;
        }

        // ---------- P/L and position size: a price line with the marks that matter
        float VisPrice(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); bool pos = tbSel == "possize";
            var r = VisCard(g, x, y, w, 170 * S, pos ? "WHAT YOU RISK IF THE STOP IS HIT" : "BUY, BREAK-EVEN AND SELL ON ONE PRICE LINE", st);
            float bx = r.X + 40 * S, bw = r.Width - 80 * S, ly = r.Y + 92 * S;
            var marks = new List<Tuple<double, string, Color>>();
            if (pos) { double e = NV(sp.F[2]), s = NV(sp.F[3]); if (!double.IsNaN(e)) marks.Add(Tuple.Create(e, L.T("entry"), Theme.Blue)); if (!double.IsNaN(s)) marks.Add(Tuple.Create(s, L.T("stop"), Theme.Red)); }
            else { double b = NV(sp.F[0]), s = NV(sp.F[1]), be = NV(new CF { Text = Big("Break-even sell price") }); if (!double.IsNaN(b)) marks.Add(Tuple.Create(b, L.T("buy"), Theme.Blue)); if (!double.IsNaN(be)) marks.Add(Tuple.Create(be, L.T("break-even"), Theme.Amber)); if (!double.IsNaN(s)) marks.Add(Tuple.Create(s, L.T("sell"), s >= (double.IsNaN(be) ? b : be) ? Theme.Green : Theme.Red)); }
            if (marks.Count < 1) return r.Bottom + 14 * S;
            double lo = marks.Min(z => z.Item1), hi = marks.Max(z => z.Item1), pad = Math.Max((hi - lo) * 0.25, Math.Abs(hi) * 0.02 + 0.01); lo -= pad; hi += pad;
            Func<double, float> X = v => bx + (float)((v - lo) / (hi - lo)) * bw;
            using (var p = new Pen(Theme.Line, 3 * S)) g.DrawLine(p, bx, ly, bx + bw, ly);
            if (marks.Count >= 2) { var a = marks[0]; var b2 = marks[marks.Count - 1]; Theme.Fill(g, new RectangleF(Math.Min(X(a.Item1), X(b2.Item1)), ly - 5 * S, Math.Abs(X(b2.Item1) - X(a.Item1)), 10 * S), 5 * S, Color.FromArgb(90, b2.Item3)); }
            int k = 0;
            foreach (var mk in marks) { float mx = X(mk.Item1); Theme.Fill(g, new RectangleF(mx - 6 * S, ly - 6 * S, 12 * S, 12 * S), 6 * S, mk.Item3); bool up = k++ % 2 == 0; VTextC(g, mk.Item2 + " $" + mk.Item1.ToString("0.##", CultureInfo.CurrentCulture), Draw.fSmall, Theme.Ink, mx, up ? ly - 32 * S : ly + 12 * S, 140 * S); }
            string head = pos ? Big("Shares") : (VRow("Profit") != null ? Big("Profit") : Big("Loss"));
            VText(g, head, Draw.fBodyB, Theme.Ink, r.X + 18 * S, r.Y + 40 * S);
            VFoot(g, r, "The numbers only - not advice.");
            return r.Bottom + 14 * S;
        }

        // ---------- average cost: each buy as a bar, the average as a line
        float VisAvg(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); var p0 = sp.F[0].Items.Select(z => NV(new CF { Text = z })).ToList(); var n0 = sp.F[1].Items.Select(z => NV(new CF { Text = z })).ToList(); int k0 = Math.Min(p0.Count, n0.Count);
            // bug-hunt L-343: an empty price box drops that buy, not the whole picture
            var keep = Enumerable.Range(0, k0).Where(i => !double.IsNaN(p0[i])).ToList(); var p = keep.Select(i => p0[i]).ToList(); var n = keep.Select(i => n0[i]).ToList(); int k = keep.Count;
            var r = VisCard(g, x, y, w, 190 * S, "EACH BUY, AND WHERE YOUR AVERAGE LANDS", st);
            if (k == 0) return r.Bottom + 14 * S;
            double max = p.Take(k).Max() * 1.15, tot = 0, sh = 0; for (int i = 0; i < k; i++) if (!double.IsNaN(n[i])) { tot += p[i] * n[i]; sh += n[i]; }
            float bx = r.X + 40 * S, top = r.Y + 48 * S, h = 110 * S, bwid = Math.Min(70 * S, (r.Width - 80 * S) / k - 12 * S);
            for (int i = 0; i < k; i++) { float bh = (float)(p[i] / max) * h, cx = bx + i * (bwid + 12 * S); Theme.Fill(g, new RectangleF(cx, top + h - bh, bwid, bh), 5 * S, Lane[0]); VTextC(g, "$" + p[i].ToString("0.##", CultureInfo.CurrentCulture) + " × " + n[i].ToString("0", CultureInfo.CurrentCulture), Draw.fTiny, Theme.Sub, cx + bwid / 2, top + h + 4 * S, bwid + 12 * S); }
            if (sh > 0) { float ay = top + h - (float)(tot / sh / max) * h; using (var pen = new Pen(Theme.Orange, 2 * S) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, bx - 10 * S, ay, bx + k * (bwid + 12 * S), ay); VText(g, L.T("average") + " $" + (tot / sh).ToString("0.##", CultureInfo.CurrentCulture), Draw.fBodyB, Theme.Orange, bx + k * (bwid + 12 * S) + 4 * S, ay - 9 * S); }
            return r.Bottom + 14 * S;
        }

        // ---------- a fall and the rise to get back
        float VisBack(Graphics g, float x, float y, float w, string st)
        {
            double pc = NV(new CF { Text = (VisIn()[0] ?? "").Replace("%", "").Replace("+", "") });
            var r = VisCard(g, x, y, w, 196 * S, "$100, AFTER THE MOVE - AND WHAT IT TAKES TO GET BACK", st);
            if (double.IsNaN(pc) || pc <= -100) return r.Bottom + 14 * S;
            double after = 100 * (1 + pc / 100), back = pc < 0 ? (100 / after - 1) * 100 : (100 / after - 1) * 100; double max = Math.Max(100, after) * 1.1;
            float bx = r.X + 60 * S, base0 = r.Y + 160 * S, h = 110 * S, bwid = 70 * S;
            Action<float, double, string, Color> bar = (cx, v, t, c) => { float bh = (float)(v / max) * h; Theme.Fill(g, new RectangleF(cx, base0 - bh, bwid, bh), 6 * S, c); VTextC(g, t, Draw.fSmall, Theme.Ink, cx + bwid / 2, base0 - bh - 20 * S, 160 * S); };
            bar(bx, 100, "$100", Theme.Blue); bar(bx + 120 * S, after, "$" + after.ToString("0.##", CultureInfo.CurrentCulture) + " (" + (pc > 0 ? "+" : "") + pc.ToString("0.##", CultureInfo.CurrentCulture) + "%)", pc < 0 ? Theme.Red : Theme.Green);
            VArrow(g, bx + 200 * S, base0 - (float)(after / max) * h, bx + 290 * S, base0 - h * (float)(100 / max), pc < 0 ? Theme.Green : Theme.Red, false);
            VText(g, (back > 0 ? "+" : "") + back.ToString("0.#", CultureInfo.CurrentCulture) + "% " + L.T("to get back to $100"), Draw.fBodyB, Theme.Ink, bx + 300 * S, base0 - h * (float)(100 / max) - 4 * S);
            return r.Bottom + 14 * S;
        }

        // ---------- trading latency and game ticks: time moving on, your view arriving late
        float VisTicks(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); bool game = tbSel == "tickrate"; double t = AnimT;
            var r = VisCard(g, x, y, w, 180 * S, game ? "THE SERVER'S UPDATES, AND WHEN YOU SEE THEM" : "YOUR ORDER'S ROUND TRIP WHILE THE PRICE MOVES", st);
            float x0 = r.X + 70 * S, x1 = r.Right - 70 * S, len = x1 - x0;
            if (game)
            {
                double hz = NV(new CF { Text = Opt(sp.F[0]) }), ping = NV(sp.F[1]), lerp = NV(sp.F[2]); if (double.IsNaN(lerp)) lerp = 2; if (double.IsNaN(ping)) ping = 40;
                double tms = 1000 / hz, late = ping / 2 + lerp * tms, span = 400, px = len / span, now = t * 1000 % 100000;
                VText(g, L.T("server"), Draw.fSmall, Theme.Sub, r.X + 18 * S, r.Y + 54 * S); VText(g, L.T("you see"), Draw.fSmall, Theme.Sub, r.X + 18 * S, r.Y + 104 * S);
                var old = g.Clip; g.SetClip(new RectangleF(x0, r.Y + 40 * S, len, 100 * S));
                double first = Math.Floor((now - span) / tms) * tms;
                using (var p1 = new Pen(Theme.Blue, 3 * S)) using (var p2 = new Pen(Theme.Green, 3 * S))
                    for (double tk = first; tk < now + tms; tk += tms) { float sx = x1 - (float)((now - tk) * px), vx = x1 - (float)((now - tk - late) * px); g.DrawLine(p1, sx, r.Y + 50 * S, sx, r.Y + 74 * S); g.DrawLine(p2, vx, r.Y + 100 * S, vx, r.Y + 124 * S); }
                g.Clip = old;
                VText(g, L.T("now"), Draw.fTiny, Theme.Muted, x1 - 24 * S, r.Y + 36 * S);
                VText(g, Big("You see the game"), Draw.fBodyB, Theme.Ink, x0, r.Y + 134 * S);
            }
            else
            {
                double ping = NV(sp.F[0]); if (double.IsNaN(ping)) ping = 40; float cy = r.Y + 70 * S;
                VNode(g, r.X + 40 * S, cy, 18 * S, "devices", Theme.Blue, true); VNode(g, r.Right - 40 * S, cy, 18 * S, "traffic", Theme.Green, true);
                using (var p = new Pen(Theme.Line, 3 * S)) g.DrawLine(p, x0, cy, x1, cy);
                double cyc = Math.Max(0.6, ping / 1000 * 15); double ph = (t % cyc) / cyc; float ox = ph < 0.5 ? x0 + (float)(ph * 2) * len : x1 - (float)((ph - 0.5) * 2) * len;
                Theme.Fill(g, new RectangleF(ox - 7 * S, cy - 7 * S, 14 * S, 14 * S), 7 * S, Theme.Orange);
                // the price, wiggling along underneath
                var pts = new List<PointF>(); for (int i = 0; i <= 60; i++) { double tt = t * 2 + i * 0.15; pts.Add(new PointF(x0 + len * i / 60f, r.Y + 112 * S + (float)(Math.Sin(tt) * 6 + Math.Sin(tt * 2.7) * 4) * S)); }
                var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; using (var pp = new Pen(Theme.Blue, 2 * S)) g.DrawLines(pp, pts.ToArray()); g.SmoothingMode = sm;
                VText(g, L.T("you") + "  ·  " + ping.ToString("0", CultureInfo.CurrentCulture) + " ms " + L.T("there and back (slowed down 15× here)") + "  ·  " + L.T("broker"), Draw.fSmall, Theme.Sub, x0, cy + 14 * S);
                VText(g, Big("Typical move while your order travels"), Draw.fBodyB, Theme.Ink, x0, r.Y + 134 * S);
            }
            return r.Bottom + 14 * S;
        }

        // ---------- commodities: one unit (or one contract) broken into what it holds
        float VisCommodity(Graphics g, float x, float y, float w, string st)
        {
            var sp = VSpec(); string head, sub; int cells; Color c = Theme.Amber;
            if (tbSel == "futures") { var ct = Calc.Futures.FirstOrDefault(z => z[0] == Opt(sp.F[0])); double size = ct == null ? 1000 : double.Parse(ct[1], CultureInfo.InvariantCulture); head = L.T("One contract") + " = " + size.ToString("N0", CultureInfo.CurrentCulture) + " " + (ct == null ? "" : ct[2] + "s"); sub = Big("One contract controls"); cells = 100; }
            else if (tbSel == "perunit") { head = "1 " + Opt(sp.F[1]) + " → " + Opt(sp.F[2]); sub = VRow("In CA$") != null ? Big("In CA$") : Big("In US$"); cells = Opt(sp.F[1]) == "barrel" && Opt(sp.F[2]) == "litre" ? 159 : 60; }
            else { head = (sp.F[0].Text ?? "") + " " + Opt(sp.F[1]); var first = VisRows().Skip(1).FirstOrDefault(); sub = first == null ? "" : first[2] + " " + first[1]; cells = 60; }
            var r = VisCard(g, x, y, w, 196 * S, tbSel == "futures" ? "WHAT ONE CONTRACT HOLDS - EACH SQUARE IS 1%" : "ONE UNIT, MEASURED ANOTHER WAY", st);
            float bx = r.X + 18 * S, by = r.Y + 50 * S, cs = 9 * S; int perRow = (int)Math.Max(10, Math.Min(40, (r.Width * 0.55f) / (cs + 2 * S)));
            // bug-hunt L-339: on a narrow card the squares get smaller so all of them stay above the foot
            float availH = r.Height - 50 * S - 46 * S; int rowsN = (Math.Min(cells, 160) + perRow - 1) / perRow;
            if (rowsN * (cs + 2 * S) > availH) { cs = Math.Max(3 * S, availH / rowsN - 2 * S); perRow = (int)Math.Max(10, Math.Min(60, (r.Width * 0.55f) / (cs + 2 * S))); }
            for (int i = 0; i < Math.Min(cells, 160); i++) Theme.Fill(g, new RectangleF(bx + (i % perRow) * (cs + 2 * S), by + (i / perRow) * (cs + 2 * S), cs, cs), 2 * S, Color.FromArgb(i == 0 ? 255 : 150, c));
            float tx = bx + perRow * (cs + 2 * S) + 24 * S;
            VText(g, head, Draw.fBodyB, Theme.Ink, tx, by); using (var sb = new SolidBrush(Theme.Ink)) g.DrawString(sub, Draw.fBody, sb, new RectangleF(tx, by + 26 * S, r.Right - tx - 18 * S, 60 * S));
            VFoot(g, r, tbSel == "futures" ? "Futures move on margin: a small deposit carries the whole contract's gains and losses. Specs from CME - check your exchange's." : "Barrels and bushels are volumes; gold and silver use troy ounces (31.1 g).");
            return r.Bottom + 14 * S;
        }
    }
}
