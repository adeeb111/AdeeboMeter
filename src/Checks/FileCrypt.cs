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
        public const long MaxBytes = 4L * 1024 * 1024 * 1024, MaxCms = 50L * 1024 * 1024, MaxCmsOpen = 100L * 1024 * 1024;   // a standard .p7m is built in memory (several copies): made up to 50 MB; one someone sends is opened up to 100 MB; .adeebo streams up to 4 GB
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
            var pw = Encoding.UTF8.GetBytes(Clean(code)); var k = new byte[64];
            try
            {
                // Windows' own PBKDF2 (bcrypt.dll) gives the same bytes many times faster than .NET's managed one - 600,000
                // rounds take a fraction of a second instead of ~15 s; the managed one is only the fallback
                if (!NativePbkdf2(pw, salt, iter, k))
                    using (var kdf = new Rfc2898DeriveBytes(pw, salt, iter, HashAlgorithmName.SHA256)) { var m = kdf.GetBytes(64); Buffer.BlockCopy(m, 0, k, 0, 64); Array.Clear(m, 0, m.Length); }
                enc = k.Take(32).ToArray(); mac = k.Skip(32).ToArray();
            }
            finally { Array.Clear(k, 0, k.Length); Array.Clear(pw, 0, pw.Length); }
        }
        public static bool NativePbkdf2(byte[] pw, byte[] salt, int iter, byte[] outKey)
        {
            IntPtr alg = IntPtr.Zero;
            try
            {
                if (Pbkdf2Native.BCryptOpenAlgorithmProvider(out alg, "SHA256", null, 8 /* BCRYPT_ALG_HANDLE_HMAC_FLAG */) != 0) { alg = IntPtr.Zero; return false; }
                return Pbkdf2Native.BCryptDeriveKeyPBKDF2(alg, pw, pw.Length, salt, salt.Length, (ulong)iter, outKey, outKey.Length, 0) == 0;
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
            finally { if (alg != IntPtr.Zero && Pbkdf2Native.BCryptCloseAlgorithmProvider(alg, 0) != 0) Program.Log("bcrypt close failed"); }
        }
        public static byte[] ManagedPbkdf2(byte[] pw, byte[] salt, int iter, int len) { using (var kdf = new Rfc2898DeriveBytes(pw, salt, iter, HashAlgorithmName.SHA256)) return kdf.GetBytes(len); }
        // what people type: any case, with or without dashes and spaces
        public static string Clean(string code) { return new string((code ?? "").ToUpperInvariant().Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()); }

        public static void EncryptCode(Stream input, string name, Stream output, string code, int iterations) { EncryptCode(input, name, output, code, iterations, Magic); }
        public static void EncryptCode(Stream input, string name, Stream output, string code, int iterations, byte[] magic)
        {
            var salt = new byte[16]; var iv = new byte[16]; using (var g = RandomNumberGenerator.Create()) { g.GetBytes(salt); g.GetBytes(iv); }
            byte[] ek, mk; Keys(code, salt, iterations, out ek, out mk);
            var head = new List<byte>(magic); head.AddRange(BitConverter.GetBytes(iterations).Reverse()); head.AddRange(salt); head.AddRange(iv);
            try { Seal(input, name, output, head.ToArray(), ek, mk, iv); }
            finally { Array.Clear(ek, 0, ek.Length); Array.Clear(mk, 0, mk.Length); }
        }
        // header as given, then AES-256-CBC( name length | name | file ), then HMAC-SHA256 over header + ciphertext
        public static void Seal(Stream input, string name, Stream output, byte[] header, byte[] ek, byte[] mk, byte[] iv)
        {
            using (var hmac = new HMACSHA256(mk))
            {
                output.Write(header, 0, header.Length); hmac.TransformBlock(header, 0, header.Length, null, 0);
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7; aes.Key = ek; aes.IV = iv;
                    var macTee = new MacStream(output, hmac);
                    using (var cs = new CryptoStream(macTee, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        string nm0 = name ?? ""; while (Encoding.UTF8.GetByteCount(nm0) > 255) nm0 = nm0.Substring(0, nm0.Length - 1); if (nm0.Length > 0 && char.IsHighSurrogate(nm0[nm0.Length - 1])) nm0 = nm0.Substring(0, nm0.Length - 1); var nb = Encoding.UTF8.GetBytes(nm0);   /* a long name is shortened by whole characters */
                        cs.WriteByte((byte)(nb.Length >> 8)); cs.WriteByte((byte)nb.Length); cs.Write(nb, 0, nb.Length);
                        input.CopyTo(cs, 81920); cs.FlushFinalBlock();
                    }
                }
                hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0); output.Write(hmac.Hash, 0, MacLen);
            }
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
        public static string DecryptCode(Stream input, Stream output, string code) { return DecryptCode(input, output, code, Magic); }
        // returns the original name; throws CryptographicException when the code is wrong or the file was changed
        public static string DecryptCode(Stream input, Stream output, string code, byte[] magic)
        {
            if (input.Length < HeaderLen + 16 + MacLen) throw new CryptographicException("not an AdeeboMeter encrypted file");
            var head = new byte[HeaderLen]; ReadAll(input, head);
            for (int i = 0; i < magic.Length; i++) if (head[i] != magic[i]) throw new CryptographicException(i < 6 ? "not an AdeeboMeter encrypted file" : i == 7 ? "not this kind of .adeebo file" : "made by a newer AdeeboMeter - update to open it");
            int iter = head[8] << 24 | head[9] << 16 | head[10] << 8 | head[11]; if (iter < 100000 || iter > 2000000) throw new CryptographicException("damaged header");   // we write 600,000; a crafted file can't make opening grind for minutes
            var salt = head.Skip(12).Take(16).ToArray(); var iv = head.Skip(28).Take(16).ToArray();
            byte[] ek, mk; Keys(code, salt, iter, out ek, out mk);
            try { return Open(input, output, head, ek, mk, iv); }
            finally { Array.Clear(ek, 0, ek.Length); Array.Clear(mk, 0, mk.Length); }
        }
        // The stream sits just after the header. One pass: every ciphertext byte is read once, fed to the HMAC and to the
        // decryptor together, so what's decrypted is exactly what was checked (a share or USB stick that serves different
        // bytes on a second read can't slip anything in). The output is a temp file (or memory) that the caller only keeps
        // when this returns - a bad tag throws and the temp is deleted. A padding error is reported as the MAC failure.
        public static string Open(Stream input, Stream output, byte[] head, byte[] ek, byte[] mk, byte[] iv)
        {
            long ctStart = input.Position, ctLen = input.Length - ctStart - MacLen; if (ctLen < 16) throw new CryptographicException("file is cut short");
            using (var hmac = new HMACSHA256(mk))
            using (var aes = Aes.Create())
            {
                hmac.TransformBlock(head, 0, head.Length, null, 0);
                aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7; aes.Key = ek; aes.IV = iv;
                var lim = new LimitStream(input, ctLen, hmac); string name = null; Exception bad = null;
                try
                {
                    using (var cs = new CryptoStream(lim, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        int hi = cs.ReadByte(), lo = cs.ReadByte(); if (hi < 0 || lo < 0) throw new CryptographicException("damaged");
                        var nb = new byte[hi << 8 | lo]; ReadAll(cs, nb);
                        cs.CopyTo(output, 81920);
                        name = Encoding.UTF8.GetString(nb);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { bad = e; }   // whatever the decryptor does with bytes that don't check out (bad padding: some runtimes throw ArgumentException) - the tag decides what to say
                lim.Drain();   // whatever the decryptor didn't read still counts for the tag
                hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var tag = new byte[MacLen]; ReadAll(input, tag);
                int diff = 0; for (int i = 0; i < MacLen; i++) diff |= tag[i] ^ hmac.Hash[i];   // constant time
                if (diff != 0) throw new CryptographicException("wrong code or key, or the file was changed or damaged");
                if (bad is IOException || bad is UnauthorizedAccessException) throw new IOException(bad.Message, bad);   // the file checked out; writing the result failed (a full disk)
                if (bad != null) throw new CryptographicException("damaged", bad);
                return name;
            }
        }
        // the file being read, checking Stop between blocks
        sealed class StopStream : Stream
        {
            readonly Stream s; readonly Toolkit.Run run;
            public StopStream(Stream s, Toolkit.Run run) { this.s = s; this.run = run; }
            public override int Read(byte[] b, int off, int n) { if (run != null && run.Stop) throw new OperationCanceledException(); return s.Read(b, off, n); }
            public override bool CanRead { get { return true; } } public override bool CanSeek { get { return s.CanSeek; } } public override bool CanWrite { get { return false; } }
            public override long Length { get { return s.Length; } } public override long Position { get { return s.Position; } set { s.Position = value; } }
            public override void Flush() { }
            public override long Seek(long off, SeekOrigin so) { return s.Seek(off, so); }
            public override void SetLength(long v) { throw new NotSupportedException(); }
            public override void Write(byte[] b, int off, int n) { throw new NotSupportedException(); }
            protected override void Dispose(bool d) { if (d) s.Dispose(); base.Dispose(d); }
        }
        static StopStream In(string file, Toolkit.Run run) { return new StopStream(File.OpenRead(file), run); }
        // work in progress goes to a hidden temp file in the same folder, renamed only once it's complete and checked -
        // nothing half-written ever sits there under a real name
        static FileStream Temp(string dir, out string tmp)
        {
            tmp = Path.Combine(dir, "." + Guid.NewGuid().ToString("N") + ".part");
            Track(tmp, true);   // written down first: if AdeeboMeter is killed mid-way, the next start removes it
            var f = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write);
            try { File.SetAttributes(tmp, FileAttributes.Hidden | FileAttributes.NotContentIndexed); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return f;
        }
        static void Drop(string tmp) { try { if (tmp != null && File.Exists(tmp)) { File.SetAttributes(tmp, FileAttributes.Normal); File.Delete(tmp); } if (tmp != null && !File.Exists(tmp)) Track(tmp, false); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        // bytes that are already in memory, written the same safe way: a temp file first, renamed when complete
        static void WriteSafe(string dest, byte[] data)
        {
            string tmp = null;
            try { using (var o = Temp(Path.GetDirectoryName(dest), out tmp)) o.Write(data, 0, data.Length); Keep(tmp, dest); tmp = null; }
            finally { if (tmp != null) Drop(tmp); }
        }
        static void Keep(string tmp, string dest) { File.SetAttributes(tmp, FileAttributes.Normal); File.Move(tmp, dest); Track(tmp, false); }
        // the list of temp files in progress (data folder, parts.txt): a crash, a kill or a logoff can't leave hidden
        // half-decrypted plaintext behind for good - SweepParts at the next start deletes what's still listed
        static readonly object pk = new object();
        static string PartsFile { get { return Path.Combine(Program.DataDir, "parts.txt"); } }
        static void Track(string tmp, bool add)
        {
            lock (pk)
                try
                {
                    var l = File.Exists(PartsFile) ? File.ReadAllLines(PartsFile).Where(x => x != "" && x != tmp).Take(200).ToList() : new List<string>();
                    if (add) l.Add(tmp);
                    if (l.Count == 0) { if (File.Exists(PartsFile)) File.Delete(PartsFile); } else File.WriteAllLines(PartsFile, l);
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        public static void SweepParts()
        {
            lock (pk)
                try
                {
                    if (!File.Exists(PartsFile)) return;
                    foreach (var p in File.ReadAllLines(PartsFile).Take(200))
                        if (Path.GetFileName(p).StartsWith(".", StringComparison.Ordinal) && p.EndsWith(".part", StringComparison.OrdinalIgnoreCase) && File.Exists(p))   // only our own hidden .<guid>.part files
                            try { File.SetAttributes(p, FileAttributes.Normal); File.Delete(p); Program.Log("removed a leftover decrypt temp file: " + p); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    File.Delete(PartsFile);
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        static readonly HashSet<string> RunsWhenOpened = new HashSet<string> { ".exe", ".scr", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse", ".wsf", ".msi", ".lnk", ".hta", ".cpl", ".dll", ".reg", ".pif", ".url" };
        static bool Risky(string name) { return RunsWhenOpened.Contains(Path.GetExtension(name ?? "").ToLowerInvariant()); }

        sealed class LimitStream : Stream
        {
            readonly Stream s; long left; readonly HashAlgorithm mac;
            public LimitStream(Stream s, long n, HashAlgorithm mac) { this.s = s; left = n; this.mac = mac; }
            public override int Read(byte[] b, int off, int n)
            {
                if (left <= 0) return 0; int r = s.Read(b, off, (int)Math.Min(n, left));
                if (r <= 0) throw new CryptographicException("file is cut short");
                left -= r; if (mac != null) mac.TransformBlock(b, off, r, null, 0); return r;
            }
            public void Drain() { var buf = new byte[81920]; while (left > 0) Read(buf, 0, buf.Length); }
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
        static readonly HashSet<string> Reserved = new HashSet<string> { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "COM\u00B9", "COM\u00B2", "COM\u00B3", "LPT\u00B9", "LPT\u00B2", "LPT\u00B3", "CONIN$", "CONOUT$" };
        public static string FreeName(string dir, string name)
        {
            // the name inside an encrypted file comes from someone else: keep only its last part, only valid characters,
            // never a device name (CON, NUL, COM1...) and never longer than 120 characters
            string n = name ?? ""; int cut = Math.Max(n.LastIndexOf('\\'), n.LastIndexOf('/')); if (cut >= 0) n = n.Substring(cut + 1);
            var bad = Path.GetInvalidFileNameChars(); n = new string(n.Where(c => !bad.Contains(c) && !char.IsControl(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format).ToArray()).Trim().TrimStart('.').TrimEnd('.', ' ');   // Format: right-to-left overrides that make "x.exe" look like "x.pdf"
            if (n.Length > 120) { string ext = Path.GetExtension(n); n = n.Substring(0, 120 - Math.Min(ext.Length, 20)) + (ext.Length <= 20 ? ext : ""); }
            string stem = n.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (n == "" || Reserved.Contains(stem)) n = "opened-file" + (n == "" ? "" : "-" + n);
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
            if (file == "" || !File.Exists(file)) { rows.Add(R(2, "File", file == "" ? "type or paste the path of the file to encrypt" : "not found: " + file, "", "")); return rows; }
            var fi = new FileInfo(file); if (fi.Length > MaxBytes) { rows.Add(R(2, "File", "bigger than 4 GB", "", "")); return rows; }
            string t = (to ?? "").Trim();
            if (t == "" || t.Equals("code", StringComparison.OrdinalIgnoreCase))
            {
                string code = NewCode(), outFile, tmpE = null;
                run.Status = "Encrypting with a new code (the key takes a second to make)...";
                try
                {
                    using (var i = In(file, run)) using (var o = Temp(fi.DirectoryName, out tmpE)) EncryptCode(i, fi.Name, o, code, Iterations);
                    outFile = FreeName(fi.DirectoryName, fi.Name + ".adeebo"); Keep(tmpE, outFile);
                }
                catch (OperationCanceledException) { Drop(tmpE); rows.Add(R(1, "Encrypt", "stopped - nothing was saved", "", "")); return rows; }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is CryptographicException)) throw; Drop(tmpE); rows.Add(R(2, "Encrypt", e.Message, "", "")); return rows; }
                Program.Log("encrypted a file with a code (" + fi.Length + " bytes)");
                rows.Add(new[] { "0", "Encrypted", Path.GetFileName(outFile) + " - next to the original, which isn't changed", "", "m", "show:" + outFile });
                rows.Add(new[] { "1", "The code", code, "Give it another way than the file - read it out on the phone, or send it by text. AdeeboMeter doesn't keep it: if it's lost, nobody can open the file, you included.", "k", "copy:" + code });
                rows.Add(R(3, "To open it", "the other person opens the .adeebo file in AdeeboMeter (Toolbox › Open an encrypted file) and types the code", "AdeeboMeter is free - it's the only app that opens .adeebo files.", ""));
                return rows;
            }
            // for recipients: their key type decides - EC P-384 keys get a .adeebo (AdeeboMeter only), RSA certificates a standard .p7m
            var names0 = t.Split(',').Select(x => x.Trim()).Where(x => x != "").ToList();
            var ecs = names0.Select(CertKeys.EcRecipient).ToList();
            if (ecs.Any(x => x != null))
            {
                if (ecs.Any(x => x == null)) { rows.Add(R(2, "Recipients", "mix of key types: " + string.Join(", ", names0.Where((x, i) => ecs[i] == null)) + " have standard (RSA) keys, the others P-384", "Encrypt twice - once for each kind - or ask them for the same kind of key.", "")); return rows; }
                var list = ecs.ToList(); var who = names0.ToList();
                if (!string.Equals((alsoMe ?? "").Trim(), "no", StringComparison.OrdinalIgnoreCase)) { var me = EcKeys.All().FirstOrDefault(k => k.Here); if (me != null) { list.Add(Convert.FromBase64String(me.Pub)); who.Add("you (" + me.Name + ")"); } else rows.Add(R(1, "Also for you", "you have no P-384 key of your own on this PC - you won't be able to open this file yourself", "Make one with New key (P-384) first if you need to.", "")); }
                string outEc, tmpE = null;
                try { run.Status = "Encrypting for " + string.Join(", ", who) + " (EC P-384)..."; using (var i = In(file, run)) using (var o = Temp(fi.DirectoryName, out tmpE)) EcKeys.Encrypt(i, fi.Name, o, list); outEc = FreeName(fi.DirectoryName, fi.Name + ".adeebo"); Keep(tmpE, outEc); }
                catch (OperationCanceledException) { Drop(tmpE); rows.Add(R(1, "Encrypt", "stopped - nothing was saved", "", "")); return rows; }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is CryptographicException)) throw; Drop(tmpE); rows.Add(R(2, "Encrypt", e.Message, "", "")); return rows; }
                Program.Log("encrypted a file for " + list.Count + " P-384 recipient(s)");
                rows.Add(new[] { "0", "Encrypted", Path.GetFileName(outEc) + " for " + string.Join(", ", who) + " · EC P-384", "", "m", "show:" + outEc });
                rows.Add(R(3, "To open it", "they open it in AdeeboMeter (Open an encrypted file) - their key is found by itself, no code needed", "Only AdeeboMeter opens these: the strongest choice, unique to the app.", ""));
                return rows;
            }
            if (fi.Length > MaxCms) { rows.Add(R(2, "File", "a standard (RSA) .p7m holds up to 50 MB - for bigger files use a code, or a P-384 key (.adeebo streams up to 4 GB)", "", "")); return rows; }
            var certs = new List<X509Certificate2>(); var names = new List<string>();
            foreach (var nm in t.Split(',').Select(x => x.Trim()).Where(x => x != ""))
            {
                var c = CertKeys.Recipient(nm); if (c == null) { rows.Add(R(2, "Recipient", "no saved recipient called \"" + nm + "\"", "Save them first with Recipients (their .cer file) - or type code to share with a code.", "")); return rows; }
                certs.Add(c); names.Add(nm);
            }
            if (string.Equals((alsoMe ?? "").Trim(), "no", StringComparison.OrdinalIgnoreCase) == false)
            { var me = CertKeys.MyEncryptionCert(); if (me != null) { certs.Add(me); names.Add("you (" + me.GetNameInfo(X509NameType.SimpleName, false) + ")"); } else rows.Add(R(1, "Also for you", "you have no key of your own yet - you won't be able to open this copy", "Make one in My keys (New key) to read files you encrypt for others.", "")); }
            string outP7m = FreeName(fi.DirectoryName, fi.Name + ".p7m");
            try
            {
                run.Status = "Encrypting for " + string.Join(", ", names) + "...";
                var env = new EnvelopedCms(new ContentInfo(File.ReadAllBytes(file)), new AlgorithmIdentifier(new Oid("2.16.840.1.101.3.4.1.42")));   // AES-256-CBC
                var rc = new CmsRecipientCollection(); foreach (var c in certs) rc.Add(new CmsRecipient(SubjectIdentifierType.IssuerAndSerialNumber, c));
                env.Encrypt(rc); WriteSafe(outP7m, env.Encode());
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is CryptographicException)) throw; rows.Add(R(2, "Encrypt", e.Message, "Recipients need an RSA certificate.", "")); return rows; }
            finally { foreach (var c in certs) c.Dispose(); }
            Program.Log("encrypted a file for " + names.Count + " recipient(s)");
            rows.Add(new[] { "0", "Encrypted", Path.GetFileName(outP7m) + " for " + string.Join(", ", names), "", "m", "show:" + outP7m });
            rows.Add(R(3, "To open it", "they open it with the private key they already hold: AdeeboMeter (Open an encrypted file), or  openssl cms -decrypt -in file.p7m -inform DER -inkey key.pem -out file", "Standard CMS (S/MIME) with AES-256 - no code to pass around.", ""));
            return rows;
        }
        public static List<string[]> Decrypt(string fileIn, string code, Toolkit.Run run)
        {
            var rows = new List<string[]>(); string file = FileArg(fileIn);
            if (file == "" || !File.Exists(file)) { rows.Add(R(2, "File", file == "" ? "type or paste the path of the .adeebo or .p7m file" : "not found: " + file, "", "")); return rows; }
            var fi = new FileInfo(file); string tmp = null; int kind = 0;   // 1 code, 2 P-384 recipients, 3 key backup, 0 anything else (a .p7m)
            if (fi.Length > MaxBytes + 1024 * 1024) { rows.Add(R(2, "File", "bigger than any file AdeeboMeter makes (4 GB) - not opened", "", "")); return rows; }
            try
            {
                var h8 = new byte[8]; using (var f8 = File.OpenRead(file)) f8.Read(h8, 0, 8);
                kind = EcKeys.IsMode2(h8) ? 2 : EcKeys.IsBackup(h8) ? 3 : h8.Take(6).SequenceEqual(Magic.Take(6)) && h8[6] == 1 && h8[7] == 1 ? 1 : 0;
                if (EcKeys.IsMode2(h8))
                {
                    run.Status = "Looking for your P-384 key and checking the file..."; string name, used;
                    using (var i = In(file, run)) using (var o = Temp(fi.DirectoryName, out tmp)) name = EcKeys.Decrypt(i, o, out used);
                    string dest = FreeName(fi.DirectoryName, name); Keep(tmp, dest);
                    rows.Add(new[] { "0", "Opened", Path.GetFileName(dest) + " - with your key " + used + "; not changed or damaged since it was encrypted", "", "m", "show:" + dest });
                    rows.Add(R(3, "Who sent it", "not proven", "Anyone who has your public key can make a file for you - the check proves it arrived intact, not who made it.", ""));
                    if (Risky(dest)) rows.Add(R(1, "It's a program", Path.GetExtension(dest) + " - it runs when opened", "Only open it if you were expecting a program from this person.", ""));
                }
                else if (EcKeys.IsBackup(h8))
                {
                    if (Clean(code).Length == 0) { rows.Add(R(2, "Recovery passphrase", "this is a key backup - type its recovery passphrase in the Code box", "", "")); return rows; }
                    run.Status = "Restoring the key..."; EcKeys.Key k; using (var i = File.OpenRead(file)) k = EcKeys.Restore(i, code);
                    rows.Add(R(0, "Key restored", k.Name + " is back in My keys (EC P-384, kept by Windows) · fingerprint " + EcKeys.Fingerprint(Convert.FromBase64String(k.Pub)), "", "w"));
                }
                else if (h8.Take(6).SequenceEqual(Magic.Take(6)) && h8[6] == 1 && h8[7] == 1)   // the file's own first bytes decide, not its name
                {
                    if (Clean(code).Length == 0) { rows.Add(R(2, "Code", "type the code you were given", "", "")); return rows; }
                    run.Status = "Checking the file, then opening it..."; string name;
                    using (var i = In(file, run)) using (var o = Temp(fi.DirectoryName, out tmp)) name = DecryptCode(i, o, code);
                    string dest = FreeName(fi.DirectoryName, name); Keep(tmp, dest);
                    rows.Add(new[] { "0", "Opened", Path.GetFileName(dest) + " - not changed or damaged since it was encrypted", "", "m", "show:" + dest });
                    if (Risky(dest)) rows.Add(R(1, "It's a program", Path.GetExtension(dest) + " - it runs when opened", "Only open it if you were expecting a program from this person.", ""));
                }
                else if (h8.Take(6).SequenceEqual(Magic.Take(6))) { rows.Add(R(2, "Open", "an AdeeboMeter file of a kind this version doesn't know - made by a newer AdeeboMeter?", "Update AdeeboMeter and open it again.", "")); return rows; }
                else
                {
                    if (fi.Length > MaxCmsOpen) { rows.Add(R(2, "File", "bigger than 100 MB - too big to open as a .p7m here (it is decoded in memory)", "", "")); return rows; }
                    run.Status = "Looking for your key and opening it...";
                    var env = new EnvelopedCms(); env.Decode(File.ReadAllBytes(file)); env.Decrypt();   // Windows finds the matching private key in your store
                    string name = fi.Name.EndsWith(".p7m", StringComparison.OrdinalIgnoreCase) ? fi.Name.Substring(0, fi.Name.Length - 4) : fi.Name + ".opened";
                    string dest = FreeName(fi.DirectoryName, name); WriteSafe(dest, env.ContentInfo.Content);
                    rows.Add(new[] { "0", "Opened", Path.GetFileName(dest), "", "m", "show:" + dest });
                    rows.Add(R(3, "Tamper check", "none in this format", "A standard .p7m is encrypted but not sealed: a change on the way isn't detected. The .adeebo format checks it.", ""));
                    if (Risky(dest)) rows.Add(R(1, "It's a program", Path.GetExtension(dest) + " - it runs when opened", "Only open it if you were expecting a program from this person.", ""));
                }
            }
            catch (CryptographicException e)
            {
                rows.Add(R(2, "Couldn't open it", e.Message.IndexOf("wrong code", StringComparison.Ordinal) >= 0 ? "wrong code - or the file was changed or damaged on the way" : e.Message, kind == 1 ? "Nothing was opened or written. Check the code (letters and numbers only; dashes don't matter)." : kind == 2 ? "Nothing was opened or written. If it was made for you, ask the sender to make it again - it may have been damaged on the way." : kind == 3 ? "Check the recovery passphrase (letters and numbers; dashes don't matter)." : "This file wasn't made for any of your keys - ask the sender to encrypt it for your public key.", ""));
            }
            catch (OperationCanceledException) { rows.Add(R(1, "Open", "stopped - nothing was saved", "", "")); }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; rows.Add(R(2, "Open", e.Message, "", "")); }
            finally { Drop(tmp); }
            return rows;
        }
    }
    static class Pbkdf2Native
    {
        [System.Runtime.InteropServices.DllImport("bcrypt.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        internal static extern int BCryptOpenAlgorithmProvider(out IntPtr handle, string alg, string impl, int flags);
        [System.Runtime.InteropServices.DllImport("bcrypt.dll")]
        internal static extern int BCryptDeriveKeyPBKDF2(IntPtr prf, byte[] password, int passwordLen, byte[] salt, int saltLen, ulong iterations, [System.Runtime.InteropServices.Out] byte[] derived, int derivedLen, int flags);
        [System.Runtime.InteropServices.DllImport("bcrypt.dll")]
        internal static extern int BCryptCloseAlgorithmProvider(IntPtr handle, int flags);
    }
}
