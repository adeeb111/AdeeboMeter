using System; using System.Reflection; using System.Collections; using System.Collections.Generic; using System.Linq; using System.IO; using System.Globalization;
class N {
  static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n, true); }
  static MethodInfo M(string t,string m,int n){ return T(t).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).First(x=>x.Name==m&&x.GetParameters().Length==n); }
  static object F(object o,string f){ return o.GetType().GetField(f).GetValue(o); }
  static void Main(string[] a){
    A=Assembly.LoadFrom("r4.dll"); Console.OutputEncoding=System.Text.Encoding.UTF8;
    foreach (var lang in a) {
      string iface=File.ReadAllText("netsh/"+lang+".if.txt"), nets=File.ReadAllText("netsh/"+lang+".net.txt");
      iface=(string)M("LinkInfo","ActiveBlock",1).Invoke(null,new object[]{iface});
      var f=(Dictionary<string,string>)M("LinkInfo","Fields",1).Invoke(null,new object[]{iface});
      string v; int ch=-1, sig=-1;
      if (f.TryGetValue("Channel", out v)) int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out ch);
      if (f.TryGetValue("Signal", out v)) int.TryParse(v.TrimEnd('%',' ',' ',' ','\t'), NumberStyles.Integer, CultureInfo.InvariantCulture, out sig);
      var pw=new object[]{iface,null,null,null,null,null,null}; M("Monitor","ParseWifi",7).Invoke(null,pw);
      int psig=(int)pw[1]; string pch=(string)pw[3], pbssid=(string)pw[4], pstate=(string)pw[5]; int prate=(int)pw[6];
      if (sig<0) sig=psig; if (ch<0) int.TryParse(pch, NumberStyles.Integer, CultureInfo.InvariantCulture, out ch); if (ch==0) ch=-1;
      string apiBand = f.ContainsKey("Band") ? "" : "2.4 GHz";   // Collect asks WlanApi only when netsh had no Band line (simulated: API answers 2.4 GHz)
      var NB=M("LinkInfo","NormBand",1); string nb=f.TryGetValue("Band",out v)?(string)NB.Invoke(null,new object[]{v}):""; string band=(string)M("LinkInfo","BandOf",2).Invoke(null,new object[]{ch, nb!=""?nb:"2.4 GHz"});
      string radio=f.TryGetValue("Radio type",out v)?v:"";
      string bssid=f.TryGetValue("AP BSSID",out v)?v:f.TryGetValue("BSSID",out v)?v:pbssid;
      Func<string,string> FF=k=>{string x; return f.TryGetValue(k,out x)&&x!=""?x:"-";};
      var aps=(IList)M("LinkInfo","ParseNetworks",1).Invoke(null,new object[]{nets});
      var ov=M("LinkInfo","Overlaps",3);
      var same=aps.Cast<object>().Where(x=>!string.Equals((string)F(x,"Bssid"),bssid,StringComparison.OrdinalIgnoreCase) && (string)F(x,"Band")==band && (bool)ov.Invoke(null,new object[]{F(x,"Channel"),ch,band})).ToList();
      int strong=same.Count(x=>(int)F(x,"Signal")>=50);
      bool note24 = band.StartsWith("2.4",StringComparison.Ordinal) && aps.Cast<object>().Any(x=>(string)F(x,"Ssid")==FF("SSID") && !((string)F(x,"Band")).StartsWith("2.4",StringComparison.Ordinal));
      var lt=typeof(List<>).MakeGenericType(T("Ap")); var al=(IList)Activator.CreateInstance(lt); foreach(var x in aps) al.Add(x);
      var lc=(int[])M("WlanApi","LeastCrowded",4).Invoke(null,new object[]{al,band,ch,bssid});
      Console.WriteLine("["+lang+"] sig="+sig+" ch="+ch+" band='"+band+"' radio='"+radio+"' bssid="+bssid+" state="+pstate+" rate="+prate+" rx='"+FF("Receive rate (Mbps)")+"' sec='"+FF("Authentication")+" · "+FF("Cipher")+"'");
      Console.WriteLine("     APs: "+string.Join("; ",aps.Cast<object>().Select(x=>F(x,"Ssid")+"/"+F(x,"Signal")+"%/ch"+F(x,"Channel")+"/'"+F(x,"Band")+"'")));
      Console.WriteLine("     Sharing your channel: "+(same.Count==0?"nobody - clean air":same.Count+" ("+strong+" strong)")+" | 2.4->5 note: "+note24+" | LeastCrowded: "+(lc==null?"null":string.Join(",",lc)));
    }
  }
}
