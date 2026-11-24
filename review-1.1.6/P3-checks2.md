# P3-checks2: release check of src/Checks/PathTools.cs .. WinEvidence.cs (1.1.6 @ 999a100)

Every line of the 24 files was read (about 8,000 lines). Harnesses are in `scratchpad/review5/P3-checks2/`:
- `mtr.cs` + `mtr5.txt`: odd MTR hop patterns
- `h1.cs`: NTP, dig, the certificate deadline, UpdateBreak and TLS, run against the local fakes in `srv.py`
- `h2.cs`: a fuzz of every binary and text parser, 20,000 inputs each
- `ov*.cs`: the profile-date arithmetic
- `sub.cs`: subnet and Unicode-digit inputs

Mono build r6.dll. For the TLS test, /etc/hosts sent www.microsoft.com to 127.0.0.1 for the run only, and was restored afterwards.

### 1. Policy `live_capture = off` doesn't stop "Routing protocols on my segment" or "Announcements": both still run tshark packet capture
Severity: security  
Where: src\Checks\RoutingTools.cs line 140-163 (Listen)  
How to reproduce: Put `live_capture = off` in AdeeboMeter.policy on a PC with Wireshark and Npcap installed. Open Toolbox › Path & routing › "Routing protocols on my segment", or "Announcements (mDNS, SSDP, LLMNR, NetBIOS)", and press Run.  
What happens / what should happen: Listen only checks `LiveCapture.Available`, which is Npcap plus the tshark and dumpcap files. It never checks `Policy.NoCapture`. It then runs `tshark -i \Device\NPF_{..}` for up to 120 s. Every other capture path (LiveCapture.Start, Packets, Journey, Gamer) refuses with "turned off by your IT team". IT believes capture is off, but it isn't. Listen should return the same refusal when `Policy.NoCapture` is set.  
Confidence: suspected (read it: every capture entry point was traced. Listen is the only one without the check.)

### 2. Security checkup "Encrypted traffic inspected?" is green for a forged, untrusted certificate (a real man-in-the-middle)
Severity: security  
Where: src\Checks\SecChecks.cs line 405-430 (TlsIntercept)  
How to reproduce: With online lookups on, let something on the path answer www.microsoft.com:443 with a self-signed or otherwise untrusted certificate. A hotel captive box or a malicious hotspot does this. Run the Security checkup.  
What happens / what should happen: The validation callback returns `true` and never reads `SslPolicyErrors`. `Inspection.InspectedBy` returns "" for a chain that doesn't reach a trusted root ("can't tell"). The code reads that "" as "known" and shows green: "no - www.microsoft.com's certificate comes from a public authority". An untrusted certificate is the worst case and should be red. The "Is this Wi-Fi OK?" check (Picks.Hotel) gets this right by checking `errs`.  
Confidence: confirmed (h1.exe `tls`: a self-signed www.microsoft.com on 127.0.0.1:443 gave `[0 | Encrypted traffic inspected? | no - ... comes from a public authority]`)

### 3. "What changed on my server" and "Certificate calendar" can hang for ever: the TLS handshake has no deadline, and Stop doesn't end it
Severity: slow  
Where: src\Checks\Sectors2.cs line 70-77 (Cert). Called from ServerChange line 38 and from CertCalendar (Sectors3.cs line 193).  
How to reproduce: Target a port that accepts TCP and then waits. Examples: `myhost:5432` (PostgreSQL waits for a startup packet), a stuck proxy, or any listener that reads first. Run "What changed on my server", or "Certificate calendar" with that host listed. The calendar also adds every host from the board watch list and from `data\servers\*.txt` by itself.  
What happens / what should happen: `ssl.AuthenticateAsClient(host)` runs synchronously on a NetworkStream with no ReadTimeout. It blocks until the server closes the connection, which can be never. The Toolbox shows the tool as busy, and Stop is only checked between hosts. The handshake should use the 8 s deadline pattern from `Protocols.Tls`, which closes the socket under it.  
Confidence: confirmed (h1.exe `cert`: Sectors.Cert against a server that accepts and stays silent was "STILL BLOCKED after 30 s")

