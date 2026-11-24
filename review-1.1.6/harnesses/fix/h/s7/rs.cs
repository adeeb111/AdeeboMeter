using System; using System.Reflection; using System.Linq;
class R { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 var I=A.GetType("AdeeboMeter.Icmp"); var m=I.GetMethod("Resolve4",BF);
 foreach(var s in new[]{"[","]","http://[","https://","http://","[::1","[::1]:x","::",":","a:b:c","%","%%","http://%zz","https://a b","https:///x","http://:80","\u0000","....","-","a..b","999.999.999.999","1.2.3.4:99999","http://[fe80::1%eth0]/","javascript:alert(1)","file:///c:/x","\\\\server\\share","http://user:pass@1.2.3.4/", new string('a',300), "xn--", "http://xn--/", "http://[1:2:3:4:5:6:7:8:9]/"}){
  try{ var r=m.Invoke(null,new object[]{s}); Console.WriteLine("ok   "+s.Replace("\u0000","\\0")+" -> "+(r??"null")); } catch(TargetInvocationException e){ Console.WriteLine("THREW "+s+" -> "+e.InnerException.GetType().Name+": "+e.InnerException.Message); } }
}}
