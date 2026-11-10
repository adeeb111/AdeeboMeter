// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;

namespace AdeeboMeter
{
    // More SNMP views: a snapshot of everything, find a MAC, VLANs, PoE, link aggregation, printers and UPSes.
    // Read-only like the rest (GET / GETBULK) - nothing on a device is ever changed.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "table headers and OID lists, built once per run")]
    static partial class SnmpTools
    {
        static long Sn(Snmp.Vb v) { if (v == null || v.Raw.Length == 0 || v.Raw.Length > 8) return -2; long n = v.Type == 2 && (v.Raw[0] & 0x80) != 0 ? -1 : 0; foreach (var b in v.Raw) n = n << 8 | b; return n; }
        static string I(long n) { return n.ToString(CultureInfo.InvariantCulture); }

        // ---------- Snapshot: overview, interfaces, CPU and memory, sensors, neighbours - one run, one deadline, one Copy / Save
        public static List<string[]> Snapshot(string dev, string cred, Toolkit.Run run)
        {
            run.Deadline = DateTime.UtcNow.AddSeconds(180);
            var rows = System(dev, cred);
            if (rows.Count == 0 || rows[0][0] == "2") return rows;
            rows[0][1] = "Snapshot of " + rows[0][1];
            keep = true;
            try
            {
                var parts = new[] { "INTERFACES", "CPU AND MEMORY", "SENSORS", "NEIGHBOURS (LLDP)" };
                for (int k = 0; k < parts.Length && !run.Stop; k++)
                {
                    run.Status = "Reading " + parts[k].ToLowerInvariant() + "...";
                    rows.Add(R(4, parts[k], "", "", "m"));
                    rows.AddRange(k == 0 ? Interfaces(dev, cred, run) : k == 1 ? CpuMem(dev, cred, run) : k == 2 ? Sensors(dev, cred, run) : Lldp(dev, cred, run));
                }
            }
            finally { keep = false; }
            return rows;
        }

        // names for ports: bridge port -> ifIndex -> ifName (falls back to ifDescr, then "port n")
        static Func<string, string> IfNames(IPEndPoint ep, Snmp.Cred c, Toolkit.Run run)
        {
            string e; var nm = Col(ep, c, "1.3.6.1.2.1.31.1.1.1.1", run, out e); if (nm.Count == 0) nm = Col(ep, c, "1.3.6.1.2.1.2.2.1.2", run, out e);
            return i => { string n = S(nm, i, "", null); return n != "" ? n : "if " + i; };
        }
        static Func<string, string> BridgeNames(IPEndPoint ep, Snmp.Cred c, Toolkit.Run run)
        {
            string e; var bp = Col(ep, c, "1.3.6.1.2.1.17.1.4.1.2", run, out e); var ifn = IfNames(ep, c, run);
            return p => { var b = V(bp, p); return b != null ? ifn(I((long)Snmp.Num(b))) : "port " + p; };
        }
        static List<int> PortList(Snmp.Vb v)
        {
            var r = new List<int>(); if (v == null) return r;
            for (int b = 0; b < v.Raw.Length && b < 512; b++) for (int k = 0; k < 8; k++) if ((v.Raw[b] & (0x80 >> k)) != 0) r.Add(b * 8 + k + 1);
            return r;
        }
        public static string NormMac(string s)
        {
            string h = new string((s ?? "").Where(ch => ch != ':' && ch != '-' && ch != '.' && ch != ' ').ToArray()).ToLowerInvariant();
            if (h.Length != 12 || h.Any(ch => !Uri.IsHexDigit(ch))) return null;
            return string.Join(":", Enumerable.Range(0, 6).Select(k => h.Substring(k * 2, 2)));
        }
        static string MacOf(string[] parts, int from) { if (parts.Length < from + 6) return null; var b = new byte[6]; for (int k = 0; k < 6; k++) { int n; if (!int.TryParse(parts[from + k], out n) || n < 0 || n > 255) return null; b[k] = (byte)n; } return string.Join(":", b.Select(x => x.ToString("x2", CultureInfo.InvariantCulture))); }