### 4. [day 1] Market-open check never reads the clock, so it can never say "Connection ready"
Severity: wrong result  
Where: src\Checks\Sectors.cs line 77 (MarketOpen)  
How to reproduce: Run Toolbox › Market-open check on any healthy connection.  
What happens / what should happen: It looks for the NTP row with `r[1] == "Your clock"`. Protocols.Ntp names that row "Your clock vs this server (an estimate from one server)", so the lookup always finds nothing. Every run gets "Clock: couldn't ask the time server - unchecked" plus one warning, so the verdict is always amber "CAREFUL - 1 thing to fix first", even when the clock answered fine. The visuals code already matches with StartsWith. Use StartsWith("Your clock") here too.  
Confidence: confirmed (h1.exe `ntp` against a local NTP responder: the row is named "Your clock vs this server ...", and the MarketOpen match is False)

### 5. "What changed on my server" never records the MX or NS records, so a mail or DNS move is never reported
Severity: wrong result  
Where: src\Checks\Sectors2.cs line 32 (ServerChange)  
How to reproduce: Run it on a domain, change the domain's MX record, then run it again.  
What happens / what should happen: It keeps Dig rows whose `r[1] == "MX"` or `"NS"`. Toolkit.Dig names those rows `r.Type + "  " + r.Name`, for example "MX  example.com", so nothing matches. The snapshot has no "DNS MX" or "DNS NS" keys at all, but the tool says it snapshots DNS. A fixed match would hit a second problem: `r[2]` holds "10 mx.example.com   (TTL 300 s)", and the TTL counts down, so every run would show a fake change. Strip the "(TTL ...)" part before comparing.  
Confidence: confirmed (h1.exe `dig` against a local DNS server: rows are `MX  example.com` and `NS  example.com`, and ServerChange matches 0)

### 6. [day 1] "What did Windows Update break?" with little History says "A small change after <update>" and tells you to uninstall it, and that row also hides a real strong match
Severity: wrong result  
Where: src\Checks\UpdateBreak.cs line 191 (Compare), line 230-233 (Run)  
How to reproduce: Use a fresh install, or any update installed today or yesterday so there is no "after" data yet. Run the tool.  
What happens / what should happen: When there isn't enough History, Compare returns `Lv = 3`, the "can't tell" level. Run then picks the worst finding with `fs.Where(f => f.Lv >= 1).OrderByDescending(f => f.Lv)`, so this Lv 3 row ranks above a real Lv 2 "Strong" finding. The verdict reads "A small change after KB… (Mon 23 Nov): not enough History around it to compare (0 day(s) before, 0 after)". It adds "To test it: Settings › Windows Update › Update history › Uninstall updates". On day 1 this tells a new user to uninstall an update based on no data. With two weeks of History, any update from today hides the real match from last week. Lv 3 should be left out of the worst-finding pick.  
Confidence: confirmed (h1.exe `upd`: Compare with no History returns Lv=3. The ordering is from reading Run.)

### 7. "Is this Wi-Fi OK to use?" skips the "Private network" warning about two thirds of the time (the date arithmetic overflows)
Severity: wrong result  
Where: src\Checks\Picks.cs line 190-194 (ProfileCategory)  
How to reproduce: Join a hotel Wi-Fi that Windows marked Private, then run "Is this Wi-Fi OK to use? (hotel, cafe)".  
What happens / what should happen: The code builds `when = when*10000 + field` from six SYSTEMTIME fields, which needs about 10^23 and so overflows a long. For about 67% of dates the result is negative. `bestWhen` starts at -1 and the test is `when > bestWhen`, so a negative date never counts. ProfileCategory then returns "", the red "a Private (home) network - other guests can find this PC" row is dropped, and the verdict can turn green. When two profiles share a name, the "most recent" pick is also arbitrary. Compare DateTime values instead, as WinEvidence.All does.  
Confidence: confirmed (ov.exe / ov2.exe: 2,503 of 3,755 sampled times in 2025-2027 give a negative value, for example 2026-11-24 09:00 → -480137551105398528. The registry part is from reading.)

