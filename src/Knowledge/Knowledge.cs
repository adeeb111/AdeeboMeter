// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AdeeboMeter
{
    // ================================================================== the knowledge pack
    // Everything AdeeboMeter explains (programs, terms, protocols, Windows' insides, Teams and Office) is plain text in
    // knowledge/*.txt in the source. The build packs each file compressed (gzip) inside the exe. Nothing is unpacked until
    // a page or a hover card asks for it, and what's unpacked is held only weakly - when no window is using it, the memory
    // goes back to Windows.
    //
    // Format (one block per entry):   == key|other key
    //                                 field: text
    //                                 field: text
    static class Knowledge
    {
        public sealed class Rec
        {
            public string[] Keys; readonly Dictionary<string, string> f = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string this[string k] { get { string v; return f.TryGetValue(k, out v) ? v : ""; } set { f[k] = value; } }
            public string Id { get { return Keys[0]; } }
        }

        static readonly Dictionary<string, WeakReference> cache = new Dictionary<string, WeakReference>();
        static readonly object k = new object();
        // While the engineer view is open, keep what it unpacked (a weak reference alone was dropped at the next garbage
        // collection, so hovering re-unpacked the glossary again and again). Released when the view closes.
        static readonly Dictionary<string, object> held = new Dictionary<string, object>(); static volatile bool hold;
        public static bool Hold { get { return hold; } set { lock (k) { hold = value; if (!value) held.Clear(); } } }

        public static string RawText(string pack)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream("AdeeboMeter.knowledge." + pack + ".gz"))
            {
                if (s != null)
                    using (var z = new GZipStream(s, CompressionMode.Decompress))
                    using (var r = new StreamReader(z, Encoding.UTF8)) return r.ReadToEnd();
            }
            // a developer build without the packed resources: read the text files next to the exe or in the source tree
            foreach (var dir in new[] { Path.Combine(Program.Dir ?? "", "knowledge"), Path.Combine(Program.Dir ?? "", "..", "knowledge") })
            {
                string f = Path.Combine(dir, pack + ".txt");
                if (File.Exists(f)) return File.ReadAllText(f, Encoding.UTF8);
            }
            return "";
        }

        public static List<Rec> Parse(string text)
        {
            var list = new List<Rec>(); Rec cur = null;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.StartsWith("== ", StringComparison.Ordinal)) { cur = new Rec { Keys = line.Substring(3).Split('|').Select(x => x.Trim()).Where(x => x != "").ToArray() }; if (cur.Keys.Length > 0) list.Add(cur); continue; }
                if (cur == null || line.Length == 0 || line[0] == '#') continue;
                int c = line.IndexOf(": ", StringComparison.Ordinal);
                if (c > 0) cur[line.Substring(0, c).Trim()] = line.Substring(c + 2);
                else if (line.StartsWith("  ", StringComparison.Ordinal)) { /* continuation */ }
            }
            return list;
        }

        static T Cached<T>(string key, Func<T> build) where T : class
        {
            lock (k)
            {
                WeakReference w; T v;
                if (cache.TryGetValue(key, out w) && (v = w.Target as T) != null) { if (Hold) held[key] = v; return v; }
                v = build(); cache[key] = new WeakReference(v); if (Hold) held[key] = v; return v;
            }
        }

        // The program guide and the glossary as Explain entries, keyed by every name they answer to.
        public static Dictionary<string, Explain.Entry> Guide(string pack)
        {
            return Cached("guide:" + pack, delegate
            {
                var d = new Dictionary<string, Explain.Entry>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in Parse(RawText(pack)))
                {
                    int close; if (!int.TryParse(r["close"], NumberStyles.Integer, CultureInfo.InvariantCulture, out close)) close = -1;
                    var e = new Explain.Entry { Friendly = r["friendly"], Cat = r["cat"], Close = close, Tag = r["tag"], What = r["what"], How = r["how"], Why = r["why"], Without = r["without"] };
                    foreach (var key in r.Keys) d[key] = e;
                }
                return d;
            });
        }

        // Learn mode, the deep dives and the app stories: a list in file order.
        public static List<Rec> Learn()
        {
            return Cached("learn", delegate { return Parse(RawText("learn")); });
        }
        public static Rec Topic(string id)
        {
            return Learn().FirstOrDefault(r => r.Keys.Contains(id, StringComparer.OrdinalIgnoreCase));
        }
    }
}
