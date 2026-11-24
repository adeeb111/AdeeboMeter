using System; using System.Reflection; using System.Linq; using System.Collections.Generic;
class H { static void Main(string[] a){ var A=Assembly.LoadFrom(a[0]); Console.WriteLine("System folder='"+Environment.GetFolderPath(Environment.SpecialFolder.System)+"'");
 var r=(List<string[]>)A.GetType("AdeeboMeter.WhyFails").GetMethod("Hosts").Invoke(null,new object[]{"intranet.example.com"});
 Console.WriteLine("rows="+r.Count+" pins="+r.Count(x=>x[1]=="Pins your site")+" last="+string.Join("|",r.Last())); }}
