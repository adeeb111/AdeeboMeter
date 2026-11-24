using System; using System.IO; using System.Linq; using System.Reflection; using System.Collections.Generic; using System.Drawing;
class P { static long Rss(){ foreach(var l in File.ReadAllLines("/proc/self/status")) if(l.StartsWith("VmRSS:")) return long.Parse(l.Substring(6).Trim().Split()[0])/1024; return -1; }
 static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  A.GetType("AdeeboMeter.Program").GetField("Dir",BF).SetValue(null,a[1]);
  var D=A.GetType("AdeeboMeter.Draw"); D.GetMethod("Init",BF).Invoke(null,new object[]{1f});
  var Sn=A.GetType("AdeeboMeter.Snap"); var sn=Activator.CreateInstance(Sn);
  Sn.GetField("Down").SetValue(sn,12.43); Sn.GetField("Up").SetValue(sn,1.07); Sn.GetField("DownHist").SetValue(sn,new[]{1.0,5,12,8}); Sn.GetField("UpHist").SetValue(sn,new[]{0.5,1,1.2,0.9});
  var pv=D.GetMethods(BF).First(m=>m.Name=="Preview"&&m.GetParameters().Length==3);
  foreach (var st in (a.Length>2? a.Skip(2).ToArray(): new[]{"speed","speedstatus","speedapps","everyday","gamer","calls","engineer","auto"}))
   foreach (bool card in new[]{false,true}) { long r0=Rss(); var sw=System.Diagnostics.Stopwatch.StartNew(); string res;
     try { var b=(Bitmap)pv.Invoke(null,new object[]{sn,st,card}); res=b==null?"null":b.Width+"x"+b.Height+" "+b.PixelFormat; if(b!=null) b.Dispose(); } catch(TargetInvocationException e){ res="THREW "+e.InnerException.GetType().Name+": "+e.InnerException.Message+" @ "+string.Join(" < ", e.InnerException.StackTrace.Split('\n').Take(4).Select(x=>x.Trim())); }
     Console.WriteLine(st+(card?" card ":" strip ")+res+"  RSS "+r0+" -> "+Rss()+" MB  "+sw.ElapsedMilliseconds+" ms"); }
 }}
