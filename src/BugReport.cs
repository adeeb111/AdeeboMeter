// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
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
        public static string Text(string page)
        {
            return "**What happened?**\n(What you clicked, what you expected, what you saw instead. A screenshot helps.)\n\n" + Tech(page);
        }
        // the technical part only (the report box shows it before anything goes)
        public static string Tech(string page)
        {
            var sb = new StringBuilder();
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
        static readonly Regex ipv6short = new Regex(@"(?<![\w:])(?:[0-9A-Fa-f]{1,4}:){0,7}:(?:[0-9A-Fa-f]{1,4}:?){0,7}(?:%[\w.-]+)?(?![\w:])"), anyPath = new Regex(@"\b[A-Za-z]:[\\/](?:[^\s""'<>|]+|\s(?=[^\s""'<>|]*[\\/.][^\s""'<>|]))+"), unc = new Regex(@"[\\/]{2}[\w.$-]+(?:[\\/][^\s""'<>|]*)?"), mac12 = new Regex(@"\b(?=[0-9A-Fa-f]*[A-Fa-f])[0-9A-Fa-f]{12}\b"),
            host = new Regex(@"\b(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+([A-Za-z]{2,24})\b");
        // file names that look like host names, and the app's own sites: kept (they say nothing about the person)
        static readonly HashSet<string> NotHost = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "exe", "dll", "cs", "txt", "json", "log", "png", "settings", "md", "xml", "config", "ini", "tmp", "pdf", "csv", "drawio", "zip" };
        static readonly HashSet<string> OkHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "adeebometer.com", "www.adeebometer.com", "github.com", "api.github.com", "open-meteo.com", "api.open-meteo.com", "feeds.bbci.co.uk", "api-web.nhle.com", "www.bankofcanada.ca" };
        // take out what could point to a person: their user and PC names, the profile path, addresses of any kind
        static bool VersionBefore(string s, int at) { int from = Math.Max(0, at - 14); string before = s.Substring(from, at - from); return Regex.IsMatch(before, @"(\bv|version|AdeeboMeter|Windows|build|CLR|\.NET)\s*$", RegexOptions.IgnoreCase); }
        public static string Scrub(string s) { return Scrub(s, Environment.UserName, Environment.MachineName); }
        public static string Scrub(string s, string user, string pc)
        {
            // review 1.1.6 (security #8): whole paths and shares go first, so no folder or file name (and no surname after a space) is left behind
            s = unc.Replace(s ?? "", "<path>"); s = anyPath.Replace(s, "<path>");
            s = userPath.Replace(s, "$1<user>");
            if (!string.IsNullOrEmpty(user) && user.Length >= 2) s = Regex.Replace(s, Regex.Escape(user), "<user>", RegexOptions.IgnoreCase);
            if (!string.IsNullOrEmpty(pc) && pc.Length >= 2) s = Regex.Replace(s, Regex.Escape(pc), "<pc>", RegexOptions.IgnoreCase);
            // bug-hunt L-291: a one-letter name is taken out where it's named as one (user X, PC X, \\X\\) - not every lone letter
            if (!string.IsNullOrEmpty(user) && user.Length == 1) s = Regex.Replace(s, @"(?<=\b(?:user|username|account|signed in as)[ :=]+)" + Regex.Escape(user) + @"\b", "<user>", RegexOptions.IgnoreCase);
            if (!string.IsNullOrEmpty(pc) && pc.Length == 1) s = Regex.Replace(s, @"(?<=\b(?:pc|computer|machine|host)[ :=]+)" + Regex.Escape(pc) + @"\b|(?<=\\\\)" + Regex.Escape(pc) + @"(?=\\)", "<pc>", RegexOptions.IgnoreCase);
            s = email.Replace(s, "<email>"); s = mac.Replace(s, "<mac>"); s = mac12.Replace(s, "<mac>"); s = unc.Replace(s, "<path>"); s = anyPath.Replace(s, m => m.Value.StartsWith("<", StringComparison.Ordinal) ? m.Value : "<path>");
            s = ipv6.Replace(s, "<ipv6>"); s = ipv6short.Replace(s, m => m.Value.Contains("::") && m.Value.Length > 2 ? "<ipv6>" : m.Value); s = ipv4.Replace(s, m => VersionBefore(s, m.Index) ? m.Value : "<ip>");   // bug-hunt L-290: "version 1.1.5.0" stays a version
            s = host.Replace(s, m => NotHost.Contains(m.Groups[1].Value) || OkHosts.Contains(m.Value) || CodeName(m.Value) ? m.Value : "<host>");   // "System.IO" stays, "nas.home.lan" goes
            return s;
        }
        // bug-hunt ML-165: only code names stay (System.IO, Microsoft.Win32, AdeeboMeter.Remote) - "Jane.Smith" or "Office.Laptop" go
        static readonly HashSet<string> CodeRoots = new HashSet<string>(StringComparer.Ordinal) { "System", "Microsoft", "AdeeboMeter", "Windows", "Interop", "Mono" };
        static bool CodeName(string v) { var p = v.Split('.'); return p.Length >= 2 && !v.Contains("-") && CodeRoots.Contains(p[0]) && p.All(z => z.Length > 0 && char.IsUpper(z[0])); }
    }
}
