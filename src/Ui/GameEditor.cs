// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // ================================================================== Edit games & servers
    // One game at a time: its name, the program that means it's running, and up to 12 servers (name, location, host name or IP,
    // how to measure). Everything is validated here and again when the file is read; nothing typed can become a command.
    sealed class GameEditor : Form
    {
        sealed class Line { public RoundBox Name, Where, Host, Port; public ComboBox How; public Label Result; public PillButton Remove; public Panel Row; }
        readonly Game g; readonly bool isNew;
        readonly RoundBox name, exe; readonly FlowLayoutPanel list; readonly Label err; readonly List<Line> lines = new List<Line>();
        public bool Saved;
        static readonly string[] HowNames = { "Auto (ping, then TCP)", "Ping (ICMP) only", "TCP port" };
        static int Sc(float v) { return (int)(v * Draw.S); }

        public GameEditor(Game game, bool asNew)
        {
            g = game; isNew = asNew;
            Text = asNew ? "Add a game" : "Edit " + game.Name; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Card; ForeColor = Theme.Ink; Font = Draw.fBody; KeyPreview = true;
            ClientSize = new Size(Sc(980), Sc(640)); Padding = new Padding(Sc(18));

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Theme.Card, WrapContents = true, Padding = new Padding(0, 0, 0, Sc(8)) };
            name = new RoundBox(Sc(300), Draw.fInput, "e.g. My game") { Label = "Game name" }; name.Box.Text = game.Name ?? ""; name.Box.MaxLength = 40;
            exe = new RoundBox(Sc(380), Draw.fInput, "optional - e.g. VALORANT-Win64-Shipping") { Label = "Program that means it's running" }; exe.Box.Text = string.Join("; ", game.Exes); exe.Box.MaxLength = 200;
            var pick = new PillButton("Pick from running programs...", PillButton.K.Chip) { Margin = new Padding(0, Sc(22), 0, 0) };
            pick.Click += delegate { PickRunning(pick); };
            top.Controls.Add(name); top.Controls.Add(exe); top.Controls.Add(pick);

            var head = new Label { Dock = DockStyle.Top, Height = Sc(26), Text = "SERVERS  ·  name  ·  location  ·  host name or IP  ·  how to measure", Font = Draw.fTiny, ForeColor = Theme.Muted, BackColor = Theme.Card, TextAlign = ContentAlignment.BottomLeft };
            list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.Card };
            err = new Label { Dock = DockStyle.Bottom, Height = Sc(40), ForeColor = Theme.LvText(2), BackColor = Theme.Card, Font = Draw.fSmall, Text = "" };

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, BackColor = Theme.Card, WrapContents = true, Padding = new Padding(0, Sc(8), 0, 0) };
            var add = new PillButton("+ Add server", PillButton.K.Chip); add.Click += delegate { if (lines.Count < 12) AddLine(new GameServer { Name = "", Where = "", Host = "", Port = 0, Kind = "exact" }); };
            var test = new PillButton("Test these now", PillButton.K.Chip); test.Click += delegate { TestAll(); };
            var imp = new PillButton("Import...", PillButton.K.Chip); imp.Click += delegate { Import(); };
            var expo = new PillButton("Export...", PillButton.K.Chip); expo.Click += delegate { Export(); };
            var reset = new PillButton("Reset to defaults", PillButton.K.Chip) { Visible = !g.Custom && !asNew }; reset.Click += delegate { Games.Reset(g.Id); Saved = true; Close(); };
            var cancel = new PillButton("Cancel", PillButton.K.Chip); cancel.Click += delegate { Close(); };
            var save = new PillButton("Save", PillButton.K.Primary); save.Click += delegate { Save(); };
            foreach (var b in new Control[] { add, test, imp, expo, reset, cancel, save }) { b.Margin = new Padding(0, 0, Sc(8), 0); bottom.Controls.Add(b); }
            var note = new Label { Dock = DockStyle.Bottom, Height = Sc(36), Font = Draw.fSmall, ForeColor = Theme.Muted, BackColor = Theme.Card, Text = "Auto tries a ping first and times a TCP connection when the server ignores ping. Host names are looked up fresh each time, so they follow a game's IP changes. At most 12 servers per game; " + GamePing.MaxServers + " are watched at once." };

            Controls.Add(list); Controls.Add(head); Controls.Add(top); Controls.Add(err); Controls.Add(note); Controls.Add(bottom);
            foreach (var s in game.Servers.Take(12)) AddLine(s);
            if (lines.Count == 0) AddLine(new GameServer { Name = "", Where = "", Host = "", Port = 0, Kind = "exact" });
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
        }

        void AddLine(GameServer s)
        {
            var row = new Panel { Width = Sc(930), Height = Sc(44), BackColor = Theme.Card, Margin = new Padding(0, 0, 0, Sc(4)) };
            var l = new Line { Row = row };
            l.Name = new RoundBox(Sc(150), Draw.fBody, "name") { Location = new Point(0, Sc(4)) }; l.Name.Box.Text = s.Name ?? ""; l.Name.Box.MaxLength = 30;
            l.Where = new RoundBox(Sc(150), Draw.fBody, "location") { Location = new Point(Sc(158), Sc(4)) }; l.Where.Box.Text = s.Where ?? ""; l.Where.Box.MaxLength = 40;
            l.Host = new RoundBox(Sc(260), Draw.fMonoS, "host name or IP") { Location = new Point(Sc(316), Sc(4)) }; l.Host.Box.Text = s.Host ?? ""; l.Host.Box.MaxLength = 253;
            l.How = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Draw.fSmall, Width = Sc(160), Location = new Point(Sc(584), Sc(10)), FlatStyle = FlatStyle.Flat, BackColor = Theme.Card, ForeColor = Theme.Ink };
            l.How.Items.AddRange(HowNames); l.How.SelectedIndex = s.Port > 0 ? 2 : s.Port < 0 ? 1 : 0;
            l.Port = new RoundBox(Sc(70), Draw.fBody, "port") { Location = new Point(Sc(752), Sc(4)), Visible = s.Port > 0 }; l.Port.Box.Text = s.Port > 0 ? s.Port.ToString(CultureInfo.InvariantCulture) : ""; l.Port.Box.MaxLength = 5;
            l.How.SelectedIndexChanged += delegate { l.Port.Visible = l.How.SelectedIndex == 2; if (l.Port.Visible && l.Port.Box.Text == "") l.Port.Box.Text = "443"; };
            l.Result = new Label { Location = new Point(Sc(828), Sc(14)), AutoSize = true, Font = Draw.fSmall, ForeColor = Theme.Muted, BackColor = Theme.Card };
            l.Remove = new PillButton("✕", PillButton.K.Chip) { Location = new Point(Sc(884), Sc(6)), AccessibleName = "Remove this server" };
            l.Remove.Click += delegate { lines.Remove(l); list.Controls.Remove(row); row.Dispose(); };
            l.Host.Tag = s.Kind;
            row.Controls.AddRange(new Control[] { l.Name, l.Where, l.Host, l.How, l.Port, l.Result, l.Remove });
            lines.Add(l); list.Controls.Add(row);
        }

        // what the form holds, as servers; null + a message when something isn't valid
        List<GameServer> Read(out string problem)
        {
            problem = null; var r = new List<GameServer>(); int n = 0;
            foreach (var l in lines)
            {
                n++; string host = l.Host.Box.Text.Trim(), nm = l.Name.Box.Text.Trim();
                if (host == "" && nm == "") continue;
                if (!Games.ValidHost(host)) { problem = "Line " + n + ": \"" + (host.Length > 40 ? host.Substring(0, 40) + "..." : host) + "\" isn't a host name or IP - letters, digits, dots and dashes only."; return null; }
                int port = l.How.SelectedIndex == 1 ? -1 : 0;
                if (l.How.SelectedIndex == 2 && (!int.TryParse(l.Port.Box.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)) { problem = "Line " + n + ": the TCP port must be 1 - 65535."; return null; }
                string kind = l.Host.Tag as string; if (kind != "valve" && kind != "estimate") kind = "exact";
                r.Add(new GameServer { Name = nm == "" ? host : nm, Where = l.Where.Box.Text.Trim(), Host = host, Port = port, Kind = kind });
            }
            if (r.Select(z => z.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != r.Count) { problem = "Two servers have the same name - give each one its own."; return null; }
            return r;
        }
        void Save()
        {
            string problem; var servers = Read(out problem);
            string nm = name.Box.Text.Trim();
            if (problem == null && nm == "") problem = "Give the game a name.";
            var exes = exe.Box.Text.Split(';', ',').Select(ExeName).Where(z => z.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
            if (problem != null) { err.Text = problem; return; }
            if (isNew)
            {
                if (Games.All.Any(x => string.Equals(x.Name, nm, StringComparison.OrdinalIgnoreCase))) { err.Text = "You already have a game called " + nm + " - pick another name, or edit that one."; return; }
                string id = new string(nm.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()); if (id.Length == 0) id = "game"; if (id.Length > 30) id = id.Substring(0, 30);
                // two names that reduce to the same id ("CS 2" and "CS-2") get their own: my-cs2, my-cs2-2 ... never one replacing the other
                string baseId = "my-" + id, tryId = baseId; for (int n = 2; Games.All.Any(x => x.Id == tryId); n++) tryId = baseId + "-" + n;
                g.Id = tryId; g.Custom = true;
            }
            g.Name = nm; g.Exes = exes; g.Servers = servers;
            Games.SaveGame(g); Saved = true; Close();
        }
        // what's typed or pasted as the program: "C:\Games\VALORANT.exe" (quotes and all) -> VALORANT; nothing that can throw
        static string ExeName(string z)
        {
            z = (z ?? "").Trim().Trim('"', '\'', ' ');
            int cut = Math.Max(z.LastIndexOf('\\'), z.LastIndexOf('/')); if (cut >= 0) z = z.Substring(cut + 1);
            var bad = Path.GetInvalidFileNameChars(); z = new string(z.Where(c => !bad.Contains(c)).ToArray()).Trim();
            if (z.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) z = z.Substring(0, z.Length - 4);
            return z.Length > 60 ? z.Substring(0, 60) : z;
        }
        // one probe per line, off the window's thread; results shown beside each line
        void TestAll()
        {
            string problem; var servers = Read(out problem); if (problem != null) { err.Text = problem; return; }
            err.Text = "";
            var targets = lines.Where(l => l.Host.Box.Text.Trim() != "").ToList();
            foreach (var l in targets) l.Result.Text = "...";
            for (int i = 0; i < targets.Count && i < servers.Count; i++)
            {
                var l = targets[i]; var s = servers[i];
                Task.Factory.StartNew(() =>
                {
                    string used; double v = GamePing.Probe(s, out used);
                    string t = double.IsNaN(v) ? "no reply" : Math.Round(v) + " ms";
                    try { BeginInvoke((Action)delegate { if (!l.Result.IsDisposed) l.Result.Text = t; }); } catch (InvalidOperationException) { }
                });
            }
        }
        void PickRunning(Control at)
        {
            var menu = new ContextMenuStrip();
            try
            {
                // read the name while the process object is still open, then dispose it (one step - reading after Dispose throws)
                var names = Process.GetProcesses().Select(p => { try { using (p) return p.MainWindowHandle != IntPtr.Zero ? p.ProcessName : ""; } catch (InvalidOperationException) { return ""; } catch (System.ComponentModel.Win32Exception) { return ""; } })
                    .Where(z => z != "" && !string.Equals(z, "AdeeboMeter", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(z => z, StringComparer.OrdinalIgnoreCase).Take(40).ToList();
                foreach (var n in names) { string pn = n; menu.Items.Add(pn, null, delegate { exe.Box.Text = pn; }); }
                if (names.Count == 0) menu.Items.Add("(no programs with a window)").Enabled = false;
            }
            catch (Exception e) { if (!(e is InvalidOperationException || e is System.ComponentModel.Win32Exception)) throw; }
            menu.Closed += delegate { try { if (!IsDisposed) BeginInvoke((Action)delegate { menu.Dispose(); }); else menu.Dispose(); } catch (InvalidOperationException) { menu.Dispose(); } };
            menu.Show(at, new Point(0, at.Height));
        }
        // the same line format as games.txt, for this one game - a list a friend can import
        void Export()
        {
            string problem; var servers = Read(out problem); if (problem != null) { err.Text = problem; return; }
            using (var d = new SaveFileDialog { FileName = "AdeeboMeter-" + new string((name.Box.Text.Trim() == "" ? "game" : name.Box.Text.Trim()).Where(char.IsLetterOrDigit).ToArray()) + ".txt", Filter = "AdeeboMeter game list|*.txt", OverwritePrompt = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var l = new List<string> { "# AdeeboMeter game list - Gamer Zone > Edit > Import", "game|x|" + name.Box.Text.Trim().Replace("|", " ") + "|" + exe.Box.Text.Replace("|", " ").Replace(",", ";") };
                l.AddRange(servers.Select(s => "server|x|" + s.Name.Replace("|", " ") + "|" + (s.Where ?? "").Replace("|", " ") + "|" + s.Host + "|" + s.Port.ToString(CultureInfo.InvariantCulture) + "|" + s.Kind));
                try { File.WriteAllLines(d.FileName, l); err.Text = ""; } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; err.Text = "Couldn't save: " + e.Message; }
            }
        }
        void Import()
        {
            using (var d = new OpenFileDialog { Filter = "AdeeboMeter game list|*.txt|All files|*.*" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (new FileInfo(d.FileName).Length > 64 * 1024) { err.Text = "That file is too big for a game list."; return; }
                    var got = new List<GameServer>(); int skipped = 0;
                    foreach (var raw in File.ReadLines(d.FileName).Take(200))
                    {
                        var f = raw.Split('|');
                        if (f[0] == "game" && f.Length >= 4 && name.Box.Text.Trim() == "") name.Box.Text = f[2].Trim();
                        if (f[0] != "server" || f.Length < 7) continue;
                        int port; string host = f[4].Trim();
                        if (!Games.ValidHost(host) || !int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port < -1 || port > 65535) { skipped++; continue; }
                        got.Add(new GameServer { Name = f[2].Trim(), Where = f[3].Trim(), Host = host, Port = port, Kind = f[6].Trim() == "valve" ? "valve" : f[6].Trim() == "estimate" ? "estimate" : "exact" });
                    }
                    if (got.Count == 0) { err.Text = "No valid servers in that file."; return; }
                    foreach (var l in lines.ToList()) { list.Controls.Remove(l.Row); l.Row.Dispose(); }
                    lines.Clear(); foreach (var s in got.Take(12)) AddLine(s);
                    err.Text = skipped > 0 ? skipped + " line(s) skipped - not a valid host name, IP or port." : "";
                }
                catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; err.Text = "Couldn't read it: " + e.Message; }
            }
        }
    }
}
