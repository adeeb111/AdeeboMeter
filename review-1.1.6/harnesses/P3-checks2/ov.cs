using System;
class O{ static long W(int y,int mo,int d,int h,int mi,int s){ long when=0; foreach(var v in new[]{y,mo,d,h,mi,s}) when=when*10000+v; return when; }
 static void Main(){ var a=W(2026,11,24,9,0,0); var b=W(2025,3,1,9,0,0); var c=W(2026,11,23,9,0,0); Console.WriteLine("2026-11-24 -> "+a+"\n2025-03-01 -> "+b+"\n2026-11-23 -> "+c+"\nnewer(2026-11-24) > older(2025-03-01)? "+(a>b)+"; 24 Nov > 23 Nov? "+(a>c)); } }
