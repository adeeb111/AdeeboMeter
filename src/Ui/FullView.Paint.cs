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
        // ---------- painting
        void PaintHeader(Graphics g, Rectangle r)
        {
            float x = Sc(18), cy = r.Height / 2f; Begin(header);
            Theme.Logo(g, new RectangleF(x, cy - 15 * S, 30 * S, 30 * S), true); x += 40 * S;
            using (var b = new SolidBrush(Theme.Ink)) using (var mb = new SolidBrush(Theme.Muted))
            {
                var ts = g.MeasureString("AdeeboMeter", Draw.fBig); var tg = g.MeasureString(Program.Tagline, Draw.fSmall);
                g.DrawString(L.T("AdeeboMeter"), Draw.fBig, b, x, cy - ts.Height / 2 - 8 * S);
                g.DrawString(Program.Tagline, Draw.fSmall, mb, x + 1 * S, cy + ts.Height / 2 - 6 * S);
                x += Math.Max(ts.Width, tg.Width) + 10 * S;
            }
            string w = Draw.Word(snap.Level);
            Theme.Pill(g, w, Draw.fPill, x, cy - 8 * S, snap.Level, S); x += g.MeasureString(w, Draw.fPill).Width + 22 * S;
            string head = (snap.Level == 0 && !Voice.Serious ? Voice.Greet() + " " : "") + snap.Headline;
            using (var hb = new SolidBrush(snap.Level == 0 ? Theme.Ink : Theme.LvText(snap.Level)))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                float room = Math.Max(0, themeSw.Left - x - 16 * S), lw = 0; string link = "Investigate  ›";
                if (snap.Level > 0 && snap.Flow != "") lw = g.MeasureString(link, Draw.fBodyB).Width + 14 * S;
                float hw = Math.Max(0, Math.Min(g.MeasureString(head, Draw.fHead).Width, room - lw));
                g.DrawString(head, Draw.fHead, hb, new RectangleF(x, cy - 10 * S, Math.Max(0, hw), 22 * S), sf);
                if (lw > 0)
                {
                    // a problem leads straight to the check that investigates it
                    var lr = new RectangleF(x + hw + 14 * S, cy - 9 * S, lw - 14 * S, 22 * S); string fl = snap.Flow;
                    using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(link, Draw.fBodyB, lb, lr.X, lr.Y);
                    Act(header, lr, delegate { tab = "guide"; ShowTab(); StartGuide(fl, false); });
                }
            }
        }

        void PaintFooter(Graphics g, Rectangle r)
        {
            using (var line = new Pen(Theme.Line)) g.DrawLine(line, 0, 0, r.Width, 0);
            float x = Sc(20), y = 7 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("AdeeboMeter itself"), Draw.fBodyB, ink, x, y - 1 * S); x += g.MeasureString(L.T("AdeeboMeter itself"), Draw.fBodyB).Width + 14 * S;
                string st = selfCpu.ToString("0.0", CultureInfo.CurrentCulture) + "% CPU    " + (selfMem / 1048576.0).ToString("0", CultureInfo.CurrentCulture) + " MB of 64 MB budget" + (live.Running && liveView != null ? "    + tshark " + liveView.TsharkMB + " MB (live capture on)" : "");
                using (var b = new SolidBrush(selfMem > 64L * 1048576 ? Theme.Red : Theme.Ink)) g.DrawString(st, Draw.fMono, b, x, y);
                string right = Program.Version + " · no cloud · no telemetry · never listens · hover the ? cursor to learn · Esc = back to the corner";
                var rs = g.MeasureString(right, Draw.fSmall); g.DrawString(right, Draw.fSmall, muted, r.Width - rs.Width - 20 * S, y);
            }
        }

        void PaintSide(Graphics g, Rectangle r)
        {
            float x = 0, y = 0, w = r.Width;
            Begin(side);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub))
            {
                float h1 = 16 * S + 22 * S + snap.Health.Count * 24 * S + 8 * S;
                var c1 = new RectangleF(x, y, w - 1, h1); Theme.Fill(g, c1, 12 * S, Theme.Card); Theme.Stroke(g, c1, 12 * S, Theme.Line, 1);
                float yy = y + 14 * S, xx = x + 14 * S, ww = w - 28 * S;
                g.DrawString(L.T("TCP / IP HEALTH"), Draw.fTiny, muted, xx, yy);
                var ns = g.MeasureString(L.T("Windows counters · no admin"), Draw.fSmall); g.DrawString(L.T("Windows counters · no admin"), Draw.fSmall, muted, xx + ww - ns.Width, yy - 2 * S);
                yy += 22 * S;
                for (int i = 0; i < snap.Health.Count && i < snap.HealthHist.Count; i++)
                {
                    var hrow = snap.Health[i]; int lv = int.Parse(hrow[2], CultureInfo.InvariantCulture);
                    Hint(side, new RectangleF(xx, yy - 2 * S, ww, 22 * S), hrow[0]);
                    using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                        g.DrawString(hrow[0], Draw.fBody, sub, new RectangleF(xx, yy, ww - 170 * S, 20 * S), sf);
                    Theme.Spark(g, snap.HealthHist[i], new RectangleF(xx + ww - 160 * S, yy + 2 * S, 84 * S, 16 * S), Theme.Lv(lv), 0, false);
                    var vs = g.MeasureString(hrow[1], Draw.fMono);
                    using (var b = new SolidBrush(lv == 1 || lv == 2 ? Theme.Lv(lv) : Theme.Ink)) g.DrawString(hrow[1], Draw.fMono, b, xx + ww - vs.Width, yy);
                    yy += 24 * S;
                }
                y += h1 + 14 * S;
                float h2 = 16 * S + 20 * S + 4 * 20 * S + 8 * S;
                var c2 = new RectangleF(x, y, w - 1, h2); Theme.Fill(g, c2, 12 * S, Theme.Card); Theme.Stroke(g, c2, 12 * S, Theme.Line, 1);
                yy = y + 14 * S; g.DrawString(L.T("THIS PC"), Draw.fTiny, muted, xx, yy); yy += 20 * S;
                float yb = yy; Draw.Bar(g, "", "CPU", snap.Cpu, Draw.Fmt(snap.Cpu, "0", "%"), 60, 88, xx, ref yy, ww, null); Hint(side, new RectangleF(xx, yb, ww, yy - yb), "CPU");
                yb = yy; Draw.Bar(g, "", "Memory", snap.Mem, snap.MemText, 80, 90, xx, ref yy, ww, null); Hint(side, new RectangleF(xx, yb, ww, yy - yb), "Memory");
                yb = yy; Draw.Bar(g, "", "Disk", snap.Disk, snap.DiskText, 60, 90, xx, ref yy, ww, null); Hint(side, new RectangleF(xx, yb, ww, yy - yb), "Disk");
                yb = yy; double dn = snap.Down; Draw.Bar(g, "", "Download", double.IsNaN(dn) ? double.NaN : Math.Min(100, dn), Draw.Fmt(dn, "0.0", " Mb/s"), 200, 200, xx, ref yy, ww, null); Hint(side, new RectangleF(xx, yb, ww, yy - yb), "Download");
                y += h2 + 14 * S;
                int fit = (int)((r.Height - y - 16 * S - 18 * S - 8 * S) / (20 * S));   // fewer rows on short / high-DPI screens instead of clipping
                int n = Math.Max(0, Math.Min(Math.Min(6, fit), Math.Max(snap.TopMem.Count, snap.TopCpu.Count)));
                if (n == 0) return;
                float h3 = 16 * S + 18 * S + n * 20 * S + 8 * S;
                var c3 = new RectangleF(x, y, w - 1, h3); Theme.Fill(g, c3, 12 * S, Theme.Card); Theme.Stroke(g, c3, 12 * S, Theme.Line, 1);
                yy = y + 14 * S; float colW = (ww - 16 * S) / 2;
                g.DrawString(L.T("TOP BY MEMORY"), Draw.fTiny, muted, xx, yy); g.DrawString(L.T("TOP BY CPU"), Draw.fTiny, muted, xx + colW + 16 * S, yy);
                Hint(side, new RectangleF(xx, yy, colW, 16 * S), "Top by memory"); Hint(side, new RectangleF(xx + colW + 16 * S, yy, colW, 16 * S), "Top by CPU"); yy += 18 * S;
                for (int i = 0; i < n; i++)
                {
                    if (i < snap.TopMem.Count) Hint(side, new RectangleF(xx, yy, colW, 20 * S), "proc:" + snap.TopMem[i].Name);
                    if (i < snap.TopCpu.Count) Hint(side, new RectangleF(xx + colW + 16 * S, yy, colW, 20 * S), "proc:" + snap.TopCpu[i].Name);
                    if (i < snap.TopMem.Count) { var a = snap.TopMem[i]; g.DrawString(Draw.Trunc(a.Name, 15), Draw.fBody, ink, xx, yy); var v = Draw.MemStr(a.Mem); var vs = g.MeasureString(v, Draw.fMono); g.DrawString(v, Draw.fMono, ink, xx + colW - vs.Width, yy); }
                    if (i < snap.TopCpu.Count) { var a = snap.TopCpu[i]; g.DrawString(Draw.Trunc(a.Name, 15), Draw.fBody, ink, xx + colW + 16 * S, yy); var v = a.Cpu.ToString("0.0", CultureInfo.CurrentCulture) + "%"; var vs = g.MeasureString(v, Draw.fMono); g.DrawString(v, Draw.fMono, ink, xx + ww - vs.Width, yy); }
                    yy += 20 * S;
                }
            }
        }

        void PaintDest(Graphics g)
        {
            g.TranslateTransform(destCanvas.AutoScrollPosition.X, destCanvas.AutoScrollPosition.Y);
            float x = 14 * S, y = 6 * S, w = destCanvas.ClientSize.Width - 28 * S;
            Begin(destCanvas);
            if (Cfg.PinWatch && Pinned.Host != "") y = PaintPinWatch(g, x, y, w);
            var pr = pinResults;
            if (pr != null) y = PaintPinTable(g, pr, x, y, w);
            var d = dest;
            if (d == null && pr != null)
            {
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Voice.P("Click a row for the full story.", "Click a row for details."), Draw.fBody, mb, x, y);
                var need0 = new Size(0, (int)(y + 40 * S)); SetNeed(destCanvas, need0);
                return;
            }
            bool done = d != null && d.Done;
            int lv = d == null ? 3 : done ? d.Level : -2;
            string title = d == null ? "READY" : done ? "RESULT · " + d.Target : "CHECKING " + d.Target;
            string verdict = d == null ? Voice.P("Give me a host, an IP or a URL and press Check. Nothing runs until you do.", "Type a host, IP or URL and press Check.")
                                           + (Cfg.Pins.Count == 0 ? Voice.P("\nTip: press Pin to save places you check often - the VPN head-end, Jira, the lab jump host - then run them all at once.", "\nTip: Pin saves a destination; Run all checks every saved one.") : "")
                           : done ? d.Verdict : (d.Note ?? Voice.P("Working through DNS, route, ping, TCP, TLS, HTTP, MTU and path...", "Checking..."));
            float vh = Draw.Measure(verdict, Draw.fTitle, (int)(w - 32 * S)).Height;
            var vb = new RectangleF(x, y, w, vh + 38 * S);
            Color bg = lv == -2 ? Theme.BlueSoft : lv == 3 ? Theme.Soft : Theme.LvSoft(lv), fg = lv == -2 ? Theme.BlueDark : lv == 3 ? Theme.Sub : Theme.LvText(lv);
            Theme.Fill(g, vb, 10 * S, bg);
            using (var b = new SolidBrush(fg)) { g.DrawString(title, Draw.fTiny, b, x + 16 * S, y + 10 * S); g.DrawString(verdict, Draw.fTitle, b, new RectangleF(x + 16 * S, y + 26 * S, w - 32 * S, vh + 4)); }
            y += vb.Height + 20 * S;
            if (done)
            {
                // Copy as text and Save: only when you press them, only where you choose
                string ct = destCopied > DateTime.Now.AddSeconds(-3) ? "Copied ✓" : "Copy as text", st = "Save...";
                var cs = g.MeasureString(ct, Draw.fSmall); var ss = g.MeasureString(st, Draw.fSmall); var cr = new RectangleF(x + w - 16 * S - cs.Width, vb.Y + 8 * S, cs.Width, cs.Height); var sr = new RectangleF(cr.X - 14 * S - ss.Width, cr.Y, ss.Width, ss.Height);
                using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) { g.DrawString(ct, Draw.fSmall, lb, cr.X, cr.Y); g.DrawString(st, Draw.fSmall, lb, sr.X, sr.Y); }
                var dd = d; Act(destCanvas, cr, delegate { try { Clipboard.SetText(dd.Text()); destCopied = DateTime.Now; } catch (Exception ce) { CopyFailed(ce); } destCanvas.Invalidate(); });
                Act(destCanvas, sr, delegate { SaveText("AdeeboMeter-check-" + Draw.Trunc(dd.Target, 40).Replace(':', '-'), dd.Text()); });
            }
            int n = DestResult.Steps.Length; float slot = w / n, dot = 18 * S;
            using (var line = new Pen(Theme.Rail, 2 * S)) for (int i = 0; i < n - 1; i++) g.DrawLine(line, x + slot * i + slot / 2 + dot / 2 + 2 * S, y + dot / 2, x + slot * (i + 1) + slot / 2 - dot / 2 - 2 * S, y + dot / 2);
            using (var center = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                for (int i = 0; i < n; i++)
                {
                    float cx = x + slot * i + slot / 2;
                    int sl = d == null ? -1 : d.Lv[i]; bool cur = d != null && !done && d.Running == i;
                    if (cur) { using (var b = new SolidBrush(Theme.Card)) g.FillEllipse(b, cx - dot / 2, y, dot, dot); using (var p = new Pen(Theme.Blue, 3 * S)) g.DrawEllipse(p, cx - dot / 2, y, dot, dot); }
                    else using (var b = new SolidBrush(sl < 0 ? Theme.Track : Theme.Lv(sl))) g.FillEllipse(b, cx - dot / 2, y, dot, dot);
                    using (var b = new SolidBrush(Theme.Ink)) g.DrawString(DestResult.Steps[i], Draw.fName, b, new RectangleF(cx - slot / 2, y + dot + 3 * S, slot, 18 * S), center);
                    Hint(destCanvas, new RectangleF(cx - slot / 2, y - 4 * S, slot, dot + 44 * S), "step." + DestResult.Steps[i]);
                    string v = d == null ? "-" : cur ? "checking..." : d.Val[i] ?? "-";
                    using (var b = new SolidBrush(Theme.Sub)) g.DrawString(v, Draw.fMonoS, b, new RectangleF(cx - slot / 2 + 2 * S, y + dot + 21 * S, slot - 4 * S, 16 * S), center);
                }
            y += dot + 48 * S;
            if (d != null && (d.Story != null || (d.Findings != null && d.Findings.Count > 0))) y = PaintStory(g, d, x, y, w);
            var cards = d != null ? d.CardsCopy() : new List<KeyValuePair<string, List<string[]>>>();
            if (cards.Count > 0)
            {
                int per = Math.Max(1, (int)(w / (300 * S))); float cw = (w - (per - 1) * 12 * S) / per, inner = cw - 28 * S;
                // each row: name and value on one line when they fit, otherwise the value gets its own (wrapping) line - nothing is cut
                Func<string[], float> rowH = row =>
                {
                    string rn = row[0], rv = row.Length > 1 ? row[1] : "";
                    float nw = g.MeasureString(rn, Draw.fBody).Width, vw = g.MeasureString(rv, Draw.fMonoS).Width;
                    if (nw + vw + 12 * S <= inner) return 19 * S;
                    return Draw.Measure(rn, Draw.fBody, (int)inner).Height + Draw.Measure(rv, Draw.fMonoS, (int)inner).Height + 6 * S;
                };
                for (int i = 0; i < cards.Count; i += per)
                {
                    float rowsH = 0;
                    for (int j = i; j < Math.Min(cards.Count, i + per); j++) rowsH = Math.Max(rowsH, 38 * S + cards[j].Value.Sum(rowH));
                    for (int j = i; j < Math.Min(cards.Count, i + per); j++)
                    {
                        var cr = new RectangleF(x + (j - i) * (cw + 12 * S), y, cw, rowsH);
                        Theme.Stroke(g, cr, 10 * S, Theme.Track, 1);
                        using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(cards[j].Key, Draw.fTiny, mb, new RectangleF(cr.X + 14 * S, cr.Y + 12 * S, inner, 16 * S));
                        Hint(destCanvas, new RectangleF(cr.X, cr.Y, cr.Width, 26 * S), CardKey(cards[j].Key));
                        float ry = cr.Y + 30 * S;
                        foreach (var row in cards[j].Value)
                        {
                            string v = row.Length > 1 ? row[1] : ""; float h = rowH(row);
                            using (var mb = new SolidBrush(Theme.Muted)) using (var vb2 = new SolidBrush(row.Length > 2 ? Theme.LvText(int.Parse(row[2], CultureInfo.InvariantCulture)) : Theme.Ink))
                            {
                                if (h <= 19 * S + 0.5f)
                                {
                                    g.DrawString(row[0], Draw.fBody, mb, cr.X + 14 * S, ry);
                                    var vs = g.MeasureString(v, Draw.fMonoS); g.DrawString(v, Draw.fMonoS, vb2, cr.Right - 14 * S - vs.Width, ry + 1 * S);
                                }
                                else
                                {
                                    float nh = Draw.Measure(row[0], Draw.fBody, (int)inner).Height;
                                    g.DrawString(row[0], Draw.fBody, mb, new RectangleF(cr.X + 14 * S, ry, inner, nh + 2));
                                    g.DrawString(v, Draw.fMonoS, vb2, new RectangleF(cr.X + 14 * S, ry + nh, inner, h - nh));
                                }
                            }
                            ry += h;
                        }
                    }
                    y += rowsH + 12 * S;
                }
            }
            Need(destCanvas, y);
        }

        void PaintHealth(Graphics g)
        {
            g.TranslateTransform(healthCanvas.AutoScrollPosition.X, healthCanvas.AutoScrollPosition.Y);
            float x = 14 * S, y = 4 * S, w = healthCanvas.ClientSize.Width - 28 * S;
            Begin(healthCanvas);
            y = PaintToolResult(g, x, y, w);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (health == null && (toolRows != null || toolBusy)) { var need0 = new Size(0, (int)(y + 20 * S)); SetNeed(healthCanvas, need0); return; }
                if (health == null)
                {
                    g.DrawString(healthRunning ? Voice.P("Poking around...", "Running...") : Voice.P("Press the button and I'll give your PC a quick once-over.", "Press Run health check."), Draw.fBody, muted, x, y + 6 * S);
                    return;
                }
                float nameW = 250 * S;
                foreach (var row in health)
                {
                    int lv = int.Parse(row[0], CultureInfo.InvariantCulture);
                    float ah = Draw.Measure(row[3], Draw.fBody, (int)(w - nameW - 30 * S)).Height;
                    float rh = Math.Max(46 * S, ah + 18 * S);
                    Draw.LvDot(g, lv, x, y + 12 * S, 13 * S, 13 * S);
                    g.DrawString(row[1], Draw.fBodyB, ink, x + 26 * S, y + 6 * S);
                    { string ev = Evidence.For("health", row); float ex = x + 26 * S + g.MeasureString(row[1], Draw.fBodyB).Width + 6 * S, ew = Evidence.Pill(g, ev, ex, y + 8 * S, healthAt); Hint(healthCanvas, new RectangleF(ex, y + 4 * S, ew, 20 * S), Evidence.Key(ev)); }
                    Hint(healthCanvas, new RectangleF(x, y, nameW, rh), "hc." + (row[1].StartsWith("Disk", StringComparison.Ordinal) ? "Disk" : row[1]));
                    g.DrawString(row[2], Draw.fMonoS, sub, new RectangleF(x + 26 * S, y + 25 * S, nameW - 30 * S, 16 * S), nowrap);
                    g.DrawString(row[3], Draw.fBody, sub, new RectangleF(x + nameW + 10 * S, y + 8 * S, w - nameW - 20 * S, ah + 4));
                    y += rh; g.DrawLine(line, x, y, x + w, y); y += 4 * S;
                }
                var need = new Size(0, (int)(y + 20 * S));
                SetNeed(healthCanvas, need);
            }
        }

        // the outage log: every time the internet went quiet in the last 7 days (from history + the last 2 hours)
        float PaintOutages(Graphics g, float x, float y, float w)
        {
            var list = Outages.Last(M, 7);
            using (var muted = new SolidBrush(Theme.Muted)) using (var ink = new SolidBrush(Theme.Ink))
            {
                g.DrawString("INTERNET OUTAGES · LAST 7 DAYS" + (Cfg.History ? "" : " · HISTORY IS OFF, SO ONLY THE LAST 2 HOURS"), Draw.fTiny, muted, x, y); y += 20 * S;
                if (list.Count == 0) { g.DrawString(Cfg.NetSec == 0 ? "The internet check is off in Settings, so outages can't be noticed." : "None - the internet answered every time AdeeboMeter checked.", Draw.fBody, muted, x, y); y += 26 * S; }
                foreach (var o in list.Take(8))
                {
                    using (var b = new SolidBrush(o.Ongoing ? Theme.Red : Theme.Amber)) g.FillEllipse(b, x + 2 * S, y + 6 * S, 9 * S, 9 * S);
                    g.DrawString(Outages.Text(o), Draw.fBody, ink, x + 20 * S, y); y += 24 * S;
                }
                if (list.Count > 8) { g.DrawString("and " + (list.Count - 8) + " more", Draw.fSmall, muted, x + 20 * S, y); y += 22 * S; }
            }
            return y + 12 * S;
        }
        void PaintChanges(Graphics g)
        {
            g.TranslateTransform(changesCanvas.AutoScrollPosition.X, changesCanvas.AutoScrollPosition.Y);
            float x = 14 * S, y = 12 * S, w = changesCanvas.ClientSize.Width - 28 * S;
            Begin(changesCanvas);
            y = PaintDayPicker(g, x, y, w);
            if (histDay == null) y = PaintInsights(g, x, y, w);
            if (histDay == null) y = PaintOutages(g, x, y, w);
            var evs = histDay == null ? M.Events.Recent(300) : (histData != null ? histData.E : new List<Timeline.Ev>());
            if (histDay != null && histData != null)
            {
                float ch = 196 * S; string key = histDay.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "|" + (int)w + "|" + Theme.Dark + "|" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(histData) + "|" + histData.M.Count;   // a reload of the same day (Today again) is new data
                if (histBmp == null || histKey != key)
                {
                    if (histBmp != null) histBmp.Dispose();
                    histBmp = new Bitmap(Math.Max(1, (int)w), Math.Max(1, (int)ch)); histKey = key;
                    using (var bg = Graphics.FromImage(histBmp)) { Prep(bg); bg.Clear(Theme.Card); PaintDayChart(bg, histData, 0, 0, w); }
                }
                g.DrawImageUnscaled(histBmp, (int)x, (int)y);
                Hint(changesCanvas, new RectangleF(x, y, w, ch), "hist.CHART");
                y += ch + 14 * S;
            }
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var line = new Pen(Theme.Line2))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(histDay == null ? "LAST 2 HOURS · NEWEST FIRST · ONLY CHANGES ARE RECORDED" + (Cfg.History ? " · ALSO SAVED TO HISTORY" : ", IN MEMORY ONLY") : "EVERYTHING THAT CHANGED ON " + histDay.Value.ToString("dddd d MMMM", CultureInfo.CurrentCulture).ToUpperInvariant(), Draw.fTiny, muted, x, y); y += 22 * S;
                if (evs.Count <= (histDay == null ? 1 : 0)) g.DrawString(Voice.P("Nothing has changed yet. Boring is good.", "No changes recorded yet."), Draw.fBody, muted, x, y + 4 * S);
                foreach (var e in evs)
                {
                    g.DrawString(e.T.ToString("HH:mm:ss", CultureInfo.CurrentCulture), Draw.fMonoS, muted, x, y + 4 * S);
                    Draw.LvDot(g, e.Lv, x + 76 * S, y + 7 * S, 10 * S, 10 * S);
                    using (var tb = new SolidBrush(e.Lv == 2 ? Theme.LvText(2) : Theme.Ink)) g.DrawString(e.Text, e.Lv == 2 ? Draw.fBodyB : Draw.fBody, tb, new RectangleF(x + 96 * S, y + 2 * S, w - 200 * S, 20 * S), nowrap);
                    var ks = g.MeasureString(e.Kind, Draw.fSmall); g.DrawString(e.Kind, Draw.fSmall, muted, x + w - ks.Width, y + 4 * S);
                    Hint(changesCanvas, new RectangleF(x + 76 * S, y, w - 76 * S, 26 * S), "kind." + e.Kind);
                    y += 26 * S; g.DrawLine(line, x, y, x + w, y);
                }
            }
            var need = new Size(0, (int)(y + 20 * S));
            SetNeed(changesCanvas, need);
        }

        DateTime? histDay; History.Day histData; Bitmap histBmp; string histKey = ""; List<DateTime> histDays; DateTime histDaysAt = DateTime.MinValue;
        float PaintDayPicker(Graphics g, float x, float y, float w)
        {
            if (histDays == null || (DateTime.UtcNow - histDaysAt).TotalSeconds > 30) { histDays = History.Days(); histDaysAt = DateTime.UtcNow; }
            var days = histDays;
            if (days.Count == 0)
            {
                string t = Cfg.History ? "History is on - the first day appears here after a few minutes." : "Want to look back at yesterday? Turn on 7-day history in Settings  >";
                using (var b = new SolidBrush(Cfg.History ? Theme.Muted : (Theme.Dark ? Theme.BlueDark : Theme.Blue))) g.DrawString(t, Draw.fBody, b, x, y);
                if (!Cfg.History) Act(changesCanvas, new RectangleF(x, y, g.MeasureString(t, Draw.fBody).Width, 20 * S), delegate { tab = "settings"; ShowTab(); });
                return y + 32 * S;
            }
            float cx = x;
            var opts = new List<KeyValuePair<string, DateTime?>> { new KeyValuePair<string, DateTime?>("Now (2 h)", null) };
            foreach (var d in days.Take(7)) opts.Add(new KeyValuePair<string, DateTime?>(d.Date == DateTime.Today ? "Today" : d.Date == DateTime.Today.AddDays(-1) ? "Yesterday" : d.ToString("ddd d MMM", CultureInfo.CurrentCulture), d));
            foreach (var o in opts)
            {
                float tw = g.MeasureString(o.Key, Draw.fBody).Width + 26 * S; var r = new RectangleF(cx, y, tw, 30 * S);
                bool on = histDay == o.Value;
                Theme.Fill(g, r, 15 * S, on ? Theme.Blue : Theme.Soft); if (!on) Theme.Stroke(g, r, 15 * S, Theme.Line, 1);
                using (var b = new SolidBrush(on ? Color.White : Theme.Sub)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(o.Key, Draw.fBody, b, r, c);
                var pick = o.Value; Act(changesCanvas, r, delegate { histDay = pick; histData = pick == null ? null : History.Load(pick.Value); changesCanvas.AutoScrollPosition = Point.Empty; changesCanvas.Invalidate(); });
                cx += tw + 8 * S; if (cx > x + w - 120 * S) break;
            }
            return y + 44 * S;
        }
        // One day at a glance: the verdict as a coloured band, router latency and Wi-Fi signal as lines, events as ticks.
        static float PaintDayChart(Graphics g, History.Day d, float x, float y, float w)
        {
            float h = 150 * S; var card = new RectangleF(x, y, w, h + 46 * S);
            Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("THE DAY AT A GLANCE  ·  BAND = VERDICT, BLUE = ROUTER DELAY (MS), GREEN = WI-FI SIGNAL (%)"), Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            float px = x + 40 * S, pw = w - 60 * S, top = y + 34 * S, bandY = top + h - 18 * S, ph = h - 30 * S;
            Func<double, float> X = sec => px + (float)(sec / 86400.0) * pw;
            using (var axis = new Pen(Theme.Line)) using (var mb = new SolidBrush(Theme.Muted))
                for (int hr = 0; hr <= 24; hr += 3) { float xx = X(hr * 3600); g.DrawLine(axis, xx, top, xx, bandY); g.DrawString(hr.ToString("00", CultureInfo.InvariantCulture), Draw.fSmall, mb, xx - 7 * S, bandY + 10 * S); }
            double gmax = Math.Max(20, d.M.Where(m => !double.IsNaN(m[5])).Select(m => m[5]).DefaultIfEmpty(20).Max());
            // the verdict band: one rectangle per run of minutes with the same level (not one per minute)
            var lvBrush = new[] { new SolidBrush(Theme.Lv(0)), new SolidBrush(Theme.Lv(1)), new SolidBrush(Theme.Lv(2)), new SolidBrush(Theme.Lv(3)) };
            try
            {
                int runLv = -1; double runFrom = 0, runTo = 0;
                Action flush = delegate { if (runLv >= 0) g.FillRectangle(lvBrush[runLv], X(runFrom), bandY, Math.Max(1.5f, X(runTo) - X(runFrom) + pw / 1440f), 8 * S); };
                foreach (var m in d.M)
                {
                    int l = double.IsNaN(m[1]) ? 0 : Math.Max(0, Math.Min(3, (int)m[1]));
                    if (l == runLv && m[0] - runTo < 120) { runTo = m[0]; continue; }
                    flush(); runLv = l; runFrom = runTo = m[0];
                }
                flush();
            }
            finally { foreach (var b in lvBrush) b.Dispose(); }
            Action<int, double, Color> lineOf = delegate(int col, double max, Color c)
            {
                using (var pen = new Pen(c, 1.5f * S))
                {
                    // one polyline per unbroken stretch (not one DrawLine per minute)
                    var run = new List<PointF>(); double prevT = -1;
                    Action draw = delegate { if (run.Count > 1) g.DrawLines(pen, run.ToArray()); run.Clear(); };
                    foreach (var m in d.M)
                    {
                        if (double.IsNaN(m[col]) || m[col] < 0) { draw(); continue; }
                        if (run.Count > 0 && m[0] - prevT >= 120) draw();
                        run.Add(new PointF(X(m[0]), top + ph - (float)(Math.Min(m[col], max) / max) * ph)); prevT = m[0];
                    }
                    draw();
                }
            };
            lineOf(7, 100, Theme.Green); lineOf(5, gmax, Theme.Blue);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(gmax.ToString("0", CultureInfo.CurrentCulture) + " ms", Draw.fSmall, mb, x + 6 * S, top - 2 * S);
            foreach (var e in d.E.Where(e => e.Lv >= 1)) Draw.LvDot(g, e.Lv, X(e.T.TimeOfDay.TotalSeconds) - 3 * S, top - 4 * S, 6 * S, 6 * S);
            return y + card.Height + 14 * S;
        }

        void PaintLive(Graphics g)
        {
            g.TranslateTransform(liveCanvas.AutoScrollPosition.X, liveCanvas.AutoScrollPosition.Y);
            float x = 14 * S, y = 4 * S, w = liveCanvas.ClientSize.Width - 28 * S;
            Begin(liveCanvas);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (live.Error != "") { using (var eb = new SolidBrush(Theme.LvText(1))) g.DrawString(live.Error, Draw.fBody, eb, new RectangleF(x, y, w, 40 * S)); y += 30 * S; }
                var v = liveView;
                if (!live.Running || v == null)
                {
                    g.DrawString(live.Running ? Voice.P("Warming up tshark...", "Starting...") : Voice.P("Press Start and I'll put Wireshark's brain on your traffic. Nothing runs until you do.", "Press Start live capture."), Draw.fBody, muted, x, y + 6 * S);
                    return;
                }
                g.DrawString(v.State + "   ·   " + v.Pkts.ToString("N0", CultureInfo.CurrentCulture) + " packets   ·   " + Draw.MemStr(v.Bytes) + "   ·   " + v.Pps.ToString("0", CultureInfo.CurrentCulture) + " pkt/s", Draw.fMonoS, sub, x, y); y += 24 * S;
                if (v.DhcpServers.Count > 1)
                {
                    string warn = Voice.P("Two DHCP servers are handing out addresses here (" + string.Join(", ", v.DhcpServers) + "). Only one should - the other may be a rogue device or a misconfigured router.", "Multiple DHCP servers seen: " + string.Join(", ", v.DhcpServers) + ".");
                    var wh = Draw.Measure(warn, Draw.fBodyB, (int)(w - 28 * S)).Height; var wr = new RectangleF(x, y, w, wh + 20 * S);
                    Theme.Fill(g, wr, 10 * S, Theme.LvSoft(2)); using (var wb = new SolidBrush(Theme.LvText(2))) g.DrawString(warn, Draw.fBodyB, wb, new RectangleF(x + 14 * S, y + 10 * S, w - 28 * S, wh + 4));
                    Hint(liveCanvas, wr, "live.DHCP"); y += wr.Height + 12 * S;
                }
                float colW = (w - 24 * S) / 2, cardH = 0;
                // protocol hierarchy
                var c1 = new RectangleF(x, y, colW, 34 * S + Math.Max(1, v.Phs.Count) * 20 * S);
                Theme.Stroke(g, c1, 10 * S, Theme.Track, 1);
                g.DrawString(L.T("PROTOCOL HIERARCHY (BY PACKETS)"), Draw.fTiny, muted, c1.X + 14 * S, c1.Y + 12 * S); Hint(liveCanvas, new RectangleF(c1.X, c1.Y, c1.Width, 26 * S), "live.PROTOCOL HIERARCHY");
                float yy = c1.Y + 30 * S;
                foreach (var r in v.Phs)
                {
                    int depth = int.Parse(r[0], CultureInfo.InvariantCulture); float pct = float.Parse(r[2].TrimEnd('%'), System.Globalization.CultureInfo.CurrentCulture);
                    g.DrawString(r[1], Draw.fMonoS, depth == 0 ? muted : ink, c1.X + 14 * S + depth * 12 * S, yy);
                    var tr = new RectangleF(c1.Right - 190 * S, yy + 5 * S, 110 * S, 7 * S); Theme.Fill(g, tr, 3.5f * S, Theme.Track);
                    Theme.Fill(g, new RectangleF(tr.X, tr.Y, Math.Max(4 * S, tr.Width * pct / 100f), tr.Height), 3.5f * S, depth <= 1 ? Theme.Blue : depth == 2 ? Theme.C("#8250DF") : Theme.Orange);
                    var ps = g.MeasureString(r[2], Draw.fMonoS); g.DrawString(r[2], Draw.fMonoS, ink, c1.Right - 14 * S - ps.Width, yy);
                    yy += 20 * S;
                }
                cardH = c1.Height;
                // expert info
                var c3 = new RectangleF(x + colW + 24 * S, y, colW, 34 * S + v.Expert.Count * 20 * S);
                Theme.Stroke(g, c3, 10 * S, Theme.Track, 1);
                g.DrawString(L.T("EXPERT INFO (WIRESHARK)"), Draw.fTiny, muted, c3.X + 14 * S, c3.Y + 12 * S); Hint(liveCanvas, new RectangleF(c3.X, c3.Y, c3.Width, 26 * S), "live.EXPERT INFO");
                yy = c3.Y + 30 * S;
                foreach (var e in v.Expert)
                {
                    Draw.LvDot(g, int.Parse(e[0], CultureInfo.InvariantCulture), c3.X + 14 * S, yy + 4 * S, 9 * S, 9 * S);
                    g.DrawString(e[1], Draw.fBody, ink, c3.X + 30 * S, yy); Hint(liveCanvas, new RectangleF(c3.X, yy, c3.Width, 20 * S), e[1]);
                    var ns = g.MeasureString(e[2], Draw.fMono); g.DrawString(e[2], Draw.fMono, ink, c3.Right - 14 * S - ns.Width, yy);
                    yy += 20 * S;
                }
                cardH = Math.Max(cardH, c3.Height);
                y += cardH + 14 * S;
                // bytes per app + call quality
                var c4 = new RectangleF(x, y, colW, 34 * S + Math.Max(1, v.ByApp.Count) * 20 * S);
                Theme.Stroke(g, c4, 10 * S, Theme.Track, 1);
                g.DrawString(L.T("BYTES PER APP (SINCE START)"), Draw.fTiny, muted, c4.X + 14 * S, c4.Y + 12 * S); Hint(liveCanvas, new RectangleF(c4.X, c4.Y, c4.Width, 26 * S), "live.BY APP");
                yy = c4.Y + 30 * S;
                if (v.ByApp.Count == 0) g.DrawString(L.T("-"), Draw.fBody, muted, c4.X + 14 * S, yy);
                foreach (var a in v.ByApp)
                {
                    g.DrawString(a[0], Draw.fBody, ink, new RectangleF(c4.X + 14 * S, yy, colW * 0.4f, 18 * S), nowrap);
                    float pct = float.Parse(a[3], CultureInfo.CurrentCulture);
                    var tr = new RectangleF(c4.X + colW * 0.42f, yy + 6 * S, colW * 0.25f, 7 * S); Theme.Fill(g, tr, 3.5f * S, Theme.Track);
                    Theme.Fill(g, new RectangleF(tr.X, tr.Y, Math.Max(4 * S, tr.Width * pct / 100f), tr.Height), 3.5f * S, Theme.Blue);
                    string t = a[1] + "  " + a[2] + " Mb/s"; var ts2 = g.MeasureString(t, Draw.fMonoS); g.DrawString(t, Draw.fMonoS, ink, c4.Right - 14 * S - ts2.Width, yy + 1 * S);
                    yy += 20 * S;
                }
                var c5 = new RectangleF(x + colW + 24 * S, y, colW, 34 * S + Math.Max(2, v.Calls.Count) * 20 * S);
                Theme.Stroke(g, c5, 10 * S, Theme.Track, 1);
                g.DrawString(L.T("CALL QUALITY (TEAMS AND OTHER CALLS)"), Draw.fTiny, muted, c5.X + 14 * S, c5.Y + 12 * S); Hint(liveCanvas, new RectangleF(c5.X, c5.Y, c5.Width, 26 * S), "live.CALL QUALITY");
                yy = c5.Y + 30 * S;
                if (!v.Extras) g.DrawString(L.T("Needs a newer Wireshark (3.0 or later)."), Draw.fBody, muted, new RectangleF(c5.X + 14 * S, yy, colW - 28 * S, 40 * S));
                else if (v.Calls.Count == 0) g.DrawString(Voice.P("No call running. Start a Teams call and I'll grade it live.", "No call media seen yet."), Draw.fBody, muted, new RectangleF(c5.X + 14 * S, yy, colW - 28 * S, 40 * S));
                foreach (var cl in v.Calls)
                {
                    int lv = int.Parse(cl[6], CultureInfo.InvariantCulture);
                    Draw.LvDot(g, lv, c5.X + 14 * S, yy + 4 * S, 9 * S, 9 * S);
                    g.DrawString(cl[1] + " " + cl[0], Draw.fBody, ink, new RectangleF(c5.X + 30 * S, yy, colW * 0.42f, 18 * S), nowrap);
                    string t = lv == 3 ? cl[2] + " packets - too short to judge" : "loss " + cl[3] + " · jitter " + cl[4] + " · MOS " + cl[5]; var ts3 = g.MeasureString(t, Draw.fMonoS);
                    using (var tb = new SolidBrush(Theme.LvText(lv))) g.DrawString(t, Draw.fMonoS, tb, c5.Right - 14 * S - ts3.Width, yy + 1 * S);
                    Hint(liveCanvas, new RectangleF(c5.X, yy, c5.Width, 20 * S), "live.MOS");
                    yy += 20 * S;
                }
                y += Math.Max(c4.Height, c5.Height) + 14 * S;
                // conversations
                var c2 = new RectangleF(x, y, w, 34 * S + Math.Max(1, v.Top.Count) * 36 * S);
                Theme.Stroke(g, c2, 10 * S, Theme.Track, 1);
                g.DrawString(L.T("TOP CONVERSATIONS · NAMED FROM TLS SERVER NAME / DNS IN THE TRAFFIC"), Draw.fTiny, muted, c2.X + 14 * S, c2.Y + 12 * S); Hint(liveCanvas, new RectangleF(c2.X, c2.Y, c2.Width, 26 * S), "live.TOP CONVERSATIONS");
                yy = c2.Y + 30 * S;
                if (v.Top.Count == 0) g.DrawString(Voice.P("Quiet out there.", "No traffic yet."), Draw.fBody, muted, c2.X + 14 * S, yy);
                foreach (var c in v.Top)
                {
                    g.DrawString(c.Host, Draw.fBodyB, ink, new RectangleF(c2.X + 14 * S, yy, w * 0.55f, 18 * S), nowrap);
                    g.DrawString(c.Proto + "   " + c.Remote + ":" + c.RemotePort, Draw.fMonoS, muted, new RectangleF(c2.X + 14 * S, yy + 17 * S, w * 0.6f, 16 * S), nowrap);
                    string rate = c.Rate >= 0.05 ? c.Rate.ToString("0.0", CultureInfo.CurrentCulture) + " Mb/s" : (c.Rate * 1000).ToString("0", CultureInfo.CurrentCulture) + " kb/s";
                    var rs = g.MeasureString(rate, Draw.fMono); g.DrawString(rate, Draw.fMono, ink, c2.Right - 130 * S - rs.Width, yy + 6 * S);
                    string flag = c.Retx > 0 ? c.Retx + " retrans" : "clean";
                    using (var fb = new SolidBrush(c.Retx > 0 ? Theme.LvText(1) : Theme.LvText(0))) { var fs = g.MeasureString(flag, Draw.fSmall); g.DrawString(flag, Draw.fSmall, fb, c2.Right - 14 * S - fs.Width, yy + 7 * S); }
                    yy += 36 * S;
                }
                y += c2.Height + 20 * S;
            }
            var need = new Size(0, (int)(y + 20 * S));
            SetNeed(liveCanvas, need);
        }
    }
}
