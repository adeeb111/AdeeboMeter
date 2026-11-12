# 08 - Engineer pack, replays, pcap reader, speed test, ISP report, Gamer Zone, VPN awareness, UDP MTU, outages

Reviewer: independent adversarial pass on /home/user/AdeeboMeter-dev (branch dev-1.1.5, HEAD f6d5747). Line numbers are dev-1.1.5.
Harness: `scratchpad/review/harnessE/H.cs` (pcap/replay fuzzer, crafted inputs, LossStart patterns, redaction, misc), `M.cs` + `srv.py`
(hostile SMTP server). Everything marked "confirmed (ran against test build under mono)" was invoked by reflection on the shipped
`scratchpad/run1/AdeeboMeter.exe` (copied to `harnessE/app/` with a portable marker so run1's data was not touched).

## Fuzz summary (pcap / pcapng reader + all four replays)

- Seeds: the 9 shipped sample .pcap files + one hand-built pcapng (SHB/IDB/EPB carrying an RA).
- Mutators: random byte, bit flip, truncation, 32-bit length plants (0, 1, 2, 4, 12, 255, 65535, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFF), protocol-magic bytes (0x86dd, 0x888e, 58, 134, 43, 44 ...).
- Each input went through `Pcap.Parse` -> `Replay.DhcpRows`, `RaRows`, `EapRows`, `IcmpRowsFromPackets`.
- **530,000 iterations: 0 exceptions, 0 hangs, max 60 ms per input** (two runs: 30k + 500k, seed 1234).
- Crafted: classic caplen 0xFFFFFFFF; pcapng block length 0 / 12 / 13 / 0x7FFFFFFC / 0xFFFFFFFC; 60 MB of 12-byte unknown blocks (53 ms); 20 chained IPv6 hop-by-hop headers; RA options len 0 / type 25 len 255 / type 25 len 3; DHCP option overload (52), zero-length host name, mis-sized router option, message types 0/9/255; EAPOL lengths 0 / 0xFFFF / truncated; ICMP quoting an inner IPv4 header with IHL 0 and 15. **No crash, no infinite loop, no exception** in any of them. The reader's length checks hold up well.
- Extra fuzz: 200,000 random RADIUS replies through `EngPack.DecodeAttrs` + `Protocols.ReplyOk`: 0 exceptions.

What the fuzzing did turn up is wrong results and memory use, not crashes (findings 6, 7, 16).

---

### 1. SMTP path / Mail check: a hostile mail server can use up all memory (OOM) or hang the tool forever
Severity: crash (OOM) / slow (hang)
Where: src\Checks\Protocols.cs lines 184-189, `Protocols.Mail` (unchanged from 1.1.4, but 1.1.5's new `EngPack.SmtpPath` line 351 now points it at the MX hosts of **any domain the user types**, so the server is someone else's)
```
string banner = rd.ReadLine() ?? "";
wr.WriteLine("EHLO adeebometer.local"); var caps = new List<string>(); string l; while ((l = rd.ReadLine()) != null) { caps.Add(...); if (l.Length < 4 || l[3] != '-') break; }
```
How to reproduce: `harnessE/srv.py` on 127.0.0.1:2525, then call `Protocols.Mail("127.0.0.1:2525")` (M.exe):
- `multiline`: "220 hi" then `250-XXXX...` lines forever -> ran 40.8 s (until my server stopped), **peak working set 6,392 MB**; returned an "Offers" row of 2,433 chars.
- `nocrlf`: 64 KB chunks with no line end -> **OutOfMemoryException after 11 s, 4,251 MB** (caught by the generic catch and shown as "Insufficient memory to continue..."; in the 32-bit / 64 MB-budget app other threads are likely to die first).
- `drip`: one byte every 5 s -> `ReadTimeout = 8000` never fires; **still running after 45 s**, limited only by the server. The Stop button does nothing because `Mail` never checks `run.Stop`.
What happens / should happen: the capability list (`caps`) and `StreamReader.ReadLine` are unbounded, and the 8 s timeout applies per read, not to the whole exchange. It should cap each line (for example 1 KB), cap the number of EHLO lines (for example 50), set an overall deadline (for example 20 s from connect), and check `run.Stop`.
Confidence: confirmed (ran against test build under mono).

### 2. Speed test grade: bufferbloat bad enough that every loaded ping times out is graded "A+"
Severity: wrong result
Where: src\Checks\SpeedTest.cs line 244 (`Phase`), line 182 (`Measure`), line 203 (`One`)
```
while (!done()) { double v = One(ip); if (!double.IsNaN(v) && sw.Elapsed.TotalSeconds >= 1) lock (pings) pings.Add(v); ... }
res.Grade = GradeOf(res.Idle, Math.Max(double.IsNaN(res.LoadDown) ? 0 : res.LoadDown, double.IsNaN(res.LoadUp) ? 0 : res.LoadUp));
```
How to reproduce: a line where the queue adds more than about 1 s under load. `One()` gives up after 1000 ms of ICMP plus 1000 ms of TCP and returns NaN. NaN samples are dropped, so `LoadDown` and `LoadUp` are NaN, become 0, and `GradeOf(20, 0)` returns **"A+"** (ran: `GradeOf(20,0) = A+`, `GradeOf(20,1200) = F`). Partial timeouts have the same bias: only the pings that came back under 1 s are counted, so the median is understated.
What happens / should happen: the worst bufferbloat case reads as "Calls and games stay smooth", and the ISP report repeats it under "ALREADY RULED OUT: the router's queue - grade A+". A timed-out loaded ping should count as at least the timeout, and the grade should be "F" (or "-" when there were no loaded samples at all), never "A+".
Confidence: confirmed (GradeOf ran against test build; the NaN path is read from the code).

### 3. MTR loss rule: loss at the destination alone is reported as "real loss starts at hop N" and blamed on the provider
Severity: wrong result
Where: src\Checks\PathTools.cs line 107, `LossStart`, and src\Checks\IspReport.cs lines 94-97 (the `Finding` row)
```
var later = hops.Skip(i + 1).Where(Answers).ToList();
if (later.Count == 0 && !h.Reached) continue;
if (later.All(x => LossOf(x) >= l * 0.6)) return i;   // later is empty for the destination -> All() == true
```
How to reproduce (LossStart and MtrVerdict on the test build):
- Hops 192.168.1.1 0%, 10.20.0.1 0%, 203.0.113.1 0%, destination 198.51.100.7 10% (reached): result `3`, "Real loss starts at hop 4 (198.51.100.7) and carries on to the end - 10% lost from there."
- The same with the destination at 50% (typical ICMP rate limiting): result `2`, the same text.
What happens / should happen: the destination is "carried on" vacuously because it has no later hops. As a result `MtrVerdict`'s "Loss at the destination only ... rate-limits pings" branch and IspReport's "Only the destination drops some probes ... the path itself is clean" branch can never run for loss of 3% or more. The ISP report then says in red "Loss starts at hop N ..., inside or past the provider's network, and carries through to the destination. The home network was clean." and the paste-ready message asks the ISP to "check that link". That is exactly the "loss that doesn't carry on isn't real" rule the brief says must hold. The destination should start real loss only if the hop before it (the last answering router) also loses, or the result should be reported as destination-only.
Confidence: confirmed (ran against test build under mono).

### 4. ISP report: loss starting at a provider hop with a private or CGNAT address is blamed on the home network
Severity: wrong result
Where: src\Checks\IspReport.cs line 76 (`firstIsp` = first hop with a non-private address), line 94-95, `Private()` line 24
How to reproduce: hops 192.168.1.1 0%, **10.11.0.1 8%**, 100.64.0.1 8%, 203.0.113.9 8%, destination 8%. Many ISPs number their access and core hops from RFC 1918 or CGNAT space. `LossStart` returns 1 (hop 2, confirmed). `firstIsp` = 3 (the first public hop), so `start < firstIsp`, and the finding is "Loss starts at hop 2 - before the provider (Wi-Fi or the router). Fix the home side first." The paste-ready message changes to "Could you check my line?".
What happens / should happen: the home side is hop 1, the default gateway (or anything up to `m.Gateway`). The boundary should be "the hop after the gateway", not "the first public address". As it stands, the ISP's own fault sends the user back to their Wi-Fi.
Confidence: LossStart output confirmed (ran); the finding text is read from the code.

### 5. MTR loss rule: a total outage past hop N during the MTR run is reported as "No real loss"
Severity: wrong result
Where: src\Checks\PathTools.cs lines 99-111 (`Answers` = `Lost < Sent`)
How to reproduce: the trace answered hops 1-4, then everything past hop 2 stopped answering for the whole MTR minute (Sent 120 / Lost 120 at hops 3-4). Result `-1`: "No real loss: any loss at a middle hop doesn't carry on - that router just ignores some pings (normal). The destination doesn't answer pings at all, so this is judged by the last router that does (hop 2)."
What happens / should happen: hops that answered the trace (`Ip != ""`, `Reached` on the destination) and then lost 100% are treated as silent routers. The ISP report then says the path is clean during an outage. A hop that answered in the trace and then went to 100% (especially the reached destination) is real loss.
Confidence: confirmed (ran against test build under mono).

### 6. Pcap reader: a 63 MB capture (just under the 64 MB limit) needs about 175 MB of managed memory
Severity: slow (memory - breaks the 64 MB budget, rule 7)
Where: src\Checks\Pcap.cs line 35 (`File.ReadAllBytes`), lines 56-63 and 91-109 (one `Pkt` per frame: `IPAddress` x2, MAC string built with LINQ, payload copy)
How to reproduce: a classic pcap of 300-byte DHCP-like UDP frames filling 63 MB (184,526 packets, under both caps). `Pcap.Parse` took 320 ms; **managed heap grew by about 111 MB on top of the 63 MB file array** (about 175 MB peak). On the 32-bit build with a 64 MB target this is the largest allocation in the app, and it all goes onto the LOH.
What happens / should happen: the file is loaded whole and every frame is kept, even though each replay uses only a few hundred of them. The reader should stream it (FileStream + per-record reads), drop frames the chosen replay ignores (for DHCP keep only UDP 67/68; for EAP only 0x888e; and so on), and lower MaxPackets.
Confidence: confirmed (ran against test build under mono).

### 7. pcapng: nanosecond (and any non-microsecond) timestamps are 1000x off
Severity: wrong result
Where: src\Checks\Pcap.cs line 79 (`double t = ts / 1e6;`) - `if_tsresol` in the IDB is never read
How to reproduce: the dhcp-good sample rewritten as pcapng with `if_tsresol = 9` (Wireshark / dumpcap default on Linux, and on Windows with some Npcap settings), packets 1 s apart. The DHCP replay prints Offer at "1000.000 s", ACK at "3000.000 s", and "Lease for ... in **3000.000 s**" (should be 3 s).
What happens / should happen: the replay's whole timing story ("ACK took 50 minutes") is wrong. The IDB options should be parsed for `if_tsresol` (option 9) per interface, and `ts` scaled by it.
Confidence: confirmed (ran against test build under mono).

### 8. Speed test "check" sends requests to every server even when the policy turns the speed test off
Severity: security (policy bypass, rule 6)
Where: src\Checks\SpeedTest.cs line 101 (`Run` -> `Servers(check)`) and line 128 (`Head(s.Down)`). The `Policy.NoSpeed` test exists only in `Measure` (line 153).
How to reproduce: `AdeeboMeter.policy` with `speed_test = off`. Toolbox > Speed test, type `check`, Run. About 20 HTTPS range requests go out (Cloudflare, 17 Vultr cities, 2 OVH), one at a time.
What happens / should happen: the policy says "the speed test (all servers)" is off. `Run` should return the policy row before both `list/check` and `Measure`.
Confidence: suspected (read it - the path is direct; the network is not reachable from the harness).

### 9. Speed test: redirects and other non-file answers are measured and reported as a real speed
Severity: wrong result
Where: src\Checks\SpeedTest.cs line 146 (`AllowAutoRedirect = false`), lines 232-236, 139 (`Head`)
How to reproduce: a server line or typed URL whose server answers `302 Found` (many hosting test files move, and http->https upgrades are common). With redirects off, `GetResponse()` returns the 302 without throwing. The download loop reads its body of a few hundred bytes, fetches again and again for 8 s, and `Phase` returns a tiny non-NaN Mbps. The result shows in green as e.g. "Download 0.2 Mbps", is saved as `SpeedTest.Last`, and goes into History and the ISP report. `Head()` also reports such a server as "answers" in `check`.
What happens / should happen: any status other than 200/206 (and a Content-Length far below what is expected) should be "this server didn't send its test file".
Confidence: suspected (read it).

### 10. Speed test: only 2 of the "4 streams" run at once, and the time base includes thread-join waits
Severity: wrong result
Where: src\Checks\SpeedTest.cs lines 215-246. Nowhere in the source or config sets `ServicePointManager.DefaultConnectionLimit` (grep: no hits; no app.config)
What happens: on .NET Framework, a desktop app gets 2 connections per host (per proxy when `GetSystemWebProxy` applies). Streams 3 and 4 queue inside `GetResponse` and can time out after 6 s, which counts towards `fails`, so fast lines are understated. Separately, `secs` is taken **after** `foreach (var t in th) t.Join(3000)` (line 245-246), so a stream stuck in `GetResponse` or `Read` stretches the divisor by up to 3 s per thread and lowers Mbps further.
Should: set `ServicePointManager.FindServicePoint(uri).ConnectionLimit` (or DefaultConnectionLimit) to at least the number of streams, and record `secs` when `done()` first becomes true.
Confidence: suspected (read it; the connection limit is documented framework behaviour).

### 11. Speed test stopped during upload is saved and reported as a complete result
Severity: wrong result
Where: src\Checks\SpeedTest.cs lines 172-186
What happens: `run.Stop` is checked only after the download phase. A Stop during the 8 s upload exits `Phase` early. The code then computes `Up` from the partial window, grades it, sets `SpeedTest.Last`, writes the History event "Speed test to ...: X down / Y up" and logs it as a normal run. The run should be marked "stopped" after the upload phase too (`res.Error = "stopped"`). Related: when the upload has no samples, `Up` is NaN and the Toolbox shows a green "Upload - Mbps" row (line 108, level 0).
Confidence: suspected (read it).

### 12. Gamer / ISP report redaction: the Wi-Fi-name regex corrupts the report and fails on HTML-escaped names; LAN hostnames are not redacted
Severity: security (privacy) / cosmetic
Where: src\Gamer\Gamer.cs line 614 (`GameSession.Redact`); applied to the **whole HTML** in src\Checks\IspReport.cs line 294
How to reproduce (Redact on the test build):
- SSID `td` -> `<[Wi-Fi name] class="n">Home router (hop 1)</[Wi-Fi name]>` - **the saved ISP report HTML is broken**. The same happens with SSIDs `tr`, `div`, `table`, `body`, `html`, `class`.
- SSID `ms` -> "2.1 [Wi-Fi name] avg" (every "ms" unit replaced, which also reveals the SSID by elimination). SSID `loss`, `Home` or `Router` damage the text the same way.
- SSID `AT&T WiFi`: the HTML holds `AT&amp;T WiFi`, so **the name is not redacted** (the raw SSID is matched against escaped text).
- `fe80::1%12` -> "[internal address]%12"; a public IPv6 with an EUI-64 interface id (2001:db8:1:2:a00:27ff:fe4e:66a1 - it encodes MAC 08:00:27:4e:66:a1) and CGNAT 100.64.x.x pass through. The CSV hides 100.64/10 as "(private)", but the HTML does not.
- The ISP report's hop table prints `nm(ip) + " (" + ip + ")"`. For private hops only the IP is redacted, and the **reverse-DNS name of the home router or LAN devices is kept** (e.g. "Johns-MacBook.lan ([internal address])", "RT-AX88U-1F2C.home").
Should: redact the row values before HTML encoding, not the finished markup; match the SSID only as a whole word (or not at all when it is shorter than 4 characters, or a common word); replace names of private hops; also redact 100.64/10 and %zone suffixes.
Confidence: confirmed (ran against test build under mono).

### 13. Find my match server picks the PC's own address when the PC has a public IPv4
Severity: wrong result
Where: src\Gamer\Gamer.cs lines 652-664, `MatchFinder.Busiest`
How to reproduce: tshark output in which the PC's own public address (bridge-mode modem, university or office networks, some ISPs that give the PC a public address directly) appears in every packet. `Busiest` returned **203.0.113.50 (the PC itself)** instead of the match server 198.51.100.20. That address then goes into the game's server list as "exact" (FullView.Gamer.cs line 156) and gets pinged as "the match server".
Should: leave out this PC's own unicast addresses (from `NetworkInterface`), or count only the remote side per packet direction.
Confidence: confirmed (ran against test build under mono).

### 14. Outage log: an outage that ends with a network change is never closed - it merges into a 10-hour outage or stays "ongoing"
Severity: wrong result
Where: src\Brain\Outages.cs lines 30-41 (`Pair`), fed by src\Monitor.cs lines 239-240 and 245 (`netChanged` resets `netDown = 0` without logging "Internet back")
How to reproduce: Wi-Fi drops, so after 3 failed pings "went quiet" (Lv 2) is logged. Windows reconnects (address change, so `netChanged`), which clears `netDown` and emits no Lv 0 event. Pair input [quiet 18:00, quiet 04:00(+1d), back 04:05, quiet 18:00, back 18:02] gives **"Tue 10 Nov 18:00 - 04:05 · 10 h 5 min"** plus the real 2-min one. If no later "back" ever comes, the outage shows "now ... so far" indefinitely. The same happens when the internet check is turned off mid-outage (line 244) or the app quits mid-outage. A backwards clock step (DST fall-back) during an outage puts "back" before "quiet", and the outage becomes "ongoing".
Also: "From" is the time of the 3rd failure, not the first (2 x interval late); `Last(m, days)` caches for 60 s regardless of `days`; time asleep with an open outage counts as outage.
Should: Monitor should emit a closing event whenever it resets `netDown >= 3`. `Pair` should close an open outage at the next Lv 2 event's predecessor, at the first later successful sample, or at app start, and should cap or flag gaps without data (sleep or app closed).
Confidence: Pair result confirmed (ran against test build); the Monitor cause is read from the code.

### 15. SMTP path: an invalid certificate is shown as "Got as far as ... > certificate checked" in green; the port 25 hint is dead code
Severity: wrong result
Where: src\Checks\EngPack.cs lines 300-310 (`HowFar`), 353-354
How to reproduce: rows from a server whose STARTTLS certificate fails validation (the exception row "The remote certificate is invalid according to the validation procedure."). `HowFar` returned **"MX found > connected > greeting > EHLO > STARTTLS > certificate checked"**, and line 353 makes it level 0 (green) because it ends with "certificate checked". The verdict is built by substring search over all rows, so server-chosen banner text ("220 ... certificate ...", "refused") also steers it. In addition, line 354 tests `r[0][2]` (the HowFar text) for "couldn't", which HowFar never produces ("connection FAILED"), so the "most home providers block outgoing port 25" advice never appears.
Should: build the step list from structured results (booleans returned by Mail), not from words in rows.
Confidence: confirmed (ran HowFar against test build); line 354 is read from the code.

### 16. ICMP translator: an inner header with IHL < 5 prints a bogus port
Severity: cosmetic
Where: src\Checks\Replay.cs lines 186-189 (`Inner`)
How to reproduce: an ICMP 3/4 quoting an IPv4 header 0x40 ... (IHL 0). Output "About: TCP to 5.6.7.8:0". The port is read from inside the IP header itself.
Should: require ihl >= 20.
Confidence: confirmed (ran against test build under mono).

### 17. Certificate chain compare never compares non-443 sites
Severity: wrong result
Where: src\Checks\EngPack.cs line 41 (`Host = h + (port != 443 ? ":" + port : "")`) vs line 33 (`ParseCert` requires `Games.ValidHost(p[1])`)
How to reproduce: `ParseCert("snap|intranet.example.com:8443|run 1|202611120900|trusted by Windows|CN~Issuer~AB~2027-01-01~")` returned **null**. Every saved snapshot for host:port is dropped on reload, so the second run always says "now change one thing ... and run again" and never shows "COMPARED WITH ...". These lines are also lost from cert-compare.txt on the next write.
Confidence: confirmed (ran against test build under mono).

### 18. RADIUS lab: User-Password longer than 128 bytes is malformed (the length byte wraps at 252+)
Severity: wrong result
Where: src\Checks\EngPack.cs lines 199-211 (`HidePassword`), line 280 (`add`)
How to reproduce: HidePassword with a 252-byte password gave a 256-byte value, so the attribute length byte is **2**. A 260-byte password gave a 272-byte value with **length byte 18**. 129-200 bytes give 144-208 bytes, which breaks RFC 2865's 128-byte limit, and the bytes past 128 are encrypted zeros (the copy is clamped to 128 but `n` is not). The server sees a malformed packet and drops it silently, which the tool reports as "wrong shared secret / not a client".
Should: refuse passwords over 128 bytes with a clear row.
Confidence: confirmed (ran against test build under mono).

### 19. RADIUS lab: a corrupt aaa-runs.txt line breaks the tool on every later run
Severity: crash (tool-level; caught as "Couldn't finish")
Where: src\Checks\EngPack.cs line 322 (`DateTime.ParseExact(p[2], ...)`)
What happens: any `aaa|host:port|<not yyyyMMddHHmm>|...` line with 6 or more fields that becomes `prevSame` or `prevOther` throws FormatException after the request was sent. The user never sees the result, and the line is never pruned (it stays in the last 30). Use `TryParseExact` and skip the line.
Confidence: suspected (read it).

### 20. RADIUS lab accepts Access-Accept without a Message-Authenticator
Severity: security (low - lab tool)
Where: src\Checks\Protocols.cs lines 381-403 (`ReplyOk`)
What happens: the request always carries Message-Authenticator (BlastRADIUS hardening). The reply's M-A is checked only "when the reply carries one", so a reply without it is "signed" if the MD5 Response Authenticator matches. The current BlastRADIUS guidance (the RFC 3579 update / draft-ietf-radext-deprecating-radius) is that clients which sent M-A should require it in Access-Accept/Reject/Challenge. Reporting "accepted" there teaches the wrong baseline. Should: show a warning row ("server doesn't send Message-Authenticator - not hardened against BlastRADIUS") or treat it as unsigned.
Secret handling: checked. The secret and password are never logged (`Program.Log` gets only the address), never shown, blanked from `shown`/`key` (FullView.Toolbox.cs line 660-661), masked boxes, and arrays wiped. UdpClient.Connect filters replies from other source addresses. No issue found there.
Confidence: suspected (read it).

### 21. VPN awareness: Tailscale, Cloudflare WARP, NordLynx, ZeroTier and Wintun tunnels are "not connected"; full tunnels without a gateway entry read as "split"; PPPoE reads as a VPN
Severity: wrong result
Where: src\Monitor.cs lines 269-276 (`vpnWords`, `IsVpn`), used by src\Checks\VpnAware.cs lines 46-54
What happens:
- False negatives: the adapter descriptions "Tailscale Tunnel", "Cloudflare WARP Interface Tunnel", "NordLynx Tunnel", "ZeroTier Virtual Port" and "Wintun Userspace Tunnel" contain none of `vpnWords`. VpnAware lists the product (it knows the process) but then says "Tunnel adapter: none up - the VPN isn't connected right now", and the tunnel latency layer is skipped.
- `FullTunnel` = the adapter has an IPv4 gateway. WireGuard (route 0.0.0.0/0 on-link, no gateway) and OpenVPN def1 (0/1 + 128/1 via route.exe) are full tunnels with no adapter gateway, so they show "company traffic only (split tunnel)". The routing table should be checked for a default or half-default route via the tunnel interface.
- False positive: `NetworkInterfaceType.Ppp` is VPN, so a PPPoE broadband connection on the PC shows as a VPN with the ISP's DNS as "the VPN's far side", and "Your VPN is the slow part" is possible. Hyper-V / WSL / Docker vEthernet, VirtualBox Host-Only and Npcap Loopback are correctly not matched.
Confidence: suspected (read it; adapter description strings are from those products' Windows drivers).

### 22. Game server names that collide after truncation make Side-by-side throw
Severity: crash (tool-level)
Where: src\Gamer\GameCompare.cs line 70 (`servers.ToDictionary(z => z.Name, ...)`); src\Gamer\Gamer.cs line 117 (`SaveGame` writes `Clean(s.Name, 30)`), line 103 (ApplyFile does not de-duplicate)
How to reproduce: in Edit, leave Name blank on two servers (Name defaults to the host) with hosts sharing their first 30 characters, e.g. `eu-west-gameservers.provider.example.com` / `eu-west-gameservers.provider.example.net`. The editor sees distinct names and saves. On reload both are `eu-west-gameservers.provider.` -> GameCompare: ArgumentException "An item with the same key has already been added" -> "Couldn't finish". Plan() (line 377) and the `due` dictionary also key on Name, so the second server is never surveyed. A hand-edited games.txt with duplicate names gives the same result.
Confidence: suspected (read it).

### 23. Speed server list: a user line with id "cloudflare" silently changes what "auto" means
Severity: cosmetic / wrong result
Where: src\Checks\SpeedTest.cs line 59 (`l.RemoveAll(x => x.Id == s.Id); l.Add(s);`) and line 81 (`auto` = `l[0]`)
What happens: overriding a built-in id moves it to the end, so `l[0]` (what "auto" and the default pick) becomes Vultr Toronto. SafeUrl allows `http://localhost`, `http://127.0.0.1`, `http://[::1]`, `http://192.168.1.1/...`, `https://169.254.169.254/...` and `http://0x7f000001/` (all ran -> True). A speedservers.txt someone hands you can therefore make the speed test GET a router admin URL with 4 streams for 8 s and POST 4 MB blobs to any URL (the `Up` field). Low risk for a desktop app, but the comment "a real host name or public address" (line 67) is not what the code does.
Confidence: SafeUrl confirmed (ran); the rest is read from the code.

### 24. Replays: unsupported link types (802.11 / radiotap) give "no DHCP / no EAPOL in this capture" instead of saying why
Severity: cosmetic
Where: src\Checks\Pcap.cs line 104 (`else return null;`)
What happens: a Wi-Fi monitor-mode capture (link type 105/127) or any other link type is read as 0 packets, with advice to capture again. The reader should report "link type N isn't supported". Related: classic pcap snaplen and orig_len are ignored (harmless), and SPB timestamps reuse the previous packet's.
Confidence: confirmed (ran: unknown link types return 0 packets).

### 25. Network discovery (Reach.cs, changed in 1.1.5) binds UDP sockets that accept unicast replies from any LAN host for 4 s
Severity: security (rule 2 interpretation)
Where: src\Checks\Reach.cs lines 192-199 (`u.Client.Bind(new IPEndPoint(local, 0))`), mDNS QU bit set
What happens: by design, the Ask-the-network tool now opens three bound UDP ports and asks devices to send unicast replies to them. The 1.1.5 text itself mentions the Windows Firewall "allow AdeeboMeter" question. That is an inbound-accepting socket (short-lived, user-run), and owner rule 2 says "No open listening port, ever". Flagged so the owner can decide whether this counts.
Confidence: suspected (read it).

---

## Checked and found OK (no finding)
- Pcap/pcapng lengths: block length 0/13/huge, caplen 0xFFFFFFFF, cap > blen, negative ints, 64-bit sums: all bounded (fuzz above). The IPv6 extension-header loop is capped at 8. RA option length 0 breaks the loop. DHCP options are bounds-checked, with pad (0) and end (255) handled. EAPOL short frames are fine.
- ISP report public IP: `PublicIp()` runs only when the third box is exactly "yes" (IspReport.cs line 158). Nothing else in rows, the message, the HTML or the CSV carries the user's public address (hops are routers; "Addresses" says only "a public IPv6 address" without the address).
- Find my match server: tshark arguments are built only from a parsed GUID and integer ports, so there is no injection. `Policy.NoCapture` is honoured.
- Game editor: ports are validated 1-65535 (`NumberStyles.None`), and "1-99999", "0", "a,b" are rejected. games.txt `ApplyFile` uses `TryParse` with range -1..65535. Hosts go through `ValidHost`.
- UdpMtu: at most about 26 packets, a connected socket, DF set, only to the one target. DnsPad sizes are exact (checked by arithmetic).
- Traffic Lab DSCP (Lab.cs): `Policy.NoLab` now checked in `Allowed`; the gap is clamped to `MinGapMs`; counts are capped by `Count()`.
- RADIUS reply from a spoofed source: `UdpClient.Connect` makes the socket accept only the server's address and port. A wrong identifier or authenticator is rejected by `ReplyOk`.
- TACACS+: only 3 TCP connects (no protocol exchange, so the "unencrypted flag" case cannot occur). The row "The daemon is listening" is true of any TCP 49 listener; it should be worded that way.

## Rule 8 (no Nokia references) in my files
- src\Checks\Protocols.cs line 418, Twamp hint: "On SR OS: a TWAMP-light reflector on the router" - SR OS is Nokia's router OS. No other "Nokia" / "SR OS" / "TiMOS" / "7750" hits in src, knowledge, docs or README (grep).
