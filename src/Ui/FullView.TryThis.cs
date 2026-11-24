using System;
using System.Drawing;
using System.Linq;

namespace AdeeboMeter
{
    // "Try this" (Adeeb, 22 Nov: "people don't know what we can do"): one line and one button on the pages people come to for
    // something else - Sports, Streaming, Markets, Gamer - pointing at the check that answers the question they're likely to
    // have there. Two or three tips per page, a different one every few minutes.
    sealed partial class FullView
    {
        // { page, tip, button, where it goes (tool:/guide:/case:/a page) }
        static readonly string[][] TryTips = {
            new[] { "sports", "Streams worse in the evening? See your network's rush hour - and when to watch.", "Rush hour", "tool:rushhour" },
            new[] { "sports", "Picture freezing at the big moment? Find out if it's your Wi-Fi, your line or the service.", "Why it buffers", "case:buffer" },
            new[] { "stream", "Going live? Check upload, Wi-Fi and what else is using the line in one go.", "Ready to stream?", "tool:golive" },
            new[] { "stream", "Frames dropping in OBS? See whether it's the network or the PC.", "OBS check", "tool:obs" },
            new[] { "stream", "Is your plan fast enough for 4K on every screen at home?", "Plan check", "tool:plan" },
            new[] { "trade", "Your broker lags at the open? Check the line to it before the bell.", "Market-open check", "tool:marketopen" },
            new[] { "trade", "Is your connection fast enough to trade? See what 40 ms really costs.", "Line vs trading", "tool:tradelat" },
            new[] { "trade", "A slow moment while you traded? The Time machine shows what your line did then.", "Time machine", "time" },
            new[] { "gamer", "Lag spikes? Find out whose fault it is - your Wi-Fi, your line or the game's server.", "Why it lags", "tool:lag" },
            new[] { "gamer", "Before a ranked match: one check of Wi-Fi, background downloads and the line.", "Ready to play?", "tool:prematch" },
            new[] { "gamer", "Would a ping booster (GPN) actually help you? Measure it instead of guessing.", "GPN check", "tool:gpn" } };
        float TryThis(Graphics g, System.Windows.Forms.Control c, string page, float x, float y, float w, bool paper)
        {
            var tips = TryTips.Where(t => t[0] == page).ToArray(); if (tips.Length == 0) return y;
            var tip = tips[(int)(DateTime.Now.Ticks / TimeSpan.TicksPerMinute / 4 % tips.Length)];   // a new one every 4 minutes
            var f = Draw.fBody; var bf = Draw.fBtn; var bs = g.MeasureString(L.T(tip[2]) + "  ›", bf);
            float h = 48 * S; var r = new RectangleF(x, y, w, h);
            Color acc = paper ? Paper.Navy : Kit.Accent(page == "trade" ? "trade" : page);
            if (paper) { Theme.Fill(g, r, 6 * S, Paper.DeskBg); Theme.Stroke(g, r, 6 * S, Paper.Line, 1); }
            else { Theme.Fill(g, r, 12 * S, Theme.Card); Theme.Stroke(g, r, 12 * S, Theme.Line, 1); }   // a plain card; the icon carries the colour (24 Nov)
            var ir = new RectangleF(x + 14 * S, y + (h - 26 * S) / 2, 26 * S, 26 * S);
            using (var ib = new SolidBrush(Color.FromArgb(Theme.Dark && !paper ? 50 : 30, acc))) g.FillEllipse(ib, ir);
            Kit.Icon(g, "question", new RectangleF(ir.X + 5 * S, ir.Y + 5 * S, 16 * S, 16 * S), acc);
            var br = new RectangleF(r.Right - bs.Width - 34 * S, y + (h - 32 * S) / 2, bs.Width + 22 * S, 32 * S);
            Theme.Fill(g, br, paper ? 6 * S : 10 * S, acc);
            using (var wb = new SolidBrush(Color.White)) g.DrawString(L.T(tip[2]) + "  ›", bf, wb, br.X + 11 * S, br.Y + (32 * S - bs.Height) / 2);
            using (var tb = new SolidBrush(paper ? Paper.Ink : Theme.Ink)) using (var one = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisWord })
            {
                var lab = (paper ? "" : L.T("Try this") + ":  ") + L.T(tip[1]); float tx = ir.Right + 12 * S;
                g.DrawString(lab, f, tb, new RectangleF(tx, y + (h - f.GetHeight(g)) / 2, br.X - tx - 12 * S, f.GetHeight(g) + 4), one);
            }
            string go = tip[3]; Act(c, r, delegate { if (go.Contains(":")) FixGo(go); else OpenNav(go); });
            return y + h + 14 * S;
        }
    }
}
