// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== Examples
    // Every tool and every empty page can show what its result looks like before you run it - made-up data, clearly
    // marked, nothing sent. They live in knowledge/examples.txt (plain text anyone can read and improve):
    //   == toolid
    //   in: first box|second box          (optional - what "Fill the boxes" puts in)
    //   lv|name|value|advice|evidence     (one result row: lv -1 heading, 0 ok, 1 note, 2 problem, 3 info)
    // Addresses are documentation ones (192.0.2.x, 198.51.100.x, 203.0.113.x, 2001:db8::, example.com) - never a real network.
    static class Examples
    {
        public sealed class Ex { public string Id = ""; public string[] In = Array.Empty<string>(); public readonly List<string[]> Rows = new List<string[]>(); }
        static Dictionary<string, Ex> map; static readonly object k = new object();
        public const string Mark = "EXAMPLE";

        public static Dictionary<string, Ex> All()
        {
            lock (k) { if (map == null) map = Parse(Knowledge.RawText("examples")); return map; }
        }
        public static Ex Get(string id) { Ex e; return id != null && All().TryGetValue(id, out e) ? e : null; }

        public static Dictionary<string, Ex> Parse(string text)
        {
            var d = new Dictionary<string, Ex>(StringComparer.Ordinal); Ex cur = null;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                string l = raw.TrimEnd('\r');
                if (l.Trim() == "" || l.StartsWith("#", StringComparison.Ordinal)) continue;
                if (l.StartsWith("== ", StringComparison.Ordinal)) { cur = new Ex { Id = l.Substring(3).Trim() }; d[cur.Id] = cur; continue; }
                if (cur == null) continue;
                if (l.StartsWith("in: ", StringComparison.Ordinal)) { cur.In = l.Substring(4).Split('|').Select(s => s.Trim()).ToArray(); continue; }
                var p = l.Split('|'); if (p.Length < 3) continue;
                cur.Rows.Add(new[] { p[0].Trim(), p[1].Trim(), p[2].Trim(), p.Length > 3 ? p[3].Trim() : "", p.Length > 4 ? p[4].Trim() : "" });
            }
            return d;
        }

        // the rows shown: a banner first (with "Fill the boxes" when there are inputs), then the made-up result
        public static List<string[]> Rows(Ex e, bool canFill)
        {
            var r = new List<string[]>
            {
                new[] { "1", Mark, L.T("Made-up data - not your PC or network. Nothing was sent."), canFill && e.In.Length > 0 ? L.T("Click here to fill the boxes with these inputs, then press Run to try it for real.") : L.T("Press Run to see your own."), "", canFill && e.In.Length > 0 ? "exfill:" + e.Id : "" }
            };
            r.AddRange(e.Rows.Select(x => (string[])x.Clone()));
            return r;
        }
        public static bool IsExample(List<string[]> rows) { return rows != null && rows.Count > 0 && rows[0].Length > 1 && rows[0][1] == Mark; }
    }
}
