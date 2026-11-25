// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace AdeeboMeter
{
    // ================================================================== item 2 for the three groups (Adeeb, 15 Nov)
    // What changed on my server (a snapshot of its DNS, certificate, open ports and banners each run, compared with the last ones),
    // the trading session tape (the broker all session, every slow moment and who caused it) and the after-stream report (the
    // stream minute by minute, every drop and its cause). The last two read what the Trading and Streaming pages already record.
    static partial class Sectors
    {
        static readonly int[] Watched = { 22, 25, 80, 443, 445, 587, 993, 3389, 8080, 8443 };
        static readonly char[] Tab = { '\t' }, Eq = { '=' };
        static readonly string[] DotS = { " · " }, MxNs = { "MX", "NS" };

        // ---------- server people: what changed since the last check
        public static List<string[]> ServerChange(string target, Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string host; int port;
            if (!Split(target, 443, out host, out port)) { rows.Add(R(2, "Server", "type a host name or address, e.g. mail.example.com", "", "u")); return rows; }
            run.Status = "DNS records..."; var now = new SortedDictionary<string, string>(StringComparer.Ordinal);
            try { var a = Dns.GetHostAddresses(host); now["DNS A"] = string.Join(" ", a.Where(x => x.AddressFamily == AddressFamily.InterNetwork).Select(x => x.ToString()).OrderBy(x => x, StringComparer.Ordinal).ToArray()); now["DNS AAAA"] = string.Join(" ", a.Where(x => x.AddressFamily == AddressFamily.InterNetworkV6).Select(x => x.ToString()).OrderBy(x => x, StringComparer.Ordinal).ToArray()); }
            catch (SocketException) { now["DNS A"] = "(no answer)"; } catch (ArgumentException) { now["DNS A"] = "(not a name)"; }
            foreach (var t in MxNs) { if (run.Stop) return rows; var d = Toolkit.Dig(host, t, "", m).Where(r => r.Length > 2 && (r[1] == t || r[1].StartsWith(t + " ", StringComparison.Ordinal))).Select(r => System.Text.RegularExpressions.Regex.Replace(r[2].Split(DotS, StringSplitOptions.None)[0], @"\s*\(TTL[^)]*\)", "").Trim())   /* review 1.1.6 P3 #5: rows are "MX  example.com", and the TTL counts down */.OrderBy(x => x, StringComparer.Ordinal).ToArray(); if (d.Length > 0) now["DNS " + t] = string.Join(" ", d); }
            run.Status = "Open ports..."; var open = new List<int>();
            foreach (var p in Watched.Concat(Watched.Contains(port) ? Enumerable.Empty<int>() : new[] { port })) { if (run.Stop) return rows; using (var c = Toolkit.Open(host, p, 2000)) if (c != null) open.Add(p); }
            now["Open ports"] = open.Count == 0 ? "(none of " + (Watched.Length + (Watched.Contains(port) ? 0 : 1)) + ")"   /* L-203: the ports really tried */ : string.Join(" ", open.Select(p => p.ToString(CultureInfo.InvariantCulture)).ToArray());
            // bug-hunt ML-103: the certificate on the port you named too (8443, 993, 636...), not only 443
            int certPort = open.Contains(port) && port != 22 && port != 25 && port != 80 && port != 53 ? port : open.Contains(443) ? 443 : -1;
            if (certPort > 0) { run.Status = "Certificate..."; string iss, exp, fp; DateTime na0; Cert(host, certPort, out iss, out exp, out fp, out na0); if (iss != "") { now["Certificate issuer"] = iss; now["Certificate expires"] = exp; now["Certificate fingerprint"] = fp; } }
            if (open.Contains(22)) now["SSH banner"] = Banner(host, 22);
            if (open.Contains(25)) now["SMTP banner"] = Banner(host, 25);

            // the history: one line per check, newest last, at most 30
            string dir = Path.Combine(Program.DataDir, "servers"), file = Path.Combine(dir, Safe(host) + ".txt"); var hist = new List<KeyValuePair<DateTime, Dictionary<string, string>>>();
            try { if (File.Exists(file)) foreach (var line in File.ReadAllLines(file)) { var p = line.Split(Tab); DateTime t; if (p.Length < 1 || !DateTime.TryParseExact(p[0], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) continue; hist.Add(new KeyValuePair<DateTime, Dictionary<string, string>>(t, p.Skip(1).Select(z => z.Split(Eq, 2)).Where(z => z.Length == 2).GroupBy(z => z[0]).ToDictionary(z => z.Key, z => z.Last()[1]))); } }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            var last = hist.Count > 0 ? hist[hist.Count - 1].Value : null; var changes = new List<string[]>();
            if (last != null) foreach (var k in now.Keys.Union(last.Keys).OrderBy(z => z, StringComparer.Ordinal)) { string a, b; last.TryGetValue(k, out a); now.TryGetValue(k, out b); if ((a ?? "") != (b ?? "")) changes.Add(new[] { k, a ?? "(none)", b ?? "(gone)" }); }
            bool outage = (now.ContainsKey("DNS A") && now["DNS A"].StartsWith("(", StringComparison.Ordinal)) || open.Count == 0; bool saved = false;
            if (!outage) hist.Add(new KeyValuePair<DateTime, Dictionary<string, string>>(DateTime.Now, now.ToDictionary(z => z.Key, z => z.Value)));
            if (!outage) try { Directory.CreateDirectory(dir); WriteWhole(file, string.Join("\n", hist.Skip(Math.Max(0, hist.Count - 30)).Select(h => h.Key.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\t" + string.Join("\t", h.Value.Select(z => z.Key + "=" + z.Value.Replace("\t", " ").Replace("\n", " ")).ToArray())).ToArray()) + "\n"); saved = true; }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            if (outage) rows.Add(R(1, "Not kept", "no DNS answer or no open port - it looks like an outage, so this check isn't kept as the new baseline", "", "m"));
            else if (!saved) rows.Add(R(1, "Not kept", "couldn't save this check - the next run will compare with the older one", "", "m"));

            if (last == null) rows.Add(R(3, "First check", host + " - " + now.Count + " things noted" + (saved ? "" : " (not kept)") + ". Run it again later (after a deploy, a renewal, a migration) to see what changed.", "Kept in AdeeboMeter's own folder (servers), last 30 checks.", "m"));
            else rows.Add(R(changes.Count == 0 ? 0 : 1, "Since " + hist[hist.Count - (outage ? 1 : 2)].Key.ToString("d MMM HH:mm", CultureInfo.CurrentCulture), changes.Count == 0 ? "nothing changed" : changes.Count + " change" + (changes.Count == 1 ? "" : "s"), "", "m"));
            foreach (var c in changes) rows.Add(R(c[0].StartsWith("Certificate expires", StringComparison.Ordinal) || c[0] == "Open ports" || c[0] == "DNS A" ? 1 : 3, c[0], c[1] + "  →  " + c[2], "", "m"));
            rows.Add(R(-1, "NOW", "", "", "m"));
            foreach (var kv in now) rows.Add(R(3, kv.Key, kv.Value, "", "m"));
            // earlier changes, as a timeline (newest first)
            var past = new List<string[]>();
            for (int i = hist.Count - (outage ? 1 : 2); i >= 1 && past.Count < 8; i--)   // bug-hunt L-204: an outage run wasn't added, so the newest pair starts one later
                foreach (var k in hist[i].Value.Keys.Union(hist[i - 1].Value.Keys)) { string a, b; hist[i - 1].Value.TryGetValue(k, out a); hist[i].Value.TryGetValue(k, out b); if ((a ?? "") != (b ?? "")) past.Add(R(3, hist[i].Key.ToString("d MMM HH:mm", CultureInfo.CurrentCulture), k + ": " + (a ?? "(none)") + " → " + (b ?? "(gone)"), "", "m")); }
            if (past.Count > 0) { rows.Add(R(-1, "EARLIER CHANGES", hist.Count + " checks kept", "", "m")); rows.AddRange(past); }
            return rows;
        }
        internal static string Safe(string host) { var sb = new StringBuilder(); foreach (char c in host) sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : '_'); return sb.ToString(); }
        static void Cert(string host, out string issuer, out string expires, out string fp) { DateTime na; Cert(host, 443, out issuer, out expires, out fp, out na); }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5359", Justification = "reads the certificate only - nothing is sent")]
        static void Cert(string host, int port, out string issuer, out string expires, out string fp, out DateTime notAfter)
        {
            issuer = expires = fp = ""; notAfter = DateTime.MinValue; System.Security.Cryptography.X509Certificates.X509Certificate2 got = null;
            try { using (var c = Toolkit.Open(host, port, 4000)) { if (c == null) return; using (var ssl = new SslStream(c.GetStream(), false, (o, cert, ch, e) => { if (cert != null) got = new System.Security.Cryptography.X509Certificates.X509Certificate2(cert); return true; })) { var hs = System.Threading.Tasks.Task.Factory.StartNew(() => ssl.AuthenticateAsClient(host)); bool done; try { done = hs.Wait(8000); } catch (AggregateException) { done = true; } if (!done) { c.Close(); try { hs.Wait(1000); } catch (AggregateException) { } } } } }   /* review 1.1.6 P3 #3: an 8 s deadline - a port that accepts and then waits can't hang the tool (closing the socket ends the handshake) */
            catch (IOException) { } catch (System.Security.Authentication.AuthenticationException) { } catch (SocketException) { }
            if (got == null) return;
            using (got) { notAfter = got.NotAfter; issuer = got.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.SimpleName, true); expires = got.NotAfter.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); fp = got.GetCertHashString().Substring(0, 16); }
        }
        static string Banner(string host, int port)
        {
            try
            {
                using (var c = Toolkit.Open(host, port, 3000))
                {
                    if (c == null) return "(no answer)"; c.ReceiveTimeout = 3000; var s = c.GetStream(); var buf = new byte[256]; int n = s.Read(buf, 0, buf.Length);
                    return Encoding.ASCII.GetString(buf, 0, Math.Max(0, n)).Split('\r', '\n')[0].Trim();
                }
            }
            catch (IOException) { return "(no banner)"; } catch (SocketException) { return "(no banner)"; }
        }

        // ---------- traders: the session tape, and streamers: the after-stream report - both from WatchKit sessions
        public static WatchKit.Session PickSession(string mode, string which)
        {
            WatchKit.Session live = null;
            if (mode == "trade") lock (Trading.k) live = Trading.Day == null ? null : Trading.Day.Copy();
            else lock (Streams.k) { var n = Streams.Now; if (n != null) live = n.S.Copy(); }
            var all = WatchKit.RecentNow(mode).OrderBy(s => s.Start).ToList(); if (live != null && !all.Contains(live)) all.Add(live);
            string w = (which ?? "").Trim().ToLowerInvariant(); DateTime d;
            if (w == "" || w == "last") return all.LastOrDefault();
            if (w == "today") return all.LastOrDefault(s => s.Start.Date == DateTime.Today);
            if (w == "yesterday") return all.LastOrDefault(s => s.Start.Date == DateTime.Today.AddDays(-1));
            if (DateTime.TryParse(w, CultureInfo.CurrentCulture, DateTimeStyles.None, out d))
            {
                // AM-CG-120: a time picks that day's session running then (or the nearest start), not just the day's last
                if (d.TimeOfDay != TimeSpan.Zero) return all.Where(s => s.Start.Date == d.Date).OrderBy(s => d >= s.Start && d <= s.End ? 0 : Math.Abs((s.Start - d).TotalMinutes)).FirstOrDefault();
                return all.LastOrDefault(s => s.Start.Date == d.Date);
            }
            return null;
        }
        public static List<string[]> SessionRows(string mode, string which)
        {
            var rows = new List<string[]>(); var s = PickSession(mode, which); bool trade = mode == "trade";
            string wq = (which ?? "").Trim(); DateTime dd;
            if (s == null && wq != "" && !"today last yesterday".Split(' ').Contains(wq.ToLowerInvariant()) && !DateTime.TryParse(wq, CultureInfo.CurrentCulture, DateTimeStyles.None, out dd)) { rows.Add(R(1, "Which session", "didn't understand \"" + wq + "\" - type today, yesterday, last or a date like 14 Nov", "", "u")); return rows; }
            if (s == null && wq != "") { rows.Add(R(3, trade ? "No session" : "No stream", "nothing recorded for " + wq, "", "u")); return rows; }
            if (s == null) { rows.Add(R(3, trade ? "No session yet" : "No stream yet", trade ? "turn on broker watching on the Trading page - it records the broker all session" : "the Streaming page records each stream by itself - watch something, then come back", "", "u")); return rows; }
            var far = s.Line.Where(p => p.Length > 2 && p[2] >= 0).Select(p => (double)p[2]).ToList(); var down = s.Line.Where(p => p.Length > 1 && p[1] >= 0).Select(p => (double)p[1]).ToList();
            var tally = s.Tally(); int dips = s.Dips.Count;
            string farN = trade ? "your broker" : s.Far;   // AM-CG-123: a shared result names no broker address
            var sameDay = WatchKit.RecentNow(mode).Where(o => o.Start.Date == s.Start.Date && Math.Abs((o.Start - s.Start).TotalSeconds) > 1).OrderBy(o => o.Start).ToList();
            if (sameDay.Count > 0) rows.Add(R(3, "Other sessions that day", string.Join(", ", sameDay.Select(o => o.Start.ToString("HH:mm", CultureInfo.CurrentCulture) + "-" + o.End.ToString("HH:mm", CultureInfo.CurrentCulture)).ToArray()), "Type a start time (like " + sameDay[0].Start.ToString("HH:mm", CultureInfo.CurrentCulture) + ") to see one of them.", "m"));
            // AM-CG-115: a session with no readings at all isn't a clean one
            if (far.Count == 0 && down.Count == 0) { rows.Add(R(3, trade ? "Session" : "Stream", s.Start.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture) + " - " + s.End.ToString("HH:mm", CultureInfo.CurrentCulture) + " · no readings were recorded, so it can't be judged", "", "m")); return rows; }
            rows.Add(R(dips == 0 ? 0 : dips > 5 ? 2 : 1, trade ? "Session" : "Stream", (trade ? WatchKit.BlameName("far", farN) : s.What) + " · " + s.Start.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture) + " - " + s.End.ToString("HH:mm", CultureInfo.CurrentCulture) + " · " + (int)s.Length.TotalMinutes + " min", "", "m"));
            rows.Add(R(dips == 0 ? 0 : 1, trade ? "Slow moments" : "Drops", dips == 0 ? "none" : dips + " · " + string.Join(", ", tally.Where(z => z.Value > 0).OrderByDescending(z => z.Value).Select(z => z.Value + " " + WatchKit.BlameName(z.Key, farN)).ToArray()), "", "m"));
            if (far.Count > 0) rows.Add(R(3, trade ? "Broker" : "The service", Math.Round(far.Average()) + " ms on average · worst minute " + Math.Round(far.Max()) + " ms" + (!double.IsNaN(s.FarNormal) ? " · normal " + Math.Round(s.FarNormal) + " ms" : ""), "", "m"));
            if (!trade && down.Count > 0) rows.Add(R(3, "Download", Math.Round(down.Average(), 1) + " Mb/s on average · lowest minute " + Math.Round(down.Min(), 1), "", "m"));
            if (dips > 0)
            {
                rows.Add(R(-1, trade ? "EVERY SLOW MOMENT" : "EVERY DROP", "", "", "m"));
                if (s.Dips.Count > 20) rows.Add(R(3, "Showing", "the first 20 of " + s.Dips.Count, "", "m"));
                foreach (var d in s.Dips.Take(20)) rows.Add(R(d.Blame == "provider" || d.Blame == "far" ? 2 : 1, d.T.ToString("HH:mm:ss", CultureInfo.CurrentCulture), d.Secs + " s · " + WatchKit.BlameName(d.Blame, farN) + (d.Evidence != "" ? " · " + d.Evidence : ""), "", "m"));
                var top = tally.OrderByDescending(z => z.Value).First(); var lesson = WatchKit.Lesson(top.Key, mode);
                if (lesson != null && lesson.Length > 0) rows.Add(R(1, "Most of it", WatchKit.BlameName(top.Key, farN), string.Join(" ", lesson), "i"));
            }
            return rows;
        }
    }
}
