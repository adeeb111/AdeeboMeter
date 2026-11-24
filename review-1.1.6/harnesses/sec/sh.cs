using System; using System.Reflection; using System.Threading; using System.Drawing; using System.Diagnostics;
class P {
 static Assembly A; static Type T(string n){ foreach (var t in A.GetTypes()) if (t.Name==n) return t; return null; }
 static void Main(string[] a) {
  A = Assembly.LoadFrom("r6.dll");
  foreach (var t in A.GetTypes()) foreach (var nm in new[]{"UiFam","UiSemi","UiDisp"}) { var f = t.GetField(nm, BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic); if (f != null && f.FieldType==typeof(string)) f.SetValue(null, "DejaVu Sans"); }
  var draw = T("Draw"); var di = draw.GetMethod("Init", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic); try { di.Invoke(null, new object[]{1f}); } catch (Exception e) { Console.WriteLine("draw init " + e.InnerException); }
  foreach (var t in A.GetTypes()) foreach (var nm in new[]{"UiFam","UiSemi","UiDisp"}) { var f = t.GetField(nm, BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic); if (f != null && f.FieldType==typeof(string)) f.SetValue(null, "DejaVu Sans"); }
  draw.GetField("fName", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null, null); try { di.Invoke(null, new object[]{1f}); } catch (Exception e) { Console.WriteLine("draw init2 " + e.InnerException); }
  var mon = Activator.CreateInstance(T("Monitor"), true);
  var fvT = T("FullView");
  object fv = fvT.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic, null, new[]{typeof(Icon), T("Monitor")}, null).Invoke(new object[]{ SystemIcons.Application, mon });
  var ts = fvT.GetMethod("TestShell", BindingFlags.Instance|BindingFlags.NonPublic, null, new[]{typeof(string), typeof(int).MakeByRefType()}, null);
  var rs = fvT.GetMethod("TestShellReset", BindingFlags.Instance|BindingFlags.NonPublic);
  if (a[0]=="remote") {
   var ra = fvT.GetMethod("RemoteAllowed", BindingFlags.Instance|BindingFlags.NonPublic);
   foreach (var id in new[]{"chain","icmpexplain","certcmds","netsnap","http","speedtest","board","tcpping","ldapcheck","rdpcheck","certcmp","shell","udpsend","sweep","iperf","fw","listen","events"}) Console.WriteLine("RemoteAllowed(" + id + ") = " + ra.Invoke(fv, new object[]{id}));
   var ct = T("CertTools"); var inp = ct.GetMethod("Input", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
   foreach (var p in new[]{ @"\\attacker\share\c.pem", "//attacker/share/c.pem", "/\\attacker/share/c.pem" }) { var rows = new System.Collections.Generic.List<string[]>(); var args = new object[]{ p, rows, null }; inp.Invoke(null, args); foreach (var r in rows) Console.WriteLine("CertTools.Input(" + p + ") -> " + string.Join(" | ", r)); }
   var rp = T("Replay"); var ie = rp.GetMethod("IcmpExplain", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
   foreach (var p in new[]{ @"\\attacker\share\x.pcap", "/etc/passwd", "/etc/nonexistent" }) { var rows = (System.Collections.Generic.List<string[]>)ie.Invoke(null, new object[]{p}); foreach (var r in rows) Console.WriteLine("IcmpExplain(" + p + ") -> " + string.Join(" | ", r)); }
   Environment.Exit(0); }
  var lines = System.IO.File.ReadAllLines(a[0]);
  int lim = a.Length > 1 ? int.Parse(a[1]) : 20;
  foreach (var l in lines) {
   if (l.Trim()=="" || l.StartsWith("#")) continue;
   string got = null; Exception ex = null; int exit = -9;
   rs.Invoke(fv, null);
   var sw = Stopwatch.StartNew();
   var th = new Thread(() => { try { var args = new object[]{ l, 0 }; got = (string)ts.Invoke(fv, args); exit = (int)args[1]; } catch (Exception e) { ex = e.InnerException ?? e; } }, 256*1024*1024) { IsBackground = true };
   th.Start(); bool fin = th.Join(lim*1000);
   GC.Collect(); long mem = Process.GetCurrentProcess().WorkingSet64/1048576;
   string g = got == null ? "" : got.Replace("\n","\\n"); if (g.Length > 300) g = g.Substring(0,150) + " ...[" + got.Length + " chars]... " + g.Substring(g.Length-120);
   Console.WriteLine("CMD: " + l + "\n  " + (fin ? "done " + sw.ElapsedMilliseconds + "ms exit=" + exit : "TIMEOUT >" + lim + "s") + " ws=" + mem + "MB " + (ex != null ? "THREW " + ex.GetType().Name + ": " + ex.Message : "") + "\n  OUT: " + g);
   if (!fin) { Console.WriteLine("  (abandoning thread)"); }
  }
  Environment.Exit(0);
 }}
