using System; using System.Reflection; using System.Windows.Forms; using System.Drawing;
class R { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 var Th=A.GetType("AdeeboMeter.Theme"); var FV=A.GetType("AdeeboMeter.FullView");
 Th.GetMethod("Apply").Invoke(null,new object[]{true}); var ink=(Color)Th.GetField("Ink").GetValue(null);
 var p=new Panel(); var cb=new CheckBox{ForeColor=ink}; p.Controls.Add(cb);
 Th.GetMethod("Apply").Invoke(null,new object[]{false}); FV.GetMethod("Recolor",BF).Invoke(null,new object[]{p});
 Console.WriteLine("after dark->light: checkbox fore="+ColorTranslator.ToHtml(cb.ForeColor)+" back="+ColorTranslator.ToHtml(cb.BackColor)+" (light Ink should be #1B2330)");
}}
