// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Globalization;

namespace AdeeboMeter
{
    // every finding says where it came from, so nobody has to take AdeeboMeter's word for it.
    //   m = measured (AdeeboMeter timed it itself)    w = Windows-reported (an API or Windows tool said so)
    //   p = seen in packets (Wireshark decoded it)     i = inferred (a conclusion drawn from the others)
    //   u = unavailable (this PC doesn't let us see it)
    // With Details on (the engineer toggle in the header) the pill also shows the time it was read.
    static class Evidence
    {
        public static string Label(string c)
        {
            switch (c) { case "m": return "measured"; case "w": return "Windows-reported"; case "p": return "seen in packets"; case "i": return "inferred"; case "u": return "unavailable"; default: return ""; }
        }
        public static Color Col(string c)
        {
            switch (c) { case "m": return Theme.Green; case "w": return Theme.Blue; case "p": return Color.FromArgb(150, 110, 230); case "i": return Theme.Amber; default: return Theme.Grey; }
        }
        public static string Key(string c) { return "ev." + Label(c); }

        // Small outlined pill; returns its width. at = when the value was read (shown only with Details on).
        public static float Pill(Graphics g, string c, float x, float y, DateTime? at)
        {
            string t = Label(c); if (t == "") return 0;
            if (Cfg.Details && at != null) t += " · " + at.Value.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            var sz = g.MeasureString(t, Draw.fTiny); float S = Draw.S;
            var r = new RectangleF(x, y, sz.Width + 12 * S, sz.Height + 4 * S);
            var col = Col(c);
            Theme.Fill(g, r, r.Height / 2, Color.FromArgb(Theme.Dark ? 50 : 28, col));
            using (var b = new SolidBrush(Theme.Dark ? EvTint.Light(col) : EvTint.Dark(col, 0.1f))) g.DrawString(t, Draw.fTiny, b, x + 6 * S, y + 2 * S);
            return r.Width;
        }

        // Which kind of evidence a health / tool row is, from where it was produced.
        public static string For(string tool, string[] row)
        {
            if (row == null || row.Length < 3) return "";
            if (row.Length > 4 && row[4] != "") return row[4];
            string name = row[1] ?? "", val = row[2] ?? "";
            if (name == "Verdict") return "i";
            if (val == "not available" || val.StartsWith("no answer", StringComparison.Ordinal)) return "u";
            switch (tool)
            {
                case "health": return "w";
                case "precall": return name.StartsWith("Your upload", StringComparison.Ordinal) ? "w" : name.StartsWith("Latency under load", StringComparison.Ordinal) ? "i" : "m";
                case "dns": return "m";
                default: return "w";
            }
        }
    }

    static class EvTint
    {
        public static Color Light(Color c) { return Color.FromArgb(c.A, Math.Min(255, c.R + (255 - c.R) / 2), Math.Min(255, c.G + (255 - c.G) / 2), Math.Min(255, c.B + (255 - c.B) / 2)); }
        public static Color Dark(Color c, float f) { float k = 0.6f - f; return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k)); }
    }
}
