# Changelog

Facts only: what each version reads, shows and checks. Created by Adeeb Khan.

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
