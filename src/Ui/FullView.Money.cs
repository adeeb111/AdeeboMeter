// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== Learn › Money & investing (Adeeb, 14 Nov)
    // A picture for each money topic and the "Ten years of money" game. Learning only: every number is made up, no real
    // company or ticker appears, and nothing ever says what to buy or sell.
    sealed partial class FullView
    {
        // ---------- the topic pictures (pic: in knowledge/learn.txt)
        static float MoneyPic(Graphics g, string pic, float x, float y, float w)
        {
            float h = 170 * S; var r = new RectangleF(x, y + 4 * S, w, h); Kit.Box(g, r, 14 * S);
            float cx0 = r.X + 50 * S, cw = r.Width - 80 * S, cy0 = r.Y + 24 * S, ch = h - 64 * S;
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                switch (pic)
                {
                    case "bullbear": case "dip": case "recession":
                        {
                            // a made-up 20-year index: two bears, a few corrections
                            double[] v = { 100, 112, 125, 121, 140, 158, 150, 103, 118, 140, 165, 180, 172, 196, 214, 200, 158, 190, 222, 250, 268 };   // bug-hunt ML-168: the second bear really is -21% (200 to 158)
                            double max = v.Max() * 1.08, min = v.Min() * 0.85; Func<int, float> X = i => cx0 + cw * i / (v.Length - 1); Func<double, float> Y = q => cy0 + ch - (float)((q - min) / (max - min)) * ch;
                            if (pic == "recession") { var rr = new RectangleF(X(6), cy0, X(9) - X(6), ch); Theme.Fill(g, rr, 6 * S, Theme.LvSoft(2)); VTextC(g, L.T("recession"), Draw.fTiny, Theme.LvText(2), (X(6) + X(9)) / 2, cy0 + 2 * S, X(9) - X(6)); }
                            for (int i = 1; i < v.Length; i++)
                            {
                                bool bear = (i == 7 || i == 16), corr = !bear && v[i] < v[i - 1];
                                using (var p = new Pen(bear ? Theme.Red : corr ? Theme.Amber : Theme.Green, 3 * S) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, X(i - 1), Y(v[i - 1]), X(i), Y(v[i]));
                            }
                            VText(g, pic == "dip" ? L.T("amber: small dips (3-7%) - normal weather") : L.T("green: bull years"), Draw.fSmall, pic == "dip" ? Theme.LvText(1) : Theme.LvText(0), r.X + 18 * S, r.Bottom - 30 * S);
                            VText(g, L.T("red: bear markets (-20% or more)"), Draw.fSmall, Theme.LvText(2), r.X + 280 * S, r.Bottom - 30 * S);
                            break;
                        }
                    case "inflation":
                        {
                            // $100 of buying power at 3% a year
                            float bw = cw / 11 - 8 * S;
                            for (int yr = 0; yr <= 10; yr++) { double val = 100 / Math.Pow(1.03, yr); float bh = (float)(val / 100) * ch; Theme.Fill(g, new RectangleF(cx0 + yr * (bw + 8 * S), cy0 + ch - bh, bw, bh), 5 * S, yr == 0 ? Theme.Green : Theme.Amber); if (yr % 5 == 0) VTextC(g, "$" + val.ToString("0", CultureInfo.CurrentCulture), Draw.fSmall, Theme.Ink, cx0 + yr * (bw + 8 * S) + bw / 2, cy0 + ch - bh - 18 * S, 60 * S); }
                            VText(g, L.T("What $100 buys, year by year, at 3% inflation (today to 10 years)"), Draw.fSmall, Theme.Sub, r.X + 18 * S, r.Bottom - 30 * S);
                            break;
                        }
                    case "compound": case "work": case "fees":
                        {
                            var lines = pic == "compound" ? new[] { new object[] { 6.0, Theme.Green, "6% a year" }, new object[] { 0.0, Theme.Blue, "what you put in" } }
                                : pic == "fees" ? new[] { new object[] { 5.8, Theme.Green, "0.2% fee" }, new object[] { 4.0, Theme.Red, "2% fee" } }
                                : new[] { new object[] { 6.0, Theme.Green, "index fund (made-up 6%)" }, new object[] { 2.0, Theme.Blue, "savings (2%)" }, new object[] { -3.0, Theme.Red, "cash vs 3% inflation" } };
                            int yrs = pic == "fees" ? 30 : 20; double top = 0; var all = new List<double[]>();
                            foreach (var l in lines) { var pts = new double[yrs + 1]; for (int i = 0; i <= yrs; i++) pts[i] = pic == "compound" && (double)l[0] == 0 ? 100 * (1 + i) : (pic == "compound" ? Calc.Compound(0, 100 / 12.0, (double)l[0], i).Last() + 100 : 100 * Math.Pow(1 + (double)l[0] / 100, i)); all.Add(pts); top = Math.Max(top, pts.Max()); }
                            for (int k = 0; k < lines.Length; k++)
                            {
                                var pts = all[k].Select((q, i) => new PointF(cx0 + cw * i / yrs, cy0 + ch - (float)(q / top) * ch)).ToArray();
                                using (var p = new Pen((Color)lines[k][1], 3 * S)) g.DrawLines(p, pts);
                                VText(g, L.T((string)lines[k][2]), Draw.fSmall, (Color)lines[k][1], pts.Last().X - 150 * S, pts.Last().Y - 18 * S);
                            }
                            VText(g, "0", Draw.fTiny, Theme.Muted, cx0, cy0 + ch + 4 * S); VText(g, yrs + " " + L.T("years"), Draw.fTiny, Theme.Muted, cx0 + cw - 40 * S, cy0 + ch + 4 * S);
                            break;
                        }
                    case "emergency":
                        {
                            // the bucket that catches the surprise
                            var bucket = new RectangleF(cx0 + 40 * S, cy0 + 20 * S, 150 * S, ch - 20 * S);
                            Theme.Stroke(g, bucket, 12 * S, Theme.Ink, 2.5f * S); Theme.Fill(g, new RectangleF(bucket.X + 4 * S, bucket.Y + bucket.Height * 0.35f, bucket.Width - 8 * S, bucket.Height * 0.65f - 4 * S), 10 * S, Theme.Blue);
                            VTextC(g, L.T("3-6 months of costs"), Draw.fBodyB, Color.White, bucket.X + bucket.Width / 2, bucket.Y + bucket.Height * 0.6f, bucket.Width);
                            float sx = bucket.Right + 60 * S;
                            foreach (var e in new[] { "car repair $2,000", "job lost: 4 months", "roof leak $3,000" }) { Theme.Fill(g, new RectangleF(sx, cy0 + 10 * S, 150 * S, 30 * S), 8 * S, Theme.LvSoft(2)); VTextC(g, L.T(e), Draw.fSmall, Theme.LvText(2), sx + 75 * S, cy0 + 16 * S, 150 * S); float ey = bucket.Y + 24 * S + (sx - bucket.Right - 60 * S) / (170 * S) * 26 * S; VArrow(g, sx + 75 * S, cy0 + 42 * S, bucket.Right + 6 * S, ey, Theme.Muted, true); sx += 170 * S; if (sx > r.Right - 160 * S) break; }
                            VText(g, L.T("The surprise is paid from the bucket - not a credit card, and not by selling investments in a dip."), Draw.fSmall, Theme.Sub, r.X + 18 * S, r.Bottom - 30 * S);
                            break;
                        }
                    case "budget":
                        {
                            float bx = r.X + 18 * S, bw = r.Width - 36 * S, by = cy0 + 30 * S; var parts = new[] { new object[] { 0.5f, Theme.Blue, "50% needs - rent, food, bills" }, new object[] { 0.3f, VPurple, "30% wants" }, new object[] { 0.2f, Theme.Green, "20% saving and debt" } };
                            float px = bx; foreach (var p in parts) { float pw = bw * (float)p[0]; Theme.Fill(g, new RectangleF(px, by, pw - 4 * S, 44 * S), 8 * S, (Color)p[1]); VText(g, L.T((string)p[2]), Draw.fSmall, Color.White, px + 10 * S, by + 13 * S); px += pw; }
                            VText(g, L.T("Pay yourself first: move the 20% on payday."), Draw.fBodyB, Theme.Ink, bx, by + 60 * S);
                            break;
                        }
                    case "diversify":
                        {
                            // one company falls 60%: alone it's a disaster, in a basket of 100 it's a dent
                            float bx = r.X + 30 * S, by = cy0 + 10 * S; Theme.Fill(g, new RectangleF(bx, by + 40 * S, 60 * S, 40 * S), 6 * S, Theme.Red); Theme.Fill(g, new RectangleF(bx, by, 60 * S, 40 * S), 6 * S, Theme.Line);
                            VText(g, L.T("one company: -60%"), Draw.fSmall, Theme.LvText(2), bx, by + 88 * S);
                            float gx = bx + 160 * S; for (int i = 0; i < 100; i++) Theme.Fill(g, new RectangleF(gx + (i % 20) * 13 * S, by + (i / 20) * 13 * S, 10 * S, 10 * S), 2 * S, i == 37 ? Theme.Red : Theme.Green);
                            VText(g, L.T("one of 100 falls 60%: the basket -0.6%"), Draw.fSmall, Theme.LvText(0), gx, by + 88 * S);
                            break;
                        }
                }
            }
            finally { g.SmoothingMode = sm; }
            using (var mb = new SolidBrush(Theme.Muted)) { string mn = L.T("Made-up numbers, for learning - not advice."); g.DrawString(mn, Draw.fTiny, mb, r.Right - g.MeasureString(mn, Draw.fTiny).Width - 14 * S, r.Bottom - 18 * S); }
            return r.Bottom + 12 * S;
        }

        // ================================================================== the game: ten years of money
        // Each year $6,000 to put away, split between a safe savings account (2%) and an index fund. The year then happens
        // (made up): returns, inflation and surprises. A surprise is paid from the safe money first; if that's short, index
        // shares are sold at whatever they're worth that year - a forced sale in a dip is what the game is about.
        static readonly double[] YrReturn = { 12, 9, -32, 24, 6, 15, -12, 18, 8, 11 }, YrInflation = { 2, 2, 3, 2, 2, 8, 5, 3, 2, 2 }, YrShock = { 0, 0, 2000, 0, 6000, 0, 0, 3000, 0, 0 };
        static readonly string[] YrNews = {
            "A calm year. Prices drift up.", "Good times - everyone at work is talking about shares.", "Fear spreads: the market falls all year (a bear market). Your car needs a $2,000 repair.",
            "A quiet recovery starts - few notice it.", "Recession: your company cuts jobs. You're out of work for four months and need $6,000 to live on.", "Prices jump: groceries cost 8% more than last year (inflation).",
            "A sharp dip: -12% in a few weeks, then calm.", "Back to growth. The roof leaks: $3,000.", "A steady year.", "Another steady year." };
        static readonly string[] YrPicks = { "All into savings (safe, 2%)", "Emergency fund first (to $9,000), the rest into the index", "Half and half", "All into the index fund" };
        int yrAt = -1, yrPick = -1; double ySafe, yIdx, yForced, gCash, gPanicSafe, gPanicIdx; string yrEarn, yrLast = "";
        void YrStart() { yrAt = 0; yrPick = -1; ySafe = yIdx = yForced = gCash = gPanicSafe = gPanicIdx = 0; yrEarn = null; yrLast = ""; NewRun("game.years"); }
        // one year for one strategy; returns what had to be sold in a dip
        static double YrStep(int yr, int pick, ref double safe, ref double idx)
        {
            double put = 6000, toSafe = pick == 0 ? put : pick == 1 ? Math.Min(put, Math.Max(0, 9000 - safe)) : pick == 2 ? put / 2 : 0;
            safe += toSafe; idx += put - toSafe;
            idx *= 1 + YrReturn[yr] / 100; safe *= 1.02;
            double need = YrShock[yr], sold = 0;
            if (need > 0) { double fromSafe = Math.Min(safe, need); safe -= fromSafe; need -= fromSafe; if (need > 0) { sold = Math.Min(idx, need); idx -= sold; } }
            return sold;
        }
        float PaintYears(Graphics g, float x, float y, float w)
        {
            GameTick(false);
            if (yrAt < 0)
            {
                y = Lead2(g, L.T("Ten made-up years. Each year you have $6,000 to put away - you choose where before you know what the year brings, just like real life. Surprises get paid from your safe money first; if that runs out, you have to sell index shares at whatever they're worth that day. At the end: you against two ghosts - one who kept it all in savings, one who panicked and sold in the crash."), x, y, w);
                y = Lead2(g, L.T("Learning only - every number is made up, and AdeeboMeter never says what to buy or sell."), x, y, w);
                Button(learnInfo, g, "Start year 1", true, delegate { YrStart(); learnInfo.Invalidate(); }, x, y); return y + 46 * S;
            }
            // the buckets as they stand
            y = YrBars(g, x, y, w);
            if (yrAt >= 10)
            {
                double infl = YrInflation.Aggregate(1.0, (a, v) => a * (1 + v / 100)), you = ySafe + yIdx, cash = gCash, panic = gPanicSafe + gPanicIdx;
                if (yrEarn == null) yrEarn = EarnFirst("game.years");
                y = Say(g, Voice.P("Ten years done. ", "Done. ") + L.T("You") + " $" + you.ToString("N0", CultureInfo.CurrentCulture) + "  ·  " + L.T("savings-only ghost") + " $" + cash.ToString("N0", CultureInfo.CurrentCulture) + "  ·  " + L.T("panic-seller ghost") + " $" + panic.ToString("N0", CultureInfo.CurrentCulture) + ". " + yrEarn, you >= Math.Max(cash, panic) ? 0 : 1, x, y, w);
                y = Lead2(g, L.T("In today's money (after ten years of inflation) yours is worth about") + " $" + (you / infl).ToString("N0", CultureInfo.CurrentCulture) + ". " + (yForced > 0 ? L.T("You were forced to sell") + " $" + yForced.ToString("N0", CultureInfo.CurrentCulture) + " " + L.T("of index shares in bad years - an emergency fund is what prevents that.") : L.T("You never had to sell in a dip - that's what the emergency fund is for.")), x, y, w);
                Button(learnInfo, g, "Play again", true, delegate { yrAt = -1; learnInfo.Invalidate(); }, x, y); return y + 46 * S;
            }
            y = Say(g, L.T("Year") + " " + (yrAt + 1) + " " + L.T("of 10") + " - " + L.T("where does this year's $6,000 go?"), 1, x, y, w);
            if (yrPick < 0)
                y = Options(g, YrPicks, x, y, w, z => 0, z =>
                {
                    yrPick = Array.IndexOf(YrPicks, z); int yr = yrAt;
                    double sold = YrStep(yr, yrPick, ref ySafe, ref yIdx); yForced += sold;
                    double ci = 0; YrStep(yr, 0, ref gCash, ref ci);
                    YrStep(yr, 3, ref gPanicSafe, ref gPanicIdx); if (yr == 2) { gPanicSafe += gPanicIdx; gPanicIdx = 0; }   // the ghost sells everything at the bottom...
                    if (yr == 5) { gPanicIdx += gPanicSafe; gPanicSafe = 0; }   // ...and buys back after the recovery
                    yrLast = L.T(YrNews[yr]) + "  " + L.T("The index") + " " + (YrReturn[yr] > 0 ? "+" : "") + YrReturn[yr].ToString("0", CultureInfo.CurrentCulture) + "%, " + L.T("prices") + " +" + YrInflation[yr].ToString("0", CultureInfo.CurrentCulture) + "%." + (sold > 0 ? "  " + L.T("Your safe money ran out: you had to sell") + " $" + sold.ToString("N0", CultureInfo.CurrentCulture) + " " + L.T("of index shares") + (YrReturn[yr] < 0 ? " " + L.T("- in a falling year.") : ".") : YrShock[yr] > 0 ? "  " + L.T("Paid from your safe money - nothing sold.") : "");
                    Revealed("game.years"); learnInfo.Invalidate();
                });
            else
            {
                y = Say(g, yrLast, YrReturn[yrAt] < 0 || YrShock[yrAt] > 0 ? 2 : 0, x, y, w);
                Button(learnInfo, g, yrAt + 1 < 10 ? "Next year" : "See how you did", true, delegate { yrAt++; yrPick = -1; learnInfo.Invalidate(); }, x, y); y += 46 * S;
            }
            return y;
        }
        // your two buckets and the ghosts, as bars
        float YrBars(Graphics g, float x, float y, float w)
        {
            double max = Math.Max(1, new[] { ySafe + yIdx, gCash, gPanicSafe + gPanicIdx, 60000 }.Max());
            var rows = new[] { new object[] { L.T("You"), ySafe, yIdx }, new object[] { L.T("Savings-only ghost"), gCash, 0.0 }, new object[] { L.T("Panic-seller ghost"), gPanicSafe, gPanicIdx } };
            float bx = x + 150 * S, bw = w - 260 * S;
            foreach (var rw in rows)
            {
                double s0 = (double)rw[1], i0 = (double)rw[2]; float sw = (float)(bw * s0 / max), iw = (float)(bw * i0 / max);
                VText(g, (string)rw[0], Draw.fBodyB, Theme.Ink, x, y + 2 * S);
                Theme.Fill(g, new RectangleF(bx, y, bw, 20 * S), 6 * S, Theme.Soft);
                if (sw > 1) Theme.Fill(g, new RectangleF(bx, y, sw, 20 * S), 6 * S, Theme.Blue);
                if (iw > 1) Theme.Fill(g, new RectangleF(bx + sw, y, iw, 20 * S), 6 * S, Theme.Green);
                VText(g, "$" + (s0 + i0).ToString("N0", CultureInfo.CurrentCulture), Draw.fBody, Theme.Ink, bx + bw + 8 * S, y + 1 * S);
                y += 28 * S;
            }
            float lx = bx; foreach (var k in new[] { new object[] { Theme.Blue, "safe savings" }, new object[] { Theme.Green, "index fund" } }) { Theme.Fill(g, new RectangleF(lx, y + 4 * S, 10 * S, 10 * S), 3 * S, (Color)k[0]); string t = L.T((string)k[1]); VText(g, t, Draw.fSmall, Theme.Sub, lx + 14 * S, y); lx += 30 * S + g.MeasureString(t, Draw.fSmall).Width; }
            return y + 30 * S;
        }
    }
}
