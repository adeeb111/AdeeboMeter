// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace AdeeboMeter
{
    // ================================================================== measuring the parts: disk, memory, processor
    // Each test is short, capped in time and size, and cleans up after itself (the disk test file is deleted by Windows
    // even if the app is killed: it is opened delete-on-close).
    static class HwTests
    {
        internal sealed class DiskResult
        {
            public string Error = "", Path = "", Culprit = ""; public double WriteMBs = double.NaN, ReadMBs = double.NaN, LatMs = double.NaN, Iops = double.NaN, CliffGb = double.NaN, CliffMBs = double.NaN, BusyBefore = double.NaN, Gb;
            public readonly List<double> WriteSeries = new List<double>(); public DateTime At = DateTime.Now;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool WriteFile(SafeFileHandle h, IntPtr buf, int n, out int done, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool ReadFile(SafeFileHandle h, IntPtr buf, int n, out int done, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool SetFilePointerEx(SafeFileHandle h, long to, out long now, int how);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool FlushFileBuffers(SafeFileHandle h);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr VirtualAlloc(IntPtr a, UIntPtr size, uint type, uint prot);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool VirtualFree(IntPtr a, UIntPtr size, uint type);
        const uint NoBuffering = 0x20000000, WriteThrough = 0x80000000, DeleteOnClose = 0x04000000, Temporary = 0x100;

        // full: up to 8 GB or 25 s of writing (finds a cheap SSD's cache cliff); quick: 256 MB, about 10 s in all
        internal static DiskResult Disk(string letter, bool full, Toolkit.Run run)
        {
            var r = new DiskResult();
            string dir;
            letter = (letter ?? "").Trim().TrimEnd(':', '\\').ToUpperInvariant();
            if (letter == "" || letter == HwSpecs.SystemLetter()) dir = System.IO.Path.GetTempPath(); else dir = letter + @":\";
            try
            {
                var di = new DriveInfo(System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(dir)));
                long free = di.AvailableFreeSpace, want = full ? 8L << 30 : 256L << 20;
                if (free < (1L << 30) + (256L << 20)) { r.Error = "only " + (free >> 20) + " MB free on " + di.Name + " - the test needs about 1.3 GB, so it didn't run"; return r; }
                want = Math.Min(want, Math.Max(256L << 20, (free - (2L << 30)) / 2)); want &= ~((4L << 20) - 1);
                r.Path = System.IO.Path.Combine(dir, "AdeeboMeter-disktest-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".tmp");
                // how busy the disk already is: a busy disk is reported as busy, not as slow
                if (run != null) run.Status = "Checking how busy the disk already is...";
                PcHealth.Sample(); Thread.Sleep(1000); var pv = PcHealth.Sample(); double idle; if (pv.TryGetValue("idle", out idle) && !double.IsNaN(idle)) r.BusyBefore = Math.Max(0, Math.Min(100, 100 - idle));
                r.Culprit = PcHealth.TopIo();
                const int Block = 4 << 20; IntPtr buf = VirtualAlloc(IntPtr.Zero, (UIntPtr)Block, 0x3000, 0x04);
                if (buf == IntPtr.Zero) { r.Error = "couldn't get memory for the test"; return r; }
                try
                {
                    var rnd = new byte[Block]; new Random(7).NextBytes(rnd); Marshal.Copy(rnd, 0, buf, Block);   // random bytes: some drives compress zeros and would look faster than they are
                    using (var h = CreateFileW(r.Path, 0xC0000000, 0, IntPtr.Zero, 2, NoBuffering | WriteThrough | DeleteOnClose | Temporary, IntPtr.Zero))
                    {
                        if (h.IsInvalid) { r.Error = "Windows wouldn't let it write a test file there (error " + Marshal.GetLastWin32Error() + ")"; return r; }
                        // 1. writing, second by second
                        var sw = Stopwatch.StartNew(); long done = 0, mark = 0; double lastT = 0, maxSecs = full ? 25 : 6;
                        while (done < want && sw.Elapsed.TotalSeconds < maxSecs && (run == null || !run.Stop))
                        {
                            int n; if (!WriteFile(h, buf, Block, out n, IntPtr.Zero) || n <= 0) { if (done == 0) { r.Error = "writing failed (error " + Marshal.GetLastWin32Error() + ")"; return r; } break; }
                            done += n; double t = sw.Elapsed.TotalSeconds;
                            if (t - lastT >= 0.5) { r.WriteSeries.Add((done - mark) / 1048576.0 / (t - lastT)); mark = done; lastT = t; if (run != null) run.Status = "Writing... " + (done >> 20) + " MB · " + HwSpecs.Fmt(r.WriteSeries.Last()) + " MB/s"; }
                        }
                        FlushFileBuffers(h); double wt = sw.Elapsed.TotalSeconds; r.Gb = done / 1073741824.0;
                        r.WriteMBs = done / 1048576.0 / Math.Max(0.001, wt);
                        // the cache cliff: fast at first, then a sharp, lasting drop
                        if (r.WriteSeries.Count >= 8)
                        {
                            double first = r.WriteSeries.Take(4).Average();
                            for (int i = 4; i < r.WriteSeries.Count - 3; i++)
                                if (r.WriteSeries.Skip(i).Take(4).Average() < first * 0.45) { r.CliffMBs = r.WriteSeries.Skip(i).Average(); r.CliffGb = r.WriteSeries.Take(i).Sum() * 0.5 / 1024.0; break; }
                        }
                        if (run != null && run.Stop) return r;
                        // 2. reading it back, start to end
                        if (run != null) run.Status = "Reading it back...";
                        long pos; SetFilePointerEx(h, 0, out pos, 0); sw.Restart(); long rd = 0;
                        while (rd < done && sw.Elapsed.TotalSeconds < (full ? 12 : 4)) { int n; if (!ReadFile(h, buf, Block, out n, IntPtr.Zero) || n <= 0) break; rd += n; }
                        r.ReadMBs = rd / 1048576.0 / Math.Max(0.001, sw.Elapsed.TotalSeconds);
                        // 3. small random reads: how long one 4 KB request takes - a spinning disk can't hide its spin here
                        if (run != null) run.Status = "Small random reads...";
                        var rng = new Random(11); var lat = new List<double>(); long blocks = Math.Max(1, done / 4096); var t0 = Stopwatch.StartNew();
                        for (int i = 0; i < (full ? 400 : 160) && t0.Elapsed.TotalSeconds < 5; i++)
                        {
                            long off = (long)(rng.NextDouble() * (blocks - 1)) * 4096; SetFilePointerEx(h, off, out pos, 0);
                            var one = Stopwatch.StartNew(); int n; if (!ReadFile(h, buf, 4096, out n, IntPtr.Zero)) break; lat.Add(one.Elapsed.TotalMilliseconds);
                        }
                        if (lat.Count > 10) { lat.Sort(); r.LatMs = lat[lat.Count / 2]; r.Iops = 1000.0 / Math.Max(0.001, lat.Average()); }
                    }
                }
                finally { VirtualFree(buf, UIntPtr.Zero, 0x8000); }
                SaveLast("disk", r.WriteMBs, r.ReadMBs, r.LatMs);
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; r.Error = e.Message; Program.Log("disk test: " + e.Message); }
            finally { try { if (r.Path != "" && File.Exists(r.Path)) File.Delete(r.Path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            return r;
        }

        // memory: copy blocks between two buffers on several threads; each byte is read once and written once
        internal static double MemoryGBs(Toolkit.Run run)
        {
            int th = Math.Max(1, Math.Min(8, Environment.ProcessorCount)); long total = 0; var sw = Stopwatch.StartNew();
            var ts = new List<Thread>(); var bytes = new long[th];
            for (int i = 0; i < th; i++)
            {
                int me = i;
                var t = new Thread(() =>
                {
                    var a = new byte[16 << 20]; var b = new byte[16 << 20]; var w = Stopwatch.StartNew();
                    while (w.Elapsed.TotalSeconds < 1.5 && (run == null || !run.Stop)) { Buffer.BlockCopy(a, 0, b, 0, a.Length); Buffer.BlockCopy(b, 0, a, 0, a.Length); bytes[me] += 4L * a.Length; }
                }) { IsBackground = true, Priority = ThreadPriority.BelowNormal };
                ts.Add(t); t.Start();
            }
            foreach (var t in ts) t.Join();
            total = bytes.Sum(); double gbs = total / 1e9 / Math.Max(0.001, sw.Elapsed.TotalSeconds);
            SaveLast("ram", gbs, double.NaN, double.NaN); GC.Collect();
            return gbs;
        }

        // the processor under full load for 3 s: how fast it really ran (from Windows' own "% of rated speed"), first second vs last
        internal sealed class CpuResult { public double Score = double.NaN, OneScore = double.NaN, PerfFirst = double.NaN, PerfLast = double.NaN, PerfAvg = double.NaN; }
        internal static CpuResult Cpu(Toolkit.Run run)
        {
            var r = new CpuResult(); int th = Environment.ProcessorCount; var counts = new long[th]; bool stop = false;
            // one thread alone first: how high one core boosts
            long one = 0; var w1 = Stopwatch.StartNew(); while (w1.Elapsed.TotalSeconds < 1) { one += Spin(200000); } r.OneScore = one / 1e6 / w1.Elapsed.TotalSeconds;
            var ts = new List<Thread>();
            for (int i = 0; i < th; i++) { int me = i; var t = new Thread(() => { while (!stop) counts[me] += Spin(100000); }) { IsBackground = true, Priority = ThreadPriority.BelowNormal }; ts.Add(t); t.Start(); }
            var perf = new List<double>(); var sw = Stopwatch.StartNew();
            PcHealth.Sample();
            while (sw.Elapsed.TotalSeconds < 3.2 && (run == null || !run.Stop)) { Thread.Sleep(400); double p; if (PcHealth.Sample().TryGetValue("perf", out p) && !double.IsNaN(p)) perf.Add(p); }
            stop = true; foreach (var t in ts) t.Join();
            r.Score = counts.Sum() / 1e6 / sw.Elapsed.TotalSeconds;
            if (perf.Count > 0) { r.PerfFirst = perf.First(); r.PerfLast = perf.Last(); r.PerfAvg = perf.Average(); }
            SaveLast("cpu", r.Score, r.OneScore, r.PerfAvg);
            return r;
        }
        // a fixed amount of integer work the compiler can't skip; returns how many units were done
        static long Spin(int n) { uint x = 2463534242; for (int i = 0; i < n; i++) { x ^= x << 13; x ^= x >> 17; x ^= x << 5; } return x == 0 ? n + 1 : n; }

        // ---------- the last result of each test, to say "slower than last time"
        internal static void SaveLast(string what, double a, double b, double c)
        {
            try
            {
                var path = Store.PathOf("hwlast.txt"); var lines = File.Exists(path) ? File.ReadAllLines(path).Where(l => !l.StartsWith(what + "\t", StringComparison.Ordinal)).ToList() : new List<string>();
                Func<double, string> f = v => double.IsNaN(v) ? "" : v.ToString("0.###", CultureInfo.InvariantCulture);
                lines.Add(what + "\t" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) + "\t" + f(a) + "\t" + f(b) + "\t" + f(c) + "\t" + (Last(what) == null ? "" : Last(what)[1]));
                FullView.WriteSafe(path, string.Join("\n", lines) + "\n", false);
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("hwlast: " + e.Message); }
        }
        // [when, a, b, c, previous a] or null
        internal static string[] Last(string what)
        {
            try { var path = Store.PathOf("hwlast.txt"); if (!File.Exists(path)) return null; var l = File.ReadAllLines(path).LastOrDefault(x => x.StartsWith(what + "\t", StringComparison.Ordinal)); return l == null ? null : l.Split('\t').Skip(1).ToArray(); }
            catch (IOException) { return null; } catch (UnauthorizedAccessException) { return null; }
        }
        internal static double LastNum(string what, int i) { var l = Last(what); double v; return l != null && l.Length > i && double.TryParse(l[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }
    }
}
