// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== Neighbour wars: 7 days of who is on your air
    // Once an hour, the Wi-Fi list Windows already holds (no extra scan) is written down: name, channel, width, signal,
    // security of each access point heard here. Kept 7 days in air.txt, on this PC only, and only while History is on.
    // Neighbours' network names are what they broadcast to everyone; nothing about their devices or traffic is seen.
    static class AirLog
    {
        public const int Days = 7;
        static string File { get { return Store.PathOf("air.txt"); } }
        static readonly object k = new object(); static DateTime lastHour = DateTime.MinValue;
        public sealed class Row { public DateTime T; public string Bssid = "", Ssid = "", Band = "", Sec = ""; public int Channel, Width, Rssi; public bool Mine; }

        public static void Record(Monitor m)
        {
            if (!Cfg.History) return;
            var hour = DateTime.Now.Date.AddHours(DateTime.Now.Hour); if (hour == lastHour) return;
            bool wifi; string myB, mySsid; lock (m.L) { wifi = m.HasWifi && m.AdapterKind == "Wi-Fi"; myB = (m.Bssid ?? "").Replace('-', ':'); mySsid = m.Ssid ?? ""; }
            if (!wifi) return;
            string err; var bss = WlanApi.List(false, out err); if (bss.Count == 0) return;   // location off, or nothing heard: write nothing
            lastHour = hour;
            var lines = bss.Where(b => b.Channel > 0).OrderByDescending(b => b.Rssi).Take(40).Select(b => string.Join("\t", hour.ToString("yyyy-MM-ddTHH", CultureInfo.InvariantCulture), b.Bssid, (b.Ssid ?? "").Replace('\t', ' '), b.Band, b.Channel.ToString(CultureInfo.InvariantCulture), b.Width.ToString(CultureInfo.InvariantCulture), b.Rssi.ToString(CultureInfo.InvariantCulture), b.Sec, string.Equals(b.Bssid, myB, StringComparison.OrdinalIgnoreCase) || (mySsid != "" && b.Ssid == mySsid) ? "1" : "0")).ToList();
            lock (k)
            {
                try
                {
                    var keep = new List<string>(); var cut = DateTime.Now.Date.AddDays(-(Days - 1));
                    if (System.IO.File.Exists(File)) foreach (var l in System.IO.File.ReadAllLines(File).Take(20000)) { var r = Parse(l); if (r != null && r.T >= cut && r.T < hour) keep.Add(l); }
                    keep.AddRange(lines); string tmp = File + ".tmp"; System.IO.File.WriteAllLines(tmp, keep); Program.ReplaceFile(tmp, File);
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("air log: " + e.Message); }
            }
        }
        public static void Clear() { lock (k) { try { if (System.IO.File.Exists(File)) System.IO.File.Delete(File); } catch (IOException e) { Program.Log("air log clear: " + e.Message); } catch (UnauthorizedAccessException e) { Program.Log("air log clear: " + e.Message); } } }
        static Row Parse(string l)
        {
            var f = (l ?? "").Split('\t'); if (f.Length < 9) return null; DateTime t; int ch, wd, rs;
            if (!DateTime.TryParseExact(f[0], "yyyy-MM-ddTHH", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) || !int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out ch) || !int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out wd) || !int.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out rs)) return null;
            return new Row { T = t, Bssid = f[1], Ssid = Program.Plain(f[2]), Band = f[3], Channel = ch, Width = wd, Rssi = rs, Sec = f[7], Mine = f[8] == "1" };
        }
        public static List<Row> Load() { lock (k) { try { return System.IO.File.Exists(File) ? System.IO.File.ReadAllLines(File).Take(20000).Select(Parse).Where(r => r != null).ToList() : new List<Row>(); } catch (IOException) { return new List<Row>(); } catch (UnauthorizedAccessException) { return new List<Row>(); } } }

        // ---------- the wars: per network, per day - signal, channel, and who was strongest on your channel
        public sealed class Net { public string Name = "", Band = ""; public bool Mine; public double Rssi; public int Channel, Width; public string Sec = ""; public string Moved = ""; public readonly Dictionary<DateTime, int> ChByDay = new Dictionary<DateTime, int>(); public readonly Dictionary<DateTime, double> RssiByDay = new Dictionary<DateTime, double>(); }
        public sealed class Wars { public List<Net> Nets = new List<Net>(); public List<DateTime> DaysSeen = new List<DateTime>(); public Dictionary<DateTime, string> Winner = new Dictionary<DateTime, string>(); public Dictionary<DateTime, double> Crowding = new Dictionary<DateTime, double>(); public int MyChannel; public string MyBand = "", Verdict = ""; public int Quiet = -1; }
        static double Pct(int rssi) { return Math.Max(0, Math.Min(100, 2 * (rssi + 100))); }
        public static Wars Analyse(List<Row> rows)
        {
            var w = new Wars(); if (rows.Count == 0) return w;
            w.DaysSeen = rows.Select(r => r.T.Date).Distinct().OrderBy(d => d).ToList();
            var mine = rows.Where(r => r.Mine).ToList(); if (mine.Count == 0) return w;
            // your band and channel: the one your router was on most of the hours
            var myMain = mine.GroupBy(r => r.Band + "|" + r.Channel).OrderByDescending(gr => gr.Count()).First().First(); w.MyBand = myMain.Band; w.MyChannel = myMain.Channel;
            // a network = its name on that band (hidden ones by address); one row per network per hour (the strongest of its access points)
            Func<Row, string> key = r => (r.Ssid == "" ? r.Bssid : r.Ssid) + "|" + r.Band;
            foreach (var gr in rows.Where(r => r.Band == w.MyBand).GroupBy(key))
            {
                var hourly = gr.GroupBy(r => r.T).Select(h => h.OrderByDescending(r => r.Rssi).First()).ToList();
                var n = new Net { Name = gr.First().Ssid == "" ? "(hidden network)" : gr.First().Ssid, Band = w.MyBand, Mine = gr.Any(r => r.Mine), Rssi = hourly.Average(r => (double)r.Rssi), Sec = gr.Last().Sec };
                var last = hourly.OrderBy(r => r.T).Last(); n.Channel = last.Channel; n.Width = last.Width;
                foreach (var d in hourly.GroupBy(r => r.T.Date)) { n.ChByDay[d.Key] = d.GroupBy(r => r.Channel).OrderByDescending(c => c.Count()).First().Key; n.RssiByDay[d.Key] = d.Average(r => (double)r.Rssi); }
                // moved onto your channel: on another channel earlier, on yours since
                var byDay = n.ChByDay.OrderBy(kv => kv.Key).ToList();
                if (!n.Mine && byDay.Count >= 2 && byDay.Last().Value == w.MyChannel) { int i = byDay.FindLastIndex(kv => kv.Value != w.MyChannel); if (i >= 0) n.Moved = "ch " + byDay[i].Value + " → " + w.MyChannel + " " + byDay[i + 1].Key.ToString("ddd", CultureInfo.CurrentCulture); }
                w.Nets.Add(n);
            }
            w.Nets = w.Nets.OrderByDescending(n => n.Mine).ThenByDescending(n => n.Rssi).Take(15).ToList();
            foreach (var d in w.DaysSeen)
            {
                var onMine = w.Nets.Where(n => n.ChByDay.ContainsKey(d) && (n.ChByDay[d] == w.MyChannel || LinkInfo.Overlaps(n.ChByDay[d], w.MyChannel, w.MyBand))).ToList();
                var best = onMine.OrderByDescending(n => n.RssiByDay[d]).FirstOrDefault(); if (best != null) w.Winner[d] = best.Mine ? "you" : best.Name;
                w.Crowding[d] = onMine.Where(n => !n.Mine).Sum(n => Math.Pow(Pct((int)n.RssiByDay[d]) / 100, 2)) * 100;
            }
            // the quietest channel all week, among the usual ones for the band
            int[] cand = w.MyBand.StartsWith("2.4", StringComparison.Ordinal) ? new[] { 1, 6, 11 } : w.MyBand.StartsWith("5", StringComparison.Ordinal) ? new[] { 36, 40, 44, 48, 149, 153, 157, 161 } : Array.Empty<int>();
            if (cand.Length > 0) w.Quiet = cand.OrderBy(c => w.Nets.Where(n => !n.Mine && (n.Channel == c || LinkInfo.Overlaps(n.Channel, c, w.MyBand))).Sum(n => Math.Pow(Pct((int)n.Rssi) / 100, 2))).First();
            int won = w.Winner.Count(kv => kv.Value == "you"), days = w.DaysSeen.Count; int sharing = w.Nets.Count(n => !n.Mine && n.Channel == w.MyChannel);
            var mover = w.Nets.FirstOrDefault(n => n.Moved != "");
            w.Verdict = "You were the strongest on your channel " + won + " of " + days + " day" + (days == 1 ? "" : "s") + (sharing > 0 ? ", and " + sharing + " neighbour" + (sharing == 1 ? "" : "s") + " now share" + (sharing == 1 ? "s" : "") + " channel " + w.MyChannel : " - nobody else is on channel " + w.MyChannel) + "." + (mover != null ? " " + mover.Name + " moved onto it (" + mover.Moved + ")." : "") + (w.Quiet > 0 && w.Quiet != w.MyChannel && sharing > 0 ? " Channel " + w.Quiet + " has been the quietest this week." : "");
            return w;
        }
    }
}
