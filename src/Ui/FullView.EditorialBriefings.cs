// AdeeboMeter - native editorial briefings, integrated with the existing Sports page.
// No new background services, online requests, or dependencies.
using System;
using System.Drawing;
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
                f.Size = new Size(Sc(930), Sc(700));
                f.MinimumSize = new Size(Sc(600), Sc(460));
                f.BackColor = Theme.Card;
                f.ForeColor = Theme.Ink;
                f.Font = Draw.fBody;
                var tabs = new TabControl { Dock = DockStyle.Fill, Font = Draw.fBody };
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

        void BriefText(FlowLayoutPanel p, string text, bool heading)
        {
            var l = new Label
            {
                Text = text, AutoSize = false, Width = Sc(780),
                Height = heading ? Sc(56) : Sc(44),
                Font = new Font("Georgia", heading ? 25 : 13, heading ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = Theme.Ink,
                TextAlign = heading ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft,
                Margin = new Padding(0, Sc(3), 0, Sc(4))
            };
            p.Controls.Add(l);
        }

        void BriefRule(FlowLayoutPanel p)
        {
            p.Controls.Add(new Panel { Width = Sc(780), Height = Sc(2), BackColor = Theme.Line, Margin = new Padding(0, Sc(6), 0, Sc(12)) });
        }

        void BriefParagraph(FlowLayoutPanel p, string title, string body)
        {
            BriefText(p, title, false);
            var l = new Label
            {
                Text = body, Width = Sc(780), Height = Sc(70),
                ForeColor = Theme.Sub, Font = Draw.fBody,
                Margin = new Padding(0, 0, 0, Sc(10))
            };
            p.Controls.Add(l);
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
