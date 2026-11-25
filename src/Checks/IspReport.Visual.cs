// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

// review 1.1.6 P2 #14: the report is for a provider's support desk - one language (English) throughout, never mixed with the app's
namespace AdeeboMeter
{
    // ================================================================== Report for my ISP: the pictures (Adeeb, 15 Nov - "a former ISP support guy:
    // what we send means nothing if we can't show it in a human way"). Pages in front of the full table, each answering what a
    // support desk asks in the order it asks: the answer and what's ruled out, the path hop by hop (whose stretch, where loss
    // starts and carries on), when it happens (hour of day, and 7 days x 24 hours from History), two routes side by side, what we
    // ask them to check, and the message. Inline SVG - the file stays one self-contained page; Print > Save as PDF keeps the pages.
    static partial class IspReport
    {
        sealed class Visual
        {
            public Route Main; public List<Route> Routes; public int Start, FirstIsp, GwIdx, Minutes; public Func<string, string> Nm; public Dictionary<string, string> Owners;
            public Dictionary<int, double> Heat; public bool Wifi; public string Finding = "", Msg = ""; public List<string> Ruled; public Game Game;
        }
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string N0(double v) { return double.IsNaN(v) ? "-" : Math.Round(v).ToString(Inv); }
        static string N1(double v) { return double.IsNaN(v) ? "-" : v.ToString(v < 10 ? "0.0" : "0", Inv); }

        // ---- the look (24 Nov, Adeeb: "we will have to enhance the ISP report"): the website's and the widget's - the logo's blue
        // and the gauge's three colours darkened for paper, sentence-case headings, the same five stops the widget shows. Light on
        // purpose: support desks print it or save it as a PDF.
        const string Ink = "#16191E", Sub = "#4B5360", Mut = "#5A6270", Line = "#DDE1E7", Soft = "#F1F3F6", Face = "#1155E0", Ok = "#17794F", Warn = "#875A00", Bad = "#B42318";
        const string Font = "Segoe UI Variable Text,Segoe UI,system-ui,sans-serif";
        static string Svg(int w, int h, int fs) { return "<svg viewBox=\"0 0 " + w + " " + h + "\" xmlns=\"http://www.w3.org/2000/svg\" font-family=\"" + Font + "\" font-size=\"" + fs + "\" fill=\"" + Ink + "\">"; }
        internal const string Logo = "<svg class=\"logo\" viewBox=\"0 0 64 64\" width=\"28\" height=\"28\" aria-hidden=\"true\"><rect x=\"4\" y=\"4\" width=\"56\" height=\"56\" rx=\"14\" fill=\"#1155E0\"/><g fill=\"none\" stroke-width=\"5\" stroke-linecap=\"round\"><path d=\"M16.04 32.48A16 16 0 0 1 21.29 21.71\" stroke=\"#5EF2B0\"/><path d=\"M26.01 18.77A16 16 0 0 1 37.99 18.77\" stroke=\"#FCD34D\"/><path d=\"M42.71 21.71A16 16 0 0 1 47.96 32.48\" stroke=\"#FF7A6B\"/></g><line x1=\"32\" y1=\"33.6\" x2=\"40.52\" y2=\"21.87\" stroke=\"#fff\" stroke-width=\"1.6\" stroke-linecap=\"round\"/><circle cx=\"32\" cy=\"33.6\" r=\"2.6\" fill=\"#fff\"/></svg>";
        // a heading from the data ("HOP BY HOP") in sentence case, the network words kept as they're written - never translated, the report is English
        internal static string Sc(string t)
        {
            if (string.IsNullOrEmpty(t) || t.Any(char.IsLower)) return t;
            var keep = new[] { "ISP", "DNS", "TCP", "UDP", "IP", "IPv4", "IPv6", "MTU", "VPN", "Wi-Fi", "CGNAT", "TLS", "MTR", "NAT" };
            var words = t.ToLower(Inv).Split(' ');
            for (int i = 0; i < words.Length; i++) { var k = keep.FirstOrDefault(z => string.Equals(z, words[i], StringComparison.OrdinalIgnoreCase)); if (k != null) words[i] = k; else if (i == 0 && words[i].Length > 0) words[i] = char.ToUpper(words[i][0], Inv) + words[i].Substring(1); }
            return string.Join(" ", words);
        }

