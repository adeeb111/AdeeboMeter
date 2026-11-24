using System; using System.Reflection; using System.Linq; using System.Collections; using System.Runtime.Serialization;
class S { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var FV=A.GetType("AdeeboMeter.FullView",true); var BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 var fv=FormatterServices.GetUninitializedObject(FV); var items=(IList)FV.GetMethod("SetItems",BF).Invoke(fv,null); var SI=FV.GetNestedType("SetItem",BF); var m=FV.GetMethod("SetMatch",BF);
 Console.WriteLine("rows "+items.Count);
 foreach(var q in new[]{"wi-fi","Wi-Fi","wifi","français","ping","history","dns","1.1.1.1","8.8.8.8","bgp","théme","sign-in","gamer zone","24 h"}){ var hits=items.Cast<object>().Where(it=>(bool)m.Invoke(null,new object[]{it,q})).Select(it=>(string)SI.GetField("Label").GetValue(it)).ToList(); Console.WriteLine(q.Replace("ç","c~").Replace("é","e'")+" -> "+hits.Count+": "+string.Join(" | ",hits.Take(5))); }
}}
