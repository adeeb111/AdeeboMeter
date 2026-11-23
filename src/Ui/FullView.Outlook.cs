// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Outlook (1.1.6)
    // The weather, your network and this PC: how things are now, and what the last few days say about the next one. Every
    // line of "the next day, in short" names what it's based on; with too little history it says so instead of guessing.
    // Nothing new is measured: the weather is the forecast AdeeboMeter already fetches (when Weather is on), the rest is
    // History and the live monitor.
    sealed partial class FullView
    {
        Panel outlookPage; DB outlookCanvas; string outlookKey = "";
        Panel BuildOutlookPage()
        {
            outlookPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };
            outlookCanvas = new DB { Name = "outlookCanvas", Dock = DockStyle.Fill, BackColor = Theme.Bg, AutoScroll = true };
            outlookCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintOutlook(e.Graphics); }; Wire(outlookCanvas);
            outlookPage.Controls.Add(outlookCanvas);
            return outlookPage;
        }
        void OutlookTick()
        {
            if (tab != "outlook" || outlookCanvas == null) return;
            string k = DateTime.Now.Minute + "|" + System.Threading.Interlocked.Read(ref LineHours.Ver) + "|" + System.Threading.Interlocked.Read(ref Sports.Ver) + "|" + (M.Latest == null ? 0 : M.Latest.GetHashCode()) / 7;
            if (k != outlookKey) { outlookKey = k; outlookCanvas.Invalidate(); }
        }
        void OlText(Graphics g, string t, Font f, Color c, float x, float y) { using (var b = new SolidBrush(c)) g.DrawString(t, f, b, x, y); }
        void OlTextR(Graphics g, string t, Font f, Color c, float right, float y) { var s = g.MeasureString(t, f); using (var b = new SolidBrush(c)) g.DrawString(t, f, b, right - s.Width, y); }
        RectangleF OlChip(Graphics g, string t, float right, float y, int lv)
        {
            var s = g.MeasureString(t, Draw.fTiny); var r = new RectangleF(right - s.Width - 20 * S, y, s.Width + 20 * S, 24 * S);
            Theme.Fill(g, r, 12 * S, lv == 3 ? Theme.Soft : Theme.LvSoft(lv)); using (var b = new SolidBrush(lv == 3 ? Theme.Sub : Theme.LvText(lv))) g.DrawString(t, Draw.fTiny, b, r.X + 10 * S, r.Y + (r.Height - s.Height) / 2);
            return r;
        }

        void PaintOutlook(Graphics g)
        {
            g.TranslateTransform(outlookCanvas.AutoScrollPosition.X, outlookCanvas.AutoScrollPosition.Y);
            Begin(outlookCanvas);
            float x = 24 * S, y = 20 * S, w = outlookCanvas.ClientSize.Width - 48 * S;
            y = PaintOutlookBody(g, outlookCanvas, x, y, w, true); Need(outlookCanvas, y);
        }
        // the Outlook itself - on its own page and on Home (22 Nov: "everything should be in the page")
        float PaintOutlookBody(Graphics g, Control oc, float x, float y, float w, bool own)
        {
            int days = 0; double[] byHour = null; if (Cfg.History) byHour = LineHours.ByHour(out days);
            // the header: title, place, updated, refresh
            if (own) OlText(g, L.T("Outlook"), Draw.fTitle, Theme.Ink, x, y); else using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("OUTLOOK  ·  NOW AND NEXT"), Draw.fTiny, mb, x, y + 18 * S);
            if (own) using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(L.T("The weather, your network and this PC: how things are now, and what the last few days say about the next one."), Draw.fBody, sb, new RectangleF(x, y + 36 * S, w * 0.62f, 40 * S));
            string place = Weather.Active ? Weather.PlaceName() : L.T("Choose a place for the weather");
            var ps = g.MeasureString(place, Draw.fBtn); var pr = new RectangleF(x + w - ps.Width - 40 * S - 120 * S, y + 8 * S, ps.Width + 40 * S, 40 * S); Kit.Box(g, pr, 10 * S);
            Kit.Icon(g, "dest", new RectangleF(pr.X + 10 * S, pr.Y + 11 * S, 18 * S, 18 * S), Theme.Blue); OlText(g, place, Draw.fBtn, Theme.Ink, pr.X + 32 * S, pr.Y + (pr.Height - ps.Height) / 2);
            Act(oc, pr, delegate { var p = PickPlace(); if (p != null) { Cfg.WeatherPlace = Weather.Line(p); Cfg.WeatherOn = true; Cfg.Save(); oc.Invalidate(); } });
            OlText(g, L.T("Updated") + " " + DateTime.Now.ToString("t", CultureInfo.CurrentCulture), Draw.fSmall, Theme.Muted, pr.Right + 12 * S, pr.Y + 12 * S);
            y += 86 * S;

            // row 1: weather now | your network | this PC
            float cw = (w - 32 * S) / 3, ch = 228 * S;
            var hours = Weather.Active ? Weather.Ahead(DateTime.UtcNow, 72) : new List<Weather.Hour>(); var now = hours.FirstOrDefault();
            var wr = new RectangleF(x, y, cw, ch); Kit.Box(g, wr);
            OlText(g, L.T("Weather"), Draw.fName, Theme.Ink, x + 20 * S, y + 16 * S); OlTextR(g, "Open-Meteo", Draw.fSmall, Theme.Muted, wr.Right - 20 * S, y + 20 * S);
            if (now == null) using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(Weather.Active ? L.T("The forecast is on its way...") : Policy.NoOnline ? L.T("Weather is off: your IT team turned online lookups off.") : L.T("Choose a place above and the forecast shows here (Open-Meteo: free, no account)."), Draw.fBody, sb, new RectangleF(x + 20 * S, y + 56 * S, cw - 40 * S, 120 * S));
            else
            {
                Paper.WeatherGlyph(g, now.Code, new RectangleF(x + 18 * S, y + 52 * S, 72 * S, 72 * S));
                OlText(g, Weather.Temp(now.Temp), Draw.fHero, Theme.Ink, x + 100 * S, y + 54 * S);
                OlText(g, L.T(Weather.Words(now.Code)) + " · " + L.T("feels like") + " " + Weather.Temp(now.Feels), Draw.fSmall, Theme.Sub, x + 100 * S, y + 104 * S);
                var day = hours.Where(h => h.Utc < DateTime.UtcNow.AddHours(24)).ToList(); var wet = day.Where(h => h.Pop >= 0).OrderByDescending(h => h.Pop).FirstOrDefault();
                float dy = y + 142 * S;
                foreach (var kv in new[] { new[] { L.T("Rain chance, next 24 h"), wet == null ? "-" : wet.Pop + "% " + L.T("around") + " " + wet.Utc.ToLocalTime().ToString("h tt", CultureInfo.CurrentCulture) }, new[] { L.T("Humidity"), now.Hum < 0 ? "-" : now.Hum + "%" }, new[] { L.T("Wind"), double.IsNaN(now.Wind) ? "-" : now.Wind.ToString("0", CultureInfo.CurrentCulture) + " km/h" + (now.WindFrom >= 0 ? " " + L.T("from the") + " " + L.T(Compass(now.WindFrom)) : "") } })
                { OlText(g, kv[0], Draw.fSmall, Theme.Muted, x + 20 * S, dy); OlTextR(g, kv[1], Draw.fBodyB, Theme.Ink, wr.Right - 20 * S, dy - 1 * S); dy += 26 * S; }
            }
            // your network
            var nr = new RectangleF(x + cw + 16 * S, y, cw, ch); Kit.Box(g, nr); float nx = nr.X;
            double gw, net, loss; double[] hist; lock (M.L) { gw = M.GwPing.Avg(10); net = M.NetPing.Avg(10); loss = M.Loss.Avg(20) * 100; hist = M.NetPing.Values(30); }
            int nlv = double.IsNaN(net) ? 3 : (!double.IsNaN(loss) && loss >= 2) || net > 100 ? 2 : net > 60 ? 1 : 0;
            OlText(g, L.T("Your network"), Draw.fName, Theme.Ink, nx + 20 * S, y + 16 * S); OlChip(g, nlv == 0 ? L.T("Steady") : nlv == 1 ? L.T("Busy") : nlv == 2 ? L.T("Unsteady") : L.T("Checking"), nr.Right - 20 * S, y + 16 * S, nlv);
            float ny = y + 56 * S;
            foreach (var kv in new[] { new[] { L.T("Router"), double.IsNaN(gw) ? "-" : gw.ToString("0", CultureInfo.CurrentCulture) + " ms" }, new[] { L.T("Internet"), double.IsNaN(net) ? "-" : net.ToString("0", CultureInfo.CurrentCulture) + " ms" }, new[] { L.T("Loss, recent"), double.IsNaN(loss) ? "-" : loss.ToString("0.0", CultureInfo.CurrentCulture) + "%" }, new[] { L.T("Drops this week"), LineHours.Drops7 < 0 ? (Cfg.History ? "..." : "-") : LineHours.Drops7 == 0 ? L.T("none") : LineHours.Drops7.ToString(CultureInfo.CurrentCulture) } })
            { OlText(g, kv[0], Draw.fSmall, Theme.Muted, nx + 20 * S, ny); OlTextR(g, kv[1], Draw.fBodyB, Theme.Ink, nr.Right - 20 * S, ny - 1 * S); ny += 26 * S; }
            Paper.Spark(g, new RectangleF(nx + 20 * S, ny + 6 * S, cw - 40 * S, 36 * S), hist, Theme.Blue, true);
            // this PC
            var cr = new RectangleF(x + 2 * (cw + 16 * S), y, cw, ch); Kit.Box(g, cr); float cx = cr.X;
            double cpu, mem; lock (M.L) { cpu = M.Cpu.Avg(10); mem = M.Mem.Last; } var snap = M.Latest;
            int clv = double.IsNaN(cpu) ? 3 : cpu > 85 || mem > 92 ? 2 : cpu > 60 || mem > 85 ? 1 : 0;
            OlText(g, L.T("This PC"), Draw.fName, Theme.Ink, cx + 20 * S, y + 16 * S); OlChip(g, clv == 0 ? L.T("Fine") : clv == 1 ? L.T("Busy") : clv == 2 ? L.T("Struggling") : L.T("Checking"), cr.Right - 20 * S, y + 16 * S, clv);
            float cy = y + 58 * S;
            foreach (var bar in new[] { new object[] { "CPU", cpu }, new object[] { L.T("Memory"), mem } })
            {
                double v = (double)bar[1]; OlText(g, (string)bar[0], Draw.fSmall, Theme.Muted, cx + 20 * S, cy); OlTextR(g, double.IsNaN(v) ? "-" : v.ToString("0", CultureInfo.CurrentCulture) + "%", Draw.fBodyB, Theme.Ink, cr.Right - 20 * S, cy - 1 * S);
                var tr = new RectangleF(cx + 20 * S, cy + 22 * S, cw - 40 * S, 6 * S); Theme.Fill(g, tr, 3 * S, Theme.Track); if (!double.IsNaN(v)) Theme.Fill(g, new RectangleF(tr.X, tr.Y, tr.Width * (float)Math.Min(1, v / 100), tr.Height), 3 * S, v > 85 ? Theme.Red : Theme.Blue); cy += 40 * S;
            }
            OlText(g, L.T("Disk"), Draw.fSmall, Theme.Muted, cx + 20 * S, cy); OlTextR(g, OlFree(), Draw.fBodyB, Theme.Ink, cr.Right - 20 * S, cy - 1 * S); cy += 28 * S;
            var pw = SystemInformation.PowerStatus; if (pw.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery && pw.BatteryLifePercent <= 1) { OlText(g, L.T("Battery"), Draw.fSmall, Theme.Muted, cx + 20 * S, cy); OlTextR(g, (pw.BatteryLifePercent * 100).ToString("0", CultureInfo.CurrentCulture) + "%" + (pw.PowerLineStatus == PowerLineStatus.Online ? ", " + L.T("plugged in") : ""), Draw.fBodyB, Theme.Ink, cr.Right - 20 * S, cy - 1 * S); }
            y += ch + 16 * S;

            // row 2: next 12 hours | three days
            if (hours.Count > 0)
            {
                float lw = (w - 16 * S) * 0.64f; var hr = new RectangleF(x, y, lw, 150 * S); Kit.Box(g, hr);
                OlText(g, L.T("Next 12 hours"), Draw.fName, Theme.Ink, x + 20 * S, y + 16 * S);
                var twelve = hours.Take(12).ToList(); float tw = (lw - 40 * S - 11 * 6 * S) / 12;
                for (int i = 0; i < twelve.Count; i++)
                {
                    var h = twelve[i]; var tr = new RectangleF(x + 20 * S + i * (tw + 6 * S), y + 50 * S, tw, 84 * S);
                    if (i == 0) { Theme.Fill(g, tr, 10 * S, Theme.BlueSoft); Theme.Stroke(g, tr, 10 * S, Theme.Blue, 1); } else Theme.Stroke(g, tr, 10 * S, Theme.Line2, 1);
                    string t = i == 0 ? L.T("Now") : h.Utc.ToLocalTime().ToString("h tt", CultureInfo.CurrentCulture); var ts = g.MeasureString(t, Draw.fTiny); OlText(g, t, Draw.fTiny, i == 0 ? Theme.BlueDark : Theme.Muted, tr.X + (tr.Width - ts.Width) / 2, tr.Y + 8 * S);
                    var tt = Weather.TempShort(h.Temp); var tz = g.MeasureString(tt, Draw.fBodyB); OlText(g, tt, Draw.fBodyB, Theme.Ink, tr.X + (tr.Width - tz.Width) / 2, tr.Y + 30 * S);
                    var pp = h.Pop < 0 ? "" : h.Pop + "%"; var pz = g.MeasureString(pp, Draw.fTiny); OlText(g, pp, Draw.fTiny, h.Pop >= 50 ? Theme.BlueDark : Theme.Muted, tr.X + (tr.Width - pz.Width) / 2, tr.Y + 58 * S);
                }
                var dr = new RectangleF(x + lw + 16 * S, y, w - lw - 16 * S, 150 * S); Kit.Box(g, dr);
                OlText(g, L.T("Three days"), Draw.fName, Theme.Ink, dr.X + 20 * S, y + 16 * S);
                float dy2 = y + 52 * S;
                foreach (var grp in hours.GroupBy(h => h.Utc.ToLocalTime().Date).Take(3))
                {
                    var l = grp.ToList(); double hi = l.Where(h => !double.IsNaN(h.Temp)).Select(h => h.Temp).DefaultIfEmpty(double.NaN).Max(), lo = l.Where(h => !double.IsNaN(h.Temp)).Select(h => h.Temp).DefaultIfEmpty(double.NaN).Min(); int pop = l.Max(h => h.Pop);
                    OlText(g, grp.Key == DateTime.Today ? L.T("Today") : grp.Key.ToString("dddd", CultureInfo.CurrentCulture), Draw.fBodyB, Theme.Ink, dr.X + 20 * S, dy2);
                    OlText(g, Weather.TempShort(hi) + " / " + Weather.TempShort(lo), Draw.fBody, Theme.Sub, dr.X + dr.Width * 0.45f, dy2); OlTextR(g, pop < 0 ? "-" : pop + "%", Draw.fBodyB, pop >= 50 ? Theme.BlueDark : Theme.Muted, dr.Right - 20 * S, dy2);
                    dy2 += 30 * S;
                }
                y += 166 * S;
            }

            // row 3: when your internet is slowest | coming up
            float lw3 = (w - 16 * S) * 0.64f; var sr = new RectangleF(x, y, lw3, 210 * S); Kit.Box(g, sr);
            OlText(g, L.T("When your internet is slowest"), Draw.fName, Theme.Ink, x + 20 * S, y + 16 * S);
            OlTextR(g, Cfg.History ? days + " " + L.T("days of History on this PC") : L.T("History is off"), Draw.fSmall, Theme.Muted, sr.Right - 20 * S, y + 20 * S);
            if (!Cfg.History) using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(L.T("Turn on History in Settings: AdeeboMeter writes one line a minute on this PC, and this chart is made from it."), Draw.fBody, sb, new RectangleF(x + 20 * S, y + 60 * S, lw3 - 40 * S, 60 * S));
            else if (byHour == null) OlText(g, L.T("Reading your history..."), Draw.fBody, Theme.Muted, x + 20 * S, y + 60 * S);
            else if (days < 5) using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(L.T("Not enough history yet to show a pattern - ask again in a few days.") + " (" + days + " / 5)", Draw.fBody, sb, new RectangleF(x + 20 * S, y + 44 * S, lw3 - 40 * S, 24 * S));   // R2-075: the same 5 days the rest of the page waits for
            else
            {
                string words = LineHours.SlowWords(byHour, days);
                using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(words ?? L.T("No hour stands out: your line is about as quick all day."), Draw.fBody, sb, new RectangleF(x + 20 * S, y + 44 * S, lw3 - 40 * S, 24 * S));
                var bars = new RectangleF(x + 20 * S, y + 76 * S, lw3 - 40 * S, 100 * S); double mx = byHour.Where(v => !double.IsNaN(v)).DefaultIfEmpty(1).Max();   // R2-176: the unused "worst hour" is gone
                float bw = bars.Width / 24;
                for (int i = 0; i < 24; i++) { if (double.IsNaN(byHour[i])) continue; float bh = (float)(byHour[i] / mx) * bars.Height; Theme.Fill(g, new RectangleF(bars.X + i * bw + 1.5f * S, bars.Bottom - bh, bw - 3 * S, bh), 2 * S, byHour[i] >= mx * 0.8 ? Theme.Blue : Kit.Mix(Theme.Card, Theme.Blue, 0.4f)); }
                for (int i = 0; i <= 24; i += 6) OlText(g, LineHours.Clock(i % 24), Draw.fTiny, Theme.Muted, Math.Min(bars.X + i * bw, bars.Right - 40 * S), bars.Bottom + 6 * S);
            }
            var ur = new RectangleF(x + lw3 + 16 * S, y, w - lw3 - 16 * S, 210 * S); Kit.Box(g, ur); float ux = ur.X;
            OlText(g, L.T("Coming up"), Draw.fName, Theme.Ink, ux + 20 * S, y + 16 * S);
            float uy = y + 54 * S;
            Sports.Game next = null; if (Cfg.Sports) lock (Sports.k) next = SpNext(Sports.Games.ToList());
            var nyx = MarketData.Exchanges.First(e => e.Name == "New York"); var ses = MarketData.NextSession(nyx, DateTime.UtcNow); string nst = MarketData.State(nyx, DateTime.UtcNow);
            var rainAt = hours.Take(24).FirstOrDefault(h => h.Pop >= 50);
            var items = new List<string[]>();
            if (next != null) items.Add(new[] { next.Home + " " + L.T("vs") + " " + next.Away, next.State == "live" ? L.T("playing now") : next.StartUtc.ToLocalTime().ToString("dddd h:mm tt", CultureInfo.CurrentCulture) + " · " + L.T("a team you follow"), "sports" });
            if (ses != null) items.Add(new[] { nst == "open" ? L.T("Markets are open") : L.T("Markets open") + " " + ses.Item1.ToString("dddd h:mm tt", CultureInfo.CurrentCulture), L.T("New York and Toronto"), "trade" });
            if (rainAt != null) items.Add(new[] { L.T("Rain from about") + " " + rainAt.Utc.ToLocalTime().ToString("h tt", CultureInfo.CurrentCulture), rainAt.Pop + "% " + L.T("chance"), "" });
            if (items.Count == 0) OlText(g, L.T("Nothing planned - follow a team or turn on the markets numbers."), Draw.fSmall, Theme.Muted, ux + 20 * S, uy);
            foreach (var it in items.Take(3))
            {
                var ir = new RectangleF(ux + 16 * S, uy, ur.Width - 32 * S, 44 * S); Theme.Stroke(g, ir, 10 * S, Theme.Line2, 1);
                OlText(g, it[0], Draw.fBodyB, Theme.Ink, ir.X + 12 * S, ir.Y + 4 * S); OlText(g, it[1], Draw.fTiny, Theme.Muted, ir.X + 12 * S, ir.Y + 24 * S);
                if (it[2] != "") { string to = it[2]; Act(oc, ir, delegate { tab = to; ShowTab(); SyncNav(); }); }
                uy += 50 * S;
            }
            y += 226 * S;

            // row 4: the next day, in short (each line says what it's based on)
            var rows = new List<string[]>();   // label, chip level, chip, sentence, based on
            if (hours.Count > 0)
            {
                var d24 = hours.Take(24).ToList(); var wetH = d24.Where(h => h.Pop >= 50).ToList();
                rows.Add(new[] { L.T("Weather"), wetH.Count > 0 ? "1" : "0", wetH.Count > 0 ? L.T("Rain later") : L.T("Dry"), wetH.Count > 0 ? L.T("Rain likely around") + " " + wetH[0].Utc.ToLocalTime().ToString("h tt", CultureInfo.CurrentCulture) + " (" + wetH.Max(h => h.Pop) + "%)." : L.T("No rain expected in the next 24 hours."), L.T("Open-Meteo forecast") });
            }
            if (!Cfg.History) rows.Add(new[] { L.T("Network"), "3", L.T("History off"), L.T("Turn on History to see how your line usually behaves."), "-" });
            else if (days < 5) rows.Add(new[] { L.T("Network"), "3", L.T("Learning"), L.T("Not enough history yet to spot a pattern. Ask again in") + " " + (5 - days) + " " + L.T("days") + ".", days + " " + L.T("days of History") });
            else
            {
                string sw = LineHours.SlowWords(byHour, days); int dr7 = LineHours.Drops7;
                rows.Add(new[] { L.T("Network"), dr7 > 2 ? "1" : "0", dr7 > 2 ? L.T("Some drops") : L.T("Steady"), (dr7 <= 0 ? L.T("No drops in the last 7 days.") : dr7 + " " + L.T("drops in the last 7 days.")) + (sw != null ? " " + L.T("Expect the usual slow stretch:") + " " + sw + "." : ""), days + " " + L.T("days of History") });
            }
            rows.Add(new[] { L.T("This PC"), clv.ToString(CultureInfo.InvariantCulture), clv == 0 ? L.T("Fine") : clv == 1 ? L.T("Busy") : clv == 2 ? L.T("Struggling") : L.T("Checking"), clv == 0 ? L.T("Nothing running hot right now.") : L.T("Something is working the PC hard - see What's running."), L.T("live, right now") });
            var tr2 = new RectangleF(x, y, w, 70 * S + rows.Count * 44 * S); Kit.Box(g, tr2);
            OlText(g, L.T("The next day, in short"), Draw.fName, Theme.Ink, x + 20 * S, y + 16 * S);
            OlText(g, L.T("Each line says what it's based on. With too little history, it says so instead of guessing."), Draw.fSmall, Theme.Muted, x + 20 * S, y + 42 * S);
            float ry = y + 68 * S;
            foreach (var rw in rows)
            {
                using (var lp = new Pen(Theme.Line2, 1)) g.DrawLine(lp, x + 16 * S, ry, x + w - 16 * S, ry);
                OlText(g, rw[0], Draw.fBodyB, Theme.Ink, x + 20 * S, ry + 12 * S);
                var chip = OlChip(g, rw[2], x + 140 * S + g.MeasureString(rw[2], Draw.fTiny).Width + 20 * S, ry + 10 * S, int.Parse(rw[1], CultureInfo.InvariantCulture));
                using (var sb = new SolidBrush(Theme.Sub)) using (var nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(rw[3], Draw.fBody, sb, new RectangleF(x + 290 * S, ry + 12 * S, w - 290 * S - 220 * S, 22 * S), nf);
                OlTextR(g, rw[4], Draw.fSmall, Theme.Muted, x + w - 20 * S, ry + 14 * S);
                ry += 44 * S;
            }
            y = tr2.Bottom + 24 * S;
            return y;
        }
        // free space on the Windows drive (read at most once a minute)
        static string olFree = "-"; static DateTime olFreeAt = DateTime.MinValue;
        static string OlFree()
        {
            if ((DateTime.UtcNow - olFreeAt).TotalSeconds < 60) return olFree; olFreeAt = DateTime.UtcNow;
            try { var d = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory)); olFree = (d.AvailableFreeSpace / 1073741824.0).ToString("N0", CultureInfo.CurrentCulture) + " GB " + L.T("free"); }
            catch (Exception e) { if (!(e is System.IO.IOException || e is UnauthorizedAccessException || e is ArgumentException)) throw; olFree = "-"; }
            return olFree;
        }
        static string Compass(int deg) { var n = new[] { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" }; return n[((deg + 22) % 360) / 45]; }
    }
}
