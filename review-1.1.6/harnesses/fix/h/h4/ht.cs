using System; using System.IO; using System.Linq; using System.Reflection; using System.Collections.Generic;
static class P { static void Main(string[] a){ var A=Assembly.LoadFrom(Path.Combine(a[0],"r4.dll")); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 A.GetType("AdeeboMeter.Program").GetField("Dir",BF).SetValue(null,a[0]);
 A.GetType("AdeeboMeter.Cfg").GetField("Portal",BF).SetValue(null,false);
 var run=Activator.CreateInstance(A.GetType("AdeeboMeter.Toolkit+Run"),true);
 var mi=A.GetType("AdeeboMeter.CertChain").GetMethods(BF).First(m=>m.Name=="Https"&&m.GetParameters().Length==2);
 foreach (var h in a.Skip(1)) { Console.WriteLine("== "+h); foreach (var r in (List<string[]>)mi.Invoke(null,new object[]{h,run})) Console.WriteLine("  "+string.Join(" | ",r.Take(3))); } }}
