using System; using System.Reflection; class R{static void Main(string[] a){foreach(var n in Assembly.LoadFrom(a[0]).GetManifestResourceNames())Console.WriteLine(n);}}
