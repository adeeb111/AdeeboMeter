// The companion's expressions (Adeeb, 21 Nov: "expressions in our emoji ... change based on the output and run").
// The face keeps showing the network's mood; for a few seconds it reacts to what just happened - a tool starting and
// finishing, an outage beginning or ending, points earned, waking up - then settles back. Always the round face.
using System;
using System.Collections.Generic;

namespace AdeeboMeter
{
    static class BuddyReact
    {
        static readonly object k = new object();
        static string mood; static DateTime until = DateTime.MinValue;
        static int lastLv = -1; static DateTime redSince = DateTime.MinValue;
        static DateTime outDay = DateTime.MinValue; static int outsToday;

        // every face it can make, drawn by Draw.Buddy (the self-test draws each one)
        public static readonly string[] All = { "smug", "happy", "sparkle", "love", "blush", "sweaty", "worried", "dizzy", "sad", "tears", "gloom", "betrayed", "angry", "shout", "frustrated", "speechless", "asleep" };

        public static void Set(string m, double seconds)
        {
            if (Voice.Serious) return;   // the calm view keeps the plain face
            lock (k) { mood = m; until = DateTime.UtcNow.AddSeconds(seconds); }
        }
        public static void Clear(string m) { lock (k) if (mood == m) until = DateTime.MinValue; }

        // what the face shows right now instead of the network's mood (null: nothing to react to)
        public static string Now(int level)
        {
            lock (k)
            {
                Feed(level);
                if (DateTime.UtcNow < until) return mood;
                if (level == 2 && redSince != DateTime.MinValue && (DateTime.UtcNow - redSince).TotalMinutes >= 10 && !Voice.Serious) return "gloom";   // a long outage
                return null;
            }
        }
        // the network's level, seen every time the face is drawn or checked: an outage starting or ending is a reaction
        static void Feed(int level)
        {
            if (level < 0 || level > 2) return;
            if (lastLv >= 0 && level == 2 && lastLv < 2)
            {
                redSince = DateTime.UtcNow;
                if (outDay != DateTime.Today) { outDay = DateTime.Today; outsToday = 0; }
                outsToday++;
                if (!Voice.Serious) { mood = outsToday >= 3 ? "frustrated" : "shout"; until = DateTime.UtcNow.AddSeconds(8); }   // the third time today: frustrated
            }
            else if (lastLv == 2 && level == 0)
            {
                redSince = DateTime.MinValue;
                if (!Voice.Serious) { mood = "love"; until = DateTime.UtcNow.AddSeconds(10); }   // back up
            }
            else if (level < 2) redSince = DateTime.MinValue;
            lastLv = level;
        }

        // a finished tool: its worst row decides
        public static void ToolDone(List<string[]> rows, bool stopped)
        {
            if (stopped) { Set("smug", 0); return; }
            if (rows == null || rows.Count == 0) { Set("speechless", 6); return; }
            int worst = 0; foreach (var r in rows) { int lv; if (r.Length > 0 && int.TryParse(r[0], out lv) && lv > worst && lv <= 2) worst = lv; }
            Set(worst == 2 ? "tears" : worst == 1 ? "worried" : "happy", worst == 2 ? 8 : 6);
        }
    }
}
