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
            public string Error = "", Path = "", Culprit = "", Volume = "", Key = "disk"; public double WriteMBs = double.NaN, ReadMBs = double.NaN, LatMs = double.NaN, Iops = double.NaN, CliffGb = double.NaN, CliffMBs = double.NaN, BusyBefore = double.NaN, Gb;
            public bool IoFailed, Stopped, BusyUnknown;   // a failed or stopped run is never compared or kept as the last result (PC-015/016/017)
            public readonly List<double> WriteSeries = new List<double>(), WriteBytes = new List<double>(); public DateTime At = DateTime.Now;
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
            string dir, sys = HwSpecs.SystemLetter();
            letter = (letter ?? "").Trim().TrimEnd(':', '\\').ToUpperInvariant(); if (letter == "") letter = sys;
            if (letter == sys)
            {   // PC-013: the Windows drive itself - TEMP only when TEMP is on it, else this user's own folder there
                string tmp = System.IO.Path.GetTempPath(), app = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                Func<string, bool> onSys = p => { try { return p != "" && string.Equals(System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(p)).Substring(0, 1), sys, StringComparison.OrdinalIgnoreCase); } catch (ArgumentException) { return false; } };
                dir = onSys(tmp) ? tmp : onSys(app) ? app : sys + @":\";
            }
            else dir = letter + @":\";
            r.Key = letter == sys ? "disk" : "disk-" + letter;   // PC-031: each drive keeps its own last result
            try
            {
                var di = new DriveInfo(System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(dir))); r.Volume = di.Name.TrimEnd('\\');
                long free = di.AvailableFreeSpace, want = full ? 8L << 30 : 256L << 20;
                if (free < (1L << 30) + (256L << 20)) { r.Error = "only " + (free >> 20) + " MB free on " + di.Name + " - the test needs about 1.3 GB, so it didn't run"; return r; }
                want = Math.Min(want, Math.Max(256L << 20, (free - (2L << 30)) / 2)); want &= ~((4L << 20) - 1);
                r.Path = System.IO.Path.Combine(dir, "AdeeboMeter-disktest-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".tmp");
                // how busy the disk already is: a busy disk is reported as busy, not as slow. Windows' counters here are for all
                // disks together, so with more than one disk nobody can say it was this one (PC-014)
                if (run != null) run.Status = "Checking how busy the disk already is...";
                if (HwSpecs.Get().Disks.Count <= 1)
                {
                    PcHealth.Sample(); Thread.Sleep(1000); var pv = PcHealth.Sample(); double idle; if (pv.TryGetValue("idle", out idle) && !double.IsNaN(idle)) r.BusyBefore = Math.Max(0, Math.Min(100, 100 - idle));
                    r.Culprit = PcHealth.TopIo();
                }
                else r.BusyUnknown = true;
                const int Block = 4 << 20; IntPtr buf = VirtualAlloc(IntPtr.Zero, (UIntPtr)Block, 0x3000, 0x04);
                if (buf == IntPtr.Zero) { r.Error = "couldn't get memory for the test"; return r; }
                Func<string, int, DiskResult> fail = (what, err) => { r.IoFailed = true; r.Error = what + " (Windows error " + err + ") - no speed is shown for a test that didn't finish; Windows' own disk check can tell if the disk has a problem"; return r; };
                Func<bool> stop = () => run != null && run.Stop;
                try
                {
                    var rnd = new byte[Block]; new Random(7).NextBytes(rnd); Marshal.Copy(rnd, 0, buf, Block);   // random bytes: some drives compress zeros and would look faster than they are
                    using (var h = CreateFileW(r.Path, 0xC0000000, 0, IntPtr.Zero, 2, NoBuffering | WriteThrough | DeleteOnClose | Temporary, IntPtr.Zero))
                    {
                        if (h.IsInvalid) { r.Error = "Windows wouldn't let it write a test file there (error " + Marshal.GetLastWin32Error() + ")"; return r; }
                        // 1. writing, second by second - each sample keeps its real length and bytes (PC-020)
                        var sw = Stopwatch.StartNew(); long done = 0, mark = 0; double lastT = 0, maxSecs = full ? 25 : 6;
                        while (done < want && sw.Elapsed.TotalSeconds < maxSecs && !stop())
                        {
                            int n; if (!WriteFile(h, buf, Block, out n, IntPtr.Zero) || n != Block) return fail("writing stopped after " + (done >> 20) + " MB", Marshal.GetLastWin32Error());   // PC-015
                            done += n; double t = sw.Elapsed.TotalSeconds;
                            if (t - lastT >= 0.5) { r.WriteSeries.Add((done - mark) / 1048576.0 / (t - lastT)); r.WriteBytes.Add(done - mark); mark = done; lastT = t; if (run != null) run.Status = "Writing... " + (done >> 20) + " MB · " + HwSpecs.Fmt(r.WriteSeries.Last()) + " MB/s"; }
                        }
                        if (stop()) { r.Stopped = true; r.Error = "stopped"; return r; }
                        if (!FlushFileBuffers(h)) return fail("finishing the write failed", Marshal.GetLastWin32Error());
                        double wt = sw.Elapsed.TotalSeconds; r.Gb = done / 1073741824.0;
                        r.WriteMBs = done / 1048576.0 / Math.Max(0.001, wt);
                        // the cache cliff: fast at first, then a sharp, lasting drop
                        if (r.WriteSeries.Count >= 8)
                        {
                            double first = r.WriteSeries.Take(4).Average();
                            for (int i = 4; i < r.WriteSeries.Count - 3; i++)
                                if (r.WriteSeries.Skip(i).Take(4).Average() < first * 0.45) { r.CliffMBs = r.WriteSeries.Skip(i).Average(); r.CliffGb = r.WriteBytes.Take(i).Sum() / 1073741824.0; break; }
                        }
                        // 2. reading it back, start to end - a failed read is a failure, not a slow disk (PC-016); Stop works here too (PC-017)
                        if (run != null) run.Status = "Reading it back...";
                        long pos; if (!SetFilePointerEx(h, 0, out pos, 0)) return fail("couldn't go back to the start of the test file", Marshal.GetLastWin32Error());
                        sw.Restart(); long rd = 0;
                        while (rd < done && sw.Elapsed.TotalSeconds < (full ? 12 : 4) && !stop()) { int n; if (!ReadFile(h, buf, Block, out n, IntPtr.Zero) || n <= 0) return fail("reading back failed after " + (rd >> 20) + " MB", Marshal.GetLastWin32Error()); rd += n; }
                        if (stop()) { r.Stopped = true; r.Error = "stopped"; return r; }
                        r.ReadMBs = rd / 1048576.0 / Math.Max(0.001, sw.Elapsed.TotalSeconds);
                        // 3. small random reads: how long one 4 KB request takes - a spinning disk can't hide its spin here
                        if (run != null) run.Status = "Small random reads...";
                        var rng = new Random(11); var lat = new List<double>(); long blocks = Math.Max(1, done / 4096); var t0 = Stopwatch.StartNew();
                        for (int i = 0; i < (full ? 400 : 160) && t0.Elapsed.TotalSeconds < 5 && !stop(); i++)
                        {
                            long off = Math.Min(blocks - 1, (long)(rng.NextDouble() * blocks)) * 4096;   // PC-018: every block, the last one too
                            if (!SetFilePointerEx(h, off, out pos, 0)) return fail("a small read couldn't seek", Marshal.GetLastWin32Error());
                            var one = Stopwatch.StartNew(); int n; if (!ReadFile(h, buf, 4096, out n, IntPtr.Zero) || n != 4096) return fail("a small read failed", Marshal.GetLastWin32Error());   // PC-019
                            lat.Add(one.Elapsed.TotalMilliseconds);
                        }
                        if (stop()) { r.Stopped = true; r.Error = "stopped"; return r; }
                        if (lat.Count > 10) { lat.Sort(); r.LatMs = lat[lat.Count / 2]; r.Iops = 1000.0 / Math.Max(0.001, lat.Average()); }
                    }
                }
                finally { VirtualFree(buf, UIntPtr.Zero, 0x8000); }
                SaveLast(r.Key, r.WriteMBs, r.ReadMBs, r.LatMs);
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; r.Error = e.Message; Program.Log("disk test: " + e.Message); }
            finally { try { if (r.Path != "" && File.Exists(r.Path)) File.Delete(r.Path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            return r;
        }

        // memory: copy blocks between two buffers on several threads; each byte is read once and written once.
        // NaN with why when it was stopped or memory ran out - and then nothing is kept as the last result (PC-025/026)
        internal static double MemoryGBs(Toolkit.Run run, out string why)
        {
            why = ""; int th = Math.Max(1, Math.Min(8, Environment.ProcessorCount)); var ts = new List<Thread>(); var bytes = new long[th]; int failed = 0;
            var go = new ManualResetEventSlim(false); bool stop = false;
            for (int i = 0; i < th; i++)
            {
                int me = i;
                var t = new Thread(() =>
                {
                    try
                    {
                        var a = new byte[16 << 20]; var b = new byte[16 << 20]; go.Wait();
                        while (!stop) { Buffer.BlockCopy(a, 0, b, 0, a.Length); Buffer.BlockCopy(b, 0, a, 0, a.Length); bytes[me] += 4L * a.Length; }
                    }
                    catch (OutOfMemoryException) { Interlocked.Exchange(ref failed, 1); go.Wait(); }   // PC-026: a worker that can't get memory reports it, it doesn't end the app
                }) { IsBackground = true, Priority = ThreadPriority.BelowNormal };
                ts.Add(t); t.Start();
            }
            Thread.Sleep(50); var sw = Stopwatch.StartNew(); go.Set();
            while (sw.Elapsed.TotalSeconds < 1.5 && (run == null || !run.Stop) && failed == 0) Thread.Sleep(20);
            stop = true; double secs = sw.Elapsed.TotalSeconds; foreach (var t in ts) t.Join(); go.Dispose();
            if (failed != 0) { why = "Windows didn't have enough free memory for the test - close some programs and try again"; GC.Collect(); return double.NaN; }
            if (run != null && run.Stop) { why = "stopped"; GC.Collect(); return double.NaN; }
            double gbs = bytes.Sum() / 1e9 / Math.Max(0.001, secs);
            SaveLast("ram", gbs, double.NaN, double.NaN); GC.Collect();
            return gbs;
        }

        // the processor under full load for 3 s: how fast it really ran (from Windows' own "% of rated speed"), first second vs last
        internal sealed class CpuResult { public double Score = double.NaN, OneScore = double.NaN, PerfFirst = double.NaN, PerfLast = double.NaN, PerfAvg = double.NaN; public bool Stopped; }
        internal static CpuResult Cpu(Toolkit.Run run)
        {
            var r = new CpuResult(); int th = Environment.ProcessorCount; var counts = new long[th]; bool stop = false;
            Func<bool> halt = () => run != null && run.Stop;
            // one thread alone first: how high one core boosts (PC-028: Stop is checked before and during each stage)
            if (halt()) { r.Stopped = true; return r; }
            long one = 0; var w1 = Stopwatch.StartNew(); while (w1.Elapsed.TotalSeconds < 1 && !halt()) { one += Spin(200000); } r.OneScore = one / 1e6 / w1.Elapsed.TotalSeconds;
            if (halt()) { r.Stopped = true; return r; }
            // PC-027: every worker waits at the gate; the clock starts when the gate opens, so only timed work is counted
            var ts = new List<Thread>(); var go = new ManualResetEventSlim(false);
            for (int i = 0; i < th; i++) { int me = i; var t = new Thread(() => { go.Wait(); while (!stop) counts[me] += Spin(100000); }) { IsBackground = true, Priority = ThreadPriority.BelowNormal }; ts.Add(t); t.Start(); }
            Thread.Sleep(50); var perf = new List<double>(); var sw = Stopwatch.StartNew(); go.Set();
            PcHealth.Sample();
            while (sw.Elapsed.TotalSeconds < 3.2 && !halt()) { Thread.Sleep(400); double p; if (PcHealth.Sample().TryGetValue("perf", out p) && !double.IsNaN(p)) perf.Add(p); }
            stop = true; double secs = sw.Elapsed.TotalSeconds; foreach (var t in ts) t.Join(); go.Dispose();
            if (halt()) { r.Stopped = true; return r; }   // a part-run isn't a score and isn't kept
            r.Score = counts.Sum() / 1e6 / secs;
            if (perf.Count > 0) { r.PerfFirst = perf.First(); r.PerfLast = perf.Last(); r.PerfAvg = perf.Average(); }
            SaveLast("cpu", r.Score, r.OneScore, r.PerfAvg);
            return r;
        }
        // a fixed amount of integer work the compiler can't skip; returns how many units were done
        static long Spin(int n) { uint x = 2463534242; for (int i = 0; i < n; i++) { x ^= x << 13; x ^= x >> 17; x ^= x << 5; } return x == 0 ? n + 1 : n; }

        // ---------- the last result of each test, to say "slower than last time"
        // PC-021/022: every measure keeps its own previous value, and two tests finishing together can't lose each other's line
        static readonly object saveLock = new object();
        internal static void SaveLast(string what, double a, double b, double c)
        {
            lock (saveLock)
                try
                {
                    var path = Store.PathOf("hwlast.txt"); var lines = File.Exists(path) ? File.ReadAllLines(path).Where(l => !l.StartsWith(what + "\t", StringComparison.Ordinal)).ToList() : new List<string>();
                    Func<double, string> f = v => double.IsNaN(v) ? "" : v.ToString("0.###", CultureInfo.InvariantCulture);
                    var prev = Last(what);
                    lines.Add(what + "\t" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) + "\t" + f(a) + "\t" + f(b) + "\t" + f(c) + "\t" + (prev == null ? "\t\t" : prev[1] + "\t" + (prev.Length > 2 ? prev[2] : "") + "\t" + (prev.Length > 3 ? prev[3] : "")));
                    FullView.WriteSafe(path, string.Join("\n", lines) + "\n", false);
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("hwlast: " + e.Message); }
        }
        // [when, a, b, c, previous a, previous b, previous c] or null
        internal static string[] Last(string what)
        {
            try { var path = Store.PathOf("hwlast.txt"); if (!File.Exists(path)) return null; var l = File.ReadAllLines(path).LastOrDefault(x => x.StartsWith(what + "\t", StringComparison.Ordinal)); return l == null ? null : l.Split('\t').Skip(1).ToArray(); }
            catch (IOException) { return null; } catch (UnauthorizedAccessException) { return null; }
        }
        internal static double LastNum(string what, int i) { var l = Last(what); double v; return l != null && l.Length > i && double.TryParse(l[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }
    }
}
