using System; using System.Reflection; using System.Linq; using System.Collections.Generic; using System.Net.Sockets; using System.Threading;
class Lb { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 var P=A.GetType("AdeeboMeter.Policy"); P.GetField("NoLab",BF).SetValue(null,true);
 int hits=0; var l=new TcpListener(System.Net.IPAddress.Loopback,15201); l.Start(); var th=new Thread(()=>{ try{ while(true){ var c=l.AcceptTcpClient(); Interlocked.Increment(ref hits); c.Close(); } }catch{} }){IsBackground=true}; th.Start();
 var TK=A.GetType("AdeeboMeter.Toolkit"); var RunT=TK.GetNestedType("Run",BF); var run=Activator.CreateInstance(RunT);
 var rows=(List<string[]>)TK.GetMethod("Iperf",BF).Invoke(null,new object[]{"127.0.0.1:15201","-t 1",run});
 Thread.Sleep(300);
 Console.WriteLine("Policy.NoLab=true; iperf connections that reached the server: "+hits);
 foreach(var r in rows.Take(4)) Console.WriteLine("  "+string.Join(" | ",r));
 var FV=A.GetType("AdeeboMeter.FullView"); Console.WriteLine("LabExempt: "+string.Join(",",(HashSet<string>)FV.GetField("LabExempt",BF).GetValue(null)));
}}
