using System; using System.Text;
namespace AdeeboMeter { static class QH { static void Main(string[] a) { foreach (var s in System.IO.File.ReadAllLines(a[0])) { var m = Qr.Encode(s); var sb = new StringBuilder(); int n = m.GetLength(0); for (int y = 0; y < n; y++) { for (int x = 0; x < n; x++) sb.Append(m[y, x] ? '1' : '0'); sb.Append('/'); } Console.WriteLine(sb); } } } }
