# Findings - Claude Code, round 4 (deep inspection)

Commit reviewed: **e80412e** (branch dev-1.1.5), 8 commits since round 3's 8502674 (+3,103 / -663; new files src\Checks\CertChain.cs, src\Checks\Fun.cs, src\Mood.cs, tools\analyze.sh, tools\analyzers.globalconfig).

## How this round was done

- **Coverage.** Eight reviewers each took a slice and read every line of it: all 110 C# files under src\ (34,680 lines), plus knowledge\*.txt where the slice used them, both GitHub workflows, hof_verify.py, the issue templates, release.ps1, build_exe.cmd, store\, .gitignore, tools\ and the docs and website. The slices: S1 core, S2 Brain/Link/Capture/WindowsDeep/M365/Knowledge, S3 network tools, S4 security checkup/certificates/crypto/replays/fun tools, S5 SNMP/routing/ISP report/investigations, S6 Gamer/Labs/Learn/calculators, S7 main UI, S8 the rest of the UI/self-test/CI/docs. The lead re-read the items marked "lead re-read" below.
- **Build.** e80412e was built with mono (C# 5, knowledge resources embedded) into r4.dll / r4.exe and driven by reflection harnesses. Three Windows-only pieces were stubbed, and each limits what "confirmed" can mean:
  - **ECDiffieHellmanCng** throws PlatformNotSupportedException. P-384 recipients mode and `.adeebokey` key agreement never ran end to end on real CNG keys; the KDF and the header checks were read and checked piece by piece.
  - **EventLogReader** throws at use. Everything that reads the Windows event log (Wi-Fi drop reasons, crashes, 802.1X sign-in evidence, Windows-update breakage) was read, not run.
  - **crypt32 signature check** (`CertChain.SignedBy`) goes through a stub libcrypt32.so that returns TRUE (or STUB_SIG). The chain walk, ordering and verdict logic ran for real; real signature verification did not. mono's SslStream and X509Chain are not SChannel either, and that is said where it matters.
  - Other mono differences: P/Invokes into Windows fail, there are no IPv6 sockets in the container, .NET Framework's UdpClient.Receive ignores the endpoint, and Int32.ToString never emits native digits. Findings reason from .NET Framework behaviour where the two differ, and say so.
- **Harness counts and fuzzing (totals from the section files).**
  - Pcap / pcapng: 200,000 mutated files through Pcap.Parse and the DHCP, RA, 802.1X and ICMP replays: 0 exceptions, none over 1 s.
  - SNMP decoder: 200,000 mutated v2c replies and 60,000 mutated v3 messages: 0 exceptions, at most 8 ms each. A hostile Python SNMP agent with 10+ misbehaviour modes.
  - Network parsers: 60,000 random or mutated inputs each through ParseStun, ParseVarbinds, SnmpCheck, DhcpText 119/121, LdapDone, Json.Parse, Protocols.ReplyOk and Toolkit.Parse: 0 exceptions. Hostile HTTP / DoH-drip / redirect / NTP servers and a hostile iperf3 server in Python.
  - Certificates: 11 generated chains (full, missing intermediate, wrong order, expired, not yet valid, non-CA signer, cross-signed, 11-long, self-signed, RSA-1024, P-384) against local TLS 1.2 / 1.3 / mTLS servers.
  - MTR / ISP report: 31 hop patterns through LossStart / MtrVerdict, plus crafted ISP routes.
  - Calculators and games: 1,500 random VLSM cases and 2,000 number-converter inputs diffed against Python, 5,000 subnetting-quiz seeds, 5,000 quick-fire addresses: 0 mismatches apart from the findings below.
  - Hall of Fame: 349 random names from every Unicode plane round-tripped through CleanName / clean_name; hof_verify.py run on a copy with a dummy key and hostile issue bodies.
  - Website: Playwright + Chrome at 360, 768 and 1366 px, light and dark.
  - The deep test and the full self-test were not run on Windows.
- **What was not real.** The netsh samples in 13 languages (S2) were written from knowledge of localized Windows, not captured on real PCs: findings that depend on exact translated labels say so. No real Hall of Fame key was used (a dummy HOF_KEY on a copy of hof_verify.py). Nothing was committed or pushed, and the dev checkout was not changed.
- **Confidence.** *Confirmed* means the behaviour was reproduced by running the real code (harness, mono build, hof_verify, git, Playwright). A few are marked "confirmed (read)": the fact is fully visible in the files with no runtime dependence (a literal, a missing string in the compiled assembly, a comment). *Suspected* means read only: the path depends on Windows APIs, a localized Windows, or a network the container doesn't have.
- **Duplicates.** Findings that two or more slices reported, or that share one cause and one fix, are merged. Each title ends with the slice finding(s) it came from, e.g. "(from S3-1)". 151 slice findings became 137 entries (12 merges).

## Totals

| Severity | Confirmed | Suspected | Total |
|---|---|---|---|
| crash | 0 | 0 | 0 |
| security | 10 | 5 | 15 |
| wrong result | 62 | 25 | 87 |
| slow | 5 | 5 | 10 |
| cosmetic | 19 | 6 | 25 |
| **Total** | **96** | **41** | **137** |

By section (slice findings before merging, by the slice's own severity):

| Section | Files | Findings | security | wrong result | slow | cosmetic |
|---|---|---|---|---|---|---|
| S1 core | Program, AppCtx, Setup, Policy, Report, History, Model, Native, Points, HallOfFame, Mood, Lang, Examples, Qr, Monitor | 12 | 2 | 7 | 1 | 2 |
| S2 brain, link, capture | Brain, Link, Capture, WindowsDeep, M365, Knowledge | 17 | 1 | 11 | 1 | 4 |
| S3 network tools | Toolkit, DestCheck, Reach, Icmp, Online, Protocols, MoreTools, Tools, Lab, UdpMtu, SpeedTest | 26 | 6 | 14 | 4 | 2 |
| S4 security, certs, crypto | SecChecks, CertChain, CertKeys, CertTools, EcKeys, FileCrypt, EngPack, Pcap, Replay, Fun | 21 | 4 | 13 | 1 | 3 |
| S5 SNMP, route, investigate | Snmp*, RoutingTools, PathTools, IspReport, Investigate*, WhyFails, HealthCheck, Inspection, WinEvidence, SysTools, VpnAware | 22 | 0 | 19 | 2 | 1 |
| S6 gamer, labs, learn | Gamer, Labs, Learn games, Calc, Picks, OverTime | 15 | 0 | 9 | 2 | 4 |
| S7 main UI | FullView, Toolbox, ToolHome, Tools, Examples, Settings, Overview, Paint, Controls, Theme, examples.txt, lang-fr.txt | 21 | 2 | 11 | 2 | 6 |
| S8 rest of UI, CI, docs | other Ui files, SelfTest, workflows, hof_verify, release, docs | 17 | 3 | 9 | 0 | 5 |
| **Total** | | **151** | **18** | **93** | **13** | **27** |

The 12 merges: S1-4 + S7-4; S4-3 + S5-12; S1-11 + S2-9; S2-1 + S2-15; S2-7 + S4-13; S3-2 + S7-1 + S7-2; S1-12 + S7-19; S1-7 + S5-8; S3-14 + S4-14; S7-3 + S7-8; S3-5 + S3-6; S3-23 + S3-24 + S3-26.

## Rule breaks

| Rule | Findings |
|---|---|
| 1. Offline (nothing the user didn't ask for, everything listed) | 1 (revocation lookups past the switch and the policy), 3, 10, 11, 12, 30, 31, 32 |
| 2. Never listens | none found |
| 3. No admin | none found |
| 4. Secrets are never kept | 4 |
| 5. SNMP is read-only | none found (no SET anywhere) |
| 6. The policy can only switch things off | 1 (the reverse failure: `traffic_lab = off`, `speed_test = off` and `online_lookups = off` don't switch off what they name) |
| 7. Low weight (64 MB, no busy loops, no idle repaints) | 101, 103, 104, 107, 109, 110, 111, 112 |
| 8. No Nokia references | 33 |
| 9. Examples are made-up data | none found |
| 10. Points can't be farmed / award path | 5, 22, 23, 134 (adjacent) |
| Brief's safety guard ("one device only") | 8 |

## Top 20 to fix before release

1. **#1** IT policy switches don't switch off iperf3 / bufferbloat / DSCP ping / Wake-on-LAN, the closest-server search, or the inspector's revocation lookup.
2. **#2** A hostile iperf3 server makes AdeeboMeter open thousands of TCP connections (8,470 in 20 s).
3. **#3** WhyFails.Clock() sends HTTP to msftconnecttest.com and runs w32tm without the Sign-in page switch (HTTPS check, Check a destination, A website won't open).
4. **#4** HTTP request builder shows and keeps a URL's user:password (Reach.cs line 83).
5. **#5** Every push to main / store / dev-* builds an exe with HOF_KEY and uploads it as an artifact.
6. **#16** Fresh installs (and "Just run it from here") always open in Pro mode.
7. **#6** In Serious mode the ARP-spoofing pop-up never appears.
8. **#17** "Time lost waiting" is inside a // comment, so the Month and Six-month reviews lost the row.
9. **#18** "Can I close it?" says svchost.exe is OK to close.
10. **#15** A .pyc is committed and .gitignore misses dist\, data\, *.sha256, the portable and policy files.
11. **#33** PLAN-1.1.5.md line 8 names the forbidden company (rule 8).
12. **#20** Import settings drops mood, revocation, buddy, hat, outage best and overlay position.
13. **#22** Learn games still pay for answers they have already shown (rule 10).
14. **#7** speedservers.txt: the upload host is never checked against private addresses, and the download host is re-resolved.
15. **#8** TWAMP-light, MTU over UDP and the ping tools send to multicast and broadcast addresses.
16. **#9** Hall of Fame: whoever submits a shared card first attaches their own social handle to it.
17. **#21** A match session records almost nothing for its own server, then reports "loss 99-100%".
18. **#27** HTTPS check shows a green "A good padlock" when the handshake fails after the certificate (mTLS).
19. **#36** "2,4 GHz" (decimal comma) switches off every 2.4 GHz rule in six languages, including the air log.
20. **#103** A hostile key-backup file is decrypted whole into memory before the HMAC is checked (300 MB file: 585 MB, 12 s).

---

## Security

### 1. IT policy switches that don't switch their tools off: Traffic Lab's iperf3 / bufferbloat / DSCP / Wake-on-LAN, the closest speed server, and the inspector's revocation lookup (from S7-1, S7-2, S3-2)
Severity: security  
Where: src\Ui\FullView.Toolbox.cs line 242 (`LabExempt` = wol, iperf, bloat, dscp), line 728 (the gate is applied only to LabGate / SenderGate), lines 50-53 and 140 (the Do lambdas call Toolkit.Iperf / Loaded / DscpPing / Wake directly); src\Checks\Toolkit.cs line 158 (Iperf: no Policy.NoLab check). Policy.NoLab is checked only in Lab.Allowed and src\Checks\SecChecks.cs line 435 (lead re-read). src\Ui\FullView.Overview.cs lines 28, 39 and 53-80 (SpeedMenu / FindClosestSpeed: no Policy.NoSpeed check). src\Checks\Toolkit.cs line 784 (Tls always adds the `revoke:` row) and lines 722-753 (Revocation: X509RevocationMode.Online, no gate); src\Ui\FullView.Toolbox.cs line 376 (handler)  
How to reproduce:  
- `traffic_lab = off` in AdeeboMeter.policy, then Toolbox › Traffic Lab › LOAD & QUEUES › iperf3 to your server (or Latency under load, DSCP-marked ping, Wake-on-LAN) › Run.
- `speed_test = off`, then Overview › the "Server: … · change" chip › Find the closest server for me.
- `online_lookups = off` (or Settings › Privacy & online lookups › Certificate revocation checks off), then HTTP and TLS inspector on any https site, and click the Revocation row.  

What happens / what should happen:  
- **Traffic Lab.** The test runs. With Policy.NoLab = true, Toolkit.Iperf("127.0.0.1:15201", "-t 1") still opened a connection to the local test server ("Settings TCP · 1 stream · 1 s", then "Upload failed: server closed"). The policy file says `traffic_lab = off` covers "Toolbox › Traffic Lab (building and sending test packets)", and iperf3 sends UDP at whatever `-b` the user types. LabExempt was meant only to keep these tools out of the "my own network" tick; it also takes them out of the IT switch. Check Policy.NoLab for every tool whose section is "lab".
- **Closest speed server.** A DNS lookup, then a ping (or a TCP connect to 443), goes to Cloudflare, every Vultr and OVHcloud city and every speedservers.txt entry. SpeedTest.Measure and Run refuse under the policy (SpeedTest.cs lines 115 and 173); this path never checks it. It also skips Measure's `Reachable(ip)` guard, so a speedservers.txt name that resolves into the LAN or loopback is probed. Check Policy.NoSpeed, grey the server menu under the policy, and apply the Reachable filter.
- **Revocation in the inspector.** chain.Build fetches OCSP / CRL (and AIA) from the CA, while About › What leaves your PC says "Certificate revocation: Off". CertChain.cs lines 148 and 364 gate the same check on `CertCfg.Revocation && !Policy.NoOnline`; Toolkit.cs has no Policy or CertCfg reference at all. Hide the row or say "off by your IT team / in Settings". The handler also sets tbBusy = true without checking whether another tool runs, and Revocation ignores its Run, so Stop does nothing.  

Confidence: confirmed (ran it: s7/lab.cs with Policy.NoLab = true, the local listener counted the connection; lead re-read LabExempt and every NoLab reference). Revocation: confirmed (read: no gate in Toolkit.cs, both gates in CertChain). Closest server: suspected (read)  
Rule: 6 (the policy fails to switch off what it names) and 1 (revocation lookups with the switch off)  

### 2. A hostile iperf3 server makes AdeeboMeter open thousands of TCP connections (8,470 in 20 s) (from S3-1)
Severity: security  
Where: src\Checks\Toolkit.cs lines 288-310 (IperfOne, state 10 CREATE_STREAMS), lines 275-277 (guard: deadline secs + 60 s)  
How to reproduce: h3/iperfsrv.py accepts the control connection, reads the 37-byte cookie, then sends 0x0A (CREATE_STREAMS) every 10 ms and accepts every stream. In the Toolbox: iperf3 to 127.0.0.1:15201 with `-P 16 -t 1` (h3/h5.cs).  
What happens / what should happen: every 0x0A opens another `par` sockets and adds them to `streams`. The server logged "stream connections opened in 20 s: 8470". The only limit is the guard deadline (secs + 60 s, up to 180 s with -t 120), long enough to use up Windows' ~16,000 ephemeral ports, so every other program on the PC fails to connect until the sockets close and TIME_WAIT ends. Any public iperf3 server can do this. Accept CREATE_STREAMS once (each state at most once, in order) and cap `streams` at `par`.  
Confidence: confirmed (ran it: mono harness and a Python fake server)  

### 3. WhyFails.Clock() sends HTTP to msftconnecttest.com and runs w32tm with the Sign-in page check switched off, in three tools, and the HTTPS check says "no HTTP request" (from S4-3, S5-12)
Severity: security  
Where: src\Checks\WhyFails.cs lines 205-216 (Clock: HEAD http://www.msftconnecttest.com/connecttest.txt plus w32tm, no Cfg.Portal or Policy check), called from src\Checks\CertChain.cs line 329 (`if (expired || early) clock = WhyFails.Clock();`), src\Checks\DestCheck.cs line 303, src\Checks\Investigate.cs line 133 (when TLS failed). The text that contradicts it: CertChain.cs line 339 ("Two short handshakes, no HTTP request") and src\Ui\FullView.Toolbox.cs line 517 ("two short handshakes (no HTTP request)")  
How to reproduce: Settings › Sign-in page check = Off ("Off: a failed check won't look for a Wi-Fi sign-in page"). Then (a) HTTPS check against a site whose certificate is expired (h4 server 8446, or expired.badssl.com); (b) A website or app won't open on a host whose TLS fails; (c) Check a destination on such a host.  
What happens / what should happen: a "Clock" row appears ("couldn't compare: NameResolutionFailure" in the offline container), which proves the request to www.msftconnecttest.com was attempted (through the system proxy) and that w32tm was started. Program.cs line 10 and the About row say this request is made "only if a check you started fails" and that "Settings can turn that off"; PortalTest honours the switch (Investigate.Whats.cs line 29), these three callers don't. Gate Clock() on Cfg.Portal and Policy.NoOnline (or compare only the local clock), and correct the HTTPS check's description.  
Confidence: confirmed (ran it: h4/t.exe https localhost:8446 shows the Clock row; lead re-read all three call sites)  
Rule: 1  

### 4. HTTP request builder shows and keeps a URL's user:password (from S3-3)
Severity: security  
Where: src\Checks\Reach.cs line 83 (`Draw.Trunc(uri.ToString(), 80)` - Reach.NoUser isn't used here); src\Ui\FullView.Toolbox.cs line 737 (`shown[0]` keeps the URL as typed)  
How to reproduce: HTTP request builder, URL `http://admin:s3cret@127.0.0.1:18080/api` (h3/h2.cs + srv.py).  
What happens / what should happen: the first result row reads `GET http://admin:s3cret@127.0.0.1:18080/api | 200 OK`. That row goes into Copy as text, Save, the last result and Compare, and the inputs key keeps the URL too. Toolkit.Http already wraps every URL in Reach.NoUser; use it here and blank the userinfo in `shown[0]`. The password isn't sent as an Authorization header (mono or .NET Framework), so it is only shown and kept.  
Confidence: confirmed (ran it: mono harness, row text above; lead re-read line 83)  
Rule: 4  

### 5. Every push to main, store or dev-* builds an "official" exe with the Hall of Fame key and publishes it as an artifact (from S8-2)
Severity: security  
Where: .github\workflows\build.yml lines 10-11 (push: main, store, "dev-*") and lines 39-41 (HOF_KEY passed to release.ps1); release.ps1 lines 41-49  
How to reproduce: push any commit to any `dev-*` branch, then Actions › the run › Artifacts › AdeeboMeter.  
What happens / what should happen: the artifact's exe has HOF_KEY compiled in, and a public repository's artifacts can be downloaded by any signed-in GitHub user. Work-in-progress builds, including ones with known rule-10 holes, therefore sign genuine award cards, and the secret reaches release.ps1 from any branch someone with push access creates. Pass HOF_KEY only for `release` events or a tag dispatch; push builds stay "unofficial build".  
Confidence: suspected (read it; lead re-read build.yml line 11 and line 40 and the artifact upload)  
Rule: 10  

### 6. In Serious mode the ARP-spoofing pop-up never appears (from S1-2)
Severity: security  
Where: src\Monitor.cs lines 550-551 (WatchGatewayMac: serious text "Gateway MAC flips ... possible ARP spoofing."), src\Brain\Watch.cs lines 38-39 (Alerts.OnEvent raises the alert only when the text contains "hardware address")  
How to reproduce: Tray › Serious mode (no jokes). Have the gateway's MAC flip back within 10 minutes on the same network (an ARP spoofer, or `arp -s` on a test LAN).  
What happens / what should happen: the timeline gets the level-2 event, but its serious wording has no "hardware address", so the "Your router's hardware address changed on the same network - possible spoofing" alert is never raised. With the companion voice it is. Match on the event kind and a fixed tag, not on wording that Voice.P swaps.  
Confidence: confirmed (ran it: misc.cs feeds both texts to Alerts.OnEvent as kind "net", level 2: companion → ALERT, serious → NO ALERT; lead re-read both lines)  

### 7. speedservers.txt: the upload host is never checked against private addresses, and the download host is checked against a different DNS answer than the one used (from S3-4)
Severity: security  
Where: src\Checks\SpeedTest.cs lines 180-185 (Measure resolves and checks only `srv.Host`, the Down URL's host), line 201 (`Phase(srv.Up, ...)`), lines 77-88 (SafeUrl accepts any dotted name)  
How to reproduce: a speedservers.txt line `server|x|X|Y|https://speed.cloudflare.com/__down?bytes=25000000|http://192.168.1.1.nip.io/apply.cgi` (any public name that resolves to a LAN address; `SafeUrl("http://127.0.0.1.nip.io/upload")` returns True - h3/h1.cs). Speed test with `x`.  
What happens / what should happen: the download host is public, so the check passes, and the upload phase POSTs 4 MB bodies to the router for 8 s (up to 100 MB). HttpWebRequest also re-resolves the Down host, so a name with both a public and a private A record (or a rebinding name) passes with the public answer and connects to the private one. Round 2 #7 asked for the check at connect time. Check the Up host too, and connect to the checked IP (Host header / SNI) or refuse private addresses in `ServicePoint.BindIPEndPointDelegate`.  
Confidence: confirmed (ran SafeUrl; read Measure); delivery to the LAN not run (no internet here)  

### 8. TWAMP-light, MTU over UDP and the ping tools skip the "one device only" guard: up to 1,000 packets to a multicast group, 50 pings a second to a broadcast address (from S3-5, S3-6)
Severity: security  
Where: src\Checks\Protocols.cs lines 458-466 (Twamp: no Lab.NotOne); src\Checks\UdpMtu.cs lines 29-31 (no Lab.NotOne); src\Checks\Icmp.cs lines 97-103 (Resolve4: IPAddress.TryParse first, no NotOne), used by src\Checks\Toolkit.cs line 416 (Sustained ping), line 186 (DscpPing) and line 212 (Loaded)  
How to reproduce: TWAMP-light, reflector `224.0.0.1:862` (or `239.255.255.250`), Packets 1000; MTU over UDP to `224.0.0.251` port 53 (on a home network the SenderGate tick isn't needed). Sustained ping to `192.168.1.255`, `224.0.0.1` or `4294967295`, How many 2000, Every ms 20.  
What happens / what should happen: `Twamp("224.0.0.1:862","10")` sends and reports "no reflected packets", while `Syslog("224.0.0.1")` is refused with "is a multicast group - many devices at once". `Resolve4("4294967295")` and `Resolve4("https://4294967295/")` return 255.255.255.255, and the ping tools ping it 50 times a second. The ping sweep and the ICMP builder never allow this. Call Lab.NotOne on the resolved address in Twamp, UdpMtu and Resolve4's callers (or in Resolve4), as Syslog, Trap, NetFlow and the Lab senders do. Round 1 #229 added the tick but not this guard.  
Confidence: confirmed (ran it: mono harness, Twamp sent to the group, Resolve4 output); suspected that IcmpSendEcho puts broadcast echoes on the wire (Windows-only API; multicast echo is known to work)  
Rule: brief's guard (one target)  

### 9. Hall of Fame: the social handle isn't covered by the claim code, so whoever submits a shared card first attaches their own profile to it (from S1-5)
Severity: security  
Where: .github\scripts\hof_verify.py lines 125-141 (only name, points and date are checked; the handle comes from the form), lines 132-137 (the real owner is then refused as "already"); src\HallOfFame.cs lines 23-25 (the comment says the code stops "someone else's card with a new name")  
How to reproduce: take a card picture someone posted (the code is printed on it). Submit its name, points, date and code with site "X" and your own handle before they do. Then submit the same card with the owner's GitHub handle.  
What happens / what should happen: the first submission is added with `"site": "x", "handle": "thief_handle"`, and the page links the winner's name to the thief's profile. The owner's submission gets "Ada Lovelace is already in the Hall of Fame." and can't correct it. Tie the handle to the card (sign it in the card text), or accept a handle only from the issue author (`ISSUE_AUTHOR`) and let a later matching submission by the same GitHub user change it.  
Confidence: confirmed (ran it: hof_verify.py on a copy with an empty hall-of-fame.json and HOF_KEY=testkey: first run "added" with the thief's handle, second "already")  

### 10. `--deeptest --local` connects to company domain controllers and to the internet (from S8-1)
Severity: security  
Where: src\Ui\FullView.DeepTest.cs line 90 (netNoInput) and lines 91-97 (section 2, "Real run"); the header comment at lines 14-17 promises "never your company's servers", and `--local` prints "local only"  
How to reproduce: on a domain-joined PC run `AdeeboMeter.exe --deeptest --local`. Section 2 runs every tool with no input boxes unless it is in ByHand, LabGate or SenderGate; only `dnsrules teams clock cloud doh hairpin` are skipped when `net` is false.  
What happens / what should happen: these still run for real: `dc` (SysTools.DomainControllers: queries `_ldap._tcp.dc._msdcs.<domain>`, then TCP to 88, 389, 636, 445, 135 and 3268 on up to 3 DCs), `ipv6score` (resolves www.google.com, pings 2606:4700:4700::1111), `hotel` (HTTP GET to www.msftconnecttest.com), `tonight` and `prematch` (game servers), `vpn` and `dupip`. For `--local`, run an allow-list of proven-local tools, not a skip-list, and add `dc` to ByHand.  
Confidence: suspected (read it: the tool list was dumped from r4.exe and each Do body read)  
Rule: 1  

### 11. "Check a PFX" (and the inspection test) build the chain with X509Chain, which downloads missing intermediates, so "nothing sent" is false and a PFX without its intermediate reads as complete (from S4-4)
Severity: security  
Where: src\Checks\CertTools.cs lines 254-258 (Pfx: `new X509Chain`, `ch.Build(leaf)` with only RevocationMode.NoCheck), header comment line 16 ("all on this PC, nothing sent"); src\Checks\CertChain.cs line 354 → Inspection.InspectedBy (same kind of X509Chain)  
How to reproduce: on Windows, check a PFX holding only the leaf and its key, from a public CA, whose leaf has an AIA "CA Issuers" URL, with the intermediate not cached on this PC.  
What happens / what should happen: .NET Framework 4.8 has no DisableCertificateDownloads; CertGetCertificateChain fetches the intermediate over HTTP from the AIA URL (up to 15 s, possibly on the calling thread). The PC contacts the CA unasked, and ChainElements then includes the downloaded intermediate, so the row says "Chain: 3 levels up to a root" although the PFX lacks the intermediate and the server will send an incomplete chain. CertChain.cs's own header explains why it avoids Windows' chain builder for exactly this. Use the CertChain.Walk ladder for PFX files, or CERT_CHAIN_CACHE_ONLY_URL_RETRIEVAL via P/Invoke.  
Confidence: suspected (read it; Windows CryptoAPI behaviour)  
Rule: 1  

### 12. Match / Live / Recipients read any path, including \\server\share UNC paths and files of any size, which CertTools deliberately refuses (from S4-5)
Severity: security  
Where: src\Checks\CertKeys.cs line 213 (Load: `File.Exists(s) ? File.ReadAllBytes(s)`), line 281 (Recipients: `File.Exists(ci) ? File.ReadAllText(ci)`, outside the try)  
How to reproduce: in "Check certificate and key", "Compare file vs live server" or "Recipients", type `\\203.0.113.5\share\a.cer`, or a path to a 1.5 GB file.  
What happens / what should happen: File.Exists opens an SMB session and Windows sends the user's NTLM credentials. CertTools.Input (line 32) and PsSafe (lines 370-371) refuse `\\` paths for exactly this reason. A big file is read whole into memory, and in Recipients a locked or unreadable file throws IOException / UnauthorizedAccessException outside the try. Use CertTools.Input (rooted, no UNC, 1 MB cap) here too.  
Confidence: suspected (read it; Windows behaviour)  
Rule: 1  

### 13. Crash notes keep every folder name after the first space, and the note goes into a GitHub issue URL (from S2-11)
Severity: security  
Where: src\Brain\Watch.cs line 286 (Crash.Clean regex `[^\s"<>|:*?]*\\`)  
How to reproduce: C.exe with `Could not find file 'D:\Clients\Acme Corp\Merger plan Q3\board.adeebo'` and `\\fileserver\HR Share\Salaries 2026\list.csv`.  
What happens / what should happen: results `<folder>\Acme Corp\Merger plan Q3\board.adeebo` and `<folder>\HR Share\Salaries 2026\list.csv`. `C:\Program Files\Contoso VPN\Secret Project\x.txt` isn't touched at all, and on Windows `%USERPROFILE%\OneDrive - Acme Corporation\Clients\...` stays whole (the home folder is replaced first and the rest has no drive letter). The comment promises "only the file's own name is kept", and this text goes into the GitHub issue URL. Match up to the last backslash before the file name, allowing spaces (path ending at a quote, an apostrophe or the end of the line).  
Confidence: confirmed (ran it: C.exe)  

### 14. PKCS7 path: a "-----BEGIN PKCS7-----" block holding a passwordless PFX imports a private key (from S4-19)
Severity: security  
Where: src\Checks\CertTools.cs lines 111, 130 and 152-158 (P7b: `col.Import(b)` without checking the content type; the comment says "Such a bundle holds no private key, so nothing reaches a key store")  
How to reproduce: base64 a PFX with an empty password, wrap it in -----BEGIN PKCS7----- / -----END PKCS7-----, and give it to Inspect a certificate or Chain check.  
What happens / what should happen: on Windows, X509Certificate2Collection.Import with default flags writes the key into the user's key container. The certificate is then dropped because HasPrivateKey is true, and the key is removed only when the object is disposed or finalized; a crash or kill in between leaves it in %APPDATA%\Microsoft\Crypto. Round 1 #17 was fixed for the CERTIFICATE path only. Check `GetCertContentType(b) == Pkcs7` before Import, as lines 114-115 already do for the raw-file case.  
Confidence: suspected (read it; Windows import behaviour)  

### 15. A .pyc is committed, and .gitignore misses the build, portable-mode and release outputs (from S8-3)
Severity: security  
Where: .github\scripts\__pycache__\hof_verify.cpython-313.pyc (tracked since 4fd6e22, present in e80412e); .gitignore; build_exe.cmd line 23; release.ps1 lines 54-74  
How to reproduce: `git check-ignore --no-index` reports NOT ignored for `dist/AdeeboMeter-1.1.5-win64.zip`, `dist/SHA256SUMS`, `dist/RELEASE-NOTES-1.1.5.md`, `AdeeboMeter.exe.sha256`, `data/points.txt`, `AdeeboMeter.portable`, `AdeeboMeter.policy`, `sample_chip_light.png` and `.github/scripts/__pycache__/x.pyc`.  
What happens / what should happen: build_exe.cmd writes AdeeboMeter.exe.sha256 into the repo root and release.ps1 writes multi-MB zips to dist\. Running portable from the checkout (the documented way to test) puts history, air.txt (neighbours' BSSIDs, a location trail), points.txt, keys-ec.txt and recipients\ under data\. One `git add -A` publishes all of it, and history can't be cleaned without a rewrite. The committed .pyc holds no secret (checked with strings) but is a binary copy of an older hof_verify.py. `git rm --cached` the .pyc and add `__pycache__/`, `*.pyc`, `dist/`, `data/`, `*.sha256`, `AdeeboMeter.portable`, `AdeeboMeter.policy` and `sample_*.png`.  
Confidence: confirmed (ran git check-ignore on e80412e; lead re-read: the .pyc is in the commit, no secret)  

## Wrong result

### 16. A fresh install, or "Just run it from here", always opens in Pro mode: the Simple start never shows (from S1-1)
Severity: wrong result  
Where: src\AppCtx.cs line 210 (LoadSettings: `if (!first && Cfg.Mode == "") Cfg.Mode = "pro"`), with src\Setup.cs line 171 (Install: `SetLine(settings, "autostart", ...)` always creates the settings file) and line 454 (Press "here": the same for a copy run where it is)  
How to reproduce: on a PC that has never run AdeeboMeter, double-click the exe and press Install (or "Just run it from here"). The installed copy starts with an AdeeboMeter.settings that holds only `autostart=1`.  
What happens / what should happen: LoadSettings sees an existing file (`first` = false) with no `mode=` line, takes it for an upgrade from 1.1.4 and sets Mode = "pro". Every new user who goes through the installer gets Pro (every tab), not "shown as Simple with the choice on the home page" (Program.cs line 320), and the `if (first) SaveSettings()` step is skipped too. Treat a file with no `mode=` and no other 1.1.4 keys as a first start, or have Setup write `mode=` (empty) or a "fresh" marker.  
Confidence: confirmed (ran it: mode.cs calls AppCtx.LoadSettings on a file holding only `autostart=1` → Cfg.Mode = 'pro'; with no file it stays ''; lead re-read line 210)  

### 17. "Time lost waiting" was removed by accident: the row is inside a // comment (from S6-4)
Severity: wrong result  
Where: src\Checks\OverTime.cs line 99 (PeriodRows)  
How to reproduce: read line 99: `double waits = p.BadMinutes + p.OutageUnrecorded;   // rough minutes, plus ... as rough if (waits > 0) rows.Add(R(3, "Time lost waiting", ...));`. The UTF-16 literal "Time lost waiting" does not occur anywhere in r4.dll, while "Laggiest game" from the line above does.  
What happens / what should happen: the round-3 #4 fix (no double counting) put the `if` on the comment line, so the Month and Six-month reviews and the saved HTML review no longer have the "Time lost waiting" row, and `OutageUnrecorded` and the `badAt` set are computed for nothing. Move the `if` to its own line.  
Confidence: confirmed (string absent from the compiled assembly; lead re-read line 99)  

### 18. "Can I close it?" tells people svchost.exe is OK to close (from S2-5)
Severity: wrong result  
Where: knowledge\processes.txt `== svchost` (close: 1), shown by src\Knowledge\Explain.cs line 38 (CloseText[1]); also `== msiexec` (close: 1)  
How to reproduce: What's running › any svchost.exe › hover card.  
What happens / what should happen: the card says "Leave it be. If it misbehaves, closing it is OK - Windows (or the app) brings it back.", while its own "without" line says "Kill the wrong copy and the network, the sound or the whole PC goes with it". Ending the svchost that hosts DcomLaunch or RpcSs shuts Windows down within a minute. It should be close: 2 ("Don't close it"). msiexec at level 1 also promises "Windows brings it back", but killing it mid-install can leave a half-installed app; it needs its own text ("lets the install fail") or level 2 while an install runs.  
Confidence: confirmed (read it; lead re-read the svchost entry)  

### 19. `--report` writes into the user's real history and data files and runs the ISP trace, while its header says nothing is written (from S1-3)
Severity: wrong result  
Where: src\Report.cs lines 17 and 28-31 (Monitor.Start inside the report); src\Monitor.cs line 91 (Events.Add "AdeeboMeter started") → src\Model.cs line 117 (Timeline.Add → History.Event); src\Monitor.cs line 151 (Brain.Second → Flush: History.Minute, Usage.Add, Baseline.Minute, Devices.Check at :x0, AirLog at :05, UpdateBreak at :20); src\Monitor.cs line 266 (LearnIsp)  
How to reproduce: copy r4.exe into an empty folder with AdeeboMeter.portable and run `AdeeboMeter.exe --report out.json --seconds 5`.  
What happens / what should happen: data\history\2026-11-13-events.csv appears with "AdeeboMeter started" and "On LAN (eth0), gateway ..." events. A run that crosses a minute boundary also writes a minute row, a usage and baseline update, and possibly devices.txt, the air log or the Windows-update check. An RMM job running it hourly fills Time machine and the outage log with fake "started" and "On LAN" events; with the GUI copy running, both processes append to the same history files (duplicated rows, or rows lost to sharing errors). Each run also does the 6-hop trace and PTR lookup toward 1.1.1.1 that the JSON never uses. The header promises "Nothing is written except the file you name and the usual log". Run the monitor in a report mode with no Timeline-to-History link, no Brain.Second side effects and no LearnIsp.  
Confidence: confirmed (ran it under mono: the events file was created; the minute, usage and devices paths were read)  

### 20. Export settings → Import settings silently drops the voice (mood), certificate revocation, the buddy, its hat, the outage best score and the overlay position (from S1-4, S7-4)
Severity: wrong result  
Where: src\Program.cs line 370 (Cfg.Known has no `mood`, `crlrev`, `buddy`, `hat`, `outagebest` or `ovat`) against lines 359-366 (Cfg.Lines writes them); src\Ui\FullView.Settings.cs line 544 (ImportSettings keeps Known keys only)  
How to reproduce: Settings › Voice = Brutal (or Sarcastic engineer), Certificate revocation = On, Buddy = Off. Export settings…, then Import that file on another PC, or on the same PC after changing those back.  
What happens / what should happen: "Loaded N settings", but those six values are unchanged. The exported file holds `mood=`, `crlrev=`, `buddy=`, `hat=`, `outagebest=` and `ovat=`, and Import throws every key not in Known away. The normal save/load round trip keeps all 46 keys; only Export → Import loses these. Add them to Known (hat and outagebest are already validated by Cfg.Read), or derive Known from Lines().  
Confidence: confirmed (ran it: S1 misc.cs listed the saved keys missing from Known; S7 rt.cs set non-defaults, Lines(), Read back and diffed with Known: exactly `mood crlrev buddy outagebest hat ovat`)  

### 21. A match session records almost nothing for the server it is about, then reports "loss 99-100%", and the stream overlay plots hours-old samples as the last minute (from S6-1)
Severity: wrong result  
Where: src\Gamer\Gamer.cs line 380 (Plan: `p.Servers.Take(MaxServers)` at 1 s), lines 339-340 (StartSession: `Server = Best(g)`), line 455 (Evidence: a sample counts only if younger than 3 s whenever `Cfg.GameFast`), line 742 (Report: `GameStat.Loss(gv)` counts NaN as lost); src\Ui\FullView.Gamer.cs line 255 (card rows `Take(MaxServers)`), line 219 (session line); src\Ui\StreamOverlay.cs line 68 (`Recent(60/step)` with step = 1)  
How to reproduce: Balanced preset (MaxServers 8). Watch Valorant (or any built-in "world" game) from a PC whose best region is India, Oceania or Middle East (entries 9-11) and play a match. `P.exe` with playing = valorant prints the plan: NA East … Southeast Asia at 1 s, India / Oceania / Middle East at 300 s. Any Valve game is worse: 30+ relays in Valve's JSON order, and only the first 8 (16 on Gamer) are probed each second. Without any region problem the same happens when "Start session" is pressed before the game runs, when its exe isn't detected, or with "start by itself" (GameAuto) off: Playing is null, nothing is probed at 1 s, yet Evidence still demands a 3-second-fresh sample (about 3 s of every 30 s background probe).  
What happens / what should happen:  
- The session's server is `Best(g)` (Oceania), probed once every 5 minutes, so `Evidence()` writes `Game = NaN` in about 299 of 300 rows.
- "Copy match report" and the THIS MATCH line both say `loss 99%` / `100%` with avg "-", and no spike can ever be detected for that server (it needs 20 samples in 15 min).
- The streaming lag graph plots the last 60 survey samples (5 hours) as the last 60 seconds.
- The game card shows only the first 8 rows, so the ★ best region isn't on it, while its header reads "Best for you over 7 days: Oceania".
- Should: always include the session's server and the best region in the 1-second set, choose the 8 or 16 by score rather than list order, judge "fresh" by the pace that server is really probed at, and report "not measured" rather than counting unmeasured seconds as loss.  

Confidence: confirmed for the plan (ran P.exe); the Evidence, Report and card consequences are read from the exact lines above  

### 22. Learn games still pay for answers they have already shown: escape room, quick-fire, and any timed game left before its end screen (from S6-5)
Severity: wrong result  
Where: src\Ui\FullView.Games.cs line 94 (time's up: "It was: … Fix: …", no MarkTried), line 128 (`roomMiss >= 3 ? ""`, no MarkTried), lines 233 and 268 (heist and outage mark tried only when the end screen is painted), line 333 (dungeon only on death), line 389 (quick-fire uses `EarnPts` with no tried logic)  
How to reproduce:  
1. Escape room: let the clock run out (the fault and fix are printed), press Next room until the same fault returns (with one unearned fault left it always does), click the shown answers: +4. The same works after escaping with 3+ wrong guesses, which pays nothing and marks nothing.
2. Heist / Survive the outage: each round shows the answer and reason. Play 9 of 10 (or 7 of 8), then click another Learn item or restart, so the end screen never paints. Play again with the answers known: +3.
3. Dungeon: leave with one heart left, come back, Enter again: no tried mark.
4. Port / Address quick-fire: every wrong pick shows "No - 443"; play again until 8 of 10.  

What happens / what should happen: round-3 #9 added `EarnFirst` and `MarkTried` for thief and detective only. Each key still awards once, but it is earned after the answer was shown. Call `MarkTried(key)` at the moment an answer is revealed (first wrong pick, time-out, first round of a heist or outage), not at an end-of-game paint.  
Confidence: confirmed (read; every award and reveal path listed)  
Rule: 10  

### 23. A name that cleans to nothing makes an award card that can never be claimed (from S8-8)
Severity: wrong result  
Where: src\Ui\FullView.Points.cs lines 100-102 (MakeAwardCard: `name = CleanName(...)`, `Code(key, name, ...)`); src\HallOfFame.cs line 100 (`if (nm == "") nm = "A network detective"`); .github\scripts\hof_verify.py line 113  
How to reproduce: at 500+ points, Make my award card, and type a name made only of emoji, symbols or punctuation ("🙂🙂", "@@@").  
What happens / what should happen: the picture shows "A network detective", but the claim code is the HMAC of an empty name and the copied text says "Name: " (empty). Typing the name shown on the card gives a mismatch, and an empty name is refused as "missing". Refuse an empty cleaned name in AskName (keep the dialog open with a note), so the card's name is always the one the code was made from.  
Confidence: confirmed for the verifier side (ran hof_verify with an empty name: "missing"); app side read  
Rule: 10 (the award path: a user can't claim a card they earned)  

### 24. With the internet check off (Settings or policy), every Teams call ends with "lost 100% of internet checks" and a pop-up (from S2-3)
Severity: wrong result  
Where: src\Brain\Watch.cs lines 62 and 72 (Calls: `rows.Count(r => double.IsNaN(r.Net))`), lines 82 and 90 (Alerts.Raise "call"); src\Monitor.cs line 263 (NetPing cleared when the check is off) and line 223 (`netSec = 0` under Policy.NoInternetCheck)  
How to reproduce: Settings › internet check Off (or `internet_check = off` in the policy), then a Teams call longer than 20 s on a perfect network. K.exe feeds 120 seconds of `net = NaN` (what `NetPing.Last` returns for an empty ring).  
What happens / what should happen: "Your 2-minute call lost 100% of internet checks - the trouble was past your router (provider or VPN)." It is level 1, so the "After your call: ..." notification fires too, although no check ran. Count a sample only when a new internet probe arrived during the call (as Brain does with NetAt), and say nothing about loss when there were none. Also, a 20-29 s call is called a "0-minute call" (Math.Round).  
Confidence: confirmed (ran it: K.exe output "Your 0-minute call lost 100% of internet checks", level 1)  

### 25. Time machine paints the internet red ("no answer") for every minute the internet check didn't run (from S8-4)
Severity: wrong result  
Where: src\Ui\FullView.Replay.cs line 198 (`netLv = double.IsNaN(net) ? 2`) and lines 226-231 (plain words); data from src\Brain\Brain.cs lines 51 and 99 (netAvg is NaN when no probe landed in that minute)  
How to reproduce: Preset Penny (internet every 5 minutes), or the internet check Off (Settings or `internet_check = off`). Open Time machine and drag across any hour.  
What happens / what should happen: on Penny 4 of every 5 minutes have no internet sample, so Internet and ISP show red "no answer"; with the check off, every minute is red. The widget travels back red too (AppCtx.Rewind, line 207), and when the recorded worst level is not 0 the sentence reads "the internet was slow or dropping (no answer)". An unsampled minute is "not measured" (level 3, grey), and the loss column should say so. (A different cause from round-1 #50, a router that ignores ping, which is still present - see the table at the end.)  
Confidence: suspected (read the write path in Brain.Flush and the read path in Replay)  

### 26. "Nothing is wrong ... and the way in all work" when the way-in test couldn't run or is switched off: in What's wrong? (green) and in the `--report` JSON (verdict "unknown") (from S1-7, S5-8)
Severity: wrong result  
Where: src\Checks\Investigate.Whats.cs lines 157-159 (level 3 / NotEst only), line 166 (`weak` only looks at level 1), line 174 (fixed wording); src\Report.cs line 95 (Json writes r.Finding unchanged after Verdict, lines 79-84, has downgraded 0 to 3)  
How to reproduce: (a) S5 inv.cs s3 with `http_proxy=http://127.0.0.1:1` (the web test fails with ConnectFailure, as on a network that needs a proxy), or s1 with Settings › Sign-in page check Off. (b) `--report out.json` on a PC where step 6 "The way in" couldn't run (a proxy-only network, or an offline lab).  
What happens / what should happen: What's wrong? shows "6. The way in: Windows' connectivity test couldn't run (ConnectFailure) - web traffic may need a proxy here" and a not-established note, but the finding is level 0: "Nothing is wrong right now - your PC, the link, your router, the internet, names and the way in all work." The JSON gets `"verdict": "unknown", "exit_code": 3, "finding": "Nothing is wrong right now ... the way in all work."`, so a script or person reading the finding sees a healthy PC that the exit code calls unknown. When portalErr != "" or the check is off, leave "the way in" out of that sentence and don't give level 0 (a failed web test with everything else working should be at least amber); when Verdict downgrades, replace or extend the finding ("one step couldn't run: ...").  
Confidence: confirmed (ran it: Whats with a crafted Monitor; rep/out.json under mono, where step 6 couldn't resolve)  

### 27. HTTPS check shows a green "A good padlock" when the handshake fails after the certificate (mTLS, client-certificate servers) (from S4-2)
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 305-313 (hsErr is set but cert stays non-null) and lines 333-338 (the verdict never looks at hsErr once cert != null)  
How to reproduce: a TLS server that requires a client certificate (h4/srv.py 8444 "tls12 mtls"), then HTTPS check on localhost:8444. The same happens with any server that stalls or aborts after sending its certificate.  
What happens / what should happen: the verdict row is level 0, "A good padlock: , the right name, a chain Windows trusts." (protocol blank), while the TLS version row under it is red: "TLS 1.2 - the handshake stopped at the certificate". If the handshake didn't finish, say so ("the server wants a client certificate / the handshake failed: <hsErr>") instead of claiming a good padlock.  
Confidence: confirmed (ran it: h4/t.exe https localhost:8444)  

### 28. ISP report: a game server that ignores ping is reported as 100% loss and blamed on a provider hand-off, and the message to the ISP says so (from S5-2)
Severity: wrong result  
Where: src\Checks\IspReport.cs lines 137-145 (`LastOrDefault(h => h.Sent > 0)`) and line 288 (Message); src\Checks\PathTools.cs line 36 (Trace keeps one silent hop at the tail)  
How to reproduce: Report for my ISP on a game whose servers drop ICMP (common), or a host that ignores ping, with the second route (or 1.1.1.1) answering. Harness isp.cs M2: route 1 = 4 clean hops plus the silent tail hop Trace keeps, route 2 = a clean path to a reached destination.  
What happens / what should happen: "SAME TIME, TWO ROUTES" uses the last hop that was *sent* a probe, which is the silent tail hop, so it shows "loss 100%" in red. When the two routes leave the provider at different rDNS domains, "What it means" says "Only the route that leaves at X loses packets - the problem is on that hand-off, not your line. Your provider can steer that traffic another way.", and the pasted message says "At the same time Game EU-Central had 0.0% loss against 100%." The path was clean. Use the last hop that *answered* (Sent > Lost), as `end` does at line 97, and say "doesn't answer ping" instead of a loss figure.  
Confidence: confirmed (ran Message() and the row logic with crafted routes; 4 answering hops plus silence leaves 5 hops, the last silent)  

### 29. ISP report blames the home network for loss inside the provider when the router isn't the first answering hop (from S5-5)
Severity: wrong result  
Where: src\Checks\IspReport.cs lines 91-94 (firstIsp), lines 103-104 (finding), line 111 ("Home router" row = first answering hop)  
How to reproduce: isp.cs (a verbatim copy of lines 92-106 plus the real StartOfLoss). I1: the router doesn't answer TTL-exceeded (hop 1 silent) and loss starts at the provider's CGNAT hop 100.64.0.1. I4: the router answers from another of its own addresses (192.168.0.1 while the gateway is 192.168.1.1), loss from 100.64.0.1. I2: a full-tunnel VPN, loss starts at the VPN server 10.8.0.1.  
What happens / what should happen: when gwIdx is -1, firstIsp becomes the first *public* hop, so every 10.x / 100.64.x provider hop before it counts as home: "Loss starts at hop 2 - before the provider (the home network). Fix the home side first." I2 says "(Wi-Fi or the router)" for loss at the VPN server. The round-1 #58 fix works only when the gateway's address appears in the trace. Fall back to "the first hop after the first answering hop", and recognise the VPN case (VpnUp: name the tunnel).  
Confidence: confirmed (ran the copied logic with the real IspReport.Private / StartOfLoss)  

### 30. ISP report: the cable-modem page reads and the hop name lookups aren't in "What leaves your PC" (from S5-13)
Severity: wrong result  
Where: src\Checks\IspReport.cs lines 79-83 (PTR lookups for up to 60 hop addresses through the user's DNS), lines 244-262 (up to 4 HTTP GETs to 192.168.100.1); src\Ui\FullView.Settings.cs line 319 (LeaveIsp text)  
How to reproduce: read About / Settings › What leaves your PC › Report for my ISP.  
What happens / what should happen: the entry lists only "an MTR to the destinations you chose ... your public IP only if you type yes". Every run also sends reverse-DNS queries for every hop to the configured resolver, and HTTP requests to 192.168.100.1; where that isn't a modem they go to the default route or a VPN. Add both to the list.  
Confidence: confirmed (read it; both sends are unconditional in Run)  
Rule: 1  

### 31. The provider trace is repeated up to four times per network and again on every switch back, while About says "once per network" (from S1-10)
Severity: wrong result  
Where: src\Monitor.cs lines 508-533 (LearnIsp: `if (Isp == "" && ispTries++ < 3) ispKey = ""`; ispKey keeps only the last network); src\Ui\FullView.Settings.cs line 391 ("Once per network: a short trace (6 hops) toward 1.1.1.1 and one name lookup")  
How to reproduce: a network whose first public hop has no reverse-DNS name, or where ICMP time-exceeded is filtered (common on company networks); or dock and undock between two networks.  
What happens / what should happen: each failed learn re-runs the 6-hop trace and the PTR lookup at the next 10 s interface check, four traces in all, and going A → B → A runs it again for A. About under-states the background traffic. Remember the result per network key, failures too, for the session (or say "up to 4 times").  
Confidence: suspected (read it)  
Rule: 1  

### 32. Privacy texts miss the new revocation lookups and the files written automatically, and the Hall of Fame keeps more than it says (from S8-11)
Severity: wrong result  
Where: docs\privacy.html "What it sends" (no certificate revocation CRL/OCSP, which e80412e added; the in-app About lists it at src\Ui\FullView.Settings.cs line 396); docs\privacy.html "What stays on your PC" (nothing about reports\Roast-*.png, therapy or letter files, src\Checks\Fun.cs lines 30-34 and 233, written on every run; also missing games.txt, game-compare.txt, cert-compare.txt, aaa-runs.txt); docs\hall-of-fame.html line 203 ("nothing else about you is kept") and .github\ISSUE_TEMPLATE\hall-of-fame.yml line 11, while hof_verify.py line 141 stores `by` (GitHub login) and `ref` (issue URL) in the public JSON  
How to reproduce: compare the pages with the code above.  
What happens / what should happen: the published privacy policy (also the Store listing's) doesn't list a network contact the app makes when the setting is on, or files written without being asked; the Hall of Fame publishes the GitHub login and issue link after saying nothing else is kept. Add these to the pages, or drop `by` and `ref`.  
Confidence: confirmed (grep, and the JSON output of the hof run)  
Rule: 1  

### 33. PLAN-1.1.5.md names the forbidden company (from S8-12)
Severity: wrong result  
Where: PLAN-1.1.5.md line 8 (the rule "never mention or reference ..." spells the name out)  
How to reproduce: `git grep -n -i` for the name.  
What happens / what should happen: the only hit in the repository is the rule itself, in a file published with the public source and the source zip (release.ps1 line 69, `git archive HEAD`). Reword it to "the forbidden vendor (see the private brief)", or keep the plan out of the repo.  
Confidence: confirmed (git grep; lead re-read line 8)  
Rule: 8  

### 34. Policy values other than off/no/false/0 are silently ignored, so IT believes a switch is off (from S1-8)
Severity: wrong result  
Where: src\Policy.cs line 59 (Parse: `off = v == "off" || v == "no" || v == "false" || v == "0"`), with no log for an unrecognised value or key  
How to reproduce: AdeeboMeter.policy with `internet_check = disabled`, `internet_check = "off"`, `internet_check: off` or `speed_test = disable`.  
What happens / what should happen: none of these switch anything off and nothing is logged, while Policy.File is still set, so the `--report` JSON shows `"policy": "...AdeeboMeter.policy"` as if it applied. history_days already logs a bad value. Accept disable/disabled/n and quoted values, and log every line that sets nothing.  
Confidence: confirmed (ran it: pol.cs - only "off" and "OFF" set NoInternetCheck)  

### 35. Your connection on Russian, Japanese, Chinese, Korean, Arabic and Turkish Windows says "nobody - clean air" and invents a "faster band"; rate, standard and security rows show "-" in several more languages (from S2-1, S2-15)
Severity: wrong result  
Where: src\Link\LinkInfo.cs lines 474-490 (KeyWords / NormKey: no per-AP labels for ru, ja, zh, ko, ar; no Turkish "Sinyal"; no Polish or Dutch rate labels and no Italian "Tipo frequenza radio", although the comment claims Dutch and Polish), lines 520-530 (ParseNetworks), 602-607 (Sharing your channel), 620-621 (2.4 GHz note); WlanApi.LeastCrowded line 301; src\Link\WifiAdvisor.cs lines 89-99; src\Monitor.cs line 452 (ParseWifi parses "144,4" with InvariantCulture → -1)  
How to reproduce: S2's N.exe on the netsh samples (`mono N.exe ru2 ja2 tr2`, and `it pl fr ru`): the PC is on 2.4 GHz channel 6, three neighbours (6, 4 and 8, all 70-80%) overlap it, and the network has no 5 GHz AP.  
What happens / what should happen:  
- English: "Sharing your channel: 3 (3 strong)", no band note, least crowded channel 11.
- ru, ja, zh, ko, ar: Сигнал/Канал/Диапазон, シグナル/チャネル/バンド, 信号/信道/波段 and so on aren't in KeyWords, so every AP has Signal -1, Channel -1 and Band "". The page says "Sharing your channel: nobody - clean air" and always adds "You're on 2.4 GHz, but your network also offers a faster band" (`!a.Band.StartsWith("2.4")` is true for Band "", including your own AP). LeastCrowded answers "stay on 6, load 0".
- tr: "Sinyal" isn't mapped: "3 (0 strong)", no crowding warning, every neighbour weighed 0.
- pl, it, fr, ru (and ja, zh, ko, ar, tr for security): Receive and Transmit rate "-", Standard "-" (it), Security "- · -". Monitor's netsh fallback reads "144,4" as -1, so "link rate dropped" events and the RxRate history column go blank.
- Monitor.ChanKeys already knows "канал", "チャネル" and "信道"; LinkInfo.KeyWords doesn't. Share one label table, fall back to the shape of the values (NN% signal, small-integer channel, "x GHz" band, the number on a line whose label contains "(Mb"), and treat Band "" as unknown, never as "not 2.4".  

Confidence: confirmed on the samples (ran N.exe on 13 languages); the exact translated labels are from knowledge, not captured on real PCs  

### 36. A decimal-comma band ("2,4 GHz") switches off all 2.4 GHz logic for German, French, Spanish, Italian, Portuguese and Polish, including the air log (from S2-2)
Severity: wrong result  
Where: src\Link\LinkInfo.cs line 581 (band from netsh's Band line), lines 538, 589, 602, 620 (`StartsWith("2.4")`); WlanApi.LeastCrowded line 289; src\Link\WifiAdvisor.cs lines 87 and 102; src\Link\AirLog.cs line 60 (Overlaps) and line 117 (quietest-channel candidates) (lead re-read)  
How to reproduce: `mono N.exe de fr es it pt pl` with samples whose Band line reads "2,4 GHz" (e.g. "Band : 2,4 GHz").  
What happens / what should happen: band = "2,4 GHz". Overlaps falls back to `a == b`, so the neighbours on 4 and 8 aren't counted ("Sharing your channel: 1 (1 strong)" instead of 3, no crowding note); LeastCrowded returns null; WifiAdvisor skips the 2.4 GHz channel advice and "your network is also on 5 GHz"; the 2.4 GHz amber badge and band note never appear. AirLog.Overlaps uses block maths instead of the ±4-channel rule, and the week's quietest-channel pick offers no 1/6/11 candidates. The KeyWords mapping added in 96abea2 made this worse: before it, these languages had no Band key and the language-neutral WlanApi band ("2.4 GHz") was used. Russian "2,4 ГГц" has the same shape. Normalise the band by its leading digit (2 / 5 / 6), or prefer WlanApi's band whenever available.  
Confidence: confirmed for the parsing (ran it); that localized netsh really prints "2,4" is suspected (check on a German PC); the AirLog lines were read  

### 37. Non-English Windows: the location block is never noticed ("Nothing to tweak - your Wi-Fi is set up well"), and the checkup's "This Wi-Fi's security" never flags an open or WEP network (from S2-7, S4-13)
Severity: wrong result  
Where: src\Link\LinkInfo.cs line 501 (Blocked: English "location" + "permission"), line 569; src\Link\WifiAdvisor.cs lines 77-81 and 125; src\Checks\SecChecks.cs lines 40-43 (WifiSec: `Authentication\s*:` on `netsh wlan show interfaces`, and English Open/WEP words)  
How to reproduce: German or French Windows 11 24H2. (a) "Let desktop apps access your location" off, then open Your connection. (b) Join an open hotspot and run Security checkup (netsh prints "Authentification : Ouvrir" / "Authentifizierung : Offen").  
What happens / what should happen: (a) netsh's translated refusal doesn't match, so sig and ch are -1 and the verdict is "Windows didn't say the signal or channel...", with no location hint, and the advisor, with no APs, ends "Nothing to tweak - your Wi-Fi is set up well." The Monitor already knows this in any language (WlanApi.Current rc 5 → `m.WifiBlocked`); set `s.LocationBlocked` from it. (b) The regex doesn't match, so the row reads "not on Wi-Fi, or Windows doesn't show it" (info) instead of the red open-network warning. Read the auth algorithm from WlanApi (WLAN_CONNECTION_ATTRIBUTES.wlanSecurityAttributes), which Wi-Fi watch already calls. Round 1 #68 fixed this for saved networks only.  
Confidence: suspected (read it; no localized netsh output here)  

### 38. "Your Wi-Fi dropped N times in the last day" counts your own disconnects on non-English Windows (from S2-8)
Severity: wrong result  
Where: src\Link\LinkInfo.cs line 676 (`IndexOf("by the user")`), lines 107-109 and 121-130 (EvLog.WifiDrops / Plain)  
How to reproduce: German Windows: disconnect Wi-Fi from the tray three times, then open Your connection.  
What happens / what should happen: event 8003's Reason is a translated sentence, so "by the user" never matches and all three count as real drops: level 1, "Your Wi-Fi dropped 3 times in the last day", and Plain() adds no explanation. Use the event's numeric ReasonCode, which is the same in every language.  
Confidence: suspected (read it; EventLogReader is stubbed on mono)  

### 39. MTR / ISP report: a destination that answered the trace and then stopped answering is called "No real loss" (from S5-3)
Severity: wrong result  
Where: src\Checks\PathTools.cs lines 136-138 (LossStart needs a later hop), lines 145-151 (MtrVerdict uses the one-argument Answers, so silentEnd is true); src\Checks\IspReport.cs lines 97-106  
How to reproduce: mtr.txt P6: hops 1-4 at 0%, the destination has Reached=true from the trace and then loses 120/120 probes (a server that goes down during the run).  
What happens / what should happen: "No real loss: any loss at a middle hop doesn't carry on ... The destination doesn't answer pings at all, so this is judged by the last router that does (hop 4)", and the ISP report says "The destination doesn't answer pings ... No real loss on the path". It did answer during the trace, so this is a destination outage. P25 and P26 have the same wording problem ("Real loss starts at hop 3 ... 100%. The destination doesn't answer pings at all"). When Reached && Lost == Sent, say "the destination stopped answering during the run".  
Confidence: confirmed (ran LossStart / MtrVerdict, patterns P6, P25, P26)  

### 40. Steady low loss on every hop (under 3%) is blamed on the destination rate-limiting pings (from S5-4)
Severity: wrong result  
Where: src\Checks\PathTools.cs line 136 (`if (l < 3) continue`), line 152; src\Checks\IspReport.cs line 105  
How to reproduce: mtr.txt P21 (3 of 120 lost at every hop from the home router on) and P22 (a 1-minute run, 1-2 of 60 lost from hop 2 on).  
What happens / what should happen: LossStart is -1, so "Loss at the destination only (3%) - the server itself drops or rate-limits pings", and the ISP report says "Only the destination drops some probes ... the path itself is clean." 2-3% is what makes calls robotic, and with 60-120 probes it can't pass the 3% threshold. When every answering hop from some point on loses about 1% or more, report possible loss from that hop, or say "too little loss to locate"; don't call it destination rate-limiting.  
Confidence: confirmed (ran P21, P22)  

### 41. MTR: two routers at the tail that ignore the probes make an earlier rate-limited hop "real loss" (round-2 #21, two-router variant) (from S5-6)
Severity: wrong result  
Where: src\Checks\PathTools.cs lines 124-130 (`dark >= 2` makes every traced 100% hop "answering"), lines 136-139  
How to reproduce: mtr.txt P15: hop 2 at 5% (rate limiting), hops 3 and 4 answered the trace and then 0 of 120, destination silent. P16: the same without hop 2's loss.  
What happens / what should happen: P15: "Real loss starts at hop 2 (203.0.113.1) and carries on to the end - 5% lost from there ... judged by the last router that does (hop 2)". If the tail really went dark the loss starts at hop 3 at 100%; if they are quiet routers there is no loss; neither puts it at hop 2 with 5%. P16 says "Real loss starts at hop 3 ... 100%" with nothing past it answering. Count dark hops as an outage only when something after them (or the reached destination) also went dark, and then start the loss at the first dark hop.  
Confidence: confirmed (ran P15, P16)  

### 42. ISP message says "my router answering normally" when the loss starts at the router, and the copied message keeps the private address (from S5-7)
Severity: wrong result  
Where: src\Checks\IspReport.cs line 286 (Message), lines 199-200 (`copy:` + msg, not redacted)  
How to reproduce: isp.cs M1: on a cable, the home router (hop 1) losing 10% through to the end.  
What happens / what should happen: "This was measured on a cable, with my router answering normally. The loss starts at hop 1 (192.168.1.1) ...", which contradicts itself, and the LAN address goes into a text meant for a support chat. The saved HTML redacts it; the click-to-copy text doesn't. Claim the router was normal only when its loss was under 1%, and run the message through GameSession.Redact before copying.  
Confidence: confirmed (ran Message())  

### 43. CS2 / Dota 2: servers you add, and "My match" from Find my match server, vanish as soon as they are saved (from S6-2)
Severity: wrong result  
Where: src\Gamer\Gamer.cs line 89 (`foreach (var g in all.Where(x => x.Valve)) g.Servers = Valve.Cached() ?? g.Servers;`, after ApplyFile); src\Ui\FullView.Gamer.cs lines 163-168 (FindMatch: SaveGame + Reload, then "Added as \"My match\" and watched from now on"); src\Ui\GameEditor.cs lines 112-113  
How to reproduce: G.exe valve writes a small valve-servers.txt (IAD, FRA) and a games.txt with `server|cs2|My match|found|203.0.113.9|-1|exact` and `server|cs2|LAN box|home|192.168.1.50|27015|exact`, then reads Games.All: the cs2 servers come back as IAD, FRA only.  
What happens / what should happen: whenever a Valve list is cached (always, after the first click on a Valve game), edits saved through Edit servers are replaced on the next Reload(), and so is the match server that Find my match server says is "watched from now on". Merge the user's exact servers with Valve's list, or don't offer Edit / "Added" for Valve games.  
Confidence: confirmed (G.exe)  

### 44. The match report never shows the router / Wi-Fi / traffic / CPU context for a spike (from S6-3)
Severity: wrong result  
Where: src\Gamer\Gamer.cs line 748 (`var at = Around(z.T, 0).FirstOrDefault();`) and line 685 (`Math.Abs((r.T - t).TotalSeconds) <= secs`)  
How to reproduce: G.exe report: a session of 300 one-second rows, each stamped 137 ms after the second, plus one spike 100 s ago.  
What happens / what should happen: the spike line is `12:48:39  30 -> 120 ms  · your provider` with no "(router … ms, Wi-Fi …%, traffic …, CPU …%)". A 0 s window matches only a row with the identical timestamp, and the spike is stamped on a probe task while rows are stamped on the loop thread, so they never match. Use the nearest row within about 1 s.  
Confidence: confirmed (G.exe output)  

### 45. Chain check: a cross-signed copy of a trusted root is preferred over the trusted root itself, so a valid chain reads red as "the middle certificate is missing" (from S4-6)
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 77-80 (Walk: candidates ordered by "signed it", then "in date", files you gave first; trusted roots never preferred)  
How to reproduce: Chain check with h4/c/chain-cross.pem (leaf, intermediate, and "Test Root CA" cross-signed by "Old Root"), with the self-signed Test Root CA trusted. In the wild: servers sending Sectigo's USERTrust cross-signed by AAA Certificate Services, and other cross-signs.  
What happens / what should happen: red "Your certificate is fine, but the middle certificate is missing - add it to the files above.", missing link "Old Root". Windows and browsers stop at the trusted self-signed root and accept the chain. Prefer a trusted root with the same name and key (sort trusted roots first), or end the walk as soon as the current certificate's key matches a trusted root.  
Confidence: confirmed (ran it: h4/t.exe chain chain-cross.pem; signatures through the crypt32 stub)  

### 46. Chain check: a root that isn't in the local stores is reported as a missing *middle* certificate, including public roots Windows hasn't downloaded yet (from S4-7)
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 65 and 81 (Walk only looks in Root / AuthRoot / CA as they sit on disk; any unknown issuer sets `missing`), lines 106 and 125 (the text says "middle certificate")  
How to reproduce: Chain check with h4/c/srv-chain.pem (leaf + intermediate) on a profile whose stores don't hold the root. On Windows this happens with any public chain whose root hasn't been fetched into AuthRoot yet (Windows fills AuthRoot on demand, as Inspection.cs line 74 notes), and with a private CA whose root wasn't given.  
What happens / what should happen: red "Your certificate is fine, but the middle certificate is missing - add it to the files above", "missing: the certificate that signed the one below (Test Root CA)"; over HTTPS, "the server forgot to send the middle certificate", although servers aren't supposed to send roots. When the missing issuer is the issuer of a CA certificate, call it the root, and look it up in Windows' own list (Inspection.InWindowsList already reads the AuthRoot CTL) before calling it missing.  
Confidence: confirmed (ran it with empty stores); the AuthRoot on-demand part is suspected  

### 47. HTTPS check calls any site signed by a private root "Your company is inspecting HTTPS", including intranet servers and home-lab CAs (from S4-8)
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 354-355 (any non-public root → "Your company is inspecting HTTPS (...)" / "(a company proxy)"); src\Checks\Inspection.cs lines 74-86 (a root in Root that isn't on the public lists counts as inspection)  
How to reproduce: on Windows, with the company's internal root (or a NAS / home-lab root) trusted, run HTTPS check on intranet.corp.example or nas.local, signed directly by that CA.  
What happens / what should happen: an amber row says HTTPS is inspected by a company proxy; nothing is intercepting. The inference only holds for a well-known public site (as SecChecks.TlsIntercept uses it, with www.microsoft.com). For an arbitrary site say "signed by a private root this PC trusts (company or your own CA)", and call it inspection only for sites known to use a public CA, or when the site looks different off the network (Certificate chain compare).  
Confidence: suspected (read it; needs a Windows store)  

### 48. The chain ladder doesn't check signature hash, key size, key usage or path length, so an RSA-1024 leaf passes as "Every link holds" (from S4-18)
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 42-46 (IsCa checks BasicConstraints only) and lines 61-97 (Walk); header comment lines 20-21 ("is the signer allowed to sign")  
How to reproduce: Chain check with h4/c/leaf-1024.pem plus int.pem.  
What happens / what should happen: every row is green. Current clients reject RSA keys under 2048 bits, SHA-1-signed server certificates, a missing serverAuth EKU and pathLen violations. CertTools.Describe already flags RSA < 2048 and SHA-1 per certificate; the ladder, which gives the verdict, should too.  
Confidence: confirmed (ran it for RSA-1024); SHA-1, EKU and pathLen suspected  

### 49. Chain check stops after 9 certificates and then shows a green "In plain words" that says the root isn't trusted (from S4-16)
Severity: wrong result  
Where: src\Checks\CertChain.cs line 67 (`depth < 8`), line 117 (level 0 when there's no Bad and no `missing`), line 130  
How to reproduce: Chain check with h4/c/chain-long.pem (leaf + 10 intermediates, root trusted).  
What happens / what should happen: the ladder ends at "Mid 2" with no root and no "missing" row, and "In plain words" is level 0 (green): "Every link holds, but the top is a root this PC doesn't trust". When the walk stops at the depth limit, say "chain longer than 9 - stopped" and give it at least amber.  
Confidence: confirmed (ran it)  

### 50. Chain check treats the first certificate in a .p7b as "your certificate", but bundles are often stored root-first (from S4-17)
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 139-147 (`certs[0]` is the leaf, `certs.Skip(1)` the helpers); src\Checks\CertTools.cs line 111 (P7b order as decoded)  
How to reproduce: Chain check on a .p7b whose SignedData lists root, intermediate, leaf (python cryptography's pkcs7.serialize_certificates writes that order: h4/c/chain-py.p7b; many CA "bundle" downloads do the same).  
What happens / what should happen: the ladder starts at the root: "your certificate: Test Root CA". For a bundle, take as leaf the certificate that issues none of the others.  
Confidence: suspected (read it; order confirmed with openssl pkcs7 -print_certs; mono can't import .p7b, so not run end to end)  

### 51. Windows version check gives "Pro Education" the Enterprise / Education support dates: 24H2 Pro Education shows "fixes until 2027-10-12", but its support ended on 2026-10-13 (from S4-10)
Severity: wrong result  
Where: src\Checks\SecChecks.cs line 151 (`Regex.IsMatch(edition, "Enterprise|Education")` matches EditionID "ProfessionalEducation")  
How to reproduce: VersionVerdict("26100", "ProfessionalEducation", "24H2", 2026-11-13).  
What happens / what should happen: green "Windows 11 24H2 (Enterprise / Education) · fixes until 2027-10-12". Microsoft's lifecycle puts Pro Education with Home / Pro, so it should be red "security fixes ENDED 2026-10-13", as for "Professional". Match `^(Enterprise|Education|IoTEnterprise)` on the EditionID, not a substring.  
Confidence: confirmed (ran it: h4/t.exe ver 26100 ProfessionalEducation …)  

### 52. Startup check flags Windows' own programs as unsigned and counts every per-user Run entry twice (from S4-9)
Severity: wrong result  
Where: src\Checks\SecChecks.cs lines 266-267 (`Tuple.Create(CU, RegistryView.Registry32)`) and line 276 (`X509Certificate.CreateFromSignedFile`)  
How to reproduce: Security checkup › Programs that start with Windows on stock 64-bit Windows 10/11 with one per-user startup app (e.g. Discord).  
What happens / what should happen: CreateFromSignedFile reads only an embedded Authenticode signature, so catalog-signed Windows binaries such as SecurityHealthSystray.exe (HKLM Run "SecurityHealth") throw and are listed as "without a publisher's signature" (amber) on almost every PC. HKCU\Software isn't redirected by WOW64, so the Registry32 view of HKCU opens the same Run key: every per-user entry is counted twice ("N start automatically" inflated), and an unsigned one is named twice. Drop the HKCU Registry32 view; check signatures with WinVerifyTrust (covers catalogs), or at least skip files under %windir%.  
Confidence: suspected (read it; Windows registry and signing behaviour)  

### 53. Roast my network: the picture includes the provider's name although the tool says "no provider name" (from S4-12)
Severity: wrong result  
Where: src\Checks\Fun.cs lines 201-202 (burns use `prov` = m.Isp), line 230 (burns drawn on the PNG), line 238 ("No Wi-Fi name, no addresses, no provider name - just the grade and the burns.")  
How to reproduce: Roast with a grade of A/A+ or F (Burns(week, "A+", "Acme Broadband Ltd")).  
What happens / what should happen: "Honestly? Hard to roast. Acme Broadband Ltd is doing okay. For now." and "Acme Broadband Ltd should be paying you." go into the picture meant for posting, and the provider name usually narrows down where the user lives. Use "your provider" in the picture, or drop the promise.  
Confidence: confirmed (ran it: h4/t.exe burns)  

### 54. A CSR for a wildcard name gets the subject "CN=.example.com" (from S4-11)
Severity: wrong result  
Where: src\Checks\CertKeys.cs lines 163 and 182-185 (Dn → Cn → Safe, which drops '*')  
How to reproduce: Toolbox › CSR with names `*.example.com` and an empty subject.  
What happens / what should happen: subject "CN=.example.com" while the SAN keeps *.example.com. CAs that require the CN to be one of the SANs reject it; others issue a certificate with an invalid CN. The key's friendly name ("Local HTTPS - .example.com") loses the '*' too. Keep '*' in a leading "*." label.  
Confidence: confirmed (ran it: h4/t.exe dn "*.example.com")  

### 55. IPv4 typed with leading zeros, hex or fewer than four parts is read as a different address in the ping tools, the subnet calculator and URL-form destinations (from S3-7)
Severity: wrong result  
Where: src\Checks\Icmp.cs line 100 (Resolve4); src\Checks\Toolkit.cs line 928 (Subnet only counts the dots); src\Checks\DestCheck.cs lines 64-67 (URL branch: Uri has already canonicalised the host before the line 90/91 checks)  
How to reproduce: h3/h1.cs and h2.cs: `Resolve4("010.0.0.1")` → 8.0.0.1, `Resolve4("192.168.001.010")` → 192.168.1.8, `Resolve4("0x7f.1")` → 127.0.0.1. `Subnet("192.168.001.010/24")` shows the host as .8, `Subnet("010.1.2.3/24")` gives network 8.1.2.0/24. `DestCheck.Parse("http://010.1.1.1/")` → host 8.1.1.1 with no message, `http://0x7f.1/` → 127.0.0.1.  
What happens / what should happen: Destination check refuses `010.1.1.1` with "Windows would read 0-prefixed numbers as octal", but the same input works silently in DSCP ping, Sustained ping, Latency under load, Return path and the subnet calculator, and in Destination check written as a URL. Zero-padded addresses are common in printed configs. Use the strict `^[0-9]{1,3}(\.[0-9]{1,3}){3}$` + no-leading-zero rule in Resolve4 and Subnet, and check what the user typed (not Uri.Host) in the URL branch. Mapped forms go the other way: `Resolve4("::ffff:10.0.0.1")` returns null, "couldn't find (IPv4)".  
Confidence: confirmed (ran it: mono harness; mono and .NET Framework agree on these inet_addr rules)  

### 56. Revocation checks the wrong certificate when "Name to ask for (SNI)" was used (from S3-8)
Severity: wrong result  
Where: src\Checks\Toolkit.cs line 784 (`revoke:` + host, the connection address), line 731 (`ssl.AuthenticateAsClient(host)`)  
How to reproduce: HTTP and TLS inspector, URL `https://203.0.113.5`, Name to ask for `www.example.com`, then press Revocation.  
What happens / what should happen: the revocation handshake sends no SNI (an IP literal), so a shared or CDN server returns its default certificate; the rows above described www.example.com's certificate, and the "not revoked" verdict is about a different one. Carry the SNI in the action (`revoke:host:port:sni`) and use it in AuthenticateAsClient.  
Confidence: suspected (read it)  

### 57. Check a destination on port 853 (DNS over TLS) or 636 sends an HTTP HEAD and turns a healthy server yellow (from S3-11)
Severity: wrong result  
Where: src\Checks\DestCheck.cs lines 322-339 (the HTTP step runs for every `tls` port: 443, 8443, 993, 995, 465, 636, 853)  
How to reproduce: Check a destination `1.1.1.1:853` (Parse gives port 853, tls=True).  
What happens / what should happen: after a good TLS handshake it writes `HEAD / HTTP/1.1`. A DoT server reads "HE" as an 18,501-byte length and waits, so the read times out after 6 s: step HTTP "no answer" (level 1), verdict level 1. IMAP, POP and SMTP greet first and show "not HTTP". Send the HEAD only on web ports (80, 443, 8080, 8443) and mark the others "not a web port".  
Confidence: suspected (read it; no DoT server here)  

### 58. NTP: a reply with zero (or garbage) receive and transmit timestamps says "Your clock is behind by 291404279795 ms" in red (from S3-9)
Severity: wrong result  
Where: src\Checks\Protocols.cs lines 166-171 (Ntp), line 178 (ReadNtp: 0 → era 1 = 2036)  
How to reproduce: h3/srv.py NTP on 127.0.0.1:18123: mode 4, stratum 2, origin = our transmit time, everything else zero. NTP tool `127.0.0.1:18123`.  
What happens / what should happen: "answers · stratum 2 (gets time from 0.0.0.0)", then a red "behind by 291404279795 ms" (9 years); the delay check passes because t2 = t3. RFC 4330 section 5 says to discard replies whose transmit timestamp is 0 (stratum 0, or refid 0 at stratum ≥ 2, is suspect). Reject a zero transmit or receive timestamp, and show large offsets in s / min / days rather than raw ms.  
Confidence: confirmed (ran it: mono harness)  

### 59. SNMP test trap: sysUpTime is badly encoded for many uptimes (e.g. any uptime between 23.3 and 46.6 hours) (from S3-10)
Severity: wrong result  
Where: src\Checks\Lab.cs lines 340-341 (TrapPacket: `up.SkipWhile(x == 0)` with no leading 0x00 when the top bit is set)  
How to reproduce: `Lab.TrapPacket("public", 200, 1, "x")` → `...43-01-C8...`; `TrapPacket(..., 9000000, ...)` → `43-03-89-54-40` (9,000,000 ticks = 25 h; ticks = TickCount/10).  
What happens / what should happen: BER encodes TimeTicks like an INTEGER, so a first byte ≥ 0x80 reads as negative: net-snmp shows 0xFFFFFFC8 (about 497 days) and strict decoders (pyasn1) reject the trap. It happens for uptimes of 1.28-2.56 s, 5.5-10.9 min and 23.3-46.6 h. Add a 0x00 byte when `(first & 0x80) != 0`, as `Int()` already does.  
Confidence: confirmed (ran it: bytes above)  

### 60. Unicode digits make Many ports, the ICMP builder and the ICMP builder's "Takes ..." estimate throw FormatException (from S3-12, plus one location found by the lead)
Severity: wrong result  
Where: src\Checks\MoreTools.cs lines 165-168 (`\d+` then `int.Parse(..., Invariant)`); src\Checks\Lab.cs lines 124-129 (`(\d+)` then `int.Parse`); src\Ui\FullView.Toolbox.cs line 539 (Duration, case "icmpb": `(?:every|gap)\s*=?\s*(\d{1,5})` then int.Parse), called from the Toolbox canvas paint at line 1019  
How to reproduce: Many ports, Service `٨٠` (or `８０`); ICMP builder, Options `size ١٠٠` or `gap ١٠٠`.  
What happens / what should happen: Many ports and the ICMP builder end with "Couldn't finish: FormatException" (logged, no crash). In the ICMP builder, typing `gap ١٠٠` makes Duration throw inside PaintTb on every repaint of the tool page (before Run is pressed): DB.OnPaint has no catch, so each paint is abandoned below the "Takes ..." line and "UI error" is logged through Application.ThreadException (Program.cs line 135) for as long as the text is in the box. Same class as round 3 #7 (DestCheck / Calc were fixed with `[0-9]`); these three were missed. Use `[0-9]` or RegexOptions.ECMAScript.  
Confidence: confirmed (ran it: S3 h1.cs "THREW FormatException" for MoreTools and Lab; lead ran FullView.Duration("icmpb", …"gap ١٠٠") via reflection on r4.dll: FormatException. The paint consequence is read)  

### 61. LDAP check: a 16 KB reply with a long-form length near its end throws IndexOutOfRange (from S3-13)
Severity: wrong result  
Where: src\Checks\MoreTools.cs line 335 (ParseRootDse: `b[j + 2 + q]` and `b[o + q]` for up to 127 length bytes, checked only against `j + 2 < n`)  
How to reproduce: `ParseRootDse(b, 16384)` where the last 30 bytes are `04 14 "defaultNamingContext" 31 FF` (h3/h2.cs).  
What happens / what should happen: IndexOutOfRangeException, shown as "Couldn't finish" (a hostile or odd LDAP server). Reject length forms over 3 bytes and check `k + nb <= end` before reading them, as TryTlv in Protocols does.  
Confidence: confirmed (ran it)  

### 62. Internationalised and non-default-port hosts go out wrong: HTTP timing sends Host without the port, and HTTP timing and the HTTPS check send an IDN as "b?cher" (from S3-14, S4-14)
Severity: wrong result  
Where: src\Checks\Protocols.cs line 91 (`"Host: " + u.Host`), line 89 (SNI `u.Host`); src\Checks\CertChain.cs line 271 (`Encoding.ASCII.GetBytes(sni)` in the hand-built ClientHello) and line 309 (AuthenticateAsClient with the Unicode name)  
How to reproduce: HTTP timing `http://127.0.0.1:18080/x` against h3/srv.py (the server received `Host: 127.0.0.1`); also `https://bücher.de` and `https://[2001:db8::1]:8443/`. `CertChain.ClientHello("bücher.example", 0x0303)`: the server_name bytes are `62 3F 63 68 65 72 2E ...` ("b?cher.example").  
What happens / what should happen: RFC 9112 requires `host:port` for non-default ports, so virtual hosts and redirect logic on :8080 / :8443 answer something else (wrong vhost, 400 or 421). IDN hosts go out as `b?cher.de` (Encoding.ASCII) in HTTP timing's Host and in HTTPS check's SNI, so the server answers with its default certificate or an alert and the ladder describes the wrong certificate; IPv6 goes into SNI with brackets. Use `u.IdnHost` / `u.Authority`, IdnMapping.GetAscii before building the hello and before AuthenticateAsClient, and an SNI without brackets. A plain-http timing run also lists a "Lock (TLS)" row (about 1 ms).  
Confidence: confirmed (ran it: Host header captured; ClientHello bytes); the IPv6 part read  

### 63. ICMP builder silently ignores options it doesn't know (`-l 1472 -f`), and its chart repeats the last RTT for lost packets (from S3-15)
Severity: wrong result  
Where: src\Checks\Lab.cs line 124 (the regex skips unknown words), line 162 (`run.Add(times[b].Count > 0 ? times[b].Last() : 0)`)  
How to reproduce: ICMP builder, Options `-l 1472 -f` (Windows ping syntax).  
What happens / what should happen: it runs with a "32-byte payload, TTL 128" and no DF, and nothing says the options were dropped. iperf lists "Not understood and ignored"; do the same, or accept -l / -f / -i / -v. On the chart a lost or TTL-expired packet repeats the previous reply time (or 0 before the first reply), so loss looks like a flat line; add NaN as Sustained ping does.  
Confidence: confirmed (ran it: row text); chart read  

### 64. Many ports: "8000-8010" tests only 8000 and 8010 (from S3-16)
Severity: wrong result  
Where: src\Checks\MoreTools.cs line 165 (`Regex.Matches(w, @"\d+")`)  
How to reproduce: Many ports, host 127.0.0.1, Service `8000-8010`.  
What happens / what should happen: "0 of 2 ports open". Expand ranges (still 20 at most), or say "ranges aren't supported".  
Confidence: confirmed (ran it)  

### 65. STUN to an IPv6 server always says "answered, but not with an address" (from S3-17)
Severity: wrong result  
Where: src\Checks\Reach.cs line 128 (ParseStun accepts family 1 only)  
How to reproduce: STUN to `[2001:db8::x]:3478`, or a name with only AAAA records (Lab.Addr then picks IPv6). ParseStun on a Binding Success with an IPv6 XOR-MAPPED-ADDRESS (family 2, 20 bytes) returns null (h3/h6.cs).  
What happens / what should happen: RFC 5389 section 15.2: family 0x02 XORs the 16-byte address with cookie + transaction id. Decode it.  
Confidence: confirmed (ran the parser); the live IPv6 path wasn't run (no IPv6 in the container)  

### 66. Captive-portal check reads a chunked "Microsoft Connect Test" as a sign-in page and overrides the verdict in red (from S3-18)
Severity: wrong result  
Where: src\Checks\DestCheck.cs lines 157-163 (ClassifyPortal: the body must start with "Microsoft Connect Test")  
How to reproduce: `ClassifyPortal("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n16\r\nMicrosoft Connect Test\r\n0\r\n\r\n")` → 1 (portal).  
What happens / what should happen: behind a proxy that re-chunks responses (uncommon), a failed check becomes "Found the real culprit: a Wi-Fi sign-in page" at level 2, hiding the real cause. Look for the marker inside the body, or de-chunk first.  
Confidence: confirmed (ran it)  

### 67. Internationalised names typed bare are refused by Check a destination and every ping tool (from S3-19)
Severity: wrong result  
Where: src\Checks\DestCheck.cs line 56 (hostRx is ASCII only) and line 92; src\Checks\Icmp.cs line 101 (Resolve4 uses DestCheck.Parse)  
How to reproduce: Check a destination `bücher.de`; Sustained ping `bücher.de`.  
What happens / what should happen: "That is not a valid host, IP or URL." The URL form `http://bücher.de/` works (IdnHost → xn--bcher-kva.de) and dig handles IDN. Convert with IdnMapping before the regex.  
Confidence: confirmed (ran DestCheck.Parse)  

### 68. Host:port splitters disagree: Lab.Split refuses `[2001:db8::1]` without a port; Protocols.Split takes `[::1]x80` as port 80 (from S3-20)
Severity: wrong result  
Where: src\Checks\Lab.cs lines 63-66 (the bracket form is handled only with `]:`, then `[` fails the regex); src\Checks\Protocols.cs line 30 (`Substring(e + 2)` skips whatever is at e+1)  
How to reproduce: STUN, SNMP trap, NetFlow, Every IP behind a name, RDP or LDAP with `[2001:db8::1]` → Lab.Split returns False ("type a STUN server..."), while `[2001:db8::1]:3478` works. NTP or SSH with `[::1]x22`.  
What happens / what should happen: the documented bracketed form fails without a port, and a typo is taken as a port. Use one splitter: strip `[...]`, then require `]` to be followed by nothing or `:digits`.  
Confidence: confirmed (Lab.Split ran); Protocols.Split read  

### 69. Toolbox URL rewrite works per tool, not per box: Certificate chain compare loses a URL's port and checks the wrong server, and Compare file vs live server refuses a URL (from S7-3, S7-8)
Severity: wrong result  
Where: src\Ui\FullView.Toolbox.cs line 275 (UrlTools: certlive is in it, certcmp isn't), lines 716-723 (StartTool URL rewrite; certcmp's label "Site - e.g. outlook.office.com" has no ":port", so withPort = false); src\Checks\CertKeys.cs line 249 (Live: Lab.Split)  
How to reproduce: (a) Certificate chain compare, Site = `https://intranet.example.com:8443/login`, Run. (b) Compare file vs live server, Certificate = any .crt, Server = `https://vpn.example.com:8443/`.  
What happens / what should happen: (a) StartTool rewrites the box to `intranet.example.com` before EngPack.CertCompare sees it (url.cs printed `https://intranet.example.com:8443/login -> intranet.example.com`), so the compare handshakes with port 443 (another service, or refused) and stores that snapshot under the run name. CertCompare parses URLs itself and keeps the port (EngPack.cs line 44), and its own error text suggests "https://intranet.example.com". (b) "type the server - e.g. vpn.example.com or 10.0.0.5:8443": certlive is in UrlTools to protect its file box, which also switches off the rewrite for its server box (`Lab.Split("https://intranet.example.com:8443/login")` → false). Make UrlTools per box (skip box 0 only), add certcmp or pass withPort when the URL has an explicit port, or parse URLs in Live as Https does (CertChain.cs line 293).  
Confidence: confirmed (ran it: s7/url.cs over Tools2() with StartTool's skip rules, and Lab.Split on the URL)  

### 70. Minimising the window (or Win+D) stops a running 60-minute MTR, ISP report or any other running tool (from S7-6)
Severity: wrong result  
Where: src\Ui\FullView.cs line 360 (Resize → Minimized → BeginInvoke(Close)), line 371 (FormClosed: `tbRun0.Stop = true`), against lines 322-323 (Esc is deliberately ignored while tbBusy)  
How to reproduce: Toolbox › MTR, Minutes = 60, Run. Press Win+D, or the minimise button.  
What happens / what should happen: the view closes and the MTR stops; its result, hop table and Compare are lost (the partial CSV is written but nothing points to it). The ISP report, iperf3, sustained ping and the rest go the same way. Esc is guarded so "a stray Esc doesn't throw it away", but minimising is far more common. While tbBusy (or sniffing), hide to the corner without closing, ask, or say first that minimising stops it.  
Confidence: suspected (read)  

### 71. The open window doesn't take up imported settings (mode tabs, Toolbox view, Points button, language) (from S7-9)
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs lines 547-559 (ImportSettings: Cfg.Read, Policy.Apply, Save, Nudge, BuildPinBar only)  
How to reproduce: export a settings file with mode=pro, tbview=list, points=0 and lang=fr, then Import it in a Simple-mode window.  
What happens / what should happen: the Settings rows now show Pro, Off and Français, but the tab row stays Simple (ApplyMode not called), the Adeebo button stays (SyncPointsBtn not called), the Toolbox stays "by question", and the language isn't loaded (L.Use not called). Call the same setters each Settings row uses, or say "restart to apply".  
Confidence: suspected (read)  

### 72. live_capture = off: Start live capture does nothing and says nothing (from S7-10)
Severity: wrong result  
Where: src\Ui\FullView.cs lines 241-247 (liveBtn.Enabled = LiveCapture.Available only), lines 542-548 (ToggleLive), line 243 (label); src\Capture\LiveCapture.cs line 81 (Start returns false, only logs)  
How to reproduce: policy `live_capture = off`, Wireshark installed. Monitor › Live capture › Start live capture.  
What happens / what should happen: nothing happens; the page keeps saying "Press Start live capture" and "Off until you press Start". Sniff and the Journey both show "turned off by your IT team" (FullView.cs line 948, Journey line 187). Show the same message and disable the button.  
Confidence: suspected (read)  

### 73. Toolbox: clicks that open another tool are silently dropped while a tool runs, and some write into the running tool's boxes (from S7-11)
Severity: wrong result  
Where: src\Ui\FullView.Toolbox.cs line 664 (SelectTool: `if (tbBusy) return;`), line 257 (ShowSection overwrites run.Status); src\Ui\FullView.Overview.cs line 49 ("Add my own server..." → OpenTool("speedtest") then `tbBoxes[0].Box.Text = "list"`); src\Ui\FullView.cs line 204 (Check a destination chips)  
How to reproduce: start a 1-minute MTR, then (a) Overview › server chip › Add my own server…; (b) Check a destination › "MTU per hop"; (c) click another Toolbox section.  
What happens / what should happen: (a) the MTR stays open and its Destination box now reads "list", no longer matching the run on screen. (b) the click is ignored without a word. (c) the run's status ("Probing every hop once a second...") is replaced by "still running - press Stop first to change section" for the rest of the run, because most tools set Status once. Say a switch was refused without clobbering the run's status, and check tbBusy in the Overview before writing into the boxes.  
Confidence: suspected (read)  

### 74. Settings search finds nothing for "Wi-Fi", "sign-in" or "français" (from S7-5)
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs lines 167-173 (SetMatch splits the setting text on `[^a-z0-9.]+` but keeps the query words whole)  
How to reproduce: Settings › search: `Wi-Fi` (the app's own spelling), `sign-in`, `français`.  
What happens / what should happen: "Nothing matches". "Wi-Fi watch" splits into "wi" and "fi" while the query stays "wi-fi"; "Sign-in page check" fails the same way; `français` is in the Language row's Keys, but "ç" splits it into "fran" and "ais". Over the real 38 rows: wi-fi 0, Wi-Fi 0, sign-in 0, français 0; wifi 2, ping 7, bgp 1. Split the query with the same regex and keep letters beyond a-z (`\p{L}`).  
Confidence: confirmed (ran it: s7/sm.cs and sm2.cs via reflection on SetItems() / SetMatch)  

### 75. Toolbox search: "wifi" misses most Wi-Fi tools, "SPF" opens the routing-protocol listener, "dmarc" suggests "mac" (from S7-12)
Severity: wrong result  
Where: src\Ui\FullView.Toolbox.cs lines 553-568 (Synonyms), 787-814 (Matches: substring `text.Contains(w)`), 816-824 (DidYouMean)  
How to reproduce: Toolbox search (s7/m.cs on the real Tools2() and Explain terms): `wifi`, `wi-fi`, `SPF`, `dmarc`, `port 443`.  
What happens / what should happen: `wifi` gives 3 tools (roams, dhcp, arp), while the box's own hint says "wifi" and `wi-fi` gives 19 ("Is this Wi-Fi OK to use?", "Wi-Fi rush hour", "Side by side: Wi-Fi vs cable" are missing). `SPF` gives only rlisten ("spf" is inside "OSPF"). `dmarc` gives nothing and "Did you mean mac". `port 443` gives only udpmtu, not Port test. Normalise the hyphen, match explanation text on word starts, and add spf / dkim / dmarc → smtppath / dig (missing since round 1).  
Confidence: confirmed (ran it: m.cs, en and fr)  

### 76. Settings › Keep history for highlights the wrong length under an IT policy (from S7-13)
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs line 145 (Cur = FindLastIndex(choices <= HistoryDays)); src\Policy.cs line 69 (history_days accepts any 1-180)  
How to reproduce: policy `history_days = 5`.  
What happens / what should happen: Cfg.HistoryDays is 5 but the row highlights "3 days"; clicking "7 days" stores min(7, 5) = 5 and still shows "3 days". Mark the row as policy-held (PolicyLocks), or show the real value.  
Confidence: suspected (read)  

### 77. Health page: the "DONE hh:mm" stamp shows the current time, not when the check finished (from S7-7)
Severity: wrong result  
Where: src\Ui\FullView.Tools.cs line 59 (`"  ·  DONE " + DateTime.Now.ToString("HH:mm")`)  
How to reproduce: Investigate › Health › e.g. Background jobs; wait a few minutes, then scroll or hover so the page repaints.  
What happens / what should happen: DONE shows whatever minute the repaint happens. Use toolAt, set when the check finishes and already used for Copy and the evidence pills.  
Confidence: suspected (read; deterministic)  

### 78. Shutdown or logoff loses up to 5 minutes of data usage every time (from S1-9)
Severity: wrong result  
Where: src\AppCtx.cs lines 290-295 (OnSessionEnding stops captures only); src\Brain\Brain.cs Usage.Add (saves at most every 5 min); Usage.Save() runs only in Quit (AppCtx.cs line 318)  
How to reproduce: use the PC normally, then shut down Windows without Exit from the tray; compare today's usage with what moved in the last minutes.  
What happens / what should happen: Windows ends the process after SessionEnding, so Quit and Usage.Save never run and the last ≤5 minutes of the day's MB are lost; with a daily shutdown the monthly cap warning reads low. Call Usage.Save() (and flush the current history minute) in OnSessionEnding.  
Confidence: suspected (read)  

### 79. Packet journey on DNS over TCP (53) or DNS over TLS (853): "Some questions were asked twice" on every clean lookup (from S2-4)
Severity: wrong result  
Where: src\Capture\Journey.cs line 144 (Kind = "dns" for any 53/853), lines 343-349 (Dns: every outbound packet is a "question", every inbound one an "answer")  
How to reproduce: J.exe with one clean TCP exchange (handshake, query, answer, FIN, ACKs); the 853 run adds a TLS hello.  
What happens / what should happen: port 853: "In short: some questions were asked twice" and "4 question(s) went unanswered and were repeated - each costs about a second"; port 53: "3 question(s)". SYN, ACK, FIN and TLS handshake packets count as questions, and "answer time" pairs ACKs with replies. Apply the question/answer rule to UDP only (or to TCP segments with payload, ignoring the TLS handshake), and use the TCP rules for DoT.  
Confidence: confirmed (ran it: J.exe)  

### 80. Live capture call quality: RTP renumbering still gives 0% or 50-60% loss (round-3 #2 only partly fixed) (from S2-6)
Severity: wrong result  
Where: src\Capture\LiveCapture.cs lines 264-273 (AddRtp)  
How to reproduce: R.exe, same SSRC throughout, 3.75% real loss after the event: (a) 45000..45999 then a restart at 20000; (b) 45000..45999 then a jump to 50000; (c) 60000..60999 then a restart at 2000; (d) reordering across a real wrap (65534 after 0).  
What happens / what should happen: (a) 0.00%: the backward jump is under 30,000, so it isn't a new run, every later packet falls below MaxExt and the loss is hidden. (b) 51.88%: a forward jump of 4,000 within 20 ms counts as lost packets. (c) 63.46%: taken for a wrap, 6,536 phantom packets expected. (d) 0.10% phantom loss, about 3 packets per wrap (negligible). RFC 3550 A.1 uses MAX_DROPOUT 3000 and MAX_MISORDER 100: a jump over 3,000 either way followed by two in-sequence packets starts a new run; a jump far larger than elapsed time × packet rate can't be loss; (c) should also need the old number within ~100 of 65535.  
Confidence: confirmed (ran it: R.exe)  

### 81. Two Wi-Fi adapters: while the main one is between states, the service path reports the other adapter's "disconnected" as a drop (from S2-10)
Severity: wrong result  
Where: src\Link\LinkInfo.cs lines 198-203 (Current: no connected adapter → adapter 0's state); src\Monitor.cs line 570  
How to reproduce: a laptop with an idle USB Wi-Fi dongle enumerated first; the built-in adapter goes through "associating" / "authenticating" while it reconnects.  
What happens / what should happen: Current returns the dongle's "disconnected", so the timeline gets "Wi-Fi dropped" (level 2, counted by the 3-drops alert), then "Wi-Fi reconnected", though the adapter that matters never disconnected. With both connected, the first connected one wins, not necessarily the routing adapter (m.Primary). Pick the interface whose GUID is the primary NIC's Id; if not found, the non-disconnected one.  
Confidence: suspected (read)  

### 82. Startup list: items switched off in Task Manager still look active (Startup folder and 32-bit Run list) (from S2-12)
Severity: wrong result  
Where: src\WindowsDeep\Inside.cs lines 177-187 (only `StartupApproved\Run` is checked, by value name), line 193 (ExeOf on unquoted paths)  
How to reproduce: disable a Startup-folder shortcut, or a 32-bit app from HKLM\...\WOW6432Node\...\Run, in Task Manager › Startup apps.  
What happens / what should happen: Windows records those under `StartupApproved\StartupFolder` (value name with ".lnk") and `StartupApproved\Run32`, so they are never marked "switched off in Task Manager". An unquoted Run value with spaces (`C:\Program Files\Realtek\Audio\HDA\RtkNGUI64.exe -s`) gives Exe "Program", so "why did it start?" misses it. Check Run32 and StartupFolder (file name with extension), and resolve unquoted paths with spaces up to ".exe".  
Confidence: suspected (read)  

### 83. A program running from C:\Windows\Temp is described as "ships with Windows" (from S2-13)
Severity: wrong result  
Where: src\Knowledge\Explain.cs lines 76-81 (Where: only the user's %TEMP% counts as "temp"; C:\Windows\Temp falls under "windows")  
How to reproduce: run any exe from C:\Windows\Temp (a common malware and installer location for SYSTEM tasks), then open What's running.  
What happens / what should happen: "It lives in the Windows folder, so it ships with Windows (or a driver)." Watch.Conns still flags it by its own "\Temp\" test. Check `%WINDIR%\Temp` (and other Temp folders) before "windows".  
Confidence: confirmed (read it; same logic as the harnessed paths)  

### 84. What's wrong? calls a router that just ignores ping "weak" and makes that the amber finding (from S5-9)
Severity: wrong result  
Where: src\Checks\Investigate.Whats.cs line 94 (gwSilent gives level 1 on "3. Your router"), line 122, lines 166-169  
How to reproduce: a gateway that drops ICMP and has nothing on TCP 53/80/443 (ASA/FTD, an HSRP address, cloud gateways), with the internet and DNS working. Run What's wrong?.  
What happens / what should happen: step 3 is level 1, so `weak` picks it: "Nothing is broken, but your router (x) is weak: no answer to ping or TCP. That's where to look if things feel slow or unreliable.", and the next line says "Your router is quiet ... that's a setting on the router, not a fault." Once step 4 succeeds, a silent router should be level 0 or 3, not 1.  
Confidence: suspected (read it; the weak-selection path was confirmed by running s5/s6/s9, but the container's transparent proxy makes every TCP connect succeed, so a fully silent gateway couldn't be produced)  

### 85. The hosts-file check only looks at the first 40 entries, so "A website won't open" misses a pin further down (from S5-10)
Severity: wrong result  
Where: src\Checks\WhyFails.cs lines 80-84 (Hosts: `lines.Take(40)` limits both display and matching); src\Checks\Investigate.cs line 131  
How to reproduce: a hosts file with 100 ad-block lines, then `10.9.9.9 intranet.example.com` (S5 hosts.cs).  
What happens / what should happen: rows=40, pins=0, so Site() never says "Your hosts file pins ...". Ad-blocking hosts files have thousands of lines, so this is the common case. Match every line; cap only the display.  
Confidence: confirmed (ran WhyFails.Hosts)  

### 86. SNMP Interfaces: a device restart between two runs isn't noticed when the new uptime is higher than the old one, so deltas across the reboot show as growth (from S5-11)
Severity: wrong result  
Where: src\Checks\SnmpTools.cs line 130 (Changes: reboot only when now.Up < old.Up)  
How to reproduce: agent.py mode `ifreboot`: run 1 sees sysUpTime 1 s and ifInErrors 50; the device restarts; run 2 sees sysUpTime 1 h and ifInErrors 80, 0 s later.  
What happens / what should happen: "3 ports changed · 3 with growing errors", "+30" on each port, no "restarted" note. Uptime moving forward more than the wall time elapsed can only be a restart: also treat it as a reboot when (now.Up - old.Up) / 100 is clearly less than the seconds between the runs.  
Confidence: confirmed (ran Interfaces twice against the agent)  

### 87. SNMP walk: an agent that answers with the same OID again ends the walk silently, shown as complete (from S5-15)
Severity: wrong result  
Where: src\Checks\Snmp.cs lines 381 and 387 (`continue` on an equal OID, then `if (!moved) break` with error "")  
How to reproduce: agent.py mode `same` (GETBULK from x.1 answers x.1).  
What happens / what should happen: "1 values · 1 table", no Partial row, so it reads as the whole table. Loops and backwards OIDs get "the device's answers stopped moving forward"; a reply that doesn't move should set the same error.  
Confidence: confirmed (ran it)  

### 88. SNMP: any stray datagram from the device ends the request with an error, although the code says stray packets are skipped (from S5-16)
Severity: wrong result  
Where: src\Checks\Snmp.cs line 137 (accept rejects only "a reply to a different request"), lines 110-111 (comment), lines 148 and 154 (v3 has no accept filter)  
How to reproduce: agent.py modes `garbagefirst` (6 junk bytes, then the real reply) and `wrongcommfirst` (a reply with another community, then the real one).  
What happens / what should happen: "not an SNMP reply" and "a reply for a different community"; the real answer right after is thrown away and the view fails, so a forged packet with the device's address and port can fail any walk. Skip every datagram that doesn't parse as our reply, and add the same filter to v3 (message ID).  
Confidence: confirmed (ran it; wrong request-id then right is skipped correctly, and another source port is dropped by the connected socket)  

### 89. An SNMP community with any non-ASCII character never works (from S5-17)
Severity: wrong result  
Where: src\Checks\Snmp.cs line 136 (sent as UTF-8), line 167 (compared with Encoding.ASCII.GetString)  
How to reproduce: community `café` against agent.py (it echoes the community bytes).  
What happens / what should happen: "a reply for a different community" on every request. Compare the raw bytes.  
Confidence: confirmed (ran it)  

### 90. SNMP Traffic on 32-bit counters: no "Busiest %", a phantom multi-gigabit rate when ifSpeed is missing, and a reboot near the counter top read as traffic (from S5-18)
Severity: wrong result  
Where: src\Checks\SnmpTools.cs line 188 (Sp from ifHighSpeed only), lines 192-193, line 185 (unknown speed accepts any wrap up to 10 Gb/s); Traffic never reads sysUpTime  
How to reproduce: agent.py modes `t32wrap` (100 Mb/s port, ifSpeed given), `t32nospeed`, `t32reboottop`.  
What happens / what should happen: t32wrap gives a correct 47.9 Mb/s but Busiest % is "-", so a port near 100% is never highlighted although ifSpeed was walked (`ls`). t32nospeed shows "2.07 Gb/s" for a counter reset; t32reboottop shows "10 kb/s" for a reboot. Use `ls` for Sp, and compare sysUpTime before and after (one GET).  
Confidence: confirmed (ran it)  

### 91. Inspection treats any root in a "Third-Party Root CAs" (AuthRoot) store as public, including one an admin or GPO put there (from S5-20)
Severity: wrong result  
Where: src\Checks\Inspection.cs lines 170-179 (OnPublicList returns true on any AuthRoot thumbprint match before the Mozilla / Windows-list check)  
How to reproduce: deploy an inspection root through Group Policy › Public Key Policies › Third-Party Root Certification Authorities (machine AuthRoot group-policy store), then check an HTTPS site it re-signs.  
What happens / what should happen: InspectedBy returns "", so no "Certificate re-signed by". AuthRoot isn't only Microsoft's list; apply the same thumbprint test (Public or InWindowsList) to roots found there.  
Confidence: suspected (read)  

### 92. UPS (APC MIB): output "sleeping", "rebooting" or "sleeping until power returns" is shown green as "other" (from S5-21)
Severity: wrong result  
Where: src\Checks\SnmpGear.cs line 324 (os 5, 8, 11 fall through to src = 1), line 328  
How to reproduce: an APC card reporting upsBasicOutputStatus 5, 8 or 11.  
What happens / what should happen: "Power: other" at level 0 while the load has no output. Map these to "OFF - no output" (level 2).  
Confidence: suspected (read; APC PowerNet-MIB values)  

### 93. Network sign-in check says "no failed sign-ins (Wi-Fi or wired)" in green when the Wi-Fi log couldn't be read (from S5-22)
Severity: wrong result  
Where: src\Checks\WinEvidence.cs line 100 (the error is shown only when both e1 and e2 are set)  
How to reproduce: a PC where WLAN-AutoConfig/Operational can't be read but the Wired log can (or the reverse).  
What happens / what should happen: green "no failed sign-ins in the last 7 days (Wi-Fi or wired 802.1X)". Name the log that couldn't be read.  
Confidence: suspected (read; EventLogReader is stubbed on mono)  

### 94. Bandwidth: "1.000 GB" is read as 1 GB, and a size in "Gb" is read as gigabytes (from S6-7)
Severity: wrong result  
Where: src\Checks\Calc.cs lines 186-191 (Num / Parse2) and line 196 (Size lowercases the unit)  
How to reproduce: `bw  1.000 GB  100` → "80 s at the full 100 Mbps" (1 GB); `bw  4 Gb  1 Gbps` → "32 s" (4 gigabytes; 4 gigabits is 4 s).  
What happens / what should happen: the parser accepts a decimal comma ("4,7 GB" = 4.7), so it follows European input, but the European thousands form "1.000 GB" silently becomes 1 GB, a factor of 1,000. Refuse a dot followed by exactly 3 digits when there's no other separator (or follow the current culture). Speed rejects "Mb" / "Gb" as ambiguous, but Size lowercases "Gb" to gigabytes, 8× off: treat `Gb` / `Mb` as bits in sizes, or refuse them.  
Confidence: confirmed (H.exe)  

### 95. Packet stack: the IPv6 fragment count ignores the 8-byte fragment header (from S6-8)
Severity: wrong result (minor)  
Where: src\Checks\Calc.cs lines 106-107 (Stack: `fragData = (innerMtu - ip) / 8 * 8`)  
How to reproduce: `stack  udp ipv6 ethernet  (MTU empty)  2900`.  
What happens / what should happen: "IP splits it into 2 fragments". Each IPv6 fragment carries an 8-byte Fragment header, so it holds (1500 − 40 − 8) / 8 × 8 = 1,448 bytes and 2,908 bytes need 3. Payloads 2,889-2,904 (and similar ranges at every multiple) are miscounted. Subtract 8 for IPv6 before rounding. (The IPv4 boundary from round 2 is now right.)  
Confidence: confirmed (H.exe)  

### 96. "GB" in the month review, the six-month review and the data-cap forecast is decimal MB ÷ 1024 (from S6-9)
Severity: wrong result (minor)  
Where: src\Checks\OverTime.cs line 61 (`p.GBDown += r[13] / 1024`), line 260 (`Usage.Month(null) / 1024`); the columns come from src\Brain\Brain.cs line 49 (`mbDown += Mbps / 8`, decimal MB)  
How to reproduce: read: History's "MB down" is Mbps ÷ 8 (10⁶ bytes); dividing by 1,024 gives neither GB nor GiB.  
What happens / what should happen: totals read 2.3% low; on a 1,000 GB plan the cap forecast says "you stay under" until about 1,024 GB, while Calc says sizes are decimal "like ISPs". Divide by 1,000.  
Confidence: suspected (read; arithmetic)  

### 97. Game history is never rolled off when History is off or when no game is watched (from S6-10)
Severity: wrong result (minor, retention)  
Where: src\Gamer\Gamer.cs line 577 (`Minute` returns when `!Cfg.History`) and line 589 (Tidy is called only from Minute); src\History.cs lines 66-68 (History.Tidy deletes only `yyyy-MM-dd*` names)  
How to reproduce: watch a game for a few days, then turn History off, or stop monitoring every game (the loop idles and never calls Minute).  
What happens / what should happen: `history\games-*.csv(.gz)` stay forever, although Settings says game history is kept for "Keep game history for" days; shortening that setting doesn't prune until a game is watched again. Run GameHistory's roll-off from History.Tidy too.  
Confidence: suspected (read)  

### 98. "Type an address in the box above and press Enter to see which route it takes" never shows the route, and runs a full destination check instead (from S8-5)
Severity: wrong result  
Where: src\Ui\FullView.Travel.cs line 151 (the instruction) and line 153 (the card only uses `storyIp`); src\Ui\FullView.Story.cs line 37 (StorySite sets `storyIp = ""`)  
How to reproduce: Network story › click the Router box › type 8.8.8.8 in the box at the top › Enter.  
What happens / what should happen: StorySite clears storyIp and starts FollowPacket, a full DestCheck (DNS, TCP, TLS, HTTP and a trace). The "WHICH ROUTE WINS FOR ..." card appears only for the auto-picked app's IP, never for a typed address. When the typed text is an IP, set storyIp (no network check needed to look up a route), or reword the help.  
Confidence: suspected (read it)  

### 99. "Email it": the app says the address is on the Hall of Fame page, but the page still has the placeholder and hides the button (from S8-6)
Severity: wrong result  
Where: docs\hall-of-fame.html line 215 (`var MAIL = 'HALL-OF-FAME-EMAIL'`), lines 172-176 and 197; src\Ui\FullView.Points.cs line 127  
How to reproduce: open hall-of-fame.html, step 3 "one of two ways"; then make a card in the app.  
What happens / what should happen: the "Email it" pill is hidden (MAIL has no '@'), yet the page still says "one of two ways ... By email, paste the same text", and the app's award box says "or email it (the address is on the Hall of Fame page)". The workflow's dispatch path for emailed cards can't be used by a winner. Put the real address in, or remove the email path from the page and the app until one exists.  
Confidence: confirmed (Playwright: `#mail` has display:none)  

### 100. hof_verify.py crashes on a claim code with any non-ASCII character, and the issue gets an empty answer (from S8-7)
Severity: wrong result  
Where: .github\scripts\hof_verify.py line 125 (`hmac.compare_digest` on str) and line 112 (claim only has whitespace removed); .github\workflows\hall-of-fame.yml lines 186-194  
How to reproduce: submit the form with the code pasted from Word or Outlook (hyphens become U+2011), or with a full-width letter: claim `ADB‑XXXX‑…` (U+2011).  
What happens / what should happen: `TypeError: comparing strings with non-ASCII characters is not supported`, exit 1, nothing in GITHUB_OUTPUT. The workflow posts " (Edit the issue to fix it - the check runs again by itself.)" with no reason and labels the issue "not verified". Normalise (NFKC, Unicode dashes → '-'), refuse anything outside `[A-Z2-7-]` with a clear message before compare_digest, and treat an empty RESULT as a server error in the workflow.  
Confidence: confirmed (ran it: exit 1, empty GITHUB_OUTPUT)  

### 101. Self-test: three checks prove less than their titles, and the website says they do (from S8-9)
Severity: wrong result  
Where: src\SelfTest.cs line 1176 ("memory after everything" fails only above 96 MB while printing "budget 64"), line 1198 ("GDI / user handles" asserts nothing), line 306 (the leak test judges only when `gdi0 > 0`, so a failing GetGuiResources passes silently), line 656 ("a check question per Learn topic, a challenge per lab, learning paths that exist" never checks labs); docs\index.html lines 878-879 ("Memory stays inside the 64 MB budget")  
How to reproduce: read the assertions. Under mono the run printed "private 119 MB (budget 64)" and failed only because 119 > 96.  
What happens / what should happen: a build at 65-96 MB passes "memory", any GDI count passes "GDI", and a lab without a goal passes the points test. Fail above 64 MB (or change the claim), assert a GDI / USER ceiling, fail when GetGuiResources returns 0 on Windows, and check `LabGoals` for every lab except quiz.  
Confidence: confirmed (read every T(); 71 T() calls, 70 without --samples; the memory print seen under mono)  
Rule: 7  

### 102. Every deep test leaves an encrypted "diagnosis" file in the user's Documents that nobody can open (from S8-10)
Severity: wrong result  
Where: src\Ui\FullView.DeepTest.cs lines 91-97 (section 2 runs "friend"); src\Checks\OverTime.cs lines 555-575 (FriendBundle writes Documents\AdeeboMeter-diagnosis-<time>.txt and encrypts it with a fresh code)  
How to reproduce: `AdeeboMeter.exe --deeptest`, then look in Documents.  
What happens / what should happen: each run leaves an AdeeboMeter-diagnosis-yyyyMMdd-HHmm.txt.adeebo whose code was only in rows the deep test throws away. Add `friend` to ByHand, or point it at a temp folder in deep-test mode.  
Confidence: suspected (read it)  

## Slow

### 103. A hostile or oversized key-backup file is decrypted whole into memory before the HMAC is checked (300 MB file used 585 MB and took 12 s) (from S4-1)
Severity: slow  
Where: src\Checks\EcKeys.cs line 185 (Restore: `FileCrypt.DecryptCode(input, o /* MemoryStream */ ...)`); src\Checks\FileCrypt.cs lines 129-157 (Open: one pass, decrypt and HMAC together), line 333 (only cap is 4 GB + 1 MB), line 350 (`File.OpenRead`, not the Stop-aware `In()`)  
How to reproduce: write `"ADEEBO" 01 03` + iterations 100000 (big-endian) + 32 random bytes + 300 MB of random data + 32 bytes. Toolbox › Open an encrypted file, any code (h4/t2.exe calls FileCrypt.Decrypt on it).  
What happens / what should happen: the whole body is decrypted into a MemoryStream before the tag is compared: "wrong code" after 12.2 s with a peak working set of 585 MB. Files up to 4 GB pass the size check; the MemoryStream doubles up to its 2 GB limit, the error is swallowed as "wrong code", and Stop does nothing on this path. A real key backup is under 1 KB. Refuse mode-3 files over a few KB, and check the tag before decrypting (a second pass over a tiny file). The format notes don't match the code: FileCrypt.cs lines 19-20 ("checked BEFORE anything is decrypted ... never half-opened") and EcKeys.cs line 22 ("refused before anything is decrypted"); since round 1 the code decrypts and MACs in one pass, writing plaintext to a hidden temp file or memory first and dropping it if the tag fails.  
Confidence: confirmed (ran it: h4/t2.exe, 300 MB file, mono)  
Rule: 7  

### 104. An SNMP walk has no size cap in bytes: a device with long values makes AdeeboMeter use about 450 MB (from S5-1)
Severity: slow  
Where: src\Checks\Snmp.cs line 367 (Walk: only a 2,000-value cap), line 192 (ReadPdu keeps every Raw), lines 406-433 (Text returns the whole string); src\Checks\SnmpTools.cs lines 278-317 (Walk / Render keep 2,000 texts and 300 rows of full length)  
How to reproduce: `python3 agent.py 11161`, then Toolbox › SNMP Walk on 127.0.0.1:11161, community `huge`, OID 1.3.6.1.4.1.9.1 (each GETBULK answer is one 60,000-byte OCTET STRING). Harness: `bash mem.sh mono snmp.exe r4.dll app 11161 huge/1.3.6.1.4.1.9.1`.  
What happens / what should happen: 2,000 values of 60 KB (120 MB of Raw bytes), Tables() turns each into a 60,000-character string (another 240 MB), and 300 become rows. Peak RSS 455 MB; the walk took 50 s and finished as "2000 values". A printer, a buggy switch or a hostile agent can do this. Cap the bytes kept per walk (e.g. 4 MB) and cut each value's text (e.g. 512 characters) before it goes into a row.  
Confidence: confirmed (ran it: hostile agent, peak RSS 455,288 kB under mono)  
Rule: 7  

### 105. DNS and captive-portal reads have per-read timeouts only - no overall deadline, no size cap on DoH, and no Stop: a dripping server holds the Toolbox for hours or days (from S3-23, S3-24, S3-26)
Severity: slow  
Where: src\Checks\Toolkit.cs lines 564-570 (QueryDoh: ReadWriteTimeout 6 s per read, 64 KB cap), lines 527-529 (TCP retry: ReadN with 3 s per read), line 443 (Dig takes no Run), also used by MoreTools.DnsCompare and TRACE; src\Checks\DestCheck.cs lines 129-133 (Portal: 4 KB, 4 s per Receive, no total deadline); src\Checks\Protocols.cs lines 123-124 (Encrypted DNS: `r.ReadToEnd()`, only Timeout set, ReadWriteTimeout left at 300 s, no size cap)  
How to reproduce: h3/srv.py port 18081: a 200 answer with `application/dns-message`, Content-Length 60000, then 1 byte every 4 s; `QueryDoh(http://127.0.0.1:18081/dns-query, ...)`. For the portal: a network (or the portal) answering msftconnecttest.com with 1 byte every 3.9 s. For Encrypted DNS: a network that intercepts cloudflare-dns.com or dns.google with a large or slow body.  
What happens / what should happen: QueryDoh was still running when the harness was killed at 75 s; 64 KB at 1 B / 4 s is about 73 h, Stop doesn't help and the Toolbox stays busy. The TCP fallback (a TC-bit answer, then a drip) does the same, and TRACE asks servers any domain owner chooses (`dig evil.example TRACE`). Check a destination stays "Checking for a captive portal..." for up to about 4.4 h. Encrypted DNS can use unbounded memory or wait 5 minutes per stalled read. Use one deadline (about 8 s) for the whole query, checked in the read loops, and read at most a few KB, as Reach.Http does.  
Confidence: confirmed (ran it: DoH drip); the TCP path, the portal and Encrypted DNS were read  

### 106. One crafted MIB file in the MIB folder makes the index build run for hours (quadratic regex) (from S5-14)
Severity: slow  
Where: src\Checks\Snmp.cs line 616 (RxTc `::=\s*TEXTUAL-CONVENTION.*?SYNTAX`, Singleline), lines 537-545 (EnsureBuilt holds `lock(k)` with no stop or time limit)  
How to reproduce: a .mib file of `Tn ::= TEXTUAL-CONVENTION STATUS current DESCRIPTION "x"` lines with no SYNTAX (S5 mib.cs).  
What happens / what should happen: 2,000 entries (121 KB) took 4.9 s, 4,000 took 20.3 s and 8,000 (487 KB) 81.7 s: square of the size. An allowed 8 MB file runs for hours (mono times; .NET's backtracking regex is the same order). Walk and Search show "Reading your MIB files (once)" with no way to stop, hold the lock so every later MIB walk or search waits, and rebuild next time because the index file is never written. Bound the lazy scan (e.g. `[\s\S]{0,4000}?SYNTAX`), use a regex match timeout, and check run.Stop.  
Confidence: confirmed (ran MibIndex.BuildLines on mono)  

### 107. iperf3 download: a reset stream spins in a tight exception loop until the test ends (from S3-25)
Severity: slow  
Where: src\Checks\Toolkit.cs line 339 (`try { n = s.Receive(buf); } catch (SocketException) { continue; }`)  
How to reproduce: iperf3 `-R -t 60` to a server that resets a data stream part-way (server killed, a middlebox RST).  
What happens / what should happen: on Windows, Receive on a reset TCP socket throws ConnectionReset on every call, so the thread loops throwing and catching for the rest of the test (one core busy). The `continue` is meant for the 2 s ReceiveTimeout only: continue only on SocketError.TimedOut, break otherwise. Linux returns 0 after the first ECONNRESET, so mono can't show this.  
Confidence: suspected (read it; Windows-specific)  
Rule: 7  

### 108. Three failed Wi-Fi service reads (including "no Wi-Fi interface right now") switch the Wi-Fi watch to netsh.exe for the rest of the session (from S1-11, S2-9)
Severity: slow  
Where: src\Monitor.cs lines 463-470 and 483 (`++wlanApiFails >= 3` → `wlanApiOk = false`, never set back); src\Link\LinkInfo.cs lines 195-197 (Current returns -1 when WlanEnumInterfaces lists none, or when the handle fails)  
How to reproduce: three Wi-Fi reads in a row fail: the WLAN service restarting after a driver update or reset, WlanQueryInterface returning ERROR_INVALID_STATE (5023) during a reconnect, or a resume from sleep where the adapter takes 20-40 s to come back (AdapterKind is still "Wi-Fi").  
What happens / what should happen: every later read starts netsh.exe (and a console host), every 30 s, or every 10 s with the view open, all day until restart: the cost c529c2b removed comes back silently. Treat "no interface" as "no Wi-Fi now", not "this Windows can't", and retry the service path now and then (every 10 minutes, or after a network change or resume).  
Confidence: suspected (read)  

### 109. GameHistory's week verdict is quadratic: 0.37-0.5 s per game while holding the lock the Gamer page's paint waits on (from S6-6)
Severity: slow  
Where: src\Gamer\Gamer.cs line 663 (Load: `grp.Average(...)` evaluated inside `days.Count(d => d.Where(evening).Any(x => ... grp.Average(...)))`), line 654 (the whole Load under `lock (k)`), line 645 (Week takes the same lock), line 650 (Warm loads each watched game in turn)  
How to reproduce: G.exe load 11: 7 days of history, one region every minute plus 10 surveyed every 5 minutes (today plain, older days gz). Load takes 511 ms (3 more: 1,423 ms) while the 7 Read calls take 74 ms; with one server 367 ms against 25 ms.  
What happens / what should happen: for each server, every evening row of a day without trouble recomputes the average over ~10,000 rows (about 25 M LINQ steps per server per week). Warm runs this for every watched game every 10 minutes on the GamePing thread, and a Gamer page or overlay paint calling `Best → Week` blocks on `k` meanwhile: with 5 games about 2 s of locked CPU per cycle. Compute the average once per group, and read and group outside the lock.  
Confidence: confirmed (timed with G.exe on mono)  
Rule: 7  

### 110. Game probes block thread-pool threads, and a failed DNS lookup is retried 16 times a second (from S6-11)
Severity: slow  
Where: src\Gamer\Gamer.cs line 441 (`Task.WaitAll(go.Select(p => Task.Factory.StartNew(() => ProbeOne(...))), 2500)`, up to 18 per second), line 510 (`Dns.GetHostAddresses`, no timeout, failures not cached), line 535 (sync `Ping.Send` 1 s), line 544 (TCP 1.5 s)  
How to reproduce: Gamer preset, a Valve game whose relays don't answer ICMP (16 probes of 1 s every second); or lose DNS while playing an AWS-region game (16 lookups of about 10 s started every second; the "cached 5 minutes" comment covers successes only).  
What happens / what should happen: each probe blocks a pool thread; the pool starts at the core count and grows by about 2 threads a second, so probes queue. WaitAll gives up at 2.5 s and the next pass queues 16-18 more, so the backlog and thread count grow and other pool work (Tidy, the Toolbox runners, BeginInvoke continuations) waits behind them. Use SendAsync / ConnectAsync, cache a failed lookup for a few seconds, and skip a server whose previous probe hasn't finished.  
Confidence: suspected (read)  
Rule: 7  

### 111. What changed repaints every second; Network story every 2 s; a running tool's canvas twice a second (from S7-14)
Severity: slow  
Where: src\Ui\FullView.cs line 519 (`if (tab == "changes") changesCanvas.Invalidate();` on every Monitor version, i.e. every second, unkeyed), line 582 (travelCanvas.Invalidate on every connection refresh, every 2 s on the travel tab), line 525 (tbCanvas.Invalidate every 500 ms while tbBusy), lines 871 and 894 (UpdateBadges → PageBadge("changes") builds a list of up to 300 events twice per 500 ms tick, on every page)  
How to reproduce: open Time machine › What changed and leave it; or run Routing protocols on my segment (120 s) or a 60-minute MTR and count paints (the DB slow-paint log shows only paints over 40 ms).  
What happens / what should happen: What changed (insights, outage log, up to 300 event rows) repaints every second whether or not an event arrived; Network story with no target every 2 s; a Listen run repaints 240 times while only the status changes; a 60-minute MTR repaints 7,200 times, each drawing up to 3,600 series bars and copying the series list. The other timer paths use a key (headKey, pathKey, sideKey, footKey, ovKey, gamerKey): key these on M.Events' newest time, connSnap's version, run.Status plus the series count, and cache the "changes" badge.  
Confidence: suspected (read; header/path/side/footer/overview/gamer keyed; changes, travel, tbCanvas, destCanvas-while-running and liveCanvas unkeyed)  
Rule: 7  

### 112. Widget cards create a new GDI font on every draw (from S7-15)
Severity: slow  
Where: src\Ui\Theme.cs line 425 (GamerCard: `new Font(fHead.FontFamily, 26 * S, …)`), line 489 (MetricCard: calls, engineer), line 321 (Buddy asleep: `new Font("Segoe UI", …)` per chip draw)  
How to reproduce: widget style Gamer while playing, or hover the Calls or Engineer card (it redraws every second while hovered or playing).  
What happens / what should happen: one font created and disposed per draw, about 3,600 an hour while gaming, against the class comment "All fonts made once; nothing is created per paint". Make the 26 px font and the "z" font once in Draw.Init.  
Confidence: suspected (read)  
Rule: 7  

## Cosmetic

### 113. UI text and comments still describe netsh, Engineer and old figures (from S1-12, S7-19)
Severity: cosmetic  
Where: src\Ui\FullView.Settings.cs line 127 (Wi-Fi watch: "The priciest job here - it starts a small Windows tool (netsh) each time" - it now asks the WLAN service, netsh is only the fallback since c529c2b), line 328 (About: "from Penny to Engineer" - Engineer was merged into Balanced in 4fd6e22), line 111 (French Language help: "les noms des outils … restent en anglais" - all 163 Toolbox titles are now translated, fr2.py found 0 untranslated), lines 227, 33 and 392 (Penny "Wi-Fi every 2 min", the Wi-Fi row `Every(Cfg.WifiSec)`, About's Wi-Fi row - with the window open Wi-Fi is read every 10 s whatever the preset, Monitor.cs line 223 `ViewOpen ? 10`); src\Program.cs line 302 ("one netsh start every 30 s"), line 11 (a portable copy's settings and log "in its data folder" - they are written next to the exe); src\Points.cs line 15 ("Off until you turn it on" - on from day 1) and line 28 ("about 580 in all" - 806 now); src\AppCtx.cs line 211 ("the game settings are kept" - ApplyPreset resets GameBgSec / GameBgServers); src\History.cs line 12 ("1-90 days" - up to 180); src\HallOfFame.cs line 141 ("32 characters fit at 18 px" - the loop's last size is 16 px)  
How to reproduce: Settings › Background checks › Wi-Fi watch; Settings' "Tune it" tip; About › Getting around; Settings › General › Language in French.  
What happens / what should happen: the Wi-Fi row tells users the default job starts a program each time, so they may turn off a check that is now cheap; the other texts and comments no longer match the code. Update the wording, and add "(every 10 s while this window is open)" as the router row already does.  
Confidence: confirmed (read it, and saw the log next to the exe in the portable run; fr2.py over the French table)  

### 114. Points and docs numbers are out of date in several places (from S8-13)
Severity: cosmetic  
Where (measured from r4.exe: Tools2 = 163 tools, Learn = 68 topics, terms 337, earnable 806, Points.Max 900): README.md lines 32 and 37 ("158 tools", "59 topics"); CHANGELOG.md line 7 ("158 tools") and line 58 ("capped at 800 (everything earnable is about 750)"); docs\THEORY-OF-EVERYTHING.md lines 232 and 235 ("332 terms, 59 topics", 158); docs\index.html line 623 (59 topics), "Probe - every 2 to 10 seconds" (Penny's router check is 30 s) and "one line to today's 7-day history file" (default 3 days, up to 180); docs\hall-of-fame.html lines 120, 123 and 132 ("158 tools", "59 topics", "About 750 points ... two thirds"), and "a click or a key, not just opening it" (Time machine pays on simply opening a recorded day, src\Ui\FullView.Replay.cs line 150); src\Points.cs lines 28-29 comment ("about 580 ... 500 means you've done nearly everything"); src\Ui\FullView.Points.cs line 277 ("Nearly everything AdeeboMeter can teach" at 500, which is 62 %); Learn › Games (30 items, 100 points) missing from both "how to earn" lists (FullView.Points.cs line 285; the hall-of-fame.html table)  
How to reproduce: `mono s8/max.exe app/AdeeboMeter.exe` → "tools 163, learn 68 topics, ... everything there is to earn: 806 Points.Max = 900".  
What happens / what should happen: update the numbers; better, have the self-test print them and compare them with README.  
Confidence: confirmed (reflection census)  

### 115. The newest features are missing from the CHANGELOG, README and website (from S8-14)
Severity: cosmetic  
Where: e80412e touched none of CHANGELOG.md, README.md or docs\  
How to reproduce: `git show e80412e --stat`; grep the docs for buddy, mood, roast, therapy, revocation, "Break the Adeebo Award", "packet dungeon" (0 hits).  
What happens / what should happen: the 1.1.5 release notes come from CHANGELOG (release.ps1 lines 22-26), so they would leave out the certificate walk and revocation option, the buddy and hats, the moods, 3 games, and Roast, Therapy and Letter. The heading is still "## 1.1.5 (unreleased)", and release.ps1 would publish it that way.  
Confidence: confirmed (git show / grep)  

### 116. French: untranslated captions, and one overlap measured in English (from S7-20)
Severity: cosmetic  
Where: knowledge\lang-fr.txt; src\Ui\FullView.Toolbox.cs line 991 (the award is placed after `tbTitle.ToUpperInvariant()` measured in English, but the title is drawn as L.T(tbTitle)), lines 620-621 and 679-681 (RoundBox.Label / Cue set without L.T); src\Ui\FullView.cs lines 158, 194, 268 (cues), 219-228 (Health chips), 882 ("Your cable" / "Cable")  
How to reproduce: Settings › Language › Français, restart; open the Toolbox, a tool, and Investigate › Health. fr.py extracts every L.T("…"), `new PillButton("…")` and `new RoundBox(…, "…")` literal from src and diffs the table.  
What happens / what should happen: 25 literals are missing from the table. Real UI ones: "By question", "All tools (list)", the "Adeebo" header button, the cues "Filter by app, host, IP or port", "host, IP or URL - e.g. github.com:443", "Find a process or app", "Site, app or server - e.g. outlook.office.com", "Find a process", "Search: DNS, TCP, svchost, jitter...", "Jump to a tab, program, term or protocol...", "Find a feature... mtr, dns, wifi, certificate", "Your website or mail domain (optional)", "Or type a site - e.g. github.com", "Room name - e.g. Kitchen", plus GameEditor's 6. Array-built captions too: 11 Health chips ("Pre-call check", "DNS speed test", "VPN map", "What can AdeeboMeter see here?", "Memory, disk, heat", "Disk", "Background jobs", "Crashes and hangs", "Graphics per app", "What changed (7 days)", "Battery") and the tab names "Your cable" and "Cable". Every Toolbox box label and cue (set after construction, never through L.T), the SNMP view tabs, the group headers, "Copy as text", "Save...", "Takes … Press F1", the safety tick and "DONE" stay English. On a tool page the green "+N Adeebo Points" is drawn at the English title's width, so it overlaps longer French titles ("dig - any DNS record" is 1.95× longer in French). "Play again" is listed twice (line 983, same value).  
Confidence: confirmed (ran it: fr.py / fr2.py over src and lang-fr.txt)  

### 117. Settings rows with many choices spill over the label and the side menu on a 1366×768 laptop at 125 % (from S7-18)
Severity: cosmetic  
Where: src\Ui\FullView.Settings.cs lines 282-316 (SetRow: `tw = w - ow - 24*S` may go negative; the segment strip is drawn at `x + w - ow`), line 182 (w)  
How to reproduce: a maximised view at 1366×768, 125 % (the most common laptop setup): Settings › History & data, General (Voice), Alerts (Monthly data cap). French is a little worse.  
What happens / what should happen: with Liberation Sans (Arial metrics, close to Segoe UI) at S = 1.25, w = 569 px: "Keep history for" segments are 620 px wide (help column -80 px), Voice 519 px (21 px), Monthly data cap 496 px (44 px). The strip starts left of the row, over the label and up to the side menu (whose click areas then overlap), and the help text gets ≤ 0 width or about 2 characters per line. At the minimum window (980 px, 100 %): HistDayNames -150 px, Voice -69, Cap -52. When `ow > w * 0.6`, wrap the segments onto a second line.  
Confidence: confirmed (ran it: seg.cs with the real option arrays through L.T and FullView's width formula; font widths approximate Windows)  

### 118. Switching from dark to light leaves the Toolbox safety tick's text near-white on white (from S7-16)
Severity: cosmetic  
Where: src\Ui\FullView.cs lines 557-570 (Recolor: only TextBox and Label get a ForeColor); src\Ui\FullView.Toolbox.cs line 643 (tbSweepOk created with ForeColor = Theme.Ink)  
How to reproduce: dark theme, open the Toolbox (it is also built 2.5 s after the view opens), switch to light with the header switch, then open Ping sweep or any Lab tool.  
What happens / what should happen: "This is my own network, or I'm allowed to send test traffic to it" is drawn #E6EDF3 on white, practically invisible; it is the safety statement the user should read before ticking. Recolor should handle ButtonBase / CheckBox (ForeColor = Ink).  
Confidence: confirmed (ran it: rc.cs under xvfb/mono: after Apply(dark) → Apply(light) → Recolor the checkbox fore was #E6EDF3 on White)  

### 119. High contrast: switching it on or off while running isn't followed, and BlueSoft panels lose their text (from S7-17)
Severity: cosmetic  
Where: src\Ui\Theme.cs lines 48-76 (Apply: the high-contrast override maps Ink, Muted, Card, Line, RowSel and BlueDark, not BlueSoft or Blue); src\AppCtx.cs lines 139-144 (re-applies only `if (d2 != Theme.Dark)`)  
How to reproduce: (a) with AdeeboMeter running, turn on a Windows contrast theme (app mode unchanged). (b) Start in a contrast theme where WindowText is white while the app mode is light; look at the Overview "Sounds like…" hint and the first-run Simple/Pro card.  
What happens / what should happen: (a) nothing changes until restart (UserPreferenceChanged re-applies only on a dark/light flip, and turning the contrast theme off keeps the system colours). (b) those panels are BlueSoft #EEF4FF with Ink = WindowText (white) text: white on near-white (FullView.Overview.cs lines 222-224 and 312-313); the tip bar, the selected Settings preset card and the About heading discs are BlueSoft too. Re-apply on any UserPreferenceChanged (or HighContrast change), and map BlueSoft to SystemColors.Window under high contrast.  
Confidence: suspected (read)  

### 120. Mood: the provider "excuse" times an outage from the first outage of the day, and the header line never updates (from S1-6)
Severity: cosmetic  
Where: src\Mood.cs lines 30-41 (downSince and greenSince change only inside Quip); src\AppCtx.cs lines 385-391 (a green widget is the folded chip, which never calls Quip); src\Ui\FullView.cs line 514 (the header repaints only on a level, headline, flow or hour change)  
How to reproduce: view closed, widget card shown during a red outage at 09:00 (downSince = 09:00); the line comes back and the widget folds to the green chip, so Quip isn't called and downSince isn't reset; at 14:00 the provider goes down again.  
What happens / what should happen: the card at once says "<ISP>'s excuse, after 5 h: They've misplaced the internet...", counting from the morning outage. In the engineer view the excuse comes only when the headline changes, so it usually never appears, or keeps an old "after 3 min" for an hour. The koan's "two hours of all green" has the same problem. Keep the down and green clocks from the Monitor (netDownSince / Judge), not from paint calls.  
Confidence: confirmed (ran it: misc.cs calls Mood.Quip for an outage with downSince 4 h back, and the next outage's first quip says "after 4 h"; the repaint rules were read)  

### 121. Certificate expiry: a certificate that expired less than a day ago reads "0 days left" / "expired 1 days ago" (from S4-15)
Severity: cosmetic  
Where: src\Checks\SecChecks.cs line 215 (CertExpiry: `(int)(NotAfter - Now).TotalDays` truncates -0.5 to 0, so `d < 0` is false); src\Checks\CertChain.cs line 353 ("expired " + Math.Max(1, -days) + " days ago")  
How to reproduce: Security checkup › Certificate expiring, for a site whose certificate expired 12 hours ago; HTTPS check against h4 server 8446 (leaf expired 12 h ago).  
What happens / what should happen: the checkup says "0 days left (2026-11-13)" in red, never "EXPIRED"; the HTTPS check says "expired 1 days ago". Compare against NotAfter directly (`DateTime.Now > NotAfter`), as CertChain's verdict does, and fix the plural.  
Confidence: confirmed for CertChain (ran it); suspected for SecChecks (read)  

### 122. Revocation: "couldn't tell - the issuer's list didn't answer in 10 s" also appears when the certificate has no CRL or OCSP address at all (from S4-20)
Severity: cosmetic  
Where: src\Checks\CertChain.cs line 164  
How to reproduce: revocation setting on, Chain check on a private-CA certificate without a CRL Distribution Point or AIA OCSP.  
What happens / what should happen: Windows returns RevocationStatusUnknown at once, but the text blames a 10 s timeout. Say "no revocation address in the certificate" when there is no CDP or OCSP extension; keep the timeout text for OfflineRevocation.  
Confidence: suspected (read)  

### 123. Letter to my provider says "The outages above ..." when there were no outages (from S4-21)
Severity: cosmetic  
Where: src\Checks\Fun.cs line 263  
How to reproduce: history with router delay over 15 ms on average and no outages in the period; run Letter to my provider.  
What happens / what should happen: "The outages above are the internet check getting no answer at all, which my Wi-Fi alone doesn't explain." No outages were mentioned, and with none the trouble may well be the Wi-Fi. The letter's own rule is "never lies": add the sentence only when w.Outages > 0.  
Confidence: suspected (read)  

### 124. "The internet is slow" with the router check off ends with green "router no answer" (from S5-19)
Severity: cosmetic  
Where: src\Checks\Investigate.cs line 102 (F(NaN) = "no answer")  
How to reproduce: S5 inv.cs s12 (GwPing empty, internet 20 ms).  
What happens / what should happen: "Nothing on the path is slow right now: router no answer, internet 20 ms." Say "router not measured".  
Confidence: confirmed (ran it)  

### 125. Wi-Fi 7 320 MHz shows as 160 MHz; WPA3-Enterprise (non-192) shows as WPA2-Enterprise; FILS as plain "WPA2" (from S2-14)
Severity: cosmetic  
Where: src\Link\LinkInfo.cs lines 314-342 (ReadIes: no EHT Operation, ext. id 106), lines 279-281 (Rsn)  
How to reproduce: W.exe: an HE 6 GHz IE (width code 3) plus an EHT Operation IE with width 4 (320 MHz); RSN AKM 5 alone; AKM 14.  
What happens / what should happen: "width=160"; AKM 5 → "WPA2-Enterprise" (WPA3-Enterprise only mode is AKM 5 with PMF required); AKM 14 → "WPA2". AirLog then judges a 320 MHz neighbour's overlap with a 160 MHz block. Read EHT Operation's channel width; report AKM 5 with MFPR set as WPA3-Enterprise.  
Confidence: confirmed (ran it: W.exe)  

### 126. Driver dates show one day early west of UTC (from S2-16)
Severity: cosmetic  
Where: src\WindowsDeep\DevInfo.cs line 71 (`FromFileTimeUtc(ft).ToLocalTime()` for DriverDate)  
How to reproduce: a device whose INF says DriverVer 03/15/2024, on a PC in Ottawa (UTC-5).  
What happens / what should happen: shown as 14 Mar 2024 (Inbox() already allows "20 or 21 June 2006" for this reason). Keep DriverDate in UTC, date only; only Arrived and Removed are real moments.  
Confidence: suspected (read)  

### 127. Learn › hash says MD5/SHA-1 let you "build a second file with the same fingerprint" (from S2-17)
Severity: cosmetic  
Where: knowledge\learn.txt `== hash`, the "more:" line  
How to reproduce: Learn › A hash.  
What happens / what should happen: MD5 and SHA-1 are broken for collisions (someone can make two files that share a fingerprint), not for matching an existing file's fingerprint (second preimage). As written it suggests any download's SHA-1 can be forged to match. Say "...and nobody can make two different files with the same fingerprint (MD5 and SHA-1 are broken for that)".  
Confidence: confirmed (read it)  

### 128. Misleading error text: dig "no answer from X in 3 s" for a name it never sent; iperf "-6" says "Sequence contains no matching element" (from S3-21)
Severity: cosmetic  
Where: src\Checks\Toolkit.cs lines 476-477 with Question line 551 (an empty label or one over 63 bytes → null → "no answer"); line 264 (`.First(...)`)  
How to reproduce: dig `a..b` or a 70-character label with server 127.0.0.1 → "no answer from 127.0.0.1 in 3 s". iperf3 `127.0.0.1` with `-6` → "Upload | failed: Sequence contains no matching element".  
What happens / what should happen: say "a label is empty or longer than 63 characters" (nothing was sent), and "this server has no IPv6 address".  
Confidence: confirmed (ran it)  

### 129. Check a destination: path MTU is 1 byte low for about a third of the sizes below 1200 (from S3-22)
Severity: cosmetic  
Where: src\Checks\DestCheck.cs lines 376-377 (after 1172 fails, `hi` stays 1471 and the search stops after 9 rounds)  
How to reproduce: simulate the search for every true payload 548-1471 (Python, same loop): 277 of 924 sizes end 1 below the truth (e.g. 550 → 549).  
What happens / what should happen: set `hi = 1171` when 1172 fails, or loop until `lo == hi`.  
Confidence: confirmed (ran the same algorithm in Python)  

### 130. Bandwidth calculator: a size alone or a time alone gives a blank result, and common units are refused (from S6-12)
Severity: cosmetic  
Where: src\Checks\Calc.cs lines 165-183 (Bandwidth), lines 202 and 207 (unit lists)  
How to reproduce: (H.exe) `bw  50 GB` and `bw  (empty)  (empty)  2 h` return 0 rows; `10 minutes`, `30 seconds`, `1 minute`, `100 Mb` (as an ISP writes a speed), `100 Kbit/s` and `1 Tbps` are refused ("type e.g. 90 s, 10 min or 2 h"), although `hours` is accepted.  
What happens / what should happen: say what a size or a time alone needs (a second value), and accept `minute(s)`, `second(s)`, `Kbit`, `Tbps`, and "Mb" as a speed.  
Confidence: confirmed (H.exe)  

### 131. Gamer comments and on-screen notes describe limits that have changed (from S6-13)
Severity: cosmetic  
Where: src\Gamer\Gamer.cs line 22 ("Caps: 8 servers at once, 1 probe a second each, 10 probes a second in total"; now 8 or 16 and MaxServers + 2 per second), lines 342 and 688 ("timed every 5 s"; the code uses 15 s, line 443), lines 568 and 579-586 (the "router ms" column of games-*.csv is always empty: `gw` is never assigned); src\Ui\GameEditor.cs line 53 ("8 are watched at once"; 16 on the Gamer preset)  
How to reproduce: read; open Edit servers on the Gamer preset.  
What happens / what should happen: correct the text, and fill the router column or drop it.  
Confidence: confirmed (read)  

### 132. Learn and console tools contact a game's servers without the Gamer Zone's consent click, unlike Wi-Fi vs cable (from S6-14)
Severity: cosmetic (inconsistent consent)  
Where: src\Gamer\GamerTools.cs line 27 (Pick: any game by name, watched or not), lines 50-52 (Gpn: trace and pings to it), lines 191 and 200 (ConsoleCheck: `GamePing.Probe` every 5 s); compare src\Gamer\GameCompare.cs line 290 ("click it in the Gamer Zone first - that's your OK to contact its servers")  
How to reproduce: Toolbox › Would a GPN help me, or Console mode, with a game never clicked (e.g. `fortnite`).  
What happens / what should happen: the game's region is traced, pinged or probed. The user ran the tool, so rule 1 holds, but the Gamer Zone's promise ("nothing is sent to a game company's servers until the user clicks that game") and GameCompare's rule say otherwise. Apply the same check in both tools.  
Confidence: suspected (read)  

### 133. Survive the outage: "A new personal best" on a tie, and on every repaint (from S6-15)
Severity: cosmetic  
Where: src\Ui\FullView.Games.cs lines 267-270  
How to reproduce: score exactly your previous best.  
What happens / what should happen: `best` is `outScore > Cfg.OutageBest`, but the shown line tests `outScore >= Cfg.OutageBest`, so a tie is announced as a new best. Use the `best` flag computed before saving.  
Confidence: confirmed (read)  

### 134. Keyboard users never earn the "used a page" point on painted-only pages (from S7-21)
Severity: cosmetic  
Where: src\Ui\FullView.cs line 793 (WireKeys Press: runs the action, no PageUsed), against line 812 (mouse click: `PageUsed(usedTab)`)  
How to reproduce: Tab to a painted action on What changed, About or Network story (pages without real buttons) and press Enter.  
What happens / what should happen: the action runs, but "Use a page for the first time" is never awarded; a mouse click on the same spot awards it. Press should call PageUsed(tab) like the click path.  
Confidence: suspected (read)  
Rule: 10 (adjacent: earning differs by input method)  

### 135. Website contrast below WCAG AA in both themes (from S8-15)
Severity: cosmetic  
Where: docs\hall-of-fame.html (td.pts #2DA44E on white 3.22:1; th #6B7685 on #F6F8FB 4.33:1; dark .pill.ghost 4.07:1); docs\index.html (.btn.ghost 4.04:1 light and 3.30:1 dark; .meta/.ver 4.49:1; mock chips white on green or amber at 2.59 and 3.14; dark "All quiet" chip 2.06:1; dark .k 4.07:1)  
How to reproduce: `python3 s8/web.py` (Chrome, 360 / 768 / 1366, light / dark).  
What happens / what should happen: 26-36 text elements per page fall below 4.5:1. Darken the green to about #1A7F37 and the muted greys, and lighten the dark-mode blue links.  
Confidence: confirmed (Playwright computed styles)  

### 136. Hall of Fame page: long handles are cut off, combining marks spill over the label, and a stray "site" value hides the list (from S8-16)
Severity: cosmetic  
Where: docs\hall-of-fame.html lines 59-60 (.winner overflow:hidden, link not wrapping) and line 240 (`SITES[String(w.site)]` reads Object.prototype keys)  
How to reproduce: `python3 s8/hof_web.py normal` and `... proto` (screenshots s8/hof_*.png).  
What happens / what should happen: a 253-character Bluesky handle or 100-character LinkedIn handle (both accepted by hof_verify) is clipped mid-word; a name of "Z" plus 31 combining marks draws up over "★ ADEEBO AWARD"; an entry with `"site":"constructor"` (only by hand-editing the JSON) throws in forEach, the later cards vanish and the count says "Couldn't load the list". Use `overflow-wrap:anywhere` on the link, cap combining marks per base letter in clean_name and CleanName, and use `Object.prototype.hasOwnProperty.call(SITES, site)`.  
Confidence: confirmed (Playwright)  

### 137. ByHand points to a test plan that doesn't exist (from S8-17)
Severity: cosmetic  
Where: src\Ui\FullView.DeepTest.cs line 23 ("checked by hand (TESTPLAN-1.1.md)")  
How to reproduce: `git ls-files | grep -i testplan` finds nothing.  
What happens / what should happen: the deep-test report says 40-odd tools are "checked by hand" against a plan that isn't in the repository. Add the plan, or point at docs\LIVE-TESTS.md.  
Confidence: confirmed (git ls-files)  

## Still present from earlier rounds

Merged from all eight slices; rows reported by two slices appear once. "See #N" points to this round's entry where the leftover is now described in full.

| earlier id | where now | evidence |
|---|---|---|
| R1 #43 / R2 #58 / R1 version strings (version 1.1.4) | src\Program.cs lines 115-116 | `Version = "1.1.4"`; the report JSON says "AdeeboMeter 1.1.4" while the website, README and JSON-LD say 1.1.5, and release.ps1 / pack.ps1 take the version from Program.cs. New effect: with 1.1.4 installed, SetupWindow.Same is true (Setup.cs line 321), so the 1.1.5 download offers "Open AdeeboMeter" and starts the old copy, and Single.RunningIsOlder (Program.cs line 463) never replaces a running 1.1.4 (S1, S8) |
| R2 #4 (Key and Code public, card signable with the exe) | src\HallOfFame.cs lines 31-35, 59; release.ps1 line 44 | unchanged by design (comment at lines 23-25 accepts the limit); #5 widens it to every push build (S1, S8) |
| R2 #5 (Hall of Fame impersonation) | .github\scripts\hof_verify.py line 45 (same_person: NFKC + casefold only) | Cyrillic "Аda Lovelace" was `added` next to "Ada Lovelace" (ran it); 32 combining marks also accepted (S8) |
| R2 #74 (Points "off by default" text) | src\Points.cs line 15 | "Off until you turn it on (Settings)" while Cfg.Points = true; see #113 (S1) |
| R2 #101 (BuildSnap every second) | src\Monitor.cs line 149 | builds a full Snap with Details every second whether shown or not (S1) |
| R2 #103 (interfaces, ARP and DriveInfo every 10 s) | src\Monitor.cs lines 266, 409 | IfEvery 10 s (60 s on Penny only) (S1) |
| R2 #106 (abandoned PTR task) | src\Monitor.cs lines 523-524 | `t.Wait(3000)`, then the task is left running (S1) |
| R1 #152 (wall time in Ago) | src\Model.cs lines 125-128 | dedupe now uses Clock.Mono, but Ago ("Internet back after ...") still subtracts DateTime.Now (S1) |
| R1 #50 (a router that ignores ping painted red / "isn't answering") | src\Checks\Investigate.cs lines 88-90; src\Ui\FullView.Replay.cs line 197; src\Ui\FullView.Story.cs line 85 | inv.cs s10: router ring all NaN, internet 20 ms → level 2 "Your router isn't answering at all - the problem is right next to you" (confirmed); Replay / Story: `lanLv = double.IsNaN(gw) ? 2` (S5, S8) |
| R1 #154 / R2 #28 (What's wrong? on IPv6-only) | src\Checks\Investigate.Whats.cs lines 57, 88, 95 (Gateway only, never Gateway6) | inv.cs s2: Gateway "" and Gateway6 fe80::1 → level 2 "Windows has no default gateway ... Usually DHCP didn't finish" (confirmed) (S5) |
| R1 #49 (RoutingTools part) | src\Checks\RoutingTools.cs lines 36-48 (GwTick) | lastGw / lastMac keyed only by the gateway IP, so another network with the same gateway IP gives "The gateway moved to another device" (read) (S5) |
| R2 #21 (quiet routers in MTR) | src\Checks\PathTools.cs lines 124-130 | the single quiet router is fixed (P13 is -1); the two-router variant is still wrong, see #41 (S5) |
| R1 #58 (ISP report "home" side) | src\Checks\IspReport.cs lines 92-94 | fixed only when the gateway shows in the trace; silent router, other router address and VPN still give "home", see #29 (S5) |
| R1 #96 (SNMP walk that doesn't move) | src\Checks\Snmp.cs line 381 | loop and backwards fixed; the "same OID" agent still finishes as complete, see #87 (S5) |
| R1 #142 / R2 #38 (5/6 GHz overlap on the live page) | src\Link\LinkInfo.cs line 538 (Overlaps), used by LeastCrowded line 301 and Collect line 602 | 5 and 6 GHz still `a == b`: an 80 MHz neighbour on 36 doesn't "share" your 44, and LeastCrowded can pick 40 inside the neighbour's block. AirLog now uses blocks, so only the live page and the advisor are affected (S2) |
| R3-regressions #2 (RTP renumbering) | src\Capture\LiveCapture.cs lines 264-270 | partly fixed: a restart under 30,000 lower still hides all later loss (3.75% → 0.00%), see #80 (S2) |
| R1 #65 / #66, R2 #37 (localized netsh) | src\Link\LinkInfo.cs KeyWords; src\Monitor.cs ChanKeys / StateKeys | partly fixed: Korean and Arabic channel / state and the Russian / Japanese / Chinese per-AP labels are still unread, see #35 (S2) |
| R2 muse (4-hour row cap vs call summary) | src\Brain\Watch.cs lines 62 and 71-72 | `mins` comes from `started`, loss from the first 14,400 rows (S2) |
| R2 muse (OpenWifi misses saved WEP) | src\Link\LinkInfo.cs line 171 | `open` still needs `<encryption>none`, so a saved WEP profile that joins by itself isn't flagged (S2) |
| R2 muse #18 (new-device pop-ups) | src\Brain\Watch.cs line 245 | partly fixed (random MACs no longer count); a home with more than 30 fixed-MAC devices ever seen never gets a pop-up, and the set is never pruned (S2) |
| R1 #146 (same-second names) | src\Capture\Packets.cs line 38 (`journey-HHmmss.pcapng`); src\Capture\LiveCapture.cs line 420 (`adebometer_<app>_HHmmss`) | the work folder has a GUID now, but these two final names still collide within one second (S2) |
| R2 muse #24 (window scaling) | src\Capture\Journey.cs lines 334-339 | mitigated by the 0.7-1.1× band; a scaled window that sits in that band is still reported as "capped" (S2) |
| R1 #36 / R2 #7 (speed server address check) | src\Checks\SpeedTest.cs lines 180-185, 201 | only the Down host's resolved address is checked, the Up host never, and HttpWebRequest re-resolves; see #7 (S3) |
| R2 #54 / R1 #124 (octal addresses) | src\Checks\DestCheck.cs lines 64-67; src\Checks\Icmp.cs line 100; src\Checks\Toolkit.cs line 928 | bare `010.1.1.1` now refused, but `http://010.1.1.1/` → 8.1.1.1 and Resolve4 / Subnet still take octal and dword forms; see #55 (S3) |
| R1 #229 / R1 #24 (one-device guard) | src\Checks\Protocols.cs line 458; src\Checks\UdpMtu.cs line 29; src\Checks\Icmp.cs line 97 | the tick gate (SenderGate) was added, but TWAMP, UDP MTU and the non-Lab ping tools still have no broadcast / multicast guard; see #8 (S3) |
| R2 #71 (Pins without a lock) | src\Checks\MoreTools.cs line 206 | `Cfg.Pins.ToArray()` on the worker without a lock: a smaller window, still unsynchronised (S3) |
| R1 #172 / R2 #92 (pcap memory) | src\Checks\Pcap.cs lines 37, 59-64 | the Wanted filter helps with web traffic, but a 60 MB DHCP-heavy capture still keeps 147k packets: 148 MB managed, 172 MB peak (h4/t3.exe big.pcap) (S4) |
| R1 #179 / R2 #93 (.p7m in memory) | src\Checks\FileCrypt.cs lines 28, 317, 365-369 | still ReadAllBytes plus EnvelopedCms decode and copies, up to 100 MB in and 50 MB out; only the caps changed (S4) |
| R1 S6 / carry-over (update date culture) | src\Checks\SecChecks.cs line 133 | still tries InvariantCulture (M/d/yyyy) first for InstalledOn; unchanged, now with a comment saying it's by design (S4) |
| R2 #11 / R2-cc #113 (TCP lab tiny link) | src\Labs\LabSim.cs lines 37, 43, 51 | Q.exe: 1 Mbps / 5 ms / 0.1×, 480 RTTs → 242 losses (CUBIC), 241 (Reno), util 1.000: a loss every other RTT next to "100% of the link", while the lab text says the extra "waits in the queue" (S6) |
| R3-new #9 (games pay for shown answers) | src\Ui\FullView.Games.cs lines 94, 128, 233, 268, 389 | fixed for thief and detective only; see #22 (S6) |
| R3-new #14 (ping map labels overlap) | src\Ui\FullView.PingMap.cs line 69 | now `(off++ % 2) * 12 * S`; neighbours alternate by draw order, so clusters of 3+ (Valve: Sterling / Washington / NY, plus NA East / NA Central) still overlap (S6) |
| R3-perf #2 (paint waits on Week's lock) | src\Gamer\Gamer.cs lines 645 and 654 | Load still reads and groups under `k`; now quantified as quadratic, see #109 (S6) |
| R2-cc #97 (UpdateBreak.Background every hour) | src\Gamer\UpdateBreak.cs line 129 | `Changes(14)` (two event-log queries) still runs hourly with History off; only `Strong` is gated on History (S6) |
| R2-cc #110 (small undisposed objects) | src\Ui\FullView.PingMap.cs line 49 | `var old = g.Clip` (a new Region) on every Gamer paint, never disposed (S6) |
| R2 #15 (TCP lab cost per frame) | src\Ui\FullView.Labs.cs line 237 | one `SolidBrush` per round (up to 480) on every 66 ms Play frame; both simulations rerun each frame (S6) |
| Muse-200 #15 (MTR Duration clamps at 10 min, tool allows 60) | src\Ui\FullView.Toolbox.cs line 501 vs line 32 | `Math.Min(10, n)` in Duration, `Math.Min(60, min)` in the tool: "30" says "10 minutes" (S7) |
| Muse-200 (TWAMP has no Duration case) | src\Ui\FullView.Toolbox.cs lines 488-547 | no `case "twamp"`; 100 packets say "a few seconds" (S7) |
| R1 (no spf / dkim / dmarc search words) | src\Ui\FullView.Toolbox.cs lines 553-568 | `SPF` → rlisten via "OSPF", `dmarc` → "Did you mean mac"; see #75 (S7) |
| R1 (Language switch applies at once to painted text, not captions) | src\Ui\FullView.Settings.cs line 111 | `L.Use(Cfg.Lang); Invalidate(true)` while PillButton captions keep the old language (S7) |
| Muse-200 #143 (Import with autostart= opens Startup settings) | src\Ui\FullView.Settings.cs line 551 | `if (k == "autostart") Cfg.SetAutoStart(v == "1")` on any imported value (S7) |
| R1 #262 / #1201 (HWND_TOPMOST on every Present) | src\Ui\Controls.cs line 60 | SetWindowPos(-1) on every widget draw (S7) |
| R3-new #16 (Console example) | knowledge\examples.txt line 852 | Fill removed (fixed), but the example still prints "PC → router 3 ms (192.0.2.1)", which the tool can't print (S7) |
| R1 #1401 (duplicate French key) | knowledge\lang-fr.txt line 983 | now "Play again" (the "Simple" duplicate is gone) (S7) |
| R1 (UrlTools per tool, not per box) | src\Ui\FullView.Toolbox.cs line 275 | the dig / dnscmp DoH case is fixed by the label check; certlive and certcmp are still wrong, see #69 (S7) |
| Muse R2 #45 (dead "no neighbours" message) | src\Ui\FullView.Link.cs lines 91-93 | `if (ls.Wifi) ...; else if (ls.Wifi && ...)` can never run (S8) |
| 1.1.4 05-ui (BootWhen per paint) | src\Ui\FullView.Inside.cs lines 91-100 | Process.GetProcessById(...).StartTime for 15 rows on every list paint (S8) |
| R1 ("never listens" self-test vacuous / pass-on-no-throw) | src\SelfTest.cs lines 229, 238, 239, 240-246 | the never-listens checks run before any view opens; gateway ping, health and destination only print (S8) |
| R1 (hotkey test) | src\SelfTest.cs lines 1170-1175 | only checks that Ctrl+Alt+A is free; skips when another copy runs (S8) |
| R2 #75 ("built from this exact source") | release.ps1 line 104; SECURITY.md ("built ... from the public source"); README line 54 | official builds compile obj\HofKey.cs, so a rebuild never matches (S8) |
| R2 #77 (privacy "stays" list) | docs\privacy.html | Points and air.txt added; games list, game-compare, cert-compare, aaa-runs and overlay.png still missing; see #32 (S8) |
| R1 #213 / R2 #126 (counts) | README, CHANGELOG, THEORY-OF-EVERYTHING, website | new numbers, same class; see #114 (S8) |

## Checked and held up

**Rules**
- Rule 1: every outbound request in the network tools comes from a pressed tool or a row in About (CompareDns → "Checks you start"; Portal → "Sign-in page check"; Online.* gated by Online.On and the policy), apart from the entries above. The Brain / Link / Capture code sends nothing without a user action. Fun.cs (Therapy, Roast, Letter) makes no network calls. Match path pings are every 15 s, off on Penny, and listed in About.
- Rule 2: no listening sockets in any new code. The single-instance listener now waits 30 s between checks.
- Rule 4: WlanGetProfile never asks for plaintext keys. The RADIUS secret and password are wiped on every path and never written to aaa-runs.txt. Codes and passphrases come back in "k" rows, kept out of the last-result cache (IsSecretRow). ResultText / ResultCsv drop secret rows and secret input labels; tbShown blanks secret boxes; httpreq's auth or cookie headers and body are hidden before tbLast or Copy. Reach.Http hides Set-Cookie values. No HOF key anywhere in the repo, its history or the committed .pyc (strings).
- Rule 5: no SET (0xA3) anywhere. Snmp.Request is called only with 0xA0 (GET) and 0xA5 (GETBULK) (SnmpTools.cs lines 82 and 99; SnmpGear.cs lines 105, 306 and 319); Protocols.cs builds only a GET; the 0xA7 in Lab.cs is a v2 trap sent by Traffic Lab, which writes nothing on a device.
- Rule 6: every policy key only narrows; mode accepts simple only; history_days is clamped to 1-180; at most 64 K characters and 2,000 lines are read; both files are read and the strictest value wins. Policy-locked Settings rows (Mode, Internet and DNS, BGP and its timer, the four Gamer rows, Live capture) get a no-op setter and the "Set by your IT team" help; preset cards re-apply Policy.Apply. SpeedTest honours Policy.NoSpeed for test, list and check. The company / policy gates for DHCP and router probes in SecChecks are in place. Lab.Allowed honours Policy.NoLab before the tick.
- Rule 7: the Gamer loop idles at 5 s with nothing watched and uses Clock.Mono; Sparks stop their timer when empty; the play head and translation repaint 4 times a second; the stream overlay makes fonts once, re-encodes the PNG only when its key changes and writes it only when the bytes change; Replay, week and anatomy bitmaps are disposed when their key changes; the DB focus mark runs only when focused with cues. Mood costs a few small arrays per card draw.
- Rule 8: no forbidden-vendor terms in src\ or knowledge\ (VPRN gone; SR OS, SAP, SDP, MD-CLI, 7750, VPLS, Alcatel, TiMOS: 0 hits). The only hit in the repository is #33.
- Rule 9: Ctrl+C, the Copy link, Save, Compare and tbLast all refuse example rows (IsExample checks at Toolbox lines 331, 414, 444, 995-997; tbLast is only written by real runs); example rows carry no row actions apart from the banner's exfill; example days never pay page.replay (`!replayExample`). The example registry matches the tool registry (163 tools, 164 examples incl. page.gamer, no stray or duplicate ids, levels -1..3); every Fill value is documentation-range, example.*, a calculator value or a local path; secret boxes are skipped by Fill.
- Rule 10: everything earnable is 806 (359 items = 791, plus 5 replays × 3), under Max 900 = hof_verify MAX_POINTS; AwardAt is 500 in both; retired "finddev" still counts; the replay rule uses the boot ID and the uptime check; Check yourself marks tried even with Points off; points earned in paint are guarded by Cfg.Points and award once; a failed NAT trace no longer awards.

**Files from other people and parsers**
- Pcap / pcapng: 200,000 mutated files (byte flips, magic values, 0 / 0x7FFFFFFF / 0xFFFFFFFF lengths, truncation; seeds were the 9 sample captures plus a pcapng conversion with if_tsresol=9, EPB and SPB) through Pcap.Parse, DhcpRows, RaRows, EapRows and IcmpRowsFromPackets: 0 exceptions, none over 1 s; the pcapng conversion parses to the same packet counts.
- SNMP: 200,000 mutated v2c replies through Parse2 + Text + Tables and 60,000 mutated v3 messages (noAuth, SHA, SHA-256 + AES) through Parse3: 0 exceptions, ≤ 8 ms each; multi-byte lengths, truncation, 0x84 lengths and oversized integers rejected. Hostile agent: OID loops and backwards OIDs stop with a Partial row; a wrong request-id then the right one is handled; a spoofed source port is dropped by the connected socket; empty GETBULK, mid-walk column jumps and 2,000 × 25-value bulk answers (1.8 s, 36 MB RSS) are fine. 32-bit wrap on a known-speed port is right and a counter reset shows "reset" (round-2 #26 fixed); Interfaces deltas show "n/a", never 0, for unread columns. The v3 key stretch runs once per engine.
- Network parsers (60,000 inputs each): ParseStun, ParseVarbinds, SnmpCheck, DhcpText 119/121 (pointer loops capped), LdapDone, Json.Parse (depth 64, list cap), Protocols.ReplyOk and Toolkit.Parse (≥ 12-byte inputs): no exceptions. DNS Name() caps at 20 jumps. Icmp.Resolve4 with 31 hostile inputs: no exception.
- iperf ParseOpts rejects or lists `-b 1e30`, `-b -5M`, `-b NaN`, `-l 0`, `-t 99999999999`; -b ≤ 10 Gb/s, -w ≤ 16 MB, -l ≤ 1 MB (UDP ≤ 65507); the results JSON is capped at 1 MB with depth-limited parsing. Reach.Http: 64 KB body, 20 s, Stop honoured, CR/LF/NUL headers refused. Mail lines 1 KB / 20 s / Stop; Lab.Tcp 4 KB / 3 s; RDP 64 B / 3 s; LDAP 16 KB / 4 s.
- Speed test: redirects and non-200/206 answers refused; caps 400 MB / 100 MB and 8 s; secs ≥ 0.5; decimal Mbps / MB throughout; a stopped upload isn't saved; a ping lost under load counts as 1000 ms.
- Report JSON escaping handles quotes, backslashes and control characters; exit 4 for a missing / bad / non-.json name, 3 for any exception; --seconds clamped to 5-120.
- Settings: all 46 keys written by Cfg.Lines() come back through Cfg.Read; every Settings option array lines up with what Cfg.Read accepts; saved preset=engineer opens as Balanced with ApplyPreset at load.

**Crypto and certificates**
- NewCode is unbiased (248 = 8 × 31, 118.9 bits). Iterations clamped to 100k-2M. EcKeys KDF = SHA-384(label|eph|recip|Z), pad = SHA-512(secret|"wrap"): encrypt and decrypt match each other and the notes (the "wrap" suffix isn't documented). The ephemeral key header is validated (ECK3/48); 1-20 recipients enforced on both sides. The HMAC covers the header and is compared in constant time; a padding error folds into the MAC failure (no oracle). FreeName strips paths, bidi and format characters and device names. Temp .part files are tracked and swept. (P-384 key agreement itself ran only through the CNG stub.)
- CertChain.Walk: wrong order, extra certificates, a non-CA signer (red), not-yet-valid and expired leaves (with the clock hint), an expired intermediate next to the renewed one (the valid one is picked), self-signed, and the loop guard all judged right. HTTPS check against a TLS 1.2 full chain, leaf only, expired leaf, TLS 1.3, a closed port and a URL with a path: all reported correctly. Raw hello / Certificate parsing is bounds-checked (18 KB records, 128 KB, 10 certificates).
- SecChecks: LM interception roots flagged; Antivirus productState decode correct; About lists cloudflare-dns.com and www.microsoft.com; NewDevices locks the writer's object. EngPack: CertCompare names the subject and keeps host:port (R1 #238 fixed); HowFar no longer green for a failed certificate; 802.1X judges every frame.
- Protocol correctness: RADIUS Response Authenticator and Message-Authenticator (RFC 2865 §3 / RFC 3579 §3.2); DNSKEY key tag (RFC 4034 App. B); SVCB / HTTPS keys; NTP offset / delay, origin check, KoD and alarm; TWAMP offsets (RFC 5357); RDP X.224 + RDP_NEG_REQ; LDAP rootDSE request; NetFlow v5 layout; SNMP INTEGER sign handling; UDP MTU QUIC VN / DNS padding sizes (RFC 9000 §6, RFC 7830).

**Hall of Fame and CI**
- Claim code: HMAC-SHA256 over "v1|name|total|date", 16 base-32 characters = 80 bits (about 38 million years at 1e9 guesses a second). C# HallOfFame.Code / CleanName agree with hof_verify on all 4 self-test vectors (astral, Devanagari, decomposed accents) and on 349 random names from every plane; truncation to 32 code points is identical and idempotent; AskName MaxLength 40 then CleanName gives the card, the copied text and the code the same name; no newline can reach GITHUB_OUTPUT.
- hof_verify: 99999 refused (500-900); HTML, RTL overrides and control characters stripped; a 65 KB body handled in milliseconds; a duplicated `### Name` takes the first; a junk author is blanked; `ref` must match the repo's issue URL; handles must fit each site's regex and the page rebuilds the link (javascript: never becomes a link); codes de-duplicated; same-name detection across case and width; Arabic-Indic digits in points harmless.
- hall-of-fame.html writes every field with textContent (no XSS from an `<img onerror>` name); mixed RTL / Latin names lay out; 32-character names wrap; no horizontal scroll at 360 px. Website at 360 / 768 / 1366, light and dark: no console or page errors, no horizontal overflow, no broken anchors, every local link and asset exists; the update checker only echoes a version matching `^\d{1,3}(\.\d{1,4}){1,3}$`.
- Workflows: no `${{ }}` reaches a `run:` line; issue data goes through env vars; every action pinned to a SHA; the build job is `contents: read` with `persist-credentials: false`; only the release job can write and it checks the tag and RELEASE-NOTES-<ver>; a tag dispatch builds that tag (R1 #29 fixed); no concurrency group, so no silent cancel (R2 #50 fixed); retry-on-conflict re-runs the verify.
- Self-test count: 71 T() registrations (70 without --samples), matching "71 checks"; Security count 42 matches; jobs per minute penny 8, balanced 67, gamer 71 plus game pings, matching "8·67·~100".

**Windows structures and Wi-Fi**
- WLAN_INTERFACE_INFO (532 bytes, state at +528), WLAN_CONNECTION_ATTRIBUTES (SSID 520/524, BSSID 560, signal 576, rx kbps 580, size ≥ 588), WLAN_BSS_ENTRY (360 bytes; RSSI 56, centre frequency at 92, capability 88, IE offset / size 352/356), opcodes 7 and 8: same on x86, x64 and ARM64. WlanFreeMemory on every path; the handle closed in finally; no notification callbacks; rc 5 maps to the location block; units match netsh. Lowercase BSSID and state strings match NormState, so drop / roam / channel events work with either source.
- Frequency → band / channel (2412-2472 → 1-13, 2484 → 14, 5160-5885 → 32-177, 5935 → 6 GHz ch 2, 5955 → 1, 7115 → 233); HT40, VHT 80/160 (CCFS1 and old width 2/3), 80+80 → 80, HE 6 GHz widths all right; RSN parsing bounds-safe. AirLog.Block 5 GHz and 6 GHz blocks correct. ActiveBlock picks the connected adapter's block with header and "Hosted network" blocks, Turkish "%88" and NBSP / NNBSP before %.
- Native: DNS_RECORD offsets (2P / 2P+16), job-object structs, MEMORYSTATUSEX, SYSTEM_POWER_STATUS, token elevation (class 18), TCP/UDP owner-PID row sizes (24/56, 12/28), ICMP_ECHO_REPLY options at 16+IntPtr.Size, ICMPV6_ECHO_REPLY address at 6 / status at 28, MIB_IPFORWARDROW 56-byte stride, DHCPAPI_PARAMS 32/20-byte stride, DEVPKEY GUIDs / PIDs, ENUM_SERVICE_STATUS_PROCESS stride 56/44.
- Portable detection distrusts only the Downloads and Desktop folders themselves, caches the result, and a read-only portable copy falls back to AppData. QR versions 1-10 at level L match ISO/IEC 18004. SystemEvents are subscribed on the UI thread; Quit unhooks every static event, stops the monitor and game threads and runs CleanCaptures.

**Brain, capture and investigations**
- Brain: per-minute averages over real samples only; flush stamped with its own minute (sleep-safe); Bloat only on new gateway samples; one long-lived PC-jobs thread; Baseline never learns silent minutes and resets per network; NaN round-trips through baselines.txt. Outages.Pair handles network change, switch-off and DST fall-back. Alerts cooldowns per kind; Crash.Allowed parses host and path exactly; IssueUrl stays under 7,000 characters. Mood is text only and changes no verdict, alert, probe or pace; Serious hides it.
- RTP: clean wrap 0%, 1% loss across a wrap → 1.20% (exact), SSRC change → separate streams, 90 kHz video gets its clock (R2 muse #23 fixed), 32-bit timestamp wrap adds no jitter spike, Opus jitter converges to RFC 3550. Journey: BPF built only from a parsed IP and integer ports; Rewind reads at most two ring files ≤ 32 MB; Stop kills dumpcap within 500 ms; payloads stripped except ICMP; resend / keepalive / dup-ACK marks match RFC 9293. LiveCapture restarts extras only on tshark's field complaints; a stale reader can't stop its replacement; the 300 MB restart can't throw out of Snapshot; percentages use one culture for write and parse.
- ProcTree PID-reuse and cycle breaking (R1 #144 fixed); PcHealth "on battery 255%" fixed; Crashes read language-neutral event data; M365.Role bound to the domain (R1 #141 fixed); InRange right for /14 and /15; WebView2 attributed by walking up parents.
- MTR patterns judged right (P1-P5, P7-P10, P12-P14, P17-P20, P26, P28-P30): clean path, loss carried from the router, rate-limited middle hop, real loss from hop 4, destination-only 20%, a never-answering middle hop, silent hops before or inside a loss run, one quiet traced hop (R2 #21 repro), two quiet middle hops with the destination at 0%, loss from a CGNAT hop, the router alone rate-limiting, a run stopped mid-round, growing loss, everything past hop 2 dark (R1 #59 repro), 50% everywhere, only the router answering, the router ignoring MTR.
- ISP report: CGNAT / 172.16 provider hops after a seen gateway count as provider; private hops get no PTR name, are redacted in the HTML and "(private)" in the CSV; the public IP only on "yes"; owner lookups only with Online on; modem redirects stay on the same host.
- What's wrong?: a dead primary DNS with a working secondary, DoH / plain-53 blocked, a full VPN with no local gateway, a captive portal behind a proxy, weak Wi-Fi and 4% loss all judged right; Slow() with amber rows ends amber (R1 #64). Overlap ignores 0/1 + 128/1 (R1 #130); RouteFor with no route is red (#131); Routing listen counts DHCP servers by server-id (#99); TeamsUdp needs two relays (#133); w32tm 0x8007 errors red (#132); 802.1X reads failure IDs only (#103); FindMac / IP-to-MAC say "may be further down" past 2,000 entries. Listen, DomainControllers and GroupPolicy build their commands only from constants, a parsed GUID or the domain name; the temp XML is deleted.

**Gamer, labs, learn and calculators**
- VLSM: 1,500 random cases diffed against Python `ipaddress`, 0 mismatches (round-2 fit, /32, octal and Unicode-digit bugs fixed). Number converter: 2,000 inputs right apart from the ambiguous `0b`. Packet stack: web MSS 1,460, WireGuard 1,440 / 1,420, WireGuard + PPPoE 1,432, IPv4 fragment boundary 2,952 → 2 / 2,953 → 3, efficiency on 1,538 bytes. Bandwidth: `1,500 MB`, `4,7 GB`, speed 0, `Mbit/s`, `MiB/s` right.
- Subnetting quiz: 5,000 seeds right, always 4 distinct options. Address quick-fire: 5,000 addresses in the right category; port answers unique and standard. Detective, heist, dungeon and outage answers checked by hand; every game can be won.
- TCP lab: Reno after RFC 5681, CUBIC after RFC 9438 (K ≈ 200 RTT agrees with a 188-RTT loss period at 685 packets / 40 ms; Reno-friendly α right); Mathis lab formula and its 3.6% / 4.75 Mbps agreement; jitter lab σ = J / 1.128 and 1.2% late at 2× jitter; NAT lab says "Possibly double NAT". Labs say "A simulation with the numbers you pick"; TCP Play stops when you leave.
- Gamer scheduler: MaxPerSecond = MaxServers + 2 reaches every played server; TcpMs disposes its wait handle; game history compressed. Guided.Lag treats an unchecked or silent router as unknown; Picks.ParseWhen reads "9" as 09:00 and never a future day; UpdateBreak needs a real step and skips Defender and USB / HID / storage drivers. Six-month review reads once, runs off the UI thread, and its HTML is encoded and redacted. Game editor reads names before Dispose, cleans quoted exe paths and suffixes id collisions.

**Main UI**
- Every Questions card covers its TbLayout groups and every group belongs to one card; LabToolsMissingGate matches LabGate, SenderGate and Senders. URL stripping leaves dig's and dnscmp's DoH boxes, payload and secret boxes alone, and keeps host:port. Toolbox search survives regex characters and empty queries; "speed", "printer(s)", "cert", "ssl" rank right; French search works on translated titles.
- BeginInvoke after close: every background completion in the UI is wrapped and resets its busy flag in the catch; the watchdog is disposed in FormClosed; the warm-up timer checks IsDisposed; a Toolbox run can't be double-started (Run, Enter, list Enter and RowAction all check tbBusy on the UI thread, and StartTool sets it synchronously). Busy flags clear on every path in the other UI files (link, inside, travel, wars, week, procs, security, walk test).
- PillButton re-announces its name when its text changes; ConnList has arrow, Page, Home, End and Enter keys.
