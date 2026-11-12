// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // ================================================================== Certificates and keys
    // My keys live in Windows' own certificate store for your account (Current User › Personal - no admin), marked with the
    // name "AdeeboMeter: ...". Windows protects the private keys; AdeeboMeter lists and uses them and never writes a private
    // key to a file of its own. The only way a private key leaves is "Back up private key": a password-protected PFX, with a
    // recovery passphrase made for you and shown once. Recipients are other people's public certificates, kept as .cer files.
    // Everything is offline except "Compare file vs live server" (one TLS handshake).
    static class CertKeys
    {
        public const string Prefix = "AdeeboMeter: ";
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        public static string OutDir { get { var d = Path.Combine(Program.DataDir, "certs"); Directory.CreateDirectory(d); return d; } }
        static string RecipDir { get { var d = Path.Combine(Program.DataDir, "recipients"); Directory.CreateDirectory(d); return d; } }
        static string Safe(string s, int max) { s = new string((s ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.' || c == '@').ToArray()).Trim(); return s.Length > max ? s.Substring(0, max) : s; }
        static string Pem(string label, byte[] der) { var b = Convert.ToBase64String(der); var sb = new StringBuilder("-----BEGIN " + label + "-----\r\n"); for (int i = 0; i < b.Length; i += 64) sb.Append(b.Substring(i, Math.Min(64, b.Length - i))).Append("\r\n"); return sb.Append("-----END " + label + "-----\r\n").ToString(); }

        // ---------- the store
        public static List<X509Certificate2> Mine()
        {
            using (var st = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                st.Open(OpenFlags.ReadOnly);
                return st.Certificates.Cast<X509Certificate2>().Where(c => c.HasPrivateKey && (c.FriendlyName ?? "").StartsWith(Prefix, StringComparison.Ordinal)).OrderBy(c => c.FriendlyName, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
        public static string NameOf(X509Certificate2 c) { return (c.FriendlyName ?? "").StartsWith(Prefix, StringComparison.Ordinal) ? c.FriendlyName.Substring(Prefix.Length) : c.GetNameInfo(X509NameType.SimpleName, false); }
        static X509Certificate2 Find(string nameOrThumb)
        {
            string q = (nameOrThumb ?? "").Trim();
            return Mine().FirstOrDefault(c => string.Equals(c.Thumbprint, q, StringComparison.OrdinalIgnoreCase) || string.Equals(NameOf(c), q, StringComparison.OrdinalIgnoreCase));
        }
        public static X509Certificate2 MyEncryptionCert() { return Mine().FirstOrDefault(c => Purpose(c) == "encrypt"); }
        static string Purpose(X509Certificate2 c)
        {
            var eku = c.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
            if (eku != null && eku.EnhancedKeyUsages.Cast<Oid>().Any(o => o.Value == "1.3.6.1.5.5.7.3.1")) return "server";
            if (eku != null && eku.EnhancedKeyUsages.Cast<Oid>().Any(o => o.Value == "1.3.6.1.5.5.7.3.2")) return "client";
            return "encrypt";
        }
        // a new RSA key, kept in Windows with a certificate wrapped around it (self-signed until a CA signs a CSR for it)
        public static X509Certificate2 Create(string name, string preset, IList<string> sans, int days)
        {
            using (var rsa = RSA.Create(3072))
            {
                var req = new CertificateRequest("CN=" + Cn(name), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                Decorate(req, preset, sans);
                using (var c = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(days)))
                    return Persist(c, name);
            }
        }
        static string Cn(string s) { return Safe(s, 64).Replace(",", " ").Replace("=", " "); }
        static void Decorate(CertificateRequest req, string preset, IList<string> sans)
        {
            bool server = preset == "https" || preset == "lab", client = preset == "mtls";
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(server || client ? X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment : X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.DigitalSignature, true));
            if (server || client) req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(server ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2") }, false));
            if (sans != null && sans.Count > 0)
            {
                var b = new SubjectAlternativeNameBuilder();
                foreach (var s in sans) { IPAddress ip; if (IPAddress.TryParse(s, out ip)) b.AddIpAddress(ip); else b.AddDnsName(s); }
                req.CertificateExtensions.Add(b.Build());
            }
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));
        }
        // into Windows' store: the key is kept by Windows for this user, marked exportable only so you can back it up
        static X509Certificate2 Persist(X509Certificate2 withKey, string name)
        {
            var pw = Convert.ToBase64String(Guid.NewGuid().ToByteArray());   // only for the hand-over to Windows, then gone
            var pfx = withKey.Export(X509ContentType.Pfx, pw);
            var stored = new X509Certificate2(pfx, pw, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
            Array.Clear(pfx, 0, pfx.Length);
            stored.FriendlyName = Prefix + Safe(name, 60);
            using (var st = new X509Store(StoreName.My, StoreLocation.CurrentUser)) { st.Open(OpenFlags.ReadWrite); st.Add(stored); }
            return stored;
        }
        static readonly char[] SanSep = { ',', ' ', ';' };
        static List<string> Sans(string s)
        {
            return (s ?? "").Split(SanSep, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => Games.ValidHost(x) || x.StartsWith("*.", StringComparison.Ordinal) && Games.ValidHost(x.Substring(2))).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToList();
        }

        // ---------- the tools
        public static List<string[]> MyKeys()
        {
            var rows = new List<string[]>(); var l = Mine(); var ec = EcKeys.All();
            foreach (var k in ec)
            {
                rows.Add(R(-1, k.Name.ToUpperInvariant(), "for files encrypted to you · EC P-384 (strongest, opens .adeebo files - AdeeboMeter only) · made " + k.Made.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + " · fingerprint " + EcKeys.Fingerprint(Convert.FromBase64String(k.Pub)), "", "w"));
                rows.Add(new[] { "3", "Copy public key", "a .adeebokey file to send - only AdeeboMeter uses it", "", "w", "key:pub:ec:" + k.KeyName });
                rows.Add(new[] { "3", "Back up private key", "an encrypted .adeebo backup, with a strong recovery passphrase shown once", "", "w", "key:backup:ec:" + k.KeyName });
                rows.Add(new[] { "3", "Delete", "remove it from Windows - asks first", "", "w", "key:del:ec:" + k.KeyName });
            }
            if (l.Count == 0 && ec.Count == 0) rows.Add(R(3, "My keys", "none yet - make one with New key", "Kept by Windows for your account (Current User › Personal). AdeeboMeter never keeps a private key itself.", "w"));
            foreach (var c in l)
            {
                string p = Purpose(c); double days = (c.NotAfter - DateTime.Now).TotalDays;
                rows.Add(R(-1, NameOf(c).ToUpperInvariant(), (p == "server" ? "server certificate" : p == "client" ? "client certificate (mTLS)" : "for files encrypted to you") + " · RSA " + c.PublicKey.Key.KeySize + " · " + (c.Subject == c.Issuer ? "self-signed" : "from " + c.GetNameInfo(X509NameType.SimpleName, true)) + " · expires " + c.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + (days < 30 ? " (soon)" : ""), "", "w"));
                rows.Add(new[] { "3", "Copy public key", "a .cer someone needs to encrypt a file for you - copied, and saved in AdeeboMeter's certs folder", "", "w", "key:pub:" + c.Thumbprint });
                rows.Add(new[] { "3", "Back up private key", "a password-protected .pfx for a new PC, with a strong recovery passphrase shown once", "", "w", "key:backup:" + c.Thumbprint });
                rows.Add(new[] { "3", "Delete", "remove it from Windows - asks first", "", "w", "key:del:" + c.Thumbprint });
                c.Dispose();
            }
            return rows;
        }
        // purpose: encrypt (files to you) | server | client
        public static List<string[]> NewKey(string name, string purpose, string type)
        {
            var rows = new List<string[]>(); string n = Safe(name, 60);
            if (n == "") { rows.Add(R(2, "Name", "give the key a name - e.g. Adeeb work", "", "")); return rows; }
            if (Find(n) != null || EcKeys.Find(n) != null) { rows.Add(R(2, "Name", "you already have a key called " + n, "", "")); return rows; }
            string p = (purpose ?? "").Trim().ToLowerInvariant(), ty = (type ?? "").Trim().ToLowerInvariant().Replace("-", "").Replace(" ", "");
            bool files = !p.StartsWith("serv", StringComparison.Ordinal) && !p.StartsWith("cli", StringComparison.Ordinal) && p != "mtls";
            if (files && (ty == "p384" || ty == "ec" || ty == "ecp384" || ty == "strongest"))
            {
                if (!EcKeys.Available) { rows.Add(R(2, "EC P-384", "this Windows can't make P-384 keys here", "", "w")); return rows; }
                try { var k = EcKeys.Create(n, null); rows.Add(R(0, "Made", n + " · EC P-384 · kept by Windows for your account · fingerprint " + EcKeys.Fingerprint(Convert.FromBase64String(k.Pub)), "Files encrypted to this key are .adeebo files - only AdeeboMeter opens them. Next: Copy public key (in My keys) and send the .adeebokey file.", "w")); }
                catch (CryptographicException e) { rows.Add(R(2, "New key", e.Message, "", "w")); }
                return rows;
            } string preset = p.StartsWith("serv", StringComparison.Ordinal) ? "lab" : p.StartsWith("cli", StringComparison.Ordinal) || p == "mtls" ? "mtls" : "encrypt";
            try { using (var c = Create(n, preset, null, 3 * 365)) rows.Add(R(0, "Made", n + " · RSA 3072 · kept by Windows for your account", "Next: Copy public key (in My keys) and send it to anyone who'll encrypt files for you.", "w")); }
            catch (CryptographicException e) { rows.Add(R(2, "New key", e.Message, "", "w")); }
            return rows;
        }
        // CSR: for a saved key, or a new one made now
        public static List<string[]> Csr(string keyName, string names, string subject)
        {
            var rows = new List<string[]>(); var sans = Sans(names); string kn = Safe(keyName, 60);
            if (kn == "") { rows.Add(R(2, "Key", "type a key name - an existing one from My keys, or a new name to make one", "", "")); return rows; }
            if (sans.Count == 0) { rows.Add(R(2, "Names", "type the DNS names and IP addresses the certificate is for - e.g. vpn.example.com, 10.0.0.5", "", "")); return rows; }
            string cn = string.IsNullOrWhiteSpace(subject) ? sans[0] : subject.Trim();
            var key = Find(kn); bool made = false;
            try
            {
                if (key == null) { key = Create(kn, "lab", sans, 30); made = true; }
                using (var rsa = key.GetRSAPrivateKey())
                {
                    var req = new CertificateRequest(Dn(cn), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); Decorate(req, "lab", sans);
                    string pem = Pem("CERTIFICATE REQUEST", req.CreateSigningRequest());
                    string file = Path.Combine(OutDir, Safe(kn, 40).Replace(' ', '-') + ".csr"); File.WriteAllText(file, pem, new UTF8Encoding(false));
                    rows.Add(new[] { "0", "CSR ready", Path.GetFileName(file) + " - send it to your CA", "", "m", "show:" + file });
                    rows.Add(new[] { "3", "Copy the CSR text", "for CAs that want it pasted", "", "m", "copy:" + pem });
                    rows.Add(R(3, "For", Dn(cn) + " · names: " + string.Join(", ", sans), made ? "A new key, " + kn + ", was made for it and kept by Windows. When the CA sends the certificate back, use Check certificate and key - it puts the two together." : "The key is " + kn + " from My keys.", "m"));
                }
            }
            catch (CryptographicException e) { rows.Add(R(2, "CSR", e.Message, "", "m")); }
            finally { if (key != null) key.Dispose(); }
            return rows;
        }
        static string Dn(string s)
        {
            s = (s ?? "").Trim(); if (s.IndexOf('=') > 0) { try { return new X500DistinguishedName(s).Name; } catch (CryptographicException) { } }
            return "CN=" + Cn(s);
        }
        static readonly string[][] Presets = { new[] { "https", "Local HTTPS", "397" }, new[] { "lab", "Lab server", "730" }, new[] { "mtls", "mTLS client", "397" } };
        public static List<string[]> SelfSigned(string preset, string names)
        {
            var rows = new List<string[]>(); string p = (preset ?? "").Trim().ToLowerInvariant().Replace(" ", "");
            var pr = Presets.FirstOrDefault(x => x[0] == p || string.Equals(x[1].Replace(" ", ""), p, StringComparison.OrdinalIgnoreCase) || (p.StartsWith("local", StringComparison.Ordinal) && x[0] == "https") || (p.StartsWith("client", StringComparison.Ordinal) && x[0] == "mtls"));
            if (pr == null) { rows.Add(R(2, "Preset", "type Local HTTPS, Lab server or mTLS client", "", "")); return rows; }
            var sans = Sans(names);
            if (pr[0] == "https") { foreach (var x in new[] { "localhost", "127.0.0.1" }) if (!sans.Contains(x)) sans.Add(x); }
            if (sans.Count == 0) { rows.Add(R(2, "Names", pr[0] == "mtls" ? "type a name for this client - e.g. lab-client-01" : "type the names it's for - e.g. lab01.lab, 10.0.0.5", "", "")); return rows; }
            string name = pr[1] + " - " + sans[0]; if (Find(name) != null) name += " " + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            try
            {
                using (var c = Create(name, pr[0], sans, int.Parse(pr[2], CultureInfo.InvariantCulture)))
                {
                    string file = Path.Combine(OutDir, Safe(sans[0], 40).Replace('*', '_') + "-" + pr[0] + ".cer"); File.WriteAllText(file, Pem("CERTIFICATE", c.RawData), new UTF8Encoding(false));
                    rows.Add(R(0, "Made", name + " · valid " + pr[2] + " days · names: " + string.Join(", ", sans), "The certificate and its key are in My keys (kept by Windows).", "w"));
                    rows.Add(new[] { "3", "Certificate for the lab", Path.GetFileName(file) + " - the public part, to copy to the server or client", "", "m", "show:" + file });
                    rows.Add(R(1, "Trust", "not trusted anywhere yet - you decide", "Browsers and clients will warn until you add this certificate to their trusted list. AdeeboMeter never adds anything to Windows' trusted roots - do it only on lab machines, by double-clicking the .cer.", ""));
                }
            }
            catch (CryptographicException e) { rows.Add(R(2, "Self-signed", e.Message, "", "w")); }
            return rows;
        }
        static X509Certificate2 Load(string input)
        {
            string s = (input ?? "").Trim().Trim('"');
            byte[] data = File.Exists(s) ? File.ReadAllBytes(s) : Encoding.ASCII.GetBytes(s);
            string t = Encoding.ASCII.GetString(data); var m = Regex.Match(t, "-----BEGIN CERTIFICATE-----([A-Za-z0-9+/=\\s]+)-----END CERTIFICATE-----");
            return new X509Certificate2(m.Success ? Convert.FromBase64String(Regex.Replace(m.Groups[1].Value, "\\s", "")) : data);
        }
        static bool SameKey(X509Certificate2 a, X509Certificate2 b) { return a.PublicKey.EncodedKeyValue.RawData.SequenceEqual(b.PublicKey.EncodedKeyValue.RawData); }
        // the certificate your CA sent back: which of My keys it belongs to - and, if you say so, put the two together in Windows
        public static List<string[]> Match(string certIn, string install)
        {
            var rows = new List<string[]>(); X509Certificate2 cert;
            try { cert = Load(certIn); } catch (Exception e) { if (!(e is CryptographicException || e is IOException || e is FormatException || e is UnauthorizedAccessException)) throw; rows.Add(R(2, "Certificate", "couldn't read it - a .cer, .crt or .pem file, or paste the PEM text", "", "")); return rows; }
            using (cert)
            {
                rows.Add(R(3, "Certificate", cert.GetNameInfo(X509NameType.SimpleName, false) + " · from " + cert.GetNameInfo(X509NameType.SimpleName, true) + " · expires " + cert.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture), "", "m"));
                var mine = Mine(); var key = mine.FirstOrDefault(k => SameKey(k, cert));
                if (key == null) { rows.Add(R(2, "Matching key", "none of My keys matches this certificate", "It was made for a different key - from a different CSR, or a key on another PC. The CA must re-issue it for your CSR.", "m")); foreach (var k in mine) k.Dispose(); return rows; }
                rows.Add(R(0, "Matching key", NameOf(key) + " - this certificate belongs to it", "", "m"));
                if (key.Thumbprint == cert.Thumbprint) rows.Add(R(0, "Already together", "this exact certificate is already in My keys", "", "m"));
                else if (string.Equals((install ?? "").Trim(), "yes", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using (var rsa = key.GetRSAPrivateKey()) using (var both = cert.CopyWithPrivateKey(rsa)) using (var stored = Persist(both, NameOf(key) + " (" + cert.GetNameInfo(X509NameType.SimpleName, true) + ")"))
                            rows.Add(R(0, "Put together", "the CA's certificate and your key are now one entry in My keys: " + NameOf(stored), "Use Back up private key on it to get a .pfx for the server.", "m"));
                    }
                    catch (CryptographicException e) { rows.Add(R(2, "Put together", e.Message, "", "m")); }
                }
                else rows.Add(R(3, "Next", "type yes in the second box and run again to put them together in My keys", "", ""));
                foreach (var k in mine) k.Dispose();
            }
            return rows;
        }
        // the certificate you meant to deploy vs what the server actually sends
        public static List<string[]> Live(string certIn, string server)
        {
            var rows = new List<string[]>(); X509Certificate2 want;
            try { want = Load(certIn); } catch (Exception e) { if (!(e is CryptographicException || e is IOException || e is FormatException || e is UnauthorizedAccessException)) throw; rows.Add(R(2, "Certificate", "couldn't read it - a .cer, .crt or .pem file, or paste the PEM text", "", "")); return rows; }
            string h; int port; if (!Lab.Split((server ?? "").Trim(), 443, out h, out port) || !Games.ValidHost(h)) { want.Dispose(); rows.Add(R(2, "Server", "type the server - e.g. vpn.example.com or 10.0.0.5:8443", "", "")); return rows; }
            var sent = new List<X509Certificate2>(); SslPolicyErrors errs = SslPolicyErrors.None;
            try
            {
                using (var tc = new TcpClient())
                {
                    if (!tc.ConnectAsync(h, port).Wait(6000)) { rows.Add(R(2, h + ":" + port, "couldn't connect in 6 s", "", "m")); return rows; }
                    using (var ssl = new SslStream(tc.GetStream(), false, (o, c, ch, e) => { errs = e; if (ch != null) foreach (var el in ch.ChainElements) sent.Add(new X509Certificate2(el.Certificate)); else if (c != null) sent.Add(new X509Certificate2(c)); return e == SslPolicyErrors.None; }))
                    { var hs = System.Threading.Tasks.Task.Factory.StartNew(() => ssl.AuthenticateAsClient(h)); hs.Wait(8000); }
                }
            }
            catch (Exception e) { if (!(e is SocketException || e is IOException || e is AggregateException || e is System.Security.Authentication.AuthenticationException)) throw; }
            using (want)
            {
                if (sent.Count == 0) { rows.Add(R(2, h + ":" + port, "no certificate came back - not a TLS port?", "", "m")); return rows; }
                bool same = sent[0].Thumbprint == want.Thumbprint;
                rows.Add(R(same ? 0 : 2, "The server presents", same ? "exactly this certificate" : "a DIFFERENT certificate: " + sent[0].GetNameInfo(X509NameType.SimpleName, false) + " · from " + sent[0].GetNameInfo(X509NameType.SimpleName, true) + " · expires " + sent[0].NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture),
                    same ? "" : SameKey(sent[0], want) ? "Same key, different certificate - an older or newer copy is still loaded (restart the service, or check which certificate the binding uses)." : "Not even the same key - the new certificate isn't bound to this port yet, or another server answers on this name.", "m"));
                for (int i = 1; i < sent.Count; i++) rows.Add(R(sent[i].NotAfter < DateTime.Now ? 2 : 3, i == sent.Count - 1 && sent[i].Subject == sent[i].Issuer ? "Root" : "Intermediate", sent[i].GetNameInfo(X509NameType.SimpleName, false) + " · expires " + sent[i].NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + (sent[i].NotAfter < DateTime.Now ? " · EXPIRED" : ""), "", "m"));
                rows.Add(R(errs == SslPolicyErrors.None ? 0 : 1, "Windows trusts it", errs == SslPolicyErrors.None ? "yes" : "no - " + errs, errs.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors) ? "A missing or expired intermediate is the usual reason: install the CA's full chain on the server." : "", "m"));
                foreach (var c in sent) c.Dispose();
            }
            return rows;
        }
        // other people's public certificates, to encrypt files for them: add (a .cer and a name) or list
        public static List<string[]> Recipients(string certIn, string name)
        {
            var rows = new List<string[]>(); string n = Safe(name, 40);
            if (!string.IsNullOrWhiteSpace(certIn))
            {
                if (n == "") { rows.Add(R(2, "Name", "type the person's name - you'll pick them by it when you encrypt", "", "")); return rows; }
                string ci = certIn.Trim().Trim('"'); string txt = File.Exists(ci) ? File.ReadAllText(ci) : ci; string kn;
                var ecPub = EcKeys.ReadPublic(txt, out kn);
                if (ecPub != null)
                {
                    File.WriteAllText(Path.Combine(RecipDir, n + ".adeebokey"), txt.Trim() + "\r\n", new UTF8Encoding(false));
                    rows.Add(R(0, "Saved", n + " · EC P-384 (.adeebo files)", "Check the fingerprint with them by phone once: " + EcKeys.Fingerprint(ecPub), ""));
                }
                else try
                {
                    using (var c = Load(certIn))
                    {
                        if (!(c.PublicKey.Key is RSA)) { rows.Add(R(2, "Certificate", "it needs an RSA key (this one isn't)", "", "")); return rows; }
                        File.WriteAllText(Path.Combine(RecipDir, n + ".cer"), Pem("CERTIFICATE", c.RawData), new UTF8Encoding(false));
                        rows.Add(R(0, "Saved", n + " · " + c.GetNameInfo(X509NameType.SimpleName, false) + " · expires " + c.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture), "Check the fingerprint with them by phone once: SHA-256 " + BitConverter.ToString(Sha(c.RawData)).Replace("-", ":").Substring(0, 23) + "...", ""));
                    }
                }
                catch (Exception e) { if (!(e is CryptographicException || e is IOException || e is FormatException || e is UnauthorizedAccessException)) throw; rows.Add(R(2, "Certificate", "couldn't read it - the .cer they sent you, or paste its text", "", "")); return rows; }
            }
            var files = Directory.GetFiles(RecipDir, "*.cer").Concat(Directory.GetFiles(RecipDir, "*.adeebokey")).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            rows.Add(R(-1, "SAVED RECIPIENTS", files.Count == 0 ? "none yet" : files.Count.ToString(CultureInfo.InvariantCulture), "", ""));
            foreach (var f in files.Where(x => x.EndsWith(".adeebokey", StringComparison.OrdinalIgnoreCase))) { string nm; var pb = EcKeys.ReadPublic(File.ReadAllText(f), out nm); if (pb != null) rows.Add(R(3, Path.GetFileNameWithoutExtension(f), "EC P-384 · .adeebo files · fingerprint " + EcKeys.Fingerprint(pb), "", "")); }
            foreach (var f in files.Where(x => x.EndsWith(".cer", StringComparison.OrdinalIgnoreCase))) { try { using (var c = Load(f)) rows.Add(R(c.NotAfter < DateTime.Now ? 1 : 3, Path.GetFileNameWithoutExtension(f), c.GetNameInfo(X509NameType.SimpleName, false) + " · expires " + c.NotAfter.ToString("d MMM yyyy", CultureInfo.CurrentCulture), "", "")); } catch (CryptographicException) { } }
            return rows;
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350", Justification = "SHA-256 fingerprint for display")]
        static byte[] Sha(byte[] d) { using (var s = SHA256.Create()) return s.ComputeHash(d); }
        public static byte[] EcRecipient(string name)
        {
            string f = Path.Combine(RecipDir, Safe(name, 40) + ".adeebokey"); if (!File.Exists(f)) return null; string nm;
            return EcKeys.ReadPublic(File.ReadAllText(f), out nm);
        }
        public static X509Certificate2 Recipient(string name)
        {
            string f = Path.Combine(RecipDir, Safe(name, 40) + ".cer"); if (!File.Exists(f)) return null;
            try { return Load(f); } catch (CryptographicException) { return null; }
        }
        // ---------- key actions (rows in My keys)
        public static List<string[]> PublicKey(string thumb)
        {
            var rows = new List<string[]>(); using (var c = Find(thumb))
            {
                if (c == null) { rows.Add(R(2, "Key", "not found", "", "w")); return rows; }
                string pem = Pem("CERTIFICATE", c.RawData), file = Path.Combine(OutDir, Safe(NameOf(c), 40).Replace(' ', '-') + "-public.cer");
                File.WriteAllText(file, pem, new UTF8Encoding(false));
                rows.Add(new[] { "0", "Public key of " + NameOf(c), Path.GetFileName(file) + " - send this file; it's safe to share", "", "w", "show:" + file });
                rows.Add(new[] { "3", "Copy as text", "the same, to paste into an email or chat", "", "w", "copy:" + pem });
            }
            return rows;
        }
        public static List<string[]> Backup(string thumb)
        {
            var rows = new List<string[]>(); using (var c = Find(thumb))
            {
                if (c == null) { rows.Add(R(2, "Key", "not found", "", "w")); return rows; }
                string pass = FileCrypt.NewCode(), file = FileCrypt.FreeName(OutDir, Safe(NameOf(c), 40).Replace(' ', '-') + "-backup.pfx");
                try { var pfx = c.Export(X509ContentType.Pfx, pass); File.WriteAllBytes(file, pfx); Array.Clear(pfx, 0, pfx.Length); }
                catch (CryptographicException e) { rows.Add(R(2, "Back up", e.Message + " - this key was made not to leave the PC", "", "w")); return rows; }
                Program.Log("key backup written (" + Path.GetFileName(file) + ")");
                rows.Add(new[] { "0", "Backup", Path.GetFileName(file) + " - copy it somewhere safe (a USB stick, your own cloud)", "", "w", "show:" + file });
                rows.Add(new[] { "1", "Recovery passphrase", pass, "Save it in your password manager now - AdeeboMeter doesn't keep it. Without it the backup can't be opened, by anyone.", "k", "copy:" + pass });
                rows.Add(R(3, "On a new PC", "double-click the .pfx (or Toolbox › Check a PFX), type the passphrase - then it's in My keys there too", "", ""));
            }
            return rows;
        }
        public static bool Delete(string thumb)
        {
            using (var st = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                st.Open(OpenFlags.ReadWrite);
                var c = st.Certificates.Cast<X509Certificate2>().FirstOrDefault(x => x.Thumbprint == thumb && (x.FriendlyName ?? "").StartsWith(Prefix, StringComparison.Ordinal));   // only AdeeboMeter's own keys
                if (c == null) return false;
                st.Remove(c); Program.Log("key deleted: " + NameOf(c)); return true;
            }
        }
    }
}
