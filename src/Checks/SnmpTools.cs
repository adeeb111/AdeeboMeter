// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace AdeeboMeter
{
    // The SNMP views: questions an engineer asks, answered from read-only GET / GETBULK, problems first.
    // Rows with level "6" are a monospaced table line, "7" the same line marked as trouble.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "table headers and OID lists, built once per run")]
    static class SnmpTools
    {
        static Snmp.Vb V(Dictionary<string, Snmp.Vb> d, string i) { Snmp.Vb v; return d.TryGetValue(i, out v) ? v : null; }
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string[] Line(string text, bool trouble) { return new[] { trouble ? "7" : "6", "", text, "", "" }; }
        const string Hint = "Read-only: AdeeboMeter only reads (GET / GETBULK) - it never changes a device. v2c sends the community in clear text; v3 with auth + privacy doesn't.";

        static bool Target(string dev, out IPEndPoint ep, out string err)
        {
            ep = null; err = ""; string h = (dev ?? "").Trim(); int port = 161;
            int c = h.LastIndexOf(':'); if (c > 0 && h.IndexOf(':') == c) { int p; if (int.TryParse(h.Substring(c + 1), out p) && p > 0 && p < 65536) port = p; h = h.Substring(0, c); }
            if (h == "") { err = "type the device's address"; return false; }
            IPAddress a; if (!IPAddress.TryParse(h.Trim('[', ']'), out a)) { try { a = Dns.GetHostAddresses(h).FirstOrDefault(); } catch { a = null; } }
            if (a == null) { err = "couldn't find " + h; return false; }
            ep = new IPEndPoint(a, port); return true;
        }
        static List<string[]> Fail(string what, string err, Snmp.Cred c)
        {
            return new List<string[]> { R(2, what, err, err.StartsWith("no answer", StringComparison.Ordinal) ? (c.Version == 2 ? "Wrong community, SNMP off, an access list that doesn't include this PC, or UDP 161 blocked - a device stays silent in all four cases." : (c.Priv ? "With v3 privacy, a wrong privacy password looks exactly like this - the device can't decrypt the request and stays silent. Otherwise: " : "") + "SNMP v3 not enabled for this PC, an access list, or UDP 161 blocked.") : "", "m") };
        }
        static Dictionary<string, Snmp.Vb> Col(IPEndPoint ep, Snmp.Cred c, string oid, Toolkit.Run run, out string err)
        {
            var d = new Dictionary<string, Snmp.Vb>(); var vbs = Snmp.Walk(ep, c, oid, 2000, run, out err);
            if (vbs != null) foreach (var v in vbs) d[v.Oid.Substring(oid.Length + 1)] = v;
            return d;
        }
        static string S(Dictionary<string, Snmp.Vb> col, string i, string name, MibIndex.Entry e) { Snmp.Vb v; return col.TryGetValue(i, out v) ? Snmp.Text(v, name, e) : ""; }
        static int IdxCmp(string a, string b) { var x = a.Split('.'); var y = b.Split('.'); for (int i = 0; i < Math.Min(x.Length, y.Length); i++) { long p, q; if (!long.TryParse(x[i], out p)) p = 0; if (!long.TryParse(y[i], out q)) q = 0; if (p != q) return p.CompareTo(q); } return x.Length.CompareTo(y.Length); }

        // a fixed-width table: the header, then rows; columns as wide as their longest value (capped)
        static void Table(List<string[]> rows, string[] head, List<string[]> cells, List<bool> trouble)
        {
            var w = head.Select((h, i) => Math.Min(32, Math.Max(h.Length, cells.Count == 0 ? 0 : cells.Max(r => i < r.Length ? r[i].Length : 0)))).ToArray();
            Func<string[], string> fmt = r => string.Join("  ", r.Select((v, i) => { v = v ?? ""; if (v.Length > w[i]) v = v.Substring(0, w[i] - 1) + "…"; return v.PadRight(w[i]); })).TrimEnd();
            rows.Add(Line(fmt(head), false));
            for (int i = 0; i < cells.Count; i++) rows.Add(Line(fmt(cells[i]), trouble[i]));
        }

        // ---------- System
        public static List<string[]> System(string dev, string cred)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            var sw = global::System.Diagnostics.Stopwatch.StartNew();
            var v = Snmp.Request(ep, c, 0xA0, 0, 0, new[] { "1.3.6.1.2.1.1.5.0", "1.3.6.1.2.1.1.1.0", "1.3.6.1.2.1.1.3.0", "1.3.6.1.2.1.1.6.0", "1.3.6.1.2.1.1.4.0", "1.3.6.1.2.1.1.2.0" }, out err);
            if (v == null) return Fail(ep.ToString(), err, c);
            rows.Add(R(0, ep + " (" + sw.ElapsedMilliseconds + " ms)", "answered SNMP " + c.Label(), Hint, "m"));
            var names = new[] { "Name", "Description", "Up for", "Location", "Contact", "Object ID" };
            for (int i = 0; i < v.Count && i < names.Length; i++) rows.Add(R(3, names[i], Snmp.Text(v[i], i == 2 ? "sysUpTime" : "", null), i == 5 ? "The vendor's product ID - add its MIB files to the MIB folder to name it." : "", "m"));
            return rows;
        }

        // ---------- Interfaces: status, speed, errors and discards, problems first
        public static List<string[]> Interfaces(string dev, string cred, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            const string t = "1.3.6.1.2.1.2.2.1.", x = "1.3.6.1.2.1.31.1.1.1.";
            var descr = Col(ep, c, t + "2", run, out err); if (descr.Count == 0) return Fail(ep.ToString(), err == "" ? "no interfaces table (IF-MIB) on this device" : err, c);
            var name = Col(ep, c, x + "1", run, out err); var alias = Col(ep, c, x + "18", run, out err); var hs = Col(ep, c, x + "15", run, out err); var spd = Col(ep, c, t + "5", run, out err);
            var adm = Col(ep, c, t + "7", run, out err); var op = Col(ep, c, t + "8", run, out err);
            var ie = Col(ep, c, t + "14", run, out err); var oe = Col(ep, c, t + "20", run, out err); var idc = Col(ep, c, t + "13", run, out err); var odc = Col(ep, c, t + "19", run, out err);
            var ud = MibIndex.BuiltinOnly(); MibIndex.Entry upd; ud.Column(t + "8.1", out upd, out err);
            var list = descr.Keys.Select(i =>
            {
                string a = S(adm, i, "", upd), o = S(op, i, "", upd); ulong sp = V(hs, i) != null ? Snmp.Num(V(hs, i)) * 1000000UL : Snmp.Num(V(spd, i));
                ulong e1 = Snmp.Num(V(ie, i)), e2 = Snmp.Num(V(oe, i)), d1 = Snmp.Num(V(idc, i)), d2 = Snmp.Num(V(odc, i));
                bool down = a == "up" && o != "up";
                return new { i, Name = S(name, i, "", null) != "" ? S(name, i, "", null) : S(descr, i, "", null), a, o, sp, e1, e2, d1, d2, Alias = S(alias, i, "", null), down, errs = e1 + e2 > 0 };
            }).OrderBy(r => r.down ? 0 : r.errs ? 1 : 2).ThenBy(r => r.i, Comparer<string>.Create(IdxCmp)).ToList();
            int nDown = list.Count(r => r.down), nErr = list.Count(r => r.errs);
            rows.Add(R(nDown > 0 ? 1 : nErr > 0 ? 1 : 0, ep.ToString(), list.Count + " interfaces · " + list.Count(r => r.o == "up") + " up · " + nDown + " admin up but down · " + nErr + " with errors", "Errors and discards are counted since the device or interface started - run it again in a few minutes and Compare to see if they're still growing. " + Hint, "m"));
            Func<ulong, string> sps = v => v == 0 ? "-" : v >= 1000000000UL ? (v / 1000000000UL) + "G" : (v / 1000000UL) + "M";
            Table(rows, new[] { "Interface", "Admin", "Oper", "Speed", "In err", "Out err", "In disc", "Out disc", "Description" },
                list.Take(400).Select(r => new[] { r.Name, r.a, r.o, sps(r.sp), r.e1.ToString(CultureInfo.InvariantCulture), r.e2.ToString(CultureInfo.InvariantCulture), r.d1.ToString(CultureInfo.InvariantCulture), r.d2.ToString(CultureInfo.InvariantCulture), r.Alias }).ToList(),
                list.Take(400).Select(r => r.down || r.errs).ToList());
            return rows;
        }

        // ---------- Traffic: the 64-bit counters twice, 5 seconds apart -> rate per port, busiest first
        public static List<string[]> Traffic(string dev, string cred, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            const string x = "1.3.6.1.2.1.31.1.1.1."; bool hc = true;
            var in1 = Col(ep, c, x + "6", run, out err); var out1 = Col(ep, c, x + "10", run, out err);
            if (in1.Count == 0) { hc = false; in1 = Col(ep, c, "1.3.6.1.2.1.2.2.1.10", run, out err); out1 = Col(ep, c, "1.3.6.1.2.1.2.2.1.16", run, out err); }
            if (in1.Count == 0) return Fail(ep.ToString(), err == "" ? "no traffic counters (IF-MIB) on this device" : err, c);
            var t0 = DateTime.UtcNow; for (int i = 0; i < 50 && !run.Stop; i++) { run.Status = "Measuring for 5 s..."; Thread.Sleep(100); }
            var in2 = Col(ep, c, hc ? x + "6" : "1.3.6.1.2.1.2.2.1.10", run, out err); var out2 = Col(ep, c, hc ? x + "10" : "1.3.6.1.2.1.2.2.1.16", run, out err); double secs = (DateTime.UtcNow - t0).TotalSeconds;
            var name = Col(ep, c, x + "1", run, out err); var hs = Col(ep, c, x + "15", run, out err);
            Func<Dictionary<string, Snmp.Vb>, Dictionary<string, Snmp.Vb>, string, double> rate = (a, b, i) =>
            {
                Snmp.Vb va = V(a, i), vb2 = V(b, i); if (va == null || vb2 == null) return -1; ulong p = Snmp.Num(va), q = Snmp.Num(vb2);
                ulong d = q >= p ? q - p : hc ? 0 : (uint.MaxValue - p + q + 1); return d * 8.0 / secs;   // a 32-bit counter can wrap once
            };
            var list = in1.Keys.Select(i => new { i, Name = S(name, i, "", null) != "" ? S(name, i, "", null) : "if " + i, In = rate(in1, in2, i), Out = rate(out1, out2, i), Sp = V(hs, i) != null ? Snmp.Num(V(hs, i)) * 1e6 : 0 })
                .Where(r => r.In >= 0).OrderByDescending(r => Math.Max(r.In, r.Out)).ToList();
            Func<double, string> bps = v => v >= 1e9 ? (v / 1e9).ToString("0.00", CultureInfo.CurrentCulture) + " Gb/s" : v >= 1e6 ? (v / 1e6).ToString("0.0", CultureInfo.CurrentCulture) + " Mb/s" : (v / 1e3).ToString("0", CultureInfo.CurrentCulture) + " kb/s";
            rows.Add(R(0, ep.ToString(), list.Count + " interfaces over " + secs.ToString("0.0", CultureInfo.CurrentCulture) + " s" + (hc ? "" : " (32-bit counters - fast links can wrap)"), "Busiest first. A port near 100% of its speed is the bottleneck.", "m"));
            Table(rows, new[] { "Interface", "In", "Out", "Busiest %" }, list.Take(40).Select(r => new[] { r.Name, bps(r.In), bps(r.Out), r.Sp > 0 ? (Math.Max(r.In, r.Out) * 100 / r.Sp).ToString("0", CultureInfo.CurrentCulture) + "%" : "-" }).ToList(),
                list.Take(40).Select(r => r.Sp > 0 && Math.Max(r.In, r.Out) > r.Sp * 0.8).ToList());
            return rows;
        }

        // ---------- CPU and memory (HOST-RESOURCES-MIB)
        public static List<string[]> CpuMem(string dev, string cred, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            var cpu = Col(ep, c, "1.3.6.1.2.1.25.3.3.1.2", run, out err);
            const string st = "1.3.6.1.2.1.25.2.3.1.";
            var type = Col(ep, c, st + "2", run, out err);
            if (cpu.Count == 0 && type.Count == 0)
            {
                if (err != "" && !err.StartsWith("SNMP error", StringComparison.Ordinal)) return Fail(ep.ToString(), err, c);
                rows.Add(R(3, ep.ToString(), "this device doesn't offer the standard CPU / memory tables (HOST-RESOURCES-MIB)", "Network vendors usually have their own: search the MIB folder for cpu or memory, then walk what it finds.", "m")); return rows;
            }
            if (cpu.Count > 0) { var loads = cpu.Values.Select(v => (double)Snmp.Num(v)).ToList(); rows.Add(R(loads.Max() > 85 ? 2 : loads.Average() > 60 ? 1 : 0, "Processor", loads.Count + " cores · average " + loads.Average().ToString("0", CultureInfo.CurrentCulture) + "% · busiest " + loads.Max().ToString("0", CultureInfo.CurrentCulture) + "%", "Averaged over the last minute by the device itself.", "m")); }
            var descr = Col(ep, c, st + "3", run, out err); var unit = Col(ep, c, st + "4", run, out err); var size = Col(ep, c, st + "5", run, out err); var used = Col(ep, c, st + "6", run, out err);
            foreach (var i in type.Keys.OrderBy(k => k, Comparer<string>.Create(IdxCmp)).Take(20))
            {
                string ty = Snmp.Text(type[i], "", null); string kind = ty.EndsWith(".2", StringComparison.Ordinal) ? "Memory" : ty.EndsWith(".3", StringComparison.Ordinal) ? "Virtual memory" : ty.EndsWith(".4", StringComparison.Ordinal) ? "Disk" : ty.EndsWith(".9", StringComparison.Ordinal) ? "Flash" : "Storage";
                double u = Snmp.Num(V(unit, i)), s = Snmp.Num(V(size, i)) * u, us = Snmp.Num(V(used, i)) * u;
                if (s <= 0) continue; double pct = us * 100 / s;
                // only real RAM, disks and flash get a colour: "other" entries (cached, shared, buffers) are full by design
                int lvS = kind == "Memory" ? (pct > 97 ? 2 : pct > 90 ? 1 : 3) : kind == "Disk" || kind == "Flash" ? (pct > 95 ? 2 : pct > 85 ? 1 : 3) : 3;
                rows.Add(R(lvS, kind + " · " + S(descr, i, "", null), (us / 1048576).ToString("0", CultureInfo.CurrentCulture) + " of " + (s / 1048576).ToString("0", CultureInfo.CurrentCulture) + " MB used (" + pct.ToString("0", CultureInfo.CurrentCulture) + "%)", "", "m"));
            }
            return rows;
        }

        // ---------- Neighbours (LLDP): who is on which port
        public static List<string[]> Lldp(string dev, string cred, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            const string r = "1.0.8802.1.1.2.1.4.1.1.", l = "1.0.8802.1.1.2.1.3.7.1.";
            var sys = Col(ep, c, r + "9", run, out err);
            if (sys.Count == 0) { if (err != "" && !err.StartsWith("SNMP error", StringComparison.Ordinal)) return Fail(ep.ToString(), err, c); rows.Add(R(3, ep.ToString(), "no LLDP neighbours (or LLDP-MIB isn't offered)", "LLDP may be off on the device or its ports.", "m")); return rows; }
            var port = Col(ep, c, r + "7", run, out err); var pdesc = Col(ep, c, r + "8", run, out err); var chassis = Col(ep, c, r + "5", run, out err);
            var lport = Col(ep, c, l + "4", run, out err); var lpid = Col(ep, c, l + "3", run, out err);
            var list = sys.Keys.Select(i => { var p = i.Split('.'); string lp = p.Length >= 3 ? p[p.Length - 2] : ""; string ln = S(lport, lp, "", null); if (ln == "") ln = S(lpid, lp, "", null); if (ln == "") ln = "port " + lp; return new[] { ln, S(sys, i, "", null), S(port, i, "", null), S(pdesc, i, "", null), S(chassis, i, "", null) }; })
                .OrderBy(a => a[0]).ToList();
            rows.Add(R(0, ep.ToString(), list.Count + " neighbours", "From what the neighbours announce over LLDP.", "m"));
            Table(rows, new[] { "Our port", "Neighbour", "Their port", "Their port description", "Chassis ID" }, list.Take(300).ToList(), list.Take(300).Select(a => false).ToList());
            return rows;
        }

        // ---------- Sensors (ENTITY-SENSOR-MIB): temperatures, optics, voltages, fans
        static readonly string[] SensorType = { "", "other", "unknown", "V AC", "V DC", "A", "W", "Hz", "°C", "%RH", "rpm", "cmm", "true/false", "special", "dBm" };
        public static List<string[]> Sensors(string dev, string cred, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            const string s = "1.3.6.1.2.1.99.1.1.1.";
            var val = Col(ep, c, s + "4", run, out err);
            if (val.Count == 0) { if (err != "" && !err.StartsWith("SNMP error", StringComparison.Ordinal)) return Fail(ep.ToString(), err, c); rows.Add(R(3, ep.ToString(), "this device doesn't offer the standard sensor table (ENTITY-SENSOR-MIB)", "Search the MIB folder for temperature or optic to find the vendor's own table.", "m")); return rows; }
            var type = Col(ep, c, s + "1", run, out err); var scale = Col(ep, c, s + "2", run, out err); var prec = Col(ep, c, s + "3", run, out err); var stat = Col(ep, c, s + "5", run, out err);
            var name = Col(ep, c, "1.3.6.1.2.1.47.1.1.1.1.7", run, out err); var descr = Col(ep, c, "1.3.6.1.2.1.47.1.1.1.1.2", run, out err);
            var list = val.Keys.Select(i =>
            {
                long ty = (V(type, i) != null ? (long)Snmp.Num(V(type, i)) : 2), sc = (V(scale, i) != null ? (long)Snmp.Num(V(scale, i)) : 9), pr = (V(prec, i) != null ? (long)Snmp.Num(V(prec, i)) : 0), stt = (V(stat, i) != null ? (long)Snmp.Num(V(stat, i)) : 1);
                long raw = 0; var rv = val[i].Raw; if (rv.Length > 0 && rv.Length <= 8) { raw = (rv[0] & 0x80) != 0 ? -1 : 0; foreach (var b in rv) raw = raw << 8 | b; }
                double v = raw * Math.Pow(10, (sc - 9) * 3) / Math.Pow(10, pr);
                string nm = S(name, i, "", null); if (nm == "") nm = S(descr, i, "", null); if (nm == "") nm = "sensor " + i;
                string unit = ty >= 0 && ty < SensorType.Length ? SensorType[ty] : "";
                bool bad = stt != 1 || (ty == 8 && v > 75);
                return new { nm, Text = v.ToString(Math.Abs(v) < 10 ? "0.00" : "0.0", CultureInfo.CurrentCulture) + " " + unit, State = stt == 1 ? "ok" : stt == 2 ? "unavailable" : stt == 3 ? "non-operational" : "?", bad, ty };
            }).OrderBy(r => r.bad ? 0 : 1).ThenBy(r => r.ty).ThenBy(r => r.nm).ToList();
            rows.Add(R(list.Any(r => r.bad) ? 1 : 0, ep.ToString(), list.Count + " sensors", "Temperatures over 75 °C and sensors that aren't 'ok' come first. Optical power is in dBm.", "m"));
            Table(rows, new[] { "Sensor", "Value", "State" }, list.Take(300).Select(r => new[] { r.nm, r.Text, r.State }).ToList(), list.Take(300).Select(r => r.bad).ToList());
            return rows;
        }

        // ---------- Walk any OID or MIB name, rebuilt into tables
        public static List<string[]> Walk(string dev, string cred, string what, Toolkit.Run run)
        {
            IPEndPoint ep; string err; var c = Snmp.Cred.Parse(cred); var rows = new List<string[]>();
            if (!Target(dev, out ep, out err)) { rows.Add(R(2, "Device", err, "", "u")); return rows; }
            run.Status = "Reading the MIB folder..."; MibIndex.EnsureBuilt(s => run.Status = s);
            string b = MibIndex.ResolveName(what);
            if (b == null) { rows.Add(R(2, "OID", (what ?? "").Trim() == "" ? "type an OID (1.3.6.1.2.1.2.2) or a name from your MIBs (ifTable)" : "\"" + what + "\" isn't a number OID or a name in your MIB folder", "Search the MIB folder to find names.", "u")); return rows; }
            var idx = MibIndex.For(b);
            var vbs = Snmp.Walk(ep, c, b, 2000, run, out err);
            if (vbs == null) return Fail(ep.ToString(), err, c);
            if (vbs.Count == 0) { rows.Add(R(1, ep.ToString(), "nothing under " + (idx.NameOfOid(b) ?? b), "The device doesn't offer this table, or the community / user can't see it (its SNMP view).", "m")); return rows; }
            Render(rows, vbs, idx, b, ep.Address.ToString());
            return rows;
        }
        // a walk -> a summary row, then each table (trouble rows first), then single values
        public static void Render(List<string[]> rows, List<Snmp.Vb> vbs, MibIndex idx, string b, string where)
        {
            var scalars = new List<string[]>(); var tables = Snmp.Tables(vbs, idx, scalars);
            bool cut = vbs.Count >= 2000;
            string label = idx.NameOfOid(b) ?? b; if (label.EndsWith("Entry", StringComparison.Ordinal) && b.Contains(".")) label = idx.NameOfOid(b.Substring(0, b.LastIndexOf('.'))) ?? label;
            rows.Add(R(cut ? 1 : 0, label + " on " + where, vbs.Count + " values" + (cut ? " (stopped at 2,000)" : "") + " · " + tables.Count + (tables.Count == 1 ? " table" : " tables") + (scalars.Count > 0 ? " · " + scalars.Count + " single values" : ""),
                (idx.Files == 0 && b.StartsWith("1.3.6.1.4.1.", StringComparison.Ordinal) ? "This is a vendor's (enterprise) OID - add the vendor's MIB files to the MIB folder to get names. " : "") + "Rows that are admin-up but operationally down come first.", "m"));
            foreach (var t in tables)
            {
                // columns: names and descriptions first, then states, then the rest; a row-status column says nothing useful
                // a column that only repeats the row index, or says the same thing in every row, tells you least - it goes last
                Func<string, bool> same = n => t.Rows.Count > 1 && t.Order.Select(i => { string v; return t.Rows[i].TryGetValue(n, out v) ? v : ""; }).Distinct().Count() == 1;
                Func<string, bool> isIdx = n => t.Order.All(i => { string v; return t.Rows[i].TryGetValue(n, out v) && v == i; });
                var cols = t.Cols.Where(n => !n.EndsWith("RowStatus", StringComparison.Ordinal) && !isIdx(n)).OrderBy(n => same(n) ? 9 : Rank(n)).ThenBy(n => t.Cols.IndexOf(n)).ToList();
                var ord = t.Order.OrderBy(i => Snmp.Trouble(t, t.Rows[i]) ? 0 : 1).ThenBy(i => i, Comparer<string>.Create(IdxCmp)).Take(500).ToList();
                rows.Add(R(4, t.Name, t.Rows.Count + " rows · " + t.Cols.Count + " columns" + (cols.Count > 9 ? " (9 shown here, all of them in Copy as text)" : ""), "", "m"));
                string pre = Common(cols.Where(n => !n.StartsWith("col.", StringComparison.Ordinal)).ToList());
                Table(rows, new[] { "Index" }.Concat(cols.Take(9).Select(n => n.StartsWith(pre, StringComparison.Ordinal) && n.Length >= pre.Length + 2 ? n.Substring(pre.Length) : n)).ToArray(),
                    ord.Select(i => new[] { i }.Concat(cols.Take(9).Select(n => { string v; return t.Rows[i].TryGetValue(n, out v) ? v : ""; })).ToArray()).ToList(),
                    ord.Select(i => Snmp.Trouble(t, t.Rows[i])).ToList());
                if (cols.Count > 9) foreach (var i in ord) rows.Add(new[] { "8", "", i + "  " + string.Join("  ·  ", cols.Select(n => { string v; return n + "=" + (t.Rows[i].TryGetValue(n, out v) ? v : ""); })), "", "" });   // every column - for Copy as text only
            }
            foreach (var s in scalars.Take(300)) rows.Add(R(3, s[0], s[1], "", "m"));
        }

        // --mibtest <walk.txt>: a Net-SNMP walk saved to a file, read through your MIB folder offline - nothing is sent
        public static List<string[]> FromNetSnmp(string file, Action<string> status)
        {
            var vbs = new List<Snmp.Vb>(); string first = null;
            foreach (var l in global::System.IO.File.ReadLines(file).Take(50000))
            {
                var m = global::System.Text.RegularExpressions.Regex.Match(l, @"^\s*([\w-]+::)?([\w.-]+)\s*=\s*(?:([\w-]+):\s*)?(.*)$"); if (!m.Success) continue;
                string o = m.Groups[2].Value, ty = m.Groups[3].Value, v = m.Groups[4].Value.Trim();
                if (o.StartsWith("enterprises.", StringComparison.Ordinal)) o = "1.3.6.1.4.1." + o.Substring(12); else if (o.StartsWith("iso.", StringComparison.Ordinal)) o = "1." + o.Substring(4); else if (o.StartsWith(".", StringComparison.Ordinal)) o = o.Substring(1);
                if (!Snmp.ValidOid(o)) continue; if (first == null) first = o;
                var vb = new Snmp.Vb { Oid = o };
                ulong n; long sn;
                switch (ty)
                {
                    case "INTEGER": vb.Type = 2; { var mm = global::System.Text.RegularExpressions.Regex.Match(v, @"-?\d+"); sn = mm.Success ? long.Parse(mm.Value, CultureInfo.InvariantCulture) : 0; var b = new List<byte>(); do { b.Insert(0, (byte)sn); sn >>= 8; } while (sn != 0 && sn != -1); vb.Raw = b.ToArray(); } break;
                    case "Gauge32": case "Counter32": case "Counter64": case "Unsigned32": vb.Type = (byte)(ty == "Counter64" ? 0x46 : ty == "Counter32" ? 0x41 : 0x42); ulong.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n); vb.Raw = BitConverter.GetBytes(n).Reverse().ToArray(); break;
                    case "Timeticks": vb.Type = 0x43; { var mm = global::System.Text.RegularExpressions.Regex.Match(v, @"\((\d+)\)"); n = mm.Success ? ulong.Parse(mm.Groups[1].Value, CultureInfo.InvariantCulture) : 0; vb.Raw = BitConverter.GetBytes((uint)n).Reverse().ToArray(); } break;
                    case "IpAddress": { IPAddress a; vb.Type = 0x40; vb.Raw = IPAddress.TryParse(v, out a) ? a.GetAddressBytes() : new byte[4]; } break;
                    default: vb.Type = 4; vb.Raw = global::System.Text.Encoding.UTF8.GetBytes(v.Trim('"')); break;
                }
                vbs.Add(vb);
            }
            var rows = new List<string[]>(); if (vbs.Count == 0) { rows.Add(R(2, file, "no Net-SNMP walk lines found", "", "u")); return rows; }
            MibIndex.EnsureBuilt(status);
            // the base: the longest OID prefix every line shares
            string b0 = first; foreach (var v in vbs) while (b0.Length > 0 && !(v.Oid == b0 || v.Oid.StartsWith(b0 + ".", StringComparison.Ordinal))) b0 = b0.Contains(".") ? b0.Substring(0, b0.LastIndexOf('.')) : "";
            Render(rows, vbs, MibIndex.For(b0), b0, global::System.IO.Path.GetFileName(file));
            return rows;
        }
        static int Rank(string n)
        {
            if (Regex(n, "Name")) return 0;
            if (Regex(n, "Descr|Description|Alias")) return 1;
            if (Regex(n, "AdminStatus|AdminState|OperStatus|OperState")) return 2;
            if (Regex(n, "Status|State|Type")) return 3;
            return 4;
        }
        static bool Regex(string n, string p) { return global::System.Text.RegularExpressions.Regex.IsMatch(n, "(" + p + ")$"); }
        static string Common(List<string> cols)
        {
            if (cols.Count < 2) return ""; string p = cols[0];
            foreach (var cn in cols) { int i = 0; while (i < p.Length && i < cn.Length && p[i] == cn[i]) i++; p = p.Substring(0, i); }
            int k = p.Length; while (k > 0 && !char.IsLower(p[k - 1])) k--;   // whole words only ("svc", "if"), never half a word
            return p.Substring(0, k).Length >= 2 ? p.Substring(0, k) : "";
        }

        // ---------- the MIB folder: what's in it, and search
        public static List<string[]> Search(string words, Toolkit.Run run)
        {
            var rows = new List<string[]>();
            run.Status = "Reading the MIB folder..."; MibIndex.EnsureBuilt(s => run.Status = s);
            int files = MibIndex.FileCount();
            rows.Add(new[] { "3", "MIB folder", files == 0 ? "empty - only the standard objects AdeeboMeter knows" : files + " MIB files", "Put your vendors' .mib files here (the whole set - they import each other). Read locally, never sent anywhere. Click to open the folder.", "w", "open:" + MibIndex.Folder });
            if ((words ?? "").Trim() == "") { rows.Add(R(3, "Search", "type what you're looking for: temperature, optic, fan, bgp, cpu...", "", "u")); return rows; }
            var hits = MibIndex.Search(words, 60);
            if (hits.Count == 0) { rows.Add(R(1, "Search", "nothing matches \"" + words.Trim() + "\"", files == 0 ? "Add the vendor's MIB files first." : "Try a shorter word.", "u")); return rows; }
            rows.Add(R(0, "Search", hits.Count + (hits.Count >= 60 ? "+" : "") + " objects match \"" + words.Trim() + "\" - click one to walk it on the device", "", "i"));
            foreach (var h in hits) rows.Add(new[] { "3", h[0], h[1] + (h[2] != "" && h[2] != "standard" ? "  ·  " + (h[2].Length > 200 ? h[2].Substring(0, 199) + "…" : h[2]) : ""), "", "i", "walk:" + h[1] });
            return rows;
        }
    }
}
