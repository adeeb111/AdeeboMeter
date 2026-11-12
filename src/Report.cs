// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== AdeeboMeter.exe --report out.json   (for IT teams)
    // No window: watches for about 15 seconds (the same pings and checks the corner widget does), runs "What's wrong?" once,
    // writes the answer as JSON and exits. Exit code = the verdict: 0 fine, 1 worth a look, 2 a problem, 3 couldn't tell
    // (also when the verdict looked fine but one of its steps couldn't run); 4 = the report itself couldn't be made (a bad
    // file name, a folder you can't write to). The user's own settings apply: an internet check switched off stays off.
    // Usable from a script, Intune or an RMM tool. Nothing is written except the file you name and the usual log.
    static class Report
    {
        public static int Run(string outPath, int seconds)
        {
            string file;
            try { file = Path.GetFullPath(outPath); } catch (Exception e) { if (!(e is ArgumentException || e is NotSupportedException || e is PathTooLongException || e is System.Security.SecurityException)) throw; return 4; }
            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) { Program.Log("--report: the file name must end in .json"); return 4; }
            var m = new Monitor();
            try
            {
                ReadSettings(); Policy.Load(); Policy.Apply();
                m.RefreshInterfaces(); m.Start();
                Thread.Sleep(Math.Max(5, Math.Min(120, seconds)) * 1000);
                var r = new GuideRun(); Investigate.Run("whats", m, r, delegate { });
                int lv = r.Lv < 0 ? 3 : Math.Min(3, r.Lv);
                // "fine" isn't fine when a step couldn't run: say so instead of passing a half-checked PC
                // only a step that couldn't run ("u" evidence) - not an informational row like "Around then: On LAN" or "normal for
                // this network", which every healthy run has
                if (lv == 0 && r.ObservedCopy().Any(o => o.Length > 1 && o[0] == "u" && o[1] == "3")) lv = 3;
                string json = Json(m, r, lv);
                try { File.WriteAllText(file, json, new UTF8Encoding(false)); }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is NotSupportedException || e is System.Security.SecurityException)) throw; Program.Log("--report: can't write " + file + ": " + e.Message); return 4; }
                Program.Log("--report written: " + file + " (verdict " + lv + ")");
                return lv;
            }
            catch (Exception e)
            {
                Program.Log("--report: " + e); return 3;   // never the CLR's own crash code: a script gets "couldn't tell"
            }
            finally { try { m.Stop(); } catch (InvalidOperationException) { } }
        }
        // the user's saved settings (what the background checks may do), read-only - nothing is written back
        static void ReadSettings()
        {
            try
            {
                string f = Path.Combine(Program.WriteDir, "AdeeboMeter.settings"); if (!File.Exists(f)) return;
                foreach (var line in File.ReadAllLines(f).Take(500)) { int eq = line.IndexOf('='); if (eq > 0) Cfg.Read(line.Substring(0, eq), line.Substring(eq + 1)); }
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("--report settings: " + e.Message); }
        }
        static string Q(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
        // the monitor's DNS probe runs on the internet check's pace and may not have run in a short window: then the time
        // What's wrong? measured itself (its "5. Names" step), so the JSON never says null next to "names work"
        static double DnsMs(Monitor m, GuideRun r)
        {
            double d = m.Dns.Avg(6); if (!double.IsNaN(d)) return d;
            var o = r.ObservedCopy().FirstOrDefault(x => x.Length > 3 && x[2].StartsWith("5. Names", StringComparison.Ordinal));
            if (o == null) return double.NaN;
            var mm = System.Text.RegularExpressions.Regex.Match(o[3], @"^(\d+(?:[.,]\d+)?)\s*ms");
            double v; return mm.Success && double.TryParse(mm.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN;
        }
        static string N(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "null" : Math.Round(v, 1).ToString(CultureInfo.InvariantCulture); }
        public static string Json(Monitor m, GuideRun r, int lv)
        {
            string kind, adapter, gw, dns, vpnName, ssid; bool vpn, viaTcp; int sig;
            lock (m.L) { kind = m.AdapterKind; adapter = m.Adapter; gw = m.Gateway; dns = m.DnsServer; vpn = m.VpnUp; vpnName = m.VpnName; ssid = m.Ssid; sig = m.WifiSignal; viaTcp = m.NetViaTcp; }
            string[] words = { "ok", "warning", "problem", "unknown" };
            var sb = new StringBuilder("{\n");
            sb.Append("  \"app\": ").Append(Q("AdeeboMeter " + Program.Version)).Append(",\n");
            sb.Append("  \"time\": ").Append(Q(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture))).Append(",\n");
            sb.Append("  \"computer\": ").Append(Q(Environment.MachineName)).Append(",\n");
            sb.Append("  \"verdict\": ").Append(Q(words[lv])).Append(", \"exit_code\": ").Append(lv).Append(",\n");
            sb.Append("  \"finding\": ").Append(Q(r.Finding)).Append(",\n");
            sb.Append("  \"network\": { \"kind\": ").Append(Q(kind)).Append(", \"adapter\": ").Append(Q(adapter)).Append(", \"wifi_signal\": ").Append(sig >= 0 ? sig.ToString(CultureInfo.InvariantCulture) : "null")
              .Append(", \"wifi_name\": ").Append(Q(ssid)).Append(", \"gateway\": ").Append(Q(gw)).Append(", \"dns\": ").Append(Q(dns)).Append(", \"vpn\": ").Append(vpn ? "true" : "false").Append(", \"vpn_name\": ").Append(Q(vpnName)).Append(" },\n");
            sb.Append("  \"measured_ms\": { \"router\": ").Append(N(m.GwPing.Avg(6))).Append(", \"internet\": ").Append(N(m.NetPing.Avg(6))).Append(", \"internet_via\": ").Append(Q(viaTcp ? "tcp443" : "icmp"))
              .Append(", \"jitter\": ").Append(N(m.NetPing.Jitter(10))).Append(", \"dns\": ").Append(N(DnsMs(m, r))).Append(" },\n");
            sb.Append("  \"observed\": [\n");
            var obs = r.ObservedCopy();
            for (int i = 0; i < obs.Count; i++) sb.Append("    { \"what\": ").Append(Q(obs[i][2])).Append(", \"value\": ").Append(Q(obs[i][3])).Append(", \"level\": ").Append(obs[i][1]).Append(", \"evidence\": ").Append(Q(obs[i][0])).Append(" }").Append(i < obs.Count - 1 ? ",\n" : "\n");
            sb.Append("  ],\n  \"next\": [").Append(string.Join(", ", r.Next.Select(Q))).Append("],\n");
            sb.Append("  \"policy\": ").Append(Q(Policy.File)).Append("\n}\n");
            return sb.ToString();
        }
    }
}
