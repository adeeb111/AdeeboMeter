# Changelog

Facts only: what each version reads, shows and checks. Created by Adeeb Khan.

## 1.1.4 (2026-11-09)

A Security tab, a Traffic Lab, "What's wrong?", 35 new tools (103 in all, plus 42 security checks), honest SNMP, and the fixes from four outside reviews.

### New: Security tab
- **One "Security checkup" button** - a scorecard (risky · worth a look · good · info / can't tell), problems first; click any check to run it again; Copy as text.
- **42 defensive checks, no admin**: this PC (antivirus, firewall per network type, BitLocker, Secure Boot, UAC, Windows Update and pending restart, Windows version still supported, SMBv1, SMB signing, Remote Desktop and NLA, Remote Assistance, LLMNR / NetBIOS / mDNS, WPAD, ports open to the network, root certificates added for you, hosts file, proxy, unsigned startup programs, scheduled tasks in writable folders, LSA protection / Credential Guard, memory integrity, SmartScreen, AutoPlay, everyday admin account, screen lock, saved open Wi-Fi, PowerShell policy); your network (Wi-Fi security, DNS honesty, TLS interception, ARP spoofing signs, a second DHCP server, UPnP, router admin over http, router Telnet / FTP / TR-069, public IPv6, DNS leak on VPN, new devices); your sites (certificate expiry, old TLS, security headers, SPF / DKIM / DMARC).
- Reads settings only; never tries a password, never scans beyond your own router, changes nothing. Every fix opens the Windows screen (or your router's page) where you act. The two checks that ask the internet (DNS vs an encrypted resolver, TLS interception) run only with online lookups on.

### New: What's wrong?
- First card in Investigate: this PC -> the link -> your router -> the internet -> names -> the way in, stopping at the first broken layer. Reuses what's measured; re-tests only what's stale (TCP when ping is blocked). Weak-but-working layers are named, never "all clear"; events around then are labelled as not causes; hands over to the matching guided check.

### New: Traffic Lab (fourth Toolbox section)
- ICMP builder (size, don't-fragment, TTL, gap; IPv4 / IPv6), UDP and TCP senders (text or hex), ARP who-has, DHCP options from the server, SNMP test trap (send only), NetFlow v5 test record, Wake-on-LAN with SecureOn, syslog with facility.severity; iperf3, bufferbloat and DSCP ping moved here.
- One target; at most 10 packets a second and 1,000 in all; on a company domain or VPN nothing is sent without the "allowed to send test traffic" tick; Stop works; each send is logged.

### New tools elsewhere
- Network: HTTP request builder, STUN, many ports for one service, every IP behind a name, Remote Desktop reachable?, Active Directory / LDAP reachable?, DNS answer compare, before / after snapshot, compare two saved results, My sites.
- Devices: name of a device (NetBIOS / LLMNR / mDNS / PTR), find devices (mDNS / SSDP / WS-Discovery), shared folders (SMB), remote access commands (ssh / scp / sftp / keys, filled in; click to copy).
- SNMP: Snapshot, Find a MAC, VLANs, PoE, LAG (LACP), Printer, UPS.
- Windows: Certificates & files - inspect a certificate, check a PFX (key never exported), decode a CSR, convert PEM / DER, hash a file and compare, decode Base64 / JWT, certificate commands; services that failed to start.
- Investigate › Health: **Quick tidy** - temp files, Recycle Bin, disk space, startup, frozen or memory-hungry apps, recent installs; each opens the Windows screen, nothing is deleted or changed.

### SNMP - honest numbers
- A column the device didn't give shows n/a and a "Not read" line - never 0. One 2-minute limit per view; Stop says "Stopped - partial result".
- Interfaces: CRC column, and "Since HH:mm" lists what changed per port (by ifIndex) - counter deltas, resets, up / down - kept in memory only.
- A saved walk's unreadable number shows as its own text.

### Widget and the Internet dot
- The widget claims "always on top" again after sleep, unlock or a display change, and every 30 s; tray › Widget on screen; today's down / up in the graph corners, the month against your cap.
- With the internet check off, ISP and Internet are grey (never red) and nothing is pinged - also with the main view open.

### Fixes and safety
- Secrets are never kept after a run: password / secret / community boxes are blanked in Copy, Save and Compare and masked on screen; HTTP sign-in headers are hidden and a request body isn't kept.
- Live capture drops impossible lines instead of counting them. Check a destination and Health results have Copy as text and Save. Search: "Did you mean ...?". Bufferbloat says inconclusive when the iperf3 server ignores ping.
- Release gate: the deep test says INCOMPLETE (not ALL PASSED) when its time cap skips checks; GitHub's build runs the self-test before uploading anything; build_exe.cmd returns the self-test's result.

## 1.1.3 (2026-11-08)

A Toolbox you can find your way around, stricter SNMPv3, and the fixes from two outside reviews of 1.1.2.

### Toolbox - redesigned
- **Three sections** - Network · Devices · Windows - each opening on a home page with a card per group and your recent tools; every tool page has a way back.
- **Quick buttons** on every Toolbox page: SNMP · Switch ports · MTR · Port test · dig · HTTP & TLS · iperf3 · Ping sweep.
- **SNMP is one tool with view buttons** - Overview · Interfaces · Traffic · CPU & memory · Neighbours · Sensors · Walk · MIBs - and the device and credentials carry across them.
- **The PC checks moved to Investigate › Health** ("Look deeper": memory, disk, space, background jobs, crashes, leaks, graphics, what changed, battery) and run there.
- **Search ranks its results**: *wifi* opens Wi-Fi roams, *nslookup* opens dig, *traceroute* opens MTR, *printers* opens ping sweep; plurals and punctuation are understood; a result from another section says where it lives.
- Keyboard: Esc clears an active search instead of closing the window; F1 explains the tool the keyboard is on; Ctrl+C copies the result; Ctrl+PageDown / PageUp step through the SNMP views; "All N tools ›" opens the group in the list; every tool says how long it takes (iperf3 counts both directions and -t / -O).
- Copy and Save use the inputs the result was run with, not what's in the boxes now; CSV also guards cells that start with spaces before = + - @; an MTU result is saved with its real time.
- Links (Copy, Save, Compare, Investigate ›) were nearly invisible in the dark theme since 1.0 - fixed. Input boxes and buttons are no longer clipped at 125%.

### SNMP
- **SNMPv3 replies are checked as RFC 3414 asks**: same message ID, same engine, same user, a signature of the right length checked at its parsed place, and inside the 150-second time window. A reply below the requested security is refused - unsigned, or unencrypted when you asked for privacy. Discovery only accepts the device's Report to our own request. Note: a `:` can't be used in the user name or auth password, because it separates the parts (the privacy password may contain one). A privacy password without an auth password is refused instead of being sent unprotected; `v3-sha1:` works, an unknown algorithm is an error. A device that rebooted is re-learned at once. SHA-256 key localisation has a test vector.
- A walk that stops part-way shows **Partial result** at the top (and in Copy / Save); walks stop after 2 minutes.
- Traffic times each direction on its own; a missing reading shows n/a and a counter that went backwards shows reset - never 0.
- CPU / memory colours only RAM, disks and flash (cached and shared memory are full by design).
- Bracketed IPv6 with a port (`[2001:db8::1]:161`); negative INTEGERs; values over 127 bytes; error codes in more than one byte; the MIB index has a size limit and can be read while it's rebuilt.

### Honest results
- Check a destination and the health check say red findings in plain words; the header's Investigate › link picks the right check when the PC is red, the gateway is slow or DNS fails during a call.
- **Bufferbloat**: no grade without load in both directions, "inconclusive" when most pings are lost (never an invented number), ping loss per direction, pings the iperf3 server itself by default, and says the grade assumes the line was really full.
- iperf3: UDP at the rate you asked for is no longer called slow; an unknown direction word is refused; Advanced -R / --both / -t (also as -t20 or --time=20) win over the plain controls; host:port and the "50m" shorthand go through the same limits as the options.
- Wi-Fi roams cover the real last 24 hours (yesterday included) and "back and forth" means back to the access point it just left.
- New devices are listed in What changed during a network's first day too (only the pop-up waits); the very first visit to a network is its baseline and lists nothing; a sweep lists ten and counts the rest.

### Network checks
- **Revocation is checked only when you press it.** The HTTP inspector and Check a destination were asking the certificate authority on every run.
- Teams UDP test accepts answers only from the relay it asked, with the STUN magic cookie; RADIUS checks a Message-Authenticator when the reply has one.
- dig: a short or damaged record (names and SOA included) never loses the records after it; DS and TLSA digests are shown in full.
- HTTP / TLS: the chain view says when it's cut; the revocation check reports any other chain problem instead of a clean "not revoked".
- NTP reads time correctly after February 2036.
- HTTP timing stops after 5 minutes overall (30 s per download); a capture you stopped can't restart itself; the history merge after a sniff can't be left running; a stalled TLS check closes its connection before cleaning up.
- Monitor can be stopped and started again.

### Corrections to earlier notes
- 1.1.2 "online lookups read at most 16 KB": they read in 16 KB pieces, up to 4 MB.
- 1.1.2 "IPv6 path MTU in ping and trace": in IPv6 ping only. "DHCP options": router, DNS and domain.
- 1.1.1 "TLS handshakes give up after 8 s": DoT gives up after 6 s.

## 1.1.2 (2026-11-08)

SNMP that reads like a person wrote it, a friendlier Toolbox, more engineer's tools - and the loose ends from the 1.1 reviews.

### SNMP - read-only, never changes a device, never listens
- **Views that answer questions**: what is this device, interfaces (status, speed, errors, discards - problems first), traffic per port (two readings 5 s apart), CPU and memory, neighbours (LLDP), sensors (temperature, optics).
- **Walk any OID or MIB name** (GETBULK, up to 2,000 values, paced, Stop): rows rebuilt into tables joined by index, the useful columns first, values decoded ("not changed since the device booted" instead of 0).
- **Your own MIB folder**: drop a vendor's .mib files in the folder (*Open MIB folder*); a strict local reader indexes object names, tables, enumerations and descriptions once - nothing in a MIB is ever run. **Search your MIBs** for "temperature", "optic", "fan"... and one click walks just that table.
- **SNMPv3** (user-based security): no auth, auth (MD5, SHA-1, SHA-256) and auth + AES-128 privacy - type `v3:user:authpass:privpass`. Keys are localised as RFC 3414 says, checked against its own test vectors. Credentials are masked, kept for this session only, never in Copy, Save or the compare key.

### Toolbox - easier to use
- **Two lines per tool**: the name and what it answers. Search understands everyday words (wifi, slow, lag, traceroute, nslookup, printer...).
- **Keyboard**: arrows, Page Up / Down, Home / End and Enter in the tool list; typing jumps to search; **F1** explains whatever is under the mouse, or the tool you're in.
- **Labels stay above the boxes** while you type, and every tool says how long it takes before you press Run.
- **iperf3 in plain words**: direction (both, upload, download) and seconds, with iperf3's own options under Advanced.
- **Ping sweep**: a tick-box ("this is my own network, or I'm allowed to scan it") instead of typing yes - never ticked by default, never remembered.
- **Save...** next to Copy as text: .txt, or .csv for a spreadsheet (cells that start with = + - @ can't turn into formulas). Only where you choose, only when you press it.
- **A red verdict is never a joke** - the plain words, whatever voice is set - and it links straight to the Investigate check that fits (*Investigate ›* in the header).

### New and deeper tools
- **Latency under load (bufferbloat)** with your own iperf3 server: idle delay, then delay while downloading and uploading, graded A+ to F.
- **DSCP-marked ping**: marked and unmarked pings in turn, compared - and honest that only a capture proves the marking left the PC.
- **Who announces itself**: mDNS, SSDP / UPnP, LLMNR and NetBIOS on your segment (Wireshark + Npcap; listens only).
- **DHCP lease**: obtained, renews (T1), rebinds (T2), expires, server and options; where each IPv6 address came from and how long it stays.
- **Wi-Fi roams and drops** (24 h): every hop between access points, back-and-forth roams marked. Roams now record which access point they moved from and to.
- **dig**: HTTPS / SVCB (HTTP/3, Encrypted Client Hello), DS, DNSKEY (with key tag), TLSA, SSHFP, IPv6 reverse names - and queries over DoH when the server is an https:// address.
- **HTTP and TLS inspector**: every header, security headers explained (and which are missing), the whole certificate chain with keys, signatures and validity dates, "not valid yet" - and a revocation check (OCSP / CRL) only when you press it.
- **IPv6**: the path MTU Windows learned (Packet Too Big) in IPv6 ping and trace; *Check a destination* tries the same port over IPv6 next to IPv4.
- **Keep this capture**: Sniff captures are still deleted on exit - one click copies the newest where you choose.

### Reliability
- Live capture starts and stops under one lock and its reader thread is joined; Monitor waits for its threads on stop; history tidying no longer holds the sampler.
- Ping sweep has one deadline for the whole sweep (90 s); iperf3 keeps UDP loss and jitter per stream with -P and checks the UDP connect reply; online lookups read at most 16 KB and escape what they send; the list of seen devices is capped.
- The build workflow is split: the build job can only read the repository; a separate release job, which never runs the build script, is the only one that can sign provenance and attach files.

### Not in this version
- Per-monitor DPI (moving the window between a 100% and a 200% screen) - it means re-laying-out every owner-drawn page live; too risky to rush. Windows scales the window instead, slightly soft.

## 1.1.1 (2026-11-08)

A patch from the first day of use and four outside code reviews.

- **New-device alerts are off by default.** Phones join Wi-Fi with a private, changing hardware address, and office networks are full of other people's devices - so every new device is still listed in What changed, but a pop-up only comes if you choose *Tell me when a new device joins → Home networks only* (Settings). Even then: never on a company network or VPN, never for private addresses, not in the first day on a network, at most one an hour.
- **Copy as text** on every Toolbox result (including the MTU and MTR tables) - for a ticket or a chat. Built only when you press it; a RADIUS secret is never included.
- **Neighbours (ARP table)** in the Toolbox: who this PC has talked to on its network, with makers, private addresses and virtual routers. Nothing is sent.
- **Replies are only taken from the server you asked.** NTP, SNMP, RADIUS, TWAMP and DNS sockets are connected to that server; STUN checks the transaction ID and the server's address; NTP checks its answer echoes your question (and reports "kiss of death" and unsynchronised servers instead of an offset); TWAMP checks the reflector echoed your timestamp; SNMP checks it's a reply to this GET with no error.
- **Harder parsing:** the SNMP reader checks every length before using it, handles long values (descriptions over 127 bytes) and nesting has a limit; the JSON reader refuses nesting deeper than 64 (a hostile iperf3 server could otherwise end the app); DNS checks the TCP answer and every record's length. The RADIUS reply check is tested against RFC 2865's own example.
- **Nothing waits forever:** TLS handshakes give up after 8 s (HTTP timing, encrypted DNS, mail, TLS inspector); iperf3 streams connect within 5 s each and Stop is checked between them.
- **Child programs never start in the folder AdeeboMeter runs from** (Downloads): netsh, tshark, dumpcap and the rest start in their own folder, so they can't pick up a planted DLL.
- **iperf3 always cleans up:** Stop, or a test running a minute past its length, closes every socket at once (breaking any wait); sockets close even when a test fails; a stopped test says "stopped - no result" instead of a number.
- **Clearer protocol verdicts:** RADIUS tells "secret matches and Status-Server answered properly" from "signed with this secret but not the answer Status-Server expects"; SNMP checks the reply's community and handles out-of-range values; NTP refuses timestamps that don't add up and calls the offset what it is - an estimate from one server.
- Smaller: iperf3 rate capped at 10 Gb/s and port checked; the syslog test strips line breaks; TWAMP no longer claims a synced clock, accepts 40-byte replies and says when loss can't be put on a direction; Wake-on-LAN no longer shows green for a packet it can't confirm; sustained ping starts with 50 pings (10 s); a destination typed elsewhere no longer replaces the gateway in sustained ping and SNMP; "Settings > Online lookups" named correctly; the build workflow treats the release tag as data.
- File version in the exe has the four numbers Windows expects.

## 1.1 (2026-11-08)

Troubleshoot anything from the PC - the path, the way back, the PC itself, and an engineer's toolbox.

### Toolbox - now its own section (everything on press, outbound only)
- **Find a tool**: type mtu, dns, radius, slow... - searches names and explanations; Enter opens the first match. Recent tools on top (this session only), groups fold away.
- **Grouped by the question**: Path & routing · Is it reachable? · Speed & quality · Names & time · Network devices · Windows · The PC.
- **One target**: type a host once - every tool that looks at a destination starts with it.
- **Compare with the last run**: run a tool again with the same inputs and see what changed, row by row (kept in memory only).
- **Routing, as a PC can see it**: which route Windows picks for a destination (VPN or not, the matching route and metric); your gateway as a VRRP / HSRP / GLBP virtual router, with takeovers on the timeline; IPv6 routers and prefixes (rogue router advertisements); load sharing (ECMP), reroutes, hidden tunnel hops and delay jumps in MTR; slow plug-in (spanning tree without port fast) on the timeline.
- **Routing protocols on my segment** (needs Wireshark + Npcap): listens for OSPF, EIGRP, RIP, IS-IS, PIM, LDP, BFD, VRRP / HSRP, IPv6 RAs, extra DHCP servers, spanning tree changes, LLDP / CDP and DTP - and says which ones don't belong on a user network. AdeeboMeter never sends a routing packet.
- **MTU per hop**: the biggest packet that reaches each hop - *Next hop*, *Probe all* or click a hop; names the drop (PPPoE, VPN, GRE) and silent black holes.
- **MTR**: loss and delay per hop over 1-10 minutes, read properly (loss that doesn't carry on is just a router ignoring pings).
- **The way back**: reply-TTL asymmetry per hop, Record Route, how the internet reaches you (with online lookups), and the command to run from the far end.
- **Who carries my traffic (BGP)**: AS and owner per hop, handovers and internet exchanges, delay per network, the destination's prefix, RPKI and route changes, and who you leave the internet through.
- **Why does it fail?**: which proxy a URL uses (and WPAD / PAC / WinHTTP), hosts file, DNS hijack and the VPN's name rules, port test (refused vs dropped), Teams UDP test with NAT type, clock check.
- **Speed**: iperf3 client to your own server (TCP up / down / both, parallel, UDP with jitter and loss), sustained ping with a loss timeline, nearest cloud.
- **DNS and web**: dig for any record (and TRACE from the root down), HTTP and TLS inspector.
- **Your network**: your own router (public address, CGNAT, hairpin / NAT loopback), ping sweep with makers (asks first on company networks), Wake-on-LAN, subnet calculator (IPv4 / IPv6).
- **Protocols** (clients only, never logs in): TCP ping, HTTP timing repeated (DNS / connect / TLS / first byte / download), encrypted DNS (DoH, DoT), NTP query, mail server (SMTP / IMAP / POP greeting, STARTTLS, certificate), SSH banner, SNMP v2c get, syslog test message, RADIUS Status-Server (secret masked, never saved), TACACS+ reachability, TWAMP-light to your reflector (loss and jitter each way), IPv6 ping and trace.
- **Windows**: address conflicts, network profile and firewall, adapter power saving / speed / driver, DHCP, and why 802.1X network sign-in failed; domain controllers (and their Kerberos / LDAP / SMB / RPC / GC ports), Group Policy applied, certificates that expire soon, firewall rules for an app, what's listening, event log errors in 24 h explained, where the PC gets its time.
- **The PC**: memory pressure, disk latency, heat / throttling, driver interrupts, frozen windows, slow leaks, crashes and hangs, background jobs, graphics card per app, what changed in 7 days, battery health, disk users / health / space trend and where the space went.

### Hardening
- DLL planting protection: once it starts, the app loads DLLs only from System32, never from the folder it runs in (Downloads).
- File details (product name, version) set in the exe; release builds carry GitHub build provenance; the build workflow pins every action to an exact commit.
- `--deeptest`: hostile input into every tool, real runs, repeat-run leak check and UI stress, at low priority with a time cap - writes AdeeboMeter-deeptest.txt. Found and fixed: switching away from Ping sweep crashed the Toolbox.

### Fixes and polish
- Cards never cut a value: a name and value that don't fit on one line get two; the Wi-Fi neighbours columns and the button rows fit the window.
- Walk test: rename or remove a room.
- Network story packets show arrows and names correctly (Wireshark's UTF-8 output).
- No "new device" alerts for devices a ping sweep finds - they were already there.
- Pages redraw cleanly when their height changes while scrolled.

### Investigate
- Two new guided checks: **Works at home, not on VPN or at work** and **My PC is slow**. "Won't open" now also checks the hosts file, proxy and clock; "Calls freeze" tests UDP to Teams.

### Online lookups (opt-in, timed)
- Settings > Online lookups: off by default; switch on for 5 min - 3 h (30 min default), a header chip shows the time left and turns them off. Only public IP addresses go to RIPEstat and PeeringDB over HTTPS.

### Time machine
- History and Replay now include the PC: disk latency and memory read back from disk, every minute ("it was the disk, not the Wi-Fi"); frozen apps on the timeline.

## 1.0 (2026-11-08)

The first public version: a quiet Windows companion that tells you, in one glance, whether your PC, your Wi-Fi, your provider, DNS or the internet is the problem — what to do about it, and the whole story when you want it.

### Highlights
- **Corner widget** — faded, click-through, folds into a small chip when a window covers the corner. One sentence
  verdict, five dots from your PC out to the internet.
- **Engineer view** (tray icon or Ctrl+Alt+A) — six sections: Overview, Monitor, Investigate, Network story, Time machine, Learn.
- **Your Wi-Fi** — band, channel and width, signal in dBm, roaming support, neighbouring networks on your channel, why
  Wi-Fi dropped. **Your cable** — speed, and the switch, port and VLAN you're plugged into (LLDP / CDP).
- **Check a destination** — DNS, route, ping, TCP, TLS, HTTP, path MTU and a trace, then the path story: who carries
  your traffic, NAT, tunnels, firewalls and load balancing.
- **Live capture** (with Wireshark) — what your traffic is made of, bytes per app, Teams call quality, rogue DHCP.
- **Plain English everywhere** — hover anything with a **?** cursor; 160 terms and 72 programs explained.
- **Light by design** — Penny / Balanced / Engineer presets; about half a percent of one CPU core in the background.

### Find your way
- **Six sections**: Overview · Monitor · Investigate · Network story · Time machine · Learn, each with its own pages underneath - room to grow.
- **Overview** start page: the verdict, router / internet / DNS / traffic right now, what needs attention, today, and where everything lives.
- **Investigate a problem**: "The internet is slow", "A website or app won't open", "Calls freeze or sound robotic", "I keep disconnecting" - each ends with Finding · What I observed · Not established · Next test.
- **Evidence labels** on every finding: measured · Windows-reported · seen in packets · inferred · unavailable.
- **Details** switch (header): raw values, sources and the exact time each was read.
- **Replay** (Time machine): drag through any day of history - the five dots, the numbers against the day's middle value, and what changed 15 minutes either side; jump to the next problem.
- **Corner widget**: visibility 20-100 %, and a **bigger widget** (tray menu or Settings).

### Watch smarter
- **History on by default** (3 days; Settings 1-90, rolling delete), one line a minute with peaks, finished days compressed (~15 KB a day).
- **What changed**: the week at a glance (7 days x 24 hours), data usage today / this month per network with an optional monthly cap, what's normal on this network, latency under load (bufferbloat), after-call summaries, suggestions and new devices.
- **Alerts** only when there's something to do (Wi-Fi keeps dropping, internet gone, slower than usual, a suspicious new program, a new device, cap at 80%); quiet hours 22-07; silent while you present or play full screen.
- **Slower than usual for this network** - learns what's normal for each network (told apart by the router's hardware address, so it works with location off).
- **Your Wi-Fi**: Wi-Fi health score that works with location off (router link, link speed, adapter errors, drops), Wi-Fi advisor (quieter channel, better band, sticky access point, Windows' Wi-Fi power saving), look-alike networks (evil twin), and the walk test.
- **Watch and suggest**: first-time internet use, programs in Downloads / Temp or without a publisher, night-time connections, big unexpected uploads - with how to block it. Never blocks.
- **HTTPS inspection**: Check a destination says when a certificate was re-signed by a root that isn't on Microsoft's public list (a company proxy - or a man in the middle).
- **New devices** on your network, from the ARP table (nothing scanned).
- **Meeting replay**: one sentence after each Teams call about what went wrong on the network. Numbers in memory only; nothing about the call is saved.
- **Health tools you press**: pre-call check, DNS speed test, VPN map (split tunnel), and "What can AdeeboMeter see here?" (what IT allows on this PC).
- **Watch a pinned destination all day** (opt-in): one ping every 30 s, graphed.
- **Local crash note** with a "report this" button that opens a pre-filled GitHub issue (no user names or folders).
- **Check for updates** in About opens the website, which compares versions - the app itself never checks.
- **Settings export / import.**

### Understand everything
- **Network story**: follow any app (the busiest is picked for you) or a site through seven boxes - App → Windows → Wi-Fi / Cable → Router → Provider → Internet → Server; click a box for what happens there (the routing table and "which route wins", packet sizes and MTU, how your PC got its addresses, the path); *Time each step* shows name · connect · lock · first byte.
- **With Wireshark**: the real handshake as a ladder, any packet explained layer by layer, "what my PC is saying" (protocols, IPv6, QUIC, LLMNR / NetBIOS / mDNS / SSDP chatter), call packet priority (DSCP).
- **Teams & Office end to end**: process families (with their WebView2 helpers), every connection labelled by role, "Teams fell back to TCP", how a Teams call and Outlook connect, "Why is Outlook slow?".
- **Inside Windows**: who started whom (process tree), how Windows starts (boot chain with live PIDs), inside each svchost, what starts with Windows and who added it, what a program is doing right now (CPU, memory, disk, connections, why it started), deep dives.
- **Learn**: 43 topics - start here (how apps use the internet), protocols, Windows, Teams and Office - each two lines, a picture and one "if it goes wrong" line, with "See it live on my PC" (Wireshark); **Ctrl+K** jumps to anything.

### Install, update, remove
- **One file installs itself**: double-click AdeeboMeter.exe and choose *Install*, *Update* or *Just run it from here*.
  Per user (`%LOCALAPPDATA%\Programs\AdeeboMeter\<version>`), no administrator rights, a Start menu entry and an entry
  in Settings > Apps; your settings and "Start with Windows" come along.
- **Remove** from Settings > Apps or the tray menu (*Uninstall AdeeboMeter...*) — folder, settings and history.
- **Updates** ask the running copy to step aside, then replace it; older versions are removed.
- **Icon** on the exe, the Start menu and Settings > Apps.
- **Website** with a download button, the Theory of Everything as slides, and a suggestion box:
  https://adeeb111.github.io/AdeeboMeter/

### Privacy and security
- No cloud, no account, no telemetry, no auto-update; never opens a listening port (checked at every start).
- History, data usage and what's normal per network stay on this PC (3 days by default, 1-90); meeting replay keeps numbers in memory only.
- What it sends is listed in *About* with your current settings, and each item can be switched off.
- No administrator rights, no shell; Windows tools by full System32 path; TLS 1.2 / 1.3 with Windows' certificate
  checks; builds clean against Microsoft's .NET security and quality analyzers with every rule on.

### Known limitations
- Not code-signed yet: SmartScreen asks once (*More info > Run anyway*).
- No AS numbers / BGP data (would need an internet service); traces use ICMP (TCP/UDP traces need admin).
- Wi-Fi name, signal and neighbours on Windows 11 need *Location* allowed for desktop apps; without it AdeeboMeter falls back to a Wi-Fi health score from the router link, link speed, adapter errors and drops.
- Live capture and *Find my switch port* need Wireshark (tshark + Npcap).

### Under the hood
- Releases are tagged `v<version>` and built by `release.ps1` on GitHub's Windows runner; the app installs and
  removes itself (no Setup.cmd).
- Self-test: 27 checks (install paths, the settings line, the icon, every explanation, evidence kinds, investigations and learn topics; the view walk visits all 13 pages); `--perf` times every page on your own PC.
- Source in `src/`, one file per part; `build_exe.cmd` and `release.ps1` compile `/recurse:src\*.cs`.
- The knowledge (programs, terms, learn topics) moved to plain-text `knowledge/*.txt`, packed **gzip-compressed inside the exe** and unpacked only when a page asks; released when the engineer view closes.

## beta-0.6.3 (2026-11-07)

Release pattern, steadier self-test.

- **Download**: a versioned zip with Setup.cmd / Uninstall.cmd (replaced in 1.0 by the exe installing itself);
  releases built by `release.ps1` on GitHub's Windows runner, notes from this changelog.
- **Build**: `build_exe.cmd` replaces build.ps1 (double-click; no PowerShell execution policy in the way).
- **Self-test**: the handle-leak check opens and closes the engineer view 30 times and allows 10 handles of drift (the
  old 12-handle failure was measurement noise); closing the view drops its icon and hover card explicitly.

## 0.6.2 and earlier (before the release pattern)

Fixes from the first runs on a real PC:

- **Your PC**: TCP rows Windows reports with state 0, or closed with no remote end, show as BOUND.
- **DNS cache**: when Windows needs elevation to show it, the view says so instead of showing it empty.
- **What's running**: programs under your user profile (Documents and the like) count as "yours", not "elsewhere".
- **Your Wi-Fi / cable**: with no adapter matched, the first active adapter is used, else one plain sentence.
- **ISP**: learned once from a 6-hop trace to 1.1.1.1 and the reverse name of the first public hop ("provider: …").

What 0.6 brought - renamed to AdeeboMeter (settings and "start with Windows" of the old name move over by themselves).

- **Your Wi-Fi**: band, channel, channel width (HT / VHT / HE), signal in dBm, 802.11k/v/r roaming support, the channel
  map of who else is on the air, why Wi-Fi dropped (Windows' WLAN event log), devices on your network (ARP), driver age.
- **Your cable**: speed and duplex, and from LLDP / CDP (needs Wireshark, on demand) the switch, port and VLAN.
- **The path story**: who carries your traffic (reverse names), where networks hand over, private / CGNAT / public
  hops, PPPoE (MTU 1492), load balancing, firewalls inferred from refusals, the return path from reply TTLs.
- **Live capture**: Teams / RTP call quality (jitter RFC 3550, MOS), rogue DHCP servers.
- **Header**: day / night switch, About, Settings, Back to corner.
- **Settings**: Penny / Balanced / Engineer (18 / 36 / 74 checks a minute); optional 7-day history.
- **Explanations**: every process and every term on hover (what it is, how it works, why it matters, what to do).
- **Captive portal**: Windows' own NCSI probe; DNS second opinion via 1.1.1.1 / 8.8.8.8.
