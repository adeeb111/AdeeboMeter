// The buddy's line on a tool page (PLAN 11.7): one line while it runs (busy) and one when it's done (the verdict in the
// current persona or mood). Words only - the rows below are the facts and stay exactly as they are.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    partial class FullView
    {
        string buddyFor; string buddyText = "";   // the rows (or run) the line was picked for: one pick per result, not per repaint
        // a mode's buddy line (Streaming, Trading): one steady line for the state it's in; quiet when the buddy is quiet
        float PaintModeLine(Graphics g, string key, string v, bool problem, float x, float y, float w)
        {
            if (!Lines.Speaks(problem ? "problem" : "state")) return y;
            bool money = key.StartsWith("trade.", StringComparison.Ordinal);
            string text = money && Lines.Faith != "off" ? Lines.SteadyAs(key, v, "plain") : Lines.Steady(key, v);
            if (text == "") return y;
            text = Typing.Show(money ? "trade" : "stream", text, money ? (System.Windows.Forms.Control)tradeCanvas : streamCanvas);
            var r = new RectangleF(x, y, w, 34 * S); Theme.Fill(g, r, 10 * S, Kit.Soft("mine"));
            Kit.Icon(g, "mood", new RectangleF(r.X + 10 * S, r.Y + 9 * S, 16 * S, 16 * S), Kit.Accent("mine"));
            using (var b = new SolidBrush(Kit.Ink("mine"))) using (var sf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                g.DrawString(text, Draw.fBody, b, new RectangleF(r.X + 34 * S, r.Y + 8 * S, r.Width - 44 * S, 20 * S), sf);
            return y + r.Height + 12 * S;
        }
        float PaintBuddyLine(Graphics g, float x, float y, float w)
        {
            string text = "";
            if (tbBusy && tbRun0 != null)
            {
                string k0 = "busy|" + tbRun0.GetHashCode();
                if (buddyFor != k0) { buddyFor = k0; buddyText = Lines.Speaks("state") ? Lines.Line("busy", null) : ""; }
                text = buddyText;
            }
            else if (tbRows != null && !Examples.IsExample(tbRows))
            {
                string k1 = "rows|" + tbSel + "|" + tbRows.GetHashCode() + "|" + Lines.Who() + "|" + Lines.Chat + "|" + Voice.Serious;   // AM-CG-180
                if (buddyFor != k1)
                {
                    buddyFor = k1; string v; string key = VerdictKey(tbSel, tbRows, Inputs(), out v);
                    int worst = WorstLevel(tbRows);
                    buddyText = key == null || !Lines.Speaks(worst >= 2 ? "problem" : "state") ? "" : Lines.Line(key, v);
                }
                text = buddyText;
            }
            if (text == "") return y;
            text = Typing.Show("tool", text, tbCanvas);
            var r = new RectangleF(x, y, w, 34 * S); Theme.Fill(g, r, 10 * S, Kit.Soft("mine"));
            Kit.Icon(g, "mood", new RectangleF(r.X + 10 * S, r.Y + 9 * S, 16 * S, 16 * S), Kit.Accent("mine"));
            using (var b = new SolidBrush(Kit.Ink("mine"))) using (var sf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                g.DrawString(text, Draw.fBody, b, new RectangleF(r.X + 34 * S, r.Y + 8 * S, r.Width - 44 * S, 20 * S), sf);
            return y + r.Height + 12 * S;
        }
        static int WorstLevel(List<string[]> rows)
        {
            int worst = 0, lv; foreach (var o in rows) if (o.Length > 0 && int.TryParse(o[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out lv) && lv >= 0 && lv <= 2 && lv > worst) worst = lv;
            return worst;
        }
        // which moment a tool's result is, and the value the line may quote; null = this tool has no buddy verdict
        internal static string VerdictKey(string tool, List<string[]> rows, string[] inputs, out string v)
        {
            v = null; int worst = WorstLevel(rows); string target = inputs != null && inputs.Length > 0 ? inputs[0] ?? "" : "";
            Func<string, string[]> row = name => rows.FirstOrDefault(o => o.Length > 2 && o[1].IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            switch (tool)
            {
                case "port": case "tcpping":
                    {
                        v = target != "" ? target : null; if (worst >= 2) return "port.closed";
                        bool open = rows.Any(o => o.Length > 2 && o[0] == "0" && Regex.IsMatch(o[2], @"\bopen\b|answered|\d+(\.\d+)?\s*ms", RegexOptions.IgnoreCase));   // AM-CG-168: positive evidence only
                        return open ? "port.open" : null;
                    }
                case "mtr":
                    { var lossy = rows.FirstOrDefault(o => o.Length > 2 && o[0] != "0" && Regex.IsMatch(o[1], @"^\s*(hop\s*)?\d+", RegexOptions.IgnoreCase) && o[2].Contains("%") && !o[2].StartsWith("0%", StringComparison.Ordinal));
                      if (worst >= 1 && lossy != null) { var m = Regex.Match(lossy[1], @"\d+"); v = m.Success ? m.Value : null; return "mtr.lossy"; }
                      return worst == 0 ? "mtr.clean" : null; }
                case "mtu": case "udpmtu":
                    {   // bug-hunt L-148: the number from the row that IS the MTU (biggest packet / MTU), never the first number anywhere
                        if (worst == 0) return null;
                        var mr = rows.FirstOrDefault(o => o.Length > 2 && Regex.IsMatch(o[1], "biggest|MTU|largest", RegexOptions.IgnoreCase) && Regex.IsMatch(o[2], @"\b\d{3,4}\s*bytes\b"));
                        var m = mr != null ? Regex.Match(mr[2], @"\b(\d{3,4})\s*bytes\b") : Match.Empty; v = m.Success ? m.Groups[1].Value : null; return "mtu.small";
                    }
                case "speedtest": { var d = row("Download"); if (d != null) v = Regex.Match(d[2], @"[0-9.,]+\s*[kMG]bps").Value;   /* L-149: kbps too */ if (v == "") v = null; return worst >= 1 ? "speed.bloat" : "speed.great"; }
                case "bloat": return worst >= 1 ? "speed.bloat" : null;
                case "http": case "certlive": case "https": case "chain":
                    { var ex = rows.FirstOrDefault(o => o.Length > 2 && o[0] == "2" && o[2].IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0);
                      if (ex != null) { var m = Regex.Match(ex[2], @"\d{4}-\d{2}-\d{2}|\d{1,2} \w+ \d{4}"); v = m.Success ? m.Value : null; return "cert.expired"; }
                      bool certSeen = rows.Any(o => o.Length > 2 && o[0] == "0" && Regex.IsMatch(o[1] + " " + o[2], "certificate|TLS|expires", RegexOptions.IgnoreCase));   // AM-CG-187
                      return worst == 0 && certSeen ? "cert.ok" : null; }
                case "inventory": case "arp": case "sweep": { int n = rows.Count(o => o.Length > 2 && o[0] != "-1" && Regex.IsMatch(o[1], @"\d+\.\d+\.\d+\.\d+|[0-9a-f]{2}[:-][0-9a-f]{2}", RegexOptions.IgnoreCase)); if (n == 0) return null; v = n.ToString(CultureInfo.CurrentCulture); return "devices.found"; }
                case "encrypt": return worst == 0 ? "encrypt.done" : null;
                case "decrypt": return worst == 0 ? "decrypt.done" : null;
                case "lag": case "gamecompare": case "prematch": return worst >= 1 ? "game.lag" : "game.good";
                case "vpn": return worst >= 1 ? "vpn.slow" : "vpn.on";
                case "winupdate": case "changed": return worst >= 1 ? "update.suspect" : null;
                case "dig": case "dnsrace": return worst >= 1 ? "dns.slow" : null;
            }
            return null;
        }
        // Settings > Companion: a live preview, then "Talk like..." as tiles - Surprise me first, the featured ones, the rest
        string[] factsUndo;   // what Just the facts changed, until Undo or a restart
        float PaintCompanion(Graphics g, float x, float y, float w)
        {
            string who = Lines.Who(), sample = Voice.Serious ? "" : !Lines.Speaks("state") ? L.T("(face only - the buddy shows its face but says nothing)") : Lines.Steady("all.good", null);   // AM-CG-181
            string cf = Lines.Faith != "off" && !Voice.Serious ? Lines.Comfort() : ""; if (cf != "" && Lines.Speaks("idle")) sample += "   ·   " + cf;
            var pr = new RectangleF(x, y, Math.Min(w, 620 * S), 54 * S); Theme.Fill(g, pr, 12 * S, Kit.Soft("mine"));
            Kit.Icon(g, "mood", new RectangleF(pr.X + 14 * S, pr.Y + 17 * S, 20 * S, 20 * S), Kit.Accent("mine"));
            using (var b = new SolidBrush(Kit.Ink("mine"))) using (var m = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("PREVIEW") + "  ·  " + L.T(Lines.PersonaName(Lines.Persona == "surprise" ? who : Lines.Persona == "none" ? Lines.Mood : Lines.Persona)).ToUpperInvariant(), Draw.fTiny, m, pr.X + 46 * S, pr.Y + 9 * S);
                g.DrawString(Voice.Serious ? L.T("Fun lines are off - plain words everywhere.") : sample, Draw.fBody, b, new RectangleF(pr.X + 46 * S, pr.Y + 26 * S, pr.Width - 56 * S, 22 * S));
            }
            y = pr.Bottom + 18 * S;
            using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T("TALK LIKE..."), Draw.fTiny, m, x, y); y += 22 * S;
            var ids = new List<string> { "surprise" }; ids.AddRange(Lines.Personas.Select(p => p[0])); ids.Add("none");
            int cols = w > 820 * S ? 4 : w > 560 * S ? 3 : 2; float cw = (w - (cols - 1) * 10 * S) / cols, ch = 50 * S; int k = 0;
            foreach (var id0 in ids)
            {
                string id = id0; bool on = Lines.Persona == id && !Voice.Serious;
                var r = new RectangleF(x + (k % cols) * (cw + 10 * S), y + (k / cols) * (ch + 10 * S), cw, ch);
                Theme.Fill(g, r, 12 * S, on ? Kit.Soft("mine") : Theme.Soft); if (on) Theme.Stroke(g, r, 12 * S, Kit.Accent("mine"), 1.5f * S);
                Kit.Icon(g, id == "surprise" ? "star" : id == "none" ? "mood" : "app", new RectangleF(r.X + 12 * S, r.Y + 17 * S, 16 * S, 16 * S), on ? Kit.Accent("mine") : Theme.Muted);
                using (var b = new SolidBrush(on ? Kit.Ink("mine") : Theme.Ink)) using (var sf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(L.T(Lines.PersonaName(id)), Draw.fBodyB, b, new RectangleF(r.X + 36 * S, r.Y + 15 * S, r.Width - 44 * S, 20 * S), sf);
                Act(settingsCanvas, r, delegate { Lines.Persona = id; Voice.Serious = false; Cfg.Save(); Invalidate(true); });
                k++;
            }
            y += ((k + cols - 1) / cols) * (ch + 10 * S) + 10 * S;
            var jf = new RectangleF(x, y, 260 * S, 30 * S);
            using (var b = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(L.T("Just the facts (Plain, Details on, face only)"), Draw.fBodyB, b, jf.X, jf.Y + 4 * S);
            Act(settingsCanvas, jf, delegate { factsUndo = new[] { Voice.Serious ? "1" : "0", Lines.Chat, Cfg.Details ? "1" : "0" }; Voice.Serious = true; Lines.Chat = "face"; Cfg.Details = true; Cfg.Save(); Invalidate(true); foreach (System.Windows.Forms.Control c in Descendants(this)) c.Invalidate(); });
            if (factsUndo != null)
            {
                // AM-CG-182: what it changed, and the way back
                using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T("Changed: Fun lines off · Details on · How chatty: face only."), Draw.fSmall, m, jf.X, jf.Bottom + 2 * S);
                var ur = new RectangleF(jf.X + 380 * S, jf.Bottom, 60 * S, 20 * S); using (var b = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(L.T("Undo"), Draw.fBodyB, b, ur.X, ur.Y);
                Act(settingsCanvas, ur, delegate { if (factsUndo == null) return; Voice.Serious = factsUndo[0] == "1"; Lines.Chat = factsUndo[1]; Cfg.Details = factsUndo[2] == "1"; factsUndo = null; Cfg.Save(); Invalidate(true); foreach (System.Windows.Forms.Control c in Descendants(this)) c.Invalidate(); });
                y += 22 * S;
            }
            y += 44 * S;
            // your own lines: import (checked and fixed, never silent), export to share, clear
            int own = Lines.MineCount; float bx = x;
            using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T("YOUR OWN LINES") + "  ·  " + own, Draw.fTiny, m, x, y); y += 22 * S;
            using (var b = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
                foreach (var a in new[] { "Import...", "Export...", "Clear" })
                {
                    if (a != "Import..." && own == 0) continue;
                    string t = L.T(a); var ts = g.MeasureString(t, Draw.fBodyB); var r = new RectangleF(bx, y, ts.Width + 4, 24 * S);
                    g.DrawString(t, Draw.fBodyB, b, r.X, r.Y); string a0 = a;
                    Act(settingsCanvas, r, delegate { if (a0 == "Import...") ImportLines(); else if (a0 == "Export...") SaveText("AdeeboMeter-my-lines", Lines.MineText()); else { if (!Lines.ClearMine()) System.Windows.Forms.MessageBox.Show(this, L.T("Windows wouldn't let me remove your lines file - nothing changed."), "AdeeboMeter"); Invalidate(true); } });
                    bx += ts.Width + 24 * S;
                }
            if (Lines.CanUndoClear)
                using (var b = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
                {
                    string t = L.T("Undo clear"); var ts = g.MeasureString(t, Draw.fBodyB); var r = new RectangleF(bx, y, ts.Width + 4, 24 * S);
                    g.DrawString(t, Draw.fBodyB, b, r.X, r.Y); Act(settingsCanvas, r, delegate { if (!Lines.UndoClear()) System.Windows.Forms.MessageBox.Show(this, L.T("Couldn't bring your lines back."), "AdeeboMeter"); Invalidate(true); });
                }
            y += 30 * S;
            using (var m = new SolidBrush(Theme.Muted)) g.DrawString(L.T("A text file, one line each: key|text - for example all.good|All calm, {addr}. Yours come first."), Draw.fSmall, m, new RectangleF(x, y, w, 36 * S));
            return y + 40 * S;
        }
        void ImportLines()
        {
            string path;
            using (var d = new System.Windows.Forms.OpenFileDialog { Filter = L.T("Text files") + " (*.txt)|*.txt|" + L.T("All files") + "|*.*", Title = L.T("Import lines") })
            { if (d.ShowDialog(this) != System.Windows.Forms.DialogResult.OK) return; path = d.FileName; }
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                List<string> kept = null, report;
                try
                {
                    var fi = new System.IO.FileInfo(path);
                    if (fi.Length > 1048576) report = new List<string> { L.T("Refused: the file is over 1 MB.") };
                    else report = Lines.Import(System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8), out kept);
                }
                catch (Exception e) { report = new List<string> { L.T("Couldn't read the file: ") + e.Message }; }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        string msg = string.Join("\n", report.Take(18)) + (report.Count > 18 ? "\n(+" + (report.Count - 18) + " more)" : "");
                        if (kept == null || kept.Count == 0) { System.Windows.Forms.MessageBox.Show(this, msg, "AdeeboMeter", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information); return; }
                        if (System.Windows.Forms.MessageBox.Show(this, msg + "\n\n" + L.T("Add these lines to yours?"), "AdeeboMeter", System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Question) != System.Windows.Forms.DialogResult.OK) return;
                        if (!Lines.SaveMine(kept, true)) System.Windows.Forms.MessageBox.Show(this, L.T("Couldn't save your lines."), "AdeeboMeter");
                        else if (Lines.LastDropped > 0) System.Windows.Forms.MessageBox.Show(this, L.T("Saved. Your lines are limited to 600, so the oldest") + " " + Lines.LastDropped + " " + L.T("were let go."), "AdeeboMeter");
                        Invalidate(true);
                    });
                }
                catch (InvalidOperationException) { }
            });
        }
        // Settings > Widget: every style as a live picture drawn by the widget's own code - the folded strip on top, the card
        // you get on hover below. Pictures are kept for 10 s (they show live numbers) and redrawn on theme or option changes.
        readonly Dictionary<string, Bitmap> wpics = new Dictionary<string, Bitmap>(StringComparer.Ordinal); DateTime wpicsAt; string wpicsKey = "";
        Bitmap WPic(string style, bool card)
        {
            // bug-hunt L-146: "example" or live is decided now, not by the last picture drawn - live numbers replace the example at once
            var snapNow = snap; bool exampleNow = snapNow == null || double.IsNaN(snapNow.Down) || snapNow.DownHist == null || snapNow.DownHist.Length < 5;
            string key = exampleNow + "|" + Theme.Dark + "|" + Theme.Palette + "|" + Cfg.BigWidget + "|" + Draw.S + "|" + Cfg.WUnits + Cfg.WDir + Cfg.WGraph + Cfg.WTotal + Cfg.Buddy + Cfg.BuddyFace + Lines.Mood + Lines.Persona + Voice.Serious;   // AM-CG-200
            if (key != wpicsKey || (DateTime.Now - wpicsAt).TotalSeconds > 10) { foreach (var b in wpics.Values) if (b != null) b.Dispose(); wpics.Clear(); wpicsKey = key; wpicsAt = DateTime.Now; }
            Bitmap bm; string k = style + (card ? "|c" : "|s");
            if (!wpics.TryGetValue(k, out bm)) { try { bm = Draw.Preview(PreviewSnap(), style, card); } catch (Exception e) { Program.Log("widget preview " + style + ": " + e.Message); bm = null; } wpics[k] = bm; }
            return bm;
        }
        // the live minute when there is one; example numbers (marked on the tile) before the first readings or offline
        bool previewExample;
        Snap PreviewSnap()
        {
            var s = snap ?? new Snap(); previewExample = double.IsNaN(s.Down) || s.DownHist == null || s.DownHist.Length < 5;
            if (!previewExample) return s;
            var e = new Snap { Level = 0, Headline = L.T("All quiet. Nothing needs you."), Chip = "All quiet", Down = 24.6, Up = 3.2, Cpu = 12, Mem = 41, Disk = 3, TodayDown = 3.1e9, TodayUp = 4.2e8, Path = new[] { 0, 0, 0, 0, 0 } };
            e.DownHist = Enumerable.Range(0, 60).Select(i => 18 + 10 * Math.Sin(i / 5.0) + (i % 7)).ToArray(); e.UpHist = Enumerable.Range(0, 60).Select(i => 2.5 + Math.Cos(i / 4.0)).ToArray(); e.NetHist = Enumerable.Range(0, 60).Select(i => 18 + (double)(i % 5)).ToArray();
            e.TopCpu = new List<AppStat> { new AppStat { Name = "chrome", Cpu = 9.4 }, new AppStat { Name = "OneDrive", Cpu = 3.1 }, new AppStat { Name = "Teams", Cpu = 2.2 } };
            return e;
        }
        float PaintWidgetPicker(Graphics g, float x, float y, float w)
        {
            using (var m = new SolidBrush(Theme.Sub)) g.DrawString(L.T("How the corner widget looks - pick one. Hover the widget for its card."), Draw.fBody, m, x, y); y += 30 * S;
            int cols = w > 980 * S ? 4 : w > 700 * S ? 3 : 2; float cw = (w - (cols - 1) * 12 * S) / cols, ch = 250 * S; int k = 0;
            for (int i = 0; i < WStyleIds.Length; i++)
            {
                string id = WStyleIds[i]; bool on = Cfg.WidgetStyle == id;
                var r = new RectangleF(x + (k % cols) * (cw + 12 * S), y + (k / cols) * (ch + 12 * S), cw, ch); k++;
                if (Kit.Off(g, r)) { Act(settingsCanvas, r, delegate { Cfg.WidgetStyle = id; Cfg.Save(); Invalidate(true); }); continue; }
                Theme.Fill(g, r, 12 * S, on ? Theme.BlueSoft : Theme.Soft); Theme.Stroke(g, r, 12 * S, on ? Theme.Blue : Theme.Line, on ? 2 : 1);
                using (var tb = new SolidBrush(on ? (Theme.Dark ? Theme.Ink : Theme.BlueDark) : Theme.Ink)) g.DrawString(L.T(WStyle[i]), Draw.fBodyB, tb, r.X + 12 * S, r.Y + 10 * S);
                using (var hb = new SolidBrush(Theme.Muted)) using (var sf = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(L.T(WStyleHint[i]), Draw.fSmall, hb, new RectangleF(r.X + 12 * S, r.Y + 30 * S, r.Width - 24 * S, 18 * S), sf);
                // the pictures, scaled to fit the tile (never up)
                var area = new RectangleF(r.X + 10 * S, r.Y + 54 * S, r.Width - 20 * S, r.Height - 64 * S);
                var strip = WPic(id, false); var card = WPic(id, true); float py = area.Y;
                var q = g.InterpolationMode; g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                if (strip != null) { float k1 = Math.Min(1f, area.Width / strip.Width); var sr = new RectangleF(area.X, py, strip.Width * k1, strip.Height * k1); g.DrawImage(strip, sr); py += sr.Height + 8 * S; }
                if (card != null) { float k2 = Math.Min(Math.Min(1f, area.Width / card.Width), (area.Bottom - py) / card.Height); if (k2 > 0.1f) g.DrawImage(card, new RectangleF(area.X, py, card.Width * k2, card.Height * k2)); }
                g.InterpolationMode = q;
                if (previewExample) using (var eb = new SolidBrush(Theme.Muted)) { string ex = L.T("example"); var es = g.MeasureString(ex, Draw.fTiny); g.DrawString(ex, Draw.fTiny, eb, r.Right - es.Width - 10 * S, r.Y + 12 * S); }
                Act(settingsCanvas, r, delegate { Cfg.WidgetStyle = id; Cfg.Save(); Invalidate(true); });
            }
            return y + ((k + cols - 1) / cols) * (ch + 12 * S) + 10 * S;
        }
    }
}
