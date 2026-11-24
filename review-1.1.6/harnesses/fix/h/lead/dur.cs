using System; using System.Reflection;
class P { static void Main(string[] a) {
 var asm = Assembly.LoadFrom(a[0]);
 Type fv = null; foreach (var t in asm.GetTypes()) if (t.Name == "FullView") fv = t;
 var m = fv.GetMethod("Duration", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
 foreach (var s in new[]{"gap 100","gap ١٠٠","every １００"}) {
  try { Console.WriteLine(s + " -> " + m.Invoke(null, new object[]{"icmpb", new[]{"x","10",s}})); }
  catch (TargetInvocationException e) { Console.WriteLine(s + " -> THREW " + e.InnerException.GetType().Name); } }
}}
