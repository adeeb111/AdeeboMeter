# Round 4 - slice S3 (network tools) - e80412e

Files: src\Checks\Toolkit.cs, DestCheck.cs, Reach.cs, Icmp.cs, Online.cs, Protocols.cs, MoreTools.cs, Tools.cs, Lab.cs, UdpMtu.cs, SpeedTest.cs (all 4,238 lines read).
Harnesses: scratchpad/review4/h3/ (h1-h6.cs reflection on build4/r4.exe under mono; srv.py = hostile HTTP / DoH-drip / redirect / NTP servers; iperfsrv.py = hostile iperf3 server). Mono and .NET Framework agree on the IPv4 parsing shown (inet_addr octal/hex/dword rules); where they could differ it is said.

### 1. A hostile iperf3 server makes AdeeboMeter open thousands of TCP connections (8,470 in 20 s)
Severity: security  
Where: src\Checks\Toolkit.cs lines 288-310 (IperfOne, state 10 CREATE_STREAMS), lines 275-277 (guard: deadline secs + 60 s)  
How to reproduce: h3/iperfsrv.py: accept the control connection, read the 37-byte cookie, then send the byte 0x0A (CREATE_STREAMS) every 10 ms and accept every stream. In the Toolbox: iperf3 to 127.0.0.1:15201 with `-P 16 -t 1` (h3/h5.cs).  
What happens / what should happen: every 0x0A opens another `par` sockets and adds them to `streams`. The server logged "stream connections opened in 20 s: 8470". The only limit is the guard deadline (secs + 60 s, up to 180 s with -t 120), long enough to use up Windows' ~16,000 ephemeral ports, so every other program on the PC fails to connect until the sockets close and their TIME_WAIT ends. A public iperf3 server you don't control can do this. It should accept CREATE_STREAMS once only (and each other state at most once, in order), and cap `streams` at `par`.  
Confidence: confirmed (ran it: mono harness + Python fake server)

