// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // 1.1.5 "your network over time": the month and six months on the network, Wi-Fi rush hour, the wake-up report,
    // the data-cap forecast and the download-time predictor - all read from History and Usage, nothing new is measured.
    // Plus the network-settings snapshot, the IPv6 score, the VPN leak suite, paired Bluetooth and the duplicate-IP watch.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "result rows and small lists, built once per run")]
    static class OverTime
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string N0(double v) { return double.IsNaN(v) ? "-" : Math.Round(v).ToString("N0", CultureInfo.CurrentCulture); }
        static string N1(double v) { return double.IsNaN(v) ? "-" : v.ToString("0.#", CultureInfo.CurrentCulture); }
        static double Avg(IEnumerable<double> v) { var a = v.Where(x => !double.IsNaN(x)).ToList(); return a.Count == 0 ? double.NaN : a.Average(); }
        static readonly string[] DayNames = CultureInfo.CurrentCulture.DateTimeFormat.DayNames;
        static List<string[]> NoHistory() { return new List<string[]> { R(1, "History", Cfg.History ? "nothing recorded yet" : "History is off", "Turn on History in Settings - AdeeboMeter writes one line a minute on this PC, and these reviews are made from it.", "u") }; }

        // ---------- one period summed up: the numbers the month and six-month reviews share
        public sealed class Period
        {
            public DateTime From, To; public int Days, Minutes, BadMinutes, OutageCount; public double OutageMin, GwAvg, NetAvg, LossAvg, GBDown, GBUp, PeakDown;
            public DateTime BestDay, WorstDay; public double BestScore = double.NaN, WorstScore = double.NaN;
            public int RushDow = -1, RushHour = -1; public double RushGw = double.NaN; public string Laggiest = ""; public double LaggiestMs = double.NaN; public List<string> Speeds = new List<string>();
        }
        public static Period Sum(Monitor m, DateTime from, DateTime to, Toolkit.Run run)
        {
            var p = new Period { From = from.Date, To = to.Date };
            var hourGw = new double[7, 24]; var hourN = new int[7, 24]; var gws = new List<double>(); var nets = new List<double>(); var loss = new List<double>();
            foreach (var d in History.Days().Where(d => d >= p.From && d <= p.To).OrderBy(d => d))
            {
                if (run != null) { if (run.Stop) break; run.Status = "Reading " + d.ToString("d MMM", CultureInfo.CurrentCulture) + "..."; }
                var day = History.Load(d); if (day.M.Count == 0) continue;
                p.Days++; p.Minutes += day.M.Count;
                int bad = day.M.Count(r => r[1] >= 2); p.BadMinutes += bad;
                double dg = Avg(day.M.Select(r => r[5])); double score = bad * 10 + (double.IsNaN(dg) ? 0 : dg);
                if (double.IsNaN(p.BestScore) || score < p.BestScore) { p.BestScore = score; p.BestDay = d; }
                if (double.IsNaN(p.WorstScore) || score > p.WorstScore) { p.WorstScore = score; p.WorstDay = d; }
                foreach (var r in day.M)
                {
                    if (!double.IsNaN(r[5])) { gws.Add(r[5]); int dw = (int)d.DayOfWeek, h = Math.Min(23, (int)(r[0] / 3600)); hourGw[dw, h] += r[5]; hourN[dw, h]++; }
                    if (!double.IsNaN(r[6])) nets.Add(r[6]); if (!double.IsNaN(r[11])) loss.Add(r[11]);
                    if (!double.IsNaN(r[13])) p.GBDown += r[13] / 1024; if (!double.IsNaN(r[14])) p.GBUp += r[14] / 1024;
                    if (!double.IsNaN(r[4]) && r[4] > p.PeakDown) p.PeakDown = r[4];
                }
                foreach (var e in day.E) if (e.Kind == "speed" && p.Speeds.Count < 40) p.Speeds.Add(e.T.ToString("d MMM HH:mm", CultureInfo.CurrentCulture) + " · " + e.Text);
            }
            p.GwAvg = Avg(gws); p.NetAvg = Avg(nets); p.LossAvg = Avg(loss);
            foreach (var o in Outages.Last(m, Math.Max(1, (int)(DateTime.Today - p.From).TotalDays + 1)).Where(o => o.From.Date >= p.From && o.From.Date <= p.To)) { p.OutageCount++; if (!o.EndUnseen) p.OutageMin += o.Len.TotalMinutes; }
            // rush hour: the slowest (weekday, hour) by router delay, among hours with at least 30 minutes of data
            for (int dw = 0; dw < 7; dw++) for (int h = 0; h < 24; h++) if (hourN[dw, h] >= 30) { double a = hourGw[dw, h] / hourN[dw, h]; if (double.IsNaN(p.RushGw) || a > p.RushGw) { p.RushGw = a; p.RushDow = dw; p.RushHour = h; } }
            // the laggiest game: highest average ping across its history rows in the period (game history is kept GameDays)
            foreach (var g in Games.All)
            {
                double sum = 0; int n = 0;   // a running average - never a list of every minute
                for (var d = p.From; d <= p.To && (run == null || !run.Stop); d = d.AddDays(1)) foreach (var x in GameHistory.Read(d, g.Id)) if (!double.IsNaN(x.Avg)) { sum += x.Avg; n++; }
                if (n < 30) continue; double a = sum / n;
                if (double.IsNaN(p.LaggiestMs) || a > p.LaggiestMs) { p.LaggiestMs = a; p.Laggiest = g.Name; }
            }
            return p;
        }
        static void PeriodRows(Period p, List<string[]> rows)
        {
            var c = CultureInfo.CurrentCulture; double online = 100.0 * (p.Minutes - p.BadMinutes) / Math.Max(1, p.Minutes);
            rows.Add(R(online >= 99 ? 0 : online >= 95 ? 1 : 2, "Smooth", online.ToString("0.#", c) + "% of the minutes measured (" + N0(p.Minutes / 60.0) + " hours over " + p.Days + " days)", "A minute counts as rough when AdeeboMeter's verdict was a problem.", "m"));
            rows.Add(R(p.OutageCount == 0 ? 0 : 1, "Outages", p.OutageCount == 0 ? "none" : p.OutageCount + " · " + N0(p.OutageMin) + " minutes without internet", "The internet check got no answer at all.", "m"));
            rows.Add(R(3, "Usual delay", "router " + N1(p.GwAvg) + " ms · internet " + N1(p.NetAvg) + " ms · loss " + N1(p.LossAvg) + "%", "", "m"));
            rows.Add(R(3, "Data", N1(p.GBDown) + " GB down · " + N1(p.GBUp) + " GB up", "Counted from Windows' own counters, all networks.", "w"));
            if (p.PeakDown > 0) rows.Add(R(3, "Fastest minute", N1(p.PeakDown) + " Mbps on average down", "", "m"));
            if (!double.IsNaN(p.BestScore)) rows.Add(R(0, "Best day", p.BestDay.ToString("dddd d MMMM", c), "", "m"));
            if (!double.IsNaN(p.WorstScore) && p.WorstDay != p.BestDay) rows.Add(R(1, "Worst day", p.WorstDay.ToString("dddd d MMMM", c), "Open it in the Time machine to see what happened.", "m"));
            if (p.RushHour >= 0) rows.Add(R(3, "Rush hour", DayNames[p.RushDow] + "s " + p.RushHour.ToString("00", c) + ":00-" + ((p.RushHour + 1) % 24).ToString("00", c) + ":00 · router " + N1(p.RushGw) + " ms", "The slowest hour of the week on your own network.", "m"));
            if (p.Laggiest != "") rows.Add(R(3, "Laggiest game", p.Laggiest + " · " + N0(p.LaggiestMs) + " ms on average", "From the Gamer Zone's history (kept " + Cfg.GameDays + " days).", "m"));
            double waits = p.OutageMin + p.BadMinutes; if (waits > 0) rows.Add(R(3, "Time lost waiting", N0(waits / 60.0) + " hours of outages and rough minutes", "", "i"));
        }

        // ---------- your month on the network ("2026-10", "october", empty = last month, "this" = this month so far)
        public static List<string[]> Month(Monitor m, string which, Toolkit.Run run)
        {
            var rows = new List<string[]>(); if (!Cfg.History || History.Days().Count == 0) return NoHistory();
            DateTime first; string w = (which ?? "").Trim().ToLowerInvariant();
            var t = DateTime.Today; var thisMonth = new DateTime(t.Year, t.Month, 1);
            if (w == "" || w == "last") first = thisMonth.AddMonths(-1);
            else if (w == "this" || w == "now") first = thisMonth;
            else
            {
                DateTime d; int mi = Array.FindIndex(CultureInfo.CurrentCulture.DateTimeFormat.MonthNames, n => n != "" && n.ToLowerInvariant().StartsWith(w, StringComparison.Ordinal) && w.Length >= 3);
                if (mi < 0) mi = Array.FindIndex(CultureInfo.InvariantCulture.DateTimeFormat.MonthNames, n => n != "" && n.ToLowerInvariant().StartsWith(w, StringComparison.Ordinal) && w.Length >= 3);
                if (mi >= 0) { first = new DateTime(t.Year, mi + 1, 1); if (first > thisMonth) first = first.AddYears(-1); }
                else if (DateTime.TryParseExact(w, new[] { "yyyy-MM", "yyyy-M", "MM/yyyy", "M/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) first = d;
                else { rows.Add(R(2, "Month", "type a month - e.g. october, 2026-10, this or last", "", "u")); return rows; }
            }
            var last = first.AddMonths(1).AddDays(-1); if (last > t) last = t;
            var p = Sum(m, first, last, run);
            rows.Add(R(-1, first.ToString("MMMM yyyy", CultureInfo.CurrentCulture).ToUpper(CultureInfo.CurrentCulture) + " ON YOUR NETWORK", "", "", "m"));
            if (p.Days == 0) { rows.Add(R(1, "History", "nothing recorded in " + first.ToString("MMMM", CultureInfo.CurrentCulture), "History keeps " + Cfg.HistoryDays + " days (Settings).", "u")); return rows; }
            PeriodRows(p, rows);
            var prev = Sum(m, first.AddMonths(-1), first.AddDays(-1), run);
            if (prev.Days >= 7 && !double.IsNaN(prev.GwAvg) && !double.IsNaN(p.GwAvg))
            {
                double now = 100.0 * p.BadMinutes / Math.Max(1, p.Minutes), was = 100.0 * prev.BadMinutes / Math.Max(1, prev.Minutes);
                rows.Add(R(now <= was ? 0 : 1, "Compared with " + first.AddMonths(-1).ToString("MMMM", CultureInfo.CurrentCulture), (now <= was ? "smoother" : "rougher") + " - " + N1(now) + "% rough minutes (was " + N1(was) + "%), " + N0(p.OutageMin) + " outage minutes (was " + N0(prev.OutageMin) + ")", "", "m"));
            }
            return rows;
        }

        // ---------- six months on the network: a review to keep or share (an HTML file), made from History before it goes
        public static List<string[]> SixMonths(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>(); if (!Cfg.History || History.Days().Count == 0) return NoHistory();
            var days = History.Days(); var from = days.Min(); var to = DateTime.Today; if ((to - from).TotalDays > 183) from = to.AddDays(-182);
            var p = Sum(m, from, to, run);
            rows.Add(R(-1, "SIX MONTHS ON YOUR NETWORK", from.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + " - " + to.ToString("d MMM yyyy", CultureInfo.CurrentCulture), "", "m"));
            if ((to - from).TotalDays < 150) rows.Add(R(3, "So far", p.Days + " days recorded - a full review needs about six months. Keep history for 180 days (Settings) to get there.", "", "i"));
            PeriodRows(p, rows);
            // month by month: one line each
            rows.Add(R(-1, "MONTH BY MONTH", "", "", "m"));
            for (var mo = new DateTime(from.Year, from.Month, 1); mo <= to; mo = mo.AddMonths(1))
            {
                if (run != null && run.Stop) break; var mp = Sum(m, mo < from ? from : mo, mo.AddMonths(1).AddDays(-1) > to ? to : mo.AddMonths(1).AddDays(-1), run); if (mp.Days == 0) continue;
                double rough = 100.0 * mp.BadMinutes / Math.Max(1, mp.Minutes);
                rows.Add(R(rough < 1 ? 0 : rough < 5 ? 1 : 2, mo.ToString("MMMM", CultureInfo.CurrentCulture), N1(100 - rough) + "% smooth · " + mp.OutageCount + " outages · router " + N1(mp.GwAvg) + " ms · " + N1(mp.GBDown) + " GB down", "", "m"));
            }
            string file = SaveReview(rows, "Six-months", from, to);
            if (file != null) rows.Add(new[] { "0", "Saved to share", Path.GetFileName(file) + " - open it in your browser; Print > Save as PDF to send it. Names and addresses are left out.", "", "m", "show:" + file });
            rows.Add(R(3, "Then", "the raw minutes roll off by themselves after the History setting (180 days at most); the review file stays until you delete it", "", "i"));
            return rows;
        }
        // History's rolling delete calls this first: when six months are about to roll off, the review is written once
        public static void BeforeRollOff()
        {
            try
            {
                if (Cfg.HistoryDays < 180 || !Cfg.History) return;
                string dir = Path.Combine(Program.DataDir, "reports");
                var days = History.Days(); if (days.Count == 0 || (DateTime.Today - days.Min()).TotalDays < 175) return;   // the span, not the count: a PC that's often off still gets its review
                // only an automatic review (or a manual one that already covered ~5 months) counts as done
                if (Directory.Exists(dir) && Directory.GetFiles(dir, "Six-months-*.html").Any(f => (DateTime.Now - File.GetLastWriteTime(f)).TotalDays < 170 && Covered(f) >= 150)) return;
                SixMonths(null, null);
                Program.Log("six-month review written before history rolls off");
                Alerts.Raise("review", "Your six months on the network are ready to look at and share: Toolbox > Six months on your network. The oldest days now roll off as usual.", 60 * 24);
            }
            catch (Exception e) { Program.Log("six months: " + e.Message); }
        }
        static int Covered(string file)   // Six-months-yyyy-MM-to-yyyy-MM.html: the months it spans, in days
        {
            var mm = Regex.Match(Path.GetFileName(file), @"([0-9]{4})-([0-9]{2})-to-([0-9]{4})-([0-9]{2})"); if (!mm.Success) return 0;
            int a = int.Parse(mm.Groups[1].Value, CultureInfo.InvariantCulture) * 12 + int.Parse(mm.Groups[2].Value, CultureInfo.InvariantCulture), b = int.Parse(mm.Groups[3].Value, CultureInfo.InvariantCulture) * 12 + int.Parse(mm.Groups[4].Value, CultureInfo.InvariantCulture);
            return (b - a) * 30;
        }
        static string SaveReview(List<string[]> rows, string kind, DateTime from, DateTime to)
        {
            try
            {
                string dir = Path.Combine(Program.DataDir, "reports"); Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, kind + "-" + from.ToString("yyyy-MM", CultureInfo.InvariantCulture) + "-to-" + to.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".html");
                Func<string, string> E = z => WebUtility.HtmlEncode(GameSession.Redact(z ?? "", ""));
                var sb = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Six months on my network</title><style>body{font:15px/1.5 'Segoe UI',sans-serif;color:#1B2330;max-width:760px;margin:24px auto;padding:0 16px}h1{font-size:26px;margin:0}h2{font-size:13px;letter-spacing:.06em;color:#5B6675;margin:26px 0 6px}.m{color:#5B6675}table{border-collapse:collapse;width:100%}td{padding:7px 8px;border-bottom:1px solid #E3E8EF;vertical-align:top}td.n{font-weight:600;width:34%}.l0{color:#1A7F37}.l1{color:#9A6700}.l2{color:#CF222E}</style></head><body>");
                sb.Append("<h1>Six months on my network</h1><div class=\"m\">" + E(from.ToString("d MMMM yyyy", CultureInfo.CurrentCulture) + " - " + to.ToString("d MMMM yyyy", CultureInfo.CurrentCulture)) + " · measured on my PC by AdeeboMeter (free, offline)</div><table>");
                foreach (var r in rows)
                {
                    if (r[0] == "-1") { sb.Append("</table><h2>" + E(r[1]) + "</h2><table>"); continue; }
                    if (r.Length > 5 && r[5] != "") continue;
                    sb.Append("<tr><td class=\"n l" + E(r[0]) + "\">" + E(r[1]) + "</td><td>" + E(r[2]) + (r[3] != "" ? "<div class=\"m\">" + E(r[3]) + "</div>" : "") + "</td></tr>");
                }
                sb.Append("</table></body></html>");
                string tmp = file + ".tmp"; File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false)); Program.ReplaceFile(tmp, file);
                return file;
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("review save: " + e.Message); return null; }
        }

        // ---------- Wi-Fi rush hour: the slowest hour of each weekday, last 4 weeks
        public static List<string[]> RushHour(Toolkit.Run run)
        {
            var rows = new List<string[]>(); if (!Cfg.History || History.Days().Count == 0) return NoHistory();
            var sum = new double[7, 24]; var n = new int[7, 24]; var bad = new int[7, 24];
            foreach (var d in History.Days().Where(d => d >= DateTime.Today.AddDays(-27)))
            {
                if (run != null && run.Stop) break; if (run != null) run.Status = "Reading " + d.ToString("d MMM", CultureInfo.CurrentCulture) + "...";
                foreach (var r in History.Load(d).M) { int h = Math.Min(23, (int)(r[0] / 3600)), dw = (int)d.DayOfWeek; if (!double.IsNaN(r[5])) { sum[dw, h] += r[5]; n[dw, h]++; } if (r[1] >= 1) bad[dw, h]++; }
            }
            var c = CultureInfo.CurrentCulture; int found = 0;
            double all = 0; int alln = 0; for (int a = 0; a < 7; a++) for (int b = 0; b < 24; b++) { all += sum[a, b]; alln += n[a, b]; }
            double usual = alln > 0 ? all / alln : double.NaN;
            rows.Add(R(-1, "THE SLOWEST HOUR OF EACH DAY", "router delay, last 4 weeks", "", "m"));
            for (int k = 0; k < 7; k++)
            {
                int dw = ((int)c.DateTimeFormat.FirstDayOfWeek + k) % 7, best = -1; double worst = double.NaN;
                for (int h = 0; h < 24; h++) if (n[dw, h] >= 20) { double a = sum[dw, h] / n[dw, h]; if (double.IsNaN(worst) || a > worst) { worst = a; best = h; } }
                if (best < 0) continue; found++;
                bool slow = !double.IsNaN(usual) && worst > usual * 2 && worst > usual + 5;
                rows.Add(R(slow ? 1 : 3, DayNames[dw], best.ToString("00", c) + ":00-" + ((best + 1) % 24).ToString("00", c) + ":00 · " + N1(worst) + " ms" + (bad[dw, best] > 0 ? " · " + bad[dw, best] + " rough minutes" : ""), slow ? (worst / usual).ToString("0.#", c) + "× the usual - your network's rush hour." : "", "m"));
            }
            if (found == 0) rows.Add(R(1, "Not enough yet", "each hour needs 20 minutes of data - leave AdeeboMeter running a few days", "", "u"));
            else rows.Insert(1, R(3, "Usual", "router " + N1(usual) + " ms", "Rush hour is usually when everyone at home - or in the building - is streaming. Big downloads and updates go best outside it.", "m"));
            return rows;
        }

        // ---------- the wake-up report: how long after waking the PC was online again (Monitor logs "wake" events)
        public static List<string[]> WakeUp(Monitor m)
        {
            var rows = new List<string[]>(); var evs = new List<Timeline.Ev>();
            if (Cfg.History) foreach (var d in History.Days().Where(d => d >= DateTime.Today.AddDays(-29))) evs.AddRange(History.EventsOf(d).Where(e => e.Kind == "wake"));
            foreach (var e in m.Events.Recent(300)) if (e.Kind == "wake" && !evs.Any(x => x.T == e.T && x.Text == e.Text)) evs.Add(e);
            var secs = new List<Tuple<DateTime, double, double>>();   // when, seconds to the router, seconds to DNS (NaN: DNS never answered)
            int failed = 0;   // woke, and the router never answered in 2 minutes
            foreach (var e in evs)
            {
                var mm = Regex.Match(e.Text, @"router ([0-9]+(?:\.[0-9]+)?) s(?:.*?DNS ([0-9]+(?:\.[0-9]+)?) s)?", RegexOptions.CultureInvariant); if (!mm.Success) { if (e.Text.IndexOf("didn't answer within 2 minutes", StringComparison.Ordinal) >= 0) failed++; continue; }
                double r = double.Parse(mm.Groups[1].Value, CultureInfo.InvariantCulture), dn = mm.Groups[2].Success ? double.Parse(mm.Groups[2].Value, CultureInfo.InvariantCulture) : double.NaN;
                secs.Add(Tuple.Create(e.T, r, dn));
            }
            if (secs.Count == 0 && failed > 0) { rows.Add(R(2, "After waking", failed + " wake-up" + (failed == 1 ? "" : "s") + " - the router never answered within 2 minutes", "The Wi-Fi didn't come back by itself: the adapter's power saving, or a driver that doesn't resume well. Device Manager > the Wi-Fi adapter > Power Management.", "m")); return rows; }
            if (secs.Count == 0) { rows.Add(R(3, "No wake-ups yet", "the next time the PC wakes from sleep, AdeeboMeter times how long until the router and DNS answer", Cfg.History ? "" : "Turn on History to keep the times for a month.", "u")); return rows; }
            var c = CultureInfo.CurrentCulture; double avg = secs.Average(s => s.Item2), worst = secs.Max(s => s.Item2);
            rows.Add(R(avg <= 10 ? 0 : avg <= 30 ? 1 : 2, "Back online after waking", N1(avg) + " s on average · slowest " + N1(worst) + " s · " + secs.Count + " wake-ups", avg <= 10 ? "Quick." : "Slow wake-ups are usually the Wi-Fi adapter's power saving or a long DHCP renew. Device Manager > the Wi-Fi adapter > Power Management, or a cable, often fixes it.", "m"));
            double dns = Avg(secs.Select(s => s.Item3)); if (!double.IsNaN(dns)) rows.Add(R(3, "DNS answered", N1(dns) + " s after waking, on average", "", "m"));
            int noDns = secs.Count(s => double.IsNaN(s.Item3)); if (noDns > 0) rows.Add(R(1, "DNS never answered", noDns + " time" + (noDns == 1 ? "" : "s") + " the router came back but DNS didn't within 2 minutes", "", "m"));
            if (failed > 0) rows.Add(R(2, "Never came back", failed + " wake-up" + (failed == 1 ? "" : "s") + " - the router didn't answer within 2 minutes", "Not in the average above.", "m"));
            rows.Add(R(-1, "BY DAY", "", "", "m"));
            foreach (var g in secs.GroupBy(s => s.Item1.Date).OrderByDescending(g => g.Key).Take(14))
                rows.Add(R(g.Max(s => s.Item2) > 30 ? 1 : 3, g.Key.ToString("ddd d MMM", c), g.Count() + " wake-ups · " + N1(g.Average(s => s.Item2)) + " s on average · slowest " + N1(g.Max(s => s.Item2)) + " s", "", "m"));
            return rows;
        }

        // ---------- the data-cap forecast: this month so far, at this pace
        public static List<string[]> CapForecast()
        {
            var rows = new List<string[]>(); var t = DateTime.Today; int dim = DateTime.DaysInMonth(t.Year, t.Month);
            double gb = Usage.Month(null) / 1024, perDay = gb / Math.Max(1, t.Day), atEnd = perDay * dim;
            rows.Add(R(3, "This month so far", N1(gb) + " GB down + up in " + t.Day + " days · " + N1(perDay) + " GB a day", "Counted on this PC from Windows' own counters - other devices on the plan aren't included.", "w"));
            if (Cfg.CapGB <= 0) { rows.Add(R(3, "At this pace", N1(atEnd) + " GB by the end of the month", "Set your plan's cap in Settings > Alerts to get a date.", "i")); return rows; }
            if (gb >= Cfg.CapGB) { rows.Add(R(2, "Cap", "already over: " + N1(gb) + " of " + Cfg.CapGB + " GB", "", "w")); return rows; }
            if (t.Day < 3) { rows.Add(R(3, "At this pace", "too early to tell - a forecast needs about three days of this month", "", "i")); return rows; }
            double daysLeft = perDay > 0 ? (Cfg.CapGB - gb) / perDay : double.PositiveInfinity;
            if (t.Day + daysLeft > dim) rows.Add(R(0, "At this pace", "you stay under the cap: about " + N0(atEnd) + " of " + Cfg.CapGB + " GB by the " + dim + Th(dim), "", "i"));
            else
            {
                int hit = t.Day + (int)Math.Ceiling(daysLeft);
                rows.Add(R(atEnd > Cfg.CapGB * 1.2 ? 2 : 1, "At this pace", "you reach " + Cfg.CapGB + " GB on the " + hit + Th(hit) + " - " + (dim - hit) + " days early", "To stay under: " + N1((Cfg.CapGB - gb) / Math.Max(1, dim - t.Day + 1)) + " GB a day from now. Game updates and 4K video are the usual big ones.", "i"));
            }
            return rows;
        }
        static string Th(int d) { return d % 10 == 1 && d != 11 ? "st" : d % 10 == 2 && d != 12 ? "nd" : d % 10 == 3 && d != 13 ? "rd" : "th"; }

        // ---------- download time at your real speeds: speed tests you ran, and the fastest minutes History saw, by hour
        public static List<string[]> DownloadTime(string sizeText, Toolkit.Run run)
        {
            var rows = new List<string[]>(); double gb = ParseGB(sizeText);
            if (double.IsNaN(gb) || gb <= 0) { rows.Add(R(2, "Size", "type a size - e.g. 50 GB or 800 MB", "", "u")); return rows; }
            var tests = new List<Tuple<DateTime, double>>(); var hourPeak = new double[24];
            if (Cfg.History)
                foreach (var d in History.Days().Where(d => d >= DateTime.Today.AddDays(-29)))
                {
                    if (run != null && run.Stop) break; if (run != null) run.Status = "Reading " + d.ToString("d MMM", CultureInfo.CurrentCulture) + "...";
                    var day = History.Load(d);
                    foreach (var e in day.E.Where(e => e.Kind == "speed")) { var mm = Regex.Match(e.Text, @":\s*([\d.,]+)\s*down"); double v; if (mm.Success && double.TryParse(mm.Groups[1].Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0) tests.Add(Tuple.Create(e.T, v)); }
                    foreach (var r in day.M) if (!double.IsNaN(r[4])) { int h = Math.Min(23, (int)(r[0] / 3600)); if (r[4] > hourPeak[h]) hourPeak[h] = r[4]; }
                }
            if (SpeedTest.Last != null && !double.IsNaN(SpeedTest.Last.Down) && !tests.Any(x => Math.Abs((x.Item1 - SpeedTest.Last.T).TotalMinutes) < 2)) tests.Add(Tuple.Create(SpeedTest.Last.T, SpeedTest.Last.Down));
            Func<double, string> dur = mbps => { double s = gb * 8000 / mbps / 0.94; return s < 3600 ? Math.Ceiling(s / 60) + " min" : Math.Floor(s / 3600) + " h " + Math.Round(s % 3600 / 60) + " min"; };
            var c = CultureInfo.CurrentCulture;
            if (tests.Count > 0)
            {
                var med = tests.Select(x => x.Item2).OrderBy(x => x).ElementAt(tests.Count / 2);
                rows.Add(R(0, "At your measured speed", dur(med) + " at " + N0(med) + " Mbps", "The middle of " + tests.Count + " speed test" + (tests.Count == 1 ? "" : "s") + " you ran in the last 30 days.", "m"));
                var ev = tests.Where(x => x.Item1.Hour >= 18 && x.Item1.Hour <= 23).Select(x => x.Item2).ToList();
                if (ev.Count > 0) rows.Add(R(3, "In the evening", dur(ev.Average()) + " at " + N0(ev.Average()) + " Mbps", "Speed tests run between 18:00 and midnight.", "m"));
            }
            double peak = hourPeak.Max();
            if (peak > 1)
            {
                int bh = Array.IndexOf(hourPeak, peak);
                rows.Add(R(3, "Fastest hour seen", bh.ToString("00", c) + ":00 - your downloads reached " + N0(peak) + " Mbps then: " + dur(peak), "From History's minute averages - what your downloads actually got, not a test. Real capacity can be higher.", "m"));
                var ev2 = Enumerable.Range(19, 4).Select(h => hourPeak[h]).Where(v => v > 1).ToList(); if (ev2.Count > 0) { double e2 = ev2.Max(); rows.Add(R(3, "Evening (19-23)", N0(e2) + " Mbps seen: " + dur(e2), "", "m")); }
            }
            if (rows.Count == 0) rows.Add(R(1, "No speeds yet", "run a speed test (Toolbox > Speed test) or leave History on for a few days", "Until then: " + dur(100) + " at 100 Mbps, " + dur(500) + " at 500 Mbps.", "u"));
            return rows;
        }
        static double ParseGB(string s)
        {
            var mm = Regex.Match((s ?? "").Trim(), @"^([\d.,]+)\s*(tb|gb|mb|g|m|t)?b?$", RegexOptions.IgnoreCase); if (!mm.Success) return double.NaN;
            string num = mm.Groups[1].Value; num = Regex.IsMatch(num, @"^[0-9]{1,3}(,[0-9]{3})+(\.[0-9]+)?$") ? num.Replace(",", "") : num.Replace(',', '.');   // 1,000 is a thousand; 1,5 is one and a half
            double v; if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return double.NaN;
            string u = mm.Groups[2].Value.ToLowerInvariant(); return u.StartsWith("t", StringComparison.Ordinal) ? v * 1000 : u.StartsWith("m", StringComparison.Ordinal) ? v / 1000 : v;
        }

        // ---------- network-settings snapshot: save now, compare later (DNS, proxy, hosts, each adapter's address and router)
        static string SnapFile { get { return Path.Combine(Program.DataDir, "netsnap.txt"); } }
        public static SortedDictionary<string, string> ReadSettings()
        {
            var s = new SortedDictionary<string, string>(StringComparer.Ordinal);
            try
            {
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                {
                    var p = n.GetIPProperties(); string k = "adapter " + n.Name;
                    s[k + " · address"] = string.Join(", ", p.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(a => a.Address + "/" + a.PrefixLength));
                    var v6 = p.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6 && !a.Address.IsIPv6LinkLocal).Select(a => a.Address.ToString()).ToList(); if (v6.Count > 0) s[k + " · IPv6"] = string.Join(", ", v6.Take(4));
                    s[k + " · router"] = string.Join(", ", p.GatewayAddresses.Select(g => g.Address.ToString()).Where(g => g != "0.0.0.0"));
                    s[k + " · DNS servers"] = string.Join(", ", p.DnsAddresses.Select(d => d.ToString()));
                    try { s[k + " · DHCP"] = p.GetIPv4Properties() != null && p.GetIPv4Properties().IsDhcpEnabled ? "on" : "off (fixed address)"; } catch (NetworkInformationException) { }
                    if (p.DnsSuffix != "") s[k + " · DNS suffix"] = p.DnsSuffix;
                }
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                    if (key != null)
                    {
                        object en = key.GetValue("ProxyEnable"), sv = key.GetValue("ProxyServer"), pac = key.GetValue("AutoConfigURL");
                        s["proxy"] = en is int && (int)en == 1 ? (sv as string ?? "on") : "off";
                        s["proxy script (PAC)"] = pac as string ?? "none";
                    }
                string hosts = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");
                if (File.Exists(hosts))
                {
                    var lines = File.ReadLines(hosts).Take(5000).Select(l => l.Trim()).Where(l => l != "" && !l.StartsWith("#", StringComparison.Ordinal)).ToList();
                    s["hosts file"] = lines.Count + " active lines" + (lines.Count > 0 ? " · " + Fp(string.Join("\n", lines)) : "");
                }
            }
            catch (Exception e) { if (!(e is NetworkInformationException || e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is NotImplementedException || e is PlatformNotSupportedException)) throw; }   /* a runtime without every adapter property (mono) still gets the rest */
            return s;
        }
        static string Fp(string s) { using (var h = System.Security.Cryptography.SHA256.Create()) return "fingerprint " + BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(s)), 0, 4).Replace("-", "").ToLowerInvariant(); }
        public static List<string[]> Snapshot(string action)
        {
            var rows = new List<string[]>(); var now = ReadSettings(); string a = (action ?? "").Trim().ToLowerInvariant();
            if (a == "save" || !File.Exists(SnapFile))
            {
                try { File.WriteAllLines(SnapFile, new[] { "# AdeeboMeter network-settings snapshot " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) }.Concat(now.Select(kv => kv.Key.Replace("=", " ") + "=" + kv.Value))); }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; rows.Add(R(2, "Snapshot", "couldn't save: " + e.Message, "", "u")); return rows; }
                rows.Add(R(0, "Snapshot saved", now.Count + " settings, " + DateTime.Now.ToString("d MMM HH:mm", CultureInfo.CurrentCulture), "Run it again later (before and after a trip, a VPN or a new program) with the box empty to see what changed.", "w"));
                foreach (var kv in now) rows.Add(R(3, kv.Key, kv.Value == "" ? "-" : kv.Value, "", "w"));
                return rows;
            }
            var old = new SortedDictionary<string, string>(StringComparer.Ordinal); string when = "";
            foreach (var l in File.ReadLines(SnapFile).Take(500)) { if (l.StartsWith("#", StringComparison.Ordinal)) { if (l.Length > 20) when = l.Substring(l.Length - 16); continue; } int eq = l.IndexOf('='); if (eq > 0) old[l.Substring(0, eq)] = l.Substring(eq + 1); }
            var changed = now.Keys.Union(old.Keys).Where(k => { string x, y; now.TryGetValue(k, out x); old.TryGetValue(k, out y); return (x ?? "") != (y ?? ""); }).ToList();
            rows.Add(R(changed.Count == 0 ? 0 : 1, "Since the snapshot (" + when + ")", changed.Count == 0 ? "nothing changed" : changed.Count + " settings changed", changed.Count == 0 ? "" : "Type save to make this the new snapshot.", "w"));
            foreach (var k in changed)
            {
                string x, y; now.TryGetValue(k, out x); old.TryGetValue(k, out y);
                rows.Add(R(k.StartsWith("proxy", StringComparison.Ordinal) || k.StartsWith("hosts", StringComparison.Ordinal) || k.EndsWith("DNS servers", StringComparison.Ordinal) ? 1 : 3, k, (y ?? "(not there)") + "  →  " + (x ?? "(gone)"), Undo(k, y), "w"));
            }
            return rows;
        }
        // putting a setting back needs admin for DNS and hosts - so the way back is told, never done silently
        static string Undo(string k, string was)
        {
            if (k.EndsWith("DNS servers", StringComparison.Ordinal)) return "To put it back: Settings > Network > the adapter > DNS server assignment" + (string.IsNullOrEmpty(was) ? "" : " - " + was) + ". A VPN or a security app may have set it - check them first.";
            if (k.StartsWith("proxy", StringComparison.Ordinal)) return "To put it back: Settings > Network > Proxy. An unexpected proxy is a classic adware trick.";
            if (k.StartsWith("hosts", StringComparison.Ordinal)) return "Open " + Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts") + " in Notepad as administrator and look at the lines without a #.";
            return "";
        }

        // ---------- IPv6 readiness: one score from the pieces that are scattered elsewhere
        public static List<string[]> Ipv6Score(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>(); int score = 0;
            var ups = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel && !Monitor.IsVpn(n) && Props(n) != null).ToList();   // a VPN's IPv6 isn't the home network's
            var v6 = ups.SelectMany(n => n.GetIPProperties().UnicastAddresses).Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6 && !a.Address.IsIPv6LinkLocal && !a.Address.IsIPv6SiteLocal && (a.Address.GetAddressBytes()[0] & 0xE0) == 0x20).ToList();
            bool addr = v6.Count > 0; if (addr) score++;
            rows.Add(R(addr ? 0 : 2, "A public IPv6 address", addr ? v6.Count + " - e.g. " + Mask(v6[0].Address) : "none - this network doesn't hand out IPv6", addr ? "" : "Your router or provider has IPv6 off. Many providers offer it - look for IPv6 / DHCPv6-PD in the router's settings.", "w"));
            bool temp = v6.Any(a => { try { return a.SuffixOrigin == SuffixOrigin.Random; } catch (PlatformNotSupportedException) { return false; } });
            if (addr) rows.Add(R(temp ? 0 : 1, "Privacy addresses", temp ? "on - outgoing connections use a changing address" : "off - one fixed address for everything", temp ? "" : "Windows normally turns these on. A fixed address makes this PC easier to follow across sites.", "w"));
            if (addr && temp) score++;
            bool gw6 = ups.Any(n => n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetworkV6)); if (gw6) score++;
            rows.Add(R(gw6 ? 0 : (addr ? 1 : 3), "An IPv6 router", gw6 ? "yes - Windows has an IPv6 default route" : "no", "", "w"));
            bool dns6 = false;
            try { run.Status = "Asking your DNS server for an IPv6 address..."; dns6 = Dns.GetHostAddresses("www.google.com").Any(a => a.AddressFamily == AddressFamily.InterNetworkV6); } catch (SocketException) { }
            if (dns6) score++;
            rows.Add(R(dns6 ? 0 : 1, "DNS gives IPv6 answers", dns6 ? "yes" : "no - names come back IPv4-only", "Asked your own DNS server for www.google.com.", "m"));
            bool reach = false; string how = "";
            if (Policy.NoInternetCheck) how = "not tried: your IT team turned the internet check off";
            else if (gw6)
            {
                run.Status = "Pinging an IPv6 address on the internet...";
                try { using (var p = new Ping()) { var r = p.Send(IPAddress.Parse("2606:4700:4700::1111"), 2000); reach = r.Status == IPStatus.Success; how = reach ? r.RoundtripTime + " ms to Cloudflare's IPv6 DNS" : "no answer"; } }
                catch (PingException) { how = "no answer"; } catch (InvalidOperationException) { how = "no answer"; }
            }
            else how = "not tried - no IPv6 router";
            if (reach) score++;
            rows.Add(R(reach ? 0 : (gw6 ? 2 : 3), "Reaches the IPv6 internet", reach ? "yes · " + how : how, "", "m"));
            string verdict = score >= 5 ? "Ready" : score >= 3 ? "Half-ready" : score >= 1 ? "Barely" : "Off";
            rows.Insert(0, R(score >= 5 ? 0 : score >= 3 ? 1 : 3, "IPv6 readiness", verdict + " · " + score + " of 5", score >= 5 ? "Games, calls and apps can use IPv6 here - often a shorter path and no shared NAT." : score == 0 ? "IPv4 only. Nothing is wrong with that - but with IPv6, apps don't have to squeeze through your provider's shared NAT." : "Something is missing - the red line below is the one to fix first.", "m"));
            return rows;
        }
        // one adapter that can't answer (a driver mid-reset) mustn't sink the whole tool: it's left out
        static IPInterfaceProperties Props(NetworkInterface n) { try { return n.GetIPProperties(); } catch (NetworkInformationException) { return null; } catch (NotImplementedException) { return null; } }
        static string Mask(IPAddress a) { var s = a.ToString(); int k = s.IndexOf(':', s.IndexOf(':', s.IndexOf(':') + 1) + 1); return k > 0 ? s.Substring(0, k) + ":…" : s; }

        // ---------- VPN leak suite: does traffic leave outside the tunnel? Two STUN answers and the IPv6 path, one verdict
        public static IPEndPoint StunVia(IPAddress server, int port, IPAddress bindLocal)
        {
            var tid = new byte[12]; using (var g = System.Security.Cryptography.RandomNumberGenerator.Create()) g.GetBytes(tid);
            var req = new byte[20]; req[1] = 1; req[4] = 0x21; req[5] = 0x12; req[6] = 0xA4; req[7] = 0x42; Array.Copy(tid, 0, req, 8, 12);
            try
            {
                using (var u = new UdpClient(server.AddressFamily))
                {
                    if (bindLocal != null) u.Client.Bind(new IPEndPoint(bindLocal, 0));
                    u.Client.ReceiveTimeout = 1500; u.Connect(server, port);
                    for (int t = 0; t < 3; t++) { u.Send(req, req.Length); try { var ep = new IPEndPoint(server.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0); var r = u.Receive(ref ep); var mp = Reach.ParseStun(r, tid); if (mp != null) return mp; } catch (SocketException) { } }
                }
            }
            catch (SocketException) { } catch (ArgumentException) { }
            return null;
        }
        public static List<string[]> VpnLeak(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>();
            if (Policy.NoOnline || !Online.On) { rows.Add(R(1, "Online lookups", Policy.NoOnline ? "turned off by your IT team" : "off", "This check asks a public STUN server (Cloudflare) which address it sees - turn on Online lookups (top of the window) to run it.", "u")); return rows; }
            var all = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && Props(n) != null).ToList();
            var vpn = all.FirstOrDefault(Monitor.IsVpn);
            if (vpn == null) { rows.Add(R(3, "VPN", "none connected", "Connect your VPN first, then run this - it checks what still leaves outside the tunnel.", "w")); return rows; }
            var phys = all.Where(n => !Monitor.IsVpn(n) && n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))).ToList();
            IPAddress[] srv; try { run.Status = "Finding the STUN server..."; srv = Dns.GetHostAddresses("stun.cloudflare.com"); } catch (SocketException) { rows.Add(R(2, "STUN server", "couldn't find stun.cloudflare.com", "", "m")); return rows; }
            var s4 = srv.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork); var s6 = srv.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);
            rows.Add(R(3, "VPN", vpn.Description, "", "w"));
            int leaks = 0, unknown = 0;
            if (s4 != null)
            {
                run.Status = "Asking which address the internet sees...";
                var viaDefault = StunVia(s4, 3478, null);
                var physIp = phys.SelectMany(n => n.GetIPProperties().UnicastAddresses).Select(a => a.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                var viaPhys = physIp != null ? StunVia(s4, 3478, physIp) : null;
                rows.Add(R(3, "The internet sees you as", viaDefault == null ? "no answer" : viaDefault.Address.ToString(), "Through the normal route - this should be the VPN's exit.", "m"));
                if (viaDefault != null && viaPhys != null && viaDefault.Address.Equals(viaPhys.Address)) { leaks++; rows.Add(R(2, "IPv4 outside the tunnel", "your normal traffic leaves with your own provider's address (" + viaPhys.Address + ")", "The VPN is on but doesn't carry everything - a split tunnel, or the VPN's route lost. If you expect full protection, reconnect the VPN or turn on its kill switch.", "m")); }
                else if (viaDefault == null || viaPhys == null) { unknown++; rows.Add(R(1, "IPv4", "couldn't tell - " + (viaDefault == null ? "the STUN server didn't answer" : "no answer when asked through your Wi-Fi or cable directly"), "Not a clean bill - try again, or on another network.", "m")); }
                else if (viaDefault != null && viaPhys != null) rows.Add(R(0, "IPv4", "goes through the tunnel (" + viaPhys.Address + " is only used when an app picks the Wi-Fi or cable on purpose)", "", "m"));
            }
            bool v6Phys = phys.Any(n => n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetworkV6));
            bool v6Vpn = vpn.GetIPProperties().UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6 && !a.Address.IsIPv6LinkLocal);
            if (v6Phys && !v6Vpn && s6 != null)
            {
                run.Status = "Trying IPv6...";
                var six = StunVia(s6, 3478, null);
                if (six != null) { leaks++; rows.Add(R(2, "IPv6 outside the tunnel", "IPv6 traffic leaves directly as " + six.Address, "Your VPN carries only IPv4. Sites that prefer IPv6 see your real address. Turn on the VPN's IPv6 protection, or turn IPv6 off on this adapter while you use it.", "m")); }
                else rows.Add(R(0, "IPv6", "blocked while the VPN is up", "", "m"));
            }
            else if (v6Phys && !v6Vpn) { unknown++; rows.Add(R(1, "IPv6", "couldn't check - this network has IPv6 but no IPv6 STUN address was found", "", "m")); }
            else rows.Add(R(0, "IPv6", v6Vpn ? "the VPN carries IPv6 too" : "no IPv6 on this network - nothing to leak", "", "w"));
            var dnsPhys = phys.SelectMany(n => n.GetIPProperties().DnsAddresses).Where(a => !IPAddress.IsLoopback(a)).Distinct().ToList();
            var dnsVpn = vpn.GetIPProperties().DnsAddresses.ToList();
            if (dnsVpn.Count == 0 && dnsPhys.Count > 0) { leaks++; rows.Add(R(1, "DNS", "the VPN sets no DNS server - names are asked via " + string.Join(", ", dnsPhys.Take(2)), "Your provider can see every site name you visit. Most VPN apps have a \"use VPN DNS\" switch.", "w")); }
            else rows.Add(R(0, "DNS", dnsVpn.Count > 0 ? "the VPN sets its own DNS server (" + string.Join(", ", dnsVpn.Take(2)) + ")" : "-", "Security > DNS leak on VPN checks which one Windows actually uses.", "w"));
            rows.Insert(0, R(leaks > 0 ? 2 : unknown > 0 ? 1 : 0, "VPN leaks", leaks > 0 ? leaks + " found" : unknown > 0 ? "none found - but " + unknown + " check" + (unknown == 1 ? "" : "s") + " couldn't tell" : "none found", leaks == 0 && unknown == 0 ? "Your traffic, IPv6 and DNS stay inside the tunnel." : leaks == 0 ? "See the amber lines - those parts weren't checked." : "Details below - the first red line matters most.", "m"));
            return rows;
        }

        // ---------- your own Bluetooth devices (paired ones only - nobody else's are listed or kept)
        public static List<string[]> Bluetooth()
        {
            var rows = new List<string[]>();
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher("SELECT Name, Status, ConfigManagerErrorCode, Present, PNPClass, DeviceID FROM Win32_PnPEntity WHERE PNPClass = 'Bluetooth'"))
                {
                    s.Options.Timeout = TimeSpan.FromSeconds(10);
                    var list = new List<Bt>();   // the collection and each object are disposed: WMI handles aren't left to the finalizer
                    using (var col = s.Get()) foreach (System.Management.ManagementObject o in col) using (o) list.Add(new Bt { Name = (o["Name"] as string) ?? "", Id = (o["DeviceID"] as string) ?? "", Err = o["ConfigManagerErrorCode"] is uint ? (uint)o["ConfigManagerErrorCode"] : 0u, Present = o["Present"] is bool && (bool)o["Present"] });
                    var radio = list.Where(x => !x.Id.StartsWith("BTHENUM", StringComparison.OrdinalIgnoreCase) && !x.Id.StartsWith("BTHLE", StringComparison.OrdinalIgnoreCase) && x.Name.IndexOf("Enumerator", StringComparison.OrdinalIgnoreCase) < 0).ToList();
                    var devs = list.Where(x => (x.Id.StartsWith("BTHENUM\\DEV_", StringComparison.OrdinalIgnoreCase) || x.Id.StartsWith("BTHLE\\DEV_", StringComparison.OrdinalIgnoreCase)) && x.Name != "").GroupBy(x => x.Name + "|" + DevKey(x.Id)).Select(g => g.OrderByDescending(x => x.Present).First()).ToList();
                    if (radio.Count == 0 && devs.Count == 0) { rows.Add(R(3, "Bluetooth", "no Bluetooth radio found on this PC", "", "w")); return rows; }
                    foreach (var r in radio.Take(2)) rows.Add(R(r.Err == 0 ? 0 : 2, "Radio", r.Name + (r.Err == 0 ? "" : " · " + DevInfo.Problem((int)r.Err)[0]), r.Err == 0 ? "" : DevInfo.Problem((int)r.Err)[1], "w"));
                    rows.Add(R(-1, "PAIRED WITH THIS PC", Math.Min(40, devs.Count) + (devs.Count > 40 ? " of " + devs.Count : "") + " devices", "", "w"));
                    foreach (var d in devs.OrderByDescending(d => d.Present).ThenBy(d => d.Name).Take(40))
                        rows.Add(R(d.Err != 0 ? 2 : d.Present ? 0 : 3, d.Name, d.Err != 0 ? DevInfo.Problem((int)d.Err)[0] : d.Present ? "available" : "paired, not around right now", d.Err != 0 ? DevInfo.Problem((int)d.Err)[1] : "", "w"));
                    rows.Add(R(3, "Only yours", "devices paired with this PC - other people's phones and watches nearby are never listed or recorded", "", "i"));
                }
            }
            catch (Exception e) { if (!(e is System.Management.ManagementException || e is UnauthorizedAccessException || e is System.Runtime.InteropServices.COMException)) throw; rows.Add(R(1, "Bluetooth", "Windows didn't answer: " + e.Message, "", "u")); }
            return rows;
        }

        sealed class Bt { public string Name = "", Id = ""; public uint Err; public bool Present; }
        static string DevKey(string id) { var mm = Regex.Match(id ?? "", @"DEV_([0-9A-F]{12})", RegexOptions.IgnoreCase); return mm.Success ? mm.Groups[1].Value.ToUpperInvariant() : id; }   // two "AirPods" are two devices
        // ---------- another device using my address? Windows notices it (Tcpip 4199) - read live and for 30 days
        public static List<string[]> DuplicateIp(Monitor m)
        {
            var rows = new List<string[]>(); var hits = new List<Tuple<DateTime, string>>();
            try
            {
                var q = new System.Diagnostics.Eventing.Reader.EventLogQuery("System", System.Diagnostics.Eventing.Reader.PathType.LogName, "*[System[Provider[@Name='Tcpip'] and (EventID=4199 or EventID=4198) and TimeCreated[timediff(@SystemTime) <= 2592000000]]]") { ReverseDirection = true };
                using (var r = new System.Diagnostics.Eventing.Reader.EventLogReader(q))
                    for (int i = 0; i < 50; i++) { using (var e = r.ReadEvent()) { if (e == null) break; string msg; try { msg = e.FormatDescription() ?? ""; } catch (System.Diagnostics.Eventing.Reader.EventLogException) { msg = ""; } hits.Add(Tuple.Create(e.TimeCreated ?? DateTime.MinValue, msg)); } }
            }
            catch (Exception e) { if (!(e is System.Diagnostics.Eventing.Reader.EventLogException || e is UnauthorizedAccessException)) throw; rows.Add(R(1, "Event log", "couldn't read it: " + e.Message, "", "u")); }
            string mine; lock (m.L) mine = m.LocalIp;
            if (hits.Count == 0) rows.Add(R(0, "Address conflicts", "none in 30 days", "Windows checks for a second device with the same address each time it joins (and when one shows up later), and logs it - nothing like that happened.", "w"));
            else
            {
                var last = hits[0];
                rows.Add(R((DateTime.Now - last.Item1).TotalHours < 24 ? 2 : 1, "Address conflicts", (hits.Count >= 50 ? "50+" : hits.Count.ToString(CultureInfo.CurrentCulture)) + " in 30 days - the last " + last.Item1.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture), "Another device used the same address. Usually a device with a fixed address inside the router's DHCP range: give it an address outside the range, or a reservation in the router.", "w"));
                foreach (var h in hits.Take(10)) { var mm = Regex.Match(h.Item2, @"([0-9a-fA-F]{2}[-:]){5}[0-9a-fA-F]{2}"); rows.Add(R(3, h.Item1.ToString("d MMM HH:mm", CultureInfo.CurrentCulture), (mm.Success ? "the other device: " + mm.Value + (Toolkit.Vendor(mm.Value) != "" ? " (" + Toolkit.Vendor(mm.Value) + ")" : "") : Draw.Trunc(h.Item2, 140)), "", "w")); }
            }
            rows.Add(R(3, "Your address now", mine == "" ? "-" : mine, "Windows watches this for you all the time; AdeeboMeter reads what it saw.", "w"));
            return rows;
        }

        // ---------- the diagnosis in a few lines: for the QR code and the help-a-friend bundle (names and addresses removed)
        public static string Summary(Monitor m, int max)
        {
            var s = m.Latest; var c = CultureInfo.InvariantCulture; var sb = new StringBuilder();
            sb.Append("AdeeboMeter ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm", c)).Append('\n');
            sb.Append(Word(s.Level)).Append(": ").Append(s.Headline).Append('\n');
            if (!string.IsNullOrEmpty(s.Hint)) sb.Append("Why: ").Append(s.Hint).Append('\n');
            for (int i = 0; i < 5 && i < s.PathLabel.Length; i++) sb.Append(s.PathLabel[i]).Append(' ').Append(Word(s.Path[i])).Append(string.IsNullOrEmpty(s.PathMain[i]) ? "" : " " + s.PathMain[i]).Append('\n');
            var o = Outages.Last(m, 1); if (o.Count > 0) sb.Append("Outages today: ").Append(o.Count).Append('\n');
            string txt = GameSession.Redact(sb.ToString(), "");
            txt = Regex.Replace(txt, @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", "x.x.x.x");
            return txt.Length > max ? txt.Substring(0, max) : txt;
        }
        static string Word(int lv) { return lv == 0 ? "OK" : lv == 1 ? "Busy" : lv == 2 ? "Problem" : "-"; }

        // ---------- help a friend: the diagnosis and its fix steps in one encrypted .adeebo; the code travels separately
        public static List<string[]> FriendBundle(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>();
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); if (dir == "" || !Directory.Exists(dir)) dir = Program.DataDir;
            string txt = Path.Combine(dir, "AdeeboMeter-diagnosis-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".txt");
            try
            {
                var s = m.Latest; var sb = new StringBuilder(Summary(m, 4000));
                sb.Append("\nWhere to look\n");
                for (int i = 0; i < 5 && i < s.PathLabel.Length; i++) if (s.Path[i] == 1 || s.Path[i] == 2) sb.Append("- ").Append(s.PathLabel[i]).Append(": ").Append(GameSession.Redact((s.PathMain[i] ?? "") + " " + (s.PathSub[i] ?? ""), "").Trim()).Append('\n');
                sb.Append("\nOpen this file in AdeeboMeter (Toolbox > Decrypt) with the code you were given, then run Fix a problem on your own PC to compare.\n");
                File.WriteAllText(txt, sb.ToString(), new UTF8Encoding(false));
                rows.AddRange(FileCrypt.Encrypt(txt, "code", "", run));
                rows.Add(R(3, "For your friend", "send the .adeebo file; tell them the code another way (a call, a text). They open it in AdeeboMeter > Toolbox > Decrypt.", "Nothing goes through any server - you carry the file.", "i"));
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; rows.Add(R(2, "Bundle", "couldn't write it: " + e.Message, "", "u")); }
            finally { try { if (File.Exists(txt)) File.Delete(txt); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            rows.Add(File.Exists(txt) ? R(1, "Plain copy", "couldn't delete " + Path.GetFileName(txt) + " - delete it yourself (it's the unencrypted text)", "", "u") : R(0, "Plain copy", "deleted - only the encrypted file is left", "", "m"));
            return rows;
        }
    }
}
