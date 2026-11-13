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
        public sealed class Row { public DateTime T; public string Bssid = "", Ssid = "", Band = "", Sec = ""; public int Channel, Width, Rssi; public bool Mine, Same; }   // Mine = the access point this PC was connected to; Same = another one with your network's name (mesh, extender)

        public static void Record(Monitor m)
        {
            if (!Cfg.History) { Clear(); return; }   // History off: nothing is kept - an old log goes within the hour (the next hourly tick)
            var hour = DateTime.Now.Date.AddHours(DateTime.Now.Hour); if (hour == lastHour) return;
            bool wifi; string myB, mySsid; lock (m.L) { wifi = m.HasWifi && m.AdapterKind == "Wi-Fi"; myB = (m.Bssid ?? "").Replace('-', ':'); mySsid = m.Ssid ?? ""; }
            if (!wifi) return;
            string err; var bss = WlanApi.List(false, out err); if (bss.Count == 0) return;   // location off, or nothing heard: write nothing
            lastHour = hour;
            var lines = bss.Where(b => b.Channel > 0).OrderByDescending(b => b.Rssi).Take(40).Select(b => string.Join("\t", hour.ToString("yyyy-MM-ddTHH", CultureInfo.InvariantCulture), b.Bssid, (b.Ssid ?? "").Replace('\t', ' '), b.Band, b.Channel.ToString(CultureInfo.InvariantCulture), b.Width.ToString(CultureInfo.InvariantCulture), b.Rssi.ToString(CultureInfo.InvariantCulture), b.Sec, string.Equals(b.Bssid, myB, StringComparison.OrdinalIgnoreCase) ? "1" : mySsid != "" && b.Ssid == mySsid ? "2" : "0")).ToList();
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
        public static void Clear() { lock (k) { try { lastHour = DateTime.MinValue; if (System.IO.File.Exists(File)) System.IO.File.Delete(File); } catch (IOException e) { Program.Log("air log clear: " + e.Message); } catch (UnauthorizedAccessException e) { Program.Log("air log clear: " + e.Message); } } }
        static Row Parse(string l)
        {
            var f = (l ?? "").Split('\t'); if (f.Length < 9) return null; DateTime t; int ch, wd, rs;
            if (!DateTime.TryParseExact(f[0], "yyyy-MM-ddTHH", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) || !int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out ch) || !int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out wd) || !int.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out rs)) return null;
            return new Row { T = t, Bssid = f[1], Ssid = Program.Plain(f[2]), Band = f[3], Channel = ch, Width = wd, Rssi = rs, Sec = f[7], Mine = f[8] == "1", Same = f[8] == "2" };
        }
        public static List<Row> Load() { lock (k) { try { return System.IO.File.Exists(File) ? System.IO.File.ReadAllLines(File).Take(20000).Select(Parse).Where(r => r != null).ToList() : new List<Row>(); } catch (IOException) { return new List<Row>(); } catch (UnauthorizedAccessException) { return new List<Row>(); } } }

        // ---------- the wars: per network, per day - signal, channel, and who was strongest on your channel
        public sealed class Net { public string Name = "", Band = ""; public bool Mine, Same; public double Rssi; public int Channel, Width; public string Sec = ""; public string Moved = ""; public readonly Dictionary<DateTime, int> ChByDay = new Dictionary<DateTime, int>(); public readonly Dictionary<DateTime, int> WidthByDay = new Dictionary<DateTime, int>(); public readonly Dictionary<DateTime, double> RssiByDay = new Dictionary<DateTime, double>(); }
        public sealed class Wars { public List<Net> Nets = new List<Net>(); public List<DateTime> DaysSeen = new List<DateTime>(); public Dictionary<DateTime, string> Winner = new Dictionary<DateTime, string>(); public Dictionary<DateTime, double> Crowding = new Dictionary<DateTime, double>(); public Dictionary<DateTime, int> MyChByDay = new Dictionary<DateTime, int>(); public int MyChannel, MyWidth; public string MyBand = "", Verdict = ""; public int Quiet = -1; public bool MovedLately; }
        static double Pct(int rssi) { return Math.Max(0, Math.Min(100, 2 * (rssi + 100))); }

        // the channels an access point really occupies: on 5 and 6 GHz a 40, 80 or 160 MHz network covers 2, 4 or 8 of the
        // 20 MHz channels in a fixed block (36-48 is one 80 MHz block). On 2.4 GHz channels 5 apart don't overlap.
        public static bool Overlap(int a, int wa, int b, int wb, string band)
        {
            if (a <= 0 || b <= 0) return false;
            if (band.StartsWith("2.4", StringComparison.Ordinal)) return Math.Abs(a - b) < 5;
            int a0, a1, b0, b1; Block(a, wa, band, out a0, out a1); Block(b, wb, band, out b0, out b1);
            return a0 <= b1 && b0 <= a1;
        }
        static void Block(int ch, int width, string band, out int lo, out int hi)
        {
            int span = width >= 160 ? 32 : width >= 80 ? 16 : width >= 40 ? 8 : 4;   // in channel numbers (one 20 MHz channel = 4)
            int anchor = band.StartsWith("6", StringComparison.Ordinal) ? 1 : ch >= 149 ? 149 : 36;
            if (ch < anchor) { lo = hi = ch; return; }
            lo = anchor + (ch - anchor) / span * span; hi = lo + span - 4;
        }

        public static Wars Analyse(List<Row> rows)
        {
            var w = new Wars(); if (rows.Count == 0) return w;
            w.DaysSeen = rows.Select(r => r.T.Date).Distinct().OrderBy(d => d).ToList();
            var mine = rows.Where(r => r.Mine).OrderBy(r => r.T).ToList(); if (mine.Count == 0) return w;
            // your band and channel NOW: the access point you were on in the newest hour (routers change channel by themselves)
            var newest = mine.Last(); w.MyBand = newest.Band; w.MyChannel = newest.Channel; w.MyWidth = newest.Width;
            foreach (var d in mine.Where(r => r.Band == w.MyBand).GroupBy(r => r.T.Date)) w.MyChByDay[d.Key] = d.GroupBy(r => r.Channel).OrderByDescending(c => c.Count()).First().Key;
            w.MovedLately = w.MyChByDay.Values.Distinct().Count() > 1;
            // a network = its name on that band (hidden ones by address); yours = the access point you were connected to;
            // other access points with your network's name (mesh, extenders) are kept apart and never counted as neighbours
            // two neighbours both called "xfinitywifi" are two access points: each is its own network (by its address)
            Func<Row, string> key = r => r.Mine ? "\u0001mine" : (r.Ssid == "" ? r.Bssid : r.Same || (r.Bssid ?? "") == "" ? r.Ssid : r.Ssid + "\u0003" + r.Bssid) + (r.Same ? "\u0002same" : "") + "|" + r.Band;
            foreach (var gr in rows.Where(r => r.Band == w.MyBand).GroupBy(key))
            {
                var hourly = gr.GroupBy(r => r.T).Select(h => h.OrderByDescending(r => r.Rssi).First()).ToList();
                var first = gr.First();
                var n = new Net { Name = first.Mine ? first.Ssid : first.Ssid == "" ? "(hidden network)" : first.Same ? first.Ssid + " (another access point of yours)" : first.Ssid, Band = w.MyBand, Mine = first.Mine, Same = first.Same, Rssi = hourly.Average(r => (double)r.Rssi), Sec = gr.Last().Sec };
                var last = hourly.OrderBy(r => r.T).Last(); n.Channel = last.Channel; n.Width = last.Width;
                foreach (var d in hourly.GroupBy(r => r.T.Date))
                {
                    var top = d.GroupBy(r => r.Channel).OrderByDescending(c => c.Count()).First();
                    n.ChByDay[d.Key] = top.Key; n.WidthByDay[d.Key] = top.Max(r => r.Width); n.RssiByDay[d.Key] = d.Average(r => (double)r.Rssi);
                }
                // moved onto your channel: elsewhere earlier, on the channel you're on now since
                var byDay = n.ChByDay.OrderBy(kv => kv.Key).ToList();
                if (!n.Mine && !n.Same && byDay.Count >= 2 && Overlap(byDay.Last().Value, n.WidthByDay[byDay.Last().Key], w.MyChannel, w.MyWidth, w.MyBand))
                {
                    int i = byDay.FindLastIndex(kv => !Overlap(kv.Value, n.WidthByDay[kv.Key], w.MyChannel, w.MyWidth, w.MyBand));
                    if (i >= 0) n.Moved = "ch " + byDay[i].Value + " → " + byDay[i + 1].Value + " " + byDay[i + 1].Key.ToString("ddd", CultureInfo.CurrentCulture);
                }
                w.Nets.Add(n);
            }
            w.Nets = w.Nets.OrderByDescending(n => n.Mine).ThenByDescending(n => n.Rssi).Take(15).ToList();
            var me = w.Nets.FirstOrDefault(n => n.Mine);
            // each day against the channel YOU were on that day
            foreach (var d in w.DaysSeen)
            {
                int myCh; if (!w.MyChByDay.TryGetValue(d, out myCh)) continue;
                int myW; if (me == null || !me.WidthByDay.TryGetValue(d, out myW)) myW = w.MyWidth;
                var onMine = w.Nets.Where(n => n.ChByDay.ContainsKey(d) && (n.Mine || Overlap(n.ChByDay[d], n.WidthByDay[d], myCh, myW, w.MyBand))).ToList();
                var best = onMine.Where(n => n.Mine || !n.Same).OrderByDescending(n => n.RssiByDay[d]).FirstOrDefault(); if (best != null) w.Winner[d] = best.Mine ? "you" : best.Name;
                w.Crowding[d] = onMine.Where(n => !n.Mine && !n.Same).Sum(n => Math.Pow(Pct((int)n.RssiByDay[d]) / 100, 2)) * 100;
            }
            // the quietest channel all week, among the usual ones for the band - judged at your own width
            int[] cand = w.MyBand.StartsWith("2.4", StringComparison.Ordinal) ? new[] { 1, 6, 11 } : w.MyBand.StartsWith("5", StringComparison.Ordinal) ? (w.MyWidth >= 80 ? new[] { 36, 52, 100, 116, 149 } : w.MyWidth >= 40 ? new[] { 36, 44, 52, 60, 100, 108, 149, 157 } : new[] { 36, 40, 44, 48, 149, 153, 157, 161 }) : Array.Empty<int>();
            // 5 GHz: 149+ and the radar (DFS) channels only when they're already in use around here - proof they're allowed (as Least crowded does)
            if (w.MyBand.StartsWith("5", StringComparison.Ordinal))
            {
                var seen = new HashSet<int>(w.Nets.Select(n => n.Channel)); seen.Add(w.MyChannel);
                cand = cand.Where(c => c <= 48 || (c >= 149 ? seen.Any(z => z >= 149) : seen.Any(z => z >= 52 && z <= 144))).ToArray();
            }
            if (cand.Length > 0) w.Quiet = cand.OrderBy(c => w.Nets.Where(n => !n.Mine && !n.Same && Overlap(n.Channel, n.Width, c, w.MyWidth, w.MyBand)).Sum(n => Math.Pow(Pct((int)n.Rssi) / 100, 2))).ThenBy(c => c == w.MyChannel ? 0 : 1).First();
            int won = w.Winner.Count(kv => kv.Value == "you"), days = w.Winner.Count;
            var sharingNow = w.Nets.Where(n => !n.Mine && !n.Same && Overlap(n.Channel, n.Width, w.MyChannel, w.MyWidth, w.MyBand)).ToList(); int sharing = sharingNow.Count;
            var mover = w.Nets.FirstOrDefault(n => n.Moved != "");
            bool quietIsMine = w.Quiet > 0 && Overlap(w.Quiet, w.MyWidth, w.MyChannel, w.MyWidth, w.MyBand);
            string chName = "channel " + w.MyChannel + (w.MyWidth >= 40 && !w.MyBand.StartsWith("2.4", StringComparison.Ordinal) ? " (" + w.MyWidth + " MHz)" : "");
            w.Verdict = "You were the strongest on your channel " + won + " of " + days + " day" + (days == 1 ? "" : "s")
                + (sharing > 0 ? ", and " + sharing + " neighbour" + (sharing == 1 ? "" : "s") + " now overlap" + (sharing == 1 ? "s" : "") + " your " + chName : " - nobody else overlaps your " + chName + " now") + "."
                + (w.MovedLately ? " Your router changed channel this week (it's on " + w.MyChannel + " now)." : "")
                + (mover != null ? " " + mover.Name + " moved onto it (" + mover.Moved + ")." : "")
                + (w.Quiet > 0 && !quietIsMine && sharing > 0 ? " Channel " + w.Quiet + " has been the quietest this week." : "");
            return w;
        }
    }
}
