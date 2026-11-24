using System; using System.Linq; using System.Reflection; using System.Collections.Generic;
static class W { static void Main() { var A = Assembly.LoadFrom("r6.dll"); Console.WriteLine("System folder: [" + Environment.GetFolderPath(Environment.SpecialFolder.System) + "]");
 var m = A.GetType("AdeeboMeter.WhyFails").GetMethod("Hosts");
 foreach (var n in new[]{"example.com","example.com:443"}) { var r = (List<string[]>)m.Invoke(null, new object[]{n}); Console.WriteLine(n + " => " + string.Join(" || ", r.Select(x => x[1] + "=" + x[2]))); } } }
