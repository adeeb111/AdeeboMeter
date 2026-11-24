using System; using System.Reflection; using System.Linq; using System.Collections.Generic; using System.Diagnostics; using System.Text;
class Mb { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var t=A.GetType("AdeeboMeter.MibIndex"); var bl=t.GetMethod("BuildLines");
  foreach(var n in new[]{2000,4000,8000,16000}){
   foreach(var kind in new[]{"objtype","tc"}){
    var sb=new StringBuilder(); for(int i=0;i<n;i++) sb.Append(kind=="objtype"?"a"+i+" OBJECT-TYPE SYNTAX Integer32 MAX-ACCESS read-only STATUS current DESCRIPTION \"x\" ::= { 1 3 }\n":"T"+i+" ::= TEXTUAL-CONVENTION STATUS current DESCRIPTION \"x\" \n");
    var sw=Stopwatch.StartNew(); var r=(List<string>)bl.Invoke(null,new object[]{new[]{sb.ToString()}}); Console.WriteLine(kind+" n="+n+" bytes="+sb.Length+" lines="+r.Count+" "+sw.ElapsedMilliseconds+" ms"); }
  }
 }}
