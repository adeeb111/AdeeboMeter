// Network Lab (1.1.6): the Learn › Labs card that opens it without an event, and the one place every "Show my path"
// button goes through. The lab itself is docs/lab.html in its own Edge window (src/Labs/LabLink.cs).
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    sealed partial class FullView
    {
        string labNote;   // what went wrong opening the lab (no Edge, the data folder not writable) - shown on the page, never a pop-up

        // json: a measured path (LabLink.PathJson), or null for the lab as it was left
        // json null: the lab's start page, with its ready-made drawings (See my LAN, Path, Wi-Fi, Phone vs PC, VPN split) made
        // off the UI thread from what the app already knows; view: open straight into one of them
        void OpenLab(string json, bool breakIt) { OpenLab(json, breakIt, null, false); }
        volatile bool alabBusy;
        void OpenLab(string json, bool breakIt, string view, bool ask) { OpenLab(json, breakIt, view, ask, null, null); }
        void OpenLab(string json, bool breakIt, string view, bool ask, string arg) { OpenLab(json, breakIt, view, ask, arg, null); }
        // arg: a diagram's id or an example's (view "file" / "ex"); openJson: a lab file you picked (view "open")
        void OpenLab(string json, bool breakIt, string view, bool ask, string arg, string openJson)
        {
            if (json != null) { LabDone(LabLink.Open(json, breakIt)); return; }
            if (alabBusy) return; alabBusy = true;
            if (ask) Toast(L.T("Asking the devices on your network who they are (about 8 s)..."));
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                string note;
                try
                {
                    string views = LabViews.All(M);
                    if (ask && view == "lan") { try { var lan = LabViews.Lan(M, true); if (lan != null) views = views.Substring(0, views.Length - 1) + ",\"lan\":" + lan + "}"; } catch (Exception le) { if (le is OutOfMemoryException) throw; Program.Log("lab lan ask: " + le.Message); } }   // R2-027: the other views stay
                    if (openJson != null) views = views.Substring(0, views.Length - 1) + ",\"open\":" + openJson + "}";
                    note = LabLink.Open(null, breakIt, views, view, arg);
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("lab views: " + e.Message); note = LabLink.Open(null, breakIt); }
                finally { alabBusy = false; }
                try { BeginInvoke((Action)(() => LabDone(note))); } catch (InvalidOperationException) { }
            }, System.Threading.Tasks.TaskCreationOptions.LongRunning);
        }
        void LabDone(string note)
        {
            labNote = note;
            if (labNote != null) Toast(labNote);
            if (learnInfo != null) learnInfo.Invalidate();
            if (labCanvas != null) labCanvas.Invalidate();
        }
        // one button style for the lab's links: a filled pill, or a plain link when small
        RectangleF LabPill(Graphics g, Control c, string text, float x, float y, bool primary, Action go)
        {
            var sz = g.MeasureString(L.T(text), Draw.fBodyB); var r = new RectangleF(x, y, sz.Width + 28 * S, 34 * S);
            Theme.Fill(g, r, 17 * S, primary ? Theme.Blue : Theme.Soft);
            using (var b = new SolidBrush(primary ? Theme.OnBlue : Theme.Ink)) g.DrawString(L.T(text), Draw.fBodyB, b, x + 14 * S, y + (34 * S - sz.Height) / 2);
            Act(c, r, go); return r;
        }
        float PaintNetLab(Graphics g, float x, float y, float w)
        {
            y = Title2(g, "AdeeboLab", x, y, w);
            y = Lead2(g, "Draw your network the way an engineer would - your PC, Wi-Fi, router, the provider's boxes, the internet and the server - then break one thing and watch the packet stop where it fails. It opens in its own window; nothing is sent.", x, y, w);
            y += 10 * S;
            var a = LabPill(g, learnInfo, "Open the AdeeboLab  ›", x, y, true, delegate { OpenNav("lab"); });
            LabPill(g, learnInfo, "Break it", a.Right + 10 * S, y, false, delegate { OpenLab(null, true); });
            y = a.Bottom + 16 * S;
            y = Lead2(g, "Your own path: in Packet journey, Gamer, Trading, Streaming or the Service board, press \"Show my path\" - the stops AdeeboMeter measured are drawn white, the provider's equipment it can't see is grey and dashed.", x, y, w);
            int done = LabLink.Faults.Count(f => Points.Me.Has("nlab." + f[0]));
            if (Cfg.Points) y = Lead2(g, "Break it: " + done + " of " + LabLink.Faults.Length + " faults watched to the end · +" + Points.Value("nlab.dns") + " Adeebo Points each, the first time.", x, y + 4 * S, w);
            if (labNote != null) using (var mb = new SolidBrush(Theme.LvText(1))) { g.DrawString(labNote, Draw.fSmall, mb, x, y + 4 * S); y += 22 * S; }
            return y;
        }
    }
}
