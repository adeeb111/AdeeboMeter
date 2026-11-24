using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

static class P
{
    static Assembly A;
    const BindingFlags ALL = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static Type T(string n) { var t = A.GetType("AdeeboMeter." + n); if (t == null) throw new Exception("no type " + n); return t; }
    public static object S(string type, string m, params object[] a)
    {
        var mi = T(type).GetMethods(ALL).Where(x => x.Name == m && x.GetParameters().Length == a.Length).First();
        try { return mi.Invoke(null, a); } catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static void SetF(Type t, string f, object v) { var fi = t.GetField(f, ALL); fi.SetValue(null, v); }
    static void Dump(object o) { foreach (var r in (List<string[]>)o) Console.WriteLine("  " + string.Join(" | ", r)); }
    static object Run() { var r = Activator.CreateInstance(T("Toolkit+Run"), true); return r; }

    static void Main(string[] args)
    {
        string app = args[0]; A = Assembly.LoadFrom(Path.Combine(app, "r4.dll"));
        SetF(T("Program"), "Dir", app);
        string w = args[1];
        if (w == "addroot") { using (var st = new X509Store(StoreName.Root, StoreLocation.CurrentUser)) { st.Open(OpenFlags.ReadWrite); st.Add(new X509Certificate2(args[2])); } Console.WriteLine("added"); }
        if (w == "chain") Dump(S("CertChain", "Check", args[2], args.Length > 3 ? args[3] : ""));
        if (w == "inspect") Dump(S("CertTools", "Inspect", args[2]));
        if (w == "https") Dump(S("CertChain", "Https", args[2], Run()));
        if (w == "dn") Console.WriteLine(S("CertKeys", "Dn", args[2]));
        if (w == "ver") Dump(S("Security", "VersionVerdict", args[2], args[3], args[4], DateTime.Parse(args[5])));
        if (w == "hello") { var b = (byte[])S("CertChain", "ClientHello", args[2], 0x0303); Console.WriteLine(BitConverter.ToString(b)); }
        if (w == "burns")
        {
            var wt = T("Fun+Week"); var wk = Activator.CreateInstance(wt, true);
            wt.GetField("Smooth").SetValue(wk, 99.95); wt.GetField("Loss").SetValue(wk, 0.0);
            var g = (string)S("Fun", "Grade", wk); Console.WriteLine("grade " + g);
            foreach (var s in (List<string>)S("Fun", "Burns", wk, g, "Acme Broadband Ltd")) Console.WriteLine("  " + s);
            wt.GetField("Smooth").SetValue(wk, 80.0); g = (string)S("Fun", "Grade", wk); Console.WriteLine("grade " + g);
            foreach (var s in (List<string>)S("Fun", "Burns", wk, g, "Acme Broadband Ltd")) Console.WriteLine("  " + s);
        }
    }
}