        // ---------- Find a MAC: which port a device is on (the switch's forwarding table, BRIDGE-MIB / Q-BRIDGE-MIB)
        public static List<string[]> FindMac(string dev, string cred, string what, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            string q = (what ?? "").Trim(), mac = null; IPAddress ip = null;
            if (q != "") { mac = NormMac(q); if (mac == null && !IPAddress.TryParse(q, out ip)) { rows.Add(R(2, "Find", "\"" + Draw.Trunc(q, 40) + "\" isn't a MAC (aa:bb:cc:dd:ee:ff) or an IP address", "", "u")); return rows; } }
            if (ip != null)
            {
                // an IP: the device's own ARP table turns it into a MAC (a router or layer-3 switch has one)
                var arp = Col(ep, c, "1.3.6.1.2.1.4.22.1.2", run, out err);
                foreach (var kv in arp) if (kv.Key.EndsWith("." + ip, StringComparison.Ordinal) && kv.Value.Raw.Length == 6) { mac = string.Join(":", kv.Value.Raw.Select(x => x.ToString("x2", CultureInfo.InvariantCulture))); break; }
                if (mac == null) { rows.Add(R(1, ip.ToString(), "this device doesn't have it in its ARP table", "A plain switch has no ARP table - ask the router or core switch for the IP, or type the MAC. A device that hasn't talked for a few minutes also drops out.", "m")); return rows; }
                rows.Add(R(3, ip.ToString(), "is " + mac + " (from the device's ARP table)", "", "m"));
            }
            bool qb = true; var fdb = Col(ep, c, "1.3.6.1.2.1.17.7.1.2.2.1.2", run, out err);
            if (fdb.Count == 0) { qb = false; fdb = Col(ep, c, "1.3.6.1.2.1.17.4.3.1.2", run, out err); }
            if (fdb.Count == 0)
            {
                if (err != "" && !err.StartsWith("SNMP error", StringComparison.Ordinal)) return Fail(ep.ToString(), err, c);
                rows.Add(R(3, ep.ToString(), "no MAC table offered (BRIDGE-MIB)", "Some switches show it per VLAN only: try the community as community@10 for VLAN 10. Routers and firewalls usually have none.", "m")); return rows;
            }
            var pn = BridgeNames(ep, c, run);
            var all = fdb.Select(kv => { var p = kv.Key.Split('.'); return new { Mac = MacOf(p, qb ? 1 : 0), Vlan = qb && p.Length > 0 ? p[0] : "", Port = I((long)Snmp.Num(kv.Value)) }; }).Where(e => e.Mac != null).ToList();
            var perPort = all.GroupBy(e => e.Port).ToDictionary(g => g.Key, g => g.Count());
            Func<string, string> portName = p => p == "0" ? "the switch itself" : pn(p);
            if (mac != null)
            {
                var hits = all.Where(e => e.Mac == mac).ToList();
                if (hits.Count == 0) { rows.Add(R(1, mac, "not in this switch's MAC table (" + all.Count + " entries)", "Switches forget a MAC after about 5 minutes of silence - ping the device, then run this again. Or it's behind another switch.", "m")); return rows; }
                foreach (var h in hits)
                {
                    int n; if (!perPort.TryGetValue(h.Port, out n)) n = 1;
                    rows.Add(R(n > 3 ? 1 : 0, mac, "on " + portName(h.Port) + (h.Vlan != "" ? " · VLAN " + h.Vlan : ""), n > 3 ? "That port carries " + n + " MACs - it's an uplink to another switch or access point. Run this on that switch to get the real port." : "The only device (or one of a few) on that port - this is where it's plugged in.", "m"));
                }
                return rows;
            }
            rows.Add(R(0, ep.ToString(), all.Count + " MACs on " + perPort.Count + " ports", "Ports with many MACs are uplinks to other switches or access points. Type a MAC or IP to find one device.", "m"));
            Table(rows, new[] { "Port", "MACs", "VLANs", "One of them" }, all.GroupBy(e => e.Port).OrderByDescending(g => g.Count()).Take(300).Select(g => new[] { portName(g.Key), I(g.Count()), string.Join(",", g.Select(e => e.Vlan).Where(v => v != "").Distinct().Take(6)), g.First().Mac }).ToList(), all.GroupBy(e => e.Port).Take(300).Select(g => false).ToList());
            Table(rows, new[] { "MAC", "VLAN", "Port" }, all.Take(1000).Select(e => new[] { e.Mac, e.Vlan, portName(e.Port) }).ToList(), all.Take(1000).Select(e => false).ToList());
            return rows;
        }

