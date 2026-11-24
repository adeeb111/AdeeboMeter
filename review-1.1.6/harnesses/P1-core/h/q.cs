using System; using System.Reflection; using System.Linq; using System.Collections.Generic;
class X { static void Main(string[] a){
 var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 var L=A.GetType("AdeeboMeter.L"); var T=L.GetMethod("T",BF);
 var Li=A.GetType("AdeeboMeter.Lines"); Li.GetField("Persona",BF).SetValue(null,"none"); Li.GetField("Mood",BF).SetValue(null,"plain");
 var Mo=A.GetType("AdeeboMeter.Mood"); var Sn=A.GetType("AdeeboMeter.Snap");
 var quip=Mo.GetMethod("Quip",BF,null,new[]{Sn,typeof(string)},null);
 foreach (var code in new[]{"en","es","ar","bn","fr"}) {
   L.GetMethod("Use",BF).Invoke(null,new object[]{code});
   var s=Activator.CreateInstance(Sn); Sn.GetField("Path").SetValue(s,new[]{0,2,3,3,3}); Sn.GetField("Level").SetValue(s,2);
   Sn.GetField("Headline").SetValue(s, T.Invoke(null,new object[]{"Not connected to any network (no Wi-Fi, no cable, or airplane mode)."}));
   Console.WriteLine(code+": "+quip.Invoke(null,new[]{s,""}));
 }
}}
