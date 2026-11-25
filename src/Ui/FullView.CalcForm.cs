// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== calculators: one value per box (Adeeb, 14 Nov)
    // "200, 60, 12, 2" and "2 4k, 1 call, 1 game" were hard to type. Each calculator now gets a small form: numbers with a
    // unit picker, choices as chips, counts with - and +, a list with + add. The form writes the same text into the
    // (hidden) input boxes the calculator always read - so Copy, Save, Example and the engines don't change.
    // Pure arithmetic, so the result follows as you type (150 ms after the last key) - no Run button.
    sealed partial class FullView
    {
        // one field of a calculator's form
        sealed class CF
        {
            public char Kind;          // n number (+unit), c choice, s stepper, k counts, l list, t text (+presets)
            public int Map;            // which input box it writes into
            public string Label = "", Glue = " ", Cue = "";
            public string[] Units = Array.Empty<string>(), Opts = Array.Empty<string>(), Names = Array.Empty<string>(), Tokens = Array.Empty<string>(), Presets = Array.Empty<string>();
            public string Show = "";   // a unit shown beside the box but not written (the engine wants the bare number)
            public string Text = ""; public int Unit, Sel, Step, Min, Max = 99, Width = 86; public int[] Counts = Array.Empty<int>(); public List<string> Items = new List<string>();
            public CF G(string glue) { Glue = glue; return this; }
            public CF W(int w) { Width = w; return this; }
            public CF Bare(string show) { Show = show; return this; }
            public CF Hint(string cue) { Cue = cue; return this; }
            // what this field adds to its box's text ("" when empty: an optional field left empty)
            public string Piece()
            {
                switch (Kind)
                {
                    case 'n': { string v = Text.Trim(); return v == "" ? "" : v + (Units.Length > 0 ? " " + Units[Math.Min(Unit, Units.Length - 1)] : ""); }
                    case 't': return Text.Trim();
                    case 'c': return Opts.Length == 0 ? "" : Opts[Math.Max(0, Math.Min(Sel, Opts.Length - 1))];
                    case 's': return Step.ToString(CultureInfo.InvariantCulture);
                    case 'k': return string.Join(", ", Counts.Select((n, i) => n > 0 ? n.ToString(CultureInfo.InvariantCulture) + " " + Tokens[i] : null).Where(z => z != null).ToArray());
                    case 'l': return string.Join(", ", Items.Select(z => z.Trim()).Where(z => z != "").ToArray());
                }
                return "";
            }
        }
        sealed class CalcSpec { public string Id; public CF[] F; }
        static CF Num(int map, string label, string def, params string[] units) { return new CF { Kind = 'n', Map = map, Label = label, Text = def, Units = units }; }
        static CF Pick(int map, string label, string def, params string[] opts) { return new CF { Kind = 'c', Map = map, Label = label, Opts = opts, Sel = Math.Max(0, Array.IndexOf(opts, def)) }; }
        static CF Steps(int map, string label, int min, int max, int def) { return new CF { Kind = 's', Map = map, Label = label, Min = min, Max = max, Step = def }; }
        static CF Many(int map, string label, string[] names, string[] tokens, int[] defs) { return new CF { Kind = 'k', Map = map, Label = label, Names = names, Tokens = tokens, Counts = (int[])defs.Clone() }; }
        static CF ListOf(int map, string label, params string[] defs) { var f = new CF { Kind = 'l', Map = map, Label = label }; f.Items.AddRange(defs); return f; }
        static CF Txt(int map, string label, string def, int width, params string[] presets) { return new CF { Kind = 't', Map = map, Label = label, Text = def, Width = width, Presets = presets }; }

        // every calculator's form - built fresh each time it's opened (cheap), so defaults never carry stale state
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "a form is built once each time a calculator opens")]
        static CalcSpec[] CalcSpecs()
        {
            return new[] {
                new CalcSpec { Id = "stack", F = new[] { Txt(0, "Layers, top to bottom", "tcp ipv4 ethernet", 250, "web", "game", "call", "dsl", "vpn", "office vpn"), Num(1, "Link MTU", "1500").Bare("bytes"), Num(2, "Data size (optional)", "").Bare("bytes") } },
                new CalcSpec { Id = "vlsm", F = new[] { Txt(0, "Block", "10.0.0.0/22", 150), ListOf(1, "Hosts each subnet needs", "200", "60", "12", "2") } },
                new CalcSpec { Id = "wildcard", F = new[] { Txt(0, "Mask", "255.255.252.0", 160, "/24", "/22", "/16", "0.0.3.255") } },
                new CalcSpec { Id = "v6split", F = new[] { Txt(0, "Prefix", "2001:db8:abcd::/48", 200), Pick(1, "Split into", "/56", "/52", "/56", "/60", "/64", "4", "16") } },
                new CalcSpec { Id = "binhex", F = new[] { Txt(0, "Number, address or MAC", "200", 190, "0xC8", "0b11001000", "192.168.1.10", "00:1A:2B:3C:4D:5E") } },
                new CalcSpec { Id = "units", F = new[] { Txt(0, "Value", "100 Mbps", 150, "12 MB/s", "1 GiB", "90 min", "1 Gbps") } },
                new CalcSpec { Id = "bdp", F = new[] { Num(0, "Link speed", "1", "Gbps", "Mbps"), Num(1, "Round trip", "80", "ms"), Num(2, "TCP window (optional)", "", "KB", "MB") } },
                new CalcSpec { Id = "bandwidth", F = new[] { Num(0, "Size", "4.7", "GB", "MB", "TB"), Num(1, "Speed", "100", "Mbps", "Gbps", "MB/s"), Num(2, "Time (optional - any two give the third)", "", "min", "s", "h") } },
                new CalcSpec { Id = "cellular", F = new[] { Pick(0, "Technology", "LTE", "LTE", "NR"), Pick(1, "Bandwidth", "20 MHz", "5 MHz", "10 MHz", "15 MHz", "20 MHz", "40 MHz", "60 MHz", "80 MHz", "100 MHz"), Pick(2, "Antennas (MIMO)", "4x4", "1x1", "2x2", "4x4", "8x8"), Pick(2, "Modulation", "256QAM", "QPSK", "16QAM", "64QAM", "256QAM", "1024QAM"), Pick(3, "Subcarrier spacing kHz (5G NR)", "30", "15", "30", "60") } },
                new CalcSpec { Id = "wifirate", F = new[] { Pick(0, "Wi-Fi generation", "6", "4", "5", "6", "7"), Pick(1, "Channel width (MHz)", "80", "20", "40", "80", "160", "320"), Pick(2, "Streams (antennas)", "2", "1", "2", "3", "4") } },
                new CalcSpec { Id = "linkbudget", F = new[] { Num(0, "Transmit power", "20", "dBm", "mW", "W"), Num(1, "Antenna gain - sending", "23").Bare("dBi"), Num(1, "Antenna gain - receiving", "23").Bare("dBi").G(" and "), Num(2, "Distance", "5", "km", "m", "mi"), Num(2, "Frequency", "5.8", "GHz", "MHz").G(" at "), Num(3, "Receiver sensitivity (optional)", "-80", "dBm") } },
                new CalcSpec { Id = "raid", F = new[] { Pick(0, "RAID level", "5", "0", "1", "5", "6", "10"), Steps(1, "Disks", 2, 24, 6), Num(2, "Disk size", "4", "TB", "GB") } },
                new CalcSpec { Id = "backup", F = new[] { Num(0, "Data", "2", "TB", "GB"), Num(1, "Link", "1", "Gbps", "Mbps"), Num(2, "Changes each day (optional)", "5", "%", "GB") } },
                new CalcSpec { Id = "ups", F = new[] { Steps(0, "Batteries", 1, 8, 2), Num(0, "Each battery", "12", "V").G(" x "), Num(0, "Capacity", "9", "Ah"), Num(1, "Load", "300", "W"), Num(2, "Inverter efficiency (optional)", "85", "%") } },
                new CalcSpec { Id = "sla", F = new[] { Txt(0, "Uptime - or a downtime like 4 h a year", "99.9%", 170, "99%", "99.9%", "99.95%", "99.99%", "99.999%") } },
                new CalcSpec { Id = "growth", F = new[] { Num(0, "Storage now", "10", "TB", "GB"), Num(1, "Growth", "3", "% a month", "TB a year", "TB a month"), Num(2, "Capacity (optional)", "40", "TB", "GB") } },
                new CalcSpec { Id = "stream", F = new[] { Pick(0, "Resolution", "1080p60", "720p30", "720p60", "1080p30", "1080p60", "1440p60", "4K30", "4K60"), Pick(1, "Codec", "H264", "H264", "HEVC", "AV1"), Num(2, "Your upload (optional)", "10", "Mbps") } },
                new CalcSpec { Id = "datause", F = new[] { Num(0, "Video a day", "3", "h"), Pick(0, "Quality", "HD", "SD", "HD", "4K"), Num(1, "Video calls a day", "1", "h"), Num(2, "Gaming a day", "2", "h") } },
                new CalcSpec { Id = "plan", F = new[] { Num(0, "Download", "300", "Mbps", "Gbps"), Num(1, "Upload (optional)", "20", "Mbps", "Gbps"), Many(2, "Busy at the same time", new[] { "4K video", "HD video", "Video call", "Online game", "Streaming yourself", "Big download" }, new[] { "4k", "hd", "call", "game", "stream", "download" }, new[] { 2, 0, 1, 1, 0, 0 }) } },
                new CalcSpec { Id = "tdm", F = new[] { Pick(0, "Line", "E1", "E1", "T1"), Steps(1, "Timeslots", 1, 31, 4) } },
                new CalcSpec { Id = "jitterbuf", F = new[] { Num(0, "Shortest delay", "20").Bare("ms"), Num(0, "Longest delay", "70", "ms").G("-"), Pick(1, "Packet time (ms)", "20", "10", "20", "30"), Pick(2, "Codec (optional)", "G711", "G711", "G729", "Opus") } },
            }.Concat(MoneySpecs()).ToArray();
        }
        static readonly string[] Currencies = { "CAD", "USD", "EUR", "GBP", "JPY", "CNY", "INR", "AUD", "CHF", "MXN" };
        static CalcSpec[] MoneySpecs()
        {
            return new[] {
                new CalcSpec { Id = "fxconv", F = new[] { Num(0, "Amount", "1000"), Pick(1, "From", "USD", Currencies), Pick(2, "To", "CAD", Currencies), Num(3, "Bank markup (optional)", "2.5").Bare("%") } },
                new CalcSpec { Id = "pnl", F = new[] { Num(0, "Buy price", "150").Bare("$"), Num(1, "Sell price (optional)", "165").Bare("$"), Num(2, "Shares", "20"), Num(3, "Commission per trade", "9.99").Bare("$"), Num(3, "Currency fee (optional)", "", "% fx").G(", ") } },
                new CalcSpec { Id = "possize", F = new[] { Num(0, "Account", "20000").Bare("$"), Pick(1, "Risk per trade", "1%", "0.5%", "1%", "2%", "3%"), Num(2, "Entry price", "50").Bare("$"), Num(3, "Stop price", "47").Bare("$") } },
                new CalcSpec { Id = "avgcost", F = new[] { ListOf(0, "Prices you bought at", "50", "45", "40"), ListOf(1, "Shares each time", "10", "10", "20") } },
                new CalcSpec { Id = "compound", F = new[] { Num(0, "Start with", "5000").Bare("$"), Num(1, "Add each month", "300").Bare("$"), Num(2, "Growth a year", "6").Bare("%"), Steps(3, "Years", 1, 50, 20) } },
                new CalcSpec { Id = "pctback", F = new[] { Txt(0, "A price falls or rises by", "-30%", 110, "-10%", "-20%", "-50%", "+50%", "+100%") } },
                new CalcSpec { Id = "tradelat", F = new[] { Num(0, "Your ping to the broker", "40", "ms"), Num(1, "Share price", "150").Bare("$"), Num(2, "How much it moves in a day", "2").Bare("%") } },
                new CalcSpec { Id = "comunits", F = new[] { Num(0, "Amount", "1"), Pick(1, "Unit", "barrel", "barrel", "US gallon", "litre", "troy oz", "gram", "kg", "lb", "tonne", "bushel", "MMBtu", "GJ", "therm"), Pick(2, "Crop (bushels)", "wheat", "wheat", "corn", "soybeans") } },
                new CalcSpec { Id = "futures", F = new[] { Pick(0, "Contract", "WTI crude oil", Calc.Futures.Select(z => z[0]).ToArray()), Num(1, "Price", "75").Bare("$ a unit"), Steps(2, "Contracts", 1, 50, 1), Num(3, "Price move (optional)", "2").Bare("%") } },
                new CalcSpec { Id = "perunit", F = new[] { Num(0, "Price in US$", "75"), Pick(1, "Per", "barrel", "barrel", "US gallon", "troy oz", "lb", "tonne", "MMBtu"), Pick(2, "Show per", "litre", "litre", "US gallon", "gram", "kg", "kWh", "GJ"), Pick(3, "In", "CAD", "CAD", "USD") } },
                new CalcSpec { Id = "tickrate", F = new[] { Pick(0, "Server tick rate (Hz)", "64", "20", "30", "60", "64", "128"), Num(1, "Your ping", "40", "ms"), Num(2, "Smoothing (optional)", "2").Bare("ticks") } },
            };
        }
        static CalcSpec SpecOf(string id) { return CalcSpecs().FirstOrDefault(s => s.Id == id); }
        static bool IsCalc(string id) { return CalcSpecs().Any(s => s.Id == id); }

        // the boxes' text from the form: fields that share a box are joined by their glue ("23 and 23", "2 x 12 V 9 Ah")
        static string[] Compose(CalcSpec sp)
        {
            var a = new string[4];
            for (int i = 0; i < 4; i++)
            {
                string s = "";
                foreach (var f in sp.F.Where(z => z.Map == i)) { string p = f.Piece(); if (p == "") continue; s = s == "" ? p : s + f.Glue + p; }
                a[i] = s;
            }
            return a;
        }
        static readonly Regex numRx = new Regex(@"-?[0-9]+(?:[.,][0-9]+)?");
        // the form from the boxes' text (an Example's inputs, coming back to a tool): best effort - what can't be read stays as it was
        static void FillSpec(CalcSpec sp, string[] a)
        {
            for (int i = 0; i < 4; i++)
            {
                string v = i < a.Length ? (a[i] ?? "").Trim() : ""; var fs = sp.F.Where(z => z.Map == i).ToList(); if (fs.Count == 0) continue;
                var nums = numRx.Matches(v).Cast<Match>().Select(m => m.Value).ToList(); int nth = 0;
                foreach (var f in fs)
                {
                    switch (f.Kind)
                    {
                        case 't': if (fs.Count == 1) f.Text = v; break;
                        case 'l': f.Items = v.Split(',').Select(z => z.Trim()).Where(z => z != "").ToList(); break;
                        case 'c': { int best = -1, bl = 0; for (int k = 0; k < f.Opts.Length; k++) if (f.Opts[k].Length > bl && v.IndexOf(f.Opts[k], StringComparison.OrdinalIgnoreCase) >= 0) { best = k; bl = f.Opts[k].Length; } if (best >= 0) f.Sel = best; break; }
                        case 'k': for (int k = 0; k < f.Tokens.Length; k++) { var m = Regex.Match(v, @"(\d{1,3})\s*(?:x\s*)?" + Regex.Escape(f.Tokens[k]) + @"\b", RegexOptions.IgnoreCase); f.Counts[k] = m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0; } break;
                        case 's': { int n; if (nth < nums.Count && int.TryParse(nums[nth], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) f.Step = Math.Max(f.Min, Math.Min(f.Max, n)); nth++; break; }
                        case 'n':
                            if (v == "") { f.Text = ""; break; }
                            if (nth < nums.Count) f.Text = nums[nth].Replace(',', '.'); if (f.Glue == "-") f.Text = f.Text.TrimStart('-');   // "20-70": the dash is a range, not a minus
                            { int bu = -1, bl = 0; for (int u = 0; u < f.Units.Length; u++) if (f.Units[u].Length > bl && Regex.IsMatch(v, @"(?<![A-Za-z])" + Regex.Escape(f.Units[u]) + @"(?![A-Za-z])", RegexOptions.IgnoreCase)) { bu = u; bl = f.Units[u].Length; } if (bu >= 0) f.Unit = bu; }   // bug-hunt ML-175: the whole unit ("5 mi" is miles, not metres)
                            nth++; break;
                    }
                }
            }
        }

        // ---------- the form on screen
        FlowLayoutPanel calcHost; CalcSpec calcNow; bool calcSyncing; Timer calcTimer, animTimer; bool calcTouched; readonly HashSet<string> calcEarned = new HashSet<string>();
        void BuildCalcForm(string id)
        {
            if (calcHost == null)
            {
                calcHost = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, BackColor = Theme.Card, Margin = new Padding(0, 0, Sc(8), 0), MaximumSize = new Size(Sc(1200), 0) };
                tbBar.Controls.Add(calcHost); tbBar.Controls.SetChildIndex(calcHost, 0);
                calcTimer = new Timer { Interval = 150 }; calcTimer.Tick += delegate { calcTimer.Stop(); CalcNow(); };
            }
            calcHost.SuspendLayout();
            foreach (Control c in calcHost.Controls.Cast<Control>().ToList()) { calcHost.Controls.Remove(c); c.Dispose(); }
            calcNow = SpecOf(id); calcTouched = false;
            if (calcNow == null) { calcHost.Visible = false; calcHost.ResumeLayout(); return; }
            calcHost.MaximumSize = new Size(Math.Max(Sc(200), Math.Min(Math.Max(Sc(500), tbBar.ClientSize.Width - Sc(260)), tbBar.ClientSize.Width - Sc(16))), 0);   // bug-hunt L-335: never wider than the bar itself
            foreach (var f in calcNow.F) calcHost.Controls.Add(FieldUi(f));
            calcHost.Visible = true; calcHost.ResumeLayout(true);
            foreach (var rb in tbPool) rb.Visible = false;
            tbRun.Visible = false; tbSweepOk.Visible = false;
            WriteBoxes(); CalcNow();
        }
        // a label over the field's own row
        FlowLayoutPanel FieldUi(CF f)
        {
            var col = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Card, Margin = new Padding(0, 0, Sc(18), Sc(8)) };
            col.Controls.Add(new Label { Text = L.T(f.Label), Font = Draw.fSmall, ForeColor = Theme.Sub, BackColor = Theme.Card, AutoSize = true, Margin = new Padding(Sc(2), 0, 0, Sc(4)), UseMnemonic = false });
            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Card, Margin = new Padding(0) };
            col.Controls.Add(row);
            switch (f.Kind)
            {
                case 'n': case 't':
                    {
                        var rb = new RoundBox(Sc(f.Width), Draw.fInput, f.Cue) { Margin = new Padding(0, 0, Sc(6), 0) }; rb.Box.Text = f.Text;
                        rb.Box.TextChanged += delegate { f.Text = rb.Box.Text; Changed(); };
                        rb.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; calcTimer.Stop(); CalcNow(); } };
                        row.Controls.Add(rb);
                        if (f.Units.Length > 1) AddChips(row, f.Units, () => f.Unit, i => { f.Unit = i; Changed(); });
                        else if (f.Units.Length == 1 || f.Show != "") row.Controls.Add(new Label { Text = f.Units.Length == 1 ? f.Units[0] : f.Show, Font = Draw.fBody, ForeColor = Theme.Muted, BackColor = Theme.Card, AutoSize = true, Margin = new Padding(0, Sc(9), Sc(4), 0) });
                        if (f.Presets.Length > 0) { var pr = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Card, Margin = new Padding(0, Sc(4), 0, 0) }; foreach (var p in f.Presets) { var c = new PillButton(p, PillButton.K.Chip) { Margin = new Padding(0, 0, Sc(4), 0) }; string pv = p; c.Click += delegate { rb.Box.Text = pv; }; pr.Controls.Add(c); } col.Controls.Add(pr); }
                        break;
                    }
                case 'c': AddChips(row, f.Opts, () => f.Sel, i => { f.Sel = i; Changed(); }); break;
                case 's': row.Controls.Add(Stepper(() => f.Step, v => { f.Step = Math.Max(f.Min, Math.Min(f.Max, v)); Changed(); })); break;
                case 'k':
                    for (int i = 0; i < f.Names.Length; i++)
                    {
                        int k = i; var cell = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Card, Margin = new Padding(0, 0, Sc(14), 0) };
                        cell.Controls.Add(new Label { Text = L.T(f.Names[i]), Font = Draw.fBody, ForeColor = Theme.Ink, BackColor = Theme.Card, AutoSize = true, Margin = new Padding(0, Sc(7), Sc(4), 0), UseMnemonic = false });
                        cell.Controls.Add(Stepper(() => f.Counts[k], v => { f.Counts[k] = Math.Max(0, Math.Min(20, v)); Changed(); }));
                        row.Controls.Add(cell);
                    }
                    row.WrapContents = true; row.MaximumSize = new Size(Sc(760), 0); break;
                case 'l': ListUi(row, f); break;
            }
            return col;
        }
        // chips that act as one choice
        static void AddChips(FlowLayoutPanel row, string[] opts, Func<int> cur, Action<int> set)
        {
            var chips = new List<PillButton>();
            for (int i = 0; i < opts.Length; i++)
            {
                var c = new PillButton(opts[i], PillButton.K.Chip) { Margin = new Padding(0, Sc(3), Sc(4), 0), Selected = i == cur() }; int k = i;
                c.Click += delegate { set(k); foreach (var o in chips) o.Selected = o == c; };
                chips.Add(c); row.Controls.Add(c);
            }
        }
        // - 4 +
        static FlowLayoutPanel Stepper(Func<int> get, Action<int> set)
        {
            var p = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Card, Margin = new Padding(0, Sc(3), 0, 0) };
            var minus = new PillButton("–", PillButton.K.Chip) { Margin = new Padding(0) }; var plus = new PillButton("+", PillButton.K.Chip) { Margin = new Padding(0) };
            var val = new Label { Text = get().ToString(CultureInfo.CurrentCulture), Font = Draw.fBodyB, ForeColor = Theme.Ink, BackColor = Theme.Card, AutoSize = false, Size = new Size(Sc(34), Sc(30)), TextAlign = ContentAlignment.MiddleCenter };
            minus.Click += delegate { set(get() - 1); val.Text = get().ToString(CultureInfo.CurrentCulture); };
            plus.Click += delegate { set(get() + 1); val.Text = get().ToString(CultureInfo.CurrentCulture); };
            p.Controls.Add(minus); p.Controls.Add(val); p.Controls.Add(plus);
            return p;
        }
        // a few small boxes, + add and - remove
        void ListUi(FlowLayoutPanel row, CF f)
        {
            row.SuspendLayout();
            foreach (Control c in row.Controls.Cast<Control>().ToList()) { row.Controls.Remove(c); c.Dispose(); }
            for (int i = 0; i < f.Items.Count; i++)
            {
                int k = i; var rb = new RoundBox(Sc(64), Draw.fInput, "") { Margin = new Padding(0, 0, Sc(6), 0) }; rb.Box.Text = f.Items[i];
                rb.Box.TextChanged += delegate { if (k < f.Items.Count) { f.Items[k] = rb.Box.Text; Changed(); } };
                row.Controls.Add(rb);
            }
            if (f.Items.Count < 12) { var add = new PillButton("+ " + L.T("add"), PillButton.K.Chip) { Margin = new Padding(Sc(2), Sc(3), Sc(4), 0) }; add.Click += delegate { f.Items.Add(""); ListUi(row, f); Changed(); var last = row.Controls.OfType<RoundBox>().LastOrDefault(); if (last != null) last.Box.Focus(); }; row.Controls.Add(add); }
            if (f.Items.Count > 1) { var rem = new PillButton("– " + L.T("last"), PillButton.K.Chip) { Margin = new Padding(0, Sc(3), 0, 0) }; rem.Click += delegate { f.Items.RemoveAt(f.Items.Count - 1); ListUi(row, f); Changed(); }; row.Controls.Add(rem); }
            row.ResumeLayout(true);
        }
        void Changed() { if (calcSyncing) return; calcTouched = true; WriteBoxes(); calcTimer.Stop(); calcTimer.Start(); }
        // the form's text into the hidden boxes the calculator reads
        void WriteBoxes()
        {
            if (calcNow == null) return; var a = Compose(calcNow); calcSyncing = true;
            try { for (int i = 0; i < tbPool.Length; i++) if (i < tbBoxes.Count && tbBoxes[i] != null && tbBoxes[i].Box.Text != a[i]) tbBoxes[i].Box.Text = a[i]; }
            finally { calcSyncing = false; }
        }
        // the form from the boxes (Example's "Fill the boxes", coming back to a tool)
        void ReadBoxes()
        {
            if (calcNow == null) return; FillSpec(calcNow, Inputs());
            string id = calcNow.Id; calcSyncing = true;
            try { BuildCalcFormKeep(id); } finally { calcSyncing = false; }
        }
        void BuildCalcFormKeep(string id)
        {
            var keep = calcNow; calcHost.SuspendLayout();
            foreach (Control c in calcHost.Controls.Cast<Control>().ToList()) { calcHost.Controls.Remove(c); c.Dispose(); }
            foreach (var f in keep.F) calcHost.Controls.Add(FieldUi(f));
            calcHost.ResumeLayout(true); calcNow = keep;
        }
        // the result, right away: arithmetic only - nothing is sent
        void CalcNow()
        {
            if (calcNow == null || tbSel != calcNow.Id || tbBusy) return;
            var t = Tools2().FirstOrDefault(x => x.Id == tbSel); if (t == null) return;
            var a = Inputs(); List<string[]> res;
            if (a.All(z => z == "")) { tbRows = null; tbCanvas.Invalidate(); return; }
            try { res = t.Do(a, new Toolkit.Run()) ?? new List<string[]>(); }
            catch (Exception e) { Program.Log("calc " + t.Id + ": " + e.Message); res = new List<string[]> { new[] { "2", "Couldn't work it out", Draw.Trunc(e.Message, 100), "", "u" } }; }
            tbRows = res; tbAt = DateTime.Now; tbShown = a; tbRowsKey = string.Join("|", a); tbCompare = false;
            if (calcTouched && !calcEarned.Contains(t.Id) && !res.Any(o => o.Length > 0 && o[0] == "2")) { calcEarned.Add(t.Id); Earn(t.Id, true); }
            tbLast[t.Id] = new PrevRun { Inputs = tbRowsKey, At = tbAt, Rows = res, Shown = a };
            InvalidateBelowPicture();
        }
        // the picture and what's under it - the title, the boxes and the rest of the page aren't repainted
        void InvalidateBelowPicture()
        {
            if (visRect.Height <= 0) { tbCanvas.Invalidate(); return; }
            int top = (int)visRect.Y + tbCanvas.AutoScrollPosition.Y - 2;
            tbCanvas.Invalidate(new Rectangle(0, Math.Max(0, top), tbCanvas.ClientSize.Width, Math.Max(1, tbCanvas.ClientSize.Height - Math.Max(0, top))));
        }

        // ---------- motion: only the picture's rectangle, only while you can see it
        RectangleF visRect;   // where the picture was drawn (page coordinates)
        // Adeeb, 22 Nov: "play once, then pause at least 45 s - it's distracting". The pictures' clock runs for AnimPlay seconds
        // after a tool opens, then holds still for AnimHold, then plays again. While it holds, nothing repaints.
        const double AnimPlay = 8, AnimHold = 50;
        static int animStart = Environment.TickCount;
        static double AnimSince { get { return (double)unchecked(Environment.TickCount - animStart) / 1000.0; } }
        static double AnimT { get { double e = Math.Max(0, AnimSince), p = e % (AnimPlay + AnimHold); return Math.Floor(e / (AnimPlay + AnimHold)) * AnimPlay + Math.Min(p, AnimPlay); } }
        static bool AnimHolding { get { return Math.Max(0, AnimSince) % (AnimPlay + AnimHold) > AnimPlay + 0.3; } }   // the last frame drawn, then still
        static void AnimRestart() { animStart = Environment.TickCount; }
        static readonly HashSet<string> Animated = new HashSet<string> { "cellular", "wifirate", "tdm", "plan", "bandwidth", "backup", "bdp", "jitterbuf", "ups", "tradelat", "tickrate" };
        static bool WindowsAnimates() { try { return Motion.On; } catch (Exception e) { if (e is OutOfMemoryException) throw; return true; } }
        void AnimTick()
        {
            bool on = tab == "toolbox" && Visible && WindowState != FormWindowState.Minimized && Form.ActiveForm == this && (Animated.Contains(tbSel) || Animates3(tbSel)) && !tbHome && !tbBusy && visRect.Height > 0 && WindowsAnimates();
            if (!on) { animTimer.Stop(); return; }   /* review 1.1.6 U1 #13: no 20 ticks a second on pages with nothing moving - EnsureAnim starts it again */
            if (AnimHolding) return;
            var r = new Rectangle((int)visRect.X + tbCanvas.AutoScrollPosition.X, (int)visRect.Y + tbCanvas.AutoScrollPosition.Y, (int)visRect.Width + 1, (int)visRect.Height + 1);
            if (r.Bottom < 0 || r.Top > tbCanvas.ClientSize.Height) return;   // scrolled away
            tbCanvas.Invalidate(r);
        }
        void EnsureAnim()
        {
            if (animTimer != null) { if (!animTimer.Enabled) animTimer.Start(); return; }
            Activated += delegate { if (animTimer != null && tab == "toolbox") animTimer.Start(); };
            animTimer = new Timer { Interval = 50 }; animTimer.Tick += delegate { try { AnimTick(); } catch (Exception e) { Program.Log("anim: " + e.Message); animTimer.Stop(); } }; animTimer.Start();
        }
    }
}