        // ---------- VLANs (Q-BRIDGE-MIB): names, tagged and untagged ports, each port's own VLAN
        public static List<string[]> Vlans(string dev, string cred, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            const string q = "1.3.6.1.2.1.17.7.1.4.";
            var name = Col(ep, c, q + "3.1.1", run, out err); var egr = Col(ep, c, q + "3.1.2", run, out err, "member ports"); var unt = Col(ep, c, q + "3.1.4", run, out err, "untagged ports");
            if (name.Count == 0 && egr.Count == 0)
            {
                if (err != "" && !err.StartsWith("SNMP error", StringComparison.Ordinal)) return Fail(ep.ToString(), err, c);
                // older Cisco switches keep VLANs in their own MIB (VTP): names, and each access port's VLAN
                var vtp = Col(ep, c, "1.3.6.1.4.1.9.9.46.1.3.1.1.4", run, out err);
                if (vtp.Count == 0) { rows.Add(R(3, ep.ToString(), "no VLAN table offered (Q-BRIDGE-MIB)", "The vendor may keep VLANs in its own MIB - search the MIB folder for vlan.", "m")); return rows; }
                var vm = Col(ep, c, "1.3.6.1.4.1.9.9.68.1.2.2.1.2", run, out err); var ifn0 = IfNames(ep, c, run);
                rows.Add(R(0, ep.ToString(), vtp.Count + " VLANs (VTP)", "Access ports and their VLAN below; trunks carry many VLANs and aren't listed per VLAN here.", "m"));
                Table(rows, new[] { "VLAN", "Name", "Access ports" }, vtp.Keys.OrderBy(k => k, Comparer<string>.Create(IdxCmp)).Take(500).Select(k => { string id = k.Split('.').Last(); return new[] { id, S(vtp, k, "", null), string.Join(", ", vm.Where(z => I((long)Snmp.Num(z.Value)) == id).Select(z => ifn0(z.Key)).Take(40)) }; }).ToList(), vtp.Keys.Take(500).Select(k => false).ToList());
                return rows;
            }
            var pvid = Col(ep, c, q + "5.1.1", run, out err); var pn = BridgeNames(ep, c, run);
            var ids = name.Keys.Union(egr.Keys).OrderBy(k => k, Comparer<string>.Create(IdxCmp)).ToList();
            var list = ids.Select(i =>
            {
                var e = PortList(V(egr, i)); var u = PortList(V(unt, i));
                return new { i, Name = S(name, i, "", null), Un = u.Select(p => pn(I(p))).ToList(), Tg = e.Except(u).Select(p => pn(I(p))).ToList(), Empty = V(egr, i) != null && e.Count == 0 };
            }).ToList();
            rows.Add(R(0, ep.ToString(), list.Count + " VLANs" + (pvid.Count > 0 ? " · " + pvid.Count + " ports with a native VLAN" : ""), "Untagged: the port's own VLAN (a PC or printer). Tagged: trunks to other switches, access points or servers. A VLAN with no ports carries nothing here.", "m"));
            Missing(rows);
            Table(rows, new[] { "VLAN", "Name", "Untagged", "Tagged" }, list.Take(500).Select(r => new[] { r.i, r.Name, r.Un.Count == 0 ? "-" : r.Un.Count + ": " + string.Join(", ", r.Un), r.Tg.Count == 0 ? "-" : r.Tg.Count + ": " + string.Join(", ", r.Tg) }).ToList(), list.Take(500).Select(r => r.Empty).ToList());
            foreach (var r in list.Take(500)) rows.Add(new[] { "8", "", "VLAN " + r.i + " " + r.Name + "  untagged: " + string.Join(", ", r.Un) + "  tagged: " + string.Join(", ", r.Tg), "", "" });   // every port - for Copy as text only
            if (pvid.Count > 0)
            {
                rows.Add(R(4, "Native VLAN per port (PVID)", pvid.Count + " ports", "Untagged traffic arriving on the port lands in this VLAN. A PVID that isn't one of the port's untagged VLANs is usually a mistake.", "m"));
                Table(rows, new[] { "Port", "Native VLAN" }, pvid.Keys.OrderBy(k => k, Comparer<string>.Create(IdxCmp)).Take(500).Select(p => new[] { pn(p), I((long)Snmp.Num(pvid[p])) }).ToList(), pvid.Keys.OrderBy(k => k, Comparer<string>.Create(IdxCmp)).Take(500).Select(p => { string v = I((long)Snmp.Num(pvid[p])); var vl = list.FirstOrDefault(r => r.i == v); return vl != null && vl.Un.Count > 0 && !vl.Un.Contains(pn(p)); }).ToList());
            }
            return rows;
        }

