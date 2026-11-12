// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AdeeboMeter
{
    // SNMP, read only: GET and GETBULK over v2c or v3 (no auth, auth, auth + AES privacy). It never sends SET and never
    // listens for traps. Replies are accepted only from the device asked, for this request, and every length is checked.
    [SuppressMessage("Performance", "CA1861", Justification = "small fixed arrays in one-off requests")]
    static class Snmp
    {
        public sealed class Vb { public string Oid = ""; public byte Type; public byte[] Raw = Array.Empty<byte>(); public bool End { get { return Type >= 0x80 && Type <= 0x82; } } }
        public sealed class Cred
        {
            public int Version = 2; public string Community = "public", User = "", AuthPass = "", PrivPass = ""; public string AuthAlg = "sha";   // md5 | sha | sha256
            public bool Auth { get { return AuthPass != ""; } }
            public bool Priv { get { return PrivPass != ""; } }
            // the localized keys, kept on this object only - it already holds the passwords and lives for one walk, so nothing is kept longer.
            // RFC 3414's 1 MB stretch then runs once per device, not once per message.
            byte[] kEid, kAuth, kPriv;
            internal byte[] Key(bool priv, byte[] eid)
            {
                if (kEid == null || !kEid.SequenceEqual(eid)) { kEid = (byte[])eid.Clone(); kAuth = null; kPriv = null; }
                if (priv) { if (kPriv == null) kPriv = Localize(PrivPass, eid, AuthAlg); return kPriv; }
                if (kAuth == null) kAuth = Localize(AuthPass, eid, AuthAlg); return kAuth;
            }
            public string Bad = "";   // set when the text can't be used as typed - the request is refused, nothing is sent
            // a ':' can't be part of the user or auth password (it separates them); the privacy password may contain one
            // "public"  ·  "v3:user"  ·  "v3:user:authpass"  ·  "v3:user:authpass:privpass"  ·  "v3-md5:..." / "v3-sha256:..."
            public static Cred Parse(string s)
            {
                // spaces around it are a paste slip and go; "community:" in front keeps a community exactly as typed (spaces,
                // or one that starts with v3:)
                var c = new Cred(); string raw = s ?? ""; s = raw.Trim(); if (s == "") return c;
                if (s.StartsWith("community:", StringComparison.OrdinalIgnoreCase)) { c.Community = raw.Substring(raw.IndexOf(':') + 1); return c; }
                var m = Regex.Match(s, @"^v3(?:-([a-z0-9]+))?:(.*)$", RegexOptions.IgnoreCase);
                if (!m.Success) { c.Community = s; return c; }
                c.Version = 3;
                if (m.Groups[1].Success && m.Groups[1].Value != "")
                {
                    string alg = m.Groups[1].Value.ToLowerInvariant(); if (alg == "sha1") alg = "sha";
                    if (alg != "md5" && alg != "sha" && alg != "sha256") { c.Bad = "unknown v3 algorithm '" + m.Groups[1].Value + "' - use v3-md5:, v3:, v3-sha1: or v3-sha256:"; return c; }
                    c.AuthAlg = alg;
                }
                var p = m.Groups[2].Value.Split(new[] { ':' }, 3);
                c.User = p[0]; if (p.Length > 1) c.AuthPass = p[1]; if (p.Length > 2) c.PrivPass = p[2];
                if (c.User == "") c.Bad = "v3 needs a user name: v3:user[:authpass[:privpass]]";
                else if (c.Priv && !c.Auth) c.Bad = "privacy needs an auth password too: v3:user:authpass:privpass";   // never quietly send it unprotected
                return c;
            }
            public string Label() { return Version == 2 ? "v2c" : "v3 " + (Priv ? "auth + privacy (" + AuthAlg.ToUpperInvariant() + ", AES-128)" : Auth ? "auth (" + AuthAlg.ToUpperInvariant() + ")" : "no auth"); }
        }

        // ================================================================== BER
        public static byte[] Tlv(byte tag, byte[] v)
        {
            byte[] l = v.Length < 128 ? new[] { (byte)v.Length } : v.Length < 256 ? new byte[] { 0x81, (byte)v.Length } : new byte[] { 0x82, (byte)(v.Length >> 8), (byte)v.Length };
            var r = new byte[1 + l.Length + v.Length]; r[0] = tag; Array.Copy(l, 0, r, 1, l.Length); Array.Copy(v, 0, r, 1 + l.Length, v.Length); return r;
        }
        static byte[] Cat(params byte[][] parts) { var r = new List<byte>(); foreach (var p in parts) r.AddRange(p); return r.ToArray(); }
        static byte[] Int(long v) { var b = new List<byte>(); do { b.Insert(0, (byte)v); v >>= 8; } while (v != 0 && v != -1); if (v == 0 && (b[0] & 0x80) != 0) b.Insert(0, 0); if (v == -1 && (b[0] & 0x80) == 0) b.Insert(0, 0xFF); return Tlv(2, b.ToArray()); }
        static byte[] Str(byte[] s) { return Tlv(4, s ?? Array.Empty<byte>()); }
        public static byte[] OidBytes(string s)
        {
            var p = s.Trim('.').Split('.').Select(x => long.Parse(x, CultureInfo.InvariantCulture)).ToArray(); if (p.Length < 2) throw new FormatException("an OID needs at least two numbers");
            var b = new List<byte> { (byte)(p[0] * 40 + p[1]) };
            for (int i = 2; i < p.Length; i++) { var st = new List<byte>(); long v = p[i]; st.Add((byte)(v & 0x7F)); v >>= 7; while (v > 0) { st.Add((byte)(0x80 | (v & 0x7F))); v >>= 7; } st.Reverse(); b.AddRange(st); }
            return Tlv(6, b.ToArray());
        }
        public static bool ValidOid(string s) { return Regex.IsMatch((s ?? "").Trim('.'), @"^[0-2](\.\d{1,9}){1,127}$"); }
        static string OidText(byte[] r, int o, int l)
        {
            if (l < 1) return ""; var sb = new StringBuilder(); int f = r[o]; sb.Append(f / 40 > 2 ? 2 : f / 40).Append('.').Append(f / 40 > 2 ? f - 80 : f % 40);
            long v = 0; for (int i = o + 1; i < o + l; i++) { v = v << 7 | (uint)(r[i] & 0x7F); if ((r[i] & 0x80) == 0) { sb.Append('.').Append(v); v = 0; } if (v > uint.MaxValue) return ""; }
            return sb.ToString();
        }
        static bool T(byte[] r, int pos, int end, out int tag, out int hl, out int len)
        {
            tag = 0; hl = 0; len = 0;
            if (pos < 0 || pos + 2 > end || end > r.Length) return false;
            tag = r[pos]; len = r[pos + 1]; hl = 2;
            if ((len & 0x80) != 0) { int nb = len & 0x7F; if (nb == 0 || nb > 3 || pos + 2 + nb > end) return false; len = 0; for (int i = 0; i < nb; i++) len = len << 8 | r[pos + 2 + i]; hl = 2 + nb; }
            return len >= 0 && pos + hl + len <= end;
        }
        static long ReadInt(byte[] r, int o, int l) { if (l < 1 || l > 8) return 0; long v = (r[o] & 0x80) != 0 ? -1 : 0; for (int i = 0; i < l; i++) v = v << 8 | r[o + i]; return v; }

        // ================================================================== messages
        sealed class Engine { public byte[] Id = Array.Empty<byte>(); public int Boots, Time; public DateTime At; }
        static readonly Dictionary<string, Engine> engines = new Dictionary<string, Engine>();   // v3 engine per device, this session only
        static int nextId = Environment.TickCount & 0x7FFFFFFF;

        static byte[] Pdu(byte type, int reqId, int a, int b, IEnumerable<string> oids)
        {
            var vbs = oids.Select(o => Tlv(0x30, Cat(OidBytes(o), new byte[] { 5, 0 }))).ToArray();
            return Tlv(type, Cat(Int(reqId), Int(a), Int(b), Tlv(0x30, Cat(vbs))));
        }

        // one request / reply over a connected socket; null = no answer
        static byte[] Exchange(IPEndPoint ep, byte[] msg, int timeoutMs) { bool refused; return Exchange(ep, msg, timeoutMs, null, out refused); }
        // accept: is this datagram the answer to our request? Anything else (a stray or forged packet) is skipped, not taken
        // as the answer. refused: the device said nothing listens on that port (ICMP port unreachable).
        static byte[] Exchange(IPEndPoint ep, byte[] msg, int timeoutMs, Func<byte[], bool> accept, out bool refused)
        {
            refused = false;
            using (var u = new UdpClient(ep.AddressFamily))
            {
                u.Connect(ep); u.Send(msg, msg.Length); var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (true)
                {
                    int left = (int)(until - DateTime.UtcNow).TotalMilliseconds; if (left <= 0) return null;
                    u.Client.ReceiveTimeout = left;
                    try { var from = new IPEndPoint(IPAddress.Any, 0); var r = u.Receive(ref from); if (accept == null || accept(r)) return r; }
                    catch (SocketException se) { if (se.SocketErrorCode == SocketError.ConnectionReset) refused = true; return null; }
                }
            }
        }

        // Sends one PDU and returns its varbinds; error is "" when it worked.
        public static List<Vb> Request(IPEndPoint ep, Cred c, byte pduType, int a, int b, IEnumerable<string> oids, out string error)
        {
            error = ""; int id = Interlocked.Increment(ref nextId) & 0x7FFFFFFF;
            if (c.Bad != "") { error = c.Bad; return null; }
            byte[] pdu = Pdu(pduType, id, a, b, oids), reply;
            if (c.Version == 2)
            {
                var msg = Tlv(0x30, Cat(Int(1), Str(Encoding.UTF8.GetBytes(c.Community)), pdu)); bool refused;
                reply = Exchange(ep, msg, 3000, r => { string pe; Parse2(r, id, c.Community, out pe); return pe != "a reply to a different request"; }, out refused);
                if (reply == null) { error = refused ? "nothing listens on UDP " + ep.Port + " there (port unreachable) - SNMP is off on that device" : "no answer in 3 s"; return null; }
                return Parse2(reply, id, c.Community, out error);
            }
            Engine e; string ek = ep.ToString();
            lock (engines) engines.TryGetValue(ek, out e);
            if (e == null || (DateTime.UtcNow - e.At).TotalMinutes > 10)
            {
                e = Discover(ep, out error); if (e == null) return null;
                lock (engines) engines[ek] = e;
            }
            reply = Exchange(ep, Message3(c, e, pdu, id), 3000);
            if (reply == null) { error = "no answer in 3 s"; return null; }
            var vbs = Parse3(reply, c, e, id, out error);
            if (error.IndexOf("time window", StringComparison.Ordinal) >= 0)   // the device restarted or our clock drifted: learn its time again, once
            {
                e = Discover(ep, out error); if (e == null) return null; lock (engines) engines[ek] = e;
                reply = Exchange(ep, Message3(c, e, pdu, id), 3000); if (reply == null) { error = "no answer in 3 s"; return null; }
                vbs = Parse3(reply, c, e, id, out error);
            }
            return vbs;
        }

        static List<Vb> Parse2(byte[] r, int id, string community, out string error)
        {
            error = ""; int tag, hl, len;
            if (!T(r, 0, r.Length, out tag, out hl, out len) || tag != 0x30) { error = "not an SNMP reply"; return null; }
            int p = hl, end = hl + len;
            if (!T(r, p, end, out tag, out hl, out len) || tag != 2 || ReadInt(r, p + hl, len) != 1) { error = "not an SNMP v2c reply"; return null; }
            p += hl + len; if (!T(r, p, end, out tag, out hl, out len) || tag != 4) { error = "not an SNMP reply"; return null; }
            if (Encoding.ASCII.GetString(r, p + hl, len) != community) { error = "a reply for a different community"; return null; }
            p += hl + len;
            return ReadPdu(r, p, end, id, out error);
        }
        static List<Vb> ReadPdu(byte[] r, int p, int end, int id, out string error)
        {
            error = ""; int tag, hl, len;
            if (!T(r, p, end, out tag, out hl, out len)) { error = "a broken reply"; return null; }
            if (tag == 0xA8) { error = "the device reported a problem (" + ReportName(r, p + hl, p + hl + len) + ")"; return null; }
            if (tag != 0xA2) { error = "not a reply to this request"; return null; }
            p += hl; int pend = p + len;
            if (!T(r, p, pend, out tag, out hl, out len) || tag != 2 || ReadInt(r, p + hl, len) != id) { error = "a reply to a different request"; return null; }
            p += hl + len; if (!T(r, p, pend, out tag, out hl, out len) || tag != 2) { error = "a broken reply"; return null; }
            if (len < 1) { error = "a broken reply (no error status)"; return null; }
            long err = ReadInt(r, p + hl, len);
            if (err != 0) { error = "SNMP error " + err + " (" + (err == 2 ? "no such name" : err == 5 ? "general error" : err == 6 ? "no access" : err == 16 ? "authorization error" : "see RFC 3416") + ")"; return null; }
            p += hl + len; if (!T(r, p, pend, out tag, out hl, out len)) { error = "a broken reply"; return null; }
            p += hl + len; if (!T(r, p, pend, out tag, out hl, out len) || tag != 0x30) { error = "a broken reply"; return null; }
            var list = new List<Vb>(); int vp = p + hl, vend = vp + len;
            while (vp < vend && list.Count < 5000)
            {
                int vt, vhl, vl; if (!T(r, vp, vend, out vt, out vhl, out vl) || vt != 0x30) break;
                int q = vp + vhl, qend = q + vl; int ot, ohl, ol, xt, xhl, xl;
                if (!T(r, q, qend, out ot, out ohl, out ol) || ot != 6) break;
                int xp = q + ohl + ol; if (!T(r, xp, qend, out xt, out xhl, out xl)) break;
                var vb = new Vb { Oid = OidText(r, q + ohl, ol), Type = (byte)xt, Raw = new byte[xl] }; Array.Copy(r, xp + xhl, vb.Raw, 0, xl);
                if (vb.Oid == "") break; list.Add(vb); vp = vend > qend ? qend : vend;
            }
            return list;
        }
        // a Report PDU: request-id, error-status, error-index, then the counter that fired as the first varbind
        static string ReportName(byte[] r, int p, int end)
        {
            int tag, hl, len, q = p;
            for (int i = 0; i < 3; i++) { if (!T(r, q, end, out tag, out hl, out len)) return "report"; q += hl + len; }
            if (!T(r, q, end, out tag, out hl, out len) || tag != 0x30) return "report";
            int v = q + hl; if (!T(r, v, end, out tag, out hl, out len) || tag != 0x30) return "report";
            int o = v + hl; if (!T(r, o, end, out tag, out hl, out len) || tag != 6) return "report";
            return UsmReport(OidText(r, o + hl, len));
        }
        static string UsmReport(string oid)
        {
            switch (oid.EndsWith(".0", StringComparison.Ordinal) ? oid.Substring(0, oid.Length - 2) : oid)
            {
                case "1.3.6.1.6.3.15.1.1.1": return "unsupported security level";
                case "1.3.6.1.6.3.15.1.1.2": return "time window - clocks out of step";
                case "1.3.6.1.6.3.15.1.1.3": return "unknown user name";
                case "1.3.6.1.6.3.15.1.1.4": return "unknown engine ID";
                case "1.3.6.1.6.3.15.1.1.5": return "wrong digest - the auth password or algorithm is wrong";
                case "1.3.6.1.6.3.15.1.1.6": return "decryption error - the privacy password is wrong";
                default: return oid;
            }
        }

        // ---------- v3 (RFC 3412 / 3414 / 3826)
        static Engine Discover(IPEndPoint ep, out string error)
        {
            error = ""; int id = Interlocked.Increment(ref nextId) & 0x7FFFFFFF;
            var usm = Tlv(0x30, Cat(Str(null), Int(0), Int(0), Str(null), Str(null), Str(null)));
            var scoped = Tlv(0x30, Cat(Str(null), Str(null), Pdu(0xA0, id, 0, 0, Array.Empty<string>())));
            var msg = Tlv(0x30, Cat(Int(3), Tlv(0x30, Cat(Int(id), Int(65507), Str(new byte[] { 4 }), Int(3))), Str(usm), scoped));
            var r = Exchange(ep, msg, 3000);
            if (r == null) { error = "no answer to SNMPv3 discovery in 3 s"; return null; }
            int tag, hl, len, p; byte[] usmB; int rid;
            if (!Head3(r, out p, out usmB, out rid)) { error = "not an SNMPv3 reply"; return null; }
            if (rid != id || !ReportAt(r, p)) { error = "an SNMPv3 discovery reply that doesn't answer our request"; return null; }   // only our own Report teaches us the engine
            var e = new Engine { At = DateTime.UtcNow };
            int q = 0; if (!T(usmB, 0, usmB.Length, out tag, out hl, out len) || tag != 0x30) { error = "a broken v3 reply"; return null; }
            q = hl; if (!T(usmB, q, usmB.Length, out tag, out hl, out len) || tag != 4) { error = "a broken v3 reply"; return null; }
            e.Id = usmB.Skip(q + hl).Take(len).ToArray(); q += hl + len;
            if (!T(usmB, q, usmB.Length, out tag, out hl, out len) || tag != 2) { error = "a broken v3 reply"; return null; } e.Boots = (int)ReadInt(usmB, q + hl, len); q += hl + len;
            if (!T(usmB, q, usmB.Length, out tag, out hl, out len) || tag != 2) { error = "a broken v3 reply"; return null; } e.Time = (int)ReadInt(usmB, q + hl, len);
            if (e.Id.Length < 5 || e.Id.Length > 32) { error = "the device sent no engine ID"; return null; }
            return e;
        }
        // version 3, header, USM parameters (returned), and where the scoped PDU starts
        static bool Head3(byte[] r, out int scopedPos, out byte[] usm, out int msgId) { int up; return Head3(r, out scopedPos, out usm, out msgId, out up); }
        static bool Head3(byte[] r, out int scopedPos, out byte[] usm, out int msgId, out int usmPos)
        {
            scopedPos = 0; usm = null; msgId = 0; usmPos = 0; int tag, hl, len;
            if (!T(r, 0, r.Length, out tag, out hl, out len) || tag != 0x30) return false;
            int p = hl, end = hl + len;
            if (!T(r, p, end, out tag, out hl, out len) || tag != 2 || ReadInt(r, p + hl, len) != 3) return false;
            p += hl + len; if (!T(r, p, end, out tag, out hl, out len) || tag != 0x30) return false;
            int g = p + hl; int gt, ghl, gl; if (T(r, g, p + hl + len, out gt, out ghl, out gl) && gt == 2) msgId = (int)ReadInt(r, g + ghl, gl);
            p += hl + len; if (!T(r, p, end, out tag, out hl, out len) || tag != 4) return false;
            usmPos = p + hl; usm = r.Skip(p + hl).Take(len).ToArray(); scopedPos = p + hl + len; return true;
        }
        static int AuthLen(Cred c) { return c.AuthAlg == "sha256" ? 24 : 12; }
        static byte[] Message3(Cred c, Engine e, byte[] pdu, int id)
        {
            int elapsed = (int)(DateTime.UtcNow - e.At).TotalSeconds; int time = e.Time + elapsed;
            byte flags = (byte)((c.Auth ? 1 : 0) | (c.Auth && c.Priv ? 2 : 0) | 4);
            var scoped = Tlv(0x30, Cat(Str(e.Id), Str(null), pdu));
            byte[] privParams = Array.Empty<byte>(), body = scoped;
            if (c.Auth && c.Priv)
            {
                var key = c.Key(true, e.Id); privParams = new byte[8]; using (var g = RandomNumberGenerator.Create()) g.GetBytes(privParams);
                body = Str(Aes(key, e.Boots, time, privParams, scoped, true));
            }
            byte[] authParams = new byte[c.Auth ? AuthLen(c) : 0];
            Func<byte[]> build = () => Tlv(0x30, Cat(Int(3), Tlv(0x30, Cat(Int(id), Int(65507), Str(new[] { flags }), Int(3))),
                Str(Tlv(0x30, Cat(Str(e.Id), Int(e.Boots), Int(time), Str(Encoding.UTF8.GetBytes(c.User)), Str(authParams), Str(privParams)))), body));
            var msg = build();
            if (c.Auth) { var mac = Mac(c, c.Key(false, e.Id), msg); Array.Copy(mac, authParams, authParams.Length); msg = build(); }
            return msg;
        }
        static List<Vb> Parse3(byte[] r, Cred c, Engine e, int id, out string error)
        {
            error = ""; int sp, mid, up; byte[] usm;
            if (!Head3(r, out sp, out usm, out mid, out up)) { error = "not an SNMPv3 reply"; return null; }
            if (mid != id) { error = "a reply to another request (message ID)"; return null; }
            int tag, hl, len, q;
            // USM: engine id, boots, time, user, auth params, priv params - each with where it sits in the message
            if (!T(usm, 0, usm.Length, out tag, out hl, out len)) { error = "a broken v3 reply"; return null; }
            q = hl; var f = new List<byte[]>(); var at = new int[6];
            for (int i = 0; i < 6; i++) { if (!T(usm, q, usm.Length, out tag, out hl, out len) || tag != (i == 1 || i == 2 ? 2 : 4)) { error = "a broken v3 reply"; return null; } at[i] = up + q + hl; f.Add(usm.Skip(q + hl).Take(len).ToArray()); q += hl + len; }
            int p = sp;
            if (!T(r, p, r.Length, out tag, out hl, out len)) { error = "a broken v3 reply"; return null; }
            bool isReport = tag == 0x30 && ReportAt(r, p);
            if (!f[0].SequenceEqual(e.Id)) { error = "a reply from another SNMP engine than the one discovered"; return null; }
            if (!isReport && Encoding.UTF8.GetString(f[3]) != c.User) { error = "a reply for another user"; return null; }
            if (c.Auth)
            {
                // a signed request needs a signed answer: right length, MAC checked at its parsed place, inside the 150 s window
                if (f[4].Length == 0 && isReport) { }   // unsigned reports (unknown user, wrong time...) are read below as errors, never as data
                else if (f[4].Length != AuthLen(c)) { error = "a reply whose signature has the wrong length for " + c.AuthAlg.ToUpperInvariant() + " - check the algorithm"; return null; }
                else
                {
                    var copy = (byte[])r.Clone(); for (int i = 0; i < f[4].Length; i++) copy[at[4] + i] = 0;
                    var mac = Mac(c, c.Key(false, e.Id), copy);
                    for (int i = 0; i < f[4].Length; i++) if (mac[i] != f[4][i]) { error = "a reply that isn't signed with your auth password"; return null; }
                    long rb = ReadInt(f[1], 0, f[1].Length), rt = ReadInt(f[2], 0, f[2].Length), now = e.Time + (long)(DateTime.UtcNow - e.At).TotalSeconds;
                    if (rb != e.Boots || Math.Abs(rt - now) > 150) { error = "time window: the reply's engine time is out of step"; return null; }
                }
            }
            else if (!isReport && f[4].Length != 0) { error = "a signed reply to an unsigned request"; return null; }
            byte[] scoped; int sstart;
            if (c.Priv && tag != 4 && !isReport) { error = "an unencrypted reply to an encrypted request - refused"; return null; }   // privacy can't be dropped by the device
            if (tag == 4)   // encrypted
            {
                if (!c.Priv) { error = "an encrypted reply, but no privacy password was given"; return null; }
                var enc = r.Skip(p + hl).Take(len).ToArray();
                long b = ReadInt(f[1], 0, f[1].Length), t = ReadInt(f[2], 0, f[2].Length);
                try { scoped = Aes(c.Key(true, e.Id), (int)b, (int)t, f[5], enc, false); } catch (CryptographicException) { error = "a reply that doesn't decrypt - check the privacy password"; return null; }
                sstart = 0;
            }
            else { scoped = r; sstart = p; }
            if (!T(scoped, sstart, scoped.Length, out tag, out hl, out len) || tag != 0x30) { error = c.Priv ? "a reply that doesn't decrypt - check the privacy password" : "a broken v3 reply"; return null; }
            int s = sstart + hl, send = s + len;
            if (!T(scoped, s, send, out tag, out hl, out len) || tag != 4) { error = "a broken v3 reply"; return null; } s += hl + len;
            if (!T(scoped, s, send, out tag, out hl, out len) || tag != 4) { error = "a broken v3 reply"; return null; } s += hl + len;
            if (T(scoped, s, send, out tag, out hl, out len) && tag == 0xA8) { error = "the device reported: " + ReportName(scoped, s + hl, s + hl + len); return null; }
            return ReadPdu(scoped, s, send, id, out error);
        }
        // a plaintext scoped PDU whose PDU is a Report (0xA8)
        static bool ReportAt(byte[] r, int p)
        {
            int tag, hl, len; if (!T(r, p, r.Length, out tag, out hl, out len) || tag != 0x30) return false;
            int s = p + hl, end = s + len;
            for (int i = 0; i < 2; i++) { if (!T(r, s, end, out tag, out hl, out len)) return false; s += hl + len; }
            return T(r, s, end, out tag, out hl, out len) && tag == 0xA8;
        }

        // RFC 3414 A.2: hash the password stretched to 1 MB, then localize it to the device's engine ID
        [SuppressMessage("Security", "CA5350", Justification = "SNMPv3 USM defines SHA-1 (RFC 3414); offered alongside SHA-256")]
        [SuppressMessage("Security", "CA5351", Justification = "SNMPv3 USM defines MD5 (RFC 3414) for devices that offer nothing else")]
        public static byte[] Localize(string password, byte[] engineId, string alg)
        {
            using (HashAlgorithm h = alg == "md5" ? (HashAlgorithm)MD5.Create() : alg == "sha256" ? (HashAlgorithm)SHA256.Create() : SHA1.Create())
            {
                var pw = Encoding.UTF8.GetBytes(password); if (pw.Length == 0) throw new ArgumentException("empty password");
                var buf = new byte[1048576]; for (int i = 0; i < buf.Length; i++) buf[i] = pw[i % pw.Length];
                var ku = h.ComputeHash(buf);
                return h.ComputeHash(Cat(ku, engineId, ku));
            }
        }
        [SuppressMessage("Security", "CA5350", Justification = "SNMPv3 USM: HMAC-SHA-96 (RFC 3414)")]
        [SuppressMessage("Security", "CA5351", Justification = "SNMPv3 USM: HMAC-MD5-96 (RFC 3414)")]
        static byte[] Mac(Cred c, byte[] key, byte[] msg)
        {
            using (HMAC m = c.AuthAlg == "md5" ? (HMAC)new HMACMD5(key) : c.AuthAlg == "sha256" ? (HMAC)new HMACSHA256(key) : new HMACSHA1(key)) return m.ComputeHash(msg);
        }
        // AES-128 in CFB-128 mode (RFC 3826): key = the first 16 bytes of the localized privacy key, IV = boots + time + salt
        [SuppressMessage("Security", "CA5358", Justification = "SNMPv3 USM AES (RFC 3826) is defined as CFB-128")]
        public static byte[] Aes(byte[] key, int boots, int time, byte[] salt, byte[] data, bool encrypt)
        {
            if (salt == null || salt.Length != 8) throw new CryptographicException("bad privacy parameters");
            var iv = new byte[16]; iv[0] = (byte)(boots >> 24); iv[1] = (byte)(boots >> 16); iv[2] = (byte)(boots >> 8); iv[3] = (byte)boots;
            iv[4] = (byte)(time >> 24); iv[5] = (byte)(time >> 16); iv[6] = (byte)(time >> 8); iv[7] = (byte)time; Array.Copy(salt, 0, iv, 8, 8);
            using (var a = new RijndaelManaged { Mode = CipherMode.CFB, FeedbackSize = 128, BlockSize = 128, Padding = PaddingMode.None, Key = key.Take(16).ToArray(), IV = iv })
            {
                // CFB-128 over a whole number of blocks; the tail is cut back afterwards
                int n = (data.Length + 15) / 16 * 16; var inp = new byte[n]; Array.Copy(data, inp, data.Length);
                using (var t = encrypt ? a.CreateEncryptor() : a.CreateDecryptor()) { var o = t.TransformFinalBlock(inp, 0, n); return o.Take(data.Length).ToArray(); }
            }
        }

        // ================================================================== walking
        // GETBULK from base until the OIDs leave it, at most max rows; paced so a busy device isn't hammered
        public static List<Vb> Walk(IPEndPoint ep, Cred c, string baseOid, int max, Toolkit.Run run, out string error)
        {
            // one deadline per view (set on the run), so a view of ten walks can't take ten times two minutes
            error = ""; var all = new List<Vb>(); string cur = baseOid.Trim('.'), pre = cur + "."; var deadline = run != null && run.Deadline != default(DateTime) ? run.Deadline : DateTime.UtcNow.AddSeconds(120);
            while (all.Count < max)
            {
                if (run != null && run.Stop) { error = "stopped by you - " + all.Count + " values so far"; return all.Count > 0 ? all : null; }
                if (DateTime.UtcNow > deadline) { error = "stopped at this view's time limit - " + all.Count + " values so far"; return all.Count > 0 ? all : null; }   // a slow device can't keep a walk going for an hour
                var vbs = Request(ep, c, 0xA5, 0, 25, new[] { cur }, out error);
                if (vbs == null) return all.Count > 0 ? all : null;
                bool moved = false;
                foreach (var v in vbs)
                {
                    if (v.End || !v.Oid.StartsWith(pre, StringComparison.Ordinal)) { return all; }
                    if (string.CompareOrdinal(v.Oid, cur) == 0) continue;
                    // every answer must come after the last one (compared number by number): an agent that loops or goes
                    // backwards would repeat data forever and look like a clean finish
                    if (OidCompare(v.Oid, cur) <= 0) { error = "the device's answers stopped moving forward (OID not increasing at " + v.Oid + ") - " + all.Count + " values so far"; return all.Count > 0 ? all : null; }
                    all.Add(v); cur = v.Oid; moved = true; if (all.Count >= max) break;
                }
                if (!moved) break;
                if (run != null) run.Status = all.Count + " values from " + ep.Address + "...";
                Thread.Sleep(20);
            }
            return all;
        }

        public static int OidCompare(string a, string b)
        {
            var x = a.Trim('.').Split('.'); var y = b.Trim('.').Split('.');
            for (int i = 0; i < x.Length && i < y.Length; i++)
            {
                long p, q; long.TryParse(x[i], NumberStyles.None, CultureInfo.InvariantCulture, out p); long.TryParse(y[i], NumberStyles.None, CultureInfo.InvariantCulture, out q);
                if (p != q) return p < q ? -1 : 1;
            }
            return x.Length.CompareTo(y.Length);
        }

        // ================================================================== reading values
        public static string Text(Vb v, string name, MibIndex.Entry col)
        {
            var r = v.Raw;
            switch (v.Type)
            {
                case 2: { if (!HasNum(v)) return r.Length == 0 ? "n/a" : "(too long)"; long n = ReadInt(r, 0, r.Length); string en; if (col != null && col.Enums != null && col.Enums.TryGetValue(n, out en)) return en; return n.ToString(CultureInfo.InvariantCulture); }
                case 0x41: case 0x42: case 0x46: { if (!HasNum(v)) return r.Length == 0 ? "n/a" : "(too long)"; ulong n = 0; foreach (var b in r) n = n << 8 | b; return n.ToString(CultureInfo.InvariantCulture); }
                case 0x43:
                    {
                        if (!HasNum(v)) return r.Length == 0 ? "n/a" : "(too long)"; ulong n = 0; foreach (var b in r) n = n << 8 | b;
                        if (n == 0 && name != null && name.IndexOf("Last", StringComparison.OrdinalIgnoreCase) >= 0) return "not since boot";
                        var ts = TimeSpan.FromMilliseconds(n * 10.0); return (ts.Days > 0 ? ts.Days + " d " : "") + ts.Hours + " h " + ts.Minutes.ToString("00", CultureInfo.InvariantCulture) + " min";
                    }
                case 0x40: return r.Length == 4 ? new IPAddress(r).ToString() : "(bad address)";
                case 6: return OidText(r, 0, r.Length);
                case 5: return "";
                case 0x80: return "(no such object)"; case 0x81: return "(no such instance)"; case 0x82: return "(end)";
                case 4:
                    {
                        // text: printable ASCII, or valid UTF-8 ("café", "Büro") - checked strictly, so a 6-byte name with an accent
                        // isn't taken for a MAC address and a MAC isn't taken for text
                        string txt = Utf8Text(r);
                        if (r.Length == 6 && txt == null) return string.Join(":", r.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
                        if (txt != null) return Regex.Replace(txt, @"\s+", " ").Trim();
                        return "0x" + string.Concat(r.Take(32).Select(b => b.ToString("x2", CultureInfo.InvariantCulture))) + (r.Length > 32 ? "…" : "");
                    }
                default: return "(type 0x" + v.Type.ToString("X2", CultureInfo.InvariantCulture) + ")";
            }
        }
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        static string Utf8Text(byte[] r)
        {
            if (r.Length == 0) return "";
            if (r.Any(b => b < 32 && b != 9 && b != 10 && b != 13 || b == 127)) return null;
            if (r.All(b => b < 128)) return Encoding.ASCII.GetString(r);
            try { var t = StrictUtf8.GetString(r); return t.Any(ch => char.IsControl(ch) && ch != '\t' && ch != '\n' && ch != '\r') ? null : t; } catch (DecoderFallbackException) { return null; } catch (ArgumentException) { return null; }
        }
        // a real number came back: a numeric type of a size its type allows (not NULL, "no such object", empty or too long)
        public static bool HasNum(Vb v)
        {
            if (v == null || v.Raw == null || v.Raw.Length == 0) return false;
            int n = v.Raw.Length, max = v.Type == 2 ? 8 : v.Type == 0x46 ? 9 : v.Type == 0x41 || v.Type == 0x42 || v.Type == 0x43 ? 5 : 0;
            if (max == 0 || n > max) return false;
            return n < max || v.Type == 2 || v.Raw[0] == 0;   // the one extra byte of an unsigned type is only ever a leading zero
        }
        // INTEGER is signed (a sensor precision of -2 is -2, not 254)
        public static long Signed(Vb v) { if (!HasNum(v)) return 0; long n = v.Type == 2 && (v.Raw[0] & 0x80) != 0 ? -1 : 0; foreach (var b in v.Raw) n = n << 8 | b; return n; }
        public static ulong Num(Vb v) { if (v == null || v.Raw.Length == 0 || v.Raw.Length > 9) return 0; ulong n = 0; foreach (var b in v.Raw) n = n << 8 | b; return n; }

        // ================================================================== a table from a walk: rows by index, columns by name
        public sealed class Table { public string Name = ""; public List<string> Cols = new List<string>(); public Dictionary<string, Dictionary<string, string>> Rows = new Dictionary<string, Dictionary<string, string>>(); public List<string> Order = new List<string>(); }
        public static List<Table> Tables(List<Vb> vbs, MibIndex idx, List<string[]> scalars)
        {
            var tables = new Dictionary<string, Table>(); var order = new List<Table>();
            foreach (var v in vbs)
            {
                MibIndex.Entry col; string inst; idx.Column(v.Oid, out col, out inst);
                // a column the index doesn't name, under an entry it does: keep it in the table as "entry.N"
                if (col != null && !col.IsColumn && col.Name.EndsWith("Entry", StringComparison.Ordinal) && inst.Contains("."))
                {
                    string n = inst.Substring(0, inst.IndexOf('.'));
                    col = new MibIndex.Entry { Name = "col." + n, Oid = col.Oid + "." + n, IsColumn = true, Parent = col.Oid.Substring(0, col.Oid.LastIndexOf('.')) };
                    inst = inst.Substring(n.Length + 1);
                }
                string colName = col != null ? col.Name : v.Oid.Substring(0, Math.Max(0, v.Oid.LastIndexOf('.')));
                if (col == null || inst == "" || inst == "0")
                {
                    if (scalars != null) scalars.Add(new[] { col != null ? col.Name + (inst != "" && inst != "0" ? "." + inst : "") : v.Oid, Text(v, col != null ? col.Name : "", col) });
                    continue;
                }
                string tkey = col.Parent ?? colName; Table t;
                if (!tables.TryGetValue(tkey, out t)) { t = new Table { Name = idx.NameOfOid(tkey) ?? tkey }; tables[tkey] = t; order.Add(t); }
                if (!t.Cols.Contains(col.Name)) t.Cols.Add(col.Name);
                Dictionary<string, string> row; if (!t.Rows.TryGetValue(inst, out row)) { row = new Dictionary<string, string>(); t.Rows[inst] = row; t.Order.Add(inst); }
                row[col.Name] = Text(v, col.Name, col);
            }
            return order;
        }
        // admin up but operationally not up: the one pattern worth putting first in any vendor's table
        public static bool Trouble(Table t, Dictionary<string, string> row)
        {
            string adm = t.Cols.FirstOrDefault(c => c.EndsWith("AdminStatus", StringComparison.OrdinalIgnoreCase) || c.EndsWith("AdminState", StringComparison.OrdinalIgnoreCase));
            string op = t.Cols.FirstOrDefault(c => c.EndsWith("OperStatus", StringComparison.OrdinalIgnoreCase) || c.EndsWith("OperState", StringComparison.OrdinalIgnoreCase));
            string a, o; if (adm == null || op == null || !row.TryGetValue(adm, out a) || !row.TryGetValue(op, out o)) return false;
            return (a == "up" || a == "1" || a == "inService" || a == "enabled") && !(o == "up" || o == "1" || o == "inService" || o == "enabled");
        }
    }

    // ================================================================== MIB index: the standard objects AdeeboMeter knows, plus any MIB files you add
    sealed class MibIndex
    {
        public sealed class Entry { public string Name = "", Oid = "", Parent; public Dictionary<long, string> Enums; public bool IsColumn; }
        readonly Dictionary<string, Entry> byOid = new Dictionary<string, Entry>();
        public static string Folder { get { return Path.Combine(Program.DataDir, "mibs"); } }
        static string IndexFile { get { return Path.Combine(Program.DataDir, "mib-index.txt"); } }
        public int Count { get { return byOid.Count; } }
        public int Files;

        // ---------- built in: just what the ready-made views need (standard MIBs, RFC 1213 / 2863 / 2790 / LLDP / ENTITY)
        static readonly Dictionary<long, string> IfTypes = new Dictionary<long, string> { { 1, "other" }, { 6, "ethernet" }, { 24, "loopback" }, { 53, "virtual" }, { 71, "wifi" }, { 131, "tunnel" }, { 135, "vlan" }, { 136, "l3vlan" }, { 150, "mpls tunnel" }, { 161, "lag" }, { 166, "mpls" }, { 209, "bridge" } };
        static readonly Dictionary<long, string> UpDown = new Dictionary<long, string> { { 1, "up" }, { 2, "down" }, { 3, "testing" }, { 4, "unknown" }, { 5, "dormant" }, { 6, "notPresent" }, { 7, "lowerLayerDown" } };
        static readonly string[] Builtin = {
            "1.3.6.1.2.1.1.1|sysDescr", "1.3.6.1.2.1.1.2|sysObjectID", "1.3.6.1.2.1.1.3|sysUpTime", "1.3.6.1.2.1.1.4|sysContact", "1.3.6.1.2.1.1.5|sysName", "1.3.6.1.2.1.1.6|sysLocation",
            "1.3.6.1.2.1.2.2|ifTable", "1.3.6.1.2.1.2.2.1|ifEntry", "1.3.6.1.2.1.2.2.1.1|ifIndex|c", "1.3.6.1.2.1.2.2.1.2|ifDescr|c", "1.3.6.1.2.1.2.2.1.3|ifType|t", "1.3.6.1.2.1.2.2.1.4|ifMtu|c", "1.3.6.1.2.1.2.2.1.5|ifSpeed|c",
            "1.3.6.1.2.1.2.2.1.6|ifPhysAddress|c", "1.3.6.1.2.1.2.2.1.7|ifAdminStatus|u", "1.3.6.1.2.1.2.2.1.8|ifOperStatus|u", "1.3.6.1.2.1.2.2.1.9|ifLastChange|c", "1.3.6.1.2.1.2.2.1.10|ifInOctets|c",
            "1.3.6.1.2.1.2.2.1.13|ifInDiscards|c", "1.3.6.1.2.1.2.2.1.14|ifInErrors|c", "1.3.6.1.2.1.2.2.1.16|ifOutOctets|c", "1.3.6.1.2.1.2.2.1.19|ifOutDiscards|c", "1.3.6.1.2.1.2.2.1.20|ifOutErrors|c",
            "1.3.6.1.2.1.31.1.1|ifXTable", "1.3.6.1.2.1.31.1.1.1|ifXEntry", "1.3.6.1.2.1.31.1.1.1.1|ifName|c", "1.3.6.1.2.1.31.1.1.1.6|ifHCInOctets|c", "1.3.6.1.2.1.31.1.1.1.10|ifHCOutOctets|c", "1.3.6.1.2.1.31.1.1.1.15|ifHighSpeed|c", "1.3.6.1.2.1.31.1.1.1.18|ifAlias|c",
            "1.3.6.1.2.1.25.3.3|hrProcessorTable", "1.3.6.1.2.1.25.3.3.1|hrProcessorEntry", "1.3.6.1.2.1.25.3.3.1.2|hrProcessorLoad|c",
            "1.3.6.1.2.1.25.2.3|hrStorageTable", "1.3.6.1.2.1.25.2.3.1|hrStorageEntry", "1.3.6.1.2.1.25.2.3.1.2|hrStorageType|c", "1.3.6.1.2.1.25.2.3.1.3|hrStorageDescr|c", "1.3.6.1.2.1.25.2.3.1.4|hrStorageAllocationUnits|c", "1.3.6.1.2.1.25.2.3.1.5|hrStorageSize|c", "1.3.6.1.2.1.25.2.3.1.6|hrStorageUsed|c",
            "1.0.8802.1.1.2.1.4.1|lldpRemTable", "1.0.8802.1.1.2.1.4.1.1|lldpRemEntry", "1.0.8802.1.1.2.1.4.1.1.5|lldpRemChassisId|c", "1.0.8802.1.1.2.1.4.1.1.7|lldpRemPortId|c", "1.0.8802.1.1.2.1.4.1.1.8|lldpRemPortDesc|c", "1.0.8802.1.1.2.1.4.1.1.9|lldpRemSysName|c",
            "1.0.8802.1.1.2.1.3.7|lldpLocPortTable", "1.0.8802.1.1.2.1.3.7.1|lldpLocPortEntry", "1.0.8802.1.1.2.1.3.7.1.3|lldpLocPortId|c", "1.0.8802.1.1.2.1.3.7.1.4|lldpLocPortDesc|c",
            "1.3.6.1.2.1.47.1.1.1|entPhysicalTable", "1.3.6.1.2.1.47.1.1.1.1|entPhysicalEntry", "1.3.6.1.2.1.47.1.1.1.1.2|entPhysicalDescr|c", "1.3.6.1.2.1.47.1.1.1.1.7|entPhysicalName|c",
            "1.3.6.1.2.1.99.1.1|entPhySensorTable", "1.3.6.1.2.1.99.1.1.1|entPhySensorEntry", "1.3.6.1.2.1.99.1.1.1.1|entPhySensorType|c", "1.3.6.1.2.1.99.1.1.1.2|entPhySensorScale|c", "1.3.6.1.2.1.99.1.1.1.3|entPhySensorPrecision|c", "1.3.6.1.2.1.99.1.1.1.4|entPhySensorValue|c", "1.3.6.1.2.1.99.1.1.1.5|entPhySensorOperStatus|c",
        };
        public static MibIndex BuiltinOnly()
        {
            var m = new MibIndex();
            foreach (var l in Builtin)
            {
                var p = l.Split('|'); var e = new Entry { Oid = p[0], Name = p[1], IsColumn = p.Length > 2 }; if (p.Length > 2 && p[2] == "u") e.Enums = UpDown; if (p.Length > 2 && p[2] == "t") e.Enums = IfTypes;
                m.byOid[e.Oid] = e;
            }
            foreach (var e in m.byOid.Values) if (e.IsColumn) e.Parent = e.Oid.Substring(0, e.Oid.LastIndexOf('.'));
            foreach (var e in m.byOid.Values.ToList()) if (e.IsColumn && e.Parent != null) { Entry en; if (m.byOid.TryGetValue(e.Parent, out en)) e.Parent = en.Oid.Substring(0, en.Oid.LastIndexOf('.')); }
            return m;
        }

        // ---------- loading: built-in + only the part of your MIB index under the OID being walked (the full index of a
        // vendor's MIBs is tens of thousands of objects - it stays in its file, not in memory)
        static readonly object k = new object();
        public static int FileCount() { return MibFiles().Count(); }
        public static void EnsureBuilt(Action<string> status)
        {
            lock (k)
            {
                string sig = Signature(); if (sig == "") return;
                try { if (!File.Exists(IndexFile) || FirstLine(IndexFile) != "#" + sig) Build(sig, status); }
                catch (Exception ex) { Program.Log("mib index: " + ex.Message); }
            }
        }
        public static MibIndex For(string baseOid)
        {
            var m = BuiltinOnly(); var b = (baseOid ?? "").Trim('.'); m.Files = FileCount();
            if (b == "" || !File.Exists(IndexFile)) return m;
            try
            {
                foreach (var l in File.ReadLines(IndexFile).Skip(1))
                {
                    int t = l.IndexOf('\t'); if (t < 0) continue; string o = l.Substring(0, t);
                    bool under = o == b || o.StartsWith(b + ".", StringComparison.Ordinal), above = b.StartsWith(o + ".", StringComparison.Ordinal);
                    if (!under && !above) continue;
                    var p = l.Split('\t'); if (p.Length < 4) continue;
                    var e = new Entry { Oid = p[0], Name = p[1], IsColumn = p[2] != "", Parent = p[2] == "" ? null : p[2], Enums = ParseEnums(p[3]) };
                    if (!m.byOid.ContainsKey(e.Oid)) m.byOid[e.Oid] = e;
                    if (m.byOid.Count > 20000) break;
                }
            }
            catch (Exception ex) { Program.Log("mib index: " + ex.Message); }
            return m;
        }
        public static string ResolveName(string nameOrOid)
        {
            var s = (nameOrOid ?? "").Trim(); if (Snmp.ValidOid(s)) return s.Trim('.');
            foreach (var l in Builtin) { var p = l.Split('|'); if (string.Equals(p[1], s, StringComparison.OrdinalIgnoreCase)) return p[0]; }
            try
            {
                if (File.Exists(IndexFile))
                    foreach (var l in File.ReadLines(IndexFile).Skip(1)) { var p = l.Split('\t'); if (p.Length > 1 && string.Equals(p[1], s, StringComparison.OrdinalIgnoreCase)) return p[0]; }
            }
            catch (IOException ex) { Program.Log("mib index: " + ex.Message); }   // being rebuilt right now - the built-in names still work
            return null;
        }
        static string FirstLine(string f) { using (var r = new StreamReader(f)) return r.ReadLine() ?? ""; }
        static IEnumerable<FileInfo> MibFiles()
        {
            if (!Directory.Exists(Folder)) return Array.Empty<FileInfo>();
            return new DirectoryInfo(Folder).GetFiles().Where(f => f.Length > 0 && f.Length < 8000000 && (f.Extension == "" || ".mib .my .txt .smi .mib2".Split(' ').Contains(f.Extension.ToLowerInvariant()))).OrderBy(f => f.Name).Take(2000).TakeWhileTotal(200000000);   // 2,000 files and 200 MB at most
        }
        static string Signature()
        {
            var fs = MibFiles().ToList(); if (fs.Count == 0) return "";
            return fs.Count.ToString(CultureInfo.InvariantCulture) + ";" + fs.Sum(f => f.Length).ToString(CultureInfo.InvariantCulture) + ";" + fs.Max(f => f.LastWriteTimeUtc.Ticks).ToString(CultureInfo.InvariantCulture);
        }
        static Dictionary<long, string> ParseEnums(string s)
        {
            if (string.IsNullOrEmpty(s)) return null; var d = new Dictionary<long, string>();
            foreach (var kv in s.Split(',')) { int i = kv.IndexOf('='); long n; if (i > 0 && long.TryParse(kv.Substring(0, i), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) d[n] = kv.Substring(i + 1); }
            return d;
        }

        // ---------- the parser: OBJECT-TYPE / OBJECT IDENTIFIER / *-IDENTITY / TEXTUAL-CONVENTION enums, and DESCRIPTION.
        // A MIB is text; nothing in it is executed, and only these shapes are read.
        // ASN.1 comments run from "--" to the next "--" or the end of the line - but never inside a quoted string
        static string StripComments(string t)
        {
            var sb = new StringBuilder(t.Length); bool q = false;
            for (int i = 0; i < t.Length; i++)
            {
                char c = t[i];
                if (c == '"') { q = !q; sb.Append(c); continue; }
                if (!q && c == '-' && i + 1 < t.Length && t[i + 1] == '-')
                {
                    i += 2; while (i < t.Length && t[i] != '\n' && !(t[i] == '-' && i + 1 < t.Length && t[i + 1] == '-')) i++;
                    if (i < t.Length && t[i] == '-') i++; else if (i < t.Length) sb.Append('\n');
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
        static readonly Regex RxTc = new Regex(@"([A-Za-z][\w-]*)\s*::=\s*TEXTUAL-CONVENTION.*?SYNTAX\s+([\w-]+(?:\s+[\w-]+)?)\s*(\{[^}]*\})?", RegexOptions.Singleline | RegexOptions.Compiled);
        static readonly Regex RxTypeAlias = new Regex(@"^\s*([A-Z][\w-]*)\s*::=\s*(INTEGER|Integer32)\s*(\{[^}]*\})", RegexOptions.Multiline | RegexOptions.Compiled);
        static readonly Regex RxDef = new Regex(@"([a-z][\w-]*)\s+(OBJECT-TYPE|OBJECT IDENTIFIER|MODULE-IDENTITY|OBJECT-IDENTITY)(.*?)::=\s*\{\s*([\w-]+)\s+(\d+)\s*\}", RegexOptions.Singleline | RegexOptions.Compiled);
        static readonly Regex RxEnum = new Regex(@"([\w-]+)\s*\(\s*(-?\d+)\s*\)", RegexOptions.Compiled);
        static readonly Regex RxSyntax = new Regex(@"SYNTAX\s+([\w-]+(?:\s+OF\s+[\w-]+)?)\s*(\{[^}]*\})?", RegexOptions.Compiled);
        static readonly Regex RxDesc = new Regex("DESCRIPTION\\s*\"([^\"]*)\"", RegexOptions.Compiled);
        static readonly Dictionary<string, string> Roots = new Dictionary<string, string> {
            { "iso", "1" }, { "org", "1.3" }, { "dod", "1.3.6" }, { "internet", "1.3.6.1" }, { "directory", "1.3.6.1.1" }, { "mgmt", "1.3.6.1.2" }, { "mib-2", "1.3.6.1.2.1" }, { "transmission", "1.3.6.1.2.1.10" },
            { "experimental", "1.3.6.1.3" }, { "private", "1.3.6.1.4" }, { "enterprises", "1.3.6.1.4.1" }, { "security", "1.3.6.1.5" }, { "snmpV2", "1.3.6.1.6" }, { "snmpModules", "1.3.6.1.6.3" }, { "zeroDotZero", "0.0" },
            { "ieee802dot1mibs", "1.0.8802.1.1" }, { "std", "1.0" }, { "iso8802", "1.0.8802" }, { "ieee802dot1", "1.0.8802.1" } };
        sealed class Raw { public string Parent, Num, Kind, Syntax, Desc; public Dictionary<long, string> Enums; }
        static void Build(string sig, Action<string> status)
        {
            var files = MibFiles().ToList(); int n = 0;
            var lines = BuildLines(files.Select(f => { n++; if (status != null) status("Reading your MIB files (once): " + n + " of " + files.Count + " - " + f.Name); try { return File.ReadAllText(f.FullName); } catch { return ""; } }));
            using (var w = new StreamWriter(IndexFile + ".tmp", false, new UTF8Encoding(false))) { w.WriteLine("#" + sig); foreach (var l in lines) w.WriteLine(l); }
            if (File.Exists(IndexFile)) File.Delete(IndexFile);
            File.Move(IndexFile + ".tmp", IndexFile);
        }
        // MIB texts -> index lines: OID, name, table OID (for a column), enums, description
        public static List<string> BuildLines(IEnumerable<string> texts)
        {
            var defs = new Dictionary<string, Raw>(); var tcs = new Dictionary<string, object>();   // TC name -> enum dict or the type it refines
            foreach (var raw in texts)
            {
                string t = StripComments(raw ?? "");
                foreach (Match m in RxTc.Matches(t))
                {
                    if (m.Groups[3].Success) tcs[m.Groups[1].Value] = Enums(m.Groups[3].Value); else if (!tcs.ContainsKey(m.Groups[1].Value)) tcs[m.Groups[1].Value] = m.Groups[2].Value;
                }
                foreach (Match m in RxTypeAlias.Matches(t)) tcs[m.Groups[1].Value] = Enums(m.Groups[3].Value);
                foreach (Match m in RxDef.Matches(t))
                {
                    string body = m.Groups[3].Value; if (m.Groups[2].Value == "OBJECT IDENTIFIER" && body.Trim() != "") continue;
                    var r = new Raw { Parent = m.Groups[4].Value, Num = m.Groups[5].Value, Kind = m.Groups[2].Value };
                    var sx = RxSyntax.Match(body); if (sx.Success) { r.Syntax = sx.Groups[1].Value; if (sx.Groups[2].Success && (r.Syntax == "INTEGER" || r.Syntax == "Integer32")) r.Enums = Enums(sx.Groups[2].Value); }
                    var d = RxDesc.Match(body); if (d.Success) r.Desc = Regex.Replace(d.Groups[1].Value, @"\s+", " ").Trim();
                    defs[m.Groups[1].Value] = r;
                }
            }
            var oid = new Dictionary<string, string>();
            Func<string, int, string> res = null;
            res = (name, depth) =>
            {
                string o; if (oid.TryGetValue(name, out o)) return o; if (Roots.TryGetValue(name, out o)) return o;
                Raw r; if (depth > 64 || !defs.TryGetValue(name, out r)) return null;
                var p = res(r.Parent, depth + 1); if (p == null) return null; o = p + "." + r.Num; oid[name] = o; return o;
            };
            var lines = new List<string>();
            foreach (var kv in defs)
            {
                var o = res(kv.Key, 0); if (o == null) continue; var r = kv.Value;
                bool col = false; string table = null;
                // a column: its parent is an Entry (an OBJECT-TYPE whose syntax is a SEQUENCE type); the table is the entry's parent
                Raw pr; if (r.Kind == "OBJECT-TYPE" && defs.TryGetValue(r.Parent, out pr) && pr.Kind == "OBJECT-TYPE" && pr.Syntax != null && !pr.Syntax.StartsWith("SEQUENCE OF", StringComparison.Ordinal) && Regex.IsMatch(pr.Syntax, @"^[A-Z]")) { var po = res(r.Parent, 0); if (po != null) { col = true; table = po.Substring(0, po.LastIndexOf('.')); } }
                var en = r.Enums; string sy = r.Syntax; int hops = 0;
                while (en == null && sy != null && hops++ < 6) { object x; if (!tcs.TryGetValue(sy, out x)) break; en = x as Dictionary<long, string>; sy = x as string; }
                lines.Add(o + "\t" + kv.Key + "\t" + (col ? table : "") + "\t" + (en == null ? "" : string.Join(",", en.Take(64).Select(e => e.Key.ToString(CultureInfo.InvariantCulture) + "=" + e.Value))) + "\t" + (r.Desc == null ? "" : r.Desc.Length > 300 ? r.Desc.Substring(0, 300) : r.Desc));
            }
            return lines;
        }
        static Dictionary<long, string> Enums(string s)
        {
            var d = new Dictionary<long, string>(); foreach (Match m in RxEnum.Matches(s)) { long v; if (long.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) d[v] = m.Groups[1].Value; }
            return d.Count > 0 ? d : null;
        }

        // ---------- lookups
        public string NameOfOid(string o) { Entry e; return byOid.TryGetValue(o, out e) ? e.Name : null; }
        // the longest known prefix of a returned OID: the column, and the rest is the row index
        public void Column(string oid, out Entry col, out string inst)
        {
            col = null; inst = ""; string p = oid;
            for (int i = 0; i < 40; i++)
            {
                Entry e; if (byOid.TryGetValue(p, out e)) { col = e; inst = oid.Length > p.Length ? oid.Substring(p.Length + 1) : ""; return; }
                int d = p.LastIndexOf('.'); if (d <= 0) break; p = p.Substring(0, d);
            }
        }
        // search names and descriptions (the index file is read line by line - descriptions never sit in memory)
        public static List<string[]> Search(string words, int max)
        {
            var res = new List<string[]>(); var w = (words ?? "").ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries); if (w.Length == 0) return res;
            Func<string, string, string, string, bool> hit = (oid, name, desc, en) => { var hay = (name + " " + desc).ToLowerInvariant(); return w.All(x => hay.Contains(x)); };
            foreach (var l in Builtin) { var p = l.Split('|'); if (hit(p[0], p[1], "", "")) res.Add(new[] { p[1], p[0], "standard" }); }
            try
            {
                if (File.Exists(IndexFile))
                    foreach (var l in File.ReadLines(IndexFile).Skip(1))
                    {
                        var p = l.Split('\t'); if (p.Length < 5) continue;
                        if (hit(p[0], p[1], p[4], p[3])) { res.Add(new[] { p[1], p[0], p[4] }); if (res.Count >= max) break; }
                    }
            }
            catch (IOException ex) { Program.Log("mib search: " + ex.Message); }
            // tables and columns first - that's what you walk
            return res.OrderBy(r => r[0].EndsWith("Table", StringComparison.Ordinal) ? 0 : r[0].EndsWith("Entry", StringComparison.Ordinal) ? 2 : 1).Take(max).ToList();
        }
    }
    static class FileListExt
    {
        public static IEnumerable<FileInfo> TakeWhileTotal(this IEnumerable<FileInfo> files, long max)
        {
            long t = 0; foreach (var f in files) { t += f.Length; if (t > max) yield break; yield return f; }
        }
    }
}
