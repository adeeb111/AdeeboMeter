// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace AdeeboMeter
{
    // ================================================================== short, on-demand captures for "How it travels" and Learn mode
    // Wireshark's own tools (dumpcap to record, tshark to read), by full path, no shell, a few seconds at most, your own PC's
    // traffic only (no promiscuous mode), into AdeeboMeter's capture folder - deleted when AdeeboMeter exits.
    static class Packets
    {
        public sealed class Pkt { public int No; public double T; public string Src = "", Dst = "", Proto = "", Info = "", Dscp = "", SPort = "", DPort = "", IpVer = ""; }
        public static string Iface(Monitor m)
        {
            string id; lock (m.L) id = m.IfaceId; Guid g;
            return Guid.TryParse((id ?? "").Trim('{', '}'), out g) ? "\\Device\\NPF_{" + g.ToString().ToUpperInvariant() + "}" : "";
        }
        // only letters, digits, spaces and the few characters capture filters use - never anything a program could misread
        static bool SafeFilter(string f) { return f.All(c => char.IsLetterOrDigit(c) || " .:-()".IndexOf(c) >= 0) && f.Length < 200; }

        // Record for `seconds` (optionally doing something meanwhile), then read the packets back. err explains a failure.
        public static List<Pkt> Capture(Monitor m, string filter, int seconds, Action during, out string file, out string err)
        {
            err = ""; file = ""; var list = new List<Pkt>();
            if (!LiveCapture.Available) { err = "Needs Wireshark (with Npcap) - not found on this PC."; return list; }
            if (Policy.NoCapture) { err = "Capturing packets is turned off by your IT team (AdeeboMeter.policy)."; return list; }
            string ifc = Iface(m); if (ifc == "") { err = "No active network adapter."; return list; }
            if (filter != "" && !SafeFilter(filter)) { err = "Bad filter."; return list; }
            file = Program.SafeCapturePath("journey-" + DateTime.Now.ToString("HHmmss-fff", CultureInfo.InvariantCulture) + ".pcapng");
            if (file == null) { err = "Capture folder unavailable."; return list; }
            var psi = new ProcessStartInfo(LiveCapture.Dumpcap, "-i \"" + ifc + "\" -p -q " + (filter != "" ? "-f \"" + filter + "\" " : "") + "-a duration:" + seconds.ToString(CultureInfo.InvariantCulture) + " -w \"" + file + "\"") { WorkingDirectory = Path.GetDirectoryName(LiveCapture.Dumpcap), UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            try
            {
                using (var p = Process.Start(psi))
                {
                    Job.Add(p); p.StandardError.ReadToEndAsync();
                    System.Threading.Thread.Sleep(900);     // dumpcap needs a moment before it sees packets
                    if (during != null) try { during(); } catch (Exception e) { Program.Log("capture action: " + e.Message); }
                    if (!p.WaitForExit((seconds + 8) * 1000)) { try { p.Kill(); } catch { } }
                }
            }
            catch (Exception e) { err = Draw.Trunc(e.Message, 80); return list; }
            if (!File.Exists(file)) { err = "Nothing was recorded."; return list; }
            return Read(file, "", 400, out err);
        }
        public static List<Pkt> Read(string file, string display, int max, out string err)
        {
            err = ""; var list = new List<Pkt>();
            string args = "-r \"" + file + "\" -n " + (display != "" ? "-Y \"" + display + "\" " : "") + "-T fields -E separator=/t -E occurrence=f -e frame.number -e frame.time_relative -e ip.src -e ipv6.src -e ip.dst -e ipv6.dst -e _ws.col.Protocol -e _ws.col.Info -e ip.dsfield.dscp -e udp.srcport -e udp.dstport -e ip.version";
            string text;
            try { text = Job.Run(LiveCapture.Tshark, args, 20000) ?? ""; } catch (Exception e) { err = Draw.Trunc(e.Message, 80); return list; }
            foreach (var line in text.Split('\n').Take(max))
            {
                var f = line.TrimEnd('\r').Split('\t'); if (f.Length < 8) continue;
                int no; double t;
                if (!int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out no)) continue;
                double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out t);
                list.Add(new Pkt { No = no, T = t, Src = f[2] != "" ? f[2] : f[3], Dst = f[4] != "" ? f[4] : f[5], Proto = f[6], Info = f[7], Dscp = f.Length > 8 ? f[8] : "", SPort = f.Length > 9 ? f[9] : "", DPort = f.Length > 10 ? f[10] : "", IpVer = f.Length > 11 ? f[11] : "" });
            }
            return list;
        }
        // Wireshark's full decode of one packet, as layer blocks: { layer title, field lines }
        public static List<KeyValuePair<string, List<string>>> Decode(string file, int no)
        {
            var r = new List<KeyValuePair<string, List<string>>>();
            string text = "";
            try { text = Job.Run(LiveCapture.Tshark, "-r \"" + file + "\" -n -V -Y \"frame.number == " + no.ToString(CultureInfo.InvariantCulture) + "\"", 20000) ?? ""; } catch { return r; }
            List<string> cur = null;
            foreach (var raw in text.Split('\n'))
            {
                string l = raw.TrimEnd('\r'); if (l.Trim() == "") continue;
                if (!l.StartsWith(" ", StringComparison.Ordinal)) { cur = new List<string>(); r.Add(new KeyValuePair<string, List<string>>(l.Trim(), cur)); }
                else if (cur != null && cur.Count < 40 && l.StartsWith("    ", StringComparison.Ordinal) && !l.StartsWith("        ", StringComparison.Ordinal)) cur.Add(l.Trim());
            }
            return r;
        }
        // a plain-English line for each layer Wireshark names
        public static string LayerStory(string title)
        {
            string t = title.ToLowerInvariant();
            if (t.StartsWith("frame", StringComparison.Ordinal)) return "Layer 1 · the packet as your network card saw it: when, how big.";
            if (t.StartsWith("ethernet", StringComparison.Ordinal)) return "Layer 2 · hardware addresses: from your adapter to the next box (your router) - not the final server.";
            if (t.StartsWith("802.1q", StringComparison.Ordinal)) return "Layer 2 · VLAN tag: which virtual network the switch puts this on.";
            if (t.StartsWith("internet protocol version 4", StringComparison.Ordinal)) return "Layer 3 · IPv4: the end-to-end addresses, TTL (hops it may still cross), DSCP (priority) and whether it may be split.";
            if (t.StartsWith("internet protocol version 6", StringComparison.Ordinal)) return "Layer 3 · IPv6: the end-to-end addresses, hop limit and traffic class (priority).";
            if (t.StartsWith("transmission control", StringComparison.Ordinal)) return "Layer 4 · TCP: ports (which app), sequence numbers (order), flags (SYN, ACK, FIN), window (how much the other side may send).";
            if (t.StartsWith("user datagram", StringComparison.Ordinal)) return "Layer 4 · UDP: just ports and a length - no handshake, no resend. DNS, QUIC and call audio use it.";
            if (t.StartsWith("transport layer security", StringComparison.Ordinal) || t.StartsWith("tls", StringComparison.Ordinal)) return "Layers 5-6 · TLS: the encryption. The handshake (hello, certificate, keys) is visible; the data after it isn't.";
            if (t.StartsWith("quic", StringComparison.Ordinal)) return "Layers 4-6 · QUIC: TCP + TLS rebuilt on UDP - what modern browsers use for HTTP/3.";
            if (t.StartsWith("domain name system", StringComparison.Ordinal)) return "Layer 7 · DNS: a name asked, an address answered.";
            if (t.StartsWith("hypertext transfer protocol", StringComparison.Ordinal)) return "Layer 7 · HTTP: the request and the reply.";
            if (t.StartsWith("dynamic host", StringComparison.Ordinal)) return "Layer 7 · DHCP: getting (or renewing) your IP address.";
            if (t.StartsWith("address resolution", StringComparison.Ordinal)) return "Layer 2½ · ARP: 'who has this IP? tell me your hardware address'.";
            if (t.StartsWith("internet control message", StringComparison.Ordinal)) return "Layer 3 · ICMP: ping and 'time exceeded' / 'unreachable' messages.";
            return "";
        }

        // A TCP + TLS handshake to host:443 to watch (nothing is sent after the handshake; the connection closes at once).
        public static void Handshake(string host, int port)
        {
            using (var c = new TcpClient())
            {
                var ar = c.BeginConnect(host, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(5000)) return;
                c.EndConnect(ar);
                if (port != 443) return;
                using (var ssl = new SslStream(c.GetStream(), false, delegate(object o, System.Security.Cryptography.X509Certificates.X509Certificate cert, System.Security.Cryptography.X509Certificates.X509Chain ch, SslPolicyErrors e) { return e == SslPolicyErrors.None; }))   // only the handshake; no data is sent
                {
                    var t = Task.Factory.StartNew(() => ssl.AuthenticateAsClient(host, null, SslProtocols.Tls12 | (SslProtocols)12288, false));
                    try { t.Wait(5000); } catch { }
                }
            }
        }
    }
}
