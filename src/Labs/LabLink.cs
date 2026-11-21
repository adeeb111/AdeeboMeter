// Network Lab hand-off (1.1.6): the measured path of a conversation, drawn in the Network Lab page (docs/lab.html, shipped
// inside the exe). The page opens in an Edge app window with its own profile; the path rides in the address after the #
// (the part a browser never sends anywhere). Nothing listens: the page and the app share no channel but that address.
// Measured stops are drawn white; the provider's equipment a PC can't see (fibre box, OLT, sign-in router, the cell tower)
// is drawn as the usual setup, grey and dashed, and says so.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using Microsoft.Win32;

namespace AdeeboMeter
{
    static class LabLink
    {
        public sealed class Stop { public string Id, Kind, Name, Sub = "", Ms = "", St = ""; public int X, Y; }
        public sealed class Wire { public string A, B, Kind = "cable", Label = ""; }

        // how this PC reaches its router: Wi-Fi, a SIM (mobile broadband) or a cable
        public static string LinkKind(Monitor m)
        {
            string id, kind; lock (m.L) { id = m.IfaceId; kind = m.AdapterKind; }
            try
            {
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                    if (n.Id == id && (n.NetworkInterfaceType == NetworkInterfaceType.Wwanpp || n.NetworkInterfaceType == NetworkInterfaceType.Wwanpp2)) return "cell";
            }
            catch (NetworkInformationException) { }
            return kind == "Wi-Fi" ? "wifi" : "cable";
        }
        static bool Laptop() { try { return System.Windows.Forms.SystemInformation.PowerStatus.BatteryChargeStatus != System.Windows.Forms.BatteryChargeStatus.NoSystemBattery; } catch (Exception) { return false; } }
        static string Ms(double v) { return double.IsNaN(v) ? "" : v.ToString(v < 10 ? "0.#" : "0", CultureInfo.InvariantCulture) + " ms"; }

