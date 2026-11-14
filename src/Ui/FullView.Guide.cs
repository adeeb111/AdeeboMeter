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
            if (guideBusy) { tab = "guide"; ShowTab(); SyncNav(); return; }
            Investigate.CheckupSpeed = checkupSpeed;
            tab = "guide"; ShowTab(); SyncNav(); StartGuide("full", false);
        }
        bool? checkupTick;
        bool checkupSpeed { get { if (checkupTick == null) checkupTick = !CheckupMetered(); return checkupTick.Value; } set { checkupTick = value; } }
        bool CheckupMetered()
        {
            if (Cfg.CapGB > 0) return true;   // a monthly cap set in Alerts: don't spend it without asking
            try { System.Net.NetworkInformation.NetworkInterface p; lock (M.L) p = M.Primary; return p != null && (p.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wwanpp || p.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wwanpp2 || p.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ppp); }
            catch (InvalidOperationException) { return false; }
        }
        float PaintCheckupCard(Graphics g, float x, float y, float w)
        {
            var r = guideRun; bool running = guideBusy && r != null && r.Id == "full";
            var cr = new RectangleF(x, y, w, 74 * S); Kit.Card(g, cr, "home", false);
            using (var cb = new SolidBrush(Kit.Soft("home"))) g.FillEllipse(cb, cr.X + 16 * S, cr.Y + 19 * S, 38 * S, 38 * S);
            Kit.Icon(g, "check", new RectangleF(cr.X + 25 * S, cr.Y + 28 * S, 20 * S, 20 * S), Kit.Accent("home"));
            float tx = cr.X + 66 * S, bw = 190 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var m = new SolidBrush(Theme.Muted)) using (var sf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(L.T("Full checkup"), Draw.fHead, ink, tx, cr.Y + 14 * S);
                g.DrawString(running ? L.T("Running") + ": " + r.Status : L.T("This PC, its disks and apps, your Wi-Fi, router, internet and names - one report, biggest problem first."), Draw.fSmall, m, new RectangleF(tx, cr.Y + 38 * S, Math.Max(40 * S, cr.Width - (tx - cr.X) - bw - 262 * S), 18 * S), sf);
            }
            // the speed-test tick (off on a capped or mobile connection) and the button
            var tk = new RectangleF(cr.Right - bw - 230 * S, cr.Y + 26 * S, 220 * S, 22 * S);
            var box = new RectangleF(tk.X, tk.Y + 3 * S, 16 * S, 16 * S); Theme.Fill(g, box, 4 * S, checkupSpeed ? Kit.Accent("home") : Theme.Card); Theme.Stroke(g, box, 4 * S, checkupSpeed ? Kit.Accent("home") : Theme.Line, 1);
            if (checkupSpeed) Kit.Icon(g, "check", new RectangleF(box.X + 2 * S, box.Y + 2 * S, 12 * S, 12 * S), Color.White);
            using (var sb2 = new SolidBrush(Theme.Sub)) g.DrawString(L.T("Include a speed test (100-300 MB)"), Draw.fSmall, sb2, box.Right + 6 * S, tk.Y + 2 * S);
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
                try { BeginInvoke((Action)delegate { guideBusy = false; guideCanvas.Invalidate(); }); } catch { guideBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }

        DateTime guideCopied; string guideNote = "";
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
                    y += ((Investigate.All.Length + cols - 1) / cols) * (chh + 14 * S) + 10 * S;
                    g.DrawString(Voice.P("Every answer says where each fact came from - measured, Windows-reported, seen in packets or inferred - and what I couldn't establish.", "Each fact is labelled: measured, Windows-reported, seen in packets, inferred or unavailable."), Draw.fSmall, muted, new RectangleF(x, y, w, 40 * S));
                    y += 40 * S;
                }
                else
                {
                    string back = "<  All problems"; var bs = g.MeasureString(back, Draw.fBodyB);
                    using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(back, Draw.fBodyB, lb, x, y);
                    Act(guideCanvas, new RectangleF(x, y, bs.Width, bs.Height), delegate { if (!guideBusy) { guideRun = null; guideCanvas.Invalidate(); } });
                    g.DrawString(r.Title.ToUpperInvariant() + (r.Input != "" && r.Id == "site" ? "  ·  " + r.Input.ToUpperInvariant() : "") + "  ·  " + (r.Done ? "DONE " + r.Started.ToString("HH:mm", CultureInfo.CurrentCulture) : (r.Status != "" ? r.Status : "WORKING...")), Draw.fTiny, muted, x + bs.Width + 16 * S, y + 3 * S);
                    y += 32 * S;
                    // the finding
                    string fin = r.Done ? r.Finding : Voice.P("Looking...", "Running...");
                    float fh = Draw.Measure(fin, Draw.fName, (int)(w - 40 * S)).Height;
                    var frc = new RectangleF(x, y, w, fh + 44 * S);
                    Theme.Fill(g, frc, 12 * S, Theme.LvSoft(r.Done ? r.Lv : 3));
                    using (var tb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("FINDING"), Draw.fTiny, tb, x + 18 * S, y + 10 * S);
                    using (var fb = new SolidBrush(r.Done ? (r.Lv == 0 ? Theme.Ink : Theme.LvText(r.Lv)) : Theme.Muted)) g.DrawString(fin, Draw.fName, fb, new RectangleF(x + 18 * S, y + 28 * S, w - 36 * S, fh + 6));
                    y += frc.Height + 18 * S;
                    // observed
                    var obs = r.ObservedCopy();
                    if (obs.Count > 0)
                    {
                        g.DrawString(L.T("WHAT I OBSERVED"), Draw.fTiny, muted, x, y); y += 22 * S;
                        float nameW = Math.Min(280 * S, w * 0.34f), pillW = Cfg.Details ? 170 * S : 120 * S;
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
                            y += rh; g.DrawLine(line, x, y, x + w, y);
                        }
                        y += 16 * S;
                    }
                    if (r.Done)
                    {
                        Func<string, System.Collections.Generic.List<string>, float> list = delegate(string head, System.Collections.Generic.List<string> items)
                        {
                            if (items.Count == 0) return y;
                            g.DrawString(head, Draw.fTiny, muted, x, y); y += 22 * S;
                            foreach (var it in items) { float ih = Draw.Measure(it, Draw.fBody, (int)(w - 24 * S)).Height; g.DrawString(L.T("-"), Draw.fBody, sub, x + 2 * S, y); g.DrawString(it, Draw.fBody, sub, new RectangleF(x + 18 * S, y, w - 24 * S, ih + 4)); y += ih + 8 * S; }
                            return y + 10 * S;
                        };
                        y = list("NOT ESTABLISHED", r.NotEst);
                        y = list("WHAT TO TRY NEXT", r.Next);
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
                        using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
                            foreach (var a in new[] { "It's fixed", "Still broken - check again", (DateTime.Now - guideCopied).TotalSeconds < 3 ? "Copied" : "Copy for someone who can help" })
                            {
                                var asz = g.MeasureString(a, Draw.fBodyB); var ar = new RectangleF(lx, y, asz.Width, asz.Height);
                                g.DrawString(a, Draw.fBodyB, lb, lx, y);
                                if (a == "It's fixed") Act(guideCanvas, ar, delegate { guideNote = "Good. AdeeboMeter keeps watching - if it comes back, it'll tell you."; guideRun = null; guideCanvas.Invalidate(); });
                                else if (a.StartsWith("Still", StringComparison.Ordinal)) Act(guideCanvas, ar, delegate { if (!guideBusy) StartGuide(r2.Id, false); });
                                else Act(guideCanvas, ar, delegate { try { string ssid; lock (M.L) ssid = M.Ssid; Clipboard.SetText(GameSession.Redact(GuideText(r2), ssid)); guideCopied = DateTime.Now; } catch (Exception ce) { CopyFailed(ce); } guideCanvas.Invalidate(); });
                                lx += asz.Width + 26 * S;
                            }
                        y += 34 * S;
                    }
                }
            }
            var need = new Size(0, (int)(y + 20 * S));
            SetNeed(guideCanvas, need);
        }
    }
}
