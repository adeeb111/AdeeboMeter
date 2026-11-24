// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== hiding a problem on Home (Adeeb, 23 Nov: "some PCs are
    // always problematic"). Hiding only quiets Home's card: the status strip and the corner widget always tell the truth.
    //   for today            - back tomorrow, or sooner if it gets worse
    //   while it stays like this - back when it gets about 5 points worse, the level rises, or something new breaks
    //   never for this kind  - Home never leads with this kind again
    // While a problem is hidden, Home's card carries one "Show again" button - the way back for every mode (review 24 Nov).
    sealed partial class FullView
    {
        // the problem Home leads with, as a kind and the number that measures it (NaN when there's no single number)
        static string ProblemKind(Snap s, out double val)
        {
            val = double.NaN;
            if (s == null || s.Level < 1 || s.Level > 2) return null;
            bool netOk = true; for (int i = 1; i < s.Path.Length; i++) if (s.Path[i] == 1 || s.Path[i] == 2) netOk = false;
            if (s.Path.Length > 0 && (s.Path[0] == 1 || s.Path[0] == 2) && netOk)
            {
                bool mem = !double.IsNaN(s.Mem) && s.Mem >= 85 && (double.IsNaN(s.Cpu) || s.Mem - 85 >= s.Cpu - 85);
                val = mem ? s.Mem : s.Cpu; return mem ? "pc.mem" : "pc.cpu";
            }
            for (int i = 1; i < s.Path.Length; i++) if (s.Path[i] == 1 || s.Path[i] == 2) return "net." + i.ToString(CultureInfo.InvariantCulture);
            return "other";
        }
        static string KindName(string kind)
        {
            switch (kind)
            {
                case "pc.mem": return L.T("memory warnings"); case "pc.cpu": return L.T("processor warnings");
                case "net.1": return L.T("router and Wi-Fi warnings"); case "net.2": return L.T("VPN and provider warnings");
                case "net.3": return L.T("DNS warnings"); case "net.4": return L.T("internet warnings");
                default: return L.T("this kind of warning");
            }
        }
        // is Home's lead problem hidden right now?
        static bool ProblemHidden(Snap s)
        {
            double v; string k = ProblemKind(s, out v); if (k == null) return false;
            if (("," + (Cfg.HideNever ?? "") + ",").Contains("," + k + ",")) return true;
            var p = (Cfg.HideProblem ?? "").Split('|'); if (p.Length < 5 || p[0] != k) return false;
            int lv; double was; int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv);
            if (s.Level > lv) return false;
            if (double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out was) && !double.IsNaN(v) && v > was + 5) return false;
            if (p[1] == "day" && p[4] != DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture)) return false;
            return true;
        }
        bool pcHidden { get { double v; var k = ProblemKind(snap, out v); return k != null && k.StartsWith("pc.", StringComparison.Ordinal) && ProblemHidden(snap); } }
        void HideProblemFor(string mode)
        {
            double v; string k = ProblemKind(snap, out v); if (k == null) return;
            if (mode == "never") Cfg.HideNever = string.Join(",", (Cfg.HideNever ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Concat(new[] { k }).Distinct());
            else Cfg.HideProblem = k + "|" + mode + "|" + (double.IsNaN(v) ? "" : v.ToString("0.#", CultureInfo.InvariantCulture)) + "|" + snap.Level.ToString(CultureInfo.InvariantCulture) + "|" + DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            Cfg.Save(); if (overviewCanvas != null) overviewCanvas.Invalidate();
        }
        void UnhideProblem()
        {
            double v; string k = ProblemKind(snap, out v);
            Cfg.HideProblem = ""; if (k != null) Cfg.HideNever = string.Join(",", (Cfg.HideNever ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Where(z => z != k));
            Cfg.Save(); if (overviewCanvas != null) overviewCanvas.Invalidate();
        }
        // the buttons on Home's card when it leads with a problem: Hide ▾ · See what's running (a PC problem) · Investigate.
        // measure = true only works out the width; returns the width used, right-aligned at "right"
        float PaintVerdictActs(Graphics g, float right, float y, bool measure)
        {
            double v; string k = ProblemKind(snap, out v); bool pc = k != null && k.StartsWith("pc.", StringComparison.Ordinal);
            var items = new System.Collections.Generic.List<Tuple<string, int>>();
            if (ProblemHidden(snap)) items.Add(Tuple.Create(L.T("Show again"), 3));   // the way back from every Hide, "never" included
            else
            {
                items.Add(Tuple.Create(L.T("Hide") + "  ▾", 0));
                if (pc) items.Add(Tuple.Create(L.T("See what's running"), 1));
                if (snap.Flow != "" || pc) items.Add(Tuple.Create(L.T("Investigate"), 2));
            }
            float x = right, h = 34 * Draw.S, total = 0;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i]; float tw = g.MeasureString(it.Item1, Draw.fBtn).Width + 26 * Draw.S; x -= tw; total += tw + (i > 0 ? 8 * Draw.S : 0);
                if (!measure)
                {
                    var r = new RectangleF(x, y, tw, h); bool prim = it.Item2 == 2;
                    if (prim) Theme.Fill(g, r, 10 * Draw.S, Theme.Blue); else { Theme.Fill(g, r, 10 * Draw.S, Theme.Card); Theme.Stroke(g, r, 10 * Draw.S, Theme.Line, 1); }
                    using (var b = new SolidBrush(prim ? Theme.OnBlue : it.Item2 == 1 ? Theme.BlueText : Theme.Sub)) g.DrawString(it.Item1, Draw.fBtn, b, r.X + 13 * Draw.S, r.Y + (h - Draw.fBtn.GetHeight(g)) / 2);
                    int kind = it.Item2; var rr = r;
                    Act(overviewCanvas, r, delegate
                    {
                        if (kind == 0) ShowHideMenu(overviewCanvas, new Point((int)rr.X, (int)(rr.Bottom + 4 * Draw.S) + overviewCanvas.AutoScrollPosition.Y));
                        else if (kind == 1) { tab = "procs"; ShowTab(); }
                        else if (kind == 3) UnhideProblem();
                        else { string fl = snap.Flow; tab = "guide"; ShowTab(); if (fl != "") StartGuide(fl, false); }
                    });
                }
                x -= 8 * Draw.S;
            }
            return total;
        }
        void ShowHideMenu(Control on, Point at)
        {
            double v; string k = ProblemKind(snap, out v); if (k == null) return;
            var m = Menus.Make();
            Menus.Add(m, L.T("Hide for today"), L.T("Comes back tomorrow, or sooner if it gets worse"), delegate { HideProblemFor("day"); });
            Menus.Add(m, L.T("Hide while it stays like this"), L.T("Comes back if it gets worse or something new breaks"), delegate { HideProblemFor("while"); });
            m.Items.Add(new ToolStripSeparator());
            Menus.Add(m, L.T("Never show") + " " + KindName(k) + " " + L.T("here"), L.T("The widget still shows it. Show again undoes this."), delegate { HideProblemFor("never"); });
            m.Show(on, at);
        }
    }
}
