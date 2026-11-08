// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace AdeeboMeter
{
    // History (on by default, Settings: 1-90 days, rolling): one line a minute (worst level, averages and peaks) plus every
    // event, one file per day in %LOCALAPPDATA%\AdeeboMeter\history. Today stays plain text; older days are compressed
    // (.csv.gz, ~15 KB a day) and only opened when you look at them. Every night anything older than your setting goes.
    static class History
    {
        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdeeboMeter", "history"); } }
        static readonly object k = new object(); static DateTime lastPrune = DateTime.MinValue; static readonly char[] CommaSep = { ',' };
        static string Name(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        static string FileFor(DateTime d, string kind) { return Path.Combine(Dir, Name(d) + (kind == "ev" ? "-events" : "") + ".csv"); }
        static string N(double v) { return double.IsNaN(v) ? "" : v.ToString("0.#", CultureInfo.InvariantCulture); }

        // One minute. Columns: time, level, cpu, mem, down, gw, net, wifi, up, gwMax, netMax, loss %, link Mb/s, MB down, MB up, network
        public const int Cols = 15;   // numeric columns after the time (index 1..14 in Day.M rows; index 0 is seconds of day)
        public static void Minute(DateTime t, double[] v, string net, double diskMs, double faults)
        {
            if (!Cfg.History) return;
            var sb = new StringBuilder(t.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            for (int i = 0; i < Cols - 1; i++) sb.Append(',').Append(i < v.Length ? N(v[i]) : "");
            sb.Append(',').Append((net ?? "").Replace(',', ' ')).Append(',').Append(N(diskMs)).Append(',').Append(N(faults));   // the PC: disk ms per request, pages read back from disk per second
            Write(FileFor(t, "m"), sb.ToString());
        }
        public static void Event(int lv, string kind, string text)
        {
            if (!Cfg.History) return;
            Write(FileFor(DateTime.Now, "ev"), DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "," + lv.ToString(CultureInfo.InvariantCulture) + "," + kind + "," + (text ?? "").Replace('\n', ' ').Replace("\r", ""));
        }
        static void Write(string file, string line)
        {
            lock (k)
            {
                try
                {
                    Directory.CreateDirectory(Dir); File.AppendAllText(file, line + "\r\n");
                    if ((DateTime.UtcNow - lastPrune).TotalHours > 6) { lastPrune = DateTime.UtcNow; System.Threading.ThreadPool.QueueUserWorkItem(delegate { Tidy(); }); }   // never on the 1-second sampler
                }
                catch (Exception e) { Program.Log("history: " + e.Message); }
            }
        }

        // Rolling delete by the day in the file name, then compress every finished day.
        public static void Tidy()
        {
            lock (k)
            {
                try
                {
                    if (!Directory.Exists(Dir)) return;
                    var cut = DateTime.Today.AddDays(-(Math.Max(1, Cfg.HistoryDays) - 1));
                    foreach (var f in Directory.GetFiles(Dir))
                    {
                        string n = Path.GetFileName(f); DateTime d;
                        if (n.Length < 10 || !DateTime.TryParseExact(n.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) continue;
                        if (d < cut) { File.Delete(f); continue; }
                        if (d < DateTime.Today && n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var src = File.OpenRead(f))
                            using (var dst = File.Create(f + ".gz"))
                            using (var z = new GZipStream(dst, CompressionLevel.Optimal)) src.CopyTo(z);
                            File.Delete(f);
                        }
                    }
                }
                catch (Exception e) { Program.Log("history tidy: " + e.Message); }
            }
        }
        public static void Clear() { lock (k) { try { if (Directory.Exists(Dir)) foreach (var f in Directory.GetFiles(Dir)) File.Delete(f); } catch (Exception e) { Program.Log("history clear: " + e.Message); } } }
        public static long Bytes() { try { return Directory.Exists(Dir) ? Directory.GetFiles(Dir).Sum(f => new FileInfo(f).Length) : 0; } catch { return 0; } }

        public static List<DateTime> Days()
        {
            var r = new HashSet<DateTime>();
            try
            {
                if (!Directory.Exists(Dir)) return new List<DateTime>();
                foreach (var f in Directory.GetFiles(Dir, "????-??-??.csv*"))
                { DateTime d; if (DateTime.TryParseExact(Path.GetFileName(f).Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) r.Add(d); }
            }
            catch { }
            return r.OrderByDescending(d => d).ToList();
        }

        static IEnumerable<string> Lines(string file, int max)
        {
            if (File.Exists(file)) return File.ReadLines(file).Take(max);
            if (File.Exists(file + ".gz"))
            {
                var list = new List<string>();
                using (var s = File.OpenRead(file + ".gz")) using (var z = new GZipStream(s, CompressionMode.Decompress)) using (var r = new StreamReader(z))
                { string l; while ((l = r.ReadLine()) != null && list.Count < max) list.Add(l); }
                return list;
            }
            return Enumerable.Empty<string>();
        }

        // M rows: [0] seconds of day, then the 14 numeric columns (older 10-second files fill the first 7; the rest are NaN).
        public sealed class Day { public List<double[]> M = new List<double[]>(); public List<string> Net = new List<string>(); public List<double[]> Pc = new List<double[]>(); public List<Timeline.Ev> E = new List<Timeline.Ev>(); }
        public static Day Load(DateTime d)
        {
            var day = new Day();
            Func<string, double> P = s => { double v; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; };
            try
            {
                foreach (var line in Lines(FileFor(d, "m"), 10000))
                {
                    var f = line.Split(','); TimeSpan t; if (f.Length < 8 || !TimeSpan.TryParse(f[0], CultureInfo.InvariantCulture, out t)) continue;
                    var row = new double[Cols]; row[0] = t.TotalSeconds;
                    for (int i = 1; i < Cols; i++) row[i] = i < f.Length ? P(f[i]) : double.NaN;
                    day.M.Add(row); day.Net.Add(f.Length > Cols ? f[Cols] : ""); day.Pc.Add(new[] { f.Length > Cols + 1 ? P(f[Cols + 1]) : double.NaN, f.Length > Cols + 2 ? P(f[Cols + 2]) : double.NaN });
                }
                foreach (var line in Lines(FileFor(d, "ev"), 2000))
                {
                    var f = line.Split(CommaSep, 4); TimeSpan t; int lv; if (f.Length < 4 || !TimeSpan.TryParse(f[0], CultureInfo.InvariantCulture, out t) || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv)) continue;
                    day.E.Add(new Timeline.Ev { T = d.Date + t, Lv = lv, Kind = f[2], Text = f[3] });
                }
            }
            catch (Exception e) { Program.Log("history load: " + e.Message); }
            day.E.Reverse();
            return day;
        }

        // The 7 x 24 heatmap: for each of the last 7 days and each hour, the worst minute-level seen and the average router delay.
        // Returns [day 0..6 (0 = 6 days ago), hour] = level (-1 = no data); gw = average gateway ms.
        public static int[,] Week(out double[,] gw)
        {
            var lv = new int[7, 24]; gw = new double[7, 24];
            for (int d = 0; d < 7; d++) for (int h = 0; h < 24; h++) { lv[d, h] = -1; gw[d, h] = double.NaN; }
            for (int d = 0; d < 7; d++)
            {
                var day = Load(DateTime.Today.AddDays(d - 6));
                var sum = new double[24]; var cnt = new int[24];
                foreach (var r in day.M)
                {
                    int h = Math.Min(23, (int)(r[0] / 3600)); int l = double.IsNaN(r[1]) ? -1 : (int)r[1];
                    if (l >= 0 && l <= 2 && l > lv[d, h]) lv[d, h] = l;
                    if (!double.IsNaN(r[5])) { sum[h] += r[5]; cnt[h]++; }
                }
                for (int h = 0; h < 24; h++) if (cnt[h] > 0) gw[d, h] = sum[h] / cnt[h];
            }
            return lv;
        }
    }
}
