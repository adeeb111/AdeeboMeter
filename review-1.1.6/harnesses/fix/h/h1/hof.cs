using System; using System.Reflection; using System.IO; using System.Text;
class H { static void Main(string[] a){
 var A=Assembly.LoadFrom(a[0]); var T=A.GetType("AdeeboMeter.HallOfFame",true);
 var code=T.GetMethod("Code"); var clean=T.GetMethod("CleanName");
 foreach (var line in File.ReadAllLines(a[1], Encoding.UTF8)) {
   string n = Encoding.UTF8.GetString(Convert.FromBase64String(line));
   string c=(string)clean.Invoke(null,new object[]{n});
   string k=(string)code.Invoke(null,new object[]{"testkey",n,512,"2026-11-12"});
   Console.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(c))+" "+k);
 }}}
