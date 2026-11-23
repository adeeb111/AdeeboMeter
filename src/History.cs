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
    // History (on by default, Settings: 1-180 days, rolling): one line a minute (worst level, averages and peaks) plus every
    // event, one file per day in the data folder's history (%LOCALAPPDATA%\AdeeboMeter\history when installed). Today stays plain text; older days are compressed
    // (.csv.gz, ~15 KB a day) and only opened when you look at them. Every night anything older than your setting goes.
    static class History
    {
        public static string Dir { get { return Path.Combine(Program.DataDir, "history"); } }   // a portable copy keeps its own (data\history beside it); installed: %LOCALAPPDATA%\AdeeboMeter\history
        static readonly object k = new object(); static DateTime lastPrune = DateTime.MinValue; static readonly char[] CommaSep = { ',' };
        static string Name(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        static string FileFor(DateTime d, string kind) { return Path.Combine(Dir, Name(d) + (kind == "ev" ? "-events" : "") + ".csv"); }
        static string N(double v) { return double.IsNaN(v) ? "" : v.ToString("0.#", CultureInfo.InvariantCulture); }

        // One minute. Columns: time, level, cpu, mem, down, gw, net, wifi, up, gwMax, netMax, loss %, link Mb/s, MB down, MB up, network
        static readonly TimeSpan OneDay = TimeSpan.FromDays(1);   // a time of day outside 0-24 h is a damaged line: skipped, never a crash
        public const int Cols = 15;   // numeric columns after the time (index 1..14 in Day.M rows; index 0 is seconds of day)
        public static void Minute(DateTime t, double[] v, string net, double diskMs, double faults)
        {
            if (!Cfg.History) return;
            var sb = new StringBuilder(t.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            for (int i = 0; i < Cols - 1; i++) sb.Append(',').Append(i < v.Length ? N(v[i]) : "");
            sb.Append(',').Append((net ?? "").Replace(',', ' ')).Append(',').Append(N(diskMs)).Append(',').Append(N(faults));   // the PC: disk ms per request, pages read back from disk per second
            sb.Append(',').Append(((int)TimeZoneInfo.Local.GetUtcOffset(t).TotalMinutes).ToString(CultureInfo.InvariantCulture));   // AM-CG-269: the UTC offset, so the hour the clocks go back is told apart
            Write(FileFor(t, "m"), sb.ToString());
        }
        public static void Event(int lv, string kind, string text)
        {
            if (Report.Active) return;   // --report writes only the file it was asked for
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
            // keeping 180 days: before the oldest days go, the six-month review is written once (Toolbox > Six months on your network)
            if (Cfg.HistoryDays >= 180) OverTime.BeforeRollOff();
            GameHistory.TidyNow();
            lock (k)
            {
                try
                {
                    if (!Directory.Exists(Dir)) return;
                    var cut = DateTime.Today.AddDays(-(Math.Max(1, Cfg.HistoryDays) - 1));
                    foreach (var f in Directory.GetFiles(Dir))
                    try
                    {
                        string n = Path.GetFileName(f); DateTime d;
                        if (n.Length < 10 || !DateTime.TryParseExact(n.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) continue;
                        if (d < cut) { File.Delete(f); continue; }
                        if (n.EndsWith(".packing", StringComparison.OrdinalIgnoreCase))
                        {
                            // R2-041: left by a tidy that stopped. In the .gz only if the .gz was written after it (a .gz older than the day's
                            // lines means the merge never finished - the lines go back to the .csv, and are merged on this pass or the next)
                            string csv = f.Substring(0, f.Length - ".packing".Length), gz0 = csv + ".gz";
                            if (File.Exists(gz0) && File.GetLastWriteTimeUtc(gz0) > File.GetLastWriteTimeUtc(f)) File.Delete(f);
                            else if (!File.Exists(csv)) File.Move(f, csv);
                            continue;
                        }
                        if (d < DateTime.Today && n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        {
                            // a day already compressed can still get a late .csv (clock or time-zone change): add to it, never replace it
                            // bug-hunt L-46: the .csv is renamed aside first, so if its delete fails the day isn't read twice
                            string gz = f + ".gz", tmp = gz + ".tmp", packing = f + ".packing";
                            File.Move(f, packing); bool merged = false;
                            try
                            {
                                using (var dst = File.Create(tmp))
                                using (var z = new GZipStream(dst, CompressionLevel.Optimal))
                                {
                                    if (File.Exists(gz)) using (var old = File.OpenRead(gz)) using (var oz = new GZipStream(old, CompressionMode.Decompress)) CopyCapped(oz, z, 64L << 20);   // bug-hunt L-265: a day is a few MB - a bigger one isn't ours
                                    using (var src = File.OpenRead(packing)) src.CopyTo(z);
                                }
                                Program.ReplaceFile(tmp, gz); merged = true;
                                File.Delete(packing);
                            }
                            finally { if (!merged && File.Exists(packing) && !File.Exists(f)) File.Move(packing, f); }   // not merged: the day goes back as it was
                        }
                    }
                    // one damaged day never stops the others: it's left as it is (its temp file removed), the rest go on
                    catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("history tidy " + Path.GetFileName(f) + ": " + e.Message); try { File.Delete(f + ".gz.tmp"); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
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

        // the compressed part of a day first, then any plain lines written after it - both, never one hiding the other
        static void CopyCapped(Stream from, Stream to, long cap)
        {
            var b = new byte[81920]; long n = 0; int got;
            while ((got = from.Read(b, 0, b.Length)) > 0) { n += got; if (n > cap) throw new InvalidDataException("the saved day is far too big to be one of ours - left as it is"); to.Write(b, 0, got); }
        }
        static string CapLine(TextReader r)
        {
            var sb = new StringBuilder(); int c; bool any = false;
            while ((c = r.Read()) >= 0) { any = true; if (c == '\n') break; if (c != '\r' && sb.Length < 4096) sb.Append((char)c); }
            return any ? sb.ToString() : null;
        }
        static List<string> Lines(string file, int max)
        {
            var list = new List<string>();
            if (File.Exists(file + ".gz"))
                using (var s = File.OpenRead(file + ".gz")) using (var z = new GZipStream(s, CompressionMode.Decompress)) using (var r = new StreamReader(z))
                {
                    // bug-hunt L-47: a damaged .gz gives what it can and the plain lines still load; L-45: each line is capped at 4 KB
                    try { string l; while ((l = CapLine(r)) != null && list.Count < max) list.Add(l); } catch (InvalidDataException e) { Program.Log("history: damaged " + Path.GetFileName(file) + ".gz - " + e.Message); }
                }
            if (File.Exists(file)) using (var r = new StreamReader(file)) { string l; while ((l = CapLine(r)) != null && list.Count < max) list.Add(l); }
            return list;
        }

        // M rows: [0] seconds of day, then the 14 numeric columns (older 10-second files fill the first 7; the rest are NaN).
        public sealed class Day { public List<double[]> M = new List<double[]>(); public List<string> Net = new List<string>(); public List<double[]> Pc = new List<double[]>(); public List<int> Off = new List<int>(); public List<Timeline.Ev> E = new List<Timeline.Ev>(); }
        // just the events of a day (cheap: no measurement rows) - the outage log reads these
        public static List<Timeline.Ev> EventsOf(DateTime d)
        {
            var l = new List<Timeline.Ev>();
            try
            {
                foreach (var line in Lines(FileFor(d, "ev"), 2000))
                {
                    var f = line.Split(CommaSep, 4); TimeSpan t; int lv; if (f.Length < 4 || !TimeSpan.TryParse(f[0], CultureInfo.InvariantCulture, out t) || t < TimeSpan.Zero || t >= OneDay || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv)) continue;
                    l.Add(new Timeline.Ev { T = d.Date + t, Lv = lv, Kind = f[2], Text = f[3] });
                }
            }
            catch (Exception e) { Program.Log("history events: " + e.Message); }
            return l;
        }
        public static Day Load(DateTime d)
        {
            var day = new Day();
            Func<string, double> P = s => { double v; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; };
            try
            {
                foreach (var line in Lines(FileFor(d, "m"), 10000))
                {
                    var f = line.Split(','); TimeSpan t; if (f.Length < 8 || !TimeSpan.TryParse(f[0], CultureInfo.InvariantCulture, out t) || t < TimeSpan.Zero || t >= OneDay) continue;
                    var row = new double[Cols]; row[0] = t.TotalSeconds;
                    for (int i = 1; i < Cols; i++) row[i] = i < f.Length ? P(f[i]) : double.NaN;
                    day.M.Add(row); day.Net.Add(f.Length > Cols ? f[Cols] : ""); day.Pc.Add(new[] { f.Length > Cols + 1 ? P(f[Cols + 1]) : double.NaN, f.Length > Cols + 2 ? P(f[Cols + 2]) : double.NaN });
                    int off; day.Off.Add(f.Length > Cols + 3 && int.TryParse(f[Cols + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out off) && off > -900 && off < 900 ? off : int.MinValue);
                }
                foreach (var line in Lines(FileFor(d, "ev"), 2000))
                {
                    var f = line.Split(CommaSep, 4); TimeSpan t; int lv; if (f.Length < 4 || !TimeSpan.TryParse(f[0], CultureInfo.InvariantCulture, out t) || t < TimeSpan.Zero || t >= OneDay || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv)) continue;
                    day.E.Add(new Timeline.Ev { T = d.Date + t, Lv = lv, Kind = f[2], Text = f[3] });
                }
            }
            catch (Exception e) { Program.Log("history load: " + e.Message); }
            day.E.Reverse();
            return day;
        }

        // The 7 x 24 heatmap: for each of the last 7 days and each hour, the worst minute-level seen and the average router delay.
        // Returns [day 0..6 (0 = 6 days ago), hour] = level (-1 = no data); gw = average gateway ms.
        public static int[,] Week(out double[,] gw) { string[,] why; return Week(out gw, out why); }
        // why: the worst thing logged in each hour (Review-1: a red square could be the PC's memory, not the internet)
        public static int[,] Week(out double[,] gw, out string[,] why)
        {
            var lv = new int[7, 24]; gw = new double[7, 24]; why = new string[7, 24]; var whyLv = new int[7, 24];
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
                foreach (var e in day.E) { int h = e.T.Hour; if (e.Lv >= 1 && (why[d, h] == null || e.Lv > whyLv[d, h])) { why[d, h] = (e.Kind ?? "") + "\u0001" + (e.Text ?? ""); whyLv[d, h] = e.Lv; } }
            }
            return lv;
        }
    }
}
