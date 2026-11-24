using System; using System.Reflection; using System.Diagnostics; using System.Windows.Forms;
class P { static void Main() {
 var A = Assembly.LoadFrom("r6.dll"); Type te = null; foreach (var t in A.GetTypes()) if (t.Name=="TermEditor") te = t;
 var ed = te.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic, null, new[]{typeof(bool),typeof(string),typeof(string),typeof(string)}, null).Invoke(new object[]{true, null, null, new string('a',1000)+"\n"});
 var ch = te.GetMethod("Char", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
 var body = te.GetProperty("Body", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
 Action<string> type = s => { foreach (var c in s) ch.Invoke(ed, new object[]{c}); };
 foreach (var step in new[]{"yy","100000p","gg","100000yy","50p"}) {
  var sw = Stopwatch.StartNew();
  try { type(step); Console.WriteLine(step + " ok " + sw.ElapsedMilliseconds + "ms ws=" + Process.GetCurrentProcess().WorkingSet64/1048576 + "MB"); }
  catch (TargetInvocationException e) { Console.WriteLine(step + " THREW " + e.InnerException.GetType().Name + " after " + sw.ElapsedMilliseconds + "ms ws=" + Process.GetCurrentProcess().WorkingSet64/1048576 + "MB"); break; }
 }
}}
