using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Diagnostics;
class Sn { static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n,true); }
 const BindingFlags ALL=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static object Call(string t,string m,params object[] a){ var x=T(t).GetMethods(ALL).First(z=>z.Name==m&&z.GetParameters().Length==a.Length); try{return x.Invoke(null,a);}catch(TargetInvocationException e){throw e.InnerException;} }
 static string Rows(object o){ var l=o as List<string[]>; return l==null?"null":string.Join("\n      ",l.Select(r=>string.Join(" | ",r.Take(4)).Replace("\n"," "))); }
 static void Main(string[] a){ A=Assembly.LoadFrom(a[0]); T("Program").GetField("Dir",ALL).SetValue(null,a[1]);
  string dev="127.0.0.1:"+a[2];
  foreach(var job in a.Skip(3)){ var p=job.Split('/'); string mode=p[0], what=p[1];
   var run=Activator.CreateInstance(T("Toolkit+Run")); var sw=Stopwatch.StartNew(); object res; string part="";
   try{
     if(what=="traffic") res=Call("SnmpTools","Traffic",dev,mode,run);
     else if(what=="ifaces") res=Call("SnmpTools","Interfaces",dev,mode,run);
     else res=Call("SnmpTools","Walk",dev,mode,what,run);
     part=(string)Call("SnmpTools","TakePartial");
   }catch(Exception e){ res=null; part="EXC "+e.GetType().Name+": "+e.Message; }
   Console.WriteLine("== "+mode+" "+what+" ("+sw.ElapsedMilliseconds+" ms) partial="+(part??"(none)")+"\n      "+Rows(res));
  }
 }}
