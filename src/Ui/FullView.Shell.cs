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
        List<Tool> shTools;
        volatile ShJob shFg; readonly List<ShJob> shJobs = new List<ShJob>(); int shJobN; readonly Dictionary<int, int> shDone = new Dictionary<int, int>();
        volatile ManualResetEvent shAskWait; string shAskAnswer; bool shTabTwice; string shPasteBlock;
        bool ShBusy { get { return shFg != null || shJobs.Any(j => !j.Done); } }   // the window's Esc guard (FullView.cs)

        // where the shell is: its folder, its variables, the last exit status. The interactive shell has one; $( ), a pipeline's
        // stages, background jobs and parallel's workers each work on a copy, so a cd or x=1 in there doesn't move yours
        // (shell scrutiny #8). A sourced script shares it, as in bash.
        sealed class ShEnv
        {
            public string Cwd = "/home", Prev = "/home"; public int LastExit, Depth;
            public readonly Dictionary<string, string> Vars = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<string, List<string>> Arrs = new Dictionary<string, List<string>>(StringComparer.Ordinal);   // a=(x y z)
            public readonly Dictionary<string, Node> Funcs = new Dictionary<string, Node>(StringComparer.Ordinal);        // name() { ...; }
            public ShEnv Clone()
            {
                var e = new ShEnv { Cwd = Cwd, Prev = Prev, LastExit = LastExit, Depth = Depth };
                lock (Vars) { foreach (var kv in Vars) e.Vars[kv.Key] = kv.Value; foreach (var kv in Arrs) e.Arrs[kv.Key] = kv.Value.ToList(); foreach (var kv in Funcs) e.Funcs[kv.Key] = kv.Value; }
                return e;
            }
        }
        readonly ShEnv shMain = new ShEnv();
        [ThreadStatic] static ShEnv tEnv;
        [ThreadStatic] static int loopDepth, substDepth, lastSubstExit, funcDepth;
        ShEnv Env { get { return tEnv ?? shMain; } }
        T InSub<T>(Func<T> f) { var saved = tEnv; tEnv = Env.Clone(); try { return f(); } finally { tEnv = saved; } }

        sealed class ShJob { public int Id; public string Cmd = ""; public volatile bool Cancel, Done, Abandoned; public bool Bg; public int Exit; public readonly List<Toolkit.Run> Runs = new List<Toolkit.Run>(); public readonly StringBuilder Saved = new StringBuilder(); public DateTime CancelAt = DateTime.MinValue; }
        // where output goes: the screen (coloured), or text for the next command in a pipe / a file. Errors have their own way
        // (shell scrutiny #2): dropped (2>/dev/null), kept for a file (2> f), or passed on to where they went before (ErrTo) -
        // never into the data a pipe hands on.
        sealed class ShOut { public StringBuilder Cap, ErrCap; public bool ErrNull, Tee; public long LastTee;   // Tee: captured AND shown as it comes (alltools)
        public ShOut ErrTo; public ShJob Job; public bool Screen { get { return Cap == null; } } }
        sealed class ShCtx { public ShJob Job; public ShOut Out; public string Stdin; public List<string> Args = new List<string>(); public bool Yes, Script; public Dictionary<string, string> Frame; public ShCtx With(ShOut o, string stdin) { return new ShCtx { Job = Job, Out = o, Stdin = stdin, Args = Args, Yes = Yes, Script = Script, Frame = Frame }; } }   // Frame: a function's local variables
        // exit / return / break / continue: unwinds to the script, the function of the loop that owns it (#6, #23)
        sealed class ShFlow : Exception { public readonly string Kind; public readonly int Code; public int Level; public ShFlow(string kind, int code, int level) : base(kind) { Kind = kind; Code = code; Level = level; } }
        sealed class ShTooBig : Exception { public ShTooBig() : base("more than 64 MB of output in one go - stopped (write it to a file, or narrow it with grep / head)") { } }
        const int ShMaxCap = 64 * 1024 * 1024;

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
        List<TermView.Seg> ShPromptSegs() { return new List<TermView.Seg> { new TermView.Seg(ShUser + "@" + ShHost, ShGreen), new TermView.Seg(":", ShFg), new TermView.Seg(ShShort(shMain.Cwd), ShBlue), new TermView.Seg("$ ", ShFg) }; }
        void ShPrompt()
        {
            term.PromptSegs = ShPromptSegs();
            term.Title = ShUser + "@" + ShHost + ": " + ShShort(shMain.Cwd) + "  —  AdeeboShell"; term.Secret = false;
            term.ShowInput = (shFg == null || shAskWait != null) && shPasteBlock == null; term.Invalidate();
        }
        void ShWelcome()
        {
            term.Write("  █▀█ █▀▄ █▀▀ █▀▀ █▄▄ █▀█   " + "AdeeboShell · AdeeboMeter " + Program.Version + "\n", ShCyan);
            term.Write("  █▀█ █▄▀ ██▄ ██▄ █▄█ █▄█   " + L.T("every tool is a command, every page is a folder") + "\n", ShCyan);
            term.Write("\n", ShFg);
            term.Write("  ls · cd pages · cd ~ · df -h · top · mtr 1.1.1.1 · parallel -j 5 ping {} 4 ::: 1.1.1.1 8.8.8.8 9.9.9.9\n", ShDim);
            term.Write("  " + L.T("help for everything · Tab completes · Ctrl+C stops · nothing here starts another program") + "\n\n", ShDim);
        }

        // ---------- the screen, from any thread. Writes are batched (one UI call per burst, not per line - #3), and a batch is
        // closed whenever another screen action is queued, so the order on screen is the order things happened.
        readonly object swLock = new object(); List<KeyValuePair<string, Color>> swBatch;
        void SW(string s, Color col)
        {
            List<KeyValuePair<string, Color>> b = null;
            lock (swLock) { if (swBatch == null) { swBatch = new List<KeyValuePair<string, Color>>(); b = swBatch; } swBatch.Add(new KeyValuePair<string, Color>(s, col)); }
            if (b == null) return;
            Ui0(delegate
            {
                List<KeyValuePair<string, Color>> items; lock (swLock) { if (swBatch == b) swBatch = null; items = b.ToList(); }
                var run = new StringBuilder(); Color rc = ShFg; bool any = false;
                foreach (var kv in items) { if (any && kv.Value != rc) { term.Write(run.ToString(), rc); run.Clear(); } rc = kv.Value; any = true; run.Append(kv.Key); }
                if (run.Length > 0) term.Write(run.ToString(), rc);
            });
        }
        void Ui0(Action a) { try { if (IsDisposed) return; if (InvokeRequired) BeginInvoke(a); else a(); } catch (InvalidOperationException) { } }
        void Ui(Action a) { lock (swLock) swBatch = null; Ui0(a); }
        void O(ShCtx c, string s, Color col)
        {
            if (c.Job != null && c.Job.Abandoned) return;   // let go of with ^C^C: nothing more from it (#34)
            if (c.Out.Screen) { if (c.Job != null && c.Job.Bg) { lock (c.Job.Saved) if (c.Job.Saved.Length < 4 * 1024 * 1024) c.Job.Saved.Append(s); } else SW(s, col); }
            else { lock (c.Out.Cap) { if (c.Out.Cap.Length + s.Length > ShMaxCap) throw new ShTooBig(); c.Out.Cap.Append(s); } if (c.Out.Tee) { c.Out.LastTee = DateTime.UtcNow.Ticks; SW(s, col); } }
        }
        void OL(ShCtx c, string s, Color col) { O(c, s + "\n", col); }
        void OL(ShCtx c, string s) { OL(c, s, ShFg); }
        void Err(ShCtx c, string s) { ErrRaw(c, s + "\n", ShRed); }
        void ErrRaw(ShCtx c, string s, Color col)
        {
            var o = c.Out;
            for (int guard = 0; guard < 64; guard++) { if (o.ErrNull) return; if (o.ErrCap != null) { lock (o.ErrCap) o.ErrCap.Append(s); if (o.Tee) { o.LastTee = DateTime.UtcNow.Ticks; SW(s, col); } return; } if (o.ErrTo == null) break; o = o.ErrTo; }
            O(c.With(o, c.Stdin), s, col);
        }
        ShOut ShCapture(ShCtx c) { return new ShOut { Cap = new StringBuilder(), Job = c.Job, ErrTo = c.Out }; }

        // ---------- keys
        void ShChar(object s, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar) || shPasteBlock != null) { e.Handled = shPasteBlock != null; return; }
            e.Handled = true; term.ClearSel(); term.Follow();
            term.Input = term.Input.Insert(term.Caret, e.KeyChar.ToString()); term.Caret++; shTabTwice = false; term.Invalidate();
        }
        // a pasted block of lines is shown first and runs only on Enter (#48: "copy this into AdeeboShell" can't run on its own)
        void ShPaste()
        {
            string t; try { t = Clipboard.GetText(); } catch (Exception e) { if (!(e is System.Runtime.InteropServices.ExternalException || e is ThreadStateException)) throw; return; }
            if (string.IsNullOrEmpty(t) || shPasteBlock != null) return; t = t.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = t.Split('\n').Where(z => z.Trim() != "").ToList();
            if (lines.Count <= 1 || shAskWait != null || shFg != null) { string one = string.Join(" ", lines).Replace("\t", " "); term.Input = term.Input.Insert(term.Caret, one); term.Caret += one.Length; term.Invalidate(); return; }
            shPasteBlock = (term.Input.Trim() != "" ? term.Input + "\n" : "") + string.Join("\n", lines); term.Input = ""; term.Caret = 0;
            term.Write("\n", ShFg);
            foreach (var l in lines.Take(14)) term.Write("  │ " + l + "\n", ShDim);
            if (lines.Count > 14) term.Write("  │ … " + (lines.Count - 14) + " more lines\n", ShDim);
            term.Write("Pasted " + lines.Count + " lines. Enter runs them · Esc cancels\n", ShAmber);
            ShPrompt();
        }
        void ShKey(object s, KeyEventArgs e)
        {
            var k = e.KeyCode; string inp = term.Input; int c = term.Caret;
            if (shPasteBlock != null)
            {
                e.SuppressKeyPress = true;
                if (k == Keys.Enter) { string b = shPasteBlock; shPasteBlock = null; ShSubmit(b); }
                else if (k == Keys.Escape || (e.Control && k == Keys.C)) { shPasteBlock = null; term.Write("not run\n", ShDim); ShPrompt(); }
                return;
            }
            if (k != Keys.Tab) shTabTwice = false;
            if (e.Control && k == Keys.C) { e.SuppressKeyPress = true; if (term.Copy()) return; ShInterrupt(); return; }
            if (e.Control && k == Keys.V) { e.SuppressKeyPress = true; ShPaste(); return; }
            if (e.Control && k == Keys.L) { e.SuppressKeyPress = true; term.Clear(); ShPrompt(); return; }
            if (e.Control && k == Keys.U) { e.SuppressKeyPress = true; term.Input = inp.Substring(c); term.Caret = 0; term.Invalidate(); return; }
            if (e.Control && k == Keys.K) { e.SuppressKeyPress = true; term.Input = inp.Substring(0, c); term.Invalidate(); return; }
            if (e.Control && k == Keys.A) { e.SuppressKeyPress = true; term.Caret = 0; term.Invalidate(); return; }
            if (e.Control && k == Keys.E) { e.SuppressKeyPress = true; term.Caret = inp.Length; term.Invalidate(); return; }
            if (e.Control && (k == Keys.W || k == Keys.Back)) { e.SuppressKeyPress = true; int a = c; while (a > 0 && inp[a - 1] == ' ') a--; while (a > 0 && inp[a - 1] != ' ' && inp[a - 1] != '/') a--; term.Input = inp.Remove(a, c - a); term.Caret = a; term.Invalidate(); return; }
            if (k == Keys.PageUp || k == Keys.PageDown) { e.SuppressKeyPress = true; term.ScrollPage(k == Keys.PageUp ? 1 : -1); return; }
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
                // #34: let go means let go - nothing more from it reaches the screen or a file
                if (fg.Cancel && (DateTime.UtcNow - fg.CancelAt).TotalSeconds > 2) { fg.Abandoned = true; term.EndLive(); term.Write("^C again - let go of it: it may still finish out of sight, but nothing more from it is shown or written\n", ShAmber); ShFinishFg(fg, 130); return; }
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
            string shown = ShMasked(line); term.Write(shown.Replace("\n", "\n> ") + "\n", ShFg);
            line = line.Trim(); shHi = -1; shDraft = null;
            if (line == "") { ShPrompt(); return; }
            string hist = shown.Replace("\n", " ; ").Replace("do ;", "do").Replace("then ;", "then").Replace("else ;", "else");   // history keeps it on one line
            shHist.Remove(hist); shHist.Add(hist); if (shHist.Count > 500) shHist.RemoveAt(0);
            var job = new ShJob { Id = 0, Cmd = hist }; shFg = job; term.ShowInput = false; term.Invalidate();
            var t = new Thread(() =>
            {
                int ex = 0;
                try { ex = ShRunText(line, new ShCtx { Job = job, Out = new ShOut { Job = job } }); }
                catch (OutOfMemoryException) { GC.Collect(); SW("shell: ran out of memory - that command was stopped (AdeeboMeter carries on)\n", ShRed); ex = 2; }   // #10: never the whole app
                catch (Exception e2) { Program.Log("shell: " + e2); SW("shell: " + e2.Message + "\n", ShRed); ex = 2; }
                Ui(delegate { if (shFg == job) ShFinishFg(job, ex); });
            }, 16 * 1024 * 1024) { IsBackground = true, Name = "adeeboshell" };   // a deep script has room; nesting is still capped (#56)
            t.Start();
        }
        void ShFinishFg(ShJob job, int ex)
        {
            job.Done = true; job.Exit = ex; shMain.LastExit = ex; if (shFg == job) shFg = null;
            term.EndLive(); if (!term.AtLineStart) term.Write("\n", ShFg);
            ShPrompt();
        }
        // a question from a running command (y/N, read): the answer comes back on the next Enter
        string ShAsk(string q, bool secret)
        {
            var w = new ManualResetEvent(false); shAskAnswer = null;
            Ui(delegate { term.EndLive(); term.Write(q, ShAmber); term.PromptSegs = new List<TermView.Seg>(); term.Secret = secret; shAskWait = w; term.ShowInput = true; term.Input = ""; term.Caret = 0; term.Invalidate(); });
            w.WaitOne(); using (w) { }
            Ui(delegate { term.Secret = false; ShPromptKeepHidden(); });
            return shAskAnswer;
        }
        void ShPromptKeepHidden() { term.PromptSegs = ShPromptSegs(); term.ShowInput = shFg == null; term.Invalidate(); }

        // ---------- secrets never shown (shared secret, community, password, code boxes). Carry-over C2: every place a command can
        // start - after ; | && || &, after time / then / do / else / !, after NAME=value, and inside $( ) - not only the first word.
        string ShMasked(string line)
        {
            if (shTools == null || string.IsNullOrEmpty(line)) return line ?? "";
            List<Tok> a; try { a = Lex(line); } catch (FormatException) { return line; }
            bool any = false, cmdPos = true;
            for (int i = 0; i < a.Count; i++)
            {
                var tk = a[i];
                if (tk.Op != null) { cmdPos = tk.Op == ";" || tk.Op == "|" || tk.Op == "&&" || tk.Op == "||" || tk.Op == "&"; continue; }
                if (tk.W.IndexOf('\u0005') >= 0) tk.W = Regex.Replace(tk.W, "\u0005([^\u0006]*)\u0006", m => { string inner = ShMasked(m.Groups[1].Value); if (inner != m.Groups[1].Value) any = true; return "\u0005" + inner + "\u0006"; });
                if (!cmdPos) continue;
                if (!tk.Q && (tk.W == "time" || tk.W == "then" || tk.W == "do" || tk.W == "else" || tk.W == "!" || tk.W == "if" || tk.W == "while" || tk.W == "nohup" || Regex.IsMatch(tk.W, "^[A-Za-z_][A-Za-z0-9_]*="))) continue;
                cmdPos = false;
                var t = shTools.FirstOrDefault(x => string.Equals(x.Id, tk.W, StringComparison.OrdinalIgnoreCase)); if (t == null) continue;
                int box = 0;
                for (int j = i + 1; j < a.Count && a[j].Op == null; j++) { if (a[j].W == "--yes" || a[j].W == "-y") continue; if (box < t.Inputs.Length && SecretInput(t.Inputs[box]) && a[j].W != "" && a[j].W != "-") { a[j].W = "****"; a[j].Q = false; any = true; } box++; }
            }
            return any ? Unlex(a) : line;
        }
        static string Unlex(IEnumerable<Tok> a)
        {
            return string.Join(" ", a.Select(z =>
            {
                if (z.Op != null) return z.Op == ";" ? ";" : z.Op;
                string w = Regex.Replace(z.W, "\u0005([^\u0006]*)\u0006", "$$($1)"); w = Regex.Replace(w, "\u0007([^\u0006]*)\u0006", "$$(($1))"); w = w.Replace('\u0001', '$');
                return z.Q || w.Contains(" ") || w == "" ? "\"" + w.Replace("\"", "\\\"") + "\"" : w;
            }));
        }

        // =====================================================================================================================
        // the language: words, operators, and the structures scripts use (for / while / if)
        // In a word: \u0001 is a literal $ ('...' or \$), \u0005...\u0006 a $(command), \u0007...\u0006 a $((sum)).
        sealed class Tok { public string W, Op, M; public bool Q; public List<Tok> Arr; }   // M: per character of W, '1' where it was quoted (case patterns)   // Arr: the words of a=( ... )
        static List<Tok> Lex(string s)
        {
            var l = new List<Tok>(); var sb = new StringBuilder(); var pm = new StringBuilder(); bool had = false, q = false, lit = false; char quote = '\0';
            Action pad = () => { while (pm.Length < sb.Length) pm.Append(lit ? '1' : '0'); };
            Action flush = () => { pad(); if (sb.Length > 0 || had) l.Add(new Tok { W = sb.ToString(), Q = q, M = pm.ToString() }); sb.Clear(); pm.Clear(); had = false; q = false; };
            for (int i = 0; i < s.Length; i++)
            {
                pad(); lit = quote != '\0';   // what this round appends is quoted if it's inside quotes
                char ch = s[i];
                // $(command), `command`, $((math)): kept whole for Expand
                if (quote != '\'' && ((ch == '$' && i + 1 < s.Length && s[i + 1] == '(') || ch == '`'))
                {
                    int end;
                    if (ch == '`') { end = -1; for (int j = i + 1; j < s.Length; j++) { if (s[j] == '\\') { j++; continue; } if (s[j] == '`') { end = j; break; } } if (end < 0) throw new FormatException("a ` isn't closed"); sb.Append('\u0005').Append(s, i + 1, end - i - 1).Append('\u0006'); i = end; had = true; continue; }
                    if (i + 2 < s.Length && s[i + 2] == '(' && (end = MathEnd(s, i + 3)) > 0) { sb.Append('\u0007').Append(s, i + 3, end - i - 3).Append('\u0006'); i = end + 1; had = true; continue; }
                    if ((end = ParenEnd(s, i + 2)) < 0) throw new FormatException("a $( isn't closed");
                    sb.Append('\u0005').Append(s, i + 2, end - i - 2).Append('\u0006'); i = end; had = true; continue;
                }
                if (quote != '\0')
                {
                    if (ch == quote) { quote = '\0'; continue; }
                    if (quote == '"' && ch == '\\' && i + 1 < s.Length && "\"\\$`".IndexOf(s[i + 1]) >= 0) { char n2 = s[++i]; sb.Append(n2 == '$' ? '\u0001' : n2); continue; }
                    if (quote == '"' && ch == '\\' && i + 1 < s.Length && s[i + 1] == '\n') { i++; continue; }
                    if (quote == '\'' && ch == '$') { sb.Append('\u0001'); continue; }
                    sb.Append(ch); continue;
                }
                if (ch == '"' || ch == '\'') { quote = ch; had = true; q = true; continue; }
                if (ch == '\\' && i + 1 < s.Length) { char n2 = s[++i]; if (n2 == '\n') continue; lit = true; sb.Append(n2 == '$' ? '\u0001' : n2); had = true; q = true; continue; }
                if (ch == '#' && sb.Length == 0 && !had) { while (i + 1 < s.Length && s[i + 1] != '\n') i++; continue; }   // a comment: to the end of its line only (#1)
                if (ch == ' ' || ch == '\t') { flush(); continue; }
                if (ch == '\n') { flush(); l.Add(new Tok { Op = ";" }); continue; }
                string two = i + 1 < s.Length ? s.Substring(i, 2) : "";
                if (ch == '2' && sb.Length == 0 && !had && two == "2>")   // 2> 2>> 2>&1: where the errors go
                {
                    flush(); string rest = s.Substring(i);
                    if (rest.StartsWith("2>&1", StringComparison.Ordinal)) { l.Add(new Tok { Op = "2>&1" }); i += 3; }
                    else if (rest.StartsWith("2>>", StringComparison.Ordinal)) { l.Add(new Tok { Op = "2>>" }); i += 2; }
                    else { l.Add(new Tok { Op = "2>" }); i += 1; }
                    continue;
                }
                if (two == "&>") { flush(); l.Add(new Tok { Op = "&>" }); i++; continue; }
                if (two == ">&" && i + 2 < s.Length && s[i + 2] == '2') { flush(); l.Add(new Tok { Op = ">&2" }); i += 2; continue; }   // #21: this output goes the errors' way
                if (two == ">&" && i + 2 < s.Length && s[i + 2] == '1') { flush(); i += 2; continue; }
                if (two == "&&" || two == "||" || two == ">>") { flush(); l.Add(new Tok { Op = two }); i++; continue; }
                if (two == ";;") { flush(); l.Add(new Tok { Op = ";;" }); i++; continue; }   // the end of a case arm
                if (ch == '|' || ch == ';' || ch == '&' || ch == '>' || ch == '<' || ch == '(' || ch == ')') { flush(); l.Add(new Tok { Op = ch.ToString() }); continue; }
                sb.Append(ch);
            }
            if (quote != '\0') throw new FormatException("a " + quote + " isn't closed");   // #51: a syntax error, not plausible output
            flush(); return l;
        }
        // the ) that closes a $( - quotes and nested ( ) respected; -1 when it never closes
        static int ParenEnd(string s, int from)
        {
            int depth = 1; char q = '\0';
            for (int j = from; j < s.Length; j++)
            {
                char ch = s[j];
                if (q != '\0') { if (ch == q) q = '\0'; else if (ch == '\\' && q == '"') j++; continue; }
                if (ch == '\'' || ch == '"') { q = ch; continue; }
                if (ch == '\\') { j++; continue; }
                if (ch == '(') depth++; else if (ch == ')' && --depth == 0) return j;
            }
            return -1;
        }
        // the first ) of the )) that closes a $((
        static int MathEnd(string s, int from)
        {
            int depth = 0;
            for (int j = from; j < s.Length; j++) { if (s[j] == '(') depth++; else if (s[j] == ')') { if (depth == 0) return j + 1 < s.Length && s[j + 1] == ')' ? j : -1; depth--; } }
            return -1;
        }
        abstract class Node { }
        sealed class NCmd : Node { public List<Tok> Words = new List<Tok>(); }
        sealed class NPipe : Node { public List<Node> Cmds = new List<Node>(); public bool Bg; public string Text; }   // a stage: a command, or a whole for/while/if (for ... done | sort)
        sealed class NAndOr : Node { public List<Node> Parts = new List<Node>(); public List<string> Ops = new List<string>(); }
        sealed class NSeq : Node { public List<Node> Items = new List<Node>(); }
        sealed class NFor : Node { public string Var; public List<Tok> In = new List<Tok>(); public NSeq Body; }
        sealed class NWhile : Node { public NSeq Cond, Body; public bool Until; }
        sealed class NIf : Node { public List<NSeq> Conds = new List<NSeq>(), Bodies = new List<NSeq>(); public NSeq Else; }
        sealed class NCase : Node { public Tok Word; public List<KeyValuePair<List<Tok>, NSeq>> Arms = new List<KeyValuePair<List<Tok>, NSeq>>(); }
        sealed class NFunc : Node { public string Name; public Node Body; }
        sealed class NGroup : Node { public NSeq Body; }       // { ...; } - in this shell
        sealed class NSubshell : Node { public NSeq Body; }    // ( ... ) - in a copy of it
        sealed class NRedir : Node { public Node Inner; public List<Tok> Redirs = new List<Tok>(); }   // for ... done > file (#9)
        static bool NeedsTarget(string op) { return op == ">" || op == ">>" || op == "<" || op == "2>" || op == "2>>" || op == "&>"; }
        static bool IsRedir(string op) { return op != null && (NeedsTarget(op) || op == "2>&1" || op == ">&2"); }
        sealed class Parser
        {
            readonly List<Tok> t; int i, depth;
            public Parser(List<Tok> toks) { t = toks; }
            bool Sep() { return i < t.Count && t[i].Op == ";"; }
            bool Kw(params string[] k) { return i < t.Count && t[i].Op == null && !t[i].Q && k.Contains(t[i].W); }
            string Expect(params string[] k) { if (!Kw(k)) throw new FormatException("expected " + string.Join(" or ", k) + (i < t.Count ? " near '" + (t[i].Op ?? t[i].W) + "'" : " at the end")); return t[i++].W; }
            void Deeper() { if (++depth > 120) throw new FormatException("nested more than 120 deep"); }   // #56
            public NSeq List(params string[] stop)
            {
                var s = new NSeq();
                while (true)
                {
                    while (Sep()) i++;
                    if (i >= t.Count || Kw(stop) || (t[i].Op != null && stop.Contains(t[i].Op))) return s;
                    int st = i; var stmt = Statement();
                    if (i < t.Count && t[i].Op == "&")   // #32: anything can go to the background - a loop too
                    {
                        var p = stmt as NPipe; if (p == null || p.Cmds.Count == 0) p = new NPipe { Cmds = { stmt } };
                        p.Bg = true; p.Text = Unlex(t.GetRange(st, i - st)); s.Items.Add(p); i++;
                    }
                    else
                    {
                        s.Items.Add(stmt);
                        if (i < t.Count && !Sep() && !Kw(stop) && !(t[i].Op != null && stop.Contains(t[i].Op))) throw new FormatException("unexpected '" + (t[i].Op ?? t[i].W) + "'");   // echo (hi), done foo - but "cmd & next" is fine
                    }
                }
            }
            Node Statement()
            {
                Deeper();
                var ao = new NAndOr(); ao.Parts.Add(Pipe());
                while (i < t.Count && (t[i].Op == "&&" || t[i].Op == "||")) { ao.Ops.Add(t[i++].Op); while (Sep()) i++; ao.Parts.Add(Pipe()); }
                depth--;
                return ao.Parts.Count == 1 ? ao.Parts[0] : ao;
            }
            NSeq Cond(string word, params string[] stop) { var c = List(stop); if (c.Items.Count == 0) throw new FormatException(word + " needs a condition"); return c; }   // #54
            Node Compound()
            {
                Node n;
                if (Kw("for"))
                {
                    i++; var f = new NFor(); if (i >= t.Count || t[i].Op != null) throw new FormatException("for needs a name"); f.Var = t[i++].W;
                    if (Kw("in")) { i++; while (i < t.Count && t[i].Op == null && !Kw("do")) f.In.Add(t[i++]); }
                    else f.In.Add(new Tok { W = "\u0009@" });   // for x; do: the script's arguments
                    while (Sep()) i++; Expect("do"); f.Body = List("done"); Expect("done"); n = f;
                }
                else if (Kw("while", "until")) { bool until = t[i].W == "until"; i++; var w = new NWhile { Cond = Cond(until ? "until" : "while", "do"), Until = until }; Expect("do"); w.Body = List("done"); Expect("done"); n = w; }
                else if (Kw("if"))
                {
                    i++; var f = new NIf(); f.Conds.Add(Cond("if", "then")); Expect("then"); f.Bodies.Add(List("elif", "else", "fi"));
                    while (Kw("elif")) { i++; f.Conds.Add(Cond("elif", "then")); Expect("then"); f.Bodies.Add(List("elif", "else", "fi")); }
                    if (Kw("else")) { i++; f.Else = List("fi"); }
                    Expect("fi"); n = f;
                }
                else if (Kw("case"))
                {
                    i++; if (i >= t.Count || t[i].Op != null) throw new FormatException("case needs a word"); var cs = new NCase { Word = t[i++] };
                    while (Sep()) i++; Expect("in");
                    while (true)
                    {
                        while (Sep()) i++;
                        if (Kw("esac")) { i++; break; }
                        if (i >= t.Count) throw new FormatException("case without esac");
                        if (t[i].Op == "(") i++;
                        var pats = new List<Tok>();
                        while (true) { if (i >= t.Count || t[i].Op != null) throw new FormatException("case: a pattern is missing"); pats.Add(t[i++]); if (i < t.Count && t[i].Op == "|") { i++; continue; } break; }
                        if (i >= t.Count || t[i].Op != ")") throw new FormatException("case: expected ) after the pattern");
                        i++; var body = List(";;", "esac"); cs.Arms.Add(new KeyValuePair<List<Tok>, NSeq>(pats, body));
                        if (i < t.Count && t[i].Op == ";;") i++;
                    }
                    n = cs;
                }
                else if (Kw("function") || (i + 2 < t.Count && t[i].Op == null && !t[i].Q && Regex.IsMatch(t[i].W, "^[A-Za-z_][A-Za-z0-9_.-]*$") && t[i + 1].Op == "(" && t[i + 2].Op == ")"))
                {
                    if (Kw("function")) i++;
                    if (i >= t.Count || t[i].Op != null) throw new FormatException("function needs a name");
                    string name = t[i++].W; if (i + 1 < t.Count && t[i].Op == "(" && t[i + 1].Op == ")") i += 2;
                    while (Sep()) i++;
                    if (!Kw("{") && !(i < t.Count && t[i].Op == "(")) throw new FormatException(name + "(): the body goes in { }");
                    return new NFunc { Name = name, Body = Compound() };
                }
                else if (Kw("{")) { i++; var g = List("}"); Expect("}"); n = new NGroup { Body = g }; }
                else if (i < t.Count && t[i].Op == "(") { i++; var g = List(")"); if (i >= t.Count || t[i].Op != ")") throw new FormatException("a ( isn't closed"); i++; n = new NSubshell { Body = g }; }
                else return Cmd();
                // redirects after done / fi belong to the whole loop or if (#9)
                if (i < t.Count && IsRedir(t[i].Op))
                {
                    var r = new NRedir { Inner = n };
                    while (i < t.Count && IsRedir(t[i].Op)) { var op = t[i++]; r.Redirs.Add(op); if (NeedsTarget(op.Op)) { if (i >= t.Count || t[i].Op != null) throw new FormatException(op.Op + " needs a file name"); r.Redirs.Add(t[i++]); } }
                    return r;
                }
                return n;
            }
            NPipe Pipe()
            {
                int st = i; var p = new NPipe(); p.Cmds.Add(Compound());
                while (i < t.Count && t[i].Op == "|") { i++; while (Sep()) i++; p.Cmds.Add(Compound()); }
                p.Text = Unlex(t.GetRange(st, i - st));
                return p;
            }
            NCmd Cmd()
            {
                var c = new NCmd();
                while (i < t.Count && (t[i].Op == null || IsRedir(t[i].Op)))
                {
                    if (t[i].Op != null && !NeedsTarget(t[i].Op)) { c.Words.Add(t[i++]); continue; }   // 2>&1, >&2
                    if (t[i].Op != null) { var op = t[i++]; if (i >= t.Count || t[i].Op != null) throw new FormatException(op.Op + " needs a file name"); c.Words.Add(op); c.Words.Add(t[i++]); continue; }
                    if (!t[i].Q && Regex.IsMatch(t[i].W, @"^[A-Za-z_][A-Za-z0-9_]*\+?=$") && i + 1 < t.Count && t[i + 1].Op == "(")   // a=( x y z )
                    {
                        var arr = new Tok { W = t[i].W, Arr = new List<Tok>() }; i += 2;
                        while (i < t.Count && t[i].Op != ")") { if (t[i].Op == ";") { i++; continue; } if (t[i].Op != null) throw new FormatException("an array: unexpected '" + t[i].Op + "'"); arr.Arr.Add(t[i++]); }
                        if (i >= t.Count) throw new FormatException("an array's ( isn't closed"); i++; c.Words.Add(arr); continue;
                    }
                    c.Words.Add(t[i++]);
                }
                if (c.Words.Count == 0) throw new FormatException("a command is missing" + (i < t.Count ? " before '" + (t[i].Op ?? t[i].W) + "'" : ""));
                return c;
            }
        }
        int ShRunText(string text, ShCtx c) { return ShRunText(text, c, "adeeboshell: "); }
        int ShRunText(string text, ShCtx c, string who) { return ShRunText(text, c, who, false); }
        // passExit: a sourced script's exit ends the script that sourced it too (RG-4) - return still ends only the file
        int ShRunText(string text, ShCtx c, string who, bool passExit)
        {
            NSeq prog;
            try { prog = new Parser(Lex(text.Replace("\r", ""))).List(); }
            catch (FormatException fe) { Err(c, who + fe.Message); return 2; }
            try { return Exec(prog, c); }
            catch (ShFlow fl) { if (passExit && fl.Kind == "exit") throw; return fl.Kind == "exit" || fl.Kind == "return" ? fl.Code : 0; }
        }
        int Exec(Node n, ShCtx c)
        {
            if (c.Job.Cancel) return 130;
            var seq = n as NSeq; if (seq != null) { int r = 0; foreach (var it in seq.Items) { if (c.Job.Cancel) return 130; r = Exec(it, c); Env.LastExit = r; } return r; }
            var ao = n as NAndOr;
            if (ao != null) { int r = Exec(ao.Parts[0], c); Env.LastExit = r; for (int k = 0; k < ao.Ops.Count; k++) { if ((ao.Ops[k] == "&&") == (r == 0)) { r = Exec(ao.Parts[k + 1], c); Env.LastExit = r; } } return r; }   // #7: $? is right inside the chain
            var f = n as NFor;
            if (f != null)
            {
                List<string> items;
                if (f.In.Count == 1 && f.In[0].W == "\u0009@") items = c.Args.ToList();
                else { try { items = Expand(f.In, c).Select(z => z.StartsWith("\u0008", StringComparison.Ordinal) ? z.Substring(1) : z).ToList(); } catch (Exception e) { if (!(e is FormatException || e is DivideByZeroException)) throw; Err(c, "for: " + e.Message); return 1; } }
                int r = 0; loopDepth++;
                try
                {
                    foreach (var w in items)
                    {
                        if (c.Job.Cancel) return 130;
                        lock (Env.Vars) Env.Vars[f.Var] = w;
                        try { r = Exec(f.Body, c); }
                        catch (ShFlow fl) { if (fl.Kind != "break" && fl.Kind != "continue") throw; if (fl.Level > 1) { fl.Level--; throw; } if (fl.Kind == "break") break; }
                    }
                }
                finally { loopDepth--; }
                return r;
            }
            var wh = n as NWhile;
            if (wh != null)
            {
                int r = 0, guard = 0; loopDepth++;
                try
                {
                    while (!c.Job.Cancel && ((Exec(wh.Cond, c) == 0) != wh.Until))
                    {
                        try { r = Exec(wh.Body, c); }
                        catch (ShFlow fl) { if (fl.Kind != "break" && fl.Kind != "continue") throw; if (fl.Level > 1) { fl.Level--; throw; } if (fl.Kind == "break") break; }
                        if (++guard > 100000) { Err(c, (wh.Until ? "until" : "while") + ": stopped after 100,000 rounds"); return 1; }
                    }
                }
                finally { loopDepth--; }
                return c.Job.Cancel ? 130 : r;
            }
            var nf = n as NIf;
            if (nf != null) { for (int k = 0; k < nf.Conds.Count; k++) if (Exec(nf.Conds[k], c) == 0) return Exec(nf.Bodies[k], c); return nf.Else != null ? Exec(nf.Else, c) : 0; }
            var cs = n as NCase;
            if (cs != null)
            {
                string w; try { w = Tilde(Subst(cs.Word.W, c), cs.Word.Q).Replace('\u0001', '$'); } catch (FormatException e) { Err(c, "case: " + e.Message); return 1; }
                foreach (var arm in cs.Arms) foreach (var pat in arm.Key) if (PatternMatch(CasePattern(pat, c), w)) return Exec(arm.Value, c);
                return 0;
            }
            var fn = n as NFunc; if (fn != null) { lock (Env.Vars) Env.Funcs[fn.Name] = fn.Body; return 0; }
            var gr = n as NGroup; if (gr != null) return Exec(gr.Body, c);
            var sh = n as NSubshell; if (sh != null) return InSub(() => { try { return Exec(sh.Body, c); } catch (ShFlow fl) { if (fl.Kind == "exit") return fl.Code; throw; } });
            var rd = n as NRedir;
            if (rd != null) { List<string> w; try { w = Expand(rd.Redirs, c); } catch (FormatException e) { Err(c, "adeeboshell: " + e.Message); return 1; } return ApplyRedirs(w, c, cc => Exec(rd.Inner, cc)); }
            var p = n as NPipe; if (p == null) return 0;
            if (p.Bg) return ShBackground(p, c);
            if (p.Cmds.Count == 1) return Stage(p.Cmds[0], c);
            // a pipeline: every stage in its own copy of the folder and variables (#8); errors skip the data and go on (#2)
            string stdin = c.Stdin; int ex = 0;
            for (int k = 0; k < p.Cmds.Count; k++)
            {
                bool last = k == p.Cmds.Count - 1; var o = last ? c.Out : ShCapture(c); var node = p.Cmds[k]; var cc = c.With(o, stdin);
                ex = InSub(() => { try { return Stage(node, cc); } catch (ShFlow fl) { if (fl.Kind == "exit") return fl.Code; throw; } }); if (!last) stdin = o.Cap.ToString();   // RG-4: exit in a stage ends that stage, like bash's subshell
                if (c.Job.Cancel) return 130;
            }
            return ex;
        }
        int Stage(Node n, ShCtx c) { var cmd = n as NCmd; return cmd != null ? RunCmd(cmd, c) : Exec(n, c); }
        int ShBackground(NPipe p, ShCtx c)
        {
            var job = new ShJob { Id = Interlocked.Increment(ref shJobN), Bg = true, Cmd = p.Text ?? "(job)" }; lock (shJobs) shJobs.Add(job);
            Node body = p.Cmds.Count == 1 ? p.Cmds[0] : new NPipe { Cmds = p.Cmds, Text = p.Text };
            var env = Env.Clone(); var args = c.Args; bool yes = c.Yes;
            OL(c, "[" + job.Id + "] started  " + job.Cmd, ShDim);
            new Thread(() =>
            {
                tEnv = env; int ex = 0;
                try { ex = Exec(body, new ShCtx { Job = job, Out = new ShOut { Job = job }, Args = args, Yes = yes, Script = true }); }
                catch (ShFlow fl) { ex = fl.Code; }
                catch (OutOfMemoryException) { GC.Collect(); lock (job.Saved) job.Saved.Append("ran out of memory - stopped\n"); ex = 2; }
                catch (Exception e) { lock (job.Saved) job.Saved.Append("error: " + e.Message + "\n"); ex = 2; }
                job.Exit = ex; job.Done = true;
                lock (shJobs) { shJobs.Remove(job); shDone[job.Id] = ex; if (shDone.Count > 64) shDone.Remove(shDone.Keys.Min()); }   // #55: finished jobs leave the list
                string outp; lock (job.Saved) outp = job.Saved.ToString();
                Ui(delegate { bool hidden = shFg == null; term.EndLive(); if (!term.AtLineStart) term.Write("\n", ShFg); term.Write("[" + job.Id + "] done (exit " + ex + ")  " + job.Cmd + "\n", ex == 0 ? ShGreen : ShAmber); if (outp != "") term.Write(outp.EndsWith("\n", StringComparison.Ordinal) ? outp : outp + "\n", ShFg); if (hidden) ShPrompt(); });
            }) { IsBackground = true, Name = "adeeboshell job" }.Start();
            return 0;
        }

        // words -> the strings a command gets: $VARS, ${...}, $(cmd), $((sum)), ~, and * globs (not inside quotes).
        // An assignment word (NAME=value) comes back as \u0008NAME=value, unsplit and unglobbed (#25); a redirect's target must
        // expand to exactly one word, else it comes back as \u0004 "ambiguous" (#20).
        List<string> Expand(List<Tok> words, ShCtx c)
        {
            var r = new List<string>(); bool target = false;
            foreach (var w in words)
            {
                if (w.Op != null) { r.Add("\u0003" + w.Op); target = NeedsTarget(w.Op); continue; }
                string raw = w.W; bool hadVar = raw.IndexOf('$') >= 0 || raw.IndexOf('\u0005') >= 0 || raw.IndexOf('\u0007') >= 0;
                if (target)
                {
                    target = false; string v = Tilde(Subst(raw, c), w.Q).Replace('\u0001', '$');
                    if (v == "" || (hadVar && !w.Q && Regex.IsMatch(v, @"\s"))) { r.Add("\u0004" + Unlex(new[] { w })); continue; }
                    if (!w.Q && (v.Contains("*") || v.Contains("?"))) { var g = Glob(v); if (g.Count > 1) { r.Add("\u0004" + v); continue; } if (g.Count == 1) v = g[0]; }
                    r.Add(v); continue;
                }
                if (w.Arr != null)   // a=( ... ): \u000E name(+) \0 the words
                {
                    var items = Expand(w.Arr, c).Select(z => z.StartsWith("\u0008", StringComparison.Ordinal) ? z.Substring(1) : z);
                    r.Add("\u000E" + raw.TrimEnd('=') + "\u0000" + string.Join("\u001F", items)); continue;
                }
                var em = Regex.Match(raw, @"^([A-Za-z_][A-Za-z0-9_]*)\[([^\]]*)\]=(.*)$", RegexOptions.Singleline);   // a[i]=value
                if (em.Success) { r.Add("\u000F" + em.Groups[1].Value + "\u0000" + MathOf(Subst(em.Groups[2].Value, c), c).ToString(CultureInfo.InvariantCulture) + "\u0000" + Tilde(Subst(em.Groups[3].Value, c), w.Q).Replace('\u0001', '$')); continue; }
                var all = Regex.Match(raw, @"^\$\{([A-Za-z_][A-Za-z0-9_]*)\[[@*]\]\}$");   // "${a[@]}": one word per element
                if (all.Success) { List<string> arr; lock (Env.Vars) Env.Arrs.TryGetValue(all.Groups[1].Value, out arr); if (arr != null) { foreach (var el in arr.Where(z => z != null).ToList()) if (w.Q) r.Add(el); else foreach (var pt in el.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)) AddWord(r, pt, false); continue; } }
                var am = Regex.Match(raw, @"^([A-Za-z_][A-Za-z0-9_]*\+?)=");
                if (am.Success) { string val = Subst(raw.Substring(am.Length), c); val = Tilde(val, w.Q).Replace('\u0001', '$'); r.Add("\u0008" + am.Groups[1].Value + "=" + val); continue; }
                string s = Subst(raw, c);
                if (hadVar && !w.Q)
                {
                    var parts = s.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);   // HOSTS="a b c"; for h in $HOSTS - split like bash
                    if (parts.Length != 1) { foreach (var pt in parts) AddWord(r, pt.Replace('\u0001', '$'), false); continue; }
                }
                AddWord(r, Tilde(s, w.Q).Replace('\u0001', '$'), w.Q);
            }
            return r;
        }
        void AddWord(List<string> r, string s, bool quoted)
        {
            if (!quoted && (s.Contains("*") || s.Contains("?")) && !s.Contains("://")) { var g = Glob(s); if (g.Count > 0) { r.AddRange(g); return; } }
            r.Add(s);
        }
        static string Tilde(string s, bool quoted)
        {
            if (quoted) return s;
            if (s == "~" || s.StartsWith("~/", StringComparison.Ordinal)) return "/home" + s.Substring(1);
            return Regex.Replace(s, "(^|:)~(?=/|:|$)", "$1/home");   // PATH-style a:~/b
        }
        static readonly Regex VarRx = new Regex(@"\$(\{[^}]*\}|\?|#|@|\$|\d|[A-Za-z_][A-Za-z0-9_]*)");
        // $VARS in the plain parts, $(command) run and its output put in (trailing newlines off), $((math)) worked out
        string Subst(string s, ShCtx c)
        {
            var sb = new StringBuilder(); int i = 0;
            while (i < s.Length)
            {
                int a = s.IndexOfAny(new[] { '\u0005', '\u0007' }, i); string plain = a < 0 ? s.Substring(i) : s.Substring(i, a - i);
                sb.Append(VarRx.Replace(plain, m => { string g = m.Groups[1].Value; return g.StartsWith("{", StringComparison.Ordinal) ? Param(g.Substring(1, g.Length - 2), c) : Var(g, c); }));
                if (a < 0) break;
                int e = s.IndexOf('\u0006', a); if (e < 0) e = s.Length;
                string inner = s.Substring(a + 1, e - a - 1);
                if (s[a] == '\u0005')
                {
                    if (substDepth >= 40) throw new FormatException("$( ) nested more than 40 deep");   // #56
                    substDepth++; int saveLoop = loopDepth; loopDepth = 0;
                    try
                    {
                        var cap = ShCapture(c); var cc = new ShCtx { Job = c.Job, Out = cap, Args = c.Args, Yes = c.Yes, Script = true };
                        int ex = InSub(() => ShRunText(inner, cc)); Env.LastExit = ex; lastSubstExit = ex;
                        sb.Append(cap.Cap.ToString().Replace("\r", "").TrimEnd('\n'));
                    }
                    finally { substDepth--; loopDepth = saveLoop; }
                }
                else sb.Append(MathOf(MathPrep(inner, c), c).ToString(CultureInfo.InvariantCulture));
                i = e + 1;
            }
            return sb.ToString();
        }
        // ${NAME} ${#NAME} ${NAME:-w} ${NAME:=w} ${NAME:+w} ${NAME:?w} ${NAME#p} ${NAME##p} ${NAME%p} ${NAME%%p} ${NAME/p/r}
        // ${NAME//p/r} ${NAME:off:len} (#57) - a form it doesn't know is an error, not literal text
        string Param(string e, ShCtx c)
        {
            var ar = Regex.Match(e, @"^([#!])?([A-Za-z_][A-Za-z0-9_]*)\[([^\]]+)\]$");   // ${a[1]} ${a[@]} ${#a[@]} ${!a[@]} ${#a[2]}
            if (ar.Success)
            {
                List<string> arr; string nm = ar.Groups[2].Value, idx = ar.Groups[3].Value, pre = ar.Groups[1].Value;
                lock (Env.Vars) { Env.Arrs.TryGetValue(nm, out arr); arr = arr != null ? arr.ToList() : null; }
                if (arr == null) { string v0 = Var(nm, c); arr = v0 == "" ? new List<string>() : new List<string> { v0 }; }
                if (idx == "@" || idx == "*") return pre == "#" ? arr.Count(z => z != null).ToString(CultureInfo.InvariantCulture) : pre == "!" ? string.Join(" ", Enumerable.Range(0, arr.Count).Where(z => arr[z] != null)) : string.Join(" ", arr.Where(z => z != null));
                long k = MathOf(MathPrep(idx, c), c); if (k < 0) k += arr.Count;
                string el = (k >= 0 && k < arr.Count ? arr[(int)k] : null) ?? "";
                return pre == "#" ? el.Length.ToString(CultureInfo.InvariantCulture) : el;
            }
            if (e.Length > 1 && e[0] == '#' && Regex.IsMatch(e.Substring(1), @"^([A-Za-z_][A-Za-z0-9_]*|\d+)$")) return Var(e.Substring(1), c).Length.ToString(CultureInfo.InvariantCulture);
            var m = Regex.Match(e, @"^([A-Za-z_][A-Za-z0-9_]*|\d+|[?#@$])(.*)$", RegexOptions.Singleline);
            if (!m.Success) throw new FormatException("${" + e + "}: bad substitution");
            string name = m.Groups[1].Value, rest = m.Groups[2].Value, val = Var(name, c); bool set; lock (Env.Vars) set = Env.Vars.ContainsKey(name) || val != "";
            if (rest == "") return val;
            Func<string, string> word = w => Subst(w, c).Replace('\u0001', '$');
            var op = Regex.Match(rest, @"^(:?[-=+?])(.*)$", RegexOptions.Singleline);
            if (op.Success)
            {
                bool colon = op.Groups[1].Value.StartsWith(":", StringComparison.Ordinal), empty = colon ? val == "" : !set; string w = op.Groups[2].Value;
                switch (op.Groups[1].Value.TrimStart(':'))
                {
                    case "-": return empty ? word(w) : val;
                    case "=": if (empty) { string nv = word(w); lock (Env.Vars) Env.Vars[name] = nv; return nv; } return val;
                    case "+": return empty ? "" : word(w);
                    case "?": if (empty) throw new FormatException(name + ": " + (w == "" ? "not set" : word(w))); return val;
                }
            }
            var cut = Regex.Match(rest, @"^(##|#|%%|%)(.*)$", RegexOptions.Singleline);
            if (cut.Success)
            {
                var rx = new Regex("^" + GlobRx(word(cut.Groups[2].Value)) + "$", RegexOptions.Singleline, TimeSpan.FromSeconds(2)); string how = cut.Groups[1].Value;
                if (how == "#") { for (int k = 0; k <= val.Length; k++) if (rx.IsMatch(val.Substring(0, k))) return val.Substring(k); }
                else if (how == "##") { for (int k = val.Length; k >= 0; k--) if (rx.IsMatch(val.Substring(0, k))) return val.Substring(k); }
                else if (how == "%") { for (int k = val.Length; k >= 0; k--) if (rx.IsMatch(val.Substring(k))) return val.Substring(0, k); }
                else { for (int k = 0; k <= val.Length; k++) if (rx.IsMatch(val.Substring(k))) return val.Substring(0, k); }
                return val;
            }
            var rep = Regex.Match(rest, @"^(//?)([^/]*)(?:/(.*))?$", RegexOptions.Singleline);
            if (rep.Success)
            {
                var rx = new Regex(GlobRx(word(rep.Groups[2].Value)), RegexOptions.Singleline, TimeSpan.FromSeconds(2)); string to = word(rep.Groups[3].Value);
                return rep.Groups[1].Value == "//" ? rx.Replace(val, to.Replace("$", "$$")) : rx.Replace(val, to.Replace("$", "$$"), 1);
            }
            var sub = Regex.Match(rest, @"^:\s*(-?\d+)(?::(\d+))?$");
            if (sub.Success)
            {
                int off = int.Parse(sub.Groups[1].Value, CultureInfo.InvariantCulture); if (off < 0) off = Math.Max(0, val.Length + off); off = Math.Min(off, val.Length);
                int len = sub.Groups[2].Success ? int.Parse(sub.Groups[2].Value, CultureInfo.InvariantCulture) : val.Length - off;
                return val.Substring(off, Math.Max(0, Math.Min(len, val.Length - off)));
            }
            throw new FormatException("${" + e + "}: bad substitution");
        }
        static string GlobRx(string p) { return Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", "."); }
        // a case pattern as written: the quoted parts (and what their $vars hold) match literally, the rest is a glob - so
        // *"$x"* means "contains x", like bash
        string CasePattern(Tok pat, ShCtx c)
        {
            string w = pat.W, m = pat.M ?? new string(pat.Q ? '1' : '0', w.Length); var sb = new StringBuilder(); int i = 0;
            while (i < w.Length)
            {
                char kind = i < m.Length ? m[i] : '0'; int j = i; while (j < w.Length && (j < m.Length ? m[j] : '0') == kind) j++;
                string part = Subst(w.Substring(i, j - i), c).Replace('\u0001', '$');
                if (kind == '1') foreach (char ch in part) { if ("*?[]\\".IndexOf(ch) >= 0) sb.Append('\\'); sb.Append(ch); } else sb.Append(part);
                i = j;
            }
            return sb.ToString();
        }
        // a case pattern: * ? and [abc] / [!a-z] (^ is an ordinary member, as in bash)
        static bool PatternMatch(string pat, string text)
        {
            var sb = new StringBuilder("^");
            for (int i = 0; i < pat.Length; i++)
            {
                char ch = pat[i];
                if (ch == '*') sb.Append(".*"); else if (ch == '?') sb.Append('.');
                else if (ch == '[' && pat.IndexOf(']', i + 2) > 0) { int e = pat.IndexOf(']', i + 2); string set = pat.Substring(i + 1, e - i - 1); bool neg = set.StartsWith("!", StringComparison.Ordinal); if (neg) set = set.Substring(1); set = set.Replace("\\", "\\\\").Replace("^", "\\^"); sb.Append('[').Append(neg ? "^" : "").Append(set).Append(']'); i = e; }
                else if (ch == '\\' && i + 1 < pat.Length) sb.Append(Regex.Escape(pat[++i].ToString()));
                else sb.Append(Regex.Escape(ch.ToString()));
            }
            try { return Regex.IsMatch(text, sb.Append('$').ToString(), RegexOptions.Singleline, TimeSpan.FromSeconds(1)); } catch (ArgumentException) { return pat == text; }
        }
        // inside $(( )): $(command), `command` and ${...} worked out first - $(( n * $(fact $((n-1))) ))
        [ThreadStatic] static int mathPrepDepth;
        string MathPrep(string e, ShCtx c)
        {
            if (mathPrepDepth >= 40) throw new FormatException("$(( )) nested more than 40 deep");
            mathPrepDepth++; try { return MathPrep1(e, c); } finally { mathPrepDepth--; }
        }
        string MathPrep1(string e, ShCtx c)
        {
            if (e.IndexOf("$(", StringComparison.Ordinal) < 0 && e.IndexOf("${", StringComparison.Ordinal) < 0 && e.IndexOf('`') < 0) return e;
            var sb = new StringBuilder();
            for (int i = 0; i < e.Length; i++)
            {
                bool dollar = e[i] == '$' && i + 1 < e.Length && (e[i + 1] == '(' || e[i + 1] == '{');
                if (!dollar && e[i] != '`') { sb.Append(e[i]); continue; }
                if (e[i] == '`') { int q = e.IndexOf('`', i + 1); if (q < 0) throw new FormatException("a ` isn't closed"); sb.Append(Subst("\u0005" + e.Substring(i + 1, q - i - 1) + "\u0006", c)); i = q; continue; }
                char open = e[i + 1], close = open == '(' ? ')' : '}'; int d = 0, j = i + 1;
                for (; j < e.Length; j++) { if (e[j] == open) d++; else if (e[j] == close && --d == 0) break; }
                if (j >= e.Length) throw new FormatException("a $" + open + " isn't closed");
                string body = e.Substring(i + 2, j - i - 2);
                if (open == '{') sb.Append(Param(body, c));
                else if (body.StartsWith("(", StringComparison.Ordinal) && body.EndsWith(")", StringComparison.Ordinal)) sb.Append(MathOf(MathPrep(body.Substring(1, body.Length - 2), c), c).ToString(CultureInfo.InvariantCulture));
                else sb.Append(Subst("\u0005" + body + "\u0006", c).Trim());
                i = j;
            }
            return sb.ToString();
        }
        // $(( )): whole numbers; + - * / % ** ( ) == != < <= > >= && || ! and names or $names (empty = 0). && and || don't work out
        // the side they don't need (#24): $((x && 10/x)) is safe.
        long MathOf(string e, ShCtx c)
        {
            var toks = Regex.Matches(e, @"\s*(\d+|\$?[A-Za-z_][A-Za-z0-9_]*|\$\d|\*\*|==|!=|<=|>=|&&|\|\||[-+*/%()<>!])").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            if (string.Join("", toks) != Regex.Replace(e, @"\s+", "")) throw new FormatException("$((" + e + ")): not a sum this shell can do");
            { int dp = 0, run = 0; foreach (var t0 in toks) { if (t0 == "(") dp++; else if (t0 == ")") dp--; run = t0 == "-" || t0 == "+" || t0 == "!" || t0 == "(" ? run + 1 : 0; if (dp > 200 || run > 400) throw new FormatException("$(( )): nested more than 200 deep"); } }   // #56: a bounded depth, never a stack overflow
            int k = 0, skip = 0; Func<int, long> bin = null; Func<long> unary = null;
            Func<string, long> val = w => { long v; return long.TryParse(Var(w.TrimStart('$'), c).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; };
            unary = () =>
            {
                if (k >= toks.Count) throw new FormatException("$((" + e + ")): ends too soon");
                string t = toks[k++];
                if (t == "-") return -unary(); if (t == "+") return unary(); if (t == "!") return unary() == 0 ? 1 : 0;
                if (t == "(") { long v = bin(0); if (k >= toks.Count || toks[k] != ")") throw new FormatException("$((" + e + ")): a ( isn't closed"); k++; return v; }
                long n; if (long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
                if (Regex.IsMatch(t, @"^\$?[A-Za-z_0-9]")) return val(t);
                throw new FormatException("$((" + e + ")): unexpected " + t);
            };
            var prec = new Dictionary<string, int> { { "||", 1 }, { "&&", 2 }, { "==", 3 }, { "!=", 3 }, { "<", 4 }, { "<=", 4 }, { ">", 4 }, { ">=", 4 }, { "+", 5 }, { "-", 5 }, { "*", 6 }, { "/", 6 }, { "%", 6 }, { "**", 7 } };
            bin = min =>
            {
                long l = unary();
                while (k < toks.Count && prec.ContainsKey(toks[k]) && prec[toks[k]] >= min)
                {
                    string op = toks[k++];
                    if ((op == "&&" && l == 0) || (op == "||" && l != 0)) { skip++; bin(prec[op] + 1); skip--; l = op == "||" ? 1 : 0; continue; }
                    long r = bin(op == "**" ? prec[op] : prec[op] + 1);
                    switch (op)
                    {
                        case "+": l += r; break; case "-": l -= r; break; case "*": l *= r; break;
                        case "/": if (r == 0) { if (skip > 0) { l = 0; break; } throw new DivideByZeroException("$((" + e + ")): division by 0"); } l /= r; break;
                        case "%": if (r == 0) { if (skip > 0) { l = 0; break; } throw new DivideByZeroException("$((" + e + ")): division by 0"); } l %= r; break;
                        case "**": { long p0 = 1; for (long q = 0; q < r && q < 64; q++) p0 *= l; l = p0; break; }
                        case "==": l = l == r ? 1 : 0; break; case "!=": l = l != r ? 1 : 0; break;
                        case "<": l = l < r ? 1 : 0; break; case "<=": l = l <= r ? 1 : 0; break; case ">": l = l > r ? 1 : 0; break; case ">=": l = l >= r ? 1 : 0; break;
                        case "&&": l = l != 0 && r != 0 ? 1 : 0; break; case "||": l = l != 0 || r != 0 ? 1 : 0; break;
                    }
                }
                return l;
            };
            if (toks.Count == 0) return 0;
            long res = bin(0); if (k != toks.Count) throw new FormatException("$((" + e + ")): unexpected " + toks[k]);
            return res;
        }
        static readonly Random shRnd = new Random();
        string Var(string n, ShCtx c)
        {
            if (n == "?") return Env.LastExit.ToString(CultureInfo.InvariantCulture);
            if (n == "#") return c.Args.Count.ToString(CultureInfo.InvariantCulture);
            if (n == "@") return string.Join(" ", c.Args);
            if (n == "$") { using (var me = System.Diagnostics.Process.GetCurrentProcess()) return me.Id.ToString(CultureInfo.InvariantCulture); }
            if (n == "0") return "adeeboshell";
            int k; if (int.TryParse(n, out k)) return k >= 1 && k <= c.Args.Count ? c.Args[k - 1] : "";
            string v; List<string> av; lock (Env.Vars) { if (Env.Vars.TryGetValue(n, out v)) return v; if (Env.Arrs.TryGetValue(n, out av)) return av.Count > 0 ? av[0] ?? "" : ""; }   // $a: the first element
            switch (n) { case "HOME": return "/home"; case "PWD": return Env.Cwd; case "OLDPWD": return Env.Prev; case "USER": return ShUser; case "HOSTNAME": return ShHost; case "SHELL": return "/bin/adeeboshell"; case "GATEWAY": lock (M.L) return M.Gateway ?? ""; case "RANDOM": lock (shRnd) return shRnd.Next(32768).ToString(CultureInfo.InvariantCulture); }
            return "";
        }
        List<string> Glob(string pat)
        {
            var r = new List<string>(); string dirV = pat.Contains("/") ? pat.Substring(0, pat.LastIndexOf('/')) : "", name = pat.Substring(pat.LastIndexOf('/') + 1);
            if (dirV.Contains("*") || dirV.Contains("?")) return r;
            string av = Abs(dirV == "" ? (pat.StartsWith("/", StringComparison.Ordinal) ? "/" : ".") : dirV);
            var rx = new Regex("^" + GlobRx(name) + "$", RegexOptions.IgnoreCase);
            Func<string, bool> ok = z => rx.IsMatch(z) && (name.StartsWith(".", StringComparison.Ordinal) || !z.StartsWith(".", StringComparison.Ordinal));
            string pre = dirV == "" ? (pat.StartsWith("/", StringComparison.Ordinal) ? "/" : "") : dirV + "/";
            string real = Real(av);
            if (real == null) { bool known; foreach (var e in VirtualList(av, out known).Select(z => z[0]).Where(ok).OrderBy(z => z, StringComparer.OrdinalIgnoreCase)) r.Add(pre + e); return r; }   // #58: /pages/*, /proc/*
            if (!Directory.Exists(real)) return r;
            try { foreach (var e in Directory.EnumerateFileSystemEntries(real).Select(Path.GetFileName).Where(ok).OrderBy(z => z, StringComparer.OrdinalIgnoreCase)) r.Add(pre + e); }
            catch (Exception ex) { if (!(ex is IOException || ex is UnauthorizedAccessException)) throw; }
            return r;
        }

        // =====================================================================================================================
        // the folders: virtual paths -> real ones
        string Abs(string p)
        {
            p = (p ?? "").Replace('\\', '/');
            if (p == "" || p == ".") return Env.Cwd;
            if (p == "~" || p.StartsWith("~/", StringComparison.Ordinal)) p = "/home" + p.Substring(1);
            if (Regex.IsMatch(p, "^[A-Za-z]:(/|$)")) p = "/mnt/" + char.ToLowerInvariant(p[0]) + p.Substring(2);   // C:\Users -> /mnt/c/Users
            var parts = new List<string>(p.StartsWith("/", StringComparison.Ordinal) ? new string[0] : Env.Cwd.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
            foreach (var seg in p.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)) { if (seg == ".") continue; if (seg == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); continue; } parts.Add(seg); }
            return "/" + string.Join("/", parts);
        }
        static string Home { get { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } }
        // the real folder behind a path, or null for the virtual ones (/, /pages, /tools, /mnt, /proc)
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
        static readonly string[][] ShRoots = { new[] { "home", "your folder (" + Environment.UserName + ")" }, new[] { "data", "AdeeboMeter's own folder - history, reports, recordings" }, new[] { "mnt", "your drives" }, new[] { "pages", "every page - cd into one, then open" }, new[] { "tools", "every tool by group - each one is a command" }, new[] { "proc", "this PC as files - cpuinfo, meminfo, uptime, mounts" } };
        string ToolDir(string g) { return Regex.Replace(GroupName(g).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-'); }
        List<string> ShPageIds() { return Secs.Select(z => z[0]).Concat(new[] { "calc", "reports", "lab", "settings", "about" }).Distinct().ToList(); }
        // what's in a virtual folder: (name, is a folder, a word about it)
        List<string[]> VirtualList(string v, out bool known)
        {
            known = true; var seg = v.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); var r = new List<string[]>();
            if (seg.Length == 0) { foreach (var x in ShRoots) r.Add(new[] { x[0], "d", x[1] }); return r; }
            switch (seg[0].ToLowerInvariant())
            {
                case "proc": if (seg.Length == 1) { foreach (var f in ProcFiles) r.Add(new[] { f, "f", "" }); return r; } break;
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
        bool ProcExists(string v) { return v.StartsWith("/proc/", StringComparison.Ordinal) && ProcFiles.Contains(v.Substring(6)); }

        // =====================================================================================================================
        // one command: assignments (alone, or FOO=bar for this command only - #25), redirects, then a built-in or a tool
        int RunCmd(NCmd cmd, ShCtx c)
        {
            lastSubstExit = -1;
            List<string> words;
            try { words = Expand(cmd.Words, c); }
            catch (Exception e) { if (!(e is FormatException || e is DivideByZeroException || e is RegexMatchTimeoutException)) throw; Err(c, "adeeboshell: " + e.Message); return 1; }   // #24: an error ends this command, not the script
            var redirs = new List<string>(); var args = new List<string>(); var assigns = new List<KeyValuePair<string, string>>();
            for (int k = 0; k < words.Count; k++)
            {
                string w = words[k];
                if (w.StartsWith("\u0003", StringComparison.Ordinal)) { redirs.Add(w); if (NeedsTarget(w.Substring(1))) redirs.Add(k + 1 < words.Count ? words[++k] : "\u0004?"); continue; }
                if (w.StartsWith("\u000E", StringComparison.Ordinal) || w.StartsWith("\u000F", StringComparison.Ordinal)) { ArrayAssign(w, c, args.Count > 0 && args[0] == "local"); continue; }
                if (w.StartsWith("\u0008", StringComparison.Ordinal)) { string kv = w.Substring(1); if (args.Count == 0) { int eq = kv.IndexOf('='); assigns.Add(new KeyValuePair<string, string>(kv.Substring(0, eq), kv.Substring(eq + 1))); } else args.Add(kv); continue; }
                args.Add(w);
            }
            if (args.Count == 0)   // only assignments and/or redirects: > file still makes the file, < file still has to exist (#35)
            {
                lock (Env.Vars) foreach (var a in assigns) { if (a.Key.EndsWith("+", StringComparison.Ordinal)) { string k = a.Key.TrimEnd('+'), ov; Env.Vars.TryGetValue(k, out ov); if (ov == null) { List<string> av; if (Env.Arrs.TryGetValue(k, out av) && av.Count > 0) ov = av[0]; } Env.Vars[k] = (ov ?? "") + a.Value; } else Env.Vars[a.Key] = a.Value; }   // x+=more
                int st = lastSubstExit >= 0 ? lastSubstExit : 0;
                return redirs.Count == 0 ? st : ApplyRedirs(redirs, c, cc => st);
            }
            Dictionary<string, string> old = null;
            if (assigns.Count > 0) { old = new Dictionary<string, string>(); lock (Env.Vars) foreach (var a in assigns) { string ov; old[a.Key] = Env.Vars.TryGetValue(a.Key, out ov) ? ov : null; Env.Vars[a.Key] = a.Value; } }
            try { return redirs.Count == 0 ? Dispatch(args, c) : ApplyRedirs(redirs, c, cc => Dispatch(args, cc)); }
            finally { if (old != null) lock (Env.Vars) foreach (var kv in old) { if (kv.Value == null) Env.Vars.Remove(kv.Key); else Env.Vars[kv.Key] = kv.Value; } }
        }
        // a=( ... ) / a+=( ... ) / a[i]=value
        void ArrayAssign(string w, ShCtx c, bool local)
        {
            var parts = w.Substring(1).Split('\u0000'); string name = parts[0].TrimEnd('+'); bool append = parts[0].EndsWith("+", StringComparison.Ordinal);
            lock (Env.Vars)
            {
                if (local && c.Frame != null && !c.Frame.ContainsKey("\u000E" + name)) { List<string> o0; c.Frame["\u000E" + name] = Env.Arrs.TryGetValue(name, out o0) ? string.Join("\u001F", o0) : null; }
                List<string> arr; if (!Env.Arrs.TryGetValue(name, out arr)) { arr = new List<string>(); string sv; if (Env.Vars.TryGetValue(name, out sv)) { arr.Add(sv); Env.Vars.Remove(name); } Env.Arrs[name] = arr; }
                if (w[0] == '\u000E') { var items = parts[1] == "" ? new string[0] : parts[1].Split('\u001F'); if (!append) arr.Clear(); arr.AddRange(items); }
                else { long k; long.TryParse(parts[1], out k); if (k < 0) k += arr.Count; if (k < 0 || k > 100000) return; while (arr.Count <= k) arr.Add(null); arr[(int)k] = parts[2]; }   // null: a gap - a[5]=x after 3 items (bash's arrays are sparse)
                Env.Vars.Remove(name);
            }
        }
        // a function: its own $1 $2 $#, local variables put back after, return N
        int CallFunc(string name, Node body, List<string> args, ShCtx c)
        {
            if (funcDepth >= 100) { Err(c, name + ": functions called 100 deep - stopped (does it call itself forever?)"); return 1; }
            var frame = new Dictionary<string, string>(StringComparer.Ordinal); funcDepth++;
            try { return Exec(body, new ShCtx { Job = c.Job, Out = c.Out, Stdin = c.Stdin, Args = args, Yes = c.Yes, Script = c.Script, Frame = frame }); }
            catch (ShFlow fl) { if (fl.Kind == "return") return fl.Code; throw; }
            finally
            {
                funcDepth--;
                lock (Env.Vars) foreach (var kv in frame)
                    {
                        if (kv.Key.StartsWith("\u000E", StringComparison.Ordinal)) { string an = kv.Key.Substring(1); if (kv.Value == null) Env.Arrs.Remove(an); else Env.Arrs[an] = kv.Value.Split('\u001F').ToList(); }
                        else if (kv.Value == null) Env.Vars.Remove(kv.Key); else Env.Vars[kv.Key] = kv.Value;
                    }
            }
        }
        // > >> < 2> 2>> &> 2>&1 >&2, for a command or a whole loop. Errors keep going where they went unless redirected; 2>&1 sends
        // them along with the output (into a file or a pipe - #21); > f 2> f is one file, not one overwriting the other (#63).
        // RG-3: redirects in the order they're written, like bash - "2>&1 > f" sends errors where the output went BEFORE the
        // file (the screen), "> f 2>&1" sends both to f, and "> a > b" still creates (empties) a. A destination is the screen's
        // output, the screen's errors, /dev/null or a file; every file is emptied (>) or created (>>) as its redirect is met.
        sealed class RTarget { public string Kind, Path, Shown; }   // Kind: out, err, null, file
        int ApplyRedirs(List<string> r, ShCtx c, Func<ShCtx, int> body)
        {
            string inFile = null; var outT = new RTarget { Kind = "out" }; var errT = new RTarget { Kind = "err" };
            var files = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
            Func<string, bool, RTarget> open = (tg, app) =>
            {
                if (tg == "/dev/null") return new RTarget { Kind = "null" };
                string real = Real(Abs(tg)); if (real == null) { Err(c, "adeeboshell: " + tg + ": can't write there - only folders on a disk (~, /data, /mnt/...)"); return null; }
                if (Directory.Exists(real)) { Err(c, "adeeboshell: " + tg + ": is a folder"); return null; }
                string full; try { full = Path.GetFullPath(real); } catch (ArgumentException) { Err(c, "adeeboshell: " + tg + ": not a file name"); return null; } catch (NotSupportedException) { Err(c, "adeeboshell: " + tg + ": not a file name"); return null; }
                try { if (!app) File.WriteAllText(full, ""); else if (!File.Exists(full)) File.WriteAllText(full, ""); }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, "adeeboshell: " + tg + ": " + e.Message); return null; }
                if (!files.ContainsKey(full)) files[full] = new StringBuilder();
                return new RTarget { Kind = "file", Path = full, Shown = tg };
            };
            for (int k = 0; k < r.Count; k++)
            {
                string op = r[k].Substring(1), tg = NeedsTarget(op) && k + 1 < r.Count ? r[++k] : null;
                if (tg != null && tg.StartsWith("\u0004", StringComparison.Ordinal)) { Err(c, "adeeboshell: " + (tg.Length > 1 ? tg.Substring(1) : "") + ": ambiguous redirect - the name must be one word (quote it)"); return 1; }
                RTarget t;
                switch (op)
                {
                    case ">": case ">>": t = open(tg, op == ">>"); if (t == null) return 1; outT = t; break;
                    case "&>": t = open(tg, false); if (t == null) return 1; outT = t; errT = t; break;
                    case "2>": case "2>>": t = open(tg, op == "2>>"); if (t == null) return 1; errT = t; break;
                    case "2>&1": errT = outT.Kind == "out" ? new RTarget { Kind = "out" } : outT; break;
                    case ">&2": outT = errT.Kind == "err" ? new RTarget { Kind = "err" } : errT; break;
                    case "<": inFile = tg; break;
                }
            }
            if (inFile != null)
            {
                if (inFile == "/dev/null") c = c.With(c.Out, "");
                else { string t = ReadAll(inFile, c); if (t == null) return 1; c = c.With(c.Out, t); }   // #10, #73: capped like every read, and /proc too
            }
            var screenOut = new ShOut { Cap = c.Out.Cap, Job = c.Job };   // the output as it was, with nothing about errors
            StringBuilder toErr = null;
            var o = new ShOut { Job = c.Job };
            switch (outT.Kind)
            {
                case "out": o.Cap = c.Out.Cap; break;
                case "file": o.Cap = files[outT.Path]; break;
                case "err": o.Cap = toErr = new StringBuilder(); break;
                default: o.Cap = new StringBuilder(); break;   // /dev/null: kept, then thrown away
            }
            switch (errT.Kind)
            {
                case "null": o.ErrNull = true; break;
                case "file": o.ErrCap = files[errT.Path]; break;
                case "out": o.ErrTo = screenOut; break;
                default: o.ErrTo = c.Out; break;
            }
            int ex = body(c.With(o, c.Stdin));
            if (c.Job.Abandoned) return ex;
            if (toErr != null && toErr.Length > 0) ErrRaw(c, toErr.ToString(), ShRed);
            try { foreach (var f in files) if (f.Value.Length > 0) File.AppendAllText(f.Key, f.Value.ToString()); }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, "adeeboshell: " + e.Message); return 1; }
            return ex;
        }
        bool SameFile(string a, string b) { if (a == "/dev/null" || b == "/dev/null") return false; string x = Real(Abs(a)), y = Real(Abs(b)); return x != null && y != null && string.Equals(Path.GetFullPath(x), Path.GetFullPath(y), StringComparison.OrdinalIgnoreCase); }
        int Dispatch(List<string> a, ShCtx c)
        {
            string cmd = a[0], low = cmd.ToLowerInvariant(); var args = a.Skip(1).ToList();
            try
            {
                Node fb; lock (Env.Vars) Env.Funcs.TryGetValue(cmd, out fb);
                if (fb != null) return CallFunc(cmd, fb, args, c);
                if (cmd.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) || cmd.StartsWith("./", StringComparison.Ordinal)) return Source(cmd, args, c);
                int r = Builtin(low, args, c); if (r != int.MinValue) return r;
                if (low == "iperf3" || (low == "iperf" && args.Any(z => z.StartsWith("-", StringComparison.Ordinal) && z != "--yes" && z != "-y"))) return Iperf3(args, c);
                var t = shTools != null ? shTools.FirstOrDefault(x => string.Equals(x.Id, low, StringComparison.OrdinalIgnoreCase)) : null;
                if (t != null) return RunTool(t, args, c);
                var near = (shTools ?? new List<Tool>()).Select(x => x.Id).Concat(ShCmds).Where(w => w.StartsWith(low.Substring(0, Math.Min(2, low.Length)), StringComparison.OrdinalIgnoreCase)).Distinct().Take(6).ToList();
                Err(c, "adeeboshell: " + cmd + ": command not found" + (near.Count > 0 ? " - did you mean: " + string.Join(", ", near) + "?" : "  (help lists them)"));
                return 127;
            }
            catch (Exception e) { if (e is OutOfMemoryException || e is ThreadAbortException || e is ShFlow) throw; Err(c, cmd + ": " + e.Message); if (!(e is ShTooBig)) Program.Log("shell " + cmd + ": " + e); return 2; }
        }
        static readonly string[] ShCmds = { "help", "man", "tools", "ls", "ll", "cd", "pwd", "cat", "head", "tail", "grep", "sort", "uniq", "wc", "cp", "mv", "mkdir", "rm", "touch", "find", "tree", "echo", "printf", "open", "pages", "status", "debug", "log", "history", "clear", "copy", "save", "exit", "lab", "source", "alltools", "parallel", "jobs", "wait", "kill", "sleep", "seq", "date", "whoami", "hostname", "uname", "env", "export", "unset", "set", "read", "test", "[", "true", "false", "time", "basename", "dirname", "which", "type", "tee", "du", "stat", "hash", "nano", "vim", "vi", "iperf3", "df", "du", "free", "uptime", "ps", "top", "htop", "ip", "ifconfig", "route", "ss", "netstat", "lsblk", "lscpu", "nproc", "id", "who", "w", "watch", "cut", "tr", "sed", "awk", "xargs", "less", "more", "rev", "nl", "shuf", "base64", "md5sum", "sha1sum", "sha256sum", "traceroute", "tracert", "nslookup", "host", "curl", "wget", "rmdir", "realpath", "egrep", "fgrep", "printenv", "declare", "sha512sum", "gawk", "return", "break", "continue", "until", "most", "local", "typeset", "readonly" };

        int Builtin(string cmd, List<string> args, ShCtx c)
        {
            switch (cmd)
            {
                case "help": if (args.Count > 0) return Man(args[0], c); Help(c); return 0;
                case "man": if (args.Count == 0) { Err(c, "usage: man <tool or command>"); return 1; } return Man(args[0], c);
                case "tools": ShToolList(args.Count > 0 ? string.Join(" ", args) : "", c); return 0;
                case "pwd": OL(c, Env.Cwd + (Real(Env.Cwd) != null && !args.Contains("-L") ? "   (" + Real(Env.Cwd) + ")" : "")); return 0;
                case "cd": return Cd(args.Count == 0 ? "~" : args[0], c);
                case "ls": case "ll": case "dir": return Ls(args, cmd == "ll", c);
                case "tree": return Tree(args.FirstOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal)) ?? ".", c);
                case "cat": return Cat(args, c);
                case "head": case "tail": return HeadTail(cmd == "head", args, c);
                case "grep": case "egrep": case "fgrep": return Grep(cmd == "egrep" ? new[] { "-E" }.Concat(args).ToList() : cmd == "fgrep" ? new[] { "-F" }.Concat(args).ToList() : args, c);
                case "sort":
                    {
                        // sort -k N (a field, blank-separated or -t's), -t sep: the key is that field onwards, as GNU's -kN
                        int key = 0; string sep = null; var rest = new List<string>();
                        for (int k = 0; k < args.Count; k++)
                        {
                            string w = args[k]; Match km;
                            if ((km = Regex.Match(w, @"^-k(\d+)(?:,\d+)?$")).Success) { key = int.Parse(km.Groups[1].Value, CultureInfo.InvariantCulture); continue; }
                            if (w == "-k" && k + 1 < args.Count) { var km2 = Regex.Match(args[++k], @"^(\d+)"); if (km2.Success) key = int.Parse(km2.Value, CultureInfo.InvariantCulture); continue; }
                            if (w.StartsWith("-t", StringComparison.Ordinal) && w.Length > 2) { sep = w.Substring(2); continue; }
                            if (w == "-t" && k + 1 < args.Count) { sep = args[++k]; continue; }
                            rest.Add(w);
                        }
                        var fl = Flags(rest); var l = InLines(Files(rest), c); if (l == null) return 1;
                        Func<string, string> kf = z => { if (key <= 1) return z; var parts = sep != null ? z.Split(new[] { sep }, StringSplitOptions.None) : Regex.Split(z.Trim(), @"\s+"); return key - 1 < parts.Length ? string.Join(sep ?? " ", parts.Skip(key - 1)) : ""; };
                        bool rev = fl.Contains('r'), num = fl.Contains('n'), uq = fl.Contains('u'), ic = fl.Contains('f');
                        IEnumerable<string> s = num ? l.OrderBy(z0 => { string z = kf(z0); double d; return double.TryParse(Regex.Match(z, @"-?\d+(\.\d+)?").Value, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : double.MinValue; }) : l.OrderBy(z => kf(z), ic ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                        if (rev) s = s.Reverse(); if (uq) s = s.Distinct(ic ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal); foreach (var x in s) OL(c, x); return 0;
                    }
                case "uniq":   // #72: -c -d -u -i
                    {
                        var fl = Flags(args); var l = InLines(Files(args), c); if (l == null) return 1;
                        bool cnt = fl.Contains('c'), dup = fl.Contains('d'), uniqOnly = fl.Contains('u'); var cmp = fl.Contains('i') ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                        string prev = null; int n = 0;
                        Action fl0 = () => { if (prev == null) return; if (dup && n < 2) return; if (uniqOnly && n > 1) return; OL(c, cnt ? n.ToString(CultureInfo.InvariantCulture).PadLeft(7) + " " + prev : prev); };
                        foreach (var x in l) { if (prev != null && cmp.Equals(x, prev)) { n++; continue; } fl0(); prev = x; n = 1; }
                        fl0(); return 0;
                    }
                case "wc": return Wc(args, c);
                case "tee": { string txt = c.Stdin ?? ""; foreach (var f in Files(args)) { string r = Real(Abs(f)); if (r == null) { Err(c, "tee: " + f + ": can't write there"); return 1; } if (args.Contains("-a")) File.AppendAllText(r, txt); else File.WriteAllText(r, txt); } O(c, txt, ShFg); return 0; }
                case "echo":   // #31: -n leaves the newline off; -e reads \n \t \\ escapes
                    {
                        bool nl = true, esc = false; int k = 0;
                        while (k < args.Count && Regex.IsMatch(args[k], "^-[neE]+$")) { if (args[k].Contains("n")) nl = false; if (args[k].Contains("e")) esc = true; if (args[k].Contains("E")) esc = false; k++; }
                        string t = string.Join(" ", args.Skip(k)); if (esc) t = Escapes(t);
                        O(c, nl ? t + "\n" : t, ShFg); return 0;
                    }
                case "printf": return Printf(args, c);
                case "cp": case "mv": return CpMv(cmd == "mv", args, c);
                case "mkdir": { foreach (var d in Files(args)) { string r = Real(Abs(d)); if (r == null) { Err(c, "mkdir: " + d + ": not on a disk"); return 1; } if (Directory.Exists(r) && !args.Contains("-p")) { Err(c, "mkdir: " + d + ": already exists"); return 1; } Directory.CreateDirectory(r); } return 0; }
                case "touch":   // #91: flags are flags; -c never creates
                    {
                        bool noCreate = Flags(args).Contains('c');
                        foreach (var f in Files(args)) { string r = Real(Abs(f)); if (r == null) { Err(c, "touch: " + f + ": not on a disk"); return 1; } if (File.Exists(r)) File.SetLastWriteTime(r, DateTime.Now); else if (!noCreate) File.WriteAllText(r, ""); }
                        return 0;
                    }
                case "rm": return Rm(args, c);
                case "rmdir": { foreach (var d in Files(args)) { string v = Abs(d), r = Real(v); if (r == null || !Directory.Exists(r)) { Err(c, "rmdir: " + d + ": not a folder"); return 1; } if (v.Count(ch => ch == '/') <= 1 || r.TrimEnd('\\').Length <= 3) { Err(c, "rmdir: won't remove " + d); return 1; } if (Directory.EnumerateFileSystemEntries(r).Any()) { Err(c, "rmdir: " + d + ": not empty"); return 1; } Directory.Delete(r); } return 0; }
                case "find": return Find(args, c);
                case "stat": return Stat(args, c);
                case "basename": { if (args.Count == 0) return 0; string b = args[0].TrimEnd('/'); b = b.Substring(b.LastIndexOf('/') + 1); if (args.Count > 1 && b.EndsWith(args[1], StringComparison.Ordinal) && b != args[1]) b = b.Substring(0, b.Length - args[1].Length); OL(c, b); return 0; }
                case "dirname": OL(c, args.Count > 0 && args[0].TrimEnd('/').Contains("/") ? (args[0].TrimEnd('/').LastIndexOf('/') == 0 ? "/" : args[0].TrimEnd('/').Substring(0, args[0].TrimEnd('/').LastIndexOf('/'))) : "."); return 0;
                case "realpath": case "readlink": foreach (var f in Files(args)) { string v = Abs(f), r = Real(v); OL(c, r != null ? v + "   (" + r + ")" : v); } return 0;
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
                // #6: exit / return stop the script (with their code); typed at the prompt, exit also leaves the shell page
                case "exit": case "quit": case "logout": case "return":
                    {
                        int code = Env.LastExit; if (args.Count > 0 && !int.TryParse(args[0], out code)) { Err(c, cmd + ": " + args[0] + ": a number is needed"); code = 2; }
                        if (cmd != "return" && !c.Script && !c.Job.Bg) Ui(delegate { tab = "overview"; ShowTab(); SyncNav(); });
                        throw new ShFlow(cmd == "return" ? "return" : "exit", code & 255, 1);
                    }
                case "break": case "continue":   // #23
                    {
                        int lv = 1; if (args.Count > 0 && (!int.TryParse(args[0], out lv) || lv < 1)) { Err(c, cmd + ": " + args[0] + ": a loop count is needed"); return 1; }
                        if (loopDepth == 0) { Err(c, cmd + ": only meaningful in a for, while or until loop"); return 0; }
                        throw new ShFlow(cmd, 0, Math.Min(lv, loopDepth));
                    }
                case "lab": return ShLab(args, c);
                case "source": case ".": case "sh": case "bash": if (args.Count == 0) { Err(c, "usage: source <file.sh> [args] [--yes]"); return 1; } return Source(args[0], args.Skip(1).ToList(), c, cmd == "source" || cmd == ".");
                case "alltools": return AllTools(args.Contains("--yes"), c);
                case "parallel": return Parallel(args, c);
                case "less": case "more": case "most": return Less(args, c);
                case "jobs": lock (shJobs) foreach (var j in shJobs.Where(z => !z.Done)) OL(c, "[" + j.Id + "]  Running   " + j.Cmd, ShCyan); return 0;
                case "wait":   // #55: wait %n waits for that job
                    {
                        var ids = args.Select(w => { int id; return int.TryParse(w.TrimStart('%'), out id) ? id : -1; }).Where(z => z > 0).ToList(); int last = 0;
                        if (args.Count > 0 && ids.Count == 0) { Err(c, "wait: " + args[0] + ": not a job (%1 or 1)"); return 127; }
                        var until = DateTime.UtcNow.AddMinutes(60);
                        while (!c.Job.Cancel && DateTime.UtcNow < until)
                        {
                            bool any; lock (shJobs) any = shJobs.Any(z => !z.Done && z != c.Job && (ids.Count == 0 || ids.Contains(z.Id)));
                            if (!any) break; Thread.Sleep(100);
                        }
                        lock (shJobs) foreach (var id in ids) { int ex; if (shDone.TryGetValue(id, out ex)) last = ex; else if (!shJobs.Any(z => z.Id == id)) { Err(c, "wait: %" + id + ": no such job"); last = 127; } }
                        return c.Job.Cancel ? 130 : last;
                    }
                case "kill":
                    {
                        int ex0 = 0;
                        foreach (var w in args.Where(z => !Regex.IsMatch(z, "^-[0-9A-Z]+$")))
                        {
                            int id; if (!w.StartsWith("%", StringComparison.Ordinal) || !int.TryParse(w.Substring(1), out id)) { Err(c, "kill: " + w + ": only this shell's jobs (%1) - AdeeboShell never stops Windows programs"); ex0 = 1; continue; }
                            ShJob j; lock (shJobs) j = shJobs.FirstOrDefault(z => z.Id == id);
                            if (j == null) { bool fin; lock (shJobs) fin = shDone.ContainsKey(id); Err(c, "kill: %" + id + (fin ? ": already finished" : ": no such job")); ex0 = 1; continue; }
                            j.Cancel = true; j.CancelAt = DateTime.UtcNow; lock (j.Runs) foreach (var r in j.Runs) r.Stop = true; OL(c, "[" + id + "] stopping  " + j.Cmd, ShAmber);
                        }
                        return ex0;
                    }
                case "sleep": { double s; if (!double.TryParse((args.FirstOrDefault() ?? "1").TrimEnd('s'), NumberStyles.Float, CultureInfo.InvariantCulture, out s)) { Err(c, "sleep: a number of seconds"); return 1; } var until = DateTime.UtcNow.AddSeconds(Math.Min(3600, s)); while (!c.Job.Cancel && DateTime.UtcNow < until) Thread.Sleep(50); return c.Job.Cancel ? 130 : 0; }
                case "seq":
                    {
                        var nums = args.Where(z => !z.StartsWith("-", StringComparison.Ordinal) || Regex.IsMatch(z, @"^-\d")).Select(z => { long v; return long.TryParse(z, out v) ? (long?)v : null; }).ToList();
                        if (nums.Count == 0 || nums.Any(z => z == null)) { Err(c, "usage: seq [first [step]] last"); return 1; }
                        long a0 = nums.Count > 1 ? nums[0].Value : 1, st = nums.Count > 2 ? nums[1].Value : 1, b0 = nums[nums.Count - 1].Value; if (st == 0) st = 1; int n = 0;
                        for (long v = a0; st > 0 ? v <= b0 : v >= b0; v += st) { OL(c, v.ToString(CultureInfo.InvariantCulture)); if (++n >= 1000000) { Err(c, "seq: stopped at 1,000,000 numbers"); break; } }
                        return 0;
                    }
                case "date": OL(c, DateTime.Now.ToString(args.Count > 0 && args[0].StartsWith("+", StringComparison.Ordinal) ? args[0].Substring(1).Replace("%Y", "yyyy").Replace("%m", "MM").Replace("%d", "dd").Replace("%H", "HH").Replace("%M", "mm").Replace("%S", "ss").Replace("%F", "yyyy-MM-dd").Replace("%T", "HH:mm:ss") : "ddd d MMM yyyy HH:mm:ss", CultureInfo.CurrentCulture)); return 0;
                case "whoami": OL(c, ShUser); return 0;
                case "hostname": OL(c, ShHost); return 0;
                case "uname": OL(c, args.Contains("-a") ? "AdeeboShell " + ShHost + " " + Environment.OSVersion.Version + " " + Program.Version + " " + (Environment.Is64BitOperatingSystem ? "x86_64" : "i686") + " Windows" : "AdeeboShell"); return 0;
                case "env": case "set": case "printenv": if (cmd == "printenv" && args.Count > 0) { int miss = 0; foreach (var nm in args) { string pv = Var(nm, c); lock (Env.Vars) { if (!Env.Vars.ContainsKey(nm) && !Env.Arrs.ContainsKey(nm) && pv == "") { miss = 1; continue; } } OL(c, pv); } return miss; }   // printenv NAME: just its value
                    lock (Env.Vars) { foreach (var kv in Env.Vars.OrderBy(z => z.Key)) OL(c, kv.Key + "=" + kv.Value); foreach (var kv in Env.Arrs.OrderBy(z => z.Key)) OL(c, kv.Key + "=(" + string.Join(" ", kv.Value.Where(z => z != null).Select(ShQuote)) + ")"); if (cmd == "set") foreach (var kv in Env.Funcs.OrderBy(z => z.Key)) OL(c, kv.Key + " () { ... }"); } OL(c, "HOME=/home  PWD=" + Env.Cwd + "  USER=" + ShUser, ShDim); return 0;
                case "local":
                    {
                        if (c.Frame == null) { Err(c, "local: only inside a function"); return 1; }
                        foreach (var w in args.Where(z => !z.StartsWith("-", StringComparison.Ordinal)))
                        {
                            var m = Regex.Match(w, "^([A-Za-z_][A-Za-z0-9_]*)(?:=(.*))?$", RegexOptions.Singleline); if (!m.Success) { Err(c, "local: " + w + ": not a name"); continue; }
                            string nm = m.Groups[1].Value, ov;
                            lock (Env.Vars) { if (!c.Frame.ContainsKey(nm)) c.Frame[nm] = Env.Vars.TryGetValue(nm, out ov) ? ov : null; Env.Vars[nm] = m.Groups[2].Success ? m.Groups[2].Value : ""; }
                        }
                        return 0;
                    }
                case "export": case "declare": case "typeset": case "readonly": foreach (var w in args) { var m = Regex.Match(w, "^([A-Za-z_][A-Za-z0-9_]*)=(.*)$", RegexOptions.Singleline); if (m.Success) lock (Env.Vars) Env.Vars[m.Groups[1].Value] = m.Groups[2].Value; } return 0;
                case "unset":
                    lock (Env.Vars)
                        foreach (var w in args)
                        {
                            if (w == "-f" || w == "-v") continue;
                            var em = Regex.Match(w, @"^([A-Za-z_][A-Za-z0-9_]*)\[(\d+)\]$"); List<string> arr;
                            if (em.Success) { if (Env.Arrs.TryGetValue(em.Groups[1].Value, out arr)) { int k = int.Parse(em.Groups[2].Value, CultureInfo.InvariantCulture); if (k < arr.Count) arr[k] = null; } continue; }
                            if (args.Contains("-f")) Env.Funcs.Remove(w); else { Env.Vars.Remove(w); Env.Arrs.Remove(w); if (!args.Contains("-v")) Env.Funcs.Remove(w); }
                        }
                    return 0;
                case "read": return Read(args, c);
                case "test": case "[":   // #52: [ needs its ]
                    {
                        if (cmd == "[") { if (args.Count == 0 || args[args.Count - 1] != "]") { Err(c, "[: missing ]"); return 2; } args = args.Take(args.Count - 1).ToList(); }
                        try { return Test(args) ? 0 : 1; } catch (FormatException fe) { Err(c, cmd + ": " + fe.Message); return 2; }
                    }
                case "true": case ":": return 0;
                case "false": return 1;
                case "time":   // #67: the timing goes the errors' way, not into a pipe
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew(); int r = args.Count == 0 ? 0 : Dispatch(args, c);
                        ErrRaw(c, "real " + sw.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + " s\n", ShDim); return r;
                    }
                case "nano": case "vim": case "vi": case "edit": return Edit(cmd == "vim" || cmd == "vi", args, c);
                case "hash": { if (args.Count == 0) { Err(c, "usage: hash <file> - SHA-256 (sha256sum and the hashf tool do more)"); return 1; } return HashSum("sha256sum", args, c); }
            }
            return SysCmd(cmd, args, c);   // df free ps top ip ss ... (FullView.ShellSys.cs)
        }
        static string Flags(List<string> args) { return string.Concat(args.Where(z => z.Length > 1 && z.StartsWith("-", StringComparison.Ordinal) && !z.StartsWith("--", StringComparison.Ordinal) && !Regex.IsMatch(z, @"^-\d")).Select(z => z.Substring(1))); }
        static List<string> Files(List<string> args) { return args.Where(z => z == "-" || !z.StartsWith("-", StringComparison.Ordinal)).ToList(); }
        static string Escapes(string t)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] != '\\' || i + 1 >= t.Length) { sb.Append(t[i]); continue; }
                char n = t[++i];
                switch (n) { case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; case '\\': sb.Append('\\'); break; case 'r': sb.Append('\r'); break; case 'a': sb.Append('\a'); break; case 'e': sb.Append('\u001b'); break; case '0': sb.Append('\0'); break; default: sb.Append('\\').Append(n); break; }
            }
            return sb.ToString();
        }
        // #22: the format is used again until the arguments run out; %s %d %i %f %x %o %c %b %% with flags, width and precision
        int Printf(List<string> args, ShCtx c)
        {
            if (args.Count == 0) { Err(c, "usage: printf FORMAT [arguments...]"); return 2; }
            string fmt = args[0]; var rest = args.Skip(1).ToList(); int ai = 0, ex = 0; var sb = new StringBuilder();
            var spec = new Regex(@"%([-+ 0#]*)(\d+|\*)?(?:\.(\d+))?([sdifxXocb%])");
            do
            {
                int start = ai;
                string outp = spec.Replace(Escapes(fmt.Replace("\\%", "%%")), m =>
                {
                    char k = m.Groups[4].Value[0]; if (k == '%') return "%";
                    string flags = m.Groups[1].Value; int width = 0; if (m.Groups[2].Value == "*") { int.TryParse(ai < rest.Count ? rest[ai++] : "0", out width); } else if (m.Groups[2].Success) int.TryParse(m.Groups[2].Value, out width);
                    int prec = m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : -1;
                    string a = ai < rest.Count ? rest[ai++] : ""; string v;
                    switch (k)
                    {
                        case 's': v = prec >= 0 && a.Length > prec ? a.Substring(0, prec) : a; break;
                        case 'b': v = Escapes(a); break;
                        case 'c': v = a.Length > 0 ? a.Substring(0, 1) : ""; break;
                        case 'f': { double d; if (!double.TryParse(a == "" ? "0" : a, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) { Err(c, "printf: " + a + ": not a number"); ex = 1; d = 0; } v = d.ToString("F" + (prec >= 0 ? prec : 6), CultureInfo.InvariantCulture); if (flags.Contains("+") && d >= 0) v = "+" + v; break; }
                        default:
                            {
                                long n; if (!long.TryParse(a == "" ? "0" : a, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) { Err(c, "printf: " + a + ": not a whole number"); ex = 1; n = 0; }
                                v = k == 'x' ? n.ToString("x", CultureInfo.InvariantCulture) : k == 'X' ? n.ToString("X", CultureInfo.InvariantCulture) : k == 'o' ? Convert.ToString(n, 8) : n.ToString(CultureInfo.InvariantCulture);
                                if (flags.Contains("+") && n >= 0 && (k == 'd' || k == 'i')) v = "+" + v; break;
                            }
                    }
                    if (v.Length < width) v = flags.Contains("-") ? v.PadRight(width) : flags.Contains("0") && k != 's' ? (v.StartsWith("-", StringComparison.Ordinal) ? "-" + v.Substring(1).PadLeft(width - 1, '0') : v.PadLeft(width, '0')) : v.PadLeft(width);
                    return v;
                });
                sb.Append(outp);
                if (ai == start) break;   // the format takes no arguments: once
            } while (ai < rest.Count);
            O(c, sb.ToString(), ShFg); return ex;
        }
        // #61: every file, and a total; -c bytes, -m characters
        int Wc(List<string> args, ShCtx c)
        {
            string fl = Flags(args); var files = Files(args); bool l = fl.Contains('l'), w = fl.Contains('w'), b = fl.Contains('c'), m = fl.Contains('m'); if (!l && !w && !b && !m) { l = w = b = true; }
            long tl = 0, tw = 0, tb = 0, tm = 0; int ex = 0;
            Action<long, long, long, long, string> line = (a1, a2, a3, a4, name) => { var p = new List<string>(); if (l) p.Add(a1.ToString(CultureInfo.InvariantCulture).PadLeft(7)); if (w) p.Add(a2.ToString(CultureInfo.InvariantCulture).PadLeft(7)); if (m) p.Add(a4.ToString(CultureInfo.InvariantCulture).PadLeft(7)); if (b) p.Add(a3.ToString(CultureInfo.InvariantCulture).PadLeft(8)); string s0 = string.Join(" ", p).TrimStart(); OL(c, name == null ? s0 : s0 + " " + name); };
            Action<IEnumerable<string>, string> count = (ls, name) => { long a1 = 0, a2 = 0, a3 = 0, a4 = 0; foreach (var x in ls) { a1++; a2 += Regex.Matches(x, @"\S+").Count; a4 += x.Length + 1; a3 += Encoding.UTF8.GetByteCount(x) + 1; } tl += a1; tw += a2; tb += a3; tm += a4; line(a1, a2, a3, a4, name); };
            if (files.Count == 0) { string t = c.Stdin ?? ""; long a1 = t.Count(ch => ch == '\n'), a2 = Regex.Matches(t, @"\S+").Count; line(a1, a2, Encoding.UTF8.GetByteCount(t), t.Length, null); return 0; }
            // #61: what GNU wc counts - newline characters, the file's real bytes, characters and words - not rebuilt lines
            foreach (var f in files)
            {
                string av = f == "-" ? null : Abs(f), rp = av == null || av.StartsWith("/proc/", StringComparison.Ordinal) ? null : Real(av);
                if (rp == null) { var ls = StreamLines(f, c); if (ls == null) { ex = 1; continue; } string t = string.Join("\n", ls) + (ls.Any() ? "\n" : ""); long w0 = Regex.Matches(t, @"\S+").Count; tl += t.Count(ch => ch == '\n'); tw += w0; tb += Encoding.UTF8.GetByteCount(t); tm += t.Length; line(t.Count(ch => ch == '\n'), w0, Encoding.UTF8.GetByteCount(t), t.Length, f); continue; }
                if (!File.Exists(rp)) { Err(c, "wc: " + f + ": " + (Directory.Exists(rp) ? "is a folder" : "no such file")); ex = 1; continue; }
                long a1 = 0, a2 = 0, a3, a4 = 0; bool inW = false;
                try
                {
                    using (var fs = new FileStream(rp, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        a3 = fs.Length;
                        using (var rd = new StreamReader(fs, Encoding.UTF8, true))
                        {
                            var buf = new char[65536]; int got;
                            while ((got = rd.Read(buf, 0, buf.Length)) > 0)
                            {
                                if (c.Job.Cancel) return 130;
                                for (int i = 0; i < got; i++) { char ch = buf[i]; if (ch == '\n') a1++; bool sp = char.IsWhiteSpace(ch); if (!sp && !inW) a2++; inW = !sp; }
                                a4 += got;
                            }
                        }
                    }
                }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, "wc: " + f + ": " + e.Message); ex = 1; continue; }
                tl += a1; tw += a2; tb += a3; tm += a4; line(a1, a2, a3, a4, f);
            }
            if (files.Count > 1) line(tl, tw, tb, tm, "total");
            return ex;
        }
        // #53: read a b c - one field each, the last takes the rest; from a pipe, or asked at the prompt
        int Read(List<string> args, ShCtx c)
        {
            bool sec = false; string ask = ""; var names = new List<string>();
            string arrName = null;
            for (int i = 0; i < args.Count; i++)   // RG-8: the flags bash's read has take their value; an unknown one is an error, never a variable name
            {
                string x = args[i];
                if (x == "-s") sec = true; else if (x == "-r") { }
                else if (x == "-p" || x == "-t" || x == "-n" || x == "-N" || x == "-d" || x == "-a" || x == "-u")
                {
                    if (i + 1 >= args.Count) { Err(c, "read: " + x + " needs a value"); return 2; }
                    string v = args[++i]; if (x == "-p") ask = v; else if (x == "-a") arrName = v;   // -t -n -d -u: accepted; a line is read whole
                }
                else if (x.StartsWith("-", StringComparison.Ordinal) && x.Length > 1) { Err(c, "read: " + x + ": not a flag read knows (-r -s -p -a -t -n -d)"); return 2; }
                else names.Add(x);
            }
            if (arrName != null && !Regex.IsMatch(arrName, "^[A-Za-z_][A-Za-z0-9_]*$")) { Err(c, "read: " + arrName + ": not a name"); return 2; }
            if (names.Count == 0) names.Add("REPLY");
            string ans;
            if (c.Stdin != null) { if (c.Stdin == "") return 1; int nl = c.Stdin.IndexOf('\n'); ans = (nl < 0 ? c.Stdin : c.Stdin.Substring(0, nl)).TrimEnd('\r'); c.Stdin = nl < 0 ? "" : c.Stdin.Substring(nl + 1); }
            else { if (c.Job.Bg) { Err(c, "read: nothing to read here - pipe lines into it (cat list.txt | while read h; do ...; done)"); return 1; } ans = ShAsk(ask, sec); if (ans == null) return 130; }
            if (arrName != null) { lock (Env.Vars) { Env.Vars.Remove(arrName); Env.Arrs[arrName] = ans.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList(); } return 0; }   // read -a a: one element per word
            if (names.Count == 1) { lock (Env.Vars) Env.Vars[names[0]] = names[0] == "REPLY" ? ans : ans.Trim(); return 0; }
            var rest = ans.Trim();
            lock (Env.Vars)
                for (int i = 0; i < names.Count; i++)
                {
                    if (i == names.Count - 1) { Env.Vars[names[i]] = rest; break; }
                    var m = Regex.Match(rest, @"^(\S*)\s*(.*)$", RegexOptions.Singleline); Env.Vars[names[i]] = m.Groups[1].Value; rest = m.Groups[2].Value;
                }
            return 0;
        }

        // ---------- folders and files
        int Cd(string p, ShCtx c)
        {
            if (p == "-") p = Env.Prev;
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
            string real = Real(v); if (real != null) { var seg = v.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); v = "/" + string.Join("/", seg.Take(seg[0] == "mnt" ? 2 : 1)) + VirtualTail(real, seg[0] == "mnt" ? char.ToUpperInvariant(seg[1][0]) + ":\\" : seg[0] == "home" ? Home : Program.DataDir); }
            Env.Prev = Env.Cwd; Env.Cwd = v;
            if (tEnv == null) Ui(ShPromptKeepHidden);   // only the interactive shell's own cd moves the prompt (#8)
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
            string fl = Flags(args); bool all = fl.Contains('a'); longForm |= fl.Contains('l');
            var targets = Files(args); if (targets.Count == 0) targets.Add(".");
            int ex = 0;
            foreach (var tg in targets)
            {
                string v = Abs(tg); if (targets.Count > 1) OL(c, v + ":", ShBlue);
                if (ProcExists(v)) { OL(c, tg); continue; }
                string real = Real(v);
                if (real == null)
                {
                    bool known; var l = VirtualList(v, out known); if (!known) { Err(c, "ls: " + tg + ": no such folder"); ex = 1; continue; }
                    if (l.Count == 0 && v.StartsWith("/pages/", StringComparison.Ordinal)) { OL(c, "a page - type  open  to go there", ShDim); continue; }
                    int w = l.Count == 0 ? 0 : l.Max(z => z[0].Length) + 2;
                    foreach (var e in l) { O(c, (e[0] + (e[1] == "d" ? "/" : "")).PadRight(w + 1), e[1] == "d" ? ShBlue : e[1] == "x" ? ShGreen : ShFg); OL(c, e[2], ShDim); }
                    continue;
                }
                if (File.Exists(real)) { var fi = new FileInfo(real); OL(c, longForm ? FmtLs(fi) : fi.Name); continue; }
                if (!Directory.Exists(real)) { Err(c, "ls: " + tg + ": no such file or folder"); ex = 1; continue; }
                try
                {
                    var di = new DirectoryInfo(real);
                    var ents = di.EnumerateFileSystemInfos().Where(z => all || ((z.Attributes & FileAttributes.Hidden) == 0 && !z.Name.StartsWith(".", StringComparison.Ordinal))).OrderBy(z => !(z is DirectoryInfo)).ThenBy(z => z.Name, StringComparer.OrdinalIgnoreCase).Take(5000).ToList();
                    if (longForm) { foreach (var e in ents) OL(c, FmtLs(e), e is DirectoryInfo ? ShBlue : IsScript(e.Name) ? ShGreen : ShFg); if (c.Out.Screen) OL(c, ents.Count + " entries", ShDim); }
                    else
                    {
                        // columns, like ls on a terminal (one name per line into a pipe or a file)
                        var names = ents.Select(e => e.Name + (e is DirectoryInfo && c.Out.Screen ? "/" : "")).ToList(); int colw = names.Count == 0 ? 0 : Math.Min(40, names.Max(z => z.Length) + 2), per = Math.Max(1, 100 / Math.Max(1, colw));
                        for (int i = 0; i < names.Count; i++) { var e = ents[i]; string n = c.Out.Screen && names[i].Length > colw - 1 ? names[i].Substring(0, colw - 2) + "…" : names[i]; O(c, c.Out.Screen ? n.PadRight(colw) : n, e is DirectoryInfo ? ShBlue : IsScript(e.Name) ? ShGreen : ShFg); if (!c.Out.Screen || (i + 1) % per == 0 || i == names.Count - 1) O(c, "\n", ShFg); }
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
        static bool IsLink(string path) { try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; return true; } }
        int Tree(string p, ShCtx c)
        {
            string real = Real(Abs(p)); if (real == null || !Directory.Exists(real)) { Err(c, "tree: " + p + ": not a folder on a disk"); return 1; }
            int n = 0; Action<string, string, int> walk = null;
            walk = (dir, pre, depth) =>
            {
                if (depth > 3 || n > 400 || c.Job.Cancel) return; string[] ents; try { ents = Directory.GetFileSystemEntries(dir).Where(z => !Path.GetFileName(z).StartsWith(".", StringComparison.Ordinal)).OrderBy(z => !Directory.Exists(z)).ThenBy(z => z, StringComparer.OrdinalIgnoreCase).ToArray(); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; return; }
                for (int i = 0; i < ents.Length && n <= 400; i++) { bool last = i == ents.Length - 1, d = Directory.Exists(ents[i]); n++; O(c, pre + (last ? "└── " : "├── "), ShDim); OL(c, Path.GetFileName(ents[i]) + (d ? "/" : ""), d ? ShBlue : ShFg); if (d && !IsLink(ents[i])) walk(ents[i], pre + (last ? "    " : "│   "), depth + 1); }
            };
            OL(c, ShShort(Abs(p)), ShBlue); walk(real, "", 1); if (n > 400) OL(c, "... (stopped at 400)", ShDim); return 0;
        }
        const long ShReadCap = 20L * 1048576;
        string ReadAll(string f, ShCtx c)
        {
            if (f == "-") return c.Stdin ?? "";
            string av = Abs(f); if (av.StartsWith("/proc/", StringComparison.Ordinal)) { var pf = ProcFile(av.Substring(6)); if (pf == null) Err(c, f + ": no such file"); return pf; }
            string r = Real(av); if (r == null || !File.Exists(r)) { Err(c, f + ": " + (r != null && Directory.Exists(r) ? "is a folder" : "no such file")); return null; }
            var fi = new FileInfo(r); if (fi.Length > ShReadCap) { Err(c, f + ": over 20 MB (" + (fi.Length / 1048576) + " MB) - head, tail, grep and wc read files of any size; for the rest, narrow it first (grep x " + f + " | sort)"); return null; }
            try { return File.ReadAllText(r); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, f + ": " + e.Message); return null; }
        }
        // a file's lines one at a time, any size (#26) - head, tail, grep and wc use it; - is the pipe, /proc works too
        IEnumerable<string> StreamLines(string f, ShCtx c)
        {
            if (f == "-") return SplitLines(c.Stdin ?? "");
            string av = Abs(f); if (av.StartsWith("/proc/", StringComparison.Ordinal)) { var pf = ProcFile(av.Substring(6)); if (pf == null) { Err(c, f + ": no such file"); return null; } return SplitLines(pf); }
            string r = Real(av); if (r == null || !File.Exists(r)) { Err(c, f + ": " + (r != null && Directory.Exists(r) ? "is a folder" : "no such file")); return null; }
            return FileLines(r, c);
        }
        // RG-6: a line longer than 16 MB is cut - and said so once, never silently
        IEnumerable<string> FileLines(string path, ShCtx c)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) using (var rd = new StreamReader(fs, Encoding.UTF8, true))
            {
                var sb = new StringBuilder(); int ch, n = 0; bool cut = false;
                while ((ch = rd.Read()) >= 0)
                {
                    if (sb.Length >= 16 << 20 && ch != '\r' && ch != '\n') { if (!cut) { cut = true; Err(c, Path.GetFileName(path) + ": a line over 16 MB - the rest of that line is left out"); } continue; }
                    if (ch == '\r') { if (rd.Peek() == '\n') rd.Read(); yield return sb.ToString(); sb.Clear(); if ((++n & 4095) == 0 && c.Job.Cancel) yield break; continue; }
                    if (ch == '\n') { yield return sb.ToString(); sb.Clear(); if ((++n & 4095) == 0 && c.Job.Cancel) yield break; continue; }
                    sb.Append((char)ch);
                }
                if (sb.Length > 0) yield return sb.ToString();
            }
        }
        // \r\n, \n and a lone \r all end a line (#69)
        static List<string> SplitLines(string t)
        {
            if (string.IsNullOrEmpty(t)) return new List<string>();
            var l = t.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            if (l.Count > 0 && l[l.Count - 1] == "") l.RemoveAt(l.Count - 1);
            return l;
        }
        List<string> InLines(List<string> files, ShCtx c)
        {
            if (files.Count == 0) return SplitLines(c.Stdin ?? "");
            var l = new List<string>(); foreach (var f in files) { var t = ReadAll(f, c); if (t == null) return null; l.AddRange(SplitLines(t)); } return l;
        }
        int Cat(List<string> args, ShCtx c)
        {
            var files = Files(args); bool num = Flags(args).Contains('n');
            if (files.Count == 0) files.Add("-");   // #33: no file, or -, is the pipe
            int ex = 0, n = 0;
            foreach (var f in files)
            {
                var t = ReadAll(f, c); if (t == null) { ex = 1; continue; }
                if (f != "-" && t.Take(8000).Count(ch => ch == '\0') > 2) { Err(c, "cat: " + f + ": a binary file - sha256sum, base64 or the hashf tool can read it"); ex = 1; continue; }
                if (num) { foreach (var ln in SplitLines(t)) OL(c, (++n).ToString(CultureInfo.InvariantCulture).PadLeft(6) + "  " + ln); }
                else { O(c, t, ShFg); if (t.Length > 0 && !t.EndsWith("\n", StringComparison.Ordinal) && c.Out.Screen) O(c, "\n", ShFg); }
            }
            return ex;
        }
        int HeadTail(bool head, List<string> args, ShCtx c)
        {
            int n = 10; bool fromStart = false; var files = new List<string>();
            for (int k = 0; k < args.Count; k++)
            {
                string a = args[k]; string v = null;
                if (a == "-n" && k + 1 < args.Count) v = args[++k]; else if (a.StartsWith("-n", StringComparison.Ordinal) && a.Length > 2) v = a.Substring(2); else if (Regex.IsMatch(a, @"^-\d+$")) v = a.Substring(1);
                if (v != null) { if (v.StartsWith("+", StringComparison.Ordinal)) { fromStart = true; v = v.Substring(1); } if (!int.TryParse(v, out n)) { Err(c, (head ? "head" : "tail") + ": " + v + ": not a number"); return 1; } continue; }
                if (a == "-f") { Err(c, "tail -f: use  watch -n 2 tail " + string.Join(" ", args.Where(z => z != "-f")) + "  here"); return 1; }
                files.Add(a);
            }
            if (files.Count == 0) files.Add("-");
            int ex = 0;
            foreach (var f in files)
            {
                if (files.Count > 1) OL(c, "==> " + f + " <==", ShBlue);
                var ls = StreamLines(f, c); if (ls == null) { ex = 1; continue; }
                if (head) foreach (var x in ls.Take(n)) OL(c, x);
                else if (fromStart) foreach (var x in ls.Skip(Math.Max(0, n - 1))) OL(c, x);
                else { var q = new Queue<string>(); foreach (var x in ls) { q.Enqueue(x); if (q.Count > n) q.Dequeue(); } foreach (var x in q) OL(c, x); }
            }
            return ex;
        }
        // GNU's basic regular expressions (grep and sed by default): \| \+ \? \( \) \{ \} are the operators, | + ? ( ) { } plain
        // characters - so 'loss\|average' and '[0-9]\+' do what they do on Linux (#27). -E switches to extended.
        static string BreToNet(string p)
        {
            var sb = new StringBuilder(); bool inSet = false;
            for (int i = 0; i < p.Length; i++)
            {
                char ch = p[i];
                if (inSet) { sb.Append(ch); if (ch == ']' && sb.Length > 1 && sb[sb.Length - 2] != '[' && !(sb.Length > 2 && sb[sb.Length - 2] == '^' && sb[sb.Length - 3] == '[')) inSet = false; continue; }
                if (ch == '[') { inSet = true; sb.Append(ch); if (i + 1 < p.Length && p[i + 1] == '^') { sb.Append('^'); i++; } if (i + 1 < p.Length && p[i + 1] == ']') { sb.Append("\\]"); i++; } continue; }
                if (ch == '\\' && i + 1 < p.Length) { char n = p[++i]; if ("|+?(){}".IndexOf(n) >= 0) sb.Append(n); else sb.Append('\\').Append(n); continue; }
                if ("|+?(){}".IndexOf(ch) >= 0) { sb.Append('\\').Append(ch); continue; }
                sb.Append(ch);
            }
            return sb.ToString();
        }
        static Regex ShRegex(string pat, bool ere, bool fixedStr, bool ic, bool word)
        {
            string p = fixedStr ? Regex.Escape(pat) : ere ? pat : BreToNet(pat);
            if (word) p = @"(?<![\w])(?:" + p + @")(?![\w])";
            return new Regex(p, (ic ? RegexOptions.IgnoreCase : RegexOptions.None) | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        }
        int Grep(List<string> args, ShCtx c)
        {
            bool ic = false, inv = false, num = false, count = false, ere = false, fx = false, word = false, only = false, quiet = false, list = false, names = false, noNames = false, recurse = false;
            string pat = null; var files = new List<string>(); bool opts = true;
            for (int i = 0; i < args.Count; i++)
            {
                string a = args[i];
                if (opts && a == "--") { opts = false; continue; }
                if (opts && a == "-e" && i + 1 < args.Count) { pat = args[++i]; continue; }
                if (opts && a.StartsWith("-", StringComparison.Ordinal) && a.Length > 1 && !(pat == null && Regex.IsMatch(a, @"^-\d"))) { foreach (char f in a.Substring(1)) switch (f) { case 'i': ic = true; break; case 'v': inv = true; break; case 'n': num = true; break; case 'c': count = true; break; case 'E': ere = true; break; case 'F': fx = true; break; case 'w': word = true; break; case 'o': only = true; break; case 'q': quiet = true; break; case 'l': list = true; break; case 'H': names = true; break; case 'h': noNames = true; break; case 'r': case 'R': recurse = true; break; } continue; }
                if (pat == null) pat = a; else files.Add(a);
            }
            if (pat == null) { Err(c, "usage: grep [-i -v -n -c -E -F -w -o -l -q -r] pattern [file...]"); return 2; }
            Regex rx; try { rx = ShRegex(pat, ere, fx, ic, word); } catch (ArgumentException e) { Err(c, "grep: " + pat + ": not a pattern (" + e.Message.Split('-').Last().Trim() + ")"); return 2; }
            if (recurse) { var more = new List<string>(); foreach (var f in files.DefaultIfEmpty(".")) { string r = Real(Abs(f)); if (r != null && Directory.Exists(r)) { try { foreach (var x in Directory.EnumerateFiles(r, "*", SearchOption.AllDirectories).Take(20000)) more.Add(f.TrimEnd('/') + "/" + x.Substring(r.TrimEnd('\\').Length + 1).Replace('\\', '/')); } catch (UnauthorizedAccessException) { } } else more.Add(f); } files = more; }
            if (files.Count == 0) files.Add("-");
            bool prefix = !noNames && (names || files.Count > 1);   // #62: which file matched
            int hits = 0, ex = 1;
            foreach (var f in files)
            {
                var ls = StreamLines(f, c); if (ls == null) { ex = 2; continue; }
                int i = 0, here = 0;
                try
                {
                    foreach (var x in ls)
                    {
                        i++;
                        bool m = rx.IsMatch(x) != inv; if (!m) continue;
                        here++; hits++; if (quiet) return 0; if (list) break; if (count) continue;
                        string pre = (prefix ? f + ":" : "") + (num ? i + ":" : "");
                        if (only && !inv) foreach (Match mm in rx.Matches(x)) OL(c, pre + mm.Value); else OL(c, pre + x);
                    }
                }
                catch (RegexMatchTimeoutException) { Err(c, "grep: the pattern took too long on " + f + " - stopped"); return 2; }
                if (list && here > 0) OL(c, f);
                if (count) OL(c, (prefix ? f + ":" : "") + here);
            }
            return hits > 0 ? 0 : ex;
        }
        int CpMv(bool move, List<string> args, ShCtx c)
        {
            bool rec = Flags(args).IndexOfAny(new[] { 'r', 'R', 'a' }) >= 0, noClobber = Flags(args).Contains('n'); var p = Files(args);
            if (p.Count < 2) { Err(c, "usage: " + (move ? "mv" : "cp [-r]") + " source... target"); return 1; }
            string dst = Real(Abs(p[p.Count - 1])); if (dst == null) { Err(c, "can't write to " + p[p.Count - 1] + " - only folders on a disk"); return 1; }
            if (p.Count > 2 && !Directory.Exists(dst)) { Err(c, (move ? "mv" : "cp") + ": " + p[p.Count - 1] + " isn't a folder (several sources need one)"); return 1; }
            int ex = 0;
            foreach (var s in p.Take(p.Count - 1))
            {
                string src = Real(Abs(s)); if (src == null || (!File.Exists(src) && !Directory.Exists(src))) { Err(c, s + ": no such file"); ex = 1; continue; }
                string to = Directory.Exists(dst) ? Path.Combine(dst, Path.GetFileName(src.TrimEnd('\\'))) : dst;
                if (string.Equals(Path.GetFullPath(src).TrimEnd('\\'), Path.GetFullPath(to).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) { Err(c, s + " and " + p[p.Count - 1] + " are the same file"); ex = 1; continue; }
                if (noClobber && (File.Exists(to) || Directory.Exists(to))) continue;
                bool sameVol = string.Equals(Path.GetPathRoot(Path.GetFullPath(src)), Path.GetPathRoot(Path.GetFullPath(to)), StringComparison.OrdinalIgnoreCase);
                if (File.Exists(src))
                {
                    if (!move) { File.Copy(src, to, true); continue; }
                    // #19: the existing file is replaced only once the new one is in place - never deleted first
                    if (!sameVol) { string tmp = to + ".adeeboshell-part"; File.Copy(src, tmp, true); if (File.Exists(to)) File.Replace(tmp, to, null); else File.Move(tmp, to); File.Delete(src); }
                    else if (File.Exists(to)) File.Replace(src, to, null); else File.Move(src, to);
                    continue;
                }
                // a folder: never into itself (#88)
                string full = Path.GetFullPath(src).TrimEnd('\\') + "\\";
                if (Path.GetFullPath(to).TrimEnd('\\').StartsWith(full, StringComparison.OrdinalIgnoreCase) || (Path.GetFullPath(to).TrimEnd('\\') + "\\").StartsWith(full, StringComparison.OrdinalIgnoreCase)) { Err(c, (move ? "mv" : "cp") + ": " + s + " can't go inside itself"); ex = 1; continue; }
                if (!move && !rec) { Err(c, "cp: " + s + " is a folder - use cp -r"); ex = 1; continue; }
                if (move && sameVol && !Directory.Exists(to)) { Directory.Move(src, to); continue; }
                if (move && Directory.Exists(to)) { Err(c, "mv: " + p[p.Count - 1] + "/" + Path.GetFileName(src) + " already exists"); ex = 1; continue; }
                CopyDir(src, to, c); if (c.Job.Cancel) return 130;
                if (move) Directory.Delete(src, true);
            }
            return ex;
        }
        static void CopyDir(string a, string b, ShCtx c)
        {
            Directory.CreateDirectory(b);
            foreach (var f in Directory.GetFiles(a)) { if (c.Job.Cancel) return; File.Copy(f, Path.Combine(b, Path.GetFileName(f)), true); }
            foreach (var d in Directory.GetDirectories(a)) { if (IsLink(d)) continue; CopyDir(d, Path.Combine(b, Path.GetFileName(d)), c); }
        }
        int Rm(List<string> args, ShCtx c)
        {
            string fl = Flags(args); bool rec = fl.IndexOfAny(new[] { 'r', 'R' }) >= 0, force = fl.Contains('f');
            var p = Files(args); if (p.Count == 0) { Err(c, "usage: rm [-r] [-f] file..."); return 1; }
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
            string start = args.FirstOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal)) ?? ".";
            int ni = args.IndexOf("-name"), ii = args.IndexOf("-iname"), ti = args.IndexOf("-type"), di = args.IndexOf("-maxdepth");
            string pat = ni >= 0 && ni + 1 < args.Count ? args[ni + 1] : ii >= 0 && ii + 1 < args.Count ? args[ii + 1] : "*", type = ti >= 0 && ti + 1 < args.Count ? args[ti + 1] : ""; int maxDepth = int.MaxValue; if (di >= 0 && di + 1 < args.Count) int.TryParse(args[di + 1], out maxDepth);
            string real = Real(Abs(start)); if (real == null || !Directory.Exists(real)) { Err(c, "find: " + start + ": not a folder on a disk"); return 1; }
            var rx = new Regex("^" + GlobRx(pat) + "$", RegexOptions.IgnoreCase); int n = 0, denied = 0;
            var stack = new Stack<KeyValuePair<string, int>>(); stack.Push(new KeyValuePair<string, int>(real, 0)); string baseV = start.TrimEnd('/');
            while (stack.Count > 0 && n < 5000 && !c.Job.Cancel)
            {
                var d = stack.Pop(); string[] fs, ds;
                try { fs = Directory.GetFiles(d.Key); ds = Directory.GetDirectories(d.Key); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; denied++; continue; }
                foreach (var x in ds.Concat(fs))
                {
                    bool isD = Directory.Exists(x); if ((type == "f" && isD) || (type == "d" && !isD)) continue;
                    if (rx.IsMatch(Path.GetFileName(x))) { n++; OL(c, baseV + x.Substring(real.TrimEnd('\\').Length).Replace('\\', '/'), isD ? ShBlue : ShFg); }
                }
                if (d.Value + 1 < maxDepth) foreach (var x in ds.Reverse()) if (!IsLink(x)) stack.Push(new KeyValuePair<string, int>(x, d.Value + 1));   // #88: never round a junction
            }
            if (n >= 5000) Err(c, "find: stopped at 5,000 results");
            if (denied > 0) Err(c, "find: " + denied + " folder" + (denied == 1 ? "" : "s") + " couldn't be read (no permission)");
            return denied > 0 ? 1 : 0;
        }
        int Stat(List<string> args, ShCtx c)
        {
            int ex = 0;
            foreach (var f in Files(args).DefaultIfEmpty("."))
            {
                string r = Real(Abs(f)); if (r == null) { Err(c, "stat: " + f + ": not on a disk"); ex = 1; continue; }
                if (File.Exists(r)) { var fi = new FileInfo(r); O(c, "  File: ", ShBlue); OL(c, f); O(c, "  Size: ", ShBlue); OL(c, fi.Length.ToString("N0", CultureInfo.CurrentCulture) + " bytes" + ((fi.Attributes & FileAttributes.ReadOnly) != 0 ? " · read-only" : "")); O(c, "Modify: ", ShBlue); OL(c, fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)); O(c, "Create: ", ShBlue); OL(c, fi.CreationTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)); O(c, "  Path: ", ShBlue); OL(c, r); }
                else if (Directory.Exists(r)) { var di = new DirectoryInfo(r); O(c, "  Folder: ", ShBlue); OL(c, f + (IsLink(r) ? "  (a link/junction)" : "")); O(c, "  Modify: ", ShBlue); OL(c, di.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)); O(c, "  Path: ", ShBlue); OL(c, r); OL(c, "  (du -sh " + f + " adds up its size)", ShDim); }
                else { Err(c, "stat: " + f + ": no such file"); ex = 1; }
            }
            return ex;
        }
        // test / [ : ! ( ) -a -o, -z -n -f -d -e -s -r -w, = != < > (strings), -eq -ne -lt -le -gt -ge (numbers) - #52
        bool Test(List<string> a)
        {
            int i = 0;
            Func<bool> orE = null, andE = null, notE = null, prim = null;
            orE = () => { bool v = andE(); while (i < a.Count && a[i] == "-o") { i++; bool r = andE(); v = v || r; } return v; };
            andE = () => { bool v = notE(); while (i < a.Count && a[i] == "-a") { i++; bool r = notE(); v = v && r; } return v; };
            notE = () => { if (i < a.Count && a[i] == "!" && i + 1 < a.Count) { i++; return !notE(); } return prim(); };
            prim = () =>
            {
                if (i >= a.Count) throw new FormatException("an argument is missing");
                if (a[i] == "(") { i++; bool v = orE(); if (i >= a.Count || a[i] != ")") throw new FormatException("a ( isn't closed"); i++; return v; }
                if (i + 2 < a.Count + 0 && i + 1 < a.Count && Regex.IsMatch(a[i + 1], "^(=|==|!=|<|>|-eq|-ne|-lt|-le|-gt|-ge|-nt|-ot)$") && i + 2 < a.Count)
                {
                    string l = a[i], op = a[i + 1], r = a[i + 2]; i += 3;
                    switch (op)
                    {
                        case "=": case "==": return l == r; case "!=": return l != r;
                        case "<": return string.CompareOrdinal(l, r) < 0; case ">": return string.CompareOrdinal(l, r) > 0;
                        case "-nt": case "-ot": { string x = Real(Abs(l)), y = Real(Abs(r)); if (x == null || y == null || !File.Exists(x) || !File.Exists(y)) return false; int cmp = File.GetLastWriteTimeUtc(x).CompareTo(File.GetLastWriteTimeUtc(y)); return op == "-nt" ? cmp > 0 : cmp < 0; }
                    }
                    long x0, y0; if (!long.TryParse(l.Trim(), out x0) || !long.TryParse(r.Trim(), out y0)) throw new FormatException((long.TryParse(l.Trim(), out x0) ? r : l) + ": a whole number is needed");
                    switch (op) { case "-eq": return x0 == y0; case "-ne": return x0 != y0; case "-lt": return x0 < y0; case "-le": return x0 <= y0; case "-gt": return x0 > y0; default: return x0 >= y0; }
                }
                if (Regex.IsMatch(a[i], "^-[zndefsrwxL]$") && i + 1 < a.Count)
                {
                    string op = a[i], v = a[i + 1]; i += 2; string av = Abs(v), r = Real(av);
                    switch (op)
                    {
                        case "-z": return v == ""; case "-n": return v != "";
                        case "-f": return (r != null && File.Exists(r)) || ProcExists(av);
                        case "-d": return IsDir(av);
                        case "-e": case "-r": return (r != null && (File.Exists(r) || Directory.Exists(r))) || ProcExists(av) || IsDir(av);
                        case "-s": return (r != null && File.Exists(r) && new FileInfo(r).Length > 0) || ProcExists(av);
                        case "-w": return r != null && ((File.Exists(r) && (File.GetAttributes(r) & FileAttributes.ReadOnly) == 0) || Directory.Exists(r));
                        case "-x": return r != null && File.Exists(r) && IsScript(r);
                        case "-L": return r != null && (File.Exists(r) || Directory.Exists(r)) && IsLink(r);
                    }
                }
                return a[i++] != "";
            };
            if (a.Count == 0) return false;
            bool res = orE(); if (i < a.Count) throw new FormatException("too many arguments near '" + a[i] + "'");
            return res;
        }

        // ---------- pages, the lab, the help
        int Open(List<string> args, ShCtx c)
        {
            string p = args.Count == 0 ? "" : args[0].ToLowerInvariant().Trim('/');
            if (p == "" || p == ".") { var seg = Env.Cwd.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); if (seg.Length == 2 && seg[0] == "pages") p = seg[1]; else if (seg.Length == 3 && seg[0] == "tools") p = "tool:" + seg[2]; else { Err(c, "open: which page? (cd pages/<name> then open, or open <name>)"); return 1; } }
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
            OL(c, L.T("FOLDERS"), ShBlue);
            h("ls [-l -a] [folder]", L.T("what's here (ll = ls -l) · cd /data/scripts for ready-made scripts")); h("cd <folder> · cd ~ · cd - · cd ..", L.T("home = your folder, data, mnt/c, pages, tools")); h("pwd · tree · find . -name '*.csv'", L.T("where you are, what's below, find by name"));
            h("cat · head -n 20 · tail · grep -i x", L.T("read files or what a pipe sends")); h("cp [-r] · mv · mkdir · touch · rm [-r -f]", L.T("files - rm asks first")); h("du · stat · hash <file>", L.T("sizes, details, SHA-256"));
            OL(c, L.T("THIS PC (read only)"), ShBlue);
            h("df -h · du -sh * · lsblk · free -h", L.T("disks, folder sizes, drives, memory")); h("ps --sort=cpu · top · uptime · lscpu", L.T("what's running and what it costs (nothing is stopped from here)"));
            h("ip a · ip route · ss -tlp · ifconfig", L.T("addresses, routes, connections and listeners")); h("cat /proc/meminfo · watch -n 2 df -h", L.T("/proc as files; re-run anything every few seconds"));
            OL(c, L.T("TOOLS AND PAGES"), ShBlue);
            h("<tool> [args]", L.T("every Toolbox tool is a command: ping 1.1.1.1 20 · dig example.com MX"));
            h("iperf3 -c 10.0.0.5 -R -t 10 -P 4", L.T("iperf3 with its real flags (-p -u -b --bidir too); AdeeboMeter is the client")); h("tools [word] · man <tool>", L.T("list them, or what one does")); h("open <page> · cd pages/markets; open", L.T("go to a page")); h("lab [lan|path|wifi|phone|vpn|new]", L.T("AdeeboLab")); h("status · debug · log [n]", L.T("the NOW line · raw values · the app's log"));
            OL(c, L.T("POWER"), ShBlue);
            h("cmd | grep x | sort | head", L.T("pipes; > file, >> file, < file, 2>/dev/null, &> file"));
            h("x=$(cmd) · $((n * 2)) · cut sed awk tr xargs", L.T("command output and sums in a line; the text tools")); h("cmd1 && cmd2 · cmd1 || cmd2 · cmd &", L.T("and / or / in the background (jobs, wait, kill %1)"));
            h("parallel -j 5 ping {} 4 ::: a b c", L.T("run together, merge as each finishes; cat hosts.txt | parallel ping {} 4")); h("case $x in a|b) ... ;; *) ... ;; esac", L.T("choose by pattern (* ? [abc])"));
            h("f() { local n=$1; echo $n; } · a=(x y z)", L.T("functions with local and return; arrays: ${a[1]} \"${a[@]}\" ${#a[@]}"));
            h("for h in a b c; do ping $h 4; done", L.T("loops, while, if/then/else/fi, NAME=value, $1 $? read test seq sleep"));
            h("nano file · vim file · less file", L.T("write a script or any text file, right here (^S save ^X exit · :wq); read a long one a page at a time"));
            h("source run.sh [args] [--yes] · ./run.sh", L.T("a script; --yes confirms its test-traffic tools for the whole run")); h("alltools [--yes]", L.T("every tool with its example"));
            OL(c, L.T("Keys: Tab completes (twice lists) · Up/Down history · Ctrl+C stops (or copies a selection) · Ctrl+L clears · Ctrl+U/K/W edit · right-click pastes"), ShDim);
            OL(c, L.T("Nothing here starts another program: every command is AdeeboMeter's own."), ShDim);
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
        // Carry-over C1: --yes counts only when you type it yourself - a script can't give itself (or another script, alltools or
        // parallel) consent to send test traffic. Typed:  ./x.sh --yes · alltools --yes · parallel --yes ...
        bool YesTyped(List<string> args, ShCtx c, string what)
        {
            bool asked = args.Remove("--yes");
            if (asked && c.Script) { Err(c, what + ": --yes only counts when you type it at the prompt - inside a script the test-traffic tools are skipped"); return false; }
            return asked;
        }
        int Source(string file, List<string> args, ShCtx c) { return Source(file, args, c, false); }
        // sourced: "source x.sh" / ". x.sh" (its exit ends the caller too); "./x.sh", "sh x.sh": a script of its own
        int Source(string file, List<string> args, ShCtx c, bool sourced)
        {
            bool yes = YesTyped(args, c, "source " + file);
            string r = Real(Abs(file)); if (r == null || !File.Exists(r)) { Err(c, "source: " + file + ": no such file"); return 1; }
            if (new FileInfo(r).Length > 512 * 1024) { Err(c, "source: " + file + " is over 512 KB - that's not a script"); return 1; }
            string text; try { text = File.ReadAllText(r); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, file + ": " + e.Message); return 1; }
            if (Env.Depth >= 8) { Err(c, "source: scripts nested 8 deep - stopped (does a script run itself?)"); return 1; }
            Env.Depth++;
            try { return ShRunText(text, new ShCtx { Job = c.Job, Out = c.Out, Stdin = c.Stdin, Args = args, Yes = c.Yes || yes, Script = true }, file + ": ", sourced); }
            finally { Env.Depth--; }
        }
        // a word as the shell reads it back: 'it''s' quoting, so an item with a quote or a $ stays one plain word (#59)
        static string ShQuote(string s) { return "'" + (s ?? "").Replace("'", "'\\''") + "'"; }
        int Parallel(List<string> args, ShCtx c)
        {
            bool yes = YesTyped(args, c, "parallel"); int jobs = 4;
            for (int k = 0; k < args.Count; k++)
            {
                if (args[k] == ":::") break;
                if (args[k] == "-j") { int v; if (k + 1 >= args.Count || !int.TryParse(args[k + 1], out v)) { Err(c, "parallel: -j needs a number (-j 5)"); return 2; } jobs = v; args.RemoveRange(k, 2); k--; continue; }
                if (Regex.IsMatch(args[k], @"^-j\d+$")) { jobs = int.Parse(args[k].Substring(2), CultureInfo.InvariantCulture); args.RemoveAt(k); k--; continue; }
                if (args[k].StartsWith("-", StringComparison.Ordinal) && k == 0) { Err(c, "parallel: " + args[k] + ": not an option this parallel knows (-j N, --yes)"); return 2; }
                break;
            }
            jobs = Math.Max(1, Math.Min(16, jobs));
            int sep = args.IndexOf(":::"); var cmd = sep >= 0 ? args.Take(sep).ToList() : args; var items = sep >= 0 ? args.Skip(sep + 1).ToList() : SplitLines(c.Stdin ?? "").Where(z => z.Trim() != "").Select(z => z.Trim()).ToList();
            if (cmd.Count == 0 || items.Count == 0) { Err(c, "usage: parallel [-j N] [--yes] command {} ::: item item ...   (or: cat list.txt | parallel ping {} 4)"); return 2; }
            if (items.Count > 500) { Err(c, "parallel: " + items.Count + " items - 500 at most"); return 2; }
            if (!cmd.Any(z => z.Contains("{}"))) cmd.Add("{}");
            var sem = new SemaphoreSlim(jobs); int doneN = 0, worst = 0, running = 0; var threads = new List<Thread>(); var outLock = new object();
            var sw = System.Diagnostics.Stopwatch.StartNew(); var env = Env.Clone(); bool ok = c.Yes || yes; var cargs = c.Args;
            foreach (var it in items)
            {
                if (c.Job.Cancel) break;
                sem.Wait(); if (c.Job.Cancel) { sem.Release(); break; }
                var words = cmd.Select(w => w.Replace("{}", it)).ToList();
                string line = string.Join(" ", words.Select(ShQuote)), shown = string.Join(" ", words.Select(w => w.Contains(" ") ? "\"" + w + "\"" : w));
                Interlocked.Increment(ref running);
                var th = new Thread(() =>
                {
                    tEnv = env.Clone(); var cap = new ShOut { Cap = new StringBuilder(), Job = c.Job }; cap.ErrCap = cap.Cap; int ex = 0;   // each item's errors stay with its own output
                    try { ex = ShRunText(line, new ShCtx { Job = c.Job, Out = cap, Args = cargs, Yes = ok, Script = true }); }
                    catch (OutOfMemoryException) { GC.Collect(); lock (cap.Cap) cap.Cap.Append("ran out of memory - stopped\n"); ex = 2; }
                    catch (Exception e) { lock (cap.Cap) cap.Cap.Append("error: " + e.Message + "\n"); ex = 2; }
                    finally { Interlocked.Decrement(ref running); sem.Release(); }
                    lock (outLock) { doneN++; worst = Math.Max(worst, ex); try { if (c.Out.Screen) OL(c, "── " + shown + (ex == 0 ? "" : "   (exit " + ex + ")"), ex == 0 ? ShCyan : ShAmber); else if (ex != 0) ErrRaw(c, "parallel: " + shown + " (exit " + ex + ")\n", ShAmber); O(c, cap.Cap.ToString(), ShFg); } catch (ShTooBig) { } }   // 1.1.6 PC test: into a pipe or a file, only the jobs' own output - like GNU parallel
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
            ErrRaw(c, "parallel: " + doneN + " of " + items.Count + " done in " + sw.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s" + (worst > 0 ? " · some failed" : "") + "\n", worst > 0 ? ShAmber : ShGreen);
            return c.Job.Cancel ? 130 : worst;
        }
        // left out of alltools: they need you there (keys, files, screens), act on your phone, write something you keep, or cost a lot
        static readonly HashSet<string> ShSkip = new HashSet<string> { "newkey", "csrmake", "selfsigned", "encrypt", "decrypt", "recipients", "friend", "screentest", "keytest", "soundtest", "remote", "radiuslab", "wol", "keys", "mtu", "speedtest", "beforeafter" };
        int AllTools(bool asked, ShCtx c)
        {
            bool yes = asked && !c.Script; if (asked && c.Script) Err(c, "alltools: --yes only counts when you type it at the prompt - the test-traffic tools are left out");   // C1
            string gw; lock (M.L) gw = M.Gateway ?? "";
            int ran = 0, failed = 0, skipped = 0, pictures = 0; var log = new StringBuilder(); var cap = new ShOut { Cap = log, Job = c.Job, Tee = c.Out.Screen && !c.Job.Bg }; cap.ErrCap = log;
            // 1.1.6 test day: it showed only "── mtr ..." and then nothing for minutes - now every tool's output streams as it comes,
            // with [n/N] and the time it took, and a "still running" line when a tool is quiet for 10 s
            Func<Tool, bool> runs = t => !(ShSkip.Contains(t.Id) || t.Group == "REMOTE") && (yes || !(LabGate.Contains(t.Id) || SenderGate.Contains(t.Id) || t.Id == "sweep")) && t.Do != null;
            int total = shTools.Count(runs), n = 0; var all0 = DateTime.UtcNow;
            OL(c, "alltools: " + total + " tools to run, one after another - each one's output shows below as it comes. Ctrl+C stops.", ShGreen);
            foreach (var t in shTools)
            {
                if (c.Job.Cancel) break;
                if (ShSkip.Contains(t.Id) || t.Group == "REMOTE") { skipped++; continue; }
                bool gate = LabGate.Contains(t.Id) || SenderGate.Contains(t.Id) || t.Id == "sweep"; if (gate && !yes) { skipped++; continue; }
                if (t.Do == null) { pictures++; continue; }
                n++;
                var ex = Examples.Get(t.Id); var inp = ex != null ? ex.In : new string[0];
                if (t.Id == "diskspeed") inp = inp.Select(v => string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase) ? "" : v).ToArray();
                var words = inp.Select(v => Regex.Replace(v ?? "", @"\b(192\.0\.2|198\.51\.100|203\.0\.113)\.\d{1,3}\b", gw == "" ? "$0" : gw)).Select(v => v == "" ? "-" : v).ToList();
                string head = "── [" + n + "/" + total + "] " + t.Id + " " + string.Join(" ", words) + "  ·  " + L.T(t.Title);
                OL(c, head, ShCyan); log.AppendLine(head);
                var t0 = DateTime.UtcNow; cap.LastTee = t0.Ticks; string tid = t.Id;
                using (var beat = cap.Tee ? new System.Threading.Timer(delegate { if (new TimeSpan(DateTime.UtcNow.Ticks - cap.LastTee).TotalSeconds >= 10) { cap.LastTee = DateTime.UtcNow.Ticks; SW("   … " + tid + " still running (" + (int)(DateTime.UtcNow - t0).TotalSeconds + " s)\n", ShDim); } }, null, 2000, 2000) : null)
                { int r = RunTool(t, words, new ShCtx { Job = c.Job, Out = cap, Yes = yes, Script = true }); ran++; if (r == 2) failed++;
                  string done = (r == 0 ? "   ok" : r == 1 ? "   ok (found problems)" : "   FAILED") + "  ·  " + (DateTime.UtcNow - t0).TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s  ·  " + n + " of " + total + " done, " + (int)(DateTime.UtcNow - all0).TotalMinutes + " min so far";
                  OL(c, done, r == 2 ? ShRed : ShDim); log.AppendLine(done); }
            }
            string tail = (c.Job.Cancel ? "== STOPPED " : "== DONE ") + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " · " + ran + " run · " + failed + " failed · " + skipped + " left out" + (yes ? "" : " (test-traffic tools need --yes, typed at the prompt)") + " · " + pictures + " pictures (Toolbox only)";
            log.AppendLine(tail); OL(c, tail, failed > 0 || c.Job.Cancel ? ShAmber : ShGreen);
            string f = Path.Combine(Program.WriteDir, "alltools.out.txt"); try { File.WriteAllText(f, log.ToString()); OL(c, "transcript: " + f, ShDim); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            return failed > 0 ? 1 : 0;
        }

        // the self-test's way in: a line run to the end, its text back (no screen, no questions). interactive: as if typed at the
        // prompt (Script = false) - for the --yes checks
        internal string TestShell(string text, out int exit) { return TestShell(text, out exit, false); }
        internal string TestShell(string text, out int exit, bool interactive)
        {
            if (shTools == null) shTools = Tools2();
            var job = new ShJob(); var o = new ShOut { Cap = new StringBuilder(), Job = job };
            exit = ShRunText(text, new ShCtx { Job = job, Out = o, Script = !interactive, Stdin = interactive ? "" : null }); return o.Cap.ToString().Replace("\r", "");
        }
        // masking: a line that hides a tool with a secret box behind ; and NAME=value and time - the secret mustn't show (C2)
        internal bool TestMaskHides()
        {
            if (shTools == null) shTools = Tools2();
            foreach (var t in shTools)
            {
                int box = Array.FindIndex(t.Inputs, SecretInput); if (box < 0) continue;
                var w = new List<string>(); for (int i = 0; i <= box; i++) w.Add(i == box ? "S3CRETzz" : "v" + i);
                string line = "echo hi; X=1 time " + t.Id + " " + string.Join(" ", w) + " && echo \"$(" + t.Id + " " + string.Join(" ", w) + ")\"";
                return !ShMasked(line).Contains("S3CRETzz");
            }
            return true;
        }
        // an editor save puts the file back exactly: BOM, CRLF, tabs, no final newline (#18)
        internal bool TestShapeRoundTrip(string path)
        {
            var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("a\tb\r\nc é\r\nlast")).ToArray(); File.WriteAllBytes(path, bytes);
            var sh = new TextShape(); string t = ReadShaped(path, sh); var ed = new TermEditor(false, path, "x", t);
            WriteShaped(path, ed.Body, sh); return File.ReadAllBytes(path).SequenceEqual(bytes);
        }
        internal void TestShellReset() { shMain.Cwd = shMain.Prev = "/home"; shMain.LastExit = 0; shMain.Depth = 0; lock (shMain.Vars) { shMain.Vars.Clear(); shMain.Arrs.Clear(); shMain.Funcs.Clear(); } }

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

        // ---------- nano / vim: our own editor on the shell's screen (TermEditor); the command waits until you leave it.
        // Shell scrutiny #18/#47: a file comes back the way it was - its encoding (UTF-8 with or without BOM, UTF-16, or the
        // Windows code page), its line endings and its last newline - and is written to a side file first, then swapped in, so a
        // failed save never leaves half a file. #93: a file with a line too long to edit here is refused, not frozen on.
        sealed class TextShape { public Encoding Enc = new UTF8Encoding(false); public bool Bom, Crlf, FinalNewline = true; }
        static string ReadShaped(string path, TextShape sh)
        {
            var b = File.ReadAllBytes(path);
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) { sh.Enc = new UTF8Encoding(true); sh.Bom = true; return Shape(sh, new UTF8Encoding(false).GetString(b, 3, b.Length - 3)); }
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) { sh.Enc = new UnicodeEncoding(false, true); sh.Bom = true; return Shape(sh, Encoding.Unicode.GetString(b, 2, b.Length - 2)); }
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) { sh.Enc = new UnicodeEncoding(true, true); sh.Bom = true; return Shape(sh, Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2)); }
            try { return Shape(sh, new UTF8Encoding(false, true).GetString(b)); }
            catch (DecoderFallbackException) { sh.Enc = Encoding.Default; return Shape(sh, Encoding.Default.GetString(b)); }   // ANSI / Windows-1252: its high letters kept
        }
        static string Shape(TextShape sh, string t)
        {
            int crlf = 0, lf = 0; for (int i = 0; i < t.Length; i++) if (t[i] == '\n') { if (i > 0 && t[i - 1] == '\r') crlf++; else lf++; }
            sh.Crlf = crlf > 0 && crlf >= lf; sh.FinalNewline = t.Length == 0 || t.EndsWith("\n", StringComparison.Ordinal);
            return t;
        }
        static void WriteShaped(string path, string body, TextShape sh)
        {
            if (!sh.FinalNewline && body.EndsWith("\n", StringComparison.Ordinal)) body = body.Substring(0, body.Length - 1);
            if (sh.Crlf) body = body.Replace("\n", "\r\n");
            var pre = sh.Bom ? sh.Enc.GetPreamble() : new byte[0]; var data = sh.Enc.GetBytes(body);
            string tmp = path + ".adeeboshell-save";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)) { fs.Write(pre, 0, pre.Length); fs.Write(data, 0, data.Length); fs.Flush(true); }
            try { if (File.Exists(path)) File.Replace(tmp, path, null, true); else File.Move(tmp, path); }
            catch (IOException) { File.Copy(tmp, path, true); File.Delete(tmp); }   // a drive that can't swap files (FAT, some shares): copied over, the original was only touched once the copy was whole
        }
        int Edit(bool vim, List<string> args, ShCtx c)
        {
            if (c.Script || c.Job.Bg || !c.Out.Screen) { Err(c, (vim ? "vim" : "nano") + ": needs you at the keyboard - not inside a script or a pipe"); return 1; }
            string arg = args.FirstOrDefault(z => !z.StartsWith("-", StringComparison.Ordinal) && !z.StartsWith("+", StringComparison.Ordinal)), real = null, text = null; var shape = new TextShape();
            if (arg != null)
            {
                real = Real(Abs(arg)); if (real == null) { Err(c, (vim ? "vim" : "nano") + ": " + arg + ": pages and tools aren't files - use a path under ~, /data or /mnt"); return 1; }
                if (Directory.Exists(real)) { Err(c, (vim ? "vim" : "nano") + ": " + arg + " is a folder"); return 1; }
                if (File.Exists(real))
                {
                    if (new FileInfo(real).Length > 5 * 1048576) { Err(c, arg + ": over 5 MB - too big to edit here"); return 1; }
                    try { text = ReadShaped(real, shape); } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; Err(c, arg + ": " + e.Message); return 1; }
                    if (!(shape.Enc is UnicodeEncoding) && text.Take(8000).Count(ch => ch == '\0') > 2) { Err(c, arg + ": a binary file - not editable as text"); return 1; }
                    int longest = 0, run = 0; foreach (char ch in text) { if (ch == '\n') { run = 0; continue; } if (++run > longest) longest = run; }
                    if (longest > 100000) { Err(c, arg + ": has a line of " + longest.ToString("N0", CultureInfo.CurrentCulture) + " characters (minified?) - too long to edit here; less can show it"); return 1; }
                }
            }
            var done = new ManualResetEvent(false); string title0 = null;
            var ed = new TermEditor(vim, real, arg, text);
            ed.Words = new HashSet<string>(ShCmds.Concat(shTools.Select(t => t.Id)), StringComparer.OrdinalIgnoreCase);
            ed.Save = (to, body) =>
            {
                string r = to == real ? real : Real(Abs(to)); if (r == null) return "not a folder on a disk";
                try { string dir = Path.GetDirectoryName(r); if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) return "no folder " + dir; WriteShaped(r, body, shape); real = r; return null; }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)) throw; return e.Message; }
            };
            ed.Closed = () => { term.Editor = null; if (title0 != null) term.Title = title0; term.Invalidate(); done.Set(); };
            Ui(delegate { term.EndLive(); title0 = term.Title; term.Title = (vim ? "vim" : "nano") + " — " + (arg ?? "new file") + "  ·  AdeeboShell"; term.Editor = ed; term.Invalidate(); term.Focus(); });
            done.WaitOne(); using (done) { }
            return 0;
        }
        // less / more: a page at a time (#65); into a pipe or a script it's cat
        int Less(List<string> args, ShCtx c)
        {
            if (c.Script || c.Job.Bg || !c.Out.Screen) return Cat(args, c);
            var files = Files(args); string name = files.Count == 0 ? "(pipe)" : files[0], text;
            if (files.Count == 0 || files[0] == "-") text = c.Stdin ?? ""; else { text = ReadAll(files[0], c); if (text == null) return 1; }
            var done = new ManualResetEvent(false); string title0 = null;
            var ed = new TermEditor(true, null, name, text) { ReadOnly = true };
            ed.Closed = () => { term.Editor = null; if (title0 != null) term.Title = title0; term.Invalidate(); done.Set(); };
            Ui(delegate { term.EndLive(); title0 = term.Title; term.Title = "less — " + name + "  ·  AdeeboShell"; term.Editor = ed; term.Invalidate(); term.Focus(); });
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
                string five = "# five-at-a-time.sh - ping five places together and merge the answers into one file\n# run it:  ./five-at-a-time.sh      edit it:  nano five-at-a-time.sh\n\nHOSTS=\"1.1.1.1 8.8.8.8 9.9.9.9 208.67.222.222 $GATEWAY\"\necho \"pinging: $HOSTS\"\nparallel -j 5 ping {} 10 ::: $HOSTS > merged.txt\necho \"merged into $PWD/merged.txt\"\ngrep -iE \"loss|average\" merged.txt\n";
                // #92: an earlier version is replaced only when it's exactly as we wrote it - never one you changed
                try { if (File.Exists(f1)) { string have = File.ReadAllText(f1), v2 = five.Replace("grep -iE \"loss|average\"", "grep -i \"loss|average\""), v1 = five.Replace("grep -iE \"loss|average\"", "grep -i \"loss\\|average\""); if (have == v1 || have == v2) File.WriteAllText(f1, five); } } catch (IOException) { }
                if (!File.Exists(f1)) File.WriteAllText(f1, five);
                if (!File.Exists(f3)) File.WriteAllText(f3, "# health-check.sh - the quick look, in order; stops at the first thing that's broken\n# run it:  ./health-check.sh\n\nstatus\nif ping $GATEWAY 4 > /dev/null; then echo \"router: ok\"; else echo \"router: NOT answering - start there\"; exit 1; fi\nping 1.1.1.1 4 > /dev/null && echo \"internet: ok\" || echo \"internet: NOT answering\"\ndig example.com > /dev/null && echo \"names: ok\" || echo \"names: NOT working\"\n");
                if (!File.Exists(f2) && shTools != null)
                {
                    var sb = new StringBuilder("# every-tool.sh - every AdeeboMeter tool with its example, one after another\n# run it:      ./every-tool.sh > all.txt          (test-traffic tools are skipped)\n#             ./every-tool.sh --yes > all.txt    (your own network, typed at the prompt: runs those too)\n# change any line, delete what you don't need, add your own addresses.\n\n");
                    foreach (var g in shTools.GroupBy(t => GroupName(t.Group)))
                    {
                        sb.Append("\n# ---- " + g.Key + "\n");
                        foreach (var t in g)
                        {
                            if (ShSkip.Contains(t.Id) || t.Group == "REMOTE" || t.Do == null) continue;
                            var ex = Examples.Get(t.Id); var inp = ex != null ? ex.In : new string[0];
                            if (t.Id == "diskspeed") inp = inp.Select(v => string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase) ? "" : v).ToArray();
                            var w = inp.Select(v => Regex.Replace(v ?? "", @"\b(192\.0\.2|198\.51\.100|203\.0\.113)\.\d{1,3}\b", "$$GATEWAY")).Select(v => v == "" ? "-" : Regex.IsMatch(v, "[\\s|;&<>\"'`#*?]") ? "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("`", "\\`") + "\"" : v).ToList();
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
