using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Runtime.Serialization;
class P { static void Main(string[] a){
  var A=Assembly.LoadFrom(a[0]); var bf=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  var FV=A.GetType("AdeeboMeter.FullView",true); var Pt=A.GetType("AdeeboMeter.Points",true);
  Func<string,int> val = id => (int)Pt.GetMethod("Value").Invoke(null,new object[]{id});
  var fv=FormatterServices.GetUninitializedObject(FV);
  var tools=(IEnumerable)FV.GetMethod("Tools2",bf).Invoke(fv,null);
  var ids=tools.Cast<object>().Select(t=>(string)t.GetType().GetField("Id",bf).GetValue(t)).ToList();
  int tl=ids.Sum(val); var replay=(HashSet<string>)Pt.GetField("Replay").GetValue(null); int rp=ids.Count(replay.Contains)*3;
  var K=A.GetType("AdeeboMeter.Knowledge",true); var learn=((IEnumerable)K.GetMethod("Learn").Invoke(null,null)).Cast<object>().ToList();
  var idx=learn[0].GetType().GetProperties().FirstOrDefault(p=>p.GetIndexParameters().Length==1);
  int lr=learn.Count*2; int ck=learn.Count(r=>(string)idx.GetValue(r,new object[]{"check"})!="")*2;
  var labs=(string[][])FV.GetField("Labs",bf).GetValue(null); var goals=(IDictionary)FV.GetField("LabGoals",bf).GetValue(null);
  int lb=labs.Count(l=>l[0]!="quiz"&&goals.Contains(l[0]))*4;
  var pages=(string[][])FV.GetField("PtPages",bf).GetValue(null); int pg=pages.Sum(p=>val(p[0]));
  var paths=(Array)FV.GetField("Paths",bf).GetValue(null); int pa=paths.Length*10;
  var LG=A.GetType("AdeeboMeter.LearnGames",true); var gk=((IEnumerable<string>)LG.GetMethod("Keys").Invoke(null,null)).ToList(); int gm=gk.Sum(val);
  int tot=tl+rp+lr+ck+lb+8+3+pg+pa+gm;
  Console.WriteLine("tools "+ids.Count+" -> "+tl+", replay "+rp+", learn "+learn.Count+" topics -> "+lr+", checks "+ck+", labs "+lb+", quiz 8, journey 3, pages "+pg+", paths "+pa+", games "+gk.Count+" -> "+gm);
  var sec=ids.Count; Console.WriteLine("labs total "+labs.Length+" pages "+pages.Length+" nonEarnTools "+ids.Count(i=>val(i)==0)); Console.WriteLine("everything there is to earn: "+tot+"   Points.Max = "+Pt.GetField("Max").GetValue(null));
}}
