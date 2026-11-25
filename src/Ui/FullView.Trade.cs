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
        string tradeVer = "";
        void TradeTick()
        {
            if (tab != "trade" || tradeCanvas == null) return;
            // a new broker sample only moves the live broker card (Line Check); the verdict level, a new dip, new rates or
            // index numbers, the minute or anything else redraws the page
            int tlv; Trading.Headline(out tlv); var day = Trading.Day;
            string ver = System.Threading.Interlocked.Read(ref Trading.Ver).ToString(CultureInfo.InvariantCulture);
            string k = tlv + "|" + (day == null ? "-" : day.Start.Ticks + ":" + day.Dips.Count) + "|" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Trading.Rates) + "|" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Trading.Indexes) + "|" + Trading.FxNote + "|" + System.Threading.Interlocked.Read(ref WatchKit.CacheVer) + "|" + System.Threading.Interlocked.Read(ref MarketData.Ver) + "|" + DateTime.Now.Minute + "|" + Cfg.TradeLive + "|" + StreamCheck.Running + "|" + mkTab;
            if (k != tradeKey) { tradeKey = k; tradeVer = ver; tradeCanvas.Invalidate(); }
            else if (ver != tradeVer) { tradeVer = ver; if (mkTab == "line" && !RedrawSpots(tradeCanvas, "tr.live")) tradeCanvas.Invalidate(); }   // other tabs show no per-sample numbers (the NOW line keeps itself live)
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

        // Markets › Line Check, under the verdict: the live line to the broker (pick one first), today's market note and
        // the past trading days - the 1.1.5 Trading page's own parts, unchanged
        float PaintLineDetail(Graphics g, float x, float y, float w)
        {
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                if (Cfg.TradeLive && Cfg.TradeHost != "")
                {
                    int tlv; Trading.Headline(out tlv); bool openNow = Trading.MarketOpen(Trading.EasternNow()) || Cfg.TradeCrypto;
                    if (!openNow || tlv == 0 || tlv == 2) y = PaintModeLine(g, !openNow ? "trade.closed" : tlv == 2 ? "trade.slow" : "trade.ok", null, tlv == 2, x, y, w);   // AM-CG-106: starting, stale or unknown says nothing, never "ok"
                }
                float by0 = y; y = PaintBrokerLive(g, x, y, w, true); LiveSpot(tradeCanvas, "tr.live", new RectangleF(x - 4 * S, by0, w + 8 * S, y - by0));   // a new broker sample redraws only this card (TradeTick)
                string hol = Markets.TodayNote(Trading.EasternNow());
                if (hol != "") { var hr = new RectangleF(x, y, w, 34 * S); Theme.Fill(g, hr, 12 * S, Theme.LvSoft(1)); using (var hb = new SolidBrush(Theme.LvText(1))) g.DrawString(hol, Draw.fBodyB, hb, hr.X + 14 * S, hr.Y + 8 * S); y = hr.Bottom + 14 * S; }
                var past = WatchKit.Recent("trade").Take(10).ToList();
                if (past.Count > 0)
                {
                    g.DrawString(Kit.Sent("YOUR BROKER CONNECTION · PAST TRADING DAYS"), Draw.fTiny, muted, x, y); y += 20 * S;
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
            }
            return y;
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
                List<WatchKit.Sample> samples; WatchKit.Session day; lock (Trading.k) { samples = Trading.Samples.ToList(); day = Trading.Day == null ? null : Trading.Day.Copy(); }   // AM-CG-131: a copy, read while the watch adds
                bool live = samples.Count > 0 && (DateTime.Now - samples.Last().T).TotalSeconds < Math.Max(15, Trading.Sec * 3);   // AM-CG-116: fresh for three rounds of the watch, whatever its pace
                float hh = big ? 324 * S : 144 * S; var c2 = new RectangleF(x, y, w, hh);   /* 24 Nov: + room for the Market: row under the tag (it ran into the tiles) */ Kit.Box(g, c2); if (live) Theme.Stroke(g, c2, 16 * S, Theme.Lv(lv), 2);
                float cx2 = x + 22 * S, cy2 = y + 18 * S;
                Draw.LvDot(g, live ? lv : 3, cx2, cy2 + 8 * S, 14 * S, 14 * S);
                g.DrawString(live ? (head.Contains("answering in") ? L.T(head.Split('·')[0].Trim()) + " · " + BrokerName(Cfg.TradeHost) + head.Substring(head.IndexOf("answering", StringComparison.Ordinal) - 1) : L.T(head)) : BrokerName(Cfg.TradeHost) + (open || Cfg.TradeCrypto ? L.T(" · starting...") : L.T(" · market closed - the live check sleeps until 9:30 Eastern")), Draw.fTitle, ink, cx2 + 24 * S, cy2);
                string tag = open ? Kit.Sent("LIVE") + " · " + Kit.Sent(Trading.VenueName) + " " + Kit.Sent("OPEN").ToLower(CultureInfo.CurrentCulture) : Cfg.TradeCrypto ? Kit.Sent("LIVE · CRYPTO 24/7") : Kit.Sent(Trading.VenueName) + " " + Kit.Sent("CLOSED").ToLower(CultureInfo.CurrentCulture); var tgs = g.MeasureString(tag, Draw.fTiny);
                var tgr = new RectangleF(x + w - tgs.Width - 40 * S, cy2 + 2 * S, tgs.Width + 18 * S, 22 * S); Theme.Fill(g, tgr, 11 * S, open || Cfg.TradeCrypto ? Theme.Red : Theme.Soft);
                using (var tb = new SolidBrush(open || Cfg.TradeCrypto ? Color.White : Theme.Sub)) g.DrawString(tag, Draw.fTiny, tb, tgr.X + 9 * S, tgr.Y + (22 * S - tgs.Height) / 2);
                // which market "open" means (AM-CG-108): a US-only trader picks US, a Toronto-only trader Toronto
                {
                    float vx = x + w - 22 * S; var opts = new[] { new[] { "both", "Either" }, new[] { "ca", "Toronto" }, new[] { "us", "US" } };
                    foreach (var o in opts)
                    {
                        bool on = Cfg.TradeVenue == o[0]; string t = L.T(o[1]); var ts = g.MeasureString(t, on ? Draw.fTiny : Draw.fTiny); vx -= ts.Width + 10 * S;
                        var vr = new RectangleF(vx - 4 * S, tgr.Bottom + 6 * S, ts.Width + 8 * S, 18 * S); if (on) Theme.Fill(g, vr, 9 * S, Kit.Soft("trade"));
                        using (var vb = new SolidBrush(on ? Kit.Ink("trade") : Theme.Muted)) g.DrawString(t, Draw.fTiny, vb, vx, vr.Y + 1 * S);
                        string id = o[0]; Act(tradeCanvas, vr, delegate { Cfg.TradeVenue = id; Cfg.Save(); tradeCanvas.Invalidate(); });
                    }
                    using (var mb = new SolidBrush(Theme.Muted)) { var ms = g.MeasureString(L.T("Market:"), Draw.fTiny); g.DrawString(L.T("Market:"), Draw.fTiny, mb, vx - ms.Width - 6 * S, tgr.Bottom + 7 * S); }
                }
                cy2 += 64 * S;
                if (big)
                {
                    var far = samples.Where(z => !double.IsNaN(z.Far)).Select(z => z.Far).ToList(); var last = samples.LastOrDefault();
                    double jit = far.Count >= 3 ? Enumerable.Range(1, far.Count - 1).Average(i => Math.Abs(far[i] - far[i - 1])) : double.NaN;
                    int per = Math.Max(1, 60 / Trading.Sec); var lastMin = samples.Skip(Math.Max(0, samples.Count - per)).Where(z => !z.NoProbe).ToList(); int lost = lastMin.Count(z => z.FarLost); int n = lastMin.Count;   // AM-CG-117: a round that couldn't try isn't a reply
                    var lastDip = day != null && day.Dips.Count > 0 ? day.Dips.Last() : null;
                    string[][] tiles = {
                        new[] { "BROKER DELAY", last == null || double.IsNaN(last.Far) ? "-" : Ms(last.Far) + " ms", day == null || double.IsNaN(day.FarNormal) ? "" : L.T("usual ") + Ms(day.FarNormal) },
                        new[] { "JITTER", double.IsNaN(jit) ? "-" : Ms(jit) + " ms", double.IsNaN(jit) ? "" : jit < 10 ? L.T("steady") : L.T("jumpy") },
                        new[] { "LOSS (1 MIN)", n == 0 ? "-" : (lost * 100 / n).ToString(CultureInfo.CurrentCulture) + "%", n == 0 ? L.T("not measured") : lost == 0 ? L.T("no drops") : lost + L.T(" lost") },
                        new[] { "YOUR ROUTER", last == null ? "-" : last.RouterLost ? L.T("no reply") : double.IsNaN(last.Router) ? "-" : Ms(last.Router) + " ms", last == null ? "" : last.Wifi ? (last.Signal >= 0 ? "Wi-Fi " + last.Signal + "%" : L.T("Wi-Fi, signal hidden")) : L.T("cable") },   // AM-CG-129: Wi-Fi with location off is still Wi-Fi
                        new[] { "LAST HICCUP", lastDip == null ? "-" : lastDip.T.ToString("HH:mm", CultureInfo.CurrentCulture), lastDip == null ? L.T("none today") : WatchKit.BlameName(lastDip.Blame, Trading.FarName) } };
                    float tw = (w - 44 * S - 4 * 10 * S) / 5;
                    for (int i = 0; i < 5; i++)
                    {
                        var tr = new RectangleF(cx2 + i * (tw + 10 * S), cy2, tw, 66 * S); Theme.Fill(g, tr, 12 * S, Theme.Soft);
                        g.DrawString(Kit.Sent(tiles[i][0]), Draw.fTiny, muted, tr.X + 12 * S, tr.Y + 9 * S); g.DrawString(tiles[i][1], Draw.fName, ink, tr.X + 12 * S, tr.Y + 25 * S);
                        if (tiles[i][2] != "") using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter }) g.DrawString(tiles[i][2], Draw.fSmall, sub, new RectangleF(tr.X + 12 * S, tr.Y + 47 * S, tw - 20 * S, 18 * S), nf);
                    }
                    cy2 += 80 * S;
                    g.DrawString(Kit.Sent("LAST 10 MINUTES") + " · " + L.T("one check every") + " " + Trading.Sec + " s · " + L.T("dashed = 100 ms, the \"wait\" line"), Draw.fTiny, muted, cx2, cy2); cy2 += 18 * S;
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
                    if (day != null && day.Dips.Count > 0 && Cfg.History) { var lt = day.Dips.Last().T; Act(tradeCanvas, new RectangleF(cx2, cy2, w - 44 * S, 40 * S), delegate { OpenTimeAt(lt, L.T("Markets") + " - " + L.T("slow moment")); }); }   // the last slow moment in the Time machine
                }
                // switches: alerts, crypto 24/7, stop
                float bx = cx2, by = c2.Bottom - 44 * S;
                foreach (var sw in new[] { new[] { "alert", Cfg.TradeAlert ? "Alerts on" : "Alerts off" }, new[] { "crypto", Cfg.TradeCrypto ? "Crypto 24/7: on" : "Crypto 24/7: off" }, new[] { "change", "Change broker" }, new[] { "path", "Show my path" }, new[] { "stop", "Stop watching" } })
                {
                    if (!big && sw[0] != "change" && sw[0] != "stop" && sw[0] != "path") continue;
                    var ts = g.MeasureString(L.T(sw[1]), Draw.fSmall); var r = new RectangleF(bx, by, ts.Width + 22 * S, 28 * S); Theme.Fill(g, r, 14 * S, Theme.Soft);
                    g.DrawString(L.T(sw[1]), Draw.fSmall, ink, r.X + 11 * S, r.Y + (28 * S - ts.Height) / 2); bx = r.Right + 8 * S;
                    string id = sw[0];
                    if (id == "path")   // 1.1.6: this PC to the broker and on to the exchange, in the Network Lab
                    {
                        var farV = samples.Where(z => !double.IsNaN(z.Far)).Select(z => z.Far).ToList(); double farMs = farV.Count > 0 ? farV.Min() : double.NaN; string host = Cfg.TradeHost;
                        string bname = (Brokers.FirstOrDefault(b => b[1] == host) ?? new[] { "Broker" })[0] + " gateway";
                        Act(tradeCanvas, r, delegate { OpenLab(LabLink.PathJson(M, "trade", host, bname, farMs, MinOf(M.GwPing), MinOf(M.NetPing)), false); });
                        continue;
                    }
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
            var menu = Menus.Make(); menu.ShowImageMargin = false;
            var box = new ToolStripTextBox { AutoSize = false, Width = Sc(260), Font = Draw.fBody };
            menu.Items.Add(new ToolStripLabel(L.T("Your broker's web address (e.g. mybroker.com)"))); menu.Items.Add(box);
            Action go = () => { string h = Cfg.CleanHost(box.Text); if (h == "") return; Cfg.TradeHost = h; Cfg.TradeLive = true; Cfg.Save(); tradeCanvas.Invalidate(); };
            var ok = new ToolStripMenuItem(L.T("Watch it")); ok.Click += delegate { go(); }; menu.Items.Add(ok);
            box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { go(); menu.Close(); } };
            menu.Closed += delegate { try { if (!IsDisposed) BeginInvoke((Action)menu.Dispose); else menu.Dispose(); } catch (InvalidOperationException) { menu.Dispose(); } };
            menu.Show(tradeCanvas, at); box.Focus();
        }


    }
}
