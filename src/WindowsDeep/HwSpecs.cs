// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // ================================================================== what's inside this PC, and what each part should be able to do
    // Read once (cached 10 minutes), all without admin: the processor, the memory sticks, the disks and the link each one sits on,
    // the graphics cards (dedicated and borrowed memory), the network link, the battery, restarts and startup apps.
    // Every part gets a ceiling worked out from its specs - the measured number is then judged against its own ceiling, not a guess.
    static class HwSpecs
    {
        internal sealed class Disk { public string Name = "", Bus = "", Media = "", Letters = "", DeviceId = ""; public double Gb; public int Rpm = -1, Health = 5, LinkGen, LinkWidth, MaxGen, MaxWidth; public bool System; }
        internal sealed class Gpu { public string Name = ""; public double DedicatedMb, SharedMb; public bool Integrated, Software; public long Luid; public int LinkGen, LinkWidth, MaxGen, MaxWidth, Vendor; }
        internal sealed class Ram { public double Gb; public int Mts, RatedMts, Type; public string Slot = ""; }
        internal sealed class Specs
        {
            public string Cpu = "", Maker = "", Model = "", Bios = "", Nic = "", NicKind = "", Power = "";
            public int Cores, Threads, BaseMhz; public double RamGb; public int RamSticks, Channels = 1; public bool Soldered;
            public readonly List<Ram> Rams = new List<Ram>(); public readonly List<Disk> Disks = new List<Disk>(); public readonly List<Gpu> Gpus = new List<Gpu>();
            public double NicMbps = double.NaN; public DateTime Boot = DateTime.MinValue; public bool RestartPending; public readonly List<string> Startup = new List<string>();
            public DateTime At;
        }
        static Specs last; static readonly object k = new object();
        static DateTime nicAt;
        public static Specs Get() { lock (k) { if (last == null || (DateTime.Now - last.At).TotalMinutes > 10) { last = Read(); nicAt = DateTime.Now; } else if ((DateTime.Now - nicAt).TotalSeconds > 20) { ReadNic(last); nicAt = DateTime.Now; } return last; } }   // PC-036: Wi-Fi to cable shows at once

        static string S(object o) { return Convert.ToString(o, CultureInfo.InvariantCulture) ?? ""; }
        static double D(object o) { try { return o == null ? 0 : Convert.ToDouble(o, CultureInfo.InvariantCulture); } catch (FormatException) { return 0; } catch (InvalidCastException) { return 0; } catch (OverflowException) { return 0; } }
        static void Wmi(string scope, string q, Action<ManagementBaseObject> each)
        {
            try
            {
                using (var s = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(q), new EnumerationOptions { Timeout = TimeSpan.FromSeconds(8), ReturnImmediately = true, Rewindable = false }))
                    foreach (ManagementBaseObject o in s.Get()) using (o) each(o);
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("hw " + q.Split(' ').Skip(3).FirstOrDefault() + ": " + e.Message); }
        }

        static Specs Read()
        {
            var s = new Specs { At = DateTime.Now };
            Wmi(@"\\.\root\cimv2", "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor", o => { s.Cpu = Regex.Replace(S(o["Name"]).Trim(), @"\s+", " "); s.Cores += (int)D(o["NumberOfCores"]); s.Threads += (int)D(o["NumberOfLogicalProcessors"]); s.BaseMhz = Math.Max(s.BaseMhz, (int)D(o["MaxClockSpeed"])); });
            if (s.Threads == 0) s.Threads = Environment.ProcessorCount;
            Wmi(@"\\.\root\cimv2", "SELECT Manufacturer, Model FROM Win32_ComputerSystem", o => { s.Maker = S(o["Manufacturer"]).Trim(); s.Model = S(o["Model"]).Trim(); });
            Wmi(@"\\.\root\cimv2", "SELECT ReleaseDate FROM Win32_BIOS", o => { var r = S(o["ReleaseDate"]); if (r.Length >= 8) s.Bios = r.Substring(0, 4) + "-" + r.Substring(4, 2) + "-" + r.Substring(6, 2); });
            Wmi(@"\\.\root\cimv2", "SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, DeviceLocator, BankLabel, FormFactor FROM Win32_PhysicalMemory", o =>
            {
                var r = new Ram { Gb = D(o["Capacity"]) / 1073741824.0, RatedMts = (int)D(o["Speed"]), Mts = (int)D(o["ConfiguredClockSpeed"]), Type = (int)D(o["SMBIOSMemoryType"]), Slot = (S(o["BankLabel"]) + " " + S(o["DeviceLocator"])).Trim() };
                if (r.Mts <= 0) r.Mts = r.RatedMts; 
                // bug-hunt ML-163: soldered is chips on the board (CIM form factor 14 SMD, 21 BGA, 22 FPBGA), LPDDR (never a stick), or a slot
                // named onboard / soldered - 13 is SRIMM, an old removable module
                int ff = (int)D(o["FormFactor"]); s.Soldered |= ff == 14 || ff == 21 || ff == 22 || r.Type == 27 || r.Type == 28 || r.Type == 29 || r.Type == 30 || r.Type == 35 || Regex.IsMatch(r.Slot, "onboard|soldered|on board", RegexOptions.IgnoreCase);
                s.Rams.Add(r);
            });
            s.RamSticks = s.Rams.Count; s.RamGb = s.Rams.Sum(r => r.Gb);
            if (s.RamGb <= 0) { var ms = new MemStatus(); ms.Length = (uint)Marshal.SizeOf(typeof(MemStatus)); if (GlobalMemoryStatusEx(ref ms)) s.RamGb = ms.Total / 1073741824.0; }
            s.Channels = ChannelsOf(s.Rams);
            // disks: Windows' storage view (bus, media, spin speed) and which one holds C:
            // PC-032: a partition's DiskNumber is a disk (MSFT_Disk), which isn't always one physical disk (Storage Spaces, RAID).
            // The two are joined by serial number, and only when that is one-to-one; otherwise the letters stay unknown
            var sysDisk = new HashSet<string>(); var letters = new Dictionary<string, string>(); var serialOf = new Dictionary<string, string>(); var serialCount = new Dictionary<string, int>();
            Wmi(@"\\.\root\Microsoft\Windows\Storage", "SELECT Number, SerialNumber FROM MSFT_Disk", o => { string sn = S(o["SerialNumber"]).Trim(); if (sn == "") return; serialOf[S(o["Number"])] = sn; int c; serialCount.TryGetValue(sn, out c); serialCount[sn] = c + 1; });
            Wmi(@"\\.\root\Microsoft\Windows\Storage", "SELECT DiskNumber, DriveLetter FROM MSFT_Partition", o => { var dl = S(o["DriveLetter"]).Trim('\0', ' '); if (dl == "") return; string sn; if (!serialOf.TryGetValue(S(o["DiskNumber"]), out sn) || serialCount[sn] != 1) return; string old; letters.TryGetValue(sn, out old); letters[sn] = (old ?? "") + dl + ":"; if (string.Equals(dl, SystemLetter(), StringComparison.OrdinalIgnoreCase)) sysDisk.Add(sn); });
            Wmi(@"\\.\root\Microsoft\Windows\Storage", "SELECT DeviceId, SerialNumber, FriendlyName, BusType, MediaType, SpindleSpeed, Size, HealthStatus FROM MSFT_PhysicalDisk", o =>
            {
                var d = new Disk { DeviceId = S(o["DeviceId"]), Name = S(o["FriendlyName"]).Trim(), Bus = BusName((int)D(o["BusType"])), Gb = D(o["Size"]) / 1e9, Health = (int)D(o["HealthStatus"] ?? 5) };
                int mt = (int)D(o["MediaType"]); uint rpm = (uint)D(o["SpindleSpeed"]); d.Media = mt == 4 ? "SSD" : mt == 3 ? "HDD" : mt == 5 ? "SCM" : "";
                if (rpm > 0 && rpm < 30000) { d.Rpm = (int)rpm; d.Media = "HDD"; } else if (rpm == 0 && d.Media == "") d.Media = "SSD";
                if (d.Bus == "NVMe") d.Media = "SSD";
                string id = S(o["SerialNumber"]).Trim(); d.System = id != "" && sysDisk.Contains(id); string l; if (id != "" && letters.TryGetValue(id, out l)) d.Letters = l;
                s.Disks.Add(d);
            });
            // graphics cards: DXGI says how much memory each has of its own and how much it may borrow from the PC
            foreach (var g in Dxgi()) s.Gpus.Add(g);
            // PCIe links: what each card and NVMe drive runs at now, and the most it can do (SetupAPI, no admin)
            var links = PciLinks(); var nv = links.Where(x => x.Item1 == 1 && x.Item2 == 8).ToList(); var vids = links.Where(x => x.Item1 == 3).ToList();
            var nvDisks = s.Disks.Where(d => d.Bus == "NVMe").ToList();
            // PC-033/034: Windows lists drives and PCI devices in its own orders, so a link is only given to a part when there's
            // exactly one of its kind on each side - two NVMe drives or two cards from one maker keep "link not readable"
            if (nvDisks.Count == 1 && nv.Count == 1) { var l = nv[0].Item3; nvDisks[0].LinkGen = l[0]; nvDisks[0].LinkWidth = l[1]; nvDisks[0].MaxGen = l[2]; nvDisks[0].MaxWidth = l[3]; }
            foreach (var g in s.Gpus.Where(g => !g.Software))
            {
                if (s.Gpus.Count(x => !x.Software && x.Vendor == g.Vendor) != 1 || vids.Count(v => v.Item5 == g.Vendor) != 1) continue;
                var m = vids.FirstOrDefault(v => v.Item5 == g.Vendor); if (m == null) continue;
                vids.Remove(m); var l = m.Item3; g.LinkGen = l[0]; g.LinkWidth = l[1]; g.MaxGen = l[2]; g.MaxWidth = l[3];
            }
            ReadNic(s);
            s.Boot = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount & int.MaxValue);
            try { s.Boot = DateTime.Now - TimeSpan.FromMilliseconds(GetTickCount64()); } catch (EntryPointNotFoundException) { }
            s.RestartPending = KeyThere(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") || KeyThere(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            s.Startup.AddRange(StartupApps());
            s.Power = PowerPlan();
            return s;
        }
        // the network link in use: the adapter Windows picks for the internet (its route, not the fastest card) - PC-035
        [DllImport("iphlpapi.dll")] static extern int GetBestInterface(uint dest, out uint index);
        static void ReadNic(Specs s)
        {
            try
            {
                var up = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.GetIPProperties().GatewayAddresses.Any(gw => gw.Address != null && !gw.Address.Equals(System.Net.IPAddress.Any))).ToList();
                uint idx; NetworkInterface best = null;
                if (GetBestInterface(0x08080808u, out idx) == 0) best = up.FirstOrDefault(n => { try { var p4 = n.GetIPProperties().GetIPv4Properties(); return p4 != null && p4.Index == idx; } catch (NetworkInformationException) { return false; } });
                if (best == null) best = up.OrderByDescending(n => n.Speed).FirstOrDefault();
                s.Nic = ""; s.NicKind = ""; s.NicMbps = double.NaN;
                if (best != null) { s.Nic = best.Description; s.NicKind = best.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "Cable"; if (best.Speed > 0) s.NicMbps = best.Speed / 1e6; }
            }
            catch (NetworkInformationException) { } catch (EntryPointNotFoundException) { } catch (DllNotFoundException) { }
        }
        internal static string SystemLetter() { try { return Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Substring(0, 1).ToUpperInvariant(); } catch (ArgumentException) { return "C"; } }
        static bool KeyThere(string p) { try { using (var kk = Registry.LocalMachine.OpenSubKey(p)) return kk != null; } catch (System.Security.SecurityException) { return false; } }
        static string BusName(int b) { switch (b) { case 1: return "SCSI"; case 3: return "ATA"; case 7: return "USB"; case 8: return "RAID"; case 10: return "SAS"; case 11: return "SATA"; case 12: return "SD"; case 13: return "MMC"; case 15: return "Virtual"; case 17: return "NVMe"; case 18: return "SCM"; case 19: return "UFS"; default: return ""; } }
        static string RamType(int t) { switch (t) { case 20: return "DDR"; case 21: return "DDR2"; case 24: return "DDR3"; case 26: return "DDR4"; case 27: return "LPDDR"; case 28: return "LPDDR2"; case 29: return "LPDDR3"; case 30: return "LPDDR4"; case 34: return "DDR5"; case 35: return "LPDDR5"; default: return ""; } }
        internal static string RamTypeOf(Specs s) { return s.Rams.Select(r => RamType(r.Type)).FirstOrDefault(t => t != "") ?? ""; }
        // memory channels: "ChannelA/ChannelB" or "DIMM A1/B1" in the slot names when Windows has them; otherwise two or more sticks = two
        // 0 = not reported (PC-038): no sticks listed means nobody knows the wiring, not "single channel"
        internal static int ChannelsOf(List<Ram> rams)
        {
            if (rams.Count == 0) return 0;
            var found = rams.Select(r => Regex.Match(r.Slot, @"(?:Channel\s*|CH\s*|DIMM\s*|\b)([A-H])\d?\b", RegexOptions.IgnoreCase)).Where(m => m.Success).Select(m => m.Groups[1].Value.ToUpperInvariant()).ToList();
            int ch = found.Distinct().Count();
            bool lp = rams.Any(r => r.Type == 27 || r.Type == 28 || r.Type == 29 || r.Type == 30 || r.Type == 35);
            if (lp) return rams.Count >= 2 ? 2 : 1;   // soldered LPDDR is wide even when Windows lists it as many small chips
            if (found.Count == rams.Count) return Math.Min(ch, 8);   // PC-037: every stick names its channel - A1 and A2 are one channel; bug-hunt ML-155: workstations have 6 or 8 (A-H)
            return ch >= 2 ? Math.Min(ch, 8) : Math.Min(rams.Count, 2);
        }
        internal static string ChText(Specs s) { return s.Channels <= 0 ? "channels not reported" : s.Channels + " channel" + (s.Channels > 1 ? "s" : ""); }

        // ---------- ceilings: what each part can do at most, from its specs
        static readonly double[] LaneGBs = { 0, 0.25, 0.5, 0.985, 1.969, 3.938, 7.877 };   // PCIe gen 1..6, one lane, after line coding
        internal static double PcieMBs(int gen, int width) { return gen <= 0 || width <= 0 || gen >= LaneGBs.Length ? double.NaN : LaneGBs[gen] * width * 1000; }
        // the most this disk can move in one direction, MB/s; why: the sentence behind the number
        internal static double DiskCeiling(Disk d, out string why)
        {
            if (d.Bus == "NVMe")
            {
                double l = PcieMBs(d.LinkGen, d.LinkWidth);
                if (!double.IsNaN(l)) { why = "PCIe " + d.LinkGen + ".0 x" + d.LinkWidth + " link ≈ " + Fmt(l * 0.9) + " MB/s usable"; return l * 0.9; }
                why = "NVMe (link not readable) - assuming PCIe 3.0 x4 ≈ 3,500 MB/s"; return 3500;
            }
            // bug-hunt ML-156: on USB the port sets the ceiling, and Windows doesn't say USB 2 (≈40 MB/s) or USB 3 (≈400) - so none is claimed
            if (d.Bus == "USB") { why = "USB disk - up to ≈ 40 MB/s on a USB 2 port, ≈ 400 MB/s on USB 3" + (d.Media == "HDD" || d.Rpm > 0 ? " (a spinning disk ≈ 110 at most)" : "") + "; Windows doesn't say which port"; return double.NaN; }
            if (d.Media == "HDD" || d.Rpm > 0) { int r = d.Rpm > 0 ? d.Rpm : 5400; double c = r >= 10000 ? 220 : r >= 7200 ? 160 : 110; why = "hard disk" + (d.Rpm > 0 ? " at " + d.Rpm + " rpm" : "") + " ≈ " + Fmt(c) + " MB/s at best (outer edge)"; return c; }
            if (d.Bus == "SATA" || d.Bus == "ATA" || d.Bus == "RAID") { why = "SATA III link (600 MB/s) ≈ 550 MB/s usable"; return 550; }
            if (d.Bus == "MMC" || d.Bus == "SD") { why = "eMMC / SD storage ≈ 250 MB/s at best"; return 250; }
            if (d.Bus == "UFS") { why = "UFS storage ≈ 1,000 MB/s"; return 1000; }
            why = "kind not reported - judged as a SATA SSD (550 MB/s)"; return 550;
        }
        internal static double RamCeilingGBs(Specs s) { int mts = s.Rams.Count > 0 ? s.Rams.Max(r => r.Mts) : 0; return mts <= 0 || s.Channels <= 0 ? double.NaN : mts * 8.0 * s.Channels / 1000; }
        internal static string Fmt(double v) { return double.IsNaN(v) ? "?" : v >= 100 ? v.ToString("#,0", CultureInfo.InvariantCulture) : v.ToString("0.#", CultureInfo.InvariantCulture); }   // invariant: the pictures read these numbers back
        internal static Disk SystemDisk(Specs s) { return s.Disks.FirstOrDefault(d => d.System) ?? s.Disks.FirstOrDefault(d => d.Letters.IndexOf(SystemLetter() + ":", StringComparison.OrdinalIgnoreCase) >= 0) ?? s.Disks.FirstOrDefault(); }
        internal static Disk DiskFor(Specs s, string letter) { letter = (letter ?? "").Trim().TrimEnd(':', '\\').ToUpperInvariant(); return letter == "" || letter == SystemLetter() ? SystemDisk(s) : s.Disks.FirstOrDefault(d => d.Letters.IndexOf(letter + ":", StringComparison.OrdinalIgnoreCase) >= 0); }   // PC-024: never another disk's ceiling

        // ---------- graphics cards through DXGI (no admin, no DirectX drawing)
        [ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDXGIFactory1
        {
            void SetPrivateData(); void SetPrivateDataInterface(); void GetPrivateData(); void GetParent();
            [PreserveSig] int EnumAdapters(uint i, out IntPtr a); void MakeWindowAssociation(); void GetWindowAssociation(); void CreateSwapChain(); void CreateSoftwareAdapter();
            [PreserveSig] int EnumAdapters1(uint i, out IDXGIAdapter1 a); [PreserveSig] int IsCurrent();
        }
        [ComImport, Guid("29038f61-3839-4626-91fd-086879011a05"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDXGIAdapter1
        {
            void SetPrivateData(); void SetPrivateDataInterface(); void GetPrivateData(); void GetParent();
            void EnumOutputs(); void GetDesc(); void CheckInterfaceSupport();
            [PreserveSig] int GetDesc1(out AdapterDesc1 d);
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct AdapterDesc1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
            public uint VendorId, DeviceId, SubSysId, Revision; public UIntPtr DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory; public uint LuidLow; public int LuidHigh; public uint Flags;
        }
        [DllImport("dxgi.dll")] static extern int CreateDXGIFactory1([In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IDXGIFactory1 f);
        static List<Gpu> Dxgi()
        {
            var r = new List<Gpu>();
            try
            {
                var iid = typeof(IDXGIFactory1).GUID; IDXGIFactory1 f; if (CreateDXGIFactory1(ref iid, out f) != 0 || f == null) return r;
                try
                {
                    for (uint i = 0; i < 16; i++)
                    {
                        IDXGIAdapter1 a; if (f.EnumAdapters1(i, out a) != 0 || a == null) break;
                        try
                        {
                            AdapterDesc1 d; if (a.GetDesc1(out d) != 0) continue;
                            var g = new Gpu { Name = (d.Description ?? "").Trim(), DedicatedMb = d.DedicatedVideoMemory.ToUInt64() / 1048576.0, SharedMb = d.SharedSystemMemory.ToUInt64() / 1048576.0, Software = (d.Flags & 2) != 0 || d.VendorId == 0x1414, Vendor = (int)d.VendorId, Luid = ((long)d.LuidHigh << 32) | d.LuidLow };
                            // PC-040: small own memory alone doesn't make a card built in - Intel's are, and AMD / Qualcomm ones named as the processor's graphics
                            g.Integrated = !g.Software && g.DedicatedMb < 4096 && (   /* bug-hunt L-279: a laptop's BIOS can set aside 2-3 GB for the built-in GPU - the name decides; real cards have 4 GB or more */g.Vendor == 0x8086 && !Regex.IsMatch(g.Name, @"\bArc\b.*A\d{3}", RegexOptions.IgnoreCase) || g.Vendor == 0x5143 || Regex.IsMatch(g.Name, @"Radeon\(TM\) Graphics|Radeon Graphics|Vega \d+ Graphics|Radeon \d{3}M\b", RegexOptions.IgnoreCase));
                            if (!r.Any(x => x.Luid == g.Luid)) r.Add(g);
                        }
                        finally { Marshal.ReleaseComObject(a); }
                    }
                }
                finally { Marshal.ReleaseComObject(f); }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("dxgi: " + e.Message); }
            return r;
        }

        // ---------- PCIe links through SetupAPI: (base class, sub class, [gen now, width now, gen max, width max], name, vendor)
        [StructLayout(LayoutKind.Sequential)] struct DevInfo { public int cbSize; public Guid Class; public int Inst; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] struct DevPropKey { public Guid fmtid; public uint pid; public DevPropKey(Guid g, uint p) { fmtid = g; pid = p; } }
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevsW(IntPtr cls, string enumerator, IntPtr hwnd, int flags);
        [DllImport("setupapi.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool SetupDiEnumDeviceInfo(IntPtr set, int i, ref DevInfo d);
        [DllImport("setupapi.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref DevInfo d, ref DevPropKey key, out uint type, byte[] buf, int size, out int need, int flags);
        static readonly Guid PciProps = new Guid("3ab22e31-8264-4b4e-9af5-a8d2d8e33e62"), DevProps = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0");
        static int Prop(IntPtr set, ref DevInfo d, Guid g, uint pid) { var key = new DevPropKey(g, pid); var b = new byte[8]; uint t; int need; return SetupDiGetDevicePropertyW(set, ref d, ref key, out t, b, b.Length, out need, 0) ? BitConverter.ToInt32(b, 0) : -1; }
        static int HwVendor(IntPtr set, ref DevInfo d)
        {
            var key = new DevPropKey(DevProps, 3); var b = new byte[2048]; uint t; int need;   // DEVPKEY_Device_HardwareIds: "PCI\VEN_10DE&DEV_2684&..."
            if (!SetupDiGetDevicePropertyW(set, ref d, ref key, out t, b, b.Length, out need, 0)) return -1;
            var m = System.Text.RegularExpressions.Regex.Match(System.Text.Encoding.Unicode.GetString(b, 0, Math.Min(need, b.Length)), @"VEN_([0-9A-Fa-f]{4})");
            return m.Success ? int.Parse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture) : -1;
        }
        static List<Tuple<int, int, int[], string, int>> PciLinks()
        {
            var r = new List<Tuple<int, int, int[], string, int>>();
            IntPtr set = IntPtr.Zero;
            try
            {
                set = SetupDiGetClassDevsW(IntPtr.Zero, "PCI", IntPtr.Zero, 0x2 | 0x4);   // present, all classes
                if (set == IntPtr.Zero || set == new IntPtr(-1)) return r;
                var d = new DevInfo { cbSize = Marshal.SizeOf(typeof(DevInfo)) };
                for (int i = 0; i < 512 && SetupDiEnumDeviceInfo(set, i, ref d); i++)
                {
                    int bc = Prop(set, ref d, PciProps, 3), sc = Prop(set, ref d, PciProps, 4); if (!(bc == 1 && sc == 8) && bc != 3) continue;
                    int cs = Prop(set, ref d, PciProps, 9), cw = Prop(set, ref d, PciProps, 10), ms = Prop(set, ref d, PciProps, 11), mw = Prop(set, ref d, PciProps, 12);
                    if (cs <= 0 || cw <= 0) continue;
                    // bug-hunt M-21: PCI property 1 is the device *type* (endpoint, bridge...), not the maker - read VEN_xxxx from its hardware ID
                    int ven = HwVendor(set, ref d);
                    r.Add(Tuple.Create(bc, sc, new[] { cs, cw, Math.Max(cs, ms), Math.Max(cw, mw) }, "", ven & 0xFFFF));
                }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("pci: " + e.Message); }
            finally { if (set != IntPtr.Zero && set != new IntPtr(-1)) SetupDiDestroyDeviceInfoList(set); }
            return r;
        }

        // ---------- startup apps, the power plan, restart time
        // bug-hunt M-20: the same places Inside.Startup reads - both Run keys (64- and 32-bit) and both Startup folders -
        // each with its own StartupApproved list, so an item switched off in Task Manager isn't counted
        internal static List<string> StartupApps()
        {
            var r = new List<string>();
            Action<RegistryKey, string, string> read = (root, p, approved) =>
            {
                try
                {
                    using (var kk = root.OpenSubKey(p)) if (kk != null) foreach (var n in kk.GetValueNames()) { if (n != "" && !Disabled(root, approved, n)) r.Add(n); }
                }
                catch (System.Security.SecurityException) { } catch (UnauthorizedAccessException) { } catch (IOException) { }
            };
            read(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run"); read(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run");
            read(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run32");
            foreach (var where in new[] { Tuple.Create(Environment.SpecialFolder.Startup, Registry.CurrentUser), Tuple.Create(Environment.SpecialFolder.CommonStartup, Registry.LocalMachine) })
                try { foreach (var f in Directory.GetFiles(Environment.GetFolderPath(where.Item1))) if (!f.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) && !Disabled(where.Item2, "StartupFolder", Path.GetFileName(f))) r.Add(Path.GetFileNameWithoutExtension(f)); }
                catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { }
            return r.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        // Task Manager's "Disabled": the first byte of StartupApproved is odd
        static bool Disabled(RegistryKey root, string list, string name)
        {
            try { using (var kk = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\" + list)) { var b = kk == null ? null : kk.GetValue(name) as byte[]; return b != null && b.Length > 0 && (b[0] & 1) == 1; } }
            catch (System.Security.SecurityException) { return false; } catch (UnauthorizedAccessException) { return false; } catch (IOException) { return false; }
        }
        [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr guid);
        [DllImport("powrprof.dll")] static extern uint PowerReadFriendlyName(IntPtr root, IntPtr scheme, IntPtr sub, IntPtr setting, byte[] buf, ref uint size);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);
        internal static string PowerPlan()
        {
            try
            {
                IntPtr g; if (PowerGetActiveScheme(IntPtr.Zero, out g) != 0) return "";
                try { var buf = new byte[512]; uint n = (uint)buf.Length; if (PowerReadFriendlyName(IntPtr.Zero, g, IntPtr.Zero, IntPtr.Zero, buf, ref n) != 0) return ""; return System.Text.Encoding.Unicode.GetString(buf, 0, (int)Math.Min(n, (uint)buf.Length)).TrimEnd('\0'); }
                finally { LocalFree(g); }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; return ""; }
        }
        [StructLayout(LayoutKind.Sequential)] struct PowerStatusRaw { public byte AcLine, BatteryFlag, BatteryPercent, SaverOn; public int LifeTime, FullLifeTime; }
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool GetSystemPowerStatus(out PowerStatusRaw p);
        internal static bool BatterySaver() { PowerStatusRaw p; try { return GetSystemPowerStatus(out p) && p.SaverOn == 1; } catch (EntryPointNotFoundException) { return false; } }
        [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
        [StructLayout(LayoutKind.Sequential)] struct MemStatus { public uint Length, Load; public ulong Total, Avail, TotalPage, AvailPage, TotalVirtual, AvailVirtual, AvailExt; }
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool GlobalMemoryStatusEx(ref MemStatus m);

        // ---------- the battery: design vs full charge, and charge cycles
        internal static void Battery(out double design, out double full, out int cycles)
        {
            double de = double.NaN, fu = double.NaN; int cy = -1;
            Wmi(@"\\.\root\wmi", "SELECT DesignedCapacity FROM BatteryStaticData", o => de = D(o["DesignedCapacity"]));
            Wmi(@"\\.\root\wmi", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity", o => fu = D(o["FullChargedCapacity"]));
            Wmi(@"\\.\root\wmi", "SELECT CycleCount FROM BatteryCycleCount", o => cy = (int)D(o["CycleCount"]));
            design = de; full = fu; cycles = cy;
        }
        // admin only: wear, hours used, temperature, read errors (MSFT_StorageReliabilityCounter); null when Windows says no
        internal static double[] Wear() { return Wear(null); }
        // bug-hunt ML-159: the counters of the disk asked for (by its DeviceId), not whichever WMI lists first
        internal static double[] Wear(string deviceId)
        {
            double[] r = null, first = null;
            Wmi(@"\\.\root\Microsoft\Windows\Storage", "SELECT DeviceId, Wear, PowerOnHours, Temperature, ReadErrorsUncorrected FROM MSFT_StorageReliabilityCounter", o =>
            {
                var v = new[] { D(o["Wear"]), D(o["PowerOnHours"]), D(o["Temperature"]), D(o["ReadErrorsUncorrected"]) }; if (first == null) first = v;
                if (r == null && !string.IsNullOrEmpty(deviceId) && S(o["DeviceId"]) == deviceId) r = v;
            });
            return string.IsNullOrEmpty(deviceId) ? first : r;
        }
    }
}
