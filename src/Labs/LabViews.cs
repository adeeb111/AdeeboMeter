// AdeeboLab views (1.1.6): drawings the app makes from what it already knows, handed to the lab page when it opens -
// See my LAN (the kept device list), Path to anywhere (the last trace or MTR), Wi-Fi around you (the last Wi-Fi list),
// Phone vs this PC, and VPN split. Nothing new is sent to build them: every number is one the app measured already
// (See my LAN can ask the devices again when started from AdeeboShell: "lab lan --ask").
// The views ride inside the lab page the app writes to its data folder (LabLink.PagePath) - not in the address,
// which has a size limit; the page reads them from window data, never from the network.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;

namespace AdeeboMeter
{
    static class LabViews
    {
        public const int LanMax = 50;   // drawn devices; the rest are one "+N more" box (a 1000-device network is a list, not a drawing)

        sealed class N { public string Id, K, Name, Sub = "", Ms = "", St = "", Info = ""; public int X, Y; }
        sealed class Ln { public string A, B, Kind = "cable", Label = ""; }
        sealed class Ar { public int X, Y, W, H; public string T; }
        sealed class Tx { public int X, Y; public string T; }
        sealed class D { public string Name, Note = ""; public List<N> Nodes = new List<N>(); public List<Ln> Links = new List<Ln>(); public List<Ar> Areas = new List<Ar>(); public List<Tx> Texts = new List<Tx>(); }

        static string Str(string v)
        {
            var sb = new StringBuilder("\""); string s = v ?? ""; if (s.Length > 400) s = s.Substring(0, 400);
            foreach (char c in s)
                if (c == '"' || c == '\\') sb.Append('\\').Append(c); else if (c == '\n') sb.Append("\\n"); else if (c == '<') sb.Append("\\u003c"); else if (c < ' ') sb.Append(' '); else sb.Append(c);
            return sb.Append('"').ToString();
        }
        static string I(int v) { return v.ToString(CultureInfo.InvariantCulture); }
        static string Json(D d)
        {
            var sb = new StringBuilder("{\"name\":").Append(Str(d.Name)).Append(",\"measured\":true,\"note\":").Append(Str(d.Note)).Append(",\"nodes\":[");
            sb.Append(string.Join(",", d.Nodes.Select(n => "{\"id\":" + Str(n.Id) + ",\"k\":" + Str(n.K) + ",\"name\":" + Str(n.Name) + ",\"sub\":" + Str(n.Sub) + ",\"ms\":" + Str(n.Ms) + ",\"st\":" + Str(n.St) +
                (n.Info != "" ? ",\"info\":" + Str(n.Info) : "") + ",\"x\":" + I(n.X) + ",\"y\":" + I(n.Y) + "}")));
            sb.Append("],\"links\":[").Append(string.Join(",", d.Links.Select(l => "{\"a\":" + Str(l.A) + ",\"b\":" + Str(l.B) + ",\"kind\":" + Str(l.Kind) + ",\"label\":" + Str(l.Label) + "}")));
            sb.Append("],\"areas\":[").Append(string.Join(",", d.Areas.Select(a => "{\"x\":" + I(a.X) + ",\"y\":" + I(a.Y) + ",\"w\":" + I(a.W) + ",\"h\":" + I(a.H) + ",\"t\":" + Str(a.T) + "}")));
            sb.Append("],\"texts\":[").Append(string.Join(",", d.Texts.Select(t => "{\"x\":" + I(t.X) + ",\"y\":" + I(t.Y) + ",\"t\":" + Str(t.T) + "}"))).Append("]}");
            return sb.ToString();
        }
        static string Ms(double v) { return double.IsNaN(v) || v < 0 ? "" : (v < 1 ? "<1" : v.ToString(v < 10 ? "0.#" : "0", CultureInfo.InvariantCulture)) + " ms"; }
        static string Ago(DateTime t)
        {
            var s = DateTime.Now - t; if (t == DateTime.MinValue) return "";
            return s.TotalMinutes < 2 ? "just now" : s.TotalHours < 1 ? (int)s.TotalMinutes + " min ago" : s.TotalDays < 1 ? (int)s.TotalHours + " h ago" : (int)s.TotalDays + (s.TotalDays < 2 ? " day ago" : " days ago");
        }
        static bool Laptop() { try { return System.Windows.Forms.SystemInformation.PowerStatus.BatteryChargeStatus != System.Windows.Forms.BatteryChargeStatus.NoSystemBattery; } catch (Exception) { return false; } }
        public static object TestPc() { return new N { Id = "pc", K = "laptop", Name = "This PC", Sub = "192.168.1.50", Ms = "Wi-Fi" }; }
        static N ThisPc(Monitor m, int x, int y)
        {
            string link = LabLink.LinkKind(m), ip; lock (m.L) ip = m.LocalIp ?? "";
            return new N { Id = "pc", K = Laptop() ? "laptop" : "pc", Name = "This PC", Sub = ip, Ms = link == "wifi" ? "Wi-Fi" : link == "cell" ? "SIM" : "cable", X = x, Y = y, Info = "This PC\n" + Environment.MachineName + "\n" + ip };
        }

