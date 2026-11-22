using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // AdeeboShell (Adeeb, 22 Nov): the whole app from the keyboard, Linux-terminal style. Every Toolbox tool is a command
    // (its boxes are the arguments, in order) and prints the same result as the Toolbox's text; pages open with "open";
    // "debug" shows the raw values the pages tidy away (addresses, adapter, MACs) - on this PC only, never sent.
    // Not a Windows shell: nothing typed here runs a program. Ctrl+` opens it from anywhere; Tab completes; Up/Down history;
    // Ctrl+C stops a run; Ctrl+L clears.
    sealed partial class FullView
    {
        Panel shellPage; RichTextBox shOut; TextBox shIn; Label shPrompt;
        readonly List<string> shHist = new List<string>(); int shHi = -1; Toolkit.Run shRun; string shLast = "";
        static readonly Color ShBg = Color.FromArgb(13, 17, 23), ShFg = Color.FromArgb(201, 209, 217), ShDim = Color.FromArgb(125, 133, 144),
            ShGreen = Color.FromArgb(63, 185, 80), ShAmber = Color.FromArgb(210, 153, 34), ShRed = Color.FromArgb(248, 81, 73), ShCyan = Color.FromArgb(57, 197, 207), ShBlue = Color.FromArgb(88, 166, 255), ShPurple = Color.FromArgb(188, 140, 255);

        Panel BuildShellPage()
        {
            shellPage = new Panel { Dock = DockStyle.Fill, BackColor = ShBg, Visible = false, Padding = new Padding(Sc(14), Sc(10), Sc(14), Sc(10)) };
            var mono = new Font(IsFont("Cascadia Mono") ? "Cascadia Mono" : IsFont("Consolas") ? "Consolas" : FontFamily.GenericMonospace.Name, 10.5f);
            shOut = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = ShBg, ForeColor = ShFg, Font = mono, DetectUrls = false, WordWrap = true, ScrollBars = RichTextBoxScrollBars.Vertical, TabStop = false };
            var bar = new Panel { Dock = DockStyle.Bottom, Height = Sc(30), BackColor = ShBg };
            shPrompt = new Label { Dock = DockStyle.Left, AutoSize = true, ForeColor = ShGreen, BackColor = ShBg, Font = mono, Padding = new Padding(0, Sc(5), 0, 0), Text = Prompt() };
            shIn = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = ShBg, ForeColor = ShFg, Font = mono, AcceptsTab = true };
            var pad = new Panel { Dock = DockStyle.Fill, BackColor = ShBg, Padding = new Padding(Sc(6), Sc(6), 0, 0) }; pad.Controls.Add(shIn);
            bar.Controls.Add(pad); bar.Controls.Add(shPrompt);
            shellPage.Controls.Add(shOut); shellPage.Controls.Add(bar);
            shIn.KeyDown += ShKey;
            shOut.MouseUp += delegate { if (shOut.SelectionLength == 0) shIn.Focus(); };
            shellPage.VisibleChanged += delegate { if (shellPage.Visible) { if (shOut.TextLength == 0) ShWelcome(); BeginInvoke((Action)delegate { shIn.Focus(); }); } };
            return shellPage;
        }
        static bool IsFont(string n) { using (var f = new Font(n, 10f)) return f.Name == n; }
        static string Prompt() { string u = Environment.UserName.ToLowerInvariant(), h = Environment.MachineName.ToLowerInvariant(); return u + "@" + h + ":adeebo$"; }

        // ---------- writing
        void W(string s, Color c) { if (shOut == null || shOut.IsDisposed) return; shOut.SelectionStart = shOut.TextLength; shOut.SelectionLength = 0; shOut.SelectionColor = c; shOut.AppendText(s); shOut.SelectionColor = ShFg; }
        void WL(string s, Color c) { W(s + "\n", c); }
        void WL(string s) { WL(s, ShFg); }
        // no drawing while a block of lines goes in (WM_SETREDRAW), one paint after
        int shHolds;
        void ShHold(bool on)
        {
            if (shOut == null || shOut.IsDisposed || !shOut.IsHandleCreated) return;
            if (on) { if (shHolds++ == 0) Native.SendMessage(shOut.Handle, 0x000B, IntPtr.Zero, IntPtr.Zero); }
            else if (shHolds > 0 && --shHolds == 0) { Native.SendMessage(shOut.Handle, 0x000B, (IntPtr)1, IntPtr.Zero); shOut.Invalidate(); }
        }
        void ShEnd() { shOut.SelectionStart = shOut.TextLength; shOut.ScrollToCaret(); }
        void ShWelcome()
        {
            WL("AdeeboShell - AdeeboMeter " + Program.Version + " from the keyboard", ShCyan);
            WL("Every tool is a command; the same checks and the same answers as the Toolbox. Nothing here runs Windows programs.", ShDim);
            WL("Type  help  for commands,  tools  for the list,  man <tool>  for what one does. Tab completes, Ctrl+C stops.", ShDim);
            WL("");
        }
        // a result row as the Toolbox's text, coloured by how it went
        void ShRows(List<string[]> rows)
        {
            foreach (var o in rows.Where(x => !IsSecretRow(x)))
            {
                if (o[0] == "6" || o[0] == "7" || o[0] == "8") { WL((o[0] == "7" ? "! " : "  ") + ShareCell(o[2]), o[0] == "7" ? ShAmber : ShFg); continue; }
                if (o[0] == "-1") { WL(""); WL(ShareCell(o[1]).ToUpperInvariant() + (o[2] != "" ? "  " + ShareCell(o[2]) : ""), ShBlue); continue; }
                Color c = o[0] == "2" ? ShRed : o[0] == "1" ? ShAmber : o[0] == "0" ? ShGreen : ShDim;
                W(o[0] == "2" ? "[!!] " : o[0] == "1" ? "[!]  " : o[0] == "0" ? "[ok] " : "     ", c);
                W(ShareCell(o[1]), ShFg); W(": ", ShDim); WL(ShareCell(o[2]), o[0] == "2" ? ShRed : o[0] == "1" ? ShAmber : ShFg);
                if (o.Length > 3 && o[3] != "") WL("      " + ShareCell(o[3]), ShDim);
            }
        }

        // ---------- keys
        void ShKey(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; string line = shIn.Text; shIn.Clear(); ShRunLine(line); return; }
            if (e.KeyCode == Keys.C && e.Control && shIn.SelectionLength == 0) { e.SuppressKeyPress = true; if (shQueue.Count > 0) { shQueue.Clear(); WL("(script stopped)", ShAmber); } if (shRun != null) { shRun.Stop = true; WL("^C - stopping...", ShAmber); } else WL("^C", ShDim); ShEnd(); return; }
            if (e.KeyCode == Keys.L && e.Control) { e.SuppressKeyPress = true; shOut.Clear(); return; }
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true; if (shHist.Count == 0) return;
                shHi = e.KeyCode == Keys.Up ? (shHi < 0 ? shHist.Count - 1 : Math.Max(0, shHi - 1)) : (shHi < 0 ? -1 : shHi + 1);
                if (shHi >= shHist.Count) { shHi = -1; shIn.Clear(); return; }
                if (shHi >= 0) { shIn.Text = shHist[shHi]; shIn.SelectionStart = shIn.TextLength; }
                return;
            }
            if (e.KeyCode == Keys.Tab) { e.SuppressKeyPress = true; ShComplete(); return; }
            if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; shIn.Clear(); }
        }
        static readonly string[] ShCmds = { "help", "tools", "man", "open", "pages", "status", "debug", "log", "history", "clear", "copy", "save", "exit", "lab" };
        IEnumerable<string> ShWords() { return ShCmds.Concat(Tools2().Select(t => t.Id)); }
        IEnumerable<string> ShPages() { return Secs.Select(z => z[0]).Concat(tabNames.Keys).Concat(new[] { "calc", "reports", "lab", "settings", "about" }).Distinct(); }
        void ShComplete()
        {
            string t = shIn.Text; var parts = t.Split(' '); string last = parts[parts.Length - 1];
            IEnumerable<string> pool = parts.Length == 1 ? ShWords() : parts[0] == "open" || parts[0] == "cd" ? ShPages() : parts[0] == "man" || parts[0] == "help" ? Tools2().Select(x => x.Id) : Enumerable.Empty<string>();
            var hits = pool.Where(w => w.StartsWith(last, StringComparison.OrdinalIgnoreCase)).Distinct().OrderBy(w => w).ToList();
            if (hits.Count == 1) { parts[parts.Length - 1] = hits[0]; shIn.Text = string.Join(" ", parts) + " "; shIn.SelectionStart = shIn.TextLength; }
            else if (hits.Count > 1)
            {
                string common = hits[0]; foreach (var h in hits) while (!h.StartsWith(common, StringComparison.OrdinalIgnoreCase)) common = common.Substring(0, common.Length - 1);
                if (common.Length > last.Length) { parts[parts.Length - 1] = common; shIn.Text = string.Join(" ", parts); shIn.SelectionStart = shIn.TextLength; }
                else { WL(string.Join("  ", hits.Take(60)), ShDim); ShEnd(); }
            }
        }
        // "quoted words" stay one argument; a lone - is an empty box
        static List<string> ShSplit(string line)
        {
            var l = new List<string>(); var sb = new StringBuilder(); bool q = false;
            foreach (char c in line) { if (c == '"') { q = !q; continue; } if (c == ' ' && !q) { if (sb.Length > 0) { l.Add(sb.ToString()); sb.Clear(); } continue; } sb.Append(c); }
            if (sb.Length > 0) l.Add(sb.ToString());
            return l;
        }

        // ---------- commands
        void ShRunLine(string line)
        {
            W(Prompt() + " ", ShGreen); WL(line);
            line = (line ?? "").Trim(); if (line == "") { ShEnd(); return; }
            shHist.Remove(line); shHist.Add(line); if (shHist.Count > 200) shHist.RemoveAt(0); shHi = -1;
            var a = ShSplit(line); string cmd = a[0].ToLowerInvariant(); var args = a.Skip(1).ToList();
            ShHold(true);
            bool yes = args.Remove("--yes") | args.Remove("-y");
            try
            {
                switch (cmd)
                {
                    case "help": case "?": if (args.Count > 0) ShMan(args[0]); else ShHelp(); break;
                    case "tools": case "ls": ShTools(args.Count > 0 ? string.Join(" ", args) : ""); break;
                    case "man": if (args.Count == 0) WL("usage: man <tool>", ShAmber); else ShMan(args[0]); break;
                    case "open": case "cd": case "go": if (args.Count == 0) ShPagesList(); else ShOpen(args[0]); break;
                    case "pages": ShPagesList(); break;
                    case "lab": ShLab(args); break;
                    case "status": ShStatus(); break;
                    case "debug": ShDebug(); break;
                    case "log": ShLog(args.Count > 0 ? args[0] : "20"); break;
                    case "history": for (int i = 0; i < shHist.Count; i++) WL((i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(4) + "  " + shHist[i], ShDim); break;
                    case "clear": case "cls": shOut.Clear(); return;
                    case "copy": if (shLast == "") WL("nothing to copy yet - run a tool first", ShAmber); else { try { Clipboard.SetText(shLast); WL("copied the last result", ShGreen); } catch (Exception ce) { if (!(ce is System.Runtime.InteropServices.ExternalException || ce is System.Threading.ThreadStateException)) throw; WL("couldn't copy - another app holds the clipboard", ShRed); } } break;
                    case "save": ShSave(); break;
                    case "exit": case "quit": tab = "overview"; ShowTab(); SyncNav(); break;
                    case "source": if (args.Count == 0) WL("usage: source <file> - runs its lines one by one, then writes <file>.out.txt", ShAmber); else ShSource(string.Join(" ", args)); break;
                    case "alltools": ShAllTools(); break;
                    default:
                        var t = Tools2().FirstOrDefault(x => string.Equals(x.Id, cmd, StringComparison.OrdinalIgnoreCase));
                        if (t != null) ShTool(t, args, yes);
                        else
                        {
                            var near = ShWords().Where(w => w.StartsWith(cmd.Substring(0, Math.Min(2, cmd.Length)), StringComparison.OrdinalIgnoreCase)).Take(6).ToList();
                            WL("adeeboshell: " + cmd + ": command not found" + (near.Count > 0 ? " - did you mean: " + string.Join(", ", near) + "?" : "  (help lists the commands)"), ShRed);
                        }
                        break;
                }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; WL("error: " + e.Message, ShRed); Program.Log("shell: " + e); }
            finally { ShHold(false); }
            ShEnd();
        }
        // AdeeboLab from the shell: the start page, or straight into one of its drawings (lan --ask asks the devices again)
        void ShLab(List<string> args)
        {
            bool ask = args.Remove("--ask"); string v = args.Count > 0 ? args[0].ToLowerInvariant() : "";
            var views = new[] { "lan", "path", "wifi", "phone", "vpn" };
            if (v != "" && !views.Contains(v)) { WL("lab: no view \"" + v + "\" - try " + string.Join(", ", views), ShRed); return; }
            if (v == "path" && LabViews.Path(M) == null) { WL("lab path: run a trace or MTR first (e.g.  mtr 1.1.1.1 1), then  lab path", ShAmber); return; }
            WL(v == "" ? "Opening AdeeboLab..." : "Opening AdeeboLab: " + v + (ask ? " (asking the devices, about 8 s)" : "") + "...", ShDim);
            OpenLab(null, false, v == "" ? null : v, ask);
        }
        void ShHelp()
        {
            WL("COMMANDS", ShBlue);
            foreach (var c in new[] {
                new[] { "<tool> [args...]", "run a Toolbox tool; its boxes are the arguments, in order (\"quote\" spaces, - for an empty box)" },
                new[] { "<tool> ... --yes", "for tools that send test traffic: you confirm it's your network (the Toolbox's tick box)" },
                new[] { "tools [word]", "list every tool, or the ones that match (ls works too)" },
                new[] { "man <tool>", "what a tool does, what you type, how long, what it sends" },
                new[] { "open <page>", "go to a page: open markets, open sports, open calc (cd works too)" },
                new[] { "pages", "every page you can open" },
                new[] { "lab [view]", "AdeeboLab: lab (start page), lab lan [--ask], lab path, lab wifi, lab phone, lab vpn" },
                new[] { "status", "the NOW line: router, internet, names, this PC, the verdict" },
                new[] { "debug", "the raw values the pages tidy away: addresses, adapter, MACs, data folder" },
                new[] { "log [n]", "the last n lines of AdeeboMeter's own log (default 20)" },
                new[] { "copy / save", "the last result to the clipboard / to a file in Documents" },
                new[] { "history / clear / exit", "past commands / clear the screen (Ctrl+L) / back to Home" } })
            { W("  " + c[0].PadRight(24), ShCyan); WL(c[1], ShDim); }
            WL("Keys: Tab completes · Up/Down history · Ctrl+C stops a run · Ctrl+` opens the shell from anywhere", ShDim);
            WL("Example:  ping 1.1.1.1 50 200   ·   dig example.com MX   ·   subnet 10.1.2.3/22   ·   open markets", ShDim);
        }
        void ShTools(string word)
        {
            var list = Tools2().Where(t => word == "" || t.Id.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 || L.T(t.Title).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 || GroupName(t.Group).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            foreach (var g in list.GroupBy(t => GroupName(t.Group)))
            {
                WL(g.Key.ToUpperInvariant() + "  (" + g.Count() + ")", ShBlue);
                foreach (var t in g) { W("  " + t.Id.PadRight(14), ShCyan); WL(L.T(t.Title), ShFg); }
            }
            WL(list.Count + " tools" + (word != "" ? " match \"" + word + "\"" : "") + " - man <tool> for details", ShDim);
        }
        void ShMan(string id)
        {
            var t = Tools2().FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)); if (t == null) { WL("man: no tool called " + id + " (tools lists them)", ShRed); return; }
            var te = Explain.Term("tb." + t.Id); var e = te != null ? ToolTip(t.Id, te) : null;
            WL(t.Id + " - " + L.T(t.Title), ShCyan);
            if (e != null && !string.IsNullOrEmpty(e.Tag)) WL(e.Tag, ShFg);
            W("USAGE  ", ShBlue); W(t.Id, ShCyan); WL(t.Inputs.Length == 0 ? "" : " " + string.Join(" ", t.Inputs.Where(h => h != "").Select(h => "<" + BoxLabel(h).ToLowerInvariant().Replace(' ', '-') + ">")), ShFg);
            for (int i = 0; i < t.Inputs.Length; i++) if (t.Inputs[i] != "") WL("  " + (i + 1) + ". " + t.Inputs[i], ShDim);
            var ex = Examples.Get(t.Id); if (ex != null && ex.In.Length > 0 && ex.In.Any(x => x != "")) { W("EXAMPLE  ", ShBlue); WL(t.Id + " " + string.Join(" ", ex.In.Select(x => x == "" ? "-" : x.Contains(" ") ? "\"" + x + "\"" : x)), ShFg); }
            if (e != null && e.Facts != null) foreach (var f in e.Facts) { W(f[0].PadRight(9), ShBlue); WL(f[1], ShFg); }
            if (e != null && !string.IsNullOrEmpty(e.What)) { WL("WHAT IT DOES", ShBlue); WL("  " + e.What, ShFg); }
            if (e != null && !string.IsNullOrEmpty(e.Why)) { WL("WHY", ShBlue); WL("  " + e.Why, ShFg); }
            if (LabGate.Contains(t.Id) || SenderGate.Contains(t.Id) || t.Id == "sweep") WL("NOTE     sends test traffic - add --yes to confirm it's your network", ShAmber);
        }
        void ShPagesList()
        {
            WL("PAGES", ShBlue);
            foreach (var p in ShPages()) { string[] tn; W("  " + p.PadRight(12), ShCyan); WL(tabNames.TryGetValue(p, out tn) ? tn[0] : NavInfo.ContainsKey(p) ? NavName(p) : p, ShDim); }
        }
        void ShOpen(string p)
        {
            p = p.ToLowerInvariant(); string[] alias = { "home=home", "today=overview", "markets=trade", "market=trade", "trading=trade", "streaming=stream", "time=time", "machine=time", "wifi=link", "running=procs", "connections=conn", "problem=guide", "wrong=guide", "check=dest", "tool=toolbox", "tools=toolbox", "calculators=calc", "story=story", "game=gamer", "games=gamer" };
            foreach (var al in alias) { var kv = al.Split('='); if (p == kv[0]) p = kv[1]; }
            
            if (NavInfo.ContainsKey(p) || Secs.Any(z => z[0] == p)) { WL("opening " + NavName(p), ShGreen); OpenNav(p); return; }
            if (tabNames.ContainsKey(p) || p == "about") { WL("opening " + p, ShGreen); tab = p; ShowTab(); SyncNav(); return; }
            WL("open: no page called " + p + " (pages lists them)", ShRed);
        }
        void ShStatus()
        {
            var s = snap; Func<double, string> ms = v => double.IsNaN(v) ? "no answer" : v.ToString("0", CultureInfo.InvariantCulture) + " ms";
            Color lc = s.Level == 2 ? ShRed : s.Level == 1 ? ShAmber : ShGreen;
            WL(s.Headline, lc);
            W("router   ", ShBlue); WL(ms(M.GwPing.Avg(6)));
            W("internet ", ShBlue); WL(ms(M.NetPing.Avg(6)));
            W("names    ", ShBlue); WL(ms(M.Dns.Avg(6)));
            W("this PC  ", ShBlue); WL("CPU " + (double.IsNaN(s.Cpu) ? "?" : s.Cpu.ToString("0", CultureInfo.InvariantCulture) + "%") + " · memory " + (double.IsNaN(s.Mem) ? "?" : s.Mem.ToString("0", CultureInfo.InvariantCulture) + "%"));
            W("traffic  ", ShBlue); WL(Draw.Pair(M.Down.Last, M.Up.Last));
        }
        void ShDebug()
        {
            string ad, kind, ip, gw, gw6, dns, ssid, bssid, chan, link, vpn, ifid; int sig; bool vpnUp;
            lock (M.L) { ad = M.Adapter; kind = M.AdapterKind; ip = M.LocalIp; gw = M.Gateway; gw6 = M.Gateway6; dns = M.DnsServer; ssid = M.Ssid; bssid = M.Bssid; chan = M.WifiChannel; link = M.LinkSpeed; vpnUp = M.VpnUp; vpn = M.VpnName; ifid = M.IfaceId; sig = M.WifiSignal; }
            WL("RAW VALUES - on this PC only; the pages tidy or hide some of these", ShBlue);
            Action<string, string> kv = (k, v) => { W("  " + k.PadRight(14), ShCyan); WL(string.IsNullOrEmpty(v) ? "-" : v, ShFg); };
            kv("adapter", ad + " (" + kind + ")"); kv("interface id", ifid); kv("IPv4", ip); kv("gateway", gw); kv("gateway (v6)", gw6); kv("DNS", dns);
            kv("Wi-Fi", ssid == "" ? "(name hidden or not Wi-Fi)" : ssid); kv("BSSID", bssid); kv("signal", sig >= 0 ? sig + "%" : ""); kv("channel", chan); kv("link", link); kv("VPN", vpnUp ? "up · " + vpn : "off");
            kv("version", Program.Version); kv("data folder", Program.DataDir);
            using (var me = System.Diagnostics.Process.GetCurrentProcess()) { kv("memory", (me.PrivateMemorySize64 / 1048576) + " MB private · " + me.Threads.Count + " threads · " + me.HandleCount + " handles"); kv("running for", (DateTime.Now - me.StartTime).ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture)); }
            try { foreach (var n in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)) kv("nic", n.Name + " · " + n.NetworkInterfaceType + " · " + string.Join(":", n.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("x2", CultureInfo.InvariantCulture)))); }
            catch (System.Net.NetworkInformation.NetworkInformationException) { }
        }
        void ShLog(string nIn)
        {
            int n; if (!int.TryParse(nIn, out n)) n = 20; n = Math.Max(1, Math.Min(400, n));
            string p = Path.Combine(Program.WriteDir, "AdeeboMeter.log");
            if (!File.Exists(p)) { WL("no log yet", ShDim); return; }
            string[] lines; using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var sr = new StreamReader(fs)) lines = sr.ReadToEnd().Split('\n');
            foreach (var l in lines.Select(x => x.TrimEnd('\r')).Where(x => x != "").Skip(Math.Max(0, lines.Length - n - 1)))
                WL(l, l.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || l.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0 ? ShRed : l.IndexOf("slow", StringComparison.OrdinalIgnoreCase) >= 0 ? ShAmber : ShDim);
        }
        void ShSave()
        {
            if (shLast == "") { WL("nothing to save yet - run a tool first", ShAmber); return; }
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), f = Path.Combine(dir, "AdeeboShell-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt");
            try { File.WriteAllText(f, shLast); WL("saved: " + f, ShGreen); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; WL("couldn't save: " + e.Message, ShRed); }
        }

        // ---------- scripts (testing): "source file" runs its lines one after another - each waits for the one before, a tool
        // gets 30 s (then it's stopped) - and writes everything shown to file.out.txt, ending "== DONE"; "alltools" queues every
        // tool with its example inputs (documentation addresses become your router), the ones that need you left out
        readonly Queue<string> shQueue = new Queue<string>(); string shOutFile; Timer shQTimer; DateTime shStepAt;
        void ShSource(string file)
        {
            file = file.Trim().Trim('"'); if (!File.Exists(file)) { WL("source: no such file: " + file, ShRed); return; }
            foreach (var l in File.ReadAllLines(file)) { var t = l.Trim(); if (t != "" && !t.StartsWith("#", StringComparison.Ordinal)) shQueue.Enqueue(t); }
            shOutFile = file + ".out.txt"; WL("source: " + shQueue.Count + " lines queued", ShCyan); ShPump();
        }
        static readonly HashSet<string> ShSkip = new HashSet<string> { "newkey", "csrmake", "selfsigned", "encrypt", "decrypt", "recipients", "friend", "screentest", "keytest", "soundtest", "remote", "radiuslab", "wol", "keys", "mtu" };
        void ShAllTools()
        {
            string gw; lock (M.L) gw = M.Gateway ?? "";
            foreach (var t in Tools2())
            {
                if (ShSkip.Contains(t.Id)) continue; var ex = Examples.Get(t.Id); var inp = ex != null ? ex.In : new string[0];
                var words = inp.Select(v => System.Text.RegularExpressions.Regex.Replace(v ?? "", @"\b(192\.0\.2|198\.51\.100|203\.0\.113)\.\d{1,3}\b", gw == "" ? "$0" : gw)).Select(v => v == "" ? "-" : "\"" + v.Replace("\"", "") + "\"");
                shQueue.Enqueue((t.Id + " " + string.Join(" ", words)).Trim() + (LabGate.Contains(t.Id) || SenderGate.Contains(t.Id) || t.Id == "sweep" ? " --yes" : ""));
            }
            WL("alltools: " + shQueue.Count + " commands queued (Ctrl+C stops the current one)", ShCyan); if (shOutFile == null) shOutFile = Path.Combine(Program.WriteDir, "alltools.out.txt"); ShPump();
        }
        void ShPump()
        {
            if (shQTimer == null) { shQTimer = new Timer { Interval = 250 }; shQTimer.Tick += delegate { ShPump(); }; }
            if (shRun != null) { if ((DateTime.UtcNow - shStepAt).TotalSeconds > 30 && !shRun.Stop) { shRun.Stop = true; WL("(30 s - stopped by the script)", ShAmber); } if (!shQTimer.Enabled) shQTimer.Start(); return; }
            if (shQueue.Count == 0)
            {
                shQTimer.Stop();
                if (shOutFile != null) { try { File.WriteAllText(shOutFile, shOut.Text + "\n== DONE " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "\n"); WL("transcript: " + shOutFile, ShGreen); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; WL("couldn't write the transcript: " + e.Message, ShRed); } shOutFile = null; ShEnd(); }
                return;
            }
            shStepAt = DateTime.UtcNow; ShRunLine(shQueue.Dequeue()); if (!shQTimer.Enabled) shQTimer.Start();
        }

        // ---------- a tool: the same code the Toolbox runs, off the window thread
        void ShTool(Tool t, List<string> args, bool yes)
        {
            if (shRun != null) { WL("a tool is still running - Ctrl+C stops it", ShAmber); return; }
            if (t.Do == null) { WL(t.Id + " draws its result as a picture - opening it in the Toolbox", ShAmber); tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(t.Id); SyncNav(); return; }
            bool needs = t.Inputs.Length > 0 && t.Inputs[0] != "" && t.Inputs[0].IndexOf("optional", StringComparison.OrdinalIgnoreCase) < 0 && t.Inputs[0].IndexOf("empty", StringComparison.OrdinalIgnoreCase) < 0;
            if (needs && args.Count == 0) { WL("usage: " + t.Id + " " + string.Join(" ", t.Inputs.Where(h => h != "").Select(h => "<" + BoxLabel(h).ToLowerInvariant().Replace(' ', '-') + ">")), ShAmber); var ex0 = Examples.Get(t.Id); if (ex0 != null && ex0.In.Any(z => z != "")) WL("e.g.   " + t.Id + " " + string.Join(" ", ex0.In.Select(z => z == "" ? "-" : z.Contains(" ") ? "\"" + z + "\"" : z)), ShDim); WL("man " + t.Id + " says more", ShDim); return; }   // 22 Nov: "ping" alone said "couldn't find  (IPv4)"
            var a = new string[Math.Max(4, t.Inputs.Length)];
            for (int i = 0; i < a.Length; i++) a[i] = i < args.Count && args[i] != "-" ? args[i] : "";
            // more words than boxes: the rest belong to the last box ("dig example.com MX" fits; a sentence for a free-text box too)
            if (t.Inputs.Length > 0 && args.Count > t.Inputs.Length) a[t.Inputs.Length - 1] = string.Join(" ", args.Skip(t.Inputs.Length - 1));
            if (!UrlTools.Contains(t.Id)) for (int i = 0; i < t.Inputs.Length; i++) if (!SecretInput(t.Inputs[i]) && !PayloadBox(t.Id, i)) a[i] = Toolkit.UrlToTarget(a[i] ?? "", t.Inputs[i].IndexOf(":port", StringComparison.OrdinalIgnoreCase) >= 0);
            if (t.Id == "sweep" || LabGate.Contains(t.Id)) a[3] = yes ? "yes" : "";
            string sender = SenderGate.Contains(t.Id) ? (yes ? "yes" : "") : null;
            W("running " + t.Id, ShDim); WL(" ... (Ctrl+C stops)", ShDim); ShEnd();
            var run = new Toolkit.Run(); shRun = run; var sw = System.Diagnostics.Stopwatch.StartNew();
            Task.Factory.StartNew(() =>
            {
                List<string[]> res;
                try
                {
                    if (sender != null) { var gate = new List<string[]>(); res = Lab.Allowed(M, sender, gate) ? gate.Concat(t.Do(a, run) ?? new List<string[]>()).ToList() : gate; }
                    else res = t.Do(a, run) ?? new List<string[]>();
                }
                catch (Exception e) { Program.Log("shell tool " + t.Id + ": " + e); res = new List<string[]> { new[] { "2", "Couldn't finish", Draw.Trunc(e.Message, 100), "", "u" } }; }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        shRun = null; if (shOut == null || shOut.IsDisposed) return;
                        ShHold(true);
                        if (t.Id == "mtr" && mtrHops != null)
                        {
                            WL("hop     router                                   loss  sent  avg   best  worst  jitter", ShBlue);
                            foreach (var hp in mtrHops.ToList()) { double loss = hp.Sent > 0 ? 100.0 * hp.Lost / hp.Sent : 0, avg = hp.Sent - hp.Lost > 0 ? hp.Sum / (hp.Sent - hp.Lost) : double.NaN; WL(hp.N.ToString(CultureInfo.InvariantCulture).PadRight(8) + (hp.Ip == "" ? "*" : hp.Ip).PadRight(40) + (loss.ToString("0", CultureInfo.InvariantCulture) + "%").PadLeft(5) + hp.Sent.ToString(CultureInfo.InvariantCulture).PadLeft(6) + PathTools.F(avg).PadLeft(6) + PathTools.F(hp.Best).PadLeft(6) + PathTools.F(hp.Worst).PadLeft(7) + PathTools.F(hp.Jit).PadLeft(8), loss > 20 ? ShAmber : ShFg); }
                        }
                        ShRows(res);
                        var sb = new StringBuilder(); sb.AppendLine("AdeeboMeter " + Program.Version + " · " + L.T(t.Title) + " · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                        foreach (var o in res.Where(x => !IsSecretRow(x))) { string lv = o[0] == "2" ? "[!!] " : o[0] == "1" ? "[!]  " : o[0] == "0" ? "[ok] " : "     "; sb.AppendLine(lv + (o[1] == "" ? "" : ShareCell(o[1]) + ": ") + ShareCell(o[2])); if (o.Length > 3 && o[3] != "") sb.AppendLine("      " + ShareCell(o[3])); }
                        shLast = sb.ToString();
                        WL((run.Stop ? "stopped" : "done") + " in " + sw.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s", ShDim);
                        if ((LabGate.Contains(t.Id) || SenderGate.Contains(t.Id) || t.Id == "sweep") && !yes) WL("(it sends test traffic: add --yes if this is your network)", ShAmber);
                        ShHold(false); ShEnd();
                    });
                }
                catch (InvalidOperationException) { shRun = null; }
            });
        }
    }
}
