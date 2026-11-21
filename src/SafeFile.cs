// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.IO;
using System.Text;

namespace AdeeboMeter
{
    // ================================================================== small files, written whole or not at all (bug-hunt ML-2/ML-3/ML-4)
    // A hidden temp file in the same folder, renamed over the real name only once it's complete - a kill mid-write never
    // leaves half a certificate or key under its real name. Reads are capped so a huge file can't be pulled into memory.
    static class SafeFile
    {
        public const int Cap = 1 << 20;   // 1 MB: far more than any certificate, key or recipient file
        public static void WriteText(string path, string text) { WriteBytes(path, new UTF8Encoding(false).GetBytes(text)); }
        public static void WriteBytes(string path, byte[] data) { Write(path, data, true); }
        // like FileMode.CreateNew: an IOException if the name is already taken
        public static void WriteNew(string path, byte[] data) { Write(path, data, false); }
        static void Write(string path, byte[] data, bool replace)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path)), tmp = Path.Combine(dir, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".part");
            try
            {
                using (var f = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write)) { f.Write(data, 0, data.Length); f.Flush(true); }
                if (replace && File.Exists(path)) File.Replace(tmp, path, null, true); else File.Move(tmp, path);
                tmp = null;
            }
            finally { if (tmp != null) try { File.Delete(tmp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        public static string ReadText(string path, int cap = Cap)
        {
            var fi = new FileInfo(path); if (fi.Exists && fi.Length > cap) throw new IOException(fi.Name + " is over " + (cap >> 10) + " KB - not a key or certificate file");
            return File.ReadAllText(path);
        }
    }
}
