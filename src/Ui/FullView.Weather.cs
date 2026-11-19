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

        // returns the picked place, or null (cancelled, nothing found, offline)
        Weather.Place PickPlace()
        {
            using (var f = new Form { Text = AdeeboMeter.L.T("AdeeboMeter - ") + L.T("Weather for"), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Card, ClientSize = new Size(Sc(420), Sc(330)) })
            {
                var lb = new Label { Text = L.T("A city, or a country - e.g. Ottawa, Dhaka, or Ottawa, Illinois"), Font = Draw.fBodyB, ForeColor = Theme.Ink, Location = new Point(Sc(16), Sc(14)), AutoSize = true };
                var tb = new TextBox { Font = Draw.fInput, MaxLength = 60, Location = new Point(Sc(16), Sc(42)), Width = Sc(290) };
                var find = new PillButton(L.T("Find"), PillButton.K.Primary) { Location = new Point(Sc(318), Sc(40)) };
                var list = new ListBox { Font = Draw.fBody, Location = new Point(Sc(16), Sc(84)), Size = new Size(Sc(388), Sc(150)), BackColor = Theme.Card, ForeColor = Theme.Ink, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false };
                var note = new Label { Text = L.T("Only the name you type goes to Open-Meteo (free, no account) to find it - then only its coordinates."), Font = Draw.fSmall, ForeColor = Theme.Sub, Location = new Point(Sc(16), Sc(240)), Size = new Size(Sc(388), Sc(34)) };
                var ok = new PillButton(L.T("Use this place"), PillButton.K.Primary) { Location = new Point(Sc(16), Sc(282)), Enabled = false };
                var no = new PillButton(L.T("Cancel"), PillButton.K.Chip) { Location = new Point(Sc(160), Sc(284)) };
                var found = new List<Weather.Place>(); Weather.Place picked = null;
                Action go = delegate
                {
                    string q = tb.Text.Trim(); if (q.Length < 2 || !find.Enabled) return;   // AM-CG-166: one search at a time
                    list.Items.Clear(); list.Items.Add(L.T("Looking...")); find.Enabled = false; ok.Enabled = false;
                    Weather.Find(q, r =>
                    {
                        try
                        {
                            if (f.IsDisposed) return;
                            f.BeginInvoke((Action)delegate
                            {
                                if (f.IsDisposed) return;
                                find.Enabled = true; list.Items.Clear(); found = r ?? new List<Weather.Place>();
                                if (r == null) list.Items.Add(L.T(Weather.Note));
                                else if (r.Count == 0) list.Items.Add(L.T("Nothing found - check the spelling, or try the nearest bigger city."));
                                else { foreach (var p in r) list.Items.Add(p.Name); list.SelectedIndex = 0; }
                            });
                        }
                        catch (InvalidOperationException) { }
                    });
                };
                find.Click += delegate { go(); };
                tb.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; go(); } else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; f.DialogResult = DialogResult.Cancel; } };
                list.SelectedIndexChanged += delegate { ok.Enabled = list.SelectedIndex >= 0 && list.SelectedIndex < found.Count; };
                list.DoubleClick += delegate { if (ok.Enabled) f.DialogResult = DialogResult.OK; };
                ok.Click += delegate { if (ok.Enabled) f.DialogResult = DialogResult.OK; };
                no.Click += delegate { f.DialogResult = DialogResult.Cancel; };
                // AM-CG-167: laid out from what each piece needs (long translations, 150-200 % scaling) - nothing fixed-height
                int cw = Math.Max(Sc(388), find.Width + Sc(290) + Sc(12));
                lb.MaximumSize = new Size(cw, 0); note.AutoSize = true; note.MaximumSize = new Size(cw, 0);
                f.Controls.AddRange(new Control[] { lb, tb, find, list, note, ok, no });
                tb.Top = lb.Bottom + Sc(10); find.Location = new Point(tb.Right + Sc(12), tb.Top - Sc(2)); list.Top = Math.Max(tb.Bottom, find.Bottom) + Sc(10); list.Width = cw;
                note.Top = list.Bottom + Sc(6); ok.Top = note.Bottom + Sc(12); no.Location = new Point(ok.Right + Sc(12), ok.Top + Sc(2));
                f.ClientSize = new Size(cw + Sc(32), Math.Max(ok.Bottom, no.Bottom) + Sc(16));
                var cur = Weather.Current; if (cur != null) tb.Text = cur.Name.Split(',')[0];
                if (f.ShowDialog(this) != DialogResult.OK) return null;
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
            if (!Cfg.WeatherOn || p == null) return "Off. When on: the buddy shows hot, cold, rain, snow or a storm for 2-3 minutes a few times an hour, and the widget can show one line. You type the place - your PC's location is never used.";
            return "On for " + p.Name + ".\n" + Weather.Status() + "\nOnly its coordinates go to Open-Meteo (free, no account). Kept on this PC: the coming days and the last one - no history.";
        }
    }
}
