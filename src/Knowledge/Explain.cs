// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AdeeboMeter
{
    // ================================================================== What's running: a plain-English guide to the processes on this PC
    // Static text only. Nothing here closes, changes or uploads anything - AdeeboMeter only explains.
    static class Explain
    {
        public sealed class Entry { public string Friendly, Cat, Tag, What, How, Why, Without; public int Close; }
        // Close: 0 safe to close · 1 leave it, OK to restart if it misbehaves · 2 don't - Windows needs it · 3 managed by IT
        public static readonly string[] CloseText =
        {
            "Safe to close. It's an app - you only lose what's open in it.",
            "Leave it be. If it misbehaves, closing it is usually OK - see what you lose below first.",
            "Don't close it. Windows needs it - closing it can sign you out or crash the PC.",
            "Managed by your IT team. It's protected on purpose, so leave it running."
        };
        public static readonly string[] CloseShort = { "safe to close", "leave it", "Windows needs it", "managed by IT" };
        public const string Core = "Windows core", Sec = "Security", It = "Company / IT tools", App = "Your apps", Help = "Windows helpers", Other = "Other";
        public static readonly string[] CatOrder = { App, It, Sec, Help, Core, Other };

        // The guide itself lives in knowledge/processes.txt and knowledge/terms.txt - plain text anyone can read and improve.
        // It ships compressed inside the exe and is unpacked only when something asks for it, then let go again (see Knowledge).
        public static Entry Find(string raw) { Entry e; return raw != null && Knowledge.Guide("processes").TryGetValue(raw, out e) ? e : null; }
        public static int Count { get { return Knowledge.Guide("processes").Values.Distinct().Count(); } }
        public static Entry Term(string key) { Entry e; return key != null && Knowledge.Guide("terms").TryGetValue(key, out e) ? e : null; }
        public static IEnumerable<Entry> AllTerms { get { return Knowledge.Guide("terms").Values.Distinct(); } }




        // ---------- what the file itself says, for anything not in the guide (read once per path, cached)
        static readonly Dictionary<string, string[]> fileInfo = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        public static string[] FileFacts(string path)   // { description, company, product, where }
        {
            if (string.IsNullOrEmpty(path)) return new[] { "", "", "", "" };
            lock (fileInfo)
            {
                string[] f; if (fileInfo.TryGetValue(path, out f)) return f;
                string d = "", c = "", p = "";
                try { var v = FileVersionInfo.GetVersionInfo(path); d = Program.Plain(v.FileDescription).Trim(); c = Program.Plain(v.CompanyName).Trim(); p = Program.Plain(v.ProductName).Trim(); } catch { }
                f = new[] { d, c, p, Where(path) };
                if (fileInfo.Count > 400) fileInfo.Clear();
                fileInfo[path] = f; return f;
            }
        }
        public static string Where(string path)
        {
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows), pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), temp = Path.GetTempPath();
            Func<string, bool> under = delegate(string root) { return root != "" && path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase); };
            if (under(temp) || (win != "" && under(Path.Combine(win, "Temp"))) || path.IndexOf("\\Temp\\", StringComparison.OrdinalIgnoreCase) >= 0) return "temp";   // C:\Windows\Temp is a temp folder, not "ships with Windows"
            if (under(win)) return "windows";
            if (under(pf) || under(pf86)) return "programfiles";
            if (under(local) || under(roam) || under(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))) return "user";
            return "elsewhere";
        }
        public static string WhereText(string where)
        {
            switch (where)
            {
                case "windows": return "It lives in the Windows folder, so it ships with Windows (or a driver).";
                case "programfiles": return "It lives in Program Files - installed for everyone on this PC, usually by IT.";
                case "user": return "It lives in your own profile (just for you, no admin needed). Normal for Teams, OneDrive, Zoom and tools you built yourself - but if you don't recognise it, it's worth asking IT.";
                case "temp": return "It's running from a Temp folder. Installers do this briefly; anything that stays there is worth reporting to IT.";
                case "elsewhere": return "It runs from an unusual folder. If you don't recognise it, ask IT.";
                default: return "Windows didn't let a normal user see where it lives - usually a protected system process.";
            }
        }
    }

    // Shared painter for an explanation: title, tagline, then What / How / Why / Without (and "Can I close it?" for processes).
    static class ExplainPaint
    {
        public static float Paint(Graphics g, Explain.Entry e, float x, float y, float w, bool compact)
        {
            float S = Draw.S;
            using (var ink = new SolidBrush(Theme.Ink)) using (var blue = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue))
            {
                var th = Draw.Measure(e.Friendly, compact ? Draw.fHead : Draw.fTitle, (int)w).Height;
                if (g != null) g.DrawString(e.Friendly, compact ? Draw.fHead : Draw.fTitle, ink, new RectangleF(x, y, w, th + 4)); y += th + 2 * S;
                if (!string.IsNullOrEmpty(e.Tag)) { var tg = Draw.Measure(e.Tag, Draw.fBodyB, (int)w).Height; if (g != null) g.DrawString(e.Tag, Draw.fBodyB, blue, new RectangleF(x, y, w, tg + 4)); y += tg + 8 * S; }
            }
            bool proc = e.Close >= 0 || e.Cat != "term";
            var rows = new List<string[]>();
            if (!string.IsNullOrEmpty(e.What)) rows.Add(new[] { proc ? "WHAT IT DOES" : "WHAT IT IS", e.What });
            if (!string.IsNullOrEmpty(e.How)) rows.Add(new[] { "HOW IT WORKS", e.How });
            if (!string.IsNullOrEmpty(e.Why)) rows.Add(new[] { proc ? "WHY YOU NEED IT" : "WHY IT MATTERS", e.Why });
            if (!string.IsNullOrEmpty(e.Without)) rows.Add(new[] { proc ? (e.Close >= 0 ? "IF IT WEREN'T THERE" : "WHAT TO DO") : "WHEN IT LOOKS BAD", e.Without });
            if (e.Close >= 0 && e.Close < Explain.CloseText.Length) rows.Add(new[] { "CAN I CLOSE IT?", Explain.CloseText[e.Close], e.Close == 0 ? "0" : e.Close == 2 ? "2" : "1" });
            foreach (var r in rows)
            {
                using (var mb = new SolidBrush(Theme.Muted)) if (g != null) g.DrawString(r[0], Draw.fTiny, mb, x, y);
                y += 16 * S;
                var font = compact ? Draw.fSmall : Draw.fBody;
                var h = Draw.Measure(r[1], font, (int)w).Height;
                using (var b = new SolidBrush(r.Length > 2 ? Theme.LvText(int.Parse(r[2], CultureInfo.InvariantCulture)) : Theme.Sub)) if (g != null) g.DrawString(r[1], font, b, new RectangleF(x, y, w, h + 4));
                y += h + (compact ? 6 : 10) * S;
            }
            return y;
        }
        public static float Section(Graphics g, string title, List<string[]> rows, float x, float y, float w)
        {
            float S = Draw.S;
            using (var mb = new SolidBrush(Theme.Muted)) using (var ink = new SolidBrush(Theme.Ink)) using (var line = new Pen(Theme.Line))
            {
                g.DrawLine(line, x, y, x + w, y); y += 10 * S;
                g.DrawString(title, Draw.fTiny, mb, x, y); y += 18 * S;
                foreach (var r in rows)
                {
                    g.DrawString(r[0], Draw.fSmall, mb, x, y);
                    var h = Draw.Measure(r[1], Draw.fSmall, (int)(w - 150 * S)).Height;
                    g.DrawString(r[1], Draw.fSmall, ink, new RectangleF(x + 150 * S, y, w - 150 * S, h + 4));
                    y += Math.Max(18 * S, h) + 6 * S;
                }
            }
            return y;
        }
    }
}
