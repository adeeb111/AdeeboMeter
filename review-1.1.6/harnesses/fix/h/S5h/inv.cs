using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Diagnostics;
class Inv { static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n,true); }
 const BindingFlags ALL=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static void S(object o,string f,object v){ var fi=o.GetType().GetField(f,ALL); fi.SetValue(o,v); }
 static object G(object o,string f){ return o.GetType().GetField(f,ALL).GetValue(o); }
 static void SS(string t,string f,object v){ T(t).GetField(f,ALL).SetValue(null,v); }
 static object Mon(string gw,string gw6,double[] gwv,double[] netv,string dns,bool vpn,string kind,int sig,double loss){
   var m=Activator.CreateInstance(T("Monitor"),true);
   S(m,"Gateway",gw); S(m,"Gateway6",gw6); S(m,"DnsServer",dns); S(m,"VpnUp",vpn); S(m,"AdapterKind",kind); S(m,"WifiSignal",sig); S(m,"Adapter",kind=="Wi-Fi"?"Wi-Fi":"Ethernet");
   var nic=System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().First(); S(m,"Primary",nic);
   foreach(var v in gwv) T("Ring").GetMethod("Add").Invoke(G(m,"GwPing"),new object[]{v});
   foreach(var v in netv) T("Ring").GetMethod("Add").Invoke(G(m,"NetPing"),new object[]{v});
   for(int i=0;i<10;i++){ T("Ring").GetMethod("Add").Invoke(G(m,"Cpu"),new object[]{10.0}); T("Ring").GetMethod("Add").Invoke(G(m,"Dns"),new object[]{20.0}); }
   var now=(DateTime)T("Clock").GetProperty("Mono").GetValue(null,null); S(m,"GwAt",now); S(m,"netAt",now);
   var snap=G(m,"Latest"); S(snap,"Mem",40.0); S(snap,"LossPct",loss); S(snap,"Cpu",10.0);
   return m; }
 static void Run(string name,string id,object m){
   var r=Activator.CreateInstance(T("GuideRun"),true); var sw=Stopwatch.StartNew();
   T("Investigate").GetMethod("Run",ALL).Invoke(null,new object[]{id,m,r,(Action)delegate{}});
   Console.WriteLine("== "+name+" ["+id+"] "+sw.ElapsedMilliseconds+" ms\n   VERDICT lv="+G(r,"Lv")+": "+G(r,"Finding"));
   foreach(var o in (List<string[]>)T("GuideRun").GetMethod("ObservedCopy").Invoke(r,null)) Console.WriteLine("     obs lv"+o[1]+" "+o[2]+": "+o[3]);
   foreach(var n in (List<string>)G(r,"NotEst")) Console.WriteLine("     not-est: "+n);
 }
 static void Main(string[] a){ A=Assembly.LoadFrom(a[0]); SS("Program","Dir",a[1]);
  var ok=Enumerable.Repeat(20.0,10).ToArray(); var nan=Enumerable.Repeat(double.NaN,10).ToArray(); string dns="127.0.0.53";
  string which=a.Length>2?a[2]:"all";
  Action<string,Action> R=(n,f)=>{ if(which=="all"||which.Split(',').Contains(n)) try{f();}catch(Exception e){Console.WriteLine(n+" HARNESS EXC "+(e.InnerException??e));} };
  R("s1",()=>{ SS("Cfg","Portal",false); Run("S1 router ignores ping+TCP, internet+DNS fine, portal check off","whats",Mon(a.Length>3?a[3]:"192.0.2.1","",nan,ok,dns,false,"LAN",-1,0)); });
  R("s2",()=>{ SS("Cfg","Portal",false); Run("S2 IPv6-only network (no IPv4 gateway), internet fine","whats",Mon("","fe80::1",nan,ok,dns,false,"LAN",-1,0)); });
  R("s3",()=>{ SS("Cfg","Portal",true); Run("S3 all fine, but the sign-in page test cannot connect (proxy needed)","whats",Mon("127.0.0.1","",new double[]{2,2,2,2},ok,dns,false,"LAN",-1,0)); });
  R("s5",()=>{ SS("Cfg","Portal",false); Run("S5 dead primary DNS, secondary works","whats",Mon("127.0.0.1","",ok,ok,"192.0.2.53, "+dns,false,"LAN",-1,0)); });
  R("s6",()=>{ SS("Cfg","Portal",false); Run("S6 DoH: plain DNS to the server fails, Windows resolves","whats",Mon("127.0.0.1","",ok,ok,"192.0.2.53",false,"LAN",-1,0)); });
  R("s7",()=>{ SS("Cfg","Portal",false); Run("S7 weak Wi-Fi 20%","whats",Mon("127.0.0.1","",ok,ok,dns,false,"Wi-Fi",20,0)); });
  R("s8",()=>{ SS("Cfg","Portal",false); Run("S8 full VPN, no local gateway","whats",Mon("","",nan,ok,dns,true,"LAN",-1,0)); });
  R("s9",()=>{ SS("Cfg","Portal",false); Run("S9 4% loss","whats",Mon("127.0.0.1","",new double[]{2,2,2,2,2,2},ok,dns,false,"LAN",-1,4)); });
  R("s10",()=>{ Run("S10 Slow: router ignores ping, internet fine","slow",Mon(a.Length>3?a[3]:"192.0.2.1","",nan,ok,dns,false,"LAN",-1,0)); });
  R("s11",()=>{ SS("Cfg","Portal",true); Run("S11 captive portal: nothing past the router answers, HTTP redirected","whats",Mon("127.0.0.1","",ok,nan,dns,false,"Wi-Fi",70,0)); });
  R("s12",()=>{ SS("Cfg","Portal",false); var m=Mon("127.0.0.1","",ok,ok,dns,false,"LAN",-1,0); Run("S12 Slow, router check OFF (gw ring empty), internet fine","slow",Mon("127.0.0.1","",new double[0],ok,dns,false,"LAN",-1,0)); });
 }}
