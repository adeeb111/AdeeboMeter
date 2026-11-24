// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Investigate > Investigate a problem: pick the problem, AdeeboMeter runs the checks and answers
    // Finding · What I observed (with where each fact came from) · Not established · Next test.
    sealed partial class FullView
    {
        Panel guidePage; DB guideCanvas; RoundBox guideBox; GuideRun guideRun; volatile bool guideBusy;

        Panel BuildGuidePage()
        {
            guidePage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            guideBox = new RoundBox(Sc(320), Draw.fInput, "Site, app or server - e.g. outlook.office.com");
            guideBox.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; StartGuide("site", false); } };
            bar.Controls.Add(guideBox);
            bar.Controls.Add(new Label { Text = Voice.P("Pick what's wrong - I'll run the checks and tell you what I saw, what I can't know, and what to try next.", "Choose the problem. AdeeboMeter runs the checks and reports findings, evidence, limits and next steps."), AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fSmall, Margin = new Padding(Sc(10), Sc(10), 0, 0) });
            guideCanvas = new DB { Name = "guideCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            guideCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintGuide(e.Graphics); }; Wire(guideCanvas);
            guidePage.Controls.Add(guideCanvas); guidePage.Controls.Add(bar);
            return guidePage;
        }

        // test = run right here on this thread (self-test; only the flows that send nothing new)
        // Full checkup (1.1.5): from Home or the widget's menu; the report opens on What's wrong? like the other checks
        internal void StartCheckup()
        {
            if (guideBusy) { guideNote = L.T("A check is already running - its report opens here when it's done."); tab = "guide"; ShowTab(); SyncNav(); if (guideCanvas != null) guideCanvas.Invalidate(); return; }   // bug-hunt L-327: said, not silent
            Investigate.CheckupSpeed = checkupSpeed;
            tab = "guide"; ShowTab(); SyncNav(); StartGuide("full", false);
        }
        bool? checkupTick;
        // Review-2: the checkup's speed test is opt-in on a metered line and on Penny (low-data views)
        string checkupTickFor;
        // hunt-5 M-2: decided again whenever what it reads changes (a cap set, Penny picked, a phone hotspot) - your own tick lasts until then
        bool checkupSpeed { get { bool metered = CheckupMetered(); string k = metered + "|" + Cfg.Preset; if (checkupTick == null || checkupTickFor != k) { checkupTick = !metered && Cfg.Preset != "penny"; checkupTickFor = k; } return checkupTick.Value; } set { checkupTick = value; checkupTickFor = CheckupMetered() + "|" + Cfg.Preset; } }
        bool CheckupMetered()
        {
            if (Cfg.CapGB > 0) return true;   // a monthly cap set in Alerts: don't spend it without asking
            try { System.Net.NetworkInformation.NetworkInterface p; lock (M.L) p = M.Primary; return p != null && (p.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wwanpp || p.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wwanpp2); }   // bug-hunt ML-172: PPPoE (fibre, DSL) is a home line, not a metered one - only mobile broadband counts
            catch (InvalidOperationException) { return false; }
        }
        float PaintCheckupCard(Graphics g, float x, float y, float w)
        {
            var r = guideRun; bool running = guideBusy && r != null && r.Id == "full";
            var cr = new RectangleF(x, y, w, 74 * S); Kit.Card(g, cr, "home", false);
            using (var cb = new SolidBrush(Theme.Soft)) g.FillEllipse(cb, cr.X + 16 * S, cr.Y + 19 * S, 38 * S, 38 * S);
            Kit.Icon(g, "check", new RectangleF(cr.X + 25 * S, cr.Y + 28 * S, 20 * S, 20 * S), Theme.Sub);
            float tx = cr.X + 66 * S, bw = 190 * S;
            // the tick's label measured, so it always ends before the button and the description ends before the tick (Adeeb, 22 Nov: they overlapped)
            string tlab = L.T("Include a speed test (up to 500 MB)"); float tlw = (float)Math.Ceiling(g.MeasureString(tlab, Draw.fSmall).Width), tkw = 22 * S + tlw;
            float tkx = cr.Right - bw - 16 * S - 18 * S - tkw;
            using (var ink = new SolidBrush(Theme.Ink)) using (var m = new SolidBrush(Theme.Muted)) using (var sf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(L.T("Full checkup"), Draw.fHead, ink, tx, cr.Y + 14 * S);
                g.DrawString(running ? L.T("Running") + ": " + r.Status : L.T("This PC, its disks and apps, your Wi-Fi, router, internet and names - one report, biggest problem first."), Draw.fSmall, m, new RectangleF(tx, cr.Y + 38 * S, Math.Max(40 * S, tkx - 16 * S - tx), 18 * S), sf);
            }
            // the speed-test tick (off on a capped or mobile connection) and the button
            var tk = new RectangleF(tkx, cr.Y + 26 * S, tkw, 22 * S);   // bug-hunt L-326: ends before the button starts
            var box = new RectangleF(tk.X, tk.Y + 3 * S, 16 * S, 16 * S); Theme.Fill(g, box, 4 * S, checkupSpeed ? Kit.Accent("home") : Theme.Card); Theme.Stroke(g, box, 4 * S, checkupSpeed ? Kit.Accent("home") : Theme.Line, 1);
            if (checkupSpeed) Kit.Icon(g, "check", new RectangleF(box.X + 2 * S, box.Y + 2 * S, 12 * S, 12 * S), Color.White);
            using (var sb2 = new SolidBrush(Theme.Sub)) g.DrawString(tlab, Draw.fSmall, sb2, box.Right + 6 * S, tk.Y + 2 * S);
            Act(overviewCanvas, tk, delegate { checkupSpeed = !checkupSpeed; overviewCanvas.Invalidate(); });
            var br = new RectangleF(cr.Right - bw - 16 * S, cr.Y + 19 * S, bw, 36 * S);
            Theme.Fill(g, br, 10 * S, running ? Theme.Soft : Kit.Accent("home"));
            string bt = running ? L.T("See it  ›") : L.T("Run full checkup"); var bs = g.MeasureString(bt, Draw.fBtn);
            using (var wb = new SolidBrush(running ? Theme.Ink : Theme.OnBlue)) g.DrawString(bt, Draw.fBtn, wb, br.X + (br.Width - bs.Width) / 2, br.Y + (br.Height - bs.Height) / 2);
            Act(overviewCanvas, br, delegate { StartCheckup(); });
            return cr.Bottom + 14 * S;
        }
        void StartGuide(string id, bool test)
        {
            guideNote = "";
            if (guideBusy) return;
            var r = new GuideRun { Input = guideBox.Box.Text };
            guideRun = r; guideCanvas.AutoScrollPosition = Point.Empty;
            if (test) { if (id == "slow" || id == "drops") Investigate.Run(id, M, r, delegate { }); else { r.Id = id; r.Title = id == "full" ? "Full checkup" : Investigate.All.First(f => f.Id == id).Title; r.Conclude(3, "(self-test: not run - it would send traffic)"); r.Done = true; } guideCanvas.Invalidate(); return; }
            guideBusy = true; guideCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            {
                Investigate.Run(id, M, r, delegate { try { BeginInvoke((Action)guideCanvas.Invalidate); } catch { } });
                try { BeginInvoke((Action)delegate { guideBusy = false; guideNote = ""; guideCanvas.Invalidate(); }); } catch { guideBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }

        DateTime guideCopied; string guideNote = ""; volatile bool reportBusy;
        // what someone helping you needs: the problem, what was found, what was seen, what to try - internal addresses removed on copy
        static string GuideText(GuideRun r)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("AdeeboMeter - " + r.Title + (r.Input != "" ? " (" + r.Input + ")" : "") + " - " + r.Started.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture));
            sb.AppendLine("Finding: " + r.Finding);
            foreach (var o in r.ObservedCopy()) sb.AppendLine("  " + o[2] + ": " + o[3] + " [" + o[0] + "]");
            foreach (var n in r.Next) sb.AppendLine("Try: " + n);
            foreach (var n in r.NotEst) sb.AppendLine("Not established: " + n);
            return sb.ToString();
        }
        // icon, colour and one line per problem (the long description stays for hover and the result page)
        static readonly Dictionary<string, string[]> GuideLook = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "whats", new[] { "question", "home", "Layer by layer, stops at the first broken one" } }, { "slow", new[] { "speed", "q.slow", "Finds where the delay starts" } },
            { "site", new[] { "dest", "q.reach", "Name, route, door, lock, answer" } }, { "calls", new[] { "phone", "q.join", "Jitter, loss, upload, Wi-Fi, VPN" } },
            { "drops", new[] { "wifioff", "q.break", "Today's drops and what came just before" } }, { "vpn", new[] { "lock", "q.padlock", "Overlaps, proxy, VPN names, MTU" } },
            { "pc", new[] { "cpu", "q.windows", "CPU, memory, disk, heat, drivers" } }
        };
        // { where it goes, title, icon, one line, accent }
        static readonly string[][] MoreProblems = {
            new[] { "guide:whats", "No internet at all", "wifioff", "Nothing opens - checks every step", "q.break" },
            new[] { "guide:whats", "Wi-Fi says \"No internet\"", "wifi", "Connected, but nothing works", "q.break" },
            new[] { "link", "Can't find my Wi-Fi", "wifi", "The network isn't in the list", "q.join" },
            new[] { "tool:netcap", "Downloads are slow", "speed", "Your plan vs this PC's link and disk", "q.slow" },
            new[] { "tool:lag", "My game lags", "game", "Your Wi-Fi, your line, or the game", "q.traffic" },
            new[] { "case:buffer", "Videos keep buffering", "traffic", "Speed now and what else is busy", "q.slow" },
            new[] { "tool:wakeup", "Slow after sleep or waking", "time", "How long it takes to get back online", "q.hw" },
            new[] { "tool:mail", "Email won't send", "app", "Can this PC reach your mail server?", "q.reach" },
            new[] { "tool:inventory", "A device on my network is missing", "devices", "Everything seen on this network", "q.network" } };
        void PaintGuide(Graphics g)
        {
            g.TranslateTransform(guideCanvas.AutoScrollPosition.X, guideCanvas.AutoScrollPosition.Y);
            float x = 14 * S, y = 8 * S, w = guideCanvas.ClientSize.Width - 28 * S;
            Begin(guideCanvas);
            var r = guideRun;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
            {
                if (r == null)
                {
                    if (guideNote != "") { using (var gb = new SolidBrush(Theme.LvText(0))) g.DrawString(guideNote, Draw.fBodyB, gb, x, y); y += 30 * S; }
                    y = Kit.Header(g, x, y, w, "question", "What's wrong?", "Pick what it feels like - I check it step by step.", "home");
                    // icon tiles, one line each (PLAN 11.5); the full description shows on hover
                    int cols = w > 760 * S ? 3 : 2; float cw = (w - 14 * S * (cols - 1)) / cols, chh = 76 * S;
                    for (int i = 0; i < Investigate.All.Length; i++)
                    {
                        var f = Investigate.All[i]; string[] look; if (!GuideLook.TryGetValue(f.Id, out look)) look = new[] { "question", "home", "" };
                        var cr = new RectangleF(x + (i % cols) * (cw + 14 * S), y + (i / cols) * (chh + 14 * S), cw, chh);
                        Kit.Tile(g, cr, look[0], f.Title.Replace(" (I can't name it)", ""), look[2], look[1], false, -1);
                        string id = f.Id; Act(guideCanvas, cr, delegate { StartGuide(id, false); }); Hint(guideCanvas, cr, "guide." + id);
                    }
                    // more problems (22 Nov): each opens a check that already exists - a guide, a case, a page or a tool
                    for (int j = 0; j < MoreProblems.Length; j++)
                    {
                        int i = Investigate.All.Length + j; var p = MoreProblems[j];
                        var cr = new RectangleF(x + (i % cols) * (cw + 14 * S), y + (i / cols) * (chh + 14 * S), cw, chh);
                        Kit.Tile(g, cr, p[2], p[1], p[3], p[4], false, -1);
                        string go = p[0]; Act(guideCanvas, cr, delegate { FixGo(go); });
                    }
                    int all = Investigate.All.Length + MoreProblems.Length;
                    y += ((all + cols - 1) / cols) * (chh + 14 * S) + 10 * S;
                    g.DrawString(Voice.P("Every answer says where each fact came from - measured, Windows-reported, seen in packets or inferred - and what I couldn't establish.", "Each fact is labelled: measured, Windows-reported, seen in packets, inferred or unavailable."), Draw.fSmall, muted, new RectangleF(x, y, w, 40 * S));
                    y += 40 * S;
                }
                else
                {
                    var bk = Kit.Back(g, "All problems", x, y, "home", false);
                    Act(guideCanvas, bk, delegate { if (!guideBusy) { guideRun = null; guideCase = null; guideCanvas.Invalidate(); } });
                    y += bk.Height + 14 * S;
                    if ((guideBusy || reportBusy) && guideNote != "") { using (var gb = new SolidBrush(Theme.LvText(1))) g.DrawString(guideNote, Draw.fBodyB, gb, x, y); y += 30 * S; }   // L-327: the "already running" note shows here too
                    if (CaseShowing) y = PaintCaseHead(g, x, y, w);   // a case: what, when, from where, how sure, the minutes around it (FullView.Case.cs)
                    // 24 Nov (the approved result mockup): what was checked and when, in plain words - no capitals
                    g.DrawString(r.Title + (r.Input != "" && r.Id == "site" ? "  ·  " + r.Input : "") + "  ·  " + (r.Done ? L.T("checked at") + " " + r.Started.ToString("HH:mm", CultureInfo.CurrentCulture) : (r.Status != "" ? r.Status : L.T("checking..."))), Draw.fSmall, sub, x, y);
                    y += 26 * S;
                    // the finding
                    string fin = r.Done ? r.Finding : Voice.P("Looking...", "Running...");
                    // the answer on a white card in big ink type, a dot in its level beside it (no tinted wash, no label above it)
                    float fh = Draw.Measure(fin, Draw.fBig, (int)(w - 64 * S)).Height;
                    var frc = new RectangleF(x, y, w, fh + 40 * S); Kit.Box(g, frc, 14 * S);
                    Draw.LvDot(g, r.Done ? r.Lv : 3, x + 20 * S, y + 20 * S + Draw.fBig.GetHeight(g) / 2 - 5 * S, 10 * S, 10 * S);
                    using (var fb = new SolidBrush(r.Done ? Theme.Ink : Theme.Muted)) g.DrawString(fin, Draw.fBig, fb, new RectangleF(x + 40 * S, y + 20 * S, w - 64 * S, fh + 6));
                    y += frc.Height + 16 * S;
                    // observed
                    var obs = r.ObservedCopy();
                    if (obs.Count > 0)
                    {
                        g.DrawString(L.T("What I measured"), Draw.fHead, ink, x, y); y += 28 * S;
                        float nameW = Math.Min(280 * S, w * 0.34f), pillW = Cfg.Details ? 170 * S : 120 * S;
                        float ox = x, ow = w; x += 16 * S; w -= 32 * S;   // rows sit inside a raised card
                        float oh = 12 * S;
                        foreach (var o in obs) oh += Math.Max(30 * S, Draw.Measure(o[3], Draw.fBody, (int)(w - nameW - pillW - 30 * S)).Height + 12 * S);
                        Kit.Box(g, new RectangleF(ox, y, ow, oh + 6 * S)); y += 9 * S;
                        int oi = 0;
                        foreach (var o in obs)
                        {
                            int lv; int.TryParse(o[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv);
                            float vh = Draw.Measure(o[3], Draw.fBody, (int)(w - nameW - pillW - 30 * S)).Height, rh = Math.Max(30 * S, vh + 12 * S);
                            Draw.LvDot(g, lv, x, y + 9 * S, 10 * S, 10 * S);
                            g.DrawString(o[2], Draw.fBodyB, ink, new RectangleF(x + 20 * S, y + 5 * S, nameW - 24 * S, rh));
                            using (var vb = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Sub)) g.DrawString(o[3], Draw.fBody, vb, new RectangleF(x + nameW, y + 5 * S, w - nameW - pillW - 10 * S, vh + 4));
                            DateTime at; DateTime? atN = DateTime.TryParse(o[4], CultureInfo.CurrentCulture, DateTimeStyles.None, out at) ? at : (DateTime?)null;
                            float px = x + w - pillW + 4 * S; Evidence.Pill(g, o[0], px, y + 6 * S, atN);
                            Hint(guideCanvas, new RectangleF(px, y, pillW, rh), Evidence.Key(o[0]));
                            y += rh; if (++oi < obs.Count) g.DrawLine(line, x, y, x + w, y);
                        }
                        x = ox; w = ow; y += 24 * S;
                    }
                    if (r.Done)
                    {
                        Func<string, System.Collections.Generic.List<string>, float> list = delegate(string head, System.Collections.Generic.List<string> items)
                        {
                            if (items.Count == 0) return y;
                            // a card per list (24 Nov mockup): numbered steps for what to do, plain dots for what couldn't be told
                            bool steps = head == "WHAT TO TRY NEXT"; float ch0 = 16 * S + 30 * S;
                            foreach (var it in items) ch0 += Math.Max(26 * S, Draw.Measure(it, Draw.fBody, (int)(w - 76 * S)).Height + 10 * S);
                            Kit.Box(g, new RectangleF(x, y, w, ch0), 14 * S); float lx0 = x + 20 * S; y += 16 * S;
                            g.DrawString(L.T(steps ? "What to do" : "What I couldn't tell"), Draw.fHead, ink, lx0, y); y += 30 * S; int k = 0;
                            foreach (var it in items)
                            {
                                k++; float ih = Draw.Measure(it, Draw.fBody, (int)(w - 76 * S)).Height;
                                if (steps) { var nr = new RectangleF(lx0, y, 22 * S, 22 * S); Theme.Fill(g, nr, 11 * S, Theme.Soft); string ns = k.ToString(CultureInfo.CurrentCulture); var nsz = g.MeasureString(ns, Draw.fSmall); g.DrawString(ns, Draw.fSmall, sub, nr.X + (nr.Width - nsz.Width) / 2, nr.Y + (nr.Height - nsz.Height) / 2); }
                                else Draw.LvDot(g, 3, lx0 + 7 * S, y + 7 * S, 8 * S, 8 * S);
                                g.DrawString(it, Draw.fBody, ink, new RectangleF(lx0 + 34 * S, y + 1 * S, w - 76 * S, ih + 4)); y += Math.Max(26 * S, ih + 10 * S);
                            }
                            y += 14 * S;
                            return y + 10 * S;
                        };
                        y = list("WHAT TO TRY NEXT", r.Next);
                        y = list("NOT ESTABLISHED", r.NotEst);
                        if (r.GoTab != "")
                        {
                            var ls = g.MeasureString(r.GoLabel + "  >", Draw.fBodyB); var br = new RectangleF(x, y, ls.Width + 28 * S, 34 * S);
                            Theme.Fill(g, br, 17 * S, Theme.Blue);
                            using (var wb = new SolidBrush(Theme.OnBlue)) g.DrawString(r.GoLabel + "  >", Draw.fBodyB, wb, x + 14 * S, y + (34 * S - ls.Height) / 2);
                            string go = r.GoTab; Act(guideCanvas, br, delegate { if (go.StartsWith("guide:", StringComparison.Ordinal)) StartGuide(go.Substring(6), false); else { tab = go; ShowTab(); } });
                            y += 50 * S;
                        }
                        // for everyone: did it help, try again, or hand it to someone who can help
                        float lx = x; var r2 = r;
                        using (var lb = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.BlueDark))
                            foreach (var a in (CaseShowing ? new[] { (DateTime.Now - reportAt).TotalSeconds < 6 ? "Report saved - show it" : "Make a report (PDF)", "Learn why" } : Array.Empty<string>()).Concat(new[] { "It's fixed", "Still broken - check again", (DateTime.Now - guideCopied).TotalSeconds < 3 ? "Copied" : "Copy for someone who can help" }))
                            {
                                var asz = g.MeasureString(L.T(a), Draw.fBodyB); var ar = new RectangleF(lx, y, asz.Width + 28 * S, 34 * S);
                                if (ar.Right > x + w && lx > x) { lx = x; y += 44 * S; ar = new RectangleF(lx, y, ar.Width, ar.Height); }
                                Theme.Fill(g, ar, 17 * S, a == "It's fixed" ? Theme.BlueSoft : Theme.Soft);
                                g.DrawString(L.T(a), Draw.fBodyB, lb, lx + 14 * S, y + (34 * S - asz.Height) / 2);
                                if (a.StartsWith("Make a report", StringComparison.Ordinal) || a.StartsWith("Report saved", StringComparison.Ordinal)) { var cc = guideCase; Act(guideCanvas, ar, delegate
                                {
                                    if (a.StartsWith("Report saved", StringComparison.Ordinal) && reportFile != "") { RowAction("show:" + reportFile); return; }
                                    if (reportBusy) return;
                                    // bug-hunt L-328: printed to PDF on a thread of its own (printing wants STA) - the window never waits on it
                                    reportBusy = true; Cursor = Cursors.AppStarting; guideNote = L.T("Making the report...");
                                    var th = new System.Threading.Thread(() =>
                                    {
                                        string made = ""; try { made = MakeReport(cc); } catch (Exception re) { if (re is OutOfMemoryException) throw; Program.Log("report: " + re); }
                                        try { BeginInvoke((Action)delegate { reportBusy = false; Cursor = Cursors.Default; reportFile = made; reportAt = DateTime.Now; guideNote = ""; if (reportFile != "") RowAction("show:" + reportFile); guideCanvas.Invalidate(); }); } catch (InvalidOperationException) { reportBusy = false; }
                                    }) { IsBackground = true, Name = "report" };
                                    th.SetApartmentState(System.Threading.ApartmentState.STA); th.Start(); guideCanvas.Invalidate();
                                }); }
                                else if (a == "Learn why") { string tp = guideCase.Topic; Act(guideCanvas, ar, delegate { GoTopic(tp); }); }
                                else if (a == "It's fixed") Act(guideCanvas, ar, delegate { guideNote = "Good. AdeeboMeter keeps watching - if it comes back, it'll tell you."; guideRun = null; guideCanvas.Invalidate(); });
                                else if (a.StartsWith("Still", StringComparison.Ordinal)) Act(guideCanvas, ar, delegate { if (!guideBusy) StartGuide(r2.Id, false); });
                                else Act(guideCanvas, ar, delegate { try { string ssid; lock (M.L) ssid = M.Ssid; Clipboard.SetText(GameSession.Redact(GuideText(r2), ssid)); guideCopied = DateTime.Now; } catch (Exception ce) { CopyFailed(ce); } guideCanvas.Invalidate(); });
                                lx += ar.Width + 10 * S;
                            }
                        y += 48 * S;
                    }
                }
            }
            var need = new Size(0, (int)(y + 20 * S));
            SetNeed(guideCanvas, need);
        }
    }
}
