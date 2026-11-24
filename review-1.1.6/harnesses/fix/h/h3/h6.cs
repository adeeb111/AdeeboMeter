using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Diagnostics;
class P { static Assembly A; static Type Ty(string n){ return A.GetType("AdeeboMeter."+n,true); }
 static MethodInfo M(string t,string m,int n){ return Ty(t).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).First(x=>x.Name==m&&x.GetParameters().Length==n); }
 static void Main(string[] a){ A=Assembly.LoadFrom(a[0]); var rnd=new Random(7);
  var tests=new List<Tuple<string,Func<byte[],object>>>{
   Tuple.Create("Toolkit.Parse",(Func<byte[],object>)(b=>M("Toolkit","Parse",3).Invoke(null,new object[]{b,0L,"x"}))),
   //X,(Func<byte[],object>)(b=>M("Reach","ParseStun",2).Invoke(null,new object[]{b,b.Skip(8).Take(12).Concat(new byte[12]).Take(12).ToArray()}))),
   //X,(Func<byte[],object>)(b=>M("Protocols","ParseVarbinds",2).Invoke(null,new object[]{b,new List<string>()}))),
   //X,(Func<byte[],object>)(b=>M("Protocols","SnmpCheck",4).Invoke(null,new object[]{b,(byte)0x12,(byte)0x34,new byte[]{0x70}}))),
   //X19",(Func<byte[],object>)(b=>M("Lab","DhcpText",2).Invoke(null,new object[]{119,b}))),
   //X21",(Func<byte[],object>)(b=>M("Lab","DhcpText",2).Invoke(null,new object[]{121,b}))),
   //X.ParseRootDse",(Func<byte[],object>)(b=>M("MoreTools","ParseRootDse",2).Invoke(null,new object[]{b,b.Length}))),
   //X.LdapDone",(Func<byte[],object>)(b=>M("MoreTools","LdapDone",2).Invoke(null,new object[]{b,b.Length}))),
   Tuple.Create("Json.Parse",(Func<byte[],object>)(b=>M("Json","Parse",1).Invoke(null,new object[]{new string(b.Select(x=>"{}[]\",:0123456789.eE-tfnul\\ab"[x%29]).ToArray())}))),
   Tuple.Create("Protocols.ReplyOk",(Func<byte[],object>)(b=>M("Protocols","ReplyOk",5).Invoke(null,new object[]{b,(byte)(b.Length>1?b[1]:0),new byte[16],new byte[]{1,2},false}))),
  };
  var seeds=new Dictionary<string,byte[]>{{"Toolkit.Parse",new byte[]{0,1,0x81,0x80,0,1,0,2,0,0,0,0,3,(byte)'w',(byte)'w',(byte)'w',0,0,1,0,1,0xC0,12,0,65,0,1,0,0,0,60,0,8,0,1,0,0,1,0,0,0, 0xC0,12,0,16,0,1,0,0,0,60,0,4,3,97,98,99}}};
  foreach (var t in tests){ var types=new Dictionary<string,int>(); var sw=Stopwatch.StartNew(); int n=0;
   for (n=0; n<60000 && sw.ElapsedMilliseconds<15000; n++){ byte[] b; byte[] s; if (seeds.TryGetValue(t.Item1,out s) && n%2==0){ b=(byte[])s.Clone(); for(int k=0;k<1+rnd.Next(4);k++) b[rnd.Next(b.Length)]=(byte)rnd.Next(256); } else { b=new byte[rnd.Next(12,120)]; rnd.NextBytes(b); if (b.Length>4 && n%3==0){ b[0]=0x30; b[1]=(byte)(b.Length-2);} }
    try { t.Item2(b); } catch(TargetInvocationException e){ var k=e.InnerException.GetType().Name; if(!types.ContainsKey(k)){ types[k]=0; Console.WriteLine("  "+t.Item1+" "+k+" on "+BitConverter.ToString(b)); } types[k]++; } }
   Console.WriteLine(t.Item1+": "+n+" inputs, exceptions: "+string.Join(", ",types.Select(x=>x.Key+"="+x.Value))); }
  // STUN IPv6 XOR-MAPPED
  var tid=new byte[12]; var r=new List<byte>{1,1,0,24,0x21,0x12,0xA4,0x42}; r.AddRange(tid); r.AddRange(new byte[]{0,0x20,0,20,0,2,0x21^0x12,0x12^0x34}); r.AddRange(new byte[16]);
  Console.WriteLine("ParseStun XOR-MAPPED IPv6 -> "+(M("Reach","ParseStun",2).Invoke(null,new object[]{r.ToArray(),tid})??"null"));
 }}
