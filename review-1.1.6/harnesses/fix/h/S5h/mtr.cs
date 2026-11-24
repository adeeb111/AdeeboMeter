using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic;
class Mtr { static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n,true); }
 const BindingFlags ALL=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static object Call(string t,string m,params object[] a){ var x=T(t).GetMethods(ALL).First(z=>z.Name==m&&z.GetParameters().Length==a.Length); try{return x.Invoke(null,a);}catch(TargetInvocationException e){throw e.InnerException;} }
 static void S(object o,string f,object v){ o.GetType().GetField(f,ALL).SetValue(o,v); }
 // spec: "ip:sent:lost[:R]"  ip '-' = never answered
 static object Hop(int n,string spec){ var p=spec.Split(':'); var h=Activator.CreateInstance(T("PathTools+Hop")); string ip=p[0]=="-"?"":p[0]; int sent=int.Parse(p[1]), lost=int.Parse(p[2]);
   S(h,"N",n);S(h,"Ip",ip);S(h,"Sent",sent);S(h,"Lost",lost);S(h,"Reached",p.Length>3&&p[3]=="R"); if(sent>lost){S(h,"Sum",(double)(sent-lost)*10);S(h,"Best",10.0);S(h,"Worst",20.0);} return h; }
 static void Main(string[] a){ A=Assembly.LoadFrom(a[0]); var lt=typeof(List<>).MakeGenericType(T("PathTools+Hop"));
  foreach(var line in System.IO.File.ReadAllLines(a[1])){ if(line.Trim()==""||line.StartsWith("#")) continue; var parts=line.Split('|'); var l=(IList)Activator.CreateInstance(lt); var hs=parts[1].Trim().Split(' '); for(int i=0;i<hs.Length;i++) l.Add(Hop(i+1,hs[i]));
   int st=(int)Call("PathTools","LossStart",l); string v=(string)Call("PathTools","MtrVerdict",l);
   Console.WriteLine(parts[0].Trim()+"\n   hops: "+parts[1].Trim()+"\n   LossStart idx="+st+(st>=0?" (hop "+(st+1)+")":"")+"\n   verdict: "+v); }
 }}
