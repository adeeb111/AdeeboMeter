// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AdeeboMeter
{
    // ================================================================== your week, across everything you do online
    // One engine judges every group (streams, the broker, games, the internet itself), so one report can add them up:
    // "most of this week's trouble was your Wi-Fi". Built from records already kept - nothing new is measured or sent.
    static class Weekly
    {
        public sealed class Part { public string Id, Title, Line; public int Lv; public Dictionary<string, int> Blame = NewTally(); }
        public sealed class Report { public List<Part> Parts = new List<Part>(); public Dictionary<string, int> Blame = NewTally(); public string Head = ""; public int Lv; public DateTime From, To; }
        public sealed class Evidence { public DateTime T; public string What, Text; }
        static readonly string[] Order = { "pc", "wifi", "home", "provider", "far", "unsure" };
        static Dictionary<string, int> NewTally() { var d = new Dictionary<string, int>(StringComparer.Ordinal); foreach (var o in Order) d[o] = 0; return d; }

        static Report cache; static DateTime cacheAt = DateTime.MinValue; static volatile bool busy; static readonly object k = new object();
        // the last 7 days: the reading happens on a worker thread (a week of game files can take a moment), never in a paint;
        // the screen gets the last result straight away and "ready" when a fresher one is in. Refreshed every 10 minutes.
        public static Report Last7(Monitor m, Action ready)
        {
            Report c; lock (k) c = cache;
            if ((DateTime.UtcNow - cacheAt).TotalMinutes >= 10 && !busy)
            {
                busy = true; cacheAt = DateTime.UtcNow;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        var to = DateTime.Now; var from = DateTime.Today.AddDays(-6);
                        var all = WatchKit.RecentNow(null);
                        var streams = all.Where(s => s.Mode == "stream" && s.Start >= from).ToList();
                        var trades = all.Where(s => s.Mode == "trade" && s.Start >= from).ToList();
                        var games = new List<GameHistory.Row>();
                        if (Cfg.History && Games.Monitored().Count > 0) for (int i = 0; i < 7; i++) games.AddRange(GameHistory.Read(DateTime.Today.AddDays(-i), null));
                        var outs = m != null ? Outages.Last(m, 7).Where(o => o.From >= from).ToList() : new List<Outages.O>();
                        var r = Build(streams, trades, games, outs, from, to);
                        lock (k) cache = r;
                        if (ready != null) ready();
                    }
                    catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("weekly: " + e.Message); }
                    finally { busy = false; }
                });
            }
            return c;
        }
        public static void Drop() { lock (k) { cache = null; cacheAt = DateTime.MinValue; } }

        public static Report Build(List<WatchKit.Session> streams, List<WatchKit.Session> trades, List<GameHistory.Row> games, List<Outages.O> outs, DateTime from, DateTime to)
        {
            var r = new Report { From = from, To = to };
            // the internet itself
            if (outs.Count > 0)
            {
                var seen = outs.Where(o => !o.EndUnseen).ToList(); double mins = seen.Sum(o => o.Len.TotalMinutes);
                var p = new Part { Id = "net", Title = "Internet", Lv = mins >= 30 || outs.Count >= 5 ? 2 : 1 };
                p.Line = outs.Count + (outs.Count == 1 ? " outage" : " outages") + (seen.Count > 0 ? ", " + Mins(mins) + " in all" : "") + (seen.Count > 0 ? " · longest " + Mins(seen.Max(o => o.Len.TotalMinutes)) + " (" + seen.OrderByDescending(o => o.Len).First().From.ToString("ddd HH:mm", CultureInfo.CurrentCulture) + ")" : "");
                r.Parts.Add(p);
            }
            // streams and the broker: their dips already carry whose fault
            Action<string, string, List<WatchKit.Session>, string> sessions = (id, title, ss, unit) =>
            {
                if (ss.Count == 0) return;
                var p = new Part { Id = id, Title = title }; int dips = 0;
                foreach (var s in ss) foreach (var d in s.Dips) { int c; if (p.Blame.TryGetValue(d.Blame, out c)) p.Blame[d.Blame] = c + 1; dips++; }
                double hrs = ss.Sum(s => s.Length.TotalHours);
                p.Lv = dips == 0 ? 0 : dips >= 5 ? 2 : 1;
                p.Line = ss.Count + " " + unit + (ss.Count == 1 ? "" : "s") + ", " + Hours(hrs) + " · " + (dips == 0 ? "no dips" : dips + (dips == 1 ? " dip: " : " dips: ") + TallyText(p.Blame, id == "trade" ? Trading.FarName : Streams.FarName));
                r.Parts.Add(p);
            };
            sessions("stream", "Streaming", streams, "stream");
            sessions("trade", "Trading", trades, "trading day");
            // games: a rough minute is loss of 2% or more, or a 95th-percentile ping over 150 ms; the router's ping that minute
            // says whether home was to blame (over 80 ms), otherwise it was past the router
            if (games.Count > 0)
            {
                var p = new Part { Id = "game", Title = "Gaming" };
                var minutes = games.GroupBy(g => g.T).ToList(); int rough = 0;
                foreach (var mm in minutes)
                {
                    if (!mm.Any(g => g.Loss >= 2 || g.P95 > 150)) continue;
                    rough++; double gw = mm.Select(g => g.Gw).Where(v => !double.IsNaN(v)).DefaultIfEmpty(double.NaN).Max();
                    p.Blame[double.IsNaN(gw) ? "unsure" : gw > 80 ? "home" : "provider"]++;
                }
                double hrs = minutes.Count / 60.0; string names = string.Join(", ", games.Select(g => g.Game).Distinct().Take(3).Select(GameName));
                p.Lv = rough == 0 ? 0 : rough >= 15 ? 2 : 1;
                p.Line = names + " · " + Hours(hrs) + " watched · " + (rough == 0 ? "no rough minutes" : rough + (rough == 1 ? " rough minute: " : " rough minutes: ") + TallyText(p.Blame, "the game's server"));
                r.Parts.Add(p);
            }
            foreach (var p in r.Parts) foreach (var o in Order) r.Blame[o] += p.Blame[o];
            r.Lv = r.Parts.Count == 0 ? 0 : r.Parts.Max(p => p.Lv);
            int total = r.Blame.Values.Sum(); var top = r.Blame.OrderByDescending(kv => kv.Value).First();
            if (r.Parts.Count == 0) r.Head = "";
            else if (total == 0) r.Head = r.Parts.Any(p => p.Id == "net") ? "Apart from the outages below, everything you did ran smooth." : "A smooth week - nothing you did hit a problem.";
            else r.Head = (top.Value * 2 > total ? "Most of this week's trouble: " : "This week's trouble, biggest share: ") + Low(WatchKit.BlameName(top.Key, "the services' own servers")) + " (" + top.Value + " of " + total + ")";
            return r;
        }
        public static string TopBlame(Report r) { int total = r.Blame.Values.Sum(); return total == 0 ? null : r.Blame.OrderByDescending(kv => kv.Value).First().Key; }
        // "Your Wi-Fi" -> "your Wi-Fi" (only the first letter: Wi-Fi and PC keep their capitals)
        static string Low(string s) { return string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1); }
        static string GameName(string id) { var g = Games.All.FirstOrDefault(x => x.Id == id); return g != null ? g.Name : id; }
        static string TallyText(Dictionary<string, int> t, string farName) { return string.Join(", ", t.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => kv.Value + " " + Low(WatchKit.BlameName(kv.Key, farName)))); }
        static string Mins(double m) { return m < 1 ? "under a minute" : m < 60 ? ((int)Math.Round(m)).ToString(CultureInfo.CurrentCulture) + " min" : ((int)(m / 60)).ToString(CultureInfo.CurrentCulture) + " h " + ((int)m % 60).ToString(CultureInfo.CurrentCulture) + " min"; }
        static string Hours(double h) { return h < 1 ? Math.Max(1, (int)Math.Round(h * 60)).ToString(CultureInfo.CurrentCulture) + " min" : h.ToString(h < 10 ? "0.#" : "0", CultureInfo.CurrentCulture) + " h"; }

        public static string Text(Report r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("My week online, " + r.From.ToString("ddd d MMM", CultureInfo.CurrentCulture) + " - " + r.To.ToString("ddd d MMM", CultureInfo.CurrentCulture) + " (measured by AdeeboMeter on my PC)");
            if (r.Head != "") sb.AppendLine(r.Head);
            foreach (var p in r.Parts) sb.AppendLine("- " + p.Title + ": " + p.Line);
            return sb.ToString();
        }

        // ---------- for the provider: every slow moment the records put past the router, dated, with its evidence
        public static List<Evidence> ProviderMoments(int days)
        {
            var from = DateTime.Today.AddDays(-(days - 1)); var l = new List<Evidence>();
            foreach (var s in WatchKit.RecentNow(null).Where(z => z.Start >= from))
                foreach (var d in s.Dips.Where(z => z.Blame == "provider"))
                    l.Add(new Evidence { T = d.T, What = (s.Mode == "trade" ? "Trading (" + s.Far + ")" : "Streaming " + s.What), Text = d.Secs + " s · " + d.Evidence });
            // games: rough minutes (loss 2% or more, or a 95th-percentile ping over 150 ms) with the router fine, by game and hour
            if (Cfg.History && Games.Monitored().Count > 0)
            {
                var rows = new List<GameHistory.Row>(); for (int i = 0; i < days; i++) rows.AddRange(GameHistory.Read(DateTime.Today.AddDays(-i), null));
                foreach (var grp in rows.Where(g => (g.Loss >= 2 || g.P95 > 150) && !double.IsNaN(g.Gw) && g.Gw <= 80).GroupBy(g => g.Game + "|" + g.T.ToString("yyyyMMddHH", CultureInfo.InvariantCulture)))
                {
                    var f = grp.OrderBy(g => g.T).First(); int n = grp.Select(g => g.T).Distinct().Count();
                    l.Add(new Evidence { T = f.T, What = "Gaming " + GameName(f.Game), Text = n + (n == 1 ? " rough minute" : " rough minutes") + " on " + f.Server + " · worst " + grp.Max(g => double.IsNaN(g.P95) ? 0 : g.P95).ToString("0", CultureInfo.InvariantCulture) + " ms, loss up to " + grp.Max(g => double.IsNaN(g.Loss) ? 0 : g.Loss).ToString("0.#", CultureInfo.InvariantCulture) + "% · router " + grp.Max(g => g.Gw).ToString("0", CultureInfo.InvariantCulture) + " ms" });
                }
            }
            return l.OrderByDescending(e => e.T).ToList();
        }
    }
}
