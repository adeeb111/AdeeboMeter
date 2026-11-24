using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Diagnostics;
class Fz3 { static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n,true); }
 const BindingFlags ALL=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static MethodInfo M(string t,string m,int n){ return T(t).GetMethods(ALL).First(z=>z.Name==m&&z.GetParameters().Length==n); }
 static void Main(string[] a){ A=Assembly.LoadFrom(a[0]); var rnd=new Random(int.Parse(a[1])); int iters=int.Parse(a[2]);
  var parse=M("Snmp+Cred","Parse",1); var msg3=M("Snmp","Message3",4); var parse3=M("Snmp","Parse3",5); var pdu=M("Snmp","Pdu",5);
  var eng=Activator.CreateInstance(T("Snmp+Engine"),true); T("Snmp+Engine").GetField("Id").SetValue(eng,new byte[]{0x80,0,0,9,3,1,2,3,4}); T("Snmp+Engine").GetField("At").SetValue(eng,DateTime.UtcNow); T("Snmp+Engine").GetField("Boots").SetValue(eng,3); T("Snmp+Engine").GetField("Time").SetValue(eng,1000);
  var crashes=new Dictionary<string,int>(); int ok=0;
  foreach(var cs in new[]{"v3:user","v3:user:authpass1","v3-sha256:user:authpass1:privpass1"}){
   var c=parse.Invoke(null,new object[]{cs});
   for(int it=0;it<iters;it++){
    int id=rnd.Next(); var p=(byte[])pdu.Invoke(null,new object[]{(byte)0xA2,id,0,0,new[]{"1.3.6.1.2.1.1.5.0","1.3.6.1.2.1.1.1.0"}});
    var m=(byte[])msg3.Invoke(null,new object[]{c,eng,p,id});
    int nm=rnd.Next(0,4); for(int j=0;j<nm;j++){ int op=rnd.Next(3); int q=rnd.Next(m.Length); if(op==0) m[q]=(byte)rnd.Next(256); else if(op==1) m=m.Take(q).ToArray(); else m[q]^=0x80; if(m.Length==0) break; }
    var args=new object[]{m,c,eng,id,null};
    try{ parse3.Invoke(null,args); ok++; }
    catch(TargetInvocationException e){ var ie=e.InnerException; string k=cs+" "+ie.GetType().Name+": "+ie.Message+" @ "+ie.StackTrace.Split('\n')[0].Trim(); if(!crashes.ContainsKey(k)){ crashes[k]=0; Console.WriteLine("CRASH "+k+"\n  input="+BitConverter.ToString(m)); } crashes[k]++; }
   }
  }
  Console.WriteLine("ok="+ok+" distinct crashes="+crashes.Count);
 }}
