// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== Labs: the arithmetic behind each one
    // Pure functions - nothing sent, nothing read from the PC. The Learn page draws them; the self-test checks them.
    // TCP: Reno after RFC 5681, CUBIC after RFC 9438, both per round trip (one step = one RTT), in packets of 1,460 bytes.
    static class LabSim
    {
        public const int Mss = 1460;

        // ---------- TCP congestion control
        public sealed class Step { public int Round; public double Cwnd, Ssthresh, Queue, QueueMs, Util; public string Phase = "", Loss = ""; }
        public sealed class TcpIn
        {
            public string Algo = "cubic"; public double Mbps = 100, RttMs = 40, BufferBdp = 1, LossPct; public int Rounds = 120, Seed = 7;
            public readonly Dictionary<int, string> Events = new Dictionary<int, string>();   // round -> "soft" (3 duplicate ACKs) or "hard" (timeout)
        }
        public static double Bdp(double mbps, double rttMs) { return Math.Max(1, mbps * 1e6 * rttMs / 1000 / 8 / Mss); }
        public static List<Step> Tcp(TcpIn p)
        {
            var res = new List<Step>(); var rnd = new Random(p.Seed);
            double bdp = Bdp(p.Mbps, p.RttMs), cap = bdp * (1 + Math.Max(0, p.BufferBdp));
            double cwnd = 10, ss = double.PositiveInfinity, wmax = 0, k = 0; int sinceLoss = 0; bool cubic = p.Algo == "cubic", recovering = false;
            const double C = 0.4, Beta = 0.7;
            for (int r = 0; r < p.Rounds; r++)
            {
                var st = new Step { Round = r, Cwnd = cwnd, Ssthresh = ss };
                st.Queue = Math.Max(0, cwnd - bdp); st.QueueMs = st.Queue / bdp * p.RttMs; st.Util = Math.Min(1, cwnd / bdp);
                st.Phase = recovering ? "fast recovery" : cwnd < ss ? "slow start" : "congestion avoidance";
                // what goes wrong this round: your click, the buffer overflowing, or a random loss
                string loss; p.Events.TryGetValue(r, out loss); loss = loss ?? "";
                if (loss == "" && cwnd > Math.Max(cap, 2)) loss = "soft";   // the window never goes below 2: a link whose BDP is under 2 packets just queues them
                if (loss == "" && p.LossPct > 0 && rnd.NextDouble() < 1 - Math.Pow(1 - p.LossPct / 100, Math.Min(cwnd, 5000))) loss = "soft";
                st.Loss = loss; res.Add(st);
                recovering = false;
                if (loss != "")
                {
                    wmax = cwnd; ss = Math.Max(2, cwnd * (cubic ? Beta : 0.5)); sinceLoss = 0;
                    k = Math.Pow(wmax * (1 - Beta) / C, 1.0 / 3);
                    if (loss == "hard") cwnd = 1;                                   // a timeout: start again from one packet
                    else { cwnd = ss; recovering = true; }                          // three duplicate ACKs: halve (Reno) or x0.7 (CUBIC) and carry on
                    continue;
                }
                sinceLoss++;
                if (cwnd < ss) { double nx = Math.Min(cwnd * 2, double.IsInfinity(ss) ? cwnd * 2 : Math.Max(ss, cwnd + 1)); if (nx > cap && cwnd < cap) nx = Math.Floor(cap) + 1; cwnd = nx; }   // slow start: doubles every round trip - until the buffer overflows part-way through one
                else if (!cubic) cwnd += 1;                                         // Reno: one more packet per round trip
                else
                {
                    double t = sinceLoss * p.RttMs / 1000;                          // CUBIC grows with time since the loss, not with ACKs
                    double w = C * Math.Pow(t - k, 3) + wmax;
                    double est = wmax * Beta + 3 * (1 - Beta) / (1 + Beta) * sinceLoss;   // never slower than Reno would be (the TCP-friendly region)
                    cwnd = Math.Max(cwnd + 0.01, Math.Max(w, est));
                }
                cwnd = Math.Min(cwnd, 100000);
            }
            return res;
        }
        public static double Throughput(List<Step> s) { return s.Count == 0 ? 0 : s.Average(x => x.Util); }

        // ---------- loss -> the most a single TCP download can do (Mathis et al.): MSS / RTT * 1.22 / sqrt(loss)
        public static double MathisMbps(double rttMs, double lossPct)
        {
            if (lossPct <= 0 || rttMs <= 0) return double.PositiveInfinity;
            return Mss * 8 / (rttMs / 1000) * 1.22 / Math.Sqrt(lossPct / 100) / 1e6;
        }

        // ---------- bufferbloat: a link with a queue, a big download, and a ping trying to get through
        public sealed class BloatOut { public double PingMs, QueueMs, Mbps; }
        public static BloatOut Bloat(double linkMbps, double loadMbps, double bufferMs, double baseMs, bool smartQueue)
        {
            var o = new BloatOut(); double over = loadMbps / Math.Max(0.1, linkMbps);
            // a full queue holds bufferMs of data; a smart queue (fq_codel / CAKE) keeps its standing queue near 5 ms and
            // gives the small ping flow its own turn
            o.QueueMs = over < 0.95 ? bufferMs * Math.Max(0, over - 0.7) * 0.2 : smartQueue ? Math.Min(bufferMs, 5) : bufferMs;
            o.PingMs = baseMs + (smartQueue ? Math.Min(o.QueueMs, 2) : o.QueueMs);
            o.Mbps = Math.Min(linkMbps, loadMbps) * (smartQueue && over >= 0.95 ? 0.97 : 1);
            return o;
        }

        // ---------- the jitter buffer of a call: too small = gaps, too big = delay
        public sealed class JitterOut { public double LatePct, MouthToEarMs, Mos; }
        public static List<double> Arrivals(double jitterMs, int n, int seed)
        {
            // how much later than usual each packet arrives: a normal spread around the usual time (early ones wait in the buffer
            // time), scaled so the RFC 3550 jitter - the mean difference between neighbouring packets - is about jitterMs.
            // That's why a buffer of about twice the jitter is the usual advice.
            var rnd = new Random(seed); var l = new List<double>(); double sigma = jitterMs / 1.128;   /* not clamped: an early packet just waits in the buffer - and the RFC 3550 jitter of this spread is exactly jitterMs */
            for (int i = 0; i < n; i++)
            {
                double u1 = 1.0 - rnd.NextDouble(), u2 = rnd.NextDouble(), z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
                l.Add(sigma * z);   // negative = early (it waits in the buffer); positive = late
            }
            return l;
        }
        public static JitterOut Jitter(List<double> extraDelays, double baseMs, double bufferMs, double lossPct)
        {
            var o = new JitterOut(); if (extraDelays.Count == 0) return o;
            o.LatePct = 100.0 * extraDelays.Count(d => d > bufferMs) / extraDelays.Count;
            o.MouthToEarMs = baseMs + bufferMs + 20;   // + 20 ms for the codec on both ends
            // a late packet is a gap the codec papers over (packet loss concealment): it hurts about half as much as a lost one
            o.Mos = LiveCapture.Mos(baseMs + bufferMs, 0, lossPct + o.LatePct * 0.5);
            return o;
        }

        // ---------- Wi-Fi airtime: everyone takes turns on the air; a slow device's turn takes longest
        public sealed class AirDev { public string Name = ""; public double RateMbps; public double ShareAir, Mbps; }
        public static void Airtime(List<AirDev> devs, bool airtimeFair, int neighbours)
        {
            double airLeft = 1.0 / (1 + Math.Max(0, neighbours) * 0.35);   // busy neighbours on the channel take some of the air
            if (devs.Count == 0) return;
            if (airtimeFair) foreach (var d in devs) { d.ShareAir = airLeft / devs.Count; d.Mbps = d.RateMbps * 0.6 * d.ShareAir; }
            else
            {
                // equal turns (one frame each): time per frame is 1/rate, so the slow device holds the air longest
                double sumT = devs.Sum(d => 1 / Math.Max(0.5, d.RateMbps));
                foreach (var d in devs) { d.ShareAir = airLeft * (1 / Math.Max(0.5, d.RateMbps)) / sumT; d.Mbps = d.RateMbps * 0.6 * d.ShareAir; }
            }
        }

        // ---------- the game latency budget: what you feel is more than the ping
        public static double[] GameBudget(double pingMs, int tickRate, double interpTicks, int fps, double displayMs)
        {
            double tick = 1000.0 / Math.Max(1, tickRate), frame = 1000.0 / Math.Max(1, fps);
            return new[] { pingMs / 2, tick / 2, interpTicks * tick, frame * 1.5, displayMs, pingMs / 2 };   // up, server tick wait, interpolation, render, display, back
        }

        // ---------- QoS: one voice packet arriving behind a burst of big packets, three ways to queue
        public static double VoiceWaitMs(string mode, double linkMbps, int bulkPackets)
        {
            double perBig = 1500 * 8 / (linkMbps * 1e6) * 1000;   // ms to send one full-size packet
            switch (mode) { case "priority": return perBig; case "fair": return perBig * 1.0 + perBig * 0.5; default: return perBig * bulkPackets; }
        }

        // ---------- subnetting practice: a question, its answer and three wrong answers that look right
        public sealed class Quiz { public string Question = "", Answer = ""; public string[] Options; }
        public static Quiz Subnet(int seed)
        {
            var rnd = new Random(seed); int pre = 20 + rnd.Next(11); byte a = (byte)(new[] { 10, 172, 192 }[rnd.Next(3)]);
            var ip = new byte[] { a, (byte)(a == 172 ? 16 + rnd.Next(16) : a == 192 ? 168 : rnd.Next(256)), (byte)rnd.Next(256), (byte)(1 + rnd.Next(254)) };
            uint v = (uint)(ip[0] << 24 | ip[1] << 16 | ip[2] << 8 | ip[3]), m = 0xFFFFFFFFu << (32 - pre), net = v & m, bc = net | ~m;
            Func<uint, string> s = x => ((x >> 24) & 255) + "." + ((x >> 16) & 255) + "." + ((x >> 8) & 255) + "." + (x & 255);
            string addr = s(v) + "/" + pre; var q = new Quiz(); int kind = rnd.Next(3); var wrong = new List<string>();
            uint block = ~m + 1;   // the size of one subnet
            if (kind == 0) { q.Question = "What's the network address of " + addr + "?"; q.Answer = s(net); wrong.AddRange(new[] { s(net + 1), s(net >= block ? net - block : net + block), s(v) }); }
            else if (kind == 1) { q.Question = "What's the broadcast address of " + addr + "?"; q.Answer = s(bc); wrong.AddRange(new[] { s(bc - 1), s(bc + block), s(net + block / 2 - 1) }); }
            else { long hosts = pre >= 31 ? (pre == 31 ? 2 : 1) : (1L << (32 - pre)) - 2; q.Question = "How many usable hosts in a /" + pre + "?"; q.Answer = hosts.ToString("N0", CultureInfo.InvariantCulture); wrong.AddRange(new[] { (hosts + 2).ToString("N0", CultureInfo.InvariantCulture), (Math.Max(1, hosts / 2)).ToString("N0", CultureInfo.InvariantCulture), (hosts * 2 + 2).ToString("N0", CultureInfo.InvariantCulture) }); }
            var opts = wrong.Where(w => w != q.Answer).Distinct().Take(3).ToList(); int extra = 2; while (opts.Count < 3 && extra < 100) { string c = s(net + (uint)(extra++ * 4)); if (c != q.Answer && !opts.Contains(c)) opts.Add(c); }
            opts.Insert(rnd.Next(4), q.Answer); q.Options = opts.ToArray(); return q;
        }

        // ---------- real data: the window of a captured TCP download, estimated per round trip (data sent but not yet acknowledged)
        public static List<double> CwndFromCapture(List<Journey.Rec> recs, double rttS)
        {
            var res = new List<double>(); if (recs == null || recs.Count == 0 || rttS <= 0) return res;
            // one connection only: a capture can hold several (a browser opens parallel ones) - sequence numbers from two
            // connections can't be mixed, so take the busiest
            var tcpRecs = recs.Where(x => x.Proto == 6).ToList(); if (tcpRecs.Count == 0) return res;
            int port = tcpRecs.GroupBy(x => x.LPort).OrderByDescending(gr => gr.Sum(x => (long)x.PayLen)).First().Key;
            recs = tcpRecs.Where(x => x.LPort == port).ToList();
            long inB = recs.Where(r => !r.Out).Sum(r => (long)r.PayLen), outB = recs.Where(r => r.Out).Sum(r => (long)r.PayLen); bool down = inB >= outB;
            uint hiSeq = 0, hiAck = 0; bool seqSet = false, ackSet = false; double t0 = recs[0].T, binEnd = t0 + rttS, best = 0;
            foreach (var r in recs.Where(x => x.Proto == 6))
            {
                while (r.T > binEnd) { res.Add(best / Mss); best = 0; binEnd += rttS; if (res.Count > 2000) return res; }
                if (r.Out != down && r.PayLen > 0) { uint end = r.Seq + (uint)r.PayLen; if (!seqSet || (int)(end - hiSeq) > 0) { hiSeq = end; seqSet = true; } }
                if (r.Out == down && (r.Flags & 0x10) != 0) { if (!ackSet || (int)(r.Ack - hiAck) > 0) { hiAck = r.Ack; ackSet = true; } }
                if (seqSet && ackSet) { int fl = (int)(hiSeq - hiAck); if (fl > 0) best = Math.Max(best, fl); }
            }
            if (best > 0) res.Add(best / Mss);
            return res;
        }
        public static List<double> LastRealCwnd; public static double LastRealRttMs;   // the newest captured download, for the TCP lab
    }
}
