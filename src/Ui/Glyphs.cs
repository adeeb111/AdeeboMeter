// Big icons (1.1.6, Adeeb 21 Nov: "big icons wherever possible - today everything is small, so it looked cramped").
// One icon set for the app, the Network Lab and the website: SVG path data on a 24 x 24 grid (TopoDraw's set plus our
// own), drawn as strokes with GDI+. The parser takes the path commands these icons use: M L H V C S Q T A Z, absolute
// and relative. A shape that doesn't parse draws nothing - never an exception on a paint.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace AdeeboMeter
{
    static class Glyphs
    {
        static readonly Dictionary<string, string> D = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "board", "M3 4h18v6H3zM3 14h18v6H3zM7 7h.01M7 17h.01M11 7h7M11 17h4" },
            { "bolt", "M13 2L4 14h7l-1 8 9-12h-7z" },
            { "book", "M2 4h7a3 3 0 0 1 3 3v13a2 2 0 0 0-2-2H2zM22 4h-7a3 3 0 0 0-3 3v13a2 2 0 0 1 2-2h8z" },   /* arc flags written out: the path reader does not split "013" into 0 1 3 - the Learn tile drew blank (PC test 21 Nov) */
            { "building", "M4 21V3.5h11V21M15 8.5h5V21M2.5 21h19M7 7h1M11 7h1M7 10.5h1M11 10.5h1M7 14h1M11 14h1M17.5 12h.5M17.5 15.5h.5M8.5 21v-3.5h2V21" },
            { "cabinet", "M6 2.5h12a1 1 0 0 1 1 1v17a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1v-17a1 1 0 0 1 1-1zM5 8.5h14M5 14.5h14M9 5.5h3M9 11.5h3M9 17.5h3M16 11v1" },
            { "calc", "M6 3h12v18H6zM9 7h6M9 11h.01M12 11h.01M15 11h.01M9 15h.01M12 15h.01M15 15h.01" },
            { "camera", "M3 8.5l12-4 2.5 6-12 4zM17.5 10.5l3-1M9 13l1.5 4h-6M4.5 17v4" },
            { "cert", "M5 3h14v12H5zM9 7h6M9 10h4M10 15l-1 6 3-2 3 2-1-6" },
            { "chart", "M3 20h18M6 16l4-5 4 3 5-7" },
            { "clock", "M20.5 12a8.5 8.5 0 1 1-17 0a8.5 8.5 0 1 1 17 0M12 7v5l3.5 2" },
            { "cloud", "M7 18.5h10.5a4 4 0 0 0 .6-7.95A6 6 0 0 0 6.6 9.2 4.7 4.7 0 0 0 7 18.5z" },
            { "cpe", "M3 13h18v6H3zM6.5 16h.01M10 16h.01M17 16h1.5M8 13l-2-6M16 13l2-6" },
            { "database", "M4 6c0-1.7 3.6-3 8-3s8 1.3 8 3-3.6 3-8 3-8-1.3-8-3zM4 6v12c0 1.7 3.6 3 8 3s8-1.3 8-3V6M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3" },
            { "dns", "M4 5h16v5H4zM4 14h16v5H4zM7 7.5h.01M7 16.5h.01M11 7.5h6M11 16.5h6" },
            { "encrypt", "M6 11h12v10H6zM8 11V7a4 4 0 0 1 8 0v4M12 15v2" },
            { "firewall", "M3 5.5h18v14H3zM3 10.2h18M3 14.8h18M8 5.5v4.7M14 5.5v4.7M11 10.2v4.6M17 10.2v4.6M6 10.2v4.6M8 14.8v4.7M14 14.8v4.7" },
            { "game", "M6 12h4M8 10v4M15 11h.01M18 13h.01M2 9a4 4 0 0 1 4-4h12a4 4 0 0 1 4 4v6a4 4 0 0 1-4 4H6a4 4 0 0 1-4-4z" },
            { "house", "M3 11l9-7.5 9 7.5M5.5 9v11.5h13V9M10 20.5v-5h4v5" },
            { "icmp", "M12 3v12M8 11l4 4 4-4M5 21h14" },
            { "internet", "M21 12a9 9 0 1 1-18 0a9 9 0 1 1 18 0M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18M5 7.5h14M5 16.5h14" },
            { "iot", "M14 12a2 2 0 1 1-4 0a2 2 0 1 1 4 0M8.5 8.5a5 5 0 0 0 0 7M15.5 8.5a5 5 0 0 1 0 7M5.6 5.6a9 9 0 0 0 0 12.8M18.4 5.6a9 9 0 0 1 0 12.8" },
            { "key", "M15 7a4 4 0 1 1-3.9 5H3v3h3v-2h2v2h3" },
            { "lab", "M9 3h6M10 3v6l-5 9a2 2 0 0 0 1.8 3h10.4a2 2 0 0 0 1.8-3l-5-9V3" },
            { "laptop", "M5 5h14a1 1 0 0 1 1 1v9H4V6a1 1 0 0 1 1-1zM2 18h20l-1.5 2h-17z" },
            { "loadbal", "M12 3v5M12 8L5 13M12 8l7 5M12 8v5M3 13h4v4H3zM10 13h4v4h-4zM17 13h4v4h-4z" },
            { "lock", "M6 10.5h12v10H6zM8.5 10.5V7a3.5 3.5 0 0 1 7 0v3.5M12 14.5v2.5" },
            { "mail", "M3 6h18v12H3zM3 6l9 7 9-7" },
            { "olt", "M3 4h18v6H3zM3 14h18v6H3zM6 7h.01M9 7h.01M6 17h.01M9 17h.01M13 6.5h5M13 16.5h5M12 10v4" },
            { "ont", "M4 9h16v8H4zM7 13h.01M10 13h.01M14 12.5h3v1h-3M8 9V6.5M16 9V6M7 20.5h10" },
            { "pc", "M4.5 4h15a1.5 1.5 0 0 1 1.5 1.5v9a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 14.5v-9A1.5 1.5 0 0 1 4.5 4zM9 20h6M12 16v4" },
            { "phone", "M8 2.5h8a1.5 1.5 0 0 1 1.5 1.5v16a1.5 1.5 0 0 1-1.5 1.5H8A1.5 1.5 0 0 1 6.5 20V4A1.5 1.5 0 0 1 8 2.5zM11 18.5h2" },
            { "pkt", "M4 6h16v12H4zM4 10h16M9 6v12" },
            { "play", "M5 4h14a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2zM10 8l5 3-5 3zM8 21h8" },
            { "port", "M4 8h16v8H4zM8 12h.01M12 12h.01M16 12h.01" },
            { "rack", "M5 2.5h14v19H5zM7.5 6h9M7.5 9h9M7.5 12h9M7.5 15h9M7.5 18h9M15 6h.01M15 9h.01M15 12h.01" },
            { "roadside", "M7 3h10a1 1 0 0 1 1 1v9H6V4a1 1 0 0 1 1-1zM6 7.5h12M10 10h4M8 13v4.5M16 13v4.5M1.5 19h21M3 22h3M10.5 22h3M18 22h3" },
            { "route", "M5 19a2 2 0 1 0 0-4a2 2 0 0 0 0 4M19 9a2 2 0 1 0 0-4a2 2 0 0 0 0 4M7 17h6a3 3 0 0 0 3-3v-1a3 3 0 0 1 3-3" },
            { "send", "M4 12l16-8-6 16-2-7z" },
            { "server", "M4.5 3.5h15a1 1 0 0 1 1 1v5a1 1 0 0 1-1 1h-15a1 1 0 0 1-1-1v-5a1 1 0 0 1 1-1zM4.5 13.5h15a1 1 0 0 1 1 1v5a1 1 0 0 1-1 1h-15a1 1 0 0 1-1-1v-5a1 1 0 0 1 1-1zM7 7h.01M7 17h.01M11 7h6M11 17h6" },
            { "shield", "M12 3l8 3v6c0 4.5-3.4 8-8 9-4.6-1-8-4.5-8-9V6z" },
            { "splitter", "M2.5 12h7M9.5 12l11-6.5M9.5 12h11M9.5 12l11 6.5M9.5 9.5v5" },
            { "star", "M12 3l2.8 5.7 6.2.9-4.5 4.4 1 6.2L12 17.3 6.5 20.2l1-6.2L3 9.6l6.2-.9z" },
            { "switch", "M2.5 9h19v6h-19zM6 12h.01M9 12h.01M12 12h.01M15 12h.01M8 3l-3 2 3 2M5 5h7M16 17l3 2-3 2M19 19h-7" },
            { "tool", "M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18l3 3 6.3-6.3a4 4 0 0 0 5.4-5.4l-2.5 2.5-2.4-.6-.6-2.4z" },
            { "tower", "M12 9v12M8.5 21L12 9l3.5 12M9.6 17h4.8M13.4 7.5a1.4 1.4 0 1 1-2.8 0a1.4 1.4 0 1 1 2.8 0M8.2 4.2a5.5 5.5 0 0 0 0 6.6M15.8 4.2a5.5 5.5 0 0 1 0 6.6M5.6 2.2a9 9 0 0 0 0 10.6M18.4 2.2a9 9 0 0 1 0 10.6" },
            { "users", "M12.5 7.5a3.5 3.5 0 1 1-7 0a3.5 3.5 0 1 1 7 0M2.5 20c0-3.6 2.9-6 6.5-6s6.5 2.4 6.5 6M16 4.3a3.2 3.2 0 0 1 0 6.2M18 14.3c2.2.6 3.5 2.6 3.5 5.7" },
            { "wifi", "M2.5 9a14 14 0 0 1 19 0M5.5 12.5a9.5 9.5 0 0 1 13 0M8.6 15.8a5 5 0 0 1 6.8 0M13.2 19a1.2 1.2 0 1 1-2.4 0a1.2 1.2 0 1 1 2.4 0" },
            { "win", "M3 5.5l8-1.1v7.1H3zM12 4.2L21 3v8.5h-9zM3 12.5h8v7.1l-8-1.1zM12 12.5h9V21l-9-1.2z" },
        };
        static readonly Dictionary<string, GraphicsPath> cache = new Dictionary<string, GraphicsPath>(StringComparer.Ordinal);
        public static bool Has(string key) { return key != null && D.ContainsKey(key); }
        public static IEnumerable<string> Keys { get { return D.Keys; } }

        // the icon in rect r (square), drawn in colour c; weight is the stroke on the 24 grid (1.6 looks like the lab)
        public static void Draw(Graphics g, string key, RectangleF r, Color c, float weight = 1.6f)
        {
            var p = PathOf(key); if (p == null || r.Width <= 0) return;
            float s = Math.Min(r.Width, r.Height) / 24f;
            var keep = g.Transform; var sm = g.SmoothingMode;
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(r.X + (r.Width - 24 * s) / 2, r.Y + (r.Height - 24 * s) / 2); g.ScaleTransform(s, s);
                using (var pen = new Pen(c, weight) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawPath(pen, p);
            }
            finally { g.Transform = keep; g.SmoothingMode = sm; }
        }
        // a big icon on a soft tile of its own colour (Home's gateway, the Toolbox topics)
        public static void Tile(Graphics g, string key, RectangleF r, Color c, float radius)
        {
            Theme.Fill(g, r, radius, Color.FromArgb(Theme.Dark ? 46 : 26, c));
            float pad = r.Width * 0.2f; Draw(g, key, new RectangleF(r.X + pad, r.Y + pad, r.Width - 2 * pad, r.Height - 2 * pad), c, 1.7f);
        }
        static GraphicsPath PathOf(string key)
        {
            if (key == null) return null;
            lock (cache)
            {
                GraphicsPath p; if (cache.TryGetValue(key, out p)) return p;
                string d; if (!D.TryGetValue(key, out d)) return null;
                try { p = Parse(d); } catch (FormatException) { p = null; } catch (IndexOutOfRangeException) { p = null; } catch (ArgumentException) { p = null; }
                cache[key] = p; return p;
            }
        }

        // ---------- SVG path data -> GraphicsPath (subpaths kept open unless Z)
        internal static GraphicsPath Parse(string d)
        {
            var toks = Tokens(d); var gp = new GraphicsPath(); int i = 0; char cmd = 'M';
            float x = 0, y = 0, sx = 0, sy = 0, cx = 0, cy = 0; char last = ' '; bool open = false;
            Func<float> N = () => { if (i >= toks.Count || toks[i] is char) throw new FormatException("number expected"); return (float)toks[i++]; };
            while (i < toks.Count)
            {
                if (toks[i] is char) cmd = (char)toks[i++]; else if (cmd == 'M') cmd = 'L'; else if (cmd == 'm') cmd = 'l';
                bool rel = char.IsLower(cmd); char C = char.ToUpperInvariant(cmd);
                float ox = rel ? x : 0, oy = rel ? y : 0;
                switch (C)
                {
                    case 'M': { x = ox + N(); y = oy + N(); gp.StartFigure(); sx = x; sy = y; open = true; break; }
                    case 'L': { float nx = ox + N(), ny = oy + N(); Line(gp, x, y, nx, ny); x = nx; y = ny; break; }
                    case 'H': { float nx = ox + N(); Line(gp, x, y, nx, y); x = nx; break; }
                    case 'V': { float ny = oy + N(); Line(gp, x, y, x, ny); y = ny; break; }
                    case 'C': { float x1 = ox + N(), y1 = oy + N(), x2 = ox + N(), y2 = oy + N(), nx = ox + N(), ny = oy + N(); gp.AddBezier(x, y, x1, y1, x2, y2, nx, ny); cx = x2; cy = y2; x = nx; y = ny; break; }
                    case 'S': { float x1 = "CS".IndexOf(char.ToUpperInvariant(last)) >= 0 ? 2 * x - cx : x, y1 = "CS".IndexOf(char.ToUpperInvariant(last)) >= 0 ? 2 * y - cy : y;
                                float x2 = ox + N(), y2 = oy + N(), nx = ox + N(), ny = oy + N(); gp.AddBezier(x, y, x1, y1, x2, y2, nx, ny); cx = x2; cy = y2; x = nx; y = ny; break; }
                    case 'Q': { float qx = ox + N(), qy = oy + N(), nx = ox + N(), ny = oy + N(); Quad(gp, x, y, qx, qy, nx, ny); cx = qx; cy = qy; x = nx; y = ny; break; }
                    case 'T': { float qx = "QT".IndexOf(char.ToUpperInvariant(last)) >= 0 ? 2 * x - cx : x, qy = "QT".IndexOf(char.ToUpperInvariant(last)) >= 0 ? 2 * y - cy : y;
                                float nx = ox + N(), ny = oy + N(); Quad(gp, x, y, qx, qy, nx, ny); cx = qx; cy = qy; x = nx; y = ny; break; }
                    case 'A': { float rx = N(), ry = N(), rot = N(); bool large = N() != 0, sweep = N() != 0; float nx = ox + N(), ny = oy + N(); Arc(gp, x, y, rx, ry, rot, large, sweep, nx, ny); x = nx; y = ny; break; }
                    case 'Z': { if (open) { gp.CloseFigure(); open = false; } x = sx; y = sy; break; }
                    default: throw new FormatException("command " + cmd);
                }
                last = cmd;
            }
            return gp;
        }
        // a zero-length line ("h.01", a dot) still has to show: a tiny segment with round caps does
        static void Line(GraphicsPath gp, float x1, float y1, float x2, float y2) { if (x1 == x2 && y1 == y2) x2 += 0.01f; gp.AddLine(x1, y1, x2, y2); }
        static void Quad(GraphicsPath gp, float x0, float y0, float qx, float qy, float x, float y)
        { gp.AddBezier(x0, y0, x0 + 2f / 3 * (qx - x0), y0 + 2f / 3 * (qy - y0), x + 2f / 3 * (qx - x), y + 2f / 3 * (qy - y), x, y); }
        // the SVG elliptical arc (endpoint form) as Beziers - the W3C "implementation notes" conversion
        static void Arc(GraphicsPath gp, float x1, float y1, float rx, float ry, float rotDeg, bool large, bool sweep, float x2, float y2)
        {
            if (rx == 0 || ry == 0 || (x1 == x2 && y1 == y2)) { Line(gp, x1, y1, x2, y2); return; }
            double phi = rotDeg * Math.PI / 180, cp = Math.Cos(phi), sp = Math.Sin(phi);
            double dx = (x1 - x2) / 2.0, dy = (y1 - y2) / 2.0, x1p = cp * dx + sp * dy, y1p = -sp * dx + cp * dy;
            double arx = Math.Abs(rx), ary = Math.Abs(ry), lam = x1p * x1p / (arx * arx) + y1p * y1p / (ary * ary); if (lam > 1) { arx *= Math.Sqrt(lam); ary *= Math.Sqrt(lam); }
            double num = arx * arx * ary * ary - arx * arx * y1p * y1p - ary * ary * x1p * x1p, den = arx * arx * y1p * y1p + ary * ary * x1p * x1p;
            double co = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, num / den)), cxp = co * arx * y1p / ary, cyp = -co * ary * x1p / arx;
            double cx = cp * cxp - sp * cyp + (x1 + x2) / 2.0, cy = sp * cxp + cp * cyp + (y1 + y2) / 2.0;
            Func<double, double, double, double, double> ang = (ux, uy, vx, vy) => { double a = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy); return a; };
            double t1 = ang(1, 0, (x1p - cxp) / arx, (y1p - cyp) / ary), dt = ang((x1p - cxp) / arx, (y1p - cyp) / ary, (-x1p - cxp) / arx, (-y1p - cyp) / ary);
            if (!sweep && dt > 0) dt -= 2 * Math.PI; else if (sweep && dt < 0) dt += 2 * Math.PI;
            int segs = (int)Math.Ceiling(Math.Abs(dt) / (Math.PI / 2)); double step = dt / segs, k = 4.0 / 3 * Math.Tan(step / 4);
            double px = x1, py = y1;
            for (int s = 0; s < segs; s++)
            {
                double a1 = t1 + s * step, a2 = a1 + step, c1 = Math.Cos(a1), s1 = Math.Sin(a1), c2 = Math.Cos(a2), s2 = Math.Sin(a2);
                Func<double, double, PointF> P = (ex, ey) => new PointF((float)(cx + cp * arx * ex - sp * ary * ey), (float)(cy + sp * arx * ex + cp * ary * ey));
                var q1 = P(c1 - k * s1, s1 + k * c1); var q2 = P(c2 + k * s2, s2 - k * c2); var e = P(c2, s2);
                gp.AddBezier(new PointF((float)px, (float)py), q1, q2, e); px = e.X; py = e.Y;
            }
        }
        static List<object> Tokens(string d)
        {
            var o = new List<object>(); int i = 0;
            while (i < d.Length)
            {
                char c = d[i];
                if (char.IsLetter(c) && c != 'e' && c != 'E') { o.Add(c); i++; continue; }
                if (c == ',' || char.IsWhiteSpace(c)) { i++; continue; }
                int st = i; if (c == '-' || c == '+') i++;
                bool dot = false, exp = false;
                while (i < d.Length)
                {
                    char k = d[i];
                    if (char.IsDigit(k)) { i++; continue; }
                    if (k == '.' && !dot && !exp) { dot = true; i++; continue; }
                    if ((k == 'e' || k == 'E') && !exp) { exp = true; i++; if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++; continue; }
                    break;
                }
                if (i == st) throw new FormatException("bad path at " + st);
                o.Add(float.Parse(d.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture));
            }
            return o;
        }
    }
}
