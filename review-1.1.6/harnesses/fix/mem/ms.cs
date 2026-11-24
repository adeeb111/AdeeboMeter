using System; using System.IO; using System.Linq; using System.Reflection; using System.Drawing;
class P { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  A.GetType("AdeeboMeter.Program").GetField("Dir",BF).SetValue(null,a[1]);
  var D=A.GetType("AdeeboMeter.Draw"); D.GetMethod("Init",BF).Invoke(null,new object[]{1f});
  var me=D.GetMethod("Measure",BF,null,new[]{typeof(string),typeof(Font),typeof(int)},null);
  foreach (var fn in new[]{"fHead","fSmall","fBody"}) { var f=(Font)D.GetField(fn,BF).GetValue(null); Console.WriteLine(fn+" = "+f.Name+" "+f.Size+" "+f.Unit);
   foreach (var t in new[]{"", "x", "All good", "Hello world, this is a longer headline that should wrap onto two lines at least.", "\n", "☀ 12°"})
     foreach (int w in new[]{240,0}) Console.WriteLine("   w="+w+" '"+t.Replace("\n","\\n")+"' -> "+me.Invoke(null,new object[]{t,f,w})); }
 }}
