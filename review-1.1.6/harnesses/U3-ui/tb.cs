using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Runtime.Serialization;
class H { const BindingFlags BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]);
  var L=A.GetType("AdeeboMeter.L"); L.GetMethod("Use",BF).Invoke(null,new object[]{a[1]}); A.GetType("AdeeboMeter.Cfg").GetField("Lang",BF).SetValue(null,a[1]);
  var FV=A.GetType("AdeeboMeter.FullView"); var fv=FormatterServices.GetUninitializedObject(FV);
  var tools=(IList)FV.GetMethod("Tools2",BF).Invoke(fv,null); var TT=FV.GetNestedType("Tool",BF);
  var ms=FV.GetMethod("MatchesScored",BF);
  foreach(var q in a.Skip(2)){ var r=(IList)ms.Invoke(fv,new object[]{q}); Console.WriteLine(q+" -> "+r.Count+": "+string.Join(", ",r.Cast<object>().Take(4).Select(t=>(string)TT.GetField("Id").GetValue(t)))); }
 }}
