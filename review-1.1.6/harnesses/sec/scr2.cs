using System; using System.Reflection;
class P { static void Main() { var A = Assembly.LoadFrom("r6.dll"); Type f=null; foreach (var t in A.GetTypes()) if (t.Name=="FullView") f=t;
 var m = f.GetMethod("ShScrub", BindingFlags.Static|BindingFlags.NonPublic); foreach (var s in new[]{"curl -u bob:s3cretPW http://x", "curl: here it looks at the answer - left out: -u bob:s3cretPW", "snmpget -c privcomm 10.0.0.1", "radius 10.0.0.1 sharedsecret123"}) Console.WriteLine(s + " -> " + m.Invoke(null, new object[]{s})); }}
