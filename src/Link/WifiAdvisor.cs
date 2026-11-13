// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Wi-Fi advisor, Wi-Fi health with location off, look-alike networks
    // Runs only while Your Wi-Fi is open, on what LinkInfo already collected plus the pings the Monitor already sends.
    static class WifiAdvisor
    {
        public sealed class Extra
        {
            public List<string[]> Advice = new List<string[]>();     // { level, text }
            public List<string[]> Health = new List<string[]>();     // rows for the "Wi-Fi health" card (location off or on)
            public int Score = -1; public string ScoreText = "";
            public List<string[]> Twins = new List<string[]>();      // { ssid, bssid, why }
        }
        static readonly string[] LocationOff = { "3", "Windows hides the Wi-Fi name, signal and nearby networks because location is off for desktop apps (on a work PC, IT usually sets this). AdeeboMeter judges your Wi-Fi from what Windows still shows: the link to your router, link speed, adapter errors and Windows' drop log." };
        static readonly int[] Ch24 = { 1, 6, 11 };
        static long lastErr = -1; static DateTime lastErrAt;
        static string power = ""; static DateTime powerAt = DateTime.MinValue;

        public static Extra Fill(LinkSnap s, Monitor m)
        {
            var x = new Extra();
            if (!s.Wifi) return x;
            string ssid = s.Radio.Where(r => r[0] == "Network (SSID)").Select(r => r[1]).FirstOrDefault() ?? "-";
            int sig = -1; lock (m.L) sig = m.WifiSignal;
            NetworkInterface nic; lock (m.L) nic = m.Primary;

            // ---- health from what Windows never hides: the link to the router, link speed, adapter errors, drops
            double[] gw; lock (m.L) gw = m.GwPing.Values(30);
            var ok = gw.Where(v => !double.IsNaN(v)).ToList();
            double avg = ok.Count > 0 ? ok.Average() : double.NaN, jit = ok.Count > 2 ? ok.Zip(ok.Skip(1), (a, b) => Math.Abs(a - b)).Average() : double.NaN;
            double loss = gw.Length > 0 ? 100.0 * gw.Count(double.IsNaN) / gw.Length : double.NaN;
            long speed = 0; long errs = 0;
            try { if (nic != null) { speed = nic.Speed; var st = nic.GetIPStatistics(); errs = st.IncomingPacketsWithErrors + st.IncomingPacketsDiscarded + st.OutgoingPacketsWithErrors; } } catch { }
            double errPerMin = double.NaN;
            if (lastErr >= 0 && (DateTime.UtcNow - lastErrAt).TotalSeconds > 5) errPerMin = errs < lastErr ? double.NaN /* the adapter was reset: its counters started again */ : (errs - lastErr) / (DateTime.UtcNow - lastErrAt).TotalMinutes;
            if (lastErr < 0 || (DateTime.UtcNow - lastErrAt).TotalSeconds > 5) { lastErr = errs; lastErrAt = DateTime.UtcNow; }
            int score = 100;
            if (!double.IsNaN(avg)) { if (avg > 50) score -= 40; else if (avg > 20) score -= 20; }
            if (!double.IsNaN(jit) && jit > 10) score -= 15;
            if (!double.IsNaN(loss) && loss > 1) score -= loss > 5 ? 30 : 15;
            if (speed > 0 && speed < 50000000) score -= 15;
            if (!double.IsNaN(errPerMin) && errPerMin > 10) score -= 10;
            if (s.Drops.Count >= 3) score -= 20; else if (s.Drops.Count > 0) score -= 5;
            if (sig >= 0 && sig < 40) score -= 15;
            x.Score = Math.Max(0, score);
            x.ScoreText = x.Score >= 80 ? "Healthy - calls and video will be fine." : x.Score >= 55 ? "Usable, with rough patches - fine for browsing, calls may stutter." : "Struggling - expect stutters and drops.";
            x.Health.Add(new[] { "Router answers in", double.IsNaN(avg) ? "-" : avg.ToString("0", CultureInfo.CurrentCulture) + " ms", double.IsNaN(avg) ? "0" : avg > 50 ? "2" : avg > 20 ? "1" : "0" });
            x.Health.Add(new[] { "Jitter (how much it wobbles)", double.IsNaN(jit) ? "-" : jit.ToString("0", CultureInfo.CurrentCulture) + " ms", double.IsNaN(jit) ? "0" : jit > 30 ? "2" : jit > 10 ? "1" : "0" });
            x.Health.Add(new[] { "Lost pings to the router", double.IsNaN(loss) ? "-" : loss.ToString("0.#", CultureInfo.CurrentCulture) + " %", double.IsNaN(loss) ? "0" : loss > 5 ? "2" : loss > 1 ? "1" : "0" });
            x.Health.Add(new[] { "Link speed (falls as signal weakens)", speed > 0 ? (speed / 1e6).ToString("0", CultureInfo.CurrentCulture) + " Mb/s" : "-", speed > 0 && speed < 50000000 ? "1" : "0" });
            x.Health.Add(new[] { "Adapter errors", double.IsNaN(errPerMin) ? "measuring..." : errPerMin.ToString("0", CultureInfo.CurrentCulture) + " a minute", !double.IsNaN(errPerMin) && errPerMin > 10 ? "1" : "0" });
            x.Health.Add(new[] { "Drops in the last 24 hours", s.Drops.Count.ToString(CultureInfo.CurrentCulture), s.Drops.Count >= 3 ? "2" : s.Drops.Count > 0 ? "1" : "0" });

            // ---- Windows' Wi-Fi power saving (a classic cause of laptop drops) - powercfg is read-only and needs no admin
            if ((DateTime.UtcNow - powerAt).TotalMinutes > 10)
            {
                powerAt = DateTime.UtcNow;
                try { power = Job.Run(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "powercfg.exe"), "/q SCHEME_CURRENT 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a", 4000); } catch { power = ""; }
            }
            var hex = Regex.Matches(power, @"0x0000000([0-3])\s*$", RegexOptions.Multiline).Cast<Match>().Select(mt => mt.Groups[1].Value).ToList();
            if (hex.Count >= 2)
            {
                bool battery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
                string v = battery ? hex[hex.Count - 1] : hex[hex.Count - 2];
                if (v == "3" || v == "2") x.Advice.Add(new[] { "1", "Windows' Wi-Fi power saving is set to " + (v == "3" ? "maximum" : "medium") + (battery ? " on battery" : " when plugged in") + ". It often causes drops and lag on laptops. Fix: Control Panel → Power Options → Change plan settings → Advanced → Wireless Adapter Settings → Power Saving Mode → Maximum Performance." });
            }

            if (s.LocationBlocked)
            {
                x.Advice.Insert(0, LocationOff);
                return x;
            }

            // ---- channel advice from the neighbour scan
            var aps = s.Aps; string band = s.MyBand ?? ""; int my = s.MyChannel;
            if (my > 0 && aps.Count > 0)
            {
                if (band.StartsWith("2.4", StringComparison.Ordinal))
                {
                    Func<int, double> load = c => aps.Where(a => a.Band == band && a.Channel > 0 && !string.Equals(a.Bssid, s.MyBssid, StringComparison.OrdinalIgnoreCase) && LinkInfo.Overlaps(a.Channel, c, band)).Sum(a => Math.Max(5, a.Signal));
                    var best = Ch24.OrderBy(load).First();
                    if (best != my && load(best) < load(my) * 0.5) x.Advice.Add(new[] { "1", "Your router is on 2.4 GHz channel " + my + ", which you share with busy neighbours. Channel " + best + " is much quieter - change it in your router's Wi-Fi settings (only 1, 6 or 11 avoid overlap)." });
                }
                else if (band.StartsWith("5", StringComparison.Ordinal))
                {
                    // the same candidates as Least crowded channel: 36-48 everywhere, upper and radar channels only when they're in use around here
                    var lc = WlanApi.LeastCrowded(aps, band, my, s.MyBssid);
                    Func<int, double> load = c => aps.Where(a => a.Band == band && a.Channel == c && !string.Equals(a.Bssid, s.MyBssid, StringComparison.OrdinalIgnoreCase)).Sum(a => Math.Max(5, a.Signal));
                    var best = lc != null ? lc[0] : 36;
                    if (best != my && load(my) > 60 && load(best) < load(my) * 0.4) x.Advice.Add(new[] { "1", "Neighbours crowd 5 GHz channel " + my + ". Channel " + best + " is " + (load(best) == 0 ? "empty" : "much quieter") + " here - set it in your router." });
                }
                // the same network on a better band
                if (band.StartsWith("2.4", StringComparison.Ordinal))
                {
                    var five = aps.Where(a => a.Ssid == ssid && a.Band != "" && !a.Band.StartsWith("2.4", StringComparison.Ordinal) && a.Signal >= 40).OrderByDescending(a => a.Signal).FirstOrDefault();
                    if (five != null) x.Advice.Add(new[] { "1", "You're on 2.4 GHz, but \"" + ssid + "\" is also on " + five.Band + " here at " + five.Signal + "% - faster and less crowded. Forget and rejoin the network, or give the 5 GHz band its own name in the router." });
                }
                // sticky client: clinging to a far access point while a much stronger one of the same network is near
                if (sig >= 0 && sig < 45)
                {
                    var near = aps.Where(a => a.Ssid == ssid && !string.Equals(a.Bssid, s.MyBssid, StringComparison.OrdinalIgnoreCase) && a.Signal >= sig + 25).OrderByDescending(a => a.Signal).FirstOrDefault();
                    if (near != null) x.Advice.Add(new[] { "1", "Your PC clings to a far access point (" + sig + "%) while another one of \"" + ssid + "\" is at " + near.Signal + "% nearby. Turning Wi-Fi off and on makes it pick the closer one; in the adapter's Advanced settings, a higher \"Roaming aggressiveness\" makes it switch by itself." });
                }
                // look-alike networks: same name, different maker and not a randomised address - an "evil twin" sign (or a neighbour's
                // same-named router). Not when your own access point uses a randomised (locally administered) address, not when both
                // makers are known and the same, and not on a big managed network (3+ of your maker's APs: batches differ there)
                string myOui = Oui(s.MyBssid);
                int mine = aps.Count(a => a.Ssid == ssid && Oui(a.Bssid) == myOui);
                if (myOui != "" && ssid != "-" && !Arp.Randomised(s.MyBssid) && mine < 3)
                    foreach (var a in aps.Where(a => a.Ssid == ssid && Oui(a.Bssid) != "" && Oui(a.Bssid) != myOui && !Arp.Randomised(a.Bssid)))
                    {
                        string v1 = Toolkit.Vendor(s.MyBssid), v2 = Toolkit.Vendor(a.Bssid); if (v1 != "" && v1 == v2) continue;
                        x.Twins.Add(new[] { a.Ssid, a.Bssid, "Same name as your network, but different hardware (" + a.Signal + "%" + (v2 != "" ? ", " + v2.Trim('(', ')') : "") + "). Usually another access point bought separately or a neighbour's router with the same name - but it could be a fake one. If it shows a sign-in page you didn't expect, don't sign in." });
                    }
            }
            if (x.Advice.Count == 0) x.Advice.Add(new[] { "0", Voice.P("Nothing to tweak - your Wi-Fi is set up well.", "No changes suggested.") });
            return x;
        }
        static string Oui(string bssid) { return string.IsNullOrEmpty(bssid) || bssid.Length < 8 ? "" : bssid.Substring(0, 8).ToLowerInvariant(); }
    }
}
