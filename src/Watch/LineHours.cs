// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Your line by the hour (1.1.6: Outlook, Sports, Markets)
    // From History (one line a minute on this PC): the typical internet delay for each hour of the day over the last 7 days,
    // and for one weekday-and-hour (kick-off time) over the last 6 weeks. Read on a background thread and kept 10 minutes -
    // a paint only ever gets what's ready (bug-hunt H-4's lesson). Nothing is measured here; nothing leaves the PC.
    static class LineHours
    {
        static readonly object k = new object();
        public sealed class Slot { public DateTime Day; public double Ms = double.NaN, Loss = double.NaN; public int Minutes; }
        static double[] byHour; static int daysUsed; static DateTime hourAt = DateTime.MinValue; static volatile bool busyH;
        static readonly Dictionary<string, Tuple<DateTime, List<Slot>>> slots = new Dictionary<string, Tuple<DateTime, List<Slot>>>(); static readonly HashSet<string> busyS = new HashSet<string>();
        public static long Ver;

        static double Median(List<double> v) { if (v.Count == 0) return double.NaN; v.Sort(); return v[v.Count / 2]; }
        // the typical internet delay per hour (0-23) over the last 7 days; null until read. days = how many days had data.
        public static double[] ByHour(out int days)
        {
            lock (k)
            {
                if ((byHour == null || (DateTime.UtcNow - hourAt).TotalMinutes > 10) && !busyH)
                {
                    busyH = true;
                    new Thread(() =>
                    {
                        var per = Enumerable.Range(0, 24).Select(i => new List<double>()).ToArray(); int used = 0;
                        try
                        {
                            for (int d = 1; d <= 7; d++)
                            {
                                var day = History.Load(DateTime.Today.AddDays(-d)); if (day.M.Count < 60) continue; used++;
                                foreach (var row in day.M) { double net = row[6]; if (!double.IsNaN(net) && net > 0 && net < 5000) per[Math.Min(23, (int)(row[0] / 3600))].Add(net); }
                            }
                        }
                        catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("line hours: " + e.GetType().Name); }
                        finally { lock (k) { byHour = per.Select(l => l.Count >= 10 ? Median(l) : double.NaN).ToArray(); daysUsed = used; hourAt = DateTime.UtcNow; busyH = false; Ver++; } }
                    }) { IsBackground = true, Name = "line-hours" }.Start();
                }
                days = daysUsed; return byHour;
            }
        }
        // the slow stretch, in words: "8-10 PM run about 2x slower than mornings" (null when nothing stands out or too little data)
        public static string SlowWords(double[] h, int days)
        {
            if (h == null || days < 5) return null;
            var ok = Enumerable.Range(0, 24).Where(i => !double.IsNaN(h[i])).ToList(); if (ok.Count < 12) return null;
            double best = ok.Min(i => h[i]); int worst = ok.OrderByDescending(i => h[i]).First(); if (h[worst] < best * 1.5 || h[worst] - best < 8) return null;
            int a = worst, b = worst; while (a > 0 && !double.IsNaN(h[a - 1]) && h[a - 1] >= h[worst] * 0.8) a--; while (b < 23 && !double.IsNaN(h[b + 1]) && h[b + 1] >= h[worst] * 0.8) b++;
            return Clock(a) + "-" + Clock(b + 1) + " " + L.T("runs about") + " " + (h[worst] / best).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + "x " + L.T("slower than the quietest hours");
        }
        public static string Clock(int hour) { return new DateTime(2000, 1, 1, hour % 24, 0, 0).ToString("h tt", System.Globalization.CultureInfo.CurrentCulture); }
        // the same weekday and hours as 'at' (a kick-off), over the last 6 weeks: one slot per week (null until read)
        public static List<Slot> SameSlot(DateTime at, int hours)
        {
            string key = ((int)at.DayOfWeek) + "|" + at.Hour + "|" + hours;
            lock (k)
            {
                Tuple<DateTime, List<Slot>> c;
                if ((!slots.TryGetValue(key, out c) || (DateTime.UtcNow - c.Item1).TotalMinutes > 10) && !busyS.Contains(key))
                {
                    busyS.Add(key);
                    new Thread(() =>
                    {
                        var l = new List<Slot>();
                        try
                        {
                            for (int w = 6; w >= 1; w--)
                            {
                                var d = at.Date.AddDays(-7 * w); if (d >= DateTime.Today) continue;
                                var day = History.Load(d); double from = at.TimeOfDay.TotalSeconds, to = from + hours * 3600;
                                var rows = day.M.Where(r => r[0] >= from && r[0] < to).ToList();
                                var ms = rows.Select(r => r[6]).Where(v => !double.IsNaN(v) && v > 0 && v < 5000).ToList(); var loss = rows.Select(r => r[11]).Where(v => !double.IsNaN(v)).ToList();
                                l.Add(new Slot { Day = d, Ms = ms.Count >= 10 ? Median(ms) : double.NaN, Loss = loss.Count > 0 ? loss.Average() : double.NaN, Minutes = rows.Count });
                            }
                        }
                        catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("line slot: " + e.GetType().Name); }
                        finally { lock (k) { slots[key] = Tuple.Create(DateTime.UtcNow, l); busyS.Remove(key); Ver++; } }
                    }) { IsBackground = true, Name = "line-slot" }.Start();
                }
                return c == null ? null : c.Item2;
            }
        }
    }
}
