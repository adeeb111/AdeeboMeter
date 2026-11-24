using System; using System.Reflection;
class P { static void Main() {
 var A = Assembly.LoadFrom("r6.dll"); Type b = null; foreach (var t in A.GetTypes()) if (t.Name=="BugReport") b = t;
 var m = b.GetMethod("Scrub", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic, null, new[]{typeof(string),typeof(string),typeof(string)}, null);
 foreach (var s in new[]{ @"open failed C:\Users\Jane Doe\Documents\x.txt", @"read C:/Users/jsmith/AppData/x", @"profile at %USERPROFILE% = C:\Users\jsmith", "gw fe80::1%12 and 192.168.1.1 and 2001:db8::1", "dns nas.home.lan ok", "router TP-Link_5G_Smith answered", "mac aa-bb-cc-dd-ee-ff aabb.ccdd.eeff AABBCCDDEEFF", @"\\JANE-PC\share\x", "remote: user jsmith@contoso.com", "phone file: couldn't save C:\\Users\\jsmith\\Downloads\\AdeeboMeter\\secret plan.pdf" })
  Console.WriteLine(s + "\n   -> " + m.Invoke(null, new object[]{ s, "Jane Doe", "JANE-PC" }));
}}
