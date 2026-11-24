using System; using System.IO; using System.Reflection; using System.Collections.Generic;
class P { static void Main(string[] a){
  var A=Assembly.LoadFrom(a[0]); var T=A.GetType("AdeeboMeter.BrowserStore",true);
  var lv=T.GetMethod("LevelDb",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
  string dir=Path.Combine(Path.GetTempPath(),"bs"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
  // block: sh=0, ns=0x7FFFFFF0, vl=0x7FFFFFF0, then 4 bytes nre=0
  var blk=new List<byte>{0x00, 0xF0,0xFF,0xFF,0xFF,0x07, 0xF0,0xFF,0xFF,0xFF,0x07, 0,0,0,0, 0,0,0,0};
  var d=new List<byte>(blk); d.Add(0); d.AddRange(new byte[4]); // compression type 0 + crc
  int io=0, isz=blk.Count;
  var ft=new List<byte>(); ft.Add(0); ft.Add(0); // metaindex handle 0,0
  ft.Add((byte)io); ft.Add((byte)isz);
  while(ft.Count<40) ft.Add(0);
  ft.AddRange(BitConverter.GetBytes(0xdb4775248b80fb57UL));
  d.AddRange(ft);
  File.WriteAllBytes(Path.Combine(dir,"000005.ldb"), d.ToArray());
  try { var r=lv.Invoke(null,new object[]{dir}); Console.WriteLine("returned "+r); }
  catch(TargetInvocationException e){ Console.WriteLine("THREW "+e.InnerException.GetType().Name+": "+e.InnerException.Message); }
}}
