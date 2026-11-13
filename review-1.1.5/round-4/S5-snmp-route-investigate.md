# S5 - SNMP, routing/path, ISP report, guided investigations (e80412e)

Files read in full: Snmp.cs, SnmpGear.cs, SnmpTools.cs, RoutingTools.cs, PathTools.cs, IspReport.cs, Investigate.cs, Investigate.Whats.cs, WhyFails.cs, HealthCheck.cs, Inspection.cs, WinEvidence.cs, SysTools.cs, VpnAware.cs.
Harnesses (all against build4/r4.dll, via reflection): scratchpad/review4/S5h/ - `mtr.cs` + `mtr.txt` (31 hop patterns), `isp.cs`, `agent.py` (hostile SNMP v2c agent, mode = community string), `snmp.cs`, `fuzz.cs` / `fuzz3.cs` (decoder fuzz), `mib.cs`, `inv.cs` (+ `dns.py`, `portal.py`) for What's wrong? / Slow, `hosts.cs`, `mem.sh` (peak RSS).

### 1. An SNMP walk has no size cap in bytes: a device with long values makes AdeeboMeter use about 450 MB
Severity: slow  
Where: src\Checks\Snmp.cs line 367 (Walk: only a 2,000-value cap), line 192 (ReadPdu keeps every Raw), lines 406-433 (Text returns the whole string), src\Checks\SnmpTools.cs lines 278-317 (Walk/Render keep 2,000 texts and 300 rows of full length)  
How to reproduce: `python3 agent.py 11161`, then run Toolbox > SNMP Walk on 127.0.0.1:11161 with community `huge` and OID 1.3.6.1.4.1.9.1 (each GETBULK answer is one 60,000-byte OCTET STRING). Harness: `bash mem.sh mono snmp.exe r4.dll app 11161 huge/1.3.6.1.4.1.9.1`.  
What happens / what should happen: the walk reads 2,000 values of 60 KB (120 MB of Raw bytes), Tables() turns each into a 60,000-character string (another 240 MB), and 300 of them become rows. Peak RSS was 455 MB (mono). A printer, a buggy switch or a hostile agent can do this. Cap the total bytes kept per walk (for example 4 MB) and cut each value's text (for example to 512 characters) before it goes into a row.  
Confidence: confirmed (ran it: hostile agent, peak RSS 455,288 kB, the walk took 50 s and finished as "2000 values")  
Rule: 7

### 2. ISP report: a game server that ignores ping is reported as 100% loss and blamed on a provider hand-off, and the message to the ISP says so
Severity: wrong result  
Where: src\Checks\IspReport.cs lines 137-145 (`LastOrDefault(h => h.Sent > 0)`) and line 288 (Message); src\Checks\PathTools.cs line 36 (Trace keeps one silent hop at the tail)  
How to reproduce: run Report for my ISP on a game whose servers drop ICMP (common), or type a host that ignores ping. The second route (or 1.1.1.1) answers. Harness `isp.cs` M2: route 1 = 4 clean hops plus the one silent hop that Trace keeps, route 2 = a clean path to a reached destination.  
What happens / what should happen: "SAME TIME, TWO ROUTES" uses the last hop that was *sent* a probe, and that is the silent tail hop, so it shows "loss 100%" at level 2 (red). When the two routes leave the provider at different rDNS domains, "What it means" says "Only the route that leaves at X loses packets - the problem is on that hand-off, not your line. Your provider can steer that traffic another way." The pasted message says "At the same time Game EU-Central had 0.0% loss against 100%." The path itself was clean. Use the last hop that *answered* (Sent > Lost), as `end` does at line 97, and say "doesn't answer ping" instead of giving a loss figure.  
Confidence: confirmed (ran Message() and the row logic with crafted routes; checked the Trace tail rule: 4 answering hops plus silence leaves 5 hops, and the last one is silent)

