using System; using System.Reflection; using System.Collections.Generic; using System.Linq;
class K { static void Main(){ var A=Assembly.LoadFrom("r4.dll"); var C=A.GetType("AdeeboMeter.Calls");
  var sec=C.GetMethod("Second"); 
  for(int i=0;i<120;i++) sec.Invoke(null,new object[]{null,true,3.0,double.NaN,80,866,1.0,0.5});
  sec.Invoke(null,new object[]{null,false,3.0,double.NaN,80,866,1.0,0.5});
  var s=(List<string[]>)C.GetField("Summaries").GetValue(null); foreach(var r in s) Console.WriteLine(string.Join(" | ",r));
  // short call: 25 s
  for(int i=0;i<25;i++) sec.Invoke(null,new object[]{null,true,3.0,20.0,80,866,1.0,0.5});
  sec.Invoke(null,new object[]{null,false,3.0,20.0,80,866,1.0,0.5});
  foreach(var r in s.Take(1)) Console.WriteLine(string.Join(" | ",r));
}}