        static string Pages(Visual v, Func<string, string> X)
        {
            var sb = new StringBuilder(); var hops = v.Main.Hops.Where(h => h.Sent > 0).ToList();
            // carry-over (100-bug): the router chip is the router - the gateway's own hop, or else an answering hop before the provider's first one; never just the first hop that answered
            var router = v.GwIdx >= 0 && v.GwIdx < v.Main.Hops.Count && v.Main.Hops[v.GwIdx].Sent > 0 ? v.Main.Hops[v.GwIdx]
                : v.Main.Hops.Take(v.FirstIsp >= 0 ? v.FirstIsp : 0).FirstOrDefault(h => h.Sent > 0 && h.Lost < h.Sent);
            var end = hops.LastOrDefault(h => h.Lost < h.Sent);
            var isp = v.FirstIsp >= 0 && v.FirstIsp < v.Main.Hops.Count ? v.Main.Hops[v.FirstIsp] : null; var from = v.Start >= 0 ? v.Main.Hops[v.Start] : null;
            // bug-hunt ML-96/97: the destination's own loss, always - and named for what it is when the destination itself never answered
            bool endSilent = end != null && hops.Count > 0 && !ReferenceEquals(hops[hops.Count - 1], end);

            // ---------------------------------------------------------------- 1. the answer, and the five stops
            sb.Append("<header class=\"top\">" + Logo + "<div><h1>Connection quality report</h1><p class=\"meta\">" + X(DateTime.Now.ToString("ddd d MMM yyyy, HH:mm", CultureInfo.CurrentCulture)) + " · " + v.Minutes + "-minute run · " + X(v.Main.Label) + " · made with AdeeboMeter</p></div></header>");
            sb.Append("<section class=\"answer\"><p class=\"big\">" + X(v.Finding) + "</p>");
            // the stops: 0 fine, 1 worth a look, 2 where the problem is, -1 not seen
            int rl = router == null ? -1 : Loss(router) < 1 ? 0 : from != null && router.N >= from.N ? 2 : 1;
            bool lossInIsp = from != null && isp != null && from.N >= isp.N && !(v.Owners.ContainsKey(from.Ip) && v.Owners.ContainsKey(isp.Ip) && v.Owners[from.Ip] != v.Owners[isp.Ip]) && v.Main.Hops.IndexOf(from) < v.FirstIsp + 3;
            int il = isp == null ? -1 : lossInIsp ? 2 : 0;
            int el = end == null ? -1 : from != null ? 2 : Loss(end) >= 1 ? 1 : 0;
            var stops = new List<Tuple<string, string, string, int>> {
                Tuple.Create("This PC", "measured here", "", 0),
                Tuple.Create(v.Wifi ? "Wi-Fi" : "Cable", v.Wifi ? "a cable test rules it out" : "Wi-Fi not involved", "", v.Wifi ? 1 : 0),
                Tuple.Create("Home router", router == null ? "not seen" : N1(Avg(router)) + " ms", router == null ? "" : "loss " + N1(Loss(router)) + "%" + (rl == 1 ? " · ignores probes" : ""), rl),
                Tuple.Create("Provider", isp == null ? "not seen" : lossInIsp ? "loss starts, hop " + from.N : N1(Avg(isp)) + " ms", isp == null ? "" : lossInIsp ? v.Nm(from.Ip) : "first hop, loss " + N1(Loss(isp)) + "%", il),
                Tuple.Create(endSilent ? "Last answer" : "Destination", end == null ? "no answer" : N1(Avg(end)) + " ms", end == null ? "" : "loss " + N1(Loss(end)) + "%" + (from != null && !lossInIsp ? " · from hop " + from.N : ""), el) };
            sb.Append(StopsSvg(stops, X) + "</section>");
            if (v.Ruled.Count > 0) { sb.Append("<section><h2>Already ruled out</h2><ul class=\"ruled\">"); foreach (var r in v.Ruled) sb.Append("<li>" + Tick + X(r) + "</li>"); sb.Append("</ul></section>"); }

            // what we ask the provider to look at - written for the desk, from what was found
            var ask = new List<string>();
            if (from != null && isp != null && from.N >= isp.N) ask.Add(("The link at hop") + " " + from.N + " (" + v.Nm(from.Ip) + ") " + ("and the hand-off after it - loss starts there and carries on to the destination."));
            var badHours = v.Heat.Where(kv => kv.Value >= 1).Select(kv => kv.Key).OrderBy(k => k).ToList();
            if (badHours.Count > 0) ask.Add(("Load on the node or segment I'm on at") + " " + string.Join(", ", badHours.Select(h => h.ToString("00", Inv) + ":00")) + (badHours.All(h => h >= 17 && h <= 23) ? " - " + ("evenings only, which points to congestion") : ""));
            ask.Add(("Errors on my line and port (signal levels, uncorrectable errors, retrains) over the last 7 days."));
            if (v.Wifi) ask.Add(("(I'll repeat the test on a cable if you need it - this run was on Wi-Fi.)"));
            sb.Append("<section><h2>What we ask you to check</h2><ol class=\"ask\">" + string.Join("", ask.Select(a => "<li>" + X(a) + "</li>")) + "</ol></section>");
            sb.Append("<section><h2>The message</h2><div class=\"msg\">" + X(v.Msg) + "</div></section>");

            // ---------------------------------------------------------------- 2. the path, hop by hop
            sb.Append("<div class=\"pb\"></div><section><h2>The path, hop by hop</h2><p class=\"cap\">Each row is a router on the way. The bar is the usual time; the thin line reaches the worst one. Loss is red only where it starts and carries on to the destination. A router that just ignores some probes shows amber - that isn't a problem.</p>" + PathSvg(v, hops, X) + "</section>");

            // ---------------------------------------------------------------- the run, probe by probe: which probes got no answer, stop by stop
            string run = RunSvg(router, isp, end, v.Nm, X);
            if (run != "") sb.Append("<section><h2>During the test</h2><p class=\"cap\">One probe a second to each stop. Each mark is one probe: grey answered, red got no answer. If the home router's row stays clean while the others lose probes, the home network is not the cause.</p>" + run + "</section>");

            // ---------------------------------------------------------------- 3. when it happens
            sb.Append("<section><h2>When it happens</h2><p class=\"cap\">Loss by hour of day over the last 7 days, from " + X(v.Game != null ? v.Game.Name : ("the internet checks")) + ".</p>" + HourSvg(v.Heat));
            string grid = WeekSvg(); if (grid != "") sb.Append("<h3>Each day, hour by hour</h3><p class=\"cap\">Rough minutes (loss or no answer) in each hour. A band at the same hours every day is a busy network, not a broken one; scattered marks point to a line or equipment fault.</p>" + grid);
            sb.Append("</section>");

            // ---------------------------------------------------------------- 4. two routes
            if (v.Routes.Count > 1) sb.Append("<section><h2>The same moment, two routes</h2><p class=\"cap\">If only one route loses packets, the trouble is on its way out of the provider - the provider can steer traffic another way.</p>" + RoutesSvg(v, X) + "</section>");
            return sb.ToString();
        }
        const string Tick = "<svg viewBox=\"0 0 16 16\" width=\"16\" height=\"16\" aria-hidden=\"true\"><path d=\"M3 8.5l3 3 7-7\" fill=\"none\" stroke=\"#17794F\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>";
        static string Lc(int l) { return l == 2 ? Bad : l == 1 ? Warn : l == 0 ? Ok : "#B8BEC7"; }
        // the five stops, as the widget and the website draw them: a dot per stop on one line, the problem stop in red
        static string StopsSvg(List<Tuple<string, string, string, int>> stops, Func<string, string> X)
        {
            int W = 860, H = 104, n = stops.Count; double step = (W - 120) / (double)(n - 1);
            var sb = new StringBuilder(Svg(W, H, 13));
            sb.Append("<line x1=\"60\" x2=\"" + (W - 60) + "\" y1=\"14\" y2=\"14\" stroke=\"" + Line + "\" stroke-width=\"2\"/>");
            for (int i = 0; i < n; i++)
            {
                var s = stops[i]; double x = 60 + step * i; string c = Lc(s.Item4);
                sb.Append("<circle cx=\"" + N0(x) + "\" cy=\"14\" r=\"" + (s.Item4 == 2 ? 9 : 7) + "\" fill=\"" + (s.Item4 < 0 ? "#fff" : c) + "\" stroke=\"" + c + "\" stroke-width=\"2\"/>");
                sb.Append("<text x=\"" + N0(x) + "\" y=\"46\" text-anchor=\"middle\" font-weight=\"600\" fill=\"" + (s.Item4 < 0 ? Mut : Ink) + "\">" + X(s.Item1) + "</text>");
                sb.Append("<text x=\"" + N0(x) + "\" y=\"66\" text-anchor=\"middle\" font-weight=\"600\" fill=\"" + (s.Item4 == 2 ? Bad : s.Item4 == 1 ? Warn : Sub) + "\">" + X(Cut(s.Item2, 24)) + "</text>");
                if (s.Item3 != "") sb.Append("<text x=\"" + N0(x) + "\" y=\"84\" text-anchor=\"middle\" font-size=\"12\" fill=\"" + Mut + "\">" + X(Cut(s.Item3, 28)) + "</text>");
            }
            sb.Append("</svg>"); return sb.ToString();
        }

