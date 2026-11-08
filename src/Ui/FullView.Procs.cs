// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AdeeboMeter
{
    sealed partial class FullView
    {
        // ---------- What's running
        void RefreshProcs()
        {
            lastProcs = DateTime.UtcNow;
            List<AppStat> l; lock (M.L) l = M.Apps;
            procs = l ?? new List<AppStat>();
            procList.Invalidate(); procInfo.Invalidate();
            // where unknown programs live (once per name, off the UI thread)
            var todo = procs.Where(a => Explain.Find(a.Name) == null && Explain.Find(Monitor.RawOf(a.Name)) == null && !procWhere.ContainsKey(a.Name)).Select(a => new KeyValuePair<string, int>(a.Name, a.Pid)).ToList();
            if (todo.Count == 0 || whereBusy) return;
            whereBusy = true;
            Task.Factory.StartNew(() =>
            {
                var res = new Dictionary<string, string>();
                foreach (var t in todo) { string path = ""; try { path = Native.ExePath(t.Value); } catch { } res[t.Key] = path == "" ? "" : Explain.Where(path); }
                try { BeginInvoke((Action)delegate { foreach (var kv in res) procWhere[kv.Key] = kv.Value; whereBusy = false; procList.Invalidate(); }); } catch { whereBusy = false; }
            });
        }
        string CatOf(AppStat a)
        {
            var e = Explain.Find(a.Name) ?? Explain.Find(Monitor.RawOf(a.Name));
            if (e != null) return e.Cat;
            string w; procWhere.TryGetValue(a.Name, out w);
            return w == "windows" ? Explain.Help : w == "programfiles" || w == "user" ? Explain.App : Explain.Other;
        }
        void SelectProc(AppStat a)
        {
            procSel = a.Name; procFacts = null; procPathSel = ""; procInfo.AutoScrollPosition = Point.Empty; procList.Invalidate(); procInfo.Invalidate();
            string name = a.Name; int pid = a.Pid;
            Task.Factory.StartNew(() =>
            {
                string path = ""; try { path = Native.ExePath(pid); } catch { }
                var f = Explain.FileFacts(path);
                try { BeginInvoke((Action)delegate { if (procSel == name) { procFacts = f; procPathSel = path; procInfo.Invalidate(); } }); } catch { }
            });
        }
        void PaintProcList(Graphics g)
        {
            g.TranslateTransform(procList.AutoScrollPosition.X, procList.AutoScrollPosition.Y);
            Begin(procList);
            float x = 14 * S, y = 4 * S, w = procList.ClientSize.Width - 22 * S, rh = 42 * S;
            string q = procSearch.Box.Text.Trim();
            var items = procs.Where(a => a.Name != "Idle").Select(a => new { A = a, E = Explain.Find(a.Name) ?? Explain.Find(Monitor.RawOf(a.Name)), C = CatOf(a) })
                             .Where(i => q == "" || i.A.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || (i.E != null && (i.E.Friendly.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || i.E.Tag.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))).ToList();
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (items.Count == 0) g.DrawString(procs.Count == 0 ? Voice.P("Reading the process list...", "Loading...") : "Nothing matches.", Draw.fBody, muted, x, y + 8 * S);
                foreach (var cat in Explain.CatOrder)
                {
                    var rows = items.Where(i => i.C == cat).OrderByDescending(i => i.A.Mem).ToList();
                    if (rows.Count == 0) continue;
                    y += 8 * S;
                    g.DrawString(cat.ToUpperInvariant() + "  ·  " + rows.Count, Draw.fTiny, muted, x, y); y += 20 * S;
                    foreach (var i in rows)
                    {
                        var a = i.A; var rr = new RectangleF(0, y, w + 8 * S, rh);
                        if (a.Name == procSel) using (var b = new SolidBrush(Theme.RowSel)) g.FillRectangle(b, rr);
                        Theme.Fill(g, new RectangleF(x, y + rh / 2 - 5 * S, 10 * S, 10 * S), 5 * S, i.E == null ? Theme.Grey : i.E.Close == 2 ? Theme.Red : i.E.Close == 3 ? Theme.C("#8250DF") : i.E.Close == 1 ? Theme.Amber : Theme.Green);
                        string title = i.E != null ? i.E.Friendly : a.Name;
                        string subl = (i.E != null ? i.E.Tag : "not in the guide - click to see what the file says") + (a.Count > 1 ? "   ·   " + a.Count + " copies" : "");
                        float tx = x + 20 * S, tw = w - 20 * S - 150 * S;
                        g.DrawString(title + (i.E != null && !string.Equals(i.E.Friendly, a.Name, StringComparison.OrdinalIgnoreCase) ? "  (" + a.Name + ")" : ""), Draw.fBodyB, ink, new RectangleF(tx, y + 4 * S, tw, 18 * S), nowrap);
                        g.DrawString(subl, Draw.fSmall, muted, new RectangleF(tx, y + 22 * S, tw, 16 * S), nowrap);
                        string mem = Draw.MemStr(a.Mem), cpu = a.Cpu.ToString("0.0", CultureInfo.CurrentCulture) + "%";
                        var ms = g.MeasureString(mem, Draw.fMono); g.DrawString(mem, Draw.fMono, ink, x + w - 64 * S - ms.Width, y + 12 * S);
                        var cs = g.MeasureString(cpu, Draw.fMono); g.DrawString(cpu, Draw.fMono, sub, x + w - cs.Width, y + 12 * S);
                        g.DrawLine(line, x, y + rh - 1, x + w, y + rh - 1);
                        var captured = a; Act(procList, rr, delegate { SelectProc(captured); });
                        y += rh;
                    }
                }
            }
            var need = new Size(0, (int)(y + 20 * S)); if (procList.AutoScrollMinSize != need) { procList.AutoScrollMinSize = need; procList.Invalidate(); }
        }
        void PaintProcInfo(Graphics g)
        {
            g.TranslateTransform(procInfo.AutoScrollPosition.X, procInfo.AutoScrollPosition.Y);
            Begin(procInfo);
            float x = 20 * S, y = 14 * S, w = procInfo.ClientSize.Width - 40 * S;
            using (var line = new Pen(Theme.Line)) g.DrawLine(line, 0, 0, 0, Math.Max(procInfo.ClientSize.Height, procInfo.AutoScrollMinSize.Height));
            var a = procs.FirstOrDefault(p => p.Name == procSel);
            if (a == null)
            {
                int total = procs.Sum(p => p.Count), known = procs.Count(p => Explain.Find(p.Name) != null || Explain.Find(Monitor.RawOf(p.Name)) != null);
                var intro = new Explain.Entry
                {
                    Friendly = Voice.P("Meet the crew running your PC.", "What's running on this PC"), Close = -1,
                    Tag = total + " processes, grouped into " + procs.Count + " programs. I know " + known + " of them by heart.",
                    What = "Every program on your PC is one or more 'processes' - a running copy with its own memory. Windows itself is dozens of them, each with one job.",
                    How = "Click any row on the left. I'll tell you what it does, how it works, why it's there, what would happen without it - and whether it's safe to close.",
                    Why = "Once you know who's who, Task Manager stops being scary - and you'll know exactly what to close when the PC is struggling.",
                    Without = "Coloured dots: green = safe to close, amber = leave it (OK to restart), red = Windows needs it, purple = managed by IT, grey = not in my guide."
                };
                y = ExplainPaint.Paint(g, intro, x, y, w, false);
            }
            else
            {
                var e = ProcEntry(a.Name, procFacts, procPathSel);
                string facts = a.Count + (a.Count == 1 ? " copy" : " copies") + "   ·   " + Draw.MemStr(a.Mem) + " memory   ·   " + a.Cpu.ToString("0.0", CultureInfo.CurrentCulture) + "% CPU";
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(facts, Draw.fMonoS, mb, x, y); y += 22 * S;
                y = ExplainPaint.Paint(g, e, x, y, w, false);
                y += 10 * S;
                var rows = new List<string[]>();
                rows.Add(new[] { "Program file", procFacts == null ? "reading..." : procPathSel != "" ? procPathSel : "hidden by Windows (protected process)" });
                if (procFacts != null && procFacts[0] != "") rows.Add(new[] { "Describes itself as", procFacts[0] });
                if (procFacts != null && procFacts[1] != "") rows.Add(new[] { "Publisher", procFacts[1] });
                if (procFacts != null) rows.Add(new[] { "Where it lives", Explain.WhereText(procPathSel == "" ? "" : procFacts[3]) });
                y = ExplainPaint.Section(g, "ON THIS PC", rows, x, y, w);
            }
            var need = new Size(0, (int)(y + 30 * S)); if (procInfo.AutoScrollMinSize != need) { procInfo.AutoScrollMinSize = need; procInfo.Invalidate(); }
        }
    }
}
