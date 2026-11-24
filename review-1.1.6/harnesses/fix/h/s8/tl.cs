using System; using System.Reflection; using System.Linq; using System.Collections; using System.Runtime.Serialization;
class P{static void Main(string[] a){var A=Assembly.LoadFrom(a[0]);var bf=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
var FV=A.GetType("AdeeboMeter.FullView",true);var fv=FormatterServices.GetUninitializedObject(FV);
var tools=((IEnumerable)FV.GetMethod("Tools2",bf).Invoke(fv,null)).Cast<object>();
foreach(var t in tools){var ty=t.GetType();Func<string,object> g=n=>{var f=ty.GetField(n,bf);return f!=null?f.GetValue(t):ty.GetProperty(n,bf).GetValue(t,null);};
Console.WriteLine(g("Id")+"\t"+((string[])g("Inputs")).Length+"\t"+g("Title"));}}}
