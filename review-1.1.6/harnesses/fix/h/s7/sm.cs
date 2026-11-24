using System; using System.Reflection; using System.Linq;
class S { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var FV=A.GetType("AdeeboMeter.FullView",true); var BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 var SI=FV.GetNestedType("SetItem",BF); var it=Activator.CreateInstance(SI,true);
 SI.GetField("Sec").SetValue(it,"General"); SI.GetField("Label").SetValue(it,"Language"); SI.GetField("Keys").SetValue(it,"language french français langue english"); SI.GetField("Help").SetValue(it,(Func<string>)(()=>"English."));
 var m=FV.GetMethod("SetMatch",BF);
 foreach(var q in new[]{"french","français","fran","langue","FRANÇAIS","1.1.1.1","wi-fi","wifi"}) Console.WriteLine(q.Replace("ç","c~")+" -> "+m.Invoke(null,new object[]{it,q}));
}}
