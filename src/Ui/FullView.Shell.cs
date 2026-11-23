using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // AdeeboShell, take 2 (Adeeb, 22 Nov: "it should feel like Linux - cd into folders, ls, live output, scripts that run five at
    // once and merge, decrypt files - limitless, but only what AdeeboMeter itself can do, nothing external").
    //
    //   /            home -> your Windows profile folder, data -> AdeeboMeter's folder, mnt -> your drives,
    //                pages -> every page (cd pages/markets; open), tools -> every tool by group (cd tools/path; ls; mtr 1.1.1.1)
    //   files        ls cd pwd cat head tail grep sort uniq wc cp mv mkdir rm touch find tree, > >> < redirects, | pipes, * globs
    //   control      ; && || &, jobs wait kill, for/while/if in scripts, variables, $? $1, read, test, seq, sleep, time
    //   power        parallel -j 5 ping {} ::: 1.1.1.1 8.8.8.8 (runs together, merges as each finishes), source / ./script.sh
    //
    // It never starts another program - every command is AdeeboMeter's own code. Files are read and written where you point it.
    sealed partial class FullView
    {
        Panel shellPage; TermView term;
        static readonly Color ShBg = Color.FromArgb(12, 16, 22), ShFg = Color.FromArgb(201, 209, 217), ShDim = Color.FromArgb(125, 133, 144),
            ShGreen = Color.FromArgb(63, 220, 110), ShAmber = Color.FromArgb(230, 170, 50), ShRed = Color.FromArgb(248, 81, 73), ShCyan = Color.FromArgb(57, 207, 220), ShBlue = Color.FromArgb(88, 166, 255), ShPurple = Color.FromArgb(188, 140, 255);
        readonly List<string> shHist = new List<string>(); int shHi = -1; string shDraft; string shLast = "";
        string shCwd = "/home", shPrev = "/home"; readonly Dictionary<string, string> shVars = new Dictionary<string, string>(StringComparer.Ordinal);
        List<Tool> shTools; int shLastExit;
        volatile ShJob shFg; readonly List<ShJob> shJobs = new List<ShJob>(); int shJobN;
        volatile ManualResetEvent shAskWait; string shAskAnswer; bool shTabTwice;
        bool ShBusy { get { return shFg != null || shJobs.Any(j => !j.Done); } }   // the window's Esc guard (FullView.cs)

        sealed class ShJob { public int Id; public string Cmd = ""; public volatile bool Cancel, Done; public bool Bg; public int Exit; public readonly List<Toolkit.Run> Runs = new List<Toolkit.Run>(); public readonly StringBuilder Saved = new StringBuilder(); public DateTime CancelAt = DateTime.MinValue; }
        // where output goes: the screen (coloured), or text for the next command in a pipe / a file
        sealed class ShOut { public StringBuilder Cap; public ShJob Job; public bool Screen { get { return Cap == null; } } }
        sealed class ShCtx { public ShJob Job; public ShOut Out; public string Stdin; public List<string> Args = new List<string>(); public bool Yes, Script; public ShCtx With(ShOut o, string stdin) { return new ShCtx { Job = Job, Out = o, Stdin = stdin, Args = Args, Yes = Yes, Script = Script }; } }

        Panel BuildShellPage()
        {
            shellPage = new Panel { Dock = DockStyle.Fill, BackColor = ShBg, Visible = false };
            var mono = new Font(IsFont("Cascadia Mono") ? "Cascadia Mono" : IsFont("Consolas") ? "Consolas" : FontFamily.GenericMonospace.Name, 10.5f);
            term = new TermView(mono) { Dock = DockStyle.Fill, Bg = ShBg, Fg = ShFg };
            term.TermKey += ShKey; term.TermChar += ShChar; term.Paste = ShPaste;
            shellPage.Controls.Add(term);
            shellPage.VisibleChanged += delegate { if (shellPage.Visible) { if (shTools == null) { shTools = Tools2(); ThreadPool.QueueUserWorkItem(delegate { ShStarterScripts(); }); } if (term.AllText().Trim() == "") ShWelcome(); ShPrompt(); BeginInvoke((Action)delegate { term.Focus(); }); } };
            return shellPage;
        }
        static bool IsFont(string n) { using (var f = new Font(n, 10f)) return f.Name == n; }
        static string ShUser { get { return Environment.UserName.ToLowerInvariant(); } }
        static string ShHost { get { return Environment.MachineName.ToLowerInvariant(); } }
        string ShShort(string v) { return v == "/home" ? "~" : v.StartsWith("/home/", StringComparison.Ordinal) ? "~" + v.Substring(5) : v; }
        void ShPrompt()
        {
            term.PromptSegs = new List<TermView.Seg> { new TermView.Seg(ShUser + "@" + ShHost, ShGreen), new TermView.Seg(":", ShFg), new TermView.Seg(ShShort(shCwd), ShBlue), new TermView.Seg("$ ", ShFg) };
            term.Title = ShUser + "@" + ShHost + ": " + ShShort(shCwd) + "  —  AdeeboShell"; term.Secret = false;
            term.ShowInput = shFg == null || shAskWait != null; term.Invalidate();
        }
        void ShWelcome()
        {
            term.Write("  █▀█ █▀▄ █▀▀ █▀▀ █▄▄ █▀█   " + "AdeeboShell · AdeeboMeter " + Program.Version + "\n", ShCyan);
            term.Write("  █▀█ █▄▀ ██▄ ██▄ █▄█ █▄█   " + "every tool is a command, every page is a folder\n", ShCyan);
            term.Write("\n", ShFg);
            term.Write("  ls · cd pages · cd ~ · mtr 1.1.1.1 · parallel -j 5 ping {} 4 ::: 1.1.1.1 8.8.8.8 9.9.9.9\n", ShDim);
            term.Write("  help for everything · Tab completes · Ctrl+C stops · nothing here starts another program\n\n", ShDim);
        }

        // ---------- the screen, from any thread
        void SW(string s, Color c) { Ui(delegate { term.Write(s, c); }); }
        void Ui(Action a) { try { if (IsDisposed) return; if (InvokeRequired) BeginInvoke(a); else a(); } catch (InvalidOperationException) { } }
        void O(ShCtx c, string s, Color col) { if (c.Out.Screen) { if (c.Job != null && c.Job.Bg) { lock (c.Job.Saved) c.Job.Saved.Append(s); } else SW(s, col); } else lock (c.Out.Cap) c.Out.Cap.Append(s); }
        void OL(ShCtx c, string s, Color col) { O(c, s + "\n", col); }
        void OL(ShCtx c, string s) { OL(c, s, ShFg); }
        void Err(ShCtx c, string s) { OL(c, s, ShRed); }

        // ---------- keys
        void ShChar(object s, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;
            e.Handled = true; term.ClearSel(); term.Follow();
            term.Input = term.Input.Insert(term.Caret, e.KeyChar.ToString()); term.Caret++; shTabTwice = false; term.Invalidate();
        }
        void ShPaste()
        {
            string t; try { t = Clipboard.GetText(); } catch (Exception e) { if (!(e is System.Runtime.InteropServices.ExternalException || e is ThreadStateException)) throw; return; }
            if (string.IsNullOrEmpty(t)) return; t = t.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            var lines = t.Split('\n').Where(z => z.Trim() != "" && !z.TrimStart().StartsWith("#", StringComparison.Ordinal)).ToList();
            if (lines.Count <= 1 || shAskWait != null || shFg != null) { string one = string.Join(" ", lines); term.Input = term.Input.Insert(term.Caret, one); term.Caret += one.Length; term.Invalidate(); return; }
            // a pasted script: run as one block (a for ... done across lines works), shown as you'd see it typed
            string block = (term.Input.Trim() != "" ? term.Input + "\n" : "") + string.Join("\n", lines); term.Input = ""; term.Caret = 0; ShSubmit(block);
        }
        void ShKey(object s, KeyEventArgs e)
        {
            var k = e.KeyCode; string inp = term.Input; int c = term.Caret;
            if (k != Keys.Tab) shTabTwice = false;
            if (e.Control && k == Keys.C) { e.SuppressKeyPress = true; if (term.Copy()) return; ShInterrupt(); return; }
            if (e.Control && k == Keys.V) { e.SuppressKeyPress = true; ShPaste(); return; }
            if (e.Control && k == Keys.L) { e.SuppressKeyPress = true; term.Clear(); ShPrompt(); return; }
            if (e.Control && k == Keys.U) { e.SuppressKeyPress = true; term.Input = inp.Substring(c); term.Caret = 0; term.Invalidate(); return; }
            if (e.Control && k == Keys.K) { e.SuppressKeyPress = true; term.Input = inp.Substring(0, c); term.Invalidate(); return; }
            if (e.Control && k == Keys.A) { e.SuppressKeyPress = true; term.Caret = 0; term.Invalidate(); return; }
            if (e.Control && k == Keys.E) { e.SuppressKeyPress = true; term.Caret = inp.Length; term.Invalidate(); return; }
            if (e.Control && (k == Keys.W || k == Keys.Back)) { e.SuppressKeyPress = true; int a = c; while (a > 0 && inp[a - 1] == ' ') a--; while (a > 0 && inp[a - 1] != ' ' && inp[a - 1] != '/') a--; term.Input = inp.Remove(a, c - a); term.Caret = a; term.Invalidate(); return; }
            if (e.Shift && (k == Keys.PageUp || k == Keys.PageDown) || k == Keys.PageUp || k == Keys.PageDown) { e.SuppressKeyPress = true; term.ScrollPage(k == Keys.PageUp ? 1 : -1); return; }
            switch (k)
            {
                case Keys.Enter: e.SuppressKeyPress = true; ShEnter(); return;
                case Keys.Back: e.SuppressKeyPress = true; if (c > 0) { term.Input = inp.Remove(c - 1, 1); term.Caret--; term.Invalidate(); } return;
                case Keys.Delete: e.SuppressKeyPress = true; if (c < inp.Length) { term.Input = inp.Remove(c, 1); term.Invalidate(); } return;
                case Keys.Left: e.SuppressKeyPress = true; if (c > 0) { term.Caret = e.Control ? Math.Max(0, inp.LastIndexOf(' ', Math.Max(0, c - 2)) + 1) : c - 1; term.Invalidate(); } return;
                case Keys.Right: e.SuppressKeyPress = true; if (c < inp.Length) { if (e.Control) { int n = inp.IndexOf(' ', c + 1); term.Caret = n < 0 ? inp.Length : n; } else term.Caret = c + 1; term.Invalidate(); } return;
                case Keys.Home: e.SuppressKeyPress = true; term.Caret = 0; term.Invalidate(); return;
                case Keys.End: e.SuppressKeyPress = true; term.Caret = inp.Length; term.Invalidate(); return;
                case Keys.Escape: e.SuppressKeyPress = true; term.Input = ""; term.Caret = 0; term.Invalidate(); return;
                case Keys.Tab: e.SuppressKeyPress = true; if (shAskWait == null) ShComplete(); return;
                case Keys.Up: case Keys.Down:
                    e.SuppressKeyPress = true; if (shAskWait != null || shHist.Count == 0) return;
                    if (shHi < 0 && k == Keys.Up) shDraft = inp;
                    shHi = k == Keys.Up ? (shHi < 0 ? shHist.Count - 1 : Math.Max(0, shHi - 1)) : (shHi < 0 ? -1 : shHi + 1);
                    if (shHi >= shHist.Count || shHi < 0) { shHi = -1; term.Input = shDraft ?? ""; shDraft = null; } else term.Input = shHist[shHi];
                    term.Caret = term.Input.Length; term.Invalidate(); return;
            }
        }
        void ShInterrupt()
        {
            var fg = shFg;
            if (shAskWait != null) { shAskAnswer = null; var w = shAskWait; shAskWait = null; term.Write(term.Input + "^C\n", ShDim); term.Input = ""; term.Caret = 0; w.Set(); return; }
            if (fg != null)
            {
                if (fg.Cancel && (DateTime.UtcNow - fg.CancelAt).TotalSeconds > 2) { term.EndLive(); term.Write("^C again - let go of it (it ends in the background; its result is dropped)\n", ShAmber); ShFinishFg(fg, 130); return; }
                fg.Cancel = true; fg.CancelAt = DateTime.UtcNow; lock (fg.Runs) foreach (var r in fg.Runs) r.Stop = true; term.Write("^C\n", ShAmber); return;
            }
            term.Write(term.Input + "^C\n", ShDim); term.Input = ""; term.Caret = 0; ShPrompt();
        }
        void ShEnter()
        {
            string line = term.Input; term.Input = ""; term.Caret = 0; term.Follow(); term.ClearSel();
            ShSubmit(line);
        }
        void ShSubmit(string line)
        {
            term.Follow(); term.ClearSel();
            if (shAskWait != null) { term.Write(term.Secret ? new string('*', line.Length) + "\n" : line + "\n", ShFg); shAskAnswer = line; var w = shAskWait; shAskWait = null; term.Secret = false; term.ShowInput = false; term.Invalidate(); w.Set(); return; }
            if (shFg != null) return;   // a command is running: Ctrl+C first
            // the line as typed goes on the screen right after the prompt (secrets as ****)
            var p = term.PromptSegs; foreach (var sg in p) term.Write(sg.T, sg.C);
            string shown = string.Join("\n", line.Split('\n').Select(ShMasked)); term.Write(shown.Replace("\n", "\n> ") + "\n", ShFg);
            line = line.Trim(); shHi = -1; shDraft = null;
            if (line == "") { ShPrompt(); return; }
            shown = shown.Replace("\n", " ; ").Replace("do ;", "do").Replace("then ;", "then").Replace("else ;", "else");   // history keeps it on one line
            shHist.Remove(shown); shHist.Add(shown); if (shHist.Count > 500) shHist.RemoveAt(0);
            var job = new ShJob { Id = 0, Cmd = shown }; shFg = job; term.ShowInput = false; term.Invalidate();
            var t = new Thread(() =>
            {
                int ex = 0;
                try { ex = ShRunText(line, new ShCtx { Job = job, Out = new ShOut { Job = job } }); }
                catch (Exception e2) { if (e2 is OutOfMemoryException) throw; Program.Log("shell: " + e2); SW("shell: " + e2.Message + "\n", ShRed); ex = 2; }
                Ui(delegate { if (shFg == job) ShFinishFg(job, ex); });
            }) { IsBackground = true, Name = "adeeboshell" };
            t.Start();
        }
        void ShFinishFg(ShJob job, int ex)
        {
            job.Done = true; job.Exit = ex; shLastExit = ex; if (shFg == job) shFg = null;
            term.EndLive(); if (!term.AtLineStart) term.Write("\n", ShFg);
            ShPrompt();
        }
        // a question from a running command (y/N, read): the answer comes back on the next Enter
        string ShAsk(string q, bool secret)
        {
            var w = new ManualResetEvent(false); shAnswerReset();
            Ui(delegate { term.EndLive(); term.Write(q, ShAmber); term.PromptSegs = new List<TermView.Seg>(); term.Secret = secret; shAskWait = w; term.ShowInput = true; term.Input = ""; term.Caret = 0; term.Invalidate(); });
            w.WaitOne(); using (w) { }
            Ui(delegate { term.Secret = false; ShPromptKeepHidden(); });
            return shAskAnswer;
        }
        void shAnswerReset() { shAskAnswer = null; }
        void ShPromptKeepHidden() { term.PromptSegs = new List<TermView.Seg> { new TermView.Seg(ShUser + "@" + ShHost, ShGreen), new TermView.Seg(":", ShFg), new TermView.Seg(ShShort(shCwd), ShBlue), new TermView.Seg("$ ", ShFg) }; term.ShowInput = shFg == null; term.Invalidate(); }

        // ---------- secrets never shown (shared secret, community, password, code boxes)
        string ShMasked(string line)
        {
            var a = Lex(line ?? ""); if (a.Count < 2 || a[0].Op != null || shTools == null) return line ?? "";
            var t = shTools.FirstOrDefault(x => string.Equals(x.Id, a[0].W, StringComparison.OrdinalIgnoreCase)); if (t == null) return line;
            bool any = false; int box = 0;
            for (int i = 1; i < a.Count; i++) { if (a[i].Op != null) break; if (a[i].W == "--yes" || a[i].W == "-y") continue; if (box < t.Inputs.Length && SecretInput(t.Inputs[box]) && a[i].W != "" && a[i].W != "-") { a[i].W = "****"; a[i].Q = false; any = true; } box++; }
            return any ? string.Join(" ", a.Select(z => z.Op ?? (z.Q || z.W.Contains(" ") ? "\"" + z.W + "\"" : z.W))) : line;
        }

        // =====================================================================================================================
        // the language: words, operators, and the structures scripts use (for / while / if)
        sealed class Tok { public string W, Op; public bool Q, SQ; public Tok Copy() { return new Tok { W = W, Op = Op, Q = Q, SQ = SQ }; } }
        static List<Tok> Lex(string s)
        {
            var l = new List<Tok>(); var sb = new StringBuilder(); bool had = false, q = false, sq = false; char quote = '\0';
            Action flush = () => { if (sb.Length > 0 || had) l.Add(new Tok { W = sb.ToString(), Q = q, SQ = sq }); sb.Clear(); had = false; q = false; sq = false; };
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (quote != '\0') { if (ch == quote) { quote = '\0'; continue; } if (ch == '\\' && quote == '"' && i + 1 < s.Length && (s[i + 1] == '"' || s[i + 1] == '\\' || s[i + 1] == '$')) { sb.Append(s[++i]); continue; } if (quote == '\'' && ch == '$') { sb.Append('\u0001'); continue; } sb.Append(ch); continue; }
                if (ch == '"' || ch == '\'') { quote = ch; had = true; q = true; if (ch == '\'') sq = true; continue; }
                if (ch == '\\' && i + 1 < s.Length) { sb.Append(s[++i]); had = true; q = true; continue; }
                if (ch == '#' && sb.Length == 0 && !had) break;   // a comment
                if (ch == ' ' || ch == '\t') { flush(); continue; }
                if (ch == '\n') { flush(); l.Add(new Tok { Op = ";" }); continue; }
                string two = i + 1 < s.Length ? s.Substring(i, 2) : "";
                if (two == "&&" || two == "||" || two == ">>") { flush(); l.Add(new Tok { Op = two }); i++; continue; }
                if (ch == '|' || ch == ';' || ch == '&' || ch == '>' || ch == '<') { flush(); l.Add(new Tok { Op = ch.ToString() }); continue; }
                sb.Append(ch);
            }
            flush(); return l;
        }
        abstract class Node { }
        sealed class NCmd : Node { public List<Tok> Words = new List<Tok>(); }
        sealed class NPipe : Node { public List<Node> Cmds = new List<Node>(); public bool Bg; }   // a stage: a command, or a whole for/while/if (for ... done | sort)
        sealed class NAndOr : Node { public List<Node> Parts = new List<Node>(); public List<string> Ops = new List<string>(); }
        sealed class NSeq : Node { public List<Node> Items = new List<Node>(); }
        sealed class NFor : Node { public string Var; public List<Tok> In = new List<Tok>(); public NSeq Body; }
        sealed class NWhile : Node { public NSeq Cond, Body; }
        sealed class NIf : Node { public List<NSeq> Conds = new List<NSeq>(), Bodies = new List<NSeq>(); public NSeq Else; }
        sealed class Parser
        {
            readonly List<Tok> t; int i;
            public Parser(List<Tok> toks) { t = toks; }
            bool Sep() { return i < t.Count && t[i].Op == ";"; }
            bool Kw(params string[] k) { return i < t.Count && t[i].Op == null && !t[i].Q && k.Contains(t[i].W); }
            string Expect(params string[] k) { if (!Kw(k)) throw new FormatException("expected " + string.Join(" or ", k) + (i < t.Count ? " near '" + (t[i].Op ?? t[i].W) + "'" : " at the end")); return t[i++].W; }
            public NSeq List(params string[] stop)
            {
                var s = new NSeq();
                while (true)
                {
                    while (Sep()) i++;
                    if (i >= t.Count || Kw(stop)) return s;
                    s.Items.Add(Statement());
                    if (i < t.Count && t[i].Op == "&") { var last = s.Items[s.Items.Count - 1]; var p = last as NPipe; if (p != null) p.Bg = true; else s.Items[s.Items.Count - 1] = new NPipe { Bg = true, Cmds = { new NCmd { Words = { new Tok { W = "\u0002" } } } } }; i++; if (last is NAndOr) throw new FormatException("& after && or || isn't supported - put it in a script"); }
                }
            }
            Node Statement()
            {
                var ao = new NAndOr(); ao.Parts.Add(Pipe());
                while (i < t.Count && (t[i].Op == "&&" || t[i].Op == "||")) { ao.Ops.Add(t[i++].Op); while (Sep()) i++; ao.Parts.Add(Pipe()); }
                return ao.Parts.Count == 1 ? ao.Parts[0] : ao;
            }
            Node Compound()
            {
                if (Kw("for"))
                {
                    i++; var f = new NFor(); if (i >= t.Count || t[i].Op != null) throw new FormatException("for needs a name"); f.Var = t[i++].W;
                    if (Kw("in")) { i++; while (i < t.Count && t[i].Op == null && !Kw("do")) f.In.Add(t[i++]); }
                    while (Sep()) i++; Expect("do"); f.Body = List("done"); Expect("done"); return f;
                }
                if (Kw("while")) { i++; var w = new NWhile { Cond = List("do") }; Expect("do"); w.Body = List("done"); Expect("done"); return w; }
                if (Kw("if"))
                {
                    i++; var n = new NIf(); n.Conds.Add(List("then")); Expect("then"); n.Bodies.Add(List("elif", "else", "fi"));
                    while (Kw("elif")) { i++; n.Conds.Add(List("then")); Expect("then"); n.Bodies.Add(List("elif", "else", "fi")); }
                    if (Kw("else")) { i++; n.Else = List("fi"); }
                    Expect("fi"); return n;
                }
                return Cmd();
            }
            NPipe Pipe()
            {
                var p = new NPipe(); p.Cmds.Add(Compound());
                while (i < t.Count && t[i].Op == "|") { i++; while (Sep()) i++; p.Cmds.Add(Compound()); }
                return p;
            }
            NCmd Cmd()
            {
                var c = new NCmd();
                while (i < t.Count && (t[i].Op == null || t[i].Op == ">" || t[i].Op == ">>" || t[i].Op == "<"))
                {
                    if (t[i].Op != null) { var op = t[i++]; if (i >= t.Count || t[i].Op != null) throw new FormatException(op.Op + " needs a file name"); c.Words.Add(op); c.Words.Add(t[i++]); continue; }
                    c.Words.Add(t[i++]);
                }
                if (c.Words.Count == 0) throw new FormatException("a command is missing" + (i < t.Count ? " before '" + (t[i].Op ?? t[i].W) + "'" : ""));
                return c;
            }
        }
        int ShRunText(string text, ShCtx c)
        {
            NSeq prog;
            try { var p = new Parser(Lex(text)); prog = p.List(); }
            catch (FormatException fe) { Err(c, "adeeboshell: " + fe.Message); return 2; }
            return Exec(prog, c);
        }
        int Exec(Node n, ShCtx c)
        {
            if (c.Job.Cancel) return 130;
            var seq = n as NSeq; if (seq != null) { int r = 0; foreach (var it in seq.Items) { if (c.Job.Cancel) return 130; r = Exec(it, c); shLastExit = r; } return r; }
            var ao = n as NAndOr;
            if (ao != null) { int r = Exec(ao.Parts[0], c); for (int k = 0; k < ao.Ops.Count; k++) { if ((ao.Ops[k] == "&&") == (r == 0)) r = Exec(ao.Parts[k + 1], c); shLastExit = r; } return r; }
            var f = n as NFor;
            if (f != null) { int r = 0; foreach (var w in Expand(f.In, c)) { if (c.Job.Cancel) return 130; lock (shVars) shVars[f.Var] = w; r = Exec(f.Body, c); } return r; }
            var wh = n as NWhile;
            if (wh != null) { int r = 0, guard = 0; while (!c.Job.Cancel && Exec(wh.Cond, c) == 0) { r = Exec(wh.Body, c); if (++guard > 100000) { Err(c, "while: stopped after 100,000 rounds"); return 1; } } return r; }
            var nf = n as NIf;
            if (nf != null) { for (int k = 0; k < nf.Conds.Count; k++) if (Exec(nf.Conds[k], c) == 0) return Exec(nf.Bodies[k], c); return nf.Else != null ? Exec(nf.Else, c) : 0; }
            var p = n as NPipe; if (p == null) return 0;
            if (p.Bg) return ShBackground(p, c);
            if (p.Cmds.Count == 1) return Stage(p.Cmds[0], c);
            string stdin = c.Stdin; int ex = 0;
            for (int k = 0; k < p.Cmds.Count; k++)
            {
                bool last = k == p.Cmds.Count - 1; var o = last ? c.Out : new ShOut { Cap = new StringBuilder(), Job = c.Job };
                ex = Stage(p.Cmds[k], c.With(o, stdin)); if (!last) stdin = o.Cap.ToString();
                if (c.Job.Cancel) return 130;
            }
            return ex;
        }
        int Stage(Node n, ShCtx c) { var cmd = n as NCmd; return cmd != null ? RunCmd(cmd, c) : Exec(n, c); }
        int ShBackground(NPipe p, ShCtx c)
        {
            var job = new ShJob { Id = Interlocked.Increment(ref shJobN), Bg = true, Cmd = "(job)" }; lock (shJobs) shJobs.Add(job);
            var fg = new NPipe { Cmds = p.Cmds };
            OL(c, "[" + job.Id + "] started", ShDim);
            new Thread(() =>
            {
                int ex = 0; try { ex = Exec(fg, new ShCtx { Job = job, Out = new ShOut { Job = job }, Args = c.Args, Yes = c.Yes, Script = true }); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; lock (job.Saved) job.Saved.Append("error: " + e.Message + "\n"); ex = 2; }
                job.Exit = ex; job.Done = true;
                string outp; lock (job.Saved) outp = job.Saved.ToString();
                Ui(delegate { bool hidden = shFg == null; term.EndLive(); if (!term.AtLineStart) term.Write("\n", ShFg); term.Write("[" + job.Id + "] done (exit " + ex + ")\n", ex == 0 ? ShGreen : ShAmber); if (outp != "") term.Write(outp.EndsWith("\n", StringComparison.Ordinal) ? outp : outp + "\n", ShFg); if (hidden) ShPrompt(); });
            }) { IsBackground = true, Name = "adeeboshell job" }.Start();
            return 0;
        }

        // words -> the strings a command gets: $VARS, $?, $1, ~, and * globs (not inside quotes)
        List<string> Expand(List<Tok> words, ShCtx c)
        {
            var r = new List<string>();
            foreach (var w in words)
            {
                if (w.Op != null) { r.Add("\u0003" + w.Op); continue; }
                string s = w.W;
                bool hadVar = !w.SQ && s.Contains("$");
                if (!w.SQ) s = Regex.Replace(s, @"\$(\?|#|@|\d|\{[A-Za-z_][A-Za-z0-9_]*\}|[A-Za-z_][A-Za-z0-9_]*)", m => Var(m.Groups[1].Value.Trim('{', '}'), c));
                if (hadVar && !w.Q) { var parts = s.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries); if (parts.Length != 1) { r.AddRange(parts); continue; } }   // HOSTS="a b c"; for h in $HOSTS - split like bash
                s = s.Replace('\u0001', '$');
                if (!w.Q && (s == "~" || s.StartsWith("~/", StringComparison.Ordinal))) s = "/home" + s.Substring(1);
                if (!w.Q && (s.Contains("*") || s.Contains("?")) && !s.Contains("://")) { var g = Glob(s); if (g.Count > 0) { r.AddRange(g); continue; } }
                r.Add(s);
            }
            return r;
        }
        string Var(string n, ShCtx c)
        {
            if (n == "?") return shLastExit.ToString(CultureInfo.InvariantCulture);
            if (n == "#") return c.Args.Count.ToString(CultureInfo.InvariantCulture);
            if (n == "@") return string.Join(" ", c.Args);
            int k; if (int.TryParse(n, out k)) return k >= 1 && k <= c.Args.Count ? c.Args[k - 1] : "";
            string v; lock (shVars) if (shVars.TryGetValue(n, out v)) return v;
            switch (n) { case "HOME": return "/home"; case "PWD": return shCwd; case "USER": return ShUser; case "HOSTNAME": return ShHost; case "GATEWAY": lock (M.L) return M.Gateway ?? ""; case "RANDOM": return new Random().Next(32768).ToString(CultureInfo.InvariantCulture); }
            return "";
        }
        List<string> Glob(string pat)
        {
            var r = new List<string>(); string dirV = pat.Contains("/") ? pat.Substring(0, pat.LastIndexOf('/')) : "", name = pat.Substring(pat.LastIndexOf('/') + 1);
            if (dirV.Contains("*") || dirV.Contains("?")) return r;
            string real = Real(Abs(dirV == "" ? "." : dirV == "" ? "/" : dirV)); if (real == null || !Directory.Exists(real)) return r;
            var rx = new Regex("^" + Regex.Escape(name).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
            try { foreach (var e in Directory.EnumerateFileSystemEntries(real).Select(Path.GetFileName).Where(z => rx.IsMatch(z) && (name.StartsWith(".", StringComparison.Ordinal) || !z.StartsWith(".", StringComparison.Ordinal))).OrderBy(z => z, StringComparer.OrdinalIgnoreCase)) r.Add((dirV == "" ? "" : dirV + "/") + e); }
            catch (Exception ex) { if (!(ex is IOException || ex is UnauthorizedAccessException)) throw; }
            return r;
        }

        // =====================================================================================================================
        // the folders: virtual paths -> real ones
        string Abs(string p)
        {
            p = (p ?? "").Replace('\\', '/');
            if (p == "" || p == ".") return shCwd;
            if (p == "~" || p.StartsWith("~/", StringComparison.Ordinal)) p = "/home" + p.Substring(1);
            if (Regex.IsMatch(p, "^[A-Za-z]:(/|$)")) p = "/mnt/" + char.ToLowerInvariant(p[0]) + p.Substring(2);   // C:\Users -> /mnt/c/Users
            var parts = new List<string>(p.StartsWith("/", StringComparison.Ordinal) ? new string[0] : shCwd.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
            foreach (var seg in p.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)) { if (seg == ".") continue; if (seg == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); continue; } parts.Add(seg); }
            return "/" + string.Join("/", parts);
        }
        static string Home { get { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } }
        // the real folder behind a path, or null for the virtual ones (/, /pages, /tools, /mnt)
        string Real(string v)
        {
            var seg = v.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (seg.Length == 0) return null;
            string root;
            switch (seg[0].ToLowerInvariant())
            {
                case "home": root = Home; break;
                case "data": root = Program.DataDir; break;
                case "mnt": if (seg.Length < 2 || seg[1].Length != 1) return null; root = char.ToUpperInvariant(seg[1][0]) + ":\\"; seg = seg.Skip(1).ToArray(); break;
                default: return null;
            }
            string r = root; for (int k = 1; k < seg.Length; k++) r = Path.Combine(r, seg[k]);
            return Fix(r);
        }
        // case as it is on disk (cd documents -> Documents), so the prompt shows the real name
        static string Fix(string real)
        {
            try
            {
                if (File.Exists(real) || Directory.Exists(real)) { var parent = Path.GetDirectoryName(real); if (parent == null) return real; var hit = Directory.EnumerateFileSystemEntries(parent).FirstOrDefault(z => string.Equals(Path.GetFileName(z), Path.GetFileName(real), StringComparison.OrdinalIgnoreCase)); return hit ?? real; }
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException)) throw; }
            return real;
        }
        static readonly string[][] ShRoots = { new[] { "home", "your folder (" + Environment.UserName + ")" }, new[] { "data", "AdeeboMeter's own folder - history, reports, recordings" }, new[] { "mnt", "your drives" }, new[] { "pages", "every page - cd into one, then open" }, new[] { "tools", "every tool by group - each one is a command" } };
        string ToolDir(string g) { return Regex.Replace(GroupName(g).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-'); }
        List<string> ShPageIds() { return Secs.Select(z => z[0]).Concat(new[] { "calc", "reports", "lab", "settings", "about" }).Distinct().ToList(); }
        // what's in a virtual folder: (name, is a folder, a word about it)
        List<string[]> VirtualList(string v, out bool known)
        {
            known = true; var seg = v.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); var r = new List<string[]>();
            if (seg.Length == 0) { foreach (var x in ShRoots) r.Add(new[] { x[0], "d", x[1] }); return r; }
            switch (seg[0].ToLowerInvariant())
            {
                case "mnt": if (seg.Length == 1) { foreach (var d in DriveInfo.GetDrives()) { string lab = ""; try { if (d.IsReady) lab = d.VolumeLabel + " · " + (d.AvailableFreeSpace / 1073741824) + " GB free"; } catch (IOException) { } r.Add(new[] { d.Name.Substring(0, 1).ToLowerInvariant(), "d", d.DriveType + (lab != "" ? " · " + lab : "") }); } return r; } break;
                case "pages": if (seg.Length == 1) { foreach (var id in ShPageIds()) r.Add(new[] { id, "d", NavInfo.ContainsKey(id) ? NavName(id) : id }); return r; } if (seg.Length == 2 && ShPageIds().Contains(seg[1].ToLowerInvariant())) return r; break;
                case "tools":
                    if (shTools == null) break;
                    if (seg.Length == 1) { foreach (var g in shTools.GroupBy(t => ToolDir(t.Group))) r.Add(new[] { g.Key, "d", g.Count() + " tools · " + GroupName(g.First().Group) }); return r; }
                    if (seg.Length == 2) { var l = shTools.Where(t => ToolDir(t.Group) == seg[1].ToLowerInvariant()).ToList(); if (l.Count > 0) { foreach (var t in l) r.Add(new[] { t.Id, "x", L.T(t.Title) }); return r; } }
                    break;
            }
            known = false; return r;
        }
        bool IsDir(string v) { string real = Real(v); if (real != null) return Directory.Exists(real); bool k; VirtualList(v, out k); return k; }

        // =====================================================================================================================
        // one command: redirects, assignments, then a built-in or a tool
        int RunCmd(NCmd cmd, ShCtx c)
        {
            var words = Expand(cmd.Words, c);
            string outFile = null, inFile = null; bool append = false; var args = new List<string>();
            for (int k = 0; k < words.Count; k++)
            {
                if (words[k] == "\u0003>" || words[k] == "\u0003>>") { append = words[k] == "\u0003>>"; outFile = words[++k]; continue; }
                if (words[k] == "\u0003<") { inFile = words[++k]; continue; }
                args.Add(words[k]);
            }
            if (args.Count == 0) return 0;
            if (args[0] == "\u0002") return 0;
            // NAME=value (alone): a variable
            var am = Regex.Match(args[0], "^([A-Za-z_][A-Za-z0-9_]*)=(.*)$", RegexOptions.Singleline);
            if (am.Success && args.Count == 1) { lock (shVars) shVars[am.Groups[1].Value] = am.Groups[2].Value; return 0; }
            if (inFile != null) { string ri = Real(Abs(inFile)); if (ri == null || !File.Exists(ri)) { Err(c, "adeeboshell: " + inFile + ": no such file"); return 1; } try { c = c.With(c.Out, File.ReadAllText(ri)); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, inFile + ": " + e.Message); return 1; } }
            if (outFile != null)
            {
                string ro = Real(Abs(outFile)); if (ro == null) { Err(c, "adeeboshell: " + outFile + ": can't write there (a folder of pages or tools isn't a disk)"); return 1; }
                var cap = new ShOut { Cap = new StringBuilder(), Job = c.Job }; int ex = Dispatch(args, c.With(cap, c.Stdin));
                try { if (append) File.AppendAllText(ro, cap.Cap.ToString()); else File.WriteAllText(ro, cap.Cap.ToString()); }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, outFile + ": " + e.Message); return 1; }
                return ex;
            }
            return Dispatch(args, c);
        }
        int Dispatch(List<string> a, ShCtx c)
        {
            string cmd = a[0], low = cmd.ToLowerInvariant(); var args = a.Skip(1).ToList();
            try
            {
                if (cmd.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) || cmd.StartsWith("./", StringComparison.Ordinal)) return Source(cmd, args, c);
                int r = Builtin(low, args, c); if (r != int.MinValue) { if (c.Out.Screen && (low == "status" || low == "debug" || low == "log" || low == "ls" || low == "man" || low == "tools")) { } return r; }
                if (low == "iperf3" || (low == "iperf" && args.Any(z => z.StartsWith("-", StringComparison.Ordinal) && z != "--yes" && z != "-y"))) return Iperf3(args, c);
                var t = shTools != null ? shTools.FirstOrDefault(x => string.Equals(x.Id, low, StringComparison.OrdinalIgnoreCase)) : null;
                if (t != null) return RunTool(t, args, c);
                var near = (shTools ?? new List<Tool>()).Select(x => x.Id).Concat(ShCmds).Where(w => w.StartsWith(low.Substring(0, Math.Min(2, low.Length)), StringComparison.OrdinalIgnoreCase)).Distinct().Take(6).ToList();
                Err(c, "adeeboshell: " + cmd + ": command not found" + (near.Count > 0 ? " - did you mean: " + string.Join(", ", near) + "?" : "  (help lists them)"));
                return 127;
            }
            catch (Exception e) { if (e is OutOfMemoryException || e is ThreadAbortException) throw; Err(c, cmd + ": " + e.Message); Program.Log("shell " + cmd + ": " + e); return 2; }
        }
        static readonly string[] ShCmds = { "help", "man", "tools", "ls", "ll", "cd", "pwd", "cat", "head", "tail", "grep", "sort", "uniq", "wc", "cp", "mv", "mkdir", "rm", "touch", "find", "tree", "echo", "printf", "open", "pages", "status", "debug", "log", "history", "clear", "copy", "save", "exit", "lab", "source", "alltools", "parallel", "jobs", "wait", "kill", "sleep", "seq", "date", "whoami", "hostname", "uname", "env", "export", "unset", "set", "read", "test", "[", "true", "false", "time", "basename", "dirname", "which", "type", "tee", "du", "stat", "hash", "nano", "vim", "vi", "iperf3" };

        int Builtin(string cmd, List<string> args, ShCtx c)
        {
            switch (cmd)
            {
                case "help": if (args.Count > 0) return Man(args[0], c); Help(c); return 0;
                case "man": if (args.Count == 0) { Err(c, "usage: man <tool or command>"); return 1; } return Man(args[0], c);
                case "tools": ShToolList(args.Count > 0 ? string.Join(" ", args) : "", c); return 0;
                case "pwd": OL(c, shCwd + (Real(shCwd) != null ? "   (" + Real(shCwd) + ")" : "")); return 0;
                case "cd": return Cd(args.Count == 0 ? "~" : args[0], c);
                case "ls": case "ll": case "dir": return Ls(args, cmd == "ll", c);
                case "tree": return Tree(args.FirstOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal)) ?? ".", c);
                case "cat": return Cat(args, c);
                case "head": case "tail": return HeadTail(cmd == "head", args, c);
                case "grep": return Grep(args, c);
                case "sort": { var l = InLines(args.Where(z => !z.StartsWith("-", StringComparison.Ordinal)).ToList(), c); if (l == null) return 1; bool rev = args.Any(z => z.StartsWith("-", StringComparison.Ordinal) && z.Contains("r")), num = args.Any(z => z.StartsWith("-", StringComparison.Ordinal) && z.Contains("n")), uq = args.Any(z => z.StartsWith("-", StringComparison.Ordinal) && z.Contains("u")); IEnumerable<string> s = num ? l.OrderBy(z => { double d; return double.TryParse(Regex.Match(z, @"-?\d+(\.\d+)?").Value, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : double.MaxValue; }) : l.OrderBy(z => z, StringComparer.CurrentCultureIgnoreCase); if (rev) s = s.Reverse(); if (uq) s = s.Distinct(); foreach (var x in s) OL(c, x); return 0; }
                case "uniq": { var l = InLines(args.Where(z => !z.StartsWith("-", StringComparison.Ordinal)).ToList(), c); if (l == null) return 1; bool cnt = args.Contains("-c"); string prev = null; int n = 0; Action fl = () => { if (prev != null) OL(c, cnt ? n.ToString(CultureInfo.InvariantCulture).PadLeft(7) + " " + prev : prev); }; foreach (var x in l) { if (x == prev) { n++; continue; } fl(); prev = x; n = 1; } fl(); return 0; }
                case "wc": { var files = args.Where(z => !z.StartsWith("-", StringComparison.Ordinal)).ToList(); string txt = files.Count > 0 ? ReadAll(files[0], c) : c.Stdin ?? ""; if (txt == null) return 1; int ln = txt.Count(ch => ch == '\n') + (txt.Length > 0 && !txt.EndsWith("\n", StringComparison.Ordinal) ? 1 : 0), wd = Regex.Matches(txt, @"\S+").Count; OL(c, args.Contains("-l") ? ln.ToString(CultureInfo.InvariantCulture) : args.Contains("-w") ? wd.ToString(CultureInfo.InvariantCulture) : args.Contains("-c") ? txt.Length.ToString(CultureInfo.InvariantCulture) : ln + " " + wd + " " + txt.Length + (files.Count > 0 ? " " + files[0] : "")); return 0; }
                case "tee": { string txt = c.Stdin ?? ""; foreach (var f in args.Where(z => !z.StartsWith("-", StringComparison.Ordinal))) { string r = Real(Abs(f)); if (r == null) { Err(c, f + ": can't write there"); return 1; } if (args.Contains("-a")) File.AppendAllText(r, txt); else File.WriteAllText(r, txt); } O(c, txt, ShFg); return 0; }
                case "echo": OL(c, args.Count > 0 && args[0] == "-n" ? string.Join(" ", args.Skip(1)) : string.Join(" ", args)); return 0;
                case "printf": { if (args.Count == 0) return 0; string f = args[0].Replace("\\n", "\n").Replace("\\t", "    "); int k = 1; f = Regex.Replace(f, "%[sd]", m => k < args.Count ? args[k++] : ""); O(c, f, ShFg); return 0; }
                case "cp": case "mv": return CpMv(cmd == "mv", args, c);
                case "mkdir": { foreach (var d in args.Where(z => !z.StartsWith("-", StringComparison.Ordinal))) { string r = Real(Abs(d)); if (r == null) { Err(c, "mkdir: " + d + ": not on a disk"); return 1; } Directory.CreateDirectory(r); } return 0; }
                case "touch": { foreach (var f in args) { string r = Real(Abs(f)); if (r == null) { Err(c, "touch: " + f + ": not on a disk"); return 1; } if (File.Exists(r)) File.SetLastWriteTime(r, DateTime.Now); else File.WriteAllText(r, ""); } return 0; }
                case "rm": return Rm(args, c);
                case "find": return Find(args, c);
                case "du": case "stat": return Stat(args, c);
                case "basename": OL(c, args.Count > 0 ? args[0].TrimEnd('/').Substring(args[0].TrimEnd('/').LastIndexOf('/') + 1) : ""); return 0;
                case "dirname": OL(c, args.Count > 0 && args[0].Contains("/") ? args[0].Substring(0, Math.Max(1, args[0].TrimEnd('/').LastIndexOf('/'))) : "."); return 0;
                case "which": case "type": foreach (var w in args) OL(c, ShCmds.Contains(w) ? w + " is a shell command" : shTools.Any(t => t.Id == w) ? w + " is a tool: /tools/" + ToolDir(shTools.First(t => t.Id == w).Group) + "/" + w : w + ": not found"); return 0;
                case "open": case "go": return Open(args, c);
                case "pages": foreach (var id in ShPageIds()) { O(c, "  " + id.PadRight(12), ShCyan); OL(c, NavInfo.ContainsKey(id) ? NavName(id) : id, ShDim); } return 0;
                case "status": Status(c); return 0;
                case "debug": Debug(c); return 0;
                case "log": Log(args.Count > 0 ? args[0] : "20", c); return 0;
                case "history": for (int k = 0; k < shHist.Count; k++) OL(c, (k + 1).ToString(CultureInfo.InvariantCulture).PadLeft(5) + "  " + shHist[k], ShDim); return 0;
                case "clear": case "cls": Ui(delegate { term.Clear(); }); return 0;
                case "copy": if (shLast == "") { Err(c, "nothing to copy yet - run a tool first (or select text and press Ctrl+C)"); return 1; } Ui(delegate { try { Clipboard.SetText(shLast); } catch (Exception e) { if (!(e is System.Runtime.InteropServices.ExternalException || e is ThreadStateException)) throw; } }); OL(c, "copied the last result", ShGreen); return 0;
                case "save": { if (shLast == "") { Err(c, "nothing to save yet - or run it with  > file.txt"); return 1; } string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AdeeboShell-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt"); File.WriteAllText(f, shLast); OL(c, "saved: " + f, ShGreen); return 0; }
                case "exit": case "quit": Ui(delegate { tab = "overview"; ShowTab(); SyncNav(); }); return 0;
                case "lab": return ShLab(args, c);
                case "source": case ".": case "sh": case "bash": if (args.Count == 0) { Err(c, "usage: source <file.sh> [args] [--yes]"); return 1; } return Source(args[0], args.Skip(1).ToList(), c);
                case "alltools": return AllTools(args.Contains("--yes"), c);
                case "parallel": return Parallel(args, c);
                case "jobs": lock (shJobs) foreach (var j in shJobs.Where(z => !z.Done)) OL(c, "[" + j.Id + "] running", ShCyan); return 0;
                case "wait": { var until = DateTime.UtcNow.AddMinutes(60); while (!c.Job.Cancel && DateTime.UtcNow < until) { bool any; lock (shJobs) any = shJobs.Any(z => !z.Done && z != c.Job); if (!any) break; Thread.Sleep(100); } return 0; }
                case "kill": { foreach (var w in args) { int id; if (!int.TryParse(w.TrimStart('%'), out id)) continue; ShJob j; lock (shJobs) j = shJobs.FirstOrDefault(z => z.Id == id); if (j == null) { Err(c, "kill: no job " + w); continue; } j.Cancel = true; lock (j.Runs) foreach (var r in j.Runs) r.Stop = true; OL(c, "[" + id + "] stopping", ShAmber); } return 0; }
                case "sleep": { double s; if (!double.TryParse(args.FirstOrDefault() ?? "1", NumberStyles.Float, CultureInfo.InvariantCulture, out s)) s = 1; var until = DateTime.UtcNow.AddSeconds(Math.Min(3600, s)); while (!c.Job.Cancel && DateTime.UtcNow < until) Thread.Sleep(50); return c.Job.Cancel ? 130 : 0; }
                case "seq": { int a0 = 1, b0, st = 1; if (args.Count == 1) int.TryParse(args[0], out b0); else { int.TryParse(args.ElementAtOrDefault(0), out a0); int.TryParse(args.Count > 2 ? args[2] : args.ElementAtOrDefault(1), out b0); if (args.Count > 2) int.TryParse(args[1], out st); } if (st == 0) st = 1; int n = 0; for (int v = a0; st > 0 ? v <= b0 : v >= b0; v += st) { OL(c, v.ToString(CultureInfo.InvariantCulture)); if (++n > 100000) break; } return 0; }
                case "date": OL(c, DateTime.Now.ToString(args.Count > 0 && args[0].StartsWith("+", StringComparison.Ordinal) ? args[0].Substring(1).Replace("%Y", "yyyy").Replace("%m", "MM").Replace("%d", "dd").Replace("%H", "HH").Replace("%M", "mm").Replace("%S", "ss") : "ddd d MMM yyyy HH:mm:ss", CultureInfo.CurrentCulture)); return 0;
                case "whoami": OL(c, ShUser); return 0;
                case "hostname": OL(c, ShHost); return 0;
                case "uname": OL(c, "AdeeboShell " + Program.Version + " · " + Environment.OSVersion + " · " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")); return 0;
                case "env": case "set": lock (shVars) foreach (var kv in shVars.OrderBy(z => z.Key)) OL(c, kv.Key + "=" + kv.Value); OL(c, "HOME=/home  PWD=" + shCwd + "  USER=" + ShUser, ShDim); return 0;
                case "export": foreach (var w in args) { var m = Regex.Match(w, "^([A-Za-z_][A-Za-z0-9_]*)=(.*)$"); if (m.Success) lock (shVars) shVars[m.Groups[1].Value] = m.Groups[2].Value; } return 0;
                case "unset": lock (shVars) foreach (var w in args) shVars.Remove(w); return 0;
                case "read": { bool sec = args.Contains("-s"); string ask = ""; int pi = args.IndexOf("-p"); if (pi >= 0 && pi + 1 < args.Count) ask = args[pi + 1]; var names = args.Where((z, i) => !z.StartsWith("-", StringComparison.Ordinal) && !(pi >= 0 && i == pi + 1)).ToList(); string ans;
                    if (c.Stdin != null) { if (c.Stdin == "") return 1; int nl = c.Stdin.IndexOf('\n'); ans = (nl < 0 ? c.Stdin : c.Stdin.Substring(0, nl)).TrimEnd('\r'); c.Stdin = nl < 0 ? "" : c.Stdin.Substring(nl + 1); lock (shVars) shVars[names.Count > 0 ? names[0] : "REPLY"] = ans; return 0; }   // ... | while read h
                    if (c.Job.Bg) { Err(c, "read: nothing to read here - pipe lines into it (cat list.txt | while read h; do ...; done)"); return 1; } ans = ShAsk(ask, sec); if (ans == null) return 130; lock (shVars) shVars[names.Count > 0 ? names[0] : "REPLY"] = ans; return 0; }
                case "test": case "[": return Test(cmd == "[" ? args.Take(Math.Max(0, args.Count - 1)).ToList() : args) ? 0 : 1;
                case "true": return 0;
                case "false": return 1;
                case "time": { var sw = System.Diagnostics.Stopwatch.StartNew(); int r = args.Count == 0 ? 0 : Dispatch(args, c); OL(c, "real " + sw.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + " s", ShDim); return r; }
                case "nano": case "vim": case "vi": case "edit": return Edit(cmd == "vim" || cmd == "vi", args, c);
                case "hash": { if (args.Count == 0) { Err(c, "usage: hash <file> - SHA-256 (the hashf tool does more)"); return 1; } foreach (var f in args) { string r = Real(Abs(f)); if (r == null || !File.Exists(r)) { Err(c, f + ": no such file"); continue; } using (var sh = System.Security.Cryptography.SHA256.Create()) using (var fs = File.OpenRead(r)) OL(c, BitConverter.ToString(sh.ComputeHash(fs)).Replace("-", "").ToLowerInvariant() + "  " + f); } return 0; }
            }
            return int.MinValue;
        }

        // ---------- folders and files
        int Cd(string p, ShCtx c)
        {
            if (p == "-") p = shPrev;
            string v = Abs(p);
            if (!IsDir(v))
            {
                // "cd home" / "cd pages" from anywhere: the top folder of that name (Adeeb, 22 Nov) - else a page or a group of that name
                string bare = p.Trim('/').ToLowerInvariant();
                if (!p.Contains("/") && ShRoots.Any(z => z[0] == bare)) v = "/" + bare;
                else if (!p.Contains("/") && ShPageIds().Contains(bare)) v = "/pages/" + bare;
                else if (!p.Contains("/") && shTools != null && shTools.Any(t => ToolDir(t.Group) == bare)) v = "/tools/" + bare;
                else { Err(c, "cd: " + p + ": no such folder"); return 1; }
            }
            string real = Real(v); if (real != null) { var seg = v.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); string disk = Fix(real); if (disk != real) { } v = "/" + string.Join("/", seg.Take(seg[0] == "mnt" ? 2 : 1)) + VirtualTail(real, seg[0] == "mnt" ? char.ToUpperInvariant(seg[1][0]) + ":\\" : seg[0] == "home" ? Home : Program.DataDir); }
            shPrev = shCwd; shCwd = v; Ui(ShPromptKeepHidden);
            return 0;
        }
        // the part of a real path under its root, with the disk's own capitals
        static string VirtualTail(string real, string root)
        {
            string full = Path.GetFullPath(real), r = Path.GetFullPath(root).TrimEnd('\\');
            if (!full.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return "";
            var parts = full.Substring(r.Length).Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries); string acc = r + "\\", outp = "";
            foreach (var pt in parts) { string fixd = Fix(Path.Combine(acc, pt)); string name = Path.GetFileName(fixd); outp += "/" + name; acc = Path.Combine(acc, name); }
            return outp;
        }
        int Ls(List<string> args, bool longForm, ShCtx c)
        {
            bool all = args.Any(z => z.StartsWith("-", StringComparison.Ordinal) && z.Contains("a")); longForm |= args.Any(z => z.StartsWith("-", StringComparison.Ordinal) && z.Contains("l"));
            var targets = args.Where(z => !z.StartsWith("-", StringComparison.Ordinal)).ToList(); if (targets.Count == 0) targets.Add(".");
            int ex = 0;
            foreach (var tg in targets)
            {
                string v = Abs(tg); if (targets.Count > 1) OL(c, v + ":", ShBlue);
                string real = Real(v);
                if (real == null)
                {
                    bool known; var l = VirtualList(v, out known); if (!known) { Err(c, "ls: " + tg + ": no such folder"); ex = 1; continue; }
                    if (l.Count == 0 && v.StartsWith("/pages/", StringComparison.Ordinal)) { OL(c, "a page - type  open  to go there", ShDim); continue; }
                    int w = l.Count == 0 ? 0 : l.Max(z => z[0].Length) + 2;
                    foreach (var e in l) { O(c, (e[0] + (e[1] == "d" ? "/" : "")).PadRight(w + 1), e[1] == "d" ? ShBlue : ShGreen); OL(c, e[2], ShDim); }
                    continue;
                }
                if (File.Exists(real)) { var fi = new FileInfo(real); OL(c, longForm ? FmtLs(fi) : fi.Name); continue; }
                if (!Directory.Exists(real)) { Err(c, "ls: " + tg + ": no such file or folder"); ex = 1; continue; }
                try
                {
                    var di = new DirectoryInfo(real);
                    var ents = di.EnumerateFileSystemInfos().Where(z => all || ((z.Attributes & FileAttributes.Hidden) == 0 && !z.Name.StartsWith(".", StringComparison.Ordinal))).OrderBy(z => !(z is DirectoryInfo)).ThenBy(z => z.Name, StringComparer.OrdinalIgnoreCase).Take(5000).ToList();
                    if (longForm) { foreach (var e in ents) OL(c, FmtLs(e), e is DirectoryInfo ? ShBlue : IsScript(e.Name) ? ShGreen : ShFg); OL(c, ents.Count + " entries", ShDim); }
                    else
                    {
                        // columns, like ls on a terminal
                        var names = ents.Select(e => e.Name + (e is DirectoryInfo ? "/" : "")).ToList(); int colw = names.Count == 0 ? 0 : Math.Min(40, names.Max(z => z.Length) + 2), per = Math.Max(1, 100 / Math.Max(1, colw));
                        for (int i = 0; i < names.Count; i++) { var e = ents[i]; string n = names[i].Length > colw - 1 ? names[i].Substring(0, colw - 2) + "…" : names[i]; O(c, c.Out.Screen ? n.PadRight(colw) : n, e is DirectoryInfo ? ShBlue : IsScript(e.Name) ? ShGreen : ShFg); if (!c.Out.Screen || (i + 1) % per == 0 || i == names.Count - 1) O(c, "\n", ShFg); }
                    }
                }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, "ls: " + tg + ": " + e.Message); ex = 1; }
            }
            return ex;
        }
        static bool IsScript(string n) { return n.EndsWith(".sh", StringComparison.OrdinalIgnoreCase); }
        static string FmtLs(FileSystemInfo e)
        {
            var fi = e as FileInfo; string size = fi == null ? "-" : fi.Length < 1024 ? fi.Length + " B" : fi.Length < 1048576 ? (fi.Length / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " K" : (fi.Length / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " M";
            return (e is DirectoryInfo ? "d" : "-") + ((e.Attributes & FileAttributes.ReadOnly) != 0 ? "r-" : "rw") + "  " + size.PadLeft(9) + "  " + e.LastWriteTime.ToString("dd MMM HH:mm", CultureInfo.CurrentCulture) + "  " + e.Name + (e is DirectoryInfo ? "/" : "");
        }
        int Tree(string p, ShCtx c)
        {
            string real = Real(Abs(p)); if (real == null || !Directory.Exists(real)) { Err(c, "tree: " + p + ": not a folder on a disk"); return 1; }
            int n = 0; Action<string, string, int> walk = null;
            walk = (dir, pre, depth) =>
            {
                if (depth > 3 || n > 400 || c.Job.Cancel) return; string[] ents; try { ents = Directory.GetFileSystemEntries(dir).Where(z => !Path.GetFileName(z).StartsWith(".", StringComparison.Ordinal)).OrderBy(z => !Directory.Exists(z)).ThenBy(z => z, StringComparer.OrdinalIgnoreCase).ToArray(); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; return; }
                for (int i = 0; i < ents.Length && n <= 400; i++) { bool last = i == ents.Length - 1, d = Directory.Exists(ents[i]); n++; O(c, pre + (last ? "└── " : "├── "), ShDim); OL(c, Path.GetFileName(ents[i]) + (d ? "/" : ""), d ? ShBlue : ShFg); if (d) walk(ents[i], pre + (last ? "    " : "│   "), depth + 1); }
            };
            OL(c, ShShort(Abs(p)), ShBlue); walk(real, "", 1); if (n > 400) OL(c, "... (stopped at 400)", ShDim); return 0;
        }
        string ReadAll(string f, ShCtx c)
        {
            string r = Real(Abs(f)); if (r == null || !File.Exists(r)) { Err(c, f + ": no such file"); return null; }
            var fi = new FileInfo(r); if (fi.Length > 20 * 1048576) { Err(c, f + ": over 20 MB - head or tail it instead"); return null; }
            try { return File.ReadAllText(r); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, f + ": " + e.Message); return null; }
        }
        List<string> InLines(List<string> files, ShCtx c)
        {
            if (files.Count == 0) return (c.Stdin ?? "").Replace("\r", "").TrimEnd('\n').Split('\n').Where((z, i) => !(i == 0 && z == "" && (c.Stdin ?? "") == "")).ToList();
            var l = new List<string>(); foreach (var f in files) { var t = ReadAll(f, c); if (t == null) return null; l.AddRange(t.Replace("\r", "").TrimEnd('\n').Split('\n')); } return l;
        }
        int Cat(List<string> args, ShCtx c)
        {
            if (args.Count == 0) { O(c, c.Stdin ?? "", ShFg); return 0; }
            int ex = 0; bool num = args.Contains("-n");
            foreach (var f in args.Where(z => !z.StartsWith("-", StringComparison.Ordinal)))
            {
                string r = Real(Abs(f)); if (r != null && Directory.Exists(r)) { Err(c, "cat: " + f + ": is a folder"); ex = 1; continue; }
                var t = ReadAll(f, c); if (t == null) { ex = 1; continue; }
                if (t.Take(8000).Count(ch => ch == '\0') > 2) { Err(c, "cat: " + f + ": a binary file (" + new FileInfo(r).Length + " bytes) - hashf or b64 can read it"); ex = 1; continue; }
                if (num) { int n = 0; foreach (var ln in t.Replace("\r", "").Split('\n')) OL(c, (++n).ToString(CultureInfo.InvariantCulture).PadLeft(6) + "  " + ln); }
                else { O(c, t, ShFg); if (!t.EndsWith("\n", StringComparison.Ordinal)) O(c, "\n", ShFg); }
            }
            return ex;
        }
        int HeadTail(bool head, List<string> args, ShCtx c)
        {
            int n = 10; var files = new List<string>();
            for (int k = 0; k < args.Count; k++) { if (args[k] == "-n" && k + 1 < args.Count) { int.TryParse(args[++k], out n); continue; } if (Regex.IsMatch(args[k], @"^-\d+$")) { n = int.Parse(args[k].Substring(1), CultureInfo.InvariantCulture); continue; } files.Add(args[k]); }
            var l = InLines(files, c); if (l == null) return 1;
            foreach (var x in head ? l.Take(n) : l.Skip(Math.Max(0, l.Count - n))) OL(c, x);
            return 0;
        }
        int Grep(List<string> args, ShCtx c)
        {
            bool ic = false, inv = false, num = false, count = false; string pat = null; var files = new List<string>();
            foreach (var a in args) { if (a.StartsWith("-", StringComparison.Ordinal) && pat == null && a.Length > 1) { ic |= a.Contains("i"); inv |= a.Contains("v"); num |= a.Contains("n"); count |= a.Contains("c"); continue; } if (pat == null) pat = a; else files.Add(a); }
            if (pat == null) { Err(c, "usage: grep [-i -v -n -c] pattern [file...]"); return 2; }
            Regex rx; try { rx = new Regex(pat, ic ? RegexOptions.IgnoreCase : RegexOptions.None, TimeSpan.FromSeconds(2)); } catch (ArgumentException) { rx = new Regex(Regex.Escape(pat), ic ? RegexOptions.IgnoreCase : RegexOptions.None); }
            var l = InLines(files, c); if (l == null) return 2; int hits = 0, i = 0;
            foreach (var x in l) { i++; if (rx.IsMatch(x) != inv) { hits++; if (!count) OL(c, (num ? i + ":" : "") + x, ShFg); } }
            if (count) OL(c, hits.ToString(CultureInfo.InvariantCulture));
            return hits > 0 ? 0 : 1;
        }
        int CpMv(bool move, List<string> args, ShCtx c)
        {
            bool rec = args.Any(z => z.StartsWith("-", StringComparison.Ordinal) && z.Contains("r")); var p = args.Where(z => !z.StartsWith("-", StringComparison.Ordinal)).ToList();
            if (p.Count < 2) { Err(c, "usage: " + (move ? "mv" : "cp [-r]") + " source... target"); return 1; }
            string dst = Real(Abs(p[p.Count - 1])); if (dst == null) { Err(c, "can't write to " + p[p.Count - 1]); return 1; }
            foreach (var s in p.Take(p.Count - 1))
            {
                string src = Real(Abs(s)); if (src == null || (!File.Exists(src) && !Directory.Exists(src))) { Err(c, s + ": no such file"); return 1; }
                string to = Directory.Exists(dst) ? Path.Combine(dst, Path.GetFileName(src.TrimEnd('\\'))) : dst;
                if (File.Exists(src)) { if (move) { if (File.Exists(to)) File.Delete(to); File.Move(src, to); } else File.Copy(src, to, true); }
                else if (move) Directory.Move(src, to);
                else if (!rec) { Err(c, "cp: " + s + " is a folder - use cp -r"); return 1; }
                else CopyDir(src, to, c);
            }
            return 0;
        }
        static void CopyDir(string a, string b, ShCtx c) { Directory.CreateDirectory(b); foreach (var f in Directory.GetFiles(a)) { if (c.Job.Cancel) return; File.Copy(f, Path.Combine(b, Path.GetFileName(f)), true); } foreach (var d in Directory.GetDirectories(a)) CopyDir(d, Path.Combine(b, Path.GetFileName(d)), c); }
        int Rm(List<string> args, ShCtx c)
        {
            bool rec = args.Any(z => z.StartsWith("-", StringComparison.Ordinal) && z.Contains("r")), force = args.Any(z => z.StartsWith("-", StringComparison.Ordinal) && z.Contains("f"));
            var p = args.Where(z => !z.StartsWith("-", StringComparison.Ordinal)).ToList(); if (p.Count == 0) { Err(c, "usage: rm [-r] [-f] file..."); return 1; }
            foreach (var s in p)
            {
                string v = Abs(s), r = Real(v); if (r == null) { Err(c, "rm: " + s + ": not on a disk"); return 1; }
                if (v.Count(ch => ch == '/') <= 1 || string.Equals(r.TrimEnd('\\'), Home.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) || r.TrimEnd('\\').Length <= 3) { Err(c, "rm: won't remove " + s + " - a top folder or a whole drive"); return 1; }
                bool dir = Directory.Exists(r); if (!dir && !File.Exists(r)) { if (!force) Err(c, "rm: " + s + ": no such file"); continue; }
                if (dir && !rec) { Err(c, "rm: " + s + " is a folder - rm -r"); return 1; }
                if (!force) { if (c.Job.Bg || c.Script) { Err(c, "rm: " + s + " - add -f in a script (it can't ask you)"); return 1; } string ans = ShAsk("remove " + (dir ? "the folder " : "") + s + (dir ? " and everything in it" : "") + "? [y/N] ", false); if (ans == null || !ans.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase)) { OL(c, "kept", ShDim); continue; } }
                if (dir) Directory.Delete(r, true); else File.Delete(r);
            }
            return 0;
        }
        int Find(List<string> args, ShCtx c)
        {
            string start = args.FirstOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal)) ?? "."; int ni = args.IndexOf("-name"); string pat = ni >= 0 && ni + 1 < args.Count ? args[ni + 1] : "*";
            string real = Real(Abs(start)); if (real == null || !Directory.Exists(real)) { Err(c, "find: " + start + ": not a folder on a disk"); return 1; }
            var rx = new Regex("^" + Regex.Escape(pat).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase); int n = 0;
            var stack = new Stack<string>(); stack.Push(real); string baseV = Abs(start);
            while (stack.Count > 0 && n < 2000 && !c.Job.Cancel)
            {
                string d = stack.Pop(); string[] fs, ds;
                try { fs = Directory.GetFiles(d); ds = Directory.GetDirectories(d); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; continue; }
                foreach (var x in ds.Concat(fs)) if (rx.IsMatch(Path.GetFileName(x))) { n++; OL(c, ShShort(baseV) + x.Substring(real.TrimEnd('\\').Length).Replace('\\', '/'), Directory.Exists(x) ? ShBlue : ShFg); }
                foreach (var x in ds.Reverse()) stack.Push(x);
            }
            if (n >= 2000) OL(c, "... (stopped at 2,000)", ShDim);
            return n > 0 ? 0 : 1;
        }
        int Stat(List<string> args, ShCtx c)
        {
            foreach (var f in args.Where(z => !z.StartsWith("-", StringComparison.Ordinal)).DefaultIfEmpty("."))
            {
                string r = Real(Abs(f)); if (r == null) { Err(c, f + ": not on a disk"); continue; }
                if (File.Exists(r)) { var fi = new FileInfo(r); OL(c, f + "  " + fi.Length.ToString("N0", CultureInfo.CurrentCulture) + " bytes · changed " + fi.LastWriteTime.ToString("g", CultureInfo.CurrentCulture) + " · " + r); }
                else if (Directory.Exists(r)) { long sum = 0; int n = 0; try { foreach (var x in Directory.EnumerateFiles(r, "*", SearchOption.AllDirectories)) { if (c.Job.Cancel || ++n > 200000) break; try { sum += new FileInfo(x).Length; } catch (IOException) { } } } catch (UnauthorizedAccessException) { } OL(c, (sum / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB in " + n + " files  " + f); }
                else Err(c, f + ": no such file");
            }
            return 0;
        }
        bool Test(List<string> a)
        {
            if (a.Count == 0) return false;
            if (a[0] == "!") return !Test(a.Skip(1).ToList());
            if (a.Count == 1) return a[0] != "";
            if (a.Count == 2) { string r = Real(Abs(a[1])); switch (a[0]) { case "-z": return a[1] == ""; case "-n": return a[1] != ""; case "-f": return r != null && File.Exists(r); case "-d": return IsDir(Abs(a[1])); case "-e": return r != null && (File.Exists(r) || Directory.Exists(r)); } }
            if (a.Count == 3)
            {
                double x, y; bool nums = double.TryParse(a[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) & double.TryParse(a[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                switch (a[1]) { case "=": case "==": return a[0] == a[2]; case "!=": return a[0] != a[2]; case "-eq": return nums && x == y; case "-ne": return nums && x != y; case "-lt": return nums && x < y; case "-le": return nums && x <= y; case "-gt": return nums && x > y; case "-ge": return nums && x >= y; }
            }
            return false;
        }

        // ---------- pages, the lab, the help
        int Open(List<string> args, ShCtx c)
        {
            string p = args.Count == 0 ? "" : args[0].ToLowerInvariant().Trim('/');
            if (p == "" || p == ".") { var seg = shCwd.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); if (seg.Length == 2 && seg[0] == "pages") p = seg[1]; else if (seg.Length == 3 && seg[0] == "tools") p = "tool:" + seg[2]; else { Err(c, "open: which page? (cd pages/<name> then open, or open <name>)"); return 1; } }
            if (p.StartsWith("pages/", StringComparison.Ordinal)) p = p.Substring(6);
            if (p.StartsWith("tool:", StringComparison.Ordinal) || (shTools.Any(t => t.Id == p) && !ShPageIds().Contains(p))) { string id = p.StartsWith("tool:", StringComparison.Ordinal) ? p.Substring(5) : p; OL(c, "opening " + id + " in the Toolbox", ShGreen); Ui(delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(id); SyncNav(); }); return 0; }
            foreach (var al in new[] { "today=home", "markets=trade", "market=trade", "trading=trade", "streaming=stream", "machine=time", "wifi=link", "running=procs", "connections=conn", "problem=guide", "games=gamer", "game=gamer" }) { var kv = al.Split('='); if (p == kv[0]) p = kv[1]; }
            var r = Real(Abs(args.Count > 0 ? args[0] : "."));
            if (r != null && (File.Exists(r) || Directory.Exists(r))) { Err(c, "open: AdeeboShell doesn't start other programs - cat shows a file here, cd goes into a folder"); return 1; }
            if (NavInfo.ContainsKey(p) || Secs.Any(z => z[0] == p)) { OL(c, "opening " + NavName(p), ShGreen); Ui(delegate { OpenNav(p); }); return 0; }
            if (tabNames.ContainsKey(p) || p == "about" || p == "settings") { OL(c, "opening " + p, ShGreen); Ui(delegate { tab = p; ShowTab(); SyncNav(); }); return 0; }
            Err(c, "open: no page called " + p + " (ls /pages lists them)"); return 1;
        }
        int ShLab(List<string> args, ShCtx c)
        {
            bool ask = args.Remove("--ask"); string v = args.Count > 0 ? args[0].ToLowerInvariant() : "";
            var views = new[] { "lan", "path", "wifi", "phone", "vpn", "new" };
            if (v == "") { OL(c, "opening AdeeboLab", ShGreen); Ui(delegate { OpenNav("lab"); }); return 0; }
            if (!views.Contains(v)) { Err(c, "lab: no view \"" + v + "\" - try " + string.Join(", ", views)); return 1; }
            if (v == "path" && LabViews.Path(M) == null) { Err(c, "lab path: run a trace or MTR first (mtr 1.1.1.1 1), then lab path"); return 1; }
            if (ask && v != "lan") OL(c, "(--ask is for lab lan; ignored)", ShAmber);
            OL(c, "opening AdeeboLab: " + v + (ask ? " (asking the devices, about 8 s)" : ""), ShGreen); Ui(delegate { OpenLab(null, false, v, ask); }); return 0;
        }
        void Help(ShCtx c)
        {
            Action<string, string> h = (a, b) => { O(c, "  " + a.PadRight(34), ShCyan); OL(c, b, ShDim); };
            OL(c, "FOLDERS", ShBlue);
            h("ls [-l -a] [folder]", "what's here (ll = ls -l) · cd /data/scripts for ready-made scripts"); h("cd <folder> · cd ~ · cd - · cd ..", "home = your folder, data, mnt/c, pages, tools"); h("pwd · tree · find . -name '*.csv'", "where you are, what's below, find by name");
            h("cat · head -n 20 · tail · grep -i x", "read files or what a pipe sends"); h("cp [-r] · mv · mkdir · touch · rm [-r -f]", "files - rm asks first"); h("du · stat · hash <file>", "sizes, details, SHA-256");
            OL(c, "TOOLS AND PAGES", ShBlue);
            h("<tool> [args]", "every Toolbox tool is a command: ping 1.1.1.1 20 · dig example.com MX");
            h("iperf3 -c 10.0.0.5 -R -t 10 -P 4", "iperf3 with its real flags (-p -u -b --bidir too); AdeeboMeter is the client"); h("tools [word] · man <tool>", "list them, or what one does"); h("open <page> · cd pages/markets; open", "go to a page"); h("lab [lan|path|wifi|phone|vpn|new]", "AdeeboLab"); h("status · debug · log [n]", "the NOW line · raw values · the app's log");
            OL(c, "POWER", ShBlue);
            h("cmd | grep x | sort | head", "pipes; > file, >> file, < file"); h("cmd1 && cmd2 · cmd1 || cmd2 · cmd &", "and / or / in the background (jobs, wait, kill %1)");
            h("parallel -j 5 ping {} 4 ::: a b c", "run together, merge as each finishes; cat hosts.txt | parallel ping {} 4"); h("for h in a b c; do ping $h 4; done", "loops, while, if/then/else/fi, NAME=value, $1 $? read test seq sleep");
            h("nano file · vim file", "write a script or any text file, right here (^S save ^X exit · :wq)");
            h("source run.sh [args] [--yes] · ./run.sh", "a script; --yes confirms its test-traffic tools for the whole run"); h("alltools [--yes]", "every tool with its example");
            OL(c, "Keys: Tab completes (twice lists) · Up/Down history · Ctrl+C stops (or copies a selection) · Ctrl+L clears · Ctrl+U/K/W edit · right-click pastes", ShDim);
            OL(c, "Nothing here starts another program: every command is AdeeboMeter's own.", ShDim);
        }
        void ShToolList(string word, ShCtx c)
        {
            var list = shTools.Where(t => word == "" || t.Id.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 || L.T(t.Title).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 || GroupName(t.Group).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            foreach (var g in list.GroupBy(t => GroupName(t.Group))) { OL(c, g.Key.ToUpperInvariant() + "  (" + g.Count() + ")", ShBlue); foreach (var t in g) { O(c, "  " + t.Id.PadRight(14), ShCyan); OL(c, L.T(t.Title), ShFg); } }
            OL(c, list.Count + " tools" + (word != "" ? " match \"" + word + "\"" : "") + " - man <tool> for details", ShDim);
        }
        int Man(string id, ShCtx c)
        {
            var t = shTools.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (t == null) { if (ShCmds.Contains(id)) { OL(c, id + " is a shell command - help lists them all with an example", ShFg); return 0; } Err(c, "man: no tool called " + id + " (tools lists them)"); return 1; }
            var te = Explain.Term("tb." + t.Id); var e = te != null ? ToolTip(t.Id, te) : null;
            OL(c, t.Id + " - " + L.T(t.Title), ShCyan); if (e != null && !string.IsNullOrEmpty(e.Tag)) OL(c, e.Tag, ShFg);
            O(c, "USAGE  ", ShBlue); O(c, t.Id, ShCyan); OL(c, t.Inputs.Length == 0 ? "" : " " + string.Join(" ", t.Inputs.Where(z => z != "").Select(z => "<" + BoxLabel(z).ToLowerInvariant().Replace(' ', '-') + ">")), ShFg);
            for (int i = 0; i < t.Inputs.Length; i++) if (t.Inputs[i] != "") OL(c, "  " + (i + 1) + ". " + t.Inputs[i], ShDim);
            var ex = Examples.Get(t.Id); if (ex != null && ex.In.Any(x => x != "")) { O(c, "EXAMPLE  ", ShBlue); OL(c, t.Id + " " + string.Join(" ", ex.In.Select(x => x == "" ? "-" : x.Contains(" ") ? "\"" + x + "\"" : x)), ShFg); }
            if (e != null && e.Facts != null) foreach (var f in e.Facts) { O(c, f[0].PadRight(9), ShBlue); OL(c, f[1], ShFg); }
            if (e != null && !string.IsNullOrEmpty(e.What)) { OL(c, "WHAT IT DOES", ShBlue); OL(c, "  " + e.What, ShFg); }
            if (LabGate.Contains(t.Id) || SenderGate.Contains(t.Id) || t.Id == "sweep") OL(c, "NOTE     sends test traffic - it asks you first (in a script: source x.sh --yes)", ShAmber);
            return 0;
        }
        void Status(ShCtx c)
        {
            var s = snap; Func<double, string> ms = v => double.IsNaN(v) ? "no answer" : v.ToString("0", CultureInfo.InvariantCulture) + " ms";
            OL(c, s.Headline, s.Level == 2 ? ShRed : s.Level == 1 ? ShAmber : ShGreen);
            O(c, "router   ", ShBlue); OL(c, ms(M.GwPing.Avg(6))); O(c, "internet ", ShBlue); OL(c, ms(M.NetPing.Avg(6))); O(c, "names    ", ShBlue); OL(c, ms(M.Dns.Avg(6)));
            O(c, "this PC  ", ShBlue); OL(c, "CPU " + (double.IsNaN(s.Cpu) ? "?" : s.Cpu.ToString("0", CultureInfo.InvariantCulture) + "%") + " · memory " + (double.IsNaN(s.Mem) ? "?" : s.Mem.ToString("0", CultureInfo.InvariantCulture) + "%"));
            O(c, "traffic  ", ShBlue); OL(c, Draw.Pair(M.Down.Last, M.Up.Last));
        }
        void Debug(ShCtx c)
        {
            string ad, kind, ip, gw, gw6, dns, ssid, bssid, chan, link, vpn, ifid; int sig; bool vpnUp;
            lock (M.L) { ad = M.Adapter; kind = M.AdapterKind; ip = M.LocalIp; gw = M.Gateway; gw6 = M.Gateway6; dns = M.DnsServer; ssid = M.Ssid; bssid = M.Bssid; chan = M.WifiChannel; link = M.LinkSpeed; vpnUp = M.VpnUp; vpn = M.VpnName; ifid = M.IfaceId; sig = M.WifiSignal; }
            OL(c, "RAW VALUES - on this PC only", ShBlue);
            Action<string, string> kv = (k, v) => { O(c, "  " + k.PadRight(14), ShCyan); OL(c, string.IsNullOrEmpty(v) ? "-" : v, ShFg); };
            kv("adapter", ad + " (" + kind + ")"); kv("interface id", ifid); kv("IPv4", ip); kv("gateway", gw); kv("gateway (v6)", gw6); kv("DNS", dns); kv("Wi-Fi", ssid == "" ? "(name hidden or not Wi-Fi)" : ssid); kv("BSSID", bssid); kv("signal", sig >= 0 ? sig + "%" : ""); kv("channel", chan); kv("link", link); kv("VPN", vpnUp ? "up · " + vpn : "off");
            kv("version", Program.Version); kv("data folder", Program.DataDir);
            using (var me = System.Diagnostics.Process.GetCurrentProcess()) { kv("memory", (me.PrivateMemorySize64 / 1048576) + " MB private · " + me.Threads.Count + " threads · " + me.HandleCount + " handles"); kv("running for", (DateTime.Now - me.StartTime).ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture)); }
        }
        void Log(string nIn, ShCtx c)
        {
            int n; if (!int.TryParse(nIn, out n)) n = 20; n = Math.Max(1, Math.Min(2000, n));
            string p = Path.Combine(Program.WriteDir, "AdeeboMeter.log"); if (!File.Exists(p)) { OL(c, "no log yet", ShDim); return; }
            string[] lines; using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var sr = new StreamReader(fs)) lines = sr.ReadToEnd().Split('\n');
            foreach (var l in lines.Select(x => x.TrimEnd('\r')).Where(x => x != "").Skip(Math.Max(0, lines.Length - n - 1))) OL(c, l, l.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || l.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0 ? ShRed : ShDim);
        }

        // =====================================================================================================================
        // a tool: the Toolbox's own code, on this command's thread, its progress drawn live
        static readonly HashSet<string> ShExclusive = new HashSet<string> { "mtr", "speedtest", "diskspeed", "ramcheck", "gpucheck", "laptop", "board", "discover", "rlisten", "iperf" };
        static readonly object shExLock = new object();
        int RunTool(Tool t, List<string> args, ShCtx c)
        {
            bool yesArg = args.Remove("--yes") | args.Remove("-y");
            if (t.Do == null)
            {
                if (c.Script || c.Job.Bg || !c.Out.Screen) { Err(c, t.Id + (t.Id == "mtu" ? " runs in the Toolbox" : " draws a picture") + " - not run inside a script or a pipe"); return 1; }
                if (t.Id == "mtu") { string target = args.FirstOrDefault() ?? ""; if (target == "") { Err(c, "usage: mtu <address>"); return 1; } OL(c, "mtu runs in the Toolbox (it draws the path as it goes) - started there for " + target, ShGreen); Ui(delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool("mtu"); SyncNav(); if (!tbBusy) { if (tbBoxes.Count > 0 && tbBoxes[0] != null) tbBoxes[0].Box.Text = target; StartMtu(target); } }); return 0; }
                OL(c, t.Id + " draws its result as a picture - opening it in the Toolbox", ShAmber); string id0 = t.Id; Ui(delegate { tab = "toolbox"; ShowTab(); EnsureToolbox(); OpenTool(id0); SyncNav(); }); return 0;
            }
            bool needs = t.Inputs.Length > 0 && t.Inputs[0] != "" && t.Inputs[0].IndexOf("optional", StringComparison.OrdinalIgnoreCase) < 0 && t.Inputs[0].IndexOf("empty", StringComparison.OrdinalIgnoreCase) < 0;
            if (needs && args.Count == 0) { Err(c, "usage: " + t.Id + " " + string.Join(" ", t.Inputs.Where(h => h != "").Select(h => "<" + BoxLabel(h).ToLowerInvariant().Replace(' ', '-') + ">"))); var ex0 = Examples.Get(t.Id); if (ex0 != null && ex0.In.Any(z => z != "")) OL(c, "e.g.   " + t.Id + " " + string.Join(" ", ex0.In.Select(z => z == "" ? "-" : z.Contains(" ") ? "\"" + z + "\"" : z)), ShDim); return 2; }
            // test traffic: you say it's your network - asked here, or once for a whole script (source x.sh --yes)
            bool gate = LabGate.Contains(t.Id) || SenderGate.Contains(t.Id) || t.Id == "sweep", yes = !gate || c.Yes || (yesArg && !c.Script);
            if (gate && !yes)
            {
                if (c.Script || c.Job.Bg) { Err(c, t.Id + ": sends test traffic - skipped (run the script with --yes to confirm it's your network)"); return 1; }
                string ans = ShAsk(t.Id + " sends test traffic to " + (args.FirstOrDefault() ?? "the target") + ". Is this your own network, or one you may test? [y/N] ", false);
                if (ans == null || !ans.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase)) { OL(c, "not sent", ShDim); return 1; }
                yes = true;
            }
            var a = new string[Math.Max(4, t.Inputs.Length)];
            for (int i = 0; i < a.Length; i++) a[i] = i < args.Count && args[i] != "-" ? args[i] : "";
            if (t.Inputs.Length > 0 && args.Count > t.Inputs.Length) a[t.Inputs.Length - 1] = string.Join(" ", args.Skip(t.Inputs.Length - 1));
            for (int i = 0; i < a.Length; i++)   // a file you name from here: found from the folder you're in
            {
                if (string.IsNullOrEmpty(a[i]) || a[i].Contains("://") || (i < t.Inputs.Length && SecretInput(t.Inputs[i]))) continue;
                if (a[i].Contains("/") || a[i].StartsWith("~", StringComparison.Ordinal) || Regex.IsMatch(a[i], @"\.[A-Za-z0-9]{1,6}$")) { string r = Real(Abs(a[i])); if (r != null && (File.Exists(r) || Directory.Exists(r))) a[i] = r; }
            }
            if (!UrlTools.Contains(t.Id)) for (int i = 0; i < t.Inputs.Length; i++) if (!SecretInput(t.Inputs[i]) && !PayloadBox(t.Id, i) && t.Inputs[i].IndexOf("DoH", StringComparison.Ordinal) < 0 && t.Inputs[i].IndexOf("https://", StringComparison.Ordinal) < 0 && !File.Exists(a[i] ?? "") && !Directory.Exists(a[i] ?? "")) a[i] = Toolkit.UrlToTarget(a[i] ?? "", t.Inputs[i].IndexOf(":port", StringComparison.OrdinalIgnoreCase) >= 0);
            if (t.Id == "sweep" || LabGate.Contains(t.Id)) a[3] = yes ? "yes" : "";
            string sender = SenderGate.Contains(t.Id) ? (yes ? "yes" : "") : null;
            bool exclusive = ShExclusive.Contains(t.Id); if (exclusive) System.Threading.Monitor.Enter(shExLock);
            try
            {
                var run = new Toolkit.Run(); lock (c.Job.Runs) c.Job.Runs.Add(run);
                List<string[]> res = null; Exception err = null; bool done = false; var sw = System.Diagnostics.Stopwatch.StartNew();
                var th = new Thread(() =>
                {
                    try { if (sender != null) { var g0 = new List<string[]>(); res = Lab.Allowed(M, sender, g0) ? g0.Concat(t.Do(a, run) ?? new List<string[]>()).ToList() : g0; } else res = t.Do(a, run) ?? new List<string[]>(); }
                    catch (Exception e) { if (e is OutOfMemoryException) throw; err = e; }
                    done = true;
                }) { IsBackground = true, Name = "shell tool " + t.Id };
                th.Start();
                bool live = c.Out.Screen && !c.Job.Bg; int shown = 0; string[] spin = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" }; int sp = 0;
                while (!done)
                {
                    Thread.Sleep(120);
                    if (c.Job.Cancel && !run.Stop) run.Stop = true;
                    if (c.Job.Cancel && (DateTime.UtcNow - c.Job.CancelAt).TotalSeconds > 15) { Err(c, t.Id + ": didn't stop in 15 s - let go of it"); return 130; }
                    if (!live) continue;
                    // the samples so far, as lines (ping and friends), and the live table (mtr), then a status line
                    var ser = run.Copy(); var rows = new List<List<TermView.Seg>>();
                    if (t.Id != "mtr" && ser.Count > shown) { var add = ser.Skip(shown).ToList(); int k0 = shown; shown = ser.Count; Ui(delegate { term.EndLive(); foreach (var v in add) term.Write("  #" + (++k0).ToString(CultureInfo.InvariantCulture).PadRight(5) + (double.IsNaN(v) ? "no answer" : v.ToString("0.#", CultureInfo.InvariantCulture) + " " + (run.SeriesUnit == "" ? "" : run.SeriesUnit)) + "\n", double.IsNaN(v) ? ShRed : ShDim); }); }
                    if (t.Id == "mtr" && mtrHops != null) rows.AddRange(MtrRows(mtrHops));
                    rows.Add(new List<TermView.Seg> { new TermView.Seg(spin[sp++ % spin.Length] + " ", ShCyan), new TermView.Seg(t.Id + "  ", ShFg), new TermView.Seg((string.IsNullOrEmpty(run.Status) ? "working" : run.Status) + "  " + sw.Elapsed.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s · Ctrl+C stops", ShDim) });
                    Ui(delegate { term.Live(rows); });
                }
                if (live) Ui(delegate { term.EndLive(); });
                if (err != null) { Err(c, t.Id + ": couldn't finish - " + Draw.Trunc(err.Message, 140)); Program.Log("shell tool " + t.Id + ": " + err); return 2; }
                if (t.Id == "mtr" && mtrHops != null && c.Out.Screen) foreach (var r in MtrRows(mtrHops)) { foreach (var sg in r) O(c, sg.T, sg.C); O(c, "\n", ShFg); }
                PrintRows(res, c);
                var sb = new StringBuilder(); foreach (var o in res.Where(x => x != null && x.Length > 2 && !IsSecretRow(x))) sb.AppendLine((o[0] == "2" ? "[!!] " : o[0] == "1" ? "[!]  " : o[0] == "0" ? "[ok] " : "     ") + (o[1] == "" ? "" : ShareCell(o[1]) + ": ") + ShareCell(o[2]));
                shLast = sb.ToString();
                if (c.Out.Screen && !c.Job.Bg) OL(c, (run.Stop ? "stopped" : "done") + " in " + sw.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s", ShDim);
                return run.Stop && c.Job.Cancel ? 130 : res.Any(o => o != null && o.Length > 0 && o[0] == "2") ? 1 : 0;
            }
            finally { if (exclusive) System.Threading.Monitor.Exit(shExLock); }
        }
        List<List<TermView.Seg>> MtrRows(List<PathTools.Hop> hops)
        {
            var r = new List<List<TermView.Seg>> { new List<TermView.Seg> { new TermView.Seg("hop  router                                  loss  sent   avg  best worst jitter", ShBlue) } };
            foreach (var hp in hops.ToList())
            {
                double loss = hp.Sent > 0 ? 100.0 * hp.Lost / hp.Sent : 0, avg = hp.Sent - hp.Lost > 0 ? hp.Sum / (hp.Sent - hp.Lost) : double.NaN;
                r.Add(new List<TermView.Seg> { new TermView.Seg(hp.N.ToString(CultureInfo.InvariantCulture).PadRight(5) + (hp.Ip == "" ? "*" : hp.Ip).PadRight(40) + (loss.ToString("0", CultureInfo.InvariantCulture) + "%").PadLeft(5) + hp.Sent.ToString(CultureInfo.InvariantCulture).PadLeft(6) + PathTools.F(avg).PadLeft(6) + PathTools.F(hp.Best).PadLeft(6) + PathTools.F(hp.Worst).PadLeft(6) + PathTools.F(hp.Jit).PadLeft(7), loss > 20 ? ShAmber : ShFg) });
            }
            return r;
        }
        void PrintRows(List<string[]> rows, ShCtx c)
        {
            foreach (var o0 in rows.Where(x => x != null && x.Length > 0 && !IsSecretRow(x)))
            {
                var o = o0.Length >= 3 ? o0 : new[] { o0[0], o0.Length > 1 ? o0[1] : "", "" };
                if (!c.Out.Screen) { OL(c, (o[0] == "2" ? "[!!] " : o[0] == "1" ? "[!]  " : o[0] == "0" ? "[ok] " : o[0] == "-1" ? "" : "     ") + (o[1] == "" ? "" : ShareCell(o[1]) + (o[0] == "-1" ? "" : ": ")) + ShareCell(o[2])); continue; }
                if (o[0] == "6" || o[0] == "7" || o[0] == "8") { OL(c, (o[0] == "7" ? "! " : "  ") + ShareCell(o[2]), o[0] == "7" ? ShAmber : ShFg); continue; }
                if (o[0] == "-1") { OL(c, ""); OL(c, ShareCell(o[1]).ToUpperInvariant() + (o[2] != "" ? "  " + ShareCell(o[2]) : ""), ShBlue); continue; }
                Color col = o[0] == "2" ? ShRed : o[0] == "1" ? ShAmber : o[0] == "0" ? ShGreen : ShDim;
                O(c, o[0] == "2" ? "[!!] " : o[0] == "1" ? "[!]  " : o[0] == "0" ? "[ok] " : "     ", col);
                O(c, ShareCell(o[1]), ShFg); O(c, ": ", ShDim); OL(c, ShareCell(o[2]), o[0] == "2" ? ShRed : o[0] == "1" ? ShAmber : ShFg);
                if (o.Length > 3 && o[3] != "") OL(c, "      " + ShareCell(o[3]), ShDim);
            }
        }

        // ---------- scripts, parallel, every tool
        int Source(string file, List<string> args, ShCtx c)
        {
            bool yes = args.Remove("--yes");
            string r = Real(Abs(file)); if (r == null || !File.Exists(r)) { Err(c, "source: " + file + ": no such file"); return 1; }
            if (new FileInfo(r).Length > 512 * 1024) { Err(c, "source: " + file + " is over 512 KB - that's not a script"); return 1; }
            string text; try { text = File.ReadAllText(r); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, file + ": " + e.Message); return 1; }
            int depth; lock (shVars) { string d; shVars.TryGetValue("\u0004depth", out d); int.TryParse(d, out depth); if (depth >= 8) { Err(c, "source: scripts nested 8 deep - stopped (does a script run itself?)"); return 1; } shVars["\u0004depth"] = (depth + 1).ToString(CultureInfo.InvariantCulture); }
            try { return ShRunTextIn(text, new ShCtx { Job = c.Job, Out = c.Out, Stdin = c.Stdin, Args = args, Yes = c.Yes || yes, Script = true }); }
            finally { lock (shVars) shVars["\u0004depth"] = depth.ToString(CultureInfo.InvariantCulture); }
        }
        int ShRunTextIn(string text, ShCtx c) { NSeq prog; try { prog = new Parser(Lex(text.Replace("\r", ""))).List(); } catch (FormatException fe) { Err(c, "script: " + fe.Message); return 2; } return Exec(prog, c); }
        int Parallel(List<string> args, ShCtx c)
        {
            int jobs = 4; bool yes = args.Remove("--yes"); int ji = args.IndexOf("-j"); if (ji >= 0 && ji + 1 < args.Count) { int.TryParse(args[ji + 1], out jobs); args.RemoveRange(ji, 2); } jobs = Math.Max(1, Math.Min(16, jobs));
            int sep = args.IndexOf(":::"); var cmd = sep >= 0 ? args.Take(sep).ToList() : args; var items = sep >= 0 ? args.Skip(sep + 1).ToList() : InLines(new List<string>(), c).Where(z => z.Trim() != "").Select(z => z.Trim()).ToList();
            if (cmd.Count == 0 || items.Count == 0) { Err(c, "usage: parallel [-j N] [--yes] command {} ::: item item ...   (or: cat list.txt | parallel ping {} 4)"); return 2; }
            if (items.Count > 500) { Err(c, "parallel: " + items.Count + " items - 500 at most"); return 2; }
            string tmpl = string.Join(" ", cmd.Select(z => z.Contains(" ") ? "\"" + z + "\"" : z)); if (!tmpl.Contains("{}")) tmpl += " {}";
            var sem = new SemaphoreSlim(jobs); int doneN = 0, worst = 0, running = 0; var threads = new List<Thread>(); var outLock = new object();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var it in items)
            {
                if (c.Job.Cancel) break;
                sem.Wait(); if (c.Job.Cancel) { sem.Release(); break; }
                string line = tmpl.Replace("{}", it.Contains(" ") ? "\"" + it + "\"" : it); Interlocked.Increment(ref running);
                var th = new Thread(() =>
                {
                    var cap = new ShOut { Cap = new StringBuilder(), Job = c.Job }; int ex = 0;
                    try { ex = ShRunText(line, new ShCtx { Job = c.Job, Out = cap, Args = c.Args, Yes = c.Yes || yes, Script = true }); }
                    catch (Exception e) { if (e is OutOfMemoryException) throw; cap.Cap.Append("error: " + e.Message + "\n"); ex = 2; }
                    finally { Interlocked.Decrement(ref running); sem.Release(); }
                    lock (outLock) { doneN++; worst = Math.Max(worst, ex); OL(c, "── " + line + (ex == 0 ? "" : "   (exit " + ex + ")"), ex == 0 ? ShCyan : ShAmber); O(c, cap.Cap.ToString(), ShFg); }
                }) { IsBackground = true, Name = "parallel" };
                threads.Add(th); th.Start();
                if (c.Out.Screen && !c.Job.Bg) Ui(delegate { term.Live(new List<List<TermView.Seg>> { new List<TermView.Seg> { new TermView.Seg("parallel  ", ShCyan), new TermView.Seg(doneN + " / " + items.Count + " done · " + running + " running · " + sw.Elapsed.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s", ShDim) } }); });
            }
            while (threads.Any(z => z.IsAlive))
            {
                Thread.Sleep(200);
                if (c.Out.Screen && !c.Job.Bg) Ui(delegate { term.Live(new List<List<TermView.Seg>> { new List<TermView.Seg> { new TermView.Seg("parallel  ", ShCyan), new TermView.Seg(doneN + " / " + items.Count + " done · " + running + " running · " + sw.Elapsed.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s · Ctrl+C stops", ShDim) } }); });
                if (c.Job.Cancel && (DateTime.UtcNow - c.Job.CancelAt).TotalSeconds > 15) break;
            }
            if (c.Out.Screen && !c.Job.Bg) Ui(delegate { term.EndLive(); });
            OL(c, "parallel: " + doneN + " of " + items.Count + " done in " + sw.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s" + (worst > 0 ? " · some failed" : ""), worst > 0 ? ShAmber : ShGreen);
            return c.Job.Cancel ? 130 : worst;
        }
        // left out of alltools: they need you there (keys, files, screens), act on your phone, write something you keep, or cost a lot
        static readonly HashSet<string> ShSkip = new HashSet<string> { "newkey", "csrmake", "selfsigned", "encrypt", "decrypt", "recipients", "friend", "screentest", "keytest", "soundtest", "remote", "radiuslab", "wol", "keys", "mtu", "speedtest", "beforeafter" };
        int AllTools(bool yes, ShCtx c)
        {
            string gw; lock (M.L) gw = M.Gateway ?? "";
            int ran = 0, failed = 0, skipped = 0, pictures = 0; var log = new StringBuilder(); var cap = new ShOut { Cap = log, Job = c.Job };
            foreach (var t in shTools)
            {
                if (c.Job.Cancel) break;
                if (ShSkip.Contains(t.Id) || t.Group == "REMOTE") { skipped++; continue; }
                bool gate = LabGate.Contains(t.Id) || SenderGate.Contains(t.Id) || t.Id == "sweep"; if (gate && !yes) { skipped++; continue; }
                if (t.Do == null) { pictures++; continue; }
                var ex = Examples.Get(t.Id); var inp = ex != null ? ex.In : new string[0];
                if (t.Id == "diskspeed") inp = inp.Select(v => string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase) ? "" : v).ToArray();
                var words = inp.Select(v => Regex.Replace(v ?? "", @"\b(192\.0\.2|198\.51\.100|203\.0\.113)\.\d{1,3}\b", gw == "" ? "$0" : gw)).Select(v => v == "" ? "-" : v).ToList();
                OL(c, "── " + t.Id + " " + string.Join(" ", words), ShCyan); log.AppendLine("── " + t.Id + " " + string.Join(" ", words));
                int r = RunTool(t, words, new ShCtx { Job = c.Job, Out = cap, Yes = yes, Script = true }); ran++; if (r == 2) failed++;
                OL(c, r == 0 ? "   ok" : r == 1 ? "   ok (found problems)" : "   FAILED", r == 2 ? ShRed : ShDim);
            }
            string tail = (c.Job.Cancel ? "== STOPPED " : "== DONE ") + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " · " + ran + " run · " + failed + " failed · " + skipped + " left out" + (yes ? "" : " (test-traffic tools need --yes)") + " · " + pictures + " pictures (Toolbox only)";
            log.AppendLine(tail); OL(c, tail, failed > 0 || c.Job.Cancel ? ShAmber : ShGreen);
            string f = Path.Combine(Program.WriteDir, "alltools.out.txt"); try { File.WriteAllText(f, log.ToString()); OL(c, "transcript: " + f, ShDim); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            return failed > 0 ? 1 : 0;
        }

        // the self-test's way in: a line run to the end, its text back (no screen, no questions)
        internal string TestShell(string text, out int exit)
        {
            if (shTools == null) shTools = Tools2();
            var job = new ShJob(); var o = new ShOut { Cap = new StringBuilder(), Job = job };
            exit = ShRunText(text, new ShCtx { Job = job, Out = o, Script = true }); return o.Cap.ToString().Replace("\r", "");
        }
        internal void TestShellReset() { shCwd = shPrev = "/home"; lock (shVars) shVars.Clear(); }

        // iperf3 the way you'd type it on Linux (Adeeb, 22 Nov: "why can't I do iperf3 from the cli"): -c host, -p port, -R, --bidir,
        // -t, -P, -u, -b, -l, -w, -S, -O, -M, -4, -6 - turned into the iperf tool's boxes. AdeeboMeter's own iperf3 client runs it;
        // it is only ever the client, so -s says where the server goes instead.
        int Iperf3(List<string> a, ShCtx c)
        {
            if (a.Count == 0 || a.Contains("-h") || a.Contains("--help")) { OL(c, "usage: iperf3 -c <server> [-p port] [-R | --bidir] [-t secs] [-P streams] [-u -b 50M] [-l len] [-w win] [-S tos] [-O secs] [-M mss] [-4|-6]", ShFg); OL(c, "AdeeboMeter is the client only - on the other machine run:  iperf3 -s", ShDim); return a.Count == 0 ? 2 : 0; }
            if (a.Contains("-s") || a.Contains("--server")) { Err(c, "iperf3: AdeeboMeter is only the client - it never listens. Run  iperf3 -s  on the other machine, then  iperf3 -c <its address>  here."); return 2; }
            string host = null, port = null, dir = "upload", secs = ""; var adv = new List<string>(); var keep = new List<string>();
            for (int i = 0; i < a.Count; i++)
            {
                string w = a[i]; Func<string> val = () => i + 1 < a.Count ? a[++i] : "";
                switch (w)
                {
                    case "-c": case "--client": host = val(); break;
                    case "-p": case "--port": port = val(); break;
                    case "-R": case "--reverse": dir = "download"; break;
                    case "--bidir": case "--both": dir = "both"; break;
                    case "-t": case "--time": secs = val(); break;
                    case "--yes": case "-y": keep.Add(w); break;
                    case "-P": case "-b": case "-l": case "-w": case "-S": case "-O": case "-M": case "--parallel": case "--bitrate": case "--length": case "--window": case "--tos": case "--omit": case "--set-mss": adv.Add(w); adv.Add(val()); break;
                    default:
                        if (w.StartsWith("-t", StringComparison.Ordinal) && w.Length > 2 && char.IsDigit(w[2])) { secs = w.Substring(2); break; }
                        if (!w.StartsWith("-", StringComparison.Ordinal) && host == null) { host = w; break; }   // iperf3 10.0.0.5 works too
                        adv.Add(w); break;   // -u -4 -6 and anything else: the tool says what it didn't understand
                }
            }
            if (string.IsNullOrEmpty(host)) { Err(c, "iperf3: which server? -c <address>   (on that machine: iperf3 -s)"); return 2; }
            if (!string.IsNullOrEmpty(port)) host = host.Contains(":") && !host.StartsWith("[", StringComparison.Ordinal) ? "[" + host + "]:" + port : host + ":" + port;
            var t = shTools.FirstOrDefault(x => x.Id == "iperf"); if (t == null) { Err(c, "iperf3: the iperf tool isn't in this build"); return 2; }
            var args = new List<string> { host, dir, secs == "" ? "-" : secs, adv.Count == 0 ? "-" : string.Join(" ", adv) }; args.AddRange(keep);
            O(c, "iperf3 → " + host + " · " + dir + (secs != "" ? " · " + secs + " s" : "") + (adv.Count > 0 ? " · " + string.Join(" ", adv) : "") + "\n", ShDim);
            return RunTool(t, args, c);
        }

        // ---------- nano / vim: our own editor on the shell's screen (TermEditor); the command waits until you leave it
        int Edit(bool vim, List<string> args, ShCtx c)
        {
            if (c.Script || c.Job.Bg || !c.Out.Screen) { Err(c, (vim ? "vim" : "nano") + ": needs you at the keyboard - not inside a script or a pipe"); return 1; }
            string arg = args.FirstOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal) && !z.StartsWith("+", StringComparison.Ordinal)), real = null, text = null;
            if (arg != null)
            {
                real = Real(Abs(arg)); if (real == null) { Err(c, (vim ? "vim" : "nano") + ": " + arg + ": pages and tools aren't files - use a path under ~, /data or /mnt"); return 1; }
                if (Directory.Exists(real)) { Err(c, (vim ? "vim" : "nano") + ": " + arg + " is a folder"); return 1; }
                if (File.Exists(real))
                {
                    if (new FileInfo(real).Length > 5 * 1048576) { Err(c, arg + ": over 5 MB - too big to edit here"); return 1; }
                    try { text = File.ReadAllText(real); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, arg + ": " + e.Message); return 1; }
                    if (text.Take(8000).Count(ch => ch == '\0') > 2) { Err(c, arg + ": a binary file - not editable as text"); return 1; }
                }
            }
            var done = new ManualResetEvent(false); string title0 = null;
            var ed = new TermEditor(vim, real, arg, text);
            ed.Words = new HashSet<string>(ShCmds.Concat(shTools.Select(t => t.Id)), StringComparer.OrdinalIgnoreCase);
            ed.Save = (to, body) =>
            {
                string r = to == real ? real : Real(Abs(to)); if (r == null) return "not a folder on a disk";
                try { string dir = Path.GetDirectoryName(r); if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) return "no folder " + dir; File.WriteAllText(r, body); real = r; return null; }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)) throw; return e.Message; }
            };
            ed.Closed = () => { term.Editor = null; if (title0 != null) term.Title = title0; term.Invalidate(); done.Set(); };
            Ui(delegate { term.EndLive(); title0 = term.Title; term.Title = (vim ? "vim" : "nano") + " — " + (arg ?? "new file") + "  ·  AdeeboShell"; term.Editor = ed; term.Invalidate(); term.Focus(); });
            done.WaitOne(); using (done) { }
            return 0;
        }
        // ready-made scripts in /data/scripts, written once (yours to change - deleted ones come back on the next start)
        void ShStarterScripts()
        {
            try
            {
                string d = Path.Combine(Program.DataDir, "scripts"); Directory.CreateDirectory(d);
                string f1 = Path.Combine(d, "five-at-a-time.sh"), f2 = Path.Combine(d, "every-tool.sh"), f3 = Path.Combine(d, "health-check.sh");
                if (!File.Exists(f1)) File.WriteAllText(f1, "# five-at-a-time.sh - ping five places together and merge the answers into one file\n# run it:  ./five-at-a-time.sh      edit it:  nano five-at-a-time.sh\n\nHOSTS=\"1.1.1.1 8.8.8.8 9.9.9.9 208.67.222.222 $GATEWAY\"\necho \"pinging: $HOSTS\"\nparallel -j 5 ping {} 10 ::: $HOSTS > merged.txt\necho \"merged into $PWD/merged.txt\"\ngrep -i \"loss\\|average\" merged.txt\n");
                if (!File.Exists(f3)) File.WriteAllText(f3, "# health-check.sh - the quick look, in order; stops at the first thing that's broken\n# run it:  ./health-check.sh\n\nstatus\nif ping $GATEWAY 4 > /dev/null; then echo \"router: ok\"; else echo \"router: NOT answering - start there\"; exit; fi\nping 1.1.1.1 4 && echo \"internet: ok\" || echo \"internet: NOT answering\"\ndig example.com && echo \"names: ok\" || echo \"names: NOT working\"\n");
                if (!File.Exists(f2) && shTools != null)
                {
                    var sb = new StringBuilder("# every-tool.sh - every AdeeboMeter tool with its example, one after another\n# run it:      ./every-tool.sh > all.txt          (test-traffic tools are skipped)\n#             ./every-tool.sh --yes > all.txt    (your own network: runs those too)\n# change any line, delete what you don't need, add your own addresses.\n\n");
                    foreach (var g in shTools.GroupBy(t => GroupName(t.Group)))
                    {
                        sb.Append("\n# ---- " + g.Key + "\n");
                        foreach (var t in g)
                        {
                            if (ShSkip.Contains(t.Id) || t.Group == "REMOTE" || t.Do == null) continue;
                            var ex = Examples.Get(t.Id); var inp = ex != null ? ex.In : new string[0];
                            if (t.Id == "diskspeed") inp = inp.Select(v => string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase) ? "" : v).ToArray();
                            var w = inp.Select(v => Regex.Replace(v ?? "", @"\b(192\.0\.2|198\.51\.100|203\.0\.113)\.\d{1,3}\b", "$$GATEWAY")).Select(v => v == "" ? "-" : v.Contains(" ") || v.Contains("|") || v.Contains(";") || v.Contains(">") ? "\"" + v.Replace("\"", "\\\"") + "\"" : v).ToList();
                            while (w.Count > 0 && w[w.Count - 1] == "-") w.RemoveAt(w.Count - 1);
                            sb.Append("echo \"== " + t.Id + "\"\n" + t.Id + (w.Count > 0 ? " " + string.Join(" ", w) : "") + "\n");
                        }
                    }
                    File.WriteAllText(f2, sb.ToString());
                }
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
        }

        // =====================================================================================================================
        // Tab: commands, then files and folders (with / for a folder), pages after open, tools after man
        void ShComplete()
        {
            string inp = term.Input; int caret = term.Caret; string before = inp.Substring(0, caret);
            int ws = Math.Max(before.LastIndexOf(' '), Math.Max(before.LastIndexOf('|'), before.LastIndexOf(';'))) + 1; string word = before.Substring(ws);
            string head = before.Substring(0, ws).TrimEnd(); string prevCmd = Regex.Match(head, @"(?:^|[|;&]\s*)(\S+)[^|;&]*$").Groups[1].Value.ToLowerInvariant();
            bool firstWord = head == "" || Regex.IsMatch(head, @"[|;&]$");
            var pool = new List<string>(); bool pathMode = false;
            if (firstWord && !word.Contains("/") && !word.StartsWith(".", StringComparison.Ordinal) && !word.StartsWith("~", StringComparison.Ordinal)) pool.AddRange(ShCmds.Concat(shTools.Select(t => t.Id)));
            else if ((prevCmd == "open" || prevCmd == "go") && !word.Contains("/")) pool.AddRange(ShPageIds());
            else if ((prevCmd == "man" || prevCmd == "help" || prevCmd == "which") && !word.Contains("/")) pool.AddRange(shTools.Select(t => t.Id).Concat(ShCmds));
            else if (prevCmd == "lab") pool.AddRange(new[] { "lan", "path", "wifi", "phone", "vpn", "new", "--ask" });
            if (pool.Count == 0 || word.Contains("/") || (!firstWord && prevCmd != "open" && prevCmd != "man" && prevCmd != "lab")) { pathMode = true; pool.AddRange(PathCandidates(word)); }
            var hits = pool.Where(z => z.StartsWith(word, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(z => z, StringComparer.OrdinalIgnoreCase).ToList();
            if (hits.Count == 0) return;
            string common = hits[0]; foreach (var h in hits) { int k = 0; while (k < common.Length && k < h.Length && char.ToLowerInvariant(common[k]) == char.ToLowerInvariant(h[k])) k++; common = common.Substring(0, k); }
            if (hits.Count == 1) { string add = hits[0] + (pathMode && hits[0].EndsWith("/", StringComparison.Ordinal) ? "" : " "); term.Input = inp.Substring(0, ws) + add + inp.Substring(caret); term.Caret = ws + add.Length; shTabTwice = false; term.Invalidate(); return; }
            if (common.Length > word.Length) { term.Input = inp.Substring(0, ws) + common + inp.Substring(caret); term.Caret = ws + common.Length; term.Invalidate(); return; }
            if (!shTabTwice) { shTabTwice = true; return; }
            // twice: show them, like bash
            var pr = term.PromptSegs; term.Write("\n", ShFg);
            var shownList = hits.Take(120).Select(h => pathMode ? h.Substring(h.TrimEnd('/').LastIndexOf('/') + 1) : h).ToList(); int colw = Math.Min(30, shownList.Max(z => z.Length) + 2), per = Math.Max(1, 100 / colw);
            for (int i = 0; i < shownList.Count; i++) { term.Write(shownList[i].PadRight(colw), shownList[i].EndsWith("/", StringComparison.Ordinal) ? ShBlue : ShFg); if ((i + 1) % per == 0 || i == shownList.Count - 1) term.Write("\n", ShFg); }
            if (hits.Count > 120) term.Write("... " + (hits.Count - 120) + " more\n", ShDim);
            foreach (var sg in pr) { } shTabTwice = false; term.Invalidate();
        }
        List<string> PathCandidates(string word)
        {
            var r = new List<string>(); string dirPart = word.Contains("/") ? word.Substring(0, word.LastIndexOf('/') + 1) : "";
            string v = Abs(dirPart == "" ? "." : dirPart);
            string real = Real(v);
            if (real == null) { bool k; foreach (var e in VirtualList(v, out k)) r.Add(dirPart + e[0] + (e[1] == "d" ? "/" : "")); return r; }
            try
            {
                if (!Directory.Exists(real)) return r;
                foreach (var d in Directory.EnumerateDirectories(real).Take(500)) r.Add(dirPart + Path.GetFileName(d) + "/");
                foreach (var f in Directory.EnumerateFiles(real).Take(1000)) r.Add(dirPart + Path.GetFileName(f));
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            return r;
        }
    }
}
