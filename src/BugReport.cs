// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // ================================================================== Report a bug (About › Found a bug?)
    // Puts a ready-made bug report on the clipboard and opens a new GitHub issue; the person pastes it, reads it and sends it
    // themselves. Nothing goes anywhere by itself, and nothing personal is in it: names, the PC's name, addresses and paths
    // are taken out of the log lines before they're copied.
    static class BugReport
    {
        public const string NewIssue = "https://github.com/adeeb111/AdeeboMeter/issues/new";
        public static string Text(string page)
        {
            var sb = new StringBuilder();
            sb.AppendLine("**What happened?**");
            sb.AppendLine("(What you clicked, what you expected, what you saw instead. A screenshot helps.)");
            sb.AppendLine();
            sb.AppendLine("**About this PC** - filled in by AdeeboMeter, nothing personal");
            sb.AppendLine("- AdeeboMeter " + Program.Version + " (" + (Program.Packaged ? "Microsoft Store" : Setup.RunningInstalled ? "installed" : Program.Portable ? "portable" : "not installed") + ")");
            sb.AppendLine("- Windows " + Environment.OSVersion.Version + (Environment.Is64BitOperatingSystem ? ", 64-bit" : ", 32-bit") + " · screen scale " + Math.Round(Draw.S * 100).ToString(CultureInfo.InvariantCulture) + "% · language " + Cfg.Lang);
            sb.AppendLine("- Page: " + (page ?? "") + " · mode " + (Cfg.Simple ? "simple" : "pro") + " · layout " + Cfg.Layout + " · theme " + (Theme.Dark ? "dark" : "light") + " · checks " + Cfg.Preset + " · widget " + Cfg.WidgetStyle + (Cfg.HiddenSecs != "" ? " · hidden: " + Cfg.HiddenSecs : ""));
            var errs = LastLog(8);
            if (errs.Length > 0)
            {
                sb.AppendLine("- The last lines of AdeeboMeter.log (names and addresses removed):");
                sb.AppendLine("```");
                foreach (var l in errs) sb.AppendLine(l);
                sb.AppendLine("```");
            }
            return sb.ToString();
        }
        static string[] LastLog(int n)
        {
            try
            {
                string p = Path.Combine(Program.WriteDir, "AdeeboMeter.log"); if (!File.Exists(p)) return Array.Empty<string>();
                return File.ReadLines(p).Reverse().Take(n).Reverse().Select(l => Scrub(l.Length > 300 ? l.Substring(0, 300) + "..." : l)).ToArray();
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; return Array.Empty<string>(); }
        }
        static readonly Regex ipv4 = new Regex(@"\b\d{1,3}(\.\d{1,3}){3}\b"), mac = new Regex(@"\b([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}\b"), email = new Regex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+"), ipv6 = new Regex(@"\b([0-9A-Fa-f]{1,4}:){2,7}[0-9A-Fa-f]{1,4}\b");
        static readonly Regex userPath = new Regex(@"([A-Za-z]:\\Users\\)[^\\\s]+", RegexOptions.IgnoreCase);
        // take out what could point to a person: their user and PC names, the profile path, addresses of any kind
        public static string Scrub(string s) { return Scrub(s, Environment.UserName, Environment.MachineName); }
        public static string Scrub(string s, string user, string pc)
        {
            s = userPath.Replace(s ?? "", "$1<user>");
            if (!string.IsNullOrEmpty(user) && user.Length >= 2) s = Regex.Replace(s, Regex.Escape(user), "<user>", RegexOptions.IgnoreCase);
            if (!string.IsNullOrEmpty(pc) && pc.Length >= 2) s = Regex.Replace(s, Regex.Escape(pc), "<pc>", RegexOptions.IgnoreCase);
            s = email.Replace(s, "<email>"); s = mac.Replace(s, "<mac>"); s = ipv6.Replace(s, "<ipv6>"); s = ipv4.Replace(s, "<ip>");
            return s;
        }
    }
}
