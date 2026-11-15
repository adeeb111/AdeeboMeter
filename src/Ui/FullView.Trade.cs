// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Trading tab (PLAN-modes.md 3)
    // Active trader first: the live line to the broker, second by second, in market hours. Below it, for casual
    // investors: the day's currencies (Bank of Canada) and the broker connection's own record. Not advice - just numbers.
    sealed partial class FullView
    {
        Panel tradePage; DB tradeCanvas; string tradeKey = "";
        Panel BuildTradePage()
        {
            tradePage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            tradeCanvas = new DB { Name = "tradeCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            tradeCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintTrade(e.Graphics); }; Wire(tradeCanvas);
            tradePage.Controls.Add(tradeCanvas);
            return tradePage;
        }
        void TradeTick()
        {
            if (tab != "trade" || tradeCanvas == null) return;
            string k = System.Threading.Interlocked.Read(ref Trading.Ver) + "|" + DateTime.Now.Minute + "|" + Cfg.TradeLive;
            if (k != tradeKey) { tradeKey = k; tradeCanvas.Invalidate(); }
        }
        // a few brokers' public web addresses to start from; the user can type any other
        static readonly string[][] Brokers = {
            new[] { "Wealthsimple", "wealthsimple.com" }, new[] { "Questrade", "questrade.com" }, new[] { "Interactive Brokers", "interactivebrokers.com" },
            new[] { "TD Direct Investing", "td.com" }, new[] { "RBC Direct Investing", "rbcdirectinvesting.com" }, new[] { "Robinhood", "robinhood.com" },
            new[] { "Coinbase", "coinbase.com" }, new[] { "Kraken", "kraken.com" }, new[] { "Binance", "binance.com" } };
        // the broker choice as a settings row: None, the list, Other... (asks for the address)
        static readonly string[] BrokerOptList = new[] { "None" }.Concat(Brokers.Select(b => b[0].Replace(" Direct Investing", "").Replace("Interactive Brokers", "IBKR"))).Concat(new[] { "Other..." })   /* short names: they fit the choice cards */.ToArray();
        static string[] BrokerOpts() { return BrokerOptList; }
        static int BrokerIdx() { if (!Cfg.TradeLive || Cfg.TradeHost == "") return 0; int i = Array.FindIndex(Brokers, b => b[1] == Cfg.TradeHost); return i >= 0 ? i + 1 : Brokers.Length + 1; }
        void SetBroker(int i)
        {
            if (i <= 0) { Cfg.TradeLive = false; Cfg.TradeHost = ""; }
            else if (i <= Brokers.Length) { Cfg.TradeHost = Brokers[i - 1][1]; Cfg.TradeLive = true; }
            else { string h = Cfg.CleanHost(Ask(L.T("Your broker's web address (e.g. mybroker.com)"), Cfg.TradeHost) ?? ""); if (h == "") return; Cfg.TradeHost = h; Cfg.TradeLive = true; }
            Cfg.Save(); if (tradeCanvas != null) tradeCanvas.Invalidate();
        }
        static string BrokerName(string host) { var b = Brokers.FirstOrDefault(z => z[1] == host); return b != null ? b[0] : host; }

        void PaintTrade(Graphics g)
        {
            g.TranslateTransform(tradeCanvas.AutoScrollPosition.X, tradeCanvas.AutoScrollPosition.Y);
            Begin(tradeCanvas);
            float x = 18 * S, y = 16 * S, w = tradeCanvas.ClientSize.Width - 36 * S;
            bool active = Cfg.TradeMode != "casual";
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("Trading"), Draw.fTitle, ink, x, y);
                // Active trader | Casual investor
                float sx = x + w; string[] modes = { "casual", "active" }; string[] names = { "Casual investor", "Active trader" };
                for (int i = 0; i < 2; i++)
                {
                    string t = L.T(names[i]); var ts = g.MeasureString(t, Draw.fBodyB); var r = new RectangleF(sx - ts.Width - 28 * S, y - 2 * S, ts.Width + 28 * S, 32 * S); sx = r.X - 6 * S;
                    bool on = (modes[i] == "active") == active;
                    Theme.Fill(g, r, 16 * S, on ? Kit.Accent("trade") : Theme.Soft); using (var b = new SolidBrush(on ? Theme.OnBlue : Theme.Ink)) g.DrawString(t, Draw.fBodyB, b, r.X + 14 * S, r.Y + (32 * S - ts.Height) / 2);
                    string m = modes[i]; Act(tradeCanvas, r, delegate { Cfg.TradeMode = m; Cfg.Save(); tradeCanvas.Invalidate(); });
                }
                y += 32 * S;
                string lead = active ? Voice.P("Live: your line to your broker, second by second while the market is open. Below: the day's numbers.", "Live broker connection in market hours; end-of-day numbers below.") : Voice.P("The day's numbers after the close, plus how your connection to your broker held up.", "End-of-day numbers and your broker connection's record.");
                g.DrawString(lead + " " + L.T("Not advice - just the numbers."), Draw.fBody, sub, new RectangleF(x, y, w, 40 * S)); y += 36 * S;
                if (Cfg.TradeLive && Cfg.TradeHost != "")
                {
                    int tlv; Trading.Headline(out tlv); bool openNow = Trading.MarketOpen(Trading.EasternNow()) || Cfg.TradeCrypto;
                    y = PaintModeLine(g, !openNow ? "trade.closed" : tlv == 2 ? "trade.slow" : "trade.ok", null, tlv == 2, x, y, w);
                }
                if (active) y = PaintBrokerLive(g, x, y, w, true);
                string hol = Markets.TodayNote(Trading.EasternNow());
                if (hol != "") { var hr = new RectangleF(x, y, w, 34 * S); Theme.Fill(g, hr, 12 * S, Theme.LvSoft(1)); using (var hb = new SolidBrush(Theme.LvText(1))) g.DrawString(hol, Draw.fBodyB, hb, hr.X + 14 * S, hr.Y + 8 * S); y = hr.Bottom + 14 * S; }
                y = PaintFx(g, x, y, w);
                y = PaintCommodities(g, x, y, w);
                if (!active) y = PaintBrokerLive(g, x, y, w, false);
                var past = WatchKit.Recent("trade").Take(10).ToList();
                if (past.Count > 0)
                {
                    g.DrawString(L.T("YOUR BROKER CONNECTION · PAST TRADING DAYS"), Draw.fTiny, muted, x, y); y += 20 * S;
                    float rh = 34 * S; var card = new RectangleF(x, y, w, past.Count * rh + 12 * S); Kit.Box(g, card); y += 6 * S;
                    foreach (var s in past)
                    {
                        g.DrawString(s.Start.ToString("ddd d MMM", CultureInfo.CurrentCulture), Draw.fSmall, muted, x + 18 * S, y + 9 * S);
                        g.DrawString(BrokerName(s.Far) + " · " + L.T("usual ") + Ms(s.FarNormal) + " ms", Draw.fBodyB, ink, x + 120 * S, y + 8 * S);
                        string tt = s.Dips.Count == 0 ? L.T("no slow moments") : s.Dips.Count + L.T(" slow moments · ") + string.Join(" · ", s.Tally().Where(kv => kv.Value > 0).Select(kv => WatchKit.BlameName(kv.Key, Trading.FarName).ToLowerInvariant() + " " + kv.Value));
                        var ts = g.MeasureString(tt, Draw.fSmall); var p = new RectangleF(x + w - ts.Width - 34 * S, y + 6 * S, ts.Width + 18 * S, 22 * S);
                        if (s.Dips.Count > 0) { var ss0 = s; var ivs = g.MeasureString(L.T("Investigate this ›"), Draw.fBodyB); InvLink(g, tradeCanvas, p.X - ivs.Width - 16 * S, y + 7 * S, "trade", "trade", BrokerName(ss0.Far) + " " + ss0.Start.ToString("ddd d MMM", CultureInfo.CurrentCulture) + ": " + tt, ss0.Start, ss0.End > ss0.Start ? ss0.End : ss0.Start.AddHours(7)); }   // a side door
                        Theme.Fill(g, p, 11 * S, Theme.LvSoft(s.Dips.Count == 0 ? 0 : 1)); using (var pb = new SolidBrush(Theme.LvText(s.Dips.Count == 0 ? 0 : 1))) g.DrawString(tt, Draw.fSmall, pb, p.X + 9 * S, p.Y + (22 * S - ts.Height) / 2);
                        y += rh;
                    }
                    y += 22 * S;
                }
                g.DrawString(L.T("Stock lists and market movers arrive once their free data sources are confirmed."), Draw.fSmall, muted, x, y); y += 30 * S;
            }
            Need(tradeCanvas, y);
        }

        float PaintBrokerLive(Graphics g, float x, float y, float w, bool big)
        {
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                if (!Cfg.TradeLive || Cfg.TradeHost == "")
                {
                    // set-up: pick the broker, then switch on
                    float h = big ? 210 * S : 150 * S; var card = new RectangleF(x, y, w, h); Kit.Box(g, card);
                    float cx = x + 22 * S, cy = y + 18 * S;
                    g.DrawString(L.T("Watch your line to your broker"), Draw.fName, ink, cx, cy); cy += 28 * S;
                    g.DrawString(L.T("A light knock on your broker's server") + " " + L.T("every") + " " + Trading.Sec + " s " + L.T("(your preset's pace; every 2 s with Details on), only 9:30-16:00 Eastern on weekdays (or 24/7 for crypto, if you choose). It never logs in or sees your account."), Draw.fBody, sub, new RectangleF(cx, cy, w - 44 * S, 44 * S)); cy += 48 * S;
                    float sbx = cx;
                    foreach (var b in Brokers)
                    {
                        var ts = g.MeasureString(b[0], Draw.fBody); var r = new RectangleF(sbx, cy, ts.Width + 24 * S, 32 * S);
                        if (r.Right > x + w - 20 * S) { sbx = cx; cy += 40 * S; r = new RectangleF(sbx, cy, ts.Width + 24 * S, 32 * S); }
                        bool on = Cfg.TradeHost == b[1];
                        Theme.Fill(g, r, 16 * S, on ? Kit.Accent("trade") : Theme.Soft); using (var bb = new SolidBrush(on ? Theme.OnBlue : Theme.Ink)) g.DrawString(b[0], Draw.fBody, bb, r.X + 12 * S, r.Y + (32 * S - ts.Height) / 2);
                        string host = b[1]; Act(tradeCanvas, r, delegate { Cfg.TradeHost = host; Cfg.TradeLive = true; Cfg.Save(); tradeCanvas.Invalidate(); });
                        sbx = r.Right + 8 * S;
                    }
                    var os = g.MeasureString(L.T("Other..."), Draw.fBody); var orr = new RectangleF(sbx, cy, os.Width + 24 * S, 32 * S);
                    if (orr.Right > x + w - 20 * S) { sbx = cx; cy += 40 * S; orr = new RectangleF(sbx, cy, os.Width + 24 * S, 32 * S); }
                    Kit.Box(g, orr, 16 * S); g.DrawString(L.T("Other..."), Draw.fBody, ink, orr.X + 12 * S, orr.Y + (32 * S - os.Height) / 2);
                    Act(tradeCanvas, orr, delegate { AskBroker(new Point((int)orr.X, (int)(orr.Bottom + tradeCanvas.AutoScrollPosition.Y))); });
                    float bottom = Math.Max(card.Bottom, cy + 48 * S);
                    if (bottom > card.Bottom) { /* the chips wrapped: the card grows on the next paint */ }
                    return bottom + 22 * S;
                }
                int lv; string head = Trading.Headline(out lv); var et = Trading.EasternNow(); bool open = Trading.MarketOpen(et);
                List<WatchKit.Sample> samples; WatchKit.Session day; lock (Trading.k) { samples = Trading.Samples.ToList(); day = Trading.Day; }
                bool live = samples.Count > 0 && (DateTime.Now - samples.Last().T).TotalSeconds < 10;
                float hh = big ? 300 * S : 120 * S; var c2 = new RectangleF(x, y, w, hh); Kit.Box(g, c2); if (live) Theme.Stroke(g, c2, 16 * S, Theme.Lv(lv), 2);
                float cx2 = x + 22 * S, cy2 = y + 18 * S;
                Draw.LvDot(g, live ? lv : 3, cx2, cy2 + 8 * S, 14 * S, 14 * S);
                g.DrawString(live ? (head.Contains("answering in") ? L.T(head.Split('·')[0].Trim()) + " · " + BrokerName(Cfg.TradeHost) + head.Substring(head.IndexOf("answering", StringComparison.Ordinal) - 1) : L.T(head)) : BrokerName(Cfg.TradeHost) + (open || Cfg.TradeCrypto ? L.T(" · starting...") : L.T(" · market closed - the live check sleeps until 9:30 Eastern")), Draw.fTitle, ink, cx2 + 24 * S, cy2);
                string tag = open ? L.T("LIVE · MARKET OPEN") : Cfg.TradeCrypto ? L.T("LIVE · CRYPTO 24/7") : L.T("MARKET CLOSED"); var tgs = g.MeasureString(tag, Draw.fTiny);
                var tgr = new RectangleF(x + w - tgs.Width - 40 * S, cy2 + 2 * S, tgs.Width + 18 * S, 22 * S); Theme.Fill(g, tgr, 11 * S, open || Cfg.TradeCrypto ? Theme.Red : Theme.Soft);
                using (var tb = new SolidBrush(open || Cfg.TradeCrypto ? Color.White : Theme.Sub)) g.DrawString(tag, Draw.fTiny, tb, tgr.X + 9 * S, tgr.Y + (22 * S - tgs.Height) / 2);
                cy2 += 40 * S;
                if (big)
                {
                    var far = samples.Where(z => !double.IsNaN(z.Far)).Select(z => z.Far).ToList(); var last = samples.LastOrDefault();
                    double jit = far.Count >= 3 ? Enumerable.Range(1, far.Count - 1).Average(i => Math.Abs(far[i] - far[i - 1])) : double.NaN;
                    int per = Math.Max(1, 60 / Trading.Sec); int lost = samples.Skip(Math.Max(0, samples.Count - per)).Count(z => z.FarLost); int n = Math.Min(per, samples.Count);
                    var lastDip = day != null && day.Dips.Count > 0 ? day.Dips.Last() : null;
                    string[][] tiles = {
                        new[] { "BROKER DELAY", last == null || double.IsNaN(last.Far) ? "-" : Ms(last.Far) + " ms", day == null || double.IsNaN(day.FarNormal) ? "" : L.T("usual ") + Ms(day.FarNormal) },
                        new[] { "JITTER", double.IsNaN(jit) ? "-" : Ms(jit) + " ms", double.IsNaN(jit) ? "" : jit < 10 ? L.T("steady") : L.T("jumpy") },
                        new[] { "LOSS (1 MIN)", n == 0 ? "-" : (lost * 100 / n).ToString(CultureInfo.CurrentCulture) + "%", lost == 0 ? L.T("no drops") : lost + L.T(" lost") },
                        new[] { "YOUR ROUTER", last == null ? "-" : last.RouterLost ? L.T("no reply") : double.IsNaN(last.Router) ? "-" : Ms(last.Router) + " ms", last != null && last.Wifi && last.Signal >= 0 ? "Wi-Fi " + last.Signal + "%" : L.T("cable") },
                        new[] { "LAST HICCUP", lastDip == null ? "-" : lastDip.T.ToString("HH:mm", CultureInfo.CurrentCulture), lastDip == null ? L.T("none today") : WatchKit.BlameName(lastDip.Blame, Trading.FarName) } };
                    float tw = (w - 44 * S - 4 * 10 * S) / 5;
                    for (int i = 0; i < 5; i++)
                    {
                        var tr = new RectangleF(cx2 + i * (tw + 10 * S), cy2, tw, 66 * S); Theme.Fill(g, tr, 12 * S, Theme.Soft);
                        g.DrawString(L.T(tiles[i][0]), Draw.fTiny, muted, tr.X + 12 * S, tr.Y + 9 * S); g.DrawString(tiles[i][1], Draw.fName, ink, tr.X + 12 * S, tr.Y + 25 * S);
                        if (tiles[i][2] != "") using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter }) g.DrawString(tiles[i][2], Draw.fSmall, sub, new RectangleF(tr.X + 12 * S, tr.Y + 47 * S, tw - 20 * S, 18 * S), nf);
                    }
                    cy2 += 80 * S;
                    g.DrawString(L.T("LAST 10 MINUTES") + " · " + L.T("one check every") + " " + Trading.Sec + " s · " + L.T("dashed = 100 ms, the \"wait\" line"), Draw.fTiny, muted, cx2, cy2); cy2 += 18 * S;
                    var ch = new RectangleF(cx2, cy2, w - 44 * S, 70 * S); double mx = Math.Max(150, far.DefaultIfEmpty(0).Max() * 1.1);
                    using (var dp = new Pen(Theme.Amber, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash }) { float ly = ch.Bottom - (float)(100 / mx) * ch.Height; g.DrawLine(dp, ch.X, ly, ch.Right, ly); }
                    if (samples.Count >= 2)
                    {
                        var t0 = samples[0].T; double span = Math.Max(30, (samples.Last().T - t0).TotalSeconds);
                        var pts = samples.Where(z => !double.IsNaN(z.Far)).Select(z => new PointF(ch.X + (float)((z.T - t0).TotalSeconds / span) * ch.Width, ch.Bottom - (float)(Math.Min(z.Far, mx) / mx) * ch.Height)).ToArray();
                        if (pts.Length >= 2) using (var p = new Pen(Kit.Accent("trade"), 2 * S)) g.DrawLines(p, pts);
                        foreach (var z in samples.Where(q => q.FarLost)) using (var rb = new SolidBrush(Theme.Red)) g.FillEllipse(rb, ch.X + (float)((z.T - t0).TotalSeconds / span) * ch.Width - 3 * S, ch.Bottom - 6 * S, 6 * S, 6 * S);
                    }
                    else g.DrawString(open || Cfg.TradeCrypto ? L.T("Gathering the first seconds...") : L.T("The chart fills while the market is open."), Draw.fSmall, muted, ch.X, ch.Y + 24 * S);
                }
                else
                {
                    string one = day == null ? L.T("No checks yet today.") : day.Dips.Count == 0 ? L.T("Fine all session so far.") : L.T("Slow moments today: ") + day.Dips.Count + " · " + L.T("last at ") + day.Dips.Last().T.ToString("HH:mm", CultureInfo.CurrentCulture) + " - " + day.Dips.Last().Evidence;
                    g.DrawString(one, Draw.fBody, sub, new RectangleF(cx2, cy2, w - 44 * S, 40 * S));
                }
                // switches: alerts, crypto 24/7, stop
                float bx = cx2, by = c2.Bottom - 44 * S;
                foreach (var sw in new[] { new[] { "alert", Cfg.TradeAlert ? "Alerts on" : "Alerts off" }, new[] { "crypto", Cfg.TradeCrypto ? "Crypto 24/7: on" : "Crypto 24/7: off" }, new[] { "change", "Change broker" }, new[] { "stop", "Stop watching" } })
                {
                    if (!big && sw[0] != "change" && sw[0] != "stop") continue;
                    var ts = g.MeasureString(L.T(sw[1]), Draw.fSmall); var r = new RectangleF(bx, by, ts.Width + 22 * S, 28 * S); Theme.Fill(g, r, 14 * S, Theme.Soft);
                    g.DrawString(L.T(sw[1]), Draw.fSmall, ink, r.X + 11 * S, r.Y + (28 * S - ts.Height) / 2); bx = r.Right + 8 * S;
                    string id = sw[0];
                    Act(tradeCanvas, r, delegate
                    {
                        if (id == "alert") Cfg.TradeAlert = !Cfg.TradeAlert; else if (id == "crypto") Cfg.TradeCrypto = !Cfg.TradeCrypto; else if (id == "change") Cfg.TradeLive = false; else { Cfg.TradeLive = false; Cfg.TradeHost = ""; }
                        Cfg.Save(); tradeCanvas.Invalidate();
                    });
                }
                if (Trading.Status != "") using (var ab = new SolidBrush(Theme.LvText(1))) g.DrawString(Trading.Status, Draw.fSmall, ab, bx + 10 * S, by + 6 * S);
                return c2.Bottom + 22 * S;
            }
        }
        void AskBroker(Point at)
        {
            var menu = new ContextMenuStrip { ShowImageMargin = false, Font = Draw.fBody };
            var box = new ToolStripTextBox { AutoSize = false, Width = Sc(260), Font = Draw.fBody };
            menu.Items.Add(new ToolStripLabel(L.T("Your broker's web address (e.g. mybroker.com)"))); menu.Items.Add(box);
            Action go = () => { string h = Cfg.CleanHost(box.Text); if (h == "") return; Cfg.TradeHost = h; Cfg.TradeLive = true; Cfg.Save(); tradeCanvas.Invalidate(); };
            var ok = new ToolStripMenuItem(L.T("Watch it")); ok.Click += delegate { go(); }; menu.Items.Add(ok);
            box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { go(); menu.Close(); } };
            menu.Closed += delegate { try { if (!IsDisposed) BeginInvoke((Action)menu.Dispose); else menu.Dispose(); } catch (InvalidOperationException) { menu.Dispose(); } };
            menu.Show(tradeCanvas, at); box.Focus();
        }

        // weekly commodities: one card per group - this week's change, four weeks' change, and the five-week line
        static float PaintCommodities(Graphics g, float x, float y, float w)
        {
            if (!Cfg.TradeFx) return y;
            List<Trading.Index> idx; lock (Trading.k) idx = Trading.Indexes.ToList();
            if (idx.Count == 0) return y;   // first visit: the rates' card already says it's fetching
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("COMMODITIES · WEEKLY · CHANGE IN PRICE"), Draw.fTiny, muted, x, y); y += 20 * S;
                int cols = w > 900 * S ? 5 : w > 560 * S ? 3 : 2; float cw = (w - (cols - 1) * 10 * S) / cols, chh = 112 * S;
                for (int i = 0; i < idx.Count; i++)
                {
                    var it = idx[i]; var rr = new RectangleF(x + (i % cols) * (cw + 10 * S), y + (i / cols) * (chh + 10 * S), cw, chh); Kit.Box(g, rr, 14 * S);
                    string nm, what; Trading.IdxNames.TryGetValue(it.Code, out nm); Trading.IdxWhat.TryGetValue(it.Code, out what);
                    g.DrawString(L.T(nm ?? it.Code), Draw.fBodyB, ink, rr.X + 14 * S, rr.Y + 10 * S);
                    using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter }) g.DrawString(L.T(what ?? ""), Draw.fSmall, muted, new RectangleF(rr.X + 14 * S, rr.Y + 30 * S, cw - 24 * S, 18 * S), nf);
                    double wk = Trading.Change(it, 1), mo = Trading.Change(it, 4);
                    string wt = double.IsNaN(wk) ? "-" : (wk > 0.005 ? "▲ " : wk < -0.005 ? "▼ " : "") + Math.Abs(wk).ToString("0.0", CultureInfo.CurrentCulture) + "%";
                    using (var cb = new SolidBrush(double.IsNaN(wk) || Math.Abs(wk) < 0.05 ? Theme.Ink : Theme.LvText(wk > 0 ? 0 : 2))) g.DrawString(wt, Draw.fName, cb, rr.X + 14 * S, rr.Y + 52 * S);
                    g.DrawString(L.T("week") + (double.IsNaN(mo) ? "" : " · " + L.T("4 weeks ") + (mo > 0 ? "+" : "") + mo.ToString("0.0", CultureInfo.CurrentCulture) + "%"), Draw.fSmall, sub, rr.X + 14 * S, rr.Y + 84 * S);
                    // the five-week line, scaled to its own range so a small move still shows
                    if (it.V.Length >= 2)
                    {
                        var sr = new RectangleF(rr.Right - 74 * S, rr.Y + 54 * S, 60 * S, 26 * S); double lo = it.V.Min(), hi = it.V.Max(), span = Math.Max(hi - lo, hi * 0.002);
                        var pts = it.V.Select((v, j) => new PointF(sr.X + j * sr.Width / (it.V.Length - 1), sr.Bottom - (float)((v - lo) / span) * sr.Height)).ToArray();
                        using (var p = new Pen(Kit.Accent("trade"), 2 * S)) g.DrawLines(p, pts);
                    }
                }
                y += ((idx.Count + cols - 1) / cols) * (chh + 10 * S);
                g.DrawString(L.T("Source: Bank of Canada commodity price index, week of ") + idx.Max(z => z.Day).ToString("d MMM", CultureInfo.CurrentCulture) + L.T(" (posted about a week later). An index of world prices in US dollars - the change is what to read. ▲ = prices went up."), Draw.fSmall, muted, new RectangleF(x, y, w, 40 * S));
                return y + 44 * S;
            }
        }

        float PaintFx(Graphics g, float x, float y, float w)
        {
            if (!Cfg.TradeFx) return y;
            Trading.LoadFx(); if (Trading.FxStale() || Trading.IdxStale()) Trading.FetchFx(() => { try { if (!IsDisposed) BeginInvoke((Action)delegate { if (tradeCanvas != null) tradeCanvas.Invalidate(); }); } catch (InvalidOperationException) { } });
            List<Trading.Rate> rates; string note; lock (Trading.k) { rates = Trading.Rates.ToList(); note = Trading.FxNote; }
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("CURRENCIES · END OF DAY · 1 UNIT IN CANADIAN DOLLARS"), Draw.fTiny, muted, x, y); y += 20 * S;
                int cols = w > 900 * S ? 5 : 3; float cw = (w - (cols - 1) * 10 * S) / cols, chh = 74 * S;
                if (rates.Count == 0) { var e = new RectangleF(x, y, w, 50 * S); Kit.Box(g, e); g.DrawString(note != "" ? note : Policy.NoOnline ? L.T("Online lookups are turned off by your IT team.") : L.T("Getting today's rates from the Bank of Canada..."), Draw.fBody, sub, x + 18 * S, y + 15 * S); return e.Bottom + 22 * S; }
                for (int i = 0; i < rates.Count; i++)
                {
                    var r = rates[i]; var rr = new RectangleF(x + (i % cols) * (cw + 10 * S), y + (i / cols) * (chh + 10 * S), cw, chh); Kit.Box(g, rr, 14 * S);
                    string nm; Trading.FxNames.TryGetValue(r.Code, out nm);
                    g.DrawString(r.Code + " · " + L.T(nm ?? ""), Draw.fSmall, sub, rr.X + 14 * S, rr.Y + 10 * S);
                    g.DrawString(r.Cad.ToString(r.Cad < 0.1 ? "0.00000" : "0.0000", CultureInfo.CurrentCulture), Draw.fName, ink, rr.X + 14 * S, rr.Y + 30 * S);
                    if (!double.IsNaN(r.Prev) && r.Prev > 0)
                    {
                        double ch = (r.Cad - r.Prev) / r.Prev * 100; int lv = Math.Abs(ch) < 0.005 ? 3 : ch > 0 ? 0 : 2;
                        string t = (ch > 0 ? "▲ " : ch < 0 ? "▼ " : "") + Math.Abs(ch).ToString("0.00", CultureInfo.CurrentCulture) + "%"; var ts = g.MeasureString(t, Draw.fSmall);
                        using (var cb = new SolidBrush(Theme.LvText(lv))) g.DrawString(t, Draw.fSmall, cb, rr.Right - ts.Width - 12 * S, rr.Y + 36 * S);
                    }
                }
                y += ((rates.Count + cols - 1) / cols) * (chh + 10 * S);
                var day = rates.Max(z => z.Day);
                g.DrawString(L.T("Source: Bank of Canada daily exchange rates, ") + day.ToString("ddd d MMM", CultureInfo.CurrentCulture) + L.T(" - indicative rates, not for trading. ▲ = the currency got dearer in CAD since the day before.") + (note != "" ? " " + note : ""), Draw.fSmall, muted, new RectangleF(x, y, w, 40 * S));
                y += 44 * S;
                // opt-in: the corner widget shows the close for a while each weekday evening, then goes back by itself
                string et = L.T("Evening summary in the widget, 17:30-19:00: ") + (Cfg.TradeEvening ? L.T("on") : L.T("off")); var es = g.MeasureString(et, Draw.fSmall);
                var er = new RectangleF(x, y - 6 * S, es.Width + 22 * S, 28 * S); Theme.Fill(g, er, 14 * S, Cfg.TradeEvening ? Kit.Accent("trade") : Theme.Soft);
                using (var eb = new SolidBrush(Cfg.TradeEvening ? Theme.OnBlue : Theme.Ink)) g.DrawString(et, Draw.fSmall, eb, er.X + 11 * S, er.Y + (28 * S - es.Height) / 2);
                Act(tradeCanvas, er, delegate { Cfg.TradeEvening = !Cfg.TradeEvening; Cfg.Save(); tradeCanvas.Invalidate(); });
                g.DrawString(L.T("Shows the US dollar and energy's week instead of \"All quiet\". A problem always takes the spot back."), Draw.fSmall, muted, er.Right + 10 * S, er.Y + 6 * S);
                return y + 36 * S;
            }
        }
    }
}
