using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Threading; using System.Runtime.Serialization;
class H { static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n,true); }
 const BindingFlags ALL=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static object Call(string t,string m,params object[] a){ var x=T(t).GetMethods(ALL).First(z=>z.Name==m&&z.GetParameters().Length==a.Length); try{return x.Invoke(null,a);}catch(TargetInvocationException e){throw e.InnerException;} }
 static object CallP(string t,string m,Type[] sig,object[] a){ var x=T(t).GetMethod(m,ALL,null,sig,null); try{return x.Invoke(null,a);}catch(TargetInvocationException e){throw e.InnerException;} }
 static void Rows(object o){ foreach(string[] r in (IEnumerable)o) Console.WriteLine("   ["+string.Join(" | ",r)+"]"); }
 static object Mon(){ var m=FormatterServices.GetUninitializedObject(T("Monitor")); foreach(var f in T("Monitor").GetFields(ALL)) { if(f.Name=="L") f.SetValue(m,new object()); else if(f.FieldType==typeof(string)&&!f.IsInitOnly) f.SetValue(m,""); } T("Monitor").GetField("DnsServer").SetValue(m,"127.0.0.1"); return m; }
 static object NewRun(){ return Activator.CreateInstance(T("Toolkit+Run")); }
 static void Main(string[] a){ A=Assembly.LoadFrom(a[0]); string which=a[1];
  if(which=="ntp"){ Console.WriteLine("Protocols.Ntp rows:"); Rows(Call("Protocols","Ntp","127.0.0.1:12123"));
     var rows=(List<string[]>)Call("Protocols","Ntp","127.0.0.1:12123"); Console.WriteLine("MarketOpen's match r[1]==\"Your clock\": "+(rows.FirstOrDefault(r=>r.Length>2&&r[1]=="Your clock")!=null)); }
  if(which=="dig"){ var m=Mon(); foreach(var t in new[]{"MX","NS"}){ Console.WriteLine("Toolkit.Dig example.com "+t+" @127.0.0.1:"); var rows=(List<string[]>)Call("Toolkit","Dig","example.com",t,"127.0.0.1",m); Rows(rows); Console.WriteLine("ServerChange's match r[1]==\""+t+"\": "+rows.Count(r=>r.Length>2&&r[1]==t)); } }
  if(which=="cert"){ var mi=T("Sectors").GetMethods(ALL).First(z=>z.Name=="Cert"&&z.GetParameters().Length==6);
     var th=new Thread(()=>{ var args=new object[]{"127.0.0.1",12124,null,null,null,null}; var sw=System.Diagnostics.Stopwatch.StartNew(); mi.Invoke(null,args); Console.WriteLine("Cert returned after "+sw.ElapsedMilliseconds+" ms"); }){IsBackground=true}; th.Start();
     bool done=th.Join(30000); Console.WriteLine(done?"finished":"STILL BLOCKED after 30 s (no deadline on the TLS handshake)"); }
  if(which=="upd"){ var ct=T("UpdateBreak+Change"); var c=Activator.CreateInstance(ct); ct.GetField("T").SetValue(c,DateTime.Today.AddHours(-2)); ct.GetField("What").SetValue(c,"KB5046617 (Cumulative Update)");
     var dst=T("UpdateBreak+DayStat"); var fty=typeof(Func<,>).MakeGenericType(typeof(DateTime),dst);
     var lam=System.Linq.Expressions.Expression.Lambda(fty, System.Linq.Expressions.Expression.MemberInit(System.Linq.Expressions.Expression.New(dst)), System.Linq.Expressions.Expression.Parameter(typeof(DateTime),"d")).Compile();
     var f=Call("UpdateBreak","Compare",c,lam); var ft=T("UpdateBreak+Finding"); Console.WriteLine("Compare with no History: Lv="+ft.GetField("Lv").GetValue(f)+" Text="+ft.GetField("Text").GetValue(f)); }
  if(which=="tls"){ T("Online").GetField("until",ALL).SetValue(null,DateTime.MaxValue); Console.WriteLine("Security.TlsIntercept with a self-signed www.microsoft.com:"); Rows(Call("Security","TlsIntercept",null,new List<string>()));
     Console.WriteLine("Picks/Hotel-style check is separate. Toolkit.Http-TLS rows for https://www.microsoft.com:"); Rows(Call("Toolkit","Tls","www.microsoft.com",443,"www.microsoft.com"));
     Console.WriteLine("Security.CertExpiry for www.microsoft.com:"); Rows(Call("Security","CertExpiry",null,new List<string>{"www.microsoft.com"})); }
 }}