        // ---------- PoE (POWER-ETHERNET-MIB): the power budget, and each port's state - faults first
        public static List<string[]> Poe(string dev, string cred, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            const string p = "1.3.6.1.2.1.105.1.1.1.", m = "1.3.6.1.2.1.105.1.3.1.1.";
            var det = Col(ep, c, p + "6", run, out err);
            var pow = Col(ep, c, m + "2", run, out err, "power budget"); var mst = Col(ep, c, m + "3", run, out err, "power supply state"); var use = Col(ep, c, m + "4", run, out err, "power in use");
            if (det.Count == 0 && pow.Count == 0)
            {
                if (err != "" && !err.StartsWith("SNMP error", StringComparison.Ordinal)) return Fail(ep.ToString(), err, c);
                if (missing != null) missing.Clear();
                rows.Add(R(3, ep.ToString(), "no PoE table offered (POWER-ETHERNET-MIB)", "Not a PoE switch, or the vendor keeps it in its own MIB - search the MIB folder for poe or power.", "m")); return rows;
            }
            var adm = Col(ep, c, p + "3", run, out err); var cls = Col(ep, c, p + "10", run, out err);
            string[] dets = { "", "off", "searching", "powering", "FAULT", "test", "FAULT (other)" };
            foreach (var g in pow.Keys)
            {
                long w = L(pow, g), u = L(use, g), st = L(mst, g);
                int lv = st == 3 ? 2 : w > 0 && u >= 0 && u * 100 >= w * 90 ? 1 : 0;
                rows.Add(R(lv, "Power supply" + (pow.Count > 1 ? " " + g : ""), (st == 1 ? "on" : st == 2 ? "off" : st == 3 ? "FAULTY" : "state n/a") + " · " + (u >= 0 ? I(u) : "n/a") + " of " + I(w) + " W used" + (w > 0 && u >= 0 ? " (" + I(u * 100 / w) + "%)" : ""), lv == 1 ? "Nearly out of PoE budget - a new phone or access point may not power up." : "", "m"));
            }
            var list = det.Keys.Select(i =>
            {
                long d = L(det, i), a = L(adm, i), k = L(cls, i);
                return new { i, Port = pow.Count > 1 ? i.Replace('.', '/') : i.Split('.').Last(), State = d >= 0 && d < dets.Length ? dets[d] : "n/a", Adm = a == 1 ? "on" : a == 2 ? "off" : "n/a", Cls = k >= 1 && k <= 5 ? "class " + (k - 1) : "-", Bad = d == 4 || d == 6 };
            }).OrderBy(r => r.Bad ? 0 : r.State == "powering" ? 1 : 2).ThenBy(r => r.i, Comparer<string>.Create(IdxCmp)).ToList();
            rows.Add(R(list.Any(r => r.Bad) ? 2 : 0, ep.ToString(), list.Count(r => r.State == "powering") + " ports powering a device · " + list.Count(r => r.Bad) + " faults", "A fault is often a damaged cable or a device asking for more power than the port gives. Port numbers are the PoE table's own (group/port).", "m"));
            Missing(rows);
            Table(rows, new[] { "Port", "PoE", "State", "Class" }, list.Take(500).Select(r => new[] { r.Port, r.Adm, r.State, r.Cls }).ToList(), list.Take(500).Select(r => r.Bad).ToList());
            return rows;
        }

