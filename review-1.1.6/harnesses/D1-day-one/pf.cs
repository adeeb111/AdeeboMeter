using System; using System.Drawing; using System.Drawing.Text; using System.IO; using System.Runtime.InteropServices;
class F{static void Main(string[] a){var pfc=new PrivateFontCollection(); var d=File.ReadAllBytes(a[0]); var m=Marshal.AllocCoTaskMem(d.Length); Marshal.Copy(d,0,m,d.Length); pfc.AddMemoryFont(m,d.Length);
foreach(var fam in pfc.Families){ Console.WriteLine(fam.Name); try{var f=new Font(fam,12f,FontStyle.Regular,GraphicsUnit.Pixel); Console.WriteLine(" h="+f.GetHeight());}catch(Exception e){Console.WriteLine(" ERR "+e.Message);} }}}
