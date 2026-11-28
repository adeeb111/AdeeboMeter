// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AdeeboMeter
{
    // ================================================================== "Check my PC" (28 Nov, Adeeb: "as a normal person I want to know my PC")
    // The used-laptop check, turned toward your own PC and wired to everything useful the app already has: what's inside, is
    // it held back (processor, memory, graphics, power plan, startup), the disk (kind, space, health, wear, errors), the
    // battery and its wear over time, how stable it is (crashes, failed services, hardware errors, a waiting restart), what
    // changed lately (Network Doctor), and the plugs (network link, USB power, screens). Each row names the tool that digs
    // deeper - a click opens it. Read-only, no admin; what Windows keeps to itself says so.
    static class PcReport
    {
        static string[] R(int lv, string name, string val, string adv, string ev, string act) { return new[] { lv.ToString(CultureInfo.InvariantCulture), name, val ?? "", adv ?? "", ev ?? "", act ?? "" }; }
        static string F(double v) { return HwSpecs.Fmt(v); }
        static int Lv(string[] r) { int l; return r != null && int.TryParse(r[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out l) ? l : 3; }
        static string[] Verdict(List<string[]> rows) { return rows == null ? null : rows.FirstOrDefault(x => x.Length > 1 && x[1] == "Verdict"); }
        static string[] Safe(Func<List<string[]>> f, string what)
        {
            try { return Verdict(f()); }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("my pc " + what + ": " + e.Message); return null; }
        }

        public static List<string[]> Run(Monitor m, Toolkit.Run run)
        {
            var rows = new List<string[]>(); var issues = new List<Tuple<int, string, string>>();   // level, what, the tool that helps
            Func<bool> stop = () => run != null && run.Stop;
            Action<string> sec = t => rows.Add(R(-1, t, "", "", "", ""));
            Action<int, string, string, string, string, string> add = (lv, name, val, adv, ev, act) =>
            { rows.Add(R(lv, name, val, adv, ev, act)); if (lv == 1 || lv == 2) issues.Add(Tuple.Create(lv, name + ": " + val, act)); };
            Action<string[], string, string> take = (v, title, act) => { if (v != null) add(Lv(v), title, v[2], v[3], v[4], act); else add(3, title, "couldn't be read here", "", "u", act); };
            HwSpecs.Specs s;
            try { s = HwSpecs.Get(); }
            catch (TimeoutException e) { rows.Add(R(3, "This PC", e.Message, "", "u", "")); return rows; }

            // ---------- what it is
            if (run != null) run.Status = "Reading what's inside...";
            sec("WHAT'S INSIDE");
            add(3, "Model", (s.Maker + " " + s.Model).Trim(), "", "w", "tool:hwmap");
            add(3, "Processor", s.Cpu + (s.Cores > 0 ? " · " + s.Cores + " cores" : ""), "", "w", "tool:hwmap");
            add(s.RamGb > 0 && s.RamGb < 8 ? 1 : 3, "Memory", HwTools.Gb(s.RamGb) + " " + HwSpecs.RamTypeOf(s) + " · " + HwSpecs.ChText(s), s.RamGb > 0 && s.RamGb < 8 ? "8 GB is the comfortable minimum for Windows and a browser." : "", "w", "tool:ramcheck");
            foreach (var d in s.Disks.Where(d => d.Bus != "USB" && d.Bus != "Virtual").Take(3))
                add(d.Health == 1 ? 1 : d.Health == 2 ? 2 : 3, d.System ? "Windows disk" : "Disk", d.Name + " · " + F(d.Gb) + " GB " + d.Media + (d.Health == 1 ? " · Windows warns about it" : d.Health == 2 ? " · Windows says it's failing" : ""),
                    d.Health == 2 ? "Back up now - Windows' own health check says this disk is failing." : d.Health == 1 ? "Back up soon and watch it - Windows has a warning for this disk." : "", "w", "tool:disk");
            foreach (var g in s.Gpus.Where(g => !g.Software)) add(3, g.Integrated ? "Built-in graphics" : "Graphics card", g.Name + (g.Integrated ? "" : " · " + HwTools.Gb(g.DedicatedMb / 1024)), "", "w", "tool:gpucheck");
            if (stop()) return Done(rows, issues, true);

            // ---------- is it held back
            sec("SPEED - IS ANYTHING HOLDING IT BACK?");
            if (run != null) run.Status = "Testing the processor (3 seconds)...";
            take(Safe(() => HwTools.Cpu(run), "cpu"), "Processor", "tool:cpuheld");
            if (stop()) return Done(rows, issues, true);
            if (run != null) run.Status = "Measuring memory...";
            take(Safe(() => HwTools.Ram(run), "ram"), "Memory", "tool:ramcheck");
            take(Safe(() => HwTools.Gpu(), "gpu"), "Graphics", "tool:gpucheck");
            List<string[]> hidden = null; try { hidden = HwTools.Hidden(m); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("my pc hidden: " + e.Message); }
            Func<string, string[]> hid = name => hidden == null ? null : hidden.FirstOrDefault(x => x[1].StartsWith(name, StringComparison.Ordinal));
            var pw = hid("Power"); if (pw != null) add(Lv(pw), "Power plan", pw[2], pw[3], pw[4], "tool:hidden");
            var st = hid("Start with Windows"); if (st != null) add(Lv(st), "Starts with Windows", st[2], st[3], st[4], "tool:tidy");
            if (hidden != null) foreach (var b in hidden.Where(x => x[1].StartsWith("Browser:", StringComparison.Ordinal) && x[0] == "1")) add(1, b[1], b[2], b[3], b[4], "tool:bg");
            string pat = PcRecord.Pattern(); if (pat != "") add(3, "When it felt slow before", pat, "", "m", "tool:feltslow");
            if (stop()) return Done(rows, issues, true);

            // ---------- storage
            if (run != null) run.Status = "Checking the disk...";
            sec("STORAGE");
            var on = hid("Windows is on"); if (on != null) add(Lv(on), "Windows is on", on[2], on[3], on[4], "tool:diskspeed");
            var fs = hid("Free space"); if (fs != null) add(Lv(fs), fs[1], fs[2], fs[3], fs[4], "tool:space");
            try
            {
                var en = PcInside.Endurance("");
                var life = en.FirstOrDefault(x => x[1] == "Life used"); if (life != null && Lv(life) != 3) add(Lv(life), "SSD life used", life[2], life[3], life[4], "tool:endurance");
                var pace = en.FirstOrDefault(x => x[1] == "At this pace"); if (pace != null) add(3, "SSD life left", pace[2], pace[3], pace[4], "tool:endurance");
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("my pc endurance: " + e.Message); }
            List<Tuple<string, string, int>> errs = null; try { errs = HwTools.Errors(30); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("my pc errors: " + e.Message); }
            if (errs != null)
            {
                var de = errs.Where(e => (e.Item1 == "disk" || e.Item1 == "reset") && e.Item3 > 0).ToList();
                add(de.Count > 0 ? 2 : 0, "Disk errors (30 days)", de.Count == 0 ? "none" : string.Join(" · ", de.Select(e => e.Item3 + " " + e.Item2)), de.Count > 0 ? "Back up now: disk errors in Windows' own log come before most disk failures." : "", "w", "tool:events");
            }
            if (stop()) return Done(rows, issues, true);

            // ---------- battery
            if (System.Windows.Forms.SystemInformation.PowerStatus.BatteryChargeStatus != System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)
            {
                if (run != null) run.Status = "Reading the battery report...";
                sec("BATTERY");
                try { foreach (var b in PcHealth.Battery().Where(x => x[1] != "Battery" || x[0] != "3").Take(6)) add(Lv(b), b[1], b[2], b[3], b[4], "tool:battery"); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("my pc battery: " + e.Message); }
            }

            // ---------- stability
            if (run != null) run.Status = "Reading Windows' logs...";
            sec("STABILITY");
            var lr = hid("Last restart"); if (lr != null) add(Lv(lr), "Last restart", lr[2], lr[3], lr[4], "");
            var rp = hid("Restart waiting"); if (rp != null) add(Lv(rp), rp[1], rp[2], rp[3], rp[4], "tool:winupdate");
            try
            {
                var cr = PcHealth.Crashes(); var bad = cr.Where(x => x[0] == "1" || x[0] == "2").ToList();
                add(bad.Count == 0 ? 0 : bad.Max(x => Lv(x)), "Crashes and hangs (7 days)", bad.Count == 0 ? "none" : string.Join(" · ", bad.Take(3).Select(x => x[1] + " " + x[2])), "", "w", "tool:crash");
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("my pc crashes: " + e.Message); }
            try { var sv = MoreTools.FailedServices().FirstOrDefault(); if (sv != null) add(Lv(sv), "Windows services (7 days)", sv[2], sv[3], sv[4], "tool:svcfail"); }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("my pc services: " + e.Message); }
            if (errs != null)
            {
                var he = errs.Where(e => (e.Item1 == "whea" || e.Item1 == "power") && e.Item3 > 0).ToList();
                add(he.Count > 0 ? 1 : 0, "Hardware and power-off errors (30 days)", he.Count == 0 ? "none" : string.Join(" · ", he.Select(e => e.Item3 + " " + e.Item2)), he.Count > 0 ? "Sudden power-offs are often a battery, a loose power cable or overheating; hardware errors point at a part." : "", "w", "tool:events");
            }
            if (stop()) return Done(rows, issues, true);

            // ---------- what changed lately (Network Doctor)
            if (run != null) run.Status = "Lining up what changed...";
            sec("WHAT CHANGED LATELY");
            try
            {
                Doctor.SnapToday(); var dr = Doctor.Analyse(14);
                add(3, "Changes in 14 days", dr.Changes.Count == 0 ? "none recorded" : dr.Changes.Count + " (updates, drivers, apps, settings)", "", "w", "doctor");
                if (dr.Suspect != null) add(1, "Suspect", dr.Suspect.What + " · " + dr.Suspect.T.ToString("ddd d MMM", CultureInfo.CurrentCulture), dr.Why, "w", "doctor");
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("my pc doctor: " + e.Message); add(3, "Changes", "couldn't be read here", "", "u", "doctor"); }
            if (UpdateBreak.Strong != "") add(1, "Windows update", UpdateBreak.Strong, "", "w", "tool:winupdate");

            // ---------- plugs: network link, USB power, screens
            if (run != null) run.Status = "Checking the network link, USB and screens...";
            sec("CONNECTIONS");
            var nl = HwTools.NetLinkIssue(m, s);
            add(nl == null ? 0 : Lv(nl), nl == null ? "Network link" : nl[1], nl == null ? (s.NicKind == "" ? "nothing holding your internet back here" : s.NicKind + (s.NicMbps > 0 ? " · " + F(s.NicMbps) + " Mbps" : "") + " - not what's holding your internet back") : nl[2], nl == null ? "" : nl[3], "w", "tool:netcap");
            try { var u = PcInside.Usb().FirstOrDefault(); if (u != null && u[1] == "Power budget") add(Lv(u), "USB power", u[2], u[3], u[4], "tool:usbtree"); }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("my pc usb: " + e.Message); }
            try { foreach (var sc in PcInside.Edids().Where(x => x[0] == "0").Take(3)) add(sc[3] != "" ? 1 : 0, "Screen: " + sc[1], sc[2], sc[3], sc[4], "tool:edid"); }
            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("my pc screens: " + e.Message); }
            return Done(rows, issues, false);
        }

        // the answer first: how it is, the biggest thing, and where to go - then everything else
        static List<string[]> Done(List<string[]> rows, List<Tuple<int, string, string>> issues, bool stopped)
        {
            var worst = issues.OrderByDescending(i => i.Item1).ToList();
            string verdict = worst.Count == 0 ? "Your PC is in good shape: nothing is holding it back, the disk and battery are healthy and Windows is stable."
                : worst[0].Item1 == 2 ? worst.Count + (worst.Count == 1 ? " thing needs" : " things need") + " attention - the most important: " + worst[0].Item2
                : worst.Count + (worst.Count == 1 ? " small thing" : " small things") + " to look at - first: " + worst[0].Item2;
            var top = new List<string[]>();
            if (stopped) top.Add(R(3, "Stopped", "the check was stopped before it finished - the rows below are what it had", "", "i", ""));
            top.Add(R(worst.Count == 0 ? 0 : worst[0].Item1, "Verdict", verdict, worst.Count > 0 && worst[0].Item3 != "" ? "Click a row to open the tool that digs deeper." : "", "i", worst.Count > 0 ? worst[0].Item3 : ""));
            top.AddRange(rows); return top;
        }
    }
}