        // ---------- Link aggregation (IEEE8023-LAG-MIB / LACP): is every member in its bundle, and to the same partner?
        public static string LacpState(byte b)
        {
            var f = new List<string>();
            if ((b & 0x10) == 0) f.Add("not in sync");
            if ((b & 0x08) == 0) f.Add("not collecting");
            if ((b & 0x04) == 0) f.Add("not distributing");
            if ((b & 0x02) != 0) f.Add("no partner (defaulted)");
            if ((b & 0x01) != 0) f.Add("partner timed out");
            return f.Count == 0 ? "in bundle" : string.Join(", ", f);
        }
        public static List<string[]> Lag(string dev, string cred, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            const string a = "1.2.840.10006.300.43.1.2.1.1.";
            var att = Col(ep, c, a + "13", run, out err);   /* dot3adAggPortAttachedAggID */
            if (att.Count == 0)
            {
                if (err != "" && !err.StartsWith("SNMP error", StringComparison.Ordinal)) return Fail(ep.ToString(), err, c);
                // no LACP table: the interface stack still shows which ports sit under a LAG interface (type 161)
                var type = Col(ep, c, "1.3.6.1.2.1.2.2.1.3", run, out err); var stack = Col(ep, c, "1.3.6.1.2.1.31.1.2.1.3", run, out err); var op = Col(ep, c, "1.3.6.1.2.1.2.2.1.8", run, out err);
                var lags = new HashSet<string>(type.Where(kv => Snmp.Num(kv.Value) == 161).Select(kv => kv.Key));
                if (lags.Count == 0) { rows.Add(R(3, ep.ToString(), "no link aggregation found (no LACP table, no LAG interfaces)", "", "m")); return rows; }
                var ifn0 = IfNames(ep, c, run);
                var mem = stack.Keys.Select(k => k.Split('.')).Where(z => z.Length == 2 && lags.Contains(z[0]) && z[1] != "0").Select(z => new { Lag = z[0], If = z[1], Up = L(op, z[1]) == 1, Known = L(op, z[1]) >= 0 }).ToList();
                rows.Add(R(mem.Any(z => z.Known && !z.Up) ? 1 : 0, ep.ToString(), lags.Count + " LAGs · " + mem.Count + " member ports · " + mem.Count(z => z.Known && !z.Up) + " down", "From the interface stack - this device doesn't offer the LACP table, so only up / down is known, not whether each member is really in the bundle.", "m"));
                Table(rows, new[] { "LAG", "Member", "Member link" }, mem.Take(500).Select(z => new[] { ifn0(z.Lag), ifn0(z.If), z.Known ? (z.Up ? "up" : "down") : "n/a" }).ToList(), mem.Take(500).Select(z => z.Known && !z.Up).ToList());
                return rows;
            }
            var sel = Col(ep, c, a + "12", run, out err); var act = Col(ep, c, a + "21", run, out err, "member state"); var psys = Col(ep, c, a + "9", run, out err, "partner");   /* SelectedAggID, ActorOperState, PartnerOperSystemID */ var ifn = IfNames(ep, c, run);
            var list = att.Keys.Select(i =>
            {
                long at = L(att, i), se = L(sel, i); var s = V(act, i); string ps = V(psys, i) != null ? Snmp.Text(V(psys, i), "", null) : "n/a";
                string st = s != null && s.Raw.Length > 0 ? LacpState(s.Raw[0]) : "n/a";
                return new { i, Lag = at > 0 ? ifn(I(at)) : se > 0 ? ifn(I(se)) : "-", Agg = at > 0 ? I(at) : se > 0 ? I(se) : "", Attached = at > 0, St = st, Ps = ps };
            }).ToList();
            // a member whose partner differs from the rest of its bundle is cabled to the wrong box
            var partner = list.Where(r => r.Agg != "" && r.Ps != "n/a").GroupBy(r => r.Agg).ToDictionary(g => g.Key, g => g.GroupBy(r => r.Ps).OrderByDescending(h => h.Count()).First().Key);
            var shown = list.Where(r => r.Agg != "" || r.St != "n/a").Select(r => new { r.i, r.Lag, r.St, r.Ps, Odd = r.Agg != "" && partner.ContainsKey(r.Agg) && r.Ps != "n/a" && r.Ps != partner[r.Agg], Bad = r.St != "in bundle" && r.St != "n/a" || !r.Attached })
                .Where(r => r.Lag != "-").OrderBy(r => r.Bad || r.Odd ? 0 : 1).ThenBy(r => r.Lag).ThenBy(r => r.i, Comparer<string>.Create(IdxCmp)).ToList();
            if (shown.Count == 0) { rows.Add(R(3, ep.ToString(), "LACP is offered but no port is in a bundle", "", "m")); return rows; }
            int nBad = shown.Count(r => r.Bad || r.Odd);
            rows.Add(R(nBad > 0 ? 1 : 0, ep.ToString(), shown.Select(r => r.Lag).Distinct().Count() + " LAGs · " + shown.Count + " members · " + nBad + " not right", "A member that isn't in sync, collecting and distributing carries no traffic. A partner different from the other members means that cable goes to a different switch.", "m"));
            Missing(rows);
            Table(rows, new[] { "LAG", "Member", "State", "Partner" }, shown.Take(500).Select(r => new[] { r.Lag, ifn(r.i), r.St + (r.Odd ? " · DIFFERENT PARTNER" : ""), r.Ps }).ToList(), shown.Take(500).Select(r => r.Bad || r.Odd).ToList());
            return rows;
        }