        // the path from this PC to a server: router and internet from the monitor's own pings, the server from 10 pings
        public static string PathJson(Monitor m, string server, string serverName, double serverMs, double gwMs, double netMs)
        {
            string link = LinkKind(m), gw, ssid; int sig; lock (m.L) { gw = m.Gateway ?? ""; ssid = m.Ssid ?? ""; sig = m.WifiSignal; }
            var s = new List<Stop>(); var w = new List<Wire>();
            s.Add(new Stop { Id = "pc", Kind = Laptop() ? "laptop" : "pc", Name = "This PC", Sub = link == "wifi" ? "Wi-Fi" + (sig >= 0 ? " · " + sig + "%" : "") : link == "cell" ? "SIM · 4G / 5G" : "cable", X = 100, Y = 250 });
            if (link == "cell")
            {
                s.Add(new Stop { Id = "tw", Kind = "tower", Name = "Cell tower", Sub = "gNB / eNodeB", St = "typ", X = 330, Y = 250 });
                s.Add(new Stop { Id = "bh", Kind = "switch", Name = "Backhaul", Sub = "fibre or microwave", St = "typ", X = 480, Y = 150 });
                s.Add(new Stop { Id = "core", Kind = "server", Name = "Mobile core", Sub = "UPF / P-GW", St = "typ", X = 630, Y = 250 });
                s.Add(new Stop { Id = "gw", Kind = "router", Name = "Provider edge", Sub = gw, Ms = Ms(gwMs), X = 800, Y = 380 });
                w.Add(new Wire { A = "pc", B = "tw", Kind = "cell", Label = "4G / 5G" }); w.Add(new Wire { A = "tw", B = "bh", Kind = "fibre" });
                w.Add(new Wire { A = "bh", B = "core", Kind = "fibre" }); w.Add(new Wire { A = "core", B = "gw", Kind = "fibre", Label = "GTP tunnel" });
            }
            else
            {
                s.Add(new Stop { Id = "gw", Kind = "cpe", Name = "Your router", Sub = gw, Ms = Ms(gwMs), X = 290, Y = 250 });
                w.Add(new Wire { A = "pc", B = "gw", Kind = link, Label = link == "wifi" ? "Wi-Fi" : "cable" });
                string[][] chain = { new[] { "ont", "ont", "Fibre box (ONT)", "light to electric" }, new[] { "spl", "splitter", "Splitter", "1 fibre, ~32 homes" },
                                     new[] { "olt", "olt", "OLT", "street / exchange" }, new[] { "agg", "switch", "Aggregation switch", "thousands of homes" },
                                     new[] { "bng", "router", "Sign-in router", "BNG · gives your IP" } };
                for (int i = 0; i < chain.Length; i++) s.Add(new Stop { Id = chain[i][0], Kind = chain[i][1], Name = chain[i][2], Sub = chain[i][3], St = "typ", X = 450 + i * 120 - (i == 4 ? 10 : 0), Y = i % 2 == 1 ? 150 : 250 });
                w.Add(new Wire { A = "gw", B = "ont", Kind = "cable" });
                for (int i = 1; i < chain.Length; i++) w.Add(new Wire { A = chain[i - 1][0], B = chain[i][0], Kind = "fibre", Label = i == 1 ? "fibre" : "" });
            }
            string last = link == "cell" ? "gw" : "bng";
            s.Add(new Stop { Id = "net", Kind = "internet", Name = "Internet", Sub = "", Ms = Ms(netMs), X = 1060, Y = 300 });
            s.Add(new Stop { Id = "srv", Kind = "server", Name = string.IsNullOrEmpty(serverName) ? "Server" : serverName, Sub = server ?? "", Ms = Ms(serverMs), X = 1260, Y = 220 });
            w.Add(new Wire { A = last, B = "net", Kind = link == "cell" ? "cable" : "fibre" }); w.Add(new Wire { A = "net", B = "srv", Kind = "cable" });
            var areas = link == "cell"
                ? new[] { Area(30, 60, 200, 330, "You"), Area(260, 60, 680, 420, "Mobile provider - usual 4G / 5G setup, not measured") }
                : new[] { Area(30, 60, 330, 330, "Your home"), Area(390, 60, 640, 420, "Your provider - usual fibre setup, not measured") };
            var sb = new StringBuilder("{\"name\":").Append(Str("Path to " + (string.IsNullOrEmpty(serverName) ? server : serverName))).Append(",\"measured\":true,\"nodes\":[");
            sb.Append(string.Join(",", s.Select(n => "{\"id\":" + Str(n.Id) + ",\"k\":" + Str(n.Kind) + ",\"name\":" + Str(n.Name) + ",\"sub\":" + Str(n.Sub) + ",\"ms\":" + Str(n.Ms) +
                ",\"st\":" + Str(n.St) + ",\"x\":" + n.X.ToString(CultureInfo.InvariantCulture) + ",\"y\":" + n.Y.ToString(CultureInfo.InvariantCulture) + "}")));
            sb.Append("],\"links\":[").Append(string.Join(",", w.Select(l => "{\"a\":" + Str(l.A) + ",\"b\":" + Str(l.B) + ",\"kind\":" + Str(l.Kind) + ",\"label\":" + Str(l.Label) + "}")));
            sb.Append("],\"areas\":[").Append(string.Join(",", areas)).Append("]}");
            return sb.ToString();
        }
        static string Area(int x, int y, int w, int h, string t) { return "{\"x\":" + x + ",\"y\":" + y + ",\"w\":" + w + ",\"h\":" + h + ",\"t\":" + Str(t) + "}"; }
        static string Str(string v)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in (v ?? "").Length > 80 ? v.Substring(0, 80) : v ?? "")
                if (c == '"' || c == '\\') sb.Append('\\').Append(c); else if (c < ' ') sb.Append(' '); else sb.Append(c);
            return sb.Append('"').ToString();
        }

        // ---------- the page: written from the exe next to the data folder, then opened in Edge's app window
        public static string PagePath()
        {
            string dir = Path.Combine(Program.DataDir, "lab"), file = Path.Combine(dir, "lab.html");
            try
            {
                byte[] page;
                using (var rs = typeof(LabLink).Assembly.GetManifestResourceStream("AdeeboMeter.lab.html"))
                {
                    if (rs == null) return null;
                    using (var ms = new MemoryStream()) { rs.CopyTo(ms); page = ms.ToArray(); }
                }
                Directory.CreateDirectory(dir);
                if (!File.Exists(file) || new FileInfo(file).Length != page.Length || !File.ReadAllBytes(file).SequenceEqual(page)) SafeFile.WriteBytes(file, page);
                return file;
            }
            catch (IOException e) { Program.Log("lab page: " + e.Message); return null; }
            catch (UnauthorizedAccessException e) { Program.Log("lab page: " + e.Message); return null; }
        }
        static string Edge()
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
                try { using (var k = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe")) { var p = k == null ? null : k.GetValue("") as string; if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p; } }
                catch (System.Security.SecurityException) { }
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            {
                string p = Path.Combine(root, @"Microsoft\Edge\Application\msedge.exe"); if (File.Exists(p)) return p;
            }
            return null;
        }
        // json: a path to start from (or null for the lab as it was); returns what went wrong, or null
        public static string Open(string json, bool breakIt)
        {
            string page = PagePath(); if (page == null) return "The lab page couldn't be written to the data folder.";
            string url = new Uri(page).AbsoluteUri + (breakIt ? "?mode=break" : "");
            if (json != null) url += "#path=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            if (url.Length > 30000) return "This path is too big to hand over.";
            string edge = Edge(), profile = Path.Combine(Program.DataDir, "lab", "edge-profile");
            try
            {
                if (edge != null)
                {
                    var psi = new ProcessStartInfo(edge, "--app=\"" + url + "\" --user-data-dir=\"" + profile + "\" --no-first-run --no-default-browser-check --disable-sync") { UseShellExecute = false };
                    using (Process.Start(psi)) { }
                    return null;
                }
                using (Process.Start(new ProcessStartInfo(page) { UseShellExecute = true })) { }   // no Edge: the default browser, without the path (a file link loses the #)
                return "Edge wasn't found, so the lab opened in your browser without your path.";
            }
            catch (System.ComponentModel.Win32Exception e) { Program.Log("lab open: " + e.Message); return "The lab couldn't be opened: " + e.Message; }
        }
    }
}
