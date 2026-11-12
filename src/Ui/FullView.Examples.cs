// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // "See an example" on pages that are still empty (no game added yet, no history yet): made-up data under an EXAMPLE
    // banner - never saved, never mixed with yours, gone as soon as there is something real to show.
    sealed partial class FullView
    {
        readonly HashSet<string> exOpen = new HashSet<string>();
        static readonly string[] NetBoxWords = { "host", "server", "site", "address", "target", "domain", "device", "url", "ip", "router", "destination" };

        // self-test: every tool has an example that fits its boxes, shows under the banner and can't be copied or saved
        internal string ExampleCheck()
        {
            EnsureToolbox(); var bad = new List<string>();
            foreach (var t in Tools2())
            {
                var ex = Examples.Get(t.Id); if (ex == null) { bad.Add(t.Id + ": no example"); continue; }
                if (ex.Rows.Count == 0) bad.Add(t.Id + ": no rows");
                if (ex.In.Length > t.Inputs.Length) bad.Add(t.Id + ": " + ex.In.Length + " inputs for " + t.Inputs.Length + " boxes");
                // "Fill the boxes" must never aim a real run at a real device or company: documentation addresses and example.* only
                for (int vi = 0; vi < ex.In.Length; vi++)
                {
                    string v = ex.In[vi], lab = vi < t.Inputs.Length ? (t.Inputs[vi] ?? "").ToLowerInvariant() : "";
                    if (!NetBoxWords.Any(lab.Contains)) continue;   // a box that takes a place on the network
                    System.Net.IPAddress ip; string h = v.Split('/')[0];
                    if (t.Id != "subnet" && System.Net.IPAddress.TryParse(h, out ip) && h.Contains(".") && !(h.StartsWith("192.0.2.", StringComparison.Ordinal) || h.StartsWith("198.51.100.", StringComparison.Ordinal) || h.StartsWith("203.0.113.", StringComparison.Ordinal))) bad.Add(t.Id + ": fills in " + v);
                    string host = v.IndexOf("://", StringComparison.Ordinal) > 0 ? new Uri(v).Host : v;
                    if (t.Id != "b64" && !host.Contains("\\") && !host.Contains(" ") && System.Text.RegularExpressions.Regex.IsMatch(host, @"^[a-z0-9-]+(\.[a-z0-9-]+)*\.[a-z]{2,}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) && !System.Text.RegularExpressions.Regex.IsMatch(host, @"(^|\.)example\.(com|net|org)$|\.(pcapng|pcap|crt|cer|pfx|csr|zip|pdf|adeebo|txt)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) bad.Add(t.Id + ": fills in " + v);
                }
                foreach (var r in ex.Rows) { int lv; if (!int.TryParse(r[0], out lv) || lv < -1 || lv > 3) bad.Add(t.Id + ": level " + r[0]); if (r[4] != "" && Evidence.Label(r[4]) == "") bad.Add(t.Id + ": evidence " + r[4]); }
            }
            foreach (var id in new[] { "page.gamer" }) if (Examples.Get(id) == null) bad.Add(id + ": missing");
            SelectTool("udpmtu"); ShowExample();
            if (!Examples.IsExample(tbRows)) bad.Add("Example didn't show");
            else if (ResultText() != "") bad.Add("an example could be copied");
            RowAction("exfill:udpmtu"); if (tbBoxes.Count < 2 || tbBoxes[0] == null || tbBoxes[0].Box.Text != "example.com" || tbBoxes[1].Box.Text != "443") bad.Add("Fill the boxes didn't fill them");
            if (tbRows != null) bad.Add("filling the boxes left the example showing");
            // the points dashboard: every tool, Learn topic and page is there, and Go lands on it
            var items = PtAll(); int nTools = Tools2().Count, nLearn = Knowledge.Learn().Count;
            int want = nTools + nLearn * 2 /* read + its check question */ + PtPages.Length + (Labs.Length - 1) + 2 /* subnetting streaks */ + 1 /* journey */ + Paths.Length;
            if (items.Count != want) bad.Add("dashboard has " + items.Count + " rows, want " + want);
            if (items.Select(i => i.Key).Distinct().Count() != items.Count) bad.Add("dashboard keys repeat");
            tab = "points"; ShowTab(); if (!pointsPage.Visible) bad.Add("dashboard didn't open");
            var mtr = items.First(i => i.Key == "mtr"); mtr.Go(); if (tab != "toolbox" || tbSel != "mtr" || !Examples.IsExample(tbRows)) bad.Add("Go on MTR didn't open its Example");
            var lt = items.First(i => i.Group == "Learn"); lt.Go(); if (tab != "learn" || learnSel != "topic:" + lt.Key.Substring(6)) bad.Add("Go on a Learn topic");
            items.First(i => i.Key == "page.replay").Go(); if (tab != "replay") bad.Add("Go on Time machine");
            items.First(i => i.Key == "lab.mtu").Go(); if (tab != "learn" || learnSel != "lab:mtu") bad.Add("Go on a lab challenge");
            items.First(i => i.Key == "check.dns").Go(); if (tab != "learn" || learnSel != "topic:dns") bad.Add("Go on a check question");
            pointsSearch.Box.Text = "mtr"; if (!PtFiltered().Any(i => i.Key == "mtr")) bad.Add("search for mtr"); pointsSearch.Box.Text = "";
            var day = ExampleDay.Make(); if (day.M.Count != 1440 || day.M.Any(r => r.Length != History.Cols) || day.E.Count == 0) bad.Add("example day is malformed");
            return string.Join("; ", bad.Take(8));
        }

        float PaintExample(Graphics g, Control c, string id, float x, float y, float w)
        {
            var ex = Examples.Get(id); if (ex == null) return y;
            bool open = exOpen.Contains(id);
            string lk = open ? L.T("Hide the example") : L.T("See an example") + "  ›"; var ls = g.MeasureString(lk, Draw.fBodyB);
            using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(lk, Draw.fBodyB, lb, x, y);
            Act(c, new RectangleF(x, y, ls.Width, ls.Height), delegate { if (!exOpen.Remove(id)) exOpen.Add(id); c.Invalidate(); });
            y += ls.Height + 10 * S;
            if (!open) return y;
            return PaintExampleBanner(g, x, y, w, ex.Rows);
        }
        static float PaintExampleBanner(Graphics g, float x, float y, float w, List<string[]> rows)
        {
            var br = new RectangleF(x, y, w, 30 * S);
            using (var fb = new SolidBrush(Color.FromArgb(Theme.Dark ? 60 : 36, Theme.Amber))) g.FillRectangle(fb, br);
            using (var ab = new SolidBrush(Theme.Amber)) g.FillRectangle(ab, x, y, 4 * S, br.Height);
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(L.T("EXAMPLE") + "  ·  " + L.T("Made-up data - not your PC or network. Nothing was sent."), Draw.fSmall, ink, x + 12 * S, y + 7 * S);
            y += br.Height + 10 * S;
            if (rows != null && rows.Count > 0) y = Rows(g, rows.Select(r => new[] { r[1], r[3] != "" ? r[2] + " - " + r[3] : r[2], r[0] }), x, y, w);
            return y + 6 * S;
        }
    }

    static class ExampleDay
    {
        static readonly double[] PcRow = { 0.4, 2.0 };   // disk ms per request, pages read back per second - quiet
        // A made-up day for Time machine: quiet all day, an evening slowdown 19:00-19:25 with some loss, one Wi-Fi dip.
        // Columns follow History: time, level, cpu, mem, down, gw, net, wifi, up, gwMax, netMax, loss %, link Mb/s, MB down, MB up.
        public static History.Day Make() { return Make(DateTime.Today); }
        public static History.Day Make(DateTime forDay)   // its events carry the day being looked at, not always today
        {
            var d = new History.Day(); var rnd = new Random(7);
            for (int m = 0; m < 24 * 60; m++)
            {
                bool eve = m >= 19 * 60 && m < 19 * 60 + 25, dip = m >= 14 * 60 + 10 && m < 14 * 60 + 14, day = m >= 8 * 60 && m < 23 * 60;
                double gw = 2 + rnd.NextDouble() * 2 + (dip ? 25 : 0), net = (eve ? 70 + rnd.NextDouble() * 60 : 14 + rnd.NextDouble() * 6) + (dip ? 30 : 0);
                double loss = eve ? 2 + rnd.NextDouble() * 4 : 0, wifi = dip ? 28 : 78 + rnd.NextDouble() * 8;
                int lv = eve ? 2 : dip ? 1 : 0;
                double down = day ? 2 + rnd.NextDouble() * (eve ? 60 : 12) : 0.1, up = day ? 0.5 + rnd.NextDouble() * 2 : 0.05;
                d.M.Add(new[] { m * 60.0, lv, day ? 8 + rnd.Next(20) : 3, 52 + rnd.Next(8), down, gw, net, wifi, up, gw * 2.5, net * 1.8, loss, 866, down * 7.5, up * 7.5 });
                d.Net.Add("Wi-Fi Example-Home"); d.Pc.Add(PcRow);
            }
            Func<int, int, DateTime> at = (h, mi) => forDay.Date.AddHours(h).AddMinutes(mi);
            d.E.Add(new Timeline.Ev { T = at(14, 10), Lv = 1, Kind = "wifi", Text = "Wi-Fi signal fell to 28% (it came back at 14:14)" });
            d.E.Add(new Timeline.Ev { T = at(19, 0), Lv = 2, Kind = "net", Text = "Internet slow: 70-130 ms and 2-6% loss past your router - your home network was fine" });
            d.E.Add(new Timeline.Ev { T = at(19, 25), Lv = 0, Kind = "net", Text = "Internet back to normal (16 ms)" });
            return d;
        }
    }
}
