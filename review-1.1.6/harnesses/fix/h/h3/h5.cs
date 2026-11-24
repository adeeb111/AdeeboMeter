using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Diagnostics;
class P { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var T=A.GetType("AdeeboMeter.Toolkit",true); var run=Activator.CreateInstance(T.GetNestedType("Run"));
 var sw=Stopwatch.StartNew(); var r=(IEnumerable)T.GetMethod("Iperf").Invoke(null,new object[]{"127.0.0.1:15201","-P 16 -t 1",run});
 Console.WriteLine(sw.ElapsedMilliseconds+" ms: "+string.Join(" || ", r.Cast<string[]>().Select(x=>string.Join("|",x.Take(3))))); }}
