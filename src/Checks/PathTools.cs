// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace AdeeboMeter
{
    // Path checks you press: a trace that also reads the reply TTL (the way back), the MTU at each hop, MTR over minutes,
    // Record Route, overlapping networks, IPv6 vs IPv4. Everything outbound, nothing listens.
    static class PathTools
    {
        static string[] Row(params string[] a) { return a; }
        public sealed class Hop { public int N; public string Ip = ""; public long Rtt = -1; public int ReplyTtl = -1, Mtu = -1; public string MtuNote = "", Owner = ""; public bool Reached, BlackHole, Said; public int Sent, Lost, Switches; public string Cur = ""; public DateTime SwitchedAt; public readonly List<string> Seen = new List<string>(); public double Sum, Best = double.NaN, Worst = double.NaN, Last = double.NaN, Jit; public readonly List<double> Hist = new List<double>(); }   // Hist: one value a round (NaN = lost), for the graph and the recording
        public static string F(double v) { return double.IsNaN(v) || v < 0 ? "-" : v.ToString("0", CultureInfo.CurrentCulture); }

        // ---------- trace (ICMP, 2 tries per hop)
        public static List<Hop> Trace(IPAddress dst, int max, Func<bool> stop)
        {
            var hops = new List<Hop>();
            for (int ttl = 1; ttl <= max && !stop(); ttl++)
            {
                var h = new Hop { N = ttl };
                for (int t = 0; t < 2 && h.Ip == ""; t++)
                {
                    var r = Icmp.Echo(dst, 32, ttl, false, 1200, false);
                    if (r.Status == Icmp.TtlExpired || r.Status == Icmp.Ok) { h.Ip = r.From; h.Rtt = r.Rtt; h.ReplyTtl = r.ReplyTtl; h.Reached = r.Status == Icmp.Ok; }
                }
                hops.Add(h);
                if (h.Reached) break;
                if (hops.Count >= 6 && hops.Skip(hops.Count - 5).All(x => x.Ip == "")) { hops.RemoveRange(hops.Count - 4, 4); break; }   // 5 silent hops in a row: the rest won't answer either
            }
            return hops;
        }
        // the hops the reply needed to come back: starting TTL (64 / 128 / 255) minus what arrived
        public static int BackHops(int replyTtl) { if (replyTtl <= 0) return -1; int start = replyTtl <= 64 ? 64 : replyTtl <= 128 ? 128 : 255; return start - replyTtl + 1; }

        // ---------- the biggest packet that reaches hop `ttl` (IP size), searching between 576 and `hi`
        public static void MtuAt(IPAddress dst, Hop h, int hi)
        {
            Func<int, int> probe = ip =>
            {
                // routers rate-limit their error replies (often 1 a second): silence only counts after a pause and one more try,
                // or rate limiting reads as an "MTU black hole"
                for (int t = 0; t < 3; t++)
                {
                    if (t == 2) Thread.Sleep(1100);
                    var r = Icmp.Echo(dst, ip - 28, h.N, true, 1500, false);
                    if (r.Status == Icmp.TtlExpired || r.Status == Icmp.Ok) return 1;
                    if (r.Status == Icmp.TooBig) { h.Said = true; return 0; }
                }
                return -1;   // silence
            };
            hi = Math.Max(576, Math.Min(9216, hi));
            int top = probe(hi); if (top == 1) { h.Mtu = hi; h.MtuNote = hi > 1500 ? "jumbo frames - " + hi + " bytes get through" : hi >= 1500 ? "full size" : "same as before"; return; }
            int low = probe(576); if (low != 1) { h.Mtu = -1; h.MtuNote = "doesn't answer - can't measure"; return; }
            int lo = 576, up = hi; bool silent = top == -1;
            while (up - lo > 1) { int mid = (lo + up) / 2; int p = probe(mid); if (p == 1) lo = mid; else { up = mid; if (p == -1) silent = true; } }
            h.Mtu = lo; h.BlackHole = silent && !h.Said;
            h.MtuNote = Kind(lo) + (h.Said ? " - the router said so (packet too big)" : h.BlackHole ? " - big packets vanish here without a word (black hole)" : "");
        }
        public static string Kind(int mtu)
        {
            if (mtu > 1500) return "jumbo frames (" + mtu + ")"; if (mtu >= 1500) return "full size (Ethernet)"; if (mtu == 1492) return "PPPoE (8 bytes of wrapping)"; if (mtu == 1476) return "GRE tunnel";
            if (mtu >= 1450 && mtu < 1476) return "a tunnel (VXLAN / GRE-like)"; if (mtu >= 1350 && mtu < 1450) return "a VPN (IPsec / WireGuard)"; return "a small link - something wraps your packets heavily";
        }

        // ---------- MTR: one probe per hop per second, for `seconds`
        public static void Mtr(IPAddress dst, List<Hop> hops, int seconds, Func<bool> stop, Action tick)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < seconds && !stop())
            {
                var round = Stopwatch.StartNew();
                foreach (var h in hops.ToList())
                {
                    if (stop()) return;
                    var r = Icmp.Echo(dst, 32, h.N, false, 900, false); h.Sent++;
                    if (r.Status == Icmp.TtlExpired || r.Status == Icmp.Ok)
                    {
                        if (h.Ip == "") h.Ip = r.From;
                        if (h.Cur == "") { h.Cur = r.From; if (!h.Seen.Contains(r.From)) h.Seen.Add(r.From); }
                        else if (r.From != h.Cur) { h.Switches++; h.Cur = r.From; h.SwitchedAt = DateTime.Now; if (!h.Seen.Contains(r.From) && h.Seen.Count < 8) h.Seen.Add(r.From); }
                        double v = r.Rtt; if (!double.IsNaN(h.Last)) h.Jit += (Math.Abs(v - h.Last) - h.Jit) / 16; h.Last = v; h.Sum += v;
                        h.Best = double.IsNaN(h.Best) ? v : Math.Min(h.Best, v); h.Worst = double.IsNaN(h.Worst) ? v : Math.Max(h.Worst, v);
                        lock (h.Hist) if (h.Hist.Count < 3600) h.Hist.Add(v);
                    }
                    else { h.Lost++; lock (h.Hist) if (h.Hist.Count < 3600) h.Hist.Add(double.NaN); }
                }
                tick();
                int wait = 1000 - (int)round.ElapsedMilliseconds; if (wait > 0) Thread.Sleep(wait);
            }
        }
        // the recording: one row per round, one column per hop - opens in Excel; kept in AdeeboMeter's own folder
        public static string SaveCsv(List<Hop> hops, string target, DateTime start)
        {
            try
            {
                string dir = System.IO.Path.Combine(Program.WriteDir, "mtr"); System.IO.Directory.CreateDirectory(dir);
                string safe = new string((target ?? "").Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '-').Take(40).ToArray());
                string f = System.IO.Path.Combine(dir, "mtr-" + safe + "-" + start.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".csv");
                var sb = new System.Text.StringBuilder(); var list = hops.ToList();
                sb.AppendLine("second," + string.Join(",", list.Select(h => "hop " + h.N + " " + (h.Ip == "" ? "*" : h.Ip))));
                int rounds = list.Count == 0 ? 0 : list.Max(h => { lock (h.Hist) return h.Hist.Count; });
                for (int i = 0; i < rounds; i++) sb.AppendLine(i.ToString(CultureInfo.InvariantCulture) + "," + string.Join(",", list.Select(h => { lock (h.Hist) return i < h.Hist.Count ? (double.IsNaN(h.Hist[i]) ? "lost" : h.Hist[i].ToString("0", CultureInfo.InvariantCulture)) : ""; })));
                System.IO.File.WriteAllText(f, sb.ToString()); return f;
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("mtr csv: " + e.Message); return null; }
        }
        // loss at a middle hop only counts if the hops after it lose too
        // a hop that never answered (no reply in the whole run) is a router that ignores probes, not loss - it can't start
        // or carry loss. Loss is real only from the first answering hop whose loss every later answering hop keeps (and there
        // must be a later answering hop that keeps it).
        // A hop that answered the trace and then lost everything during the run did answer once: 100% from there on is an
        // outage, not a quiet router (that's what lets "everything past hop 2 went dark" count as loss).
        // ...but only when it isn't alone: one quiet router at the end that answered the trace and then ignored every ping
        // is just a quiet router. It's an outage when the destination itself goes dark, or two or more traced hops do.
        static bool Answers(Hop h) { return h.Sent > 0 && h.Lost < h.Sent; }
        static bool Answers(List<Hop> hops, int i)
        {
            var h = hops[i]; if (h.Sent <= 0) return false; if (h.Lost < h.Sent) return true; if (h.Ip == "") return false;
            if (h.Reached) return true;
            int dark = hops.Count(x => x.Sent > 0 && x.Ip != "" && x.Lost >= x.Sent);
            return dark >= 2;
        }
        static double LossOf(Hop h) { return 100.0 * h.Lost / h.Sent; }
        public static int LossStart(List<Hop> hops)
        {
            for (int i = 0; i < hops.Count; i++)
            {
                var h = hops[i]; if (!Answers(hops, i)) continue; double l = LossOf(h); if (l < 3) continue;
                var later = Enumerable.Range(i + 1, Math.Max(0, hops.Count - i - 1)).Where(j => Answers(hops, j)).Select(j => hops[j]).ToList();
                if (later.Count == 0) continue;   // loss only at the last hop that answers (the destination too) is that box rate-limiting pings - it has to carry on to count
                if (later.All(x => LossOf(x) >= l * 0.6)) return i;
            }
            return -1;
        }
        public static string MtrVerdict(List<Hop> hops)
        {
            var last = hops.LastOrDefault(Answers); if (last == null) return "No hop answered - probes are blocked on the way out (a firewall or VPN).";
            bool silentEnd = !hops.Any(h => h.Reached && Answers(h));
            string tail = silentEnd ? " The destination doesn't answer pings at all, so this is judged by the last router that does (hop " + last.N + ")." : "";
            int i = LossStart(hops);
            if (i >= 0) { var h = hops[i]; return "Real loss starts at hop " + h.N + " (" + h.Ip + ") and carries on to the end - " + LossOf(h).ToString("0", CultureInfo.CurrentCulture) + "% lost from there." + tail; }
            double endLoss = LossOf(last);
            if (endLoss < 1 || silentEnd) return "No real loss: any loss at a middle hop doesn't carry on - that router just ignores some pings (normal)." + tail;
            return "Loss at the destination only (" + endLoss.ToString("0", CultureInfo.CurrentCulture) + "%) - the server itself drops or rate-limits pings.";
        }

        // What routing does that a PC can still see: several routers taking turns at one hop (ECMP / load sharing),
        // a hop that changed once and stayed (a reroute), and hops hidden inside a tunnel (MPLS without TTL propagation).
        public static List<string[]> Flags(List<Hop> hops)
        {
            var rows = new List<string[]>();
            foreach (var h in hops.Where(x => x.Seen.Count > 1))
            {
                if (h.Switches >= 3) rows.Add(Row("3", "Hop " + h.N + ": load sharing", h.Seen.Count + " routers take turns (" + string.Join(", ", h.Seen.Take(4)) + ")", "Equal-cost paths (ECMP) or a bundle: each flow is hashed onto one link. Normal - but an app on a bad link while others are fine can be one of these.", "m"));
                else rows.Add(Row("1", "Hop " + h.N + ": the path changed", "at " + h.SwitchedAt.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + " from " + h.Seen[0] + " to " + h.Cur, "A reroute: the network moved your traffic (a link or router went down or came back). Loss in the seconds around it is the convergence time.", "m"));
            }
            var end = hops.LastOrDefault(x => x.Reached);
            if (end != null && end.ReplyTtl > 0)
            {
                int back = BackHops(end.ReplyTtl);
                if (back - end.N >= 3) rows.Add(Row("3", "Hidden hops?", "the reply needed " + back + " hops, your trace saw " + end.N, "Either the way back is longer, or part of the path is a tunnel (MPLS, a VPN) that doesn't show its inside routers.", "i"));
            }
            for (int i = 1; i < hops.Count; i++)
            {
                var a = hops[i - 1]; var b = hops[i]; double ra = !double.IsNaN(a.Best) ? a.Best : a.Rtt, rb = !double.IsNaN(b.Best) ? b.Best : b.Rtt;
                if (ra >= 0 && rb >= 0 && rb - ra >= 25 && hops.Skip(i).Where(x => !double.IsNaN(x.Best) || x.Rtt >= 0).All(x => (!double.IsNaN(x.Best) ? x.Best : x.Rtt) >= rb - 5))
                { rows.Add(Row("3", "Hop " + b.N + ": +" + F(rb - ra) + " ms", "the delay jumps here and stays", "Distance (an ocean or a long-haul link) or a tunnel crossing many routers at once. Not a fault on its own.", "m")); break; }
            }
            return rows;
        }

        // ---------- Record Route
        public static List<string[]> RecordRoute(IPAddress dst)
        {
            var rows = new List<string[]>();
            var r = Icmp.Echo(dst, 32, 64, false, 2000, true);
            if (r.Status != Icmp.Ok) { rows.Add(Row("3", "Record Route", "no reply with the option set", "Many networks drop packets carrying IP options - nothing more can be learned this way.", "u")); return rows; }
            if (r.Route.Count == 0) { rows.Add(Row("3", "Record Route", "reply came back, but no router wrote its address", "The routers on this path ignore the option.", "m")); return rows; }
            string d = dst.ToString(); int at = r.Route.IndexOf(d);
            for (int i = 0; i < r.Route.Count; i++)
                rows.Add(new[] { "3", (at < 0 ? "slot " : i < at ? "there " : i == at ? "turn " : "back ") + (i + 1), r.Route[i] + (i == at ? "  (the destination)" : ""), "", "m" });
            rows.Add(new[] { r.Route.Count >= 9 && at < 0 ? "1" : "0", "Reading it", at < 0 ? "All 9 slots filled before the destination - it's too far for the way back to show. Try a nearer target (your provider's first hop, the VPN gateway)." : "Before the destination: the way there. After it: the routers on the way back.", "", "i" });
            return rows;
        }

        // ---------- overlapping networks (home LAN vs VPN)
        public static List<string[]> Overlap()
        {
            var rows = new List<string[]>(); var nets = new List<Tuple<string, uint, uint, bool>>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var u in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (u.Address.AddressFamily != AddressFamily.InterNetwork || u.IPv4Mask == null) continue;
                        uint ip = BE(u.Address), m = BE(u.IPv4Mask); if (m == 0) continue;
                        nets.Add(Tuple.Create(ni.Name, ip & m, m, Monitor.IsVpn(ni)));
                    }
                }
                catch (Exception e) { Program.Log("overlap: " + e.Message); }
            }
            foreach (var n in nets) rows.Add(new[] { "3", n.Item1 + (n.Item4 ? " (VPN)" : ""), Routes.Ip(n.Item2) + "/" + Bits(n.Item3), "", "w" });
            bool found = false;
            for (int i = 0; i < nets.Count; i++) for (int j = i + 1; j < nets.Count; j++)
                {
                    uint m = Math.Min(nets[i].Item3, nets[j].Item3);   // the wider mask
                    if (nets[i].Item1 == nets[j].Item1) continue;   // two addresses on one adapter aren't an overlap
                    if ((nets[i].Item2 & m) == (nets[j].Item2 & m))
                    { found = true; rows.Insert(0, new[] { "2", "Overlap", nets[i].Item1 + " and " + nets[j].Item1 + " use the same addresses (" + Routes.Ip(nets[i].Item2) + ")", "Anything in that range can only be reached on one of them - usually the company servers on the VPN become unreachable from home. The fix is on the home router (change its network, e.g. to 192.168.77.x).", "i" }); }
                }
            try
            {
                var routes = Routes.Read(); var lan = nets.Where(n => !n.Item4).ToList();
                foreach (var r in routes.Where(r => r.Vpn && r.Prefix > 0 && r.Prefix < 32))
                    // only a VPN route at least as specific as the LAN's own wins over it (0/1 + 128/1 of a full tunnel doesn't)
                    foreach (var l in lan) { uint m = Math.Min(r.Mask, l.Item3); if (r.Mask >= l.Item3 && (r.Dest & m) == (l.Item2 & m)) { found = true; rows.Insert(0, new[] { "1", "VPN route over your LAN", "the VPN claims " + Routes.Ip(r.Dest) + "/" + r.Prefix + ", which covers your home network " + Routes.Ip(l.Item2), "Printers and other home devices may become unreachable while the VPN is up.", "i" }); } }
            }
            catch (Exception e) { Program.Log("overlap routes: " + e.Message); }
            if (!found) rows.Insert(0, Row("0", "Overlap", "none - your networks and the VPN don't share addresses", "", "i"));
            return rows;
        }
        static uint BE(IPAddress a) { var b = a.GetAddressBytes(); return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]); }
        static int Bits(uint m) { int n = 0; while ((m & 0x80000000) != 0) { n++; m <<= 1; } return n; }

        // ---------- IPv6 vs IPv4 to the same site
        public static List<string[]> Ip46(string host)
        {
            var rows = new List<string[]>(); IPAddress[] all;
            try { all = Dns.GetHostAddresses(host); } catch (Exception e) { rows.Add(new[] { "2", "Name", host + ": " + e.Message, "", "m" }); return rows; }
            var v4 = all.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork); var v6 = all.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);
            double t4 = v4 == null ? double.NaN : Connect(v4, 443, 3000), t6 = v6 == null ? double.NaN : Connect(v6, 443, 3000);
            rows.Add(new[] { v4 == null ? "3" : double.IsNaN(t4) ? "2" : "0", "IPv4", v4 == null ? "the site has no IPv4 address" : double.IsNaN(t4) ? "couldn't connect to " + v4 : F(t4) + " ms to " + v4, "", "m" });
            rows.Add(new[] { v6 == null ? "3" : double.IsNaN(t6) ? "2" : "0", "IPv6", v6 == null ? "the site has no IPv6 address (or your PC has no IPv6 DNS answer)" : double.IsNaN(t6) ? "couldn't connect to " + v6 : F(t6) + " ms to " + v6, "", "m" });
            // every combination: has v4 / v6, and whether each connected
            bool ok4 = !double.IsNaN(t4), ok6 = !double.IsNaN(t6); string verdict; int vl;
            if (ok4 && ok6) { verdict = t6 > t4 + 30 ? "Both work, but IPv6 is slower by " + F(t6 - t4) + " ms - its route takes a detour." : "Both work about equally well."; vl = t6 > t4 + 30 ? 1 : 0; }
            else if (ok4 && v6 != null) { verdict = "IPv6 is broken here: the PC tries it first and waits before falling back to IPv4 - sites feel slow to start. Fix: the router's IPv6 settings, or ask your provider."; vl = 2; }
            else if (ok4) { verdict = "Only IPv4 here - nothing to compare."; vl = 0; }
            else if (ok6 && v4 != null) { verdict = "IPv4 is broken here, IPv6 works - anything without IPv6 fails. Check the router, or ask your provider."; vl = 2; }
            else if (ok6) { verdict = "The site is IPv6-only, and IPv6 works here."; vl = 0; }
            else if (v4 == null && v6 == null) { verdict = "The name has no addresses."; vl = 2; }
            else { verdict = "Neither connected - the site, the port (443) or the way out is down."; vl = 2; }
            rows.Add(new[] { vl.ToString(CultureInfo.InvariantCulture), "Verdict", verdict, "", "i" });
            return rows;
        }
        public static double Connect(IPAddress a, int port, int timeout)
        {
            try
            {
                using (var s = new Socket(a.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                {
                    var sw = Stopwatch.StartNew(); var ar = s.BeginConnect(a, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeout)) return double.NaN;
                    s.EndConnect(ar); return sw.Elapsed.TotalMilliseconds;
                }
            }
            catch { return double.NaN; }
        }
    }
}