        // ================================================================== all of them, for the lab's start page
        public static string All(Monitor m)
        {
            var parts = new List<string>();
            Action<string, Func<string>> add = (k, f) => { try { var j = f(); if (j != null) parts.Add(Str(k) + ":" + j); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("lab view " + k + ": " + e.Message); } };
            add("lan", () => Lan(m, false));
            add("path", () => Path(m));
            add("wifi", () => Wifi(m));
            add("phone", () => Phone(m));
            add("vpn", () => Vpn(m));
            return "{\"at\":" + Str(DateTime.Now.ToString("d MMM HH:mm", CultureInfo.CurrentCulture)) + "," + string.Join(",", parts) + "}";
        }
        public static string One(Monitor m, string which, bool ask)
        {
            switch (which)
            {
                case "lan": return Lan(m, ask);
                case "path": return Path(m);
                case "wifi": return Wifi(m);
                case "phone": return Phone(m);
                case "vpn": return Vpn(m);
            }
            return null;
        }

        // ================================================================== See my LAN
        static string KindOf(Picks.LanDev d, out string group)
        {
            string k = (d.Kind ?? "").ToLowerInvariant(), mk = (d.Maker + " " + d.Model + " " + d.Name).ToLowerInvariant();
            Func<string, bool> has = w => w.Split('|').Any(x => mk.Contains(x));
            if (d.Router) { group = "gear"; return "cpe"; }
            if (k.Contains("router") || has("ubiquiti|eero|tp-link deco|netgear orbi|access point|mesh|aruba|ruckus|meraki|mikrotik")) { group = "gear"; return "wifi"; }
            if (k.Contains("printer") || has("printer|epson|brother|canon|lexmark|kyocera|xerox")) { group = "print"; return "printer"; }
            if (has("synology|qnap|nas|western digital|wd my")) { group = "print"; return "database"; }
            if (has("raspberry|server|proxmox|unraid")) { group = "pcs"; return "server"; }
            if (k.Contains("tv") || has("roku|chromecast|fire tv|apple tv|lg electronics|vizio|tcl|hisense|nvidia shield|playstation|xbox|nintendo|sony interactive")) { group = "media"; return "tv"; }
            if (k.Contains("speaker") || has("sonos|bose|echo|homepod|google home|nest audio")) { group = "media"; return "speaker"; }
            if (has("hikvision|dahua|reolink|wyze|ring|arlo|blink|camera|amcrest|eufy")) { group = "home"; return "camera"; }
            if (k.Contains("smart home") || has("espressif|tuya|shelly|sonoff|philips hue|signify|ecobee|nest|tp-link kasa|lifx|wemo|smartthings|meross|govee")) { group = "home"; return "iot"; }
            if (k.Contains("computer") || has("intel|dell|lenovo|hewlett|hp inc|microsoft|asustek|micro-star|gigabyte|realtek|liteon|azurewave")) { group = "pcs"; return "pc"; }
            if (has("apple|samsung|google|xiaomi|oneplus|huawei|motorola|oppo|vivo|pixel|iphone|ipad|galaxy") || d.Random) { group = "phones"; return "phone"; }
            group = "other"; return "pc";
        }
        static readonly string[][] Groups = { new[] { "gear", "Network gear" }, new[] { "phones", "Phones, tablets and laptops" }, new[] { "pcs", "Computers" },
                                              new[] { "media", "TV, games and speakers" }, new[] { "home", "Smart home and cameras" }, new[] { "print", "Printers and storage" }, new[] { "other", "Not sure what these are" } };
        public static string Lan(Monitor m, bool ask)
        {
            var all = Picks.LanDevices(m, ask, () => false);
            string gw; lock (m.L) gw = m.Gateway ?? "";
            return LanDoc(all, gw, Brain.NetLabel, Picks.LanAskedAt, ThisPc(m, 0, 0), LabLink.LinkKind(m));
        }
        // the drawing itself, from data only (SelfTest draws a made-up network of 70 devices with it)
        public static string LanDoc(List<Picks.LanDev> all, string gw, string netLabel, DateTime asked, object pcNode, string pl)
        {
            var pc = (N)pcNode;
            var d = new D { Name = "My LAN - " + (string.IsNullOrEmpty(netLabel) ? "this network" : netLabel) };
            if (all.Count == 0) { d.Note = "Not connected to a network right now."; pc.X = 300; pc.Y = 200; d.Nodes.Add(pc); return Json(d); }
            var drawn = all.Where(x => !x.Router).Take(LanMax).ToList(); int more = all.Count(x => !x.Router) - drawn.Count;
            var router = all.FirstOrDefault(x => x.Router);
            d.Note = all.Count + " devices seen on this network · " + all.Count(x => x.Here) + " here now · " + (asked == DateTime.MinValue ? "names: run Devices on this network once (or lab lan --ask in AdeeboShell) to ask the devices who they are" : "names and times from " + Ago(asked)) +
                     (more > 0 ? " · " + LanMax + " drawn, the other " + more + " are in Devices on this network" : "") + ". White = here now, grey = seen before. Hover a device for its address and maker.";
            // the router on top, This PC beside it; each group a box below, packed in shelves
            int cx = 640;
            d.Nodes.Add(new N { Id = "gw", K = "cpe", Name = router != null && router.Label != "" ? router.Label : "Your router", Sub = gw, Ms = router != null ? Ms(router.Ms) : "", X = cx, Y = 90,
                                Info = router != null ? Info(router) : "Your router\n" + gw });
            pc.X = cx - 260; pc.Y = 90; d.Nodes.Add(pc);
            d.Links.Add(new Ln { A = "pc", B = "gw", Kind = pl == "cell" ? "cell" : pl, Label = pl == "wifi" ? "Wi-Fi" : pl == "cell" ? "SIM" : "cable" });
            d.Nodes.Add(new N { Id = "net", K = "internet", Name = "Internet", Sub = "", St = "typ", X = cx + 260, Y = 90 }); d.Links.Add(new Ln { A = "gw", B = "net", Kind = "fibre" });
            d.Areas.Add(new Ar { X = cx - 330, Y = 20, W = 660, H = 150, T = "Your home network" });
            var byGroup = new Dictionary<string, List<Tuple<Picks.LanDev, string>>>();
            foreach (var x in drawn) { string g; string k = KindOf(x, out g); if (!byGroup.ContainsKey(g)) byGroup[g] = new List<Tuple<Picks.LanDev, string>>(); byGroup[g].Add(Tuple.Create(x, k)); }
            const int cellW = 130, cellH = 120, maxRowW = 1360, per = 4; int sx = 0, sy = 230, shelfH = 0, n = 0;
            foreach (var gr in Groups)
            {
                List<Tuple<Picks.LanDev, string>> l; if (!byGroup.TryGetValue(gr[0], out l)) continue;
                bool last = gr[0] == "other";
                int cols = Math.Min(per, l.Count + (last && more > 0 ? 1 : 0)), rows = (l.Count + (last && more > 0 ? 1 : 0) + per - 1) / per;
                int w = Math.Max(cols * cellW + 30, 200), h = rows * cellH + 120;
                if (sx > 0 && sx + w > maxRowW) { sx = 0; sy += shelfH + 30; shelfH = 0; }
                d.Areas.Add(new Ar { X = sx, Y = sy, W = w, H = h, T = gr[1] + " (" + l.Count + ")" });
                // one stop per group for the last step (the router's Wi-Fi or a port - which one isn't known): 7 lines from the router, not 50
                string hub = "g" + gr[0]; d.Nodes.Add(new N { Id = hub, K = "switch", Name = "Through your router", Sub = "Wi-Fi or a port", St = "typ", X = sx + w / 2, Y = sy + 55, Info = "These devices reach the router over its Wi-Fi or one of its ports - which one isn't something a PC can see." });
                d.Links.Add(new Ln { A = "gw", B = hub, Kind = "cable" });
                for (int i = 0; i < l.Count; i++)
                {
                    var x = l[i].Item1; string id = "d" + (n++);
                    int px = sx + 15 + cellW / 2 + (i % per) * cellW, py = sy + 140 + (i / per) * cellH;
                    d.Nodes.Add(new N { Id = id, K = l[i].Item2, Name = (x.Watch ? "★ " : "") + Short(x.Name, 18), Sub = x.Ip, Ms = x.Here ? Ms(x.Ms) : "not here now", St = !x.Here ? "typ" : x.Watch && double.IsNaN(x.Ms) && asked != DateTime.MinValue ? "warn" : "", X = px, Y = py, Info = Info(x) });
                    d.Links.Add(new Ln { A = hub, B = id, Kind = "cable" });
                }
                if (last && more > 0) AddMore(d, more, sx + 15 + cellW / 2 + (l.Count % per) * cellW, sy + 140 + (l.Count / per) * cellH);
                sx += w + 30; shelfH = Math.Max(shelfH, h);
            }
            if (more > 0 && !byGroup.ContainsKey("other")) { if (sx > 0 && sx + cellW + 30 > maxRowW) { sx = 0; sy += shelfH + 30; } d.Areas.Add(new Ar { X = sx, Y = sy, W = cellW + 30, H = cellH + 50, T = "Not drawn" }); AddMore(d, more, sx + 15 + cellW / 2, sy + 70); }
            return Json(d);
        }
        static void AddMore(D d, int more, int x, int y) { d.Nodes.Add(new N { Id = "more", K = "users", Name = "+" + more + " more", Sub = "Devices on this network lists them all", St = "typ", X = x, Y = y, Info = more + " more devices aren't drawn - a drawing stops at " + LanMax + "." }); }
        static string Short(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n - 1) + "…"; }
        static string Info(Picks.LanDev x)
        {
            var l = new List<string> { x.Name, x.Ip + "  ·  " + x.Mac + (x.Random ? " (private address)" : "") };
            string what = string.Join(" · ", new[] { x.Kind, x.Maker, x.Model }.Where(z => !string.IsNullOrEmpty(z)).Distinct()); if (what != "") l.Add(what);
            if (x.Via != "") l.Add("Told us via " + x.Via);
            l.Add(x.Here ? "Here now" + (double.IsNaN(x.Ms) ? "" : " · " + Ms(x.Ms)) : "Last seen " + Ago(x.Last));
            if (x.First != DateTime.MinValue) l.Add("First seen " + x.First.ToString("d MMM yyyy", CultureInfo.CurrentCulture));
            if (x.Watch) l.Add("★ Watched");
            return string.Join("\n", l);
        }

