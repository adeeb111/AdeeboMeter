// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // ================================================================== self-test: AdeeboMeter.exe --selftest
    // Exercises every part once on this PC and writes AdeeboMeter-selftest.txt next to the exe.
    // Network use: pings to your gateway and one HEAD request to it on port 80 - nothing on the internet.
    [SuppressMessage("Performance", "CA1861", Justification = "one-shot self-test data")]
    static class SelfTest
    {
        // --perf: how long each page takes to switch to, to paint, and what the 2-a-second timer costs - written to AdeeboMeter-perf.txt
        public static int Perf()
        {
            var sb = new StringBuilder();
            var M = new Monitor(); float sc; using (var g = Graphics.FromHwnd(IntPtr.Zero)) sc = g.DpiX / 96f; Draw.Init(sc);
            M.RefreshInterfaces(); for (int i = 0; i < 3; i++) { M.Tick(); Thread.Sleep(300); }
            sb.AppendLine("AdeeboMeter " + Program.Version + " perf  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture) + "  scale " + (sc * 100).ToString("0", CultureInfo.InvariantCulture) + "%  " + Environment.ProcessorCount + " cores");
            using (var icon = Theme.MakeIcon(32))
            {
                var fv = new FullView(icon, M); fv.Show(); Application.DoEvents(); Thread.Sleep(1500); Application.DoEvents();
                sb.Append(fv.Perf());
                fv.Close(); fv.Dispose();
            }
            File.WriteAllText(Path.Combine(Program.WriteDir, "AdeeboMeter-perf.txt"), sb.ToString());
            return 0;
        }
        // --deeptest [--local] [--minutes N]: the hardening run (see FullView.DeepTest.cs) - written to AdeeboMeter-deeptest.txt
        public static int Deep(bool net, int minutes)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }   // keeps the PC usable while it runs
            var sb = new StringBuilder();
            var M = new Monitor(); float sc; using (var g = Graphics.FromHwnd(IntPtr.Zero)) sc = g.DpiX / 96f; Draw.Init(sc);
            M.RefreshInterfaces(); for (int i = 0; i < 3; i++) { M.Tick(); Thread.Sleep(300); }
            sb.AppendLine("AdeeboMeter " + Program.Version + " deep test  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture) + "  " + Environment.OSVersion + "  " + Environment.ProcessorCount + " cores  scale " + (sc * 100).ToString("0", CultureInfo.InvariantCulture) + "%  cap " + minutes + " min" + (net ? "" : "  local only"));
            string path = Path.Combine(Program.WriteDir, "AdeeboMeter-deeptest.txt");
            try
            {
                using (var icon = Theme.MakeIcon(32))
                {
                    var fv = new FullView(icon, M); fv.Show(); Application.DoEvents();
                    sb.Append(fv.DeepTest(net, minutes));
                    fv.Close(); fv.Dispose();
                }
            }
            catch (Exception e) { sb.AppendLine("CRASHED: " + e); }
            File.WriteAllText(path, sb.ToString());
            return sb.ToString().Contains("ALL PASSED") ? 0 : 1;
        }
        // --mibtest <walk.txt>: writes AdeeboMeter-mibtest.txt - the walk named and rebuilt through your MIB folder
        public static int MibTest(string file)
        {
            var sb = new StringBuilder(); var sw = Stopwatch.StartNew();
            try
            {
                var rows = SnmpTools.FromNetSnmp(file, s => { });
                sb.AppendLine("AdeeboMeter " + Program.Version + " MIB test · " + MibIndex.FileCount() + " MIB files · " + sw.ElapsedMilliseconds + " ms");
                foreach (var r in rows) sb.AppendLine(r[0] == "6" || r[0] == "7" || r[0] == "8" ? (r[0] == "7" ? "! " : "  ") + r[2] : r[1] + ": " + r[2]);
            }
            catch (Exception e) { sb.AppendLine(Directory.Exists(file) ? "FAILED: --mibtest takes one saved Net-SNMP walk file (snmpwalk output), not a folder - your MIB files go in the MIB folder" : "FAILED: " + e); File.WriteAllText(Path.Combine(Program.WriteDir, "AdeeboMeter-mibtest.txt"), sb.ToString()); return 1; }
            File.WriteAllText(Path.Combine(Program.WriteDir, "AdeeboMeter-mibtest.txt"), sb.ToString());
            return 0;
        }
        public static int Run()
        {
            var sb = new StringBuilder(); int fails = 0;
            Action<string, Action> T = delegate(string name, Action a)
            {
                var sw = Stopwatch.StartNew();
                try { a(); sb.AppendLine("PASS  " + name.PadRight(40) + sw.ElapsedMilliseconds + " ms"); }
                catch (Exception e) { fails++; sb.AppendLine("FAIL  " + name.PadRight(40) + e.GetType().Name + ": " + e.Message); }
            };
            sb.AppendLine("AdeeboMeter " + Program.Version + " self-test  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture) + "  " + Environment.OSVersion + "  CLR " + Environment.Version + "  " + (Environment.Is64BitProcess ? "x64" : "x86"));
            var M = new Monitor();
            float sc; using (var g = Graphics.FromHwnd(IntPtr.Zero)) sc = g.DpiX / 96f;
            sb.AppendLine("      display scale " + (sc * 100).ToString("0", CultureInfo.CurrentCulture) + "%");
            T("fonts / drawing init", () => Draw.Init(sc));
            T("interfaces", () => { M.RefreshInterfaces(); lock (M.L) sb.AppendLine("      adapter=" + M.AdapterKind + " gateway=" + (M.Gateway != "" ? "yes" : "none") + " vpn=" + M.VpnUp + " mtu=" + M.PrimaryMtu); });
            T("counters x3", () => { for (int i = 0; i < 3; i++) { M.Tick(); Thread.Sleep(300); } sb.AppendLine("      cpu=" + Draw.Fmt(M.Cpu.Last, "0", "%") + " mem=" + Draw.Fmt(M.Mem.Last, "0", "%") + " disk=" + Draw.Fmt(M.Disk.Last, "0", "%")); });
            T("tcp table (every state known)", () =>
            {
                var l = Tables.Tcp(true, new Dictionary<int, string>());
                sb.AppendLine("      " + l.Count + " sockets, states: " + string.Join(",", l.Select(c => c.State).Distinct()));
                var odd = l.Where(c => c.State.StartsWith("STATE ", StringComparison.Ordinal)).ToList();
                foreach (var c in odd.Take(5)) sb.AppendLine("      odd: " + c.State + "  " + c.Proto + " " + c.Local + " -> " + c.Remote + "  pid " + c.Pid);
                if (odd.Count > 0) throw new InvalidOperationException(odd.Count + " socket(s) with a state number Windows doesn't document");
            });
            T("udp table", () => sb.AppendLine("      " + Tables.Udp().Count + " sockets"));
            T("dns cache names (offline)", () =>
            {
                M.RefreshIpNames(); lock (M.L) sb.AppendLine("      " + M.IpNames.Count + " cached addresses");
                try
                {
                    string dd = Job.Run(Path.Combine(Environment.SystemDirectory, "ipconfig.exe"), "/displaydns", 5000) ?? "";
                    var probe = new Dictionary<string, string>(); Monitor.ParseDisplayDns(dd, probe);
                    var firstLines = dd.Split('\n').Select(x => x.Trim()).Where(x => x != "").Take(3).Select(x => Draw.Trunc(x, 50));
                    sb.AppendLine("      ipconfig /displaydns: " + dd.Split('\n').Length + " lines, " + probe.Count + " addresses · starts: " + string.Join(" | ", firstLines));
                }
                catch (Exception e) { sb.AppendLine("      ipconfig /displaydns failed: " + e.Message); }
                var t = new Dictionary<string, string>();
                Monitor.ParseDisplayDns("Windows IP Configuration\r\n\r\n    github.com\r\n    ----------------------------------------\r\n    Record Name . . . . . : github.com\r\n    Record Type . . . . . : 1\r\n    Time To Live  . . . . : 41\r\n    Data Length . . . . . : 4\r\n    Section . . . . . . . : Answer\r\n    A (Host) Record . . . : 140.82.112.4\r\n\r\n    www.example.com\r\n    ----------------------------------------\r\n    AAAA Record . . . . . : 2606:2800:21f:cb07:6820:80da:af6b:8b2c\r\n", t);
                string v1, v2; t.TryGetValue("140.82.112.4", out v1); t.TryGetValue("2606:2800:21f:cb07:6820:80da:af6b:8b2c", out v2);
                if (v1 != "github.com" || v2 != "www.example.com" || t.Count != 2) throw new InvalidOperationException("ipconfig /displaydns parser wrong");
            });
            T("toolbox: local tools and PC checks run", () =>
            {
                var sub = Toolkit.Subnet("10.1.2.3/22", "10.1.3.200");
                if (!sub.Any(r => r[2].StartsWith("10.1.0.0/22", StringComparison.Ordinal)) || !sub.Any(r => r[2] == "yes")) throw new InvalidOperationException("subnet calculator wrong");
                if (!Toolkit.Subnet("2001:db8::1/64", "").Any(r => r[2].StartsWith("2001:db8::/64", StringComparison.Ordinal))) throw new InvalidOperationException("IPv6 subnet wrong");
                int n = PathTools.Overlap().Count + WhyFails.Hosts("").Count + WinEvidence.All(M).Count + PcHealth.Pressure(M).Count + PcHealth.Crashes().Count + PcHealth.Leaks().Count;
                var msg = Toolkit.Query(System.Net.IPAddress.Loopback, "example.com", 1, true);   // nothing listens on 127.0.0.1:53 - must just say "no answer"
                if (PathTools.BackHops(57) != 8 || PathTools.BackHops(120) != 9 || PathTools.Kind(1492).IndexOf("PPPoE", StringComparison.Ordinal) < 0) throw new InvalidOperationException("path helpers wrong");
                var js = Json.Parse("{\"data\":{\"asns\":[812],\"prefix\":\"24.0.0.0/12\",\"holder\":\"ROGERS-COMMUNICATIONS - Rogers\"}}") as Dictionary<string, object>;
                if (js == null || !((Dictionary<string, object>)js["data"]).ContainsKey("prefix")) throw new InvalidOperationException("json reader wrong");
                if (Json.Parse(new string('[', 200000)) != null) throw new InvalidOperationException("json reader accepts 200,000 nested arrays");   // would overflow the stack and end the app
                string grp;
                if (RoutingTools.Virtual("00-00-5e-00-01-0c", out grp) != "VRRP" || grp != "12" || RoutingTools.Virtual("00:00:0c:07:ac:01", out grp) != "HSRP" || RoutingTools.Virtual("3c:84:6a:12:34:56", out grp) != "") throw new InvalidOperationException("virtual router MAC reader wrong");
                var hp = new List<PathTools.Hop> { new PathTools.Hop { N = 1, Ip = "10.0.0.1" }, new PathTools.Hop { N = 2, Ip = "10.0.1.1" } };
                hp[1].Seen.Add("10.0.1.1"); hp[1].Seen.Add("10.0.1.2"); hp[1].Switches = 6;
                if (!PathTools.Flags(hp).Any(r => r[1].Contains("load sharing"))) throw new InvalidOperationException("ECMP flag wrong");
                // RADIUS reply check against RFC 2865 section 7.1's own example
                Func<string, byte[]> hex = h => Enumerable.Range(0, h.Length / 2).Select(i => Convert.ToByte(h.Substring(i * 2, 2), 16)).ToArray();
                var rfcReply = hex("0200002686fe220e7624ba2a1005f6bf9b55e0b20606000000010f06000000000e06c0a80103");
                if (!Protocols.ReplyOk(rfcReply, 0, hex("0f403f9473978057bd83d5cb98f4227a"), System.Text.Encoding.ASCII.GetBytes("xyzzy5461"))) throw new InvalidOperationException("RADIUS reply check fails the RFC 2865 example");
                if (Protocols.ReplyOk(rfcReply, 0, hex("0f403f9473978057bd83d5cb98f4227a"), System.Text.Encoding.ASCII.GetBytes("wrong"))) throw new InvalidOperationException("RADIUS reply check accepts a wrong secret");
                // SNMP replies from a hostile device: lengths that point backwards, past the end, or nest forever must end quickly
                var swSnmp = Stopwatch.StartNew();
                foreach (var bad in new[] { hex("3084fffffffa0201"), hex("30ff"), hex("30823000"), hex(string.Concat(Enumerable.Repeat("3082ffff", 200))), hex("3010300e060a2b06010201010100000400"), Array.Empty<byte>() })
                { var v = new List<string>(); Protocols.ParseVarbinds(bad, v); }
                var good = new List<string>(); Protocols.ParseVarbinds(hex("300f300d06082b06010201010500040161"), good);
                var big = new List<string>(); Protocols.ParseVarbinds(hex("3016301406082b060102010103004308ffffffffffffffff"), big);
                if (big.Count != 1 || big[0] != "(out of range)") throw new InvalidOperationException("SNMP TimeTicks too big isn't caught");
                if (swSnmp.ElapsedMilliseconds > 500 || good.Count != 1 || good[0] != "a") throw new InvalidOperationException("SNMP reply parser wrong (" + good.Count + " values, " + swSnmp.ElapsedMilliseconds + " ms)");
                // dig: HTTPS (RFC 9460), DNSKEY key tag (RFC 4034 B, 2371 as dnspython computes it), DS, and the IPv6 reverse name
                var dm = Toolkit.Parse(hex("052e81000001000300000000076578616d706c6503636f6d0000410001c00c004100010000012c002f000100000100060268330268320003000220fb00040004c00002010006001020010db8000000000000000000000001c00c003000010000012c00440101030d99db2cc14cabdc33d6d77da63a2f15f71112584f234e8d1dc428e39e8a4a97e1aa271a555dc90701e17e2a4c4b6f120b7c32d44f4ac02bd894cf2d4be7778a19c00c002b00010000012c002409430d021f987cc6583e92df0890718c42a0e86a0f2a6e6cde1e1c3f5c9f4a6b6b2e7f1a"), 1, "t");
                if (dm.Rrs.Count != 3 || dm.Rrs[0].Data != "priority 1 . alpn=h3,h2 port=8443 ipv4hint=192.0.2.1 ipv6hint=2001:db8::1" || !dm.Rrs[1].Data.Contains("key tag 2371") || !dm.Rrs[1].Data.StartsWith("KSK", StringComparison.Ordinal) || !dm.Rrs[2].Data.StartsWith("key tag 2371 ECDSAP256SHA256 digest SHA-256 1F987C", StringComparison.Ordinal))
                    throw new InvalidOperationException("dig HTTPS / DNSKEY / DS decoding wrong: " + string.Join(" | ", dm.Rrs.Select(x => x.Data)));
                if (Toolkit.PtrName(System.Net.IPAddress.Parse("2001:db8::1")) != "1.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.8.b.d.0.1.0.0.2.ip6.arpa") throw new InvalidOperationException("IPv6 reverse name wrong");
                foreach (var cut in new[] { 20, 60, 90, 130 }) Toolkit.Parse(hex("052e81000001000300000000076578616d706c6503636f6d0000410001c00c004100010000012c002f000100000100060268330268320003000220fb00040004c00002010006001020010db8000000000000000000000001c00c003000010000012c00440101030d99db2cc14cabdc33d6d77da63a2f15f71112584f234e8d1dc428e39e8a4a97e1aa271a555dc90701e17e2a4c4b6f120b7c32d44f4ac02bd894cf2d4be7778a19").Take(cut).ToArray(), 1, "t");   // cut short: no crash
                // SNMPv3 keys: RFC 3414 appendix A.3 (password "maplesyrup", engine 00..02)
                var eid = hex("000000000000000000000002");
                if (BitConverter.ToString(Snmp.Localize("maplesyrup", eid, "md5")).Replace("-", "") != "526F5EED9FCCE26F8964C2930787D82B") throw new InvalidOperationException("SNMPv3 MD5 key localization wrong");
                if (BitConverter.ToString(Snmp.Localize("maplesyrup", eid, "sha")).Replace("-", "") != "6695FEBC9288E36282235FC7151F128497B38F3F") throw new InvalidOperationException("SNMPv3 SHA key localization wrong");
                // SHA-256 (RFC 7860): the same method; value computed independently (Python hashlib) for this password and engine
                if (BitConverter.ToString(Snmp.Localize("maplesyrup", eid, "sha256")).Replace("-", "") != "8982E0E549E866DB361A6B625D84CCCC11162D453EE8CE3A6445C2D6776F0F8B") throw new InvalidOperationException("SNMPv3 SHA-256 key localization wrong");
                // iperf3 options typed the attached way are read the same as the spaced way
                var po1 = Toolkit.ParseOpts("-t20 -P4 -O3 -b50M -u"); var po2 = Toolkit.ParseOpts("--time=15 --omit=2 -p 5202");
                if (po1.Secs != 20 || po1.Par != 4 || po1.Omit != 3 || po1.Rate != 50000000 || po1.Port != 5201 || po2.Secs != 15 || po2.Omit != 2 || po2.Port != 5202) throw new InvalidOperationException("iperf3 attached options misread");
                // credentials that can't be used as typed are refused, never sent weaker
                if (Snmp.Cred.Parse("v3:u::p").Bad == "" || Snmp.Cred.Parse("v3-sha512:u:a").Bad == "" || Snmp.Cred.Parse("v3-sha1:u:a").AuthAlg != "sha" || Snmp.Cred.Parse("v3-sha1:u:a").Bad != "") throw new InvalidOperationException("SNMPv3 credential checks wrong");
                // a value longer than 127 bytes (long-form length) - a long sysDescr
                var longVb = new List<byte> { 0x30, 0x81, 0xD8, 0x30, 0x81, 0xD5, 0x06, 0x08, 0x2b, 0x06, 0x01, 0x02, 0x01, 0x01, 0x01, 0x00, 0x04, 0x81, 0xC8 }; longVb.AddRange(Enumerable.Repeat((byte)'a', 200));
                var lv1 = new List<string>(); Protocols.ParseVarbinds(longVb.ToArray(), lv1);
                if (lv1.Count != 1 || !lv1[0].StartsWith("aaaaaaaaaa", StringComparison.Ordinal) || lv1[0].Length < 150) throw new InvalidOperationException("SNMP long value (over 127 bytes) not read");
                var plain = System.Text.Encoding.ASCII.GetBytes("a scoped PDU of 37 bytes, not a block"); var salt = hex("0102030405060708"); var aesKey = Snmp.Localize("maplesyrup", eid, "sha");
                var enc = Snmp.Aes(aesKey, 3, 1234, salt, plain, true); if (enc.SequenceEqual(plain) || !Snmp.Aes(aesKey, 3, 1234, salt, enc, false).SequenceEqual(plain)) throw new InvalidOperationException("SNMPv3 AES round trip wrong");
                if (Snmp.Cred.Parse("v3:admin").Version != 3 || Snmp.Cred.Parse("v3-sha256:u:a:p").AuthAlg != "sha256" || Snmp.Cred.Parse("v3:u:a:p").PrivPass != "p" || Snmp.Cred.Parse("public").Community != "public") throw new InvalidOperationException("SNMP credentials parsing wrong");
                // MIB reader: a TC with enums, a table, its entry, two columns
                var mib = "TEST-MIB DEFINITIONS ::= BEGIN\n MyState ::= TEXTUAL-CONVENTION STATUS current DESCRIPTION \"x\" SYNTAX INTEGER { up(1), down(2) }\n" +
                    "test OBJECT IDENTIFIER ::= { enterprises 99999 }\n tTable OBJECT-TYPE SYNTAX SEQUENCE OF TEntry MAX-ACCESS not-accessible STATUS current DESCRIPTION \"the table\" ::= { test 1 }\n" +
                    "tEntry OBJECT-TYPE SYNTAX TEntry MAX-ACCESS not-accessible STATUS current DESCRIPTION \"a row\" INDEX { tIdx } ::= { tTable 1 }\n" +
                    "tName OBJECT-TYPE SYNTAX DisplayString MAX-ACCESS read-only STATUS current DESCRIPTION \"the name -- not a comment inside a string\" ::= { tEntry 2 }\n" +
                    "tOperState OBJECT-TYPE SYNTAX MyState MAX-ACCESS read-only STATUS current DESCRIPTION \"state\" ::= { tEntry 3 } -- a comment\nEND";
                var lines = MibIndex.BuildLines(new[] { mib });
                var st = lines.FirstOrDefault(l => l.StartsWith("1.3.6.1.4.1.99999.1.1.3\ttOperState\t1.3.6.1.4.1.99999.1\t", StringComparison.Ordinal));
                if (st == null || st.Split('\t')[3] != "1=up,2=down") throw new InvalidOperationException("MIB reader wrong: " + string.Join(" | ", lines));
                // a walk rebuilt into a table, trouble first
                var idx = MibIndex.BuiltinOnly(); Func<string, byte, byte[], Snmp.Vb> vb = (o, ty, raw) => new Snmp.Vb { Oid = o, Type = ty, Raw = raw };
                var walk = new List<Snmp.Vb> { vb("1.3.6.1.2.1.2.2.1.2.1", 4, System.Text.Encoding.ASCII.GetBytes("eth0")), vb("1.3.6.1.2.1.2.2.1.2.2", 4, System.Text.Encoding.ASCII.GetBytes("eth1")),
                    vb("1.3.6.1.2.1.2.2.1.7.1", 2, new byte[] { 1 }), vb("1.3.6.1.2.1.2.2.1.7.2", 2, new byte[] { 1 }), vb("1.3.6.1.2.1.2.2.1.8.1", 2, new byte[] { 1 }), vb("1.3.6.1.2.1.2.2.1.8.2", 2, new byte[] { 2 }) };
                var tabs = Snmp.Tables(walk, idx, null);
                if (tabs.Count != 1 || tabs[0].Name != "ifTable" || tabs[0].Rows.Count != 2 || tabs[0].Rows["2"]["ifOperStatus"] != "down" || !Snmp.Trouble(tabs[0], tabs[0].Rows["2"]) || Snmp.Trouble(tabs[0], tabs[0].Rows["1"])) throw new InvalidOperationException("SNMP table rebuild wrong");
                n += RoutingTools.Neighbours(M).Count;
                n += RoutingTools.Ipv6Routers().Count + RoutingTools.RouteFor("127.0.0.1").Count;
                n += SysTools.Listening(M).Count + SysTools.Certs().Count + SysTools.TimeSource().Count;
                if (!Protocols.Syslog("127.0.0.1", "selftest").Any(r => r[1].StartsWith("Sent", StringComparison.Ordinal))) throw new InvalidOperationException("syslog sender wrong");
                if (!Protocols.Snmp("127.0.0.1", "").Any(r => r[0] == "2")) throw new InvalidOperationException("SNMP should report no answer from 127.0.0.1");
                sb.AppendLine("      " + n + " rows from overlap, hosts, Windows evidence, PC pressure, crashes, leaks" + (msg == null ? " · DNS client: no answer from 127.0.0.1 (correct)" : ""));
            });
            T("version numbers agree", () => { if (!(Program.FileVersion + ".").StartsWith(Program.Version + ".", StringComparison.Ordinal) || Program.FileVersion.Split('.').Length != 4) throw new InvalidOperationException(Program.Version + " vs " + Program.FileVersion); });
            T("DLLs load only from System32", () => { if (!Program.DllSafety && !Program.IsWine) throw new InvalidOperationException("SetDefaultDllDirectories didn't take - DLL planting protection is off"); });
            T("never listens", () => { if (Tables.OwnListener()) throw new InvalidOperationException("AdeeboMeter owns a listening socket"); });
            T("destination input validation", () =>
            {
                string h; int p; bool t; string e;
                string[] good = { "github.com", "github.com:443", "https://example.com/x", "10.0.0.1", "[2001:db8::1]:443", "http://host:8080", "2001:db8::1" };
                string[] bad = { "", "a b", "github.com:99999", "x;rm -rf", "$(calc)", "host:abc", new string('a', 400), "http://", "\"quoted\"", "a&b", "..", "-bad.com" };
                foreach (var s in good) if (!DestCheck.Parse(s, out h, out p, out t, out e)) throw new InvalidOperationException("rejected good input: " + s);
                foreach (var s in bad) if (DestCheck.Parse(s, out h, out p, out t, out e)) throw new InvalidOperationException("accepted bad input: " + s);
            });
            T("gateway ping", () => { string gw; lock (M.L) gw = M.Gateway; sb.AppendLine("      " + Draw.Fmt(Monitor.PingMs(gw), "0", " ms")); });
            T("health check", () => { var r = HealthCheck.Run(M); sb.AppendLine("      " + r.Count + " checks: " + string.Join(", ", r.Select(x => x[1] + "=" + x[0]))); });
            T("destination check on gateway (local only)", () =>
            {
                string gw; lock (M.L) gw = M.Gateway; if (gw == "") { sb.AppendLine("      skipped: no gateway"); return; }
                var r = new DestResult(); DestCheck.Run(gw + ":80", r, delegate { }, M);
                if (!r.Done) throw new InvalidOperationException("did not finish");
                sb.AppendLine("      steps=" + string.Join(" ", r.Lv) + "  verdict=" + Draw.Trunc(r.Verdict, 70));
            });
            T("render chip / card / detail", () =>
            {
                var snap = new Snap(); snap.Details["x"] = new Detail { Title = "t", Note = "n" }; snap.Details["x"].Lines.Add(new[] { "a", "b", "0" });
                using (Draw.Chip(snap, 0.5)) { } using (Draw.Card(snap, new Dictionary<string, RectangleF>())) { } using (Draw.DetailCard(snap.Details["x"])) { }
            });
            T("engineer view opens in under 2 s", () =>
            {
                var sw = Stopwatch.StartNew();
                using (var icon = Theme.MakeIcon(32))
                {
                    var fv = new FullView(icon, M); fv.WindowState = FormWindowState.Normal; fv.Opacity = 0; fv.ShowInTaskbar = false; fv.Show(); Application.DoEvents();
                    long ms = sw.ElapsedMilliseconds; sw.Restart();
                    fv.Close(); fv.Dispose(); Application.DoEvents();
                    sb.AppendLine("      opened in " + ms + " ms, closed in " + sw.ElapsedMilliseconds + " ms");
                    if (ms > 2000) throw new InvalidOperationException("took " + ms + " ms");   /* 1 s is the aim; 2 s fails - a busy or high-DPI PC lands near 1.3 s */
                }
            });
            if (Environment.GetCommandLineArgs().Any(a => a == "--samples"))
                T("sample images (light + dark)", () =>
                {
                    var snap = new Snap { Level = 2, Headline = "Your PC is cooked, boss - memory is full. Edge (3.7 GB) and Teams are the hungry ones; close something.", Countdown = 7, Call = true, Cpu = 43, Mem = 93, Disk = 12, Down = 12.4, Up = 2.1, Jitter = 12, LossPct = 0 };
                    snap.Path = new[] { 2, 0, 0, 1, 0 }; snap.PathLabel = new[] { "PC", "Wi-Fi", "ISP", "DNS", "Internet" };
                    snap.PathMain = new[] { "CPU 43% · RAM 93%", "signal 91% · gw 2 ms", "+12 ms past router", "118 ms", "14 ms · loss 0%" };
                    snap.PathSub = new[] { "Problem here · 16 threads · 16 GB", "OK · Adeeb ch 157 · 10.0.0.1", "OK · no VPN · MTU 1500", "Slow · 64.71.255.204", "OK · probe 1.1.1.1" };
                    var rnd = new Random(3); snap.DownHist = Enumerable.Range(0, 60).Select(i => 5 + 10 * Math.Abs(Math.Sin(i / 6.0)) + rnd.NextDouble() * 3).ToArray(); snap.UpHist = snap.DownHist.Select(v => v / 5).ToArray();
                    var det = new Detail { Title = "Memory", TopTitle = "TOP BY MEMORY", Note = "Commit is the real \"can I open another app\" number." };
                    det.Lines.Add(new[] { "In use", "14.9 of 16.0 GB" }); det.Lines.Add(new[] { "Commit", "21.2 of 24.0 GB", "2" }); det.Top.Add(new[] { "Edge (31)", "3.7 GB" }); det.Top.Add(new[] { "Teams (6)", "1.6 GB" });
                    foreach (var dark in new[] { false, true })
                    {
                        Theme.Apply(dark); string tag = dark ? "dark" : "light";
                        using (var b = Draw.Chip(snap, 0.6)) b.Save(Path.Combine(Program.WriteDir, "sample_chip_" + tag + ".png"));
                        using (var b = Draw.Card(snap, new Dictionary<string, RectangleF>())) b.Save(Path.Combine(Program.WriteDir, "sample_card_" + tag + ".png"));
                        using (var b = Draw.DetailCard(det)) b.Save(Path.Combine(Program.WriteDir, "sample_detail_" + tag + ".png"));
                    }
                    Theme.Apply(false);
                });
            T("no leaks: open + close the view 30 times", () =>
            {
                IntPtr me = Native.GetCurrentProcess();
                Func<string> res = () => "GDI " + Native.GetGuiResources(me, 0) + ", USER " + Native.GetGuiResources(me, 1);
                Action<FullView> cycle = null;
                using (var icon = Theme.MakeIcon(32))
                {
                    cycle = delegate(FullView unused)
                    {
                        var fv = new FullView(icon, M); fv.WindowState = FormWindowState.Normal; fv.Opacity = 0; fv.ShowInTaskbar = false; fv.Show(); Application.DoEvents();
                        fv.TestWalk();
                        fv.Close(); fv.Dispose(); Application.DoEvents();
                    };
                    cycle(null); cycle(null);   // warm-up: Windows and WinForms create some one-time handles (cursors, caches) on first use
                    GC.Collect(); GC.WaitForPendingFinalizers(); Application.DoEvents();
                    int gdi0 = Native.GetGuiResources(me, 0), usr0 = Native.GetGuiResources(me, 1); string before = res();
                    // Windows frees window handles a moment after a window closes, so short runs swing +/- a few handles.
                    // A real leak grows with every open: measure a long run, after a pause, and allow 1 handle per 3 opens.
                    for (int i = 0; i < 30; i++) cycle(null);
                    Thread.Sleep(1500); Application.DoEvents();
                    GC.Collect(); GC.WaitForPendingFinalizers(); Application.DoEvents();
                    int gdi1 = Native.GetGuiResources(me, 0), usr1 = Native.GetGuiResources(me, 1);
                    sb.AppendLine("      after warm-up: " + before + "   after 30 more: " + res());
                    if (gdi0 > 0 && (gdi1 - gdi0 > 10 || usr1 - usr0 > 10))
                    {
                        // Find the culprit: 4 opens per variant, each visiting one page (or just the hover card).
                        var variants = new[] { "open+close only", "no icon", "in taskbar", "link", "procs", "changes", "hover card" };
                        foreach (var vname in variants)
                        {
                            int g0 = Native.GetGuiResources(me, 0), u0 = Native.GetGuiResources(me, 1);
                            for (int i = 0; i < 10; i++)
                            {
                                var fv = new FullView(vname == "no icon" ? null : icon, M); fv.WindowState = FormWindowState.Normal; fv.Opacity = 0; fv.ShowInTaskbar = vname == "in taskbar"; fv.Show(); Application.DoEvents();
                                if (vname == "hover card") fv.TestPages(Array.Empty<string>(), true);
                                else if (vname == "link" || vname == "procs" || vname == "changes") { fv.TestPages(new[] { vname }, false); Thread.Sleep(300); Application.DoEvents(); }
                                fv.Close(); fv.Dispose(); Application.DoEvents();
                            }
                            Thread.Sleep(1500); Application.DoEvents();   // let background page work finish
                            GC.Collect(); GC.WaitForPendingFinalizers(); Application.DoEvents();
                            int g1 = Native.GetGuiResources(me, 0) - g0, u1 = Native.GetGuiResources(me, 1) - u0;
                            sb.AppendLine("      leak hunt · " + vname.PadRight(16) + " GDI " + (g1 >= 0 ? "+" : "") + g1 + "  USER " + (u1 >= 0 ? "+" : "") + u1 + "  (10 opens)");
                        }
                        throw new InvalidOperationException("handles grew by " + (gdi1 - gdi0) + " GDI / " + (usr1 - usr0) + " USER over 30 opens");
                    }
                }
            });
            T("SNMP: counter changes, MACs and LACP states are read honestly", () =>
            {
                var bad = new List<string>();
                if (SnmpTools.Delta(10, 47, false) != "+37") bad.Add("delta");
                if (SnmpTools.Delta(47, 10, false) != "reset" || SnmpTools.Delta(10, 47, true) != "reset") bad.Add("reset");
                if (SnmpTools.Delta(-1, 5, false) != "n/a" || SnmpTools.Delta(5, -1, false) != "n/a") bad.Add("missing must be n/a, never 0");
                if (SnmpTools.NormMac("00-11-22-AA-bb-cc") != "00:11:22:aa:bb:cc" || SnmpTools.NormMac("0011.22aa.bbcc") != "00:11:22:aa:bb:cc" || SnmpTools.NormMac("aa:bb") != null || SnmpTools.NormMac("zz1122334455") != null) bad.Add("mac");
                if (SnmpTools.LacpState(0x3C) != "in bundle" || !SnmpTools.LacpState(0x34).Contains("not collecting") || !SnmpTools.LacpState(0x3E).Contains("defaulted")) bad.Add("lacp");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join(", ", bad));
            });
            T("Traffic Lab and certificate helpers read their input right", () =>
            {
                var bad = new List<string>();
                var pl = Lab.Payload("hex:0a 0B ff"); if (pl == null || pl.Length != 3 || pl[1] != 0x0b || pl[2] != 0xff) bad.Add("hex payload");
                if (Lab.Payload("hex:0a0") != null || Lab.Payload("GET / HTTP/1.0\\r\\n").Last() != 10) bad.Add("payload");
                string h; int pt; if (!Lab.Split("[2001:db8::1]:5060", 0, out h, out pt) || h != "2001:db8::1" || pt != 5060 || Lab.Split("x;calc:1", 0, out h, out pt)) bad.Add("host:port");
                var tid = new byte[12]; var st = new byte[32]; st[0] = 1; st[1] = 1; st[3] = 12; st[4] = 0x21; st[5] = 0x12; st[6] = 0xA4; st[7] = 0x42; st[21] = 0x20; st[23] = 8; st[25] = 1; st[26] = (byte)(0x1F ^ 0x21); st[27] = (byte)(0x90 ^ 0x12); st[28] = 203 ^ 0x21; st[29] = 0 ^ 0x12; st[30] = 113 ^ 0xA4; st[31] = 7 ^ 0x42;
                var me = Reach.ParseStun(st, tid); if (me == null || me.ToString() != "203.0.113.7:8080") bad.Add("stun " + me);
                if (Lab.DhcpText(121, new byte[] { 24, 10, 1, 2, 192, 168, 1, 1 }) != "10.1.2.0/24 via 192.168.1.1" || Lab.DhcpText(6, new byte[] { 1, 1, 1, 1, 9, 9, 9, 9 }) != "1.1.1.1, 9.9.9.9") bad.Add("dhcp options");
                var jw = CertTools.Decode("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ4IiwiZXhwIjoxNjAwMDAwMDAwfQ.c2ln"); if (!jw.Any(r => r[1] == "Expires" && r[2].Contains("EXPIRED"))) bad.Add("jwt");
                if (Lab.TrapPacket("public", 100, 7, "t")[0] != 0x30 || Lab.FlowPacket(System.Net.IPAddress.Loopback, System.Net.IPAddress.Loopback, 5000, 1).Length != 72) bad.Add("trap / flow");
                if (Reach.SafeHost("a;b") || Reach.SafeUser("x y") || !Reach.SafeHost("sw-01.lab") || !Reach.SafeUser("DOMAIN\\adeeb")) bad.Add("remote inputs");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join(", ", bad));
            });
            T("Security: 42 checks, end-of-support dates and site names read right", () =>
            {
                var bad = new List<string>();
                if (Security.All.Count != 42 || Security.All.Select(c => c.Id).Distinct().Count() != 42) bad.Add("count " + Security.All.Count);
                var d = new DateTime(2026, 11, 9);
                if (Security.VersionVerdict("19045", "Professional", "22H2", d)[0][0] != "2") bad.Add("win10 should be ended");
                if (Security.VersionVerdict("22631", "Enterprise", "23H2", d)[0][0] != "1") bad.Add("23H2 enterprise ends tomorrow");
                if (Security.VersionVerdict("26100", "Professional", "24H2", d)[0][0] != "2" || Security.VersionVerdict("26100", "Enterprise", "24H2", d)[0][0] != "0") bad.Add("24H2");
                if (Security.HostOf("https://Example.com/path") != "example.com" || Security.HostOf("a;b") != null || Security.HostOf("10.0.0.1") != null) bad.Add("site names");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join(", ", bad));
            });
            T("LDAP rootDSE request and answer are read right", () =>
            {
                var q = MoreTools.RootDseRequest(); if (q[0] != 0x30 || Array.IndexOf(q, (byte)0x63) < 0) throw new InvalidOperationException("request");
                var t = Encoding.ASCII.GetBytes("dnsHostName"); var v = Encoding.ASCII.GetBytes("dc01.corp.test");
                var pkt = new List<byte> { 0x30, 0, 0x04, (byte)t.Length }; pkt.AddRange(t); pkt.AddRange(new byte[] { 0x31, (byte)(v.Length + 2), 0x04, (byte)v.Length }); pkt.AddRange(v);
                var d = MoreTools.ParseRootDse(pkt.ToArray(), pkt.Count); string h; if (!d.TryGetValue("dnsHostName", out h) || h != "dc01.corp.test") throw new InvalidOperationException("parse: " + h);
            });
            T("outage log pairs 'went quiet' with 'back' and keeps an open one", () =>
            {
                var t0 = new DateTime(2026, 11, 11, 18, 0, 0); Func<int, int, Timeline.Ev> E = (min, lv) => new Timeline.Ev { T = t0.AddMinutes(min), Lv = lv, Kind = "inet", Text = "x" };
                var o = Outages.Pair(new List<Timeline.Ev> { E(0, 2), E(5, 0), E(7, 0), E(20, 2), E(21, 2) }, t0.AddMinutes(30));
                if (o.Count != 2 || !o[0].Ongoing || o[0].From != t0.AddMinutes(20) || o[1].Len.TotalMinutes != 5) throw new InvalidOperationException(o.Count + " outages");
            });
            T("every setting sits in a known section, and search finds words, not word parts", () =>
            {
                var bad = new List<string>();
                if (!FullView.SettingsSelfCheck(bad)) throw new InvalidOperationException(string.Join(", ", bad));
            });
            T("Gamer Zone: Valve list, host check, caps, blame and grades", () =>
            {
                var bad = new List<string>();
                string json = "{\"revision\":1,\"pops\":{\"ams\":{\"desc\":\"Amsterdam (Netherlands)\",\"geo\":[4.9,52.4],\"relays\":[{\"ipv4\":\"155.133.248.34\",\"port_range\":[27015,27060]}]},"
                    + "\"iad\":{\"geo\":[-77.4,38.9],\"desc\":\"Sterling (Washington DC)\",\"relays\":[{\"ipv4\":\"162.254.192.71\"}]},\"bad\":{\"desc\":\"x\",\"relays\":[{\"ipv4\":\"999.1.1.1\"}]}}}";
                var v = Valve.Parse(json);
                if (v.Count != 2 || v[0].Name != "AMS" || v[0].Where != "Amsterdam" || v[1].Host != "162.254.192.71" || v[1].Port != -1) bad.Add("valve parse " + v.Count);
                if (!Games.ValidHost("dynamodb.us-east-1.amazonaws.com") || !Games.ValidHost("203.0.113.20") || Games.ValidHost("riot server 3") || Games.ValidHost("a|b") || Games.ValidHost("x;calc") || Games.ValidHost("")) bad.Add("host check");
                var many = new Game { Id = "t", Name = "T" }; for (int i = 0; i < 20; i++) many.Servers.Add(new GameServer { Name = "S" + i, Host = "10.0.0." + (i + 1), Port = -1 });
                var gs = new List<Game> { many }; var plan = GamePing.Me.Plan(gs, new HashSet<string> { "t" }, "t");
                if (plan.Count(z => z.Item3 < GamePing.SurveySec) > GamePing.MaxServers) bad.Add("more than 8 servers: " + plan.Count);
                if (GamePing.Me.Plan(gs, new HashSet<string>(), null).Count != 0) bad.Add("probes a game nobody clicked");
                if (!GamePing.Blame(90, 30, 45, 4).StartsWith("your Wi-Fi", StringComparison.Ordinal) || GamePing.Blame(4, 150, 80, 4) != "your provider" || !GamePing.Blame(4, 20, 80, 4).StartsWith("past your provider", StringComparison.Ordinal)) bad.Add("blame");
                if (GameStat.Grade(34, 2, 0) != "Competitive" || GameStat.Grade(80, 9, 1) != "Casual" || GameStat.Grade(140, 3, 0) != "Laggy" || GameStat.Grade(double.NaN, 0, 100) != "No reply") bad.Add("grade");
                if (Math.Abs(GameStat.Jitter(new[] { 10.0, 14, 10, double.NaN, 12 }) - (4 + 4 + 2) / 3.0) > 0.01 || GameStat.Loss(new[] { 1.0, double.NaN, 2, double.NaN }) != 50) bad.Add("jitter/loss");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join(", ", bad));
            });
            T("match report removes internal addresses, hardware addresses and the Wi-Fi name", () =>
            {
                string r = GameSession.Redact("router 192.168.0.1 and 10.1.2.3, 172.20.0.9, mac aa:bb:cc:dd:ee:ff on HomeNet-5G; server 155.133.248.34", "HomeNet-5G");
                if (r.Contains("192.168") || r.Contains("10.1.2.3") || r.Contains("172.20") || r.Contains("aa:bb") || r.Contains("HomeNet") || !r.Contains("155.133.248.34")) throw new InvalidOperationException(r);
                var ss = new GameSession { GameId = "t", GameName = "T", Server = "NA East", Start = DateTime.Now.AddMinutes(-1) };
                ss.Add(new GameSession.Sec { T = DateTime.Now, Game = 30, Router = 2, Net = 15, Signal = 80, Down = 1, Up = 0.2, Cpu = 10 });
                ss.Mark(); string rep = ss.Report(new List<Spike>(), "");
                if (!rep.Contains("NA East") || !rep.Contains("Marked as laggy")) throw new InvalidOperationException("report");
            });
            T("Find my match server picks the busiest public address", () =>
            {
                string ip = MatchFinder.Busiest("192.168.0.10\t155.133.248.34\n155.133.248.34\t192.168.0.10\n192.168.0.10\t8.8.8.8\n192.168.0.10\t239.255.255.250\n");
                if (ip != "155.133.248.34") throw new InvalidOperationException(ip ?? "null");
                if (MatchFinder.Busiest("10.0.0.2\t10.0.0.1\n") != null) throw new InvalidOperationException("private counted");
            });
            T("Fix a problem understands everyday words", () =>
            {
                var bad = new List<string>();
                Func<string, string> go = t => { var r = FullView.FixMatch(t); return r == null ? null : r[1]; };
                if (go("youtube keeps stopping and loading") != "guide:slow") bad.Add("youtube");
                if (go("my laptop is hot and slow") != "guide:pc") bad.Add("hot laptop");
                if (go("zoom calls freeze") != "guide:calls") bad.Add("zoom");
                if (go("wifi keeps disconnecting") != "guide:drops") bad.Add("disconnect");
                if (go("valorant lag") != "gamer") bad.Add("valorant");
                if (go("disk is full") != "tool:tidy") bad.Add("disk full");
                if (go("purple elephants") != null) bad.Add("nonsense matched");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join(", ", bad));
            });
            T("ISP report finds where loss starts and ignores loss that doesn't carry on", () =>
            {
                Func<int, int, string, PathTools.Hop> H = (n, lost, ip) => { var h = new PathTools.Hop { N = n, Ip = ip, Sent = 100, Lost = lost }; h.Sum = 10 * (100 - lost); return h; };
                var clean = new List<PathTools.Hop> { H(1, 0, "192.168.1.1"), H(2, 40, "203.0.113.1"), H(3, 0, "203.0.113.9"), H(4, 0, "198.51.100.7") };
                if (IspReport.StartOfLoss(clean) != -1) throw new InvalidOperationException("a router ignoring probes was blamed");
                var bad = new List<PathTools.Hop> { H(1, 0, "192.168.1.1"), H(2, 0, "203.0.113.1"), H(3, 8, "203.0.113.9"), H(4, 7, "198.51.100.7") };
                if (IspReport.StartOfLoss(bad) != 2) throw new InvalidOperationException("loss start " + IspReport.StartOfLoss(bad));
                if (!IspReport.Private(IPAddress.Parse("100.64.3.1")) || !IspReport.Private(IPAddress.Parse("fd00::1")) || IspReport.Private(IPAddress.Parse("8.8.8.8"))) throw new InvalidOperationException("private address check");
            });
            T("wave 4: UDP MTU packets, neighbour filter, speed grade, mode setting", () =>
            {
                foreach (int n in new[] { 100, 572, 1200, 1472 }) { var d = UdpMtu.DnsPad(n); if (d.Length != n) throw new InvalidOperationException("DNS pad " + n + " -> " + d.Length); if (!UdpMtu.IsDns(new byte[] { d[0], d[1], 0x81, 0x80, 0, 1, 0, 0, 0, 0, 0, 0 }, d)) throw new InvalidOperationException("DNS reply match"); }
                var q = UdpMtu.Quic(1172); if (q.Length != 1172 || (q[0] & 0xC0) != 0xC0 || q[5] != 8) throw new InvalidOperationException("QUIC probe");
                if (!UdpMtu.IsVn(new byte[] { 0x80, 0, 0, 0, 0, 8, 1, 2 }) || UdpMtu.IsVn(q)) throw new InvalidOperationException("version negotiation check");
                if (!Reach.SameNet("192.168.1.20", "192.168.1.5") || Reach.SameNet("192.168.1.255", "192.168.1.5") || Reach.SameNet("192.168.2.20", "192.168.1.5") || Reach.SameNet("192.168.1.5", "192.168.1.5")) throw new InvalidOperationException("same network");
                if (SpeedTest.Pick("auto").Id != "cloudflare" || SpeedTest.Pick("vultr toronto").Id != "vultr-tor-ca" || SpeedTest.Pick("ovh-ca") == null || SpeedTest.Pick("ovh montreal").Id != "ovh-ca" || SpeedTest.Pick("nowhere at all") != null) throw new InvalidOperationException("speed server pick");
                if (!SpeedTest.SafeUrl("https://example.com/100MB.bin") || SpeedTest.SafeUrl("https://user:pw@example.com/x") || SpeedTest.SafeUrl("file:///c:/x") || SpeedTest.SafeUrl("ftp://example.com/x")) throw new InvalidOperationException("speed URL check");
                if (SpeedTest.All().Select(z => z.Id).Distinct().Count() != SpeedTest.All().Count || SpeedTest.All().Any(z => !SpeedTest.SafeUrl(z.Down))) throw new InvalidOperationException("speed list");
                if (SpeedTest.GradeOf(10, 12) != "A+" || SpeedTest.GradeOf(10, 90) != "C" || SpeedTest.GradeOf(double.NaN, 5) != "") throw new InvalidOperationException("speed grade");
                string old = Cfg.Mode; try { Cfg.Read("mode", "pro"); if (Cfg.Simple) throw new InvalidOperationException("pro"); Cfg.Read("mode", "nonsense"); if (Cfg.Mode != "" || !Cfg.Simple) throw new InvalidOperationException("bad mode kept"); } finally { Cfg.Mode = old; }
            });
            T("VPN layer verdict", () =>
            {
                if (!VpnAware.Verdict(3, 90, 120, true).StartsWith("Your VPN is the slow part", StringComparison.Ordinal)) throw new InvalidOperationException("slow tunnel not blamed");
                if (!VpnAware.Verdict(3, 12, 140, true).StartsWith("The tunnel itself is quick", StringComparison.Ordinal)) throw new InvalidOperationException("quick tunnel blamed");
                if (VpnAware.Verdict(3, double.NaN, 40, false) != "") throw new InvalidOperationException("no answer, no verdict");
            });
            T("admin policy only switches things off; report JSON is valid", () =>
            {
                try
                {
                    Policy.Reset(); Policy.Parse(new[] { "# test", "internet_check = off", "speed_test=on", "history_days = 2 # two", "mode = simple", "traffic_lab = maybe", "history_days = 40" });
                    if (!Policy.NoInternetCheck || Policy.NoSpeed || Policy.MaxHistoryDays != 2 || Policy.Mode != "simple" || Policy.NoLab) throw new InvalidOperationException("policy parse");
                    var rows = new List<string[]>(); Policy.NoLab = true; if (Lab.Allowed(null, "yes", rows) || rows.Count != 1) throw new InvalidOperationException("lab not blocked");
                }
                finally { Policy.Reset(); }
                var r = new GuideRun(); r.Obs("m", 1, "Router \"ping\"", "12 ms\n"); r.Conclude(1, "Wi-Fi is weak");
                string j = Report.Json(new Monitor(), r, 1);
                var o = Json.Parse(j) as Dictionary<string, object>;
                if (o == null || Convert.ToString(o["verdict"], CultureInfo.InvariantCulture) != "warning" || !(o["observed"] is List<object>)) throw new InvalidOperationException("report JSON: " + j);
            });
            T("every Traffic Lab sender needs the company-network tick", () =>
            {
                var miss = FullView.LabToolsMissingGate().ToList(); if (miss.Count > 0) throw new InvalidOperationException("not gated: " + string.Join(", ", miss));
            });
            T("plain-English guide covers every label", () =>
            {
                var keys = new List<string> { "path.PC", "path.Wi-Fi", "path.LAN", "path.ISP", "path.VPN", "path.DNS", "path.Internet", "CPU", "Memory", "Disk", "Download", "Top by memory", "Top by CPU",
                    "TCP retransmits", "TCP resets sent", "Failed connection attempts", "ICMP unreachable received", "UDP datagrams dropped", "live.PROTOCOL HIERARCHY", "live.EXPERT INFO", "live.TOP CONVERSATIONS",
                    "TCP retransmissions", "Duplicate ACKs", "Segments lost before capture", "Out of order", "Zero window", "Connection resets (RST)", "TLS alerts",
                    "card.SIGN-IN PAGE (CAPTIVE PORTAL)", "card.WHO'S TALKING TO IT", "card.PATH STORY", "story.lan", "story.cgnat", "story.hidden", "story.net",
                    "link.RADIO", "link.CABLE", "link.LINK TO YOUR ROUTER", "link.ADDRESSES", "link.TRAFFIC SINCE THE ADAPTER STARTED", "link.CHANNEL MAP", "link.NEIGHBOURS",
                    "link.Network (SSID)", "link.Access point (BSSID)", "link.Band", "link.Channel", "link.Standard", "link.Signal", "link.Receive rate", "link.Transmit rate", "link.Security",
                    "link.Sharing your channel", "link.Air time used", "link.Connection", "link.Adapter", "link.Link speed", "link.IPv4 address", "link.IPv6", "link.Gateway (router)",
                    "link.DNS servers", "link.Address from", "link.Packet size (MTU)", "link.Hardware address (MAC)", "link.Received", "link.Sent", "link.Damaged packets (errors)",
                    "link.Dropped packets (discards)", "link.Router answers in", "link.Jitter", "link.Loss", "link.Based on",
                    "theme.toggle", "link.Channel width", "link.Roaming help", "link.Driver", "link.DROPS", "link.DHCP", "link.LAN", "link.SWITCH", "live.BY APP", "live.CALL QUALITY", "live.MOS", "live.DHCP", "hist.CHART", "card.DNS SECOND OPINION",
                    "link.ADVISOR", "link.HEALTH", "link.TWINS", "link.WALK", "hist.WEEK", "hist.USAGE", "hist.CALLS", "hist.WATCH", "hist.DEVICES", "dest.PINWATCH", "travel.LAYERS", "travel.L7", "travel.L6·5", "travel.L4", "travel.L3", "travel.L2", "travel.L1", "travel.L↗",
                    "travel.LADDER", "travel.ANATOMY", "travel.WINS", "travel.ROUTES", "travel.ADDR", "travel.FAMILIES", "travel.ROLES", "travel.FALLBACK", "travel.CALLFLOW", "travel.OUTLOOK", "travel.REAL", "travel.DECODE", "travel.CHAT", "travel.CHATNOTES", "kind.base", "kind.lan" };
                keys.AddRange(DestResult.Steps.Select(x => "step." + x));
                keys.AddRange(new[] { "m", "w", "p", "i", "u" }.Select(Evidence.Key)); keys.AddRange(Investigate.All.Select(f => "guide." + f.Id)); keys.AddRange(new[] { "replay.SLIDER", "ov.TRAFFIC", "ov.DETAILS", "ov.ONLINE", "kind.pc", "path.Internet", "path.LAN", "path.DNS" }); keys.AddRange(new[] { "mtu", "mtr", "ret", "owners", "overlap", "ip46", "proxy", "hosts", "dnsrules", "port", "teams", "clock", "iperf", "ping", "cloud", "dig", "http", "sweep", "wol", "subnet", "winev", "auth", "pressure", "disk", "space", "bg", "crash", "leaks", "gpu", "changed", "battery", "ping6", "tcpping", "twamp", "httptime", "doh", "ntp", "mail", "ssh", "snmp", "syslog", "radius", "tacacs", "hairpin", "dc", "gpo", "certs", "fw", "listen", "events", "timesrc", "route", "gateway", "v6routers", "rlisten", "arp", "snmpif", "snmptraffic", "snmpcpu", "snmplldp", "snmpsens", "snmpsnap", "snmpmac", "snmpvlan", "snmppoe", "snmplag", "snmpprn", "snmpups", "snmpwalk", "mibs", "icmpb", "udpsend", "tcpsend", "arpwho", "dhcpopt", "trap", "netflow", "httpreq", "stun", "nbname", "finddev", "remote", "certfile", "pfx", "csr", "certconv", "hashf", "b64", "certcmds", "manyports", "allips", "rdpcheck", "ldapcheck", "dnscmp", "beforeafter", "twopc", "mysites", "shares", "svcfail", "tidy", "dhcp", "roams", "bloat", "dscp", "discover", "ispreport", "speedtest", "udpmtu", "vpn" }.Select(x => "tb." + x)); keys.Add("card.IPV6");
                keys.AddRange(new[] { "Uptime", "Pending reboot", "Disk", "Adapter errors", "Wi-Fi neighbourhood", "Wi-Fi", "Proxy", "VPN", "DNS server", "Memory", "Power" }.Select(x => "hc." + x));
                keys.AddRange(new[] { "net", "wifi", "vpn", "dns", "inet", "gw", "app", "call", "verdict" }.Select(x => "kind." + x));
                keys.AddRange(new[] { "ESTABLISHED", "SYN_SENT", "SYN_RCVD", "TIME_WAIT", "CLOSE_WAIT", "LISTEN", "BOUND", "FIN_WAIT1", "FIN_WAIT2", "LAST_ACK", "CLOSING", "CLOSED", "DELETE_TCB" }.Select(x => "state." + x));
                var missing = keys.Where(k => Explain.Term(k) == null).ToList();
                if (missing.Count > 0) throw new InvalidOperationException("no explanation for: " + string.Join(", ", missing));
                foreach (var e in Explain.AllTerms) if (new[] { e.Friendly, e.Tag, e.What, e.How, e.Why, e.Without }.Any(string.IsNullOrEmpty)) throw new InvalidOperationException("empty field in " + e.Friendly);
                foreach (var n in new[] { "svchost", "lsass", "dwm", "explorer", "MsMpEng", "ms-teams", "Teams", "Edge", "OneDrive", "Outlook" })
                    if (Explain.Find(n) == null && Explain.Find(Monitor.RawOf(n)) == null) throw new InvalidOperationException("process not in guide: " + n);
                var topics = Knowledge.Learn();
                foreach (var id in new[] { "dns", "dhcp", "tcp", "tls", "svchost", "boot-chain", "teams-call", "outlook-flow", "osi", "open-website", "apps-internet", "ports", "speed-vs-delay", "streaming-vs-calls", "background-chatter", "push" }) if (Knowledge.Topic(id) == null) throw new InvalidOperationException("learn topic missing: " + id);
                foreach (var t in topics) if (t["title"] == "" || t["kind"] == "" || t["line"] == "" || t["ladder"] == "") throw new InvalidOperationException("learn topic incomplete: " + t.Id);
                sb.AppendLine("      " + Explain.Count + " processes, " + Explain.AllTerms.Count() + " terms and " + topics.Count + " learn topics explained (packed: knowledge rides compressed in the exe)");
            });
            T("captive portal: tell a sign-in page from the real answer", () =>
            {
                string w;
                if (DestCheck.ClassifyPortal("HTTP/1.1 200 OK\r\nContent-Length: 22\r\n\r\nMicrosoft Connect Test", out w) != 0) throw new InvalidOperationException("real answer not recognised");
                if (DestCheck.ClassifyPortal("HTTP/1.1 302 Found\r\nLocation: https://login.hotelwifi.example/portal?x=1\r\n\r\n", out w) != 1 || w != "login.hotelwifi.example") throw new InvalidOperationException("redirect portal missed: " + w);
                if (DestCheck.ClassifyPortal("HTTP/1.1 200 OK\r\n\r\n<html>Welcome! Accept terms</html>", out w) != 1) throw new InvalidOperationException("replaced page missed");
                if (DestCheck.ClassifyPortal("HTTP/1.1 511 Network Authentication Required\r\n\r\n", out w) != 1) throw new InvalidOperationException("511 missed");
                if (DestCheck.ClassifyPortal("garbage", out w) != 2 || DestCheck.ClassifyPortal(null, out w) != 2) throw new InvalidOperationException("junk not 'can't tell'");
                if (DestCheck.ClassifyPortal("HTTP/1.1 302 Found\r\nLocation: javascript:alert(1)\r\n\r\n", out w) != 1 || w != "") throw new InvalidOperationException("odd Location shown: " + w);
            });
            T("settings round trip + presets", () =>
            {
                Cfg.ApplyPreset("penny"); int penny = Cfg.JobsPerMinute(); Cfg.ApplyPreset("engineer"); int eng = Cfg.JobsPerMinute(); Cfg.ApplyPreset("balanced"); int bal = Cfg.JobsPerMinute();
                if (!(penny < bal && bal < eng)) throw new InvalidOperationException("presets not ordered: " + penny + "/" + bal + "/" + eng);
                Cfg.Pins.Clear(); Cfg.Read("pin", "github.com"); Cfg.Read("pin", "rm -rf /"); Cfg.Read("pin", "https://jira.example.com/x?a=b"); Cfg.Read("gw", "999"); Cfg.Read("fade", "128");
                if (Cfg.Pins.Count != 2 || Cfg.GwSec != 5 || Cfg.Fade != 128) throw new InvalidOperationException("settings read wrong: pins=" + Cfg.Pins.Count + " gw=" + Cfg.GwSec + " fade=" + Cfg.Fade);
                var lines = Cfg.Lines().ToList(); Cfg.Pins.Clear(); Cfg.Fade = 77;
                foreach (var l in lines) { int eq = l.IndexOf('='); Cfg.Read(l.Substring(0, eq), l.Substring(eq + 1)); }
                if (Cfg.Pins.Count != 2 || Cfg.Fade != 128) throw new InvalidOperationException("round trip lost data");
                Cfg.Pins.Clear(); Cfg.Fade = 77;
                sb.AppendLine("      jobs a minute: penny " + penny + ", balanced " + bal + ", engineer " + eng);
            });
            T("Wi-Fi neighbours and path story parsers", () =>
            {
                string nets = "SSID 1 : HomeNet\r\n    Network type            : Infrastructure\r\n    Authentication          : WPA2-Personal\r\n    BSSID 1                 : aa:bb:cc:dd:ee:01\r\n         Signal             : 88%\r\n         Radio type         : 802.11ax\r\n         Band               : 5 GHz\r\n         Channel            : 36\r\n         Bss Load:\r\n             Connected Stations:        4\r\n             Channel Utilization:       52 (20 %)\r\n    BSSID 2                 : aa:bb:cc:dd:ee:02\r\n         Signal             : 40%\r\n         Channel            : 6\r\nSSID 2 : \r\n    BSSID 1                 : 11:22:33:44:55:66\r\n         Signal             : 70%\r\n         Channel            : 6\r\n";
                var aps = LinkInfo.ParseNetworks(nets);
                if (aps.Count != 3 || aps[0].Util != 20 || aps[0].Stations != 4 || aps[0].Band != "5 GHz" || aps[1].Band != "2.4 GHz" || aps[2].Ssid != "(hidden network)") throw new InvalidOperationException("netsh networks parser wrong");
                if (!LinkInfo.Overlaps(6, 9, "2.4 GHz") || LinkInfo.Overlaps(1, 6, "2.4 GHz") || LinkInfo.Overlaps(36, 40, "5 GHz")) throw new InvalidOperationException("channel overlap wrong");
                if (!LinkInfo.Blocked("Network shell commands need location permission to access WLAN information.")) throw new InvalidOperationException("location block not detected");
                if (DestCheck.OwnerOf("ae-1.r21.tor01.ca.bb.gin.ntt.net") != "NTT" || DestCheck.CityOf("ae-1.r21.tor01.ca.bb.gin.ntt.net") != "Toronto") throw new InvalidOperationException("router name reading wrong");
                if (DestCheck.OwnerOf("lo-0.gw1.example.co.uk") != "example.co.uk" || DestCheck.Kind("100.72.1.1") != "cgnat" || DestCheck.Kind("192.168.0.1") != "private" || DestCheck.Kind("8.8.8.8") != "public") throw new InvalidOperationException("address kinds wrong");
            });
            T("path story: NAT, carriers, handoffs, firewall", () =>
            {
                Func<string, double, Tuple<string, double, int>> H = (ip, ms) => Tuple.Create(ip, ms, 0);
                var hops = new List<Tuple<string, double, int>> { H("192.168.0.1", 2), H("10.0.0.1", 4), H("100.72.1.1", 9), H("24.156.1.1", 12), Tuple.Create((string)null, 0.0, 100),
                                                                  H("209.148.1.1", 14), H("129.250.2.1", 16), H("129.250.3.1", 88), H("140.82.114.4", 90) };
                var names = new Dictionary<string, string> { { "24.156.1.1", "8080-dgw01.ym.rogers.com" }, { "209.148.1.1", "3020-cgw01.tor.rogers.com" },
                    { "129.250.2.1", "ae-5.r20.tor01.ca.bb.gin.ntt.net" }, { "129.250.3.1", "ae-1.r24.asbnva02.us.bb.gin.ntt.net" }, { "140.82.114.4", "lb-140-82-114-4-iad.github.com" } };
                var r = new DestResult();
                DestCheck.Story(r, IPAddress.Parse("140.82.114.4"), hops, 8, "192.168.0.50", false, 1492, "", 20, 0, 90, 443, names);
                var chain = string.Join(" > ", r.Story.Select(x => x[0])); var all = string.Join(" | ", r.Findings.Select(f => f[0]));
                sb.AppendLine("      " + chain);
                foreach (var f in r.Findings) sb.AppendLine("      * " + Draw.Trunc(f[0], 110));
                if (chain != "Your PC > Your network > Carrier-grade NAT > Rogers > NTT > GitHub") throw new InvalidOperationException("segments wrong: " + chain);
                foreach (var want in new[] { "Double NAT", "Carrier-grade NAT", "Handoff at hop 7: Rogers → NTT", "PPPoE", "stayed silent", "between hop 7 and 8" })
                    if (all.IndexOf(want, StringComparison.Ordinal) < 0) throw new InvalidOperationException("missing finding: " + want);
                var r2 = new DestResult();
                DestCheck.Story(r2, IPAddress.Parse("140.82.114.4"), hops.Take(4).ToList(), -1, "192.168.0.50", false, 0, "refused", 0, 3, 90, 22, names);
                if (!r2.Findings.Any(f => f[0].Contains("closer than the server"))) throw new InvalidOperationException("nearby firewall reject not spotted");
            });
            T("deep parsers: Wi-Fi IEs, switch, event log, call quality", () =>
            {
                // HT 40 MHz + VHT 80 MHz + RM caps (11k) + mobility domain (11r) + ext caps with BSS transition (11v)
                var ies = new byte[] { 61, 22, 36, 0x05 }.Concat(new byte[20]).Concat(new byte[] { 192, 5, 1, 42, 0, 0, 0, 70, 5, 0, 0, 0, 0, 0, 54, 3, 0x12, 0x34, 0x01, 127, 3, 0, 0, 0x08 }).ToArray();
                var b = new WlanApi.Bss { FreqMHz = 5180 }; WlanApi.FromFreq(b); WlanApi.ReadIes(ies, b);
                if (b.Channel != 36 || b.Band != "5 GHz" || b.Width != 80 || !b.K || !b.V || !b.R) throw new InvalidOperationException("IE parser: ch " + b.Channel + " w " + b.Width + " k" + b.K + " v" + b.V + " r" + b.R);
                var b6 = new WlanApi.Bss { FreqMHz = 5975 }; WlanApi.FromFreq(b6); if (b6.Band != "6 GHz" || b6.Channel != 5) throw new InvalidOperationException("6 GHz channel wrong");
                var lldp = "Link Layer Discovery Protocol\r\n    Chassis Subtype = MAC address, Id: 00:11:22:33:44:55\r\n    Port Subtype = Interface name, Id: Gi1/0/12\r\n    Port Description = Desk 4B\r\n    System Name = floor3-sw1\r\n    System Description = Cisco IOS Software, C9300\r\n    Port VLAN Identifier: 120 (0x0078)\r\n";
                var si = LiveCapture.ParseSwitch(lldp);
                if (si == null || si.Name != "floor3-sw1" || si.Port != "Gi1/0/12" || si.Vlan != "120" || si.Proto != "LLDP") throw new InvalidOperationException("LLDP parser wrong");
                var cdp = LiveCapture.ParseSwitch("Cisco Discovery Protocol\r\n    Device ID: lab-sw2.example\r\n    Port ID: GigabitEthernet0/7\r\n    Native VLAN: 30\r\n    Platform: cisco WS-C2960X\r\n");
                if (cdp == null || cdp.Port != "GigabitEthernet0/7" || cdp.Vlan != "30" || cdp.Proto != "CDP") throw new InvalidOperationException("CDP parser wrong");
                if (LiveCapture.ParseSwitch("nothing here") != null) throw new InvalidOperationException("switch parser invented a switch");
                if (EvLog.Field("<EventData><Data Name='SSID'>Home &amp; Co</Data><Data Name='Reason'>The network is disconnected by the user.</Data></EventData>", "Reason") != "The network is disconnected by the user.") throw new InvalidOperationException("event field reader wrong");
                if (EvLog.Field("<Data Name=\"SSID\">Home &amp; Co</Data>", "SSID") != "Home & Co") throw new InvalidOperationException("event field decode wrong");
                if (LiveCapture.Mos(20, 2, 0) < 4.2 || LiveCapture.Mos(150, 40, 10) > 3.2) throw new InvalidOperationException("MOS model off");
                // 300 RTP packets at 50/s, 16 kHz clock, 3 lost -> ~1% loss, near-zero jitter
                var lc = new LiveCapture(); lc.SetLocalForTest(new[] { "10.0.0.82" });
                for (int i = 0; i < 300; i++)
                {
                    if (i == 100 || i == 101 || i == 200) continue;
                    var f = new string[21]; for (int j = 0; j < 21; j++) f[j] = "";
                    f[0] = "200"; f[1] = "eth:ethertype:ip:udp:rtp"; f[2] = "52.112.0.5"; f[3] = "10.0.0.82"; f[8] = "3480"; f[9] = "50020"; f[15] = "RTP";
                    f[16] = (i * 0.02).ToString("0.000", CultureInfo.InvariantCulture); f[17] = "0x1234"; f[18] = ((65500 + i) % 65536).ToString(CultureInfo.InvariantCulture); f[19] = (1000 + i * 320).ToString(CultureInfo.InvariantCulture);
                    lc.FeedForTest(string.Join("\t", f));
                }
                var v = lc.Snapshot(null);
                if (v.Calls.Count != 1) throw new InvalidOperationException("RTP stream not found");
                var c = v.Calls[0]; double loss = double.Parse(c[3].TrimEnd('%'), CultureInfo.CurrentCulture);
                sb.AppendLine("      call: " + string.Join(" · ", c.Take(6)));
                if (Math.Abs(loss - 1.0) > 0.2 || c[1] != "audio") throw new InvalidOperationException("RTP loss/kind wrong: " + string.Join(",", c));
            });
            T("process file paths without admin", () =>
            {
                int pid; using (var me = Process.GetCurrentProcess()) pid = me.Id;
                string path = Native.ExePath(pid); var f = Explain.FileFacts(path);
                sb.AppendLine("      own exe: " + (path != "" ? Path.GetFileName(path) : "(hidden)") + " · lives in: " + f[3]);
                if (path == "") throw new InvalidOperationException("could not read own path");
            });
            T("timeline, Wi-Fi roam and hints (simulated)", () =>
            {
                var sim = new Monitor(); var log = new StringBuilder();
                string h = sim.SimulateForTest(log); sb.Append(log); sb.AppendLine("      hint: " + h);
                var all = string.Join("\n", sim.Events.Recent(50).Select(e => e.Text));
                foreach (var must in new[] { "VPN connected", "VPN d", "DNS server changed", "IP address changed", "Switched to LAN", "another access point", "signal fell to 40%", "link rate dropped 866", "Wi-Fi d" })
                    if (all.IndexOf(must, StringComparison.Ordinal) < 0) throw new InvalidOperationException("missing event: " + must);
                if (h.IndexOf("Wi-Fi signal fell to 40%", StringComparison.Ordinal) < 0) throw new InvalidOperationException("wrong hint: " + h);
            });
            T("Ctrl+Alt+A shortcut is free", () =>
            {
                bool running = Process.GetProcessesByName("AdeeboMeter").Length > 1;
                if (running) { sb.AppendLine("      skipped: AdeeboMeter is running and holds it"); return; }
                using (var hk = new HotKey(delegate { })) if (!hk.Ok) throw new InvalidOperationException("another app already uses Ctrl+Alt+A");
            });
            T("memory after everything", () =>
            {
                GC.Collect(); GC.WaitForPendingFinalizers();
                using (var p = Process.GetCurrentProcess()) { long mb = p.PrivateMemorySize64 / 1048576; sb.AppendLine("      private " + mb + " MB (budget 64)"); if (mb > 96) throw new InvalidOperationException(mb + " MB"); }
            });
            T("install: paths, settings line, icon", () =>
            {
                if (!Setup.Under(Path.Combine(Setup.Base, "1.0"), Setup.Base) || Setup.Under(Path.GetTempPath(), Setup.Base)) throw new InvalidOperationException("Under()");
                if (Setup.Under(Setup.Base + "-evil", Setup.Base)) throw new InvalidOperationException("prefix trick accepted");
                string f = Path.Combine(Path.GetTempPath(), "AdeeboMeter-test-" + Guid.NewGuid().ToString("N") + ".settings");
                try
                {
                    File.WriteAllLines(f, new[] { "widget=1", "autostart=1" });
                    Setup.SetLine(f, "autostart", "0"); Setup.SetLine(f, "serious", "1");
                    var l = File.ReadAllLines(f);
                    if (!l.Contains("autostart=0") || !l.Contains("serious=1") || !l.Contains("widget=1") || l.Length != 3) throw new InvalidOperationException(string.Join("|", l));
                }
                finally { try { File.Delete(f); } catch { } }
                var ico = Theme.IcoBytes();
                if (ico.Length < 2000 || ico[2] != 1 || ico[4] != 9) throw new InvalidOperationException("ico header");
                sb.AppendLine("      installs to " + Setup.Base + " · icon " + ico.Length / 1024 + " KB");
            });
            T("GDI / user handles", () =>
            {
                using (var p = Process.GetCurrentProcess()) sb.AppendLine("      handles=" + p.HandleCount);
            });
            sb.AppendLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
            try { File.WriteAllText(Path.Combine(Program.WriteDir, "AdeeboMeter-selftest.txt"), sb.ToString()); } catch { }
            M.Stop();
            return fails;
        }

        static void AppendStory(StringBuilder sb, DestResult r)
        {
            if (r.Story == null) return;
            sb.AppendLine("  [PATH STORY]");
            sb.AppendLine("     " + string.Join("  ->  ", r.Story.Select(x => x[0] + " (" + x[1] + ")")));
            foreach (var f in r.Findings ?? new List<string[]>()) sb.AppendLine("     * " + f[0]);
        }
        public static int CheckOnce(string dest)
        {
            var M = new Monitor(); M.RefreshInterfaces(); var r = new DestResult();
            DestCheck.Run(dest, r, delegate { }, M);
            var sb = new StringBuilder(dest + "\nverdict: " + r.Verdict + "\n");
            for (int i = 0; i < 8; i++) sb.AppendLine("  " + DestResult.Steps[i].PadRight(9) + " lv=" + r.Lv[i] + "  " + r.Val[i]);
            foreach (var c in r.CardsCopy()) { sb.AppendLine("  [" + c.Key + "]"); foreach (var row in c.Value) sb.AppendLine("     " + row[0] + ": " + (row.Length > 1 ? row[1] : "")); }
            AppendStory(sb, r);
            File.AppendAllText(Path.Combine(Program.WriteDir, "AdeeboMeter-check.txt"), sb.ToString() + "\n");
            return 0;
        }

        // Feeds tshark field lines from a file through the live-capture parser and prints what the Live tab would show.
        public static int ParseTest(string file)
        {
            var lc = new LiveCapture(); var sb = new StringBuilder(); var sw = Stopwatch.StartNew(); int n = 0;
            foreach (var line in File.ReadLines(file)) { lc.FeedForTest(line); n++; }
            var v = lc.Snapshot(null);
            sb.AppendLine(n + " lines parsed in " + sw.ElapsedMilliseconds + " ms · " + lc.Dropped + " malformed lines dropped (not counted)");
            foreach (var r in v.Phs) sb.AppendLine(new string(' ', int.Parse(r[0], CultureInfo.InvariantCulture) * 2) + r[1] + "  " + r[2] + "  " + r[3]);
            sb.AppendLine("-- top conversations");
            foreach (var c in v.Top) sb.AppendLine(c.Host + "  " + c.Proto + "  " + c.Remote + ":" + c.RemotePort + "  bytes=" + c.Bytes + " retx=" + c.Retx);
            sb.AppendLine("-- expert");
            foreach (var e in v.Expert) sb.AppendLine(e[1] + " = " + e[2]);
            File.WriteAllText(Path.Combine(Program.WriteDir, "AdeeboMeter-parsetest.txt"), sb.ToString());
            return 0;
        }
    }
}