        // ---------- Printer (Printer-MIB, HOST-RESOURCES-MIB): state, errors, supplies, what the panel says
        static readonly string[] PrnErr0 = { "low paper", "no paper", "low toner", "no toner", "door open", "paper jam", "offline", "needs service" };
        static readonly string[] PrnErr1 = { "input tray missing", "output tray missing", "supply missing", "output nearly full", "output full", "input tray empty", "maintenance overdue", "" };
        public static List<string[]> Printer(string dev, string cred, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            var st = Col(ep, c, "1.3.6.1.2.1.25.3.5.1.1", run, out err); var es = Col(ep, c, "1.3.6.1.2.1.25.3.5.1.2", run, out err);
            const string s = "1.3.6.1.2.1.43.11.1.1.";
            var sd = Col(ep, c, s + "6", run, out err);
            if (st.Count == 0 && sd.Count == 0)
            {
                if (err != "" && !err.StartsWith("SNMP error", StringComparison.Ordinal)) return Fail(ep.ToString(), err, c);
                rows.Add(R(3, ep.ToString(), "not a printer, or it doesn't offer the Printer MIB", "Most network printers answer with the community public.", "m")); return rows;
            }
            string[] sts = { "", "other", "unknown", "idle", "printing", "warming up" };
            foreach (var k in st.Keys)
            {
                long v = L(st, k); var e = V(es, k); var errs = new List<string>();
                if (e != null) for (int b = 0; b < e.Raw.Length && b < 2; b++) for (int bit = 0; bit < 8; bit++) if ((e.Raw[b] & (0x80 >> bit)) != 0) { string t = (b == 0 ? PrnErr0 : PrnErr1)[bit]; if (t != "") errs.Add(t); }
                bool hard = errs.Any(z => z.StartsWith("no ", StringComparison.Ordinal) || z == "paper jam" || z == "door open" || z == "offline" || z == "output full" || z == "needs service");
                rows.Add(R(hard ? 2 : errs.Count > 0 ? 1 : 0, "Printer", (v >= 0 && v < sts.Length ? sts[v] : "state n/a") + (errs.Count > 0 ? " · " + string.Join(", ", errs) : e == null ? " · errors n/a" : " · no errors"), "", "m"));
            }
            var lines = Col(ep, c, "1.3.6.1.2.1.43.16.5.1.2", run, out err);
            string panel = string.Join(" | ", lines.Values.Select(v => Snmp.Text(v, "", null)).Where(z => z.Trim() != "").Take(4));
            if (panel != "") rows.Add(R(3, "Panel says", panel, "", "m"));
            var life = Col(ep, c, "1.3.6.1.2.1.43.10.2.1.4", run, out err);
            if (life.Count > 0) rows.Add(R(3, "Pages printed", I(life.Values.Sum(v => (long)Snmp.Num(v))), "Over the printer's life.", "m"));
            var max = Col(ep, c, s + "8", run, out err); var lvl = Col(ep, c, s + "9", run, out err);
            foreach (var k in sd.Keys.OrderBy(z => z, Comparer<string>.Create(IdxCmp)).Take(30))
            {
                long mx = Sn(V(max, k)), lv = Sn(V(lvl, k));   // -3 some left, -2 unknown, -1 other; missing reads as -2
                string val; int lvS = 3;
                if (lv == -3) val = "some left"; else if (lv == -2 || lv == -1 || lv < -3) val = "level unknown"; else if (mx > 0) { long pct = lv * 100 / mx; val = I(pct) + "%"; lvS = pct <= 5 ? 2 : pct <= 15 ? 1 : 0; } else val = "n/a";
                rows.Add(R(lvS, S(sd, k, "", null), val, "", "m"));
            }
            var al = Col(ep, c, "1.3.6.1.2.1.43.18.1.1.8", run, out err);
            foreach (var v in al.Values.Take(10)) { string t = Snmp.Text(v, "", null); if (t.Trim() != "") rows.Add(R(1, "Alert", t, "", "m")); }
            return rows;
        }

