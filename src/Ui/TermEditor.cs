// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // nano and vim inside AdeeboShell (Adeeb, 22 Nov: "have vim and nano, otherwise how will I write those scripts"). Our own
    // editor drawn on the shell's screen - no other program is started. One buffer, two keymaps:
    //   nano  type away; ^S/^O save, ^X exit, ^K cut line, ^U paste it, ^W find, ^G help, ^C where am I, ^V paste
    //   vim   NORMAL hjkl w b 0 ^ $ gg G x dd yy p P u i a A I o O / n N, counts (3dd, 5j), :w :q :q! :wq :x ZZ :set nu :N
    // .sh files are coloured: comments, keywords, strings, $variables, and AdeeboMeter's own commands and tools.
    sealed class TermEditor
    {
        public readonly bool Vim; public string Path, Shown;   // Shown: the name in the bar (as typed in the shell)
        public Func<string, string, string> Save;               // (path, text) -> null when saved, else why not
        public Action Closed; public HashSet<string> Words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // commands + tools to colour
        List<string> buf = new List<string> { "" }; int r, c, left, want = -1; bool dirty, numbers;
        string mode = "NORMAL", cmdLine = "", msg = "", search = "", pending = "", count = "", cut = "", yank = ""; bool yankLines;
        bool askSave, askName; readonly List<string[]> undo = new List<string[]>();
        public Color Bg, Fg; static readonly Color Dim = Color.FromArgb(110, 118, 129), Kw = Color.FromArgb(188, 140, 255), Str = Color.FromArgb(230, 190, 110), Var = Color.FromArgb(57, 207, 220), Cmd = Color.FromArgb(63, 220, 110), BarBg = Color.FromArgb(201, 209, 217), BarFg = Color.FromArgb(13, 17, 23), Accent = Color.FromArgb(88, 166, 255);

        public TermEditor(bool vim, string path, string shown, string text)
        {
            Vim = vim; Path = path; Shown = shown; mode = vim ? "NORMAL" : "EDIT";
            if (text != null) { buf = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(z => z.Replace("\t", "    ")).ToList(); if (buf.Count > 1 && buf[buf.Count - 1] == "") buf.RemoveAt(buf.Count - 1); }
            msg = text == null ? "[ New File ]" : "[ Read " + buf.Count + " line" + (buf.Count == 1 ? "" : "s") + " ]";
        }
        string Text { get { return string.Join("\n", buf) + "\n"; } }
        internal string Body { get { return Text; } }
        string Ln { get { return buf[r]; } set { buf[r] = value; } }
        void Snap() { undo.Add(buf.ToArray().Concat(new[] { r + "," + c }).ToArray()); if (undo.Count > 200) undo.RemoveAt(0); }
        void Undo() { if (undo.Count == 0) { msg = "Already at oldest change"; return; } var u = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1); buf = u.Take(u.Length - 1).ToList(); var rc = u[u.Length - 1].Split(','); r = Math.Min(buf.Count - 1, int.Parse(rc[0])); c = int.Parse(rc[1]); dirty = true; Clamp(); msg = "undone"; }
        void Clamp() { if (buf.Count == 0) buf.Add(""); r = Math.Max(0, Math.Min(buf.Count - 1, r)); int max = Ln.Length - (Vim && mode == "NORMAL" && Ln.Length > 0 ? 1 : 0); c = Math.Max(0, Math.Min(max, c)); }
        void Move(int dr) { if (want < 0) want = c; r += dr; Clamp(); c = Math.Min(want, Math.Max(0, Ln.Length - (Vim && mode == "NORMAL" && Ln.Length > 0 ? 1 : 0))); }
        void Hz(int nc) { c = nc; want = -1; Clamp(); }
        bool DoSave(string to)
        {
            if (string.IsNullOrEmpty(to)) { msg = "No file name"; return false; }
            string err = Save(to, Text); if (err != null) { msg = "Can't save: " + err; return false; }
            Path = to; dirty = false; msg = "[ Wrote " + buf.Count + " line" + (buf.Count == 1 ? "" : "s") + " ]"; return true;
        }
        void Close() { if (Closed != null) Closed(); }

        // ---------- typing that's the same in both: inserting, Enter, Backspace, Delete
        void Ins(string s) { Ln = Ln.Insert(c, s); c += s.Length; dirty = true; want = -1; }
        void Enter() { string rest = Ln.Substring(c); Ln = Ln.Substring(0, c); string indent = Regex.Match(Ln, @"^\s*").Value; buf.Insert(r + 1, indent + rest); r++; c = indent.Length; dirty = true; want = -1; }
        void Back() { if (c > 0) { Ln = Ln.Remove(c - 1, 1); c--; } else if (r > 0) { c = buf[r - 1].Length; buf[r - 1] += Ln; buf.RemoveAt(r); r--; } else return; dirty = true; want = -1; }
        void Del() { if (c < Ln.Length) Ln = Ln.Remove(c, 1); else if (r < buf.Count - 1) { Ln += buf[r + 1]; buf.RemoveAt(r + 1); } else return; dirty = true; }
        void PasteClip() { string t; try { t = Clipboard.GetText(); } catch (System.Runtime.InteropServices.ExternalException) { return; } if (string.IsNullOrEmpty(t)) return; Snap(); var parts = t.Replace("\r\n", "\n").Replace("\t", "    ").Split('\n'); for (int i = 0; i < parts.Length; i++) { if (i > 0) { string rest = Ln.Substring(c); Ln = Ln.Substring(0, c); buf.Insert(r + 1, rest); r++; c = 0; } Ins(parts[i]); } }
        bool Find(string s, int dir)
        {
            if (s == "") return false; int n = buf.Count;
            for (int k = 0; k <= n; k++)
            {
                int row = ((r + dir * k) % n + n) % n; int at;
                if (dir > 0) at = buf[row].IndexOf(s, k == 0 ? Math.Min(buf[row].Length, c + 1) : 0, StringComparison.OrdinalIgnoreCase);
                else { int from = k == 0 ? c - 1 : buf[row].Length - 1; at = from < 0 ? -1 : buf[row].LastIndexOf(s, Math.Min(from, Math.Max(0, buf[row].Length - 1)), StringComparison.OrdinalIgnoreCase); }
                if (at >= 0) { if (k > 0 && ((dir > 0 && row < r) || (dir < 0 && row > r))) msg = "search wrapped"; else msg = ""; r = row; c = at; want = -1; return true; }
            }
            msg = "\"" + s + "\" not found"; return false;
        }

        // ---------- keys: KeyDown for the special ones, KeyPress for what you type
        public void Key(KeyEventArgs e)
        {
            e.SuppressKeyPress = false;
            if (Vim) VimKey(e); else NanoKey(e);
            if (e.Handled) e.SuppressKeyPress = true;
        }
        public void Char(char ch)
        {
            if (char.IsControl(ch)) return;
            if (Vim) { VimChar(ch); return; }
            if (askSave) { if (ch == 'y' || ch == 'Y') { askSave = false; if (Path == null) { askName = true; cmdLine = ""; msg = ""; } else if (DoSave(Path)) Close(); } else if (ch == 'n' || ch == 'N') { askSave = false; Close(); } return; }
            if (askName || mode == "FIND") { cmdLine += ch; return; }
            Snap1(); Ins(ch.ToString());
        }
        DateTime lastSnap; void Snap1() { if ((DateTime.UtcNow - lastSnap).TotalSeconds > 1.5) Snap(); lastSnap = DateTime.UtcNow; }   // typing groups into one undo step
        bool Common(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up: Move(-1); break; case Keys.Down: Move(1); break;
                case Keys.Left: if (c > 0) Hz(c - 1); else if (!Vim && r > 0) { r--; Hz(Ln.Length); } break;
                case Keys.Right: if (c < Ln.Length) Hz(c + 1); else if (!Vim && r < buf.Count - 1) { r++; Hz(0); } break;
                case Keys.Home: Hz(0); break; case Keys.End: Hz(Ln.Length); break;
                case Keys.PageUp: Move(-Page + 1); break; case Keys.PageDown: Move(Page - 1); break;
                default: return false;
            }
            e.Handled = true; return true;
        }
        void NanoKey(KeyEventArgs e)
        {
            var k = e.KeyCode;
            if (askName || mode == "FIND")
            {
                e.Handled = true;
                if (k == Keys.Enter) { if (askName) { askName = false; string nm = cmdLine.Trim(); if (DoSave(nm == "" ? null : nm) && closeAfter) Close(); } else { mode = "EDIT"; if (cmdLine != "") search = cmdLine; Find(search, 1); } cmdLine = ""; closeAfter = false; }
                else if (k == Keys.Escape || (e.Control && k == Keys.C)) { askName = false; mode = "EDIT"; cmdLine = ""; msg = "[ Cancelled ]"; closeAfter = false; }
                else if (k == Keys.Back) { if (cmdLine.Length > 0) cmdLine = cmdLine.Substring(0, cmdLine.Length - 1); }
                else e.Handled = false;
                return;
            }
            if (askSave) { if (e.Control && k == Keys.C) { askSave = false; msg = "[ Cancelled ]"; e.Handled = true; } return; }
            if (e.Control)
            {
                e.Handled = true;
                switch (k)
                {
                    case Keys.S: case Keys.O: if (Path == null) { askName = true; cmdLine = ""; } else DoSave(Path); return;
                    case Keys.X: if (dirty) { askSave = true; msg = ""; } else Close(); return;
                    case Keys.K: Snap(); if (cutChain) cut += "\n" + Ln; else cut = Ln; cutChain = true; if (buf.Count > 1) buf.RemoveAt(r); else buf[0] = ""; dirty = true; Clamp(); return;
                    case Keys.U: if (cut == "") return; Snap(); foreach (var l in cut.Split('\n').Reverse()) buf.Insert(r, l); dirty = true; c = 0; return;
                    case Keys.W: mode = "FIND"; cmdLine = ""; return;
                    case Keys.V: PasteClip(); return;
                    case Keys.Z: Undo(); return;
                    case Keys.G: msg = "^S save  ^X exit  ^K cut line  ^U paste line  ^W find  ^Z undo  ^V paste  ^C where  ^A/^E line start/end"; return;
                    case Keys.C: msg = "[ line " + (r + 1) + "/" + buf.Count + ", col " + (c + 1) + " ]"; return;
                    case Keys.A: Hz(0); return; case Keys.E: Hz(Ln.Length); return;
                    case Keys.Home: r = 0; Hz(0); return; case Keys.End: r = buf.Count - 1; Hz(Ln.Length); return;
                }
                e.Handled = false; return;
            }
            if (k != Keys.K) cutChain = false;
            if (Common(e)) return;
            e.Handled = true;
            switch (k)
            {
                case Keys.Enter: Snap(); Enter(); return;
                case Keys.Back: Snap1(); Back(); return;
                case Keys.Delete: Snap1(); Del(); return;
                case Keys.Tab: Snap1(); Ins("    "); return;
                case Keys.Escape: msg = ""; return;
            }
            e.Handled = false;
        }
        bool cutChain, closeAfter;

        // ---------- vim
        int Count { get { int n; return int.TryParse(count, out n) && n > 0 ? Math.Min(n, 100000) : 1; } }
        void VimKey(KeyEventArgs e)
        {
            var k = e.KeyCode;
            if (mode == "INSERT")
            {
                if (k == Keys.Escape || (e.Control && (k == Keys.C || k == Keys.OemOpenBrackets))) { mode = "NORMAL"; if (c > 0) c--; Clamp(); msg = ""; e.Handled = true; return; }
                if (e.Control && k == Keys.V) { PasteClip(); e.Handled = true; return; }
                if (e.Control && k == Keys.W) { int a = c; while (a > 0 && Ln[a - 1] == ' ') a--; while (a > 0 && char.IsLetterOrDigit(Ln[a - 1])) a--; Ln = Ln.Remove(a, c - a); c = a; dirty = true; e.Handled = true; return; }
                if (Common(e)) return;
                e.Handled = true;
                switch (k) { case Keys.Enter: Enter(); return; case Keys.Back: Back(); return; case Keys.Delete: Del(); return; case Keys.Tab: Ins("    "); return; }
                e.Handled = false; return;
            }
            if (mode == "COMMAND" || mode == "SEARCH" || mode == "SEARCHB")
            {
                e.Handled = true;
                if (k == Keys.Escape || (e.Control && k == Keys.C)) { mode = "NORMAL"; cmdLine = ""; return; }
                if (k == Keys.Back) { if (cmdLine == "") mode = "NORMAL"; else cmdLine = cmdLine.Substring(0, cmdLine.Length - 1); return; }
                if (k == Keys.Enter) { string line = cmdLine; string m = mode; mode = "NORMAL"; cmdLine = ""; if (m == "COMMAND") ExCmd(line.Trim()); else { if (line != "") search = line; Find(search, m == "SEARCH" ? 1 : -1); } Clamp(); return; }
                if (e.Control && k == Keys.V) { try { cmdLine += (Clipboard.GetText() ?? "").Split('\n')[0].TrimEnd('\r'); } catch (System.Runtime.InteropServices.ExternalException) { } return; }
                e.Handled = false; return;
            }
            // NORMAL: the special keys (letters come as characters)
            if (k == Keys.Escape) { pending = ""; count = ""; e.Handled = true; return; }
            if (e.Control)
            {
                e.Handled = true;
                switch (k)
                {
                    case Keys.R: msg = "redo isn't here - u undoes"; return;
                    case Keys.D: Move(Page / 2); return; case Keys.U: Move(-Page / 2); return;
                    case Keys.F: Move(Page - 1); return; case Keys.B: Move(-Page + 1); return;
                    case Keys.G: msg = "\"" + (Shown ?? "[No Name]") + "\" " + buf.Count + " lines --" + (100 * (r + 1) / buf.Count) + "%--"; return;
                    case Keys.V: return;
                }
                e.Handled = false; return;
            }
            if (Common(e)) { count = ""; return; }
            if (k == Keys.Enter) { Move(Count); Hz(Indent(Ln)); count = ""; e.Handled = true; return; }
            if (k == Keys.Back) { if (c > 0) Hz(c - 1); e.Handled = true; return; }
            if (k == Keys.Delete) { Snap(); Del(); Clamp(); e.Handled = true; return; }
        }
        static int Indent(string s) { return Regex.Match(s, @"^\s*").Length; }
        void VimChar(char ch)
        {
            if (mode == "INSERT") { Ins(ch.ToString()); return; }
            if (mode == "COMMAND" || mode == "SEARCH" || mode == "SEARCHB") { cmdLine += ch; return; }
            msg = "";
            if (char.IsDigit(ch) && (ch != '0' || count != "")) { count += ch; return; }
            int n = Count;
            if (pending != "")
            {
                string p = pending + ch; pending = "";
                switch (p)
                {
                    case "dd": Snap(); yank = string.Join("\n", buf.Skip(r).Take(n)); yankLines = true; buf.RemoveRange(r, Math.Min(n, buf.Count - r)); if (buf.Count == 0) buf.Add(""); dirty = true; Clamp(); Hz(Indent(Ln)); break;
                    case "yy": yank = string.Join("\n", buf.Skip(r).Take(n)); yankLines = true; msg = Math.Min(n, buf.Count - r) + " line" + (n == 1 ? "" : "s") + " yanked"; break;
                    case "gg": r = count == "" ? 0 : n - 1; Clamp(); Hz(Indent(Ln)); break;
                    case "dw": case "cw": { Snap(); int e = WordEnd(r, c, p == "cw"); Ln = Ln.Remove(c, e - c); dirty = true; if (p == "cw") mode = "INSERT"; else Clamp(); break; }
                    case "d$": case "D$": Snap(); Ln = Ln.Substring(0, c); dirty = true; Clamp(); break;
                    case "cc": Snap(); Ln = new string(' ', Indent(Ln)); c = Ln.Length; mode = "INSERT"; dirty = true; break;
                    case "ZZ": if (dirty && !DoSave(Path)) break; Close(); break;
                    case "ZQ": Close(); break;
                    case ">>": Snap(); for (int i = r; i < Math.Min(buf.Count, r + n); i++) buf[i] = "    " + buf[i]; dirty = true; break;
                    case "<<": Snap(); for (int i = r; i < Math.Min(buf.Count, r + n); i++) buf[i] = buf[i].StartsWith("    ") ? buf[i].Substring(4) : buf[i].TrimStart(' '); dirty = true; Clamp(); break;
                    default: if (p[0] == 'r') { Snap(); if (Ln.Length > 0) { Ln = Ln.Remove(c, 1).Insert(c, p[1].ToString()); dirty = true; } } break;
                }
                count = ""; return;
            }
            switch (ch)
            {
                case 'h': Hz(c - n); break; case 'l': Hz(c + n); break; case 'j': Move(n); break; case 'k': Move(-n); break;
                case '0': Hz(0); break; case '^': Hz(Indent(Ln)); break; case '$': Hz(Ln.Length); break;
                case 'w': for (int i = 0; i < n; i++) WordFwd(); break; case 'b': for (int i = 0; i < n; i++) WordBack(); break; case 'e': Hz(Math.Max(c, WordEnd(r, c + 1, true) - 1)); break;
                case 'G': r = count == "" ? buf.Count - 1 : n - 1; Clamp(); Hz(Indent(Ln)); break;
                case 'g': case 'd': case 'y': case 'c': case 'Z': case 'r': case '>': case '<': pending = ch.ToString(); return;
                case 'D': Snap(); Ln = Ln.Substring(0, c); dirty = true; Clamp(); break;
                case 'C': Snap(); Ln = Ln.Substring(0, c); dirty = true; mode = "INSERT"; break;
                case 'x': if (Ln.Length > 0) { Snap(); int m = Math.Min(n, Ln.Length - c); yank = Ln.Substring(c, m); yankLines = false; Ln = Ln.Remove(c, m); dirty = true; Clamp(); } break;
                case 'X': if (c > 0) { Snap(); int m = Math.Min(n, c); Ln = Ln.Remove(c - m, m); c -= m; dirty = true; } break;
                case 'J': if (r < buf.Count - 1) { Snap(); c = Ln.Length; Ln = Ln.TrimEnd() + " " + buf[r + 1].TrimStart(); buf.RemoveAt(r + 1); dirty = true; } break;
                case 'p': case 'P':
                    if (yank == "") break; Snap(); dirty = true;
                    if (yankLines) { int at = ch == 'p' ? r + 1 : r; var ls = yank.Split('\n'); for (int i = 0; i < n; i++) buf.InsertRange(at, ls); r = at; Hz(Indent(Ln)); }
                    else { int at = ch == 'p' && Ln.Length > 0 ? c + 1 : c; Ln = Ln.Insert(Math.Min(at, Ln.Length), string.Concat(Enumerable.Repeat(yank, n))); c = at + yank.Length * n - 1; Clamp(); }
                    break;
                case 'u': Undo(); break;
                case 'i': Snap(); mode = "INSERT"; break;
                case 'a': Snap(); mode = "INSERT"; if (Ln.Length > 0) c++; break;
                case 'I': Snap(); mode = "INSERT"; c = Indent(Ln); break;
                case 'A': Snap(); mode = "INSERT"; c = Ln.Length; break;
                case 'o': Snap(); buf.Insert(r + 1, new string(' ', Indent(Ln))); r++; c = buf[r].Length; mode = "INSERT"; dirty = true; break;
                case 'O': Snap(); buf.Insert(r, new string(' ', Indent(Ln))); c = buf[r].Length; mode = "INSERT"; dirty = true; break;
                case 's': Snap(); if (Ln.Length > 0) Ln = Ln.Remove(c, 1); mode = "INSERT"; dirty = true; break;
                case ':': mode = "COMMAND"; cmdLine = ""; break;
                case '/': mode = "SEARCH"; cmdLine = ""; break;
                case '?': mode = "SEARCHB"; cmdLine = ""; break;
                case 'n': Find(search, 1); break; case 'N': Find(search, -1); break;
                case '*': { var m = Regex.Matches(Ln, @"\w+").Cast<Match>().FirstOrDefault(z => z.Index <= c && z.Index + z.Length > c); if (m != null) { search = m.Value; Find(search, 1); } break; }
                case '~': if (c < Ln.Length) { Snap(); char t = Ln[c]; Ln = Ln.Remove(c, 1).Insert(c, (char.IsUpper(t) ? char.ToLowerInvariant(t) : char.ToUpperInvariant(t)).ToString()); dirty = true; Hz(c + 1); } break;
            }
            count = "";
        }
        void WordFwd() { int i = c; string l = Ln; if (i >= l.Length) { if (r < buf.Count - 1) { r++; c = Indent(Ln); } return; } bool w = char.IsLetterOrDigit(l[i]) || l[i] == '_'; while (i < l.Length && !char.IsWhiteSpace(l[i]) && (char.IsLetterOrDigit(l[i]) || l[i] == '_') == w) i++; while (i < l.Length && char.IsWhiteSpace(l[i])) i++; if (i >= l.Length && r < buf.Count - 1) { r++; c = Indent(Ln); } else Hz(i); }
        void WordBack() { int i = c; string l = Ln; if (i == 0) { if (r > 0) { r--; Hz(Math.Max(0, Ln.Length - 1)); } return; } i--; while (i > 0 && char.IsWhiteSpace(l[i])) i--; bool w = char.IsLetterOrDigit(l[i]) || l[i] == '_'; while (i > 0 && !char.IsWhiteSpace(l[i - 1]) && (char.IsLetterOrDigit(l[i - 1]) || l[i - 1] == '_') == w) i--; Hz(i); }
        int WordEnd(int row, int from, bool noSpace) { string l = buf[row]; int i = Math.Min(from, l.Length); if (i >= l.Length) return l.Length; bool w = char.IsLetterOrDigit(l[i]) || l[i] == '_'; if (char.IsWhiteSpace(l[i])) { while (i < l.Length && char.IsWhiteSpace(l[i])) i++; return i; } while (i < l.Length && !char.IsWhiteSpace(l[i]) && (char.IsLetterOrDigit(l[i]) || l[i] == '_') == w) i++; if (!noSpace) while (i < l.Length && char.IsWhiteSpace(l[i])) i++; return i; }
        void ExCmd(string cmd)
        {
            int ln; if (int.TryParse(cmd, out ln)) { r = ln - 1; Clamp(); Hz(Indent(Ln)); return; }
            if (cmd == "$") { r = buf.Count - 1; Clamp(); return; }
            var m = Regex.Match(cmd, @"^(w|write|wq|x|q|quit|q!|wq!|w!|e!|set|noh|%s|s)(!?)(?:\s+(.*))?$");
            if (cmd.StartsWith("%s/", StringComparison.Ordinal) || cmd.StartsWith("s/", StringComparison.Ordinal)) { Subst(cmd); return; }
            switch (m.Success ? m.Groups[1].Value + m.Groups[2].Value : cmd)
            {
                case "w": case "write": case "w!": DoSave(m.Groups[3].Value.Trim() != "" ? m.Groups[3].Value.Trim() : Path); return;
                case "wq": case "x": case "wq!": if (!dirty && cmd == "x") { Close(); return; } if (DoSave(m.Groups[3].Value.Trim() != "" ? m.Groups[3].Value.Trim() : Path)) Close(); return;
                case "q": case "quit": if (dirty) { msg = "E37: No write since last change (add ! to override)"; return; } Close(); return;
                case "q!": Close(); return;
                case "set": { string o = m.Groups[3].Value.Trim(); if (o == "nu" || o == "number") numbers = true; else if (o == "nonu" || o == "nonumber") numbers = false; else msg = "E518: Unknown option: " + o; return; }
                case "noh": return;
            }
            msg = "E492: Not an editor command: " + cmd;
        }
        void Subst(string cmd)
        {
            bool all = cmd.StartsWith("%", StringComparison.Ordinal); var p = (all ? cmd.Substring(2) : cmd.Substring(1)).Split('/');
            if (p.Length < 3) { msg = "usage: :%s/old/new/g"; return; }
            bool g = p.Length > 3 && p[3].Contains("g"); Regex rx; try { rx = new Regex(p[1], p.Length > 3 && p[3].Contains("i") ? RegexOptions.IgnoreCase : RegexOptions.None); } catch (ArgumentException) { msg = "E486: bad pattern"; return; }
            Snap(); int n = 0;
            for (int i = all ? 0 : r; i <= (all ? buf.Count - 1 : r); i++) { int before = n; buf[i] = g ? rx.Replace(buf[i], z => { n++; return p[2]; }) : rx.Replace(buf[i], z => { n++; return p[2]; }, 1); }
            if (n > 0) dirty = true; msg = n + " substitution" + (n == 1 ? "" : "s"); Clamp();
        }

        // ---------- drawing (TermView hands us its font and cell size)
        int Page = 20;
        public void Paint(Graphics g, Font mono, float cw, float chh, float pad, float top, Size size, bool caretOn, bool focused)
        {
            int cols = Math.Max(10, (int)((size.Width - 2 * pad) / cw)); int rowsAll = Math.Max(4, (int)((size.Height - top - 6) / chh));
            int foot = Vim ? 1 : 3, head = Vim ? 0 : 1; int textRows = Math.Max(1, rowsAll - foot - head); Page = textRows;
            if (r < top0) top0 = r; if (r >= top0 + textRows) top0 = r - textRows + 1;
            int gutter = numbers || Vim && numbers ? buf.Count.ToString().Length + 1 : 0; int tcols = cols - gutter;
            if (c < left) left = c; if (c >= left + tcols - 1) left = c - tcols + 2;
            Func<int, float> Y = row => top + row * chh;
            TextFormatFlags tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping;
            if (!Vim)
            {   // nano's title bar: reversed
                using (var b = new SolidBrush(BarBg)) g.FillRectangle(b, pad - 4, Y(0), cols * cw + 8, chh);
                string title = "  AdeeboShell nano", name = Path == null ? "New Buffer" : Shown, right = dirty ? "Modified  " : "";
                TextRenderer.DrawText(g, title, mono, new Point((int)pad, (int)Y(0)), BarFg, tf);
                TextRenderer.DrawText(g, name, mono, new Point((int)(pad + Math.Max(title.Length + 2, (cols - name.Length) / 2) * cw), (int)Y(0)), BarFg, tf);
                TextRenderer.DrawText(g, right, mono, new Point((int)(pad + (cols - right.Length) * cw), (int)Y(0)), BarFg, tf);
            }
            bool sh = Path == null || Path.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) || (buf.Count > 0 && buf[0].StartsWith("#!", StringComparison.Ordinal));
            for (int i = 0; i < textRows; i++)
            {
                int li = top0 + i; float y = Y(head + i);
                if (li >= buf.Count) { if (Vim) TextRenderer.DrawText(g, "~", mono, new Point((int)pad, (int)y), Accent, tf); continue; }
                if (gutter > 0) TextRenderer.DrawText(g, (li + 1).ToString().PadLeft(gutter - 1), mono, new Point((int)pad, (int)y), Dim, tf);
                string line = buf[li]; int col = 0;
                foreach (var sg in sh ? Colour(line) : new List<TermView.Seg> { new TermView.Seg(line, Fg) })
                {
                    string t = sg.T; int a = Math.Max(0, left - col), b = Math.Min(t.Length, left + tcols - col);
                    if (b > a) TextRenderer.DrawText(g, t.Substring(a, b - a), mono, new Point((int)(pad + (gutter + col + a - left) * cw), (int)y), sg.C, tf);
                    col += t.Length;
                }
            }
            // the cursor: a block in normal mode, a bar while inserting (like vim in a modern terminal)
            bool onCmd = (Vim && (mode == "COMMAND" || mode == "SEARCH" || mode == "SEARCHB")) || askName || mode == "FIND";
            if (!onCmd && (caretOn || !focused))
            {
                float cx = pad + (gutter + c - left) * cw, cy = Y(head + r - top0);
                bool bar = Vim ? mode == "INSERT" : true;
                using (var cb = new SolidBrush(Color.FromArgb(focused ? 255 : 120, 63, 230, 120)))
                {
                    if (bar) g.FillRectangle(cb, cx, cy + 1, Math.Max(2, cw / 5), chh - 2);
                    else { g.FillRectangle(cb, cx, cy + 1, cw, chh - 2); if (c < Ln.Length) TextRenderer.DrawText(g, Ln.Substring(c, 1), mono, new Point((int)cx, (int)cy), Bg, tf); }
                }
            }
            // the bottom: vim's status/command line, or nano's message and shortcut rows
            float fy = Y(rowsAll - foot);
            if (Vim)
            {
                string left0 = mode == "COMMAND" ? ":" + cmdLine : mode == "SEARCH" ? "/" + cmdLine : mode == "SEARCHB" ? "?" + cmdLine : msg != "" ? msg : mode == "INSERT" ? "-- INSERT --" : "\"" + (Shown ?? "[No Name]") + "\"" + (dirty ? " [+]" : "");
                TextRenderer.DrawText(g, left0, mono, new Point((int)pad, (int)fy), msg.StartsWith("E", StringComparison.Ordinal) && mode == "NORMAL" ? Color.FromArgb(248, 81, 73) : mode == "INSERT" && msg == "" ? Cmd : Fg, tf);
                string pos = (pending + count).PadLeft(6) + "   " + (r + 1) + "," + (c + 1) + "        " + (buf.Count <= textRows ? "All" : top0 == 0 ? "Top" : top0 + textRows >= buf.Count ? "Bot" : (100 * top0 / Math.Max(1, buf.Count - textRows)) + "%");
                TextRenderer.DrawText(g, pos, mono, new Point((int)(pad + (cols - pos.Length) * cw), (int)fy), Dim, tf);
                if (onCmd && (caretOn || !focused)) using (var cb = new SolidBrush(Color.FromArgb(63, 230, 120))) g.FillRectangle(cb, pad + left0.Length * cw, fy + 1, cw, chh - 2);
            }
            else
            {
                string m0 = askSave ? "Save modified buffer?  Y Yes   N No   ^C Cancel" : askName ? "File Name to Write: " + cmdLine : mode == "FIND" ? "Search" + (search != "" ? " [" + search + "]" : "") + ": " + cmdLine : msg;
                if (m0 != "")
                {
                    bool rev = askSave || askName || mode == "FIND"; var sz = m0.Length * cw;
                    if (rev) using (var b = new SolidBrush(BarBg)) g.FillRectangle(b, pad - 4, fy, cols * cw + 8, chh);
                    float mx = rev ? pad : pad + Math.Max(0, (cols - m0.Length) / 2) * cw;
                    TextRenderer.DrawText(g, m0, mono, new Point((int)mx, (int)fy), rev ? BarFg : BarBg, tf);
                    if ((askName || mode == "FIND") && (caretOn || !focused)) using (var cb = new SolidBrush(Color.FromArgb(13, 17, 23))) g.FillRectangle(cb, pad + m0.Length * cw, fy + 1, Math.Max(2, cw / 5), chh - 2);
                }
                var help = new[] { new[] { "^G", "Help", "^S", "Save", "^W", "Where Is", "^K", "Cut", "^C", "Location" }, new[] { "^X", "Exit", "^O", "Write Out", "^Z", "Undo", "^U", "Paste", "^V", "Paste text" } };
                int cell = Math.Max(14, cols / 5);
                for (int hr = 0; hr < 2; hr++)
                    for (int i = 0; i < 5; i++)
                    {
                        float hx = pad + i * cell * cw, hy = fy + (hr + 1) * chh;
                        using (var b = new SolidBrush(BarBg)) g.FillRectangle(b, hx, hy + 1, 2 * cw, chh - 2);
                        TextRenderer.DrawText(g, help[hr][i * 2], mono, new Point((int)hx, (int)hy), BarFg, tf);
                        TextRenderer.DrawText(g, help[hr][i * 2 + 1], mono, new Point((int)(hx + 3 * cw), (int)hy), Fg, tf);
                    }
            }
        }
        int top0;
        static readonly HashSet<string> ShKw = new HashSet<string> { "for", "in", "do", "done", "while", "if", "then", "elif", "else", "fi", "test", "source", "export", "unset", "read", "echo", "exit", "true", "false", "parallel", "wait", "sleep", "seq" };
        List<TermView.Seg> Colour(string line)
        {
            var r0 = new List<TermView.Seg>(); int i = 0; bool first = true; var sb = new StringBuilder();
            Action<Color> flush = col => { if (sb.Length > 0) { r0.Add(new TermView.Seg(sb.ToString(), col)); sb.Clear(); } };
            while (i < line.Length)
            {
                char ch = line[i];
                if (ch == '#' && (i == 0 || line[i - 1] == ' ')) { r0.Add(new TermView.Seg(line.Substring(i), Dim)); return r0; }
                if (ch == '"' || ch == '\'') { int j = line.IndexOf(ch, i + 1); if (j < 0) j = line.Length - 1; r0.Add(new TermView.Seg(line.Substring(i, j - i + 1), Str)); i = j + 1; first = false; continue; }
                if (ch == '$') { var m = Regex.Match(line.Substring(i), @"^\$(\{?[A-Za-z_][A-Za-z0-9_]*\}?|\?|#|@|\d)"); if (m.Success) { r0.Add(new TermView.Seg(m.Value, Var)); i += m.Length; continue; } }
                if (ch == '|' || ch == ';' || ch == '&' || ch == '>' || ch == '<') { int j = i; while (j < line.Length && "|;&><".IndexOf(line[j]) >= 0) j++; r0.Add(new TermView.Seg(line.Substring(i, j - i), Accent)); i = j; first = true; continue; }
                if (char.IsWhiteSpace(ch)) { int j = i; while (j < line.Length && char.IsWhiteSpace(line[j])) j++; r0.Add(new TermView.Seg(line.Substring(i, j - i), Fg)); i = j; continue; }
                { int j = i; while (j < line.Length && !char.IsWhiteSpace(line[j]) && "|;&><\"'$".IndexOf(line[j]) < 0) j++; if (j == i) j = i + 1; string w = line.Substring(i, j - i); r0.Add(new TermView.Seg(w, ShKw.Contains(w) ? Kw : first && Words.Contains(w) ? Cmd : Fg)); if (!ShKw.Contains(w) || w == "echo" || w == "read" || w == "test" || w == "source" || w == "parallel") first = false; i = j; }
            }
            return r0;
        }
    }
}
