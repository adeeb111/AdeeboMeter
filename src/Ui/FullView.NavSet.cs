// Settings > Navigation (PLAN 11.11b): the tabs on your bar in order (up, down, into More), what's in More (onto the bar),
// your own tabs (new, rename, delete) and Reset. Home stays first; at most 7 on the bar; nothing built in is renamed.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    partial class FullView
    {
        const string NavDefault = "home,toolbox,gamer,time,learn";

        float PaintNavSettings(Graphics g, float x, float y, float w)
        {
            var bar = Cfg.NavClean(Cfg.Nav).Split(',').ToList();
            var mine = MyTabs();
            var more = Cfg.NavIds.Where(id => !bar.Contains(id)).Concat(mine.Select(t => t.Id).Where(id => !bar.Contains(id))).ToList();
            float rw = Math.Min(w, 560 * S), rh = 44 * S;
            Action<List<string>> save = l => { Cfg.Nav = Cfg.NavClean(string.Join(",", l)); Cfg.Save(); ApplyLayout(); settingsCanvas.Invalidate(); };
            // a small button on a row; returns its left edge
            Func<float, float, string, string, bool, Action, float> btn = (right, top, icon, text, on, go) =>
            {
                string t = L.T(text); var ts = g.MeasureString(t, Draw.fSmall);
                var r = new RectangleF(right - ts.Width - (icon != null ? 34 : 18) * S, top + 8 * S, ts.Width + (icon != null ? 34 : 18) * S, 28 * S);
                Theme.Fill(g, r, 8 * S, on ? Theme.Soft : Theme.Card); Theme.Stroke(g, r, 8 * S, Theme.Line, 1);
                if (icon != null) Kit.Icon(g, icon, new RectangleF(r.X + 9 * S, r.Y + 7 * S, 14 * S, 14 * S), on ? Theme.Sub : Theme.Line);
                using (var b = new SolidBrush(on ? Theme.Ink : Theme.Muted)) g.DrawString(t, Draw.fSmall, b, r.X + (icon != null ? 27 : 9) * S, r.Y + (r.Height - ts.Height) / 2);
                if (on) Act(settingsCanvas, r, go);
                return r.X - 6 * S;
            };
            Action<string, float> rowFace = (id, top) =>
            {
                var r = new RectangleF(x, top, rw, rh - 6 * S); Theme.Fill(g, r, 10 * S, Theme.Soft);
                string acc = NavAccent(id); using (var cb = new SolidBrush(Kit.Soft(acc))) g.FillEllipse(cb, r.X + 10 * S, r.Y + 6 * S, 26 * S, 26 * S);
                Kit.Icon(g, NavIcon(id), new RectangleF(r.X + 16 * S, r.Y + 12 * S, 14 * S, 14 * S), Kit.Accent(acc));
                using (var b = new SolidBrush(Theme.Ink)) g.DrawString(L.T(NavName(id)), Draw.fBodyB, b, r.X + 46 * S, r.Y + 9 * S);
            };
            using (var muted = new SolidBrush(Theme.Muted)) using (var ink = new SolidBrush(Theme.Ink))
            {
                g.DrawString(L.T("ON YOUR BAR") + "  ·  " + bar.Count + " / 7", Draw.fTiny, muted, x, y); y += 22 * S;
                for (int i = 0; i < bar.Count; i++)
                {
                    string id = bar[i]; int k = i; rowFace(id, y); float right = x + rw - 8 * S;
                    if (id != "home")
                    {
                        right = btn(right, y - 4 * S, null, "To More", true, delegate { var l = bar.ToList(); l.Remove(id); save(l); });
                        right = btn(right, y - 4 * S, null, "Down", k < bar.Count - 1, delegate { var l = bar.ToList(); l.RemoveAt(k); l.Insert(k + 1, id); save(l); });
                        btn(right, y - 4 * S, null, "Up", k > 1, delegate { var l = bar.ToList(); l.RemoveAt(k); l.Insert(k - 1, id); save(l); });
                    }
                    else g.DrawString(L.T("always first"), Draw.fSmall, muted, right - g.MeasureString(L.T("always first"), Draw.fSmall).Width, y + 12 * S);
                    y += rh;
                }
                y += 10 * S;
                g.DrawString(L.T("IN MORE"), Draw.fTiny, muted, x, y); y += 22 * S;
                if (more.Count == 0) { g.DrawString(L.T("Everything is on the bar."), Draw.fSmall, muted, x, y); y += 26 * S; }
                foreach (var id0 in more)
                {
                    string id = id0; rowFace(id, y);
                    btn(x + rw - 8 * S, y - 4 * S, "pin", bar.Count < 7 ? "To the bar" : "Bar is full", bar.Count < 7, delegate { var l = bar.ToList(); l.Add(id); save(l); });
                    y += rh;
                }
                y += 10 * S;
                g.DrawString(L.T("YOUR OWN TABS") + "  ·  " + mine.Count + " / 3", Draw.fTiny, muted, x, y); y += 22 * S;
                foreach (var t0 in mine)
                {
                    var t = t0; rowFace(t.Id, y);
                    string cnt = t.Items.Count + " " + L.T(t.Items.Count == 1 ? "item" : "items"); var cs = g.MeasureString(cnt, Draw.fSmall);
                    float right = x + rw - 8 * S;
                    right = btn(right, y - 4 * S, null, "Delete", true, delegate
                    {
                        if (t.Items.Count > 0 && MessageBox.Show(this, L.T("Delete this tab and its pins?") + "\n\n" + t.Name, "AdeeboMeter", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                        var l = MyTabs().Where(z => z.Id != t.Id).ToList(); SaveMyTabs(l);
                        Cfg.Nav = NavAfterDelete(Cfg.Nav, t.Id); Cfg.Save(); ApplyLayout(); settingsCanvas.Invalidate();
                    });
                    right = btn(right, y - 4 * S, null, "Rename", true, delegate
                    {
                        string nm = Ask(L.T("Rename tab"), t.Name); if (nm == null) return;
                        var l = MyTabs(); var tt = l.FirstOrDefault(z => z.Id == t.Id); if (tt != null) { tt.Name = nm; SaveMyTabs(l); ApplyLayout(); settingsCanvas.Invalidate(); }
                    });
                    g.DrawString(cnt, Draw.fSmall, muted, right - cs.Width - 4 * S, y + 12 * S);
                    y += rh;
                }
                if (mine.Count < 3)
                {
                    var nr = new RectangleF(x, y, 150 * S, 34 * S); Theme.Fill(g, nr, 10 * S, Kit.Soft("mine"));
                    Kit.Icon(g, "plus", new RectangleF(nr.X + 12 * S, nr.Y + 10 * S, 14 * S, 14 * S), Kit.Accent("mine"));
                    using (var b = new SolidBrush(Kit.Ink("mine"))) g.DrawString(L.T("New tab"), Draw.fBtn, b, nr.X + 34 * S, nr.Y + 9 * S);
                    Act(settingsCanvas, nr, delegate
                    {
                        string nm = Ask(L.T("Name your tab"), L.T("Work")); if (nm == null) return;
                        var l = MyTabs(); l.Add(new MineTab { Name = nm }); SaveMyTabs(l);
                        var ids = MyTabs(); string nid = ids.Count > 0 ? ids[ids.Count - 1].Id : null;
                        if (nid != null) { var b2 = Cfg.NavClean(Cfg.Nav).Split(',').ToList(); if (b2.Count < 7) { b2.Add(nid); Cfg.Nav = Cfg.NavClean(string.Join(",", b2)); Cfg.Save(); } }
                        ApplyLayout(); settingsCanvas.Invalidate();
                    });
                    y += 46 * S;
                }
                g.DrawString(L.T("Pin tools, pages, sites and settings to a tab from a tool page (Pin) or a search result (Ctrl+P)."), Draw.fSmall, muted, new RectangleF(x, y, rw, 40 * S)); y += 34 * S;
                var rr = new RectangleF(x, y, 170 * S, 30 * S);
                using (var b = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(L.T("Reset to default"), Draw.fBodyB, b, rr.X, rr.Y + 4 * S);
                Act(settingsCanvas, rr, delegate { Cfg.Nav = NavDefault; Cfg.Save(); ApplyLayout(); settingsCanvas.Invalidate(); });
                y += 44 * S;
            }
            return y;
        }

        // own tabs are numbered by position: deleting mine2 makes mine3 the new mine2 - the bar follows
        internal static string NavAfterDelete(string nav, string gone)
        {
            int k = gone.Length == 5 ? gone[4] - '0' : 0; var l = new List<string>();
            foreach (var id in Cfg.NavClean(nav).Split(','))
            {
                if (!id.StartsWith("mine", StringComparison.Ordinal) || id.Length != 5) { l.Add(id); continue; }
                int n = id[4] - '0'; if (n == k) continue;
                l.Add(n > k ? "mine" + (char)('0' + n - 1) : id);
            }
            return Cfg.NavClean(string.Join(",", l));
        }
        // a one-line question box: the text, or null when cancelled. 1-24 characters, no | = ;
        string Ask(string title, string start)
        {
            using (var f = new Form { Text = "AdeeboMeter", FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Card, ClientSize = new Size(Sc(360), Sc(130)) })
            {
                var lb = new Label { Text = title, Font = Draw.fBodyB, ForeColor = Theme.Ink, Location = new Point(Sc(16), Sc(14)), AutoSize = true };
                var tb = new TextBox { Text = start ?? "", Font = Draw.fInput, MaxLength = 24, Location = new Point(Sc(16), Sc(44)), Width = Sc(328) };
                var ok = new PillButton("OK", PillButton.K.Primary) { Location = new Point(Sc(16), Sc(84)) }; ok.Click += delegate { f.DialogResult = DialogResult.OK; };
                var no = new PillButton("Cancel", PillButton.K.Chip) { Location = new Point(Sc(110), Sc(86)) }; no.Click += delegate { f.DialogResult = DialogResult.Cancel; };
                f.Controls.Add(lb); f.Controls.Add(tb); f.Controls.Add(ok); f.Controls.Add(no);
                tb.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; f.DialogResult = DialogResult.OK; } else if (e.KeyCode == Keys.Escape) f.DialogResult = DialogResult.Cancel; };
                if (f.ShowDialog(this) != DialogResult.OK) return null;
                string v = new string(tb.Text.Where(c => c != '|' && c != '=' && c != ';' && !char.IsControl(c)).ToArray()).Trim();
                return v.Length == 0 ? null : v.Length > 24 ? v.Substring(0, 24) : v;
            }
        }
    }
}
