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
        internal sealed class Game { public string Sport = "", League = "", Home = "", Away = "", HomeScore = "", AwayScore = "", State = "", Detail = "", HomeAlso = "", AwayAlso = ""; public DateTime StartUtc; }   // *Also: the full names, for matching what people type
        internal sealed class Story { public string Sport = "", Title = "", Summary = "", Link = ""; public DateTime When; }
        internal static readonly object k = new object();
        internal static List<Game> Games = new List<Game>(); internal static List<Story> Stories = new List<Story>();
        internal static readonly Dictionary<string, string> Note = new Dictionary<string, string>();   // sport -> why nothing shows ("no key", "limit reached", an error)
        internal static DateTime FetchedAt = DateTime.MinValue; static volatile bool busy; static int keyGen;
        internal static readonly Dictionary<string, DateTime> FeedAt = new Dictionary<string, DateTime>();   // AM-CG-278: when each feed last came in
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
        // AM-CG-280: says whether the key was saved; a fetch already running with the old key throws its answer away
        internal static bool SetKey(string sport, string key)
        {
            key = (key ?? "").Trim(); bool ok = false;
            try
            {
                var lines = File.Exists(KeyFile) ? File.ReadAllLines(KeyFile).Where(l => !l.StartsWith(sport + "=", StringComparison.Ordinal)).ToList() : new List<string>();
                if (key != "") lines.Add(sport + "=" + Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser)));
                FullView.WriteSafe(KeyFile, string.Join("\n", lines) + "\n", false);
                ok = Key(sport) == key;
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("sport key save: " + e.GetType().Name); }
            lock (k) { FetchedAt = DateTime.MinValue; keyGen++; }
            return ok;
        }
        internal static bool HasKey(string sport) { return sport == "hockey" || Key(sport) != ""; }

        // ---------- the teams you follow: names or short codes, matched loosely ("TOR", "Leafs", "Arsenal", "India")
        internal static List<string> Teams() { return (Cfg.SportTeams ?? "").Split(',').Select(t => t.Trim()).Where(t => t.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList(); }
        internal static bool Mine(Game g)
        {
            // AM-CG-275: whole words only ("TOR" doesn't match "Toronto FC"'s neighbours, "In" never matches "India"), against the
            // short code and the full name the feed gives
            foreach (var rx in TeamRx())
                if (rx.IsMatch(g.Home) || rx.IsMatch(g.Away) || rx.IsMatch(g.HomeAlso) || rx.IsMatch(g.AwayAlso)) return true;
            return false;
        }
        // bug-hunt M-16: built once per team list, not once per team per game per paint
        static string rxFor; static List<Regex> rxList = new List<Regex>(); static readonly object rxLock = new object();
        static List<Regex> TeamRx()
        {
            string now = Cfg.SportTeams ?? "";
            lock (rxLock)
            {
                if (now != rxFor) { rxList = Teams().Select(t => new Regex(@"(?<![\p{L}\p{N}])" + Regex.Escape(t) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToList(); rxFor = now; }
                return rxList;
            }
        }
        // one followed team in one game (My Teams' rows)
        internal static bool TeamIn(string team, Game g)
        {
            var rx = new Regex(@"(?<![\p{L}\p{N}])" + Regex.Escape(team) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return rx.IsMatch(g.Home) || rx.IsMatch(g.Away) || rx.IsMatch(g.HomeAlso) || rx.IsMatch(g.AwayAlso);
        }
        internal static Game LiveMine() { lock (k) return Games.FirstOrDefault(g => g.State == "live" && Mine(g)); }
        internal static int LiveMineCount() { lock (k) return Games.Count(g => g.State == "live" && Mine(g)); }

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
                int gen; lock (k) gen = keyGen;
                // AM-CG-278: each feed shows up as soon as it's in, and a feed that fails keeps what it had (marked as old)
                Action<string, List<Game>, List<Story>, string> publish = (feed, gs, ss, note) =>
                {
                    lock (k)
                    {
                        if (gen != keyGen) return;   // the key changed while this ran - this answer is for the old one
                        if (note == null) { Note.Remove(feed); FeedAt[feed] = DateTime.UtcNow; } else Note[feed] = note;
                        if (gs != null) Games = Games.Where(z => z.Sport != feed).Concat(gs).ToList();
                        if (ss != null) { string sp = feed.Substring(5); Stories = Stories.Where(z => z.Sport != sp).Concat(ss).ToList(); }
                    }
                    Interlocked.Increment(ref Ver); if (done != null) try { done(); } catch (Exception e) { if (e is OutOfMemoryException) throw; }
                };
                Func<string, Func<List<Game>>, bool> sport = (sp, f) => { try { publish(sp, f(), null, null); return true; } catch (Exception e) { if (e is OutOfMemoryException) throw; publish(sp, null, null, Why(e)); Program.Log("sports " + sp + ": " + e.Message); return false; } };
                try
                {
                    sport("hockey", Hockey);
                    if (Key("football") == "") publish("football", new List<Game>(), null, "nokey"); else sport("football", () => Football(Key("football")));
                    if (Key("cricket") == "") publish("cricket", new List<Game>(), null, "nokey");
                    else if (HitsToday() >= 90) publish("cricket", null, null, "Today's 90 calls are used (your free key allows 100) - scores come back after midnight UTC.");
                    else sport("cricket", () => Cricket(Key("cricket")));
                    foreach (var f in new[] { Tuple.Create("top", "https://feeds.bbci.co.uk/sport/rss.xml"), Tuple.Create("football", "https://feeds.bbci.co.uk/sport/football/rss.xml"), Tuple.Create("cricket", "https://feeds.bbci.co.uk/sport/cricket/rss.xml") })
                    {
                        string feed = "news-" + f.Item1;
                        try { publish(feed, null, Rss(Get(f.Item2, null, null), f.Item1).Take(10).ToList(), null); }
                        catch (Exception e) { if (e is OutOfMemoryException) throw; publish(feed, null, null, Why(e)); Program.Log("sports " + feed + ": " + e.Message); }
                    }
                    lock (k) if (gen == keyGen) FetchedAt = DateTime.UtcNow;
                }
                finally { busy = false; MarketData.Done(done); }   // bug-hunt M-3
            }) { IsBackground = true, Name = "sports" }.Start();
        }
        static string Why(Exception e) { return e is WebException ? "couldn't reach it (" + ((WebException)e).Status + ")" : "its answer couldn't be read"; }
        internal static string Get(string url, string header, string value) { return HttpSmall.Get(url, 600, header, value); }
        static Dictionary<string, object> D(object o) { return o as Dictionary<string, object>; }
        static List<object> A(object o) { return o as List<object>; }
        static string Sv(Dictionary<string, object> d, string key) { object o; return d != null && d.TryGetValue(key, out o) && o != null ? Convert.ToString(o, CultureInfo.InvariantCulture) : ""; }
        static object V(Dictionary<string, object> d, string key) { object o; return d != null && d.TryGetValue(key, out o) ? o : null; }
        static DateTime Utc(string s) { DateTime t; return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t) ? t : DateTime.MinValue; }

        // the NHL's own scores feed (no key): today's games, live, final or coming
        internal static List<Game> Hockey() { return HockeyFrom(Get("https://api-web.nhle.com/v1/score/now", null, null)); }
        internal static List<Game> HockeyFrom(string json)
        {
            var r = new List<Game>(); var root = D(Json.Parse(json)); var list = A(V(root, "games"));
            if (list == null) throw new InvalidDataException("no games list in the NHL's answer");   // AM-CG-276: a changed feed isn't "no games today"
            foreach (var o in list)
            {
                var g = D(o); if (g == null) continue; var h = D(V(g, "homeTeam")); var a = D(V(g, "awayTeam")); string st = Sv(g, "gameState");
                var x = new Game { Sport = "hockey", League = "NHL", Home = Sv(h, "abbrev"), Away = Sv(a, "abbrev"), HomeAlso = Sv(D(V(h, "placeName")), "default") + " " + Sv(D(V(h, "name")), "default") + " " + Sv(D(V(h, "commonName")), "default"), AwayAlso = Sv(D(V(a, "placeName")), "default") + " " + Sv(D(V(a, "name")), "default") + " " + Sv(D(V(a, "commonName")), "default"), HomeScore = Sv(h, "score"), AwayScore = Sv(a, "score"), StartUtc = Utc(Sv(g, "startTimeUTC")) };
                x.State = st == "LIVE" || st == "CRIT" ? "live" : st == "FINAL" || st == "OFF" ? "done" : st == "FUT" || st == "PRE" || st == "" ? "soon" : "off";   // AM-CG-282: anything else (postponed...) says what it is
                var pd = D(V(g, "periodDescriptor")); var clock = D(V(g, "clock")); string per = Sv(pd, "number");
                x.Detail = x.State == "live" ? (per != "" ? "P" + per : "") + (Sv(clock, "timeRemaining") != "" ? " · " + Sv(clock, "timeRemaining") : "") + (Sv(clock, "inIntermission") == "True" ? " · break" : "") : x.State == "done" ? "Final" + (Sv(pd, "periodType") == "OT" ? " (OT)" : Sv(pd, "periodType") == "SO" ? " (SO)" : "") : "";
                if (x.State == "off") x.Detail = st.ToLowerInvariant();
                if (x.Home != "" && x.Away != "") r.Add(x);
            }
            return r;
        }
        // football-data.org: today's matches in the competitions the free key covers
        internal static List<Game> Football(string key) { return FootballFrom(Get("https://api.football-data.org/v4/matches", "X-Auth-Token", key)); }
        internal static List<Game> FootballFrom(string json)
        {
            var r = new List<Game>(); var root = D(Json.Parse(json)); var list = A(V(root, "matches"));
            if (list == null) throw new InvalidDataException("no matches list in football-data.org's answer");
            foreach (var o in list)
            {
                var m = D(o); if (m == null) continue; var h = D(V(m, "homeTeam")); var a = D(V(m, "awayTeam")); var ft = D(V(D(V(m, "score")), "fullTime")); string st = Sv(m, "status");
                var x = new Game { Sport = "football", League = Sv(D(V(m, "competition")), "name"), Home = Or(Sv(h, "shortName"), Sv(h, "name")), Away = Or(Sv(a, "shortName"), Sv(a, "name")), HomeAlso = Sv(h, "name") + " " + Sv(h, "tla"), AwayAlso = Sv(a, "name") + " " + Sv(a, "tla"), HomeScore = Sv(ft, "home"), AwayScore = Sv(ft, "away"), StartUtc = Utc(Sv(m, "utcDate")) };
                x.State = st == "IN_PLAY" || st == "PAUSED" || st == "LIVE" ? "live" : st == "FINISHED" || st == "AWARDED" ? "done" : st == "POSTPONED" || st == "CANCELLED" || st == "SUSPENDED" ? "off" : "soon";
                x.Detail = st == "PAUSED" ? "half time" : st == "AWARDED" ? "awarded" : x.State == "done" ? "FT" : x.State == "off" ? st.ToLowerInvariant() : "";
                if (x.Home != "") r.Add(x);
            }
            return r;
        }
        // CricketData (CricAPI): matches on now, with each innings' runs / wickets (overs)
        // AM-CG-277: the feed comes in pages - a second page is read when there is one (one more call), never more than two
        internal static List<Game> Cricket(string key)
        {
            Hit(); int total; var r = CricketFrom(Get("https://api.cricapi.com/v1/currentMatches?offset=0&apikey=" + Uri.EscapeDataString(key), null, null), out total);
            if (total > r.Count && r.Count > 0 && HitsToday() < 89) { Hit(); int t2; r.AddRange(CricketFrom(Get("https://api.cricapi.com/v1/currentMatches?offset=" + r.Count + "&apikey=" + Uri.EscapeDataString(key), null, null), out t2)); }
            return r;
        }
        internal static List<Game> CricketFrom(string json) { int total; return CricketFrom(json, out total); }
        static List<Game> CricketFrom(string json, out int total)
        {
            var r = new List<Game>(); var root = D(Json.Parse(json));
            if (Sv(root, "status") == "failure") throw new InvalidDataException(Sv(root, "reason"));
            var list = A(V(root, "data")); if (list == null) throw new InvalidDataException("no match list in CricketData's answer");
            if (!int.TryParse(Sv(D(V(root, "info")), "totalRows"), NumberStyles.Integer, CultureInfo.InvariantCulture, out total)) total = 0;
            foreach (var o in list)
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
