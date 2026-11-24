using System; using System.Drawing; using System.Linq; using System.Reflection; using System.Collections.Generic;
class Sg { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic; var FV=A.GetType("AdeeboMeter.FullView");
 var L=A.GetType("AdeeboMeter.L");
 var rows=new Dictionary<string,string[]>{{"HistDayNames",null},{"SetVoice",null},{"WStyle",null},{"SetS18",null},{"CapNames",null},{"SetOnlineMin",null},{"SetS2",null},{"SetS14",null}};
 using(var bmp=new Bitmap(10,10)) using(var g=Graphics.FromImage(bmp)) foreach(var lang in new[]{"en","fr"}) { L.GetMethod("Use").Invoke(null,new object[]{lang});
  foreach(var S in new[]{1f,1.25f}) using(var f=new Font("Liberation Sans",11*S,FontStyle.Regular,GraphicsUnit.Pixel)) {
   // settings w: window W maximized, side col = max(300S,min(390S,0.3*body)); left = body - side - 14S; w = min(left - 252S - 24S, 940S)
   foreach(var W in new[]{980,1366,1920}){ float body=W-28*S; float side=Math.Max(300*S,Math.Min(390*S,body*0.3f)); float left=body-side-14*S; float w=Math.Min(left-(16+210+26)*S-24*S,940*S);
    var parts=new List<string>();
    foreach(var k in rows.Keys){ var opts=(string[])(FV.GetField(k,BF)??A.GetType("AdeeboMeter.FullView").GetField(k,BF)).GetValue(null); float ow=opts.Select(o=>g.MeasureString((string)L.GetMethod("T").Invoke(null,new object[]{o}),f).Width+24*S).Sum()+2*S; float tw=w-ow-24*S; if(tw<120*S) parts.Add(k+": seg "+ow.ToString("0")+" help col "+tw.ToString("0")); }
    Console.WriteLine(lang+" S="+S+" window "+W+": w="+w.ToString("0")+"  "+(parts.Count==0?"ok":string.Join("; ",parts))); } } }
}}
