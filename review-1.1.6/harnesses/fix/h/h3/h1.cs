using System; using System.Reflection; using System.Linq; using System.IO; using System.Collections; using System.Collections.Generic; using System.Runtime.Serialization; using System.Net;
class P {
 static Assembly A; static Type Ty(string n){ return A.GetType("AdeeboMeter."+n,true); }
 static object Call(string t,string m,params object[] a){ var mi=Ty(t).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).First(x=>x.Name==m&&x.GetParameters().Length==a.Length); try { return mi.Invoke(null,a);} catch(TargetInvocationException e){ throw e.InnerException; } }
 static void Try(string label, Func<string> f){ try { Console.WriteLine(label+" -> "+f()); } catch(Exception e){ Console.WriteLine(label+" THREW "+e.GetType().Name+": "+e.Message); } }
 static string Rows(object o){ return string.Join(" || ", ((IEnumerable)o).Cast<string[]>().Select(r=>string.Join("|",r.Take(3)))); }
 static object NewRun(){ return Activator.CreateInstance(Ty("Toolkit").GetNestedType("Run")); }
 static void Main(string[] a){
  A=Assembly.LoadFrom(a[0]); var bf=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  var M=FormatterServices.GetUninitializedObject(Ty("Monitor")); Ty("Monitor").GetField("L",bf).SetValue(M,new object()); Ty("Monitor").GetField("DnsServer",bf).SetValue(M,"127.0.0.1");
  foreach (var s in new[]{"4294967295","010.0.0.1","0x7f.1","192.168.001.010","::ffff:10.0.0.1","https://4294967295/"}) Try("Icmp.Resolve4("+s+")", ()=>""+Call("Icmp","Resolve4",s));
  foreach (var s in new[]{"010.1.2.3/24","192.168.001.010/24","0x0a.1.2.3/8"}) Try("Subnet("+s+")", ()=>Rows(Call("Toolkit","Subnet",s,"")).Substring(0,120));
  foreach (var s in new[]{"[2001:db8::1]","[2001:db8::1]:3478","stun.example.com","host:80:90","[::1]x"}) { var args=new object[]{s,3478,null,null}; Try("Lab.Split("+s+")", ()=>{ var r=Ty("Lab").GetMethod("Split").Invoke(null,args); return r+" host="+args[2]+" port="+args[3]; }); }
  foreach (var s in new[]{"bücher.de","http://bücher.de/","http://010.1.1.1/","http://0x7f.1/","http://[::ffff:10.1.1.1]/","1.1.1.1:853","[fe80::1%eth0]:8443"}) { var args=new object[]{s,null,null,null,null}; Try("DestCheck.Parse("+s+")", ()=>{ var r=Ty("DestCheck").GetMethod("Parse").Invoke(null,args); return r+" host="+args[1]+" port="+args[2]+" tls="+args[3]+" err="+args[4]; }); }
  Try("Lab.Icmp opts 'size ١٠٠'", ()=>Rows(Call("Lab","Icmp",null,"127.0.0.1","1","size ١٠٠","yes",NewRun())));
  Try("Lab.Icmp opts '-l 1472 -f -i 5'", ()=>Rows(Call("Lab","Icmp",null,"127.0.0.1","1","-l 1472 -f -i 5","yes",NewRun())));
  Try("ManyPorts '٨٠'", ()=>Rows(Call("MoreTools","ManyPorts",null,"127.0.0.1","٨٠","yes",NewRun())));
  Try("ManyPorts '8000-8010'", ()=>Rows(Call("MoreTools","ManyPorts",null,"127.0.0.1","8000-8010","yes",NewRun())));
  // ParseRootDse crafted
  { var b=new byte[16384]; var t=System.Text.Encoding.ASCII.GetBytes("defaultNamingContext"); int i=16384-24; b[i]=4; b[i+1]=(byte)t.Length; Array.Copy(t,0,b,i+2,t.Length); b[i+2+t.Length]=0x31; b[i+3+t.Length]=0xFF;
    Try("ParseRootDse(crafted 16 KB tail)", ()=>{ var d=(IDictionary)Call("MoreTools","ParseRootDse",b,b.Length); return d.Count+" entries"; }); }
  Try("TrapPacket ticks=200", ()=>BitConverter.ToString((byte[])Call("Lab","TrapPacket","public",(uint)200,1,"x")));
  Try("TrapPacket ticks=9000000", ()=>BitConverter.ToString((byte[])Call("Lab","TrapPacket","public",(uint)9000000,1,"x")));
  Try("ClassifyPortal chunked", ()=>{ var args=new object[]{"HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n16\r\nMicrosoft Connect Test\r\n0\r\n\r\n",null}; var r=Ty("DestCheck").GetMethod("ClassifyPortal").Invoke(null,args); return r+" (0=open 1=portal)"; });
  Try("Twamp to 224.0.0.1 (10 pkts)", ()=>Rows(Call("Protocols","Twamp","224.0.0.1:862","10",NewRun())));
  Try("Syslog to 224.0.0.1", ()=>Rows(Call("Protocols","Syslog","224.0.0.1","x","")));
  Try("SafeUrl up http://127.0.0.1.nip.io/", ()=>""+Call("SpeedTest","SafeUrl","http://127.0.0.1.nip.io/upload"));
  Try("Dig a..b", ()=>Rows(Call("Toolkit","Dig","a..b","A","127.0.0.1",M)));
  Try("Dig 70-char label", ()=>Rows(Call("Toolkit","Dig",new string('a',70)+".com","A","127.0.0.1",M)));
  Try("Wake 'ab:cd:ef:01:23:45 (laptop)'", ()=>Rows(Call("Toolkit","Wake","ab:cd:ef:01:23:45 (laptop)","")));
  Try("Uri.ToString userinfo", ()=>new Uri("https://admin:s3cret@example.com/api").ToString());
 }}
