// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // Gamer Zone › ping map: every server of a game on a world map, coloured by your ping (the pings the Gamer Zone already
    // sends - nothing new). Hollow dots are region estimates (the cloud region the game runs in), solid ones real servers.
    sealed partial class FullView
    {
        // where the place names of the built-in and Valve lists are (latitude, longitude) - rough, for a picture
        static readonly Dictionary<string, float[]> Places = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Virginia", new[] { 38.9f, -77.4f } }, { "Ohio", new[] { 40.0f, -83.0f } }, { "Oregon", new[] { 45.8f, -119.7f } }, { "California", new[] { 37.4f, -121.9f } },
            { "Montreal", new[] { 45.5f, -73.6f } }, { "Toronto", new[] { 43.7f, -79.4f } }, { "Chicago", new[] { 41.9f, -87.6f } }, { "Atlanta", new[] { 33.7f, -84.4f } },
            { "Dallas", new[] { 32.8f, -96.8f } }, { "Seattle", new[] { 47.6f, -122.3f } }, { "Los Angeles", new[] { 34.0f, -118.2f } }, { "New York", new[] { 40.7f, -74.0f } },
            { "Washington", new[] { 38.9f, -77.0f } }, { "Sterling", new[] { 39.0f, -77.4f } }, { "Moses Lake", new[] { 47.1f, -119.3f } },
            { "Sao Paulo", new[] { -23.5f, -46.6f } }, { "Lima", new[] { -12.0f, -77.0f } }, { "Santiago", new[] { -33.4f, -70.6f } }, { "Buenos Aires", new[] { -34.6f, -58.4f } },
            { "London", new[] { 51.5f, -0.1f } }, { "Frankfurt", new[] { 50.1f, 8.7f } }, { "Paris", new[] { 48.9f, 2.4f } }, { "Amsterdam", new[] { 52.4f, 4.9f } },
            { "Stockholm", new[] { 59.3f, 18.1f } }, { "Vienna", new[] { 48.2f, 16.4f } }, { "Madrid", new[] { 40.4f, -3.7f } }, { "Warsaw", new[] { 52.2f, 21.0f } },
            { "Helsinki", new[] { 60.2f, 24.9f } }, { "Milan", new[] { 45.5f, 9.2f } }, { "Dublin", new[] { 53.3f, -6.3f } }, { "Ireland", new[] { 53.3f, -6.3f } }, { "Luxembourg", new[] { 49.6f, 6.1f } },
            { "Istanbul", new[] { 41.0f, 29.0f } }, { "Bahrain", new[] { 26.1f, 50.6f } }, { "Dubai", new[] { 25.2f, 55.3f } }, { "Johannesburg", new[] { -26.2f, 28.0f } }, { "Cape Town", new[] { -33.9f, 18.4f } },
            { "Mumbai", new[] { 19.1f, 72.9f } }, { "Chennai", new[] { 13.1f, 80.3f } }, { "Singapore", new[] { 1.3f, 103.8f } }, { "Hong Kong", new[] { 22.3f, 114.2f } },
            { "Tokyo", new[] { 35.7f, 139.7f } }, { "Osaka", new[] { 34.7f, 135.5f } }, { "Seoul", new[] { 37.6f, 127.0f } }, { "Shanghai", new[] { 31.2f, 121.5f } }, { "Guangzhou", new[] { 23.1f, 113.3f } },
            { "Manila", new[] { 14.6f, 121.0f } }, { "Jakarta", new[] { -6.2f, 106.8f } }, { "Sydney", new[] { -33.9f, 151.2f } }, { "Melbourne", new[] { -37.8f, 145.0f } }, { "Auckland", new[] { -36.8f, 174.8f } }
        };
        // very rough continents (longitude, latitude) - enough to see east from west, not a real map
        static readonly float[][] Lands = {
            new float[] { -168, 66, -140, 70, -95, 72, -80, 63, -60, 55, -53, 47, -67, 44, -76, 35, -81, 25, -97, 26, -97, 18, -87, 16, -78, 8, -84, 9, -105, 20, -112, 31, -124, 40, -124, 49, -135, 58, -152, 60, -165, 60 },
            new float[] { -78, 8, -60, 10, -50, 0, -35, -6, -40, -22, -48, -27, -57, -36, -66, -46, -70, -54, -75, -48, -73, -37, -71, -18, -81, -5 },
            new float[] { -10, 36, -9, 43, -2, 44, -5, 48, 2, 51, 5, 58, 11, 59, 20, 70, 30, 70, 40, 66, 40, 45, 28, 41, 23, 38, 14, 40, 10, 44, 3, 43, -1, 37 },
            new float[] { -17, 21, -10, 35, 10, 37, 32, 31, 35, 28, 43, 12, 51, 12, 40, -15, 33, -27, 20, -35, 12, -18, 9, 4, -8, 5 },
            new float[] { 40, 66, 70, 73, 110, 76, 140, 72, 180, 68, 160, 60, 142, 46, 130, 42, 122, 30, 120, 22, 108, 21, 105, 10, 103, 1, 98, 8, 92, 22, 80, 15, 77, 8, 72, 21, 62, 25, 57, 24, 51, 25, 48, 30, 35, 32, 36, 36, 28, 41, 40, 45 },
            new float[] { 114, -22, 122, -18, 130, -12, 137, -12, 142, -11, 146, -19, 153, -25, 151, -34, 146, -39, 138, -35, 130, -32, 115, -34 },
            new float[] { 130, 31, 135, 34, 140, 36, 141, 41, 142, 45, 140, 41, 136, 36, 131, 34 },
            new float[] { -8, 50, -5, 58, -2, 59, 2, 53, 1, 51 } };

        float PaintPingMap(Graphics g, Game gm, float x, float y, float w)
        {
            if (gm == null || gm.Servers.Count == 0) return y;
            y = SetHead(g, L.T("PING MAP") + " · " + gm.Name.ToUpperInvariant(), x, y, w);
            float mw = Math.Min(w, 760 * S), mh = mw / 2.2f; var map = new RectangleF(x, y, mw, mh);
            Theme.Fill(g, map, 12 * S, Theme.Soft);
            Func<float, float, PointF> P = (lon, lat) => new PointF(map.X + (lon + 180) / 360f * map.Width, map.Y + (78 - lat) / 140f * map.Height);   // 78N to 62S
            var old = g.Clip; g.SetClip(map);
            using (var land = new SolidBrush(Theme.Dark ? Color.FromArgb(48, 255, 255, 255) : Color.FromArgb(24, 0, 0, 0)))
                foreach (var l in Lands) { var pts = new PointF[l.Length / 2]; for (int i = 0; i < pts.Length; i++) pts[i] = P(l[2 * i], l[2 * i + 1]); g.FillPolygon(land, pts); }
            g.Clip = old;
            var gp = GamePing.Me; var best = gp.Best(gm); int off = 0; var missing = new List<string>();
            using (var muted = new SolidBrush(Theme.Muted)) using (var ink = new SolidBrush(Theme.Ink))
            {
                foreach (var s in gm.Servers)
                {
                    float[] ll = null; foreach (var kv in Places) if ((s.Where ?? "").IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0 || (s.Name ?? "").IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) { ll = kv.Value; break; }
                    if (ll == null) { missing.Add(s.Name); continue; }
                    var v = gp.Stat(gm.Id, s).Window(60, 3600); double a = GameStat.Avg(v);
                    Color c = double.IsNaN(a) ? Theme.Muted : a < 50 ? Theme.Green : a < 100 ? Theme.Amber : Theme.Red;
                    var pt = P(ll[1], ll[0]); float r = (best == s ? 9 : 7) * S;
                    using (var b = new SolidBrush(c)) using (var pen = new Pen(c, 2.5f * S))
                    {
                        if (s.Kind == "estimate") { using (var halo = new SolidBrush(Color.FromArgb(60, c))) g.FillEllipse(halo, pt.X - r, pt.Y - r, 2 * r, 2 * r); g.DrawEllipse(pen, pt.X - r, pt.Y - r, 2 * r, 2 * r); }
                        else g.FillEllipse(b, pt.X - r, pt.Y - r, 2 * r, 2 * r);
                    }
                    string lab = (s.Name ?? "") + " " + (double.IsNaN(a) ? "-" : Math.Round(a).ToString(CultureInfo.CurrentCulture) + " ms");
                    var ls = g.MeasureString(lab, Draw.fTiny); float lx = Math.Min(map.Right - ls.Width - 4 * S, Math.Max(map.X + 4 * S, pt.X - ls.Width / 2)), ly = pt.Y + r + 2 * S + (off++ % 2) * 12 * S;
                    if (ly + ls.Height > map.Bottom) ly = pt.Y - r - ls.Height - 2 * S;
                    g.DrawString(lab, Draw.fTiny, best == s ? ink : muted, lx, ly);
                    Hint(gamerCanvas, new RectangleF(pt.X - r, pt.Y - r, 2 * r, 2 * r), "gz.map");
                }
                y += mh + 8 * S;
                string legend = L.T("green under 50 ms · amber under 100 · red above · grey: no ping yet · ring = a region estimate (the cloud region the game runs in), dot = a real server") + (missing.Count > 0 ? "  ·  " + L.T("not on the map") + ": " + string.Join(", ", missing.Take(4)) + (missing.Count > 4 ? " " + L.T("and") + " " + (missing.Count - 4) + " " + L.T("more") : "") : "");
                var lh = Draw.Measure(legend, Draw.fSmall, (int)w).Height; g.DrawString(legend, Draw.fSmall, muted, new RectangleF(x, y, w, lh + 4)); y += lh + 16 * S;
            }
            return y;
        }
    }
}
