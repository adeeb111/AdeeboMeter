// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AdeeboMeter
{
    // Phone remote, the cryptography. The QR holds a fresh 32-byte secret and a room name; nothing else ever shares it.
    //   1. The phone makes a one-time P-256 key pair and sends its public key, sealed with an HMAC of the QR secret
    //      (proves it scanned this QR). The PC asks you, then answers with its own one-time public key, sealed the same way.
    //   2. Both work out the same ECDH secret Z. Keys = HKDF-SHA256(salt = QR secret, input = SHA-256(Z)), one pair per
    //      direction: AES-256-CBC for privacy and HMAC-SHA256 over everything (encrypt-then-MAC) so nothing can be changed.
    //   3. Every message carries a counter; anything replayed, reordered or altered is dropped and the session ends.
    // The relay in the middle only forwards these sealed messages: it never has a key. Someone who later photographs the
    // QR still can't read the session - the one-time ECDH keys are gone when it ends (forward secrecy).
    static class RemoteCrypto
    {
        public static byte[] Random(int n) { var b = new byte[n]; using (var r = RandomNumberGenerator.Create()) r.GetBytes(b); return b; }
        public static string B64u(byte[] b) { return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
        public static byte[] Unb64u(string s)
        {
            s = (s ?? "").Replace('-', '+').Replace('_', '/'); switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; case 1: throw new FormatException("bad length"); }
            return Convert.FromBase64String(s);
        }
        public static byte[] Hmac(byte[] key, params byte[][] parts)
        {
            using (var h = new HMACSHA256(key)) { foreach (var p in parts) h.TransformBlock(p, 0, p.Length, null, 0); h.TransformFinalBlock(Array.Empty<byte>(), 0, 0); return h.Hash; }
        }
        // RFC 5869
        public static byte[] Hkdf(byte[] salt, byte[] ikm, string info, int len)
        {
            var prk = Hmac(salt, ikm); var inf = Encoding.UTF8.GetBytes(info); var okm = new byte[len]; var t = Array.Empty<byte>(); int pos = 0;
            for (byte i = 1; pos < len; i++) { t = Hmac(prk, t, inf, new[] { i }); int n = Math.Min(t.Length, len - pos); Buffer.BlockCopy(t, 0, okm, pos, n); pos += n; }
            return okm;
        }
        public static bool Same(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; int d = 0; for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i]; return d == 0; }
        public static byte[] AuthKey(byte[] secret) { return Hkdf(secret, Encoding.UTF8.GetBytes("pair"), "adeebometer remote v1 auth", 32); }

        // P-256 public keys travel as 65 bytes (0x04 | X | Y), the form a browser's WebCrypto uses
        public static byte[] Raw(ECDiffieHellmanCng k)
        {
            var blob = k.PublicKey.ToByteArray();   // "ECK1" | 32 | X | Y
            var raw = new byte[65]; raw[0] = 4; Buffer.BlockCopy(blob, 8, raw, 1, 64); return raw;
        }
        public static byte[] SharedHash(ECDiffieHellmanCng mine, byte[] theirRaw)
        {
            if (theirRaw == null || theirRaw.Length != 65 || theirRaw[0] != 4) throw new CryptographicException("not a P-256 public key");
            var blob = new byte[72]; blob[0] = 0x45; blob[1] = 0x43; blob[2] = 0x4B; blob[3] = 0x31; blob[4] = 32; Buffer.BlockCopy(theirRaw, 1, blob, 8, 64);
            using (var their = CngKey.Import(blob, CngKeyBlobFormat.EccPublicBlob))
            {
                mine.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash; mine.HashAlgorithm = CngAlgorithm.Sha256; mine.SecretPrepend = null; mine.SecretAppend = null;
                return mine.DeriveKeyMaterial(their);   // SHA-256 of the shared secret
            }
        }

        // one direction of a session: its keys and the counter it has reached
        public sealed class Lane
        {
            readonly byte[] enc, mac; readonly byte dir; long seq;
            public Lane(byte[] secret, byte[] zHash, byte dir)
            {
                var k = Hkdf(secret, zHash, "adeebometer remote v1 " + (dir == 1 ? "pc>phone" : "phone>pc"), 64);
                enc = k.Take(32).ToArray(); mac = k.Skip(32).ToArray(); this.dir = dir;
            }
            static byte[] Be(long v) { var b = new byte[8]; for (int i = 7; i >= 0; i--) { b[i] = (byte)v; v >>= 8; } return b; }
            // seq(8) | iv(16) | ciphertext | tag(32), base64url
            public string Seal(string text)
            {
                var sq = Be(++seq); var iv = Random(16); byte[] ct;
                using (var a = new AesCryptoServiceProvider { Key = enc, IV = iv, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 }) using (var e = a.CreateEncryptor()) { var p = Encoding.UTF8.GetBytes(text); ct = e.TransformFinalBlock(p, 0, p.Length); }
                var tag = Hmac(mac, new[] { dir }, sq, iv, ct);
                return B64u(sq.Concat(iv).Concat(ct).Concat(tag).ToArray());
            }
            // null when it isn't genuine or isn't the next message
            public string Open(string frame)
            {
                byte[] b; try { b = Unb64u(frame); } catch (FormatException) { return null; }
                if (b.Length < 8 + 16 + 16 + 32 || (b.Length - 56) % 16 != 0) return null;
                var sq = b.Take(8).ToArray(); var iv = b.Skip(8).Take(16).ToArray(); var ct = b.Skip(24).Take(b.Length - 56).ToArray(); var tag = b.Skip(b.Length - 32).ToArray();
                if (!Same(tag, Hmac(mac, new[] { dir }, sq, iv, ct))) return null;
                long n = 0; foreach (var x in sq) n = (n << 8) | x; if (n <= seq) return null; seq = n;
                using (var a = new AesCryptoServiceProvider { Key = enc, IV = iv, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 }) using (var d = a.CreateDecryptor())
                    try { return Encoding.UTF8.GetString(d.TransformFinalBlock(ct, 0, ct.Length)); } catch (CryptographicException) { return null; }
            }
        }
    }
}
