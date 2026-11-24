using System; using System.Reflection;
class X { static void Main(string[] a){
 var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 var Mo=A.GetType("AdeeboMeter.Mood"); var Sn=A.GetType("AdeeboMeter.Snap");
 Func<int[],int,object> snap = (p,l) => { var s=Activator.CreateInstance(Sn); Sn.GetField("Path").SetValue(s,p); Sn.GetField("Level").SetValue(s,l); return s; };
 var quip=Mo.GetMethod("Quip",BF,null,new[]{Sn,typeof(string)},null); var tr=Mo.GetMethod("Track",BF);
 tr.Invoke(null,new[]{snap(new[]{0,0,2,3,3},2)});
 Mo.GetField("downTicks",BF).SetValue(null, DateTime.Now.AddHours(-4).Ticks);
 Console.WriteLine("still down 4h: "+quip.Invoke(null,new[]{snap(new[]{0,0,2,3,3},2),"ExampleNet"}));
 tr.Invoke(null,new[]{snap(new[]{0,0,0,0,0},0)});  // line back (Monitor ticks, nothing drawn)
 tr.Invoke(null,new[]{snap(new[]{0,0,2,3,3},2)});  // new outage
 Console.WriteLine("downSince after fresh outage: "+Mo.GetProperty("downSince",BF).GetValue(null,null)+" now "+DateTime.Now); Console.WriteLine("fresh outage: "+quip.Invoke(null,new[]{snap(new[]{0,0,2,3,3},2),"ExampleNet"}));
}}
