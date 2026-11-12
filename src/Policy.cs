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
    //   mode           = simple     always Simple mode (pro is also accepted - it doesn't add anything)
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
                    var info = new FileInfo(f); if (info.Length > 16384) { Program.Log("policy: " + f + " is too big - ignored"); continue; }
                    Parse(System.IO.File.ReadAllLines(f)); File = f; Program.Log("policy: " + f); return;
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
                    case "mode": if (v == "simple" || v == "pro") Mode = v; break;
                }
            }
        }
        // after the user's settings are read: the policy wins
        public static void Apply()
        {
            if (NoInternetCheck) Cfg.NetSec = 0;
            if (MaxHistoryDays > 0 && Cfg.HistoryDays > MaxHistoryDays) Cfg.HistoryDays = MaxHistoryDays;
            if (Mode != "") Cfg.Mode = Mode;
            if (NoGamer) Cfg.GameIds = "";
        }
        public static void Reset() { NoInternetCheck = NoOnline = NoSpeed = NoLab = NoCapture = NoGamer = false; MaxHistoryDays = 0; Mode = ""; File = ""; }
    }
}
