using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Text;
class F { static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n,true); }
 const BindingFlags ALL=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static MethodInfo M(string t,string m,int n){ return T(t).GetMethods(ALL).First(z=>z.Name==m&&z.GetParameters().Length==n); }
 static Dictionary<string,int> errs=new Dictionary<string,int>(); static Dictionary<string,string> ex1=new Dictionary<string,string>();
 static object Try(string tag, MethodInfo mi, params object[] a){ try{ return mi.Invoke(null,a);}catch(TargetInvocationException e){ var k=tag+": "+e.InnerException.GetType().Name; int c; errs.TryGetValue(k,out c); errs[k]=c+1; if(!ex1.ContainsKey(k)) ex1[k]=e.InnerException.Message+" @ "+(e.InnerException.StackTrace??"").Split('\n').FirstOrDefault(); return null; } }
 static void Main(string[] a){ A=Assembly.LoadFrom(a[0]); var r=new Random(12345); int N=int.Parse(a[1]);
  // pcap: mutate the sample captures and feed every replay
  var build=M("Replay","Build",1); var parse=M("Pcap","Parse",1);
  var samples=new[]{"dhcp-good","dhcp-two-servers","dhcp-nak","eap-failure","eap-good","ra-rogue","icmp-errors"}.Select(s=>(byte[])build.Invoke(null,new object[]{s})).ToList();
  var dh=M("Replay","DhcpRows",2); var ra=M("Replay","RaRows",2); var ea=M("Replay","EapRows",2); var ic=M("Replay","IcmpRowsFromPackets",2);
  for(int i=0;i<N;i++){ var b=(byte[])samples[r.Next(samples.Count)].Clone(); int muts=1+r.Next(20); for(int k=0;k<muts;k++){ int p=r.Next(b.Length); b[p]=(byte)(r.Next(3)==0?0xFF:r.Next(256)); }
    if(r.Next(5)==0) Array.Resize(ref b, r.Next(b.Length+1));
    var pk=Try("Pcap.Parse",parse,b); if(pk==null) continue;
    foreach(var mi in new[]{dh,ra,ea,ic}) Try("Replay."+mi.Name,mi,pk,new List<string[]>()); }
  // pcapng random blocks
  for(int i=0;i<N;i++){ var b=new byte[28+r.Next(400)]; r.NextBytes(b); b[0]=0x0A;b[1]=0x0D;b[2]=0x0D;b[3]=0x0A; Try("Pcap.Parse(ng)",parse,b); var c=(byte[])b.Clone(); c[0]=0xd4;c[1]=0xc3;c[2]=0xb2;c[3]=0xa1; Try("Pcap.Parse(classic)",parse,c); }
  // DHCP direct
  var pd=M("Replay","ParseDhcp",2); for(int i=0;i<N;i++){ var b=new byte[r.Next(300)+240]; r.NextBytes(b); b[236]=99;b[237]=130;b[238]=83;b[239]=99; Try("ParseDhcp",pd,b,0.0); }
  // DNS
  var dp=M("Toolkit","Parse",3); for(int i=0;i<N;i++){ var b=new byte[12+r.Next(600)]; r.NextBytes(b); if(r.Next(2)==0){b[4]=0;b[5]=1;b[6]=0;b[7]=(byte)r.Next(5);} Try("Toolkit.Parse",dp,b,0L,"x"); }
  // SNMP varbinds (Protocols) and Snmp.Parse2/ReadPdu
  var pv=M("Protocols","ParseVarbinds",2); for(int i=0;i<N;i++){ var b=new byte[r.Next(300)]; r.NextBytes(b); if(b.Length>2){b[0]=0x30;} Try("Protocols.ParseVarbinds",pv,b,new List<string>()); }
  var p2=M("Snmp","Parse2",4); var rp=M("Snmp","ReadPdu",5);
  for(int i=0;i<N;i++){ var b=new byte[r.Next(300)]; r.NextBytes(b); if(b.Length>8){b[0]=0x30;b[1]=0x81;b[2]=(byte)(b.Length-3);b[3]=2;b[4]=1;b[5]=1;b[6]=4;b[7]=0;} object[] args={b,0,"",null}; Try("Snmp.Parse2",p2,args); Try("Snmp.ReadPdu",rp,b,0,b.Length,0,null); }
  // Snmp.Text on random values
  var vbt=T("Snmp+Vb"); var tx=M("Snmp","Text",3); for(int i=0;i<N;i++){ var v=Activator.CreateInstance(vbt); var raw=new byte[r.Next(12)]; r.NextBytes(raw); vbt.GetField("Type").SetValue(v,(byte)new[]{2,4,5,6,0x40,0x41,0x42,0x43,0x46,0x80,0x81,0x82,0x03}[r.Next(13)]); vbt.GetField("Raw").SetValue(v,raw); Try("Snmp.Text",tx,v,r.Next(2)==0?"ifPhysAddress":"x",null); }
  // STUN
  var ps=M("Reach","ParseStun",2); var sm=M("Protocols","StunMapped",1); for(int i=0;i<N;i++){ var b=new byte[r.Next(80)]; r.NextBytes(b); if(b.Length>=20){b[0]=1;b[1]=1;b[4]=0x21;b[5]=0x12;b[6]=0xA4;b[7]=0x42; int L=(b.Length-20)&~3; b[2]=(byte)(L>>8); b[3]=(byte)L;} var tid=new byte[12]; if(b.Length>=20) Array.Copy(b,8,tid,0,12); Try("Reach.ParseStun",ps,b,tid); Try("Protocols.StunMapped",sm,b); }
  // text parsers with hostile input
  string[] hostile={"","   ","\0","١٨:٤٠","٣/٤","v6 ٢/٠","99:99","yesterday ","today 24:00","2026-13-45 99:99","9999-12-31 23:59","00:00:00.0000001","-1:00","1e9","NaN","∞","‮18:40","18:4O","ab:cd:ef:01:23:45:67","[::1]:99999","[]","[:","::","%","https://","https:/","http://[::1","1.2.3.4/33","1.2.3.4 255.0.255.0","0x7f.1","010.1.1.1/8","fe80::1%eth0/64","1.2.3.4/٢٤","v3:","v3::","v3-sha512:u:p","community:","v3:user::priv", new string('a',100000), "1."+string.Join(".",Enumerable.Repeat("4294967295",200)), "1.3.6.1.٦","2.999999999999","1.40"};
  var pw=M("Picks","ParseWhen",2); var ie=M("Replay","IcmpExplain",1); var po=M("Toolkit","ParseOpts",1); var sub=M("Toolkit","Subnet",2); var cp=M("Snmp+Cred","Parse",1); var vo=M("Snmp","ValidOid",1); var ob=M("Snmp","OidBytes",1); var ho=M("Toolkit","HostOnly",1); var ut=M("Toolkit","UrlToTarget",2); var hof=M("Security","HostOf",1); var su=M("SpeedTest","SafeUrl",1); var ssp=T("Sectors").GetMethods(ALL).First(z=>z.Name=="Split"&&z.GetParameters().Length==4); var rr=M("Reach","Remote",3); var nm=M("SnmpTools","NormMac",1); var oc=M("Snmp","OidCompare",2);
  foreach(var s in hostile){ object[] pa={s,null}; Try("Picks.ParseWhen",pw,pa); Try("Replay.IcmpExplain",ie,s); Try("Toolkit.ParseOpts",po,s); Try("Toolkit.Subnet",sub,s,s); Try("Snmp.Cred.Parse",cp,s); bool ok=(bool)vo.Invoke(null,new object[]{s}); if(ok) Try("Snmp.OidBytes(after ValidOid)",ob,s); Try("Toolkit.HostOnly",ho,s); Try("Toolkit.UrlToTarget",ut,s,true); Try("Security.HostOf",hof,s); Try("SpeedTest.SafeUrl",su,s); Try("Sectors.Split",ssp,s,443,null,0); Try("Reach.Remote",rr,s,s,s); Try("SnmpTools.NormMac",nm,s); Try("Snmp.OidCompare",oc,s,"1.3"); }
  // random string fuzz for ParseWhen / IcmpExplain / Subnet / ParseOpts
  string alpha="0123456789:./ -yesterdaytoday١٢٣٤٥٦٧٨٩٠hHv6x[]%"; for(int i=0;i<N;i++){ var sb=new StringBuilder(); int L=r.Next(20); for(int k=0;k<L;k++) sb.Append(alpha[r.Next(alpha.Length)]); var s=sb.ToString(); object[] pa={s,null}; Try("Picks.ParseWhen(rand)",pw,pa); Try("Replay.IcmpExplain(rand)",ie,s); Try("Toolkit.Subnet(rand)",sub,s,s); Try("Toolkit.ParseOpts(rand)",po,s); }
  foreach(var kv in errs.OrderBy(z=>z.Key)) Console.WriteLine(kv.Value.ToString().PadLeft(6)+"  "+kv.Key+"   e.g. "+ex1[kv.Key]);
  Console.WriteLine("done");
 }}
