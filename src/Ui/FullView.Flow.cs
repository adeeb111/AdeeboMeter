// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Flow (PLAN-1.1.7 D2, 28 Nov): Terminal's second tab - build a check from blocks and see the same thing as a script
    // beside it. A flow IS a shell script: Run saves it to /data/flows and runs it in the Terminal tab, inside the shell's
    // own fence. Edit either side; the other follows (comments, echo, sleep, for, if/else, VAR="..." become blocks;
    // anything else is a command block, kept as written).
    sealed partial class FullView
    {
        internal sealed class FBlock
        {
            public string Kind = "cmd", Text = "";   // cmd say wait repeat each if set note
            public List<FBlock> Kids = new List<FBlock>(), Else;
            public bool Box { get { return Kind == "repeat" || Kind == "each" || Kind == "if"; } }
        }
        Panel flowPanel, termPanel; DB flowCanvas; TextBox flowText; RoundBox flowName; List<FBlock> flow = new List<FBlock>(); FBlock flowSel; bool flowSync;
        PillButton shTabTerm, shTabFlow; TextBox flowEdit; FBlock flowEditing;
        static readonly string[][] FlowKinds = {
            new[] { "cmd", "Tool", "toolbox" }, new[] { "say", "Say", "home" }, new[] { "wait", "Wait", "time" }, new[] { "repeat", "Repeat", "gamer" },
            new[] { "each", "For each", "stream" }, new[] { "if", "If it works", "trade" }, new[] { "set", "Set", "learn" }, new[] { "note", "Note", "calc" } };

        // the Terminal | Flow switch above the shell (BuildShellPage calls this once)
        void ShellTabs(Panel page, Control term)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = Sc(48), BackColor = ShBg, Padding = new Padding(Sc(12), Sc(8), Sc(12), Sc(4)) };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = ShBg, WrapContents = false };
            shTabTerm = new PillButton(L.T("Terminal"), PillButton.K.Chip) { Selected = true }; shTabFlow = new PillButton(L.T("Flow - build with blocks"), PillButton.K.Chip);
            bar.Controls.Add(shTabTerm); bar.Controls.Add(shTabFlow); top.Controls.Add(bar);
            termPanel = new Panel { Dock = DockStyle.Fill, BackColor = ShBg }; page.Controls.Remove(term); termPanel.Controls.Add(term);
            page.Controls.Add(termPanel); page.Controls.Add(top);
            shTabTerm.Click += delegate { ShowFlow(false); };
            shTabFlow.Click += delegate { ShowFlow(true); };
        }
        void ShowFlow(bool on)
        {
            if (on && flowPanel == null) { flowPanel = BuildFlow(); termPanel.Parent.Controls.Add(flowPanel); flowPanel.BringToFront(); }
            if (flowPanel != null) flowPanel.Visible = on; termPanel.Visible = !on;
            shTabTerm.Selected = !on; shTabFlow.Selected = on;
            if (on) { if (flow.Count == 0) FlowLoad("network-check.sh"); flowCanvas.Focus(); } else term.Focus();
        }
        Panel BuildFlow()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card };
            var tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(48), BackColor = Theme.Card, Padding = new Padding(Sc(12), Sc(8), Sc(12), 0), WrapContents = false };
            flowName = new RoundBox(Sc(200), Draw.fInput, "my-check") { Margin = new Padding(0, 0, Sc(8), 0) }; flowName.Box.Text = "my-check";
            var open = new PillButton(L.T("Open..."), PillButton.K.Chip); var nw = new PillButton(L.T("New"), PillButton.K.Chip);
            var save = new PillButton(L.T("Save"), PillButton.K.Chip); var run = new PillButton(L.T("Run ▶"), PillButton.K.Primary);
            tools.Controls.Add(flowName); tools.Controls.Add(open); tools.Controls.Add(nw); tools.Controls.Add(save); tools.Controls.Add(run);
            var add = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Sc(46), BackColor = Theme.Card, Padding = new Padding(Sc(12), Sc(4), Sc(12), 0), WrapContents = false, AutoScroll = true };
            add.Controls.Add(new Label { Text = L.T("Add:"), AutoSize = true, ForeColor = Theme.Muted, Font = Draw.fBody, Margin = new Padding(0, Sc(8), Sc(6), 0) });
            foreach (var k in FlowKinds) { var kind = k[0]; var b = new PillButton(L.T(k[1]), PillButton.K.Chip); b.Click += delegate { FlowAdd(kind); }; add.Controls.Add(b); }
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Card };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            flowCanvas = new DB { Name = "flowCanvas", Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, TabStop = true };
            flowCanvas.Paint += delegate(object s, PaintEventArgs e) { Prep(e.Graphics); PaintFlow(e.Graphics); }; Wire(flowCanvas);
            flowCanvas.KeyDown += FlowKey; flowCanvas.MouseDown += delegate { flowCanvas.Focus(); };
            flowText = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, AcceptsTab = true, Font = new Font(IsFont("Cascadia Mono") ? "Cascadia Mono" : "Consolas", 10f), BackColor = ShBg, ForeColor = ShFg, BorderStyle = BorderStyle.None };
            var ttimer = new Timer { Interval = 500 }; ttimer.Tick += delegate { ttimer.Stop(); if (!flowSync) { flow = FlowParse(flowText.Text); flowSel = null; flowCanvas.Invalidate(); } };
            flowText.TextChanged += delegate { if (flowSync) return; ttimer.Stop(); ttimer.Start(); };   // typing in the script: the blocks follow half a second later
            var right = new Panel { Dock = DockStyle.Fill, BackColor = ShBg, Padding = new Padding(Sc(10)) }; right.Controls.Add(flowText);
            var rhead = new Label { Dock = DockStyle.Top, Height = Sc(26), Text = L.T("The same flow as a Terminal script - edit either side"), ForeColor = ShDim, BackColor = ShBg, Font = Draw.fSmall }; right.Controls.Add(rhead);
            split.Controls.Add(flowCanvas, 0, 0); split.Controls.Add(right, 1, 0);
            p.Controls.Add(split); p.Controls.Add(add); p.Controls.Add(tools);
            open.Click += delegate
            {
                var m = Menus.Make();
                foreach (var dir in new[] { "flows", "scripts" })
                {
                    string d = Path.Combine(Program.DataDir, dir); if (!Directory.Exists(d)) continue;
                    foreach (var f in Directory.GetFiles(d, "*.sh").OrderBy(z => z, StringComparer.OrdinalIgnoreCase)) { string ff = f; m.Items.Add("/data/" + dir + "/" + Path.GetFileName(f), null, delegate { FlowLoadFile(ff); }); }
                }
                if (m.Items.Count == 0) foreach (var r in ShReadyScripts) { string nm = r[0]; m.Items.Add(nm, null, delegate { FlowLoad(nm); }); }
                m.Show(open, new Point(0, open.Height));
            };
            nw.Click += delegate { flow = new List<FBlock> { new FBlock { Kind = "note", Text = "my check - add blocks from the row above" } }; flowName.Box.Text = "my-check"; flowSel = null; FlowChanged(); };
            save.Click += delegate { string f = FlowSave(); if (f != null) term.Flash("saved " + f); };
            run.Click += delegate { FlowRun(); };
            return p;
        }

        // ---------- blocks <-> script
        internal static string FlowScript(List<FBlock> l, int ind = 0)
        {
            var sb = new StringBuilder(); string pad = new string(' ', ind * 2);
            foreach (var b in l)
            {
                string t = b.Text ?? "";
                switch (b.Kind)
                {
                    case "note": foreach (var ln in t.Split('\n')) sb.Append(pad).Append("# ").Append(ln.TrimEnd()).Append('\n'); break;
                    case "say": sb.Append(pad).Append("echo \"").Append(t.Replace("\"", "\\\"")).Append("\"\n"); break;
                    case "wait": sb.Append(pad).Append("sleep ").Append(t.Trim() == "" ? "5" : t.Trim()).Append('\n'); break;
                    case "set": sb.Append(pad).Append(t.Trim()).Append('\n'); break;
                    case "repeat":
                        { int n; if (!int.TryParse(t.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) n = 3; n = Math.Max(1, Math.Min(500, n));
                          sb.Append(pad).Append("for i in $(seq 1 ").Append(n).Append("); do\n").Append(FlowScript(b.Kids, ind + 1)).Append(pad).Append("done\n"); } break;
                    case "each": sb.Append(pad).Append("for ").Append(t.Trim() == "" ? "s in example.com" : t.Trim()).Append("; do\n").Append(FlowScript(b.Kids, ind + 1)).Append(pad).Append("done\n"); break;
                    case "if":
                        sb.Append(pad).Append("if ").Append(t.Trim() == "" ? "ping 1.1.1.1 3" : t.Trim()).Append("; then\n").Append(FlowScript(b.Kids, ind + 1));
                        if (b.Else != null) sb.Append(pad).Append("else\n").Append(FlowScript(b.Else, ind + 1));
                        sb.Append(pad).Append("fi\n"); break;
                    default: sb.Append(pad).Append(t).Append('\n'); break;
                }
            }
            return sb.ToString();
        }
        internal static List<FBlock> FlowParse(string text)
        {
            var root = new List<FBlock>(); var stack = new Stack<Tuple<FBlock, bool>>();   // the open box, and whether we're in its else part
            Func<List<FBlock>> cur = () => stack.Count == 0 ? root : stack.Peek().Item2 ? stack.Peek().Item1.Else : stack.Peek().Item1.Kids;
            FBlock lastNote = null;
            foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                string ln = raw.Trim(); if (ln == "") { lastNote = null; continue; }
                var mFor = System.Text.RegularExpressions.Regex.Match(ln, @"^for\s+(.+?)\s*;\s*do$");
                var mSeq = System.Text.RegularExpressions.Regex.Match(ln, @"^for\s+\w+\s+in\s+\$\(seq\s+1\s+(\d+)\)\s*;\s*do$");
                var mIf = System.Text.RegularExpressions.Regex.Match(ln, @"^if\s+(.+?)\s*;\s*then$");
                if (ln.StartsWith("#", StringComparison.Ordinal))
                {
                    string t = ln.TrimStart('#').Trim();
                    if (lastNote != null) lastNote.Text += "\n" + t; else { lastNote = new FBlock { Kind = "note", Text = t }; cur().Add(lastNote); }
                    continue;
                }
                lastNote = null;
                if (mSeq.Success) { var b = new FBlock { Kind = "repeat", Text = mSeq.Groups[1].Value }; cur().Add(b); stack.Push(Tuple.Create(b, false)); }
                else if (mFor.Success) { var b = new FBlock { Kind = "each", Text = mFor.Groups[1].Value }; cur().Add(b); stack.Push(Tuple.Create(b, false)); }
                else if (mIf.Success) { var b = new FBlock { Kind = "if", Text = mIf.Groups[1].Value }; cur().Add(b); stack.Push(Tuple.Create(b, false)); }
                else if (ln == "else" && stack.Count > 0 && stack.Peek().Item1.Kind == "if") { var b = stack.Pop().Item1; b.Else = b.Else ?? new List<FBlock>(); stack.Push(Tuple.Create(b, true)); }
                else if ((ln == "done" || ln == "fi") && stack.Count > 0) stack.Pop();
                else if (ln.StartsWith("echo ", StringComparison.Ordinal) && System.Text.RegularExpressions.Regex.IsMatch(ln, "^echo \"[^\"]*\"$")) cur().Add(new FBlock { Kind = "say", Text = ln.Substring(6, ln.Length - 7) });
                else if (System.Text.RegularExpressions.Regex.IsMatch(ln, @"^sleep\s+[\d.]+$")) cur().Add(new FBlock { Kind = "wait", Text = ln.Substring(6).Trim() });
                else if (System.Text.RegularExpressions.Regex.IsMatch(ln, "^[A-Za-z_][A-Za-z0-9_]*=")) cur().Add(new FBlock { Kind = "set", Text = ln });
                else cur().Add(new FBlock { Kind = "cmd", Text = ln });
            }
            return root;
        }
        void FlowChanged()
        {
            flowSync = true; try { flowText.Text = FlowScript(flow).Replace("\n", "\r\n"); } finally { flowSync = false; }
            flowCanvas.Invalidate();
        }
        void FlowLoad(string ready)
        {
            var r = ShReadyScripts.FirstOrDefault(z => z[0] == ready); string f = Path.Combine(Program.DataDir, "scripts", ready);
            string text = File.Exists(f) ? SafeRead(f) : r != null ? r[2] : "";
            flow = FlowParse(text); flowName.Box.Text = Path.GetFileNameWithoutExtension(ready); flowSel = null; FlowChanged();
        }
        void FlowLoadFile(string f) { flow = FlowParse(SafeRead(f)); flowName.Box.Text = Path.GetFileNameWithoutExtension(f); flowSel = null; FlowChanged(); }
        static string SafeRead(string f) { try { return File.ReadAllText(f); } catch (IOException) { return ""; } catch (UnauthorizedAccessException) { return ""; } }
        string FlowSave()
        {
            string nm = System.Text.RegularExpressions.Regex.Replace(flowName.Box.Text.Trim(), @"[^A-Za-z0-9_.-]+", "-").Trim('-', '.'); if (nm == "") nm = "my-check";
            try { string d = Path.Combine(Program.DataDir, "flows"); Directory.CreateDirectory(d); string f = Path.Combine(d, nm + ".sh"); File.WriteAllText(f, FlowScript(flow)); return "/data/flows/" + nm + ".sh"; }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; term.Flash("couldn't save: " + e.Message); return null; }
        }
        void FlowRun()
        {
            string f = FlowSave(); if (f == null) return;
            ShowFlow(false);
            if (shFg != null) { term.Flash("something is still running - Ctrl+C stops it, then Run again"); return; }
            ShSubmit("cd /data/flows && ./" + Path.GetFileName(f));   // the shell's own runner and fence: nothing new runs here
        }

        // ---------- the block list
        List<FBlock> FlowParent(FBlock b, List<FBlock> l = null)
        {
            l = l ?? flow; if (l.Contains(b)) return l;
            foreach (var x in l) { var r = FlowParent(b, x.Kids); if (r != null) return r; if (x.Else != null) { r = FlowParent(b, x.Else); if (r != null) return r; } }
            return null;
        }
        void FlowAdd(string kind)
        {
            var b = new FBlock { Kind = kind, Text = kind == "say" ? "== the next step" : kind == "wait" ? "5" : kind == "repeat" ? "3" : kind == "each" ? "s in example.com github.com" : kind == "if" ? "ping 1.1.1.1 3" : kind == "set" ? "SITES=\"example.com github.com\"" : kind == "note" ? "what this part does" : "" };
            if (kind == "if") b.Else = new List<FBlock> { new FBlock { Kind = "say", Text = "it didn't work" } };
            if (kind == "cmd") { FlowPickTool(b); return; }
            FlowInsert(b);
        }
        void FlowInsert(FBlock b)
        {
            if (flowSel != null && flowSel.Box) flowSel.Kids.Add(b);
            else { var par = flowSel != null ? FlowParent(flowSel) : null; if (par != null) par.Insert(par.IndexOf(flowSel) + 1, b); else flow.Add(b); }
            flowSel = b; FlowChanged(); FlowStartEdit(b);
        }
        // a tool: pick it from the Toolbox's own list (search as you type); its example fills the arguments
        void FlowPickTool(FBlock b)
        {
            if (shTools == null) shTools = Tools2();
            var f = new Form { Text = L.T("Add a tool"), StartPosition = FormStartPosition.CenterParent, Size = new Size(Sc(520), Sc(520)), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, KeyPreview = true };
            var box = new RoundBox(Sc(480), Draw.fInput, "ping, dns, wifi, certificate...") { Location = new Point(Sc(14), Sc(14)) };
            var list = new ListBox { Location = new Point(Sc(14), Sc(64)), Size = new Size(Sc(476), Sc(360)), Font = Draw.fBody, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
            var ok = new Button { Text = L.T("Add"), DialogResult = DialogResult.OK, Location = new Point(Sc(370), Sc(436)), Size = new Size(Sc(120), Sc(34)) };
            f.Controls.Add(box); f.Controls.Add(list); f.Controls.Add(ok); f.AcceptButton = ok;
            Action fill = () =>
            {
                string q = box.Box.Text.Trim(); list.BeginUpdate(); list.Items.Clear();
                foreach (var t in shTools.Where(t => q == "" || t.Id.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || L.T(t.Title).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).Take(200)) list.Items.Add(t.Id + "  -  " + L.T(t.Title));
                if (list.Items.Count > 0) list.SelectedIndex = 0; list.EndUpdate();
            };
            box.Box.TextChanged += delegate { fill(); }; list.DoubleClick += delegate { f.DialogResult = DialogResult.OK; f.Close(); };
            f.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Down && list.SelectedIndex < list.Items.Count - 1) { list.SelectedIndex++; e.Handled = true; } else if (e.KeyCode == Keys.Up && list.SelectedIndex > 0) { list.SelectedIndex--; e.Handled = true; } else if (e.KeyCode == Keys.Escape) f.Close(); };
            fill(); Kit.Polish(f);
            if (f.ShowDialog(this) != DialogResult.OK || list.SelectedItem == null) { f.Dispose(); return; }
            string id = list.SelectedItem.ToString().Split(' ')[0]; f.Dispose();
            var ex = Examples.Get(id);
            b.Text = id + (ex != null && ex.In.Length > 0 ? " " + string.Join(" ", ex.In.Where(z => !string.IsNullOrEmpty(z)).Select(z => z.Contains(" ") ? "\"" + z + "\"" : z)) : "");
            FlowInsert(b);
        }
        void FlowStartEdit(FBlock b)
        {
            FlowEndEdit(true); if (b == null) return;
            Rectangle r; if (!flowRows.TryGetValue(b, out r)) { flowCanvas.Invalidate(); return; }
            flowEditing = b;
            flowEdit = new TextBox { Font = Draw.fBody, Text = b.Text, BorderStyle = BorderStyle.FixedSingle, Multiline = b.Kind == "note", Bounds = new Rectangle(r.X + Sc(120), r.Y + Sc(6), Math.Max(Sc(120), r.Width - Sc(220)), b.Kind == "note" ? Sc(48) : Sc(26)) };
            flowEdit.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && !(flowEdit.Multiline && e.Shift)) { e.SuppressKeyPress = true; FlowEndEdit(true); flowCanvas.Focus(); } else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; FlowEndEdit(false); flowCanvas.Focus(); } };
            flowEdit.LostFocus += delegate { FlowEndEdit(true); };
            flowCanvas.Controls.Add(flowEdit); flowEdit.Focus(); flowEdit.SelectAll();
        }
        void FlowEndEdit(bool keep)
        {
            if (flowEdit == null) return; var e = flowEdit; var b = flowEditing; flowEdit = null; flowEditing = null;
            if (keep && b != null && b.Text != e.Text) { b.Text = e.Text; FlowChanged(); }
            flowCanvas.Controls.Remove(e); e.Dispose(); flowCanvas.Invalidate();
        }
        void FlowMove(FBlock b, int d)
        {
            var par = FlowParent(b); if (par == null) return; int i = par.IndexOf(b), j = i + d; if (j < 0 || j >= par.Count) return;
            par.RemoveAt(i); par.Insert(j, b); FlowChanged();
        }
        void FlowDelete(FBlock b) { var par = FlowParent(b); if (par == null) return; int i = par.IndexOf(b); par.Remove(b); flowSel = par.Count == 0 ? null : par[Math.Min(i, par.Count - 1)]; FlowChanged(); }
        List<FBlock> FlowFlat(List<FBlock> l = null) { var o = new List<FBlock>(); foreach (var b in l ?? flow) { o.Add(b); o.AddRange(FlowFlat(b.Kids)); if (b.Else != null) o.AddRange(FlowFlat(b.Else)); } return o; }
        void FlowKey(object s, KeyEventArgs e)
        {
            var all = FlowFlat(); int i = flowSel == null ? -1 : all.IndexOf(flowSel);
            if (e.KeyCode == Keys.Down && !e.Control) { if (all.Count > 0) flowSel = all[Math.Min(all.Count - 1, i + 1)]; flowCanvas.Invalidate(); e.Handled = true; }
            else if (e.KeyCode == Keys.Up && !e.Control) { if (all.Count > 0) flowSel = all[Math.Max(0, i - 1)]; flowCanvas.Invalidate(); e.Handled = true; }
            else if (e.Control && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) && flowSel != null) { FlowMove(flowSel, e.KeyCode == Keys.Up ? -1 : 1); e.Handled = true; }
            else if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.F2) && flowSel != null) { FlowStartEdit(flowSel); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete && flowSel != null) { FlowDelete(flowSel); e.Handled = true; }
        }
        readonly Dictionary<FBlock, Rectangle> flowRows = new Dictionary<FBlock, Rectangle>();
        void PaintFlow(Graphics g)
        {
            g.TranslateTransform(flowCanvas.AutoScrollPosition.X, flowCanvas.AutoScrollPosition.Y);
            Begin(flowCanvas); flowRows.Clear();
            float x = 16 * S, y = 14 * S, w = flowCanvas.ClientSize.Width - 32 * S;
            using (var mut = new SolidBrush(Theme.Muted)) g.DrawString(L.T("Click a block to pick it, double-click or Enter to change it. Ctrl+Up/Down moves it, Delete removes it. A new block goes after the picked one - or inside it, for Repeat, For each and If."), Draw.fSmall, mut, new RectangleF(x, y, w, 36 * S));
            y += 42 * S;
            if (flow.Count == 0) using (var mut = new SolidBrush(Theme.Muted)) g.DrawString(L.T("Empty - add a block from the row above, or Open a ready-made check."), Draw.fBody, mut, x, y);
            y = PaintFlowList(g, flow, x, y, w, 0);
            SetNeed(flowCanvas, new Size(0, (int)(y + 40 * S)));
        }
        float PaintFlowList(Graphics g, List<FBlock> l, float x, float y, float w, int depth)
        {
            foreach (var b in l)
            {
                var kd = FlowKinds.FirstOrDefault(k => k[0] == b.Kind) ?? FlowKinds[0];
                float h = (b.Kind == "note" ? 26 + 16 * Math.Min(3, Math.Max(0, (b.Text ?? "").Split('\n').Length - 1)) : 38) * S;
                var r = new RectangleF(x, y, w, h); bool sel = b == flowSel;
                Theme.Fill(g, r, 10 * S, sel ? Theme.BlueSoft : b.Kind == "note" ? Theme.Card : Kit.Soft(kd[2])); Theme.Stroke(g, r, 10 * S, sel ? Theme.Blue : Theme.Line, sel ? 1.5f * S : 1);
                var chip = new RectangleF(x + 8 * S, y + (h - 24 * S) / 2, 100 * S, 24 * S);
                Theme.Fill(g, chip, 12 * S, b.Kind == "note" ? Theme.Soft : Kit.Accent(kd[2]));
                using (var cb = new SolidBrush(b.Kind == "note" ? Theme.Muted : Color.White)) using (var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(L.T(kd[1]), Draw.fTiny, cb, chip, cf);
                string shown = b.Kind == "repeat" ? string.Format(L.T("{0} times"), b.Text) : b.Kind == "wait" ? string.Format(L.T("{0} seconds"), b.Text) : b.Kind == "if" ? b.Text + "  " + L.T("works?") : b.Text;
                using (var tb = new SolidBrush(b.Kind == "note" ? Theme.Muted : Theme.Ink)) using (var nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = b.Kind == "note" ? 0 : StringFormatFlags.NoWrap })
                    g.DrawString(shown, b.Kind == "cmd" ? Draw.fMonoS : Draw.fBody, tb, new RectangleF(x + 120 * S, y + (b.Kind == "note" ? 5 * S : 10 * S), w - 220 * S, h - 8 * S), nf);
                // up, down, remove - on the picked block
                if (sel)
                {
                    var acts = new[] { Tuple.Create("↑", -1), Tuple.Create("↓", 1), Tuple.Create("✕", 0) }; float ax = x + w - 96 * S;
                    foreach (var a in acts)
                    {
                        var ar = new RectangleF(ax, y + (h - 26 * S) / 2, 26 * S, 26 * S); Theme.Fill(g, ar, 13 * S, Theme.Card); Theme.Stroke(g, ar, 13 * S, Theme.Line, 1);
                        using (var ab = new SolidBrush(a.Item2 == 0 ? Theme.Red : Theme.Ink)) using (var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(a.Item1, Draw.fBody, ab, ar, cf);
                        var bb = b; int dd = a.Item2; Act(flowCanvas, ar, delegate { if (dd == 0) FlowDelete(bb); else FlowMove(bb, dd); });
                        ax += 30 * S;
                    }
                }
                var rr = Rectangle.Round(new RectangleF(r.X + flowCanvas.AutoScrollPosition.X, r.Y + flowCanvas.AutoScrollPosition.Y, r.Width, r.Height)); flowRows[b] = rr;
                var b2 = b; Act(flowCanvas, new RectangleF(x, y, sel ? w - 100 * S : w, h), delegate { if (flowSel == b2) FlowStartEdit(b2); else { flowSel = b2; flowCanvas.Invalidate(); } });
                y += h + 6 * S;
                if (b.Box)
                {
                    float y0 = y; y = PaintFlowList(g, b.Kids, x + 28 * S, y, w - 28 * S, depth + 1);
                    if (b.Kids.Count == 0) { using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("(pick this block and add steps - they go inside)"), Draw.fSmall, mb, x + 30 * S, y + 2 * S); y += 24 * S; }
                    if (b.Else != null)
                    {
                        using (var eb = new SolidBrush(Kit.Ink(kd[2]))) g.DrawString(L.T("if it didn't work:"), Draw.fSmall, eb, x + 30 * S, y + 2 * S); y += 22 * S;
                        y = PaintFlowList(g, b.Else, x + 28 * S, y, w - 28 * S, depth + 1);
                    }
                    using (var gp = new Pen(Kit.Accent(kd[2]), 2 * S)) g.DrawLine(gp, x + 14 * S, y0 - 4 * S, x + 14 * S, y - 4 * S);
                }
            }
            return y;
        }
    }
}
