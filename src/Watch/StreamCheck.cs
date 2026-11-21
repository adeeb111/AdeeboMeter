// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Stream Check (1.1.6 Sports)
    // "Will my stream play, and how well?" - from the person's own stream link (HLS .m3u8 or DASH .mpd):
    //  1. find the server (DNS) and time its first answer, over a checked secure connection when the link is https
    //  2. read the stream's own quality ladder: what each quality needs comes from the stream itself, never a guess
    //  3. download the real video for up to 30 seconds (the top quality, so the test sees what the line can carry)
    //  4. say which qualities will play, a score, and tips - each tip from something measured on this PC right then
    // A web page (not a stream address) gets the reachability part only, and says how to find the stream address.
    // The link may carry a personal token: it is never logged (only its host), and saved links are encrypted for this
    // Windows user. Nothing else on the PC is read; nothing is sent anywhere but the stream's own server.
    static class StreamCheck
    {
        public sealed class Variant { public long Bw; public int W, H; public double Fps; public string Uri = ""; public string Name { get { return QualityName(H, Fps, Bw); } } }
        public sealed class Tip { public int Rank; public string Title = "", Body = "", Seen = ""; }
        public sealed class Result
        {
            public string Host = "", Kind = "", Error = ""; public bool Secure, CertOk, IsPage;
            public double DnsMs = double.NaN, ServerMs = double.NaN, Mbps = double.NaN, Low = double.NaN, TestSecs;
            public List<double> Samples = new List<double>(); public int DipAt = -1;
            public List<Variant> Ladder = new List<Variant>(); public Variant Best;   // Best: the top quality that plays smoothly
            public int Score = -1; public string Verdict = "", Line = "";
            public string[] Hops = new string[5], HopNote = new string[5]; public int[] HopLv = { 3, 3, 3, 3, 3 };
            public List<Tip> Tips = new List<Tip>(); public DateTime When = DateTime.Now;
        }
        public static readonly object k = new object();
        public static volatile Result Last; public static volatile bool Running; public static volatile string Step = ""; public static double Progress;
        static string HistFile { get { return Path.Combine(Program.DataDir, "streamchecks.txt"); } }
        static string LinksFile { get { return Path.Combine(Program.DataDir, "streamlinks.bin"); } }

        public static string QualityName(int h, double fps, long bw)
        {
            string q = h >= 2000 ? "4K (2160p)" : h >= 1400 ? "1440p" : h >= 1000 ? "1080p" : h >= 700 ? "720p" : h >= 500 ? "540p" : h >= 400 ? "480p" : h > 0 ? h.ToString(CultureInfo.InvariantCulture) + "p" : (bw / 1e6).ToString("0.0", CultureInfo.InvariantCulture) + " Mb/s stream";
            if (h > 0 && fps >= 49) q += ", " + Math.Round(fps).ToString(CultureInfo.InvariantCulture) + " fps";
            return q;
        }
        // short label for the strip and the overview: "1080p" / "4K"
        public static string Short(Variant v) { return v == null ? "" : v.H >= 2000 ? "4K" : v.H > 0 ? v.H.ToString(CultureInfo.InvariantCulture) + "p" : (v.Bw / 1e6).ToString("0.0", CultureInfo.InvariantCulture) + " Mb/s"; }

        // ---------- the ladder, from the stream's own words (pure, for the self-test)
        static readonly Regex attrRx = new Regex("([A-Z0-9-]+)=(\"[^\"]*\"|[^,]*)");
        public static List<Variant> ParseHls(string text)
        {
            var l = new List<Variant>(); var lines = (text ?? "").Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal)) continue;
                var v = new Variant();
                foreach (Match m in attrRx.Matches(lines[i].Substring(18)))
                {
                    string key = m.Groups[1].Value, val = m.Groups[2].Value.Trim('"');
                    long b; double f;
                    if (key == "BANDWIDTH" && long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out b) && b > 0 && b < 400000000) v.Bw = b;
                    else if (key == "RESOLUTION") { var p = val.Split('x'); int w, h; if (p.Length == 2 && int.TryParse(p[0], out w) && int.TryParse(p[1], out h) && w > 0 && w < 10000 && h > 0 && h < 10000) { v.W = w; v.H = h; } }
                    else if (key == "FRAME-RATE" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f) && f > 0 && f < 500) v.Fps = f;
                }
                for (int j = i + 1; j < lines.Length; j++) { var u = lines[j].Trim(); if (u == "" || u.StartsWith("#", StringComparison.Ordinal)) continue; v.Uri = u; break; }
                if (v.Bw > 0 && v.Uri != "" && !l.Any(x => x.Bw == v.Bw && x.H == v.H)) l.Add(v);
            }
            return l.OrderByDescending(v => v.Bw).ToList();
        }
        static readonly Regex repRx = new Regex("<Representation\\b([^>]*)>", RegexOptions.IgnoreCase);
        public static List<Variant> ParseDash(string text)
        {
            var l = new List<Variant>();
            foreach (Match m in repRx.Matches(text ?? ""))
            {
                string a = m.Groups[1].Value; Func<string, string> at = n => { var x = Regex.Match(a, "\\b" + n + "=\"([^\"]*)\""); return x.Success ? x.Groups[1].Value : ""; };
                if (at("mimeType").StartsWith("audio", StringComparison.OrdinalIgnoreCase)) continue;
                long b; int w, h; double f = 0;
                if (!long.TryParse(at("bandwidth"), NumberStyles.Integer, CultureInfo.InvariantCulture, out b) || b <= 0 || b > 400000000) continue;
                int.TryParse(at("width"), out w); int.TryParse(at("height"), out h);
                string fr = at("frameRate"); if (fr.Contains("/")) { var p = fr.Split('/'); double n0, d0; if (double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out n0) && double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out d0) && d0 > 0) f = n0 / d0; } else double.TryParse(fr, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
                if (h <= 0 && w <= 0 && at("mimeType") == "") continue;   // a representation that says nothing about video
                var v = new Variant { Bw = b, W = Math.Max(0, w), H = Math.Max(0, h), Fps = f > 0 && f < 500 ? f : 0, Uri = at("id") };
                if (!l.Any(x => x.Bw == v.Bw && x.H == v.H)) l.Add(v);
            }
            return l.OrderByDescending(v => v.Bw).ToList();
        }
        // smooth: the slow moments (the 20th percentile) still carry 1.2x what the quality needs; "may drop": the typical speed
        // carries it but the slow moments don't; otherwise it would buffer
        public static int Plays(Variant v, double typical, double low) { double need = v.Bw / 1e6; return low >= need * 1.2 ? 0 : typical >= need ? 1 : 2; }
        // the score follows the verdict: anything that plays smoothly is 60+, and the top quality playing is 100; nothing
        // playing scales 0-50 by how close the lowest quality came. Dips and a slow server take a little off.
        public static int Score(List<Variant> ladder, double typical, double low, double serverMs, bool dip)
        {
            if (double.IsNaN(typical)) return -1;
            double lo = double.IsNaN(low) ? typical : low, s;
            if (ladder.Count == 0) s = 100 * Math.Min(1, lo / 6.0);   // no ladder: 5 Mb/s HD (x1.2) as the yardstick
            else
            {
                int best = ladder.FindIndex(v => Plays(v, typical, low) == 0);
                s = best < 0 ? 50 * Math.Min(1, lo / (ladder[ladder.Count - 1].Bw / 1e6 * 1.2)) : 60 + 40.0 * (ladder.Count - 1 - best) / Math.Max(1, ladder.Count - 1);
            }
            if (dip) s -= 8; if (!double.IsNaN(serverMs) && serverMs > 400) s -= 6;
            return (int)Math.Max(0, Math.Min(100, Math.Round(s)));
        }
        static double Pctl(List<double> v, double p) { if (v.Count == 0) return double.NaN; var s = v.OrderBy(x => x).ToList(); return s[(int)Math.Min(s.Count - 1, Math.Floor(p * s.Count))]; }

        // ---------- the check itself (a background thread; the page polls Step/Progress and repaints when Last changes)
        public static bool Start(string url, Monitor m, Action done)
        {
            Uri u; if (Running || !Uri.TryCreate((url ?? "").Trim(), UriKind.Absolute, out u) || (u.Scheme != "http" && u.Scheme != "https") || url.Length > 2000) return false;
            if (Policy.NoOnline) return false;
            Running = true; Step = "Finding the server"; Progress = 0;
            new Thread(() =>
            {
                var r = new Result { Host = u.Host, Secure = u.Scheme == "https" };
                try { Run(u, r, m); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; r.Error = Friendly(e); Program.Log("stream check " + u.Host + ": " + e.GetType().Name); }
                finally
                {
                    Decide(r, m); Last = r; Running = false; Step = ""; Save(r);
                    MarketData.Done(done);
                }
            }) { IsBackground = true, Name = "streamcheck" }.Start();
            return true;
        }
        static string Friendly(Exception e)
        {
            var we = e as WebException;
            if (we != null && we.Status == WebExceptionStatus.TrustFailure) return "The secure connection failed: the server's certificate isn't valid.";
            if (we != null && we.Status == WebExceptionStatus.NameResolutionFailure) return "The server's name doesn't exist (DNS found nothing).";
            if (we != null && we.Status == WebExceptionStatus.Timeout) return "The server didn't answer within 10 seconds.";
            if (we != null && we.Response is HttpWebResponse) { int c = (int)((HttpWebResponse)we.Response).StatusCode; return c == 403 || c == 401 ? "The server refused (" + c + "): the link may have expired, or only works in its own app." : c == 404 ? "The server says the stream isn't there (404): the link may be old." : "The server answered with an error (" + c + ")."; }
            if (e is SocketException) return "The server couldn't be reached.";
            return "The check stopped: " + e.GetType().Name + ".";
        }
        static HttpWebRequest Req(Uri u)
        {
            var q = (HttpWebRequest)WebRequest.Create(u); q.Timeout = 10000; q.ReadWriteTimeout = 10000; q.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AdeeboMeter/" + Program.Version;
            q.AllowAutoRedirect = true; q.MaximumAutomaticRedirections = 5; q.Proxy = WebRequest.GetSystemWebProxy(); q.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return q;
        }
        static string Text(Uri u, Result r, bool timeIt)
        {
            var sw = Stopwatch.StartNew();
            using (var resp = (HttpWebResponse)Req(u).GetResponse())
            {
                if (timeIt) { r.ServerMs = sw.Elapsed.TotalMilliseconds; r.CertOk = r.Secure; }
                string ct = resp.ContentType ?? "";
                using (var s = resp.GetResponseStream())
                {
                    var buf = new byte[2 * 1024 * 1024]; int got = 0, n;
                    while (got < buf.Length && (n = s.Read(buf, got, buf.Length - got)) > 0) got += n;
                    var t = Encoding.UTF8.GetString(buf, 0, got);
                    if (timeIt) r.Kind = t.StartsWith("#EXTM3U", StringComparison.Ordinal) ? "HLS" : t.IndexOf("<MPD", StringComparison.OrdinalIgnoreCase) >= 0 ? "DASH" : ct.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0 ? "page" : "other";
                    return t;
                }
            }
        }
        static void Run(Uri u, Result r, Monitor m)
        {
            var sw = Stopwatch.StartNew();
            try { Dns.GetHostAddresses(u.DnsSafeHost); r.DnsMs = sw.Elapsed.TotalMilliseconds; }
            catch (SocketException) { r.Error = "The server's name doesn't exist (DNS found nothing)."; return; }
            Step = "Asking the server"; Progress = 0.05;
            string body = Text(u, r, true);
            if (r.Kind == "page" || r.Kind == "other") { r.IsPage = true; return; }
            Step = "Reading the qualities"; Progress = 0.1;
            Uri media = null; List<string> segs = null;
            if (r.Kind == "HLS")
            {
                r.Ladder = ParseHls(body);
                if (r.Ladder.Count > 0) { media = new Uri(u, r.Ladder[0].Uri); body = Text(media, r, false); } else media = u;
                segs = body.Replace("\r", "").Split('\n').Select(z => z.Trim()).Where(z => z != "" && !z.StartsWith("#", StringComparison.Ordinal)).ToList();
                if (segs.Count > 0 && segs[0].EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)) { media = new Uri(media, segs[0]); body = Text(media, r, false); segs = body.Replace("\r", "").Split('\n').Select(z => z.Trim()).Where(z => z != "" && !z.StartsWith("#", StringComparison.Ordinal)).ToList(); }
                if (r.Ladder.Count == 0) { var dur = Regex.Matches(body, "#EXTINF:([0-9.]+)").Cast<Match>().Select(x => { double d; return double.TryParse(x.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0; }).ToList(); if (dur.Count > 0) r.Line = "A single-quality stream: what it needs is measured from its own pieces."; }
                segs = segs.Skip(Math.Max(0, segs.Count - 8)).ToList();   // live: the newest pieces
            }
            else if (r.Kind == "DASH")
            {
                r.Ladder = ParseDash(body);
                segs = DashSegments(body, r.Ladder.Count > 0 ? r.Ladder[0] : null); media = u;
            }
            if (segs == null || segs.Count == 0) { r.Line = "The qualities were read, but its video pieces couldn't be found for a speed test."; return; }
            // the speed test: the real video, piece after piece, for up to 30 seconds or 150 MB
            Step = "Downloading the video (30 s)"; long total = 0; var t0 = Stopwatch.StartNew(); double lastMark = 0; long lastBytes = 0; long singleBytes = 0; double singleSecs = 0;
            var buf2 = new byte[64 * 1024];
            for (int pass = 0; pass < 20 && t0.Elapsed.TotalSeconds < 30 && total < 150L * 1024 * 1024; pass++)
                foreach (var sg in segs)
                {
                    if (t0.Elapsed.TotalSeconds >= 30 || total >= 150L * 1024 * 1024) break;
                    var su = new Uri(media, sg);
                    using (var resp = Req(su).GetResponse()) using (var s = resp.GetResponseStream())
                    {
                        int n; while ((n = s.Read(buf2, 0, buf2.Length)) > 0)
                        {
                            total += n; singleBytes += n;
                            double now = t0.Elapsed.TotalSeconds;
                            if (now - lastMark >= 1) { r.Samples.Add((total - lastBytes) * 8 / 1e6 / (now - lastMark)); lastMark = now; lastBytes = total; Progress = 0.1 + 0.9 * Math.Min(1, now / 30); }
                            if (now >= 30) break;
                        }
                    }
                }
            r.TestSecs = t0.Elapsed.TotalSeconds; singleSecs = r.TestSecs;
            var steady = r.Samples.Skip(2).ToList(); if (steady.Count == 0) steady = r.Samples.ToList();
            r.Mbps = Pctl(steady, 0.5); r.Low = Pctl(steady, 0.2);
            for (int i = 2; i < r.Samples.Count; i++) if (r.Samples[i] < r.Mbps * 0.5) { r.DipAt = i; break; }
        }
        // DASH SegmentTemplate with $Number$ (the common live case); $Time$ timelines are left unmeasured
        static List<string> DashSegments(string mpd, Variant v)
        {
            var t = Regex.Match(mpd, "<SegmentTemplate\\b([^>]*)>", RegexOptions.IgnoreCase); if (!t.Success || v == null) return null;
            string a = t.Groups[1].Value; Func<string, string> at = n => { var x = Regex.Match(a, "\\b" + n + "=\"([^\"]*)\""); return x.Success ? x.Groups[1].Value : ""; };
            string media = at("media"); if (media == "" || media.Contains("$Time$")) return null;
            int start; if (!int.TryParse(at("startNumber"), out start)) start = 1;
            return Enumerable.Range(start, 6).Select(n => media.Replace("$RepresentationID$", v.Uri).Replace("$Bandwidth$", v.Bw.ToString(CultureInfo.InvariantCulture)).Replace("$Number$", n.ToString(CultureInfo.InvariantCulture))).ToList();
        }

        // ---------- the verdict, the way along and the tips (each from a measurement taken now)
        static void Decide(Result r, Monitor m)
        {
            double cpu = double.NaN, gw = double.NaN, net = double.NaN, loss = double.NaN; bool wifi = false, vpn = false; int sig = -1; string ch = "";
            if (m != null) lock (m.L) { cpu = m.Cpu.Last; gw = m.GwPing.Avg(6); net = m.NetPing.Avg(6); loss = m.Loss.Avg(20) * 100; wifi = m.HasWifi; sig = m.WifiSignal; ch = m.WifiChannel ?? ""; vpn = m.VpnUp; }
            int chn; bool fiveG = int.TryParse(ch, out chn) && chn > 14;
            r.Hops[0] = "This PC"; r.HopLv[0] = double.IsNaN(cpu) ? 3 : cpu > 85 ? 1 : 0; r.HopNote[0] = double.IsNaN(cpu) ? "-" : "CPU " + cpu.ToString("0", CultureInfo.InvariantCulture) + "%";
            r.Hops[1] = wifi ? "Wi-Fi" : "Cable"; r.HopLv[1] = wifi ? (sig >= 0 && sig < 50 ? 2 : sig >= 0 && (sig < 70 || !fiveG) ? 1 : 0) : 0;
            r.HopNote[1] = wifi ? (sig >= 0 ? (sig >= 70 ? "signal good" : sig >= 50 ? "signal fair" : "signal weak") + (ch != "" ? (fiveG ? " · 5 GHz" : " · 2.4 GHz") : "") : "signal unknown") : "wired";
            r.Hops[2] = "Router"; r.HopLv[2] = double.IsNaN(gw) ? 3 : gw > 20 ? 1 : 0; r.HopNote[2] = double.IsNaN(gw) ? "-" : gw.ToString("0", CultureInfo.InvariantCulture) + " ms";
            r.Hops[3] = "Your provider"; r.HopLv[3] = double.IsNaN(net) ? 3 : (!double.IsNaN(loss) && loss >= 2) || net > 80 ? 1 : 0; r.HopNote[3] = double.IsNaN(net) ? "-" : net.ToString("0", CultureInfo.InvariantCulture) + " ms" + (!double.IsNaN(loss) && loss >= 1 ? " · " + loss.ToString("0", CultureInfo.InvariantCulture) + "% loss" : " · no loss");
            r.Hops[4] = "Stream server"; r.HopLv[4] = r.Error != "" ? 2 : double.IsNaN(r.ServerMs) ? 3 : r.ServerMs > 400 ? 1 : 0; r.HopNote[4] = r.Error != "" ? "not reached" : double.IsNaN(r.ServerMs) ? "-" : r.ServerMs.ToString("0", CultureInfo.InvariantCulture) + " ms" + (r.Secure ? " · secure" : " · not encrypted");
            if (r.Error != "") { r.Score = 0; r.Verdict = "Not ready"; r.Line = r.Error; }
            else if (r.IsPage) { r.Score = -1; r.Verdict = "Server reachable"; r.Line = "This is a web page, not the stream itself. The page answers in " + r.ServerMs.ToString("0", CultureInfo.InvariantCulture) + " ms. For the full check, paste the stream's own address (it ends in .m3u8 or .mpd)."; }
            else if (double.IsNaN(r.Mbps)) { r.Score = -1; r.Verdict = "Qualities read"; }
            else
            {
                r.Score = Score(r.Ladder, r.Mbps, r.Low, r.ServerMs, r.DipAt >= 0);
                r.Best = r.Ladder.FirstOrDefault(v => Plays(v, r.Mbps, r.Low) == 0);
                if (r.Ladder.Count == 0) { r.Verdict = r.Mbps >= 6 ? "Ready" : r.Mbps >= 3 ? "Ready with care" : "Not ready"; r.Line = "Video came in at " + r.Mbps.ToString("0.0", CultureInfo.InvariantCulture) + " Mb/s."; }
                else if (r.Best == null) { r.Verdict = "Not ready"; r.Line = "Even the lowest quality (" + r.Ladder.Last().Name + ") would buffer at " + r.Mbps.ToString("0.0", CultureInfo.InvariantCulture) + " Mb/s."; }
                else
                {
                    r.Verdict = r.Best == r.Ladder[0] ? "Ready for " + Short(r.Best) : "Ready for " + Short(r.Best);
                    r.Line = r.Best == r.Ladder[0] ? "Every quality this stream offers should play smoothly." : Short(r.Ladder[0]) + " would stutter right now. " + Short(r.Best) + " plays smoothly.";
                }
            }
            // tips, most helpful first; nothing is said that wasn't measured
            var tips = new List<Tip>();
            if (r.Error != "") tips.Add(new Tip { Title = "Check the link itself.", Body = r.Error, Seen = "the server's own answer" });
            bool short_ = r.Ladder.Count > 0 && r.Best != r.Ladder[0];
            if (vpn && short_) tips.Add(new Tip { Title = "Try without the VPN.", Body = "A VPN sends the video the long way round, and some streams pick a far-away server because of it.", Seen = "a VPN is connected" });
            if (wifi && sig >= 0 && (sig < 70 || !fiveG) && (short_ || r.DipAt >= 0)) tips.Add(new Tip { Title = fiveG ? "Move closer to the router, or plug in a cable." : "Join your router's 5 GHz Wi-Fi, or plug in a cable.", Body = "Your Wi-Fi is the weak link" + (r.DipAt >= 0 ? ": the dip in the test was there." : "."), Seen = "signal " + sig + "%" + (ch != "" ? (fiveG ? ", 5 GHz" : ", 2.4 GHz") : "") + (r.DipAt >= 0 ? ", a dip at " + r.DipAt + " s" : "") });
            if (short_ && r.Best != null) tips.Add(new Tip { Title = "Pick " + Short(r.Best) + " in the player, not Auto.", Body = "Auto keeps reaching for " + Short(r.Ladder[0]) + " and falling back, which looks like stutter.", Seen = "from the quality table" });
            if (!double.IsNaN(r.ServerMs) && r.ServerMs > 400) tips.Add(new Tip { Title = "The stream's server is slow to answer.", Body = "That part is on their side, not your line. Starting the stream a few minutes early lets the player fill its buffer.", Seen = "first answer " + r.ServerMs.ToString("0", CultureInfo.InvariantCulture) + " ms" });
            if (!double.IsNaN(r.DnsMs) && r.DnsMs > 300) tips.Add(new Tip { Title = "Finding the server took long.", Body = "Your DNS answered slowly. It only matters when the stream starts.", Seen = "DNS " + r.DnsMs.ToString("0", CultureInfo.InvariantCulture) + " ms" });
            if (!double.IsNaN(cpu) && cpu > 85) tips.Add(new Tip { Title = "This PC is very busy.", Body = "Close what's using the processor: video needs it to decode smoothly.", Seen = "CPU " + cpu.ToString("0", CultureInfo.InvariantCulture) + "%" });
            for (int i = 0; i < tips.Count; i++) tips[i].Rank = i + 1;
            r.Tips = tips;
        }

        // ---------- earlier checks (host only - never the link) and saved links (encrypted for this Windows user)
        static void Save(Result r)
        {
            try
            {
                var line = string.Join("\t", r.When.ToString("o", CultureInfo.InvariantCulture), r.Host.Replace("\t", " "), double.IsNaN(r.Mbps) ? "" : r.Mbps.ToString("0.0", CultureInfo.InvariantCulture), r.Verdict.Replace("\t", " "), r.Score.ToString(CultureInfo.InvariantCulture));
                var keep = File.Exists(HistFile) ? File.ReadLines(HistFile).Take(2000).ToList() : new List<string>();
                keep.Insert(0, line); File.WriteAllLines(HistFile, keep.Take(50));
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
        }
        public static List<string[]> Earlier()
        {
            try { return File.Exists(HistFile) ? File.ReadLines(HistFile).Take(50).Select(l => l.Split('\t')).Where(p => p.Length == 5).ToList() : new List<string[]>(); }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; return new List<string[]>(); }
        }
        // saved links: label TAB url, one per line, the whole file DPAPI-protected (a link can hold a personal token)
        public static List<string[]> Links()
        {
            try
            {
                if (!File.Exists(LinksFile) || new FileInfo(LinksFile).Length > 256 * 1024) return new List<string[]>();
                var t = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(LinksFile), null, DataProtectionScope.CurrentUser));
                return t.Split('\n').Select(l => l.Split('\t')).Where(p => p.Length == 2 && p[1] != "").Take(12).ToList();
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; return new List<string[]>(); }
        }
        public static void SaveLink(string label, string url)
        {
            var l = Links().Where(p => p[1] != url).ToList(); l.Insert(0, new[] { (label ?? "").Replace("\t", " ").Replace("\n", " "), url.Replace("\t", "").Replace("\n", "") });
            try { File.WriteAllBytes(LinksFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(string.Join("\n", l.Take(12).Select(p => p[0] + "\t" + p[1]))), null, DataProtectionScope.CurrentUser)); }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is CryptographicException)) throw; }
        }
        public static void ForgetLink(string url)
        {
            var l = Links().Where(p => p[1] != url).ToList();
            try { if (l.Count == 0) File.Delete(LinksFile); else File.WriteAllBytes(LinksFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(string.Join("\n", l.Select(p => p[0] + "\t" + p[1]))), null, DataProtectionScope.CurrentUser)); }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is CryptographicException)) throw; }
        }
        // a label for a link: the file name or the host, never the query (which can hold a token)
        public static string LabelFor(string url)
        {
            Uri u; if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return "stream";
            string kind = u.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ? "m3u8" : u.AbsolutePath.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase) ? "DASH" : "web page";
            return u.Host + " · " + kind;
        }
    }
}
