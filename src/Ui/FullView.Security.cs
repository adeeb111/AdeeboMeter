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
    // Security: one "Security checkup" button -> a scorecard, every check also runnable on its own.
    // Defensive only; every fix opens the Windows screen (or your router's page) where you change it yourself.
    sealed partial class FullView
    {
        Panel securityPage; DB secCanvas; RoundBox secSite; PillButton secRun;
        readonly Dictionary<string, List<string[]>> secRes = new Dictionary<string, List<string[]>>();
        volatile bool secBusy; volatile string secNow = ""; DateTime secAt; volatile bool secStop; DateTime secCopied;
        readonly HashSet<string> secOpen = new HashSet<string>(StringComparer.Ordinal);   // the groups opened to show every check
        static readonly string[][] SecGroups = { new[] { "pc", "THIS PC" }, new[] { "net", "YOUR NETWORK" }, new[] { "sites", "YOUR SITES" } };

        Panel BuildSecurityPage()
        {
            securityPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(0, Sc(64)), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), Sc(8), Sc(6)), WrapContents = true };
            secRun = new PillButton(L.T("Run security checkup"), PillButton.K.Primary); secRun.Click += delegate { if (secBusy) secStop = true; else SecRun(null); };
            secSite = new RoundBox(Sc(320), Draw.fInput, L.T("Your website or mail domain (optional)")) { Margin = new Padding(Sc(10), 0, 0, 0) };
            secSite.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (!secBusy) SecRun(null); } };
            bar.Controls.Add(secRun); bar.Controls.Add(secSite);
            secCanvas = new DB { Name = "secCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            secCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintSecurity(e.Graphics); }; Wire(secCanvas);
            securityPage.Controls.Add(secCanvas); securityPage.Controls.Add(bar);
            return securityPage;
        }
        static readonly char[] SiteSep = { ' ', ',', ';' };
        List<string> SecSites()
        {
            var l = new List<string>(); string t = secSite.Box.Text.Trim(); if (t != "") l.AddRange(t.Split(SiteSep, StringSplitOptions.RemoveEmptyEntries));
            l.AddRange(Cfg.Pins); return l;
        }
        // one check (id) or all of them (null); results land as they come, the page repaints after each
        void SecRun(string only)
        {
            if (secBusy) return; secBusy = true; secStop = false; secRun.Text = AdeeboMeter.L.T("Stop");
            var sites = SecSites(); var list = Security.All.Where(c => only == null || c.Id == only).ToList();
            if (only == null) { lock (secRes) secRes.Clear(); Program.Log("security checkup started"); }
            secCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            { var __bz = Busy.Begin(L.T("Running the security checkup")); try { 
                foreach (var c in list)
                {
                    if (secStop) break; secNow = c.Title;
                    try { BeginInvoke((Action)delegate { secCanvas.Invalidate(); }); } catch (InvalidOperationException) { }
                    var r = Security.RunOne(c, M, sites); lock (secRes) secRes[c.Id] = r;
                }
                secNow = ""; secAt = DateTime.Now;
                try { BeginInvoke((Action)delegate { secBusy = false; secRun.Text = AdeeboMeter.L.T("Run security checkup"); secCanvas.Invalidate(); }); } catch (InvalidOperationException) { secBusy = false; }
            } finally { __bz.End(); } }, TaskCreationOptions.LongRunning);
        }
        static int SecLevel(List<string[]> r) { var l = r.Select(x => int.Parse(x[0], CultureInfo.InvariantCulture)).ToList(); return l.Contains(2) ? 2 : l.Contains(1) ? 1 : l.Contains(0) ? 0 : 3; }
        static readonly HashSet<string> SecTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UserAccountControlSettings.exe", "SystemPropertiesRemote.exe", "optionalfeatures.exe", "ncpa.cpl", "certmgr.msc", "certlm.msc", "taskschd.msc", "netplwiz.exe", "taskmgr.exe" };
        static void SecFix(string f)
        {
            try
            {
                if (f.StartsWith("ms-settings:", StringComparison.Ordinal) || f.StartsWith("windowsdefender:", StringComparison.Ordinal)) { using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(f) { UseShellExecute = true })) { } }
                else if (f.StartsWith("url:http://", StringComparison.Ordinal) || f.StartsWith("url:https://", StringComparison.Ordinal)) { Uri u; if (Uri.TryCreate(f.Substring(4), UriKind.Absolute, out u)) using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true })) { } }
                else if (f == "shell:RecycleBinFolder") { using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), f) { UseShellExecute = false })) { } }
                else if (SecTools.Contains(f)) { using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(Environment.SystemDirectory, f)) { UseShellExecute = true })) { } }
            }
            catch (Exception e) { if (!(e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is System.IO.FileNotFoundException)) throw; Program.Log("security fix " + f + ": " + e.Message); }
        }
        void PaintSecurity(Graphics g)
        {
            g.TranslateTransform(secCanvas.AutoScrollPosition.X, secCanvas.AutoScrollPosition.Y);
            Begin(secCanvas);
            float x = 20 * S, y = 14 * S, w = secCanvas.ClientSize.Width - 40 * S;
            Dictionary<string, List<string[]>> res; lock (secRes) res = new Dictionary<string, List<string[]>>(secRes);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var link = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            {
                // 24 Nov (the approved mockup): one summary card with the counts, the things to fix first with their button, then
                // the three groups as one line each with their checks as small chips (open a group for all of them)
                int[] n = new int[4]; foreach (var r in res.Values) n[SecLevel(r)]++;
                int toFix = n[1] + n[2];
                {
                    string head = secBusy ? L.T("Checking") + ": " + secNow + "..." : res.Count == 0 ? L.T("Security checkup") : toFix == 0 ? L.T("Nothing to fix on this PC.") : string.Format(L.T(toFix == 1 ? "{0} thing to fix on this PC." : "{0} things to fix on this PC."), toFix);
                    string sub = secBusy ? res.Count + " " + L.T("of") + " " + Security.All.Count : res.Count == 0 ? Security.All.Count + " " + L.T("checks of this PC, your network and your sites. Nothing is changed - each fix opens the Windows screen where you change it.") : res.Count + " " + L.T("checks") + " · " + L.T("nothing was changed - each fix opens the Windows screen where you change it.");
                    float numW = res.Count > 0 ? 260 * S : 0, tw = w - 40 * S - numW;
                    float hh = Draw.Measure(head, Draw.fBig, (int)tw).Height, sh = Draw.Measure(sub, Draw.fSmall, (int)tw).Height;
                    var card = new RectangleF(x, y, w, Math.Max(76 * S, 18 * S + hh + 4 * S + sh + 18 * S)); Kit.Box(g, card, 14 * S);
                    g.DrawString(head, Draw.fBig, ink, new RectangleF(x + 20 * S, y + 18 * S, tw, hh + 4));
                    using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(sub, Draw.fSmall, sb, new RectangleF(x + 20 * S, y + 22 * S + hh, tw, sh + 4));
                    if (res.Count > 0)
                    {
                        float nx = x + w - 20 * S;
                        if (!secBusy)
                        {
                            string ct = secCopied > DateTime.Now.AddSeconds(-3) ? L.T("Copied") : L.T("Copy as text"); var cs = g.MeasureString(ct, Draw.fSmall);
                            var cr = new RectangleF(nx - cs.Width, y + (card.Height - cs.Height) / 2, cs.Width, cs.Height); g.DrawString(ct, Draw.fSmall, link, cr.X, cr.Y);
                            Act(secCanvas, cr, delegate { try { Clipboard.SetText(SecurityText()); secCopied = DateTime.Now; } catch (Exception ce) { CopyFailed(ce); } secCanvas.Invalidate(); });
                            nx = cr.X - 24 * S;
                        }
                        foreach (var it in new[] { Tuple.Create(n[3], L.T("couldn't check"), Theme.Sub), Tuple.Create(n[0], L.T("fine"), Theme.LvText(0)), Tuple.Create(toFix, L.T("to fix"), toFix > 0 ? Theme.LvText(2) : Theme.Sub) })
                        {
                            string v = it.Item1.ToString(CultureInfo.CurrentCulture); var vs = g.MeasureString(v, Draw.fCardNum); var ls = g.MeasureString(it.Item2, Draw.fSmall); float cw0 = Math.Max(vs.Width, ls.Width);
                            using (var vb = new SolidBrush(it.Item3)) g.DrawString(v, Draw.fCardNum, vb, nx - cw0 + (cw0 - vs.Width) / 2, y + 12 * S);
                            using (var lb = new SolidBrush(Theme.Sub)) g.DrawString(it.Item2, Draw.fSmall, lb, nx - cw0 + (cw0 - ls.Width) / 2, y + 12 * S + vs.Height - 4 * S);
                            nx -= cw0 + 20 * S;
                        }
                    }
                    y += card.Height + 14 * S;
                }
                // the things to fix first: every risky or worth-a-look row, with the button that opens its Windows setting
                var fix = new List<Tuple<string, string[]>>();
                foreach (var c in Security.All) { List<string[]> rr; if (!res.TryGetValue(c.Id, out rr)) continue; foreach (var o in rr.Where(o => o[0] == "2").Concat(rr.Where(o => o[0] == "1"))) fix.Add(Tuple.Create(c.Id, o)); }
                if (fix.Count > 0)
                {
                    float rowH = 62 * S; var fc = new RectangleF(x, y, w, fix.Count * rowH); Kit.Box(g, fc, 14 * S); float ry = y;
                    using (var lp = new Pen(Theme.Line2))
                        for (int i = 0; i < fix.Count; i++)
                        {
                            var o = fix[i].Item2; string id = fix[i].Item1; int lv = int.Parse(o[0], CultureInfo.InvariantCulture);
                            string fx = o.Length > 5 && o[5].StartsWith("fix:", StringComparison.Ordinal) ? o[5].Substring(4) : "";
                            float bw = 0;
                            if (fx != "")
                            {
                                string bt = fx.StartsWith("url:", StringComparison.Ordinal) ? L.T("Open the router") : L.T("Open setting"); var bs = g.MeasureString(bt, Draw.fBtn);
                                var br = new RectangleF(x + w - 16 * S - bs.Width - 28 * S, ry + (rowH - 36 * S) / 2, bs.Width + 28 * S, 36 * S); bw = br.Width + 16 * S;
                                Theme.Fill(g, br, 10 * S, Theme.Card); Theme.Stroke(g, br, 10 * S, Theme.Line, 1);
                                using (var bb = new SolidBrush(Theme.BlueText)) g.DrawString(bt, Draw.fBtn, bb, br.X + 14 * S, br.Y + (36 * S - bs.Height) / 2);
                                string f2 = fx; Act(secCanvas, br, delegate { SecFix(f2); });
                            }
                            Draw.LvDot(g, lv, x + 18 * S, ry + 16 * S, 8 * S, 8 * S);
                            using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                            {
                                float tx = x + 38 * S, tw2 = w - 38 * S - 20 * S - bw;
                                g.DrawString(o[1] + (o[2] != "" ? " - " + o[2] : ""), Draw.fBodyB, ink, new RectangleF(tx, ry + 11 * S, tw2, 20 * S), nf);
                                g.DrawString(o[3] != "" ? o[3] : o[2], Draw.fSmall, muted, new RectangleF(tx, ry + 33 * S, tw2, 18 * S), nf);
                            }
                            Act(secCanvas, new RectangleF(x, ry, w - bw, rowH), delegate { SecRun(id); });
                            ry += rowH; if (i + 1 < fix.Count) g.DrawLine(lp, x + 16 * S, ry, x + w - 16 * S, ry);
                        }
                    y = fc.Bottom + 14 * S;
                }
                // the groups: one line each, the checks as chips with their dot; open a group to see every chip
                {
                    var rowsH = new List<float>();
                    // measure first, so the card can be drawn under them
                    Func<string, bool, float> groupH = (gid, open) => { int cnt = Security.All.Count(c => c.Group == gid); int shown = open ? cnt : Math.Min(cnt, 6); float rowsOf = 1 + (open ? (float)Math.Ceiling(cnt / Math.Max(1f, (float)Math.Floor((w - 80 * S) / (170 * S)))) - 1 : 0); return 58 * S + rowsOf * 32 * S + 12 * S; };
                    float total = SecGroups.Sum(gr => groupH(gr[0], secOpen.Contains(gr[0])));
                    Kit.Box(g, new RectangleF(x, y, w, total), 14 * S);
                    using (var lp = new Pen(Theme.Line2))
                        for (int gi = 0; gi < SecGroups.Length; gi++)
                        {
                            var grp = SecGroups[gi]; bool open = secOpen.Contains(grp[0]); float gh = groupH(grp[0], open);
                            var checks = Security.All.Where(c => c.Group == grp[0]).ToList();
                            int fine = checks.Count(c => { List<string[]> r0; return res.TryGetValue(c.Id, out r0) && SecLevel(r0) == 0; }), cant = checks.Count(c => { List<string[]> r0; return res.TryGetValue(c.Id, out r0) && SecLevel(r0) == 3; });
                            Glyphs.Tile(g, grp[0] == "pc" ? "pc" : grp[0] == "net" ? "cpe" : "lock", new RectangleF(x + 16 * S, y + 14 * S, 34 * S, 34 * S), Kit.Accent("security"), 9 * S);
                            string gname = Kit.Sent(grp[1]); var gs = g.MeasureString(gname, Draw.fBodyB); g.DrawString(gname, Draw.fBodyB, ink, x + 62 * S, y + 21 * S);
                            string gsub = checks.Count + " " + L.T("checks") + (res.Count > 0 ? " · " + fine + " " + L.T("fine") + (cant > 0 ? ", " + cant + " " + L.T("couldn't check") : "") : "");
                            g.DrawString(gsub, Draw.fSmall, muted, x + 62 * S + gs.Width + 6 * S, y + 23 * S);
                            // the chevron: open or close the group
                            using (var cp = new Pen(Theme.Sub, 1.6f * S) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                            { float ax = x + w - 28 * S, ay = y + 31 * S, d = open ? -1 : 1; g.DrawLines(cp, new[] { new PointF(ax - 5 * S, ay - 2.5f * S * d), new PointF(ax, ay + 2.5f * S * d), new PointF(ax + 5 * S, ay - 2.5f * S * d) }); }
                            string gid = grp[0]; Act(secCanvas, new RectangleF(x, y, w, 58 * S), delegate { if (!secOpen.Remove(gid)) secOpen.Add(gid); secCanvas.Invalidate(); });
                            float cx0 = x + 62 * S, cy = y + 56 * S, cmax = x + w - 20 * S; int shownN = 0;
                            foreach (var c in checks)
                            {
                                if (!open && shownN >= 5 && checks.Count > 6) break;
                                List<string[]> r0; bool done = res.TryGetValue(c.Id, out r0); int lv = done ? SecLevel(r0) : -1;
                                string t = c.Title; float tw3 = g.MeasureString(t, Draw.fSmall).Width + 30 * S;
                                if (cx0 + tw3 > cmax && cx0 > x + 62 * S) { if (!open) break; cx0 = x + 62 * S; cy += 32 * S; }
                                var chr = new RectangleF(cx0, cy, tw3, 26 * S); Theme.Fill(g, chr, 7 * S, Theme.Soft);
                                if (lv >= 0) Draw.LvDot(g, lv, chr.X + 10 * S, chr.Y + 9 * S, 8 * S, 8 * S); else using (var ob = new Pen(Theme.Line, 1.4f * S)) g.DrawEllipse(ob, chr.X + 10 * S, chr.Y + 9 * S, 8 * S, 8 * S);
                                using (var cb = new SolidBrush(Theme.Sub)) g.DrawString(t, Draw.fSmall, cb, chr.X + 22 * S, chr.Y + (26 * S - Draw.fSmall.GetHeight(g)) / 2);
                                string cid = c.Id; Act(secCanvas, chr, delegate { SecRun(cid); }); Hint(secCanvas, chr, "sec." + cid);
                                cx0 = chr.Right + 6 * S; shownN++;
                            }
                            if (!open && shownN < checks.Count)
                            {
                                string more = "+" + (checks.Count - shownN) + " " + L.T("more"); float mw = g.MeasureString(more, Draw.fSmall).Width + 20 * S;
                                if (cx0 + mw > cmax) { cx0 = cmax - mw; }
                                var mr = new RectangleF(cx0, cy, mw, 26 * S); Theme.Fill(g, mr, 7 * S, Theme.Soft); using (var mb = new SolidBrush(Theme.BlueText)) g.DrawString(more, Draw.fSmall, mb, mr.X + 10 * S, mr.Y + (26 * S - Draw.fSmall.GetHeight(g)) / 2);
                                Act(secCanvas, mr, delegate { secOpen.Add(gid); secCanvas.Invalidate(); });
                            }
                            y += gh; if (gi + 1 < SecGroups.Length) g.DrawLine(lp, x, y, x + w, y);
                        }
                    y += 16 * S;
                }
                string foot = L.T("Not done on purpose: trying default passwords, looking you up in breach databases (that would send your details to a website), scanning other people's devices, or changing anything for you.");
                var fh = Draw.Measure(foot, Draw.fSmall, (int)w).Height; g.DrawString(foot, Draw.fSmall, muted, new RectangleF(x, y, w, fh + 4)); y += fh + 10 * S;
            }
            Need(secCanvas, y);
        }
        // Copy as text for the scorecard (Ctrl+C on the page)
        public string SecurityText()
        {
            var sb = new System.Text.StringBuilder(); sb.AppendLine("AdeeboMeter " + Program.Version + " security checkup · " + secAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            string[] lab = { "good", "worth a look", "risky", "can't tell" };
            lock (secRes) foreach (var c in Security.All) { List<string[]> r; if (!secRes.TryGetValue(c.Id, out r)) continue; foreach (var o in r) sb.AppendLine("[" + lab[int.Parse(o[0], CultureInfo.InvariantCulture)] + "] " + o[1] + ": " + o[2] + (o[3] != "" ? "  - " + o[3] : "")); }
            return sb.ToString();
        }
    }
}
