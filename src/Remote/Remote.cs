// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    // Phone remote: watch this PC from your phone's browser - live speed, ping, game pings, trading - and start a check from
    // there while you play. One phone, 30 minutes, only after you press Allow here. Everything between the two is
    // encrypted end to end (RemoteCrypto); the relay in the middle only forwards it and keeps nothing.
    //
    //   PC ──"pc" socket──► relay room ◄──"phone" socket── phone page (adeebometer.com/remote#room.secret)
    //   phone: hi + its key (HMAC with the QR secret)  →  PC: Allow?  →  PC: ok + its key  →  sealed messages both ways
    //
    // The PC opens an outgoing connection only - it never listens (the self-test checks that).
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "End() disposes the socket, timer and keys; a session is never reused")]
    sealed partial class Remote
    {
        // the relay's address, set when it's deployed (tools/remote). Empty: the feature says it isn't set up.
        public static readonly string Relay = "wss://adeebometer-remote.orkoadeeb.workers.dev";
        public const string Page = "https://adeebometer.com/app#";   // the phone app (remote.html forwards there for older codes)
        public const int Minutes = 30;
        public static volatile Remote Now;                       // the one session, or null
        public static DateTime MuteUntil = DateTime.MinValue;   // "mute alerts for an hour" from the phone

        // what the phone may start: network checks that read, measure or test - nothing that sends crafted traffic on
        // your LAN, touches keys or files, or changes the PC
        // what the phone may start is decided by the Toolbox itself (FullView.RemoteOk): CanRun asks it, Catalog lists it

        public enum State { Connecting, Waiting, Asking, Paired, Ended }
        public volatile State St = State.Connecting; public volatile string Why = "";
        public readonly string Room, Link; public DateTime Started = DateTime.UtcNow; public string PhoneName = "";
        readonly byte[] secret, auth; readonly ClientWebSocket ws = new ClientWebSocket(); readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly Monitor M; IDisposable mine;   // the one-time key (an ECDiffieHellmanCng) - held as IDisposable so the class loads where CNG is missing (Wine)
        RemoteCrypto.Lane tx, rx; byte[] phonePk; readonly object sendLock = new object();
        System.Threading.Timer tick;

        // the PC side hooks: ask "Allow?" on the UI thread, run a check, note a change for the window and the widget
        public volatile string Words = "";   // the matching words while a phone is asking; "" = it's an older phone page that can't show them
        public Func<string, bool> Ask; public Func<string, bool> CanRun; public Func<List<string>> Catalog; public Action<string, string[], Toolkit.Run, Action<string, List<string[]>, List<string[]>>> RunTool; public Action Changed; public Action FeltSlow;
        // AM-CG-319/320: one check at a time from the phone, tagged with the phone's request id, stoppable from the phone
        volatile Toolkit.Run op; volatile string opRid = "";
        // the other way round (PhoneAsk.cs): questions this PC sent the phone, by request id, until it answers or the session ends
        readonly System.Collections.Concurrent.ConcurrentDictionary<string, Action<string, List<string[]>>> asks = new System.Collections.Concurrent.ConcurrentDictionary<string, Action<string, List<string[]>>>();
        public bool AskPhone(string k, string q, Action<string, List<string[]>> done) { string id; return AskPhone(k, q, done, out id); }
        public bool AskPhone(string k, string q, Action<string, List<string[]>> done, out string id)
        {
            id = null;
            if (St != State.Paired || tx == null || asks.Count >= 4) return false;
            id = RemoteCrypto.B64u(RemoteCrypto.Random(6)); asks[id] = done;
            Seal("{\"t\":\"ask\",\"rid\":\"" + id + "\",\"k\":\"" + J(Clip(k, 16)) + "\",\"q\":\"" + J(Clip(q, 200)) + "\"}");
            return true;
        }

        // bug-hunt M-13: a question the phone never answered (asleep, out of coverage) gives its slot back
        public void ForgetAsk(string id) { Action<string, List<string[]>> gone; if (id != null) asks.TryRemove(id, out gone); }

        Remote(Monitor m)
        {
            M = m; secret = RemoteCrypto.Random(32); auth = RemoteCrypto.AuthKey(secret);
            Room = RemoteCrypto.B64u(RemoteCrypto.Random(16)); Link = Page + Room + "." + RemoteCrypto.B64u(secret);
        }
        // a remembered phone (RemoteKnown.cs): the room and the handshake keyed by that phone's own key; no QR, no question -
        // you said yes when you remembered it. The PC waits up to 25 minutes, then a new room.
        public readonly RemotePhones.P Known; public readonly long Slot; public readonly int Attempt;
        Remote(Monitor m, RemotePhones.P p, long slot, int attempt)
        {
            M = m; Known = p; Slot = slot; Attempt = attempt; secret = p.Key; auth = RemoteCrypto.AuthKey(secret);
            Room = RemotePhones.Room(p.Key, slot, attempt); Link = ""; PhoneName = p.Name;
        }
        public static Remote Standby(Monitor m, RemotePhones.P p, long slot, int attempt)
        {
            var r = new Remote(m, p, slot, attempt);
            if (Relay == "") { r.St = State.Ended; r.Why = "The phone relay isn't set up yet."; return r; }
            Task.Factory.StartNew(r.Run, TaskCreationOptions.LongRunning);
            return r;
        }
        public Func<string, bool> AskRemember;
        public Func<string, byte[]> Peek;   // screen peek: page -> JPEG (AppCtx draws it off-screen)
        int peekBusy; DateTime peekAt = DateTime.MinValue;   // bug-hunt ML-16
        public static readonly HashSet<string> PeekPages = new HashSet<string> { "overview", "link", "procs", "health", "changes", "travel", "toolbox", "guide", "gamer", "stream", "trade", "dest", "security", "replay" };
        public static Remote Start(Monitor m)
        {
            var old = Now; if (old != null) Task.Factory.StartNew(() => old.End("a new session started"));   // bug-hunt L-232: its goodbye (up to 2 s) never holds the window
            var r = new Remote(m); Now = r;
            if (Relay == "") { r.St = State.Ended; r.Why = "The phone relay isn't set up yet."; return r; }
            Task.Factory.StartNew(r.Run, TaskCreationOptions.LongRunning);
            return r;
        }
        public bool Active { get { return St == State.Paired; } }
        // bug-hunt ML-109/110/128: before pairing, the room's own wait (2 minutes for a code, 25 for a remembered phone - the relay
        // closes it then); once paired, the session's 30 minutes, counted from pairing (the relay counts the same)
        public TimeSpan Left { get { bool waiting = St == State.Connecting || St == State.Waiting; var l = (waiting ? Started.AddMinutes(Known != null ? 25 : 2) : Started.AddMinutes(Minutes)) - DateTime.UtcNow; return l < TimeSpan.Zero ? TimeSpan.Zero : l; } }

        void Run()
        {
            try
            {
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                ws.ConnectAsync(new Uri(Relay + "/room/" + Room + "?role=pc" + (Known != null ? "&wait=long" : "")), cts.Token).Wait(TimeSpan.FromSeconds(15));
                if (ws.State != WebSocketState.Open) { End("couldn't reach the relay"); return; }
                St = State.Waiting; Note();
                tick = new System.Threading.Timer(_ => Tick(), null, 1000, 1000);
                var buf = new byte[70 * 1024];
                while (St != State.Ended && ws.State == WebSocketState.Open)
                {
                    var sb = new StringBuilder(); WebSocketReceiveResult res; var dec = Encoding.UTF8.GetDecoder(); var cb = new char[buf.Length];   /* L-1: one decoder per message, so a character split across frames survives */
                    do
                    {
                        var t = ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token); t.Wait(cts.Token); res = t.Result;
                        if (res.MessageType == WebSocketMessageType.Close) { End("the phone or the relay closed the session"); return; }
                        int nc = dec.GetChars(buf, 0, res.Count, cb, 0, res.EndOfMessage); sb.Append(cb, 0, nc); if (sb.Length > 66 * 1024) { End("a message was too big"); return; }
                    } while (!res.EndOfMessage);
                    Got(sb.ToString());
                }
            }
            catch (Exception e)
            {
                if (e is OutOfMemoryException) throw;
                bool ours = St == State.Ended;   // bug-hunt L-222: our own End() cancelling the read isn't worth a log line
                // bug-hunt L-233: Windows without the encryption the link needs (CNG) says so, not "connection lost"
                bool noCng = e is PlatformNotSupportedException || e is System.Security.Cryptography.CryptographicException || (e is AggregateException && ((AggregateException)e).InnerException is PlatformNotSupportedException);
                if (!ours) { End(noCng ? "this Windows can't do the encryption the phone link needs (CNG is missing or turned off)" : "connection lost"); Program.Log("remote: " + e.Message); }
            }
        }

        void Got(string msg)
        {
            var j = Msg.Of(msg); if (j == null) { if (St == State.Paired) Program.Log("remote: a message that isn't JSON - ignored, like an unsealed one"); return; }   // bug-hunt L-258: logged, as ML-24's unsealed ones are
            if (St == State.Waiting && j.S("t") == "hi")
            {
                byte[] pk, mac; try { pk = RemoteCrypto.Unb64u(j.S("pk")); mac = RemoteCrypto.Unb64u(j.S("mac")); } catch (FormatException) { return; }
                if (pk.Length != 65 || !RemoteCrypto.Same(mac, RemoteCrypto.Hmac(auth, Encoding.ASCII.GetBytes("hi"), pk))) return;   // didn't scan this QR
                var me = new ECDiffieHellmanCng(256); mine = me; var myPk = RemoteCrypto.Raw(me); phonePk = pk;
                // a phone that can show the matching words gets this PC's key now, before the question (RemoteWords.cs)
                Words = "";
                // bug-hunt L-41: a new phone must show the matching words - a page too old to do that is turned away, not let in on Allow alone
                if (Known == null && j.S("w") != "1") { me.Dispose(); mine = null; Send("{\"t\":\"no\"}"); End("the phone's page is out of date - reload it on the phone and scan again"); return; }
                if (Known == null && j.S("w") == "1")
                {
                    int wv = j.S("w2") == "1" ? 2 : 1;   // bug-hunt H-1: six words when the phone page knows them
                    Words = RemoteWords.Of(secret, pk, myPk, wv);
                    Send("{\"t\":\"key\",\"wv\":" + wv + ",\"pk\":\"" + RemoteCrypto.B64u(myPk) + "\",\"mac\":\"" + RemoteCrypto.B64u(RemoteCrypto.Hmac(auth, Encoding.ASCII.GetBytes("key"), pk, myPk)) + "\"}");
                }
                St = State.Asking; if (Known == null) PhoneName = Clip(j.S("dev"), 40); else { string dv = Clip(j.S("dev") ?? "", 40).Trim(); if (dv != "" && dv != PhoneName) { PhoneName = dv; RemotePhones.Rename(Known.Id, dv); } }   /* L-240: a remembered phone's new name is kept */ Note();
                // bug-hunt L-235 (by design): what the phone sends while the question is open waits behind it and runs only after Allow -
                // the same phone, the same yes; a No ends the session and none of it runs
                bool yes = Known != null || (Ask != null && Ask(PhoneName));   // a remembered phone proved its key: you already said yes
                if (!yes || St != State.Asking) { me.Dispose(); mine = null; Send("{\"t\":\"no\"}"); End("you said no"); return; }
                var z = RemoteCrypto.SharedHash(me, pk);
                tx = new RemoteCrypto.Lane(secret, z, 1); rx = new RemoteCrypto.Lane(secret, z, 2);
                Send("{\"t\":\"ok\",\"v\":" + Proto.ToString(CultureInfo.InvariantCulture) + ",\"pk\":\"" + RemoteCrypto.B64u(myPk) + "\",\"mac\":\"" + RemoteCrypto.B64u(RemoteCrypto.Hmac(auth, Encoding.ASCII.GetBytes("ok"), pk, myPk)) + "\"}");
                mine.Dispose(); mine = null;   // the one-time private key goes as soon as the session keys exist
                Started = DateTime.UtcNow;   // the 30 minutes run from pairing (bug-hunt ML-110 - the relay starts its clock at the same moment)
                St = State.Paired; Note(); SendLive(); try { var cat = Catalog != null ? Catalog() : null; if (cat != null) foreach (var part in cat) Seal(part); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("remote catalog: " + e.Message); } return;
            }
            if (St != State.Paired || rx == null) return;
            // bug-hunt ML-24: an unsealed message (a phone page that reloaded and says hello again) is ignored, not fatal -
            // only a sealed one that fails to open means someone is tampering, and that still closes the session
            if (j.S("s") == null) { Program.Log("remote: unsealed '" + Clip(j.S("t") ?? "", 12) + "' after pairing - ignored"); return; }
            string plain = rx.Open(j.S("s"));
            if (plain == null) { End("a message didn't check out - the session was closed to be safe"); return; }
            var c = Msg.Of(plain); if (c == null) return;
            switch (c.S("t"))
            {
                case "run":
                    {
                        string id = c.S("id") ?? "", rid = Clip(c.S("rid") ?? "", 16).Replace("\"", "").Replace("\\", "");
                        if (id != "feltslow" && (CanRun == null || !CanRun(id))) { Seal("{\"t\":\"err\",\"rid\":\"" + rid + "\",\"msg\":\"That check can't be started from the phone.\"}"); return; }
                        if (id == "feltslow") { if (FeltSlow != null) FeltSlow(); Seal("{\"t\":\"note\",\"msg\":\"Marked: felt slow. The answer is on the PC.\"}"); return; }
                        var inl = c.A("in") ?? new List<string>();
                        if (inl.Count > 4 || inl.Any(x => (x ?? "").Length > 200)) { Seal("{\"t\":\"err\",\"rid\":\"" + rid + "\",\"msg\":\"A box is too long (200 characters at most) - nothing was run.\"}"); return; }   // AM-CG-364
                        var a = inl.ToArray();
                        if (op != null) { Seal("{\"t\":\"err\",\"rid\":\"" + rid + "\",\"msg\":\"A check is still running on the PC - stop it first or wait for it.\"}"); return; }
                        var run = new Toolkit.Run { Deadline = DateTime.UtcNow.AddMinutes(6) }; op = run; opRid = rid;
                        if (RunTool != null) RunTool(id, a, run, (title, rows, hops) => { if (op == run) { op = null; opRid = ""; } SendResult(id, rid, title, rows, hops); });
                        else op = null;
                        return;
                    }
                // screen peek (Adeeb, 16 Nov): one of AdeeboMeter's own pages, drawn off-screen on the PC, sent as a JPEG in pieces.
                // Only AdeeboMeter's pages - never the desktop or other apps.
                case "peek":
                    {
                        string rid = Clip(c.S("rid") ?? "", 16).Replace("\"", "").Replace("\\", ""), page = Clip(c.S("page") ?? "", 16);
                        if (Peek == null || !PeekPages.Contains(page)) { Seal("{\"t\":\"peek\",\"rid\":\"" + rid + "\",\"err\":\"That page can't be shown.\"}"); return; }
                        // bug-hunt ML-16: one picture at a time, at most one every 2 seconds, and never over 2 MB
                        if ((DateTime.UtcNow - peekAt).TotalSeconds < 2 || Interlocked.CompareExchange(ref peekBusy, 1, 0) != 0) { Seal("{\"t\":\"peek\",\"rid\":\"" + rid + "\",\"err\":\"Still sending the last picture - try again in a moment.\"}"); return; }
                        peekAt = DateTime.UtcNow;
                        Task.Factory.StartNew(() =>
                        {
                          try
                          {
                            byte[] jpg = null; try { jpg = Peek(page); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("peek: " + e.Message); }
                            if (jpg != null && jpg.Length > 2 * 1024 * 1024) jpg = null;
                            if (jpg == null || jpg.Length == 0) { Seal("{\"t\":\"peek\",\"rid\":\"" + rid + "\",\"err\":\"The PC couldn't draw that page.\"}"); return; }
                            const int P = 24000; int of = (jpg.Length + P - 1) / P;
                            for (int n = 0; n < of && St == State.Paired; n++)
                            {
                                int len = Math.Min(P, jpg.Length - n * P);
                                Seal("{\"t\":\"peek\",\"rid\":\"" + rid + "\",\"n\":" + n.ToString(CultureInfo.InvariantCulture) + ",\"of\":" + of.ToString(CultureInfo.InvariantCulture) + ",\"d\":\"" + Convert.ToBase64String(jpg, n * P, len) + "\"}");
                                Thread.Sleep(60);
                            }
                          }
                          finally { Interlocked.Exchange(ref peekBusy, 0); }
                        });
                        return;
                    }
                case "mute": { int m; if (!int.TryParse(c.S("m"), out m)) m = 0; m = Math.Max(0, Math.Min(240, m));   /* bug-hunt L-221: the phone is told what was really set */ MuteUntil = DateTime.UtcNow.AddMinutes(Math.Max(0, Math.Min(240, m))); Seal("{\"t\":\"note\",\"msg\":\"" + (m > 0 ? "Alerts muted for " + m + " minutes." : "Alerts back on.") + "\"}"); return; }
                case "stop": { var o = op; if (o != null && (c.S("rid") ?? "") == opRid) { o.Stop = true; Seal("{\"t\":\"note\",\"msg\":\"Stopping the check on the PC...\"}"); } return; }
                case "fput": case "fok": case "fno": case "fdat": case "fack": case "fend": case "fcancel": case "fgot": case "text": Files(c); return;
                // "Remember this PC" from the phone: asked here (No by default); the phone gets its own key, sealed
                case "remember":
                    if (Known != null) { Seal("{\"t\":\"note\",\"msg\":\"This phone is already remembered.\"}"); return; }
                    Task.Factory.StartNew(() =>
                    {
                        bool yes = AskRemember != null && AskRemember(PhoneName);
                        if (!yes || St != State.Paired) { Seal("{\"t\":\"rememberno\"}"); return; }
                        var p = RemotePhones.Add(PhoneName);
                        Seal("{\"t\":\"remembered\",\"id\":\"" + p.Id + "\",\"key\":\"" + RemoteCrypto.B64u(p.Key) + "\",\"pc\":\"" + J(Clip(Environment.MachineName, 40)) + "\"}");
                    });
                    return;
                case "forget": { string id = Clip(c.S("id") ?? "", 24); bool gone = id.Length > 0 && RemotePhones.Remove(id); Seal("{\"t\":\"forgot\",\"id\":\"" + J(id) + "\",\"ok\":" + (gone ? "true" : "false") + "}"); return; }   // AM-CG-329: only the phone it names, and say whether it was here
                // the phone's answer to an ask: rows come flat (level, name, value, advice, ...) - only text, clipped, at most 40
                case "pres":
                    {
                        Action<string, List<string[]>> d; if (!asks.TryRemove(c.S("rid") ?? "", out d)) return;
                        var f = c.A("r") ?? new List<string>(); var rows = new List<string[]>();
                        for (int i = 0; i + 3 < f.Count && rows.Count < 40; i += 4) { string lv = f[i] == "0" || f[i] == "1" || f[i] == "2" ? f[i] : "3"; rows.Add(new[] { lv, Clip(f[i + 1], 80), Clip(f[i + 2], 300), Clip(f[i + 3], 300), "i" }); }
                        string up = c.S("up"); if (up == "1" || up == "0") rows.Insert(0, new[] { "3", "\u0001up", up, "", "i" });   // bug-hunt ML-119
                        try { d(Clip(c.S("head") ?? "", 160), rows); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("remote ask: " + e.Message); }
                        return;
                    }
                case "bye": End("the phone ended the session"); return;
            }
        }

        int ticking;   // bug-hunt L-231: a slow tick (a big send) is never overlapped by the next one
        void Tick()
        {
            if (Interlocked.CompareExchange(ref ticking, 1, 0) != 0) return;
            try { TickOnce(); } finally { Interlocked.Exchange(ref ticking, 0); }
        }
        void TickOnce()
        {
            if (St == State.Ended) return;
            if (DateTime.UtcNow - Started > TimeSpan.FromMinutes(Minutes)) { End("30 minutes are up"); return; }
            if (St == State.Waiting && DateTime.UtcNow - Started > TimeSpan.FromMinutes(Known != null ? 25 : 2)) { End(Known != null ? "a new room" : "no phone joined within 2 minutes"); return; }
            if (St == State.Paired) { SendLive(); if ((DateTime.UtcNow - mktAt).TotalMinutes >= 10) { mktAt = DateTime.UtcNow; SendMarkets(); } }
            Note();
        }

        // ---------- what the phone sees, once a second
        void SendLive()
        {
            var s = M.Latest; var sb = new StringBuilder("{\"t\":\"live\"");
            Num(sb, "down", s.Down); Num(sb, "up", s.Up); Num(sb, "ping", s.Ping); Num(sb, "jit", s.Jitter); Num(sb, "loss", s.LossPct); Num(sb, "gw", s.GwMs); Num(sb, "dns", s.DnsMs);
            Num(sb, "cpu", s.Cpu); Num(sb, "mem", s.Mem); sb.Append(",\"lv\":").Append(s.Level);
            Str(sb, "head", s.Headline); Str(sb, "chip", s.Chip); Str(sb, "who", AdeeboMeter.Lines.Who());   /* the PC's companion: the phone talks the same way unless told otherwise */ Num(sb, "left", Left.TotalSeconds);
            string wifi, ssid, ch, speed; int sig, rate; bool hw, blocked; lock (M.L) { wifi = M.AdapterKind; hw = M.HasWifi; ssid = M.Ssid; ch = M.WifiChannel; sig = M.WifiSignal; rate = M.RxRate; blocked = M.WifiBlocked; speed = M.LinkSpeed; }
            Str(sb, "link", wifi); Str(sb, "speed", speed); Num(sb, "disk", s.Disk);
            string gwip; lock (M.L) gwip = M.Gateway ?? ""; Str(sb, "gwip", gwip);   // the map draws the real router (AM-CG-347)
            // the PC's Wi-Fi as Windows reports it (a phone's browser can't see any of this); empty when Windows hides it (location off)
            if (hw) { sb.Append(",\"wifi\":{"); Str(sb, "ssid", ssid, true); Num(sb, "sig", sig < 0 ? double.NaN : sig); Str(sb, "ch", ch); Num(sb, "rate", rate < 0 ? double.NaN : rate); sb.Append(",\"hidden\":").Append(blocked ? 1 : 0).Append('}'); }
            try { Game(sb); } catch (Exception e) { if (e is OutOfMemoryException) throw; }
            try { Trade(sb); } catch (Exception e) { if (e is OutOfMemoryException) throw; }
            try { Stream(sb); } catch (Exception e) { if (e is OutOfMemoryException) throw; }
            sb.Append('}'); Seal(sb.ToString());
        }
        static void Game(StringBuilder sb)
        {
            var games = Games.All; var mon = Games.Monitored(); var id = GamePing.Me.Playing;
            var g = games.FirstOrDefault(z => z.Id == id) ?? games.FirstOrDefault(z => mon.Contains(z.Id) && z.Servers.Count > 0);
            if (g == null || g.Servers.Count == 0) return;
            var srv = GamePing.Me.Best(g) ?? g.Servers[0]; var v = GamePing.Me.Stat(g.Id, srv).Window(60, 3600);
            double now = v.Length > 0 ? v[v.Length - 1] : double.NaN, avg = GameStat.Avg(v), jit = GameStat.Jitter(v), loss = GameStat.Loss(v);
            sb.Append(",\"game\":{"); Str(sb, "name", g.Name, true); Str(sb, "srv", srv.Name); Str(sb, "on", id == g.Id ? "1" : "0"); Num(sb, "now", now); Num(sb, "avg", avg); Num(sb, "jit", jit); Num(sb, "loss", loss);
            Str(sb, "grade", v.Length == 0 ? "Waiting" : GameStat.Grade(avg, jit, double.IsNaN(loss) ? 0 : loss));
            sb.Append(",\"hist\":[").Append(string.Join(",", v.Skip(Math.Max(0, v.Length - 60)).Select(x => double.IsNaN(x) ? "null" : Math.Round(x).ToString(CultureInfo.InvariantCulture)).ToArray())).Append("]}");
        }
        // streaming: what plays now (or nothing), its server's delay and the drops so far - and, for going live, the upload
        static void Stream(StringBuilder sb)
        {
            string what = null; double down = double.NaN, far = double.NaN; int dips = 0; string last = ""; DateTime since = DateTime.MinValue;
            lock (Streams.k)
            {
                var n = Streams.Now;
                if (n != null)
                {
                    what = n.S.What; since = n.S.Start; dips = n.S.Dips.Count; var ld = n.S.Dips.LastOrDefault(); if (ld != null) last = ld.T.ToString("HH:mm", CultureInfo.InvariantCulture) + " · " + WatchKit.BlameName(ld.Blame, Streams.FarName);
                    var ls = n.Samples.LastOrDefault(); if (ls != null) { down = ls.Down; far = ls.Far; }
                }
            }
            sb.Append(",\"stream\":{"); Str(sb, "what", what ?? "", true); Str(sb, "on", what != null ? "1" : "0");
            Num(sb, "down", down); Num(sb, "far", far); sb.Append(",\"dips\":").Append(dips); Str(sb, "last", last); Num(sb, "mins", since == DateTime.MinValue ? double.NaN : (DateTime.Now - since).TotalMinutes);
            sb.Append('}');
        }
        // the day's markets as the Trading page has them (Bank of Canada rates, commodity index) - every 10 minutes, not every second
        DateTime mktAt = DateTime.MinValue;
        void SendMarkets()
        {
            if (!Cfg.TradeFx) { Seal("{\"t\":\"mkt\",\"off\":1}"); return; }
            try { Trading.LoadFx(); } catch (Exception e) { if (e is OutOfMemoryException) throw; }   // the saved copy only - nothing is fetched for the phone
            List<Trading.Rate> rates; List<Trading.Index> idx; lock (Trading.k) { rates = Trading.Rates.ToList(); idx = Trading.Indexes.ToList(); }
            var sb = new StringBuilder("{\"t\":\"mkt\",\"rates\":[");
            sb.Append(string.Join(",", rates.Select(r => "[\"" + J(r.Code) + "\",\"" + J(FxName(r.Code)) + "\"," + N4(r.Cad) + "," + N4(r.Prev) + ",\"" + r.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\"]").ToArray()));
            sb.Append("],\"idx\":[");
            sb.Append(string.Join(",", idx.Select(i => "[\"" + J(i.Code) + "\",\"" + i.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\",[" + string.Join(",", (i.V ?? Array.Empty<double>()).Select(N4).ToArray()) + "]]").ToArray()));
            sb.Append("]}"); Seal(sb.ToString());
        }
        static string FxName(string c) { string n; return Trading.FxNames.TryGetValue(c, out n) ? n : c; }
        static string N4(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "null" : Math.Round(v, 4).ToString(CultureInfo.InvariantCulture); }
        static void Trade(StringBuilder sb)
        {
            if (Cfg.TradeHost == "") return;
            int lv; string head = Trading.Headline(out lv); WatchKit.Sample last = null; lock (Trading.k) if (Trading.Samples.Count > 0) last = Trading.Samples[Trading.Samples.Count - 1];
            sb.Append(",\"trade\":{"); Str(sb, "venue", Trading.VenueName, true); Str(sb, "open", Trading.MarketOpen(Trading.EasternNow()) ? "1" : "0"); Str(sb, "head", head); sb.Append(",\"lv\":").Append(lv);
            if (last != null) { Num(sb, "router", last.Router); Num(sb, "net", last.Net); Num(sb, "broker", last.Far); }
            sb.Append('}');
        }
        void SendResult(string id, string rid, string title, List<string[]> rows, List<string[]> hops)
        {
            // AM-CG-284: a byte budget, not just a row count - sealed (AES padding + base64 + tag) it must stay well under the
            // relay's 64 KB per message, or the relay would end the session. Rows that don't fit are counted, never cut mid-way.
            const int Budget = 40 * 1024;
            // AM-CG-366: the path (hops) is built first and its size reserved, so the whole message fits
            var hb = new StringBuilder();
            if (hops != null && hops.Count > 0)
            {
                hb.Append(",\"hops\":[");
                for (int i = 0; i < Math.Min(30, hops.Count); i++) { var h = hops[i]; if (i > 0) hb.Append(','); hb.Append("[\"").Append(string.Join("\",\"", h.Select(x => J(Cut(x, 60))).ToArray())).Append("\"]"); }
                hb.Append(']');
            }
            int room = Budget - Encoding.UTF8.GetByteCount(hb.ToString());
            var sb = new StringBuilder("{\"t\":\"result\""); Str(sb, "id", id); Str(sb, "rid", rid); Str(sb, "title", title); sb.Append(",\"rows\":[");
            bool first = true; int left = 0; var all = (rows ?? new List<string[]>()).Where(o => o != null && o.Length > 2 && o[0] != "-2" && !(o.Length > 4 && o[4] == "k")).ToList();
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                // AM-CG-362: a shortened value ends with "…" so it's never mistaken for the whole thing
                string item = "[\"" + J(o[0]) + "\",\"" + J(Cut(FullView.ShareCell(o[1]), 80)) + "\",\"" + J(Cut(FullView.ShareCell(o[2]), 300)) + "\",\"" + J(o.Length > 3 ? Cut(FullView.ShareCell(o[3]), 300) : "") + "\"]";
                if (i >= 200 || Encoding.UTF8.GetByteCount(sb.ToString()) + Encoding.UTF8.GetByteCount(item) + 300 > room) { left = all.Count - i; break; }
                if (!first) sb.Append(','); first = false; sb.Append(item);
            }
            if (left > 0) { if (!first) sb.Append(','); sb.Append("[\"3\",\"More than fits\",\"").Append(first ? "None of the " + left + " rows fit on the phone" : left + " more rows didn't fit on the phone").Append("\",\"To see every row, run this check on the PC itself.\"]"); }   // AM-CG-363: it wasn't kept on the PC
            sb.Append(']').Append(hb).Append('}'); Seal(sb.ToString());
        }

        // a parsed message: its text fields and one list of strings, nothing else is read
        sealed class Msg
        {
            Dictionary<string, object> d;
            public static Msg Of(string s) { if (s == null || s.Length > 66 * 1024) return null; var d = Json.Parse(s) as Dictionary<string, object>; return d == null ? null : new Msg { d = d }; }
            public string S(string k) { object v; return d.TryGetValue(k, out v) && v != null && !(v is Dictionary<string, object>) && !(v is List<object>) ? Convert.ToString(v, CultureInfo.InvariantCulture) : null; }
            public List<string> A(string k) { object v; var l = d.TryGetValue(k, out v) ? v as List<object> : null; return l == null ? null : l.Select(x => x == null ? "" : Convert.ToString(x, CultureInfo.InvariantCulture)).ToList(); }
        }
        // ---------- plumbing
        // the phone and PC message version: the phone page refuses a PC newer than it understands and asks to be reloaded (AM-CG-335)
        public const int Proto = 2;
        static string Clip(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n); }
        static string Cut(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n - 1).TrimEnd() + "\u2026"; }
        static string J(string s)
        {
            var b = new StringBuilder();
            foreach (char ch in s ?? "") { if (ch == '"' || ch == '\\') b.Append('\\').Append(ch); else if (ch < 0x20) b.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture)); else b.Append(ch); }
            return b.ToString();
        }
        static void Num(StringBuilder sb, string k, double v) { sb.Append(",\"").Append(k).Append("\":").Append(double.IsNaN(v) || double.IsInfinity(v) ? "null" : Math.Round(v, 2).ToString(CultureInfo.InvariantCulture)); }
        static void Str(StringBuilder sb, string k, string v, bool firstInObj = false) { if (!firstInObj) sb.Append(','); sb.Append('"').Append(k).Append("\":\"").Append(J(v)).Append('"'); }
        // AM-CG-368: numbering and sending happen under one lock, so messages leave in the order they were numbered
        void Seal(string text) { var l = tx; if (l == null) return; lock (sendLock) { string f = l.Seal(text); Send("{\"s\":\"" + f + "\"}"); } }
        void Send(string text)
        {
            if (ws.State != WebSocketState.Open) return;
            var b = Encoding.UTF8.GetBytes(text);
            // bug-hunt ML-114: a message that can't go out in 10 s ends the session plainly - a silently dropped one breaks the
            // sealed sequence and the phone would close it anyway, with no reason given
            bool stuck = false;
            lock (sendLock) try { stuck = !ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, cts.Token).Wait(TimeSpan.FromSeconds(10)); } catch (AggregateException) { stuck = true; } catch (ObjectDisposedException) { } catch (InvalidOperationException) { stuck = true; }
            if (stuck && St != State.Ended) Task.Factory.StartNew(() => End("the relay stopped taking messages (a slow or lost connection)"));
        }
        void Note() { var c = Changed; if (c != null) try { c(); } catch (Exception e) { if (e is OutOfMemoryException) throw; } }

        public void End(string why)
        {
            if (St == State.Ended) return;
            if (St == State.Paired) Seal("{\"t\":\"bye\"}");
            var o = op; if (o != null) o.Stop = true;   // AM-CG-367: a check started from the phone stops with the session
            St = State.Ended; Why = why ?? "";
            var t = tick; tick = null; if (t != null) t.Dispose();
            try { if (ws.State == WebSocketState.Open) ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).Wait(2000); } catch (AggregateException) { } catch (ObjectDisposedException) { } catch (InvalidOperationException) { }
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
            try { ws.Dispose(); } catch (ObjectDisposedException) { }
            foreach (var key in asks.Keys.ToList()) { Action<string, List<string[]>> d; if (asks.TryRemove(key, out d)) try { d(null, null); } catch (Exception e) { if (e is OutOfMemoryException) throw; } }
            FilesEnd(); var t0 = tx; var r0 = rx; tx = rx = null; if (t0 != null) t0.Wipe(); if (r0 != null) r0.Wipe(); if (mine != null) { mine.Dispose(); mine = null; }
            if (Now == this) Now = null;
            Note();
        }
    }
}
