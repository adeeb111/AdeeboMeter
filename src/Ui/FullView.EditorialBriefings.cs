// AdeeboMeter - native editorial briefings, integrated with the existing Sports page.
// No new background services, online requests, or dependencies.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    sealed partial class FullView
    {
        void ShowEditorialBriefings()
        {
            using (var f = new Form())
            {
                f.Text = "AdeeboMeter - Editorial Briefings";
                f.StartPosition = FormStartPosition.CenterParent;
                f.Size = new Size(Sc(1040), Sc(760));
                f.MinimumSize = new Size(Sc(600), Sc(460));
                f.BackColor = Theme.Card;
                f.ForeColor = Theme.Ink;
                f.Font = Draw.fBody;
                var tabs = new TabControl { Dock = DockStyle.Fill, Font = Draw.fBody, Padding = new Point(Sc(16), Sc(8)) };
                tabs.TabPages.Add(BuildBriefTab("SPORTS", "The Matchday Edition", BuildSportsBrief));
                tabs.TabPages.Add(BuildBriefTab("MARKETS", "The Daily Financial Edition", BuildMarketsBrief));
                tabs.TabPages.Add(BuildBriefTab("WEATHER", "The Daily Conditions Edition", BuildWeatherBrief));
                f.Controls.Add(tabs);
                f.ShowDialog(this);
            }
        }

        TabPage BuildBriefTab(string heading, string edition, Action<FlowLayoutPanel> build)
        {
            var page = new TabPage(heading) { BackColor = Theme.Card, ForeColor = Theme.Ink };
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Card };
            var stack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top, Padding = new Padding(Sc(16)),
                BackColor = Theme.Card
            };
            scroll.Controls.Add(stack); page.Controls.Add(scroll);
            BriefText(stack, "THE " + heading + " PAGE", true);
            BriefText(stack, edition + "  |  " + DateTime.Now.ToString("dddd, d MMMM yyyy"), false);
            BriefRule(stack);
            build(stack);
            // Resize content when the user resizes the window.
            scroll.Resize += delegate { int width = Math.Max(Sc(300), scroll.ClientSize.Width - Sc(48)); foreach (Control c in stack.Controls) c.Width = width; };
            return page;
        }

        // All drawing is native GDI+. No WebView, HTML renderer or embedded browser.
        sealed class EditorialCard : Control
        {
            public string Heading;
            public string Copy;
            public Color Ink;
            public Color Sub;
            public Color Border;
            public Color Paper;
            public float Scale;
            public EditorialCard(string heading, string copy, Color ink, Color sub, Color border, Color paper, float scale)
            {
                Heading = heading; Copy = copy; Ink = ink; Sub = sub; Border = border; Paper = paper; Scale = scale;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Height = (int)(148 * scale);
                Margin = new Padding(0, (int)(5 * scale), 0, (int)(12 * scale));
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
                using (var bg = new SolidBrush(Paper)) g.FillRectangle(bg, r);
                using (var pen = new Pen(Border)) g.DrawRectangle(pen, r);
                using (var accent = new SolidBrush(Color.FromArgb(161, 62, 49)))
                    g.FillRectangle(accent, r.Left, r.Top, Math.Max(2, (int)(4 * Scale)), r.Height);
                int pad = (int)(19 * Scale), left = r.Left + pad, top = r.Top + (int)(14 * Scale);
                int width = Math.Max(10, r.Width - pad * 2);
                using (var headline = new Font("Georgia", 18 * Scale, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var body = new Font("Segoe UI", 14 * Scale, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var inkBrush = new SolidBrush(Ink))
                using (var subBrush = new SolidBrush(Sub))
                using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                {
                    g.DrawString(Heading, headline, inkBrush, new RectangleF(left, top, width, 32 * Scale), sf);
                    using (var pen = new Pen(Border)) g.DrawLine(pen, left, top + 35 * Scale, left + width, top + 35 * Scale);
                    g.DrawString(Copy, body, subBrush,
                        new RectangleF(left, top + 44 * Scale, width, Math.Max(10, Height - (int)(64 * Scale))), sf);
                }
            }
        }

        void BriefText(FlowLayoutPanel p, string text, bool heading)
        {
            var l = new Label
            {
                Text = text, AutoSize = false, Width = Sc(780),
                Height = heading ? Sc(76) : Sc(36),
                Font = new Font("Georgia", heading ? 31 : 12, heading ? FontStyle.Bold : FontStyle.Italic),
                ForeColor = Theme.Ink,
                TextAlign = heading ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleCenter,
                Margin = new Padding(0, Sc(3), 0, Sc(4))
            };
            p.Controls.Add(l);
        }

        void BriefRule(FlowLayoutPanel p)
        {
            var rule = new Panel { Width = Sc(780), Height = Sc(7), Margin = new Padding(0, Sc(6), 0, Sc(15)) };
            rule.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Theme.Ink, Math.Max(1, Sc(2))))
                {
                    e.Graphics.DrawLine(pen, 0, 1, rule.Width, 1);
                    e.Graphics.DrawLine(pen, 0, rule.Height - 2, rule.Width, rule.Height - 2);
                }
            };
            p.Controls.Add(rule);
        }

        void BriefParagraph(FlowLayoutPanel p, string title, string body)
        {
            p.Controls.Add(new EditorialCard(title, body, Theme.Ink, Theme.Sub, Theme.Line,
                Theme.Card, S) { Width = Sc(780) });
        }

        void BuildSportsBrief(FlowLayoutPanel p)
        {
            BriefParagraph(p, "THE MATCHDAY BRIEFING", "Choose an upcoming match below. The current sports feed supplies teams and kickoff; stadium weather is not inferred without verified venue coordinates.");
            Sports.Game[] games;
            lock (Sports.k) games = Sports.Games.Where(g => g.State == "soon").OrderBy(g => g.StartUtc).Take(30).ToArray();
            var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Sc(740), Height = Sc(32) };
            picker.Items.Add("Select a fixture");
            foreach (var g in games)
                picker.Items.Add(g.Away + " vs " + g.Home + " - " + (g.StartUtc == DateTime.MinValue ? "time unavailable" : g.StartUtc.ToLocalTime().ToString("ddd d MMM, h:mm tt")));
            picker.SelectedIndex = 0; p.Controls.Add(picker);
            var selected = new Label { Width = Sc(780), Height = Sc(80), ForeColor = Theme.Sub, Text = "No match selected. Stadium location and forecast: unavailable.", Margin = new Padding(0, Sc(10), 0, Sc(8)) };
            p.Controls.Add(selected);
            picker.SelectedIndexChanged += delegate
            {
                int i = picker.SelectedIndex - 1;
                if (i < 0) { selected.Text = "No match selected. Stadium location and forecast: unavailable."; return; }
                var g = games[i];
                selected.Text = g.Away + " vs " + g.Home + "\nKickoff on this PC: " +
                    (g.StartUtc == DateTime.MinValue ? "unavailable" : g.StartUtc.ToLocalTime().ToString("f")) +
                    "\nStadium time / weather: unavailable without verified venue and timezone.";
            };
            BriefRule(p);
            BriefParagraph(p, "HOME READINESS", "PC and network forecasts require measured local history. Open the existing PC and Network views for current observations. No synthetic bandwidth forecast is generated.");
            BriefParagraph(p, "IPTV READINESS", "The existing Streaming page provides stream observations. Authenticated IPTV probing is not implemented in this initial native briefing.");
            BriefParagraph(p, "BEFORE KICKOFF", "Confirm kickoff in your local time. Check PC load and network stability. Test your authorized streaming service on demand before the match.");
        }

        void BuildMarketsBrief(FlowLayoutPanel p)
        {
            BriefParagraph(p, "TODAY'S MARKET BRIEFING", "An editorial overview of energy, precious metals, company earnings and economic releases. Prices and events are shown only when verified source data is available.");
            BriefRule(p);
            BriefParagraph(p, "ENERGY DESK - OIL & GAS", "WTI, Brent and natural gas: awaiting a verified snapshot. Future integration should reuse approved EIA sources and show units and observation dates.");
            BriefParagraph(p, "METALS DESK - GOLD & SILVER", "Gold, silver and copper: awaiting verified values. Historical or delayed observations must never appear as live prices.");
            BriefParagraph(p, "EARNINGS DESK", "Upcoming earnings require a verified calendar. SEC filings can support reported company results but not speculative analyst estimates.");
            BriefParagraph(p, "ECONOMIC CALENDAR", "Inflation, employment and rate announcements will appear when a verified schedule is connected, converted to Windows local time.");
            BriefParagraph(p, "TRADING READINESS", "Review the existing PC and Network views before trading. A responsive broker endpoint does not guarantee order execution.");
        }

        void BuildWeatherBrief(FlowLayoutPanel p)
        {
            BriefParagraph(p, "YOUR WEATHER OVERVIEW", Cfg.WeatherOn ? WeatherHelp() : "Weather is currently disabled. Enable it in existing Weather settings and select a location to use the Open-Meteo feed.");
            BriefRule(p);
            BriefParagraph(p, "THE FORECAST", "The existing Weather feature remains the source of truth for current conditions and upcoming days. This briefing does not fabricate hourly forecasts.");
            BriefParagraph(p, "NETWORK OUTLOOK", "Review current gateway and internet health in the existing Network views. Historical packet loss or latency may indicate a trend, not a guaranteed future outage.");
            BriefParagraph(p, "PC OUTLOOK", "Use the existing PC health view for CPU, memory, storage and battery measurements. Forecasts require enough local history.");
            BriefParagraph(p, "PREPARATION DESK", "For severe local weather, prepare power and connectivity contingencies as appropriate. Network and PC readiness are separate from meteorological forecasts.");
        }
    }
}
