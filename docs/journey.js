/* AdeeboMeter phone app - Signal journey (Adeeb, 16 Nov).
   A phone can't let a web app read its signal, so the person screen-records the carrier's own field test screen
   (iPhone: *3001#12345#* Dashboard; Android: *#0011# on Samsung, *#*#4636#*#* Phone info on most others) and this reads
   the numbers off the recording - on the phone, with Tesseract (Apache 2.0, /vendor/tesseract). Nothing is uploaded or kept:
   the text read from each moment is turned into numbers and thrown away, and lines like the phone number or IMEI are
   dropped as they're read. At most 30 seconds of video (one moment a second) or 30 screenshots.

     recording ─► frames (1 a second) ─► text ─► fields ─► the journey: grades, cells, antennas, moments, verdict */
(function () {
  "use strict";
  var BASE = "/vendor/tesseract/", MAXS = 30;
  var AMJ = window.AMJ = { MAXS: MAXS };
  var worker = null, loading = null;

  function script(src) { return new Promise(function (ok, no) { var s = document.createElement("script"); s.src = src; s.onload = ok; s.onerror = function () { no(new Error("couldn't load " + src)); }; document.head.appendChild(s); }); }
  // the reader: about 10 MB the first time (then the browser keeps it), loaded only when someone reads a recording
  AMJ.ready = function () {
    if (worker) return Promise.resolve(worker);
    if (loading) return loading;
    loading = (window.Tesseract ? Promise.resolve() : script(BASE + "tesseract.min.js"))
      .then(function () { return window.Tesseract.createWorker("eng", 1, { workerPath: BASE + "worker.min.js", corePath: BASE, langPath: BASE, gzip: true }); })
      .then(function (w) { worker = w; return w; });
    loading.catch(function () { loading = null; });
    return loading;
  };
  AMJ.stop = function () { var w = worker; worker = null; loading = null; if (w) try { w.terminate(); } catch (e) {} };

  // ---------- one frame's text -> fields. iPhone writes "RSRP -90 dBm"; Android "RSRP:-95", "rsrp=-95"; Samsung many on a line
  var NOKEEP = /phone\s*number|imei|imsi|iccid|meid|msisdn|serial|e-?mail|@/i;
  function num(s) { return parseFloat(String(s).replace(/[Oo]/g, "0").replace(/[−–]/g, "-")); }
  function inR(v, a, b) { return isFinite(v) && v >= a && v <= b; }
  AMJ.parse = function (text) {
    var f = { lte: {}, nr: {}, gsm: {}, umts: {} }, sec = "lte", any = false;
    String(text || "").split(/\r?\n/).forEach(function (raw) {
      var l = raw.replace(/[|»«]/g, " ").replace(/\s+/g, " ").trim(); if (!l || NOKEEP.test(l)) return;
      if (/^(5G\s*)?NR(\s*(SA|NSA))?$|^5G$/i.test(l)) { sec = "nr"; return; }
      if (/^(LTE|4G)$/i.test(l)) { sec = "lte"; return; }
      if (/^(UMTS|WCDMA|3G)$/i.test(l)) { sec = "umts"; return; }
      if (/^(GSM|2G)$/i.test(l)) { sec = "gsm"; return; }
      var m;
      if ((m = /^Carrier\b[:\s]+(.{1,30})$/i.exec(l))) { f.carrier = m[1].trim(); any = true; return; }
      if ((m = /Network\s*Capabilit\w*[:\s]+(.{1,30})$/i.exec(l))) { f.tech = m[1].trim(); any = true; return; }
      if ((m = /\bPLMN\b[^\d]{0,4}(\d{3})\s*-?\s*(\d{2,3})\b/i.exec(l))) { f.plmn = m[1] + "-" + m[2]; any = true; }
      var g = /\bNR\b|\bSS[-_ ]?(RSRP|RSRQ|SINR)|\b5G\b/i.test(l) && !/LTE/i.test(l) ? f.nr : f[sec];
      // Android sometimes writes 5G on the same line as LTE: "NR-RSRP: -101" - the prefix decides
      function put(k, v) { g[k] = v; any = true; }
      var re = /\b(?:NR[-_ ]?|SS[-_ ]?|CSI[-_ ]?)?(RSRP|RSRQ|RSSI|RSSNR|RS[-_ ]?SINR|SINR|SNR)\s*([01OoA-D])?\b\s*[:=]?\s*(-?\s?\d{1,3}(?:[.,]\d+)?)/gi, x;
      while ((x = re.exec(l))) {
        var k = x[1].toUpperCase().replace(/[-_ ]/g, ""), v = num(x[3].replace(/\s/g, "").replace(",", ".")), port = x[2] ? String(x[2]).replace(/[Oo]/, "0") : "";
        var tgt = /^NR|^SS/i.test(x[0]) ? f.nr : g;
        if (k === "RSRP" && inR(v, -156, -30)) { tgt.rsrp = v; any = true; }
        else if (k === "RSRQ" && inR(v, -43, 20)) { tgt.rsrq = v; any = true; }
        else if (k === "RSSI" && inR(v, -125, -20)) { tgt.rssi = v; any = true; }
        else if (/SINR|SNR|RSSNR/.test(k) && inR(v, -25, 45)) { (tgt.sinr = tgt.sinr || {})[/^[0-9]$/.test(port) ? port : String(Object.keys(tgt.sinr || {}).length ? "x" + Object.keys(tgt.sinr).length : "0")] = v; any = true; }
      }
      if ((m = /\b(?:m?PCI|PhyCellId|Physical\s*Cell\s*ID)\b[^\d]{0,4}(\d{1,4})\b/i.exec(l)) && inR(+m[1], 0, 1007)) put("pci", +m[1]);
      if ((m = /\b(?:Cell\s*Id|m?Ci|ECI|CID)\b[^\d]{0,4}(\d{4,10})\b/i.exec(l))) put("cid", +m[1]);
      if ((m = /\bm?TAC\b[^\d]{0,4}(\d{1,8})\b/i.exec(l))) { f.tac = +m[1]; any = true; }
      if ((m = /\b(?:NR[-_ ]?ARFCN|EARFCN|DL[-_ ]?EARFCN|UARFCN|ARFCN)\b[^\d]{0,4}(\d{1,7})\b/i.exec(l))) put("arfcn", +m[1]);
      if ((m = /\bBand\b[^\dnB]{0,4}([nB]?)(\d{1,3})\b/i.exec(l)) || (m = /\b([Bn])(\d{1,3})\b(?=.*(?:MHz|EARFCN|ARFCN|BW))/.exec(l))) put("band", (sec === "nr" || m[1] === "n" ? "n" : "B") + m[2]);
      if ((m = /\b(?:BW|Bandwidth|DL\s*BW)\b[^\d]{0,4}(\d{1,3}(?:\.\d)?)\s*MHz/i.exec(l))) put("bw", num(m[1]));
      if ((m = /\bCQI\b[^\d]{0,4}(\d{1,2})\b/i.exec(l)) && inR(+m[1], 0, 15)) put("cqi", +m[1]);
      if ((m = /\b(?:TX\s*Pwr|Tx\s*Power|TxPower)\b[^-\d]{0,4}(-?\d{1,2})\b/i.exec(l))) put("tx", +m[1]);
    });
    return any ? f : null;
  };

  // ---------- reading pictures: grey, dark screens turned light, then each pixel compared with its own neighbourhood
  // so text on a grey card (iPhone's dark Field Test) comes out as plain black on white. At most 900 px wide.
  function prep(src, w, h) {
    var s = Math.min(1, 900 / w), c = document.createElement("canvas"); c.width = Math.round(w * s); c.height = Math.round(h * s);
    var x = c.getContext("2d", { willReadFrequently: true }); x.drawImage(src, 0, 0, c.width, c.height);
    var d = x.getImageData(0, 0, c.width, c.height), p = d.data, sum = 0, n = p.length / 4;
    for (var i = 0; i < p.length; i += 4) { var y = 0.299 * p[i] + 0.587 * p[i + 1] + 0.114 * p[i + 2]; p[i] = p[i + 1] = p[i + 2] = y; sum += y; }
    if (sum / n < 110) for (var j = 0; j < p.length; j += 4) p[j] = p[j + 1] = p[j + 2] = 255 - p[j];
    var W = c.width, H = c.height, W1 = W + 1, I = new Float64Array(W1 * (H + 1)), r = Math.max(8, W >> 5);
    for (var yy = 0; yy < H; yy++) { var row = 0; for (var xx = 0; xx < W; xx++) { row += p[(yy * W + xx) * 4]; I[(yy + 1) * W1 + xx + 1] = I[yy * W1 + xx + 1] + row; } }
    var sig = 0;
    for (yy = 0; yy < H; yy++) { var y0 = Math.max(0, yy - r), y1 = Math.min(H, yy + r + 1);
      for (xx = 0; xx < W; xx++) { var x0 = Math.max(0, xx - r), x1 = Math.min(W, xx + r + 1), o = (yy * W + xx) * 4;
        var m = (I[y1 * W1 + x1] - I[y0 * W1 + x1] - I[y1 * W1 + x0] + I[y0 * W1 + x0]) / ((y1 - y0) * (x1 - x0));
        var ink = p[o] < m * 0.85; p[o] = p[o + 1] = p[o + 2] = ink ? 0 : 255;
        // the same picture twice isn't read twice: every pixel counts, so one changed digit is a new picture
        if (ink) sig = (sig * 31 + o) >>> 0; } }
    x.putImageData(d, 0, 0);
    return { c: c, sig: sig };
  }
  async function read(canvas) { var w = await AMJ.ready(); var r = await w.recognize(canvas); return r && r.data ? r.data.text : ""; }
  function wait(el, ev, ms) { return new Promise(function (ok, no) { var t = setTimeout(function () { no(new Error("the video didn't answer")); }, ms); el.addEventListener(ev, function h() { clearTimeout(t); el.removeEventListener(ev, h); ok(); }); }); }

  // a screen recording: one moment a second, the first 30 seconds at most
  AMJ.readVideo = async function (file, prog, stopped) {
    var url = URL.createObjectURL(file), v = document.createElement("video"), out = [];
    v.muted = true; v.playsInline = true; v.setAttribute("playsinline", ""); v.preload = "auto"; v.src = url;
    try {
      await wait(v, "loadedmetadata", 15000);
      try { await v.play(); v.pause(); } catch (e) {}   // iOS decodes frames only after a first play
      var full = isFinite(v.duration) ? v.duration : 0, dur = Math.min(full, MAXS), n = Math.max(1, Math.floor(dur)), last = null;
      for (var i = 0; i < n; i++) {
        if (stopped()) break;
        var t = Math.min(i + 0.5, Math.max(0, dur - 0.05)); v.currentTime = t; await wait(v, "seeked", 8000);
        var pc = prep(v, v.videoWidth, v.videoHeight), txt = null;
        if (last && last.sig === pc.sig) out.push({ t: t, f: last.f, same: true });
        else { prog(i, n, "Reading moment " + (i + 1) + " of " + n); txt = await read(pc.c); var f = AMJ.parse(txt); out.push({ t: t, f: f }); last = { sig: pc.sig, f: f }; txt = null; }
      }
      return { frames: out, cut: full > MAXS + 0.5 ? full : 0 };
    } finally { v.removeAttribute("src"); v.load(); URL.revokeObjectURL(url); }
  };
  // screenshots: up to 30, put in the order they were taken
  AMJ.readImages = async function (files, prog, stopped) {
    var L = Array.prototype.slice.call(files, 0, MAXS).sort(function (a, b) { return a.lastModified - b.lastModified; }), out = [], t0 = L.length ? L[0].lastModified : 0;
    for (var i = 0; i < L.length; i++) {
      if (stopped()) break; prog(i, L.length, "Reading picture " + (i + 1) + " of " + L.length);
      var bm = await createImageBitmap(L[i]), pc = prep(bm, bm.width, bm.height); bm.close && bm.close();
      out.push({ t: (L[i].lastModified - t0) / 1000, f: AMJ.parse(await read(pc.c)) });
    }
    return { frames: out, cut: files.length > MAXS ? files.length : 0 };
  };

  // ---------- the journey: everything the numbers say
  function med(a) { if (!a.length) return null; var s = a.slice().sort(function (x, y) { return x - y; }), m = s.length >> 1; return s.length % 2 ? s[m] : (s[m - 1] + s[m]) / 2; }
  function r1(v) { return v == null ? null : Math.round(v * 10) / 10; }
  AMJ.gRSRP = function (v) { return v == null ? null : v >= -80 ? [0, "excellent"] : v >= -90 ? [0, "good"] : v >= -100 ? [1, "fair"] : v >= -110 ? [2, "weak"] : [2, "very weak"]; };
  AMJ.gRSRQ = function (v) { return v == null ? null : v >= -10 ? [0, "good"] : v >= -15 ? [1, "busy cell"] : [2, "poor"]; };
  AMJ.gSINR = function (v) { return v == null ? null : v >= 20 ? [0, "excellent"] : v >= 13 ? [0, "good"] : v >= 3 ? [1, "fair"] : v >= 0 ? [2, "noisy"] : [2, "very noisy"]; };
  function fmtT(s) { s = Math.max(0, Math.round(s)); return Math.floor(s / 60) + ":" + ("0" + s % 60).slice(-2); }
  AMJ.fmtT = fmtT;
  var MCC = { "302": "Canada", "310": "USA", "311": "USA", "312": "USA", "313": "USA", "314": "USA", "315": "USA", "316": "USA", "234": "UK", "235": "UK", "470": "Bangladesh", "404": "India", "405": "India", "262": "Germany", "208": "France", "505": "Australia", "440": "Japan", "450": "South Korea", "460": "China" };
  var CA = { "302-220": "Telus", "302-221": "Telus", "302-610": "Bell", "302-640": "Bell", "302-720": "Rogers (Fido uses it)", "302-370": "Fido", "302-490": "Freedom", "302-500": "Videotron", "302-510": "Videotron", "302-780": "SaskTel", "302-880": "Shared Telus/Bell" };

  AMJ.analyze = function (frames, cut) {
    var ok = frames.filter(function (x) { return x.f; }), R = { n: frames.length, read: ok.length, cut: cut || 0 };
    if (!ok.length) return R;
    var rat = ok.some(function (x) { return x.f.lte.rsrp != null || x.f.lte.pci != null; }) ? "lte" : ok.some(function (x) { return x.f.nr.rsrp != null; }) ? "nr" : "lte";
    function ser(fn) { return ok.map(function (x) { var v = fn(x.f); return v == null ? null : { t: x.t, v: v }; }).filter(Boolean); }
    var P = function (k) { return function (f) { return f[rat][k]; }; };
    var rsrp = ser(P("rsrp")), rsrq = ser(P("rsrq")), rssi = ser(P("rssi")), pci = ser(P("pci")), nr = ser(function (f) { return f.nr.rsrp; });
    var ports = {}; ok.forEach(function (x) { var s = x.f[rat].sinr; if (s) Object.keys(s).forEach(function (k) { (ports[k] = ports[k] || []).push({ t: x.t, v: s[k] }); }); });
    var pk = Object.keys(ports).sort(), sinrBest = ok.map(function (x) { var s = x.f[rat].sinr; if (!s) return null; var v = Object.keys(s).map(function (k) { return s[k]; }); return v.length ? { t: x.t, v: Math.max.apply(null, v) } : null; }).filter(Boolean);
    var vals = function (s) { return s.map(function (p) { return p.v; }); };
    R.rat = rat; R.span = ok[ok.length - 1].t - ok[0].t;
    var last = function (k) { for (var i = ok.length - 1; i >= 0; i--) if (ok[i].f[k]) return ok[i].f[k]; return null; };
    R.carrier = last("carrier"); R.tech = last("tech"); R.plmn = last("plmn");
    if (R.plmn) { R.country = MCC[R.plmn.slice(0, 3)] || ""; R.net = CA[R.plmn] || ""; }
    R.rsrp = { s: rsrp, med: r1(med(vals(rsrp))), min: rsrp.length ? Math.min.apply(null, vals(rsrp)) : null, max: rsrp.length ? Math.max.apply(null, vals(rsrp)) : null };
    R.rsrq = { s: rsrq, med: r1(med(vals(rsrq))), min: rsrq.length ? Math.min.apply(null, vals(rsrq)) : null };
    R.rssi = { med: r1(med(vals(rssi))) };
    R.sinr = { s: sinrBest, med: r1(med(vals(sinrBest))), min: sinrBest.length ? Math.min.apply(null, vals(sinrBest)) : null, ports: pk.map(function (k) { return { k: k, med: r1(med(vals(ports[k]))) }; }) };
    // two antennas: a steady gap of 6 dB or more means one is covered - a hand, a case, or which way the phone faces
    if (pk.length >= 2) { var gaps = []; ok.forEach(function (x) { var s = x.f[rat].sinr; if (s && s[pk[0]] != null && s[pk[1]] != null) gaps.push(s[pk[1]] - s[pk[0]]); }); var gm = med(gaps); if (gm != null) R.ant = { gap: r1(Math.abs(gm)), weak: gm > 0 ? pk[0] : pk[1], n: gaps.length }; }
    // cells: how long on each, every change, and back-and-forth (A -> B -> A inside 15 s) that drops calls
    var cells = [], ch = [], pp = 0, prev = null;
    pci.forEach(function (p, i) { var c = cells.filter(function (z) { return z.pci === p.v; })[0]; if (!c) cells.push(c = { pci: p.v, t: 0, rsrp: [], sinr: [], first: p.t });
      var fc = ok.filter(function (x) { return x.t === p.t; })[0];
      // LTE cell id = tower (eNB) x 256 + side of the tower it points from
      if (fc && rat === "lte" && fc.f.lte.cid > 255 && c.enb == null) { c.enb = Math.floor(fc.f.lte.cid / 256); c.side = fc.f.lte.cid % 256; }
      var nx = pci[i + 1]; c.t += nx ? nx.t - p.t : 1;
      var fr = ok.filter(function (x) { return x.t === p.t; })[0]; if (fr) { if (fr.f[rat].rsrp != null) c.rsrp.push(fr.f[rat].rsrp); var s = fr.f[rat].sinr; if (s) c.sinr.push(Math.max.apply(null, Object.keys(s).map(function (k) { return s[k]; }))); }
      if (prev && prev.v !== p.v) { ch.push({ t: p.t, from: prev.v, to: p.v }); var b = ch[ch.length - 2]; if (b && b.from === p.v && p.t - b.t <= 15) pp++; } prev = p; });
    cells.forEach(function (c) { c.rsrp = r1(med(c.rsrp)); c.sinr = r1(med(c.sinr)); });
    R.cells = cells.sort(function (a, b) { return b.t - a.t; }); R.changes = ch; R.pingpong = pp;
    var tacs = []; ok.forEach(function (x) { if (x.f.tac != null && tacs.indexOf(x.f.tac) < 0) tacs.push(x.f.tac); }); R.tacs = tacs;
    // 5G: seen in some moments only = it came and went
    R.nr = { n: nr.length, of: ok.length, med: r1(med(vals(nr))) };
    var techs = []; ok.forEach(function (x) { if (x.f.tech && techs.indexOf(x.f.tech) < 0) techs.push(x.f.tech); }); R.techs = techs;
    var misc = {}; ["band", "arfcn", "bw", "cqi", "tx"].forEach(function (k) { var v = ok.map(function (x) { return x.f[rat][k]; }).filter(function (v) { return v != null; }); if (v.length) misc[k] = k === "cqi" || k === "tx" ? r1(med(v)) : v[v.length - 1]; }); R.misc = misc;
    // the worst moment: lowest quality first, then lowest strength
    var worst = null; ok.forEach(function (x) { var s = x.f[rat].sinr, sv = s ? Math.max.apply(null, Object.keys(s).map(function (k) { return s[k]; })) : null, score = (sv != null ? sv : 10) + ((x.f[rat].rsrq != null ? x.f[rat].rsrq : -10) + 10) + ((x.f[rat].rsrp != null ? x.f[rat].rsrp : -95) + 95) / 3;
      if (!worst || score < worst.score) worst = { score: score, t: x.t, rsrp: x.f[rat].rsrp, rsrq: x.f[rat].rsrq, sinr: sv, pci: x.f[rat].pci }; });
    R.worst = worst;
    // what it means: one headline, then every finding that applies
    var F = [], P2 = R.rsrp.med, Q = R.rsrq.med, S = R.sinr.med;
    if (P2 != null && P2 < -110) F.push([2, "Weak coverage", function () { return message("Strength sat around {strength} dBm - far from the tower or deep indoors. Near a window or outside it gets better; at home, Wi-Fi calling helps.", { strength: P2 }); }]);
    else if (P2 != null && P2 >= -100 && ((Q != null && Q <= -13) || (S != null && S < 3))) F.push([2, "Good bars, poor quality", function () { return message("Strength is fine ({strength} dBm) but {detail}. Distance isn't the problem, so moving a little rarely helps; another time of day often does.", { strength: P2, detail: Q != null && Q <= -13 ? message("quality was {value} dB - a busy cell sharing its air with many people", {value: Q}) : message("the signal was noisy (SINR {value} dB) - interference from nearby cells", {value: S}) }); }]);
    else if (P2 != null && P2 < -100) F.push([1, "Getting weak", function () { return message("Strength around {strength} dBm: still working, but the first to slow down when the cell gets busy.", { strength: P2 }); }]);
    // a busy stretch: quality down for a while even when the middle of the journey looks fine
    var bad = rsrq.filter(function (p) { return p.v <= -13; }), runs = 0;
    rsrq.forEach(function (p, i) { if (p.v <= -13 && (i === 0 || rsrq[i - 1].v > -13)) runs++; });
    var step = R.span / Math.max(1, ok.length - 1) || 1;
    if (bad.length >= 3 && bad.length / Math.max(1, rsrq.length) >= 0.15 && !(F[0] && F[0][1] === "Good bars, poor quality")) F.push([1, runs > 1 ? "Busy stretches" : "A busy stretch", function () { return message("Quality fell to {quality} dB for about {duration}{start} - a crowded cell; pages and video slow down there.", { quality: Math.min.apply(null, vals(bad)), duration: fmtT(Math.round(bad.length * step)), start: runs > 1 ? message(" in {count} stretches, the first at {time}", {count: runs, time: fmtT(bad[0].t)}) : message(", from {time}", {time: fmtT(bad[0].t)}) }); }]);
    if (R.ant && R.ant.gap >= 6) F.push([1, "One antenna blocked", function () { return message("The two antennas differ by {gap} dB - one is covered (your hand, a thick case, or the way the phone faces). Hold it differently or try landscape; the phone gets up to twice the speed with both.", { gap: R.ant.gap }); }]);
    if (ch.length && R.span > 0 && ch.length / (R.span / 60) > 4) F.push([pp ? 2 : 1, pp ? "Flipping between two cells" : "Lots of cell changes", function () { return message("{count} changes in {duration}{returns} - you're on the edge between towers. Calls drop and video stalls here; it's the place, not the phone.", { count: ch.length, duration: fmtT(R.span), returns: pp ? message(", {count} of them straight back again", {count: pp}) : "" }); }]);
    if (R.nr.n && R.nr.n < R.nr.of) F.push([1, "5G came and went", function () { return message("5G was there in {count} of {total} moments. Each switch is a short pause; if it bothers you, LTE only (Settings › Mobile Data › Voice & Data) is steadier here.", { count: R.nr.n, total: R.nr.of }); }]);
    if (tacs.length > 1) F.push([3, "Crossed an area border", function () { return message("The tracking area changed ({areas}) - the network re-registers you; a second or two of nothing is normal.", { areas: tacs.join(" → ") }); }]);
    if (!F.filter(function (z) { return z[0] >= 1; }).length && P2 != null) F.unshift([0, "Solid all the way", function () { return message("Strength {strength} dBm{detail} - this route is fine for calls, video and games.", { strength: P2, detail: (S != null ? message(", quality {value} dB", {value:S}) : "") + (ch.length ? message(ch.length === 1 ? ", 1 smooth cell change" : ", {count} smooth cell changes", {count:ch.length}) : "") }); }]);
    var rank = function (lv) { return lv === 3 ? -1 : lv; };   // problems first, plain notes last
    R.findings = F.sort(function (a, b) { return rank(b[0]) - rank(a[0]); });
    // what you can expect from the quality seen most of the time
    var e = S != null ? S : Q != null ? (Q >= -10 ? 15 : Q >= -15 ? 6 : 0) : null;
    R.expect = e == null ? null : e >= 13 ? [0, "4K video, fast downloads, smooth games"] : e >= 5 ? [0, "HD video and video calls fine; big downloads slower"] : e >= 0 ? [1, "Calls fine; video drops to SD; games may lag"] : [2, "Calls may break up; pages load slowly"];
    return R;
  };

  // ---------- a chart: strength (blue) and quality (orange) over the journey, dashes where the cell changed
  AMJ.chart = function (R, w) {
    w = w || 340; var h = 150, pad = 4, T = Math.max(1, R.span), L = [];
    var X = function (t) { return pad + (t - (R.rsrp.s[0] || R.sinr.s[0] || { t: 0 }).t) / T * (w - 2 * pad); };
    var line = function (s, lo, hi, col) { if (s.length < 2) return ""; return '<polyline fill="none" stroke="' + col + '" stroke-width="2.5" stroke-linejoin="round" points="' + s.map(function (p) { var y = h - 16 - (Math.max(lo, Math.min(hi, p.v)) - lo) / (hi - lo) * (h - 26); return X(p.t).toFixed(1) + "," + y.toFixed(1); }).join(" ") + '"/>'; };
    R.changes.forEach(function (c) { L.push('<line x1="' + X(c.t).toFixed(1) + '" y1="2" x2="' + X(c.t).toFixed(1) + '" y2="' + (h - 16) + '" stroke="var(--sub)" stroke-dasharray="3 4"/>'); });
    if (R.worst) L.push('<circle cx="' + X(R.worst.t).toFixed(1) + '" cy="' + (h - 10) + '" r="4" fill="var(--red)"/>');
    return '<svg role="img" aria-label="Strength and quality over the journey" viewBox="0 0 ' + w + ' ' + h + '" width="100%" style="display:block">' + '<line x1="0" y1="' + (h - 16) + '" x2="' + w + '" y2="' + (h - 16) + '" stroke="var(--line)"/>' + L.join("") +
      line(R.rsrp.s, -125, -65, "var(--blue)") + line(R.sinr.s.length ? R.sinr.s : R.rsrq.s, R.sinr.s.length ? -5 : -20, R.sinr.s.length ? 30 : -3, "#E89B2C") + '</svg>';
  };

  // ---------- the text to share (no phone number, no IMEI - they were never read in)
  function message(key, values) {
    var locale = window.AdeeboMeterLocale;
    if (locale) return values ? locale.format(key, values) : locale.t(key);
    return values ? key.replace(/\{(\w+)\}/g, function (_, name) { return values[name]; }) : key;
  }
  AMJ.findingText = function (finding) {
    return typeof finding[2] === "function" ? finding[2]() : finding[2];
  };
  AMJ.report = function (R) {
    var L = [message("AdeeboMeter - signal journey"), (R.carrier || "") + (R.net ? " · " + R.net : "") + " · " + (R.rat === "nr" ? "5G" : "LTE") + " · " + fmtT(R.span) + " · " + message("{count} moments", {count:R.read})];
    R.findings.forEach(function (f) { L.push("- " + message(f[1]) + ": " + AMJ.findingText(f)); });
    L.push(message("Strength (RSRP): {median} dBm ({min} to {max})", {median:R.rsrp.med,min:R.rsrp.min,max:R.rsrp.max}));
    if (R.rsrq.med != null) L.push(message("Quality (RSRQ): {value} dB", {value:R.rsrq.med}));
    if (R.sinr.med != null) L.push("SINR: " + R.sinr.ports.map(function (p, i) { return message("antenna {index}: {value} dB", {index:i+1,value:p.med}); }).join(", "));
    L.push(message("Cells: {cells} · {changes}", {cells:R.cells.map(function (c) { return c.pci + (c.enb != null ? message(" - tower {tower}, side {side}", {tower:c.enb,side:c.side}) : "") + " (" + fmtT(c.t) + ")"; }).join(", "), changes:message("{count} changes", {count:R.changes.length})}));
    return L.join("\n");
  };
})();
