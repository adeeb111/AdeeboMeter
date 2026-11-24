using System; using System.Reflection; using System.Linq; using System.Collections.Generic;
class X { static void Main(string[] a){
 var A=Assembly.LoadFrom(a[0]); var BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 var C=A.GetType("AdeeboMeter.Cfg");
 C.GetField("SetupDone",BF).SetValue(null,false);   // what LoadSettings does on a first start, before SaveSettings
 var lines=((IEnumerable<string>)C.GetMethod("Lines",BF).Invoke(null,null)).ToList();
 Console.WriteLine(string.Join("  ", lines.Where(l=>l.StartsWith("mode=")||l.StartsWith("setupdone=")||l.StartsWith("view=")||l.StartsWith("setupstep="))));
 Console.WriteLine("lines written: "+lines.Count+" (keys other than autostart > 0, so the next start is not 'first')");
}}
