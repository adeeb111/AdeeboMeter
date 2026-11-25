// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== tool pictures, wave 7: Hardware nerds (Adeeb, 15 Nov)
    // The PC as a set of pipes: the disk and the internet pour into memory, memory feeds the processor, the processor feeds the
    // graphics and the screen. Each part is drawn against its own ceiling; the narrowest pipe is red. Everything is read from the
    // result rows (so the Example, the phone page and an AI see the same thing); the live map also moves with the PC right now.
    sealed partial class FullView
    {
        static readonly HashSet<string> Pictured7 = new HashSet<string> { "pcwhy", "hwmap", "feltslow", "diskspeed", "ramcheck", "gpucheck", "cpuheld", "netcap", "laptop" };
        static readonly HashSet<string> Moving7 = new HashSet<string> { "hwmap", "cpuheld", "netcap" };
        static readonly string[][] IdleSticks = { new[] { "3", "Channel A", "8 GB DDR4 · 3200 MT/s" }, new[] { "3", "Channel B", "8 GB DDR4 · 3200 MT/s" } };
        static double HN(string text, string pat) { return VNum(text, pat); }

        float Wave7(Graphics g, float x, float y, float w, string st)
        {
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                switch (tbSel)
                {
                    case "pcwhy": return WhyPic(g, x, y, w, st);
                    case "hwmap": return PcMapPic(g, x, y, w, st);
                    case "feltslow": return FeltPic(g, x, y, w, st);
                    case "diskspeed": return DiskPic(g, x, y, w, st);
                    case "ramcheck": return RamPic(g, x, y, w, st);
                    case "gpucheck": return GpuPic(g, x, y, w, st);
                    case "cpuheld": return CpuPic(g, x, y, w, st);
                    case "netcap": return NetCapPic(g, x, y, w, st);
                    default: return LaptopPic(g, x, y, w, st);
                }
            }
            finally { g.SmoothingMode = sm; }
        }

        // a part: a rounded box with a title, a spec line, a live line and an optional fill bar (0..1)
        static void Part(Graphics g, RectangleF b, string icon, string title, string spec, string live, double fill, Color c, bool neck)
        {
            Theme.Fill(g, b, 10 * S, Theme.Soft);
            if (neck) using (var p = new Pen(Theme.Red, 2.5f * S)) using (var path = Theme.Round(b, 10 * S)) g.DrawPath(p, path);
            Kit.Icon(g, icon, new RectangleF(b.X + 10 * S, b.Y + 10 * S, 18 * S, 18 * S), c);
            VText(g, L.T(title), Draw.fBodyB, Theme.Ink, b.X + 34 * S, b.Y + 9 * S);
            using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(spec, Draw.fTiny, sb, new RectangleF(b.X + 10 * S, b.Y + 34 * S, b.Width - 20 * S, 32 * S));
            if (!double.IsNaN(fill)) { var bar = new RectangleF(b.X + 10 * S, b.Bottom - 30 * S, b.Width - 20 * S, 8 * S); Theme.Fill(g, bar, 4 * S, Theme.Line); Theme.Fill(g, new RectangleF(bar.X, bar.Y, Math.Max(4 * S, bar.Width * (float)Math.Max(0, Math.Min(1, fill))), bar.Height), 4 * S, c); }
            VText(g, live, Draw.fTiny, c, b.X + 10 * S, b.Bottom - 19 * S);
        }
        // a pipe between two parts with dots moving at a speed that follows the data rate (0 = still)
        static void Pipe(Graphics g, PointF a, PointF b, float width, double rate, Color c, bool neck)
        {
            using (var p = new Pen(neck ? Color.FromArgb(90, Theme.Red) : Theme.Line, width) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, a, b);
            if (rate <= 0 || !WindowsAnimates()) return;
            float sp = (float)Math.Min(1.6, 0.25 + Math.Log10(1 + rate) * 0.45); int n = 3 + (int)Math.Min(4, Math.Log10(1 + rate));
            for (int i = 0; i < n; i++)
            {
                float f = Wrap((float)AnimT * sp + i / (float)n, 1f); float px = a.X + (b.X - a.X) * f, py = a.Y + (b.Y - a.Y) * f;
                using (var br = new SolidBrush(c)) g.FillEllipse(br, px - 3.5f * S, py - 3.5f * S, 7 * S, 7 * S);
            }
        }

        // ---------- why the PC struggles: symptom → what that does → the maths → why → the fixes, like a network diagnosis
        float WhyPic(Graphics g, float x, float y, float w, string st)
        {
            var steps = new[] { Tuple.Create("1. Symptom", "WHAT'S FULL", Theme.LvText(2)), Tuple.Create("2. What that does", "WHAT THAT DOES", Theme.LvText(1)), Tuple.Create("3. The maths", "THE MATHS", Theme.Sub), Tuple.Create("4. Why it's full", "WHY", Theme.BlueText) };
            var have = steps.Where(t => RRow(t.Item1) != null).ToList(); bool idle = have.Count == 0;
            if (idle) have = steps.ToList();
            var fixes = Res().Where(o => o[1].StartsWith("Fix ", StringComparison.Ordinal)).Take(4).ToList();
            float bw = (w - 36 * S - (have.Count - 1) * 26 * S) / have.Count, bh = 150 * S;
            var r = VisCard(g, x, y, w, 60 * S + bh + 30 * S + Math.Max(1, fixes.Count) * 26 * S + 50 * S, "WHY THIS PC IS STRUGGLING - STEP BY STEP", st);
            for (int i = 0; i < have.Count; i++)
            {
                var t = have[i]; var b = new RectangleF(r.X + 18 * S + i * (bw + 26 * S), r.Y + 44 * S, bw, bh);
                Theme.Fill(g, b, 10 * S, Theme.Soft);   // no bar down the side - the heading carries the colour (24 Nov)
                VText(g, Kit.Sent(t.Item2), Draw.fTiny, t.Item3, b.X + 12 * S, b.Y + 8 * S);
                string txt = idle ? (i == 0 ? L.T("what is full or busy - memory, processor, disk, drivers") : i == 1 ? L.T("what that does to every click") : i == 2 ? L.T("the numbers behind the feeling") : L.T("the reason it's full: the apps, too little memory, a leak, heat")) : RV(t.Item1);
                using (var sb = new SolidBrush(Theme.Ink)) g.DrawString(Cut(txt, 260), Draw.fSmall, sb, new RectangleF(b.X + 12 * S, b.Y + 26 * S, b.Width - 20 * S, b.Height - 30 * S));
                if (i < have.Count - 1) VArrow(g, b.Right + 4 * S, b.Y + bh / 2, b.Right + 22 * S, b.Y + bh / 2, Theme.Muted, false);
            }
            float fy = r.Y + 44 * S + bh + 18 * S; VText(g, Kit.Sent("WHAT FIXES IT"), Draw.fTiny, Theme.LvText(0), r.X + 18 * S, fy); fy += 18 * S;
            if (fixes.Count == 0) VText(g, L.T("each fix with what it gives back (memory freed, processor returned)"), Draw.fSmall, Theme.Sub, r.X + 18 * S, fy);
            foreach (var f in fixes) { Theme.Fill(g, new RectangleF(r.X + 18 * S, fy + 5 * S, 8 * S, 8 * S), 4 * S, Theme.Green); VText(g, Cut(f[2], 150), Draw.fSmall, Theme.Ink, r.X + 32 * S, fy); fy += 26 * S; }
            VFoot(g, r, "The same depth as a network diagnosis: not just who uses the most, but what's full, what that does, the maths, and what each fix gives back.");
            return r.Bottom + 14 * S;
        }

        // ---------- the whole PC, live
        float PcMapPic(Graphics g, float x, float y, float w, string st)
        {
            bool ex = VisEx(), real = tbRows != null && !ex;
            string cpu = RV("Processor"), mem = RV("Memory"), disk = RV("Disk"), net = RV("Network"), neck = RV("Narrowest pipe");
            var gpuRows = Res().Where(o => o[1] == "Graphics card" || o[1] == "Built-in graphics").ToList();
            double cores = HN(cpu, @"(\d+)C/"), threads = HN(cpu, @"C/(\d+)T"); if (double.IsNaN(cores)) { cores = 8; threads = 16; }
            double cpuNow = real ? M.Cpu.Last : HN(cpu, @"([\d.]+)% busy"), memNow = real ? M.Mem.Last : HN(mem, @"([\d.]+)% in use"), down = real ? M.Down.Last : HN(net, @"([\d.]+) Mb/s down");
            var lastPt = real ? PcRecord.Last(15).LastOrDefault() : null; double diskBusy = lastPt != null ? lastPt.Busy : HN(RV("Disk now"), @"([\d.]+)% busy"), diskMBs = lastPt != null ? lastPt.DiskMBs : HN(RV("Disk now"), @"([\d.]+) MB/s");
            if (!real && tbRows == null && !ex) { cpuNow = M.Cpu.Last; memNow = M.Mem.Last; down = M.Down.Last; }
            var r = VisCard(g, x, y, w, 430 * S, "THIS PC, PART BY PART - DATA MOVING NOW", st);
            float top = r.Y + 44 * S, bw = Math.Min(200 * S, (r.Width - 120 * S) / 4), bh = 112 * S, gap = (r.Width - 36 * S - 4 * bw) / 3;
            float c0 = r.X + 18 * S, c1 = c0 + bw + gap, c2 = c1 + bw + gap, c3 = c2 + bw + gap;
            var bNet = new RectangleF(c0, top, bw, bh); var bDisk = new RectangleF(c0, top + bh + 40 * S, bw, bh);
            var bRam = new RectangleF(c1, top + (bh + 40 * S) / 2, bw, bh); var bCpu = new RectangleF(c2, top + (bh + 40 * S) / 2, bw, bh + 40 * S);
            var bGpu = new RectangleF(c3, top, bw, bh); var bScr = new RectangleF(c3, top + bh + 40 * S, bw, bh);
            bool nDisk = Regex.IsMatch(neck, "disk", RegexOptions.IgnoreCase), nMem = Regex.IsMatch(neck, "memory", RegexOptions.IgnoreCase), nCpu = Regex.IsMatch(neck, "processor", RegexOptions.IgnoreCase), nGpu = Regex.IsMatch(neck, "graphics", RegexOptions.IgnoreCase) && nMem;
            Func<RectangleF, PointF> R_ = b => new PointF(b.Right, b.Y + b.Height / 2); Func<RectangleF, PointF> L_ = b => new PointF(b.X, b.Y + b.Height / 2);
            Pipe(g, R_(bNet), L_(bRam), 6 * S, down, Theme.Blue, false);
            Pipe(g, R_(bDisk), L_(bRam), 6 * S, double.IsNaN(diskMBs) ? 0 : diskMBs, VPurple, nDisk);
            Pipe(g, R_(bRam), L_(bCpu), 10 * S, double.IsNaN(cpuNow) ? 0 : cpuNow * 3, Theme.Green, nMem);
            Pipe(g, new PointF(bCpu.Right, bCpu.Y + 30 * S), L_(bGpu), 8 * S, double.IsNaN(cpuNow) ? 0 : cpuNow, Theme.Orange, nGpu);
            Pipe(g, new PointF(bGpu.X + bGpu.Width / 2, bGpu.Bottom), new PointF(bScr.X + bScr.Width / 2, bScr.Y), 8 * S, 60, Theme.Orange, false);
            Part(g, bNet, "wifi", "Network", Cut(P(net, 0) + " · " + P(net, 1), 60), double.IsNaN(down) ? "" : HwSpecs.Fmt(down) + " Mb/s coming in", double.NaN, Theme.Blue, false);
            Part(g, bDisk, "report", "Disk", Cut(P(disk, 0) + " · " + P(disk, 1), 60), double.IsNaN(diskBusy) ? Cut(P(disk, 2), 34) : HwSpecs.Fmt(diskBusy) + "% busy · " + HwSpecs.Fmt(diskMBs) + " MB/s", double.IsNaN(diskBusy) ? double.NaN : diskBusy / 100, VPurple, nDisk);
            Part(g, bRam, "layout", "Memory", Cut(P(mem, 0) + " · " + P(mem, 1), 60), double.IsNaN(memNow) ? "" : HwSpecs.Fmt(memNow) + "% in use", double.IsNaN(memNow) ? double.NaN : memNow / 100, Theme.Green, nMem);
            // the processor: one square per thread, lit as busy as it is (lit squares wander, as Windows moves work between cores)
            Part(g, bCpu, "cpu", "Processor", Cut(P(cpu, 0), 60), double.IsNaN(cpuNow) ? "" : HwSpecs.Fmt(cpuNow) + "% busy", double.NaN, Theme.Green, nCpu);
            int nt = (int)Math.Min(32, Math.Max(1, threads)), cols = nt > 16 ? 8 : nt > 8 ? 6 : 4; float cs = Math.Min(14 * S, (bCpu.Width - 20 * S) / cols - 3 * S);
            int lit = (int)Math.Round(nt * (double.IsNaN(cpuNow) ? 0.2 : cpuNow / 100.0)); int seed = WindowsAnimates() ? (int)(AnimT * 2) : 0; var rnd = new Random(seed);
            var on = new HashSet<int>(Enumerable.Range(0, nt).OrderBy(i => rnd.Next()).Take(lit));
            for (int i = 0; i < nt; i++) Theme.Fill(g, new RectangleF(bCpu.X + 10 * S + (i % cols) * (cs + 3 * S), bCpu.Y + 62 * S + (i / cols) * (cs + 3 * S), cs, cs), 3 * S, on.Contains(i) ? Theme.Green : Theme.Line);
            string gname = gpuRows.Count > 0 ? P(gpuRows.Last()[2], 0) : "graphics"; string gmem = gpuRows.Count > 0 ? P(gpuRows.Last()[2], 1) : "";
            Part(g, bGpu, "layout", gpuRows.Any(o => o[1] == "Graphics card") ? "Graphics card" : "Built-in graphics", Cut(gname + " · " + gmem, 60), gpuRows.Count > 1 ? "+ " + Cut(P(gpuRows.First()[2], 0), 28) : "", double.NaN, Theme.Orange, nGpu);
            Part(g, bScr, "layout", "Screen", Screen.PrimaryScreen.Bounds.Width + " × " + Screen.PrimaryScreen.Bounds.Height, "redrawn about 60 times a second", double.NaN, Theme.Orange, false);
            VText(g, L.T("Narrowest pipe:") + " " + Cut(Or(neck, "-"), 110), Draw.fBodyB, neck.StartsWith("nothing", StringComparison.Ordinal) || neck == "" ? Theme.Green : Theme.Red, r.X + 18 * S, bDisk.Bottom + 16 * S);
            VFoot(g, r, "Dots are data moving now: from the disk and the internet into memory, from memory to the processor, out to the graphics and the screen. Each part's own words are under the result.");
            return r.Bottom + 14 * S;
        }

        // ---------- it felt slow: the last 2 minutes, and who was to blame
        float FeltPic(Graphics g, float x, float y, float w, string st)
        {
            string who = RV("Who"), tl = RV("Timeline");
            var r = VisCard(g, x, y, w, 330 * S, "THE LAST 2 MINUTES BEFORE IT FELT SLOW", st);
            // three doors: the internet, this PC, the site / app - the guilty one lit
            string[] doors = { "The internet", "This PC", "The site or app" }; int hit = who.Contains("PC") && who.Contains("internet") ? 3 : who.Contains("PC") ? 1 : who.Contains("internet") || who.Contains("router") ? 0 : who.Contains("site") ? 2 : -1;
            float dw = (r.Width - 36 * S - 20 * S) / 3;
            for (int i = 0; i < 3; i++)
            {
                bool on = hit == i || (hit == 3 && i < 2); var b = new RectangleF(r.X + 18 * S + i * (dw + 10 * S), r.Y + 40 * S, dw, 40 * S);
                Theme.Fill(g, b, 8 * S, on ? Theme.LvSoft(2) : Theme.Soft); VTextC(g, L.T(doors[i]) + (on ? "  ←" : ""), Draw.fBodyB, on ? Theme.LvText(2) : Theme.Sub, b.X + b.Width / 2, b.Y + 11 * S, b.Width);
            }
            // the lines: processor %, disk busy %, ping (own scale)
            Func<string, List<double>> ser = name => { var m = Regex.Match(tl, name + @"\s+([-\dx ]+)"); return m.Success ? m.Groups[1].Value.Trim().Split(' ').Select(t => t == "x" ? -1 : t == "-" ? double.NaN : double.Parse(t, CultureInfo.InvariantCulture)).ToList() : new List<double>(); };
            var cpu = ser("CPU"); var disk = ser("Disk"); var mem = ser("Memory"); var ping = ser("Ping");
            if (cpu.Count < 2) { cpu = new List<double> { 10, 12, 30, 20, 15, 12 }; disk = new List<double> { 5, 8, 10, 6, 5, 4 }; mem = new List<double> { 52, 53, 58, 57, 55, 54 }; /* L-282: all four lines */ ping = new List<double> { 14, 15, 14, 16, 15, 14 }; }
            var gr = new RectangleF(r.X + 50 * S, r.Y + 100 * S, r.Width - 110 * S, 150 * S);
            using (var p = new Pen(Theme.Line, 1 * S)) { g.DrawRectangle(p, gr.X, gr.Y, gr.Width, gr.Height); g.DrawLine(p, gr.X, gr.Y + gr.Height * 0.1f, gr.Right, gr.Y + gr.Height * 0.1f); }
            VText(g, "100%", Draw.fTiny, Theme.Sub, r.X + 14 * S, gr.Y - 6 * S); VText(g, "0", Draw.fTiny, Theme.Sub, r.X + 30 * S, gr.Bottom - 10 * S);
            double pmax = Math.Max(50, ping.Where(v => !double.IsNaN(v) && v >= 0).DefaultIfEmpty(50).Max() * 1.1); VText(g, HwSpecs.Fmt(pmax) + " ms", Draw.fTiny, Theme.Amber, gr.Right + 4 * S, gr.Y - 6 * S);
            Action<List<double>, double, Color> line = (v, max, c) =>
            {
                var pts = new List<PointF>(); for (int i = 0; i < v.Count; i++) { if (double.IsNaN(v[i]) || v[i] < 0) { if (pts.Count > 1) using (var p = new Pen(c, 2.5f * S)) g.DrawLines(p, pts.ToArray()); pts.Clear(); continue; } pts.Add(new PointF(gr.X + gr.Width * i / Math.Max(1, v.Count - 1), gr.Bottom - gr.Height * (float)Math.Min(1, v[i] / max))); }
                if (pts.Count > 1) using (var p = new Pen(c, 2.5f * S)) g.DrawLines(p, pts.ToArray());
            };
            line(cpu, 100, Theme.Blue); line(disk, 100, VPurple); line(mem, 100, Theme.Green); line(ping, pmax, Theme.Amber);
            for (int i = 0; i < ping.Count; i++) if (ping[i] < 0) { float px = gr.X + gr.Width * i / Math.Max(1, ping.Count - 1); VTextC(g, "×", Draw.fBodyB, Theme.Red, px, gr.Y + 4 * S, 20 * S); }
            float lx = gr.X; foreach (var k in new[] { Tuple.Create("processor %", Theme.Blue), Tuple.Create("disk busy %", VPurple), Tuple.Create("memory %", Theme.Green), Tuple.Create("internet ms (× = lost)", Theme.Amber) })
            { Theme.Fill(g, new RectangleF(lx, gr.Bottom + 10 * S, 14 * S, 6 * S), 3 * S, k.Item2); VText(g, L.T(k.Item1), Draw.fTiny, Theme.Sub, lx + 18 * S, gr.Bottom + 5 * S); lx += 150 * S; }
            VFoot(g, r, Cut(Or(RV("Verdict"), "Press 'It feels slow' (tray menu, or Ctrl+Alt+S) the moment it happens: the last 2 minutes are frozen and judged."), 230));
            return r.Bottom + 14 * S;
        }

        // ---------- disk: read and write against the ceiling, the write curve with its cliff, one small read on a log scale
        float DiskPic(Graphics g, float x, float y, float w, string st)
        {
            double ceil = HN(RV("Ceiling"), @"([\d,.]+)\s*MB/s"), rd = HN(RV("Read"), @"([\d,.]+)\s*MB/s"), wr = HN(RV("Write"), @"([\d,.]+)\s*MB/s"), lat = HN(RV("One small read"), @"([\d.]+)\s*ms");
            var series = Regex.Matches(RV("Write speed each"), @"\d+").Cast<Match>().Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
            if (double.IsNaN(ceil)) ceil = 3500;
            var r = VisCard(g, x, y, w, 400 * S, "THIS DISK AGAINST ITS OWN CEILING", st);
            float bx = r.X + 110 * S, bw = r.Width - 260 * S, by = r.Y + 46 * S;
            foreach (var t in new[] { Tuple.Create("Read", rd), Tuple.Create("Write", wr) })
            {
                VText(g, L.T(t.Item1), Draw.fBodyB, Theme.Ink, r.X + 18 * S, by); Theme.Fill(g, new RectangleF(bx, by + 2 * S, bw, 16 * S), 5 * S, Theme.Soft);
                double f = double.IsNaN(t.Item2) ? 0 : Math.Min(1, t.Item2 / ceil); Color c = f < 0.2 ? Theme.Red : f < 0.4 ? Theme.Amber : Theme.Green;
                Theme.Fill(g, new RectangleF(bx, by + 2 * S, Math.Max(4 * S, bw * (float)f), 16 * S), 5 * S, c);
                VText(g, double.IsNaN(t.Item2) ? "-" : HwSpecs.Fmt(t.Item2) + " MB/s · " + (100 * f).ToString("0", CultureInfo.CurrentCulture) + "%", Draw.fSmall, Theme.Sub, bx + bw + 10 * S, by + 1 * S); by += 30 * S;
            }
            VText(g, L.T("ceiling") + " " + HwSpecs.Fmt(ceil) + " MB/s", Draw.fTiny, Theme.Sub, bx + bw - 90 * S, by - 4 * S);
            // the write curve
            var gr = new RectangleF(r.X + 60 * S, by + 20 * S, r.Width - 340 * S, 150 * S);
            using (var p = new Pen(Theme.Line, 1 * S)) g.DrawRectangle(p, gr.X, gr.Y, gr.Width, gr.Height);
            VText(g, L.T("write speed, every ½ second"), Draw.fTiny, Theme.Sub, gr.X, gr.Y - 16 * S);
            if (series.Count < 2) series = new List<double> { 1800, 1850, 1820, 1790, 1800, 1810, 240, 230, 235, 228 };
            double smax = Math.Max(series.Max(), 1) * 1.1; var pts = series.Select((v, i) => new PointF(gr.X + gr.Width * i / (series.Count - 1), gr.Bottom - gr.Height * (float)(v / smax))).ToArray();
            using (var p = new Pen(VPurple, 2.5f * S)) g.DrawLines(p, pts);
            string cliff = RV("Write cache runs out"); if (cliff != "") VText(g, "↓ " + L.T("cache full") + ": " + Cut(cliff, 60), Draw.fSmall, Theme.Red, gr.X + 10 * S, gr.Bottom + 6 * S);
            VText(g, HwSpecs.Fmt(smax / 1.1) + " MB/s", Draw.fTiny, Theme.Sub, r.X + 14 * S, gr.Y);
            // one small read on a log scale: SSD 0.1 ms, eMMC 1 ms, spinning disk 10 ms
            float lx = gr.Right + 40 * S, lw = r.Right - 30 * S - lx, ly = gr.Y + 40 * S;
            VText(g, L.T("one small read"), Draw.fTiny, Theme.Sub, lx, gr.Y - 16 * S);
            using (var p = new Pen(Theme.Line, 6 * S) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, lx, ly, lx + lw, ly);
            Func<double, float> LX = ms => lx + lw * (float)((Math.Log10(Math.Max(0.02, Math.Min(50, ms))) + 1.7) / 3.4);
            foreach (var t in new[] { Tuple.Create(0.1, "SSD"), Tuple.Create(1.0, "eMMC"), Tuple.Create(10.0, "spinning") }) { VTextC(g, t.Item2, Draw.fTiny, Theme.Sub, LX(t.Item1), ly + 10 * S, 60 * S); VTextC(g, t.Item1.ToString("0.#", CultureInfo.CurrentCulture) + " ms", Draw.fTiny, Theme.Sub, LX(t.Item1), ly + 24 * S, 60 * S); }
            if (!double.IsNaN(lat)) { Color c = lat > 2.5 ? Theme.Red : lat > 0.6 ? Theme.Amber : Theme.Green; using (var b = new SolidBrush(c)) g.FillEllipse(b, LX(lat) - 8 * S, ly - 8 * S, 16 * S, 16 * S); VTextC(g, HwSpecs.Fmt(lat) + " ms", Draw.fBodyB, c, LX(lat), ly - 30 * S, 80 * S); }
            VFoot(g, r, Cut(Or(RV("Verdict"), "Bars: how close it gets to the most its kind and link allow. The curve: a sudden drop = the SSD's write cache filled."), 230));
            return r.Bottom + 14 * S;
        }

        // ---------- memory: the sticks in their channels, ceiling vs measured, the built-in graphics' share
        float RamPic(Graphics g, float x, float y, float w, string st)
        {
            var sticks = Res().Where(o => o[2].Contains("GB") && (o[1].Contains("DIMM") || o[1].Contains("Channel") || o[1].Contains("Bank") || o[1] == "Memory stick" || o[1].Contains("Slot"))).ToList();
            double ch = HN(RV("Channels"), @"^(\d)"), ceil = HN(RV("Ceiling"), @"([\d,.]+)\s*GB/s"), got = HN(RV("Measured"), @"([\d,.]+)\s*GB/s"), borrow = HN(RV("Built-in graphics borrows"), @"([\d.]+)\s*GB");
            if (double.IsNaN(ch)) ch = 2; if (sticks.Count == 0) sticks = IdleSticks.ToList();
            var r = VisCard(g, x, y, w, 300 * S, "THE MEMORY: STICKS, CHANNELS (LANES) AND SPEED", st);
            // two lanes to the processor; each stick sits on one
            float lx = r.X + 18 * S, ly = r.Y + 50 * S, lw = r.Width * 0.5f;
            for (int i = 0; i < 2; i++)
            {
                bool used = i < ch; float yy = ly + i * 60 * S;
                Theme.Fill(g, new RectangleF(lx, yy, lw, 44 * S), 8 * S, used ? Theme.Soft : Color.FromArgb(40, Theme.Red));
                VText(g, L.T("lane") + " " + (char)('A' + i) + (used ? "" : " - " + L.T("empty: half speed")), Draw.fTiny, used ? Theme.Sub : Theme.Red, lx + 8 * S, yy + 4 * S);
                var mine = sticks.Where((s, k) => k % 2 == i || ch < 2 && i == 0).Take(ch < 2 ? sticks.Count : 2).ToList(); float sx = lx + 90 * S;
                if (used) foreach (var s in mine) { var sr = new RectangleF(sx, yy + 10 * S, 110 * S, 26 * S); Theme.Fill(g, sr, 4 * S, Theme.Green); VTextC(g, Cut(P(s[2], 0), 18), Draw.fTiny, Color.White, sr.X + sr.Width / 2, sr.Y + 6 * S, sr.Width); sx += 120 * S; }
                if (used && WindowsAnimates()) { float f = Wrap((float)AnimT * 0.8f + i * 0.5f, 1f); using (var b = new SolidBrush(Theme.Green)) g.FillEllipse(b, lx + lw - 30 * S + f * 24 * S, yy + 18 * S, 7 * S, 7 * S); }
            }
            // ceiling vs measured
            float gx = lx + lw + 40 * S, gw = r.Right - 30 * S - gx, gy = ly + 10 * S;
            VText(g, L.T("ceiling"), Draw.fTiny, Theme.Sub, gx, gy - 14 * S); Theme.Fill(g, new RectangleF(gx, gy, gw, 18 * S), 5 * S, Theme.Soft); VText(g, HwSpecs.Fmt(ceil) + " GB/s", Draw.fSmall, Theme.Ink, gx + 6 * S, gy + 1 * S);
            if (!double.IsNaN(got)) { VText(g, L.T("measured (a copy)"), Draw.fTiny, Theme.Sub, gx, gy + 30 * S); double f = double.IsNaN(ceil) ? 0.5 : Math.Min(1, got / ceil); Theme.Fill(g, new RectangleF(gx, gy + 44 * S, Math.Max(4 * S, gw * (float)f), 18 * S), 5 * S, f < 0.25 ? Theme.Amber : Theme.Green); VText(g, HwSpecs.Fmt(got) + " GB/s", Draw.fSmall, Theme.Ink, gx + 6 * S, gy + 45 * S); }
            if (!double.IsNaN(borrow)) VText(g, L.T("built-in graphics borrow up to") + " " + HwSpecs.Fmt(borrow) + " GB", Draw.fSmall, Theme.Orange, gx, gy + 80 * S);
            VFoot(g, r, Cut(Or(RV("Verdict"), "Two lanes carry twice the data: one stick per lane is how memory gets its full speed."), 230));
            return r.Bottom + 14 * S;
        }

        // ---------- graphics: own memory vs borrowed, the link's lanes, what runs on which card
        float GpuPic(Graphics g, float x, float y, float w, string st)
        {
            var rows = VisRows(); var cards = new List<Tuple<string, string, string, string, List<string>>>();   // name, memory, use, link, apps
            Tuple<string, string, string, string, List<string>> cur = null;
            foreach (var o in rows)
            {
                if (o[0] == "-1") { cur = Tuple.Create(o[1], "", "", "", new List<string>()); cards.Add(cur); continue; }
                if (cur == null) continue;
                if (o[1] == "Memory") cur = Rep(cards, cur, o[2], null, null); else if (o[1] == "In use now") cur = Rep(cards, cur, null, o[2], null); else if (o[1] == "Link") cur = Rep(cards, cur, null, null, o[2]); else if (o[1].StartsWith("  ", StringComparison.Ordinal)) cur.Item5.Add(o[1].Trim() + " " + P(o[2], 0));
            }
            if (cards.Count == 0) cards.Add(Tuple.Create("GRAPHICS CARD: (run it to see)", "8 GB of its own", "", "PCIe 4.0 x8 now · up to PCIe 4.0 x16", new List<string>()));
            var r = VisCard(g, x, y, w, 70 * S + Math.Min(3, cards.Count) * 120 * S + 30 * S, "GRAPHICS: OWN MEMORY, BORROWED MEMORY, AND THE ROAD TO THE PROCESSOR", st);
            float yy = r.Y + 44 * S;
            foreach (var c in cards.Take(3))
            {
                bool built = c.Item1.StartsWith("BUILT-IN", StringComparison.Ordinal);
                VText(g, Cut(c.Item1, 70), Draw.fBodyB, Theme.Ink, r.X + 18 * S, yy);
                double own = HN(c.Item2, @"([\d.]+)\s*GB of its own"), bor = HN(c.Item2, @"borrow[s]?\s*(?:up to\s*)?([\d.]+)\s*GB"); if (double.IsNaN(bor)) bor = HN(c.Item2, @"([\d.]+)\s*GB more");
                float mx = r.X + 18 * S, mw = r.Width * 0.45f, tot = (float)((double.IsNaN(own) ? 0 : own) + (double.IsNaN(bor) ? 0 : bor)); if (tot <= 0) tot = 1;
                float ow = double.IsNaN(own) ? 0 : mw * (float)(own / tot);
                Theme.Fill(g, new RectangleF(mx, yy + 24 * S, mw, 20 * S), 5 * S, Theme.Soft);
                if (ow > 0) { Theme.Fill(g, new RectangleF(mx, yy + 24 * S, ow, 20 * S), 5 * S, Theme.Orange); VText(g, HwSpecs.Fmt(own) + " GB " + L.T("own"), Draw.fTiny, Color.White, mx + 6 * S, yy + 27 * S); }
                if (!double.IsNaN(bor)) VText(g, (built ? "" : "+ ") + HwSpecs.Fmt(bor) + " GB " + L.T("borrowed from the PC"), Draw.fTiny, Theme.Sub, mx + ow + 6 * S, yy + 27 * S);
                // the link: one line per lane; lit lanes are in use now
                double now = HN(c.Item4, @"x(\d+)"), max = HN(c.Item4, @"up to PCIe [\d.]+ x(\d+)");
                if (!double.IsNaN(max))
                {
                    float lx = mx + mw + 40 * S, lw2 = r.Right - 30 * S - lx; VText(g, L.T("link lanes") + ": x" + now + " / x" + max, Draw.fTiny, Theme.Sub, lx, yy + 4 * S);
                    for (int i = 0; i < (int)max && i < 16; i++) { float ly = yy + 22 * S + i * 2.6f * S; using (var p = new Pen(i < now ? Theme.Orange : Theme.Line, 1.6f * S)) g.DrawLine(p, lx, ly, lx + lw2, ly); }
                }
                VText(g, Cut(string.Join(" · ", c.Item5.Take(4).ToArray()) + (c.Item3 != "" ? "   " + c.Item3 : ""), 110), Draw.fTiny, Theme.Sub, mx, yy + 52 * S);
                yy += 120 * S;
            }
            VFoot(g, r, Cut(Or(RV("Verdict"), "Orange = the card's own fast memory; grey = what it may borrow from the PC over the slower link. Lit lines = lanes in use."), 230));
            return r.Bottom + 14 * S;
        }
        static Tuple<string, string, string, string, List<string>> Rep(List<Tuple<string, string, string, string, List<string>>> all, Tuple<string, string, string, string, List<string>> c, string mem, string use, string link)
        { var n = Tuple.Create(c.Item1, mem ?? c.Item2, use ?? c.Item3, link ?? c.Item4, c.Item5); all[all.IndexOf(c)] = n; return n; }

        // ---------- the processor: one square per thread, and the speed it really ran at
        float CpuPic(Graphics g, float x, float y, float w, string st)
        {
            string cpu = RV("Processor"); double cores = HN(cpu, @"(\d+)\s*cores"), threads = HN(cpu, @"(\d+)\s*threads"), rated = HN(cpu, @"rated\s*([\d.]+)\s*GHz"), perf = HN(RV("Speed under full load"), @"([\d.]+)%"), ghz = HN(RV("Speed under full load"), @"≈\s*([\d.]+)\s*GHz");
            if (double.IsNaN(cores)) { cores = 8; threads = 16; } if (double.IsNaN(threads)) threads = cores;
            var r = VisCard(g, x, y, w, 290 * S, "THE PROCESSOR: " + cores + " CORES (WORKERS), " + threads + " THREADS, AND ITS REAL SPEED", st);
            int nt = (int)Math.Min(64, threads), htCores = (int)Math.Max(0, Math.Min(cores, threads - cores)), cols = (int)Math.Min(16, cores); float cs = Math.Min(30 * S, (r.Width * 0.5f) / cols - 6 * S);
            double load = tbBusy && tbSel == "cpuheld" ? 100 : M.Cpu.Last; bool anim = WindowsAnimates(); var rnd = new Random(anim ? (int)(AnimT * 3) : 1);
            for (int c = 0; c < (int)cores && c < 32; c++)
            {
                float cx = r.X + 18 * S + (c % cols) * (cs + 6 * S), cy = r.Y + 48 * S + (c / cols) * (cs + 10 * S);
                Theme.Fill(g, new RectangleF(cx, cy, cs, cs), 4 * S, Theme.Soft);
                int per = c < htCores ? 2 : 1;   // bug-hunt L-280: a hybrid CPU (6 two-thread cores + 8 one-thread ones) drawn as it is
                for (int t = 0; t < per; t++) { bool on = rnd.NextDouble() * 100 < (double.IsNaN(load) ? 20 : load); Theme.Fill(g, new RectangleF(cx + 3 * S + t * (cs - 6 * S) / per, cy + 3 * S, (cs - 6 * S) / per - 2 * S, cs - 6 * S), 2 * S, on ? Theme.Green : Theme.Line); }
            }
            VText(g, htCores > 0 && htCores < cores ? L.T("each square is a core; split ones run two threads, whole ones one - lit = working now") : htCores == 0 ? L.T("each square is a core with one thread - lit = working now") : L.T("each square is a core; its halves are its two threads - lit = working now"), Draw.fTiny, Theme.Sub, r.X + 18 * S, r.Y + 48 * S + ((int)Math.Ceiling(Math.Min(32, cores) / cols)) * (cs + 10 * S));
            // the speed gauge: 100% = the rated clock; above = boost, below = held back
            float gx = r.X + r.Width * 0.62f, gy = r.Y + 150 * S, rad = Math.Min(90 * S, r.Width * 0.16f);
            var box = new RectangleF(gx - rad, gy - rad, rad * 2, rad * 2);
            using (var p = new Pen(Theme.Soft, 16 * S)) g.DrawArc(p, box, 180, 180);
            if (!double.IsNaN(perf)) { Color c = perf < 60 ? Theme.Red : perf < 85 ? Theme.Amber : Theme.Green; using (var p = new Pen(c, 16 * S)) g.DrawArc(p, box, 180, (float)(Math.Min(150, perf) / 150 * 180)); VTextC(g, HwSpecs.Fmt(perf) + "%", Draw.fTitle, c, gx, gy - 34 * S, rad * 2); }
            float a100 = (float)(Math.PI * (1 + 100.0 / 150)); using (var p = new Pen(Theme.Ink, 2 * S)) g.DrawLine(p, gx + (rad - 12 * S) * (float)Math.Cos(a100), gy + (rad - 12 * S) * (float)Math.Sin(a100), gx + (rad + 12 * S) * (float)Math.Cos(a100), gy + (rad + 12 * S) * (float)Math.Sin(a100));
            VTextC(g, L.T("speed under full load") + (double.IsNaN(ghz) ? "" : " ≈ " + ghz.ToString("0.0#", CultureInfo.CurrentCulture) + " GHz") + (double.IsNaN(rated) ? "" : " · " + L.T("rated") + " " + rated.ToString("0.0#", CultureInfo.CurrentCulture) + " GHz (" + L.T("the line") + ")"), Draw.fSmall, Theme.Sub, gx, gy + 8 * S, rad * 3.2f);
            VFoot(g, r, Cut(Or(RV("Verdict"), "Under full load a healthy processor runs at or above its rated clock (the line); well below it is held back by heat, battery saver or the power plan."), 230));
            return r.Bottom + 14 * S;
        }

        // ---------- the pipes from the internet to your files: the narrowest one is the speed you get
        float NetCapPic(Graphics g, float x, float y, float w, string st)
        {
            var pipes = new List<Tuple<string, double>>();
            pipes.Add(Tuple.Create("Internet", HN(RV("Internet"), @"([\d,.]+)\s*Mbps")));
            pipes.Add(Tuple.Create("This PC's link", HN(RV("This PC's link"), @"([\d,.]+)\s*Mbps")));
            double wifi = HN(RV("Wi-Fi link rate"), @"([\d,.]+)\s*Mbps"); if (!double.IsNaN(wifi)) pipes.Add(Tuple.Create("Wi-Fi (≈60% of its rate)", wifi * 0.6));
            pipes.Add(Tuple.Create("Disk (writing)", HN(RV("Disk write speed"), @"=\s*([\d,.]+)\s*Mbps")));
            if (pipes.All(p => double.IsNaN(p.Item2))) pipes = new List<Tuple<string, double>> { Tuple.Create("Internet", 940.0), Tuple.Create("This PC's link", 1000.0), Tuple.Create("Disk (writing)", 760.0) };
            var known = pipes.Where(p => !double.IsNaN(p.Item2)).ToList(); double max = known.Max(p => p.Item2), min = known.Min(p => p.Item2);
            var r = VisCard(g, x, y, w, 120 * S + pipes.Count * 50 * S, "FROM THE INTERNET TO YOUR FILES: THE NARROWEST PIPE WINS", st);
            float py = r.Y + 50 * S, px = r.X + 200 * S, pw = r.Width - 340 * S;
            foreach (var p in pipes)
            {
                VText(g, L.T(p.Item1), Draw.fBodyB, Theme.Ink, r.X + 18 * S, py + 6 * S);
                float th = double.IsNaN(p.Item2) ? 4 * S : Math.Max(4 * S, 30 * S * (float)(Math.Log10(1 + p.Item2) / Math.Log10(1 + max))); bool neck = !double.IsNaN(p.Item2) && p.Item2 == min && known.Count > 1;
                var pr = new RectangleF(px, py + 16 * S - th / 2, pw, th); Theme.Fill(g, pr, th / 2, neck ? Color.FromArgb(70, Theme.Red) : Theme.Soft);
                if (WindowsAnimates() && !double.IsNaN(p.Item2)) for (int k = 0; k < 4; k++) { float f = Wrap((float)AnimT * (float)(0.2 + 0.6 * p.Item2 / max) + k / 4f, 1f); using (var b = new SolidBrush(neck ? Theme.Red : Theme.Blue)) g.FillEllipse(b, px + pw * f - 4 * S, py + 12 * S, 8 * S, 8 * S); }
                VText(g, double.IsNaN(p.Item2) ? "?" : HwSpecs.Fmt(p.Item2) + " Mbps" + (neck ? "  ← " + L.T("the limit") : ""), Draw.fSmall, neck ? Theme.Red : Theme.Sub, px + pw + 10 * S, py + 8 * S);
                py += 50 * S;
            }
            VFoot(g, r, Cut(Or(RV("Verdict"), "A download can't go faster than the narrowest pipe on its way - sometimes that's the cable, the Wi-Fi or the disk, not your provider."), 230));
            return r.Bottom + 14 * S;
        }

        // ---------- the used-laptop scorecard
        float LaptopPic(Graphics g, float x, float y, float w, string st)
        {
            var tiles = new List<Tuple<string, int, string>>();
            foreach (var n in new[] { "Battery health", "Disk speed", "Disk wear", "Memory", "Graphics", "Processor", "Errors in 30 days" })
            { var o = RRow(n); tiles.Add(Tuple.Create(n, o == null ? -1 : Lv(o) < 0 && o[0] == "0" ? 0 : Lv(o), o == null ? "" : o[2])); }
            foreach (var o in Res().Where(z => z[1].StartsWith("Seller said:", StringComparison.Ordinal))) tiles.Add(Tuple.Create(o[1], o[0] == "0" ? 0 : Lv(o), o[2]));
            int cols = 4; float tw = (w - 36 * S - (cols - 1) * 10 * S) / cols, th = 92 * S; int rows = (tiles.Count + cols - 1) / cols;
            var r = VisCard(g, x, y, w, 60 * S + rows * (th + 10 * S) + 40 * S, "USED LAPTOP - THE SCORECARD", st);
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i]; var b = new RectangleF(r.X + 18 * S + (i % cols) * (tw + 10 * S), r.Y + 44 * S + (i / cols) * (th + 10 * S), tw, th);
                Theme.Fill(g, b, 10 * S, t.Item2 < 0 ? Theme.Soft : Theme.LvSoft(t.Item2)); Color c = t.Item2 < 0 ? Theme.Sub : Theme.LvText(t.Item2);
                VText(g, (t.Item2 == 0 ? "✓ " : t.Item2 == 2 ? "✗ " : t.Item2 == 1 ? "! " : "") + L.T(t.Item1), Draw.fBodyB, c, b.X + 10 * S, b.Y + 8 * S);
                using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(Cut(t.Item3, 90), Draw.fTiny, sb, new RectangleF(b.X + 10 * S, b.Y + 32 * S, b.Width - 20 * S, b.Height - 36 * S));
            }
            VFoot(g, r, Cut(Or(RV("Verdict"), "Green passes, amber is worth haggling over, red is a reason to walk away (or pay much less)."), 230));
            return r.Bottom + 14 * S;
        }

        // from the tray's "It feels slow" note: open that tool and show the moment just captured
        internal void ShowTool(string id, bool run) { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(id); if (run && !tbBusy) StartTool(); }

        // ================================================================== hands-on tests (they need a person: eyes, fingers, ears)
        List<string[]> HandsOn(string kind)
        {
            var rows = new List<string[]>(); Func<int, string, string, string, string[]> R = (lv, n, v, a) => new[] { lv.ToString(CultureInfo.InvariantCulture), n, v, a, "m" };
            try
            {
                if (kind == "screen") { int n = 0; Invoke((Action)delegate { n = ScreenTest(); }); rows.Add(R(3, "Verdict", n + " full-screen colours shown. Look for dots that stay the same colour (dead or stuck pixels) and bright patches near the edges on black (backlight bleed).", "")); rows.Add(R(3, "What to look for", "black: lit dots and bright edges · white: dark dots · red, green, blue: dots that stay another colour · grey: uneven patches", "")); }
                else if (kind == "keys") { var miss = new List<string>(); int n = 0; Invoke((Action)delegate { n = KeyTest(miss); }); rows.Add(R(miss.Count == 0 ? 0 : 1, "Verdict", n + " keys pressed" + (miss.Count == 0 ? " - every letter, number and main key answered." : ". Not pressed: " + string.Join(" ", miss.Take(30).ToArray()) + (miss.Count > 30 ? " …" : "")), miss.Count > 0 ? "If you pressed one of these and it didn't light, that key is dead or sticky." : "")); }
                else
                {
                    bool left = false, right = false; Invoke((Action)delegate { left = AskTone(true); right = AskTone(false); });
                    rows.Add(R(left ? 0 : 2, "Left speaker", left ? "heard" : "not heard on the left", "")); rows.Add(R(right ? 0 : 2, "Right speaker", right ? "heard" : "not heard on the right", ""));
                    Invoke((Action)delegate { AskCard.Show(this, L.T("After OK, talk or clap for 4 seconds - the microphone is measured."), "AdeeboMeter", MessageBoxButtons.OK, MessageBoxIcon.Information); });
                    double peak = MicPeak(4000);
                    rows.Add(R(double.IsNaN(peak) ? 3 : peak < 1 ? 2 : peak < 5 ? 1 : 0, "Microphone", double.IsNaN(peak) ? "no microphone found" : peak < 1 ? "silence - nothing came in" : "loudest " + peak.ToString("0", CultureInfo.CurrentCulture) + "% of full scale", double.IsNaN(peak) || peak < 1 ? "No sound: Settings > Privacy > Microphone (let desktop apps use it), or the mic is off / broken." : ""));
                    try { using (Process.Start(new ProcessStartInfo("microsoft.windows.camera:") { UseShellExecute = true })) { } rows.Add(R(3, "Camera", "Windows' Camera app opened - check the picture is clear and the light next to the camera comes on", "")); }
                    catch (Exception e) { if (e is OutOfMemoryException) throw; rows.Add(R(3, "Camera", "couldn't open the Camera app: " + e.Message, "")); }
                    int lv = rows.Max(o => o[0] == "3" ? 0 : int.Parse(o[0], CultureInfo.InvariantCulture));
                    rows.Insert(0, R(lv, "Verdict", lv == 0 ? "Both speakers and the microphone work." : "Something didn't work - see below.", ""));
                }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; rows.Add(R(3, "Verdict", "The test couldn't open: " + e.Message, "")); }
            return rows;
        }
        int ScreenTest()
        {
            var cols = new[] { Color.Black, Color.White, Color.Red, Color.Lime, Color.Blue, Color.FromArgb(128, 128, 128), Color.FromArgb(32, 32, 32) }; int i = 0;
            using (var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Bounds = Screen.FromControl(this).Bounds, BackColor = cols[0], TopMost = true, KeyPreview = true, Cursor = Cursors.Hand, ShowInTaskbar = false })
            {
                Action next = () => { i++; if (i >= cols.Length) f.Close(); else f.BackColor = cols[i]; };
                f.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Right) f.Close(); else next(); };
                f.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) f.Close(); else next(); };
                f.Paint += delegate(object s, PaintEventArgs e) { if (i == 0) using (var b = new SolidBrush(Color.FromArgb(70, 70, 70))) e.Graphics.DrawString(L.T("Click or press a key for the next colour · Esc to stop"), Draw.fSmall, b, 20 * S, 20 * S); };
                f.ShowDialog(this);
            }
            return Math.Min(i + 1, cols.Length);
        }
        static readonly string[] KeyRows = { "Esc F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12 PrtSc", "` 1 2 3 4 5 6 7 8 9 0 - = Back", "Tab Q W E R T Y U I O P [ ] \\", "Caps A S D F G H J K L ; ' Enter", "Shift Z X C V B N M , . / RShift", "Ctrl Win Alt Space RAlt RCtrl ← ↑ ↓ →" };
        static string KeyName(Keys k, Keys code)
        {
            switch (code)
            {
                case Keys.Escape: return "Esc"; case Keys.Oemtilde: return "`"; case Keys.OemMinus: return "-"; case Keys.Oemplus: return "="; case Keys.Back: return "Back"; case Keys.Tab: return "Tab";
                case Keys.OemOpenBrackets: return "["; case Keys.OemCloseBrackets: return "]"; case Keys.OemPipe: return "\\"; case Keys.OemBackslash: return "\\"; case Keys.CapsLock: return "Caps"; case Keys.OemSemicolon: return ";"; case Keys.OemQuotes: return "'"; case Keys.Enter: return "Enter";
                case Keys.ShiftKey: case Keys.LShiftKey: return "Shift"; case Keys.RShiftKey: return "RShift"; case Keys.Oemcomma: return ","; case Keys.OemPeriod: return "."; case Keys.OemQuestion: return "/";
                case Keys.ControlKey: case Keys.LControlKey: return "Ctrl"; case Keys.RControlKey: return "RCtrl"; case Keys.LWin: case Keys.RWin: return "Win"; case Keys.Menu: case Keys.LMenu: return "Alt"; case Keys.RMenu: return "RAlt"; case Keys.Space: return "Space";
                case Keys.PrintScreen: return "PrtSc";
                case Keys.Left: return "←"; case Keys.Up: return "↑"; case Keys.Down: return "↓"; case Keys.Right: return "→";
            }
            if (code >= Keys.D0 && code <= Keys.D9) return ((int)(code - Keys.D0)).ToString(CultureInfo.InvariantCulture);
            if (code >= Keys.A && code <= Keys.Z) return code.ToString(); if (code >= Keys.F1 && code <= Keys.F12) return code.ToString();
            return k.ToString();
        }
        sealed class KeyForm : Form
        {
            public readonly HashSet<string> Hit = new HashSet<string>(); public int Presses;
            public KeyForm() { KeyPreview = true; DoubleBuffered = true; Icon = Theme.AppIcon; Activated += delegate { Away = false; Invalidate(); }; Deactivate += delegate { Away = true; Invalidate(); }; MouseClick += (s, e) => { if (Done.Contains(e.Location)) Close(); }; }
            /* 24 Nov (Adeeb: "got stuck"): Alt or F10 put the window in menu mode, so the next keys went to the hidden system menu;
               the Win key's Start menu took the keyboard away with no sign of it; a broken Esc left no way out. Now the menu never
               opens, losing the keyboard says "click here", and Finish is a drawn button (Space or Enter can't press it) */
            public bool Away; public RectangleF Done;
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x112 && ((long)m.WParam & 0xFFF0) == 0xF100) return;   // SC_KEYMENU: Alt / F10 menu mode
                if ((m.Msg == 0x101 || m.Msg == 0x105) && (int)m.WParam == 0x2C) { Hit.Add("PrtSc"); Presses++; Invalidate(); }   // Print Screen only sends key-up
                base.WndProc(ref m);
            }
            [DllImport("user32.dll")] static extern short GetKeyState(int k);
            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (msg.Msg == 0x100 || msg.Msg == 0x104)
                {
                    var code = (Keys)(int)msg.WParam; bool ext = (((long)msg.LParam >> 24) & 1) == 1;
                    if (code == Keys.ShiftKey) code = ((long)msg.LParam >> 16 & 0xFF) == 0x36 ? Keys.RShiftKey : Keys.LShiftKey;
                    if (code == Keys.ControlKey) code = ext ? Keys.RControlKey : Keys.LControlKey; if (code == Keys.Menu) code = ext ? Keys.RMenu : Keys.LMenu;
                    if (code == Keys.Escape && Hit.Contains("Esc")) { Close(); return true; }
                    if (keyData == (Keys.Alt | Keys.F4)) { Close(); return true; }   // hunters10 ML-6: Alt+F4 closes it, as everywhere
                    Hit.Add(KeyName(keyData, code)); Presses++; Invalidate(); return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }
        }
        int KeyTest(List<string> missing)
        {
            using (var f = new KeyForm { Text = AdeeboMeter.L.T("AdeeboMeter - keyboard test (press Esc twice to finish)"), StartPosition = FormStartPosition.CenterParent, ClientSize = new Size((int)(900 * S), (int)(410 * S)), BackColor = Theme.Bg })
            {
                f.Paint += delegate(object s, PaintEventArgs e)
                {
                    var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; float yy = 16 * S;
                    VText(g, L.T("Press every key once - each one lights green. Esc twice to finish."), Draw.fBody, Theme.Sub, 16 * S, yy); yy += 34 * S;
                    foreach (var row in KeyRows)
                    {
                        float xx = 16 * S;
                        foreach (var k in row.Split(' '))
                        {
                            float kw = (k.Length > 2 ? 56 : k == "Space" ? 200 : 40) * S; if (k == "Space") kw = 220 * S; var b = new RectangleF(xx, yy, kw, 40 * S);
                            bool on = f.Hit.Contains(k); Theme.Fill(g, b, 6 * S, on ? Theme.Green : Theme.Soft); Color kc = on ? Theme.OnColor(Theme.Green) : Theme.Ink;
                            int ai = Array.IndexOf(new[] { "←", "↑", "↓", "→" }, k);
                            if (ai >= 0)
                            {
                                /* 24 Nov (Adeeb: the arrow keys disappeared): the app's own font has no arrows and GDI+ draws nothing in their
                                   place, so the four arrows are drawn as shapes */
                                float cx = b.X + b.Width / 2, cy = b.Y + b.Height / 2, r = 8 * S; var st = new[] { new PointF(-r, 0), new PointF(r, 0), new PointF(r * 0.2f, -r * 0.7f), new PointF(r * 0.2f, r * 0.7f) };
                                double ang = ai == 0 ? Math.PI : ai == 1 ? -Math.PI / 2 : ai == 2 ? Math.PI / 2 : 0; float ca = (float)Math.Cos(ang), sa = (float)Math.Sin(ang);
                                var p = st.Select(q => new PointF(cx + q.X * ca - q.Y * sa, cy + q.X * sa + q.Y * ca)).ToArray();
                                using (var pen = new Pen(kc, 2 * S) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) { g.DrawLine(pen, p[0], p[1]); g.DrawLines(pen, new[] { p[2], p[1], p[3] }); }
                            }
                            else VTextC(g, k, Draw.fSmall, kc, b.X + b.Width / 2, b.Y + 11 * S, b.Width);
                            xx += kw + 6 * S;
                        }
                        yy += 48 * S;
                    }
                    var cs = f.ClientSize; var dn = new RectangleF(cs.Width - 136 * S, cs.Height - 48 * S, 120 * S, 34 * S); f.Done = dn;
                    Theme.Fill(g, dn, dn.Height / 2, Theme.Blue); VTextC(g, L.T("Finish"), Draw.fBtn, Theme.OnBlue, dn.X + dn.Width / 2, dn.Y + 8 * S, dn.Width);
                    VText(g, f.Away ? L.T("Windows took the keyboard (Win, Alt+Tab or a notification) - click here or Alt+Tab back to carry on.") : f.Hit.Count(h => KeyRows.Any(r => r.Split(' ').Contains(h))) + " / " + KeyRows.Sum(r => r.Split(' ').Length), Draw.fSmall, f.Away ? Theme.LvText(1) : Theme.Sub, 16 * S, cs.Height - 40 * S);
                };
                Kit.Polish(f); Draw.FitOnShow(f, true); f.ShowDialog(this);
                foreach (var row in KeyRows) foreach (var k in row.Split(' ')) if (!f.Hit.Contains(k) && k != "PrtSc") missing.Add(k);   /* review 1.1.6 U1 #18: Windows 11's screen capture takes PrtSc, and many laptops have none - not a dead key */
                return f.Hit.Count(h => KeyRows.Any(r => r.Split(' ').Contains(h)));
            }
        }
        // a 1-second tone in one ear, then "did you hear it there?"
        bool AskTone(bool left)
        {
            const int rate = 44100; int n = rate; var ms = new MemoryStream(); var w = new BinaryWriter(ms);
            w.Write(0x46464952); w.Write(36 + n * 4); w.Write(0x45564157); w.Write(0x20746d66); w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(rate); w.Write(rate * 4); w.Write((short)4); w.Write((short)16); w.Write(0x61746164); w.Write(n * 4);
            for (int i = 0; i < n; i++) { short v = (short)(Math.Sin(2 * Math.PI * (left ? 440 : 660) * i / rate) * 9000 * Math.Min(1, Math.Min(i, n - i) / 2000.0)); w.Write(left ? v : (short)0); w.Write(left ? (short)0 : v); }
            ms.Position = 0; using (var p = new System.Media.SoundPlayer(ms)) { try { p.PlaySync(); } catch (InvalidOperationException) { } }
            return AskCard.Show(this, L.T(left ? "Did you hear the tone on the LEFT only?" : "Did you hear the tone on the RIGHT only?"), "AdeeboMeter", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
        // the microphone: record a few seconds through winmm, return the loudest sample as % of full scale (NaN = no microphone)
        [StructLayout(LayoutKind.Sequential)] struct WaveFmt { public short Tag, Ch; public int Rate, Bps; public short Align, Bits, Extra; }
        [StructLayout(LayoutKind.Sequential)] struct WaveHdr { public IntPtr Data; public int Len, Recorded; public IntPtr User; public int Flags, Loops; public IntPtr Next, Reserved; }
        [DllImport("winmm.dll")] static extern int waveInGetNumDevs();
        [DllImport("winmm.dll")] static extern int waveInOpen(out IntPtr h, int dev, ref WaveFmt f, IntPtr cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] static extern int waveInPrepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveInUnprepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveInAddBuffer(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveInStart(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveInReset(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveInClose(IntPtr h);
        static double MicPeak(int msec)
        {
            if (waveInGetNumDevs() <= 0) return double.NaN;
            var f = new WaveFmt { Tag = 1, Ch = 1, Rate = 16000, Bits = 16, Align = 2, Bps = 32000 }; IntPtr h;
            if (waveInOpen(out h, -1, ref f, IntPtr.Zero, IntPtr.Zero, 0) != 0) return double.NaN;
            int bytes = 32 * msec; IntPtr data = Marshal.AllocHGlobal(bytes), hdr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WaveHdr))); int hs = Marshal.SizeOf(typeof(WaveHdr));
            try
            {
                Marshal.StructureToPtr(new WaveHdr { Data = data, Len = bytes }, hdr, false);
                if (waveInPrepareHeader(h, hdr, hs) != 0 || waveInAddBuffer(h, hdr, hs) != 0 || waveInStart(h) != 0) return double.NaN;
                var sw = Stopwatch.StartNew(); while (sw.ElapsedMilliseconds < msec + 1500 && (((WaveHdr)Marshal.PtrToStructure(hdr, typeof(WaveHdr))).Flags & 1) == 0) Thread.Sleep(50);
                if (waveInReset(h) != 0) return double.NaN; var hd = (WaveHdr)Marshal.PtrToStructure(hdr, typeof(WaveHdr)); int got = Math.Min(bytes, hd.Recorded);
                var buf = new byte[got]; Marshal.Copy(data, buf, 0, got); int peak = 0; for (int i = 0; i + 1 < got; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(buf, i)));
                if (waveInUnprepareHeader(h, hdr, hs) != 0) Program.Log("mic: unprepare failed");
                return 100.0 * peak / 32768;
            }
            finally { if (waveInClose(h) != 0) Program.Log("mic: close failed"); Marshal.FreeHGlobal(data); Marshal.FreeHGlobal(hdr); }
        }
    }
}
