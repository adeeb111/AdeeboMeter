// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    sealed partial class FullView
    {
        // ---------- the phone remote's side of the Toolbox
        // The rule for what a phone may start: anything that only reads or measures. Kept on the PC: whatever changes the PC,
        // sends crafted traffic on the network, needs a password or key, works on a local file, or is a hands-on test
        // (screen, keys, sound) that needs someone at the PC.
        // by id: the Toolbox renames its groups for display, so the rule can't lean on group names
        static readonly HashSet<string> RemoteNoGroups = new HashSet<string> { "snmp", "snmpif", "snmptraffic", "snmpcpu", "snmplldp", "snmpsens", "snmpsnap", "snmpmac", "snmpvlan", "snmppoe", "snmplag", "snmpprn", "snmpups", "snmpwalk", "mibs", "icmpb", "udpsend", "tcpsend", "arpwho", "dhcpopt", "trap", "netflow", "keys", "newkey", "csrmake", "selfsigned", "certmatch", "certlive", "recipients", "encrypt", "decrypt", "certfile", "pfx", "csr", "certconv", "hashf", "b64" };
        static readonly HashSet<string> RemoteNo = new HashSet<string> { "sweep", "wol", "syslog", "remote", "tidy", "obs", "friend", "qr", "screentest", "keytest", "soundtest", "dhcpreplay", "rareplay", "eapreplay", "samplelab", "beforeafter", "twopc", "radius", "tacacs", "radiuslab", "httpreq", "shares", "mibs", "snmpwalk" };
        static readonly HashSet<string> RemoteHeavy = new HashSet<string> { "speedtest", "bloat", "mtr", "iperf", "diskspeed", "ramcheck", "cpuheld", "checkup", "gamecompare", "hidden", "pcwhy" };
        bool RemoteOk(Tool t)
        {
            if (t == null || RemoteNoGroups.Contains(t.Id) || RemoteNo.Contains(t.Id) || LabGate.Contains(t.Id) || SenderGate.Contains(t.Id)) return false;
            if (t.Inputs.Any(SecretInput)) return false;
            return t.Do != null || t.Id == "mtu";
        }
        internal bool RemoteAllowed(string id) { return id == "checkup" || id == "feltslow" || RemoteOk(Tools2().FirstOrDefault(z => z.Id == id)); }

        // the list the phone shows: group, title, and each box's own label, hint and default (AM-CG-290/293/295/300)
        // in parts of at most 24 KB each, so a long (or translated) list never runs past the relay's message size
        internal List<string> RemoteCatalog()
        {
            var parts = new List<string>(); var sb = new StringBuilder(); bool first = true;
            foreach (var t in Tools2().Where(RemoteOk))
            {
                if (sb.Length > 24000) { parts.Add("{\"t\":\"cat\",\"tools\":[" + sb + "]}"); sb.Clear(); first = true; }
                if (!first) sb.Append(','); first = false;
                sb.Append("{\"id\":\"").Append(t.Id).Append("\",\"g\":\"").Append(RJs(L.T(GroupTitle(t.Group)))).Append("\",\"t\":\"").Append(RJs(L.T(t.Title))).Append("\",\"h\":").Append(RemoteHeavy.Contains(t.Id) ? 1 : 0).Append(",\"in\":[");
                for (int i = 0; i < t.Inputs.Length; i++)
                {
                    string inp = t.Inputs[i]; int dash = inp.IndexOf(" - ", StringComparison.Ordinal);
                    string lab = dash > 0 ? inp.Substring(0, dash) : inp, cue = dash > 0 ? inp.Substring(dash + 3) : "";
                    if (i > 0) sb.Append(',');
                    sb.Append("[\"").Append(RJs(L.T(lab))).Append("\",\"").Append(RJs(L.T(cue))).Append("\",\"").Append(RJs(RemoteDefault(t.Id, i))).Append("\"]");
                }
                sb.Append("]}");
            }
            parts.Add("{\"t\":\"cat\",\"tools\":[" + sb + "],\"last\":1}");
            return parts;
        }
        static string RJs(string s)
        {
            var b = new StringBuilder();
            foreach (char ch in s ?? "") { if (ch == '"' || ch == '\\') b.Append('\\').Append(ch); else if (ch < 0x20) b.Append(' '); else b.Append(ch); }
            return b.ToString();
        }
        static string GroupTitle(string g) { return g.Length > 1 ? g.Substring(0, 1) + g.Substring(1).ToLowerInvariant() : g; }
        // the same starting values the desktop fills in
        string RemoteDefault(string id, int i)
        {
            string gw; lock (M.L) gw = M.Gateway;
            switch (id)
            {
                case "ping": return i == 0 ? (gw != "" ? gw : "1.1.1.1") : i == 1 ? "50" : i == 2 ? "200" : "";
                case "mtr": return i == 1 ? "1" : "";
                case "dig": return i == 1 ? "A" : "";
                case "tcpping": return i == 1 ? "20" : "";
                case "httptime": return i == 1 ? "5" : "";
                case "ntp": return i == 0 ? "time.windows.com" : "";
                default: return "";
            }
        }

        // a check started from the phone: runs here off the UI thread (this view is never shown). done(title, rows, hops):
        // hops are the path (MTR, MTU) as number, address, ms, detail, level - drawn on the phone like on the PC
        internal void RemoteRun(string id, string[] input, Action<string, List<string[]>, List<string[]>> done)
        {
            var t = Tools2().FirstOrDefault(z => z.Id == id);
            if (!RemoteOk(t)) { done(id, new List<string[]> { R4("2", "Not available", "this check runs on the PC only", "") }, null); return; }
            var a = new string[t.Inputs.Length]; for (int i = 0; i < a.Length; i++) a[i] = i < input.Length ? (input[i] ?? "").Trim() : "";
            for (int i = 0; i < a.Length; i++) if (a[i] == "@broker") a[i] = Cfg.TradeHost;   // AM-CG-289: "your broker" without the phone knowing its address
            if (id == "mtr" && a.Length > 1) { int m; a[1] = int.TryParse(a[1], out m) && m >= 1 && m <= 5 ? m.ToString(CultureInfo.InvariantCulture) : "1"; }   // at most 5 minutes from the phone
            Task.Factory.StartNew(() =>
            {
                List<string[]> res; List<string[]> hops = null; var run = new Toolkit.Run { Deadline = DateTime.UtcNow.AddMinutes(6) };
                try
                {
                    if (id == "mtu") res = RemoteMtu(a.Length > 0 ? a[0] : "", run, out hops);
                    else
                    {
                        res = t.Do(a, run) ?? new List<string[]>();
                        if (id == "mtr" && mtrHops != null) hops = mtrHops.ToList().Select(h => { double loss = h.Sent > 0 ? 100.0 * h.Lost / h.Sent : 0, avg = h.Sent - h.Lost > 0 ? h.Sum / (h.Sent - h.Lost) : double.NaN; return new[] { h.N.ToString(CultureInfo.InvariantCulture), h.Ip == "" ? "no answer" : h.Ip, double.IsNaN(avg) ? "" : Math.Round(avg).ToString(CultureInfo.InvariantCulture), h.Ip == "" ? "" : Math.Round(loss).ToString(CultureInfo.InvariantCulture) + "% lost", h.Ip == "" ? "-1" : loss >= 5 ? "1" : "0" }; }).ToList();
                    }
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; res = new List<string[]> { R4("2", "Didn't finish", e.Message, "") }; }
                done(L.T(t.Title), res.Where(o => o != null && !IsSecretRow(o)).ToList(), hops);
            }, TaskCreationOptions.LongRunning);
        }
        // MTU per hop without the desktop's two-step screen (AM-CG-292): trace, then the biggest packet at every hop
        List<string[]> RemoteMtu(string target, Toolkit.Run run, out List<string[]> hopsOut)
        {
            hopsOut = null; var rows = new List<string[]>();
            if (target == "") { rows.Add(R4("2", "Destination", "type a site or address", "")); return rows; }
            var dst = Icmp.Resolve4(target); if (dst == null) { rows.Add(R4("2", "Destination", "couldn't find " + target, "")); return rows; }
            run.Status = "Tracing..."; var hops = PathTools.Trace(dst, 30, () => run.Stop); if (hops == null || hops.Count == 0) { rows.Add(R4("2", "Path", "no route found", "")); return rows; }
            int local; lock (M.L) local = M.PrimaryMtu; if (local < 576) local = 1500;
            foreach (var h in hops.Where(z => z.Ip != ""))
            {
                if (run.Stop || DateTime.UtcNow > run.Deadline) break;
                int hi = hops.Where(x => x.N < h.N && x.Mtu > 0).Select(x => x.Mtu).DefaultIfEmpty(Math.Min(9216, local)).Min();
                try { PathTools.MtuAt(dst, h, hi); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("mtu: " + e.Message); }
            }
            int min = hops.Where(z => z.Mtu > 0).Select(z => z.Mtu).DefaultIfEmpty(-1).Min();
            rows.Add(R4(min < 0 ? "3" : min < 1500 ? "1" : "0", "Biggest packet all the way", min < 0 ? "not measured" : min + " bytes" + (min < 1500 ? " - " + PathTools.Kind(min) : " - full size"), min > 0 && min < 1500 ? "Something on the way wraps your packets (a VPN, PPPoE or a tunnel)." : ""));
            int prev = Math.Min(9216, local);
            hopsOut = hops.Select(h => { bool drop = h.Mtu > 0 && h.Mtu < prev; if (h.Mtu > 0) prev = h.Mtu; return new[] { h.N.ToString(CultureInfo.InvariantCulture), h.Ip == "" ? "no answer" : h.Ip, "", h.Mtu > 0 ? h.Mtu + " bytes" + (drop ? " - drops here" : "") : h.Ip == "" ? "" : "not measured", h.Ip == "" ? "-1" : drop ? "1" : "0" }; }).ToList();
            return rows;
        }
        static string[] R4(string a, string b, string c, string d) { return new[] { a, b, c, d }; }

        // the full checkup, the same one the Guide runs, without opening anything on the PC
        internal static void RemoteCheckup(Monitor m, Action<string, List<string[]>, List<string[]>> done)
        {
            Task.Factory.StartNew(() =>
            {
                var r = new GuideRun(); var rows = new List<string[]>();
                try
                {
                    Investigate.Run("full", m, r, () => { });
                    rows.Add(R4(r.Lv < 0 ? "3" : r.Lv.ToString(CultureInfo.InvariantCulture), "Verdict", r.Finding, r.Next.FirstOrDefault() ?? ""));
                    rows.Add(R4("-1", "What was checked", "", ""));
                    lock (r.Observed) foreach (var o in r.Observed.Take(60)) rows.Add(R4(o.Length > 1 ? o[1] : "3", o.Length > 2 ? o[2] : "", o.Length > 3 ? o[3] : "", ""));
                    if (r.Next.Count > 1) { rows.Add(R4("-1", "Next steps", "", "")); foreach (var n in r.Next.Take(6)) rows.Add(R4("3", "Next", n, "")); }
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; rows.Add(R4("2", "Didn't finish", e.Message, "")); }
                done(r.Title != "" ? r.Title : "Full checkup", rows, null);
            }, TaskCreationOptions.LongRunning);
        }
    }
}
