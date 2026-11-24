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
        static string[] Row(params string[] a) { return a; }
        // ---------- Settings
        static readonly string[] SetS0 = { "balanced", "Balanced", AdeeboMeter.L.T("The default - every check close. Router every 2 s, internet every 15 s: notices trouble within seconds.") };
        static readonly string[] SetSG = { "gamer", "Gamer", AdeeboMeter.L.T("Checks close together. In the background, your games' best regions every 10 s (5 servers); while you play, up to 16 servers every second.") };
        static readonly string[] SetMode = { "Simple", "Pro" };
        /* review 1.1.6 U3 #5: the label can arrive translated - match the English key or its translation */
        static readonly string[] PolicyLabels = { "View", "Internet and DNS check", "BGP and who owns the network", "Switch them off by themselves after", "Day's numbers", "Game ping in the background", "Servers watched in the background", "Every second while a game is open", "Start when a game opens", "Live capture stops after" };
        static bool PolicyLocks(string label)
        {
            foreach (var e in PolicyLabels) if (label == AdeeboMeter.L.T(e)) { label = e; break; }
            switch (label)
            {
                case "View": return Policy.Mode != "";
                case "Day's numbers": return Policy.NoOnline;
                case "Internet and DNS check": return Policy.NoInternetCheck;
                                case "BGP and who owns the network": case "Switch them off by themselves after": return Policy.NoOnline;
                case "Game ping in the background": case "Servers watched in the background": case "Every second while a game is open": case "Start when a game opens": return Policy.NoGamer;
                case "Live capture stops after": return Policy.NoCapture;
                default: return false;
            }
        }
        static readonly string[] SetS2 = { "2 s", "5 s", "10 s", "30 s", "Off" }, SetOnlineMin = { "5 min", "15 min", "30 min", "1 h", "2 h", "3 h" };
        static readonly int[] SetN3 = { 2, 5, 10, 30, 0 };
        static readonly string[] SetS4 = { "15 s", "30 s", "60 s", "5 min", "Off" };
        static readonly int[] SetN5 = { 15, 30, 60, 300, 0 };
        static readonly string[] SetS6 = { "10 s", "30 s", "2 min", "Off" };
        static readonly int[] SetN7 = { 10, 30, 120, 0 };
        static readonly string[] SetS8 = { "5 s", "10 s", "30 s", "Off" };
        static readonly int[] SetN9 = { 5, 10, 30, 0 };
        static readonly string[] SetS10 = { "On", "Off" };
        static readonly string[] LeaveTools = { AdeeboMeter.L.T("Tools you press"), AdeeboMeter.L.T("Pre-call check: 20 pings to your router and 20 to 1.1.1.1. DNS speed test: 21 name lookups to each server it races (yours, the router, 1.1.1.1, 8.8.8.8, 9.9.9.9 - about 100 in all). Walk test: 20 pings to your router per room. Only when you press them."), "1" };
        static readonly string[] HistDayNames = { "1 day", "3 days", "7 days", "14 days", "30 days", "60 days", "90 days", "6 months" };
        static readonly string[] CapNames = { "Off", "10 GB", "25 GB", "50 GB", "100 GB", "250 GB", "500 GB" };
        static readonly string[] SetS11 = { "5 min", "15 min", "30 min" };
        static readonly int[] SetN12 = { 5, 15, 30 };
        static readonly string[] SetS13 = { "30 s", "60 s" };
        static readonly string[] SetS14 = { "20%", "30%", "50%", "75%", "100%" };
        static readonly int[] SetN15 = { 51, 77, 128, 191, 255 };
        static readonly string[] SetS16 = { "0.3 s", "0.45 s", "0.8 s" };
        static readonly int[] SetN17 = { 300, 450, 800 };
        static readonly string[] SetS18 = { "Follow Windows", "Light", "Dark", "Neon", "Midnight", "Sunset", "Mint" };
        static readonly string[] SetWSyms = { "S&P 500, US dollar, gold", "My symbols..." }, SetWPosGame = { "Same as usual", "Bottom right", "Bottom left", "Top right", "Top left" }, SetWSports = { "Off", "Hockey", "Football", "Cricket", "All three" }, SetSportsEvery = { "Every 5 minutes", "Every 2 minutes", "Every minute" };
        static string[] ScreenOpts() { int n = 0; return Screen.AllScreens.Select(z => { n++; return (z.Primary ? AdeeboMeter.L.T("Main screen") : AdeeboMeter.L.T("Screen ") + n) + "  (" + z.Bounds.Width + " x " + z.Bounds.Height + ")"; }).ToArray(); }
        static string[] MineOpts() { return new[] { "(nothing)" }.Concat(Corner.Fields.Select(f => f[1])).ToArray(); }
        static string[] PlanOpts() { return new[] { "Nothing" }.Concat(WStyle).ToArray(); }
        static int PlanIndex(string slot) { var p = Corner.PlanGet(slot); return p == "" ? 0 : 1 + Math.Max(0, Array.IndexOf(WStyleIds, p)); }
        void PlanPick(string slot, int i) { Corner.PlanSet(slot, i == 0 ? "" : WStyleIds[i - 1]); Cfg.Save(); Invalidate(true); }
        static bool PlanHas(string st) { return Corner.Slots.Any(z => Corner.PlanGet(z) == st); }
        static string NewStyleHelp()
        {
            switch (Cfg.WidgetStyle)
            {
                case "markets": return AdeeboMeter.L.T("Up to four prices - an index, a stock, a commodity or a currency - with a little graph. Not live: the time they're from is on the card.");
                case "sports": return AdeeboMeter.L.T("The teams you follow: a live score at the top, then what's next and the last results.");
                case "data": return AdeeboMeter.L.T("How much you used today and this month, against your cap if you set one, and where this pace ends the month.");
                case "weather": return AdeeboMeter.L.T("Now, what's next (\"rain from 3 pm\") and the hours ahead - refreshed hourly while it shows.");
                case "mine": return AdeeboMeter.L.T("Up to six readings you pick below - speeds, pings, CPU, data, game ping, weather, a market, your team, the time.");
            }
            return null;
        }
        static readonly string[] SetSize = { "Small", "Bigger" }, SetWPos = { "Bottom right", "Bottom left", "Top right", "Top left", "Reset" };
        static readonly string[] SetCorner = { "Quiet", "Quiet + moments", AdeeboMeter.L.T("Follows my view") };
        // Settings: one list of rows, each in a section and tagged with what it touches; a side menu picks the section,
        // the search box (settingsSearch) shows every match across sections.
        sealed class SetItem
        {
            public string Sec, Label, Tag, Keys = ""; public Func<string> Help; public string[] Opts; public Func<int> Cur; public Action<int> Set; public Func<bool> Show;
        }
        internal static readonly string[] SetSections = { "General", "Widget", "Weather", "Companion", "Navigation", "Background checks", "Gamer Zone", "Streaming & trading", "Alerts", "History & data", "Privacy & online lookups", "Capture & advanced" };
        const string TagGame = "BACKGROUND · GAME SERVERS", TagPc = "ON THIS PC", TagLan = "BACKGROUND · YOUR NETWORK", TagNet = "BACKGROUND · INTERNET", TagOnline = "ONLINE LOOKUP", TagWin = "ONLY WHILE THIS WINDOW IS OPEN";
        string setSec = "";   // "" = the question cards (Adeeb, 14 Nov): General first, then the widget, then the rest
        RoundBox settingsSearch;
        static string Every(int sec) { return sec <= 0 ? "off" : sec >= 60 && sec % 60 == 0 ? "every " + sec / 60 + " min" : sec >= 60 ? "every " + (sec / 60.0).ToString("0.#", CultureInfo.CurrentCulture) + " min" : "every " + sec + " s"; }   // bug-hunt L-159: 90 s is 1.5 min
        internal static string SendingNow()
        {
            var l = new List<string>();
            if (Cfg.GwSec > 0) l.Add("router ping " + Every(Cfg.GwSec));
            if (Cfg.NetSec > 0) l.Add("internet check " + Every(Cfg.NetSec));
            if (Cfg.PinWatch && Cfg.Pins.Count > 0) l.Add(AdeeboMeter.L.T("pinned destination every 30 s"));
            if (Games.Monitored().Count > 0 && Cfg.GameBgSec > 0) l.Add("game ping " + Every(Cfg.GameBgSec));
            if (Cfg.TradeLive && Cfg.TradeHost != "" && !Cfg.Off("trade")) l.Add("broker check " + Every(Trading.Sec) + (Cfg.TradeCrypto ? "" : " in market hours"));
            if (Weather.Active) l.Add(Cfg.WeatherAcc == "better" ? AdeeboMeter.L.T("weather every 6 h") : AdeeboMeter.L.T("weather about every 2 days"));
            { int ph = 0; try { if (Cfg.PhoneStandby) ph = RemotePhones.CountCached; } catch (Exception e) { if (e is OutOfMemoryException) throw; } if (ph > 0) l.Add(AdeeboMeter.L.T("a waiting room for your phone, and a clock check once an hour")); }   // round 6 TA6-1
            if (Online.On) l.Add(AdeeboMeter.L.T("online lookups (") + Math.Ceiling(Online.Left.TotalMinutes) + AdeeboMeter.L.T(" min left)"));
            return l.Count == 0 ? AdeeboMeter.L.T("Right now this PC sends nothing in the background.") : AdeeboMeter.L.T("Right now this PC sends: ") + string.Join(" · ", l) + ".";
        }
        // bug-hunt L-155: built once and reused by every paint - rebuilt when the language or the screens change, or after 30 s
        List<SetItem> setCache; string setCacheKey = ""; DateTime setCacheAt;
        List<SetItem> SetItems()
        {
            string key = Cfg.Lang + "|" + Screen.AllScreens.Length;
            if (setCache == null || key != setCacheKey || (DateTime.UtcNow - setCacheAt).TotalSeconds > 30) { setCache = BuildSetItems(); setCacheKey = key; setCacheAt = DateTime.UtcNow; }
            return setCache;
        }
        List<SetItem> BuildSetItems()
        {
            var L = new List<SetItem>();
            Action<string, string, string, Func<string>, string[], Func<int>, Action<int>, Func<bool>, string> add = (sec, label, tag, help, opts, cur, set, show, keys) =>
            {
                if (PolicyLocks(label)) { help = () => Policy.Locked; set = delegate { }; }   // shown, but the IT team's choice stands
                L.Add(new SetItem { Sec = sec, Label = label, Tag = tag, Help = help, Opts = opts, Cur = cur, Set = set, Show = show, Keys = keys ?? "" });
            };
            // General
            { var vs = Views.All(); add("General", "View", TagPc, () => { var cur = vs.FirstOrDefault(v => v[0] == Cfg.View); return (cur != null ? AdeeboMeter.L.T(cur[2]) + ". " : "") + AdeeboMeter.L.T("Each view keeps your own changes - switch any time from the View button at the top. Learn is in every view."); }, vs.Select(v => v[1]).ToArray(), () => vs.FindIndex(v => v[0] == Cfg.View), i => SwitchView(vs[i][0]), null, "view mode simple pro engineer gamer streamer trader everyday advanced beginner tabs"); }
            add("General", "Language", TagPc, () => Cfg.Lang == "en" ? AdeeboMeter.L.T("English. A change takes full effect after a restart.") : AdeeboMeter.L.T("Preview: some text is still in English. Restart AdeeboMeter to apply the language everywhere."), AdeeboMeter.L.Languages.Select(l => l[1]).ToArray(), () => Math.Max(0, Array.FindIndex(AdeeboMeter.L.Languages, l => l[0] == Cfg.Lang)), i => PickLanguage(AdeeboMeter.L.Languages[i][0]), null, "language french français español spanish arabic العربية bengali বাংলা");
            add("General", "Theme", TagPc, () => { int th = Cfg.GetTheme(); return th == 2 ? AdeeboMeter.L.T("Neon: near-black with electric cyan - accents glow softly.") : th == 3 ? AdeeboMeter.L.T("Midnight: deep navy with aqua - calm for long nights.") : th == 4 ? AdeeboMeter.L.T("Sunset: warm plum with coral.") : th == 5 ? AdeeboMeter.L.T("Mint: light and fresh, soft green.") : th == 1 ? AdeeboMeter.L.T("Dark: always dark, whatever Windows uses.") : th == 0 ? AdeeboMeter.L.T("Light: always light, whatever Windows uses.") : AdeeboMeter.L.T("Follow Windows switches light and dark with your Windows setting."); }, SetS18, () => { int th = Cfg.GetTheme(); return th < 0 ? 0 : Math.Min(SetS18.Length - 1, th + 1); }, i => Cfg.SetTheme(i - 1), null, "dark light colour color theme neon midnight sunset mint");
            // review 24 Nov (ML-6): all four papers the settings file can hold can be picked and kept - white and the app's colours
            // were loaded but showed as "Newspaper" and were overwritten by either choice
            add("General", "Markets and Sports look", TagPc, () => Paper.App ? AdeeboMeter.L.T("App: the same cards and type as every other page.") : AdeeboMeter.L.T("Newspaper: a masthead, serif headlines and cream paper."), SetPaperOpts, () => Paper.App ? 0 : Cfg.Paper == "white" ? 2 : Cfg.Paper == "theme" ? 3 : 1, i => { Cfg.Paper = i == 0 ? "app" : i == 2 ? "white" : i == 3 ? "theme" : "cream"; Cfg.Save(); Invalidate(true); }, null, "newspaper paper markets sports look style cream");
            // 24 Nov: the Details switch left the ⋯ menu - it only adds engineers' extra numbers now (worst values, sources, link rates)
            add("General", "Extra numbers for engineers", TagPc, () => Cfg.Details ? AdeeboMeter.L.T("On: worst values, where each number comes from and link rates, in the Time machine and the tools.") : AdeeboMeter.L.T("Off: the plain readings only."), SetOnOff, () => Cfg.Details ? 0 : 1, i => SetDetails(i == 0), null, "details raw values engineer worst sources");
            // Weather (Adeeb, 14 Nov): off until turned on; the place is typed, never located (Weather.cs)
            add("Weather", "Weather", TagOnline, WeatherHelp, SetOnOff, () => Cfg.WeatherOn && Weather.Current != null ? 0 : 1, i => SetWeather(i == 0), null, "weather forecast rain snow hot cold temperature city");
            add("Weather", "Weather for", TagOnline, () => { var p = Weather.Current; return p == null ? AdeeboMeter.L.T("No place yet.") : p.Name + "  (" + p.Lat.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture) + ", " + p.Lon.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture) + AdeeboMeter.L.T(") - Remove forgets it and the kept forecast."); }, SetWeatherFor, () => -1, ChangePlace, () => Cfg.WeatherOn && !Policy.NoOnline, "weather city place country location");
            add("Weather", "Accuracy", TagOnline, () => Cfg.WeatherAcc == "better" ? AdeeboMeter.L.T("Better: refreshed every 6 hours while the PC is on (never catching up on missed ones) - at most 4 small requests a day (if the weather service can't be reached, it tries again every 30 minutes until it answers).") : AdeeboMeter.L.T("Normal: one request for today and the next 2 days, then quiet until less than a day is left - about one every 2 days. Rain times 2-3 days out can be off."), SetWeatherAcc, () => Cfg.WeatherAcc == "better" ? 1 : 0, i => { Cfg.WeatherAcc = i == 1 ? "better" : "normal"; Cfg.Save(); Invalidate(true); }, () => Cfg.WeatherOn && !Policy.NoOnline, "weather accuracy refresh");
            add("Weather", "Temperature in", TagPc, () => AdeeboMeter.L.T("How temperatures show in the widget and here."), SetWeatherUnit, () => Cfg.WeatherF ? 1 : 0, i => { Cfg.WeatherF = i == 1; Cfg.Save(); Invalidate(true); }, () => Cfg.WeatherOn, "celsius fahrenheit weather");
            add("Weather", "Weather in the widget", TagPc, () => Cfg.WWeather ? AdeeboMeter.L.T("On: the temperature on the folded strip, and a line like \"4°C · rain from 3 pm\" on the card.") : AdeeboMeter.L.T("Off: no weather on the widget or the buddy's face."), SetOnOff, () => Cfg.WWeather ? 0 : 1, i => { Cfg.WWeather = i == 0; Cfg.Save(); Invalidate(true); }, () => Cfg.WeatherOn, "weather widget strip card");
            // Companion (PLAN 11.7c): words only - data, reports and anything copied stay Plain
            add("Companion", "Fun lines", TagPc, () => Voice.Serious ? AdeeboMeter.L.T("Off: plain words everywhere, the buddy's face stays.") : AdeeboMeter.L.T("On: the buddy talks in the mood and persona below. Facts, numbers and reports never change."), SetOnOff, () => Voice.Serious ? 1 : 0, i => { Voice.Serious = i == 1; Cfg.Save(); Invalidate(true); }, null, "jokes wording tone fun lines plain serious buddy");
            add("Companion", "Mood", TagPc, () => AdeeboMeter.L.T("How the buddy feels: Chill is easy-going, Roast teases the gear (never you), Hype cheers, Support is kind, Bossy gives orders, Emo is relatable."), Lines.MoodNames, () => Voice.Serious ? Lines.Moods.Length - 1 : Math.Max(0, Array.IndexOf(Lines.Moods, Lines.Mood)), i => { if (Lines.Moods[i] == "plain") Voice.Serious = true; else { Voice.Serious = false; Lines.Mood = Lines.Moods[i]; } Cfg.Save(); Invalidate(true); }, null, "mood chill roast hype support bossy emo sarcastic personality");
            add("Companion", "Address me as", TagPc, () => Lines.Addr == "name" ? AdeeboMeter.L.T("Your name: ") + (Lines.Name == "" ? AdeeboMeter.L.T("(not set)") : Lines.Name) + AdeeboMeter.L.T(" - kept only in the settings file.") : AdeeboMeter.L.T("Chosen by you, never guessed. Personas use it their way (boss, my lady, bro...)."), Lines.AddrNames, () => Math.Max(0, Array.IndexOf(Lines.Addrs, Lines.Addr)), i => { if (Lines.Addrs[i] == "name") { string nm = Ask(AdeeboMeter.L.T("What should I call you?"), Lines.Name); if (nm == null) return; Lines.Name = nm; } Lines.Addr = Lines.Addrs[i]; Cfg.Save(); Invalidate(true); }, null, "address name sir maam lady lord bro call me");
            add("Companion", "Buddy face", TagPc, () => Cfg.BuddyFace == "emoji" ? AdeeboMeter.L.T("Emoji faces (Twemoji, CC-BY 4.0) that follow the mood. Now and then it wears a surprise for a day.") : AdeeboMeter.L.T("The drawn face. Now and then it wears a surprise for a day."), SetFace, () => Cfg.BuddyFace == "emoji" ? 1 : 0, i => { Cfg.BuddyFace = i == 1 ? "emoji" : "drawn"; Cfg.Save(); Invalidate(true); }, () => Cfg.Buddy, "face emoji buddy drawn smiley");
            add("Companion", "How chatty", TagPc, () => Lines.Chat == "face" ? AdeeboMeter.L.T("Face only: the buddy shows how it feels, no words.") : Lines.Chat == "quiet" ? AdeeboMeter.L.T("Quiet: speaks for problems and greetings only.") : Lines.Chat == "chatty" ? AdeeboMeter.L.T("Chatty: also idle lines and the rare surprise.") : AdeeboMeter.L.T("Normal: problems, states and tool results."), Lines.ChattyNames, () => Math.Max(0, Array.IndexOf(Lines.Chatty, Lines.Chat)), i => { Lines.Chat = Lines.Chatty[i]; Cfg.Save(); Invalidate(true); }, null, "chatty quiet talk words lines how often");
            add("Companion", "Words of comfort", TagPc, () => Lines.Faith == "off" ? AdeeboMeter.L.T("Off. When on: a short verse from a public-domain translation, word for word with its reference - only in calm moments, never next to a problem or in Roast.") : (Lines.Faith == "common" ? AdeeboMeter.L.T("A line a day on what the faiths share - patience, kindness, gratitude - in plain words, not a quote from any one scripture. Calm moments only.") : AdeeboMeter.L.T("A verse a day from ") + Lines.FaithNames[Array.IndexOf(Lines.Faiths, Lines.Faith)] + AdeeboMeter.L.T(", word for word from a public-domain translation, with its reference. Calm moments only.")), Lines.FaithNames, () => Math.Max(0, Array.IndexOf(Lines.Faiths, Lines.Faith)), i => { Lines.Faith = Lines.Faiths[i]; Cfg.Save(); Invalidate(true); }, null, "faith religion quran bible verse comfort islam christian hindu buddhist sikh jewish prayer");
            add("General", "Adeebo Points", TagPc, () => Cfg.Points ? AdeeboMeter.L.T("On: the Adeebo button in the header - your rank, how to earn, and every feature in one place. ") + Points.Rank(Points.Me.Total) + " · " + Points.Me.Total + AdeeboMeter.L.T(" so far, kept on this PC only.") : AdeeboMeter.L.T("Off: the button is hidden and nothing is earned") + (Points.Me.Total > 0 ? AdeeboMeter.L.T(" - your ") + Points.Me.Total + AdeeboMeter.L.T(" points are kept.") : "."), SetOnOff, () => Cfg.Points ? 0 : 1, i => { Cfg.Points = i == 0; Cfg.Save(); SyncPointsBtn(); if (!Cfg.Points && tab == "points") { tab = "settings"; ShowTab(); } Invalidate(true); }, null, "points adeebo rank reward learn examples dashboard every feature");
            add("General", "Start with Windows", TagPc, () => Program.Packaged ? AdeeboMeter.L.T("Windows keeps this switch: choosing opens Settings › Apps › Startup - turn AdeeboMeter on or off there.") : AdeeboMeter.L.T("Starts quietly when you sign in. Exit from the tray stops it completely - nothing stays behind."), SetS10, () => Cfg.GetAutoStart() ? 0 : 1, i => Cfg.SetAutoStart(i == 0), null, "startup boot login autostart");
            // Navigation (the tab list itself is drawn below the rows - PaintNavSettings)
            add("Navigation", "Layout", TagPc, () => Cfg.Layout == "sidebar" ? AdeeboMeter.L.T("Sidebar: the sections down the left, with search and a live meter. Fold it to icons with the arrow.") : AdeeboMeter.L.T("Classic: tabs along the top, the health panel on the right."), SetLayoutOpts, () => Cfg.Layout == "sidebar" ? 1 : 0, i => SetLayout(i == 1 ? "sidebar" : "classic"), null, "layout sidebar classic tabs menu left top");
            foreach (var hs in Cfg.Hideable)
            {
                string sec = hs, nm = sec == "stream" ? "Streaming" : sec == "trade" ? "Markets" : "Gamer";
                add("Navigation", "Show " + nm, TagPc, () => Cfg.Off(sec) ? AdeeboMeter.L.T("Off: hidden everywhere, and its background checks are stopped.") : AdeeboMeter.L.T("On: in the bar or under More - move it in the list below."), SetS10, () => Cfg.Off(sec) ? 1 : 0, i => { Cfg.SetOff(sec, i == 1); Cfg.Save(); if (i == 1 && tab != null && SecOf(tab) == sec) { tab = "overview"; ShowTab(); } ApplyLayout(); settingsCanvas.Invalidate(); }, null, "hide turn off section " + nm.ToLowerInvariant());
            }
            // Widget
            add("Widget", "Speed in", TagPc, () => Cfg.WUnits == "bytes" ? "MB/s - like a download in your browser." : AdeeboMeter.L.T("Mb/s - like your internet plan and speed tests."), SetUnits, () => Cfg.WUnits == "bytes" ? 1 : 0, i => { Cfg.WUnits = i == 1 ? "bytes" : "bits"; Cfg.Save(); Invalidate(true); }, () => Draw.SpeedStyle, "units bits bytes mbps mb/s speed widget");
            add("Widget", "Show", TagPc, () => AdeeboMeter.L.T("Which way the speed widget shows."), SetDir, () => Cfg.WDir == "down" ? 1 : Cfg.WDir == "up" ? 2 : 0, i => { Cfg.WDir = i == 1 ? "down" : i == 2 ? "up" : "both"; Cfg.Save(); Invalidate(true); }, () => Draw.SpeedStyle, "download upload both direction widget");
            add("Widget", "Mini graph", TagPc, () => Cfg.WGraph ? AdeeboMeter.L.T("The last 30 seconds next to the numbers, and a minute on the card.") : "Numbers only.", SetOnOff, () => Cfg.WGraph ? 0 : 1, i => { Cfg.WGraph = i == 0; Cfg.Save(); Invalidate(true); }, () => Draw.SpeedStyle, "graph sparkline chart widget");
            add("Widget", "Today's total", TagPc, () => Cfg.WTotal ? AdeeboMeter.L.T("Downloaded and uploaded since midnight, on the card.") : "Off.", SetOnOff, () => Cfg.WTotal ? 0 : 1, i => { Cfg.WTotal = i == 0; Cfg.Save(); Invalidate(true); }, () => Draw.SpeedStyle, "total today data usage gb widget");
            add("Widget", "How it behaves", TagPc, () => Cfg.Corner == Moments.Quiet ? AdeeboMeter.L.T("Quiet: the corner never changes by itself - no moments, no evening summary, no words from your view. Just like before 1.1.5.") : Cfg.Corner == Moments.FollowView ? AdeeboMeter.L.T("Follows my view: the Streamer view names what's playing, the Trader view the broker's delay - plus the moments.") : AdeeboMeter.L.T("Quiet + moments: calm all day; the buddy's face changes for a few seconds when something good happens (a clean game session, a stream with no dips, the internet back, a new speed record...). Hover to see why."), SetCorner, () => Cfg.Corner == Moments.Quiet ? 0 : Cfg.Corner == Moments.FollowView ? 2 : 1, i => { Cfg.Corner = i == 0 ? Moments.Quiet : i == 2 ? Moments.FollowView : Moments.WithMoments; Cfg.Save(); }, null, "quiet corner moments emoji face calm widget behaviour classic");
            add("Widget", "Corner widget size", TagPc, () => Cfg.BigWidget ? AdeeboMeter.L.T("A little bigger (1.3x) - easier to read at a glance.") : AdeeboMeter.L.T("Small - out of the way."), SetSize, () => Cfg.BigWidget ? 1 : 0, i => { Cfg.BigWidget = i == 1; Cfg.Save(); Invalidate(true); }, null, "big small scale");
            add("Widget", "Widget style", TagPc, () => (Corner.Holding ? AdeeboMeter.L.T("Held on another card now - tray › Stop holding goes back to this style.") + " " : "") + (NewStyleHelp() ?? (Cfg.WidgetStyle == "gamer" ? AdeeboMeter.L.T("Always the gamer card: your game's ping, jitter, loss and grade.") : Cfg.WidgetStyle == "everyday" ? AdeeboMeter.L.T("Always the everyday card: the verdict, the five dots, CPU, memory and speed.") : Cfg.WidgetStyle == "calls" ? AdeeboMeter.L.T("Always the calls card: jitter, loss and delay - what Teams, Zoom and WhatsApp care about - and the VPN when it's up.") : Cfg.WidgetStyle == "engineer" ? (Cfg.Simple ? AdeeboMeter.L.T("The engineer card needs Pro mode (General > Mode) - until then it shows the everyday card.") : AdeeboMeter.L.T("Always the engineer card: router, internet and DNS delay, jitter, loss, TCP retransmits and resets, throughput, CPU and memory.")) : AdeeboMeter.L.T("Follows what you're doing: the gamer card while a game you added is open (if Show game ping is on), the calls card during a call, the everyday card the rest of the time."))), WStyle, () => Math.Max(0, Array.IndexOf(WStyleIds, Cfg.WidgetStyle)), i => { Cfg.WidgetStyle = WStyleIds[i]; Cfg.Save(); }, null, "layout gamer game everyday card calls teams zoom engineer");
            // 24 Nov: the new presets' choices, the takeovers and the day plan (WidgetPlus.cs)
            add("Widget", "Markets on the widget", TagOnline, () => AdeeboMeter.L.T("Now: ") + string.Join(", ", Corner.Syms()) + ". " + (MarketData.HasQuoteKey() ? AdeeboMeter.L.T("Prices with your Financial Modeling Prep key, at its pace (Settings › Markets), in market hours only - ") + MarketData.CallsUsedToday + AdeeboMeter.L.T(" calls today.") : AdeeboMeter.L.T("Without a Financial Modeling Prep key only currencies show (Bank of Canada, once a day).")) + AdeeboMeter.L.T(" Any symbol your key covers (AAPL, SHOP.TO, ^GSPC), or FX:USD for a currency - up to four."), SetWSyms, () => Cfg.WSyms == "^GSPC,FX:USD,GCUSD" ? 0 : 1, i => { if (i == 0) Cfg.WSyms = "^GSPC,FX:USD,GCUSD"; else { string v = Ask(AdeeboMeter.L.T("Symbols, separated by commas (up to four)"), Cfg.WSyms, false, 90); if (v == null) return; var c = Corner.CleanSyms(v); if (c == "") { AskCard.Show(this, AdeeboMeter.L.T("None of those look like a symbol. Try AAPL, SHOP.TO, ^GSPC or FX:USD."), "AdeeboMeter"); return; } Cfg.WSyms = c; } Cfg.Save(); Invalidate(true); }, () => Cfg.WidgetStyle == "markets" || Cfg.WidgetStyle == "mine" || PlanHas("markets"), "markets stocks symbols ticker quote widget");
            for (int mi = 0; mi < 6; mi++)
            {
                int slot = mi;
                add("Widget", "My widget: reading " + (slot + 1), TagPc, () => AdeeboMeter.L.T("What this row of My widget shows."), MineOpts(), () => { var l = Corner.MineSlots(); return l[slot] == "" ? 0 : 1 + Array.FindIndex(Corner.Fields, f => f[0] == l[slot]); }, i => { Cfg.WMine = Corner.SetMine(slot, i == 0 ? null : Corner.Fields[i - 1][0]); Cfg.Save(); Invalidate(true); }, () => Cfg.WidgetStyle == "mine" || PlanHas("mine"), "my widget custom readings fields pick");
            }
            add("Widget", "Show game ping on the widget", TagPc, () => Cfg.GameWidget ? AdeeboMeter.L.T("While a game you added is open, the widget becomes the gamer card whatever style it's on, stays over the game in borderless full screen, and shows a one-minute match recap when the game closes.") : AdeeboMeter.L.T("Off: the widget keeps your style while you play."), SetOnOff, () => Cfg.GameWidget ? 0 : 1, i => { Cfg.GameWidget = i == 0; Cfg.Save(); }, null, "game gamer widget ping recap");
            add("Widget", "Widget position while gaming", TagPc, () => AdeeboMeter.L.T("Where the widget sits while a game you added is open - away from your minimap, say. Dragging it while you play moves this one. Picking the corner it's in keeps the distance."), SetWPosGame, () => WidgetPos.GameIndex, i => { Cfg.WPosGame = WidgetPos.PickGame(i); Cfg.Save(); }, () => Cfg.GameWidget, "game corner position minimap");
            add("Widget", "Switch to sports for live games of", TagOnline, () => Corner.Kinds().Count == 0 ? AdeeboMeter.L.T("Off. A single game can still go on the widget: Show on widget, on the Sports page.") : AdeeboMeter.L.T("While a team you follow plays, the widget shows the score; 5 minutes after the final it goes back to yours. Not on battery."), SetWSports, () => { var k = Corner.Kinds(); return k.Count == 0 ? 0 : k.Count >= 3 ? 4 : k.Contains("hockey") ? 1 : k.Contains("football") ? 2 : 3; }, i => { Cfg.WSportKinds = i == 0 ? "" : i == 4 ? "hockey,football,cricket" : i == 1 ? "hockey" : i == 2 ? "football" : "cricket"; Cfg.Save(); }, () => Cfg.Sports, "sports live score switch widget hockey football cricket");
            add("Widget", "Scores: how often", TagOnline, () => AdeeboMeter.L.T("While a game you follow is live (and only then). Hockey needs no key. Football and cricket use your own keys: every 2 minutes or every minute needs a paid plan. Cricket calls today: ") + Sports.CricketCallsToday + (Sports.PaidPace ? "." : AdeeboMeter.L.T(" of 90 (the free key allows 100).")), SetSportsEvery, () => Cfg.SportsEvery == "1" ? 2 : Cfg.SportsEvery == "2" ? 1 : 0, i => { Cfg.SportsEvery = i == 2 ? "1" : i == 1 ? "2" : "5"; Cfg.Save(); }, () => Cfg.Sports, "sports scores pace often key paid live");
            add("Widget", "Weather before rain or snow", TagOnline, () => Cfg.WRainSoon ? AdeeboMeter.L.T("On: an hour before rain, snow or a storm (by the forecast), the widget shows the weather, and goes back once it has started. It's a forecast from Open-Meteo - not radar or official warnings.") : AdeeboMeter.L.T("Off."), SetOnOff, () => Cfg.WRainSoon ? 0 : 1, i => { Cfg.WRainSoon = i == 0; Cfg.Save(); }, () => Cfg.WeatherOn, "weather rain snow storm switch widget");
            add("Widget", "Day plan: morning (7-9)", TagPc, () => AdeeboMeter.L.T("A preset the widget switches to for this part of the day, then back to yours. Not on battery."), PlanOpts(), () => PlanIndex("m"), i => PlanPick("m", i), null, "day plan morning schedule widget");
            add("Widget", "Day plan: working hours (9-17)", TagPc, () => AdeeboMeter.L.T("A preset the widget switches to for this part of the day, then back to yours. Not on battery."), PlanOpts(), () => PlanIndex("w"), i => PlanPick("w", i), null, "day plan work schedule widget");
            add("Widget", "Day plan: evening (17-23)", TagPc, () => AdeeboMeter.L.T("A preset the widget switches to for this part of the day, then back to yours. Not on battery."), PlanOpts(), () => PlanIndex("e"), i => PlanPick("e", i), null, "day plan evening schedule widget");
            add("Widget", "Day plan: night (23-7)", TagPc, () => AdeeboMeter.L.T("A preset the widget switches to for this part of the day, then back to yours. Not on battery."), PlanOpts(), () => PlanIndex("n"), i => PlanPick("n", i), null, "day plan night schedule widget");
            add("Widget", "Since you looked", TagPc, () => Cfg.WSince ? AdeeboMeter.L.T("On: when you point at the widget after a while, a blue line says what changed - a goal, a price move, the rain moving, a network event.") : AdeeboMeter.L.T("Off."), SetOnOff, () => Cfg.WSince ? 0 : 1, i => { Cfg.WSince = i == 0; Cfg.Save(); }, null, "since changed catch up widget");
            add("Widget", "Buddy", TagPc, () => Cfg.Buddy ? AdeeboMeter.L.T("A little face on the widget that feels what your network feels: smug when all is green, sweaty when your PC is busy, dizzy when your Wi-Fi is bad, betrayed when your provider lets you down. Asleep on the folded chip.") : AdeeboMeter.L.T("Off: the widget shows the logo and the coloured dot."), SetOnOff, () => Cfg.Buddy ? 0 : 1, i => { Cfg.Buddy = i == 0; Cfg.Save(); Invalidate(true); }, null, "buddy face creature mascot emoji widget");
            add("Widget", "Buddy's hat", TagPc, () => { int i = Array.IndexOf(Draw.Hats, Cfg.BuddyHat), have = Points.Me.Total; string next = string.Join(", ", Enumerable.Range(1, Draw.Hats.Length - 1).Where(k => have < Draw.HatPts[k]).Select(k => SetHat[k] + " at " + Draw.HatPts[k])); return (i > 0 && have < Draw.HatPts[i] ? AdeeboMeter.L.T("Locked - you have ") + have + AdeeboMeter.L.T(" points. ") : AdeeboMeter.L.T("Earned with points from Learn and the games. ")) + (next != "" ? AdeeboMeter.L.T("Still to unlock: ") + next + "." : "All unlocked.") + AdeeboMeter.L.T(" At Halloween and in December it dresses up on its own unless you pick one."); }, SetHat, () => Math.Max(0, Array.IndexOf(Draw.Hats, Cfg.BuddyHat)), i => { if (i > 0 && Points.Me.Total < Draw.HatPts[i]) return; Cfg.BuddyHat = Draw.Hats[i]; Cfg.Save(); Invalidate(true); }, () => Cfg.Buddy && Cfg.Points, "hat skin crown party cap unlock buddy");
            add("Widget", "How visible", TagPc, () => AdeeboMeter.L.T("How strongly the corner widget shows when you're not hovering it."), SetS14, () => Array.IndexOf(SetN15, Cfg.Fade), i => { Cfg.Fade = SetN15[i]; Cfg.Save(); }, null, "fade opacity transparent");
            add("Widget", "Widget position", TagPc, () => (Cfg.WPos == "" ? AdeeboMeter.L.T("Bottom right, the default. To move it, rest the mouse still on it for a moment, then drag - or tray › Corner widget › Move widget. A click opens AdeeboMeter.") : AdeeboMeter.L.T("Moved by you. Picking the corner it's in keeps the distance; another corner puts it right in that corner. Reset puts it back in the bottom-right corner of the main screen.")) + (Cfg.WidgetScreen != "" ? " " + (Screen.AllScreens.Any(z => z.DeviceName == Cfg.WidgetScreen) ? AdeeboMeter.L.T("On another screen - see Widget on screen.") : AdeeboMeter.L.T("Its screen isn't connected, so it's in the bottom-right corner of the main screen until that screen is back.")) : "")   /* hunters12 lifecycle L-3 */ + (Corner.UseGamePos ? " " + AdeeboMeter.L.T("A game is open, so the widget is at its gaming spot now - this corner applies when the game closes.") : ""), SetWPos, () => Math.Max(0, WidgetPos.NormalCornerIndex), i => { if (i == 4) { if (AppCtx.ResetWidget != null) AppCtx.ResetWidget(); else { Cfg.WPos = ""; Cfg.WidgetScreen = ""; Cfg.WPosGame = ""; Cfg.Save(); } } else { Cfg.WPos = WidgetPos.Pick(i); Cfg.Save(); } Invalidate(true); }, null, "position move corner place drag reset top left right bottom widget");
            add("Widget", "Widget on screen", TagPc, () => AdeeboMeter.L.T("Which screen the corner widget sits on. If that screen is unplugged, it goes back to the main one."), ScreenOpts(), () => { var all = Screen.AllScreens; int k = Array.FindIndex(all, z => z.DeviceName == Cfg.WidgetScreen); return Cfg.WidgetScreen == "" || k < 0 ? Math.Max(0, Array.FindIndex(all, z => z.Primary)) : k; }, i => { var all = Screen.AllScreens; if (i >= 0 && i < all.Length) { Cfg.WidgetScreen = all[i].Primary ? "" : all[i].DeviceName; Cfg.Save(); } }, () => Screen.AllScreens.Length > 1, "screen monitor display second widget");
            add("Widget", "Fade fully when the mouse comes near", TagPc, () => Cfg.WHideNear ? AdeeboMeter.L.T("On: the widget disappears as the mouse comes near, so it never covers what you point at - except when a window covers its corner, where resting on it still opens it. To move it, use the tray: Corner widget > Move widget.") : AdeeboMeter.L.T("Off: it brightens when you point at it."), SetOnOff, () => Cfg.WHideNear ? 0 : 1, i => { Cfg.WHideNear = i == 0; Cfg.Save(); }, null, "fade hide disappear mouse near transparent widget");
            add("Widget", "Move hints on the widget", TagPc, () => Cfg.WHint ? AdeeboMeter.L.T("On: a blue line on the card says how to move it while you rest on it or it's unlocked.") : AdeeboMeter.L.T("Off: no move hints. Moving still works the same way; Since you looked has its own switch."), SetOnOff, () => Cfg.WHint ? 0 : 1, i => { Cfg.WHint = i == 0; Cfg.Save(); }, null, "hint tip move help widget");
            add("Widget", "Hover wait on the folded chip", TagPc, () => AdeeboMeter.L.T("How long to rest the mouse on the folded chip before the card opens - only when a window covers the corner (with nothing over it, the card opens as soon as you point at it)."), SetS16, () => Array.IndexOf(SetN17, Cfg.DwellMs), i => { Cfg.DwellMs = SetN17[i]; Cfg.Save(); }, null, "chip delay mouse");
            // Background checks
            add("Background checks", "Router ping", TagLan, () => Cfg.GwSec == 0 ? AdeeboMeter.L.T("Off: the LAN / Wi-Fi dot goes grey and slow-router alerts stop.") : AdeeboMeter.L.T("Pings your router ") + Every(Cfg.GwSec) + AdeeboMeter.L.T(" - tells Wi-Fi trouble apart from internet trouble."), SetS2, () => Array.IndexOf(SetN3, Cfg.GwSec), i => { Cfg.GwSec = SetN3[i]; Custom(); }, null, "ping gateway lan wifi");
            add("Background checks", "Internet and DNS check", TagNet, () => Cfg.NetSec == 0 ? AdeeboMeter.L.T("Off: no 'internet went quiet' alerts; the Internet and DNS dots go grey.") : AdeeboMeter.L.T("One tiny ping to a public server ") + Every(Cfg.NetSec) + AdeeboMeter.L.T(", and one name lookup at most once a minute (every 15 s with this window open)."), SetS4, () => Array.IndexOf(SetN5, Cfg.NetSec), i => { Cfg.NetSec = SetN5[i]; Custom(); }, null, "ping internet dns 1.1.1.1 8.8.8.8");
            add("Background checks", "Wi-Fi watch", TagPc, () => Cfg.WifiSec == 0 ? AdeeboMeter.L.T("Off: no roam, signal or channel alerts.") : AdeeboMeter.L.T("Asks Windows for signal, channel and access point ") + Every(Cfg.WifiSec) + AdeeboMeter.L.T(" (every 10 s while this window is open). It asks Windows' Wi-Fi service directly - cheap. Only if that service won't answer does it start a small Windows tool (netsh) instead."), SetS6, () => Array.IndexOf(SetN7, Cfg.WifiSec), i => { Cfg.WifiSec = SetN7[i]; Custom(); }, null, "wifi signal roam");
            add("Background checks", "App and connection watch", TagPc, () => Cfg.AppsSec == 0 ? AdeeboMeter.L.T("Off: the verdict can't name the app to close, and connection bursts go unnoticed.") : AdeeboMeter.L.T("Reads the process list and connection table ") + Every(Cfg.AppsSec) + AdeeboMeter.L.T(" - powers 'top apps', connection bursts and stuck-connection alerts."), SetS8, () => Array.IndexOf(SetN9, Cfg.AppsSec), i => { Cfg.AppsSec = SetN9[i]; Custom(); }, null, "apps processes connections");
            add("Background checks", "Teams call detection", TagPc, () => Cfg.Call ? AdeeboMeter.L.T("Notices when a Teams call starts and watches the network more closely during it.") : AdeeboMeter.L.T("Off: calls get no extra attention."), SetS10, () => Cfg.Call ? 0 : 1, i => { Cfg.Call = i == 0; Custom(); }, null, "teams call meeting");
            add("Background checks", "Watch my first pinned destination all day", TagNet, () => Cfg.Pins.Count == 0 ? AdeeboMeter.L.T("Pin a destination on Check a destination first.") : Cfg.PinWatch ? AdeeboMeter.L.T("One ping to ") + Cfg.Pins[0] + AdeeboMeter.L.T(" every 30 s, graphed on Check a destination.") : AdeeboMeter.L.T("Off - pinned destinations are only checked when you press Run all."), SetOnOff, () => Cfg.PinWatch ? 0 : 1, i => { Cfg.PinWatch = i == 0 && Cfg.Pins.Count > 0; Cfg.Save(); }, null, "pin ping destination");
            // Gamer Zone
            add("Gamer Zone", "Game ping in the background", TagGame, () => Games.Monitored().Count == 0 ? AdeeboMeter.L.T("Nothing is sent to game servers until you click a game in the Gamer Zone.") : Cfg.GameBgSec == 0 ? AdeeboMeter.L.T("Off: game servers are only checked while a game is open.") : AdeeboMeter.L.T("Checks your games' best regions ") + Every(Cfg.GameBgSec) + AdeeboMeter.L.T(" while AdeeboMeter runs, so the 7-day verdict covers every hour - not only when you play."), GzBg, () => Array.IndexOf(GzBgN, Cfg.GameBgSec), i => { Cfg.GameBgSec = GzBgN[i]; Custom(); }, null, "game ping gamer background");
            add("Gamer Zone", "Servers watched in the background", TagGame, () => AdeeboMeter.L.T("Each added game's best region, up to this many. 5 also lets a game you play be watched on up to 16 servers (otherwise 8)."), GzSrv, () => Array.IndexOf(GzSrvN, Cfg.GameBgServers), i => { Cfg.GameBgServers = GzSrvN[i]; Custom(); }, () => Cfg.GameBgSec > 0, "game servers gamer");
            add("Gamer Zone", "Every second while a game is open", TagGame, () => Cfg.GameFast ? AdeeboMeter.L.T("All of that game's servers (up to ") + GamePing.MaxServers + AdeeboMeter.L.T("), once a second - for spikes and the match report.") : AdeeboMeter.L.T("Off: the game's servers stay at the background pace."), SetOnOff, () => Cfg.GameFast ? 0 : 1, i => { Cfg.GameFast = i == 0; Cfg.Save(); }, null, "game fast ping gamer");
            add("Gamer Zone", "Start when a game opens", TagPc, () => Cfg.GameAuto ? AdeeboMeter.L.T("Notices when a game you added starts (by its program name) and switches to watching it closely.") : AdeeboMeter.L.T("Off: games are only watched at the background pace."), SetOnOff, () => Cfg.GameAuto ? 0 : 1, i => { Cfg.GameAuto = i == 0; Cfg.Save(); }, null, "game auto start detect gamer");
            // Streaming & trading (PLAN-modes.md): the same switches as on their pages, asked as one question
            add("Streaming & trading", "Notice streams", TagPc, () => Cfg.Streams ? AdeeboMeter.L.T("On: notices Netflix, YouTube, Prime, Twitch, IPTV players and more by their server names (never your tabs or titles) and keeps a record of each stream's dips and whose fault they were.") + (Cfg.Off("stream") ? AdeeboMeter.L.T(" The Streaming page is hidden in Navigation, so nothing is watched.") : "") : AdeeboMeter.L.T("Off: streams are not noticed or recorded."), SetS10, () => Cfg.Streams ? 0 : 1, i => { Cfg.Streams = i == 0; Cfg.Save(); }, null, "stream streaming netflix youtube iptv video buffering");
            add("Streaming & trading", "Your broker", TagNet, () => Cfg.TradeLive && Cfg.TradeHost != "" ? AdeeboMeter.L.T("Watching ") + BrokerName(Cfg.TradeHost) + AdeeboMeter.L.T(": a light knock ") + Every(Trading.Sec) + AdeeboMeter.L.T(" while the market is open (9:30-16:00 Eastern, weekdays, holidays skipped)") + (Cfg.TradeCrypto ? AdeeboMeter.L.T(", or around the clock for crypto") : "") + AdeeboMeter.L.T(". It never logs in or sees your account.") : AdeeboMeter.L.T("Not watched. Pick your broker and AdeeboMeter checks the line to it in market hours, so you know before you click Buy whether your connection is the problem."), BrokerOpts(), BrokerIdx, i => SetBroker(i), null, "broker trading stock market wealthsimple questrade trade latency");
            add("Streaming & trading", "Broker alerts", TagPc, () => Cfg.TradeAlert ? AdeeboMeter.L.T("On: a Windows notice when the line to your broker turns slow (over 100 ms) or drops - at most every 2 minutes.") : AdeeboMeter.L.T("Off: slow moments are only on the Trading page."), SetS10, () => Cfg.TradeAlert ? 0 : 1, i => { Cfg.TradeAlert = i == 0; Cfg.Save(); }, () => Cfg.TradeLive && Cfg.TradeHost != "", "broker alert notification trading");
            add("Streaming & trading", "Crypto, around the clock", TagNet, () => Cfg.TradeCrypto ? AdeeboMeter.L.T("On: your broker is watched 24/7, weekends too - crypto never closes.") : AdeeboMeter.L.T("Off: only in stock-market hours."), SetS10, () => Cfg.TradeCrypto ? 0 : 1, i => { Cfg.TradeCrypto = i == 0; Cfg.Save(); }, () => Cfg.TradeLive && Cfg.TradeHost != "", "crypto bitcoin 24/7 broker");
            add("Streaming & trading", "Day's numbers", TagOnline, () => Policy.NoOnline ? AdeeboMeter.L.T("Off - your IT team turned online lookups off (AdeeboMeter.policy).") : Cfg.TradeFx ? AdeeboMeter.L.T("On: currencies (daily) and commodity prices (weekly) from the Bank of Canada - free, official, no account. Fetched only when the Trading page or the evening summary needs newer numbers.") : AdeeboMeter.L.T("Off: no currencies or commodities."), SetS10, () => Cfg.TradeFx ? 0 : 1, i => { Cfg.TradeFx = i == 0; Cfg.Save(); }, null, "currency exchange rate fx usd euro oil gas commodities bank of canada");
            add("Streaming & trading", "Evening summary in the widget", TagPc, () => Cfg.TradeEvening ? AdeeboMeter.L.T("On: weekdays 17:30-19:00 the folded widget shows the day's close (US dollar, energy's week) instead of \"All quiet\", then goes back by itself. A problem always takes the spot.") : AdeeboMeter.L.T("Off: the widget never changes by itself."), SetS10, () => Cfg.TradeEvening ? 0 : 1, i => { Cfg.TradeEvening = i == 0; Cfg.Save(); }, () => Cfg.TradeFx, "evening summary widget close currency oil");
            add("Gamer Zone", "Keep game history for", TagPc, () => AdeeboMeter.L.T("One line a minute per server, on this PC only, with the rest of the history") + (Cfg.History ? "." : AdeeboMeter.L.T(" - history is off in History & data, so nothing is kept.")), GzDays, () => Array.IndexOf(GzDaysN, Cfg.GameDays), i => { Cfg.GameDays = GzDaysN[i]; Cfg.Save(); }, null, "game history days gamer");
            // Alerts
            add("Alerts", "Alerts", TagPc, () => Cfg.Alerts ? AdeeboMeter.L.T("A Windows notification only when there's something to do (Wi-Fi keeps dropping, internet gone, slower than usual, a suspicious new program...). Silent while you present.") : AdeeboMeter.L.T("Off: AdeeboMeter never pops up; everything still lands in What changed."), SetOnOff, () => Cfg.Alerts ? 0 : 1, i => { Cfg.Alerts = i == 0; Cfg.Save(); }, null, "notification popup toast");
            add("Alerts", "Quiet hours", TagPc, () => Cfg.Quiet ? AdeeboMeter.L.T("No alerts between 22:00 and 07:00.") : AdeeboMeter.L.T("Alerts any time of day."), SetOnOff, () => Cfg.Quiet ? 0 : 1, i => { Cfg.Quiet = i == 0; Cfg.Save(); }, () => Cfg.Alerts, "night silent");
            add("Alerts", "Watch and suggest", TagPc, () => Cfg.Watch ? AdeeboMeter.L.T("Notices programs talking to the internet for the first time, from odd folders or at night, and big uploads - and suggests what to look at. It never blocks anything. (First 24 hours: learning what's normal.)") : "Off.", SetOnOff, () => Cfg.Watch ? 0 : 1, i => { Cfg.Watch = i == 0; Cfg.Save(); }, null, "suspicious program upload");
            add("Alerts", "Monthly data cap", TagPc, () => Cfg.CapGB > 0 ? AdeeboMeter.L.T("Warns at 80% of ") + Cfg.CapGB + AdeeboMeter.L.T(" GB (hotspots, metered plans). Counted on this PC from Windows' own counters.") : AdeeboMeter.L.T("Off - usage is still counted in What changed."), CapNames, () => Array.IndexOf(Cfg.CapChoices, Cfg.CapGB), i => { Cfg.CapGB = Cfg.CapChoices[i]; Cfg.Save(); }, null, "data usage limit gb metered");
            // History & data
            add("History & data", "History", TagPc, () => Cfg.History ? AdeeboMeter.L.T("One small line a minute plus every change, on this PC only (") + Usage.Mb(HistBytesCached() / 1e6) + AdeeboMeter.L.T(" now; about 15 KB a day once compressed). Powers past days, the week heatmap and 'slower than usual', and Neighbour wars (the Wi-Fi networks Windows lists, once an hour, 7 days).") : AdeeboMeter.L.T("Off: nothing is written to disk (the Neighbour wars log is deleted too) - What changed only remembers the last 2 hours, in memory."), SetOnOff, () => Cfg.History ? 0 : 1, i => { Cfg.History = i == 0; Cfg.Save(); }, null, "time machine record disk");
            add("History & data", "Keep history for", TagPc, () => "Every night anything older goes (rolling). 6 months of minutes is still only a few MB (games you watch add a little more) - and before it rolls off, AdeeboMeter writes your six-month review (Toolbox > Six months on your network) to keep or share." + (Policy.MaxHistoryDays > 0 ? AdeeboMeter.L.T(" Your IT team keeps it to ") + Policy.MaxHistoryDays + AdeeboMeter.L.T(" days at most.") : ""), HistDayNames, () => Array.IndexOf(Cfg.HistDayChoices, Cfg.HistoryDays) >= 0 || Policy.MaxHistoryDays == 0 ? Array.FindLastIndex(Cfg.HistDayChoices, d => d <= Cfg.HistoryDays) : -1,   /* an IT limit that isn't one of the choices (5 days): none lit - the help says the real number */ i => { int d = Cfg.HistDayChoices[i]; Cfg.HistoryDays = Policy.MaxHistoryDays > 0 ? Math.Min(d, Policy.MaxHistoryDays) : d; Cfg.Save(); System.Threading.ThreadPool.QueueUserWorkItem(delegate { try { History.Tidy(); } catch (Exception e) { Program.Log(AdeeboMeter.L.T("history tidy: ") + e.Message); } }); }, () => Cfg.History, "days retention");   /* off the window's thread: the six-month review can take seconds */
            // Privacy & online lookups
            add("Privacy & online lookups", "BGP and who owns the network", TagOnline, () => Online.On ? "On for " + Math.Ceiling(Online.Left.TotalMinutes) + AdeeboMeter.L.T(" more min: public IP addresses (never private ones or names you typed) go to RIPEstat and PeeringDB to name each network on the path, its route and RPKI.") : AdeeboMeter.L.T("Off. When on, Toolbox can name every network on your path (AS numbers), where they hand over, the destination's route and RPKI, and how the internet reaches you."), SetOnOff, () => Online.On ? 0 : 1, i => { if (i == 0) Online.Start(); else Online.Stop(); SyncOnlineChip(); Invalidate(true); }, null, "online bgp asn ripe peeringdb");
            add("Privacy & online lookups", AdeeboMeter.L.T("Switch them off by themselves after"), TagOnline, () => AdeeboMeter.L.T("So they're never left on by mistake."), SetOnlineMin, () => Array.IndexOf(Cfg.OnlineChoices, Cfg.OnlineMin), i => { Cfg.OnlineMin = Cfg.OnlineChoices[i]; Cfg.Save(); if (Online.On) Online.Start(); }, null, "timer online");
            add("Privacy & online lookups", AdeeboMeter.L.T("Sign-in page check"), TagWin, () => Cfg.Portal ? AdeeboMeter.L.T("Only inside a check you start (What's wrong? and the full checkup): one tiny request to the same test page Windows uses, to catch hotel and airport sign-in pages.") : AdeeboMeter.L.T("Off: a failed check won't look for a Wi-Fi sign-in page."), SetOnOff, () => Cfg.Portal ? 0 : 1, i => { Cfg.Portal = i == 0; Cfg.Save(); }, null, "captive portal hotel");
            add("Privacy & online lookups", AdeeboMeter.L.T("Certificate revocation checks"), TagOnline, () => Policy.NoOnline ? AdeeboMeter.L.T("Off - your IT team turned online lookups off (AdeeboMeter.policy).") : CertCfg.Revocation ? AdeeboMeter.L.T("On: Chain check and HTTPS check also ask a certificate's issuer whether it was withdrawn (its revocation list or OCSP address - one extra connection, 10 s at most).") : AdeeboMeter.L.T("Off. When on, Chain check and HTTPS check also ask a certificate's issuer whether it was withdrawn (revoked) - one extra connection to the issuer."), SetS10, () => CertCfg.Revocation ? 0 : 1, i => { CertCfg.Revocation = i == 0 && !Policy.NoOnline; Cfg.Save(); }, null, "crl ocsp revoked revocation certificate");
            add("Privacy & online lookups", "Names for IP addresses", TagPc, () => Cfg.IpNames ? AdeeboMeter.L.T("Reads names from Windows' own DNS cache - no network traffic.") : AdeeboMeter.L.T("Off: connections show bare IP addresses."), SetS10, () => Cfg.IpNames ? 0 : 1, i => { Cfg.IpNames = i == 0; Cfg.Save(); }, null, "dns names hostnames");
            // Capture & advanced
            add("Capture & advanced", "Live capture stops after", TagWin, () => "tshark is the heaviest thing AdeeboMeter can run, so it always stops by itself.", SetS11, () => Array.IndexOf(SetN12, Cfg.LiveMin), i => { Cfg.LiveMin = SetN12[i]; Cfg.Save(); }, null, "wireshark tshark pcap");
            add("Capture & advanced", "Sniff records for", TagWin, () => AdeeboMeter.L.T("How long a row's Sniff button captures before opening Wireshark."), SetS13, () => Cfg.SniffSec == 60 ? 1 : 0, i => { Cfg.SniffSec = i == 1 ? 60 : 30; Cfg.Save(); }, null, "wireshark sniff capture");
            return L;
        }
        // self-test: sections, tags and the search rule (no window needed - the rows are built without painting)
        internal static bool SettingsSelfCheck(List<string> bad)
        {
            var it = new SetItem { Sec = "Alerts", Label = "Alerts", Keys = "", Help = () => "Wi-Fi keeps dropping" };
            if (SetMatch(it, "ping")) bad.Add("'ping' matched 'dropping'");
            if (!SetMatch(it, "drop")) bad.Add("'drop' missed 'dropping'");
            if (!SetMatch(new SetItem { Sec = "Background checks", Label = "Router ping", Help = () => "" }, "router PING")) bad.Add("case");
            return bad.Count == 0;
        }
        static bool SetMatch(SetItem it, string q)
        {
            if (q.Length == 0) return true;
            // every typed word must start a word somewhere in the setting ("ping" finds Router ping, not "dropping")
            // the same split for the setting and for what you type ("Wi-Fi" -> wi + fi, "français" stays whole); "wifi" also finds "Wi-Fi"
            const string sep = @"[^\p{L}0-9.]+";
            string text = (it.Sec + " " + it.Label + " " + it.Keys + " " + it.Help() + (AdeeboMeter.L.Code != "en" ? " " + AdeeboMeter.L.T(it.Sec ?? "") + " " + AdeeboMeter.L.T(it.Label ?? "") : "")).ToLowerInvariant();   /* review 1.1.6 U3 #8: the names shown are translated */
            var words = Regex.Split(text, sep).Concat(Regex.Split(text.Replace("-", ""), sep)).ToList();
            return Regex.Split(q.ToLowerInvariant(), sep).Where(t => t != "").All(t => words.Any(wd => wd.StartsWith(t, StringComparison.Ordinal)));
        }
        void PaintSettings(Graphics g)
        {
            g.TranslateTransform(settingsCanvas.AutoScrollPosition.X, settingsCanvas.AutoScrollPosition.Y);
            Begin(settingsCanvas);
            string q = settingsSearch == null ? "" : settingsSearch.Box.Text.Trim();
            if (q.Length > 60) q = q.Substring(0, 60);
            var items = SetItems().Where(it => it.Show == null || it.Show()).ToList();
            var hits = items.Where(it => SetMatch(it, q)).ToList();
            float mx = 16 * S, mw = 210 * S, x = mx + mw + 26 * S, y = 18 * S, w = Math.Min(settingsCanvas.ClientSize.Width - x - 24 * S, 940 * S), my = 18 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted)) using (var sub = new SolidBrush(Theme.Sub))
            using (var blue = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            using (var nowrap = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center })
            {
                // side menu: All settings (the question cards), then each question by its short name
                {
                    var ar = new RectangleF(mx, my, mw, 36 * S); bool aon = q.Length == 0 && setSec == "";
                    if (aon) Theme.Fill(g, ar, 8 * S, Theme.BlueSoft);
                    using (var tb = new SolidBrush(aon ? (Theme.Dark ? Theme.Ink : Theme.BlueDark) : Theme.Sub)) g.DrawString(AdeeboMeter.L.T("All settings"), aon ? Draw.fBodyB : Draw.fBody, tb, new RectangleF(ar.X + 12 * S, ar.Y, ar.Width - 44 * S, ar.Height), nowrap);
                    Act(settingsCanvas, ar, delegate { setSec = ""; if (settingsSearch != null) settingsSearch.Box.Text = ""; settingsCanvas.AutoScrollPosition = Point.Empty; settingsCanvas.Invalidate(); });
                    my += 44 * S;
                }
                foreach (var sec in SetSections)
                {
                    var r = new RectangleF(mx, my, mw, 36 * S); bool on = q.Length == 0 && sec == setSec;
                    if (on) Theme.Fill(g, r, 8 * S, Theme.BlueSoft);
                    int n = q.Length > 0 ? hits.Count(h => h.Sec == sec) : 0;
                    using (var tb = new SolidBrush(on ? (Theme.Dark ? Theme.Ink : Theme.BlueDark) : Theme.Sub)) g.DrawString(AdeeboMeter.L.T(sec), on ? Draw.fBodyB : Draw.fBody, tb, new RectangleF(r.X + 12 * S, r.Y, r.Width - 44 * S, r.Height), nowrap);
                    if (n > 0) g.DrawString(n.ToString(CultureInfo.InvariantCulture), Draw.fSmall, muted, new RectangleF(r.Right - 28 * S, r.Y, 24 * S, r.Height), nowrap);
                    string s2 = sec; Act(settingsCanvas, r, delegate { setSec = s2; if (settingsSearch != null) settingsSearch.Box.Text = ""; settingsCanvas.AutoScrollPosition = Point.Empty; settingsCanvas.Invalidate(); });
                    my += 40 * S;
                }
                my += 14 * S;
                using (var line = new Pen(Theme.Line)) g.DrawLine(line, mx, my, mx + mw, my); my += 12 * S;
                var ex = new RectangleF(mx + 12 * S, my, mw - 12 * S, 24 * S); g.DrawString(AdeeboMeter.L.T("Export settings..."), Draw.fBodyB, blue, ex.X, ex.Y); Act(settingsCanvas, ex, ExportSettings); my += 30 * S;
                var im = new RectangleF(mx + 12 * S, my, mw - 12 * S, 24 * S); g.DrawString(AdeeboMeter.L.T("Import settings..."), Draw.fBodyB, blue, im.X, im.Y); Act(settingsCanvas, im, ImportSettings); my += 30 * S;
                if (settingsNote != "") { var nh0 = Draw.Measure(settingsNote, Draw.fSmall, (int)(mw - 12 * S)).Height; g.DrawString(settingsNote, Draw.fSmall, muted, new RectangleF(mx + 12 * S, my, mw - 12 * S, nh0 + 4)); my += nh0 + 8 * S; }
                string where = Program.Packaged ? AdeeboMeter.L.T("Saved in your AppData folder. Nothing leaves this PC.") : AdeeboMeter.L.T("Saved next to AdeeboMeter.exe. Nothing leaves this PC.");
                var wh = Draw.Measure(where, Draw.fSmall, (int)(mw - 12 * S)).Height; g.DrawString(where, Draw.fSmall, muted, new RectangleF(mx + 12 * S, my, mw - 12 * S, wh + 4)); my += wh + 8 * S;

                // header: what this PC sends right now
                string now = SendingNow();
                var nhh = Draw.Measure(now, Draw.fSmall, (int)w).Height; g.DrawString(now, Draw.fSmall, sub, new RectangleF(x, y, w, nhh + 4)); y += nhh + 12 * S;

                if (q.Length > 0)
                {
                    g.DrawString(hits.Count == 0 ? AdeeboMeter.L.T("Nothing matches \"") + q + "\"" : hits.Count + (hits.Count == 1 ? AdeeboMeter.L.T(" setting matches \"") : AdeeboMeter.L.T(" settings match \"")) + q + "\"", Draw.fTitle, ink, x, y); y += 34 * S;
                    string last = null;
                    foreach (var it in hits)
                    {
                        if (it.Sec != last) { y = SetHead(g, AdeeboMeter.L.T(it.Sec), x, y, w); last = it.Sec; }
                        y = SetRow(g, x, y, w, AdeeboMeter.L.T(it.Label), AdeeboMeter.L.T(it.Help()), it.Opts.Select(L.T).ToArray(), it.Cur(), it.Set, it.Tag);
                    }
                }
                else if (setSec == "") y = PaintSettingsHome(g, x, y, w, items);
                else
                {
                    // a question page: back, the question, the big choices, then everything else under Fine-tune
                    var bk = Kit.Back(g, "All settings", x, y, SetQLookOf(setSec)[1], false); Act(settingsCanvas, bk, delegate { setSec = ""; settingsCanvas.AutoScrollPosition = Point.Empty; settingsCanvas.Invalidate(); }); y += 50 * S;
                    y = Kit.Header(g, x, y, w, SetQLookOf(setSec)[0], SetQLookOf(setSec)[2], AdeeboMeter.L.T(setSec), SetQLookOf(setSec)[1]);
                    foreach (var lab in BigOnes(setSec)) { var bi = items.FirstOrDefault(i2 => i2.Sec == setSec && i2.Label == lab); if (bi != null) y = PaintBigChoice(g, x, y, w, bi, SetQLookOf(setSec)[1]); }
                    if (setSec == "Widget") y = PaintWidgetPicker(g, x, y, w);
                    if (setSec == "Background checks")
                    {
                        string lead = AdeeboMeter.L.T("The widget always stays light on the PC. The deep stuff (live capture, the full connection list, path checks) only runs while this window is open. These decide how much watching happens in the background - checks a minute, plus game pings once you watch a game.");
                        var lh = Draw.Measure(lead, Draw.fBody, (int)w).Height; g.DrawString(lead, Draw.fBody, sub, new RectangleF(x, y, w, lh + 4)); y += lh + 14 * S;
                        string[][] presets = { new[] { "penny", "Penny", AdeeboMeter.L.T("The lightest touch. Router every 30 s, internet every 5 min, Wi-Fi every 2 min (every 10 s while this window is open), no call watching.") }, SetS0, SetSG };
                        int cols = w > 640 * S ? 3 : 2; float pw = (w - (cols - 1) * 12 * S) / cols, ph = 112 * S;
                        for (int i = 0; i < presets.Length; i++)
                        {
                            var r = new RectangleF(x + (i % cols) * (pw + 12 * S), y + (i / cols) * (ph + 12 * S), pw, ph); bool on = Cfg.Preset == presets[i][0];
                            Theme.Fill(g, r, 12 * S, on ? Theme.BlueSoft : Theme.Card); Theme.Stroke(g, r, 12 * S, on ? Theme.Blue : Theme.Line, on ? 2 : 1);
                            // how much it watches, as a short bar of steps (24 Nov mockup): one more step per level
                            { float bx = r.X + 14 * S, bw = (r.Width - 28 * S - (presets.Length - 1) * 4 * S) / presets.Length, by2 = r.Bottom - 14 * S;
                              for (int k = 0; k < presets.Length; k++) Theme.Fill(g, new RectangleF(bx + k * (bw + 4 * S), by2, bw, 4 * S), 2 * S, k <= i ? Theme.Blue : Theme.Track); }
                            using (var tb = new SolidBrush(on ? Theme.BlueDark : Theme.Ink)) g.DrawString(AdeeboMeter.L.T(presets[i][1]), Draw.fHead, tb, r.X + 14 * S, r.Y + 12 * S);
                            string cost = "~" + JobsFor(presets[i][0]) + " + " + Cfg.GameJobsForPreset(presets[i][0]) + " " + AdeeboMeter.L.T("game");
                            var cs = g.MeasureString(cost, Draw.fSmall); g.DrawString(cost, Draw.fSmall, muted, r.Right - 14 * S - cs.Width, r.Y + 15 * S);
                            using (var ef = new StringFormat { Trimming = StringTrimming.EllipsisWord }) g.DrawString(AdeeboMeter.L.T(presets[i][2]), Draw.fSmall, sub, new RectangleF(r.X + 14 * S, r.Y + 40 * S, r.Width - 28 * S, ph - 62 * S), ef);
                            string key = presets[i][0]; Act(settingsCanvas, r, delegate { Cfg.ApplyPreset(key); Policy.Apply(); Cfg.Save(); M.Nudge(); settingsCanvas.Invalidate(); });
                        }
                        y += ((presets.Length + cols - 1) / cols) * (ph + 12 * S) - 2 * S;
                        int gj = Cfg.GameJobsPerMinute(); string cnt = AdeeboMeter.L.T("Right now: about") + " " + Cfg.JobsPerMinute() + " " + AdeeboMeter.L.T("checks a minute in the background") + (Cfg.Preset == "custom" ? " (" + AdeeboMeter.L.T("your own mix") + ")" : "") + (gj > 0 ? ", " + AdeeboMeter.L.T("plus about") + " " + gj + " " + AdeeboMeter.L.T("game pings a minute for the games you watch (one a second per server while you play)") : "") + ", " + AdeeboMeter.L.T("on top of the widget's once-a-second read of Windows' own counters.");
                        var ch = Draw.Measure(cnt, Draw.fSmall, (int)w).Height; g.DrawString(cnt, Draw.fSmall, muted, new RectangleF(x, y, w, ch + 4)); y += ch + 18 * S;
                    }
                    var fine = items.Where(i2 => i2.Sec == setSec && i2.Label != "Widget style" && !BigOnes(setSec).Contains(i2.Label)).ToList();   // the big ones are above (all still turn up in a search)
                    if (fine.Count > 0 && (BigOnes(setSec).Length > 0 || setSec == "Widget" || setSec == "Background checks")) y = SetHead(g, Kit.Sent(AdeeboMeter.L.T("FINE-TUNE")), x, y + 6 * S, w);
                    foreach (var it in fine)
                        y = SetRow(g, x, y, w, AdeeboMeter.L.T(it.Label), AdeeboMeter.L.T(it.Help()), it.Opts.Select(L.T).ToArray(), it.Cur(), it.Set, it.Tag);
                    if (setSec == "Navigation") y = PaintNavSettings(g, x, y + 6 * S, w);
                    if (setSec == "Companion") y = PaintCompanion(g, x, y + 6 * S, w);
                    if (setSec == "History & data" && HistDaysCached() > 0)
                    {
                        var clr = new RectangleF(x, y, 240 * S, 22 * S);
                        using (var cb = new SolidBrush(Theme.LvText(2))) g.DrawString(AdeeboMeter.L.T("Delete all saved history now"), Draw.fBodyB, cb, clr.X, clr.Y);
                        Act(settingsCanvas, clr, delegate
                        {
                            int nd = History.Days().Count;
                            if (AskCard.Show(this, AdeeboMeter.L.T("Delete all saved history?") + "\n\n" + nd + " " + AdeeboMeter.L.T("days of history are deleted for good - this can't be undone. The Neighbour wars log and saved replays go too."), "AdeeboMeter", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                            History.Clear(); AirLog.Clear(); histDay = null; histData = null; histDays = null; histStatAt = DateTime.MinValue; ReplayForget(); settingsCanvas.Invalidate();
                        });
                        y += 30 * S;
                    }
                }
            }
            var need = new Size(0, (int)(Math.Max(y, my) + 20 * S)); SetNeed(settingsCanvas, need);
        }
        static readonly string[] SetPaperOpts = { "App", "Newspaper", "Newspaper, white", AdeeboMeter.L.T("Newspaper, app colours") };
        static readonly string[] SetOnOff = { "On", "Off" }, SetLayoutOpts = { "Classic", "Sidebar" }, SetFace = { "Drawn", "Emoji" };
        static readonly string[] SetHat = { "None", "Cap", "Party hat", "Crown" };
        static readonly string[] WStyle = { "Speed only", "Speed + status", "Speed + apps", "Everyday", "Gamer", "Calls", "Engineer", "Auto", "Markets", "Sports", "Data use", "Weather", "My widget" }, WStyleIds = { "speed", "speedstatus", "speedapps", "everyday", "gamer", "calls", "engineer", "auto", "markets", "sports", "data", "weather", "mine" };
        static readonly string[] WStyleHint = { AdeeboMeter.L.T("Just the speeds, like DU Meter."), "Speeds plus OK / Busy / Problem.", AdeeboMeter.L.T("Speeds plus the busiest apps."), AdeeboMeter.L.T("The verdict, five dots, CPU, memory, speed."), AdeeboMeter.L.T("Your game's ping, jitter, loss and grade."), AdeeboMeter.L.T("Jitter, loss and delay for calls."), "Router, internet, DNS, retransmits (Pro).", AdeeboMeter.L.T("Follows what you're doing."), AdeeboMeter.L.T("Up to four prices with a little graph."), AdeeboMeter.L.T("Your teams: live scores and what's next."), AdeeboMeter.L.T("Today, this month and your cap."), AdeeboMeter.L.T("Now, next and the hours ahead."), AdeeboMeter.L.T("Up to six readings you pick.") };
        static readonly string[] SetUnits = { "Bits (Mb/s)", "Bytes (MB/s)" }, SetDir = { "Both", "Download", "Upload" };
        static readonly string[] GzBg = { "10 s", "30 s", "60 s", "Off" }, GzSrv = { "1", "3", "5" }, GzDays = { "7 days", "14 days", "30 days" };
        static readonly int[] GzBgN = { 10, 30, 60, 0 }, GzSrvN = { 1, 3, 5 }, GzDaysN = { 7, 14, 30 };
        // the history folder is read at most every 10 s, not on every repaint (a slow or scanned disk)
        long histBytes; int histDayCount; DateTime histStatAt = DateTime.MinValue;
        void HistStat() { if ((DateTime.UtcNow - histStatAt).TotalSeconds < 10) return; histStatAt = DateTime.UtcNow; histBytes = History.Bytes(); histDayCount = History.Days().Count; }
        long HistBytesCached() { HistStat(); return histBytes; }
        int HistDaysCached() { HistStat(); return histDayCount; }
        static int JobsFor(string preset) { return Cfg.JobsForPreset(preset); }
        void Custom() { Cfg.Preset = "custom"; Cfg.Save(); M.Nudge(); }
        static float SetHead(Graphics g, string t, float x, float y, float w)
        {
            y += 8 * S;
            using (var mb = new SolidBrush(Theme.Ink)) g.DrawString(Kit.Sent(t), Draw.fBodyB, mb, x, y);
            using (var line = new Pen(Theme.Line)) g.DrawLine(line, x, y + 22 * S, x + w, y + 22 * S);
            return y + 28 * S;
        }
        float SetRow(Graphics g, float x, float y, float w, string label, string help, string[] opts, int cur, Action<int> set) { return SetRow(g, x, y, w, label, help, opts, cur, set, null); }
        float SetRow(Graphics g, float x, float y, float w, string label, string help, string[] opts, int cur, Action<int> set, string tag)
        {
            float ow = 0; var widths = opts.Select(o => g.MeasureString(o, Draw.fSmall).Width + 24 * S).ToArray(); ow = widths.Sum() + 2 * S;
            // many choices on a narrow window (1366 x 768 at 125 %, or French): the choices go on their own line under the text,
            // never over the label or the side menu
            bool wrap = ow > w * 0.6f;
            if (wrap && ow > w) { float k = (w - 2 * S) / (ow - 2 * S); for (int i = 0; i < widths.Length; i++) widths[i] *= k; ow = w; }
            float tw = wrap ? w : w - ow - 24 * S;
            bool rtl = L.IsRtl;
            float tx = !rtl || wrap ? x : x + w - tw;
            float hh = Draw.Measure(help, Draw.fSmall, (int)tw).Height;
            using (var ink = new SolidBrush(Theme.Ink)) using (var muted = new SolidBrush(Theme.Muted))
            {
                if (rtl)
                {
                    using (var sf = TextAtRight(label))
                        g.DrawString(label, Draw.fBodyB, ink, new RectangleF(tx, y, tw, 20 * S), sf);
                }
                else g.DrawString(label, Draw.fBodyB, ink, tx, y);
                if (!string.IsNullOrEmpty(tag))
                {
                    float lw = g.MeasureString(label, Draw.fBodyB).Width; string tgt = AdeeboMeter.L.T(tag); float tgw = g.MeasureString(tgt, Draw.fTiny).Width + 12 * S;
                    // bug-hunt L-160: no room for the word - its first letter in the same colour, never nothing
                    if (lw + tgw + 16 * S >= tw && tgt.Length > 0) { tgt = tgt.Substring(0, 1); tgw = g.MeasureString(tgt, Draw.fTiny).Width + 10 * S; }
                    if (lw + tgw + 16 * S < tw)
                    {
                        Color tc = tag == TagNet || tag == TagGame ? Theme.Amber : tag == TagLan ? Theme.Blue : tag == TagOnline ? Theme.Orange : tag == TagWin ? Theme.Grey : Theme.Green;
                        var tr = new RectangleF(rtl ? tx + tw - lw - tgw - 10 * S : tx + lw + 10 * S, y + 2 * S, tgw, 17 * S);
                        Theme.Fill(g, tr, 6 * S, Color.FromArgb(Theme.Dark ? 60 : 36, tc));
                        using (var tbr = new SolidBrush(Theme.Dark ? Theme.Ink : tc)) using (var csf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(tgt, Draw.fTiny, tbr, tr, csf);
                    }
                }
                var hr = new RectangleF(tx, y + 20 * S, tw, hh + 4);
                if (rtl)
                {
                    using (var sf = TextAtRight(help))
                        g.DrawString(help, Draw.fSmall, muted, hr, sf);
                }
                else g.DrawString(help, Draw.fSmall, muted, hr);
            }
            float segX = (wrap ? rtl : !rtl) ? x + w - ow : x;
            var seg = new RectangleF(segX, wrap ? y + 32 * S + hh : y + 4 * S, ow, 30 * S);
            Theme.Fill(g, seg, 15 * S, Theme.Soft); Theme.Stroke(g, seg, 15 * S, Theme.Line, 1);
            float sx = rtl ? seg.Right - S : seg.X + S;
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                for (int i = 0; i < opts.Length; i++)
                {
                    if (rtl) sx -= widths[i];
                    var r = new RectangleF(sx, seg.Y + 2 * S, widths[i], seg.Height - 4 * S);
                    center.FormatFlags = L.RunsRtl(opts[i]) ? StringFormatFlags.DirectionRightToLeft : 0;
                    if (i == cur) Theme.Fill(g, r, 13 * S, Theme.Blue);
                    using (var b = new SolidBrush(i == cur ? Theme.OnBlue : Theme.Sub)) g.DrawString(opts[i], Draw.fSmall, b, r, center);
                    int idx = i; Act(settingsCanvas, r, delegate { set(idx); settingsCanvas.Invalidate(); });
                    if (!rtl) sx += widths[i];
                }
            return wrap ? seg.Bottom + 12 * S : y + Math.Max(44 * S, 26 * S + hh) + 10 * S;
        }

        static StringFormat TextAtRight(string text)
        {
            bool rtl = L.RunsRtl(text);
            return new StringFormat {
                Alignment = rtl ? StringAlignment.Near : StringAlignment.Far,
                FormatFlags = rtl ? StringFormatFlags.DirectionRightToLeft : 0
            };
        }

        // ---------- About: the whole story, told simply
        static string[] BoardRow()
        {
            int m; string list; Sectors.Watch(out m, out list);
            return new[] { AdeeboMeter.L.T("Service board watch"), m > 0 ? string.Format(AdeeboMeter.L.T("A TCP handshake (no data) to each host on your board every {0} min while AdeeboMeter runs, even from the tray."), m) : "Off.", m > 0 ? "1" : "-1" };
        }
        static readonly string[] LeaveIsp = { AdeeboMeter.L.T("Report for my ISP"), AdeeboMeter.L.T("Only when you run it: an MTR to the destinations you chose for the minutes you chose; a reverse name lookup for each router on the way (to your own DNS server); up to 4 page reads from a cable modem at 192.168.100.1 (its signal page); your public IP (one request to www.cloudflare.com/cdn-cgi/trace) only if you type yes. A typed host also gets 1.1.1.1 traced next to it, for comparison."), "1" };
        static readonly string[] LeaveChecks = { AdeeboMeter.L.T("Checks you start"), AdeeboMeter.L.T("Check a destination sends only what you ask for, to where you ask, when you press Check - plus one DNS question for the same name to 1.1.1.1 (8.8.8.8 if that fails) - or, when an IT policy turns online lookups off, to your own DNS server, the second opinion that spots a filter. An IT policy with online_lookups = off sends it to your own DNS server instead."), "1" };
        static readonly string[] LeaveLive = { "Live capture", AdeeboMeter.L.T("Listens to your own adapter only while you press Start. Nothing is sent; captures are deleted when AdeeboMeter exits."), "-1" };
        static readonly string[] LeaveNever = { "Never", AdeeboMeter.L.T("No account, no telemetry, no updates, no ads, no crash reports. It never listens on a port - and checks that about itself every time it starts."), "0" };
        static readonly string[][] AboutNav = {
                    new[] { AdeeboMeter.L.T("Open this view"), AdeeboMeter.L.T("Click the tray icon, or press Ctrl+Alt+A from anywhere.") },
                    new[] { AdeeboMeter.L.T("Back to the corner"), AdeeboMeter.L.T("Esc, minimise, the X or the Back to corner button - they all do the same thing: this view closes, gives its memory back, and the widget keeps watching.") },
                    new[] { "Quit completely", AdeeboMeter.L.T("Right-click the tray icon and choose Exit. Nothing keeps running - not even a helper.") },
                    new[] { "Learn anything", AdeeboMeter.L.T("Point at anything with a small ? beside it, then at the ? - a short explanation opens. The × closes it.") },
                    new[] { "Tune it", AdeeboMeter.L.T("Settings decides how much AdeeboMeter watches in the background - from Penny to Gamer.") } };
        static readonly string[][] AboutTrust = {
                    new[] { "Readable source", AdeeboMeter.L.T("The whole app is plain source code you can read before you run it, built with the compiler that already ships inside Windows.") },
                    new[] { AdeeboMeter.L.T("No admin rights"), AdeeboMeter.L.T("Everything it shows comes from what Windows already lets a normal user see. If something is locked, it simply doesn't show.") },
                    new[] { AdeeboMeter.L.T("Secure by default"), AdeeboMeter.L.T("Checks use TLS with Windows' own certificate validation (Windows picks TLS 1.2 or 1.3). One exception: when you ask the TLS inspector for a different name, it accepts a certificate for another name and says so on its page. Inputs are validated; no shell is ever used.") },
                    new[] { "Light as a feather", AdeeboMeter.L.T("Around 70 small checks a minute on Balanced (about 8 on Penny) in the background. The heavy tools only run while this window is open, and always stop by themselves.") },
                    new[] { AdeeboMeter.L.T("Leaves no trace"), AdeeboMeter.L.T("Settings and a small log live next to the exe (an installed copy keeps them in your AppData folder). Captures are deleted on exit. Window titles and traffic content are never logged. History (a few days, on this PC only) is on unless you switch it off - and one click deletes it.") } };
        static readonly string[][] AboutTabs =
        {
            new[] { "Connections", AdeeboMeter.L.T("Every conversation your PC is having right now - which app, with whom, on which port, in what state. New ones flash in, closed ones fade out. Point at the ? beside a state to learn what it means; press Sniff on a row to record just that conversation for Wireshark.") },
            new[] { "Your Wi-Fi / cable", AdeeboMeter.L.T("Everything about your link: band, channel, signal in dBm, speeds, security, who else shares your air (with a channel map), addresses, errors, and how healthy the hop to your router is. Click the Wi-Fi dot to get here.") },
            new[] { "What's running", AdeeboMeter.L.T("Every program on your PC, in plain English: what it does, how it works, why it's there, what happens without it - and whether it's safe to close. AdeeboMeter only explains; it never closes anything.") },
            new[] { "Check a destination", AdeeboMeter.L.T("Type a name, an IP or a URL. AdeeboMeter walks the whole path like an engineer would - DNS, route, ping, TCP, TLS, HTTP, packet size, every router on the way - and tells you in one sentence where it breaks. It also tells the path's story: who carries your traffic, where networks hand over (that's where BGP decides), NAT, tunnels and firewalls. Pin the places you check often and run them all at once.") },
            new[] { "Health check", AdeeboMeter.L.T("A one-click check-up of the things that quietly cause trouble: days since restart, pending updates, disk space, adapter errors, crowded Wi-Fi, proxies, VPN, DNS, memory and power.") },
            new[] { "What changed", AdeeboMeter.L.T("The diary. Every roam, drop, DNS change, VPN flap and app hiccup in the last two hours, with the time it happened - so 'it was slow at 2:15' finally has an answer.") },
            new[] { AdeeboMeter.L.T("How it travels"), AdeeboMeter.L.T("Your own connection, one layer at a time: which route won and why, your router's hardware address, MTU and packet anatomy, the handshake as a ladder with the time of each step, and with Wireshark the real packets explained field by field. Plus Teams and Office end to end.") },
            new[] { "Inside Windows", AdeeboMeter.L.T("Who started whom (the process tree), what each svchost is hosting, what a program is doing right now (CPU, memory, disk, connections) and why it started, everything that starts with Windows - and the deep dives on the processes people ask about.") },
            new[] { "Learn (Ctrl+K)", AdeeboMeter.L.T("Press Ctrl+K anywhere in this window to jump to a tab, a program, a term or a protocol. Learn mode explains each protocol with its ladder diagram and, with Wireshark, shows it live on your PC for ten seconds.") },
            new[] { "Live capture", AdeeboMeter.L.T("Wireshark's engine (tshark), only if you have Wireshark installed and only while you press Start. It shows what your traffic is made of, who you talk to by name, and Wireshark's own warnings. It stops by itself.") }
        };
        static readonly string[][] AboutLinks =
        {
            new[] { AdeeboMeter.L.T("Ask a question or share an idea"), "straight to Adeeb - no account needed", "app:feedback" },
            new[] { "Report a bug", AdeeboMeter.L.T("say what you did and what you expected - no account needed"), "app:bug" },
            new[] { AdeeboMeter.L.T("Source code on GitHub"), "everything AdeeboMeter does can be read there", "https://github.com/adeeb111/AdeeboMeter" },
        };
        static readonly string[][] Donate =
        {
            new[] { "$5", "https://paypal.me/adeeb1111/5CAD" }, new[] { "$10", "https://paypal.me/adeeb1111/10CAD" }, new[] { "$20", "https://paypal.me/adeeb1111/20CAD" },
            new[] { "$50", "https://paypal.me/adeeb1111/50CAD" }, new[] { "Other amount", "https://paypal.me/adeeb1111" },
        };
        void PaintAbout(Graphics g)
        {
            g.TranslateTransform(aboutCanvas.AutoScrollPosition.X, aboutCanvas.AutoScrollPosition.Y);
            Begin(aboutCanvas);
            float W = aboutCanvas.ClientSize.Width, w = Math.Min(W - 100 * S, 860 * S), x = (W - w) / 2, y = 34 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            using (var blue = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            {
                // each numbered section sits on a raised card: the canvas is already the card colour, so a frame (edge + soft
                // shadow) drawn once the section's height is known lifts it without hiding what's inside
                float secTop = -1;
                Func<float, float> Sec = delegate(float yy)
                {
                    if (secTop >= 0) { Kit.Frame(g, new RectangleF(x - 22 * S, secTop, w + 44 * S, yy - secTop + 12 * S)); yy += 28 * S; }
                    secTop = yy; return yy - 2 * S;
                };
                Theme.Logo(g, new RectangleF(x, y, 56 * S, 56 * S), true);
                g.DrawString(AdeeboMeter.L.T("AdeeboMeter"), Draw.fHero, ink, x + 70 * S, y - 2 * S);
                g.DrawString(AdeeboMeter.L.T("Know what's wrong. Show anyone."), Draw.fHead, blue, x + 72 * S, y + 38 * S);
                using (var sb0 = new SolidBrush(Theme.Sub)) g.DrawString(AdeeboMeter.L.T("Monitor, diagnose, automate, visualize, learn."), Draw.fBody, sb0, x + 72 * S, y + 62 * S);   // 24 Nov: what it does, under the promise
                // found a bug? the report is put on the clipboard, the person pastes and sends it themselves
                {
                    string bt = AdeeboMeter.L.T((DateTime.Now - bugCopied).TotalSeconds < 6 ? AdeeboMeter.L.T("Copied - paste it into the page that opened") : AdeeboMeter.L.T("Found a bug? Report it")); var bs = g.MeasureString(bt, Draw.fBodyB);
                    var br = new RectangleF(x + w - bs.Width - 30 * S, y + 6 * S, bs.Width + 30 * S, 34 * S); Theme.Fill(g, br, 17 * S, Theme.Soft); Theme.Stroke(g, br, 17 * S, Theme.Line, 1);
                    g.DrawString(bt, Draw.fBodyB, ink, br.X + 15 * S, br.Y + (34 * S - bs.Height) / 2);
                    Act(aboutCanvas, br, delegate
                    {
                        ReportDialog("bug", "");
                    });
                }
                y += 100 * S;
                y = Para(g, AdeeboMeter.L.T("When the internet feels slow, most of us open Task Manager, then a command prompt, then ping, then guess. AdeeboMeter does all of that for you - quietly, every second - and tells you in one plain sentence what's wrong and what to do about it."), x, y, w, sub, Draw.fBody);
                y = Para(g, AdeeboMeter.L.T("No account. No telemetry. Never listens. Every packet it sends is listed below - and you can switch each one off."), x, y, w, ink, Draw.fBodyB);

                y = Sec(y); y = AboutHead(g, "1", AdeeboMeter.L.T("The corner widget"), x, y, w);
                y = Para(g, AdeeboMeter.L.T("A small card in the bottom-right corner, faded to 30% so it never distracts. It shows the one thing that matters - is everything OK - plus CPU, memory, disk and network at a glance. It never takes clicks, so whatever is underneath keeps working. When a window covers the corner it folds into a tiny chip; rest your mouse on it for half a second and it opens."), x, y, w, sub, Draw.fBody);

                y = Sec(y); y = AboutHead(g, "2", "Getting around", x, y, w);
                y = Rows(g, AboutNav, x, y, w);

                y = Sec(y); y = AboutHead(g, "3", AdeeboMeter.L.T("Is it me or the network?"), x, y, w);
                y = Para(g, AdeeboMeter.L.T("The five dots at the top follow your traffic out of the building: your PC, your Wi-Fi or cable to the router, your provider (or VPN), DNS - the internet's phone book - and the internet itself. The first dot that isn't green is where the trouble starts. Everything before it is fine; everything after it is a victim, not a cause. That's the whole trick."), x, y, w, sub, Draw.fBody);

                y = Sec(y); y = AboutHead(g, "4", AdeeboMeter.L.T("What each tab does"), x, y, w);
                y = Rows(g, AboutTabs, x, y, w);

                y = Sec(y); y = AboutHead(g, "5", AdeeboMeter.L.T("What leaves your PC - and what never does"), x, y, w);
                Func<int, string> every = sec => sec <= 0 ? "Off" : sec >= 60 ? "every " + sec / 60 + " min" : "every " + sec + " s";
                var leaves = new List<string[]> {
                    new[] { "Router ping", Cfg.GwSec > 0 ? AdeeboMeter.L.T("A tiny ping to your own router, ") + every(Cfg.GwSec) + AdeeboMeter.L.T(" (every ") + (Cfg.GwSec >= 30 ? 5 : 2) + AdeeboMeter.L.T(" s while this window is open). Stays on your local network.") : "Off.", Cfg.GwSec > 0 ? "0" : "-1" },
                    new[] { "Internet and DNS check", Cfg.NetSec > 0 ? AdeeboMeter.L.T("One ping to 1.1.1.1 (or 8.8.8.8) ") + every(Cfg.NetSec) + (Cfg.NetSec > 5 ? AdeeboMeter.L.T(" (every 5 s while this window is open or a call runs)") : "") + AdeeboMeter.L.T(", and one name lookup at most once a minute (every 15 s with this window open). On networks that block ping: a timed connection to port 443 of the same address instead, closed at once.") : AdeeboMeter.L.T("Off - nothing goes to the internet in the background."), Cfg.NetSec > 0 ? "1" : "-1" },
                    new[] { "Provider name", Cfg.NetSec > 0 ? AdeeboMeter.L.T("Once per network (tried twice more only if no router answered): a short trace (6 hops) toward 1.1.1.1 and one name lookup, to put your provider's name on the ISP dot.") : AdeeboMeter.L.T("Off (follows the internet check)."), Cfg.NetSec > 0 ? "1" : "-1" },
                    new[] { "Wi-Fi watch", Cfg.WifiSec > 0 ? AdeeboMeter.L.T("Asks Windows about your Wi-Fi, ") + every(Cfg.WifiSec) + AdeeboMeter.L.T(". Nothing is sent anywhere.") : "Off.", "-1" },
                    LeaveChecks,
                    LeaveTools,
                    new[] { AdeeboMeter.L.T("Phone waiting room"), Cfg.PhoneStandby ? AdeeboMeter.L.T("Only once a phone is remembered: a waiting room at AdeeboMeter's relay so the phone can find this PC (sealed - the relay can't read it), and once an hour one request to the relay for the time, so both sides pick the same room.") : "Off.", "-1" },   // round 6 TA6-1
                    new[] { AdeeboMeter.L.T("Pinned destination watch"), Cfg.PinWatch && Cfg.Pins.Count > 0 ? AdeeboMeter.L.T("One ping to ") + Cfg.Pins[0] + AdeeboMeter.L.T(" every 30 s (you switched it on).") : "Off.", Cfg.PinWatch ? "1" : "-1" },
                    new[] { "Certificate revocation", CertCfg.Revocation ? AdeeboMeter.L.T("When you run Chain check or HTTPS check: one question to the certificate issuer's revocation list (CRL) or OCSP address, 10 s at most.") : AdeeboMeter.L.T("Off (Settings › Privacy & online lookups)."), CertCfg.Revocation ? "1" : "-1" },
                    new[] { "Gamer Zone", Games.Monitored().Count == 0 ? AdeeboMeter.L.T("Nothing - until you click a game in the Gamer Zone.") : AdeeboMeter.L.T("A ping (or a timed TCP connection) to the regions of the games you added: the best ones ") + every(Cfg.GameBgSec) + AdeeboMeter.L.T(", the others ") + every(GamePing.SurveySec) + AdeeboMeter.L.T(", every second while a game is open") + (Cfg.GameBgSec < 60 ? AdeeboMeter.L.T(" - plus, while a game runs, a trace to its server and one ping to each router on the way every 15 s (the match path)") : "") + AdeeboMeter.L.T(". Valve's server list is fetched only when you add a Valve game or press Refresh."), Games.Monitored().Count == 0 ? "-1" : "1" },
                    new[] { "Streaming", Cfg.Streams && !Cfg.Off("stream") ? AdeeboMeter.L.T("Only while something streams: a timed TCP connection (no data) to the stream's own server every ") + Streams.FarSec + AdeeboMeter.L.T(" s, to tell its slowdowns from yours - six tries at most if it never answers. Which service it is comes from Windows' own list of names, read on this PC.") : "Off.", Cfg.Streams && !Cfg.Off("stream") ? "1" : "-1" },
                    new[] { "Broker watch", Cfg.TradeLive && Cfg.TradeHost != "" && !Cfg.Off("trade") ? AdeeboMeter.L.T("A timed TCP connection (no data, no sign-in) to ") + Cfg.TradeHost + " " + Every(Trading.Sec) + AdeeboMeter.L.T(", only while the market is open") + (Cfg.TradeCrypto ? AdeeboMeter.L.T(" - around the clock, as you chose for crypto") : "") + "." : AdeeboMeter.L.T("Off - until you pick your broker on the Trading page."), Cfg.TradeLive && Cfg.TradeHost != "" && !Cfg.Off("trade") ? "1" : "-1" },
                    new[] { AdeeboMeter.L.T("Bank of Canada numbers"), Cfg.TradeFx && !Policy.NoOnline ? AdeeboMeter.L.T("Only when Markets is open (or the evening summary is on) and newer numbers should be out: one request per currency on your list (its history back to 2017 the first time) and, about weekly, the commodity index, the policy rate and inflation, from bankofcanada.ca. No account, nothing about you is sent.") : "Off.", Cfg.TradeFx && !Policy.NoOnline ? "1" : "-1" },
                    // B2 (review 1.1.6): every service the app talks to is on this list, with when and how often
                    new[] { AdeeboMeter.L.T("Sports scores"), Cfg.Sports && !Policy.NoOnline ? AdeeboMeter.L.T("While the Sports page is open (about every 5 min), or a stream plays while a team you follow is live: the day's fixtures and scores from api-web.nhle.com (NHL), site.api.espn.com (football) and feeds.bbci.co.uk (headlines), plus api.football-data.org and api.cricapi.com when you added your own key.") + (Cfg.KickoffRemind ? AdeeboMeter.L.T(" With the kick-off reminder on: your teams' fixtures every 30 min while the PC is on.") : "") : AdeeboMeter.L.T("Off - nothing is asked until you turn scores on."), Cfg.Sports ? "1" : "-1" },
                    new[] { AdeeboMeter.L.T("Market quotes"), MarketData.HasQuoteKey() && !Policy.NoOnline ? AdeeboMeter.L.T("With your own key, while Markets is open: index and futures quotes from financialmodelingprep.com, as often as you set (1-60 min).") : AdeeboMeter.L.T("Off - it needs your own key."), MarketData.HasQuoteKey() ? "1" : "-1" },
                    new[] { AdeeboMeter.L.T("Stream Check"), AdeeboMeter.L.T("Only when you press Test my stream: the playlist of the link you gave, and up to 30 s of its video, from that link's own server.") , "1" },
                    new[] { AdeeboMeter.L.T("Phone link"), Policy.NoRemote ? AdeeboMeter.L.T("Off - your IT team turned the phone link off.") : AdeeboMeter.L.T("Only when you press Show the QR code, or a remembered phone connects: a sealed connection through AdeeboMeter's relay (adeebometer-remote.orkoadeeb.workers.dev). The relay passes messages along and can't read them."), Policy.NoRemote ? "-1" : "1" },
                    BoardRow(),
                    new[] { AdeeboMeter.L.T("Watched devices"), DevWatch.Count > 0 ? string.Format(AdeeboMeter.L.T("A ping a minute to each device you watch on your network ({0} now). Stays on your local network."), DevWatch.Count) : "Off.", DevWatch.Count > 0 ? "0" : "-1" },
                    new[] { "Weather", Weather.Active ? "For " + Weather.Current.Name + AdeeboMeter.L.T(": one request to api.open-meteo.com with only its coordinates, ") + (Cfg.WeatherAcc == "better" ? AdeeboMeter.L.T("every 6 hours while the PC is on.") : AdeeboMeter.L.T("about every 2 days (when less than a day of forecast is left).")) + AdeeboMeter.L.T(" Finding a place sends the name you type, once.") : AdeeboMeter.L.T("Off (Settings > Weather). Your PC's location is never used."), Weather.Active ? "1" : "-1" },
                    new[] { AdeeboMeter.L.T("Reports you send"), Policy.NoOnline ? AdeeboMeter.L.T("Off - your IT team turned online sending off.") : AdeeboMeter.L.T("Only when you press Send in a report box (a bug, an idea, your Hall of Fame card), after seeing exactly what goes: to AdeeboMeter's relay, which files it for Adeeb. No account."), Policy.NoOnline ? "-1" : "1" },
                    new[] { "Speed test", AdeeboMeter.L.T("Only when you press Test my speed (or Run in the Toolbox): about 8 s of download from the server you picked (Cloudflare's nearest by default, or a Vultr or OVHcloud test file in a city), 8 s of upload where it offers it, and pings to it - up to ") + (SpeedTest.MaxDownMB + SpeedTest.MaxUpMB) * 2 + AdeeboMeter.L.T(" MB. Find the closest: one ping to each server. Never by itself."), "1" },
                    new[] { "Online lookups (BGP)", Online.On ? "ON for " + Math.Ceiling(Online.Left.TotalMinutes) + AdeeboMeter.L.T(" more min: public IP addresses of the path you look at go to stat.ripe.net and peeringdb.com (HTTPS).") : AdeeboMeter.L.T("Off (you switch them on; they switch themselves off after ") + Cfg.OnlineMin + " min).", Online.On ? "1" : "-1" },
                    new[] { "Security checkup", Online.On ? AdeeboMeter.L.T("With online lookups on, when you run it: one encrypted-DNS question for www.microsoft.com to cloudflare-dns.com, and one TLS handshake with www.microsoft.com (to see whether something on the way reads encrypted traffic).") : AdeeboMeter.L.T("Nothing leaves - its two online checks wait for online lookups."), Online.On ? "1" : "-1" },
                    LeaveIsp,
                    Row("Toolbox", AdeeboMeter.L.T("Only when you press Run: MTU per hop and MTR (pings with a rising TTL), port and Teams UDP tests, dig and TLS checks, iperf3 to your own server, ping sweep of your own network, nearest-cloud timing, the clock check (one request to msftconnecttest.com)."), "1"),
                    new[] { AdeeboMeter.L.T("Sign-in page check"), Cfg.Portal ? AdeeboMeter.L.T("Only inside a check you start (What's wrong? and the full checkup): one request to the same test page Windows itself uses (msftconnecttest.com).") : "Off.", Cfg.Portal ? "1" : "-1" },
                    LeaveLive,
                    LeaveNever };
                y = Rows(g, leaves, x, y, w);
                var setLink = new RectangleF(x, y, 260 * S, 22 * S);
                g.DrawString(AdeeboMeter.L.T("Change any of these in Settings  >"), Draw.fBodyB, blue, setLink.X, setLink.Y);
                Act(aboutCanvas, setLink, delegate { tab = "settings"; ShowTab(); });
                y += 34 * S;

                y = Sec(y); y = AboutHead(g, "6", AdeeboMeter.L.T("Built to be trusted"), x, y, w);
                y = Rows(g, AboutTrust, x, y, w);

                // community and support: links that open in the browser - AdeeboMeter itself sends nothing
                y += 6 * S;
                y = Sec(y); y = AboutHead(g, "7", AdeeboMeter.L.T("Community and support"), x, y, w);
                g.DrawString(AdeeboMeter.L.T("AdeeboMeter is free and open source. A report goes only when you press Send in its box."), Draw.fBody, sub, new RectangleF(x, y, w, 24 * S)); y += 32 * S;
                foreach (var lk in AboutLinks)
                {
                    string url = lk[2]; var ls = g.MeasureString(AdeeboMeter.L.T(lk[0]) + "  >", Draw.fBodyB); var lr = new RectangleF(x, y, ls.Width, ls.Height);
                    g.DrawString(AdeeboMeter.L.T(lk[0]) + "  >", Draw.fBodyB, blue, lr.X, lr.Y);
                    g.DrawString(AdeeboMeter.L.T(lk[1]), Draw.fSmall, muted, new RectangleF(x + Math.Max(ls.Width + 12 * S, 300 * S), y + 2 * S, w - 300 * S, 20 * S));
                    Act(aboutCanvas, lr, delegate { if (url.StartsWith("app:", StringComparison.Ordinal)) ReportDialog(url.Substring(4), ""); else Crash.OpenUrl(url); });
                    y += 30 * S;
                }
                y += 6 * S;
                g.DrawString(AdeeboMeter.L.T("Donate"), Draw.fBodyB, ink, x, y); y += 26 * S;
                g.DrawString(AdeeboMeter.L.T("Free for everyone. If it saved you time, a donation helps cover code signing and the Microsoft Store, and keeps AdeeboMeter free."), Draw.fBody, sub, new RectangleF(x, y, w, 44 * S)); y += Draw.Measure(AdeeboMeter.L.T("Free for everyone. If it saved you time, a donation helps cover code signing and the Microsoft Store, and keeps AdeeboMeter free."), Draw.fBody, (int)w).Height + 10 * S;
                float dx = x;
                foreach (var d in Donate)
                {
                    string label = AdeeboMeter.L.T(d[0]), url = d[1]; var ds = g.MeasureString(label, Draw.fBodyB); var dr = new RectangleF(dx, y, ds.Width + 30 * S, 34 * S);
                    Theme.Fill(g, dr, 17 * S, Theme.BlueSoft); Theme.Stroke(g, dr, 17 * S, Theme.Blue, 1);
                    g.DrawString(label, Draw.fBodyB, blue, dr.X + 15 * S, dr.Y + (dr.Height - ds.Height) / 2);
                    Act(aboutCanvas, dr, delegate { Crash.OpenUrl(url); });
                    dx += dr.Width + 10 * S; if (dx > x + w - 120 * S) { dx = x; y += 44 * S; }
                }
                y += 44 * S;
                g.DrawString(AdeeboMeter.L.T("PayPal, in Canadian dollars. Nothing about you is sent by AdeeboMeter."), Draw.fSmall, muted, x, y); y += 30 * S;

                y = Sec(y) + 10 * S; secTop = -1;
                y += 14 * S;
                g.DrawString(AdeeboMeter.L.T("AdeeboMeter ") + Program.Version + AdeeboMeter.L.T("  ·  made by Adeeb Khan  ·  ") + Program.Tagline, Draw.fSmall, muted, x, y); y += 26 * S;
                var upd = new RectangleF(x, y, 420 * S, 22 * S);
                g.DrawString(AdeeboMeter.L.T("Check for updates  >"), Draw.fBodyB, blue, upd.X, upd.Y);
                g.DrawString(AdeeboMeter.L.T("opens the website in your browser - AdeeboMeter itself never checks"), Draw.fSmall, muted, x + g.MeasureString(AdeeboMeter.L.T("Check for updates  >"), Draw.fBodyB).Width + 10 * S, y + 2 * S);
                Act(aboutCanvas, upd, delegate { Crash.OpenUrl("https://adeebometer.com/?v=" + Uri.EscapeDataString(Program.Version) + "#update"); });
                y += 30 * S;
                // credits the licences ask for
                string cred = AdeeboMeter.L.T("Credits") + ":  Barlow typeface, (c) The Barlow Project Authors, SIL Open Font License 1.1  ·  Twemoji faces, (c) Twitter, Inc and other contributors, CC-BY 4.0  ·  Weather data by Open-Meteo.com, CC BY 4.0  ·  "
                    + AdeeboMeter.L.T("Words of comfort: King James Version, Pickthall (1930), JPS (1917), Max Muller (1881), Edwin Arnold (1885), Macauliffe (1909) - all public domain.");
                float chh = Draw.Measure(cred, Draw.fSmall, (int)w).Height; g.DrawString(cred, Draw.fSmall, muted, new RectangleF(x, y, w, chh + 4)); y += chh + 10 * S;
            }
            var need = new Size(0, (int)(y + 20 * S)); SetNeed(aboutCanvas, need);
        }
        static float Para(Graphics g, string t, float x, float y, float w, Brush b, Font f)
        {
            t = AdeeboMeter.L.T(t);
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
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(seg.Count > 0 ? AdeeboMeter.Kit.Sent(AdeeboMeter.L.T("THE PATH STORY  ·  WHO CARRIES YOUR TRAFFIC, FROM ROUTER NAMES")) : "WHAT WE FOUND", Draw.fTiny, mb, x, y);
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
                try
                {
                    Cfg.Save();
                    var keep = File.ReadAllLines(Path.Combine(Program.WriteDir, "AdeeboMeter.settings")).Where(l => !PrivateKeys.Any(p => l.StartsWith(p + "=", StringComparison.Ordinal))).ToList();
                    WriteSafe(d.FileName, string.Join("\r\n", keep) + "\r\n", false);
                    settingsNote = AdeeboMeter.L.T("Saved to ") + Path.GetFileName(d.FileName) + AdeeboMeter.L.T(" - without your weather place, broker, teams, market symbols, pinned game, widget position, the name you are called and your words of comfort (they stay on this PC).");   /* hunters13 lifecycle L-7 */
                }
                catch (Exception e) { settingsNote = AdeeboMeter.L.T("Couldn't save: ") + e.Message; }
            }
            settingsCanvas.Invalidate();
        }
        // never in an exported file: where you are, your broker, the teams you follow (AM-CG-122/164)
        static readonly string[] PrivateKeys = { "wplace", "tradehost", "sportteams", "addrname", "faith", "faithlast", "searches", "wgeo", "wapi", "wsyms", "wpin", "wpos", "wscreen", "wposgame", "wheld", "wback" };   // hunters12 ML-1: your symbols, a pinned game and this PC's screens stay on this PC too   // review 1.1.6 U3 #2: your recent searches and weather servers stay yours
        // settings that make AdeeboMeter reach out to someone else's server on its own: an imported file asks first (AM-CG-124)
        static readonly string[][] OutboundKeys = { new[] { "tradelive", "watch your broker in market hours" }, new[] { "tradecrypto", "watch your broker around the clock" }, new[] { "streams2", "notice streams and check their server" },
            new[] { "weather", "fetch the weather" }, new[] { "tradefx2", "fetch the day's currency rates" }, new[] { "sports", "fetch sports scores and headlines" } };
        void ImportSettings()
        {
            using (var d = new OpenFileDialog { Filter = "AdeeboMeter settings|*.settings|All files|*.*" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    // read the whole file first; nothing changes unless it really is a settings file. A settings file is a few KB:
                    // anything over 64 KB, or a line over 2,000 characters, isn't one (AM-CG-221)
                    if (new FileInfo(d.FileName).Length > 64 * 1024) { settingsNote = AdeeboMeter.L.T("That file is too big to be AdeeboMeter settings - nothing changed."); settingsCanvas.Invalidate(); return; }
                    var kv = new List<KeyValuePair<string, string>>();
                    foreach (var line in File.ReadLines(d.FileName).Take(400))
                    {
                        if (line.Length > 2000) { settingsNote = AdeeboMeter.L.T("That isn't an AdeeboMeter settings file - nothing changed."); settingsCanvas.Invalidate(); return; }   // bug-hunt L-161: as the comment says - not one
                        int eq = line.IndexOf('='); if (eq <= 0) continue;
                        string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                        if (Cfg.Known.Contains(k)) kv.Add(new KeyValuePair<string, string>(k, v));
                    }
                    if (kv.Count == 0) { settingsNote = AdeeboMeter.L.T("That isn't an AdeeboMeter settings file - nothing changed."); settingsCanvas.Invalidate(); return; }
                    // changes that start things on their own (with Windows, in the background, to a server) or keep data are
                    // shown first and only applied on a yes; the rest of the file loads either way
                    var now = new Dictionary<string, string>(); foreach (var l in Cfg.Lines()) { int e2 = l.IndexOf('='); if (e2 > 0) now[l.Substring(0, e2)] = l.Substring(e2 + 1); }
                    var asks = new List<string[]>();
                    foreach (var x in kv)
                    {
                        string cur; now.TryGetValue(x.Key, out cur);
                        if (x.Key == "autostart") { if ((x.Value == "1") != Cfg.GetAutoStart()) asks.Add(new[] { x.Key, x.Value == "1" ? AdeeboMeter.L.T("start AdeeboMeter with Windows") : AdeeboMeter.L.T("stop starting with Windows") }); continue; }
                        if (x.Key == "pin") continue;   // asked below, all together
                        if (cur == x.Value) continue;
                        if (x.Key == "pinwatch" && x.Value == "1") { asks.Add(new[] { x.Key, AdeeboMeter.L.T("ping your pinned addresses in the background") }); continue; }   // SEC-03 (security hunt): background traffic is asked first
                        if (x.Key == "gmon" && x.Value.Trim() != "") { asks.Add(new[] { x.Key, AdeeboMeter.L.T("check these games' servers in the background: ") + (x.Value.Length > 60 ? x.Value.Substring(0, 60) + "..." : x.Value) }); continue; }
                        var ob = OutboundKeys.FirstOrDefault(o => o[0] == x.Key);
                        if (ob != null) { if (x.Value == "1") asks.Add(new[] { x.Key, ob[1] + AdeeboMeter.L.T(" (contacts a server on its own)") }); }
                        else if (x.Key == "tradehost") asks.Add(new[] { x.Key, AdeeboMeter.L.T("watch a different broker address: ") + (x.Value.Length > 60 ? x.Value.Substring(0, 60) + "..." : x.Value) });
                        else if (x.Key == "history2") asks.Add(new[] { x.Key, x.Value == "1" ? "keep a history of your connection" : "stop keeping history" });
                        else if (x.Key == "histdays") { int hd; if (int.TryParse(x.Value, NumberStyles.None, CultureInfo.InvariantCulture, out hd) && Cfg.HistDayChoices.Contains(hd)) asks.Add(new[] { x.Key, "keep history for " + hd + " days" }); }   // bug-hunt L-162: only a real choice is shown (a bad one is ignored when loaded)
                        else if (x.Key == "wplace") asks.Add(new[] { x.Key, "change the weather place" });
                        // review 1.1.6 U3 #2: a different weather server gets your place's coordinates; the internet check, the phone's
                        // waiting room and the sign-in page check each reach a server on their own - all asked first
                        else if (x.Key == "wapi" || x.Key == "wgeo") asks.Add(new[] { x.Key, AdeeboMeter.L.T("send your weather place to a different server: ") + Program.Plain(x.Value.Length > 60 ? x.Value.Substring(0, 60) + "..." : x.Value) });
                        else if (x.Key == "net" && (cur == "0" || cur == "") && x.Value != "0") asks.Add(new[] { x.Key, AdeeboMeter.L.T("turn the internet check on (a ping to 1.1.1.1 in the background)") });
                        else if (x.Key == "gauto" && x.Value != "0" && !Cfg.GameAuto) asks.Add(new[] { x.Key, AdeeboMeter.L.T("start game monitoring on its own when a game opens") });   /* hunters11 sec L1 */
                        else if (x.Key == "phonestandby" && x.Value == "1") asks.Add(new[] { x.Key, AdeeboMeter.L.T("keep a waiting room for remembered phones at AdeeboMeter's relay") });
                        else if (x.Key == "portal" && x.Value == "1") asks.Add(new[] { x.Key, AdeeboMeter.L.T("ask msftconnecttest.com when a check fails (sign-in page check)") });
                    }
                    {   // SEC-03: the file's pinned addresses replace yours and get pinged - say which, ask first
                        var filePins = kv.Where(x => x.Key == "pin").Select(x => x.Value.Trim()).Where(v => v != "").ToList(); List<string> mine; lock (Cfg.Pins) mine = Cfg.Pins.ToList();
                        if (filePins.Count > 0 && !(filePins.Count == mine.Count && filePins.All(p => mine.Contains(p, StringComparer.OrdinalIgnoreCase))))
                            asks.Add(new[] { "pin", AdeeboMeter.L.T("replace your pinned addresses with: ") + string.Join(", ", filePins.Take(5).Select(p => Program.Plain(p.Length > 40 ? p.Substring(0, 40) + "..." : p))) });
                    }
                    string viewBefore = Cfg.View;   // round 6: a view in the file is switched to, like picking it
                    kv.RemoveAll(x => x.Key == "histdays" && !asks.Any(o => o[0] == "histdays"));   // L-162: a value that isn't a choice never lands either
                    if (asks.Count > 0)
                    {
                        var ans = AskCard.Show(this, AdeeboMeter.L.T("This file would also:") + "\n\n- " + string.Join("\n- ", asks.Select(o => AdeeboMeter.L.T(o[1])).ToArray()) + "\n\n" + AdeeboMeter.L.T("Yes: apply these too.  No: load the rest of the file without them.  Cancel: change nothing."), "AdeeboMeter", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                        if (ans == DialogResult.Cancel) { settingsNote = "Nothing changed."; settingsCanvas.Invalidate(); return; }
                        if (ans != DialogResult.Yes) kv = kv.Where(x => !asks.Any(o => o[0] == x.Key)).ToList();
                    }
                    int n = 0; if (kv.Any(x => x.Key == "pin")) lock (Cfg.Pins) Cfg.Pins.Clear();   // pins are replaced only when the file has its own
                    foreach (var x in kv)
                    {
                        string k = x.Key, v = x.Value;
                        if (k == "autostart") { if ((v == "1") != Cfg.GetAutoStart()) Cfg.SetAutoStart(v == "1"); }   // only a real change (a Store copy would open Windows' Startup settings)
                        else if (k == "serious") Voice.Serious = v == "1";
                        else if (k == "theme") { int pi = Array.IndexOf(Theme.Palettes, v); Cfg.SetTheme(pi >= 0 ? pi + 2 : v == "dark" ? 1 : v == "light" ? 0 : -1); }   // AM-CG-199
                        else if (k == "widget" || k == "wpos" || k == "wscreen" || k == "wposgame" || k == "wheld" || k == "wback" || k == "wsyms" || k == "wpin") continue;   /* hunters13 lifecycle ML-3: an older file's symbols and pinned game don't replace yours either */   /* hunters12 ML-1: where the widget sits belongs to this PC's screens */   // the corner widget's on/off stays as it is on this PC - not counted
                        else Cfg.Read(k, v);
                        n++;
                    }
                    Policy.Apply();   // an IT policy still wins over an imported file
                    { string vNew = Cfg.View; if (vNew != viewBefore && vNew != "") { Cfg.View = viewBefore; try { SwitchView(vNew); } catch (Exception e) { if (e is OutOfMemoryException) throw; Cfg.View = vNew; Program.Log("import view: " + e.Message); } } }
                    Cfg.Save(); M.Nudge(); BuildPinBar();
                    // the open window takes them up at once: the tabs (Simple / Pro), the Adeebo button, the language
                    L.Use(Cfg.Lang); ApplyMode(); SyncPointsBtn(); SyncLangBtn(); SyncRestart(); PlaceTop(); Invalidate(true);   /* hunt-5: the language button and the Restart pill follow an import too */
                    settingsNote = "Loaded " + n + AdeeboMeter.L.T(" settings.") + (kv.Any(x => x.Key == "lang") ? AdeeboMeter.L.T(" Some captions switch language at the next start.") : "");
                }
                catch (Exception e) { settingsNote = AdeeboMeter.L.T("Couldn't read it: ") + e.Message; }
            }
            settingsCanvas.Invalidate();
        }
    }
}
