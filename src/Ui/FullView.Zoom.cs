// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== zoom and the right-click menu (Adeeb, 14 Nov)
    // Zoom: 80-175 %, Ctrl + / Ctrl - / Ctrl 0, Ctrl + mouse wheel, or the right-click menu. The window is built again at
    // the new size on the same page (text stays sharp - it's drawn at that size, not stretched). The corner widget keeps
    // its own size. Right-click anywhere on a page: zoom, search, refresh, a picture of the window, report, settings.
    sealed partial class FullView
    {
        public static Action ReopenAsked; public static string StartTab;
        internal static readonly int[] Zooms = { 80, 90, 100, 110, 125, 150, 175 };
        void ZoomBy(int dir)
        {
            // bug-hunt L-178: a size between the steps (120 from an imported file) moves to the next step in that direction, not to 100
            int z = Cfg.Zoom, i = Array.IndexOf(Zooms, z);
            if (i >= 0) ZoomTo(Zooms[Math.Max(0, Math.Min(Zooms.Length - 1, i + dir))]);
            else ZoomTo(dir > 0 ? Zooms.Where(s => s > z).DefaultIfEmpty(Zooms.Last()).First() : Zooms.Where(s => s < z).DefaultIfEmpty(Zooms[0]).Last());
        }
        void ZoomTo(int z)
        {
            if (z == Cfg.Zoom || ReopenAsked == null) return;
            Cfg.Zoom = z; Cfg.Save(); StartTab = tab;
            Program.Log("zoom " + z + "%");
            var a = ReopenAsked; BeginInvoke(a);   // after this click is done: the window closes and comes back at the new size
        }
        bool IsPopup(Control c) { return c == viewSheet || c == topMenu || c == buddySheet || c == pageMenu; }

        // ---------- the right-click menu: a small card at the mouse
        DB pageMenu; PopFilter pageFilter; readonly List<string[]> pageItems = new List<string[]>(); readonly List<Action> pageActs = new List<Action>();
        internal void TouchZoom(int d) { ZoomBy(d); }   // a pinch (Touch.cs)
        void PageMenu(Control on, Point at)
        {
            pageItems.Clear(); pageActs.Clear();
            Action<string, string, Action> add = (t, k, a) => { pageItems.Add(new[] { t, k }); pageActs.Add(a); };
            add(L.T("Zoom in"), "Ctrl +", () => ZoomBy(1));
            add(L.T("Zoom out"), "Ctrl -", () => ZoomBy(-1));
            add(L.T("Actual size") + (Cfg.Zoom != 100 ? "  (" + L.T("now") + " " + Cfg.Zoom + "%)" : ""), "Ctrl 0", () => ZoomTo(100));   // bug-hunt L-177: "now 125%" - the size you're at, not what this item gives
            add("-", "", null);
            add(L.T("Search"), "Ctrl K", OpenPalette);
            add(L.T("Refresh readings"), "", () => { if (M != null) M.Nudge(); RefreshConns(true); Invalidate(true); });
            add(L.T("Save a picture of this window"), "", SaveWindowPicture);
            add("-", "", null);
            add(L.T("Report a bug"), "", () => ReportDialog("bug", ""));
            add(L.T("Settings"), "", () => { tab = "settings"; ShowTab(); });
            if (pageMenu == null)
            {
                pageMenu = new DB { Name = "pageMenu", Visible = false };
                pageMenu.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintPageMenu(e.Graphics); }; Wire(pageMenu);
                pageMenu.MouseMove += delegate { pageMenu.Invalidate(); };
                Controls.Add(pageMenu); Deactivate += delegate { HidePageMenu(); };
            }
            int w = Sc(270), h = Sc(16); foreach (var it in pageItems) h += it[0] == "-" ? Sc(11) : Sc(34);
            var p = PointToClient(on.PointToScreen(at));
            pageMenu.Bounds = new Rectangle(Math.Max(0, Math.Min(p.X, ClientSize.Width - w - Sc(6))), Math.Max(0, Math.Min(p.Y, ClientSize.Height - h - Sc(6))), w, h);
            pageMenu.BackColor = header.BackColor; pageMenu.Visible = true; pageMenu.BringToFront(); pageMenu.Focus(); pageMenu.Invalidate();   // hunters10 M-3
            if (pageFilter == null) pageFilter = new PopFilter(this, () => pageMenu, () => Rectangle.Empty, HidePageMenu);
            Application.AddMessageFilter(pageFilter);
        }
        void HidePageMenu() { if (pageMenu == null || !pageMenu.Visible) return; pageMenu.Visible = false; if (pageFilter != null) Application.RemoveMessageFilter(pageFilter); }
        void PaintPageMenu(Graphics g)
        {
            Begin(pageMenu);
            var card = new RectangleF(1, 1, pageMenu.Width - 3, pageMenu.Height - 3);
            Theme.Fill(g, card, 12 * S, Theme.Card); Theme.Stroke(g, card, 12 * S, Theme.Line, 1);
            float x = 8 * S, y = 8 * S, w = card.Width - 16 * S; var mouse = pageMenu.PointToClient(Control.MousePosition);
            for (int i = 0; i < pageItems.Count; i++)
            {
                if (pageItems[i][0] == "-") { using (var p = new Pen(Theme.Line2)) g.DrawLine(p, x + 6 * S, y + 5 * S, x + w - 6 * S, y + 5 * S); y += 11 * S; continue; }
                var r = new RectangleF(x, y, w, 34 * S);
                if (r.Contains(mouse)) Theme.Fill(g, r, 8 * S, Theme.Soft);
                using (var b = new SolidBrush(Theme.Ink)) g.DrawString(pageItems[i][0], Draw.fBody, b, r.X + 10 * S, r.Y + (34 * S - g.MeasureString(pageItems[i][0], Draw.fBody).Height) / 2);
                if (pageItems[i][1] != "") { var ks = g.MeasureString(pageItems[i][1], Draw.fSmall); using (var m = new SolidBrush(Theme.Muted)) g.DrawString(pageItems[i][1], Draw.fSmall, m, r.Right - ks.Width - 10 * S, r.Y + (34 * S - ks.Height) / 2); }
                var a = pageActs[i]; Act(pageMenu, r, delegate { HidePageMenu(); a(); });
                y += 34 * S;
            }
        }
        // a picture of the window (to show someone, or attach to a provider chat): saved in Pictures\AdeeboMeter
        void SaveWindowPicture()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AdeeboMeter"); Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "AdeeboMeter " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss", CultureInfo.InvariantCulture) + ".png");
                using (var bmp = new Bitmap(ClientSize.Width, ClientSize.Height)) { DrawToBitmap(bmp, new Rectangle(Point.Empty, ClientSize)); bmp.Save(file, ImageFormat.Png); }
                pointsFlash = L.T("Saved to Pictures\\AdeeboMeter"); pointsFlashUntil = DateTime.Now.AddSeconds(6); SyncPointsBtn();
                try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + file + "\""); } catch (System.ComponentModel.Win32Exception) { }
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is System.Runtime.InteropServices.ExternalException || e is ArgumentException)) throw; Program.Log("window picture: " + e.Message); }
        }
    }
}
