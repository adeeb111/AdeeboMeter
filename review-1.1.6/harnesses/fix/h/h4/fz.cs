using System; using System.IO; using System.Linq; using System.Reflection; using System.Collections.Generic; using System.Diagnostics;
static class F {
  static Assembly A; const BindingFlags ALL = BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  static object S(string t, string m, params object[] a) { var mi = A.GetType("AdeeboMeter."+t).GetMethods(ALL).First(x => x.Name == m && x.GetParameters().Length == a.Length); try { return mi.Invoke(null, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
  static void W32(List<byte> l, uint v) { l.Add((byte)v); l.Add((byte)(v>>8)); l.Add((byte)(v>>16)); l.Add((byte)(v>>24)); }
  static void W16(List<byte> l, int v) { l.Add((byte)v); l.Add((byte)(v>>8)); }
  // classic pcap -> pcapng (SHB, IDB with if_tsresol=9, EPB per packet; every 3rd as SPB)
  static byte[] ToNg(byte[] c) {
    var o = new List<byte>(); W32(o,0x0A0D0D0A); W32(o,28); W32(o,0x1A2B3C4D); W16(o,1); W16(o,0); W32(o,0xFFFFFFFF); W32(o,0xFFFFFFFF); W32(o,28);
    W32(o,1); W32(o,32); W16(o,1); W16(o,0); W32(o,65535); W16(o,9); W16(o,1); o.Add(9); o.Add(0); o.Add(0); o.Add(0); W16(o,0); W16(o,0); W32(o,32);
    int p = 24, n = 0;
    while (p + 16 <= c.Length) {
      uint s = BitConverter.ToUInt32(c,p), us = BitConverter.ToUInt32(c,p+4); int incl = (int)BitConverter.ToUInt32(c,p+8); p += 16;
      var d = c.Skip(p).Take(incl).ToArray(); p += incl; int pad = (4 - incl % 4) % 4;
      if (n++ % 3 == 2) { int bl = 16 + incl + pad; W32(o,3); W32(o,(uint)bl); W32(o,(uint)incl); o.AddRange(d); o.AddRange(new byte[pad]); W32(o,(uint)bl); continue; }
      ulong ts = (ulong)s * 1000000000UL + (ulong)us * 1000UL; int blen = 32 + incl + pad;
      W32(o,6); W32(o,(uint)blen); W32(o,0); W32(o,(uint)(ts>>32)); W32(o,(uint)ts); W32(o,(uint)incl); W32(o,(uint)incl); o.AddRange(d); o.AddRange(new byte[pad]); W32(o,(uint)blen);
    }
    return o.ToArray();
  }
  static void Main(string[] a) {
    A = Assembly.LoadFrom(Path.Combine(a[0], "r4.dll"));
    A.GetType("AdeeboMeter.Program").GetField("Dir", ALL).SetValue(null, a[0]);
    int iters = int.Parse(a[1]); var rnd = new Random(int.Parse(a[2]));
    var names = ((string[][])A.GetType("AdeeboMeter.Replay").GetField("Samples", ALL).GetValue(null)).Where(x => x[2] != "cert").Select(x => x[0]).ToList();
    var seeds = new List<byte[]>();
    foreach (var n in names) { var b = (byte[])S("Replay","Build", n); seeds.Add(b); seeds.Add(ToNg(b)); }
    // sanity: ng seeds parse the same count
    foreach (var n in names) { var b = (byte[])S("Replay","Build", n); var c1 = ((System.Collections.IList)S("Pcap","Parse", b)).Count; var c2 = ((System.Collections.IList)S("Pcap","Parse", ToNg(b))).Count; if (c1 != c2) Console.WriteLine("NG MISMATCH " + n + " " + c1 + " vs " + c2); }
    var errs = new Dictionary<string,int>(); var ex1 = new Dictionary<string,string>(); var sw = Stopwatch.StartNew(); long slow = 0;
    for (int i = 0; i < iters; i++) {
      var b = (byte[])seeds[rnd.Next(seeds.Count)].Clone();
      int k = rnd.Next(1, 12);
      for (int j = 0; j < k; j++) {
        int m = rnd.Next(5); int at = rnd.Next(b.Length);
        if (m == 0) b[at] = (byte)rnd.Next(256);
        else if (m == 1) b[at] = (byte)(b[at] ^ (1 << rnd.Next(8)));
        else if (m == 2) b[at] = new byte[]{0,0xFF,0x7F,0x80,1}[rnd.Next(5)];
        else if (m == 3 && at + 4 <= b.Length) { var v = new uint[]{0,0xFFFFFFFF,0x7FFFFFFF,0x80000000,0xFFFF,0x10000}[rnd.Next(6)]; b[at]=(byte)v; b[at+1]=(byte)(v>>8); b[at+2]=(byte)(v>>16); b[at+3]=(byte)(v>>24);} 
        else b = b.Take(Math.Max(1, at)).ToArray();
      }
      var t0 = sw.ElapsedMilliseconds;
      foreach (var stage in new[] { "parse", "dhcp", "ra", "eap", "icmp" }) {
        try {
          var pk = S("Pcap","Parse", b);
          var rows = new List<string[]> { new[] { "3", "Read", "", "", "" } };
          if (stage == "dhcp") S("Replay","DhcpRows", pk, rows);
          if (stage == "ra") S("Replay","RaRows", pk, rows);
          if (stage == "eap") S("Replay","EapRows", pk, rows);
          if (stage == "icmp") S("Replay","IcmpRowsFromPackets", pk, rows);
        } catch (Exception e) {
          string key = stage + ":" + e.GetType().Name + ":" + (e.StackTrace ?? "").Split('\n').FirstOrDefault(l => l.Contains("AdeeboMeter"));
          int c; errs.TryGetValue(key, out c); errs[key] = c + 1; if (!ex1.ContainsKey(key)) { ex1[key] = Convert.ToBase64String(b); File.WriteAllBytes(Path.Combine(a[3], "crash-" + ex1.Count + ".pcap"), b); }
        }
      }
      if (sw.ElapsedMilliseconds - t0 > 1000) slow++;
    }
    Console.WriteLine("iters " + iters + " time " + sw.Elapsed.TotalSeconds.ToString("0") + " s, slow " + slow);
    foreach (var kv in errs) Console.WriteLine(kv.Value + "  " + kv.Key);
  }
}
