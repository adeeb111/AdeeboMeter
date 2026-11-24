using System; using System.Reflection; using System.Collections; using System.Collections.Generic; using System.Linq;
class P { static void Main(){
  var A=Assembly.LoadFrom("/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/build6/r6.dll");
  var cfg=A.GetType("AdeeboMeter.Cfg"); Func<string,object> C=n=>{var f=cfg.GetField(n,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic); if(f!=null) return f.GetValue(null); var p=cfg.GetProperty(n,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic); return p==null?"?":p.GetValue(null);};
  Console.WriteLine("GameFast="+C("GameFast")+" GameBgSec="+C("GameBgSec")+" GameBgServers="+C("GameBgServers")+" GameAuto="+C("GameAuto"));
  var gp=A.GetType("AdeeboMeter.GamePing"); var me=gp.GetField("Me").GetValue(null);
  var games=(IList)A.GetType("AdeeboMeter.Games").GetProperty("All").GetValue(null);
  var mon=new HashSet<string>{"valorant"};
  var plan=(IList)gp.GetMethod("Plan").Invoke(me,new object[]{games,mon,"valorant"});
  foreach(var t in plan){ var s=t.GetType().GetProperty("Item2").GetValue(t); Console.WriteLine(t.GetType().GetProperty("Item3").GetValue(t)+" s  "+s.GetType().GetField("Name").GetValue(s)); }
  Console.WriteLine("MaxServers="+gp.GetProperty("MaxServers").GetValue(null)+" MaxPerSecond="+gp.GetProperty("MaxPerSecond").GetValue(null));
}}
