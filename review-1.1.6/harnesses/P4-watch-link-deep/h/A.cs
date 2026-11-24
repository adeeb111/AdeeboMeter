using System; using System.Reflection; using System.Linq; using System.Collections.Generic; using System.Globalization; using System.Threading;
class A { static void Main(){
 var asm=Assembly.LoadFrom("r6.dll"); var BF=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 var MD=asm.GetType("AdeeboMeter.MarketData");
 // build a valet answer with FX USD up to N days ago
 Func<int,string> body = lagDays => { var sb=new System.Text.StringBuilder("{\"observations\":["); var last=DateTime.Today.AddDays(-lagDays); for(int i=30;i>=0;i--){ var d=last.AddDays(-i); if(d.DayOfWeek==DayOfWeek.Saturday||d.DayOfWeek==DayOfWeek.Sunday) continue; sb.Append("{\"d\":\""+d.ToString("yyyy-MM-dd")+"\",\"FXUSDCAD\":{\"v\":\"1.40\"}},"); } sb.Length--; sb.Append("]}"); return sb.ToString(); };
 var parse=MD.GetMethod("ParseValet",BF); var take=MD.GetMethod("Take",BF); var stale=MD.GetMethod("Stale",BF);
 var hist=(System.Collections.IDictionary)MD.GetField("hist",BF).GetValue(null);
 var et=(DateTime)asm.GetType("AdeeboMeter.Trading").GetMethod("EasternNow",BF).Invoke(null,null);
 Console.WriteLine("Eastern now: "+et+" "+et.DayOfWeek);
 for(int lag=0;lag<=7;lag++){ hist.Clear(); take.Invoke(null,new object[]{"FX:USD",parse.Invoke(null,new object[]{body(lag)})}); var s=MD.GetMethod("Get",BF).Invoke(null,new object[]{"FX:USD"}); var ld=(DateTime)s.GetType().GetProperty("LastDay").GetValue(s,null); Console.WriteLine("last value "+ld.ToString("ddd yyyy-MM-dd")+" -> stale="+stale.Invoke(null,new object[]{"FX:USD"})); }
 var LH=asm.GetType("AdeeboMeter.LineHours");
 foreach(var c in new[]{"en-CA","fr-FR","fr-CA","de-DE","es-ES","ar-SA","zh-CN","bn-BD"}){ try{ Thread.CurrentThread.CurrentCulture=new CultureInfo(c); var ci=CultureInfo.CurrentCulture; Console.WriteLine(c+" AM='"+ci.DateTimeFormat.AMDesignator+"' PM='"+ci.DateTimeFormat.PMDesignator+"' Clock(8)='"+LH.GetMethod("Clock",BF).Invoke(null,new object[]{8})+"' Clock(20)='"+LH.GetMethod("Clock",BF).Invoke(null,new object[]{20})+"' month="+new DateTime(2026,11,1).ToString("MMMM yyyy",ci)+" cal="+ci.Calendar.GetType().Name);}catch(Exception e){Console.WriteLine(c+" "+e.Message);} }
}}
