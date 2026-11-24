using System; using System.Reflection; using System.Collections; using System.Collections.Generic; using System.Linq;
class W {
  static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n, true); }
  static MethodInfo M(string t,string m,int n){ return T(t).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).First(x=>x.Name==m&&x.GetParameters().Length==n); }
  static object F(object o,string f){ return o.GetType().GetField(f).GetValue(o); }
  static void S(object o,string f,object v){ o.GetType().GetField(f).SetValue(o,v); }
  static object Bss(int freq, byte[] ies){ var b=Activator.CreateInstance(T("WlanApi+Bss")); S(b,"FreqMHz",freq); M("WlanApi","FromFreq",1).Invoke(null,new[]{b}); if(ies!=null) M("WlanApi","ReadIes",2).Invoke(null,new object[]{ies,b}); return b; }
  static string D(object b){ return "band='"+F(b,"Band")+"' ch="+F(b,"Channel")+" width="+F(b,"Width")+" sec='"+F(b,"Sec")+"'"; }
  static byte[] IE(int id, params int[] body){ var l=new List<byte>{(byte)id,(byte)body.Length}; l.AddRange(body.Select(x=>(byte)x)); return l.ToArray(); }
  static byte[] Cat(params byte[][] a){ return a.SelectMany(x=>x).ToArray(); }
  static byte[] Rsn(params int[] akms){ var l=new List<int>{1,0, 0,0x0f,0xac,4, 1,0, 0,0x0f,0xac,4, akms.Length,0}; foreach(var k in akms){ l.AddRange(new[]{0,0x0f,0xac,k}); } l.AddRange(new[]{0xc0,0}); return IE(48,l.ToArray()); }
  static void Main(){
    A=Assembly.LoadFrom("r4.dll");
    foreach (var f in new[]{2412,2437,2462,2472,2484,5160,5180,5320,5500,5720,5745,5825,5865,5885,5905,5935,5955,5975,6115,6415,7115,4920,4940,5040}) Console.WriteLine(f+" MHz -> "+D(Bss(f,null)));
    Console.WriteLine("--- IEs");
    Console.WriteLine("HT40 (2.4, ch6 sec above): "+D(Bss(2437, IE(61, 6, 0x05, 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0))));
    Console.WriteLine("VHT80 ch36 cf42: "+D(Bss(5180, Cat(IE(61,36,0x05,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0), IE(192,1,42,0,0,0)))));
    Console.WriteLine("VHT160 new-style ch36 ccfs0=42 ccfs1=50: "+D(Bss(5180, IE(192,1,42,50,0,0))));
    Console.WriteLine("VHT80+80 ch36 ccfs0=42 ccfs1=155: "+D(Bss(5180, IE(192,1,42,155,0,0))));
    Console.WriteLine("VHT160 old-style w=2 cf=50: "+D(Bss(5180, IE(192,2,50,0,0,0))));
    // HE operation: ext id 36, params(3) with bit17 set (6GHz op info present) => byte2 bit1 => 0x02, colour, mcs(2), 6GHz info: primary, control(width), ccfs0, ccfs1, minrate
    foreach (int cw in new[]{0,1,2,3}) Console.WriteLine("HE 6GHz width code "+cw+": "+D(Bss(5975, IE(255,36, 0,0,0x02, 1, 0xff,0xff, 5, cw, 7,0, 1))));
    // EHT operation (ext 106): params(1) bit0 = EHT op info present, basic MCS(4), info: control(width 4 = 320), ccfs0, ccfs1
    Console.WriteLine("Wi-Fi 7 320 MHz (HE says 160, EHT says 320): "+D(Bss(5975, Cat(IE(255,36, 0,0,0x02, 1, 0xff,0xff, 5, 3, 15,31, 1), IE(255,106, 0x01, 0,0,0,0, 4, 31, 63)))));
    Console.WriteLine("--- RSN AKMs");
    foreach (var a in new[]{ new[]{2}, new[]{8}, new[]{2,8}, new[]{1}, new[]{5}, new[]{1,5}, new[]{12}, new[]{18}, new[]{24}, new[]{6}, new[]{11}, new[]{3}, new[]{4}, new[]{13}, new[]{14} }) Console.WriteLine("AKM "+string.Join("+",a)+": "+F(Bss(5180, Rsn(a)),"Sec"));
  }
}
