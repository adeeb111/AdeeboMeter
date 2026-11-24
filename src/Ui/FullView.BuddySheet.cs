// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== the buddy sheet (Adeeb, 14 Nov: "the menu looks generic")
    // A card under the buddy's face: a speech bubble with a real line in the mood and voice you have (hover a voice to hear
    // it), the moods as chips with their faces, the voices as coloured cards, More voices, None, Companion settings.
    // One click applies at once; a click outside or Esc closes it.
    sealed partial class FullView
    {
        DB buddySheet; PopFilter buddyFilter; string buddyHover = ""; bool buddyMore;
        // each voice's colour (the stripe and its letter)
        static readonly string[] VoiceColors = { "#3B82F6", "#E0484A", "#EC6FB3", "#22A06B", "#0EA5A4", "#6366F1", "#9B6CF0", "#64748B", "#16A34A", "#B45309", "#78716C", "#475569", "#DC2626", "#0284C7", "#C026D3", "#0891B2", "#4F46E5", "#DB2777" };
        static Color VoiceColor(string id) { int i = Array.FindIndex(Lines.Personas, p => p[0] == id); return i < 0 ? Theme.Amber : ColorTranslator.FromHtml(VoiceColors[i % VoiceColors.Length]); }

        void BuddySheet()
        {
            if (buddySheet != null && buddySheet.Visible) { HideBuddySheet(); return; }
            if (buddySheet == null)
            {
                buddySheet = new DB { Name = "buddySheet", Visible = false };
                buddySheet.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintBuddySheet(e.Graphics); }; Wire(buddySheet);
                buddySheet.MouseMove += delegate(object s, MouseEventArgs e) { string h = VoiceAt(e.Location); if (h != buddyHover) { buddyHover = h; buddySheet.Invalidate(); } };
                buddySheet.MouseLeave += delegate { if (buddyHover != "") { buddyHover = ""; buddySheet.Invalidate(); } };
                Controls.Add(buddySheet);
                Deactivate += delegate { HideBuddySheet(); };
            }
            buddyMore = false; buddyHover = "";
            SizeBuddySheet();
            buddySheet.BackColor = header.BackColor; buddySheet.Visible = true; buddySheet.BringToFront(); buddySheet.Invalidate();
            if (buddyFilter == null) buddyFilter = new PopFilter(this, () => buddySheet, () => RectangleToScreen(new Rectangle(gearBtn.Left - Sc(52), header.Top, Sc(48), header.Height)), HideBuddySheet);
            Application.AddMessageFilter(buddyFilter);
        }
        void SizeBuddySheet()
        {
            int rows = 3 + (buddyMore ? (Lines.Personas.Length - Lines.Featured + 2) / 3 : 0);
            int w = Math.Min(Sc(720), ClientSize.Width - Sc(24)), h = Sc(240) + (Lines.Faith != "off" ? Sc(130) : 0) + rows * Sc(68);   // + the words-of-comfort bar   // the bubble, the moods, the voices, the footer
            int right = gearBtn.Left + Sc(4);   // under the face, ending near the gear
            buddySheet.Bounds = new Rectangle(Math.Max(Sc(12), right - w), header.Bottom + Sc(6), w, Math.Min(h, ClientSize.Height - header.Bottom - Sc(20)));
            buddySheet.AutoScroll = true; buddySheet.AutoScrollMinSize = new Size(0, h > buddySheet.Height ? h : 0);   // AM-CG-178
        }
        void HideBuddySheet()
        {
            if (buddySheet == null || !buddySheet.Visible) return;
            buddySheet.Visible = false; if (buddyFilter != null) Application.RemoveMessageFilter(buddyFilter);
        }
        // the voice card under the mouse (for the preview line)
        readonly System.Collections.Generic.List<Tuple<RectangleF, string>> voiceRects = new System.Collections.Generic.List<Tuple<RectangleF, string>>();
        string VoiceAt(Point p) { foreach (var t in voiceRects) if (t.Item1.Contains(p)) return t.Item2; return ""; }

        void PaintBuddySheet(Graphics g)
        {
            g.TranslateTransform(buddySheet.AutoScrollPosition.X, buddySheet.AutoScrollPosition.Y);
            Begin(buddySheet); voiceRects.Clear();
            var card = new RectangleF(1, 1, buddySheet.ClientSize.Width - 3, Math.Max(buddySheet.ClientSize.Height, buddySheet.AutoScrollMinSize.Height) - 3);
            Theme.Fill(g, card, 18 * S, Theme.Card); Theme.Stroke(g, card, 18 * S, Theme.Line, 1);
            float x = 18 * S, y = 16 * S, w = card.Width - 36 * S;
            bool plain = Voice.Serious;
            // the face and the bubble: what it would say now - in the voice under the mouse, if any
            string who = buddyHover != "" && buddyHover != "none" ? (buddyHover == "surprise" ? Lines.Today() : buddyHover) : Lines.Who();
            string line = plain ? L.T("Plain words: no jokes, just what's happening.") : (Lines.SteadyAs("all.good", null, who) ?? "");
            if (line == "") line = L.T("All quiet. Nothing to worry about.");
            var face = new RectangleF(x, y, 60 * S, 60 * S);
            using (var fb = new SolidBrush(Kit.Soft("mine"))) g.FillEllipse(fb, face);
            var em = Draw.Emoji(plain ? "1f610" : Draw.MoodEmoji(Lines.Mood)); if (em != null) g.DrawImage(em, new RectangleF(face.X + 10 * S, face.Y + 10 * S, 40 * S, 40 * S));
            float bx = face.Right + 16 * S, bw = w - (face.Right - x) - 16 * S - 120 * S;
            var bubble = new RectangleF(bx, y, bw, 60 * S); Theme.Fill(g, bubble, 14 * S, Theme.Soft);
            using (var tri = new SolidBrush(Theme.Soft)) g.FillPolygon(tri, new[] { new PointF(bx, y + 22 * S), new PointF(bx - 8 * S, y + 29 * S), new PointF(bx, y + 36 * S) });
            string tag = plain ? Kit.Sent(L.T("PLAIN")) : (L.T(Lines.MoodName(Lines.Mood)) + " · " + L.T("talks like: ") + L.T(Lines.PersonaName(buddyHover != "" ? buddyHover : Lines.Persona)));
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(tag, Draw.fTiny, mb, bx + 14 * S, y + 9 * S);
            using (var ib = new SolidBrush(Theme.Ink)) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.NoWrap }) { string ty = Typing.Show("sheet", line, buddySheet); g.DrawString("“" + ty + (ty == line ? "”" : ""), Draw.fBody, ib, new RectangleF(bx + 14 * S, y + 28 * S, bw - 24 * S, 24 * S), sf); }
            // Fun lines switch
            var fr = new RectangleF(x + w - 108 * S, y + 18 * S, 108 * S, 24 * S);
            using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(L.T("Fun lines"), Draw.fSmall, sb, fr.X, fr.Y + 3 * S);
            var k = new RectangleF(fr.Right - 38 * S, fr.Y + 1 * S, 38 * S, 22 * S); Theme.Fill(g, k, 11 * S, plain ? Theme.Track : Theme.Blue);
            using (var kb = new SolidBrush(Color.White)) g.FillEllipse(kb, plain ? k.X + 2 * S : k.Right - 20 * S, k.Y + 2 * S, 18 * S, 18 * S);
            Act(buddySheet, fr, delegate { Voice.Serious = !Voice.Serious; Cfg.Save(); Invalidate(true); buddySheet.Invalidate(); });
            y += 76 * S;
            if (Lines.Faith != "off") y = PaintComfortBar(g, x, y, w) + 14 * S;   // Adeeb, 22 Nov: no religion choice on show by default - it lives in Settings › Companion; the bar only appears once you turned it on there

            // moods: chips with their faces
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Kit.Sent(L.T("MOOD - HOW IT FEELS ABOUT THINGS")), Draw.fTiny, mb, x, y); y += 20 * S;
            float cx = x;
            for (int i = 0; i < Lines.Moods.Length; i++)
            {
                string id = Lines.Moods[i], nm = L.T(Lines.MoodNames[i]); bool on = id == "plain" ? plain : !plain && Lines.Mood == id;
                var ts = g.MeasureString(nm, Draw.fBodyB); var r = new RectangleF(cx, y, ts.Width + 46 * S, 34 * S);
                if (r.Right > x + w) { cx = x; y += 42 * S; r = new RectangleF(cx, y, r.Width, r.Height); }
                Theme.Fill(g, r, 17 * S, on ? Kit.Soft("ok") : Theme.Soft); Theme.Stroke(g, r, 17 * S, on ? Kit.Accent("ok") : Theme.Line, on ? 1.5f * S : 1);
                var e = Draw.Emoji(Draw.MoodEmoji(id)); if (e != null) g.DrawImage(e, new RectangleF(r.X + 8 * S, r.Y + 7 * S, 20 * S, 20 * S));
                using (var tb = new SolidBrush(on ? Kit.Ink("ok") : Theme.Ink)) g.DrawString(nm, Draw.fBodyB, tb, r.X + 34 * S, r.Y + (34 * S - ts.Height) / 2);
                Act(buddySheet, r, delegate { if (id == "plain") Voice.Serious = true; else { Voice.Serious = false; Lines.Mood = id; } Cfg.Save(); Invalidate(true); buddySheet.Invalidate(); });
                cx = r.Right + 8 * S;
            }
            y += 48 * S;

            // voices: coloured cards with a sample line
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(Kit.Sent(L.T("TALKS LIKE - HOVER TO HEAR ONE")), Draw.fTiny, mb, x, y); y += 20 * S;
            var ids = new System.Collections.Generic.List<string> { "surprise" }; ids.AddRange(Lines.Personas.Take(Lines.Featured).Select(p => p[0]));
            if (buddyMore) ids.AddRange(Lines.Personas.Skip(Lines.Featured).Select(p => p[0]));
            int cols = w > 560 * S ? 3 : 2; float gap = 8 * S, cw = (w - (cols - 1) * gap) / cols, chh = 60 * S;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i]; bool on = !plain && Lines.Persona == id;
                var r = new RectangleF(x + (i % cols) * (cw + gap), y + (i / cols) * (chh + gap), cw, chh);
                Color c = id == "surprise" ? Theme.Amber : VoiceColor(id);
                Theme.Fill(g, r, 12 * S, on || buddyHover == id ? Kit.Mix(Theme.Card, c, 0.12f) : Theme.Soft);
                if (on) Theme.Stroke(g, r, 12 * S, c, 1.5f * S);
                // a round badge with the voice's first letter (or the dice for Surprise me)
                var bd = new RectangleF(r.X + 14 * S, r.Y + 10 * S, 22 * S, 22 * S); using (var bb = new SolidBrush(c)) g.FillEllipse(bb, bd);
                string letter = id == "surprise" ? "?" : L.T(Lines.PersonaName(id)).Substring(0, 1).ToUpperInvariant();
                using (var lb = new SolidBrush(Color.White)) using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(letter, Draw.fTiny, lb, bd, sf);
                using (var tb = new SolidBrush(Theme.Ink)) using (var tf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(L.T(Lines.PersonaName(id)), Draw.fBodyB, tb, new RectangleF(bd.Right + 8 * S, r.Y + 12 * S, r.Right - bd.Right - 8 * S - (on ? g.MeasureString(Kit.Sent(L.T("IN USE")), Draw.fTiny).Width + 30 * S : 8 * S), 20 * S), tf);   // AM-CG-179
                if (on) { string iu = Kit.Sent(L.T("IN USE")); var iz = g.MeasureString(iu, Draw.fTiny); var ir = new RectangleF(r.Right - iz.Width - 22 * S, r.Y + 11 * S, iz.Width + 12 * S, 18 * S); Theme.Fill(g, ir, 6 * S, c); using (var wb = new SolidBrush(Color.White)) g.DrawString(iu, Draw.fTiny, wb, ir.X + 6 * S, ir.Y + (18 * S - iz.Height) / 2); }
                string sample = id == "surprise" ? L.T("A different voice every day") : "“" + (Lines.SteadyAs("all.good", null, id) ?? "") + "”";
                using (var sb = new SolidBrush(Theme.Sub)) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(sample, Draw.fSmall, sb, new RectangleF(r.X + 14 * S, r.Y + 37 * S, r.Width - 26 * S, 18 * S), sf);
                voiceRects.Add(Tuple.Create(r, id));
                Act(buddySheet, r, delegate { Lines.Persona = id; Voice.Serious = false; Cfg.Save(); Invalidate(true); buddySheet.Invalidate(); });
            }
            y += ((ids.Count + cols - 1) / cols) * (chh + gap) + 4 * S;
            // More voices · None · Companion settings
            using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) using (var sb = new SolidBrush(Theme.Sub))
            {
                string mt = buddyMore ? L.T("Fewer voices ‹") : L.T("More voices") + " (" + (Lines.Personas.Length - Lines.Featured) + ") ›"; var ms = g.MeasureString(mt, Draw.fBodyB);
                var mr = new RectangleF(x, y, ms.Width, ms.Height); g.DrawString(mt, Draw.fBodyB, lb, mr.X, mr.Y);
                Act(buddySheet, mr, delegate { buddyMore = !buddyMore; SizeBuddySheet(); buddySheet.Invalidate(); });
                string nt = (Lines.Persona == "none" && !plain ? "● " : "○ ") + L.T("None - the mood talks on its own"); var ns = g.MeasureString(nt, Draw.fBody);
                var nr = new RectangleF(mr.Right + 28 * S, y, ns.Width, ns.Height); g.DrawString(nt, Draw.fBody, sb, nr.X, nr.Y);
                Act(buddySheet, nr, delegate { Lines.Persona = "none"; Voice.Serious = false; Cfg.Save(); Invalidate(true); buddySheet.Invalidate(); });
                string ct = L.T("Companion settings ›"); var cs = g.MeasureString(ct, Draw.fBodyB); var cr = new RectangleF(x + w - cs.Width, y, cs.Width, cs.Height);
                g.DrawString(ct, Draw.fBodyB, lb, cr.X, cr.Y); Act(buddySheet, cr, delegate { HideBuddySheet(); OpenSettingsAt("Companion"); });
            }
        }

        // ---------- words of comfort, highlighted: on/off, which faith (or what they share), and today's line
        float PaintComfortBar(Graphics g, float x, float y, float w)
        {
            bool on = Lines.Faith != "off"; Color warm = Color.FromArgb(Theme.Dark ? 0x3A : 0xFF, Theme.Dark ? 0x30 : 0xF6, Theme.Dark ? 0x1C : 0xE0);
            int chipRows = 1; { float mx = x + 14 * S + g.MeasureString(Kit.Sent(L.T("WORDS OF COMFORT")), Draw.fTiny).Width + 60 * S; for (int i = 1; i < Lines.Faiths.Length; i++) { float cw0 = g.MeasureString(L.T(Lines.FaithNames[i]), Draw.fSmall).Width + 24 * S; if (mx + cw0 > x + w - 10 * S) { chipRows++; mx = x + 14 * S; } mx += cw0; } }
            var bar = new RectangleF(x, y, w, 88 * S + chipRows * 30 * S); Theme.Fill(g, bar, 14 * S, on ? warm : Theme.Soft);   /* AM-CG-179: as tall as its chips need */ if (on) Theme.Stroke(g, bar, 14 * S, Theme.Amber, 1);   // room for two rows of faiths
            float bx = bar.X + 14 * S, by = bar.Y + 10 * S;
            using (var hb = new SolidBrush(on ? (Theme.Dark ? Color.FromArgb(0xF2, 0xC9, 0x6B) : Color.FromArgb(0x8A, 0x5A, 0x00)) : Theme.Muted)) g.DrawString(Kit.Sent(L.T("WORDS OF COMFORT")), Draw.fTiny, hb, bx, by + 3 * S);
            // the switch: on picks the last faith chosen (what they share, if none)
            var k = new RectangleF(bx + g.MeasureString(Kit.Sent(L.T("WORDS OF COMFORT")), Draw.fTiny).Width + 10 * S, by, 34 * S, 20 * S); Theme.Fill(g, k, 10 * S, on ? Theme.Amber : Theme.Track);
            using (var kb = new SolidBrush(Color.White)) g.FillEllipse(kb, on ? k.Right - 18 * S : k.X + 2 * S, k.Y + 2 * S, 16 * S, 16 * S);
            Act(buddySheet, new RectangleF(bx, by - 2 * S, k.Right - bx + 4 * S, 24 * S), delegate { if (Lines.Faith == "off") Lines.Faith = Lines.LastFaith; else { Lines.LastFaith = Lines.Faith; Lines.Faith = "off"; } Cfg.Save(); Invalidate(true); buddySheet.Invalidate(); });
            // the faiths, as small chips ("Common to all" first)
            float cx = k.Right + 16 * S;
            for (int i = 1; i < Lines.Faiths.Length; i++)
            {
                string id = Lines.Faiths[i], nm = L.T(Lines.FaithNames[i]); bool sel = Lines.Faith == id; var ts = g.MeasureString(nm, Draw.fSmall);
                var r = new RectangleF(cx, by - 2 * S, ts.Width + 18 * S, 24 * S); if (r.Right > bar.Right - 10 * S) { cx = bx; by += 30 * S; r = new RectangleF(cx, by - 2 * S, r.Width, r.Height); }   // the rest on a second row
                Theme.Fill(g, r, 12 * S, sel ? Theme.Amber : Theme.Card); if (!sel) Theme.Stroke(g, r, 12 * S, Theme.Line, 1);
                using (var tb = new SolidBrush(sel ? Color.White : Theme.Sub)) g.DrawString(nm, Draw.fSmall, tb, r.X + 9 * S, r.Y + (24 * S - ts.Height) / 2);
                Act(buddySheet, r, delegate { Lines.Faith = id; Lines.LastFaith = id; Cfg.Save(); Invalidate(true); buddySheet.Invalidate(); });
                cx = r.Right + 6 * S;
            }
            // today's line - or why there isn't one
            string line = !on ? L.T("Off. Turn it on for one calm line a day - from your faith, word for word, or what all of them share.") : Voice.Serious ? L.T("Not shown with Fun lines off (Plain).") : Lines.Mood == "roast" ? L.T("Not shown in Roast - pick another mood to see it.") : Lines.Comfort();
            using (var lb = new SolidBrush(on ? Theme.Ink : Theme.Muted)) using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                g.DrawString(line, on ? Draw.fBody : Draw.fSmall, lb, new RectangleF(bx, by + 30 * S, bar.Width - 28 * S, bar.Bottom - by - 34 * S), sf);
            return bar.Bottom;
        }

        // a click outside a popup (and outside its own button) closes it
        sealed class PopFilter : IMessageFilter
        {
            readonly FullView v; readonly Func<Control> pop; readonly Func<Rectangle> button; readonly Action close;
            public PopFilter(FullView v, Func<Control> pop, Func<Rectangle> button, Action close) { this.v = v; this.pop = pop; this.button = button; this.close = close; }
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x201 && m.Msg != 0x204 && m.Msg != 0xA1) return false;
                try
                {
                    var c = pop(); if (v.IsDisposed || c == null || !c.Visible) return false;
                    var p = Control.MousePosition;
                    if (c.RectangleToScreen(c.ClientRectangle).Contains(p) || button().Contains(p)) return false;
                    v.BeginInvoke(close);
                }
                catch (InvalidOperationException) { }
                return false;
            }
        }
    }
}
