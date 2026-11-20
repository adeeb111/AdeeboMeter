using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace AdeeboMeter
{
    sealed partial class FullView
    {
        // Recipes navigate to existing checks. The user still presses Run for each check.
        void ShowDiagnosticRecipes()
        {
            string[][] plans = {
                new[] { "Game lag / unstable ping", "bloat", "mtr", "gamecompare", "ispreport" },
                new[] { "Slow website / DNS", "dig", "httptime", "mtr" },
                new[] { "VPN / service trouble", "route", "tcpping", "mtu" }
            };
            using (var f = new Form { Text = "Guided diagnosis", Size = new Size(Sc(680), Sc(490)),
                MinimumSize = new Size(Sc(480), Sc(330)), StartPosition = FormStartPosition.CenterParent,
                BackColor = Theme.Card, ForeColor = Theme.Ink, Font = Draw.fBody })
            {
                var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                    WrapContents = false, AutoScroll = true, Padding = new Padding(Sc(16)) };
                panel.Controls.Add(new Label { Text = "Choose a symptom. Open checks individually; no automatic tests.",
                    AutoSize = true, Font = Draw.fBodyB, Margin = new Padding(0, 0, 0, Sc(12)) });
                foreach (var plan in plans)
                {
                    panel.Controls.Add(new Label { Text = plan[0], AutoSize = true, Font = Draw.fBodyB,
                        Margin = new Padding(0, Sc(10), 0, Sc(5)) });
                    foreach (var id in plan.Skip(1))
                    {
                        var tool = Tools2().FirstOrDefault(t => t.Id == id);
                        if (tool == null) continue;
                        string tid = id;
                        var btn = new Button { Text = "Open " + tool.Title + "  (manual Run)", AutoSize = true,
                            Margin = new Padding(Sc(12), 0, 0, Sc(4)) };
                        btn.Click += delegate { if (tbBusy) { BusyNote(); return; } f.Close(); OpenTool(tid); };
                        panel.Controls.Add(btn);
                    }
                }
                f.Controls.Add(panel); f.ShowDialog(this);
            }
        }
    }
}
