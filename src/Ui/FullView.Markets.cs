// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Markets (1.1.6): THE MARKETS PAGE
    // An overview and five deeper tabs - World Markets, Currencies, Commodities, Line Check (highlighted: the line to your
    // broker is what AdeeboMeter itself can judge) and Casual Investor. The Line Check strip sits under every tab.
    // Numbers come from MarketData / Trading (the Bank of Canada, free and keyless; index levels only with the person's own
    // key). Fetched only while this page is open and the person has turned the numbers on. Numbers only - never advice.
    sealed partial class FullView
    {
        string mkTab = "", mkFx = "USD", mkRange = "1y", mkCvFrom = "CAD", mkCvTo = "USD"; double mkCvAmt = 500;
        double svStart = 5000, svMonthly = 200, svRate = 5, svYears = 20; double abAmt = 2000; string abCcy = "EUR";
        static readonly string[][] MkTabs = { new[] { "overview", "Overview" }, new[] { "world", "World Markets" }, new[] { "fx", "Currencies" }, new[] { "commod", "Commodities" }, new[] { "line", "Line Check", "hl" }, new[] { "casual", "Casual Investor" } };
        Monitor MkMon { get { return M; } }

        void MarketsFetch()
        {
            Action again = () => { try { if (!IsDisposed) BeginInvoke((Action)delegate { if (tradeCanvas != null) tradeCanvas.Invalidate(); }); } catch (Exception e) { if (!(e is InvalidOperationException || e is ObjectDisposedException)) throw; } };
            if (!Cfg.TradeFx || Policy.NoOnline) return;
            Trading.LoadFx(); Trading.FetchFx(again);
            MarketData.Fetch("CANADA", again); MarketData.Fetch("BCPI", again);
            if (mkTab == "fx" || mkTab == "casual" || mkTab == "overview") foreach (var c in MarketData.MyFx().Concat(new[] { mkFx, abCcy }).Distinct()) MarketData.Fetch("FX:" + c, again);
            if (mkTab == "commod") MarketData.Fetch("FX:USD", again);
            if (MarketData.HasQuoteKey() && (mkTab == "world" || mkTab == "overview")) MarketData.FetchQuotes(false, again);
            if (MarketData.HasQuoteKey() && mkTab == "commod") MarketData.FetchQuotes(true, again);
        }

        void PaintTrade(Graphics g)
        {
            if (mkTab == "") mkTab = Cfg.TradeMode == "casual" ? "casual" : "overview";
            var vis = new RectangleF(0, 0, tradeCanvas.ClientSize.Width, tradeCanvas.ClientSize.Height);
            Paper.Ground(g, vis);
            g.TranslateTransform(tradeCanvas.AutoScrollPosition.X, tradeCanvas.AutoScrollPosition.Y);
            Begin(tradeCanvas); Paper.Fonts(); MarketsFetch();
            float x = 22 * S, y = 16 * S, w = tradeCanvas.ClientSize.Width - 44 * S;
            y = Paper.Masthead(g, x, y, w, Kit.Sent(L.T("THE MARKETS PAGE")), L.T("AdeeboMeter Daily Financial Edition"), L.T("Your PC · Your Network · Your Markets"), L.T("All in One Place"), mkTab != "overview");
            List<Tuple<string, RectangleF>> hits; y = Paper.TabBar(g, x, y, w, MkTabs, mkTab, out hits);
            foreach (var h in hits) { string id = h.Item1; Act(tradeCanvas, h.Item2, delegate { mkTab = id; tradeCanvas.AutoScrollPosition = Point.Empty; tradeCanvas.Invalidate(); }); }
            y = TryThis(g, tradeCanvas, "trade", x, y + 6 * S, w, true);   // 24 Nov: no NOW line - the status chips at the top already say it
            if (mkTab != "line") y = LineStrip(g, x, y, w);
            bool need = mkTab != "line" && mkTab != "world";
            if (need && (!Cfg.TradeFx || Policy.NoOnline)) y = MkTurnOn(g, x, y, w);
            else switch (mkTab)
                {
                    case "world": y = MkWorld(g, x, y, w); break;
                    case "fx": y = MkCurrencies(g, x, y, w); break;
                    case "commod": y = MkCommodities(g, x, y, w); break;
                    case "line": y = MkLine(g, x, y, w); break;
                    case "casual": y = MkCasual(g, x, y, w); break;
                    default: y = MkOverview(g, x, y, w); break;
                }
            using (var mb = new SolidBrush(Paper.Muted)) { string foot = L.T("Numbers only - AdeeboMeter never tells you what to buy or sell."); var fs = g.MeasureString(foot, Paper.Italic); g.DrawString(foot, Paper.Italic, mb, x + (w - fs.Width) / 2, y + 6 * S); y += fs.Height + 30 * S; }
            Need(tradeCanvas, y);
        }

        float MkTurnOn(Graphics g, float x, float y, float w)
        {
            var r = new RectangleF(x, y, w, 120 * S); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 18 * S, y + 14 * S, "Currencies, commodities and Canada's rates", null);
            using (var ink = new SolidBrush(Paper.Ink)) g.DrawString(Policy.NoOnline ? L.T("Off - your IT team turned online lookups off.") : L.T("From the Bank of Canada: official, free, no account. Fetched only while this page is open, at most once after each day's update. Off until you turn it on."), Paper.Body, ink, new RectangleF(x + 18 * S, y + 50 * S, w - 220 * S, 60 * S));
            if (!Policy.NoOnline) { var br = Paper.Pill(g, L.T("Turn on"), x + w - 90 * S, y + 46 * S, Paper.Act); Act(tradeCanvas, br, delegate { Cfg.TradeFx = true; Cfg.Save(); tradeCanvas.Invalidate(); }); }
            return r.Bottom + 16 * S;
        }

        // ---------- the Line Check strip (every tab but its own)
        float LineStrip(Graphics g, float x, float y, float w)
        {
            var r = LineCheck.Now(MkMon); int lv = r.Score < 0 ? 3 : r.Score >= 80 ? 0 : r.Score >= 55 ? 1 : 2;
            var tip = r.Tips.FirstOrDefault(t => !t.Title.StartsWith("This PC is ready", StringComparison.Ordinal));
            Paper.StripHits sh; y = Paper.Strip(g, x, y, w, Kit.Sent(L.T("LINE CHECK")), true, LineCheck.Checking ? L.T("Checking your line...") : L.T(r.Verdict), r.Line, tip == null ? null : L.T(tip.Title), L.T("Check my line"), LineCheck.Checking ? 3 : lv, out sh);
            Act(tradeCanvas, sh.Button, delegate { if (Cfg.TradeHost == "") { mkTab = "line"; tradeCanvas.Invalidate(); return; } LineCheck.Check(() => { try { if (!IsDisposed) BeginInvoke((Action)delegate { tradeCanvas.Invalidate(); }); } catch (Exception e) { if (!(e is InvalidOperationException || e is ObjectDisposedException)) throw; } }); tradeCanvas.Invalidate(); });
            Act(tradeCanvas, sh.Details, delegate { mkTab = "line"; tradeCanvas.AutoScrollPosition = Point.Empty; tradeCanvas.Invalidate(); });
            return y;
        }

        // ---------- shared pieces
        static string F4(double v) { return double.IsNaN(v) ? "-" : v >= 100 ? v.ToString("N1", CultureInfo.CurrentCulture) : v >= 1 ? v.ToString("0.0000", CultureInfo.CurrentCulture) : v >= 0.01 ? v.ToString("0.00000", CultureInfo.CurrentCulture) : v.ToString("0.000000", CultureInfo.CurrentCulture); }
        static double FxNow(string c) { if (c == "CAD") return 1; var s = MarketData.Get("FX:" + c); if (s != null && s.Count > 0) return s.Last; lock (Trading.k) { var r = Trading.Rates.FirstOrDefault(z => z.Code == c); return r == null ? double.NaN : r.Cad; } }
        static double FxDay(string c)
        {
            lock (Trading.k) { var r = Trading.Rates.FirstOrDefault(z => z.Code == c); if (r != null && !double.IsNaN(r.Prev)) return MarketData.Pct(r.Cad, r.Prev); }
            var s = MarketData.Get("FX:" + c); return s != null && s.Count > 1 ? MarketData.Pct(s.V[s.Count - 1], s.V[s.Count - 2]) : double.NaN;
        }
        // Keep It Light (R2-068: the two ticks did nothing): once a minute at most - a Friday-evening note in the corner, and a note
        // when a currency on your list moved more than 2% in a week (once a week each). From the numbers already kept; a fetch only
        // when Markets' currency data is turned on.
        DateTime casualAt = DateTime.MinValue, casualFriDone = DateTime.MinValue; readonly Dictionary<string, DateTime> casualMoved = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        void CasualTick()
        {
            if ((!Cfg.CasualFriday && !Cfg.CasualMoves) || (DateTime.UtcNow - casualAt).TotalSeconds < 60) return; casualAt = DateTime.UtcNow;
            try
            {
                var now = DateTime.Now;
                if (Cfg.CasualMoves)
                    foreach (var c in MarketData.MyFx())
                    {
                        if (Cfg.TradeFx && !Policy.NoOnline) MarketData.Fetch("FX:" + c, null); else MarketData.Load("FX:" + c);
                        double ch = FxOver(c, 7); DateTime was;
                        if (double.IsNaN(ch) || Math.Abs(ch) <= 2 || (casualMoved.TryGetValue(c, out was) && (now - was).TotalDays < 7)) continue;
                        casualMoved[c] = now;
                        Alerts.Raise("casualfx", string.Format(CultureInfo.CurrentCulture, L.T("The {0} moved {1:0.0}% against the Canadian dollar this week."), c, ch), 0);
                    }
                if (Cfg.CasualFriday && now.DayOfWeek == DayOfWeek.Friday && now.Hour >= 17 && casualFriDone.Date != now.Date)
                {
                    foreach (var c in new[] { "USD" }.Concat(MarketData.MyFx())) MarketData.Load("FX:" + c);
                    var facts = MkWeekFacts(); if (facts.Count == 0) return;
                    casualFriDone = now;
                    Alerts.Raise("casualfri", MkHeadline() + " - " + facts[0].Item2, 0);
                }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("keep it light: " + e.Message); }
        }
        static double FxOver(string c, int days) { return MarketData.ChangeOver(MarketData.Get("FX:" + c), TimeSpan.FromDays(days)); }
        void MkText(Graphics g, string t, Font f, Color c, float x, float y) { using (var b = new SolidBrush(c)) g.DrawString(t, f, b, x, y); }
        void MkTextR(Graphics g, string t, Font f, Color c, float right, float y) { var s = g.MeasureString(t, f); using (var b = new SolidBrush(c)) g.DrawString(t, f, b, right - s.Width, y); }
        RectangleF MkButton(Graphics g, string t, float x, float y, bool primary)
        {
            bool chev = t.EndsWith(" ▾", StringComparison.Ordinal); if (chev) t = t.Substring(0, t.Length - 2);   // the fonts have no ▾: it's drawn
            var s = g.MeasureString(t, Draw.fBtn); var r = new RectangleF(x, y, s.Width + 28 * S + (chev ? 16 * S : 0), 36 * S);
            if (primary) Theme.Fill(g, r, 6 * S, Paper.Navy); else { Theme.Fill(g, r, 6 * S, Paper.DeskBg); Theme.Stroke(g, r, 6 * S, Paper.Line, 1); }
            MkText(g, t, Draw.fBtn, primary ? Color.White : Paper.Ink, r.X + 14 * S, r.Y + (r.Height - s.Height) / 2);
            if (chev) using (var cb = new SolidBrush(primary ? Color.White : Paper.Ink)) { float cx = r.Right - 20 * S, cy = r.Y + r.Height / 2; g.FillPolygon(cb, new[] { new PointF(cx - 5 * S, cy - 3 * S), new PointF(cx + 5 * S, cy - 3 * S), new PointF(cx, cy + 3 * S) }); }
            return r;
        }
        void MkRange(Graphics g, float right, float y, string[][] opts, string cur, Action<string> pick)
        {
            float rx = right;
            foreach (var o in opts.Reverse())
            {
                var s = g.MeasureString(L.T(o[1]), Draw.fSmall); var r = new RectangleF(rx - s.Width - 22 * S, y, s.Width + 22 * S, 30 * S); rx = r.X - 6 * S;
                bool on = o[0] == cur; if (on) Theme.Fill(g, r, 15 * S, Paper.Navy); else { Theme.Fill(g, r, 15 * S, Paper.DeskBg); Theme.Stroke(g, r, 15 * S, Paper.Line, 1); }
                MkText(g, L.T(o[1]), Draw.fSmall, on ? Color.White : Paper.Ink, r.X + 11 * S, r.Y + (r.Height - s.Height) / 2);
                string id = o[0]; Act(tradeCanvas, r, delegate { pick(id); tradeCanvas.Invalidate(); });
            }
        }
        // a currency picker: search by code or name; returns the code, or null
        string PickCurrency(string title, IEnumerable<string> exclude)
        {
            var ex = new HashSet<string>(exclude ?? new string[0]);
            using (var f = new Form { Text = "AdeeboMeter - " + title, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Card, ClientSize = new Size(Sc(380), Sc(420)) })
            {
                var tb = new TextBox { Font = Draw.fInput, Location = new Point(Sc(14), Sc(14)), Width = Sc(352) };
                var lb = new ListBox { Font = Draw.fBody, Location = new Point(Sc(14), Sc(52)), Size = new Size(Sc(352), Sc(310)), BackColor = Theme.Card, ForeColor = Theme.Ink, IntegralHeight = false };
                var ok = new PillButton(L.T("Add"), PillButton.K.Primary) { Location = new Point(Sc(272), Sc(374)) };
                Action fill = () => { lb.Items.Clear(); foreach (var a in MarketData.FindFx(tb.Text)) lb.Items.Add(a[0] + "  " + L.T(a[1]) + (ex.Contains(a[0]) ? "  · " + L.T("on your list") : "")); if (lb.Items.Count > 0) lb.SelectedIndex = 0; };
                tb.TextChanged += delegate { fill(); }; fill();
                string picked = null;
                Action done = () => { if (lb.SelectedItem == null) return; var c = lb.SelectedItem.ToString().Substring(0, 3); if (ex.Contains(c)) return; picked = c; f.DialogResult = DialogResult.OK; f.Close(); };
                ok.Click += delegate { done(); }; lb.DoubleClick += delegate { done(); }; tb.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { done(); e.SuppressKeyPress = true; } if (e.KeyCode == Keys.Down && lb.Items.Count > 0) { lb.Focus(); } };
                f.Controls.Add(tb); f.Controls.Add(lb); f.Controls.Add(ok); f.AcceptButton = null; f.Shown += delegate { tb.Focus(); };
                f.ShowDialog(this);
                return picked;
            }
        }
        void PickFrom(RectangleF at, IEnumerable<string> codes, Action<string> pick)
        {
            var m = Menus.Make(); foreach (var c in codes) { string cc = c; m.Items.Add(cc + "  " + (cc == "CAD" ? L.T("Canadian dollar") : L.T(MarketData.FxName(cc))), null, delegate { pick(cc); tradeCanvas.Invalidate(); }); }
            m.Show(tradeCanvas, new Point((int)at.X, (int)(at.Bottom + tradeCanvas.AutoScrollPosition.Y)));
        }
        double? AskNumber(string title, double now)
        {
            string t = Ask(title, now.ToString("0.##", CultureInfo.CurrentCulture)); double v;
            if (t == null) return null;
            // R2-069: the group separator of this PC's own number format is dropped, never the decimal one ("2,5" stays 2.5 in French)
            var nf = CultureInfo.CurrentCulture.NumberFormat; t = t.Replace("$", "").Replace("\u00a0", " ").Trim();
            if (nf.NumberGroupSeparator != nf.NumberDecimalSeparator) t = t.Replace(nf.NumberGroupSeparator, "").Replace(nf.NumberGroupSeparator == "\u00a0" || nf.NumberGroupSeparator == "\u202f" ? " " : nf.NumberGroupSeparator, "");
            return double.TryParse(t, NumberStyles.Float, CultureInfo.CurrentCulture, out v) && v >= 0 && v < 1e12 ? (double?)v : null;
        }

        // ---------- the week in plain words (pure facts from the numbers - never a forecast)
        string MkHeadline()
        {
            double usdWk = FxOver("USD", 7); var groups = new[] { "ENER", "MTLS", "AGRI", "FOPR", "FISH" }.Select(c => Tuple.Create(c, MarketData.ChangeOver(MarketData.Get(c), TimeSpan.FromDays(7)))).Where(t => !double.IsNaN(t.Item2)).OrderByDescending(t => Math.Abs(t.Item2)).ToList();
            if (double.IsNaN(usdWk) && groups.Count == 0) return L.T("The Week in Numbers");
            string loonie = double.IsNaN(usdWk) ? null : usdWk < -0.15 ? L.T("Loonie Firms") : usdWk > 0.15 ? L.T("Loonie Slips") : L.T("Loonie Holds Steady");
            string com = groups.Count == 0 ? null : L.T(MarketData.GroupInfo[groups[0].Item1][0]) + " " + (groups[0].Item2 >= 0 ? L.T("Leads Commodities") : L.T("Weighs on Commodities"));
            return loonie != null && com != null ? loonie + " " + L.T("as") + " " + com : loonie ?? com;
        }
        List<Tuple<int, string>> MkWeekFacts()
        {
            var l = new List<Tuple<int, string>>();
            double usdWk = FxOver("USD", 7);
            if (!double.IsNaN(usdWk)) l.Add(Tuple.Create(usdWk < 0 ? 1 : usdWk > 0 ? -1 : 0, string.Format(CultureInfo.CurrentCulture, L.T("Loonie {0} {1:0.0}% against the US dollar this week."), usdWk <= 0 ? L.T("up") : L.T("down"), Math.Abs(usdWk))));
            foreach (var c in new[] { "ENER", "FOPR", "MTLS", "AGRI" })
            {
                var s = MarketData.Get(c); int st = MarketData.Streak(s); double wk = MarketData.ChangeOver(s, TimeSpan.FromDays(7));
                if (Math.Abs(st) >= 2) l.Add(Tuple.Create(Math.Sign(st), string.Format(CultureInfo.CurrentCulture, L.T("{0} index {1} {2} weeks running."), L.T(MarketData.GroupInfo[c][0]), st > 0 ? L.T("up") : L.T("down"), Math.Abs(st))));
                else if (!double.IsNaN(wk) && Math.Abs(wk) >= 0.8) l.Add(Tuple.Create(Math.Sign(wk), string.Format(CultureInfo.CurrentCulture, L.T("{0} index {1} {2:0.0}% on the week."), L.T(MarketData.GroupInfo[c][0]), wk > 0 ? L.T("up") : L.T("down"), Math.Abs(wk))));
                if (l.Count >= 4) break;
            }
            var et = Trading.EasternNow();
            for (int d = 1; d <= 7 && l.Count < 5; d++) { var day = et.Date.AddDays(d); foreach (var v in new[] { "us", "ca" }) { var h = Markets.Holiday(day, v); if (h != null && h != "Weekend") l.Add(Tuple.Create(0, string.Format(CultureInfo.CurrentCulture, L.T("{0} closed {1} ({2})."), v == "us" ? L.T("New York") : L.T("Toronto"), day.ToString("dddd", CultureInfo.CurrentCulture), L.T(h)))); } }
            return l;
        }

        // ---------- Overview
        float MkOverview(Graphics g, float x, float y, float w)
        {
            // the lead: ribbon, headline, standfirst | At a Glance
            float lw = w * 0.58f, gx = x + lw + 18 * S, gw = w - lw - 18 * S;
            float ly = Paper.Ribbon(g, Kit.Sent(L.T("THIS WEEK'S MARKET BRIEFING")), x, y) + 10 * S;
            var head = MkHeadline(); var hs = g.MeasureString(head, Paper.Head1, (int)lw);
            MkText(g, "", Paper.Head1, Paper.Head, x, ly); using (var hb = new SolidBrush(Paper.Head)) g.DrawString(head, Paper.Head1, hb, new RectangleF(x, ly, lw, hs.Height + 4)); ly += hs.Height + 6 * S;
            var et = Trading.EasternNow(); bool open = Trading.MarketOpen(et);
            string stand = (open ? L.T("Markets are open.") : et.DayOfWeek == DayOfWeek.Saturday || et.DayOfWeek == DayOfWeek.Sunday ? L.T("Markets are closed for the weekend.") : L.T("Markets are closed right now.")) + " " + L.T("The Bank of Canada's numbers, in plain words.");
            var ss = g.MeasureString(stand, Paper.Italic, (int)lw); using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(stand, Paper.Italic, ib, new RectangleF(x, ly, lw, ss.Height + 4)); ly += ss.Height + 8 * S;
            float gy = y, gh = 210 * S; var gr = new RectangleF(gx, gy, gw, gh); Paper.Desk(g, gr);
            float cy = Paper.DeskTitle(g, gx + 16 * S, gy + 12 * S, "At a Glance", L.T("(Bank of Canada)")) + 4 * S;
            foreach (var c in MarketData.MyFx().Take(3)) { MkText(g, c + " " + L.T("in CAD"), Paper.Head3, Paper.Head, gx + 16 * S, cy); MkTextR(g, F4(FxNow(c)), Paper.Body, Paper.Ink, gx + gw - 110 * S, cy + 1 * S); Paper.Change(g, FxDay(c), Paper.BodyB, gx + gw - 16 * S, cy + 1 * S, "0.00"); cy += 30 * S; }
            var bc = MarketData.Get("BCPI"); MkText(g, L.T("Commodity index"), Paper.Head3, Paper.Head, gx + 16 * S, cy); MkTextR(g, bc == null ? "-" : bc.Last.ToString("N1", CultureInfo.CurrentCulture), Paper.Body, Paper.Ink, gx + gw - 110 * S, cy + 1 * S); Paper.Change(g, MarketData.ChangeOver(bc, TimeSpan.FromDays(7)), Paper.BodyB, gx + gw - 16 * S, cy + 1 * S, "0.00"); cy += 30 * S;
            MkText(g, L.T("Changes vs the day before; the commodity index vs last week."), Paper.Small, Paper.Muted, gx + 16 * S, cy + 2 * S);
            y = Math.Max(ly, gr.Bottom) + 18 * S;
            if (MarketData.Note != "") { MkText(g, L.T(MarketData.Note), Paper.Small, Paper.Amber, x, y); y += 24 * S; }

            // currencies (wide) | converter + the week in numbers
            float cw = w * 0.6f, rx = x + cw + 16 * S, rw = w - cw - 16 * S;
            float y1 = MkFxDesk(g, x, y, cw, true);
            float y2 = MkConverter(g, rx, y, rw);
            y2 = MkWeekDesk(g, rx, y2, rw);
            y = Math.Max(y1, y2);
            y = MkCommodDesk(g, x, y, w, false);
            // market days, session times, watchlist
            float tw = (w - 2 * 16 * S) / 3;
            float b1 = MkDaysDesk(g, x, y, tw), b2 = MkSessionsDesk(g, x + tw + 16 * S, y, tw), b3 = MkWatchDesk(g, x + 2 * (tw + 16 * S), y, tw);
            return Math.Max(b1, Math.Max(b2, b3));
        }

        // the person's currencies: rate, day, week, 30 days (line and range), remove; Add currency
        float MkFxDesk(Graphics g, float x, float y, float w, bool compact)
        {
            var list = MarketData.MyFx(); float rh = 40 * S, h = 70 * S + 34 * S + list.Count * rh + 16 * S; var r = new RectangleF(x, y, w, h); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Currencies Desk", L.T("one unit, in Canadian dollars"));
            var ab = MkButton(g, "+ " + L.T("Add currency"), x + w - 16 * S - g.MeasureString("+ " + L.T("Add currency"), Draw.fBtn).Width - 28 * S, y + 12 * S, true);
            Act(tradeCanvas, ab, delegate { var c = PickCurrency(L.T("Add a currency"), list); if (c != null && MarketData.AddFx(c)) { Trading.FetchFx(null); tradeCanvas.Invalidate(); } });
            float ty = y + 58 * S, c0 = x + 16 * S, c1 = x + w * 0.42f, c2 = x + w * 0.53f, c3 = x + w * 0.64f, c4 = x + w * 0.68f, c5 = x + w - 60 * S;
            using (var hb = new SolidBrush(Paper.Soft)) g.FillRectangle(hb, x + 8 * S, ty, w - 16 * S, 28 * S);
            MkText(g, L.T("Currency"), Draw.fSmall, Paper.Ink, c0, ty + 6 * S); MkTextR(g, L.T("In CAD"), Draw.fSmall, Paper.Ink, c1, ty + 6 * S); MkTextR(g, L.T("Day"), Draw.fSmall, Paper.Ink, c2, ty + 6 * S); MkTextR(g, L.T("Week"), Draw.fSmall, Paper.Ink, c3, ty + 6 * S); MkText(g, L.T("Last 30 days"), Draw.fSmall, Paper.Ink, c4, ty + 6 * S);
            ty += 32 * S;
            foreach (var c in list)
            {
                using (var lp = new Pen(Paper.Line2, 1)) g.DrawLine(lp, x + 8 * S, ty, x + w - 8 * S, ty);
                var cr = new RectangleF(c0, ty + 9 * S, 40 * S, 22 * S); Theme.Fill(g, cr, 4 * S, Paper.Navy); MkText(g, c, Draw.fTiny, Color.White, cr.X + 6 * S, cr.Y + 4 * S);
                MkText(g, L.T(MarketData.FxName(c)), Paper.Body, Paper.Ink, c0 + 48 * S, ty + 10 * S);
                MkTextR(g, F4(FxNow(c)), Paper.BodyB, Paper.Head, c1, ty + 10 * S);
                Paper.Change(g, FxDay(c), Paper.Small, c2, ty + 12 * S, "0.00"); Paper.Change(g, FxOver(c, 7), Paper.Small, c3, ty + 12 * S, "0.00");
                var hs = MarketData.Window(MarketData.Get("FX:" + c), DateTime.Today.AddDays(-30));
                if (hs.Count >= 2) { double wk = FxOver(c, 30); Paper.Spark(g, new RectangleF(c4, ty + 8 * S, Math.Min(130 * S, c5 - c4 - 120 * S), 24 * S), hs.V, double.IsNaN(wk) || Math.Abs(wk) < 0.05 ? Paper.Muted : wk > 0 ? Paper.Up : Paper.Down, false); MkTextR(g, F4(hs.V.Min()) + " – " + F4(hs.V.Max()), Paper.Small, Paper.Muted, c5 - 8 * S, ty + 12 * S); }
                else MkText(g, L.T("history on its way"), Paper.Small, Paper.Muted, c4, ty + 12 * S);
                var xr = new RectangleF(x + w - 48 * S, ty + 5 * S, 30 * S, 30 * S); Theme.Fill(g, xr, 6 * S, Paper.DeskBg); Theme.Stroke(g, xr, 6 * S, Paper.Line, 1); MkText(g, "×", Paper.BodyB, Paper.Muted, xr.X + 10 * S, xr.Y + 5 * S);
                if (list.Count > 1) { string cc = c; Act(tradeCanvas, xr, delegate { MarketData.RemoveFx(cc); tradeCanvas.Invalidate(); }); }
                ty += rh;
            }
            return r.Bottom + 16 * S;
        }
        float MkConverter(Graphics g, float x, float y, float w)
        {
            var r = new RectangleF(x, y, w, 196 * S); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Converter", null);
            double a = FxNow(mkCvFrom), b = FxNow(mkCvTo), res = double.IsNaN(a) || double.IsNaN(b) || b <= 0 ? double.NaN : mkCvAmt * a / b;
            var amt = MkButton(g, mkCvAmt.ToString("N2", CultureInfo.CurrentCulture), x + 16 * S, y + 52 * S, false);
            Act(tradeCanvas, amt, delegate { var v = AskNumber(L.T("Amount"), mkCvAmt); if (v.HasValue) { mkCvAmt = v.Value; tradeCanvas.Invalidate(); } });
            var fr = MkButton(g, mkCvFrom + " ▾", amt.Right + 10 * S, amt.Y, false); var codes = new[] { "CAD" }.Concat(MarketData.MyFx()).ToList();
            Act(tradeCanvas, fr, delegate { PickFrom(fr, codes, c => mkCvFrom = c); });
            MkText(g, L.T("comes to"), Paper.Small, Paper.Muted, x + 16 * S, y + 100 * S);
            MkText(g, double.IsNaN(res) ? "-" : res.ToString("N2", CultureInfo.CurrentCulture), Paper.Num, Paper.Head, x + 16 * S, y + 116 * S);
            var to = MkButton(g, mkCvTo + " ▾", x + w - 16 * S - g.MeasureString(mkCvTo + " ▾", Draw.fBtn).Width - 28 * S, y + 122 * S, false);
            Act(tradeCanvas, to, delegate { PickFrom(to, codes, c => mkCvTo = c); });
            MkText(g, L.T("At the Bank of Canada's rate. Banks and exchange shops add a margin."), Paper.Small, Paper.Muted, x + 16 * S, y + 168 * S);
            return r.Bottom + 16 * S;
        }
        float MkWeekDesk(Graphics g, float x, float y, float w)
        {
            var facts = MkWeekFacts(); float h = 58 * S + Math.Max(1, facts.Count) * 28 * S + 30 * S; var r = new RectangleF(x, y, w, h); Paper.Desk(g, r);
            float ty = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "The Week in Numbers", null) + 4 * S;
            if (facts.Count == 0) { MkText(g, L.T("The numbers arrive after the first fetch."), Paper.Body, Paper.Muted, x + 16 * S, ty); ty += 28 * S; }
            foreach (var f in facts) { using (var b = new SolidBrush(f.Item1 > 0 ? Paper.Up : f.Item1 < 0 ? Paper.Down : Paper.Muted)) g.FillEllipse(b, x + 16 * S, ty + 6 * S, 9 * S, 9 * S); using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(f.Item2, Paper.Body, ib, new RectangleF(x + 32 * S, ty, w - 48 * S, 26 * S)); ty += 28 * S; }
            MkText(g, L.T("Facts from this week's numbers, not a forecast."), Paper.Small, Paper.Muted, x + 16 * S, ty + 2 * S);
            return r.Bottom + 16 * S;
        }
        // the commodity groups as cards (13 weeks each), and on the deep tab the big comparison too
        float MkCommodDesk(Graphics g, float x, float y, float w, bool deep)
        {
            var groups = new[] { "ENER", "MTLS", "AGRI", "FOPR", "FISH" };
            int per = w > 1100 * S ? 5 : w > 700 * S ? 3 : 2; int rows = (groups.Length + per - 1) / per; float cw = (w - 32 * S - (per - 1) * 12 * S) / per, ch = 150 * S;
            float h = 60 * S + rows * (ch + 12 * S) + 30 * S; var r = new RectangleF(x, y, w, h); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Commodities Desk", L.T("Bank of Canada commodity price indexes, in US dollars · weekly"));
            for (int i = 0; i < groups.Length; i++)
            {
                var s = MarketData.Get(groups[i]); var cr = new RectangleF(x + 16 * S + (i % per) * (cw + 12 * S), y + 56 * S + (i / per) * (ch + 12 * S), cw, ch);
                Theme.Fill(g, cr, 6 * S, Paper.CardBg); Theme.Stroke(g, cr, 6 * S, Paper.Line2, 1);
                MkText(g, L.T(MarketData.GroupInfo[groups[i]][0]), Paper.Head3, Paper.Head, cr.X + 12 * S, cr.Y + 8 * S);
                Paper.Change(g, MarketData.ChangeOver(s, TimeSpan.FromDays(7)), Paper.Small, cr.Right - 10 * S, cr.Y + 12 * S, "0.00");
                using (var mb = new SolidBrush(Paper.Muted)) using (var nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(L.T(MarketData.GroupInfo[groups[i]][1]), Paper.Small, mb, new RectangleF(cr.X + 12 * S, cr.Y + 32 * S, cr.Width - 24 * S, 18 * S), nf);
                MkText(g, s == null ? "-" : s.Last.ToString("N1", CultureInfo.CurrentCulture), Paper.NumS, Paper.Head, cr.X + 12 * S, cr.Y + 54 * S);
                var ws = MarketData.Window(s, DateTime.Today.AddDays(-91)); double q = MarketData.ChangeOver(s, TimeSpan.FromDays(91));
                Paper.Spark(g, new RectangleF(cr.X + 12 * S, cr.Y + 84 * S, cr.Width - 24 * S, 34 * S), ws.V, double.IsNaN(q) ? Paper.Muted : q >= 0 ? Paper.Up : Paper.Down, true);
                int st = MarketData.Streak(s);
                MkText(g, L.T("3 months:") + " " + (double.IsNaN(q) ? "-" : (q >= 0 ? "+" : "−") + Math.Abs(q).ToString("0.0", CultureInfo.CurrentCulture) + "%") + (Math.Abs(st) >= 2 ? " · " + (st > 0 ? L.T("up") : L.T("down")) + " " + Math.Abs(st) + " " + L.T("weeks") : ""), Paper.Small, Paper.Muted, cr.X + 12 * S, cr.Bottom - 24 * S);
            }
            MkText(g, L.T("An index, not a price per barrel or ounce: it shows which way a group of Canadian exports moves, and how far."), Paper.Small, Paper.Muted, x + 16 * S, r.Bottom - 26 * S);
            return r.Bottom + 16 * S;
        }
        float MkDaysDesk(Graphics g, float x, float y, float w)
        {
            var et = Trading.EasternNow(); var rows = new List<string[]>();
            for (int d = 0; d <= 45 && rows.Count < 5; d++)
            {
                var day = et.Date.AddDays(d); if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
                string us = Markets.Holiday(day, "us"), ca = Markets.Holiday(day, "ca"); bool usHalf = us == null && Markets.Close(day, "us").Hours == 13, caHalf = ca == null && Markets.Close(day, "ca").Hours == 13;
                if (us != null && ca != null) rows.Add(new[] { day.ToString("ddd, MMM d", CultureInfo.CurrentCulture), L.T("Both"), L.T("Closed") + " · " + L.T(us) });
                else if (us != null) rows.Add(new[] { day.ToString("ddd, MMM d", CultureInfo.CurrentCulture), L.T("New York"), L.T("Closed") + " · " + L.T(us) });
                else if (ca != null) rows.Add(new[] { day.ToString("ddd, MMM d", CultureInfo.CurrentCulture), L.T("Toronto"), L.T("Closed") + " · " + L.T(ca) });
                if (usHalf || caHalf) rows.Add(new[] { day.ToString("ddd, MMM d", CultureInfo.CurrentCulture), usHalf && caHalf ? L.T("Both") : usHalf ? L.T("New York") : L.T("Toronto"), L.T("Closes 1 PM") });
            }
            float h = 60 * S + Math.Max(1, rows.Count) * 30 * S + 12 * S; var r = new RectangleF(x, y, w, h); Paper.Desk(g, r);
            float ty = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Market Days Ahead", null) + 4 * S;
            if (rows.Count == 0) MkText(g, L.T("No closings in the next six weeks."), Paper.Body, Paper.Ink, x + 16 * S, ty);
            foreach (var rw in rows) { MkText(g, rw[0], Paper.BodyB, Paper.Head, x + 16 * S, ty); MkText(g, rw[1], Paper.Body, Paper.Ink, x + w * 0.42f, ty); MkTextR(g, rw[2], Paper.Body, rw[2].StartsWith(L.T("Closed"), StringComparison.Ordinal) ? Paper.Down : Paper.Ink, x + w - 16 * S, ty); ty += 30 * S; }
            return r.Bottom + 16 * S;
        }
        float MkSessionsDesk(Graphics g, float x, float y, float w)
        {
            var r = new RectangleF(x, y, w, 160 * S); Paper.Desk(g, r);
            float ty = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Session Times", L.T("(your time)")) + 6 * S;
            foreach (var name in new[] { "New York", "Toronto" })
            {
                var ex = MarketData.Exchanges.First(e => e.Name == name); string st = MarketData.State(ex, DateTime.UtcNow); var ns = MarketData.NextSession(ex, DateTime.UtcNow);
                MkText(g, L.T(name), Paper.BodyB, Paper.Head, x + 16 * S, ty);
                if (ns != null) MkText(g, ns.Item1.ToString("t", CultureInfo.CurrentCulture) + " – " + ns.Item2.ToString("t", CultureInfo.CurrentCulture), Paper.Body, Paper.Ink, x + w * 0.38f, ty);
                var pr = Paper.Pill(g, st == "open" ? L.T("Open") : L.T("Closed"), x + w - 56 * S, ty - 2 * S, st == "open" ? Paper.Green : Paper.Muted);
                ty += 34 * S;
            }
            var nx = MarketData.NextSession(MarketData.Exchanges.First(e => e.Name == "New York"), DateTime.UtcNow);
            if (nx != null && nx.Item1 > DateTime.Now) { var span = nx.Item1 - DateTime.Now; MkText(g, L.T("Next open:") + " " + L.T("New York") + " " + nx.Item1.ToString("dddd t", CultureInfo.CurrentCulture) + " · " + L.T("in") + " " + (span.Days > 0 ? span.Days + " " + L.T("d") + " " : "") + span.Hours + " " + L.T("h") + " " + span.Minutes + " " + L.T("min"), Paper.Small, Paper.Muted, x + 16 * S, ty + 4 * S); }
            return r.Bottom + 16 * S;
        }
        float MkWatchDesk(Graphics g, float x, float y, float w)
        {
            var r = new RectangleF(x, y, w, 160 * S); Paper.Desk(g, r);
            float ty = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "World Indexes", null) + 4 * S;
            if (!MarketData.HasQuoteKey())
            {
                using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(L.T("Index levels need a quote service. Add your own free Financial Modeling Prep key and New York, Toronto, London, Frankfurt, Tokyo and more show here. Without one this stays empty - never old or made-up numbers."), Paper.Small, ib, new RectangleF(x + 16 * S, ty, w - 32 * S, 70 * S));
                var kb = MkButton(g, L.T("Add a quote key..."), x + 16 * S, r.Bottom - 48 * S, true); Act(tradeCanvas, kb, delegate { AskQuoteKey(); });
            }
            else
            {
                List<MarketData.Quote> qs; lock (MarketData.k) qs = MarketData.Quotes.Values.Where(q => q.Region != null).ToList();
                foreach (var sym in new[] { "^GSPC", "^GSPTSE", "^FTSE", "^N225" }) { var q = qs.FirstOrDefault(z => z.Sym == sym); var info = MarketData.IndexList.First(i => i[0] == sym); MkText(g, L.T(info[1]), Paper.Body, Paper.Ink, x + 16 * S, ty); MkTextR(g, q == null ? "-" : q.Price.ToString("N1", CultureInfo.CurrentCulture), Paper.BodyB, Paper.Head, x + w - 90 * S, ty); Paper.Change(g, q == null ? double.NaN : q.Pct, Paper.Small, x + w - 16 * S, ty + 2 * S, "0.00"); ty += 26 * S; }
                var more = MkButton(g, L.T("All world markets") + " ›", x + 16 * S, r.Bottom - 46 * S, false); Act(tradeCanvas, more, delegate { mkTab = "world"; tradeCanvas.Invalidate(); });
            }
            return r.Bottom + 16 * S;
        }
        void AskQuoteKey()
        {
            string k = Ask(L.T("Your Financial Modeling Prep key (free at financialmodelingprep.com) - kept encrypted on this PC"), "", true, 200);   // longer than a key can be, so the check below says so instead of a silent cut
            if (k == null) return; k = k.Trim();
            if (k != "" && (k.Length < 10 || k.Length > 80 || k.Any(c => !char.IsLetterOrDigit(c)))) { AskCard.Show(this, L.T("That doesn't look like a key: letters and digits only."), "AdeeboMeter", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            Sports.SetKey("fmp", k); MarketData.QuoteKeyChanged(); lock (MarketData.k) { MarketData.Quotes.Clear(); MarketData.QuoteNote = ""; } tradeCanvas.Invalidate();
        }

        // ---------- World Markets: the day of every exchange in your time, the indexes by region, how they're linked
        float MkWorld(Graphics g, float x, float y, float w)
        {
            var nowL = DateTime.Now; var from = nowL.AddHours(-6); var to = from.AddHours(24);
            var nextOpen = MarketData.Exchanges.Select(e => MarketData.NextSession(e, DateTime.UtcNow)).Where(z => z != null).Select(z => z.Item1).DefaultIfEmpty(nowL).Min();
            bool anyOpen = MarketData.Exchanges.Any(e => MarketData.State(e, DateTime.UtcNow) != "closed");
            if (!anyOpen && nextOpen > to.AddHours(-12)) { from = new DateTime(nextOpen.Year, nextOpen.Month, nextOpen.Day, nextOpen.Hour, 0, 0).AddHours(-2); to = from.AddHours(24); }
            bool nowIn = nowL >= from && nowL <= to;
            float lab = 170 * S, bx = x + 16 * S + lab, bw = w - 32 * S - lab - 96 * S, rh = 28 * S;   // the pills sit right of the bars
            float h = 70 * S + MarketData.Exchanges.Length * rh + 64 * S; var r = new RectangleF(x, y, w, h); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "The World's Trading Day", L.T("in your time · worked out on this PC"));
            float ty = y + 54 * S;
            for (int hh = 0; hh <= 24; hh += 4) { var t = from.AddHours(hh); string lbl = t.ToString("h tt", CultureInfo.CurrentCulture); var ls = g.MeasureString(lbl, Draw.fTiny); MkText(g, lbl, Draw.fTiny, Paper.Muted, Math.Min(bx + bw * hh / 24 - ls.Width / 2, bx + bw - ls.Width), ty); }
            ty += 20 * S;
            foreach (var ex in MarketData.Exchanges)
            {
                string st = MarketData.State(ex, DateTime.UtcNow);
                MkText(g, L.T(ex.Name), Paper.BodyB, Paper.Head, x + 16 * S, ty + 3 * S);
                var track = new RectangleF(bx, ty + 4 * S, bw, 18 * S); using (var tb = new SolidBrush(Color.FromArgb(40, Paper.Muted))) g.FillRectangle(tb, track);
                // each session overlapping the window (today's and the next ones), lunch breaks cut out
                var cursor = DateTime.UtcNow.AddHours(-30);
                for (int k2 = 0; k2 < 4; k2++)
                {
                    var ns = MarketData.NextSession(ex, cursor); if (ns == null) break; cursor = ns.Item2.ToUniversalTime().AddMinutes(1);
                    if (ns.Item1 > to) break; if (ns.Item2 < from) continue;
                    Color c = ex.Zone.StartsWith("Eastern", StringComparison.Ordinal) ? Paper.Up : ex.Zone.Contains("Europe") || ex.Zone.StartsWith("GMT", StringComparison.Ordinal) || ex.Zone.StartsWith("Romance", StringComparison.Ordinal) ? Color.FromArgb(176, 138, 30) : Color.FromArgb(61, 111, 168);
                    Action<DateTime, DateTime> bar = (a, b) => { if (b <= from || a >= to) return; float x0 = bx + (float)((Math.Max(a.Ticks, from.Ticks) - from.Ticks) / (double)(to.Ticks - from.Ticks)) * bw, x1 = bx + (float)((Math.Min(b.Ticks, to.Ticks) - from.Ticks) / (double)(to.Ticks - from.Ticks)) * bw; Theme.Fill(g, new RectangleF(x0, track.Y, Math.Max(2, x1 - x0), track.Height), 4 * S, c); };
                    if (ex.LunchTo > ex.LunchFrom) { var l0 = ns.Item1 + (ex.LunchFrom - ex.Open); var l1 = ns.Item1 + (ex.LunchTo - ex.Open); bar(ns.Item1, l0); bar(l1, ns.Item2); } else bar(ns.Item1, ns.Item2);
                }
                var pr = Paper.Pill(g, st == "open" ? L.T("Open") : st == "lunch" ? L.T("Lunch") : L.T("Closed"), x + w - 40 * S, ty, st == "open" ? Paper.Green : st == "lunch" ? Paper.Amber : Paper.Muted);
                ty += rh;
            }
            if (nowIn) { float nx = bx + (float)((nowL - from).TotalHours / 24) * bw; using (var np = new Pen(Paper.Red, 2 * S)) g.DrawLine(np, nx, y + 70 * S, nx, ty); }
            MkText(g, (nowIn ? L.T("Red line: now.") : L.T("All closed now - here is the next trading day, from") + " " + nextOpen.ToString("dddd h:mm tt", CultureInfo.CurrentCulture) + ".") + " " + L.T("Holidays are known for New York and Toronto; other exchanges' holidays aren't checked."), Paper.Small, Paper.Muted, x + 16 * S, ty + 10 * S);
            y = r.Bottom + 16 * S;

            // indexes by region (with a key)
            List<MarketData.Quote> qs; lock (MarketData.k) qs = MarketData.Quotes.Values.ToList();
            if (!MarketData.HasQuoteKey()) return MkWatchDesk(g, x, y, w);
            float colW = (w - 2 * 16 * S) / 3; float bottom = y;
            int col = 0;
            foreach (var region in new[] { "Americas", "Europe", "Asia" })
            {
                var items = MarketData.IndexList.Where(i => i[2] == region).ToList(); float dh = 60 * S + items.Count * 30 * S + 10 * S;
                var dr = new RectangleF(x + col * (colW + 16 * S), y, colW, dh); Paper.Desk(g, dr); float iy = Paper.DeskTitle(g, dr.X + 16 * S, y + 12 * S, region, null) + 4 * S;
                foreach (var it in items)
                {
                    var q = qs.FirstOrDefault(z => z.Sym == it[0]);
                    MkText(g, L.T(it[1]), Paper.Body, Paper.Ink, dr.X + 16 * S, iy); MkTextR(g, q == null ? "-" : q.Price.ToString("N1", CultureInfo.CurrentCulture), Paper.BodyB, Paper.Head, dr.Right - 110 * S, iy); Paper.Change(g, q == null ? double.NaN : q.Pct, Paper.Small, dr.Right - 44 * S, iy + 2 * S, "0.00"); MkTextR(g, it[3], Draw.fTiny, Paper.Muted, dr.Right - 10 * S, iy + 3 * S);
                    iy += 30 * S;
                }
                bottom = Math.Max(bottom, dr.Bottom); col++;
            }
            y = bottom + 10 * S;
            string qn; lock (MarketData.k) qn = MarketData.QuoteNote;
            MkText(g, qn != "" ? L.T(qn) : L.T("From your Financial Modeling Prep key · delayed while markets are open"), Paper.Small, qn != "" ? Paper.Amber : Paper.Muted, x, y); y += 26 * S;
            if (MarketData.HasQuoteKey())   // how often: your key, your choice (5 and 1 minute need FMP's paid plan)
            {
                float px = x; MkText(g, L.T("Update:"), Paper.Small, Paper.Muted, px, y + 4 * S); px += 64 * S;
                foreach (var o in new[] { new[] { "60", "every hour" }, new[] { "15", "every 15 min (free)" }, new[] { "5", "every 5 min (paid)" }, new[] { "1", "every minute (paid)" } })
                {
                    bool on = Cfg.FmpEvery == o[0]; var ob = MkButton(g, L.T(o[1]), px, y, on); string v = o[0];
                    Act(tradeCanvas, ob, delegate { Cfg.FmpEvery = v; Cfg.Save(); tradeCanvas.Invalidate(); }); px = ob.Right + 8 * S;
                    if (px > x + w - 150 * S && o[0] != "1") { px = x + 64 * S; y += ob.Height + 6 * S; }
                }
                y += 44 * S;
            }
            // how they're linked: each market's index and its currency against the Canadian dollar
            var lr = new RectangleF(x, y, w, 60 * S + 6 * 30 * S); Paper.Desk(g, lr);
            float ly = Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "How They're Linked", L.T("a rise abroad can grow or shrink once it's turned into Canadian dollars")) + 4 * S;
            foreach (var pair in new[] { new[] { "^N225", "JPY" }, new[] { "^GDAXI", "EUR" }, new[] { "000001.SS", "CNY" }, new[] { "^FTSE", "GBP" }, new[] { "^GSPC", "USD" }, new[] { "^NSEI", "INR" } })
            {
                var q = qs.FirstOrDefault(z => z.Sym == pair[0]); var info = MarketData.IndexList.First(i => i[0] == pair[0]);
                MkText(g, L.T(info[4]), Paper.BodyB, Paper.Head, x + 16 * S, ly); MkText(g, L.T("index"), Paper.Small, Paper.Muted, x + w * 0.35f, ly + 2 * S); Paper.Change(g, q == null ? double.NaN : q.Pct, Paper.Body, x + w * 0.55f, ly, "0.00");
                MkText(g, pair[1] + " " + L.T("vs CAD"), Paper.Small, Paper.Muted, x + w * 0.62f, ly + 2 * S); Paper.Change(g, FxDay(pair[1]), Paper.Body, x + w - 20 * S, ly, "0.00");
                ly += 30 * S;
            }
            return lr.Bottom + 16 * S;
        }

        // ---------- Currencies: one currency's long chart, the stats, all of yours, cross rates, the converter
        float MkCurrencies(Graphics g, float x, float y, float w)
        {
            var s = MarketData.Get("FX:" + mkFx); var from = mkRange == "1m" ? DateTime.Today.AddMonths(-1) : mkRange == "3m" ? DateTime.Today.AddMonths(-3) : mkRange == "5y" ? DateTime.Today.AddYears(-5) : mkRange == "all" ? DateTime.MinValue : DateTime.Today.AddYears(-1);
            var win = MarketData.Window(s, from);
            var r = new RectangleF(x, y, w, 470 * S); Paper.Desk(g, r);
            var pick = MkButton(g, mkFx + " · " + L.T(MarketData.FxName(mkFx)) + "  ▾", x + 16 * S, y + 14 * S, false);
            Act(tradeCanvas, pick, delegate { PickFrom(pick, MarketData.MyFx(), c => mkFx = c); });
            MkText(g, L.T("in Canadian dollars"), Paper.Body, Paper.Ink, pick.Right + 10 * S, y + 22 * S);
            MkRange(g, x + w - 16 * S, y + 16 * S, new[] { new[] { "1m", "1 month" }, new[] { "3m", "3 months" }, new[] { "1y", "1 year" }, new[] { "5y", "5 years" }, new[] { "all", "Since 2017" } }, mkRange, id => mkRange = id);
            double last = s == null ? double.NaN : s.Last; double day = FxDay(mkFx);
            MkText(g, F4(last), Paper.Num, Paper.Head, x + 16 * S, y + 60 * S);
            Paper.Change(g, day, Paper.BodyB, x + 16 * S + g.MeasureString(F4(last), Paper.Num).Width + 90 * S, y + 72 * S, "0.00");
            MkText(g, s == null ? L.T("on its way from the Bank of Canada...") : L.T("Bank of Canada") + ", " + s.LastDay.ToString("ddd MMM d", CultureInfo.CurrentCulture), Paper.Small, Paper.Muted, x + w * 0.45f, y + 80 * S);
            var ch = new RectangleF(x + 60 * S, y + 120 * S, w - 80 * S, 220 * S);
            if (win.Count >= 2)
            {
                double lo = win.V.Min(), hi = win.V.Max(), pad = (hi - lo) * 0.08 + 1e-9; lo -= pad; hi += pad;
                for (int i = 0; i <= 4; i++) { float gy = ch.Y + ch.Height * i / 4; using (var gp = new Pen(Paper.Line2, 1)) g.DrawLine(gp, ch.X, gy, ch.Right, gy); MkTextR(g, F4(hi - (hi - lo) * i / 4), Draw.fTiny, Paper.Muted, ch.X - 6 * S, gy - 7 * S); }
                var pts = new List<PointF>(); for (int i = 0; i < win.Count; i++) pts.Add(new PointF(ch.X + ch.Width * i / (win.Count - 1), (float)(ch.Bottom - (win.V[i] - lo) / (hi - lo) * ch.Height)));
                using (var fb = new SolidBrush(Color.FromArgb(18, Paper.Navy))) { var poly = new List<PointF>(pts) { new PointF(ch.Right, ch.Bottom), new PointF(ch.X, ch.Bottom) }; g.FillPolygon(fb, poly.ToArray()); }
                using (var lp = new Pen(Paper.Navy, 2.4f * S) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round }) g.DrawLines(lp, pts.ToArray());
                int hiI = win.V.IndexOf(win.V.Max()); using (var hb = new SolidBrush(Paper.Red)) g.FillEllipse(hb, pts[hiI].X - 4 * S, pts[hiI].Y - 4 * S, 8 * S, 8 * S);
                MkText(g, L.T("high") + " " + F4(win.V[hiI]) + " · " + win.D[hiI].ToString("MMM d, yyyy", CultureInfo.CurrentCulture), Draw.fTiny, Paper.Red, Math.Min(pts[hiI].X + 8 * S, ch.Right - 150 * S), pts[hiI].Y - 6 * S);
                for (int i = 0; i < 6; i++) { int ix = (win.Count - 1) * i / 5; var lbl = win.D[ix].ToString(mkRange == "1m" || mkRange == "3m" ? "MMM d" : "MMM yyyy", CultureInfo.CurrentCulture); MkText(g, lbl, Draw.fTiny, Paper.Muted, Math.Min(ch.X + ch.Width * i / 5, ch.Right - 50 * S), ch.Bottom + 6 * S); }
                // the numbers under the chart
                int loI = win.V.IndexOf(win.V.Min()); double avg = win.V.Average(), chg = MarketData.Pct(win.V[win.Count - 1], win.V[0]);
                double big = 0; DateTime bigD = DateTime.MinValue; for (int i = 1; i < win.Count; i++) { double p = Math.Abs(MarketData.Pct(win.V[i], win.V[i - 1])); if (p > big) { big = p; bigD = win.D[i]; } }
                var stats = new[] { new[] { L.T("High"), F4(win.V[hiI]), win.D[hiI].ToString("MMM d", CultureInfo.CurrentCulture) }, new[] { L.T("Low"), F4(win.V[loI]), win.D[loI].ToString("MMM d", CultureInfo.CurrentCulture) }, new[] { L.T("Average"), F4(avg), "" }, new[] { L.T("Change"), (chg >= 0 ? "+" : "−") + Math.Abs(chg).ToString("0.0", CultureInfo.CurrentCulture) + "%", chg < 0 ? L.T("loonie stronger") : L.T("loonie weaker") }, new[] { L.T("Biggest day"), big.ToString("0.00", CultureInfo.CurrentCulture) + "%", bigD == DateTime.MinValue ? "" : bigD.ToString("MMM d", CultureInfo.CurrentCulture) } };
                float sw = (w - 32 * S - 4 * 10 * S) / 5;
                for (int i = 0; i < 5; i++) { var sr = new RectangleF(x + 16 * S + i * (sw + 10 * S), ch.Bottom + 30 * S, sw, 64 * S); Theme.Fill(g, sr, 6 * S, Paper.CardBg); Theme.Stroke(g, sr, 6 * S, Paper.Line2, 1); MkText(g, stats[i][0], Paper.Small, Paper.Muted, sr.X + 10 * S, sr.Y + 8 * S); MkText(g, stats[i][1], Paper.NumS, Paper.Head, sr.X + 10 * S, sr.Y + 26 * S); if (stats[i][2] != "") MkTextR(g, stats[i][2], Paper.Small, Paper.Muted, sr.Right - 8 * S, sr.Y + 30 * S); }
            }
            else MkText(g, L.T("The history arrives from the Bank of Canada in a moment."), Paper.Body, Paper.Muted, ch.X, ch.Y + 60 * S);
            y = r.Bottom + 16 * S;
            float lw = w * 0.6f; float y1 = MkFxTable(g, x, y, lw); float y2 = MkCross(g, x + lw + 16 * S, y, w - lw - 16 * S); y2 = MkConverter(g, x + lw + 16 * S, y2, w - lw - 16 * S);
            return Math.Max(y1, y2);
        }
        float MkFxTable(Graphics g, float x, float y, float w)
        {
            var list = MarketData.MyFx(); var r = new RectangleF(x, y, w, 100 * S + list.Count * 34 * S); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "All Your Currencies", null);
            var ab = MkButton(g, "+ " + L.T("Add currency"), x + w - 16 * S - g.MeasureString("+ " + L.T("Add currency"), Draw.fBtn).Width - 28 * S, y + 12 * S, true);
            Act(tradeCanvas, ab, delegate { var c = PickCurrency(L.T("Add a currency"), list); if (c != null && MarketData.AddFx(c)) { Trading.FetchFx(null); tradeCanvas.Invalidate(); } });
            float ty = y + 58 * S, c1 = x + w * 0.5f, c2 = x + w * 0.64f, c3 = x + w * 0.79f, c4 = x + w - 16 * S;
            using (var hb = new SolidBrush(Paper.Soft)) g.FillRectangle(hb, x + 8 * S, ty, w - 16 * S, 26 * S);
            MkText(g, L.T("Currency"), Draw.fSmall, Paper.Ink, x + 16 * S, ty + 5 * S); MkTextR(g, L.T("In CAD"), Draw.fSmall, Paper.Ink, c1, ty + 5 * S); MkTextR(g, L.T("Week"), Draw.fSmall, Paper.Ink, c2, ty + 5 * S); MkTextR(g, L.T("Month"), Draw.fSmall, Paper.Ink, c3, ty + 5 * S); MkTextR(g, L.T("Year"), Draw.fSmall, Paper.Ink, c4, ty + 5 * S);
            ty += 30 * S;
            foreach (var c in list)
            {
                var rr = new RectangleF(x + 8 * S, ty, w - 16 * S, 34 * S); if (c == mkFx) using (var sb = new SolidBrush(Color.FromArgb(30, Paper.Navy))) g.FillRectangle(sb, rr);
                MkText(g, c + "  " + L.T(MarketData.FxName(c)), Paper.BodyB, Paper.Head, x + 16 * S, ty + 7 * S); MkTextR(g, F4(FxNow(c)), Paper.Body, Paper.Ink, c1, ty + 7 * S);
                Paper.Change(g, FxOver(c, 7), Paper.Small, c2, ty + 9 * S, "0.00"); Paper.Change(g, FxOver(c, 30), Paper.Small, c3, ty + 9 * S, "0.00"); Paper.Change(g, FxOver(c, 365), Paper.Small, c4, ty + 9 * S, "0.0");
                string cc = c; Act(tradeCanvas, rr, delegate { mkFx = cc; tradeCanvas.AutoScrollPosition = Point.Empty; tradeCanvas.Invalidate(); });
                ty += 34 * S;
            }
            MkText(g, L.T("A change is how much that currency's price in Canadian dollars moved. Click a row to chart it."), Paper.Small, Paper.Muted, x + 16 * S, ty + 4 * S);
            return r.Bottom + 16 * S;
        }
        float MkCross(Graphics g, float x, float y, float w)
        {
            var codes = new[] { "CAD" }.Concat(MarketData.MyFx().Take(3)).ToList(); float cell = (w - 32 * S) / (codes.Count + 1);
            var r = new RectangleF(x, y, w, 90 * S + codes.Count * 30 * S); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Cross Rates", null);
            MkText(g, L.T("One of the row buys this much of the column."), Paper.Small, Paper.Muted, x + 16 * S, y + 44 * S);
            float ty = y + 66 * S;
            for (int j = 0; j < codes.Count; j++) MkTextR(g, codes[j], Draw.fSmall, Paper.Head, x + 16 * S + cell * (j + 2) - 6 * S, ty);
            ty += 24 * S;
            foreach (var a in codes)
            {
                MkText(g, a, Paper.BodyB, Paper.Head, x + 16 * S, ty);
                for (int j = 0; j < codes.Count; j++) { double v = a == codes[j] ? 1 : MarketData.Cross(a, codes[j]); MkTextR(g, a == codes[j] ? "1" : double.IsNaN(v) ? "-" : v >= 100 ? v.ToString("N2", CultureInfo.CurrentCulture) : v.ToString("0.0000", CultureInfo.CurrentCulture), Paper.Small, a == codes[j] ? Paper.Muted : Paper.Ink, x + 16 * S + cell * (j + 2) - 6 * S, ty + 2 * S); }
                ty += 30 * S;
            }
            return r.Bottom + 16 * S;
        }

        // ---------- Commodities: the five groups side by side (set to 100 at the start), the table, energy and the loonie
        float MkCommodities(Graphics g, float x, float y, float w)
        {
            var from = mkRange == "3m" ? DateTime.Today.AddMonths(-3) : mkRange == "5y" ? DateTime.Today.AddYears(-5) : DateTime.Today.AddYears(-1);
            var groups = new[] { "ENER", "FISH", "AGRI", "MTLS", "FOPR" }; var cols = new[] { Paper.Up, Color.FromArgb(61, 111, 168), Color.FromArgb(122, 91, 166), Color.FromArgb(176, 138, 30), Paper.Down };
            var r = new RectangleF(x, y, w, 400 * S); Paper.Desk(g, r);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Five Commodity Groups, Side by Side", L.T("each set to 100 at the start, so you can compare"));
            MkRange(g, x + w - 16 * S, y + 14 * S, new[] { new[] { "3m", "3 months" }, new[] { "1y", "1 year" }, new[] { "5y", "5 years" } }, mkRange == "3m" || mkRange == "5y" ? mkRange : "1y", id => mkRange = id);
            var ch = new RectangleF(x + 50 * S, y + 64 * S, w - 70 * S, 270 * S);
            var series = groups.Select(c => MarketData.Window(MarketData.Get(c), from)).ToList();
            var norm = series.Select(s => s.Count >= 2 && s.V[0] > 0 ? s.V.Select(v => v / s.V[0] * 100).ToList() : new List<double>()).ToList();
            if (norm.Any(n => n.Count >= 2))
            {
                double lo = norm.Where(n => n.Count > 0).Min(n => n.Min()), hi = norm.Where(n => n.Count > 0).Max(n => n.Max()); double pad = (hi - lo) * 0.08 + 0.5; lo -= pad; hi += pad;
                for (int i = 0; i <= 4; i++) { float gy = ch.Y + ch.Height * i / 4; using (var gp = new Pen(Paper.Line2, 1)) g.DrawLine(gp, ch.X, gy, ch.Right, gy); MkTextR(g, (hi - (hi - lo) * i / 4).ToString("0", CultureInfo.CurrentCulture), Draw.fTiny, Paper.Muted, ch.X - 6 * S, gy - 7 * S); }
                float y100 = (float)(ch.Bottom - (100 - lo) / (hi - lo) * ch.Height); using (var dp = new Pen(Paper.Muted, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash }) g.DrawLine(dp, ch.X, y100, ch.Right, y100);
                for (int k2 = 0; k2 < groups.Length; k2++)
                {
                    var n = norm[k2]; if (n.Count < 2) continue;
                    var pts = n.Select((v, i) => new PointF(ch.X + ch.Width * i / (n.Count - 1), (float)(ch.Bottom - (v - lo) / (hi - lo) * ch.Height))).ToArray();
                    using (var p = new Pen(cols[k2], (k2 == 0 ? 3f : 2.2f) * S) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round }) g.DrawLines(p, pts);
                }
            }
            else MkText(g, L.T("The groups arrive from the Bank of Canada in a moment."), Paper.Body, Paper.Muted, ch.X, ch.Y + 60 * S);
            float lx = x + 16 * S;
            for (int k2 = 0; k2 < groups.Length; k2++) { using (var b = new SolidBrush(cols[k2])) g.FillRectangle(b, lx, r.Bottom - 34 * S, 16 * S, 4 * S); string t = L.T(MarketData.GroupInfo[groups[k2]][0]) + (norm[k2].Count > 0 ? " " + norm[k2].Last().ToString("0", CultureInfo.CurrentCulture) : ""); MkText(g, t, Draw.fSmall, Paper.Ink, lx + 22 * S, r.Bottom - 42 * S); lx += g.MeasureString(t, Draw.fSmall).Width + 44 * S; }
            y = r.Bottom + 16 * S;
            // each group in detail | energy and the loonie
            float lw = w * 0.6f;
            var all = new[] { "ENER", "MTLS", "AGRI", "FOPR", "FISH", "BCPI", "BCNE" };
            var tr = new RectangleF(x, y, lw, 90 * S + all.Length * 38 * S); Paper.Desk(g, tr);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Each Group in Detail", null);
            float ty = y + 56 * S, c2 = x + lw * 0.68f, c3 = x + lw * 0.82f, c4 = x + lw - 16 * S;
            using (var hb = new SolidBrush(Paper.Soft)) g.FillRectangle(hb, x + 8 * S, ty, lw - 16 * S, 26 * S);
            MkText(g, L.T("Group · what's in it"), Draw.fSmall, Paper.Ink, x + 16 * S, ty + 5 * S); MkTextR(g, L.T("Week"), Draw.fSmall, Paper.Ink, c2, ty + 5 * S); MkTextR(g, L.T("3 months"), Draw.fSmall, Paper.Ink, c3, ty + 5 * S); MkTextR(g, L.T("Year"), Draw.fSmall, Paper.Ink, c4, ty + 5 * S);
            ty += 32 * S;
            foreach (var c in all)
            {
                var s = MarketData.Get(c);
                MkText(g, L.T(MarketData.GroupInfo[c][0]), Paper.BodyB, Paper.Head, x + 16 * S, ty);
                using (var mb = new SolidBrush(Paper.Muted)) using (var nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(L.T(MarketData.GroupInfo[c][1]), Paper.Small, mb, new RectangleF(x + 16 * S, ty + 18 * S, c2 - x - 100 * S, 18 * S), nf);
                Paper.Change(g, MarketData.ChangeOver(s, TimeSpan.FromDays(7)), Paper.Small, c2, ty + 8 * S, "0.0"); Paper.Change(g, MarketData.ChangeOver(s, TimeSpan.FromDays(91)), Paper.Small, c3, ty + 8 * S, "0.0"); Paper.Change(g, MarketData.ChangeOver(s, TimeSpan.FromDays(365)), Paper.Small, c4, ty + 8 * S, "0.0");
                ty += 38 * S;
            }
            float ex = x + lw + 16 * S, ew = w - lw - 16 * S;
            var er = new RectangleF(ex, y, ew, 320 * S); Paper.Desk(g, er);
            Paper.DeskTitle(g, ex + 16 * S, y + 12 * S, "Energy and the Loonie", null);
            using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(L.T("When energy prices rise, the Canadian dollar often rises with them. Both over the same time, set to 100 at the start."), Paper.Small, ib, new RectangleF(ex + 16 * S, y + 46 * S, ew - 32 * S, 40 * S));
            var en = MarketData.Window(MarketData.Get("ENER"), from); var usd = MarketData.Window(MarketData.Get("FX:USD"), from);
            var ec = new RectangleF(ex + 16 * S, y + 96 * S, ew - 32 * S, 170 * S);
            if (en.Count >= 2 && usd.Count >= 2)
            {
                // the loonie in US dollars = 1 / (USD in CAD), sampled on the energy index's weeks
                var cadUsd = en.D.Select(d => { double u = MarketData.At(usd, d); return double.IsNaN(u) || u <= 0 ? double.NaN : 1 / u; }).ToList();
                double e0 = en.V[0], c0 = cadUsd.FirstOrDefault(v => !double.IsNaN(v));
                var eN = en.V.Select(v => v / e0 * 100).ToList(); var cN = cadUsd.Select(v => double.IsNaN(v) || double.IsNaN(c0) ? double.NaN : v / c0 * 100).ToList();
                double lo = eN.Concat(cN.Where(v => !double.IsNaN(v))).Min() - 1, hi = eN.Concat(cN.Where(v => !double.IsNaN(v))).Max() + 1;
                Func<List<double>, PointF[]> pts = l => l.Select((v, i) => double.IsNaN(v) ? PointF.Empty : new PointF(ec.X + ec.Width * i / (l.Count - 1), (float)(ec.Bottom - (v - lo) / (hi - lo) * ec.Height))).Where(p => p != PointF.Empty).ToArray();
                var pe = pts(eN); var pc = pts(cN);
                if (pe.Length >= 2) using (var p = new Pen(Paper.Up, 3 * S)) g.DrawLines(p, pe);
                if (pc.Length >= 2) using (var p = new Pen(Paper.Down, 2.4f * S) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash }) g.DrawLines(p, pc);
            }
            using (var b = new SolidBrush(Paper.Up)) g.FillRectangle(b, ex + 16 * S, er.Bottom - 40 * S, 16 * S, 4 * S); MkText(g, L.T("Energy index"), Draw.fSmall, Paper.Ink, ex + 38 * S, er.Bottom - 48 * S);
            using (var b = new SolidBrush(Paper.Down)) g.FillRectangle(b, ex + ew / 2, er.Bottom - 40 * S, 16 * S, 4 * S); MkText(g, L.T("Canadian dollar, in US dollars"), Draw.fSmall, Paper.Ink, ex + ew / 2 + 22 * S, er.Bottom - 48 * S);
            y = Math.Max(tr.Bottom, er.Bottom) + 16 * S;
            // prices per barrel and ounce, with a key
            if (MarketData.HasQuoteKey())
            {
                List<MarketData.Quote> qs; lock (MarketData.k) qs = MarketData.Quotes.Values.ToList();
                var fr = new RectangleF(x, y, w, 70 * S + MarketData.FuturesList.Length * 32 * S); Paper.Desk(g, fr);
                Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Prices per Barrel and Ounce", L.T("futures, from your quote key · delayed while open"));
                float fy = y + 54 * S;
                foreach (var f in MarketData.FuturesList) { var q = qs.FirstOrDefault(z => z.Sym == f[0]); MkText(g, L.T(f[1]), Paper.BodyB, Paper.Head, x + 16 * S, fy); MkTextR(g, q == null ? "-" : q.Price.ToString("N2", CultureInfo.CurrentCulture), Paper.Body, Paper.Ink, x + w * 0.45f, fy); Paper.Change(g, q == null ? double.NaN : q.Pct, Paper.Small, x + w * 0.6f, fy + 2 * S, "0.00"); MkText(g, L.T(f[2]), Paper.Small, Paper.Muted, x + w * 0.66f, fy + 2 * S); fy += 32 * S; }
                y = fr.Bottom + 16 * S;
            }
            return y;
        }

        // ---------- Line Check: the verdict and the tips, then the broker watch itself
        float MkLine(Graphics g, float x, float y, float w)
        {
            var lr = LineCheck.Now(MkMon); float lw = Math.Max(300 * S, w * 0.34f);
            var tips = lr.Tips; float th = 70 * S + Math.Max(1, tips.Count) * 64 * S;
            float h = Math.Max(330 * S, th); var vr = new RectangleF(x, y, lw, h); Paper.Desk(g, vr);
            var ring = new RectangleF(x + (lw - 160 * S) / 2, y + 22 * S, 160 * S, 160 * S); Paper.Ring(g, ring, LineCheck.Checking ? -1 : lr.Score);
            string v = LineCheck.Checking ? L.T("Checking your line...") : L.T(lr.Verdict); var vs = g.MeasureString(v, Paper.Head2); MkText(g, v, Paper.Head2, Paper.Head, x + (lw - vs.Width) / 2, ring.Bottom + 12 * S);
            using (var ib = new SolidBrush(Paper.Ink)) using (var cf = new StringFormat { Alignment = StringAlignment.Center }) g.DrawString(lr.Line, Paper.Body, ib, new RectangleF(x + 16 * S, ring.Bottom + 46 * S, lw - 32 * S, 50 * S), cf);
            if (Cfg.TradeHost != "")
            {
                string bt = L.T("Check my line now (20 s)"); var bs = g.MeasureString(bt, Draw.fBtn); var br = new RectangleF(x + (lw - bs.Width - 36 * S) / 2, vr.Bottom - 58 * S, bs.Width + 36 * S, 42 * S);
                if (LineCheck.Checking) { Theme.Fill(g, br, 6 * S, Paper.Soft); Theme.Stroke(g, br, 6 * S, Paper.Line, 1); } else Theme.Fill(g, br, 6 * S, Paper.Act);   /* busy: a quiet box, not white on grey (review 24 Nov L-5) */
                MkText(g, bt, Draw.fBtn, LineCheck.Checking ? Paper.Muted : Paper.OnFill(Paper.Act), br.X + 18 * S, br.Y + (br.Height - bs.Height) / 2);
                Act(tradeCanvas, br, delegate { LineCheck.Check(() => { try { if (!IsDisposed) BeginInvoke((Action)delegate { tradeCanvas.Invalidate(); }); } catch (Exception e) { if (!(e is InvalidOperationException || e is ObjectDisposedException)) throw; } }); tradeCanvas.Invalidate(); });
            }
            float tx = x + lw + 16 * S, tw = w - lw - 16 * S; var tr = new RectangleF(tx, y, tw, h); Paper.Desk(g, tr);
            float ty = Paper.DeskTitle(g, tx + 16 * S, y + 12 * S, "How to Make It Better", L.T("most helpful first · each from something measured")) + 4 * S;
            if (tips.Count == 0) MkText(g, L.T("Nothing to fix: every check came back fine."), Paper.Body, Paper.Ink, tx + 16 * S, ty);
            MkTips(g, tips, tx, ty, tw);
            y = Math.Max(vr.Bottom, tr.Bottom) + 18 * S;
            return PaintLineDetail(g, x, y, w);
        }
        float MkTips(Graphics g, List<StreamCheck.Tip> tips, float tx, float ty, float tw)
        {
            foreach (var t in tips)
            {
                using (var lp = new Pen(Paper.Line2, 1)) g.DrawLine(lp, tx + 12 * S, ty, tx + tw - 12 * S, ty);
                var nr = new RectangleF(tx + 16 * S, ty + 12 * S, 28 * S, 28 * S); var rc = t.Rank == 1 && tips.Count > 1 ? Paper.Down : t.Rank == 2 && tips.Count > 2 ? Paper.Amber : Paper.Navy; using (var nb = new SolidBrush(Paper.FillOf(rc))) g.FillEllipse(nb, nr);
                var ns = g.MeasureString(t.Rank.ToString(CultureInfo.CurrentCulture), Draw.fBtn); MkText(g, t.Rank.ToString(CultureInfo.CurrentCulture), Draw.fBtn, Paper.OnFill(rc), nr.X + (nr.Width - ns.Width) / 2, nr.Y + (nr.Height - ns.Height) / 2);
                MkText(g, L.T(t.Title), Paper.BodyB, Paper.Head, tx + 56 * S, ty + 10 * S);
                using (var ib = new SolidBrush(Paper.Ink)) g.DrawString(L.T(t.Body), Paper.Small, ib, new RectangleF(tx + 56 * S, ty + 30 * S, tw - 72 * S, 20 * S));
                MkText(g, L.T("seen:") + " " + t.Seen, Draw.fTiny, Paper.Muted, tx + 56 * S, ty + 48 * S);
                ty += 64 * S;
            }
            return ty;
        }

        // ---------- Casual Investor: the week in plain words, Canada's rates, savings, money abroad, keep it light
        float MkCasual(Graphics g, float x, float y, float w)
        {
            float lw = w * 0.56f, rx = x + lw + 16 * S, rw = w - lw - 16 * S;
            float ly = Paper.Ribbon(g, Kit.Sent(L.T("YOUR WEEK IN TWO MINUTES")), x, y) + 10 * S;
            var head = MkHeadline(); var hs = g.MeasureString(head, Paper.Head1, (int)lw); using (var hb = new SolidBrush(Paper.Head)) g.DrawString(head, Paper.Head1, hb, new RectangleF(x, ly, lw, hs.Height + 4)); ly += hs.Height + 8 * S;
            foreach (var f in MkWeekFacts()) { using (var b = new SolidBrush(Paper.Ink)) { g.FillEllipse(b, x + 4 * S, ly + 8 * S, 6 * S, 6 * S); g.DrawString(f.Item2, Paper.Body, b, new RectangleF(x + 18 * S, ly, lw - 18 * S, 24 * S)); } ly += 26 * S; }
            // Canada
            var rate = MarketData.Get("V39079"); var cpi = MarketData.Get("STATIC_TOTALCPICHANGE");
            var cr = new RectangleF(rx, y, rw, 250 * S); Paper.Desk(g, cr);
            Paper.DeskTitle(g, rx + 16 * S, y + 12 * S, "Rates and Prices in Canada", null);
            float sw = (rw - 42 * S) / 2;
            Action<float, float, string, string, string> stat = (sx, sy, a, b, c) => { var sr = new RectangleF(sx, sy, sw, 84 * S); Theme.Fill(g, sr, 6 * S, Paper.CardBg); Theme.Stroke(g, sr, 6 * S, Paper.Line2, 1); MkText(g, a, Paper.Small, Paper.Muted, sr.X + 10 * S, sr.Y + 8 * S); MkText(g, b, Paper.NumS, Paper.Head, sr.X + 10 * S, sr.Y + 30 * S); MkText(g, c, Draw.fTiny, Paper.Muted, sr.X + 10 * S, sr.Y + 62 * S); };
            var since = MarketData.RateSince(rate);
            stat(rx + 16 * S, y + 54 * S, L.T("Bank of Canada policy rate"), rate == null ? "-" : rate.Last.ToString("0.00", CultureInfo.CurrentCulture) + "%", since == DateTime.MinValue ? "" : L.T("unchanged since") + " " + since.ToString("MMM d, yyyy", CultureInfo.CurrentCulture));
            stat(rx + 26 * S + sw, y + 54 * S, L.T("Inflation, past 12 months"), cpi == null ? "-" : cpi.Last.ToString("0.0", CultureInfo.CurrentCulture) + "%", cpi == null ? "" : cpi.LastDay.ToString("MMMM yyyy", CultureInfo.CurrentCulture) + " · " + L.T("Statistics Canada"));
            double buys = cpi == null ? double.NaN : 100 / (1 + cpi.Last / 100);
            stat(rx + 16 * S, y + 148 * S, L.T("What $100 buys now"), double.IsNaN(buys) ? "-" : "$" + buys.ToString("0.00", CultureInfo.CurrentCulture), L.T("of what it bought a year ago"));
            double prevRate = rate == null || since == DateTime.MinValue ? double.NaN : MarketData.At(rate, since.AddDays(-1));
            stat(rx + 26 * S + sw, y + 148 * S, L.T("Last move"), double.IsNaN(prevRate) ? "-" : (rate.Last > prevRate ? "+" : "−") + Math.Abs(rate.Last - prevRate).ToString("0.00", CultureInfo.CurrentCulture) + " " + L.T("points"), since == DateTime.MinValue ? "" : since.ToString("MMM d, yyyy", CultureInfo.CurrentCulture));
            y = Math.Max(ly, cr.Bottom) + 18 * S;
            // savings | money abroad
            float hw = (w - 16 * S) / 2;
            var sr2 = new RectangleF(x, y, hw, 270 * S); Paper.Desk(g, sr2);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "How Savings Grow", L.T("a calculator, not a promise"));
            var fields = new[] { new object[] { L.T("Start with ($)"), svStart }, new object[] { L.T("Add each month ($)"), svMonthly }, new object[] { L.T("Yearly growth (%)"), svRate }, new object[] { L.T("Years"), svYears } };
            for (int i = 0; i < 4; i++)
            {
                float fx = x + 16 * S + (i % 2) * (hw / 2 - 8 * S), fy = y + 54 * S + (i / 2) * 58 * S; MkText(g, (string)fields[i][0], Paper.Small, Paper.Muted, fx, fy);
                var b = MkButton(g, ((double)fields[i][1]).ToString("N0", CultureInfo.CurrentCulture), fx, fy + 18 * S, false); int ii = i;
                Act(tradeCanvas, b, delegate { var v = AskNumber((string)fields[ii][0], (double)fields[ii][1]); if (!v.HasValue) return; if (ii == 0) svStart = v.Value; else if (ii == 1) svMonthly = v.Value; else if (ii == 2) svRate = Math.Min(30, v.Value); else svYears = Math.Max(1, Math.Min(60, Math.Round(v.Value))); tradeCanvas.Invalidate(); });
            }
            double bal = svStart, put = svStart; var yearly = new List<double[]>(); double mr = svRate / 100 / 12;
            for (int yy = 1; yy <= (int)svYears; yy++) { for (int m = 0; m < 12; m++) { bal = bal * (1 + mr) + svMonthly; put += svMonthly; } yearly.Add(new[] { put, bal }); }
            MkText(g, L.T("After") + " " + svYears.ToString("0", CultureInfo.CurrentCulture) + " " + L.T("years"), Paper.Small, Paper.Muted, x + 16 * S, y + 176 * S);
            MkText(g, "$" + bal.ToString("N0", CultureInfo.CurrentCulture), Paper.Num, Paper.Head, x + 16 * S, y + 192 * S);
            MkText(g, L.T("you put in") + " $" + put.ToString("N0", CultureInfo.CurrentCulture) + " · " + L.T("growth") + " $" + (bal - put).ToString("N0", CultureInfo.CurrentCulture), Paper.Small, Paper.Muted, x + 16 * S, y + 240 * S);
            var bars = new RectangleF(x + hw * 0.55f, y + 176 * S, hw * 0.45f - 16 * S, 80 * S); int nb = Math.Min(yearly.Count, 20); double mx = yearly.Count == 0 ? 1 : yearly.Max(z => z[1]);
            for (int i = 0; i < nb; i++) { var v = yearly[(int)((double)i * (yearly.Count - 1) / Math.Max(1, nb - 1))]; float bw2 = bars.Width / nb - 2 * S, bx2 = bars.X + i * bars.Width / nb; float hp = (float)(v[0] / mx) * bars.Height, ht = (float)(v[1] / mx) * bars.Height; using (var pb = new SolidBrush(Paper.Line)) g.FillRectangle(pb, bx2, bars.Bottom - hp, bw2, hp); using (var gb = new SolidBrush(Paper.Up)) g.FillRectangle(gb, bx2, bars.Bottom - ht, bw2, ht - hp); }
            // money abroad
            var ar = new RectangleF(x + hw + 16 * S, y, hw, 270 * S); Paper.Desk(g, ar); float ax = ar.X;
            Paper.DeskTitle(g, ax + 16 * S, y + 12 * S, "Your Money Abroad", L.T("a trip, or money sent home"));
            MkText(g, L.T("Amount"), Paper.Small, Paper.Muted, ax + 16 * S, y + 54 * S); var amb = MkButton(g, abAmt.ToString("N0", CultureInfo.CurrentCulture), ax + 16 * S, y + 72 * S, false);
            Act(tradeCanvas, amb, delegate { var v = AskNumber(L.T("Amount"), abAmt); if (v.HasValue) { abAmt = v.Value; tradeCanvas.Invalidate(); } });
            MkText(g, L.T("Currency"), Paper.Small, Paper.Muted, ax + hw / 2, y + 54 * S); var acb = MkButton(g, abCcy + " · " + L.T(MarketData.FxName(abCcy)) + " ▾", ax + hw / 2, y + 72 * S, false);
            Act(tradeCanvas, acb, delegate { var c = PickCurrency(L.T("Which currency?"), null); if (c != null) { abCcy = c; tradeCanvas.Invalidate(); } });
            var hs2 = MarketData.Get("FX:" + abCcy); double now = MarketData.At(hs2, DateTime.MaxValue), mo = MarketData.At(hs2, hs2 == null ? DateTime.MinValue : hs2.LastDay.AddMonths(-1)), yr = MarketData.At(hs2, hs2 == null ? DateTime.MinValue : hs2.LastDay.AddYears(-1));
            float bw3 = (hw - 52 * S) / 3; var labels = new[] { L.T("Today"), L.T("A month ago"), L.T("A year ago") }; var vals = new[] { now, mo, yr };
            for (int i = 0; i < 3; i++)
            {
                var br2 = new RectangleF(ax + 16 * S + i * (bw3 + 10 * S), y + 130 * S, bw3, 100 * S); Theme.Fill(g, br2, 6 * S, Paper.CardBg); Theme.Stroke(g, br2, 6 * S, Paper.Line2, 1);
                MkText(g, labels[i], Paper.Small, Paper.Muted, br2.X + 10 * S, br2.Y + 8 * S);
                MkText(g, double.IsNaN(vals[i]) ? "-" : "$" + (abAmt * vals[i]).ToString("N0", CultureInfo.CurrentCulture), Paper.NumS, Paper.Head, br2.X + 10 * S, br2.Y + 30 * S);
                if (i > 0 && !double.IsNaN(vals[i]) && !double.IsNaN(now)) { double d = abAmt * (now - vals[i]); MkText(g, "$" + Math.Abs(d).ToString("N0", CultureInfo.CurrentCulture) + " " + (d >= 0 ? L.T("more now") : L.T("less now")), Paper.Small, d > 0 ? Paper.Down : Paper.Up, br2.X + 10 * S, br2.Y + 62 * S); }
                else if (i == 0) MkText(g, L.T("in Canadian dollars"), Paper.Small, Paper.Muted, br2.X + 10 * S, br2.Y + 62 * S);
            }
            y = Math.Max(sr2.Bottom, ar.Bottom) + 16 * S;
            // keep it light: two opt-in nudges
            var kr = new RectangleF(x, y, w, 140 * S); Paper.Desk(g, kr);
            Paper.DeskTitle(g, x + 16 * S, y + 12 * S, "Keep It Light", null);
            var opts = new[] { new object[] { L.T("A Friday evening summary in the corner"), Cfg.CasualFriday, (Action)delegate { Cfg.CasualFriday = !Cfg.CasualFriday; } }, new object[] { L.T("Tell me when a currency on my list moves more than 2% in a week"), Cfg.CasualMoves, (Action)delegate { Cfg.CasualMoves = !Cfg.CasualMoves; } } };
            float oy = y + 52 * S;
            foreach (var o in opts)
            {
                var box = new RectangleF(x + 16 * S, oy, 22 * S, 22 * S); bool on = (bool)o[1];
                if (on) Paper.Tick(g, box, Paper.Act); else { Theme.Fill(g, box, 4 * S, Paper.CardBg); Theme.Stroke(g, box, 4 * S, Paper.Line, 1.4f); }
                MkText(g, (string)o[0] + (on ? "" : "  " + L.T("(off)")), Paper.Body, Paper.Ink, x + 48 * S, oy + 1 * S);
                var act = (Action)o[2]; Act(tradeCanvas, new RectangleF(x + 12 * S, oy - 4 * S, w * 0.7f, 30 * S), delegate { act(); Cfg.Save(); tradeCanvas.Invalidate(); });
                oy += 34 * S;
            }
            return kr.Bottom + 16 * S;
        }
    }
}
