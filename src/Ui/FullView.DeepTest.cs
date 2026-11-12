// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // --deeptest: hardening run on the real PC. Every tool with hostile input, the safe tools for real, the same tools
    // over and over (leaks), and the Toolbox UI under stress - one at a time, at low priority, each with a time limit.
    // Network: only the gateway, loopback and a few well-known public names (DNS, HTTPS, NTP, STUN) - never a sweep,
    // never your company's servers, never a routing packet. Writes AdeeboMeter-deeptest.txt next to the exe.
    sealed partial class FullView
    {
        sealed class Probe { public string Status = "OK"; public long Ms; public int Rows; public double MemMb; public int Handles, Gdi, Threads; }

        static readonly string[] Hostile = { "", " ", "x;rm -rf /", "$(calc)", "&calc&", "'\"<>|%n%s", new string('a', 5000), "999.999.999.999", "::::::", "-1", "http://", "a\u0000b", "😀.example", "../../windows/win.ini", "127.0.0.1:99999" };
        // tools that act on the network the moment their input is valid, or need gear only you have - checked by hand (TESTPLAN-1.1.md)
        static readonly HashSet<string> ByHand = new HashSet<string> { "mtu", "sweep", "wol", "iperf", "twamp", "snmp", "syslog", "radius", "tacacs", "ssh", "mail", "owners", "rlisten", "mtr", "bloat", "dscp", "discover", "ispreport", "speedtest", "udpmtu" };

        Probe RunOne(Tool t, string[] a, int limitMs)
        {
            if (a.Length < 4) { var a4 = new string[4]; for (int i = 0; i < 4; i++) a4[i] = i < a.Length && a[i] != null ? a[i] : ""; a = a4; }   // the tools read up to four boxes
            var p = new Probe(); var run = new Toolkit.Run(); List<string[]> res = null; Exception err = null;
            var me = Process.GetCurrentProcess(); me.Refresh(); long m0 = me.PrivateMemorySize64; int h0 = me.HandleCount, g0 = Native.GetGuiResources(Native.GetCurrentProcess(), 0), t0 = me.Threads.Count;
            var sw = Stopwatch.StartNew();
            var task = Task.Factory.StartNew(() => { try { res = t.Do(a, run); } catch (Exception e) { err = e; } }, TaskCreationOptions.LongRunning);
            while (!task.Wait(40)) { Application.DoEvents(); if (sw.ElapsedMilliseconds > limitMs && !run.Stop) run.Stop = true; if (sw.ElapsedMilliseconds > limitMs + 10000) break; }   // Stop is asked first; 10 s later it counts as hung
            p.Ms = sw.ElapsedMilliseconds;
            if (!task.IsCompleted) p.Status = "HUNG (ignored Stop)";
            else if (err != null) p.Status = "THREW " + err.GetType().Name + ": " + Draw.Trunc(err.Message, 80) + " at " + Draw.Trunc((err.StackTrace ?? "").Trim().Split('\n')[0].Trim(), 120);
            else if (res == null) p.Status = "NO ROWS (null)";
            else
            {
                p.Rows = res.Count;
                var bad = res.FirstOrDefault(r => r == null || r.Length < 3 || r.Any(c => c == null));
                if (bad != null) p.Status = "BAD ROW (null or short)";
                else if (res.Count == 0) p.Status = "EMPTY (no rows at all)";
                else if (p.Ms > limitMs) p.Status = "SLOW (stopped at the limit)";
            }
            me.Refresh(); p.MemMb = (me.PrivateMemorySize64 - m0) / 1048576.0; p.Handles = me.HandleCount - h0; p.Gdi = Native.GetGuiResources(Native.GetCurrentProcess(), 0) - g0; p.Threads = me.Threads.Count - t0;
            if (res != null && task.IsCompleted) { tbRows = res; tbSel = t.Id; tbTitle = t.Title; tbAt = DateTime.Now; try { tbCanvas.Refresh(); } catch (Exception e) { p.Status = "PAINT THREW " + e.GetType().Name; } }   // and the result must paint
            return p;
        }
        static string Line(string what, Probe p) { return (p.Status == "OK" ? "ok    " : "FAIL  ") + what.PadRight(44) + p.Ms.ToString(CultureInfo.InvariantCulture).PadLeft(6) + " ms  " + p.Rows.ToString(CultureInfo.InvariantCulture).PadLeft(3) + " rows  mem " + p.MemMb.ToString("+0.0;-0.0", CultureInfo.InvariantCulture).PadLeft(6) + " MB  handles " + p.Handles.ToString("+0;-0", CultureInfo.InvariantCulture).PadLeft(4) + "  gdi " + p.Gdi.ToString("+0;-0", CultureInfo.InvariantCulture).PadLeft(3) + (p.Status == "OK" ? "" : "   " + p.Status); }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861", Justification = "one-shot test data")]
        public string DeepTest(bool net, int capMinutes)
        {
            var sb = new StringBuilder(); var all = Stopwatch.StartNew(); int fails = 0;
            bool skipped = false;
            Func<bool> late = () => { if (all.Elapsed.TotalMinutes > capMinutes) skipped = true; return skipped; };
            Action<string> head = s => { sb.AppendLine(); sb.AppendLine("== " + s + "   (" + (int)all.Elapsed.TotalSeconds + " s)"); };
            Action<string, Probe> rec = (w, p) => { if (p.Status != "OK") fails++; sb.AppendLine(Line(w, p)); };
            var me = Process.GetCurrentProcess(); me.Refresh(); long mStart = me.PrivateMemorySize64; int hStart = me.HandleCount, gStart = Native.GetGuiResources(Native.GetCurrentProcess(), 0), tStart = me.Threads.Count;
            tab = "toolbox"; ShowTab(); EnsureToolbox(); Application.DoEvents();
            string gw; lock (M.L) gw = M.Gateway; if (gw == "") gw = "127.0.0.1";

            // 1. hostile input into every box of every tool - must answer with rows, never throw, hang or crash
            head("1. Hostile input (" + Hostile.Length + " values into every box)");
            foreach (var t in Tools2().Where(x => x.Do != null && x.Inputs.Length > 0 && !ByHand.Contains(x.Id)))
            {
                if (late()) { sb.AppendLine("      (time cap reached - rest skipped)"); break; }
                var worst = new Probe(); long total = 0; int tries = 0; double mem = 0; int hd = 0, gd = 0;
                for (int box = 0; box < t.Inputs.Length; box++)
                    foreach (var h in Hostile)
                    {
                        var a = new string[4]; for (int i = 0; i < 4; i++) a[i] = i == box ? h : i == 0 ? "127.0.0.1" : "";   // other boxes: loopback, so nothing leaves the PC
                        if (box == 0 && TargetTools.Contains(t.Id) && h.Length > 0 && h.Trim().Length == 0) continue;
                        var p = RunOne(t, a, 3000); total += p.Ms; tries++; mem += p.MemMb; hd += p.Handles; gd += p.Gdi;
                        if (p.Status != "OK" && p.Status != "SLOW (stopped at the limit)" && worst.Status == "OK") { worst = p; worst.Status += "  [box " + (box + 1) + " = " + Draw.Trunc(h.Replace("\u0000", "\\0"), 30) + "]"; }
                    }
                worst.Ms = total; worst.Rows = tries; worst.MemMb = mem; worst.Handles = hd; worst.Gdi = gd; rec(t.Id + " × " + tries + " inputs", worst);
            }

            // 2. the safe tools for real
            head("2. Real run" + (net ? " (local + light network)" : " (local only)"));
            var inputs = new Dictionary<string, string[]> {
                { "fw", new[] { "teams", "", "" } }, { "events", new[] { "", "", "" } }, { "subnet", new[] { "10.1.2.3/22", "10.1.3.9", "" } }, { "route", new[] { "8.8.8.8", "", "" } }, { "hosts", new[] { "", "", "" } } };
            if (net)
                foreach (var kv in new Dictionary<string, string[]> {
                    { "port", new[] { gw + ":80", "", "" } }, { "tcpping", new[] { gw + ":80", "5", "" } }, { "ping", new[] { gw, "20", "200" } }, { "dig", new[] { "github.com", "A", "" } },
                    { "http", new[] { "github.com", "", "" } }, { "httptime", new[] { "www.microsoft.com", "3", "" } }, { "proxy", new[] { "https://outlook.office.com", "", "" } }, { "ip46", new[] { "www.google.com", "", "" } },
                    { "ret", new[] { "github.com", "", "" } }, { "ping6", new[] { "www.google.com", "", "" } }, { "ntp", new[] { "time.windows.com", "", "" } } }) inputs[kv.Key] = kv.Value;
            var netNoInput = new HashSet<string> { "dnsrules", "teams", "clock", "cloud", "doh", "hairpin" };
            foreach (var t in Tools2().Where(x => x.Do != null && !ByHand.Contains(x.Id) && !LabGate.Contains(x.Id)))   /* senders: hostile input only (to loopback), never a real run */
            {
                if (late()) { sb.AppendLine("      (time cap reached - rest skipped)"); break; }
                string[] a;
                if (t.Inputs.Length == 0) { if (netNoInput.Contains(t.Id) && !net) continue; a = new string[3]; }
                else if (!inputs.TryGetValue(t.Id, out a)) continue;
                rec(t.Id, RunOne(t, a, t.Id == "space" || t.Id == "gpo" || t.Id == "battery" || t.Id == "cloud" ? 45000 : 25000));
            }

            // 3. the same quick tools 15 times each - memory, handles and threads must come back down
            head("3. Repeat (leaks)");
            // the tools that grew most in a single run are repeated too: growth once is a cache, growth every time is a leak
            foreach (var id in net ? new[] { "listen", "certs", "gateway", "v6routers", "overlap", "pressure", "route", "http", "doh", "events", "fw", "gpo", "clock", "ip46", "disk", "bg" } : new[] { "listen", "certs", "gateway", "v6routers", "overlap", "pressure", "route", "events", "fw", "gpo", "disk", "bg" })
            {
                if (late()) { sb.AppendLine("      (time cap reached - rest skipped)"); break; }
                var t = Tools2().FirstOrDefault(x => x.Id == id); if (t == null) continue;
                var a = id == "route" ? new[] { "8.8.8.8", "", "" } : id == "http" ? new[] { "github.com", "", "" } : id == "ip46" ? new[] { "www.google.com", "", "" } : id == "fw" ? new[] { "teams", "", "" } : new[] { "", "", "" };
                RunOne(t, a, 20000); GC.Collect(); GC.WaitForPendingFinalizers(); me.Refresh(); long m0 = me.PrivateMemorySize64; int h0 = me.HandleCount, g0 = Native.GetGuiResources(Native.GetCurrentProcess(), 0), th0 = me.Threads.Count;
                var sw = Stopwatch.StartNew(); string st = "OK";
                for (int i = 0; i < 15; i++) { var p = RunOne(t, a, 20000); if (p.Status != "OK") { st = p.Status; break; } }
                GC.Collect(); GC.WaitForPendingFinalizers(); Thread.Sleep(300); me.Refresh();
                var r = new Probe { Ms = sw.ElapsedMilliseconds, Rows = 15, MemMb = (me.PrivateMemorySize64 - m0) / 1048576.0, Handles = me.HandleCount - h0, Gdi = Native.GetGuiResources(Native.GetCurrentProcess(), 0) - g0, Threads = me.Threads.Count - th0, Status = st };
                if (r.Status == "OK" && (r.Handles > 40 || r.Gdi > 10 || r.MemMb > 12)) r.Status = "GROWS - possible leak";
                rec(id + " × 15", r);
            }

            // 4. the Toolbox UI: open every tool, search, fold, a 600-row result, every section
            head("4. Toolbox and sections under stress");
            Action<string, Action, long> ui = (w, act, limit) =>
            {
                var p = new Probe(); var sw = Stopwatch.StartNew();
                try { act(); } catch (Exception e) { p.Status = "THREW " + e.GetType().Name + ": " + Draw.Trunc(e.Message, 80) + " at " + Draw.Trunc((e.StackTrace ?? "").Trim().Split('\n')[0].Trim(), 120); }
                p.Ms = sw.ElapsedMilliseconds; if (p.Status == "OK" && p.Ms > limit) p.Status = "SLOW (over " + limit + " ms)"; rec(w, p);
            };
            int gdiBefore = Native.GetGuiResources(Native.GetCurrentProcess(), 0), userBefore = Native.GetGuiResources(Native.GetCurrentProcess(), 1);
            ui("open every tool (" + Tools2().Count + ") - its boxes are the ones on screen", () =>
            {
                foreach (var t in Tools2())
                {
                    tbLast.Remove(t.Id); SelectTool(t.Id); tbCanvas.Refresh(); tbList.Refresh(); Application.DoEvents();
                    if (tbBoxes.Count != t.Inputs.Length) throw new InvalidOperationException(t.Id + ": " + tbBoxes.Count + " boxes for " + t.Inputs.Length + " inputs");
                    for (int i = 0; i < t.Inputs.Length; i++)
                    {
                        var b = tbBoxes[i]; if (t.Inputs[i] == "") { if (b != null) throw new InvalidOperationException(t.Id + ": a box where there should be none"); continue; }
                        if (b == null || b.IsDisposed || b.Parent != tbBar || !b.Visible || (b.Tag as string) != t.Inputs[i] || b.Label == "") throw new InvalidOperationException(t.Id + ": box " + (i + 1) + " isn't the one on screen");
                    }
                    if (tbPool.Count(b => b.Visible) != tbBoxes.Count(b => b != null)) throw new InvalidOperationException(t.Id + ": a leftover box is still showing");
                }
                // typing goes where the tool reads it
                SelectTool("subnet"); tbBoxes[0].Box.Text = "10.9.8.7/24"; if (Inputs()[0] != "10.9.8.7/24") throw new InvalidOperationException("the tool doesn't read the box you typed in");
                SelectTool("radius"); if (!tbBoxes[1].Box.UseSystemPasswordChar) throw new InvalidOperationException("the RADIUS secret isn't masked");
                SelectTool("subnet"); if (tbBoxes[0].Box.UseSystemPasswordChar || tbBoxes[0].Box.Text != "") throw new InvalidOperationException("a box kept the secret's mask or old text");
                // a company sweep needs the tick, which never carries over; iperf's plain controls become the right options
                SelectTool("sweep"); if (!tbSweepOk.Visible || tbSweepOk.Checked) throw new InvalidOperationException("the sweep tick isn't showing, or starts ticked");
                tbSweepOk.Checked = true; SelectTool("subnet"); if (tbSweepOk.Visible) throw new InvalidOperationException("the sweep tick shows on another tool");
                SelectTool("sweep"); if (tbSweepOk.Checked) throw new InvalidOperationException("the sweep tick stayed ticked");
                if (IperfOpts(new[] { "s", "download", "15", "-P 4" }) != "-R -t 15 -P 4" || IperfOpts(new[] { "s", "both", "10", "" }) != "--both -t 10" || IperfOpts(new[] { "s", "upload", "999", "" }) != "-t 60" || IperfOpts(new[] { "s", "both", "10", "-R -t 5" }) != "-R -t 5") throw new InvalidOperationException("iperf's plain controls give the wrong options");
                // search ranks: the everyday word's own tool comes first, so Enter opens the right one
                foreach (var q in new[] { "wifi:roams", "nslookup:dig", "traceroute:mtr", "printers:sweep", "ospf:rlisten", "bufferbloat:bloat", "mtu:mtu" })
                { var qa = q.Split(':'); tbFilter = qa[0]; var mm = Matches(); if (mm.Count == 0 || mm[0].Id != qa[1]) throw new InvalidOperationException("searching " + qa[0] + " opens " + (mm.Count > 0 ? mm[0].Id : "nothing") + ", not " + qa[1]); }
                if (DidYouMean("travceroute") != "traceroute" || DidYouMean("snmpp") != "snmp") throw new InvalidOperationException("did-you-mean misses: " + DidYouMean("travceroute") + " / " + DidYouMean("snmpp"));
                tbFilter = "";
                if (Duration("iperf", new[] { "s", "both", "", "-t20 -O3" }) != "about 48 seconds" || Duration("iperf", new[] { "s", "download", "15", "" }) != "about 17 seconds") throw new InvalidOperationException("iperf duration wrong: " + Duration("iperf", new[] { "s", "both", "", "-t20 -O3" }));
                if (IperfOpts(new[] { "s", "sideways", "10", "" }) != null) throw new InvalidOperationException("an unknown iperf direction is guessed instead of refused");
                MoveTool(1); MoveTool(-1);
            }, Tools2().Count * 120);
            ui("open every tool again - GDI / USER objects must not grow", () =>
            {
                int g1 = Native.GetGuiResources(Native.GetCurrentProcess(), 0), u1 = Native.GetGuiResources(Native.GetCurrentProcess(), 1);
                foreach (var t in Tools2()) { SelectTool(t.Id); tbCanvas.Refresh(); Application.DoEvents(); }
                int g2 = Native.GetGuiResources(Native.GetCurrentProcess(), 0) - g1, u2 = Native.GetGuiResources(Native.GetCurrentProcess(), 1) - u1;
                sb.AppendLine("      first pass built GDI " + (g1 - gdiBefore) + " / USER " + (u1 - userBefore) + " (input boxes, made once) · second pass GDI " + g2.ToString("+0;-0", CultureInfo.InvariantCulture) + " / USER " + u2.ToString("+0;-0", CultureInfo.InvariantCulture));
                if (g2 > 5 || u2 > 5) throw new InvalidOperationException("switching tools keeps creating window objects");
            }, Tools2().Count * 120);
            ui("search: type 12 words letter by letter", () => { foreach (var w in new[] { "mtu", "dns", "radius", "slow", "vpn", "teams", "disk", "zzzz", "route", "time", "😀", "a b c" }) { foreach (var c in w) { tbSearch.Box.Text += c; tbList.Refresh(); } tbSearch.Box.Text = ""; } }, 4000);
            ui("fold and unfold every group 5 times", () => { var grps = Tools2().Select(x => x.Group).Distinct().ToList(); for (int i = 0; i < 5; i++) foreach (var g in grps) { if (!tbOpen.Remove(g)) tbOpen.Add(g); tbList.Refresh(); } }, 3000);
            ui("paint a 600-row result 5 times", () =>
            {
                var rows = new List<string[]>(); for (int i = 0; i < 600; i++) rows.Add(new[] { (i % 4).ToString(CultureInfo.InvariantCulture), "Row " + i, "value " + new string('x', i % 90) + " end", i % 3 == 0 ? "advice text that wraps over a couple of lines when the window is narrow " + i : "", "m" });
                tbRows = rows; tbAt = DateTime.Now; for (int i = 0; i < 5; i++) tbCanvas.Refresh(); tbRows = null;
            }, 5000);
            ui("compare view with changed rows", () =>
            {
                tbSel = "subnet"; var r1 = new List<string[]> { new[] { "3", "A", "one", "", "m" } }; var r2 = new List<string[]> { new[] { "3", "A", "two", "", "m" }, new[] { "1", "B", "new", "", "m" } };
                tbPrev["subnet"] = new PrevRun { Inputs = "k", At = DateTime.Now.AddMinutes(-5), Rows = r1 }; tbRows = r2; tbRowsKey = "k"; tbCompare = true; tbCanvas.Refresh(); tbCompare = false; tbPrev.Remove("subnet"); tbRows = null;
            }, 2000);
            ui("every section 3 times", () => { for (int i = 0; i < 3; i++) foreach (var sc in Secs) { tab = sc[2]; ShowTab(); Refresh(); Application.DoEvents(); } tab = "toolbox"; ShowTab(); }, 3 * Secs.Length * 400);

            // summary
            GC.Collect(); GC.WaitForPendingFinalizers(); Thread.Sleep(500); me.Refresh();
            head("Summary");
            sb.AppendLine("since start: memory " + ((me.PrivateMemorySize64 - mStart) / 1048576.0).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " MB · handles " + (me.HandleCount - hStart).ToString("+0;-0", CultureInfo.InvariantCulture) + " · GDI " + (Native.GetGuiResources(Native.GetCurrentProcess(), 0) - gStart).ToString("+0;-0", CultureInfo.InvariantCulture) + " · threads " + (me.Threads.Count - tStart).ToString("+0;-0", CultureInfo.InvariantCulture) + " · now " + (me.PrivateMemorySize64 / 1048576) + " MB");
            sb.AppendLine("checked by hand (needs your gear or acts on the network): " + string.Join(", ", ByHand.OrderBy(x => x)));
            // a run cut short by the time cap is never a pass - it says what it didn't get to
            sb.AppendLine(fails > 0 ? fails + " FAILED - in " + (int)all.Elapsed.TotalSeconds + " s" + (skipped ? " (and the time cap skipped the rest)" : "")
                : skipped ? "INCOMPLETE - the " + capMinutes + "-minute cap was reached and later checks were skipped (see \"rest skipped\" above). Not a pass: run again with --minutes " + Math.Min(60, capMinutes * 2) + "."
                : "ALL PASSED in " + (int)all.Elapsed.TotalSeconds + " s");
            return sb.ToString();
        }
    }
}
