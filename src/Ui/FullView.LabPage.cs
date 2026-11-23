// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // AdeeboLab's page in the app (Adeeb, 22 Nov: "open that in the app like the way we do, then from a click we open the webpage").
    // What to draw is chosen here - a new diagram, one of yours, AdeeboMeter's own drawings of your network, an example, a file -
    // and only that click opens the lab's window (one at a time: the last one is closed first, and keeps what you drew).
    sealed partial class FullView
    {
        Panel labPage; DB labCanvas; List<string[]> labSaved; volatile bool labSavedBusy; DateTime labSavedAt = DateTime.MinValue;
        static readonly string[][] LabViewTiles = {
            new[] { "lan", "See my LAN", "Every device on your network, grouped - with how fast each answers", "users" },
            new[] { "path", "My path", "The route your last trace or MTR took, hop by hop", "route" },
            new[] { "wifi", "Wi-Fi around you", "The networks this PC hears, by channel - yours and the loud neighbours", "wifi" },
            new[] { "phone", "Your phone vs this PC", "Measured here, the usual setup there - where each one's way out goes", "phone" },
            new[] { "vpn", "VPN split", "What goes through the tunnel and what goes straight out", "lock" } };
        static readonly string[][] LabExamples = { new[] { "gamer", "Gamer" }, new[] { "cell", "On 4G / 5G" }, new[] { "trader", "Trader" }, new[] { "streamer", "Streamer" }, new[] { "iptv", "IPTV viewer" }, new[] { "server", "Server owner" } };

        Panel BuildLabPage()
        {
            labPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Visible = false };
            labCanvas = new DB { Name = "labCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            labCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintLabPage(e.Graphics); }; Wire(labCanvas);
            labPage.Controls.Add(labCanvas);
            return labPage;
        }
        // your diagrams live in the lab window's own browser storage: read again when the page is shown (at most every 5 s)
        void LabPageShown()
        {
            if (labSavedBusy || (DateTime.UtcNow - labSavedAt).TotalSeconds < 5) { labCanvas.Invalidate(); return; }
            labSavedBusy = true; labSavedAt = DateTime.UtcNow;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<string[]> l = null; try { l = LabLink.SavedDiagrams(); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("lab page: " + e.Message); }
                try { BeginInvoke((Action)delegate { labSaved = l ?? new List<string[]>(); labSavedBusy = false; if (labCanvas != null) labCanvas.Invalidate(); }); } catch (InvalidOperationException) { labSavedBusy = false; }
            });
        }
        void LabOpenFile()
        {
            using (var dlg = new OpenFileDialog { Title = L.T("Open a lab drawing"), Filter = L.T("AdeeboLab drawings") + " (*.json;*.adeebolab)|*.json;*.adeebolab|" + L.T("All files") + " (*.*)|*.*" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var fi = new FileInfo(dlg.FileName); if (fi.Length > 2000000) { labNote = L.T("That file is over 2 MB - too big for a lab drawing."); labCanvas.Invalidate(); return; }
                    string t = File.ReadAllText(dlg.FileName).Trim();
                    var d = AdeeboMeter.Json.Parse(t) as Dictionary<string, object>; if (d == null || !d.ContainsKey("nodes")) { labNote = L.T("That file isn't a lab drawing."); labCanvas.Invalidate(); return; }
                    labNote = null; OpenLab(null, false, "open", false, null, t);
                }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is FormatException || e is InvalidDataException)) throw; labNote = L.T("Couldn't read that file:") + " " + e.Message; labCanvas.Invalidate(); }
            }
        }

        void PaintLabPage(Graphics g)
        {
            g.TranslateTransform(labCanvas.AutoScrollPosition.X, labCanvas.AutoScrollPosition.Y);
            Begin(labCanvas);
            float x = 24 * S, y = 20 * S, w = labCanvas.ClientSize.Width - 48 * S;
            Color acc = Kit.Accent("learn");
            using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub)) using (var mut = new SolidBrush(Theme.Muted)) using (var nf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                // the head
                Glyphs.Tile(g, "lab", new RectangleF(x, y, 56 * S, 56 * S), acc, 16 * S);
                g.DrawString(L.T("AdeeboLab"), Draw.fHero, ink, x + 72 * S, y - 2 * S);
                string intro = L.T("Draw your network, see the path a packet takes, then break one thing and watch where it fails. A drawing opens in its own window; everything stays on this PC.");
                float ih = Draw.Measure(intro, Draw.fBody, (int)(w - 72 * S)).Height;
                g.DrawString(intro, Draw.fBody, sub, new RectangleF(x + 72 * S, y + 38 * S, w - 72 * S, ih + 4));
                y += Math.Max(60 * S, 40 * S + ih) + 22 * S;
                if (!string.IsNullOrEmpty(labNote)) { using (var nb = new SolidBrush(Theme.LvText(1))) g.DrawString(labNote, Draw.fBodyB, nb, new RectangleF(x, y, w, 22 * S), nf); y += 30 * S; }

                // start: new, open a file, break it
                var starts = new[] {
                    new object[] { "pc", L.T("New diagram"), L.T("An empty page - pick devices and click to place them"), (Action)delegate { OpenLab(null, false, "new", false); } },
                    new object[] { "database", L.T("Open a file..."), L.T("A lab drawing someone sent you (.json)"), (Action)LabOpenFile },
                    new object[] { "tool", L.T("Break it"), L.T("Pick a fault - DNS, MTU, loss, a wrong gateway - and watch where the packet dies"), (Action)delegate { OpenLab(null, true); } } };
                int sc = w > 760 * S ? 3 : 1; float sw = (w - (sc - 1) * 14 * S) / sc, sh = 86 * S;
                for (int i = 0; i < starts.Length; i++)
                {
                    var r = new RectangleF(x + (i % sc) * (sw + 14 * S), y + (i / sc) * (sh + 12 * S), sw, sh);
                    bool big = i == 0; Theme.Fill(g, r, Kit.CardRad, big ? Kit.Soft("learn") : Theme.Card); Theme.Stroke(g, Kit.Edge(r), Kit.CardRad, big ? acc : Theme.Line, big ? 1.5f : 1);
                    Glyphs.Tile(g, (string)starts[i][0], new RectangleF(r.X + 16 * S, r.Y + 22 * S, 42 * S, 42 * S), acc, 12 * S);
                    g.DrawString((string)starts[i][1], Draw.fName, ink, new RectangleF(r.X + 72 * S, r.Y + 16 * S, r.Width - 84 * S, 24 * S), nf);
                    g.DrawString((string)starts[i][2], Draw.fSmall, mut, new RectangleF(r.X + 72 * S, r.Y + 42 * S, r.Width - 84 * S, 38 * S));
                    Act(labCanvas, r, (Action)starts[i][3]);
                }
                y += ((starts.Length + sc - 1) / sc) * (sh + 12 * S) + 16 * S;

                // AdeeboMeter's own drawings of your network
                g.DrawString(L.T("YOUR NETWORK, AS ADEEBOMETER SEES IT"), Draw.fTiny, mut, x, y); y += 22 * S;
                bool hasPath = LabViews.Path(M) != null;
                int vc = w > 900 * S ? 5 : w > 560 * S ? 3 : 2; float vw = (w - (vc - 1) * 12 * S) / vc, vh = 118 * S;
                for (int i = 0; i < LabViewTiles.Length; i++)
                {
                    var t = LabViewTiles[i]; var r = new RectangleF(x + (i % vc) * (vw + 12 * S), y + (i / vc) * (vh + 12 * S), vw, vh);
                    bool off = t[0] == "path" && !hasPath;
                    Kit.Box(g, r, Kit.CardRad);
                    Glyphs.Tile(g, t[3], new RectangleF(r.X + 14 * S, r.Y + 14 * S, 38 * S, 38 * S), off ? Theme.Muted : acc, 10 * S);
                    using (var tb = new SolidBrush(off ? Theme.Muted : Theme.Ink)) g.DrawString(L.T(t[1]), Draw.fBodyB, tb, new RectangleF(r.X + 14 * S, r.Y + 60 * S, r.Width - 22 * S, 20 * S), nf);
                    g.DrawString(off ? L.T("Run a trace or MTR first") : L.T(t[2]), Draw.fSmall, mut, new RectangleF(r.X + 14 * S, r.Y + 82 * S, r.Width - 22 * S, 34 * S));
                    string v = t[0]; if (!off) Act(labCanvas, r, delegate { OpenLab(null, false, v, false); });
                    if (v == "lan")   // ask the devices their names first (about 8 s)
                    {
                        string at = L.T("ask them") + " ›"; var asz = g.MeasureString(at, Draw.fSmall); var ar = new RectangleF(r.Right - asz.Width - 12 * S, r.Y + 16 * S, asz.Width, asz.Height);
                        using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(at, Draw.fSmall, lb, ar.X, ar.Y);
                        Act(labCanvas, ar, delegate { OpenLab(null, false, "lan", true); });
                    }
                }
                y += ((LabViewTiles.Length + vc - 1) / vc) * (vh + 12 * S) + 16 * S;

                // your diagrams
                g.DrawString(L.T("YOUR DIAGRAMS"), Draw.fTiny, mut, x, y); y += 22 * S;
                if (labSaved == null) { g.DrawString(L.T("Looking..."), Draw.fBody, mut, x, y); y += 30 * S; }
                else if (labSaved.Count == 0) { g.DrawString(L.T("None yet - every drawing you change in the lab is kept here (on this PC)."), Draw.fBody, mut, x, y); y += 30 * S; }
                else
                {
                    int dc = w > 900 * S ? 4 : w > 560 * S ? 3 : 2; float dw = (w - (dc - 1) * 12 * S) / dc, dh = 58 * S;
                    for (int i = 0; i < labSaved.Count && i < 24; i++)
                    {
                        var d = labSaved[i]; var r = new RectangleF(x + (i % dc) * (dw + 12 * S), y + (i / dc) * (dh + 10 * S), dw, dh);
                        Theme.Fill(g, r, 12 * S, Theme.Soft);
                        Glyphs.Draw(g, "route", new RectangleF(r.X + 12 * S, r.Y + 17 * S, 24 * S, 24 * S), acc, 1.6f);
                        g.DrawString(d[1], Draw.fBodyB, ink, new RectangleF(r.X + 46 * S, r.Y + 10 * S, r.Width - 54 * S, 20 * S), nf);
                        g.DrawString(d[2], Draw.fSmall, mut, new RectangleF(r.X + 46 * S, r.Y + 31 * S, r.Width - 54 * S, 18 * S), nf);
                        string id = d[0]; Act(labCanvas, r, delegate { OpenLab(null, false, "file", false, id, null); });
                    }
                    y += ((Math.Min(24, labSaved.Count) + dc - 1) / dc) * (dh + 10 * S) + 16 * S;
                }

                // examples
                g.DrawString(L.T("EXAMPLES - A TYPICAL HOME, READY TO BREAK"), Draw.fTiny, mut, x, y); y += 22 * S;
                float cx = x;
                foreach (var ex in LabExamples)
                {
                    string t = L.T(ex[1]); var ts = g.MeasureString(t, Draw.fBody); var r = new RectangleF(cx, y, ts.Width + 28 * S, 34 * S);
                    if (r.Right > x + w) { cx = x; y += 42 * S; r = new RectangleF(cx, y, r.Width, r.Height); }
                    Theme.Fill(g, r, 17 * S, Theme.Card); Theme.Stroke(g, Kit.Edge(r), 17 * S, Theme.Line, 1);
                    g.DrawString(t, Draw.fBody, ink, r.X + 14 * S, r.Y + (r.Height - ts.Height) / 2);
                    string id = ex[0]; Act(labCanvas, r, delegate { OpenLab(null, false, "ex", false, id, null); });
                    cx = r.Right + 8 * S;
                }
                y += 50 * S;
                g.DrawString(L.T("The lab window needs Microsoft Edge (it's in Windows). One lab window at a time: opening another closes the last, and keeps what you drew."), Draw.fSmall, mut, new RectangleF(x, y, w, 40 * S)); y += 44 * S;
            }
            SetNeed(labCanvas, new Size(0, (int)(y + 20 * S)));
        }
    }
}
