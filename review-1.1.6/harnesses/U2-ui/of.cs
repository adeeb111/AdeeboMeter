using System; using System.Windows.Forms;
class P{[STAThread]static void Main(){var a=new Form(); var h=a.Handle; Console.WriteLine("after handle: "+Application.OpenForms.Count); var b=new Form(); b.Show(); Console.WriteLine("after show b: "+Application.OpenForms.Count+" first is a? "+(Application.OpenForms[0]==a));}}
