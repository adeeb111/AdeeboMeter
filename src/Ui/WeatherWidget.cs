// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;

namespace AdeeboMeter
{
    // the weather in the corner widget (Weather.cs): a few characters on the folded strip, one line on the card
    static partial class Draw
    {
        static string WeatherKeyPart() { var now = DateTime.UtcNow; var t = Weather.Chip(now); return t == null ? "" : "|w" + t + Weather.ChipEmoji(now); }
        static float WeatherChipW()
        {
            var now = DateTime.UtcNow; var t = Weather.Chip(now); if (t == null) return 0;
            return 12 * S + (Weather.ChipEmoji(now) != null ? 18 * S : 0) + Measure(t, fSmall, 0).Width + 2 * S;
        }
        // a thin divider, the hour's emoji (only when it's notable) and the temperature
        static void WeatherChipDraw(Graphics g, float x, float H)
        {
            var now = DateTime.UtcNow; var t = Weather.Chip(now); if (t == null) return;
            using (var p = new Pen(Theme.Line)) g.DrawLine(p, x, 7 * S, x, H - 7 * S); x += 8 * S;
            var code = Weather.ChipEmoji(now); var em = code == null ? null : Emoji(code);
            if (em != null) { var q = g.InterpolationMode; g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(em, new RectangleF(x, (H - 15 * S) / 2, 15 * S, 15 * S)); g.InterpolationMode = q; x += 18 * S; }
            using (var b = new SolidBrush(Theme.Sub)) g.DrawString(t, fSmall, b, x, (H - Measure(t, fSmall, 0).Height) / 2);
        }
        static string WeatherCardText() { return Weather.Active && Cfg.WWeather ? Weather.Text(DateTime.UtcNow) : null; }
        static void WeatherCardDraw(Graphics g, float x, float y, float inner, string text)
        {
            var code = Weather.ChipEmoji(DateTime.UtcNow); var em = code == null ? null : Emoji(code);
            if (em != null) { var q = g.InterpolationMode; g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(em, new RectangleF(x, y, 16 * S, 16 * S)); g.InterpolationMode = q; x += 20 * S; inner -= 20 * S; }
            using (var b = new SolidBrush(Theme.Sub)) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(text, fSmall, b, new RectangleF(x, y, inner, 18 * S), sf);
        }
    }
}
