using System; using System.Reflection; using System.Collections.Generic;
class X { static void Main(string[] a){
 var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 var P=A.GetType("AdeeboMeter.Policy");
 foreach (var l in new[]{"internet_check = off","internet_check = disabled","internet_check = \"off\"","internet_check: off","internet_check = off;","Internet_Check = OFF","speed_test = disable"}) {
   P.GetMethod("Reset",BF).Invoke(null,null); P.GetMethod("Parse",BF).Invoke(null,new object[]{new List<string>{l}});
   Console.WriteLine(string.Format("{0,-28} -> NoInternetCheck={1} NoSpeed={2}", l, P.GetField("NoInternetCheck",BF).GetValue(null), P.GetField("NoSpeed",BF).GetValue(null)));
 }}}
