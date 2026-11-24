using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Diagnostics;
class P {
 static Assembly A; static Type Ty(string n){ return A.GetType("AdeeboMeter."+n,true); }
 static object Call(string t,string m,params object[] a){ var mi=Ty(t).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).First(x=>x.Name==m&&x.GetParameters().Length==a.Length); try { return mi.Invoke(null,a);} catch(TargetInvocationException e){ throw e.InnerException; } }
 static void Try(string label, Func<string> f){ var sw=Stopwatch.StartNew(); try { Console.WriteLine(label+" -> "+f()+"   ["+sw.ElapsedMilliseconds+" ms]"); } catch(Exception e){ Console.WriteLine(label+" THREW "+e.GetType().Name+": "+e.Message+"   ["+sw.ElapsedMilliseconds+" ms]"); } }
 static string Rows(object o){ return string.Join(" || ", ((IEnumerable)o).Cast<string[]>().Select(r=>string.Join("|",r.Take(3)))); }
 static object NewRun(){ return Activator.CreateInstance(Ty("Toolkit").GetNestedType("Run")); }
 static void Main(string[] a){
  A=Assembly.LoadFrom(a[0]);
  Try("Iperf 127.0.0.1 -6", ()=>Rows(Call("Toolkit","Iperf","127.0.0.1:1","-6",NewRun())));
  Try("HttpTiming :18080", ()=>Rows(Call("Protocols","HttpTiming","http://127.0.0.1:18080/x","1",NewRun())));
  var O=Ty("Toolkit").GetNestedType("Opt");
  foreach (var s in new[]{"-b 1e30","-b -5M","-l 0","-P 0x10","-t 99999999999","-S 0x1FF","-b NaN","-w 1e12","udp 20m -l 16"}) Try("ParseOpts("+s+")", ()=>{ var o=Call("Toolkit","ParseOpts",s); return string.Join(",", O.GetFields().Where(f=>f.FieldType!=typeof(List<string>)).Select(f=>f.Name+"="+f.GetValue(o)))+" ign="+string.Join(" ",(List<string>)O.GetField("Ignored").GetValue(o)); });
 }}
