using System; using System.Reflection; using System.Linq; using System.Collections;
class X { static void Main(string[] a){
 var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 var M=A.GetType("AdeeboMeter.Monitor"); var m=Activator.CreateInstance(M);
 var Clock=A.GetType("AdeeboMeter.Clock"); var now=(DateTime)Clock.GetProperty("Mono").GetValue(null);
 // the internet check is off: only the router probe calls WakeStep(t0, gwOk, false)
 M.GetMethod("Resumed").Invoke(m,null);
 var ws=M.GetMethod("WakeStep",BF);
 ws.Invoke(m,new object[]{now.AddSeconds(3),true,false});      // router answers 3 s after waking
 ws.Invoke(m,new object[]{now.AddSeconds(125),true,false});    // a later router probe, past 2 minutes
 var ev=M.GetField("Events").GetValue(m); var rec=(IList)ev.GetType().GetMethod("Recent").Invoke(ev,new object[]{10});
 foreach (var e in rec) { var t=e.GetType(); Console.WriteLine("lv "+t.GetField("Lv").GetValue(e)+" ["+t.GetField("Kind").GetValue(e)+"] "+t.GetField("Text").GetValue(e)); }
}}
