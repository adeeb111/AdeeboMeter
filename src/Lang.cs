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
        public static readonly string[][] Languages = { new[] { "en", "English" }, new[] { "fr", "Français (aperçu)" } };
        static Dictionary<string, string> map;   // null = English
        public static string Code = "en";
        public static void Use(string code)
        {
            Code = Languages.Any(l => l[0] == code) ? code : "en"; map = null;
            if (Code == "en") return;
            try { map = Parse(Knowledge.RawText("lang-" + Code)); }
            catch (Exception e) { if (!(e is InvalidOperationException || e is System.IO.IOException || e is ArgumentException)) throw; Program.Log("language " + Code + ": " + e.Message); Code = "en"; }
        }
        public static Dictionary<string, string> Parse(string text)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var raw in (text ?? "").Split('\n'))
            {
                string line = raw.TrimEnd('\r'); if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t'); if (tab <= 0 || tab == line.Length - 1) continue;
                d[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
            return d;
        }
        public static string T(string en)
        {
            var m = map; if (m == null || string.IsNullOrEmpty(en)) return en;
            string v; return m.TryGetValue(en, out v) ? v : en;
        }
        public static int Count { get { var m = map; return m == null ? 0 : m.Count; } }
    }
}
