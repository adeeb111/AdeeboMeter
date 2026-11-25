using System;
using System.IO;

namespace AdeeboMeter
{
    static class NetPath
    {
        // B1 (review 1.1.6): a path that would make Windows open a network share - \\server\share, //server/share, /\server,
        // \\?\UNC\..., or a device path \\.\ / \\?\ - is refused before any file call. Opening one sends the user's Windows
        // sign-in (NTLM) to that server. Anything that can't be resolved is refused too.
        public static bool Is(string p)
        {
            p = (p ?? "").Trim().Trim('"');
            if (p.Length >= 2 && (p[0] == '\\' || p[0] == '/') && (p[1] == '\\' || p[1] == '/')) return true;
            try { string f = Path.GetFullPath(p); return f.StartsWith(@"\\", StringComparison.Ordinal) || f.StartsWith("//", StringComparison.Ordinal); }
            catch (Exception e) { if (e is ArgumentException || e is NotSupportedException || e is PathTooLongException || e is System.Security.SecurityException) return true; throw; }
        }
    }
}