### 3. MTR / ISP report: a destination that answered the trace and then stopped answering is called "No real loss"
Severity: wrong result  
Where: src\Checks\PathTools.cs lines 136-138 (LossStart needs a later hop), lines 145-151 (MtrVerdict uses the one-argument Answers, so silentEnd is true); src\Checks\IspReport.cs lines 97-106  
How to reproduce: `mtr.txt` P6: hops 1-4 at 0%, the destination has Reached=true from the trace and then loses 120/120 probes. The real-world case is a server that goes down during the run.  
What happens / what should happen: the verdict is "No real loss: any loss at a middle hop doesn't carry on ... The destination doesn't answer pings at all, so this is judged by the last router that does (hop 4)". The ISP report finding is "The destination doesn't answer pings ... No real loss on the path". The destination did answer pings during the trace, so this is a destination outage. P25 and P26 have the same wording problem: "Real loss starts at hop 3 ... 100%. The destination doesn't answer pings at all". When Reached && Lost == Sent, say "the destination stopped answering during the run".  
Confidence: confirmed (ran LossStart / MtrVerdict, patterns P6, P25, P26)

### 4. Steady low loss on every hop (under 3%) is blamed on the destination rate-limiting pings
Severity: wrong result  
Where: src\Checks\PathTools.cs line 136 (`if (l < 3) continue`), line 152; src\Checks\IspReport.cs line 105  
How to reproduce: `mtr.txt` P21 (3 of 120 lost at every hop from the home router to the destination) and P22 (a 1-minute run, 1-2 of 60 lost from hop 2 on).  
What happens / what should happen: LossStart is -1, so the verdict is "Loss at the destination only (3%) - the server itself drops or rate-limits pings". The ISP report says "Only the destination drops some probes ... the path itself is clean." 2-3% loss is what makes calls robotic. With 60-120 probes it can't pass the 3% start threshold, but the end-of-path wording still treats it as the server's fault. When every answering hop from some point on loses at least about 1%, report it as possible loss from that hop, or say "too little loss to locate". Do not call it destination rate-limiting.  
Confidence: confirmed (ran P21, P22)

### 5. ISP report blames the home network for loss inside the provider when the router isn't the first answering hop
Severity: wrong result  
Where: src\Checks\IspReport.cs lines 91-94 (firstIsp), 103-104 (finding), 111 ("Home router" row = first answering hop)  
How to reproduce: harness `isp.cs` (a verbatim copy of lines 92-106 plus the real StartOfLoss):  
I1: the router doesn't answer TTL-exceeded (hop 1 silent), loss starts at the provider's CGNAT hop 100.64.0.1.  
I4: the router answers TTL-exceeded from another of its own addresses (192.168.0.1 while the gateway is 192.168.1.1), loss starts at 100.64.0.1.  
I2: a full-tunnel VPN, loss starts at the VPN server 10.8.0.1.  
What happens / what should happen: when gwIdx is -1, firstIsp becomes the first *public* hop, so every 10.x / 100.64.x provider hop before it counts as "home". The finding is "Loss starts at hop 2 - before the provider (the home network). Fix the home side first." I2 says "(Wi-Fi or the router)" for loss at the VPN server. The round-1 #58 fix only works when the gateway's own address appears in the trace. Fall back to "the first hop after the first answering hop", and recognise the VPN case (VpnUp: name the tunnel instead).  
Confidence: confirmed (ran the copied logic with the real IspReport.Private / StartOfLoss)

### 6. MTR: two routers at the tail that ignore the probes make an earlier rate-limited hop "real loss" (round-2 #21, two-router variant)
Severity: wrong result  
Where: src\Checks\PathTools.cs lines 124-130 (`dark >= 2` makes every traced 100% hop "answering"), lines 136-139  
How to reproduce: `mtr.txt` P15: hop 2 at 5% (rate limiting), hops 3 and 4 answered the trace and then 0 of 120, destination silent. P16: the same without hop 2's loss.  
What happens / what should happen: P15 gives "Real loss starts at hop 2 (203.0.113.1) and carries on to the end - 5% lost from there. ... judged by the last router that does (hop 2)". If the tail really went dark, the loss starts at hop 3 at 100%. If those are quiet routers, there is no loss. Neither reading puts it at hop 2 with 5%. P16 says "Real loss starts at hop 3 ... 100%" with nothing past it answering. Only count dark hops as an outage when something after them, or the reached destination, also went dark, and then start the loss at the first dark hop.  
Confidence: confirmed (ran P15, P16)

