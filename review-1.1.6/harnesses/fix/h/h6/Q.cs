using System; using System.Reflection; using System.Collections; using System.Collections.Generic; using System.Linq;
class Q {
  static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n, true); }
  static object F(object o,string f){ var t=o.GetType(); var fi=t.GetField(f,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static); return fi.GetValue(o); }
  static void Main(string[] a){
    A=Assembly.LoadFrom("/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/build6/r6.dll");
    var ls=T("LabSim");
    if (a[0]=="subnet") for(int s=0;s<int.Parse(a[1]);s++){ var q=ls.GetMethod("Subnet").Invoke(null,new object[]{s}); Console.WriteLine(s+"\t"+F(q,"Question")+"\t"+F(q,"Answer")+"\t"+string.Join("|",(string[])F(q,"Options"))); }
    if (a[0]=="addr") { var lg=T("LearnGames"); var m=lg.GetMethod("Addr"); var r=new Random(5); for(int i=0;i<int.Parse(a[1]);i++){ var args=new object[]{r,null,null}; m.Invoke(null,args); Console.WriteLine(args[1]+"\t"+args[2]); } }
    if (a[0]=="jitter") {
      double[] jits={2,5,10,20,30,50,80}, bufs={10,20,40,60,80,120,200}, bases={10,30,60,100,150,250}, losses={0,0.5,1,2,5};
      int met=0,tot=0;
      foreach(var j in jits){ if(j<30) continue; var arr=ls.GetMethod("Arrivals").Invoke(null,new object[]{j,2000,11});
        foreach(var bs in bases) foreach(var lo in losses){ 
          Func<double,double> mos = b => (double)F(ls.GetMethod("Jitter").Invoke(null,new object[]{arr,bs,b,lo}),"Mos");
          foreach(var bf in bufs){ tot++; bool ok = mos(bf)>=4.0 && bufs.Where(x=>x<bf).All(x=>mos(x)<4.0); if(ok){met++; Console.WriteLine("met jit="+j+" base="+bs+" loss="+lo+" buf="+bf+" mos="+mos(bf).ToString("0.00"));} } } }
      Console.WriteLine("met "+met+" of "+tot);
      foreach(var j in jits){ var arr=ls.GetMethod("Arrivals").Invoke(null,new object[]{j,2000,11}); var o=ls.GetMethod("Jitter").Invoke(null,new object[]{arr,60.0,2*j,0.0}); var o2=ls.GetMethod("Jitter").Invoke(null,new object[]{arr,60.0,60.0,0.0});
        var l=(List<double>)arr; double mad=0; for(int i=1;i<l.Count;i++) mad+=Math.Abs(l[i]-l[i-1]); mad/=l.Count-1;
        Console.WriteLine("jit "+j+" measured RFC3550-ish "+mad.ToString("0.0")+" buf 2x late%="+((double)F(o,"LatePct")).ToString("0.00")+" mos="+((double)F(o,"Mos")).ToString("0.00")+" | buf60 mos="+((double)F(o2,"Mos")).ToString("0.00")); }
    }
    if (a[0]=="tcp") {
      foreach (var algo in new[]{"cubic","reno"}) foreach(var cfg in new[]{new double[]{100,40,1,0},new double[]{1,5,0.1,0},new double[]{1000,300,8,0},new double[]{100,40,1,1}}){
        var ti=T("LabSim+TcpIn"); var p=Activator.CreateInstance(ti,true); ti.GetField("Algo").SetValue(p,algo); ti.GetField("Mbps").SetValue(p,cfg[0]); ti.GetField("RttMs").SetValue(p,cfg[1]); ti.GetField("BufferBdp").SetValue(p,cfg[2]); ti.GetField("LossPct").SetValue(p,cfg[3]); ti.GetField("Rounds").SetValue(p,480);
        var s=(IList)ls.GetMethod("Tcp").Invoke(null,new[]{p}); int n=s.Cast<object>().Count(x=>(string)F(x,"Loss")!="");
        Console.WriteLine(algo+" "+string.Join("/",cfg)+" losses="+n+" util="+((double)ls.GetMethod("Throughput").Invoke(null,new object[]{s})).ToString("0.000")+" max="+s.Cast<object>().Max(x=>(double)F(x,"Cwnd")).ToString("0.0")+" first losses at "+string.Join(",",s.Cast<object>().Where(x=>(string)F(x,"Loss")!="").Take(4).Select(x=>F(x,"Round")+":"+((double)F(x,"Cwnd")).ToString("0"))));
      }
      Console.WriteLine("mathis 30ms 1%: "+ls.GetMethod("MathisMbps").Invoke(null,new object[]{30.0,1.0}));
    }
  }
}
