// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace AdeeboMeter
{
    // ================================================================== sending a report (Adeeb, 14 Nov: "no GitHub login")
    // A bug report, feedback or a Hall of Fame card goes to AdeeboMeter's relay (tools/relay/worker.js, a Cloudflare
    // Worker) which files it - nobody needs an account. Only ever when the person presses Send, after seeing exactly what
    // goes; never when online lookups are turned off by IT. Without the internet it waits in "outbox" and goes the next
    // time AdeeboMeter starts online (at most 3 kept, each at most 7 days).
    static class Relay
    {
        public const string Url = "https://adeebometer-relay.orkoadeeb.workers.dev/send";   // tools/relay/worker.js on Cloudflare
        public static bool Ready { get { return !Policy.NoOnline; } }   // bug-hunt L-294: Url is a constant - only the policy decides
        public sealed class Result { public bool Ok, Queued; public int Number; public string Why = ""; }

        // one JSON object, built by hand (no library): kind bug / feedback / hof
        public static string Json(string kind, string title, string body, Dictionary<string, string> card)
        {
            var sb = new StringBuilder("{");
            Action<string, string> put = (k, v) => { if (sb.Length > 1) sb.Append(','); sb.Append('"').Append(k).Append("\":\"").Append(Esc(v)).Append('"'); };
            put("kind", kind); put("from", "app"); put("version", Program.Version); put("title", title ?? ""); put("body", body ?? "");
            if (card != null) { sb.Append(",\"card\":{"); bool first = true; foreach (var kv in card) { if (!first) sb.Append(','); first = false; sb.Append('"').Append(Esc(kv.Key)).Append("\":\"").Append(Esc(kv.Value)).Append('"'); } sb.Append('}'); }
            return sb.Append('}').ToString();
        }
        static string Esc(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "") { if (c == '"' || c == '\\') sb.Append('\\').Append(c); else if (c == '\n') sb.Append("\\n"); else if (c == '\r') continue; else if (c == '\t') sb.Append("\\t"); else if (c < ' ') continue; else sb.Append(c); }
            return sb.ToString();
        }
        // send now (on a worker thread); offline or the relay unreachable: kept in the outbox and sent later
        public static void Send(string json, Action<Result> done)
        {
            new Thread(() =>
            {
                Result r;
                try { r = Post(json); }
                catch (Exception e) { if (e is OutOfMemoryException) throw; r = new Result { Why = e.Message }; }
                if (!r.Ok && (r.Why == "offline" || Retryable(r.Why))) { r.Queued = Keep(BugReport.Scrub(json)); if (!r.Queued) r.Why = "couldn't keep it to send later"; }   // cleaned before it touches the disk
                if (done != null) done(r);
            }) { IsBackground = true, Name = "relay" }.Start();
        }
        static Result Post(string json)
        {
            if (!Ready) return new Result { Why = Policy.NoOnline ? "turned off by your IT team" : "not set up yet" };
            if (json.Length > 11000) return new Result { Why = "too long" };
            try
            {
                var q = (HttpWebRequest)WebRequest.Create(Url); q.Method = "POST"; q.ContentType = "application/json"; q.Timeout = 15000; q.ReadWriteTimeout = 15000;
                q.UserAgent = "AdeeboMeter/" + Program.Version; q.Proxy = WebRequest.GetSystemWebProxy();
                var b = Encoding.UTF8.GetBytes(json); q.ContentLength = b.Length;
                using (var s = q.GetRequestStream()) s.Write(b, 0, b.Length);
                string body; using (var rs = (HttpWebResponse)q.GetResponse()) using (var rd = new StreamReader(rs.GetResponseStream())) body = rd.ReadToEnd();
                return Parse(body);
            }
            catch (WebException e)
            {
                var hr = e.Response as HttpWebResponse;
                if (hr == null) return new Result { Why = "offline" };   // no answer at all: try again later
                string body = ""; try { using (var rd = new StreamReader(hr.GetResponseStream())) body = rd.ReadToEnd(); } catch (IOException) { }
                var r = Parse(body); if (r.Why == "") r.Why = "the relay said " + (int)hr.StatusCode; return r;
            }
        }
        // {"ok":true,"number":42} or {"ok":false,"why":"..."}
        internal static Result Parse(string body)
        {
            var r = new Result(); body = body ?? "";
            // bug-hunt L-292: the "ok" field itself, read as JSON - never the words "ok":true anywhere in the text
            object okv; var d = AdeeboMeter.Json.Parse(body) as Dictionary<string, object>; r.Ok = d != null && d.TryGetValue("ok", out okv) && okv is bool && (bool)okv;
            var n = System.Text.RegularExpressions.Regex.Match(body, "\"number\"\\s*:\\s*(\\d{1,9})"); if (n.Success) r.Number = int.Parse(n.Groups[1].Value, CultureInfo.InvariantCulture);
            var w = System.Text.RegularExpressions.Regex.Match(body, "\"why\"\\s*:\\s*\"([^\"]{0,200})\""); if (w.Success) r.Why = w.Groups[1].Value;
            return r;
        }

        // ---------- the outbox: reports written while offline
        static string Box { get { return Path.Combine(Program.DataDir, "outbox"); } }
        public static int Waiting { get { try { return Directory.Exists(Box) ? new DirectoryInfo(Box).GetFiles("*.json").Length : 0; } catch (IOException) { return 0; } catch (UnauthorizedAccessException) { return 0; } } }
        public static string WaitingText() { try { return Directory.Exists(Box) ? string.Join("\n\n---\n\n", new DirectoryInfo(Box).GetFiles("*.json").OrderBy(f => f.Name).Select(f => File.ReadAllText(f.FullName)).ToArray()) : ""; } catch (IOException) { return ""; } catch (UnauthorizedAccessException) { return ""; } }
        public static bool DropWaiting() { try { if (Directory.Exists(Box)) foreach (var f in new DirectoryInfo(Box).GetFiles("*.json")) f.Delete(); return true; } catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; } }
        static bool Keep(string json)
        {
            try
            {
                Directory.CreateDirectory(Box);
                var old = new DirectoryInfo(Box).GetFiles("*.json").OrderBy(f => f.LastWriteTimeUtc).ToList();
                while (old.Count >= 3) { old[0].Delete(); old.RemoveAt(0); }
                SafeFile.WriteText(Path.Combine(Box, DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + ".json"), json);   // bug-hunt L-293: never a half-written report
                return true;
            }
            catch (Exception e) { if (!(e is IOException || e is UnauthorizedAccessException)) throw; return false; }
        }
        // at start (once online): send what waited; a week-old report is dropped
        // a 5xx or 429 from the relay is a busy moment, not a refusal: the report waits in the outbox instead of being dropped
        static bool Retryable(string why) { int c; return why != null && why.StartsWith("the relay said ", StringComparison.Ordinal) && int.TryParse(why.Substring(15).Trim(), out c) && (c >= 500 || c == 429); }
        public static void Flush()
        {
            if (!Ready) return;
            new Thread(() =>
            {
                try
                {
                    if (!Directory.Exists(Box)) return;
                    foreach (var f in new DirectoryInfo(Box).GetFiles("*.json").OrderBy(x => x.Name))
                    {
                        if ((DateTime.UtcNow - f.LastWriteTimeUtc).TotalDays > 7) { f.Delete(); continue; }
                        var r = Post(File.ReadAllText(f.FullName));
                        if (r.Why == "offline" || Retryable(r.Why)) return;   // still no internet, or the relay busy for a moment (bug-hunt M-24): next time
                        f.Delete();                          // sent, or refused for good (a refusal won't change by retrying)
                        Program.Log("outbox: " + (r.Ok ? "sent #" + r.Number : "dropped (" + r.Why + ")"));
                    }
                }
                catch (Exception e) { if (e is OutOfMemoryException) throw; Program.Log("outbox: " + e.Message); }
            }) { IsBackground = true, Name = "outbox" }.Start();
        }
    }
}
