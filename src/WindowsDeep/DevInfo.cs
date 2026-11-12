// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace AdeeboMeter
{
    // ================================================================== devices as Windows' Plug and Play keeps them
    // Read-only, no admin: the Configuration Manager (cfgmgr32) lists every device Windows knows - plugged in now or seen
    // before - with its problem code, driver version and date, and when it last arrived and was last removed.
    // Nothing is changed: no driver is installed, rolled back or disabled here.
    static class DevInfo
    {
        [StructLayout(LayoutKind.Sequential)] struct Key { public Guid F; public int Pid; public Key(string g, int p) { F = new Guid(g); Pid = p; } }
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_Device_ID_List_SizeW(out int len, string filter, int flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_Device_ID_ListW(string filter, [Out] char[] buffer, int len, int flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Locate_DevNodeW(out int inst, string id, int flags);
        [DllImport("cfgmgr32.dll")] static extern int CM_Get_DevNode_Status(out int status, out int problem, int inst, int flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_DevNode_PropertyW(int inst, ref Key key, out int type, [Out] byte[] buf, ref int size, int flags);

        static readonly Key Name = new Key("b725f130-47ef-101a-a5f1-02608c9ebac0", 10), Desc = new Key("a45c254e-df1c-4efd-8020-67d146a850e0", 2), Class = new Key("a45c254e-df1c-4efd-8020-67d146a850e0", 9),
            Arrived = new Key("83da6326-97a6-4088-9453-a1923f573b29", 102), Removed = new Key("83da6326-97a6-4088-9453-a1923f573b29", 103),
            DrvDate = new Key("a8b865dd-2e3d-4094-ad97-e593a70c75d6", 2), DrvVer = new Key("a8b865dd-2e3d-4094-ad97-e593a70c75d6", 3), DrvProv = new Key("a8b865dd-2e3d-4094-ad97-e593a70c75d6", 9);

        static readonly char[] Nul = { '\0' };
        public sealed class Dev
        {
            public string Id = "", Name = "", Class = "", DriverVer = "", Provider = ""; public bool Present; public int Problem; public DateTime DriverDate = DateTime.MinValue, Arrived = DateTime.MinValue, Removed = DateTime.MinValue;
            public string Enumerator { get { int i = Id.IndexOf('\\'); return i > 0 ? Id.Substring(0, i) : Id; } }
        }

        // enumerator "" = every device; "USB", "USBSTOR", "BTHENUM", "PCI" ... = those only. err says why the list is empty.
        public static List<Dev> List(string enumerator, bool presentOnly, out string err)
        {
            var res = new List<Dev>(); err = "";
            try
            {
                int flags = (enumerator != "" ? 0x1 : 0) | (presentOnly ? 0x100 : 0), len; string filter = enumerator != "" ? enumerator : null;
                int rc = CM_Get_Device_ID_List_SizeW(out len, filter, flags); if (rc != 0 || len <= 0) { err = rc == 0 ? "" : "Windows wouldn't list the devices (" + rc + ")."; return res; }
                var buf = new char[len]; rc = CM_Get_Device_ID_ListW(filter, buf, len, flags); if (rc != 0) { err = "Windows wouldn't list the devices (" + rc + ")."; return res; }
                foreach (var id in new string(buf).Split(Nul, StringSplitOptions.RemoveEmptyEntries).Take(3000))
                {
                    int inst; if (CM_Locate_DevNodeW(out inst, id, 1 /*phantom: devices not plugged in too*/) != 0) continue;
                    var d = new Dev { Id = id };
                    int st, pr; int s2 = CM_Get_DevNode_Status(out st, out pr, inst, 0);
                    d.Present = s2 == 0; if (s2 == 0 && (st & 0x400) != 0) d.Problem = pr;   // DN_HAS_PROBLEM
                    d.Name = Str(inst, Name); if (d.Name == "") d.Name = Str(inst, Desc);
                    d.Class = Str(inst, Class); d.DriverVer = Str(inst, DrvVer); d.Provider = Str(inst, DrvProv);
                    d.DriverDate = Time(inst, DrvDate); d.Arrived = Time(inst, Arrived); d.Removed = Time(inst, Removed);
                    res.Add(d);
                }
            }
            catch (Exception e) { if (!(e is DllNotFoundException || e is EntryPointNotFoundException)) throw; err = "Device details aren't available on this version of Windows."; }
            return res;
        }
        static byte[] Raw(int inst, Key k, out int type)
        {
            var key = k; int size = 0; type = 0;
            int rc = CM_Get_DevNode_PropertyW(inst, ref key, out type, null, ref size, 0);
            if (size <= 0 || size > 65536) return null;
            var b = new byte[size]; key = k;
            return CM_Get_DevNode_PropertyW(inst, ref key, out type, b, ref size, 0) == 0 ? b : null;
        }
        static string Str(int inst, Key k) { int t; var b = Raw(inst, k, out t); if (b == null || t != 0x12) return ""; return Program.Plain(System.Text.Encoding.Unicode.GetString(b).TrimEnd('\0')); }
        static DateTime Time(int inst, Key k)
        {
            int t; var b = Raw(inst, k, out t); if (b == null || t != 0x10 || b.Length < 8) return DateTime.MinValue;
            long ft = BitConverter.ToInt64(b, 0); if (ft <= 0) return DateTime.MinValue;
            try { return DateTime.FromFileTimeUtc(ft).ToLocalTime(); } catch (ArgumentOutOfRangeException) { return DateTime.MinValue; }
        }

        // the Windows problem code in plain words: what it means, and the one thing to try
        public static string[] Problem(int code)
        {
            switch (code)
            {
                case 0: return new[] { "working", "" };
                case 1: return new[] { "not set up properly", "Unplug it, wait 10 seconds, plug it into another port." };
                case 3: return new[] { "the driver may be damaged, or Windows is short of memory", "Restart the PC; if it stays, update or reinstall the driver in Device Manager." };
                case 10: return new[] { "can't start", "Unplug and replug it; if it stays, update the driver (or roll it back if this started after an update)." };
                case 12: return new[] { "not enough free resources for it", "Unplug other devices on the same hub or port and try again." };
                case 14: return new[] { "needs a restart to work", "Restart the PC." };
                case 18: return new[] { "its driver needs reinstalling", "Device Manager › the device › Uninstall, then unplug and replug it." };
                case 19: return new[] { "its settings in Windows are damaged", "Uninstall it in Device Manager, then unplug and replug it." };
                case 21: return new[] { "Windows is removing it", "Wait a moment, or restart the PC." };
                case 22: return new[] { "switched off in Device Manager", "Device Manager › the device › Enable device." };
                case 24: return new[] { "not there, not working, or missing drivers", "Check the cable and port; then update the driver." };
                case 28: return new[] { "has no driver", "Windows Update › Optional updates often has it; otherwise the maker's website." };
                case 29: return new[] { "switched off in the PC's firmware (BIOS / UEFI)", "Turn it on in the BIOS / UEFI settings." };
                case 31: return new[] { "not working - Windows couldn't load its driver", "Update or reinstall the driver." };
                case 32: return new[] { "its driver is switched off", "Reinstall the driver." };
                case 37: return new[] { "its driver failed to start", "Update the driver, or roll it back if this started after an update." };
                case 39: return new[] { "its driver is missing or damaged", "Reinstall the driver." };
                case 41: return new[] { "the driver loaded but can't find the hardware", "Unplug and replug it; try another port or cable." };
                case 43: return new[] { "Windows stopped it because it reported problems", "The classic USB failure: try another port and cable (a hub without power often causes it); then update the driver." };
                case 45: return new[] { "not plugged in right now", "Plug it in - Windows remembers it from before." };
                case 47: return new[] { "safely removed - waiting to be unplugged", "Unplug it and plug it back in." };
                case 48: return new[] { "its driver is blocked as incompatible", "Get a newer driver from the maker." };
                case 52: return new[] { "its driver isn't signed, so Windows won't load it", "Get a signed driver from the maker." };
                default: return new[] { "Windows reports problem code " + code.ToString(CultureInfo.InvariantCulture), "Look it up in Device Manager › the device › Properties." };
            }
        }
        public static string When(DateTime t) { if (t == DateTime.MinValue) return "-"; var c = CultureInfo.CurrentCulture; return t.Date == DateTime.Today ? "today " + t.ToString("HH:mm", c) : t.Date == DateTime.Today.AddDays(-1) ? "yesterday " + t.ToString("HH:mm", c) : t.ToString("d MMM yyyy HH:mm", c); }
        public static string Age(DateTime t) { if (t == DateTime.MinValue) return ""; double y = (DateTime.Now - t).TotalDays / 365.25; return y >= 1 ? y.ToString("0.#", CultureInfo.CurrentCulture) + " years old" : ((int)(DateTime.Now - t).TotalDays) + " days old"; }
    }
}
