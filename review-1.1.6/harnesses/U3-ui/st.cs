using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Runtime.Serialization;
class H { 
 static Assembly A; const BindingFlags BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object F(object o,string n){ var t=o as Type ?? o.GetType(); var f=t.GetField(n,BF); return f.GetValue(o is Type?null:o);} 
 static void Main(string[] a){ A=Assembly.LoadFrom(a[0]);
  var L=A.GetType("AdeeboMeter.L"); var use=L.GetMethod("Use",BF); var T=L.GetMethod("T",BF);
  use.Invoke(null,new object[]{a.Length>1?a[1]:"en"});
  var FV=A.GetType("AdeeboMeter.FullView");
  var secs=(string[])F(FV,"SetSections");
  Console.WriteLine("static init lang="+(a.Length>1?a[1]:"en")+" SetSections[10]="+secs[10]);
  string to=a.Length>2?a[2]:"fr"; use.Invoke(null,new object[]{to});
  var Cfg=A.GetType("AdeeboMeter.Cfg"); Cfg.GetField("Lang",BF).SetValue(null,to);
  var fv=FormatterServices.GetUninitializedObject(FV);
  var items=(IList)FV.GetMethod("BuildSetItems",BF).Invoke(fv,null);
  var SI=FV.GetNestedType("SetItem",BF);
  int n=0; foreach(var it in items){ if((string)SI.GetField("Sec").GetValue(it)==secs[10]) n++; }
  var secsInItems=items.Cast<object>().Select(it=>(string)SI.GetField("Sec").GetValue(it)).Distinct().ToList();
  Console.WriteLine("after switch to "+to+": rows in side-menu section '"+secs[10]+"' = "+n+"; sections seen in rows: "+string.Join(" | ",secsInItems));
  // policy lock
  var Pol=A.GetType("AdeeboMeter.Policy"); Pol.GetField("NoOnline",BF).SetValue(null,true);
  items=(IList)FV.GetMethod("BuildSetItems",BF).Invoke(fv,null);
  foreach(var it in items){ string lab=(string)SI.GetField("Label").GetValue(it); var help=(Func<string>)SI.GetField("Help").GetValue(it); if(lab.Contains("BGP")||lab.StartsWith("Switch")||lab.StartsWith("Les ")||lab.StartsWith("Desact")||lab.Contains("بعد")||lab.Contains("বন্ধ")) Console.WriteLine("  NoOnline row '"+lab+"' help="+help()); }
  // search
  var match=FV.GetMethod("SetMatch",BF);
  foreach(var q in a.Skip(3)){ var hits=items.Cast<object>().Where(it=>{ try{return (bool)match.Invoke(null,new object[]{it,q});}catch{return false;} }).Select(it=>(string)SI.GetField("Label").GetValue(it)).ToList(); Console.WriteLine("  search '"+q+"' -> "+hits.Count+": "+string.Join(", ",hits.Take(6))); }
 }}