### 8. HTTP / TLS inspector shows a green "Certificate" row when the handshake failed because the certificate isn't trusted
Severity: wrong result  
Where: src\Checks\Toolkit.cs line 788-797 (Tls)  
How to reproduce: Run Toolbox › HTTP / TLS inspector on a site with a self-signed, name-mismatched or expired-chain certificate.  
What happens / what should happen: The TLS 1.2 and 1.3 rows go grey (level 3), either "certificate not trusted (...)" or, on mono, "not offered". The Certificate row's colour depends only on days left, so it is green (0), and the only coloured verdict on the page is green. Only its advice text says "Windows doesn't trust it". The row should be red when `errs != None`, and a trust failure shouldn't be shown as a grey "not offered".  
Confidence: confirmed (h1.exe `tls`: `[3 | TLS 1.2 | not offered (Authentication failed...)]` and `[0 | Certificate | www.microsoft.com · from www.microsoft.com · expires ...]` for a self-signed cert. On .NET Framework the TLS row text differs, but the level is the same.)

### 9. Security checkup "Certificate expiring" doesn't check the certificate is trusted or for the right name
Severity: wrong result  
Where: src\Checks\SecChecks.cs line 531-557 (SiteCert, CertExpiry)  
How to reproduce: Type a site that serves the hosting company's default certificate (another name), or a self-signed one, then run the checkup.  
What happens / what should happen: The callback accepts anything (`return true`), and the row is green with "N days left". A site whose visitors get a browser warning should be red. Pass `SslPolicyErrors` out of the callback and use it in the verdict.  
Confidence: confirmed (h1.exe `tls`: a self-signed cert got "29 days left" at level 1, amber only because it was under 30 days. A cert valid longer reads green.)

### 10. MTR: if every hop goes dark after the trace (Wi-Fi or internet dropped for the whole run), the verdict blames a firewall or VPN
Severity: wrong result  
Where: src\Checks\PathTools.cs line 150 (MtrVerdict)  
How to reproduce: Run MTR, and pull the cable or Wi-Fi right after the trace finishes. Pattern Q12: `192.168.1.1:120:120 100.64.0.1:120:120 dest:120:120:R`  
What happens / what should happen: `last = hops.LastOrDefault(Answers)` uses the one-hop Answers check, which ignores DestDark, so it comes back null. The verdict is "No hop answered - probes are blocked on the way out (a firewall or VPN)", even though every hop answered the trace and LossStart itself returns hop 1. It should say the whole path, from your router on, went dark during the run.  
Confidence: confirmed (mtr.exe `mtr5.txt` Q12)

### 11. MTR: a lossy last router before a destination that ignores ping is called "No real loss"
Severity: wrong result  
Where: src\Checks\PathTools.cs line 141 and 163 (LossStart, MtrVerdict)  
How to reproduce: Q1: hops 1-3 at 0%, hop 4 at 40%, destination silent. Q8: only the home router answers, at 30% loss, and everything after it is silent.  
What happens / what should happen: Both give "No real loss: any loss at a middle hop doesn't carry on - that router just ignores some pings (normal)". With nothing answering after it, there's no way to tell. In Q8, 30% loss to your own router is most likely the Wi-Fi itself. It should say "can't tell: the last router that answers loses N%, and nothing past it answers", not show a green all-clear.  
Confidence: confirmed (mtr.exe Q1 and Q8)

### 12. MTR: a destination that answered the trace and then lost 119 of 120 pings is "the server rate-limits pings"
Severity: wrong result  
Where: src\Checks\PathTools.cs line 133 (DestDark) and line 164  
How to reproduce: Q13: `192.168.1.1:120:0 100.64.0.1:120:0 dest:120:119:R`  
What happens / what should happen: DestDark needs every probe lost (`Lost >= Sent`), so one stray reply flips the verdict to "Loss at the destination only (99%) - the server itself drops or rate-limits pings". At one ping a second, 99% isn't rate limiting: the server, or the way to it, went down. This is a variant of round-4 #39. Use a near-total threshold, for example 95% or more.  
Confidence: confirmed (mtr.exe Q13)

