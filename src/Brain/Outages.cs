// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== the outage log: every time the internet went quiet
    // Built from the timeline's own "inet" events (went quiet -> back), from saved history when it's on, plus the last two
    // hours in memory. Nothing new is recorded for it.
    static class Outages
    {
        public sealed class O { public DateTime From, To; public bool Ongoing, EndUnseen; public TimeSpan Len { get { var d = To - From; return d < TimeSpan.Zero ? TimeSpan.Zero : d; } } /* local times: a DST fall-back mid-outage must not give a negative length */ }
        // one cache per length: the Overview asks for today, the outage list for 7 days - a single slot made them reload each other every tick
        static readonly Dictionary<int, KeyValuePair<DateTime, List<O>>> cache = new Dictionary<int, KeyValuePair<DateTime, List<O>>>();
        static readonly object k = new object();
        public static List<O> Last(Monitor m, int days)
        {
            lock (k)
            {
                KeyValuePair<DateTime, List<O>> c; if (cache.TryGetValue(days, out c) && (DateTime.UtcNow - c.Key).TotalSeconds < 60) return c.Value;
                var evs = new List<Timeline.Ev>();
                if (Cfg.History) foreach (var d in History.Days().Where(d => d >= DateTime.Today.AddDays(-(days - 1)))) evs.AddRange(History.EventsOf(d));
                if (m != null) foreach (var e in m.Events.Recent(300)) if (!evs.Any(x => x.T == e.T && x.Text == e.Text)) evs.Add(e);
                var res = Pair(evs.Where(e => e.Kind == "inet").OrderBy(e => e.T).ToList(), DateTime.Now, m != null && m.NetQuiet); cache[days] = new KeyValuePair<DateTime, List<O>>(DateTime.UtcNow, res);
                return res;
            }
        }
        // went quiet (level 2) ... back (level 0) -> one outage; a quiet with no "back" yet is ongoing
        // A "went quiet" long after another with no "back" between (the app closed, the PC slept, the check was switched
        // off): the first one's end wasn't seen - it's listed without a made-up length. An open one at the end is ongoing
        // only while the internet is still quiet now.
        public static List<O> Pair(List<Timeline.Ev> inet, DateTime now) { return Pair(inet, now, true); }
        public static List<O> Pair(List<Timeline.Ev> inet, DateTime now, bool quietNow)
        {
            var r = new List<O>(); O open = null; DateTime lastQuiet = DateTime.MinValue;
            foreach (var e in inet)
            {
                if (e.Lv == 2 && open != null && (e.T - lastQuiet).TotalMinutes > 10) { open.To = lastQuiet; open.EndUnseen = true; r.Add(open); open = null; }
                if (e.Lv == 2) { lastQuiet = e.T; if (open == null) open = new O { From = e.T }; }
                else if (e.Lv == 0 && open != null) { open.To = e.T; r.Add(open); open = null; }
            }
            if (open != null) { if (quietNow) { open.To = now; open.Ongoing = true; } else { open.To = lastQuiet; open.EndUnseen = true; } r.Add(open); }
            r.Reverse();
            return r;
        }
        public static string Text(O o)
        {
            var c = CultureInfo.CurrentCulture;
            string len = o.Len.TotalMinutes < 1 ? Math.Max(1, (int)o.Len.TotalSeconds) + " s" : o.Len.TotalHours < 1 ? (int)o.Len.TotalMinutes + " min" : (int)o.Len.TotalHours + " h " + o.Len.Minutes + " min";
            if (o.EndUnseen) return o.From.ToString("ddd d MMM  HH:mm", c) + "  ·  end not seen (AdeeboMeter was closed, the PC slept or the network changed)";
            return o.From.ToString("ddd d MMM  HH:mm", c) + " - " + (o.Ongoing ? "now" : o.To.ToString("HH:mm", c)) + "  ·  " + len + (o.Ongoing ? " so far" : "");
        }
    }
}
