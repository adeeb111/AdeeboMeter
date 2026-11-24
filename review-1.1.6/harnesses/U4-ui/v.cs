using System; using System.Reflection; using System.Linq; using System.Collections.Generic; using System.Globalization;
class V { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 var FV=A.GetType("AdeeboMeter.FullView"); var vn=FV.GetMethod("VNum",BF); var rm=FV.GetMethod("RowMs",BF);
 System.Threading.Thread.CurrentThread.CurrentCulture=new CultureInfo("fr-CA");
 double gb=12.34, rough=0.2;
 string so=gb.ToString("0.#",CultureInfo.CurrentCulture)+" GB down + up in 24 days";
 string six=(100-rough).ToString("0.#",CultureInfo.CurrentCulture)+"% smooth";
 string wake=(4.25).ToString("0.#",CultureInfo.CurrentCulture)+" s on average";
 Console.WriteLine("fr-CA text: '"+so+"' -> used GB = "+vn.Invoke(null,new object[]{so,@"([0-9.]+)\s*GB"}));
 Console.WriteLine("fr-CA text: '"+six+"' -> bar % = "+vn.Invoke(null,new object[]{six,@"([0-9.]+)%"}));
 Console.WriteLine("fr-CA text: '"+wake+"' -> s = "+vn.Invoke(null,new object[]{wake,@"([0-9.]+)\s*s on average"}));
 Console.WriteLine("RowMs('12,5 ms') = "+rm.Invoke(null,new object[]{"12,5 ms"}));
 var L=A.GetType("AdeeboMeter.L"); 
 foreach(var m in L.GetMethods(BF).Where(x=>x.Name=="Use")) { m.Invoke(null,new object[]{"fr"}); break; }
 var T=L.GetMethods(BF).First(x=>x.Name=="T"&&x.GetParameters().Length==1);
 var D=A.GetType("AdeeboMeter.Draw"); var W=D.GetMethod("Word",BF);
 foreach(var s in new[]{"Problem","Busy","OK","LINE OK","CHOPPY","Waiting","Competitive","Laggy","jitter","Why: ","no reply"}) Console.WriteLine("L.T(fr) '"+s+"' -> '"+T.Invoke(null,new object[]{s})+"'");
 Console.WriteLine("Draw.Word(2) in fr = '"+W.Invoke(null,new object[]{2})+"'");
}}
