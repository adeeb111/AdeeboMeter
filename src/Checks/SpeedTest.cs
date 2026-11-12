// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Speed test - only when you press it
    // Downloads from and uploads to Cloudflare's public speed-test server (speed.cloudflare.com, the one their own web page
    // uses) for about 8 seconds each, with pings while the line is full (latency under load). Nothing runs in the background,
    // nothing about you is sent, and it is capped: 400 MB down and 100 MB up at most - say so before it starts.
    static class SpeedTest
    {
        public const string Host = "speed.cloudflare.com";
        public const int MaxDownMB = 400, MaxUpMB = 100, Seconds = 8;
        public sealed class Result { public DateTime T; public double Down = double.NaN, Up = double.NaN, Idle = double.NaN, LoadDown = double.NaN, LoadUp = double.NaN, MB; public string Grade = "", Error = ""; }
        public static Result Last;   // shown on the home page until the app closes (and kept in the day's events)
        public static volatile bool Busy;

        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        public static string Mbps(double v) { return double.IsNaN(v) ? "-" : v >= 100 ? Math.Round(v).ToString(CultureInfo.InvariantCulture) : v.ToString("0.0", CultureInfo.InvariantCulture); }
        public static string GradeOf(double idle, double loaded)
        {
            if (double.IsNaN(idle) || double.IsNaN(loaded)) return "";
            double worst = Math.Max(0, loaded - idle);
            return worst < 5 ? "A+" : worst < 30 ? "A" : worst < 60 ? "B" : worst < 200 ? "C" : worst < 400 ? "D" : "F";   // the same scale as Latency under load
        }

        public static List<string[]> Run(Toolkit.Run run)
        {
            var rows = new List<string[]>(); var res = Measure(run);
            if (res.Error != "") { rows.Add(R(2, "Speed test", res.Error, "Nothing else was sent. A company network may block it - then ask IT, or use Latency under load with your own iperf3 server.", "m")); return rows; }
            rows.Add(R(0, "Download", Mbps(res.Down) + " Mbps", "", "m"));
            rows.Add(R(0, "Upload", Mbps(res.Up) + " Mbps", "", "m"));
            rows.Add(R(3, "Ping when idle", double.IsNaN(res.Idle) ? "-" : Math.Round(res.Idle) + " ms", "", "m"));
            rows.Add(R(3, "Ping while downloading", double.IsNaN(res.LoadDown) ? "-" : Math.Round(res.LoadDown) + " ms", "", "m"));
            rows.Add(R(3, "Ping while uploading", double.IsNaN(res.LoadUp) ? "-" : Math.Round(res.LoadUp) + " ms", "", "m"));
            if (res.Grade != "") rows.Add(R(res.Grade.StartsWith("A", StringComparison.Ordinal) ? 0 : res.Grade == "B" ? 1 : 2, "Stays quick when full", res.Grade, res.Grade.StartsWith("A", StringComparison.Ordinal) ? "Calls and games stay smooth even while someone downloads." : "The delay grows a lot when the line is busy - calls and games stutter while someone uploads or downloads. The fix is on the router: Smart Queue / SQM.", "m"));
            rows.Add(R(3, "Data used", Math.Round(res.MB) + " MB · server " + Host, "Wi-Fi and other people on the network make the result lower than your plan - for your plan's real speed, test on a cable when nobody else is using it.", "m"));
            return rows;
        }

        public static Result Measure(Toolkit.Run run)
        {
            var res = new Result { T = DateTime.Now };
            if (Busy) { res.Error = "a speed test is already running"; return res; }
            Busy = true;
            try
            {
                IPAddress ip;
                try { ip = Dns.GetHostAddresses(Host).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork); }
                catch (SocketException) { ip = null; }
                if (ip == null) { res.Error = "couldn't find " + Host + " - DNS or the internet is down"; return res; }
                if (run != null) run.Status = "Ping while idle...";
                res.Idle = Pings(ip, 10, 200, () => run != null && run.Stop).DefaultIfEmpty(double.NaN).OrderBy(x => x).Skip(1).FirstOrDefault();   // second best of 10
                if (double.IsNaN(res.Idle)) res.Idle = Pings(IPAddress.Parse("1.1.1.1"), 5, 200, () => false).DefaultIfEmpty(double.NaN).Min();
                long down = 0, up = 0;
                if (run != null) run.Status = "Downloading for " + Seconds + " s...";
                double secD; var pd = new List<double>();
                res.Down = Phase(true, ip, run, pd, ref down, out secD); res.LoadDown = Median(pd);
                if (run != null && run.Stop) { res.Error = "stopped"; return res; }
                if (run != null) run.Status = "Uploading for " + Seconds + " s...";
                double secU; var pu = new List<double>();
                res.Up = Phase(false, ip, run, pu, ref up, out secU); res.LoadUp = Median(pu);
                res.MB = (down + up) / 1e6;
                if (double.IsNaN(res.Down) && double.IsNaN(res.Up)) { res.Error = "the server didn't answer (" + Host + ")"; return res; }
                res.Grade = GradeOf(res.Idle, Math.Max(double.IsNaN(res.LoadDown) ? 0 : res.LoadDown, double.IsNaN(res.LoadUp) ? 0 : res.LoadUp));
                Last = res;
                History.Event(0, "speed", "Speed test: " + Mbps(res.Down) + " down / " + Mbps(res.Up) + " up Mbps · ping " + (double.IsNaN(res.Idle) ? "-" : Math.Round(res.Idle).ToString(CultureInfo.InvariantCulture)) + " ms" + (res.Grade != "" ? " · under load " + res.Grade : ""));
                Program.Log("speed test " + Mbps(res.Down) + "/" + Mbps(res.Up) + " Mbps, " + Math.Round(res.MB) + " MB");
                return res;
            }
            finally { Busy = false; }
        }
        static double Median(List<double> l) { lock (l) { if (l.Count == 0) return double.NaN; var s = l.OrderBy(x => x).ToList(); return s[s.Count / 2]; } }
        static List<double> Pings(IPAddress ip, int n, int gapMs, Func<bool> stop)
        {
            var l = new List<double>();
            for (int i = 0; i < n && !stop(); i++) { var r = Icmp.Echo(ip, 32, 64, false, 1000, false); if (r.Status == Icmp.Ok && r.Rtt >= 0) l.Add(r.Rtt); Thread.Sleep(gapMs); }
            return l;
        }
        // several connections at once for Seconds, each repeating requests; the first second is left out (TCP ramping up)
        static double Phase(bool download, IPAddress ip, Toolkit.Run run, List<double> pings, ref long total, out double secs)
        {
            long bytes = 0, counted = 0; var sw = System.Diagnostics.Stopwatch.StartNew(); long cap = (download ? MaxDownMB : MaxUpMB) * 1000000L;
            Func<bool> done = () => sw.Elapsed.TotalSeconds >= Seconds || Interlocked.Read(ref bytes) >= cap || (run != null && run.Stop);
            int streams = download ? 4 : 3; var th = new List<Thread>(); int fails = 0;
            for (int i = 0; i < streams; i++)
            {
                var t = new Thread(() =>
                {
                    var buf = new byte[64 * 1024]; var payload = download ? null : new byte[4 * 1024 * 1024];
                    while (!done())
                    {
                        try
                        {
                            var req = (HttpWebRequest)WebRequest.Create("https://" + Host + (download ? "/__down?bytes=25000000" : "/__up"));
                            req.Timeout = 10000; req.ReadWriteTimeout = 10000; req.UserAgent = "AdeeboMeter/" + Program.Version; req.Proxy = WebRequest.GetSystemWebProxy(); req.AllowAutoRedirect = false;
                            if (!download)
                            {
                                req.Method = "POST"; req.ContentType = "application/octet-stream"; req.ContentLength = payload.Length; req.AllowWriteStreamBuffering = false;
                                using (var o = req.GetRequestStream()) for (int off = 0; off < payload.Length && !done(); off += buf.Length) { int n = Math.Min(buf.Length, payload.Length - off); o.Write(payload, off, n); Interlocked.Add(ref bytes, n); if (sw.Elapsed.TotalSeconds >= 1) Interlocked.Add(ref counted, n); }
                                using (req.GetResponse()) { }
                            }
                            else using (var resp = req.GetResponse()) using (var s = resp.GetResponseStream())
                            {
                                int n; while (!done() && (n = s.Read(buf, 0, buf.Length)) > 0) { Interlocked.Add(ref bytes, n); if (sw.Elapsed.TotalSeconds >= 1) Interlocked.Add(ref counted, n); }
                            }
                        }
                        catch (WebException) { if (Interlocked.Increment(ref fails) > 6) return; Thread.Sleep(200); }
                        catch (IOException) { if (Interlocked.Increment(ref fails) > 6) return; Thread.Sleep(200); }
                    }
                }) { IsBackground = true };
                th.Add(t); t.Start();
            }
            while (!done()) { var r = Icmp.Echo(ip, 32, 64, false, 1000, false); if (r.Status == Icmp.Ok && r.Rtt >= 0 && sw.Elapsed.TotalSeconds >= 1) lock (pings) pings.Add(r.Rtt); Thread.Sleep(250); }
            foreach (var t in th) t.Join(3000);
            secs = Math.Max(0.5, sw.Elapsed.TotalSeconds - 1); total = Interlocked.Read(ref bytes);
            long c = Interlocked.Read(ref counted);
            return c <= 0 ? double.NaN : c * 8 / 1e6 / secs;
        }
    }
}
