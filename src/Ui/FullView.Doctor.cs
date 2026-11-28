// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Network Doctor's page (My PC › Network Doctor, 28 Nov): the suspect card, two weeks as a strip (drops and typical ping
    // per day, a pin for each day something changed), the changes newest first, and "what changed in the settings" between
    // two daily snapshots. The work runs off the window thread; the page draws what it has.
    sealed partial class FullView
    {
        Panel doctorPage; DB doctorCanvas; Doctor.Result docRes; volatile bool docBusy; int docSince = 1; List<string> docDiff; string docDiffKey = "";
        Panel BuildDoctorPage()
        {
            doctorPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            doctorCanvas = new DB { Name = "doctorCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            doctorCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintDoctor(e.Graphics); }; Wire(doctorCanvas);
            doctorPage.Controls.Add(doctorCanvas);
            return doctorPage;
        }
        void DoctorShown()
        {
            if (docBusy || (docRes != null && (DateTime.Now - docRes.At).TotalMinutes < 10)) return;
            docBusy = true; doctorCanvas.Invalidate();
            new Thread(() =>
            {
                Doctor.Result r = null;
                try { Doctor.SnapToday(); r = Doctor.Analyse(14); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("doctor: " + e.Message); }
                finally
                {
                    try { BeginInvoke((Action)delegate { docBusy = false; if (r != null) docRes = r; docDiffKey = ""; if (doctorCanvas != null && !doctorCanvas.IsDisposed) doctorCanvas.Invalidate(); }); }
                    catch (InvalidOperationException) { docBusy = false; }
                }
            }) { IsBackground = true, Name = "doctor" }.Start();
        }
        static string DocKind(string k)
        {
            return k == "update" ? "Windows update" : k == "app" ? "App update" : k == "driver" ? "Driver" : k == "hardware" ? "Hardware" : k == "crash" ? "Crash" : k == "restart" ? "Restart" : k == "settings" ? "Settings" : k;
        }
        static Color DocKindColor(string k)
        {
            return k == "update" || k == "app" ? Theme.Blue : k == "driver" ? Kit.Accent("toolbox") : k == "hardware" ? Kit.Accent("security") : k == "crash" ? Theme.Red : k == "restart" ? Theme.Grey : Kit.Accent("time");
        }
        void DocOpenDay(DateTime t)
        {
            var day = t.Date; double at = t.TimeOfDay.TotalSeconds / 86400.0;
            replayDay = day; replayData = null; tab = "replay"; ShowTab(); SyncNav(); ReplayAt(at);
        }
        void PaintDoctor(Graphics g)
        {
            g.TranslateTransform(doctorCanvas.AutoScrollPosition.X, doctorCanvas.AutoScrollPosition.Y);
            Begin(doctorCanvas);
            float x = 24 * S, y = 18 * S, w = doctorCanvas.ClientSize.Width - 48 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(L.T("Network Doctor"), Draw.fTitle, ink, x, y); y += 38 * S;
                string intro = L.T("What changed on this PC (updates, drivers, apps, settings), next to how each day went. It names a suspect and shows why.");
                float ih = Draw.Measure(intro, Draw.fBody, (int)w).Height; g.DrawString(intro, Draw.fBody, sub, new RectangleF(x, y, w, ih + 4)); y += ih + 16 * S;
                var r = docRes;
                if (r == null) { g.DrawString(L.T(docBusy ? "Looking through two weeks of changes..." : "Opening..."), Draw.fBody, muted, x, y); SetNeed(doctorCanvas, new Size(0, (int)(y + 60 * S))); return; }

                // ---- the suspect
                {
                    var s = r.Suspect; int lv = s != null ? s.F.Lv : 3;
                    string head = s != null ? string.Format(L.T("Most likely: {0}"), s.What) : r.WhyT != null ? L.T(r.WhyT).Replace("{0}", r.DataDays.ToString(CultureInfo.CurrentCulture)) : L.T(r.Why);
                    string detail = s != null ? s.T.ToString("ddd d MMM, HH:mm", CultureInfo.CurrentCulture) + " · " + L.T(DocKind(s.Kind)) + " · " + s.F.Text : "";
                    float hh = Draw.Measure(head, Draw.fHead, (int)(w - 40 * S)).Height, dh = detail != "" ? Draw.Measure(detail, Draw.fBody, (int)(w - 40 * S)).Height : 0;
                    var card = new RectangleF(x, y, w, 20 * S + hh + (dh > 0 ? 8 * S + dh : 0) + (s != null ? 52 * S : 20 * S));
                    Theme.Fill(g, card, 14 * S, s != null ? Theme.LvSoft(lv) : Theme.Soft); Theme.Stroke(g, card, 14 * S, s != null ? Theme.Lv(lv) : Theme.Line, 1);
                    using (var hb = new SolidBrush(s != null ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(head, Draw.fHead, hb, new RectangleF(x + 20 * S, y + 16 * S, w - 40 * S, hh + 4));
                    float cy = y + 16 * S + hh + 8 * S;
                    if (dh > 0) { g.DrawString(detail, Draw.fBody, sub, new RectangleF(x + 20 * S, cy, w - 40 * S, dh + 4)); cy += dh + 10 * S; }
                    if (s != null)
                    {
                        var b = MkPill(g, L.T("Open that day in History"), x + 20 * S, cy, true); DateTime t = s.T; Act(doctorCanvas, b, delegate { DocOpenDay(t); });
                        string note = L.T("A suspect, not proof: undo it if you can (roll back the driver, uninstall the update) and see if it gets better.");
                        g.DrawString(note, Draw.fSmall, muted, new RectangleF(b.Right + 14 * S, cy + 6 * S, Math.Max(40, x + w - b.Right - 34 * S), 34 * S));
                    }
                    y = card.Bottom + 22 * S;
                }

                // ---- two weeks as a strip: drops (bars), typical ping (dots), a pin for each change
                {
                    g.DrawString(L.T("The last two weeks"), Draw.fHead, ink, x, y); y += 30 * S;
                    int n = r.Days.Count; float colW = w / Math.Max(1, n), chartH = 120 * S, top = y + 18 * S;
                    double maxPing = Math.Max(30, r.Days.Where(d => !double.IsNaN(d.Ping)).Select(d => d.Ping).DefaultIfEmpty(30).Max() * 1.2);
                    int maxDrops = Math.Max(3, r.Days.Select(d => d.Drops).DefaultIfEmpty(0).Max());
                    using (var grid = new Pen(Theme.Line)) g.DrawLine(grid, x, top + chartH, x + w, top + chartH);
                    PointF? last = null;
                    for (int i = 0; i < n; i++)
                    {
                        var d = r.Days[i]; float cx = x + i * colW;
                        var colR = new RectangleF(cx, top - 18 * S, colW, chartH + 40 * S);
                        if (d.Drops > 0) { float bh = chartH * d.Drops / (float)maxDrops; Theme.Fill(g, new RectangleF(cx + colW * 0.25f, top + chartH - bh, colW * 0.5f, bh), 3 * S, Theme.LvSoft(2)); using (var dbr = new SolidBrush(Theme.LvText(2))) g.DrawString(d.Drops.ToString(CultureInfo.CurrentCulture), Draw.fTiny, dbr, cx + colW * 0.5f - 4 * S, top + chartH - bh - 14 * S); }
                        if (!double.IsNaN(d.Ping))
                        {
                            var p = new PointF(cx + colW / 2, top + chartH - (float)(chartH * d.Ping / maxPing));
                            if (last != null) using (var lp = new Pen(Theme.Blue, 2 * S)) g.DrawLine(lp, last.Value, p);
                            Theme.Fill(g, new RectangleF(p.X - 3.5f * S, p.Y - 3.5f * S, 7 * S, 7 * S), 3.5f * S, Theme.Blue); last = p;
                        }
                        var onDay = r.Changes.Where(c => c.T.Date == d.D.Date && c.Kind != "restart").ToList();
                        if (onDay.Count > 0)
                        {
                            Color pc = onDay.Any(c => c == r.Suspect) ? Theme.Lv(2) : DocKindColor(onDay[0].Kind);
                            Theme.Fill(g, new RectangleF(cx + colW / 2 - 5 * S, top - 14 * S, 10 * S, 10 * S), 5 * S, pc);
                            if (onDay.Count > 1) g.DrawString(onDay.Count.ToString(CultureInfo.CurrentCulture), Draw.fTiny, muted, cx + colW / 2 + 7 * S, top - 17 * S);
                        }
                        g.DrawString(d.D.ToString("ddd", CultureInfo.CurrentCulture), Draw.fTiny, muted, new RectangleF(cx, top + chartH + 4 * S, colW, 14 * S), new StringFormat { Alignment = StringAlignment.Center });
                        g.DrawString(d.D.Day.ToString(CultureInfo.CurrentCulture), Draw.fTiny, muted, new RectangleF(cx, top + chartH + 17 * S, colW, 14 * S), new StringFormat { Alignment = StringAlignment.Center });
                        DateTime dd = d.D; Act(doctorCanvas, colR, delegate { DocOpenDay(dd.AddHours(12)); });
                    }
                    y = top + chartH + 38 * S;
                    g.DrawString(L.T("Blue: typical ping to the internet. Red bars: connection drops that day. A dot on top: something changed that day (red = the suspect). Click a day to replay it."), Draw.fSmall, muted, new RectangleF(x, y, w, 36 * S));
                    y += 40 * S;
                }

                // ---- the changes, newest first
                {
                    g.DrawString(L.T("What changed"), Draw.fHead, ink, x, y); y += 30 * S;
                    var list = r.Changes.Where(c => c.Kind != "restart").Take(40).ToList();
                    if (list.Count == 0) { g.DrawString(L.T("No updates, drivers, hardware or settings changes in the last two weeks."), Draw.fBody, muted, x, y); y += 30 * S; }
                    using (var line = new Pen(Theme.Line2))
                        foreach (var c in list)
                        {
                            float rh = 30 * S; bool f2 = c.F != null && c.F.Lv >= 1;
                            if (f2) rh += 20 * S;
                            g.DrawString(c.T.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture), Draw.fSmall, muted, x, y + 7 * S);
                            string kn = L.T(DocKind(c.Kind)); var kr = new RectangleF(x + 150 * S, y + 5 * S, g.MeasureString(kn, Draw.fTiny).Width + 16 * S, 20 * S);
                            Theme.Fill(g, kr, 10 * S, Kit.Mix(DocKindColor(c.Kind), Theme.Card, 0.82f)); using (var kb = new SolidBrush(Kit.Mix(DocKindColor(c.Kind), Theme.Ink, 0.35f))) g.DrawString(kn, Draw.fTiny, kb, kr.X + 8 * S, kr.Y + 3 * S);
                            using (var tb = new SolidBrush(c == r.Suspect ? Theme.LvText(2) : Theme.Ink)) g.DrawString(c.What, c == r.Suspect ? Draw.fBodyB : Draw.fBody, tb, new RectangleF(kr.Right + 10 * S, y + 5 * S, x + w - kr.Right - 10 * S, 22 * S), nowrap);
                            if (f2) using (var fb = new SolidBrush(Theme.LvText(c.F.Lv))) g.DrawString(L.T("After it:") + " " + c.F.Text, Draw.fSmall, fb, new RectangleF(kr.Right + 10 * S, y + 28 * S, x + w - kr.Right - 10 * S, 18 * S), nowrap);
                            DateTime t = c.T; Act(doctorCanvas, new RectangleF(x, y, w, rh), delegate { DocOpenDay(t); }); Hint(doctorCanvas, new RectangleF(x, y, w, rh), "doctor.change");
                            y += rh; g.DrawLine(line, x, y, x + w, y);
                        }
                    y += 22 * S;
                }

                // ---- what changed in the settings: two daily snapshots compared
                {
                    g.DrawString(L.T("Settings, apps and services - compared"), Draw.fHead, ink, x, y); y += 30 * S;
                    var days = Doctor.SnapDays();
                    if (days.Count < 2) { g.DrawString(L.T("The Network Doctor saves a small snapshot each day. Come back tomorrow to compare."), Draw.fBody, muted, new RectangleF(x, y, w, 44 * S)); y += 50 * S; }
                    else
                    {
                        float px = x;
                        foreach (var o in new[] { new[] { "1", "since yesterday" }, new[] { "7", "since a week ago" }, new[] { "99", "since the first snapshot" } })
                        {
                            int v = int.Parse(o[0], CultureInfo.InvariantCulture); var b = MkPill(g, L.T(o[1]), px, y, docSince == v);
                            Act(doctorCanvas, b, delegate { docSince = v; docDiffKey = ""; doctorCanvas.Invalidate(); }); px = b.Right + 8 * S;
                        }
                        y += 44 * S;
                        DateTime b2 = days[days.Count - 1], a = days.Where(d => d <= b2.AddDays(-docSince)).DefaultIfEmpty(days[0]).Last(); if (docSince == 99) a = days[0];
                        string key = a.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + b2.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                        if (docDiffKey != key) { docDiffKey = key; docDiff = Doctor.Diff(Doctor.Load(a), Doctor.Load(b2)); }
                        g.DrawString(string.Format(L.T("{0} compared with {1}"), b2.ToString("ddd d MMM", CultureInfo.CurrentCulture), a.ToString("ddd d MMM", CultureInfo.CurrentCulture)), Draw.fSmall, muted, x, y); y += 24 * S;
                        if (docDiff == null || docDiff.Count == 0) { g.DrawString(L.T("Nothing changed between them."), Draw.fBody, muted, x, y); y += 28 * S; }
                        else foreach (var d in docDiff.Take(80))
                            {
                                Color dc = d[0] == '+' ? Theme.Green : d[0] == '-' ? Theme.Red : Theme.Amber;
                                Theme.Fill(g, new RectangleF(x, y + 8 * S, 8 * S, 8 * S), 4 * S, dc);
                                g.DrawString(Doctor.Plain(d), Draw.fBody, ink, new RectangleF(x + 18 * S, y + 2 * S, w - 18 * S, 22 * S), nowrap); y += 26 * S;
                            }
                        if (docDiff != null && docDiff.Count > 80) { g.DrawString(string.Format(L.T("...and {0} more"), docDiff.Count - 80), Draw.fSmall, muted, x, y); y += 24 * S; }
                    }
                    g.DrawString(L.T("Snapshots stay on this PC for 30 days, only while History is on."), Draw.fSmall, muted, new RectangleF(x, y + 6 * S, w, 36 * S)); y += 44 * S;
                }
            }
            SetNeed(doctorCanvas, new Size(0, (int)(y + 20 * S)));
        }
        // a small pill button drawn on a canvas (the Doctor's choices)
        RectangleF MkPill(Graphics g, string text, float x, float y, bool on)
        {
            var sz = g.MeasureString(text, Draw.fBody); var r = new RectangleF(x, y, sz.Width + 28 * S, 32 * S);
            Theme.Fill(g, r, 16 * S, on ? Theme.BlueSoft : Theme.Card); Theme.Stroke(g, r, 16 * S, on ? Theme.Blue : Theme.Line, on ? 1.5f * S : 1);
            using (var b = new SolidBrush(on ? Theme.BlueText : Theme.Ink)) using (var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(text, Draw.fBody, b, r, cf);
            return r;
        }
    }
}
