using System; using System.IO; using System.IO.Compression; using System.Reflection; using System.Collections; using System.Collections.Generic; using System.Linq; using System.Diagnostics; using System.Globalization;
class G {
  static Assembly A; static Type T(string n){ return A.GetType("AdeeboMeter."+n, true); }
  static BindingFlags BF=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
  static void Main(string[] a){
    A=Assembly.LoadFrom("/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/build6/r6.dll");
    string data=(string)T("Program").GetProperty("DataDir").GetValue(null); Console.WriteLine("data "+data);
    string hist=Path.Combine(data,"history"); Directory.CreateDirectory(hist);
    var rnd=new Random(1); var ci=CultureInfo.InvariantCulture;
    if (a[0]=="load") {
      int servers=int.Parse(a[1]);
      for(int d=0; d<7; d++){ var day=DateTime.Today.AddDays(-d); var lines=new List<string>();
        for(int m=0;m<1440;m++){ var t=TimeSpan.FromMinutes(m).ToString(@"hh\:mm\:ss");
          for(int s=0;s<servers;s++) if (s==0 || m%5==0) lines.Add(t+",valorant,S"+s+","+(30+rnd.Next(5))+","+(40+rnd.Next(5))+",0,1.5,60,"); }
        string f=Path.Combine(hist,"games-"+day.ToString("yyyyMMdd",ci)+".csv");
        if (d==0) File.WriteAllLines(f,lines); else using(var fs=File.Create(f+".gz")) using(var z=new GZipStream(fs,CompressionMode.Compress)) using(var w=new StreamWriter(z)) foreach(var l in lines) w.WriteLine(l);
      }
      var gh=T("GameHistory"); var load=gh.GetMethod("Load",BF);
      var sw=Stopwatch.StartNew(); var r=(IDictionary)load.Invoke(null,new object[]{"valorant"}); Console.WriteLine("Load ms="+sw.ElapsedMilliseconds+" servers="+r.Count);
      sw.Restart(); for(int i=0;i<3;i++) load.Invoke(null,new object[]{"valorant"}); Console.WriteLine("3 more Loads ms="+sw.ElapsedMilliseconds);
      // all-games read cost per Read
      sw.Restart(); for(int d=0; d<7; d++) gh.GetMethod("Read").Invoke(null,new object[]{DateTime.Today.AddDays(-d),"valorant"}); Console.WriteLine("7 Reads only ms="+sw.ElapsedMilliseconds);
    }
    if (a[0]=="report") {
      var gs=T("GameSession"); var s=Activator.CreateInstance(gs,true);
      gs.GetField("GameId").SetValue(s,"valorant"); gs.GetField("GameName").SetValue(s,"Valorant"); gs.GetField("Server").SetValue(s,"NA East"); gs.GetField("Start").SetValue(s,DateTime.Now.AddMinutes(-5));
      var secT=T("GameSession+Sec"); var add=gs.GetMethod("Add");
      for(int i=0;i<300;i++){ var sec=Activator.CreateInstance(secT); secT.GetField("T").SetValue(sec,DateTime.Now.AddSeconds(-300+i).AddMilliseconds(137)); secT.GetField("Game").SetValue(sec,30.0); secT.GetField("Router").SetValue(sec,3.0); secT.GetField("Signal").SetValue(sec,70); secT.GetField("Down").SetValue(sec,1.0); secT.GetField("Up").SetValue(sec,0.5); secT.GetField("Cpu").SetValue(sec,10.0); add.Invoke(s,new[]{sec}); }
      var spT=T("Spike"); var sp=Activator.CreateInstance(spT); spT.GetField("T").SetValue(sp,DateTime.Now.AddSeconds(-100)); spT.GetField("Game").SetValue(sp,"valorant"); spT.GetField("Server").SetValue(sp,"NA East"); spT.GetField("Blame").SetValue(sp,"your provider"); spT.GetField("Ms").SetValue(sp,120.0); spT.GetField("Normal").SetValue(sp,30.0);
      var lst=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(spT)); lst.Add(sp);
      Console.WriteLine(gs.GetMethod("Report").Invoke(s,new object[]{lst,"HomeNet"}));
    }
    if (a[0]=="valve") {
      File.WriteAllText(Path.Combine(data,"valve-servers.txt"),"{\"pops\":{\"iad\":{\"desc\":\"Sterling (Washington DC)\",\"relays\":[{\"ipv4\":\"155.133.252.1\"}]},\"fra\":{\"desc\":\"Frankfurt\",\"relays\":[{\"ipv4\":\"155.133.226.1\"}]}}}");
      File.WriteAllLines(Path.Combine(data,"games.txt"),new[]{"game|cs2|Counter-Strike 2|cs2","server|cs2|My match|found|203.0.113.9|-1|exact","server|cs2|LAN box|home|192.168.1.50|27015|exact"});
      var games=(IList)T("Games").GetProperty("All").GetValue(null);
      foreach(var g in games){ var id=(string)g.GetType().GetField("Id").GetValue(g); if(id!="cs2") continue; var sv=(IList)g.GetType().GetField("Servers").GetValue(g); foreach(var x in sv) Console.WriteLine("cs2 server: "+x.GetType().GetField("Name").GetValue(x)+" "+x.GetType().GetField("Host").GetValue(x)); }
    }
  }
}
