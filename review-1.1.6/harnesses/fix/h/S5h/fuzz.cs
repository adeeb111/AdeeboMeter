using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Diagnostics;
class Fz { static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n,true); }
 const BindingFlags ALL=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static MethodInfo M(string t,string m,int n){ return T(t).GetMethods(ALL).First(z=>z.Name==m&&z.GetParameters().Length==n); }
 static byte[] Tlv(byte t, byte[] v){ return (byte[])M("Snmp","Tlv",2).Invoke(null,new object[]{t,v}); }
 static byte[] Cat(params byte[][] p){ return p.SelectMany(x=>x).ToArray(); }
 static void Main(string[] a){ A=Assembly.LoadFrom(a[0]); var rnd=new Random(int.Parse(a[1])); int iters=int.Parse(a[2]);
  var parse2=M("Snmp","Parse2",4); var text=M("Snmp","Text",3); var tables=M("Snmp","Tables",3); var idx=M("MibIndex","BuiltinOnly",0).Invoke(null,null);
  var oidb=M("Snmp","OidBytes",1);
  Func<string,byte[]> O=s=>(byte[])oidb.Invoke(null,new object[]{s});
  var types=new byte[]{2,4,5,6,0x40,0x41,0x42,0x43,0x44,0x46,0x80,0x81,0x82,0x30,0xA2};
  var crashes=new Dictionary<string,int>(); int ok=0; var sw=Stopwatch.StartNew(); long maxMs=0;
  for(int it=0;it<iters;it++){
    int nvb=rnd.Next(0,6); var vbs=new List<byte[]>();
    for(int k=0;k<nvb;k++){ byte ty=types[rnd.Next(types.Length)]; var val=new byte[rnd.Next(0,12)]; rnd.NextBytes(val); if(ty==6) val=O("1.3.6.1."+rnd.Next(0,100000)+"."+rnd.Next()).Skip(2).ToArray();
      vbs.Add(Tlv(0x30,Cat(O("1.3.6.1.2.1.2.2.1."+rnd.Next(1,30)+"."+rnd.Next(0,9999)),Tlv(ty,val)))); }
    int id=rnd.Next();
    var pdu=Tlv(0xA2,Cat(Tlv(2,BitConverter.GetBytes(id).Reverse().ToArray()),Tlv(2,new byte[]{0}),Tlv(2,new byte[]{0}),Tlv(0x30,Cat(vbs.ToArray()))));
    var msg=Tlv(0x30,Cat(Tlv(2,new byte[]{1}),Tlv(4,System.Text.Encoding.ASCII.GetBytes("public")),pdu));
    // mutate
    int nm=rnd.Next(0,4); for(int j=0;j<nm;j++){ int op=rnd.Next(4); int p=rnd.Next(msg.Length);
      if(op==0) msg[p]=(byte)rnd.Next(256); else if(op==1) msg=msg.Take(p).ToArray(); else if(op==2){ msg[p]=0x84; } else { msg[p]=(byte)(msg[p]^0x80); } if(msg.Length==0) break; }
    var args=new object[]{msg,id,"public",null}; var t0=sw.ElapsedMilliseconds;
    try{ var res=parse2.Invoke(null,args) as IList; if(res!=null){ foreach(var v in res) text.Invoke(null,new object[]{v,"x",null}); var lt=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("Snmp+Vb"))); foreach(var v in res) lt.Add(v); tables.Invoke(null,new object[]{lt,idx,new List<string[]>()}); } ok++; }
    catch(TargetInvocationException e){ var ie=e.InnerException; string k=ie.GetType().Name+": "+ie.Message+" @ "+ie.StackTrace.Split('\n')[0].Trim(); if(!crashes.ContainsKey(k)){ crashes[k]=0; Console.WriteLine("CRASH "+k+"\n  input="+BitConverter.ToString(msg)); } crashes[k]++; }
    maxMs=Math.Max(maxMs,sw.ElapsedMilliseconds-t0);
  }
  Console.WriteLine("iters="+iters+" ok="+ok+" distinct crashes="+crashes.Count+" max ms="+maxMs);
 }}
