/* QR code (byte mode, level L) - the same encoder as the Windows app (src/Qr.cs), for "Share Wi-Fi as a QR code".
   Made on the phone; nothing is sent anywhere. window.AMQR.encode(text) -> rows of booleans; .svg(text) -> an <svg>. */
(function () {
  "use strict";
  var Blocks = [null, [7, 1, 19, 0, 0], [10, 1, 34, 0, 0], [15, 1, 55, 0, 0], [20, 1, 80, 0, 0], [26, 1, 108, 0, 0], [18, 2, 68, 0, 0], [20, 2, 78, 0, 0], [24, 2, 97, 0, 0], [30, 2, 116, 0, 0], [18, 2, 68, 2, 69],
    [20, 4, 81, 0, 0], [24, 2, 92, 2, 93], [26, 4, 107, 0, 0], [30, 3, 115, 1, 116], [22, 5, 87, 1, 88], [24, 5, 98, 1, 99], [28, 1, 107, 5, 108], [30, 5, 120, 1, 121], [28, 3, 113, 4, 114], [28, 3, 107, 5, 108],
    [28, 4, 116, 4, 117], [28, 2, 111, 7, 112], [30, 4, 121, 5, 122], [30, 6, 117, 4, 118], [26, 8, 106, 4, 107], [28, 10, 114, 2, 115], [30, 8, 122, 4, 123], [30, 3, 117, 10, 118], [30, 7, 116, 7, 117], [30, 5, 115, 10, 116]];
  var Align = [null, [], [6, 18], [6, 22], [6, 26], [6, 30], [6, 34], [6, 22, 38], [6, 24, 42], [6, 26, 46], [6, 28, 50], [6, 30, 54], [6, 32, 58], [6, 34, 62], [6, 26, 46, 66], [6, 26, 48, 70], [6, 26, 50, 74], [6, 30, 54, 78], [6, 30, 56, 82], [6, 30, 58, 86], [6, 34, 62, 90],
    [6, 28, 50, 72, 94], [6, 26, 50, 74, 98], [6, 30, 54, 78, 102], [6, 28, 54, 80, 106], [6, 32, 58, 84, 110], [6, 30, 58, 86, 114], [6, 34, 62, 90, 118], [6, 26, 50, 74, 98, 122], [6, 30, 54, 78, 102, 126], [6, 26, 52, 78, 104, 130]];
  function DataBytes(v) { var b = Blocks[v]; return b[1] * b[2] + b[3] * b[4]; }
  function Mul(x, y) { var z = 0; for (var i = 7; i >= 0; i--) { z = ((z << 1) ^ ((z >> 7) * 0x11D)) & 0xFFFF; z ^= ((y >> i) & 1) * x; } return z; }
  function Divisor(deg) { var r = new Array(deg).fill(0); r[deg - 1] = 1; var root = 1; for (var i = 0; i < deg; i++) { for (var j = 0; j < deg; j++) { r[j] = Mul(r[j], root) & 255; if (j + 1 < deg) r[j] ^= r[j + 1]; } root = Mul(root, 2) & 255; } return r; }
  function Remainder(data, div) { var r = new Array(div.length).fill(0); data.forEach(function (b) { var f = b ^ r[0]; r.shift(); r.push(0); for (var i = 0; i < r.length; i++) r[i] ^= Mul(div[i], f) & 255; }); return r; }
  function encode(text) {
    var data = Array.from(new TextEncoder().encode(text || "")), ver = 0;
    for (var v = 1; v <= 30; v++) if ((v < 10 ? 12 : 20) + data.length * 8 <= DataBytes(v) * 8) { ver = v; break; }
    if (!ver) return null;
    var bits = [], put = function (val, n) { for (var i = n - 1; i >= 0; i--) bits.push(((val >> i) & 1) !== 0); };
    put(4, 4); put(data.length, ver < 10 ? 8 : 16); data.forEach(function (b) { put(b, 8); });
    var cap = DataBytes(ver) * 8; put(0, Math.min(4, cap - bits.length)); while (bits.length % 8) bits.push(false);
    for (var pad = 0xEC; bits.length < cap; pad ^= 0xEC ^ 0x11) put(pad, 8);
    var cw = new Array(bits.length / 8).fill(0); for (var i = 0; i < bits.length; i++) if (bits[i]) cw[i >> 3] |= 0x80 >> (i & 7);
    var bl = Blocks[ver], ec = bl[0], blocks = [], pos = 0;
    for (var g = 0; g < 2; g++) for (var k = 0; k < bl[1 + g * 2]; k++) { blocks.push(cw.slice(pos, pos + bl[2 + g * 2])); pos += bl[2 + g * 2]; }
    var div = Divisor(ec), ecs = blocks.map(function (d) { return Remainder(d, div); }), all = [], maxLen = Math.max.apply(null, blocks.map(function (b) { return b.length; }));
    for (i = 0; i < maxLen; i++) blocks.forEach(function (b) { if (i < b.length) all.push(b[i]); });
    for (i = 0; i < ec; i++) ecs.forEach(function (e) { all.push(e[i]); });
    var size = ver * 4 + 17, m = [], fn = []; for (i = 0; i < size; i++) { m.push(new Array(size).fill(false)); fn.push(new Array(size).fill(false)); }
    var set = function (x, y, dark) { m[y][x] = dark; fn[y][x] = true; };
    for (i = 0; i < size; i++) { set(6, i, i % 2 === 0); set(i, 6, i % 2 === 0); }
    [[3, 3], [size - 4, 3], [3, size - 4]].forEach(function (c) { for (var dy = -4; dy <= 4; dy++) for (var dx = -4; dx <= 4; dx++) { var x = c[0] + dx, y = c[1] + dy; if (x < 0 || y < 0 || x >= size || y >= size) continue; var dist = Math.max(Math.abs(dx), Math.abs(dy)); set(x, y, dist !== 2 && dist !== 4); } });
    var al = Align[ver];
    for (i = 0; i < al.length; i++) for (var j = 0; j < al.length; j++) { if ((i === 0 && j === 0) || (i === 0 && j === al.length - 1) || (i === al.length - 1 && j === 0)) continue; for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++) set(al[i] + dx, al[j] + dy, Math.max(Math.abs(dx), Math.abs(dy)) !== 1); }
    Format(size, 0, set);
    if (ver >= 7) { var rem = ver; for (i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25); var vb = ver * 4096 + rem; for (i = 0; i < 18; i++) { var b2 = (Math.floor(vb / Math.pow(2, i)) & 1) !== 0, a = size - 11 + i % 3, c2 = Math.floor(i / 3); set(a, c2, b2); set(c2, a, b2); } }
    var bi = 0, nbits = all.length * 8;
    for (var right = size - 1; right >= 1; right -= 2) { if (right === 6) right = 5; for (var vert = 0; vert < size; vert++) for (j = 0; j < 2; j++) { var x = right - j, up = ((right + 1) & 2) === 0, y = up ? size - 1 - vert : vert; if (!fn[y][x] && bi < nbits) { m[y][x] = ((all[bi >> 3] >> (7 - (bi & 7))) & 1) !== 0; bi++; } } }
    var best = 0, bestScore = Infinity;
    for (k = 0; k < 8; k++) { Mask(m, fn, size, k); Format(size, k, set); var sc = Penalty(m, size); if (sc < bestScore) { bestScore = sc; best = k; } Mask(m, fn, size, k); }
    Mask(m, fn, size, best); Format(size, best, set);
    return m;
  }
  function Format(size, mask, set) {
    var data = 1 << 3 | mask, rem = data; for (var i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
    var bits = (data << 10 | rem) ^ 0x5412, bit = function (i) { return ((bits >> i) & 1) !== 0; };
    for (i = 0; i <= 5; i++) set(8, i, bit(i));
    set(8, 7, bit(6)); set(8, 8, bit(7)); set(7, 8, bit(8));
    for (i = 9; i < 15; i++) set(14 - i, 8, bit(i));
    for (i = 0; i < 8; i++) set(size - 1 - i, 8, bit(i));
    for (i = 8; i < 15; i++) set(8, size - 15 + i, bit(i));
    set(8, size - 8, true);
  }
  function Mask(m, fn, size, k) {
    for (var y = 0; y < size; y++) for (var x = 0; x < size; x++) {
      if (fn[y][x]) continue; var inv;
      switch (k) { case 0: inv = (x + y) % 2 === 0; break; case 1: inv = y % 2 === 0; break; case 2: inv = x % 3 === 0; break; case 3: inv = (x + y) % 3 === 0; break;
        case 4: inv = (Math.floor(x / 3) + Math.floor(y / 2)) % 2 === 0; break; case 5: inv = x * y % 2 + x * y % 3 === 0; break; case 6: inv = (x * y % 2 + x * y % 3) % 2 === 0; break; default: inv = ((x + y) % 2 + x * y % 3) % 2 === 0; }
      if (inv) m[y][x] = !m[y][x];
    }
  }
  function Penalty(m, size) {
    var p = 0, dark = 0;
    for (var pass = 0; pass < 2; pass++) for (var a = 0; a < size; a++) {
      var line = []; for (var b = 0; b < size; b++) line.push(pass === 0 ? m[a][b] : m[b][a]);
      var run = 1, prev = line[0];
      for (b = 1; b < size; b++) { if (line[b] === prev) { run++; if (run === 5) p += 3; else if (run > 5) p++; } else { run = 1; prev = line[b]; } }
      var light = function (i) { return i < 0 || i >= size || !line[i]; };
      for (b = 0; b + 6 < size; b++) if (line[b] && !line[b + 1] && line[b + 2] && line[b + 3] && line[b + 4] && !line[b + 5] && line[b + 6]) {
        var before = light(b - 1) && light(b - 2) && light(b - 3) && light(b - 4), after = light(b + 7) && light(b + 8) && light(b + 9) && light(b + 10); if (before || after) p += 40; }
    }
    for (var y = 0; y < size; y++) for (var x = 0; x < size; x++) { if (m[y][x]) dark++; if (x + 1 < size && y + 1 < size && m[y][x] === m[y][x + 1] && m[y][x] === m[y + 1][x] && m[y][x] === m[y + 1][x + 1]) p += 3; }
    var total = size * size, k = Math.trunc((Math.abs(dark * 20 - total * 10) + total - 1) / total) - 1; p += Math.max(0, k) * 10;
    return p;
  }
  function svg(text) {
    var m = encode(text); if (!m) return ""; var n = m.length, q = 4, d = "";
    for (var y = 0; y < n; y++) for (var x = 0; x < n; x++) if (m[y][x]) d += "M" + (x + q) + " " + (y + q) + "h1v1h-1z";
    return '<svg viewBox="0 0 ' + (n + 2 * q) + " " + (n + 2 * q) + '" shape-rendering="crispEdges" role="img" aria-label="QR code"><rect width="100%" height="100%" fill="#fff"/><path d="' + d + '" fill="#000"/></svg>';
  }
  (typeof window !== "undefined" ? window : globalThis).AMQR = { encode: encode, svg: svg };
})();