### 7. ISP message says "my router answering normally" when the loss starts at the router, and the copied message keeps the private address
Severity: wrong result  
Where: src\Checks\IspReport.cs line 286 (Message), lines 199-200 (`copy:` + msg is not redacted)  
How to reproduce: harness `isp.cs` M1: on a cable, with the home router (hop 1) losing 10% through to the end.  
What happens / what should happen: the message reads "This was measured on a cable, with my router answering normally. The loss starts at hop 1 (192.168.1.1) ...". That contradicts itself, and the LAN address goes into a text meant to be pasted into a support chat. The saved HTML redacts it, but the click-to-copy text doesn't. Only claim the router was normal when its loss was under 1%, and run the message through GameSession.Redact before it is copied.  
Confidence: confirmed (ran Message())

### 8. What's wrong? says "the way in all work" in green when the sign-in page test couldn't run or is switched off
Severity: wrong result  
Where: src\Checks\Investigate.Whats.cs lines 157-159 (level 3 / NotEst only), 166 (weak only looks at level 1), 174 (fixed wording)  
How to reproduce: harness `inv.cs` s3 with `http_proxy=http://127.0.0.1:1` (the web test fails with ConnectFailure, as on a network that needs a proxy). Also s1 with Settings > Sign-in page check Off.  
What happens / what should happen: the observations show "6. The way in: Windows' connectivity test couldn't run (ConnectFailure) - web traffic may need a proxy here" and a not-established note, but the finding is level 0: "Nothing is wrong right now - your PC, the link, your router, the internet, names and the way in all work." When portalErr != "" or the check is off, leave "the way in" out of that sentence and don't give level 0. A failed web test when the rest works should at least be amber.  
Confidence: confirmed (ran Whats with a crafted Monitor)

### 9. What's wrong? calls a router that just ignores ping "weak" and makes that the amber finding
Severity: wrong result  
Where: src\Checks\Investigate.Whats.cs line 94 (gwSilent gives level 1 on "3. Your router"), line 122, lines 166-169  
How to reproduce: a gateway that drops ICMP and has nothing on TCP 53/80/443 (an ASA/FTD, an HSRP address, cloud gateways). The internet and DNS work. Run What's wrong?.  
What happens / what should happen: step 3 is recorded at level 1, so `weak` picks it and the finding is "Nothing is broken, but your router (x) is weak: no answer to ping or TCP. That's where to look if things feel slow or unreliable." On the next line it says "Your router is quiet ... that's a setting on the router, not a fault." Once step 4 succeeds, a silent router should be level 0 or 3, not 1. (The weak path firing on a level-1 router row was confirmed in s5/s6/s9. The container's transparent proxy makes every TCP connect "succeed", so a fully silent gateway couldn't be produced here.)  
Confidence: suspected (read it; the weak-selection path confirmed by running it)

### 10. The hosts-file check only looks at the first 40 entries, so "A website won't open" misses a pin further down
Severity: wrong result  
Where: src\Checks\WhyFails.cs lines 80-84 (Hosts: `lines.Take(40)` limits both display and matching); src\Checks\Investigate.cs line 131  
How to reproduce: a hosts file with 100 ad-block lines and then `10.9.9.9 intranet.example.com`. Harness `hosts.cs` (run in a folder holding drivers/etc/hosts).  
What happens / what should happen: rows=40, pins=0. The pin isn't found, so Site() never says "Your hosts file pins ...". Ad-blocking hosts files have thousands of lines, so this is the common case. Match against every line and cap only what is displayed.  
Confidence: confirmed (ran WhyFails.Hosts)

### 11. SNMP Interfaces: a device restart between two runs isn't noticed when the new uptime is higher than the old one, so deltas across the reboot are shown as growth
Severity: wrong result  
Where: src\Checks\SnmpTools.cs line 130 (Changes: reboot only when now.Up < old.Up)  
How to reproduce: `agent.py` mode `ifreboot`: run 1 sees sysUpTime 1 s and ifInErrors 50. The device restarts. Run 2 sees sysUpTime 1 h and ifInErrors 80. Here the two runs were 0 s apart. In real use: run it right after a reboot, then hours later after another reboot.  
What happens / what should happen: "3 ports changed · 3 with growing errors", "+30" on each port, and no "restarted" note. The uptime moved forward 1 hour in 0 s of wall time, which can only be a restart. Also treat it as a reboot when (now.Up - old.Up) / 100 is clearly less than the seconds elapsed between the runs.  
Confidence: confirmed (ran Interfaces twice against the agent)

