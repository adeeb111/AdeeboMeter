// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Settings > Weather: pick the place by name (Weather.cs). The matches are listed so "Ottawa" in Ontario and in Illinois
    // can't be mixed up; the one you pick is saved and shown under the switch.
    sealed partial class FullView
    {
        static readonly string[] SetWeatherFor = { "Change place...", "Remove" };
        static readonly string[] SetWeatherAcc = { "Normal", "Better" };
        static readonly string[] SetWeatherUnit = { "°C", "°F" };

        // returns the picked place, or null (cancelled, nothing found, offline). 24 Nov: in the app's own look - a rounded search
        // box, a list of places with a pin and the region under each name, the reason in words when the search fails.
        Weather.Place PickPlace()
        {
            using (var f = new Form { Text = L.T("Weather for"), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Card, Font = Draw.fBody, KeyPreview = true })
            {
                int cw = Sc(420);
                var title = new Label { Text = L.T("Where should the weather be for?"), Font = Draw.fTitle, ForeColor = Theme.Ink, Location = new Point(Sc(20), Sc(18)), AutoSize = true, MaximumSize = new Size(cw, 0), BackColor = Theme.Card };
                var hint = new Label { Text = L.T("A city, or a country - e.g. Ottawa, Dhaka, or Ottawa, Illinois"), Font = Draw.fSmall, ForeColor = Theme.Sub, AutoSize = true, MaximumSize = new Size(cw, 0), BackColor = Theme.Card };
                var find = new PillButton(L.T("Find"), PillButton.K.Primary);
                var box = new RoundBox(cw - find.Width - Sc(10), Draw.fInput, L.T("Type a place"));
                var found = new List<Weather.Place>(); string status = ""; int hot = -1;
                var list = new ListBox { DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Sc(46), Size = new Size(cw, Sc(46) * 5 + 2), BorderStyle = BorderStyle.None, BackColor = Theme.Card, IntegralHeight = false };
                var note = new Label { Text = WeatherPrivacy(), Font = Draw.fSmall, ForeColor = Theme.Muted, AutoSize = true, MaximumSize = new Size(cw, 0), BackColor = Theme.Card };
                // the servers: Open-Meteo filled in; any host that answers the same API can replace it
                var geoBox = new RoundBox(cw, Draw.fBody, Weather.GeoDefault) { Label = L.T("Place search server") };
                var apiBox = new RoundBox(cw, Draw.fBody, Weather.ApiDefault) { Label = L.T("Forecast server") };
                geoBox.Box.Text = Weather.GeoHost; apiBox.Box.Text = Weather.ApiHost;
                geoBox.Box.AccessibleName = L.T("Place search server"); apiBox.Box.AccessibleName = L.T("Forecast server");   // review L-11: the field's name, not the host
                // review 24 Nov (M-2): the servers apply for this search, Cancel puts back what was there, a name that isn't a
                // host says so instead of quietly turning back into Open-Meteo, and the privacy line names the real hosts
                string geo0 = Cfg.WeatherGeo, api0 = Cfg.WeatherApi;
                Func<string> keepServers = delegate
                {
                    string gt = geoBox.Box.Text.Trim(), at = apiBox.Box.Text.Trim();
                    string gv = Weather.CleanServer(gt), av = Weather.CleanServer(at);
                    if ((gt != "" && gv == "") || (at != "" && av == "")) return L.T("That server isn't a host name - use one like api.example.com, or host:port.");
                    Cfg.WeatherGeo = gv == Weather.GeoDefault ? "" : gv; Cfg.WeatherApi = av == Weather.ApiDefault ? "" : av;
                    note.Text = WeatherPrivacy(); return "";
                };
                var ok = new PillButton(L.T("Use this place"), PillButton.K.Primary) { Enabled = false };
                var no = new PillButton(L.T("Cancel"), PillButton.K.Chip);
                Weather.Place picked = null;
                // the list draws itself: empty / looking / failed as one calm line, places as rows with a pin
                list.DrawItem += delegate(object s, DrawItemEventArgs e)
                {
                    if (e.Index < 0) return; var g = e.Graphics; Prep(g); var r = e.Bounds;
                    using (var bg = new SolidBrush(Theme.Card)) g.FillRectangle(bg, r);
                    if (e.Index >= found.Count) return;
                    bool sel = (e.State & DrawItemState.Selected) != 0, h = e.Index == hot;
                    var rr = new RectangleF(r.X + 1, r.Y + 2, r.Width - 2, r.Height - 4);
                    if (sel) Theme.Fill(g, rr, 10 * Draw.S, Theme.BlueSoft); else if (h) Theme.Fill(g, rr, 10 * Draw.S, Theme.Soft);
                    Kit.Icon(g, "dest", new RectangleF(r.X + Sc(12), r.Y + (r.Height - Sc(18)) / 2f, Sc(18), Sc(18)), sel ? Theme.BlueText : Theme.Muted);
                    var parts = found[e.Index].Name.Split(new[] { ", " }, 2, StringSplitOptions.None);
                    using (var ib = new SolidBrush(Theme.Ink)) using (var sb = new SolidBrush(Theme.Sub)) using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    {
                        float tx = r.X + Sc(40), tw = r.Width - Sc(52);
                        g.DrawString(parts[0], Draw.fBodyB, ib, new RectangleF(tx, r.Y + Sc(5), tw, Sc(20)), nf);
                        if (parts.Length > 1) g.DrawString(parts[1], Draw.fSmall, sb, new RectangleF(tx, r.Y + Sc(24), tw, Sc(18)), nf);
                    }
                };
                // the state line under the box (looking, nothing found, why it failed)
                var state = new Label { Font = Draw.fBody, ForeColor = Theme.Sub, AutoSize = true, MaximumSize = new Size(cw, 0), BackColor = Theme.Card };
                Action<string, bool> say = (t, bad) => { status = t ?? ""; state.Text = status; state.ForeColor = bad ? Theme.LvText(2) : Theme.Sub; state.Visible = status != ""; list.Visible = status == ""; };
                Action go = delegate
                {
                    string q = box.Box.Text.Trim(); if (q.Length < 2 || !find.Enabled) return;   // AM-CG-166: one search at a time
                    { string bad = keepServers(); if (bad != "") { say(bad, true); return; } }
                    found.Clear(); list.Items.Clear(); say(L.T("Looking..."), false); find.Enabled = false; ok.Enabled = false;
                    Weather.Find(q, r =>
                    {
                        try
                        {
                            if (f.IsDisposed) return;
                            f.BeginInvoke((Action)delegate
                            {
                                if (f.IsDisposed) return;
                                find.Enabled = true; found = r ?? new List<Weather.Place>();
                                if (r == null) say(L.T(Weather.Note), true);
                                else if (r.Count == 0) say(L.T("Nothing found - check the spelling, or try the nearest bigger city."), false);
                                else { say("", false); foreach (var p in r) list.Items.Add(p.Name); list.SelectedIndex = 0; }
                            });
                        }
                        catch (InvalidOperationException) { }
                    });
                };
                find.Click += delegate { go(); };
                KeyEventHandler enterFinds = delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; go(); } };
                geoBox.Box.KeyDown += enterFinds; apiBox.Box.KeyDown += enterFinds;   // review L-11: Enter searches from the server boxes too
                box.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; go(); } else if (e.KeyCode == Keys.Down && list.Items.Count > 0) { e.SuppressKeyPress = true; list.Focus(); } };
                f.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; f.DialogResult = DialogResult.Cancel; } };
                list.MouseMove += delegate(object s, MouseEventArgs e) { int i = list.IndexFromPoint(e.Location); if (i != hot) { hot = i; list.Invalidate(); } };
                list.MouseLeave += delegate { if (hot != -1) { hot = -1; list.Invalidate(); } };
                list.SelectedIndexChanged += delegate { ok.Enabled = list.SelectedIndex >= 0 && list.SelectedIndex < found.Count; list.Invalidate(); };
                /* OK checks the servers first: a bad one is said, not silently dropped (review 24 Nov L-4) */
                Action accept = delegate { if (!ok.Enabled) return; string bad = keepServers(); if (bad != "") { say(bad, true); return; } f.DialogResult = DialogResult.OK; };
                list.DoubleClick += delegate { accept(); };
                list.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && ok.Enabled) { e.SuppressKeyPress = true; accept(); } };
                ok.Click += delegate { accept(); };
                no.Click += delegate { f.DialogResult = DialogResult.Cancel; };
                // AM-CG-167: laid out from what each piece needs (long translations, 150-200 % scaling) - nothing fixed-height
                f.Controls.AddRange(new Control[] { title, hint, box, find, state, list, note, geoBox, apiBox, ok, no });
                hint.Location = new Point(Sc(20), title.Bottom + Sc(4));
                box.Location = new Point(Sc(20), hint.Bottom + Sc(12)); find.Location = new Point(box.Right + Sc(10), box.Top + (box.Height - find.Height) / 2);
                state.Location = new Point(Sc(20), box.Bottom + Sc(14)); list.Location = new Point(Sc(20), box.Bottom + Sc(10));
                note.Location = new Point(Sc(20), list.Bottom + Sc(10));
                Action lowerPart = delegate   // again when the privacy line changes length (another server named in it)
                {
                    geoBox.Location = new Point(Sc(20), note.Bottom + Sc(14)); apiBox.Location = new Point(Sc(20), geoBox.Bottom + Sc(12));   /* 24 Nov: one under the other - side by side cut the host names off */
                    int by = Math.Max(geoBox.Bottom, apiBox.Bottom) + Sc(18); ok.Location = new Point(Sc(20) + cw - ok.Width, by); no.Location = new Point(ok.Left - no.Width - Sc(10), by + (ok.Height - no.Height) / 2);
                    f.ClientSize = new Size(cw + Sc(40), ok.Bottom + Sc(18));
                };
                Draw.FitOnShow(f, true);   /* hunters11 core ML-2 */ lowerPart(); note.SizeChanged += delegate { lowerPart(); };
                say(L.T("Type a place and press Enter."), false);
                var cur = Weather.Current; if (cur != null) box.Box.Text = cur.Name.Split(',')[0];
                f.Shown += delegate { box.Box.Focus(); box.Box.SelectAll(); };
                Kit.Polish(f); if (f.ShowDialog(this) != DialogResult.OK) { Cfg.WeatherGeo = geo0; Cfg.WeatherApi = api0; return null; }   // Cancel: the servers go back too
                if (keepServers() != "") { Cfg.WeatherGeo = geo0; Cfg.WeatherApi = api0; }
                Cfg.Save();
                if (list.SelectedIndex >= 0 && list.SelectedIndex < found.Count) picked = found[list.SelectedIndex];
                return picked;
            }
        }
        // the switch: turning it on asks for a place first if there isn't one
        void SetWeather(bool on)
        {
            if (on && Weather.Current == null) { var p = PickPlace(); if (p == null) return; Cfg.WeatherPlace = Weather.Line(p); }
            Cfg.WeatherOn = on; if (!on) Weather.Forget(); Cfg.Save(); if (on) Weather.Fetch(); Invalidate(true);
        }
        void ChangePlace(int i)
        {
            if (i == 1) { Cfg.WeatherPlace = ""; Cfg.WeatherOn = false; Weather.Forget(); Cfg.Save(); Invalidate(true); return; }
            var p = PickPlace(); if (p == null) return;
            Cfg.WeatherPlace = Weather.Line(p); Weather.Forget(); Cfg.Save(); Weather.Fetch(); Invalidate(true);
        }
        static string WeatherHelp()
        {
            if (Policy.NoOnline) return "Off - your IT team turned online lookups off (AdeeboMeter.policy).";
            var p = Weather.Current;
            if (!Cfg.WeatherOn || p == null) return "Off. When on, with Weather in the widget on too: the buddy shows hot, cold, rain, snow or a storm for 2-3 minutes a few times an hour, and the widget shows one line. You type the place - your PC's location is never used.";
            return "On for " + p.Name + ".\n" + Weather.Status() + "\nOnly its coordinates go to " + (Weather.ApiHost == Weather.ApiDefault ? "Open-Meteo (free, no account)" : Weather.ApiHost) + ". Kept on this PC: the coming days and the last one - no history.";
        }
        // what leaves the PC, naming the servers actually set
        static string WeatherPrivacy()
        {
            if (Weather.GeoHost == Weather.GeoDefault && Weather.ApiHost == Weather.ApiDefault) return L.T("Only the name you type goes to Open-Meteo (free, no account) to find it - then only its coordinates.");
            return string.Format(L.T("Only the name you type goes to {0} to find it - then only its coordinates, to {1}."), Weather.GeoHost, Weather.ApiHost);
        }
    }
}
