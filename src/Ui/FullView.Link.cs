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
        // ---------- Your connection
        void RefreshLink(bool now)
        {
            if (linkBusy || (!now && (DateTime.UtcNow - lastLink).TotalSeconds < 10)) return;
            linkBusy = true; lastLink = DateTime.UtcNow;
            Task.Factory.StartNew(() =>
            {
                if (IsDisposed) { linkBusy = false; return; }
                LinkSnap ls = null; WifiAdvisor.Extra ex = null;
                try { ls = LinkInfo.Collect(M); ex = WifiAdvisor.Fill(ls, M); } catch (Exception e) { Program.Log("link: " + e.Message); }
                if (IsDisposed || !IsHandleCreated) { linkBusy = false; return; }
                try { BeginInvoke((Action)delegate { linkBusy = false; if (ls != null && !IsDisposed) { linkSnap = ls; linkExtra = ex; linkCanvas.Invalidate(); } }); } catch { linkBusy = false; }
            });
        }
        float RowsCard(Graphics g, string title, List<string[]> rows, RectangleF cr, string keyPrefix)
        {
            Theme.Stroke(g, cr, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(title, Draw.fTiny, mb, cr.X + 14 * S, cr.Y + 12 * S);
            Hint(linkCanvas, new RectangleF(cr.X, cr.Y, cr.Width, 26 * S), keyPrefix + title);
            float ry = cr.Y + 34 * S, lw = Math.Min(170 * S, cr.Width * 0.42f);
            using (var muted = new SolidBrush(Theme.Muted))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                foreach (var row in rows)
                {
                    g.DrawString(row[0], Draw.fBody, muted, new RectangleF(cr.X + 14 * S, ry, lw, 20 * S), nowrap);
                    using (var vb = new SolidBrush(row.Length > 2 && row[2] != "0" ? Theme.LvText(int.Parse(row[2], CultureInfo.InvariantCulture)) : Theme.Ink))
                        g.DrawString(row[1], Draw.fBodyB, vb, new RectangleF(cr.X + 14 * S + lw, ry, cr.Width - lw - 28 * S, 20 * S), nowrap);
                    Hint(linkCanvas, new RectangleF(cr.X, ry - 2 * S, cr.Width, 22 * S), "link." + row[0]);
                    ry += 22 * S;
                }
            return ry;
        }
        void PaintLink(Graphics g)
        {
            g.TranslateTransform(linkCanvas.AutoScrollPosition.X, linkCanvas.AutoScrollPosition.Y);
            Begin(linkCanvas);
            float x = 14 * S, y = 6 * S, w = linkCanvas.ClientSize.Width - 28 * S;
            var ls = linkSnap;
            if (ls == null) { using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Voice.P("Asking Windows about your connection...", "Reading..."), Draw.fBody, mb, x, y + 10 * S); return; }
            string title = (ls.Wifi ? "YOUR WI-FI" : "YOUR CABLE") + "  ·  " + Draw.Trunc(ls.Adapter, 50) + "  ·  UPDATED " + ls.At.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            float vh = Draw.Measure(ls.Verdict, Draw.fTitle, (int)(w - 32 * S)).Height;
            var vb = new RectangleF(x, y, w, vh + 44 * S);
            Theme.Fill(g, vb, 12 * S, Theme.LvSoft(ls.Level));
            using (var b = new SolidBrush(Theme.LvText(ls.Level))) { g.DrawString(title, Draw.fTiny, b, x + 16 * S, y + 10 * S); g.DrawString(ls.Verdict, Draw.fTitle, b, new RectangleF(x + 16 * S, y + 26 * S, w - 32 * S, vh + 4)); }
            y += vb.Height + 16 * S;
            if (ls.Radio.Count + ls.Address.Count + ls.Traffic.Count == 0) return;   // nothing connected: the message says it all
            float cw = (w - 14 * S) / 2;
            Func<List<string[]>, float> hOf = rows => 44 * S + rows.Count * 22 * S;
            float h1 = Math.Max(hOf(ls.Radio), hOf(ls.Quality)), h2 = Math.Max(hOf(ls.Address), hOf(ls.Traffic));
            RowsCard(g, ls.Wifi ? "RADIO" : "CABLE", ls.Radio, new RectangleF(x, y, cw, h1), "link.");
            RowsCard(g, "LINK TO YOUR ROUTER", ls.Quality, new RectangleF(x + cw + 14 * S, y, cw, h1), "link."); y += h1 + 14 * S;
            RowsCard(g, "ADDRESSES", ls.Address, new RectangleF(x, y, cw, h2), "link.");
            RowsCard(g, "TRAFFIC SINCE THE ADAPTER STARTED", ls.Traffic, new RectangleF(x + cw + 14 * S, y, cw, h2), "link."); y += h2 + 18 * S;

            if (ls.Wifi) y = PaintWifiExtras(g, ls, x, y, w);
            if (!ls.Wifi) y = PaintSwitch(g, x, y, w);
            if (ls.Wifi) y = ListCard(g, "WI-FI DROPS IN THE LAST 24 HOURS  ·  FROM WINDOWS' OWN LOG", ls.Drops, ls.DropsErr != "" ? ls.DropsErr : Voice.P("Not a single drop. Rock solid.", "No drops in 24 h."), "link.DROPS", x, y, w);
            y = ListCard(g, "ADDRESS (DHCP) TROUBLE IN THE LAST 24 HOURS", ls.Dhcp, ls.DhcpErr != "" ? ls.DhcpErr : "None - your address renewed without a hitch.", "link.DHCP", x, y, w);
            y = ListCard(g, "DEVICES ON YOUR NETWORK  ·  THE ONES YOUR PC HAS TALKED TO (NOTHING IS SCANNED)", ls.Lan.Select(r => new[] { r[0], r[1] + (r[2] != "" ? "  ·  " + r[2] : ""), r[2] == "your router" ? "-1" : "0" }).ToList(), "None in Windows' table yet.", "link.LAN", x, y, w);
            if (ls.Wifi && ls.MyChannel > 0 && ls.Aps.Count > 0) y = PaintChannelMap(g, ls, x, y, w);
            if (ls.Wifi && ls.Aps.Count == 0 && !ls.LocationBlocked)
                using (var mb = new SolidBrush(Theme.Muted)) { g.DrawString(L.T("Windows didn't list any neighbouring networks this time - it refreshes its scan every minute or so."), Draw.fBody, mb, x, y); y += 30 * S; }
            if (ls.Wifi) y = PaintWars(g, x, y, w);
            if (!ls.Wifi)
            {
                string tip = Voice.P("Cable is the gold standard: no interference, no roaming, no neighbours. If calls still stutter, the problem is further out - check a destination.", "Wired links have no radio interference; look further along the path if problems persist.");
                var th = Draw.Measure(tip, Draw.fBody, (int)w).Height;
                using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(tip, Draw.fBody, sb, new RectangleF(x, y, w, th + 4)); y += th + 20 * S;
            }
            var need = new Size(0, (int)(y + 20 * S)); SetNeed(linkCanvas, need);
        }
        float ListCard(Graphics g, string title, List<string[]> rows, string empty, string key, float x, float y, float w)
        {
            float rh = 22 * S; var card = new RectangleF(x, y, w, 40 * S + Math.Max(1, rows.Count) * rh);
            Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(title, Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(linkCanvas, new RectangleF(x, y, w, 26 * S), key);
            float ry = y + 34 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (rows.Count == 0) g.DrawString(empty, Draw.fBody, muted, new RectangleF(x + 14 * S, ry, w - 28 * S, 20 * S), nowrap);
                foreach (var r in rows)
                {
                    int lv = r.Length > 2 ? int.Parse(r[2], CultureInfo.InvariantCulture) : 0;
                    using (var d = new SolidBrush(lv < 0 ? Theme.Blue : Theme.Lv(lv))) g.FillEllipse(d, x + 14 * S, ry + 6 * S, 8 * S, 8 * S);
                    g.DrawString(r[0], Draw.fBodyB, ink, new RectangleF(x + 30 * S, ry, w * 0.30f, 20 * S), nowrap);
                    g.DrawString(r[1], Draw.fBody, muted, new RectangleF(x + 30 * S + w * 0.31f, ry, w * 0.69f - 44 * S, 20 * S), nowrap);
                    ry += rh;
                }
            }
            return y + card.Height + 14 * S;
        }
        float PaintSwitch(Graphics g, float x, float y, float w)
        {
            var card = new RectangleF(x, y, w, switchInfo != null ? 150 * S : 86 * S);
            Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("YOUR SWITCH PORT  ·  LLDP / CDP, LISTENED FOR PASSIVELY"), Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(linkCanvas, new RectangleF(x, y, w, 26 * S), "link.SWITCH");
            var btn = new RectangleF(x + 14 * S, y + 36 * S, 210 * S, 32 * S);
            bool can = LiveCapture.Available && !switchBusy;
            Theme.Fill(g, btn, 16 * S, can ? Theme.Blue : Theme.Track);
            string bt = switchBusy ? "Listening... " + (int)(DateTime.UtcNow - switchStart).TotalSeconds + " s" : "Find my switch port";
            using (var bb = new SolidBrush(can ? Color.White : Theme.Muted)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(bt, Draw.fBodyB, bb, btn, c);
            if (can) Act(linkCanvas, btn, StartSwitch);
            string note = !LiveCapture.Available ? "Needs Wireshark and Npcap on this PC." : switchErr != "" ? switchErr : "Switches announce themselves every 30-60 s. I listen for up to 65 s - nothing is sent.";
            using (var nb = new SolidBrush(switchErr != "" ? Theme.LvText(1) : Theme.Muted)) g.DrawString(note, Draw.fSmall, nb, new RectangleF(btn.Right + 14 * S, btn.Y, w - btn.Width - 56 * S, 34 * S));
            var si = switchInfo;
            if (si != null)
            {
                float ry = y + 82 * S;
                var rows = new[] { new[] { "Switch", si.Name + (si.Mgmt != "" ? "  (" + si.Mgmt + ")" : "") }, new[] { "Port", si.Port + (si.PortDesc != "" && si.PortDesc != si.Port ? "  ·  " + si.PortDesc : "") }, new[] { "VLAN", si.Vlan != "" ? si.Vlan : "not announced" }, new[] { "Model", (si.Model != "" ? si.Model : "-") + "  ·  via " + si.Proto } };
                using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    for (int i = 0; i < rows.Length; i++)
                    {
                        float cx = x + 14 * S + (i % 2) * (w / 2), cy = ry + (i / 2) * 26 * S;
                        g.DrawString(rows[i][0], Draw.fBody, muted, cx, cy);
                        g.DrawString(rows[i][1], Draw.fBodyB, ink, new RectangleF(cx + 70 * S, cy, w / 2 - 100 * S, 20 * S), nowrap);
                    }
            }
            return y + card.Height + 14 * S;
        }
        void StartSwitch()
        {
            if (switchBusy) return;
            string iface; lock (M.L) iface = M.IfaceId;
            switchBusy = true; switchErr = ""; switchInfo = null; switchStart = DateTime.UtcNow; linkCanvas.Invalidate();
            Task.Factory.StartNew(() =>
            {
                string err; var si = LiveCapture.FindSwitch(iface, 65, out err);
                try { BeginInvoke((Action)delegate { switchBusy = false; switchInfo = si; switchErr = err; linkCanvas.Invalidate(); }); } catch { switchBusy = false; }
            }, TaskCreationOptions.LongRunning);
        }
        static readonly string[] Legend = { "your channel", "overlaps yours", "other channels (bar = strongest signal, number = access points)" };
        float PaintChannelMap(Graphics g, LinkSnap ls, float x, float y, float w)
        {
            string band = ls.MyBand;
            var inBand = ls.Aps.Where(a => a.Band == band && a.Channel > 0).ToList();
            var chans = inBand.Select(a => a.Channel).Concat(new[] { ls.MyChannel }).Distinct().OrderBy(c => c).ToList();
            float ch = 170 * S; var card = new RectangleF(x, y, w, ch + 70 * S);
            Theme.Stroke(g, card, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString("WHO ELSE IS ON THE AIR  ·  " + band.ToUpperInvariant() + "  ·  " + inBand.Count + " ACCESS POINTS NEARBY", Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(linkCanvas, new RectangleF(x, y, w, 26 * S), "link.CHANNEL MAP");
            float plotX = x + 20 * S, plotW = w - 40 * S, base0 = y + 34 * S + ch - 30 * S, colW = Math.Min(64 * S, plotW / Math.Max(1, chans.Count));
            using (var axis = new Pen(Theme.Line)) g.DrawLine(axis, plotX, base0, plotX + plotW, base0);
            using (var muted = new SolidBrush(Theme.Muted)) using (var center = new StringFormat { Alignment = StringAlignment.Center })
                for (int i = 0; i < chans.Count; i++)
                {
                    int c = chans[i]; var here = inBand.Where(a => a.Channel == c).ToList();
                    bool mine = c == ls.MyChannel, overlap = !mine && LinkInfo.Overlaps(c, ls.MyChannel, band);
                    int best = here.Count > 0 ? here.Max(a => a.Signal) : 0;
                    float cx = plotX + i * colW + colW / 2, bh = Math.Max(4 * S, (ch - 50 * S) * best / 100f);
                    var bar = new RectangleF(cx - colW * 0.3f, base0 - bh, colW * 0.6f, bh);
                    Theme.Fill(g, bar, 5 * S, mine ? Theme.Blue : overlap ? Theme.Amber : Theme.Rail);
                    g.DrawString(here.Count.ToString(CultureInfo.CurrentCulture), Draw.fSmall, muted, new RectangleF(cx - colW / 2, bar.Y - 17 * S, colW, 16 * S), center);
                    using (var lb = new SolidBrush(mine ? Theme.Blue : Theme.Ink)) g.DrawString(c.ToString(CultureInfo.CurrentCulture), mine ? Draw.fBodyB : Draw.fSmall, lb, new RectangleF(cx - colW / 2, base0 + 4 * S, colW, 18 * S), center);
                    Hint(linkCanvas, new RectangleF(cx - colW / 2, y + 30 * S, colW, ch), "link.CHANNEL MAP");
                }
            float ly = y + ch + 36 * S, lx = x + 20 * S;
            var colors = new[] { Theme.Blue, Theme.Amber, Theme.Rail };
            using (var muted = new SolidBrush(Theme.Muted))
                for (int i = 0; i < 3; i++)
                { Theme.Fill(g, new RectangleF(lx, ly + 3 * S, 10 * S, 10 * S), 3 * S, colors[i]); g.DrawString(Legend[i], Draw.fSmall, muted, lx + 16 * S, ly); lx += g.MeasureString(Legend[i], Draw.fSmall).Width + 34 * S; }
            y += card.Height + 16 * S;
            // which channel is least crowded, and networks that share a name but not their security
            var tips = new List<string[]>();
            var lc = WlanApi.LeastCrowded(ls.Aps, band, ls.MyChannel, ls.MyBssid);
            if (lc != null)
            {
                if (lc[0] == ls.MyChannel || lc[2] - lc[1] < 15) tips.Add(new[] { "Least crowded", "You're already on the quietest channel there is (" + ls.MyChannel + ") - changing won't help.", "0" });
                else tips.Add(new[] { "Least crowded", "Channel " + lc[0] + " is the quietest in " + band + " (crowding " + lc[1] + " vs " + lc[2] + " on yours). If this is your own router, set it to " + lc[0] + " in its Wi-Fi settings - or leave it on Auto and restart it so it picks again.", "1" });
            }
            foreach (var grp in ls.Aps.Where(a => a.Sec != "" && a.Ssid != "" && a.Ssid != "(hidden network)").GroupBy(a => a.Ssid))
            {
                var secs = grp.Select(a => a.Sec).Distinct().ToList();
                if (secs.Count > 1 && secs.Any(WlanApi.Weak) && secs.Any(z => !WlanApi.Weak(z)))
                    tips.Add(new[] { "Same name, different lock", "\"" + grp.Key + "\" is on the air both as " + string.Join(" and as ", secs) + ". The " + secs.First(WlanApi.Weak) + " one could be a fake copy - join the protected one, and don't sign in on a page you didn't expect.", "2" });
            }
            if (tips.Count > 0) y = TextCard(g, "WHAT TO DO ABOUT IT", tips, "link.CHANNEL MAP", x, y, w);

            var top = ls.Aps.OrderByDescending(a => a.Signal).Take(12).ToList();
            float rh = 24 * S; var card2 = new RectangleF(x, y, w, 40 * S + top.Count * rh);
            Theme.Stroke(g, card2, 10 * S, Theme.Track, 1);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("STRONGEST NEIGHBOURS"), Draw.fTiny, mb, x + 14 * S, y + 12 * S);
            Hint(linkCanvas, new RectangleF(x, y, w, 26 * S), "link.NEIGHBOURS");
            float ry = y + 34 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    Func<Ap, string> facts = a => a.Signal + "%" + (a.Rssi < 0 ? " · " + a.Rssi + " dBm" : "") + (a.Width > 20 ? " · " + a.Width + " MHz" : "") + (a.Util >= 0 ? " · air " + a.Util + "%" : "") + (a.Sec != "" ? " · " + a.Sec : "");
                    float factW = top.Count == 0 ? 0 : top.Max(a => g.MeasureString(facts(a), Draw.fSmall).Width) + 6 * S;
                    float nameW = Math.Min(w * 0.30f, Math.Max(120 * S, top.Count == 0 ? 0 : top.Max(a => g.MeasureString(a.Ssid + "  (you)", Draw.fBodyB).Width) + 10 * S));
                    float bandW = Math.Min(w * 0.28f, top.Count == 0 ? 0 : top.Max(a => g.MeasureString(a.Band + " · ch " + a.Channel + (a.Radio != "" ? " · " + LinkInfo.Gen(a.Radio) : ""), Draw.fSmall).Width) + 16 * S);
                    float left = x + 30 * S, bandX = left + nameW, bx = bandX + bandW, bw = Math.Max(40 * S, Math.Min(w * 0.22f, x + w - 14 * S - factW - 10 * S - bx));
                    foreach (var a in top)
                    {
                        bool me = string.Equals(a.Bssid, ls.MyBssid, StringComparison.OrdinalIgnoreCase);
                        bool clash = !me && a.Band == band && LinkInfo.Overlaps(a.Channel, ls.MyChannel, band);
                        using (var d = new SolidBrush(me ? Theme.Blue : clash ? Theme.Amber : Theme.Grey)) g.FillEllipse(d, x + 14 * S, ry + 6 * S, 8 * S, 8 * S);
                        g.DrawString(a.Ssid + (me ? "  (you)" : ""), me ? Draw.fBodyB : Draw.fBody, ink, new RectangleF(left, ry, nameW - 6 * S, 20 * S), nowrap);
                        g.DrawString(a.Band + " · ch " + a.Channel + (a.Radio != "" ? " · " + LinkInfo.Gen(a.Radio) : ""), Draw.fSmall, muted, new RectangleF(bandX, ry + 2 * S, bandW - 6 * S, 18 * S), nowrap);
                        Theme.Fill(g, new RectangleF(bx, ry + 7 * S, bw, 6 * S), 3 * S, Theme.Track);
                        Theme.Fill(g, new RectangleF(bx, ry + 7 * S, bw * Math.Max(0, a.Signal) / 100f, 6 * S), 3 * S, me ? Theme.Blue : clash ? Theme.Amber : Theme.Rail);
                        g.DrawString(facts(a), Draw.fSmall, muted, new RectangleF(bx + bw + 10 * S, ry + 2 * S, x + w - 14 * S - (bx + bw + 10 * S), 18 * S), nowrap);
                        ry += rh;
                    }
                }
            return y + card2.Height + 16 * S;
        }
            // ---------- neighbour wars: 7 days of who is on your air (read in the background, at most every 10 minutes)
        AirLog.Wars wars; DateTime warsAt = DateTime.MinValue; volatile bool warsBusy;
        float PaintWars(Graphics g, float x, float y, float w)
        {
            if (!warsBusy && (DateTime.UtcNow - warsAt).TotalMinutes > 10)
            {
                warsBusy = true; warsAt = DateTime.UtcNow;
                Task.Factory.StartNew(() => { AirLog.Wars r = null; try { r = AirLog.Analyse(AirLog.Load()); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("wars: " + e.Message); } try { BeginInvoke((Action)delegate { warsBusy = false; wars = r; linkCanvas.Invalidate(); }); } catch (InvalidOperationException) { warsBusy = false; } });
            }
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("NEIGHBOUR WARS  ·  7 DAYS ON YOUR AIR"), Draw.fTiny, mb, x, y);
            Hint(linkCanvas, new RectangleF(x, y - 2 * S, w, 18 * S), "link.WARS"); y += 22 * S;
            var wr = wars;
            if (!Cfg.History) return Lead(g, "Turn on History in Settings and AdeeboMeter writes down who is on your air once an hour (the list Windows already has - no extra scanning), kept 7 days on this PC.", x, y, w, true);
            if (wr == null || wr.Nets.Count == 0 || wr.MyChannel <= 0) return Lead(g, "Nothing recorded yet - the first reading comes within the hour (Windows' location permission for desktop apps is needed to see neighbours).", x, y, w, true);
            var days = wr.DaysSeen.Skip(Math.Max(0, wr.DaysSeen.Count - 7)).ToList();
            float nameW = Math.Min(240 * S, w * 0.3f), infoW = Math.Min(230 * S, w * 0.28f), dayW = Math.Max(30 * S, (w - nameW - infoW) / Math.Max(1, days.Count));
            using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub)) using (var amb = new SolidBrush(Theme.LvText(1))) using (var c = new StringFormat { Alignment = StringAlignment.Center })
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                for (int i = 0; i < days.Count; i++) g.DrawString(days[i].ToString("ddd", CultureInfo.CurrentCulture), Draw.fTiny, muted, new RectangleF(x + nameW + infoW + i * dayW, y, dayW, 16 * S), c);
                y += 20 * S; int rank = 0;
                foreach (var n in wr.Nets.Take(10))
                {
                    rank++; bool share = !n.Mine && !n.Same && AirLog.Overlap(n.Channel, n.Width, wr.MyChannel, wr.MyWidth, wr.MyBand);
                    using (var nb = new SolidBrush(n.Mine ? Theme.Blue : share ? Theme.LvText(1) : Theme.Ink)) g.DrawString(rank + "  " + n.Name + (n.Mine ? "  (" + L.T("you") + ")" : ""), n.Mine ? Draw.fBodyB : Draw.fBody, nb, new RectangleF(x, y, nameW - 6 * S, 20 * S), nowrap);
                    g.DrawString(n.Rssi.ToString("0", CultureInfo.CurrentCulture) + " dBm · ch " + n.Channel + (n.Width > 20 ? " · " + n.Width + " MHz" : "") + (n.Moved != "" ? " · " + L.T("moved") + " " + n.Moved : ""), Draw.fSmall, share ? amb : sub, new RectangleF(x + nameW, y + 2 * S, infoW - 6 * S, 18 * S), nowrap);
                    for (int i = 0; i < days.Count; i++)
                    {
                        string win; bool won = wr.Winner.TryGetValue(days[i], out win) && (n.Mine ? win == "you" : win == n.Name);
                        int ch, myCh; bool here = n.ChByDay.TryGetValue(days[i], out ch); bool onMine = here && wr.MyChByDay.TryGetValue(days[i], out myCh) && (n.Mine || AirLog.Overlap(ch, n.WidthByDay[days[i]], myCh, wr.MyWidth, wr.MyBand));
                        var cr = new RectangleF(x + nameW + infoW + i * dayW + dayW / 2 - 7 * S, y + 3 * S, 14 * S, 14 * S);
                        if (won) using (var wb = new SolidBrush(n.Mine ? Theme.Green : Theme.Amber)) g.FillEllipse(wb, cr);
                        else if (onMine) using (var p = new Pen(n.Mine ? Theme.Green : Theme.Amber, 1.5f * S)) g.DrawEllipse(p, cr);
                        else if (here) using (var db = new SolidBrush(Theme.Track)) g.FillEllipse(db, cr.X + 4 * S, cr.Y + 4 * S, 6 * S, 6 * S);
                    }
                    y += 24 * S;
                }
                // your channel's crowding, day by day
                g.DrawString(L.T("Crowding on your channel"), Draw.fSmall, muted, x, y + 4 * S);
                double cm = Math.Max(10, wr.Crowding.Count > 0 ? wr.Crowding.Values.Max() : 10);
                for (int i = 0; i < days.Count; i++)
                {
                    double v; wr.Crowding.TryGetValue(days[i], out v); float bh = (float)(v / cm) * 22 * S;
                    Theme.Fill(g, new RectangleF(x + nameW + infoW + i * dayW + dayW / 2 - 8 * S, y + 26 * S - bh, 16 * S, Math.Max(2, bh)), 3 * S, v > cm * 0.6 ? Theme.Amber : Theme.Rail);
                }
                y += 34 * S;
                g.DrawString(L.T("filled = strongest on your channel that day · ring = on your channel · dot = heard here"), Draw.fTiny, muted, x, y); y += 18 * S;
            }
            return Lead(g, wr.Verdict, x, y + 4 * S, w, false);
        }
    }
}
