using System; using System.IO; using System.Linq; using System.Reflection; using System.Collections.Generic; using System.Text; using System.Diagnostics;
static class F {
  static Assembly A; const BindingFlags ALL = BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  static MethodInfo M(string t, string m, int n) { return A.GetType("AdeeboMeter."+t).GetMethods(ALL).First(x => x.Name == m && x.GetParameters().Length == n); }
  static void Main(string[] a) {
    A = Assembly.LoadFrom("r6.dll"); var rnd = new Random(11); var seen = new Dictionary<string,string>();
    var hello = A.GetType("AdeeboMeter.CertChain+Hello"); var parse = M("CertChain","Parse",2);
    var mdns = M("DevId","ParseMdns",3); var dhcp = M("Lab","DhcpText",2); var attrs = M("EngPack","DecodeAttrs",1); var rdse = M("MoreTools","ParseRootDse",2); var csr = M("CertTools","Csr",1); var json = M("Json","Parse",1); var oid2 = M("CertChain","Oid2",1);
    var addT = A.GetType("AdeeboMeter.DevId").GetNestedType("Info", ALL);
    Action<string,Action> run = (k, f) => { try { f(); } catch (Exception e0) { var e = e0 is TargetInvocationException ? e0.InnerException : e0; string key = k + " " + e.GetType().Name; if (!seen.ContainsKey(key)) seen[key] = e.Message + " @ " + e.StackTrace.Split('\n').FirstOrDefault(); } };
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < 200000; i++) {
      int len = rnd.Next(0, i % 50 == 0 ? 3000 : 120); var b = new byte[len]; rnd.NextBytes(b);
      if (len > 4 && rnd.Next(3) == 0) { b[0] = (byte)new[]{2,11,14,0x30,0x04}[rnd.Next(5)]; }
      run("Parse", () => parse.Invoke(null, new object[] { b, Activator.CreateInstance(hello) }));
      run("DhcpText", () => dhcp.Invoke(null, new object[] { new[]{1,3,51,15,119,121,43}[rnd.Next(7)], b }));
      if (len >= 20) { var r = (byte[])b.Clone(); r[2] = (byte)(len >> 8); r[3] = (byte)len; run("DecodeAttrs", () => attrs.Invoke(null, new object[] { r })); }
      run("RootDse", () => rdse.Invoke(null, new object[] { b, len }));
      if (len > 2) { var o = (byte[])b.Clone(); o[0] = 6; run("Oid2", () => oid2.Invoke(null, new object[] { o })); }
      if (i % 20 == 0) run("Csr", () => csr.Invoke(null, new object[] { "-----BEGIN CERTIFICATE REQUEST-----\n" + Convert.ToBase64String(new byte[]{0x30,0x82,(byte)(len>>8),(byte)len}.Concat(b).ToArray()) + "\n-----END CERTIFICATE REQUEST-----" }));
      if (i % 5 == 0) { var s = new string(b.Select(x => "{}[]\":,0123456789.eE-tfn \\u"[x % 26]).ToArray()); run("Json", () => json.Invoke(null, new object[] { s })); }
    }
    Console.WriteLine("done " + sw.ElapsedMilliseconds + " ms");
    foreach (var kv in seen) Console.WriteLine(kv.Key + " :: " + kv.Value);
    // deep json
    run("JsonDeep", () => json.Invoke(null, new object[] { new string('[', 100000) }));
    // Json memory: a 3.9 MB looking-glass-like document
    var sb = new StringBuilder("{\"data\":{\"rrcs\":["); for (int r = 0; sb.Length < 3900000; r++) sb.Append("{\"rrc\":\"RRC" + r + "\",\"location\":\"X\",\"peers\":[{\"asn_origin\":\"13335\",\"as_path\":\"3333 1103 13335\",\"community\":\"1103:1 1103:2\",\"last_updated\":\"2026-11-24T00:00:00\",\"prefix\":\"1.1.1.0/24\",\"peer\":\"193.0.0.56\",\"origin\":\"IGP\",\"next_hop\":\"193.0.0.56\",\"latest_time\":\"2026-11-24T00:00:00\"}]},");
    sb.Append("{}]}}"); string big = sb.ToString(); GC.Collect(); long m0 = GC.GetTotalMemory(true);
    var obj = json.Invoke(null, new object[] { big }); long m1 = GC.GetTotalMemory(true);
    Console.WriteLine("json text " + big.Length + " chars; parsed object graph ~" + (m1 - m0) / 1048576 + " MB (plus the 4M-char string " + big.Length * 2 / 1048576 + " MB) " + (obj != null));
    foreach (var kv in seen.Where(k => k.Key.StartsWith("JsonDeep"))) Console.WriteLine(kv.Key + " :: " + kv.Value);
    var ip = M("Lab","Addr",1); foreach (var t in new[]{"192.168.001.010","010.000.000.005","1.1.1.010"}) Console.WriteLine("Lab.Addr " + t + " -> " + ip.Invoke(null, new object[]{t}));
  }
}
