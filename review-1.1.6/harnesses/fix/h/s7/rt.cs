using System; using System.Reflection; using System.Linq; using System.Collections.Generic;
class R { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var C=A.GetType("AdeeboMeter.Cfg",true); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 Func<Dictionary<string,string>> snap=()=>{ var d=new Dictionary<string,string>(); foreach(var f in C.GetFields(BF)){ if(f.IsLiteral||f.FieldType.IsArray|| f.FieldType.Name.StartsWith("Action")||f.FieldType.Name.StartsWith("Func")) continue; var v=f.GetValue(null); d[f.Name]= v is System.Collections.IEnumerable && !(v is string) ? string.Join(",",((System.Collections.IEnumerable)v).Cast<object>()) : ""+v; }
   d["Mood"]=""+A.GetType("AdeeboMeter.Mood").GetField("Name",BF).GetValue(null); d["Rev"]=""+A.GetType("AdeeboMeter.CertCfg").GetField("Revocation",BF).GetValue(null); return d; };
 var def=snap();
 // set non-defaults
 Action<string,string> rd=(k,v)=>C.GetMethod("Read").Invoke(null,new object[]{k,v});
 foreach(var kv in new[]{"preset=custom","gw=5","net=60","wifi=120","apps=10","call=0","ipnames=0","mood=brutal","crlrev=1","portal=0","seentip=1","details=1","bigwidget=1","history2=0","histdays=90","alerts=0","quiet=0","devalerts=1","watch=0","capgb=50","onlinemin=120","pinwatch=1","wscreen=\\\\.\\DISPLAY2","gmon=valorant,cs2","gbg=60","gsrv=5","gdays=30","gfast=0","buddy=0","outagebest=77","hat=crown","gauto=0","gwidget=0","wstyle=calls","mode=pro","lang=fr","tbview=list","lview=all","ovat=10,20","speedsrv=vultr-tor","points=0","livemin=30","sniffsec=60","fade=191","dwell=800","pin=github.com"}){ int e=kv.IndexOf('='); rd(kv.Substring(0,e),kv.Substring(e+1)); }
 var set=snap();
 Console.WriteLine("did not take from Read: "+string.Join(" ",set.Where(p=>def[p.Key]==p.Value).Select(p=>p.Key)));
 var lines=((IEnumerable<string>)C.GetMethod("Lines").Invoke(null,null)).ToList();
 // reset to defaults then read back
 foreach(var f in C.GetFields(BF)){ if(f.IsLiteral||f.IsInitOnly||!def.ContainsKey(f.Name)) continue; }
 C.GetMethod("ApplyPreset").Invoke(null,new object[]{"balanced"}); rd("mood","companion"); rd("crlrev","0"); ((List<string>)C.GetField("Pins").GetValue(null)).Clear();
 foreach(var l in lines){ int e=l.IndexOf('='); rd(l.Substring(0,e),l.Substring(e+1)); }
 var back=snap();
 Console.WriteLine("lost in round trip: "+string.Join(" ",set.Where(p=>back[p.Key]!=p.Value).Select(p=>p.Key+"("+p.Value+"->"+back[p.Key]+")")));
 var known=(HashSet<string>)C.GetField("Known").GetValue(null);
 Console.WriteLine("Export->Import drops: "+string.Join(" ",lines.Select(l=>l.Substring(0,l.IndexOf('='))).Where(k=>!known.Contains(k)).Distinct()));
}}
