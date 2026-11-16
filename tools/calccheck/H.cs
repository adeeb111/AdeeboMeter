using System; using System.Linq; using System.Collections.Generic;
namespace AdeeboMeter { static class H { static void Main(string[] a) {
 Console.OutputEncoding = System.Text.Encoding.UTF8; var lines = System.IO.File.ReadAllLines(a[0]);
 foreach (var l in lines) { var p = l.Split('\t'); var m = typeof(Calc).GetMethod(p[0]); var args = p.Skip(1).Cast<object>().ToArray();
  var r = (List<string[]>)m.Invoke(null, args); Console.WriteLine("### " + l); foreach (var x in r) Console.WriteLine(string.Join(" | ", x.Take(4))); } } } }
