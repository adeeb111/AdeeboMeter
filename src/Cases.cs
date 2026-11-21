// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== cases: one front door, many side doors (1.2 design, Adeeb 14 Nov)
    // A case is a problem as you'd say it ("my Teams call keeps freezing"), the time it happened, and where you came from
    // (the search box, Home, a stream's dip, a slow broker moment, a red tool result, a minute in the Time machine). It runs
    // the guided investigation that fits (Investigate.cs - the same checks, nothing new is sent), adds what AdeeboMeter
    // already recorded about that time, and ends in one page and one report.
    static class Cases
    {
        // id, what people say, the investigation it runs, the words that find it, the Learn topic behind it
        public static readonly string[][] Symptoms = {
            new[] { "calls", "Calls freeze or sound robotic", "calls", "teams zoom call calls meeting freeze freezing robotic voice webex skype choppy echo", "speed-vs-delay" },
            new[] { "game", "Game lag or spikes", "slow", "game gaming lag laggy ping spike spikes rubberband rubber banding fps", "speed-vs-delay" },
            new[] { "buffer", "Video keeps buffering", "slow", "buffer buffering netflix youtube stream streaming video twitch spinning", "streaming-vs-calls" },
            new[] { "slow", "The internet is slow", "slow", "slow internet speed sluggish crawling", "speed-vs-delay" },
            new[] { "drops", "Wi-Fi keeps dropping", "drops", "drop drops dropping disconnect disconnects disconnecting wifi wi-fi signal keeps", "wifi" },
            new[] { "site", "A site or app won't open", "site", "open load loading reach website site page app won't wont cannot can't error", "open-website" },
            new[] { "vpn", "Trouble on VPN or the work network", "vpn", "vpn work office corporate remote", "mtu" },
            new[] { "trade", "Broker or trading app lags", "slow", "broker trade trading order orders quotes stock", "inv-connection" },
            new[] { "pc", "My PC is slow", "pc", "pc computer laptop freeze frozen hot fan cpu memory", "background-chatter" },
            new[] { "full", "Check everything", "full", "everything full checkup check all whole", "apps-internet" } };
        static readonly char[] Sep = { ' ', ',', '.', '!', '?', '-', '/' };
        // the best symptom for what someone typed, or null: at least one meaningful word must hit, and plain tool names
        // ("mtu", "dig") don't open a case on their own
        public static string[] Match(string q)
        {
            // bug-hunt M-23: two-letter words count ("pc"), and one word that IS a case's keyword ("wifi", "vpn", "lag") opens it
            var words = (q ?? "").ToLowerInvariant().Split(Sep, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 2).ToList(); if (words.Count == 0) return null;
            string[] best = null; int bs = 0; bool bestExact = false;
            foreach (var s in Symptoms)
            {
                var keys = s[3].Split(' '); int sc = 0; bool exact = false;
                foreach (var w in words) { bool ex = keys.Any(k => k == w); if (ex || (w.Length >= 4 && keys.Any(k => k.StartsWith(w, StringComparison.Ordinal)))) { sc += w.Length >= 5 ? 2 : 1; exact |= ex; } }
                if (sc > bs) { bs = sc; best = s; bestExact = exact; }
            }
            return bs >= 2 || (bs >= 1 && words.Count == 1 && (words[0].Length >= 5 || bestExact)) ? best : null;
        }
        public static string[] Of(string id) { return Symptoms.FirstOrDefault(s => s[0] == id) ?? Symptoms[3]; }

        public sealed class Case
        {
            public string Symptom = "slow", Source = "search", Context = "", App = ""; public DateTime From, To, Opened = DateTime.Now; public GuideRun Run;
            public string Title { get { return L.T(Of(Symptom)[1]); } }
            public string Flow { get { return Of(Symptom)[2]; } }
            public string Topic { get { return Of(Symptom)[4]; } }
        }
        static readonly List<Case> recent = new List<Case>(); static readonly object k = new object();
        public static Case New(string symptom, string source, string context, DateTime? from, DateTime? to)
        {
            var now = DateTime.Now; var c = new Case { Symptom = Of(symptom)[0], Source = source ?? "search", Context = context ?? "", From = from ?? now.AddMinutes(-30), To = to ?? now };
            lock (k) { recent.Insert(0, c); if (recent.Count > 8) recent.RemoveAt(recent.Count - 1); }
            return c;
        }
        public static List<Case> Recent() { lock (k) return recent.ToList(); }
        public static string SourceName(string s)
        {
            switch (s)
            {
                case "home": return "Home"; case "week": return "Your week"; case "stream": return "Streaming"; case "trade": return "Trading"; case "game": return "Gamer Zone";
                case "tool": return "a Toolbox result"; case "replay": return "the Time machine"; case "widget": return "the corner widget"; default: return "the search box";
            }
        }
        // how sure the finding is, 1-4: the level, how many observations back it, whether they were measured, and what's left open
        public static int Sure(GuideRun r)
        {
            if (r == null || !r.Done) return 0; var o = r.ObservedCopy();
            if (r.Lv == 0) return o.Count >= 3 ? 3 : 2;
            if (r.Lv == 3 || r.Lv < 0) return 1;
            int back = o.Count(z => z[1] == "1" || z[1] == "2"), measured = o.Count(z => (z[1] == "1" || z[1] == "2") && z[0] == "m");
            int s = back >= 3 ? 3 : back >= 1 ? 2 : 1; if (measured >= 2 && r.Lv == 2) s++; if (r.NotEst.Count >= 3) s--;
            return Math.Max(1, Math.Min(4, s));
        }
        public static string SureWords(int s) { return s >= 4 ? "very sure" : s == 3 ? "fairly sure" : s == 2 ? "likely" : s == 1 ? "a first guess" : ""; }

        // the minutes AdeeboMeter kept for the case's time (History): router ms, internet ms, Wi-Fi %, loss % - per minute
        public sealed class Series { public List<DateTime> T = new List<DateTime>(); public List<double> Gw = new List<double>(), Net = new List<double>(), Wifi = new List<double>(), Loss = new List<double>(); }
        public static Series Minutes(DateTime from, DateTime to)
        {
            var s = new Series(); if (!Cfg.History || to <= from) return s;
            if ((to - from).TotalDays > 2) from = to.AddDays(-2);   // bug-hunt ML-166: the chart shows the minutes around the case - two days of files at most, never a month
            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                History.Day day; try { day = History.Load(d); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("case history: " + e.Message); continue; }
                if (day == null) continue;
                foreach (var row in day.M)
                {
                    var t = d.AddSeconds(row[0]); if (t < from || t > to || row.Length < 12) continue;
                    s.T.Add(t); s.Gw.Add(row[5]); s.Net.Add(row[6]); s.Wifi.Add(row[7]); s.Loss.Add(row[11]);
                }
            }
            return s;
        }
    }
}
