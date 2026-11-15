// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Home: your week, across everything (Weekly.cs)
    sealed partial class FullView
    {
        DateTime weekCopied = DateTime.MinValue;
        static readonly string[][] WeekLook = { new[] { "net", "home", "home" }, new[] { "stream", "stream", "app" }, new[] { "trade", "trade", "traffic" }, new[] { "game", "gamer", "game" } };
        float PaintWeek(Graphics g, float x, float y, float w)
        {
            var r = Weekly.Last7(M, () => { try { if (!IsDisposed) BeginInvoke((Action)delegate { if (overviewCanvas != null) overviewCanvas.Invalidate(); }); } catch (InvalidOperationException) { } });
            if (r == null || r.Parts.Count == 0) return y;   // nothing recorded yet: no empty card
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("YOUR WEEK · LAST 7 DAYS · EVERYTHING YOU DO ONLINE"), Draw.fTiny, muted, x, y); y += 22 * S;
                float rh = 30 * S, hh = Draw.Measure(L.T(r.Head), Draw.fName, (int)(w - 40 * S)).Height;
                var card = new RectangleF(x, y, w, 18 * S + hh + 10 * S + r.Parts.Count * rh + 56 * S); Kit.Box(g, card);
                float cy = y + 16 * S;
                using (var hb = new SolidBrush(r.Lv == 0 ? Theme.Ink : Theme.LvText(Math.Min(2, r.Lv)))) g.DrawString(L.T(r.Head), Draw.fName, hb, new RectangleF(x + 20 * S, cy, w - 40 * S, hh + 4)); cy += hh + 10 * S;
                using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    foreach (var p in r.Parts)
                    {
                        var look = WeekLook.FirstOrDefault(z => z[0] == p.Id) ?? WeekLook[0];
                        Kit.Icon(g, look[2], new RectangleF(x + 20 * S, cy + 4 * S, 16 * S, 16 * S), Kit.Accent(look[1]));
                        g.DrawString(L.T(p.Title), Draw.fBodyB, ink, x + 44 * S, cy + 2 * S);
                        Draw.LvDot(g, p.Lv, x + 150 * S, cy + 8 * S, 9 * S, 9 * S);
                        g.DrawString(p.Line, Draw.fBody, sub, new RectangleF(x + 166 * S, cy + 2 * S, w - 186 * S, rh), nf);
                        string id = p.Id; if (id != "net") Act(overviewCanvas, new RectangleF(x + 16 * S, cy, w - 32 * S, rh), delegate { tab = id == "game" ? "gamer" : id; ShowTab(); });
                        cy += rh;
                    }
                cy += 8 * S;
                string ct = L.T((DateTime.Now - weekCopied).TotalSeconds < 3 ? "Copied" : "Copy my week"); var cs = g.MeasureString(ct, Draw.fBodyB);
                var cr = new RectangleF(x + 20 * S, cy, cs.Width + 30 * S, 34 * S); Theme.Fill(g, cr, 17 * S, Theme.Soft); g.DrawString(ct, Draw.fBodyB, ink, cr.X + 15 * S, cr.Y + (34 * S - cs.Height) / 2);
                var rep = r; Act(overviewCanvas, cr, delegate { try { Clipboard.SetText(Weekly.Text(rep)); weekCopied = DateTime.Now; } catch (Exception e) { if (!(e is System.Runtime.InteropServices.ExternalException || e is System.Threading.ThreadStateException)) throw; } overviewCanvas.Invalidate(); });
                string tb = Weekly.TopBlame(r); float lx = cr.Right + 16 * S;
                if (tb == "provider")
                {
                    // the provider's share is the big one: the complaint letter, with every dated moment, is one click away
                    string pt = L.T("Report for my ISP ›"); var ps = g.MeasureString(pt, Draw.fBodyB); var pr = new RectangleF(lx, cy + 7 * S, ps.Width, 22 * S);
                    using (var pb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(pt, Draw.fBodyB, pb, pr.X, pr.Y);
                    Act(overviewCanvas, pr, delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool("ispreport"); });
                    lx = pr.Right + 20 * S;
                }
                if (r.Lv > 0) lx = InvLink(g, overviewCanvas, lx, cy + 8 * S, tb == "wifi" ? "drops" : tb == "pc" ? "pc" : "slow", "week", L.T(r.Head), DateTime.Now.AddDays(-7), DateTime.Now);   // a side door: the week's bad moments as a case
                if (tb != null) LessonLink(g, overviewCanvas, WatchKit.Lesson(tb, "stream"), lx, cy + 8 * S);
                return card.Bottom + 22 * S;
            }
        }
        // "2-min lesson: Wi-Fi - why it drops ›" - opens the Learn topic
        void LessonLink(Graphics g, Control on, string[] lesson, float x, float y)
        {
            if (lesson == null || Knowledge.Topic(lesson[0]) == null) return;
            string t = L.T("2-min lesson: ") + L.T(lesson[1]) + " ›"; var ts = g.MeasureString(t, Draw.fBody); var r = new RectangleF(x, y, ts.Width, ts.Height);
            using (var b = new SolidBrush(Kit.Ink("learn"))) g.DrawString(t, Draw.fBody, b, x, y);
            string id = lesson[0]; Act(on, r, delegate { GoTopic(id); });
        }
        void GoTopic(string id)
        {
            var t = Knowledge.Topic(id); if (t == null) return;
            tab = "learn"; ShowTab(); learnSel = "topic:" + t.Id; learnPkts = null; learnErr = "";
            if (learnInfo != null) { learnInfo.AutoScrollPosition = Point.Empty; learnInfo.Invalidate(); }
            if (learnList != null) learnList.Invalidate();
        }
    }
}
