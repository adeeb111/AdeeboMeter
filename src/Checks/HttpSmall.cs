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
        public static string Get(string url, int maxKb, string header, string value)
        {
            var q = (HttpWebRequest)WebRequest.Create(url); q.Timeout = 10000; q.ReadWriteTimeout = 10000; q.UserAgent = "AdeeboMeter/" + Program.Version; q.Proxy = WebRequest.GetSystemWebProxy();
            q.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            if (header != null) q.Headers[header] = value;
            var deadline = DateTime.UtcNow.AddSeconds(20);
            using (var r = q.GetResponse()) using (var s = r.GetResponseStream())
            {
                // bug-hunt L-39: an answer from another site (or over plain http) after a redirect isn't the one asked for
                if (!string.Equals(r.ResponseUri.Host, q.RequestUri.Host, StringComparison.OrdinalIgnoreCase) || (q.RequestUri.Scheme == "https" && r.ResponseUri.Scheme != "https")) throw new InvalidDataException("the site sent this to " + r.ResponseUri.Host + " instead");
                var buf = new byte[maxKb * 1024]; int got = 0, n;
                while (got < buf.Length && (n = s.Read(buf, got, buf.Length - got)) > 0) { got += n; if (DateTime.UtcNow > deadline) throw new InvalidDataException("answer too slow"); }
                if (got == buf.Length && s.ReadByte() >= 0) throw new InvalidDataException("answer too large");
                return Encoding.UTF8.GetString(buf, 0, got);
            }
        }
        public static string Get(string url, int maxKb) { return Get(url, maxKb, null, null); }
    }
}
