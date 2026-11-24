using System; using System.IO; using System.Reflection; using System.Diagnostics; using System.Collections;
static class Q { static void Main(string[] a) {
  var A = Assembly.LoadFrom(Path.Combine(a[0], "r4.dll"));
  var mi = A.GetType("AdeeboMeter.Pcap").GetMethod("Read", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
  var args = new object[] { a[1], null }; var sw = Stopwatch.StartNew(); var l = (IList)mi.Invoke(null, args);
  var p = Process.GetCurrentProcess(); p.Refresh(); long live = GC.GetTotalMemory(false);
  Console.WriteLine("pkts " + l.Count + " err '" + args[1] + "' managed now MB " + live/1048576 + " peak WS MB " + p.PeakWorkingSet64/1048576 + " " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s");
}}
