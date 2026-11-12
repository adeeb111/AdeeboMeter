// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== Admin preset (for IT teams)
    // A plain text file, AdeeboMeter.policy, next to AdeeboMeter.exe or in %ProgramData%\AdeeboMeter\. It can only switch
    // things OFF or narrow them - never turn anything on or give AdeeboMeter more reach - so a file nobody vetted can't make the
    // app do more than it would by default. Read once at start; the Settings rows it locks say "Set by your IT team".
    //
    //   # AdeeboMeter.policy - one setting per line
    //   internet_check = off        the background ping to 1.1.1.1 / 8.8.8.8 (and its TCP fallback)
    //   online_lookups = off        BGP / who-owns-the-network lookups (RIPEstat, PeeringDB)
    //   speed_test     = off        the speed test (all servers)
    //   traffic_lab    = off        Toolbox › Traffic Lab (building and sending test packets)
    //   live_capture   = off        Live capture and Find my match server (Wireshark's tshark)
    //   gamer_zone     = off        game pings in the background (the tab says it's off)
    //   history_days   = 3          keep at most this many days of history (1-30)
    //   mode           = simple     always Simple mode
    //
    // Both files are read and combined - the strictest wins - so an empty file next to the exe can't cancel IT's file in
    // %ProgramData%. The policy only changes what runs; the user's own saved settings are kept and come back if it goes.
    static class Policy
    {
        public static bool NoInternetCheck, NoOnline, NoSpeed, NoLab, NoCapture, NoGamer;
        public static int MaxHistoryDays;   // 0 = no limit
        public static string Mode = "", File = "";
        public static bool Any { get { return File != ""; } }
        public const string Locked = "Set by your IT team (AdeeboMeter.policy).";

        public static void Load()
        {
            foreach (var dir in new[] { Program.Dir, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AdeeboMeter") })
            {
                try
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    string f = Path.Combine(dir, "AdeeboMeter.policy"); if (!System.IO.File.Exists(f)) continue;
                    // a big file (a long comment header) is read up to 2,000 lines - never ignored whole: IT would think things are off
                    var info = new FileInfo(f); if (info.Length > 16384) Program.Log("policy: " + f + " is " + info.Length + " bytes - reading its first 2,000 lines");
                    Parse(System.IO.File.ReadLines(f).Take(2000).ToList()); File = File == "" ? f : File + "; " + f; Program.Log("policy: " + f);
                }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException)) throw; Program.Log("policy: " + e.Message); }
            }
        }
        public static void Parse(IEnumerable<string> lines)
        {
            foreach (var raw in lines.Take(200))
            {
                string line = raw; int hash = line.IndexOf('#'); if (hash >= 0) line = line.Substring(0, hash);
                int eq = line.IndexOf('='); if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim().ToLowerInvariant(), v = line.Substring(eq + 1).Trim().ToLowerInvariant();
                bool off = v == "off" || v == "no" || v == "false" || v == "0";
                switch (k)
                {
                    case "internet_check": NoInternetCheck |= off; break;
                    case "online_lookups": NoOnline |= off; break;
                    case "speed_test": NoSpeed |= off; break;
                    case "traffic_lab": NoLab |= off; break;
                    case "live_capture": NoCapture |= off; break;
                    case "gamer_zone": NoGamer |= off; break;
                    case "history_days": int d; if (int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out d) && d >= 1 && d <= 30) MaxHistoryDays = MaxHistoryDays == 0 ? d : Math.Min(MaxHistoryDays, d); break;
                    case "mode": if (v == "simple") Mode = v; break;   // only ever narrows: "pro" isn't a policy
                }
            }
        }
        // after the user's settings are read: the policy wins
        // The user's own values that the policy is covering, so saving settings writes theirs back, not the policy's.
        static readonly Dictionary<string, string> mine = new Dictionary<string, string>();
        public static void Apply()
        {
            lock (mine)
            {
                if (NoInternetCheck && Cfg.NetSec != 0) { mine["net"] = Cfg.NetSec.ToString(CultureInfo.InvariantCulture); Cfg.NetSec = 0; }
                if (MaxHistoryDays > 0 && Cfg.HistoryDays > MaxHistoryDays) { mine["histdays"] = Cfg.HistoryDays.ToString(CultureInfo.InvariantCulture); Cfg.HistoryDays = MaxHistoryDays; }
                if (Mode != "" && Cfg.Mode != Mode) { mine["mode"] = Cfg.Mode; Cfg.Mode = Mode; }
                if (NoGamer && Cfg.GameIds != "") { mine["gmon"] = Cfg.GameIds; Cfg.GameIds = ""; }
            }
        }
        // what to save for a key: the user's own value while the policy covers it
        public static string Keep(string key, string current)
        {
            lock (mine) { string v; return mine.TryGetValue(key, out v) ? v : current; }
        }
        public static void Reset() { NoInternetCheck = NoOnline = NoSpeed = NoLab = NoCapture = NoGamer = false; MaxHistoryDays = 0; Mode = ""; File = ""; lock (mine) mine.Clear(); }
    }
}
