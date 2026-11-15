// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Sports (Streaming › Sports, PLAN-modes.md 2)
    // Today in sport as a newspaper page: headlines from BBC Sport's RSS feeds, scores from the NHL's public feed (no key),
    // football-data.org and CricketData (each with the person's own free key). Off until turned on; fetched only while the
    // Sports page is open (or a stream plays and a followed team is live), at most every 5 minutes; cricket's free key
    // allows 100 calls a day, so it stops at 90. Keys are kept encrypted for this Windows user (DPAPI), never in the
    // settings file, so an exported settings file never carries them.
    static class Sports
    {
        internal sealed class Game { public string Sport = "", League = "", Home = "", Away = "", HomeScore = "", AwayScore = "", State = "", Detail = ""; public DateTime StartUtc; }
        internal sealed class Story { public string Sport = "", Title = "", Summary = "", Link = ""; public DateTime When; }
        internal static readonly object k = new object();
        internal static List<Game> Games = new List<Game>(); internal static List<Story> Stories = new List<Story>();
        internal static readonly Dictionary<string, string> Note = new Dictionary<string, string>();   // sport -> why nothing shows ("no key", "limit reached", an error)
        internal static DateTime FetchedAt = DateTime.MinValue; static volatile bool busy;
        internal static long Ver;
        public static readonly string[] Kinds = { "football", "cricket", "hockey" };
        public static string Name(string sport) { return sport == "football" ? "Football" : sport == "cricket" ? "Cricket" : sport == "hockey" ? "Hockey" : "Sport"; }

        // ---------- the person's own keys, encrypted for this Windows user
        static string KeyFile { get { return Path.Combine(Program.DataDir, "sportkeys.dat"); } }
        internal static string Key(string sport)
        {
            try
            {
                if (!File.Exists(KeyFile)) return "";
                foreach (var l in File.ReadAllLines(KeyFile))
                {
                    int i = l.IndexOf('='); if (i <= 0 || l.Substring(0, i) != sport) continue;
                    return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(l.Substring(i + 1)), null, DataProtectionScope.CurrentUser));
                }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("sport key: " + e.GetType().Name); }
            return "";
        }
        internal static void SetKey(string sport, string key)
        {
            key = (key ?? "").Trim();
            try
            {
                var lines = File.Exists(KeyFile) ? File.ReadAllLines(KeyFile).Where(l => !l.StartsWith(sport + "=", StringComparison.Ordinal)).ToList() : new List<string>();
                if (key != "") lines.Add(sport + "=" + Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser)));
                FullView.WriteSafe(KeyFile, string.Join("\n", lines) + "\n", false);
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("sport key save: " + e.GetType().Name); }
            lock (k) FetchedAt = DateTime.MinValue;
        }
        internal static bool HasKey(string sport) { return sport == "hockey" || Key(sport) != ""; }

        // ---------- the teams you follow: names or short codes, matched loosely ("TOR", "Leafs", "Arsenal", "India")
        internal static List<string> Teams() { return (Cfg.SportTeams ?? "").Split(',').Select(t => t.Trim()).Where(t => t.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList(); }
        internal static bool Mine(Game g)
        {
            foreach (var t in Teams())
                if (g.Home.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 || g.Away.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        internal static Game LiveMine() { lock (k) return Games.FirstOrDefault(g => g.State == "live" && Mine(g)); }

        // ---------- cricket's free key: 100 calls a day - stop at 90 so a busy day never runs it dry
        static string HitsFile { get { return Path.Combine(Program.DataDir, "sporthits.txt"); } }
        static int HitsToday() { try { var l = File.Exists(HitsFile) ? File.ReadAllText(HitsFile).Trim().Split(' ') : Array.Empty<string>(); int n; return l.Length == 2 && l[0] == DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) && int.TryParse(l[1], out n) ? n : 0; } catch (IOException) { return 0; } }
        static void Hit() { try { File.WriteAllText(HitsFile, DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + " " + (HitsToday() + 1).ToString(CultureInfo.InvariantCulture)); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

        // ---------- fetching: one background pass for every sport and the headlines, at most every 5 minutes
        public static bool Due { get { lock (k) return (DateTime.UtcNow - FetchedAt).TotalMinutes >= 5; } }
        public static void Fetch(Action done)
        {
            if (busy || !Cfg.Sports || Policy.NoOnline || !Due) return;
            busy = true;
            new Thread(() =>
            {
                var games = new List<Game>(); var stories = new List<Story>(); var notes = new Dictionary<string, string>();
                try
                {
                    Try(notes, "hockey", () => games.AddRange(Hockey()));
                    if (Key("football") == "") notes["football"] = "nokey"; else Try(notes, "football", () => games.AddRange(Football(Key("football"))));
                    if (Key("cricket") == "") notes["cricket"] = "nokey";
                    else if (HitsToday() >= 90) notes["cricket"] = "Today's 90 calls are used (your free key allows 100) - scores come back after midnight UTC.";
                    else Try(notes, "cricket", () => games.AddRange(Cricket(Key("cricket"))));
                    foreach (var f in new[] { Tuple.Create("top", "https://feeds.bbci.co.uk/sport/rss.xml"), Tuple.Create("football", "https://feeds.bbci.co.uk/sport/football/rss.xml"), Tuple.Create("cricket", "https://feeds.bbci.co.uk/sport/cricket/rss.xml") })
                        Try(notes, "news-" + f.Item1, () => stories.AddRange(Rss(Get(f.Item2, null, null), f.Item1).Take(10)));
                    lock (k) { Games = games; Stories = stories; Note.Clear(); foreach (var n in notes) Note[n.Key] = n.Value; FetchedAt = DateTime.UtcNow; }
                    Interlocked.Increment(ref Ver);
                }
                finally { busy = false; if (done != null) done(); }
            }) { IsBackground = true, Name = "sports" }.Start();
        }
        static void Try(Dictionary<string, string> notes, string what, Action a)
        {
            try { a(); }
            catch (Exception e) { if (e is OutOfMemoryException) throw; notes[what] = e is WebException ? "couldn't reach it (" + ((WebException)e).Status + ")" : "its answer couldn't be read"; Program.Log("sports " + what + ": " + e.Message); }
        }
        // a bounded GET: the timeouts are set before the request goes out, and at most 600 KB is read
        internal static string Get(string url, string header, string value)
        {
            var q = (HttpWebRequest)WebRequest.Create(url); q.Timeout = 10000; q.ReadWriteTimeout = 10000; q.UserAgent = "AdeeboMeter/" + Program.Version; q.Proxy = WebRequest.GetSystemWebProxy();
            if (header != null) q.Headers[header] = value;
            using (var r = q.GetResponse()) using (var s = r.GetResponseStream())
            {
                var buf = new byte[600 * 1024]; int got = 0, n;
                while (got < buf.Length && (n = s.Read(buf, got, buf.Length - got)) > 0) got += n;
                if (got == buf.Length) throw new InvalidDataException("answer too large");
                return Encoding.UTF8.GetString(buf, 0, got);
            }
        }
        static Dictionary<string, object> D(object o) { return o as Dictionary<string, object>; }
        static List<object> A(object o) { return o as List<object>; }
        static string Sv(Dictionary<string, object> d, string key) { object o; return d != null && d.TryGetValue(key, out o) && o != null ? Convert.ToString(o, CultureInfo.InvariantCulture) : ""; }
        static object V(Dictionary<string, object> d, string key) { object o; return d != null && d.TryGetValue(key, out o) ? o : null; }
        static DateTime Utc(string s) { DateTime t; return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t) ? t : DateTime.MinValue; }

        // the NHL's own scores feed (no key): today's games, live, final or coming
        internal static List<Game> Hockey() { return HockeyFrom(Get("https://api-web.nhle.com/v1/score/now", null, null)); }
        internal static List<Game> HockeyFrom(string json)
        {
            var r = new List<Game>(); var root = D(Json.Parse(json));
            foreach (var o in A(V(root, "games")) ?? new List<object>())
            {
                var g = D(o); if (g == null) continue; var h = D(V(g, "homeTeam")); var a = D(V(g, "awayTeam")); string st = Sv(g, "gameState");
                var x = new Game { Sport = "hockey", League = "NHL", Home = Sv(h, "abbrev"), Away = Sv(a, "abbrev"), HomeScore = Sv(h, "score"), AwayScore = Sv(a, "score"), StartUtc = Utc(Sv(g, "startTimeUTC")) };
                x.State = st == "LIVE" || st == "CRIT" ? "live" : st == "FINAL" || st == "OFF" ? "done" : "soon";
                var pd = D(V(g, "periodDescriptor")); var clock = D(V(g, "clock")); string per = Sv(pd, "number");
                x.Detail = x.State == "live" ? (per != "" ? "P" + per : "") + (Sv(clock, "timeRemaining") != "" ? " · " + Sv(clock, "timeRemaining") : "") + (Sv(clock, "inIntermission") == "True" ? " · break" : "") : x.State == "done" ? "Final" + (Sv(pd, "periodType") == "OT" ? " (OT)" : Sv(pd, "periodType") == "SO" ? " (SO)" : "") : "";
                if (x.Home != "" && x.Away != "") r.Add(x);
            }
            return r;
        }
        // football-data.org: today's matches in the competitions the free key covers
        internal static List<Game> Football(string key) { return FootballFrom(Get("https://api.football-data.org/v4/matches", "X-Auth-Token", key)); }
        internal static List<Game> FootballFrom(string json)
        {
            var r = new List<Game>(); var root = D(Json.Parse(json));
            foreach (var o in A(V(root, "matches")) ?? new List<object>())
            {
                var m = D(o); if (m == null) continue; var h = D(V(m, "homeTeam")); var a = D(V(m, "awayTeam")); var ft = D(V(D(V(m, "score")), "fullTime")); string st = Sv(m, "status");
                var x = new Game { Sport = "football", League = Sv(D(V(m, "competition")), "name"), Home = Or(Sv(h, "shortName"), Sv(h, "name")), Away = Or(Sv(a, "shortName"), Sv(a, "name")), HomeScore = Sv(ft, "home"), AwayScore = Sv(ft, "away"), StartUtc = Utc(Sv(m, "utcDate")) };
                x.State = st == "IN_PLAY" || st == "PAUSED" || st == "LIVE" ? "live" : st == "FINISHED" || st == "AWARDED" ? "done" : st == "POSTPONED" || st == "CANCELLED" || st == "SUSPENDED" ? "off" : "soon";
                x.Detail = st == "PAUSED" ? "half time" : x.State == "done" ? "FT" : x.State == "off" ? st.ToLowerInvariant() : "";
                if (x.Home != "") r.Add(x);
            }
            return r;
        }
        // CricketData (CricAPI): matches on now, with each innings' runs / wickets (overs)
        internal static List<Game> Cricket(string key) { Hit(); return CricketFrom(Get("https://api.cricapi.com/v1/currentMatches?offset=0&apikey=" + Uri.EscapeDataString(key), null, null)); }
        internal static List<Game> CricketFrom(string json)
        {
            var r = new List<Game>(); var root = D(Json.Parse(json));
            if (Sv(root, "status") == "failure") throw new InvalidDataException(Sv(root, "reason"));
            foreach (var o in A(V(root, "data")) ?? new List<object>())
            {
                var m = D(o); if (m == null) continue; var teams = (A(V(m, "teams")) ?? new List<object>()).Select(t => Convert.ToString(t, CultureInfo.InvariantCulture)).ToList(); if (teams.Count < 2) continue;
                var x = new Game { Sport = "cricket", League = Sv(m, "matchType").ToUpperInvariant(), Home = teams[0], Away = teams[1], Detail = Sv(m, "status"), StartUtc = Utc(Sv(m, "dateTimeGMT")) };
                foreach (var s in A(V(m, "score")) ?? new List<object>())
                {
                    var sc = D(s); string inn = Sv(sc, "inning"), runs = Sv(sc, "r") + "/" + Sv(sc, "w") + " (" + Sv(sc, "o") + ")";
                    if (inn.StartsWith(x.Home, StringComparison.OrdinalIgnoreCase)) x.HomeScore = x.HomeScore == "" ? runs : x.HomeScore + " & " + runs; else if (inn.StartsWith(x.Away, StringComparison.OrdinalIgnoreCase)) x.AwayScore = x.AwayScore == "" ? runs : x.AwayScore + " & " + runs;
                }
                bool started = Sv(m, "matchStarted") == "True", ended = Sv(m, "matchEnded") == "True";
                x.State = ended ? "done" : started ? "live" : "soon";
                r.Add(x);
            }
            return r;
        }
        // RSS 2.0 items: title, description, link, date - read with plain text matching (no XML library in this build)
        internal static List<Story> Rss(string xml, string sport)
        {
            var r = new List<Story>();
            foreach (Match it in Regex.Matches(xml ?? "", @"<item\b[^>]*>(.*?)</item>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                Func<string, string> tag = t => { var m = Regex.Match(it.Groups[1].Value, "<" + t + @"\b[^>]*>(.*?)</" + t + ">", RegexOptions.Singleline | RegexOptions.IgnoreCase); return m.Success ? Clean(m.Groups[1].Value) : ""; };
                var s = new Story { Sport = sport, Title = tag("title"), Summary = tag("description"), Link = tag("link") };
                DateTime w; if (DateTime.TryParse(tag("pubDate"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out w)) s.When = w;
                if (s.Title != "" && (s.Link.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || s.Link.StartsWith("http://", StringComparison.OrdinalIgnoreCase))) r.Add(s);
            }
            return r;
        }
        static string Clean(string s)
        {
            s = Regex.Replace(s, @"^\s*<!\[CDATA\[(.*?)\]\]>\s*$", "$1", RegexOptions.Singleline);
            s = Regex.Replace(s, "<[^>]+>", " ");
            s = WebUtility.HtmlDecode(s);
            return Regex.Replace(s, @"\s+", " ").Trim();
        }
        static string Or(string a, string b) { return string.IsNullOrEmpty(a) ? b : a; }
    }
}
