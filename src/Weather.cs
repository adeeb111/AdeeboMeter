// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Weather (Adeeb, 14 Nov)
    // Off until you turn it on. The place is one you type in Settings (corporate PCs keep location off) - never worked out
    // from the PC or its address. Open-Meteo: free, no account, CC BY 4.0. What leaves the PC: the place name once to
    // find it, then only its coordinates.
    //   Normal  - one request for today and the next 2 days, hour by hour; then quiet until less than a day is left.
    //   Better  - the same, refreshed every 6 hours while the PC is on (never catching up on missed ones).
    // Only the coming days and the last one are kept (weather.txt, about 2 KB) - no history, no trends.
    // It shows as the buddy's face for 2-3 minutes a few times an hour when the hour is hot, cold, rainy, snowy or stormy
    // (never in the Quiet corner, during a call or a problem), and as one line in the widget if you want it.
    static class Weather
    {
        public sealed class Hour { public DateTime Utc; public double Temp = double.NaN, Feels = double.NaN, Wind = double.NaN; public int Code = -1, Pop = -1, Hum = -1, WindFrom = -1; }   // 1.1.6: humidity %, wind km/h and where it comes from (degrees)
        public sealed class Place { public string Name = ""; public double Lat, Lon; }
        static readonly char[] Semi = { ';' };
        static readonly object k = new object();
        static List<Hour> hours = new List<Hour>(); static DateTime fetchedUtc = DateTime.MinValue, triedUtc = DateTime.MinValue, checkedUtc = DateTime.MinValue; static string forPlace = "";
        static volatile bool busy, loaded; public static string Note = "";
        public const double HotC = 28, ColdC = -10;
        public const int BetterHours = 6;
        static readonly DateTime started = DateTime.UtcNow;

        // ---------- the place (Cfg.WeatherPlace = "lat;lon;name")
        public static string CleanPlace(string v)
        {
            var p = (v ?? "").Split(Semi, 3); double la, lo;
            if (p.Length < 3 || !double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out la) || !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lo) || !Finite(la) || !Finite(lo) || Math.Abs(la) > 90 || Math.Abs(lo) > 180) return "";
            string n = new string(p[2].Where(c => c != '|' && c != '=' && c != ';' && !char.IsControl(c)).ToArray()).Trim(); if (n.Length > 80) n = n.Substring(0, 80);
            return n == "" ? "" : Line(new Place { Lat = la, Lon = lo, Name = n });
        }
        public static string Line(Place p) { return p.Lat.ToString("0.####", CultureInfo.InvariantCulture) + ";" + p.Lon.ToString("0.####", CultureInfo.InvariantCulture) + ";" + p.Name.Replace(";", ",").Replace("=", " ").Replace("|", " "); }
        public static string PlaceName() { var p = Current; return p == null ? "" : p.Name.Split(',')[0].Trim(); }
        // the forecast hour nearest a moment (a kick-off), within 90 minutes - or null
        public static Hour HourAt(DateTime utc) { return Ahead(DateTime.UtcNow, 96).Where(h => Math.Abs((h.Utc - utc).TotalMinutes) <= 90).OrderBy(h => Math.Abs((h.Utc - utc).TotalMinutes)).FirstOrDefault(); }
        public static Place Current
        {
            get
            {
                var p = (Cfg.WeatherPlace ?? "").Split(Semi, 3); double la, lo;
                if (p.Length < 3 || !double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out la) || !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lo) || !Finite(la) || !Finite(lo) || Math.Abs(la) > 90 || Math.Abs(lo) > 180) return null;
                return new Place { Lat = la, Lon = lo, Name = p[2] };
            }
        }
        static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        public static bool Active { get { return Cfg.WeatherOn && Current != null && !Policy.NoOnline; } }

        // ---------- finding a place by name ("Ottawa", "Dhaka, Bangladesh", "Ottawa, Illinois")
        // the servers (24 Nov, Adeeb: "give me an option to change the server"): any host that answers Open-Meteo's API -
        // a mirror or a self-hosted copy. Only a bare host name (and port) is kept; HTTPS always.
        public const string GeoDefault = "geocoding-api.open-meteo.com", ApiDefault = "api.open-meteo.com";
        public static string GeoHost { get { var h = Cfg.WeatherGeo; return string.IsNullOrEmpty(h) ? GeoDefault : h; } }
        public static string ApiHost { get { var h = Cfg.WeatherApi; return string.IsNullOrEmpty(h) ? ApiDefault : h; } }
        public static string CleanServer(string v)
        {
            v = (v ?? "").Trim(); if (v.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) v = v.Substring(8); else if (v.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) v = v.Substring(7);
            int sl = v.IndexOf('/'); if (sl >= 0) v = v.Substring(0, sl);
            if (!Regex.IsMatch(v, @"^[A-Za-z0-9.-]{1,120}(:[0-9]{1,5})?$")) return "";
            int c = v.IndexOf(':'); if (c > 0) { int port = int.Parse(v.Substring(c + 1), System.Globalization.CultureInfo.InvariantCulture); if (port < 1 || port > 65535) return ""; }   // review 24 Nov: :99999 is no port
            return v.ToLowerInvariant();
        }
        public static string SearchUrl(string typed)
        {
            string q = (typed ?? "").Split(',')[0].Trim(); if (q.Length > 60) q = q.Substring(0, 60);
            bool qualified = (typed ?? "").IndexOf(',') >= 0;   // AM-CG-154: "Springfield, Illinois" - 10 Springfields may not include Illinois
            return "https://" + GeoHost + "/v1/search?count=" + (qualified ? "100" : "10") + "&language=en&format=json&name=" + Uri.EscapeDataString(q);
        }
        static readonly Regex objRx = new Regex("\\{[^{}]*\"latitude\"[^{}]*\\}"), strRx = new Regex("\"(name|country|admin1)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""), numRx = new Regex("\"(latitude|longitude)\"\\s*:\\s*(-?[0-9.]+)");
        public static List<Place> ParsePlaces(string json, string typed)
        {
            var list = new List<Place>(); var parts = (typed ?? "").Split(',').Skip(1).Select(z => z.Trim().ToLowerInvariant()).Where(z => z != "").ToList();
            foreach (Match o in objRx.Matches(json ?? ""))
            {
                var d = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Match m in strRx.Matches(o.Value)) { string v; try { v = Regex.Unescape(m.Groups[2].Value); } catch (ArgumentException) { v = m.Groups[2].Value; } d[m.Groups[1].Value] = v; }
                foreach (Match m in numRx.Matches(o.Value)) d[m.Groups[1].Value] = m.Groups[2].Value;
                double la, lo; string nm, la0, lo0;
                if (!d.TryGetValue("name", out nm) || !d.TryGetValue("latitude", out la0) || !d.TryGetValue("longitude", out lo0)) continue;
                if (!double.TryParse(la0, NumberStyles.Float, CultureInfo.InvariantCulture, out la) || !double.TryParse(lo0, NumberStyles.Float, CultureInfo.InvariantCulture, out lo) || !Finite(la) || !Finite(lo) || Math.Abs(la) > 90 || Math.Abs(lo) > 180) continue;
                string a1, co; d.TryGetValue("admin1", out a1); d.TryGetValue("country", out co);
                var names = new[] { nm, a1, co }.Where(z => !string.IsNullOrEmpty(z) && z != null).Distinct().ToList();
                string full = string.Join(", ", names);
                if (parts.Count > 0 && !parts.All(pt => names.Any(z => z.IndexOf(pt, StringComparison.OrdinalIgnoreCase) >= 0))) continue;   // "Ottawa, Illinois" keeps only Illinois
                if (list.Any(z => z.Name == full)) continue;
                list.Add(new Place { Name = full, Lat = la, Lon = lo });
            }
            return list.Take(8).ToList();
        }
        // on a background thread; done gets the matches (empty: none) or null with Note set
        public static void Find(string typed, Action<List<Place>> done)
        {
            if (Policy.NoOnline) { Note = "Your IT team turned online lookups off (AdeeboMeter.policy)."; done(null); return; }
            new Thread(() =>
            {
                List<Place> r = null;
                try { r = ParsePlaces(Get(SearchUrl(typed)), typed); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Note = "Couldn't reach " + GeoHost + ": " + HttpSmall.Why(e).TrimEnd('.') + "."; Program.Log("weather find: " + e.Message); }
                done(r);
            }) { IsBackground = true, Name = "weather find" }.Start();
        }

        // ---------- the forecast
        public static string ForecastUrl(Place p)
        {
            return "https://" + ApiHost + "/v1/forecast?latitude=" + p.Lat.ToString("0.####", CultureInfo.InvariantCulture) + "&longitude=" + p.Lon.ToString("0.####", CultureInfo.InvariantCulture)
                + "&hourly=temperature_2m,apparent_temperature,weather_code,precipitation_probability,relative_humidity_2m,wind_speed_10m,wind_direction_10m&forecast_days=3&timezone=auto&timeformat=unixtime";
        }
        static double[] Nums(string json, string key)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\[([^\\]]*)\\]"); if (!m.Success) return null;
            return m.Groups[1].Value.Split(',').Select(z => { double v; return double.TryParse(z.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }).ToArray();
        }
        // the codes Open-Meteo uses (WMO): anything else is unknown, never "clear"
        static readonly HashSet<int> KnownCodes = new HashSet<int> { 0, 1, 2, 3, 45, 48, 51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 71, 73, 75, 77, 80, 81, 82, 85, 86, 95, 96, 99 };
        public static int PlaceOffset = int.MinValue;   // the place's own UTC offset (seconds), to say when its times aren't the PC's
        // unix times (AM-CG-159); older answers with local "2026-11-14T00:00" times still read. Every value is checked: a
        // temperature outside -90..60 C, a chance outside 0..100 % or an unknown code is "unknown" (AM-CG-156)
        public static List<Hour> ParseForecast(string json)
        {
            var list = new List<Hour>(); json = json ?? "";
            var om = Regex.Match(json, "\"utc_offset_seconds\"\\s*:\\s*(-?[0-9]+)"); long off = 0;
            if (om.Success && (!long.TryParse(om.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out off) || Math.Abs(off) > 18 * 3600)) throw new InvalidDataException("the place's time offset doesn't make sense");   // AM-CG-158
            PlaceOffset = om.Success ? (int)off : int.MinValue;
            var tm = Regex.Match(json, "\"time\"\\s*:\\s*\\[([^\\]]*)\\]"); if (!tm.Success) return list;
            var raw = tm.Groups[1].Value.Split(',').Select(z => z.Trim().Trim('"')).Where(z => z != "").ToList();
            double[] t = Nums(json, "temperature_2m"), f = Nums(json, "apparent_temperature"), c = Nums(json, "weather_code"), p = Nums(json, "precipitation_probability"), hu = Nums(json, "relative_humidity_2m"), ws = Nums(json, "wind_speed_10m"), wd = Nums(json, "wind_direction_10m");
            for (int i = 0; i < raw.Count; i++)
            {
                DateTime utc; long unix;
                if (long.TryParse(raw[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out unix) && unix > 946684800 && unix < 4102444800) utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix);
                else { DateTime local; if (!DateTime.TryParseExact(raw[i], "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out local)) continue; utc = DateTime.SpecifyKind(local.AddSeconds(-off), DateTimeKind.Utc); }
                var h = new Hour { Utc = utc };
                if (t != null && i < t.Length && Finite(t[i]) && t[i] > -90 && t[i] < 60) h.Temp = t[i];
                if (f != null && i < f.Length && Finite(f[i]) && f[i] > -100 && f[i] < 70) h.Feels = f[i];
                if (c != null && i < c.Length && Finite(c[i]) && c[i] == Math.Floor(c[i]) && KnownCodes.Contains((int)c[i])) h.Code = (int)c[i];
                if (p != null && i < p.Length && Finite(p[i]) && p[i] >= 0 && p[i] <= 100) h.Pop = (int)p[i];
                if (hu != null && i < hu.Length && Finite(hu[i]) && hu[i] >= 0 && hu[i] <= 100) h.Hum = (int)Math.Round(hu[i]);
                if (ws != null && i < ws.Length && Finite(ws[i]) && ws[i] >= 0 && ws[i] < 500) h.Wind = ws[i];
                if (wd != null && i < wd.Length && Finite(wd[i]) && wd[i] >= 0 && wd[i] <= 360) h.WindFrom = (int)Math.Round(wd[i]) % 360;
                list.Add(h);
            }
            return list;
        }

        // ---------- when to ask (pure, for the self-test)
        public static bool NeedFetch(List<Hour> have, DateTime fetched, DateTime nowUtc, string acc)
        {
            if (have == null || have.Count == 0) return true;
            if ((have.Max(h => h.Utc) - nowUtc).TotalHours < 24) return true;                       // less than a day of forecast left
            if (have.Count(h => h.Utc > nowUtc && h.Utc <= nowUtc.AddHours(24) && h.Code >= 0 && !double.IsNaN(h.Temp)) < 18) return true;   // AM-CG-163: holes or unknown hours in the next day
            return acc == "better" && (nowUtc - fetched).TotalHours >= BetterHours;                  // Better: every 6 hours while the PC is on
        }
        // only the last day and what's ahead: 3-4 days at most
        public static List<Hour> Trim(List<Hour> l, DateTime nowUtc) { return l.Where(h => h.Utc >= nowUtc.AddHours(-24) && h.Utc <= nowUtc.AddDays(4)).OrderBy(h => h.Utc).ToList(); }

        // called from the widget's tick: cheap - looks once a minute, asks the internet only when NeedFetch says so
        public static void Tick()
        {
            if (!Active || busy) return;
            var now = DateTime.UtcNow;
            if ((now - checkedUtc).TotalSeconds < 60 || (now - started).TotalSeconds < 45) return;   // not in the first moments after start
            checkedUtc = now; Load();
            List<Hour> have; DateTime f; lock (k) { have = forPlace == Cfg.WeatherPlace ? hours.ToList() : new List<Hour>(); f = fetchedUtc; }
            if (!NeedFetch(have, f, now, Cfg.WeatherAcc) || (triedFor == Cfg.WeatherPlace && (now - triedUtc).TotalMinutes < 30)) return;   // a failed try waits half an hour (for that place only)
            Fetch();
        }
        static string triedFor = ""; static int gen; static volatile bool again;
        public static void Fetch()
        {
            var p = Current; if (p == null || Policy.NoOnline) return;
            if (busy) { again = true; return; }   // AM-CG-161: the new place goes as soon as the old request is done
            busy = true; triedUtc = DateTime.UtcNow; string place = Cfg.WeatherPlace; triedFor = place; int g0 = gen;
            new Thread(() =>
            {
                try
                {
                    var l = Trim(ParseForecast(Get(ForecastUrl(p))), DateTime.UtcNow);
                    if (l.Count == 0) throw new InvalidDataException("no hours in the answer");
                    // AM-CG-160: removed, switched off or changed while this was out - keep nothing of it
                    if (g0 != gen || !Cfg.WeatherOn || Cfg.WeatherPlace != place) return;
                    lock (k) { hours = l; fetchedUtc = DateTime.UtcNow; forPlace = place; } Note = ""; Save();
                    Program.Log("weather: " + l.Count + " hours");   // AM-CG-164: never the place's name in the log
                }
                catch (InvalidDataException e) { if (g0 == gen) Note = string.Format(L.T("The answer from {0} didn't make sense - trying again in half an hour."), ApiHost); Program.Log("weather: " + e.Message); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; if (g0 == gen) Note = string.Format(L.T("Couldn't reach {0} - trying again in half an hour."), ApiHost); Program.Log("weather: " + e.GetType().Name); }
                finally { busy = false; if (again) { again = false; if (Cfg.WeatherPlace != place) Fetch(); } }
            }) { IsBackground = true, Name = "weather" }.Start();
        }
        static string Get(string url) { return HttpSmall.Get(url, 300); }   // AM-CG-165: capped while reading

        // ---------- kept on disk: "place=..." / "fetched=..." / "utc|temp|feels|code|pop" lines
        static string File0 { get { return Path.Combine(Program.DataDir, "weather.txt"); } }
        static void Load()
        {
            if (loaded) return; loaded = true;
            try
            {
                if (!File.Exists(File0)) return;
                var l = new List<Hour>(); string place = ""; DateTime f = DateTime.MinValue;
                foreach (var line in File.ReadAllLines(File0))
                {
                    if (line.StartsWith("place=", StringComparison.Ordinal)) { place = line.Substring(6); continue; }
                    if (line.StartsWith("fetched=", StringComparison.Ordinal)) { DateTime.TryParse(line.Substring(8), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out f); continue; }
                    var c = line.Split('|'); DateTime u; double t = double.NaN, fe = double.NaN; int co = -1, po = -1;
                    if (c.Length < 5 || !DateTime.TryParseExact(c[0], "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out u)) continue;
                    if ((c[1] != "" && !double.TryParse(c[1], NumberStyles.Float, CultureInfo.InvariantCulture, out t)) | (c[2] != "" && !double.TryParse(c[2], NumberStyles.Float, CultureInfo.InvariantCulture, out fe))
                        | (c[3] != "" && !int.TryParse(c[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out co)) | (c[4] != "" && !int.TryParse(c[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out po))) continue;   // AM-CG-162
                    if (c[3] != "" && !KnownCodes.Contains(co)) co = -1; if (c[4] != "" && (po < 0 || po > 100)) po = -1;
                    var hr = new Hour { Utc = DateTime.SpecifyKind(u, DateTimeKind.Utc), Temp = c[1] == "" ? double.NaN : t, Feels = c[2] == "" ? double.NaN : fe, Code = c[3] == "" ? -1 : co, Pop = c[4] == "" ? -1 : po };
                    // hunt-5: humidity, wind and its direction (older files have five fields - those stay blank)
                    int hu; double wi; int wf;
                    if (c.Length > 5 && int.TryParse(c[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out hu) && hu >= 0 && hu <= 100) hr.Hum = hu;
                    if (c.Length > 6 && double.TryParse(c[6], NumberStyles.Float, CultureInfo.InvariantCulture, out wi) && wi >= 0 && wi < 500) hr.Wind = wi;
                    if (c.Length > 7 && int.TryParse(c[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out wf) && wf >= 0 && wf <= 360) hr.WindFrom = wf % 360;
                    l.Add(hr);
                }
                lock (k) { hours = Trim(l, DateTime.UtcNow); fetchedUtc = f; forPlace = place; }
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
        }
        static void Save()
        {
            try
            {
                var lines = new List<string>(); lock (k) { lines.Add("place=" + forPlace); lines.Add("fetched=" + fetchedUtc.ToString("o", CultureInfo.InvariantCulture));
                    foreach (var h in hours) lines.Add(h.Utc.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture) + "|" + N(h.Temp) + "|" + N(h.Feels) + "|" + (h.Code < 0 ? "" : h.Code.ToString(CultureInfo.InvariantCulture)) + "|" + (h.Pop < 0 ? "" : h.Pop.ToString(CultureInfo.InvariantCulture)) + "|" + (h.Hum < 0 ? "" : h.Hum.ToString(CultureInfo.InvariantCulture)) + "|" + N(h.Wind) + "|" + (h.WindFrom < 0 ? "" : h.WindFrom.ToString(CultureInfo.InvariantCulture))); }   /* hunt-5: humidity and wind kept too */
                SafeFile.WriteText(File0, string.Join("\r\n", lines) + "\r\n");   // bug-hunt L-320: temp file, then replace
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
        }
        static string N(double v) { return double.IsNaN(v) ? "" : v.ToString("0.#", CultureInfo.InvariantCulture); }
        // Remove (or turning it off): the place and the kept forecast go
        public static void Forget()
        {
            gen++;   // a fetch still out comes back to nothing (AM-CG-160)
            lock (k) { hours = new List<Hour>(); fetchedUtc = DateTime.MinValue; forPlace = ""; } Note = "";
            try { if (File.Exists(File0)) File.Delete(File0); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
        }

        // ---------- reading it
        public static Hour Now(DateTime nowUtc)
        {
            Load();
            lock (k)
            {
                if (forPlace != Cfg.WeatherPlace) return null;
                var h = hours.LastOrDefault(z => z.Utc <= nowUtc); return h != null && (nowUtc - h.Utc).TotalHours < 1.5 ? h : null;
            }
        }
        public static List<Hour> Ahead(DateTime nowUtc, int n) { Load(); lock (k) return forPlace != Cfg.WeatherPlace ? new List<Hour>() : hours.Where(z => z.Utc > nowUtc.AddHours(-1)).Take(n).ToList(); }
        public static DateTime Until { get { lock (k) return hours.Count == 0 ? DateTime.MinValue : hours.Max(h => h.Utc); } }
        public static DateTime Fetched { get { lock (k) return fetchedUtc; } }

        // hot / cold / rain / snow / storm - or null for an ordinary hour (feels-like for hot and cold, so a mild windless
        // Ottawa winter day isn't "cold" all day)
        public static string Kind(Hour h)
        {
            if (h == null) return null; int c = h.Code;
            if (c >= 95) return "storm";
            if ((c >= 71 && c <= 77) || c == 85 || c == 86) return "snow";
            if ((c >= 51 && c <= 67) || (c >= 80 && c <= 82)) return "rain";
            double f = double.IsNaN(h.Feels) ? h.Temp : h.Feels;
            if (!double.IsNaN(f) && f >= HotC) return "hot";
            if (!double.IsNaN(f) && f <= ColdC) return "cold";
            return null;
        }
        public static string EmojiOf(string kind) { switch (kind) { case "storm": return "26c8"; case "snow": return "1f328"; case "rain": return "1f327"; case "hot": return "1f975"; case "cold": return "1f976"; default: return null; } }
        public static string Words(int code)
        {
            if (code < 0) return ""; if (code == 0) return "clear"; if (code == 1) return "mostly clear"; if (code == 2) return "partly cloudy"; if (code == 3) return "cloudy";
            if (code == 45 || code == 48) return "fog"; if (code == 56 || code == 57) return "freezing drizzle"; if (code >= 51 && code <= 55) return "drizzle"; if (code == 66 || code == 67) return "freezing rain"; if (code >= 61 && code <= 65) return "rain"; if (code >= 71 && code <= 77) return "snow";
            if (code >= 80 && code <= 82) return "showers"; if (code == 85 || code == 86) return "snow showers"; return code == 96 || code == 99 ? "thunderstorm with hail" : code == 95 ? "thunderstorm" : "";
        }
        static bool Wet(int code) { return code >= 51 && code != 45 && code != 48; }
        public static string Temp(double c) { if (double.IsNaN(c)) return "-"; return Cfg.WeatherF ? (c * 9 / 5 + 32).ToString("0", CultureInfo.CurrentCulture) + "°F" : c.ToString("0", CultureInfo.CurrentCulture) + "°C"; }
        public static string TempShort(double c) { if (double.IsNaN(c)) return "-"; return (Cfg.WeatherF ? c * 9 / 5 + 32 : c).ToString("0", CultureInfo.CurrentCulture) + "°"; }
        static string At(DateTime utc) { return utc.ToLocalTime().ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H") ? "HH:mm" : AdeeboMeter.Draw.Tt("h tt"), CultureInfo.CurrentCulture).ToLowerInvariant(); }
        // "4°C · rain from 3 pm" / "-2°C · snow until 18:00" / "21°C · clear"
        public static string Text(DateTime nowUtc)
        {
            var h = Now(nowUtc); if (h == null) return null;
            string head = Temp(h.Temp), w = Words(h.Code); var next = Ahead(nowUtc, 13).Where(z => z.Utc > nowUtc).ToList();
            var known = next.TakeWhile(z => z.Code >= 0).ToList();   // AM-CG-157: a missing hour ends what can be said
            string zone = PlaceOffset != int.MinValue && Math.Abs(PlaceOffset - (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalSeconds) >= 3600 ? " " + L.T("(your PC's time)") : "";
            if (Wet(h.Code)) { var dry = known.FirstOrDefault(z => !Wet(z.Code)); return head + " · " + w + (dry != null ? " " + L.T("until") + " " + At(dry.Utc) + zone : ""); }
            var wet = known.FirstOrDefault(z => Wet(z.Code)); if (wet != null) return head + " · " + Words(wet.Code) + " " + L.T("from") + " " + At(wet.Utc) + zone;
            return head + (w != "" ? " · " + L.T(w) : "");
        }
        // the chip's few characters: "4°" with the hour's emoji beside it (drawn as a picture)
        public static string Chip(DateTime nowUtc) { if (!Active || !Cfg.WWeather) return null; var h = Now(nowUtc); return h == null ? null : TempShort(h.Temp); }
        public static string ChipEmoji(DateTime nowUtc) { var h = Now(nowUtc); return EmojiOf(Kind(h)); }

        // the face: 2.5 minutes three times an hour (:12, :32, :52) when the hour is notable
        public static bool InWindow(DateTime local) { double m = local.Minute + local.Second / 60.0; return (m >= 12 && m < 14.5) || (m >= 32 && m < 34.5) || (m >= 52 && m < 54.5); }
        public static string Face(DateTime nowUtc) { if (!Active || !Cfg.WWeather || !Moments.On || !InWindow(nowUtc.ToLocalTime())) return null; return EmojiOf(Kind(Now(nowUtc))); }

        // what Settings says under the switch
        public static string Status()
        {
            var p = Current; if (p == null) return "";
            if (busy) return L.T("Fetching the forecast...");
            var u = Until; if (u == DateTime.MinValue || forPlace != Cfg.WeatherPlace) return Note != "" ? L.T(Note) : L.T("The forecast comes within a minute or two.");
            var nowU = DateTime.UtcNow; DateTime next = Cfg.WeatherAcc == "better" ? Fetched.AddHours(BetterHours) : u.AddHours(-24); if (next < nowU) next = nowU;
            return L.T("Forecast until") + " " + u.ToLocalTime().ToString("ddd d MMM", CultureInfo.CurrentCulture) + " · " + L.T("next check") + " " + (next.ToLocalTime().Date == DateTime.Today ? L.T("today") + " " + At(next) : next.ToLocalTime().ToString("ddd", CultureInfo.CurrentCulture) + " " + At(next)) + (Note != "" ? " · " + L.T(Note) : "");
        }
        // self-test
        internal static void Inject(List<Hour> l, string place) { lock (k) { hours = l; forPlace = place; fetchedUtc = DateTime.UtcNow; loaded = true; } }
    }
}
