// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // ================================================================== tool pictures, wave 6 (Adeeb, 15 Nov): over time, the PC, keys - from the result
    // The last wave: what happened when (a timeline with the break and the first suspect), how full and how hot (gauges), who uses
    // what (bars), where the month is heading (a forecast line against the cap), the two halves of a key and who holds which,
    // two runs or two PCs side by side. Everything is read from the result; before a run the same shapes show what they'll hold.
    sealed partial class FullView
    {
        static readonly HashSet<string> Pictured6 = new HashSet<string> {
            "timeline", "winupdate", "changed", "usb", "rushhour", "besttime", "sixmonths", "wakeup", "dltime", "leaks", "gpu", "bg", "certs", "pressure", "disk",
            "capfc", "month", "svcfail", "events", "winev", "drivers", "bluetooth", "prematch", "tonight", "keys", "newkey", "recipients", "selfsigned", "csrmake",
            "hashf", "certconv", "certcmp", "beforeafter", "twopc", "ispreport", "qr", "marketopen", "board", "golive", "srvchange", "tradetape", "afterstream", "uptime", "certcal", "obs" };
        static readonly HashSet<string> Moving6 = new HashSet<string> { "board", "srvchange", "tradetape", "afterstream", "timeline", "winupdate", "changed", "usb", "tonight", "ispreport", "keys", "newkey", "recipients", "selfsigned", "csrmake" };
        static readonly string[] DotSep6 = { " · " };

        static double Or2(double a, double b) { return double.IsNaN(a) ? b : a; }
        float Wave6(Graphics g, float x, float y, float w, string st)
        {
            var res = Res();
            switch (tbSel)
            {
                // ---------------------------------------------------------------- when: a timeline
                case "timeline": case "winupdate": case "changed": case "usb":
                    {
                        var ev = new List<string[]>();   // when, what, level
                        // bug-hunt ML-222: the times are in the row names - "18:40", "Mon 9 Nov 18:40 (after)" (Picks.Timeline); "Installed / Windows Update / Driver Mon 9 Nov" (PcHealth.Changes)
                        if (tbSel == "timeline") foreach (var o in res.Where(o => o.Length > 2 && Regex.IsMatch(o[1], @"\d{1,2}:\d{2}(\s+\(after\))?$") && o[1] != "The break")) ev.Add(new[] { Regex.Replace(o[1], @"\s+\(after\)$", ""), o[2], Lv(o).ToString(CultureInfo.InvariantCulture) });
                        else if (tbSel == "winupdate") foreach (var o in res.Where(o => o[1].Contains(" · ")).Reverse()) ev.Add(new[] { P(o[1], 0), P(o[1], 1) + ": " + Regex.Replace(o[2], @"\s*-\s*(things|no change).*$", ""), Lv(o).ToString(CultureInfo.InvariantCulture), Regex.Match(o[2], @"ping\s*[0-9.]+\s*ms\s*→\s*[0-9.]+\s*ms").Value });
                        else if (tbSel == "changed") foreach (var o in res.Where(o => o.Length > 2 && Regex.IsMatch(o[1], @"^(Installed|Windows Update|Driver) "))) { var km = Regex.Match(o[1], @"^(Installed|Windows Update|Driver) (.+)$"); ev.Add(new[] { km.Groups[2].Value, km.Groups[1].Value + ": " + Cut(o[2], 60), Lv(o).ToString(CultureInfo.InvariantCulture) }); }
                        else foreach (var o in res.Where(o => o[1] != "Verdict")) ev.Add(new[] { Or(Regex.Match(o[2], @"\d{1,2}:\d{2}").Value, "·"), o[1] + ": " + Cut(o[2], 50), Lv(o).ToString(CultureInfo.InvariantCulture) });
                        string fs = RV("First suspect"); if (fs != "") foreach (var e in ev) if (fs.StartsWith(e[1], StringComparison.Ordinal) && e[2] != "2") e[2] = "1";
                        string sus = tbSel == "timeline" ? RV("First suspect") : tbSel == "usb" ? RV("Verdict") : tbSel == "winupdate" ? RV("Verdict") : "";
                        return Timeline(g, x, y, w, st, tbSel == "timeline" ? Kit.Sent(L.T("WHAT HAPPENED BEFORE IT BROKE AT")) + " " + Or(RV("The break"), "?") : tbSel == "winupdate" ? "UPDATES AND WHAT CAME AFTER" : tbSel == "changed" ? "WHAT CHANGED THIS WEEK" : "WHAT YOUR USB DEVICE DID", ev,
                            Or(Cut(sus, 220), "Things in the order they happened - the red one is when it broke; the amber one is the likeliest cause."));
                    }

                // ---------------------------------------------------------------- how much: bars
                case "rushhour": case "besttime": case "sixmonths": case "wakeup": case "dltime": case "leaks": case "gpu": case "bg": case "certs": case "certcal": case "uptime":
                    {
                        var bars = new List<Tuple<string, double, string, int>>(); string cap, unit = "", foot; double mark = double.NaN;
                        switch (tbSel)
                        {
                            case "rushhour": foreach (var o in res.Where(o => Regex.IsMatch(o[1], "day$"))) bars.Add(Tuple.Create(o[1], RowMs(o[2]), P(o[2], 0), Lv(o))); mark = RowMs(RV("Usual")); unit = "ms"; cap = "THE SLOWEST HOUR OF EACH DAY"; foot = "The line is your usual delay. " + Or(res.Where(o => Lv(o) == 1 && o.Length > 3).Select(o => o[3]).FirstOrDefault(), "Rush hour is when everyone is streaming."); break;
                            case "besttime": foreach (var o in res.Where(o => RowMs(o[2]) > 0 && o[1] != "Server")) bars.Add(Tuple.Create(o[1], RowMs(o[2]), P(o[2], 1), Lv(o))); unit = "ms"; cap = "WHEN " + Or(P(RV("Server"), 0), "THE SERVER").ToUpperInvariant() + " PLAYS BEST"; foot = Or(res.Where(o => o[1] == "Evenings" && o.Length > 3).Select(o => o[3]).FirstOrDefault(), "Short bars = the best hours to play."); break;
                            case "sixmonths": foreach (var o in res.Where(o => Regex.IsMatch(o[2], @"% smooth"))) bars.Add(Tuple.Create(o[1], VNum(o[2], @"([0-9.]+)%"), P(o[2], 1) + " · " + P(o[2], 3), Lv(o))); unit = "%"; cap = "SIX MONTHS, MONTH BY MONTH";   /* L-354: no dangling " - " (there's no "Smooth" row) */ foot = Or(RV("Outages"), "") + (RV("Saved") != "" ? " · " + RV("Saved") : ""); break;
                            case "wakeup": foreach (var o in res.Where(o => Regex.IsMatch(o[1], @"^\w{3} \d"))) bars.Add(Tuple.Create(o[1], VNum(o[2], @"([0-9.]+)\s*s on average"), P(o[2], 0) + " · " + P(o[2], 2), Lv(o))); unit = "s"; cap = "BACK ONLINE AFTER WAKING - " + Or(P(RV("Back online"), 0), "").ToUpperInvariant(); foot = Or(RRow("Back online") != null && RRow("Back online").Length > 3 ? RRow("Back online")[3] : "", "Slow wake-ups are usually Wi-Fi power saving or a long DHCP renew."); break;
                            case "dltime": foreach (var o in res) bars.Add(Tuple.Create(o[1], VNum(o[2], @"([0-9.]+)\s*min"), Regex.Match(o[2], @"[0-9.]+\s*Mbps").Value, Lv(o))); unit = "min"; cap = Kit.Sent(L.T("HOW LONG")) + " " + Or(VisIn()[0], "IT").ToUpperInvariant() + " " + Kit.Sent(L.T("TAKES")); foot = "From your own speed tests and what your downloads really got - not the plan's number."; break;
                            case "leaks": foreach (var o in res.Where(o => o.Length > 2 && o[2].StartsWith("+", StringComparison.Ordinal))) { var m = Regex.Match(o[2], @"^\+([0-9.,]+)\s*(MB|GB)"); double grew = m.Success ? double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture) * (m.Groups[2].Value == "GB" ? 1024 : 1) : double.NaN; bars.Add(Tuple.Create(o[1], grew, Cut(o[2], 60), Lv(o))); } unit = "MB more";   /* bug-hunt ML-223: PcHealth.Leaks names rows by program, "+1.2 GB in 2.0 h" */ cap = "WHAT KEEPS GROWING"; foot = Or(res.Where(o => o.Length > 3).Select(o => o[3]).FirstOrDefault(), "Memory that only goes up, never down, is a leak - a restart gives it back."); break;
                            case "gpu": foreach (var o in res) bars.Add(Tuple.Create(o[1], VNum(o[2], @"([0-9.]+)%"), P(o[2], 1), Lv(o))); unit = "%"; cap = "THE GRAPHICS CARD, PER APP"; foot = "Video memory full = stutter; a busy GPU at 100% = the game is limited by graphics, not the network."; break;
                            case "bg": foreach (var o in res.Where(o => o.Length > 2 && o[2].Contains("% processor"))) bars.Add(Tuple.Create(o[1], VNum(o[2], @"([0-9.]+)%\s*processor"),   /* bug-hunt ML-224 */ P(o[2], 0) + (P(o[2], 2) != "" ? " · " + P(o[2], 2) : ""), Lv(o))); unit = "% CPU"; cap = "WORKING IN THE BACKGROUND RIGHT NOW"; foot = "Uploads and updates in the background eat the line too - pause them before a call or a match."; break;
                            case "uptime": foreach (var o in res.Where(o => o[2].Contains("% up"))) bars.Add(Tuple.Create(o[1], VNum(o[2], @"([0-9.]+)%"), P(o[2], 2) + " · " + P(o[2], 3), Lv(o))); unit = "%"; mark = 99; cap = "UPTIME - " + Or(P(RV("Period"), 0), "").ToUpperInvariant(); foot = "The line is 99%. Each bar is the share of checks that got an answer; p50/p95 is the usual and the slow handshake."; break;
                            default: foreach (var o in res.Where(o => o.Length > 2 && (o[2].Contains("expires in") || Regex.IsMatch(o[2], @"^expires .*\((\d+) days\)") || o[2].StartsWith("EXPIRED", StringComparison.Ordinal) || o[2].StartsWith("expired", StringComparison.Ordinal))))   /* bug-hunt ML-225: SysTools.Certs says "expires 3 Mar 2027 (102 days)" */
                                    bars.Add(Tuple.Create(o[1], o[2].StartsWith("EXPIRED", StringComparison.OrdinalIgnoreCase) ? 0 : Or2(VNum(o[2], @"expires in\s*(\d+)"), VNum(o[2], @"\((\d+) days\)")), P(o[2], 0), Lv(o))); unit = "days"; mark = 30; cap = "CERTIFICATES AND THE DAYS THEY HAVE LEFT"; foot = Or(res.Where(o => o.Length > 3 && o[3] != "").Select(o => o[3]).FirstOrDefault(), "The line is 30 days. Company certificates renew by themselves on the company network."); break;
                        }
                        return Bars(g, x, y, w, st, cap, bars, unit, mark, foot);
                    }

                // ---------------------------------------------------------------- how full: gauges
                case "pressure": case "disk":
                    {
                        var gs = new List<Tuple<string, double, string, int>>();
                        foreach (var o in res)
                        {
                            // bug-hunt ML-231: "Space on C:" says "120 GB free of 500 GB" - the gauge shows how full it is (and numbers may carry a decimal comma)
                            double pct = double.NaN; var fm = Regex.Match(o[2], @"([0-9.,]+)\s*GB free of\s*([0-9.,]+)\s*GB"); var m = Regex.Match(o[2], @"([0-9.,]+)\s*(?:GB\s*)?of\s*([0-9.,]+)\s*GB");
                            Func<string, double> num = z => { double d; return double.TryParse(z.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : double.NaN; };
                            if (fm.Success) pct = 100 * (1 - num(fm.Groups[1].Value) / num(fm.Groups[2].Value));
                            else if (m.Success) pct = 100 * num(m.Groups[1].Value) / num(m.Groups[2].Value);
                            else if (Regex.IsMatch(o[2], @"[0-9.]+%\s*(full|worn)")) pct = VNum(o[2], @"([0-9.]+)%");
                            else if (o[2].Contains("°C")) pct = VNum(o[2], @"([0-9.]+)\s*°C");
                            gs.Add(Tuple.Create(o[1], pct, o[2], Lv(o)));
                        }
                        return Gauges(g, x, y, w, st, tbSel == "disk" ? "THE DISK" : "MEMORY, DISK AND HEAT", gs, "Green is room to spare. Memory near full makes Windows swap to disk; a hot CPU slows itself down.");
                    }
                case "capfc":
                    return Forecast(g, x, y, w, st);

                // ---------------------------------------------------------------- tiles: facts, devices, a checklist
                case "obs": case "marketopen": case "month": case "svcfail": case "events": case "winev": case "drivers": case "bluetooth": case "prematch":
                    {
                        var t = new List<string[]>(); string grp = ""; string cap;
                        foreach (var o in VisRows().Where(o => o.Length > 2))
                        {
                            if ((tbSel == "marketopen" && o[1] == "Market-open check") || (tbSel == "obs" && o[1] == "OBS link")) continue;
                            if (o[0] == "-1" || (o[2] == "" && string.Equals(o[1], o[1].ToUpperInvariant(), StringComparison.Ordinal))) { grp = o[1]; continue; }
                            if (tbSel == "month" && o[1].StartsWith("Compared with ", StringComparison.Ordinal))   /* L-355: whatever the previous month is */ { t.Add(new[] { o[1], o[2], o[0], Kit.Sent(L.T("AGAINST LAST MONTH")) }); continue; }
                            t.Add(new[] { o[1], P(o[2], 0) + (P(o[2], 1) != "" ? " · " + P(o[2], 1) : ""), o[0], tbSel == "drivers" || tbSel == "bluetooth" ? grp : "" });
                        }
                        cap = tbSel == "obs" ? Or(RV("OBS link"), "OBS LINK") : tbSel == "marketopen" ? Or(RV("Market-open check"), "MARKET-OPEN CHECK") : tbSel == "month" ? Or(VisRows().Where(o => o[0] == "-1").Select(o => o[1]).FirstOrDefault(), "YOUR MONTH") : tbSel == "svcfail" ? "SERVICES THAT FAILED TO START" : tbSel == "events" || tbSel == "winev" ? "WHAT WINDOWS WROTE DOWN" : tbSel == "drivers" ? "DEVICES, DRIVERS AND USB" : tbSel == "bluetooth" ? "BLUETOOTH" : Or(P(RV("Ready to play"), 0), "READY TO PLAY?").ToUpperInvariant();
                        return Tiles(g, x, y, w, st, cap.ToUpperInvariant(), t, tbSel == "prematch" ? Or(RRow("Ready to play") != null && RRow("Ready to play").Length > 3 ? RRow("Ready to play")[3] : "", "Amber is worth fixing before the match.") : "Red needs you; amber is worth a look; the rest is for the record.");
                    }
                case "srvchange":
                    {
                        var ev = new List<string[]>(); string since = res.Where(o => o[1].StartsWith("Since ", StringComparison.Ordinal)).Select(o => o[1].Substring(6)).FirstOrDefault() ?? "";
                        var all = VisRows().Where(o => o.Length > 2).ToList(); foreach (var o in all.TakeWhile(o => o[0] != "-1").Where(o => o[2].Contains("→"))) ev.Add(new[] { L.T("now"), o[1] + ": " + o[2], o[1].StartsWith("Certificate expires", StringComparison.Ordinal) || o[1] == "Open ports" ? "2" : "1" });
                        bool inPast = false; foreach (var o in all) { if (o[0] == "-1") { inPast = o[1] == "EARLIER CHANGES"; continue; } if (inPast) ev.Add(new[] { o[1], o[2], "3" }); }
                        if (ev.Count == 0) ev.Add(new[] { L.T("now"), res.Count > 0 ? res[0][1] + ": " + res[0][2] : L.T("first check - nothing to compare yet"), "0" });
                        return Timeline(g, x, y, w, st, Kit.Sent(L.T("WHAT CHANGED ON")) + " " + Or(VisIn()[0], "THE SERVER").ToUpperInvariant() + (since != "" ? " · " + Kit.Sent(L.T("SINCE")) + " " + since.ToUpperInvariant() : ""), ev, "Each check keeps a snapshot: DNS records, open ports, certificate, banners. A change right after a deploy is expected; one nobody made is worth a question.");
                    }
                case "tradetape": case "afterstream":
                    return SessionPic(g, x, y, w, st);
                case "board":
                    {
                        var lanes = res.Where(o => o[1].Contains(":") && o[1] != "Up right now").Select(o => Tuple.Create(o[1], RowMs(o[2]), VNum(o[2], @"max\s*([0-9.]+)"), Lv(o))).ToList();
                        foreach (var o in res.Where(o => Lv(o) == 2 && o[1].Contains(":"))) lanes.RemoveAll(l => l.Item1 == o[1]);
                        var down = res.Where(o => Lv(o) == 2 && o[1].Contains(":")).Select(o => o[1]).ToList();
                        return RaceCard(g, x, y, w, st, "SERVICE BOARD - " + Or(RV("Up right now"), "").ToUpperInvariant() + " UP", lanes, down.Count > 0 ? L.T("Down:") + " " + string.Join(", ", down.ToArray()) : "Blue: the average handshake. Purple: the slowest of 5 tries. Every service answered.");
                    }
                case "golive":
                    {
                        var bars = new List<Tuple<string, double, string, int>>(); double kb = VNum(RV("Your bitrate"), @"([0-9.]+)");
                        var u = RRow("Your upload"); if (u != null) bars.Add(Tuple.Create(L.T("Your upload"), VNum(u[2], @"([0-9.]+)\s*Mbps") * 1000, "", Lv(u)));
                        var sf = RRow("Safe bitrate"); if (sf != null) bars.Add(Tuple.Create(L.T("Safe bitrate"), VNum(sf[2], @"([0-9.]+)"), "", Lv(sf)));
                        if (!double.IsNaN(kb)) bars.Add(Tuple.Create(L.T("Your bitrate"), kb, "", RLv("Your bitrate")));
                        var iu = RRow("Upload in use"); if (iu != null) bars.Add(Tuple.Create(L.T("Already uploading"), VNum(iu[2], @"([0-9.]+)") * 1000, "", Lv(iu)));
                        return Bars(g, x, y, w, st, Or(RV("Go-live check"), "GO-LIVE CHECK").ToUpperInvariant(), bars, "kbps", kb, Or(res.Where(o => o[1].EndsWith(" ingest", StringComparison.Ordinal)).Select(o => o[1] + ": " + o[2]).FirstOrDefault(), "The line is your bitrate. Your upload should be about 1.5x it."));
                    }
                case "tonight":
                    {
                        var lanes = res.Where(o => RowMs(o[2]) > 0).Select(o => Tuple.Create(Regex.Replace(o[1], @"^(★|\d\.)\s*", "") + " · " + P(o[2], 0), RowMs(o[2]), VNum(o[2], @"jitter\s*([0-9.]+)"), Lv(o))).ToList();
                        return RaceCard(g, x, y, w, st, "WHICH GAME PLAYS BEST RIGHT NOW", lanes, "Blue: the ping. Purple: the jitter (how much it wobbles). The shortest pair wins.");
                    }
                case "ispreport":
                    {
                        var hops = res.Where(o => o[1].Contains("hop")).Select(o => Tuple.Create(Regex.Match(o[1], @"\((?:hop\s*)?(\d+)\)").Groups[1].Value, Regex.Replace(o[1], @"\s*\(.*\)", ""), VNum(o[2], @"([0-9.]+)\s*ms avg"), Regex.Replace(o[1], @"\s*\(.*\)", ""), Lv(o))).ToList();
                        return HopRoad(g, x, y, w, st, "WHAT GOES IN THE REPORT FOR YOUR PROVIDER", hops, Or(Cut(RV("Finding"), 220), "The three places that matter to a provider: your router, their first hop, the destination."));
                    }

                // ---------------------------------------------------------------- keys: two halves
                case "keys": case "newkey": case "recipients": case "selfsigned": case "csrmake":
                    return KeyPair(g, x, y, w, st);
                case "hashf":
                    return HashPic(g, x, y, w, st);
                case "qr":
                    return QrPic(g, x, y, w, st);

                // ---------------------------------------------------------------- side by side
                default:   // certconv, certcmp, beforeafter, twopc
                    return TwoCols(g, x, y, w, st);
            }
        }

        // ================================================================== the session tape: the line all session, every dip a band coloured by who caused it
        static readonly Dictionary<string, Color> BlameCol = new Dictionary<string, Color> { { "pc", Color.FromArgb(130, 80, 223) }, { "wifi", Color.FromArgb(191, 135, 0) }, { "home", Color.FromArgb(225, 112, 0) }, { "provider", Color.FromArgb(207, 34, 46) }, { "far", Color.FromArgb(87, 96, 106) }, { "unsure", Color.FromArgb(160, 168, 180) } };
        float SessionPic(Graphics g, float x, float y, float w, string st)
        {
            bool trade = tbSel == "tradetape"; var s = st == "EXAMPLE" ? null : Sectors.PickSession(trade ? "trade" : "stream", VisIn()[0]);
            if (s == null || s.Line.Count < 2)
            {   // no recorded session here (the Example, or nothing yet): the moments from the rows, on a timeline
                var ev = Res().Where(o => System.Text.RegularExpressions.Regex.IsMatch(o[1], @"^\d{1,2}:\d{2}")).Select(o => new[] { o[1], o[2], Lv(o).ToString(CultureInfo.InvariantCulture) }).ToList();
                return Timeline(g, x, y, w, st, trade ? "THE SESSION, SLOW MOMENT BY SLOW MOMENT" : "THE STREAM, DROP BY DROP", ev, Or(RV(trade ? "Slow moments" : "Drops"), "Each moment with who caused it - your PC, the Wi-Fi, home, the provider or the far end."));
            }
            var r = VisCard(g, x, y, w, 250 * S, trade ? Kit.Sent(L.T("THE SESSION")) + " · " + WatchKit.BlameName("far", s.Far).ToUpperInvariant() : Kit.Sent(L.T("THE STREAM")) + " · " + s.What.ToUpperInvariant(), st);
            float gx = r.X + 50 * S, gy = r.Y + 40 * S, gw = r.Width - 70 * S, gh = 130 * S; double mins = Math.Max(1, s.Length.TotalMinutes); int idx = trade ? 2 : 1;
            var vals = s.Line.Where(p => p.Length > idx && p[idx] >= 0).Select(p => (double)p[idx]).ToList(); double max = Math.Max(trade ? 20 : 5, vals.Count > 0 ? vals.Max() * 1.15 : 1);
            Func<double, float> X = m => gx + gw * (float)Math.Max(0, Math.Min(1, m / mins)); Func<double, float> Y = v => gy + gh - gh * (float)Math.Min(1, v / max);
            using (var p = new Pen(Theme.Line, 1 * S)) { g.DrawLine(p, gx, gy + gh, gx + gw, gy + gh); g.DrawLine(p, gx, gy, gx, gy + gh); }
            foreach (var d in s.Dips) { Color c; if (!BlameCol.TryGetValue(d.Blame, out c)) c = VSlate; double m0 = (d.T - s.Start).TotalMinutes; using (var b = new SolidBrush(Color.FromArgb(70, c))) g.FillRectangle(b, X(m0), gy, Math.Max(3 * S, X(m0 + d.Secs / 60.0) - X(m0)), gh); }
            if (trade) foreach (var mk in new[] { Tuple.Create(new TimeSpan(9, 30, 0), L.T("open")), Tuple.Create(new TimeSpan(16, 0, 0), L.T("close")) })
                { double m0 = (s.Start.Date + mk.Item1 - s.Start).TotalMinutes; if (m0 < 0 || m0 > mins) continue; using (var p = new Pen(Theme.Ink, 1.5f * S) { DashStyle = DashStyle.Dash }) g.DrawLine(p, X(m0), gy - 6 * S, X(m0), gy + gh); VText(g, mk.Item2, Draw.fTiny, Theme.Ink, X(m0) + 3 * S, gy - 8 * S); }
            var pts = s.Line.Where(p => p.Length > idx && p[idx] >= 0).Select(p => new PointF(X(p[0]), Y(p[idx]))).ToArray();
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (pts.Length > 1) using (var p = new Pen(trade ? Theme.Blue : Theme.Green, 2 * S)) g.DrawLines(p, pts);
            if (WindowsAnimates() && pts.Length > 1) { int k = (int)(Wrap((float)AnimT * 0.15f, 1f) * (pts.Length - 1)); using (var b = new SolidBrush(Theme.Ink)) g.FillEllipse(b, pts[k].X - 4 * S, pts[k].Y - 4 * S, 8 * S, 8 * S); }
            g.SmoothingMode = sm;
            VText(g, max.ToString("0", CultureInfo.CurrentCulture) + (trade ? " ms" : " Mb/s"), Draw.fTiny, Theme.Sub, r.X + 8 * S, gy - 4 * S);
            VText(g, s.Start.ToString("HH:mm", CultureInfo.CurrentCulture), Draw.fTiny, Theme.Sub, gx, gy + gh + 4 * S); var es = s.End.ToString("HH:mm", CultureInfo.CurrentCulture); VText(g, es, Draw.fTiny, Theme.Sub, gx + gw - g.MeasureString(es, Draw.fTiny).Width, gy + gh + 4 * S);
            float lx = gx; var tally = s.Tally();
            foreach (var kv in tally.Where(z => z.Value > 0)) { Color c; if (!BlameCol.TryGetValue(kv.Key, out c)) c = VSlate; Theme.Fill(g, new RectangleF(lx, gy + gh + 24 * S, 12 * S, 10 * S), 3 * S, c); string t = kv.Value + " " + WatchKit.BlameName(kv.Key, s.Far); VText(g, t, Draw.fSmall, Theme.Ink, lx + 16 * S, gy + gh + 21 * S); lx += 40 * S + g.MeasureString(t, Draw.fSmall).Width; }
            VFoot(g, r, trade ? "The line is the broker's answer time each minute; each band is a slow moment, coloured by who caused it." : "The line is the download each minute; each band is a drop, coloured by who caused it.");
            return r.Bottom + 14 * S;
        }

        // ================================================================== the drawings wave 6 adds
        // things in the order they happened, on one line: red = it broke, amber = the likeliest cause; a cursor walks it
        static float Timeline(Graphics g, float x, float y, float w, string st, string cap, List<string[]> ev, string foot)
        {
            if (ev.Count == 0) { ev = new List<string[]> { new[] { "18:14", L.T("a change"), "1" }, new[] { "18:40", L.T("it broke"), "2" }, new[] { "18:47", L.T("back"), "0" } }; cap += "  ·  " + Kit.Sent(L.T("EXAMPLE - NO DATED EVENTS IN THIS RESULT")); }   // bug-hunt L-358
            int moreE = ev.Count - 7; ev = ev.Take(7).ToList(); if (moreE > 0) cap += "  ·  +" + moreE + " " + L.T("more");   // bug-hunt L-353
            var r = VisCard(g, x, y, w, 90 * S + ev.Count * 34 * S + 44 * S, cap, st);
            float lx = r.X + 110 * S, top = r.Y + 48 * S; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(Theme.Line, 3 * S)) g.DrawLine(p, lx, top, lx, top + (ev.Count - 1) * 34 * S + 8 * S);
            float cur = WindowsAnimates() ? Wrap((float)AnimT * 0.6f, ev.Count + 1f) : ev.Count;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i]; int lv; if (!int.TryParse(e[2], out lv)) lv = -1; float ey = top + i * 34 * S + 4 * S; Color c = lv == 2 ? Theme.Red : lv == 1 ? Theme.Amber : lv == 0 ? Theme.Green : VSlate;
                bool lit = true; using (var b = new SolidBrush(lit ? c : Theme.Line)) g.FillEllipse(b, lx - 7 * S, ey - 7 * S, 14 * S, 14 * S);
                if ((int)cur == i) using (var p = new Pen(c, 2 * S)) g.DrawEllipse(p, lx - 12 * S, ey - 12 * S, 24 * S, 24 * S);
                var ts = g.MeasureString(e[0], Draw.fBodyB); VText(g, e[0], Draw.fBodyB, lit ? Theme.Ink : Theme.Muted, lx - 18 * S - ts.Width, ey - 9 * S);
                using (var b = new SolidBrush(lit ? (lv == 2 ? Theme.Red : Theme.Ink) : Theme.Muted)) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(e[1] + (e.Length > 3 && e[3] != "" ? "   (" + e[3] + ")" : ""), lv >= 1 ? Draw.fBodyB : Draw.fBody, b, new RectangleF(lx + 20 * S, ey - 9 * S, r.Right - lx - 40 * S, 20 * S), sf);
            }
            g.SmoothingMode = sm; using (var b = new SolidBrush(Theme.Sub)) g.DrawString(L.T(foot), Draw.fSmall, b, new RectangleF(r.X + 18 * S, r.Bottom - 50 * S, r.Width - 36 * S, 46 * S));
            return r.Bottom + 14 * S;
        }
        // one bar per thing, longest = most; an optional line (your usual, 30 days) to measure against
        static float Bars(Graphics g, float x, float y, float w, string st, string cap, List<Tuple<string, double, string, int>> bars, string unit, double mark, string foot)
        {
            PhoneB = Tuple.Create(cap, unit, bars.ToList()); PhoneKind = "b";
            bars = bars.Where(b => !double.IsNaN(b.Item2)).ToList(); int moreB = bars.Count - 10; bars = bars.Take(10).ToList(); if (moreB > 0) cap += "  ·  +" + moreB + " " + L.T("more");   // bug-hunt L-353
            if (bars.Count == 0) bars = new List<Tuple<string, double, string, int>> { Tuple.Create(L.T("(run it to see)"), 0.0, "", -1) };
            var r = VisCard(g, x, y, w, 56 * S + bars.Count * 34 * S + 50 * S, cap, st);
            float nx = r.X + 18 * S, bx = r.X + 180 * S, bw = r.Width - 180 * S - 230 * S, by = r.Y + 46 * S; double max = Math.Max(bars.Max(b => b.Item2), double.IsNaN(mark) ? 0 : mark) * 1.08; if (max <= 0) max = 1;
            if (unit == "%") max = 100;
            foreach (var b in bars)
            {
                Color c = b.Item4 == 2 ? Theme.Red : b.Item4 == 1 ? Theme.Amber : b.Item4 == 0 ? Theme.Green : Theme.Blue;
                using (var ib = new SolidBrush(Theme.Ink)) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(b.Item1, Draw.fBody, ib, new RectangleF(nx, by + 2 * S, bx - nx - 10 * S, 20 * S), sf);
                Theme.Fill(g, new RectangleF(bx, by + 4 * S, bw, 16 * S), 5 * S, Theme.Soft);
                Theme.Fill(g, new RectangleF(bx, by + 4 * S, Math.Max(4 * S, bw * (float)(b.Item2 / max)), 16 * S), 5 * S, c);
                VText(g, b.Item2.ToString(b.Item2 < 10 ? "0.#" : "0", CultureInfo.CurrentCulture) + " " + unit + (b.Item3 != "" ? "  " + Cut(b.Item3, 30) : ""), Draw.fSmall, Theme.Sub, bx + bw + 10 * S, by + 4 * S);
                by += 34 * S;
            }
            if (!double.IsNaN(mark)) { float mx = bx + bw * (float)(mark / max); using (var p = new Pen(Theme.Ink, 1.5f * S) { DashStyle = DashStyle.Dash }) g.DrawLine(p, mx, r.Y + 40 * S, mx, by); }
            VFoot(g, r, foot); return r.Bottom + 14 * S;
        }
        // half-round gauges: how full, how worn, how hot
        static float Gauges(Graphics g, float x, float y, float w, string st, string cap, List<Tuple<string, double, string, int>> gs, string foot)
        {
            if (gs.Count == 0) gs = new List<Tuple<string, double, string, int>> { Tuple.Create(L.T("Memory"), double.NaN, "", -1), Tuple.Create(L.T("Disk"), double.NaN, "", -1), Tuple.Create(L.T("Heat"), double.NaN, "", -1) };
            int gmax = w > 640 * S ? 6 : 4, moreG = gs.Count - gmax; gs = gs.Take(gmax).ToList(); if (moreG > 0) cap += "  ·  +" + moreG + " " + L.T("more");   // bug-hunt L-357/L-353: all six pressures on a wide window
            var r = VisCard(g, x, y, w, 230 * S, cap, st); float cw = (r.Width - 36 * S) / gs.Count;
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < gs.Count; i++)
            {
                var t = gs[i]; float cx = r.X + 18 * S + cw * i + cw / 2, cy = r.Y + 130 * S, rad = Math.Min(70 * S, cw / 2 - 16 * S);
                var box = new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2);
                using (var p = new Pen(Theme.Soft, 14 * S)) g.DrawArc(p, box, 180, 180);
                // the colour is the check's own verdict when it has one (102% of rated speed is good, not "full")
                Color c = t.Item4 == 2 ? Theme.Red : t.Item4 == 1 ? Theme.Amber : t.Item4 == 0 ? Theme.Green : t.Item2 >= 90 ? Theme.Red : t.Item2 >= 75 ? Theme.Amber : Theme.Green;
                if (!double.IsNaN(t.Item2)) { using (var p = new Pen(c, 14 * S)) g.DrawArc(p, box, 180, (float)(Math.Min(100, t.Item2) / 100 * 180)); VTextC(g, t.Item2.ToString("0", CultureInfo.CurrentCulture) + (t.Item3.Contains("°C") ? " °C" : "%"), Draw.fBodyB, Theme.Ink, cx, cy - 26 * S, rad * 2); }
                else if (t.Item4 >= 0)
                {
                    // no "how full" for pages/s or ms: the whole arc, faint, in the verdict's colour, and the number in the middle
                    using (var p = new Pen(Color.FromArgb(110, c), 14 * S)) g.DrawArc(p, box, 180, 180);
                    var lead = Regex.Match(t.Item3, @"^-?[0-9.,]+\s*(%|ms|pages/s|°C|GB|MB)?").Value.Trim(); VTextC(g, lead, Draw.fBodyB, Theme.Ink, cx, cy - 26 * S, rad * 2);
                }
                VTextC(g, t.Item1, Draw.fBodyB, Theme.Ink, cx, cy + 8 * S, cw - 10 * S); VTextC(g, Cut(t.Item3, 40), Draw.fTiny, Theme.Sub, cx, cy + 28 * S, cw - 10 * S);
            }
            g.SmoothingMode = sm; VFoot(g, r, foot); return r.Bottom + 14 * S;
        }
        // the month so far, the pace, the cap: where the line crosses it is the day you run out
        float Forecast(Graphics g, float x, float y, float w, string st)
        {
            // bug-hunt ML-227: the cap is the one set in Settings (also in "of N GB" when it isn't reached); no cap set = no cap line
            string so = RV("This month so far"), pace = RV("At this pace"); double used = VNum(so, @"([0-9.]+)\s*GB"), days = VNum(so, @"in\s*(\d+)\s*days"), cap = VNum(pace, @"reach\s*([0-9.]+)\s*GB"), day = VNum(pace, @"on the\s*(\d+)");
            if (double.IsNaN(cap)) cap = VNum(pace + " " + RV("Cap"), @"of\s*([0-9.]+)\s*GB"); if (double.IsNaN(cap) && Cfg.CapGB > 0) cap = Cfg.CapGB;
            bool ran0 = so != ""; if (double.IsNaN(used)) { used = 120; days = 10; cap = 400; day = double.NaN; } if (double.IsNaN(days) || days <= 0) days = DateTime.Now.Day; bool noCap = double.IsNaN(cap); if (noCap) cap = ran0 ? used / days * DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month) : 400;
            var r = VisCard(g, x, y, w, 250 * S, "WHERE THIS MONTH'S DATA IS HEADING", st);
            float gx = r.X + 60 * S, gy = r.Y + 50 * S, gw = r.Width - 100 * S, gh = 130 * S; int dim = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month); double top = Math.Max(cap, used / days * dim) * 1.1;
            Func<double, float> X = d => gx + gw * (float)(d / dim); Func<double, float> Y = v => gy + gh - gh * (float)(v / top);
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(Theme.Line, 1 * S)) { g.DrawLine(p, gx, gy + gh, gx + gw, gy + gh); g.DrawLine(p, gx, gy, gx, gy + gh); }
            if (!noCap) { using (var p = new Pen(Theme.Red, 2 * S) { DashStyle = DashStyle.Dash }) g.DrawLine(p, gx, Y(cap), gx + gw, Y(cap)); VText(g, L.T("cap") + " " + cap.ToString("0", CultureInfo.CurrentCulture) + " GB", Draw.fSmall, Theme.Red, gx + 6 * S, Y(cap) - 18 * S); }
            using (var p = new Pen(Theme.Blue, 4 * S)) g.DrawLine(p, X(0), Y(0), X(days), Y(used));
            using (var p = new Pen(Theme.Blue, 2 * S) { DashStyle = DashStyle.Dot }) g.DrawLine(p, X(days), Y(used), X(dim), Y(used / days * dim));
            using (var b = new SolidBrush(Theme.Blue)) g.FillEllipse(b, X(days) - 5 * S, Y(used) - 5 * S, 10 * S, 10 * S);
            VText(g, L.T("today:") + " " + used.ToString("0", CultureInfo.CurrentCulture) + " GB", Draw.fBodyB, Theme.Ink, X(days) + 8 * S, Y(used) + 2 * S);
            if (!double.IsNaN(day) && day <= dim) { using (var b = new SolidBrush(Theme.Red)) g.FillEllipse(b, X(day) - 6 * S, Y(cap) - 6 * S, 12 * S, 12 * S); VTextC(g, L.T("the") + " " + day.ToString("0", CultureInfo.CurrentCulture) + L.T("th: cap reached"), Draw.fBodyB, Theme.Red, X(day), Y(cap) + 8 * S, 160 * S); }
            VText(g, "1", Draw.fTiny, Theme.Sub, gx, gy + gh + 4 * S); VText(g, dim.ToString(CultureInfo.CurrentCulture), Draw.fTiny, Theme.Sub, gx + gw - 12 * S, gy + gh + 4 * S);
            g.SmoothingMode = sm; VFoot(g, r, Or(RRow("At this pace") != null && RRow("At this pace").Length > 3 ? pace + " - " + RRow("At this pace")[3] : pace, "The solid line is what you used; the dotted line is where the same pace ends the month."));
            return r.Bottom + 14 * S;
        }
        // a key pair: the private half stays on this PC (Windows keeps it), the public half travels to whoever needs it
        float KeyPair(Graphics g, float x, float y, float w, string st)
        {
            var res = Res(); string name, detail, trust = ""; var people = new List<string>();
            switch (tbSel)
            {
                case "keys":
                    {
                        // bug-hunt ML-226: each key is a heading row (level -1, which Res() leaves out) - read from all the rows
                        var k = VisRows().FirstOrDefault(o => o.Length > 2 && o[0] == "-1" && (o[2].Contains("RSA") || o[2].Contains("EC P-384")));
                        name = k != null ? k[1] : (res.Count > 0 ? L.T("no key yet") : "MY KEY"); detail = k != null ? k[2] : RV("My keys"); break;
                    }
                case "newkey": name = Or(VisIn()[0], "new key"); detail = Or(RV("Made"), ""); break;
                case "recipients": name = L.T("your key"); detail = Or(RV("SAVED RECIPIENTS"), ""); people = res.Where(o => o[0] != "0" && o[1] != "SAVED RECIPIENTS").Select(o => o[1] + " · " + P(o[2], 0)).Take(4).ToList(); break;
                case "selfsigned": name = Or(P(RV("Made"), 0), "lab01.lab"); detail = RV("Made"); trust = RV("Trust"); break;
                default: name = Or(VisIn()[0], "vpn-2027"); detail = RV("CSR"); break;
            }
            if (people.Count == 0) people = tbSel == "csrmake" ? new List<string> { L.T("your CA (signs it)") } : tbSel == "selfsigned" ? new List<string> { L.T("clients you tell to trust it") } : new List<string> { L.T("anyone who encrypts a file for you") };
            var r = VisCard(g, x, y, w, 90 * S + people.Count * 40 * S + 60 * S, tbSel == "csrmake" ? "A NEW KEY PAIR AND ITS REQUEST" : tbSel == "selfsigned" ? "A CERTIFICATE THAT VOUCHES FOR ITSELF" : "ONE KEY, TWO HALVES", st);
            float cy = r.Y + 70 * S + (people.Count - 1) * 20 * S; var priv = new RectangleF(r.X + 24 * S, cy - 28 * S, 200 * S, 56 * S); var pub = new RectangleF(priv.Right + 40 * S, cy - 28 * S, 180 * S, 56 * S);
            Theme.Fill(g, priv, 10 * S, Color.FromArgb(40, Theme.Red)); Kit.Icon(g, "lock", new RectangleF(priv.X + 10 * S, priv.Y + 18 * S, 20 * S, 20 * S), Theme.Red);
            VText(g, L.T("private half"), Draw.fBodyB, Theme.Ink, priv.X + 38 * S, priv.Y + 8 * S); VText(g, L.T("stays here - Windows keeps it"), Draw.fTiny, Theme.Sub, priv.X + 38 * S, priv.Y + 30 * S);
            Theme.Fill(g, pub, 10 * S, Color.FromArgb(40, Theme.Green)); Kit.Icon(g, "key", new RectangleF(pub.X + 10 * S, pub.Y + 18 * S, 20 * S, 20 * S), Theme.Green);
            VText(g, tbSel == "csrmake" ? L.T("public half + names") : L.T("public half"), Draw.fBodyB, Theme.Ink, pub.X + 38 * S, pub.Y + 8 * S); VText(g, tbSel == "csrmake" ? Cut(P(detail, 0), 24) : L.T("safe to share"), Draw.fTiny, Theme.Sub, pub.X + 38 * S, pub.Y + 30 * S);
            VText(g, Cut(name, 40), Draw.fBodyB, VPurple, priv.X, priv.Y - 24 * S);
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; float px0 = pub.Right + 10 * S, px1 = r.Right - 230 * S;
            for (int i = 0; i < people.Count; i++)
            {
                float ty = r.Y + 70 * S + i * 40 * S; using (var p = new Pen(Theme.Green, 2 * S)) { p.CustomEndCap = Cap45; g.DrawBezier(p, px0, cy, px0 + 40 * S, cy, px1 - 40 * S, ty, px1, ty); }
                VNode(g, px1 + 18 * S, ty, 14 * S, "devices", Theme.Blue, false); VText(g, Cut(people[i], 34), Draw.fSmall, Theme.Ink, px1 + 38 * S, ty - 8 * S);
                if (WindowsAnimates()) { float f = Wrap((float)AnimT * 0.5f + i * 0.3f, 1f); var pt = Bez(new PointF(px0, cy), new PointF(px0 + 40 * S, cy), new PointF(px1 - 40 * S, ty), new PointF(px1, ty), f); Kit.Icon(g, "key", new RectangleF(pt.X - 8 * S, pt.Y - 8 * S, 16 * S, 16 * S), Theme.Green); }
            }
            g.SmoothingMode = sm;
            VFoot(g, r, trust != "" ? Cut(trust, 120) : Cut(detail, 140) + (detail != "" ? " - " : "") + L.T("What's locked with the public half only the private half opens."));
            return r.Bottom + 14 * S;
        }
        // the QR code itself, big, right in the tool - no extra click - with what the phone will show
        string qrKey = "", qrLink = ""; Bitmap qrBmp; DateTime qrAt;   // the link is rebuilt at most every 30 s (it reads History)
        float QrPic(Graphics g, float x, float y, float w, string st)
        {
            if ((DateTime.Now - qrAt).TotalSeconds > 30 || qrLink == "") { qrLink = QrLink(); qrAt = DateTime.Now; } string link = qrLink, key = link;
            // R2-17: the code fits in half the visible height, so it's whole on screen when the tool opens - no scrolling to scan it
            int capH = tbCanvas != null && tbCanvas.ClientSize.Height > 0 ? tbCanvas.ClientSize.Height / 2 : 600; key += "|" + capH;
            if (key != qrKey || qrBmp == null) { if (qrBmp != null) qrBmp.Dispose(); var m = Qr.Encode(link); qrBmp = m == null ? null : Qr.Picture(m, Math.Max(2, Math.Min((int)(5 * S), capH / (m.GetLength(0) + 8)))); qrKey = key; }
            float side = qrBmp == null ? 0 : qrBmp.Width; var r = VisCard(g, x, y, w, Math.Max(side + 70 * S, 260 * S), "SCAN WITH YOUR PHONE'S CAMERA", st);
            if (qrBmp != null) { g.InterpolationMode = InterpolationMode.NearestNeighbor; g.DrawImage(qrBmp, r.X + 24 * S, r.Y + 44 * S, side, side); }
            float tx = r.X + 48 * S + side, ty = r.Y + 48 * S, tw = r.Right - tx - 24 * S;
            VText(g, L.T("One tap opens your result"), Draw.fBtn, Theme.Ink, tx, ty); ty += 34 * S;
            string[] lines = { L.T("the verdict and where the trouble is"), L.T("the numbers in plain words"), L.T("charts of the last checks and minutes"), L.T("what to do next"), L.T("Send to my provider · Share · Save as PDF") };
            foreach (var l in lines) { Theme.Fill(g, new RectangleF(tx, ty + 6 * S, 8 * S, 8 * S), 4 * S, Theme.Green); VText(g, l, Draw.fBody, Theme.Ink, tx + 16 * S, ty); ty += 24 * S; }
            using (var b = new SolidBrush(Theme.Sub)) g.DrawString(L.T("It opens adeebometer.com/r. Your numbers ride in the link after the # - that part never leaves the phone, so the site never sees them. Names and addresses are left out."), Draw.fSmall, b, new RectangleF(tx, ty + 8 * S, tw, 60 * S));
            return r.Bottom + 14 * S;
        }
        // a file's fingerprint: change one byte and the whole hash changes; compare it with the one the site published
        float HashPic(Graphics g, float x, float y, float w, string st)
        {
            string file = System.IO.Path.GetFileName(Or(VisIn()[0], "tool.zip")), h = Or(RV("SHA-256"), "3f2a9c…e71b"), want = VisIn().Length > 1 ? VisIn()[1].Trim() : ""; var r = VisCard(g, x, y, w, 200 * S, "THE FILE'S FINGERPRINT", st);
            var doc = new RectangleF(r.X + 30 * S, r.Y + 50 * S, 70 * S, 86 * S); Theme.Fill(g, doc, 6 * S, Theme.Soft); Kit.Icon(g, "report", new RectangleF(doc.X + 20 * S, doc.Y + 20 * S, 30 * S, 30 * S), Theme.Blue); VTextC(g, Cut(file, 20), Draw.fTiny, Theme.Sub, doc.X + doc.Width / 2, doc.Bottom + 6 * S, 140 * S);
            float hx = doc.Right + 70 * S; using (var p = new Pen(Theme.Ink, 2 * S)) { p.CustomEndCap = Cap45; g.DrawLine(p, doc.Right + 10 * S, doc.Y + 43 * S, hx - 10 * S, doc.Y + 43 * S); } VTextC(g, "SHA-256", Draw.fTiny, Theme.Sub, (doc.Right + hx) / 2, doc.Y + 22 * S, 80 * S);
            string hex = Regex.Replace(h, @"[^0-9A-Fa-f]", ""); int n = Math.Min(16, Math.Max(4, hex.Length / 2)); float cw = Math.Min(22 * S, (r.Right - hx - 30 * S) / n);
            for (int i = 0; i < n; i++)
            {
                string pair = hex.Length >= i * 2 + 2 ? hex.Substring(i * 2, 2) : "··"; int v; if (!int.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) v = 128;
                Theme.Fill(g, new RectangleF(hx + i * cw, doc.Y + 30 * S, cw - 2 * S, 26 * S), 3 * S, Color.FromArgb(255, 60 + v / 3, 90 + v / 4, 200 - v / 3));
            }
            VText(g, Cut(h, 70), Draw.fSmall, Theme.Ink, hx, doc.Y + 62 * S);
            if (want != "") { bool same = Regex.Replace(want, @"\s", "").Equals(Regex.Replace(RV("SHA-256"), @"\s", ""), StringComparison.OrdinalIgnoreCase); VText(g, (same ? L.T("matches the published one") : L.T("does NOT match what you pasted")), Draw.fBodyB, same ? Theme.Green : Theme.Red, hx, doc.Y + 84 * S); }
            VFoot(g, r, "Change one byte of the file and every colour above changes. Compare with the hash the download page publishes."); return r.Bottom + 14 * S;
        }
        // two runs, two PCs, two chains, two formats: side by side, the differences marked
        float TwoCols(Graphics g, float x, float y, float w, string st)
        {
            var res = Res(); string la, lb, cap; var rows = new List<string[]>();   // what, left, right, level
            switch (tbSel)
            {
                case "certconv": la = "PEM (text)"; lb = "DER (binary)"; cap = "THE SAME CERTIFICATE, TWO WAYS OF WRITING IT"; rows.Add(new[] { L.T("file"), System.IO.Path.GetFileName(Or(VisIn()[0], "server.crt")), Or(System.IO.Path.GetFileName(Or(Res().Where(o => o.Length > 2 && o[1].EndsWith(" written", StringComparison.Ordinal)).Select(o => o[2]).FirstOrDefault(), "").Trim()), "server.der")   /* L-356: CertTools names the row "PEM (text) written" / "DER (binary) written" */, "0" }); rows.Add(new[] { L.T("looks like"), "-----BEGIN CERTIFICATE-----", "30 82 01 E8 30 82 …", "-1" }); rows.Add(new[] { L.T("inside"), L.T("the same certificate"), L.T("the same certificate"), "0" }); break;
                case "certcmp":
                    {
                        // bug-hunt ML-228: EngPack.CertCompare - the run's name row (trust), the chain ("Site certificate" / "Intermediate" / "Root" = "CN · from issuer"),
                        // then under the "COMPARED WITH" heading only what differs
                        var all = VisRows(); var hdr = all.FirstOrDefault(o => o.Length > 1 && o[1].StartsWith("COMPARED WITH", StringComparison.Ordinal));
                        var named = res.FirstOrDefault(o => o.Length > 2 && o[1] != "Site" && !Regex.IsMatch(o[1], "^(Site certificate|Intermediate|Root|Next|Names it covers differ|Chain length differs|Compared)"));
                        la = named != null ? named[1] : L.T("this run"); lb = hdr != null ? Or(Regex.Match(hdr[1], "\"([^\"]+)\"").Groups[1].Value, L.T("the saved run")) : L.T("the saved run");
                        cap = "THE CERTIFICATE CHAIN, " + la.ToUpperInvariant() + " vs " + lb.ToUpperInvariant();
                        var diffs = res.Where(o => o.Length > 2 && (o[1].EndsWith("differ", StringComparison.Ordinal) || o[1].EndsWith("differs", StringComparison.Ordinal))).ToList();
                        foreach (var o in res.Where(o => o.Length > 2 && Regex.IsMatch(o[1], "^(Site certificate|Intermediate|Root)$"))) rows.Add(new[] { o[1], Cut(P(o[2], 0), 30), hdr == null ? "?" : diffs.Count == 0 ? "=" : L.T("see below"), "-1" });
                        foreach (var d in diffs) rows.Add(new[] { d[1], Cut(d[2], 40), "", "1" });
                        if (rows.Count == 0) rows.Add(new[] { L.T("chain"), "?", "?", "-1" });
                    }
                    break;
                case "twopc":
                    {
                        // bug-hunt ML-229: MoreTools.TwoPc - "a.txt vs b.txt" = "N rows · N differ", then each differing row "first   |   second"
                        var head = res.FirstOrDefault(o => o.Length > 2 && o[1].Contains(" vs ")); var parts = head != null ? head[1].Split(new[] { " vs " }, StringSplitOptions.None) : null;
                        la = parts != null ? System.IO.Path.GetFileNameWithoutExtension(parts[0]) : System.IO.Path.GetFileNameWithoutExtension(Or(VisIn()[0], "laptop")); lb = parts != null && parts.Length > 1 ? System.IO.Path.GetFileNameWithoutExtension(parts[1]) : System.IO.Path.GetFileNameWithoutExtension(VisIn().Length > 1 && VisIn()[1] != "" ? VisIn()[1] : "desktop"); cap = "TWO PCS, SIDE BY SIDE";
                        foreach (var o in res.Where(o => o.Length > 2 && o[2].Contains("   |   ")).Take(12)) { var v = o[2].Split(new[] { "   |   " }, StringSplitOptions.None); rows.Add(new[] { o[1], Cut(v[0], 30), Cut(v.Length > 1 ? v[1] : "", 30), "1" }); }
                        if (rows.Count == 0 && head != null) rows.Add(new[] { L.T("every row"), "=", "=", "0" });
                    }
                    break;
                default:   // beforeafter
                    {
                        // bug-hunt ML-230: MoreTools.Snapshot - a first run is "Saved \"name\"" and the settings; a second run is "\"before\" -> \"after\"", what changed ("a  ->  b") and the rest "(same)"
                        var saved = res.FirstOrDefault(o => o.Length > 2 && o[1].StartsWith("Saved \"", StringComparison.Ordinal)); var cmp = res.FirstOrDefault(o => o.Length > 2 && o[1].Contains("\" -> \""));
                        if (cmp != null) { var lm = Regex.Matches(cmp[1], "\"([^\"]+)\""); la = lm.Count > 0 ? lm[0].Groups[1].Value : L.T("before"); lb = lm.Count > 1 ? lm[1].Groups[1].Value : L.T("after"); }
                        else { la = saved != null ? Regex.Match(saved[1], "\"([^\"]+)\"").Groups[1].Value : L.T("before"); lb = L.T("after (run it again)"); }
                        cap = "BEFORE AND AFTER - " + la.ToUpperInvariant() + " vs " + lb.ToUpperInvariant();
                        foreach (var o in res.Where(o => o.Length > 2 && o != saved && o != cmp).Take(14))
                        {
                            if (o[2].Contains("  ->  ")) { var v = o[2].Split(new[] { "  ->  " }, StringSplitOptions.None); rows.Add(new[] { o[1], Cut(v[0], 30), Cut(v.Length > 1 ? v[1] : "", 30), "1" }); }
                            else if (o[2].EndsWith(" (same)", StringComparison.Ordinal)) { string v = Cut(o[2].Substring(0, o[2].Length - 7), 30); rows.Add(new[] { o[1], v, v, "0" }); }
                            else if (cmp == null) rows.Add(new[] { o[1], Cut(o[2], 30), "", "-1" });
                        }
                    }
                    break;
            }
            if (rows.Count == 0) rows.Add(new[] { L.T("(run it to compare)"), "", "", "-1" });
            int moreR = rows.Count - 7; rows = rows.Take(7).ToList(); if (moreR > 0) cap += "  ·  +" + moreR + " " + L.T("more");   // L-353
            var r = VisCard(g, x, y, w, 80 * S + rows.Count * 34 * S + 44 * S, cap, st);
            float kx = r.X + 18 * S, ax = r.X + 160 * S, cw = (r.Right - ax - 30 * S) / 2, ty = r.Y + 44 * S;
            Theme.Fill(g, new RectangleF(ax, ty, cw - 8 * S, 24 * S), 6 * S, Theme.Soft); Theme.Fill(g, new RectangleF(ax + cw, ty, cw - 8 * S, 24 * S), 6 * S, Theme.Soft);
            VText(g, Cut(la, 30), Draw.fBodyB, Theme.Ink, ax + 8 * S, ty + 3 * S); VText(g, Cut(lb, 30), Draw.fBodyB, Theme.Ink, ax + cw + 8 * S, ty + 3 * S); ty += 34 * S;
            foreach (var o in rows)
            {
                int lv; if (!int.TryParse(o[3], out lv)) lv = -1; Color c = lv == 2 ? Theme.Red : lv == 1 ? Theme.Amber : Theme.Sub;
                VText(g, Cut(o[0], 22), Draw.fSmall, Theme.Sub, kx, ty + 2 * S);
                if (lv >= 1) Theme.Fill(g, new RectangleF(ax + cw - 4 * S, ty - 2 * S, cw - 4 * S, 24 * S), 5 * S, Color.FromArgb(36, c));
                VText(g, Cut(o[1], 40), Draw.fBody, Theme.Ink, ax + 8 * S, ty); VText(g, Cut(o[2], 40), lv >= 1 ? Draw.fBodyB : Draw.fBody, lv >= 1 ? c : Theme.Ink, ax + cw + 8 * S, ty);
                ty += 34 * S;
            }
            VFoot(g, r, tbSel == "certcmp" ? Or(RV("Compared"), "") : tbSel == "certconv" ? "PEM is the same bytes as DER, written as text (Base64) between BEGIN/END lines - apps just want one or the other." : "Amber is what differs. The rest is the same in both."); return r.Bottom + 14 * S;
        }
    }
}
