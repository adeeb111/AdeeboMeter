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
    sealed class Remote
    {
        // the relay's address, set when it's deployed (tools/remote). Empty: the feature says it isn't set up.
        public static readonly string Relay = "wss://adeebometer-remote.orkoadeeb.workers.dev";
        public const string Page = "https://adeebometer.com/remote#";
        public const int Minutes = 30;
        public static volatile Remote Now;                       // the one session, or null
        public static DateTime MuteUntil = DateTime.MinValue;   // "mute alerts for an hour" from the phone

        // what the phone may start: network checks that read, measure or test - nothing that sends crafted traffic on
        // your LAN, touches keys or files, or changes the PC
        // what the phone may start is decided by the Toolbox itself (FullView.RemoteOk): CanRun asks it, Catalog lists it

        public enum State { Connecting, Waiting, Asking, Paired, Ended }
        public volatile State St = State.Connecting; public volatile string Why = "";
        public readonly string Room, Link; public readonly DateTime Started = DateTime.UtcNow; public string PhoneName = "";
        readonly byte[] secret, auth; readonly ClientWebSocket ws = new ClientWebSocket(); readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly Monitor M; ECDiffieHellmanCng mine; RemoteCrypto.Lane tx, rx; byte[] phonePk; readonly object sendLock = new object();
        System.Threading.Timer tick;

        // the PC side hooks: ask "Allow?" on the UI thread, run a check, note a change for the window and the widget
        public Func<string, bool> Ask; public Func<string, bool> CanRun; public Func<List<string>> Catalog; public Action<string, string[], Toolkit.Run, Action<string, List<string[]>, List<string[]>>> RunTool; public Action Changed; public Action FeltSlow;
        // AM-CG-319/320: one check at a time from the phone, tagged with the phone's request id, stoppable from the phone
        volatile Toolkit.Run op; volatile string opRid = "";

        Remote(Monitor m)
        {
            M = m; secret = RemoteCrypto.Random(32); auth = RemoteCrypto.AuthKey(secret);
            Room = RemoteCrypto.B64u(RemoteCrypto.Random(16)); Link = Page + Room + "." + RemoteCrypto.B64u(secret);
        }
        public static Remote Start(Monitor m)
        {
            var old = Now; if (old != null) old.End("a new session started");
            var r = new Remote(m); Now = r;
            if (Relay == "") { r.St = State.Ended; r.Why = "The phone relay isn't set up yet."; return r; }
            Task.Factory.StartNew(r.Run, TaskCreationOptions.LongRunning);
            return r;
        }
        public bool Active { get { return St == State.Paired; } }
        public TimeSpan Left { get { var l = Started.AddMinutes(Minutes) - DateTime.UtcNow; return l < TimeSpan.Zero ? TimeSpan.Zero : l; } }

        void Run()
        {
            try
            {
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                ws.ConnectAsync(new Uri(Relay + "/room/" + Room + "?role=pc"), cts.Token).Wait(TimeSpan.FromSeconds(15));
                if (ws.State != WebSocketState.Open) { End("couldn't reach the relay"); return; }
                St = State.Waiting; Note();
                tick = new System.Threading.Timer(_ => Tick(), null, 1000, 1000);
                var buf = new byte[70 * 1024];
                while (St != State.Ended && ws.State == WebSocketState.Open)
                {
                    var sb = new StringBuilder(); WebSocketReceiveResult res;
                    do
                    {
                        var t = ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token); t.Wait(cts.Token); res = t.Result;
                        if (res.MessageType == WebSocketMessageType.Close) { End("the phone or the relay closed the session"); return; }
                        sb.Append(Encoding.UTF8.GetString(buf, 0, res.Count)); if (sb.Length > 66 * 1024) { End("a message was too big"); return; }
                    } while (!res.EndOfMessage);
                    Got(sb.ToString());
                }
            }
            catch (Exception e) { if (e is OutOfMemoryException) throw; if (St != State.Ended) End("connection lost"); Program.Log("remote: " + e.Message); }
        }

        void Got(string msg)
        {
            var j = Msg.Of(msg); if (j == null) return;
            if (St == State.Waiting && j.S("t") == "hi")
            {
                byte[] pk, mac; try { pk = RemoteCrypto.Unb64u(j.S("pk")); mac = RemoteCrypto.Unb64u(j.S("mac")); } catch (FormatException) { return; }
                if (pk.Length != 65 || !RemoteCrypto.Same(mac, RemoteCrypto.Hmac(auth, Encoding.ASCII.GetBytes("hi"), pk))) return;   // didn't scan this QR
                St = State.Asking; PhoneName = Clip(j.S("dev"), 40); Note();
                bool yes = Ask != null && Ask(PhoneName);
                if (!yes || St != State.Asking) { Send("{\"t\":\"no\"}"); End("you said no"); return; }
                mine = new ECDiffieHellmanCng(256); var myPk = RemoteCrypto.Raw(mine); phonePk = pk;
                var z = RemoteCrypto.SharedHash(mine, pk);
                tx = new RemoteCrypto.Lane(secret, z, 1); rx = new RemoteCrypto.Lane(secret, z, 2);
                Send("{\"t\":\"ok\",\"pk\":\"" + RemoteCrypto.B64u(myPk) + "\",\"mac\":\"" + RemoteCrypto.B64u(RemoteCrypto.Hmac(auth, Encoding.ASCII.GetBytes("ok"), pk, myPk)) + "\"}");
                mine.Dispose(); mine = null;   // the one-time private key goes as soon as the session keys exist
                St = State.Paired; Note(); SendLive(); try { var cat = Catalog != null ? Catalog() : null; if (cat != null) foreach (var part in cat) Seal(part); } catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("remote catalog: " + e.Message); } return;
            }
            if (St != State.Paired || rx == null) return;
            string plain = j.S("s") != null ? rx.Open(j.S("s")) : null;
            if (plain == null) { End("a message didn't check out - the session was closed to be safe"); return; }
            var c = Msg.Of(plain); if (c == null) return;
            switch (c.S("t"))
            {
                case "run":
                    {
                        string id = c.S("id") ?? "", rid = Clip(c.S("rid") ?? "", 16).Replace("\"", "").Replace("\\", "");
                        if (id != "feltslow" && (CanRun == null || !CanRun(id))) { Seal("{\"t\":\"err\",\"rid\":\"" + rid + "\",\"msg\":\"That check can't be started from the phone.\"}"); return; }
                        if (id == "feltslow") { if (FeltSlow != null) FeltSlow(); Seal("{\"t\":\"note\",\"msg\":\"Marked: felt slow. The answer is on the PC.\"}"); return; }
                        var a = (c.A("in") ?? new List<string>()).Take(4).Select(x => Clip(x, 200)).ToArray();
                        if (op != null) { Seal("{\"t\":\"err\",\"rid\":\"" + rid + "\",\"msg\":\"A check is still running on the PC - stop it first or wait for it.\"}"); return; }
                        var run = new Toolkit.Run { Deadline = DateTime.UtcNow.AddMinutes(6) }; op = run; opRid = rid;
                        if (RunTool != null) RunTool(id, a, run, (title, rows, hops) => { if (op == run) { op = null; opRid = ""; } SendResult(id, rid, title, rows, hops); });
                        else op = null;
                        return;
                    }
                case "mute": { int m; if (!int.TryParse(c.S("m"), out m)) m = 0; MuteUntil = DateTime.UtcNow.AddMinutes(Math.Max(0, Math.Min(240, m))); Seal("{\"t\":\"note\",\"msg\":\"" + (m > 0 ? "Alerts muted for " + m + " minutes." : "Alerts back on.") + "\"}"); return; }
                case "stop": { var o = op; if (o != null && (c.S("rid") ?? "") == opRid) { o.Stop = true; Seal("{\"t\":\"note\",\"msg\":\"Stopping the check on the PC...\"}"); } return; }
                case "bye": End("the phone ended the session"); return;
            }
        }

        void Tick()
        {
            if (St == State.Ended) return;
            if (DateTime.UtcNow - Started > TimeSpan.FromMinutes(Minutes)) { End("30 minutes are up"); return; }
            if (St == State.Waiting && DateTime.UtcNow - Started > TimeSpan.FromMinutes(2)) { End("no phone joined within 2 minutes"); return; }
            if (St == State.Paired) { SendLive(); if ((DateTime.UtcNow - mktAt).TotalMinutes >= 10) { mktAt = DateTime.UtcNow; SendMarkets(); } }
            Note();
        }

        // ---------- what the phone sees, once a second
        void SendLive()
        {
            var s = M.Latest; var sb = new StringBuilder("{\"t\":\"live\"");
            Num(sb, "down", s.Down); Num(sb, "up", s.Up); Num(sb, "ping", s.Ping); Num(sb, "jit", s.Jitter); Num(sb, "loss", s.LossPct); Num(sb, "gw", s.GwMs); Num(sb, "dns", s.DnsMs);
            Num(sb, "cpu", s.Cpu); Num(sb, "mem", s.Mem); sb.Append(",\"lv\":").Append(s.Level);
            Str(sb, "head", s.Headline); Str(sb, "chip", s.Chip); Str(sb, "who", AdeeboMeter.Lines.Who());   // the PC's companion: the phone talks the same way unless told otherwise Num(sb, "left", Left.TotalSeconds);
            string wifi; lock (M.L) wifi = M.AdapterKind; Str(sb, "link", wifi);
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
            var sb = new StringBuilder("{\"t\":\"result\""); Str(sb, "id", id); Str(sb, "rid", rid); Str(sb, "title", title); sb.Append(",\"rows\":[");
            bool first = true; int left = 0; var all = (rows ?? new List<string[]>()).Where(o => o != null && o.Length > 2 && o[0] != "-2" && !(o.Length > 4 && o[4] == "k")).ToList();
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                string item = "[\"" + J(o[0]) + "\",\"" + J(Clip(FullView.ShareCell(o[1]), 80)) + "\",\"" + J(Clip(FullView.ShareCell(o[2]), 300)) + "\",\"" + J(o.Length > 3 ? Clip(FullView.ShareCell(o[3]), 300) : "") + "\"]";
                if (i >= 200 || Encoding.UTF8.GetByteCount(sb.ToString()) + Encoding.UTF8.GetByteCount(item) + 200 > Budget) { left = all.Count - i; break; }
                if (!first) sb.Append(','); first = false; sb.Append(item);
            }
            if (left > 0) { if (!first) sb.Append(','); sb.Append("[\"3\",\"Left on the PC\",\"").Append(left).Append(" more rows - open the result on the PC\",\"\"]"); }
            sb.Append(']');
            if (hops != null && hops.Count > 0)
            {
                sb.Append(",\"hops\":[");
                for (int i = 0; i < Math.Min(30, hops.Count); i++) { var h = hops[i]; if (i > 0) sb.Append(','); sb.Append("[\"").Append(string.Join("\",\"", h.Select(x => J(Clip(x, 60))).ToArray())).Append("\"]"); }
                sb.Append(']');
            }
            sb.Append('}'); Seal(sb.ToString());
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
        static string Clip(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n); }
        static string J(string s)
        {
            var b = new StringBuilder();
            foreach (char ch in s ?? "") { if (ch == '"' || ch == '\\') b.Append('\\').Append(ch); else if (ch < 0x20) b.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture)); else b.Append(ch); }
            return b.ToString();
        }
        static void Num(StringBuilder sb, string k, double v) { sb.Append(",\"").Append(k).Append("\":").Append(double.IsNaN(v) || double.IsInfinity(v) ? "null" : Math.Round(v, 2).ToString(CultureInfo.InvariantCulture)); }
        static void Str(StringBuilder sb, string k, string v, bool firstInObj = false) { if (!firstInObj) sb.Append(','); sb.Append('"').Append(k).Append("\":\"").Append(J(v)).Append('"'); }
        void Seal(string text) { var l = tx; if (l == null) return; string f; lock (sendLock) f = l.Seal(text); Send("{\"s\":\"" + f + "\"}"); }
        void Send(string text)
        {
            if (ws.State != WebSocketState.Open) return;
            var b = Encoding.UTF8.GetBytes(text);
            lock (sendLock) try { ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, cts.Token).Wait(TimeSpan.FromSeconds(10)); } catch (AggregateException) { } catch (ObjectDisposedException) { } catch (InvalidOperationException) { }
        }
        void Note() { var c = Changed; if (c != null) try { c(); } catch (Exception e) { if (e is OutOfMemoryException) throw; } }

        public void End(string why)
        {
            if (St == State.Ended) return;
            if (St == State.Paired) Seal("{\"t\":\"bye\"}");
            St = State.Ended; Why = why ?? "";
            var t = tick; tick = null; if (t != null) t.Dispose();
            try { if (ws.State == WebSocketState.Open) ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).Wait(2000); } catch (AggregateException) { } catch (ObjectDisposedException) { } catch (InvalidOperationException) { }
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
            try { ws.Dispose(); } catch (ObjectDisposedException) { }
            tx = rx = null; if (mine != null) { mine.Dispose(); mine = null; }
            if (Now == this) Now = null;
            Note();
        }
    }
}
