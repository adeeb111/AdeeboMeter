// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Speed test - only when you press it, to the server you pick
    // Servers: Cloudflare's speed-test service (the nearest of its data centres answers; download and upload) and the public
    // test files big hosting companies publish in each of their data centres (download only) - so you can test to a city.
    // The list is a plain file you can edit (speedservers.txt in AdeeboMeter's data folder); a file URL you type works too.
    // About 8 s each way, with pings to that server while the line is full. Capped: 400 MB down, 100 MB up.
    static class SpeedTest
    {
        // a public address that is really out there: not private, and not the documentation or benchmarking ranges either
        static bool Reachable(IPAddress ip)
        {
            if (!Watch.IsPublic(ip.ToString())) return false;
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4(); var b = ip.GetAddressBytes();
            if (b.Length == 4) return !((b[0] == 192 && b[1] == 0 && b[2] == 2) || (b[0] == 198 && b[1] == 51 && b[2] == 100) || (b[0] == 203 && b[1] == 0 && b[2] == 113) || (b[0] == 198 && (b[1] == 18 || b[1] == 19)));
            return !(b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8);   // 2001:db8::/32
        }
        public const int MaxDownMB = 400, MaxUpMB = 100, Seconds = 8;
        public sealed class Server
        {
            public string Id = "", Provider = "", Where = "", Down = "", Up = "";
            public string Host { get { Uri u; return Uri.TryCreate(Down, UriKind.Absolute, out u) ? u.Host : ""; } }
            public string Label { get { return Provider + (Where != "" ? " · " + Where : ""); } }
        }
        public sealed class Result { public DateTime T; public string Server = "", Where = ""; public double Down = double.NaN, Up = double.NaN, Idle = double.NaN, LoadDown = double.NaN, LoadUp = double.NaN, MB; public bool NoUpload; public string Grade = "", Error = ""; }
        public static Result Last;   // shown on the home page until the app closes (and kept in the day's events)
        public static volatile bool Busy;

        // ---------- the list. Cloudflare first (it picks its nearest data centre itself), then cities, nearest-to-Canada first
        // within each provider. Each file is a public test file the provider publishes for exactly this. Every one answered from
        // a home line in Ottawa on 11 Nov 2026; Linode's and Hetzner's test servers didn't (no answer / connection closed) - left out.
        static readonly string[][] Vultr = { new[] { "tor-ca", "Toronto" }, new[] { "nj-us", "New Jersey" }, new[] { "il-us", "Chicago" }, new[] { "ga-us", "Atlanta" }, new[] { "tx-us", "Dallas" }, new[] { "fl-us", "Miami" }, new[] { "wa-us", "Seattle" }, new[] { "sjo-ca-us", "Silicon Valley" }, new[] { "lax-ca-us", "Los Angeles" }, new[] { "lon-gb", "London" }, new[] { "par-fr", "Paris" }, new[] { "ams-nl", "Amsterdam" }, new[] { "fra-de", "Frankfurt" }, new[] { "sgp", "Singapore" }, new[] { "hnd-jp", "Tokyo" }, new[] { "sel-kor", "Seoul" }, new[] { "syd-au", "Sydney" } };
        public static List<Server> BuiltIn()
        {
            var l = new List<Server> { new Server { Id = "cloudflare", Provider = "Cloudflare", Where = "nearest", Down = "https://speed.cloudflare.com/__down?bytes=25000000", Up = "https://speed.cloudflare.com/__up" } };
            l.AddRange(Vultr.Select(v => new Server { Id = "vultr-" + v[0], Provider = "Vultr", Where = v[1], Down = "https://" + v[0] + "-ping.vultr.com/vultr.com.100MB.bin" }));
            l.Add(new Server { Id = "ovh-ca", Provider = "OVHcloud", Where = "Beauharnois (Montreal)", Down = "https://proof.ovh.ca/files/100Mb.dat" });
            l.Add(new Server { Id = "ovh-fr", Provider = "OVHcloud", Where = "Roubaix (France)", Down = "https://proof.ovh.net/files/100Mb.dat" });
            return l;
        }
        public static string FilePath { get { return Path.Combine(Program.DataDir, "speedservers.txt"); } }
        static List<Server> all;
        // built-in + your own lines from speedservers.txt:  server|id|provider|where|download URL|upload URL (optional)
        public static List<Server> All()
        {
            var a = all; if (a != null) return a;
            var l = BuiltIn();
            try
            {
                if (File.Exists(FilePath))
                    foreach (var line in File.ReadLines(FilePath).Take(200))
                    {
                        var p = line.Split('|'); if (p.Length < 5 || p[0] != "server") continue;
                        var s = new Server { Id = Clean(p[1], 40).ToLowerInvariant(), Provider = Clean(p[2], 40), Where = Clean(p[3], 40), Down = p[4].Trim(), Up = p.Length > 5 ? p[5].Trim() : "" };
                        if (s.Id == "" || !SafeUrl(s.Down) || (s.Up != "" && !SafeUrl(s.Up))) continue;
                        if (l.Any(x => x.Id == s.Id)) continue;   // a file adds servers; it can't replace a built-in one (or change what "auto" means)
                        l.Add(s);
                    }
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("speed servers: " + e.Message); }
            all = l; return l;
        }
        public static void Reload() { all = null; }
        static string Clean(string s, int max) { s = new string((s ?? "").Where(ch => !char.IsControl(ch) && ch != '|').ToArray()).Trim(); return s.Length > max ? s.Substring(0, max) : s; }
        // http(s) only, a real host name or public address, no user name or password in it
        public static bool SafeUrl(string url)
        {
            Uri u; if (!Uri.TryCreate((url ?? "").Trim(), UriKind.Absolute, out u)) return false;
            if (u.Scheme != "https" && u.Scheme != "http") return false;
            if (u.UserInfo != "" || !Games.ValidHost(u.Host) || url.Length > 400) return false;
            // never this PC, the router, the LAN or a cloud metadata address: a name with a dot, or a public address
            // (Uri has already turned 0x7f000001 / 2130706433 / 0177.0.0.1 into 127.0.0.1)
            string h = u.DnsSafeHost; IPAddress ip;
            if (IPAddress.TryParse(h, out ip)) return Reachable(ip);
            if (h.IndexOf('.') < 0 || h.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || h.EndsWith(".lan", StringComparison.OrdinalIgnoreCase) || h.EndsWith(".home", StringComparison.OrdinalIgnoreCase) || h.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) || h.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
        // what the user picked (Settings / the home page chip / the Toolbox box): an id, words matching one ("vultr toronto"),
        // or a file URL; "" or "auto" = Cloudflare
        static readonly char[] WordSep = { ' ', ',', '-' };
        public static Server Pick(string what)
        {
            string w = (what ?? "").Trim(); var l = All();
            if (w == "" || w.Equals("auto", StringComparison.OrdinalIgnoreCase)) return l[0];
            var byId = l.FirstOrDefault(s => s.Id.Equals(w, StringComparison.OrdinalIgnoreCase)); if (byId != null) return byId;
            if (w.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || w.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return SafeUrl(w) ? new Server { Id = "url", Provider = "Your file", Where = new Uri(w).Host, Down = w } : null;
            var words = w.Split(WordSep, StringSplitOptions.RemoveEmptyEntries);
            return l.FirstOrDefault(s => words.All(x => (s.Label + " " + s.Id).IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        public static string Mbps(double v) { return double.IsNaN(v) ? "-" : v >= 100 ? Math.Round(v).ToString(CultureInfo.InvariantCulture) : v.ToString("0.0", CultureInfo.InvariantCulture); }
        public static string GradeOf(double idle, double loaded)
        {
            if (double.IsNaN(idle) || double.IsNaN(loaded)) return "";
            double worst = Math.Max(0, loaded - idle);
            return worst < 5 ? "A+" : worst < 30 ? "A" : worst < 60 ? "B" : worst < 200 ? "C" : worst < 400 ? "D" : "F";   // the same scale as Latency under load
        }

        // ---------- Toolbox: run one test, or "list" / "check" the servers
        public static List<string[]> Run(string what, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string w = (what ?? "").Trim();
            if (Policy.NoSpeed) { rows.Add(R(1, "Speed test", "turned off by your IT team (AdeeboMeter.policy)", "Nothing was sent.", "")); return rows; }
            if (w.Equals("list", StringComparison.OrdinalIgnoreCase) || w.Equals("check", StringComparison.OrdinalIgnoreCase)) return Servers(w.Equals("check", StringComparison.OrdinalIgnoreCase), run);
            var srv = Pick(w == "" ? Cfg.SpeedServer : w);
            if (srv == null) { rows.Add(R(2, "Server", "no server matches \"" + w + "\"", "Type auto, a city or provider (e.g. vultr toronto), a file URL - or list to see them all.", "")); return rows; }
            var res = Measure(run, srv);
            if (res.Error != "") { rows.Add(R(2, "Speed test", res.Error, (res.Error.StartsWith("stopped during the upload", StringComparison.Ordinal) ? "" : "Nothing else was sent. ") + "Try another server - type list to see them, check to see which answer from here. A company network may block it.", "m")); return rows; }
            rows.Add(R(3, "Server", res.Server + (res.Where != "" ? " · " + res.Where : ""), "", "m"));
            rows.Add(R(0, "Download", Mbps(res.Down) + " Mbps", "", "m"));
            rows.Add(R(res.NoUpload ? 3 : double.IsNaN(res.Up) ? 1 : 0, "Upload", res.NoUpload ? "this server only offers download - pick Cloudflare (or your own file server) for upload" : (double.IsNaN(res.Up) ? "didn't take the upload (" + (srv.Up.Length > 0 ? new Uri(srv.Up).Host : "") + ")" : Mbps(res.Up) + " Mbps"), "", "m"));
            rows.Add(R(3, "Ping when idle", double.IsNaN(res.Idle) ? "-" : Math.Round(res.Idle) + " ms", "", "m"));
            rows.Add(R(3, "Ping while downloading", double.IsNaN(res.LoadDown) ? "-" : Math.Round(res.LoadDown) + " ms", "", "m"));
            if (!res.NoUpload) rows.Add(R(3, "Ping while uploading", double.IsNaN(res.LoadUp) ? "-" : Math.Round(res.LoadUp) + " ms", "", "m"));
            if (res.Grade != "") rows.Add(R(res.Grade.StartsWith("A", StringComparison.Ordinal) ? 0 : res.Grade == "B" ? 1 : 2, "Stays quick when full", res.Grade, res.Grade.StartsWith("A", StringComparison.Ordinal) ? "Calls and games stay smooth even while someone downloads." : "The delay grows a lot when the line is busy - calls and games stutter while someone uploads or downloads. The fix is on the router: Smart Queue / SQM.", "m"));
            rows.Add(R(3, "Data used", Math.Round(res.MB) + " MB", "A far-away server shows less than your plan (distance slows a single download). Wi-Fi and other people on the network do too - for your plan's real speed, use a nearby server, on a cable, when nobody else is using it.", "m"));
            return rows;
        }
        // the list, and (check) whether each answers from here: the first 1 KB of its file, one at a time, 4 s each
        static List<string[]> Servers(bool check, Toolkit.Run run)
        {
            var rows = new List<string[]>(); var l = All();
            rows.Add(R(3, "How to choose", "type auto, an id or words (e.g. vultr toronto), or a file URL", "Your own servers: add lines to " + FilePath + "  as  server|id|provider|city|download URL|upload URL (optional)", ""));
            foreach (var g in l.GroupBy(s => s.Provider))
            {
                rows.Add(R(-1, g.Key.ToUpperInvariant(), g.First().Up != "" ? "download and upload" : "download only", "", "m"));
                foreach (var s in g)
                {
                    if (run.Stop) return rows;
                    string state = "";
                    if (check) { run.Status = "Checking " + s.Label + "..."; var sw = System.Diagnostics.Stopwatch.StartNew(); bool ok = Head(s.Down); state = ok ? "answers · " + sw.ElapsedMilliseconds + " ms" : "no answer"; }
                    rows.Add(new[] { check ? (state.StartsWith("answers", StringComparison.Ordinal) ? "0" : "2") : "3", s.Where, s.Id + (state != "" ? " · " + state : ""), "", "m", "copy:" + s.Id });
                }
            }
            return rows;
        }
        static bool Head(string url)
        {
            try
            {
                var req = Req(url); req.AddRange(0, 1023);
                using (var resp = (HttpWebResponse)req.GetResponse()) using (var s = resp.GetResponseStream())
                {
                    // a redirect or an empty answer isn't a test file
                    if (resp.StatusCode != HttpStatusCode.OK && resp.StatusCode != HttpStatusCode.PartialContent) return false;
                    var b = new byte[1024]; return s.Read(b, 0, b.Length) > 0;
                }
            }
            catch (WebException) { return false; } catch (IOException) { return false; }
        }
        static HttpWebRequest Req(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 6000; req.ReadWriteTimeout = 8000; req.UserAgent = "AdeeboMeter/" + Program.Version; req.Proxy = WebRequest.GetSystemWebProxy(); req.AllowAutoRedirect = false;
            return req;
        }

        public static Result Measure(Toolkit.Run run, Server srv)
        {
            var res = new Result { T = DateTime.Now, Server = srv.Label };
            if (Policy.NoSpeed) { res.Error = "turned off by your IT team (AdeeboMeter.policy)"; return res; }
            if (Busy) { res.Error = "a speed test is already running"; return res; }
            Busy = true;
            try
            {
                IPAddress ip;
IPAddress[] found;
                try { found = Dns.GetHostAddresses(srv.Host); } catch (SocketException) { found = Array.Empty<IPAddress>(); } catch (ArgumentException) { found = Array.Empty<IPAddress>(); }
                ip = found.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                if (ip == null && found.Length > 0) { res.Error = srv.Host + " has only IPv6 addresses - this speed test runs over IPv4; pick another server"; return res; }
                if (ip == null) { res.Error = "couldn't find " + srv.Host + " - the server's name doesn't resolve, or DNS / the internet is down"; return res; }
                // a server from speedservers.txt whose name points into this PC or the LAN (127.0.0.1.nip.io, localtest.me): never
                if (!Reachable(ip)) { res.Error = srv.Host + " points to a private address (" + ip + ") - a speed test only goes to a server on the internet"; return res; }
                if (run != null) run.Status = "Ping to " + srv.Label + "...";
                res.Idle = Ping(ip, 10, () => run != null && run.Stop);
                // some providers' test-file servers have no HTTPS: then the file comes over plain HTTP (it's only test data, and
                // nothing is sent) - the result says so
                string downUrl = srv.Down;
                if (downUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !Head(downUrl) && Head("http://" + downUrl.Substring(8))) { downUrl = "http://" + downUrl.Substring(8); res.Where = "over plain HTTP (this server has no HTTPS)"; }
                long down = 0, up = 0; var pd = new List<double>(); string colo = "";
                if (run != null) run.Status = "Downloading from " + srv.Label + " for " + Seconds + " s...";
                res.Down = Phase(downUrl, true, ip, run, pd, ref down, ref colo); res.LoadDown = Median(pd);
                if (srv.Provider == "Cloudflare" && colo != "") res.Where = "data centre " + colo + Colo(colo);
                if (run != null && run.Stop) { res.Error = "stopped"; return res; }
                if (srv.Up != "")
                {
                    if (run != null) run.Status = "Uploading to " + srv.Label + " for " + Seconds + " s...";
                    var pu = new List<double>(); string c2 = "";
                    res.Up = Phase(srv.Up, false, ip, run, pu, ref up, ref c2); res.LoadUp = Median(pu);
                    if (run != null && run.Stop) { res.Error = "stopped during the upload - nothing saved (about " + Math.Round(up / 1e6).ToString(CultureInfo.CurrentCulture) + " MB had been sent)"; return res; }
                }
                else res.NoUpload = true;
                res.MB = (down + up) / 1e6;
                if (double.IsNaN(res.Down)) { res.Error = srv.Label + " didn't send its test file (" + srv.Host + ")"; return res; }
                res.Grade = GradeOf(res.Idle, double.IsNaN(res.LoadDown) ? res.LoadUp : double.IsNaN(res.LoadUp) ? res.LoadDown : Math.Max(res.LoadDown, res.LoadUp));   // nothing measured: no grade
                Last = res;
                History.Event(0, "speed", "Speed test to " + srv.Label + ": " + Mbps(res.Down) + " down" + (res.NoUpload ? "" : " / " + Mbps(res.Up) + " up") + " Mbps · ping " + (double.IsNaN(res.Idle) ? "-" : Math.Round(res.Idle).ToString(CultureInfo.InvariantCulture)) + " ms" + (res.Grade != "" ? " · under load " + res.Grade : ""));
                Program.Log("speed test " + srv.Id + " " + Mbps(res.Down) + "/" + Mbps(res.Up) + " Mbps, " + Math.Round(res.MB) + " MB");
                return res;
            }
            finally { Busy = false; }
        }
        // a few Cloudflare data-centre codes people would recognise; others show the code alone
        static string Colo(string c)
        {
            switch (c)
            {
                case "YYZ": return " (Toronto)"; case "YUL": return " (Montreal)"; case "YVR": return " (Vancouver)"; case "YOW": return " (Ottawa)"; case "YYC": return " (Calgary)"; case "YWG": return " (Winnipeg)";
                case "EWR": return " (Newark)"; case "IAD": return " (Ashburn)"; case "ORD": return " (Chicago)"; case "DFW": return " (Dallas)"; case "LAX": return " (Los Angeles)"; case "SJC": return " (San Jose)"; case "SEA": return " (Seattle)"; case "ATL": return " (Atlanta)"; case "MIA": return " (Miami)"; case "BOS": return " (Boston)";
                case "LHR": return " (London)"; case "FRA": return " (Frankfurt)"; case "AMS": return " (Amsterdam)"; case "CDG": return " (Paris)"; case "DAC": return " (Dhaka)"; case "BOM": return " (Mumbai)"; case "SIN": return " (Singapore)"; case "NRT": return " (Tokyo)"; case "SYD": return " (Sydney)";
                default: return "";
            }
        }
        static double Median(List<double> l) { lock (l) { if (l.Count == 0) return double.NaN; var s = l.OrderBy(x => x).ToList(); return s[s.Count / 2]; } }
        // ping, or (servers that don't answer ping) a timed connection to port 443
        static double One(IPAddress ip) { var r = Icmp.Echo(ip, 32, 64, false, 1000, false); return r.Status == Icmp.Ok && r.Rtt >= 0 ? Math.Max(0.5, r.Rtt) : GamePing.TcpMs(ip, 443, 1000); }
        static double Ping(IPAddress ip, int n, Func<bool> stop)
        {
            var l = new List<double>();
            for (int i = 0; i < n && !stop(); i++) { double v = One(ip); if (!double.IsNaN(v)) l.Add(v); Thread.Sleep(150); }
            return l.Count == 0 ? double.NaN : l.OrderBy(x => x).Skip(l.Count > 2 ? 1 : 0).First();   // second best: one lucky reply doesn't count
        }
        // several connections at once for Seconds, each repeating requests; the first second is left out (TCP ramping up)
        static double Phase(string url, bool download, IPAddress ip, Toolkit.Run run, List<double> pings, ref long total, ref string colo)
        {
            long bytes = 0, counted = 0; var sw = System.Diagnostics.Stopwatch.StartNew(); long cap = (download ? MaxDownMB : MaxUpMB) * 1000000L;
            Func<bool> done = () => sw.Elapsed.TotalSeconds >= Seconds || Interlocked.Read(ref bytes) >= cap || (run != null && run.Stop);
            int streams = download ? 4 : 3; var th = new List<Thread>(); int fails = 0; string seenColo = "";
            // .NET allows 2 connections per server by default - the other streams would just queue
            try { var sp = ServicePointManager.FindServicePoint(new Uri(url)); if (sp.ConnectionLimit < streams + 1) sp.ConnectionLimit = streams + 1; } catch (UriFormatException) { }
            for (int i = 0; i < streams; i++)
            {
                var t = new Thread(() =>
                {
                    var buf = new byte[64 * 1024]; var payload = download ? null : new byte[4 * 1024 * 1024];
                    while (!done())
                    {
                        try
                        {
                            var req = Req(url);
                            if (!download)
                            {
                                req.Method = "POST"; req.ContentType = "application/octet-stream"; req.ContentLength = payload.Length; req.AllowWriteStreamBuffering = false;
                                using (var o = req.GetRequestStream()) for (int off = 0; off < payload.Length && !done(); off += buf.Length) { int n = Math.Min(buf.Length, payload.Length - off); o.Write(payload, off, n); Interlocked.Add(ref bytes, n); if (sw.Elapsed.TotalSeconds >= 1) Interlocked.Add(ref counted, n); }
                                using (var r2 = (HttpWebResponse)req.GetResponse()) if ((int)r2.StatusCode >= 300) throw new WebException("answered " + (int)r2.StatusCode);
                            }
                            else using (var resp = (HttpWebResponse)req.GetResponse()) using (var s = resp.GetResponseStream())
                            {
                                if (resp.StatusCode != HttpStatusCode.OK && resp.StatusCode != HttpStatusCode.PartialContent) throw new WebException("answered " + (int)resp.StatusCode + ", not the test file");   // a redirect's few hundred bytes are not a speed
                                string ray = resp.Headers["CF-Ray"] ?? ""; int dash = ray.LastIndexOf('-'); if (dash > 0 && ray.Length - dash == 4) seenColo = ray.Substring(dash + 1).ToUpperInvariant();
                                int n; while (!done() && (n = s.Read(buf, 0, buf.Length)) > 0) { Interlocked.Add(ref bytes, n); if (sw.Elapsed.TotalSeconds >= 1) Interlocked.Add(ref counted, n); }
                            }
                        }
                        catch (Exception e)   // a background thread: nothing may escape it (a bad URL, a protocol error...)
                        {
                            if (e is OutOfMemoryException || e is ThreadAbortException) throw;
                            if (Interlocked.Increment(ref fails) > 6) return; Thread.Sleep(200);
                        }
                    }
                }) { IsBackground = true };
                th.Add(t); t.Start();
            }
            // a ping lost under load counts as the full second it waited - a queue so bad that nothing comes back is the worst
            // case, not a missing one (the grade is blank anyway when the server never answered pings at idle)
            while (!done()) { double v = One(ip); if (sw.Elapsed.TotalSeconds >= 1) lock (pings) pings.Add(double.IsNaN(v) ? 1000 : v); Thread.Sleep(250); }
            double secs = Math.Max(0.5, sw.Elapsed.TotalSeconds - 1);   // the measured window ends here, not after waiting for the threads
            long c = Interlocked.Read(ref counted);
            foreach (var t in th) t.Join(3000); total = Interlocked.Read(ref bytes); if (seenColo != "") colo = seenColo;
            return c <= 0 ? double.NaN : c * 8 / 1e6 / secs;
        }
    }
}
