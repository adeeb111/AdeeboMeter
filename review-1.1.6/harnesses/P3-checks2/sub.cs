using System; using System.Reflection; using System.Linq; using System.Collections.Generic;
class S{ static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); var t=A.GetType("AdeeboMeter.Toolkit"); var m=t.GetMethods().First(z=>z.Name=="Subnet"&&z.GetParameters().Length==2);
 foreach(var s in new[]{"0x0A.1.2.3/24","1.2.3.4/24"}) { var r=(List<string[]>)m.Invoke(null,new object[]{s,""}); Console.WriteLine(s+" -> "+string.Join(" ; ",r.Take(2).Select(x=>x[1]+": "+x[2]))); }
 var ie=A.GetType("AdeeboMeter.Replay").GetMethod("IcmpExplain"); try{ ie.Invoke(null,new object[]{"٣/٤"}); }catch(TargetInvocationException e){ Console.WriteLine("IcmpExplain(\"٣/٤\") -> "+e.InnerException.GetType().Name+": "+e.InnerException.Message); } } }
