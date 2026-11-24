using System; using System.Reflection; using System.Linq; using System.Collections; using System.Runtime.Serialization;
class H { const BindingFlags BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]);
  var FV=A.GetType("AdeeboMeter.FullView"); var fv=FormatterServices.GetUninitializedObject(FV);
  var tools=(IList)FV.GetMethod("Tools2",BF).Invoke(fv,null); var TT=FV.GetNestedType("Tool",BF);
  var sends=FV.GetMethod("ToolSends",BF); var takes=FV.GetMethod("ToolTakes",BF);
  int fb=0,calc=0; foreach(var t in tools){ var id=(string)TT.GetField("Id").GetValue(t); var s=(string)sends.Invoke(null,new[]{t}); if(s.StartsWith("a few small requests")) fb++; if(((string)TT.GetField("Group").GetValue(t)).Contains("MATHS")&&!s.StartsWith("nothing")) calc++; } Console.WriteLine("tools="+tools.Count+" fallback="+fb+" maths-not-nothing="+calc); foreach(var id in a.Skip(1)){ var t=tools.Cast<object>().First(z=>(string)TT.GetField("Id").GetValue(z)==id); Console.WriteLine(id+" ["+TT.GetField("Group").GetValue(t)+"] SENDS: "+sends.Invoke(null,new[]{t})+" | TAKES: "+takes.Invoke(null,new[]{t})); }
 }}