### 13. "Overlapping networks" flags a laptop on Wi-Fi and Ethernet to the same home router as a VPN overlap
Severity: wrong result  
Where: src\Checks\PathTools.cs line 226-232 (Overlap)  
How to reproduce: Dock a laptop so Ethernet and Wi-Fi are both connected to the same 192.168.1.0/24, with no VPN. Run Toolbox › Overlapping networks (home vs VPN).  
What happens / what should happen: Any two different adapters with the same network get a level-2 "Overlap" row: "...usually the company servers on the VPN become unreachable from home. The fix is on the home router (change its network...)". This is a normal setup, and the advice is wrong. Only flag a pair where one adapter is a VPN (Item4), or show a neutral row when both are LAN adapters.  
Confidence: suspected (read it)

### 14. Security checkup: two antivirus products both switched off read as grey "that's normal"
Severity: wrong result  
Where: src\Checks\SecChecks.cs line 76-80 (Antivirus)  
How to reproduce: Use a PC where Security Center lists two products and neither is on. For example, Defender real-time protection off plus a registered third-party tool with its real-time shield off.  
What happens / what should happen: Each "off" row gets level 3 when `av.Count > 1`, with "Off because another antivirus is on - that's normal". No row says nothing is protecting the PC. Use 3 only when some other product is on, and give level 2 when none is.  
Confidence: suspected (read it)

### 15. "What did Windows Update break?" says green "nothing was installed in the last 14 days" when the event log couldn't be read
Severity: wrong result  
Where: src\Checks\UpdateBreak.cs line 145-151 (Changes) and 225 (Run)  
How to reproduce: Use a PC where the System log can't be read, for example when policy restricts event logs, which round-4 notes as possible on work PCs.  
What happens / what should happen: Changes discards `err`, and the empty list gives a green verdict: "nothing was installed in the last 14 days - if things got worse, it wasn't Windows Update". It should show the error and stay grey.  
Confidence: suspected (read it)

### 16. The device inventory (every MAC seen on every network) is kept for ever, and turning History off doesn't remove it
Severity: security  
Where: src\Checks\Picks.cs line 243-271 (LoadInv, SeeDevices, SaveInv). Called from src\Brain\Watch.cs line 225.  
How to reproduce: With History on, visit hotels, cafes and offices, then turn History off in Settings.  
What happens / what should happen: `inventory.txt` keeps up to 500 devices per network for every network ever joined, with no age limit and no limit on the number of networks. It is loaded whole into memory at start. Turning History off deletes devices.txt and device-nets.txt, but not inventory.txt, and `history_days` in the policy doesn't trim it either. Other guests' MAC and IP addresses and their seen times stay on disk after the user turned History off. Trim it with History, and delete it when History is switched off.  
Confidence: suspected (read it)  
Rule: 7

### 17. The Service board's background watch and the watched-device pings aren't in "What leaves your PC"
Severity: security  
Where: src\Checks\Sectors3.cs line 108-125 (WatchTick, run every minute from AppCtx), src\Checks\Picks.cs line 329-331 (DevWatch, "a ping a minute"), and the About list in src\Ui\FullView.Settings.cs line 495-530  
How to reproduce: Service board with "15" in the minutes box, or Devices on this network › Watch one. Then read Settings › About › What leaves your PC.  
What happens / what should happen: Both keep sending in the background (TCP handshakes to your hosts every N minutes, while the app runs even from the tray; a ping a minute to each watched device). Neither is listed, although About promises "Every packet it sends is listed". Both are opted into, so the risk is low, but the list is the rule's contract. Add them to the list.  
Confidence: suspected (read it)  
Rule: 1

