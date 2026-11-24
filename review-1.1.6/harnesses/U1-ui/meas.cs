using System; using System.Reflection; using System.Linq; using System.Drawing; using System.Collections.Generic;
class T { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 var D=A.GetType("AdeeboMeter.Draw"); D.GetMethod("Init",BF).Invoke(null,new object[]{1.25f});
 float S=1.25f;
 foreach(var fi in D.GetFields(BF).Where(x=>x.FieldType==typeof(Font))){ var old=(Font)fi.GetValue(null); if(old==null) continue; fi.SetValue(null,new Font("DejaVu Sans",old.Size,old.Style,GraphicsUnit.Pixel)); }
 var L=A.GetType("AdeeboMeter.L"); var use=L.GetMethod("Use",BF); var tr=L.GetMethod("T",BF);
 Func<string,Font> F=n=>(Font)D.GetField(n,BF).GetValue(null);
 var checks=new List<Tuple<string,string,float>>{
  Tuple.Create("Changed: Fun lines off · Details on · How chatty: face only.","fSmall",380f),
  Tuple.Create("Just the facts (Plain, Details on, face only)","fBodyB",260f),
  Tuple.Create("Fun lines","fSmall",70f),
  Tuple.Create("Stop monitoring","fSmall",120f),
  Tuple.Create("Edit servers","fSmall",100f),
  Tuple.Create("Refresh Valve list","fSmall",140f),
  Tuple.Create("Copy match report (internal addresses removed)","fBodyB",360f),
  Tuple.Create("Journey","fSmall",66f),
  Tuple.Create("Sniff","fSmall",58f),
  Tuple.Create("Measure again","fSmall",150f),
  Tuple.Create("Measure again  ↻","fBodyB",140f),
  Tuple.Create("This PC","fSmall",94f), Tuple.Create("Router","fSmall",94f), Tuple.Create("Internet","fSmall",94f), Tuple.Create("Names","fSmall",94f),
  Tuple.Create("no answer","fTiny",114f), Tuple.Create("not yet","fTiny",114f),
  Tuple.Create("Your ping right now","fSmall",216f),
  Tuple.Create("Include a speed test (up to 500 MB)","fSmall",9999f),
 };
 using(var bmp=new Bitmap(10,10)) using(var g=Graphics.FromImage(bmp))
 foreach(var lang in new[]{"en","fr","es","ar"}){ use.Invoke(null,new object[]{lang});
  foreach(var c in checks){ string t=(string)tr.Invoke(null,new object[]{c.Item1}); float wpx=g.MeasureString(t,F(c.Item2)).Width; float box=c.Item3*S; string flag= wpx*0.88f>box?"  OVER":""; Console.WriteLine(lang+" | "+c.Item1+" -> \""+t+"\" "+wpx.ToString("0")+"px (x0.88="+(wpx*0.88).ToString("0")+") box "+box.ToString("0")+flag); }
 }
}}