        // self-test: a made-up evening problem (loss from the provider's 2nd hop on, worse 20-23h) written as a report - the pages must draw
        internal static string Sample()
        {
            var rnd = new Random(7); var hops = new List<PathTools.Hop>();
            string[] ips = { "192.168.1.1", "198.51.100.1", "198.51.100.9", "198.51.100.40", "203.0.113.1", "203.0.113.10" }; double[] base0 = { 2, 9, 11, 18, 20, 22 };
            for (int i = 0; i < ips.Length; i++)
            {
                var h = new PathTools.Hop { N = i == 0 ? 1 : i + 1, Ip = ips[i], Reached = i == ips.Length - 1 };
                for (int k = 0; k < 120; k++) { bool lost = i >= 2 && rnd.NextDouble() < 0.04; double ms = base0[i] + rnd.NextDouble() * (i >= 2 ? 25 : 2); h.Sent++; if (lost) { h.Lost++; h.Hist.Add(double.NaN); } else { h.Sum += ms; h.Hist.Add(ms); if (double.IsNaN(h.Worst) || ms > h.Worst) h.Worst = ms; } }
                hops.Add(h);
            }
            var main = new Route { Label = "example.com", Host = "example.com", Hops = hops };
            var second = new Route { Label = "Reference (1.1.1.1)", Host = "1.1.1.1", Hops = hops.Take(2).ToList() };
            var heat = Enumerable.Range(0, 24).ToDictionary(hr => hr, hr => hr >= 20 && hr <= 22 ? 2.5 : 0.2);
            var v = new Visual { Main = main, Routes = new List<Route> { main, second }, Start = 2, FirstIsp = 1, GwIdx = 0, Nm = ip => ip, Owners = new Dictionary<string, string>(), Heat = heat, Wifi = false,
                Finding = "Loss starts at hop 3 (198.51.100.9), inside the provider's network, and carries through to the destination. The home network was clean.", Ruled = new List<string> { "the home network - the router answered every probe, in 3 ms", "Wi-Fi - measured on a cable" }, Msg = "Hi - I see packet loss in the evenings (20:00-23:00). ...", Minutes = 2 };
            var rows = new List<string[]> { R(2, "Finding", v.Finding, "", "m"), R(-1, "HOP BY HOP", "", "", "m"), R(0, "Hop 1", "192.168.1.1 · avg 3 ms", "", "m") };
            return Save(rows, "", v);
        }
        // the hop ladder: whose stretch each hop is (home / provider / beyond), avg bar + worst whisker, loss % (red only where it carries on)
        static string PathSvg(Visual v, List<PathTools.Hop> hops, Func<string, string> X)
        {
            hops = hops.Take(20).ToList(); int W = 860, rowH = 32,   // R2-16: room for "12.5% lost" and the zone name side by side
            top = 4, H = top + hops.Count * rowH + 26, lx = 330, bw = 300;
            double max = Math.Max(20, hops.Select(h => double.IsNaN(h.Worst) ? Avg(h) : Math.Min(h.Worst, Avg(h) * 4 + 20)).Where(z => !double.IsNaN(z)).DefaultIfEmpty(20).Max() * 1.1);
            var sb = new StringBuilder(Svg(W, H, 13));
            string ispOwner = ""; if (v.FirstIsp >= 0 && v.FirstIsp < v.Main.Hops.Count && !v.Owners.TryGetValue(v.Main.Hops[v.FirstIsp].Ip, out ispOwner)) ispOwner = ""; if (ispOwner == null) ispOwner = "";
            string lastZone = "";
            for (int i = 0; i < hops.Count; i++)
            {
                var h = hops[i]; int y = top + i * rowH; int idx = v.Main.Hops.IndexOf(h);
                string own; v.Owners.TryGetValue(h.Ip, out own);
                bool home = v.FirstIsp < 0 ? i == 0 : idx < v.FirstIsp, prov = !home && (own == null || ispOwner == "" ? idx < v.FirstIsp + 3 : own == ispOwner);
                string zone = home ? "Your home" : prov ? "Your provider" : "Beyond";
                if (i % 2 == 0) sb.Append("<rect x=\"0\" y=\"" + y + "\" width=\"" + W + "\" height=\"" + rowH + "\" fill=\"" + Soft + "\" rx=\"6\"/>");
                if (zone != lastZone) { if (i > 0) sb.Append("<line x1=\"0\" x2=\"" + W + "\" y1=\"" + y + "\" y2=\"" + y + "\" stroke=\"" + Sub + "\" stroke-dasharray=\"3 3\"/>"); sb.Append("<text x=\"" + (W - 8) + "\" y=\"" + (y + 21) + "\" text-anchor=\"end\" font-weight=\"600\" fill=\"" + Sub + "\">" + zone + "</text>"); lastZone = zone; }
                double a = Avg(h), l = Loss(h), wst = h.Worst; bool real = v.Start >= 0 && idx >= v.Start;
                sb.Append("<text x=\"10\" y=\"" + (y + 21) + "\" font-weight=\"600\" fill=\"" + Mut + "\">" + h.N + "</text><text x=\"36\" y=\"" + (y + 21) + "\">" + X(Cut(v.Nm(h.Ip), 30) + (own != null ? " · " + Cut(own, 18) : "")) + "</text>");
                if (!double.IsNaN(a))
                {
                    double ww = Math.Max(3, bw * Math.Min(1, a / max)); sb.Append("<rect x=\"" + lx + "\" y=\"" + (y + 10) + "\" width=\"" + N0(ww) + "\" height=\"12\" rx=\"3\" fill=\"" + (real ? Bad : Face) + "\"/>");
                    if (!double.IsNaN(wst) && wst > a) { double wx = lx + bw * Math.Min(1, wst / max); sb.Append("<line x1=\"" + N0(lx + ww) + "\" x2=\"" + N0(wx) + "\" y1=\"" + (y + 16) + "\" y2=\"" + (y + 16) + "\" stroke=\"" + Mut + "\"/><line x1=\"" + N0(wx) + "\" x2=\"" + N0(wx) + "\" y1=\"" + (y + 11) + "\" y2=\"" + (y + 21) + "\" stroke=\"" + Mut + "\"/>"); }
                    sb.Append("<text x=\"" + (lx + bw + 12) + "\" y=\"" + (y + 21) + "\">" + N1(a) + " ms</text>");
                }
                else sb.Append("<text x=\"" + lx + "\" y=\"" + (y + 21) + "\" fill=\"" + Mut + "\">" + ("no answer to probes") + "</text>");
                if (!double.IsNaN(l) && l >= 0.5) sb.Append("<text x=\"" + (lx + bw + 80) + "\" y=\"" + (y + 21) + "\" font-weight=\"600\" fill=\"" + (real ? Bad : Warn) + "\">" + N1(l) + "% " + (real ? ("lost") : ("ignored")) + "</text>");
            }
            sb.Append("<text x=\"" + lx + "\" y=\"" + (H - 6) + "\" font-size=\"12\" fill=\"" + Mut + "\">0</text><text x=\"" + (lx + bw) + "\" y=\"" + (H - 6) + "\" text-anchor=\"end\" font-size=\"12\" fill=\"" + Mut + "\">" + N0(max) + " ms</text></svg>");
            return sb.ToString();
        }
        static string Cut(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n - 1) + "…"; }

