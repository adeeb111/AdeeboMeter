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
    // Forget on either side ends it. Forgotten on the PC: P is deleted here and the PC never waits in its rooms again, so the
    // phone (which still has its copy of P) finds no PC and its tries go nowhere until it's forgotten there too (bug-hunt
    // L-257 - the phone's copy can't be revoked from here; it just has nothing left to open). Forgotten on the phone: no P,
    // so it can't even name the room.
    static class RemotePhones
    {
        public sealed class P { public string Id = "", Name = ""; public byte[] Key; public DateTime Added; }
        public const int Max = 3, SlotSeconds = 1500;
        static readonly byte[] Entropy = Encoding.ASCII.GetBytes("adeebometer remembered phone v1");
        static readonly object k = new object();

        public static volatile int Unreadable;
        public static List<P> All()
        {
            var l = new List<P>(); int bad = 0;
            lock (k)
                foreach (var r in Store.Read("phones.txt"))
                {
                    if (r.Length < 4) { bad++; continue; }
                    try
                    {
                        var key = ProtectedData.Unprotect(Convert.FromBase64String(r[2]), Entropy, DataProtectionScope.CurrentUser);
                        DateTime a; DateTime.TryParse(r[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out a);
                        if (key.Length == 32) l.Add(new P { Id = r[0], Name = r[1], Key = key, Added = a });
                    }
                    catch (Exception e) { if (!(e is CryptographicException || e is FormatException)) throw; Program.Log("phones: a key couldn't be opened"); bad++; }
                }
            Unreadable = bad;   // bug-hunt L-239: the phone page says so - a phone isn't forgotten without a word
            return l;
        }
        static void Save(List<P> l)
        {
            Store.Write("phones.txt", l.Select(p => new[] { p.Id, p.Name, Convert.ToBase64String(ProtectedData.Protect(p.Key, Entropy, DataProtectionScope.CurrentUser)), p.Added.ToString("o", CultureInfo.InvariantCulture) }));
        }
        public static void Rename(string id, string name)
        {
            lock (k) { var l = All(); var p = l.FirstOrDefault(z => z.Id == id); if (p == null || string.IsNullOrEmpty(name) || p.Name == name) return; p.Name = name; Save(l); }
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
        // AM-CG-375: when a fourth is asked for, the question says which one it replaces
        public static string Replacing()
        {
            var l = All(); if (l.Count < Max) return "";
            var o = l[0]; return "\n\n" + L.T("This PC already remembers") + " " + Max.ToString(CultureInfo.InvariantCulture) + " " + L.T("phones. Saying yes forgets the oldest:") + " " + o.Name + " (" + o.Added.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture) + ").";
        }
        public static bool Remove(string id) { lock (k) { var l = All(); int n = l.RemoveAll(p => p.Id == id); if (n > 0) Save(l); return n > 0; } }
        public static void Clear() { lock (k) Store.Write("phones.txt", Enumerable.Empty<string[]>()); }
        public static int Count { get { return All().Count; } }

        // the room for this phone, this 25-minute slot, attempt k (a used or timed-out room can't be reused)
        // bug-hunt ML-14: the phone corrects its clock against the internet's before picking a room, so the PC does too -
        // a PC clock minutes off would otherwise wait in a room the phone never looks in. Checked once an hour, off the UI thread.
        public static long Slot { get { CheckClock(); return (long)Math.Floor(((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds + clockOff) / SlotSeconds); } }
        static double clockOff; static DateTime clockAt = DateTime.MinValue; static volatile bool clockBusy;
        static void CheckClock()
        {
            if (clockBusy || (DateTime.UtcNow - clockAt).TotalMinutes < 60) return;
            clockBusy = true; clockAt = DateTime.UtcNow;
            new System.Threading.Thread(() =>
            {
                try
                {
                    double t0 = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                    string j = HttpSmall.Get("https://adeebometer-live.orkoadeeb.workers.dev/ping?pc" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture), 4) ?? "";
                    double t1 = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                    var m = System.Text.RegularExpressions.Regex.Match(j, @"""now""\s*:\s*(\d{12,14})");
                    if (m.Success && t1 - t0 < 5000) { double off = (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) - (t0 + t1) / 2) / 1000; clockOff = Math.Abs(off) > 20 && Math.Abs(off) < 86400 ? off : 0; }
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; clockAt = DateTime.UtcNow.AddMinutes(-50); }   // offline: try again in 10 minutes
                finally { clockBusy = false; }
            }) { IsBackground = true, Name = "phone-clock" }.Start();
        }
        public static string Room(byte[] key, long slot, int attempt)
        {
            return RemoteCrypto.B64u(RemoteCrypto.Hmac(key, Encoding.ASCII.GetBytes("room|" + slot.ToString(CultureInfo.InvariantCulture) + "|" + attempt.ToString(CultureInfo.InvariantCulture)))).Substring(0, 22);
        }
    }
}
