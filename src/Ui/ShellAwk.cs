// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AdeeboMeter
{
    // awk for AdeeboShell (shell scrutiny #28/#29/#74): a small, honest awk - BEGIN and END run once, patterns (/re/, comparisons,
    // && || !), expressions with concatenation and arithmetic, fields ($1, $NF, $(i+1), assignable), variables, arrays
    // (count[$1]++ ... for (k in count)), if / while / for, next / exit / break / continue, print and printf, and the usual
    // functions: length substr index split tolower toupper sprintf int sqrt sub gsub match. FS " " (the default) splits on runs of
    // whitespace, one other character splits on it, anything longer is a regular expression. What it doesn't know it says so.
    sealed class ShellAwk
    {
        public Action<string> Out; public Func<bool> Cancelled;
        readonly Dictionary<string, string> vars = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, Dictionary<string, string>> arrs = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        List<string> f = new List<string> { "" }; long nr, fnr; long steps;
        internal bool BeginOnly { get { return rules.Count == 0 && ends.Count == 0; } }   // test6: awk 'BEGIN{...}' reads nothing (POSIX), so it never asks for input
        readonly List<Action> begins = new List<Action>(), ends = new List<Action>(); readonly List<KeyValuePair<Func<bool>, Action>> rules = new List<KeyValuePair<Func<bool>, Action>>();
        sealed class Next : Exception { } sealed class NextFile : Exception { } sealed class Exit : Exception { public int Code; } sealed class Brk : Exception { } sealed class Cont : Exception { }
        sealed class LV { public Func<string> Get; public Action<string> Set; }

        public ShellAwk(string program, string fs, IEnumerable<KeyValuePair<string, string>> pre)
        {
            vars["FS"] = fs ?? " "; vars["OFS"] = " "; vars["ORS"] = "\n"; vars["SUBSEP"] = "\u001c";
            foreach (var kv in pre) vars[kv.Key] = kv.Value;
            toks = Lex(program); Program();
        }
        // runs the program over the lines; the exit code
        public int Run(IEnumerable<string> lines) { return Run(new[] { new KeyValuePair<string, IEnumerable<string>>("", lines) }); }
        // file by file: FNR starts again and FILENAME is its name; NR runs on (a finding of the shell fix check)
        bool inEnd;
        public int Run(IEnumerable<KeyValuePair<string, IEnumerable<string>>> inputs)
        {
            try
            {
                try { foreach (var b in begins) b(); } catch (Next) { throw new FormatException("next can't be used in BEGIN"); } catch (NextFile) { throw new FormatException("nextfile can't be used in BEGIN"); }
                if (rules.Count > 0 || ends.Count > 0)
                    foreach (var inp in inputs)
                    {
                        vars["FILENAME"] = inp.Key; fnr = 0;
                        try
                        {
                            foreach (var line in inp.Value)
                            {
                                nr++; fnr++; SetRecord(line);
                                try { foreach (var r in rules) if (r.Key == null || r.Key()) r.Value(); }
                                catch (Next) { }
                            }
                        }
                        catch (NextFile) { }
                    }
                inEnd = true;
                try { foreach (var e in ends) e(); } catch (Next) { throw new FormatException("next can't be used in END"); } catch (NextFile) { throw new FormatException("nextfile can't be used in END"); }
                return 0;
            }
            // exit in BEGIN or a rule runs END once; exit inside END stops there (hunt-5: END used to run a second time)
            catch (Exit x) { if (!inEnd && x.Code != 130) { inEnd = true; try { foreach (var e in ends) e(); } catch (Exit x2) { return x2.Code; } catch (Next) { } } return x.Code; }
            catch (Brk) { throw new FormatException("break outside a loop"); }
            catch (Cont) { throw new FormatException("continue outside a loop"); }
        }
        void Tick() { if (++steps > 20000000) throw new FormatException("stopped after 20 million steps (an endless loop?)"); if ((steps & 0xFFFF) == 0 && Cancelled != null && Cancelled()) throw new Exit { Code = 130 }; }

        // ---------- values: strings that are numbers when they look like one
        static readonly Regex NumRx = new Regex(@"^\s*[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?", RegexOptions.CultureInvariant);
        static double Num(string s) { var m = NumRx.Match(s ?? ""); double d; return m.Success && double.TryParse(m.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0; }
        static bool LooksNum(string s) { s = (s ?? "").Trim(); double d; return s != "" && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d); }
        static string Str(double d) { if (double.IsNaN(d)) return "nan"; if (d == Math.Floor(d) && Math.Abs(d) < 1e16) return ((long)d).ToString(CultureInfo.InvariantCulture); return d.ToString("G6", CultureInfo.InvariantCulture); }
        static bool True(string s) { return LooksNum(s) ? Num(s) != 0 : s != ""; }

        // ---------- records and fields
        void SetRecord(string line) { f = new List<string> { line }; f.AddRange(Split(line, Var("FS"))); }
        static IEnumerable<string> Split(string s, string fs)
        {
            if (fs == " ") return s.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (s == "") return new string[0];
            if (fs.Length == 1 && fs != "\\") return s.Split(fs[0]);
            return new Regex(fs, RegexOptions.None, TimeSpan.FromSeconds(2)).Split(s);
        }
        string Field(int i) { if (i < 0) throw new FormatException("a field number below 0"); return i < f.Count ? f[i] : ""; }
        void SetField(int i, string v)
        {
            if (i == 0) { SetRecord(v); return; }
            while (f.Count <= i) f.Add(""); f[i] = v; f[0] = string.Join(Var("OFS"), f.Skip(1));
        }
        string Var(string n)
        {
            switch (n) { case "NR": return nr.ToString(CultureInfo.InvariantCulture); case "FNR": return fnr.ToString(CultureInfo.InvariantCulture); case "NF": return (f.Count - 1).ToString(CultureInfo.InvariantCulture); }
            string v; return vars.TryGetValue(n, out v) ? v : "";
        }
        void SetVar(string n, string v)
        {
            if (n == "NF") { int k = (int)Num(v); while (f.Count - 1 > k && f.Count > 1) f.RemoveAt(f.Count - 1); while (f.Count - 1 < k) f.Add(""); f[0] = string.Join(Var("OFS"), f.Skip(1)); return; }
            if (n == "NR") { nr = (long)Num(v); return; }
            if (n == "FNR") { fnr = (long)Num(v); return; }
            vars[n] = v;
        }
        Dictionary<string, string> Arr(string n) { Dictionary<string, string> a; if (!arrs.TryGetValue(n, out a)) arrs[n] = a = new Dictionary<string, string>(StringComparer.Ordinal); return a; }

        // ---------- tokens
        enum K { Num, Str, Re, Id, Op, Sep, End }
        sealed class Tk { public K K; public string S; public override string ToString() { return S; } }
        readonly List<Tk> toks; int p;
        static List<Tk> Lex(string s)
        {
            var l = new List<Tk>(); int i = 0;
            Func<bool> valueBefore = () => { if (l.Count == 0) return false; var t = l[l.Count - 1]; return t.K == K.Num || t.K == K.Str || (t.K == K.Id && !Keywords.Contains(t.S)) || t.S == ")" || t.S == "]" || t.S == "$" || t.S == "++" || t.S == "--"; };
            while (i < s.Length)
            {
                char ch = s[i];
                if (ch == ' ' || ch == '\t' || ch == '\r') { i++; continue; }
                if (ch == '\\' && i + 1 < s.Length && s[i + 1] == '\n') { i += 2; continue; }
                if (ch == '\n' || ch == ';') { l.Add(new Tk { K = K.Sep, S = ch == ';' ? ";" : "\n" }); i++; continue; }
                if (ch == '#') { while (i < s.Length && s[i] != '\n') i++; continue; }
                if (char.IsDigit(ch) || (ch == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1]))) { var m = Regex.Match(s.Substring(i), @"^(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?"); l.Add(new Tk { K = K.Num, S = m.Value }); i += m.Length; continue; }
                if (char.IsLetter(ch) || ch == '_') { int j = i; while (j < s.Length && (char.IsLetterOrDigit(s[j]) || s[j] == '_')) j++; l.Add(new Tk { K = K.Id, S = s.Substring(i, j - i) }); i = j; continue; }
                if (ch == '"')
                {
                    var sb = new StringBuilder(); i++;
                    while (i < s.Length && s[i] != '"') { if (s[i] == '\\' && i + 1 < s.Length) { char n = s[++i]; sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n == 'r' ? '\r' : n == '\\' ? '\\' : n == '"' ? '"' : n == '/' ? '/' : n); if (n != 'n' && n != 't' && n != 'r' && n != '\\' && n != '"' && n != '/') sb.Insert(sb.Length - 1, '\\'); i++; continue; } sb.Append(s[i++]); }
                    if (i >= s.Length) throw new FormatException("a \" isn't closed"); i++; l.Add(new Tk { K = K.Str, S = sb.ToString() }); continue;
                }
                if (ch == '/' && !valueBefore())
                {
                    var sb = new StringBuilder(); i++;
                    while (i < s.Length && s[i] != '/') { if (s[i] == '\\' && i + 1 < s.Length) { if (s[i + 1] == '/') { sb.Append('/'); i += 2; continue; } sb.Append(s[i]).Append(s[i + 1]); i += 2; continue; } sb.Append(s[i++]); }
                    if (i >= s.Length) throw new FormatException("a /regex/ isn't closed"); i++; l.Add(new Tk { K = K.Re, S = sb.ToString() }); continue;
                }
                string[] ops = { "+=", "-=", "*=", "/=", "%=", "^=", "==", "!=", "<=", ">=", "&&", "||", "++", "--", "!~", "**" };
                string two = i + 1 < s.Length ? s.Substring(i, 2) : "";
                if (ops.Contains(two)) { l.Add(new Tk { K = K.Op, S = two == "**" ? "^" : two }); i += 2; continue; }
                if ("+-*/%^=<>!~(){}[],$?:".IndexOf(ch) >= 0) { l.Add(new Tk { K = K.Op, S = ch.ToString() }); i++; continue; }
                throw new FormatException("unexpected '" + ch + "'");
            }
            l.Add(new Tk { K = K.End, S = "" }); return l;
        }
        Tk Peek(int k = 0) { return toks[Math.Min(p + k, toks.Count - 1)]; }
        bool Is(string s) { var t = Peek(); return (t.K == K.Op || t.K == K.Id) && t.S == s; }
        void Expect(string s) { if (!Is(s)) throw new FormatException("expected '" + s + "' near '" + Peek().S + "'"); p++; }
        void Seps() { while (Peek().K == K.Sep) p++; }
        void Nls() { while (Peek().K == K.Sep && Peek().S == "\n") p++; }

        // ---------- program: BEGIN { } · pattern { } · { } · END { }
        void Program()
        {
            Seps();
            while (Peek().K != K.End)
            {
                if (Is("BEGIN")) { p++; Nls(); begins.Add(Block()); }
                else if (Is("END")) { p++; Nls(); ends.Add(Block()); }
                else if (Is("{")) rules.Add(new KeyValuePair<Func<bool>, Action>(null, Block()));
                else
                {
                    var e = Expr(true); Func<bool> pat = () => True(e());
                    if (Is(",")) throw new FormatException("range patterns (p1, p2) aren't supported here");
                    rules.Add(new KeyValuePair<Func<bool>, Action>(pat, Is("{") ? Block() : () => Print(new List<Func<string>>())));
                }
                Seps();
            }
        }
        Action Block()
        {
            Expect("{"); var l = new List<Action>(); Seps();
            while (!Is("}")) { if (Peek().K == K.End) throw new FormatException("a { isn't closed"); l.Add(Stmt()); Seps(); }
            p++; return () => { foreach (var a in l) a(); };
        }
        Action Simple(Action a) { if (!(Peek().K == K.Sep || Is("}") || Peek().K == K.End)) throw new FormatException("unexpected '" + Peek().S + "'"); return a; }
        Action Stmt()
        {
            Tick();
            if (Is("{")) return Block();
            if (Is("if"))
            {
                p++; Expect("("); var c = Expr(true); Expect(")"); Nls(); var then = Stmt(); int save = p; Seps(); Action els = null;
                if (Is("else")) { p++; Nls(); els = Stmt(); } else p = save;
                return () => { if (True(c())) then(); else if (els != null) els(); };
            }
            if (Is("while"))
            {
                p++; Expect("("); var c = Expr(true); Expect(")"); Nls(); var body = Stmt();
                return () => { while (True(c())) { Tick(); try { body(); } catch (Brk) { break; } catch (Cont) { } } };
            }
            if (Is("for"))
            {
                p++; Expect("(");
                if (Peek().K == K.Id && Peek(1).S == "in" && Peek(2).K == K.Id && Peek(3).S == ")")
                {
                    string v = Peek().S, an = Peek(2).S; p += 4; Nls(); var body = Stmt();
                    return () => { foreach (var k in Arr(an).Keys.ToList()) { Tick(); SetVar(v, k); try { body(); } catch (Brk) { break; } catch (Cont) { } } };
                }
                Func<string> init = Is(";") ? null : Expr(true); Expect(";"); Func<string> cond = Is(";") ? null : Expr(true); Expect(";"); Func<string> step = Is(")") ? null : Expr(true); Expect(")"); Nls(); var b2 = Stmt();
                return () => { if (init != null) init(); while (cond == null || True(cond())) { Tick(); try { b2(); } catch (Brk) { break; } catch (Cont) { } if (step != null) step(); } };
            }
            if (Is("nextfile")) { p++; return Simple(() => { throw new NextFile(); }); }   // round 6 D2-23
            if (Is("next")) { p++; return Simple(() => { throw new Next(); }); }
            if (Is("exit")) { p++; var e = Peek().K == K.Sep || Is("}") || Peek().K == K.End ? null : Expr(true); return Simple(() => { throw new Exit { Code = e == null ? 0 : (int)Num(e()) }; }); }
            if (Is("break")) { p++; return Simple(() => { throw new Brk(); }); }
            if (Is("continue")) { p++; return Simple(() => { throw new Cont(); }); }
            if (Is("delete")) { p++; if (Peek().K != K.Id) throw new FormatException("delete needs an array"); string an = Peek().S; p++; if (Is("[")) { var sub = Subscript(); return Simple(() => Arr(an).Remove(sub())); } return Simple(() => Arr(an).Clear()); }
            if (Is("getline")) throw new FormatException("getline isn't supported here - pipe the input in");
            if (Is("print") || Is("printf"))
            {
                bool pf = Peek().S == "printf"; p++; var args = new List<Func<string>>();
                bool paren = Is("(") && ParenList(); if (paren) p++;
                if (!(Peek().K == K.Sep || Is("}") || Peek().K == K.End || (paren && Is(")"))))
                { args.Add(Expr(false)); while (Is(",")) { p++; Nls(); args.Add(Expr(false)); } }
                if (paren) Expect(")");
                if (Is(">") || Is("|")) throw new FormatException("print to a file or a command isn't supported here - put > file after the whole awk command");
                if (pf && args.Count == 0) throw new FormatException("printf needs a format");
                return Simple(pf ? (Action)(() => Out(Printf(args.Select(z => z()).ToList()))) : () => Print(args));
            }
            var ex = Expr(true); return Simple(() => ex());
        }
        // print (a, b): a parenthesised list - not (a) b
        bool ParenList() { int depth = 0; for (int k = p; k < toks.Count; k++) { var s = toks[k].S; if (toks[k].K == K.Op && s == "(") depth++; else if (toks[k].K == K.Op && s == ")") { if (--depth == 0) { var nx = toks[Math.Min(k + 1, toks.Count - 1)]; return nx.K == K.Sep || nx.K == K.End || nx.S == "}"; } } } return false; }
        void Print(List<Func<string>> args) { Out((args.Count == 0 ? Field(0) : string.Join(Var("OFS"), args.Select(a => a()))) + Var("ORS")); }
        Func<string> Subscript() { Expect("["); var l = new List<Func<string>> { Expr(true) }; while (Is(",")) { p++; l.Add(Expr(true)); } Expect("]"); return () => string.Join(Var("SUBSEP"), l.Select(z => z())); }

        // ---------- expressions, lowest to highest: assignment, ?:, ||, &&, in, ~ !~, comparison, concatenation, + -, * / %, unary, ^, ++ --, $
        Func<string> Expr(bool gt) { return Assign(gt); }
        Func<string> Assign(bool gt)
        {
            int save = p; var lv = TryLValue();
            if (lv != null && Peek().K == K.Op && new[] { "=", "+=", "-=", "*=", "/=", "%=", "^=" }.Contains(Peek().S) && !(Peek().S == "=" && Peek(1).S == "="))
            {
                string op = Peek().S; p++; Nls(); var rhs = Assign(gt);
                return () => { string v = rhs(); if (op != "=") { double a = Num(lv.Get()), b = Num(v); v = Str(op == "+=" ? a + b : op == "-=" ? a - b : op == "*=" ? a * b : op == "/=" ? Div(a, b) : op == "%=" ? Mod(a, b) : Math.Pow(a, b)); } lv.Set(v); return v; };
            }
            p = save; return Ternary(gt);
        }
        Func<string> Ternary(bool gt)
        {
            var c = Or(gt); if (!Is("?")) return c;
            p++; var a = Assign(gt); Expect(":"); var b = Assign(gt); return () => True(c()) ? a() : b();
        }
        Func<string> Or(bool gt) { var l = And(gt); while (Is("||")) { p++; Nls(); var r = And(gt); var l0 = l; l = () => True(l0()) || True(r()) ? "1" : "0"; } return l; }
        Func<string> And(bool gt) { var l = In(gt); while (Is("&&")) { p++; Nls(); var r = In(gt); var l0 = l; l = () => True(l0()) && True(r()) ? "1" : "0"; } return l; }
        Func<string> In(bool gt)
        {
            var l = Match(gt);
            while (Is("in") && Peek(1).K == K.Id) { string an = Peek(1).S; p += 2; var l0 = l; l = () => Arr(an).ContainsKey(l0()) ? "1" : "0"; }
            return l;
        }
        Func<string> Match(bool gt)
        {
            var l = Cmp(gt);
            while (Is("~") || Is("!~")) { bool neg = Peek().S == "!~"; p++; var r = ReOf(Cmp(gt)); var l0 = l; l = () => r().IsMatch(l0()) != neg ? "1" : "0"; }
            return l;
        }
        Func<Regex> ReOf(Func<string> e) { var cache = new Dictionary<string, Regex>(); return () => { string s = e(); Regex r; if (!cache.TryGetValue(s, out r)) cache[s] = r = new Regex(s, RegexOptions.None, TimeSpan.FromSeconds(2)); return r; }; }
        Func<string> Cmp(bool gt)
        {
            int ls0 = p; var l = Concat(gt); int ls1 = p;
            var t = Peek(); if (t.K == K.Op && (t.S == "<" || t.S == "<=" || t.S == "==" || t.S == "!=" || t.S == ">=" || (t.S == ">" && gt)))
            {
                string op = t.S; p++; int rs0 = p; var r = Concat(gt); int rs1 = p;
                // POSIX: a "string" written in the program makes it a string comparison ("10" < "9" is true); numbers that
                // came in as input (fields, variables from them) compare as numbers when both look like one
                bool strK = toks.Skip(ls0).Take(ls1 - ls0).Any(z => z.K == K.Str) || toks.Skip(rs0).Take(rs1 - rs0).Any(z => z.K == K.Str);
                return () =>
                {
                    string a = l(), b = r(); int c;
                    if (!strK && LooksNum(a) && LooksNum(b)) c = Num(a).CompareTo(Num(b)); else c = string.CompareOrdinal(a, b);
                    bool ok = op == "<" ? c < 0 : op == "<=" ? c <= 0 : op == "==" ? c == 0 : op == "!=" ? c != 0 : op == ">=" ? c >= 0 : c > 0;
                    return ok ? "1" : "0";
                };
            }
            return l;
        }
        bool StartsValue() { var t = Peek(); return t.K == K.Num || t.K == K.Str || t.K == K.Re || (t.K == K.Id && t.S != "in" && t.S != "getline") || (t.K == K.Op && (t.S == "$" || t.S == "(" || t.S == "!" || t.S == "-" || t.S == "+" || t.S == "++" || t.S == "--")); }
        Func<string> Concat(bool gt)
        {
            var l = Add();
            while (StartsValue() && !(Peek().K == K.Op && (Peek().S == "-" || Peek().S == "+"))) { var r = Add(); var l0 = l; l = () => { string x = l0() + r(); if (x.Length > 8 * 1024 * 1024) throw new FormatException("a string over 8 M characters - stopped (review 1.1.6: memory budget)"); return x; }; }   // "a" "b" and $1 $2 join; a -1 stays a minus
            return l;
        }
        Func<string> Add() { var l = Mul(); while (Is("+") || Is("-")) { string op = Peek().S; p++; var r = Mul(); var l0 = l; l = () => Str(op == "+" ? Num(l0()) + Num(r()) : Num(l0()) - Num(r())); } return l; }
        static double Div(double a, double b) { if (b == 0) throw new FormatException("division by zero"); return a / b; }
        static double Mod(double a, double b) { if (b == 0) throw new FormatException("division by zero in %"); return a % b; }
        Func<string> Mul() { var l = Unary(); while (Is("*") || Is("/") || Is("%")) { string op = Peek().S; p++; var r = Unary(); var l0 = l; l = () => Str(op == "*" ? Num(l0()) * Num(r()) : op == "/" ? Div(Num(l0()), Num(r())) : Mod(Num(l0()), Num(r()))); } return l; }
        Func<string> Unary()
        {
            if (Is("!")) { p++; var e = Unary(); return () => True(e()) ? "0" : "1"; }
            if (Is("-")) { p++; var e = Unary(); return () => Str(-Num(e())); }
            if (Is("+")) { p++; var e = Unary(); return () => Str(Num(e())); }
            return Pow();
        }
        Func<string> Pow() { var l = Post(); if (Is("^")) { p++; var r = Unary(); var l0 = l; return () => Str(Math.Pow(Num(l0()), Num(r()))); } return l; }
        Func<string> Post()
        {
            if (Is("++") || Is("--")) { bool inc = Peek().S == "++"; p++; var lv = TryLValue(); if (lv == null) throw new FormatException("++/-- needs a variable"); return () => { string v = Str(Num(lv.Get()) + (inc ? 1 : -1)); lv.Set(v); return v; }; }
            int save = p; var lv2 = TryLValue();
            if (lv2 != null) { if (Is("++") || Is("--")) { bool inc = Peek().S == "++"; p++; return () => { string old = Str(Num(lv2.Get())); lv2.Set(Str(Num(old) + (inc ? 1 : -1))); return old; }; } return lv2.Get; }
            p = save; return Primary();
        }
        // a variable, an array element or a field - or null (the position is left alone)
        LV TryLValue()
        {
            int save = p; var t = Peek();
            if (t.K == K.Op && t.S == "$")
            {
                p++; Func<string> idx;
                if (Is("++") || Is("--") || Is("-")) { idx = Unary(); } else { int s2 = p; var lv = TryLValue(); idx = lv != null ? lv.Get : (Func<string>)null; if (idx == null) { p = s2; idx = Primary(); } }
                var ix = idx; return new LV { Get = () => Field((int)Num(ix())), Set = v => SetField((int)Num(ix()), v) };
            }
            if (t.K == K.Id && !Builtins.Contains(t.S) && !Keywords.Contains(t.S))
            {
                p++; string n = t.S;
                if (Is("[")) { var sub = Subscript(); return new LV { Get = () => { string v; return Arr(n).TryGetValue(sub(), out v) ? v : ""; }, Set = v => Arr(n)[sub()] = v }; }
                if (Is("(")) { p = save; return null; }
                return new LV { Get = () => Var(n), Set = v => SetVar(n, v) };
            }
            p = save; return null;
        }
        static readonly HashSet<string> Keywords = new HashSet<string> { "BEGIN", "END", "if", "else", "while", "for", "do", "in", "next", "nextfile", "exit", "break", "continue", "delete", "print", "printf", "getline", "function", "return" };
        static readonly HashSet<string> Builtins = new HashSet<string> { "length", "substr", "index", "split", "tolower", "toupper", "sprintf", "int", "sqrt", "log", "exp", "sin", "cos", "atan2", "rand", "srand", "sub", "gsub", "match" };
        Random rnd = new Random(0); double seed;   // like awk: the same numbers each run until srand() is called
        Func<string> Primary()
        {
            var t = Peek();
            if (t.K == K.Num) { p++; string v = Str(Num(t.S)); return () => v; }
            if (t.K == K.Str) { p++; string v = t.S; return () => v; }
            if (t.K == K.Re) { p++; var r = new Regex(t.S, RegexOptions.None, TimeSpan.FromSeconds(2)); return () => r.IsMatch(Field(0)) ? "1" : "0"; }
            if (Is("(")) { p++; var e = Expr(true); Expect(")"); return e; }
            if (t.K == K.Id && Builtins.Contains(t.S))
            {
                string fn = t.S; p++;
                if (fn == "length" && !Is("(")) return () => Field(0).Length.ToString(CultureInfo.InvariantCulture);
                Expect("("); var args = new List<Func<string>>(); var lvs = new List<LV>(); var raw = new List<int>();
                if (!Is(")")) { do { if (Is(",")) p++; raw.Add(p); int s2 = p; var lv = TryLValue(); if (lv != null && (Is(",") || Is(")"))) { lvs.Add(lv); args.Add(lv.Get); } else { p = s2; lvs.Add(null); args.Add(Peek().K == K.Re ? RegexArg() : Expr(true)); } } while (Is(",")); }
                Expect(")");
                Func<int, string> A = i => i < args.Count ? args[i]() : "";
                switch (fn)
                {
                    case "length": { string an = args.Count == 1 && toks[raw[0]].K == K.Id && toks[raw[0] + 1].S == ")" ? toks[raw[0]].S : null; return () => an != null && arrs.ContainsKey(an) ? Arr(an).Count.ToString(CultureInfo.InvariantCulture) : A(0).Length.ToString(CultureInfo.InvariantCulture); }   // an array is looked for when it runs, not when it's read
                    case "substr": return () => { string s = A(0); double m = Math.Round(Num(A(1))), n = args.Count > 2 ? Math.Round(Num(A(2))) : double.MaxValue; double start = Math.Max(1, m), end = args.Count > 2 ? Math.Min(s.Length + 1, m + n) : s.Length + 1; return end <= start ? "" : s.Substring((int)start - 1, (int)(end - start)); };
                    case "index": return () => (A(0).IndexOf(A(1), StringComparison.Ordinal) + 1).ToString(CultureInfo.InvariantCulture);
                    case "split": { if (args.Count < 2 || toks[raw[1]].K != K.Id) throw new FormatException("split(s, array[, sep])"); string an = toks[raw[1]].S; return () => { var a = Arr(an); a.Clear(); var parts = Split(A(0), args.Count > 2 ? A(2) : Var("FS")).ToList(); for (int i = 0; i < parts.Count; i++) a[(i + 1).ToString(CultureInfo.InvariantCulture)] = parts[i]; return parts.Count.ToString(CultureInfo.InvariantCulture); }; }
                    case "tolower": return () => A(0).ToLowerInvariant();
                    case "toupper": return () => A(0).ToUpperInvariant();
                    case "sprintf": return () => Printf(args.Select(z => z()).ToList());
                    case "int": return () => Str(Math.Truncate(Num(A(0))));
                    case "sqrt": return () => Str(Math.Sqrt(Num(A(0))));
                    case "log": return () => Str(Math.Log(Num(A(0))));
                    case "exp": return () => Str(Math.Exp(Num(A(0))));
                    case "sin": return () => Str(Math.Sin(Num(A(0))));
                    case "cos": return () => Str(Math.Cos(Num(A(0))));
                    case "atan2": return () => Str(Math.Atan2(Num(A(0)), Num(A(1))));
                    case "rand": return () => Str(rnd.NextDouble());
                    case "srand": return () => { double old = seed; seed = args.Count > 0 ? Num(A(0)) : Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds); rnd = new Random((int)(long)seed); return Str(old); };
                    case "match": return () => { var m = new Regex(A(1), RegexOptions.None, TimeSpan.FromSeconds(2)).Match(A(0)); SetVar("RSTART", (m.Success ? m.Index + 1 : 0).ToString(CultureInfo.InvariantCulture)); SetVar("RLENGTH", (m.Success ? m.Length : -1).ToString(CultureInfo.InvariantCulture)); return m.Success ? (m.Index + 1).ToString(CultureInfo.InvariantCulture) : "0"; };
                    case "sub": case "gsub":
                        {
                            var target = lvs.Count > 2 && lvs[2] != null ? lvs[2] : new LV { Get = () => Field(0), Set = v => SetField(0, v) }; bool all = fn == "gsub";
                            return () => { var rx = new Regex(A(0), RegexOptions.None, TimeSpan.FromSeconds(2)); string rep = A(1), s = target.Get(); int n = 0; string res = rx.Replace(s, m => { n++; return rep.Replace("\\&", "\u0002").Replace("&", m.Value).Replace("\u0002", "&"); }, all ? -1 : 1); if (n > 0) target.Set(res); return n.ToString(CultureInfo.InvariantCulture); };
                        }
                }
            }
            if (t.K == K.Id && Peek(1).S == "(" && !Keywords.Contains(t.S)) throw new FormatException(t.S + "(): not a function this awk knows");
            throw new FormatException("unexpected '" + (t.K == K.End ? "end of the program" : t.S) + "'");
        }
        Func<string> RegexArg() { string s = Peek().S; p++; return () => s; }   // gsub(/re/, ...): the pattern itself, not "does $0 match"

        // printf / sprintf: %s %d %i %f %e %g %x %X %o %c %% with flags, width (or *) and precision; the format is used once
        static string Printf(List<string> a)
        {
            if (a.Count == 0) return ""; int ai = 1;
            return Regex.Replace(a[0], @"%([-+ 0#]*)(\*|\d+)?(?:\.(\*|\d+))?([sdiufeEgGxXoc%])", m =>   /* hunters11 shell L-7: %u and .* as in the shell's printf */
            {
                char k = m.Groups[4].Value[0]; if (k == '%') return "%";
                string fl = m.Groups[1].Value; int w = 0; if (m.Groups[2].Value == "*") w = (int)Num(ai < a.Count ? a[ai++] : "0"); else if (m.Groups[2].Success) int.TryParse(m.Groups[2].Value, out w);
                int pr = -1; if (m.Groups[3].Value == "*") pr = (int)Num(ai < a.Count ? a[ai++] : "0"); else if (m.Groups[3].Success && !int.TryParse(m.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out pr)) pr = int.MaxValue; string v = ai < a.Count ? a[ai++] : "", r;
                if (w > 1000000 || w < -1000000 || pr > 1000000) throw new FormatException("printf: a width or precision over 1,000,000 isn't done");   // review 1.1.6 (security #9)
                switch (k)
                {
                    case 's': r = pr >= 0 && v.Length > pr ? v.Substring(0, pr) : v; break;
                    case 'c': r = LooksNum(v) ? ((char)(int)Num(v)).ToString() : v.Length > 0 ? v.Substring(0, 1) : ""; break;
                    case 'u': { double uv = Math.Truncate(Num(v)); r = (uv < 0 ? unchecked((ulong)(long)uv) : (ulong)uv).ToString(CultureInfo.InvariantCulture); break; }
                    case 'd': case 'i': r = ((long)Math.Truncate(Num(v))).ToString(CultureInfo.InvariantCulture); if (fl.Contains("+") && Num(v) >= 0) r = "+" + r; break;
                    case 'x': r = ((long)Num(v)).ToString("x", CultureInfo.InvariantCulture); break;
                    case 'X': r = ((long)Num(v)).ToString("X", CultureInfo.InvariantCulture); break;
                    case 'o': r = Convert.ToString((long)Num(v), 8); break;
                    case 'e': case 'E': r = Num(v).ToString((k == 'e' ? "e" : "E") + (pr >= 0 ? pr : 6), CultureInfo.InvariantCulture); break;
                    case 'g': case 'G': r = Num(v).ToString("G" + (pr >= 0 ? Math.Max(1, pr) : 6), CultureInfo.InvariantCulture); break;
                    default: r = Num(v).ToString("F" + (pr >= 0 ? pr : 6), CultureInfo.InvariantCulture); if (fl.Contains("+") && Num(v) >= 0) r = "+" + r; break;
                }
                if (r.Length < w) r = fl.Contains("-") ? r.PadRight(w) : fl.Contains("0") && k != 's' && k != 'c' ? (r.StartsWith("-", StringComparison.Ordinal) ? "-" + r.Substring(1).PadLeft(w - 1, '0') : r.PadLeft(w, '0')) : r.PadLeft(w);
                return r;
            }).Replace("\\n", "\n").Replace("\\t", "\t");
        }
    }

    sealed partial class FullView
    {
        // awk [-F sep] [-v name=value] 'program' [file...]
        int Awk(List<string> a, ShCtx c)
        {
            string fs = null, prog = null; var files = new List<string>(); var pre = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < a.Count; i++)
            {
                string w = a[i];
                if (prog == null && w == "-F" && i + 1 < a.Count) { fs = a[++i]; continue; }
                if (prog == null && w.StartsWith("-F", StringComparison.Ordinal)) { fs = w.Substring(2); continue; }
                if (prog == null && w == "-v" && i + 1 < a.Count) { var m = Regex.Match(a[++i], "^([A-Za-z_][A-Za-z0-9_]*)=(.*)$", RegexOptions.Singleline); if (!m.Success) { Err(c, "awk: -v needs name=value"); return 2; } pre.Add(new KeyValuePair<string, string>(m.Groups[1].Value, m.Groups[2].Value)); continue; }
                if (prog == null) prog = w; else files.Add(w);
            }
            if (prog == null) { Err(c, "usage: awk '{print $1}'  ·  awk -F, 'NR>1 {print $2, $5}'  ·  awk '{n[$1]++} END {for (k in n) print k, n[k]}'"); return 2; }
            if (fs == "t" || fs == "\\t") fs = "\t";   // -F' ' is the default whitespace split (#74)
            ShellAwk awk;
            try { awk = new ShellAwk(prog, fs, pre); } catch (FormatException e) { Err(c, "awk: " + e.Message); return 2; } catch (ArgumentException e) { Err(c, "awk: a pattern isn't valid - " + e.Message); return 2; }
            var outp = new StringBuilder(); awk.Out = s => { outp.Append(s); if (outp.Length > 65536) { O(c, outp.ToString(), ShFg); outp.Clear(); } }; awk.Cancelled = () => c.Job.Cancel;
            if (files.Count == 0 && !awk.BeginOnly && NoInput(c)) return 1;
            IEnumerable<string> lines = files.Count == 0 ? (awk.BeginOnly ? new List<string>() : SplitLines(c.Stdin)) : null;
            var inputs = new List<KeyValuePair<string, IEnumerable<string>>>();
            if (files.Count == 0) inputs.Add(new KeyValuePair<string, IEnumerable<string>>("", lines));
            else foreach (var f in files) { var ls = StreamLines(f, c); if (ls == null) return 2; inputs.Add(new KeyValuePair<string, IEnumerable<string>>(f, ls)); }
            int ex;
            try { ex = awk.Run(inputs); }
            catch (FormatException e) { O(c, outp.ToString(), ShFg); Err(c, "awk: " + e.Message); return 2; }
            catch (RegexMatchTimeoutException) { O(c, outp.ToString(), ShFg); Err(c, "awk: a pattern took too long - stopped"); return 2; }
            O(c, outp.ToString(), ShFg); return ex;
        }
    }
}
