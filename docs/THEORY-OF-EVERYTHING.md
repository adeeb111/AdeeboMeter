# AdeeboMeter — The Theory of Everything

*How every part of AdeeboMeter works, in plain words. Version 1.1.5.*

---

## 1. The idea in one breath

When the internet feels slow, most people open Task Manager, then a command prompt, then `ping`, then guess.
AdeeboMeter does all of that for you — quietly, every second — and tells you in one sentence **whether the problem is you or the network**, and what to do about it.

It lives in two places:

- **The corner widget** — a small, faded card in the bottom-right corner of your screen. You glance at it; you never click it.
- **The engineer view** — a full-screen window you open from the tray icon (or Ctrl+Alt+A) when something is actually wrong.

Everything else in this document is detail behind those two.

---

## 2. Five promises the whole design follows

| Promise | What it means in the code |
|---|---|
| **A penny app** | The background work is a few dozen tiny jobs a minute. Anything heavy (capture, traces, the process guide) runs only while you are looking at it, and stops by itself. |
| **No cloud. No telemetry. Never listens.** | There is no server, no account, no update check. AdeeboMeter never opens a listening port — and checks that about itself every time it starts. Every packet it sends is listed in *About*, and each can be switched off. |
| **No admin rights** | Everything comes from what Windows already shows a normal user. If Windows says no, that part simply doesn't show. |
| **Readable source** | Plain C# in `src/`, one file per part, built by the compiler that already ships inside Windows. Nothing to install, nothing hidden. |
| **A companion, not a robot** | Every message has two versions: a friendly one ("Good morning boss, your PC looks cooked") and a plain one (*Serious* mode). The fix always comes right after the joke. |

---

## 3. The big picture

```
                 ┌──────────────────────── Windows ─────────────────────────┐
                 │ CPU/memory/disk counters · adapter stats · TCP/UDP tables │
                 │ netsh / Wi-Fi API · event logs · DNS · ping · ARP table   │
                 └───────────────┬──────────────────────────────────────────┘
                                 │  read only
              ┌──────────────────▼──────────────────┐
              │              Monitor                │  two background threads
              │  Sampler (1 s)     Probe (2-10 s)   │
              │  counters, verdict  pings, Wi-Fi,   │
              │  every 10 s         apps, conns     │
              └──────────────────┬──────────────────┘
                                 │  a fresh "Snap" (snapshot) every second
            ┌────────────────────┼─────────────────────┐
            ▼                                          ▼
   Corner widget (AppCtx)                    Engineer view (FullView)
   draws the Snap as an image,               seven sections: Overview, Monitor,
   faded, click-through                      Investigate, Toolbox, Network
                                             story, Time machine, Learn
                                             (+ Settings, About)
```

The rule that keeps it light: **the Monitor is the only thing that always runs**. The widget just paints the latest snapshot. The engineer view adds its own work only while it's open.

---

## 4. The life of AdeeboMeter

1. **Start** (`Program.Main`). Windows starts `AdeeboMeter.exe` (by you, or at sign-in if *Start with Windows* is on).
   - It makes sure only one copy runs (`Single`). A second start just tells the first one to open the engineer view.
   - It loads settings from `AdeeboMeter.settings` next to the exe (and carries over an old `AdeboMeter.settings` from before the rename).
   - It checks it isn't listening on any port. If it ever were, it would refuse to run.
2. **Run.** The Monitor's two threads start. The tray icon appears. The widget appears in the corner.
3. **Open the engineer view** — tray click or Ctrl+Alt+A. The Monitor switches to a faster pace while it's open.
4. **Back to the corner** — Esc, minimise, the X, or *Back to corner*. The view closes and **gives all its memory back**. The widget keeps watching.
5. **Exit** — right-click the tray icon → *Exit*. Every helper process (tshark, dumpcap, netsh) belongs to a Windows *job object* that is killed with the app, so nothing is left behind. Temporary captures are deleted.

---

## 5. The parts, one by one

Each part below follows the same pattern: **what it is · how it works · why it's there · what happens without it.**

### 5.1 `Program` — the front door
- **What:** where Windows starts the app. Reads command-line switches (`--selftest`, `--check <host>`, `--light`, `--dark`, …), sets up the log file and the capture folder.
- **How:** a few lines that hand over to `AppCtx` (the running app) or to `SelfTest`.
- **Why:** one place that decides *how* the app runs.
- **Without it:** nothing starts.
- **Also here:** `SafeCapturePath` — every file path for captures is checked to stay inside AdeeboMeter's own folder (no path tricks), and the log keeps itself under 256 KB and never records window titles or traffic content.

### 5.2 `Cfg` — your settings
- **What:** every choice you can make in *Settings*: how often to ping the router, check the internet, read Wi-Fi, scan apps; call detection; history; widget fade; saved destinations.
- **How:** plain `key=value` lines in `AdeeboMeter.settings`. Every value is checked against a short list of allowed values when it's read, so a broken file can't do harm. Saving writes a temporary file and swaps it in, so a power cut can't leave half a file.
- **Presets:** *Penny* (~8 checks a minute: router every 30 s, internet every 5 min), *Balanced* (~71, the default: router every 2 s, internet every 15 s; it took over Engineer, which had the same timings) and *Gamer* (the same, plus up to 16 game servers and the best regions every 10 s). The DNS lookup runs at most once a minute. These counts are on top of the once-a-second counter read the widget always does.

