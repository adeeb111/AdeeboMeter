// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace AdeeboMeter
{
    // ================================================================== Encrypt a file
    // Two ways, both offline:
    //  * Share with a code - a .adeebo file. AdeeboMeter makes a random code; you send the file and give the code another way.
    //    Format v1 (all big-endian), built only from standard pieces:
    //      "ADEEBO" 0x01 0x01 | iterations (4) | salt (16) | IV (16) | AES-256-CBC( name length (2) | name (UTF-8) | file ) | HMAC-SHA256 (32)
    //    Keys: PBKDF2-HMAC-SHA256(code, salt, iterations) -> 64 bytes: AES key, then HMAC key. The HMAC covers everything
    //    before it and is checked BEFORE anything is decrypted - a changed or damaged file is refused, never half-opened.
    //  * Encrypt for a recipient - a standard CMS / S/MIME .p7m (AES-256) for their certificate; opens in AdeeboMeter,
    //    OpenSSL (openssl cms -decrypt) or Outlook-compatible tools. Windows finds the recipient's private key by itself.
    // Codes are never saved or logged; the original file is never changed.
    static class FileCrypt
    {
        static readonly byte[] Magic = { (byte)'A', (byte)'D', (byte)'E', (byte)'E', (byte)'B', (byte)'O', 1, 1 };
        public const int Iterations = 600000, HeaderLen = 8 + 4 + 16 + 16, MacLen = 32;
        public const long MaxBytes = 4L * 1024 * 1024 * 1024, MaxCms = 200L * 1024 * 1024;
        const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";   // no 0/O, 1/I/L - easy to read out on the phone
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }

        // 24 characters from 31 = about 119 bits, shown as six groups of four
        public static string NewCode()
        {
            var sb = new StringBuilder(); var b = new byte[1];
            using (var g = RandomNumberGenerator.Create())
                while (sb.Length < 24) { g.GetBytes(b); if (b[0] < 248) sb.Append(Alphabet[b[0] % 31]); }   // 248 = 8 x 31: no bias
            return string.Join("-", Enumerable.Range(0, 6).Select(i => sb.ToString(i * 4, 4)));
        }
        // the AES key and the HMAC key, from the code
        static void Keys(string code, byte[] salt, int iter, out byte[] enc, out byte[] mac)
        {
            using (var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(Clean(code)), salt, iter, HashAlgorithmName.SHA256))
            { var k = kdf.GetBytes(64); enc = k.Take(32).ToArray(); mac = k.Skip(32).ToArray(); Array.Clear(k, 0, k.Length); }
        }
        // what people type: any case, with or without dashes and spaces
        public static string Clean(string code) { return new string((code ?? "").ToUpperInvariant().Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()); }

        public static void EncryptCode(Stream input, string name, Stream output, string code, int iterations)
        {
            var salt = new byte[16]; var iv = new byte[16]; using (var g = RandomNumberGenerator.Create()) { g.GetBytes(salt); g.GetBytes(iv); }
            byte[] ek, mk; Keys(code, salt, iterations, out ek, out mk);
            try
            {
                using (var hmac = new HMACSHA256(mk))
                {
                    var head = new List<byte>(Magic); head.AddRange(BitConverter.GetBytes(iterations).Reverse()); head.AddRange(salt); head.AddRange(iv);
                    var h = head.ToArray(); output.Write(h, 0, h.Length); hmac.TransformBlock(h, 0, h.Length, null, 0);
                    using (var aes = Aes.Create())
                    {
                        aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7; aes.Key = ek; aes.IV = iv;
                        var macTee = new MacStream(output, hmac);
                        using (var cs = new CryptoStream(macTee, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            var nb = Encoding.UTF8.GetBytes(name ?? ""); if (nb.Length > 255) nb = nb.Take(255).ToArray();
                            cs.WriteByte((byte)(nb.Length >> 8)); cs.WriteByte((byte)nb.Length); cs.Write(nb, 0, nb.Length);
                            input.CopyTo(cs, 81920); cs.FlushFinalBlock();
                        }
                    }
                    hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0); output.Write(hmac.Hash, 0, MacLen);
                }
            }
            finally { Array.Clear(ek, 0, ek.Length); Array.Clear(mk, 0, mk.Length); }
        }
        // writes ciphertext through to the file and into the HMAC; never closes the file underneath
        sealed class MacStream : Stream
        {
            readonly Stream o; readonly HMAC h;
            public MacStream(Stream o, HMAC h) { this.o = o; this.h = h; }
            public override void Write(byte[] b, int off, int n) { o.Write(b, off, n); h.TransformBlock(b, off, n, null, 0); }
            public override bool CanRead { get { return false; } } public override bool CanSeek { get { return false; } } public override bool CanWrite { get { return true; } }
            public override long Length { get { throw new NotSupportedException(); } } public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
            public override void Flush() { o.Flush(); }
            public override int Read(byte[] b, int off, int n) { throw new NotSupportedException(); }
            public override long Seek(long off, SeekOrigin so) { throw new NotSupportedException(); }
            public override void SetLength(long v) { throw new NotSupportedException(); }
        }
        // returns the original name; throws CryptographicException("wrong code or changed file") when the check fails
        public static string DecryptCode(Stream input, Stream output, string code)
        {
            long len = input.Length; if (len < HeaderLen + 16 + MacLen) throw new CryptographicException("not an AdeeboMeter encrypted file");
            var head = new byte[HeaderLen]; ReadAll(input, head);
            for (int i = 0; i < Magic.Length; i++) if (head[i] != Magic[i]) throw new CryptographicException(i < 6 ? "not an AdeeboMeter encrypted file" : "made by a newer AdeeboMeter - update to open it");
            int iter = head[8] << 24 | head[9] << 16 | head[10] << 8 | head[11]; if (iter < 100000 || iter > 10000000) throw new CryptographicException("damaged header");
            var salt = head.Skip(12).Take(16).ToArray(); var iv = head.Skip(28).Take(16).ToArray();
            byte[] ek, mk; Keys(code, salt, iter, out ek, out mk);
            try
            {
                // 1. check: the HMAC over header + ciphertext, before decrypting anything
                long ctLen = len - HeaderLen - MacLen; var buf = new byte[81920];
                using (var hmac = new HMACSHA256(mk))
                {
                    hmac.TransformBlock(head, 0, head.Length, null, 0); long left = ctLen;
                    while (left > 0) { int n = input.Read(buf, 0, (int)Math.Min(buf.Length, left)); if (n <= 0) throw new CryptographicException("file is cut short"); hmac.TransformBlock(buf, 0, n, null, 0); left -= n; }
                    hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    var tag = new byte[MacLen]; ReadAll(input, tag);
                    int diff = 0; for (int i = 0; i < MacLen; i++) diff |= tag[i] ^ hmac.Hash[i];   // constant time
                    if (diff != 0) throw new CryptographicException("wrong code, or the file was changed or damaged");
                }
                // 2. decrypt
                input.Position = HeaderLen;
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7; aes.Key = ek; aes.IV = iv;
                    using (var lim = new LimitStream(input, ctLen)) using (var cs = new CryptoStream(lim, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        int hi = cs.ReadByte(), lo = cs.ReadByte(); if (hi < 0 || lo < 0) throw new CryptographicException("damaged");
                        var nb = new byte[hi << 8 | lo]; ReadAll(cs, nb);
                        cs.CopyTo(output, 81920);
                        return Encoding.UTF8.GetString(nb);
                    }
                }
            }
            finally { Array.Clear(ek, 0, ek.Length); Array.Clear(mk, 0, mk.Length); }
        }
        sealed class LimitStream : Stream
        {
            readonly Stream s; long left;
            public LimitStream(Stream s, long n) { this.s = s; left = n; }
            public override int Read(byte[] b, int off, int n) { if (left <= 0) return 0; int r = s.Read(b, off, (int)Math.Min(n, left)); left -= r; return r; }
            public override bool CanRead { get { return true; } } public override bool CanSeek { get { return false; } } public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } } public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
            public override void Flush() { }
            public override long Seek(long off, SeekOrigin so) { throw new NotSupportedException(); }
            public override void SetLength(long v) { throw new NotSupportedException(); }
            public override void Write(byte[] b, int off, int n) { throw new NotSupportedException(); }
            protected override void Dispose(bool disposing) { base.Dispose(disposing); }   // leaves the file open for its owner
        }
        static void ReadAll(Stream s, byte[] b) { int got = 0; while (got < b.Length) { int n = s.Read(b, got, b.Length - got); if (n <= 0) throw new CryptographicException("file is cut short"); got += n; } }
        // a file name that can't escape the folder or overwrite something: name only, safe characters, and " (2)" if taken
        public static string FreeName(string dir, string name)
        {
            string n = Path.GetFileName(name ?? ""); n = new string(n.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim().TrimStart('.');
            if (n == "") n = "opened-file";
            string p = Path.Combine(dir, n); int i = 2;
            while (File.Exists(p)) { p = Path.Combine(dir, Path.GetFileNameWithoutExtension(n) + " (" + i++ + ")" + Path.GetExtension(n)); }
            return p;
        }

        // ---------- the Toolbox tools
        static string FileArg(string s) { return (s ?? "").Trim().Trim('"'); }
        // "code" (or empty) = share with a code; otherwise recipient names (comma-separated) from the saved recipients
        public static List<string[]> Encrypt(string fileIn, string to, string alsoMe, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string file = FileArg(fileIn);
            if (file == "" || !File.Exists(file)) { rows.Add(R(2, "File", file == "" ? "type or paste the path of the file to encrypt" : "not found: " + file, "", "u")); return rows; }
            var fi = new FileInfo(file); if (fi.Length > MaxBytes) { rows.Add(R(2, "File", "bigger than 4 GB", "", "u")); return rows; }
            string t = (to ?? "").Trim();
            if (t == "" || t.Equals("code", StringComparison.OrdinalIgnoreCase))
            {
                string code = NewCode(), outFile = FreeName(fi.DirectoryName, fi.Name + ".adeebo");
                run.Status = "Encrypting with a new code (the key takes a second to make)...";
                try
                {
                    using (var i = File.OpenRead(file)) using (var o = new FileStream(outFile, FileMode.CreateNew, FileAccess.Write)) EncryptCode(i, fi.Name, o, code, Iterations);
                }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is CryptographicException)) throw; try { File.Delete(outFile); } catch (IOException) { } rows.Add(R(2, "Encrypt", e.Message, "", "u")); return rows; }
                Program.Log("encrypted a file with a code (" + fi.Length + " bytes)");
                rows.Add(new[] { "0", "Encrypted", Path.GetFileName(outFile) + " - next to the original, which isn't changed", "", "m", "show:" + outFile });
                rows.Add(new[] { "1", "The code", code, "Give it another way than the file - read it out on the phone, or send it by text. AdeeboMeter doesn't keep it: if it's lost, nobody can open the file, you included.", "k", "copy:" + code });
                rows.Add(R(3, "To open it", "the other person opens the .adeebo file in AdeeboMeter (Toolbox › Open an encrypted file) and types the code", "AdeeboMeter is free - it's the only app that opens .adeebo files.", "u"));
                return rows;
            }
            // for recipients (their public certificates), standard CMS
            if (fi.Length > MaxCms) { rows.Add(R(2, "File", "for a recipient, files up to 200 MB - use Share with a code for bigger ones", "", "u")); return rows; }
            var certs = new List<X509Certificate2>(); var names = new List<string>();
            foreach (var nm in t.Split(',').Select(x => x.Trim()).Where(x => x != ""))
            {
                var c = CertKeys.Recipient(nm); if (c == null) { rows.Add(R(2, "Recipient", "no saved recipient called \"" + nm + "\"", "Save them first with Recipients (their .cer file) - or type code to share with a code.", "u")); return rows; }
                certs.Add(c); names.Add(nm);
            }
            if (string.Equals((alsoMe ?? "").Trim(), "no", StringComparison.OrdinalIgnoreCase) == false)
            { var me = CertKeys.MyEncryptionCert(); if (me != null) { certs.Add(me); names.Add("you (" + me.GetNameInfo(X509NameType.SimpleName, false) + ")"); } else rows.Add(R(1, "Also for you", "you have no key of your own yet - you won't be able to open this copy", "Make one in My keys (New key) to read files you encrypt for others.", "u")); }
            string outP7m = FreeName(fi.DirectoryName, fi.Name + ".p7m");
            try
            {
                run.Status = "Encrypting for " + string.Join(", ", names) + "...";
                var env = new EnvelopedCms(new ContentInfo(File.ReadAllBytes(file)), new AlgorithmIdentifier(new Oid("2.16.840.1.101.3.4.1.42")));   // AES-256-CBC
                var rc = new CmsRecipientCollection(); foreach (var c in certs) rc.Add(new CmsRecipient(SubjectIdentifierType.IssuerAndSerialNumber, c));
                env.Encrypt(rc); File.WriteAllBytes(outP7m, env.Encode());
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is CryptographicException)) throw; rows.Add(R(2, "Encrypt", e.Message, "Recipients need an RSA certificate.", "u")); return rows; }
            finally { foreach (var c in certs) c.Dispose(); }
            Program.Log("encrypted a file for " + names.Count + " recipient(s)");
            rows.Add(new[] { "0", "Encrypted", Path.GetFileName(outP7m) + " for " + string.Join(", ", names), "", "m", "show:" + outP7m });
            rows.Add(R(3, "To open it", "they open it with the private key they already hold: AdeeboMeter (Open an encrypted file), or  openssl cms -decrypt -in file.p7m -inform DER -inkey key.pem -out file", "Standard CMS (S/MIME) with AES-256 - no code to pass around.", "u"));
            return rows;
        }
        public static List<string[]> Decrypt(string fileIn, string code, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string file = FileArg(fileIn);
            if (file == "" || !File.Exists(file)) { rows.Add(R(2, "File", file == "" ? "type or paste the path of the .adeebo or .p7m file" : "not found: " + file, "", "u")); return rows; }
            var fi = new FileInfo(file); string tmp = Path.Combine(fi.DirectoryName, "." + Guid.NewGuid().ToString("N") + ".opening");
            try
            {
                if (file.EndsWith(".adeebo", StringComparison.OrdinalIgnoreCase))
                {
                    if (Clean(code).Length == 0) { rows.Add(R(2, "Code", "type the code you were given", "", "u")); return rows; }
                    run.Status = "Checking the file, then opening it..."; string name;
                    using (var i = File.OpenRead(file)) using (var o = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write)) name = DecryptCode(i, o, code);
                    string dest = FreeName(fi.DirectoryName, name); File.Move(tmp, dest);
                    rows.Add(new[] { "0", "Opened", Path.GetFileName(dest) + " - the file checked out (not changed since it was encrypted)", "", "m", "show:" + dest });
                }
                else
                {
                    if (fi.Length > MaxCms) { rows.Add(R(2, "File", "bigger than 200 MB", "", "u")); return rows; }
                    run.Status = "Looking for your key and opening it...";
                    var env = new EnvelopedCms(); env.Decode(File.ReadAllBytes(file)); env.Decrypt();   // Windows finds the matching private key in your store
                    string name = fi.Name.EndsWith(".p7m", StringComparison.OrdinalIgnoreCase) ? fi.Name.Substring(0, fi.Name.Length - 4) : fi.Name + ".opened";
                    string dest = FreeName(fi.DirectoryName, name); File.WriteAllBytes(dest, env.ContentInfo.Content);
                    rows.Add(new[] { "0", "Opened", Path.GetFileName(dest), "", "m", "show:" + dest });
                }
            }
            catch (CryptographicException e)
            {
                rows.Add(R(2, "Couldn't open it", e.Message.IndexOf("wrong code", StringComparison.Ordinal) >= 0 ? "wrong code - or the file was changed or damaged on the way" : e.Message, file.EndsWith(".adeebo", StringComparison.OrdinalIgnoreCase) ? "Nothing was opened or written. Check the code (letters and numbers only; dashes don't matter)." : "This file wasn't made for any of your keys - ask the sender to encrypt it for your public key.", "u"));
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; rows.Add(R(2, "Open", e.Message, "", "u")); }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { } }
            return rows;
        }
    }
}