        // ================================================================== Path to anywhere: the last trace or MTR the app ran
        sealed class Run { public string Target; public DateTime At; public List<PathTools.Hop> Hops; }
        static Run lastRun; static readonly object rk = new object();
        public static void Remember(string target, List<PathTools.Hop> hops) { if (hops == null || hops.Count == 0) return; lock (rk) lastRun = new Run { Target = target, At = DateTime.Now, Hops = hops.ToList() }; }
        public static string Path(Monitor m)
        {
            Run r; lock (rk) r = lastRun;
            if (r == null) return null;   // the start page says how to get one
            return PathDoc(r.Target, r.At, r.Hops, ThisPc(m, 0, 0), LabLink.LinkKind(m));
        }
        public static string PathDoc(string target, DateTime at, List<PathTools.Hop> hops0, object pcNode, string link)
        {
            var r = new Run { Target = target, At = at, Hops = hops0 }; var pc = (N)pcNode;
            var d = new D { Name = "Path to " + r.Target };
            var hops = r.Hops; var final = hops.LastOrDefault(h => h.Ip != "");
            Func<PathTools.Hop, double> loss = h => h.Sent > 0 ? 100.0 * h.Lost / h.Sent : double.NaN;
            Func<PathTools.Hop, double> ms = h => h.Sent - h.Lost > 0 && h.Sum > 0 ? h.Sum / (h.Sent - h.Lost) : h.Rtt >= 0 ? h.Rtt : double.NaN;
            double endLoss = final != null ? loss(final) : double.NaN; bool mtr = hops.Any(h => h.Sent > 0);
            d.Note = (mtr ? "MTR" : "Trace") + " to " + r.Target + ", " + Ago(r.At) + ". Red or amber = loss that carries on to the end (real). Loss at one hop only is that router ignoring pings - normal. Hover a hop for its numbers.";
            int per = 6; pc.X = 100; pc.Y = 140; d.Nodes.Add(pc);
            string prev = "pc"; double prevMs = 0;
            for (int i = 0; i < hops.Count && i < 30; i++)
            {
                var h = hops[i]; int row = (i + 1) / per, col = (i + 1) % per; bool rev = row % 2 == 1;
                int x = 100 + (rev ? per - 1 - col : col) * 200, y = 140 + row * 170;
                double ls = loss(h), mv = ms(h); bool silent = h.Ip == "";
                double eff = double.IsNaN(ls) || double.IsNaN(endLoss) ? 0 : Math.Min(ls, endLoss);   // only the part of a hop's loss that carries on to the end is real
                string st = silent ? "typ" : eff >= 2 ? (eff >= 10 ? "bad" : "warn") : !double.IsNaN(mv) && mv - prevMs > 50 && i > 0 ? "warn" : "";
                string id = "h" + h.N;
                d.Nodes.Add(new N { Id = id, K = h.Reached || h == final ? "server" : "router", Name = h.Reached ? r.Target : "Hop " + h.N, Sub = silent ? "no answer" : h.Ip, St = st,
                                    Ms = silent ? "" : Ms(mv) + (double.IsNaN(ls) ? "" : " · " + Math.Round(ls) + "% loss"), X = x, Y = y,
                                    Info = "Hop " + h.N + "\n" + (silent ? "didn't answer (many routers don't - not a problem by itself)" : h.Ip + (h.Owner != "" ? "  ·  " + h.Owner : "")) +
                                           (double.IsNaN(mv) ? "" : "\naverage " + Ms(mv) + (double.IsNaN(h.Best) ? "" : " · best " + Ms(h.Best) + " · worst " + Ms(h.Worst))) +
                                           (double.IsNaN(ls) ? "" : "\nloss " + Math.Round(ls, 1) + "% (" + h.Lost + " of " + h.Sent + ")") + (h.Switches > 0 ? "\nchanged router " + h.Switches + " times (load sharing)" : "") });
                d.Links.Add(new Ln { A = prev, B = id, Kind = st == "bad" ? "bad" : i == 0 && link == "wifi" ? "wifi" : "cable" });
                prev = id; if (!double.IsNaN(mv)) prevMs = mv;
            }
            if (hops.Count > 30) d.Texts.Add(new Tx { X = 100, Y = 140 + (31 / per + 1) * 170, T = "…and " + (hops.Count - 30) + " more hops" });
            return Json(d);
        }

