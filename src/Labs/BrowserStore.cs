// Network Lab (1.1.6): read what the lab page kept in its own browser's localStorage, straight from that window's Edge
// profile on disk - files opened for reading, no sockets (AdeeboMeter never listens). Ported from Topogram's
// browserstore.py (the localStorage half): LevelDB write-ahead logs (*.log, CRC-32C checked) and tables (*.ldb / *.sst,
// blocks maybe Snappy-compressed). A localStorage key is "_<origin>\0\x01<key>", a value \0 + UTF-16LE or \x01 + Latin-1.
// Every read is defensive: a damaged, half-written or locked file is skipped, never thrown to the caller.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AdeeboMeter
{
    static class BrowserStore
    {
        const long FileCap = 32L << 20;   // a lab's storage is a few KB; anything this big isn't ours to read

        static ulong Varint(byte[] b, ref int i)
        {
            ulong v = 0; int s = 0;
            while (true)
            {
                if (i >= b.Length) throw new InvalidDataException("varint past the end");
                byte c = b[i++]; v |= (ulong)(c & 0x7F) << s;
                if (c < 0x80) return v;
                s += 7; if (s > 63) throw new InvalidDataException("varint too long");
            }
        }
        static int VarintInt(byte[] b, ref int i) { ulong v = Varint(b, ref i); if (v > int.MaxValue) throw new InvalidDataException("length too big"); return (int)v; }

        // ---------- raw Snappy (LevelDB's block compression)
        internal static byte[] Snappy(byte[] b)
        {
            int i = 0, n = VarintInt(b, ref i); if (n > 64 << 20) throw new InvalidDataException("snappy too big");
            var o = new byte[n]; int op = 0;
            while (i < b.Length)
            {
                int t = b[i++], k = t & 3, len, off;
                if (k == 0)
                {
                    len = t >> 2;
                    if (len >= 60) { int nb = len - 59; len = 0; for (int j = 0; j < nb; j++) len |= b[i + j] << (8 * j); i += nb; }
                    len += 1;
                    if (len < 0 || i + len > b.Length || op + len > n) throw new InvalidDataException("snappy literal");
                    Buffer.BlockCopy(b, i, o, op, len); i += len; op += len; continue;
                }
                if (k == 1) { len = ((t >> 2) & 7) + 4; off = ((t >> 5) << 8) | b[i]; i += 1; }
                else if (k == 2) { len = (t >> 2) + 1; off = b[i] | b[i + 1] << 8; i += 2; }
                else { len = (t >> 2) + 1; off = b[i] | b[i + 1] << 8 | b[i + 2] << 16 | b[i + 3] << 24; i += 4; }
                if (off <= 0 || off > op || op + len > n) throw new InvalidDataException("snappy copy");
                for (int j = 0; j < len; j++, op++) o[op] = o[op - off];   // may overlap itself
            }
            if (op != n) throw new InvalidDataException("snappy length");
            return o;
        }

        // ---------- CRC-32C (Castagnoli): LevelDB's record check, stored masked
        static uint[] c32;
        internal static uint Crc32C(byte[] b, int start, int count)
        {
            if (c32 == null) { var t = new uint[256]; for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? (c >> 1) ^ 0x82F63B78u : c >> 1; t[n] = c; } c32 = t; }
            uint r = 0xFFFFFFFFu; for (int i = start; i < start + count; i++) r = c32[(r ^ b[i]) & 0xFF] ^ (r >> 8); return r ^ 0xFFFFFFFFu;
        }
        static uint Unmask(uint m) { uint r = unchecked(m - 0xA282EAD8u); return (r >> 17) | (r << 15); }
        internal static uint Mask(uint c) { return unchecked(((c >> 15) | (c << 17)) + 0xA282EAD8u); }

        sealed class Row { public ulong Seq; public byte[] Key, Val; }

        // one write batch: sequence, count, then (tag, key, value) - tag 1 = put, 0 = delete
        static void Batch(byte[] rec, List<Row> rows)
        {
            if (rec.Length < 12) return;
            ulong seq = BitConverter.ToUInt64(rec, 0); uint cnt = BitConverter.ToUInt32(rec, 8); int i = 12;
            var got = new List<Row>();
            for (uint n = 0; n < cnt; n++)
            {
                byte tag = rec[i++]; int kl = VarintInt(rec, ref i); if (i + kl > rec.Length) throw new InvalidDataException("cut key");
                var k = new byte[kl]; Buffer.BlockCopy(rec, i, k, 0, kl); i += kl;
                if (tag == 1)
                {
                    int vl = VarintInt(rec, ref i); if (i + vl > rec.Length) throw new InvalidDataException("cut value");
                    var v = new byte[vl]; Buffer.BlockCopy(rec, i, v, 0, vl); i += vl; got.Add(new Row { Seq = seq + n, Key = k, Val = v });
                }
                else got.Add(new Row { Seq = seq + n, Key = k });
            }
            rows.AddRange(got);   // a batch counts whole or not at all
        }
        static void Log(byte[] d, List<Row> rows)
        {
            int i = 0; var frag = new MemoryStream();
            while (i + 7 <= d.Length)
            {
                int left = 32768 - (i % 32768); if (left < 7) { i += left; continue; }   // the block's trailer
                uint crc = BitConverter.ToUInt32(d, i); int len = d[i + 4] | d[i + 5] << 8, typ = d[i + 6];
                if (i + 7 + len > d.Length) break;   // half-written at the end
                int at = i + 7; i += 7 + len;
                if (typ == 0 && len == 0) continue;
                var chk = new byte[len + 1]; chk[0] = (byte)typ; Buffer.BlockCopy(d, at, chk, 1, len);
                if (Unmask(crc) != Crc32C(chk, 0, chk.Length)) { frag.SetLength(0); continue; }   // damaged or being written: never the newest value
                try
                {
                    if (typ == 1) { var r = new byte[len]; Buffer.BlockCopy(d, at, r, 0, len); Batch(r, rows); frag.SetLength(0); }
                    else if (typ == 2) { frag.SetLength(0); frag.Write(d, at, len); }
                    else if (typ == 3) frag.Write(d, at, len);
                    else if (typ == 4) { frag.Write(d, at, len); Batch(frag.ToArray(), rows); frag.SetLength(0); }
                }
                catch (InvalidDataException) { frag.SetLength(0); }
                catch (IndexOutOfRangeException) { frag.SetLength(0); }
                catch (ArgumentException) { frag.SetLength(0); }
            }
        }
        static byte[] Block(byte[] d, int off, int size)
        {
            if (off < 0 || size < 0 || off + size > d.Length) throw new InvalidDataException("block outside the file");
            var raw = new byte[size]; Buffer.BlockCopy(d, off, raw, 0, size);
            return off + size < d.Length && d[off + size] == 1 ? Snappy(raw) : raw;
        }
        static IEnumerable<KeyValuePair<byte[], byte[]>> Entries(byte[] blk)
        {
            if (blk.Length < 4) yield break;
            int nre = BitConverter.ToInt32(blk, blk.Length - 4), end = blk.Length - 4 - 4 * nre, i = 0; var key = new byte[0];
            if (nre < 0 || end < 0) yield break;
            while (i < end)
            {
                int sh = VarintInt(blk, ref i), ns = VarintInt(blk, ref i), vl = VarintInt(blk, ref i);
                if (sh > key.Length || i + ns + vl > blk.Length) yield break;
                var k = new byte[sh + ns]; Buffer.BlockCopy(key, 0, k, 0, sh); Buffer.BlockCopy(blk, i, k, sh, ns); key = k; i += ns;
                var v = new byte[vl]; Buffer.BlockCopy(blk, i, v, 0, vl); i += vl;
                yield return new KeyValuePair<byte[], byte[]>(key, v);
            }
        }
        static void Table(byte[] d, List<Row> rows)
        {
            if (d.Length < 48 || BitConverter.ToUInt64(d, d.Length - 8) != 0xdb4775248b80fb57UL) return;
            var ft = new byte[48]; Buffer.BlockCopy(d, d.Length - 48, ft, 0, 48); int j = 0;
            Varint(ft, ref j); Varint(ft, ref j); int io = VarintInt(ft, ref j), isz = VarintInt(ft, ref j);
            // finding 20: an index that breaks part-way still gives the blocks it named before the break
            var handles = new List<KeyValuePair<byte[], byte[]>>();
            try { foreach (var h0 in Entries(Block(d, io, isz))) handles.Add(h0); }
            catch (InvalidDataException) { } catch (IndexOutOfRangeException) { } catch (ArgumentException) { }
            foreach (var h in handles)
            {
                try
                {
                    int q = 0, bo = VarintInt(h.Value, ref q), bs = VarintInt(h.Value, ref q);
                    foreach (var e in Entries(Block(d, bo, bs)))
                    {
                        if (e.Key.Length < 8) continue;
                        ulong tag = BitConverter.ToUInt64(e.Key, e.Key.Length - 8); var k = new byte[e.Key.Length - 8]; Buffer.BlockCopy(e.Key, 0, k, 0, k.Length);
                        rows.Add(new Row { Seq = tag >> 8, Key = k, Val = (tag & 0xFF) == 1 ? e.Value : null });
                    }
                }
                catch (InvalidDataException) { }
                catch (IndexOutOfRangeException) { }
                catch (ArgumentException) { }
            }
        }
        // newest write of each key in a LevelDB folder; deleted keys left out
        internal static Dictionary<string, byte[]> LevelDb(string folder)
        {
            var rows = new List<Row>();
            string[] files; try { files = Directory.GetFiles(folder); } catch (IOException) { return new Dictionary<string, byte[]>(); } catch (UnauthorizedAccessException) { return new Dictionary<string, byte[]>(); }
            long total = 0; int nfiles = 0;
            foreach (var f in files)
            {
                if (total > 4 * FileCap || nfiles >= 200) break;   // finding 26: a whole folder is capped too, not only each file
                bool log = f.EndsWith(".log", StringComparison.OrdinalIgnoreCase), tbl = f.EndsWith(".ldb", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".sst", StringComparison.OrdinalIgnoreCase);
                if (!log && !tbl) continue;
                try
                {
                    byte[] d;
                    using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))   // Edge may hold it open
                    {
                        if (fs.Length > FileCap) continue; total += fs.Length; nfiles++;
                        d = new byte[fs.Length]; int got = 0; while (got < d.Length) { int r = fs.Read(d, got, d.Length - got); if (r <= 0) break; got += r; }
                        if (got < d.Length) Array.Resize(ref d, got);
                    }
                    if (log) Log(d, rows); else Table(d, rows);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (InvalidDataException) { }
                catch (IndexOutOfRangeException) { }
                catch (ArgumentException) { }
            }
            var kv = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var r in rows.OrderBy(z => z.Seq)) { string k = Convert.ToBase64String(r.Key); if (r.Val == null) kv.Remove(k); else kv[k] = r.Val; }
            return kv;
        }
        static string Text(byte[] v, int from)
        {
            if (v == null || v.Length <= from) return "";
            return v[from] == 0 ? Encoding.Unicode.GetString(v, from + 1, v.Length - from - 1) : Encoding.GetEncoding(28591).GetString(v, from + 1, v.Length - from - 1);
        }
        // localStorage of file:// pages in an Edge / Chrome profile: key -> text, only the keys that start with prefix
        public static Dictionary<string, string> LocalStorage(string profile, string prefix)
        {
            var o = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in LevelDb(Path.Combine(profile, "Default", "Local Storage", "leveldb")))
            {
                byte[] k = Convert.FromBase64String(kv.Key); if (k.Length < 3 || k[0] != (byte)'_') continue;
                int z = Array.IndexOf(k, (byte)0); if (z < 0 || z + 1 >= k.Length) continue;
                string key = Text(k, z + 1);
                if (key.StartsWith(prefix, StringComparison.Ordinal)) o[key] = Text(kv.Value, 0);
            }
            return o;
        }
        // a cheap fingerprint of the folder (names, sizes, times): read again only when it changed
        public static string Stamp(string profile)
        {
            try { var d = new DirectoryInfo(Path.Combine(profile, "Default", "Local Storage", "leveldb")); if (!d.Exists) return ""; return string.Join("|", d.GetFiles().OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => f.Name + ":" + f.Length + ":" + f.LastWriteTimeUtc.Ticks)); }
            catch (IOException) { return ""; } catch (UnauthorizedAccessException) { return ""; }
        }

        // ---------- the self-test's own LevelDB log record (a put batch), so the reader is checked without a browser
        internal static byte[] TestLog(ulong seq, byte[] key, byte[] val)
        {
            var b = new MemoryStream(); b.Write(BitConverter.GetBytes(seq), 0, 8); b.Write(BitConverter.GetBytes(1u), 0, 4); b.WriteByte(1);
            Action<int> vi = n => { while (n >= 0x80) { b.WriteByte((byte)(n | 0x80)); n >>= 7; } b.WriteByte((byte)n); };
            vi(key.Length); b.Write(key, 0, key.Length); vi(val.Length); b.Write(val, 0, val.Length);
            byte[] rec = b.ToArray(), chk = new byte[rec.Length + 1]; chk[0] = 1; Buffer.BlockCopy(rec, 0, chk, 1, rec.Length);
            var o = new MemoryStream(); o.Write(BitConverter.GetBytes(Mask(Crc32C(chk, 0, chk.Length))), 0, 4); o.WriteByte((byte)(rec.Length & 0xFF)); o.WriteByte((byte)(rec.Length >> 8)); o.WriteByte(1); o.Write(rec, 0, rec.Length);
            return o.ToArray();
        }
    }
}
