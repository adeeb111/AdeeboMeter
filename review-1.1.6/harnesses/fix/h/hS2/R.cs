using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Runtime.Serialization; using System.Globalization;
class R {
  static Type T, RT; static object lc; static MethodInfo add;
  static void New(){ lc=FormatterServices.GetUninitializedObject(T);
    foreach (var fn in new[]{"rtp"}) { var dt=typeof(Dictionary<,>).MakeGenericType(typeof(string),RT); T.GetField(fn,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(lc,Activator.CreateInstance(dt)); } }
  static void P(string ssrc,double t,long seq,long ts){ add.Invoke(lc,new object[]{ssrc,"in","203.0.113.5",t.ToString("0.000000",CultureInfo.InvariantCulture),seq.ToString(),ts.ToString()}); }
  static void Show(string name, int sent, int got){
    var d=(IDictionary)T.GetField("rtp",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(lc);
    foreach (DictionaryEntry kv in d){ var r=kv.Value; Func<string,object> F=n=>RT.GetField(n).GetValue(r);
      long expected=(long)F("PrevExpected")+(long)F("MaxExt")-(long)F("BaseExt")+1, count=(long)F("Count"); double loss=expected>0?Math.Max(0,(expected-count)*100.0/expected):0;
      Console.WriteLine("{0,-58} key={1} real loss {2:0.00}% -> shown {3:0.00}%  rate={4} jitter={5:0.00} ms", name, kv.Key, sent>0?100.0*(sent-got)/sent:0, loss, F("Rate"), (double)F("Jitter")*1000); }
  }
  static void Main(string[] a){
    var A=Assembly.LoadFrom("r4.dll"); T=A.GetType("AdeeboMeter.LiveCapture",true); RT=T.GetNestedType("Rtp",BindingFlags.NonPublic);
    add=T.GetMethod("AddRtp",BindingFlags.NonPublic|BindingFlags.Instance);
    var rnd=new Random(7);
    // 1 clean wrap
    New(); { double t=0; long ts=0; int s=0,g=0; for(int i=0;i<3000;i++){ long seq=(64000+i)%65536; t+=0.02; ts+=160; s++; P("0x1",t,seq,ts); g++; } Show("1 clean wrap 64000..",s,g); }
    // 2 wrap with one packet reordered across the wrap
    New(); { double t=0; long ts=0; var order=new List<long>(); for(int i=0;i<3000;i++) order.Add((64000+i)%65536); int w=order.IndexOf(65534); var x=order[w]; order.RemoveAt(w); order.Insert(w+2,x); foreach(var seq in order){ t+=0.02; ts+=160; P("0x1",t,seq,ts);} Show("2 wrap, 65534 arrives after 0",3000,3000); }
    // 3 1% random loss across a wrap
    New(); { double t=0; long ts=0; int s=0,g=0; for(int i=0;i<5000;i++){ long seq=(63000+i)%65536; t+=0.02; ts+=160; s++; if(rnd.NextDouble()<0.01) continue; P("0x1",t,seq,ts); g++; } Show("3 1% loss across wrap",s,g); }
    // 4 restart lower by < 30000 (same SSRC) then 5% loss
    foreach (int to in new[]{20000, 100, 50000}) { New(); double t=0; long ts=0; int s=0,g=0; for(int i=0;i<1000;i++){ t+=0.02; ts+=160; s++; P("0x1",t,45000+i,ts); g++; } for(int i=0;i<3000;i++){ t+=0.02; ts+=160; s++; if(i%20==0) continue; P("0x1",t,to+i,ts); g++; } Show("4 restart 45000->"+to+", then 5% loss",s,g); }
    // 5 restart near top (60000..61000) then to 2000: looks like a wrap
    { New(); double t=0; long ts=0; int s=0,g=0; for(int i=0;i<1000;i++){ t+=0.02; ts+=160; s++; P("0x1",t,60000+i,ts); g++; } for(int i=0;i<3000;i++){ t+=0.02; ts+=160; s++; if(i%20==0) continue; P("0x1",t,2000+i,ts); g++; } Show("5 restart 61000->2000 (seen as wrap), 5% loss",s,g); }
    // 6 SSRC change mid-call
    { New(); double t=0; long ts=0; for(int i=0;i<1000;i++){ t+=0.02; ts+=160; P("0xA",t,30000+i,ts);} for(int i=0;i<1000;i++){ t+=0.02; ts+=160; if(i%10==0) continue; P("0xB",t,5+i,ts);} Show("6 SSRC change A->B (B 10% loss)",0,0); }
    // 7 duplicates (10% duplicated) with 3% loss
    { New(); double t=0; long ts=0; int s=0,g=0; for(int i=0;i<3000;i++){ t+=0.02; ts+=160; s++; if(rnd.NextDouble()<0.03) continue; P("0x1",t,1000+i,ts); g++; if(rnd.NextDouble()<0.10) P("0x1",t+0.001,1000+i,ts);} Show("7 3% loss + 10% duplicates",s,g); }
    // 8 video 90 kHz, 30 fps, 5 packets per frame
    { New(); double t=0; long ts=0; long seq=100; for(int fr=0;fr<300;fr++){ t=fr/30.0; ts=fr*3000; for(int k=0;k<5;k++){ P("0x1",t+k*0.0005,seq++,ts);} } Show("8 video 90k 30fps 5 pkt/frame (no loss)",1,1); }
    // 9 audio 48 kHz opus 20 ms, network jitter uniform +-5 ms
    { New(); double ts=0; for(int i=0;i<3000;i++){ double t=i*0.02+(rnd.NextDouble()*0.01); P("0x1",t,i,(long)(i*960)); } Show("9 opus 48k, arrival jitter U(0,10ms) (RFC J ~ 3.3 ms)",1,1); }
    // 10 RTP timestamp wrap (32-bit) mid-call
    { New(); for(int i=0;i<3000;i++){ double t=i*0.02; long tsv=(4294967296L-160L*1500+160L*i)%4294967296L; P("0x1",t,i,tsv);} Show("10 32-bit RTP timestamp wrap mid-call",1,1); }
    // 11 timestamp starts near wrap: wraps within first 0.5 s (rate estimate)
    { New(); for(int i=0;i<3000;i++){ double t=i*0.02; long tsv=(4294967296L-160L*10+160L*i)%4294967296L; P("0x1",t,i,tsv);} Show("11 RTP timestamp wraps in first 0.2 s",1,1); }
  }
}
