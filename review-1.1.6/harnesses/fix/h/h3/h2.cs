using System; using System.Reflection; using System.Linq; using System.IO; using System.Collections; using System.Collections.Generic; using System.Diagnostics;
class P {
 static Assembly A; static Type Ty(string n){ return A.GetType("AdeeboMeter."+n,true); }
 static object Call(string t,string m,params object[] a){ var mi=Ty(t).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).First(x=>x.Name==m&&x.GetParameters().Length==a.Length); try { return mi.Invoke(null,a);} catch(TargetInvocationException e){ throw e.InnerException; } }
 static void Try(string label, Func<string> f){ var sw=Stopwatch.StartNew(); try { Console.WriteLine(label+" -> "+f()+"   ["+sw.ElapsedMilliseconds+" ms]"); } catch(Exception e){ Console.WriteLine(label+" THREW "+e.GetType().Name+": "+e.Message+"   ["+sw.ElapsedMilliseconds+" ms]"); } }
 static string Rows(object o){ return string.Join(" || ", ((IEnumerable)o).Cast<string[]>().Select(r=>string.Join("|",r.Take(4)))); }
 static object NewRun(){ return Activator.CreateInstance(Ty("Toolkit").GetNestedType("Run")); }
 static void Main(string[] a){
  A=Assembly.LoadFrom(a[0]);
  { var b=new byte[16384]; var t=System.Text.Encoding.ASCII.GetBytes("defaultNamingContext"); int i=16384-30; b[i]=4; b[i+1]=(byte)t.Length; Array.Copy(t,0,b,i+2,t.Length); b[i+2+t.Length]=0x31; b[i+3+t.Length]=0xFF;
    Try("ParseRootDse(crafted tail)", ()=>{ var d=(IDictionary)Call("MoreTools","ParseRootDse",b,b.Length); return d.Count+" entries"; }); }
  Try("Subnet 192.168.001.010", ()=>Rows(Call("Toolkit","Subnet","192.168.001.010/24","")));
  Try("Lab.Icmp '-l 1472 -f'", ()=>Rows(Call("Lab","Icmp",null,"127.0.0.1","1","-l 1472 -f","yes",NewRun())));
  Try("Reach.Http userinfo", ()=>Rows(Call("Reach","Http","http://admin:s3cret@127.0.0.1:18080/api","GET","","")));
  Try("Toolkit.Http redirect to file:", ()=>Rows(Call("Toolkit","Http","http://127.0.0.1:18082/","")));
  Try("Ntp zero timestamps", ()=>Rows(Call("Protocols","Ntp","127.0.0.1:18123")));
  if (a.Length>1) Try("QueryDoh drip (1 B / 4 s)", ()=>{ string why=""; var args=new object[]{new Uri("http://127.0.0.1:18081/dns-query"),"example.com",1,why}; var r=Ty("Toolkit").GetMethod("QueryDoh").Invoke(null,args); return (r==null?"null":"msg")+" why="+args[3]; });
 }}
