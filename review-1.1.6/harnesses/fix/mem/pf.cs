using System; using System.Drawing; using System.Drawing.Text; using System.Runtime.InteropServices; using System.IO;
class P { static void Main(string[] a){
  var pfc=new PrivateFontCollection(); var data=File.ReadAllBytes(a[0]); var p=Marshal.AllocCoTaskMem(data.Length); Marshal.Copy(data,0,p,data.Length); pfc.AddMemoryFont(p,data.Length);
  var pfc2=new PrivateFontCollection(); pfc2.AddFontFile(a[0]);
  using (var bmp=new Bitmap(1,1)) using (var g=Graphics.FromImage(bmp)) {
   foreach (var f in new[]{ new Font(pfc.Families[0],14,FontStyle.Bold,GraphicsUnit.Pixel), new Font(pfc2.Families[0],14,FontStyle.Bold,GraphicsUnit.Pixel), new Font("DejaVu Sans",14,GraphicsUnit.Pixel)})
    for (int i=0;i<4;i++) Console.WriteLine(f.Name+" ("+(f.FontFamily==pfc.Families[0]?"memory":"file/system")+") 'All good' w=240 -> "+g.MeasureString("All good",f,240)); } }}
