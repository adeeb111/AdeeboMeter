// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Side by side: Wi-Fi vs cable, or two PCs
    // The same game servers, the same number of probes (30 each, one a second per server), saved under a name you choose.
    // Switch to the cable (or run it on the other PC) and run again with another name - every saved run is shown side by side.
    // A run from another PC comes over as one line you copy there and paste here. Up to 4 runs per game, in game-compare.txt.
    static class GameCompare
    {
        public sealed class RunRow { public string Server; public double Avg, P95, Jit, Loss; }
        public sealed class Saved { public string Game, Name; public DateTime T; public List<RunRow> Rows = new List<RunRow>(); }
        public const int Samples = 30, MaxRuns = 4;
        static string FilePath { get { return Path.Combine(Program.DataDir, "game-compare.txt"); } }
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string F(double v) { return double.IsNaN(v) ? "-" : Math.Round(v).ToString(CultureInfo.InvariantCulture); }

        // one line per run:  run|game|name|yyyyMMddHHmm|server=avg/p95/jit/loss;server=...
        public static string Line(Saved s)
        {
            return "run|" + s.Game + "|" + Clean(s.Name) + "|" + s.T.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + "|" +
                string.Join(";", s.Rows.Select(r => Clean(r.Server).Replace("=", "-").Replace(";", ",") + "=" + string.Join("/", new[] { r.Avg, r.P95, r.Jit, r.Loss }.Select(x => double.IsNaN(x) ? "" : x.ToString("0.#", CultureInfo.InvariantCulture)))));
        }
        public static Saved ParseLine(string l)
        {
            var p = (l ?? "").Trim().Split('|'); if (p.Length != 5 || p[0] != "run" || !Regex.IsMatch(p[1], "^[a-z0-9-]{1,40}$")) return null;
            DateTime t; if (!DateTime.TryParseExact(p[3], "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return null;
            var s = new Saved { Game = p[1], Name = Clean(p[2]), T = t };
            foreach (var part in p[4].Split(';').Take(12))
            {
                int eq = part.IndexOf('='); if (eq <= 0) continue; var n = part.Substring(eq + 1).Split('/'); if (n.Length != 4) continue;
                Func<string, double> d = x => { double v; return double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0 && v < 100000 ? v : double.NaN; };
                s.Rows.Add(new RunRow { Server = Clean(part.Substring(0, eq)), Avg = d(n[0]), P95 = d(n[1]), Jit = d(n[2]), Loss = d(n[3]) });
            }
            return s.Rows.Count == 0 ? null : s;
        }
        static string Clean(string s) { s = new string((s ?? "").Where(c => !char.IsControl(c) && c != '|').ToArray()).Trim(); return s.Length > 40 ? s.Substring(0, 40) : s; }
        static List<Saved> Load() { try { return File.Exists(FilePath) ? File.ReadAllLines(FilePath).Take(200).Select(ParseLine).Where(x => x != null).ToList() : new List<Saved>(); } catch (IOException) { return new List<Saved>(); } }
        static void Store(List<Saved> all) { try { File.WriteAllLines(FilePath, all.Select(Line)); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("game compare: " + e.Message); } }

        // input: game | a name for this run - or paste a run copied from another PC | "clear" to start over
        public static List<string[]> Run(Monitor m, string gameIn, string name, string clear, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string q = (gameIn ?? "").Trim();
            var game = Games.All.FirstOrDefault(z => !z.Soon && (string.Equals(z.Id, q, StringComparison.OrdinalIgnoreCase) || string.Equals(z.Name, q, StringComparison.OrdinalIgnoreCase)))
                       ?? (q == "" ? Games.All.FirstOrDefault(z => z.Id == GamePing.Me.Playing) ?? Games.All.FirstOrDefault(z => Games.Monitored().Contains(z.Id)) : null);
            if (game == null) { rows.Add(R(2, "Game", "type a game you added - e.g. valorant", "", "u")); return rows; }
            var all = Load();
            if (string.Equals((clear ?? "").Trim(), "clear", StringComparison.OrdinalIgnoreCase)) { all.RemoveAll(z => z.Game == game.Id); Store(all); rows.Add(R(0, "Cleared", "the saved runs for " + game.Name, "", "u")); return rows; }
            string nm = (name ?? "").Trim();
            var pasted = ParseLine(nm);
            if (pasted != null)
            {
                if (pasted.Game != game.Id) { rows.Add(R(2, "Pasted run", "it's for " + pasted.Game + ", not " + game.Id, "", "u")); return rows; }
                pasted.Name = Clean(pasted.Name + " (other PC)"); all.Add(pasted);
            }
            else
            {
                if (!Games.Monitored().Contains(game.Id)) { rows.Add(R(2, game.Name, "click it in the Gamer Zone first - that's your OK to contact its servers", "", "u")); return rows; }
                if (nm == "") nm = m.AdapterKind == "Wi-Fi" ? "Wi-Fi" : "Cable";
                var servers = game.Servers.Take(8).ToList(); var s = new Saved { Game = game.Id, Name = Clean(nm), T = DateTime.Now };
                var samples = servers.ToDictionary(z => z.Name, z => new List<double>());
                for (int i = 0; i < Samples && !run.Stop; i++)
                {
                    var t0 = DateTime.UtcNow; run.Status = "Probe " + (i + 1) + " of " + Samples + " to " + servers.Count + " servers (" + nm + ")...";
                    foreach (var sv in servers) { string used; samples[sv.Name].Add(GamePing.Probe(sv, out used)); }
                    int wait = 1000 - (int)(DateTime.UtcNow - t0).TotalMilliseconds; if (wait > 0) Thread.Sleep(wait);
                }
                if (run.Stop) { rows.Add(R(1, "Stopped", "the run wasn't saved", "", "m")); return rows; }
                foreach (var sv in servers) { var v = samples[sv.Name].ToArray(); s.Rows.Add(new RunRow { Server = sv.Name, Avg = GameStat.Avg(v), P95 = GameStat.Pct(v, 0.95), Jit = GameStat.Jitter(v), Loss = GameStat.Loss(v) }); }
                all.Add(s);
            }
            var mine = all.Where(z => z.Game == game.Id).ToList();
            if (mine.Count > MaxRuns) { foreach (var old in mine.Take(mine.Count - MaxRuns)) all.Remove(old); mine = mine.Skip(mine.Count - MaxRuns).ToList(); }
            Store(all);
            // side by side: one row per server, each run's avg / 95% / jitter / loss
            rows.Add(R(-1, game.Name.ToUpperInvariant() + " · SIDE BY SIDE", string.Join("  vs  ", mine.Select(z => z.Name + " (" + z.T.ToString("d MMM HH:mm", CultureInfo.CurrentCulture) + ")")), "", "m"));
            foreach (var srv in mine.SelectMany(z => z.Rows.Select(r => r.Server)).Distinct())
            {
                var cells = mine.Select(z => { var r = z.Rows.FirstOrDefault(x => x.Server == srv); return z.Name + ": " + (r == null ? "-" : F(r.Avg) + " ms · 95% " + F(r.P95) + " · jitter " + F(r.Jit) + " · loss " + (double.IsNaN(r.Loss) ? "-" : r.Loss.ToString("0.#", CultureInfo.InvariantCulture) + "%")); });
                rows.Add(R(3, srv, string.Join("   |   ", cells), "", "m"));
            }
            if (mine.Count >= 2) rows.Add(R(0, "What it means", Verdict(mine[mine.Count - 2], mine[mine.Count - 1]), "", "i"));
            else rows.Add(R(3, "Next", "now switch (plug in the cable, or go to the other PC) and run again with another name", "", "u"));
            var last = mine.Last();
            rows.Add(new[] { "3", "Copy this run", "to compare with another PC: copy here, paste in that PC's \"name\" box", "", "u", "copy:" + Line(last) });
            return rows;
        }
        // compares the two newest runs on what a gamer feels: jitter and loss first, then the average
        public static string Verdict(Saved a, Saved b)
        {
            Func<Saved, Func<RunRow, double>, double> mean = (s, f) => { var v = s.Rows.Select(f).Where(x => !double.IsNaN(x)).ToList(); return v.Count == 0 ? double.NaN : v.Average(); };
            double ja = mean(a, r => r.Jit), jb = mean(b, r => r.Jit), la = mean(a, r => r.Loss), lb = mean(b, r => r.Loss), aa = mean(a, r => r.Avg), ab = mean(b, r => r.Avg);
            var bits = new List<string>();
            if (!double.IsNaN(la) && !double.IsNaN(lb) && Math.Abs(la - lb) >= 0.5) bits.Add((la < lb ? a.Name : b.Name) + " loses fewer packets (" + Math.Min(la, lb).ToString("0.#", CultureInfo.InvariantCulture) + "% vs " + Math.Max(la, lb).ToString("0.#", CultureInfo.InvariantCulture) + "%)");
            if (!double.IsNaN(ja) && !double.IsNaN(jb) && Math.Abs(ja - jb) >= 3) bits.Add((ja < jb ? a.Name : b.Name) + " is steadier (jitter " + F(Math.Min(ja, jb)) + " vs " + F(Math.Max(ja, jb)) + " ms)");
            if (!double.IsNaN(aa) && !double.IsNaN(ab) && Math.Abs(aa - ab) >= 3) bits.Add((aa < ab ? a.Name : b.Name) + " is " + F(Math.Abs(aa - ab)) + " ms quicker on average");
            return bits.Count == 0 ? "No real difference between " + a.Name + " and " + b.Name + " in these runs." : string.Join("; ", bits) + ".";
        }
    }
}
