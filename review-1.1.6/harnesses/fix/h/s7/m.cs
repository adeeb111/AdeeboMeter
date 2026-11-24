using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Runtime.Serialization;
class M { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var FV=A.GetType("AdeeboMeter.FullView",true); var BF=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 var fv=FormatterServices.GetUninitializedObject(FV);
 var L=A.GetType("AdeeboMeter.L"); 
 foreach(var lang in new[]{"en","fr"}){ L.GetMethod("Use").Invoke(null,new object[]{lang}); Console.WriteLine("=== "+lang);
 foreach(var q in new[]{"speed","printer","cert","ssl","dmarc","wi-fi","wifi","","(",".*","[a-","\\","İP","IP","ip","dns","vitesse","imprimante","certificat","pare-feu","sans fil","ping","traceroute","dhcp","arp","mtu","printers","SPF","dkim","headset","bluetooth","speed test","port 443","https://github.com","192.168.1.1","rogue dhcp","pcap","bgp", "lag","toner","temperature","éducation","Ping","PING","sfp"}){
   FV.GetField("tbFilter",BF).SetValue(fv,q);
   try{ var r=(IList)FV.GetMethod("MatchesScored",BF).Invoke(fv,new object[]{q}); var ids=r.Cast<object>().Take(6).Select(t=>(string)t.GetType().GetField("Id").GetValue(t)); Console.WriteLine("'"+q+"' -> "+r.Count+": "+string.Join(",",ids));
     if(r.Count==0){ var d=FV.GetMethod("DidYouMean",BF).Invoke(fv,new object[]{q}); Console.WriteLine("    did you mean: "+d);} }
   catch(Exception e){ Console.WriteLine("'"+q+"' THREW "+(e.InnerException??e).GetType().Name+": "+(e.InnerException??e).Message); }
 }}
}}
