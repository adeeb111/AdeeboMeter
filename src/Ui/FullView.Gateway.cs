// Home as a gateway (1.1.6, 21 Nov): big icons. The path in the verdict card, the watch warnings in Needs attention
// (a service-board host down 3 checks in a row, a certificate inside 30 days), the go-to tiles with a live line each,
// and "See this moment in the Time machine".
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    sealed partial class FullView
    {
        // ---------- the Time machine at a minute
        string replayFrom = "", replayBack = "";   // "Opened from Home - nas.local:445 down" and the page to go back to
        internal void OpenTimeAt(DateTime t, string from)
        {
            replayFrom = from ?? ""; replayBack = tab;
            replayDay = t.Date; replayData = null; tab = "replay"; ShowTab(); SyncNav();
            ReplayAt(t.TimeOfDay.TotalSeconds / 86400.0);
        }
        // the link under an event: "See 14:05 in the Time machine", or why it can't
        float TimeLink(Graphics g, Control c, DateTime t, string from, float x, float y)
        {
            string s = Cfg.History ? L.T("See") + " " + t.ToString("HH:mm", CultureInfo.CurrentCulture) + " " + L.T("in the Time machine") + "  ›" : L.T("History is off, so there's no replay of this minute (Settings › General › History).");
            var sz = g.MeasureString(s, Draw.fSmall);
            using (var b = new SolidBrush(Cfg.History ? Theme.Blue : Theme.Sub)) g.DrawString(s, Draw.fSmall, b, x, y);
            if (Cfg.History) Act(c, new RectangleF(x, y - 2 * S, sz.Width, sz.Height + 4 * S), delegate { OpenTimeAt(t, from); });
            return x + sz.Width;
        }

        // ---------- the path, as big icons: this PC · Wi-Fi or cable · your router · the internet · name lookups
        static int LvMs(double v, double amber, double red) { return double.IsNaN(v) ? 2 : v > red ? 2 : v > amber ? 1 : 0; }
        float PathStrip(Graphics g, float x, float y, float w)
        {
            string kind; int sig; lock (M.L) { kind = M.AdapterKind; sig = M.WifiSignal; }
            bool wifi = kind == "Wi-Fi", cell = kind == "Cellular" || kind == "Mobile";
            double gw = M.GwPing.Avg(6), net = M.NetPing.Avg(6), dns = M.Dns.Avg(6);
            var stops = new[] {
                new object[] { "pc", L.T("This PC"), 0 },
                new object[] { wifi ? "wifi" : cell ? "tower" : "switch", wifi ? (sig >= 0 ? sig + "%" : "Wi-Fi") : cell ? "4G/5G" : L.T("Cable"), wifi && sig >= 0 && sig < 40 ? 1 : 0 },
                new object[] { "cpe", L.T("Router"), LvMs(gw, 15, 50) },
                new object[] { "internet", L.T("Internet"), LvMs(net, 80, 150) },
                new object[] { "dns", L.T("Names"), double.IsNaN(dns) ? 3 : dns > 300 ? 1 : 0 } };
            float ic = 34 * S, gap = Math.Min(110 * S, (w - stops.Length * ic) / (stops.Length - 1));
            for (int i = 0; i < stops.Length; i++)
            {
                float ix = x + i * (ic + gap); int lv = (int)stops[i][2];
                Color col = lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink;
                if (i > 0)   // the link into this stop takes its colour: where the path goes amber or red is where it hurts
                    using (var p = new Pen(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Line, (lv == 1 || lv == 2 ? 3 : 2) * S))
                    {
                        if (lv == 2) p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        g.DrawLine(p, ix - gap + 4 * S, y + ic / 2, ix - 4 * S, y + ic / 2);
                    }
                Glyphs.Draw(g, (string)stops[i][0], new RectangleF(ix, y, ic, ic), col, 1.8f);
                using (var b = new SolidBrush(Theme.Sub)) using (var cf = new StringFormat { Alignment = StringAlignment.Center })
                    g.DrawString((string)stops[i][1], Draw.fSmall, b, new RectangleF(ix - 30 * S, y + ic + 2 * S, ic + 60 * S, 18 * S), cf);
            }
            return ic + 24 * S;
        }

        // ---------- watch warnings: things AdeeboMeter keeps an eye on for you, not only what happened in the last 2 hours
        float PaintWatch(Graphics g, float x, float y, float w, out int count)
        {
            var down = Sectors.DownNow(3); DateTime certAt; var certs = Sectors.CertsDue(30, out certAt);
            count = down.Count + certs.Count; if (count == 0) return y;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                foreach (var d in down.Take(3))
                {
                    var host = d.Item1; var since = d.Item2;
                    var rr = new RectangleF(x, y, w, 62 * S); Theme.Fill(g, rr, 12 * S, Theme.LvSoft(2));
                    Glyphs.Tile(g, "board", new RectangleF(x + 10 * S, y + 11 * S, 40 * S, 40 * S), Theme.LvText(2), 10 * S);
                    string t = host + " " + L.T("is down") + " - " + d.Item3 + " " + L.T("checks in a row since") + " " + since.ToString(since.Date == DateTime.Today ? "HH:mm" : "d MMM HH:mm", CultureInfo.CurrentCulture);
                    g.DrawString(t, Draw.fBodyB, ink, new RectangleF(x + 62 * S, y + 9 * S, w - 72 * S, 20 * S), nowrap);
                    float lx = x + 62 * S, ly = y + 36 * S;
                    using (var bl = new SolidBrush(Theme.Blue))
                    {
                        string sp = L.T("Show my path") + "  ›"; g.DrawString(sp, Draw.fSmall, bl, lx, ly);
                        var sz = g.MeasureString(sp, Draw.fSmall); string h = host;
                        Act(overviewCanvas, new RectangleF(lx, ly - 2 * S, sz.Width, sz.Height + 4 * S), delegate { OpenLab(LabLink.PathJson(M, "server", h, h, double.NaN, M.GwPing.Avg(6), M.NetPing.Avg(6)), false); });
                        lx += sz.Width + 18 * S;
                    }
                    lx = TimeLink(g, overviewCanvas, since, L.T("Home") + " - " + host + " " + L.T("down"), lx, ly) + 18 * S;
                    using (var bl = new SolidBrush(Theme.Blue))
                    {
                        string ob = L.T("Service board") + "  ›"; g.DrawString(ob, Draw.fSmall, bl, lx, ly);
                        Act(overviewCanvas, new RectangleF(lx, ly - 2 * S, g.MeasureString(ob, Draw.fSmall).Width, 22 * S), delegate { OpenToolFromHome("board"); });
                    }
                    y += rr.Height + 8 * S;
                }
                foreach (var c in certs.Take(3))
                {
                    int days = (int)Math.Floor((c.Item2 - DateTime.Now).TotalDays); int lv = days <= 7 ? 2 : 1;
                    var rr = new RectangleF(x, y, w, 54 * S); Theme.Fill(g, rr, 12 * S, Theme.LvSoft(lv));
                    Glyphs.Tile(g, "cert", new RectangleF(x + 10 * S, y + 9 * S, 36 * S, 36 * S), Theme.LvText(lv), 10 * S);
                    string t = days < 0 ? c.Item1 + " - " + L.T("the certificate has expired") + " (" + c.Item2.ToString("d MMM", CultureInfo.CurrentCulture) + ")"
                        : c.Item1 + " - " + L.T("the certificate ends") + " " + c.Item2.ToString("d MMM", CultureInfo.CurrentCulture) + " (" + (days == 0 ? L.T("today") : days + " " + L.T(days == 1 ? "day" : "days")) + ")";
                    g.DrawString(t, Draw.fBodyB, ink, new RectangleF(x + 58 * S, y + 8 * S, w - 68 * S, 20 * S), nowrap);
                    string when = L.T("Certificate calendar, last looked") + " " + certAt.ToString("d MMM HH:mm", CultureInfo.CurrentCulture) + "  ·  " + L.T("open") + "  ›";
                    g.DrawString(when, Draw.fSmall, sub, new RectangleF(x + 58 * S, y + 30 * S, w - 68 * S, 18 * S), nowrap);
                    Act(overviewCanvas, rr, delegate { OpenToolFromHome("certcal"); });
                    y += rr.Height + 8 * S;
                }
                if (count > Math.Min(3, down.Count) + Math.Min(3, certs.Count)) { g.DrawString(L.T("and more on the Service board and in the Certificate calendar"), Draw.fSmall, sub, x, y); y += 22 * S; }
            }
            return y + 4 * S;
        }
        void OpenToolFromHome(string id) { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(id); SyncNav(); }

        // ---------- the go-to tiles: a big icon, a name, one live line
        float PaintGateway(Graphics g, float x, float y, float w)
        {
            g.DrawString(L.T("GO TO"), Draw.fTiny, new SolidBrush(Theme.Muted), x, y); y += 24 * S;
            var down = Sectors.DownNow(3); DateTime certAt; var certs = Sectors.CertsDue(30, out certAt);
            int done = LabLink.Faults.Count(f => Points.Me.Has("nlab." + f[0]));
            // id, icon, accent, name, live line, go
            var l = new List<object[]>();
            Action<string, string, string, string, string, Action> add = (id, ic, acc, name, live, go) => l.Add(new object[] { id, ic, acc, name, live, go });
            add("toolbox", "tool", "toolbox", "Toolbox", "232 tools, by topic: DNS, DHCP, SNMP, Wi-Fi...", delegate { OpenNav("toolbox"); EnsureToolbox(); SetTbView("topic"); });
            add("encrypt", "encrypt", "q.keys", "Encryption", "Lock a file with a code or for named people; open one; your keys.", delegate { OpenToolFromHome("encrypt"); });
            add("dest", "dns", "dest", "Check a destination", "Name, route, door, lock and answer for any site.", delegate { OpenNav("dest"); });
            add("time", "clock", "time", "Time machine", Cfg.History ? "Replay any minute, what changed, the week." : "History is off - turn it on to replay.", delegate { OpenNav("time"); });
            add("board", "board", "q.reach", "Service board", down.Count > 0 ? down.Count + " " + (down.Count == 1 ? "host is" : "hosts are") + " down now" : "Your hosts and ports, watched.", delegate { OpenToolFromHome("board"); });
            add("certcal", "cert", "q.padlock", "Certificates", certs.Count > 0 ? certs.Count + " ending within 30 days" : certAt == DateTime.MinValue ? "When your sites' certificates end." : "None ending within 30 days.", delegate { OpenToolFromHome("certcal"); });
            add("security", "shield", "security", "Security", "This PC's locks, your network and sites.", delegate { OpenNav("security"); });
            add("story", "route", "story", "Network story", "Your traffic layer by layer, real packets.", delegate { OpenNav("story"); });
            add("learn", "book", "learn", "Learn", "Protocols, Windows inside, every term.", delegate { OpenNav("learn"); });
            add("netlab", "lab", "learn", "Network Lab", "Draw a network, break one thing. " + done + " of " + LabLink.Faults.Length + " faults seen.", delegate { OpenLab(null, false); });
            add("gamer", "game", "gamer", "Gamer", "Game servers live, lag spikes and whose fault.", delegate { OpenNav("gamer"); });
            add("stream", "play", "stream", "Streaming", "Is the stream ready - and why it stalled.", delegate { OpenNav("stream"); });
            add("trade", "chart", "trade", "Markets", "Your broker's connection, the day's numbers.", delegate { OpenNav("trade"); });
            if (Cfg.Points) add("points", "star", "home", "Adeebo Points", Points.Rank(Points.Me.Total) + " · " + Points.Me.Total + " points", delegate { tab = "points"; ShowTab(); SyncNav(); });
            // the current VIEW's hidden pages stay hidden here too
            l = l.Where(o => { string id = (string)o[0]; return Secs.All(s => s[0] != id) || !Cfg.Off(id); }).ToList();
            if (Cfg.Off("toolbox")) l = l.Where(o => (string)o[0] != "encrypt" && (string)o[0] != "board" && (string)o[0] != "certcal").ToList();

            int mc = w > 1000 * S ? 4 : w > 620 * S ? 3 : 2; float mw = (w - (mc - 1) * 12 * S) / mc, mh = 104 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub))
                for (int i = 0; i < l.Count; i++)
                {
                    var o = l[i]; var mr = new RectangleF(x + (i % mc) * (mw + 12 * S), y + (i / mc) * (mh + 12 * S), mw, mh);
                    string acc = (string)o[2];
                    Theme.Fill(g, mr, 14 * S, Theme.Card); Kit.Frame(g, mr);
                    Glyphs.Tile(g, (string)o[1], new RectangleF(mr.X + 14 * S, mr.Y + 14 * S, 48 * S, 48 * S), Kit.Accent(acc), 12 * S);
                    g.DrawString(L.T((string)o[3]), Draw.fBodyB, ink, new RectangleF(mr.X + 74 * S, mr.Y + 16 * S, mr.Width - 84 * S, 22 * S));
                    bool warn = ((string)o[0] == "board" && down.Count > 0) || ((string)o[0] == "certcal" && certs.Count > 0);
                    using (var lb = new SolidBrush(warn ? Theme.LvText(down.Count > 0 && (string)o[0] == "board" ? 2 : 1) : Theme.Sub))
                    using (var ef = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                        g.DrawString(L.T((string)o[4]), Draw.fSmall, lb, new RectangleF(mr.X + 74 * S, mr.Y + 40 * S, mr.Width - 84 * S, mr.Height - 46 * S), ef);
                    var go = (Action)o[5]; Act(overviewCanvas, mr, go);
                }
            y += ((l.Count + mc - 1) / mc) * (mh + 12 * S) + 6 * S;
            return y;
        }

        // ---------- the NOW line on Gamer, Streaming and Markets: your end of the path in one row, Home one click away
        float NowLine(Graphics g, Control c, float x, float y, float w)
        {
            string kind; int sig; lock (M.L) { kind = M.AdapterKind; sig = M.WifiSignal; }
            double gw = M.GwPing.Avg(6), net = M.NetPing.Avg(6), cpu = M.Cpu.Avg(3);
            Func<double, string> ms = v => double.IsNaN(v) ? L.T("no answer") : v.ToString("0", CultureInfo.CurrentCulture) + " ms";
            var items = new List<object[]> {
                new object[] { "cpe", L.T("Router") + " " + ms(gw), LvMs(gw, 15, 50) },
                new object[] { "internet", L.T("Internet") + " " + ms(net), LvMs(net, 80, 150) } };
            if (kind == "Wi-Fi") items.Add(new object[] { "wifi", "Wi-Fi " + (sig >= 0 ? sig + "%" : L.T("on")), sig >= 0 && sig < 40 ? 1 : 0 });
            else items.Add(new object[] { "switch", L.T("Cable"), 0 });
            items.Add(new object[] { "pc", L.T("PC busy") + " " + (double.IsNaN(cpu) ? "-" : cpu.ToString("0", CultureInfo.CurrentCulture) + "%"), double.IsNaN(cpu) ? 3 : cpu > 90 ? 2 : cpu > 75 ? 1 : 0 });
            var bar = new RectangleF(x, y, w, 40 * S); Theme.Fill(g, bar, 12 * S, Theme.Soft);
            float cx = x + 12 * S;
            using (var mb = new SolidBrush(Theme.Muted)) { g.DrawString(L.T("NOW"), Draw.fTiny, mb, cx, y + 13 * S); cx += g.MeasureString(L.T("NOW"), Draw.fTiny).Width + 10 * S; }
            string hm = L.T("Home") + "  ›"; var hs = g.MeasureString(hm, Draw.fBodyB); var hr = new RectangleF(bar.Right - hs.Width - 16 * S, y, hs.Width + 8 * S, 40 * S);
            foreach (var it in items)
            {
                int lv = (int)it[2]; string t = (string)it[1]; var ts = g.MeasureString(t, Draw.fBody);
                if (cx + 44 * S + ts.Width > hr.X) break;   // a narrow window keeps what fits, Home always shows
                Glyphs.Draw(g, (string)it[0], new RectangleF(cx, y + 9 * S, 22 * S, 22 * S), lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Sub, 1.7f);
                Draw.LvDot(g, lv, cx + 26 * S, y + 16 * S, 8 * S, 8 * S);
                using (var tb = new SolidBrush(lv == 1 || lv == 2 ? Theme.LvText(lv) : Theme.Ink)) g.DrawString(t, Draw.fBody, tb, cx + 38 * S, y + (40 * S - ts.Height) / 2);
                cx += 38 * S + ts.Width + 18 * S;
            }
            using (var bl = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(hm, Draw.fBodyB, bl, hr.X + 4 * S, y + (40 * S - hs.Height) / 2);
            Act(c, hr, delegate { tab = "overview"; ShowTab(); SyncNav(); });
            return y + 52 * S;
        }

        // ---------- the big icon for a Toolbox question or a Learn card (falls back to the small line icon)
        static readonly Dictionary<string, string> BigGlyph = new Dictionary<string, string>(StringComparer.Ordinal) {
            { "q:reach", "internet" }, { "q:slow", "bolt" }, { "q:path", "route" }, { "q:lan", "switch" }, { "q:join", "wifi" }, { "q:keys", "key" },
            { "q:cert", "cert" }, { "q:hw", "pc" }, { "q:win", "win" }, { "q:lab", "pkt" }, { "q:calc", "calc" }, { "q:report", "book" }, { "q:pc", "pc" }, { "q:game", "game" }, { "q:time", "clock" }, { "q:fun", "star" },
            { "l:basics", "star" }, { "l:protocol", "pkt" }, { "l:provider", "olt" }, { "l:mobile", "tower" }, { "l:crypto", "encrypt" }, { "l:windows", "win" },
            { "l:apps", "laptop" }, { "l:streaming", "play" }, { "l:investing", "chart" }, { "l:labs", "lab" }, { "l:games", "game" }, { "l:check", "shield" }, { "l:paths", "route" } };
        // true when it drew the big tile; false: the caller draws its old small icon
        static bool BigIcon(Graphics g, string key, RectangleF r, Color c)
        {
            string gk; if (!BigGlyph.TryGetValue(key, out gk) || !Glyphs.Has(gk)) return false;
            Glyphs.Tile(g, gk, r, c, r.Width * 0.26f); return true;
        }
    }
}
