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
    // ================================================================== small data types
    sealed class Ring
    {
        readonly double[] a; int n, head; readonly object k = new object();
        public Ring(int size) { a = new double[size]; }
        public void Add(double v) { lock (k) { a[head] = v; head = (head + 1) % a.Length; if (n < a.Length) n++; } }
        double At(int back) { return a[((head - 1 - back) % a.Length + a.Length) % a.Length]; }
        public double Last { get { lock (k) return n == 0 ? double.NaN : At(0); } }
        public double[] Values(int max)
        {
            lock (k)
            {
                int c = Math.Min(n, max); double[] r = new double[c];
                for (int i = 0; i < c; i++) r[i] = At(c - 1 - i);
                return r;
            }
        }
        public double Avg(int last)
        {
            lock (k)
            {
                int m = Math.Min(last, n), c = 0; double s = 0;
                for (int i = 0; i < m; i++) { double v = At(i); if (!double.IsNaN(v)) { s += v; c++; } }
                return c == 0 ? double.NaN : s / c;
            }
        }
        public double Max(int last)
        {
            lock (k)
            {
                int m = Math.Min(last, n); double r = double.NaN;
                for (int i = 0; i < m; i++) { double v = At(i); if (!double.IsNaN(v) && (double.IsNaN(r) || v > r)) r = v; }
                return r;
            }
        }
        public double Sum(int last)
        {
            lock (k)
            {
                int m = Math.Min(last, n); double s = 0;
                for (int i = 0; i < m; i++) { double v = At(i); if (!double.IsNaN(v)) s += v; }
                return s;
            }
        }
        public double Jitter(int last)
        {
            lock (k)
            {
                int m = Math.Min(last, n), c = 0; double s = 0, prev = double.NaN;
                for (int i = m - 1; i >= 0; i--)
                {
                    double v = At(i); if (double.IsNaN(v)) continue;
                    if (!double.IsNaN(prev)) { s += Math.Abs(v - prev); c++; }
                    prev = v;
                }
                return c == 0 ? double.NaN : s / c;
            }
        }
    }

    sealed class AppStat { public string Name; public double Cpu; public long Mem; public int Count, Pid; }

    sealed class Conn
    {
        public string Key, Proc, Proto, Local, Remote, State, RemoteIp, Host = "", HostFrom = "";
        public int Pid, LocalPort, RemotePort;
    }

    sealed class Detail
    {
        public string Title = "", Note = "", TopTitle = "";
        public List<string[]> Lines = new List<string[]>(), Top = new List<string[]>();
    }

    // A small "what changed" log: only changes are recorded, kept for 2 hours (max 300 lines), in memory only.
    sealed class Timeline
    {
        public sealed class Ev { public DateTime T; public int Lv; public string Kind, Text; }
        readonly List<Ev> list = new List<Ev>(); readonly object k = new object();
        public void Add(int lv, string kind, string text)
        {
            lock (k)
            {
                var now = DateTime.Now;
                if (list.Count > 0 && list[list.Count - 1].Text == text && (now - list[list.Count - 1].T).TotalSeconds < 120) return;
                list.Add(new Ev { T = now, Lv = lv, Kind = kind, Text = text });
                History.Event(lv, kind, text);
                var cut = now.AddHours(-2); list.RemoveAll(e => e.T < cut);
                if (list.Count > 300) list.RemoveRange(0, list.Count - 300);
            }
            var ev = new Ev { T = DateTime.Now, Lv = lv, Kind = kind, Text = text };
            try { Alerts.OnEvent(ev); Calls.OnEvent(ev); } catch { }
        }
        public List<Ev> Recent(int n) { lock (k) { var r = list.Skip(Math.Max(0, list.Count - n)).ToList(); r.Reverse(); return r; } }
        public static string Ago(DateTime t)
        {
            var d = DateTime.Now - t;
            return d.TotalSeconds < 60 ? ((int)d.TotalSeconds) + " s" : d.TotalMinutes < 60 ? ((int)d.TotalMinutes) + " min" : ((int)d.TotalHours) + " h " + d.Minutes + " min";
        }
    }

    sealed class Snap
    {
        public string Hint = "";
        public List<Timeline.Ev> Recent = new List<Timeline.Ev>();
        public int Level; public string Headline = "Waking up... give me ten seconds, boss.", Chip = "All quiet"; public int Countdown;
        public int[] Path = { 3, 3, 3, 3, 3 };
        public string[] PathLabel = { "PC", "LAN", "ISP", "DNS", "Internet" }, PathMain = new string[5], PathSub = new string[5];
        public double Cpu = double.NaN, Mem = double.NaN, Disk = double.NaN, Down = double.NaN, Up = double.NaN, Ping, Jitter, LossPct, DnsMs;
        public double[] DownHist = Array.Empty<double>(), UpHist = Array.Empty<double>();
        public bool Call;
        public string MemText = "", DiskText = "";
        public Dictionary<string, Detail> Details = new Dictionary<string, Detail>();
        public List<string[]> Health = new List<string[]>();
        public List<double[]> HealthHist = new List<double[]>();
        public List<AppStat> TopMem = new List<AppStat>(), TopCpu = new List<AppStat>();
    }
}
