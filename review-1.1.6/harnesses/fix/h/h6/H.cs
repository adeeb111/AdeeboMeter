using System; using System.Reflection; using System.Collections.Generic; using System.Linq; using System.IO;
class H {
  static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n, true); }
  static object Call(string type, string m, params object[] a){ var mi = T(type).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).First(x=>x.Name==m && x.GetParameters().Length==a.Length); try { return mi.Invoke(null,a);} catch(TargetInvocationException e){ return "EXC:"+e.InnerException.GetType().Name+": "+e.InnerException.Message; } }
  static string Rows(object o){ var l = o as List<string[]>; if (l==null) return o==null?"null":o.ToString(); return string.Join(" || ", l.Select(r=>string.Join(" | ", r))); }
  static void Main(string[] args){
    A = Assembly.LoadFrom("/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/build6/r6.dll"); Console.OutputEncoding = System.Text.Encoding.UTF8; Console.InputEncoding = System.Text.Encoding.UTF8;
    string line;
    while((line=Console.In.ReadLine())!=null){
      var p = line.Split('\t'); string outp;
      try {
      switch(p[0]){
        case "vlsm": outp = Rows(Call("Calc","Vlsm",p[1],p[2])); break;
        case "stack": outp = Rows(Call("Calc","Stack",p[1],p[2],p[3])); break;
        case "bw": outp = Rows(Call("Calc","Bandwidth",p[1],p[2],p[3])); break;
        case "num": outp = Rows(Call("Calc","Number",p[1])); break;
        default: outp="?"; break;
      }} catch(Exception e){ outp = "HEXC:"+e; }
      Console.WriteLine(outp.Replace("\n","\\n")); Console.Out.Flush();
    }
  }
}
