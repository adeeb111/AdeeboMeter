using System; using System.Drawing;
class F{static void Main(string[] a){foreach(var n in a){try{var f=new Font(n,12f);Console.WriteLine(n+" -> "+f.FontFamily.Name+" h="+f.GetHeight());}catch(Exception e){Console.WriteLine(n+" ERR "+e.Message);}}}}
