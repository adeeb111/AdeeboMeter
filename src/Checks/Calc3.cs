// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // Money, commodity and game maths (Adeeb, 14 Nov). Arithmetic only - nothing is sent, nothing is advice: these say what
    // the numbers give, never what to do. Currency uses the Bank of Canada rates the Trading page already keeps.
    static partial class Calc
    {
        static double P(string t) { var m = Regex.Match((t ?? "").Replace(",", ""), @"-?[0-9]+(?:\.[0-9]+)?"); double v; return m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }
        static List<double> Ps(string t) { return Regex.Matches((t ?? "").Replace(", ", " "), @"-?[0-9]+(?:\.[0-9]+)?").Cast<Match>().Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList(); }
        static string M(double v) { return (v < 0 ? "-$" : "$") + Math.Abs(v).ToString(Math.Abs(v) >= 1000 ? "N0" : "N2", CultureInfo.CurrentCulture); }
        static string Pc(double v) { return (v > 0 ? "+" : "") + v.ToString("0.##", CultureInfo.CurrentCulture) + "%"; }
        const string Edu = "The numbers only - not advice.";

        // ---------- currency: amount from one currency to another through the Canadian dollar (Bank of Canada, indicative)
        public static List<string[]> FxConv(string amountText, string from, string to, string markupText)
        {
            double amt = P(amountText); if (double.IsNaN(amt)) return Need("Amount", "type e.g. 1000");
            Trading.LoadFx(); List<Trading.Rate> rates; lock (Trading.Rates) rates = Trading.Rates.ToList();
            Func<string, double> cad = c => { c = (c ?? "").Trim().ToUpperInvariant(); if (c == "CAD" || c == "") return 1; var r = rates.FirstOrDefault(x => x.Code == c); return r == null ? double.NaN : r.Cad; };
            double a = cad(from), b = cad(to);
            if (double.IsNaN(a) || double.IsNaN(b)) return new List<string[]> { R(1, "Rates", rates.Count == 0 ? "no Bank of Canada rates on this PC yet - open the Trading page once (Settings > Streaming & trading > Day's numbers on)" : "no rate for " + (double.IsNaN(a) ? from : to), "", "i") };
            double mid = amt * a / b, mk = P(markupText); if (double.IsNaN(mk)) mk = 2.5;
            if (mk < 0 || mk >= 100) return Need("Markup", "0 to under 100% - banks charge about 2-3%");   // FRESH-056
            var day = rates.Count > 0 ? rates.Max(x => x.Day) : DateTime.Today;
            return new List<string[]> {
                R(0, "At the official rate", amt.ToString("N2", CultureInfo.CurrentCulture) + " " + from + " = " + mid.ToString("N2", CultureInfo.CurrentCulture) + " " + to, "Bank of Canada, " + day.ToString("d MMM", CultureInfo.CurrentCulture) + " - indicative, nobody trades at exactly this.", "i"),
                R(1, "What a bank usually gives", (mid * (1 - mk / 100)).ToString("N2", CultureInfo.CurrentCulture) + " " + to + "  (" + mk.ToString("0.##", CultureInfo.CurrentCulture) + "% markup)", "The markup is the real fee - often 2-3% at a bank, under 1% at a currency broker.", "i"),
                R(3, "Rate", "1 " + from + " = " + (a / b).ToString("0.0000", CultureInfo.CurrentCulture) + " " + to, "", "i") };
        }
        // ---------- profit, loss and break-even with fees
        public static List<string[]> Pnl(string buyText, string sellText, string sharesText, string feesText)
        {
            double buy = P(buyText), sell = P(sellText), n = P(sharesText); if (double.IsNaN(buy) || double.IsNaN(n) || n <= 0 || buy <= 0) return Need("Buy price and shares", "type e.g. 150 and 20 - both above 0");   // FRESH-077
            var fxm = Regex.Match(feesText ?? "", @"(-?[0-9.]+)\s*%"); double fx = fxm.Success ? P(fxm.Groups[1].Value) / 100 : 0;
            var fs = Ps(Regex.Replace(feesText ?? "", @"-?[0-9.]+\s*%", " ")); double comm = fs.Count > 0 ? fs[0] : 0;   // FRESH-029: "2%" is a percentage, not also $2
            if (fx < 0 || comm < 0) return Need("Fees", "can't be negative - e.g. 9.99 or 0.5%");
            if (fx >= 1) return Need("Fees", "a percentage fee has to be under 100%");   // FRESH-030
            double cost = buy * n * (1 + fx) + comm, be = (buy * n * (1 + fx) + 2 * comm) / (n * (1 - fx));
            var rows = new List<string[]> { R(3, "You pay", M(cost) + " (" + M(buy * n) + " + fees " + M(cost - buy * n) + ")", "", "i"), R(0, "Break-even sell price", M(be) + "  (" + Pc((be / buy - 1) * 100) + " just to cover the fees)", Edu, "i") };
            if (!double.IsNaN(sell)) { double back = sell * n * (1 - fx) - comm, pl = back - cost; rows.Insert(0, R(pl >= 0 ? 0 : 2, pl >= 0 ? "Profit" : "Loss", M(pl) + "  (" + Pc(pl / cost * 100) + ")", "Selling at " + M(sell) + " brings " + M(back) + " after fees.", "i")); }
            return rows;
        }
        // ---------- position size from the risk you'll accept
        public static List<string[]> PosSize(string accText, string riskText, string entryText, string stopText)
        {
            double acc = P(accText), risk = P(riskText), e = P(entryText), s = P(stopText);
            if (double.IsNaN(acc) || double.IsNaN(e) || double.IsNaN(s) || e == s) return Need("Account, entry and stop", "type e.g. 20000, 1%, 50 and 47");
            if (double.IsNaN(risk)) risk = 1;
            if (acc <= 0) return Need("Account", "more than 0");   // FRESH-055
            if (risk <= 0 || risk > 100) return Need("Risk", "more than 0 and at most 100% of the account");   // FRESH-031
            if (e <= 0 || s < 0) return Need("Entry and stop", "prices above 0");   // FRESH-078
            double cash = acc * risk / 100, per = Math.Abs(e - s); int n = (int)Math.Floor(cash / per); double pos = n * e;
            var rows = new List<string[]> { R(0, "Shares", n.ToString("N0", CultureInfo.CurrentCulture) + " - each loses " + M(per) + " if the stop is hit", "Risking " + M(cash) + " (" + risk.ToString("0.##", CultureInfo.CurrentCulture) + "% of " + M(acc) + "). " + Edu, "i"),
                R(pos > acc ? 1 : 3, "Position", M(pos) + " = " + (pos / acc * 100).ToString("0", CultureInfo.CurrentCulture) + "% of the account" + (pos > acc ? " - more than you have (borrowed money)" : ""), "A stop isn't guaranteed: a price that jumps (gaps) can skip right past it.", "i") };
            return rows;
        }
        // ---------- average cost of buying in steps
        public static List<string[]> AvgCost(string pricesText, string sharesText)
        {
            var p = Ps(pricesText); var n = Ps(sharesText); if (p.Count == 0) return Need("Prices", "type the prices you bought at, e.g. 50, 45, 40");
            if (n.Count == 0) n = p.Select(z => 1.0).ToList();
            if (n.Count != p.Count) return Need("Shares", p.Count + " prices but " + n.Count + " share counts - one count for each price");   // FRESH-032
            if (p.Any(z => z <= 0)) return Need("Prices", "above 0");
            if (n.Any(z => z <= 0)) return Need("Shares", "more than 0 each time - a sale isn't a buy");   // FRESH-079
            int k = p.Count;
            double tot = 0, sh = 0; for (int i = 0; i < k; i++) { tot += p[i] * n[i]; sh += n[i]; }
            if (sh <= 0) return Need("Shares", "type how many each time");
            return new List<string[]> { R(0, "Average cost", M(tot / sh) + " a share", k + " buys, " + sh.ToString("N0", CultureInfo.CurrentCulture) + " shares, " + M(tot) + " in all. " + Edu, "i"), R(3, "Lowest and highest", M(p.Take(k).Min()) + " - " + M(p.Take(k).Max()), "", "i") };
        }
        // ---------- compound growth with monthly additions
        public static double[] Compound(double start, double monthly, double ratePct, int years)
        {
            var v = new double[years + 1]; double bal = start, r = ratePct / 100 / 12; v[0] = bal;
            for (int y = 1; y <= years; y++) { for (int m = 0; m < 12; m++) bal = bal * (1 + r) + monthly; v[y] = bal; }
            return v;
        }
        public static List<string[]> CompoundCalc(string startText, string monthlyText, string rateText, string yearsText)
        {
            double s = P(startText), m = P(monthlyText), r = P(rateText), y = P(yearsText);
            if (double.IsNaN(s)) s = 0; if (double.IsNaN(m)) m = 0; if (double.IsNaN(r) || double.IsNaN(y) || y < 1 || y > 60) return Need("Rate and years", "type e.g. 6% and 20 years");
            if (!Whole(y)) return Need("Years", "whole years, 1 to 60");   // FRESH-033
            if (r <= -100) return Need("Rate", "has to be more than -100% a year");   // FRESH-080
            var v = Compound(s, m, r, (int)y); double put = s + m * 12 * (int)y, end = v[v.Length - 1];
            return new List<string[]> { R(0, "After " + (int)y + " years", M(end), "You put in " + M(put) + "; growth " + M(end - put) + ". A steady " + r.ToString("0.##", CultureInfo.CurrentCulture) + "% a year is an assumption - real returns go up and down. " + Edu, "i"),
                R(3, "Doubles about every", r > 0 ? (72 / r).ToString("0.#", CultureInfo.CurrentCulture) + " years (rule of 72)" : "-", "", "i") };
        }
        // ---------- a fall and the rise it takes to get back
        public static List<string[]> PctBack(string pctText)
        {
            double p = P(pctText); if (double.IsNaN(p) || p <= -100) return Need("Change", "type e.g. -30% or +50%");
            if (p < 0) { double up = (1 / (1 + p / 100) - 1) * 100; return new List<string[]> { R(1, "To get back", Pc(up) + " needed after a " + Pc(p) + " fall", "Falls hurt more than rises help: -50% needs +100%.", "i") }; }
            double down = (1 / (1 + p / 100) - 1) * 100; return new List<string[]> { R(3, "To lose it all again", Pc(down) + " undoes a " + Pc(p) + " rise", "", "i"), R(3, "Same-size fall", "a " + Pc(p) + " rise then " + Pc(-p) + " leaves " + Pc(((1 + p / 100) * (1 - p / 100) - 1) * 100), "", "i") };
        }
        // ---------- how far a price moves while your order travels
        public static List<string[]> TradeLat(string pingText, string priceText, string volText)
        {
            double ping = Bare(pingText) ? P(pingText) : Time(pingText) * 1000, price = P(priceText), vol = P(volText);   // a bare number is ms; "1 s" is 1000 ms (FRESH-081)
            if (double.IsNaN(ping) || ping < 0) return Need("Round trip", "type your broker's ping, e.g. 40 ms");
            if (double.IsNaN(price)) price = 100; if (double.IsNaN(vol)) vol = 2;
            if (price <= 0) return Need("Price", "above 0"); if (vol < 0) return Need("Volatility", "can't be negative - e.g. 2% a day");   // FRESH-058
            double perSec = vol / 100 / Math.Sqrt(23400), move = perSec * Math.Sqrt(ping / 1000) * price;   // FRESH-034: no floor - 0 ms moves nothing   // a typical (one standard deviation) move, 6.5 trading hours
            return new List<string[]> { R(ping < 150 ? 0 : 1, "Typical move while your order travels", M(move) + " on " + M(price) + " (" + (move / price * 100).ToString("0.####", CultureInfo.CurrentCulture) + "%)", (ping < 200 ? "For anyone clicking by hand, " + ping.ToString("0", CultureInfo.CurrentCulture) + " ms is far below what matters - your own reaction takes 200 ms or more." : ping.ToString("N0", CultureInfo.CurrentCulture) + " ms is as long as a person's reaction or longer - in a fast market that's noticeable.") + " Spikes and drops matter more than the average.", "i"),
                R(3, "In that time", "light could cross about " + (ping * 100).ToString("N0", CultureInfo.CurrentCulture) + " km of fibre each way", "", "i") };
        }
        // ---------- commodity units
        static readonly string[][] ComUnits = {   // name, family, how many base units (litre / gram / MJ)
            new[] { "barrel", "v", "158.987" }, new[] { "US gallon", "v", "3.78541" }, new[] { "litre", "v", "1" }, new[] { "m³", "v", "1000" },
            new[] { "troy oz", "m", "31.1035" }, new[] { "gram", "m", "1" }, new[] { "kg", "m", "1000" }, new[] { "lb", "m", "453.592" }, new[] { "tonne", "m", "1000000" }, new[] { "short ton", "m", "907185" },
            new[] { "bushel", "m", "0" }, new[] { "MMBtu", "e", "1055.06" }, new[] { "GJ", "e", "1000" }, new[] { "therm", "e", "105.506" }, new[] { "kWh", "e", "3.6" } };
        static readonly Dictionary<string, double> BushelKg = new Dictionary<string, double> { { "wheat", 27.2155 }, { "soybeans", 27.2155 }, { "corn", 25.4012 } };
        static string[] UnitOf(string t, out double baseUnits, string crop)
        {
            t = (t ?? "").ToLowerInvariant(); baseUnits = double.NaN;
            foreach (var u in ComUnits.OrderByDescending(z => z[0].Length)) if (t.IndexOf(u[0], StringComparison.OrdinalIgnoreCase) >= 0) { baseUnits = u[0] == "bushel" ? BushelKg[BushelKg.ContainsKey(crop) ? crop : "wheat"] * 1000 : double.Parse(u[2], CultureInfo.InvariantCulture); return u; }
            return null;
        }
        public static List<string[]> ComConv(string amountText, string unitText, string cropText)
        {
            double amt = P(amountText); string crop = (cropText ?? "").Trim().ToLowerInvariant(); if (crop == "") crop = "wheat"; double b; var u = UnitOf(unitText, out b, crop);
            if (double.IsNaN(amt) || u == null) return Need("Amount and unit", "e.g. 1 barrel, 1 troy oz, 100 bushel, 1 MMBtu");
            if (u[1] == "m" && !BushelKg.ContainsKey(crop)) return Need("Crop", "wheat, soybeans or corn - a bushel weighs different amounts of each");   // FRESH-035
            var rows = new List<string[]>();
            foreach (var o in ComUnits.Where(z => z[1] == u[1] && z[0] != u[0]))
            {
                double ob = o[0] == "bushel" ? BushelKg[BushelKg.ContainsKey(crop) ? crop : "wheat"] * 1000 : double.Parse(o[2], CultureInfo.InvariantCulture);
                double v = amt * b / ob; rows.Add(R(3, o[0] + (o[0] == "bushel" ? " of " + crop : ""), v.ToString(v >= 100 ? "N0" : v >= 1 ? "N2" : "0.####", CultureInfo.CurrentCulture), "", "i"));
            }
            rows.Insert(0, R(0, amt.ToString("N2", CultureInfo.CurrentCulture) + " " + u[0] + (u[0] == "bushel" ? " of " + crop : ""), u[1] == "v" ? "volume" : u[1] == "m" ? "weight" : "energy", u[0] == "bushel" ? "A bushel is a volume: by weight wheat and soybeans are 60 lb, corn 56 lb." : u[0] == "troy oz" ? "Gold and silver are priced per troy ounce (31.1 g), not the everyday ounce (28.3 g)." : "", "i"));
            return rows;
        }
        // ---------- futures contracts (CME specifications - check your exchange's)
        public static readonly string[][] Futures = {   // name, size, unit, tick
            new[] { "WTI crude oil", "1000", "barrel", "0.01" }, new[] { "Natural gas", "10000", "MMBtu", "0.001" }, new[] { "Gold", "100", "troy oz", "0.10" }, new[] { "Silver", "5000", "troy oz", "0.005" },
            new[] { "Copper", "25000", "lb", "0.0005" }, new[] { "Corn", "5000", "bushel", "0.0025" }, new[] { "Wheat", "5000", "bushel", "0.0025" }, new[] { "Soybeans", "5000", "bushel", "0.0025" } };
        public static List<string[]> FuturesCalc(string contractText, string priceText, string countText, string moveText)
        {
            var c = Futures.FirstOrDefault(z => (contractText ?? "").IndexOf(z[0], StringComparison.OrdinalIgnoreCase) >= 0); double price = P(priceText), n = P(countText), mv = P(moveText);
            if (c == null || double.IsNaN(price)) return Need("Contract and price", "e.g. WTI crude oil at 75 a barrel");
            if (string.IsNullOrWhiteSpace(countText)) n = 1; else if (double.IsNaN(n) || n < 1 || !Whole(n)) return Need("Contracts", "a whole number, 1 or more");   // FRESH-037/083
            double size = double.Parse(c[1], CultureInfo.InvariantCulture), tick = double.Parse(c[3], CultureInfo.InvariantCulture);
            var rows = new List<string[]> {
                R(0, "One contract controls", size.ToString("N0", CultureInfo.CurrentCulture) + " " + c[2] + "s = " + M(size * price) + (n > 1 ? "  ·  " + n + " contracts: " + M(size * price * n) : ""), "You put up only a margin (often 5-15%), so gains and losses run on the whole value. " + Edu, "i"),
                R(3, "Smallest move (one tick)", (tick < 0.01 ? "$" + tick.ToString("0.####", CultureInfo.CurrentCulture) : M(tick)) + " a " + c[2] + " = " + M(tick * size * n), "", "i"),
                R(3, "A $1 move in price", M(size * n), "", "i") };
            if (!double.IsNaN(mv)) rows.Insert(1, R(1, "A " + Pc(mv) + " move", M(size * price * n * mv / 100), "", "i"));
            return rows;
        }
        // ---------- a price per one unit, in another unit and currency (US$ a barrel -> CA¢ a litre)
        public static List<string[]> PerUnit(string priceText, string fromText, string toText, string curText)
        {
            string both = ((fromText ?? "") + " " + (toText ?? "")).ToLowerInvariant(), crop = both.Contains("corn") ? "corn" : both.Contains("soy") ? "soybeans" : "wheat";   // FRESH-084
            double price = P(priceText), fb, tb; var f = UnitOf(fromText, out fb, crop); var t = UnitOf(toText, out tb, crop);
            if (double.IsNaN(price) || f == null || t == null) return Need("Price and units", "e.g. 75 US$ a barrel, in litres");
            if (f[1] != t[1]) return new List<string[]> { R(2, "Units", f[0] + " and " + t[0] + " measure different things", "", "u") };
            double usd = price * tb / fb; string cur = (curText ?? "").Trim().ToUpperInvariant();
            bool bu = f[0] == "bushel" || t[0] == "bushel";
            var rows = new List<string[]> { R(0, "In US$", usd.ToString(usd < 1 ? "0.0000" : "N2", CultureInfo.CurrentCulture) + " a " + t[0] + (bu ? " (a bushel of " + crop + ")" : ""), bu ? "A bushel of " + crop + " weighs " + (crop == "corn" ? "56" : "60") + " lb - write \"bushel of corn\" or \"soybeans\" for the others." : "", "i") };
            if (cur != "" && cur != "USD" && cur != "US$")   // FRESH-057: any currency the Bank of Canada lists, not only CAD
            {
                Trading.LoadFx(); Trading.Rate r, o; lock (Trading.Rates) { r = Trading.Rates.FirstOrDefault(z => z.Code == "USD"); o = Trading.Rates.FirstOrDefault(z => z.Code == cur); }
                double per = cur == "CAD" ? 1 : o != null ? o.Cad : double.NaN; string lab = cur == "CAD" ? "In CA$" : "In " + cur;
                if (r != null && !double.IsNaN(per)) { double x = usd * r.Cad / per; rows.Insert(0, R(0, lab, x.ToString(x < 1 ? "0.0000" : "N2", CultureInfo.CurrentCulture) + " a " + t[0], "at the Bank of Canada's rates (" + r.Day.ToString("d MMM", CultureInfo.CurrentCulture) + "). The raw commodity only - taxes, refining and shipping come on top.", "i")); }
                else rows.Add(R(1, lab, r == null ? "no Bank of Canada rate on this PC yet - open the Trading page once" : "no Bank of Canada rate for " + cur, "", "i"));
            }
            return rows;
        }
        // ---------- how many updates behind you play
        public static List<string[]> TickRate(string tickText, string pingText, string interpText)
        {
            double tick = P(tickText), ping = P(pingText), lerp = P(interpText); if (double.IsNaN(tick) || tick <= 0 || double.IsNaN(ping)) return Need("Tick rate and ping", "e.g. 64 and 40 ms");
            if (double.IsNaN(lerp)) lerp = 2;
            if (ping < 0) return Need("Ping", "can't be negative"); if (lerp < 0) return Need("Interpolation", "0 or more ticks");   // FRESH-038/082
            double tms = 1000 / tick, late = ping / 2 + lerp * tms;
            return new List<string[]> { R(late < 100 ? 0 : late < 160 ? 1 : 2, "You see the game", late.ToString("0", CultureInfo.CurrentCulture) + " ms late = " + (late / tms).ToString("0.#", CultureInfo.CurrentCulture) + " ticks behind", "Half your ping (the update travelling to you) + " + lerp.ToString("0.#", CultureInfo.CurrentCulture) + " ticks of smoothing (interpolation) the game adds on purpose.", "i"),
                R(3, "One tick", tms.ToString("0.#", CultureInfo.CurrentCulture) + " ms at " + tick.ToString("0", CultureInfo.CurrentCulture) + " Hz", "Lag compensation rewinds the server about your ping when you shoot - that's why you can be hit behind a wall.", "i") };
        }
    }
}
