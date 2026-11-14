// The chips under a tool's boxes (PLAN 11.6): one tap fills the box you're in - Browse for a file, Yes / No, the choices
// the box's hint lists, the hosts you used lately, quick minute or second picks. Six reused buttons, nothing made per tool.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AdeeboMeter
{
    partial class FullView
    {
        FlowLayoutPanel tbAssist; readonly PillButton[] tbAssistPool = new PillButton[6]; int tbAssistBox = -1;
        static readonly string[] MinPicks = { "1", "5", "15", "60" }, SecPicks = { "10", "30", "60" }, Fallback = { "1.1.1.1" }, TestPins = { "example.com" };
        static readonly Regex ChoiceSplit = new Regex(@"\s*(?:,|/|\bor\b|\|)\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        void BuildAssist()
        {
            tbAssist = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Card, Padding = new Padding(Sc(10), 0, Sc(8), Sc(6)), WrapContents = true, Visible = false };
            for (int i = 0; i < tbAssistPool.Length; i++)
            {
                var b = new PillButton("", PillButton.K.Chip) { Visible = false, Margin = new Padding(0, 0, Sc(6), 0) }; tbAssistPool[i] = b;
                b.Click += delegate(object s, EventArgs e) { var a = ((PillButton)s).Tag as Action; if (a != null) a(); };
                tbAssist.Controls.Add(b);
            }
        }

        // what a box wants, from its English label and hint
        internal static List<string> AssistFor(string input, string gateway, string target, IEnumerable<string> pins, out bool browse)
        {
            browse = false; var l = new List<string>(); string s = (input ?? "").ToLowerInvariant();
            int dash = s.IndexOf(" - ", StringComparison.Ordinal); string lab = dash > 0 ? s.Substring(0, dash) : s, cue = dash > 0 ? s.Substring(dash + 3) : "";
            if (SecretInput(input ?? "")) return l;
            if (Regex.IsMatch(lab, @"\b(file|path|folder|capture|saved result)\b|\.(pcap|cer|pfx|txt)\b")) { browse = true; return l; }
            if (Regex.IsMatch(s, @"\byes\s*/\s*no\b|\byes or no\b|\(y/n\)|\btype yes\b")) { l.Add("yes"); l.Add("no"); return l; }
            if (Regex.IsMatch(lab, @"^minutes\b")) { l.AddRange(MinPicks); return l; }
            if (Regex.IsMatch(lab, @"^seconds\b")) { l.AddRange(SecPicks); return l; }
            // a short menu in the hint: "443 or 53", "A, AAAA, MX, TXT" - but never examples ("e.g. 200, 60, 12") or anything with a dot
            bool eg = Regex.IsMatch(cue, @"^(e\.g\.|eg\b|like\b|for example)");
            var parts = ChoiceSplit.Split(cue).Select(p => p.Trim().Trim('.', '(', ')', '"')).Where(p => p != "").ToList();
            if (!eg && !cue.Contains("://") && parts.Count >= 2 && parts.Count <= 6 && parts.All(p => Regex.IsMatch(p, @"^[a-z0-9+_-]{1,12}$")) && Regex.IsMatch(cue, @",|/|\bor\b"))
            {
                string orig = (input ?? "").Substring((input ?? "").Length - cue.Length);   // keep the hint's own capitals (A, AAAA)
                var op = ChoiceSplit.Split(orig).Select(p => p.Trim().Trim('.', '(', ')', '"')).Where(p => p != "").ToList();
                return op.Count == parts.Count ? op : parts;
            }
            // a host: the one you used last, your router, your pinned sites. Not for names of runs, OIDs, MACs, accounts or lists
            if (Regex.IsMatch(lab, @"\b(host|target|address|server|device|ip|site|router|destination|url)\b|^name\b(?! this)|^name:port")
                && !Regex.IsMatch(lab, @"\b(oid|mib|mac|account)\b|^name this") && !cue.Contains("empty ="))
            {
                // a box that wants host:port gets the port its hint names (":587", "(1812)") - 443 when it names none
                string port = null;
                if (Regex.IsMatch(lab, @"^(host|name):port")) { var m = Regex.Match(cue, @":(\d{2,5})\b"); if (!m.Success) m = Regex.Match(cue, @"\b(\d{2,5})\b"); port = m.Success ? m.Groups[1].Value : "443"; }
                foreach (var h in new[] { target, gateway }.Concat(pins ?? Enumerable.Empty<string>()).Concat(Fallback))
                {
                    if (string.IsNullOrEmpty(h)) continue;
                    string v = port != null && h.IndexOf(':') < 0 ? h + ":" + port : h;
                    if (!l.Contains(v) && l.Count < 5) l.Add(v);
                }
            }
            return l;
        }

        // self-test: every tool box's chips, one line each, and anything that looks wrong
        internal string AssistCheck(System.Text.StringBuilder log)
        {
            var bad = new List<string>();
            foreach (var t in Tools2())
                foreach (var inp in t.Inputs)
                {
                    if (string.IsNullOrEmpty(inp)) continue; bool br;
                    var c = AssistFor(inp, "192.168.1.1", "", TestPins, out br);
                    if (br || c.Count > 0) log.AppendLine("      chips " + t.Id + " [" + inp + "] " + (br ? "Browse " : "") + string.Join(" | ", c));
                    if (SecretInput(inp) && (br || c.Count > 0)) bad.Add(t.Id + ": chips on a secret box");
                    if (c.Any(z => z.EndsWith(":", StringComparison.Ordinal) || z.Contains("//") || z.Length > 60)) bad.Add(t.Id + ": odd chip in " + inp);
                }
            return string.Join("; ", bad);
        }
        void FillAssist(int box)
        {
            if (tbAssist == null) return; tbAssistBox = box;
            var rb = box >= 0 && box < tbPool.Length ? tbPool[box] : null;
            string inp = rb != null && rb.Visible ? rb.Tag as string : null;
            bool browse = false; string gw; lock (M.L) gw = M.Gateway;
            var picks = inp != null ? AssistFor(inp, gw, tbTarget, Cfg.Pins, out browse) : new List<string>();
            tbAssist.SuspendLayout(); int n = 0;
            if (browse)
            {
                var b = tbAssistPool[n++]; b.Text = "Browse..."; b.Accent = QAccent(QuestionOf(Tools2().FirstOrDefault(z => z.Id == tbSel) ?? Tools2()[0]));
                b.Tag = (Action)delegate { using (var d = new OpenFileDialog { CheckFileExists = true, Title = L.T("Pick a file") }) if (d.ShowDialog(this) == DialogResult.OK) { rb.Box.Text = d.FileName; rb.Box.Focus(); } };
                b.Visible = true;
            }
            foreach (var p in picks.Take(tbAssistPool.Length - n))
            {
                var b = tbAssistPool[n++]; string v = p; b.Text = p; b.Accent = null;
                b.Tag = (Action)delegate { rb.Box.Text = v; rb.Box.Focus(); rb.Box.SelectionStart = v.Length; };
                b.Visible = true;
            }
            for (int i = n; i < tbAssistPool.Length; i++) tbAssistPool[i].Visible = false;
            tbAssist.Visible = n > 0 && !tbHome;
            tbAssist.ResumeLayout();
        }
    }
}
