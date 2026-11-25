// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== Moments (Adeeb, 14 Nov: "quiet with an occasional emoji")
    // The corner stays calm; when something good or notable happens the buddy's face changes for 12 seconds, and the
    // hover card says why. Only things the app really knows - it sees ping, not who won the match. Each kind at most once
    // a day, never during a call or a problem, never in the Quiet corner.
    static class Moments
    {
        public const string Quiet = "quiet", WithMoments = "moments", FollowView = "view";
        public static string Face { get { lock (k) return DateTime.UtcNow < until ? face : null; } }
        public static string Line { get { lock (k) return DateTime.UtcNow < until ? line : null; } }
        static readonly object k = new object(); static string face, line; static DateTime until = DateTime.MinValue;
        static readonly Dictionary<string, DateTime> shown = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        public static bool On { get { return Cfg.Corner != Quiet; } }

        // fire one: face is an emoji code the app carries (assets/emoji), line the hover sentence
        public static bool Fire(string kind, string emoji, string text)
        {
            if (!On || string.IsNullOrEmpty(text)) return false;
            lock (k)
            {
                if (!seenLoaded) LoadSeen();
                DateTime last; if (shown.TryGetValue(kind, out last) && last.Date == DateTime.Today) return false;   // once a day per kind
                shown[kind] = DateTime.Now; SaveSeen(); face = emoji; line = text; until = DateTime.UtcNow.AddSeconds(12);
            }
            Program.Log("moment: " + kind);
            return true;
        }
        // the face to draw now (null = the usual one): not during a call or a problem
        public static string FaceFor(Snap s) { return s == null || s.Call || s.Level > 0 ? null : Face ?? Weather.Face(DateTime.UtcNow); }   // then the weather: 2.5 min three times an hour

        // ---------- the moments
        // a game session ended clean: 15 minutes or more, at most 2 spiky seconds
        public static void GameEnded(string game, DateTime start, DateTime end, int spikes)
        {
            double min = (end - start).TotalMinutes; if (min < 15 || spikes > 2) return;
            Fire("game", "1f60e", L.T("Clean session") + " - " + game + ", " + Dur(min) + L.T(", no lag spikes."));
        }
        // a stream ended without a single dip (20 minutes or more)
        public static void StreamEnded(string what, double minutes, int dips)
        {
            if (minutes < 20 || dips > 0) return;
            Fire("stream", "1f60c", what + ", " + Dur(minutes) + L.T(" - not a single dip."));
        }
        // the broker line stayed fast for the whole trading day (3 hours or more watched, no dips)
        public static void BrokerDay(double hours, int dips, double medianMs)
        {
            if (hours < 3 || dips > 0) return;
            Fire("broker", "1f607", L.T("Broker steady all day") + (double.IsNaN(medianMs) ? "." : ", " + medianMs.ToString("0", CultureInfo.CurrentCulture) + " ms."));
        }
        // a big move in what the Trading page follows: a currency 1% or more in a day, a commodity group 5% or more in a week
        public static void Moves(List<Trading.Rate> rates, List<Trading.Index> idx)
        {
            if (Cfg.Off("trade") || !Cfg.TradeFx) return;
            var big = rates.Where(r => !double.IsNaN(r.Prev) && r.Prev > 0).Select(r => new { r.Code, P = (r.Cad - r.Prev) / r.Prev * 100 }).Where(z => Math.Abs(z.P) >= 1).OrderByDescending(z => Math.Abs(z.P)).FirstOrDefault();
            if (big != null) { Fire("move", "1f92f", big.Code + (big.P > 0 ? " ▲" : " ▼") + Math.Abs(big.P).ToString("0.0", CultureInfo.CurrentCulture) + L.T("% against the Canadian dollar today.")); return; }
            var bi = idx.Select(i => new { i.Code, P = Trading.Change(i, 1) }).Where(z => !double.IsNaN(z.P) && Math.Abs(z.P) >= 5).OrderByDescending(z => Math.Abs(z.P)).FirstOrDefault();
            if (bi != null) { string n; Trading.IdxNames.TryGetValue(bi.Code, out n); Fire("move", "1f92f", L.T(n ?? bi.Code) + (bi.P > 0 ? " ▲" : " ▼") + Math.Abs(bi.P).ToString("0.0", CultureInfo.CurrentCulture) + L.T("% this week.")); }
        }
        // the internet came back after a red spell of a minute or more
        public static void Back(TimeSpan down) { if (down.TotalMinutes >= 1) Fire("back", "1f973", L.T("Back. That lasted ") + Dur(down.TotalMinutes) + "."); }
        // a new best download in a speed test (kept in moments.txt beside the settings)
        public static void Speed(double downMbps)
        {
            if (double.IsNaN(downMbps) || downMbps <= 0) return;
            double best = Best(); if (downMbps <= best * 1.02) return;   // bug-hunt L-123: a first result is always above 0, so it's saved below
            SaveBest(downMbps);
            if (best > 0) Fire("speed", "1f929", L.T("New record: ") + downMbps.ToString("0", CultureInfo.CurrentCulture) + L.T(" Mb/s down (was ") + best.ToString("0", CultureInfo.CurrentCulture) + ").");
        }
        static string BestFile { get { return Path.Combine(Program.DataDir, "moments.txt"); } }
        static double Best()
        {
            try { double v; return File.Exists(BestFile) && double.TryParse(File.ReadAllText(BestFile).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0 && v < 100000 ? v : 0; }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; return 0; }
        }
        static void SaveBest(double v) { try { File.WriteAllText(BestFile, v.ToString("0.0", CultureInfo.InvariantCulture)); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; } }
        static string Dur(double min) { return min < 60 ? Math.Max(1, (int)Math.Round(min)).ToString(CultureInfo.CurrentCulture) + " min" : ((int)(min / 60)).ToString(CultureInfo.CurrentCulture) + " h " + ((int)min % 60).ToString(CultureInfo.CurrentCulture) + " min"; }
        // self-test
        internal static void Reset() { lock (k) { shown.Clear(); seenLoaded = true; until = DateTime.MinValue; } }
        // bug-hunt L-122: "once a day" survives a restart - the day each kind was last shown is kept beside the settings
        static bool seenLoaded;
        static string SeenFile { get { return Path.Combine(Program.DataDir, "moments-seen.txt"); } }
        static void LoadSeen()
        {
            seenLoaded = true;
            try { if (File.Exists(SeenFile)) foreach (var l in File.ReadLines(SeenFile).Take(50)) { var p = l.Split('='); DateTime d; if (p.Length == 2 && DateTime.TryParseExact(p[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) shown[p[0]] = d; } }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
        }
        static void SaveSeen()
        {
            try { SafeFile.WriteText(SeenFile, string.Join("\n", shown.Where(kv => kv.Value.Date == DateTime.Today).Select(kv => kv.Key + "=" + kv.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))) + "\n"); }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
        }
    }
}
