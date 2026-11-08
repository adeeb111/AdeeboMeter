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
        // ---------- Settings
        static readonly string[] SetS0 = { "balanced", "Balanced", "The default. Notices trouble within seconds and still costs next to nothing." };
        static readonly string[] SetS1 = { "engineer", "Engineer", "Watches everything closely - for when something is wrong and you want every clue." };
        static readonly string[] SetS2 = { "2 s", "5 s", "10 s", "Off" };
        static readonly int[] SetN3 = { 2, 5, 10, 0 };
        static readonly string[] SetS4 = { "15 s", "30 s", "60 s", "Off" };
        static readonly int[] SetN5 = { 15, 30, 60, 0 };
        static readonly string[] SetS6 = { "10 s", "30 s", "2 min", "Off" };
        static readonly int[] SetN7 = { 10, 30, 120, 0 };
        static readonly string[] SetS8 = { "5 s", "10 s", "30 s", "Off" };
        static readonly int[] SetN9 = { 5, 10, 30, 0 };
        static readonly string[] SetS10 = { "On", "Off" };
        static readonly string[] LeaveTools = { "Tools you press", "Pre-call check: 20 pings to your router and 20 to 1.1.1.1. DNS speed test: about 40 name lookups to your DNS, 1.1.1.1, 8.8.8.8 and 9.9.9.9. Walk test: 20 pings to your router per room. Only when you press them.", "1" };
        static readonly string[] HistDayNames = { "1 day", "3 days", "7 days", "14 days", "30 days", "60 days", "90 days" };
        static readonly string[] CapNames = { "Off", "10 GB", "25 GB", "50 GB", "100 GB", "250 GB", "500 GB" };
        static readonly string[] SetS11 = { "5 min", "15 min", "30 min" };
        static readonly int[] SetN12 = { 5, 15, 30 };
        static readonly string[] SetS13 = { "30 s", "60 s" };
        static readonly string[] SetS14 = { "20%", "30%", "50%", "75%", "100%" };
        static readonly int[] SetN15 = { 51, 77, 128, 191, 255 };
        static readonly string[] SetS16 = { "0.3 s", "0.45 s", "0.8 s" };
        static readonly int[] SetN17 = { 300, 450, 800 };
        static readonly string[] SetS18 = { "Follow Windows", "Light", "Dark" };
        static readonly string[] SetS19 = { "Companion", "Serious" }, SetSize = { "Small", "Bigger" };
        void PaintSettings(Graphics g)
        {
            g.TranslateTransform(settingsCanvas.AutoScrollPosition.X, settingsCanvas.AutoScrollPosition.Y);
            Begin(settingsCanvas);
            float x = 24 * S, y = 18 * S, w = Math.Min(settingsCanvas.ClientSize.Width - 48 * S, 1100 * S);
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub))
            {
                g.DrawString(Voice.P("How hard should I work, boss?", "How hard should AdeeboMeter work?"), Draw.fTitle, ink, x, y); y += 30 * S;
                string lead = "The widget is always a penny app. The deep stuff (live capture, the full connection list, path checks) only runs while this window is open. These settings decide how much watching happens in the background.";
                var lh = Draw.Measure(lead, Draw.fBody, (int)w).Height; g.DrawString(lead, Draw.fBody, sub, new RectangleF(x, y, w, lh + 4)); y += lh + 14 * S;

                // presets
                string[][] presets = { new[] { "penny", "Penny", "The lightest touch. Checks less often, Wi-Fi watch every 2 min, no call watching." },
                                       SetS0,
                                       SetS1 };
                float pw = (w - 24 * S) / 3, ph = 96 * S;
                for (int i = 0; i < 3; i++)
                {
                    var r = new RectangleF(x + i * (pw + 12 * S), y, pw, ph); bool on = Cfg.Preset == presets[i][0];
                    Theme.Fill(g, r, 12 * S, on ? Theme.BlueSoft : Theme.Soft); Theme.Stroke(g, r, 12 * S, on ? Theme.Blue : Theme.Line, on ? 2 : 1);
                    using (var tb = new SolidBrush(on ? Theme.BlueDark : Theme.Ink)) g.DrawString(presets[i][1], Draw.fHead, tb, r.X + 14 * S, r.Y + 12 * S);
                    string cost = "~" + JobsFor(presets[i][0]) + " checks a minute";
                    var cs = g.MeasureString(cost, Draw.fSmall); g.DrawString(cost, Draw.fSmall, muted, r.Right - 14 * S - cs.Width, r.Y + 15 * S);
                    g.DrawString(presets[i][2], Draw.fSmall, sub, new RectangleF(r.X + 14 * S, r.Y + 40 * S, r.Width - 28 * S, ph - 46 * S));
                    string key = presets[i][0]; Act(settingsCanvas, r, delegate { Cfg.ApplyPreset(key); Cfg.Save(); M.Nudge(); settingsCanvas.Invalidate(); });
                }
                y += ph + 10 * S;
                string now = "Right now: about " + Cfg.JobsPerMinute() + " checks a minute in the background" + (Cfg.Preset == "custom" ? " (your own mix)" : "") + ", on top of the widget's once-a-second read of Windows' own counters. Each check is one tiny probe or one quick read - a few milliseconds of work.";
                var nh = Draw.Measure(now, Draw.fSmall, (int)w).Height; g.DrawString(now, Draw.fSmall, muted, new RectangleF(x, y, w, nh + 4)); y += nh + 18 * S;

                y = SetHead(g, "IN THE BACKGROUND  ·  WHILE ONLY THE WIDGET RUNS", x, y, w);
                y = SetRow(g, x, y, w, "Router ping", Cfg.GwSec == 0 ? "Off: the LAN / Wi-Fi dot goes grey and slow-router alerts stop." : "Pings your router every " + Cfg.GwSec + " s - tells Wi-Fi trouble apart from internet trouble.",
                    SetS2, Array.IndexOf(SetN3, Cfg.GwSec), delegate(int i) { Cfg.GwSec = SetN3[i]; Custom(); });
                y = SetRow(g, x, y, w, "Internet and DNS check", Cfg.NetSec == 0 ? "Off: no 'internet went quiet' alerts; the Internet and DNS dots go grey." : "One tiny ping to a public server and one name lookup every " + Cfg.NetSec + " s.",
                    SetS4, Array.IndexOf(SetN5, Cfg.NetSec), delegate(int i) { Cfg.NetSec = SetN5[i]; Custom(); });
                y = SetRow(g, x, y, w, "Wi-Fi watch", Cfg.WifiSec == 0 ? "Off: no roam, signal or channel alerts." : "Asks Windows for signal, channel and access point every " + (Cfg.WifiSec >= 60 ? Cfg.WifiSec / 60 + " min" : Cfg.WifiSec + " s") + ". The priciest job here - it starts a small Windows tool (netsh) each time.",
                    SetS6, Array.IndexOf(SetN7, Cfg.WifiSec), delegate(int i) { Cfg.WifiSec = SetN7[i]; Custom(); });
                y = SetRow(g, x, y, w, "App and connection watch", Cfg.AppsSec == 0 ? "Off: the verdict can't name the app to close, and connection bursts go unnoticed." : "Reads the process list and connection table every " + Cfg.AppsSec + " s - powers 'top apps', connection bursts and stuck-connection alerts.",
                    SetS8, Array.IndexOf(SetN9, Cfg.AppsSec), delegate(int i) { Cfg.AppsSec = SetN9[i]; Custom(); });
                y = SetRow(g, x, y, w, "Teams call detection", Cfg.Call ? "Notices when a Teams call starts and watches the network more closely during it." : "Off: calls get no extra attention.",
                    SetS10, Cfg.Call ? 0 : 1, delegate(int i) { Cfg.Call = i == 0; Custom(); });

                y = SetRow(g, x, y, w, "History", Cfg.History ? "One small line a minute plus every change, on this PC only (" + Usage.Mb(History.Bytes() / 1048576.0) + " now; about 15 KB a day once compressed). Powers past days, the week heatmap and 'slower than usual'." : "Off: nothing is written to disk - What changed only remembers the last 2 hours, in memory.",
                    SetOnOff, Cfg.History ? 0 : 1, delegate(int i) { Cfg.History = i == 0; Cfg.Save(); });
                if (Cfg.History)
                    y = SetRow(g, x, y, w, "Keep history for", "Every night anything older goes (rolling). 90 days is still only about 1.5 MB.",
                        HistDayNames, Array.IndexOf(Cfg.HistDayChoices, Cfg.HistoryDays), delegate(int i) { Cfg.HistoryDays = Cfg.HistDayChoices[i]; Cfg.Save(); History.Tidy(); });
                y = SetRow(g, x, y, w, "Alerts", Cfg.Alerts ? "A Windows notification only when there's something to do (Wi-Fi keeps dropping, internet gone, slower than usual, a suspicious new program...). Silent while you present." : "Off: AdeeboMeter never pops up; everything still lands in What changed.",
                    SetOnOff, Cfg.Alerts ? 0 : 1, delegate(int i) { Cfg.Alerts = i == 0; Cfg.Save(); });
                if (Cfg.Alerts)
                    y = SetRow(g, x, y, w, "Quiet hours", Cfg.Quiet ? "No alerts between 22:00 and 07:00." : "Alerts any time of day.", SetOnOff, Cfg.Quiet ? 0 : 1, delegate(int i) { Cfg.Quiet = i == 0; Cfg.Save(); });
                y = SetRow(g, x, y, w, "Watch and suggest", Cfg.Watch ? "Notices programs talking to the internet for the first time, from odd folders or at night, and big uploads - and suggests what to look at. It never blocks anything. (First 24 hours: learning what's normal.)" : "Off.",
                    SetOnOff, Cfg.Watch ? 0 : 1, delegate(int i) { Cfg.Watch = i == 0; Cfg.Save(); });
                y = SetRow(g, x, y, w, "Monthly data cap", Cfg.CapGB > 0 ? "Warns at 80% of " + Cfg.CapGB + " GB (hotspots, metered plans). Counted on this PC from Windows' own counters." : "Off - usage is still counted in What changed.",
                    CapNames, Array.IndexOf(Cfg.CapChoices, Cfg.CapGB), delegate(int i) { Cfg.CapGB = Cfg.CapChoices[i]; Cfg.Save(); });
                y = SetRow(g, x, y, w, "Watch my first pinned destination all day", Cfg.Pins.Count == 0 ? "Pin a destination on Check a destination first." : Cfg.PinWatch ? "One ping to " + Cfg.Pins[0] + " every 30 s, graphed on Check a destination. (Listed in About.)" : "Off - pinned destinations are only checked when you press Run all.",
                    SetOnOff, Cfg.PinWatch ? 0 : 1, delegate(int i) { Cfg.PinWatch = i == 0 && Cfg.Pins.Count > 0; Cfg.Save(); });
                if (History.Days().Count > 0)
                {
                    var clr = new RectangleF(x, y - 4 * S, 220 * S, 22 * S);
                    using (var cb = new SolidBrush(Theme.LvText(2))) g.DrawString("Delete all saved history now", Draw.fBodyB, cb, clr.X, clr.Y);
                    Act(settingsCanvas, clr, delegate { History.Clear(); histDay = null; histData = null; histDays = null; settingsCanvas.Invalidate(); });
                    y += 26 * S;
                }
                y = SetHead(g, "IN THIS WINDOW  ·  ONLY WHILE IT'S OPEN", x, y, w);
                y = SetRow(g, x, y, w, "Sign-in page check", Cfg.Portal ? "Only when a check you start fails: one tiny request to the same test page Windows uses, to catch hotel and airport sign-in pages." : "Off: a failed check won't look for a Wi-Fi sign-in page.",
                    SetOnOff, Cfg.Portal ? 0 : 1, delegate(int i) { Cfg.Portal = i == 0; Cfg.Save(); });
                y = SetRow(g, x, y, w, "Names for IP addresses", Cfg.IpNames ? "Reads names from Windows' own DNS cache - no network traffic." : "Off: connections show bare IP addresses.",
                    SetS10, Cfg.IpNames ? 0 : 1, delegate(int i) { Cfg.IpNames = i == 0; Cfg.Save(); });
                y = SetRow(g, x, y, w, "Live capture stops after", "tshark is the heaviest thing AdeeboMeter can run, so it always stops by itself.",
                    SetS11, Array.IndexOf(SetN12, Cfg.LiveMin), delegate(int i) { Cfg.LiveMin = SetN12[i]; Cfg.Save(); });
                y = SetRow(g, x, y, w, "Sniff records for", "How long a row's Sniff button captures before opening Wireshark.",
                    SetS13, Cfg.SniffSec == 60 ? 1 : 0, delegate(int i) { Cfg.SniffSec = i == 1 ? 60 : 30; Cfg.Save(); });

                y = SetHead(g, "THE WIDGET", x, y, w);
                y = SetRow(g, x, y, w, "How visible", "How strongly the corner widget shows when you're not hovering it.",
                    SetS14, Array.IndexOf(SetN15, Cfg.Fade), delegate(int i) { Cfg.Fade = SetN15[i]; Cfg.Save(); });
                y = SetRow(g, x, y, w, "Hover wait on the folded chip", "How long to rest the mouse on the folded chip before the card opens.",
                    SetS16, Array.IndexOf(SetN17, Cfg.DwellMs), delegate(int i) { Cfg.DwellMs = SetN17[i]; Cfg.Save(); });

                y = SetHead(g, "GENERAL", x, y, w);
                int th = Cfg.GetTheme();
                y = SetRow(g, x, y, w, "Theme", "Follow Windows switches light and dark with your Windows setting.",
                    SetS18, th < 0 ? 0 : th == 0 ? 1 : 2, delegate(int i) { Cfg.SetTheme(i == 0 ? -1 : i == 1 ? 0 : 1); });
                y = SetRow(g, x, y, w, "Voice", Voice.Serious ? "Plain, serious wording." : "The companion - a joke first, then the fix.",
                    SetS19, Voice.Serious ? 1 : 0, delegate(int i) { Voice.Serious = i == 1; Cfg.Save(); Invalidate(true); });
                y = SetRow(g, x, y, w, "Corner widget size", Cfg.BigWidget ? "A little bigger (1.3x) - easier to read at a glance." : "Small - out of the way.",
                    SetSize, Cfg.BigWidget ? 1 : 0, delegate(int i) { Cfg.BigWidget = i == 1; Cfg.Save(); Invalidate(true); });
                y = SetRow(g, x, y, w, "Start with Windows", "Starts quietly when you sign in. Exit from the tray stops it completely - nothing stays behind.",
                    SetS10, Cfg.GetAutoStart() ? 0 : 1, delegate(int i) { Cfg.SetAutoStart(i == 0); });
                y += 10 * S;
                g.DrawString("Settings are saved next to AdeeboMeter.exe (AdeeboMeter.settings). Nothing leaves this PC.", Draw.fSmall, muted, x, y); y += 24 * S;
                using (var blue = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
                {
                    var ex = new RectangleF(x, y, 150 * S, 22 * S); g.DrawString("Export settings...", Draw.fBodyB, blue, ex.X, ex.Y); Act(settingsCanvas, ex, ExportSettings);
                    var im = new RectangleF(x + 170 * S, y, 150 * S, 22 * S); g.DrawString("Import settings...", Draw.fBodyB, blue, im.X, im.Y); Act(settingsCanvas, im, ImportSettings);
                    if (settingsNote != "") g.DrawString(settingsNote, Draw.fSmall, muted, x + 340 * S, y + 2 * S);
                    y += 30 * S;
                }
            }
            var need = new Size(0, (int)(y + 20 * S)); if (settingsCanvas.AutoScrollMinSize != need) settingsCanvas.AutoScrollMinSize = need;
        }
        static readonly string[] SetOnOff = { "On", "Off" };
        static int JobsFor(string preset)
        {
            int gw = Cfg.GwSec, net = Cfg.NetSec, wifi = Cfg.WifiSec, apps = Cfg.AppsSec; bool call = Cfg.Call; string p = Cfg.Preset;
            Cfg.ApplyPreset(preset); int n = Cfg.JobsPerMinute();
            Cfg.GwSec = gw; Cfg.NetSec = net; Cfg.WifiSec = wifi; Cfg.AppsSec = apps; Cfg.Call = call; Cfg.Preset = p;
            return n;
        }
        void Custom() { Cfg.Preset = "custom"; Cfg.Save(); M.Nudge(); }
        static float SetHead(Graphics g, string t, float x, float y, float w)
        {
            y += 8 * S;
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(t, Draw.fTiny, mb, x, y);
            using (var line = new Pen(Theme.Line)) g.DrawLine(line, x, y + 20 * S, x + w, y + 20 * S);
            return y + 28 * S;
        }
        float SetRow(Graphics g, float x, float y, float w, string label, string help, string[] opts, int cur, Action<int> set)
        {
            float ow = 0; var widths = opts.Select(o => g.MeasureString(o, Draw.fSmall).Width + 24 * S).ToArray(); ow = widths.Sum() + 2 * S;
            float tw = w - ow - 24 * S;
            float hh = Draw.Measure(help, Draw.fSmall, (int)tw).Height;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString(label, Draw.fBodyB, ink, x, y);
                g.DrawString(help, Draw.fSmall, muted, new RectangleF(x, y + 20 * S, tw, hh + 4));
            }
            var seg = new RectangleF(x + w - ow, y + 4 * S, ow, 30 * S);
            Theme.Fill(g, seg, 15 * S, Theme.Soft); Theme.Stroke(g, seg, 15 * S, Theme.Line, 1);
            float sx = seg.X + 1 * S;
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                for (int i = 0; i < opts.Length; i++)
                {
                    var r = new RectangleF(sx, seg.Y + 2 * S, widths[i], seg.Height - 4 * S);
                    if (i == cur) Theme.Fill(g, r, 13 * S, Theme.Blue);
                    using (var b = new SolidBrush(i == cur ? Color.White : Theme.Sub)) g.DrawString(opts[i], Draw.fSmall, b, r, center);
                    int idx = i; Act(settingsCanvas, r, delegate { set(idx); settingsCanvas.Invalidate(); });
                    sx += widths[i];
                }
            return y + Math.Max(44 * S, 26 * S + hh) + 10 * S;
        }

        // ---------- About: the whole story, told simply
        static readonly string[] LeaveChecks = { "Checks you start", "Check a destination sends only what you ask for, to where you ask, when you press Check.", "1" };
        static readonly string[] LeaveLive = { "Live capture", "Listens to your own adapter only while you press Start. Nothing is sent; captures are deleted when AdeeboMeter exits.", "-1" };
        static readonly string[] LeaveNever = { "Never", "No cloud, no account, no telemetry, no updates, no ads, no crash reports. It never listens on a port - and checks that about itself every time it starts.", "0" };
        static readonly string[][] AboutNav = {
                    new[] { "Open this view", "Click the tray icon, or press Ctrl+Alt+A from anywhere." },
                    new[] { "Back to the corner", "Esc, minimise, the X or the Back to corner button - they all do the same thing: this view closes, gives its memory back, and the widget keeps watching." },
                    new[] { "Quit completely", "Right-click the tray icon and choose Exit. Nothing keeps running - not even a helper." },
                    new[] { "Learn anything", "When the mouse turns into a ? cursor, rest it there for a moment and an explanation appears." },
                    new[] { "Tune it", "Settings decides how much AdeeboMeter watches in the background - from Penny to Engineer." } };
        static readonly string[][] AboutTrust = {
                    new[] { "One readable file", "The whole app is a single source file you can read before you run it, built with the compiler that already ships inside Windows." },
                    new[] { "No admin rights", "Everything it shows comes from what Windows already lets a normal user see. If something is locked, it simply doesn't show." },
                    new[] { "Secure by default", "Checks use TLS 1.2 or newer with Windows' own certificate validation - it never skips a certificate check. Inputs are validated; no shell is ever used." },
                    new[] { "Light as a feather", "A few dozen small checks a minute in the background. The heavy tools only run while this window is open, and always stop by themselves." },
                    new[] { "Leaves no trace", "Settings and a small log live next to the exe. Captures are deleted on exit. Window titles and traffic content are never logged. The 7-day history is off unless you switch it on - and one click deletes it." } };
        static readonly string[][] AboutTabs =
        {
            new[] { "Connections", "Every conversation your PC is having right now - which app, with whom, on which port, in what state. New ones flash in, closed ones fade out. Hover a state to learn what it means; press Sniff on a row to record just that conversation for Wireshark." },
            new[] { "Your Wi-Fi / cable", "Everything about your link: band, channel, signal in dBm, speeds, security, who else shares your air (with a channel map), addresses, errors, and how healthy the hop to your router is. Click the Wi-Fi dot to get here." },
            new[] { "What's running", "Every program on your PC, in plain English: what it does, how it works, why it's there, what happens without it - and whether it's safe to close. AdeeboMeter only explains; it never closes anything." },
            new[] { "Check a destination", "Type a name, an IP or a URL. AdeeboMeter walks the whole path like an engineer would - DNS, route, ping, TCP, TLS, HTTP, packet size, every router on the way - and tells you in one sentence where it breaks. It also tells the path's story: who carries your traffic, where networks hand over (that's where BGP decides), NAT, tunnels and firewalls. Pin the places you check often and run them all at once." },
            new[] { "Health check", "A one-click check-up of the things that quietly cause trouble: days since restart, pending updates, disk space, adapter errors, crowded Wi-Fi, proxies, VPN, DNS, memory and power." },
            new[] { "What changed", "The diary. Every roam, drop, DNS change, VPN flap and app hiccup in the last two hours, with the time it happened - so 'it was slow at 2:15' finally has an answer." },
            new[] { "How it travels", "Your own connection, one layer at a time: which route won and why, your router's hardware address, MTU and packet anatomy, the handshake as a ladder with the time of each step, and with Wireshark the real packets explained field by field. Plus Teams and Office end to end." },
            new[] { "Inside Windows", "Who started whom (the process tree), what each svchost is hosting, what a program is doing right now (CPU, memory, disk, connections) and why it started, everything that starts with Windows - and the deep dives on the processes people ask about." },
            new[] { "Learn (Ctrl+K)", "Press Ctrl+K anywhere to jump to a tab, a program, a term or a protocol. Learn mode explains each protocol with its ladder diagram and, with Wireshark, shows it live on your PC for ten seconds." },
            new[] { "Live capture", "Wireshark's engine (tshark), only if you have Wireshark installed and only while you press Start. It shows what your traffic is made of, who you talk to by name, and Wireshark's own warnings. It stops by itself." }
        };
        void PaintAbout(Graphics g)
        {
            g.TranslateTransform(aboutCanvas.AutoScrollPosition.X, aboutCanvas.AutoScrollPosition.Y);
            Begin(aboutCanvas);
            float W = aboutCanvas.ClientSize.Width, w = Math.Min(W - 64 * S, 860 * S), x = (W - w) / 2, y = 34 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            using (var blue = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            {
                Theme.Logo(g, new RectangleF(x, y, 56 * S, 56 * S), true);
                g.DrawString("AdeeboMeter", Draw.fHero, ink, x + 70 * S, y - 2 * S);
                g.DrawString("Is it me or the network? One glance tells you.", Draw.fHead, blue, x + 72 * S, y + 38 * S);
                y += 84 * S;
                y = Para(g, "When the internet feels slow, most of us open Task Manager, then a command prompt, then ping, then guess. AdeeboMeter does all of that for you - quietly, every second - and tells you in one plain sentence what's wrong and what to do about it.", x, y, w, sub, Draw.fBody);
                y = Para(g, "No cloud. No telemetry. Never listens. Every packet it sends is listed below - and you can switch each one off.", x, y, w, ink, Draw.fBodyB);

                y = AboutHead(g, "1", "The corner widget", x, y, w);
                y = Para(g, "A small card in the bottom-right corner, faded to 30% so it never distracts. It shows the one thing that matters - is everything OK - plus CPU, memory, disk and network at a glance. It never takes clicks, so whatever is underneath keeps working. When a window covers the corner it folds into a tiny chip; rest your mouse on it for half a second and it opens.", x, y, w, sub, Draw.fBody);

                y = AboutHead(g, "2", "Getting around", x, y, w);
                y = Rows(g, AboutNav, x, y, w);

                y = AboutHead(g, "3", "Is it me or the network?", x, y, w);
                y = Para(g, "The five dots at the top follow your traffic out of the building: your PC, your Wi-Fi or cable to the router, your provider (or VPN), DNS - the internet's phone book - and the internet itself. The first dot that isn't green is where the trouble starts. Everything before it is fine; everything after it is a victim, not a cause. That's the whole trick.", x, y, w, sub, Draw.fBody);

                y = AboutHead(g, "4", "What each tab does", x, y, w);
                y = Rows(g, AboutTabs, x, y, w);

                y = AboutHead(g, "5", "What leaves your PC - and what never does", x, y, w);
                Func<int, string> every = sec => sec <= 0 ? "Off" : sec >= 60 ? "every " + sec / 60 + " min" : "every " + sec + " s";
                var leaves = new List<string[]> {
                    new[] { "Router ping", Cfg.GwSec > 0 ? "A tiny ping to your own router, " + every(Cfg.GwSec) + ". Stays on your local network." : "Off.", Cfg.GwSec > 0 ? "0" : "-1" },
                    new[] { "Internet and DNS check", Cfg.NetSec > 0 ? "One ping to 1.1.1.1 (or 8.8.8.8) and one name lookup, " + every(Cfg.NetSec) + "." : "Off - nothing goes to the internet in the background.", Cfg.NetSec > 0 ? "1" : "-1" },
                    new[] { "Provider name", Cfg.NetSec > 0 ? "Once per network: a short trace (6 hops) toward 1.1.1.1 and one name lookup, to put your provider's name on the ISP dot." : "Off (follows the internet check).", Cfg.NetSec > 0 ? "1" : "-1" },
                    new[] { "Wi-Fi watch", Cfg.WifiSec > 0 ? "Asks Windows about your Wi-Fi, " + every(Cfg.WifiSec) + ". Nothing is sent anywhere." : "Off.", "-1" },
                    LeaveChecks,
                    LeaveTools,
                    new[] { "Pinned destination watch", Cfg.PinWatch && Cfg.Pins.Count > 0 ? "One ping to " + Cfg.Pins[0] + " every 30 s (you switched it on)." : "Off.", Cfg.PinWatch ? "1" : "-1" },
                    new[] { "Sign-in page check", Cfg.Portal ? "Only if a check you started fails: one request to the same test page Windows itself uses (msftconnecttest.com)." : "Off.", Cfg.Portal ? "1" : "-1" },
                    LeaveLive,
                    LeaveNever };
                y = Rows(g, leaves, x, y, w);
                var setLink = new RectangleF(x, y, 260 * S, 22 * S);
                g.DrawString("Change any of these in Settings  >", Draw.fBodyB, blue, setLink.X, setLink.Y);
                Act(aboutCanvas, setLink, delegate { tab = "settings"; ShowTab(); });
                y += 34 * S;

                y = AboutHead(g, "6", "Built to be trusted", x, y, w);
                y = Rows(g, AboutTrust, x, y, w);

                y += 10 * S;
                using (var line = new Pen(Theme.Line)) g.DrawLine(line, x, y, x + w, y); y += 14 * S;
                g.DrawString("AdeeboMeter " + Program.Version + "  ·  made by Adeeb Khan  ·  " + Program.Tagline, Draw.fSmall, muted, x, y); y += 26 * S;
                var upd = new RectangleF(x, y, 420 * S, 22 * S);
                g.DrawString("Check for updates  >", Draw.fBodyB, blue, upd.X, upd.Y);
                g.DrawString("opens the website in your browser - AdeeboMeter itself never checks", Draw.fSmall, muted, x + g.MeasureString("Check for updates  >", Draw.fBodyB).Width + 10 * S, y + 2 * S);
                Act(aboutCanvas, upd, delegate { Crash.OpenUrl("https://adeeb111.github.io/AdeeboMeter/?v=" + Uri.EscapeDataString(Program.Version) + "#update"); });
                y += 30 * S;
            }
            var need = new Size(0, (int)(y + 20 * S)); if (aboutCanvas.AutoScrollMinSize != need) aboutCanvas.AutoScrollMinSize = need;
        }
        static float Para(Graphics g, string t, float x, float y, float w, Brush b, Font f)
        {
            var h = Draw.Measure(t, f, (int)w).Height; g.DrawString(t, f, b, new RectangleF(x, y, w, h + 4)); return y + h + 12 * Draw.S;
        }
        static float AboutHead(Graphics g, string num, string title, float x, float y, float w)
        {
            float S = Draw.S; y += 18 * S;
            var c = new RectangleF(x, y, 26 * S, 26 * S);
            Theme.Fill(g, c, 13 * S, Theme.BlueSoft);
            using (var b = new SolidBrush(Theme.Dark ? Theme.Ink : Theme.BlueDark)) using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(num, Draw.fBodyB, b, c, sf);
            using (var b = new SolidBrush(Theme.Ink)) g.DrawString(title, Draw.fTitle, b, x + 38 * S, y + 1 * S);
            return y + 38 * S;
        }
        static float Rows(Graphics g, IEnumerable<string[]> rows, float x, float y, float w)
        {
            float S = Draw.S, lw = 190 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var line = new Pen(Theme.Line2))
                foreach (var r in rows)
                {
                    float tx = x, h = Draw.Measure(r[1], Draw.fBody, (int)(w - lw)).Height;
                    if (r.Length > 2) { int lv = int.Parse(r[2], CultureInfo.InvariantCulture); using (var d = new SolidBrush(lv < 0 ? Theme.Grey : Theme.Lv(lv))) g.FillEllipse(d, x, y + 5 * S, 9 * S, 9 * S); tx += 16 * S; }
                    g.DrawString(r[0], Draw.fBodyB, ink, new RectangleF(tx, y, lw - (tx - x) - 8 * S, 40 * S));
                    g.DrawString(r[1], Draw.fBody, sub, new RectangleF(x + lw, y, w - lw, h + 4));
                    y += Math.Max(h, 20 * S) + 10 * S;
                    g.DrawLine(line, x, y - 5 * S, x + w, y - 5 * S);
                }
            return y + 4 * S;
        }

        float PaintStory(Graphics g, DestResult d, float x, float y, float w)
        {
            var seg = d.Story ?? new List<string[]>(); var find = d.Findings ?? new List<string[]>();
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(seg.Count > 0 ? "THE PATH STORY  ·  WHO CARRIES YOUR TRAFFIC, FROM ROUTER NAMES" : "WHAT WE FOUND", Draw.fTiny, mb, x, y);
            Hint(destCanvas, new RectangleF(x, y, w, 16 * S), "card.PATH STORY"); y += 22 * S;
            // chain of boxes, wrapping to the next line when needed
            float bx = x, bh = seg.Count > 0 ? 52 * S : -14 * S, gap = 26 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var arrow = new Pen(Theme.Rail, 2 * S))
            using (var nowrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                for (int i = 0; i < seg.Count; i++)
                {
                    var sgm = seg[i];
                    float bw = Math.Min(230 * S, Math.Max(g.MeasureString(sgm[0], Draw.fBodyB).Width, g.MeasureString(sgm[1], Draw.fSmall).Width) + 30 * S);
                    if (bx + bw > x + w && bx > x) { bx = x; y += bh + 14 * S; }
                    var br = new RectangleF(bx, y, bw, bh);
                    Color fill = sgm[2] == "you" || sgm[2] == "dest" ? Theme.BlueSoft : sgm[2] == "cgnat" ? Color.FromArgb(40, Theme.Amber) : sgm[2] == "hidden" ? Theme.Soft : Theme.Soft;
                    Theme.Fill(g, br, 10 * S, fill); Theme.Stroke(g, br, 10 * S, sgm[2] == "dest" || sgm[2] == "you" ? Theme.Blue : Theme.Line, 1);
                    g.DrawString(sgm[0], Draw.fBodyB, ink, new RectangleF(br.X + 12 * S, br.Y + 8 * S, bw - 20 * S, 18 * S), nowrap);
                    g.DrawString(sgm[1], Draw.fSmall, muted, new RectangleF(br.X + 12 * S, br.Y + 28 * S, bw - 20 * S, 16 * S), nowrap);
                    string key = sgm[2] == "lan" ? "story.lan" : sgm[2] == "cgnat" ? "story.cgnat" : sgm[2] == "hidden" ? "story.hidden" : sgm[2] == "net" ? "story.net" : null;
                    if (key != null) Hint(destCanvas, br, key);
                    bx += bw;
                    if (i < seg.Count - 1 && bx + gap < x + w) { g.DrawLine(arrow, bx + 4 * S, y + bh / 2, bx + gap - 6 * S, y + bh / 2); g.DrawLine(arrow, bx + gap - 11 * S, y + bh / 2 - 4 * S, bx + gap - 6 * S, y + bh / 2); g.DrawLine(arrow, bx + gap - 11 * S, y + bh / 2 + 4 * S, bx + gap - 6 * S, y + bh / 2); }
                    bx += gap;
                }
            y += bh + 14 * S;
            using (var sub = new SolidBrush(Theme.Sub))
                foreach (var f in find)
                {
                    int lv = int.Parse(f[1], CultureInfo.InvariantCulture);
                    var h = Draw.Measure(f[0], Draw.fBody, (int)(w - 22 * S)).Height;
                    using (var dot = new SolidBrush(lv < 0 ? Theme.Blue : Theme.Lv(lv))) g.FillEllipse(dot, x + 2 * S, y + 6 * S, 8 * S, 8 * S);
                    using (var tb = new SolidBrush(lv == 2 ? Theme.LvText(2) : Theme.Sub)) g.DrawString(f[0], Draw.fBody, tb, new RectangleF(x + 20 * S, y, w - 22 * S, h + 4));
                    y += h + 8 * S;
                }
            return y + 14 * S;
        }
        // ---------- settings to a file and back (to set up another PC the same way); only known, validated settings are read
        string settingsNote = "";
        void ExportSettings()
        {
            using (var d = new SaveFileDialog { FileName = "AdeeboMeter.settings", Filter = "AdeeboMeter settings|*.settings", OverwritePrompt = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { Cfg.Save(); File.Copy(Path.Combine(Program.Dir, "AdeeboMeter.settings"), d.FileName, true); settingsNote = "Saved to " + Path.GetFileName(d.FileName) + "."; }
                catch (Exception e) { settingsNote = "Couldn't save: " + e.Message; }
            }
            settingsCanvas.Invalidate();
        }
        void ImportSettings()
        {
            using (var d = new OpenFileDialog { Filter = "AdeeboMeter settings|*.settings|All files|*.*" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    int n = 0; Cfg.Pins.Clear();
                    foreach (var line in File.ReadLines(d.FileName).Take(200))
                    {
                        int eq = line.IndexOf('='); if (eq <= 0) continue;
                        string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                        if (!Cfg.Known.Contains(k)) continue;
                        if (k == "autostart") Cfg.SetAutoStart(v == "1"); else if (k == "serious") Voice.Serious = v == "1"; else Cfg.Read(k, v);
                        n++;
                    }
                    Cfg.Save(); M.Nudge(); BuildPinBar(); settingsNote = "Loaded " + n + " settings.";
                }
                catch (Exception e) { settingsNote = "Couldn't read it: " + e.Message; }
            }
            settingsCanvas.Invalidate();
        }
    }
}
