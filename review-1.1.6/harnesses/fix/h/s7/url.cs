using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Runtime.Serialization;
class U { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic; var FV=A.GetType("AdeeboMeter.FullView");
 var fv=FormatterServices.GetUninitializedObject(FV); var tools=(IList)FV.GetMethod("Tools2",BF).Invoke(fv,null); var url=(HashSet<string>)FV.GetField("UrlTools",BF).GetValue(null);
 var TK=A.GetType("AdeeboMeter.Toolkit"); var u2t=TK.GetMethod("UrlToTarget",BF); var sec=FV.GetMethod("SecretInput",BF); var pay=FV.GetMethod("PayloadBox",BF);
 string v="https://intranet.example.com:8443/login";
 foreach(var t in tools){ string id=(string)t.GetType().GetField("Id").GetValue(t); var ins=(string[])t.GetType().GetField("Inputs").GetValue(t); if(url.Contains(id)) continue;
  for(int i=0;i<ins.Length;i++){ var l=ins[i]; if((bool)sec.Invoke(null,new object[]{l})||(bool)pay.Invoke(null,new object[]{id,i})||l.IndexOf("DoH")>=0||l.IndexOf("https://")>=0) continue;
   bool wp=l.IndexOf(":port",StringComparison.OrdinalIgnoreCase)>=0; var r=(string)u2t.Invoke(null,new object[]{v,wp}); if(!wp && i==0 && (l.Contains("8443")||id=="certcmp"||id=="smtppath"||id=="ssh"||id=="ntp"||id=="certlive"||id=="https")) Console.WriteLine(id+" box"+i+" '"+l+"' : "+v+" -> "+r); } }
 foreach(var id in new[]{"certlive","https","certcmp"}) Console.WriteLine(id+" in UrlTools: "+url.Contains(id));
 var LabT=A.GetType("AdeeboMeter.Lab"); var sp=LabT.GetMethod("Split",BF); var args=new object[]{v,443,null,0}; Console.WriteLine("Lab.Split(url) for certlive: "+sp.Invoke(null,args)+" host="+args[2]);
}}