### 2. "Press to ask the authority" (HTTP and TLS inspector) contacts the CA even when IT set online_lookups = off, or the user turned revocation checks off
Severity: security  
Where: src\Checks\Toolkit.cs line 784 (Tls adds the `revoke:` row every time), lines 722-753 (Revocation: X509RevocationMode.Online, no check); src\Ui\FullView.Toolbox.cs line 376 (handler)  
How to reproduce: put `online_lookups = off` in AdeeboMeter.policy, or turn Settings › Privacy & online lookups › Certificate revocation checks off. Run HTTP and TLS inspector on any https site, then click the Revocation row.  
What happens / what should happen: chain.Build fetches OCSP/CRL (and AIA) from the CA. About › What leaves your PC says "Certificate revocation: Off". CertChain.cs (lines 148 and 364) gates the same check on `CertCfg.Revocation && !Policy.NoOnline`, but this path doesn't. Apply the same gate here: hide the row, or show "off by your IT team / in Settings". Also, the handler sets tbBusy = true without checking whether another tool is already running, and Revocation ignores the Run it is given, so Stop does nothing.  
Confidence: confirmed (read: there is no Policy or CertCfg reference in Toolkit.cs; CertChain has both)  
Rule: 1 (and the policy can't switch it off)

### 3. HTTP request builder shows and keeps a URL's user:password
Severity: security  
Where: src\Checks\Reach.cs line 83 (`Draw.Trunc(uri.ToString(), 80)` - Reach.NoUser isn't used here); src\Ui\FullView.Toolbox.cs line 737 (`shown[0]` keeps the URL as typed)  
How to reproduce: HTTP request builder, URL `http://admin:s3cret@127.0.0.1:18080/api` (h3/h2.cs + srv.py).  
What happens / what should happen: the first result row reads `GET http://admin:s3cret@127.0.0.1:18080/api | 200 OK`. That row ends up in Copy as text, Save, the last result and Compare, and the inputs key keeps the URL too. Toolkit.Http already wraps every URL in Reach.NoUser. Use NoUser here and blank the userinfo in `shown[0]`. Under mono the password wasn't sent as an Authorization header; on .NET Framework it isn't sent either, so it is only shown and kept.  
Confidence: confirmed (ran it: mono harness, row text above)  
Rule: 4

### 4. speedservers.txt: the upload URL's host is never checked against private addresses, and the download host is checked against a different DNS answer than the one used
Severity: security  
Where: src\Checks\SpeedTest.cs lines 180-185 (Measure resolves and checks only `srv.Host`, which is the Down URL's host), line 201 (`Phase(srv.Up, ...)`), lines 77-88 (SafeUrl accepts any dotted name)  
How to reproduce: a speedservers.txt line `server|x|X|Y|https://speed.cloudflare.com/__down?bytes=25000000|http://192.168.1.1.nip.io/apply.cgi` (any public DNS name that resolves to a LAN address; `SafeUrl("http://127.0.0.1.nip.io/upload")` returns True - h3/h1.cs). Then Speed test with `x`.  
What happens / what should happen: the download host is public, so the check passes, and the upload phase then POSTs 4 MB bodies to the router for 8 s (up to 100 MB). Separately, HttpWebRequest resolves the Down host again on its own, so a name with both a public and a private A record (or a rebinding name) can pass the check with the public answer and connect to the private one. Round 2 #7 asked for "check the resolved address at connect time". Check the Up host too. Better still, connect to the checked IP with a Host header or SNI, or check `ServicePoint.BindIPEndPointDelegate` and refuse private addresses.  
Confidence: confirmed (ran SafeUrl; read Measure); delivery to the LAN not run (no internet here)

### 5. TWAMP-light and MTU over UDP skip the "one device only" guard: 1,000 packets to a multicast group
Severity: security  
Where: src\Checks\Protocols.cs lines 458-466 (Twamp: no Lab.NotOne); src\Checks\UdpMtu.cs lines 29-31 (no Lab.NotOne)  
How to reproduce: TWAMP-light, reflector `224.0.0.1:862` (or `239.255.255.250`), Packets 1000; MTU over UDP to `224.0.0.251`, port 53. On a home network the SenderGate tick isn't needed.  
What happens / what should happen: h3/h1.cs: `Twamp("224.0.0.1:862","10")` sends and reports "no reflected packets", while `Syslog("224.0.0.1")` is refused with "is a multicast group - many devices at once". Every device on the segment gets up to 1,000 packets at 10 a second. Call Lab.NotOne after Addr in both tools, as Syslog, Trap, NetFlow and the Lab senders do. Round 1 #229 added the tick, but not this guard.  
Confidence: confirmed (ran it: mono harness; Linux sends UDP to multicast without any socket option, and Windows does too)  
Rule: brief guard (one target)

### 6. Sustained ping, DSCP ping and Latency under load ping broadcast and multicast addresses (up to 50 a second, 2,000 in all), and accept 4294967295 as 255.255.255.255
Severity: security  
Where: src\Checks\Icmp.cs lines 97-103 (Resolve4: IPAddress.TryParse first, no NotOne); src\Checks\Toolkit.cs line 416 (Sustained), line 186 (DscpPing), line 212 (Loaded)  
How to reproduce: Sustained ping, target `192.168.1.255`, `224.0.0.1` or `4294967295`, How many 2000, Every ms 20.  
What happens / what should happen: Resolve4 returns the broadcast or multicast address (`Resolve4("4294967295")` gives 255.255.255.255 and `Resolve4("https://4294967295/")` the same - h3/h1.cs), and the tool pings it 50 times a second. The ping sweep and the ICMP builder never allow this. Run Lab.NotOne on the resolved address in Resolve4's callers, or in Resolve4 itself.  
Confidence: confirmed (Resolve4 output); suspected that IcmpSendEcho puts broadcast echoes on the wire (Windows-only API; multicast echo is known to work)

### 7. IPv4 typed with leading zeros, hex or fewer than four parts is read as a different address in the ping tools, the subnet calculator and URL-form destinations
Severity: wrong result  
Where: src\Checks\Icmp.cs line 100 (Resolve4); src\Checks\Toolkit.cs line 928 (Subnet only counts the dots); src\Checks\DestCheck.cs lines 64-67 (URL branch: Uri has already canonicalised the host before the lines 90/91 checks)  
How to reproduce: h3/h1.cs and h2.cs: `Resolve4("010.0.0.1")` → 8.0.0.1, `Resolve4("192.168.001.010")` → 192.168.1.8, `Resolve4("0x7f.1")` → 127.0.0.1. `Subnet("192.168.001.010/24")` shows the host as `...00000001.00001000` (.8), and `Subnet("010.1.2.3/24")` gives network 8.1.2.0/24. `DestCheck.Parse("http://010.1.1.1/")` → host 8.1.1.1 with no message, and `http://0x7f.1/` → 127.0.0.1.  
What happens / what should happen: Destination check refuses `010.1.1.1` with "Windows would read 0-prefixed numbers as octal", but the same input works silently in DSCP ping, Sustained ping, Latency under load, Return path and the subnet calculator, and in Destination check when written as a URL. Zero-padded addresses are common in printed configs. Use the same strict `^[0-9]{1,3}(\.[0-9]{1,3}){3}$` + no-leading-zero rule in Resolve4 and Subnet, and check what the user typed (not Uri.Host) in the URL branch. Mapped forms are refused instead: `Resolve4("::ffff:10.0.0.1")` returns null, "couldn't find (IPv4)".  
Confidence: confirmed (ran it: mono harness)

### 8. Revocation checks the wrong certificate when "Name to ask for (SNI)" was used
Severity: wrong result  
Where: src\Checks\Toolkit.cs line 784 (`revoke:` + host, the connection address), line 731 (`ssl.AuthenticateAsClient(host)`)  
How to reproduce: HTTP and TLS inspector, URL `https://203.0.113.5`, Name to ask for `www.example.com`, then press Revocation.  
What happens / what should happen: the handshake for the revocation check sends no SNI (an IP literal), so a shared or CDN server returns its default certificate. The rows above it described the www.example.com certificate; the "not revoked" verdict is about a different one. Carry the SNI in the action (`revoke:host:port:sni`) and use it in AuthenticateAsClient.  
Confidence: suspected (read it)

### 9. NTP: a reply with zero (or garbage) receive and transmit timestamps says "Your clock is behind by 291404279795 ms" in red
Severity: wrong result  
Where: src\Checks\Protocols.cs lines 166-171 (Ntp), line 178 (ReadNtp: 0 → era 1 = 2036)  
How to reproduce: h3/srv.py NTP on 127.0.0.1:18123: mode 4, stratum 2, origin = our transmit time, everything else zero. NTP tool `127.0.0.1:18123`.  
What happens / what should happen: "answers · stratum 2 (gets time from 0.0.0.0)", then a red "behind by 291404279795 ms" (9 years). The delay check passes because t2 = t3. RFC 4330 section 5 says to discard replies whose transmit timestamp is 0 (and stratum 0, or refid 0 at stratum ≥ 2, is suspect). Reject a zero transmit or receive timestamp, and show large offsets in s/min/days rather than raw ms.  
Confidence: confirmed (ran it: mono harness)

### 10. SNMP test trap: sysUpTime is badly encoded for many uptimes (e.g. any uptime between 23.3 and 46.6 hours)
Severity: wrong result  
Where: src\Checks\Lab.cs lines 340-341 (TrapPacket: `up.SkipWhile(x == 0)` with no leading 0x00 when the top bit is set)  
How to reproduce: `Lab.TrapPacket("public", 200, 1, "x")` → `...43-01-C8...`; `TrapPacket(..., 9000000, ...)` → `43-03-89-54-40` (h3/h1.cs). 9,000,000 ticks is 25 h of uptime; the ticks value is TickCount/10.  
What happens / what should happen: BER encodes TimeTicks like an INTEGER, so a first byte ≥ 0x80 reads as negative. net-snmp shows 0xFFFFFFC8 (about 497 days), and strict decoders (pyasn1) reject the trap. It happens whenever the trimmed first byte has its top bit set: uptimes from 1.28-2.56 s, 5.5-10.9 min, and 23.3-46.6 h. Add a 0x00 byte when `(first & 0x80) != 0`, as `Int()` already does.  
Confidence: confirmed (ran it: bytes above)

### 11. Check a destination on port 853 (DNS over TLS) or 636 sends an HTTP HEAD and turns a healthy server yellow
Severity: wrong result  
Where: src\Checks\DestCheck.cs lines 322-339 (HTTP step runs for every `tls` port: 443, 8443, 993, 995, 465, 636, 853)  
How to reproduce: Check a destination `1.1.1.1:853`. Parse gives port 853 and tls=True (h3/h1.cs).  
What happens / what should happen: after a good TLS handshake it writes `HEAD / HTTP/1.1`. A DoT server reads "HE" as a 18,501-byte length and waits, so the read times out after 6 s: step HTTP = "no answer" (level 1) and the verdict is level 1 ("worst >= 1"). IMAP, POP and SMTP greet first, so they show "not HTTP". Only send the HEAD on web ports (80, 443, 8080, 8443) and mark the others "not a web port".  
Confidence: suspected (read it; no DoT server here)

### 12. Unicode digits make Many ports and the ICMP builder fail ("Couldn't finish: FormatException")
Severity: wrong result (an error row, logged; no crash)  
Where: src\Checks\MoreTools.cs lines 165-168 (`\d+` then `int.Parse(..., Invariant)`); src\Checks\Lab.cs lines 124-129 (`(\d+)` then `int.Parse`)  
How to reproduce: Many ports, Service `٨٠` (or `８０`); ICMP builder, Options `size ١٠٠` (h3/h1.cs).  
What happens / what should happen: both throw FormatException. This is the same class as round 3 #7 (DestCheck/Calc were fixed with `[0-9]`), and these two were missed. Use `[0-9]` or RegexOptions.ECMAScript.  
Confidence: confirmed (ran it: "THREW FormatException")

### 13. LDAP check: a 16 KB reply with a long-form length near its end throws IndexOutOfRange
Severity: wrong result (an error row "Couldn't finish"; a hostile or odd LDAP server)  
Where: src\Checks\MoreTools.cs line 335 (ParseRootDse: `b[j + 2 + q]` and `b[o + q]` for up to 127 length bytes, checked only against `j + 2 < n`)  
How to reproduce: `ParseRootDse(b, 16384)` where the last 30 bytes are `04 14 "defaultNamingContext" 31 FF` (h3/h2.cs).  
What happens / what should happen: IndexOutOfRangeException. Reject length forms over 3 bytes, and check `k + nb <= end` before reading them, as TryTlv in Protocols does.  
Confidence: confirmed (ran it)

### 14. HTTP timing sends a Host header without the port, and the raw host for IDN and IPv6
Severity: wrong result  
Where: src\Checks\Protocols.cs line 91 (`"Host: " + u.Host`), line 89 (SNI `u.Host`)  
How to reproduce: HTTP timing `http://127.0.0.1:18080/x` against h3/srv.py: the server received `Host: 127.0.0.1`. Also `https://bücher.de` and `https://[2001:db8::1]:8443/`.  
What happens / what should happen: RFC 9112 requires `host:port` for non-default ports, so virtual hosts and redirect logic on :8080/:8443 answer something else (wrong vhost, 400 or 421). IDN hosts go out as `b?cher.de` (Encoding.ASCII) and as a Unicode SNI, and IPv6 as `[..]` in SNI. Use `u.IdnHost` / `u.Authority` and an SNI without brackets. A plain-http run also lists a "Lock (TLS)" row (about 1 ms).  
Confidence: confirmed (ran it: Host header captured); IDN/IPv6 parts read

### 15. ICMP builder silently ignores options it doesn't know (`-l 1472 -f`), and its chart repeats the last RTT for lost packets
Severity: wrong result  
Where: src\Checks\Lab.cs line 124 (the regex skips unknown words), line 162 (`run.Add(times[b].Count > 0 ? times[b].Last() : 0)`)  
How to reproduce: ICMP builder, Options `-l 1472 -f` (Windows ping syntax).  
What happens / what should happen: it runs with a "32-byte payload, TTL 128" and no DF, and nothing says the options were dropped (h3/h2.cs). iperf lists "Not understood and ignored"; do the same here, or accept -l / -f / -i / -v. On the chart, a lost or TTL-expired packet repeats the previous reply time (or 0 before the first reply), so loss looks like a flat line. Add NaN as Sustained ping does.  
Confidence: confirmed (ran it: row text); chart read

### 16. Many ports: "8000-8010" tests only 8000 and 8010
Severity: wrong result  
Where: src\Checks\MoreTools.cs line 165 (`Regex.Matches(w, @"\d+")`)  
How to reproduce: Many ports, host 127.0.0.1, Service `8000-8010`.  
What happens / what should happen: "0 of 2 ports open" (h3/h1.cs). Either expand ranges (still 20 at most) or say "ranges aren't supported".  
Confidence: confirmed (ran it)

### 17. STUN to an IPv6 server always says "answered, but not with an address"
Severity: wrong result  
Where: src\Checks\Reach.cs line 128 (ParseStun accepts family 1 only)  
How to reproduce: STUN to `[2001:db8::x]:3478`, or to a name with only AAAA records (Lab.Addr then picks IPv6). `ParseStun` on a Binding Success with an IPv6 XOR-MAPPED-ADDRESS (family 2, 20 bytes) returns null (h3/h6.cs).  
What happens / what should happen: RFC 5389 section 15.2: family 0x02 XORs the 16-byte address with cookie + transaction id. Decode it.  
Confidence: confirmed (ran the parser); the live IPv6 path wasn't run (no IPv6 in the container)

### 18. Captive-portal check reads a chunked "Microsoft Connect Test" as a sign-in page and overrides the verdict in red
Severity: wrong result  
Where: src\Checks\DestCheck.cs lines 157-163 (ClassifyPortal: the body must start with "Microsoft Connect Test")  
How to reproduce: `ClassifyPortal("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n16\r\nMicrosoft Connect Test\r\n0\r\n\r\n")` → 1 (portal) (h3/h1.cs).  
What happens / what should happen: behind a proxy that re-chunks responses, a failed check becomes "Found the real culprit: a Wi-Fi sign-in page" at level 2, hiding the real cause. Look for the marker inside the body (`Contains`) or de-chunk first.  
Confidence: confirmed (ran it); proxies that re-chunk the response are uncommon

### 19. Internationalised names typed bare are refused by Check a destination and every ping tool
Severity: wrong result  
Where: src\Checks\DestCheck.cs line 56 (hostRx is ASCII only) and line 92; src\Checks\Icmp.cs line 101 (Resolve4 uses DestCheck.Parse)  
How to reproduce: Check a destination `bücher.de`; Sustained ping `bücher.de`.  
What happens / what should happen: "That is not a valid host, IP or URL." The URL form `http://bücher.de/` works (IdnHost → xn--bcher-kva.de) (h3/h1.cs), and dig handles IDN. Convert with IdnMapping before the regex.  
Confidence: confirmed (ran DestCheck.Parse)

### 20. Host:port splitters disagree: Lab.Split refuses `[2001:db8::1]` without a port; Protocols.Split takes `[::1]x80` as port 80
Severity: wrong result  
Where: src\Checks\Lab.cs lines 63-66 (the bracket form is only handled with `]:`, then the `[` fails the regex); src\Checks\Protocols.cs line 30 (`Substring(e + 2)` skips whatever is at e+1)  
How to reproduce: STUN, SNMP trap, NetFlow, Every IP behind a name, RDP or LDAP with `[2001:db8::1]` → `Lab.Split` returns False ("type a STUN server..."), while `[2001:db8::1]:3478` works (h3/h1.cs). NTP or SSH with `[::1]x22`.  
What happens / what should happen: the documented bracketed form fails without a port, and a typo is taken as a port. Use one splitter: strip `[...]`, then require `]` to be followed by nothing or `:digits`.  
Confidence: confirmed (Lab.Split ran); Protocols.Split read

### 21. Misleading error text: dig "no answer from X in 3 s" for a name it never sent; iperf "-6" says "Sequence contains no matching element"
Severity: cosmetic  
Where: src\Checks\Toolkit.cs lines 476-477 with Question line 551 (an empty label or one over 63 bytes → null → "no answer"); line 264 (`.First(...)`)  
How to reproduce: dig `a..b` or a 70-character label, server 127.0.0.1 → "no answer from 127.0.0.1 in 3 s". iperf3 `127.0.0.1` with `-6` → "Upload | failed: Sequence contains no matching element" (h3/h1.cs, h4.cs).  
What happens / what should happen: say "a label is empty or longer than 63 characters" (nothing was sent), and "this server has no IPv6 address".  
Confidence: confirmed (ran it)

### 22. Check a destination: path MTU is 1 byte low for about a third of the sizes below 1200
Severity: cosmetic  
Where: src\Checks\DestCheck.cs lines 376-377 (after 1172 fails, `hi` stays 1471 and the search stops after 9 rounds)  
How to reproduce: simulate the search for every true payload 548-1471 (Python, same loop): 277 of 924 sizes end 1 below the truth (e.g. 550 → 549).  
What happens / what should happen: set `hi = 1171` when 1172 fails, or loop until `lo == hi`.  
Confidence: confirmed (ran the same algorithm in Python)

### 23. dig / DNS compare / TRACE: DoH and the TCP fallback have per-read timeouts only - no overall deadline and no Stop (a dripping server holds the Toolbox for days)
Severity: slow  
Where: src\Checks\Toolkit.cs lines 564-570 (QueryDoh: ReadWriteTimeout 6 s per read, 64 KB cap), lines 527-529 (TCP retry: ReadN with 3 s per read), line 443 (Dig takes no Run); MoreTools.DnsCompare calls both  
How to reproduce: h3/srv.py port 18081: a 200 answer with `application/dns-message`, Content-Length 60000, then 1 byte every 4 s. `QueryDoh(http://127.0.0.1:18081/dns-query, ...)`.  
What happens / what should happen: still running when the harness was killed at 75 s. Up to 64 KB at 1 B/4 s is about 73 h. Stop doesn't help, and the Toolbox stays busy. The TCP fallback (a TC-bit answer, then a drip) does the same, and TRACE asks servers that any domain owner chooses (`dig evil.example TRACE`). Use one deadline (e.g. 8 s) for the whole query, checked in the read loops.  
Confidence: confirmed (ran it: DoH drip); TCP path read

### 24. Captive-portal probe can be dripped for hours
Severity: slow  
Where: src\Checks\DestCheck.cs lines 129-133 (Portal: 4 KB, 4 s per Receive, no total deadline)  
How to reproduce: a network (or the portal itself) answering msftconnecttest.com with 1 byte every 3.9 s.  
What happens / what should happen: Check a destination stays "Checking for a captive portal..." for up to about 4.4 h, with no Stop. Add an overall deadline of a few seconds.  
Confidence: suspected (read it)

### 25. iperf3 download: a reset stream spins in a tight exception loop until the test ends
Severity: slow  
Where: src\Checks\Toolkit.cs line 339 (`try { n = s.Receive(buf); } catch (SocketException) { continue; }`)  
How to reproduce: iperf3 `-R -t 60` to a server that resets a data stream part-way (server killed, a middlebox RST).  
What happens / what should happen: on Windows, Receive on a reset TCP socket throws ConnectionReset again on every call, so the thread loops throwing and catching for the rest of the test (one core busy). The `continue` is meant for the 2 s ReceiveTimeout only. Continue only on SocketError.TimedOut, and break otherwise. Linux returns 0 after the first ECONNRESET, so mono can't show this.  
Confidence: suspected (read it; Windows-specific)  
Rule: 7

### 26. Encrypted DNS (DoH) reads the whole answer with no size cap and a 300 s per-read timeout
Severity: slow  
Where: src\Checks\Protocols.cs lines 123-124 (`r.ReadToEnd()`; only Timeout set, ReadWriteTimeout left at the default 300 s)  
How to reproduce: a network that intercepts cloudflare-dns.com or dns.google (a captive portal, a filtering proxy) and answers with a large or slow body.  
What happens / what should happen: the tool can use unbounded memory, or wait 5 minutes per stalled read, with no Stop (the tool takes no Run). Read at most a few KB with a total deadline, as Reach.Http does.  
Confidence: suspected (read it)

## Still present from earlier rounds

| earlier id | where now | one-line evidence |
|---|---|---|
| R1 #36 / R2 #7 (partly) | SpeedTest.cs 180-185, 201 | only the Down host's resolved address is checked; Up host never; HttpWebRequest re-resolves (new #4) |
| R2 #54 / R1 #124 (partly) | DestCheck.cs 64-67; Icmp.cs 100; Toolkit.cs 928 | bare `010.1.1.1` now refused, but `http://010.1.1.1/` → 8.1.1.1 and Resolve4/Subnet still take octal/dword (new #7) |
| R1 #229 (partly) | Protocols.cs 458; UdpMtu.cs 29 | the tick gate is added (SenderGate) but there is still no one-device guard (new #5) |
| R1 #24 (residual) | Icmp.cs 97 | non-Lab ping tools have no broadcast/multicast guard (new #6) |
| R2 #71 (residual) | MoreTools.cs 206 | `Cfg.Pins.ToArray()` on the worker without a lock - a smaller window, still unsynchronised |

## Checked and held up
- Lab guards: NotOne unwraps IPv4-mapped addresses and refuses 0.0.0.0, ::, 255.x, 0.x, 224-239, ff00::/8 and each local subnet's broadcast. Names are checked after resolution (a name that resolves to 224.0.0.251 is refused). The ICMP builder and UDP sender hold ≥100 ms between sends (also against a closed port) and ≤1,000 packets. Allowed() honours Policy.NoLab before the tick, and the tick only matters on company networks (as designed).
- Parsers fuzzed (h3/h6.cs, 60k random or mutated inputs each, no exceptions): ParseStun, ParseVarbinds, SnmpCheck, DhcpText 119/121 (pointer loops capped), LdapDone, Json.Parse (depth 64, list cap), Protocols.ReplyOk, and Toolkit.Parse for ≥12-byte inputs. DNS Name() caps at 20 jumps.
- Protocol correctness: RADIUS Response Authenticator and Message-Authenticator (RFC 2865 §3 / RFC 3579 §3.2), secret wiped on every path. DNSKEY key tag (RFC 4034 App. B). SVCB/HTTPS keys. NTP offset/delay formulas, origin check, KoD and alarm. TWAMP field offsets (RFC 5357). RDP X.224 + RDP_NEG_REQ bytes. LDAP rootDSE request. NetFlow v5 header and record layout. SNMP GET is read-only and INTEGER sign handling is right. UDP MTU QUIC VN / DNS padding (RFC 9000 §6, RFC 7830) sizes are exact.
- Native layouts: ICMP_ECHO_REPLY options at 16+IntPtr.Size, ICMPV6_ECHO_REPLY address at 6 / status at 28, MIB_IPFORWARDROW 56-byte stride, DNS_RECORD data at 2P+16, DHCPAPI_PARAMS 32/20-byte stride.
- iperf ParseOpts: `-b 1e30`, `-b -5M`, `-b NaN`, `-l 0`, `-t 99999999999` are rejected or ignored and listed; -b ≤ 10 Gb/s, -w ≤ 16 MB, -l ≤ 1 MB (UDP ≤ 65507); the results JSON is capped at 1 MB with depth-limited parsing.
- Speed test: Policy.NoSpeed is honoured for test, list and check; redirects and non-200/206 answers are refused; caps are 400 MB / 100 MB and 8 s; secs ≥ 0.5 (no divide by zero); units are consistent (decimal Mbps/MB); a stopped upload isn't saved; a ping lost under load counts as 1000 ms.
- Reach.Http: 64 KB body / 20 s / Stop honoured; CR/LF/NUL headers refused; Set-Cookie values hidden. Mail Lines: 1 KB/line, 20 s total, Stop. Lab.Tcp: 4 KB / 3 s. RDP 64 B / 3 s. LDAP 16 KB / 4 s.
- Rule 1: every outbound request in these files comes from a pressed tool or a row in About (CompareDns → "Checks you start"; Portal → "Sign-in page check"; Online.* gated by Online.On and the policy), except #2.
- Rule 8: no Nokia / SR OS / Alcatel / TiMOS strings in these files.
