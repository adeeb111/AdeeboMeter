// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AdeeboMeter
{
    // ================================================================== the Adeebo Award card and the Hall of Fame
    // At 500 Adeebo Points you can make a picture - your name, your points, the date and a claim code - and send it to
    // the Hall of Fame (the GitHub form, or by email). Nothing is sent by AdeeboMeter: it only saves a picture you share
    // yourself.
    //
    // The claim code is HMAC-SHA256 over "v1|name|points|date" with a key that is NOT in the public source: the official
    // builds get it from a GitHub secret at build time (obj/HofKey.cs, written by release.ps1). The Hall of Fame's check
    // (.github/scripts/hof_verify.py) computes the same code with the same secret; only a match is shown. A build from
    // the source has no key, so its card says "unofficial build" and carries no code.
    //
    // Honest limit: the key has to ride inside the official exe, and points live in a file on the PC - so a determined
    // person could still forge a card. The code stops the easy fakes (an edited picture, someone else's card with a new
    // name), and every entry in the Hall of Fame can be removed by hand.
    static partial class HallOfFame
    {
        public const string Page = "https://adeeb111.github.io/AdeeboMeter/hall-of-fame.html";
        public const string Form = "https://github.com/adeeb111/AdeeboMeter/issues/new?template=hall-of-fame.yml";

        static partial void BuildKey(ref string key);   // only the official build has a body for this (obj/HofKey.cs)
        public static string Key { get { string k = null; BuildKey(ref k); return k ?? ""; } }
        public static bool Official { get { return Key != ""; } }
        // the generated file holds the key masked, so it isn't a readable string in the exe (a speed bump, not a lock)
        internal static string Unmask(byte[] b) { var o = new byte[b.Length]; for (int i = 0; i < b.Length; i++) o[i] = (byte)(b[i] ^ ((i * 73 + 41) & 0xFF)); return Encoding.UTF8.GetString(o); }

        // a name fit for a public page: letters and their accents/vowel signs (any script, also outside the BMP), digits,
        // space and . - _ ' ; NFC first, single spaces, at most 32 characters. hof_verify.py's clean_name is the same rule.
        public static string CleanName(string n)
        {
            string src; try { src = (n ?? "").Trim().Normalize(NormalizationForm.FormC); } catch (ArgumentException) { src = (n ?? "").Trim(); }
            var parts = new List<string>(); bool sp = false; int marks = 0;   /* accent marks on the current letter: at most 2, so "Z" plus 30 marks can't draw over the card */
            for (int i = 0; i < src.Length; i++)
            {
                bool pair = char.IsSurrogatePair(src, i); string cp = pair ? src.Substring(i, 2) : src.Substring(i, 1);
                if (pair) i++;
                if (cp.Length == 1 && char.IsWhiteSpace(cp[0])) { if (!sp && parts.Count > 0) parts.Add(" "); sp = true; continue; }
                var cat = CharUnicodeInfo.GetUnicodeCategory(cp, 0);
                bool ok = cat == UnicodeCategory.UppercaseLetter || cat == UnicodeCategory.LowercaseLetter || cat == UnicodeCategory.TitlecaseLetter || cat == UnicodeCategory.ModifierLetter || cat == UnicodeCategory.OtherLetter
                       || cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark || cat == UnicodeCategory.DecimalDigitNumber || cp == "." || cp == "-" || cp == "_" || cp == "'";
                bool mark = cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark;
                if (ok && mark && (parts.Count == 0 || parts[parts.Count - 1] == " " || marks >= 2)) continue;   /* a mark with no letter under it, or a third one */
                if (ok) { parts.Add(cp); sp = false; marks = mark ? marks + 1 : 0; }
            }
            while (parts.Count > 0 && parts[parts.Count - 1] == " ") parts.RemoveAt(parts.Count - 1);
            if (parts.Count > 32) { parts = parts.Take(32).ToList(); while (parts.Count > 0 && parts[parts.Count - 1] == " ") parts.RemoveAt(parts.Count - 1); }
            return string.Concat(parts);
        }
        /* A word that mixes Latin letters with Cyrillic or Greek ones ("Аdeeb" with a Cyrillic А) is a look-alike of
           someone else's name. Each alphabet alone is fine. hof_verify.py refuses the same thing. */
        static int Script(int c)
        {
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= 0xC0 && c <= 0x24F && c != 0xD7 && c != 0xF7) || (c >= 0x1E00 && c <= 0x1EFF)) return 1;
            if (c >= 0x400 && c <= 0x52F) return 2;
            if ((c >= 0x370 && c <= 0x3FF) || (c >= 0x1F00 && c <= 0x1FFF)) return 3;
            return 0;
        }
        public static bool MixedScript(string name)
        {
            foreach (var w in (name ?? "").Split(' '))
            {
                int seen = 0;
                foreach (char ch in w) { int s = Script(ch); if (s == 0) continue; if (seen != 0 && s != seen) return true; seen = s; }
            }
            return false;
        }
        public static string Date(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        // ADB-XXXX-XXXX-XXXX-XXXX: the first 80 bits of the HMAC in base32 (no 0/1/8/9 mix-ups: the RFC 4648 alphabet)
        public static string Code(string key, string name, int total, string date)
        {
            if (string.IsNullOrEmpty(key)) return "";
            byte[] mac; using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(key))) mac = h.ComputeHash(Encoding.UTF8.GetBytes("v1|" + CleanName(name) + "|" + total.ToString(CultureInfo.InvariantCulture) + "|" + date));
            var b32 = Base32(mac).Substring(0, 16);
            return "ADB-" + b32.Substring(0, 4) + "-" + b32.Substring(4, 4) + "-" + b32.Substring(8, 4) + "-" + b32.Substring(12, 4);
        }
        static string Base32(byte[] b)
        {
            const string A = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; var sb = new StringBuilder(); int buf = 0, bits = 0;
            foreach (var x in b) { buf = (buf << 8) | x; bits += 8; while (bits >= 5) { sb.Append(A[(buf >> (bits - 5)) & 31]); bits -= 5; } buf &= (1 << bits) - 1; }
            if (bits > 0) sb.Append(A[(buf << (5 - bits)) & 31]);
            return sb.ToString();
        }
        // the text to paste in an email or the form
        public static string Text(string name, int total, string date, string code)
        {
            return "Adeebo Award - AdeeboMeter Hall of Fame\r\nName: " + CleanName(name) + "\r\nPoints: " + total.ToString(CultureInfo.InvariantCulture) + "\r\nDate: " + date + "\r\nClaim code: " + (code != "" ? code : "(unofficial build - no code)") + "\r\n";
        }

        public sealed class Stats { public int Tools, ToolsAll, Topics, TopicsAll, Checks, Labs, LabsAll; public string[] Paths = Array.Empty<string>(); }

        // the card: 1200 x 675, readable in a photo of a screen as well as a saved picture
        public static Bitmap Card(string name, int total, string date, string code, Stats st)
        {
            const int W = 1200, H = 675; var bmp = new Bitmap(W, H);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (var bg = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.FromArgb(14, 33, 74), Color.FromArgb(31, 111, 235), 35f)) g.FillRectangle(bg, 0, 0, W, H);
                // a soft gold glow behind the star
                using (var path = new GraphicsPath()) { path.AddEllipse(820, -120, 520, 520); using (var pg = new PathGradientBrush(path) { CenterColor = Color.FromArgb(90, 242, 204, 96), SurroundColors = new[] { Color.FromArgb(0, 242, 204, 96) } }) g.FillEllipse(pg, 820, -120, 520, 520); }
                Star(g, 1080, 140, 92, Color.FromArgb(242, 204, 96));
                Mark(g, 60, 52, 56);
                var white = Brushes.White; var gold = new SolidBrush(Color.FromArgb(242, 204, 96)); var soft = new SolidBrush(Color.FromArgb(200, 255, 255, 255)); var faint = new SolidBrush(Color.FromArgb(150, 255, 255, 255));
                using (var f1 = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Pixel)) g.DrawString("AdeeboMeter", f1, white, 128, 64);
                using (var fk = new Font("Segoe UI", 22f, FontStyle.Bold, GraphicsUnit.Pixel)) g.DrawString("ADEEBO AWARD", fk, gold, 60, 150);
                string nm = CleanName(name); if (nm == "") nm = "A network detective";
                using (var fn = Fit(g, ref nm, 880, 76)) g.DrawString(nm, fn, white, 54, 182);
                using (var fp = new Font("Segoe UI", 30f, FontStyle.Regular, GraphicsUnit.Pixel)) g.DrawString(total.ToString(CultureInfo.InvariantCulture) + " Adeebo Points  ·  " + date, fp, soft, 60, 290);
                // what it took
                float y = 370;
                using (var fs = new Font("Segoe UI", 24f, FontStyle.Regular, GraphicsUnit.Pixel)) using (var fsb = new Font("Segoe UI", 24f, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    if (st != null)
                    {
                        string[][] lines = {
                            new[] { st.Tools + " of " + st.ToolsAll, "tools run for real" },
                            new[] { st.Topics + " of " + st.TopicsAll, "Learn topics read  ·  " + st.Checks + " check questions right" },
                            new[] { st.Labs + " of " + st.LabsAll, "lab challenges met" },
                            new[] { st.Paths.Length == 0 ? "-" : string.Join("  ·  ", st.Paths), "learning paths finished" } };
                        foreach (var l in lines)
                        {
                            g.DrawString(l[0], fsb, white, 60, y); var w0 = g.MeasureString(l[0], fsb).Width;
                            g.DrawString(l[1], fs, faint, 60 + w0 + 6, y); y += 38;
                        }
                    }
                }
                // the claim code
                var box = new RectangleF(60, H - 118, W - 120, 72);
                using (var bb = new SolidBrush(Color.FromArgb(46, 0, 0, 0))) using (var gp = Round(box, 16)) g.FillPath(bb, gp);
                using (var fl = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel)) g.DrawString(code != "" ? "CLAIM CODE" : "UNOFFICIAL BUILD", fl, faint, box.X + 22, box.Y + 12);
                using (var fc = new Font("Consolas", 32f, FontStyle.Bold, GraphicsUnit.Pixel)) using (var fu = new Font("Segoe UI", 20f, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    if (code != "") g.DrawString(code, fc, gold, box.X + 20, box.Y + 30);
                    else g.DrawString("built from the source - this card can't be verified", fu, soft, box.X + 22, box.Y + 36);
                }
                using (var fw = new Font("Segoe UI", 18f, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    const string site = "adeeb111.github.io/AdeeboMeter/hall-of-fame"; var sw = g.MeasureString(site, fw).Width;
                    g.DrawString("Send it to the Hall of Fame", fw, faint, box.Right - sw - 22, box.Y + 12); g.DrawString(site, fw, soft, box.Right - sw - 22, box.Y + 38);
                }
                gold.Dispose(); soft.Dispose(); faint.Dispose();
            }
            return bmp;
        }
        static Font Fit(Graphics g, ref string s, float maxW, float size)
        {
            for (float z = size; ; z -= 4)
            {
                var f = new Font("Segoe UI", z, FontStyle.Bold, GraphicsUnit.Pixel);
                if (g.MeasureString(s, f).Width <= maxW) return f;
                if (z <= 18) return f;   // the whole name, always - smaller rather than cut: the claim is typed from the picture (32 characters fit at 16 px)
                f.Dispose();
            }
        }
        static void Star(Graphics g, float cx, float cy, float r, Color c)
        {
            var pts = Enumerable.Range(0, 10).Select(i => { double a = -Math.PI / 2 + i * Math.PI / 5; float rr = i % 2 == 0 ? r : r * 0.42f; return new PointF(cx + (float)(Math.Cos(a) * rr), cy + (float)(Math.Sin(a) * rr)); }).ToArray();
            using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
        }
        // the AdeeboMeter mark, drawn (no image files): the blue tile and the three-colour gauge
        static void Mark(Graphics g, float x, float y, float s)
        {
            float k = s / 64f;
            using (var gp = Round(new RectangleF(x + 2 * k, y + 2 * k, 60 * k, 60 * k), 14 * k)) using (var b = new SolidBrush(Color.FromArgb(31, 111, 235))) g.FillPath(b, gp);
            using (var w = new Pen(Color.FromArgb(120, 255, 255, 255), 1.5f)) using (var gp = Round(new RectangleF(x + 2 * k, y + 2 * k, 60 * k, 60 * k), 14 * k)) g.DrawPath(w, gp);
            var arc = new RectangleF(x + 12 * k, y + 20 * k, 40 * k, 40 * k);
            using (var p1 = new Pen(Color.FromArgb(63, 185, 80), 7 * k)) g.DrawArc(p1, arc, 180, 60);
            using (var p2 = new Pen(Color.FromArgb(242, 204, 96), 7 * k)) g.DrawArc(p2, arc, 240, 60);
            using (var p3 = new Pen(Color.FromArgb(255, 123, 114), 7 * k)) g.DrawArc(p3, arc, 300, 60);
            using (var n = new Pen(Color.White, 5 * k) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(n, x + 32 * k, y + 40 * k, x + 43 * k, y + 27 * k);
            g.FillEllipse(Brushes.White, x + 27 * k, y + 35 * k, 10 * k, 10 * k);
        }
        static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath(); float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
            return p;
        }
    }
}
