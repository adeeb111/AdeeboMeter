/* AdeeboMeter calculators - the same maths as the Windows app (src/Checks/Calc*.cs), for the phone and the web.
   Pure arithmetic: nothing is sent anywhere. Each calculator returns rows: [level, name, value, advice]
   (level 0 good, 1 caution, 2 problem, 3 info). tools/calccheck compares every one with the PC's own code. */
(function () {
  "use strict";
  function R(lv, name, val, adv) { return [String(lv), name, val == null ? "" : String(val), adv || ""]; }
  function I(n) { n = Math.trunc(n); return n.toLocaleString("en-US"); }
  // .NET-style number formats used by the PC: "0.##", "0.#", "0.0", "0", "N2", "#,0" and friends
  function F(d, fmt) {
    if (!isFinite(d)) return d > 0 ? "∞" : d < 0 ? "-∞" : "NaN";
    var m = /^N(\d)$/.exec(fmt); if (m) { var k = +m[1]; return rnd(d, k).toLocaleString("en-US", { minimumFractionDigits: k, maximumFractionDigits: k }); }
    var grp = fmt.indexOf(",") >= 0, dot = fmt.indexOf("."), dec = dot < 0 ? "" : fmt.slice(dot + 1), req = (dec.match(/0/g) || []).length, max = dec.length;
    var v = rnd(d, max), s = Math.abs(v).toFixed(max);
    if (max > req) { s = s.replace(new RegExp("0{1," + (max - req) + "}$"), ""); if (s.slice(-1) === ".") s = s.slice(0, -1); }
    if (grp) { var p = s.split("."); p[0] = (+p[0]).toLocaleString("en-US"); s = p.join("."); }
    return (v < 0 && +s !== 0 ? "-" : "") + s;
  }
  function rnd(d, k) { var f = Math.pow(10, k), x = +(Math.abs(d) * f).toPrecision(15), r = Math.round(x); return (d < 0 ? -1 : 1) * r / f; }   // half away from zero, like .NET
  function blank(s) { return s == null || String(s).trim() === ""; }
  var U32 = 4294967296;
  function V4(t) { var m = /^([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})$/.exec(String(t || "").trim()); if (!m) return null; var v = 0; for (var i = 1; i <= 4; i++) { var o = +m[i]; if (o > 255) return null; v = v * 256 + o; } return v; }
  function U(v) { v = ((v % U32) + U32) % U32; return [Math.floor(v / 16777216) % 256, Math.floor(v / 65536) % 256, Math.floor(v / 256) % 256, v % 256].join("."); }
  function maskOf(p) { return p === 0 ? 0 : (U32 - Math.pow(2, 32 - p)); }

  // ================================================================== packet stack
  function L(k, n, kind, b, note, ap) { return { Key: k, Name: n, Kind: kind, Bytes: b, Note: note, Approx: !!ap }; }
  var Known = [L("tcp", "TCP", "p", 20, "20 bytes without options; +12 with timestamps (most Linux and macOS hosts)"), L("udp", "UDP", "p", 8, ""), L("icmp", "ICMP echo", "p", 8, ""),
    L("tls", "TLS 1.3 record", "r", 22, "5-byte header + 1 content type + 16-byte tag per record of up to 16 KB - spread over about 11 packets, so it doesn't change the MSS or the MTU", true),
    L("quic", "QUIC short header", "p", 25, "1 + connection ID (often 8) + packet number (1-4) + 16-byte tag; rides on UDP", true), L("rtp", "RTP", "p", 12, "voice / video; +4 per contributing source"), L("dns", "DNS header", "p", 12, ""),
    L("ipv4", "IPv4", "i", 20, "20 bytes without options"), L("ipv6", "IPv6", "i", 40, "40 bytes; extension headers add more"),
    L("wireguard", "WireGuard (outer IPv4)", "t", 60, "20 IPv4 + 8 UDP + 16 header + 16 tag"), L("wireguard6", "WireGuard (outer IPv6)", "t", 80, "40 IPv6 + 8 UDP + 16 header + 16 tag"),
    L("ipsec", "IPsec ESP tunnel, AES-GCM", "t", 58, "20 outer IPv4 + 8 ESP + 8 IV + 2 trailer + 16 ICV + up to 3 padding (worst case 4-byte alignment)", true),
    L("ipsec-natt", "IPsec ESP over NAT (UDP 4500)", "t", 66, "ESP tunnel + 8 UDP, used when there's NAT on the way", true), L("gre", "GRE (outer IPv4)", "t", 24, "20 outer IPv4 + 4 GRE; +4 with a key"),
    L("vxlan", "VXLAN (outer IPv4)", "t", 50, "20 IPv4 + 8 UDP + 8 VXLAN + 14 inner Ethernet"), L("gtpu", "GTP-U (mobile user plane)", "t", 36, "20 IPv4 + 8 UDP + 8 GTP; +4 with options"),
    L("sslvpn", "SSL VPN over TLS (typical)", "t", 82, "varies by product - 20 IPv4 + 20 TCP or 8 UDP/DTLS + record and tag; check the client's own MTU", true),
    L("pppoe", "PPPoE", "l", 8, "6 PPPoE + 2 PPP - DSL and many fibre links; leaves 1492 for IP"), L("vlan", "802.1Q VLAN tag", "f", 4, "outside the IP MTU; the switch must allow 1522-byte frames"),
    L("qinq", "Q-in-Q (two tags)", "f", 8, "outside the IP MTU"), L("mpls", "MPLS label", "f", 4, "per label - outside the customer's IP MTU but inside the core link's; raise the core MTU by 4 per label"),
    L("ethernet", "Ethernet", "f", 18, "14 header + 4 FCS; on the wire also 8 preamble + 12 gap"), L("wifi", "Wi-Fi (802.11 data)", "f", 36, "about 24-30 header + 8 LLC/SNAP + 4 FCS; QoS and security add more", true)];
  var Alias = { ip: "ipv4", v4: "ipv4", v6: "ipv6", wg: "wireguard", esp: "ipsec", "nat-t": "ipsec-natt", natt: "ipsec-natt", eth: "ethernet", "802.1q": "vlan", dot1q: "vlan", label: "mpls", gtp: "gtpu", https: "tls", ping: "icmp", wlan: "wifi", ssl: "sslvpn" };
  var Presets = [["web", "tls tcp ipv4 ethernet"], ["game", "udp ipv4 ethernet"], ["call", "rtp udp ipv4 ethernet"], ["dsl", "tcp ipv4 pppoe ethernet"], ["vpn", "tcp ipv4 wireguard ethernet"], ["office vpn", "tcp ipv4 ipsec-natt ethernet"]];
  function parseStack(text) {
    var t = String(text || "").trim().toLowerCase(); Presets.forEach(function (p) { if (t === p[0]) t = p[1]; }); if (t === "") t = "tcp ipv4 ethernet";
    var list = [], words = t.split(/[ ,>\/+|]+/).filter(Boolean);
    for (var i = 0; i < words.length; i++) {
      var w = words[i]; if (w === "over" || w === "on" || w === "in") continue;
      var m = /^x([0-9])$/.exec(w); if (m && list.length) { for (var k = 1; k < +m[1] && k < 8; k++) list.push(list[list.length - 1]); continue; }
      if (Alias[w]) w = Alias[w];
      var l = Known.filter(function (x) { return x.Key === w; })[0]; if (!l) return { list: list, err: w }; list.push(l);
    }
    return { list: list, err: null };
  }
  function intText(t) { var s = String(t).trim().replace(/,/g, ""); return /^[0-9]+$/.test(s) ? parseInt(s, 10) : NaN; }
  function sum(st, k) { return st.filter(function (x) { return x.Kind === k; }).reduce(function (a, x) { return a + x.Bytes; }, 0); }
  function Stack(layers, mtuText, payloadText) {
    var rows = [], p = parseStack(layers), st = p.list;
    if (p.err != null) { rows.push(R(2, "Layers", "\"" + p.err + "\" isn't a layer I know - try: " + Known.map(function (k) { return k.Key; }).join(", ") + ", or a preset: " + Presets.map(function (x) { return x[0]; }).join(", "), "")); return rows; }
    var mtu = 1500; if (!blank(mtuText)) { mtu = intText(mtuText); if (isNaN(mtu) || mtu < 576 || mtu > 9216) { rows.push(R(2, "MTU", "type a link MTU between 576 and 9216 (1500 = normal Ethernet)", "")); return rows; } }
    var payload = -1; if (!blank(payloadText)) { payload = intText(payloadText); if (isNaN(payload)) { rows.push(R(2, "Payload", "type the data size in bytes, e.g. 1400", "")); return rows; } }
    var ips = st.filter(function (x) { return x.Kind === "i"; }).length; if (ips > 1) rows.push(R(1, "Layers", ips + " IP layers - both are counted inside the MTU. A tunnel's outer IP belongs to the tunnel layer (gre, ipsec, vxlan...), which already carries it.", "")); if (ips === 0) { rows.push(R(2, "Layers", "add an IP layer (ipv4 or ipv6) - the MTU is counted against it", "")); return rows; }
    var pay = sum(st, "p"), ip = sum(st, "i"), tun = sum(st, "t"), shim = sum(st, "l"), frame = sum(st, "f");
    var ap = st.some(function (x) { return x.Approx && x.Kind !== "r"; }) ? "~" : "", innerMtu = mtu - shim - tun, maxData = innerMtu - ip - pay, eth = st.some(function (x) { return x.Key === "ethernet"; });
    rows.push(R(3, "Stack", st.map(function (x) { return x.Name; }).join(" > "), "Top is what the app sends; bottom is what goes on the wire."));
    st.forEach(function (x) { rows.push(R(3, "  " + x.Name, (x.Approx ? "~" : "") + x.Bytes + " bytes" + (x.Kind === "f" ? " (outside the MTU)" : x.Kind === "r" ? " per record, not per packet" : ""), x.Note)); });
    if (maxData <= 0) { rows.push(R(2, "Fits?", "the headers alone are bigger than an MTU of " + mtu, "")); return rows; }
    var tcp = st.some(function (x) { return x.Key === "tcp"; });
    rows.push(R(0, tcp ? "Biggest segment (MSS)" : "Biggest data per packet", ap + I(maxData) + " bytes", tcp ? "What TCP should advertise so nothing splits: link MTU " + mtu + " - " + (shim + tun > 0 ? (shim + tun) + " tunnel/PPPoE - " : "") + ip + " IP - " + pay + " above." : ""));
    if (tun + shim > 0) rows.push(R(1, "Set the inner MTU to", ap + I(innerMtu), "The tunnel (or PPPoE) eats " + ap + (tun + shim) + " bytes of the " + mtu + " - set the VPN adapter's MTU (or the router's MSS clamp) to this, or big packets split or vanish."));
    var wire = mtu + frame, full = maxData + pay + ip + tun + shim + frame, eff = 100 * maxData / Math.max(1, full + (eth ? 20 : 0));
    rows.push(R(3, "Frame on the wire", ap + I(wire) + " bytes" + (eth ? " (+20 preamble and gap)" : ""), ""));
    rows.push(R(3, "Efficiency", ap + F(eff, "0.0") + "% of the line carries your data at full size", "Small packets carry far less: a 100-byte game update is mostly headers."));
    if (payload >= 0) {
      if (payload <= maxData) rows.push(R(0, I(payload) + " bytes of data", "fits in one packet (" + ap + I(payload + pay + ip + tun + shim) + " of " + mtu + ")", ""));
      else {
        var ii = st.filter(function (x) { return x.Kind === "i"; }), innerIp = ii[ii.length - 1], fragHdr = innerIp && innerIp.Key === "ipv6" ? 8 : 0, fragData = Math.floor((innerMtu - ip - fragHdr) / 8) * 8;
        var n = tcp ? Math.ceil(payload / maxData) : Math.ceil((payload + pay) / Math.max(8, fragData));
        if (tcp) rows.push(R(3, I(payload) + " bytes of data", "TCP sends it as " + n + " segments - normal", ""));
        else rows.push(R(1, I(payload) + " bytes of data", "too big for one packet: IP splits it into " + n + " fragments, or drops it if Don't Fragment is set (IPv6 never splits on the way)", "Fragments get lost or blocked by firewalls more often - keep UDP data under " + maxData + "."));
      }
    }
    return rows;
  }

  // ================================================================== VLSM planner
  function Vlsm(block, sizes) {
    var rows = [], b = String(block || "").trim(), slash = b.indexOf("/"), ip, pre;
    if (slash < 0 || (ip = V4(b.slice(0, slash))) == null || !/^[0-9]+$/.test(b.slice(slash + 1)) || (pre = +b.slice(slash + 1)) > 30) { rows.push(R(2, "Network", "type an IPv4 block, e.g. 10.0.0.0/22", "")); return rows; }
    var need = [], ws = String(sizes || "").split(/[, ;]+/).filter(Boolean);
    for (var i = 0; i < ws.length; i++) { var w = ws[i], n = /^[0-9]+$/.test(w) ? +w : NaN; if (isNaN(n) || n < 1 || n > 16777214) { rows.push(R(2, "Sizes", "\"" + w + "\" isn't a host count - type e.g. 200, 60, 12, 2", "")); return rows; } need.push(n); }
    if (!need.length) { rows.push(R(2, "Sizes", "type how many hosts each subnet needs, e.g. 200, 60, 12, 2", "")); return rows; }
    if (need.length > 64) { rows.push(R(2, "Sizes", "64 subnets at most", "")); return rows; }
    var total = Math.pow(2, 32 - pre), start = ip - ip % total, end = start + total;
    rows.push(R(3, "Block", U(start) + "/" + pre + "  (" + I(total) + " addresses)", "Biggest first, so every subnet lands on its own boundary."));
    var order = need.map(function (n, i) { return { n: n, i: i }; }).sort(function (a, b) { return b.n - a.n || a.i - b.i; }), at = start, used = 0, full = false;
    order.forEach(function (x) {
      var p = 32; while (p > 0 && Math.pow(2, 32 - p) - (p <= 30 ? 2 : 0) < x.n) p--;
      if (x.n <= 2) p = 30;
      var size = Math.pow(2, 32 - p), a = at % size !== 0 ? at + size - at % size : at;
      if (a + size > end) { rows.push(R(2, "Subnet " + (x.i + 1) + " (" + I(x.n) + " hosts)", "doesn't fit - not enough room left in the block", "")); full = true; return; }
      at = a; var net = at, bc = at + size - 1, range = p <= 30 ? U(net + 1) + " - " + U(bc - 1) : U(net) + " - " + U(bc), usable = p <= 30 ? size - 2 : size;
      rows.push(R(0, "Subnet " + (x.i + 1) + " (" + I(x.n) + " hosts)", U(net) + "/" + p + "  ·  " + range + "  ·  gateway " + U(net + 1) + (p <= 30 ? "  ·  broadcast " + U(bc) : ""), I(usable) + " usable, " + I(usable - x.n) + " spare · mask " + U(maskOf(p))));
      at += size; used += size;
    });
    rows.push(R(full ? 2 : 3, "Used", I(used) + " of " + I(total) + " addresses" + (at < end && !full ? " · free from " + U(at) : ""), full ? "Use a bigger block (a shorter prefix) or fewer hosts." : ""));
    return rows;
  }

  // ================================================================== bandwidth and transfer time
  function Parse2(t) {
    var m = /^\s*([0-9]{1,3}(?:,[0-9]{3})+(?:\.[0-9]+)?|[0-9]+(?:[.,][0-9]+)?)\s*([a-zA-Z\/µ]*)\s*$/.exec(t || ""); if (!m) return { v: NaN, u: "" };
    var n = m[1], u = m[2]; if (/^[0-9]{1,3}\.[0-9]{3}$/.test(n)) return { v: NaN, u: u };
    n = /^[0-9]{1,3}(,[0-9]{3})+(\.[0-9]+)?$/.test(n) ? n.replace(/,/g, "") : n.replace(",", ".");
    var v = parseFloat(n); return { v: isFinite(v) ? v : NaN, u: u };
  }
  function Size(t) {
    if (blank(t)) return NaN; var p = Parse2(t), v = p.v, u = p.u; if (isNaN(v)) return v;
    if (u.length === 2 && u[1] === "b" && u[0] === u[0].toUpperCase() && u[0] !== u[0].toLowerCase()) return NaN;
    switch (u.toLowerCase()) { case "": case "b": case "bytes": return v; case "kb": return v * 1e3; case "mb": return v * 1e6; case "gb": return v * 1e9; case "tb": return v * 1e12; case "kib": return v * 1024; case "mib": return v * 1048576; case "gib": return v * 1073741824; case "tib": return v * 1099511627776; default: return NaN; }
  }
  function Speed(t) {
    if (blank(t)) return NaN; var p = Parse2(t), v = p.v; if (isNaN(v)) return v; var k = p.u.replace("/s", "ps"); if (k === "") return v * 1e6;
    var T = { bps: 1, kbps: 1e3, Kbps: 1e3, kbit: 1e3, kbitps: 1e3, Kbit: 1e3, Kbitps: 1e3, Tbps: 1e12, tbps: 1e12, Tbit: 1e12, Tbitps: 1e12, Mb: 1e6, Gb: 1e9, Mbps: 1e6, mbps: 1e6, Mbit: 1e6, mbit: 1e6, Mbitps: 1e6, mbitps: 1e6, Gbps: 1e9, gbps: 1e9, Gbit: 1e9, gbit: 1e9, Gbitps: 1e9, gbitps: 1e9, Bps: 8, KBps: 8e3, kBps: 8e3, MBps: 8e6, GBps: 8e9, KiBps: 8 * 1024, MiBps: 8 * 1048576, GiBps: 8 * 1073741824 };
    return Object.prototype.hasOwnProperty.call(T, k) ? v * T[k] : NaN;
  }
  function Time(t) {
    if (blank(t)) return NaN; var p = Parse2(t), v = p.v; if (isNaN(v)) return v;
    switch (p.u.toLowerCase()) { case "": case "s": case "sec": case "secs": case "second": case "seconds": return v; case "ms": return v / 1000; case "m": case "min": case "mins": case "minute": case "minutes": return v * 60; case "h": case "hr": case "hrs": case "hour": case "hours": return v * 3600; case "d": case "day": case "days": return v * 86400; default: return NaN; }
  }
  function Bits(b) { return b >= 1e9 ? F(b / 1e9, "0.##") + " Gbps" : b >= 1e6 ? F(b / 1e6, "0.##") + " Mbps" : b >= 1e3 ? F(b / 1e3, "0.##") + " kbps" : F(b, "0") + " bps"; }
  function ByteSize(b) { return b >= 1e12 ? F(b / 1e12, "0.##") + " TB" : b >= 1e9 ? F(b / 1e9, "0.##") + " GB" : b >= 1e6 ? F(b / 1e6, "0.##") + " MB" : b >= 1e3 ? F(b / 1e3, "0.##") + " kB" : F(b, "0") + " bytes"; }
  function ByteRate(b) { return ByteSize(b / 8) + "/s"; }
  function Dur(s) { if (s < 0.001) return F(s * 1e6, "0.#") + " µs"; if (s < 1) return F(s * 1000, "0.#") + " ms"; if (s < 90) return F(s, "0.#") + " s"; if (s < 5400) return F(s / 60, "0.#") + " min"; if (s < 172800) return F(s / 3600, "0.#") + " h"; return F(s / 86400, "0.#") + " days"; }
  function Bandwidth(sizeText, speedText, timeText) {
    var rows = [], bytes = Size(sizeText), bps = Speed(speedText), secs = Time(timeText);
    if (isNaN(bytes) && !blank(sizeText)) { rows.push(R(2, "Size", "type e.g. 4.7 GB, 300 MB or 1.2 TB (in bytes - \"Gb\" is gigabits; and write 1000 or 1,000, not 1.000)", "")); return rows; }
    if (isNaN(bps) && !blank(speedText)) { rows.push(R(2, "Speed", "type e.g. 100 Mbps, 1 Gbps or 12 MB/s", "")); return rows; }
    if (isNaN(secs) && !blank(timeText)) { rows.push(R(2, "Time", "type e.g. 90 s, 10 min or 2 h", "")); return rows; }
    var have = (isNaN(bytes) ? 0 : 1) + (isNaN(bps) ? 0 : 1) + (isNaN(secs) ? 0 : 1);
    if (!have) { rows.push(R(2, "Input", "type a size and a speed (how long?), a size and a time (how fast?), or just a speed (conversions)", "")); return rows; }
    if (bps === 0 || secs === 0) { rows.push(R(2, bps === 0 ? "Speed" : "Time", "has to be more than 0", "")); return rows; }
    if (have === 1 && isNaN(bps)) { rows.push(R(2, isNaN(bytes) ? "Time" : "Size", "needs one more value: a " + (isNaN(bytes) ? "size (how much moves in that time at a speed?) or a speed" : "speed (how long does it take?) or a time (how fast must it go?)"), "")); return rows; }
    if (!isNaN(bytes) && !isNaN(bps) && bps > 0) {
      var t = bytes * 8 / bps; rows.push(R(0, "Time", Dur(t) + " at the full " + Bits(bps), ""));
      rows.push(R(3, "Realistic", Dur(t / 0.94) + " - TCP and IP headers take about 5-6%; Wi-Fi usually gets half to two thirds of its link rate", ""));
      if (!isNaN(secs)) { var off = Math.abs(secs - t) > Math.max(1, t * 0.1); rows.push(R(off ? 1 : 3, "Your time", Dur(secs) + (off ? " doesn't fit the size and speed (" + Dur(t) + ") - the size and speed were used" : " fits the size and speed"), "")); }
    } else if (!isNaN(bytes) && !isNaN(secs) && secs > 0) { bps = bytes * 8 / secs; rows.push(R(0, "Speed needed", Bits(bps) + "  (" + ByteRate(bps) + ")", "Plus about 6% for headers: " + Bits(bps / 0.94) + " on the line.")); }
    else if (!isNaN(bps) && !isNaN(secs)) rows.push(R(0, "Moves", ByteSize(bps * secs / 8) + " in " + Dur(secs), ""));
    if (!isNaN(bps) && bps > 0) {
      rows.push(R(3, "Same speed", Bits(bps) + " = " + ByteRate(bps) + " = " + ByteSize(bps / 8 * 3600) + " an hour", "Network speeds are in bits; file sizes in bytes (8 bits). Sizes here are decimal (1 MB = 1,000,000 bytes), like ISPs and Windows' network pages."));
      rows.push(R(3, "Full-size packets", I(Math.floor(bps / 8 / 1538)) + " a second (1500-byte Ethernet)  ·  " + F(1538 * 8 / bps * 1e6, "0.##") + " µs each on the wire", ""));
    }
    return rows;
  }

  // ================================================================== decimal / hex / binary (BigInt: up to 64 bits, like the PC's ulong)
  function Dscp(d) { return { 46: " (EF - voice)", 34: " (AF41 - video)", 26: " (AF31)", 18: " (AF21)", 10: " (AF11)", 48: " (CS6 - network control)", 8: " (CS1 - low priority)", 0: " (best effort)" }[d] || ""; }
  var MAXU = (BigInt(1) << BigInt(64)) - BigInt(1);
  function big(s, radix) { var v = BigInt(0), R2 = BigInt(radix); s = s.toLowerCase(); for (var i = 0; i < s.length; i++) { v = v * R2 + BigInt(parseInt(s[i], radix)); if (v > MAXU) return null; } return v; }
  function ParseNum(t) {
    if (t === "") return null; var ip = V4(t); if (ip != null) return { v: BigInt(ip), from: "IPv4 address" };
    if (/^([0-9a-fA-F]{2}[:-]){5}[0-9a-fA-F]{2}$/.test(t) || /^[0-9a-fA-F]{4}\.[0-9a-fA-F]{4}\.[0-9a-fA-F]{4}$/.test(t)) return { v: big(t.replace(/[^0-9a-fA-F]/g, ""), 16), from: "MAC address" };
    var s = t.replace(/ /g, ""), v;
    if (/^0x/i.test(s)) { v = /^[0-9a-f]+$/i.test(s.slice(2)) ? big(s.slice(2), 16) : null; return v == null ? null : { v: v, from: "hex" }; }
    if (/h$/i.test(s) && s.length > 1) { v = /^[0-9a-f]+$/i.test(s.slice(0, -1)) ? big(s.slice(0, -1), 16) : null; return v == null ? null : { v: v, from: "hex" }; }
    if (/^0b/i.test(s) || /^[01]+[bB]$/.test(s)) { var bits = /^0b/i.test(s) ? s.slice(2) : s.slice(0, -1); if (!bits.length || bits.length > 64 || !/^[01]+$/.test(bits)) return null; return { v: big(bits, 2), from: "binary" }; }
    var d = s.replace(/,/g, ""); if (/^[0-9]+$/.test(d)) { v = big(d, 10); return v == null ? null : { v: v, from: "decimal" }; }
    if (/^[0-9a-fA-F]+$/.test(s) && s.length <= 16) return { v: big(s, 16), from: "hex (it has letters)" };
    return null;
  }
  function Number_(text) {
    var rows = [], t = String(text || "").trim().replace(/_/g, ""), p = ParseNum(t);
    if (!p) { rows.push(R(2, "Input", "type a number: 200, 0xC8, 0b11001000, C8h, an IPv4 address (10.1.2.3) or a MAC (00:11:22:aa:bb:cc)", "")); return rows; }
    var v = p.v; rows.push(R(3, "Read as", p.from, "")); rows.push(R(0, "Decimal", v.toString(), "")); rows.push(R(0, "Hex", "0x" + v.toString(16).toUpperCase(), ""));
    var bin = v.toString(2), w = bin.length <= 8 ? 8 : bin.length <= 16 ? 16 : bin.length <= 32 ? 32 : bin.length <= 48 ? 48 : 64; bin = bin.padStart(w, "0");
    var parts = []; for (var i = 0; i < w / 8; i++) parts.push(bin.substr(i * 8, 8));
    rows.push(R(0, "Binary", parts.join(" "), w + " bits shown; " + Math.max(1, v.toString(2).replace(/^0+/, "").length) + " needed"));
    if (v <= BigInt(4294967295)) {
      var u = Number(v); rows.push(R(3, "As an IPv4 address", U(u), ""));
      var ones = 0, contig = true; for (var j = 31; j >= 0; j--) { if (Math.floor(u / Math.pow(2, j)) % 2 === 1) { if (ones !== 31 - j) contig = false; ones++; } }
      if (contig && ones > 0 && p.from.indexOf("IPv4") === 0) rows.push(R(3, "As a mask", "/" + ones + "  ·  wildcard " + U(4294967295 - u), ""));
      if (u <= 65535) rows.push(R(3, "As a port / VLAN / ID", u <= 4094 && u >= 1 ? "valid VLAN ID and port number" : "valid port number", ""));
      if (u <= 63) rows.push(R(3, "As DSCP", "DSCP " + u + "  ·  ToS byte 0x" + ("0" + (u * 4).toString(16).toUpperCase()).slice(-2) + Dscp(u), ""));
    }
    if ((v <= BigInt("0xFFFFFFFFFFFF") && v > BigInt(4294967295)) || p.from.indexOf("MAC") === 0) { var m = []; for (var k = 0; k < 6; k++) m.push(("0" + ((v >> BigInt(40 - k * 8)) & BigInt(255)).toString(16)).slice(-2)); rows.push(R(3, "As a MAC", m.join(":"), "")); }
    return rows;
  }


  // ================================================================== Calc2: masks, IPv6, links, radio, storage, streaming, voice
  function anyNums(t) {   // numbers not glued to a digit, dot or comma before them: "20-70 ms" is 20 and 70, "-80 dBm" is -80
    t = String(t || ""); var out = [], re = /-?[0-9]+(?:[.,][0-9]+)?/y;
    for (var i = 0; i < t.length;) { if (i > 0 && /[0-9.,]/.test(t[i - 1])) { i++; continue; } re.lastIndex = i; var m = re.exec(t); if (m) { var v = parseFloat(m[0].replace(",", ".")); if (!isNaN(v)) out.push(v); i = re.lastIndex; } else i++; }
    return out;
  }
  function First(t) { var a = anyNums(t); return a.length ? a[0] : NaN; }
  function Need(box, what) { return [R(2, box, what, "")]; }
  function Pct(v) { return F(v, v < 1 ? "0.###" : "0.##") + "%"; }
  function Dotted(v) { return U(v); }
  function Wildcard(text) {
    var t = String(text || "").trim(); if (t === "") return Need("Mask", "type a mask (255.255.252.0), a prefix (/22) or a wildcard (0.0.3.255)");
    var mask, prefix;
    if (/^\/?\d{1,2}$/.test(t)) { prefix = parseInt(t.replace(/^\//, ""), 10); if (prefix > 32) return Need("Mask", "a prefix is 0 to 32"); mask = maskOf(prefix); }
    else {
      var v = V4(t); if (v == null) return Need("Mask", "type it like 255.255.252.0, /22 or 0.0.3.255");
      if (v < 2147483648 && v !== 0) v = 4294967295 - v;
      var inv = 4294967295 - v; if (((inv + 1) % U32 !== 0) && (inv & (inv + 1)) !== 0) return [R(1, "Not contiguous", t + " has gaps - fine in a Cisco ACL wildcard (it matches odd patterns), but it isn't a subnet mask", "Each 0 bit in a wildcard must match; each 1 bit is 'don't care'.")];
      mask = v; prefix = 0; for (var m2 = mask; m2 !== 0; m2 = (m2 * 2) % U32) prefix++;
    }
    var wild = 4294967295 - mask, hosts = prefix >= 31 ? (prefix === 31 ? 2 : 1) : Math.pow(2, 32 - prefix) - 2;
    return [R(0, "Mask", Dotted(mask), ""), R(0, "Prefix", "/" + prefix, ""), R(0, "Wildcard", Dotted(wild), "What an ACL or OSPF network line wants: the mask turned inside out."),
      R(3, "Addresses", I(Math.pow(2, 32 - prefix)) + " in the block · " + I(hosts) + " usable" + (prefix === 31 ? " (point-to-point, RFC 3021)" : ""), ""),
      R(3, "In an ACL", "permit ip 10.0.0.0 " + Dotted(wild) + " any  - matches 10.0.0.0/" + prefix, "Swap 10.0.0.0 for your block's first address.")];
  }
  // IPv6 as 16 bytes, written the usual short way (lowercase, the longest run of zero groups as ::)
  function V6(t) {
    t = String(t || "").trim().toLowerCase(); if (!/^[0-9a-f:.]+$/.test(t) || t.indexOf(":") < 0) return null;
    var tail = null; var m4 = /:(\d+\.\d+\.\d+\.\d+)$/.exec(t); if (m4) { tail = V4(m4[1]); if (tail == null) return null; t = t.slice(0, -m4[1].length) + "0:0"; }
    var dbl = t.split("::"); if (dbl.length > 2) return null;
    var head = dbl[0] ? dbl[0].split(":") : [], back = dbl.length === 2 ? (dbl[1] ? dbl[1].split(":") : []) : [];
    if (dbl.length === 1 && head.length !== 8) return null; if (dbl.length === 2 && head.length + back.length > 7) return null;
    var g = head.concat(dbl.length === 2 ? new Array(8 - head.length - back.length).fill("0") : [], back), b = [];
    for (var i = 0; i < 8; i++) { if (!/^[0-9a-f]{1,4}$/.test(g[i])) return null; var x = parseInt(g[i], 16); b.push(x >> 8, x & 255); }
    if (tail != null) { b[12] = Math.floor(tail / 16777216) % 256; b[13] = Math.floor(tail / 65536) % 256; b[14] = Math.floor(tail / 256) % 256; b[15] = tail % 256; }
    return b;
  }
  function V6s(b) {
    if (b.slice(0, 10).every(function (x) { return x === 0; }) && b[10] === 255 && b[11] === 255) return "::ffff:" + b.slice(12).join(".");
    var g = []; for (var i = 0; i < 16; i += 2) g.push(b[i] * 256 + b[i + 1]);
    var best = -1, bl = 0; for (i = 0; i < 8;) { if (g[i] === 0) { var j = i; while (j < 8 && g[j] === 0) j++; if (j - i > bl) { best = i; bl = j - i; } i = j; } else i++; }
    var h = g.map(function (x) { return x.toString(16); });
    if (bl < 2) return h.join(":");
    return h.slice(0, best).join(":") + "::" + h.slice(best + bl).join(":");
  }
  function V6Split(prefixText, intoText) {
    var m = /^([0-9a-fA-F:]+)\/(\d{1,3})$/.exec(String(prefixText || "").trim()), ip = m ? V6(m[1]) : null;
    if (!m || !ip) return Need("Prefix", "type it like 2001:db8:abcd::/48");
    var p = +m[2]; if (p > 128) return Need("Prefix", "a prefix is 0 to 128");
    var it = String(intoText || "").trim(), q;
    if (it[0] === "/") { if (!/^[+-]?\d+$/.test(it.slice(1))) return Need("Split into", "type /56, /64 or a count like 256"); q = parseInt(it.slice(1), 10); }
    else { var n = First(it); if (isNaN(n) || n < 2) q = p <= 56 ? 64 : Math.min(128, p + 8); else q = p + Math.ceil(Math.log(n) / Math.log(2) - 1e-9); }
    if (q <= p || q > 128) return Need("Split into", "has to be longer than /" + p + " and at most /128");
    var extra = q - p, b = ip.slice(); for (var bit = p; bit < 128; bit++) b[bit >> 3] &= ~(0x80 >> (bit % 8)) & 255;
    var rows = [R(0, "Pieces", (extra >= 63 ? "2^" + extra : I(Math.pow(2, extra))) + " × /" + q, "")];
    if (q <= 64) rows.push(R(3, "Each one holds", (64 - q >= 63 ? "2^" + (64 - q) : I(Math.pow(2, 64 - q))) + " LAN-sized /64" + (q === 64 ? "" : "s"), "A /64 is one LAN (SLAAC needs exactly /64)."));
    else rows.push(R(1, "Smaller than /64", "SLAAC and most LANs need a /64; /" + q + " fits point-to-point links (/127) or loopbacks (/128)", ""));
    var show = Math.min(4, Math.pow(2, Math.min(extra, 20)));
    for (var i = 0; i < show; i++) { var c = b.slice(); for (var k = 0, n2 = i; k < 62 && n2 !== 0; k++, n2 = Math.floor(n2 / 2)) if (n2 % 2) { var bb = q - 1 - k; c[bb >> 3] |= 0x80 >> (bb % 8); } rows.push(R(3, i === 0 ? "First" : "Next", V6s(c) + "/" + q, "")); }
    if (extra > 2) { var last = b.slice(); for (bit = p; bit < q; bit++) last[bit >> 3] |= 0x80 >> (bit % 8); rows.push(R(3, "Last", V6s(last) + "/" + q, "")); }
    return rows;
  }
  function Bdp(speedText, rttText, winText) {
    var bps = Speed(speedText), rtt = Time(rttText); if (!isNaN(rtt) && /^\s*[0-9.,]+\s*$/.test(rttText || "")) rtt /= 1000;
    if (isNaN(bps) || bps <= 0) return Need("Link speed", "type e.g. 1 Gbps or 500 Mbps"); if (isNaN(rtt) || rtt <= 0) return Need("Round trip", "type e.g. 80 ms");
    var bdp = bps * rtt / 8, rows = [R(0, "In flight", ByteSize(bdp) + " - the window TCP needs to fill " + Bits(bps) + " at " + Dur(rtt), "BDP = speed × round trip ÷ 8.")];
    var scale = 0; while (65535 * Math.pow(2, scale) < bdp && scale < 14) scale++;
    rows.push(R(3, "Window scaling", scale === 0 ? "not needed - fits in 64 KB" : "shift " + scale + " (×" + Math.pow(2, scale) + ") - Windows and Linux do this on their own", ""));
    var win = Size(winText);
    if (!isNaN(win) && win > 0) { var max = win * 8 / rtt; rows.push(R(max < bps * 0.9 ? 1 : 0, "With a " + ByteSize(win) + " window", "at most " + Bits(Math.min(max, bps)) + (max < bps * 0.9 ? " - the window, not the link, is the limit" : " - enough to fill the link"), "Throughput ≤ window ÷ round trip.")); }
    rows.push(R(3, "One stream, 64 KB window", "at most " + Bits(65535 * 8 / rtt), "Why old apps and some VPNs crawl on long links."));
    return rows;
  }
  var NrPrb = { 5: [25, 11, 0], 10: [52, 24, 11], 15: [79, 38, 18], 20: [106, 51, 24], 25: [133, 65, 31], 30: [160, 78, 38], 40: [216, 106, 51], 50: [270, 133, 65], 60: [0, 162, 79], 70: [0, 189, 93], 80: [0, 217, 107], 90: [0, 245, 121], 100: [0, 273, 135] };
  function Cellular(techText, bwText, mimoText, scsText) {
    var tech = String(techText || "").trim().toLowerCase(), nr = tech.indexOf("nr") >= 0 || tech.indexOf("5g") >= 0;
    if (!nr && tech.indexOf("lte") < 0 && tech.indexOf("4g") < 0) return Need("Technology", "type LTE or NR (5G)");
    var bw = First(bwText); if (isNaN(bw) || bw <= 0) return Need("Bandwidth", "type e.g. 20 MHz");
    var mm = String(mimoText || "").toLowerCase(), lm = /(\d)\s*x\s*(\d)/.exec(mm), layers = lm ? Math.min(+lm[1], +lm[2]) : 2;
    var qm = mm.indexOf("1024") >= 0 ? 10 : mm.indexOf("256") >= 0 ? 8 : mm.indexOf("64") >= 0 ? 6 : mm.indexOf("16") >= 0 ? 4 : mm.indexOf("qpsk") >= 0 ? 2 : 8;
    if (layers < 1 || layers > 8) return Need("MIMO", "layers are 1 to 8, e.g. 4x4");
    var rows = [], mbps;
    if (nr) {
      var scs = First(scsText); if (isNaN(scs)) scs = 30; var col = scs <= 15 ? 0 : scs <= 30 ? 1 : 2, mu = col, prbs = NrPrb[Math.trunc(bw)];
      if (!prbs || prbs[col] === 0) return Need("Bandwidth", "NR FR1 at " + F(15 * Math.pow(2, col), "0") + " kHz takes " + Object.keys(NrPrb).map(Number).filter(function (k) { return NrPrb[k][col] > 0; }).join(", ") + " MHz");
      var ts = 1e-3 / (14 * Math.pow(2, mu)); mbps = 1e-6 * layers * qm * 1.0 * (948 / 1024) * prbs[col] * 12 / ts * (1 - 0.14);
      rows.push(R(0, "Peak (downlink)", F(mbps, "0") + " Mbps", "TS 38.306: layers × bits per symbol × 948/1024 × PRB × 12 ÷ symbol time × (1 − 14% overhead), one carrier."));
      rows.push(R(3, "Carrier", F(bw, "0") + " MHz · " + prbs[col] + " resource blocks at " + F(15 * Math.pow(2, mu), "0") + " kHz · " + layers + " layers · " + Math.pow(2, qm) + "QAM", ""));
      rows.push(R(3, "Uplink (rough)", "about " + F(mbps / layers * Math.min(layers, 2) * 0.2, "0") + " Mbps - phones send on 1-2 layers, and TDD bands give most slots to the downlink", ""));
    } else {
      if (bw > 20) return Need("Bandwidth", "an LTE carrier is at most 20 MHz - add carriers with aggregation");
      var rb = bw === 1.4 ? 6 : bw * 5; mbps = rb * 12 * 14000 * qm * layers * 0.75 / 1e6;
      rows.push(R(0, "Peak (downlink)", F(mbps, "0") + " Mbps", "Resource blocks × 12 subcarriers × 14 symbols a millisecond × bits per symbol × layers, less ~25% for control, reference signals and coding."));
      rows.push(R(3, "Carrier", F(bw, "0.#") + " MHz · " + F(rb, "0") + " resource blocks · " + layers + " layers · " + Math.pow(2, qm) + "QAM", ""));
    }
    rows.push(R(3, "What you'll see", "about " + F(mbps * 0.3, "0") + "-" + F(mbps * 0.6, "0") + " Mbps on a good day - the cell is shared and the signal is rarely perfect", ""));
    return rows;
  }
  function WifiRate(genText, widthText, ssText) {
    var gen = Math.trunc(First(genText)) || 0, g0 = String(genText || "").toLowerCase();
    if (g0.indexOf("ax") >= 0) gen = 6; else if (g0.indexOf("be") >= 0) gen = 7; else if (g0.indexOf("ac") >= 0) gen = 5; else if (/\bn\b/.test(g0)) gen = 4;
    if (gen < 4 || gen > 7) return Need("Generation", "type 4 (n), 5 (ac), 6 (ax) or 7 (be)");
    var w = isNaN(First(widthText)) ? (gen >= 6 ? 80 : gen === 5 ? 80 : 40) : Math.trunc(First(widthText)), ss = isNaN(First(ssText)) ? 2 : Math.trunc(First(ssText));
    var maxW = gen === 4 ? 40 : gen === 7 ? 320 : 160, maxSs = gen === 4 ? 4 : gen === 7 ? 16 : 8;
    if ((w !== 20 && w !== 40 && w !== 80 && w !== 160 && w !== 320) || w > maxW) return Need("Channel width", "Wi-Fi " + gen + " takes 20" + (maxW >= 40 ? ", 40" : "") + (maxW >= 80 ? ", 80" : "") + (maxW >= 160 ? ", 160" : "") + (maxW >= 320 ? ", 320" : "") + " MHz");
    if (ss < 1 || ss > maxSs) return Need("Streams", "1 to " + maxSs + " (laptops and phones have 2)");
    var sc, bits, sym;
    if (gen >= 6) { sc = w === 20 ? 234 : w === 40 ? 468 : w === 80 ? 980 : w === 160 ? 1960 : 3920; bits = gen === 7 ? 12 * 5 / 6 : 10 * 5 / 6; sym = 13.6e-6; }
    else { sc = w === 20 ? 52 : w === 40 ? 108 : w === 80 ? 234 : 468; bits = gen === 5 ? 8 * 5 / 6 : 6 * 5 / 6; sym = 3.6e-6; }
    var phy = sc * bits * ss / sym / 1e6;
    return [R(0, "Link rate", F(phy, "0") + " Mbps", "Data subcarriers × bits per subcarrier (top MCS) × streams ÷ symbol time."),
      R(3, "Real downloads", "about " + F(phy * 0.55, "0") + "-" + F(phy * 0.7, "0") + " Mbps close to the router - airtime, ACKs and sharing take the rest", ""),
      R(3, "Top MCS", (gen === 7 ? "4096-QAM 5/6" : gen === 6 ? "1024-QAM 5/6" : gen === 5 ? "256-QAM 5/6" : "64-QAM 5/6") + " - needs a strong signal (about -55 dBm or better); further away it steps down", ""),
      R(3, "Wider isn't free", w >= 80 ? (w === 80 ? "an " : "a ") + w + " MHz channel is easier to clash with neighbours - 40 MHz is often faster in flats" : "few neighbours to clash with at " + w + " MHz", "")];
  }
  function LinkBudget(txText, gainText, distText, sensText) {
    var tx = First(txText); if (isNaN(tx)) return Need("Transmit power", "type e.g. 20 dBm");
    if (/\bm?W\b/i.test(txText || "") && !/dbm/i.test(txText)) tx = 10 * Math.log10(tx * (/\bmW\b/.test(txText) ? 1 : 1000));
    var gains = anyNums(gainText), gt = gains.length > 0 ? gains[0] : 0, gr = gains.length > 1 ? gains[1] : gt;
    var dt = String(distText || "").toLowerCase(), dm = /([0-9.,]+)\s*(km|m|mi)\b/.exec(dt), fm = /([0-9.,]+)\s*(ghz|mhz)/.exec(dt);
    if (!dm || !fm) return Need("Distance and frequency", "type e.g. 2 km at 5.8 GHz");
    var d = parseFloat(dm[1].replace(",", ".")) * (dm[2] === "km" ? 1 : dm[2] === "mi" ? 1.609 : 0.001), fmhz = parseFloat(fm[1].replace(",", ".")) * (fm[2] === "ghz" ? 1000 : 1);
    if (!(d > 0) || !(fmhz > 0)) return Need("Distance and frequency", "both have to be more than 0");
    var fspl = 20 * Math.log10(d) + 20 * Math.log10(fmhz) + 32.44, rx = tx + gt + gr - fspl;
    var rows = [R(0, "Received", F(rx, "0.#") + " dBm", "Transmit + gains − free-space loss. Real links lose another 5-20 dB to walls, rain, trees and cables."), R(3, "Free-space loss", F(fspl, "0.#") + " dB over " + F(d, "0.###") + " km at " + F(fmhz, "0") + " MHz", "FSPL = 20·log10(km) + 20·log10(MHz) + 32.44")];
    var sens = First(sensText);
    if (!isNaN(sens)) { var mg = rx - sens; rows.push(R(mg < 10 ? (mg < 0 ? 2 : 1) : 0, "Margin", F(mg, "0.#") + " dB" + (mg < 0 ? " - won't link" : mg < 10 ? " - thin; fades will drop it" : mg < 20 ? " - OK" : " - comfortable"), "Aim for 15-20 dB on outdoor links.")); }
    rows.push(R(3, "Keep clear", F(8.657 * Math.sqrt(d / (fmhz / 1000)), "0.#") + " m around the line at the middle (first Fresnel zone) - 60% of it at least", ""));
    return rows;
  }
  function Raid(levelText, disksText, sizeText) {
    var lv = String(levelText || "").toLowerCase().replace(/raid|\s/g, ""), fd = First(disksText), n = Math.trunc(fd), size = Size(sizeText);
    if (isNaN(size)) { var v = First(sizeText); if (!isNaN(v)) size = v * 1e12; }
    if (isNaN(fd) || n < 1) return Need("Disks", "type how many, e.g. 6");
    if (!/^\d+(\s*(disks?|drives?))?$/i.test(String(disksText || "").trim())) return Need("Disks", "a whole number of disks, e.g. 6 - \"" + String(disksText || "").trim() + "\" isn't one");
    if (isNaN(size) || size <= 0) return Need("Disk size", "type e.g. 4 TB");
    var min, data, survive, note;
    switch (lv) {
      case "0": min = 2; data = n; survive = 0; note = "striped - fast, but one disk dies and everything goes"; break;
      case "1": min = 2; data = 1; survive = n - 1; note = "mirrored - every disk holds a full copy"; break;
      case "5": min = 3; data = n - 1; survive = 1; note = "one disk of parity - a rebuild on big disks takes a day and stresses the rest"; break;
      case "6": min = 4; data = n - 2; survive = 2; note = "two disks of parity - the safe choice for big disks"; break;
      case "10": case "1+0": min = 4; data = Math.floor(n / 2); survive = 1; note = "mirrors, striped - survives one per mirror pair, fast rebuilds"; break;
      default: return Need("RAID level", "type 0, 1, 5, 6 or 10");
    }
    if (n < min) return Need("Disks", "RAID " + lv + " needs at least " + min);
    if ((lv === "10" || lv === "1+0") && n % 2 !== 0) return Need("Disks", "RAID 10 needs an even number");
    var usable = data * size;
    return [R(0, "Usable", ByteSize(usable) + "  (" + F(usable / Math.pow(2, 40), "0.##") + " TiB as Windows shows it)", ""),
      R(survive === 0 ? 2 : survive === 1 ? 1 : 0, "Survives", survive === 0 ? "no disk failures" : survive + " failed disk" + (survive === 1 ? "" : "s") + (lv === "10" || lv === "1+0" ? " (more if they're in different pairs)" : ""), ""),
      R(3, "Efficiency", Pct(100 * data / n) + " of " + I(n) + " × " + ByteSize(size) + " - " + note, ""),
      R(3, "Still not a backup", "RAID keeps you running when a disk dies; it copies a deleted file or ransomware just as fast", "")];
  }
  function Backup(dataText, linkText, changeText) {
    var bytes = Size(dataText), bps = Speed(linkText); if (isNaN(bytes) || bytes <= 0) return Need("Data", "type e.g. 2 TB"); if (isNaN(bps) || bps <= 0) return Need("Link", "type e.g. 1 Gbps");
    var eff = 0.7, full = bytes * 8 / (bps * eff), rows = [R(0, "Full copy", Dur(full) + " at about 70% of " + Bits(bps), "Disks, encryption, small files and the far end rarely let a copy run at the full link speed."), R(3, "At the full link speed", Dur(bytes * 8 / bps), "")];
    var ch = First(changeText);
    if (!isNaN(ch) && ch > 0) { var daily = String(changeText || "").indexOf("%") >= 0 ? bytes * ch / 100 : Size(changeText); if (isNaN(daily)) daily = bytes * ch / 100; var t = daily * 8 / (bps * eff); rows.push(R(t > 8 * 3600 ? 1 : 0, "Each night", ByteSize(daily) + " changes · " + Dur(t) + (t > 8 * 3600 ? " - longer than a night; it won't finish before morning" : ""), "")); }
    rows.push(R(3, "Restore", "plan on the full-copy time or longer - restores are usually slower than backups", ""));
    return rows;
  }
  function Ups(battText, loadText, effText) {
    var b = String(battText || "").toLowerCase(), wh, whm = /([0-9.,]+)\s*wh/.exec(b), ahm = /([0-9.,]+)\s*ah/.exec(b), vm = /([0-9.,]+)\s*v\b/.exec(b), xm = /\b(\d{1,3})\s*[x×]/.exec(b);
    var v = function (m) { return parseFloat(m[1].replace(",", ".")); };
    if (whm) wh = v(whm); else if (ahm && vm) wh = v(ahm) * v(vm) * (xm ? +xm[1] : 1);
    else return Need("Battery", "type the energy (216 Wh) or volts and amp-hours (2 x 12 V 9 Ah) - a VA rating alone doesn't say how long it lasts");
    var load = First(loadText); if (isNaN(load) || load <= 0) return Need("Load", "type the watts, e.g. 300 W");
    var eff = First(effText); eff = isNaN(eff) ? 0.85 : eff > 1 ? eff / 100 : eff;
    var hours = wh * eff / load, real = hours * (load > wh ? 0.6 : 0.8);
    return [R(real < 5 / 60 ? 1 : 0, "Runtime", "about " + Dur(real * 3600) + " at " + F(load, "0") + " W", "Energy × inverter efficiency ÷ load, less ~20% (more at heavy loads) for battery chemistry and age."),
      R(3, "On paper", Dur(hours * 3600) + " from " + F(wh, "0") + " Wh at " + Pct(eff * 100) + " efficiency", ""), R(3, "Rule of thumb", "batteries lose a lot of runtime after 3-5 years - test it once a year", "")];
  }
  function Sla(text) {
    var t = String(text || "").trim().toLowerCase(), up, n = First(t), Year = 365.25 * 86400;
    if (isNaN(n)) return Need("Uptime", "type e.g. 99.9% - or a downtime, e.g. 4 h a year");
    if (t.indexOf("%") >= 0 || (n > 90 && n < 100 && !/[a-z]/.test(t))) up = n / 100;
    else { var secs = Time(t.replace(/\s*(a|per|\/)\s*(year|month|week|day).*$/, "").trim()); if (isNaN(secs)) return Need("Uptime", "type e.g. 99.9% or 4 h a year"); var per = t.indexOf("month") >= 0 ? Year / 12 : t.indexOf("week") >= 0 ? 7 * 86400 : t.indexOf("day") >= 0 ? 86400 : Year; up = 1 - secs / per; }
    if (up <= 0 || up >= 1) return Need("Uptime", "has to be between 0 and 100%");
    var down = 1 - up;
    return [R(0, "Uptime", Pct(up * 100) + "  (" + F(-Math.log10(down), "0.#") + " nines)", ""), R(3, "Down a year", Dur(down * Year), ""), R(3, "Down a month", Dur(down * Year / 12), ""),
      R(3, "Down a week", Dur(down * 7 * 86400), ""), R(3, "Down a day", Dur(down * 86400), ""), R(3, "Two in a row", "two " + Pct(up * 100) + " parts in series give " + Pct(up * up * 100) + " - each link in the chain counts", "")];
  }
  function Growth(nowText, growText, capText) {
    var now = Size(nowText); if (isNaN(now)) { var v0 = First(nowText); if (!isNaN(v0)) now = v0 * 1e12; }
    if (isNaN(now) || now <= 0) return Need("Storage now", "type e.g. 10 TB");
    var g = String(growText || "").toLowerCase(), gv = First(g); if (isNaN(gv) || gv <= 0) return Need("Growth", "type e.g. 3% a month or 2 TB a year");
    var pct = g.indexOf("%") >= 0, perMonth = g.indexOf("year") >= 0 ? 1 / 12 : g.indexOf("week") >= 0 ? 52 / 12 : g.indexOf("day") >= 0 ? 30.4 : 1, step = Size(g.replace(/\s*(a|per|\/)\s*(year|month|week|day).*$/, ""));
    var after = function (mo) { return pct ? now * Math.pow(1 + gv / 100, mo * perMonth) : now + step * mo * perMonth; };
    if (!pct && isNaN(after(1))) return Need("Growth", "type e.g. 3% a month or 2 TB a year");
    var rows = [R(3, "In 12 months", ByteSize(after(12)), ""), R(3, "In 3 years", ByteSize(after(36)), "")];
    var cap = Size(capText); if (isNaN(cap)) { var v1 = First(capText); if (!isNaN(v1)) cap = v1 * 1e12; }
    if (!isNaN(cap) && cap > 0) {
      if (cap <= now) rows.unshift(R(2, "Full", "already at or past " + ByteSize(cap), ""));
      else { var mo = 0; while (after(mo) < cap && mo < 1200) mo++; var warn = 0; while (after(warn) < cap * 0.8 && warn < 1200) warn++;
        rows.unshift(R(mo <= 6 ? 2 : mo <= 18 ? 1 : 0, "Full in", mo >= 1200 ? "over 100 years" : (mo < 24 ? mo + " months" : F(mo / 12, "0.#") + " years") + " · 80% in " + (warn < 24 ? warn + " months" : F(warn / 12, "0.#") + " years"), "Order more at 80% - buying, shipping and migrating takes months.")); }
    }
    return rows;
  }
  function Stream(resText, codecText, upText) {
    var r = String(resText || "").toLowerCase(), m = /(\d{3,4})\s*p\s*(\d{2,3})?/.exec(r), h, fps;
    if (r.indexOf("4k") >= 0 || r.indexOf("2160") >= 0) h = 2160; else if (r.indexOf("1440") >= 0 || r.indexOf("2k") >= 0) h = 1440; else if (m) h = +m[1]; else return Need("Resolution", "type e.g. 1080p60, 720p30 or 4k60");
    var fm = /p\s*(\d{2,3})|(\d{2,3})\s*fps|4k\s*(\d{2,3})/.exec(r); fps = fm ? +(fm[1] || fm[2] || fm[3]) : 30;
    if (h < 240 || h > 4320 || fps < 10 || fps > 240) return Need("Resolution", "type e.g. 1080p60");
    var w = h * 16 / 9, c = String(codecText || "").toLowerCase(), codec = c.indexOf("av1") >= 0 ? 0.55 : c.indexOf("hevc") >= 0 || c.indexOf("265") >= 0 ? 0.65 : 1, cname = codec === 0.55 ? "AV1" : codec === 0.65 ? "HEVC" : "H.264";
    var bpp = 0.055 * Math.pow(1080 / h, 0.25), video = w * h * fps * bpp * codec, total = video + 160e3;
    var rows = [R(0, "Bitrate", Bits(total) + " (" + cname + ", video " + Bits(video) + " + audio 160 kbps)", "Pixels × frames × about " + F(bpp, "0.###") + " bits each for H.264, less for HEVC and AV1. Fast games want more; talking heads need less."),
      R(3, "Upload you need", "at least " + Bits(total * 1.5) + " - 50% headroom so the stream doesn't drop when someone else uploads", ""),
      R(3, "Data", ByteSize(total / 8 * 3600) + " an hour · " + ByteSize(total / 8 * 3600 * 4) + " for a 4-hour stream or recording", "")];
    var up = Speed(upText);
    if (!isNaN(up) && up > 0) rows.splice(1, 0, R(up < total * 1.5 ? (up < total ? 2 : 1) : 0, "Your upload", Bits(up) + (up < total ? " - too slow; drop to " + (h > 720 ? "720p" : "a lower frame rate") : up < total * 1.5 ? " - just about; no headroom" : " - plenty"), "Run the speed test for your real upload."));
    return rows;
  }
  function DataUse(videoText, callText, gameText) {
    var v = String(videoText || "").toLowerCase(), vh = First(v), ch = First(callText), gh = First(gameText);
    if (isNaN(vh) && isNaN(ch) && isNaN(gh)) return Need("Hours", "type hours a day, e.g. 3 h HD");
    var vRate = v.indexOf("4k") >= 0 || v.indexOf("uhd") >= 0 ? 7 : v.indexOf("sd") >= 0 ? 1 : 3, gb = [isNaN(vh) ? 0 : vh * vRate * 30.4, isNaN(ch) ? 0 : ch * 1 * 30.4, isNaN(gh) ? 0 : gh * 0.1 * 30.4], total = gb[0] + gb[1] + gb[2];
    return [R(0, "A month", F(total, "0") + " GB", "Video " + F(vRate, "0") + " GB an hour, video calls ~1 GB, online games ~0.1 GB (not counting game downloads and updates, often 50-150 GB each)."),
      R(3, "Video", F(gb[0], "0") + " GB", ""), R(3, "Calls", F(gb[1], "0") + " GB", ""), R(3, "Games", F(gb[2], "0") + " GB", ""),
      R(total > 1000 ? 1 : 3, "Plan", total > 1000 ? "over 1 TB - check your plan's cap" : "fits most capped plans above " + F(Math.ceil(total / 100) * 100, "0") + " GB", "")];
  }
  var Uses = { "4k": [25, 0.2], hd: [5, 0.1], sd: [3, 0.1], call: [4, 4], game: [3, 1], stream: [1, 8], work: [5, 2], download: [50, 1], browse: [2, 0.5] };
  function Plan(downText, upText, whoText) {
    var down = Speed(downText), up = Speed(upText); if (isNaN(down) || down <= 0) return Need("Plan download", "type e.g. 300 Mbps");
    var re = /\b(\d{1,3})\s*(?:x\s*)?([a-z0-9]+)/g, parts = [], mm; while ((mm = re.exec(String(whoText || "").toLowerCase()))) parts.push(mm);
    if (!parts.length) return Need("Busy at once", "type e.g. 2 4k, 1 call, 1 game (also: hd, sd, stream, work, download, browse)");
    var needD = 0, needU = 0, unknown = [], what = [];
    parts.forEach(function (p) { var n = +p[1], k = p[2].replace(/s+$/, ""); if (k === "4" || k === "uhd") k = "4k"; if (k === "video" || k === "movie" || k === "netflix") k = "hd"; if (k === "gamer" || k === "gaming") k = "game"; if (k === "zoom" || k === "team" || k === "meeting") k = "call";
      var u = Object.prototype.hasOwnProperty.call(Uses, k) ? Uses[k] : null; if (!u) { unknown.push(p[2]); return; } needD += n * u[0]; needU += n * u[1]; what.push(n + " " + k); });
    if (!what.length) return Need("Busy at once", "use: 4k, hd, sd, call, game, stream, work, download, browse");
    var dM = down / 1e6, uM = isNaN(up) ? NaN : up / 1e6;
    var rows = [R(dM >= needD * 1.3 ? 0 : dM >= needD ? 1 : 2, "Download", F(needD, "0") + " Mbps needed of your " + F(dM, "0") + (dM >= needD * 1.3 ? " - plenty" : dM >= needD ? " - tight; Wi-Fi usually gets less than the plan" : " - not enough at once"), "Plans are the most you'll get on a cable; Wi-Fi often delivers half to two thirds.")];
    if (!isNaN(uM)) rows.push(R(uM >= needU * 1.3 ? 0 : uM >= needU ? 1 : 2, "Upload", F(needU, "0.#") + " Mbps needed of your " + F(uM, "0.#") + (uM >= needU ? "" : " - calls and streams will break up first"), ""));
    else rows.push(R(3, "Upload", F(needU, "0.#") + " Mbps needed - type your plan's upload to check it", ""));
    rows.push(R(3, "Counted", what.join(", ") + (unknown.length ? "  ·  not known: " + unknown.join(", ") : ""), ""));
    return rows;
  }
  function BinSize(b) { return b >= 1099511627776 ? F(b / 1099511627776, "0.##") + " TiB" : b >= 1073741824 ? F(b / 1073741824, "0.##") + " GiB" : b >= 1048576 ? F(b / 1048576, "0.##") + " MiB" : b >= 1024 ? F(b / 1024, "0.##") + " KiB" : F(b, "0") + " bytes"; }
  function Units(text) {
    var t = String(text || "").trim(); if (t === "") return Need("Value", "type e.g. 100 Mbps, 12 MB/s, 1 GiB or 90 min");
    var rows = [], p = Parse2(t), v = p.v, u = p.u; if (isNaN(v)) return Need("Value", "type a number and a unit, e.g. 100 Mbps");
    if (u.indexOf("/") >= 0 || /ps$/.test(u.toLowerCase())) {
      var bps = Speed(t); if (isNaN(bps)) return Need("Value", "type e.g. 100 Mbps or 12 MB/s");
      rows.push(R(0, "Bits", Bits(bps), "")); rows.push(R(0, "Bytes", ByteRate(bps), "")); rows.push(R(3, "Binary", F(bps / 8 / 1048576, "0.##") + " MiB/s - what some download managers show", "")); rows.push(R(3, "An hour", ByteSize(bps / 8 * 3600) + " · a day " + ByteSize(bps / 8 * 86400), ""));
    } else if (!isNaN(Size(t)) || /^(Kb|Mb|Gb|Tb|kbit|Mbit|Gbit|bit|bits)$/i.test(u)) {
      var bytes = Size(t); if (isNaN(bytes)) { var mult = /^T/i.test(u) ? 1e12 : /^G/i.test(u) ? 1e9 : /^M/i.test(u) ? 1e6 : /^K/i.test(u) ? 1e3 : 1; bytes = v * mult / 8; }
      rows.push(R(0, "Decimal", ByteSize(bytes) + " (what drive makers and networks use)", "")); rows.push(R(0, "Binary", BinSize(bytes) + " (what Windows shows, though it writes GB)", ""));
      rows.push(R(3, "Bits", Bits(bytes * 8).replace("ps", "") + " · " + I(Math.min(bytes * 8, 9223372036854775807)) + " bits", ""));
    } else { var s = Time(t); if (isNaN(s)) return Need("Value", "use a size (GB, GiB, Mb), a speed (Mbps, MB/s) or a time (ms, s, min, h)"); rows.push(R(0, "Time", Dur(s), "")); rows.push(R(3, "In seconds", F(s, "0.######") + " s", "")); rows.push(R(3, "In ms", F(s * 1000, "0.###") + " ms", "")); }
    return rows;
  }
  function Tdm(lineText, slotsText) {
    var l = String(lineText || "").toLowerCase(), e1 = l.indexOf("e1") >= 0, t1 = l.indexOf("t1") >= 0 || l.indexOf("ds1") >= 0;
    if (!e1 && !t1) return Need("Line", "type E1 or T1");
    var max = e1 ? 31 : 24, n = First(slotsText), ts = isNaN(n) ? (e1 ? 30 : 24) : Math.trunc(n);
    if (ts < 1 || ts > max) return Need("Timeslots", "1 to " + max + " on " + (e1 ? "an E1" : "a T1"));
    var rows = [R(0, "Bandwidth", I(ts * 64) + " kbps for " + ts + " timeslot" + (ts === 1 ? "" : "s"), "Each timeslot (DS0) is 8 bits × 8000 frames a second = 64 kbps."), R(3, "The line", e1 ? "2,048 kbps: 32 × 64k - slot 0 is framing, slot 16 usually signalling, 30 for voice" : "1,544 kbps: 24 × 64k + 8k framing bit", "")];
    if (t1) rows.push(R(3, "Robbed-bit", I(ts * 56) + " kbps if the line steals a bit for signalling (56k per slot)", ""));
    rows.push(R(3, "Calls", ts + " uncompressed G.711 calls" + (e1 && ts === 31 ? " (if slot 16 isn't signalling)" : ""), ""));
    return rows;
  }
  function JitterBuf(pdvText, ptimeText, codecText) {
    var pd = anyNums(pdvText); if (!pd.length) return Need("Delay variation", "type e.g. 30 ms, or the measured range 20-70 ms");
    var pdv = pd.length >= 2 ? Math.abs(pd[1] - pd[0]) : pd[0], pt = First(ptimeText); if (isNaN(pt) || pt <= 0) pt = 20;
    var buf = Math.ceil(pdv * 1.5 / pt) * pt; if (buf < pt) buf = pt;
    var c = String(codecText || "").toLowerCase(), cd = c.indexOf("729") >= 0 ? 25 : c.indexOf("opus") >= 0 ? 26.5 : 1, net = pd.length >= 2 ? Math.min(pd[0], pd[1]) : 40, m2e = cd + pt + net + buf;
    return [R(0, "Buffer", F(buf, "0") + " ms (" + F(buf / pt, "0") + " packets of " + F(pt, "0") + " ms)", "About 1.5 × the delay variation, rounded up to whole packets - big enough that late packets still play."),
      R(m2e > 400 ? 2 : m2e > 150 ? 1 : 0, "Mouth to ear", "about " + F(m2e, "0") + " ms" + (m2e > 400 ? " - people will talk over each other" : m2e > 150 ? " - noticeable; ITU G.114 says keep under 150 ms" : " - fine"), "Codec + packet time + network delay" + (pd.length < 2 ? " (40 ms assumed)" : "") + " + buffer."),
      R(3, "Too small", "packets that arrive later than the buffer are dropped - choppy audio even with 0% network loss", "")];
  }

  // ================================================================== Calc3: money, markets, commodities, games
  function P(t) { var m = /-?[0-9]+(?:\.[0-9]+)?/.exec(String(t || "").replace(/,/g, "")); return m ? parseFloat(m[0]) : NaN; }
  function Ps(t) { return (String(t || "").replace(/, /g, " ").match(/-?[0-9]+(?:\.[0-9]+)?/g) || []).map(parseFloat); }
  function M(v) { return (v < 0 ? "-$" : "$") + F(Math.abs(v), Math.abs(v) >= 1000 ? "N0" : "N2"); }
  function Pc(v) { return (v > 0 ? "+" : "") + F(v, "0.##") + "%"; }
  var Edu = "The numbers only - not advice.", MON = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
  function dayStr(d) { d = d instanceof Date ? d : new Date(d); return d.getDate() + " " + MON[d.getMonth()]; }
  // Bank of Canada rates (CAD per unit), handed in by the page from the day's numbers: { USD: 1.40, ..., day: Date }
  var rates = { list: {}, day: null };
  function FxConv(amountText, from, to, markupText) {
    var amt = P(amountText); if (isNaN(amt)) return Need("Amount", "type e.g. 1000");
    var cad = function (c) { c = String(c || "").trim().toUpperCase(); if (c === "CAD" || c === "") return 1; return rates.list[c] || NaN; }, a = cad(from), b = cad(to), n = Object.keys(rates.list).length;
    if (isNaN(a) || isNaN(b)) return [R(1, "Rates", n === 0 ? "no Bank of Canada rates yet - open Live or Trading once while online" : "no rate for " + (isNaN(a) ? from : to), "")];
    var mid = amt * a / b, mk = P(markupText); if (isNaN(mk)) mk = 2.5;
    return [R(0, "At the official rate", F(amt, "N2") + " " + from + " = " + F(mid, "N2") + " " + to, "Bank of Canada, " + dayStr(rates.day || new Date()) + " - indicative, nobody trades at exactly this."),
      R(1, "What a bank usually gives", F(mid * (1 - mk / 100), "N2") + " " + to + "  (" + F(mk, "0.##") + "% markup)", "The markup is the real fee - often 2-3% at a bank, under 1% at a currency broker."),
      R(3, "Rate", "1 " + from + " = " + F(a / b, "0.0000") + " " + to, "")];
  }
  function Pnl(buyText, sellText, sharesText, feesText) {
    var buy = P(buyText), sell = P(sellText), n = P(sharesText); if (isNaN(buy) || isNaN(n) || n <= 0) return Need("Buy price and shares", "type e.g. 150 and 20");
    var fs = Ps(feesText), comm = fs.length ? fs[0] : 0, fxm = /([0-9.]+)\s*%/.exec(feesText || ""), fx = fxm ? P(fxm[1]) / 100 : 0;
    var cost = buy * n * (1 + fx) + comm, be = (buy * n * (1 + fx) + 2 * comm) / (n * (1 - fx));
    var rows = [R(3, "You pay", M(cost) + " (" + M(buy * n) + " + fees " + M(cost - buy * n) + ")", ""), R(0, "Break-even sell price", M(be) + "  (" + Pc((be / buy - 1) * 100) + " just to cover the fees)", Edu)];
    if (!isNaN(sell)) { var back = sell * n * (1 - fx) - comm, pl = back - cost; rows.unshift(R(pl >= 0 ? 0 : 2, pl >= 0 ? "Profit" : "Loss", M(pl) + "  (" + Pc(pl / cost * 100) + ")", "Selling at " + M(sell) + " brings " + M(back) + " after fees.")); }
    return rows;
  }
  function PosSize(accText, riskText, entryText, stopText) {
    var acc = P(accText), risk = P(riskText), e = P(entryText), s = P(stopText);
    if (isNaN(acc) || isNaN(e) || isNaN(s) || e === s) return Need("Account, entry and stop", "type e.g. 20000, 1%, 50 and 47");
    if (isNaN(risk)) risk = 1;
    var cash = acc * risk / 100, per = Math.abs(e - s), n = Math.floor(cash / per), pos = n * e;
    return [R(0, "Shares", F(n, "N0") + " - each loses " + M(per) + " if the stop is hit", "Risking " + M(cash) + " (" + F(risk, "0.##") + "% of " + M(acc) + "). " + Edu),
      R(pos > acc ? 1 : 3, "Position", M(pos) + " = " + F(pos / acc * 100, "0") + "% of the account" + (pos > acc ? " - more than you have (borrowed money)" : ""), "A stop isn't guaranteed: a price that jumps (gaps) can skip right past it.")];
  }
  function AvgCost(pricesText, sharesText) {
    var p = Ps(pricesText), n = Ps(sharesText); if (!p.length) return Need("Prices", "type the prices you bought at, e.g. 50, 45, 40");
    if (!n.length) n = p.map(function () { return 1; }); var k = Math.min(p.length, n.length), tot = 0, sh = 0; for (var i = 0; i < k; i++) { tot += p[i] * n[i]; sh += n[i]; }
    if (sh <= 0) return Need("Shares", "type how many each time");
    var pk = p.slice(0, k);
    return [R(0, "Average cost", M(tot / sh) + " a share", k + " buys, " + F(sh, "N0") + " shares, " + M(tot) + " in all. " + Edu), R(3, "Lowest and highest", M(Math.min.apply(null, pk)) + " - " + M(Math.max.apply(null, pk)), "")];
  }
  function CompoundCalc(startText, monthlyText, rateText, yearsText) {
    var s = P(startText), m = P(monthlyText), r = P(rateText), y = P(yearsText);
    if (isNaN(s)) s = 0; if (isNaN(m)) m = 0; if (isNaN(r) || isNaN(y) || y < 1 || y > 60) return Need("Rate and years", "type e.g. 6% and 20 years");
    var yi = Math.trunc(y), bal = s, rr = r / 100 / 12; for (var yy = 1; yy <= yi; yy++) for (var mm = 0; mm < 12; mm++) bal = bal * (1 + rr) + m;
    var put = s + m * 12 * yi;
    return [R(0, "After " + yi + " years", M(bal), "You put in " + M(put) + "; growth " + M(bal - put) + ". A steady " + F(r, "0.##") + "% a year is an assumption - real returns go up and down. " + Edu),
      R(3, "Doubles about every", r > 0 ? F(72 / r, "0.#") + " years (rule of 72)" : "-", "")];
  }
  function PctBack(pctText) {
    var p = P(pctText); if (isNaN(p) || p <= -100) return Need("Change", "type e.g. -30% or +50%");
    if (p < 0) return [R(1, "To get back", Pc((1 / (1 + p / 100) - 1) * 100) + " needed after a " + Pc(p) + " fall", "Falls hurt more than rises help: -50% needs +100%.")];
    return [R(3, "To lose it all again", Pc((1 / (1 + p / 100) - 1) * 100) + " undoes a " + Pc(p) + " rise", ""), R(3, "Same-size fall", "a " + Pc(p) + " rise then " + Pc(-p) + " leaves " + Pc(((1 + p / 100) * (1 - p / 100) - 1) * 100), "")];
  }
  function TradeLat(pingText, priceText, volText) {
    var ping = P(pingText), price = P(priceText), vol = P(volText); if (isNaN(ping) || ping < 0) return Need("Round trip", "type your broker's ping, e.g. 40 ms");
    if (isNaN(price)) price = 100; if (isNaN(vol)) vol = 2;
    var move = vol / 100 / Math.sqrt(23400) * Math.sqrt(Math.max(0.001, ping / 1000)) * price;
    return [R(ping < 150 ? 0 : 1, "Typical move while your order travels", M(move) + " on " + M(price) + " (" + F(move / price * 100, "0.####") + "%)", "For anyone clicking by hand, " + F(ping, "0") + " ms is far below what matters - your own reaction takes 200 ms or more. Spikes and drops matter more than the average."),
      R(3, "In that time", "light could cross about " + F(ping * 100, "N0") + " km of fibre each way", "")];
  }
  var ComUnits = [["barrel", "v", 158.987], ["US gallon", "v", 3.78541], ["litre", "v", 1], ["m³", "v", 1000], ["troy oz", "m", 31.1035], ["gram", "m", 1], ["kg", "m", 1000], ["lb", "m", 453.592], ["tonne", "m", 1000000], ["short ton", "m", 907185], ["bushel", "m", 0], ["MMBtu", "e", 1055.06], ["GJ", "e", 1000], ["therm", "e", 105.506], ["kWh", "e", 3.6]];
  var BushelKg = { wheat: 27.2155, soybeans: 27.2155, corn: 25.4012 };
  function bushel(crop) { return (BushelKg[crop] || BushelKg.wheat) * 1000; }
  function UnitOf(t, crop) { t = String(t || "").toLowerCase(); var s = ComUnits.slice().sort(function (a, b) { return b[0].length - a[0].length; }); for (var i = 0; i < s.length; i++) if (t.indexOf(s[i][0].toLowerCase()) >= 0) return { u: s[i], b: s[i][0] === "bushel" ? bushel(crop) : s[i][2] }; return null; }
  function ComConv(amountText, unitText, cropText) {
    var amt = P(amountText), crop = String(cropText == null ? "wheat" : cropText).trim().toLowerCase(), x = UnitOf(unitText, crop);
    if (isNaN(amt) || !x) return Need("Amount and unit", "e.g. 1 barrel, 1 troy oz, 100 bushel, 1 MMBtu");
    var u = x.u, rows = [];
    ComUnits.filter(function (z) { return z[1] === u[1] && z[0] !== u[0]; }).forEach(function (o) { var ob = o[0] === "bushel" ? bushel(crop) : o[2], v = amt * x.b / ob; rows.push(R(3, o[0] + (o[0] === "bushel" ? " of " + crop : ""), F(v, v >= 100 ? "N0" : v >= 1 ? "N2" : "0.####"), "")); });
    rows.unshift(R(0, F(amt, "N2") + " " + u[0] + (u[0] === "bushel" ? " of " + crop : ""), u[1] === "v" ? "volume" : u[1] === "m" ? "weight" : "energy", u[0] === "bushel" ? "A bushel is a volume: by weight wheat and soybeans are 60 lb, corn 56 lb." : u[0] === "troy oz" ? "Gold and silver are priced per troy ounce (31.1 g), not the everyday ounce (28.3 g)." : ""));
    return rows;
  }
  var Futures = [["WTI crude oil", 1000, "barrel", 0.01], ["Natural gas", 10000, "MMBtu", 0.001], ["Gold", 100, "troy oz", 0.10], ["Silver", 5000, "troy oz", 0.005], ["Copper", 25000, "lb", 0.0005], ["Corn", 5000, "bushel", 0.0025], ["Wheat", 5000, "bushel", 0.0025], ["Soybeans", 5000, "bushel", 0.0025]];
  function FuturesCalc(contractText, priceText, countText, moveText) {
    var c = Futures.filter(function (z) { return String(contractText || "").toLowerCase().indexOf(z[0].toLowerCase()) >= 0; })[0], price = P(priceText), n = P(countText), mv = P(moveText);
    if (!c || isNaN(price)) return Need("Contract and price", "e.g. WTI crude oil at 75 a barrel");
    if (isNaN(n) || n < 1) n = 1; var size = c[1], tick = c[3];
    var rows = [R(0, "One contract controls", F(size, "N0") + " " + c[2] + "s = " + M(size * price) + (n > 1 ? "  ·  " + n + " contracts: " + M(size * price * n) : ""), "You put up only a margin (often 5-15%), so gains and losses run on the whole value. " + Edu),
      R(3, "Smallest move (one tick)", M(tick) + " a " + c[2] + " = " + M(tick * size * n), ""), R(3, "A $1 move in price", M(size * n), "")];
    if (!isNaN(mv)) rows.splice(1, 0, R(1, "A " + Pc(mv) + " move", M(size * price * n * mv / 100), ""));
    return rows;
  }
  function PerUnit(priceText, fromText, toText, curText) {
    var price = P(priceText), f = UnitOf(fromText, "wheat"), t = UnitOf(toText, "wheat");
    if (isNaN(price) || !f || !t) return Need("Price and units", "e.g. 75 US$ a barrel, in litres");
    if (f.u[1] !== t.u[1]) return [R(2, "Units", f.u[0] + " and " + t.u[0] + " measure different things", "")];
    var usd = price * t.b / f.b, cur = String(curText || "").trim().toUpperCase(), rows = [R(0, "In US$", F(usd, usd < 1 ? "0.0000" : "N2") + " a " + t.u[0], "")];
    if (cur === "CAD") { var r = rates.list.USD; if (r) rows.unshift(R(0, "In CA$", F(usd * r, usd * r < 1 ? "0.0000" : "N2") + " a " + t.u[0], "at the Bank of Canada's US$ " + F(r, "0.0000") + " (" + dayStr(rates.day || new Date()) + "). The raw commodity only - taxes, refining and shipping come on top.")); else rows.push(R(1, "In CA$", "no Bank of Canada rate yet - open Live or Trading once while online", "")); }
    return rows;
  }
  function TickRate(tickText, pingText, interpText) {
    var tick = P(tickText), ping = P(pingText), lerp = P(interpText); if (isNaN(tick) || tick <= 0 || isNaN(ping)) return Need("Tick rate and ping", "e.g. 64 and 40 ms");
    if (isNaN(lerp)) lerp = 2; var tms = 1000 / tick, late = ping / 2 + lerp * tms;
    return [R(late < 100 ? 0 : late < 160 ? 1 : 2, "You see the game", F(late, "0") + " ms late = " + F(late / tms, "0.#") + " ticks behind", "Half your ping (the update travelling to you) + " + F(lerp, "0.#") + " ticks of smoothing (interpolation) the game adds on purpose."),
      R(3, "One tick", F(tms, "0.#") + " ms at " + F(tick, "0") + " Hz", "Lag compensation rewinds the server about your ping when you shoot - that's why you can be hit behind a wall.")];
  }

  // ================================================================== subnet calculator (Toolkit.Subnet on the PC; here without "this PC's address")
  function ipParse(t) {   // { b: bytes, v4: bool } - IPv4 as four plain numbers, IPv6 the usual ways (a %zone is set aside)
    t = String(t || "").trim(); var z = t.indexOf("%"); if (z > 0) t = t.slice(0, z);
    if (/^[0-9.]+$/.test(t)) { var v = V4(t); return v == null ? null : { b: [Math.floor(v / 16777216) % 256, Math.floor(v / 65536) % 256, Math.floor(v / 256) % 256, v % 256], v4: true }; }
    var b6 = V6(t); return b6 ? { b: b6, v4: false } : null;
  }
  function ipStr(a) { return a.v4 ? a.b.join(".") : V6s(a.b); }
  function maskBytes(n, prefix) { var m = new Array(n).fill(0); for (var i = 0; i < prefix; i++) m[i >> 3] |= 0x80 >> (i % 8); return m; }
  function MaskBits(m) { var n = 0; for (var k = 0; k < m.length; k++) for (var i = 7; i >= 0; i--) { if ((m[k] >> i & 1) === 1) n++; else return n; } return n; }
  function eq(a, b) { if (a.length !== b.length) return false; for (var i = 0; i < a.length; i++) if (a[i] !== b[i]) return false; return true; }
  function Subnet(input, inside, gateway) {
    var rows = [], s = String(input || "").trim(), prefix = -1, ipPart = s, slash = s.indexOf("/"), sp = s.split(/\s+/).filter(Boolean), bad = null, mk = null;
    if (s === "") { rows.push(R(2, "Input", "type e.g. 10.1.2.3/22, 10.1.2.3 255.255.252.0 or 2001:db8::1/64", "")); return rows; }
    if (slash > 0) { ipPart = s.slice(0, slash); var pt = s.slice(slash + 1); if (!/^[0-9]+$/.test(pt)) bad = "\"/" + pt + "\" isn't a prefix length"; else prefix = +pt; }
    else if (sp.length === 2) { ipPart = sp[0]; mk = ipParse(sp[1]); if (!mk) bad = "\"" + sp[1] + "\" isn't a mask"; else { prefix = MaskBits(mk.b); if (!eq(mk.b, maskBytes(mk.b.length, prefix))) bad = sp[1] + " isn't a contiguous mask"; } }
    else if (sp.length > 2) bad = "too many parts - one address and a prefix or mask";
    if (bad == null && /^[0-9.]+$/.test(ipPart) && !/^(0|[1-9][0-9]{0,2})(\.(0|[1-9][0-9]{0,2})){3}$/.test(ipPart)) bad = "write the address as four plain numbers, no leading zeros (Windows reads 010 as 8)";
    var ip = bad == null ? ipParse(ipPart) : null;
    if (bad != null || !ip) { rows.push(R(2, "Input", (bad != null ? bad + " - " : "") + "type e.g. 10.1.2.3/22, 10.1.2.3 255.255.252.0 or 2001:db8::1/64", "")); return rows; }
    var bits = ip.v4 ? 32 : 128; if (prefix < 0) prefix = bits;
    if (mk && mk.v4 !== ip.v4) { rows.push(R(2, "Mask", "an IPv" + (bits === 32 ? "6" : "4") + " mask on an IPv" + (bits === 32 ? "4" : "6") + " address", "")); return rows; }
    if (prefix > bits) { rows.push(R(2, "Prefix", "/" + prefix + " is too long", "")); return rows; }
    var b = ip.b, mask = maskBytes(b.length, prefix), net = b.map(function (x, i) { return x & mask[i]; }), bc = b.map(function (x, i) { return (x | ~mask[i]) & 255; }), V = function (a) { return ipStr({ b: a, v4: ip.v4 }); };
    var count = bits === 32 ? Math.pow(2, 32 - prefix) : 0;
    rows.push(R(3, "Network", V(net) + "/" + prefix, ""));
    if (bits === 32) {
      rows.push(R(3, "Mask / wildcard", V(mask) + "  /  " + V(mask.map(function (x) { return ~x & 255; })), ""));
      if (prefix <= 30) { var f = net.slice(); f[3]++; var l = bc.slice(); l[3]--; rows.push(R(3, "Usable hosts", V(f) + " - " + V(l) + "  (" + (count - 2) + ")", "")); rows.push(R(3, "Broadcast", V(bc), "")); }
      else rows.push(R(3, "Addresses", count + (prefix === 31 ? " (point-to-point link, RFC 3021)" : " (one host)"), ""));
      rows.push(R(3, "In binary", b.map(function (x) { return ("0000000" + x.toString(2)).slice(-8); }).join("."), ""));
    } else {
      var g = []; for (var i = 0; i < 8; i++) g.push(("000" + (b[i * 2] * 256 + b[i * 2 + 1]).toString(16)).slice(-4));
      rows.push(R(3, "Expanded", g.join(":"), "")); rows.push(R(3, "Compressed", V(b), "")); rows.push(R(3, "Addresses in it", prefix === 64 ? "2^64 (a standard LAN)" : "2^" + (bits - prefix), ""));
    }
    if (!blank(inside)) {
      var q = ipParse(inside);
      if (!q) rows.push(R(1, "Inside?", "\"" + String(inside).trim() + "\" isn't an IP address - type one like " + (ip.v4 ? "10.1.2.50" : "2001:db8::50"), ""));
      else if (q.v4 !== ip.v4) rows.push(R(1, ipStr(q) + " inside?", "can't be - it's IPv" + (q.v4 ? "4" : "6") + " and this network is IPv" + (ip.v4 ? "4" : "6"), ""));
      else { var inq = eq(q.b.map(function (x, i) { return x & mask[i]; }), net); rows.push(R(inq ? 0 : 1, ipStr(q) + " inside?", inq ? "yes" : "no", "")); }
    }
    var gr = GatewayRows(ip, prefix, net, bc, gateway), at = 1; for (var k = 0; k < rows.length; k++) if (rows[k][1] === "Network") at = k + 1;
    rows.splice.apply(rows, [at, 0].concat(gr));
    return rows;
  }
  function GatewayRows(ip, prefix, net, bc, gateway) {
    var rows = [], g = String(gateway || "").trim(), v4 = ip.v4, bits = v4 ? 32 : 128, hb = ip.b, mask = maskBytes(hb.length, prefix), V = function (a) { return ipStr({ b: a, v4: v4 }); };
    if (g === "") { if (v4 && prefix <= 30) { var f0 = net.slice(); f0[3]++; var l0 = bc.slice(); l0[3]--; rows.push(R(3, "Gateway", "none given - the usual choice is " + V(f0) + " (first) or " + V(l0) + " (last)", "")); } return rows; }
    var gp = -1, gPart = g, gs = g.split(/\s+/).filter(Boolean), gsl = g.indexOf("/");
    if (gsl > 0) { gPart = g.slice(0, gsl); gp = /^[0-9]+$/.test(g.slice(gsl + 1)) ? +g.slice(gsl + 1) : -2; }
    else if (gs.length === 2) { gPart = gs[0]; var gm = ipParse(gs[1]); if (gm && gm.v4 === v4) { gp = MaskBits(gm.b); if (!eq(gm.b, maskBytes(gm.b.length, gp))) gp = -2; } else gp = -2; }
    var gw = ipParse(gPart);
    if (!gw || gp === -2 || gp > bits) { rows.push(R(2, "Gateway", "\"" + g + "\" isn't an address (with an optional /prefix or mask)", "")); return rows; }
    if (gw.v4 !== v4) { rows.push(R(2, "Gateway", "an IPv" + (v4 ? "6" : "4") + " gateway for an IPv" + (v4 ? "4" : "6") + " address - each family needs its own", "")); return rows; }
    var gb = gw.b, inNet = eq(gb.map(function (x, i) { return x & mask[i]; }), net), gws = ipStr(gw);
    if (eq(gb, hb)) { rows.push(R(2, "Gateway", "Will not work: the gateway is this host's own address - it would send to itself. Use the router's address.", "")); return rows; }
    if (!v4) {
      if (gb[0] === 0xfe && (gb[1] & 0xc0) === 0x80) rows.push(R(0, "Gateway", gws + " - link-local, the normal IPv6 way (learned from router adverts, always on-link)", ""));
      else if (!inNet) rows.push(R(1, "Gateway", gws + " is outside " + V(net) + "/" + prefix + " - it only works if a route says it's on-link; routers usually give a link-local (fe80::) gateway", ""));
      else rows.push(R(0, "Gateway", gws + " is inside the prefix", ""));
      return rows;
    }
    var netTxt = V(net) + "/" + prefix;
    if (prefix === 32) { rows.push(R(1, "Gateway", "a /32 has no neighbours: " + gws + " is reachable only with an on-link route (Windows adds one for a /32 point-to-point link; most hosts don't)", "")); return rows; }
    if (!inNet) {
      var common = 0; for (var i = 0; i < 32; i++) { var bt = 0x80 >> (i % 8); if ((hb[i >> 3] & bt) !== (gb[i >> 3] & bt)) break; common++; }
      var fix;
      if (gp >= 0 && gp < prefix) { var gm2 = maskBytes(4, gp), gnet = gb.map(function (x, i) { return x & gm2[i]; }), holds = eq(hb.map(function (x, i) { return x & gm2[i]; }), gnet); fix = holds ? "Use /" + gp + " (" + V(gm2) + ") - the gateway's network " + V(gnet) + "/" + gp + " holds " + ipStr(ip) + " too." : "The gateway's own network " + V(gnet) + "/" + gp + " doesn't hold " + ipStr(ip) + " either - one of the two addresses is wrong."; }
      else { var cm = maskBytes(4, common); fix = common >= 1 ? "The smallest subnet holding both is /" + common + " (" + V(cm) + ") - if that's the real network, use that mask; otherwise the gateway address is wrong." : "No sensible subnet holds both - the gateway address is wrong."; }
      rows.push(R(2, "Gateway", "Will not work: " + gws + " is outside " + netTxt + ". The host won't ARP for it, so nothing leaves this subnet - no ping beyond it, no internet. " + fix, "")); return rows;
    }
    if (prefix <= 30 && eq(gb, net)) { rows.push(R(2, "Gateway", "Will not work: " + gws + " is the network address of " + netTxt + ", not a host", "")); return rows; }
    if (prefix <= 30 && eq(gb, bc)) { rows.push(R(2, "Gateway", "Will not work: " + gws + " is the broadcast address of " + netTxt + ", not a host", "")); return rows; }
    if (gp >= 0 && gp !== prefix) { rows.push(R(1, "Gateway", gws + " is inside, but its mask is /" + gp + " and the host's is /" + prefix + " - the two disagree about who is on-link, so some traffic works one way only (one side ARPs, the other routes). Use the same mask on both.", "")); return rows; }
    if (prefix === 31) { rows.push(R(0, "Gateway", gws + " - the other end of the /31 point-to-point link", "")); return rows; }
    var first = net.slice(); first[3]++; var last = bc.slice(); last[3]--;
    if (eq(gb, first) || eq(gb, last)) rows.push(R(0, "Gateway", gws + " is inside " + netTxt + " (the " + (eq(gb, first) ? "first" : "last") + " usable address - the usual choice)", ""));
    else rows.push(R(0, "Gateway", gws + " is inside " + netTxt + " - fine; not the first or last usable address, which is the usual choice", ""));
    return rows;
  }
  var C = window.AMCALC = { R: R, I: I, F: F, V4: V4, U: U, maskOf: maskOf, Size: Size, Speed: Speed, Time: Time, Parse2: Parse2, Bits: Bits, ByteSize: ByteSize, ByteRate: ByteRate, Dur: Dur, blank: blank, rnd: rnd,
    rates: rates, V6: V6, V6s: V6s, First: First, anyNums: anyNums,
    fn: { Stack: Stack, Vlsm: Vlsm, Bandwidth: Bandwidth, Number: Number_, Wildcard: Wildcard, V6Split: V6Split, Bdp: Bdp, Cellular: Cellular, WifiRate: WifiRate, LinkBudget: LinkBudget, Raid: Raid, Backup: Backup, Ups: Ups, Sla: Sla, Growth: Growth,
      Stream: Stream, DataUse: DataUse, Plan: Plan, Units: Units, Tdm: Tdm, JitterBuf: JitterBuf, FxConv: FxConv, Pnl: Pnl, PosSize: PosSize, AvgCost: AvgCost, CompoundCalc: CompoundCalc, PctBack: PctBack, TradeLat: TradeLat,
      ComConv: ComConv, FuturesCalc: FuturesCalc, PerUnit: PerUnit, TickRate: TickRate, Subnet: Subnet } };
})();
