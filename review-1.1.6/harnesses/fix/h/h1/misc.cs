using System; using System.Reflection; using System.Linq; using System.Collections.Generic;
class X { static void Main(string[] a){
 var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 var C=A.GetType("AdeeboMeter.Cfg");
 var known=(HashSet<string>)C.GetField("Known",BF).GetValue(null);
 var lines=((IEnumerable<string>)C.GetMethod("Lines",BF).Invoke(null,null)).Select(l=>l.Split('=')[0]).Distinct();
 Console.WriteLine("Saved keys that Import ignores: "+string.Join(", ", lines.Where(k=>!known.Contains(k))));
 // Alerts: ARP flip text in both voices
 var Al=A.GetType("AdeeboMeter.Alerts"); var shown=new List<string>();
 Al.GetField("Show",BF).SetValue(null, (Action<string,string>)((t,x)=>shown.Add(x)));
 var EvT=A.GetType("AdeeboMeter.Timeline+Ev");
 foreach (var txt in new[]{"Your router's hardware address keeps flipping (aa → bb) on the same network. If nobody swapped the router, someone may be pretending to be it (ARP spoofing). Use the VPN until you know.","Gateway MAC flips aa → bb on the same network - possible ARP spoofing."}){
   var ev=Activator.CreateInstance(EvT); EvT.GetField("Kind").SetValue(ev,"net"); EvT.GetField("Lv").SetValue(ev,2); EvT.GetField("Text").SetValue(ev,txt); EvT.GetField("T").SetValue(ev,DateTime.Now);
   int before=shown.Count; Al.GetMethod("OnEvent",BF).Invoke(null,new[]{ev});
   Console.WriteLine((shown.Count>before?"ALERT   ":"NO ALERT")+"  <- "+txt.Substring(0,40));
   // reset cooldown
   ((System.Collections.IDictionary)Al.GetField("last",BF).GetValue(null)).Clear();
 }
 // Mood: an outage that started 4 h ago, seen once, then a fresh outage now
 var Mo=A.GetType("AdeeboMeter.Mood"); var Sn=A.GetType("AdeeboMeter.Snap");
 Func<int[],object> snap = p => { var s=Activator.CreateInstance(Sn); Sn.GetField("Path").SetValue(s,p); Sn.GetField("Level").SetValue(s,2); return s; };
 var quip=Mo.GetMethod("Quip",BF,null,new[]{Sn,typeof(string)},null);
 quip.Invoke(null,new[]{snap(new[]{0,0,2,3,3}),"ExampleNet"});           // first outage seen (widget drawn)
 Mo.GetField("downSince",BF).SetValue(null, DateTime.Now.AddHours(-4)); // ... 4 hours pass with the card never drawn; the line came back and went again
 Console.WriteLine("Fresh outage, first quip: "+quip.Invoke(null,new[]{snap(new[]{0,0,2,3,3}),"ExampleNet"}));
}}
