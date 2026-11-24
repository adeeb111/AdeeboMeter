using System; using System.Reflection; using System.Collections.Generic; using System.Runtime.Serialization;
class P { static void Main(string[] a){
  var A=Assembly.LoadFrom(a[0]); var bf=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  var MT=A.GetType("AdeeboMeter.Monitor",true); object M;
  try { M=Activator.CreateInstance(MT,true); } catch(Exception e){ Console.WriteLine("ctor: "+e.InnerException); return; }
  foreach (var case_ in new[]{ new double[]{3,150}, new double[]{3,double.NaN} }) {
    var gw=MT.GetField("GwPing",bf).GetValue(M); var net=MT.GetField("NetPing",bf).GetValue(M);
    gw.GetType().GetMethod("Clear").Invoke(gw,null); net.GetType().GetMethod("Clear").Invoke(net,null);
    for(int i=0;i<10;i++){ gw.GetType().GetMethod("Add").Invoke(gw,new object[]{case_[0]}); if(!double.IsNaN(case_[1])) net.GetType().GetMethod("Add").Invoke(net,new object[]{case_[1]}); }
    MT.GetField("AdapterKind",bf).SetValue(M,"LAN");
    var RunT=A.GetType("AdeeboMeter.Toolkit+Run",true); var run=Activator.CreateInstance(RunT);
    var rows=(List<string[]>)A.GetType("AdeeboMeter.GamerTools",true).GetMethod("PreMatch",bf).Invoke(null,new object[]{M,run});
    Console.WriteLine("== router "+case_[0]+" ms, internet "+case_[1]+" ms");
    foreach(var r in rows) Console.WriteLine("  lv="+r[0]+"  "+r[1]+": "+r[2]+"  | "+r[3]);
  }
}}
