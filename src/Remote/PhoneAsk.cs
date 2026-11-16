// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace AdeeboMeter
{
    // The other direction of the phone link (Adeeb, 16 Nov: "connect to your phone on PC ... and vice versa"): the PC asks
    // the paired phone to run one of its own checks and shows the answer next to its own. Same encrypted session as the
    // phone remote; the relay only passes it along. A phone's browser can't work in the background, so the phone has to
    // have AdeeboMeter open - when it doesn't answer, the rows say so.
    //
    //   PC ── {t:"ask", rid, k, q} ──► phone runs it (banner: "Your PC asked...") ── {t:"pres", rid, head, r} ──► PC
    static class PhoneAsk
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        public sealed class Answer { public bool NoPhone, NoAnswer, Stopped; public string Head = ""; public List<string[]> Rows = new List<string[]>(); }

        // one question to the phone; waits up to 45 s (the phone may be measuring), stops with the check
        public static Answer Ask(string k, string q, Toolkit.Run run)
        {
            var a = new Answer(); var s = Remote.Now;
            if (s == null || s.St != Remote.State.Paired) { a.NoPhone = true; return a; }
            var got = new ManualResetEventSlim(false); string head = null; List<string[]> rows = null;
            if (run != null) run.Status = "Asking your phone...";
            if (!s.AskPhone(k, q, (h, r) => { head = h; rows = r; got.Set(); })) { a.NoPhone = true; return a; }
            var until = DateTime.UtcNow.AddSeconds(45);
            while (!got.Wait(250))
            {
                if (run != null && run.Stop) { a.Stopped = true; return a; }
                if (DateTime.UtcNow > until || s.St != Remote.State.Paired) { a.NoAnswer = true; return a; }
            }
            if (rows == null) { a.NoAnswer = true; return a; }
            a.Head = head ?? ""; a.Rows = rows; return a;
        }
        static List<string[]> Missing(Answer a)
        {
            if (a.Stopped) return new List<string[]> { R(3, "Stopped", "before your phone answered", "", "i") };
            if (a.NoPhone) return new List<string[]> { R(1, "Your phone", "not connected", "Right-click AdeeboMeter in the tray > Phone remote..., scan the code with your phone (or open a remembered phone), then run this again. Keep AdeeboMeter open on the phone while it runs.", "i") };
            return new List<string[]> { R(1, "Your phone", "didn't answer", "A phone's browser can't work in the background - keep AdeeboMeter open and the screen on while this runs, then try again.", "i") };
        }

        // the same small measurement the phone makes (7 HTTPS requests to the same server, the first one left out), so the
        // two numbers can be compared fairly; IT's "no online lookups" switch keeps it to the PC's own ping
        static double[] Https(Toolkit.Run run)
        {
            var t = new List<double>(); int fail = 0;
            for (int i = 0; i < 7; i++)
            {
                if (run != null && run.Stop) break;
                var sw = Stopwatch.StartNew();
                try { HttpSmall.Get("https://adeebometer-live.orkoadeeb.workers.dev/ping?pc" + i + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture), 4); if (i > 0) t.Add(sw.Elapsed.TotalMilliseconds); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; if (i > 0) fail++; }
            }
            if (t.Count == 0) return new[] { double.NaN, double.NaN, fail };
            t.Sort(); return new[] { t[t.Count / 2], t[t.Count - 1] - t[0], fail };
        }
        static double Num(List<string[]> rows, string name)
        {
            var r = rows.FirstOrDefault(x => x[1] == name); if (r == null) return double.NaN;
            var m = System.Text.RegularExpressions.Regex.Match(r[2] ?? "", @"^(\d+(?:\.\d+)?)\s*ms"); double v;
            return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN;
        }
        static string Ms(double v) { return double.IsNaN(v) ? "-" : Math.Round(v).ToString(CultureInfo.InvariantCulture) + " ms"; }

        // ---------- This PC vs my phone: is it this PC, the home network, or the provider?
        public static List<string[]> Vs(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>();
            if (run != null) run.Status = "Measuring this PC...";
            double[] pc = Policy.NoOnline ? null : Https(run);
            var a = Ask("check", "", run);
            if (a.NoPhone || a.NoAnswer || a.Stopped) { rows.AddRange(Missing(a)); if (pc != null) rows.Add(R(3, "This PC", Ms(pc[0]) + " to Cloudflare (7 web requests)", "", "i")); return rows; }
            double ph = Num(a.Rows, "Delay to the internet"), pj = Num(a.Rows, "Request-time spread");
            bool cell = (a.Head ?? "").IndexOf("mobile data", StringComparison.OrdinalIgnoreCase) >= 0, phoneDead = a.Rows.Any(r => r[1] == "Connection" && r[0] == "2");
            double p = pc == null ? double.NaN : pc[0]; bool pcDead = pc != null && double.IsNaN(p);
            string verdict, adv;
            if (pcDead && phoneDead) { verdict = "Neither reaches the internet"; adv = cell ? "Even mobile data fails - likely a wider outage, or the phone has no signal." : "Both are on the same network: the router or the provider. Restart the router; if it stays down, check the provider's outage page."; }
            else if (pcDead) { verdict = "Your phone is online, this PC isn't"; adv = cell ? "The phone is on mobile data, so the home internet may be down - or just this PC. Turn the phone's Wi-Fi on and run this again to tell which." : "Same network, so it's this PC: its Wi-Fi or cable, a VPN, a firewall or proxy. Try the Full checkup."; }
            else if (phoneDead) { verdict = "This PC is online, your phone isn't"; adv = "It's the phone: its Wi-Fi, mobile data or a blocker/VPN app on it."; }
            else if (!double.IsNaN(p) && !double.IsNaN(ph) && p > ph + 40) { verdict = "This PC is slower than your phone"; adv = cell ? "Your phone on mobile data is faster than the home connection right now - the home line or Wi-Fi is the slow part." : "Same network, so the slow part is on this PC: Wi-Fi signal, a busy upload, a VPN or a program using the line."; }
            else if (!double.IsNaN(p) && !double.IsNaN(ph) && ph > p + 40) { verdict = "Your phone is slower than this PC"; adv = cell ? "Mobile data is slower here than your home internet - normal indoors." : "Same network, so it's the phone's Wi-Fi: distance, walls or the 2.4 GHz band."; }
            else { verdict = "About the same"; adv = cell ? "Your home internet and mobile data look alike right now." : "Both see the same network the same way - if something felt slow, it's more likely the app or website."; }
            rows.Add(R(pcDead || phoneDead ? 2 : verdict == "About the same" ? 0 : 1, "Verdict", verdict, adv, "i"));
            rows.Add(R(3, "This PC", pc == null ? "ping " + Ms(m.Latest.Ping) + " (IT turned off online lookups)" : pcDead ? "no answer" : Ms(p) + " to Cloudflare, spread " + Ms(pc[1]), "7 small web requests, like the phone's.", "i"));
            rows.Add(R(3, "Your phone", phoneDead ? "no answer" : Ms(ph) + " to Cloudflare, spread " + Ms(pj), a.Head, "i"));
            foreach (var r in a.Rows) rows.Add(R(int.Parse(r[0], CultureInfo.InvariantCulture), "Phone - " + r[1], r[2], r[3], "i"));
            return rows;
        }

        // ---------- Is it down - from this PC and from my phone
        public static List<string[]> Down(string site, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string h = (site ?? "").Trim();
            if (h == "") { rows.Add(R(2, "Website", "type one, e.g. netflix.com", "", "u")); return rows; }
            if (run != null) run.Status = "Trying " + h + " from this PC...";
            bool pcUp = false; double ms = double.NaN; string pcWhy = "";
            if (Policy.NoOnline) pcWhy = "IT turned off online lookups on this PC";
            else
            {
                var sw = Stopwatch.StartNew();
                try { HttpSmall.Get((h.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "" : "https://") + h, 64); pcUp = true; ms = sw.Elapsed.TotalMilliseconds; }
                catch (System.Net.WebException e) { var hr = e.Response as System.Net.HttpWebResponse; if (hr != null) { pcUp = true; ms = sw.Elapsed.TotalMilliseconds; pcWhy = "answered " + (int)hr.StatusCode; } else pcWhy = e.Status.ToString(); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; pcUp = e is System.IO.InvalidDataException; ms = sw.Elapsed.TotalMilliseconds; pcWhy = pcUp ? "" : e.GetType().Name; }
            }
            var a = Ask("isdown", h, run);
            if (a.NoPhone || a.NoAnswer || a.Stopped) { rows.AddRange(Missing(a)); rows.Add(R(pcUp ? 0 : 2, "This PC", pcUp ? "reached " + h + " in " + Ms(ms) : "couldn't reach " + h + (pcWhy != "" ? " (" + pcWhy + ")" : ""), "", "i")); return rows; }
            bool phUp = a.Head.StartsWith("Up", StringComparison.OrdinalIgnoreCase);
            string v = pcUp && phUp ? "Up from both" : !pcUp && !phUp ? "Down from both" : pcUp ? "Only your phone can't reach it" : "Only this PC can't reach it";
            string adv = pcUp && phUp ? "If it still won't work, it's the app, the page or your account - not the network." : !pcUp && !phUp ? "Likely down for everyone, or blocked on this network (if the phone is on the same Wi-Fi). Try the phone on mobile data." : pcUp ? "It's the phone - its connection, or a blocker/VPN app." : "It's this PC: a proxy, VPN, firewall, the hosts file or its DNS. Try Hosts file and Which proxy does this URL use?";
            rows.Add(R(pcUp && phUp ? 0 : 2, "Verdict", v, adv, "i"));
            rows.Add(R(pcUp ? 0 : 2, "This PC", pcUp ? "reached it in " + Ms(ms) + (pcWhy != "" ? " (" + pcWhy + ")" : "") : "couldn't reach it" + (pcWhy != "" ? " (" + pcWhy + ")" : ""), "", "i"));
            rows.Add(R(phUp ? 0 : 2, "Your phone", a.Head, "", "i"));
            foreach (var r in a.Rows) rows.Add(R(int.Parse(r[0], CultureInfo.InvariantCulture), "Phone - " + r[1], r[2], r[3], "i"));
            return rows;
        }

        // ---------- My phone's week (kept on the phone; it comes over only when asked, and isn't saved here)
        public static List<string[]> Week(Toolkit.Run run)
        {
            var a = Ask("week", "", run);
            if (a.NoPhone || a.NoAnswer || a.Stopped) return Missing(a);
            var rows = new List<string[]> { R(3, "Your phone's week", a.Head, "From the checks run on the phone, kept on the phone only. Shown here, not saved.", "i") };
            foreach (var r in a.Rows) rows.Add(R(int.Parse(r[0], CultureInfo.InvariantCulture), r[1], r[2], r[3], "i"));
            return rows;
        }
    }
}