        // ================================================================== Wi-Fi around you: the networks this PC hears, by channel
        public static string Wifi(Monitor m)
        {
            string err; var list = WlanApi.List(false, out err);
            string myB, mySsid; lock (m.L) { myB = m.Bssid ?? ""; mySsid = m.Ssid ?? ""; }
            var d = new D { Name = "Wi-Fi around you" };
            if (list.Count == 0)
            {
                d.Note = err != "" ? "No Wi-Fi list: " + err + "." : "This PC hears no Wi-Fi networks (or has no Wi-Fi).";
                d.Nodes.Add(ThisPc(m, 200, 200)); d.Texts.Add(new Tx { X = 120, Y = 300, T = err != "" ? err : "No Wi-Fi networks heard" }); return Json(d);
            }
            var mine = list.FirstOrDefault(b => string.Equals(b.Bssid, myB, StringComparison.OrdinalIgnoreCase));
            var aps = list.OrderByDescending(b => b == mine).ThenByDescending(b => b.Rssi).Take(40).OrderBy(b => b.FreqMHz).ToList();
            d.Note = list.Count + " access points heard" + (list.Count > 40 ? " (the 40 loudest drawn)" : "") + ". Yours is white; amber = a loud neighbour on a channel that overlaps yours. Hover one for details.";
            var bands = new[] { new[] { "2.4", "2.4 GHz - only 1, 6 and 11 don't overlap" }, new[] { "5", "5 GHz" }, new[] { "6", "6 GHz" } };
            int y0 = 30, n = 0;
            foreach (var bd in bands)
            {
                var l = aps.Where(b => BandOf(b) == bd[0]).ToList(); if (l.Count == 0) continue;
                var chans = l.Select(b => b.Channel).Distinct().OrderBy(c => c).ToList();
                int laneH = Math.Min(5, l.GroupBy(b => b.Channel).Max(g => g.Count())) * 100 + 90, w = Math.Max(600, chans.Count * 150 + 60);
                d.Areas.Add(new Ar { X = 0, Y = y0, W = w, H = laneH, T = bd[1] });
                for (int c = 0; c < chans.Count; c++)
                {
                    var onCh = l.Where(b => b.Channel == chans[c]).OrderByDescending(b => b.Rssi).ToList(); int x = 90 + c * 150;
                    d.Texts.Add(new Tx { X = x - 18, Y = y0 + laneH - 12, T = "ch " + chans[c] + (onCh.Count > 5 ? " (+" + (onCh.Count - 5) + ")" : "") });
                    for (int i = 0; i < onCh.Count && i < 5; i++)
                    {
                        var b = onCh[i]; bool me = b == mine;
                        bool near = mine != null && !me && BandOf(b) == BandOf(mine) && WifiOverlap(b, mine) && b.Rssi > -70;
                        string id = "w" + (n++);
                        d.Nodes.Add(new N { Id = id, K = "wifi", Name = Short(b.Ssid == "" ? "(hidden)" : b.Ssid, 18), Sub = b.Rssi + " dBm · " + b.Width + " MHz", St = me ? "" : near ? "warn" : "typ", Ms = me ? "yours" : "",
                                            X = x, Y = y0 + 70 + i * 100, Info = (b.Ssid == "" ? "(hidden network)" : b.Ssid) + "\n" + b.Bssid + "\nchannel " + b.Channel + " · " + b.Width + " MHz · " + b.Band + "\n" + b.Rssi + " dBm" + (b.Sec != "" ? " · " + b.Sec : "") + (me ? "\nThis PC is connected here" : near ? "\nShares airtime with yours" : "") });
                        if (me) { d.Nodes.Add(ThisPc(m, w + 120, y0 + 70)); d.Links.Add(new Ln { A = "pc", B = id, Kind = "wifi", Label = mySsid }); }
                    }
                }
                y0 += laneH + 40;
            }
            return Json(d);
        }
        static string BandOf(WlanApi.Bss b) { return b.FreqMHz >= 5925 ? "6" : b.FreqMHz >= 4900 ? "5" : "2.4"; }
        static bool WifiOverlap(WlanApi.Bss a, WlanApi.Bss b) { return LinkInfo.Overlaps(a.Channel, a.Width, b.Channel, b.Width, BandOf(a) + " GHz"); }

