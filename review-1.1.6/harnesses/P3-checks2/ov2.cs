using System;
class O{ static long W(DateTime t){ long when=0; foreach(var v in new[]{t.Year,t.Month,(int)t.DayOfWeek,t.Day,t.Hour,t.Minute,t.Second}) {} foreach(var v in new[]{t.Year,t.Month,t.Day,t.Hour,t.Minute,t.Second}) when=when*10000+v; return when; }
 static void Main(){ int neg=0,tot=0; for(var d=new DateTime(2025,1,1);d<new DateTime(2028,1,1);d=d.AddHours(7)){ tot++; if(W(d)<0) neg++; } Console.WriteLine(neg+" of "+tot+" sampled times 2025-2027 give a negative 'when' (never > -1)"); } }
