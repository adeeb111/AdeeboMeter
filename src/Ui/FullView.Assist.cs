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

        static bool PrivIp(string h) { System.Net.IPAddress a; return System.Net.IPAddress.TryParse(h, out a) && IspReport.Private(a); }
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
            /* release check 24 Nov: "0-63" is a range, and "IPv4 or IPv6" under a Target says what it takes - neither is a menu */
            if (!eg && !cue.Contains("://") && parts.Count >= 2 && parts.Count <= 6 && parts.All(p => Regex.IsMatch(p, @"^[a-z0-9+_-]{1,12}$") && !Regex.IsMatch(p, @"^\d+-\d+$")) && Regex.IsMatch(cue, @",|/|\bor\b")
                && !parts.All(p => p == "ipv4" || p == "ipv6"))
            {
                // keep the hint's own capitals (A, AAAA) - cut from the original at its own " - " (bug-hunt L-364: lower-casing can change
                // the length, "İ" becomes two characters, so the lower-cased length isn't used to cut the original)
                string inp = input ?? ""; int od = inp.IndexOf(" - ", StringComparison.Ordinal); string orig = od > 0 ? inp.Substring(od + 3) : inp;
                var op = ChoiceSplit.Split(orig).Select(p => p.Trim().Trim('.', '(', ')', '"')).Where(p => p != "").ToList();
                return op.Count == parts.Count ? op : parts;
            }
            // a host: the one you used last, your router, your pinned sites. Not for names of runs, OIDs, MACs, accounts or lists
            if (Regex.IsMatch(lab, @"\b(host|target|address|server|device|ip|site|router|destination|url)\b|^name\b(?! this)|^name:port")
                && !Regex.IsMatch(lab, @"\b(oid|mib|mac|account)\b|^name this") && !cue.Contains("empty =")
                // 21 Nov sitting test: a plain "Name" box whose example isn't a site (a key's name: "e.g. Adeeb work") gets no host chips
                && !(Regex.IsMatch(lab, @"^name$") && !cue.Contains(".")))
            {
                /* release check 24 Nov (the self-test's chip list): the chips must be things this box can use.
                   - a box named only by a soft word ("Name", "Which device") whose examples aren't hosts (a key's name, "headset") gets none
                   - a box for something on your own network (a device, an iperf3 server, "IP on your network") gets only local addresses
                   - a server box gets the real servers its own hint names (time.windows.com, 1.1.1.1), not your pinned websites
                   - the SNI box takes a name, never an IP */
                string plain = Regex.Replace(cue, @"\b(e\.g|i\.e)\.", " ");
                bool hosty = Regex.IsMatch(plain, @"[a-z0-9-]+\.[a-z0-9-]+|:\d{2,5}\b");
                bool strong = Regex.IsMatch(lab, @"\b(host|target|address|server|ip|site|router|destination|url)\b|^name:port");
                if (!strong && !hosty) return l;
                bool local = Regex.IsMatch(s, @"on your network|\biperf3?\b|\bconsole\b") || Regex.IsMatch(lab, @"\bdevice\b");
                // a host box whose own examples are all local (10.0.0.1, nas01, dc01) is for something inside your network
                var ips = Regex.Matches(plain, @"\b\d{1,3}(?:\.\d{1,3}){3}\b").Cast<Match>().Select(z => z.Value).ToList();
                var names = Regex.Matches(plain, @"\b[a-z]+\d+\b").Cast<Match>().Select(z => z.Value).Where(z => !Regex.IsMatch(z, @"^(ipv|sha|md|v)\d")).ToList();
                if (!Regex.IsMatch(lab, @"\b(destination|target|site|url)\b") && (ips.Count > 0 || names.Count > 0) && ips.All(PrivIp)
                    && !Regex.IsMatch(plain, @"(?<![\w-])(?![a-z0-9-]*example\.)[a-z0-9-]+\.[a-z]{2,}\b")) local = true;
                bool service = (Regex.IsMatch(lab, @"\bserver\b") || Regex.IsMatch(cue, @"\d+\s*/\s*\d+[^,]*\b(smtp|imap|pop)\b")) && !local;
                bool nameOnly = Regex.IsMatch(lab, @"\bsni\b|name to ask");
                var examples = new List<string>();
                foreach (Match em in Regex.Matches(plain, @"(?<![\w./:-])((?:[a-z0-9-]+\.)+[a-z]{2,}|\d{1,3}(?:\.\d{1,3}){3})(?![\w.-])"))
                {
                    string ex = em.Groups[1].Value; System.Net.IPAddress eip;
                    if (ex.Contains("example.") || ex.StartsWith("e.g", StringComparison.Ordinal)) continue;   // illustrations, not places
                    if (System.Net.IPAddress.TryParse(ex, out eip) && (PrivIp(ex) || ex.StartsWith("203.0.113.", StringComparison.Ordinal) || ex.StartsWith("198.51.100.", StringComparison.Ordinal) || ex.StartsWith("192.0.2.", StringComparison.Ordinal))) continue;
                    examples.Add(ex);
                }
                Func<string, bool> fits = h =>
                {
                    System.Net.IPAddress hip; bool isIp = System.Net.IPAddress.TryParse(h, out hip);
                    if (nameOnly && isIp) return false;
                    if (local) return isIp ? PrivIp(h) : h.IndexOf('.') < 0;
                    return true;
                };
                var pool = service ? new[] { target }.Concat(examples) : new[] { target, gateway }.Concat(examples).Concat(pins ?? Enumerable.Empty<string>()).Concat(Fallback);
                // a box that wants an IP address (and not a name) gets only IP addresses - never example.com
                bool ipOnly = Regex.IsMatch(lab, @"\bip\b") && !Regex.IsMatch(lab, @"\b(host|name|site|url|domain|destination|target|server|device)\b");
                // a box that wants host:port gets the port its hint names (":587", "(1812)") - 443 when it names none
                string port = null;
                if (Regex.IsMatch(lab, @"^(host|name):port")) { var m = Regex.Match(cue, @":(\d{2,5})\b"); if (!m.Success) m = Regex.Match(cue, @"\b(\d{2,5})\b"); port = m.Success ? m.Groups[1].Value : "443"; }
                foreach (var h in pool)
                {
                    if (string.IsNullOrEmpty(h) || !fits(h)) continue;
                    string v = port != null && h.IndexOf(':') < 0 ? h + ":" + port : h;
                    System.Net.IPAddress ipa; if (ipOnly && !System.Net.IPAddress.TryParse(h, out ipa)) continue;
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
                    // release check 24 Nov: chips the box can't use - a range, a format word, a public address for a local box, hosts for a name
                    if (c.Any(z => Regex.IsMatch(z, @"^\d+-\d+$") || z == "IPv4" || z == "IPv6")) bad.Add(t.Id + ": a range or a format offered as a value in " + inp);
                    if (Regex.IsMatch(inp, @"on your network|iperf", RegexOptions.IgnoreCase) && c.Any(z => { var h = z.Split(':')[0]; System.Net.IPAddress a; return !System.Net.IPAddress.TryParse(h, out a) || !IspReport.Private(a); })) bad.Add(t.Id + ": a public address offered for a box on your own network");
                    if (Regex.IsMatch(inp, @"^(Name - e\.g\. [A-Z][a-z]+( [a-z]+)?|Which device)") && c.Count > 0) bad.Add(t.Id + ": host chips in a box for a name (" + inp + ")");
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
                var b = tbAssistPool[n++]; b.Text = AdeeboMeter.L.T("Browse..."); b.Accent = null;
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
