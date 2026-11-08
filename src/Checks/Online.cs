// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace AdeeboMeter
{
    // Online lookups (BGP and who owns the network). OFF unless you switch it on, and it switches itself off again
    // (Settings: 5 min - 3 h, 30 min by default). Only public IP addresses and prefixes are sent - never private,
    // CGNAT or LAN addresses, never the names you type - and only to RIPEstat (stat.ripe.net) and PeeringDB, over HTTPS.
    static class Online
    {
        static string[] Row(params string[] a) { return a; }
        static DateTime until = DateTime.MinValue; static readonly object k = new object();
        static readonly Dictionary<string, object> cache = new Dictionary<string, object>();
        public static bool On { get { return DateTime.UtcNow < until; } }
        public static TimeSpan Left { get { var l = until - DateTime.UtcNow; return l < TimeSpan.Zero ? TimeSpan.Zero : l; } }
        public static void Start() { until = DateTime.UtcNow.AddMinutes(Math.Max(5, Math.Min(180, Cfg.OnlineMin))); Program.Log("online lookups on for " + Cfg.OnlineMin + " min"); }
        public static void Stop() { until = DateTime.MinValue; lock (k) cache.Clear(); Program.Log("online lookups off"); }
        public static void Tick() { if (until != DateTime.MinValue && !On) Stop(); }

        public static bool Shareable(string ip)
        {
            IPAddress a; if (!IPAddress.TryParse(ip ?? "", out a) || a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            return Watch.IsPublic(ip);   // excludes private, link-local, loopback and CGNAT (100.64/10)
        }

        // ---------- HTTPS GET + a small JSON reader (no external library)
        static object Get(string url)
        {
            if (!On) return null;
            lock (k) { object c; if (cache.TryGetValue(url, out c)) return c; }
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url); req.Timeout = 8000; req.ReadWriteTimeout = 8000; req.UserAgent = "AdeeboMeter/" + Program.Version; req.Proxy = WebRequest.GetSystemWebProxy();
                using (var resp = req.GetResponse()) using (var s = resp.GetResponseStream()) using (var r = new StreamReader(s, Encoding.UTF8))
                {
                    var sb = new StringBuilder(); var buf = new char[16384]; int n;   // read at most 4 MB, in small pieces - never the whole thing first
                    while ((n = r.Read(buf, 0, buf.Length)) > 0) { sb.Append(buf, 0, n); if (sb.Length > 4000000) return null; }
                    var text = sb.ToString();
                    var o = Json.Parse(text); lock (k) cache[url] = o; return o;
                }
            }
            catch (Exception e) { Program.Log("online: " + Draw.Trunc(e.Message, 80)); return null; }
        }
        static object P(object o, string path) { foreach (var part in path.Split('.')) { var d = o as Dictionary<string, object>; if (d == null) { var l = o as List<object>; int i; if (l != null && int.TryParse(part, out i) && i < l.Count) { o = l[i]; continue; } return null; } object v; if (!d.TryGetValue(part, out v)) return null; o = v; } return o; }
        static string S(object o, string path) { var v = P(o, path); return v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture); }

        // ---------- lookups
        public sealed class Net { public string Asn = "", Name = "", Prefix = "", Ix = ""; }
        public static Net Owner(string ip)
        {
            var n = new Net(); if (!Shareable(ip)) return n;
            var ni = Get("https://stat.ripe.net/data/network-info/data.json?resource=" + ip);
            n.Asn = S(ni, "data.asns.0"); n.Prefix = S(ni, "data.prefix");
            if (n.Asn != "") n.Name = AsName(n.Asn);
            var ix = Get("https://www.peeringdb.com/api/netixlan?ipaddr4=" + ip) ;
            string ixName = S(ix, "data.0.name"); if (ixName != "") n.Ix = ixName; else if (S(ix, "data.0.ix_id") != "") n.Ix = "an internet exchange (#" + S(ix, "data.0.ix_id") + ")";
            return n;
        }
        public static string AsName(string asn)
        {
            var o = Get("https://stat.ripe.net/data/as-overview/data.json?resource=AS" + Uri.EscapeDataString(asn.TrimStart('A', 'S', 'a', 's')));
            string h = S(o, "data.holder"); int dash = h.IndexOf(" - ", StringComparison.Ordinal); return dash > 0 && dash < 30 ? h.Substring(0, dash) : Draw.Trunc(h, 40);
        }
        public static string Rpki(string asn, string prefix)
        {
            if (asn == "" || prefix == "") return "";
            return S(Get("https://stat.ripe.net/data/rpki-validation/data.json?resource=AS" + Uri.EscapeDataString(asn) + "&prefix=" + Uri.EscapeDataString(prefix)), "data.status");
        }
        public static int UpdatesLastHours(string prefix, int hours)
        {
            var o = Get("https://stat.ripe.net/data/bgp-updates/data.json?resource=" + Uri.EscapeDataString(prefix) + "&starttime=" + DateTime.UtcNow.AddHours(-hours).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));
            var l = P(o, "data.updates") as List<object>; return l == null ? -1 : l.Count;
        }
        public static string MyIp() { return S(Get("https://stat.ripe.net/data/whats-my-ip/data.json"), "data.ip"); }

        // ---------- the path, network by network (the trace's public hops)
        public static List<string[]> PathOwners(List<PathTools.Hop> hops, string target)
        {
            var rows = new List<string[]>(); string prevAs = "", prevName = "";
            foreach (var h in hops)
            {
                if (h.Ip == "") { rows.Add(new[] { "3", "hop " + h.N, "no answer", "", "u" }); continue; }
                if (!Shareable(h.Ip)) { rows.Add(new[] { "3", "hop " + h.N, h.Ip + " · private / your provider's inside (not looked up)", "", "w" }); continue; }
                var n = Owner(h.Ip); h.Owner = n.Asn != "" ? "AS" + n.Asn + " " + n.Name : "";
                string hand = n.Asn != "" && prevAs != "" && n.Asn != prevAs ? "  ← handover from " + prevName + (n.Ix != "" ? " at " + n.Ix + " (peering)" : "") : n.Ix != "" ? "  · at " + n.Ix : "";
                rows.Add(new[] { hand != "" && hand.StartsWith("  ←", StringComparison.Ordinal) ? "4" : "3", "hop " + h.N, h.Ip + " · " + (n.Asn != "" ? "AS" + n.Asn + " " + n.Name : "owner unknown") + (h.Rtt >= 0 ? " · " + h.Rtt + " ms" : "") + hand, "", "i" });
                if (n.Asn != "" && n.Asn != prevAs) { prevAs = n.Asn; prevName = n.Name; }
            }
            // per network: how much delay each one added
            var per = hops.Where(h => h.Owner != "" && h.Rtt >= 0).GroupBy(h => h.Owner).Select(g => Tuple.Create(g.Key, g.Max(x => x.Rtt))).ToList();
            long before = 0; var parts = new List<string>();
            foreach (var p in per) { parts.Add(p.Item1.Split(' ').Skip(1).FirstOrDefault() + " +" + Math.Max(0, p.Item2 - before) + " ms"); before = Math.Max(before, p.Item2); }
            if (parts.Count > 0) rows.Insert(0, new[] { "3", "Delay by network", string.Join("  →  ", parts), "Where the milliseconds are added - call the network with the big number, not the others.", "i" });
            // the destination's route
            var last = hops.LastOrDefault(h => h.Reached && Shareable(h.Ip));
            if (last != null)
            {
                var n = Owner(last.Ip); string rp = Rpki(n.Asn, n.Prefix); int upd = n.Prefix != "" ? UpdatesLastHours(n.Prefix, 1) : -1;
                rows.Add(new[] { rp == "invalid" ? "2" : "0", "Destination route", (n.Prefix != "" ? n.Prefix : last.Ip) + " announced by AS" + n.Asn + " " + n.Name + " · RPKI: " + (rp == "" ? "unknown" : rp), rp == "invalid" ? "The announcement doesn't match what the owner signed - a leak or hijack. If the site misbehaves, this is why." : "", "i" });
                if (upd >= 0) rows.Add(new[] { upd > 50 ? "1" : "0", "Route changes", upd + " BGP updates for this prefix in the last hour", upd > 50 ? "The route is moving a lot right now - an outage or maintenance somewhere; problems may come and go." : "Quiet - the route is stable.", "i" });
            }
            string me = MyIp();
            if (me != "") { var n = Owner(me); rows.Insert(0, new[] { "3", "You leave the internet via", me + " · AS" + n.Asn + " " + n.Name, "Your home provider, your company, or a cloud security service (Zscaler, Netskope...) - this tells you which.", "i" }); }
            return rows;
        }

        // ---------- how the internet reaches you (AS paths toward your prefix from RIPE's route collectors)
        public static List<string[]> ReachMe()
        {
            var rows = new List<string[]>(); string me = MyIp(); if (me == "") { rows.Add(Row("3", "Your public address", "couldn't find it", "", "u")); return rows; }
            var n = Owner(me); rows.Add(new[] { "3", "You", me + " in " + (n.Prefix != "" ? n.Prefix : "?") + " · AS" + n.Asn + " " + n.Name, "", "i" });
            var lg = Get("https://stat.ripe.net/data/looking-glass/data.json?resource=" + Uri.EscapeDataString(n.Prefix != "" ? n.Prefix : me));
            var rrcs = P(lg, "data.rrcs") as List<object>;
            if (rrcs == null) { rows.Add(Row("3", "Route collectors", "no answer", "", "u")); return rows; }
            var names = new Dictionary<string, string>();
            foreach (var rc in rrcs.Take(8))
            {
                string loc = S(rc, "location"); var peers = P(rc, "peers") as List<object>; if (peers == null || peers.Count == 0) continue;
                string path = S(peers[0], "as_path"); var asns = path.Split(' ').Where(x => x != "").Distinct().ToList();
                var named = asns.Select(a => { string nm; if (!names.TryGetValue(a, out nm)) { nm = names.Count < 15 ? AsName(a) : ""; names[a] = nm; } return nm != "" ? nm.Split(' ')[0] : "AS" + a; });
                rows.Add(new[] { "3", "From " + (loc != "" ? loc : S(rc, "rrc")), string.Join(" → ", named), "", "i" });
            }
            rows.Add(Row("3", "Reading it", "Each line is how a network near that city reaches you - the way back from servers there, network by network.", "", "i"));
            return rows;
        }
    }

    // A small, strict JSON reader: objects, arrays, strings, numbers, true / false / null. Nothing is executed or resolved.
    static class Json
    {
        // nesting deeper than 64 is refused: a hostile server can't run the stack out (that would end the whole app)
        [ThreadStatic] static int depth;
        public static object Parse(string s) { int i = 0; depth = 0; try { return Val(s, ref i); } catch (InvalidDataException) { return null; } }
        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        static object Val(string s, ref int i)
        {
            Ws(s, ref i); if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{' || c == '[') { if (++depth > 64) throw new InvalidDataException("JSON nested too deep"); try { return Container(s, ref i, c); } finally { depth--; } }
            return Scalar(s, ref i, c);
        }
        static object Container(string s, ref int i, char c)
        {
            if (c == '{') { var d = new Dictionary<string, object>(); i++; Ws(s, ref i); if (i < s.Length && s[i] == '}') { i++; return d; } while (i < s.Length) { Ws(s, ref i); string key = Str(s, ref i); Ws(s, ref i); if (i < s.Length && s[i] == ':') i++; d[key] = Val(s, ref i); Ws(s, ref i); if (i < s.Length && s[i] == ',') { i++; continue; } if (i < s.Length && s[i] == '}') i++; break; } return d; }
            if (c == '[') { var l = new List<object>(); i++; Ws(s, ref i); if (i < s.Length && s[i] == ']') { i++; return l; } while (i < s.Length && l.Count < 100000) { l.Add(Val(s, ref i)); Ws(s, ref i); if (i < s.Length && s[i] == ',') { i++; continue; } if (i < s.Length && s[i] == ']') i++; break; } return l; }
            return null;
        }
        static object Scalar(string s, ref int i, char c)
        {
            if (c == '"') return Str(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            double v; if (i > st && double.TryParse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            i = Math.Max(i, st + 1); return null;
        }
        static string Str(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return "";
            var sb = new StringBuilder(); i++;
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char e = s[++i];
                    if (e == 'u' && i + 4 < s.Length) { int code; if (int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)) sb.Append((char)code); i += 4; }
                    else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                }
                else sb.Append(s[i]);
                i++;
            }
            i++; return sb.ToString();
        }
    }
}
