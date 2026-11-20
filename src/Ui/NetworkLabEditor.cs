using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
namespace AdeeboMeter
{
    // A fully offline editable lab graph. Does not create sockets or transmit packets.
    sealed class NetworkLabEditor : Form
    {
        sealed class Node { public string Name, Kind; public Point Position; }
        readonly List<Node> nodes = new List<Node>();
        readonly List<Tuple<int,int>> links = new List<Tuple<int,int>>();
        readonly Panel surface;
        readonly ComboBox kind;
        readonly Label status;
        Node dragged;
        int linkStart = -1;
        int serial = 1;
        readonly string path;
        public NetworkLabEditor()
        {
            Text = "Network Sandbox - topology editor";
            Width = 940; Height = 640; MinimumSize = new Size(560, 390);
            StartPosition = FormStartPosition.CenterParent;
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AdeeboMeter", "sandbox-topology.txt");
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, AutoScroll = true };
            kind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
            kind.Items.AddRange(new object[] { "PC", "Switch", "Router", "DNS", "Firewall", "Server" });
            kind.SelectedIndex = 0; bar.Controls.Add(kind);
            AddButton(bar, "Add device", delegate { if (nodes.Count >= 32) return;
                nodes.Add(new Node { Name = kind.Text + " " + serial++, Kind = kind.Text,
                    Position = new Point(100 + nodes.Count % 5 * 135, 95 + nodes.Count / 5 * 95) });
                surface.Invalidate(); });
            AddButton(bar, "Link nodes", delegate { linkStart = -2; status.Text = "Click the first node, then the second."; });
            AddButton(bar, "Delete selected", delegate { if (dragged == null) return;
                int idx = nodes.IndexOf(dragged); if (idx < 0) return;
                nodes.RemoveAt(idx); links.RemoveAll(l => l.Item1 == idx || l.Item2 == idx);
                for (int i = 0; i < links.Count; i++) { var l = links[i];
                    links[i] = Tuple.Create(l.Item1 > idx ? l.Item1 - 1 : l.Item1,
                        l.Item2 > idx ? l.Item2 - 1 : l.Item2); }
                dragged = null; surface.Invalidate(); });
            AddButton(bar, "Save lab", Save);
            AddButton(bar, "Load lab", Load);
            AddButton(bar, "Reset", delegate { nodes.Clear(); links.Clear(); dragged = null; surface.Invalidate(); });
            surface = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(246, 248, 251) };
            surface.Paint += PaintGraph;
            surface.MouseDown += delegate(object s, MouseEventArgs e)
            {
                dragged = nodes.LastOrDefault(n => Hit(n, e.Location));
                if (linkStart != -1)
                {
                    int idx = dragged == null ? -1 : nodes.IndexOf(dragged);
                    if (linkStart == -2) linkStart = idx < 0 ? -2 : idx;
                    else { if (idx >= 0 && idx != linkStart && !links.Any(l =>
                        (l.Item1 == idx && l.Item2 == linkStart) || (l.Item2 == idx && l.Item1 == linkStart))
                        && links.Count < 64) links.Add(Tuple.Create(linkStart, idx));
                        linkStart = -1; }
                    dragged = null; surface.Invalidate(); return;
                }
            };
            surface.MouseMove += delegate(object s, MouseEventArgs e) {
                if (dragged != null && e.Button == MouseButtons.Left)
                { dragged.Position = new Point(Math.Max(0, Math.Min(surface.Width - 100, e.X - 48)),
                    Math.Max(0, Math.Min(surface.Height - 50, e.Y - 20))); surface.Invalidate(); }
            };
            surface.MouseUp += delegate { dragged = null; };
            status = new Label { Dock = DockStyle.Bottom, Height = 28, Text = "Offline only. Add devices, drag them and link them." };
            Controls.Add(surface); Controls.Add(status); Controls.Add(bar);
        }
        static void AddButton(FlowLayoutPanel p, string name, Action action)
        { var b = new Button { Text = name, AutoSize = true }; b.Click += delegate { action(); }; p.Controls.Add(b); }
        static bool Hit(Node n, Point p) { return new Rectangle(n.Position, new Size(100, 48)).Contains(p); }
        void PaintGraph(object sender, PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(74, 130, 183), 2))
                foreach (var l in links) if (l.Item1 < nodes.Count && l.Item2 < nodes.Count)
                    g.DrawLine(pen, nodes[l.Item1].Position.X + 50, nodes[l.Item1].Position.Y + 24,
                        nodes[l.Item2].Position.X + 50, nodes[l.Item2].Position.Y + 24);
            foreach (var n in nodes)
            {
                var r = new Rectangle(n.Position, new Size(100, 48));
                using (var b = new SolidBrush(Color.FromArgb(225, 239, 252))) g.FillRectangle(b, r);
                using (var pen = new Pen(Color.FromArgb(74, 130, 183))) g.DrawRectangle(pen, r);
                using (var b = new SolidBrush(Color.FromArgb(20, 40, 65)))
                { g.DrawString(n.Kind, Font, b, r.X + 6, r.Y + 5);
                  g.DrawString(n.Name, Font, b, r.X + 6, r.Y + 25); }
            }
        }
        void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var lines = new List<string> { "ADEEBO-LAB-1" };
                foreach (var n in nodes) lines.Add("N|" + n.Kind + "|" + n.Name + "|" + n.Position.X + "|" + n.Position.Y);
                foreach (var l in links) lines.Add("E|" + l.Item1 + "|" + l.Item2);
                var tmp = path + ".part"; File.WriteAllLines(tmp, lines.ToArray(), Encoding.UTF8);
                if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
                status.Text = "Lab saved locally.";
            }
            catch (Exception e) { status.Text = "Save failed: " + e.Message; }
        }
        void Load()
        {
            try
            {
                if (!File.Exists(path)) { status.Text = "No saved lab."; return; }
                if (new FileInfo(path).Length > 64 * 1024) throw new InvalidDataException("Lab file too large.");
                var rows = File.ReadAllLines(path); if (rows.Length == 0 || rows[0] != "ADEEBO-LAB-1")
                    throw new InvalidDataException("Unknown lab format.");
                var nn = new List<Node>(); var ll = new List<Tuple<int,int>>();
                foreach (var row in rows.Skip(1))
                {
                    var v = row.Split('|'); int x, y;
                    if (v.Length == 5 && v[0] == "N" && nn.Count < 32 &&
                        new[] { "PC", "Switch", "Router", "DNS", "Firewall", "Server" }.Contains(v[1]) &&
                        v[2].Length <= 60 && int.TryParse(v[3], out x) && int.TryParse(v[4], out y))
                        nn.Add(new Node { Kind = v[1], Name = v[2], Position = new Point(Math.Max(0, Math.Min(4000,x)), Math.Max(0, Math.Min(4000,y))) });
                    else if (v.Length == 3 && v[0] == "E" && ll.Count < 64 &&
                        int.TryParse(v[1], out x) && int.TryParse(v[2], out y))
                        ll.Add(Tuple.Create(x,y));
                }
                if (ll.Any(l => l.Item1 < 0 || l.Item2 < 0 || l.Item1 >= nn.Count || l.Item2 >= nn.Count))
                    throw new InvalidDataException("Invalid link reference.");
                nodes.Clear(); nodes.AddRange(nn); links.Clear(); links.AddRange(ll);
                serial = nodes.Count + 1; surface.Invalidate(); status.Text = "Lab loaded.";
            }
            catch (Exception e) { status.Text = "Load failed: " + e.Message; }
        }
    }
}
