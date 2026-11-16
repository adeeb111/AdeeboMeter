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
            new[] { "coder", "Coder" }, new[] { "genz", "Gen-Z" },
            new[] { "stoic", "Philosopher: Marcus Aurelius (Stoic)" }, new[] { "aristotle", "Philosopher: Aristotle" }, new[] { "tao", "Philosopher: Taoist (Laozi)" }, new[] { "confucius", "Philosopher: Confucius" } };
        public const int Featured = 8;
        public static readonly string[] Chatty = { "face", "quiet", "normal", "chatty" };
        public static readonly string[] ChattyNames = { "Face only", "Quiet", "Normal", "Chatty" };
        public static readonly string[] Addrs = { "neutral", "sir", "maam", "name" };
        public static readonly string[] AddrNames = { "Neutral (boss)", "Sir", "Ma'am", "My name" };

        // ---------- words of comfort (opt-in, PLAN 11.7d): a verse from a public-domain translation, word for word, with its
        // reference. Only in calm moments - a greeting or a quiet stretch - never in Roast, never next to a problem, never
        // in Surprise me, and never chosen for you.
        // "common": no faith named - short lines on what the traditions share (patience, kindness, gratitude), in plain words,
        // never presented as a quote from any scripture
        public static readonly string[] Faiths = { "off", "common", "islam", "christianity", "judaism", "hinduism", "buddhism", "sikhism", "stoic", "aristotle", "tao", "confucius" };
        public static readonly string[] FaithNames = { "Off", "Common to all", "Islam", "Christianity", "Judaism", "Hinduism", "Buddhism", "Sikhism", "Marcus Aurelius", "Aristotle", "Taoism", "Confucius" };
        public static volatile string Faith = "off", LastFaith = "common";   // LastFaith: what turning comfort back on brings back, across restarts (AM-CG-184)
        static List<string[]> comfort;
        public static List<string[]> Comforts()
        {
            lock (k)
            {
                if (comfort != null) return comfort;
                comfort = new List<string[]>();
                foreach (var l0 in Knowledge.RawText("comfort").Split('\n')) { var l = l0.Trim(); if (l == "" || l[0] == '#') continue; var p = l.Split('|'); if (p.Length == 3) comfort.Add(new[] { p[0].Trim(), p[1].Trim(), p[2].Trim() }); }
                return comfort;
            }
        }
        // one verse a day for the chosen faith ("" when off, in Roast or Plain)
        public static string Comfort()
        {
            if (Faith == "off" || Voice.Serious || Mood == "roast") return "";
            var l = Comforts().Where(c => c[0] == Faith).ToList(); if (l.Count == 0) return "";
            var c0 = l[DateTime.Today.DayOfYear % l.Count];
            return Faith == "common" ? c0[1] + "  - " + c0[2] : "\u201C" + c0[1] + "\u201D  - " + c0[2];   // our own words get no quotation marks
        }

        // ---------- settings (Cfg reads and writes these; volatile: the Monitor thread draws the widget line)
        public static volatile string Mood = "chill", Persona = "surprise", Addr = "neutral", Name = "", Chat = "normal";

        public static bool PersonaKnown(string id) { return id == "surprise" || id == "none" || Personas.Any(p => p[0] == id); }
        public static string PersonaName(string id) { if (id == "surprise") return "Surprise me"; if (id == "none") return "None (the mood talks)"; var p = Personas.FirstOrDefault(z => z[0] == id); return p != null ? p[1] : id; }
        public static string MoodName(string id) { int i = Array.IndexOf(Moods, id); return i >= 0 ? MoodNames[i] : id; }

        // Surprise me: one persona a day, the same all day (not a new one every line - that feels chaotic). Only personas
        // with their own file; never Plain, never a faith.
        static List<string> haveIds; static string todayId; static DateTime todayFor;
        public static string Today()
        {
            lock (k) if (todayId != null && todayFor == DateTime.Today) return todayId;
            // AM-CG-186: whether a pack exists is checked once (its text, not a parse that the small cache would evict again)
            if (haveIds == null) haveIds = Personas.Where(p => Knowledge.RawText("lines-" + p[0]) != "").Select(p => p[0]).ToList();
            string pick = "none";
            if (haveIds.Count > 0) { int seed = DateTime.Today.Year * 400 + DateTime.Today.DayOfYear; pick = haveIds[(int)((uint)(seed * 2654435761u) % (uint)haveIds.Count)]; }
            lock (k) { todayId = pick; todayFor = DateTime.Today; }
            return pick;
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
            string own = OwnFor(key, v != null, who); if (own != null) return Fill(own, v, who);
            foreach (var id in Chain(who))
            {
                var p = Pack(id); List<string> l; if (p == null || !p.TryGetValue(key, out l)) continue;
                var ok = l.Where(t => v != null || t.IndexOf("{v}", StringComparison.Ordinal) < 0).Distinct().ToList(); if (ok.Count == 0) continue;
                return Fill(Pick(key, ok), v, id);
            }
            return "";
        }
        // a random one, never the one shown last time - and never an empty choice (AM-CG-172: two identical variants)
        static string Pick(string key, List<string> ok)
        {
            lock (k) { string prev; last.TryGetValue(key, out prev); var fresh = ok.Where(t => t != prev).ToList(); if (fresh.Count == 0) fresh = ok; var pick = fresh[rnd.Next(fresh.Count)]; last[key] = pick; return pick; }
        }
        // your line, marked as yours (AM-CG-176: never styled like a measured verdict), for the voice that's talking now
        public const string MineMark = "✎ ";
        static string OwnFor(string key, bool haveV, string who)
        {
            if (who == "plain" || who != Who()) return null;   // AM-CG-183: not in another voice's preview, not in the neutral voice
            string own = MineLine(key, haveV); return own == null ? null : MineMark + own;
        }
        // the same line for a few minutes for a state that lasts (the header, the widget)
        public static string Steady(string key, string v) { return SteadyAs(key, v, Who()); }
        // the same, in a given voice (Trading speaks Plain when a faith is chosen: money is no joke there)
        public static string SteadyAs(string key, string v, string who)
        {
            string sk = who + "|" + key + "|" + (v != null ? "v" : "-"); KeyValuePair<DateTime, string> s;   // AM-CG-171: "with a number" and "without" cached apart
            lock (k) if (sticky.TryGetValue(sk, out s) && (DateTime.Now - s.Key).TotalMinutes < 5 && (v != null || s.Value.IndexOf("{v}", StringComparison.Ordinal) < 0)) return Fill(s.Value, v, who);
            string raw = RawLine(key, v != null, who); if (raw == "") return "";
            lock (k) { if (sticky.Count > 40) sticky.Clear(); sticky[sk] = new KeyValuePair<DateTime, string>(DateTime.Now, raw); }
            return Fill(raw, v, who);
        }
        static string RawLine(string key, bool haveV, string who)
        {
            string own = OwnFor(key, haveV, who); if (own != null) return own;
            foreach (var id in Chain(who))
            {
                var p = Pack(id); List<string> l; if (p == null || !p.TryGetValue(key, out l)) continue;
                var ok = l.Where(t => haveV || t.IndexOf("{v}", StringComparison.Ordinal) < 0).Distinct().ToList(); if (ok.Count == 0) continue;
                return Pick(key, ok);
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
            "quiz.right", "quiz.wrong", "game.streak", "game.timeup", "market.open", "storm.near", "mtr.clean", "mtr.lossy", "mtu.small", "points.earned", "port.closed", "port.open", "portal", "sec.issue", "sec.ok", "speed.bloat", "speed.great", "update.suspect", "vpn.on", "vpn.slow", "stream.on", "stream.dip", "stream.none", "view.switch", "trade.ok", "trade.slow", "trade.closed" };
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

        // ---------- your own lines (PLAN 11.7c): on top of the built-ins, in %LOCALAPPDATA%\AdeeboMeter\lines\mine.txt
        // (DataDir), surviving updates. Imported text is untrusted: shown as text only, control and direction-override
        // characters removed, only {addr} and {v} filled. Limits: 1 MB, 600 lines, 200 characters a line, 2 s.
        static string MinePath { get { return System.IO.Path.Combine(System.IO.Path.Combine(Program.DataDir, "lines"), "mine.txt"); } }
        static Dictionary<string, List<string>> mine; static bool mineRead;
        static Dictionary<string, List<string>> Mine()
        {
            lock (k)
            {
                if (mineRead) return mine; mineRead = true;
                try { var f = new System.IO.FileInfo(MinePath); if (f.Exists && f.Length <= 1048576) mine = Parse(System.IO.File.ReadAllText(MinePath, System.Text.Encoding.UTF8)); }
                catch (System.IO.IOException) { } catch (UnauthorizedAccessException) { }
                return mine;
            }
        }
        public static int MineCount { get { var m = Mine(); return m == null ? 0 : m.Values.Sum(l => l.Count); } }
        // a line of yours for this key, if you wrote one (picked like the others)
        static string MineLine(string key, bool haveV)
        {
            var m = Mine(); List<string> l; if (m == null || !m.TryGetValue(key, out l)) return null;
            var ok = l.Where(t => haveV || t.IndexOf("{v}", StringComparison.Ordinal) < 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); if (ok.Count == 0) return null;
            return Pick("mine." + key, ok);   // AM-CG-177: no immediate repeats, duplicates don't weigh the pick
        }
        static readonly Regex Bidi = new Regex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F‎‏‪-‮⁦-⁩]", RegexOptions.CultureInvariant);
        // check a file and fix what can be fixed; returns what happened, line by line, never silently
        public static List<string> Import(string text, out List<string> kept)
        {
            var report = new List<string>(); kept = new List<string>(); var sw = System.Diagnostics.Stopwatch.StartNew();
            var rows = (text ?? "").Replace("\r", "").Split('\n');
            int real = rows.Count(r => r.Trim() != "" && !r.TrimStart().StartsWith("#", StringComparison.Ordinal));
            if (real > 600) { report.Add("Refused: " + real + " lines - the limit is 600 a file."); return report; }
            int n = 0;
            foreach (var r0 in rows)
            {
                n++; if (sw.ElapsedMilliseconds > 2000) { report.Add("Stopped at line " + n + ": took longer than 2 seconds."); break; }
                string r = Bidi.Replace(r0, "").Trim(); if (r.Length == 0 || r[0] == '#') continue;
                if (r.Length > 200) { report.Add("Line " + n + " refused: over 200 characters."); continue; }
                // wrong separator: a tab, a semicolon or " - " after the key -> "|"
                var sep = Regex.Match(r, @"^([a-z]+\.?[a-z]*)\s*(\||\t|;|:| - )\s*(.+)$", RegexOptions.CultureInvariant);
                if (!sep.Success) { report.Add("Line " + n + " refused: write it as key|text, e.g. all.good|All calm."); continue; }
                string key = sep.Groups[1].Value, t = sep.Groups[3].Value.Trim();
                if (sep.Groups[2].Value.Trim() != "|") report.Add("Line " + n + " fixed: separator changed to |.");
                if (t.IndexOf('|') > 0) { var parts = t.Split('|'); if (Array.IndexOf(Moods, parts[0].Trim()) >= 0 || Personas.Any(p => p[0] == parts[0].Trim())) { t = string.Join("|", parts.Skip(1)).Trim(); report.Add("Line " + n + " fixed: the mood '" + parts[0].Trim() + "' was dropped - kept: " + t); } }   // "key|mood|text": the mood is dropped - your lines are for every mood
                if (Array.IndexOf(Keys, key) < 0)
                {
                    string near = Keys.OrderBy(kk => EditDist(kk, key)).First();
                    if (EditDist(near, key) <= 3) { report.Add("Line " + n + " fixed: key '" + key + "' -> '" + near + "'."); key = near; }
                    else { report.Add("Line " + n + " refused: unknown key '" + key + "'."); continue; }
                }
                t = Regex.Replace(t, @"\{([^}]*)\}", m =>
                {
                    string ph = m.Groups[1].Value.Trim().ToLowerInvariant(); if (ph == "v" || ph == "addr") return "{" + (m.Groups[1].Value.Trim() == "Addr" ? "Addr" : ph) + "}";
                    if (ph.Length >= 3 && EditDist(ph, "addr") == 1) { report.Add("Line " + n + " fixed: {" + m.Groups[1].Value + "} -> {addr} (one letter off)."); return "{addr}"; }
                    report.Add("Line " + n + ": {" + m.Groups[1].Value + "} isn't a placeholder ({v} or {addr}) - kept as plain text, nothing filled in."); return "(" + m.Groups[1].Value + ")";
                });
                var words = t.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 16) { t = string.Join(" ", words.Take(16)).TrimEnd(',', ';', ':') + "..."; report.Add("Line " + n + " fixed: shortened to 16 words."); }
                if (kept.Contains(key + "|" + t, StringComparer.OrdinalIgnoreCase)) { report.Add("Line " + n + " skipped: the same line is already in this file."); continue; }   // AM-CG-177
                kept.Add(key + "|" + t);
            }
            report.Insert(0, kept.Count + " line" + (kept.Count == 1 ? "" : "s") + " ready.");
            return report;
        }
        public static int LastDropped;   // how many of the oldest lines the last save had to let go (over 600)
        public static bool SaveMine(List<string> lines, bool append)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MinePath));
                var all = new List<string>(); if (append && System.IO.File.Exists(MinePath)) all.AddRange(System.IO.File.ReadAllLines(MinePath, System.Text.Encoding.UTF8).Where(l => l.Trim() != "" && !l.StartsWith("#", StringComparison.Ordinal)));
                all.AddRange(lines); all = all.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                LastDropped = Math.Max(0, all.Count - 600); if (LastDropped > 0) all = all.Skip(LastDropped).ToList();
                FullView.WriteSafe(MinePath, "# My AdeeboMeter lines: key|text. {addr} = how I like to be called, {v} = the measured value.\n" + string.Join("\n", all) + "\n", false);
                lock (k) { mineRead = false; mine = null; }
                return true;
            }
            catch (System.IO.IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
        }
        public static string MineText() { try { return System.IO.File.Exists(MinePath) ? System.IO.File.ReadAllText(MinePath, System.Text.Encoding.UTF8) : ""; } catch (System.IO.IOException) { return ""; } catch (UnauthorizedAccessException) { return ""; } }
        // Clear moves the file aside (mine.undo) so Undo can bring it back; false = Windows wouldn't let it (AM-CG-185)
        public static bool ClearMine()
        {
            bool ok = true;
            try { string u = MinePath + ".undo"; if (System.IO.File.Exists(MinePath)) { if (System.IO.File.Exists(u)) System.IO.File.Delete(u); System.IO.File.Move(MinePath, u); } }
            catch (System.IO.IOException) { ok = false; } catch (UnauthorizedAccessException) { ok = false; }
            lock (k) { mineRead = false; mine = null; } return ok;
        }
        public static bool CanUndoClear { get { return System.IO.File.Exists(MinePath + ".undo") && !System.IO.File.Exists(MinePath); } }
        public static bool UndoClear()
        {
            try { System.IO.File.Move(MinePath + ".undo", MinePath); lock (k) { mineRead = false; mine = null; } return true; }
            catch (System.IO.IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
        }
        static int EditDist(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i; for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++) for (int j = 1; j <= b.Length; j++) d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[a.Length, b.Length];
        }
    }
}
