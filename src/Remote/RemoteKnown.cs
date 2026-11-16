// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AdeeboMeter
{
    // Remembered phones: "Remember this PC" on the phone, Yes on the PC. Each phone gets its own 32-byte key P, kept here
    // protected by Windows for this user (DPAPI) and on the phone behind Face ID / fingerprint. With it, the phone can
    // reconnect without a new code: the PC waits in a room only P can name (a new one every 25 minutes), and the
    // handshake is the usual one keyed by P instead of the QR secret - fresh one-time keys each session.
    // Forget on either side ends it: the PC deletes P, and without P the phone can't find or open the room.
    static class RemotePhones
    {
        public sealed class P { public string Id = "", Name = ""; public byte[] Key; public DateTime Added; }
        public const int Max = 3, SlotSeconds = 1500;
        static readonly byte[] Entropy = Encoding.ASCII.GetBytes("adeebometer remembered phone v1");
        static readonly object k = new object();

        public static List<P> All()
        {
            var l = new List<P>();
            lock (k)
                foreach (var r in Store.Read("phones.txt"))
                {
                    if (r.Length < 4) continue;
                    try
                    {
                        var key = ProtectedData.Unprotect(Convert.FromBase64String(r[2]), Entropy, DataProtectionScope.CurrentUser);
                        DateTime a; DateTime.TryParse(r[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out a);
                        if (key.Length == 32) l.Add(new P { Id = r[0], Name = r[1], Key = key, Added = a });
                    }
                    catch (Exception e) { if (!(e is CryptographicException || e is FormatException)) throw; Program.Log("phones: a key couldn't be opened"); }
                }
            return l;
        }
        static void Save(List<P> l)
        {
            Store.Write("phones.txt", l.Select(p => new[] { p.Id, p.Name, Convert.ToBase64String(ProtectedData.Protect(p.Key, Entropy, DataProtectionScope.CurrentUser)), p.Added.ToString("o", CultureInfo.InvariantCulture) }));
        }
        public static P Add(string name)
        {
            lock (k)
            {
                var l = All(); if (l.Count >= Max) l.RemoveAt(0);   // the oldest goes when a fourth is added
                var p = new P { Id = RemoteCrypto.B64u(RemoteCrypto.Random(9)), Name = string.IsNullOrEmpty(name) ? "Phone" : name, Key = RemoteCrypto.Random(32), Added = DateTime.UtcNow };
                l.Add(p); Save(l); return p;
            }
        }
        public static bool Remove(string id) { lock (k) { var l = All(); int n = l.RemoveAll(p => p.Id == id); if (n > 0) Save(l); return n > 0; } }
        public static void Clear() { lock (k) Store.Write("phones.txt", Enumerable.Empty<string[]>()); }
        public static int Count { get { return All().Count; } }

        // the room for this phone, this 25-minute slot, attempt k (a used or timed-out room can't be reused)
        public static long Slot { get { return (long)Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds / SlotSeconds); } }
        public static string Room(byte[] key, long slot, int attempt)
        {
            return RemoteCrypto.B64u(RemoteCrypto.Hmac(key, Encoding.ASCII.GetBytes("room|" + slot.ToString(CultureInfo.InvariantCulture) + "|" + attempt.ToString(CultureInfo.InvariantCulture)))).Substring(0, 22);
        }
    }
}
