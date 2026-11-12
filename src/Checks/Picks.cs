// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // 1.1.5 picks: what changed when it broke · is this hotel / cafe Wi-Fi OK · DNS race · devices on this network ·
    // drivers and USB history · the internet weather sentence. Each runs only when asked; the inventory is kept from
    // the ARP table AdeeboMeter already reads every ten minutes.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "result rows and small lists, built once per run")]
    static class Picks
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string Hm(DateTime t) { return t.ToString(t.Date == DateTime.Today ? "HH:mm" : "ddd d MMM HH:mm", CultureInfo.CurrentCulture); }

        // ================================================================== what changed when it broke
        // Everything AdeeboMeter and Windows wrote down around the moment: network events, sleep and wake, updates,
        // drivers and installs. The closest change before the break is the first suspect.
        public sealed class Change { public DateTime T; public int Lv; public string What = "", Kind = ""; }
        public static List<string[]> Timeline(Monitor m, string when)
        {
            var rows = new List<string[]>(); DateTime at; string w = (when ?? "").Trim();
            var outs = Outages.Last(m, 7);
            if (w == "" || w.Equals("last outage", StringComparison.OrdinalIgnoreCase))
            {
                var o = outs.FirstOrDefault(); if (o == null) { rows.Add(R(3, "When", "no outage in the last 7 days - type when it broke, e.g. 18:40 or yesterday 18:40", "", "u")); return rows; }
                at = o.From; rows.Add(R(3, "The break", "the last outage: " + Outages.Text(o), "", "i"));
            }
            else if (!ParseWhen(w, out at)) { rows.Add(R(2, "When", "type a time like 18:40, yesterday 18:40 or 2026-11-10 09:15", "", "u")); return rows; }
            else rows.Add(R(3, "The break", Hm(at), "", "i"));
            var ch = Gather(m, at.AddHours(-48), at.AddHours(2));
            var before = ch.Where(c => c.T <= at.AddMinutes(1)).OrderByDescending(c => c.T).ToList();
            var suspect = before.FirstOrDefault(c => c.Kind == "update" || c.Kind == "driver" || c.Kind == "install" || c.Kind == "wake" || c.Kind == "net" || c.Kind == "wifi" || c.Kind == "vpn");
            if (suspect != null) rows.Insert(1, R(1, "First suspect", suspect.What + " - " + Ago(at - suspect.T) + " before it broke", suspect.Kind == "update" || suspect.Kind == "driver" ? "If it broke right after this, undo it: Settings › Windows Update › Update history (uninstall), or Device Manager › driver › Roll back." : suspect.Kind == "wake" ? "Right after waking, Wi-Fi and VPNs reconnect - a break then is usually the reconnect, not a fault." : "", "i"));
            else rows.Insert(1, R(0, "First suspect", "nothing changed on this PC in the 48 hours before - the cause is probably outside it (the router or the provider)", "", "i"));
            int n = 0;
            foreach (var c in ch.OrderBy(c => c.T))
            {
                if (++n > 80) break;
                bool after = c.T > at.AddMinutes(1);
                rows.Add(R(c.Lv, Hm(c.T) + (after ? "  (after)" : ""), c.What, "", c.Kind == "update" || c.Kind == "driver" || c.Kind == "install" ? "w" : "i"));
            }
            if (ch.Count == 0) rows.Add(R(3, "Nothing recorded", "no events around that time" + (Cfg.History ? "" : " - turn on History in Settings so AdeeboMeter keeps its events past two hours"), "", "i"));
            return rows;
        }
        static string Ago(TimeSpan d) { return d.TotalMinutes < 90 ? Math.Max(0, (int)d.TotalMinutes) + " min" : d.TotalHours < 48 ? ((int)d.TotalHours) + " h" : ((int)d.TotalDays) + " days"; }
        public static bool ParseWhen(string w, out DateTime at)
        {
            at = DateTime.MinValue; string s = w.Trim().ToLowerInvariant(); DateTime day = DateTime.Today;
            if (s.StartsWith("yesterday", StringComparison.Ordinal)) { day = day.AddDays(-1); s = s.Substring(9).Trim(); }
            else if (s.StartsWith("today", StringComparison.Ordinal)) s = s.Substring(5).Trim();
            var m = Regex.Match(s, @"^(\d{1,2})[:h.](\d{2})$"); TimeSpan ts;
            if (m.Success) { int h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), mi = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); if (h > 23 || mi > 59) return false; at = day.AddHours(h).AddMinutes(mi); if (at > DateTime.Now.AddMinutes(5) && day == DateTime.Today) at = at.AddDays(-1); return true; }
            DateTime full; if (DateTime.TryParse(w, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out full) || DateTime.TryParse(w, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out full)) { at = full; return at <= DateTime.Now.AddMinutes(5); }
            if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out ts)) { at = day + ts; return true; }
            return false;
        }
        static List<Change> Gather(Monitor m, DateTime from, DateTime to)
        {
            var l = new List<Change>();
            // AdeeboMeter's own timeline (history files when History is on, plus the last two hours in memory)
            var evs = new List<Timeline.Ev>();
            if (Cfg.History) for (var d = from.Date; d <= to.Date; d = d.AddDays(1)) evs.AddRange(History.EventsOf(d));
            if (m != null) foreach (var e in m.Events.Recent(300)) if (!evs.Any(x => x.T == e.T && x.Text == e.Text)) evs.Add(e);
            foreach (var e in evs.Where(e => e.T >= from && e.T <= to && e.Kind != "app"))
                l.Add(new Change { T = e.T, Lv = e.Lv == 2 ? 2 : e.Lv == 1 ? 1 : 3, What = e.Text, Kind = e.Kind == "inet" ? "inet" : e.Kind == "wifi" || e.Kind == "roam" ? "wifi" : e.Kind == "vpn" ? "vpn" : e.Kind == "net" || e.Kind == "lan" || e.Kind == "dns" ? "net" : e.Kind });
            double hours = Math.Max(1, (DateTime.Now - from).TotalHours + 1); string err;
            // Windows: updates, drivers, sleep and wake
            foreach (var e in EvLog.Recent("System", "EventID=19", hours, 40, out err).Where(x => x.T >= from && x.T <= to && x.Text.IndexOf("Installation Successful", StringComparison.OrdinalIgnoreCase) >= 0))
            { string t = EvLog.Field(e.Xml, "updateTitle"); l.Add(new Change { T = e.T, Lv = 3, What = "Windows Update installed " + (t != "" ? t : "an update"), Kind = "update" }); }
            foreach (var e in EvLog.Recent("System", "EventID=20001", hours, 30, out err).Where(x => x.T >= from && x.T <= to))
                l.Add(new Change { T = e.T, Lv = 3, What = "Driver installed: " + Draw.Trunc(Regex.Replace(e.Text, @"\s+", " "), 120), Kind = "driver" });
            foreach (var e in EvLog.Recent("System", "EventID=42 or EventID=1 or EventID=107", hours, 60, out err).Where(x => x.T >= from && x.T <= to && (x.Text.IndexOf("sleep", StringComparison.OrdinalIgnoreCase) >= 0 || x.Text.IndexOf("resum", StringComparison.OrdinalIgnoreCase) >= 0 || x.Text.IndexOf("wake", StringComparison.OrdinalIgnoreCase) >= 0 || x.Text.IndexOf("returned from", StringComparison.OrdinalIgnoreCase) >= 0)))
                l.Add(new Change { T = e.T, Lv = 3, What = e.Id == 42 ? "The PC went to sleep" : "The PC woke up", Kind = e.Id == 42 ? "sleep" : "wake" });
            // programs installed (the day only - Windows doesn't keep the time)
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
                foreach (var path in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
                    try
                    {
                        using (var k2 = root.OpenSubKey(path)) if (k2 != null) foreach (var s in k2.GetSubKeyNames().Take(800))
                                    using (var a = k2.OpenSubKey(s))
                                    {
                                        if (a == null) continue; string d = Convert.ToString(a.GetValue("InstallDate", ""), CultureInfo.InvariantCulture), n = Program.Plain(Convert.ToString(a.GetValue("DisplayName", ""), CultureInfo.InvariantCulture)); DateTime t;
                                        if (n != "" && DateTime.TryParseExact(d, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) && t >= from.Date && t <= to && !l.Any(x => x.What.EndsWith(n, StringComparison.Ordinal)))
                                            l.Add(new Change { T = t, Lv = 3, What = "Installed or updated (that day): " + n, Kind = "install" });
                                    }
                    }
                    catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("timeline installs: " + e.Message); }
            return l;
        }

        // ================================================================== is this Wi-Fi OK to use? (hotel, cafe, airport)
        public static List<string[]> Hotel(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string kind, ssid, gw, dns; bool vpn; NetworkInterface nic;
            lock (m.L) { kind = m.AdapterKind; ssid = m.Ssid; gw = m.Gateway; dns = m.DnsServer; vpn = m.VpnUp; nic = m.Primary; }
            if (nic == null || gw == "") { rows.Add(R(2, "Connection", "not connected to a network", "", "u")); return rows; }
            int bad = 0, warn = 0;
            Action<int, string, string, string> add = (lv, a, b, c) => { if (lv == 2) bad++; else if (lv == 1) warn++; rows.Add(R(lv, a, b, c, "i")); };
            // 1. the lock on the Wi-Fi itself
            if (kind == "Wi-Fi")
            {
                var prof = WlanApi.Profiles(); var mine = prof == null ? null : prof.FirstOrDefault(p => p.Item1 == ssid);
                string werr; var bss = WlanApi.List(false, out werr); string mb; lock (m.L) mb = m.Bssid;
                var me = bss.FirstOrDefault(b => string.Equals(b.Bssid, (mb ?? "").Replace('-', ':'), StringComparison.OrdinalIgnoreCase));
                string sec = me != null ? me.Sec : mine != null && mine.Item2 ? "Open" : "";
                if (sec == "Open" || sec == "WEP") add(2, "Wi-Fi lock", (ssid != "" ? "\"" + ssid + "\" " : "This network ") + (sec == "Open" ? "has no password - anyone nearby can see which sites you visit (not what's inside HTTPS) and can pretend to be it" : "uses WEP, broken for 20 years"), "Use a VPN on it, or your phone's hotspot.");
                else if (sec == "Enhanced Open") add(1, "Wi-Fi lock", "no password, but each device's traffic is encrypted (Enhanced Open) - better than open, still anyone can join", "");
                else if (sec != "") add(0, "Wi-Fi lock", sec + " - a password protects the air", "Everyone who has the password (all the guests) is still on the same network as you.");
                else add(3, "Wi-Fi lock", "couldn't read it (location access is off for desktop apps)", "");
                var twins = bss.Where(b => b.Ssid == ssid && b.Ssid != "").Select(b => b.Sec).Where(s => s != "").Distinct().ToList();
                if (twins.Count > 1 && twins.Any(WlanApi.Weak) && twins.Any(s => !WlanApi.Weak(s))) add(2, "Look-alike network", "\"" + ssid + "\" is on the air both with and without a password - make sure you joined the protected one", "");
            }
            else add(3, "Connection", kind == "LAN" ? "a cable - no Wi-Fi to check" : kind, "");
            // 2. a sign-in page in the way (captive portal): Windows' own test address
            run.Status = "Checking for a sign-in page...";
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://www.msftconnecttest.com/connecttest.txt"); req.Timeout = 5000; req.AllowAutoRedirect = false; req.Proxy = null; req.UserAgent = "AdeeboMeter";
                using (var resp = (HttpWebResponse)req.GetResponse()) using (var sr = new System.IO.StreamReader(resp.GetResponseStream()))
                {
                    string body = sr.ReadToEnd(); body = body.Length > 200 ? body.Substring(0, 200) : body;
                    if ((int)resp.StatusCode >= 300 && (int)resp.StatusCode < 400) add(1, "Sign-in page", "the network sends you to a sign-in page first (" + Draw.Trunc(resp.Headers["Location"] ?? "", 60) + ") - open a browser and accept it; until then apps can't get out", "");
                    else if (body.Trim() == "Microsoft Connect Test") add(0, "Sign-in page", "none in the way - you're through to the internet", "");
                    else add(1, "Sign-in page", "something answered in the internet's place - a sign-in page or a filter", "");
                }
            }
            catch (WebException e) { var hr = e.Response as HttpWebResponse; if (hr != null && (int)hr.StatusCode >= 300 && (int)hr.StatusCode < 400) add(1, "Sign-in page", "the network sends you to a sign-in page first - open a browser and accept it", ""); else add(1, "Sign-in page", "couldn't reach the internet's test address (" + Draw.Trunc(e.Message, 60) + ")", ""); }
            // 3. DNS that lies: a name that can't exist must not get an answer
            run.Status = "Checking DNS honesty...";
            IPAddress dsrv; if (IPAddress.TryParse((dns ?? "").Split(',')[0].Trim(), out dsrv))
            {
                string fake = "adm-" + Guid.NewGuid().ToString("N").Substring(0, 12) + ".example.com";
                var msg = Toolkit.Query(dsrv, fake, 1, true);
                if (msg == null) add(1, "DNS", "the network's DNS server didn't answer", "");
                else if (msg.Rcode == 3 || msg.Rrs.All(r => r.Section != 1)) add(0, "DNS", "honest - a name that doesn't exist gets \"no such name\"", "");
                else add(2, "DNS", "it answers for names that don't exist - this network rewrites DNS (ads, filters, or worse)", "Use your VPN, or set a DNS-over-HTTPS server in Windows' network settings.");
            }
            // 4. who else can reach you: the network profile and the neighbours already in the ARP table
            string cat = ProfileCategory(ssid);
            if (cat == "Private") add(2, "Windows treats it as", "a Private (home) network - other guests can find this PC and its shared folders", "Settings › Network › this network › set it to Public.");
            else if (cat == "Public") add(0, "Windows treats it as", "a Public network - this PC stays hidden from other guests", "");
            try { int nb = Arp.Table().Count(e => e.Dynamic && e.Ip != gw); if (nb > 0) add(nb > 5 ? 1 : 3, "Other devices seen", nb + " other device" + (nb == 1 ? "" : "s") + " on this network have talked to this PC" + (nb > 5 ? " - guests aren't kept apart here" : ""), ""); else add(0, "Other devices seen", "none - guests may be kept apart (client isolation)", ""); } catch (Exception e) { if (e is OutOfMemoryException) throw; }
            // 5. someone reading HTTPS on the way
            run.Status = "Checking for HTTPS inspection...";
            try
            {
                System.Security.Cryptography.X509Certificates.X509Certificate seen = null; var errs = System.Net.Security.SslPolicyErrors.None;
                using (var tc = new TcpClient())
                    if (tc.ConnectAsync("www.microsoft.com", 443).Wait(5000))
                        using (var ssl = new System.Net.Security.SslStream(tc.GetStream(), false, (s, c, ch2, e2) => { seen = c; errs = e2; return e2 == System.Net.Security.SslPolicyErrors.None; }))
                            try { ssl.AuthenticateAsClient("www.microsoft.com"); } catch (System.Security.Authentication.AuthenticationException) { }
                if (seen == null) add(3, "HTTPS", "couldn't check - no answer from microsoft.com", "");
                else if (errs != System.Net.Security.SslPolicyErrors.None) add(2, "HTTPS", "the certificate this network shows for microsoft.com isn't trusted - something on the way is pretending to be the site", "Don't sign in to anything here; use your phone's hotspot.");
                else { string by = Inspection.InspectedBy(new System.Security.Cryptography.X509Certificates.X509Certificate2(seen)); if (by != "") add(2, "HTTPS", "someone on this network opens your HTTPS (certificate from " + Draw.Trunc(by, 40) + ")", "Fine on your company's network; on a hotel or cafe network, stop and use your phone instead."); else add(0, "HTTPS", "not opened on the way - certificates come straight from the sites", ""); }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; add(3, "HTTPS", "couldn't check (" + Draw.Trunc(e.GetBaseException().Message, 60) + ")", ""); }
            if (vpn) add(0, "VPN", "on - everything above matters much less: your traffic is wrapped until it reaches the VPN", "");
            else if (bad > 0 || warn > 0) add(1, "VPN", "off - turn it on before you sign in to anything", "");
            rows.Insert(0, R(bad > 0 ? 2 : warn > 0 ? 1 : 0, "Verdict", bad > 0 ? (vpn ? "Risky network, but your VPN covers it" : "Don't trust this network - use a VPN or your phone's hotspot") : warn > 0 ? "Usable - mind the notes below" : "Looks fine to use", "Checks the Wi-Fi lock, sign-in pages, DNS honesty, who else can reach you and HTTPS inspection. Two requests go out: Windows' own internet test and one HTTPS connection to microsoft.com.", "i"));
            return rows;
        }
        // Windows' own label for the network (NetworkList profiles: 0 Public, 1 Private, 2 Domain) - read only
        static string ProfileCategory(string name)
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\NetworkList\Profiles"))
                    if (k != null) foreach (var s in k.GetSubKeyNames()) using (var p = k.OpenSubKey(s))
                            {
                                if (p == null || !string.Equals(Convert.ToString(p.GetValue("ProfileName", ""), CultureInfo.InvariantCulture), name, StringComparison.Ordinal)) continue;
                                int c = Convert.ToInt32(p.GetValue("Category", -1), CultureInfo.InvariantCulture); return c == 0 ? "Public" : c == 1 ? "Private" : c == 2 ? "Domain" : "";
                            }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("network profile: " + e.Message); }
            return "";
        }

        // ================================================================== DNS race: which server answers fastest, here
        public static List<string[]> DnsRace(Monitor m, string extra, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string dns, gw; lock (m.L) { dns = m.DnsServer; gw = m.Gateway; }
            var servers = new List<Tuple<string, IPAddress>>();
            foreach (var d in (dns ?? "").Split(',').Select(x => x.Trim()).Where(x => x != "").Take(3)) { IPAddress a; if (IPAddress.TryParse(d, out a) && !servers.Any(s => s.Item2.Equals(a))) servers.Add(Tuple.Create("Yours (" + d + ")", a)); }
            IPAddress ga; if (IPAddress.TryParse(gw ?? "", out ga) && !servers.Any(s => s.Item2.Equals(ga))) servers.Add(Tuple.Create("Your router (" + gw + ")", ga));
            foreach (var p in new[] { new[] { "Cloudflare", "1.1.1.1" }, new[] { "Google", "8.8.8.8" }, new[] { "Quad9", "9.9.9.9" } }) { var a = IPAddress.Parse(p[1]); if (!servers.Any(s => s.Item2.Equals(a))) servers.Add(Tuple.Create(p[0] + " (" + p[1] + ")", a)); }
            foreach (var x in (extra ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(3)) { IPAddress a; if (!IPAddress.TryParse(x, out a)) { rows.Add(R(2, "Extra server", "\"" + x + "\" isn't an IP address", "", "u")); return rows; } if (!servers.Any(s => s.Item2.Equals(a))) servers.Add(Tuple.Create(x, a)); }
            string[] names = { "www.google.com", "www.microsoft.com", "www.wikipedia.org", "outlook.office.com", "www.amazon.com", "www.youtube.com" };
            var res = new List<Tuple<string, double, double, int>>();   // name, typical cached, typical fresh, failures
            foreach (var s in servers)
            {
                if (run.Stop) break; run.Status = "Asking " + s.Item1 + "...";
                var cached = new List<double>(); var fresh = new List<double>(); int fail = 0;
                for (int round = 0; round < 3; round++) foreach (var n in names)
                    {
                        if (run.Stop) break; var q = Toolkit.Query(s.Item2, n, 1, true); if (q == null) fail++; else (round == 0 ? fresh : cached).Add(q.Ms);
                    }
                // a name nobody asked before: how fast it goes out and finds the answer itself
                for (int i = 0; i < 3 && !run.Stop; i++) { var q = Toolkit.Query(s.Item2, "r" + Guid.NewGuid().ToString("N").Substring(0, 10) + ".example.com", 1, true); if (q == null) fail++; else fresh.Add(q.Ms); }
                res.Add(Tuple.Create(s.Item1, cached.Count > 0 ? Journey.Median(cached) : double.NaN, fresh.Count > 0 ? Journey.Median(fresh) : double.NaN, fail));
            }
            var ok = res.Where(r => !double.IsNaN(r.Item2)).OrderBy(r => r.Item2 + r.Item3 * 0.3).ToList();
            if (ok.Count == 0) { rows.Add(R(2, "DNS race", "no server answered - DNS is blocked or the network is down", "", "m")); return rows; }
            var mine = res.FirstOrDefault(r => r.Item1.StartsWith("Yours", StringComparison.Ordinal)); var best = ok[0];
            bool mineBest = mine != null && (mine == best || (!double.IsNaN(mine.Item2) && mine.Item2 - best.Item2 < 5));
            rows.Add(R(mineBest ? 0 : 1, "Winner", best.Item1 + " - " + best.Item2.ToString("0", CultureInfo.CurrentCulture) + " ms for a name it knows, " + (double.IsNaN(best.Item3) ? "-" : best.Item3.ToString("0", CultureInfo.CurrentCulture)) + " ms for a new one", mineBest ? "Yours is as fast as any - nothing to change." : "Yours is slower here. Speed isn't everything: your provider's DNS may steer you to closer servers, and a company PC must keep its own DNS.", "m"));
            foreach (var r in res) rows.Add(R(r.Item4 > 3 ? 1 : 3, r.Item1, double.IsNaN(r.Item2) ? "no answer" : r.Item2.ToString("0", CultureInfo.CurrentCulture) + " ms known · " + (double.IsNaN(r.Item3) ? "-" : r.Item3.ToString("0", CultureInfo.CurrentCulture)) + " ms new" + (r.Item4 > 0 ? " · " + r.Item4 + " unanswered" : ""), "", "m"));
            rows.Add(R(3, "How", "each server was asked for 6 popular names three times (known = its cached answer) and for 3 made-up names (new = it had to go and find out). Typical (middle) times shown.", "", "i"));
            return rows;
        }

        // ================================================================== devices on this network, kept over time
        // From the ARP table AdeeboMeter already reads every ten minutes: first and last seen, the address it had, a label
        // you can give it. Nothing is scanned for it.
        sealed class Inv { public string Net = "", Mac = "", Ip = "", Label = ""; public DateTime First, Last; }
        static List<Inv> inv; static readonly object ik = new object();
        static void LoadInv()
        {
            if (inv != null) return; inv = new List<Inv>();
            foreach (var r in Store.Read("inventory.txt")) { if (r.Length < 6) continue; DateTime f, l; DateTime.TryParse(r[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out f); DateTime.TryParse(r[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out l); inv.Add(new Inv { Net = r[0], Mac = r[1], First = f, Last = l, Ip = r[4], Label = r[5] }); }
        }
        static void SaveInv() { Store.Write("inventory.txt", inv.Select(i => new[] { i.Net, i.Mac, i.First.ToString("o", CultureInfo.InvariantCulture), i.Last.ToString("o", CultureInfo.InvariantCulture), i.Ip, i.Label })); }
        public static void SeeDevices(string net, List<Arp.Entry> table, string gw)
        {
            if (net == "" || net == "offline") return;
            lock (ik)
            {
                LoadInv(); var now = DateTime.Now;
                foreach (var e in table.Where(x => x.Dynamic && x.Mac != ""))
                {
                    var i = inv.FirstOrDefault(x => x.Net == net && x.Mac == e.Mac);
                    if (i == null) { if (inv.Count(x => x.Net == net) >= 500) continue; inv.Add(i = new Inv { Net = net, Mac = e.Mac, First = now }); }
                    i.Last = now; i.Ip = e.Ip;
                }
                SaveInv();
            }
        }
        public static List<string[]> Inventory(Monitor m, string label)
        {
            var rows = new List<string[]>(); string net = Brain.NetKey, gw; lock (m.L) gw = m.Gateway;
            if (net == "" || net == "offline") { rows.Add(R(2, "Network", "not connected", "", "u")); return rows; }
            List<Arp.Entry> now; try { now = Arp.Table(); } catch (Exception e) { if (e is OutOfMemoryException) throw; now = new List<Arp.Entry>(); }
            SeeDevices(net, now, gw);
            lock (ik)
            {
                string l = (label ?? "").Trim();
                if (l != "")
                {
                    var mm = Regex.Match(l, @"^([0-9a-fA-F]{2}([:-][0-9a-fA-F]{2}){5}|\d+\.\d+\.\d+\.\d+)\s*=\s*(.{0,40})$");
                    if (!mm.Success) { rows.Add(R(2, "Label", "type it as  aa:bb:cc:dd:ee:ff = Living room TV  or  192.168.1.20 = Printer  (empty after = removes the label)", "", "u")); return rows; }
                    string key = mm.Groups[1].Value.ToLowerInvariant().Replace('-', ':'); var it = inv.FirstOrDefault(x => x.Net == net && (string.Equals(x.Mac.Replace('-', ':'), key, StringComparison.OrdinalIgnoreCase) || x.Ip == key));
                    if (it == null) { rows.Add(R(2, "Label", key + " isn't in the list for this network", "", "u")); return rows; }
                    it.Label = Program.Plain(mm.Groups[3].Value.Trim()); SaveInv();
                    rows.Add(R(0, "Label", it.Label == "" ? "removed from " + key : key + " is now \"" + it.Label + "\"", "", "i"));
                }
                var here = new HashSet<string>(now.Where(e => e.Dynamic).Select(e => e.Mac));
                var mine = inv.Where(x => x.Net == net).OrderByDescending(x => here.Contains(x.Mac)).ThenByDescending(x => x.Last).ToList();
                rows.Insert(0, R(3, Brain.NetLabel, mine.Count + " devices seen here · " + mine.Count(x => here.Contains(x.Mac)) + " here now", "From the ARP table AdeeboMeter reads every ten minutes - devices that talked to this PC or the router recently. Find devices or a Ping sweep fills it in.", "i"));
                foreach (var d in mine.Take(200))
                {
                    string maker = Toolkit.Vendor(d.Mac); bool now2 = here.Contains(d.Mac);
                    rows.Add(R(now2 ? 0 : 3, d.Label != "" ? d.Label : d.Ip == gw ? "Your router" : maker != "" ? maker.Trim('(', ')') : d.Ip, d.Ip + "  ·  " + d.Mac + "  ·  " + (now2 ? "here now" : "last seen " + DevInfo.When(d.Last)) + "  ·  first seen " + DevInfo.When(d.First), "", "i"));
                }
            }
            return rows;
        }

        // ================================================================== drivers and USB history
        public static List<string[]> Drivers()
        {
            var rows = new List<string[]>(); string err;
            var all = DevInfo.List("", true, out err);
            if (all.Count == 0) { rows.Add(R(2, "Devices", err != "" ? err : "Windows listed no devices", "", "u")); return rows; }
            var probs = all.Where(d => d.Problem != 0 && d.Problem != 45).ToList();
            rows.Add(R(probs.Count > 0 ? 2 : 0, "Devices with a problem", probs.Count == 0 ? "none - every device Windows sees is working" : probs.Count + " - listed first below", "", "w"));
            foreach (var d in probs.Take(20)) { var p = DevInfo.Problem(d.Problem); rows.Add(R(2, Draw.Trunc(d.Name != "" ? d.Name : d.Id, 50), p[0] + " (code " + d.Problem + ")", p[1], "w")); }
            foreach (var cls in new[] { new[] { "Net", "Network adapters" }, new[] { "Bluetooth", "Bluetooth" }, new[] { "USB", "USB controllers and hubs" } })
            {
                var l = all.Where(d => string.Equals(d.Class, cls[0], StringComparison.OrdinalIgnoreCase) && d.DriverVer != "" && d.Name.IndexOf("WAN Miniport", StringComparison.OrdinalIgnoreCase) < 0 && d.Name.IndexOf("Kernel Debug", StringComparison.OrdinalIgnoreCase) < 0)
                           .GroupBy(d => d.Name).Select(gr => gr.First()).OrderBy(d => d.Name).Take(15).ToList();
                if (l.Count == 0) continue;
                rows.Add(R(3, cls[1].ToUpperInvariant(), "", "", "w"));
                foreach (var d in l)
                {
                    double years = d.DriverDate == DateTime.MinValue ? 0 : (DateTime.Now - d.DriverDate).TotalDays / 365.25; bool old = cls[0] == "Net" && years >= 3 && d.Provider.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) < 0;
                    rows.Add(R(old ? 1 : 3, Draw.Trunc(d.Name, 50), d.DriverVer + (d.DriverDate != DateTime.MinValue ? " · " + d.DriverDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + " (" + DevInfo.Age(d.DriverDate) + ")" : "") + (d.Provider != "" ? " · " + d.Provider : ""), old ? "An old network driver is a common cause of drops and slow Wi-Fi - the maker's site (or the PC maker's support page) usually has a newer one." : "", "w"));
                }
            }
            // USB history: every USB device Windows has seen, by when it last came or went
            var usb = DevInfo.List("USB", false, out err).Concat(DevInfo.List("USBSTOR", false, out err)).Where(d => d.Name != "" && d.Name.IndexOf("Root Hub", StringComparison.OrdinalIgnoreCase) < 0 && d.Name.IndexOf("Composite Device", StringComparison.OrdinalIgnoreCase) < 0 && d.Name.IndexOf("Host Controller", StringComparison.OrdinalIgnoreCase) < 0)
                         .OrderByDescending(d => d.Arrived > d.Removed ? d.Arrived : d.Removed).Take(25).ToList();
            if (usb.Count > 0)
            {
                rows.Add(R(3, "USB HISTORY", "the devices Windows has seen, most recent first", "", "w"));
                foreach (var d in usb) rows.Add(R(d.Problem != 0 && d.Problem != 45 ? 2 : 3, Draw.Trunc(d.Name, 50), (d.Present ? "plugged in" : "not plugged in") + " · arrived " + DevInfo.When(d.Arrived) + (d.Removed != DateTime.MinValue ? " · removed " + DevInfo.When(d.Removed) : ""), "", "w"));
            }
            return rows;
        }

        // ================================================================== the internet weather: one sentence for the Overview
        public static string Weather(Monitor m)
        {
            double net = m.NetPing.Avg(10); var v = m.NetPing.Values(30).Where(x => !double.IsNaN(x)).ToList(); double jit = v.Count > 2 ? v.Zip(v.Skip(1), (a, b) => Math.Abs(a - b)).Average() : double.NaN;
            var today = Outages.Last(m, 1).Where(o => o.From.Date == DateTime.Today).ToList(); var now = today.FirstOrDefault(o => o.Ongoing);
            if (now != null) return "Stormy: the internet has been out since " + now.From.ToString("HH:mm", CultureInfo.CurrentCulture) + ".";
            if (double.IsNaN(net)) return "";
            string drops = today.Count == 0 ? "no drops today" : today.Count + (today.Count == 1 ? " drop" : " drops") + " today (last at " + today[0].From.ToString("HH:mm", CultureInfo.CurrentCulture) + ")";
            string word = today.Count >= 3 || net > 150 || jit > 40 ? "Stormy" : today.Count > 0 || net > 60 || jit > 15 ? "Choppy" : "Calm";
            return word + ": " + net.ToString("0", CultureInfo.CurrentCulture) + " ms to the internet" + (double.IsNaN(jit) ? "" : ", wobbling " + jit.ToString("0", CultureInfo.CurrentCulture) + " ms") + ", " + drops + ".";
        }
    }
}
