using System; using System.Runtime.InteropServices; using System.Threading;
class C {
 [DllImport("libX11.so.6")] static extern IntPtr XOpenDisplay(IntPtr n);
 [DllImport("libX11.so.6")] static extern int XFlush(IntPtr d);
 [DllImport("libX11.so.6")] static extern int XKeysymToKeycode(IntPtr d, IntPtr ks);
 [DllImport("libXtst.so.6")] static extern int XTestFakeMotionEvent(IntPtr d,int s,int x,int y,ulong delay);
 [DllImport("libXtst.so.6")] static extern int XTestFakeButtonEvent(IntPtr d,uint b,bool p,ulong delay);
 [DllImport("libXtst.so.6")] static extern int XTestFakeKeyEvent(IntPtr d,uint k,bool p,ulong delay);
 // args: sequence of "x,y" (click), "m:x,y" (move), "k:keysymhex" , "w:ms"
 static void Main(string[] a){ var d=XOpenDisplay(IntPtr.Zero); if(d==IntPtr.Zero){Console.WriteLine("no display");return;}
  foreach(var s in a){
   if(s.StartsWith("w:")){Thread.Sleep(int.Parse(s.Substring(2)));continue;}
   if(s.StartsWith("k:")){uint kc=(uint)XKeysymToKeycode(d,(IntPtr)Convert.ToInt32(s.Substring(2),16)); XTestFakeKeyEvent(d,kc,true,0);XTestFakeKeyEvent(d,kc,false,0);XFlush(d);Thread.Sleep(100);continue;}
   bool mv=s.StartsWith("m:"); var p=(mv?s.Substring(2):s).Split(','); XTestFakeMotionEvent(d,-1,int.Parse(p[0]),int.Parse(p[1]),0); XFlush(d); Thread.Sleep(150);
   if(!mv){XTestFakeButtonEvent(d,1,true,0);XFlush(d);Thread.Sleep(60);XTestFakeButtonEvent(d,1,false,0);XFlush(d);Thread.Sleep(200);}
  } }
}
