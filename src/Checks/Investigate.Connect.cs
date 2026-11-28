// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Globalization;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== One evidence layer (28 Nov, Adeeb: "connect the tools")
    // After any network investigation: (1) this PC, ruled in or out from its last two minutes, so a network verdict never
    // blames the line for a busy disk - and never the PC for a bad line; (2) when something is wrong, what changed on this PC
    // just before (Network Doctor), as evidence; (3) a "next" button that opens the tool that digs deeper, boxes filled in.
    static partial class Investigate
    {
        static void Connect(string id, Monitor m, GuideRun r)
        {
            if (id == "pc" || id == "full") return;   // those read the PC themselves
            // 1. this PC
            bool hasPc = r.ObservedCopy().Any(o => o[2].StartsWith("This PC", StringComparison.Ordinal) || o[2].StartsWith("Your PC", StringComparison.Ordinal));
            if (!hasPc)
            {
                var cap = PcRecord.Judge(PcRecord.Last(120), DateTime.Now);
                if (cap.Who == "pc" || cap.Who == "both") r.Obs("m", Math.Max(1, cap.Lv), "This PC", cap.Text);
                else if (cap.Who == "notpc" || cap.Who == "internet" || cap.Who == "app") r.Obs("m", 0, "This PC", "not the cause - its processor, memory and disk weren't holding anything back in the last 2 minutes");
            }
            // 2. what changed, only when something is wrong
            if (r.Lv >= 1 && r.Lv <= 2)
            {
                r.Status = "Checking what changed on this PC lately...";
                try
                {
                    var dr = Doctor.Analyse(14);
                    if (dr.Suspect != null)
                    {
                        r.Obs("w", 1, "What changed just before", dr.Suspect.What + " · " + dr.Suspect.T.ToString("ddd d MMM", CultureInfo.CurrentCulture));
                        r.Next.Add("Network Doctor lines up this change with the days things went wrong - and how to undo it.");
                        if (r.GoTab == "") { r.GoTab = "doctor"; r.GoLabel = "Open Network Doctor"; }
                    }
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("connect doctor: " + e.Message); }
            }
            // 3. the tool that digs deeper, when the flow didn't already pick one
            if (r.GoTab == "" && r.Lv >= 1 && r.Lv <= 2)
            {
                if (id == "slow") { r.GoTab = "tool:mtr|1.1.1.1|2"; r.GoLabel = "Run MTR - where the delay starts"; }
                else if (id == "drops") { r.GoTab = "link"; r.GoLabel = "Open Your Wi-Fi"; }
                else if (id == "site" && r.Input != "") { r.GoTab = "tool:http|" + r.Input; r.GoLabel = "Open HTTP & TLS for " + r.Input; }
                else if (id == "vpn") { r.GoTab = "tool:udpmtu"; r.GoLabel = "Run MTU over UDP"; }
            }
        }
    }
}
