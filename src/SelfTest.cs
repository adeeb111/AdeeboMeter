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
        // the phone remote's ECDH against a WebCrypto vector - its own method so a missing CNG type (Wine) is caught as one
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void RemoteEcdhVector()
        {
            var priv = RemoteCrypto.Unb64u("WI52uFtWGuVTT912etBFaFm2mmlsTW1EeRYQ9Sv560sGnwH9W_GT9M0VzkTjh1Xm6j04shRHlN5PdCJsa92cdNK1Zj8B_EIWKHLrdeoPYkQwt9t-5kIc2RB64fUwy9vn");
            var blob = new byte[8 + 96]; blob[0] = 0x45; blob[1] = 0x43; blob[2] = 0x4B; blob[3] = 0x32; blob[4] = 32; Buffer.BlockCopy(priv, 0, blob, 8, 96);
            byte[] z;
            using (var key = System.Security.Cryptography.CngKey.Import(blob, System.Security.Cryptography.CngKeyBlobFormat.EccPrivateBlob)) using (var me = new System.Security.Cryptography.ECDiffieHellmanCng(key))
                z = RemoteCrypto.SharedHash(me, RemoteCrypto.Unb64u("BG-09qgHAX1x4uIdROM5baZkFc_XMNzVJrlELLKPGryTlMztVlJmg4eI80ik5YgSZvZB-jd1jfcwkZpwyz9tDkA"));
            if (RemoteCrypto.B64u(z) != "dLo6VSnV2DaBhX1WfU81Ak_KkERsBGlIrRY8zIDTzJI") throw new InvalidOperationException("ECDH differs");
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5351", Justification = "checks RADIUS (RFC 2865) password hiding, which is defined with MD5")]
        static byte[] Md5(byte[] d) { using (var h = System.Security.Cryptography.MD5.Create()) return h.ComputeHash(d); }
        /* Is this ASCII word in the bytes - as single bytes, or as UTF-16 (each letter followed by a zero byte)? No copies made. */
        static bool InBytes(byte[] hay, string word, bool wide, bool ignoreCase)
        {
            int step = wide ? 2 : 1, n = word.Length * step;
            Func<int, int> low = c => ignoreCase && c >= 'A' && c <= 'Z' ? c + 32 : c;
            for (int i = 0; i + n <= hay.Length; i++)
            {
                int k = 0;
                while (k < word.Length && low(hay[i + k * step]) == low(word[k]) && (!wide || hay[i + k * step + 1] == 0)) k++;
                if (k == word.Length) return true;
            }
            return false;
        }
        // --perf: how long each page takes to switch to, to paint, and what the 2-a-second timer costs - written to AdeeboMeter-perf.txt
        static double MemNow(Monitor M) { double m = M.Mem.Last; return double.IsNaN(m) && M.Latest != null ? M.Latest.Mem : m; }   // real-PC find: Latest.Mem can be empty while the series has it
        static double Busy(Monitor M) { try { double c = M.Cpu.Last, m = MemNow(M); return (!double.IsNaN(c) && c > 50) || (!double.IsNaN(m) && m > 85) ? 3 : 1; } catch (NullReferenceException) { return 1; } }
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
            // the checks simulate Wi-Fi drops, VPNs and roams: none of it may land in your real history, or raise a pop-up
            Cfg.History = false; Cfg.Alerts = false; Cfg.Portal = false;   // and no sign-in-page request: the self-test stays off the internet
            Action<string, Action> T = delegate(string name, Action a)
            {
                var sw = Stopwatch.StartNew();
                try { a(); sb.AppendLine("PASS  " + name.PadRight(40) + sw.ElapsedMilliseconds + " ms"); }
                catch (Exception e) { fails++; sb.AppendLine("FAIL  " + name.PadRight(40) + e.GetType().Name + ": " + e.Message); }
            };
            sb.AppendLine("AdeeboMeter " + Program.Version + " self-test  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture) + "  " + Environment.OSVersion + "  CLR " + Environment.Version + "  " + (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? (Environment.Is64BitProcess ? "64-bit" : "32-bit")));
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
                Func<List<string[]>, string, string> val = (rs, name) => { var rr = rs.FirstOrDefault(r => r[1] == name); return rr == null ? "" : rr[2]; };
                var stk = Calc.Stack("tcp ipv4 wireguard pppoe ethernet", "", "1400"); string mss = val(stk, "Biggest segment (MSS)"), inner = val(stk, "Set the inner MTU to");
                if (!mss.Replace(",", "").Replace(".", "").Replace("\u00a0", "").Replace(" ", "").StartsWith("1392", StringComparison.Ordinal) || !inner.Replace(",", "").Replace(".", "").Replace("\u00a0", "").Replace(" ", "").StartsWith("1432", StringComparison.Ordinal)) throw new InvalidOperationException("packet stack wrong: " + mss + " / " + inner);
                if (Calc.Stack("tcp banana", "", "")[0][0] != "2" || Calc.Stack("udp ipv4 ethernet", "", "3000").All(r => !r[2].Contains("fragments"))) throw new InvalidOperationException("packet stack input checks wrong");
                var vl = Calc.Vlsm("10.0.0.0/22", "200, 60, 12, 2"); if (!val(vl, "Subnet 2 (60 hosts)").StartsWith("10.0.1.0/26", StringComparison.Ordinal) || !val(vl, "Subnet 4 (2 hosts)").StartsWith("10.0.1.80/30", StringComparison.Ordinal) || !Calc.Vlsm("10.0.0.0/24", "200, 100").Any(r => r[0] == "2")) throw new InvalidOperationException("VLSM wrong");
                var bw = Calc.Bandwidth("4.7 GB", "100 Mbps", ""); if (!val(bw, "Time").Contains("min")) throw new InvalidOperationException("bandwidth wrong: " + val(bw, "Time"));
                // round-2 review: a subnet that doesn't fit leaves the room for smaller ones; 1 host is a /30; leading zeros are decimal
                var v2 = Calc.Vlsm("10.0.1.0/24", "500, 100"); if (!val(v2, "Subnet 1 (500 hosts)").Contains("doesn't fit") || !val(v2, "Subnet 2 (100 hosts)").StartsWith("10.0.1.0/25", StringComparison.Ordinal)) throw new InvalidOperationException("VLSM after a subnet that doesn't fit: " + val(v2, "Subnet 2 (100 hosts)"));
                if (!val(Calc.Vlsm("10.0.0.0/24", "1, 1"), "Subnet 2 (1 hosts)").StartsWith("10.0.0.4/30", StringComparison.Ordinal)) throw new InvalidOperationException("VLSM 1 host should be a /30");
                if (!val(Calc.Vlsm("010.0.0.0/8", "100"), "Block").StartsWith("10.0.0.0/8", StringComparison.Ordinal) || Calc.Vlsm("0x0A.0.0.0/8", "100")[0][0] != "2" || val(Calc.Number("010.1.1.1"), "Decimal") != "167837953") throw new InvalidOperationException("leading zeros read as octal");
                if (!val(Calc.Bandwidth("1,500 MB", "100 Mbps", ""), "Time").StartsWith("2 min", StringComparison.Ordinal) || !val(Calc.Bandwidth("4,7 GB", "100 Mbps", ""), "Time").Contains("min") || Calc.Bandwidth("4.7 GB", "0", "")[0][0] != "2") throw new InvalidOperationException("bandwidth thousands / zero: " + val(Calc.Bandwidth("1,500 MB", "100 Mbps", ""), "Time"));
                if (val(Calc.Number("100000000"), "Decimal") != "100000000" || val(Calc.Number("101b"), "Decimal") != "5") throw new InvalidOperationException("number converter read decimal as binary");
                if (!val(Calc.Stack("web", "", ""), "Biggest segment (MSS)").Replace(",", "").Replace(".", "").Replace("\u00a0", "").Replace(" ", "").StartsWith("1460", StringComparison.Ordinal)) throw new InvalidOperationException("TLS taken off the MSS: " + val(Calc.Stack("web", "", ""), "Biggest segment (MSS)"));
                if (!Calc.Stack("udp ipv4 ethernet", "", "2952").Any(r => r[2].Contains("into 2 fragments"))) throw new InvalidOperationException("UDP fragment count");
                var nb = Calc.Number("0xC8"); if (val(nb, "Decimal") != "200" || val(nb, "Binary") != "11001000" || val(Calc.Number("255.255.252.0"), "As a mask").StartsWith("/22", StringComparison.Ordinal) == false || val(Calc.Number("00:11:22:aa:bb:cc"), "As a MAC") != "00:11:22:aa:bb:cc") throw new InvalidOperationException("number converter wrong");
                Func<string, string, string> gwv = (a, g) => { var gr = Toolkit.Subnet(a, "", g, null).FirstOrDefault(r => r[1] == "Gateway"); return gr == null ? "none" : gr[0] + ":" + gr[2]; };
                string g1 = gwv("10.20.3.7/31", "10.20.0.1/21"), g2 = gwv("10.1.2.3/24", "10.1.2.0"), g3 = gwv("10.1.2.3/24", "10.1.2.255"), g4 = gwv("10.1.2.3/24", "10.1.2.3"), g5 = gwv("10.1.2.3/24", "10.1.2.1/23"), g6 = gwv("10.1.2.3/24", "10.1.2.1"), g7 = gwv("10.1.2.6/31", "10.1.2.7"), g8 = gwv("2001:db8::5/64", "fe80::1"), g9 = gwv("10.1.2.3/24", "10.1.2.77"), g10 = gwv("10.1.2.3/24", ""), g11 = gwv("10.1.2.3/24", "10.1.9.1");
                if (!g1.StartsWith("2:Won't work as a normal gateway", StringComparison.Ordinal) || !g1.Contains("Use /21") || !g2.StartsWith("2:", StringComparison.Ordinal) || !g2.Contains("network address") || !g3.Contains("broadcast") || !g4.Contains("own address")
                    || !g5.StartsWith("1:", StringComparison.Ordinal) || !g6.StartsWith("0:", StringComparison.Ordinal) || !g6.Contains("first") || !g7.StartsWith("0:", StringComparison.Ordinal) || !g8.StartsWith("0:", StringComparison.Ordinal) || !g9.Contains("not the first") || !g10.Contains("10.1.2.1 (first)") || !g11.Contains("/20"))
                    throw new InvalidOperationException("subnet gateway checks wrong: " + string.Join(" | ", g1, g2, g3, g4, g5, g6, g7, g8, g9, g10, g11));
                // Hardware nerds: the specs read, every verdict side of "it felt slow", the ceilings, the seller's words
                {
                    var hs = HwSpecs.Get(); if (hs == null) throw new InvalidOperationException("hardware specs not read");
                    Func<double, double, double, double, bool, PcRecord.Pt> pt = (c, b, net, gw, lost) => new PcRecord.Pt { T = DateTime.Now, Cpu = c, Busy = b, DiskMs = b > 90 ? 120 : 2, Faults = 5, Perf = 100, Dpc = 1, Net = net, Gw = gw, Lost = lost, TopIo = "MsMpEng", TopCpu = "chrome" };
                    var pcSlow = PcRecord.Judge(Enumerable.Range(0, 20).Select(i => pt(30, 99, 14, 2, false)).ToList(), DateTime.Now);
                    var netSlow = PcRecord.Judge(Enumerable.Range(0, 20).Select(i => pt(10, 5, 400, 2, i % 3 == 0)).ToList(), DateTime.Now);
                    var appSlow = PcRecord.Judge(Enumerable.Range(0, 20).Select(i => pt(10, 5, 14, 2, false)).ToList(), DateTime.Now);
                    if (pcSlow.Who != "pc" || !pcSlow.Text.Contains("MsMpEng") || netSlow.Who != "internet" || appSlow.Who != "app" || PcRecord.Judge(new List<PcRecord.Pt>(), DateTime.Now).Who != "unknown")
                        throw new InvalidOperationException("felt-slow verdicts wrong: " + pcSlow.Who + "/" + netSlow.Who + "/" + appSlow.Who);
                    if (!PcRecord.Series(Enumerable.Range(0, 3).Select(i => pt(50, 60, 20, 2, i == 1)).ToList()).Contains("Ping 20 x 20")) throw new InvalidOperationException("felt-slow series wrong");
                    // PC-001..005: no internet readings isn't "fine"; blind counters don't clear the PC; a lone page spike isn't memory; router and internet each count against their own readings; all lost is loss
                    var noNet = PcRecord.Judge(Enumerable.Range(0, 10).Select(i => pt(10, 5, double.NaN, double.NaN, false)).ToList(), DateTime.Now);
                    var blind = PcRecord.Judge(Enumerable.Range(0, 10).Select(i => new PcRecord.Pt { T = DateTime.Now, Cpu = double.NaN, Busy = double.NaN, DiskMs = double.NaN, Faults = double.NaN, Perf = double.NaN, Dpc = double.NaN, Mem = double.NaN, Net = 400, Gw = 2 }).ToList(), DateTime.Now);
                    var page = PcRecord.Judge(Enumerable.Range(0, 5).Select(i => new PcRecord.Pt { T = DateTime.Now, Cpu = 10, Busy = 5, DiskMs = 2, Faults = i == 0 ? 900 : 5, Perf = 100, Dpc = 1, Mem = 20, Net = 14, Gw = 2 }).ToList(), DateTime.Now);
                    var gw1 = PcRecord.Judge(Enumerable.Range(0, 5).Select(i => new PcRecord.Pt { T = DateTime.Now, Cpu = 10, Busy = 5, DiskMs = 2, Faults = 5, Perf = 100, Dpc = 1, Mem = 20, Net = i == 0 ? 14 : double.NaN, Gw = i == 2 ? 90 : 2 }).ToList(), DateTime.Now);
                    var allLost = PcRecord.Judge(Enumerable.Range(0, 6).Select(i => pt(10, 5, double.NaN, 2, true)).ToList(), DateTime.Now);
                    if (noNet.Who != "notpc" || blind.Text.Contains("was fine") || page.Who == "pc" || gw1.Who == "internet" || allLost.Who != "internet" || allLost.Text.Contains("?"))
                        throw new InvalidOperationException("felt-slow edge verdicts wrong: " + noNet.Who + "/" + blind.Text + "/" + page.Who + "/" + gw1.Who + "/" + allLost.Text);
                    // PC-064: a folder named x.exe.backup isn't the program; PC-037/038: A1 + A2 is one channel, nothing listed is unknown
                    string ex1 = Startup.ExeOf(@"C:\Apps\archive.exe.backup\launcher.exe --start"), ex2 = Startup.ExeOf(@"C:\Program Files\X\x.exe -s");
                    int ch1 = HwSpecs.ChannelsOf(new List<HwSpecs.Ram> { new HwSpecs.Ram { Slot = "ChannelA-DIMM1" }, new HwSpecs.Ram { Slot = "ChannelA-DIMM2" } }), ch0 = HwSpecs.ChannelsOf(new List<HwSpecs.Ram>());
                    try { Startup.List(); } catch (ArgumentException ae) { throw new InvalidOperationException("startup list breaks on a command line: " + ae.Message); }   // real-PC find: a quoted Run command
                    if (ex1 != "launcher.exe" || ex2 != "x.exe" || ch1 != 1 || ch0 != 0) throw new InvalidOperationException("startup / channels: " + ex1 + " " + ex2 + " " + ch1 + " " + ch0);
                    string why; var nvme = new HwSpecs.Disk { Bus = "NVMe", Media = "SSD", LinkGen = 3, LinkWidth = 4 }; var hdd = new HwSpecs.Disk { Bus = "SATA", Media = "HDD", Rpm = 5400 };
                    if (Math.Abs(HwSpecs.DiskCeiling(nvme, out why) - 3546) > 5 || HwSpecs.DiskCeiling(hdd, out why) != 110 || HwSpecs.DiskCeiling(new HwSpecs.Disk { Bus = "SATA", Media = "SSD" }, out why) != 550) throw new InvalidOperationException("disk ceilings wrong");
                    var two = new List<HwSpecs.Ram> { new HwSpecs.Ram { Slot = "BANK 0 ChannelA-DIMM0", Mts = 3200 }, new HwSpecs.Ram { Slot = "BANK 2 ChannelB-DIMM0", Mts = 3200 } }; var one = new List<HwSpecs.Ram> { new HwSpecs.Ram { Slot = "DIMM A1", Mts = 3200 } };
                    if (HwSpecs.ChannelsOf(two) != 2 || HwSpecs.ChannelsOf(one) != 1) throw new InvalidOperationException("memory channels wrong");
                    var fake = new HwSpecs.Specs { Cpu = "Intel(R) Core(TM) i5-8250U CPU", RamGb = 8, Cores = 4, Threads = 8, BaseMhz = 1600 }; fake.Disks.Add(new HwSpecs.Disk { Gb = 512, Media = "HDD", Bus = "SATA", System = true }); fake.Gpus.Add(new HwSpecs.Gpu { Name = "Intel UHD Graphics 620", Integrated = true });
                    var sel = HwTools.Seller("i7, 16GB RAM, 512GB SSD, RTX 3050", fake);
                    if (sel.Count != 4 || sel.Count(x => x[0] == "2") != 4 || !sel.Any(x => x[2].Contains("spinning"))) throw new InvalidOperationException("seller check wrong: " + string.Join(" | ", sel.Select(x => x[0] + " " + x[1] + " " + x[2]).ToArray()));
                    if (HwTools.Seller("i5 8gb 512gb ssd", fake).Count(x => x[0] == "0") != 2) throw new InvalidOperationException("seller check: matching specs not matched");
                    foreach (var part in new[] { "cores", "clock", "ram", "disk", "link", "gpu", "net", "battery", "cache" }) if (HwTools.Explain(part, fake).Length < 80) throw new InvalidOperationException("no explanation for " + part);
                    if (!HwTools.Explain("cores", fake).StartsWith("4 cores", StringComparison.Ordinal)) throw new InvalidOperationException("explanations don't use this PC's numbers");
                    HwTools.Errors(30); HwTools.Hidden(M); HwTools.Map(M); HwTools.Gpu();
                }
                // Sports: each feed's shape read right, from made-up answers (nothing is fetched here)
                {
                    var hk = Sports.HockeyFrom("{\"games\":[{\"gameState\":\"LIVE\",\"startTimeUTC\":\"2026-11-15T23:00:00Z\",\"homeTeam\":{\"abbrev\":\"TOR\",\"score\":2},\"awayTeam\":{\"abbrev\":\"MTL\",\"score\":1},\"periodDescriptor\":{\"number\":2},\"clock\":{\"timeRemaining\":\"12:34\",\"inIntermission\":false}},{\"gameState\":\"FUT\",\"homeTeam\":{\"abbrev\":\"OTT\"},\"awayTeam\":{\"abbrev\":\"BOS\"}}]}");
                    if (hk.Count != 2 || hk[0].State != "live" || hk[0].HomeScore != "2" || !hk[0].Detail.Contains("P2") || hk[1].State != "soon") throw new InvalidOperationException("hockey scores read wrong");
                    var fb = Sports.FootballFrom("{\"matches\":[{\"status\":\"PAUSED\",\"utcDate\":\"2026-11-15T15:00:00Z\",\"competition\":{\"name\":\"Premier League\"},\"homeTeam\":{\"shortName\":\"Arsenal\"},\"awayTeam\":{\"shortName\":\"Chelsea\"},\"score\":{\"fullTime\":{\"home\":1,\"away\":0}}}]}");
                    if (fb.Count != 1 || fb[0].State != "live" || fb[0].Detail != "half time" || fb[0].HomeScore != "1") throw new InvalidOperationException("football scores read wrong");
                    var ck = Sports.CricketFrom("{\"status\":\"success\",\"data\":[{\"matchType\":\"odi\",\"status\":\"India need 45 runs\",\"teams\":[\"India\",\"Bangladesh\"],\"score\":[{\"r\":250,\"w\":8,\"o\":50,\"inning\":\"Bangladesh Inning 1\"},{\"r\":206,\"w\":3,\"o\":38.2,\"inning\":\"India Inning 1\"}],\"matchStarted\":true,\"matchEnded\":false}]}");
                    if (ck.Count != 1 || ck[0].State != "live" || ck[0].AwayScore != "250/8 (50)" || ck[0].HomeScore != "206/3 (38.2)") throw new InvalidOperationException("cricket scores read wrong: " + (ck.Count > 0 ? ck[0].HomeScore + " | " + ck[0].AwayScore : "none"));
                    var rs = Sports.Rss("<rss><channel><item><title><![CDATA[Big win &amp; more]]></title><description>A <b>great</b> day</description><link>https://www.bbc.co.uk/sport/x</link></item><item><title>No link</title><link>javascript:bad()</link></item></channel></rss>", "top");
                    if (rs.Count != 1 || rs[0].Title != "Big win & more" || rs[0].Summary != "A great day") throw new InvalidOperationException("sports headlines read wrong");
                    // AM-CG-276: a changed feed is an error, not "no games"; AM-CG-275: whole-word team matching; AM-CG-271: BBC Sport opens
                    bool threw = false; try { Sports.HockeyFrom("{\"gamesX\":[]}"); } catch (InvalidDataException) { threw = true; } if (!threw) throw new InvalidOperationException("a wrong-shaped hockey feed read as no games");
                    string teams0 = Cfg.SportTeams; Cfg.SportTeams = "In,TOR";
                    try { var gx = new Sports.Game { Home = "India", Away = "Bangladesh" }; var gy = new Sports.Game { Home = "MTL", Away = "TOR" }; if (Sports.Mine(gx) || !Sports.Mine(gy)) throw new InvalidOperationException("team matching isn't whole-word"); }
                    finally { Cfg.SportTeams = teams0; }
                    if (!Crash.Allowed("https://www.bbc.co.uk/sport/football/123") || Crash.Allowed("https://www.bbc.co.uk/news/x") || Crash.Allowed("https://evil.bbc.co.uk.example/sport")) throw new InvalidOperationException("BBC Sport link rule");
                }
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
            T("phone remote crypto matches the browser's", () =>
            {
                // vectors made with WebCrypto (what the phone page uses): the same keys, the same ECDH secret, a frame it sealed opens here
                var secret = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
                if (RemoteCrypto.B64u(RemoteCrypto.AuthKey(secret)) != "kVa2aoyX2h1CWA30hJqbyYclFiPT18UuetqVLroHEhM") throw new InvalidOperationException("pairing key differs");
                // the ECDH part needs Windows' CNG (Wine doesn't have it); the rest runs everywhere
                var z = RemoteCrypto.Unb64u("dLo6VSnV2DaBhX1WfU81Ak_KkERsBGlIrRY8zIDTzJI");
                try { RemoteEcdhVector(); } catch (TypeLoadException) { sb.AppendLine("      ECDH vector skipped: this system has no CNG (Wine)"); }
                // the matching words: same recipe and list as the phone page (vector made with WebCrypto and Python)
                if (RemoteWords.List.Length != 256 || RemoteWords.List.Distinct().Count() != 256) throw new InvalidOperationException("the word list isn't 256 different words");
                if (RemoteWords.Of(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(), new byte[] { 4 }.Concat(Enumerable.Range(1, 64).Select(i => (byte)i)).ToArray(), new byte[] { 4 }.Concat(Enumerable.Range(100, 64).Select(i => (byte)i)).ToArray()) != "wagon · bucket · brook · quartz") throw new InvalidOperationException("matching words differ from the phone's");
                var lane = new RemoteCrypto.Lane(secret, z, 1); const string fr = "AAAAAAAAAAHIx8bFxMPCwcC_vr28u7q5HhaFBDnjOMAo4LDWK4DgqbwpaUmVUNdbx3P_dm87XXJ4JeujuUjZ2Cl9tr8x-2hiamubuNxZtgu67gmrfoekcw";
                if (lane.Open(fr) != "{\"t\":\"live\",\"ping\":12.5}") throw new InvalidOperationException("frame didn't open");
                if (lane.Open(fr) != null) throw new InvalidOperationException("a replayed frame opened");
                var bad = new RemoteCrypto.Lane(secret, z, 1); var bb = RemoteCrypto.Unb64u(fr); bb[30] ^= 1; if (bad.Open(RemoteCrypto.B64u(bb)) != null) throw new InvalidOperationException("a changed frame opened");
                var a1 = new RemoteCrypto.Lane(secret, z, 2); var b1 = new RemoteCrypto.Lane(secret, z, 2); if (b1.Open(a1.Seal("hello")) != "hello") throw new InvalidOperationException("round trip");
            });
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
                    if (ms > 2000 * Busy(M)) throw new InvalidOperationException("took " + ms + " ms");   /* 1 s is the aim; 2 s fails - a busy or high-DPI PC lands near 1.3 s */
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
                        // A real leak grows with every open; a one-time allocation (a cache filled on a later open - the
                        // official build showed +11 GDI once, 17 Nov) doesn't. So run 30 more: only growth again is a leak.
                        for (int i = 0; i < 30; i++) cycle(null);
                        Thread.Sleep(1500); Application.DoEvents();
                        GC.Collect(); GC.WaitForPendingFinalizers(); Application.DoEvents();
                        int gdi2 = Native.GetGuiResources(me, 0), usr2 = Native.GetGuiResources(me, 1);
                        sb.AppendLine("      and 30 more: " + res() + "  (a leak keeps growing; a one-time cache doesn't)");
                        if (gdi2 - gdi1 <= 10 && usr2 - usr1 <= 10) return;
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
                        throw new InvalidOperationException("handles grew by " + (gdi1 - gdi0) + " GDI / " + (usr1 - usr0) + " USER over 30 opens, and again by " + (gdi2 - gdi1) + " / " + (usr2 - usr1) + " over 30 more");
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
                if (Reach.SafeHost("a;b") || Reach.SafeUser("x y") || !Reach.SafeHost("sw-01.lab") || Reach.SafeHost("sw-01:22") || !Reach.SafeHost("[2001:db8::1]") || !Reach.SafeHost("2001:db8::1") || !Reach.SafeUser("DOMAIN\\adeeb")) bad.Add("remote inputs");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join(", ", bad));
            });
            T("copied results name each box whole (\"How many (10-2000)\" not \"How many (10\")", () =>
            {
                if (FullView.BoxLabel("How many (10-2000)") != "How many (10-2000)") throw new InvalidOperationException(FullView.BoxLabel("How many (10-2000)"));
                if (FullView.BoxLabel("Target - e.g. github.com") != "Target") throw new InvalidOperationException(FullView.BoxLabel("Target - e.g. github.com"));
                if (FullView.BoxLabel("Wi-Fi name") != "Wi-Fi name") throw new InvalidOperationException("Wi-Fi");
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
                if (plan.Count(z => z.Item3 < GamePing.SurveySec) > GamePing.MaxServers) bad.Add("more than " + GamePing.MaxServers + " servers: " + plan.Count);
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
                // short and HTML-like Wi-Fi names, zones and MAC-built IPv6
                if (GameSession.Redact("<td class=\"n\">12 ms</td>", "td") != "<td class=\"n\">12 ms</td>" || GameSession.Redact("ping 12 ms", "ms") != "ping 12 ms") throw new InvalidOperationException("a short Wi-Fi name ate the text");
                if (GameSession.Redact("On Wi-Fi \"AT&T WiFi\"", "AT&T WiFi").Contains("AT&T")) throw new InvalidOperationException("AT&T not redacted");
                if (GameSession.Redact("Wi-Fi: Bob", "Bob").Contains("Bob") || GameSession.Redact("On ATT (channel 6)", "ATT").Contains("ATT")) throw new InvalidOperationException("a short Wi-Fi name stayed readable");
                if (GameSession.Redact("Your router (192.0.2.1) answered", "Router").IndexOf("router", StringComparison.OrdinalIgnoreCase) < 0) throw new InvalidOperationException("a Wi-Fi name that's a common word erased that word");
                string z = GameSession.Redact("gw fe80::1%12 and 2001:db8::211:22ff:fe33:4455 via 100.64.1.2", "x"); if (z.Contains("%12") || z.Contains("ff:fe33") || z.Contains("100.64")) throw new InvalidOperationException(z);
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
                if (MatchFinder.Busiest("203.0.113.50\t198.51.100.20\n198.51.100.20\t203.0.113.50\n203.0.113.50\t8.8.8.8\n", new HashSet<string> { "203.0.113.50" }) != "198.51.100.20") throw new InvalidOperationException("the PC's own public address won");
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
                // round-2: what's wrong beats where ("at work"); games beat streaming; mail is a site that won't open
                if (go("my work laptop is slow") != "guide:pc") bad.Add("work laptop slow -> " + go("my work laptop is slow"));
                if (go("the network at work is slow") != "guide:slow") bad.Add("network at work slow -> " + go("the network at work is slow"));
                if (go("streaming works but games lag") != "gamer") bad.Add("games lag");
                if (go("I can't get to work email") != "guide:site") bad.Add("work email -> " + go("I can't get to work email"));
                if (go("globalprotect won't connect") != "guide:vpn") bad.Add("vpn product");
                if (go("my internet doesn't work") != "guide:whats") bad.Add("internet doesn't work -> " + go("my internet doesn't work"));
                if (go("printer not working") != "guide:whats") bad.Add("printer");
                if (go("Wi-Fi works but nothing loads") != "guide:whats") bad.Add("nothing loads");
                if (go("my VPN won't connect") != "guide:vpn") bad.Add("vpn won't connect");
                if (go("ping is high") != "guide:slow") bad.Add("ping is high");
                if (go("disk is full") != "tool:tidy") bad.Add("disk is full");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join(", ", bad));
            });
            T("ISP report finds where loss starts and ignores loss that doesn't carry on", () =>
            {
                Func<int, int, string, PathTools.Hop> H = (n, lost, ip) => { var h = new PathTools.Hop { N = n, Ip = ip, Sent = 100, Lost = lost }; h.Sum = 10 * (100 - lost); return h; };
                var clean = new List<PathTools.Hop> { H(1, 0, "192.168.1.1"), H(2, 40, "203.0.113.1"), H(3, 0, "203.0.113.9"), H(4, 0, "198.51.100.7") };
                if (IspReport.StartOfLoss(clean) != -1) throw new InvalidOperationException("a router ignoring probes was blamed");
                var bad = new List<PathTools.Hop> { H(1, 0, "192.168.1.1"), H(2, 0, "203.0.113.1"), H(3, 8, "203.0.113.9"), H(4, 7, "198.51.100.7") };
                if (IspReport.StartOfLoss(bad) != 2) throw new InvalidOperationException("loss start " + IspReport.StartOfLoss(bad));
                // seen on a real line: the router rate-limits (67%), then silent hops and a game server that never answers ping
                var silent = new List<PathTools.Hop> { H(1, 67, "10.0.0.1"), H(2, 24, "174.116.88.1"), H(3, 0, "69.63.250.1"), H(4, 0, "209.148.235.1"), H(5, 0, "104.160.128.1"), H(6, 100, ""), H(7, 100, "") };
                if (IspReport.StartOfLoss(silent) != -1) throw new InvalidOperationException("silent hops were blamed: hop " + IspReport.StartOfLoss(silent));
                if (!PathTools.MtrVerdict(silent).Contains("doesn't answer pings")) throw new InvalidOperationException("silent destination not said: " + PathTools.MtrVerdict(silent));
                var lastOnly = new List<PathTools.Hop> { H(1, 0, "10.0.0.1"), H(2, 0, "203.0.113.1"), H(3, 30, "203.0.113.9"), H(4, 100, "") };
                if (IspReport.StartOfLoss(lastOnly) != -1) throw new InvalidOperationException("a lone lossy hop before silence was blamed");
                var reached = new List<PathTools.Hop> { H(1, 0, "10.0.0.1"), H(2, 0, "203.0.113.1"), H(3, 12, "198.51.100.7") }; reached[2].Reached = true;
                if (IspReport.StartOfLoss(reached) != -1) throw new InvalidOperationException("loss only at the destination was called real loss");
                if (!PathTools.MtrVerdict(reached).StartsWith("Loss at the destination only", StringComparison.Ordinal)) throw new InvalidOperationException("destination-only loss verdict: " + PathTools.MtrVerdict(reached));
                // everything past hop 2 went dark for the whole run (those hops had answered the trace): an outage, not quiet routers
                var dark = new List<PathTools.Hop> { H(1, 0, "192.168.1.1"), H(2, 0, "203.0.113.1"), H(3, 100, "203.0.113.9"), H(4, 100, "198.51.100.7") }; dark[3].Reached = true;
                if (IspReport.StartOfLoss(dark) != 2) throw new InvalidOperationException("an outage past hop 2 wasn't seen: " + PathTools.MtrVerdict(dark));
                // round-2 review: ONE quiet router at the end (answered the trace, then ignored every ping) doesn't make an earlier
                // hop's rate-limiting "real loss", and the verdict is judged by a hop that answered pings
                var quietTail = new List<PathTools.Hop> { H(1, 0, "192.168.1.1"), H(2, 5, "203.0.113.1"), H(3, 100, "203.0.113.5") };
                if (IspReport.StartOfLoss(quietTail) != -1 || PathTools.MtrVerdict(quietTail).Contains("hop 3")) throw new InvalidOperationException("a quiet tail hop made loss real: " + PathTools.MtrVerdict(quietTail));
                if (!IspReport.Private(IPAddress.Parse("100.64.3.1")) || !IspReport.Private(IPAddress.Parse("fd00::1")) || IspReport.Private(IPAddress.Parse("8.8.8.8"))) throw new InvalidOperationException("private address check");
            });
            T("SMTP path: steps come from the rows - a bad certificate is never 'checked', a banner can't fake a step", () =>
            {
                Func<string, string, string[]> Rw = (lv, n) => new[] { lv, n, "x", "", "m" };
                string ok = EngPack.HowFar(new List<string[]> { Rw("0", "SMTP greeting (40 ms)"), Rw("3", "Offers"), Rw("0", "STARTTLS"), Rw("0", "Certificate") });
                if (!ok.EndsWith("certificate checked", StringComparison.Ordinal)) throw new InvalidOperationException("good path: " + ok);
                string bad = EngPack.HowFar(new List<string[]> { Rw("0", "SMTP greeting (40 ms)"), Rw("3", "Offers"), Rw("0", "STARTTLS"), Rw("2", "mx.example.com:25") });
                if (!bad.EndsWith("certificate FAILED", StringComparison.Ordinal)) throw new InvalidOperationException("invalid certificate: " + bad);
                string none = EngPack.HowFar(new List<string[]> { Rw("2", "mx.example.com:25") });
                if (!none.EndsWith("connection FAILED", StringComparison.Ordinal)) throw new InvalidOperationException("no connection: " + none);
                var snap = EngPack.ParseCert("snap|intranet.example.com:8443|run|202611121000|ok|CN=a~CN=b~ab~20270101");
                if (snap == null || snap.Host != "intranet.example.com:8443") throw new InvalidOperationException("a non-443 snapshot didn't load back");
            });
            T("Microsoft 365 roles: only Microsoft's own names get them", () =>
            {
                if (M365.Role("login.microsoftonline.com", "", 443) != "Sign-in" || M365.Role("eu.trouter.teams.microsoft.com", "", 443) == "") throw new InvalidOperationException("a real name lost its role");
                foreach (var h in new[] { "msauth.evil.example", "notskype.com.attacker.net", "myoffice.com.example.net" }) if (M365.Role(h, "", 443) != "") throw new InvalidOperationException(h + " got a Microsoft role");
            });
            T("code keys: Windows' fast PBKDF2 gives the same bytes as the managed one", () =>
            {
                var pw = Encoding.UTF8.GetBytes("ABCD2345EFGH6789JKMN"); var salt = new byte[16]; for (int i = 0; i < 16; i++) salt[i] = (byte)(i * 7);
                var nat = new byte[64]; var man = FileCrypt.ManagedPbkdf2(pw, salt, 1000, 64);
                if (!FileCrypt.NativePbkdf2(pw, salt, 1000, nat)) { if (Environment.OSVersion.Platform == PlatformID.Win32NT && !Program.IsWine) throw new InvalidOperationException("bcrypt PBKDF2 not available on Windows"); return; }
                if (!nat.SequenceEqual(man)) throw new InvalidOperationException("native and managed PBKDF2 differ");
            });
            T("untrusted files: damaged captures, .adeebo, .adeebokey and file names never crash", () =>
            {
                var rnd = new Random(1234); int cases = 0;
                Action<byte[]> replay = bytes =>
                {
                    try
                    {
                        var pk = Pcap.Parse(bytes); cases++;
                        Replay.DhcpRows(pk, new List<string[]>()); Replay.RaRows(pk, new List<string[]>()); Replay.EapRows(pk, new List<string[]>()); Replay.IcmpRowsFromPackets(pk, new List<string[]>());
                    }
                    catch (Exception e)
                    { if (!(e is IndexOutOfRangeException || e is ArgumentException || e is OverflowException || e is NullReferenceException)) throw; var st = (e.StackTrace ?? "").Split('\n'); throw new InvalidOperationException(e.GetType().Name + " at " + string.Join(" < ", st.Take(3).Select(l => l.Trim()))); }
                };
                foreach (var smp in Replay.Samples.Where(x => x[2] != "cert"))
                {
                    var good = Replay.Build(smp[0]);
                    for (int n = 0; n < 150; n++)
                    {
                        var b = (byte[])good.Clone(); int flips = 1 + rnd.Next(8);
                        for (int f = 0; f < flips; f++) b[rnd.Next(b.Length)] = (byte)rnd.Next(256);
                        if (rnd.Next(4) == 0) Array.Resize(ref b, rnd.Next(b.Length));
                        replay(b);
                    }
                }
                // a pcapng block that claims to be ~2 GB, and random bytes behind each magic number
                var ng = new byte[64]; BitConverter.GetBytes(0x0A0D0D0Au).CopyTo(ng, 0); BitConverter.GetBytes(28u).CopyTo(ng, 4); BitConverter.GetBytes(0x1A2B3C4Du).CopyTo(ng, 8); BitConverter.GetBytes(28u).CopyTo(ng, 24);
                BitConverter.GetBytes(6u).CopyTo(ng, 28); BitConverter.GetBytes(0x7FFFFFFCu).CopyTo(ng, 32); replay(ng);
                foreach (var magic in new[] { 0xa1b2c3d4u, 0xd4c3b2a1u, 0x0A0D0D0Au })
                    for (int n = 0; n < 200; n++) { var b = new byte[24 + rnd.Next(400)]; rnd.NextBytes(b); BitConverter.GetBytes(magic).CopyTo(b, 0); replay(b); }
                // a tampered .adeebo: only "can't open this" (CryptographicException), never a crash or a half-opened file
                byte[] enc; using (var o = new MemoryStream()) { FileCrypt.EncryptCode(new MemoryStream(Encoding.UTF8.GetBytes("hello")), "a.txt", o, "ABCD2345EFGH6789JKMN2345", 100000); enc = o.ToArray(); }
                for (int n = 0; n < 24; n++)
                {
                    var b = (byte[])enc.Clone(); int at = rnd.Next(b.Length); if (at >= 8 && at < 12) at = 12; b[at] ^= (byte)(1 + rnd.Next(255));
                    if (n % 6 == 5) Array.Resize(ref b, rnd.Next(b.Length));
                    var ob = new MemoryStream();
                    try { FileCrypt.DecryptCode(new MemoryStream(b), ob, "ABCD2345EFGH6789JKMN2345"); throw new InvalidOperationException("a changed file opened (byte " + at + ")"); }
                    catch (System.Security.Cryptography.CryptographicException) { if (ob.Length != 0) throw new InvalidOperationException("AM-CG-225: plaintext written before the check (byte " + at + ")"); }
                }
                using (var ow = new MemoryStream()) { try { FileCrypt.DecryptCode(new MemoryStream(enc), ow, "ABCD2345EFGH6789JKMN2346"); throw new InvalidOperationException("wrong code opened"); } catch (System.Security.Cryptography.CryptographicException) { if (ow.Length != 0) throw new InvalidOperationException("AM-CG-225: wrong code wrote plaintext"); } }
                using (var ok = new MemoryStream()) { FileCrypt.DecryptCode(new MemoryStream(enc), ok, "ABCD2345EFGH6789JKMN2345"); if (Encoding.UTF8.GetString(ok.ToArray()) != "hello") throw new InvalidOperationException("code round trip"); }
                // AM-CG-224: shared copies keep origin and path, never the query or fragment
                foreach (var u in new[] { new[] { "see https://a.example/p/q?token=SECRET1&x=1 now", "see https://a.example/p/q?… now" }, new[] { "http://h.test/x#SECRET2", "http://h.test/x#…" }, new[] { "https://h.test/plain", "https://h.test/plain" } })
                    if (FullView.ShareCell(u[0]) != u[1]) throw new InvalidOperationException("ShareCell: " + FullView.ShareCell(u[0]));
                var big = (byte[])enc.Clone(); big[8] = 0x7F; try { FileCrypt.DecryptCode(new MemoryStream(big), new MemoryStream(), "x"); throw new InvalidOperationException("huge iteration count accepted"); } catch (System.Security.Cryptography.CryptographicException) { }
                // mangled public key text
                string nm; foreach (var t in new[] { "", "ADEEBOMETER PUBLIC KEY v1", "ADEEBOMETER PUBLIC KEY v1\nkey: ###", "ADEEBOMETER PUBLIC KEY v1\nkey: QUJD", "ADEEBOMETER PUBLIC KEY v1\nname: x\nkey: " + new string('A', 5000) }) if (EcKeys.ReadPublic(t, out nm) != null) throw new InvalidOperationException("bad key text accepted");
                // a file name chosen by whoever encrypted the file stays a plain name in the chosen folder
                string dir = Path.GetTempPath().TrimEnd('\\');
                foreach (var bad in new[] { "..\\..\\Windows\\evil.exe", "C:\\Windows\\x.dll", "a|b<c>.txt", "CON", "nul.txt", "", "...", new string('x', 500) + ".txt", "/etc/passwd" })
                {
                    string p = FileCrypt.FreeName(dir, bad);
                    if (!string.Equals(Path.GetDirectoryName(p).TrimEnd('\\'), dir, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("escaped the folder: " + bad + " -> " + p);
                    string f = Path.GetFileName(p), stem = f.Split('.')[0]; if (f.Length > 140 || string.Equals(stem, "CON", StringComparison.OrdinalIgnoreCase) || string.Equals(stem, "NUL", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("bad name: " + bad + " -> " + f);
                }
                sb.AppendLine("      " + cases + " damaged or random captures read without a crash");
            });
            T("no company reference anywhere in the product", () =>
            {
                var exe = System.IO.File.ReadAllBytes(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string word = ((char)('m' + 1)).ToString(CultureInfo.InvariantCulture) + "ok" + "ia";   // built at run time so this test doesn't find itself
                /* searched in the raw bytes, as ASCII and as UTF-16: turning the 3 MB exe into text made ten copies of up to 12 MB,
                   and Windows' .NET kept that memory - the "memory after everything" check then failed at 104-115 MB */
                foreach (bool wide in new[] { false, true }) if (InBytes(exe, word, wide, true)) throw new InvalidOperationException("found in the exe (" + (wide ? "utf-16" : "ascii") + ")");
                foreach (var pack in new[] { "terms", "learn", "processes", "examples" }.Concat(L.Languages.Skip(1).Select(l => "lang-" + l[0]))) if (Knowledge.RawText(pack).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) throw new InvalidOperationException("found in " + pack);
                // its products too (the operating system and router families), built at run time for the same reason
                Func<string, string, string> j = (x, y) => x + y;   // joined at run time - "a" + "b" would be folded into one literal
                foreach (var w in new[] { j("S", "R OS"), j("S", "R-OS"), j("Ti", "MOS"), j("Alca", "tel") })
                {
                    foreach (bool wide in new[] { false, true }) if (InBytes(exe, w, wide, false)) throw new InvalidOperationException("product name in the exe: " + w);
                    foreach (var pack in new[] { "terms", "learn", "processes", "examples" }.Concat(L.Languages.Skip(1).Select(l => "lang-" + l[0]))) if (Knowledge.RawText(pack).IndexOf(w, StringComparison.Ordinal) >= 0) throw new InvalidOperationException("product name in " + pack + ": " + w);
                }
            });
            T("review fixes: URLs as targets, one device only for Traffic Lab, iperf3 UDP block size", () =>
            {
                var bad = new List<string>();
                foreach (var c in new[] { new[] { "https://example.com/x?y", "example.com" }, new[] { "example.com:8443", "example.com" }, new[] { "http://192.0.2.5:8080/", "192.0.2.5" }, new[] { "[2001:db8::1]:22", "2001:db8::1" }, new[] { "2001:db8::1", "2001:db8::1" }, new[] { "10.0.0.1", "10.0.0.1" }, new[] { "example.com/path", "example.com" } })
                    if (Toolkit.HostOnly(c[0]) != c[1]) bad.Add("HostOnly(" + c[0] + ") = " + Toolkit.HostOnly(c[0]));
                if (Toolkit.UrlToTarget("https://example.com/x", true) != "example.com:443" || Toolkit.UrlToTarget("http://example.com", true) != "example.com:80" || Toolkit.UrlToTarget("https://example.com:8443", false) != "example.com") bad.Add("UrlToTarget");
                var r4 = Icmp.Resolve4("https://127.0.0.1/status"); if (r4 == null || r4.ToString() != "127.0.0.1") bad.Add("Resolve4 of a URL gave " + r4);
                foreach (var no in new[] { "255.255.255.255", "224.0.0.1", "239.255.255.250", "0.0.0.0", "ff02::1", "::" }) if (Lab.NotOne(IPAddress.Parse(no)) == "") bad.Add("Traffic Lab would send to " + no);
                if (Lab.NotOne(IPAddress.Parse("192.0.2.10")) != "") bad.Add("one device refused");
                var o = Toolkit.ParseOpts("-u -l 4"); if (Toolkit.BlockLen(o) < 16) bad.Add("UDP block " + Toolkit.BlockLen(o));
                if (Toolkit.BlockLen(Toolkit.ParseOpts("-l 4")) != 4 || Toolkit.BlockLen(Toolkit.ParseOpts("-u")) != 1400) bad.Add("block sizes");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("core review fixes: Wi-Fi in any language, one key per app across updates, no negative ages", () =>
            {
                var bad = new List<string>(); int sig, rate; string ssid, ch, bssid, st;
                Monitor.ParseWifi("    State                  : connected\r\n    SSID                   : Home\r\n    BSSID                  : aa:bb:cc:00:00:01\r\n    Channel                : 36\r\n    Receive rate (Mbps)    : 866.7\r\n    Signal                 : 81%\r\n", out sig, out ssid, out ch, out bssid, out st, out rate);
                if (sig != 81 || ssid != "Home" || ch != "36" || st != "connected" || rate != 866) bad.Add("English: " + sig + "/" + ssid + "/" + ch + "/" + st + "/" + rate);
                Monitor.ParseWifi("    Status                 : Verbunden\r\n    SSID                   : Zuhause\r\n    Kanal                  : 6\r\n    Signal                 : 64 %\r\n", out sig, out ssid, out ch, out bssid, out st, out rate);
                if (sig != 64 || ssid != "Zuhause" || ch != "6" || st != "connected") bad.Add("German: " + sig + "/" + ssid + "/" + ch + "/" + st);
                Monitor.ParseWifi("    Estado                 : desconectado\r\n    Señal                  : 0%\r\n", out sig, out ssid, out ch, out bssid, out st, out rate);
                if (st != "disconnected") bad.Add("Spanish state: " + st);
                if (!LinkInfo.Blocked("Network shell commands need location permission to access WLAN information.")) bad.Add("location block not seen");
                if (Watch.Unversioned(@"C:\Users\a\AppData\Local\Discord\app-1.0.9013\Discord.exe") != Watch.Unversioned(@"C:\Users\a\AppData\Local\Discord\app-1.0.9014\Discord.exe")) bad.Add("versioned folders");
                if (Timeline.Ago(DateTime.Now.AddMinutes(30)) != "0 s") bad.Add("negative age: " + Timeline.Ago(DateTime.Now.AddMinutes(30)));
                if (!Program.CanWrite(Path.GetTempPath())) bad.Add("temp not writable?");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("About links: only AdeeboMeter's pages and its PayPal donation link open", () =>
            {
                foreach (var ok in new[] { "https://github.com/adeeb111/AdeeboMeter/discussions", "https://paypal.me/adeeb1111", "https://paypal.me/adeeb1111/5CAD", "https://adeeb111.github.io/AdeeboMeter/#update" }) if (!Crash.Allowed(ok)) throw new InvalidOperationException("refused " + ok);
                foreach (var no in new[] { "https://paypal.me/adeeb11112", "https://paypal.me/someone", "http://paypal.me/adeeb1111", "https://github.com/other/AdeeboMeter", "https://evil.example/https://paypal.me/adeeb1111", "", "https://github.com/adeeb111/AdeeboMeterEvil", "https://github.com/adeeb111/AdeeboMeter@evil.example", "https://x@github.com/adeeb111/AdeeboMeter", "https://adeeb111.github.io/AdeeboMeterX/" }) if (Crash.Allowed(no)) throw new InvalidOperationException("allowed " + no);
            });
            T("Adeebo Points: +2 / +4 once, replay +3 once, survives a restart, off keeps the total, 500 is the Adeebo Award", () =>
            {
                var bad = new List<string>();
                if (!Cfg.Known.Contains("points")) bad.Add("setting not known");
                foreach (var line in Cfg.Lines()) { int eq = line.IndexOf('='); if (eq > 0 && !Cfg.Known.Contains(line.Substring(0, eq))) bad.Add("saved setting " + line.Substring(0, eq) + " is dropped by Import settings (add it to Cfg.Known)"); }
                foreach (var id in FullView.LabGateIds) if (Points.Value(id) != 0) bad.Add(id + " earns behind the 'my own network' tick");
                foreach (var id in new[] { "speedtest", "iperf", "bloat" }) if (Points.Replay.Contains(id)) bad.Add(id + " has a replay bonus (moves a lot of data)");
                string f = Path.Combine(Path.GetTempPath(), "am-points-" + Guid.NewGuid().ToString("N") + ".txt");
                try
                {
                    var p = new Points(f); var t0 = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
                    if (p.Total != 0) bad.Add("fresh total " + p.Total);
                    if (p.Award("ping", false, true, t0, t0) != 0) bad.Add("earned without opening the Example");
                    if (p.Award("ping", true, false, t0, t0) != 0) bad.Add("earned with earning off");
                    if (p.Award("ping", true, true, t0, t0) != 2) bad.Add("first run not +2");
                    if (p.Award("ping", true, true, t0.AddMinutes(1), t0.AddMinutes(1)) != 0 || p.Award("ping", true, true, t0.AddHours(3), t0.AddHours(3)) != 0) bad.Add("regular example earned twice");
                    if (p.Award("ispreport", true, true, t0, t0) != 4) bad.Add("a longer test (ISP report) first not +4");
                    if (p.Award("ispreport", true, true, t0.AddMinutes(5), t0.AddMinutes(5)) != 0) bad.Add("replay bonus inside 10 minutes");
                    if (p.Award("ispreport", true, true, t0.AddMinutes(11), t0.AddMinutes(11)) != 3) bad.Add("replay bonus not +3");
                    if (p.Award("ispreport", true, true, t0.AddMinutes(40), t0.AddMinutes(40)) != 0) bad.Add("replay bonus twice");
                    if (p.Award("udpsend", true, true, t0, t0) != 0) bad.Add("a Traffic Lab sender earned");
                    if (p.Award("mtr", true, true, t0, t0) != 4 || p.Award("mtr", true, true, t0.AddMinutes(11), t0.AddMinutes(1)) != 0) bad.Add("moving the Windows clock paid the replay bonus");
                    if (p.Total != 13) bad.Add("total " + p.Total + " (want 13)");
                    var again = new Points(f); if (again.Total != 13 || again.Done != 3) bad.Add("after restart " + again.Total + "/" + again.Done);
                    if (again.Award("mtr", true, true, t0.AddMinutes(12), t0.AddMinutes(2)) != 0) bad.Add("a restart plus a clock moved forward paid the replay bonus");
                    if (again.Award("mtr", true, true, t0.AddMinutes(13), t0.AddMinutes(13)) != 3) bad.Add("the replay bonus after a real 10 minutes (across a restart)");
                    // round-3 #10: the uptime rule holds only within one Windows boot - a reboot the next day pays at 5 min of uptime
                    {
                        string fb = f + ".boot"; try { File.Delete(fb); } catch (IOException) { }
                        try
                        {
                            var pb = new Points(fb); pb.Award("gamecompare", true, true, t0, t0, 5L << 32 | 120000);
                            if (pb.Award("gamecompare", true, true, t0.AddMinutes(12), t0.AddMinutes(12), 5L << 32 | 300000) != 0) bad.Add("same boot, 3 min of uptime later: paid");
                            if (pb.Award("gamecompare", true, true, t0.AddDays(1), t0.AddDays(1), 6L << 32 | 300000) != 3) bad.Add("a new boot the next day: the replay bonus was refused");
                        }
                        finally { try { File.Delete(fb); } catch (IOException) { } }
                    }
                    if (again.Award("ispreport", true, true, t0.AddDays(1), t0.AddDays(1)) != 0 || again.Award("ping", true, true, t0.AddDays(1), t0.AddDays(1)) != 0) bad.Add("restart let it earn again");
                    if (again.Award("dig", true, false, t0, t0) != 0 || again.Total != 16) bad.Add("earning off changed the total");
                    if (again.Award("learn.dhcp", true, true, t0, t0) != 2 || again.Award("page.replay", true, true, t0, t0) != 2 || again.Award("check.dhcp", true, true, t0, t0) != 2 || again.Award("lab.tcp", true, true, t0, t0) != 4 || again.Award("quiz.5", true, true, t0, t0) != 3 || again.Award("quiz.10", true, true, t0, t0) != 5 || again.Award("journey.explain", true, true, t0, t0) != 3 || again.Award("path.gamer", true, true, t0, t0) != 10 || again.Award("lab.tcp", true, true, t0.AddHours(2), t0.AddHours(2)) != 0 || again.Award("learn.dhcp", true, true, t0.AddHours(1), t0.AddHours(1)) != 0) bad.Add("Learn / page points");
                    if (!again.Replayed("ispreport") || again.Replayed("ping")) bad.Add("replay state");
                    // round-2 review: lines typed into points.txt for things that don't exist count nothing; a replay mark on a
                    // tool without replay counts nothing; the total never passes the maximum; a wrong first answer never pays
                    string farm = f + ".farm"; var fl = new List<string>(); for (int i = 0; i < 250; i++) fl.Add("zz" + i + "|" + t0.Ticks); for (int i = 0; i < 50; i++) fl.Add("path.fake" + i + "|" + t0.Ticks); fl.Add("ping|" + t0.Ticks + "|" + t0.AddMinutes(30).Ticks + "|2");
                    File.WriteAllLines(farm, fl);
                    try { var pf = new Points(farm); if (pf.Total != 2) bad.Add("farmed points.txt counted " + pf.Total + " (want 2)"); pf.MarkTried("check.dns"); var pf2 = new Points(farm); if (!pf2.Tried("check.dns") || pf2.Total != 2) bad.Add("tried marker"); }
                    finally { try { File.Delete(farm); } catch (IOException) { } }
                    int left; if (Points.Rank(29) != "Beginner" || Points.Rank(30) != "Learner" || Points.Rank(99) != "Learner" || Points.Rank(100) != "Troubleshooter" || Points.Rank(200) != "Expert" || Points.Rank(300) != "Network detective" || Points.Rank(499) != "Packet whisperer" || Points.Rank(Points.AwardAt) != "Adeebo Award" || Points.Next(32, out left) != "Troubleshooter" || left != 68 || Points.Next(600, out left) != "") bad.Add("ranks");
                    // a points file from 1.1.4 (+1 per tool, +2 replay) counts at today's values
                    string old = f + ".old"; File.WriteAllLines(old, new[] { "ping|" + t0.Ticks + "|-|1", "mtr|" + t0.Ticks + "|" + t0.AddMinutes(20).Ticks + "|2", "learn.dns|" + t0.Ticks + "|-|1" });
                    try { var po = new Points(old); if (po.Total != 2 + 4 + 3 + 2) bad.Add("1.1.4 file total " + po.Total); } finally { try { File.Delete(old); } catch (IOException) { } }
                    var ex = Examples.Rows(Examples.Get("ping"), true);
                    if (Points.Completed(ex, false)) bad.Add("an example counted as a run");
                    if (Points.Completed(new List<string[]> { new[] { "0", "Replies", "50 of 50", "", "m" } }, true)) bad.Add("a stopped run counted");
                    if (Points.Completed(new List<string[]> { new[] { "2", "Target", "type a host", "", "" } }, false)) bad.Add("an input prompt counted");
                    if (!Points.Completed(new List<string[]> { new[] { "2", "Closed", "port 25 blocked", "", "m" } }, false)) bad.Add("a real (bad) result didn't count");
                }
                finally { try { File.Delete(f); } catch (IOException) { } }
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("Adeebo Points 1.1.5: a check question per Learn topic, a challenge per lab, learning paths that exist", () =>
            {
                var bad = new List<string>();
                foreach (var t in Knowledge.Learn())
                {
                    var p = t["check"].Split('|').Select(x => x.Trim()).ToArray();
                    if (p.Length != 5 || p.Any(x => x == "") || p.Skip(1).Distinct().Count() != 4) bad.Add(t.Id + ": check question needs a question, the answer and 3 wrong ones");
                }
                foreach (var k in FullView.PathKeys) if (!FullView.PtKeyKnown(k)) bad.Add("path step " + k + " doesn't exist");
                if (Points.Value("lab.tcp") != 4 || Points.Value("check.dns") != 2 || Points.Value("path.home") != 10 || Points.Value("udpsend") != 0) bad.Add("values");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad.Take(8)));
            });
            T("Adeebo Award card: claim code matches the Hall of Fame check, names cleaned, picture drawn", () =>
            {
                var bad = new List<string>();
                // the same vectors as .github/scripts/hof_verify.py --code (HOF_KEY=test-key-1)
                if (HallOfFame.Code("test-key-1", "Ada  Lovelace", 512, "2026-11-12") != "ADB-I5IU-AQRM-QTIL-TP2C") bad.Add("code 1 " + HallOfFame.Code("test-key-1", "Ada  Lovelace", 512, "2026-11-12"));
                if (HallOfFame.Code("test-key-1", "\u00C1d\u00E1m O'Neil-\u00E9", 500, "2026-12-01") != "ADB-WOQK-DNUY-56M2-5DB4") bad.Add("code 2 (accents)");
                if (HallOfFame.Code("", "Ada", 512, "2026-11-12") != "") bad.Add("a code without a key");
                // names outside the Basic Multilingual Plane, Indic vowel signs and decomposed accents: the same code as Python's check
                if (HallOfFame.Code("test-key-1", "\U00020BB7\u7530 \u0926\u0947\u0935\u0928\u093E\u0917\u0930\u0940", 512, "2026-11-10") != "ADB-MDL2-JV5N-5LJA-LJPT") bad.Add("astral / Devanagari name code");
                if (HallOfFame.Code("test-key-1", "Jose\u0301", 512, "2026-11-10") != "ADB-3YBG-N6OJ-JA72-XFEL") bad.Add("decomposed accent code");
                if (HallOfFame.Code("test-key-1", "Ada", 513, "2026-11-12") == HallOfFame.Code("test-key-1", "Ada", 512, "2026-11-12")) bad.Add("points not in the code");
                if (HallOfFame.CleanName("  <script>alert(1)</script>  Bob ") != "scriptalert1script Bob") bad.Add("clean: " + HallOfFame.CleanName("  <script>alert(1)</script>  Bob "));
                if (HallOfFame.CleanName(new string('a', 50)).Length != 32) bad.Add("name length");
                if (HallOfFame.CleanName("Z" + new string('\u0301', 31) + " Bob") != "\u0179\u0301\u0301 Bob") bad.Add("accent marks not capped: " + HallOfFame.CleanName("Z" + new string('\u0301', 31)).Length);
                if (!HallOfFame.MixedScript("\u0410deeb") || HallOfFame.MixedScript("\u0410\u0434\u0438\u0431 Adeeb") || HallOfFame.MixedScript("Jos\u00e9")) bad.Add("look-alike names");
                if (HallOfFame.Unmask(new byte[] { (byte)('k' ^ 41), (byte)('y' ^ 114) }) != "ky") bad.Add("unmask");
                var st = new HallOfFame.Stats { Tools = 120, ToolsAll = 131, Topics = 46, TopicsAll = 46, Checks = 44, Labs = 11, LabsAll = 11, Paths = new[] { "Home user", "Gamer" } };
                using (var b = HallOfFame.Card("Ada Lovelace", 512, "2026-11-12", "ADB-I5IU-AQRM-QTIL-TP2C", st)) if (b.Width != 1200 || b.Height != 675) bad.Add("card size");
                using (var b = HallOfFame.Card("", 500, "2026-11-12", "", null)) if (b.Width != 1200) bad.Add("unofficial card");
                if (HallOfFame.Form != "app:hof" || !Crash.Allowed(HallOfFame.Page)) bad.Add("Hall of Fame: sent from the app, page allowed to open");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("What did Windows Update break: before/after per install, KB names", () =>
            {
                var bad = new List<string>();
                if (UpdateBreak.Short("2026-11 Cumulative Update for Windows 11 Version 24H2 for x64-based Systems (KB5046617)") != "KB5046617 (Cumulative Update)") bad.Add("KB name: " + UpdateBreak.Short("2026-11 Cumulative Update for Windows 11 Version 24H2 for x64-based Systems (KB5046617)"));
                if (UpdateBreak.Short("Security Intelligence Update for Microsoft Defender Antivirus - KB2267602 (Version 1.421.1)") != "KB2267602 (Defender definitions)") bad.Add("Defender name");
                var day0 = DateTime.Today.AddDays(-6); var c = new UpdateBreak.Change { T = day0.AddHours(3), What = "KB5046617", Kind = "update" };
                // worse from the day after, and still worse today
                Func<DateTime, UpdateBreak.DayStat> worse = d => new UpdateBreak.DayStat { D = d, Ping = d > day0 ? 62 : 21, Drops = d > day0 ? 3 : 0, Minutes = 1440 };
                var f = UpdateBreak.Compare(c, worse); if (!f.Strong || !f.Stayed || f.Lv != 2) bad.Add("clear change: " + f.Text);
                // the same before and after
                Func<DateTime, UpdateBreak.DayStat> same = d => new UpdateBreak.DayStat { D = d, Ping = 21 + (d.Day % 3), Drops = 0, Minutes = 1440 };
                f = UpdateBreak.Compare(c, same); if (f.Lv != 0 || f.Strong) bad.Add("no change: " + f.Text);
                // a one-day blip that has gone again
                Func<DateTime, UpdateBreak.DayStat> blip = d => new UpdateBreak.DayStat { D = d, Ping = d == day0.AddDays(1) || d == day0.AddDays(2) ? 70 : 21, Drops = 0, Minutes = 1440 };
                f = UpdateBreak.Compare(c, blip); if (f.Stayed) bad.Add("a blip that settled said 'still like that'");
                // no History before it
                Func<DateTime, UpdateBreak.DayStat> none = d => new UpdateBreak.DayStat { D = d, Ping = d > day0 ? 30 : double.NaN };
                f = UpdateBreak.Compare(c, none); if (f.Lv != 3 || f.Strong) bad.Add("no History judged anyway");
                // round-2 review: it got worse on day D. An install the day AFTER (or two days before) must not be blamed;
                // the one the day before is
                var dD = DateTime.Today.AddDays(-5);
                Func<DateTime, UpdateBreak.DayStat> stepAtD = d => new UpdateBreak.DayStat { D = d, Ping = d >= dD ? 60 : 20, Drops = 0, Minutes = 1440 };
                if (UpdateBreak.Compare(new UpdateBreak.Change { T = dD.AddDays(1).AddHours(9), What = "after" }, stepAtD).Strong) bad.Add("blamed an install made after it got worse");
                if (UpdateBreak.Compare(new UpdateBreak.Change { T = dD.AddDays(-2).AddHours(9), What = "two before" }, stepAtD).Strong) bad.Add("blamed an install two days before it got worse");
                if (!UpdateBreak.Compare(new UpdateBreak.Change { T = dD.AddDays(-1).AddHours(9), What = "the day before" }, stepAtD).Strong) bad.Add("missed the install the day before it got worse");
                if (!UpdateBreak.Defender("Security Intelligence Update for Microsoft Defender Antivirus - KB2267602 (Version 1.421.1)") || UpdateBreak.Defender("2026-11 Cumulative Update (KB5046617)")) bad.Add("Defender definitions not set apart");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("round-2 core fixes: mapped addresses, outage ends, ReplaceFile, --report verdict", () =>
            {
                var bad = new List<string>();
                if (Lab.NotOne(IPAddress.Parse("::ffff:255.255.255.255")) == "" || Lab.NotOne(IPAddress.Parse("::ffff:224.0.0.251")) == "") bad.Add("IPv4-mapped broadcast/multicast passed as one device");
                if (Watch.IsPublic("::ffff:192.168.1.1") || Watch.IsPublic("::ffff:127.0.0.1") || !Watch.IsPublic("::ffff:8.8.8.8")) bad.Add("IPv4-mapped private address counted as public");
                if (SpeedTest.SafeUrl("http://[::ffff:127.0.0.1]/x") || SpeedTest.SafeUrl("http://[::ffff:192.168.1.1]/cgi-bin/x")) bad.Add("speed server URL into this PC / the LAN");
                var t0 = new DateTime(2026, 11, 12, 18, 0, 0);
                var evs = new List<Timeline.Ev> { new Timeline.Ev { T = t0, Lv = 2, Kind = "inet", Text = "quiet" }, new Timeline.Ev { T = t0.AddMinutes(9), Lv = 1, Kind = "inet", Text = "network changed" } };
                var op = Outages.Pair(evs, t0.AddHours(1), false); if (op.Count != 1 || !op[0].EndUnseen) bad.Add("a network change closed an outage with a made-up end");
                // round-3 #7: digits from other scripts never reach int.Parse - they're refused, not a crash
                try { Calc.Vlsm("\u0661\u0660.0.0.0/24", "10"); Calc.Number("\u0661.2.3.4"); string h0; int p0; bool t0x; string e0; DestCheck.Parse("01.1.1.\u0661", out h0, out p0, out t0x, out e0); }
                catch (FormatException) { bad.Add("Arabic-Indic digits threw"); }
                // --report: a step that couldn't run turns "ok" into "couldn't tell"; an informational row doesn't
                var gr = new GuideRun { Lv = 0 }; gr.Obs("m", 3, "Around then", "On LAN"); if (Report.Verdict(gr) != 0) bad.Add("--report: an info row downgraded a healthy run");
                gr.Obs("u", 3, "6. The way in", "Windows' connectivity test couldn't run (NameResolutionFailure)"); if (Report.Verdict(gr) != 3) bad.Add("--report said ok with a step that couldn't run");
                string f = Path.Combine(Path.GetTempPath(), "am-replace-" + Guid.NewGuid().ToString("N"));
                try
                {
                    File.WriteAllText(f, "old"); File.WriteAllText(f + ".tmp", "new"); Program.ReplaceFile(f + ".tmp", f);
                    if (File.ReadAllText(f) != "new" || File.Exists(f + ".tmp") || File.Exists(f + ".bak")) bad.Add("ReplaceFile");
                }
                finally { foreach (var x in new[] { f, f + ".tmp", f + ".bak" }) try { File.Delete(x); } catch (IOException) { } }
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("round-2 batch: SNMP text, Wi-Fi parsing, mail records, URL slips, iperf block size", () =>
            {
                var bad = new List<string>();
                Func<byte[], string> txt = b => Snmp.Text(new Snmp.Vb { Type = 4, Raw = b }, "sysName", null);
                if (txt(Encoding.UTF8.GetBytes("café")) != "café" || txt(Encoding.UTF8.GetBytes("Bürox")) != "Bürox") bad.Add("SNMP UTF-8 text: " + txt(Encoding.UTF8.GetBytes("café")) + " / " + txt(Encoding.UTF8.GetBytes("Bürox")));
                if (txt(new byte[] { 0x00, 0x11, 0x22, 0xaa, 0xbb, 0xcc }) != "00:11:22:aa:bb:cc") bad.Add("SNMP MAC");
                int sig, rate; string ssid, ch, bssid, state;
                Monitor.ParseWifi("    Sinyal                 : %74\n    SSID                   : Ev\n", out sig, out ssid, out ch, out bssid, out state, out rate); if (sig != 74) bad.Add("Turkish signal " + sig);
                Monitor.ParseWifi("    BSSID du point d'accès : 02:11:22:33:44:55\n    Signal : 80 %\n", out sig, out ssid, out ch, out bssid, out state, out rate); if (bssid != "02:11:22:33:44:55") bad.Add("French BSSID label");
                if (Toolkit.HostOnly("https:/example.com/x") != "example.com" || Toolkit.UrlToTarget("https:/example.com", false) != "example.com") bad.Add("one-slash URL: " + Toolkit.HostOnly("https:/example.com/x"));
                if (Toolkit.BlockLen(Toolkit.ParseOpts("-u -l 1M")) > 65507) bad.Add("iperf3 UDP block over 65,507");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("Neighbour wars: winner per day, who moved onto your channel, the quietest channel", () =>
            {
                var bad = new List<string>(); var rows = new List<AirLog.Row>(); var d0 = DateTime.Today.AddDays(-2);
                for (int day = 0; day < 3; day++) for (int h = 0; h < 4; h++)
                {
                    var t = d0.AddDays(day).AddHours(9 + h);
                    rows.Add(new AirLog.Row { T = t, Bssid = "02:00:00:00:00:01", Ssid = "Home", Band = "2.4 GHz", Channel = 6, Width = 20, Rssi = -50, Sec = "WPA2", Mine = true });
                    rows.Add(new AirLog.Row { T = t, Bssid = "02:00:00:00:00:02", Ssid = "Neighbour", Band = "2.4 GHz", Channel = day == 0 ? 1 : 6, Width = 20, Rssi = -45, Sec = "WPA2" });
                    rows.Add(new AirLog.Row { T = t, Bssid = "02:00:00:00:00:03", Ssid = "Far", Band = "2.4 GHz", Channel = 11, Width = 20, Rssi = -80, Sec = "WPA3" });
                }
                var w = AirLog.Analyse(rows);
                if (w.MyChannel != 6 || w.DaysSeen.Count != 3) bad.Add("my channel " + w.MyChannel + " days " + w.DaysSeen.Count);
                string win0, win2; w.Winner.TryGetValue(d0, out win0); w.Winner.TryGetValue(d0.AddDays(2), out win2);
                if (win0 != "you" || win2 != "Neighbour") bad.Add("winners " + win0 + " / " + win2);
                var nb = w.Nets.FirstOrDefault(n => n.Name == "Neighbour"); if (nb == null || nb.Moved == "") bad.Add("the move onto channel 6 not seen");
                if (w.Nets.Any(n => n.Name == "Far" && n.Moved != "")) bad.Add("Far moved?");
                if (w.Quiet != 1) bad.Add("quietest " + w.Quiet);
                if (w.Verdict.IndexOf("Neighbour moved onto it", StringComparison.Ordinal) < 0 || w.Verdict.IndexOf("1 of 3", StringComparison.Ordinal) < 0) bad.Add("verdict: " + w.Verdict);
                if (AirLog.Analyse(new List<AirLog.Row>()).Nets.Count != 0) bad.Add("empty log");
                // round-2 review: 80 MHz blocks overlap on 5 GHz; the verdict uses the channel you're on NOW; another access
                // point with your name (mesh) is never a neighbour
                if (!AirLog.Overlap(44, 80, 36, 80, "5 GHz") || AirLog.Overlap(44, 80, 52, 80, "5 GHz") || !AirLog.Overlap(40, 20, 36, 40, "5 GHz") || AirLog.Overlap(40, 20, 44, 40, "5 GHz") || !AirLog.Overlap(149, 80, 161, 20, "5 GHz")) bad.Add("5 GHz width overlap");
                var r5 = new List<AirLog.Row>();
                for (int day = 0; day < 3; day++) for (int h = 0; h < 3; h++)
                {
                    var t = d0.AddDays(day).AddHours(9 + h);
                    r5.Add(new AirLog.Row { T = t, Bssid = "02:00:00:00:01:01", Ssid = "Home", Band = "5 GHz", Channel = day < 2 ? 149 : 44, Width = 80, Rssi = -60, Sec = "WPA2", Mine = true });
                    r5.Add(new AirLog.Row { T = t, Bssid = "02:00:00:00:01:02", Ssid = "Home", Band = "5 GHz", Channel = 100, Width = 80, Rssi = -40, Sec = "WPA2", Same = true });
                    r5.Add(new AirLog.Row { T = t, Bssid = "02:00:00:00:01:03", Ssid = "Next door", Band = "5 GHz", Channel = 36, Width = 80, Rssi = -45, Sec = "WPA2" });
                }
                var w5 = AirLog.Analyse(r5);
                if (w5.MyChannel != 44 || !w5.MovedLately) bad.Add("5 GHz: my channel now " + w5.MyChannel);
                if (w5.Verdict.IndexOf("nobody else overlaps", StringComparison.Ordinal) >= 0 || w5.Verdict.IndexOf("1 neighbour now overlaps", StringComparison.Ordinal) < 0) bad.Add("80 MHz neighbour missed: " + w5.Verdict);
                string wd0; w5.Winner.TryGetValue(d0, out wd0); if (wd0 != "you") bad.Add("day 1 (on 149, alone) winner " + wd0);
                if (w5.Quiet == 36 || w5.Quiet == 40 || w5.Quiet == 44 || w5.Quiet == 48) bad.Add("quietest channel inside the busy 80 MHz block: " + w5.Quiet);
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("packet journey: filter, records, translation and Explain rules", () =>
            {
                const string me = "192.0.2.10", srv = "203.0.113.40", rtr = "198.51.100.1", m1 = "02:00:00:00:00:01", m2 = "02:00:00:00:00:02";
                var f = new Journey.Flow { Remote = srv, RemotePort = 443, LocalPort = 50123, Udp = false };
                if (Journey.Bpf(f) != "(host 203.0.113.40 and port 443 and port 50123) or (icmp and host 203.0.113.40)") throw new InvalidOperationException("journey filter: " + Journey.Bpf(f));
                // TCP: handshake 30 ms, data, one resend, a router's "too big"
                var w = new Pcap.Writer(); var body = new byte[1200];
                w.Add(0, Pcap.Eth(m2, m1, 0x0800, Pcap.Ip4(me, srv, 6, 64, Pcap.Tcp(50123, 443, 1000, 0, 0x02, 64240, Array.Empty<byte>()))));
                w.Add(0.030, Pcap.Eth(m1, m2, 0x0800, Pcap.Ip4(srv, me, 6, 52, Pcap.Tcp(443, 50123, 5000, 1001, 0x12, 65535, Array.Empty<byte>()))));
                w.Add(0.001, Pcap.Eth(m2, m1, 0x0800, Pcap.Ip4(me, srv, 6, 64, Pcap.Tcp(50123, 443, 1001, 5001, 0x10, 64240, Array.Empty<byte>()))));
                w.Add(0.001, Pcap.Eth(m2, m1, 0x0800, Pcap.Ip4(me, srv, 6, 64, Pcap.Tcp(50123, 443, 1001, 5001, 0x18, 64240, new byte[300]))));
                for (int i = 0; i < 20; i++) w.Add(0.002, Pcap.Eth(m1, m2, 0x0800, Pcap.Ip4(srv, me, 6, 52, Pcap.Tcp(443, 50123, (uint)(5001 + i * 1200), 1301, 0x10, 65535, body))));
                w.Add(0.2, Pcap.Eth(m1, m2, 0x0800, Pcap.Ip4(srv, me, 6, 52, Pcap.Tcp(443, 50123, 5001 + 5 * 1200, 1301, 0x10, 65535, body))));   // sent again
                var quote = Pcap.Ip4(me, srv, 6, 64, Pcap.Tcp(50123, 443, 1301, 0, 0x10, 1, Array.Empty<byte>()));
                w.Add(0.01, Pcap.Eth(m1, m2, 0x0800, Pcap.Ip4(rtr, me, 1, 250, Pcap.Icmp4(3, 4, new byte[] { 0, 0, 0x05, 0x78 }.Concat(quote).ToArray()))));
                var pk = Pcap.Parse(w.Bytes(), k => Journey.Belongs(k, f)); if (pk.Count != 26) throw new InvalidOperationException("journey kept " + pk.Count + " of 26 packets");
                var recs = Journey.Records(pk, f, pk[0].Abs); string kind = Journey.Kind(f, recs); Journey.Translate(recs, kind);
                if (kind != "web" || recs.Count(r => r.Resend) != 1 || recs[0].Say != "This PC asks to connect (SYN)" || !recs.Any(r => r.Say.IndexOf("AGAIN", StringComparison.Ordinal) >= 0) || recs.Last().IcmpMtu != 1400) throw new InvalidOperationException("journey records wrong: " + kind + " / " + string.Join(" | ", recs.Take(3).Select(r => r.Say)));
                var ex = Journey.Explain(f, recs, kind, false);
                if (ex[0].Lv != 2 || !ex.Any(n => n.Title.StartsWith("Packets were lost", StringComparison.Ordinal)) || !ex.Any(n => n.Title.StartsWith("A packet was too large", StringComparison.Ordinal)) || ex.Last().Title != "Can't tell from here" || !ex.Any(n => n.Text.IndexOf("30 ms", StringComparison.Ordinal) >= 0))
                    throw new InvalidOperationException("journey explain (TCP) wrong: " + string.Join(" | ", ex.Select(n => n.Lv + " " + n.Title)));
                // UDP game: steady 8 ms both ways, three 120 ms stalls each followed by a burst; the far end found from the packets
                var g = new Journey.Flow { Remote = "", LocalPort = 61000, Udp = true }; var w2 = new Pcap.Writer(); var upd = new byte[90];
                var ev = new List<KeyValuePair<double, bool>>();   // time, outgoing
                for (int i = 0; i < 300; i++) ev.Add(new KeyValuePair<double, bool>(i * 0.008, true));
                double tin = 0.004;
                for (int i = 0; i < 300; i++)
                {
                    if (i == 100 || i == 180 || i == 250) { tin += 0.120; for (int k = 0; k < 5; k++) { ev.Add(new KeyValuePair<double, bool>(tin, false)); tin += 0.0005; } }
                    ev.Add(new KeyValuePair<double, bool>(tin, false)); tin += 0.008;
                }
                double prev = 0;
                foreach (var e in ev.OrderBy(z => z.Key)) { w2.Add(e.Key - prev, e.Value ? Pcap.Eth(m2, m1, 0x0800, Pcap.Ip4(me, srv, 17, 64, Pcap.Udp(61000, 7086, upd))) : Pcap.Eth(m1, m2, 0x0800, Pcap.Ip4(srv, me, 17, 52, Pcap.Udp(7086, 61000, upd)))); prev = e.Key; }
                if (Journey.Bpf(g) != "(udp port 61000) or icmp or icmp6") throw new InvalidOperationException("journey UDP filter: " + Journey.Bpf(g));
                var pk2 = Pcap.Parse(w2.Bytes(), k => Journey.Belongs(k, g)); Journey.FindRemote(g, pk2, new HashSet<string> { me });
                if (g.Remote != srv || g.RemotePort != 7086) throw new InvalidOperationException("journey didn't find the far end: " + g.Remote + ":" + g.RemotePort);
                var r2 = Journey.Records(pk2.Where(k => Journey.Belongs(k, g)).ToList(), g, pk2[0].Abs); string k2 = Journey.Kind(g, r2); var ex2 = Journey.Explain(g, r2, k2, true);
                if (k2 != "realtime" || r2.Count(r => r.GapBefore > 0 && !r.Out) != 3 || !ex2.Any(n => n.Title.StartsWith("Delayed and bunched", StringComparison.Ordinal)) || !ex2.Any(n => n.Title == "Your PC sent on time") || ex2.Last().Text.IndexOf("UDP carries no sequence", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("journey explain (UDP) wrong: " + k2 + " stalls=" + r2.Count(r => r.GapBefore > 0) + " / " + string.Join(" | ", ex2.Select(n => n.Lv + " " + n.Title)));
                if (Journey.Explain(f, recs.Take(2).ToList(), kind, false)[0].Title != "Too little to go on") throw new InvalidOperationException("journey: two packets should be too little");
                // an idle connection's keepalives (1 byte at one before the next) and their ACKs are not loss (found on a real PC)
                var w3 = new Pcap.Writer(); w3.Add(0, Pcap.Eth(m2, m1, 0x0800, Pcap.Ip4(me, srv, 6, 64, Pcap.Tcp(50123, 443, 1000, 5000, 0x18, 500, new byte[100]))));
                w3.Add(0.05, Pcap.Eth(m1, m2, 0x0800, Pcap.Ip4(srv, me, 6, 52, Pcap.Tcp(443, 50123, 5000, 1100, 0x10, 500, Array.Empty<byte>()))));
                for (int i = 0; i < 4; i++) { w3.Add(5, Pcap.Eth(m2, m1, 0x0800, Pcap.Ip4(me, srv, 6, 64, Pcap.Tcp(50123, 443, 1099, 5000, 0x10, 500, new byte[1])))); w3.Add(0.05, Pcap.Eth(m1, m2, 0x0800, Pcap.Ip4(srv, me, 6, 52, Pcap.Tcp(443, 50123, 5000, 1100, 0x10, 500, Array.Empty<byte>())))); }
                var r3 = Journey.Records(Pcap.Parse(w3.Bytes(), k => Journey.Belongs(k, f)), f, 0); var p3 = Pcap.Parse(w3.Bytes(), k => Journey.Belongs(k, f)); r3 = Journey.Records(p3, f, p3[0].Abs);
                if (r3.Count(z => z.Keepalive) != 4 || r3.Any(z => z.Resend || z.DupAck) || Journey.Explain(f, r3, "web", false).Any(n => n.Title.StartsWith("Packets were lost", StringComparison.Ordinal))) throw new InvalidOperationException("keepalives read as loss: " + r3.Count(z => z.Keepalive) + " keepalives, " + r3.Count(z => z.Resend) + " resends, " + r3.Count(z => z.DupAck) + " dup ACKs");
            });
            T("1.1.5 picks: when, Wi-Fi security and channels, devices, questions", () =>
            {
                DateTime at; if (!Picks.ParseWhen("yesterday 18:40", out at) || at != DateTime.Today.AddDays(-1).AddHours(18).AddMinutes(40) || Picks.ParseWhen("25:99", out at) || !Picks.ParseWhen("7:05", out at) || (Picks.ParseWhen("9", out at) && at > DateTime.Now.AddMinutes(5)) || Picks.ParseWhen("99", out at)) throw new InvalidOperationException("when parser wrong");
                // RSN: version, group CCMP, 1 pairwise CCMP, 1 AKM SAE -> WPA3; AKM PSK -> WPA2; both -> transition mode
                Func<byte, byte, string> rsn = (a1, a2) => { var b = new WlanApi.Bss(); var ie = new List<byte> { 48, 0, 1, 0, 0, 0x0F, 0xAC, 4, 1, 0, 0, 0x0F, 0xAC, 4 }; var ak = new List<byte> { a2 == 0 ? (byte)1 : (byte)2, 0, 0, 0x0F, 0xAC, a1 }; if (a2 != 0) ak.AddRange(new byte[] { 0, 0x0F, 0xAC, a2 }); ie.AddRange(ak); ie[1] = (byte)(ie.Count - 2); WlanApi.ReadIes(ie.ToArray(), b); return b.Sec; };
                if (rsn(8, 0) != "WPA3" || rsn(2, 0) != "WPA2" || rsn(2, 8) != "WPA2/WPA3" || rsn(1, 0) != "WPA2-Enterprise" || !WlanApi.Weak("Open") || WlanApi.Weak("WPA2")) throw new InvalidOperationException("Wi-Fi security reading wrong: " + rsn(8, 0) + " " + rsn(2, 0));
                var aps = new List<Ap> { new Ap { Ssid = "a", Bssid = "1", Band = "2.4 GHz", Channel = 6, Signal = 90 }, new Ap { Ssid = "b", Bssid = "2", Band = "2.4 GHz", Channel = 6, Signal = 80 }, new Ap { Ssid = "c", Bssid = "3", Band = "2.4 GHz", Channel = 1, Signal = 70 }, new Ap { Ssid = "me", Bssid = "9", Band = "2.4 GHz", Channel = 6, Signal = 99 } };
                var lc = WlanApi.LeastCrowded(aps, "2.4 GHz", 6, "9"); if (lc == null || lc[0] != 11 || lc[2] <= lc[1]) throw new InvalidOperationException("least crowded channel wrong: " + (lc == null ? "null" : string.Join(",", lc)));
                // an EU building: 36-48 busy, the router on 100 - never "set it to 149" when nothing around uses the upper block
                var eu = new[] { 36, 40, 44, 48, 36, 44 }.Select((c, i) => new Ap { Ssid = "n" + i, Bssid = "n" + i, Band = "5 GHz", Channel = c, Signal = 70 }).ToList(); eu.Add(new Ap { Ssid = "me", Bssid = "9", Band = "5 GHz", Channel = 100, Signal = 99 });
                var lc5 = WlanApi.LeastCrowded(eu, "5 GHz", 100, "9"); if (lc5 == null || lc5[0] >= 149 || lc5[0] < 52) throw new InvalidOperationException("5 GHz pick wrong for Europe: " + (lc5 == null ? "null" : string.Join(",", lc5)));
                if (DevInfo.Problem(43)[0].IndexOf("stopped", StringComparison.Ordinal) < 0 || DevInfo.Problem(28)[1] == "" || DevInfo.Problem(999)[0].IndexOf("999", StringComparison.Ordinal) < 0) throw new InvalidOperationException("problem codes wrong");
                // the guided checks and the PC lists never throw, whatever Windows (or Wine) gives them
                var lag = Guided.Lag(M, ""); if (lag.Count < 4 || lag[0][1] != "Verdict" || lag.Last()[1] != "Honest limits") throw new InvalidOperationException("game lag check wrong");
                // a router that never answers ping (or the router check off) is "can't tell", never "your home network"
                var mq = new Monitor(); lock (mq.L) { for (int i = 0; i < 30; i++) mq.NetPing.Add(20); }
                var lq = Guided.Lag(mq, ""); if (lq[0][2].IndexOf("home network", StringComparison.OrdinalIgnoreCase) >= 0 || lq[0][2].IndexOf("Wi-Fi", StringComparison.Ordinal) >= 0) throw new InvalidOperationException("lag blamed the home network with no router data: " + lq[0][2]);
                var usb = Guided.Usb(""); if (usb.Count == 0) throw new InvalidOperationException("USB check returned nothing");
                if (Picks.Drivers().Count == 0 || Picks.Timeline(M, "12:00").Count == 0 || Picks.Inventory(M, "not a label").Count == 0) throw new InvalidOperationException("a pick returned nothing");
                Picks.Weather(M);
            });
            T("labs: TCP Reno and CUBIC, loss limit, bufferbloat, jitter buffer, airtime, quiz", () =>
            {
                var p = new LabSim.TcpIn { Algo = "reno", Mbps = 100, RttMs = 40, BufferBdp = 1, Rounds = 200 };
                var reno = LabSim.Tcp(p); double bdp = LabSim.Bdp(100, 40);
                var first = reno.FirstOrDefault(z => z.Loss != ""); if (first == null) throw new InvalidOperationException("Reno never filled the buffer");
                var nxt = reno[first.Round + 1]; if (Math.Abs(nxt.Cwnd - first.Cwnd / 2) > 1 || nxt.Phase != "fast recovery") throw new InvalidOperationException("Reno didn't halve on loss: " + first.Cwnd + " -> " + nxt.Cwnd + " " + nxt.Phase);
                if (reno[first.Round + 3].Cwnd - reno[first.Round + 2].Cwnd != 1) throw new InvalidOperationException("Reno doesn't add one packet per round trip");
                if (reno.Take(4).Any(z => z.Phase != "slow start") || reno[1].Cwnd != 20) throw new InvalidOperationException("slow start doesn't double");
                p.Algo = "cubic"; var cub = LabSim.Tcp(p); var cf = cub.First(z => z.Loss != ""); if (Math.Abs(cub[cf.Round + 1].Cwnd - cf.Cwnd * 0.7) > 1) throw new InvalidOperationException("CUBIC didn't keep 70%");
                if (LabSim.Throughput(cub) < LabSim.Throughput(reno) - 0.02) throw new InvalidOperationException("CUBIC should keep the link at least as busy as Reno here");
                p.Events[30] = "hard"; var hard = LabSim.Tcp(p); if (hard[31].Cwnd != 1) throw new InvalidOperationException("a timeout should drop the window to 1");
                double mm = LabSim.MathisMbps(40, 1); if (mm < 3 || mm > 4) throw new InvalidOperationException("Mathis limit wrong: " + mm);
                var b1 = LabSim.Bloat(20, 20, 500, 20, false); var b2 = LabSim.Bloat(20, 20, 500, 20, true); if (b1.PingMs < 400 || b2.PingMs > 30 || LabSim.Bloat(20, 5, 500, 20, false).PingMs > 25) throw new InvalidOperationException("bufferbloat model wrong");
                var a30 = LabSim.Arrivals(30, 2000, 11); double rj = Enumerable.Range(1, a30.Count - 1).Average(i => Math.Abs(a30[i] - a30[i - 1])); if (rj < 26 || rj > 34) throw new InvalidOperationException("the jitter lab's 30 ms is really " + rj.ToString("0.#", CultureInfo.InvariantCulture) + " ms"); if (LabSim.Jitter(a30, 60, 60, 0).Mos < 3.9) throw new InvalidOperationException("a 2x-jitter buffer should give a usable call: " + LabSim.Jitter(a30, 60, 60, 0).Mos);
                var arr = LabSim.Arrivals(20, 2000, 11); var j1 = LabSim.Jitter(arr, 60, 20, 0); var j2 = LabSim.Jitter(arr, 60, 120, 0); if (j1.LatePct <= j2.LatePct || j1.MouthToEarMs >= j2.MouthToEarMs) throw new InvalidOperationException("jitter buffer trade-off wrong");
                var devs = new List<LabSim.AirDev> { new LabSim.AirDev { RateMbps = 600 }, new LabSim.AirDev { RateMbps = 6 } }; LabSim.Airtime(devs, false, 0); if (devs[1].ShareAir < 0.9) throw new InvalidOperationException("the slow device should hold most of the air");
                LabSim.Airtime(devs, true, 0); if (Math.Abs(devs[0].ShareAir - 0.5) > 0.01) throw new InvalidOperationException("airtime fairness should split evenly");
                if (LabSim.VoiceWaitMs("priority", 10, 50) >= LabSim.VoiceWaitMs("fifo", 10, 50)) throw new InvalidOperationException("priority queue should be faster");
                for (int sd = 0; sd < 200; sd++) { var q = LabSim.Subnet(sd); if (q.Options.Length != 4 || q.Options.Count(o => o == q.Answer) != 1 || q.Options.Distinct().Count() != 4) throw new InvalidOperationException("quiz " + sd + ": " + q.Question + " " + string.Join(",", q.Options)); }
                var qn = LabSim.Subnet(3); if (qn.Question.IndexOf("network address", StringComparison.Ordinal) >= 0) { var part = qn.Question.Split(' ').First(z => z.Contains("/")).TrimEnd('?'); var sub = Toolkit.Subnet(part, ""); if (!sub.Any(r => r[2].StartsWith(qn.Answer + "/", StringComparison.Ordinal))) throw new InvalidOperationException("quiz answer disagrees with the calculator"); }
                var wnd = LabSim.CwndFromCapture(Journey.Sample(), 0.05); if (wnd == null) throw new InvalidOperationException("cwnd estimate failed");
            });
            T("examples: every tool has one, it fits its boxes, it's marked and never copied", () =>
            {
                using (var icon = Theme.MakeIcon(16))
                {
                    var fv = new FullView(icon, M); fv.WindowState = FormWindowState.Normal; fv.Opacity = 0; fv.ShowInTaskbar = false; fv.Show(); Application.DoEvents();
                    try { string bad = fv.ExampleCheck(); if (bad == "" && Examples.Problems.Count > 0) bad = "examples.txt: " + string.Join("; ", Examples.Problems.Take(3)); if (bad != "") throw new InvalidOperationException(bad); }
                    finally { fv.Close(); fv.Dispose(); }
                }
            });
            T("weather: places by name, the forecast parses to UTC, kinds, when to ask, what's kept, the face's minutes", () =>
            {
                var bad = new List<string>();
                string geo = "{\"results\":[{\"id\":6094817,\"name\":\"Ottawa\",\"latitude\":45.41117,\"longitude\":-75.69812,\"feature_code\":\"PPLC\",\"country_code\":\"CA\",\"admin1\":\"Ontario\",\"country\":\"Canada\",\"postcodes\":[\"K1A\"]},{\"id\":4904996,\"name\":\"Ottawa\",\"latitude\":41.34559,\"longitude\":-88.84258,\"admin1\":\"Illinois\",\"country\":\"United States\"},{\"id\":6077243,\"name\":\"Montr\\u00e9al\",\"latitude\":45.50884,\"longitude\":-73.58781,\"admin1\":\"Quebec\",\"country\":\"Canada\"}],\"generationtime_ms\":0.5}";
                var all = Weather.ParsePlaces(geo, "Ottawa"); if (all.Count != 3 || all[0].Name != "Ottawa, Ontario, Canada" || Math.Abs(all[0].Lat - 45.41117) > 1e-6) bad.Add("places: " + string.Join(" / ", all.Select(z => z.Name).ToArray()));
                if (!all.Any(z => z.Name.StartsWith("Montréal", StringComparison.Ordinal))) bad.Add("unicode name not unescaped");
                var il = Weather.ParsePlaces(geo, "Ottawa, Illinois"); if (il.Count != 1 || !il[0].Name.Contains("Illinois")) bad.Add("Ottawa, Illinois didn't narrow");
                if (!Weather.SearchUrl("Dhaka, Bangladesh").EndsWith("name=Dhaka", StringComparison.Ordinal)) bad.Add("search sends more than the first part: " + Weather.SearchUrl("Dhaka, Bangladesh"));
                if (Weather.CleanPlace("45.41;-75.7;Ottawa=x|y") != "45.41;-75.7;Ottawax y" && Weather.CleanPlace("45.41;-75.7;Ottawa=x|y") != "45.41;-75.7;Ottawaxy") bad.Add("place not cleaned: " + Weather.CleanPlace("45.41;-75.7;Ottawa=x|y"));
                if (Weather.CleanPlace("95;10;Nowhere") != "" || Weather.CleanPlace("junk") != "") bad.Add("a bad place was kept");
                if (Weather.ForecastUrl(all[0]).Contains("Ottawa")) bad.Add("the forecast request carries the name");
                string fc = "{\"latitude\":45.4,\"utc_offset_seconds\":-18000,\"hourly_units\":{\"time\":\"iso8601\",\"temperature_2m\":\"°C\"},\"hourly\":{\"time\":[\"2026-11-14T00:00\",\"2026-11-14T01:00\",\"2026-11-14T02:00\"],\"temperature_2m\":[3.1,2.4,null],\"apparent_temperature\":[-1.0,-2.2,-3],\"weather_code\":[3,61,95],\"precipitation_probability\":[10,80,90]}}";
                var hs = Weather.ParseForecast(fc);
                if (hs.Count != 3 || hs[0].Utc != new DateTime(2026, 11, 14, 5, 0, 0, DateTimeKind.Utc) || hs[1].Code != 61 || hs[1].Pop != 80 || !double.IsNaN(hs[2].Temp) || Math.Abs(hs[0].Feels + 1) > 1e-9) bad.Add("forecast parse");
                if (Weather.Kind(hs[0]) != null || Weather.Kind(hs[1]) != "rain" || Weather.Kind(hs[2]) != "storm") bad.Add("kinds from codes");
                if (Weather.Kind(new Weather.Hour { Code = 73 }) != "snow" || Weather.Kind(new Weather.Hour { Code = 0, Feels = 31 }) != "hot" || Weather.Kind(new Weather.Hour { Code = 2, Temp = -3, Feels = -14 }) != "cold" || Weather.Kind(new Weather.Hour { Code = 2, Temp = -9, Feels = -9 }) != null) bad.Add("hot / cold / snow");
                var now = new DateTime(2026, 11, 14, 17, 0, 0, DateTimeKind.Utc);
                Func<int, int, List<Weather.Hour>> span = (from, to) => Enumerable.Range(from, to - from + 1).Select(i => new Weather.Hour { Utc = now.AddHours(i), Temp = 4, Code = 3 }).ToList();
                if (!Weather.NeedFetch(new List<Weather.Hour>(), now, now, "normal")) bad.Add("nothing kept but no fetch");
                if (Weather.NeedFetch(span(-2, 60), now.AddHours(-7), now, "normal")) bad.Add("normal fetched with 60 h left");
                if (!Weather.NeedFetch(span(-2, 20), now.AddHours(-30), now, "normal")) bad.Add("normal didn't fetch with 20 h left");
                if (!Weather.NeedFetch(span(-2, 60), now.AddHours(-7), now, "better")) bad.Add("better didn't refresh after 7 h");
                if (Weather.NeedFetch(span(-2, 60), now.AddHours(-2), now, "better")) bad.Add("better refreshed after 2 h");
                var trimmed = Weather.Trim(span(-50, 80), now); if (trimmed.First().Utc < now.AddHours(-24) || trimmed.Last().Utc > now.AddDays(4)) bad.Add("kept more than 3-4 days");
                if (!Weather.InWindow(new DateTime(2026, 11, 14, 9, 12, 0)) || Weather.InWindow(new DateTime(2026, 11, 14, 9, 15, 0)) || !Weather.InWindow(new DateTime(2026, 11, 14, 9, 54, 20)) || Weather.InWindow(new DateTime(2026, 11, 14, 9, 55, 0))) bad.Add("face minutes");
                int win = Enumerable.Range(0, 3600).Count(sec => Weather.InWindow(new DateTime(2026, 11, 14, 9, 0, 0).AddSeconds(sec))); if (win < 400 || win > 540) bad.Add("face shows " + win + " s an hour, want 6-9 min");
                // the line and the face, with a forecast on hand (settings put back after)
                bool on0 = Cfg.WeatherOn; string pl0 = Cfg.WeatherPlace, co0 = Cfg.Corner; bool f0 = Cfg.WeatherF;
                try
                {
                    Cfg.WeatherOn = true; Cfg.WeatherPlace = "45.41;-75.7;Ottawa, Ontario, Canada"; Cfg.Corner = Moments.WithMoments; Cfg.WeatherF = false;
                    var nowU = DateTime.UtcNow; var hb = new DateTime(nowU.Year, nowU.Month, nowU.Day, nowU.Hour, 0, 0, DateTimeKind.Utc);
                    var l = Enumerable.Range(-1, 30).Select(i => new Weather.Hour { Utc = hb.AddHours(i), Temp = 4, Feels = 1, Code = i >= 0 && i < 3 ? 61 : 3 }).ToList();
                    Weather.Inject(l, Cfg.WeatherPlace);
                    string t = Weather.Text(nowU); if (t == null || !t.StartsWith("4°C · rain until", StringComparison.Ordinal)) bad.Add("line: " + t);
                    if (Weather.Chip(nowU) != "4°" || Weather.ChipEmoji(nowU) != "1f327") bad.Add("chip: " + Weather.Chip(nowU) + " " + Weather.ChipEmoji(nowU));
                    Cfg.WeatherF = true; if (Weather.Chip(nowU) != "39°") bad.Add("°F: " + Weather.Chip(nowU)); Cfg.WeatherF = false;
                    var nx = Enumerable.Range(-1, 30).Select(i => new Weather.Hour { Utc = hb.AddHours(i), Temp = 4, Code = i >= 2 ? 71 : 3 }).ToList(); Weather.Inject(nx, Cfg.WeatherPlace);
                    t = Weather.Text(nowU); if (t == null || !t.Contains("snow from")) bad.Add("coming snow: " + t);
                    Weather.Inject(l, Cfg.WeatherPlace);
                    var inWin = hb.ToLocalTime().AddMinutes(12.5).ToUniversalTime(); var hb0 = hb; 
                    if (inWin.Hour == hb0.Hour && Weather.Face(inWin) != "1f327") bad.Add("no rain face in its minutes");
                    if (Weather.Face(hb.AddMinutes(20)) != null) bad.Add("a face outside its minutes");
                    Cfg.Corner = Moments.Quiet; if (Weather.Face(inWin) != null) bad.Add("a face in the Quiet corner"); Cfg.Corner = Moments.WithMoments;
                    var sn = new Snap { Call = true }; if (Moments.FaceFor(sn) != null) bad.Add("a face during a call");
                    Weather.Inject(l, "1;1;Elsewhere"); if (Weather.Text(nowU) != null) bad.Add("another place's forecast shown");
                    Cfg.WeatherOn = false; if (Weather.Chip(nowU) != null || Weather.Face(inWin) != null) bad.Add("shows while off");
                    foreach (var e in new[] { "1f975", "1f976", "1f327", "1f328", "26c8" }) if (Draw.Emoji(e) == null) bad.Add("emoji " + e + " not built in");
                }
                finally { Cfg.WeatherOn = on0; Cfg.WeatherPlace = pl0; Cfg.Corner = co0; Cfg.WeatherF = f0; Weather.Inject(new List<Weather.Hour>(), ""); }
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad.Take(8)));
            });
            // 21 Nov: the sitting test - what a person notices by leaving it open (Adeeb: "I found a lot of bugs just by sitting").
            // Plays a day on the real window: every page at three sizes, light and dark, idle; a freeze, a sleep; the face and
            // the typing. Reports every problem at once, not just the first.
            T("sitting: idle pages don't keep redrawing, nothing is wider than the window, slow pages, freeze card, sleep, typing, faces", () =>
            {
                var bad = new List<string>();
                Action<int> pump = ms => { var until = DateTime.UtcNow.AddMilliseconds(ms); while (DateTime.UtcNow < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } };
                // the face and the typing (no window needed)
                Typing.Show("sit", "first line", null); if (Typing.Show("sit", "second line", null) != "second line") bad.Add("typing: a line changing again within 15 s types again (should show whole)");
                BuddyReact.Now(0); BuddyReact.Now(2); if (BuddyReact.Now(2) != "shout") bad.Add("face: an outage starting doesn't shout");
                if (BuddyReact.Now(0) != "love") bad.Add("face: coming back up isn't love");
                BuddyReact.Set("happy", 0.2); System.Threading.Thread.Sleep(300); if (BuddyReact.Now(0) != null) bad.Add("face: a reaction doesn't settle back");
                BuddyReact.ToolDone(new List<string[]> { new[] { "0", "a", "b" }, new[] { "2", "c", "d" } }, false); if (BuddyReact.Now(0) != "tears") bad.Add("face: a red result isn't tears");
                BuddyReact.Set("smug", 0);
                using (var icon = Theme.MakeIcon(16))
                {
                    var fv = new FullView(icon, M); fv.WindowState = FormWindowState.Normal; fv.Opacity = 0; fv.ShowInTaskbar = false; fv.Size = new Size(1280, 860); fv.Show(); pump(1500);
                    try
                    {
                        var pages = new[] { "overview", "toolbox", "gamer", "stream", "trade", "replay", "learn", "security", "travel", "dest", "procs", "health", "link", "changes", "outlook", "conn", "windows", "phone" };
                        foreach (var dark in new[] { false, true })
                        {
                            Theme.Apply(dark); fv.Invalidate(true);
                            foreach (var sz in new[] { new Size(1280, 860), new Size(1024, 700), new Size(1920, 1080) })
                            {
                                fv.Size = sz; pump(300);
                                foreach (var pg in pages)
                                {
                                    string at = pg + " " + sz.Width + "x" + sz.Height + (dark ? " dark" : "");
                                    try { fv.TestShow(pg); } catch (Exception e) { bad.Add(at + ": opening threw " + e.GetType().Name + " " + e.Message); continue; }
                                    pump(700);
                                    var cs = fv.TestCanvases();
                                    foreach (var c in cs)
                                    {
                                        var sw = System.Diagnostics.Stopwatch.StartNew(); c.Refresh(); sw.Stop();
                                        if (sw.ElapsedMilliseconds > 400) bad.Add(at + ": " + c.Name + " takes " + sw.ElapsedMilliseconds + " ms to draw");
                                        if (c.AutoScrollMinSize.Width > c.ClientSize.Width + 4) bad.Add(at + ": " + c.Name + " is wider than the window (" + c.AutoScrollMinSize.Width + " > " + c.ClientSize.Width + ")");
                                    }
                                    if (sz.Width != 1280 || dark) continue;   // idle redraw: once per page is enough
                                    foreach (var c in cs) { c.Paints = 0; c.PaintArea = 0; }
                                    pump(5000);
                                    foreach (var c in cs)
                                    {
                                        double full = c.PaintArea / Math.Max(1.0, (double)c.ClientSize.Width * c.ClientSize.Height);
                                        if (full > 2.5) bad.Add(pg + ": " + c.Name + " redrew " + full.ToString("0.0", CultureInfo.InvariantCulture) + " whole pages in 5 s while idle (" + c.Paints + " paints)");
                                    }
                                }
                            }
                        }
                        Theme.Apply(false);
                        // the other languages: longer words are where text runs off the page - a new window per language, as after a switch
                        string oldL = L.Code;
                        foreach (var lc in new[] { "fr", "es", "bn", "ar" })
                        {
                            try
                            {
                                L.Use(lc); Draw.UseLanguageFonts();
                                var lv = new FullView(icon, M); lv.WindowState = FormWindowState.Normal; lv.Opacity = 0; lv.ShowInTaskbar = false; lv.Size = new Size(1024, 700); lv.Show(); pump(800);
                                try
                                {
                                    foreach (var pg in pages)
                                    {
                                        try { lv.TestShow(pg); } catch (Exception e) { bad.Add(lc + " " + pg + ": opening threw " + e.GetType().Name + " " + e.Message); continue; }
                                        pump(400);
                                        foreach (var c in lv.TestCanvases())
                                        {
                                            try { c.Refresh(); } catch (Exception e) { bad.Add(lc + " " + pg + ": drawing threw " + e.GetType().Name); }
                                            if (c.AutoScrollMinSize.Width > c.ClientSize.Width + 4) bad.Add(lc + " " + pg + ": " + c.Name + " is wider than the window");
                                        }
                                    }
                                }
                                finally { lv.Close(); lv.Dispose(); }
                            }
                            finally { L.Use(oldL); Draw.UseLanguageFonts(); }
                        }
                        // a real freeze shows the card, and it goes when the window answers
                        fv.TestShow("overview"); pump(500); int shown0 = FreezeCard.Shown;
                        System.Threading.Thread.Sleep(3300); pump(2500);
                        if (FreezeCard.Shown == shown0) bad.Add("freeze card: a 3 s freeze didn't show it");
                        if (FreezeCard.Visible) bad.Add("freeze card: still up after the window answered");
                        // waking from sleep isn't a freeze
                        int shown1 = FreezeCard.Shown; fv.TestSlept(); pump(2500);
                        if (FreezeCard.Shown != shown1 || FreezeCard.Visible) bad.Add("freeze card: shown after a sleep");
                        // the Time machine's "Opened from" ends when you leave
                        fv.OpenTimeAt(DateTime.Now.AddMinutes(-2), "sitting test"); pump(400);
                        if (fv.TestOpenedFrom != "sitting test") bad.Add("Time machine: Opened from not set");
                        fv.TestShow("overview"); pump(200); if (fv.TestOpenedFrom != "") bad.Add("Time machine: Opened from still set after leaving");
                    }
                    finally { fv.Close(); fv.Dispose(); Theme.Apply(false); }
                }
                if (bad.Count > 0) throw new InvalidOperationException(bad.Count + " problem(s):\n  " + string.Join("\n  ", bad.Distinct().Take(80)));
            });
            T("tool pictures: each one draws before a run and with its Example, and the Example fills it", () =>
            {
                using (var icon = Theme.MakeIcon(16))
                {
                    var fv = new FullView(icon, M); fv.WindowState = FormWindowState.Normal; fv.Opacity = 0; fv.ShowInTaskbar = false; fv.Show(); Application.DoEvents();
                    try { string bad = fv.VisualCheck(); if (bad != "") throw new InvalidOperationException(bad); string gc = Glyphs.Check(); if (gc != "") throw new InvalidOperationException("icons: " + gc); using (var fb = new Bitmap(64, 64)) using (var fg = Graphics.FromImage(fb)) foreach (var fm in BuddyReact.All) { BuddyReact.Set(fm, 5); Draw.Buddy(fg, new RectangleF(4, 4, 56, 56), new Snap(), false); } BuddyReact.Set("smug", 0); if (FullView.TmWorse(10, 40, 10, true) != "w" || FullView.TmWorse(80, 40, 10, false) != "w" || FullView.TmWorse(40, 20, 10, true) != "b" || FullView.TmWorse(10, 15, 10, true) != "" || FullView.TmWorse(double.NaN, 5, 1, true) != "") throw new InvalidOperationException("pinned-moment compare marks worse/better wrongly"); string tc = fv.TopicCheck(); if (tc != "") throw new InvalidOperationException("toolbox topics: " + tc); }
                    finally { fv.Close(); fv.Dispose(); }
                }
            });
            T("network lab: the page is in the exe, the path hand-over, Edge's localStorage read back, faults earn once", () =>
            {
                var bad = new List<string>();
                using (var rs = typeof(LabLink).Assembly.GetManifestResourceStream("AdeeboMeter.lab.html")) if (rs == null || rs.Length < 20000) bad.Add("lab.html not packed into the exe");
                var sn = BrowserStore.Snappy(new byte[] { 9, 0x08, (byte)'a', (byte)'b', (byte)'c', 0x09, 3 }); if (System.Text.Encoding.ASCII.GetString(sn) != "abcabcabc") bad.Add("snappy: " + System.Text.Encoding.ASCII.GetString(sn));
                string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "am-labtest-" + Guid.NewGuid().ToString("N")), ldb = System.IO.Path.Combine(dir, "Default", "Local Storage", "leveldb");
                try
                {
                    System.IO.Directory.CreateDirectory(ldb);
                    var key = new List<byte>(System.Text.Encoding.ASCII.GetBytes("_file://")); key.Add(0); key.Add(1); key.AddRange(System.Text.Encoding.ASCII.GetBytes("adeebometer-lab:solved"));
                    var val = new List<byte> { 1 }; val.AddRange(System.Text.Encoding.ASCII.GetBytes("[\"mtu\",\"bogus\"]"));
                    var log = new List<byte>(BrowserStore.TestLog(7, key.ToArray(), val.ToArray()));
                    var damaged = BrowserStore.TestLog(9, key.ToArray(), System.Text.Encoding.ASCII.GetBytes("\u0001[\"dns\"]")); damaged[damaged.Length - 2] ^= 0x55; log.AddRange(damaged);   // a half-written record: never read
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(ldb, "000003.log"), log.ToArray());
                    string got; var ls = BrowserStore.LocalStorage(dir, "adeebometer-lab:");
                    if (!ls.TryGetValue("adeebometer-lab:solved", out got) || got != "[\"mtu\",\"bogus\"]") bad.Add("localStorage read back: " + (got ?? "nothing"));
                    if (BrowserStore.Stamp(dir) == "") bad.Add("no folder fingerprint");
                }
                finally { try { System.IO.Directory.Delete(dir, true); } catch (System.IO.IOException) { } catch (UnauthorizedAccessException) { } }
                if (!Points.Known("nlab.mtu") || Points.Known("nlab.bogus") || Points.Value("nlab.mtu") != 2) bad.Add("nlab points keys");
                foreach (var who in new[] { "game", "view", "trade", "stream", "server" })
                {
                    string j = LabLink.PathJson(M, who, "203.0.113.9", "Test \"quoted\" server", 31, 2, 18);
                    if (!j.StartsWith("{\"name\":", StringComparison.Ordinal) || !j.Contains("\"measured\":true") || !j.EndsWith("]}", StringComparison.Ordinal) || !j.Contains("31 ms") || j.Contains("Test \"quoted")) bad.Add("path json (" + who + ")");
                }
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("UI 1.1.5 wave 3: search finds tools, forgives typos, groups; tool chips make sense", () =>
            {
                var vocab = new HashSet<string>(StringComparer.Ordinal) { "wifi", "dns", "traceroute", "certificate", "port" };
                if (FullView.Fix("wfii", vocab) != "wifi" || FullView.Fix("tracerout", vocab) != "traceroute" || FullView.Fix("certifcate", vocab) != "certificate" || FullView.Fix("443", vocab) != null || FullView.Fix("dns", vocab) != null || FullView.Fix("zzzzzz", vocab) != null) throw new InvalidOperationException("typo fixing wrong: " + FullView.Fix("wfii", vocab));
                var it = new PalItem { Title = "Wi-Fi roams and drops", Words = "wi-fi roams and drops" };
                if (FullView.Score(it, new[] { "wi" }) == 0 || FullView.Score(it, new[] { "roams" }) == 0 || FullView.Score(it, new[] { "oams" }) >= FullView.Score(it, new[] { "roams" }) || FullView.Score(it, new[] { "printer" }) != 0) throw new InvalidOperationException("search scoring wrong");
                bool br; var c = FullView.AssistFor("Record type - A, AAAA, MX, TXT", "", "", null, out br);
                if (c.Count != 4 || c[0] != "A" || br) throw new InvalidOperationException("choices from the hint: " + string.Join(",", c));
                if (FullView.AssistFor("URL - e.g. github.com or https://203.0.113.5", "10.0.0.1", "", null, out br).Any(z => z.Contains(":") && !z.Contains("."))) throw new InvalidOperationException("URL example read as choices");
                if (FullView.AssistFor("host:port - e.g. smtp.office365.com:587", "10.0.0.1", "", null, out br).FirstOrDefault() != "10.0.0.1:587" || FullView.AssistFor("Second saved result (.txt)", "10.0.0.1", "", null, out br).Count != 0 || !br || FullView.AssistFor("Name this run - e.g. VPN off", "10.0.0.1", "", null, out br).Count != 0 || FullView.AssistFor("Hosts each subnet needs - e.g. 200, 60, 12, 2", "", "", null, out br).Count != 0) throw new InvalidOperationException("host:port chip has no port");
                if (FullView.AssistFor("Shared secret", "10.0.0.1", "x", null, out br).Count != 0) throw new InvalidOperationException("chips on a secret");
                if (FullView.NavAfterDelete("home,mine1,toolbox,mine3,mine2", "mine2") != "home,mine1,toolbox,mine2" || Cfg.NavClean("toolbox,home,bogus,gamer,a,b,c,d,e,f,g,time,learn,story,security") .Split(',').Length > 7 || !Cfg.NavClean("toolbox").StartsWith("home,", StringComparison.Ordinal)) throw new InvalidOperationException("tab order rules wrong: " + FullView.NavAfterDelete("home,mine1,toolbox,mine3,mine2", "mine2"));
                using (var icon = Theme.MakeIcon(16))
                {
                    var fv = new FullView(icon, M); fv.WindowState = FormWindowState.Normal; fv.Opacity = 0; fv.ShowInTaskbar = false; fv.Show(); Application.DoEvents();
                    try { string bad = fv.AssistCheck(sb); if (bad != "") throw new InvalidOperationException(bad); }
                    finally { fv.Close(); fv.Dispose(); }
                }
            });
            T("buddy lines: every built-in line passes the tone rules, every persona falls back, nothing is blank", () =>
            {
                var bad = new List<string>(); int n = 0;
                foreach (var id in new[] { "plain" }.Concat(Lines.Moods.Where(m => m != "plain")).Concat(Lines.Personas.Select(p => p[0])))
                {
                    string raw = Knowledge.RawText("lines-" + id); if (raw == "") { bad.Add(id + ": no file"); continue; }
                    bad.AddRange(Lines.Check(id, raw)); n += Lines.Parse(raw).Values.Sum(l => l.Count);
                }
                string pm = Lines.Mood, pp = Lines.Persona, pa = Lines.Addr; bool ps = Voice.Serious;
                try
                {
                    Voice.Serious = false;
                    foreach (var p in Lines.Personas.Select(z => z[0]).Concat(new[] { "none", "surprise" }))
                    {
                        Lines.Persona = p;
                        foreach (var key in Lines.Keys) { string t = Lines.Line(key, "42"); if (t == "" || t.Contains("{")) bad.Add(p + "/" + key + ": '" + t + "'"); }
                    }
                    Lines.Persona = "epic-fantasy"; Lines.Addr = "maam"; if (!Lines.Line("all.good", null).Contains("lady") && !Lines.AddrFor("epic-fantasy").Contains("lady")) bad.Add("address");
                    Lines.Persona = "surprise"; if (Lines.Who() == "none" || Lines.Who() == "plain") bad.Add("Surprise me picked " + Lines.Who());
                }
                finally { Lines.Mood = pm; Lines.Persona = pp; Lines.Addr = pa; Voice.Serious = ps; }
                foreach (var f in Lines.Faiths.Skip(1)) if (!Lines.Comforts().Any(c => c[0] == f)) bad.Add("comfort: nothing for " + f);
                if (Lines.Comforts().Any(c => c[1].Length < 10 || (c[0] == "common" ? c[2].IndexOf("faiths", StringComparison.Ordinal) < 0 : c[2].IndexOf('(') < 0))) bad.Add("comfort: a verse without its translation (or a shared line that doesn't say it's shared)");
                string fm = Lines.Faith, mo = Lines.Mood; try { Lines.Faith = "islam"; Lines.Mood = "roast"; if (Lines.Comfort() != "") bad.Add("comfort shown in Roast"); Lines.Mood = "chill"; if (Lines.Comfort() == "") bad.Add("comfort missing"); } finally { Lines.Faith = fm; Lines.Mood = mo; }
                foreach (var m in new[] { "smug", "sweaty", "worried", "dizzy", "betrayed", "asleep", "smile" }) if (typeof(Draw).Assembly.GetManifestResourceStream("AdeeboMeter.emoji." + Draw.EmojiFor(m) + ".png") == null) bad.Add("emoji face missing for " + m);
                int surprise = Enumerable.Range(0, 366).Count(d => Draw.SurpriseFace(new DateTime(2027, 1, 1).AddDays(d)) != null); if (surprise < 5 || surprise > 40) bad.Add("surprise faces on " + surprise + " days a year");
                if (Draw.HourMark(new Snap { Level = 0 }, new DateTime(2027, 1, 1, 22, 0, 1)) != "asleep" || Draw.HourMark(new Snap { Level = 2 }, new DateTime(2027, 1, 1, 14, 0, 1)) != null || Draw.HourMark(new Snap { Level = 0, Call = true }, new DateTime(2027, 1, 1, 14, 0, 1)) != null) bad.Add("hour mark rules");
                List<string> kept; var rep = Lines.Import("all.good\tAll calm, {adr}.\nwifi.drop|Wi-Fi left {v} times\nzzz.zzz|no\nidle|one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen\nbusy|hi\u202Ethere\n" + new string('x', 250), out kept);
                if (kept.Count != 4 || kept[0] != "all.good|All calm, {addr}." || kept[1] != "wifi.drops|Wi-Fi left {v} times" || !kept[2].EndsWith("...", StringComparison.Ordinal) || kept[3] != "busy|hithere" || !rep.Any(r => r.Contains("unknown key")) || !rep.Any(r => r.Contains("200 characters"))) bad.Add("import fixes: " + string.Join(" / ", kept));
                if (Lines.Import(string.Join("\n", Enumerable.Repeat("idle|x", 601)), out kept).All(r => !r.Contains("600"))) bad.Add("import limit");
                sb.AppendLine("      " + n + " built-in lines in " + (1 + Lines.Moods.Length - 1 + Lines.Personas.Length) + " files");
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad.Take(8)) + (bad.Count > 8 ? " (+" + (bad.Count - 8) + " more)" : ""));
            });
            T("never slow, never silent: pages draw fast in both layouts and themes, tools start off the window thread, the freeze card shows", () =>
            {
                using (var icon = Theme.MakeIcon(16))
                {
                    var fv = new FullView(icon, M); fv.WindowState = FormWindowState.Normal; fv.ShowInTaskbar = false; fv.Show(); Application.DoEvents();
                    try
                    {
                        // 50 ms on a real PC (Adeeb, 16 Nov: slower PCs than the test ones must pass too - still well under the 100 ms people notice);
                        // Wine draws text in software, so the self-test there allows 4x and reports the numbers
                        // AM-CG-306: a PC that's already busy (CPU over 50 % or memory over 85 % before the test) gets 3x the room,
                        // and the run says so - the numbers still show, so an idle PC's rerun is the real verdict
                        double busy = Busy(M); double lim = (Program.IsWine ? 120 : 50) * busy;
                        if (busy > 1) sb.AppendLine("      busy PC (CPU " + Math.Round(M.Cpu.Last) + "%, memory " + Math.Round(MemNow(M)) + "%): timing limits x3 - rerun on an idle PC for the real numbers");
                        string b0 = fv.QuestionCheck(), b1 = fv.PerfCheck(sb, lim), b2 = fv.StartCheck(sb, (Program.IsWine ? 400 : 150) * busy), b3 = FullView.FreezeCheck(sb);
                        string all = string.Join("; ", new[] { b0, b1, b2, b3 }.Where(z => z != ""));
                        if (all != "") throw new InvalidOperationException(all);
                    }
                    finally { fv.Close(); fv.Dispose(); }
                }
            });
            T("watch engine and streaming: verdicts blame the earliest broken hop, dips merge, records survive a save, streams are found by server name", () =>
            {
                Func<WatchKit.Sample> ok = () => new WatchKit.Sample { T = DateTime.Now, Router = 3, Net = 20, Far = 40, Down = 8, Cpu = 10, Wifi = true, Signal = 80 };
                var s = ok(); if (WatchKit.Judge(s, 40) != null) throw new InvalidOperationException("a healthy path got a verdict");
                s = ok(); s.Router = 200; s.Net = 400; s.Far = 900; if (WatchKit.Judge(s, 40) != "wifi") throw new InvalidOperationException("slow router on Wi-Fi must blame Wi-Fi, not " + WatchKit.Judge(s, 40));
                s = ok(); s.Wifi = false; s.RouterLost = true; if (WatchKit.Judge(s, 40) != "home") throw new InvalidOperationException("lost router on a cable must blame the home network");
                s = ok(); s.NetLost = true; s.Far = 900; if (WatchKit.Judge(s, 40) != "provider") throw new InvalidOperationException("internet loss with a fine router must blame the provider");
                s = ok(); s.Far = 400; if (WatchKit.Judge(s, 40) != "far") throw new InvalidOperationException("only the server slow must blame the server");
                s = ok(); s.Far = 100; if (WatchKit.Judge(s, 40) != null) throw new InvalidOperationException("100 ms against a 40 ms normal is not a dip (needs 150 and 3x)");
                s = ok(); s.Cpu = 97; if (WatchKit.Judge(s, 40) != "pc") throw new InvalidOperationException("a pegged processor must blame the PC");
                s = ok(); s.Signal = 20; if (WatchKit.Judge(s, 40) != "wifi") throw new InvalidOperationException("20% signal must blame Wi-Fi");
                // three bad samples 5 s apart are one dip; a gap of a minute starts another
                var mk = new WatchKit.DipMaker(); var t0 = new DateTime(2026, 11, 11, 21, 10, 0); var dips = new List<WatchKit.Dip>();
                Action<int, string> feed = (sec, b) => { var x = ok(); x.T = t0.AddSeconds(sec); if (b == "far") x.Far = 400; if (b == "provider") x.NetLost = true; var d = mk.Add(x, WatchKit.Judge(x, 40), 5, "the stream's server"); if (d != null) dips.Add(d); };
                feed(0, "far"); feed(5, "far"); feed(10, "provider"); feed(15, null); feed(20, null); feed(80, "provider"); feed(85, null); feed(100, null);
                var last = mk.Close("the stream's server"); if (last != null) dips.Add(last);
                if (dips.Count != 2 || dips[0].Blame != "far" || dips[0].Secs != 10 || dips[1].Blame != "provider") throw new InvalidOperationException("dips merged wrong: " + string.Join(", ", dips.Select(d => d.Blame + "/" + d.Secs)));
                if (dips[0].Evidence.IndexOf("The stream's server", StringComparison.Ordinal) < 0) throw new InvalidOperationException("evidence must name the server: " + dips[0].Evidence);
                // a record survives encoding, tabs and all
                var ses = new WatchKit.Session { Mode = "stream", What = "TiviMate\t(IPTV)", Via = "Chrome", Far = "1.2.3.4", Start = t0, End = t0.AddMinutes(125), AvgDown = 8.4, FarNormal = 41 };
                ses.Dips.AddRange(dips); ses.Line.Add(new[] { 0f, 8.2f, 40f }); ses.Line.Add(new[] { 1f, 7.9f, -1f });
                var back = WatchKit.Decode(WatchKit.Encode(ses));
                if (back == null || back.Dips.Count != 2 || back.Line.Count != 2 || back.Length.TotalMinutes != 125 || back.Dips[1].Blame != "provider" || Math.Abs(back.AvgDown - 8.4) > 0.01) throw new InvalidOperationException("the record didn't survive a save");
                if (WatchKit.Decode("junk\tline") != null || WatchKit.Decode("") != null) throw new InvalidOperationException("junk read as a record");
                var tally = back.Tally(); if (tally["far"] != 1 || tally["provider"] != 1 || tally.Values.Sum() != 2) throw new InvalidOperationException("tally wrong");
                if (!WatchKit.MonthReport(new List<WatchKit.Session> { back }, t0, "Streaming").Contains("beyond my router")) throw new InvalidOperationException("the monthly report lost its line for the provider");
                // found by server name, apps and players alike
                var want = new Dictionary<string, string> { { "rr3---sn-ab5l6n7s.googlevideo.com", "YouTube" }, { "ipv4-c001-yyz001-ix.1.oca.nflxvideo.net", "Netflix" }, { "video-weaver.yto01.hls.ttvnw.net", "Twitch" }, { "googlevideo.com.evil.example", null }, { "notnetflix.com", null }, { "", null } };
                foreach (var kv in want) if (Streams.ServiceOf(kv.Key) != kv.Value) throw new InvalidOperationException(kv.Key + " -> " + Streams.ServiceOf(kv.Key) + ", wanted " + kv.Value);
                if (Streams.PlayerOf("vlc") != "VLC" || Streams.PlayerOf("Kodi.exe") != "Kodi" || Streams.PlayerOf("chrome") != null) throw new InvalidOperationException("player apps not recognised");
                var vw = Streams.Verdict4K(45, 200, 10, true); if (!vw[3].StartsWith("4K|risky", StringComparison.Ordinal) || vw[4].IndexOf("Wi-Fi", StringComparison.Ordinal) < 0) throw new InvalidOperationException("45 Mb/s on 10% Wi-Fi must not be 4K-safe (AM-CG-138)");
                if (Streams.Median(new List<double> { 10, 90 }) != 50) throw new InvalidOperationException("median of 10 and 90 must be 50 (AM-CG-137)");
                var v = Streams.Verdict4K(18, 3, 41, true); if (!v[3].StartsWith("4K|no", StringComparison.Ordinal) && !v[3].StartsWith("4K|risky", StringComparison.Ordinal)) throw new InvalidOperationException("18 Mb/s is not safe for 4K: " + v[3]);
                if (!v[1].StartsWith("HD|safe", StringComparison.Ordinal) || v[4].IndexOf("Wi-Fi", StringComparison.Ordinal) < 0) throw new InvalidOperationException("18 Mb/s on weak Wi-Fi: HD safe, Wi-Fi named");
            });
            T("trading: Bank of Canada rates parse with the day before, broker addresses are cleaned, market hours are Eastern weekdays", () =>
            {
                string json = "{\"terms\":{\"url\":\"x\"},\"seriesDetail\":{\"FXUSDCAD\":{\"label\":\"USD/CAD\"}},\"observations\":[{\"d\":\"2026-11-12\",\"FXUSDCAD\":{\"v\":\"1.3900\"},\"FXEURCAD\":{\"v\":\"1.5100\"}},{\"d\":\"2026-11-13\",\"FXUSDCAD\":{\"v\":\"1.3950\"},\"FXEURCAD\":{\"v\":\"1.5000\"},\"FXJPYCAD\":{\"v\":\"0.00912\"}}]}";
                var r = Trading.ParseFx(json);
                var usd = r.FirstOrDefault(z => z.Code == "USD"); var eur = r.FirstOrDefault(z => z.Code == "EUR"); var jpy = r.FirstOrDefault(z => z.Code == "JPY");
                if (usd == null || usd.Cad != 1.395 || usd.Prev != 1.39 || usd.Day != new DateTime(2026, 11, 13)) throw new InvalidOperationException("USD wrong");
                if (eur == null || eur.Cad != 1.5 || eur.Prev != 1.51) throw new InvalidOperationException("EUR wrong");
                if (jpy == null || !double.IsNaN(jpy.Prev)) throw new InvalidOperationException("JPY with one day must have no change");
                if (Trading.ParseFx("<html>error</html>").Count != 0 || Trading.ParseFx(null).Count != 0) throw new InvalidOperationException("junk read as rates");
                var hosts = new Dictionary<string, string> { { "https://my.wealthsimple.com/app/home?x=1", "my.wealthsimple.com" }, { "Questrade.com", "questrade.com" }, { "1.2.3.4:443", "1.2.3.4" }, { "evil.com; rm -rf", "" }, { "", "" }, { "a b.com", "" }, { "https://jane:secret@broker.example/x", "broker.example" }, { "broker.example:8443", "broker.example:8443" }, { "[2001:DB8::1]:8443", "[2001:db8::1]:8443" }, { "broker.example:70000", "" } };
                foreach (var kv in hosts) if (Cfg.CleanHost(kv.Key) != kv.Value) throw new InvalidOperationException("CleanHost(" + kv.Key + ") = " + Cfg.CleanHost(kv.Key));
                string hid0 = Cfg.HiddenSecs, pre0 = Cfg.Preset; bool det0 = Cfg.Details;
                try
                {
                    Cfg.HiddenSecs = ""; Cfg.SetOff("trade", true); Cfg.SetOff("bogus", true); Cfg.SetOff("stream", true); Cfg.SetOff("trade", false);
                    if (Cfg.HiddenSecs != "stream" || !Cfg.Off("stream") || Cfg.Off("trade") || Cfg.Off("strea")) throw new InvalidOperationException("hiding sections: " + Cfg.HiddenSecs);
                    Cfg.Details = false; Cfg.Preset = "penny"; if (Trading.Sec != 30 || Streams.SampleSec != 30) throw new InvalidOperationException("Penny must check every 30 s");
                    Cfg.Preset = "balanced"; if (Trading.Sec != 10 || Streams.SampleSec != 5) throw new InvalidOperationException("Balanced: broker 10 s, stream 5 s");
                    Cfg.Details = true; Trading.viewOpen = true; if (Trading.Sec != 2) throw new InvalidOperationException("2 s only with Details on and the view open");
                    Trading.viewOpen = false; if (Trading.Sec != 10) throw new InvalidOperationException("Details in the corner: back to the preset's pace (AM-CG-114)");
                }
                finally { Cfg.HiddenSecs = hid0; Cfg.Preset = pre0; Cfg.Details = det0; }
                if (!Trading.MarketOpen(new DateTime(2026, 11, 13, 10, 0, 0)) || Trading.MarketOpen(new DateTime(2026, 11, 13, 9, 29, 0)) || Trading.MarketOpen(new DateTime(2026, 11, 13, 16, 0, 0)) || Trading.MarketOpen(new DateTime(2026, 11, 14, 11, 0, 0))) throw new InvalidOperationException("market hours wrong");
            });
            T("market holidays, weekly commodities and the evening line", () =>
            {
                // holidays (2026: Thanksgiving US Thu 26 Nov, Canada Mon 12 Oct; Christmas Fri, Boxing Day Sat -> Mon 28; Good Friday 3 Apr)
                var want = new[] { "2026-11-26 us Thanksgiving", "2026-10-12 ca Thanksgiving", "2026-04-03 us Good Friday", "2026-04-03 ca Good Friday", "2026-12-28 ca Boxing Day", "2026-07-03 us Independence Day", "2026-05-18 ca Victoria Day", "2026-08-03 ca Civic Holiday", "2026-06-19 us Juneteenth", "2027-12-24 us Christmas", "2028-12-26 ca Boxing Day", "2027-12-27 ca Christmas", "2026-01-19 us Martin Luther King Jr. Day" };
                foreach (var w in want)
                {
                    var p = w.Split(new[] { ' ' }, 3); var d = DateTime.ParseExact(p[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    string h = Markets.Holiday(d, p[1]); if (h != p[2]) throw new InvalidOperationException(w + " -> " + (h ?? "open"));
                }
                if (Markets.Holiday(new DateTime(2026, 11, 26), "ca") != null || Markets.Holiday(new DateTime(2026, 10, 12), "us") != null || Markets.Holiday(new DateTime(2027, 12, 31), "us") != null) throw new InvalidOperationException("a trading day taken for a holiday");
                if (Markets.Easter(2026) != new DateTime(2026, 4, 5) || Markets.Easter(2027) != new DateTime(2027, 3, 28)) throw new InvalidOperationException("Easter");
                if (!Trading.MarketOpen(new DateTime(2026, 11, 26, 11, 0, 0))) throw new InvalidOperationException("US Thanksgiving: Toronto still trades");
                if (Trading.MarketOpen(new DateTime(2026, 4, 3, 11, 0, 0)) || Trading.MarketOpen(new DateTime(2026, 12, 24, 13, 30, 0)) || !Trading.MarketOpen(new DateTime(2026, 12, 24, 12, 30, 0))) throw new InvalidOperationException("Good Friday / Christmas Eve half day");
                if (!Trading.MarketOpen(new DateTime(2026, 11, 27, 14, 0, 0)) || Markets.Open(new DateTime(2026, 11, 27, 14, 0, 0), "us")) throw new InvalidOperationException("day after Thanksgiving: US closes 13:00, Toronto open");
                if (Markets.TodayNote(new DateTime(2026, 11, 26, 10, 0, 0)).IndexOf("Thanksgiving", StringComparison.Ordinal) < 0 || Markets.TodayNote(new DateTime(2026, 11, 13, 10, 0, 0)) != "") throw new InvalidOperationException("holiday note");
                // the Bank of Canada's weekly index
                string js = "{\"observations\":[{\"d\":\"2026-11-04\",\"W.MTLS\":{\"v\":\"1088.31\"},\"W.ENER\":{\"v\":\"1592.96\"}},{\"d\":\"2026-10-28\",\"W.MTLS\":{\"v\":\"1095.74\"},\"W.ENER\":{\"v\":\"1650.99\"}},{\"d\":\"2026-10-07\",\"W.ENER\":{\"v\":\"1543.23\"}}]}";
                var ix = Trading.ParseIdx(js); var en = ix.FirstOrDefault(i => i.Code == "ENER");
                if (ix.Count != 2 || en == null || en.Day != new DateTime(2026, 11, 4) || en.V.Length != 3 || Math.Abs(en.V[2] - 1592.96) > 0.001) throw new InvalidOperationException("weekly index parse");
                double wk = Trading.Change(en, 1); if (Math.Abs(wk - (1592.96 - 1650.99) / 1650.99 * 100) > 0.001 || !double.IsNaN(Trading.Change(en, 4))) throw new InvalidOperationException("weekly change");
                if (Trading.ParseIdx("<html>nope</html>").Count != 0) throw new InvalidOperationException("junk must give nothing");
                // the evening line: only in its window, and it says what it has
                if (!Trading.EveningNow(new DateTime(2026, 11, 13, 17, 30, 0)) || Trading.EveningNow(new DateTime(2026, 11, 13, 19, 0, 0)) || Trading.EveningNow(new DateTime(2026, 11, 14, 18, 0, 0))) throw new InvalidOperationException("evening window");
                var rates = new List<Trading.Rate> { new Trading.Rate { Code = "USD", Day = new DateTime(2026, 11, 13), Cad = 1.395, Prev = 1.392 } };
                string ev = Trading.EveningText(rates, ix, new DateTime(2026, 11, 13));
                if (ev == null || ev.IndexOf("1", StringComparison.Ordinal) < 0 || ev.IndexOf("▲", StringComparison.Ordinal) < 0 || ev.IndexOf("▼", StringComparison.Ordinal) < 0) throw new InvalidOperationException("evening line: " + ev);
                if (Trading.EveningText(new List<Trading.Rate>(), new List<Trading.Index>(), DateTime.Today) != null) throw new InvalidOperationException("no numbers: no evening line");
                bool eve0 = Cfg.TradeEvening; try { Cfg.TradeEvening = false; if (Trading.EveningLine() != null) throw new InvalidOperationException("evening line while off"); } finally { Cfg.TradeEvening = eve0; }
            });
            T("your week, the provider's evidence, lessons, the first question and the bug report", () =>
            {
                var t0 = new DateTime(2026, 11, 10, 20, 0, 0);
                Func<string, string, string[], WatchKit.Session> sess = (mode, what, blames) => { var x = new WatchKit.Session { Mode = mode, What = what, Far = "wealthsimple.com", Start = t0, End = t0.AddHours(2) }; foreach (var bl in blames) x.Dips.Add(new WatchKit.Dip { T = t0.AddMinutes(10), Secs = 20, Blame = bl, Evidence = "test" }); return x; };
                var st = new List<WatchKit.Session> { sess("stream", "Netflix", new[] { "wifi", "wifi", "provider" }), sess("stream", "YouTube", Array.Empty<string>()) };
                var tr = new List<WatchKit.Session> { sess("trade", "wealthsimple.com", new[] { "wifi" }) };
                var gm = new List<GameHistory.Row> { new GameHistory.Row { T = t0, Game = "valorant", Server = "NA", Avg = 40, P95 = 200, Loss = 0, Gw = 5 }, new GameHistory.Row { T = t0.AddMinutes(1), Game = "valorant", Server = "NA", Avg = 40, P95 = 45, Loss = 0, Gw = 4 } };
                var outs = new List<Outages.O> { new Outages.O { From = t0, To = t0.AddMinutes(12) } };
                var r = Weekly.Build(st, tr, gm, outs, t0.Date.AddDays(-6), t0);
                if (r.Parts.Count != 4 || r.Blame["wifi"] != 3 || r.Blame["provider"] != 2 || Weekly.TopBlame(r) != "wifi") throw new InvalidOperationException("weekly tally: " + string.Join(",", r.Blame.Select(kv => kv.Key + kv.Value)));
                if (r.Head.IndexOf("Wi-Fi", StringComparison.Ordinal) < 0 || r.Head.IndexOf("3 of 5", StringComparison.Ordinal) < 0) throw new InvalidOperationException("weekly headline: " + r.Head);
                if (Weekly.Text(r).Split('\n').Length < 5) throw new InvalidOperationException("weekly text");
                var quiet = Weekly.Build(new List<WatchKit.Session> { sess("stream", "Netflix", Array.Empty<string>()) }, new List<WatchKit.Session>(), new List<GameHistory.Row>(), new List<Outages.O>(), t0.Date.AddDays(-6), t0);
                if (quiet.Head.IndexOf("smooth", StringComparison.Ordinal) < 0 || quiet.Lv != 0) throw new InvalidOperationException("a quiet week: " + quiet.Head);
                if (Weekly.Build(new List<WatchKit.Session>(), new List<WatchKit.Session>(), new List<GameHistory.Row>(), new List<Outages.O>(), t0, t0).Parts.Count != 0) throw new InvalidOperationException("no records: no card");
                // every verdict has a lesson, and every lesson exists in Learn
                foreach (var bl in new[] { "pc", "wifi", "home", "provider", "far", "unsure" }) foreach (var mode in new[] { "stream", "trade" }) { var l = WatchKit.Lesson(bl, mode); if (Knowledge.Topic(l[0]) == null) throw new InvalidOperationException("lesson missing: " + l[0]); }
                // the first question
                string hid0 = Cfg.HiddenSecs; bool call0 = Cfg.Call;
                try
                {
                    Cfg.HiddenSecs = ""; FullView.ApplyUses(new HashSet<string> { "stream", "calls" }, null);
                    if (Cfg.Off("stream") || !Cfg.Off("trade") || !Cfg.Off("gamer") || !Cfg.Call) throw new InvalidOperationException("first question: " + Cfg.HiddenSecs);
                    Cfg.HiddenSecs = ""; FullView.ApplyUses(new HashSet<string>(), null); if (Cfg.HiddenSecs != "") throw new InvalidOperationException("nothing picked must hide nothing");
                    if (FullView.SuggestedPreset(new HashSet<string> { "game", "stream" }) != "gamer" || FullView.SuggestedPreset(new HashSet<string> { "check" }) != "penny" || FullView.SuggestedPreset(new HashSet<string> { "trade" }) != "balanced") throw new InvalidOperationException("suggested pace");
                }
                finally { Cfg.HiddenSecs = hid0; Cfg.Call = call0; }
                // the bug report keeps nothing personal
                string scr = BugReport.Scrub(@"open C:\Users\ahasa\Documents\x.txt on DESKTOP-ABC1 by ahasa: 192.168.1.20 fe80:0:0:0:1:2:3:4 aa:bb:cc:dd:ee:ff me@mail.com", "ahasa", "DESKTOP-ABC1");
                foreach (var leak in new[] { "ahasa", "DESKTOP-ABC1", "192.168", "aa:bb", "me@mail", "fe80" }) if (scr.IndexOf(leak, StringComparison.OrdinalIgnoreCase) >= 0) throw new InvalidOperationException("bug report leaks " + leak + ": " + scr);
                var bt = BugReport.Text("trade"); if (bt.IndexOf(Program.Version, StringComparison.Ordinal) < 0 || bt.IndexOf("Page: trade", StringComparison.Ordinal) < 0) throw new InvalidOperationException("bug report text");
            });
            T("views: switching keeps each view's own changes, yours are made and removed, junk is ignored", () =>
            {
                var st0 = Views.Current(); string view0 = Cfg.View, line0 = Views.Line();
                try
                {
                    Views.Load(""); Cfg.View = "";
                    if (!Views.Switch("gamer") || Cfg.View != "gamer" || Cfg.Off("gamer") || !Cfg.Off("trade") || Cfg.Preset != "gamer" || Cfg.GameBgSec != 10) throw new InvalidOperationException("gamer view: " + Views.Current().Line());
                    Cfg.SetOff("trade", false); Views.Remember();   // the gamer wants to learn trading: Trading on, in the Gamer view
                    if (!Cfg.Nav.StartsWith("home,gamer", StringComparison.Ordinal)) throw new InvalidOperationException("the Gamer view puts the Gamer Zone in the bar: " + Cfg.Nav);
                    foreach (var b in Views.BuiltIn) foreach (var sec in Cfg.Hideable) if (!Views.Default(b[0]).Hidden.Contains(sec) && Views.Default(b[0]).Nav.IndexOf(sec, StringComparison.Ordinal) < 0 && b[0] != "engineer") throw new InvalidOperationException(b[0] + " shows " + sec + " but not in its bar");
                    Views.Switch("engineer"); if (Cfg.Mode != "pro" || !Cfg.Details || Cfg.HiddenSecs != "") throw new InvalidOperationException("engineer view");
                    Views.Switch("gamer"); if (Cfg.Off("trade")) throw new InvalidOperationException("the Gamer view forgot Trading was turned on");
                    Views.Reset("gamer"); if (!Cfg.Off("trade")) throw new InvalidOperationException("reset the Gamer view");
                    Views.Switch("everyday"); if (Cfg.Preset != "penny" || !Cfg.Off("gamer") || Cfg.Mode != "simple") throw new InvalidOperationException("everyday view");
                    string mine = Views.Make("Gaming + trading"); if (mine == null || Cfg.View != mine || Views.NameOf(mine) != "Gaming + trading") throw new InvalidOperationException("make a view");
                    if (Views.Make("  ") != null) throw new InvalidOperationException("an empty name must not make a view");
                    string saved = Views.Line(); Views.Load(saved); if (Views.Line() != saved || !Views.All().Any(v => v[0] == mine)) throw new InvalidOperationException("views must survive a restart: " + saved);
                    Views.Delete(mine); if (Views.All().Any(v => v[0] == mine) || Cfg.View != "everyday") throw new InvalidOperationException("remove a view");
                    Views.Load("bad|x~y~z|gamer~G~pro/stream/penny/everyday/1/1/2/15/30/5/30/3|my-1~A~nope|../../x~B~simple//balanced/auto/0/1/2/15/30/5/30/3");
                    if (Views.All().Count != Views.BuiltIn.Length) throw new InvalidOperationException("junk views loaded: " + Views.Line());
                    var ps = Views.State.Parse("simple/stream+evil+trade/penny/hack/0/1/99999/15/30/5/30/3"); if (ps != null) throw new InvalidOperationException("an out-of-range pace must be refused");
                    ps = Views.State.Parse("simple/stream+evil+trade/penny/hack/0/1/2/15/30/5/30/3"); if (ps == null || ps.Hidden != "stream,trade" || ps.Widget != "auto") throw new InvalidOperationException("view state not cleaned: " + (ps == null ? "null" : ps.Line()));
                    if (Views.FromUses(new[] { "game" }) != "gamer" || Views.FromUses(new[] { "check", "calls" }) != "everyday" || Views.FromUses(new[] { "game", "trade" }) != "play-trade" || Views.FromUses(new[] { "game", "stream", "trade" }) != "everything" || Views.FromUses(new[] { "fix" }) != "engineer") throw new InvalidOperationException("view from the first question");
                    foreach (var b in Views.BuiltIn) if (Views.Default(b[0]) == null) throw new InvalidOperationException("no defaults for " + b[0]);
                }
                finally { Views.Load(line0); Cfg.View = view0; Views.Put(st0); Policy.Apply(); }
            });
            T("moments in the corner, the quiet corner, sending a report without an account", () =>
            {
                string c0 = Cfg.Corner;
                try
                {
                    Moments.Reset(); Cfg.Corner = Moments.Quiet;
                    Moments.StreamEnded("Netflix", 90, 0); if (Moments.Face != null) throw new InvalidOperationException("the quiet corner showed a moment");
                    Cfg.Corner = Moments.WithMoments;
                    Moments.StreamEnded("Netflix", 90, 2); if (Moments.Face != null) throw new InvalidOperationException("a stream with dips is no moment");
                    Moments.StreamEnded("Netflix", 90, 0); if (Moments.Face != "1f60c" || (Moments.Line ?? "").IndexOf("Netflix", StringComparison.Ordinal) < 0) throw new InvalidOperationException("a clean stream: " + Moments.Line);
                    if (Moments.Fire("stream", "1f60c", "again")) throw new InvalidOperationException("once a day per kind");
                    var calm = new Snap { Level = 0 }; var red = new Snap { Level = 2 }; var call = new Snap { Level = 0, Call = true };
                    if (Moments.FaceFor(calm) == null || Moments.FaceFor(red) != null || Moments.FaceFor(call) != null) throw new InvalidOperationException("never during a problem or a call");
                    if (Mood.Quip(calm).IndexOf("Netflix", StringComparison.Ordinal) < 0) throw new InvalidOperationException("the hover line says why");
                    Moments.Reset(); Moments.GameEnded("Valorant", DateTime.Now.AddMinutes(-40), DateTime.Now, 1); if (Moments.Face != "1f60e") throw new InvalidOperationException("a clean game session");
                    Moments.Reset(); Moments.GameEnded("Valorant", DateTime.Now.AddMinutes(-40), DateTime.Now, 9); if (Moments.Face != null) throw new InvalidOperationException("a laggy session is no moment");
                    Moments.Reset(); Moments.Back(TimeSpan.FromSeconds(20)); if (Moments.Face != null) throw new InvalidOperationException("a 20-second blip is no moment");
                    foreach (var e in new[] { "1f60e", "1f60c", "1f607", "1f92f", "1f973", "1f929" }) using (var st = typeof(Draw).Assembly.GetManifestResourceStream("AdeeboMeter.emoji." + e + ".png")) if (st == null) throw new InvalidOperationException("moment face missing: " + e);
                    // Follows my view only: the view's words in the corner
                    Cfg.Corner = Moments.Quiet; if (Views.ChipLine() != null) throw new InvalidOperationException("view words in a quiet corner");
                }
                finally { Cfg.Corner = c0; Moments.Reset(); }
                // the report: JSON escaped, the answer read, our website allowed, GitHub's issue pages no longer opened from the app
                string js = Relay.Json("bug", "a \"quote\" and \\ slash", "line1\nline2\u0001", null);
                if (js.IndexOf("\\\"quote\\\"", StringComparison.Ordinal) < 0 || js.IndexOf("line1\\nline2\"", StringComparison.Ordinal) < 0 || js.IndexOf("\"from\":\"app\"", StringComparison.Ordinal) < 0) throw new InvalidOperationException("report JSON: " + js);
                string hj = Relay.Json("hof", "", "", new Dictionary<string, string> { { "name", "Ada" }, { "code", "ADB-1" } }); if (hj.IndexOf("\"card\":{\"name\":\"Ada\",\"code\":\"ADB-1\"}", StringComparison.Ordinal) < 0) throw new InvalidOperationException("card JSON: " + hj);
                var ok = Relay.Parse("{\"ok\":true,\"number\":42}"); var no = Relay.Parse("{\"ok\":false,\"why\":\"too long\"}");
                if (!ok.Ok || ok.Number != 42 || no.Ok || no.Why != "too long" || Relay.Parse("<html>").Ok) throw new InvalidOperationException("relay answers");
                if (!Crash.Allowed("https://adeebometer.com/#feedback") || Crash.Allowed("https://adeebometer.com.evil.example/")) throw new InvalidOperationException("our website");
                if (BugReport.Tech("trade").IndexOf("Page: trade", StringComparison.Ordinal) < 0 || BugReport.Tech("x").IndexOf("What happened", StringComparison.Ordinal) >= 0) throw new InvalidOperationException("the technical part");
            });
            T("speed widgets and full checkup: every style draws, the strip only changes with its numbers, the checkup reports every layer", () =>
            {
                var bad = new List<string>(); var sn = new Snap { Down = 12.43, Up = 1.07, DownHist = new[] { 1.0, 5, 12, 8 }, UpHist = new[] { 0.5, 1, 1.2, 0.9 }, TodayDown = 2.4e9, TodayUp = 3e8, TopCpu = new List<AppStat> { new AppStat { Name = "chrome", Cpu = 12 } } };
                foreach (var st in new[] { "speed", "speedstatus", "speedapps", "everyday", "gamer", "calls", "engineer", "auto" })
                    foreach (bool card in new[] { false, true })
                        using (var b = Draw.Preview(sn, st, card)) if (b == null || b.Width < 50 || b.Height < 20) bad.Add(st + (card ? " card" : " strip"));
                string u0 = Cfg.WUnits, w0 = Cfg.WidgetStyle;
                try
                {
                    Cfg.WidgetStyle = "speed"; Cfg.WUnits = "bits"; string k1 = Draw.ChipKey(sn); sn.Headline = "something else"; if (Draw.ChipKey(sn) != k1) bad.Add("the speed strip redraws for words it doesn't show");
                    sn.Down = 12.44; if (Draw.ChipKey(sn) != k1) bad.Add("the strip redraws for a digit it doesn't show"); sn.Down = 13.9; if (Draw.ChipKey(sn) == k1) bad.Add("the strip didn't change with its number");
                    if (Draw.Rate(0.85, true) != (0.85 * 1000).ToString("0", CultureInfo.CurrentCulture) + " kb/s" || Draw.Rate(940, true) != "940 Mb/s") bad.Add("rates in bits: " + Draw.Rate(0.85, true));
                    if (Draw.Rate(0, true) != "0 kb/s" || Draw.Pair(0.85, 0.12) != "↓ " + (850).ToString("0", CultureInfo.CurrentCulture) + "  ↑ " + (120).ToString("0", CultureInfo.CurrentCulture) + " kb/s" || !Draw.Pair(12, 0.5).Contains("Mb/s") || !Draw.Pair(12, 0.5).Contains("kb/s")) bad.Add("quiet line / pair: " + Draw.Rate(0, true) + " · " + Draw.Pair(0.85, 0.12));   // 21 Nov: never "0.0 Mb/s" for a quiet line
                    Cfg.WUnits = "bytes"; if (Draw.Rate(100, true) != (12.5).ToString("0", CultureInfo.CurrentCulture) + " MB/s") bad.Add("rates in bytes: " + Draw.Rate(100, true));
                }
                finally { Cfg.WUnits = u0; Cfg.WidgetStyle = w0; }
                var r = new GuideRun(); bool sp = Investigate.CheckupSpeed; Investigate.CheckupSpeed = false;
                try { Investigate.Run("full", M, r, delegate { }); } finally { Investigate.CheckupSpeed = sp; }
                var obs = r.ObservedCopy(); sb.AppendLine("      full checkup: " + obs.Count + " facts, verdict level " + r.Lv + ": " + Draw.Trunc(r.Finding, 90));
                if (!r.Done || r.Finding == "" || !obs.Any(o => o[2].StartsWith("1. This PC", StringComparison.Ordinal)) || !obs.Any(o => o[2].StartsWith("3. ", StringComparison.Ordinal)) || !r.NotEst.Any(n => n.Contains("speed test"))) bad.Add("full checkup layers missing: " + string.Join(" | ", obs.Select(o => o[2]).Take(6)));
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("themes: every theme's text is readable and green / amber / red keep their meaning", () =>
            {
                var bad = new List<string>(); int f0 = Program.ForceTheme;
                Func<Color, double> lum = c => { Func<double, double> ch = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }; return 0.2126 * ch(c.R) + 0.7152 * ch(c.G) + 0.0722 * ch(c.B); };
                Func<Color, Color, double> cr = (a, b) => { double l1 = lum(a), l2 = lum(b); return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05); };
                try
                {
                    for (int t = 0; t < 2 + Theme.Palettes.Length; t++)
                    {
                        Program.ForceTheme = t; Theme.Apply(Program.WantDark()); string n = t == 0 ? "Light" : t == 1 ? "Dark" : Theme.PaletteNames[t - 2];
                        if (t >= 2 && Theme.Palette != Theme.Palettes[t - 2]) bad.Add(n + " didn't apply");
                        if (cr(Theme.Ink, Theme.Card) < 7) bad.Add(n + ": text on cards " + cr(Theme.Ink, Theme.Card).ToString("0.0", CultureInfo.InvariantCulture));
                        if (cr(Theme.Ink, Theme.Bg) < 7) bad.Add(n + ": text on the page");
                        if (cr(Theme.Muted, Theme.Card) < 4.5) bad.Add(n + ": grey text " + cr(Theme.Muted, Theme.Card).ToString("0.0", CultureInfo.InvariantCulture));
                        for (int l = 0; l < 3; l++) if (cr(Theme.LvText(l), Theme.LvSoft(l)) < 4.5) bad.Add(n + ": level " + l + " text " + cr(Theme.LvText(l), Theme.LvSoft(l)).ToString("0.0", CultureInfo.InvariantCulture));
                        var gr = Theme.LvText(0); var rd = Theme.LvText(2); if (!(gr.G > gr.R && rd.R > rd.G)) bad.Add(n + ": green or red changed meaning");
                        if (cr(Theme.OnBlue, Theme.Blue) < 2.5) bad.Add(n + ": button text " + cr(Theme.OnBlue, Theme.Blue).ToString("0.0", CultureInfo.InvariantCulture));
                    }
                }
                finally { Program.ForceTheme = f0; Theme.Apply(Program.WantDark()); }
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("calculators 1.1.5: NR, LTE, Wi-Fi, link budget, RAID, SLA, jitter buffer give the textbook numbers", () =>
            {
                Func<List<string[]>, string> v0 = r => r.Count > 0 ? r[0][2] : "";
                var want = new Dictionary<string, string> {
                    { "NR", v0(Calc.Cellular("NR", "100 MHz", "4x4 256QAM", "30")) + "|2337 Mbps" }, { "LTE", v0(Calc.Cellular("LTE", "20 MHz", "2x2 64QAM", "")) + "|151 Mbps" },
                    { "Wi-Fi 6", v0(Calc.WifiRate("6", "80", "2")) + "|1201 Mbps" }, { "Wi-Fi 5", v0(Calc.WifiRate("ac", "80", "1")) + "|433 Mbps" }, { "Wi-Fi 7", v0(Calc.WifiRate("7", "320", "1")) + "|2882 Mbps" },
                    { "wildcard", v0(Calc.Wildcard("0.0.3.255")) + "|255.255.252.0" }, { "SLA", Calc.Sla("99.9%")[1][2] + "|8.8 h" }, { "jitter", v0(Calc.JitterBuf("20-70 ms", "20", "")).Split(' ')[0] + "|80" },
                    { "E1", v0(Calc.Tdm("E1", "30")).Split(' ')[0] + "|" + (1920).ToString("N0", CultureInfo.CurrentCulture) },
                    { "v6", v0(Calc.V6Split("2001:db8:abcd::/48", "/56")).Split(' ')[0] + "|256" }, { "units", Calc.Units("100 Mbps")[1][2] + "|12.5 MB/s".Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator) } };
                var bad = want.Where(kv => { var p = kv.Value.Split('|'); return p[0] != p[1]; }).Select(kv => kv.Key + " gave " + kv.Value.Split('|')[0]).ToList();
                if (!Calc.Raid("6", "6", "4 TB")[0][2].StartsWith("16 TB", StringComparison.Ordinal)) bad.Add("RAID 6");
                if (Calc.Raid("10", "5", "4 TB")[0][0] != "2" || Calc.Raid("5", "2", "4 TB")[0][0] != "2") bad.Add("RAID refusals");
                if (!Calc.LinkBudget("20 dBm", "23 and 23", "5 km at 5.8 GHz", "-80")[0][2].StartsWith("-55", StringComparison.Ordinal)) bad.Add("link budget");
                if (Calc.Ups("1500 VA", "300 W", "")[0][0] != "2") bad.Add("UPS from VA alone should ask for Wh");
                if (Calc.Plan("300 Mbps", "", "2 4k, 1 call")[0][0] != "0" || Calc.Plan("10 Mbps", "", "2 4k")[0][0] != "2") bad.Add("plan");
                foreach (var junk in new[] { "", "zzz", "-1", "999999999999999999999", "1e400", "/", "::" })
                {
                    Calc.Wildcard(junk); Calc.V6Split(junk, junk); Calc.Bdp(junk, junk, junk); Calc.Units(junk); Calc.Cellular(junk, junk, junk, junk); Calc.WifiRate(junk, junk, junk); Calc.LinkBudget(junk, junk, junk, junk);
                    Calc.Raid(junk, junk, junk); Calc.Backup(junk, junk, junk); Calc.Ups(junk, junk, junk); Calc.Sla(junk); Calc.Growth(junk, junk, junk); Calc.Stream(junk, junk, junk); Calc.DataUse(junk, junk, junk); Calc.Plan(junk, junk, junk); Calc.Tdm(junk, junk); Calc.JitterBuf(junk, junk, junk);
                }
                if (bad.Count > 0) throw new InvalidOperationException(string.Join("; ", bad));
            });
            T("wave 4: UDP MTU packets, neighbour filter, speed grade, mode setting", () =>
            {
                foreach (int n in new[] { 100, 572, 1200, 1472 }) { var d = UdpMtu.DnsPad(n); if (d.Length != n) throw new InvalidOperationException("DNS pad " + n + " -> " + d.Length); if (!UdpMtu.IsDns(new byte[] { d[0], d[1], 0x81, 0x80, 0, 1, 0, 0, 0, 0, 0, 0 }, d)) throw new InvalidOperationException("DNS reply match"); }
                var q = UdpMtu.Quic(1200); if (q.Length != 1200 || (q[0] & 0xC0) != 0xC0 || q[5] != 8) throw new InvalidOperationException("QUIC probe");
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
                    // two files (next to the exe and in ProgramData): combined, the strictest wins; "pro" is never a policy
                    Policy.Reset(); Policy.Parse(new[] { "history_days = 5", "mode = pro" }); Policy.Parse(new[] { "speed_test = off", "history_days = 3" });
                    if (!Policy.NoSpeed || Policy.MaxHistoryDays != 3 || Policy.Mode != "") throw new InvalidOperationException("two policy files");
                    var sp = SpeedTest.Run("check", new Toolkit.Run()); if (sp.Count != 1 || sp[0][2].IndexOf("IT team", StringComparison.Ordinal) < 0) throw new InvalidOperationException("speed check ran under the policy");
                    // the user's own value comes back when the policy goes
                    int keepNet = Cfg.NetSec; Cfg.NetSec = 60; Policy.Reset(); Policy.NoInternetCheck = true; Policy.Apply();
                    bool kept = Cfg.Lines().Any(l => l == "net=60") && Cfg.NetSec == 0; Cfg.NetSec = keepNet; if (!kept) throw new InvalidOperationException("the policy overwrote the saved setting");
                }
                finally { Policy.Reset(); }
                var r = new GuideRun(); r.Obs("m", 1, "Router \"ping\"", "12 ms\n"); r.Conclude(1, "Wi-Fi is weak");
                string j = Report.Json(new Monitor(), r, 1);
                var o = Json.Parse(j) as Dictionary<string, object>;
                if (o == null || Convert.ToString(o["verdict"], CultureInfo.InvariantCulture) != "warning" || !(o["observed"] is List<object>)) throw new InvalidOperationException("report JSON: " + j);
            });
            T("side-by-side runs: line round-trip, junk refused, verdict", () =>
            {
                var a = new GameCompare.Saved { Game = "valorant", Name = "Wi-Fi", T = new DateTime(2026, 11, 11, 21, 0, 0) }; a.Rows.Add(new GameCompare.RunRow { Server = "NA East", Avg = 40, P95 = 70, Jit = 12, Loss = 2 });
                var b = new GameCompare.Saved { Game = "valorant", Name = "Cable", T = a.T }; b.Rows.Add(new GameCompare.RunRow { Server = "NA East", Avg = 31, P95 = 35, Jit = 2, Loss = 0 });
                var back = GameCompare.ParseLine(GameCompare.Line(a)); if (back == null || back.Rows[0].Avg != 40 || back.Name != "Wi-Fi") throw new InvalidOperationException("round trip");
                if (GameCompare.ParseLine("run|../x|a|202611112100|s=1/2/3/4") != null || GameCompare.ParseLine("hello") != null) throw new InvalidOperationException("junk accepted");
                string v = GameCompare.Verdict(a, b); if (!v.Contains("Cable loses fewer") || !v.Contains("Cable is steadier")) throw new InvalidOperationException(v);
            });
            T("engineer pack: capture replays find the classic failures", () =>
            {
                Func<string, List<string[]>> lab = n => Replay.Lab(n);
                Func<List<string[]>, string, bool> has = (rows, w) => rows.Any(r => (r[1] + " " + r[2] + " " + r[3]).IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
                var good = lab("dhcp-good"); if (!has(good, "192.0.2.50 from 192.0.2.1") || has(good, "Two DHCP servers")) throw new InvalidOperationException("good lease");
                if (!has(lab("dhcp-two-servers"), "Two DHCP servers answered")) throw new InvalidOperationException("rogue DHCP not found");
                if (!has(lab("dhcp-no-answer"), "No offer")) throw new InvalidOperationException("no-answer not found");
                if (!has(lab("dhcp-nak"), "Refused (NAK)")) throw new InvalidOperationException("NAK not found");
                var ra = lab("ra-rogue"); if (!has(ra, "More than one router") || !has(ra, "2001:db8:1::/64")) throw new InvalidOperationException("rogue RA");
                var ic = lab("icmp-errors"); if (!has(ic, "MTU 1400") || !has(ic, "Port unreachable") || !has(ic, "TCP to 198.51.100.10:443")) throw new InvalidOperationException("ICMP: " + string.Join(" / ", ic.Select(r => r[2])));
                if (!Replay.Explain(true, 2, 0).StartsWith("Packet too big", StringComparison.Ordinal)) throw new InvalidOperationException("ICMPv6 2/0");
                if (!has(lab("dhcp-no-ack"), "No ACK for")) throw new InvalidOperationException("missing ACK not found");
                var ef = lab("eap-failure"); if (!has(ef, "refused after starting PEAP") || !has(ef, "host/LAPTOP-ADEEB")) throw new InvalidOperationException("EAP failure: " + string.Join(" / ", ef.Select(r => r[2])));
                if (!has(lab("eap-good"), "The sign-in worked")) throw new InvalidOperationException("EAP success");
                var ce = lab("cert-expired-intermediate"); if (!has(ce, "EXPIRED") || !has(ce, "3 certificates")) throw new InvalidOperationException("expired intermediate: " + string.Join(" / ", ce.Select(r => r[2])));
                if (Pcap.Parse(new byte[] { 1, 2, 3 }).Count != 0) throw new InvalidOperationException("junk read as packets");
                // the same lease saved by Wireshark (tshark -F pcapng): AdeeboMeter's reader must handle pcapng blocks too
                byte[] ng; using (var z = new System.IO.Compression.GZipStream(new MemoryStream(Convert.FromBase64String("H4sIAPkx9WoC/+Pi5eXKYGBg8LXRlmIE0v+hgIXBkSEkOCOxKFtBIzyzKLUYxNRUMNEz0jNS0HDPLFEog7ALEpOzE9NTUxQSiyGyuoZ6hkmlmTkpxpoMEAAyH2S2CJT+/x/CZgNiH0aImkhnIM/DTUUHyAdhiCuYGOIEGBg5GFwZGOVAqhwEKy8wQF3J4MLgzMgFNJGRjSFK/MA9BiwAop9h2IDkpuBkU0ZGHh4fx4AQ/wBdRxdXV6f/oDAEhWUMUlguCHdTsQHybZDDUgEalnqQsNy+7wADEyM4LJ0ZXBilgOGFFpZAeaPhHZZMZiygQGBkAYUCM5jNBiaNWRgYAxv+x0DDNgIpbAvi3VQsgHwLbOlUC5pOj6CkU7GRmE6ZIWFrBCKN0NJsBJY0q5E9mmaJCVdWItIsAG9BjrvYBQAA")), System.IO.Compression.CompressionMode.Decompress)) using (var o2 = new MemoryStream()) { z.CopyTo(o2); ng = o2.ToArray(); }
                var ngp = Pcap.Parse(ng); if (ngp.Count != 4 || !has(Replay.DhcpRows(ngp, new List<string[]>()), "192.0.2.50 from 192.0.2.1")) throw new InvalidOperationException("pcapng: " + ngp.Count + " packets");
            });
            T("engineer pack: RADIUS password hiding, certificate verdicts", () =>
            {
                // a self-signed root "CN=Proxy, O=DigiCert Inc" (made with openssl for this test): borrowing a CA's name isn't enough
                using (var fake = new System.Security.Cryptography.X509Certificates.X509Certificate2(Convert.FromBase64String("MIIBojCCAUmgAwIBAgIUczkokrF21nSiSTwdKqzk9oKitU0wCgYIKoZIzj0EAwIwJzEOMAwGA1UEAwwFUHJveHkxFTATBgNVBAoMDERpZ2lDZXJ0IEluYzAeFw0yNjExMTIyMTQ1NTRaFw0zNjExMDkyMTQ1NTRaMCcxDjAMBgNVBAMMBVByb3h5MRUwEwYDVQQKDAxEaWdpQ2VydCBJbmMwWTATBgcqhkjOPQIBBggqhkjOPQMBBwNCAAS0PWTXiCZJEplkuK/1l8pPwCcUfegjodLEqbwMXqU45y1eEHBYpxlaMmVogPCqQ2uch+vXNyhAE/8I54TZng2Eo1MwUTAdBgNVHQ4EFgQU75K4CLLqAdvJaWr/2H0n2hQsNP8wHwYDVR0jBBgwFoAU75K4CLLqAdvJaWr/2H0n2hQsNP8wDwYDVR0TAQH/BAUwAwEB/zAKBggqhkjOPQQDAgNHADBEAiBKWHWXXk1gS7o7L8vPIVTdzmIMje5LTTd6tzF36cnkjgIgC/IyYOEIICxPoxmxAdzCX3gwBwLTXGp4tvYMUJGSNeE=")))
                    if (Inspection.IsWellKnown(fake)) throw new InvalidOperationException("a root that borrows a CA's name counted as public");
                // and real ones do: ISRG Root X1 (Mozilla's list), Microsoft Root Certificate Authority 2011 (Windows' own); the list is whole
                if (!Inspection.PublicThumb("CABD2A79A1076A31F21D253635CB039D4329A5E8") || !Inspection.PublicThumb("8F43288AD272F3103B6FB1428485EA3014C0BCFE") || Inspection.PublicCount < 120) throw new InvalidOperationException("public root list: " + Inspection.PublicCount);
                byte[] pw = Encoding.UTF8.GetBytes("test-password-123"), sec = Encoding.UTF8.GetBytes("s3cret"), ra = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
                var c = EngPack.HidePassword(pw, sec, ra); if (c.Length != 32) throw new InvalidOperationException("length " + c.Length);
                {
                    var b1 = Md5(sec.Concat(ra).ToArray()); var b2 = Md5(sec.Concat(c.Take(16)).ToArray());
                    var back = c.Select((x, i) => (byte)(x ^ (i < 16 ? b1[i] : b2[i - 16]))).ToArray();
                    if (Encoding.UTF8.GetString(back).TrimEnd('\0') != "test-password-123") throw new InvalidOperationException("RFC 2865 hiding doesn't round-trip");
                }
                var a = new EngPack.CertSnap { Name = "VPN off", Trust = "trusted by Windows" }; a.Chain.Add(new[] { "site", "DigiCert", "AA", "2027-01-01" });
                var b = new EngPack.CertSnap { Name = "VPN on", Trust = "trusted by Windows" }; b.Chain.Add(new[] { "site", "Zscaler Intermediate", "BB", "2027-01-01" });
                if (EngPack.CertVerdict(a, b)[1] != "Different issuer" || EngPack.CertVerdict(a, a)[1] != "Same certificate") throw new InvalidOperationException("cert verdict");
                if (EngPack.ParseCert(EngPack.CertLine(new EngPack.CertSnap { Host = "x.example.com", Name = "n", T = DateTime.Now, Trust = "t", Chain = a.Chain })) == null) throw new InvalidOperationException("cert line");
            });
            T("languages: embedded previews load and English stays the fallback", () =>
            {
                var fr = L.Parse(Knowledge.RawText("lang-fr"));
                if (fr.Count < 250) throw new InvalidOperationException("French table has only " + fr.Count + " lines");
                if (L.LastDupes.Count > 0) throw new InvalidOperationException("French table names a line twice: " + string.Join(" | ", L.LastDupes.Take(3)));
                foreach (var kv in fr)
                {
                    if (kv.Value.Trim().Length == 0) throw new InvalidOperationException("empty translation: " + kv.Key);
                    // numbers and placeholders must survive translation (a "20 pings" that becomes "pings" is a wrong translation)
                    var nums = System.Text.RegularExpressions.Regex.Matches(kv.Key, @"\d+").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value);
                    foreach (var n in nums) if (kv.Value.IndexOf(n, StringComparison.Ordinal) < 0) throw new InvalidOperationException("number " + n + " lost in: " + kv.Value);
                }
                var expected = new Dictionary<string, string> {
                    { "es", "Ajustes" }, { "ar", "الإعدادات" }, { "bn", "সেটিংস" }
                };
                string old = L.Code;
                try
                {
                    foreach (var lang in expected)
                    {
                        L.Use(lang.Key);
                        if (L.T("Settings") != lang.Value || L.Count == 0 || L.IsRtl != (lang.Key == "ar"))
                            throw new InvalidOperationException("language lookup: " + lang.Key);
                        if (L.T("not in the table at all") != "not in the table at all")
                            throw new InvalidOperationException("fallback: " + lang.Key);
                    }
                    if (!L.RunsRtl(expected["ar"]) || L.RunsRtl("IPv6"))
                        throw new InvalidOperationException("Arabic text direction");
                }
                finally { L.Use(old); }
                try { L.Use("fr"); if (L.T("Settings") != "Paramètres" || L.T("not in the table at all") != "not in the table at all") throw new InvalidOperationException("lookup"); L.Use("xx"); if (L.Code != "en" || L.T("Settings") != "Settings") throw new InvalidOperationException("unknown language must fall back to English"); }
                finally { L.Use(old); }
            });
            T(".adeebo: round trip, wrong code refused, a changed byte refused, safe names", () =>
            {
                var data = Enumerable.Range(0, 100000).Select(i => (byte)(i * 7)).ToArray(); string code = FileCrypt.NewCode();
                if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^([2-9A-HJKMNP-Z]{4}-){5}[2-9A-HJKMNP-Z]{4}$")) throw new InvalidOperationException("code shape " + code);
                byte[] enc; using (var o = new MemoryStream()) { FileCrypt.EncryptCode(new MemoryStream(data), "report.pdf", o, code, 100000); enc = o.ToArray(); }
                using (var o = new MemoryStream()) { string nm = FileCrypt.DecryptCode(new MemoryStream(enc), o, code.ToLowerInvariant().Replace("-", " ")); if (nm != "report.pdf" || !o.ToArray().SequenceEqual(data)) throw new InvalidOperationException("round trip"); }
                Func<byte[], string, bool> refused = (b, c) => { try { FileCrypt.DecryptCode(new MemoryStream(b), new MemoryStream(), c); return false; } catch (System.Security.Cryptography.CryptographicException) { return true; } };
                if (!refused(enc, FileCrypt.NewCode())) throw new InvalidOperationException("wrong code accepted");
                var bad = (byte[])enc.Clone(); bad[bad.Length / 2] ^= 1; if (!refused(bad, code)) throw new InvalidOperationException("changed file accepted");
                var cut = enc.Take(enc.Length - 40).ToArray(); if (!refused(cut, code)) throw new InvalidOperationException("cut file accepted");
                string dir = Path.GetTempPath(); var fn = FileCrypt.FreeName(dir, "..\\..\\evil.exe"); if (Path.GetDirectoryName(fn) != dir.TrimEnd('\\') || !fn.EndsWith("evil.exe", StringComparison.Ordinal)) throw new InvalidOperationException("name escapes the folder: " + fn);
            });
            T(".adeebo for P-384 recipients: round trip, wrong key refused, key backup restores", () =>
            {
                if (!EcKeys.Available) return;   // Windows' key storage (CNG) - not in the Linux test lab; runs on a real PC
                EcKeys.Key a = null, b = null, rest = null;
                try
                {
                    a = EcKeys.Create("selftest-a " + Guid.NewGuid().ToString("N").Substring(0, 6), null); b = EcKeys.Create("selftest-b " + Guid.NewGuid().ToString("N").Substring(0, 6), null);
                    var data = Enumerable.Range(0, 50000).Select(i => (byte)(i * 13)).ToArray(); byte[] enc;
                    using (var o = new MemoryStream()) { EcKeys.Encrypt(new MemoryStream(data), "plan.docx", o, new[] { Convert.FromBase64String(a.Pub) }); enc = o.ToArray(); }
                    using (var o = new MemoryStream()) { string used; string nm = EcKeys.Decrypt(new MemoryStream(enc), o, out used); if (nm != "plan.docx" || !o.ToArray().SequenceEqual(data) || used != a.Name) throw new InvalidOperationException("EC round trip"); }
                    var bad = (byte[])enc.Clone(); bad[bad.Length - 40] ^= 1; bool refused = false; try { string u; EcKeys.Decrypt(new MemoryStream(bad), new MemoryStream(), out u); } catch (System.Security.Cryptography.CryptographicException) { refused = true; } if (!refused) throw new InvalidOperationException("changed EC file accepted");
                    string code = FileCrypt.NewCode(); var backup = EcKeys.BackupBytes(a, code); EcKeys.Delete(a);
                    rest = EcKeys.Restore(new MemoryStream(backup), code);
                    using (var o = new MemoryStream()) { string used; EcKeys.Decrypt(new MemoryStream(enc), o, out used); if (!o.ToArray().SequenceEqual(data)) throw new InvalidOperationException("restored key can't open the file"); }
                }
                finally { foreach (var k in new[] { a, b, rest }) if (k != null) EcKeys.Delete(k); }
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
                    "travel.LADDER", "travel.ANATOMY", "travel.JOURNEY", "travel.JTIME", "travel.JREAL", "travel.JSAY", "travel.JEXPLAIN", "travel.WINS", "travel.ROUTES", "travel.ADDR", "travel.FAMILIES", "travel.ROLES", "travel.FALLBACK", "travel.CALLFLOW", "travel.OUTLOOK", "travel.REAL", "travel.DECODE", "travel.CHAT", "travel.CHATNOTES", "kind.base", "kind.lan" };
                keys.AddRange(DestResult.Steps.Select(x => "step." + x));
                keys.AddRange(new[] { "m", "w", "p", "i", "u" }.Select(Evidence.Key)); keys.AddRange(Investigate.All.Select(f => "guide." + f.Id)); keys.AddRange(new[] { "replay.SLIDER", "ov.TRAFFIC", "ov.DETAILS", "ov.ONLINE", "ov.WEATHER", "link.WARS", "kind.pc", "path.Internet", "path.LAN", "path.DNS" }); keys.AddRange(new[] { "mtu", "mtr", "ret", "owners", "overlap", "ip46", "proxy", "hosts", "dnsrules", "port", "teams", "clock", "iperf", "ping", "cloud", "dig", "http", "sweep", "wol", "subnet", "stack", "vlsm", "bandwidth", "binhex", "timeline", "hotel", "dnsrace", "inventory", "drivers", "usb", "lag", "winupdate", "winev", "auth", "pressure", "disk", "space", "bg", "crash", "leaks", "gpu", "changed", "battery", "ping6", "tcpping", "twamp", "httptime", "doh", "ntp", "mail", "ssh", "snmp", "syslog", "radius", "tacacs", "hairpin", "dc", "gpo", "certs", "fw", "listen", "events", "timesrc", "route", "gateway", "v6routers", "rlisten", "arp", "snmpif", "snmptraffic", "snmpcpu", "snmplldp", "snmpsens", "snmpsnap", "snmpmac", "snmpvlan", "snmppoe", "snmplag", "snmpprn", "snmpups", "snmpwalk", "mibs", "icmpb", "udpsend", "tcpsend", "arpwho", "dhcpopt", "trap", "netflow", "httpreq", "stun", "nbname", "remote", "certfile", "pfx", "csr", "certconv", "hashf", "b64", "certcmds", "manyports", "allips", "rdpcheck", "ldapcheck", "dnscmp", "beforeafter", "twopc", "mysites", "shares", "svcfail", "tidy", "dhcp", "roams", "bloat", "dscp", "discover", "ispreport", "speedtest", "udpmtu", "vpn", "gamecompare", "join", "dot1x", "dhcpreplay", "rareplay", "eapreplay", "icmpexplain", "certcmp", "keys", "newkey", "csrmake", "selfsigned", "certmatch", "certlive", "recipients", "encrypt", "decrypt", "radiuslab", "smtppath", "samplelab" }.Select(x => "tb." + x)); keys.Add("card.IPV6");
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
                foreach (var t in topics) if (t["title"] == "" || t["kind"] == "" || t["line"] == "" || (t["ladder"] == "" && t["pic"] == "")) throw new InvalidOperationException("learn topic incomplete: " + t.Id);
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
            T("Learn games, the QR code and the over-time tools", () =>
            {
                foreach (var f in LearnGames.Faults)
                {
                    var room = LearnGames.NewRoom(7, f.Id); if (room.F.Id != f.Id || !room.FaultChoices.Contains(f.Name) || !room.FixChoices.Contains(f.Fix) || room.FixChoices.Distinct().Count() != 4) throw new InvalidOperationException("room " + f.Id);
                    foreach (var c in LearnGames.Commands) if (LearnGames.Run(room, c[0]) == "") throw new InvalidOperationException("room " + f.Id + " says nothing to " + c[0]);
                }
                foreach (var id in LearnGames.ThiefCases) { var c = LearnGames.NewCase(3, id); if (c.Id != id || c.Devices.Count(d => d.Thief) != 1 || c.Clues.Length < 3) throw new InvalidOperationException("thief case " + id); }
                foreach (var d in LearnGames.Detective) { int w = int.Parse(d[2], CultureInfo.InvariantCulture); if (w < 0 || w >= d[1].Split('~').Length || d[1].Split('~').Any(x => x.Split('|').Length != 3)) throw new InvalidOperationException("detective " + d[0]); }
                foreach (var k in new[] { "ports", "addr" }) { var q = LearnGames.NewQuick(k, 5); if (q.Q.Count != 10 || q.Q.Any(z => z.Length != 6 || !z.Skip(2).Contains(z[1]) || z.Skip(2).Distinct().Count() != 4)) throw new InvalidOperationException("quick-fire " + k); }
                var rnd = new Random(1); for (int i = 0; i < 200; i++) { string a, cls; LearnGames.Addr(rnd, out a, out cls); IPAddress ip; if (!IPAddress.TryParse(a, out ip)) throw new InvalidOperationException("address " + a); if (cls.StartsWith("private", StringComparison.Ordinal) != (IspReport.Private(ip) && !a.StartsWith("100.", StringComparison.Ordinal) && !a.StartsWith("169.254.", StringComparison.Ordinal) && !a.StartsWith("127.", StringComparison.Ordinal))) throw new InvalidOperationException("address kind " + a + " " + cls); }
                if (LearnGames.Keys().Any(k => !FullView.PtKeyKnown(k) || Points.Value(k) < 3)) throw new InvalidOperationException("game points not known");
                if (Qr.Encode("hi").GetLength(0) != 21 || Qr.Encode(new string('A', 250)).GetLength(0) != 57 || Qr.Encode(new string('z', 300)).GetLength(0) != 61 || Qr.Encode(new string('z', 900)).GetLength(0) != 101 || Qr.Encode(new string('z', 1732)).GetLength(0) != 137 || Qr.Encode(new string('z', 1733)) != null) throw new InvalidOperationException("QR sizes");
                var snap = OverTime.ReadSettings(); if (!snap.Keys.Any(k => k.StartsWith("adapter ", StringComparison.Ordinal) || k == "proxy")) throw new InvalidOperationException("settings snapshot read nothing");
                sb.AppendLine("      " + LearnGames.Keys().Count() + " game scenarios, " + snap.Count + " network settings read");
            });
            T("settings round trip + presets", () =>
            {
                Cfg.ApplyPreset("penny"); int penny = Cfg.JobsPerMinute(); Cfg.ApplyPreset("engineer"); int eng = Cfg.JobsPerMinute(); Cfg.ApplyPreset("balanced"); int bal = Cfg.JobsPerMinute();
                Cfg.ApplyPreset("gamer"); int gmr = Cfg.JobsPerMinute(); bool gOk = Cfg.GameBgSec == 10 && Cfg.GameBgServers == 5 && GamePing.MaxServers == 16 && GamePing.MaxPerSecond >= GamePing.MaxServers; Cfg.ApplyPreset("penny"); bool pOk = Cfg.GameBgSec == 60 && GamePing.MaxServers == 8 && GamePing.SurveySec == 1800; Cfg.ApplyPreset("balanced");
                if (!(penny * 3 < bal && bal == eng && gmr > bal) ||   /* "engineer" (merged) is Balanced; Gamer watches Wi-Fi more closely */ !(Cfg.GameJobsForPreset("gamer") > Cfg.GameJobsForPreset("engineer") && Cfg.GameJobsForPreset("penny") < Cfg.GameJobsForPreset("balanced")) || !gOk || !pOk) throw new InvalidOperationException("presets wrong: " + penny + "/" + bal + "/" + eng + "/" + gmr + " game " + gOk + "/" + pOk);
                Cfg.Read("preset", "gamer"); if (Cfg.Preset != "gamer") throw new InvalidOperationException("gamer preset not read"); Cfg.Read("preset", "engineer"); if (Cfg.Preset != "balanced") throw new InvalidOperationException("a saved Engineer should open as Balanced"); Cfg.Read("preset", "x"); if (Cfg.Preset != "balanced") throw new InvalidOperationException("odd preset kept");
                Cfg.Read("gw", "30"); Cfg.Read("net", "300"); if (Cfg.GwSec != 30 || Cfg.NetSec != 300) throw new InvalidOperationException("30 s / 5 min not accepted"); Cfg.ApplyPreset("balanced");
                Cfg.Pins.Clear(); Cfg.Read("pin", "github.com"); Cfg.Read("pin", "rm -rf /"); Cfg.Read("pin", "https://jira.example.com/x?a=b"); Cfg.Read("gw", "999"); Cfg.Read("fade", "128");
                if (Cfg.Pins.Count != 2 || Cfg.GwSec != 2 || Cfg.Fade != 128) throw new InvalidOperationException("settings read wrong: pins=" + Cfg.Pins.Count + " gw=" + Cfg.GwSec + " fade=" + Cfg.Fade);
                var lines = Cfg.Lines().ToList(); Cfg.Pins.Clear(); Cfg.Fade = 77;
                foreach (var l in lines) { int eq = l.IndexOf('='); Cfg.Read(l.Substring(0, eq), l.Substring(eq + 1)); }
                if (Cfg.Pins.Count != 2 || Cfg.Fade != 128) throw new InvalidOperationException("round trip lost data");
                Cfg.Pins.Clear(); Cfg.Fade = 77;
                sb.AppendLine("      jobs a minute: penny " + penny + ", balanced " + bal + ", engineer " + eng + ", gamer " + gmr);
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
                using (var hk = new HotKey(delegate { })) if (!hk.Ok) sb.AppendLine("      note: another app on this PC already uses Ctrl+Alt+A, so the open-the-window shortcut does nothing here (the corner widget and tray still open it)");   // the PC's setup, not a fault in the app
            });
            T("memory after everything", () =>
            {
                GC.Collect(); GC.WaitForPendingFinalizers();
                using (var p = Process.GetCurrentProcess()) { long mb = p.PrivateMemorySize64 / 1048576; sb.AppendLine("      private " + mb + " MB (80 MB in normal use; up to 120 here, where every page and tool has just been opened at once)"); if (mb > 120) throw new InvalidOperationException(mb + " MB"); }
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
                int gdi = Native.GetGuiResources(Native.GetCurrentProcess(), 0), user = Native.GetGuiResources(Native.GetCurrentProcess(), 1);
                using (var p = Process.GetCurrentProcess()) sb.AppendLine("      handles=" + p.HandleCount + " · GDI " + gdi + " · USER " + user);
                // Windows' own limit is 10,000 per process: well past 2,000 after this run means something is created and never freed
                if (!Program.IsWine && (gdi == 0 || user == 0)) throw new InvalidOperationException("couldn't read the GDI / USER counts");
                if (gdi > 2000 || user > 2000) throw new InvalidOperationException("GDI " + gdi + " / USER " + user + " - something isn't freed");
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
