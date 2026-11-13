// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    // Certificates & HTTPS: the chain of trust drawn as a ladder (root at the top, your certificate at the bottom), each link
    // checked here on this PC - who signed it, is the signature real, is the signer allowed to sign, are the dates right.
    // The walk is done by AdeeboMeter itself, from your files and Windows' own trust stores: Windows' chain builder can fetch a
    // missing middle certificate from the internet by itself, which would hide exactly what this is for (and send a request).
    // The HTTPS check reads a server's handshake: the certificates it really sends, the TLS version and the cipher in plain
    // parts. It sends no HTTP request. Revocation lists are only fetched when Settings says so (off by default).
    [SuppressMessage("Performance", "CA1861", Justification = "result rows, built once per run")]
    static class CertChain
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }

        [DllImport("crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CryptVerifyCertificateSignatureEx(IntPtr prov, int encoding, int subjectType, IntPtr subject, int issuerType, IntPtr issuer, int flags, IntPtr extra);
        // does the issuer's public key really check the subject's signature? (CryptoAPI, offline)
        internal static bool SignedBy(X509Certificate2 subject, X509Certificate2 issuer)
        {
            try { return CryptVerifyCertificateSignatureEx(IntPtr.Zero, 1 /*X509_ASN_ENCODING*/, 2 /*CERT_CONTEXT*/, subject.Handle, 2 /*CERT_CONTEXT*/, issuer.Handle, 0, IntPtr.Zero); }
            catch (Exception e) { if (!(e is DllNotFoundException || e is EntryPointNotFoundException || e is CryptographicException)) throw; return false; }
        }
        static bool SameName(X500DistinguishedName a, X500DistinguishedName b) { var x = a.RawData; var y = b.RawData; return x.Length == y.Length && x.SequenceEqual(y); }
        static bool SelfSigned(X509Certificate2 c) { return SameName(c.SubjectName, c.IssuerName); }
        static bool IsCa(X509Certificate2 c)
        {
            var bc = c.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault();
            return bc != null ? bc.CertificateAuthority : SelfSigned(c);   // an old v1 root has no constraints at all
        }

        // Windows' own stores: CA (middle certificates), Root (trusted roots) and AuthRoot (the public root list), user and machine
        static List<X509Certificate2> Store(StoreName name)
        {
            var l = new List<X509Certificate2>();
            foreach (var loc in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
                try { using (var st = new X509Store(name, loc)) { st.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly); foreach (X509Certificate2 c in st.Certificates) l.Add(c); } }
                catch (Exception e) { if (!(e is CryptographicException || e is System.Security.SecurityException)) throw; }
            return l;
        }

        public sealed class Link { public X509Certificate2 Cert; public string From = ""; public List<string> Bad = new List<string>(); public bool Warn; }

        // walk from the certificate up: its issuer from the files given (or sent by the server) first, then Windows' stores
        public static List<Link> Walk(X509Certificate2 leaf, List<X509Certificate2> given, out bool missing, out bool trusted, out string missingName)
        {
            var chain = new List<Link> { new Link { Cert = leaf, From = "yours" } };
            missing = false; trusted = false; missingName = "";
            var ca = Store(StoreName.CertificateAuthority); var roots = Store(StoreName.Root).Concat(Store(StoreName.AuthRoot)).ToList();
            var cur = leaf; var now = DateTime.Now;
            for (int depth = 0; depth < 8; depth++)
            {
                // a cross-signed copy of a root this PC trusts (same name, same key): Windows and browsers stop at the trusted one
                if (chain.Count > 1 && !SelfSigned(cur))
                {
                    var c0 = cur; byte[] k0; try { k0 = c0.PublicKey.EncodedKeyValue.RawData; } catch (CryptographicException) { k0 = null; }
                    var anchor = k0 == null ? null : roots.FirstOrDefault(r => SameName(r.SubjectName, c0.SubjectName) && r.PublicKey.EncodedKeyValue.RawData.SequenceEqual(k0));
                    if (anchor != null) { trusted = true; chain[chain.Count - 1].From += " - the same key as a trusted root, so the chain ends here"; break; }
                }
                if (SelfSigned(cur))
                {
                    trusted = roots.Any(r => r.Thumbprint == cur.Thumbprint);
                    if (!SignedBy(cur, cur)) chain[chain.Count - 1].Bad.Add("its own signature doesn't check out");
                    break;
                }
                Func<X509Certificate2, bool> names = c => SameName(c.SubjectName, cur.IssuerName);
                // the candidates whose name matches, the ones that really signed it first, then the ones still in date
                var cands = given.Where(names).Select(c => Tuple.Create(c, "you gave it"))
                    .Concat(ca.Where(names).Select(c => Tuple.Create(c, "Windows' store")))
                    .Concat(roots.Where(names).Select(c => Tuple.Create(c, "Windows' trusted roots")))
                    .OrderBy(t => SignedBy(cur, t.Item1) ? 0 : 1).ThenBy(t => t.Item2 == "Windows' trusted roots" ? 0 : 1).ThenBy(t => now >= t.Item1.NotBefore && now <= t.Item1.NotAfter ? 0 : 1).ToList();   // a trusted root beats a cross-signed copy of it
                if (cands.Count == 0) { missing = true; missingName = cur.IssuerName.Name; break; }
                var up = cands[0];
                var link = new Link { Cert = up.Item1, From = up.Item2 };
                if (!SignedBy(cur, up.Item1)) chain[chain.Count - 1].Bad.Add("the signature doesn't match the one above - not really signed by it");
                if (!IsCa(up.Item1)) link.Bad.Add("it isn't allowed to sign certificates (not a CA) - yet it signed the one below");
                chain.Add(link);
                if (chain.Take(chain.Count - 1).Any(l => l.Cert.Thumbprint == up.Item1.Thumbprint)) { link.Bad.Add("the chain loops back on itself"); break; }
                cur = up.Item1;
            }
            for (int i = 0; i < chain.Count; i++)
            {
                // what today's apps refuse: an RSA key under 2048 bits, a SHA-1 or MD5 signature (a root's own signature doesn't count)
                var l0 = chain[i]; bool selfRoot = SelfSigned(l0.Cert);
                try { using (var rsa = l0.Cert.GetRSAPublicKey()) if (rsa != null && rsa.KeySize < 2048) l0.Bad.Add("weak key: RSA " + rsa.KeySize + " bits (apps want 2048 or more)"); } catch (CryptographicException) { }
                string sig = l0.Cert.SignatureAlgorithm != null ? l0.Cert.SignatureAlgorithm.Value : "";
                if (!selfRoot && (sig == "1.2.840.113549.1.1.5" || sig == "1.2.840.10045.4.1" || sig == "1.2.840.113549.1.1.4" || sig == "1.3.14.3.2.29")) l0.Bad.Add("weak signature: " + (sig == "1.2.840.113549.1.1.4" ? "MD5" : "SHA-1") + " (apps refuse it)");
            }
            foreach (var l in chain)
            {
                if (now > l.Cert.NotAfter) l.Bad.Add("expired on " + l.Cert.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture));
                else if (now < l.Cert.NotBefore) l.Bad.Add("not valid until " + l.Cert.NotBefore.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + " - or this PC's clock is behind");
                else if ((l.Cert.NotAfter - now).TotalDays < 30) l.Warn = true;
            }
            return chain;
        }

        // the ladder rows, root at the top - and one plain sentence on top of them
        public static void Ladder(List<string[]> rows, X509Certificate2 leaf, List<X509Certificate2> given, bool fromServer)
        {
            bool missing, trusted; string missingName;
            var chain = Walk(leaf, given, out missing, out trusted, out missingName);
            // the missing one is the root when the last certificate we have is itself a CA: servers aren't meant to send roots
            bool rootMissing = missing && chain.Count > 1 && IsCa(chain[chain.Count - 1].Cert);
            bool stopped = !missing && !trusted && !SelfSigned(chain[chain.Count - 1].Cert);   // the walk hit its 9-certificate limit
            if (fromServer)
            {
                var eku = leaf.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
                if (eku != null && eku.EnhancedKeyUsages.Count > 0 && !eku.EnhancedKeyUsages.Cast<Oid>().Any(o => o.Value == "1.3.6.1.5.5.7.3.1" || o.Value == "2.5.29.37.0")) chain[0].Bad.Add("not made for servers (its 'used for' list has no server authentication)");
            }
            int at = rows.Count;
            rows.Add(R(-1, "THE CHAIN OF TRUST, ROOT AT THE TOP", "", "", "m"));
            if (stopped) rows.Add(R(1, "…  stopped", "the chain is longer than 9 certificates - the walk stopped here", "Real chains have 2-4. A chain this long is a mistake, or a loop of cross-signs.", "m"));
            if (rootMissing) rows.Add(R(1, "?  root", "not on this PC: " + Draw.Trunc(Cn(missingName), 50), "Windows fetches a public root the first time a site needs it; a private CA's root must be installed. Servers don't need to send it.", "m"));
            else if (missing) rows.Add(R(2, "✗  missing", "the certificate that signed the one below (" + Draw.Trunc(Cn(missingName), 50) + ")", fromServer ? "The server should send it with its own certificate - its admin adds the intermediate (chain) file to the server's setup." : "Add the intermediate file (from the issuer's site or the server's admin) to the box above.", "m"));
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                var l = chain[i]; bool root = i == chain.Count - 1 && SelfSigned(l.Cert) && !missing;
                if (root && !trusted) l.Bad.Add("a root this PC doesn't trust (not in Windows' trusted roots)");
                string role = i == 0 ? "your certificate" : root ? "root" : "middle (intermediate)";
                int lv = l.Bad.Count > 0 ? 2 : l.Warn ? 1 : 0;
                string from = i == 0 ? "" : " · " + (fromServer && l.From == "you gave it" ? "sent by the server" : l.From);
                rows.Add(R(lv, (lv == 2 ? "✗  " : "✓  ") + role, Draw.Trunc(l.Cert.GetNameInfo(X509NameType.SimpleName, false), 50) + " · until " + l.Cert.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + from,
                    l.Bad.Count > 0 ? string.Join("; ", l.Bad) + "." : l.Warn ? "Expires within 30 days." : i == 0 ? "Signed by the one above." : root ? "Trusted by Windows - the top of the ladder." : "Signed by the one above, and allowed to sign the one below.", "m"));
            }
            int plv = chain.Any(l => l.Bad.Count > 0) || (missing && !rootMissing) ? 2 : rootMissing || stopped || !trusted || chain.Any(l => l.Warn) ? 1 : 0;
            rows.Insert(at, R(plv, "In plain words", stopped ? "The chain is longer than 9 certificates, so it wasn't followed to the end - that alone is a problem worth fixing." : rootMissing && chain.All(l => l.Bad.Count == 0) ? "Every certificate here holds, but the root at the top (" + Draw.Trunc(Cn(missingName), 40) + ") isn't on this PC yet. A public root arrives by itself the first time Windows needs it; a private one has to be installed." : Plain(chain, missing, trusted, fromServer), "", "i"));
        }
        static string Cn(string dn) { var m = System.Text.RegularExpressions.Regex.Match(dn ?? "", @"CN=([^,]+)"); return m.Success ? m.Groups[1].Value : dn; }
        static string Plain(List<Link> chain, bool missing, bool trusted, bool fromServer)
        {
            var leaf = chain[0];
            if (leaf.Bad.Any(b => b.StartsWith("expired", StringComparison.Ordinal))) return "Your certificate itself has expired - renew it; the rest of the chain can't help.";
            if (leaf.Bad.Any(b => b.StartsWith("not valid until", StringComparison.Ordinal))) return "Your certificate isn't valid yet - or this PC's clock is behind. Check the clock first (Toolbox › Clock check).";
            if (missing) return leaf.Bad.Count == 0 ? (fromServer ? "Your certificate is fine, but the server forgot to send the middle certificate." : "Your certificate is fine, but the middle certificate is missing - add it to the files above.") : "The middle certificate is missing, and your certificate has a problem too (see below).";
            var bad = chain.Skip(1).FirstOrDefault(l => l.Bad.Count > 0);
            if (chain.Any(l => l.Bad.Any(b => b.StartsWith("weak", StringComparison.Ordinal)))) return "The chain links up, but it uses a weak key or an old signature (RSA under 2048 bits, SHA-1 or MD5) - today's browsers and apps refuse it. Get a new certificate.";
            if (leaf.Bad.Any(b => b.StartsWith("not made for servers", StringComparison.Ordinal))) return "This certificate isn't made for a server - its 'used for' list has no server authentication, so browsers refuse it.";
            if (chain.Any(l => l.Bad.Any(b => b.StartsWith("the signature", StringComparison.Ordinal)))) return "A signature doesn't check out - one certificate wasn't really signed by the one above it. A wrong file, or a tampered one.";
            if (bad != null && bad.Bad.Any(b => b.StartsWith("expired", StringComparison.Ordinal))) return "Your certificate is fine, but one above it has expired - the whole chain fails with it. Get the current intermediate from the issuer.";
            if (bad != null && bad.Bad.Any(b => b.StartsWith("it isn't allowed", StringComparison.Ordinal))) return "A certificate that isn't a CA signed another one - apps refuse that chain.";
            if (!trusted) return "Every link holds, but the top is a root this PC doesn't trust - a private CA (add it to Windows' trusted roots if it's yours) or something in the middle.";
            if (chain.Any(l => l.Warn)) return "Every link holds, up to a root Windows trusts - but one expires within 30 days.";
            return "Every link holds: your certificate, up to a root Windows trusts.";
        }

        // ---------- Toolbox: Chain check - a certificate and (optional) its intermediate / root files, or Windows' store
        public static List<string[]> Check(string input, string others)
        {
            var rows = new List<string[]>(); string label;
            var certs = CertTools.LoadCerts(input, rows, out label); if (certs == null) return rows;
            if (certs.Count == 0) { rows.Add(R(2, label, "no certificate in it", "", "u")); return rows; }
            // a bundle (.p7b, a pasted chain) may be stored root first: "your certificate" is the one that signed none of the others
            var leafC = certs[0];
            if (certs.Count > 1) { var notIssuer = certs.Where(c => !SelfSigned(c) && !certs.Any(o => !ReferenceEquals(o, c) && SameName(o.IssuerName, c.SubjectName))).ToList(); leafC = notIssuer.Count > 0 ? notIssuer[0] : certs[0]; }
            var given = certs.Where(c => !ReferenceEquals(c, leafC)).ToList();
            foreach (var part in (others ?? "").Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x != "").Take(8))
            {
                string l2; var more = CertTools.LoadCerts(part, rows, out l2); if (more != null) given.AddRange(more);
            }
            rows.Add(R(3, label, given.Count == 0 ? "1 certificate - the rest of its chain comes from Windows' own stores" : "1 certificate + " + given.Count + " to build its chain from (then Windows' own stores)", "Checked on this PC: nothing is fetched from the internet.", "m"));
            Ladder(rows, leafC, given, false);
            if (CertCfg.Revocation && !Policy.NoOnline) Revocation(rows, leafC, given);
            foreach (var c in certs.Concat(given)) c.Dispose();
            return rows;
        }

        // ---------- revocation (Settings › off by default): the issuer's CRL / OCSP asked, at most 10 s - the only fetch here
        static void Revocation(List<string[]> rows, X509Certificate2 leaf, List<X509Certificate2> given)
        {
            using (var ch = new X509Chain())
            {
                ch.ChainPolicy.RevocationMode = X509RevocationMode.Online; ch.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                ch.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(10); ch.ChainPolicy.ExtraStore.AddRange(given.ToArray());
                ch.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority | X509VerificationFlags.IgnoreNotTimeValid;
                try { ch.Build(leaf); } catch (CryptographicException e) { rows.Add(R(3, "Revocation", "couldn't check: " + Draw.Trunc(e.Message, 60), "", "m")); return; }
                var st = ch.ChainStatus.Select(s => s.Status).ToList();
                if (st.Contains(X509ChainStatusFlags.Revoked)) rows.Add(R(2, "Revocation", "REVOKED - its issuer has withdrawn it", "Revoked certificates must not be used: a new one is needed.", "m"));
                else if (st.Contains(X509ChainStatusFlags.RevocationStatusUnknown) || st.Contains(X509ChainStatusFlags.OfflineRevocation))
                {
                    // no CRL address and no OCSP address at all: nothing to ask - not a timeout
                    bool where = leaf.Extensions.Cast<X509Extension>().Any(x => x.Oid != null && (x.Oid.Value == "2.5.29.31" || (x.Oid.Value == "1.3.6.1.5.5.7.1.1" && x.Format(false).IndexOf("OCSP", StringComparison.OrdinalIgnoreCase) >= 0)));
                    rows.Add(R(1, "Revocation", !where ? "can't be checked - the certificate names no revocation list or OCSP server (common for a private CA)" : st.Contains(X509ChainStatusFlags.OfflineRevocation) ? "couldn't tell - the issuer's list didn't answer in 10 s" : "couldn't tell - the issuer's list or OCSP server gave no usable answer", "", "m"));
                }
                else rows.Add(R(0, "Revocation", "not revoked (the issuer's list was asked)", "", "m"));
            }
        }

        // ---------- the cipher suite, in four plain parts
        static readonly Dictionary<int, string> Suites = new Dictionary<int, string>
        {
            { 0xC02B, "ECDHE-ECDSA-AES128-GCM-SHA256" }, { 0xC02C, "ECDHE-ECDSA-AES256-GCM-SHA384" }, { 0xC02F, "ECDHE-RSA-AES128-GCM-SHA256" }, { 0xC030, "ECDHE-RSA-AES256-GCM-SHA384" },
            { 0xCCA9, "ECDHE-ECDSA-CHACHA20-POLY1305-SHA256" }, { 0xCCA8, "ECDHE-RSA-CHACHA20-POLY1305-SHA256" },
            { 0xC009, "ECDHE-ECDSA-AES128-CBC-SHA" }, { 0xC00A, "ECDHE-ECDSA-AES256-CBC-SHA" }, { 0xC013, "ECDHE-RSA-AES128-CBC-SHA" }, { 0xC014, "ECDHE-RSA-AES256-CBC-SHA" },
            { 0xC023, "ECDHE-ECDSA-AES128-CBC-SHA256" }, { 0xC024, "ECDHE-ECDSA-AES256-CBC-SHA384" }, { 0xC027, "ECDHE-RSA-AES128-CBC-SHA256" }, { 0xC028, "ECDHE-RSA-AES256-CBC-SHA384" },
            { 0x009E, "DHE-RSA-AES128-GCM-SHA256" }, { 0x009F, "DHE-RSA-AES256-GCM-SHA384" }, { 0x009C, "RSA-AES128-GCM-SHA256" }, { 0x009D, "RSA-AES256-GCM-SHA384" },
            { 0x002F, "RSA-AES128-CBC-SHA" }, { 0x0035, "RSA-AES256-CBC-SHA" }, { 0x003C, "RSA-AES128-CBC-SHA256" }, { 0x003D, "RSA-AES256-CBC-SHA256" }
        };
        static readonly int[] Offer = { 0xC02B, 0xC02F, 0xC02C, 0xC030, 0xCCA9, 0xCCA8, 0xC009, 0xC013, 0xC00A, 0xC014, 0xC023, 0xC027, 0xC024, 0xC028, 0x009E, 0x009F, 0x009C, 0x009D, 0x002F, 0x0035, 0x003C, 0x003D };
        // { key agreement, server's key, encryption, hash } - each with its meaning
        public static string[][] Parts(string suite)
        {
            string s = suite ?? "";
            string ka = s.StartsWith("ECDHE", StringComparison.Ordinal) ? "ECDHE" : s.StartsWith("DHE", StringComparison.Ordinal) ? "DHE" : s.StartsWith("RSA", StringComparison.Ordinal) ? "RSA" : "";
            string auth = s.Contains("-ECDSA-") ? "ECDSA" : s.Contains("-RSA-") || s.StartsWith("RSA", StringComparison.Ordinal) ? "RSA" : "";
            string enc = s.Contains("AES128-GCM") ? "AES-128-GCM" : s.Contains("AES256-GCM") ? "AES-256-GCM" : s.Contains("CHACHA20") ? "ChaCha20-Poly1305" : s.Contains("AES128-CBC") ? "AES-128-CBC" : s.Contains("AES256-CBC") ? "AES-256-CBC" : "";
            string hash = s.EndsWith("SHA384", StringComparison.Ordinal) ? "SHA-384" : s.EndsWith("SHA256", StringComparison.Ordinal) ? "SHA-256" : s.EndsWith("-SHA", StringComparison.Ordinal) ? "SHA-1" : "";
            return Explain(ka, auth, enc, hash);
        }
        static string[][] Explain(string ka, string auth, string enc, string hash)
        {
            return new[]
            {
                new[] { "Key agreement", ka == "" ? "-" : ka, ka == "ECDHE" || ka == "DHE" ? "A fresh secret for every session (forward secrecy): a key stolen later can't open today's traffic." : ka == "RSA" ? "No forward secrecy: anyone who gets the server's key later can open recorded traffic." : "", ka == "RSA" ? "1" : "0" },
                new[] { "Server's key", auth == "" ? "-" : auth, auth == "ECDSA" ? "A small, fast elliptic-curve key proves who the server is." : auth == "RSA" ? "An RSA key proves who the server is." : "", "0" },
                new[] { "Encryption", enc == "" ? "-" : enc, enc.EndsWith("GCM", StringComparison.Ordinal) || enc.StartsWith("ChaCha", StringComparison.Ordinal) ? "Locks the data and seals it, so any change on the way is caught." : enc.EndsWith("CBC", StringComparison.Ordinal) ? "An older mode: still encrypted, but the modern ones (GCM) are preferred." : "", enc.EndsWith("CBC", StringComparison.Ordinal) ? "1" : "0" },
                new[] { "Hash", hash == "" ? "-" : hash, hash == "SHA-1" ? "An old hash - fine for this job inside TLS, but a sign of an old setup." : hash != "" ? "Mixes the keys and checks the handshake wasn't tampered with." : "", hash == "SHA-1" ? "1" : "0" }
            };
        }
        // what Windows negotiated (TLS 1.3 included): the parts from SslStream's own fields
        internal static string[][] PartsFrom(SslStream ssl, X509Certificate2 cert, string proto)
        {
            int kx = (int)ssl.KeyExchangeAlgorithm;
            string ka = proto == "TLS 1.3" ? "ECDHE" : kx == 44550 ? "ECDHE" : kx == (int)ExchangeAlgorithmType.DiffieHellman ? "DHE" : kx == (int)ExchangeAlgorithmType.RsaKeyX || kx == (int)ExchangeAlgorithmType.RsaSign ? "RSA" : "";
            string auth = cert == null ? "" : cert.PublicKey.Oid.Value == "1.2.840.10045.2.1" ? "ECDSA" : cert.PublicKey.Oid.Value == "1.2.840.113549.1.1.1" ? "RSA" : "";
            string enc = ssl.CipherAlgorithm == CipherAlgorithmType.Aes128 ? "AES-128" : ssl.CipherAlgorithm == CipherAlgorithmType.Aes256 ? "AES-256" : ssl.CipherAlgorithm.ToString();
            if (proto == "TLS 1.3" && enc.StartsWith("AES", StringComparison.Ordinal)) enc += "-GCM";
            int h = (int)ssl.HashAlgorithm;
            string hash = ssl.HashAlgorithm == HashAlgorithmType.Sha1 ? "SHA-1" : h == 32780 ? "SHA-256" : h == 32781 ? "SHA-384" : h == 32782 ? "SHA-512" : enc.StartsWith("AES-256", StringComparison.Ordinal) ? "SHA-384" : enc.StartsWith("AES-128", StringComparison.Ordinal) ? "SHA-256" : "";
            return Explain(ka, auth, enc, hash);
        }

        // ---------- the raw read: a TLS 1.2 hello, and the server's answer up to its certificates. No key is agreed, nothing else
        // is sent - the connection is closed right after. In TLS 1.3 the certificates travel encrypted, so only TLS 1.2 shows
        // what the server really sends.
        public sealed class Hello { public int Version, Suite = -1, Alert = -1; public List<byte[]> Certs = new List<byte[]>(); public string Error = ""; }
        public static Hello ReadHello(IPAddress ip, int port, string sni, int version)
        {
            var h = new Hello();
            try
            {
                using (var tc = Toolkit.Open(ip, port, 5000))
                {
                    if (tc == null) { h.Error = "no answer on port " + port; return h; }
                    var s = tc.GetStream(); s.ReadTimeout = 6000; s.WriteTimeout = 6000;
                    var hello = ClientHello(sni, version); s.Write(hello, 0, hello.Length);
                    var hs = new MemoryStream(); var hdr = new byte[5];
                    for (int rec = 0; rec < 64 && hs.Length < 128 * 1024; rec++)
                    {
                        if (!Fill(s, hdr, 5)) break;
                        int type = hdr[0], len = hdr[3] << 8 | hdr[4]; if (len > 18432) { h.Error = "not a TLS answer"; break; }
                        var body = new byte[len]; if (!Fill(s, body, len)) break;
                        if (type == 21) { if (len >= 2) h.Alert = body[1]; break; }
                        if (type != 22) { if (type < 20 || type > 24) h.Error = "not a TLS answer"; break; }
                        hs.Write(body, 0, len);
                        if (Parse(hs.ToArray(), h)) break;   // got the certificates (or the end of the server's hello)
                    }
                    if (h.Version == 0 && h.Alert < 0 && h.Error == "") h.Error = "no TLS answer";
                }
            }
            catch (Exception e) { if (!(e is IOException || e is SocketException || e is ObjectDisposedException)) throw; if (h.Error == "") h.Error = "the connection was closed (" + Draw.Trunc(e.Message, 50) + ")"; }
            return h;
        }
        static bool Fill(NetworkStream s, byte[] b, int n) { int got = 0; while (got < n) { int k = s.Read(b, got, n - got); if (k <= 0) return false; got += k; } return true; }
        // true once there's nothing more to wait for
        static bool Parse(byte[] d, Hello h)
        {
            int p = 0;
            while (p + 4 <= d.Length)
            {
                int t = d[p], len = d[p + 1] << 16 | d[p + 2] << 8 | d[p + 3]; if (p + 4 + len > d.Length) return false;
                int b = p + 4;
                if (t == 2 && len >= 38) { h.Version = d[b] << 8 | d[b + 1]; int sid = d[b + 34]; if (b + 35 + sid + 2 <= b + len) h.Suite = d[b + 35 + sid] << 8 | d[b + 36 + sid]; }
                else if (t == 11 && len >= 3)
                {
                    int total = d[b] << 16 | d[b + 1] << 8 | d[b + 2], q = b + 3, end = Math.Min(b + 3 + total, b + len);
                    while (q + 3 <= end && h.Certs.Count < 10) { int cl = d[q] << 16 | d[q + 1] << 8 | d[q + 2]; q += 3; if (q + cl > end) break; var c = new byte[cl]; Array.Copy(d, q, c, 0, cl); h.Certs.Add(c); q += cl; }
                    return true;
                }
                else if (t == 14) return true;   // ServerHelloDone
                p = b + len;
            }
            return false;
        }
        static byte[] ClientHello(string sni, int version)
        {
            var ext = new List<byte>();
            Action<int, byte[]> add = (type, data) => { ext.Add((byte)(type >> 8)); ext.Add((byte)type); ext.Add((byte)(data.Length >> 8)); ext.Add((byte)data.Length); ext.AddRange(data); };
            IPAddress lit; if (!string.IsNullOrEmpty(sni) && !IPAddress.TryParse(sni, out lit))
            {
                var n = Encoding.ASCII.GetBytes(sni); var sn = new List<byte> { (byte)((n.Length + 3) >> 8), (byte)(n.Length + 3), 0, (byte)(n.Length >> 8), (byte)n.Length }; sn.AddRange(n); add(0x0000, sn.ToArray());
            }
            add(0x000A, new byte[] { 0, 6, 0, 0x1D, 0, 0x17, 0, 0x18 });                 // groups: x25519, P-256, P-384
            add(0x000B, new byte[] { 1, 0 });                                           // point formats: uncompressed
            add(0x000D, new byte[] { 0, 16, 4, 3, 5, 3, 6, 3, 8, 4, 8, 5, 8, 6, 4, 1, 5, 1 });   // signature algorithms
            add(0x0017, Array.Empty<byte>()); add(0xFF01, new byte[] { 0 });                    // extended master secret, renegotiation info
            var body = new List<byte> { (byte)(version >> 8), (byte)version };
            var rnd = new byte[32]; using (var g = RandomNumberGenerator.Create()) g.GetBytes(rnd); body.AddRange(rnd);
            body.Add(0);   // no session id
            body.Add((byte)(Offer.Length * 2 >> 8)); body.Add((byte)(Offer.Length * 2)); foreach (var c in Offer) { body.Add((byte)(c >> 8)); body.Add((byte)c); }
            body.Add(1); body.Add(0);   // compression: none
            body.Add((byte)(ext.Count >> 8)); body.Add((byte)ext.Count); body.AddRange(ext);
            var hs = new List<byte> { 1, (byte)(body.Count >> 16), (byte)(body.Count >> 8), (byte)body.Count }; hs.AddRange(body);
            var rec = new List<byte> { 22, 3, 1, (byte)(hs.Count >> 8), (byte)hs.Count }; rec.AddRange(hs);
            return rec.ToArray();
        }
        static string VersionName(int v) { return v == 0x0304 ? "TLS 1.3" : v == 0x0303 ? "TLS 1.2" : v == 0x0302 ? "TLS 1.1" : v == 0x0301 ? "TLS 1.0" : v == 0x0300 ? "SSL 3.0" : "?"; }

        // ---------- Toolbox: HTTPS check - one site, on click: how the padlock is built, link by link
        public static List<string[]> Https(string target, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string host; int port;
            string t = Toolkit.OneSlash(target); var um = System.Text.RegularExpressions.Regex.Match(t, @"^https?://([^/?#]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase); if (um.Success) t = um.Groups[1].Value;
            if (!Lab.Split(t, 443, out host, out port)) { rows.Add(R(2, "Site", "type a site - e.g. example.com or vpn.example.com:8443", "", "u")); return rows; }
            IPAddress ip; if (!IPAddress.TryParse(host, out ip)) { try { var all = Dns.GetHostAddresses(host); ip = all.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? all.FirstOrDefault(); } catch (SocketException) { ip = null; } catch (ArgumentException) { ip = null; } }
            if (ip == null) { rows.Add(R(2, host, "the name doesn't resolve", "Check the spelling, or DNS (Toolbox › DNS).", "m")); return rows; }
            string sni = host;
            if (sni.Any(ch => ch > 127)) { try { sni = new System.Globalization.IdnMapping().GetAscii(sni); } catch (ArgumentException) { } }   // bücher.de goes out as xn--bcher-kva.de, never "b?cher"

            // 1. Windows' own handshake - what every app on this PC gets: version, cipher, full validation, the name
            run.Status = "TLS handshake with " + host + "...";
            SslPolicyErrors errs = SslPolicyErrors.None; X509Certificate2 cert = null; string proto = "", hsErr = ""; string[][] parts = null; double ms = double.NaN;
            using (var tc = Toolkit.Open(ip, port, 5000))
            {
                if (tc == null) { rows.Add(R(2, host + ":" + port, "no answer on port " + port, "Nothing listens there, or a firewall drops it. Check a destination tells which.", "m")); return rows; }
                using (var ssl = new SslStream(tc.GetStream(), false, (o, c, ch, e) => { errs = e; if (c != null && cert == null) cert = new X509Certificate2(c); return e == SslPolicyErrors.None; }))   // full validation: an untrusted certificate stops the handshake (it's still read for the report)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var allowed = SslProtocols.Tls12 | (DestCheck.Tls13Client() ? (SslProtocols)12288 : 0);
                    var task = Task.Factory.StartNew(() => ssl.AuthenticateAsClient(sni, null, allowed, false));
                    bool done = false;
                    try { done = task.Wait(8000); } catch (AggregateException ae) { Exception ie = ae; while (ie.InnerException != null) ie = ie.InnerException; hsErr = ie.Message; done = true; }
                    if (!done) { tc.Close(); try { task.Wait(1000); } catch (AggregateException) { } hsErr = "the server stopped answering in the middle of the handshake"; }
                    else if (hsErr == "") { ms = sw.Elapsed.TotalMilliseconds; int v = (int)ssl.SslProtocol; proto = v == 12288 ? "TLS 1.3" : v == 3072 ? "TLS 1.2" : ssl.SslProtocol.ToString(); parts = PartsFrom(ssl, cert, proto); }
                }
            }
            // 2. a TLS 1.2 hello of our own: the certificates exactly as the server sends them, and the suite's real name
            run.Status = "Reading the certificates the server sends...";
            var h12 = ReadHello(ip, port, sni, 0x0303);
            bool legacy = false; Hello old = null;
            if (proto == "" && h12.Version == 0)
            {
                // nothing modern answered: one more hello offering TLS 1.0, only to name the problem (nothing is ever sent over it)
                old = ReadHello(ip, port, sni, 0x0301); legacy = old.Version == 0x0301 || old.Version == 0x0302;
            }

            // what it all means, first
            bool nameBad = (errs & SslPolicyErrors.RemoteCertificateNameMismatch) != 0, chainBad = (errs & SslPolicyErrors.RemoteCertificateChainErrors) != 0;
            var now = DateTime.Now; bool expired = cert != null && now > cert.NotAfter, early = cert != null && now < cert.NotBefore;
            List<string[]> clock = null; if (expired || early) clock = WhyFails.Clock();
            bool clockOff = clock != null && clock.Count > 0 && clock[0][0] == "2";
            string verdict; int vlv;
            if (legacy) { verdict = Voice.P("This server is living in 2010: it only speaks " + VersionName(old.Version) + ", which Windows no longer allows.", "The server only speaks " + VersionName(old.Version) + " - Windows no longer allows it."); vlv = 2; }
            else if (cert == null) { verdict = "The handshake was refused before any certificate" + (hsErr != "" ? " (" + Draw.Trunc(hsErr, 60) + ")" : "") + (h12.Alert >= 0 ? " - the server answered with TLS alert " + h12.Alert : "") + "."; vlv = 2; }
            else if (clockOff) { verdict = Voice.P("Your clock is off - every certificate looks " + (early ? "'not yet valid'" : "expired") + " to your PC. Fix the clock and HTTPS comes back.", "This PC's clock is wrong, so the certificate looks " + (early ? "not yet valid" : "expired") + ". Correct the clock."); vlv = 2; }
            else if (expired) { verdict = "The site's certificate expired on " + cert.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + " - the site's owner has to renew it."; vlv = 2; }
            else if (nameBad) { verdict = "The certificate isn't for " + host + " - it's for " + Draw.Trunc(cert.GetNameInfo(X509NameType.SimpleName, false), 40) + ". The wrong server, a wrong name, or something in the middle."; vlv = 2; }
            else if (chainBad) { verdict = "Windows doesn't trust this certificate's chain - see the ladder below for which link breaks."; vlv = 2; }
            else if (proto == "") { verdict = "The certificate checks out, but the handshake didn't finish" + (hsErr != "" ? " (" + Draw.Trunc(hsErr, 60) + ")" : "") + " - the server may want a client certificate, or it stopped answering."; vlv = 2; }
            else { verdict = "A good padlock: " + proto + ", the right name, a chain Windows trusts" + ((cert.NotAfter - now).TotalDays < 30 ? " - but it expires in " + (int)(cert.NotAfter - now).TotalDays + " days" : "") + "."; vlv = (cert.NotAfter - now).TotalDays < 30 ? 1 : 0; }
            rows.Add(R(vlv, host + ":" + port, verdict, "Two short handshakes, no HTTP request to the site: one by Windows (what your apps get), one TLS 1.2 hello to read the certificates as the server sends them." + (clock != null && Cfg.Portal && !Policy.NoOnline ? " The certificate's dates looked wrong, so the clock was also compared with msftconnecttest.com (Settings › Sign-in page check turns that off)." : ""), "m"));

            // the connection
            rows.Add(R(-1, "THE CONNECTION", "", "", "m"));
            rows.Add(R(proto == "" ? 2 : 0, "TLS version", proto == "" && cert != null && h12.Version != 0 ? VersionName(h12.Version) + " - the handshake stopped at the certificate" : proto != "" ? proto + (h12.Version == 0x0303 && proto != "TLS 1.2" ? " (TLS 1.2 works too)" : "") + (double.IsNaN(ms) ? "" : " · handshake " + ms.ToString("0", CultureInfo.CurrentCulture) + " ms") : legacy ? VersionName(old.Version) + " only" : "no handshake", proto != "" ? "" : legacy ? "TLS 1.0 and 1.1 are switched off in current Windows and browsers - the server needs updating." : "", "m"));
            string sname = ""; bool known = h12.Suite >= 0 && Suites.TryGetValue(h12.Suite, out sname); if (!known) sname = h12.Suite >= 0 ? "0x" + h12.Suite.ToString("X4", CultureInfo.InvariantCulture) : "";
            bool via12 = proto == "TLS 1.2" || (proto == "" && h12.Version == 0x0303);   // TLS 1.2 - or the handshake stopped at the certificate: the hello still says which suite
            var shown = via12 && known ? Parts(sname) : parts;
            if (via12 && sname != "") rows.Add(R(3, "Cipher suite", sname, "The four parts below, in one name.", "m"));
            if (shown != null) foreach (var p in shown) rows.Add(R(int.Parse(p[3], CultureInfo.InvariantCulture) == 1 ? 1 : 3, p[0], p[1], p[2], "m"));
            if (cert != null)
            {
                int days = (int)Math.Floor((cert.NotAfter - now).TotalDays);
                rows.Add(R(nameBad ? 2 : 0, "Name matches", nameBad ? "no - it's for " + Draw.Trunc(cert.GetNameInfo(X509NameType.SimpleName, false), 50) : "yes - valid for " + host, "", "m"));
                rows.Add(R(expired ? 2 : early ? 2 : days < 30 ? 1 : 0, "Expires", expired ? ((now - cert.NotAfter).TotalHours < 24 ? "expired " + Math.Max(1, (int)(now - cert.NotAfter).TotalHours) + " h ago" : "expired " + (int)(now - cert.NotAfter).TotalDays + " days ago") : early ? "not valid until " + cert.NotBefore.ToString("d MMM yyyy", CultureInfo.CurrentCulture) : "in " + days + " days (" + cert.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + ")", "", "m"));
                string insp = !chainBad ? Inspection.InspectedBy(cert) : "";
                // a private root can be a company proxy inspecting HTTPS - or simply an intranet / home-lab CA that signs this site itself.
                // Only a public website signed that way points at inspection (Security checkup tests that with www.microsoft.com).
                bool publicSite = ip != null && DestCheck.Kind(ip.ToString()) == "public" && host.Contains(".") && !host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) && !host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase) && !host.EndsWith(".corp", StringComparison.OrdinalIgnoreCase) && !host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) && !host.EndsWith(".home", StringComparison.OrdinalIgnoreCase);
                if (insp != "") rows.Add(R(publicSite ? 1 : 3, "Private root", "signed by a root this PC trusts but the public doesn't: " + Draw.Trunc(insp, 40), publicSite ? Voice.P("On a public website that usually means your company is inspecting HTTPS. Not a fault - just good to know when a site misbehaves.", "On a public site this usually means HTTPS inspection by a company proxy.") : "Normal for an intranet site or your own CA (a NAS, a home lab). On a public website it would mean HTTPS inspection.", "w"));
            }
            if (clock != null) rows.AddRange(clock);

            // the chain: as the server sends it (TLS 1.2), else as Windows built it
            var sent = new List<X509Certificate2>();
            foreach (var der in h12.Certs) try { sent.Add(new X509Certificate2(der)); } catch (CryptographicException) { }
            if (sent.Count > 0) { Ladder(rows, sent[0], sent.Skip(1).ToList(), true); rows.Add(R(3, "Sent by the server", sent.Count + " certificate" + (sent.Count == 1 ? "" : "s"), sent.Count == 1 ? "Only its own - fine if the issuer is a root Windows already has, otherwise the middle one is missing." : "", "m")); }
            else if (cert != null) { Ladder(rows, cert, new List<X509Certificate2>(), false); rows.Add(R(3, "Chain source", "Windows' stores - this server speaks only TLS 1.3, where the certificates travel encrypted", "So a missing middle certificate can't be told apart from one this PC already had.", "m")); }
            if (cert != null && CertCfg.Revocation && !Policy.NoOnline) Revocation(rows, sent.Count > 0 ? sent[0] : cert, sent.Skip(1).ToList());
            foreach (var c in sent) c.Dispose(); if (cert != null) cert.Dispose();
            return rows;
        }
    }

    // the revocation switch (Settings › Privacy & online lookups): off unless you turn it on
    static class CertCfg { public static volatile bool Revocation; }
}
