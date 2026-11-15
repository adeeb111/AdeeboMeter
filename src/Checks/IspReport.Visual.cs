// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

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

        static string Pages(Visual v, Func<string, string> X)
        {
            var sb = new StringBuilder(); var hops = v.Main.Hops.Where(h => h.Sent > 0).ToList();
            var router = hops.FirstOrDefault(h => h.Lost < h.Sent); var end = hops.LastOrDefault(h => h.Lost < h.Sent);
            var isp = v.FirstIsp >= 0 && v.FirstIsp < v.Main.Hops.Count ? v.Main.Hops[v.FirstIsp] : null; var from = v.Start >= 0 ? v.Main.Hops[v.Start] : null;
            int lvl = from != null ? 2 : 0;

            // ---------------------------------------------------------------- 1. the answer
            sb.Append("<div class=\"tag\">CONNECTION QUALITY REPORT · " + X(DateTime.Now.ToString("ddd d MMM yyyy HH:mm", CultureInfo.CurrentCulture)) + " · " + v.Minutes + "-MINUTE RUN · " + X(v.Main.Label) + "</div>");
            sb.Append("<div class=\"card v" + lvl + "\"><div class=\"cap\">THE ANSWER</div><div class=\"big\">" + X(v.Finding) + "</div>");
            sb.Append("<div class=\"chips\">");
            Action<string, string, string, int> chip = (k, val, sub, l) => sb.Append("<div class=\"chip\" style=\"background:" + (l == 2 ? "#FDE7E9" : l == 1 ? "#FFF4D6" : l == 0 ? "#E6F4EA" : "#F6F8FB") + "\"><span>" + X(k) + "</span><b>" + X(val) + "</b><span>" + X(sub) + "</span></div>");
            chip(L.T("Connection"), v.Wifi ? "Wi-Fi" : L.T("Cable"), v.Wifi ? L.T("a cable test rules Wi-Fi out") : L.T("Wi-Fi not involved"), v.Wifi ? 1 : 0);
            if (router != null) chip(L.T("Home router"), N1(Avg(router)) + " ms", L.T("loss") + " " + N1(Loss(router)) + "%", Loss(router) < 1 ? 0 : 1);
            if (isp != null) chip(L.T("Provider's first hop"), N1(Avg(isp)) + " ms", L.T("loss") + " " + N1(Loss(isp)) + "%" + (from != null && isp.N >= from.N ? " · " + L.T("carries on") : ""), from != null && isp.N >= from.N ? 2 : 0);
            if (from != null) chip(L.T("Loss starts"), L.T("hop") + " " + from.N, v.Nm(from.Ip), 2);
            if (end != null) chip(L.T("Destination"), N1(Avg(end)) + " ms", L.T("loss") + " " + N1(from != null ? Loss(end) : 0) + "%", from != null ? 2 : 0);
            sb.Append("</div>");
            if (v.Ruled.Count > 0) { sb.Append("<div class=\"cap\">ALREADY RULED OUT</div><ul style=\"margin:6px 0 0;padding-left:0;list-style:none\">"); foreach (var r in v.Ruled) sb.Append("<li><span class=\"ok\">✓</span> " + X(r) + "</li>"); sb.Append("</ul>"); }
            sb.Append("</div>");

            // what we ask the provider to look at - written for the desk, from what was found
            var ask = new List<string>();
            if (from != null && isp != null && from.N >= isp.N) ask.Add(L.T("The link at hop") + " " + from.N + " (" + v.Nm(from.Ip) + ") " + L.T("and the hand-off after it - loss starts there and carries on to the destination."));
            var badHours = v.Heat.Where(kv => kv.Value >= 1).Select(kv => kv.Key).OrderBy(k => k).ToList();
            if (badHours.Count > 0) ask.Add(L.T("Load on the node or segment I'm on at") + " " + string.Join(", ", badHours.Select(h => h.ToString("00", Inv) + ":00")) + (badHours.All(h => h >= 17 && h <= 23) ? " - " + L.T("evenings only, which points to congestion") : ""));
            ask.Add(L.T("Errors on my line and port (signal levels, uncorrectable errors, retrains) over the last 7 days."));
            if (v.Wifi) ask.Add(L.T("(I'll repeat the test on a cable if you need it - this run was on Wi-Fi.)"));
            sb.Append("<div class=\"card\"><div class=\"cap\">WHAT WE ASK YOU TO CHECK</div><ol class=\"ask\">" + string.Join("", ask.Select(a => "<li>" + X(a) + "</li>")) + "</ol></div>");

            // ---------------------------------------------------------------- 2. the path, hop by hop
            sb.Append("<div class=\"pb\"></div><div class=\"tag\">THE PATH, HOP BY HOP</div><div class=\"card\">" + PathSvg(v, hops, X) + "<div class=\"cap\">Each row is a router on the way. The bar is the usual time, the thin line reaches the worst one. Loss is red only where it starts and carries on to the destination; a router that just ignores some probes shows amber - that isn't a problem. Bands show whose stretch it is.</div></div>");

            // ---------------------------------------------------------------- the run, probe by probe: router flat + provider spiking = not the home side
            string run = RunSvg(router, isp, end, v.Nm, X);
            if (run != "") sb.Append("<div class=\"tag\">DURING THE TEST, PROBE BY PROBE</div><div class=\"card\">" + run + "<div class=\"cap\">One probe a second to each. Red ticks are probes that got no answer. If the home router's line stays low and flat while the others jump, the home network is not the cause.</div></div>");

            // ---------------------------------------------------------------- 3. when it happens
            sb.Append("<div class=\"tag\">WHEN IT HAPPENS</div>");
            sb.Append("<div class=\"card\"><div class=\"cap\">LOSS BY HOUR OF DAY · LAST 7 DAYS · " + X(v.Game != null ? v.Game.Name : L.T("internet checks")) + "</div>" + HourSvg(v.Heat) + "</div>");
            string grid = WeekSvg(); if (grid != "") sb.Append("<div class=\"card\"><div class=\"cap\">7 DAYS × 24 HOURS · ROUGH MINUTES (LOSS OR NO ANSWER) PER HOUR</div>" + grid + "<div class=\"cap\">Darker = more rough minutes in that hour. A band at the same hours every day is a busy network, not a broken one; random dots are a line or equipment fault.</div></div>");

            // ---------------------------------------------------------------- 4. two routes
            if (v.Routes.Count > 1) sb.Append("<div class=\"card\"><div class=\"cap\">THE SAME MOMENT, TWO ROUTES</div>" + RoutesSvg(v, X) + "<div class=\"cap\">If only one route loses packets, the trouble is on its way out of the provider - the provider can steer traffic another way.</div></div>");

            // ---------------------------------------------------------------- 5. the message
            sb.Append("<div class=\"tag\">THE MESSAGE</div><div class=\"box\">" + X(v.Msg) + "</div>");
            return sb.ToString();
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
        // the hop ladder: zone bands (home / provider / beyond), avg bar + worst whisker, loss % (red only where it carries on)
        static string PathSvg(Visual v, List<PathTools.Hop> hops, Func<string, string> X)
        {
            hops = hops.Take(20).ToList(); int W = 860, rowH = 30, top = 10, H = top + hops.Count * rowH + 30, lx = 300, bw = 380;
            double max = Math.Max(20, hops.Select(h => double.IsNaN(h.Worst) ? Avg(h) : Math.Min(h.Worst, Avg(h) * 4 + 20)).Where(z => !double.IsNaN(z)).DefaultIfEmpty(20).Max() * 1.1);
            var sb = new StringBuilder("<svg viewBox=\"0 0 " + W + " " + H + "\" xmlns=\"http://www.w3.org/2000/svg\" font-family=\"Segoe UI,sans-serif\" font-size=\"12\">");
            string ispOwner = ""; if (v.FirstIsp >= 0 && v.FirstIsp < v.Main.Hops.Count && !v.Owners.TryGetValue(v.Main.Hops[v.FirstIsp].Ip, out ispOwner)) ispOwner = ""; if (ispOwner == null) ispOwner = "";
            for (int i = 0; i < hops.Count; i++)
            {
                var h = hops[i]; int y = top + i * rowH; int idx = v.Main.Hops.IndexOf(h);
                string own; v.Owners.TryGetValue(h.Ip, out own);
                bool home = v.FirstIsp < 0 ? i == 0 : idx < v.FirstIsp, prov = !home && (own == null || ispOwner == "" ? idx < v.FirstIsp + 3 : own == ispOwner);
                string band = home ? "#E6F4EA" : prov ? "#E8F0FE" : "#F1EAFE";
                sb.Append("<rect x=\"0\" y=\"" + y + "\" width=\"" + W + "\" height=\"" + (rowH - 2) + "\" fill=\"" + band + "\" rx=\"4\"/>");
                if (i == 0 || (home != (v.FirstIsp < 0 ? i - 1 == 0 : v.Main.Hops.IndexOf(hops[i - 1]) < v.FirstIsp)))
                    sb.Append("<text x=\"" + (W - 6) + "\" y=\"" + (y + 19) + "\" text-anchor=\"end\" fill=\"#4A5568\" font-weight=\"700\">" + (home ? L.T("YOUR HOME") : L.T("YOUR PROVIDER")) + "</text>");
                else if (!home && !prov && i > 0) { var pi = hops[i - 1]; string po; v.Owners.TryGetValue(pi.Ip, out po); int pidx = v.Main.Hops.IndexOf(pi); bool pprov = !(pidx < v.FirstIsp) && (po == null || ispOwner == "" ? pidx < v.FirstIsp + 3 : po == ispOwner); if (pprov) sb.Append("<text x=\"" + (W - 6) + "\" y=\"" + (y + 19) + "\" text-anchor=\"end\" fill=\"#8250DF\" font-weight=\"700\">" + L.T("BEYOND") + "</text>"); }
                double a = Avg(h), l = Loss(h), wst = h.Worst; bool real = v.Start >= 0 && idx >= v.Start;
                sb.Append("<text x=\"8\" y=\"" + (y + 19) + "\" font-weight=\"700\">" + h.N + "</text><text x=\"32\" y=\"" + (y + 19) + "\">" + X(Cut(v.Nm(h.Ip), 34) + (own != null ? " · " + Cut(own, 22) : "")) + "</text>");
                if (!double.IsNaN(a))
                {
                    double ww = Math.Max(3, bw * Math.Min(1, a / max)); sb.Append("<rect x=\"" + lx + "\" y=\"" + (y + 8) + "\" width=\"" + N0(ww) + "\" height=\"12\" rx=\"3\" fill=\"" + (real ? "#CF222E" : "#1F6FEB") + "\"/>");
                    if (!double.IsNaN(wst) && wst > a) { double wx = lx + bw * Math.Min(1, wst / max); sb.Append("<line x1=\"" + N0(lx + ww) + "\" x2=\"" + N0(wx) + "\" y1=\"" + (y + 14) + "\" y2=\"" + (y + 14) + "\" stroke=\"#6B7685\"/><line x1=\"" + N0(wx) + "\" x2=\"" + N0(wx) + "\" y1=\"" + (y + 9) + "\" y2=\"" + (y + 19) + "\" stroke=\"#6B7685\"/>"); }
                    sb.Append("<text x=\"" + (lx + bw + 10) + "\" y=\"" + (y + 19) + "\">" + N1(a) + " ms</text>");
                }
                else sb.Append("<text x=\"" + lx + "\" y=\"" + (y + 19) + "\" fill=\"#6B7685\">" + L.T("no answer to probes") + "</text>");
                if (!double.IsNaN(l) && l >= 0.5) sb.Append("<text x=\"" + (lx + bw + 80) + "\" y=\"" + (y + 19) + "\" font-weight=\"700\" fill=\"" + (real ? "#CF222E" : "#BF8700") + "\">" + N1(l) + "% " + (real ? L.T("lost") : L.T("ignored")) + "</text>");
            }
            sb.Append("<text x=\"" + lx + "\" y=\"" + (H - 8) + "\" fill=\"#6B7685\">0</text><text x=\"" + (lx + bw) + "\" y=\"" + (H - 8) + "\" text-anchor=\"end\" fill=\"#6B7685\">" + N0(max) + " ms</text></svg>");
            return sb.ToString();
        }
        static string Cut(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n - 1) + "…"; }

        // three lines over the run (router, provider's first hop, destination), lost probes as red ticks along the bottom
        static string RunSvg(PathTools.Hop router, PathTools.Hop isp, PathTools.Hop end, Func<string, string> nm, Func<string, string> X)
        {
            var lines = new List<Tuple<string, List<double>, string>>();
            Func<PathTools.Hop, List<double>> H = h => { if (h == null) return null; lock (h.Hist) return h.Hist.ToList(); };
            if (router != null) lines.Add(Tuple.Create(L.T("home router"), H(router), "#2DA44E"));
            if (isp != null && isp != router) lines.Add(Tuple.Create(L.T("provider, first hop"), H(isp), "#1F6FEB"));
            if (end != null && end != isp && end != router) lines.Add(Tuple.Create(L.T("destination"), H(end), "#8250DF"));
            lines = lines.Where(l => l.Item2 != null && l.Item2.Count > 2).ToList(); if (lines.Count == 0) return "";
            int W = 860, H0 = 220, pad = 40, n = lines.Max(l => l.Item2.Count); var all = lines.SelectMany(l => l.Item2).Where(z => !double.IsNaN(z)).ToList(); if (all.Count == 0) return "";
            var sorted = all.OrderBy(z => z).ToList(); double max = Math.Max(10, sorted[(int)(sorted.Count * 0.98)] * 1.2);
            var sb = new StringBuilder("<svg viewBox=\"0 0 " + W + " " + (H0 + 30 + lines.Count * 16) + "\" xmlns=\"http://www.w3.org/2000/svg\" font-family=\"Segoe UI,sans-serif\" font-size=\"11\">");
            foreach (var g in new[] { 0.5, 1.0 }) { double y = H0 - (H0 - 10) * g; sb.Append("<line x1=\"" + pad + "\" x2=\"" + W + "\" y1=\"" + N0(y) + "\" y2=\"" + N0(y) + "\" stroke=\"#E3E8EF\"/><text x=\"0\" y=\"" + N0(y + 4) + "\" fill=\"#6B7685\">" + N0(max * g) + " ms</text>"); }
            int li = 0;
            foreach (var l in lines)
            {
                var pts = new StringBuilder(); var lost = new StringBuilder();
                for (int i = 0; i < l.Item2.Count; i++)
                {
                    double x = pad + (W - pad) * (double)i / Math.Max(1, n - 1), v = l.Item2[i];
                    if (double.IsNaN(v)) { lost.Append("<line x1=\"" + N0(x) + "\" x2=\"" + N0(x) + "\" y1=\"" + (H0 + 2 + li * 6) + "\" y2=\"" + (H0 + 7 + li * 6) + "\" stroke=\"#CF222E\" stroke-width=\"2\"/>"); if (pts.Length > 0) { sb.Append("<polyline fill=\"none\" stroke=\"" + l.Item3 + "\" stroke-width=\"1.6\" points=\"" + pts + "\"/>"); pts.Clear(); } continue; }
                    pts.Append(N0(x) + "," + N0(H0 - (H0 - 10) * Math.Min(1, v / max)) + " ");
                }
                if (pts.Length > 0) sb.Append("<polyline fill=\"none\" stroke=\"" + l.Item3 + "\" stroke-width=\"1.6\" points=\"" + pts + "\"/>");
                sb.Append(lost); li++;
            }
            int ly = H0 + 26;
            foreach (var l in lines) { int got = l.Item2.Count(z => !double.IsNaN(z)); sb.Append("<rect x=\"" + pad + "\" y=\"" + (ly - 9) + "\" width=\"14\" height=\"8\" rx=\"2\" fill=\"" + l.Item3 + "\"/><text x=\"" + (pad + 20) + "\" y=\"" + ly + "\">" + X(l.Item1) + " · " + (l.Item2.Count - got) + " " + L.T("of") + " " + l.Item2.Count + " " + L.T("probes lost") + "</text>"); ly += 16; }
            sb.Append("</svg>"); return sb.ToString();
        }
        // loss % by hour of day (24 bars), evening hours shaded
        static string HourSvg(Dictionary<int, double> heat)
        {
            if (heat.Count == 0) return "<div class=\"cap\">" + L.T("No history yet - it builds up while AdeeboMeter runs (History on).") + "</div>";
            int W = 860, H = 170, bw = 32; double max = Math.Max(2, heat.Values.Max() * 1.2);
            var sb = new StringBuilder("<svg viewBox=\"0 0 " + W + " " + H + "\" xmlns=\"http://www.w3.org/2000/svg\" font-family=\"Segoe UI,sans-serif\" font-size=\"11\">");
            sb.Append("<rect x=\"" + (30 + 17 * (bw + 3)) + "\" y=\"0\" width=\"" + 7 * (bw + 3) + "\" height=\"" + (H - 20) + "\" fill=\"#FFF4D6\" rx=\"4\"/><text x=\"" + (32 + 17 * (bw + 3)) + "\" y=\"12\" fill=\"#BF8700\">" + L.T("evening") + "</text>");
            for (int hr = 0; hr < 24; hr++)
            {
                double v; heat.TryGetValue(hr, out v); double bh = (H - 40) * Math.Min(1, v / max); int x = 30 + hr * (bw + 3);
                sb.Append("<rect x=\"" + x + "\" y=\"" + N0(H - 20 - bh) + "\" width=\"" + bw + "\" height=\"" + N0(Math.Max(1, bh)) + "\" rx=\"3\" fill=\"" + (v >= 3 ? "#CF222E" : v >= 1 ? "#BF8700" : "#2DA44E") + "\"/>");
                if (hr % 3 == 0) sb.Append("<text x=\"" + (x + bw / 2) + "\" y=\"" + (H - 6) + "\" text-anchor=\"middle\" fill=\"#6B7685\">" + hr.ToString("00", Inv) + "</text>");
                if (v >= 1) sb.Append("<text x=\"" + (x + bw / 2) + "\" y=\"" + N0(H - 24 - bh) + "\" text-anchor=\"middle\" fill=\"#1B2330\">" + N1(v) + "%</text>");
            }
            sb.Append("<text x=\"0\" y=\"" + (H - 20) + "\" fill=\"#6B7685\">0%</text></svg>");
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
            int W = 860, cw = 32, ch = 22, H = 7 * ch + 26; int mx = 1; foreach (var c in cells) mx = Math.Max(mx, c);
            var sb = new StringBuilder("<svg viewBox=\"0 0 " + W + " " + H + "\" xmlns=\"http://www.w3.org/2000/svg\" font-family=\"Segoe UI,sans-serif\" font-size=\"11\">");
            for (int d = 0; d < 7; d++)
            {
                sb.Append("<text x=\"0\" y=\"" + (d * ch + 15) + "\" fill=\"#4A5568\">" + DateTime.Today.AddDays(-6 + d).ToString("ddd", CultureInfo.CurrentCulture) + "</text>");
                for (int hr = 0; hr < 24; hr++) { int c = cells[d, hr]; double t = c == 0 ? 0 : 0.15 + 0.85 * c / mx; sb.Append("<rect x=\"" + (40 + hr * (cw + 2)) + "\" y=\"" + (d * ch) + "\" width=\"" + cw + "\" height=\"" + (ch - 3) + "\" rx=\"3\" fill=\"" + (c == 0 ? "#EEF1F5" : "rgba(207,34,46," + t.ToString("0.00", Inv) + ")") + "\"><title>" + c + " rough minutes</title></rect>"); }
            }
            for (int hr = 0; hr < 24; hr += 3) sb.Append("<text x=\"" + (40 + hr * (cw + 2) + cw / 2) + "\" y=\"" + (H - 4) + "\" text-anchor=\"middle\" fill=\"#6B7685\">" + hr.ToString("00", Inv) + ":00</text>");
            sb.Append("</svg>"); return sb.ToString();
        }
        // two routes, same moment: the last answering hop's time and loss side by side
        static string RoutesSvg(Visual v, Func<string, string> X)
        {
            int W = 860, H = 30 + v.Routes.Count * 46; double max = 20;
            var ends = v.Routes.Select(r => new { r, e = r.Hops.LastOrDefault(h => h.Sent > 0 && h.Lost < h.Sent), real = StartOfLoss(r.Hops) >= 0 }).ToList();
            foreach (var z in ends) if (z.e != null && !double.IsNaN(Avg(z.e))) max = Math.Max(max, Avg(z.e) * 1.2);
            var sb = new StringBuilder("<svg viewBox=\"0 0 " + W + " " + H + "\" xmlns=\"http://www.w3.org/2000/svg\" font-family=\"Segoe UI,sans-serif\" font-size=\"12\">");
            int y = 10;
            foreach (var z in ends)
            {
                sb.Append("<text x=\"0\" y=\"" + (y + 16) + "\" font-weight=\"700\">" + X(Cut(z.r.Label, 36)) + "</text>");
                if (z.e != null) { double a = Avg(z.e), l = z.real ? Loss(z.e) : 0; sb.Append("<rect x=\"300\" y=\"" + (y + 4) + "\" width=\"" + N0(Math.Max(3, 380 * Math.Min(1, a / max))) + "\" height=\"16\" rx=\"4\" fill=\"" + (z.real ? "#CF222E" : "#1F6FEB") + "\"/><text x=\"690\" y=\"" + (y + 16) + "\">" + N1(a) + " ms · " + L.T("loss") + " " + N1(l) + "%</text>"); }
                else sb.Append("<text x=\"300\" y=\"" + (y + 16) + "\" fill=\"#6B7685\">" + L.T("doesn't answer ping") + "</text>");
                y += 46;
            }
            sb.Append("</svg>"); return sb.ToString();
        }
    }
}
