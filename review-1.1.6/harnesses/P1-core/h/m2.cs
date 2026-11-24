using System; using System.Reflection; using System.IO; using System.Linq;
class M { static void Main(string[] a){
 var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 var P=A.GetType("AdeeboMeter.Program"); P.GetField("Dir",BF).SetValue(null,a[1]);
 var C=A.GetType("AdeeboMeter.Cfg"); var T=A.GetType("AdeeboMeter.AppCtx"); var V=A.GetType("AdeeboMeter.Views");
 string f=Path.Combine(a[1],"AdeeboMeter.settings");
 Func<object> mk=()=>{ var ctx=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(T); T.GetField("settingsPath",BF).SetValue(ctx,f); return ctx; };
 for (int run=1; run<=2; run++) {
   C.GetField("Mode",BF).SetValue(null,""); C.GetField("View",BF).SetValue(null,""); C.GetField("SetupDone",BF).SetValue(null,true);
   C.GetMethod("ApplyPreset",BF).Invoke(null,new object[]{"balanced"});
   var ctx=mk();
   try { T.GetMethod("LoadSettings",BF).Invoke(ctx,null); } catch(Exception e){ Console.WriteLine("(load threw "+(e.InnerException??e).GetType().Name+": "+(e.InnerException??e).Message+")"); }
   Console.WriteLine("start "+run+": Mode='"+C.GetField("Mode",BF).GetValue(null)+"' View='"+C.GetField("View",BF).GetValue(null)+"' SetupDone="+C.GetField("SetupDone",BF).GetValue(null)+"  views="+V.GetMethod("Line").Invoke(null,null));
   if (!File.Exists(f)) { // SaveSettings needs Program etc; write what it would
     File.WriteAllLines(f, new[]{"autostart=0","widget=1","serious=0","theme=auto"}.Concat((System.Collections.Generic.IEnumerable<string>)C.GetMethod("Lines",BF).Invoke(null,null)));
     Console.WriteLine("  (first start's save written by hand)"); }
 }
}}
