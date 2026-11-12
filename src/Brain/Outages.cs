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
        public sealed class O { public DateTime From, To; public bool Ongoing; public TimeSpan Len { get { var d = To - From; return d < TimeSpan.Zero ? TimeSpan.Zero : d; } } /* local times: a DST fall-back mid-outage must not give a negative length */ }
        static List<O> cache; static DateTime cacheAt;
        static readonly object k = new object();
        public static List<O> Last(Monitor m, int days)
        {
            lock (k)
            {
                if (cache != null && (DateTime.UtcNow - cacheAt).TotalSeconds < 60) return cache;
                var evs = new List<Timeline.Ev>();
                if (Cfg.History) foreach (var d in History.Days().Where(d => d >= DateTime.Today.AddDays(-(days - 1)))) evs.AddRange(History.EventsOf(d));
                if (m != null) foreach (var e in m.Events.Recent(300)) if (!evs.Any(x => x.T == e.T && x.Text == e.Text)) evs.Add(e);
                cache = Pair(evs.Where(e => e.Kind == "inet").OrderBy(e => e.T).ToList(), DateTime.Now); cacheAt = DateTime.UtcNow;
                return cache;
            }
        }
        // went quiet (level 2) ... back (level 0) -> one outage; a quiet with no "back" yet is ongoing
        public static List<O> Pair(List<Timeline.Ev> inet, DateTime now)
        {
            var r = new List<O>(); O open = null;
            foreach (var e in inet)
            {
                if (e.Lv == 2 && open == null) open = new O { From = e.T };
                else if (e.Lv == 0 && open != null) { open.To = e.T; r.Add(open); open = null; }
            }
            if (open != null) { open.To = now; open.Ongoing = true; r.Add(open); }
            r.Reverse();
            return r;
        }
        public static string Text(O o)
        {
            var c = CultureInfo.CurrentCulture;
            string len = o.Len.TotalMinutes < 1 ? Math.Max(1, (int)o.Len.TotalSeconds) + " s" : o.Len.TotalHours < 1 ? (int)o.Len.TotalMinutes + " min" : (int)o.Len.TotalHours + " h " + o.Len.Minutes + " min";
            return o.From.ToString("ddd d MMM  HH:mm", c) + " - " + (o.Ongoing ? "now" : o.To.ToString("HH:mm", c)) + "  ·  " + len + (o.Ongoing ? " so far" : "");
        }
    }
}
