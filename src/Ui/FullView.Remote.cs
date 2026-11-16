// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    sealed partial class FullView
    {
        // a check started from the phone: runs here off the UI thread (this view may never be shown), answer goes back sealed.
        // Only ids in Remote.Allowed reach this; inputs are checked by each tool as if typed.
        static string[] R4(string a, string b, string c, string d) { return new[] { a, b, c, d }; }
        internal void RemoteRun(string id, string[] input, Action<string, List<string[]>> done)
        {
            var t = Tools2().FirstOrDefault(z => z.Id == id);
            if (t == null || t.Do == null || !Remote.Allowed.Contains(id)) { done(id, new List<string[]> { R4("2", "Not available", "this check can't run from the phone", "") }); return; }
            var a = new string[t.Inputs.Length]; for (int i = 0; i < a.Length; i++) a[i] = i < input.Length ? (input[i] ?? "").Trim() : "";
            if (id == "mtr" && a.Length > 1) a[1] = "1";   // a one-minute recording at most from the phone
            Task.Factory.StartNew(() =>
            {
                List<string[]> res; var run = new Toolkit.Run { Deadline = DateTime.UtcNow.AddMinutes(3) };
                try { res = t.Do(a, run) ?? new List<string[]>(); } catch (Exception e) { if (e is OutOfMemoryException) throw; res = new List<string[]> { new[] { "2", "Didn't finish", e.Message, "" } }; }
                done(t.Title, res.Where(o => o != null && !IsSecretRow(o)).ToList());
            }, TaskCreationOptions.LongRunning);
        }
        // the full checkup, the same one the Guide runs, without opening anything on the PC
        internal static void RemoteCheckup(Monitor m, Action<string, List<string[]>> done)
        {
            Task.Factory.StartNew(() =>
            {
                var r = new GuideRun(); var rows = new List<string[]>();
                try
                {
                    Investigate.Run("full", m, r, () => { });
                    rows.Add(new[] { r.Lv < 0 ? "3" : r.Lv.ToString(System.Globalization.CultureInfo.InvariantCulture), "Verdict", r.Finding, r.Next.FirstOrDefault() ?? "" });
                    rows.Add(R4("-1", "What was checked", "", ""));
                    lock (r.Observed) foreach (var o in r.Observed.Take(60)) rows.Add(new[] { o.Length > 1 ? o[1] : "3", o.Length > 2 ? o[2] : "", o.Length > 3 ? o[3] : "", "" });
                    if (r.Next.Count > 1) { rows.Add(R4("-1", "Next steps", "", "")); foreach (var n in r.Next.Take(6)) rows.Add(new[] { "3", "Next", n, "" }); }
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; rows.Add(new[] { "2", "Didn't finish", e.Message, "" }); }
                done(r.Title != "" ? r.Title : "Full checkup", rows);
            }, TaskCreationOptions.LongRunning);
        }
    }
}