### 5.3 `Single` and `Job` — good manners
- **`Single`** uses a named mutex so only one AdeeboMeter runs, plus named events so a second start can ask the first to open its window.
- **`Job`** runs Windows tools (netsh, ipconfig, tshark) **by their full System32 path, with no shell**, with a timeout, inside a job object that dies with the app. This is why AdeeboMeter can't be tricked into running something else, and can't leave a stray process behind.

### 5.4 `Voice` — the companion
- Every message is written twice: `Voice.P("friendly", "plain")`. *Serious* mode picks the plain one. `Greet()` gives "Good morning / afternoon / evening, boss."

### 5.5 `Native` — talking to Windows
- The list of Windows functions AdeeboMeter calls directly: CPU times, memory status, TCP/UDP tables, DNS queries, ping helpers, window positions, the hotkey, dark title bars, process paths. All are loaded **only from System32** (a defence against DLL planting).

### 5.6 `Ring`, `Timeline`, `Snap` — memory with a size limit
- **`Ring`** keeps the last N values of something (e.g. 120 CPU readings). It never grows — old values fall off the end. This is a big part of why memory stays flat.
- **`Timeline`** is the *What changed* diary: the last 2 hours or 300 events, whichever is smaller, with duplicates within 2 minutes dropped.
- **`Snap`** is one finished snapshot for the screen: the verdict, the headline, the five path dots, the numbers. The UI only ever reads snapshots, never the Monitor's live data — so painting can never slow the Monitor down.

### 5.7 `Monitor` — the heart
Two background threads:

**Sampler — every second**
- Reads CPU (`GetSystemTimes`), memory (`GlobalMemoryStatusEx`), disk (performance counters), network bytes (adapter statistics), and TCP/IP health (retransmits, resets, failed connections, ICMP unreachable, UDP drops).
- Every 10 seconds it **judges**: it looks at everything and writes the verdict (OK / Busy / Act) and one sentence.
- Once a minute it writes one small line to today's history file (history is on by default, kept 3 days).

**Probe — every 2 to 10 seconds** (the pace comes from Settings; the open view and Teams calls get the fast pace)
- **Router ping** — is the local link healthy?
- **Internet + DNS check** — one ping to 1.1.1.1 (or 8.8.8.8) and one name lookup that skips the cache.
- **Adapters** — every 10 s: Wi-Fi or cable, IP address, gateway, VPN, DNS server, packet size (MTU). Any change becomes a *What changed* event.
- **Gateway spoof watch** — the router's hardware (MAC) address should never change while you stay on the same network. If it does, that's a classic sign of ARP spoofing, and you get a red event.
- **Provider name** — once per network: a 6-hop trace toward 1.1.1.1 and one reverse-DNS lookup on the first public router. Its name (e.g. `…rogers.com`) gives the ISP dot its label.
- **Wi-Fi** — `netsh wlan show interfaces`: signal, channel, access point, link rate. Roams, drops, channel changes and signal falls become events. After a drop, it asks Windows' own log *why*.
- **Apps and connections** — the process list (who uses CPU and memory) and the connection table (bursts of new connections, connections stuck knocking).
- **Teams calls** — Teams uses UDP ports 50000–50059 only during calls; when they appear, AdeeboMeter watches more closely.

**How the verdict is found** (`Judge`)
It walks the path from you outwards — *PC → Wi-Fi/LAN → ISP/VPN → DNS → Internet* — and the **first dot that isn't green is where the trouble starts**. Everything after it is a victim, not a cause. On top of that it adds **cause hints**: e.g. *"TCP retransmits (12/s) started when Wi-Fi signal fell to 40%."* — it lines up *when* things happened.

### 5.8 `Tables` — who is talking to whom
- Reads Windows' TCP and UDP tables (IPv4 and IPv6) with the owning process of every socket.
- Windows sometimes reports undocumented state numbers (0) for sockets that are only waiting; AdeeboMeter treats those as *bound* (idle) and hides them by default.
- `OwnListener()` is the self-check behind *never listens*.

### 5.9 `Theme` and `Draw` — the look
- **`Theme`** holds every colour as a token, with a light set and a dark set, switched live (the sun/moon switch, or *Follow Windows*).
- **`Draw`** creates fonts **once** (Segoe UI Variable and Cascadia Mono on Windows 11; Segoe UI and Consolas on Windows 10) and paints the widget images and the path strip.
- Every pen and brush is created and disposed inside `using` blocks — no handle leaks.

### 5.10 The corner widget (`AppCtx` + `LayeredForm`)
- **What:** a small card that is **painted as an image** and shown with per-pixel transparency (`UpdateLayeredWindow`).
- **Never takes clicks:** the window is marked *transparent* to the mouse, so whatever is underneath keeps working. All clicking happens on the tray icon.
- **30% faded** unless you hover.
- **Folds away** when a window covers the corner — into a tiny chip. Rest the mouse on the chip for 0.45 s and the card comes back.
- **Light on the CPU:** it checks the mouse 20×/s only when the mouse is near the corner, 4×/s otherwise, and 2×/s when hidden. It repaints only when the snapshot actually changed.

