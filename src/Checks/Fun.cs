// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AdeeboMeter
{
    // The fun ones, made only from what AdeeboMeter already measured on this PC (History and the timeline) - nothing new is
    // sent, nothing is uploaded. Every file lands in AdeeboMeter's own folder (data\reports) and only leaves when you share it.
    //   Network therapy: the worst stretch of a day, told as a story (Markdown, and a page with pictures).
    //   Roast my network: a square picture to post - a grade and a few honest burns.
    //   The letter to your provider: polite, measured, devastating. Honest too: if your own Wi-Fi is part of it, it says so.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "result rows and small lists, built once per run")]
    static class Fun
    {
        static string[] R(int lv, string name, string val, string adv, string ev) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev }; }
        static string[] R(int lv, string name, string val, string adv, string ev, string act) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val, adv, ev, act }; }
        static string N1(double v) { return double.IsNaN(v) ? "-" : v.ToString("0.#", CultureInfo.CurrentCulture); }
        static string Hm(DateTime t) { return t.ToString("HH:mm", CultureInfo.CurrentCulture); }
        static string Len(double min) { return min < 1 ? "under a minute" : min < 90 ? (int)Math.Round(min) + " min" : (min / 60).ToString("0.#", CultureInfo.CurrentCulture) + " h"; }
        static string Ssid(Monitor m) { lock (m.L) return m.Ssid ?? ""; }
        static string Clean(string t, string ssid) { return GameSession.Redact(t ?? "", ssid); }   // the Wi-Fi name and addresses never go into a file meant for sharing

        static string Dir() { string d = Path.Combine(Program.DataDir, "reports"); Directory.CreateDirectory(d); return d; }
        static string Write(string name, string text)
        {
            try { string f = Path.Combine(Dir(), name); File.WriteAllText(f, text, new UTF8Encoding(false)); return f; }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Program.Log("fun: " + e.Message); return null; }
        }

        // which part of the path an event is about: 0 PC, 1 your network, 2 provider/internet, 3 DNS (-1: not about the path)
        static int Layer(Timeline.Ev e)
        {
            switch (e.Kind)
            {
                case "wifi": case "lan": case "gw": case "net": return 1;   // "net": this PC's own address, gateway or router changed
                case "dns": return 3; case "inet": return 2;
                case "verdict":   // the headline: which part it names
                    {
                        string t = e.Text.ToLowerInvariant();
                        if (t.Contains("dns")) return 3;
                        if (t.Contains("wi-fi") || t.Contains("router") || t.Contains("cable") || t.Contains("gateway") || t.Contains("network connection") || t.Contains("not connected") || t.Contains("adapter")) return 1;
                        if (t.Contains("your pc") || t.Contains("this pc") || t.Contains("cpu") || t.Contains("memory") || t.Contains("disk")) return 0;
                        return 2;
                    }
                default: return -1;
            }
        }
        static readonly string[] LayerName = { "this PC", "your Wi-Fi and router", "the internet past your router", "DNS (the internet's phone book)" };

        // ================================================================== network therapy
        public static List<string[]> Therapy(Monitor m, string which, Toolkit.Run run)
        {
            var rows = new List<string[]>(); var c = CultureInfo.CurrentCulture;
            DateTime day; string w = (which ?? "").Trim().ToLowerInvariant();
            if (w == "today") day = DateTime.Today;
            else if (w == "yesterday") day = DateTime.Today.AddDays(-1);
            else if (w != "" && DateTime.TryParse(w, c, DateTimeStyles.None, out day)) day = day.Date;
            else if (w != "") return new List<string[]> { R(1, "Which day?", "type today, yesterday or a date like 2026-11-03", "", "i") };
            else if (Cfg.History) { run.Status = "Finding the worst day of the week..."; var p = OverTime.Sum(m, DateTime.Today.AddDays(-6), DateTime.Today, run, false); day = double.IsNaN(p.WorstScore) ? DateTime.Today : p.WorstDay.Date; }
            else day = DateTime.Today;

            var evs = Cfg.History ? History.EventsOf(day) : new List<Timeline.Ev>();
            if (day == DateTime.Today) foreach (var e in m.Events.Recent(300)) if (e.T.Date == day && !evs.Any(x => x.T == e.T && x.Text == e.Text)) evs.Add(e);
            evs = evs.Where(e => Layer(e) >= 0).OrderBy(e => e.T).ToList();
            if (!Cfg.History && day != DateTime.Today) return new List<string[]> { R(1, "History is off", "only the last two hours are kept", "Turn on History in Settings and AdeeboMeter can tell the story of any day.", "i") };

            // the stretches of trouble: problem events no more than 20 minutes apart, then the worst one
            var spells = new List<List<Timeline.Ev>>(); List<Timeline.Ev> cur = null; DateTime lastBad = DateTime.MinValue;
            foreach (var e in evs)
            {
                if (e.Lv >= 1) { if (cur == null || (e.T - lastBad).TotalMinutes > 20) { cur = new List<Timeline.Ev>(); spells.Add(cur); } cur.Add(e); lastBad = e.T; }
                else if (cur != null && (e.T - lastBad).TotalMinutes <= 20) cur.Add(e);   // the "back to normal" belongs to the story
            }
            string dayName = day == DateTime.Today ? "today" : day == DateTime.Today.AddDays(-1) ? "yesterday" : day.ToString("dddd d MMMM", c);
            bool isToday = day == DateTime.Today, isYest = day == DateTime.Today.AddDays(-1);
            if (spells.Count == 0)
            {
                rows.Add(R(0, "The session", Voice.P("Nothing to talk about " + dayName + ". No drama, no drops. Honestly? That's growth.", "No trouble recorded " + dayName + "."), "Therapy works best on a bad day. Try yesterday, or a date.", "m"));
                return rows;
            }
            var worst = spells.OrderByDescending(sp => sp.Sum(e => e.Lv >= 2 ? 3 : e.Lv == 1 ? 1 : 0) + (sp[sp.Count - 1].T - sp[0].T).TotalMinutes / 10).First();
            var bad = worst.Where(e => e.Lv >= 1).ToList();
            DateTime from = worst[0].T, to = worst[worst.Count - 1].T;
            var low = bad.FirstOrDefault(e => e.Lv >= 2) ?? bad[0];
            int blame = bad.GroupBy(Layer).OrderByDescending(gr => gr.Count(e => e.Lv >= 2) * 3 + gr.Count()).First().Key;
            var healed = worst.LastOrDefault(e => e.Lv == 0 && e.T > low.T);
            var outs = Outages.Last(m, Math.Max(1, (int)(DateTime.Today - day).TotalDays + 1)).Where(o => !o.EndUnseen && o.From >= from.AddMinutes(-1) && o.From <= to.AddMinutes(1)).ToList();
            double outMin = outs.Sum(o => o.Len.TotalMinutes);
            string part = from.Hour < 6 ? "night" : from.Hour < 12 ? "morning" : from.Hour < 17 ? "afternoon" : from.Hour < 22 ? "evening" : "night";
            string ssid = Ssid(m);
            // "this evening", "last night", "Tuesday afternoon" - never "today night"
            bool early = from.Hour < 6;
            string when = isToday ? (early ? "early this morning" : part == "night" ? "tonight" : "this " + part) : isYest ? (early ? "early yesterday morning" : part == "night" ? "last night" : "yesterday " + part) : dayName + " " + (early ? "early morning" : part);

            // the story, in five short chapters: each has a mood for its picture
            var ch = new List<string[]>();   // {title, text, buddy mood}
            ch.Add(new[] { Voice.P("Let's talk about it", "When"), Voice.P("Let's talk about " + when + ". " + (healed != null ? "You're safe now. The packets are flowing. Take a breath." : "It may not be over yet - but let's talk it through."), "From " + Hm(from) + " to " + Hm(to) + " " + dayName + "."), healed != null ? "smug" : "worried" });
            ch.Add(new[] { Voice.P("How it started", "First sign"), Voice.P("At " + Hm(bad[0].T) + " something felt off. " + Clean(bad[0].Text, ssid) + ". It seemed small. It always seems small.", Hm(bad[0].T) + ": " + Clean(bad[0].Text, ssid) + "."), "worried" });
            ch.Add(new[] { Voice.P("The low point", "Worst moment"), Voice.P((low == bad[0] ? "It didn't get better from there. " : "Then, at " + Hm(low.T) + ": " + Clean(low.Text, ssid) + ". ") + (outs.Count > 0 ? "The internet went quiet " + (outs.Count == 1 ? "once" : outs.Count + " times") + " - " + Len(outMin) + " with nothing. " : "") + (bad.Count > 2 ? "In total " + bad.Count + " things went wrong in " + Len((to - from).TotalMinutes) + ". " : "") + "It's okay to feel things about this.", Hm(low.T) + ": " + Clean(low.Text, ssid) + ". " + bad.Count + " problems in " + Len((to - from).TotalMinutes) + (outs.Count > 0 ? "; " + outs.Count + " outage(s), " + Len(outMin) + " in total." : ".")), blame == 1 ? "dizzy" : blame == 0 ? "sweaty" : "betrayed" });
            ch.Add(new[] { Voice.P("Healing", "Recovery"), healed != null ? Voice.P("At " + Hm(healed.T) + " it came back: " + Clean(healed.Text, ssid) + ". Healing isn't linear, but it got there.", Hm(healed.T) + ": " + Clean(healed.Text, ssid) + ".") : Voice.P("The record doesn't show a clean recovery - things just went quiet. Sometimes closure doesn't come with a timestamp.", "No recovery recorded in this stretch."), healed != null ? "smug" : "worried" });
            string who = blame == 2 ? Voice.P("This wasn't you. The trouble lived past your router, in " + (string.IsNullOrEmpty(m.Isp) ? "your provider" : m.Isp) + "'s part of the world. You can't fix them. You can only send them a letter (Toolbox › Letter to my provider).", "The trouble was past your router (provider or internet). Toolbox › Letter to my provider writes it up.")
                       : blame == 1 ? Voice.P("This one was close to home: your Wi-Fi or router. It's not a character flaw. A cable, or moving closer to the router, is self-care.", "Most of it was your Wi-Fi or router. A cable or a better spot helps; Toolbox › Wi-Fi has the details.")
                       : blame == 3 ? Voice.P("It was DNS. It's always DNS. Toolbox › DNS race finds you a calmer one.", "Most of it was DNS. Toolbox › DNS race compares servers.")
                       : Voice.P("It was this PC, working too hard. Rest is productive. So is closing a few apps.", "Most of it was this PC being busy. The Apps tab shows what.");
            ch.Add(new[] { Voice.P("What we learned", "Cause"), who, "smug" });

            string title = Voice.P("Network therapy: " + when, "What happened " + dayName);
            rows.Add(new[] { "-1", title, "", "" });
            int lvl = bad.Any(e => e.Lv >= 2) ? 2 : 1;
            foreach (var x in ch) rows.Add(R(x[0] == ch[2][0] ? lvl : 3, x[0], x[1], "", "m"));

            var md = new StringBuilder("# " + title + "\n\n_" + Hm(from) + "-" + Hm(to) + " · measured on this PC by AdeeboMeter_\n\n");
            foreach (var x in ch) md.Append("## " + x[0] + "\n\n" + x[1] + "\n\n");
            md.Append("---\nEvents, as recorded:\n\n");
            foreach (var e in worst) md.Append("- " + Hm(e.T) + " · " + Clean(e.Text, ssid) + "\n");
            string stamp = day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + from.ToString("HHmm", CultureInfo.InvariantCulture);
            string mdf = Write("Therapy-" + stamp + ".md", md.ToString());
            string html = Write("Therapy-" + stamp + ".html", StoryPage(title, Hm(from) + "-" + Hm(to), ch));
            rows.Add(new[] { "-1", "Keep it", "", "" });
            if (html != null) rows.Add(R(0, "The illustrated story", Path.GetFileName(html) + " - opens in your browser", "A page with a picture for each chapter. Print > Save as PDF to keep it.", "m", "show:" + html));
            if (mdf != null) rows.Add(R(3, "Markdown", Path.GetFileName(mdf), "For notes, a blog, Discord or GitHub.", "m", "show:" + mdf));
            rows.Add(R(3, "Copy the story", "as Markdown, to paste anywhere", "Your Wi-Fi name and addresses are left out.", "m", "copy:" + md));
            return rows;
        }

        // the buddy, as a picture in a web page (the same five faces as the widget)
        static string FaceSvg(string mood)
        {
            string body = mood == "betrayed" || mood == "dizzy" ? "#FDE2E1" : mood == "worried" || mood == "sweaty" ? "#FFF1CC" : "#DCF5E3";
            string edge = mood == "betrayed" || mood == "dizzy" ? "#CF222E" : mood == "worried" || mood == "sweaty" ? "#BF8700" : "#2DA44E";
            var s = new StringBuilder("<svg viewBox=\"0 0 20 20\" width=\"96\" height=\"96\"><circle cx=\"10\" cy=\"10\" r=\"9.3\" fill=\"" + body + "\" stroke=\"" + edge + "\" stroke-width=\"1.1\"/><g stroke=\"#28303C\" stroke-width=\"1.3\" stroke-linecap=\"round\" fill=\"none\">");
            switch (mood)
            {
                case "smug": s.Append("<line x1=\"4.5\" y1=\"8\" x2=\"7.5\" y2=\"8\"/><circle cx=\"14\" cy=\"8\" r=\"1.1\" fill=\"#28303C\" stroke=\"none\"/><path d=\"M7 13.5 Q11 15.5 13.5 12.5\"/>"); break;
                case "worried": s.Append("<circle cx=\"6\" cy=\"8.5\" r=\"1.1\" fill=\"#28303C\" stroke=\"none\"/><circle cx=\"14\" cy=\"8.5\" r=\"1.1\" fill=\"#28303C\" stroke=\"none\"/><line x1=\"4\" y1=\"5.5\" x2=\"7.5\" y2=\"4.5\"/><line x1=\"16\" y1=\"5.5\" x2=\"12.5\" y2=\"4.5\"/><line x1=\"7\" y1=\"14\" x2=\"13\" y2=\"14\"/>"); break;
                case "sweaty": s.Append("<circle cx=\"6\" cy=\"8.5\" r=\"1.1\" fill=\"#28303C\" stroke=\"none\"/><circle cx=\"14\" cy=\"8.5\" r=\"1.1\" fill=\"#28303C\" stroke=\"none\"/><path d=\"M7.5 15 Q10 13 12.5 15\"/><ellipse cx=\"15.5\" cy=\"6\" rx=\"1.2\" ry=\"1.7\" fill=\"#5AA0E6\" stroke=\"none\"/>"); break;
                case "dizzy": s.Append("<path d=\"M4.5 6.5 L7.5 9.5 M7.5 6.5 L4.5 9.5 M12.5 6.5 L15.5 9.5 M15.5 6.5 L12.5 9.5\"/><circle cx=\"10\" cy=\"14\" r=\"1.5\"/>"); break;
                default: s.Append("<circle cx=\"6\" cy=\"8\" r=\"1.8\"/><circle cx=\"14\" cy=\"8\" r=\"1.8\"/><circle cx=\"6\" cy=\"8.3\" r=\".6\" fill=\"#28303C\"/><circle cx=\"14\" cy=\"8.3\" r=\".6\" fill=\"#28303C\"/><path d=\"M6.5 15.5 Q10 12.5 13.5 15.5\"/><ellipse cx=\"6\" cy=\"11.5\" rx=\".8\" ry=\"1.3\" fill=\"#5AA0E6\" stroke=\"none\"/>"); break;
            }
            return s.Append("</g></svg>").ToString();
        }
        static string Esc(string s) { return System.Net.WebUtility.HtmlEncode(s ?? ""); }
        static string StoryPage(string title, string when, List<string[]> ch)
        {
            var sb = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>" + Esc(title) + "</title><style>body{font:16px/1.55 'Segoe UI',system-ui,sans-serif;color:#1B2330;background:#F6F7FB;max-width:760px;margin:32px auto;padding:0 16px}h1{font-size:28px;margin:0}.m{color:#5A6474;font-size:13px;margin-bottom:24px}.p{display:flex;gap:18px;align-items:center;background:#fff;border-radius:16px;padding:16px 20px;margin:14px 0;box-shadow:0 1px 3px rgba(0,0,0,.08)}.p h2{font-size:15px;letter-spacing:.04em;text-transform:uppercase;color:#5A6474;margin:0 0 4px}.p p{margin:0}.p svg{flex:none}@media (max-width:520px){.p{flex-direction:column;text-align:center}}@media print{body{background:#fff}.p{box-shadow:none;border:1px solid #E3E8EF}}</style></head><body>");
            sb.Append("<h1>" + Esc(title) + "</h1><div class=\"m\">" + Esc(when) + " · told by AdeeboMeter, from what it measured on this PC</div>");
            foreach (var x in ch) sb.Append("<div class=\"p\">" + FaceSvg(x[2]) + "<div><h2>" + Esc(x[0]) + "</h2><p>" + Esc(x[1]) + "</p></div></div>");
            return sb.Append("</body></html>").ToString();
        }

        // ================================================================== a grade for the last week (or the last minutes)
        sealed class Week { public bool History; public double Smooth = double.NaN, Gw = double.NaN, Net = double.NaN, Loss = double.NaN, OutMin; public int Outages, Days; public string WorstDay = "", Rush = ""; public List<string> Speeds = new List<string>(); public int[] Path; }
        static Week Measure(Monitor m, int days, Toolkit.Run run)
        {
            var w = new Week();
            if (Cfg.History)
            {
                var p = OverTime.Sum(m, DateTime.Today.AddDays(-(days - 1)), DateTime.Today, run, false);
                if (p.Minutes >= 60)
                {
                    w.History = true; w.Days = p.Days; w.Smooth = 100.0 * (p.Minutes - p.BadMinutes) / p.Minutes; w.Gw = p.GwAvg; w.Net = p.NetAvg; w.Loss = p.LossAvg; w.Outages = p.OutageCount; w.OutMin = p.OutageMin; w.Speeds = p.Speeds;
                    if (!double.IsNaN(p.WorstScore)) w.WorstDay = p.WorstDay.ToString("dddd d MMMM", CultureInfo.CurrentCulture);
                    if (p.RushHour >= 0) w.Rush = CultureInfo.CurrentCulture.DateTimeFormat.DayNames[p.RushDow] + "s around " + p.RushHour.ToString("00", CultureInfo.InvariantCulture) + ":00";
                    return w;
                }
            }
            var s = m.Latest;   // no History yet: the last few minutes, said honestly
            w.Smooth = s.Level >= 2 ? 90 : s.Level == 1 ? 98 : 99.9; w.Loss = s.LossPct; w.Gw = double.NaN;
            w.Net = s.Path.Length > 4 && s.Path[4] == 0 && s.Ping > 0 ? s.Ping : double.NaN;   // a ping figure only when the internet really answers - never praise a dead line
            w.Path = (int[])s.Path.Clone();
            return w;
        }
        static string Grade(Week w)
        {
            double sm = double.IsNaN(w.Smooth) ? 0 : w.Smooth, loss = double.IsNaN(w.Loss) ? 0 : w.Loss;
            if (sm >= 99.9 && w.Outages == 0 && loss < 0.2) return "A+";
            if (sm >= 99.5 && w.Outages <= 1 && loss < 0.5) return "A";
            if (sm >= 99 && loss < 1) return "B";
            if (sm >= 97) return "C";
            if (sm >= 93) return "D";
            return "F";
        }
        static List<string> Burns(Week w, string grade, string isp)
        {
            var l = new List<string>(); string prov = string.IsNullOrEmpty(isp) ? "Your provider" : isp;
            bool homeBad = (!double.IsNaN(w.Gw) && w.Gw > 15) || (w.Path != null && w.Path.Length > 1 && w.Path[1] >= 1 && w.Path[1] <= 2);   // the trouble is at home: never blame the provider for it
            if (w.Outages > 0) l.Add(w.Outages + (w.Outages == 1 ? " outage" : " outages") + ", " + Len(w.OutMin) + " of pure silence. Even your group chat isn't that dead.");
            if (!double.IsNaN(w.Gw) && w.Gw > 15) l.Add("Router delay " + N1(w.Gw) + " ms. Your Wi-Fi is buffering its own thoughts.");
            else if (!double.IsNaN(w.Gw) && w.Gw < 3) l.Add("Router delay " + N1(w.Gw) + " ms. Your home network ate. No notes.");
            if (!double.IsNaN(w.Loss) && w.Loss >= 1) l.Add(N1(w.Loss) + "% packet loss. Your packets are ghosting you.");
            if (!double.IsNaN(w.Net) && w.Net > 80) l.Add(N1(w.Net) + " ms to the internet. Long-distance relationship energy.");
            else if (!double.IsNaN(w.Net) && w.Net > 0 && w.Net < 20) l.Add(N1(w.Net) + " ms to the internet. Main character ping.");
            if (w.Rush != "") l.Add("Slowest: " + w.Rush + ". Everyone's online and so is your lag.");
            if (grade.StartsWith("A", StringComparison.Ordinal)) l.Add("Honestly? Hard to roast. " + prov + " is doing okay. For now.");
            else if (grade == "F") l.Add(homeBad ? "The call is coming from inside the house: your own Wi-Fi or router." : prov + " should be paying you.");
            if (l.Count < 3) l.Add("Smooth " + N1(w.Smooth) + "% of the time. " + (w.Smooth >= 99 ? "Not bad. Not legendary either." : "The other " + N1(100 - w.Smooth) + "% did some damage."));
            return l.Take(4).ToList();
        }

        // ================================================================== roast my network: a picture to post
        public static List<string[]> Roast(Monitor m, Toolkit.Run run)
        {
            run.Status = "Collecting receipts...";
            var w = Measure(m, 7, run); string g = Grade(w); var burns = Burns(w, g, "");   // made to be posted: never the provider's name (it says where you live)
            var rows = new List<string[]> { R(g.StartsWith("A", StringComparison.Ordinal) ? 0 : g == "F" || g == "D" ? 2 : 1, "Grade", g + (w.History ? " · the last " + w.Days + " days" : " · the last few minutes (turn on History for a week)"), "", "m") };
            foreach (var b in burns) rows.Add(R(3, "", b, "", "m"));
            string file = null;
            try
            {
                const int Z = 1080;
                using (var bmp = new Bitmap(Z, Z, PixelFormat.Format32bppArgb))
                using (var gr = Graphics.FromImage(bmp))
                {
                    gr.SmoothingMode = SmoothingMode.AntiAlias; gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using (var bg = new LinearGradientBrush(new Rectangle(0, 0, Z, Z), Color.FromArgb(24, 20, 48), Color.FromArgb(90, 24, 70), 45f)) gr.FillRectangle(bg, 0, 0, Z, Z);
                    Color gc = g.StartsWith("A", StringComparison.Ordinal) ? Color.FromArgb(70, 220, 120) : g == "B" || g == "C" ? Color.FromArgb(255, 196, 60) : Color.FromArgb(255, 90, 90);
                    using (var f1 = new Font("Segoe UI", 54, FontStyle.Bold, GraphicsUnit.Pixel)) using (var wb = new SolidBrush(Color.White)) gr.DrawString("my network got roasted", f1, wb, 72, 70);
                    using (var f2 = new Font("Segoe UI", 300, FontStyle.Bold, GraphicsUnit.Pixel)) using (var gb = new SolidBrush(gc)) gr.DrawString(g, f2, gb, 50, 130);
                    var face = new Snap(); face.Path = !w.History && w.Path != null ? w.Path : g.StartsWith("A", StringComparison.Ordinal) ? new[] { 0, 0, 0, 0, 0 } : g == "B" || g == "C" ? new[] { 0, 0, 1, 0, 0 } : new[] { 0, 0, 2, 0, 0 };
                    Draw.Buddy(gr, new RectangleF(Z - 400, 190, 300, 300), face, false);
                    float y = 560;
                    using (var f3 = new Font("Segoe UI", 38, FontStyle.Regular, GraphicsUnit.Pixel)) using (var tb = new SolidBrush(Color.FromArgb(240, 240, 250)))
                        foreach (var b in burns) { var sz = gr.MeasureString(b, f3, Z - 144); if (y + sz.Height > Z - 110) break; gr.DrawString(b, f3, tb, new RectangleF(72, y, Z - 144, sz.Height + 4)); y += sz.Height + 22; }
                    using (var f4 = new Font("Segoe UI", 26, FontStyle.Regular, GraphicsUnit.Pixel)) using (var mb = new SolidBrush(Color.FromArgb(170, 170, 200)))
                        gr.DrawString("AdeeboMeter · measured on my PC, " + (w.History ? "last " + w.Days + " days" : "just now") + " · no data left my PC to make this", f4, mb, 72, Z - 80);
                    string f = Path.Combine(Dir(), "Roast-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".png");
                    bmp.Save(f, ImageFormat.Png); file = f;
                }
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is System.Runtime.InteropServices.ExternalException)) throw; Program.Log("roast: " + e.Message); }
            if (file != null) rows.Add(R(0, "The picture", Path.GetFileName(file) + " - square, ready to post", "No Wi-Fi name, no addresses, no provider name - just the grade and the burns.", "m", "show:" + file));
            rows.Add(R(3, "Copy as text", "for a chat", "", "m", "copy:My network got a " + g + " from AdeeboMeter.\n" + string.Join("\n", burns)));
            return rows;
        }

        // ================================================================== the letter to your provider
        public static List<string[]> Letter(Monitor m, string daysText, Toolkit.Run run)
        {
            int days; if (!int.TryParse((daysText ?? "").Trim(), out days)) days = 30; days = Math.Max(1, Math.Min(180, days));
            run.Status = "Reading " + days + " days of measurements...";
            var w = Measure(m, days, run);
            if (!w.History) return new List<string[]> { R(1, "Not enough history", "the letter needs at least an hour of measurements", (Cfg.History ? "History is on - give it a day or two of normal use, then come back." : "Turn on History in Settings, use the PC for a day or two, then come back.") + " A letter without numbers is just a complaint.", "i") };
            string isp = string.IsNullOrEmpty(m.Isp) ? "" : m.Isp, c = isp == "" ? "Hello," : "Hello " + isp + " team,";
            double lostH = w.OutMin / 60.0, roughMonth = (100 - w.Smooth) / 100 * 24 * 30;
            bool home = !double.IsNaN(w.Gw) && w.Gw > 15;   // our own network was slow too: say so - the letter never lies
            var sb = new StringBuilder();
            sb.Append("Subject: My connection over the last " + days + " days, measured\n\n" + c + "\n\n");
            sb.Append(Voice.P("I really like the internet. I'd like to use it more often. ", ""));
            sb.Append("I've been measuring my connection from my PC for the last " + w.Days + " days. ");
            if (w.Outages > 0) sb.Append("In that time it dropped completely " + w.Outages + (w.Outages == 1 ? " time" : " times") + ", for " + Len(w.OutMin) + " in total" + Voice.P(lostH >= 2 ? " - about " + Math.Round(lostH / 2) + " feature-length film" + (Math.Round(lostH / 2) == 1 ? "" : "s") + " of nothing" : "", "") + ". ");
            sb.Append("It was smooth " + N1(w.Smooth) + "% of the time. " + (w.Smooth < 99.5 ? Voice.P("That sounds high until you work it out: it's roughly " + Len(roughMonth * 60) + " of trouble a month. ", "That is roughly " + Len(roughMonth * 60) + " of trouble a month. ") : ""));
            if (!double.IsNaN(w.Loss) && w.Loss >= 0.5) sb.Append("Packet loss averaged " + N1(w.Loss) + "%. ");
            if (w.WorstDay != "" && w.Outages > 0) sb.Append("The worst day was " + w.WorstDay + ". ");
            if (w.Rush != "") sb.Append("It's slowest on " + w.Rush + ". ");
            sb.Append("\n\n");
            if (home) sb.Append("To be fair to you: part of this is my own Wi-Fi (my router took " + N1(w.Gw) + " ms on average to answer), and I'm fixing that." + (w.Outages > 0 ? " The outages above are the internet check getting no answer at all, which my Wi-Fi alone doesn't explain." : "") + "\n\n");
            else if (!double.IsNaN(w.Gw)) sb.Append("My own network isn't the problem: my router answered in " + N1(w.Gw) + " ms on average the whole time, so the trouble is past my router.\n\n");
            if (w.Speeds.Count > 0) { sb.Append("Recent speed tests:\n"); foreach (var s in w.Speeds.Skip(Math.Max(0, w.Speeds.Count - 5))) sb.Append("- " + s + "\n"); sb.Append('\n'); }
            sb.Append(Voice.P("I'm not angry. I'm just measuring. ", "") + "Could you check my line and tell me what you find? I can send the full measurements if they help.\n\nThank you,\n");
            if (Mood.Name == "brutal" && !Voice.Serious) sb.Append("\nP.S. I'm also measuring how long it takes you to reply.\n");
            string text = sb.ToString(); string ssid = Ssid(m); text = Clean(text, ssid);
            string file = Write("Letter-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".txt", text);
            var rows = new List<string[]>
            {
                R(w.Outages > 0 || w.Smooth < 99 ? 1 : 0, "Your case", (w.Outages > 0 ? w.Outages + " outages · " + Len(w.OutMin) : "no outages") + " · smooth " + N1(w.Smooth) + "%", w.Outages == 0 && w.Smooth >= 99.5 ? Voice.P("Honestly? Your connection's been good. You can send this, but it's more of a thank-you note.", "The connection has been good - there is little to complain about.") : "", "m"),
                R(3, "The letter", text.Replace("\n\n", " ¶ ").Replace("\n", " "), "Read it before you send it - it's your name on it.", "m", "copy:" + text)
            };
            if (home) rows.Add(R(1, "Honest bit", "your own Wi-Fi was slow too - the letter says so", "A provider who sees you've checked your side first takes the rest seriously.", "m"));
            if (file != null) rows.Add(R(3, "Saved", Path.GetFileName(file), "", "m", "show:" + file));
            rows.Add(R(3, "Want proof too?", "Toolbox › Report for my ISP", "Traces the route and shows the hop where the loss starts - attach it to the letter.", "i", "tool:ispreport"));
            return rows;
        }
    }
}
