// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // ================================================================== What did Windows Update break?
    // Windows' own log of installed updates and drivers, set against AdeeboMeter's History (ping and outages per day):
    // the three days before each change against the three days after it. Says "started the day after ... and stayed",
    // never "the update caused it" - a provider can have a bad week that starts the same day. Read-only, nothing sent.
    static class UpdateBreak
    {
        public sealed class Change { public DateTime T; public string What = "", Kind = ""; }
        public sealed class DayStat { public DateTime D; public double Ping = double.NaN, Gw = double.NaN; public int Drops, Minutes; }
        public sealed class Finding { public Change C; public double PingBefore = double.NaN, PingAfter = double.NaN, DropsBefore, DropsAfter; public bool Strong, Stayed; public int Lv = 3; public string Text = ""; }

        static readonly Dictionary<DateTime, DayStat> dayCache = new Dictionary<DateTime, DayStat>(); static readonly object k = new object();
        public static List<DateTime> ChangeDays = new List<DateTime>();   // for the week heatmap: days something was installed
        public static string Strong = "";                                  // one line for the Overview when a match is strong (empty = none)

        public static List<Change> Changes(int days)
        {
            var l = new List<Change>(); string err;
            foreach (var e in EvLog.Recent("System", "EventID=19", days * 24, 80, out err).Where(x => x.Text.IndexOf("Installation Successful", StringComparison.OrdinalIgnoreCase) >= 0 || EvLog.Field(x.Xml, "updateTitle") != ""))
            { string t = EvLog.Field(e.Xml, "updateTitle"); l.Add(new Change { T = e.T, What = t != "" ? Short(t) : "a Windows update", Kind = "update" }); }
            foreach (var e in EvLog.Recent("System", "EventID=20001", days * 24, 40, out err))
            {
                l.Add(new Change { T = e.T, What = "driver: " + Draw.Trunc(Regex.Replace(e.Text ?? "", @"\s+", " "), 90), Kind = "driver" });
            }
            return l.OrderBy(c => c.T).ToList();
        }
        // "2026-11 Cumulative Update for Windows 11 Version 24H2 for x64-based Systems (KB5046617)" -> "KB5046617 (Cumulative Update)"
        public static string Short(string t)
        {
            var kb = Regex.Match(t, @"KB\d{6,8}"); string kind = Regex.IsMatch(t, "Cumulative", RegexOptions.IgnoreCase) ? "Cumulative Update" : Regex.IsMatch(t, "Defender|Security Intelligence", RegexOptions.IgnoreCase) ? "Defender definitions" : Regex.IsMatch(t, "\\.NET", RegexOptions.IgnoreCase) ? ".NET update" : Regex.IsMatch(t, "driver", RegexOptions.IgnoreCase) ? "driver" : "";
            return kb.Success ? kb.Value + (kind != "" ? " (" + kind + ")" : "") : Draw.Trunc(t, 80);
        }
        // one day from History: typical ping to the internet and to the router, and the outages that started that day
        public static DayStat Day(DateTime d)
        {
            lock (k) { DayStat c; if (d < DateTime.Today && dayCache.TryGetValue(d, out c)) return c; }
            var s = new DayStat { D = d };
            try
            {
                var day = History.Load(d);
                var net = day.M.Where(r => r.Length > 6 && !double.IsNaN(r[6])).Select(r => r[6]).ToList(); var gw = day.M.Where(r => r.Length > 5 && !double.IsNaN(r[5])).Select(r => r[5]).ToList();
                s.Minutes = day.M.Count; if (net.Count >= 30) s.Ping = Journey.Median(net); if (gw.Count >= 30) s.Gw = Journey.Median(gw);
                s.Drops = Outages.Pair(day.E.Where(e => e.Kind == "inet").OrderBy(e => e.T).ToList(), d.AddDays(1), false).Count;
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("update break day: " + e.Message); }
            lock (k) { if (d < DateTime.Today && s.Minutes > 0) dayCache[d] = s; }
            return s;
        }
        // the three days before a change against the three days after it (the day itself is mixed, so it's left out)
        public static Finding Compare(Change c, Func<DateTime, DayStat> day)
        {
            var f = new Finding { C = c }; var d0 = c.T.Date;
            var before = Enumerable.Range(1, 3).Select(i => day(d0.AddDays(-i))).Where(s => !double.IsNaN(s.Ping)).ToList();
            var after = Enumerable.Range(1, 3).Select(i => day(d0.AddDays(i))).Where(s => s.D <= DateTime.Today && !double.IsNaN(s.Ping)).ToList();
            if (before.Count < 2 || after.Count < 1) { f.Text = "not enough History around it to compare (" + before.Count + " day(s) before, " + after.Count + " after)"; return f; }
            f.PingBefore = Journey.Median(before.Select(s => s.Ping).ToList()); f.PingAfter = Journey.Median(after.Select(s => s.Ping).ToList());
            f.DropsBefore = before.Average(s => s.Drops); f.DropsAfter = after.Average(s => s.Drops);
            bool pingUp = f.PingAfter >= f.PingBefore * 2 && f.PingAfter - f.PingBefore >= 10, dropsUp = f.DropsAfter >= f.DropsBefore + 2;
            bool someUp = (f.PingAfter >= f.PingBefore * 1.4 && f.PingAfter - f.PingBefore >= 6) || f.DropsAfter >= f.DropsBefore + 1;
            // "stayed": the newest day with data is still as bad - a one-day blip isn't a broken update
            var last = Enumerable.Range(0, 3).Select(i => day(DateTime.Today.AddDays(-i))).FirstOrDefault(s => !double.IsNaN(s.Ping));
            f.Stayed = last != null && last.D > d0 && ((pingUp && last.Ping >= f.PingBefore * 1.6) || (dropsUp && last.Drops >= f.DropsBefore + 1));
            f.Strong = (pingUp || dropsUp) && after.Count >= 2;
            f.Lv = f.Strong ? 2 : pingUp || dropsUp || someUp ? 1 : 0;
            Func<double, string> ms = v => v.ToString("0", CultureInfo.CurrentCulture) + " ms"; Func<double, string> dr = v => v.ToString("0.#", CultureInfo.CurrentCulture);
            string nums = "ping " + ms(f.PingBefore) + " → " + ms(f.PingAfter) + " · drops a day " + dr(f.DropsBefore) + " → " + dr(f.DropsAfter);
            f.Text = f.Lv == 0 ? "no change after it - " + nums
                   : (f.Strong ? "things got clearly worse starting the day after - " : "a little worse after it - ") + nums + (f.Stayed ? " - and it's still like that" : f.Strong ? " - since then it has settled" : "");
            return f;
        }

        public static List<string[]> Run()
        {
            var rows = new List<string[]>();
            var ch = Changes(14);
            if (!Cfg.History)
            {
                rows.Add(R(1, "History is off", "AdeeboMeter needs a few days of History to compare before and after - turn it on in Settings › History. Until then, here is only what was installed.", "", "i"));
                foreach (var c in ch.OrderByDescending(c => c.T).Take(20)) rows.Add(R(3, c.T.ToString("ddd d MMM", CultureInfo.CurrentCulture), c.What, "", "w"));
                return rows;
            }
            if (ch.Count == 0) { rows.Add(R(0, "Verdict", "nothing was installed in the last 14 days - if things got worse, it wasn't Windows Update", "", "w")); return rows; }
            // one finding per day (several updates often land together)
            var byDay = ch.GroupBy(c => c.T.Date).Select(gr => new Change { T = gr.First().T, What = string.Join(", ", gr.Select(c => c.What).Distinct().Take(3)) + (gr.Count() > 3 ? " and " + (gr.Count() - 3) + " more" : ""), Kind = gr.Any(c => c.Kind == "driver") ? "driver" : "update" }).OrderByDescending(c => c.T).ToList();
            var fs = byDay.Select(c => Compare(c, Day)).ToList();
            var worst = fs.Where(f => f.Lv >= 1).OrderByDescending(f => f.Lv).ThenByDescending(f => f.Stayed).ThenByDescending(f => f.PingAfter - f.PingBefore).FirstOrDefault();
            if (worst == null) rows.Add(R(0, "Verdict", "none of the " + byDay.Count + " install days changed your connection - ping and drops stayed the same before and after", "Compared with AdeeboMeter's History: three days before each install against three days after.", "w"));
            else rows.Add(R(worst.Lv, "Verdict", (worst.Strong ? "Things got worse starting the day after " : "A small change after ") + worst.C.What + " (" + worst.C.T.ToString("ddd d MMM", CultureInfo.CurrentCulture) + "): " + worst.Text, "It started then - that's not proof it's the cause (your provider can have a bad week too). To test it: " + (worst.C.Kind == "driver" ? "Device Manager › the device › Driver › Roll Back Driver." : "Settings › Windows Update › Update history › Uninstall updates.") + " If it gets better, that was it.", "w"));
            foreach (var f in fs) rows.Add(R(f.Lv, f.C.T.ToString("ddd d MMM", CultureInfo.CurrentCulture) + " · " + (f.C.Kind == "driver" ? "driver" : "update"), f.C.What + " - " + f.Text, "", "w"));
            rows.Add(R(3, "How", "Typical ping to the internet and outages per day from History, three days before each install against three days after. The install day itself is left out.", "", "i"));
            return rows;
        }
        // in the background, at most once an hour: the install days for the week heatmap, and a line for the Overview when a match is strong and still there
        static DateTime lastBg = DateTime.MinValue; static volatile bool bgBusy;
        public static void Background()
        {
            if (bgBusy || (DateTime.UtcNow - lastBg).TotalMinutes < 60) return; bgBusy = true; lastBg = DateTime.UtcNow;
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    var ch = Changes(14); ChangeDays = ch.Select(c => c.T.Date).Distinct().ToList();
                    string s = "";
                    if (Cfg.History)
                        foreach (var c in ch.GroupBy(x => x.T.Date).Select(gr => gr.First()).OrderByDescending(x => x.T))
                        { var f = Compare(c, Day); if (f.Strong && f.Stayed) { s = "Your connection got worse the day after " + c.What + " installed (" + c.T.ToString("ddd", CultureInfo.CurrentCulture) + ") and stayed that way"; break; } }
                    Strong = s;
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("update break: " + e.Message); }
                finally { bgBusy = false; }
            });
        }
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
    }
}
