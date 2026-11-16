// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    // Send to PC / send to phone, over the same sealed session (nothing new to trust): a file or a bit of text either way.
    //
    //   sender: fput {id, name, size}  →  receiver asks "Accept?" (No by default)  →  fok / fno
    //   sender: fdat {id, n, d (base64, ≤ 30 KB)} × N, at most 20 a second and 24 ahead of the last fack  →  fend {id, n, size}
    //   receiver: fack {id, n} every 8 pieces; either side: fcancel {id}
    //
    // Every piece is sealed with its own counter and MAC (RemoteCrypto), so a changed, missing, replayed or reordered piece
    // ends the session - the size and the piece count are checked at the end as well. A file from the phone lands in
    // AdeeboMeter's own folder as a .part until it's complete, then moves to Downloads\AdeeboMeter with its name and Windows'
    // "downloaded" mark; the receiver confirms with fgot {id}. The relay only ever sees sealed pieces.
    sealed partial class Remote
    {
        public const long MaxFile = 200L * 1024 * 1024; const int Piece = 30000, Ahead = 24;
        // hooks for the PC's window: accept a file / show text that came from the phone, and say where a file went
        public Func<string, string, long, bool> AskFile; public Action<string> GotText; public Action<string> FileSaved; public Action<string> SendState;

        sealed class In { public string Id = "", Name = "", Tmp = "", Dir = ""; public long Size, Got; public int Next; public FileStream Fs; }
        sealed class Out { public string Id = ""; public volatile int Acked = -1; public volatile bool Ok, No, Cancel, Got; public readonly ManualResetEventSlim Ev = new ManualResetEventSlim(false); }
        readonly Dictionary<string, In> ins = new Dictionary<string, In>(); readonly Dictionary<string, Out> outs = new Dictionary<string, Out>(); readonly object fk = new object();

        // AM-CG-372: Windows' own Downloads folder, wherever it was moved to; the old guess only if Windows doesn't say
        public static string DropDir { get { return Path.Combine(Downloads(), "AdeeboMeter"); } }
        static string Downloads()
        {
            IntPtr p = IntPtr.Zero;
            try { if (Native.SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out p) == 0 && p != IntPtr.Zero) { string s = Marshal.PtrToStringUni(p); if (!string.IsNullOrEmpty(s)) return s; } }
            catch (EntryPointNotFoundException) { } catch (DllNotFoundException) { }
            finally { if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p); }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
        // AM-CG-374: an unfinished file is kept in AdeeboMeter's own folder (not Downloads) and anything left there by a
        // crash or a killed app is deleted the first time a transfer starts in this run
        static string Staging { get { return Path.Combine(Program.DataDir, "incoming"); } }
        static int swept;
        static void Sweep()
        {
            if (Interlocked.Exchange(ref swept, 1) != 0) return;
            try { if (Directory.Exists(Staging)) foreach (var f in Directory.GetFiles(Staging, "*.part")) try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        void Files(Msg c)
        {
            string t = c.S("t"), id = Clip(c.S("id") ?? "", 24);
            switch (t)
            {
                case "text":
                    { string s = Clip(c.S("s") ?? "", 4000).Trim(); if (s != "" && GotText != null) GotText(s); Seal("{\"t\":\"note\",\"msg\":\"Sent to the PC.\"}"); return; }
                case "fput":
                    {
                        long size; long.TryParse(c.S("size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
                        string name = SafeName(c.S("name"));
                        if (id == "" || size <= 0 || size > MaxFile) { Seal("{\"t\":\"fno\",\"id\":\"" + J(id) + "\",\"why\":\"" + (size > MaxFile ? "over 200 MB" : "empty") + "\"}"); return; }
                        lock (fk) if (ins.Count >= 2) { Seal("{\"t\":\"fno\",\"id\":\"" + J(id) + "\",\"why\":\"one at a time\"}"); return; }
                        // ask on the PC (No is the default); the answer may take a while, so not on the receive thread
                        Task.Factory.StartNew(() =>
                        {
                            bool yes = AskFile != null && AskFile(PhoneName, name, size);
                            if (!yes || St != State.Paired) { Seal("{\"t\":\"fno\",\"id\":\"" + J(id) + "\",\"why\":\"not accepted on the PC\"}"); return; }
                            try
                            {
                                Sweep(); Directory.CreateDirectory(DropDir); Directory.CreateDirectory(Staging);
                                var f = new In { Id = id, Name = name, Size = size, Dir = DropDir, Tmp = Path.Combine(Staging, Guid.NewGuid().ToString("N").Substring(0, 12) + ".part") };
                                f.Fs = new FileStream(f.Tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                                try { File.SetAttributes(f.Tmp, FileAttributes.Hidden); } catch (IOException) { }
                                lock (fk) ins[id] = f;
                                Seal("{\"t\":\"fok\",\"id\":\"" + J(id) + "\"}");
                            }
                            catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("remote file: " + e.Message); Seal("{\"t\":\"fno\",\"id\":\"" + J(id) + "\",\"why\":\"couldn't save on the PC\"}"); }
                        });
                        return;
                    }
                case "fdat":
                    {
                        In f; lock (fk) ins.TryGetValue(id, out f); if (f == null) return;
                        int n; int.TryParse(c.S("n"), NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
                        byte[] d; try { d = Convert.FromBase64String(c.S("d") ?? ""); } catch (FormatException) { Drop(f, "a piece was damaged"); return; }
                        if (n != f.Next || d.Length == 0 || d.Length > Piece || f.Got + d.Length > f.Size) { Drop(f, "pieces arrived out of order"); return; }
                        try { f.Fs.Write(d, 0, d.Length); } catch (IOException e) { Drop(f, "the disk refused it: " + e.Message); return; }
                        f.Got += d.Length; f.Next++;
                        if (f.Next % 8 == 0) Seal("{\"t\":\"fack\",\"id\":\"" + J(id) + "\",\"n\":" + (f.Next - 1).ToString(CultureInfo.InvariantCulture) + "}");
                        return;
                    }
                case "fend":
                    {
                        In f; lock (fk) ins.TryGetValue(id, out f); if (f == null) return;
                        int n; int.TryParse(c.S("n"), NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
                        if (n != f.Next || f.Got != f.Size) { Drop(f, "the file arrived incomplete"); return; }
                        string final;
                        try
                        {
                            f.Fs.Dispose(); f.Fs = null; final = Unique(Path.Combine(f.Dir, f.Name));
                            File.SetAttributes(f.Tmp, FileAttributes.Normal); File.Move(f.Tmp, final); FileCrypt.MarkFromInternet(final);
                        }
                        catch (Exception e) { if (e is OutOfMemoryException) throw; Drop(f, "couldn't save it: " + e.Message); return; }
                        lock (fk) ins.Remove(id);
                        Seal("{\"t\":\"fdone\",\"id\":\"" + J(id) + "\",\"msg\":\"" + J("Saved on the PC: " + final) + "\"}");
                        if (FileSaved != null) FileSaved(final);
                        return;
                    }
                case "fcancel":
                    {
                        In f; lock (fk) ins.TryGetValue(id, out f); if (f != null) { Drop(f, null); return; }
                        Out o; lock (fk) outs.TryGetValue(id, out o); if (o != null) { o.Cancel = true; o.Ev.Set(); }
                        return;
                    }
                case "fok": case "fno": case "fack": case "fgot":
                    {
                        Out o; lock (fk) outs.TryGetValue(id, out o); if (o == null) return;
                        if (t == "fok") o.Ok = true; else if (t == "fno") o.No = true; else if (t == "fgot") o.Got = true; else { int n; if (int.TryParse(c.S("n"), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > o.Acked) o.Acked = n; }
                        o.Ev.Set(); return;
                    }
            }
        }
        void Drop(In f, string why)
        {
            lock (fk) ins.Remove(f.Id);
            try { if (f.Fs != null) f.Fs.Dispose(); } catch (IOException) { }
            try { if (File.Exists(f.Tmp)) { File.SetAttributes(f.Tmp, FileAttributes.Normal); File.Delete(f.Tmp); } } catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; }
            if (why != null) Seal("{\"t\":\"err\",\"msg\":\"" + J("The file didn't arrive - " + why + ".") + "\"}");
        }
        void FilesEnd()
        {
            List<In> l; lock (fk) { l = ins.Values.ToList(); foreach (var o in outs.Values) { o.Cancel = true; o.Ev.Set(); } }
            foreach (var f in l) Drop(f, null);
        }
        static string SafeName(string n)
        {
            n = Path.GetFileName((n ?? "").Replace('/', '\\')) ?? "";
            foreach (char ch in Path.GetInvalidFileNameChars()) n = n.Replace(ch, '_');
            n = n.Trim().TrimEnd('.', ' '); if (n == "" || n.StartsWith(".", StringComparison.Ordinal)) n = "from-phone" + n;
            if (System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(n), @"^(CON|PRN|AUX|NUL|COM\d|LPT\d)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) n = "_" + n;
            return n.Length > 120 ? n.Substring(0, 100) + Path.GetExtension(n) : n;
        }
        static string Unique(string path)
        {
            if (!File.Exists(path)) return path;
            string d = Path.GetDirectoryName(path), b = Path.GetFileNameWithoutExtension(path), e = Path.GetExtension(path);
            for (int i = 2; i < 1000; i++) { string p = Path.Combine(d, b + " (" + i + ")" + e); if (!File.Exists(p)) return p; }
            return Path.Combine(d, b + " " + Guid.NewGuid().ToString("N").Substring(0, 6) + e);
        }

        // ---------- from the PC to the phone
        public void SendText(string s) { s = Clip(s ?? "", 4000); if (s.Trim() != "" && St == State.Paired) Seal("{\"t\":\"text\",\"s\":\"" + J(s) + "\"}"); }
        public void SendFile(string path)
        {
            Task.Factory.StartNew(() =>
            {
                var o = new Out { Id = RemoteCrypto.B64u(RemoteCrypto.Random(9)) }; Action<string> say = m => { var h = SendState; if (h != null) h(m); };
                try
                {
                    var fi = new FileInfo(path); if (!fi.Exists) { say(L.T("That file isn't there any more.")); return; }
                    if (fi.Length == 0 || fi.Length > 100L * 1024 * 1024) { say(L.T("Phones take files up to 100 MB from here.")); return; }
                    lock (fk) { if (outs.Count > 0) { say(L.T("One file at a time - wait for the last one.")); return; } outs[o.Id] = o; }
                    Seal("{\"t\":\"fput\",\"id\":\"" + o.Id + "\",\"name\":\"" + J(fi.Name) + "\",\"size\":" + fi.Length.ToString(CultureInfo.InvariantCulture) + "}");
                    say(L.T("Waiting for the phone to accept..."));
                    var until = DateTime.UtcNow.AddMinutes(2);
                    while (!o.Ok && !o.No && !o.Cancel && DateTime.UtcNow < until && St == State.Paired) { o.Ev.Wait(1000); o.Ev.Reset(); }
                    if (!o.Ok) { say(o.No ? L.T("The phone said no.") : L.T("The phone didn't answer.")); return; }
                    var buf = new byte[Piece]; int n = 0; long sent = 0;
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        int r;
                        while ((r = fs.Read(buf, 0, buf.Length)) > 0)
                        {
                            // AM-CG-370: a phone that stops acknowledging for a minute ends this transfer, not the session
                            var idle = DateTime.UtcNow; int seen = o.Acked;
                            while (n - o.Acked > Ahead && !o.Cancel && St == State.Paired) { o.Ev.Wait(1000); o.Ev.Reset(); if (o.Acked != seen) { seen = o.Acked; idle = DateTime.UtcNow; } else if ((DateTime.UtcNow - idle).TotalSeconds > 60) { o.Cancel = true; Seal("{\"t\":\"fcancel\",\"id\":\"" + o.Id + "\"}"); say(L.T("The phone stopped answering - the file wasn't sent.")); return; } }
                            if (o.Cancel || St != State.Paired) { say(L.T("Stopped.")); return; }
                            Seal("{\"t\":\"fdat\",\"id\":\"" + o.Id + "\",\"n\":" + n.ToString(CultureInfo.InvariantCulture) + ",\"d\":\"" + Convert.ToBase64String(buf, 0, r) + "\"}");
                            n++; sent += r; if (n % 20 == 0) say(L.T("Sending...") + " " + (100 * sent / fi.Length) + "%");
                            Thread.Sleep(50);   // at most 20 pieces a second: the relay allows 40 a second for everything
                        }
                    }
                    Seal("{\"t\":\"fend\",\"id\":\"" + o.Id + "\",\"n\":" + n.ToString(CultureInfo.InvariantCulture) + ",\"size\":" + sent.ToString(CultureInfo.InvariantCulture) + "}");
                    // AM-CG-369: "sent" is not "the phone has it" - wait for the phone to say so
                    say(L.T("Sent - waiting for the phone to confirm..."));
                    var by = DateTime.UtcNow.AddSeconds(60);
                    while (!o.Got && !o.Cancel && DateTime.UtcNow < by && St == State.Paired) { o.Ev.Wait(1000); o.Ev.Reset(); }
                    say(o.Got ? L.T("The phone has it:") + " " + fi.Name : L.T("Sent, but the phone didn't confirm it arrived:") + " " + fi.Name);
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; say(L.T("Couldn't send it:") + " " + e.Message); }
                finally { lock (fk) outs.Remove(o.Id); }
            }, TaskCreationOptions.LongRunning);
        }
    }
}
