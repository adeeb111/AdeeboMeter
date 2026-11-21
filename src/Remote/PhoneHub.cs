// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== Your phone: one place (Adeeb, 16 Nov - "Link icon ... merge and make it better")
    // What the "Your phone" page and the link button in the top line share with the session in AppCtx:
    //   the question a phone is asking (allow / remember / a file) - answered on the page, never a pop-up box,
    //   what came from the phone, and the last line about a file going out.
    // The relay thread asks and waits here; the page answers. No answer in 90 s = No, the safe side.
    static class PhoneHub
    {
        public static Action Start;            // AppCtx: a new code session, the page shown
        public static Action<string> Show;     // AppCtx: open the main window at the page (a phone is asking)
        static readonly object k = new object();
        static readonly List<Action> watchers = new List<Action>();
        public static void Watch(Action a) { lock (k) watchers.Add(a); }
        public static void Unwatch(Action a) { lock (k) watchers.Remove(a); }
        public static void Fire() { List<Action> l; lock (k) l = watchers.ToList(); foreach (var a in l) try { a(); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("phone link: " + e.Message); } }

        // ---- a question from the phone
        public sealed class Q { public string Kind = "", Phone = "", Text = "", Words = ""; public bool Answer; public readonly ManualResetEvent Done = new ManualResetEvent(false); public DateTime At = DateTime.UtcNow; }
        static Q pending;
        public static Q Pending { get { lock (k) return pending; } }
        public const int AskSeconds = 90;
        // called on the relay's thread - it waits for the answer on the page
        public static bool Ask(string kind, string phone, string text) { return Ask(kind, phone, text, ""); }
        public static bool Ask(string kind, string phone, string text, string words)
        {
            var q = new Q { Kind = kind, Phone = phone ?? "", Text = text ?? "", Words = words ?? "" };
            lock (k) { if (pending != null) { pending.Answer = false; pending.Done.Set(); } pending = q; }
            Fire(); var s = Show; if (s != null) try { s(kind); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("phone show: " + e.Message); }
            bool got = q.Done.WaitOne(TimeSpan.FromSeconds(AskSeconds));
            lock (k) if (pending == q) pending = null;
            Fire();
            return got && q.Answer;
        }
        public static void Answer(bool yes) { Q q; lock (k) { q = pending; pending = null; } if (q == null) return; q.Answer = yes; q.Done.Set(); Fire(); }

        // ---- what came from the phone (this run only - nothing kept on disk but the files you accepted)
        public sealed class Got { public DateTime T = DateTime.Now; public string Text = "", File = ""; }
        static readonly List<Got> inbox = new List<Got>();
        public static List<Got> Inbox { get { lock (k) return inbox.ToList(); } }
        public static void Add(Got g) { lock (k) { inbox.Insert(0, g); if (inbox.Count > 5) inbox.RemoveAt(inbox.Count - 1);   /* bug-hunt L-237: holds what the page shows */ } Fire(); }
        public static volatile string Xfer = "";   // "Sending report.pdf - 40%"
        public static volatile bool AutoClip;      // text from the phone onto this PC's clipboard - off unless ticked, this run only

        // ---- the link button's look: off / waiting / asking / linked
        public static string StateOf(out string label)
        {
            var r = Remote.Now; label = "";
            if (Pending != null) { label = L.T("Allow?"); return "ask"; }
            if (r == null || r.St == Remote.State.Ended) return "off";
            if (r.St == Remote.State.Paired) { var l = r.Left; label = (r.PhoneName != "" ? Short(r.PhoneName) : L.T("Phone")) + " · " + Math.Max(1, (int)Math.Ceiling(l.TotalMinutes)) + " " + L.T("min"); return "on"; }
            if (r.St == Remote.State.Asking) { label = L.T("Allow?"); return "ask"; }
            label = L.T("Waiting"); return "wait";
        }
        static string Short(string s) { s = s.Trim(); return s.Length > 14 ? s.Substring(0, 13) + "…" : s; }
    }
}