### 12. "A website won't open" contacts msftconnecttest.com when the Sign-in page check is Off
Severity: wrong result  
Where: src\Checks\Investigate.cs line 133 (WhyFails.Clock() when TLS failed), src\Checks\WhyFails.cs lines 205-216 (no Cfg.Portal check); the same at src\Checks\DestCheck.cs line 303 and CertChain.cs line 329  
How to reproduce: Settings > Privacy > Sign-in page check = Off ("Off: a failed check won't look for a Wi-Fi sign-in page"). Run A website or app won't open on a host whose TLS fails.  
What happens / what should happen: a HEAD request to http://www.msftconnecttest.com/connecttest.txt is sent anyway (through the system proxy) to read the server's Date. PortalTest honours the switch (Whats.cs line 29), but this path doesn't. Skip the clock comparison, or ask first, when Cfg.Portal is off, or list it under its own entry in What leaves your PC.  
Confidence: suspected (read it)  
Rule: 1

### 13. ISP report: the cable-modem page reads and the hop name lookups aren't in "What leaves your PC"
Severity: wrong result  
Where: src\Checks\IspReport.cs lines 79-83 (PTR lookups for up to 60 hop addresses through the user's DNS), lines 244-262 (up to 4 HTTP GETs to 192.168.100.1); src\Ui\FullView.Settings.cs line 319 (LeaveIsp text)  
How to reproduce: read About / Settings > What leaves your PC > Report for my ISP.  
What happens / what should happen: the entry lists only "an MTR to the destinations you chose ... your public IP only if you type yes". Every run also sends reverse-DNS queries for every hop to the configured resolver, and HTTP requests to 192.168.100.1. On a network where 192.168.100.1 isn't a modem, those requests go to the default route or a VPN. Add both to the list.  
Confidence: confirmed (read it; both sends are unconditional in Run)  
Rule: 1

### 14. One crafted MIB file in the MIB folder makes the index build run for hours (quadratic regex)
Severity: slow  
Where: src\Checks\Snmp.cs line 616 (RxTc `::=\s*TEXTUAL-CONVENTION.*?SYNTAX`, Singleline), lines 537-545 (EnsureBuilt holds `lock(k)` with no stop or time limit)  
How to reproduce: a .mib file of `Tn ::= TEXTUAL-CONVENTION STATUS current DESCRIPTION "x"` lines with no SYNTAX. Harness `mib.cs`.  
What happens / what should happen: 2,000 entries (121 KB) took 4.9 s, 4,000 took 20.3 s and 8,000 (487 KB) took 81.7 s, so time grows with the square of the size. An allowed 8 MB file would run for hours (mono times; .NET's backtracking regex is the same order). Walk and Search show "Reading your MIB files (once)" with no way to stop, hold the lock so every later MIB walk or search waits, and rebuild again next time because the index file is never written. Bound the lazy scan (for example `[\s\S]{0,4000}?SYNTAX`), use a regex match timeout, and check run.Stop.  
Confidence: confirmed (ran MibIndex.BuildLines on mono)

### 15. SNMP walk: an agent that answers with the same OID again ends the walk silently, shown as complete
Severity: wrong result  
Where: src\Checks\Snmp.cs lines 381 and 387 (`continue` on an equal OID, then `if (!moved) break` with error "")  
How to reproduce: `agent.py` mode `same` (GETBULK from x.1 answers x.1). `mono snmp.exe r4.dll app 11161 same/1.3.6.1.2.1.2.2.1.2`.  
What happens / what should happen: "1 values · 1 table", no Partial row, so it reads as the whole table. The loop and backwards cases do get "the device's answers stopped moving forward". A reply that doesn't move should set the same error.  
Confidence: confirmed (ran it)

### 16. SNMP: any stray datagram from the device ends the request with an error, although the code says stray packets are skipped
Severity: wrong result  
Where: src\Checks\Snmp.cs line 137 (accept rejects only "a reply to a different request"), lines 110-111 (comment), lines 148 and 154 (v3 has no accept filter at all)  
How to reproduce: `agent.py` modes `garbagefirst` (sends 6 junk bytes, then the real reply) and `wrongcommfirst` (a reply with another community, then the real one).  
What happens / what should happen: "not an SNMP reply" and "a reply for a different community". The real answer arriving right after is thrown away, and the view fails. A forged packet with the device's address and port can make any walk fail. Skip every datagram that doesn't parse as our reply, and add the same filter to v3 (message ID).  
Confidence: confirmed (ran it; wrong request-id-then-right is skipped correctly, and a reply from another port is dropped by the connected socket)

### 17. An SNMP community with any non-ASCII character never works
Severity: wrong result  
Where: src\Checks\Snmp.cs line 136 (sent as UTF-8), line 167 (compared with Encoding.ASCII.GetString)  
How to reproduce: community `café` against `agent.py` (it echoes the community bytes).  
What happens / what should happen: "a reply for a different community" on every request. Compare the raw bytes.  
Confidence: confirmed (ran it)

### 18. SNMP Traffic on 32-bit counters: no "Busiest %", a phantom multi-gigabit rate when ifSpeed is missing, and a reboot near the counter top read as traffic
Severity: wrong result  
Where: src\Checks\SnmpTools.cs line 188 (Sp from ifHighSpeed only), lines 192-193, line 185 (unknown speed accepts any wrap up to 10 Gb/s); Traffic never reads sysUpTime  
How to reproduce: `agent.py` modes `t32wrap` (100 Mb/s port, ifSpeed given), `t32nospeed`, `t32reboottop`, using `mono snmp.exe r4.dll app 11161 t32wrap/traffic ...`.  
What happens / what should happen: t32wrap gives a correct 47.9 Mb/s, but Busiest % is "-" and a port near 100% is never highlighted, although ifSpeed was walked (`ls`). t32nospeed shows "2.07 Gb/s" for a counter reset. t32reboottop shows "10 kb/s" for a reboot. Use `ls` for Sp, and compare sysUpTime before and after (it is one GET).  
Confidence: confirmed (ran it)

### 19. "The internet is slow" with the router check off ends with green "router no answer"
Severity: cosmetic  
Where: src\Checks\Investigate.cs line 102 (F(NaN) = "no answer")  
How to reproduce: harness `inv.cs` s12 (GwPing empty, internet 20 ms).  
What happens / what should happen: "Nothing on the path is slow right now: router no answer, internet 20 ms." It should say "router not measured".  
Confidence: confirmed (ran it)

### 20. Inspection treats any root in a "Third-Party Root CAs" (AuthRoot) store as public, including one an admin or GPO put there
Severity: wrong result  
Where: src\Checks\Inspection.cs lines 170-179 (OnPublicList returns true on any AuthRoot thumbprint match before the Mozilla / Windows-list check)  
How to reproduce: deploy an inspection root through Group Policy > Public Key Policies > Third-Party Root Certification Authorities (it lands in the machine AuthRoot group-policy store), then check an HTTPS site that it re-signs.  
What happens / what should happen: InspectedBy returns "", so no "Certificate re-signed by". AuthRoot is not only Microsoft's own list. Apply the same thumbprint test (Public or InWindowsList) to roots found there.  
Confidence: suspected (read it)

### 21. UPS (APC MIB): output "sleeping", "rebooting" or "sleeping until power returns" is shown green as "other"
Severity: wrong result  
Where: src\Checks\SnmpGear.cs line 324 (os 5, 8, 11 fall through to src = 1), line 328  
How to reproduce: an APC card reporting upsBasicOutputStatus 5, 8 or 11.  
What happens / what should happen: "Power: other" at level 0 while the load has no output. Map these to "OFF - no output" (level 2).  
Confidence: suspected (read it; APC PowerNet-MIB values)

### 22. Network sign-in check says "no failed sign-ins (Wi-Fi or wired)" in green when the Wi-Fi log couldn't be read
Severity: wrong result  
Where: src\Checks\WinEvidence.cs line 100 (the error is shown only when both e1 and e2 are set)  
How to reproduce: a PC where the WLAN-AutoConfig/Operational log can't be read but the Wired log can (or the reverse).  
What happens / what should happen: green "no failed sign-ins in the last 7 days (Wi-Fi or wired 802.1X)". Name the log that couldn't be read.  
Confidence: suspected (read it)

## Still present from earlier rounds

| earlier id | where now | one-line evidence |
|---|---|---|
| R1 #50 (Slow part) | Checks\Investigate.cs lines 88-90 | inv.cs s10: router ring all NaN, internet 20 ms, gives level 2 "Your router isn't answering at all - the problem is right next to you" (confirmed) |
| R1 #154 / R2 #28 (What's wrong? on IPv6-only) | Checks\Investigate.Whats.cs lines 57, 88, 95 (reads Gateway only, never Gateway6) | inv.cs s2: Gateway "" and Gateway6 fe80::1 give level 2 "Windows has no default gateway ... Usually DHCP didn't finish" (confirmed) |
| R1 #49 (RoutingTools part) | Checks\RoutingTools.cs lines 36-48 GwTick | lastGw/lastMac are keyed only by the gateway IP, so another network with the same gateway IP gives "The gateway moved to another device" (read) |
| R2 #21 | Checks\PathTools.cs lines 124-130 | the single quiet router is fixed (P13 is -1); the two-router variant is still wrong, see #6 (confirmed) |
| R1 #58 | Checks\IspReport.cs lines 92-94 | fixed only when the gateway shows in the trace; silent router, other router address and VPN still give "home", see #5 (confirmed) |
| R1 #96 | Checks\Snmp.cs line 381 | loop and backwards are fixed; the "same OID" agent still finishes as complete, see #15 (confirmed) |

## Checked and held up

- Rule 5: no SET (0xA3) anywhere. Snmp.Request is called only with 0xA0 (GET) and 0xA5 (GETBULK), in SnmpTools.cs lines 82 and 99 and SnmpGear.cs lines 105, 306 and 319. Protocols.cs builds only a GET. The 0xA7 in Lab.cs is a v2 trap sent by Traffic Lab, which writes nothing on a device.
- Decoder: 200,000 mutated v2c replies through Parse2 + Text + Tables, and 60,000 mutated v3 messages (noAuth, SHA auth, SHA-256 + AES) through Parse3: 0 exceptions, at most 8 ms each. Multi-byte lengths, truncation, 0x84 lengths and oversized integers are all rejected cleanly.
- Hostile agent: an OID loop (1..5,1..) and backwards OIDs stop with a Partial row. A wrong request-id followed by the right one: the wrong one is skipped. A reply from a spoofed source port is dropped (connected socket), then "no answer in 3 s". An empty GETBULK gives "nothing under ifDescr". A mid-walk jump to another column ends cleanly. 2,000 × 25-value bulk answers in 1.8 s with 36 MB RSS.
- 32-bit Traffic: a real wrap at 48 Mb/s on a 100 Mb/s port is computed correctly. A counter reset (3e9 to 1e6) on a known-speed port shows "reset" (round-2 #26 is fixed). Interfaces deltas show "n/a", never 0, for columns that weren't read.
- MTR patterns that judge right: clean path; loss carried from the router; rate-limited middle hop; real loss from hop 4; destination-only 20%; a never-answering hop in the middle; silent hops before or inside a loss run; rate limit at hop 2 with real loss from hop 5; one quiet traced hop (round-2 #21 repro); two quiet middle hops with the destination at 0%; loss starting at a CGNAT hop; the router alone rate-limiting; a run stopped mid-round; growing loss; everything past hop 2 dark (round-1 #59 repro); 50% everywhere; only the router answers; the router itself ignoring MTR (P1-P5, P7-P10, P12-P14, P17-P20, P26, P28-P30).
- ISP report: a 172.16 / CGNAT provider after a seen gateway counts as provider (I3). Private hops: no PTR name, redacted in the HTML (GameSession.Redact) and "(private)" in the CSV. The public IP is fetched only on "yes". Owner lookups only with Online on. Modem redirects stay on the same host.
- What's wrong?: a dead primary DNS with a working secondary (s5) and DoH / plain-53 blocked (s6, "through Windows") pass step 5. A full VPN with no local gateway continues (s8). A captive portal behind a proxy is caught (`portal.py`). Weak Wi-Fi (s7) and 4% loss (s9) end amber, never all clear. Slow() with amber rows now ends amber (round-1 #64).
- Overlap ignores 0/1 + 128/1 full tunnels (round-1 #130). RouteFor with no route is red (#131). Routing listen counts DHCP servers by server-id (#99). TeamsUdp only reports a NAT type with two relays (#133). w32tm errors with 0x8007 are red (#132). The 802.1X check reads failure IDs only (#103). The SNMP v3 key stretch runs once per engine (round-2 #105). FindMac / IP-to-MAC say "may be further down" past 2,000 entries (#54, round-2 #61).
- Listen builds the tshark command from a constant filter and a parsed GUID, so nothing can be injected. DomainControllers / GroupPolicy pass only the domain name or a temp GUID path, and the temp XML is deleted.