        // the run, probe by probe: one row per stop (router, provider's first hop, destination), one mark per probe - grey answered,
        // red no answer. Reads at a glance where probes go missing; the old three wobbling lines were hard to read (24 Nov review)
        static string RunSvg(PathTools.Hop router, PathTools.Hop isp, PathTools.Hop end, Func<string, string> nm, Func<string, string> X)
        {
            var rows = new List<Tuple<string, List<double>>>();
            Func<PathTools.Hop, List<double>> H = h => { if (h == null) return null; lock (h.Hist) return h.Hist.ToList(); };
            if (router != null) rows.Add(Tuple.Create(("Home router"), H(router)));
            if (isp != null && isp != router) rows.Add(Tuple.Create(("Provider, first hop"), H(isp)));
            if (end != null && end != isp && end != router) rows.Add(Tuple.Create(("Destination"), H(end)));
            rows = rows.Where(r => r.Item2 != null && r.Item2.Count > 2).ToList(); if (rows.Count == 0) return "";
            int W = 860, lx = 170, rx = 150, rowH = 34, n = rows.Max(r => r.Item2.Count); double cw = (W - lx - rx) / (double)n;
            var sb = new StringBuilder(Svg(W, rows.Count * rowH + 4, 13)); int y = 4;
            foreach (var r in rows)
            {
                int lost = r.Item2.Count(z => double.IsNaN(z));
                sb.Append("<text x=\"0\" y=\"" + (y + 18) + "\" font-weight=\"600\">" + X(r.Item1) + "</text>");
                for (int i = 0; i < r.Item2.Count; i++) { bool miss = double.IsNaN(r.Item2[i]); sb.Append("<rect x=\"" + (lx + cw * i).ToString("0.#", Inv) + "\" y=\"" + (y + (miss ? 2 : 8)) + "\" width=\"" + Math.Max(1, cw - 1).ToString("0.#", Inv) + "\" height=\"" + (miss ? 20 : 8) + "\" rx=\"1\" fill=\"" + (miss ? Bad : "#C9CED6") + "\"/>"); }
                sb.Append("<text x=\"" + (W - rx + 14) + "\" y=\"" + (y + 18) + "\" fill=\"" + (lost > 0 ? Bad : Sub) + "\" font-weight=\"" + (lost > 0 ? "600" : "500") + "\">" + lost + " of " + r.Item2.Count + " lost</text>");
                y += rowH;
            }
            sb.Append("</svg>"); return sb.ToString();
        }
        // loss % by hour of day (24 bars), the evening hours shaded; a value is written once over a run of equal bars
        static string HourSvg(Dictionary<int, double> heat)
        {
            if (heat.Count == 0) return "<p class=\"cap\">" + ("No history yet - it builds up while AdeeboMeter runs (History on).") + "</p>";
            int W = 860, H = 176, bw = 30, x0 = 40; double max = Math.Max(2, heat.Values.Max() * 1.2);
            var sb = new StringBuilder(Svg(W, H, 12));
            sb.Append("<rect x=\"" + (x0 - 2 + 17 * (bw + 4)) + "\" y=\"0\" width=\"" + 7 * (bw + 4) + "\" height=\"" + (H - 22) + "\" fill=\"" + Soft + "\" rx=\"6\"/><text x=\"" + (x0 + 4 + 17 * (bw + 4)) + "\" y=\"16\" fill=\"" + Sub + "\">" + ("Evening") + "</text>");
            sb.Append("<line x1=\"" + x0 + "\" x2=\"" + W + "\" y1=\"" + (H - 22) + "\" y2=\"" + (H - 22) + "\" stroke=\"" + Line + "\"/>");
            double prev = double.NaN;
            for (int hr = 0; hr < 24; hr++)
            {
                double v; heat.TryGetValue(hr, out v); double bh = (H - 50) * Math.Min(1, v / max); int x = x0 + hr * (bw + 4);
                sb.Append("<rect x=\"" + x + "\" y=\"" + N0(H - 22 - Math.Max(2, bh)) + "\" width=\"" + bw + "\" height=\"" + N0(Math.Max(2, bh)) + "\" rx=\"3\" fill=\"" + (v >= 3 ? Bad : v >= 1 ? Warn : "#A9D8BF") + "\"/>");
                if (hr % 3 == 0) sb.Append("<text x=\"" + (x + bw / 2) + "\" y=\"" + (H - 6) + "\" text-anchor=\"middle\" fill=\"" + Mut + "\">" + hr.ToString("00", Inv) + ":00</text>");
                if (v >= 1 && N1(v) != N1(prev)) sb.Append("<text x=\"" + (x + bw / 2) + "\" y=\"" + N0(H - 28 - bh) + "\" text-anchor=\"middle\" font-weight=\"600\">" + N1(v) + "%</text>");
                prev = v >= 1 ? v : double.NaN;
            }
            sb.Append("<text x=\"0\" y=\"" + (H - 18) + "\" fill=\"" + Mut + "\">0%</text></svg>");
            return sb.ToString();
        }
        // 7 days x 24 hours of rough minutes (loss >= 2% or no internet answer), from History's minutes
        static string WeekSvg()
        {
            if (!Cfg.History) return "";
            var cells = new int[7, 24]; bool any = false;
            for (int d = 0; d < 7; d++)
            {
                History.Day day; try { day = History.Load(DateTime.Today.AddDays(-6 + d)); } catch (Exception e) { if (e is OutOfMemoryException) throw; continue; }
                if (day == null) continue;
                foreach (var row in day.M) { if (row.Length < 12) continue; int hr = Math.Max(0, Math.Min(23, (int)(row[0] / 3600))); any = true; if ((!double.IsNaN(row[11]) && row[11] >= 2) || double.IsNaN(row[6])) cells[d, hr]++; }
            }
            if (!any) return "";
            int W = 860, cw = 30, ch = 22, x0 = 46, H = 7 * ch + 26; int mx = 1; foreach (var c in cells) mx = Math.Max(mx, c);
            var sb = new StringBuilder(Svg(W, H, 12));
            for (int d = 0; d < 7; d++)
            {
                sb.Append("<text x=\"0\" y=\"" + (d * ch + 15) + "\" fill=\"" + Sub + "\">" + DateTime.Today.AddDays(-6 + d).ToString("ddd", CultureInfo.InvariantCulture) + "</text>");
                for (int hr = 0; hr < 24; hr++) { int c = cells[d, hr]; double t = c == 0 ? 0 : 0.2 + 0.8 * c / mx; sb.Append("<rect x=\"" + (x0 + hr * (cw + 4)) + "\" y=\"" + (d * ch) + "\" width=\"" + cw + "\" height=\"" + (ch - 4) + "\" rx=\"3\" fill=\"" + (c == 0 ? Soft : "rgba(180,35,24," + t.ToString("0.00", Inv) + ")") + "\"><title>" + c + " rough minutes</title></rect>"); }
            }
            for (int hr = 0; hr < 24; hr += 3) sb.Append("<text x=\"" + (x0 + hr * (cw + 4) + cw / 2) + "\" y=\"" + (H - 4) + "\" text-anchor=\"middle\" fill=\"" + Mut + "\">" + hr.ToString("00", Inv) + ":00</text>");
            sb.Append("</svg>"); return sb.ToString();
        }
        // two routes, same moment: the last answering hop's time and loss side by side
        static string RoutesSvg(Visual v, Func<string, string> X)
        {
            int W = 860, H = 8 + v.Routes.Count * 40; double max = 20;
            var ends = v.Routes.Select(r => new { r, e = r.Hops.LastOrDefault(h => h.Sent > 0 && h.Lost < h.Sent), real = StartOfLoss(r.Hops) >= 0 }).ToList();
            foreach (var z in ends) if (z.e != null && !double.IsNaN(Avg(z.e))) max = Math.Max(max, Avg(z.e) * 1.2);
            var sb = new StringBuilder(Svg(W, H, 13));
            int y = 4;
            foreach (var z in ends)
            {
                sb.Append("<text x=\"0\" y=\"" + (y + 18) + "\" font-weight=\"600\">" + X(Cut(z.r.Label, 30)) + "</text>");
                if (z.e != null) { double a = Avg(z.e), l = Loss(z.e);   /* bug-hunt ML-96: the loss measured, never a 0 */ sb.Append("<rect x=\"260\" y=\"" + (y + 6) + "\" width=\"" + N0(Math.Max(3, 380 * Math.Min(1, a / max))) + "\" height=\"16\" rx=\"4\" fill=\"" + (z.real ? Bad : Face) + "\"/><text x=\"660\" y=\"" + (y + 18) + "\"" + (z.real ? " fill=\"" + Bad + "\" font-weight=\"600\"" : "") + ">" + N1(a) + " ms · " + ("loss") + " " + N1(l) + "%</text>"); }
                else sb.Append("<text x=\"260\" y=\"" + (y + 18) + "\" fill=\"" + Mut + "\">" + ("doesn't answer ping") + "</text>");
                y += 40;
            }
            sb.Append("</svg>"); return sb.ToString();
        }
    }
}
