using System; using System.Reflection; using System.Linq;
class J { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 A.GetType("AdeeboMeter.Cfg").GetMethod("ApplyPreset",BF).Invoke(null,new object[]{"balanced"});
 var MT=A.GetType("AdeeboMeter.Monitor"); var m=Activator.CreateInstance(MT,true);
 MT.GetField("Gateway",BF).SetValue(m,"192.168.1.1");
 MT.GetMethod("Judge",BF).Invoke(m,null);
 var s=MT.GetMethod("BuildSnap",BF).Invoke(m,null); var ST=s.GetType();
 Console.WriteLine("L="+ST.GetField("Level").GetValue(s)+" path="+string.Join(",",(int[])ST.GetField("Path").GetValue(s))+" chip="+ST.GetField("Chip").GetValue(s)+" | "+ST.GetField("Headline").GetValue(s));
 Environment.Exit(0);
}}
