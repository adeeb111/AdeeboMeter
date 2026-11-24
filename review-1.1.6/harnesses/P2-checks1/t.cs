using System; using System.IO; using System.Linq; using System.Reflection; using System.Collections.Generic; using System.Text;
static class T {
  static Assembly A; const BindingFlags ALL = BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  static object S(string t, string m, params object[] a) { var mi = A.GetType("AdeeboMeter."+t).GetMethods(ALL).First(x => x.Name == m && x.GetParameters().Length == a.Length); try { return mi.Invoke(null, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
  static void Show(string label, Func<object> f) { try { var r = f(); var l = r as List<string[]>; if (l != null) Console.WriteLine(label + " => " + string.Join(" || ", l.Select(x => x[0] + ":" + x[1] + "=" + x[2]))); else Console.WriteLine(label + " => " + r); } catch (Exception e0) { var e = e0 is TargetInvocationException ? e0.InnerException : e0; Console.WriteLine(label + " => THROWS " + e.GetType().Name + ": " + e.Message); } }
  static void Main(string[] a) {
    A = Assembly.LoadFrom("r6.dll");
    Show("Cellular LTE 4x٤", () => S("Calc","Cellular","LTE","20 MHz","4x٤ 64qam",""));
    Show("Cellular NR ٤ layers", () => S("Calc","Cellular","NR","100 MHz","٤ layers","30"));
    Show("Ups ٣ x 12V 9Ah", () => S("Calc","Ups","٣ x 12 V 9 Ah","300 W",""));
    Show("Ups 1.2.3 kWh", () => S("Calc","Ups","1.2.3 kWh","300 W",""));
    Show("Bandwidth huge size tiny time", () => S("Calc","Bandwidth","999999 TB","","1 us"));
    Show("Units 1e21 bytes", () => S("Calc","Units","9999999999999999999999 B"));
    Show("AvgCost 400 digits", () => S("Calc","AvgCost",new string('9',400),""));
    Show("Pnl fees 400", () => S("Calc","Pnl","100","110","10",new string('9',310)));
    Show("ComConv kilogram", () => S("Calc","ComConv","1","kilogram",""));
    Show("ComConv imperial gallon", () => S("Calc","ComConv","1","imperial gallon",""));
    Show("Decode JWT arabic exp", () => { string h = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\"}")).TrimEnd('='); string b = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"exp\":١٧٠٠٠٠٠٠٠٠}")).TrimEnd('=').Replace('+','-').Replace('/','_'); return S("CertTools","Decode", h + "." + b + ".sig"); });
    Show("Lab.Split arabic", () => { string h; int p; var mi = A.GetType("AdeeboMeter.Lab").GetMethod("Split"); var args = new object[] { "١٢٧.٠.٠.١:80", 1, null, null }; var ok = mi.Invoke(null, args); return ok + " host=" + args[2] + " port=" + args[3]; });
    Show("Lab.Addr 010.0.0.010", () => S("Lab","Addr","10.0.0.010"));
    Show("Lab.Addr 0x7f.1", () => S("Lab","Addr","0x7f.1"));
    Show("Lab.Addr 2130706433", () => S("Lab","Addr","2130706433"));
    Show("Games.ValidHost 010.0.0.1", () => S("Games","ValidHost","010.0.0.1"));
    Show("Games.ValidHost 255.255.255.255", () => S("Games","ValidHost","255.255.255.255"));
    Show("Icmp.Resolve4 10.0.0.010", () => S("Icmp","Resolve4","10.0.0.010"));
    foreach (var t in new[] { "http://user:pass@0x7f.1/", "https://user:pass@example.com:8443/", "http://[::ffff:127.0.0.1]/", "::ffff:10.0.0.1", "[::ffff:10.0.0.1]:443", "010.1.1.1", "١٢٧.٠.٠.١", "example.com:+443", "example.com: 443", "http://a@b@0x7f.1/", "1.2.3.4:", "example.com.", "xn--bcher-kva.de", "bücher.de:8443", "http://2130706433/", "http://0177.0.0.1/" }) {
      Show("DestCheck.Parse " + t, () => { var mi = A.GetType("AdeeboMeter.DestCheck").GetMethod("Parse"); var args = new object[] { t, null, null, null, null }; var ok = mi.Invoke(null, args); return ok + " host=" + args[1] + " port=" + args[2] + " tls=" + args[3] + " err=" + args[4]; });
    }
  }
}
