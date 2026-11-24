using System; using System.Reflection; using System.Threading; using System.Linq; using System.Collections.Generic; using System.Drawing;
class M { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 A.GetType("AdeeboMeter.Program").GetField("Dir",BF).SetValue(null, a[1]);
 var Cfg=A.GetType("AdeeboMeter.Cfg"); Cfg.GetMethod("ApplyPreset",BF).Invoke(null,new object[]{"balanced"});
 var D=A.GetType("AdeeboMeter.Draw"); D.GetMethod("Init",BF).Invoke(null,new object[]{1f});
 A.GetType("AdeeboMeter.Theme").GetMethod("Apply",BF).Invoke(null,new object[]{false});
 var MT=A.GetType("AdeeboMeter.Monitor"); var m=Activator.CreateInstance(MT,true);
 Func<object> latest=()=>MT.GetField("Latest",BF).GetValue(m);
 Action<string> render=(n)=>{ var s=latest(); var hits=new Dictionary<string,RectangleF>();
   using(var b=(Bitmap)D.GetMethod("Card",BF).Invoke(null,new object[]{s,hits})) b.Save(n+"-card.png");
   using(var b=(Bitmap)D.GetMethod("Chip",BF).Invoke(null,new object[]{s,0.0})) b.Save(n+"-chip.png"); };
 render("w0");
 MT.GetMethod("RefreshInterfaces",BF).Invoke(m,null); MT.GetMethod("Start",BF).Invoke(m,null);
 var ST=A.GetType("AdeeboMeter.Snap");
 for(int i=0;i<int.Parse(a[2])*2;i++){ var s=latest();
   Console.WriteLine((i/2.0)+"s L="+ST.GetField("Level").GetValue(s)+" path="+string.Join(",",(int[])ST.GetField("Path").GetValue(s))+" chip="+ST.GetField("Chip").GetValue(s)+" | "+ST.GetField("Headline").GetValue(s)+" gw="+ST.GetField("GwMs").GetValue(s)+" ping="+ST.GetField("Ping").GetValue(s)+" dns="+ST.GetField("DnsMs").GetValue(s));
   if(i==2) render("w1"); if(i==10) render("w5"); Thread.Sleep(500);} render("wend");
 Environment.Exit(0);
}}
