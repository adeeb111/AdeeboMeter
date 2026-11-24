using System; using System.Reflection; using System.Linq; using System.Collections.Generic; using System.Drawing;
class V { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 var D=A.GetType("AdeeboMeter.Draw"); var L=A.GetType("AdeeboMeter.L");
 float s=float.Parse(a[2]); D.GetMethod("Init",BF).Invoke(null,new object[]{s});
 L.GetMethods(BF).First(x=>x.Name=="Use").Invoke(null,new object[]{a[1]}); D.GetMethod("UseLanguageFonts",BF).Invoke(null,null);
 var SC=A.GetType("AdeeboMeter.SupportCard"); var c=SC.GetConstructors(BF)[0].Invoke(new object[]{null});
 Console.WriteLine(a[1]+" S="+s+" SupportCard ClientSize="+((System.Windows.Forms.Form)c).ClientSize);
}}
