using System; using System.IO; using System.Linq; using System.Reflection; using System.Collections.Generic; using System.Text;
static class H {
  static Assembly A; const BindingFlags ALL = BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  static void Main(string[] a) {
    A = Assembly.LoadFrom("r6.dll");
    string[] hostile = { "", " ", "zzz", "-1", "0", "999999999999999999999", new string('9', 400), "1e400", "NaN", "Infinity", "٢٢", "/٢٢", "١٢٣ MB", "١٠٠ Mbps", "٣٠ ms", "2001:db8::/٤٨",
      "::ffff:10.0.0.1", "::ffff:255.255.255.255/120", "010.001.001.001", "0x7f.1", "0x7f000001", "2130706433", "127.1", "http://user:pass@host:8443/x", "https://[::1]:99999/",
      "1.2.3 km at 5 GHz", ". wh", "1..5 Wh", "1.2.3 kWh", "12 V 9 Ah", "3 x 12 V 9 Ah", "5.8.8 GHz at 2 km", "-0", "1,,2", "1.000", "4x٤", "٤x4 256qam", "1080p٦٠", "٢ 4k", "٤ disks",
      "2 4k, 1 call", "99.9%", "1 kilogram", "1 kg", "4096 x", "hex:zz", "‮", "a\nb", "%", "100%", "1e5", "+5", "0x", "0b", "0b" + new string('1',65), "C8h", "FFFFFFFFFFFFFFFFF",
      "1440p240", "8K", "4k60", "5 fps", "1 Tbps", "1000000 TB", "0.0000001 bps", "1 bps", "1 TB" };
    var calc = A.GetType("AdeeboMeter.Calc");
    var methods = calc.GetMethods(ALL).Where(m => m.ReturnType == typeof(List<string[]>) && m.GetParameters().All(p => p.ParameterType == typeof(string))).ToList();
    var rnd = new Random(5); var seen = new Dictionary<string,string>(); int calls = 0;
    foreach (var m in methods) {
      int n = m.GetParameters().Length;
      for (int it = 0; it < 4000; it++) {
        var args = new object[n]; for (int i = 0; i < n; i++) args[i] = hostile[rnd.Next(hostile.Length)];
        if (it < hostile.Length * n) { for (int i = 0; i < n; i++) args[i] = "1"; args[it % n] = hostile[it / n % hostile.Length]; if (m.Name=="Ups" && it%n!=0) args[0]="216 Wh"; }
        calls++;
        try { var r = (List<string[]>)m.Invoke(null, args); foreach (var row in r) foreach (var c in row) if (c != null && (c.Contains("NaN") || c.Contains("∞") || c.Contains("Infinity") || c.Contains("-9,223") || c.Contains("-9223"))) { string k = m.Name + " BADTEXT " ; if (!seen.ContainsKey(k)) { seen[k] = string.Join(" | ", args) + " => " + c; } } }
        catch (Exception e0) { var e = e0 is TargetInvocationException ? e0.InnerException : e0; string k = m.Name + " " + e.GetType().Name; if (!seen.ContainsKey(k)) seen[k] = string.Join(" | ", args.Select(x => "[" + (x.ToString().Length>30? x.ToString().Substring(0,30)+"...":x) + "]")) + " => " + e.Message; }
      }
    }
    Console.WriteLine("calls " + calls + " methods " + methods.Count);
    foreach (var kv in seen.OrderBy(k=>k.Key)) Console.WriteLine(kv.Key + " :: " + kv.Value);
  }
}
