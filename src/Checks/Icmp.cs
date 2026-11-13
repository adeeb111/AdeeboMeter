// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

namespace AdeeboMeter
{
    // Windows' own ICMP API (no admin, no raw sockets): one echo with a chosen size, TTL, Don't Fragment and
    // optionally the Record Route option - and the full answer: who replied, how, how fast, and the TTL it arrived with.
    static class Icmp
    {
        [DllImport("iphlpapi.dll", SetLastError = true)] static extern IntPtr IcmpCreateFile();
        [DllImport("iphlpapi.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool IcmpCloseHandle(IntPtr h);
        [DllImport("iphlpapi.dll", SetLastError = true)] static extern int IcmpSendEcho(IntPtr h, uint dest, byte[] data, short size, ref IpOpt opt, IntPtr reply, int replySize, int timeout);
        [StructLayout(LayoutKind.Sequential)] struct IpOpt { public byte Ttl, Tos, Flags, OptionsSize; public IntPtr OptionsData; }

        public const int Ok = 0, TooBig = 11009, TimedOut = 11010, TtlExpired = 11013, NetUnreach = 11002, HostUnreach = 11003;
        public sealed class Res { public int Status = TimedOut; public string From = ""; public long Rtt = -1; public int ReplyTtl = -1; public List<string> Route = new List<string>(); }

        public static uint Addr(IPAddress a) { return BitConverter.ToUInt32(a.GetAddressBytes(), 0); }

        // size = payload bytes (IP packet = size + 28). rr = ask every router to write its address (9 slots).
        public static Res Echo(IPAddress dest, int size, int ttl, bool df, int timeoutMs, bool rr) { return Echo(dest, size, ttl, df, timeoutMs, rr, 0); }
        // tos = the whole TOS byte (DSCP << 2). Windows may clear it on the way out unless a QoS policy allows it - only a capture proves it left marked.
        public static Res Echo(IPAddress dest, int size, int ttl, bool df, int timeoutMs, bool rr, byte tos)
        {
            var r = new Res();
            if (dest == null || dest.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return r;
            IntPtr h = IcmpCreateFile(); if (h == IntPtr.Zero || h == new IntPtr(-1)) return r;
            IntPtr opts = IntPtr.Zero, buf = IntPtr.Zero;
            try
            {
                var data = new byte[Math.Max(0, Math.Min(65000, size))];
                for (int i = 0; i < data.Length; i++) data[i] = (byte)('a' + i % 23);
                var o = new IpOpt { Ttl = (byte)Math.Max(1, Math.Min(255, ttl)), Tos = tos, Flags = (byte)(df ? 2 : 0) };
                if (rr)
                {
                    var rrb = new byte[40]; rrb[0] = 7; rrb[1] = 39; rrb[2] = 4;   // Record Route: type, length, pointer, then 9 empty slots
                    opts = Marshal.AllocHGlobal(rrb.Length); Marshal.Copy(rrb, 0, opts, rrb.Length);
                    o.OptionsData = opts; o.OptionsSize = 40;
                }
                int size2 = 64 + IntPtr.Size * 4 + data.Length + 8 + 64;
                buf = Marshal.AllocHGlobal(size2);
                int n = IcmpSendEcho(h, Addr(dest), data, (short)data.Length, ref o, buf, size2, timeoutMs);
                if (n == 0) { int e = Marshal.GetLastWin32Error(); r.Status = e >= 11000 && e < 11100 ? e : TimedOut; return r; }
                r.From = new IPAddress((uint)Marshal.ReadInt32(buf, 0) & 0xFFFFFFFFL).ToString();
                r.Status = Marshal.ReadInt32(buf, 4); r.Rtt = (uint)Marshal.ReadInt32(buf, 8);
                int oo = 16 + IntPtr.Size;   // the IP_OPTION_INFORMATION inside the reply
                r.ReplyTtl = Marshal.ReadByte(buf, oo);
                int osz = Marshal.ReadByte(buf, oo + 3); IntPtr od = Marshal.ReadIntPtr(buf, oo + 4 + (IntPtr.Size == 8 ? 4 : 0));
                if (rr && osz >= 3 && od != IntPtr.Zero)
                {
                    var ob = new byte[osz]; Marshal.Copy(od, ob, 0, osz);
                    for (int i = 0; i + 2 < ob.Length;)
                    {
                        if (ob[i] == 0) break; if (ob[i] == 1) { i++; continue; }
                        int len = ob[i + 1]; if (len < 3 || i + len > ob.Length) break;
                        if (ob[i] == 7) { int ptr = ob[i + 2]; for (int a = i + 3; a + 3 < i + Math.Min(len, ptr - 1); a += 4) r.Route.Add(new IPAddress(new[] { ob[a], ob[a + 1], ob[a + 2], ob[a + 3] }).ToString()); }
                        i += len;
                    }
                }
                return r;
            }
            catch (Exception e) { Program.Log("icmp: " + e.Message); return r; }
            finally { if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf); if (opts != IntPtr.Zero) Marshal.FreeHGlobal(opts); IcmpCloseHandle(h); }
        }

        // IPv6 echo through Icmp6SendEcho2 (no admin). ttl = hop limit. Reply: address at 6 (after port + flow info), status at 28, time at 32.
        [DllImport("iphlpapi.dll", SetLastError = true)] static extern IntPtr Icmp6CreateFile();
        [DllImport("iphlpapi.dll", SetLastError = true)] static extern int Icmp6SendEcho2(IntPtr h, IntPtr ev, IntPtr apc, IntPtr ctx, byte[] src, byte[] dst, byte[] data, short size, ref IpOpt opt, IntPtr reply, int replySize, int timeout);
        public static Res Echo6(IPAddress dest, int size, int ttl, int timeoutMs)
        {
            var r = new Res();
            if (dest == null || dest.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) return r;
            IntPtr h = Icmp6CreateFile(); if (h == IntPtr.Zero || h == new IntPtr(-1)) return r;
            IntPtr buf = IntPtr.Zero;
            try
            {
                var src = new byte[28]; src[0] = 23;
                var dst = new byte[28]; dst[0] = 23; Array.Copy(dest.GetAddressBytes(), 0, dst, 8, 16);
                long scope = dest.ScopeId; dst[24] = (byte)scope; dst[25] = (byte)(scope >> 8); dst[26] = (byte)(scope >> 16); dst[27] = (byte)(scope >> 24);
                var data = new byte[Math.Max(0, Math.Min(65000, size))]; for (int i = 0; i < data.Length; i++) data[i] = (byte)('a' + i % 23);
                var o = new IpOpt { Ttl = (byte)Math.Max(1, Math.Min(255, ttl)) };
                int size2 = 36 + data.Length + 8 + 64 + 256; buf = Marshal.AllocHGlobal(size2);
                int n = Icmp6SendEcho2(h, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, src, dst, data, (short)data.Length, ref o, buf, size2, timeoutMs);
                if (n == 0) { int e = Marshal.GetLastWin32Error(); r.Status = e >= 11000 && e < 11100 ? e : TimedOut; return r; }   // no reply in the buffer to read
                var a = new byte[16]; Marshal.Copy(buf + 6, a, 0, 16); r.From = new IPAddress(a).ToString();
                if (n != 0) r.Status = Marshal.ReadInt32(buf, 28); r.Rtt = (uint)Marshal.ReadInt32(buf, 32);
                return r;
            }
            catch (Exception e) { Program.Log("icmp6: " + e.Message); return r; }
            finally { if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf); IcmpCloseHandle(h); }
        }

        // why the last Resolve4 on this thread said no ("" = just not found): a broadcast, a group, or an address typed in a form Windows reads differently
        [ThreadStatic] static string why;
        public static string LastWhy { get { return why ?? ""; } }
        public static string Why(string target, string fallback) { return LastWhy != "" ? LastWhy : fallback; }
        public static IPAddress Resolve4(string host)
        {
            why = "";
            IPAddress a; string h = Toolkit.HostOnly(host);   // a URL first, then name:port - "https://x" must not become "https"
            // numbers only (or 0x...): four plain numbers, no leading zeros - "010.0.0.1" is 8.0.0.1 to Windows, "4294967295" is 255.255.255.255
            if (System.Text.RegularExpressions.Regex.IsMatch(h ?? "", @"^(0x[0-9a-fA-F]+|[0-9.]+|[0-9a-fA-Fx.]*0x[0-9a-fA-Fx.]*)$") && !System.Text.RegularExpressions.Regex.IsMatch(h, @"^(0|[1-9][0-9]{0,2})(\.(0|[1-9][0-9]{0,2})){3}$"))
            { why = h + " isn't four plain numbers (like 192.168.1.10) - leading zeros, hex or short forms are read as a different address"; return null; }
            IPAddress got = null;
            if (IPAddress.TryParse(h, out a)) got = a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? a : null;
            else
            {
                string hh; int p; bool t; string e; if (!DestCheck.Parse(h, out hh, out p, out t, out e)) return null;
                try { got = Dns.GetHostAddresses(hh).FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork); } catch { return null; }
            }
            // one device only: never a broadcast or a multicast group (the sweep and the ICMP builder never allow it either)
            if (got != null) { string no = Lab.NotOne(got); if (no != "") { why = no; return null; } }
            return got;
        }
        public static string Text(int status)
        {
            switch (status)
            {
                case Ok: return "reply"; case TtlExpired: return "TTL expired here"; case TooBig: return "packet too big (DF set)";
                case TimedOut: return "no answer"; case NetUnreach: return "network unreachable"; case HostUnreach: return "host unreachable";
                default: return "error " + status;
            }
        }
    }
}
