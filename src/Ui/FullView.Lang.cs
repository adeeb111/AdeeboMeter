using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== the language picker (Adeeb, 21 Nov): a button beside VIEW that
    // shows the language in its own name, and a card under it - one tile per language: "Welcome" in its own script, its own
    // colours and pattern, how much is translated, a preview of real app words while the mouse is on it. A pick switches at once;
    // what was built before the switch catches up at a restart, so a Restart button shows in the top bar until then.
    sealed partial class FullView
    {
        LangButton langBtn; PillButton restartBtn; DB langSheet; LangFilter langFilter; string langHover, langDebug; int langKey = -1;
        static string runLang;   // the language this run started in: a different one now = a restart finishes the switch

        // per language: greeting, English name, two colours, the pattern on its tile
        static readonly Dictionary<string, string[]> LangLook = new Dictionary<string, string[]>
        {
            { "en", new[] { "Welcome", "The original", "#1F6FEB", "#16C2A3", "rings" } },
            { "fr", new[] { "Bienvenue", "French (Canada)", "#4F46E5", "#A855F7", "dots" } },
            { "es", new[] { "Bienvenido", "Spanish", "#F59E0B", "#EF4444", "waves" } },
            { "ar", new[] { "أهلاً وسهلاً", "Arabic", "#059669", "#14B8A6", "arches" } },
            { "bn", new[] { "স্বাগতম", "Bangla", "#E11D48", "#16A34A", "petals" } },
        };
        static readonly string[] PreviewWords = { "Settings", "Details", "Speed", "Back to corner" };
        static string[] LookOfLang(string code) { string[] l; return LangLook.TryGetValue(code, out l) ? l : new[] { code, code, "#1F6FEB", "#16C2A3", "rings" }; }
        static Color Hex(string h, int alpha) { var c = ColorTranslator.FromHtml(h); return Color.FromArgb(alpha, c); }

        // a font that has the script: Inter (the app's own) has no Bangla or Arabic letters, and a font from the exe gets no fallback
        static readonly Dictionary<string, Font> langFonts = new Dictionary<string, Font>();
        internal static Font LangFont(string code, float px, bool bold)
        {
            string key = code + "|" + px.ToString("0.#", CultureInfo.InvariantCulture) + "|" + bold;
            Font f; if (langFonts.TryGetValue(key, out f)) return f;
            string fam = code == "bn" ? Draw.Fam("Nirmala UI", "Vrinda", "Segoe UI") : code == "ar" ? Draw.Fam("Segoe UI", "Tahoma") : Draw.Fam("Segoe UI Variable Display", "Segoe UI");
            f = new Font(fam, px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            if (langFonts.Count > 60) { foreach (var o in langFonts.Values) o.Dispose(); langFonts.Clear(); }
            langFonts[key] = f; return f;
        }
        static string NativeName(string code) { var l = L.Languages.FirstOrDefault(x => x[0] == code); return l != null ? l[1] : code; }

        void MakeLangButton()
        {
            if (runLang == null) runLang = L.Code;
            langBtn = new LangButton(); SyncLangBtn();
            langBtn.Click += delegate { LangMenu(); };
            restartBtn = new PillButton("Restart to finish", PillButton.K.Chip) { Visible = false, Accent = "mine" };
            restartBtn.Click += delegate { RestartApp(); };
            header.Controls.Add(langBtn); header.Controls.Add(restartBtn);
            langBtn.Resize += delegate { PlaceTop(); }; restartBtn.Resize += delegate { PlaceTop(); };
            SyncRestart();
        }
        void SyncLangBtn() { if (langBtn != null) langBtn.Set(L.Code, NativeName(L.Code)); }
        void SyncRestart()
        {
            if (restartBtn == null) return;
            bool need = runLang != null && L.Code != runLang;
            restartBtn.Text = "Restart to finish";
            if (restartBtn.Visible != need) { restartBtn.Visible = need; PlaceTop(); }
        }
        // called by both pickers (the top button and Settings)
        internal void PickLanguage(string code)
        {
            if (!L.Languages.Any(l => l[0] == code)) return;
            Cfg.Lang = code; Cfg.Save(); L.Use(Cfg.Lang);
            ApplyMode(); SyncPointsBtn(); SyncViewBtn(); SyncLangBtn(); SyncRestart(); PlaceTop(); Invalidate(true);
        }

        // ---------- start again: a new copy waits for this one to let go, then opens this window
        void RestartApp()
        {
            try
            {
                using (Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--restart") { UseShellExecute = false })) { }
                using (var q = EventWaitHandle.OpenExisting(Single.QuitName)) q.Set();   // the same polite "please leave" an update uses
                Program.Log("restart asked (language " + L.Code + ")");
            }
            catch (Exception e)
            {
                if (!(e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is WaitHandleCannotBeOpenedException || e is UnauthorizedAccessException)) throw;
                Program.Log("restart: " + e.Message); Toast(L.T("Couldn't restart - close AdeeboMeter from the tray icon and open it again"));
            }
        }

        // ---------- the card under the button
        void LangMenu()
        {
            if (langSheet != null && langSheet.Visible) { HideLangSheet(); return; }
            HideTopMenu(); if (viewSheet != null && viewSheet.Visible && switchingTo == null) HideViewSheet();
            if (langSheet == null)
            {
                langSheet = new DB { Name = "langSheet", BackColor = Theme.Bg, Visible = false, TabStop = true };
                langSheet.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintLangSheet(e.Graphics); }; Wire(langSheet);
                langSheet.MouseMove += delegate(object s, MouseEventArgs e) { string h = TileAt(e.Location); if (h != langHover) { langHover = h; langSheet.Invalidate(); } };
                langSheet.MouseLeave += delegate { if (langHover != null) { langHover = null; langSheet.Invalidate(); } };
                langSheet.PreviewKeyDown += delegate(object s, PreviewKeyDownEventArgs e) { if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter) e.IsInputKey = true; };
                langSheet.KeyDown += delegate(object s, KeyEventArgs e)
                {
                    int n = L.Languages.Length; if (langKey < 0) langKey = Math.Max(0, Array.FindIndex(L.Languages, l => l[0] == L.Code));
                    bool rtl = L.IsRtl;
                    if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down) { langKey = (langKey + (rtl && e.KeyCode == Keys.Right ? n - 1 : 1)) % n; }
                    else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up) { langKey = (langKey + (rtl && e.KeyCode == Keys.Left ? 1 : n - 1)) % n; }
                    else if (e.KeyCode == Keys.Enter) { string c = L.Languages[langKey][0]; HideLangSheet(); PickLanguage(c); e.Handled = true; return; }
                    else if (e.KeyCode == Keys.Escape) { HideLangSheet(); e.Handled = true; return; }
                    else return;
                    langHover = L.Languages[langKey][0]; e.Handled = true; langSheet.Invalidate();
                };
                Controls.Add(langSheet);
                Deactivate += delegate { HideLangSheet(); };
                Resize += delegate { if (langSheet.Visible) PlaceLangSheet(); };
            }
            langHover = null; langKey = -1; SetTipKey(null);
            langSheet.BackColor = Theme.Bg; PlaceLangSheet();
            langSheet.Visible = true; langSheet.BringToFront(); langSheet.Focus(); langSheet.Invalidate();
            langBtn.Selected = true;
            if (langFilter == null) langFilter = new LangFilter(this);
            Application.AddMessageFilter(langFilter);
        }
        void HideLangSheet()
        {
            if (langSheet == null || !langSheet.Visible) return;
            langSheet.Visible = false; langBtn.Selected = false; langHover = null;
            if (langFilter != null) Application.RemoveMessageFilter(langFilter);
        }
        int LangCols(float w) { int n = L.Languages.Length; return w >= 860 * S ? n : w >= 520 * S ? 3 : 2; }
        void PlaceLangSheet()
        {
            int n = L.Languages.Length; int w = Math.Min(Sc(1010), ClientSize.Width - Sc(24));
            int cols = LangCols(w), rows = (n + cols - 1) / cols;
            int h = (int)((22 + 12 + 34 + 16) * S + rows * (236 * S) + (rows - 1) * 14 * S + (18 + 52 + 12 + 26 + 18) * S);
            var at = PointToClient(langBtn.Parent.PointToScreen(new Point(langBtn.Left + langBtn.Width / 2, langBtn.Bottom)));
            int x = Math.Max(Sc(12), Math.Min(at.X - Sc(190), ClientSize.Width - w - Sc(12)));
            langSheet.Bounds = new Rectangle(x, at.Y + Sc(2), w, Math.Min(h, ClientSize.Height - at.Y - Sc(8)));
        }
        // the tile under a point (its id), from the same geometry the paint uses
        readonly Dictionary<string, RectangleF> langTiles = new Dictionary<string, RectangleF>();
        string TileAt(Point p) { foreach (var kv in langTiles) if (kv.Value.Contains(p)) return kv.Key; return null; }

        sealed class LangFilter : IMessageFilter
        {
            readonly FullView v; public LangFilter(FullView v) { this.v = v; }
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x201 && m.Msg != 0x204 && m.Msg != 0xA1) return false;
                try
                {
                    if (v.IsDisposed || v.langSheet == null || !v.langSheet.Visible) return false;
                    var p = Control.MousePosition;
                    if (v.langSheet.RectangleToScreen(v.langSheet.ClientRectangle).Contains(p) || v.langBtn.RectangleToScreen(v.langBtn.ClientRectangle).Contains(p)) return false;
                    v.BeginInvoke((Action)v.HideLangSheet);
                }
                catch (InvalidOperationException) { }
                return false;
            }
        }

        void PaintLangSheet(Graphics g)
        {
            Begin(langSheet); langTiles.Clear(); string hov = langHover ?? langDebug;
            float W = langSheet.ClientSize.Width, H = langSheet.ClientSize.Height;
            // the pointer up to the button, then the card
            var bc = langSheet.PointToClient(langBtn.Parent.PointToScreen(new Point(langBtn.Left + langBtn.Width / 2, 0)));
            var card = new RectangleF(1, 10 * S, W - 3, H - 10 * S - 2);
            Theme.Fill(g, card, 20 * S, Theme.Card); Theme.Stroke(g, card, 20 * S, Theme.Line, 1);
            float px = Math.Max(30 * S, Math.Min(W - 30 * S, bc.X));
            var tri = new[] { new PointF(px - 9 * S, card.Y + 1), new PointF(px, card.Y - 9 * S), new PointF(px + 9 * S, card.Y + 1) };
            using (var b = new SolidBrush(Theme.Card)) g.FillPolygon(b, tri);
            using (var p = new Pen(Theme.Line, 1)) { g.DrawLine(p, tri[0], tri[1]); g.DrawLine(p, tri[1], tri[2]); }
            float x = 24 * S, y = card.Y + 20 * S, w = W - 48 * S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var muted = new SolidBrush(Theme.Muted))
            {
                string title = L.T("Pick your language"); g.DrawString(title, Draw.fTitle, ink, x, y);
                float tw = g.MeasureString(title, Draw.fTitle).Width;
                string note = L.T("AdeeboMeter switches right away. A restart finishes the last few screens.");
                if (tw + g.MeasureString(note, Draw.fBody).Width + 20 * S < w) g.DrawString(note, Draw.fBody, sub, x + tw + 12 * S, y + 8 * S);
                y += 34 * S + 16 * S;
                int n = L.Languages.Length, cols = LangCols(W); float gap = 14 * S, tw2 = (w - (cols - 1) * gap) / cols, th = 236 * S;
                for (int i = 0; i < n; i++)
                {
                    string code = L.Languages[i][0];
                    var r = new RectangleF(x + (i % cols) * (tw2 + gap), y + (i / cols) * (th + gap), tw2, th);
                    langTiles[code] = r;
                    PaintLangTile(g, code, r, code == L.Code, code == hov);
                    string c = code; Act(langSheet, r, delegate { HideLangSheet(); PickLanguage(c); });
                }
                y += ((n + cols - 1) / cols) * (th + gap) + 4 * S;
                // the preview strip: real words from the app in the language under the mouse
                var pr = new RectangleF(x, y, w, 52 * S);
                string pv = hov ?? L.Code; var look = LookOfLang(pv);
                using (var lg = new LinearGradientBrush(pr, Hex(look[2], Theme.Dark ? 40 : 22), Hex(look[3], Theme.Dark ? 40 : 22), 0f)) using (var path = Theme.Round(pr, 14 * S)) g.FillPath(lg, path);
                float cx = pr.X + 18 * S;
                string k = hov != null && hov != L.Code ? L.T("PREVIEW OF") + " " + NativeName(pv) : L.T("POINT AT A LANGUAGE TO PREVIEW IT");
                var kf = LangFont(L.Code == "bn" || pv == "bn" ? "bn" : L.Code == "ar" || pv == "ar" ? "ar" : "en", 10.5f * S, true);
                using (var kb = new SolidBrush(Kit.Mix(Hex(look[2], 255), Theme.Ink, Theme.Dark ? 0.35f : 0.1f))) { var ks = g.MeasureString(k, kf); g.DrawString(k, kf, kb, cx, pr.Y + (pr.Height - ks.Height) / 2); cx += ks.Width + 14 * S; }
                if (hov != null && hov != L.Code)
                {
                    var cf = LangFont(pv, 13 * S, false);
                    using (var rtl = new StringFormat(pv == "ar" ? StringFormatFlags.DirectionRightToLeft : 0))
                        foreach (var word in PreviewWords)
                        {
                            string t = L.In(pv, word); var ts = g.MeasureString(t, cf);
                            var chip = new RectangleF(cx, pr.Y + (pr.Height - 30 * S) / 2, ts.Width + 26 * S, 30 * S);
                            if (chip.Right > pr.Right - 12 * S) break;
                            Theme.Fill(g, chip, 15 * S, Theme.Card); Theme.Stroke(g, chip, 15 * S, Theme.Line, 1);
                            g.DrawString(t, cf, ink, new RectangleF(chip.X + 13 * S, chip.Y + (chip.Height - ts.Height) / 2, ts.Width + 2, ts.Height), rtl);
                            cx = chip.Right + 8 * S;
                        }
                }
                string right = L.T("Nothing changes until you click a card."); var rs = g.MeasureString(right, Draw.fSmall);
                if (cx + rs.Width + 24 * S < pr.Right) g.DrawString(right, Draw.fSmall, sub, pr.Right - rs.Width - 18 * S, pr.Y + (pr.Height - rs.Height) / 2);
                y = pr.Bottom + 12 * S;
                g.DrawString(L.T("Arrow keys move, Enter picks, Esc closes") + "   ·   " + L.T("Arabic reads right to left - the whole window turns around."), Draw.fSmall, muted, new RectangleF(x, y, w, 22 * S));
            }
        }

        void PaintLangTile(Graphics g, string code, RectangleF r, bool cur, bool hot)
        {
            var look = LookOfLang(code); Color a = Hex(look[2], 255), b = Hex(look[3], 255); bool rtl = code == "ar";
            if (hot && !cur) { r.Offset(0, -3 * S); for (int i = 1; i <= 4; i++) Theme.Fill(g, new RectangleF(r.X - i * S + 2 * S, r.Y + i * 2 * S, r.Width + 2 * i * S - 4 * S, r.Height), 16 * S + i * S, Color.FromArgb(Theme.Dark ? 22 : 10, 20, 30, 60)); }
            Theme.Fill(g, r, 16 * S, Theme.Card);
            // the pattern, top corner, faded
            using (var clip = Theme.Round(r, 16 * S))
            {
                var st = g.Save(); g.SetClip(clip);
                PaintLangArt(g, look[4], new RectangleF(r.Right - 150 * S, r.Y - 34 * S, 160 * S, 124 * S), Color.FromArgb(hot || cur ? 170 : 120, a), Color.FromArgb(hot || cur ? 170 : 120, b));
                g.Restore(st);
            }
            if (cur) using (var path = Theme.Round(new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), 15 * S)) using (var lg = new LinearGradientBrush(r, Hex("#7C4DFF", 255), Hex("#EC4899", 255), 45f)) using (var pen = new Pen(lg, 2.5f * S)) g.DrawPath(pen, path);
            else Theme.Stroke(g, r, 16 * S, hot ? Kit.Mix(a, Theme.Card, 0.4f) : Theme.Line, hot ? 1.5f * S : 1);
            if (cur)   // the tick
            {
                var tk = new RectangleF(r.X + 14 * S, r.Y + 14 * S, 26 * S, 26 * S);
                using (var lg = new LinearGradientBrush(tk, Hex("#7C4DFF", 255), Hex("#EC4899", 255), 45f)) g.FillEllipse(lg, tk);
                using (var p = new Pen(Color.White, 2.2f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(p, new[] { new PointF(tk.X + 7.5f * S, tk.Y + 13.5f * S), new PointF(tk.X + 11.5f * S, tk.Y + 17.5f * S), new PointF(tk.X + 18.5f * S, tk.Y + 9 * S) });
            }
            float px = r.X + 16 * S, pw = r.Width - 32 * S, y = r.Y + 98 * S;
            using (var sf = new StringFormat(rtl ? StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap : StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter })
            {
                // "Welcome" in its own script, in its own two colours
                float gpx = (code == "bn" ? 28 : 27) * S; var gf = LangFont(code, gpx, true);
                while (gpx > 15 * S && g.MeasureString(look[0], gf).Width > pw) { gpx -= 1.5f * S; gf = LangFont(code, gpx, true); }   // a narrow tile: the greeting shrinks, never cut
                var gr = new RectangleF(px, y, pw, gf.GetHeight(g) + 4 * S);
                using (var lg = new LinearGradientBrush(new RectangleF(gr.X - 1, gr.Y, gr.Width + 2, gr.Height), a, b, 0f)) g.DrawString(look[0], gf, lg, gr, sf);
                y = gr.Bottom + 6 * S;
                var nf = LangFont(code, 15 * S, true); using (var ib = new SolidBrush(Theme.Ink)) g.DrawString(NativeName(code), nf, ib, new RectangleF(px, y, pw, nf.GetHeight(g) + 2), sf);
                y += nf.GetHeight(g) + 2 * S;
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T(look[1]), Draw.fSmall, mb, new RectangleF(px, y, pw, 18 * S), sf);
            }
            // how much is done
            int pct = code == "en" ? 100 : L.Coverage(code);
            float by = r.Bottom - 22 * S;
            using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(code == "en" ? L.T("Complete") : pct >= 0 ? pct + "% " + L.T("translated") : L.T("Partly translated"), Draw.fSmall, sb, px, by - 20 * S);
            var track = new RectangleF(px, by, pw, 6 * S); Theme.Fill(g, track, 3 * S, Theme.Line);
            if (pct > 0) { var fill = new RectangleF(px, by, Math.Max(6 * S, pw * Math.Min(100, pct) / 100f), 6 * S); using (var lg = new LinearGradientBrush(new RectangleF(fill.X - 1, fill.Y, fill.Width + 2, fill.Height), a, b, 0f)) using (var path = Theme.Round(fill, 3 * S)) g.FillPath(lg, path); }
            if (hot && langKey >= 0) Theme.Stroke(g, new RectangleF(r.X - 3 * S, r.Y - 3 * S, r.Width + 6 * S, r.Height + 6 * S), 19 * S, Theme.BlueDark, 2);   // keyboard focus
        }

        // the patterns: drawn, never a flag (a language isn't a country)
        static void PaintLangArt(Graphics g, string kind, RectangleF box, Color a, Color b)
        {
            using (var lg = new LinearGradientBrush(box, a, b, 45f))
            {
                float cx = box.X + box.Width * 0.62f, cy = box.Y + box.Height * 0.36f;
                switch (kind)
                {
                    case "rings":
                        for (int i = 0; i < 3; i++)
                            using (var rb = new LinearGradientBrush(box, Color.FromArgb(a.A * (4 - i) / 4, a), Color.FromArgb(b.A * (4 - i) / 4, b), 45f)) using (var p = new Pen(rb, 10 * S))
                            { float rr = (22 + 24 * i) * S; g.DrawEllipse(p, cx - rr, cy - rr, 2 * rr, 2 * rr); }
                        break;
                    case "dots":
                        for (int ix = 0; ix < 6; ix++) for (int iy = 0; iy < 4; iy++) { float rr = (3 + ((ix + iy) % 3) * 2) * S; g.FillEllipse(lg, box.X + 30 * S + ix * 22 * S - rr, box.Y + 40 * S + iy * 22 * S - rr, 2 * rr, 2 * rr); }
                        break;
                    case "waves":
                        for (int j = 0; j < 4; j++)
                            using (var p = new Pen(lg, 8 * S) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                            {
                                float yy = box.Y + 48 * S + j * 19 * S; var pts = new List<PointF>();
                                for (int k = 0; k <= 24; k++) { float t = k / 24f; pts.Add(new PointF(box.X + 10 * S + t * box.Width, yy + (float)Math.Sin(t * Math.PI * 2 + j * 0.6) * 9 * S)); }
                                g.DrawCurve(p, pts.ToArray());
                            }
                        break;
                    case "arches":
                        for (int j = 0; j < 3; j++)
                            using (var p = new Pen(lg, 9 * S))
                            {
                                float rr = (40 - j * 12) * S, ax = box.X + 50 * S + j * 12 * S, top = box.Y + 40 * S + j * 10 * S;
                                using (var path = new GraphicsPath()) { path.AddLine(ax, box.Bottom - 6 * S, ax, top + rr); path.AddArc(ax, top, 2 * rr, 2 * rr, 180, 180); path.AddLine(ax + 2 * rr, top + rr, ax + 2 * rr, box.Bottom - 6 * S); g.DrawPath(p, path); }
                            }
                        break;
                    default:   // petals
                        var st = g.Save();
                        for (int d = 0; d < 180; d += 30)
                        {
                            g.Restore(st); st = g.Save();
                            g.TranslateTransform(cx, cy + 16 * S); g.RotateTransform(d);
                            using (var pb = new SolidBrush(Color.FromArgb(a.A / 2, d % 60 == 0 ? a : b))) g.FillEllipse(pb, -16 * S, -48 * S, 32 * S, 96 * S);
                        }
                        g.Restore(st);
                        g.FillEllipse(lg, cx - 14 * S, cy + 2 * S, 28 * S, 28 * S);
                        break;
                }
            }
        }
    }

    // the top-bar button: a globe in a rainbow circle and the language's own name
    sealed class LangButton : Control
    {
        string code = "en", name = "English"; bool hot, sel;
        public bool Selected { get { return sel; } set { if (sel != value) { sel = value; Invalidate(); } } }
        public LangButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand; TabStop = true; AccessibleRole = AccessibleRole.ButtonDropDown;
        }
        public void Set(string c, string n)
        {
            code = c; name = n ?? c; AccessibleName = L.T("Language: ") + name;
            float S = Draw.S;
            Width = (int)(Draw.Measure(name, FullView.LangFont(code, 15.5f * S, true), 0).Width + 72 * S); Height = (int)(38 * S);
            Invalidate();
        }
        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space || e.KeyCode == Keys.Down) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = Draw.WindowText;
            float S = Draw.S; var r = new RectangleF(1.5f, 1.5f, Width - 4, Height - 4);
            using (var b = new SolidBrush(Parent != null ? Parent.BackColor : BackColor)) g.FillRectangle(b, ClientRectangle);
            Color v = ColorTranslator.FromHtml("#7C4DFF"), p = ColorTranslator.FromHtml("#EC4899");
            using (var path = Theme.Round(r, r.Height / 2))
            {
                using (var lg = new LinearGradientBrush(r, Kit.Mix(Theme.Card, v, hot || sel ? 0.26f : 0.16f), Kit.Mix(Theme.Card, p, hot || sel ? 0.24f : 0.14f), 0f)) g.FillPath(lg, path);
                using (var lg = new LinearGradientBrush(r, v, p, 0f)) using (var pen = new Pen(lg, (sel ? 2.5f : 2f) * S)) g.DrawPath(pen, path);
            }
            // the globe in a rainbow circle
            var ic = new RectangleF(6 * S, (Height - 27 * S) / 2, 27 * S, 27 * S);
            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse(ic);
                using (var pg = new PathGradientBrush(gp) { CenterColor = ColorTranslator.FromHtml("#F59E0B"), SurroundColors = new[] { v, p, v, p }, CenterPoint = new PointF(ic.X + ic.Width * 0.7f, ic.Y + ic.Height * 0.75f) }) g.FillPath(pg, gp);
            }
            using (var pen = new Pen(Color.White, 1.4f * S))
            {
                var gr = new RectangleF(ic.X + 6.5f * S, ic.Y + 6.5f * S, 14 * S, 14 * S);
                g.DrawEllipse(pen, gr); g.DrawEllipse(pen, gr.X + 4 * S, gr.Y, 6 * S, 14 * S);
                g.DrawLine(pen, gr.X, gr.Y + 7 * S, gr.Right, gr.Y + 7 * S);
                g.DrawLine(pen, gr.X + 1.5f * S, gr.Y + 3.5f * S, gr.Right - 1.5f * S, gr.Y + 3.5f * S); g.DrawLine(pen, gr.X + 1.5f * S, gr.Y + 10.5f * S, gr.Right - 1.5f * S, gr.Y + 10.5f * S);
            }
            var f = FullView.LangFont(code, 15.5f * S, true);
            var ts = g.MeasureString(name, f);
            using (var nb = new SolidBrush(Theme.Ink)) g.DrawString(name, f, nb, ic.Right + 9 * S, (Height - ts.Height) / 2);
            float cx = Width - 20 * S, cy = Height / 2f + (sel ? -1 : 1) * 1.5f * S, d = 4 * S;
            using (var pen = new Pen(Kit.Mix(v, Theme.Ink, Theme.Dark ? 0.5f : 0.1f), 1.8f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, sel ? new[] { new PointF(cx - d, cy + d / 2), new PointF(cx, cy - d / 2), new PointF(cx + d, cy + d / 2) } : new[] { new PointF(cx - d, cy - d / 2), new PointF(cx, cy + d / 2), new PointF(cx + d, cy - d / 2) });
            if (Focused && ShowFocusCues) Theme.Stroke(g, new RectangleF(1.5f, 1.5f, Width - 3, Height - 3), (Height - 3) / 2f, Theme.BlueDark, 2);
        }
    }
}
