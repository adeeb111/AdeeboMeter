// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Monitor -> AdeeboShell -> AdeeboLab (24 Nov, Adeeb: "build it"): a finished investigation can be re-run as commands in
    // the shell (the line is typed in, not run - Enter is yours), and opened in AdeeboLab as your own network, drawn from what
    // was measured, ready to Break it. From a prompt, the shell's own "lab" command (FullView.Shell.cs) opens the same views.
    sealed partial class FullView
    {
        // the commands that check the same things again, as one line for the prompt
        static string GuideScript(GuideRun r)
        {
            string host = (r.Input ?? "").Trim();
            if (r.Id == "site" && host != "" && host.IndexOfAny(new[] { ' ', ';', '|', '&', '>', '<', '$', '`', '"', '\'' }) < 0)
                return "dig " + host + " A ; ping " + host + " 10 ; curl -I https://" + host + " ; mtr " + host + " 1";
            switch (r.Id)
            {
                case "pc": return "cd /data/scripts ; ./system-check.sh";
                case "calls": return "cd /data/scripts ; ./call-check.sh";
                case "drops": return "cd /data/scripts ; ./wifi-check.sh";
                default: return "cd /data/scripts ; ./network-check.sh";
            }
        }
        // which AdeeboLab view fits the investigation: the measured path when one was traced, otherwise your network
        static string GuideLabView(string id)
        {
            switch (id)
            {
                case "drops": case "calls": return "wifi";
                case "vpn": return "vpn";
                default: return LabViews.HasPath() ? "path" : "lan";
            }
        }
        // opens AdeeboShell with the line typed at the prompt, the caret at its end
        void ShellPrefill(string line)
        {
            tab = "shell"; ShowTab(); SyncNav();
            BeginInvoke((Action)delegate
            {
                if (term == null || term.IsDisposed) return;
                term.Input = line ?? ""; term.Caret = term.Input.Length; term.Invalidate(); term.Focus();
            });
        }
    }
}