        // ================================================================== Phone vs this PC
        public static string Phone(Monitor m)
        {
            double gw = m.GwPing.Avg(6), net = m.NetPing.Avg(6); string gwIp, link = LabLink.LinkKind(m); lock (m.L) gwIp = m.Gateway ?? "";
            string label; string ph = PhoneHub.StateOf(out label);
            var d = new D { Name = "Your phone vs this PC" };
            d.Note = "This PC's stops are measured (white). Your phone's own path isn't - the phone doesn't run AdeeboMeter - so it's the usual setup (grey). If the phone is slow on home Wi-Fi too, it's the Wi-Fi or the router; if only on 4G / 5G, it's the mobile network.";
            d.Nodes.Add(ThisPc(m, 100, 150));
            d.Nodes.Add(new N { Id = "gw", K = "cpe", Name = "Your router", Sub = gwIp, Ms = Ms(gw), X = 330, Y = 230 });
            d.Nodes.Add(new N { Id = "net", K = "internet", Name = "Internet", Ms = Ms(net), X = 900, Y = 230 });
            d.Nodes.Add(new N { Id = "isp", K = "router", Name = "Your provider", Sub = "home internet", St = "typ", X = 600, Y = 150 });
            d.Nodes.Add(new N { Id = "ph", K = "phone", Name = ph == "on" ? label : "Your phone", Sub = ph == "on" ? "connected to AdeeboMeter" : "", St = "typ", X = 100, Y = 330 });
            d.Nodes.Add(new N { Id = "tw", K = "tower", Name = "Cell tower", Sub = "4G / 5G", St = "typ", X = 330, Y = 450 });
            d.Nodes.Add(new N { Id = "core", K = "server", Name = "Mobile core", Sub = "UPF / P-GW", St = "typ", X = 600, Y = 400 });
            d.Links.Add(new Ln { A = "pc", B = "gw", Kind = link == "cell" ? "cell" : link, Label = "this PC" });
            d.Links.Add(new Ln { A = "ph", B = "gw", Kind = "wifi", Label = "phone on home Wi-Fi" });
            d.Links.Add(new Ln { A = "gw", B = "isp", Kind = "fibre" }); d.Links.Add(new Ln { A = "isp", B = "net", Kind = "fibre" });
            d.Links.Add(new Ln { A = "ph", B = "tw", Kind = "cell", Label = "phone on 4G / 5G" }); d.Links.Add(new Ln { A = "tw", B = "core", Kind = "fibre" }); d.Links.Add(new Ln { A = "core", B = "net", Kind = "fibre", Label = "GTP tunnel" });
            d.Areas.Add(new Ar { X = 20, Y = 60, W = 420, H = 230, T = "Your home - measured" }); d.Areas.Add(new Ar { X = 20, Y = 300, W = 700, H = 220, T = "Mobile network - usual setup, not measured" });
            return Json(d);
        }

