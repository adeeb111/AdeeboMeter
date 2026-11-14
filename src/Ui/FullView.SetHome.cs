// Settings as questions (Adeeb, 14 Nov): the home is a card per question with what's set now; a question opens into big
// choice cards for its main setting and every other setting under Fine-tune. Nothing is dropped or renamed - search still
// finds every row, and each section keeps its own page.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AdeeboMeter
{
    partial class FullView
    {
        // section -> icon, accent, the question
        static readonly Dictionary<string, string[]> SetQLook = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "General", new[] { "settings", "home", "How should the app look and start?" } },
            { "Widget", new[] { "app", "dest", "How should the corner widget look?" } },
            { "Companion", new[] { "mood", "mine", "How should the buddy talk?" } },
            { "Navigation", new[] { "layout", "learn", "How do you want to get around?" } },
            { "Background checks", new[] { "speed", "time", "How much should it watch?" } },
            { "Gamer Zone", new[] { "game", "gamer", "Do you play online games?" } },
            { "Streaming & trading", new[] { "app", "stream", "Do you stream or trade?" } },
            { "Alerts", new[] { "report", "q.break", "When should it tell you?" } },
            { "History & data", new[] { "time", "q.keys", "What should it remember?" } },
            { "Privacy & online lookups", new[] { "lock", "q.padlock", "What may it ask the internet?" } },
            { "Capture & advanced", new[] { "traffic", "q.traffic", "For engineers: capture limits" } }
        };
        static string[] SetQLookOf(string sec) { string[] l; return SetQLook.TryGetValue(sec, out l) ? l : new[] { "settings", "home", sec }; }
        // the settings shown as big cards on a question's page (the rest go under Fine-tune)
        static string[] BigOnes(string sec)
        {
            switch (sec)
            {
                case "General": return new[] { "Mode", "Theme" };
                case "Companion": return new[] { "Mood" };
                case "Navigation": return new[] { "Layout" };
                case "Gamer Zone": return new[] { "Game ping in the background" };
                case "Streaming & trading": return new[] { "Notice streams", "Your broker" };
                case "Alerts": return new[] { "Alerts" };
                case "History & data": return new[] { "History", "Keep history for" };
                default: return Array.Empty<string>();
            }
        }
        // what's set now, in a few words: the first two settings of the section as they are
        static string SecSummary(string sec, List<SetItem> items)
        {
            if (sec == "Widget") { int i = Array.IndexOf(WStyleIds, Cfg.WidgetStyle); return L.T(i >= 0 ? WStyle[i] : "Auto") + (Draw.SpeedStyle ? " · " + (Cfg.WUnits == "bytes" ? "MB/s" : "Mb/s") : ""); }
            if (sec == "Background checks") return L.T(Cfg.Preset == "penny" ? "Penny" : Cfg.Preset == "custom" ? "Your own mix" : Cfg.Preset == "gamer" ? "Gamer" : "Balanced") + " · ~" + Cfg.JobsPerMinute() + " " + L.T("checks a minute");
            if (sec == "Companion") return Voice.Serious ? L.T("Plain - no jokes") : L.T(Lines.MoodName(Lines.Mood)) + " · " + L.T(Lines.PersonaName(Lines.Persona));
            var l = items.Where(i => i.Sec == sec).Take(2).Select(i => { int c = i.Cur(); return c >= 0 && c < i.Opts.Length ? (sec == "General" || sec == "Navigation" ? "" : L.T(i.Label) + ": ") + L.T(i.Opts[c]) : ""; }).Where(t => t != "").ToList();   // "Alerts: On", not a bare "On"
            return string.Join(" · ", l);
        }
        float PaintSettingsHome(Graphics g, float x, float y, float w, List<SetItem> items)
        {
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(L.T("Settings"), Draw.fTitle, ink, x, y); y += 34 * S;
            int cols = w > 900 * S ? 3 : w > 560 * S ? 2 : 1; float cw = (w - (cols - 1) * 12 * S) / cols, ch = 92 * S; int k = 0;
            foreach (var sec in SetSections)
            {
                var look = SetQLookOf(sec); var r = new RectangleF(x + (k % cols) * (cw + 12 * S), y + (k / cols) * (ch + 12 * S), cw, ch); k++;
                string s2 = sec; Act(settingsCanvas, r, delegate { setSec = s2; settingsCanvas.AutoScrollPosition = Point.Empty; settingsCanvas.Invalidate(); });
                if (Kit.Off(g, r)) continue;
                Kit.Card(g, r, look[1], false);
                using (var cb = new SolidBrush(Kit.Soft(look[1]))) g.FillEllipse(cb, r.X + 14 * S, r.Y + 20 * S, 34 * S, 34 * S);
                Kit.Icon(g, look[0], new RectangleF(r.X + 22 * S, r.Y + 28 * S, 18 * S, 18 * S), Kit.Accent(look[1]));
                using (var ink = new SolidBrush(Theme.Ink)) using (var m = new SolidBrush(Theme.Muted)) using (var sf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                {
                    g.DrawString(L.T(look[2]), Draw.fBodyB, ink, new RectangleF(r.X + 58 * S, r.Y + 20 * S, r.Width - 70 * S, 40 * S));
                    g.DrawString(SecSummary(sec, items), Draw.fSmall, m, new RectangleF(r.X + 58 * S, r.Bottom - 26 * S, r.Width - 70 * S, 18 * S), sf);
                }
            }
            return y + ((k + cols - 1) / cols) * (ch + 12 * S) + 10 * S;
        }
        // a theme's colours in a little pill: its background, a card, its accent, its text (Follow Windows shows light and dark)
        static void ThemeSwatch(Graphics g, RectangleF r, int theme)
        {
            string[] bg, card, acc, ink;
            if (theme >= 2 && theme - 2 < Theme.Palettes.Length) { var v = Theme.PaletteColors[Theme.Palettes[theme - 2]]; bg = new[] { v[0] }; card = new[] { v[1] }; acc = new[] { v[12] }; ink = new[] { v[6] }; }
            else if (theme == 1) { bg = new[] { "#0E1319" }; card = new[] { "#161D26" }; acc = new[] { "#1F6FEB" }; ink = new[] { "#E6EDF3" }; }
            else if (theme == 0) { bg = new[] { "#F2F5F9" }; card = new[] { "#FFFFFF" }; acc = new[] { "#1F6FEB" }; ink = new[] { "#1B2330" }; }
            else { bg = new[] { "#F2F5F9", "#0E1319" }; card = new[] { "#FFFFFF", "#161D26" }; acc = new[] { "#1F6FEB" }; ink = new[] { "#1B2330" }; }
            Theme.Fill(g, r, r.Height / 2, Theme.C(bg[0]));
            if (bg.Length > 1) { var st = g.Save(); g.SetClip(new RectangleF(r.X + r.Width / 2, r.Y, r.Width / 2, r.Height)); Theme.Fill(g, r, r.Height / 2, Theme.C(bg[1])); g.Restore(st); }
            Theme.Stroke(g, r, r.Height / 2, Theme.Line, 1);
            float d = r.Height - 8 * S;
            using (var b = new SolidBrush(Theme.C(card[0]))) g.FillEllipse(b, r.X + 6 * S, r.Y + 4 * S, d, d);
            using (var b = new SolidBrush(Theme.C(acc[0]))) g.FillEllipse(b, r.X + 6 * S + d * 0.8f, r.Y + 4 * S, d, d);
            using (var b = new SolidBrush(Theme.C(ink[0]))) g.FillEllipse(b, r.X + 6 * S + d * 1.6f, r.Y + 4 * S, d, d);
        }
        // one setting as big cards: its options side by side, the chosen one marked, its help underneath
        float PaintBigChoice(Graphics g, float x, float y, float w, SetItem it, string acc)
        {
            using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T(it.Label).ToUpperInvariant(), Draw.fTiny, m, x, y + 2 * S); y += 22 * S;
            int n = it.Opts.Length, cur = it.Cur(), cols = Math.Max(1, Math.Min(n, Math.Min(4, (int)(w / (150 * S))))); float cw = (w - (cols - 1) * 10 * S) / cols, ch = 58 * S;
            for (int i = 0; i < n; i++)
            {
                var r = new RectangleF(x + (i % cols) * (cw + 10 * S), y + (i / cols) * (ch + 10 * S), cw, ch); bool on = i == cur;
                Theme.Fill(g, r, 12 * S, on ? Kit.Soft(acc) : Theme.Soft); Theme.Stroke(g, r, 12 * S, on ? Kit.Accent(acc) : Theme.Line, on ? 2 : 1);
                if (on) Kit.Icon(g, "check", new RectangleF(r.Right - 26 * S, r.Y + 10 * S, 16 * S, 16 * S), Kit.Accent(acc));
                if (it.Label == "Theme") ThemeSwatch(g, new RectangleF(r.Right - (on ? 92 : 70) * S, r.Y + (ch - 22 * S) / 2, 56 * S, 22 * S), i - 1);
                using (var b = new SolidBrush(on ? Kit.Ink(acc) : Theme.Ink)) using (var sf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(L.T(it.Opts[i]), Draw.fHead, b, new RectangleF(r.X + 14 * S, r.Y + (ch - 22 * S) / 2, r.Width - 44 * S, 24 * S), sf);
                int k = i; var set = it.Set; Act(settingsCanvas, r, delegate { set(k); settingsCanvas.Invalidate(); });
            }
            y += ((n + cols - 1) / cols) * (ch + 10 * S);
            string help = L.T(it.Help()); float hh = Draw.Measure(help, Draw.fSmall, (int)w).Height;
            using (var m = new SolidBrush(Theme.Sub)) g.DrawString(help, Draw.fSmall, m, new RectangleF(x, y + 2 * S, w, hh + 4)); y += hh + 18 * S;
            return y;
        }
        // ---------- first run: three questions on Home, big cards, Skip any time (new installs only)
        int setupStep;
        float PaintSetup(Graphics g, float x, float y, float w)
        {
            string[] qs = { "How should the corner widget look?", "How much should it watch?", "How should the buddy talk?" };
            string[][][] opts = {
                new[] { new[] { "speed", "Speed only", "Just the speeds, like DU Meter" }, new[] { "speedstatus", "Speed + status", "Speeds plus OK / Busy / Problem" }, new[] { "everyday", "Everyday", "The verdict, dots, CPU and speed" }, new[] { "auto", "Auto", "Changes for games and calls" } },
                new[] { new[] { "penny", "Penny", "The lightest touch" }, new[] { "balanced", "Balanced", "Notices trouble in seconds" }, new[] { "gamer", "Gamer", "Plus your game servers" } },
                new[] { new[] { "surprise", "Surprise me", "A new personality each day" }, new[] { "none", "Chill", "Easy-going, no character" }, new[] { "plain", "Plain", "Just the facts, no jokes" } } };
            int st = Math.Max(0, Math.Min(2, setupStep));
            var card = new RectangleF(x, y, w, 10); float top = y;
            y += 16 * S;
            using (var m = new SolidBrush(Kit.Ink("home"))) g.DrawString(L.T("QUICK SETUP") + "  ·  " + (st + 1) + " / 3", Draw.fTiny, m, x + 18 * S, y);
            string sk = L.T("Skip"); var ss = g.MeasureString(sk, Draw.fBodyB); var skr = new RectangleF(x + w - ss.Width - 20 * S, y - 4 * S, ss.Width + 4, 24 * S);
            using (var b = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(sk, Draw.fBodyB, b, skr.X, skr.Y);
            Act(overviewCanvas, skr, delegate { Cfg.SetupDone = true; Cfg.Save(); overviewCanvas.Invalidate(); });
            y += 22 * S;
            using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(L.T(qs[st]), Draw.fTitle, ink, x + 18 * S, y); y += 38 * S;
            var o = opts[st]; int cols = Math.Min(o.Length, w > 760 * S ? 4 : 2); float cw = (w - 36 * S - (cols - 1) * 10 * S) / cols, ch = st == 0 ? 112 * S : 70 * S;
            for (int i = 0; i < o.Length; i++)
            {
                var r = new RectangleF(x + 18 * S + (i % cols) * (cw + 10 * S), y + (i / cols) * (ch + 10 * S), cw, ch);
                Theme.Fill(g, r, 12 * S, Theme.Card); Theme.Stroke(g, r, 12 * S, Theme.Line, 1);
                using (var ink = new SolidBrush(Theme.Ink)) using (var m = new SolidBrush(Theme.Muted)) using (var sf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                {
                    g.DrawString(L.T(o[i][1]), Draw.fHead, ink, r.X + 14 * S, r.Y + 12 * S);
                    g.DrawString(L.T(o[i][2]), Draw.fSmall, m, new RectangleF(r.X + 14 * S, r.Y + 36 * S, r.Width - 28 * S, 18 * S), sf);
                }
                if (st == 0) { var pic = WPic(o[i][0], false); if (pic != null) { float k = Math.Min(1f, (r.Width - 28 * S) / pic.Width); g.DrawImage(pic, new RectangleF(r.X + 14 * S, r.Y + 62 * S, pic.Width * k, pic.Height * k)); } }
                string id = o[i][0]; int step = st;
                Act(overviewCanvas, r, delegate
                {
                    if (step == 0) Cfg.WidgetStyle = id;
                    else if (step == 1) { Cfg.ApplyPreset(id); Policy.Apply(); M.Nudge(); }
                    else { if (id == "plain") Voice.Serious = true; else { Voice.Serious = false; Lines.Persona = id; Lines.Mood = "chill"; } }
                    if (step == 2) Cfg.SetupDone = true; else setupStep = step + 1;
                    Cfg.Save(); Invalidate(true);
                });
            }
            y += ((o.Length + cols - 1) / cols) * (ch + 10 * S) + 10 * S;
            using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T("All of this can be changed any time in Settings."), Draw.fSmall, m, x + 18 * S, y); y += 26 * S;
            card.Height = y - top; Theme.Stroke(g, card, 14 * S, Kit.Accent("home"), 1.5f * S);
            return y + 14 * S;
        }
        // self-test: every setting lives under a question, every big card names a real setting, every question has a summary
        internal string QuestionCheck()
        {
            var bad = new List<string>(); var items = SetItems();
            foreach (var it in items) if (!SetSections.Contains(it.Sec)) bad.Add(it.Label + " has no question (" + it.Sec + ")");
            foreach (var sec in SetSections) { if (!SetQLook.ContainsKey(sec)) bad.Add(sec + " has no question"); foreach (var b in BigOnes(sec)) if (!items.Any(i => i.Sec == sec && i.Label == b)) bad.Add(sec + ": big card '" + b + "' isn't a setting"); SecSummary(sec, items); }
            if (SetSections[0] != "General" || SetSections[1] != "Widget") bad.Add("General first, Widget second");
            return string.Join("; ", bad);
        }
    }
}
