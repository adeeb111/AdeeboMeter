// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace AdeeboMeter
{
    // A QR code made here, offline: byte mode, error correction L (about 7%), versions 1-10 (up to 271 bytes).
    // Written from the QR specification (ISO/IEC 18004) - no library, nothing sent: the picture holds the text and nothing else.
    static class Qr
    {
        // per version 1..10 at level L: error-correction bytes per block, then (blocks, data bytes) for each of two groups
        static readonly int[][] Blocks = {
            null,
            new[] { 7, 1, 19, 0, 0 }, new[] { 10, 1, 34, 0, 0 }, new[] { 15, 1, 55, 0, 0 }, new[] { 20, 1, 80, 0, 0 }, new[] { 26, 1, 108, 0, 0 },
            new[] { 18, 2, 68, 0, 0 }, new[] { 20, 2, 78, 0, 0 }, new[] { 24, 2, 97, 0, 0 }, new[] { 30, 2, 116, 0, 0 }, new[] { 18, 2, 68, 2, 69 } };
        static readonly int[][] Align = { null, Array.Empty<int>(), new[] { 6, 18 }, new[] { 6, 22 }, new[] { 6, 26 }, new[] { 6, 30 }, new[] { 6, 34 }, new[] { 6, 22, 38 }, new[] { 6, 24, 42 }, new[] { 6, 26, 46 }, new[] { 6, 28, 50 } };
        public const int MaxBytes = 271;
        static int DataBytes(int v) { var b = Blocks[v]; return b[1] * b[2] + b[3] * b[4]; }

        // the modules, true = dark; null when the text is too long
        public static bool[,] Encode(string text)
        {
            var data = Encoding.UTF8.GetBytes(text ?? "");
            int ver = 0; for (int v = 1; v <= 10; v++) if ((v < 10 ? 12 : 20) + data.Length * 8 <= DataBytes(v) * 8) { ver = v; break; }
            if (ver == 0) return null;
            // the bit stream: mode 0100 (bytes), the count, the bytes, a terminator, then pad bytes
            var bits = new List<bool>(); Action<int, int> put = (val, n) => { for (int i = n - 1; i >= 0; i--) bits.Add(((val >> i) & 1) != 0); };
            put(4, 4); put(data.Length, ver < 10 ? 8 : 16); foreach (var b in data) put(b, 8);
            int cap = DataBytes(ver) * 8; put(0, Math.Min(4, cap - bits.Count)); while (bits.Count % 8 != 0) bits.Add(false);
            for (int pad = 0xEC; bits.Count < cap; pad ^= 0xEC ^ 0x11) put(pad, 8);
            var cw = new byte[bits.Count / 8]; for (int i = 0; i < bits.Count; i++) if (bits[i]) cw[i >> 3] |= (byte)(0x80 >> (i & 7));
            // split into blocks, error correction per block, then interleave
            var bl = Blocks[ver]; int ec = bl[0]; var blocks = new List<byte[]>(); int pos = 0;
            for (int g = 0; g < 2; g++) for (int k = 0; k < bl[1 + g * 2]; k++) { var d = new byte[bl[2 + g * 2]]; Array.Copy(cw, pos, d, 0, d.Length); pos += d.Length; blocks.Add(d); }
            var div = Divisor(ec); var ecs = blocks.Select(d => Remainder(d, div)).ToList();
            var all = new List<byte>(); int maxLen = blocks.Max(b => b.Length);
            for (int i = 0; i < maxLen; i++) foreach (var b in blocks) if (i < b.Length) all.Add(b[i]);
            for (int i = 0; i < ec; i++) foreach (var e in ecs) all.Add(e[i]);
            // the fixed patterns, then the data in the zig-zag, then the best mask
            int size = ver * 4 + 17; var m = new bool[size, size]; var fn = new bool[size, size];
            Action<int, int, bool> set = (x, y, dark) => { m[y, x] = dark; fn[y, x] = true; };
            for (int i = 0; i < size; i++) { set(6, i, i % 2 == 0); set(i, 6, i % 2 == 0); }
            foreach (var c in new[] { new[] { 3, 3 }, new[] { size - 4, 3 }, new[] { 3, size - 4 } })
                for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++) { int x = c[0] + dx, y = c[1] + dy; if (x < 0 || y < 0 || x >= size || y >= size) continue; int dist = Math.Max(Math.Abs(dx), Math.Abs(dy)); set(x, y, dist != 2 && dist != 4); }
            var al = Align[ver];
            for (int i = 0; i < al.Length; i++) for (int j = 0; j < al.Length; j++)
            {
                if ((i == 0 && j == 0) || (i == 0 && j == al.Length - 1) || (i == al.Length - 1 && j == 0)) continue;
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) set(al[i] + dx, al[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
            Format(m, fn, size, 0, set);
            if (ver >= 7)
            {
                int rem = ver; for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                long vb = (long)ver << 12 | (long)rem;
                for (int i = 0; i < 18; i++) { bool b = ((vb >> i) & 1) != 0; int a = size - 11 + i % 3, c = i / 3; set(a, c, b); set(c, a, b); }
            }
            int bi = 0, nbits = all.Count * 8;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j; bool up = ((right + 1) & 2) == 0; int y = up ? size - 1 - vert : vert;
                        if (!fn[y, x] && bi < nbits) { m[y, x] = ((all[bi >> 3] >> (7 - (bi & 7))) & 1) != 0; bi++; }
                    }
            }
            int best = 0; long bestScore = long.MaxValue;
            for (int k = 0; k < 8; k++)
            {
                Mask(m, fn, size, k); Format(m, fn, size, k, set);
                long sc = Penalty(m, size); if (sc < bestScore) { bestScore = sc; best = k; }
                Mask(m, fn, size, k);   // the mask is its own undo
            }
            Mask(m, fn, size, best); Format(m, fn, size, best, set);
            return m;
        }
        static void Format(bool[,] m, bool[,] fn, int size, int mask, Action<int, int, bool> set)
        {
            int data = 1 << 3 | mask;   // level L = 01
            int rem = data; for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = (data << 10 | rem) ^ 0x5412; Func<int, bool> bit = i => ((bits >> i) & 1) != 0;
            for (int i = 0; i <= 5; i++) set(8, i, bit(i));
            set(8, 7, bit(6)); set(8, 8, bit(7)); set(7, 8, bit(8));
            for (int i = 9; i < 15; i++) set(14 - i, 8, bit(i));
            for (int i = 0; i < 8; i++) set(size - 1 - i, 8, bit(i));
            for (int i = 8; i < 15; i++) set(8, size - 15 + i, bit(i));
            set(8, size - 8, true);
        }
        static void Mask(bool[,] m, bool[,] fn, int size, int k)
        {
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                if (fn[y, x]) continue; bool inv;
                switch (k)
                {
                    case 0: inv = (x + y) % 2 == 0; break;
                    case 1: inv = y % 2 == 0; break;
                    case 2: inv = x % 3 == 0; break;
                    case 3: inv = (x + y) % 3 == 0; break;
                    case 4: inv = (x / 3 + y / 2) % 2 == 0; break;
                    case 5: inv = x * y % 2 + x * y % 3 == 0; break;
                    case 6: inv = (x * y % 2 + x * y % 3) % 2 == 0; break;
                    default: inv = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                }
                if (inv) m[y, x] = !m[y, x];
            }
        }
        // the spec's penalty: long runs, 2x2 blocks, finder look-alikes and the dark/light balance
        static long Penalty(bool[,] m, int size)
        {
            long p = 0; int dark = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < size; a++)
                {
                    int run = 1; bool prev = Get(m, pass, a, 0); var line = new bool[size];
                    for (int b = 0; b < size; b++) line[b] = Get(m, pass, a, b);
                    for (int b = 1; b < size; b++) { if (line[b] == prev) { run++; if (run == 5) p += 3; else if (run > 5) p++; } else { run = 1; prev = line[b]; } }
                    for (int b = 0; b + 6 < size; b++)
                        if (line[b] && !line[b + 1] && line[b + 2] && line[b + 3] && line[b + 4] && !line[b + 5] && line[b + 6])
                        {
                            bool before = b >= 4 && !line[b - 1] && !line[b - 2] && !line[b - 3] && !line[b - 4], after = b + 10 < size && !line[b + 7] && !line[b + 8] && !line[b + 9] && !line[b + 10];
                            if (before || after) p += 40;
                        }
                }
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { if (m[y, x]) dark++; if (x + 1 < size && y + 1 < size && m[y, x] == m[y, x + 1] && m[y, x] == m[y + 1, x] && m[y, x] == m[y + 1, x + 1]) p += 3; }
            int total = size * size; int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1; p += Math.Max(0, k) * 10;
            return p;
        }
        static bool Get(bool[,] m, int pass, int a, int b) { return pass == 0 ? m[a, b] : m[b, a]; }
        static int Mul(int x, int y) { int z = 0; for (int i = 7; i >= 0; i--) { z = (z << 1) ^ ((z >> 7) * 0x11D); z ^= ((y >> i) & 1) * x; } return z; }
        static byte[] Divisor(int degree)
        {
            var r = new byte[degree]; r[degree - 1] = 1; int root = 1;
            for (int i = 0; i < degree; i++) { for (int j = 0; j < degree; j++) { r[j] = (byte)Mul(r[j], root); if (j + 1 < degree) r[j] ^= r[j + 1]; } root = Mul(root, 0x02); }
            return r;
        }
        static byte[] Remainder(byte[] data, byte[] div)
        {
            var r = new byte[div.Length];
            foreach (var b in data) { int f = b ^ r[0]; Array.Copy(r, 1, r, 0, r.Length - 1); r[r.Length - 1] = 0; for (int i = 0; i < r.Length; i++) r[i] ^= (byte)Mul(div[i], f); }
            return r;
        }
        // black on white, a 4-module quiet border, each module px pixels
        public static Bitmap Picture(bool[,] m, int px)
        {
            int size = m.GetLength(0), q = 4, side = (size + 2 * q) * px;
            var bmp = new Bitmap(side, side, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) if (m[y, x]) g.FillRectangle(Brushes.Black, (x + q) * px, (y + q) * px, px, px);
            }
            return bmp;
        }
    }
}
