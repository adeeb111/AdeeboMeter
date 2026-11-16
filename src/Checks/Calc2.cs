// AdeeboMeter - part of the source; see src/Program.cs for the overview.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // Calculators, 1.1.5 (PLAN 11.12): our own arithmetic from public formulas (3GPP TS 38.306 for NR, IEEE 802.11
    // subcarrier counts, Friis free-space loss, the usual RAID / SLA / bit-rate maths). Nothing is sent, nothing is read
    // from the PC. Every answer says what it means in one line; the formula sits in the advice for Details.
    static partial class Calc
    {
        static readonly Regex AnyNum = new Regex(@"(?<![0-9.,])-?[0-9]+(?:[.,][0-9]+)?", RegexOptions.CultureInvariant);   // "20-70 ms" is 20 and 70, "-80 dBm" is -80
        static double First(string t) { var m = AnyNum.Match(t ?? ""); double v; return m.Success && double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }
        static double[] Nums(string t) { return AnyNum.Matches(t ?? "").Cast<Match>().Select(m => { double v; return double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }).Where(v => !double.IsNaN(v)).ToArray(); }
        static List<string[]> Need(string box, string what) { return new List<string[]> { R(2, box, what, "", "u") }; }
        static string Pct(double v) { return F(v, v < 1 ? "0.###" : "0.##") + "%"; }

        // ---------- wildcard mask: mask <-> prefix <-> wildcard, and what an ACL line with it matches
        public static List<string[]> Wildcard(string text)
        {
            string t = (text ?? "").Trim(); if (t == "") return Need("Mask", "type a mask (255.255.252.0), a prefix (/22) or a wildcard (0.0.3.255)");
            uint mask; int prefix; bool zero = false;
            if (Regex.IsMatch(t, @"^/?\d{1,2}$")) { prefix = int.Parse(t.TrimStart('/'), CultureInfo.InvariantCulture); if (prefix > 32) return Need("Mask", "a prefix is 0 to 32"); mask = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix); }
            else
            {
                IPAddress ip; if (!IPAddress.TryParse(t, out ip) || ip.AddressFamily != AddressFamily.InterNetwork) return Need("Mask", "type it like 255.255.252.0, /22 or 0.0.3.255");
                var b = ip.GetAddressBytes(); uint v = (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]); zero = v == 0;
                bool isWild = (b[0] & 0x80) == 0 && v != 0; if (isWild) v = ~v;   // 0.0.3.255 is a wildcard; flip it into a mask
                uint inv = ~v; if ((inv & (inv + 1)) != 0) return new List<string[]> { R(1, "Not contiguous", t + " has gaps - fine in a Cisco ACL wildcard (it matches odd patterns), but it isn't a subnet mask", "Each 0 bit in a wildcard must match; each 1 bit is 'don't care'.", "i") };
                mask = v; prefix = 0; for (uint m2 = mask; m2 != 0; m2 <<= 1) prefix++;
            }
            uint wild = ~mask, ex = 0x0A000000u & mask; long hosts = prefix >= 31 ? (prefix == 31 ? 2 : 1) : (1L << (32 - prefix)) - 2;   // FRESH-002: the example block on its own boundary
            var rows = new List<string[]> {
                R(0, "Mask", Dotted(mask), "", "i"), R(0, "Prefix", "/" + prefix, "", "i"), R(0, "Wildcard", Dotted(wild), "What an ACL or OSPF network line wants: the mask turned inside out.", "i"),
                R(3, "Addresses", I(1L << (32 - prefix)) + " in the block · " + I(hosts) + " usable" + (prefix == 31 ? " (point-to-point, RFC 3021)" : ""), "", "i"),
                R(3, "In an ACL", "permit ip " + Dotted(ex) + " " + Dotted(wild) + " any  - matches " + Dotted(ex) + "/" + prefix, "Swap " + Dotted(ex) + " for your block's first address.", "i") };
            if (zero) rows.Insert(0, R(1, "Read as", "the /0 mask - every address. As an ACL wildcard, 0.0.0.0 means one exact address (/32): type /32 for that", "", "i"));   // FRESH-001
            return rows;
        }
        static string Dotted(uint v) { return (v >> 24) + "." + ((v >> 16) & 255) + "." + ((v >> 8) & 255) + "." + (v & 255); }

        // ---------- IPv6 prefix split: a /48 into /56s, how many, the first and last ones, /64s in each
        public static List<string[]> V6Split(string prefixText, string intoText)
        {
            // FRESH-004: ::ffff:192.0.2.0/120 too
            var m = Regex.Match((prefixText ?? "").Trim(), @"^([0-9a-fA-F:.]+)/(\d{1,3})$"); IPAddress ip;
            if (!m.Success || !IPAddress.TryParse(m.Groups[1].Value, out ip) || ip.AddressFamily != AddressFamily.InterNetworkV6) return Need("Prefix", "type it like 2001:db8:abcd::/48");
            int p = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); if (p > 128) return Need("Prefix", "a prefix is 0 to 128");
            string it = (intoText ?? "").Trim(); int q;
            if (it.StartsWith("/", StringComparison.Ordinal)) { if (!int.TryParse(it.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out q)) return Need("Split into", "type /56, /64 or a count like 256"); }
            else if (it != "" && (double.IsNaN(First(it)) || First(it) < 2)) return Need("Split into", "type /56, /64 or a count like 256");   // FRESH-003: only a blank picks the default
            else { double n = First(it); if (double.IsNaN(n)) { q = p <= 56 ? 64 : Math.Min(128, p + 8); } else { int bits = (int)Math.Ceiling(Math.Log(n, 2) - 1e-9); q = p + bits; } }
            if (q <= p || q > 128) return Need("Split into", "has to be longer than /" + p + " and at most /128");
            int extra = q - p; var b = ip.GetAddressBytes(); ClearFrom(b, p);
            var rows = new List<string[]>();
            rows.Add(R(0, "Pieces", (extra >= 63 ? "2^" + extra : I(1L << extra)) + " × /" + q, "", "i"));
            if (q <= 64) rows.Add(R(3, "Each one holds", (64 - q >= 63 ? "2^" + (64 - q) : I(1L << (64 - q))) + " LAN-sized /64" + (q == 64 ? "" : "s"), "A /64 is one LAN (SLAAC needs exactly /64).", "i"));
            else rows.Add(R(1, "Smaller than /64", "SLAAC and most LANs need a /64; /" + q + " fits point-to-point links (/127) or loopbacks (/128)", "", "i"));
            int show = (int)Math.Min(4, 1L << Math.Min(extra, 20));
            for (int i = 0; i < show; i++) rows.Add(R(3, i == 0 ? "First" : "Next", new IPAddress(Nth(b, q, i)) + "/" + q, "", "i"));
            if (extra > 2) { var last = (byte[])b.Clone(); for (int bit = p; bit < q; bit++) last[bit / 8] |= (byte)(0x80 >> (bit % 8)); rows.Add(R(3, "Last", new IPAddress(last) + "/" + q, "", "i")); }
            return rows;
        }
        static void ClearFrom(byte[] b, int p) { for (int bit = p; bit < b.Length * 8; bit++) b[bit / 8] &= (byte)~(0x80 >> (bit % 8)); }
        static byte[] Nth(byte[] baseB, int q, long n)
        {
            var b = (byte[])baseB.Clone(); for (int k = 0; k < 62 && n != 0; k++, n >>= 1) if ((n & 1) != 0) { int bit = q - 1 - k; b[bit / 8] |= (byte)(0x80 >> (bit % 8)); }
            return b;
        }

        // ---------- BDP: how much is in flight on a link, and the TCP window it takes to fill it
        public static List<string[]> Bdp(string speedText, string rttText, string winText)
        {
            double bps = Speed(speedText), rtt = Time(rttText); if (!double.IsNaN(rtt) && Regex.IsMatch(rttText ?? "", @"^\s*[0-9.,]+\s*$")) rtt /= 1000;   // a bare RTT is milliseconds
            if (double.IsNaN(bps) || bps <= 0) return Need("Link speed", "type e.g. 1 Gbps or 500 Mbps");
            if (double.IsNaN(rtt) || rtt <= 0) return Need("Round trip", "type e.g. 80 ms");
            double bdp = bps * rtt / 8; var rows = new List<string[]>();
            rows.Add(R(0, "In flight", ByteSize(bdp) + " - the window TCP needs to fill " + Bits(bps) + " at " + Dur(rtt), "BDP = speed × round trip ÷ 8.", "i"));
            int scale = 0; while (65535.0 * (1 << scale) < bdp && scale < 14) scale++;
            if (bdp > 65535.0 * (1 << 14)) rows.Add(R(1, "Window scaling", "even the largest shift (14, ×16384) caps one window at about 1.07 GB - less than the " + ByteSize(bdp) + " in flight, so one stream can't fill this link; use several", "", "i"));   // FRESH-005
            else rows.Add(R(3, "Window scaling", scale == 0 ? "not needed - fits in 64 KB" : "shift " + scale + " (×" + (1 << scale) + ") - Windows and Linux do this on their own", "", "i"));
            double win = Size(winText);
            if (!double.IsNaN(win) && win > 0)
            {
                double max = win * 8 / rtt;
                rows.Add(R(max < bps ? 1 : 0, "With a " + ByteSize(win) + " window", "at most " + Bits(Math.Min(max, bps)) + (max < bps ? " - the window, not the link, is the limit" : " - enough to fill the link"), "Throughput ≤ window ÷ round trip.", "i"));   // FRESH-006
            }
            rows.Add(R(3, "One stream, 64 KB window", "at most " + Bits(65535 * 8 / rtt), "Why old apps and some VPNs crawl on long links.", "i"));
            return rows;
        }

        // ---------- LTE and 5G NR peak throughput (NR: TS 38.306 4.1.2, FR1; LTE: resource blocks × 12 × 14 symbols, ~25% overhead)
        static readonly Dictionary<int, int[]> NrPrb = new Dictionary<int, int[]> {   // TS 38.101-1 table 5.3.2-1: bandwidth (MHz) -> max PRB for 15 / 30 / 60 kHz
            { 5, new[] { 25, 11, 0 } }, { 10, new[] { 52, 24, 11 } }, { 15, new[] { 79, 38, 18 } }, { 20, new[] { 106, 51, 24 } }, { 25, new[] { 133, 65, 31 } }, { 30, new[] { 160, 78, 38 } },
            { 40, new[] { 216, 106, 51 } }, { 50, new[] { 270, 133, 65 } }, { 60, new[] { 0, 162, 79 } }, { 70, new[] { 0, 189, 93 } }, { 80, new[] { 0, 217, 107 } }, { 90, new[] { 0, 245, 121 } }, { 100, new[] { 0, 273, 135 } } };
        public static List<string[]> Cellular(string techText, string bwText, string mimoText, string scsText)
        {
            string tech = (techText ?? "").Trim().ToLowerInvariant(); bool nr = tech.Contains("nr") || tech.Contains("5g");
            if (!nr && !tech.Contains("lte") && !tech.Contains("4g")) return Need("Technology", "type LTE or NR (5G)");
            double bw = First(bwText); if (double.IsNaN(bw) || bw <= 0) return Need("Bandwidth", "type e.g. 20 MHz");
            string mm = (mimoText ?? "").ToLowerInvariant(); var lm = Regex.Match(mm, @"(\d)\s*[x×]\s*(\d)"); int layers = lm.Success ? Math.Min(int.Parse(lm.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(lm.Groups[2].Value, CultureInfo.InvariantCulture)) : 2;
            int qm = mm.Contains("1024") ? 10 : mm.Contains("256") ? 8 : mm.Contains("64") ? 6 : mm.Contains("16") ? 4 : mm.Contains("qpsk") ? 2 : 8;
            if (layers < 1 || layers > 8) return Need("MIMO", "layers are 1 to 8, e.g. 4x4");
            var rows = new List<string[]>(); double mbps;
            if (nr)
            {
                double scs = First(scsText); if (double.IsNaN(scs)) scs = 30; if (scs != 15 && scs != 30 && scs != 60) return Need("Subcarrier spacing", "FR1 uses 15, 30 or 60 kHz");   // FRESH-007
                int col = scs == 15 ? 0 : scs == 30 ? 1 : 2; int mu = col;
                // FRESH-009: 20.9 MHz isn't 20
                int[] prbs; if (!Whole(bw) || !NrPrb.TryGetValue((int)bw, out prbs) || prbs[col] == 0) return Need("Bandwidth", "NR FR1 at " + F(15 * Math.Pow(2, col), "0") + " kHz takes " + string.Join(", ", NrPrb.Where(k => k.Value[col] > 0).Select(k => k.Key)) + " MHz");
                double ts = 1e-3 / (14 * Math.Pow(2, mu));
                mbps = 1e-6 * layers * qm * 1.0 * (948.0 / 1024) * prbs[col] * 12 / ts * (1 - 0.14);
                rows.Add(R(0, "Peak (downlink)", F(mbps, "0") + " Mbps", "TS 38.306: layers × bits per symbol × 948/1024 × PRB × 12 ÷ symbol time × (1 − 14% overhead), one carrier.", "i"));
                rows.Add(R(3, "Carrier", F(bw, "0") + " MHz · " + prbs[col] + " resource blocks at " + F(15 * Math.Pow(2, mu), "0") + " kHz · " + layers + " layers · " + (1 << qm) + "QAM", "", "i"));
                rows.Add(R(3, "Uplink (rough)", "about " + F(mbps / layers * Math.Min(layers, 2) * 0.2, "0") + " Mbps - phones send on 1-2 layers, and TDD bands give most slots to the downlink", "", "i"));
            }
            else
            {
                if (bw > 20) return Need("Bandwidth", "an LTE carrier is at most 20 MHz - add carriers with aggregation");
                if (bw != 1.4 && bw != 3 && bw != 5 && bw != 10 && bw != 15 && bw != 20) return Need("Bandwidth", "an LTE carrier is 1.4, 3, 5, 10, 15 or 20 MHz");   // FRESH-008
                double rb = bw == 1.4 ? 6 : bw * 5;
                mbps = rb * 12 * 14000 * qm * layers * 0.75 / 1e6;
                rows.Add(R(0, "Peak (downlink)", F(mbps, "0") + " Mbps", "Resource blocks × 12 subcarriers × 14 symbols a millisecond × bits per symbol × layers, less ~25% for control, reference signals and coding.", "i"));
                rows.Add(R(3, "Carrier", F(bw, "0.#") + " MHz · " + F(rb, "0") + " resource blocks · " + layers + " layers · " + (1 << qm) + "QAM", "", "i"));
            }
            rows.Add(R(3, "What you'll see", "about " + F(mbps * 0.3, "0") + "-" + F(mbps * 0.6, "0") + " Mbps on a good day - the cell is shared and the signal is rarely perfect", "", "i"));
            return rows;
        }

        // ---------- Wi-Fi PHY rate by generation, channel width and streams (top MCS, short guard interval)
        public static List<string[]> WifiRate(string genText, string widthText, string ssText)
        {
            int gen = (int)First(genText); string g0 = (genText ?? "").ToLowerInvariant();
            if (g0.Contains("ax")) gen = 6; else if (g0.Contains("be")) gen = 7; else if (g0.Contains("ac")) gen = 5; else if (Regex.IsMatch(g0, @"\bn\b")) gen = 4;
            if (gen < 4 || gen > 7) return Need("Generation", "type 4 (n), 5 (ac), 6 (ax) or 7 (be)");
            double wf = First(widthText), sf = First(ssText);   // FRESH-010/011: 80.9 MHz or 2.9 streams aren't settings
            int w = double.IsNaN(wf) ? (gen >= 6 ? 80 : gen == 5 ? 80 : 40) : Whole(wf) ? (int)wf : -1;
            int ss = double.IsNaN(sf) ? 2 : Whole(sf) ? (int)sf : -1;
            int maxW = gen == 4 ? 40 : gen == 7 ? 320 : 160, maxSs = gen == 4 ? 4 : gen == 7 ? 16 : 8;
            if (w != 20 && w != 40 && w != 80 && w != 160 && w != 320 || w > maxW) return Need("Channel width", "Wi-Fi " + gen + " takes 20" + (maxW >= 40 ? ", 40" : "") + (maxW >= 80 ? ", 80" : "") + (maxW >= 160 ? ", 160" : "") + (maxW >= 320 ? ", 320" : "") + " MHz");
            if (ss < 1 || ss > maxSs) return Need("Streams", "1 to " + maxSs + " (laptops and phones have 2)");
            int sc; double bits, sym;
            if (gen >= 6) { sc = w == 20 ? 234 : w == 40 ? 468 : w == 80 ? 980 : w == 160 ? 1960 : 3920; bits = gen == 7 ? 12 * 5.0 / 6 : 10 * 5.0 / 6; sym = 13.6e-6; }
            else { sc = w == 20 ? 52 : w == 40 ? 108 : w == 80 ? 234 : 468; bits = gen == 5 ? 8 * 5.0 / 6 : 6 * 5.0 / 6; sym = 3.6e-6; }
            double phy = sc * bits * ss / sym / 1e6;
            return new List<string[]> {
                R(0, "Link rate", F(phy, "0") + " Mbps", "Data subcarriers × bits per subcarrier (top MCS) × streams ÷ symbol time.", "i"),
                R(3, "Real downloads", "about " + F(phy * 0.55, "0") + "-" + F(phy * 0.7, "0") + " Mbps close to the router - airtime, ACKs and sharing take the rest", "", "i"),
                R(3, "Top MCS", (gen == 7 ? "4096-QAM 5/6" : gen == 6 ? "1024-QAM 5/6" : gen == 5 ? "256-QAM 5/6" : "64-QAM 5/6") + " - needs a strong signal (about -55 dBm or better); further away it steps down", "", "i"),
                R(3, "Wider isn't free", w >= 80 ? (w == 80 ? "an " : "a ") + w + " MHz channel is easier to clash with neighbours - 40 MHz is often faster in flats" : "few neighbours to clash with at " + w + " MHz", "", "i") };
        }

        // ---------- link budget: free-space loss (Friis), received power, margin, and the Fresnel zone to keep clear
        public static List<string[]> LinkBudget(string txText, string gainText, string distText, string sensText)
        {
            double tx = First(txText); if (double.IsNaN(tx)) return Need("Transmit power", "type e.g. 20 dBm");
            if (Regex.IsMatch(txText ?? "", @"\bm?W\b", RegexOptions.IgnoreCase) && !Regex.IsMatch(txText, "dbm", RegexOptions.IgnoreCase))
            {
                if (tx <= 0) return Need("Transmit power", "has to be more than 0 W");   // FRESH-013
                tx = 10 * Math.Log10(tx * (Regex.IsMatch(txText, @"\bmw\b", RegexOptions.IgnoreCase) ? 1 : 1000));   // FRESH-012: 100 mw is milliwatts
            }
            var gains = Nums(gainText); double gt = gains.Length > 0 ? gains[0] : 0, gr = gains.Length > 1 ? gains[1] : gt;
            string dt = (distText ?? "").ToLowerInvariant(); var dm = Regex.Match(dt, @"(-?[0-9.,]+)\s*(km|m|mi)\b"); var fm = Regex.Match(dt, @"(-?[0-9.,]+)\s*(ghz|mhz)");   // FRESH-014: keep the minus, so -1 km is refused
            if (!dm.Success || !fm.Success) return Need("Distance and frequency", "type e.g. 2 km at 5.8 GHz");
            double d = double.Parse(dm.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture) * (dm.Groups[2].Value == "km" ? 1 : dm.Groups[2].Value == "mi" ? 1.609 : 0.001);
            double fmhz = double.Parse(fm.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture) * (fm.Groups[2].Value == "ghz" ? 1000 : 1);
            if (d <= 0 || fmhz <= 0) return Need("Distance and frequency", "both have to be more than 0");
            double fspl = 20 * Math.Log10(d) + 20 * Math.Log10(fmhz) + 32.44, rx = tx + gt + gr - fspl;
            var rows = new List<string[]> { R(0, "Received", F(rx, "0.#") + " dBm", "Transmit + gains − free-space loss. Real links lose another 5-20 dB to walls, rain, trees and cables.", "i"),
                R(3, "Free-space loss", F(fspl, "0.#") + " dB over " + F(d, "0.###") + " km at " + F(fmhz, "0") + " MHz", "FSPL = 20·log10(km) + 20·log10(MHz) + 32.44", "i"),
                R(3, "Antenna gains", F(gt, "0.#") + " + " + F(gr, "0.#") + " dBi" + (gains.Length == 1 ? " - one value given, used at both ends" : gains.Length == 0 ? " - none given" : ""), "", "i") };   // FRESH-071
            double sens = First(sensText);
            if (!double.IsNaN(sens)) { double mg = rx - sens; rows.Add(R(mg < 10 ? (mg < 0 ? 2 : 1) : 0, "Margin", F(mg, "0.#") + " dB" + (mg < 0 ? " - won't link" : mg < 10 ? " - thin; fades will drop it" : mg < 20 ? " - OK" : " - comfortable"), "Aim for 15-20 dB on outdoor links.", "i")); }
            double fres = 8.657 * Math.Sqrt(d / (fmhz / 1000));
            rows.Add(R(3, "Keep clear", F(fres, "0.#") + " m around the line at the middle (first Fresnel zone) - 60% of it at least", "", "i"));
            return rows;
        }

        // ---------- RAID usable space and how many disks it survives
        public static List<string[]> Raid(string levelText, string disksText, string sizeText)
        {
            string lv = Regex.Replace((levelText ?? "").ToLowerInvariant(), @"raid|\s", ""); int n = (int)First(disksText); double size = Bare(sizeText) ? First(sizeText) * 1e12 : Size(sizeText);   // a bare number is TB; "4 xb" isn't (FRESH-015)
            if (n < 1 || double.IsNaN(First(disksText))) return Need("Disks", "type how many, e.g. 6");
            if (!Regex.IsMatch((disksText ?? "").Trim(), @"^\d+(\s*(disks?|drives?))?$", RegexOptions.IgnoreCase)) return Need("Disks", "a whole number of disks, e.g. 6 - \"" + (disksText ?? "").Trim() + "\" isn't one");   // AM-CG-303: 3.9 disks isn't 3
            if (double.IsNaN(size) || size <= 0) return Need("Disk size", "type e.g. 4 TB");
            int min, data, survive; string note;
            switch (lv)
            {
                case "0": min = 2; data = n; survive = 0; note = "striped - fast, but one disk dies and everything goes"; break;
                case "1": min = 2; data = 1; survive = n - 1; note = "mirrored - every disk holds a full copy"; break;
                case "5": min = 3; data = n - 1; survive = 1; note = "one disk of parity - a rebuild on big disks takes a day and stresses the rest"; break;
                case "6": min = 4; data = n - 2; survive = 2; note = "two disks of parity - the safe choice for big disks"; break;
                case "10": case "1+0": min = 4; data = n / 2; survive = 1; note = "mirrors, striped - survives one per mirror pair, fast rebuilds"; break;
                default: return Need("RAID level", "type 0, 1, 5, 6 or 10");
            }
            if (n < min) return Need("Disks", "RAID " + lv + " needs at least " + min);
            if ((lv == "10" || lv == "1+0") && n % 2 != 0) return Need("Disks", "RAID 10 needs an even number");
            double usable = data * size;
            return new List<string[]> {
                R(0, "Usable", ByteSize(usable) + "  (" + F(usable / Math.Pow(2, 40), "0.##") + " TiB as Windows shows it)", "", "i"),
                R(survive == 0 ? 2 : survive == 1 ? 1 : 0, "Survives", survive == 0 ? "no disk failures" : survive + " failed disk" + (survive == 1 ? "" : "s") + (lv == "10" || lv == "1+0" ? " (more if they're in different pairs)" : ""), "", "i"),
                R(3, "Efficiency", Pct(100.0 * data / n) + " of " + I(n) + " × " + ByteSize(size) + " - " + note, "", "i"),
                R(3, "Still not a backup", "RAID keeps you running when a disk dies; it copies a deleted file or ransomware just as fast", "", "i") };
        }

        // ---------- backup and restore time: a full copy and the daily change over a link
        public static List<string[]> Backup(string dataText, string linkText, string changeText)
        {
            double bytes = Size(dataText), bps = Speed(linkText); if (double.IsNaN(bytes) || bytes <= 0) return Need("Data", "type e.g. 2 TB"); if (double.IsNaN(bps) || bps <= 0) return Need("Link", "type e.g. 1 Gbps");
            double eff = 0.7, full = bytes * 8 / (bps * eff);
            var rows = new List<string[]> { R(0, "Full copy", Dur(full) + " at about 70% of " + Bits(bps), "Disks, encryption, small files and the far end rarely let a copy run at the full link speed.", "i"),
                R(3, "At the full link speed", Dur(bytes * 8 / bps), "", "i") };
            double ch = First(changeText);
            if (!double.IsNaN(ch) && ch > 0)
            {
                double daily = (changeText ?? "").Contains("%") || Bare(changeText) ? bytes * ch / 100 : Size(changeText);
                if (double.IsNaN(daily)) return Need("Daily change", "type e.g. 3% or 60 GB");   // FRESH-016
                double t = daily * 8 / (bps * eff);
                rows.Add(R(t > 8 * 3600 ? 1 : 0, "Each night", ByteSize(daily) + " changes · " + Dur(t) + (t > 8 * 3600 ? " - longer than a night; it won't finish before morning" : ""), "", "i"));
            }
            rows.Add(R(3, "Restore", "plan on the full-copy time or longer - restores are usually slower than backups", "", "i"));
            return rows;
        }

        // ---------- UPS runtime from battery energy and load
        public static List<string[]> Ups(string battText, string loadText, string effText)
        {
            string b = (battText ?? "").ToLowerInvariant(); double wh;
            // FRESH-017/072: kWh, and a minus is kept
            var whm = Regex.Match(b, @"(-?[0-9.,]+)\s*(k?)wh"); var ahm = Regex.Match(b, @"(-?[0-9.,]+)\s*ah"); var vm = Regex.Match(b, @"(-?[0-9.,]+)\s*v\b"); var xm = Regex.Match(b, @"\b(\d{1,3})\s*[x×]");
            Func<Match, double> v = m => double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            if (whm.Success) wh = v(whm) * (whm.Groups[2].Value == "k" ? 1000 : 1);
            else if (ahm.Success && vm.Success) wh = v(ahm) * v(vm) * (xm.Success ? int.Parse(xm.Groups[1].Value, CultureInfo.InvariantCulture) : 1);
            else return Need("Battery", "type the energy (216 Wh) or volts and amp-hours (2 x 12 V 9 Ah) - a VA rating alone doesn't say how long it lasts");
            if (wh <= 0) return Need("Battery", "has to be more than 0 Wh");
            double load = First(loadText); if (Regex.IsMatch(loadText ?? "", @"\bkw\b", RegexOptions.IgnoreCase)) load *= 1000;   // FRESH-073
            if (double.IsNaN(load) || load <= 0) return Need("Load", "type the watts, e.g. 300 W");
            double eff = First(effText); eff = double.IsNaN(eff) ? 0.85 : (effText ?? "").Contains("%") || eff > 1 ? eff / 100 : eff;   // FRESH-019: 1% is 1%
            if (eff <= 0 || eff > 1) return Need("Efficiency", "0 to 100% - most inverters are 80-90%");   // FRESH-018
            double hours = wh * eff / load, real = hours * (load > wh ? 0.6 : 0.8);
            return new List<string[]> {
                R(real < 5.0 / 60 ? 1 : 0, "Runtime", "about " + Dur(real * 3600) + " at " + F(load, "0") + " W", "Energy × inverter efficiency ÷ load, less ~20% (more at heavy loads) for battery chemistry and age.", "i"),
                R(3, "On paper", Dur(hours * 3600) + " from " + F(wh, "0") + " Wh at " + Pct(eff * 100) + " efficiency", "", "i"),
                R(3, "Rule of thumb", "batteries lose a lot of runtime after 3-5 years - test it once a year", "", "i") };
        }

        // ---------- SLA: uptime % <-> downtime per year / month / week / day
        public static List<string[]> Sla(string text)
        {
            string t = (text ?? "").Trim().ToLowerInvariant(); double up;
            double n = First(t); if (double.IsNaN(n)) return Need("Uptime", "type e.g. 99.9% - or a downtime, e.g. 4 h a year");
            const double Year = 365.25 * 86400;
            if (t.Contains("%") || (n > 90 && n < 100 && !Regex.IsMatch(t, "[a-z]"))) up = n / 100;
            else
            {
                double secs = Time(Regex.Replace(t, @"\s*(a|per|/)\s*(year|quarter|month|week|day).*$", "").Trim()); if (double.IsNaN(secs)) return Need("Uptime", "type e.g. 99.9% or 4 h a year");
                double per = t.Contains("quarter") ? Year / 4 : t.Contains("month") ? Year / 12 : t.Contains("week") ? 7 * 86400 : t.Contains("day") ? 86400 : Year;
                up = 1 - secs / per;
            }
            if (up <= 0 || up > 1) return Need("Uptime", "has to be between 0 and 100%");
            double down = 1 - up; Func<double, string> D = s2 => down == 0 ? "none" : Dur(s2);   // FRESH-020: 100% is a target with no downtime
            return new List<string[]> {
                R(0, "Uptime", Pct(up * 100) + "  (" + (down == 0 ? "no downtime at all - nothing real promises this" : F(-Math.Log10(down), "0.#") + " nines") + ")", "", "i"),
                R(3, "Down a year", D(down * Year), "", "i"), R(3, "Down a month", D(down * Year / 12), "", "i"),
                R(3, "Down a week", D(down * 7 * 86400), "", "i"), R(3, "Down a day", D(down * 86400), "", "i"),
                R(3, "Two in a row", "two " + Pct(up * 100) + " parts in series give " + Pct(up * up * 100) + " - each link in the chain counts", "", "i") };
        }

        // ---------- storage growth: when does it fill?
        public static List<string[]> Growth(string nowText, string growText, string capText)
        {
            double now = Bare(nowText) ? First(nowText) * 1e12 : Size(nowText);   // a bare number is TB; "10 elephants" isn't (FRESH-054)
            if (double.IsNaN(now) || now <= 0) return Need("Storage now", "type e.g. 10 TB");
            string g = (growText ?? "").ToLowerInvariant(); double gv = First(g); if (double.IsNaN(gv) || gv < 0) return Need("Growth", "type e.g. 3% a month or 2 TB a year");   // FRESH-021: 0 is flat
            bool pct = g.Contains("%"); double perMonth = g.Contains("year") ? 1.0 / 12 : g.Contains("week") ? 52.0 / 12 : g.Contains("day") ? 30.4 : 1;
            Func<double, double> after = mo => pct ? now * Math.Pow(1 + gv / 100, mo * perMonth) : now + Size(Regex.Replace(g, @"\s*(a|per|/)\s*(year|month|week|day).*$", "")) * mo * perMonth;
            if (!pct && double.IsNaN(after(1))) return Need("Growth", "type e.g. 3% a month or 2 TB a year");
            var rows = new List<string[]> { R(3, "In 12 months", ByteSize(after(12)), "", "i"), R(3, "In 3 years", ByteSize(after(36)), "", "i") };
            double cap = Bare(capText) ? First(capText) * 1e12 : Size(capText);
            if (!double.IsNaN(cap) && cap > 0)
            {
                if (cap <= now) rows.Insert(0, R(2, "Full", "already at or past " + ByteSize(cap), "", "i"));
                else
                {
                    int mo = 0; while (after(mo) < cap && mo < 1200) mo++;
                    int warn = 0; while (after(warn) < cap * 0.8 && warn < 1200) warn++;
                    rows.Insert(0, R(mo <= 6 ? 2 : mo <= 18 ? 1 : 0, "Full in", mo >= 1200 ? (gv == 0 ? "never - it isn't growing" : "over 100 years") : (mo < 24 ? mo + " months" : F(mo / 12.0, "0.#") + " years") + " · 80% in " + (warn < 24 ? warn + " months" : F(warn / 12.0, "0.#") + " years"), "Order more at 80% - buying, shipping and migrating takes months.", "i"));
                }
            }
            return rows;
        }

        // ---------- streaming: the bitrate for a resolution and frame rate, the upload it needs, the data it uses
        public static List<string[]> Stream(string resText, string codecText, string upText)
        {
            string r = (resText ?? "").ToLowerInvariant(); var m = Regex.Match(r, @"(\d{3,4})\s*p\s*(\d{2,3})?"); int h, fps;
            if (r.Contains("4k") || r.Contains("2160")) h = 2160; else if (r.Contains("1440") || r.Contains("2k")) h = 1440; else if (m.Success) h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); else return Need("Resolution", "type e.g. 1080p60, 720p30 or 4k60");
            // FRESH-022: 5fps is read, then refused
            var fm = Regex.Match(r, @"p\s*(\d{1,3})|(\d{1,3})\s*fps|4k\s*(\d{1,3})"); fps = fm.Success ? int.Parse(fm.Groups.Cast<Group>().Skip(1).First(gg => gg.Success).Value, CultureInfo.InvariantCulture) : 30;
            if (h < 240 || h > 4320) return Need("Resolution", "type e.g. 1080p60");
            if (fps < 10 || fps > 240) return Need("Frame rate", "10 to 240 fps - e.g. 1080p60");
            double w = h * 16.0 / 9; string c = (codecText ?? "").ToLowerInvariant();
            double codec = c.Contains("av1") ? 0.55 : c.Contains("hevc") || c.Contains("265") || c.Contains("vp9") ? 0.65 : 1.0; string cname = codec == 0.55 ? "AV1" : c.Contains("vp9") ? "VP9" : codec == 0.65 ? "HEVC" : "H.264";
            bool odd = c.Trim() != "" && cname == "H.264" && !c.Contains("264") && !c.Contains("avc");   // FRESH-023: an unknown codec is said, not hidden
            double bpp = 0.055 * Math.Pow(1080.0 / h, 0.25);   // bits per pixel for good live quality in H.264; big frames need a little less per pixel
            double video = w * h * fps * bpp * codec, audio = 160e3, total = video + audio;
            var rows = new List<string[]> {
                R(0, "Bitrate", Bits(total) + " (" + cname + ", video " + Bits(video) + " + audio 160 kbps)", "Pixels × frames × about " + F(bpp, "0.###") + " bits each for H.264, less for HEVC and AV1. Fast games want more; talking heads need less.", "i"),
                R(3, "Upload you need", "at least " + Bits(total * 1.5) + " - 50% headroom so the stream doesn't drop when someone else uploads", "", "i"),
                R(3, "Data", ByteSize(total / 8 * 3600) + " an hour · " + ByteSize(total / 8 * 3600 * 4) + " for a 4-hour stream or recording", "", "i") };
            if (odd) rows.Insert(1, R(1, "Codec", "\"" + (codecText ?? "").Trim() + "\" isn't one I know - shown as H.264 (AV1, HEVC and VP9 need less)", "", "i"));
            double up = Speed(upText);
            if (up == 0) rows.Insert(1, R(2, "Your upload", "none - the stream can't start", "Run the speed test for your real upload.", "i"));   // FRESH-074
            if (!double.IsNaN(up) && up > 0) rows.Insert(1, R(up < total * 1.5 ? (up < total ? 2 : 1) : 0, "Your upload", Bits(up) + (up < total ? " - too slow; drop to " + (h > 720 ? "720p" : "a lower frame rate") : up < total * 1.5 ? " - just about; no headroom" : " - plenty"), "Run the speed test for your real upload.", "i"));
            return rows;
        }

        // ---------- monthly data use: hours a day of video, calls, games
        public static List<string[]> DataUse(string videoText, string callText, string gameText)
        {
            string v = (videoText ?? "").ToLowerInvariant(); double vh = Hours(v), ch = Hours(callText), gh = Hours(gameText);
            if (double.IsNaN(vh) && double.IsNaN(ch) && double.IsNaN(gh)) return Need("Hours", "type hours a day, e.g. 3 h HD");
            foreach (var x in new[] { vh, ch, gh }) if (x < 0 || x > 24) return Need("Hours", "0 to 24 hours a day - e.g. 3 h HD or 30 min");   // FRESH-025
            double vRate = v.Contains("4k") || v.Contains("uhd") ? 7 : v.Contains("sd") ? 1 : 3;   // GB an hour (the big streaming services' own figures)
            double[] gb = { double.IsNaN(vh) ? 0 : vh * vRate * 30.4, double.IsNaN(ch) ? 0 : ch * 1.0 * 30.4, double.IsNaN(gh) ? 0 : gh * 0.1 * 30.4 };
            double total = gb.Sum();
            return new List<string[]> {
                R(0, "A month", F(total, "0") + " GB", "Video " + F(vRate, "0") + " GB an hour, video calls ~1 GB, online games ~0.1 GB (not counting game downloads and updates, often 50-150 GB each).", "i"),
                R(3, "Video", F(gb[0], "0") + " GB", "", "i"), R(3, "Calls", F(gb[1], "0") + " GB", "", "i"), R(3, "Games", F(gb[2], "0") + " GB", "", "i"),
                R(total > 1000 ? 1 : 3, "Plan", total > 1000 ? "over 1 TB - check your plan's cap" : "fits most capped plans above " + F(Math.Ceiling(total / 100) * 100, "0") + " GB", "", "i") };
        }

        // hours a day from "2 h", "30 min", "4K 2 h" or a bare "3" - the 4 of 4K or the 1080 of 1080p is not a time (FRESH-024/075)
        static double Hours(string t)
        {
            t = (t ?? "").ToLowerInvariant(); var m = Regex.Match(t, @"(?<![0-9.,])(-?[0-9]+(?:[.,][0-9]+)?)\s*(hours?|hrs?|h|minutes?|mins?|m)\b");
            if (m.Success) { double x = double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture); return m.Groups[2].Value.StartsWith("m", StringComparison.Ordinal) ? x / 60 : x; }
            return First(Regex.Replace(t, @"\b[0-9]+\s*k\b|\b[0-9]{3,4}\s*p[0-9]*\b", " "));
        }

        // ---------- is my plan fast enough for everyone at once?
        static readonly Dictionary<string, double[]> Uses = new Dictionary<string, double[]> {   // Mbps down, up - what each thing needs to run smoothly
            { "4k", new[] { 25.0, 0.2 } }, { "hd", new[] { 5.0, 0.1 } }, { "sd", new[] { 3.0, 0.1 } }, { "call", new[] { 4.0, 4.0 } }, { "game", new[] { 3.0, 1.0 } },
            { "stream", new[] { 1.0, 8.0 } }, { "work", new[] { 5.0, 2.0 } }, { "download", new[] { 50.0, 1.0 } }, { "browse", new[] { 2.0, 0.5 } } };
        public static List<string[]> Plan(string downText, string upText, string whoText)
        {
            double down = Speed(downText), up = Speed(upText);
            if (double.IsNaN(down) || down <= 0) return Need("Plan download", "type e.g. 300 Mbps");
            var parts = Regex.Matches((whoText ?? "").ToLowerInvariant(), @"\b(\d{1,3})\s*(?:x\s*)?([a-z0-9]+)").Cast<Match>().ToList();
            if (parts.Count == 0) return Need("Busy at once", "type e.g. 2 4k, 1 call, 1 game (also: hd, sd, stream, work, download, browse)");
            double needD = 0, needU = 0; var unknown = new List<string>(); var what = new List<string>();
            foreach (var p in parts)
            {
                int n = int.Parse(p.Groups[1].Value, CultureInfo.InvariantCulture); string k = p.Groups[2].Value.TrimEnd('s'); double[] u;
                if (k == "4" || k == "uhd") k = "4k"; if (k == "video" || k == "movie" || k == "netflix") k = "hd"; if (k == "gamer" || k == "gaming") k = "game"; if (k == "zoom" || k == "team" || k == "meeting") k = "call";
                if (!Uses.TryGetValue(k, out u)) { unknown.Add(p.Groups[2].Value); continue; }
                needD += n * u[0]; needU += n * u[1]; what.Add(n + " " + k);
            }
            if (what.Count == 0) return Need("Busy at once", "use: 4k, hd, sd, call, game, stream, work, download, browse");
            double dM = down / 1e6, uM = double.IsNaN(up) ? double.NaN : up / 1e6;
            int ok = unknown.Count > 0 ? 1 : 0;   // FRESH-076: with something left out, green would be a guess
            var rows = new List<string[]> {
                R(dM >= needD * 1.3 ? ok : dM >= needD ? 1 : 2, "Download", F(needD, "0") + " Mbps needed of your " + F(dM, "0") + (dM >= needD * 1.3 ? " - plenty" : dM >= needD ? " - tight; Wi-Fi usually gets less than the plan" : " - not enough at once"), "Plans are the most you'll get on a cable; Wi-Fi often delivers half to two thirds.", "i") };
            if (!double.IsNaN(uM)) rows.Add(R(uM >= needU * 1.3 ? ok : uM >= needU ? 1 : 2, "Upload", F(needU, "0.#") + " Mbps needed of your " + F(uM, "0.#") + (uM >= needU ? "" : " - calls and streams will break up first"), "", "i"));
            else rows.Add(R(3, "Upload", F(needU, "0.#") + " Mbps needed - type your plan's upload to check it", "", "i"));
            rows.Add(R(unknown.Count > 0 ? 1 : 3, "Counted", string.Join(", ", what) + (unknown.Count > 0 ? "  ·  not known, so not counted: " + string.Join(", ", unknown) : ""), unknown.Count > 0 ? "Known: " + string.Join(", ", Uses.Keys) + "." : "", "i"));
            return rows;
        }

        // ---------- one value in every unit: bits and bytes (decimal and binary), rates, time
        public static List<string[]> Units(string text)
        {
            string t = (text ?? "").Trim(); if (t == "") return Need("Value", "type e.g. 100 Mbps, 12 MB/s, 1 GiB or 90 min");
            var rows = new List<string[]>(); string u; double v = Parse2(t, out u); if (double.IsNaN(v)) return Need("Value", "type a number and a unit, e.g. 100 Mbps");
            if (u.Contains("/") || u.ToLowerInvariant().EndsWith("ps", StringComparison.Ordinal))
            {
                double bps = Speed(t); if (double.IsNaN(bps)) return Need("Value", "type e.g. 100 Mbps or 12 MB/s");
                rows.Add(R(0, "Bits", Bits(bps), "", "i")); rows.Add(R(0, "Bytes", ByteRate(bps), "", "i"));
                rows.Add(R(3, "Binary", F(bps / 8 / 1048576, "0.##") + " MiB/s - what some download managers show", "", "i"));
                rows.Add(R(3, "An hour", ByteSize(bps / 8 * 3600) + " · a day " + ByteSize(bps / 8 * 86400), "", "i"));
            }
            else if (!double.IsNaN(Size(t)) || Regex.IsMatch(u, @"^(Kb|Mb|Gb|Tb|kbit|Mbit|Gbit|bit|bits)$", RegexOptions.IgnoreCase))
            {
                double bytes = Size(t); if (double.IsNaN(bytes)) { double mult = u.StartsWith("T", StringComparison.OrdinalIgnoreCase) ? 1e12 : u.StartsWith("G", StringComparison.OrdinalIgnoreCase) ? 1e9 : u.StartsWith("M", StringComparison.OrdinalIgnoreCase) ? 1e6 : u.StartsWith("K", StringComparison.OrdinalIgnoreCase) ? 1e3 : 1; bytes = v * mult / 8; }
                rows.Add(R(0, "Decimal", ByteSize(bytes) + " (what drive makers and networks use)", "", "i"));
                rows.Add(R(0, "Binary", BinSize(bytes) + " (what Windows shows, though it writes GB)", "", "i"));
                rows.Add(R(3, "Bits", Bits(bytes * 8).Replace("ps", "") + " · " + I((long)Math.Min(bytes * 8, long.MaxValue)) + " bits", "", "i"));
            }
            else
            {
                double s = Time(t); if (double.IsNaN(s)) return Need("Value", "use a size (GB, GiB, Mb), a speed (Mbps, MB/s) or a time (ms, s, min, h)");
                rows.Add(R(0, "Time", Dur(s), "", "i")); rows.Add(R(3, "In seconds", F(s, "0.######") + " s", "", "i")); rows.Add(R(3, "In ms", F(s * 1000, "0.###") + " ms", "", "i"));
            }
            return rows;
        }
        static string BinSize(double b) { return b >= 1099511627776 ? F(b / 1099511627776, "0.##") + " TiB" : b >= 1073741824 ? F(b / 1073741824, "0.##") + " GiB" : b >= 1048576 ? F(b / 1048576, "0.##") + " MiB" : b >= 1024 ? F(b / 1024, "0.##") + " KiB" : F(b, "0") + " bytes"; }

        // ---------- E1 / T1 timeslots
        public static List<string[]> Tdm(string lineText, string slotsText)
        {
            string l = (lineText ?? "").ToLowerInvariant(); bool e1 = l.Contains("e1"), t1 = l.Contains("t1") || l.Contains("ds1");
            if (!e1 && !t1) return Need("Line", "type E1 or T1");
            int max = e1 ? 31 : 24; double n = First(slotsText); int ts = double.IsNaN(n) ? (e1 ? 30 : 24) : Whole(n) ? (int)n : -1;   // FRESH-027: 2.9 slots isn't 2
            if (ts < 1 || ts > max) return Need("Timeslots", "1 to " + max + " on " + (e1 ? "an E1" : "a T1"));
            var rows = new List<string[]> {
                R(0, "Bandwidth", I(ts * 64) + " kbps for " + ts + " timeslot" + (ts == 1 ? "" : "s"), "Each timeslot (DS0) is 8 bits × 8000 frames a second = 64 kbps.", "i"),
                R(3, "The line", e1 ? "2,048 kbps: 32 × 64k - slot 0 is framing, slot 16 usually signalling, 30 for voice" : "1,544 kbps: 24 × 64k + 8k framing bit", "", "i") };
            if (t1) rows.Add(R(3, "Robbed-bit", I(ts * 56) + " kbps if the line steals a bit for signalling (56k per slot)", "", "i"));
            rows.Add(R(3, "Calls", ts + " uncompressed G.711 calls" + (e1 && ts == 31 ? " (if slot 16 isn't signalling)" : ""), "", "i"));
            return rows;
        }

        // ---------- jitter buffer from packet delay variation
        public static List<string[]> JitterBuf(string pdvText, string ptimeText, string codecText)
        {
            var pd = Nums(pdvText); if (pd.Length == 0) return Need("Delay variation", "type e.g. 30 ms, or the measured range 20-70 ms");
            if (pd.Any(x => x < 0)) return Need("Delay variation", "can't be negative - e.g. 30 ms, or 20-70 ms");   // FRESH-028
            double pdv = pd.Length >= 2 ? Math.Abs(pd[1] - pd[0]) : pd[0]; double pt = First(ptimeText);
            if (string.IsNullOrWhiteSpace(ptimeText)) pt = 20; else if (double.IsNaN(pt) || pt <= 0) return Need("Packet time", "more than 0 - usually 20 ms");   // FRESH-051
            double buf = Math.Ceiling(pdv * 1.5 / pt) * pt; if (buf < pt) buf = pt;
            string c = (codecText ?? "").ToLowerInvariant(); double cd = c.Contains("729") ? 25 : c.Contains("opus") ? 26.5 : 1;   // codec + look-ahead delay, ms
            bool known = c.Trim() == "" || c.Contains("729") || c.Contains("opus") || c.Contains("711") || c.Contains("pcm");   // FRESH-052
            double net = pd.Length >= 2 ? Math.Min(pd[0], pd[1]) : 40;
            double m2e = cd + pt + net + buf;
            return new List<string[]> {
                R(0, "Buffer", F(buf, "0") + " ms (" + F(buf / pt, "0") + " packets of " + F(pt, "0") + " ms)", "About 1.5 × the delay variation, rounded up to whole packets - big enough that late packets still play.", "i"),
                R(m2e > 400 ? 2 : m2e > 150 ? 1 : 0, "Mouth to ear", "about " + F(m2e, "0") + " ms" + (m2e > 400 ? " - people will talk over each other" : m2e > 150 ? " - noticeable; ITU G.114 says keep under 150 ms" : " - fine"), "Codec + packet time + network delay" + (pd.Length < 2 ? " (40 ms assumed)" : "") + " + buffer." + (known ? "" : " The delay of " + (codecText ?? "").Trim() + " isn't in my table - 1 ms assumed; most compressed codecs add 20-40 ms."), "i"),
                R(3, "Too small", "packets that arrive later than the buffer are dropped - choppy audio even with 0% network loss", "", "i") };
        }
    }
}
