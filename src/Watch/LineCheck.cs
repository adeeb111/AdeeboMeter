// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== Line Check (1.1.6 Markets): "Ready to trade?"
    // Built from what the broker watch already measures (Trading.Samples, the last 10 minutes) and the market days it kept
    // (WatchKit sessions), plus this PC's state right now. A score, one sentence, and tips - each from a measurement.
    // It says how the connection is, never whether orders go through (AM-CG-105).
    static class LineCheck
    {
        public sealed class Result { public int Score = -1; public string Verdict = "", Line = "", Detail = ""; public double FarMs = double.NaN, Usual = double.NaN; public int DaysClean, Days; public List<StreamCheck.Tip> Tips = new List<StreamCheck.Tip>(); }

        // "Check my line": 20 knocks, one a second, on the broker's server (TCP only: connect, then close - nothing is sent)
        public static volatile bool Checking; public static List<WatchKit.Sample> Manual = new List<WatchKit.Sample>(); public static DateTime ManualAt;
        public static bool Check(Action done)
        {
            if (Checking || Policy.NoOnline || Cfg.TradeHost == "") return false;
            Checking = true; string host = Cfg.TradeHost;
            new System.Threading.Thread(() =>
            {
                var l = new List<WatchKit.Sample>();
                try
                {
                    string hn; int port; if (!Lab.Split(host, 443, out hn, out port)) { hn = host; port = 443; }
                    System.Net.IPAddress ip;
                    if (!System.Net.IPAddress.TryParse(hn, out ip))
                        try { var a = System.Net.Dns.GetHostAddresses(hn); ip = a.FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) ?? a.FirstOrDefault(); }
                        catch (Exception e) { if (!(e is System.Net.Sockets.SocketException || e is ArgumentException)) throw; ip = null; }
                    var end = DateTime.UtcNow.AddSeconds(20);   // R2-147: a 20-second look, even against a host that drops knocks slowly
                    for (int i = 0; i < 20 && ip != null && DateTime.UtcNow < end; i++)
                    {
                        var t0 = DateTime.UtcNow; var s = new WatchKit.Sample { T = DateTime.Now, Far = GamePing.TcpMs(ip, port, (int)Math.Max(300, Math.Min(1500, (end - DateTime.UtcNow).TotalMilliseconds))) }; s.FarLost = double.IsNaN(s.Far); l.Add(s);
                        int wait = 1000 - (int)(DateTime.UtcNow - t0).TotalMilliseconds; if (wait > 0) System.Threading.Thread.Sleep(wait);
                    }
                    if (ip == null) l.Add(new WatchKit.Sample { T = DateTime.Now, NoProbe = true, FarLost = true });
                }
                finally { lock (Trading.k) { Manual = l; ManualAt = DateTime.Now; Trading.Ver++; } Checking = false; MarketData.Done(done); }
            }) { IsBackground = true, Name = "linecheck" }.Start();
            return true;
        }
        public static Result Now(Monitor m)
        {
            var r = new Result();
            // R2-070: the newest readings win - a check you just pressed over an older watch window; nothing older than 30 minutes
            List<WatchKit.Sample> recent; lock (Trading.k)
            {
                var watch = Trading.Samples.Where(z => (DateTime.Now - z.T).TotalMinutes < 30).ToList();
                bool manualFresh = (DateTime.Now - ManualAt).TotalMinutes < 30 && Manual.Count > 0;
                recent = manualFresh && (watch.Count == 0 || ManualAt >= watch.Max(z => z.T)) ? Manual.ToList() : watch;
            }
            var days = WatchKit.Recent("trade").Where(s => s.Start >= DateTime.Now.AddDays(-8)).ToList();
            double cpu = double.NaN, net = double.NaN; bool wifi = false, vpn = false; int sig = -1;
            if (m != null) lock (m.L) { cpu = m.Cpu.Last; net = m.NetPing.Avg(6); wifi = m.HasWifi; sig = m.WifiSignal; vpn = m.VpnUp; }
            return Decide(r, recent, days, cpu, net, wifi, sig, vpn, Cfg.TradeHost != "");
        }
        // pure, for the self-test
        public static Result Decide(Result r, List<WatchKit.Sample> recent, List<WatchKit.Session> days, double cpu, double net, bool wifi, int sig, bool vpn, bool hasBroker)
        {
            var far = recent.Where(s => !double.IsNaN(s.Far)).Select(s => s.Far).ToList();
            int lost = recent.Count(s => s.FarLost && !s.NoProbe);   // R2-012: "couldn't look it up" is not a lost knock
            if (recent.Count > 0 && recent.All(s => s.NoProbe) && hasBroker) { r.Verdict = "Couldn't find your broker"; r.Line = "The broker's server name didn't resolve - check the address, or whether names work (DNS)."; return r; }
            r.FarMs = far.Count == 0 ? double.NaN : far.OrderBy(x => x).ElementAt(far.Count / 2);
            var normals = days.Where(d => !double.IsNaN(d.FarNormal)).Select(d => d.FarNormal).ToList();
            r.Usual = normals.Count == 0 ? double.NaN : normals.OrderBy(x => x).ElementAt(normals.Count / 2);
            r.Days = days.Select(d => d.Start.Date).Distinct().Count();
            r.DaysClean = days.GroupBy(d => d.Start.Date).Count(g => g.All(s => s.Dips.Count == 0));
            if (!hasBroker) { r.Verdict = "Pick your broker"; r.Line = "Choose your broker below and AdeeboMeter checks the line to it."; return r; }
            if (far.Count == 0 && lost == 0)
            {
                r.Verdict = double.IsNaN(net) ? "Not checked yet" : "Not watching right now";
                r.Line = double.IsNaN(net) ? "Press Check my line for a 20-second look." : "The internet answers in " + net.ToString("0", CultureInfo.InvariantCulture) + " ms. Press Check my line to time your broker itself.";
            }
            else
            {
                int knocked = recent.Count(z => !z.NoProbe);
                double ms = double.IsNaN(r.FarMs) ? 999 : r.FarMs, lossPct = knocked == 0 ? 0 : 100.0 * lost / knocked;
                double s = 100;
                if (ms > 40) s -= Math.Min(40, (ms - 40) / 4);                 // every 4 ms over 40 costs a point, at most 40
                if (!double.IsNaN(r.Usual) && ms > r.Usual * 1.5) s -= 10;     // slower than this line usually is
                s -= Math.Min(40, lossPct * 8);                                // each 1% lost costs 8
                s -= Math.Min(15, (r.Days - r.DaysClean) * 4);                 // dips on recent market days
                if (!double.IsNaN(cpu) && cpu > 85) s -= 8;
                r.Score = (int)Math.Max(0, Math.Min(100, Math.Round(s)));
                r.Verdict = r.Score >= 80 ? "Ready to trade" : r.Score >= 55 ? "Ready, with care" : "Not ready";
                r.Line = "Your broker answers in " + (double.IsNaN(r.FarMs) ? "-" : r.FarMs.ToString("0", CultureInfo.InvariantCulture)) + " ms" + (lost > 0 ? ", " + lost + " of " + knocked + " knocks lost" : "") + (r.Days > 0 ? " · " + (r.DaysClean == r.Days ? "no dips in " + r.Days + " market days" : (r.Days - r.DaysClean) + " of " + r.Days + " market days had a dip") : "") + ".";
            }
            var dips = days.SelectMany(d => d.Dips).ToList();
            var tips = new List<StreamCheck.Tip>();
            if (vpn) tips.Add(new StreamCheck.Tip { Title = "Switch the VPN off while you trade.", Body = "Every order takes the VPN's route first. Compare: check your line with it on, then off.", Seen = "a VPN is connected" });
            int wifiDips = dips.Count(d => d.Blame == "wifi");
            if (wifi && (wifiDips > 0 || (sig >= 0 && sig < 60))) tips.Add(new StreamCheck.Tip { Title = "Use a cable while the market is open.", Body = wifiDips > 0 ? wifiDips + " of this week's dips were on your Wi-Fi." : "Your Wi-Fi signal is weak: dips start there.", Seen = wifiDips > 0 ? "this week's sessions" : "signal " + sig + "%" });
            int provDips = dips.Count(d => d.Blame == "provider");
            if (provDips > 0) { var last = dips.Where(d => d.Blame == "provider").OrderByDescending(d => d.T).First(); tips.Add(new StreamCheck.Tip { Title = "Your provider had " + (provDips == 1 ? "a dip" : provDips + " dips") + " this week.", Body = "If it happens again, make a report: it shows where the loss started.", Seen = last.T.ToString("ddd d, h:mm tt", CultureInfo.CurrentCulture) }); }
            int farDips = dips.Count(d => d.Blame == "far");
            if (farDips > 0) tips.Add(new StreamCheck.Tip { Title = "Your broker's side was slow " + (farDips == 1 ? "once" : farDips + " times") + " this week.", Body = "That part isn't your line. Their status page may say why.", Seen = "this week's sessions" });
            if (!double.IsNaN(cpu) && cpu > 85) tips.Add(new StreamCheck.Tip { Title = "This PC is very busy.", Body = "Close what's using the processor before the open: a busy PC delays everything you click.", Seen = "CPU " + cpu.ToString("0", CultureInfo.InvariantCulture) + "%" });
            else if (!double.IsNaN(cpu)) tips.Add(new StreamCheck.Tip { Title = "This PC is ready.", Body = "Nothing heavy is running.", Seen = "CPU " + cpu.ToString("0", CultureInfo.InvariantCulture) + "%" });
            for (int i = 0; i < tips.Count; i++) tips[i].Rank = i + 1;
            r.Tips = tips;
            return r;
        }
    }
}
