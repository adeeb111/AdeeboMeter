using System; using System.Reflection; using System.Linq; using System.Collections;
class B { static void Main(){
 var asm=Assembly.LoadFrom("r6.dll"); var BF=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 var HS=asm.GetType("AdeeboMeter.HwSpecs"); var ST=HS.GetNestedType("Specs",BF); var DK=HS.GetNestedType("Disk",BF);
 var s=Activator.CreateInstance(ST,true); var disks=(IList)ST.GetField("Disks",BF).GetValue(s);
 var d=Activator.CreateInstance(DK,true); DK.GetField("Gb",BF).SetValue(d,1500.0); DK.GetField("Media",BF).SetValue(d,"SSD"); DK.GetField("Bus",BF).SetValue(d,"NVMe"); disks.Add(d);
 ST.GetField("RamGb",BF).SetValue(s,16.0); ST.GetField("Cpu",BF).SetValue(s,"Intel Core i7-1255U");
 foreach (var said in new[]{"i7, 16GB RAM, 1,5 TB SSD","i7, 16GB RAM, 1.5 TB SSD"}){
  var rows=(System.Collections.Generic.List<string[]>)asm.GetType("AdeeboMeter.HwTools").GetMethod("Seller",BF).Invoke(null,new object[]{said,s});
  Console.WriteLine(said); foreach(var r in rows) Console.WriteLine("  "+string.Join(" | ",r.Take(3)));
 }
}}
