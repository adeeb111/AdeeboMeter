// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    // ================================================================== what is that device? (Devices on this network)
    // Asked the way phones and TVs ask each other, no admin and no capture: one mDNS question (Bonjour: Chromecasts,
    // printers, speakers, HomeKit/Matter gadgets answer with their names), one SSDP search (UPnP: TVs, routers, consoles,
    // media boxes answer with a link to a description page, read once - name, maker, model), and the router's own name
    // for each address (reverse DNS). Read-only: nothing is logged in to, nothing is changed on any device, nothing is
    // kept on disk - names are held in memory for 10 minutes. About 3 seconds.
    static class DevId
    {
        public sealed class Info { public string Name = "", Maker = "", Model = "", Kind = "", Via = ""; }
        static Dictionary<string, Info> cache = new Dictionary<string, Info>(); static DateTime cachedAt = DateTime.MinValue; static readonly object k = new object();
        static readonly string[] Types = { "_googlecast._tcp", "_airplay._tcp", "_raop._tcp", "_ipp._tcp", "_printer._tcp", "_pdl-datastream._tcp", "_scanner._tcp", "_hap._tcp", "_matter._tcp", "_matterc._udp",
            "_spotify-connect._tcp", "_sonos._tcp", "_amzn-wplay._tcp", "_androidtvremote2._tcp", "_homekit._tcp", "_smb._tcp", "_device-info._tcp", "_workstation._tcp", "_http._tcp", "_esphomelib._tcp", "_hue._tcp", "_companion-link._tcp" };

        public static Dictionary<string, Info> Identify(Func<bool> stop)
        {
            lock (k) if ((DateTime.UtcNow - cachedAt).TotalMinutes < 10 && cache.Count > 0) return new Dictionary<string, Info>(cache);
            var found = new Dictionary<string, Info>(); var fl = new object();
            Action<string, Action<Info>> add = (ip, f) => { lock (fl) { Info i; if (!found.TryGetValue(ip, out i)) found[ip] = i = new Info(); f(i); } };
            var t1 = Task.Factory.StartNew(() => { try { Mdns(add, stop); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("devid mdns: " + e.Message); } });
            var t2 = Task.Factory.StartNew(() => { try { Ssdp(add, stop); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("devid ssdp: " + e.Message); } });
            Task.WaitAll(new[] { t1, t2 }, 6000);
            lock (fl) lock (k) { cache = new Dictionary<string, Info>(found); cachedAt = DateTime.UtcNow; return new Dictionary<string, Info>(found); }
        }

        // the router's own name for an address (many home routers name devices after their DHCP request) - at most 1.5 s for all
        public static Dictionary<string, string> Names(IEnumerable<string> ips)
        {
            var res = new Dictionary<string, string>(); var l = new object();
            var tasks = ips.Distinct().Take(64).Select(ip => Task.Factory.StartNew(() =>
            {
                try { var h = Dns.GetHostEntry(IPAddress.Parse(ip)).HostName; if (!string.IsNullOrEmpty(h) && h != ip && !Regex.IsMatch(h, @"^\d+[-.]\d+[-.]\d+[-.]\d+")) lock (l) res[ip] = Regex.Replace(h, @"\.(local|lan|home|localdomain|home\.arpa)\.?$", "", RegexOptions.IgnoreCase); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; }
            })).ToArray();
            Task.WaitAll(tasks, 1500);
            lock (l) return new Dictionary<string, string>(res);
        }

        // ---------- mDNS: one question for the common service types, answers asked to come straight back (the QU bit)
        static void Mdns(Action<string, Action<Info>> add, Func<bool> stop)
        {
            var q = new List<byte> { 0, 0, 0, 0, 0, (byte)Types.Length, 0, 0, 0, 0, 0, 0 };
            foreach (var t in Types) { foreach (var part in (t + ".local").Split('.')) { q.Add((byte)part.Length); q.AddRange(Encoding.ASCII.GetBytes(part)); } q.AddRange(new byte[] { 0, 0, 12, 0x80, 1 }); }   // PTR, IN, unicast reply
            using (var u = new UdpClient(new IPEndPoint(IPAddress.Any, 0)))
            {
                u.Client.ReceiveTimeout = 400; var to = new IPEndPoint(IPAddress.Parse("224.0.0.251"), 5353);
                u.Send(q.ToArray(), q.Count, to);
                var until = DateTime.UtcNow.AddMilliseconds(2600); bool again = false;
                while (DateTime.UtcNow < until && !stop())
                {
                    if (!again && DateTime.UtcNow > until.AddMilliseconds(-1600)) { again = true; try { u.Send(q.ToArray(), q.Count, to); } catch (SocketException) { } }   // one repeat: Wi-Fi drops multicast
                    IPEndPoint from = null; byte[] b;
                    try { b = u.Receive(ref from); } catch (SocketException) { continue; }
                    if (from == null || !Lan(from.Address)) continue;
                    try { ParseMdns(b, from.Address.ToString(), add); } catch (IndexOutOfRangeException) { } catch (ArgumentException) { }
                }
            }
        }
        static void ParseMdns(byte[] b, string ip, Action<string, Action<Info>> add)
        {
            if (b.Length < 12) return; int p = 12, qd = b[4] << 8 | b[5], rr = (b[6] << 8 | b[7]) + (b[8] << 8 | b[9]) + (b[10] << 8 | b[11]);
            for (int i = 0; i < qd; i++) { Name(b, ref p); p += 4; }
            for (int i = 0; i < rr && p + 10 <= b.Length; i++)
            {
                string owner = Name(b, ref p); int type = b[p] << 8 | b[p + 1], len = b[p + 8] << 8 | b[p + 9]; p += 10; int end = p + len; if (end > b.Length) return;
                if (type == 12) { int q = p; string inst = Name(b, ref q); string svc = owner.ToLowerInvariant(); string nice = inst.EndsWith("." + owner, StringComparison.OrdinalIgnoreCase) ? inst.Substring(0, inst.Length - owner.Length - 1) : inst.Split('.')[0];   /* L-208: the whole instance name ("Mr. Smith's TV"), up to the service */
                    add(ip, x => { if (x.Name == "" && nice != "" && !nice.StartsWith("_", StringComparison.Ordinal)) x.Name = Clean(nice); if (x.Kind == "") x.Kind = KindOf(svc); x.Via = Via(x.Via, "Bonjour"); }); }
                else if (type == 16)
                {
                    var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); int q = p;
                    while (q < end) { int l = b[q++]; if (q + l > end) break; string s = Encoding.UTF8.GetString(b, q, l); q += l; int eq = s.IndexOf('='); if (eq > 0) kv[s.Substring(0, eq)] = s.Substring(eq + 1); }
                    string v; string svc = owner.ToLowerInvariant();
                    add(ip, x =>
                    {
                        if (kv.TryGetValue("fn", out v) && v != "") x.Name = Clean(v);                     // Chromecast / Google TV: its friendly name
                        if (x.Model == "" && kv.TryGetValue("md", out v)) x.Model = Clean(v);              // model (Chromecast, HomeKit, Apple)
                        if (x.Model == "" && kv.TryGetValue("model", out v)) x.Model = Clean(v);
                        if (x.Model == "" && kv.TryGetValue("ty", out v)) x.Model = Clean(v);              // printers: "HP LaserJet M404"
                        if (x.Maker == "" && kv.TryGetValue("usb_MFG", out v)) x.Maker = Clean(v);
                        if (x.Kind == "") x.Kind = KindOf(svc); x.Via = Via(x.Via, "Bonjour");
                    });
                }
                p = end;
            }
        }
        static string Name(byte[] b, ref int p)
        {
            var parts = new List<string>(); int jumps = 0, q = p; bool jumped = false;
            while (q < b.Length)
            {
                int l = b[q];
                if (l == 0) { q++; break; }
                if ((l & 0xC0) == 0xC0) { if (q + 1 >= b.Length || ++jumps > 16) break; int to = (l & 0x3F) << 8 | b[q + 1]; if (!jumped) p = q + 2; jumped = true; q = to; continue; }
                if (q + 1 + l > b.Length) break; parts.Add(Encoding.UTF8.GetString(b, q + 1, l)); q += 1 + l;
            }
            if (!jumped) p = q;
            return string.Join(".", parts);
        }
        static string KindOf(string svc)
        {
            if (svc.Contains("googlecast") || svc.Contains("androidtv") || svc.Contains("amzn-wplay") || svc.Contains("airplay")) return "TV / streaming";
            if (svc.Contains("ipp") || svc.Contains("printer") || svc.Contains("pdl-") || svc.Contains("scanner")) return "Printer";
            if (svc.Contains("raop") || svc.Contains("spotify") || svc.Contains("sonos")) return "Speaker";
            if (svc.Contains("hap") || svc.Contains("matter") || svc.Contains("homekit") || svc.Contains("hue") || svc.Contains("esphome")) return "Smart home";
            if (svc.Contains("smb") || svc.Contains("workstation") || svc.Contains("companion")) return "Computer";
            return "";
        }

        // ---------- SSDP: who's a UPnP device, and its own description page (name, maker, model)
        static void Ssdp(Action<string, Action<Info>> add, Func<bool> stop)
        {
            var msg = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: ssdp:all\r\nUSER-AGENT: Windows/10 UPnP/1.1 AdeeboMeter/1\r\n\r\n");
            var locs = new Dictionary<string, string>();
            using (var u = new UdpClient(new IPEndPoint(IPAddress.Any, 0)))
            {
                u.Client.ReceiveTimeout = 400; var to = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
                u.Send(msg, msg.Length, to);
                var until = DateTime.UtcNow.AddMilliseconds(2600); bool again = false;
                while (DateTime.UtcNow < until && !stop())
                {
                    if (!again && DateTime.UtcNow > until.AddMilliseconds(-1600)) { again = true; try { u.Send(msg, msg.Length, to); } catch (SocketException) { } }
                    IPEndPoint from = null; byte[] b;
                    try { b = u.Receive(ref from); } catch (SocketException) { continue; }
                    if (from == null || !Lan(from.Address)) continue;
                    string ip = from.Address.ToString(), txt = Encoding.ASCII.GetString(b, 0, Math.Min(b.Length, 4096));
                    var lm = Regex.Match(txt, @"^LOCATION:\s*(\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    var sm = Regex.Match(txt, @"^SERVER:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    add(ip, x => { x.Via = Via(x.Via, "UPnP"); if (x.Kind == "" && Regex.IsMatch(txt, "InternetGatewayDevice", RegexOptions.IgnoreCase)) x.Kind = "Router"; if (x.Kind == "" && Regex.IsMatch(txt, "MediaRenderer|dial-multiscreen", RegexOptions.IgnoreCase)) x.Kind = "TV / streaming"; });
                    if (lm.Success && !locs.ContainsKey(ip)) locs[ip] = lm.Groups[1].Value.Trim();
                    if (sm.Success) { string sv = sm.Groups[1].Value.Trim(); add(ip, x => { if (x.Model == "" && sv.Length < 80) x.Model = Clean(Regex.Replace(sv, @"\s*UPnP/\S+\s*", " ")); }); }
                }
            }
            // each description page once, only from the device that announced it, small and quick
            var tasks = locs.Take(24).Select(kv => Task.Factory.StartNew(() =>
            {
                Uri uri; if (!Uri.TryCreate(kv.Value, UriKind.Absolute, out uri) || uri.Scheme != "http" || uri.Host != kv.Key) return;
                try
                {
                    var rq = (HttpWebRequest)WebRequest.Create(uri); rq.Timeout = 2000; rq.ReadWriteTimeout = 2000; rq.AllowAutoRedirect = false; rq.Proxy = null; rq.UserAgent = "AdeeboMeter";
                    string xml;
                    using (var rs = rq.GetResponse()) using (var st = rs.GetResponseStream())
                    {
                        var buf = new byte[65536]; int n = 0, r; while (n < buf.Length && (r = st.Read(buf, n, buf.Length - n)) > 0) n += r;
                        xml = Encoding.UTF8.GetString(buf, 0, n);
                    }
                    Func<string, string> tag = t => { var m = Regex.Match(xml, "<(?:[\\w-]+:)?" + t + "(?:\\s[^>]*)?>([^<]{1,80})</(?:[\\w-]+:)?" + t + "\\s*>", RegexOptions.IgnoreCase);   /* L-209: <ns:friendlyName xml:lang="en"> too */ return m.Success ? Clean(WebUtility.HtmlDecode(m.Groups[1].Value)) : ""; };
                    string fn = tag("friendlyName"), mk = tag("manufacturer"), md = tag("modelName"), mn = tag("modelNumber");
                    add(kv.Key, x => { if (fn != "") x.Name = fn; if (mk != "") x.Maker = mk; if (md != "") x.Model = (md + (mn != "" && !md.Contains(mn) ? " " + mn : "")).Trim(); });
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; }
            })).ToArray();
            Task.WaitAll(tasks, 3000);
        }
        static bool Lan(IPAddress a) { var b = a.GetAddressBytes(); return b.Length == 4 && (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] < 32) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254)); }
        static string Via(string had, string what) { return had.Contains(what) ? had : had == "" ? what : had + ", " + what; }
        static string Clean(string s) { s = Regex.Replace(s ?? "", @"[\x00-\x1F​-‏]", " ").Trim(); s = Regex.Replace(s, @"\s{2,}", " "); return s.Length > 60 ? s.Substring(0, 60) : s; }
    }

    // ================================================================== keep an eye on chosen devices (no alerts - just their state)
    // A ping a minute to each device you chose to watch (up to 16): is it there, how quick, and how much of the time it
    // answered. Kept in memory for 24 hours; nothing pops up when one goes quiet - you look when you want to.
    static class DevWatch
    {
        public sealed class W { public string Mac = "", Ip = "", Name = ""; public readonly Ring Ms = new Ring(1440); public DateTime Since = DateTime.Now; }
        static readonly Dictionary<string, W> watched = new Dictionary<string, W>(); static readonly object k = new object();
        static DateTime last = DateTime.MinValue; static volatile bool busy, loaded;
        public static void Set(string mac, string ip, string name, bool on)
        {
            lock (k) { if (!on) { watched.Remove(mac); return; } W w; if (!watched.TryGetValue(mac, out w)) { if (watched.Count >= 16) return; watched[mac] = w = new W { Mac = mac }; } w.Ip = ip; w.Name = name; }
        }
        public static W Get(string mac) { lock (k) { W w; return watched.TryGetValue(mac, out w) ? w : null; } }
        public static int Count { get { lock (k) return watched.Count; } }
        public static void Tick()
        {
            if (!loaded) { loaded = true; try { Picks.LoadWatched(); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("watched: " + e.Message); } }
            if (busy || (Clock.Mono - last).TotalSeconds < 60) return;
            List<W> list; lock (k) list = watched.Values.ToList(); if (list.Count == 0) return;
            last = Clock.Mono; busy = true;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    Dictionary<string, string> ipOf = null; try { ipOf = Arp.Table().Where(e => e.Dynamic).GroupBy(e => e.Mac).ToDictionary(g => g.Key, g => g.First().Ip); } catch (Exception e) { if (e is OutOfMemoryException) throw; }
                    Parallel.ForEach(list, new ParallelOptions { MaxDegreeOfParallelism = 8 }, w => { string ip; if (ipOf != null && ipOf.TryGetValue(w.Mac, out ip)) w.Ip = ip; w.Ms.Add(Monitor.PingMs(w.Ip)); });
                }
                finally { busy = false; }
            });
        }
        // "up 99% of 6 h · usually 4 ms" for a watched device
        public static string Summary(W w)
        {
            var v = w.Ms.Values(1440); if (v.Length == 0) return "watching - first ping within a minute";
            int ok = v.Count(x => !double.IsNaN(x)); var good = v.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
            double hrs = v.Length / 60.0; string span = hrs >= 1 ? Math.Round(hrs, hrs < 10 ? 1 : 0).ToString(CultureInfo.InvariantCulture) + " h" : v.Length + " min";
            bool now = !double.IsNaN(v[v.Length - 1]);
            return (now ? "answering" : "not answering right now") + " · answered " + Math.Round(100.0 * ok / v.Length).ToString(CultureInfo.InvariantCulture) + "% of the last " + span + (good.Count > 0 ? " · usually " + Math.Round(good[good.Count / 2]).ToString(CultureInfo.InvariantCulture) + " ms" : "");
        }
    }
}
