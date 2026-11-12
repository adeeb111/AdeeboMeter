// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AdeeboMeter
{
    // ================================================================== EC P-384 keys and .adeebo files for a recipient
    // The "strongest" choice, unique to AdeeboMeter: elliptic-curve Diffie-Hellman on NIST P-384.
    //  * The private key is a named key in Windows' own key storage for your account (Microsoft Software Key Storage
    //    Provider, no admin); AdeeboMeter keeps only its name and public half in keys-ec.txt.
    //  * Your public key travels as a small .adeebokey text file; its fingerprint is what you read out to check it.
    //  * A .adeebo file for recipients (format v1, mode 2):
    //      "ADEEBO" 0x01 0x02 | count (1) | per recipient: key id (8) | ephemeral public key length (2) + key | wrapped key (64)
    //      | IV (16) | AES-256-CBC( name length (2) | name | file ) | HMAC-SHA256 (32)
    //    A random 64-byte file key (AES key + HMAC key) is made per file; for each recipient a fresh one-time P-384 key
    //    agrees a secret with their public key, SHA-384(prefix | secret) -> 48 bytes, expanded with SHA-512 to the 64-byte
    //    pad the file key is XORed with (one-time, never reused). The HMAC covers the whole header, so a wrong key or a
    //    changed file is refused before anything is decrypted.
    static class EcKeys
    {
        public sealed class Key { public string Name = "", KeyName = "", Pub = ""; public DateTime Made; public string Id { get { return Fingerprint(Convert.FromBase64String(Pub)).Substring(0, 9); } } }
        static string IndexFile { get { return Path.Combine(Program.DataDir, "keys-ec.txt"); } }
        static readonly byte[] Magic2 = { (byte)'A', (byte)'D', (byte)'E', (byte)'E', (byte)'B', (byte)'O', 1, 2 }, Magic3 = { (byte)'A', (byte)'D', (byte)'E', (byte)'E', (byte)'B', (byte)'O', 1, 3 };
        static readonly byte[] Label = Encoding.ASCII.GetBytes("AdeeboMeter ECDH P-384 file key v1");

        public static bool Available
        {
            get { try { using (var k = CngKey.Create(CngAlgorithm.ECDiffieHellmanP384)) return k != null; } catch (Exception e) { if (!(e is CryptographicException || e is PlatformNotSupportedException || e is NotImplementedException || e is TypeLoadException || e is EntryPointNotFoundException || e is DllNotFoundException)) throw; return false; } }
        }
        public static List<Key> All()
        {
            var l = new List<Key>();
            try
            {
                if (File.Exists(IndexFile))
                    foreach (var line in File.ReadAllLines(IndexFile).Take(100))
                    {
                        var p = line.Split('|'); DateTime t; if (p.Length != 5 || p[0] != "ec" || !DateTime.TryParseExact(p[4], "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) continue;
                        l.Add(new Key { Name = p[1], KeyName = p[2], Pub = p[3], Made = t });
                    }
            }
            catch (IOException) { }
            return l;
        }
        static void Save(List<Key> l) { File.WriteAllLines(IndexFile, l.Select(k => "ec|" + k.Name + "|" + k.KeyName + "|" + k.Pub + "|" + k.Made.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture))); }
        public static Key Find(string q) { return All().FirstOrDefault(k => string.Equals(k.Name, q, StringComparison.OrdinalIgnoreCase) || string.Equals(k.KeyName, q, StringComparison.OrdinalIgnoreCase)); }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350", Justification = "SHA-256 fingerprint")]
        public static string Fingerprint(byte[] pub)
        {
            using (var s = SHA256.Create()) { var h = s.ComputeHash(pub); var b = string.Join("", h.Take(15).Select(x => x.ToString("X2", CultureInfo.InvariantCulture))); return string.Join("-", Enumerable.Range(0, 6).Select(i => b.Substring(i * 5, 5))); }
        }
        static byte[] KeyId(byte[] pub) { using (var s = SHA256.Create()) return s.ComputeHash(pub).Take(8).ToArray(); }

        // a new P-384 key, kept by Windows for this user; exportable only so you can back it up
        public static Key Create(string name, byte[] importPrivate)
        {
            string kn = "AdeeboMeter-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            var p = new CngKeyCreationParameters { ExportPolicy = CngExportPolicies.AllowPlaintextExport, KeyCreationOptions = CngKeyCreationOptions.None, KeyUsage = CngKeyUsages.KeyAgreement, Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider };
            if (importPrivate != null) p.Parameters.Add(new CngProperty(CngKeyBlobFormat.EccPrivateBlob.Format, importPrivate, CngPropertyOptions.None));
            using (var k = CngKey.Create(CngAlgorithm.ECDiffieHellmanP384, kn, p))
            {
                var key = new Key { Name = name, KeyName = kn, Pub = Convert.ToBase64String(k.Export(CngKeyBlobFormat.EccPublicBlob)), Made = DateTime.Now };
                var l = All(); l.Add(key); Save(l); return key;
            }
        }
        public static bool Delete(Key key)
        {
            try { if (CngKey.Exists(key.KeyName)) using (var k = CngKey.Open(key.KeyName)) k.Delete(); } catch (CryptographicException) { }
            var l = All(); int n = l.RemoveAll(x => x.KeyName == key.KeyName); Save(l); return n > 0;
        }
        public static string PublicFile(Key k)
        {
            return "ADEEBOMETER PUBLIC KEY v1\r\nname: " + k.Name + "\r\ntype: EC P-384 (opens .adeebo files)\r\nkey: " + k.Pub + "\r\nfingerprint: " + Fingerprint(Convert.FromBase64String(k.Pub)) + "\r\n";
        }
        // reads a .adeebokey file (or its text): the public key blob, checked to be a P-384 public key
        public static byte[] ReadPublic(string text, out string name)
        {
            name = ""; if (text == null || !text.StartsWith("ADEEBOMETER PUBLIC KEY v1", StringComparison.Ordinal)) return null;
            string b64 = ""; foreach (var line in text.Split('\n')) { var l = line.Trim(); if (l.StartsWith("key: ", StringComparison.Ordinal)) b64 = l.Substring(5).Trim(); else if (l.StartsWith("name: ", StringComparison.Ordinal)) name = l.Substring(6).Trim(); }
            try { var blob = Convert.FromBase64String(b64); if (blob.Length != 104) return null;   /* ECK3 header (8) + X and Y (48 each): anything else isn't a P-384 public key */
                using (var k = CngKey.Import(blob, CngKeyBlobFormat.EccPublicBlob)) return k.Algorithm == CngAlgorithm.ECDiffieHellmanP384 ? blob : null; }
            catch (Exception e) { if (!(e is FormatException || e is CryptographicException)) throw; return null; }
        }
        static byte[] Pad(byte[] secret)
        {
            using (var s = SHA512.Create()) return s.ComputeHash(secret.Concat(Encoding.ASCII.GetBytes("wrap")).ToArray());
        }
        static byte[] Agree(ECDiffieHellmanCng mine, byte[] theirPub, byte[] ephPub, byte[] recipPub)
        {
            mine.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash; mine.HashAlgorithm = CngAlgorithm.Sha384;
            mine.SecretPrepend = Label.Concat(ephPub).Concat(recipPub).ToArray(); mine.SecretAppend = null;
            using (var other = CngKey.Import(theirPub, CngKeyBlobFormat.EccPublicBlob)) return mine.DeriveKeyMaterial(other);
        }

        // ---------- encrypt for P-384 recipients; decrypt with whichever of My keys it was made for
        public static void Encrypt(Stream input, string name, Stream output, IList<byte[]> recipients)
        {
            var fileKey = new byte[64]; var iv = new byte[16]; using (var g = RandomNumberGenerator.Create()) { g.GetBytes(fileKey); g.GetBytes(iv); }
            try
            {
                var head = new List<byte>(Magic2) { (byte)recipients.Count };
                foreach (var rp in recipients)
                {
                    using (var eph = new ECDiffieHellmanCng(384))
                    {
                        var ephPub = eph.Key.Export(CngKeyBlobFormat.EccPublicBlob);
                        var secret = Agree(eph, rp, ephPub, rp); var pad = Pad(secret);
                        head.AddRange(KeyId(rp)); head.Add((byte)(ephPub.Length >> 8)); head.Add((byte)ephPub.Length); head.AddRange(ephPub);
                        head.AddRange(fileKey.Select((b, i) => (byte)(b ^ pad[i])));
                        Array.Clear(secret, 0, secret.Length); Array.Clear(pad, 0, pad.Length);
                    }
                }
                head.AddRange(iv);
                FileCrypt.Seal(input, name, output, head.ToArray(), fileKey.Take(32).ToArray(), fileKey.Skip(32).ToArray(), iv);
            }
            finally { Array.Clear(fileKey, 0, fileKey.Length); }
        }
        public static bool IsMode2(byte[] head8) { return head8.Length >= 8 && head8.Take(8).SequenceEqual(Magic2); }
        public static string Decrypt(Stream input, Stream output, out string keyUsed)
        {
            keyUsed = ""; var mine = All();
            var hb = new List<byte>(); Func<int, byte[]> take = n => { var b = new byte[n]; int got = 0; while (got < n) { int r = input.Read(b, got, n - got); if (r <= 0) throw new CryptographicException("file is cut short"); got += r; } hb.AddRange(b); return b; };
            if (!take(8).SequenceEqual(Magic2)) throw new CryptographicException("not a .adeebo file for recipients");
            int count = take(1)[0]; if (count < 1 || count > 20) throw new CryptographicException("damaged header");
            byte[] fileKey = null;
            for (int i = 0; i < count; i++)
            {
                var id = take(8); var lb = take(2); var ephPub = take(lb[0] << 8 | lb[1]); var wrapped = take(64);
                if (fileKey != null) continue;
                var k = mine.FirstOrDefault(m => KeyId(Convert.FromBase64String(m.Pub)).SequenceEqual(id)); if (k == null || !CngKey.Exists(k.KeyName)) continue;
                using (var ck = CngKey.Open(k.KeyName)) using (var dh = new ECDiffieHellmanCng(ck))
                {
                    var secret = Agree(dh, ephPub, ephPub, Convert.FromBase64String(k.Pub)); var pad = Pad(secret);
                    fileKey = wrapped.Select((b, j) => (byte)(b ^ pad[j])).ToArray(); keyUsed = k.Name;
                    Array.Clear(secret, 0, secret.Length); Array.Clear(pad, 0, pad.Length);
                }
            }
            if (fileKey == null) throw new CryptographicException("this file wasn't made for any of your keys");
            var iv = take(16);
            try { return FileCrypt.Open(input, output, hb.ToArray(), fileKey.Take(32).ToArray(), fileKey.Skip(32).ToArray(), iv); }
            finally { Array.Clear(fileKey, 0, fileKey.Length); }
        }

        // ---------- backup: the private key in a .adeebo (mode 3) protected by a recovery passphrase; restore = open it
        public static byte[] BackupBytes(Key k, string passphrase)
        {
            byte[] priv; using (var ck = CngKey.Open(k.KeyName)) priv = ck.Export(CngKeyBlobFormat.EccPrivateBlob);
            try
            {
                var payload = Encoding.UTF8.GetBytes(k.Name).Length.ToString(CultureInfo.InvariantCulture) + "|" + k.Name + "|";
                var body = Encoding.UTF8.GetBytes(payload).Concat(priv).ToArray();
                using (var o = new MemoryStream()) { FileCrypt.EncryptCode(new MemoryStream(body), "key", o, passphrase, FileCrypt.Iterations, Magic3); Array.Clear(body, 0, body.Length); return o.ToArray(); }
            }
            finally { Array.Clear(priv, 0, priv.Length); }
        }
        public static bool IsBackup(byte[] head8) { return head8.Length >= 8 && head8.Take(8).SequenceEqual(Magic3); }
        public static Key Restore(Stream input, string passphrase)
        {
            using (var o = new MemoryStream())
            {
                FileCrypt.DecryptCode(input, o, passphrase, Magic3); var body = o.ToArray();
                try
                {
                    int bar = Array.IndexOf(body, (byte)'|'); int nlen = int.Parse(Encoding.ASCII.GetString(body, 0, bar), CultureInfo.InvariantCulture);
                    string name = Encoding.UTF8.GetString(body, bar + 1, nlen); var priv = body.Skip(bar + 1 + nlen + 1).ToArray();
                    var existing = All().FirstOrDefault(x => x.Name == name); if (existing != null) name += " (restored)";
                    try { return Create(name, priv); } finally { Array.Clear(priv, 0, priv.Length); }
                }
                finally { Array.Clear(body, 0, body.Length); }
            }
        }
    }
}
