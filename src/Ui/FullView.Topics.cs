// Toolbox, by topic (1.1.6, Adeeb 21 Nov: "by question, by items - SNMP, DNS, DHCP"): the same 232 tools sorted by
// what they work on - a protocol, a part of the network, or a kind of job - as big icon tiles. A tool can sit in two
// topics (DoH is DNS and TLS). The self-test fails if a tool has no topic or a topic names a tool that doesn't exist.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    sealed partial class FullView
    {
        // id, name, icon (Glyphs), colour
        internal static readonly string[][] Topics = {
            new[] { "dns", "DNS", "dns", "#1F6FEB" },
            new[] { "dhcp", "DHCP & addressing", "cpe", "#2DA44E" },
            new[] { "snmp", "SNMP & devices", "switch", "#8250DF" },
            new[] { "wifi", "Wi-Fi", "wifi", "#0E8F9B" },
            new[] { "route", "Routing & path", "route", "#C2621F" },
            new[] { "ports", "Ports & reachability", "port", "#3D4B5A" },
            new[] { "icmp", "ICMP & ping", "icmp", "#3D4B5A" },
            new[] { "web", "HTTP, TLS & certificates", "cert", "#9A6700" },
            new[] { "email", "Email", "mail", "#CF3D5E" },
            new[] { "auth", "802.1X, RADIUS, TACACS, LDAP", "key", "#3D4B5A" },
            new[] { "vpn", "VPN & privacy", "lock", "#3D4B5A" },
            new[] { "time", "Time (NTP)", "clock", "#3D4B5A" },
            new[] { "speed", "Speed & quality", "chart", "#1A8F5A" },
            new[] { "media", "Calls, games, streams & trading", "game", "#8250DF" },
            new[] { "packets", "Packets & captures", "pkt", "#3D4B5A" },
            new[] { "keys", "Keys & encryption", "encrypt", "#9A6700" },
            new[] { "phone", "Your phone", "phone", "#1F6FEB" },
            new[] { "windows", "Windows", "win", "#3D4B5A" },
            new[] { "hw", "Hardware nerds", "bolt", "#C2621F" },
            new[] { "reports", "Reports & over time", "clock", "#3D4B5A" },
            new[] { "calc", "Calculators", "calc", "#3D4B5A" },
            new[] { "lab", "Traffic Lab", "send", "#CF222E" } };
        // tool id -> its topics
        internal static readonly Dictionary<string, string> ToolTopics = new Dictionary<string, string>(StringComparer.Ordinal) {
            { "afterstream", "media reports" }, { "allips", "dns" }, { "arp", "dhcp" }, { "arpwho", "lab dhcp" }, { "auth", "auth windows" },
            { "avgcost", "calc media" }, { "b64", "keys calc" }, { "backup", "calc" }, { "bandwidth", "calc" }, { "battery", "hw" }, { "bdp", "calc speed" },
            { "beforeafter", "reports" }, { "besttime", "media reports" }, { "bg", "windows" }, { "binhex", "calc" }, { "bloat", "speed" },
            { "bluetooth", "windows hw" }, { "board", "ports reports" }, { "capfc", "reports" }, { "cellular", "calc" }, { "certcal", "web reports" },
            { "certcmds", "web keys" }, { "certcmp", "web" }, { "certconv", "web keys" }, { "certfile", "web keys" }, { "certlive", "keys web" },
            { "certmatch", "keys web" }, { "certs", "web windows" }, { "chain", "web" }, { "changed", "windows reports" }, { "clock", "time" },
            { "cloud", "speed" }, { "compound", "calc" }, { "comunits", "calc" }, { "console", "media" }, { "cpuheld", "hw" }, { "crash", "windows" },
            { "csr", "web keys" }, { "csrmake", "keys web" }, { "datause", "calc" }, { "dc", "auth dns windows" }, { "decrypt", "keys" }, { "dhcp", "dhcp" },
            { "dhcpopt", "lab dhcp" }, { "dhcpreplay", "dhcp packets" }, { "dig", "dns" }, { "discover", "dns packets" }, { "disk", "hw" },
            { "diskspeed", "hw" }, { "dltime", "calc speed" }, { "dnscmp", "dns" }, { "dnsrace", "dns speed" }, { "dnsrules", "dns vpn" },
            { "doh", "dns web vpn" }, { "dot1x", "auth" }, { "drivers", "windows hw" }, { "dscp", "speed icmp" }, { "dupip", "dhcp" },
            { "eapreplay", "auth packets" }, { "encrypt", "keys" }, { "events", "windows" }, { "feltslow", "hw speed" }, { "friend", "keys" },
            { "futures", "calc" }, { "fw", "windows ports" }, { "fxconv", "calc media" }, { "gamecompare", "media wifi" }, { "gateway", "route dhcp" },
            { "golive", "media speed" }, { "gpn", "media route" }, { "gpo", "windows" }, { "gpu", "hw" }, { "gpucheck", "hw" }, { "growth", "calc" },
            { "hairpin", "ports" }, { "hashf", "keys" }, { "hidden", "hw" }, { "hosts", "dns windows" }, { "hotel", "wifi vpn" }, { "http", "web" },
            { "httpreq", "web" }, { "https", "web" }, { "httptime", "web speed" }, { "hwmap", "hw" }, { "icmpb", "lab icmp" }, { "icmpexplain", "icmp packets" },
            { "inventory", "snmp dhcp" }, { "ip46", "dhcp" }, { "iperf", "speed" }, { "ipv6score", "dhcp vpn" }, { "ispreport", "reports speed" },
            { "jitterbuf", "calc media" }, { "join", "dhcp auth" }, { "keys", "keys" }, { "keytest", "hw" }, { "lag", "media speed" }, { "laptop", "hw" },
            { "ldapcheck", "auth ports" }, { "leaks", "windows hw" }, { "letter", "reports" }, { "linkbudget", "calc wifi" }, { "listen", "windows ports" },
            { "mail", "email ports" }, { "manyports", "ports" }, { "marketopen", "media" }, { "mibs", "snmp" }, { "month", "reports" },
            { "mtr", "route icmp speed" }, { "mtu", "route icmp" }, { "mysites", "web" }, { "nbname", "dns snmp" }, { "netcap", "hw speed" },
            { "netflow", "lab" }, { "netsnap", "reports windows" }, { "newkey", "keys" }, { "ntp", "time" }, { "obs", "media" }, { "overlap", "vpn dhcp" },
            { "owners", "route" }, { "pctback", "calc" }, { "pcwhy", "hw" }, { "perunit", "calc" }, { "pfx", "web keys" }, { "phconn", "phone" },
            { "phdns", "phone dns" }, { "phemail", "phone email" }, { "phfastdns", "phone dns" }, { "phmac", "phone snmp" }, { "phonedown", "phone ports" },
            { "phonevs", "phone" }, { "phoneweek", "phone reports" }, { "phport", "phone ports" }, { "phrdap", "phone dns" }, { "phregions", "phone speed" },
            { "phsite", "phone web" }, { "phspam", "phone email" }, { "phspeed", "phone speed" }, { "phstatus", "phone web" }, { "phstun", "phone media" },
            { "phvpn", "phone vpn" }, { "phwho", "phone route" }, { "phwifisafe", "phone wifi" }, { "ping", "icmp speed" }, { "ping6", "icmp dhcp" },
            { "plan", "calc speed" }, { "pnl", "calc media" }, { "port", "ports" }, { "possize", "calc media" }, { "prematch", "media" },
            { "pressure", "hw windows" }, { "proxy", "web vpn" }, { "qr", "phone" }, { "radius", "auth" }, { "radiuslab", "auth" }, { "raid", "calc hw" },
            { "ramcheck", "hw" }, { "rareplay", "dhcp packets" }, { "rdpcheck", "ports" }, { "recipients", "keys" }, { "remote", "ports snmp" },
            { "ret", "route" }, { "rlisten", "route packets" }, { "roams", "wifi" }, { "roast", "reports" }, { "route", "route" },
            { "rushhour", "wifi reports" }, { "samplelab", "packets" }, { "screentest", "hw" }, { "selfsigned", "keys web" }, { "shares", "ports snmp" },
            { "sixmonths", "reports" }, { "sla", "calc" }, { "smtppath", "email" }, { "snmp", "snmp" }, { "snmpcpu", "snmp" }, { "snmpif", "snmp" },
            { "snmplag", "snmp" }, { "snmplldp", "snmp" }, { "snmpmac", "snmp" }, { "snmppoe", "snmp" }, { "snmpprn", "snmp" }, { "snmpsens", "snmp" },
            { "snmpsnap", "snmp" }, { "snmptraffic", "snmp" }, { "snmpups", "snmp" }, { "snmpvlan", "snmp" }, { "snmpwalk", "snmp" }, { "soundtest", "hw" },
            { "space", "hw" }, { "speedtest", "speed" }, { "srvchange", "web reports" }, { "ssh", "ports" }, { "stack", "calc packets" },
            { "stream", "calc media" }, { "stun", "media ports" }, { "subnet", "dhcp calc" }, { "svcfail", "windows" }, { "sweep", "lab" }, { "syslog", "lab" },
            { "tacacs", "auth" }, { "tcpping", "ports speed" }, { "tcpsend", "lab ports" }, { "tdm", "calc" }, { "teams", "media ports" },
            { "therapy", "reports" }, { "tickrate", "calc media" }, { "tidy", "windows hw" }, { "timeline", "reports" }, { "timesrc", "time windows" },
            { "tonight", "media" }, { "tradelat", "calc media" }, { "tradetape", "media reports" }, { "trap", "lab snmp" }, { "twamp", "speed" },
            { "twopc", "reports" }, { "udpmtu", "route dns" }, { "udpsend", "lab" }, { "units", "calc" }, { "ups", "calc hw" }, { "uptime", "reports ports" },
            { "usb", "windows hw" }, { "v6routers", "dhcp route" }, { "v6split", "calc dhcp" }, { "vlsm", "calc dhcp" }, { "vpn", "vpn" },
            { "vpnleak", "vpn dns" }, { "wakeup", "reports wifi" }, { "wifirate", "calc wifi" }, { "wildcard", "calc" }, { "winev", "windows" },
            { "winupdate", "windows" }, { "wol", "lab" } };
        static bool ByTopic { get { return Cfg.TbView == "topic"; } }
        string tbTopic = "dns";   // the topic open on the right
        List<Tool> TopicTools(string topic) { return Tools2().Where(t => { string v; return ToolTopics.TryGetValue(t.Id, out v) && (" " + v + " ").Contains(" " + topic + " "); }).ToList(); }
        // tools with no topic, topics with no tool, icons that don't exist - empty when all is well (self-test)
        internal string TopicCheck()
        {
            var bad = new List<string>(); var ids = new HashSet<string>(Tools2().Select(t => t.Id));
            foreach (var t in ids) if (!ToolTopics.ContainsKey(t)) bad.Add(t + ": no topic");
            foreach (var kv in ToolTopics) { if (!ids.Contains(kv.Key)) bad.Add(kv.Key + ": not a tool"); foreach (var tp in kv.Value.Split(' ')) if (!Topics.Any(z => z[0] == tp)) bad.Add(kv.Key + ": topic " + tp); }
            foreach (var tp in Topics) { if (!Glyphs.Has(tp[2])) bad.Add(tp[0] + ": no icon " + tp[2]); if (TopicTools(tp[0]).Count == 0) bad.Add(tp[0] + ": empty"); }
            return string.Join("; ", bad);
        }
        static Color TopicColor(string h) { try { return ColorTranslator.FromHtml(h); } catch (Exception) { return Theme.Blue; } }

        // left: the topics as big icon tiles; right: the picked topic's tools - Example and Open on each
        void PaintTopics(Graphics g, float x, float y, float w)
        {
            if (!Topics.Any(z => z[0] == tbTopic)) tbTopic = Topics[0][0];
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            using (var nowrap = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            using (var center = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord })
            {
                y += 8 * S;
                bool wide = w > 900 * S; float lw = wide ? Math.Min(480 * S, w * 0.42f) : w; int cols = wide ? 3 : Math.Max(2, (int)(w / (150 * S)));
                float gap = 8 * S, tw = (lw - gap * (cols - 1)) / cols, th = 104 * S, y0 = y;
                for (int i = 0; i < Topics.Length; i++)
                {
                    var tp = Topics[i]; var r = new RectangleF(x + (i % cols) * (tw + gap), y + (i / cols) * (th + gap), tw, th); bool on = tp[0] == tbTopic; var col = TopicColor(tp[3]);
                    if (!Kit.Off(g, r))
                    {
                        Theme.Fill(g, r, 14 * S, on ? Theme.BlueSoft : Theme.Card);
                        Theme.Stroke(g, r, 14 * S, on ? Theme.Blue : Theme.Line, on ? 2 * S : 1);
                        Glyphs.Draw(g, tp[2], new RectangleF(r.X + (r.Width - 36 * S) / 2, r.Y + 12 * S, 36 * S, 36 * S), col, 1.7f);
                        g.DrawString(L.T(tp[1]), Draw.fPill, ink, new RectangleF(r.X + 4 * S, r.Y + 54 * S, r.Width - 8 * S, 32 * S), center);
                        g.DrawString(TopicTools(tp[0]).Count + " " + L.T("tools"), Draw.fTiny, muted, new RectangleF(r.X, r.Y + 84 * S, r.Width, 16 * S), center);
                    }
                    string id = tp[0]; Act(tbCanvas, r, delegate { tbTopic = id; tbCanvas.Invalidate(); });
                }
                float leftEnd = y + ((Topics.Length + cols - 1) / cols) * (th + gap);
                // the picked topic
                float rx = wide ? x + lw + 20 * S : x, ry = wide ? y0 : leftEnd + 14 * S, rw = wide ? w - lw - 20 * S : w;
                var cur = Topics.First(z => z[0] == tbTopic); var list = TopicTools(tbTopic);
                Glyphs.Tile(g, cur[2], new RectangleF(rx, ry, 52 * S, 52 * S), TopicColor(cur[3]), 14 * S);
                g.DrawString(L.T(cur[1]), Draw.fName, ink, rx + 64 * S, ry + 2 * S);
                g.DrawString(list.Count + " " + L.T("tools") + " · " + L.T("Example shows a finished result first; Open runs it"), Draw.fSmall, muted, new RectangleF(rx + 64 * S, ry + 30 * S, rw - 64 * S, 20 * S), nowrap);
                ry += 66 * S;
                foreach (var t in list)
                {
                    var r = new RectangleF(rx, ry, rw, 52 * S);
                    if (!Kit.Off(g, r))
                    {
                        Theme.Fill(g, r, 10 * S, Theme.Soft);
                        var term = Explain.Term("tb." + t.Id); string tag = term != null ? term.Tag : "";
                        var os = g.MeasureString(L.T("Open"), Draw.fPill); var orr = new RectangleF(r.Right - os.Width - 34 * S, r.Y + 12 * S, os.Width + 20 * S, 28 * S);
                        Theme.Fill(g, orr, 9 * S, Theme.Blue); using (var wb = new SolidBrush(Theme.OnBlue)) g.DrawString(L.T("Open"), Draw.fPill, wb, orr.X + 10 * S, orr.Y + (28 * S - os.Height) / 2);
                        string tid = t.Id; Act(tbCanvas, orr, delegate { tbSearch.Box.Text = ""; OpenTool(tid); });
                        float ex = orr.X - 10 * S;
                        if (Examples.Get(t.Id) != null)
                        {
                            var es = g.MeasureString(L.T("Example"), Draw.fPill); var er = new RectangleF(ex - es.Width - 8 * S, r.Y + 16 * S, es.Width + 8 * S, 22 * S);
                            g.DrawString(L.T("Example"), Draw.fPill, lb, er.X + 4 * S, er.Y + 2 * S); Act(tbCanvas, er, delegate { tbSearch.Box.Text = ""; OpenTool(tid); ShowExample(); }); ex = er.X - 8 * S;
                        }
                        g.DrawString(t.Id == "snmp" ? L.T("SNMP - ask a switch or router") : L.T(t.Title), Draw.fBodyB, ink, new RectangleF(r.X + 14 * S, r.Y + (tag == "" ? 16 : 7) * S, ex - r.X - 20 * S, 22 * S), nowrap);
                        if (tag != "") g.DrawString(tag, Draw.fSmall, muted, new RectangleF(r.X + 14 * S, r.Y + 28 * S, ex - r.X - 20 * S, 18 * S), nowrap);
                        Hint(tbCanvas, r, "tb." + tid);
                    }
                    ry += 58 * S;
                }
                Need(tbCanvas, Math.Max(leftEnd, ry) + 20 * S);
            }
        }
    }
}
