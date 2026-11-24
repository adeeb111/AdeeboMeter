using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Drawing; using System.Windows.Forms; using System.Threading; using System.IO;
class W { static Assembly A; static BindingFlags BF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object F(object o,string n){ var t=o as Type ?? o.GetType(); var f=t.GetField(n,BF); return f.GetValue(o is Type?null:o);}
 static object Call(object o,string n,params object[] a){ var t=o as Type ?? o.GetType(); var m=t.GetMethods(BF).First(x=>x.Name==n && x.GetParameters().Length==a.Length); return m.Invoke(o is Type?null:o,a);}
 static int errs; static string cur="";
 static void Pump(int ms){ var until=DateTime.UtcNow.AddMilliseconds(ms); while(DateTime.UtcNow<until){ Application.DoEvents(); Thread.Sleep(20);} }
 [STAThread] static void Main(string[] a){ A=Assembly.LoadFrom(a[0]); string dir=a[1]; string langs=a.Length>2?a[2]:"en"; string shots=a.Length>3?a[3]:"";
  var P=A.GetType("AdeeboMeter.Program"); P.GetField("Dir",BF).SetValue(null,dir);
  Application.ThreadException+=(s,e)=>{errs++; Console.WriteLine("UI ERROR at "+cur+": "+e.Exception.ToString().Split('\n').Take(8).Aggregate((x,y)=>x+"\n"+y));};
  AppDomain.CurrentDomain.UnhandledException+=(s,e)=>{Console.WriteLine("FATAL at "+cur+": "+e.ExceptionObject);};
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
  var ctx=Activator.CreateInstance(A.GetType("AdeeboMeter.AppCtx"),true);
  Pump(3000);
  var L=A.GetType("AdeeboMeter.L"); var D=A.GetType("AdeeboMeter.Draw");
  foreach(var lang in langs.Split(',')){
   var Cfg=A.GetType("AdeeboMeter.Cfg"); Cfg.GetField("Lang",BF).SetValue(null,lang); Call(L,"Use",lang); Call(D,"UseLanguageFonts");
   var old=F(ctx,"full") as Form; if(old!=null){ old.Close(); Pump(300);} 
   Call(ctx,"OpenFull"); var fv=(Form)F(ctx,"full"); fv.WindowState=FormWindowState.Normal; fv.Bounds=new Rectangle(0,0,1366,768); Pump(1500);
   Console.WriteLine("=== "+lang+" code="+F(L,"Code"));
   var pages=new[]{"overview","conn","link","procs","live","guide","dest","health","toolbox","travel","replay","changes","learn","windows","settings","about","points","gamer","stream","trade","sports","phone","shell","security","outlook","lab"};
   foreach(var p in pages){ if(p=="phone") continue; cur=lang+"/"+p; try{ Call(fv,"Debug",p,null,null); Pump(700); fv.Refresh(); if(shots!="" && shots.Split(',').Contains(p)){ using(var b=new Bitmap(fv.Width,fv.Height)){ fv.DrawToBitmap(b,new Rectangle(0,0,fv.Width,fv.Height)); b.Save("walk-"+lang+"-"+p+".png"); } } }catch(Exception e){errs++; Console.WriteLine("EXC "+cur+": "+(e.InnerException??e).ToString().Split('\n')[0]);} }
   // toolbox sections and every tool, opened not run
   Call(fv,"Debug","toolbox",null,null); Pump(300);
   var secs=(string[][])F(fv.GetType(),"Sections"); foreach(var s in secs){ cur=lang+"/section "+s[0]; try{ Call(fv,"ShowSection",s[0]); Pump(150); fv.Refresh(); }catch(Exception e){errs++; Console.WriteLine("EXC "+cur+": "+(e.InnerException??e).Message);} }
   var tools=(IList)Call(fv,"Tools2"); int n=0;
   foreach(var t in tools){ string id=(string)t.GetType().GetField("Id").GetValue(t); cur=lang+"/tool "+id; try{ Call(fv,"OpenTool",id); Pump(60); fv.Refresh(); n++; }catch(Exception e){errs++; Console.WriteLine("EXC "+cur+": "+(e.InnerException??e).Message);} }
   Console.WriteLine("tools opened: "+n+"  errors so far: "+errs);
  }
  Environment.Exit(0);
 }}
