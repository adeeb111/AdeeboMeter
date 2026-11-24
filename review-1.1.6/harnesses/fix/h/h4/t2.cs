using System; using System.IO; using System.Linq; using System.Reflection; using System.Collections.Generic; using System.Diagnostics;
static class Q {
  static void Main(string[] a) {
    var A = Assembly.LoadFrom(Path.Combine(a[0], "r4.dll"));
    A.GetType("AdeeboMeter.Program").GetField("Dir", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null, a[0]);
    var run = Activator.CreateInstance(A.GetType("AdeeboMeter.Toolkit+Run"), true);
    var mi = A.GetType("AdeeboMeter.FileCrypt").GetMethod("Decrypt", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
    var sw = Stopwatch.StartNew(); object r;
    try { r = mi.Invoke(null, new object[] { a[1], "ABCD-EFGH", run }); foreach (var x in (List<string[]>)r) Console.WriteLine(string.Join(" | ", x)); }
    catch (TargetInvocationException e) { Console.WriteLine("THREW " + e.InnerException.GetType().Name + ": " + e.InnerException.Message); }
    var p = Process.GetCurrentProcess(); p.Refresh();
    Console.WriteLine("peak working set MB " + p.PeakWorkingSet64/1048576 + "  time " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s");
  }
}
