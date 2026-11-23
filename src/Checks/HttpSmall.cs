// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.IO;
using System.Net;
using System.Text;

namespace AdeeboMeter
{
    // One small, careful GET for the day's numbers, the weather and sports: the timeouts are set before the request goes out
    // (R2-11: setting one after is refused), the size is capped while reading - never after reading it all (AM-CG-127/165) -
    // and the whole answer has a total deadline.
    static class HttpSmall
    {
        // SEC-06 (security hunt): redirects are followed by hand, and only on the same site and never from https down to http -
        // so no request (and no key header) ever goes to a host that wasn't asked for, instead of refusing the data afterwards
        public static HttpWebResponse SameSite(Uri u, Func<Uri, HttpWebRequest> make)
        {
            string host = u.Host; bool secure = u.Scheme == "https";
            for (int hop = 0; hop <= 3; hop++)
            {
                var q = make(u); q.AllowAutoRedirect = false; HttpWebResponse r;
                try { r = (HttpWebResponse)q.GetResponse(); }
                catch (WebException we) { var er = we.Response as HttpWebResponse; if (er == null || (int)er.StatusCode < 300 || (int)er.StatusCode >= 400) throw; r = er; }
                int code = (int)r.StatusCode; string loc = r.Headers["Location"];
                if (code < 300 || code >= 400 || loc == null) return r;
                r.Close(); Uri next;
                if (!Uri.TryCreate(u, loc, out next) || !string.Equals(next.Host, host, StringComparison.OrdinalIgnoreCase) || (secure && next.Scheme != "https") || (next.Scheme != "https" && next.Scheme != "http"))
                    throw new InvalidDataException("the site redirected somewhere else (" + (next != null ? next.Host : "?") + ") - not followed");   // bug-hunt L-39, before the request now
                u = next;
            }
            throw new InvalidDataException("too many redirects");
        }
        public static string Get(string url, int maxKb, string header, string value)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            using (var r = SameSite(new Uri(url), x => { var q = (HttpWebRequest)WebRequest.Create(x); q.Timeout = 10000; q.ReadWriteTimeout = 10000; q.UserAgent = "AdeeboMeter/" + Program.Version; q.Proxy = Proxy(); q.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate; if (header != null) q.Headers[header] = value; return q; }))
            using (var s = r.GetResponseStream())
            {
                var buf = new byte[maxKb * 1024]; int got = 0, n;
                while (got < buf.Length && (n = s.Read(buf, got, buf.Length - got)) > 0) { got += n; if (DateTime.UtcNow > deadline) throw new InvalidDataException("answer too slow"); }
                if (got == buf.Length && s.ReadByte() >= 0) throw new InvalidDataException("answer too large");
                return Encoding.UTF8.GetString(buf, 0, got);
            }
        }
        public static string Get(string url, int maxKb) { return Get(url, maxKb, null, null); }
        // Windows' proxy, signed in as you: a company proxy that asks who you are (407) gets your Windows sign-in, as Edge does
        public static IWebProxy Proxy() { var p = WebRequest.GetSystemWebProxy(); if (p != null) p.Credentials = CredentialCache.DefaultCredentials; return p; }
        // why a request failed, in words (the weather place search said only "couldn't reach")
        public static string Why(Exception e)
        {
            var we = e as WebException; var hr = we != null ? we.Response as HttpWebResponse : null;
            if (hr != null) return (int)hr.StatusCode == 407 ? "a proxy asked for a sign-in (407)" : "it answered " + (int)hr.StatusCode;
            if (we != null) switch (we.Status)
                {
                    case WebExceptionStatus.NameResolutionFailure: return "its name didn't resolve - DNS, or blocked on this network";
                    case WebExceptionStatus.ProxyNameResolutionFailure: return "the proxy's name didn't resolve";
                    case WebExceptionStatus.Timeout: return "no answer in 10 s";
                    case WebExceptionStatus.TrustFailure: case WebExceptionStatus.SecureChannelFailure: return "the secure connection failed - something on this network checks HTTPS (a company filter?)";
                    case WebExceptionStatus.ConnectFailure: return "the connection was refused or blocked";
                    default: return we.Status.ToString();
                }
            return e.GetType().Name;
        }
    }
}
