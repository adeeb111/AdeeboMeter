// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== Languages
    // Each language is one table packed in the exe (knowledge/lang-<code>.txt): the English text, a tab, the translation.
    // L.T(english) returns the translation, or the English when there's none - so a half-done table never breaks a screen,
    // it only leaves English where it isn't translated yet. English is the source; nothing is ever fetched from the internet.
    static class L
    {
        public static readonly string[][] Languages = {
            new[] { "en", "English" }, new[] { "fr", "Français (Canada)" },
            new[] { "es", "Español" }, new[] { "ar", "العربية" }, new[] { "bn", "বাংলা" }   // Chinese (lang-zh.txt, 4% done) is kept in the repo but not offered until it is translated - a saved "zh" opens in English
        };
        public static bool IsRtl { get { return Code == "ar"; } }

        // The first strong letter sets the base direction for a mixed-script label.
        // English fallback and protocol-first text keep their left-to-right order.
        public static bool RunsRtl(string text)
        {
            foreach (char c in text ?? "")
                if (char.IsLetter(c)) return (c >= '\u0600' && c <= '\u08ff');
            return false;
        }
        static Dictionary<string, string> map;   // null = English
        public static string Code = "en";
        public static void Use(string code)
        {
            Code = Languages.Any(l => l[0] == code) ? code : "en"; map = null;
            if (Code == "en") return;
            try { map = Parse(Knowledge.RawText("lang-" + Code)); }
            catch (Exception e) { if (!(e is InvalidOperationException || e is System.IO.IOException || e is ArgumentException)) throw; Program.Log("language " + Code + ": " + e.Message); Code = "en"; }
        }
        public static List<string> LastDupes = new List<string>();
        public static Dictionary<string, string> Parse(string text)
        {
            // bug-hunt L-315/316: an empty translation is kept (T() still falls back to English, and the self-test can see it), and
            // an English line given twice is noted - the first translation stays
            var d = new Dictionary<string, string>(StringComparer.Ordinal); var dup = new List<string>();
            foreach (var raw in (text ?? "").Split('\n'))
            {
                string line = raw.TrimEnd('\r'); if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t'); if (tab <= 0) continue;
                string k = line.Substring(0, tab); if (d.ContainsKey(k)) { dup.Add(k); continue; }
                d[k] = line.Substring(tab + 1);
            }
            LastDupes = dup;
            return d;
        }
        public static string T(string en)
        {
            var m = map; if (m == null || string.IsNullOrEmpty(en)) return en;
            string v; return m.TryGetValue(en, out v) && v.Trim().Length > 0 ? v : en;
        }
        // another language's word, for the picker's preview (that table read once, kept)
        static readonly Dictionary<string, Dictionary<string, string>> others = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        static Dictionary<string, string> Table(string code)
        {
            if (code == "en" || !Languages.Any(l => l[0] == code)) return null;
            lock (others)
            {
                Dictionary<string, string> d; if (others.TryGetValue(code, out d)) return d;
                try { d = Parse(Knowledge.RawText("lang-" + code)); } catch (Exception e) { if (!(e is InvalidOperationException || e is System.IO.IOException || e is ArgumentException)) throw; d = new Dictionary<string, string>(); }
                others[code] = d; return d;
            }
        }
        public static string In(string code, string en) { var m = Table(code); string v; return m != null && en != null && m.TryGetValue(en, out v) && v.Trim().Length > 0 ? v : en; }
        // how much of the app is translated - the "#coverage NN" line tools/langcheck.py keeps in the table; -1 when it isn't there
        public static int Coverage(string code)
        {
            if (code == "en") return 100;
            try
            {
                foreach (var line in (Knowledge.RawText("lang-" + code) ?? "").Split('\n'))
                {
                    if (!line.StartsWith("#coverage ", StringComparison.Ordinal)) { if (line.Length > 0 && line[0] != '#') break; continue; }
                    int p; return int.TryParse(line.Substring(10).Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out p) ? Math.Max(0, Math.Min(100, p)) : -1;
                }
            }
            catch (Exception e) { if (!(e is InvalidOperationException || e is System.IO.IOException || e is ArgumentException)) throw; }
            return -1;
        }
        public static int Count { get { var m = map; return m == null ? 0 : m.Count; } }
    }
}
