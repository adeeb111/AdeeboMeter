using System; using System.Reflection; using System.Linq; using System.Collections;
class P { static void Main(string[] a){
 var A=Assembly.LoadFrom(a[0]); var FV=A.GetType("AdeeboMeter.FullView",true); var BF=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 var fv=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(FV);
 var items=(IList)FV.GetMethod("PtAll",BF).Invoke(fv,null);
 var PT=items[0].GetType(); int sum=0; var groups=new System.Collections.Generic.Dictionary<string,int>();
 foreach(var it in items){ int v=(int)PT.GetField("Value").GetValue(it); string g=(string)PT.GetField("Group").GetValue(it); sum+=v; groups[g]=(groups.ContainsKey(g)?groups[g]:0)+v; }
 var PTS=A.GetType("AdeeboMeter.Points"); var rep=(System.Collections.Generic.HashSet<string>)PTS.GetField("Replay").GetValue(null);
 Console.WriteLine("items="+items.Count+" sum="+sum+" replay="+rep.Count*3+" earnable="+(sum+rep.Count*3)+" Max="+PTS.GetField("Max").GetValue(null));
 foreach(var kv in groups) Console.WriteLine("  "+kv.Key+" "+kv.Value);
}}
