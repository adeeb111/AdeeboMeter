using System; using System.Reflection; using System.Linq; using System.Drawing; using System.Windows.Forms;
class T { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 var D=A.GetType("AdeeboMeter.Draw"); D.GetMethod("Init",BF).Invoke(null,new object[]{1.25f});
 foreach(var fi in D.GetFields(BF).Where(x=>x.FieldType==typeof(Font))){ var old=(Font)fi.GetValue(null); float px=old==null?15f:old.Size; fi.SetValue(null,new Font("DejaVu Sans",px,old==null?FontStyle.Regular:old.Style,GraphicsUnit.Pixel)); Console.WriteLine(fi.Name+" "+px); }
 var AC=A.GetType("AdeeboMeter.AskCard");
 var ctor=AC.GetConstructors(BF)[0];
 string body=string.Join("\n", Enumerable.Range(1,60).Select(i=>"• Hop "+i+" · 10.0.0."+i+" · 12 ms"));
 var f=(Form)ctor.Invoke(new object[]{"Exactly what the link carries - anyone you give it to can read it:\n\n"+body+"\n\nShow the code?","AdeeboMeter",MessageBoxButtons.OKCancel,MessageBoxIcon.Information,MessageBoxDefaultButton.Button1});
 Console.WriteLine("AskCard ClientSize at 125%: "+f.ClientSize+"  (1080p working area ~1040 high)");
 foreach(Control c in f.Controls) Console.WriteLine("  button "+c.Text+" at y="+c.Top);
}}
