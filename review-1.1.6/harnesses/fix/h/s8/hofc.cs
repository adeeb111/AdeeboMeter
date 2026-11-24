using System; using System.Reflection;
class P{static void Main(string[] a){var A=Assembly.LoadFrom(a[0]);var t=A.GetType("AdeeboMeter.HallOfFame");
string n="\U00020BB7田 देवनागरी";
Console.WriteLine(t.GetMethod("Code").Invoke(null,new object[]{"test-key-1",n,512,"2026-11-10"}));
Console.WriteLine(t.GetMethod("CleanName").Invoke(null,new object[]{n}));}}
