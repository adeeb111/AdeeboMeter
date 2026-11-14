// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // Moods: how AdeeboMeter talks, on top of the facts (never instead of them). Companion is the default; Serious has no
    // jokes at all. The others add one line of personality to the verdict - the verdict itself, the numbers and the fix stay
    // exactly the same. A real problem still says what's wrong first; the mood line comes after it.
    // Also here: seasons (a word or two for Halloween, winter, New Year), the rare koan after a long quiet stretch, and the
    // excuses for your provider that get more creative the longer it stays down.
    static class Mood
    {
        public static readonly string[] Ids = { "companion", "sarcastic", "anxious", "conspiracy", "brutal" };
        public static readonly string[] Names = { "Companion", "Sarcastic engineer", "Anxious network parent", "Conspiracy mode", "Brutally honest" };
        public static volatile string Name = "companion", Isp = "";   // Isp: your provider's name, once the Monitor has learned it
        public static bool On { get { return !Voice.Serious && Name != "companion"; } }

        // which part of the path is worst right now: 0 PC, 1 your network, 2 provider, 3 DNS, 4 internet (-1: all fine)
        static int Worst(Snap s, out int lv)
        {
            lv = 0; int at = -1;
            for (int i = 0; i < 5 && i < s.Path.Length; i++) if (s.Path[i] >= 1 && s.Path[i] <= 2 && s.Path[i] > lv) { lv = s.Path[i]; at = i; }
            return at;
        }
        // the same line for a few minutes, then another - never a new joke every second
        static string Pick(string[] l, int salt) { if (l.Length == 0) return ""; long slot = DateTime.Now.Ticks / TimeSpan.TicksPerMinute / 5; return l[(int)((slot + salt) % l.Length + l.Length) % l.Length]; }

        // the outage and all-green clocks: kept by the Monitor every second (Track), read when a line is drawn. Ticks in longs,
        // swapped with Interlocked - the Monitor thread writes, the UI thread reads.
        static long downTicks, greenTicks; static DateTime koanFrom = DateTime.MinValue; static string koanDone = "";
        static DateTime downSince { get { long t = System.Threading.Interlocked.Read(ref downTicks); return t == 0 ? DateTime.MinValue : new DateTime(t); } }
        static DateTime greenSince { get { long t = System.Threading.Interlocked.Read(ref greenTicks); return t == 0 ? DateTime.MinValue : new DateTime(t); } }
        public static void Track(Snap s)
        {
            int lv, at = Worst(s, out lv);
            bool down = at >= 2 && lv == 2, green = at < 0 && s.Level == 0;
            if (!down) System.Threading.Interlocked.Exchange(ref downTicks, 0); else System.Threading.Interlocked.CompareExchange(ref downTicks, DateTime.Now.Ticks, 0);
            if (!green) System.Threading.Interlocked.Exchange(ref greenTicks, 0); else System.Threading.Interlocked.CompareExchange(ref greenTicks, DateTime.Now.Ticks, 0);
        }
        // one line of personality for the verdict; "" when the mood is Companion or Serious (or there's nothing to add)
        public static string Quip(Snap s) { return Quip(s, Isp); }
        public static string Quip(Snap s, string isp)
        {
            int lv, at = Worst(s, out lv);
            bool down = at >= 2 && lv == 2 && downSince != DateTime.MinValue;
            if (Voice.Serious) return "";
            // the provider's excuses: the longer it lasts, the better the story (every mood, Companion too)
            if (down && (DateTime.Now - downSince).TotalMinutes >= 3) return Excuse((DateTime.Now - downSince).TotalMinutes, isp);
            // 1.1.5: the words come from the lines files (Lines) - the persona if one talks, else the mood
            string key, v = null, kind = "state";
            string comfort = s.Level == 0 && !s.Call ? Lines.Comfort() : "";
            if (comfort != "" && (DateTime.Now - Program.Started < TimeSpan.FromSeconds(60) || (DateTime.Now.Minute < 5 && Lines.Speaks("idle")))) return comfort;   // calm moments only: the greeting, and the first minutes of a quiet hour
            if (DateTime.Now - Program.Started < TimeSpan.FromSeconds(60) && s.Level == 0) { key = DateTime.Now.Hour >= 23 || DateTime.Now.Hour < 5 ? "late.night" : DateTime.Now.Hour < 11 ? "morning" : "app.open"; kind = "greet"; }
            else if (at < 0) { if (s.Call) key = "call.on"; else { string koan = Koan(); if (koan != "") return Lines.Speaks("idle") ? koan : ""; key = "all.good"; kind = Lines.Who() == Lines.Mood ? "idle" : "state"; } }
            else
            {
                kind = lv >= 2 ? "problem" : "state";
                if (at == 0) { key = "pc.busy"; if (!double.IsNaN(s.Cpu)) v = s.Cpu.ToString("0", CultureInfo.CurrentCulture) + "%"; }
                else if (at == 1) key = s.PathMain != null && s.PathMain.Length > 1 && (s.PathMain[1] ?? "").IndexOf("not connected", StringComparison.OrdinalIgnoreCase) >= 0 || s.Headline.IndexOf("not connected to any network", StringComparison.OrdinalIgnoreCase) >= 0 ? "net.none" : lv >= 2 ? "gw.down" : "wifi.drops";
                else if (at == 3) { key = "dns.slow"; if (s.DnsMs > 0) v = s.DnsMs.ToString("0", CultureInfo.CurrentCulture) + " ms"; }
                else key = lv >= 2 ? "isp.down" : "inet.slow";
            }
            if (!Lines.Speaks(kind)) return "";
            return Lines.Steady(key, v);
        }
        // the rare koan: only after two hours of all green, at most once a day, and only for a few minutes
        static readonly string[] Koans = {
            "A packet that is never lost was never really sent.",
            "The fastest route is the one you never notice.",
            "Two hours, no retransmits. Somewhere a router is smiling.",
            "The ping returns. The ping always returns.",
            "Bandwidth is what you buy. Latency is what you feel.",
            "In the quiet between packets, the network rests." };
        static string Koan()
        {
            if (Voice.Serious || greenSince == DateTime.MinValue || (DateTime.Now - greenSince).TotalHours < 2) { koanFrom = DateTime.MinValue; return ""; }
            string day = DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture); if (koanDone == day) return "";
            if (koanFrom == DateTime.MinValue) koanFrom = DateTime.Now;
            if ((DateTime.Now - koanFrom).TotalMinutes > 10) { koanDone = day; koanFrom = DateTime.MinValue; return ""; }   // ten minutes, then quiet until tomorrow
            return Koans[DateTime.Today.DayOfYear % Koans.Length];
        }
        // the provider's excuses, longer outage - wilder story
        static readonly string[][] Excuses = {
            new[] { "Probably a router reboot somewhere. Give it a minute.", "Maybe a cable got bumped at their end." },
            new[] { "A technician is definitely on it. Probably. Maybe having lunch first.", "Their status page says 'all systems normal'. Of course it does." },
            new[] { "A squirrel has chewed through a fibre. A very determined squirrel.", "Someone tripped over the internet. The whole internet." },
            new[] { "Their servers are being updated by a single intern with one USB stick.", "The packets have unionized and are on strike for better routes." },
            new[] { "A solar flare, a backhoe and a full moon - all at once. Rare, but here we are.", "They've misplaced the internet. They're checking behind the couch." } };
        static string Excuse(double minutes, string isp)
        {
            int tier = minutes < 10 ? 0 : minutes < 30 ? 1 : minutes < 60 ? 2 : minutes < 180 ? 3 : 4;
            string who = string.IsNullOrEmpty(isp) ? "Your provider's" : isp + "'s";
            return who + " excuse, after " + (minutes < 120 ? (int)minutes + " min" : (int)(minutes / 60) + " h") + ": " + Pick(Excuses[tier], 0);
        }

        // ---------- seasons: a word or two, never more (the widget card and the header)
        public static string Season()
        {
            var d = DateTime.Today;
            if (d.Month == 10 && d.Day >= 24) return "halloween";
            if ((d.Month == 12 && d.Day == 31) || (d.Month == 1 && d.Day <= 2)) return "newyear";
            if (d.Month == 12 || d.Month <= 2) return "winter";
            return "";
        }
        public static string SeasonLine(Snap s)
        {
            if (Voice.Serious) return "";
            int lv, at = Worst(s, out lv);
            switch (Season())
            {
                case "halloween": return at < 0 ? "🎃 No packet ghosts tonight." : "👻 Packet ghosts detected - " + (at == 1 ? "haunting your Wi-Fi." : at == 0 ? "possessing your PC." : "lurking past your router.");
                case "winter": return at < 0 ? "❄ Warm and cosy on the network." : "❄ Cold start latency - " + (at == 1 ? "your Wi-Fi needs a scarf." : "something's frozen up the line.");
                case "newyear": return at < 0 ? "🎆 A clean network to start the year." : "🎆 New year, same " + (at == 1 ? "Wi-Fi" : "provider") + ".";
                default: return "";
            }
        }

        // ---------- grades, said the way the mood says them (the letter itself never changes)
        public static string Grade(string letter)
        {
            if (!On || string.IsNullOrEmpty(letter)) return "";
            bool good = letter.StartsWith("A", StringComparison.Ordinal);
            switch (Name)
            {
                case "brutal": return good ? "An " + letter + ". Fine. Don't let it go to your head." : (letter.StartsWith("F", StringComparison.Ordinal) ? "An " : "A ") + letter + ", and you know it. The A+ people live on fibre and don't upload while they game.";
                case "sarcastic": return good ? letter + ". Look at you, with your fancy queue management." : letter + ". Your router's buffer is basically a waiting room.";
                case "anxious": return good ? letter + "! Oh thank goodness." : letter + "... I'm not saying I'm upset. I'm just saying the buffers are very full.";
                case "conspiracy": return good ? letter + ". Almost too good." : letter + ". Someone is holding your packets in a queue. On purpose? You decide.";
                default: return "";
            }
        }
    }
}
