// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // "Inside Windows": who started whom, what's inside each svchost, what starts with Windows, what a program is doing right now.
    // Read-only; built when you open the tab, released when you leave.
    sealed partial class FullView
    {
        static readonly string[] TurnOff = { "To turn it off", "Task Manager → Startup apps → right-click it → Disable (or the app's own settings). Startup time per app needs admin to measure, so AdeeboMeter doesn't guess it.", "3" };
        Panel insidePage; DB insideList, insideInfo; FlowLayoutPanel insideModes; RoundBox insideSearch;
        string insideMode = "tree"; volatile bool insideBusy;
        List<KeyValuePair<ProcTree.P, int>> treeRows; Dictionary<int, ProcTree.P> treeBy; List<Svc.S> svcs; List<Startup.Item> startups; Dictionary<string, string> startupByExe;
        int insideSelPid = -1; string insideSelKey = ""; string[] insideNow; DateTime insideNowAt;

        Panel BuildInsidePage()
        {
            insidePage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(64), BackColor = Theme.Card, Padding = new Padding(Sc(14), Sc(14), 0, 0), WrapContents = false };
            insideSearch = new RoundBox(Sc(240), Draw.fBody, "Find a process");
            insideSearch.Box.TextChanged += delegate { insideList.AutoScrollPosition = Point.Empty; insideList.Invalidate(); };
            bar.Controls.Add(insideSearch);
            foreach (var mo in new[] { new[] { "tree", "Who started whom" }, new[] { "boot", "How Windows starts" }, new[] { "services", "Inside svchost" }, new[] { "startup", "Starts with Windows" }, new[] { "dives", "Deep dives" } })
            {
                var b = new PillButton(mo[1], PillButton.K.Chip) { Tag = mo[0], Margin = new Padding(Sc(6), Sc(3), 0, 0) };
                b.Click += delegate(object s, EventArgs e) { insideMode = (string)((Control)s).Tag; insideSelKey = ""; insideSelPid = -1; StyleInsideModes(); LoadInside(); };
                bar.Controls.Add(b);
            }
            insideModes = bar;
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Card };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            insideList = new DB { Name = "insideList", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            insideList.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintInsideList(e.Graphics); }; Wire(insideList);
            insideInfo = new DB { Name = "insideInfo", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            insideInfo.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintInsideInfo(e.Graphics); }; Wire(insideInfo);
            split.Controls.Add(insideList, 0, 0); split.Controls.Add(insideInfo, 1, 0);
            insidePage.Controls.Add(split); insidePage.Controls.Add(bar);
            StyleInsideModes();
            return insidePage;
        }
        void StyleInsideModes() { foreach (Control c in insideModes.Controls) { var b = c as PillButton; if (b != null) b.Selected = (string)b.Tag == insideMode; } }
        void LoadInside()
        {
            if (insideBusy) return; insideBusy = true; insideList.Invalidate();
            Task.Factory.StartNew(() =>
            {
                List<KeyValuePair<ProcTree.P, int>> rows = null; Dictionary<int, ProcTree.P> by = null; List<Svc.S> sv = null; List<Startup.Item> st = null; Dictionary<string, string> sbe = null;
                try
                {
                    var roots = ProcTree.Snapshot(out by); rows = new List<KeyValuePair<ProcTree.P, int>>();
                    Action<ProcTree.P, int> walk = null;
                    walk = delegate(ProcTree.P p, int d) { rows.Add(new KeyValuePair<ProcTree.P, int>(p, d)); foreach (var k in p.Kids.OrderBy(k => k.Name, StringComparer.OrdinalIgnoreCase)) if (d < 30) walk(k, d + 1); };
                    foreach (var r in roots.OrderBy(r => r.Orphan).ThenBy(r => r.Pid)) walk(r, 0);
                    sv = Svc.List(); st = Startup.List(); sbe = Startup.ByExe();
                }
                catch (Exception e) { Program.Log("inside: " + e.Message); }
                try { BeginInvoke((Action)delegate { insideBusy = false; treeRows = rows; treeBy = by; svcs = sv; startups = st; startupByExe = sbe; insideList.Invalidate(); insideInfo.Invalidate(); }); } catch { insideBusy = false; }
            });
        }

        // the left list for each mode: { key, title, sub, depth }
        List<string[]> InsideItems()
        {
            var r = new List<string[]>(); string q = insideSearch.Box.Text.Trim();
            Func<string, bool> hit = s => q == "" || s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            if (insideMode == "tree" && treeRows != null)
                foreach (var kv in treeRows) { var p = kv.Key; if (hit(p.Name)) r.Add(new[] { "pid:" + p.Pid, p.Name, "PID " + p.Pid + (p.Kids.Count > 0 ? " · started " + p.Kids.Count : ""), q == "" ? kv.Value.ToString(CultureInfo.InvariantCulture) : "0" }); }
            else if (insideMode == "boot")
                foreach (var n in BootOrder) r.Add(new[] { "boot:" + n, n + ".exe", BootWhen(n), "0" });
            else if (insideMode == "services" && svcs != null)
                foreach (var gr in svcs.Where(s => s.Pid > 0).GroupBy(s => s.Pid).OrderByDescending(g => g.Count()))
                {
                    ProcTree.P hp; string host = treeBy != null && treeBy.TryGetValue(gr.Key, out hp) ? hp.Name : "?";
                    if (hit(host) || gr.Any(s => hit(s.Name) || hit(s.Display))) r.Add(new[] { "svc:" + gr.Key, host + "  ·  PID " + gr.Key, string.Join(", ", gr.Select(s => s.Name).Take(4)) + (gr.Count() > 4 ? " +" + (gr.Count() - 4) : ""), "0" });
                }
            else if (insideMode == "startup" && startups != null)
                foreach (var s in startups) { if (hit(s.Name) || hit(s.Exe)) r.Add(new[] { "start:" + s.Name, s.Name, s.Where, "0" }); }
            else if (insideMode == "dives")
                foreach (var t in Knowledge.Learn().Where(t => t["kind"] == "windows")) if (hit(t["title"])) r.Add(new[] { "dive:" + t.Id, t["title"], t["tag"], "0" });
            return r;
        }
        static readonly string[] BootOrder = { "System", "smss", "csrss", "wininit", "services", "lsass", "svchost", "winlogon", "LogonUI", "fontdrvhost", "dwm", "userinit", "explorer", "sihost", "ctfmon" };
        string BootWhen(string n)
        {
            if (treeRows == null) return "";
            var p = treeRows.Select(kv => kv.Key).Where(x => string.Equals(Path.GetFileNameWithoutExtension(x.Name), n, StringComparison.OrdinalIgnoreCase) || (n == "System" && x.Pid == 4)).ToList();
            if (p.Count == 0) return n == "userinit" ? "already finished - it starts Explorer, then exits" : "not running";
            string when = "";
            try { using (var pr = Process.GetProcessById(p[0].Pid)) when = " · since " + pr.StartTime.ToString("ddd HH:mm", CultureInfo.CurrentCulture); } catch { when = " · start time hidden by Windows"; }
            return (p.Count > 1 ? p.Count + " running" : "PID " + p[0].Pid) + when;
        }

        void PaintInsideList(Graphics g)
        {
            g.TranslateTransform(insideList.AutoScrollPosition.X, insideList.AutoScrollPosition.Y);
            Begin(insideList);
            float x = 14 * S, y = 8 * S, w = insideList.ClientSize.Width - 22 * S;
            if (treeRows == null) { if (!insideBusy) LoadInside(); using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("Looking inside Windows..."), Draw.fBody, mb, x, y); return; }
            var items = InsideItems(); float rh = 40 * S;
            int first = (int)Math.Max(0, (-insideList.AutoScrollPosition.Y - y) / rh), last = first + (int)(insideList.ClientSize.Height / rh) + 2;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var line = new Pen(Theme.Line2))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                for (int i = 0; i < items.Count; i++)
                {
                    if (i < first || i > last) { y += rh; continue; }   // draw only what's on screen
                    var it = items[i]; int depth = int.Parse(it[3], CultureInfo.InvariantCulture); float ix = x + Math.Min(depth, 12) * 16 * S;
                    var rr = new RectangleF(0, y, insideList.ClientSize.Width, rh);
                    if (it[0] == insideSelKey) using (var b = new SolidBrush(Theme.RowSel)) g.FillRectangle(b, rr);
                    if (depth > 0) using (var tp = new Pen(Theme.Line, 1)) { g.DrawLine(tp, ix - 10 * S, y, ix - 10 * S, y + rh / 2); g.DrawLine(tp, ix - 10 * S, y + rh / 2, ix - 3 * S, y + rh / 2); }
                    g.DrawString(it[1], Draw.fBodyB, ink, new RectangleF(ix, y + 3 * S, w - (ix - x), 18 * S), nowrap);
                    g.DrawString(it[2], Draw.fSmall, muted, new RectangleF(ix, y + 21 * S, w - (ix - x), 16 * S), nowrap);
                    string key = it[0]; Act(insideList, rr, delegate { SelectInside(key); });
                    y += rh; g.DrawLine(line, x, y, x + w, y);
                }
            var need = new Size(0, (int)(y + 20 * S)); if (insideList.AutoScrollMinSize != need) { insideList.AutoScrollMinSize = need; insideList.Invalidate(); }
        }
        void SelectInside(string key)
        {
            insideSelKey = key; insideNow = null; insideInfo.AutoScrollPosition = Point.Empty;
            insideSelPid = key.StartsWith("pid:", StringComparison.Ordinal) || key.StartsWith("svc:", StringComparison.Ordinal) ? int.Parse(key.Substring(4), CultureInfo.InvariantCulture) : -1;
            if (key.StartsWith("boot:", StringComparison.Ordinal) && treeRows != null)
            {
                string n = key.Substring(5); var p = treeRows.Select(kv => kv.Key).FirstOrDefault(x => string.Equals(Path.GetFileNameWithoutExtension(x.Name), n, StringComparison.OrdinalIgnoreCase) || (n == "System" && x.Pid == 4));
                insideSelPid = p != null ? p.Pid : -1;
            }
            insideList.Invalidate(); insideInfo.Invalidate(); MeasureNow();
        }
        // what it's doing right now: CPU over one second, memory, disk, connections, window, services
        void MeasureNow()
        {
            int pid = insideSelPid; if (pid <= 0) return;
            Task.Factory.StartNew(() =>
            {
                var r = new List<string>();
                try
                {
                    using (var p = Process.GetProcessById(pid))
                    {
                        TimeSpan t0 = TimeSpan.Zero; bool cpuOk = true; try { t0 = p.TotalProcessorTime; } catch { cpuOk = false; }
                        var io0 = ProcIo.Of(pid); System.Threading.Thread.Sleep(1000); p.Refresh();
                        string cpu = "hidden by Windows"; if (cpuOk) try { cpu = ((p.TotalProcessorTime - t0).TotalSeconds * 100 / Environment.ProcessorCount).ToString("0.0", CultureInfo.CurrentCulture) + "%"; } catch { }
                        string mem = "hidden"; try { mem = (p.WorkingSet64 / 1048576).ToString(CultureInfo.CurrentCulture) + " MB"; } catch { }
                        var io1 = ProcIo.Of(pid);
                        string disk = io0 != null && io1 != null ? "reading " + ((io1[0] - io0[0]) / 1024.0).ToString("0", CultureInfo.CurrentCulture) + " KB/s · writing " + ((io1[1] - io0[1]) / 1024.0).ToString("0", CultureInfo.CurrentCulture) + " KB/s · " + ((io1[0] + io1[1]) / 1048576.0).ToString("0", CultureInfo.CurrentCulture) + " MB since it started" : "hidden by Windows";
                        string win = ""; try { win = p.MainWindowTitle; } catch { }
                        Dictionary<int, string> names; lock (M.L) names = M.PidNames;
                        int conns = 0; try { conns = Tables.Tcp(false, names).Count(c => c.Pid == pid); } catch { }
                        string threads = ""; try { threads = p.Threads.Count.ToString(CultureInfo.CurrentCulture); } catch { }
                        r.AddRange(new[] { cpu, mem, disk, conns.ToString(CultureInfo.CurrentCulture), win, threads });
                    }
                }
                catch { r.Clear(); r.Add("gone"); }
                try { BeginInvoke((Action)delegate { if (insideSelPid == pid) { insideNow = r.ToArray(); insideNowAt = DateTime.Now; insideInfo.Invalidate(); } }); } catch { }
            });
        }

        void PaintInsideInfo(Graphics g)
        {
            g.TranslateTransform(insideInfo.AutoScrollPosition.X, insideInfo.AutoScrollPosition.Y);
            Begin(insideInfo);
            float x = 18 * S, y = 12 * S, w = insideInfo.ClientSize.Width - 36 * S;
            string key = insideSelKey;
            if (key == "")
            {
                string intro = insideMode == "tree" ? "Every process on this PC, under the one that started it. Windows starts with the kernel (System), which starts the session manager, which starts the rest - down to Explorer and the apps you open. Click any of them." :
                               insideMode == "boot" ? "How Windows comes to life, in order - from the kernel to your desktop. Click a step for what it does and what happens if it dies. The PIDs and times are from this PC." :
                               insideMode == "services" ? "svchost.exe is a host: one program that runs many Windows services, so they share memory. Each line is one svchost and the services living in it. Click one to see what each service is for." :
                               insideMode == "startup" ? "Everything set to start when you sign in, and who put it there. AdeeboMeter only explains - to turn one off, use Task Manager → Startup apps." :
                               "The stories behind the processes people ask about most.";
                y = Lead2(g, intro, x, y, w); return;
            }
            if (key.StartsWith("dive:", StringComparison.Ordinal)) { y = PaintTopic(g, insideInfo, Knowledge.Topic(key.Substring(5)), x, y, w); Need(insideInfo, y); return; }
            if (key.StartsWith("start:", StringComparison.Ordinal))
            {
                var s = startups == null ? null : startups.FirstOrDefault(t => t.Name == key.Substring(6));
                if (s != null)
                {
                    y = Title2(g, s.Name, x, y, w);
                    var e = Explain.Find(Path.GetFileNameWithoutExtension(s.Exe));
                    y = InfoCardOn(insideInfo, g, "WHY IT STARTS", new List<string[]> { new[] { "Added in", s.Where, "3" }, new[] { "Runs", s.Command, "3" }, TurnOff }, null, x, y, w);
                    if (e != null) y = ExplainPaint.Paint(g, e, x, y, w, false);
                }
                Need(insideInfo, y); return;
            }
            // a process (tree, boot step or svchost)
            ProcTree.P p = null; if (treeBy != null && insideSelPid > 0) treeBy.TryGetValue(insideSelPid, out p);
            string exe = p != null ? p.Name : key.StartsWith("boot:", StringComparison.Ordinal) ? key.Substring(5) + ".exe" : "";
            string raw = Path.GetFileNameWithoutExtension(exe);
            y = Title2(g, exe + (p != null ? "   ·   PID " + p.Pid : ""), x, y, w);
            if (p != null)
            {
                var chain = new List<string>(); var cur = p; int guard = 0;
                while (cur != null && guard++ < 12) { chain.Insert(0, cur.Name); ProcTree.P up; cur = treeBy.TryGetValue(cur.Parent, out up) && up.Pid != cur.Pid ? up : null; }
                var rows = new List<string[]> { new[] { "Family line", string.Join("  →  ", chain), "3" }, new[] { "Why it started", ProcTree.WhyStarted(p, treeBy, startupByExe), "3" } };
                string path = Native.ExePath(p.Pid); rows.Add(new[] { "Runs from", path != "" ? path : "hidden - Windows keeps system processes to itself for normal users", "3" });
                if (p.Kids.Count > 0) rows.Add(new[] { "It started", string.Join(", ", p.Kids.Select(k => k.Name).Distinct().Take(8)) + (p.Kids.Count > 8 ? " ..." : ""), "3" });
                var hosted = svcs == null ? new List<Svc.S>() : svcs.Where(s => s.Pid == p.Pid).ToList();
                if (hosted.Count > 0) rows.Add(new[] { "Services inside", hosted.Count + " - " + string.Join(", ", hosted.Select(s => s.Name).Take(12)), "3" });
                y = InfoCardOn(insideInfo, g, "WHERE IT COMES FROM", rows, null, x, y, w);
                var now = insideNow;
                if (now != null && now.Length >= 6)
                    y = InfoCardOn(insideInfo, g, "WHAT IT'S DOING RIGHT NOW  ·  MEASURED " + insideNowAt.ToString("HH:mm:ss", CultureInfo.CurrentCulture), new List<string[]> {
                        new[] { "CPU", now[0], "3" }, new[] { "Memory", now[1], "3" }, new[] { "Disk", now[2], "3" }, new[] { "Network connections", now[3], "3" }, new[] { "Threads", now[5], "3" }, new[] { "Window", now[4] == "" ? "none (works in the background)" : now[4], "3" } }, null, x, y, w);
                else if (now != null) y = Lead2(g, "It has exited.", x, y, w);
                else y = Lead2(g, "Measuring for one second...", x, y, w);
                var again = new RectangleF(x, y - 4 * S, 140 * S, 20 * S);
                using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(L.T("Measure again  ↻"), Draw.fBodyB, lb, again.X, again.Y);
                Act(insideInfo, again, MeasureNow); y += 26 * S;
                if (hosted.Count > 0)
                    y = InfoCardOn(insideInfo, g, "THE SERVICES LIVING IN THIS " + exe.ToUpperInvariant(), hosted.Select(s => new[] { s.Name, (Svc.What(s.Name) != "" ? Svc.What(s.Name) : s.Display), "3" }).ToList(), null, x, y, w);
            }
            var entry = Explain.Find(raw) ?? Explain.Find(exe);
            if (entry != null) y = ExplainPaint.Paint(g, entry, x, y, w, false);
            var dive = Knowledge.Topic(raw.ToLowerInvariant());
            if (dive != null) y = PaintTopic(g, insideInfo, dive, x, y + 8 * S, w);
            Need(insideInfo, y);
        }
        static float Lead2(Graphics g, string t, float x, float y, float w) { var h = Draw.Measure(t, Draw.fBody, (int)w).Height; using (var b = new SolidBrush(Theme.Sub)) g.DrawString(t, Draw.fBody, b, new RectangleF(x, y, w, h + 4)); return y + h + 12 * S; }
        static float Title2(Graphics g, string t, float x, float y, float w) { using (var b = new SolidBrush(Theme.Ink)) g.DrawString(t, Draw.fTitle, b, new RectangleF(x, y, w, 30 * S)); return y + 36 * S; }
        // when the content height changes, Windows may move the scroll position in the middle of a paint - the half that was
        // already drawn is then at the old position (rows appear twice). Repaint the whole thing once more when that happens.
        static void Need(ScrollableControl c, float y) { var need = new Size(0, (int)(y + 20 * S)); if (c.AutoScrollMinSize != need) { c.AutoScrollMinSize = need; c.Invalidate(); } }

        // a knowledge topic: what / how / why / look for, its ladder, and (protocols) the live button
        // the second line is skipped when it mostly repeats the first
        static readonly char[] WordSep = { ' ', ',', '.', ':', ';', '-' };
        static bool SaysSame(string a, string b)
        {
            Func<string, HashSet<string>> words = s => new HashSet<string>(s.ToLowerInvariant().Split(WordSep, StringSplitOptions.RemoveEmptyEntries).Where(z => z.Length > 3));
            var wa = words(a); var wb = words(b); if (wa.Count == 0 || wb.Count == 0) return false;
            return wa.Count(z => wb.Contains(z)) * 2 >= Math.Min(wa.Count, wb.Count);
        }
        static float PaintTopic(Graphics g, Control host, Knowledge.Rec t, float x, float y, float w)
        {
            if (t == null) return y;
            y = Title2(g, t["title"], x, y, w);
            if (t["tag"] != "") using (var b = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) { var h = Draw.Measure(t["tag"], Draw.fHead, (int)w).Height; g.DrawString(t["tag"], Draw.fHead, b, new RectangleF(x, y, w, h + 4)); y += h + 10 * S; }
            // two lines and a picture - people who want the textbook will read it elsewhere
            if (t["line"] != "" && !SaysSame(t["tag"], t["line"])) y = Lead2(g, t["line"], x, y, w);
            if (t["ladder"] != "") { y += 6 * S; y = DrawKnowledgeLadder(g, host, t["ladder"], x, y, w); }
            if (t["breaks"] != "")
            {
                y += 8 * S; string bt = "If it goes wrong:  " + t["breaks"]; float bh = Draw.Measure(bt, Draw.fBody, (int)(w - 28 * S)).Height;
                var br = new RectangleF(x, y, w, bh + 20 * S); Theme.Fill(g, br, 10 * S, Theme.LvSoft(1));
                using (var bb = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.LvText(1))) g.DrawString(bt, Draw.fBody, bb, new RectangleF(x + 14 * S, y + 10 * S, w - 28 * S, bh + 4));
                y += br.Height + 10 * S;
            }
            return y;
        }
    }
}
