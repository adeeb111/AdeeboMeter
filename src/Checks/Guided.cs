// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // Plain-language questions, each a short guided check that ends in a verdict, its evidence and one next step:
    //   Why isn't my USB device working?   ·   Why is my game lagging?
    // Read-only: nothing is changed, installed or switched off. The same pattern fits later questions.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "result rows, built once per run")]
    static class Guided
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }

        // ================================================================== Why isn't my USB device working?
        [StructLayout(LayoutKind.Sequential)] struct PowerStatus { public byte Ac, Battery, Percent, SystemStatus; public int Life, Full; }
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool GetSystemPowerStatus(out PowerStatus s);

        public static List<string[]> Usb(string which)
        {
            var rows = new List<string[]>(); string q = (which ?? "").Trim(), err;
            var all = DevInfo.List("USB", false, out err).Concat(DevInfo.List("USBSTOR", false, out err)).Concat(DevInfo.List("HID", false, out err).Where(d => d.Present && d.Problem != 0))
                             .Where(d => d.Name != "" && d.Name.IndexOf("Root Hub", StringComparison.OrdinalIgnoreCase) < 0 && d.Name.IndexOf("Host Controller", StringComparison.OrdinalIgnoreCase) < 0)
                             .GroupBy(d => d.Id).Select(gr => gr.First()).ToList();
            if (all.Count == 0) { rows.Add(R(2, "USB devices", err != "" ? err : "Windows lists no USB devices", "", "u")); return rows; }
            var mine = q == "" ? all : all.Where(d => d.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || d.Class.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (q != "" && mine.Count == 0) { rows.Add(R(1, "\"" + q + "\"", "Windows has never seen a USB device by that name - try a shorter word (e.g. headset, mouse, disk), or leave it empty to check them all", "If it was never recognised at all, it may be the cable (charge-only cables carry no data) or the port.", "w")); return rows; }
            int bad = 0, warn = 0; string next = "";
            // 1. problem codes on devices plugged in now
            var probs = mine.Where(d => d.Present && d.Problem != 0).ToList();
            foreach (var d in probs.Take(10)) { var p = DevInfo.Problem(d.Problem); rows.Add(R(2, Draw.Trunc(d.Name, 50), p[0] + " (Windows code " + d.Problem + ")", p[1], "w")); bad++; if (next == "") next = p[1]; }
            // 2. what came and went recently: a device that keeps dropping points at the cable, the port or power
            var recent = mine.Where(d => d.Removed != DateTime.MinValue && (DateTime.Now - d.Removed).TotalHours < 24).OrderByDescending(d => d.Removed).ToList();
            // Windows keeps the last arrival and removal per device; a removal followed by an arrival seconds later, while
            // you didn't touch it, is a drop (cable, port or power) - only the last one is known, never a count
            foreach (var d in recent.Take(8))
            {
                bool back = d.Present && d.Arrived > d.Removed, quick = back && (d.Arrived - d.Removed).TotalSeconds < 30;
                int lv = quick ? 1 : 3; if (lv == 1) warn++;
                rows.Add(R(lv, Draw.Trunc(d.Name, 50), back ? (quick ? "dropped at " + DevInfo.When(d.Removed) + " and came back seconds later" : "unplugged at " + DevInfo.When(d.Removed) + ", back at " + DevInfo.When(d.Arrived)) : "unplugged or lost at " + DevInfo.When(d.Removed) + (d.Present ? "" : " - not back since"), quick ? "If you didn't unplug it then, it dropped out by itself: a loose or long cable, a worn port, or not enough power (a hub without its own power supply, or a front-panel port). Try a port on the back of the PC, straight in." : "", "w"));
                if (quick && next == "") next = "Try another port (a back port, not a hub) and another cable.";
            }
            // 3. power: selective suspend, battery saver
            string ss = SelectiveSuspend();
            if (ss == "on") { rows.Add(R(1, "USB power saving", "on - Windows may put idle USB devices to sleep (a known cause of mice, headsets and adapters dropping out)", "Control Panel › Power Options › Change plan settings › Advanced › USB settings › USB selective suspend › Disabled (you change it; AdeeboMeter doesn't).", "w")); warn++; if (next == "") next = "Turn off USB selective suspend and see if it stops."; }
            else if (ss == "off") rows.Add(R(0, "USB power saving", "off - Windows won't put USB devices to sleep", "", "w"));
            PowerStatus ps; if (GetSystemPowerStatus(out ps) && ps.SystemStatus == 1) { rows.Add(R(1, "Battery saver", "on - it cuts power to some devices", "Plug the laptop in, or turn battery saver off, and try again.", "w")); warn++; }
            // 4. a USB network adapter or a phone's tethering: is the link actually up?
            foreach (var d in mine.Where(d => d.Present && string.Equals(d.Class, "Net", StringComparison.OrdinalIgnoreCase)).Take(4))
            {
                NetworkInterface nic = null; try { nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => string.Equals(n.Description, d.Name, StringComparison.OrdinalIgnoreCase)); } catch (NetworkInformationException) { }
                if (nic == null) { rows.Add(R(1, Draw.Trunc(d.Name, 50), "a USB network adapter Windows sees, but no network connection uses it", "Unplug and replug it; if it's a phone, turn USB tethering off and on.", "w")); warn++; continue; }
                var ip = nic.GetIPProperties().UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                bool up = nic.OperationalStatus == OperationalStatus.Up, apipa = ip != null && ip.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal);
                int lv = !up || ip == null || apipa ? 2 : 0; if (lv == 2) bad++;
                rows.Add(R(lv, Draw.Trunc(d.Name, 50), !up ? "network link is down" : ip == null ? "link up, but no address" : apipa ? "link up, but no address from the network (" + ip.Address + " - DHCP didn't answer)" : "working - address " + ip.Address, lv == 2 ? "For a phone: unlock it and allow the PC. For an adapter: check the cable to the router." : "", "w"));
            }
            // the verdict, and what we can't see
            var present = mine.Where(d => d.Present).ToList();
            rows.Insert(0, R(bad > 0 ? 2 : warn > 0 ? 1 : 0, "Verdict",
                bad > 0 ? "Found the likely reason - " + (next != "" ? next : "see below") : warn > 0 ? "Nothing broken, but " + (next != "" ? next.Substring(0, 1).ToLowerInvariant() + next.Substring(1) : "see the notes below") : q != "" && present.Count == 0 ? "Windows remembers it, but it isn't plugged in right now - plug it in, then run this again" : "Windows sees " + (q != "" ? "it" : "every USB device") + " working - if it still misbehaves, the problem is inside the device or its app",
                "Read from Windows' Plug and Play list - nothing is changed. AdeeboMeter can't test the device's own hardware.", "w"));
            if (bad == 0 && warn == 0 && present.Count > 0) foreach (var d in present.Take(12)) rows.Add(R(0, Draw.Trunc(d.Name, 50), "working" + (d.DriverVer != "" ? " · driver " + d.DriverVer : "") + " · plugged in since " + DevInfo.When(d.Arrived), "", "w"));
            return rows;
        }
        // powercfg's own answer for the current plan: USB settings › USB selective suspend (on battery and plugged in)
        static string SelectiveSuspend()
        {
            try
            {
                string pc = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "powercfg.exe"); if (!System.IO.File.Exists(pc)) return "";
                string t = Job.Run(pc, "/qh SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226", 8000) ?? "";
                var m = Regex.Matches(t, @"0x0000000([01])\s*$", RegexOptions.Multiline);
                if (m.Count == 0) return "";
                return m.Cast<Match>().Any(x => x.Groups[1].Value == "1") ? "on" : "off";
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("powercfg: " + e.Message); return ""; }
        }

        // ================================================================== Why is my game lagging?
        // The path split in four legs - this PC, the router, the provider, the game server - from the pings AdeeboMeter
        // already sends, plus what else is happening on the PC and the line. The game's own ping and frame rate can't be read.
        public static List<string[]> Lag(Monitor m, string which)
        {
            var rows = new List<string[]>(); var games = Games.All; var gp = GamePing.Me; string q = (which ?? "").Trim();
            Game g = q != "" ? games.FirstOrDefault(x => x.Id.Equals(q, StringComparison.OrdinalIgnoreCase) || x.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) : null;
            if (g == null && gp.Playing != null) g = games.FirstOrDefault(x => x.Id == gp.Playing);
            if (g == null) { var mon = Games.Monitored(); g = games.FirstOrDefault(x => mon.Contains(x.Id)); }
            if (q != "" && g == null) { rows.Add(R(2, "Game", "\"" + q + "\" isn't in the Gamer Zone - add it there first (it knows the game's servers)", "", "u")); return rows; }
            string kind, ssid; int sig; lock (m.L) { kind = m.AdapterKind; ssid = m.Ssid; sig = m.WifiSignal; }
            var gw = m.GwPing.Values(30); var net = m.NetPing.Values(30);
            Func<double[], double> avg = GameStat.Avg; Func<double[], double> jit = GameStat.Jitter; Func<double[], double> loss = GameStat.Loss;
            double gA = avg(gw), gJ = jit(gw), gL = loss(gw), nA = avg(net), nJ = jit(net), nL = loss(net);
            double[] srv = Array.Empty<double>(); string srvName = "";
            if (g != null) { var b = gp.Best(g); if (b != null) { srv = gp.Stat(g.Id, b).Recent(60); srvName = b.Name; } }
            double sA = avg(srv), sJ = jit(srv), sL = loss(srv);
            bool playing = g != null && gp.Playing == g.Id;
            rows.Add(R(3, "Game", g == null ? "none in the Gamer Zone - the checks below are for the connection itself" : g.Name + (playing ? " (running now)" : " (not running - start a match for the truest picture)") + (srvName != "" ? " · server " + srvName : ""), "", "i"));
            // the four legs: which one is bad
            Func<double, string> ms = v => double.IsNaN(v) ? "-" : v.ToString("0", CultureInfo.CurrentCulture) + " ms";
            Func<double, string> pc = v => double.IsNaN(v) ? "-" : v.ToString("0.#", CultureInfo.CurrentCulture) + "%";
            // loss by count, not percent: one lost probe in 30 is 3% but means nothing; two is worth a look, three or more is real
            int gLost = gw.Count(double.IsNaN), sLost = srv.Count(double.IsNaN);
            // the two checks run at different paces, so take away only the share the home loss rate explains - never the raw count,
            // which could wipe out real ISP loss that happens alongside a Wi-Fi blip
            int nLost = Math.Max(0, net.Count(double.IsNaN) - (gw.Length > 0 ? (int)Math.Floor((double)gLost / gw.Length * net.Length) : 0));
            // no router data (the router check is off, or the router doesn't answer pings - many don't) is "can't tell",
            // never "your home network": if the internet answers, the leg before the router can't be what's broken
            bool gwEver; lock (m.L) gwEver = m.GwEverReplied;
            bool gwUnknown = gw.Length == 0 || (double.IsNaN(gA) && (!gwEver || !double.IsNaN(nA)));
            if (gwUnknown) { gLost = 0; nLost = net.Count(double.IsNaN); }
            int lvHome = gwUnknown ? 3 : double.IsNaN(gA) ? 2 : (gA > 50 || gJ > 30 || gLost >= 3) ? 2 : (gA > 20 || gJ > 10 || gLost == 2) ? 1 : 0;
            double ispA = double.IsNaN(nA) ? double.NaN : gwUnknown || double.IsNaN(gA) ? nA : Math.Max(0, nA - gA);
            double ispJ = gwUnknown || double.IsNaN(gJ) ? nJ : nJ - gJ;
            // no internet probes at all (the internet check is off by policy) is "can't tell" too
            int lvIsp = net.Length == 0 ? 3 : double.IsNaN(nA) ? 2 : (ispA > (gwUnknown ? 80 : 60) || ispJ > 20 || nLost >= 3) ? 2 : (ispA > (gwUnknown ? 45 : 30) || ispJ > 10 || nLost == 2) ? 1 : 0;
            int lvSrv = srv.Length == 0 || double.IsNaN(sA) ? 3 : (sA > 120 || sJ > 25 || (sL > 3 && sLost >= 3)) ? 2 : (sA > 70 || sJ > 12 || (sL > 1 && sLost >= 2)) ? 1 : 0;
            rows.Add(R(lvHome, kind == "Wi-Fi" ? "This PC → router (Wi-Fi)" : "This PC → router (cable)", gwUnknown ? (gw.Length == 0 ? "not measured - the router check is off in Settings" : "your router doesn't answer pings, so this leg can't be measured on its own") + (kind == "Wi-Fi" && sig >= 0 ? " · signal " + sig + "%" : "") : ms(gA) + " · jitter " + ms(gJ) + " · lost " + gLost + " of " + gw.Length + (kind == "Wi-Fi" && sig >= 0 ? " · signal " + sig + "%" : ""), "Should be under 5 ms on a cable and under 10 ms on good Wi-Fi.", "i"));
            rows.Add(R(lvIsp, gwUnknown ? "This PC → your provider → internet" : "Router → your provider → internet", net.Length == 0 ? "not measured - the internet check is off" : (gwUnknown ? "" : "+") + ms(ispA) + " · jitter " + ms(nJ) + " · lost " + nLost + " more" + (nLost == 1 ? " (one lost probe means little)" : ""), "The part your provider is responsible for.", "i"));
            rows.Add(R(lvSrv, "→ the game's server" + (srvName != "" ? " (" + srvName + ")" : ""), srv.Length == 0 ? "not measured yet - add the game in the Gamer Zone and start it" : ms(sA) + " · jitter " + ms(sJ) + " · lost " + sLost + " of " + srv.Length, "Distance to the server sets the floor; the Gamer Zone compares regions.", "i"));
            // what else is going on
            double up = m.Up.Avg(10), down = m.Down.Avg(10), cpu = m.Cpu.Avg(10), mem = m.Mem.Avg(10);
            int lvLoad = !double.IsNaN(up) && up > 5 ? 1 : 0;
            if (!double.IsNaN(up) || !double.IsNaN(down)) rows.Add(R(lvLoad, "Your line right now", "down " + (double.IsNaN(down) ? "-" : down.ToString("0.#", CultureInfo.CurrentCulture)) + " Mb/s · up " + (double.IsNaN(up) ? "-" : up.ToString("0.#", CultureInfo.CurrentCulture)) + " Mb/s", lvLoad == 1 ? "Something is uploading - a backup, a cloud sync or a stream. Uploads fill the queue your game's packets wait in." : "", "i"));
            int lvPc = (!double.IsNaN(cpu) && cpu > 90) || (!double.IsNaN(mem) && mem > 92) ? 1 : 0;
            if (!double.IsNaN(cpu)) rows.Add(R(lvPc, "This PC", "CPU " + cpu.ToString("0", CultureInfo.CurrentCulture) + "% · memory " + (double.IsNaN(mem) ? "-" : mem.ToString("0", CultureInfo.CurrentCulture) + "%"), lvPc == 1 ? "A PC this busy drops frames - that feels like lag but isn't the network." : "", "i"));
            List<Spike> sp; lock (gp.Spikes) sp = gp.Spikes.Where(x => g == null || x.Game == g.Id).Where(x => (DateTime.Now - x.T).TotalMinutes < 30).ToList();
            if (sp.Count > 0) { var top = sp.GroupBy(x => x.Blame).OrderByDescending(gr => gr.Count()).First(); rows.Add(R(sp.Count >= 3 ? 1 : 3, "Spikes (30 min)", sp.Count + " · most blamed on " + top.Key, "", "i")); }
            // verdict: the worst leg wins; all clean = not the network
            string verdict, next;
            if (lvHome == 2 || (lvHome == 1 && lvIsp < 2 && lvSrv < 2)) { verdict = kind == "Wi-Fi" ? "Your Wi-Fi" + (sig >= 0 && sig < 60 ? " (weak signal)" : "") + " - the lag starts before the router" : "Your home network - the lag starts before the router"; next = kind == "Wi-Fi" ? "Move closer to the router or use a cable for one match: if the lag goes, that was it." : "Check the cable and the router (restart it)."; }
            else if (lvIsp == 2 || lvIsp == 1) { verdict = "Your provider - the router is fine, the delay is past it"; next = lvLoad == 1 ? "First stop the upload that's running, then test again; if it stays, the Report for my ISP shows them the numbers." : "Report for my ISP collects the proof (times, loss, the hop where it starts)."; }
            else if (lvSrv >= 1 && lvSrv != 3) { verdict = "The game server's side - your connection is clean up to the internet"; next = "Try a closer region in the game, or Find my match server in the Gamer Zone to see the real server."; }
            else if (lvPc == 1) { verdict = "This PC is too busy - the network is fine"; next = "Close what's using the CPU (Monitor shows it) and lower the game's settings."; }
            else if (lvLoad == 1) { verdict = "An upload is running - the legs look fine right now"; next = "Pause the backup or sync while you play."; }
            else { verdict = "The network looks fine - likely the PC or the game itself (frame rate)"; next = "Turn on the game's own frame-rate counter: if it drops when it lags, it's the PC, not the connection."; }
            int vlv = verdict.StartsWith("The network looks fine", StringComparison.Ordinal) ? 0 : 1;
            rows.Insert(0, R(vlv, "Verdict", verdict, "Next: " + next + " For the full picture, Capture this conversation in the packet journey shows the game's own packets - stalls, bursts and gaps.", "i"));
            rows.Add(R(3, "Honest limits", "AdeeboMeter measures the network with its own probes; it can't read the game's in-game ping or frame rate.", "", "i"));
            return rows;
        }
    }
}