### 18. Arabic-Indic and other Unicode digits make three inputs fail with "Couldn't finish: Input string was not in a correct format"
Severity: wrong result  
Where: src\Checks\Picks.cs line 60-61 (ParseWhen), src\Checks\Replay.cs line 164-165 (IcmpExplain), src\Checks\Snmp.cs line 84 + 78 (ValidOid, then OidBytes)  
How to reproduce: In Arabic (now shipped), type `١٨:٤٠` in "What changed when it broke?" or `٣/٤` in "ICMP error translator". Or walk OID `1.3.6.1.٦`.  
What happens / what should happen: Regex `\d` matches any Unicode digit, but `int.Parse` or `long.Parse` with InvariantCulture throws. The Toolbox catches it and shows a red "Couldn't finish". Use `[0-9]` in these regexes, or convert digits first. This is the same class as round-4 #60, in new places.  
Confidence: confirmed (h2.exe fuzz: FormatException from ParseWhen in 349 inputs, IcmpExplain in 23 and OidBytes after ValidOid. sub.exe: `IcmpExplain("٣/٤") -> FormatException`)

### 19. Speed test says "up to 500 MB", but one test can use about 1 GB
Severity: cosmetic  
Where: src\Checks\SpeedTest.cs line 264-267 (Phase), and src\Ui\FullView.Speed.cs line 117  
How to reproduce: Run a speed test on a 1-2.5 Gb/s line.  
What happens / what should happen: The cap counts bytes after the warm-up second (400 MB down, 100 MB up), with a hard ceiling at twice that (800 and 200 MB). On 1 Gb/s a download uses about 125 MB of warm-up plus 400 MB. On 2.5 Gb/s it uses about 710 MB. The text "About 20 seconds, up to 500 MB" understates it, which matters on metered or capped lines. Say the real ceiling, or cap the total bytes.  
Confidence: suspected (read it)

### 20. DNS race says "Yours is slower here" when your DNS wasn't in the race
Severity: wrong result  
Where: src\Checks\Picks.cs line 230-232 (DnsRace)  
How to reproduce: On an IPv6-DNS-only network (Monitor keeps only an IPv4 DNS server), or before the first network read fills `DnsServer`, run DNS race.  
What happens / what should happen: `mine` is null, so `mineBest` is false, and the Winner row is amber with "Yours is slower here...". It should say your DNS server isn't known or wasn't tested.  
Confidence: suspected (read it)

### 21. MTR "the path changed ... from A to A"
Severity: cosmetic  
Where: src\Checks\PathTools.cs line 175 (Flags)  
How to reproduce: A hop goes A → B → A during the run (Switches = 2).  
What happens / what should happen: The row reads "from 192.0.2.1 to 192.0.2.1", because `Seen[0]` and `Cur` are the same address. It should name the address it changed through, or say that it switched away and back.  
Confidence: suspected (read it)

## Still present (from round 4)

| Round-4 # | What | Seen here |
|---|---|---|
| 8 | Ping tools skip the one-device guard | `SysTools.Ping6` uses its own `Resolve6` with no `Lab.NotOne`, so `ff02::1` gets 10 pings, 3 big ones (1,452 bytes) and a 30-hop trace to every device. `Reach.NameOf` sends NetBIOS, LLMNR and mDNS to any address typed, broadcast included. The IPv4 ping tools are fixed (Icmp.Resolve4 checks). |
| 55 | IPv4 typed in hex is read as another address | `Toolkit.Subnet("0x0A.1.2.3/24")` gives network 10.1.2.0/24 (sub.exe). The leading-zero check only runs when the text is all digits and dots. |
| 39 | Dark destination called "no real loss" | The 100% case is fixed. The 99% case is #12 above. |
| 47 | Private root reads as inspection | TlsIntercept still says "YES - signed by X, not a public authority" for home-lab and intranet roots (level 1 at home). |

## Checked and held up

