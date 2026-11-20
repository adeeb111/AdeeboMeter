// Network Lab Suite: opt-in snapshots, read-only app X-Ray, offline sandbox and witness worksheet.
// Native WinForms only; no background probes, privileged access or listeners.
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
    sealed partial class FullView
    {
        readonly List<string[]> labSnapshots = new List<string[]>();
        readonly string[] labScenarios = { "Normal", "DNS timeout", "Packet loss", "Wrong gateway", "MTU black hole", "Routing loop" };
        string labScenario = "Normal";
        int labStep;
        string labPhoneNote = "";
        string LabPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdeeboMeter", "network-lab.txt"); } }

        void OpenNetworkLab()
        {
            // Standalone on-demand window: controls and buffers are disposed when closed.
            using (var f = new Form { Text = "AdeeboMeter | Network Lab", Size = new Size(Sc(900), Sc(640)),
                MinimumSize = new Size(Sc(620), Sc(440)), StartPosition = FormStartPosition.CenterParent,
                BackColor = Theme.Card, ForeColor = Theme.Ink, Font = Draw.fBody })
            {
                var tabs0 = new TabControl { Dock = DockStyle.Fill };
                tabs0.TabPages.Add(LabTimePage());
                tabs0.TabPages.Add(LabXrayPage());
                tabs0.TabPages.Add(LabSandboxPage());
                tabs0.TabPages.Add(LabWitnessPage());
                f.Controls.Add(tabs0);
                f.ShowDialog(this);
            }
        }
        TabPage LabTab(string title, out FlowLayoutPanel panel)
        {
            var page = new TabPage(title) { BackColor = Theme.Card, ForeColor = Theme.Ink };
            panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(Sc(14)), BackColor = Theme.Card };
            page.Controls.Add(panel); return page;
        }
        Label LabLabel(string value, bool heading)
        {
            return new Label { Text = value, AutoSize = true, MaximumSize = new Size(Sc(750), 0),
                Font = heading ? Draw.fBodyB : Draw.fBody, ForeColor = Theme.Ink, Margin = new Padding(0, Sc(6), 0, Sc(8)) };
        }
        Button LabButton(string title, Action click)
        {
            var b = new Button { Text = title, AutoSize = true, Margin = new Padding(0, Sc(5), 0, Sc(6)) };
            b.Click += delegate { click(); }; return b;
        }
        string[] LabSnapshot()
        {
            string gw, local, adapter, ssid, kind; int signal;
            lock (M.L) { gw = M.Gateway; local = M.LocalIp; adapter = M.Primary == null ? "" : M.Primary.Name;
                ssid = M.Ssid; kind = M.AdapterKind; signal = M.WifiSignal; }
            return new[] { DateTime.Now.ToString("o", CultureInfo.InvariantCulture), local ?? "", gw ?? "",
                adapter ?? "", kind ?? "", ssid ?? "", signal.ToString(CultureInfo.InvariantCulture),
                LabNumber(M.GwPing.Avg(6)), LabNumber(M.NetPing.Avg(6)) };
        }
        static string LabNumber(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "" : v.ToString("0.##", CultureInfo.InvariantCulture); }
        static string LabEncode(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? "")); }
        static string LabDecode(string value) { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        void LabLoad()
        {
            labSnapshots.Clear(); labScenario = "Normal"; labStep = 0;
            if (!File.Exists(LabPath)) return;
            try
            {
                var fi = new FileInfo(LabPath); if (fi.Length > 512 * 1024) return;
                foreach (var line in File.ReadAllLines(LabPath))
                {
                    if (line.StartsWith("S|", StringComparison.Ordinal))
                    {
                        var parts = line.Substring(2).Split('|');
                        if (parts.Length != 9 || labSnapshots.Count >= 64) continue;
                        labSnapshots.Add(parts.Select(LabDecode).ToArray());
                    }
                    else if (line.StartsWith("L|", StringComparison.Ordinal))
                    {
                        var parts = line.Split('|'); int n;
                        if (parts.Length == 3 && labScenarios.Contains(parts[1]) && int.TryParse(parts[2], out n))
                        { labScenario = parts[1]; labStep = Math.Max(0, Math.Min(12, n)); }
                    }
                }
            }
            catch (Exception e) { Program.Log("network lab load: " + e.Message); }
        }
        void LabSave()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LabPath));
                var lines = labSnapshots.Take(64).Select(x => "S|" + string.Join("|", x.Select(LabEncode))).ToList();
                lines.Add("L|" + labScenario + "|" + labStep.ToString(CultureInfo.InvariantCulture));
                string tmp = LabPath + ".part";
                File.WriteAllLines(tmp, lines.ToArray(), Encoding.UTF8);
                if (File.Exists(LabPath)) File.Replace(tmp, LabPath, null);
                else File.Move(tmp, LabPath);
            }
            catch (Exception e) { Program.Log("network lab save: " + e.Message); MessageBox.Show(this, "Could not save the local lab file.", "Network Lab"); }
        }
        TabPage LabTimePage()
        {
            FlowLayoutPanel p; var page = LabTab("Time Machine", out p);
            LabLoad();
            p.Controls.Add(LabLabel("Manual network snapshots: compare what was observed, not packet history. Stored locally (maximum 64).", true));
            var output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Width = Sc(730), Height = Sc(340), Font = new Font("Consolas", 10) };
            Action refresh = delegate
            {
                var sb = new StringBuilder();
                string[] fields = { "Time", "Local IP", "Gateway", "Adapter", "Type", "Wi-Fi SSID", "Signal", "Gateway latency", "Internet latency" };
                var a = labSnapshots.Count > 1 ? labSnapshots[labSnapshots.Count - 2] : null;
                var b = labSnapshots.Count > 0 ? labSnapshots[labSnapshots.Count - 1] : null;
                if (b == null) sb.AppendLine("No snapshot yet. Capture one when you want a baseline.");
                else for (int i = 0; i < fields.Length; i++)
                    sb.AppendLine((a != null && a[i] != b[i] ? "CHANGED " : "        ") + fields[i] + ": " + b[i] +
                        (a != null && a[i] != b[i] ? "  [previous: " + a[i] + "]" : ""));
                sb.AppendLine(); sb.AppendLine("Snapshots retained: " + labSnapshots.Count);
                output.Text = sb.ToString();
            };
            p.Controls.Add(LabButton("Capture snapshot now", delegate
            {
                labSnapshots.Add(LabSnapshot()); if (labSnapshots.Count > 64) labSnapshots.RemoveAt(0);
                LabSave(); refresh();
            }));
            p.Controls.Add(LabButton("Clear saved snapshots", delegate
            {
                if (MessageBox.Show(this, "Delete local snapshots?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                labSnapshots.Clear(); LabSave(); refresh();
            }));
            p.Controls.Add(output); refresh(); return page;
        }
        TabPage LabXrayPage()
        {
            FlowLayoutPanel p; var page = LabTab("Network X-Ray", out p);
            p.Controls.Add(LabLabel("Read-only view of observed TCP connections. No interception, injection or new probes.", true));
            var apps = connSnap.Where(c => !string.IsNullOrEmpty(c.Proc)).Select(c => c.Proc)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).Take(300).ToArray();
            var picker = new ComboBox { Width = Sc(340), DropDownStyle = ComboBoxStyle.DropDownList };
            picker.Items.AddRange(apps.Cast<object>().ToArray()); if (picker.Items.Count > 0) picker.SelectedIndex = 0;
            var result = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
                WordWrap = false, Width = Sc(730), Height = Sc(340), Font = new Font("Consolas", 10) };
            Action refresh = delegate
            {
                string app = picker.SelectedItem as string;
                if (app == null) { result.Text = "No application connections observed in the current snapshot."; return; }
                var group = connSnap.Where(c => string.Equals(c.Proc, app, StringComparison.OrdinalIgnoreCase)).ToList();
                var sb = new StringBuilder(); sb.AppendLine("APPLICATION: " + app);
                sb.AppendLine("Observed connections: " + group.Count + "  |  not a full dependency inventory");
                foreach (var c in group.Take(120))
                    sb.AppendLine(c.Proto + "  " + c.State + "  " + (c.Host != "" ? c.Host : c.RemoteIp) + ":" + c.RemotePort);
                if (group.Count > 120) sb.AppendLine("Additional connections omitted.");
                sb.AppendLine(); sb.AppendLine("DNS, routing and TLS dependencies are not inferred from this connection list.");
                result.Text = sb.ToString();
            };
            picker.SelectedIndexChanged += delegate { refresh(); };
            p.Controls.Add(picker);
            p.Controls.Add(LabButton("Open selected app in Network Story", delegate
            {
                string app = picker.SelectedItem as string; if (app == null) return;
                StoryPick(app); ShowPage("travel");
            }));
            p.Controls.Add(result); refresh(); return page;
        }
        TabPage LabSandboxPage()
        {
            FlowLayoutPanel p; var page = LabTab("Offline Sandbox", out p);
            p.Controls.Add(LabLabel("Deterministic simulation only. All packets, devices and faults here are fictional; nothing is sent to the network.", true));
            var scenario = new ComboBox { Width = Sc(280), DropDownStyle = ComboBoxStyle.DropDownList };
            scenario.Items.AddRange(labScenarios.Cast<object>().ToArray());
            scenario.SelectedItem = labScenario;
            p.Controls.Add(scenario);
            var scene = new Panel { Width = Sc(730), Height = Sc(210), BackColor = Theme.Card };
            var trace = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Width = Sc(730), Height = Sc(140), Font = new Font("Consolas", 10) };
            Action redraw = delegate
            {
                trace.Text = LabSandboxTrace(labScenario, labStep); scene.Invalidate();
            };
            scene.Paint += delegate(object sender, PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                string[] nodes = { "PC", "Switch", "Router", "DNS", "Server" };
                int width = Math.Max(1, scene.ClientSize.Width - Sc(110));
                for (int i = 0; i < nodes.Length; i++)
                {
                    int x = Sc(10) + i * width / (nodes.Length - 1), y = Sc(70);
                    if (i > 0) using (var pen = new Pen(Theme.Sub, 2))
                        g.DrawLine(pen, Sc(40) + (i - 1) * width / (nodes.Length - 1), y + Sc(22), x, y + Sc(22));
                    var box = new Rectangle(x, y, Sc(76), Sc(46));
                    using (var fill = new SolidBrush(i == Math.Min(4, labStep % 5) ? Theme.BlueSoft : Theme.Soft))
                        g.FillRectangle(fill, box);
                    using (var ink = new SolidBrush(Theme.Ink)) g.DrawString(nodes[i], Draw.fBodyB, ink, x + Sc(6), y + Sc(13));
                }
                using (var ink = new SolidBrush(Theme.Sub))
                    g.DrawString("Simulation step " + labStep + " / 12   |   " + labScenario, Draw.fBody, ink, Sc(10), Sc(150));
            };
            scenario.SelectedIndexChanged += delegate { labScenario = scenario.SelectedItem as string ?? "Normal"; labStep = 0; LabSave(); redraw(); };
            var actions = new FlowLayoutPanel { Width = Sc(730), Height = Sc(50), WrapContents = false };
            actions.Controls.Add(LabButton("Previous", delegate { labStep = Math.Max(0, labStep - 1); LabSave(); redraw(); }));
            actions.Controls.Add(LabButton("Next packet step", delegate { labStep = Math.Min(12, labStep + 1); LabSave(); redraw(); }));
            actions.Controls.Add(LabButton("Restart", delegate { labStep = 0; LabSave(); redraw(); }));
            p.Controls.Add(scene); p.Controls.Add(actions); p.Controls.Add(trace); redraw(); return page;
        }
        static string LabSandboxTrace(string scenario, int step)
        {
            var sb = new StringBuilder(); sb.AppendLine("Offline packet story | " + scenario);
            string[] normal = { "PC resolves destination name", "DNS query leaves PC", "Switch forwards frame",
                "Router forwards request", "DNS server responds", "PC opens TCP connection",
                "TCP SYN traverses switch", "Router forwards SYN", "Server replies SYN-ACK",
                "PC sends ACK", "Application sends request", "Server sends response", "Complete" };
            for (int i = 0; i <= Math.Min(step, 12); i++)
            {
                string note = normal[i];
                if (scenario == "DNS timeout" && i >= 4) note = "SIMULATED DNS TIMEOUT - later steps blocked";
                else if (scenario == "Packet loss" && i == 8) note = "SIMULATED DROP - TCP retransmission required";
                else if (scenario == "Wrong gateway" && i >= 3) note = "SIMULATED WRONG GATEWAY - destination unreachable";
                else if (scenario == "MTU black hole" && i >= 10) note = "SIMULATED OVERSIZE DROP - PMTU discovery fails";
                else if (scenario == "Routing loop" && i >= 7) note = "SIMULATED LOOP - TTL decreases";
                sb.AppendLine(i.ToString("00", CultureInfo.InvariantCulture) + "  " + note);
                if ((scenario == "DNS timeout" && i >= 4) || (scenario == "Wrong gateway" && i >= 3)) break;
            }
            return sb.ToString();
        }
        TabPage LabWitnessPage()
        {
            FlowLayoutPanel p; var page = LabTab("Device Witness", out p);
            p.Controls.Add(LabLabel("Two-device worksheet. Phone results must be entered manually for now; no claim of synchronized phone measurements.", true));
            p.Controls.Add(LabLabel("Enter a phone-side latency measurement in milliseconds, taken at roughly the same time.", false));
            var phone = new TextBox { Width = Sc(180), Text = labPhoneNote };
            var output = new TextBox { Multiline = true, ReadOnly = true, Width = Sc(730), Height = Sc(230),
                Font = new Font("Consolas", 10) };
            p.Controls.Add(phone);
            p.Controls.Add(LabButton("Compare observations", delegate
            {
                double v; labPhoneNote = phone.Text.Trim();
                if (!double.TryParse(labPhoneNote, NumberStyles.Float, CultureInfo.CurrentCulture, out v) || v < 0 || v > 60000)
                { output.Text = "Enter a valid latency in milliseconds (0 to 60000)."; return; }
                double pc = M.NetPing.Avg(6);
                output.Text = "PC Internet check: " + LabNumber(pc) + " ms (recent probe average)" +
                    Environment.NewLine + "Phone (manual entry): " + v.ToString("0.#", CultureInfo.CurrentCulture) +
                    " ms" + Environment.NewLine + Environment.NewLine +
                    "Targets, methods and clock alignment are not verified." + Environment.NewLine +
                    "These numbers alone cannot localize an ISP, router or device fault.";
            }));
            p.Controls.Add(output); return page;
        }
    }
}