        // ================================================================== VPN split: what goes through the tunnel and what goes straight out
        public static string Vpn(Monitor m)
        {
            var v = VpnAware.Detect(); double gw = m.GwPing.Avg(6), net = m.NetPing.Avg(6); string gwIp, link = LabLink.LinkKind(m); lock (m.L) gwIp = m.Gateway ?? "";
            bool on = v.AdapterUp || v.Zero;
            var d = new D { Name = on ? "VPN split - " + (v.Products.Count > 0 ? v.Products[0] : v.Adapter) : "VPN split (no VPN on now)" };
            d.Nodes.Add(ThisPc(m, 100, 230));
            d.Nodes.Add(new N { Id = "gw", K = "cpe", Name = "Your router", Sub = gwIp, Ms = Ms(gw), X = 330, Y = 230 });
            d.Nodes.Add(new N { Id = "net", K = "internet", Name = "Internet", Ms = Ms(net), X = 600, Y = 230 });
            d.Nodes.Add(new N { Id = "web", K = "cloud", Name = "Websites, games", Sub = "everything else", St = "typ", X = 900, Y = 110 });
            d.Nodes.Add(new N { Id = "vg", K = "firewall", Name = v.Zero ? "Cloud security service" : "VPN gateway", Sub = v.FarSide != null ? v.FarSide.ToString() : "the company's side", St = on ? "" : "typ", X = 900, Y = 340 });
            d.Nodes.Add(new N { Id = "co", K = "building", Name = "Company network", Sub = "files, intranet", St = "typ", X = 1140, Y = 340 });
            d.Links.Add(new Ln { A = "pc", B = "gw", Kind = link == "cell" ? "cell" : link }); d.Links.Add(new Ln { A = "gw", B = "net", Kind = "fibre" });
            d.Links.Add(new Ln { A = "vg", B = "co", Kind = "fibre" });
            if (!on)
            {
                d.Note = "No VPN or zero-trust client is connected now, so everything goes straight out (the top path). When one is on, open the lab again to see the split.";
                d.Links.Add(new Ln { A = "net", B = "web", Label = "everything" }); d.Links.Add(new Ln { A = "net", B = "vg", Kind = "fibre", Label = "tunnel (off)" });
            }
            else if (v.FullTunnel)
            {
                d.Note = "Full tunnel: every site goes to the company first and out from there - the direct path is unused. Sites feel as far away as the company's internet exit.";
                d.Links.Add(new Ln { A = "net", B = "vg", Kind = "cable", Label = "tunnel - everything" }); d.Links.Add(new Ln { A = "vg", B = "web", Kind = "cable", Label = "out from the company" });
            }
            else
            {
                d.Note = v.Zero ? "Zero-trust client: web traffic is checked by the company's cloud service on the way out; the rest goes straight out." : "Split tunnel: company addresses go through the tunnel, everything else straight out - the quick way for calls and games.";
                d.Links.Add(new Ln { A = "net", B = "web", Label = "direct" }); d.Links.Add(new Ln { A = "net", B = "vg", Kind = "cable", Label = v.Zero ? "web traffic" : "tunnel - company only" });
            }
            if (v.Proxy != "" || v.Pac != "") d.Texts.Add(new Tx { X = 100, Y = 400, T = "Web proxy: " + (v.Proxy != "" ? v.Proxy : "picked by a PAC script") });
            d.Areas.Add(new Ar { X = 20, Y = 160, W = 420, H = 150, T = "Your home" }); d.Areas.Add(new Ar { X = 800, Y = 270, W = 440, H = 150, T = "Your company" });
            return Json(d);
        }
    }
}