- **Pcap and replays.** Classic pcap, pcapng (SHB, IDB, EPB, SPB), VLAN, SLL, SLL2 and raw IP. Fuzzed with 20,000 mutated sample captures through DhcpRows, RaRows, EapRows and IcmpRowsFromPackets, plus 40,000 random pcapng and classic files: no exceptions. Lengths are 64-bit, the decode is capped, and the caps are 64 MB and 50,000 kept packets.
- **DHCP and DNS parsers.** ParseDhcp: 20,000 random messages, no exceptions. Toolkit.Parse and Name: 20,000 random answers, no exceptions, with a 20-jump compression cap. DoH is capped at 64 KB and 8 s.
- **SNMP.** Read-only: only GET (0xA0) and GETBULK (0xA5) leave this slice. The only SET-like PDU in the codebase is the trap, 0xA7, in Lab.cs (outside this slice). Protocols.ParseVarbinds, Snmp.Parse2 and ReadPdu: 20,000 random each, no exceptions. Depth 16 and 5,000 values. The walk is capped at 2,000 values, 4 MB, 4 KB per value and 120 s per view, honours Stop, and stops on a repeated or decreasing OID. Stray datagrams are skipped. Community bytes are compared as UTF-8. The v3 MAC is checked at its parsed position, with a 150 s window and no privacy downgrade. Localized keys are wiped. Snmp.Text was fuzzed with every type.
- **STUN and QUIC.** Reach.ParseStun and Protocols.StunMapped held under fuzz (the declared length is respected, IPv6 XOR is right). UdpMtu checks the swapped connection IDs. Twamp has the guard and the 1,000-packet cap. Syslog, TWAMP and UdpMtu call Lab.NotOne.
- **iperf3.** Streams are created once, test start can't repeat, results JSON is capped at 1 MB, a guard thread closes sockets on Stop or at the deadline, and only TimedOut continues (the reset-stream spin is fixed). Rate is capped at 10 Gb/s. Policy.NoLab covers Iperf, Loaded, DscpPing and Wake.
- **Speed test.** Policy.NoSpeed is checked twice. Busy uses a CAS. Every IPv4 address of both the download and upload hosts must be public, and the connect-time BindIPEndPoint check is in place. speedservers.txt lines can't replace built-in servers.
- **Revocation** checks Policy.NoOnline and CertCfg. DnsHijack and TlsIntercept wait for Online.On. Hotel, HTTP, mail, SSH, NTP and DoT all have deadlines. Mail lines are cut at 1 KB, with 20 s overall and Stop. The HTTP builder never shows cookie values and has a 64 KB / 20 s body limit. HideSecrets is applied. Redirects in the inspector refuse private targets.
- **NTP.** Zero or garbage timestamps, the kiss of death, the alarm bit and the 2036 era are all handled.
- **SSH, RADIUS and TACACS.** RADIUS checks the response authenticator and Message-Authenticator, and the secret is wiped on every exit.
- **Security checkup.** VersionVerdict (Pro Education now uses the Home/Pro dates), Startup (Windows folder skipped, no double count), Hosts (every line checked), Proxy, Tasks (localized "Disabled"), DnsLeak, ArpSpoof, SecondDhcp (NoLab and Company gates), Upnp, RouterAdmin and RouterOld (company gate).
- **Toolbox runner.** Exceptions from any tool become a "Couldn't finish" row, not a crash. SNMP partial results are on a ThreadStatic and read back on the same thread.
- **Fuzzed text parsers.** Subnet, ParseOpts, Snmp.Cred.Parse, HostOnly, UrlToTarget, Security.HostOf, SpeedTest.SafeUrl, Sectors.Split, Reach.Remote, NormMac and OidCompare were fed 45 hostile strings plus 20,000 random ones. Nothing threw except the Unicode-digit cases in #18.
- **Odd MTR patterns.** 30 round-4 patterns plus 13 new ones. Silent hops are never blamed. Loss counts only when it carries on, with two quiet tail routers, a hop that is silent in the trace but answers during MTR, and runs with nothing sent all handled. The failures are #10-12.
- **Other pages.** VpnAware, WinEvidence (Auth now says which log it couldn't read), SysTools (lease times, the infinite lease, the gpresult temp file deleted), Sectors uptime and log trimming, Board stop semantics, and the OBS link (password never kept, port 4455 local only).
