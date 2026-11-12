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
        static readonly string[][] SecGroups = { new[] { "pc", "THIS PC" }, new[] { "net", "YOUR NETWORK" }, new[] { "sites", "YOUR SITES" } };

        Panel BuildSecurityPage()
        {
            securityPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(0, Sc(64)), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), Sc(8), Sc(6)), WrapContents = true };
            secRun = new PillButton("Run security checkup", PillButton.K.Primary); secRun.Click += delegate { if (secBusy) secStop = true; else SecRun(null); };
            secSite = new RoundBox(Sc(320), Draw.fInput, "Your website or mail domain (optional)") { Margin = new Padding(Sc(10), 0, 0, 0) };
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
            if (secBusy) return; secBusy = true; secStop = false; secRun.Text = "Stop";
            var sites = SecSites(); var list = Security.All.Where(c => only == null || c.Id == only).ToList();
            if (only == null) { lock (secRes) secRes.Clear(); Program.Log("security checkup started"); }
            secCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            {
                foreach (var c in list)
                {
                    if (secStop) break; secNow = c.Title;
                    try { BeginInvoke((Action)delegate { secCanvas.Invalidate(); }); } catch (InvalidOperationException) { }
                    var r = Security.RunOne(c, M, sites); lock (secRes) secRes[c.Id] = r;
                }
                secNow = ""; secAt = DateTime.Now;
                try { BeginInvoke((Action)delegate { secBusy = false; secRun.Text = "Run security checkup"; secCanvas.Invalidate(); }); } catch (InvalidOperationException) { secBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }
        static int SecLevel(List<string[]> r) { var l = r.Select(x => int.Parse(x[0], CultureInfo.InvariantCulture)).ToList(); return l.Contains(2) ? 2 : l.Contains(1) ? 1 : l.Contains(0) ? 0 : 3; }
        static readonly HashSet<string> SecTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UserAccountControlSettings.exe", "SystemPropertiesRemote.exe", "optionalfeatures.exe", "ncpa.cpl", "certmgr.msc", "taskschd.msc", "netplwiz.exe", "taskmgr.exe" };
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
                // the scorecard
                int[] n = new int[4]; foreach (var r in res.Values) n[SecLevel(r)]++;
                g.DrawString(L.T("Security checkup"), Draw.fTitle, ink, x, y); y += 30 * S;
                string sub = secBusy ? "Checking: " + secNow + "...  (" + res.Count + " of " + Security.All.Count + ")" : res.Count == 0 ? Security.All.Count + " checks of this PC, your network and your sites. Mostly reads settings. On your own router it also asks 7 ports (FTP, Telnet, SSH, TR-069, web, the admin page) and DHCP once - skipped on a company network. It never tries a password and never touches anything beyond your router. Each fix opens the Windows screen where you change it yourself." : "Checked at " + secAt.ToString("HH:mm", CultureInfo.CurrentCulture) + " · click a check to run it again";
                if (res.Count > 0 && !secBusy)
                {
                    string ct = secCopied > DateTime.Now.AddSeconds(-3) ? "Copied ✓" : "Copy as text"; var cs = g.MeasureString(ct, Draw.fSmall); var cr = new RectangleF(x + w - cs.Width, y - 26 * S, cs.Width, cs.Height);
                    g.DrawString(ct, Draw.fSmall, link, cr.X, cr.Y); Act(secCanvas, cr, delegate { try { Clipboard.SetText(SecurityText()); secCopied = DateTime.Now; } catch (Exception ce) { Program.Log("copy: " + ce.Message); } secCanvas.Invalidate(); });
                }
                var sh = Draw.Measure(sub, Draw.fSmall, (int)w).Height; g.DrawString(sub, Draw.fSmall, muted, new RectangleF(x, y, w, sh + 4)); y += sh + 12 * S;
                if (res.Count > 0)
                {
                    float bx = x; string[] lab = { "good", "worth a look", "risky", "info / can't tell" };
                    foreach (int lv in new[] { 2, 1, 0, 3 })
                    {
                        string t = n[lv] + " " + lab[lv]; var ts = g.MeasureString(t, Draw.fBodyB); var pr = new RectangleF(bx, y, ts.Width + 30 * S, 32 * S);
                        Theme.Fill(g, pr, 16 * S, Theme.LvSoft(lv)); using (var b = new SolidBrush(Theme.Lv(lv))) g.FillEllipse(b, bx + 10 * S, y + 11 * S, 10 * S, 10 * S);
                        using (var tb = new SolidBrush(Theme.LvText(lv))) g.DrawString(t, Draw.fBodyB, tb, bx + 24 * S, y + (32 * S - ts.Height) / 2);
                        bx += pr.Width + 8 * S;
                    }
                    y += 46 * S;
                }
                foreach (var grp in SecGroups)
                {
                    g.DrawString(grp[1], Draw.fTiny, muted, x, y + 4 * S); y += 24 * S;
                    // problems first once checked; the plan order before
                    var checks = Security.All.Where(c => c.Group == grp[0]).OrderBy(c => { List<string[]> r; if (!res.TryGetValue(c.Id, out r)) return 9; int l = SecLevel(r); return l == 2 ? 0 : l == 1 ? 1 : l == 3 ? 2 : 3; }).ToList();
                    foreach (var c in checks)
                    {
                        List<string[]> rows; bool done = res.TryGetValue(c.Id, out rows);
                        if (!done) rows = new List<string[]> { new[] { "3", c.Title, secBusy && secNow == c.Title ? "checking..." : "not checked yet", "", "", "" } };
                        foreach (var o in rows)
                        {
                            int lv = int.Parse(o[0], CultureInfo.InvariantCulture); string fix = o.Length > 5 && o[5].StartsWith("fix:", StringComparison.Ordinal) ? o[5].Substring(4) : "";
                            float nameW = Math.Min(260 * S, w * 0.32f), tw = w - nameW - 150 * S;
                            float vh = Draw.Measure(o[2], Draw.fBody, (int)tw).Height, ah = o[3] == "" ? 0 : Draw.Measure(o[3], Draw.fSmall, (int)tw).Height + 4 * S, rh = Math.Max(34 * S, vh + ah + 14 * S);
                            if (lv == 2 || lv == 1) using (var hb = new SolidBrush(Theme.LvSoft(lv))) g.FillRectangle(hb, x - 6 * S, y, w + 12 * S, rh);
                            using (var b = new SolidBrush(done ? Theme.Lv(lv) : Theme.Line)) g.FillEllipse(b, x, y + 11 * S, 10 * S, 10 * S);
                            g.DrawString(o[1], Draw.fBodyB, ink, new RectangleF(x + 20 * S, y + 7 * S, nameW - 24 * S, rh));
                            using (var vb = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(o[2], Draw.fBody, vb, new RectangleF(x + nameW, y + 7 * S, tw, vh + 4));
                            if (o[3] != "") g.DrawString(o[3], Draw.fSmall, muted, new RectangleF(x + nameW, y + 9 * S + vh, tw, ah));
                            string id = c.Id;
                            if (fix != "")
                            {
                                string lk = fix.StartsWith("url:", StringComparison.Ordinal) ? "Open the router ›" : "Open the setting ›"; var ls = g.MeasureString(lk, Draw.fSmall); var lr = new RectangleF(x + w - ls.Width, y + 8 * S, ls.Width, ls.Height);
                                g.DrawString(lk, Draw.fSmall, link, lr.X, lr.Y); string f2 = fix; Act(secCanvas, lr, delegate { SecFix(f2); });
                                Act(secCanvas, new RectangleF(x - 6 * S, y, w - ls.Width - 4 * S, rh), delegate { SecRun(id); });
                            }
                            else Act(secCanvas, new RectangleF(x - 6 * S, y, w + 12 * S, rh), delegate { SecRun(id); });
                            y += rh + 2 * S;
                        }
                    }
                    y += 14 * S;
                }
                string foot = "Not done on purpose: trying default passwords, looking you up in breach databases (that would send your details to a website), scanning other people's devices, or changing anything for you.";
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
