using System; using System.Reflection; using System.Collections; using System.Collections.Generic; using System.Linq;
class J {
  static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n, true); }
  static void S(object o,string f,object v){ o.GetType().GetField(f).SetValue(o,v); }
  static object G(object o,string f){ return o.GetType().GetField(f).GetValue(o); }
  static object M(string t,string m,params object[] a){ return T(t).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).First(x=>x.Name==m&&x.GetParameters().Length==a.Length).Invoke(null,a); }
  static void Main(){
    A=Assembly.LoadFrom("r4.dll"); var rt=T("Journey+Rec"); var lt=typeof(List<>).MakeGenericType(rt);
    foreach (int port in new[]{853, 53}) {
      var f=Activator.CreateInstance(T("Journey+Flow")); S(f,"Remote","1.1.1.1"); S(f,"RemotePort",port); S(f,"LocalPort",50123); S(f,"Udp",false);
      var l=(IList)Activator.CreateInstance(lt); double t=0; uint sq=1000, sa=5000;
      Action<bool,int,int> add=(o,fl,pay)=>{ var r=Activator.CreateInstance(rt); S(r,"T",t); t+=0.012; S(r,"Out",o); S(r,"Proto",6); S(r,"Flags",fl); S(r,"PayLen",pay); S(r,"IpLen",40+pay); S(r,"Win",64000); S(r,"Ttl",56);
        if(o){ S(r,"Seq",sq); S(r,"Ack",sa); sq+=(uint)(pay+((fl&3)!=0?1:0)); } else { S(r,"Seq",sa); S(r,"Ack",sq); sa+=(uint)(pay+((fl&3)!=0?1:0)); } l.Add(r); };
      // one DNS-over-TLS (or DNS over TCP) exchange: handshake, (TLS), one query, one answer, close
      add(true,0x02,0); add(false,0x12,0); add(true,0x10,0);
      if (port==853) { add(true,0x18,300); add(false,0x18,3000); add(true,0x10,0); add(true,0x18,80); add(false,0x18,280); }
      add(true,0x18,45); add(false,0x18,61); add(true,0x10,0); add(true,0x11,0); add(false,0x11,0); add(true,0x10,0);
      string kind=(string)M("Journey","Kind",f,l); M("Journey","Translate",l,kind);
      var notes=(IList)M("Journey","Explain",f,l,kind,false);
      Console.WriteLine("port "+port+" kind="+kind);
      foreach (var n in notes) Console.WriteLine("   ["+G(n,"Lv")+"] "+G(n,"Title")+": "+G(n,"Text"));
    }
  }
}
