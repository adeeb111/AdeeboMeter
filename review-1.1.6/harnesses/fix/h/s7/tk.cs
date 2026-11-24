using System; using System.Linq; using System.Globalization;
class T{ static void Main(){ foreach(var s in new[]{"İP","İ","I"}) { var l=s.ToLowerInvariant(); Console.WriteLine(string.Join(" ",l.Select(c=>((int)c).ToString("X4")))); }
 System.Threading.Thread.CurrentThread.CurrentCulture=new CultureInfo("tr-TR"); Console.WriteLine(string.Join(" ","PING".ToLower().Select(c=>((int)c).ToString("X4")))); }}
