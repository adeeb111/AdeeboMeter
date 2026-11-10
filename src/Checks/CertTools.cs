// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // Certificates and files: read a certificate, a PFX, a CSR, a hash, Base64 or a JWT - all on this PC, nothing sent.
    // Private keys are never exported, shown or written anywhere; a PFX password is used once and not kept.
    [SuppressMessage("Performance", "CA1861", Justification = "result rows, built once per run")]
    static class CertTools
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        const long MaxFile = 1 << 20;

        // a path to a file, or the PEM / Base64 text pasted straight in
        static byte[] Input(string s, List<string[]> rows, out string label)
        {
            label = ""; s = (s ?? "").Trim().Trim('"');
            if (s == "") { rows.Add(R(2, "File", "type or paste the file's full path - or paste the text itself", "Tip: Shift + right-click the file in Explorer › Copy as path.", "u")); return null; }
            if (s.Contains("-----BEGIN") || (s.Length > 40 && Regex.IsMatch(s, @"^[A-Za-z0-9+/=\s]+$"))) { label = "pasted text"; return Encoding.ASCII.GetBytes(s); }
            try
            {
                if (s.StartsWith(@"\\", StringComparison.Ordinal) || s.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathRooted(s)) { rows.Add(R(2, "File", "type the file's full path, e.g. C:\\certs\\server.cer", "Network paths (\\\\server\\share) aren't read - copy the file here first.", "u")); return null; }
                var fi = new FileInfo(s); if (!fi.Exists) { rows.Add(R(2, "File", "not found: " + Draw.Trunc(s, 80), "", "u")); return null; }
                if (fi.Length > MaxFile) { rows.Add(R(2, "File", "bigger than 1 MB - not a certificate", "", "u")); return null; }
                label = fi.Name; return File.ReadAllBytes(fi.FullName);
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)) throw; rows.Add(R(2, "File", Draw.Trunc(e.Message, 100), "", "u")); return null; }
        }
        // every PEM block of one kind, or the whole thing as DER
        public static List<byte[]> Blocks(byte[] data, string kind)
        {
            var l = new List<byte[]>(); string t = Encoding.ASCII.GetString(data);
            foreach (Match m in Regex.Matches(t, "-----BEGIN " + kind + "-----([A-Za-z0-9+/=\\s]+)-----END " + kind + "-----"))
                try { l.Add(Convert.FromBase64String(Regex.Replace(m.Groups[1].Value, @"\s", ""))); } catch (FormatException) { }
            if (l.Count == 0 && !t.Contains("-----BEGIN"))
            {
                if (data.Length > 0 && data[0] == 0x30) l.Add(data);
                else try { l.Add(Convert.FromBase64String(Regex.Replace(t, @"\s", ""))); } catch (FormatException) { }
            }
            return l;
        }
        static string Hex(byte[] b) { return string.Concat(b.Select(x => x.ToString("X2", CultureInfo.InvariantCulture))); }
        static void Describe(List<string[]> rows, X509Certificate2 c, string label)
        {
            var now = DateTime.Now; int days = (int)(c.NotAfter - now).TotalDays;
            int lv = now > c.NotAfter ? 2 : now < c.NotBefore ? 2 : days < 30 ? 1 : 0;
            rows.Add(R(4, label, c.GetNameInfo(X509NameType.SimpleName, false), "", "m"));
            rows.Add(R(3, "Subject", c.Subject, "", "m"));
            bool self = c.Subject == c.Issuer;
            rows.Add(R(3, "Issued by", c.Issuer + (self ? " (itself - self-signed)" : ""), self ? "Self-signed: browsers and apps won't trust it unless it's added to their trust list - normal for devices and private CAs." : "", "m"));
            rows.Add(R(lv, "Valid", c.NotBefore.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " to " + c.NotAfter.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + (now > c.NotAfter ? " · EXPIRED" : now < c.NotBefore ? " · not valid yet" : " · " + days + " days left"), "", "m"));
            foreach (var e in c.Extensions)
            {
                string n = e.Oid.Value == "2.5.29.17" ? "Names (SAN)" : e.Oid.Value == "2.5.29.37" ? "Used for" : e.Oid.Value == "2.5.29.15" ? "Key usage" : e.Oid.Value == "2.5.29.19" ? "Basic constraints" : null;
                if (n != null) rows.Add(R(3, n, Draw.Trunc(e.Format(false), 400), n == "Names (SAN)" ? "Browsers check the name you typed against these, not the subject." : "", "m"));
            }
            string ka = c.PublicKey.Oid.FriendlyName ?? c.PublicKey.Oid.Value; int bits = KeyBits(c);
            rows.Add(R(ka == "RSA" && bits > 0 && bits < 2048 ? 1 : 3, "Key", ka + (bits > 0 ? " " + bits + " bits" : ""), ka == "RSA" && bits > 0 && bits < 2048 ? "Under 2048 bits is weak; most systems refuse it now." : "", "m"));
            string sa = c.SignatureAlgorithm.FriendlyName ?? c.SignatureAlgorithm.Value;
            rows.Add(R(sa.StartsWith("sha1", StringComparison.OrdinalIgnoreCase) || sa.StartsWith("md5", StringComparison.OrdinalIgnoreCase) ? 1 : 3, "Signed with", sa, sa.StartsWith("sha1", StringComparison.OrdinalIgnoreCase) ? "SHA-1 signatures are no longer trusted by browsers." : "", "m"));
            rows.Add(R(3, "Serial", c.SerialNumber, "", "m"));
            using (var h = SHA256.Create()) rows.Add(R(3, "SHA-256 fingerprint", Hex(h.ComputeHash(c.RawData)), "", "m"));
            rows.Add(R(3, "SHA-1 thumbprint", c.Thumbprint, "What Windows shows as Thumbprint.", "m"));
        }
        static int KeyBits(X509Certificate2 c)
        {
            try { using (var r = c.GetRSAPublicKey()) if (r != null) return r.KeySize; } catch (CryptographicException) { }
            try { using (var e = c.GetECDsaPublicKey()) if (e != null) return e.KeySize; } catch (CryptographicException) { } catch (NotSupportedException) { }
            return 0;
        }

        // ---------- Inspect a certificate file (.cer .crt .pem .der, or a chain in one PEM)
        public static List<string[]> Inspect(string input)
        {
            var rows = new List<string[]>(); string label; var data = Input(input, rows, out label); if (data == null) return rows;
            if (Encoding.ASCII.GetString(data).Contains("PRIVATE KEY")) rows.Add(R(1, "Private key inside", "this file also holds a private key", "Keep it safe - anyone with it can pretend to be this server. AdeeboMeter doesn't read or show the key.", "m"));
            var certs = new List<X509Certificate2>();
            foreach (var b in Blocks(data, "CERTIFICATE").Take(10)) try { certs.Add(new X509Certificate2(b)); } catch (CryptographicException) { }
            if (certs.Count == 0)
            {
                string t = Encoding.ASCII.GetString(data);
                rows.Add(R(2, label, t.Contains("CERTIFICATE REQUEST") ? "this is a certificate request (CSR) - use Decode a CSR" : data.Length > 3 && data[0] == 0x30 && data.Length > 1000 && !t.Contains("BEGIN") ? "not a certificate - if it's a .pfx / .p12, use Check a PFX" : "no certificate found in it", "", "u"));
                return rows;
            }
            rows.Add(R(0, label, certs.Count == 1 ? "1 certificate" : certs.Count + " certificates (a chain)", "Read on this PC - nothing is sent. Revocation isn't checked here (that needs the internet); HTTP and TLS inspector checks a live site.", "m"));
            for (int i = 0; i < certs.Count; i++) Describe(rows, certs[i], certs.Count == 1 ? "Certificate" : "Certificate " + (i + 1));
            if (certs.Count > 1)
                for (int i = 0; i + 1 < certs.Count; i++) if (certs[i].Issuer != certs[i + 1].Subject) rows.Add(R(1, "Chain order", "certificate " + (i + 1) + " isn't issued by certificate " + (i + 2), "Servers should send their own certificate first, then each issuer in turn.", "m"));
            foreach (var c in certs) c.Dispose();
            return rows;
        }

        // ---------- Hash a file and compare: SHA-256, SHA-1 and MD5 at once, against what the download page says
        [SuppressMessage("Security", "CA5350", Justification = "SHA-1 shown only to compare with a published checksum")]
        [SuppressMessage("Security", "CA5351", Justification = "MD5 shown only to compare with a published checksum")]
        public static List<string[]> Hash(string path, string expected, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string p = (path ?? "").Trim().Trim('"');
            if (p == "" || p.StartsWith(@"\\", StringComparison.Ordinal) || p.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathRooted(p)) { rows.Add(R(2, "File", "type the file's full path - e.g. C:\\Downloads\\setup.exe", "Shift + right-click the file in Explorer › Copy as path.", "u")); return rows; }
            FileInfo fi; try { fi = new FileInfo(p); if (!fi.Exists) { rows.Add(R(2, "File", "not found", "", "u")); return rows; } } catch (Exception e) { if (!(e is ArgumentException || e is NotSupportedException || e is IOException || e is UnauthorizedAccessException)) throw; rows.Add(R(2, "File", Draw.Trunc(e.Message, 90), "", "u")); return rows; }
            using (var s256 = SHA256.Create()) using (var s1 = SHA1.Create()) using (var m5 = MD5.Create())
            {
                try
                {
                    using (var f = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16))
                    {
                        var buf = new byte[1 << 20]; int n; long done = 0;
                        while ((n = f.Read(buf, 0, buf.Length)) > 0)
                        {
                            if (run.Stop) { rows.Add(R(1, "Stopped", "stopped by you", "", "m")); return rows; }
                            s256.TransformBlock(buf, 0, n, null, 0); s1.TransformBlock(buf, 0, n, null, 0); m5.TransformBlock(buf, 0, n, null, 0); done += n;
                            run.Status = "Reading " + (done * 100 / Math.Max(1, fi.Length)) + "%...";
                        }
                        s256.TransformFinalBlock(buf, 0, 0); s1.TransformFinalBlock(buf, 0, 0); m5.TransformFinalBlock(buf, 0, 0);
                    }
                }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; rows.Add(R(2, "File", Draw.Trunc(e.Message, 90), "", "u")); return rows; }
                string a = Hex(s256.Hash), b = Hex(s1.Hash), c = Hex(m5.Hash), want = new string((expected ?? "").Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
                if (want != "")
                {
                    string which = want == a ? "SHA-256" : want == b ? "SHA-1" : want == c ? "MD5" : null;
                    rows.Add(which != null ? R(0, "Matches", which + " is the same as the one you pasted", which == "SHA-256" ? "The file is exactly what was published." : which + " is old; a match is still good evidence the download isn't damaged, but SHA-256 is the one to trust.", "m")
                        : R(2, "DOESN'T MATCH", "none of the three is the hash you pasted", "The file is damaged or not the one published - download it again from the official page. Check you copied the whole hash.", "m"));
                }
                rows.Add(R(3, fi.Name, fi.Length.ToString("N0", CultureInfo.CurrentCulture) + " bytes", "", "m"));
                rows.Add(R(3, "SHA-256", a, "", "m")); rows.Add(R(3, "SHA-1", b, "", "m")); rows.Add(R(3, "MD5", c, "", "m"));
            }
            return rows;
        }

        // ---------- Convert a certificate between PEM (text) and DER (binary) - a new file next to it, never over it
        public static List<string[]> ConvertCert(string input)
        {
            var rows = new List<string[]>(); string label; var data = Input(input, rows, out label); if (data == null) return rows;
            string t = Encoding.ASCII.GetString(data);
            if (t.Contains("PRIVATE KEY") || label == "pasted text") { rows.Add(R(2, "Not converted", t.Contains("PRIVATE KEY") ? "the file holds a private key - AdeeboMeter never writes keys" : "type the path of a file - the new file goes next to it", "", "u")); return rows; }
            var certs = Blocks(data, "CERTIFICATE"); X509Certificate2 c = null;
            try { if (certs.Count > 0) c = new X509Certificate2(certs[0]); } catch (CryptographicException) { }
            if (c == null) { rows.Add(R(2, label, "no certificate found in it", "", "u")); return rows; }
            using (c)
            {
                bool toPem = !t.Contains("-----BEGIN"); string src = input.Trim().Trim('"'), dir = Path.GetDirectoryName(src), stem = Path.GetFileNameWithoutExtension(src), ext = toPem ? ".pem" : ".der";
                string outp = Path.Combine(dir, stem + ext); for (int i = 1; File.Exists(outp) && i < 100; i++) outp = Path.Combine(dir, stem + "-" + i + ext);
                var bytes = toPem ? Encoding.ASCII.GetBytes("-----BEGIN CERTIFICATE-----\r\n" + Regex.Replace(Convert.ToBase64String(c.RawData), ".{64}", "$0\r\n").TrimEnd() + "\r\n-----END CERTIFICATE-----\r\n") : c.RawData;
                try { using (var f = new FileStream(outp, FileMode.CreateNew, FileAccess.Write)) f.Write(bytes, 0, bytes.Length); }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; rows.Add(R(2, "Couldn't write", Draw.Trunc(e.Message, 90), "", "m")); return rows; }
                rows.Add(new[] { "0", toPem ? "PEM (text) written" : "DER (binary) written", outp, (certs.Count > 1 ? "Only the first of " + certs.Count + " certificates. " : "") + "PEM is the text form (-----BEGIN CERTIFICATE-----), DER the binary one - same certificate. Click to show it in its folder.", "m", "show:" + outp });
            }
            return rows;
        }

        // ---------- Check a PFX / P12: what's inside, is the key there and does it match, is the chain complete - nothing exported
        public static List<string[]> Pfx(string input, string password)
        {
            var rows = new List<string[]>(); string label; var data = Input(input, rows, out label); if (data == null) return rows;
            var col = new X509Certificate2Collection();
            try { col.Import(data, password ?? "", (X509KeyStorageFlags)32 /* EphemeralKeySet: the key never touches disk */); }
            catch (CryptographicException e)
            {
                bool pw = e.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 || (uint)e.HResult == 0x80070056;
                rows.Add(R(2, label, pw ? "wrong password" : "couldn't open it - " + Draw.Trunc(e.Message, 80), pw ? "" : "Check the password first. Otherwise it isn't a PFX / P12, or was made with an algorithm Windows can't read (older OpenSSL used RC2 / 3DES; newer uses AES - both are fine on Windows 10 and 11).", "m")); return rows;
            }
            try
            {
                var keyed = col.Cast<X509Certificate2>().Where(c => c.HasPrivateKey).ToList();
                rows.Add(R(keyed.Count == 1 ? 0 : 1, label, col.Count + " certificates · " + (keyed.Count == 0 ? "NO private key" : keyed.Count == 1 ? "1 with its private key" : keyed.Count + " with private keys"), keyed.Count == 0 ? "Without the key a server can't use it - export again with \"include the private key\"." : "The key was checked in memory only - it's never exported, shown or saved.", "m"));
                foreach (var c in keyed)
                {
                    bool match = false; try { using (var r = c.GetRSAPrivateKey()) if (r != null) match = true; } catch (CryptographicException) { }
                    if (!match) try { using (var e = c.GetECDsaPrivateKey()) if (e != null) match = true; } catch (CryptographicException) { } catch (NotSupportedException) { }
                    rows.Add(R(match ? 0 : 2, "Key", match ? "present and usable for " + c.GetNameInfo(X509NameType.SimpleName, false) : "present but Windows can't use it", "", "m"));
                }
                foreach (var c in col.Cast<X509Certificate2>().OrderBy(c => c.HasPrivateKey ? 0 : 1).Take(10)) Describe(rows, c, c.HasPrivateKey ? "Certificate (with key)" : c.Subject == c.Issuer ? "Root CA" : "Issuer");
                var leaf = keyed.FirstOrDefault();
                if (leaf != null)
                {
                    using (var ch = new X509Chain())
                    {
                        ch.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; ch.ChainPolicy.ExtraStore.AddRange(col); ch.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                        ch.Build(leaf); int n = ch.ChainElements.Count; bool selfEnd = n > 0 && ch.ChainElements[n - 1].Certificate.Subject == ch.ChainElements[n - 1].Certificate.Issuer;
                        rows.Add(R(selfEnd ? 0 : 1, "Chain", n + " levels" + (selfEnd ? " up to a root" : " - stops before a root"), selfEnd ? "" : "Servers need the intermediate certificates too; add them to the PFX or the server's chain.", "m"));
                    }
                }
            }
            finally { foreach (var c in col) c.Dispose(); }
            return rows;
        }

        // ---------- Decode a CSR (PKCS#10): who it asks for, which names, which key
        sealed class Der { public int Tag, Pos, Start, Len; public int End { get { return Start + Len; } } }
        static Der Read(byte[] b, int p)
        {
            if (p + 2 > b.Length) return null; int tag = b[p], l = b[p + 1], s = p + 2;
            if ((l & 0x80) != 0) { int k = l & 0x7F; if (k < 1 || k > 3 || s + k > b.Length) return null; l = 0; for (int i = 0; i < k; i++) l = l << 8 | b[s + i]; s += k; }
            return s + l <= b.Length ? new Der { Tag = tag, Pos = p, Start = s, Len = l } : null;
        }
        static List<Der> Kids(byte[] b, Der d) { var l = new List<Der>(); int p = d.Start; while (p < d.End) { var k = Read(b, p); if (k == null) break; l.Add(k); p = k.End; } return l; }
        static string Oid(byte[] b, Der d)
        {
            if (d.Len < 1) return ""; var sb = new StringBuilder(); int f = b[d.Start]; sb.Append(f / 40 >= 2 ? 2 : f / 40).Append('.').Append(f / 40 >= 2 ? f - 80 : f % 40); long v = 0;
            for (int i = d.Start + 1; i < d.End; i++) { v = (v << 7) + (b[i] & 0x7F); if ((b[i] & 0x80) == 0) { sb.Append('.').Append(v.ToString(CultureInfo.InvariantCulture)); v = 0; } }
            return sb.ToString();
        }
        static readonly Dictionary<string, string> Algs = new Dictionary<string, string> {
            { "1.2.840.113549.1.1.1", "RSA" }, { "1.2.840.10045.2.1", "EC" }, { "1.3.101.112", "Ed25519" }, { "1.2.840.10045.3.1.7", "P-256" }, { "1.3.132.0.34", "P-384" }, { "1.3.132.0.35", "P-521" },
            { "1.2.840.113549.1.1.11", "SHA-256 with RSA" }, { "1.2.840.113549.1.1.12", "SHA-384 with RSA" }, { "1.2.840.113549.1.1.13", "SHA-512 with RSA" }, { "1.2.840.113549.1.1.5", "SHA-1 with RSA (weak)" },
            { "1.2.840.10045.4.3.2", "ECDSA with SHA-256" }, { "1.2.840.10045.4.3.3", "ECDSA with SHA-384" }, { "1.2.840.10045.4.3.4", "ECDSA with SHA-512" }, { "1.2.840.113549.1.1.10", "RSA-PSS" } };
        static string Alg(string o) { string n; return Algs.TryGetValue(o, out n) ? n : o; }
        public static List<string[]> Csr(string input)
        {
            var rows = new List<string[]>(); string label; var data = Input(input, rows, out label); if (data == null) return rows;
            var blocks = Blocks(data, "CERTIFICATE REQUEST"); if (blocks.Count == 0) blocks = Blocks(data, "NEW CERTIFICATE REQUEST");
            var b = blocks.FirstOrDefault(); Der top = b != null ? Read(b, 0) : null;
            var parts = top != null && top.Tag == 0x30 ? Kids(b, top) : null;
            var info = parts != null && parts.Count == 3 && parts[0].Tag == 0x30 ? Kids(b, parts[0]) : null;
            if (info == null || info.Count < 3) { rows.Add(R(2, label, "not a certificate request (PKCS#10)", Encoding.ASCII.GetString(data).Contains("BEGIN CERTIFICATE-----") ? "This is a certificate - use Inspect a certificate." : "", "u")); return rows; }
            string subject; try { subject = new X500DistinguishedName(b.Skip(info[1].Pos).Take(info[1].End - info[1].Pos).ToArray()).Name; } catch (CryptographicException) { subject = "(unreadable)"; }
            rows.Add(R(0, label, "a certificate request", "Read on this PC - nothing is sent. Its signature isn't checked here.", "m"));
            rows.Add(R(3, "Asks for", subject == "" ? "(empty subject - names are in SAN only)" : subject, "", "m"));
            var spki = Kids(b, info[2]); if (spki.Count == 2)
            {
                var alg = Kids(b, spki[0]); string ka = alg.Count > 0 ? Alg(Oid(b, alg[0])) : "?", extra = "";
                if (ka == "EC" && alg.Count > 1 && alg[1].Tag == 6) extra = " " + Alg(Oid(b, alg[1]));
                if (ka == "RSA") { var bs = spki[1]; var rsa = Read(b, bs.Start + 1); var ints = rsa != null ? Kids(b, rsa) : null; if (ints != null && ints.Count > 0) { int len = ints[0].Len; if (len > 0 && b[ints[0].Start] == 0) len--; extra = " " + (len * 8) + " bits"; if (len * 8 < 2048) ka += " (weak)"; } }
                rows.Add(R(ka.Contains("weak") ? 1 : 3, "Key", ka + extra, ka.Contains("weak") ? "Under 2048 bits - most CAs refuse it." : "", "m"));
            }
            var names = new List<string>();
            if (info.Count > 3 && info[3].Tag == 0xA0)
                foreach (var attr in Kids(b, info[3]))
                {
                    var ak = Kids(b, attr); if (ak.Count < 2 || Oid(b, ak[0]) != "1.2.840.113549.1.9.14") continue;
                    foreach (var exts in Kids(b, ak[1])) foreach (var ext in Kids(b, exts))
                        {
                            var ek = Kids(b, ext); if (ek.Count < 2 || Oid(b, ek[0]) != "2.5.29.17") continue;
                            var os = ek[ek.Count - 1]; var gn = Read(b, os.Start); if (gn == null) continue;
                            foreach (var n in Kids(b, gn))
                            {
                                if (n.Tag == 0x82) names.Add("DNS " + Encoding.ASCII.GetString(b, n.Start, n.Len));
                                else if (n.Tag == 0x87 && (n.Len == 4 || n.Len == 16)) names.Add("IP " + new IPAddress(b.Skip(n.Start).Take(n.Len).ToArray()));
                                else if (n.Tag == 0x81) names.Add("email " + Encoding.ASCII.GetString(b, n.Start, n.Len));
                                else if (n.Tag == 0x86) names.Add("URI " + Encoding.ASCII.GetString(b, n.Start, n.Len));
                            }
                        }
                }
            rows.Add(R(names.Count == 0 ? 1 : 3, "Names (SAN)", names.Count == 0 ? "none" : string.Join(", ", names.Take(50)), names.Count == 0 ? "Browsers ignore the subject's CN - without SAN names the certificate won't work for a website." : "", "m"));
            var sig = Kids(b, parts[1]); if (sig.Count > 0) { string sa = Alg(Oid(b, sig[0])); rows.Add(R(sa.Contains("weak") ? 1 : 3, "Signed with", sa, "", "m")); }
            return rows;
        }

        // ---------- Decode Base64 or a JWT: text, a hint about what the bytes are, a token's claims and dates (signature not checked)
        static byte[] B64(string s)
        {
            s = Regex.Replace(s ?? "", @"\s", "").Replace('-', '+').Replace('_', '/'); s = s.TrimEnd('=');
            if (s.Length == 0 || s.Length % 4 == 1 || !Regex.IsMatch(s, @"^[A-Za-z0-9+/]+$")) return null;
            try { return Convert.FromBase64String(s + new string('=', (4 - s.Length % 4) % 4)); } catch (FormatException) { return null; }
        }
        public static List<string[]> Decode(string input)
        {
            var rows = new List<string[]>(); string s = (input ?? "").Trim(); if (s.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7).Trim();
            if (s == "" || s.Length > 50000) { rows.Add(R(2, "Text", s == "" ? "paste Base64 text or a JWT (eyJ...)" : "too long (50,000 characters at most)", "", "u")); return rows; }
            var p = s.Split('.');
            if (p.Length == 3 && s.StartsWith("eyJ", StringComparison.Ordinal))
            {
                var h = B64(p[0]); var b = B64(p[1]);
                if (h != null && b != null)
                {
                    string hs = Encoding.UTF8.GetString(h), bs = Encoding.UTF8.GetString(b);
                    rows.Add(R(0, "JWT", "a signed token · " + Regex.Match(hs, "\"alg\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value, "The signature is NOT checked - AdeeboMeter has no key. Don't trust these claims for security decisions. A token is a password while it's valid: don't paste live ones into other tools.", "m"));
                    rows.Add(R(3, "Header", Draw.Trunc(hs, 600), "", "m"));
                    rows.Add(R(3, "Claims", Draw.Trunc(bs, 2000), "", "m"));
                    foreach (var k in new[] { "iat", "nbf", "exp" })
                    {
                        var m = Regex.Match(bs, "\"" + k + "\"\\s*:\\s*(\\d{9,11})"); if (!m.Success) continue;
                        var t = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToLocalTime();
                        bool exp = k == "exp" && t < DateTime.Now; rows.Add(R(exp ? 1 : 3, k == "iat" ? "Issued" : k == "nbf" ? "Not before" : "Expires", t.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) + (exp ? " · EXPIRED" : ""), "", "m"));
                    }
                    return rows;
                }
            }
            var d = B64(s); if (d == null) { rows.Add(R(2, "Text", "not Base64 or a JWT", "Base64 uses A-Z a-z 0-9 + / (or - _) and = at the end.", "u")); return rows; }
            bool text = d.All(x => x == 9 || x == 10 || x == 13 || x >= 32);
            string hint = d.Length > 4 && d[0] == 0x30 && (d[1] & 0x80) != 0 ? "Binary ASN.1 (DER) - probably a certificate, key or CSR: paste it into Inspect a certificate or Decode a CSR." : d.Length > 2 && d[0] == 0x1F && d[1] == 0x8B ? "Gzip-compressed data." : d.Length > 3 && d[0] == 0x50 && d[1] == 0x4B ? "A ZIP file (or .docx / .xlsx)." : d.Length > 3 && d[0] == 0x89 && d[1] == 0x50 ? "A PNG image." : "";
            rows.Add(R(0, "Decoded", d.Length + " bytes" + (text ? " of text" : " of binary"), hint, "m"));
            rows.Add(R(3, text ? "Text" : "Bytes", text ? Draw.Trunc(Encoding.UTF8.GetString(d), 3000) : string.Join(" ", d.Take(256).Select(x => x.ToString("x2", CultureInfo.InvariantCulture))) + (d.Length > 256 ? " …" : ""), "", "m"));
            return rows;
        }

        // ---------- Certificate commands: certutil, PowerShell and (when it's installed) openssl, filled in - click to copy
        static string[] Cmd(string name, string cmd, string why) { return new[] { "3", name, cmd, why, "", "copy:" + cmd }; }
        static readonly string[] OpensslSpots = { @"C:\Program Files\Git\usr\bin\openssl.exe", @"C:\Program Files\OpenSSL-Win64\bin\openssl.exe" };
        static readonly char[] Unsafe = { '"', '\'', '`', '$', ';', '&', '|', '<', '>', '\r', '\n' };
        public static string FindOpenssl()
        {
            foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                try { var f = Path.Combine(d.Trim().Trim('"'), "openssl.exe"); if (d.Trim() != "" && File.Exists(f)) return f; } catch (ArgumentException) { }
            foreach (var f in OpensslSpots) if (File.Exists(f)) return f;
            return null;
        }
        public static List<string[]> Commands(string file)
        {
            var rows = new List<string[]>(); string f = (file ?? "").Trim().Trim('"');
            if (f == "") f = "C:\\certs\\server.cer";
            else if (f.Length > 240 || f.IndexOfAny(Unsafe) >= 0 || f.IndexOfAny(Path.GetInvalidPathChars()) >= 0) { rows.Add(R(2, "File", f.Length > 240 ? "that path is too long" : "that path has characters that aren't safe in a command", "", "u")); return rows; }
            string q = "\"" + f + "\"", stem = Path.Combine(Path.GetDirectoryName(f) ?? "", Path.GetFileNameWithoutExtension(f)), os = FindOpenssl();
            rows.Add(new[] { "0", "How to use", "click a command to copy it, then paste it into PowerShell", "Commands are filled in with your file. Nothing runs from here. Exporting a private key is left out on purpose - do that only on the server that owns it. Click this line to open PowerShell.", "w", "ps:" });
            rows.Add(R(4, "Look at a certificate", "", "", "m"));
            rows.Add(Cmd("Everything in it", "certutil -dump " + q, "Subject, issuer, dates, names, key, extensions."));
            rows.Add(Cmd("Check the chain and revocation", "certutil -verify -urlfetch " + q, "Builds the chain and asks the CA's revocation lists (needs the internet)."));
            rows.Add(Cmd("Fingerprint", "(Get-PfxCertificate " + q + ").Thumbprint", "The SHA-1 thumbprint Windows shows."));
            rows.Add(R(4, "Convert", "", "", "m"));
            rows.Add(Cmd("DER -> PEM (text)", "certutil -encode " + q + " \"" + stem + ".pem\"", "Base64 text form, for Linux, network gear and web servers."));
            rows.Add(Cmd("PEM -> DER (binary)", "certutil -decode " + q + " \"" + stem + ".der\"", "Binary form - what Windows calls .cer (DER encoded)."));
            rows.Add(R(4, "Files and hashes", "", "", "m"));
            rows.Add(Cmd("SHA-256 of a file", "certutil -hashfile " + q + " SHA256", "Compare with the hash on the download page."));
            rows.Add(Cmd("Same in PowerShell", "Get-FileHash " + q + " -Algorithm SHA256", ""));
            rows.Add(R(4, "Windows' certificate stores", "", "", "m"));
            rows.Add(Cmd("My certificates", "Get-ChildItem Cert:\\CurrentUser\\My | Format-List Subject, NotAfter, Thumbprint", "No admin needed for your own store."));
            rows.Add(Cmd("Trusted roots", "Get-ChildItem Cert:\\CurrentUser\\Root | Sort NotAfter | Format-Table NotAfter, Subject -AutoSize", ""));
            rows.Add(Cmd("Open the manager", "certmgr.msc", "Your own certificates; the machine store (certlm.msc) needs admin."));
            if (os != null)
            {
                string o = "& \"" + os + "\"";
                rows.Add(R(4, "openssl (found on this PC)", os, "", "m"));
                rows.Add(Cmd("Read a certificate", o + " x509 -in " + q + " -noout -text", ""));
                rows.Add(Cmd("Read a CSR", o + " req -in " + q + " -noout -text -verify", "Also checks the CSR's signature."));
                rows.Add(Cmd("What's in a PFX", o + " pkcs12 -in " + q + " -info -nokeys", "-nokeys: the certificates only, never the key."));
                rows.Add(Cmd("A live server's chain", o + " s_client -connect example.com:443 -servername example.com -showcerts", "Type the server's name in both places."));
                rows.Add(Cmd("New key and CSR", o + " req -new -newkey rsa:2048 -nodes -keyout \"" + stem + ".key\" -out \"" + stem + ".csr\"", "The .key file is your private key - keep it on the server that will use it."));
            }
            else rows.Add(R(3, "openssl", "not found on this PC", "Not needed - certutil and PowerShell above cover the everyday jobs. Git for Windows includes openssl if you have it installed elsewhere.", "m"));
            return rows;
        }
    }
}
