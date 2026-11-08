# AdeeboMeter — The Theory of Everything

*How every part of AdeeboMeter works, in plain words. Version 1.0.*

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
| **One readable file** | The whole app is one C# file (`AdeeboMeter.cs`) built by the compiler that already ships inside Windows. Nothing to install, nothing hidden. |
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
   draws the Snap as an image,               pages: Connections, Your Wi-Fi,
   faded, click-through                      What's running, Check a destination,
                                             Health check, What changed,
                                             Live capture, Settings, About
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
- **Presets:** *Penny* (~18 checks a minute), *Balanced* (~36, default), *Engineer* (~74). These counts are on top of the once-a-second counter read the widget always does.

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
- If 7-day history is on, it writes one small line to today's history file.

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

- **Header:** the verdict, the day/night switch, *About*, *Settings*, *Back to corner*.
- **The path strip:** five dots. **Click a dot** to jump to the page that explains it (PC → What's running, Wi-Fi/LAN → Your Wi-Fi, the rest → Check a destination).
- **Side panel:** TCP/IP health, CPU/memory/disk/download bars, top apps by memory and CPU. It shows fewer rows on small screens instead of cutting them off.
- **Hover explanations:** wherever the mouse turns into a **?** cursor, a card explains it — 114 terms and 72 programs. Each page records "hot spots" while it paints; the mouse just looks them up.
- **Small screens / high scaling:** tabs switch to short names, extra filter chips hide, and the side panel shrinks to 30% of the width.
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
- **7-day history** (off unless you turn it on): one line every 10 s plus every event, one small file per day in your profile, deleted after 7 days. *What changed* then shows day buttons and **the day at a glance** — the verdict as a coloured band, router delay and Wi-Fi signal as lines. The chart is drawn once into an image, so scrolling costs nothing.

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
`AdeeboMeter.exe --selftest` runs 27 checks and writes `AdeeboMeter-selftest.txt`. Among them:
- the counters, socket tables and DNS reading work
- **never listens**
- input validation rejects bad destinations
- the engineer view opens in under 1 second
- **no handle leaks over 30 open/close cycles** — and, if one appears, it re-runs page by page to name the culprit
- every label has an explanation
- the parsers (Wi-Fi lists, Wi-Fi information elements, LLDP/CDP, event logs, RTP call quality, the path story) give the right answers on known samples
- memory stays inside the 64 MB budget

---

## 6. What leaves your PC (and what never does)

| What | When | Where to |
|---|---|---|
| Router ping | every 2–10 s (Settings) | your own router |
| Internet + DNS check | every 15–60 s or off | 1.1.1.1 / 8.8.8.8 and your DNS server |
| Provider name | once per network (if the internet check is on) | a 6-hop trace toward 1.1.1.1, one reverse-DNS lookup |
| Wi-Fi details | Settings | nothing leaves — Windows is asked locally |
| Checks you start | when you press Check | where you asked, plus one DNS question to 1.1.1.1 |
| Sign-in page check | only if your check fails (can be off) | `msftconnecttest.com`, the page Windows itself uses |
| Live capture | only while you press Start | nothing leaves — it listens to your own adapter |

**Never:** cloud, account, telemetry, updates, ads, crash reports, listening ports.

---

## 7. Security, in short

- **No shell, ever.** Tools are started by full System32 path with fixed arguments; user input is validated before it reaches anything.
- **Paths are fenced.** Capture files can only be written inside AdeeboMeter's own folder.
- **No risky parsing.** No binary serializers, no XML parser (event data is read with simple patterns), no secrets in the code, no home-made crypto.
- **TLS done right.** Only TLS 1.2/1.3; Windows validates certificates; nothing is sent over an untrusted connection.
- **DLLs only from System32.**
- **Static analysis:** the code builds clean against Microsoft's .NET security and quality analyzers with every rule switched on.

---

## 8. Staying light: the budget

- **Widget only:** about 0.5% of one CPU core (measured in a test VM including its own overhead), ~40 MB.
- **Engineer view open:** about 2–3% of one core, back to zero the moment you leave.
- **Memory stays flat:** fixed-size rings, capped lists (300 events, 3,000 conversations, 7 days of history), caches that clear themselves.
- **Nothing leaks:** windows, timers, bitmaps and helper processes are released on close — and the self-test proves it.

---

## 9. What Windows doesn't let us do (honest limits)

- **AS numbers / BGP data** need an internet service or a database — left out on purpose. Router names give the "who carries my traffic" story instead.
- **TCP/UDP traceroute** needs admin rights (raw sockets) — traces use ICMP.
- **Per-connection RTT and retransmits** need admin to switch on — per-app traffic comes from live capture instead.
- **The DNS cache is admin-only on some Windows builds** — then names come from live capture.
- **Speed tests** would need a server or a listening port — not offered.
- **Inside a VPN tunnel or inside your ISP** can only be inferred, never seen.

---

## 10. Files AdeeboMeter writes

| File | Where | What |
|---|---|---|
| `AdeeboMeter.settings` | next to the exe | your choices |
| `AdeeboMeter.log` | next to the exe | start/stop and errors, max 256 KB, no window titles or traffic |
| `AdeeboMeter-selftest.txt` | next to the exe | only when you run `--selftest` |
| `captures\` | `%LOCALAPPDATA%\AdeeboMeter` | live-capture ring and sniffs — deleted on exit |
| `history\` | `%LOCALAPPDATA%\AdeeboMeter` | only if 7-day history is on — deleted after 7 days or with one click |

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

1. Change `Program.Version` in `AdeeboMeter.cs` (for example `1.1`) and add a `## 1.1 (date)` section to `CHANGELOG.md`.
2. Push, then publish a GitHub release tagged `v1.1`.
3. GitHub Actions runs `release.ps1` on a clean Windows machine and attaches `AdeeboMeter-<version>-win64.zip`
   (the exe, READ ME, the Theory of Everything PDF, SHA256SUMS), `AdeeboMeter.exe`, the source zip and `SHA256SUMS`;
   the release notes are the CHANGELOG section. The website's download button always points at the latest release.

---

*Created by Adeeb Khan. AdeeboMeter — is it me or the network?*
