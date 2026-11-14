// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== the top line (Adeeb, 14 Nov: "crowded - simplify")
    // logo · your view · search · theme · buddy · Settings · ⋯ · Back to corner. The ⋯ menu holds Details, the layout,
    // Adeebo points and About; it closes on a click anywhere else, on Esc, or when the window loses focus.
    sealed partial class FullView
    {
        void PlaceTop()
        {
            if (header == null || backBtn == null) return;
            Func<Control, int> my = c => (header.Height - c.Height) / 2;
            int xr = header.Width - Sc(20);
            xr -= backBtn.Width; backBtn.Location = new Point(xr, my(backBtn)); xr -= Sc(12);
            xr -= dotsBtn.Width; dotsBtn.Location = new Point(xr, my(dotsBtn)); xr -= Sc(8);
            xr -= gearBtn.Width; gearBtn.Location = new Point(xr, my(gearBtn)); xr -= Sc(8);
            xr -= Sc(30) + Sc(12);   // the buddy's face, painted
            xr -= themeSw.Width; themeSw.Location = new Point(xr, my(themeSw)); xr -= Sc(18);
            int xl = Sc(18) + Sc(40) + (int)Math.Ceiling(Draw.Measure(L.T("AdeeboMeter"), Draw.fBig, 0).Width) + Sc(16);
            viewBtn.Location = new Point(xl, my(viewBtn)); xl = viewBtn.Right + Sc(20);
            int room = xr - xl;
            bool show = Cfg.Layout != "sidebar" && room >= Sc(40);   // the sidebar has its own search at the top
            if (headSearch.Visible != show) headSearch.Visible = show;
            int sw = room >= Sc(220) ? Math.Min(Sc(300), room) : Sc(36);
            headSearch.Size = new Size(sw, Sc(36)); headSearch.Location = new Point(xr - sw, (header.Height - headSearch.Height) / 2);
            header.Invalidate();
        }
        void SetDetails(bool on)
        {
            Cfg.Details = on; if (detailsBtn.On != on) detailsBtn.On = on; Cfg.Save();
            if (pathPanel != null) pathPanel.Height = PathHeight;
            Refresh(); foreach (Control c in Descendants(this)) c.Invalidate();
        }

        // ---------- the ⋯ menu: a small card under the button
        DB topMenu; TopMenuFilter topFilter;
        void TopMenu()
        {
            if (topMenu != null && topMenu.Visible) { HideTopMenu(); return; }
            if (topMenu == null)
            {
                topMenu = new DB { Name = "topMenu", BackColor = Theme.Bg, Visible = false };
                topMenu.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintTopMenu(e.Graphics); }; Wire(topMenu);
                Controls.Add(topMenu);
                Deactivate += delegate { HideTopMenu(); };
            }
            int w = Sc(290), h = Sc(Cfg.Points ? 216 : 178);
            var at = PointToClient(dotsBtn.Parent.PointToScreen(new Point(dotsBtn.Right, dotsBtn.Bottom)));
            topMenu.Bounds = new Rectangle(Math.Max(Sc(8), at.X - w + Sc(6)), at.Y + Sc(6), w, h);
            topMenu.BackColor = header.BackColor;
            topMenu.Visible = true; topMenu.BringToFront(); topMenu.Invalidate();
            dotsBtn.Selected = true;
            if (topFilter == null) topFilter = new TopMenuFilter(this);
            Application.AddMessageFilter(topFilter);
        }
        void HideTopMenu()
        {
            if (topMenu == null || !topMenu.Visible) return;
            topMenu.Visible = false; dotsBtn.Selected = tab == "about" || tab == "points";
            if (topFilter != null) Application.RemoveMessageFilter(topFilter);
        }
        // a click outside the menu (and not on its own button) closes it
        sealed class TopMenuFilter : IMessageFilter
        {
            readonly FullView v; public TopMenuFilter(FullView v) { this.v = v; }
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x201 && m.Msg != 0x204 && m.Msg != 0xA1) return false;   // left / right button down, title bar
                try
                {
                    if (v.IsDisposed || v.topMenu == null || !v.topMenu.Visible) return false;
                    var p = Control.MousePosition;
                    if (v.topMenu.RectangleToScreen(v.topMenu.ClientRectangle).Contains(p) || v.dotsBtn.RectangleToScreen(v.dotsBtn.ClientRectangle).Contains(p)) return false;
                    v.BeginInvoke((Action)v.HideTopMenu);
                }
                catch (InvalidOperationException) { }
                return false;
            }
        }
        void PaintTopMenu(Graphics g)
        {
            Begin(topMenu);
            var card = new RectangleF(1, 1, topMenu.Width - 3, topMenu.Height - 3);
            Theme.Fill(g, card, 14 * S, Theme.Card); Theme.Stroke(g, card, 14 * S, Theme.Line, 1);
            float x = 10 * S, y = 10 * S, w = card.Width - 20 * S, rh = 38 * S;
            Action<string, string, bool?, Action> row = (t, right, sw, a) =>
            {
                var r = new RectangleF(x, y, w, rh);
                if (r.Contains(topMenu.PointToClient(Control.MousePosition))) Theme.Fill(g, r, 9 * S, Theme.Soft);
                using (var b = new SolidBrush(Theme.Ink)) g.DrawString(t, Draw.fBodyB, b, r.X + 12 * S, r.Y + (rh - g.MeasureString(t, Draw.fBodyB).Height) / 2);
                if (sw.HasValue)
                {
                    var k = new RectangleF(r.Right - 46 * S, r.Y + (rh - 18 * S) / 2, 34 * S, 18 * S);
                    Theme.Fill(g, k, 9 * S, sw.Value ? Theme.Blue : Theme.Track);
                    using (var kb = new SolidBrush(Color.White)) g.FillEllipse(kb, sw.Value ? k.Right - 16 * S : k.X + 2 * S, k.Y + 2 * S, 14 * S, 14 * S);
                }
                else if (right != null) { var rs = g.MeasureString(right, Draw.fSmall); using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(right, Draw.fSmall, mb, r.Right - rs.Width - 12 * S, r.Y + (rh - rs.Height) / 2); }
                Act(topMenu, r, a); y += rh;
            };
            row(L.T("Details"), null, Cfg.Details, delegate { SetDetails(!Cfg.Details); topMenu.Invalidate(); });
            row(L.T("Sidebar layout"), null, Cfg.Layout == "sidebar", delegate { HideTopMenu(); SetLayout(Cfg.Layout == "sidebar" ? "classic" : "sidebar"); PlaceTop(); });
            using (var p = new Pen(Theme.Line2)) g.DrawLine(p, x + 6 * S, y + 6 * S, x + w - 6 * S, y + 6 * S); y += 12 * S;
            if (Cfg.Points) row(L.T("Adeebo points"), Points.Me.Total.ToString(CultureInfo.CurrentCulture) + " · " + L.T(Points.Rank(Points.Me.Total)), null, delegate { HideTopMenu(); tab = "points"; ShowTab(); });
            row(L.T("About · Report a bug"), null, null, delegate { HideTopMenu(); tab = "about"; ShowTab(); });
            using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T("Esc closes this menu"), Draw.fTiny, m, x + 12 * S, y + 8 * S);
        }
    }
}
