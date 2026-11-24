using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Net;
class Isp { static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n,true); }
 const BindingFlags ALL=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static object Call(string t,string m,params object[] a){ var x=T(t).GetMethods(ALL).First(z=>z.Name==m&&z.GetParameters().Length==a.Length); try{return x.Invoke(null,a);}catch(TargetInvocationException e){throw e.InnerException;} }
 static void S(object o,string f,object v){ o.GetType().GetField(f,ALL).SetValue(o,v); }
 static object G(object o,string f){ return o.GetType().GetField(f,ALL).GetValue(o); }
 static object Hop(int n,string spec){ var p=spec.Split(':'); var h=Activator.CreateInstance(T("PathTools+Hop")); string ip=p[0]=="-"?"":p[0]; int sent=int.Parse(p[1]), lost=int.Parse(p[2]);
   S(h,"N",n);S(h,"Ip",ip);S(h,"Sent",sent);S(h,"Lost",lost);S(h,"Reached",p.Length>3&&p[3]=="R"); if(sent>lost){S(h,"Sum",(double)(sent-lost)*10);S(h,"Best",10.0);S(h,"Worst",20.0);} return h; }
 static IList Hops(string s){ var lt=typeof(List<>).MakeGenericType(T("PathTools+Hop")); var l=(IList)Activator.CreateInstance(lt); var hs=s.Split(' '); for(int i=0;i<hs.Length;i++) l.Add(Hop(i+1,hs[i])); return l; }
 static bool Priv(IPAddress a){ return (bool)Call("IspReport","Private",a); }
 static string Ip(object h){ return (string)G(h,"Ip"); }
 // exact copy of IspReport.Run lines 92-106 (firstIsp + finding)
 static void Finding(string name,string gwIp,string hops,bool wifi){
   var l=Hops(hops); var H=l.Cast<object>().ToList();
   int gwIdx=H.FindIndex(h=>Ip(h)!=""&&Ip(h)==gwIp);
   int firstIsp= gwIdx<0 ? H.FindIndex(h=>{IPAddress a; return IPAddress.TryParse(Ip(h),out a)&&!Priv(a);})
     : H.FindIndex(gwIdx+1,h=>{IPAddress a; if(!IPAddress.TryParse(Ip(h),out a)) return false; var b=a.GetAddressBytes(); return !(b.Length==4&&b[0]==192&&b[1]==168)&&!IPAddress.IsLoopback(a);});
   int start=(int)Call("IspReport","StartOfLoss",l);
   string f;
   if(start>=0&&firstIsp>=0&&start>=firstIsp) f="Loss starts at hop "+(start+1)+", inside or past the provider's network ... The home network was clean.";
   else if(start>=0) f="Loss starts at hop "+(start+1)+" - before the provider ("+(wifi?"Wi-Fi or the router":"the home network")+"). Fix the home side first.";
   else f="(no loss / dest-only)";
   Console.WriteLine(name+"\n   gw="+gwIp+" hops: "+hops+"\n   firstIsp=hop "+(firstIsp+1)+" start=hop "+(start+1)+"\n   FINDING: "+f);
 }
 static void Main(string[] a){ A=Assembly.LoadFrom(a[0]);
  Finding("I1 router silent in trace (no TTL-exceeded), loss starts at provider CGNAT hop","192.168.1.1","-:120:120 100.64.0.1:120:8 10.20.0.1:120:8 203.0.113.1:120:8 198.51.100.9:120:8:R",false);
  Finding("I2 full-tunnel VPN (router not on path), loss starts at the VPN server 10.8.0.1","192.168.1.1","10.8.0.1:120:10 10.8.0.254:120:10 203.0.113.1:120:10 198.51.100.9:120:10:R",true);
  Finding("I3 provider uses 172.16 + CGNAT after the router, loss starts at 172.16","192.168.1.1","192.168.1.1:120:0 172.16.5.1:120:8 100.64.3.1:120:8 203.0.113.1:120:8 198.51.100.9:120:8:R",false);
  Finding("I4 router answers TTL-exceeded from another of its addresses (192.168.0.1 vs gateway 192.168.1.1), loss at CGNAT hop","192.168.1.1","192.168.0.1:120:0 100.64.0.1:120:8 10.20.0.1:120:8 203.0.113.1:120:8 198.51.100.9:120:8:R",false);
  Finding("I5 home router itself loses 10% to the end (cable)","192.168.1.1","192.168.1.1:120:12 100.64.0.1:120:12 203.0.113.1:120:12 198.51.100.9:120:12:R",false);
  // Message: on a cable with loss from the home router
  var rt=T("IspReport+Route"); var rl=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rt));
  var r0=Activator.CreateInstance(rt,true); S(r0,"Label","game.example (EU)"); S(r0,"Host","203.0.113.77"); S(r0,"Hops",Hops("192.168.1.1:120:12 100.64.0.1:120:12 203.0.113.1:120:12 198.51.100.9:120:12:R")); rl.Add(r0);
  Func<string,string> nm=s=>s; var heat=new Dictionary<int,double>();
  Console.WriteLine("\nM1 message, cable, start = hop 1 (home router):\n   "+Call("IspReport","Message","f",rl,r0,0,1,false,nm,heat));
  // two routes, game server silent to ping (trace keeps one silent hop at the tail), reference 1.1.1.1 clean
  rl.Clear(); var g=Activator.CreateInstance(rt,true); S(g,"Label","Game EU-West"); S(g,"Hops",Hops("192.168.1.1:120:0 100.64.0.1:120:0 203.0.113.1:120:0 198.51.100.1:120:0 -:120:120")); rl.Add(g);
  var rf=Activator.CreateInstance(rt,true); S(rf,"Label","Game EU-Central"); S(rf,"Hops",Hops("192.168.1.1:120:0 100.64.0.1:120:0 203.0.113.1:120:0 192.0.2.10:120:0 192.0.2.11:120:0:R")); rl.Add(rf);
  Console.WriteLine("\nM2 two routes, EU-West server ignores ping (one silent hop kept by Trace), path clean:\n   "+Call("IspReport","Message","f",rl,g,-1,1,false,nm,heat));
  foreach(var r in rl){ var hl=((IList)G(r,"Hops")).Cast<object>().ToList(); var e=hl.LastOrDefault(h=>(int)G(h,"Sent")>0); Console.WriteLine("   SAME TIME row for "+G(r,"Label")+": e=hop "+G(e,"N")+" loss "+(100.0*(int)G(e,"Lost")/(int)G(e,"Sent"))+"% -> level "+Call("IspReport","Lv",100.0*(int)G(e,"Lost")/(int)G(e,"Sent"))); }
  // Trace tail: does Trace keep one silent hop?  (simulate the code's RemoveRange on 4 answering + 6 silent)
  var tl=Enumerable.Range(1,10).Select(i=>i<=4?"x":"").ToList(); var hops=new List<string>(); foreach(var x in tl){ hops.Add(x); if(hops.Count>=6&&hops.Skip(hops.Count-5).All(z=>z=="")){ hops.RemoveRange(hops.Count-4,4); break;} }
  Console.WriteLine("\nTrace tail rule on 4 answering + silent: kept "+hops.Count+" hops, last silent="+(hops.Last()==""));
 }}
