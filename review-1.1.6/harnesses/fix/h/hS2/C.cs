using System; using System.Reflection; using System.Linq;
class C { static void Main(){ var A=Assembly.LoadFrom("r4.dll"); var m=A.GetType("AdeeboMeter.Crash").GetMethod("Clean");
 foreach (var s in new[]{
  @"System.IO.FileNotFoundException: Could not find file 'D:\Clients\Acme Corp\Merger plan Q3\board.adeebo'.",
  @"System.UnauthorizedAccessException: Access to the path 'C:\Program Files\Contoso VPN\Secret Project\x.txt' is denied.",
  @"at X in \\fileserver\HR Share\Salaries 2026\list.csv",
  @"path E:\Projects\Acme\notes\file.txt",
  @"OneDrive - Acme Corporation\Documents\keys.adeebokey: C:\Users\bob\OneDrive - Acme Corporation\Documents\keys.adeebokey"
 }) Console.WriteLine(m.Invoke(null,new object[]{s})); } }
