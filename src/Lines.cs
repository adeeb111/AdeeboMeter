// AdeeboMeter - part of the source; see src/Program.cs for the overview.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // The buddy's words (PLAN 11.7 / 11.7a / 11.7c / 11.7d). The code never holds a buddy sentence: it asks for a key
    // ("wifi.drops") and gets a line in the current persona or mood. Words only - the data, numbers, Details rows,
    // reports and anything copied out never change and stay Plain.
    //
    // Who talks: the persona (Surprise me picks one a day; None lets the mood talk), else the mood, else Chill, else Plain.
    // A key nobody wrote falls back down that chain - never blank. Several lines per key: random, never the same twice in a
    // row, and the same line stays for a few minutes (a header that changes its joke every second is noise).
    // Files: knowledge\lines-<id>.txt, one "key|text" a line, embedded in the exe like the rest of the knowledge.
    // Only the files in use are kept in memory: the persona, the mood, Chill and Plain.
    static class Lines
    {
        public static readonly string[] Moods = { "chill", "roast", "hype", "support", "bossy", "emo", "plain" };
        public static readonly string[] MoodNames = { "Chill", "Roast", "Hype", "Support", "Bossy", "Emo", "Plain" };
        // personas: the featured ones first (shown in the picker), the rest under "More personas"
        public static readonly string[][] Personas = {
            new[] { "neteng", "Network engineer" }, new[] { "gamer", "Gamer" }, new[] { "bestie", "Bestie" }, new[] { "bro", "Bro code" },
            new[] { "epic-fantasy", "Epic fantasy" }, new[] { "space-saga", "Space saga" }, new[] { "pirate", "Pirate" }, new[] { "noir", "Noir detective" },
            new[] { "nature", "Nature documentary" }, new[] { "shakespeare", "Shakespeare" }, new[] { "dark-ages", "Dark Ages" }, new[] { "industrial", "Industrial" },
            new[] { "revolution", "Revolution" }, new[] { "mission", "Mission control" }, new[] { "poetic", "Poetic" }, new[] { "scientist", "Scientist" },
            new[] { "coder", "Coder" }, new[] { "genz", "Gen-Z" } };
        public const int Featured = 8;
        public static readonly string[] Chatty = { "face", "quiet", "normal", "chatty" };
        public static readonly string[] ChattyNames = { "Face only", "Quiet", "Normal", "Chatty" };
        public static readonly string[] Addrs = { "neutral", "sir", "maam", "name" };
        public static readonly string[] AddrNames = { "Neutral (boss)", "Sir", "Ma'am", "My name" };

        // ---------- settings (Cfg reads and writes these; volatile: the Monitor thread draws the widget line)
        public static volatile string Mood = "chill", Persona = "surprise", Addr = "neutral", Name = "", Chat = "normal";

        public static bool PersonaKnown(string id) { return id == "surprise" || id == "none" || Personas.Any(p => p[0] == id); }
        public static string PersonaName(string id) { if (id == "surprise") return "Surprise me"; if (id == "none") return "None (the mood talks)"; var p = Personas.FirstOrDefault(z => z[0] == id); return p != null ? p[1] : id; }
        public static string MoodName(string id) { int i = Array.IndexOf(Moods, id); return i >= 0 ? MoodNames[i] : id; }

        // Surprise me: one persona a day, the same all day (not a new one every line - that feels chaotic). Only personas
        // with their own file; never Plain, never a faith.
        public static string Today()
        {
            var have = Personas.Where(p => Has(p[0])).Select(p => p[0]).ToList(); if (have.Count == 0) return "none";
            int seed = DateTime.Today.Year * 400 + DateTime.Today.DayOfYear;
            return have[(int)((uint)(seed * 2654435761u) % (uint)have.Count)];
        }
        // who talks right now
        public static string Who() { string p = Persona; if (p == "surprise") p = Today(); return p == "none" || !Has(p) ? Mood : p; }

        // ---------- the files
        static readonly Dictionary<string, Dictionary<string, List<string>>> packs = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
        static readonly HashSet<string> missing = new HashSet<string>(StringComparer.Ordinal);
        static readonly object k = new object();
        public static bool Has(string id) { return Pack(id) != null; }
        static Dictionary<string, List<string>> Pack(string id)
        {
            lock (k)
            {
                Dictionary<string, List<string>> p; if (packs.TryGetValue(id, out p)) return p; if (missing.Contains(id)) return null;
                string raw = Knowledge.RawText("lines-" + id); if (raw == "") { missing.Add(id); return null; }
                p = Parse(raw);
                // keep only what's in use (a persona switch drops the old one)
                string who0 = Persona == "surprise" ? null : Persona;
                foreach (var old in packs.Keys.ToList()) if (old != "plain" && old != "chill" && old != Mood && old != who0 && old != id && packs.Count >= 4) packs.Remove(old);
                packs[id] = p; return p;
            }
        }
        public static Dictionary<string, List<string>> Parse(string raw)
        {
            var p = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var l0 in raw.Split('\n'))
            {
                string l = l0.Trim(); if (l.Length == 0 || l[0] == '#') continue;
                int bar = l.IndexOf('|'); if (bar <= 0) continue;
                string key = l.Substring(0, bar).Trim(), text = l.Substring(bar + 1).Trim(); if (key.Length == 0 || text.Length == 0) continue;
                List<string> list; if (!p.TryGetValue(key, out list)) p[key] = list = new List<string>();
                list.Add(text);
            }
            return p;
        }

        // ---------- picking a line
        static readonly Dictionary<string, string> last = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly Dictionary<string, KeyValuePair<DateTime, string>> sticky = new Dictionary<string, KeyValuePair<DateTime, string>>(StringComparer.Ordinal);
        static readonly Random rnd = new Random();
        // a fresh line for this moment (busy, a tool's verdict, a toast): random, never the one shown last time
        public static string Line(string key, string v) { return Line(key, v, Who()); }
        public static string Line(string key, string v, string who)
        {
            foreach (var id in Chain(who))
            {
                var p = Pack(id); List<string> l; if (p == null || !p.TryGetValue(key, out l)) continue;
                var ok = l.Where(t => v != null || t.IndexOf("{v}", StringComparison.Ordinal) < 0).ToList(); if (ok.Count == 0) continue;
                string prev; lock (k) last.TryGetValue(key, out prev);
                string pick;
                lock (k) { var fresh = ok.Count > 1 ? ok.Where(t => t != prev).ToList() : ok; pick = fresh[rnd.Next(fresh.Count)]; last[key] = pick; }
                return Fill(pick, v, id);
            }
            return "";
        }
        // the same line for a few minutes for a state that lasts (the header, the widget)
        public static string Steady(string key, string v)
        {
            string who = Who(), sk = who + "|" + key; KeyValuePair<DateTime, string> s;
            lock (k) if (sticky.TryGetValue(sk, out s) && (DateTime.Now - s.Key).TotalMinutes < 5) return Fill(s.Value, v, who);
            string raw = RawLine(key, v != null, who); if (raw == "") return "";
            lock (k) { if (sticky.Count > 40) sticky.Clear(); sticky[sk] = new KeyValuePair<DateTime, string>(DateTime.Now, raw); }
            return Fill(raw, v, who);
        }
        static string RawLine(string key, bool haveV, string who)
        {
            foreach (var id in Chain(who))
            {
                var p = Pack(id); List<string> l; if (p == null || !p.TryGetValue(key, out l)) continue;
                var ok = l.Where(t => haveV || t.IndexOf("{v}", StringComparison.Ordinal) < 0).ToList(); if (ok.Count == 0) continue;
                string prev; lock (k) { last.TryGetValue(key, out prev); var fresh = ok.Count > 1 ? ok.Where(t => t != prev).ToList() : ok; var pick = fresh[rnd.Next(fresh.Count)]; last[key] = pick; return pick; }
            }
            return "";
        }
        static IEnumerable<string> Chain(string who)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in new[] { who, Mood, "chill", "plain" }) if (!string.IsNullOrEmpty(id) && seen.Add(id)) yield return id;
        }
        static readonly Regex Ph = new Regex(@"\{(addr|Addr|v)\}", RegexOptions.CultureInvariant);
        static string Fill(string t, string v, string who)
        {
            string a = AddrFor(who);
            string r = Ph.Replace(t, m => m.Groups[1].Value == "v" ? (v ?? "") : m.Groups[1].Value == "Addr" ? Cap(a) : a);
            return Cap(r);
        }
        static string Cap(string s) { return string.IsNullOrEmpty(s) || !char.IsLower(s[0]) ? s : char.ToUpper(s[0], CultureInfo.CurrentCulture) + s.Substring(1); }
        // how you like to be called, in the persona's words; never guessed (Neutral is the default)
        public static string AddrFor(string who)
        {
            if (Addr == "name" && !string.IsNullOrWhiteSpace(Name)) return Name.Trim();
            bool epic = who == "epic-fantasy" || who == "shakespeare" || who == "dark-ages";
            switch (Addr)
            {
                case "sir": return epic ? "my lord" : who == "bro" || who == "genz" ? "bro" : who == "pirate" ? "captain" : "sir";
                case "maam": return epic ? "my lady" : who == "bro" ? "sis" : who == "genz" ? "bestie" : who == "pirate" ? "captain" : "ma'am";
                default:
                    switch (who) { case "epic-fantasy": case "shakespeare": case "dark-ages": return "friend"; case "bro": return "bro"; case "pirate": return "matey"; case "gamer": return "player"; case "noir": return "pal"; case "space-saga": return "pilot"; case "mission": return "flight"; case "bestie": case "genz": return "bestie"; default: return "boss"; }
            }
        }

        // ---------- how chatty: which kinds of moment may speak
        // face = none; quiet = problems and greetings; normal = + states and tool verdicts; chatty = + idle and rare lines
        public static bool Speaks(string kind)
        {
            if (Voice.Serious) return false;
            switch (Chat)
            {
                case "face": return false;
                case "quiet": return kind == "problem" || kind == "greet";
                case "chatty": return true;
                default: return kind != "idle";
            }
        }

        // ---------- the self-test's lines check: every built-in line, every file
        public static readonly string[] Keys = { "app.open", "all.good", "pc.busy", "wifi.drops", "net.none", "inet.slow", "busy", "idle", "search.empty", "freeze", "speedrunner", "back.again",
            "award", "call.on", "cert.expired", "cert.ok", "decrypt.done", "devices.found", "dns.slow", "encrypt.done", "game.good", "game.lag", "gw.down", "isp.down", "late.night", "morning",
            "mtr.clean", "mtr.lossy", "mtu.small", "points.earned", "port.closed", "port.open", "portal", "sec.issue", "sec.ok", "speed.bloat", "speed.great", "update.suspect", "vpn.on", "vpn.slow" };
        // banned: dark topics, slurs-adjacent words, trademarked names from the two film-style personas, famous catchphrases
        static readonly Regex Banned = new Regex(@"\b(kill\w*|dead|death|die|dies|died|dying|suicide|murder\w*|blood\w*|war|wars|warfare|gun|guns|bomb\w*|nazi\w*|hitler|rape|corpse\w*|grave\w*|stupid|idiot\w*|dumb|ugly|loser\w*|noob\w*|fuck\w*|shit\w*|damn\w*|hell|crap\w*)\b"
            + @"|winter is coming|lannister|stark|targaryen|westeros|jon snow|khaleesi|iron throne|jedi|sith\b|skywalker|vader|yoda|lightsaber|the force|death star|wookiee", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        public static List<string> Check(string id, string raw)
        {
            var bad = new List<string>(); var p = Parse(raw);
            foreach (var kv in p)
            {
                if (Array.IndexOf(Keys, kv.Key) < 0) bad.Add(id + ": unknown key " + kv.Key);
                foreach (var t in kv.Value)
                {
                    int words = t.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length; if (words > 16) bad.Add(id + "/" + kv.Key + ": " + words + " words");
                    if (t.Length > 110) bad.Add(id + "/" + kv.Key + ": " + t.Length + " characters");
                    if (t.Any(c => char.IsSurrogate(c) || (c >= 0x2600 && c <= 0x27BF))) bad.Add(id + "/" + kv.Key + ": emoji");
                    foreach (Match m in Regex.Matches(t, @"\{[^}]*\}")) if (m.Value != "{v}" && m.Value != "{addr}" && m.Value != "{Addr}") bad.Add(id + "/" + kv.Key + ": placeholder " + m.Value);
                    var bm = Banned.Match(t); if (bm.Success) bad.Add(id + "/" + kv.Key + ": '" + bm.Value + "' in \"" + t + "\"");
                }
            }
            if (id == "plain") foreach (var key in Keys) if (!p.ContainsKey(key)) bad.Add("plain: no line for " + key);
            return bad;
        }
    }
}
