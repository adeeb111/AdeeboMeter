using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
namespace AdeeboMeter
{
    sealed partial class FullView
    {
        void ShowStoryEvidence()
        {
            var r = travelDest != null && travelDest.Input == storyTarget ? travelDest : null;
            var sb = new StringBuilder();
            sb.AppendLine("NETWORK STORY / EVIDENCE");
            sb.AppendLine("Viewed at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
            sb.AppendLine("Target: " + storyTarget);
            sb.AppendLine("App connection: " + (storyApp ?? "manually entered target"));
            sb.AppendLine("App endpoint: " + (storyIp == "" ? "not observed" : storyIp));
            sb.AppendLine();
            sb.AppendLine("These are separate destination tests, NOT a captured replay of the app's packets.");
            sb.AppendLine("Measurements may target a different server than the selected application.");
            sb.AppendLine();
            if (r == null || !r.Done) sb.AppendLine("Destination test not completed. Use 'Time each step' to request one.");
            else
            {
                sb.AppendLine("DNS: " + StoryMs(r.DnsMs));
                sb.AppendLine("TCP connect: " + StoryMs(r.TcpMs));
                sb.AppendLine("TLS: " + StoryMs(r.TlsMs));
                sb.AppendLine("First byte: " + StoryMs(r.TtfbMs));
                sb.AppendLine("Test destination IP: " + (r.Ip ?? "unknown"));
                sb.AppendLine("Observed route stages: " + (r.Story == null ? "not measured" : r.Story.Count.ToString(CultureInfo.CurrentCulture)));
                if (r.Story != null) foreach (var hop in r.Story)
                    sb.AppendLine("  " + string.Join(" | ", hop));
            }
            sb.AppendLine();
            sb.AppendLine("Stage guide: App -> Windows -> Wi-Fi/Cable -> Router -> Provider -> Internet -> Server.");
            sb.AppendLine("Stage labels are a conceptual model, not a packet capture.");
            using (var f = new Form { Text = "Network Story - evidence", Size = new Size(Sc(740), Sc(540)),
                MinimumSize = new Size(Sc(520), Sc(340)), StartPosition = FormStartPosition.CenterParent,
                BackColor = Theme.Card, ForeColor = Theme.Ink })
            {
                var box = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                    ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10),
                    BackColor = Theme.Card, ForeColor = Theme.Ink, Text = sb.ToString() };
                var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = Sc(52), WrapContents = false };
                var route = new Button { Text = "Open route diagnostics", AutoSize = true };
                route.Click += delegate { f.Close(); ShowPage("toolbox"); EnsureToolbox(); OpenTool("mtr"); };
                var close = new Button { Text = "Close", AutoSize = true };
                close.Click += delegate { f.Close(); };
                actions.Controls.Add(route); actions.Controls.Add(close);
                f.Controls.Add(box); f.Controls.Add(actions); f.ShowDialog(this);
            }
        }
        static string StoryMs(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0 ? "not measured" :
                value.ToString("0.#", CultureInfo.CurrentCulture) + " ms";
        }
    }
}
