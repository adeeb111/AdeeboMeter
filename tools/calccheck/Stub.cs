using System; using System.Collections.Generic;
namespace AdeeboMeter { static class Trading { public sealed class Rate { public string Code; public double Cad; public DateTime Day; }
 public static readonly List<Rate> Rates = new List<Rate> { new Rate { Code = "USD", Cad = 1.4012, Day = new DateTime(2026, 11, 13) }, new Rate { Code = "EUR", Cad = 1.6233, Day = new DateTime(2026, 11, 13) } };
 public static void LoadFx() { } } }