        // ---------- UPS (UPS-MIB, and APC's own when that isn't offered): on mains or battery, charge, minutes left, load
        static Snmp.Vb G(List<Snmp.Vb> v, int i) { return v != null && i < v.Count && !v[i].End ? v[i] : null; }
        static long Gl(List<Snmp.Vb> v, int i) { var x = G(v, i); return x == null || x.Type == 4 ? -1 : (long)Snmp.Num(x); }
        public static List<string[]> Ups(string dev, string cred, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            const string u = "1.3.6.1.2.1.33.1.";
            var v = Snmp.Request(ep, c, 0xA0, 0, 0, new[] { u + "1.1.0", u + "1.2.0", u + "2.1.0", u + "2.2.0", u + "2.3.0", u + "2.4.0", u + "2.7.0", u + "4.1.0", u + "6.1.0" }, out err);
            if (v == null) return Fail(ep.ToString(), err, c);
            string model; long bat, onBat, mins, chg, temp, src, alarms, load = -1;
            if (G(v, 2) != null || G(v, 7) != null)
            {
                model = ((G(v, 0) != null ? Snmp.Text(G(v, 0), "", null) + " " : "") + (G(v, 1) != null ? Snmp.Text(G(v, 1), "", null) : "")).Trim();
                bat = Gl(v, 2); onBat = Gl(v, 3); mins = Gl(v, 4); chg = Gl(v, 5); temp = Gl(v, 6); src = Gl(v, 7); alarms = Gl(v, 8);
                var ld = Col(ep, c, u + "4.4.1.5", run, out err); if (ld.Count > 0) load = ld.Values.Max(x => (long)Snmp.Num(x));
            }
            else
            {
                // APC's own MIB (PowerNet): many APC cards answer only this
                const string a = "1.3.6.1.4.1.318.1.1.1.";
                var w = Snmp.Request(ep, c, 0xA0, 0, 0, new[] { a + "1.1.1.0", a + "2.1.1.0", a + "2.2.1.0", a + "2.2.3.0", a + "4.1.1.0", a + "4.2.3.0", a + "2.2.2.0" }, out err);
                if (w == null) return Fail(ep.ToString(), err, c);
                if (G(w, 1) == null && G(w, 4) == null) { rows.Add(R(3, ep.ToString(), "not a UPS, or it offers neither UPS-MIB nor APC's MIB", "Search the MIB folder for battery to find the vendor's own table.", "m")); return rows; }
                model = G(w, 0) != null ? Snmp.Text(G(w, 0), "", null) : "";
                long ab = Gl(w, 1); bat = ab == 2 ? 2 : ab == 3 ? 3 : ab == 4 ? 4 : 1; chg = Gl(w, 2); long rt = Gl(w, 3); mins = rt >= 0 ? rt / 6000 : -1; temp = Gl(w, 6); load = Gl(w, 5); alarms = -1;
                long os = Gl(w, 4); src = os == 2 || os == 12 || os == 4 ? 3 : os == 3 ? 5 : os == 6 || os == 9 || os == 10 ? 4 : os == 7 ? 2 : os < 0 ? -1 : 1; onBat = -1;
            }
            if (model != "") rows.Add(R(3, "UPS", model, "", "m"));
            string[] srcs = { "", "other", "OFF - no output", "on mains", "on bypass (not protected)", "ON BATTERY", "boosting low mains", "trimming high mains" };
            rows.Add(R(src == 5 || src == 2 ? 2 : src == 4 ? 1 : src < 0 ? 3 : 0, "Power", (src >= 1 && src < srcs.Length ? srcs[src] : "n/a") + (src == 5 && onBat > 0 ? " for " + I(onBat) + " s" : ""), src == 5 ? "The mains is out or the UPS can't use it - plan for the minutes left below." : "", "m"));
            string[] bats = { "", "unknown", "normal", "LOW", "DEPLETED / fault" };
            rows.Add(R(bat >= 3 ? 2 : bat == 2 ? 0 : 3, "Battery", (bat >= 1 && bat <= 4 ? bats[bat] : "n/a") + (chg >= 0 ? " · " + I(chg) + "% charged" : "") + (temp >= 0 ? " · " + I(temp) + " °C" : ""), bat >= 3 ? "Replace or recharge - it won't carry the load through an outage." : "", "m"));
            rows.Add(R(mins >= 0 && mins < 10 ? 2 : mins >= 0 && mins < 20 ? 1 : mins < 0 ? 3 : 0, "Runtime left", mins >= 0 ? I(mins) + " min at the present load" : "n/a", "", "m"));
            rows.Add(R(load > 90 ? 2 : load > 75 ? 1 : load < 0 ? 3 : 0, "Load", load >= 0 ? I(load) + "%" : "n/a", load > 75 ? "Near the UPS's limit - a busy moment can trip it, and runtime is short." : "", "m"));
            if (alarms >= 0) rows.Add(R(alarms > 0 ? 1 : 0, "Alarms", alarms == 0 ? "none" : I(alarms) + " active", alarms > 0 ? "Walk upsAlarmTable (1.3.6.1.2.1.33.1.6.2) to see them." : "", "m"));
            return rows;
        }
    }
}
