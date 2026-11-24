using System; using System.Reflection; using System.Linq; using System.Drawing; using System.Windows.Forms;
class T { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 var D=A.GetType("AdeeboMeter.Draw"); D.GetMethod("Init",BF).Invoke(null,new object[]{1f});
 foreach(var fi in D.GetFields(BF).Where(x=>x.FieldType==typeof(Font))){ var old=(Font)fi.GetValue(null); if(old==null) continue; fi.SetValue(null,new Font("DejaVu Sans",old.Size,old.Style,GraphicsUnit.Pixel)); }
 var L=A.GetType("AdeeboMeter.L");
 foreach(var lang in new[]{"fr","es","ar","bn"}){ L.GetMethod("Use",BF).Invoke(null,new object[]{lang});
  var f=(Form)A.GetType("AdeeboMeter.AskCard").GetConstructors(BF)[0].Invoke(new object[]{"q","",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button1});
  Console.WriteLine(lang+": buttons = "+string.Join(" | ", f.Controls.Cast<Control>().Select(c=>c.Text)));
  var g=A.GetType("AdeeboMeter.FullView").GetField("GuideLook",BF);
 }
}}
