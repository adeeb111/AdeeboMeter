using System; using System.Reflection; using System.IO;
class M { static void Main(string[] a){
 var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 var P=A.GetType("AdeeboMeter.Program"); P.GetField("Dir",BF).SetValue(null,a[1]);
 var C=A.GetType("AdeeboMeter.Cfg"); var T=A.GetType("AdeeboMeter.AppCtx");
 string f=Path.Combine(a[1],"AdeeboMeter.settings");
 Console.WriteLine("settings before: ["+(File.Exists(f)?File.ReadAllText(f).Trim():"(none)")+"]");
 var ctx=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(T);
 T.GetField("settingsPath",BF).SetValue(ctx,f);
 C.GetMethod("ApplyPreset",BF).Invoke(null,new object[]{"balanced"});
 try { T.GetMethod("LoadSettings",BF).Invoke(ctx,null); } catch(Exception e){ Console.WriteLine("(load threw "+e.InnerException.GetType().Name+")"); }
 Console.WriteLine("Cfg.Mode after load = '"+C.GetField("Mode",BF).GetValue(null)+"'");
}}