### 5.11 The engineer view (`FullView`)
A full-screen window, built only when you open it and destroyed when you leave.

- **Header:** the verdict, the day/night switch, *Details*, *About*, *Settings*, *Back to corner*.
- **Seven sections**, each with its pages underneath: **Overview** · **Monitor** (Connections, Your Wi-Fi / cable, What's running, Live capture) · **Investigate** (Investigate a problem, Check a destination, Health) · **Toolbox** · **Network story** · **Time machine** (Replay, What changed) · **Learn** (Topics, Inside Windows). **Ctrl+K** jumps to anything.
- **Details** shows raw values, sources and the exact time each was read, on every page that has them.
- **The path strip:** five dots. **Click a dot** to jump to the page that explains it (PC → What's running, Wi-Fi/LAN → Your Wi-Fi, the rest → Check a destination).
- **Side panel:** TCP/IP health, CPU/memory/disk/download bars, top apps by memory and CPU. It shows fewer rows on small screens instead of cutting them off.
- **Hover explanations:** wherever the mouse turns into a **?** cursor, a card explains it — 332 terms and 72 programs. Each page records "hot spots" while it paints; the mouse just looks them up.
- **Small screens / high scaling:** page buttons switch to short names, extra filter chips hide, and the side panel shrinks to 30% of the width.
- **Footer:** AdeeboMeter's own CPU and memory, against a 64 MB budget.

### 5.12 Connections (`ConnList`)
- A custom-drawn list of every connection, refreshed every 2 s while visible. New rows flash in; closed rows fade out.
- Filter by app or text; *Bound / listening* shows idle sockets.
- **Sniff** on a row records just that conversation (from the live-capture ring buffer plus 15 s more) and opens it in Wireshark.

### 5.13 Your Wi-Fi / Your cable (`LinkInfo`, `WlanApi`, `EvLog`, `Arp`, `DriverInfo`)
Collected only while the page is open, every 10 seconds:
- **Radio:** network name, exact access point, band (2.4/5/6 GHz), channel, **channel width** (20–160 MHz), Wi-Fi generation, signal in % and dBm, link speeds, security, **roaming help** (802.11k/v/r).
  - Channel width and roaming support are read from the access point's own announcements (beacon *information elements*) through Windows' Wi-Fi API.
- **Interference:** how many access points share or overlap your channel, and how busy the air is (if the access point reports it), with a **channel map**.
- **Cable:** adapter and negotiated speed (100 Mb/s instead of 1 Gb/s is a classic bad-cable sign), and **Find my switch port**: Wireshark's engine listens for up to 65 s for the switch's own LLDP/CDP announcement — switch name, port, VLAN, model. Nothing is sent.
- **Windows' diary:** every Wi-Fi drop in the last 24 h with **the reason Windows recorded**, and DHCP trouble — read from the event log through `wevtapi.dll`. Event data is read with simple patterns, not an XML parser.
- **Devices on your network:** the ARP table (devices your PC has already talked to — nothing is scanned), with names from your router's DNS. Random (privacy) MAC addresses are marked.
- **Driver age:** from the registry; older than 3 years is flagged.
- **Link to your router:** latency, jitter and loss from the pings already running.
- **Windows 11 catch:** newer Windows hides Wi-Fi details until *location* is allowed for desktop apps. AdeeboMeter detects that and says exactly which switch to flip.

### 5.14 What's running (`Explain`)
- Every program on the PC, grouped (your apps, company/IT tools, security, Windows helpers, Windows core), with memory and CPU.
- Click one for the story: **what it does · how it works · why you need it · what happens without it · can I close it?** (green = safe, amber = leave it, red = Windows needs it, purple = managed by IT).
- Programs that aren't in the guide are described from the program file itself (description, publisher, folder). AdeeboMeter **only explains — it never closes anything.**

### 5.15 Check a destination (`DestCheck`)
Type a name, IP or URL. Input is validated strictly before anything runs. Then, step by step:

1. **DNS** — real lookups (IPv4 and IPv6), skipping the cache. A **second opinion** asks 1.1.1.1 the same question: if your DNS says "no such name" but the public one finds it, a filter (e.g. AdGuard, a company filter, the VPN's DNS) is blocking it.
2. **Route** — which adapter Windows would use (found by "connecting" a UDP socket — nothing is sent).
3. **Ping** — four echoes.
4. **TCP** — the three-way handshake to your port, timed.
5. **TLS** — **TLS 1.2 or 1.3 only**, with Windows' own certificate validation. If the certificate isn't trusted, the check stops and **nothing is sent** to that server.
6. **HTTP** — one `HEAD` request; only the status line and time to first byte are read.
7. **Path MTU** — "don't fragment" pings narrowed down to the biggest packet that fits.
8. **Path** — an MTR-style trace: 3 rounds per hop, all hops at once.

Then **the path story**: the hops are grouped by who runs them, using each router's reverse-DNS name — *Your PC → Your network → Rogers (Toronto?) → NTT → GitHub*. Where the owner changes, your traffic crossed between networks — that's where BGP decides your route. Findings below it explain:
- NAT, double NAT, carrier-grade NAT (100.64.x.x)
- tunnels (from the packet size: 1492 = PPPoE, smaller = VPN/LTE/satellite)
- silent routers (normal)
- load-balanced hops (ECMP)
- where the delay jumps
- the return path (from the reply's TTL)
- **firewalls**: a refusal that comes back faster than the server is far away means something in between said no; ping works but the port is silent means a firewall is dropping it.

If a check fails, a **sign-in page check** asks the same test page Windows uses (`msftconnecttest.com`) to catch hotel and airport Wi-Fi portals.
**Pin** up to 5 destinations and **Run all** checks them in parallel.

### 5.16 Health check (`HealthCheck`)
One click: uptime, pending reboot, disk space, adapter errors, Wi-Fi neighbourhood, proxy, VPN, DNS, memory, power — each with what to do.

### 5.17 What changed (`Timeline`, `History`)
- The diary of the last 2 hours: roams, drops, DNS changes, VPN flaps, app bursts, verdict changes.
- **History** (on by default, kept 3 days; 1–90 in Settings): one line a minute (worst level, averages and peaks) plus every event, one file per day in your profile; finished days are compressed (~15 KB a day) and the oldest are deleted every night. *What changed* shows day buttons and **the day at a glance** — the verdict as a coloured band, router delay and Wi-Fi signal as lines.
- **The week:** 7 × 24 squares, each the worst moment of that hour; click one to replay that hour.
- Also here: data usage per network (with an optional monthly cap), what's normal on this network, latency under load, after-call summaries, suggestions and new devices.

### 5.17b Overview, Investigate and evidence (`Investigate`, `Evidence`)
- **Overview** is the start page: the verdict, router / internet / DNS / traffic right now, what needs attention, today, and where everything lives.
- **What's wrong?** is for when you can't name it: this PC → the link → your router → the internet → names → the way in, stopping at the first broken layer. It reuses what Monitor already measured and re-tests only what is stale (a few pings, TCP when ping is blocked, one DNS question, Windows' own connectivity page). A layer that works but is weak is named, never "all clear"; events from around then are listed as things that happened, not causes.
- **Investigate a problem** starts from what people say: *the internet is slow*, *a website or app won't open*, *calls freeze*, *I keep disconnecting*, *works at home but not on VPN / at work*, *my PC is slow*. Each chains checks AdeeboMeter already has and answers **Finding · What I observed · Not established · Next test**, with a button to the page that helps.
- **Evidence labels** on every finding: *measured* (AdeeboMeter timed it), *Windows-reported*, *seen in packets*, *inferred* (a conclusion from the others) and *unavailable* (this PC doesn't let us see it).

### 5.17c Network story (`FullView.Story`, `Routes`, `M365`, `Packets`)
One picture, ready when you open it: the busiest app is picked (or type a site) and followed through seven boxes — App → Windows → Wi-Fi / Cable → Router → Provider → Internet → Server. Click a box for two lines and what's behind it: the routing table under *Router*, Teams & Office roles under *App*, packet sizes under *Wi-Fi*, the path under *Internet*, the certificate under *Server*, the PC's background chatter under *Windows*. *Time each step* runs the destination check and draws name · connect · lock · first byte as one bar; with Wireshark, the real handshake packets follow, each explainable layer by layer. Drawing the picture sends nothing.

### 5.17d Time machine: Replay (`History`)
Drag through any day of history: the five dots at that minute, the numbers against the day's middle value (the PC's disk delay and memory read back from disk too — "it was the disk, not the Wi-Fi"), and what changed 15 minutes either side; step a minute, ten, or to the next problem.

### 5.17e Watching smarter (`Brain`, `Baseline`, `Alerts`, `Watch`, `Usage`, `Calls`, `Devices`, `WifiAdvisor`)
- **Slower than usual**: learns what's normal for each network (told apart by the router's hardware address, so it works with location off).
- **Alerts** only when there's something to do; quiet hours; silent while you present or play full screen.
- **Watch and suggest**: first-time internet use, programs in Downloads / Temp or without a publisher, night-time connections, big unexpected uploads — with how to block it. It never blocks.
- **Wi-Fi without location**: a health score from the router link, link speed, adapter errors and drops; Wi-Fi power-saving advice; the walk test. The channel and band advisor, the sticky access point and look-alike networks need the Wi-Fi scan, which Windows only gives with location on.
- **Meeting replay**: one sentence after each Teams call; numbers in memory only, nothing about the call is saved.
- **New devices** come from the ARP table (nothing is scanned) and are listed in *What changed*. A pop-up is off by default; *Home networks only* announces a device with a real hardware address once AdeeboMeter has known the network for a day, at most once an hour — never on a company network or VPN, and never for phones' private addresses, which change. After a ping sweep, what it found is learned quietly. The gateway's hardware address is checked every 30 s — a change is a router takeover, noted on the timeline. Frozen windows, slow leaks and disk delay are watched on a separate low-priority thread.
- **HTTPS inspection**: says when a certificate was re-signed by a root that isn't on Microsoft's public list.

### 5.17f Learn and the knowledge pack (`Knowledge`)
The explanations (72 programs, 332 terms, 59 topics) are plain text in `knowledge/`, packed **gzip-compressed inside the exe** and unpacked only while the engineer view needs them. Every Learn topic is two lines, a picture (a step-by-step ladder) and one "if it goes wrong" line — from *the layers, in plain words* to *svchost*. **Inside Windows** shows who started whom, the boot chain, inside each svchost, and what starts with Windows.

### 5.17g The Toolbox (`FullView.Toolbox`, `PathTools`, `WhyFails`, `Toolkit`, `Protocols`, `RoutingTools`, `SysTools`, `WinEvidence`, `PcHealth`)
158 tools an engineer reaches for, in five sections (Network · Devices · Traffic Lab · Engineer · Windows), by question or as one list, each with a made-up Example to look at first, each one a click and at most four boxes, each with a label that stays while you type and the time it takes. **Nothing runs until you press Run, and Stop always works.**
- **Finding a tool:** four sections — **Network**, **Devices**, **Traffic Lab** and **Windows** — each opening on a home page with a card per group and the tools you used last (this session only). **Quick buttons** on every Toolbox page go straight to SNMP, switch ports, MTR, port test, dig, HTTP & TLS, iperf3 and ping sweep. One search covers every section and ranks its results (*wifi* opens Wi-Fi roams, *nslookup* opens dig, *traceroute* opens MTR); the arrows, Enter, F1 (explains anything), Ctrl+C (copy the result) and Esc work from the keyboard. **SNMP is one tool with view buttons** (Overview · Interfaces · Traffic · CPU & memory · Neighbours · Sensors · Walk · MIBs, Ctrl+PageDown steps through them), and the device you typed carries across. The PC checks (memory, disk, crashes, leaks, battery…) moved to **Investigate › Health**, under *Look deeper*. They're grouped by the question you're asking:
  - **Path & routing:** MTU per hop (don't-fragment pings with rising TTL, a binary search per hop, *Next hop* searches only below the hop before; it names PPPoE, GRE, VPN and silent black holes), MTR (loss and delay per hop over minutes, with load sharing, reroutes, hidden tunnel hops and delay jumps flagged), the way back (reply-TTL per hop, Record Route, the command to run from the far end), which route Windows picks, your gateway (VRRP / HSRP / GLBP virtual address, takeovers on the timeline, a slow plug-in that points at spanning tree), IPv6 routers (rogue router advertisements), routing protocols on your segment (with Wireshark: OSPF, EIGRP, RIP, IS-IS, PIM, LDP, BFD, VRRP / HSRP, extra DHCP servers, spanning-tree changes, LLDP / CDP, DTP), who announces itself (mDNS, SSDP / UPnP, LLMNR, NetBIOS — and why LLMNR is a risk), IPv6 ping and trace with the path MTU Windows learned, IPv6 vs IPv4, overlapping networks, who carries my traffic (online lookups).
  - **Is it reachable?** port test (refused vs. silently dropped), TCP ping, HTTP and TLS inspector (every header, security headers explained, the whole chain with keys and signatures, revocation only when you press it), HTTP timing repeated (name · connect · lock · first byte · download), which proxy a URL uses, Teams UDP and NAT type, SSH banner, mail server (greeting, STARTTLS, certificate — never logs in), RADIUS Status-Server (the secret signs the request, is masked and never kept), TACACS+.
  - **Speed & quality:** an iperf3 client to your own server (direction and seconds in plain words, iperf3's own options under Advanced), latency under load (bufferbloat, graded A+ to F), sustained ping with a loss timeline, DSCP-marked ping (marked and unmarked in turn), TWAMP-light to your own reflector (loss and jitter each way), nearest cloud.
  - **Names & time:** dig (any record including HTTPS / SVCB, DS, DNSKEY, TLSA and SSHFP, IPv6 reverse names, over DoH, or the delegation from the root down), encrypted DNS (DoH / DoT), hosts file, DNS hijack and the VPN's name rules, NTP, clock check, where the PC gets its time.
  - **Network devices:** neighbours (the ARP table), the DHCP lease (T1, T2, expiry, options) and IPv6 lifetimes, Wi-Fi roams and drops in 24 hours, a syslog test message, your own router (public address, CGNAT, hairpin), ping sweep (a tick-box on a company network), Wake-on-LAN, subnet calculator.
  - **Windows:** its own evidence (address conflicts, profile, adapter, DHCP), why 802.1X sign-in failed, domain controllers and their ports, Group Policy, certificates that expire soon, firewall rules for an app, what's listening, the event log explained, and the PC (memory pressure, disk, heat, drivers, frozen windows, slow leaks, crashes, background jobs, graphics card per app, what changed, battery, where the space went).
  - **Compare & watch:** before / after snapshot (what changed when you switched VPN, dock or Wi-Fi), compare two saved results (two PCs), My sites (your pinned sites: name, port, certificate days), DNS answer compare. Also under Is it reachable?: HTTP request builder (method, headers, body; redirects shown, not followed), STUN (your public address and whether NAT kept the port), many ports for one service, every IP behind a name, Remote Desktop reachable? (the RDP negotiation, no sign-in), Active Directory / LDAP reachable? (the rootDSE, read anonymously, with the clock difference).
  - **Traffic Lab:** test traffic you build yourself — ICMP builder (size, don't-fragment, TTL, gap; IPv4 and IPv6), UDP and TCP senders (text or hex), ARP who-has, DHCP options from the server (an information request through Windows - your lease isn't touched), SNMP test trap (send only), NetFlow v5 test record, Wake-on-LAN with SecureOn, syslog with facility.severity, iperf3, bufferbloat and DSCP ping. One target, at most 10 packets a second and 1,000 in all; on a company domain or VPN nothing is sent without a deliberate tick; Stop works; every send is logged.
  - **Devices:** name of a device (NetBIOS, LLMNR, mDNS, PTR), shared folders on a PC or NAS (as your account sees them), and a remote access page: Windows' own ssh / scp / sftp / ssh-keyscan / ssh-keygen, curl and Test-NetConnection, filled in with your host, user and port — click to copy, nothing runs from AdeeboMeter, no password is typed here.
  - **Certificates & files:** inspect a certificate or chain, check a PFX (in memory only; the key is never exported, the password not kept), decode a CSR, convert PEM ⇄ DER (a new file next to it; files with a private key are refused), hash a file and compare with the published hash, decode Base64 or a JWT, and a certutil / PowerShell / openssl commands page. Plus services that failed to start (7 days of the System log).
  - **Quick tidy** (in Health): old temp files, the Recycle Bin, disk space, startup programs, frozen or memory-hungry apps, recent installs — each with a button that opens the Windows screen. It deletes, ends and changes nothing.
- **One target:** a host typed into one tool is waiting in the next one that looks at a destination.
- **Compare with the last run:** run a tool again with the same inputs and every row shows what it said before (memory only).
- **Copy as text / Save...:** any result, ready for a ticket — as text, or .csv for a spreadsheet (cells can't become formulas); made when you press it, saved only where you choose, never with a secret in it.
  - **SNMP (read-only):** v2c and v3 (no auth, MD5 / SHA-1 / SHA-256, AES-128 privacy; keys localised as RFC 3414 says - MD5 and SHA-1 checked against its test vectors, SHA-256 against an independent computation). Views that answer questions — what is this device, interfaces with problems first, traffic per port, CPU and memory, LLDP neighbours, sensors — and a GETBULK walk of any OID or MIB name (2,000 values at most, paced), rebuilt into tables joined by index with the useful columns first. Your own MIB folder is read by a strict local parser (names, tables, enumerations, descriptions — nothing in a MIB is run) into a small index, rebuilt only when the folder changes; search it and walk a table in one click. Never SET, never traps, never listens.
- AdeeboMeter is always the client: it **never becomes a server** (iperf3 and TWAMP need yours), never logs in anywhere, and never sends a routing protocol packet.

### 5.17h2 Security (`Security`, `FullView.Security`)
One **Security checkup** button and a scorecard — risky · worth a look · good · info / can't tell — problems first; click any check to run it again; Copy as text. **42 checks, no admin:**
- **This PC (27):** antivirus on and up to date, the firewall per network type (or another firewall that is actually on), BitLocker, Secure Boot, UAC, Windows Update age and a pending restart, whether this Windows version still gets security fixes (Microsoft's dates, built in; newer builds say "can't tell"), SMBv1, SMB signing, Remote Desktop and NLA, Remote Assistance, LLMNR / NetBIOS / mDNS, WPAD, ports open to the network, root certificates added just for you (interception tools named), the hosts file sending big sites elsewhere, a proxy (per user or machine-wide), unsigned startup programs, scheduled tasks running from Temp or Downloads, LSA protection and Credential Guard, memory integrity, SmartScreen, AutoPlay, an everyday admin account, screen lock, saved open Wi-Fi that joins by itself, the PowerShell script policy.
- **Your network (11):** this Wi-Fi's security, DNS honesty (does a name that doesn't exist get an answer?), encrypted traffic inspected on the way, ARP spoofing signs, a second DHCP server, router UPnP, the router's admin page over plain http, router Telnet / FTP / TR-069, a public IPv6 address, a DNS leak while on VPN, new devices.
- **Your sites (4):** certificate expiry, old TLS still accepted, security headers, SPF / DKIM / DMARC — for the site you type and the ones you pinned.
- **Defensive only:** it reads settings. It never tries a password, never looks you up in a breach database, never scans beyond your own router, and changes nothing — every fix opens the Windows screen (or your router's page) where you act. The two checks that ask the internet on their own (DNS vs an encrypted resolver, TLS interception) run only with online lookups on.

### 5.17h Online lookups (`Online`) — opt-in, timed
Off by default. Switched on in Settings (or from the tool that needs it) for 5 minutes to 3 hours, 30 by default; a header chip shows the time left and turns them off, and switching off clears what was looked up. They send **public IP addresses and prefixes only** — never private ones or names you typed — over HTTPS to RIPEstat and PeeringDB, and give: the network (AS) and owner of every hop, handovers and internet exchanges, the destination's prefix and RPKI, recent route changes, and how the internet reaches you.

### 5.18 Live capture (`LiveCapture`)
Only if Wireshark (tshark + Npcap) is installed, and **only while you press Start**. It stops by itself after 15 minutes (Settings), and when you leave the tab or close the view.
- tshark decodes your PC's own traffic into fields; AdeeboMeter adds up:
  - **protocol hierarchy** (what your traffic is made of)
  - **top conversations**, named from the TLS server name or DNS answers in the traffic itself
  - **Wireshark's expert warnings** (retransmissions, duplicate ACKs, zero window…)
  - **bytes per app** (each packet's local port matched to the app that owns it)
  - **call quality** — RTP sequence numbers give loss; arrival time vs. RTP timestamp gives jitter (RFC 3550); the ITU-T E-model turns them into a MOS score. Encrypted audio stays encrypted; only headers are read.
  - **rogue DHCP** — two DHCP servers answering on one network.
- A small ring buffer (5 × 20 MB) lets *Sniff* include what happened *before* you clicked.
- tshark is restarted if it ever grows past 300 MB. If your Wireshark is too old for the extra fields, it restarts without them instead of failing.

### 5.19 Settings and About
- **Settings** shows the cost of every background job and what you lose by turning it off.
- **About** tells this whole story in short, and lists **live** exactly what leaves your PC with your current settings.

### 5.20 `SelfTest` — proof, not promises
`AdeeboMeter.exe --selftest` runs 71 checks and writes `AdeeboMeter-selftest.txt`. Among them:
- the counters, socket tables and DNS reading work
- **never listens**
- input validation rejects bad destinations
- the engineer view opens in under 2 seconds
- **no handle leaks over 30 open/close cycles** — and, if one appears, it re-runs page by page to name the culprit
- every label has an explanation
- the parsers (Wi-Fi lists, Wi-Fi information elements, LLDP/CDP, event logs, RTP call quality, the path story) give the right answers on known samples
- memory stays inside the 64 MB budget
- DLLs load only from System32

`AdeeboMeter.exe --deeptest` is the hardening run (`FullView.DeepTest`, writes `AdeeboMeter-deeptest.txt`, at low priority with a 20-minute cap; `--local` keeps it off the internet): every tool gets 15 hostile inputs in every box (empty, 5,000 characters, injection strings, impossible addresses, emoji, null characters) and must answer without throwing or hanging; the safe tools run for real; the tools that grew run 15 times in a row and must give memory and handles back; and the Toolbox is stressed — every tool opened twice without new window objects, search, folding, a 600-row result, every section three times. The tools that need your own gear are on a by-hand list.

---

## 6. What leaves your PC (and what never does)

| What | When | Where to |
|---|---|---|
| Router ping | every 2–10 s (Settings) | your own router |
| Internet + DNS check | every 15–60 s or off | 1.1.1.1 / 8.8.8.8 and your DNS server |
| Provider name | once per network (if the internet check is on) | a 6-hop trace toward 1.1.1.1, one reverse-DNS lookup |
| Wi-Fi details | Settings | nothing leaves — Windows is asked locally |
| Checks you start | when you press Check, Investigate or Time each step | where you asked, plus one DNS question to 1.1.1.1 |
| DNS speed test | when you press it | about 40 questions to 1.1.1.1, 8.8.8.8, 9.9.9.9 and your DNS |
| Pinned destination | opt-in, every 30 s | the destination you pinned |
| Check for updates | when you click it in About | opens the website in your browser - the app itself never checks |
| Sign-in page check | only if your check fails (can be off) | `msftconnecttest.com`, the page Windows itself uses |
| Live capture | only while you press Start | nothing leaves — it listens to your own adapter |
| Toolbox | only when you press Run | the address you typed (or the one the tool names on screen: your gateway, Microsoft's Teams relay for STUN, time.windows.com, Cloudflare / Google for encrypted DNS, the root DNS servers for a trace) |
| What's wrong? | when you press it | only what's stale: a few pings to your router and 1.1.1.1, TCP 443 when ping is blocked, one DNS question, Windows' own connectivity page |
| Security checkup | when you press it | your own router (a few ports, one UPnP question), your DHCP server, your DNS server; the sites you list; with online lookups on: www.microsoft.com and Cloudflare's DNS |
| Traffic Lab | when you press Run | the one target you typed, capped at 10 packets a second and 1,000 in all; Wake-on-LAN to your own network |
| Online lookups | opt-in, 5 min–3 h, then off by themselves | public IP addresses only, to RIPEstat and PeeringDB (HTTPS) |

**Never:** cloud, account, telemetry, auto-updates, ads, crash reports sent, listening ports.

---

## 7. Security, in short

- **No shell, ever.** Tools are started by full System32 path with fixed arguments; user input is validated before it reaches anything.
- **Paths are fenced.** Capture files can only be written inside AdeeboMeter's own folder.
- **No risky parsing.** No binary serializers, no XML parser (event data is read with simple patterns), no secrets in the code, no home-made crypto.
- **TLS done right.** Only TLS 1.2/1.3; Windows validates certificates; nothing is sent over an untrusted connection.
- **DLLs only from System32** — for Windows calls, and for the whole process once it starts, so a DLL planted next to the exe (in Downloads) is never loaded.
- **Only the server you asked can answer.** UDP tools connect their socket to the server (or check the transaction ID and sender), and every reply is parsed with its lengths checked first, nesting limited and handshakes on a deadline — a hostile device on the network can't make a tool lie, hang or crash the app.
- **Child programs start in their own folder**, never in the one AdeeboMeter runs from.
- **Secrets stay put.** The RADIUS secret box is masked; the secret signs one request and is never logged, saved or kept for *compare*.
- **A provable build.** Releases are built by GitHub Actions from this source with every action pinned to an exact commit; the build job can only read the repository, and a separate release job — which never runs the build script — is the only one that can sign provenance and attach files; each file carries SHA-256 and GitHub build provenance (`gh attestation verify <file> -R adeeb111/AdeeboMeter`). No updater exists, so there is no update channel to hijack.
- **Static analysis:** the code builds clean against Microsoft's .NET security and quality analyzers with every rule switched on (`tools/analyze.sh` runs them; any finding fails it).

---

## 8. Staying light: the budget

- **Widget only:** about 0.5% of one CPU core (measured in a test VM including its own overhead), ~40 MB.
- **Engineer view open:** about 2–3% of one core, back to zero the moment you leave.
- **Memory stays flat:** fixed-size rings, capped lists (300 events, 3,000 conversations), history that deletes its oldest day every night, caches that clear themselves.
- **Nothing leaks:** windows, timers, bitmaps and helper processes are released on close — and the self-test proves it.

---

## 9. What Windows doesn't let us do (honest limits)

- **AS numbers / BGP data** need an internet service — only through the opt-in, timed online lookups. Without them, router names tell the "who carries my traffic" story.
- **MPLS labels** from traceroute replies aren't handed to apps by Windows' ping API; hidden tunnel hops are inferred from reply TTLs instead.
- **Routing protocols** are only visible from a PC if they leak onto your segment, and then only with packet capture.
- **TCP/UDP traceroute** needs admin rights (raw sockets) — traces use ICMP.
- **Per-connection RTT and retransmits** need admin to switch on — per-app traffic comes from live capture instead.
- **The DNS cache is admin-only on some Windows builds** — then names come from live capture.
- **Speed tests** run only when you press them (Cloudflare's nearest, a Vultr or OVHcloud city, or your own file; capped in MB); iperf3 and TWAMP-light measure against a server or reflector you run — AdeeboMeter never listens.
- **Inside a VPN tunnel or inside your ISP** can only be inferred, never seen.

---

## 10. Files AdeeboMeter writes

| File | Where | What |
|---|---|---|
| `AdeeboMeter.settings` | next to the exe | your choices |
| `AdeeboMeter.log` | next to the exe | start/stop and errors, max 256 KB, no window titles or traffic |
| `AdeeboMeter-selftest.txt`, `-deeptest.txt`, `-perf.txt` | next to the exe | only when you run `--selftest`, `--deeptest` or `--perf` |
| `captures\` | `%LOCALAPPDATA%\AdeeboMeter` | live-capture ring and sniffs — deleted when capture stops, at the next start and at logoff (*Keep this capture* copies the newest where you choose) |
| `mibs\`, `mib-index.txt` | `%LOCALAPPDATA%\AdeeboMeter` | MIB files you put there yourself, and the small index of their names (rebuilt only when the folder changes) |
| `history\` | `%LOCALAPPDATA%\AdeeboMeter` | one line a minute, kept 3 days by default (Settings 1–90), or cleared with one click |
| `usage.txt`, `baselines.txt`, `seen-apps.txt`, `devices.txt`, `space.txt` | `%LOCALAPPDATA%\AdeeboMeter` | daily data use, what's normal per network, programs and devices already seen, free disk space once a day |
| `AdeeboMeter-crash.txt` | next to the exe | only after a crash; nothing is sent - *Report this* opens a pre-filled GitHub issue you can read first |

---

## 11. Build, install and release

You need nothing but Windows (10 or 11, .NET Framework 4.8 is built in). Double-click **`build_exe.cmd`**: it compiles
`AdeeboMeter.exe` (with its icon), prints its SHA256 and runs the self-test.

**The exe installs itself** (`Setup`, `SetupWindow`). A copy started for the first time from a folder where it has never
run offers three choices: *Install*, *Just run it from here*, or — if a version is already installed — *Update* / *Open*.
Install copies the exe to `%LOCALAPPDATA%\Programs\AdeeboMeter\<version>`, brings your settings along, adds a Start
menu entry and an entry in Settings > Apps (per user, `HKCU`), asks a running copy to step aside (a named event; older
versions are stopped), then removes older versions. No administrator rights, no shell. *Uninstall* (Settings > Apps or
the tray menu) reverses every step; because a running exe can't delete its own folder, a copy in `%TEMP%` waits for it
to exit and removes the folder — it only ever removes `%LOCALAPPDATA%\Programs\AdeeboMeter`.

**A release** follows one pattern every time:

1. Change `Program.Version` in `src/Program.cs` (for example `1.1`) and add a `## 1.1 (date)` section to `CHANGELOG.md`.
2. Push, then publish a GitHub release tagged `v1.1`.
3. GitHub Actions runs `release.ps1` on a clean Windows machine and attaches `AdeeboMeter-<version>-win64.zip`
   (the exe, READ ME, the Theory of Everything PDF, SHA256SUMS), `AdeeboMeter.exe`, the source zip and `SHA256SUMS`;
   the release notes are the CHANGELOG section. The website's download button always points at the latest release.

---

*Created by Adeeb Khan. AdeeboMeter — is it me or the network?*
