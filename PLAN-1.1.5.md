# AdeeboMeter 1.1.5 - plan

Status (12 Nov 2026): built on branch dev-1.1.5 and reviewed (three outside reviews, findings fixed); not released - Adeeb releases it. Designed 11 Nov 2026. Mockups: the "AdeeboMeter Gamer Zone" design canvas (6 artboards).
One release, everything together (Adeeb's rule). Nothing is built, tested, committed or published until he says go.

## Rules every item keeps
No admin · never listens · offline by default (third parties only after the user asks or opts in) · nothing stored beyond
what each feature documents · SNMP read-only · secrets never kept · C# 5 / .NET 4.8 · no outside vendor names anywhere.

## 1. Microsoft Store version (branch `store`, commits 2501630 + 75c3bdb)
- Done on the branch: Store detection (GetCurrentPackageFullName), writable files in %LOCALAPPDATA%\AdeeboMeter when packaged,
  self-install / uninstall / Run key off, "Start with Windows" = package startup task (on by default; Settings > Apps > Startup),
  manifest with the Partner Center identity (AdeebKhan.AdeeboMeter, CN=0FFED5C5-A469-4C68-9855-2168DBCE607F), icons,
  store/pack.ps1, CI artifact `AdeeboMeter-store`, docs/privacy.html (on main).
- To do: merge `store` into the 1.1.5 work; MSIX version 1.1.5.0; test the startup task and AppData paths on a real install;
  first Store submission is 1.1.4.0 (in review) - 1.1.5.0 is the first update.
- Website: main button -> "Get it from Microsoft Store" once the listing is live; exe + winget stay as smaller links.

## 2. Gamer Zone (new tab, between Toolbox and Network story)
### Your games (first open)
- Installed games are found automatically each time the tab opens, by READING the launchers' own lists on this PC:
  Steam (libraryfolders.vdf + appmanifest_*.acf), Epic (ProgramData\Epic\EpicGamesLauncher\Data\Manifests\*.item),
  Riot (ProgramData\Riot Games\RiotClientInstalls.json / metadata). Read-only, size-capped, parsed strictly, nothing sent.
- They appear as tiles marked "On this PC · click to monitor". ONE click = consent: that game starts monitoring, and from
  then on also starts by itself when the game opens. Nothing goes to a game company's servers until that first click.
- Other games: tile grid + search + "+ Add your own". Starter list (verify each before shipping): Valorant, Counter-Strike 2,
  Fortnite, League of Legends, Apex Legends, GTA Online, Call of Duty, Overwatch 2, Rocket League, Dota 2, Roblox,
  Marvel Rivals, Minecraft (your server). GTA VI greyed "coming when it launches on PC" (no PC date announced, Nov 2026).
### The tab
- One card per monitored game; per server: ping now / average / worst / p95, jitter, loss, spike count, sparkline,
  grade Competitive / Casual / Laggy, and HOW it was measured (ICMP / TCP port / captured).
- Best region for you = the last 7 days (stability per evening), not the current ping. Per-day squares per region.
- Why did it spike? Each spike matched against router delay and Wi-Fi signal at the same second -> "your Wi-Fi" /
  "your provider" / "past your provider (game server side)".
- This match: start/stop with the game's .exe; loss, spikes, how many were Wi-Fi, a tip; Copy / Save / Report for my ISP.
- Find my match server (only with Wireshark/Npcap installed): short capture while playing -> the real server IP, add in one click.
### Edit games & servers
- Per game: name, program (.exe; "Pick from running programs", optional), auto-start tick.
- Per server: name, location, host name or IP (host names preferred - resolved fresh, so IP changes don't break lists),
  method Auto (ICMP, fall back to TCP connect) / ICMP only / TCP port N. Strict validation (host/IP only), plain error text.
- Add / remove / Test these now / Import / Export (text file) / Reset to defaults.
### Widget while playing
- Game, region, big ping, grade, sparkline, jitter / loss / Wi-Fi. Setting: Show game ping on the widget.
### Measuring and caps
- Background game ping like the internet check: default every 30 s, up to 3 servers (each ticked game's best region).
- While a game is open: 1 probe/s per server. Hard caps: 8 servers at once, 10 probes/s in total.
- Game UDP traffic can't be seen without admin; numbers are ICMP/TCP probes to the region, labelled as such.
### Research needed before building (cite sources, store a "checked on" date per preset)
- Per game: regions, ping-able endpoints (host names), the right method, program names. Fortnite / Rocket League = AWS
  regions (easy). Riot, Valve SDR, EA, Blizzard, Activision, Roblox: check what answers ICMP/TCP today.

## 3. Report for my ISP (general + gamer; button on the match card and in Investigate)
Sections: finding in one line · each part of the path (router / ISP first hop / ISP network / server: avg, p95, jitter,
loss) · hop-by-hop with owner (ASN) + "loss that doesn't continue isn't real" note · loss by hour of day, 7 days
(heatmap) · same time, two routes (which hand-off is congested) · line details (IPv4/IPv6, path MTU, DNS; cable modem
SNR / power / uncorrectable errors read from 192.168.100.1 when it shows them without a password) · exact targets (server
IP:port, bad hop IP + rDNS, hand-off IP + AS) · bufferbloat grade · already ruled out · worst moments · ready-to-paste
message for the ISP chat · how it was measured. Save as PDF, Copy as text, attach raw CSV.
- Public IP only when "Include my public IP" is ticked (one online lookup). Off by default.
- The two-routes section only draws a conclusion when the routes really leave via different hand-offs.

## 4. History kept for the Gamer Zone
- Game ping results stored with the existing history (%LOCALAPPDATA%\AdeeboMeter\history), "Keep game history for"
  7 / 14 (default) / 30 days, cleared with the rest. Privacy page + docs updated to say so.

## 5. Settings, rearranged
- Search box at the top; side menu: General · Widget · Background checks · Gamer Zone · Alerts · History & data ·
  Privacy & online lookups · Capture & advanced; Export / Import / Reset at the bottom of the menu.
- Every setting keeps a tag: On this PC / Background · your network only / Background · sends to game servers / Online lookup.
- Top line always says what this PC is sending right now ("router ping every 5 s · internet check every 30 s · ...").
- Gamer Zone settings: game ping in background Off/10/30/60 s · servers watched 1/3/5 · faster while a game is open ·
  start when a game opens · game ping on widget · keep game history for.

## 6. Other 1.1.5 items (agreed in principle)
- Simple / Pro mode (first-run choice; Simple = dots, What's wrong?, speed, history, Gamer Zone).
- Speed test only on click (endpoint to research; counts as an online action the user asked for).
- Outage log + Windows notification ("Internet dropped 18:42-18:47").
- Fix Find devices: 0 devices on Adeeb's home network (11 Nov) - check Windows Firewall dropping multicast replies to an
  unlistening client socket; fall back to ARP table + NetBIOS/mDNS names.
- Leftover gaps: SNI option in HTTP & TLS, UDP option in MTU, ICMP builder "compare with unmarked", App connections upgrade,
  TCP fallback for the Internet dot.
- Toolbox cold-open performance.
- In-app Support / Donate link (About page), same PayPal link as the website.
- Signing: Certum Open Source (about EUR 49/yr) only if Adeeb buys it - sign.ps1 + release flow that keeps hashes and
  attestation honest. 1.1.4 is never re-signed (winget checked its hash).
- winget manifest for 1.1.5 (and the msstore source once the Store listing is live).

## Open questions
- Speed test endpoint (Cloudflare speed vs the user's own iperf3 server only).
- Cable-modem page: which models to support first (Rogers / Comcast common ones).
- How many starter games ship in 1.1.5 vs later.

## 7. Also in 1.1.5 (added 11 Nov, Adeeb: "except growth, add the others")
- Languages: French first (Canada); Spanish, Hindi, Bengali after a native speaker reviews them. Step 1 is moving every
  UI string into a translation table (en is the fallback); a language only ships when its file is complete.
- Windows on ARM: ARM64-native build + Store package (arm64 alongside x64).
- VPN / company proxy awareness: recognise the common VPN / zero-trust clients and proxies from local adapters, routes
  and processes; say "your VPN is the slow part" with its own latency layer.
- For IT teams: `AdeeboMeter.exe --report out.json` (no window, exit code = verdict) and an admin preset file
  (AdeeboMeter.policy next to the exe or in ProgramData, read-only: lock the internet check, hide the Traffic Lab, etc.).
- Gamer extras: streamer lag graph (a small borderless always-on-top window or transparent PNG for OBS);
  Wi-Fi vs Ethernet / two PCs side by side for the same game server.

## 8. Widget layouts (agreed 11 Nov)
One widget engine (sleep / unlock / multi-monitor / fade / chip fixes shared), several layouts:
- Everyday (today's widget, default) · Gamer (game + region, big ping, jitter, loss, grade, sparkline, one-word blame on a
  spike: Wi-Fi / ISP / server, plus a thin five-dot strip) · Calls (Teams / Zoom: jitter, loss, VPN layer) · Engineer
  (retransmits, router ms, DNS ms, throughput; Pro mode only).
- Setting "Widget style": Auto (follows what you're doing: Gamer while a monitored game is open, Calls during a call,
  then back) / Everyday / Gamer / Calls / Engineer.
- Shows over borderless / windowed games only (no window can show over exclusive full-screen); never injected into a game.

## 9. From GPT's review (11 Nov) - taken in, adapted to our rules
- Mark lag: a button (and hotkey) during a game; keeps the 30 s around it - game ping, router / internet ping, loss, Wi-Fi
  signal and roams, adapter traffic, CPU - and shows it as a small replay in the match report.
- Who's using the connection: at each spike, the upload / download on the adapter and the busiest process (from the
  connection / process watch we already have). "Test under load" button for after the match (bufferbloat).
- Wi-Fi vs Ethernet: the same servers, same sample count, two runs side by side (already planned under gamer extras).
- Match summary: worst spikes with their evidence; Copy report with internal addresses (192.168.x, 10.x, MACs, SSID) redacted.
- Honest labels everywhere: we can't read the game's own in-game ping - our numbers are probes to the region / server.

## Build order (waves; analyzer build + self-test after each; real-PC test at the end; commit/publish only when Adeeb says)
1. Foundation: merge `store` (done, branch dev-1.1.5) · Settings rearranged (search, side menu, tags, "sending now" line)
   · in-app Donate · outage log + notifications.
2. Gamer Zone core: research endpoints + program names · launcher detection · probe engine with caps · game history ·
   tab, cards, best region (7 days), spikes blame, match report · editor · widget layouts (Everyday / Gamer / Calls / Engineer, Auto).
3. Report for my ISP (general + gamer): path layers, hops, heatmap, two routes, modem page, PDF / text / CSV.
4. Simple / Pro mode · speed test on click · Find devices fix · leftover gaps · Toolbox cold-open speed.
5. VPN awareness · --report JSON · admin preset · ARM64 · streamer graph · side-by-side compare.
6. Network engineer pack (moved in from PLAN-1.2.md, 11 Nov - "everything in one release"): Certificate Chain Compare,
   DHCP Lease Replay, 802.1X Failure Decoder, "Why didn't this PC join the network?", RADIUS Client Lab, AAA before/after,
   IPv6 RA replay, ICMP error translator, SMTP path check, sample-capture lab.
7. Languages: string table, then French.
8. Full suite: analyzer build, self-test, 25-min deep test, Windows build on Adeeb's PC, LIVE-TESTS; then hand-off for a
   Claude Code / Codex bug review; fixes; then release when Adeeb says.

## 10. Added 12 Nov (Adeeb: "your picks, the later plan and the parts from topogram") - DESIGN, not built
Rules unchanged: no admin, never listens, nothing sent unless the user presses it (drawing sends nothing), no company names.
A. Packet journey (the Network story, extended - "protocol playground" everywhere)
   - Open it from: Network story (any app / site), Gamer Zone server row ("See the journey"), Toolbox results (MTR, ping,
     speed), ISP report hops, Learn.
   - The path is real: boxes from the trace / MTR already measured (or one short trace when you press Measure).
   - At every box the packet's label stack is drawn: data > UDP/TCP (+TLS/QUIC) > IP > (VPN outer IP + ESP/WireGuard) >
     Wi-Fi 802.11 + CCMP or Ethernet; the router rewrites addresses (NAT); PPPoE +8 when the line uses it; provider
     labels (MPLS) shown as "inferred, can't be seen from a PC". Bytes per layer and what's left of the MTU.
   - Time per segment split into what causes it: distance (fibre ~5 us/km), size on the link (serialisation), queues
     (bufferbloat - idle vs loaded ping when measured), Wi-Fi airtime and retries, processing. Jitter = how much the queue
     part moves. Every number tagged measured / Windows-reported / estimated.
   - Controls: Play / step, packet size 64-1500 (fragmentation vs MTU), protocol (game UDP, web TCP+TLS, DNS, ping),
     what-if: add a VPN, PPPoE, cable instead of Wi-Fi. "Why is my jitter 14 ms?" in plain words.
   - Call-flow cards with the same engine: DHCP, DNS, TCP handshake, TLS 1.3, QUIC, 802.1X/EAP, ARP - each linked to
     the Traffic Lab tool that builds that packet.
B. Picks for 1.1.5: what-changed-when-it-broke timeline · hotel / cafe check · Wi-Fi channel map + least crowded +
   same name different security · DNS race · saved device inventory · driver audit + USB history · MTR record + graph ·
   internet weather sentence.
C. From topogram (generic only): encapsulation stack calculator (engine of A), decimal / hex / binary / bandwidth
   converter, VLSM planner on the subnet calculator, protocol call-flow cards (A).
   - Subnet calculator gateway options (Adeeb, 12 Nov, screenshot of topogram's calculator): a third box "Gateway
     (optional)" - may carry its own mask (10.20.0.1/21). Today's "Is this IP inside?" box stays. Checks, in order:
       red   gateway outside the host's subnet -> "Will not work: the host won't ARP for it, nothing leaves the subnet"
             + the smallest prefix that holds both, and whether the gateway's own network already holds the host;
       red   gateway = the network address, the broadcast address, or the host itself;
       red   /31: the gateway must be the other address of the pair; /32: says it needs an on-link route;
       amber gateway's mask differs from the host's (one side ARPs, the other routes - works one way only);
       info  gateway isn't the first or last usable address (fine, just not the usual choice);
       blank: suggests the usual choices (first / last usable).
     IPv6: a link-local gateway (fe80::) is normal (learned from router adverts); a global gateway outside the prefix
     -> amber. Buttons: "Use this PC's" fills IP, mask and gateway from the active adapter (local read, nothing sent);
     Mask drop-down (/0 - /32 with dotted masks) beside the box; Copy result. Self-test cases for each row above.
D. Later (1.1.6+): topology map, Wi-Fi heatmap, paste output -> verdict, crash-dump reader (picked
   file), privacy checkup, full-PC HTML report, ISP report card + CCTS complaint letter, winget list (read-only),
   LAN test by SMB copy (with an "I'm allowed" tick), camera-like device check.
   - With a capture (12 Nov, Adeeb): the journey also reads real packets - the live rewind buffer (last minutes, already
     kept when Npcap is there), a Sniff, or a .pcap/.pcapng the user opens. Read on this PC only, never uploaded; our own
     reader for IP/UDP/TCP headers (filtered to the one flow), tshark when Wireshark is installed for TLS/QUIC/DNS detail.
     Adds: real sizes and stack (VLAN, DSCP, PPPoE, fragments), TCP handshake / data-to-ACK RTT, retransmits, DNS and TLS
     times, the game's own UDP packet rate and inter-arrival jitter, ICMP errors. Time-matched to the history on the same
     clock (router ping, Wi-Fi signal, roams, events, Mark lag) -> "the jitter spike at 21:04:12 is your Wi-Fi".
     Honest limits: one point of view (the PC); no per-hop timing of real traffic; game UDP loss only hinted by gaps;
     the Wi-Fi air layer isn't in a normal capture; a file from another PC has another clock (shift it, or no matching).
   - Entry for real packets is a Sniff button on the journey itself (Adeeb, 12 Nov): "Capture this conversation" -
     takes the last minutes from the rewind buffer if it holds this flow, otherwise sniffs just this flow for 15-60 s
     (you pick; Stop works), then redraws the journey with real packets. No Npcap: the button says so and offers
     "Open a capture file" instead.
   - Live translation (Adeeb, 12 Nov): while capturing, a running list in plain words beside the journey - one line
     per meaningful step ("your PC asked where riotgames.com is", "the answer: 3 addresses, 18 ms", "hello / hello back /
     ok - connected in 24 ms", "encrypted game data, 128 packets a second, 90-140 bytes"); busy streams are grouped
     per second, never one line per packet. Click a line -> the packet's stack and raw fields (Details). Encrypted
     content is described, never decoded. Through the language table (French too). Needs Npcap; nothing saved.
   - The diagram itself plays the real packets (Adeeb, 12 Nov): each captured packet travels the path as a block sized by
     its bytes and coloured by kind (DNS, handshake, data, ACK, game); a lost one fades out with "?" (a PC can't see where;
     the segment is named only when the time match points to it, e.g. Wi-Fi), its resend follows marked "sent again";
     gaps show as empty road; fragments split into pieces at the MTU. Slow motion (real time is too fast to see: 38 ms),
     pause, step, scrub back through the capture; busy streams sampled (about 20 blocks a second drawn).
   - Weight (Adeeb, 12 Nov: "100 packets then stop"): capture filtered to the one flow (dumpcap, BPF), our own reader for
     headers only (no payload kept); tshark only for DNS/TLS detail and only if installed. Stops by itself at a packet cap
     or a time cap, whichever first: Quick = 100 packets, Normal = 1,000 packets / 30 s (default). Animation draws at most
     ~20 blocks/s at 30 fps, only while the journey is on screen; paused when hidden. Target: under 5% of one core while
     capturing, nothing after it stops - measured in the deep test before release.
   - Any connection, not just games (Adeeb, 12 Nov: "forget gamer, helpful everywhere"): the journey opens on any row
     of the connection list - browser, Teams / Zoom call, VPN, OneDrive upload, printer, DNS lookup. Game wording lives
     only in the real-time rules.
   - Filter: Sniff's own BPF (host + both ports, applied in the Npcap driver - nothing else reaches us) widened by
     "or (icmp and host X)" so "too big" / "unreachable" replies about this flow are kept. Rewind buffer supplies the
     seconds before the click, matched by time.
   - Honesty for UDP: no headers say a UDP packet was lost; we show gaps / late / bursts and claim loss only where the
     protocol carries sequence numbers (RTP, QUIC acks not readable - so not there either) or TCP shows resends.
   - "Explain this capture" button (moved here from D): our own fixed rules, no AI, nothing leaves the PC, runs only
     when pressed on packets already in memory (well under 1 s). Step 1 decides the kind from port, protocol, rhythm and
     sizes: real-time (call, game, stream) · download / upload (TCP bulk) · web page (TLS / QUIC) · DNS · VPN / tunnel
     · local device (printer, NAS, SMB) · unknown (shared rules only, says so). Step 2 applies shared rules (ICMP errors,
     TTL / route change, this PC sending late, Wi-Fi scan gaps) + that kind's rules:
       real-time: gaps, jitter, bunching after stalls, RTP loss / MOS when present
       download: resends, dup ACKs, window size / far-end window cap, slow start, throughput over time
       web: TCP / TLS handshake time, time to first byte, QUIC vs TCP
       DNS: time to answer, retries, which server answered
       VPN: inner packet size vs MTU, keepalives, tunnel resets
       local device: ARP, how long the device takes to reply
     Output: a one-line verdict, the evidence (every claim links to the packet time in the journey), and a "can't tell
     from here" line - encrypted contents are never guessed. Simple / Expert wording. Copy uses the ISP report redaction.
E. Two plain-language questions (Adeeb, 12 Nov) - guided checks that end in a verdict, each its own card on the
   Toolbox question page and searchable ("usb", "lag", "laggy", "stutter", "rubber band"):
   1. "Why isn't my USB device working?" (on "Is it this PC?") - read-only, no admin:
      - the device's Windows problem code in plain words (CfgMgr32: e.g. 10 can't start, 28 no driver, 43 Windows
        stopped it / bad descriptor, 45 not plugged in now) and its driver name / version / date (shares the driver audit
        from B);
      - when it last connected and dropped (Kernel-PnP / USB history from B) - "dropped 6 times in the last hour" points
        at cable, port or power;
      - power: USB selective suspend on, hub power budget exceeded, laptop on battery saver;
      - network USB devices get the network checks too (USB Wi-Fi / Ethernet adapter, phone tethering: link up?, IP?,
        driver resets).
      - Verdict + one next step (try another port / cable, update or roll back driver - opening Device Manager, never
        changing anything ourselves). Honest: we can't test the device's own hardware. Read-only APIs to be confirmed
        on a real PC without admin before building.
   2. "Why is my game lagging?" (on "Why is it slow?") - joins pieces we have:
      - is a game open (Gamer Zone list), which server / region it talks to;
      - quick split: this PC -> router -> ISP -> game server (ping + loss + jitter per leg) => which leg is at fault;
      - at the same time: Wi-Fi signal / band / roaming, the busiest process uploading or downloading, PC pressure
        (CPU, memory, disk);
      - offers "Capture this conversation" on the game's flow, then Explain (D/A above).
      - Honest labels: we can't read the game's in-game ping or frame rate. If every network leg is clean while it
        still lags, the verdict says so: "network looks fine - likely the PC or the game itself (frame rate)".
   - Pattern for later questions of the same kind ("Why does my call break up?", "Why won't my printer print?"):
     guided checks -> verdict -> evidence -> one next step. Same rules engine as Explain.

## 11. UI refresh: life, colour, a living companion (designed 13 Nov) - DESIGN, not built
Adeeb: "looks dull", "no one reads when there is too much writing", "compare topogram - the UI is way off".
Goal: every page reads in a glance - colour and an icon per section, short labels, pictures instead of paragraphs, a buddy
that reacts. Nothing an engineer needs is lost: the Details switch brings every number back.

### 11.1 Two layouts, one set of pages (Settings + a header icon, instant, no restart)
```
Classic (default)                         Sidebar (topogram style)
┌ logo · buddy line · mood · Details ┐   ┌──────┬ buddy line · mood · Details ┐
│ coloured top tabs with icons       │   │ navy │                              │
│ page ......................... ▐▌ │   │ nav  │ page                         │
│               slim health strip ▐▌ │   │ ──── │                              │
└────────────────────────────────────┘   │meter │                              │
                                          └──────┴──────────────────────────────┘
```
- Only the frame differs (navigation + where the health box sits). Pages, cards, tool pages, colours and lines are shared -
  a fix is made once.
- The grey right panel (TCP/IP health, This PC, Top apps) becomes a slim icon strip (Classic) or the sidebar meter
  (Sidebar); it expands on click. Pages get about 25% more width.

### 11.2 The topogram recipe (read from topogram v2.3 paint.py / topogram_app.py)
| Take | How |
|---|---|
| Inter font (Regular / SemiBold / Bold) | embedded in the exe, subset to Latin + French (~300 KB), PrivateFontCollection; OFL notice in About |
| Grey anti-aliased text | TextRenderingHint.AntiAlias everywhere (today ClearTypeGridFit - thin, "old school") |
| One palette | navy #0b1b3a, aqua #3de0c2, page #f5f7fb, card #fff, line #e3e8f0, ink #0b1b3a + light tints; dark theme mapped too |
| Cards | 16 px corners, soft 4-layer shadow instead of a border, optional 5 px colour stripe |
| Buttons / chips / switch | all drawn (no mixed WinForms buttons): 10 px corners, weight 600, 38 px; switch 38x22 |
| Sidebar | navy, 44 px rows, selected = white 10% wash + 3 px aqua bar, hover 5% wash, counts as chips, meter at the bottom |
| Scrolling | 60 px a notch, touchpad-aware; long pages drawn only around what is on screen |
| 2x drawing then shrink | NOT for whole pages (redrawn every second: a 20 MB scratch picture, breaks the 64 MB budget). Only icons, buddy, logo. |

### 11.3 Colour + icon per section (same everywhere: tab, card stripe, tool page header, back button)
Investigate purple · Security pink · Toolbox amber · Gamer red · Network story blue · Time machine teal · Learn violet ·
Monitor green. Toolbox questions each get their own hue (reach teal, slow amber, break red, network blue, join purple,
keys pink, padlock green, Windows grey, test traffic coral). Colour is never the only signal (icon + text always; high contrast keeps working).

### 11.4 Header + Details switch
- Details OFF: buddy line + 3 plain chips (PC · Wi-Fi · Internet). Wi-Fi "signal -1%" becomes "hidden by Windows".
- Details ON: the full 5-dot path (PC · Wi-Fi · ISP · DNS · Internet) with every number, and pages show their full rows.
- Header also gets the Mood button (11.7) and Adeebo points.

### 11.5 Pages
| Page | Today | Becomes |
|---|---|---|
| Toolbox | 9 grey cards, tool + description | colour + icon per question, tool names only (description on hover), "+5" / ✓ points, "Right now" card when something happened, rotating search examples |
| Investigate | 6 cards of 4-line paragraphs | 6 icon tiles, one line each |
| Security checkup | long grey list "not checked yet" | grid of status tiles green / amber / red |
| Check a destination | empty box + paragraph | recent + pinned sites as chips, 8 step icons that light up as they pass |
| Gamer Zone | letter tiles, text table | tiles in the game's colour, ping bar per region |
| Monitor > Your cable | one red box | cable / Wi-Fi picture + buddy line |
| Time machine | chart good, small text | keep the chart, bigger status dots with icons |
| Learn | paragraph cards, "0 / 8 done" | icon cards with progress rings |
| Points | text list | badge wall (earned in colour, locked grey) |
| Settings | dense pill rows | picture per section; Mood + Persona + Layout get their own section |
| About | wall of text | one picture + one line per step |
| Overview | (screenshot came out black under Wine - check on a real PC) | buddy + today in 3 cards |

### 11.6 One tool page for all ~160 tools
```
← big back button (to its question, in its colour)
▌icon  Tool name · 3-word hint                         +5 points
(buddy) one line - changes with mood and result
inputs: chips / switches / sliders / drop zones   ->  big coloured GO
── after the run ──
2-3 big numbers · "what this means" icons · next-step buttons
▸ Details (the full rows) · ▸ How it works (the old paragraph)
```
Automatic swaps: path box -> drop zone + Browse · yes/no box -> switch · "a or b" text -> big choice buttons ·
host box -> box + recent hosts as chips · minutes box -> slider with quick picks · paragraph -> 3-step picture (top 20 tools
first, the rest a short line until they get one).

### 11.7 Mood + persona (replaces the hidden Voice setting)
- Mood (how it feels): Chill (default) · Roast · Hype · Support · Bossy · Emo (light, "relatable", never dark) · Plain.
- Persona (who talks): Gamer · Bestie · Pirate · Noir detective · Nature documentary · Shakespeare · Dark Ages
  (Monty Python silly) · Industrial · Revolution · Mission control · Poetic · Scientist · Coder · Network engineer · Plain.
- Changes: buddy face + colour, header sentence, tool verdict lines, empty / idle text. Never: numbers, labels, settings,
  error details, Details rows.
- Each persona = one text file of ~40 lines (header, top tool verdicts, idle, empty) x good / bad / idle; anything missing
  falls back to the mood's line. Mood picks which feeling. Total ~560 lines; all 14 packs in this release (Adeeb).
- Reports and "Copy for my ISP" always Plain. French: Plain + Chill now; other French lines fall back to Chill.
- Lines are written first and approved by Adeeb before they go in (humour lands or it's cringe).

### 11.7a Lines file, variety, how to address you, tone (13 Nov, Adeeb)
One file holds every line: knowledge\lines.txt (embedded like the other knowledge files, gzip). The code never holds a
sentence for the buddy or a summary - it asks for a key. Adding a persona or fixing a line never touches code.
```
# key            | mood/persona      | line ({addr} = how you like to be called, {v} = the measured value)
wifi.drops       | plain             | Wi-Fi disconnected {v} times today.
wifi.drops       | epic-fantasy      | {Addr}, your Wi-Fi is slower than a raven with a bad wing.
wifi.drops       | epic-fantasy      | The signal fell {v} times. The night is dark and full of dropouts.
wifi.drops       | bro               | Bro. Your Wi-Fi bailed {v} times. Not cool, bro.
sum.tool.mtr     | -                 | Loss and delay at every hop, over minutes.
sum.page.gamer   | -                 | Your games, your ping, and who to blame.
```
- sum.* keys = the one or two line summary of every page, section and tool (plain, mood-free; ~200 lines). They replace
  the paragraphs; the old paragraph moves under "How it works".
- Variety: 4-5 lines for the moments seen often (header states, busy, idle, empty, freeze guard, top tool verdicts),
  2 for the rest. Picked at random, never the same line twice in a row.
- Pace-aware: clicking or switching tabs fast picks from a "speedrunner" set ("Easy, speedrunner - I'm still on the last
  one"); long idle picks from an "idle" set. Never more than one buddy line per 10 s unless something changed.
- How should I address you (Settings, default Neutral; never guessed): Neutral (boss / friend) · Sir / My Lord / bro ·
  Ma'am / My Lady / sis · Just my name (typed, kept only in settings). Personas use it through {addr}.
- New personas: Epic fantasy (houses, dragons, ravens, "My Lady") · Space saga (rebels, the dark side, hyperspace) ·
  Bro code. Named generically on screen - "Game of Thrones" and "Star Wars" are trademarks, and their famous lines are
  theirs; we write our own lines in that style (no character names, no quoted catchphrases).
- Size: ~17 personas x (12 frequent x 5 + 28 x 2) ~= 1,900 lines + ~200 summaries. Written and approved in rounds.
Tone rules (every line is checked against these)
- A companion, never above the user: we tease the network, the Wi-Fi, the PC - never the person. Roast mode roasts
  the gear, not you.
- Never cringe: no forced slang, no "hello fellow kids", no emoji in lines, max 16 words (Adeeb, 13 Nov), one joke per line.
- The fact always survives: every line still says where the problem is (PC / Wi-Fi / router / provider / server).
- Never dark: no death, self-harm, violence or real wars; Emo stays "relatable". No mocking any group of people.
- Reports, "Copy for my ISP" and anything copied out are always Plain.

### 11.7c Companion customization (13 Nov, from Adeeb's companion plan; adapted to AdeeboMeter)
Words only; data never changes. Findings' numbers, outputs, exports and anything copied always use Plain.
Settings > Companion (Basic on top, the rest under "More")
- Basic: Mood (Chill default, Roast, Hype, Support, Bossy, Emo, Plain) · Persona (none by default; the 11.7 list + Gen-Z)
  · Address me as: Neutral (boss, default) / Sir / Ma'am / my name - chosen, never guessed.
- More: how chatty (quiet / normal / chatty) · where lines show (header, verdicts, busy/waiting, greetings - each on/off)
  · buddy face: drawn faces or an emoji set the user picks, plus colour · mix personas (up to 3, rotated) · pin a favourite line / mute a line (never again)
  · live preview of a sample line. Rule: every option must visibly change the preview, or it doesn't ship.
- Header: a "Fun lines" toggle always visible - one click to Plain everywhere (next to the Mood button).
Line files (replaces "one lines.txt" in 11.7a)
- One file per persona: `key | mood | line`; placeholders {addr} {v} {router} {port} {app} {site}.
- Several lines per key + mood: random, never the same twice in a row. Missing: persona's other mood -> Plain. Never blank.
- Built-in files embedded in the exe (single-file app), read-only. Custom / imported files in
  %LOCALAPPDATA%\AdeeboMeter\lines\ (no admin, the Store build already uses this folder); they survive updates.
Custom, import, export
- Write your own lines in any language or style on top of the built-ins; import a file; export your set to share.
- Import check says what's wrong AND offers the fix; tick the lines, one click fixes them: too long -> shortened version
  offered; unknown placeholder -> nearest valid one; missing mood -> chill; wrong separator -> fixed. Refused lines are
  listed, never silent.
- Limits: runs off the UI thread, 2 s time limit, at most 1 MB and 600 lines a file, 200 characters a line, 20 custom files.
  Over a limit: refused with the reason, the app carries on. A broken file never stops the app; built-in lines take over.
- Imported text is untrusted: shown as text only, never run; control characters and right-to-left override marks
  removed; only the known placeholders are filled. No points for importing.
Automatic lines test (self-test; replaces reviewing every line - Adeeb: "I won't review every line, keep the surprise")
- Every built-in line: at most 16 words, no emoji, valid placeholders only, every key has a Plain line, no word from a
  banned list (dark topics, slurs, trademarked character / house / place / order / weapon names, famous catchphrases).
- Adeeb reviews a handful of samples per persona, not every line.
Buddy emoji (13 Nov, Adeeb: "people change emojis; we switch them occasionally as our trick")
- The buddy can wear an emoji face set instead of the drawn face; picked in Settings > Companion.
- GDI+ draws emoji in black and white, so the sets ship as small pictures: an open emoji set under CC-BY 4.0 (Twemoji),
  about 40 faces, ~60 KB, credited in About. No emoji inside the text lines (they'd show as outlines).
- Our trick: now and then (seasons, the install's birthday, a rare day) the buddy wears a surprise emoji for a day,
  then goes back to the user's pick. Happens in every mode, Plain too (Adeeb: "that's the surprise") - it's only the face,
  never the words, and never in anything copied, saved or exported. Never permanent.
- Batch 1 lines (Plain, Epic fantasy, Bro code, Network engineer) approved 13 Nov. Batches 2-5 are NOT shown to Adeeb
  ("keep it as a surprise for me"): they go in after passing the automatic lines test and a second read against the
  tone rules by a separate reviewer (a fresh agent), so they don't grade themselves.
AdeeboMeter differences from Topogram's version of this plan
- AdeeboMeter gives advice by design (Investigate says what to try). Persona lines may restate the measured finding and
  WHERE it is (PC / Wi-Fi / router / provider); the advice itself comes from the engine's Plain rows, never invented by
  a persona.
- Default persona: none (Chill buddy) for a Gen-Z audience; Topogram defaults to Network engineer for senior engineers.
- Emo mood kept (light, "relatable"); Gen-Z persona added alongside Bestie / Bro code.

### 11.7d Additions after Grok's review (13 Nov, Adeeb agreed)
- Persona picker: "Surprise me" is the default and first choice - one persona picked at random a day (not every
  line, so it doesn't feel chaotic), then the rest: 6-8 featured, the others under "More personas". Plain and the
  faith options are never in Surprise me. Address (Neutral / Sir / Ma'am / name) only fills {addr}; it never decides
  which personas you get.
- Length per place, not per persona: header line = one line at laptop width (~70 characters); tool verdicts up to
  2 lines; busy / empty lines one line. The lines test measures each line in the font, per place.
- Plain persona names on screen (Dark Ages, Revolution, Mission control, Noir) - no "Ye Olde".
- One-time "Try the new Sidebar layout?" card after updating to 1.1.5; closed = never shown again.
- Face rule: the buddy is smaller and softer than the verdict; the path dots are the only red / amber / green on the
  page; the face shows mood by expression, never by health colour.
- Health tint: the page background shifts to a calm soft tint when green and flat grey when red - one repaint when the
  state changes, no particles, nothing moving.
- Hour mark: at the top of each hour the face does one ~1 s reaction (blink or smile); at 22:00 and 23:00 a sleepy face
  for a second. In the full window AND on the corner widget (Adeeb, 13 Nov). On the widget it stays at its fade level
  (never brightens), takes no clicks and draws ~10 extra frames once an hour. Skipped during a call, a game, a running
  tool, a full-screen app or presentation / screen share.
- "What the buddy noticed" strip on Overview: only after several days of history, only patterns seen 3+ times, only
  what was measured ("quieter since Tuesday", never "since you moved the router").
- Shareable cards (award, roast, ISP letter, recap) hide network name, IPs, provider and place by default; an explicit
  switch includes them.
- Milestone hats: earned from real milestones (30 days of history, first clean security checkup, first ISP letter...),
  on top of the points hats; never taken away.
- "Just the facts" one-click in Companion: Plain, Details on, buddy face only.
- How chatty: Face only / Quiet / Normal / Chatty.
Faith quotes (opt-in only)
- Settings > Companion > "Words of comfort": Off (default) / Islam / Christianity / Hinduism / Buddhism (+ Sikhism and
  Judaism offered too, so no major faith is left out). Chosen by the user, never guessed, never in Surprise me.
- Quotes stay words shared across that faith's traditions (e.g. "Verily, with hardship comes ease" - Quran 94:5-6), each
  with its reference, never edited, never mixed with a joke, never in Roast, never next to a problem verdict. They show
  only in calm moments: greeting, idle, a long wait.
- Translations from public-domain editions only (e.g. Bible: King James; Gita: Edwin Arnold 1885; Dhammapada:
  Max Muller 1881; Quran: Pickthall 1930) - checked before shipping.
- ~20-30 quotes per faith, reviewed by the separate reviewer for accuracy of wording and reference.

### 11.7b Come back for the right reasons (13 Nov, Adeeb agreed)
Goal: people come back because they want to, never because they feel they have to. The personality makes the useful
moment memorable; it never replaces it.
Build
- The buddy remembers (local only, from data already kept): "Your Wi-Fi was steadier this week than last" ·
  "Third Monday in a row the internet dipped at 9 pm - that's your provider's busy hour." Only true, measured patterns.
- Small surprises: seasonal hats (built), rare lines that show about once a month, easter eggs, a different greeting on
  the install's birthday. Rare lines live in lines.txt with a rarity mark.
- Visible progress: points, badges, the hat collection, the buddy levelling up with points (new face details / hats).
  Progress is never taken away.
- Worth sharing: award card, roast, letter to your ISP, weekly recap card (opt-in, made on request, saved as a picture).
- Being right when it matters: when something breaks, one line that says "you or your provider" first, jokes second.
Never build
- Streaks that can be lost or "you broke your streak" messages.
- Notifications, pop-ups or reminders asking people to open the app; it never wakes on its own (existing rule).
- Fake urgency or fear ("Your network might be at risk! Check now") - alerts only for measured problems.
- A needy buddy: no "I missed you", no sadness or guilt about the user being away, no begging to stay.
- Anything that hides data, settings or the Quiet buddy / Plain switch behind points.
Check: every new line and feature in 11.x is read against this list before it goes in.

### 11.8 Also
- Fresh-install default: Classic layout, Chill mood, no persona (plain buddy).
- One-file download: CI also uploads AdeeboMeter.exe unzipped (upload-artifact v7, archive: false) - edited, not pushed.
- Penny rules stand: no idle animation; a ~150 ms hover fade only while the mouse moves; memory stays under 64 MB.

### 11.8a Never slow, never silent (Adeeb: "it shouldn't hang like topogram - always show something")
Speed rules
- Repaint only what changed (as today: path strip, side panel, footer and the open page compare their printed text first).
- Each page's background (cards, shadows, stripes, icons) is drawn once into a cached picture; live numbers are painted
  on top. Redrawn only on resize, theme, layout or language change. First open of a page ~50-100 ms, then instant.
- Hover repaints the card under the mouse only, never the page. The ~150 ms fade runs only while the mouse moves.
- The meter shows rounded numbers, so it repaints only when a shown number changes. Buddy / persona lines are picked when
  the state changes, not every tick.
Never look hung
- Rule: the window thread only draws and answers clicks. Every read, parse, network call, file open, encrypt, history load
  or capture runs on a worker - audit all of them (pcap replay up to 64 MB, encrypt/decrypt, certificate files, settings
  import, Gamer week report, history days, Time machine day load).
- Anything taking over 300 ms shows a busy line in the current mood ("Crunching the numbers, boss..." · Roast: "Hold on,
  your PC is thinking. Slowly." · Plain: "Working... 3 s") with a moving dot and a Stop button. Clicks keep working;
  starting the same thing twice is refused politely instead of queued.
- Freeze guard: the existing watchdog (it only logs today) gets a tiny "Still with you - catching up (3 s)..." card on its
  OWN thread, so it shows even if the window thread is stuck; it vanishes when the window answers. The log names what
  it was doing (UiActivity), so a freeze on a tester's PC can be traced.
- Files people pick (captures, certificates, encrypted files) keep their size caps and are read on a worker with progress.
Tests that fail the build
- Self-test: draws every page in both layouts and themes; any page over 30 ms fails.
- Self-test: the window thread answers within 100 ms while each heavy tool starts (proves the work left the UI thread).
- Deep test: 60 s on the busiest page - own CPU over 1% average, or memory over 64 MB, fails.
- The "slow tick" (over 60 ms) and "window froze" logs stay, so a slow PC shows us which page and how long.

### 11.11 Five sections (13 Nov, Adeeb: "build the five sections navigating") - replaces the 9 tabs in 11.1
```
Home ─ Overview + Monitor + Investigate merged: buddy, path, "What's wrong?" tiles, live health, alerts
Toolbox ─ every tool by question, search, calculators
Gamer ─ Gamer Zone
Time machine ─ history, replay, what changed
Learn ─ cards, labs, games, glossary
```
- Network story, Security checkup, Check a destination, reports and packet evidence move to More (11.11b) and also open
  from a result with that incident already selected ("See the story", "Check security", "Make a report"), and stay
  reachable from Toolbox and search (typing "security" or "story" goes straight there).
- Same five in both layouts: coloured tabs (Classic) or sidebar rows (Sidebar). Old deep links / keyboard shortcuts
  to removed tabs land on the right page inside Home or Toolbox, so testers' habits don't break.
- Settings, About, Points stay in the header.

### 11.11b More menu + your own tabs (13 Nov, Adeeb: "no need to hide or drop anything")
```
Home · Toolbox · Gamer · Time machine · Learn · More ▾          (defaults)
                                               ├ Network story
                                               ├ Security
                                               ├ Check a destination
                                               ├ Calculators
                                               └ Reports
```
- Nothing is hidden or dropped: the five plus a "More" menu holding Network story, Security, Check a destination,
  Calculators and Reports. Results still open them with the incident selected (11.11).
- Settings > Navigation: drag to reorder; pin anything from More to the main bar or move a main tab into More; rename
  nothing that is built in (so help and search keep matching).
- Your own tabs: "+ New tab", named by the user (up to 3), holding pinned tools, calculators, pages, saved destinations
  and settings for one-click open (e.g. "Work": VPN check, my sites, subnet calc). Pin from any tool page or search
  result with a pin icon.
- Guard rails: Home always first and can't be removed (the app always has a landing page); at most 7 tabs on the bar
  (Classic must fit a laptop width) - the rest go to More automatically; "Reset to default" button; works the same in
  Classic and Sidebar; Ctrl+1..7 jump to the tabs in their current order.
- Saved with the settings (and in settings export / import), so it moves with the user to a new PC.

### 11.11a Search everything (13 Nov, Adeeb)
- A big search bar in the header of every page (both layouts), "Search anything..."; Ctrl+K or / from anywhere.
- Searches tools, pages, settings, Learn cards, glossary terms, calculators, today's events from Time machine, and
  questions in plain words ("why is my wifi slow", "valorant lag") using the Toolbox synonyms; forgives typos.
- Results grouped (Tools · Pages · Settings · Learn · Terms · History), the best one highlighted, Enter opens it,
  arrow keys move; a setting opens with that setting highlighted. Empty box shows recent searches.
- Nothing found: one buddy line in the current mood with an example to try - never blank.
- Penny: one small in-memory index (~1,000 entries, well under 1 MB) built once at start; each keystroke searches it
  in under 5 ms on the window thread; history items read from the day already loaded. Nothing leaves the PC.
- Replaces the separate Toolbox search box (Toolbox keeps a filter for its own list).

### 11.12 Calculators (13 Nov) - our own maths from public formulas; NOTHING copied from internal tools
Existing (upgrade to the new tool page): packet stack vs MTU, VLSM planner, bandwidth / transfer time,
decimal / hex / binary. New, grouped by who needs them:
| Group | Calculators |
|---|---|
| Network | subnet + "will this mask work with my gateway?" check (the /31-in-a-/21 case), IPv6 prefix split, wildcard mask, BDP / TCP window for a link, VPN / tunnel overhead on the MTU |
| Wireless | LTE and 5G NR throughput (bandwidth, MIMO, modulation), radio overhead per packet, link budget (Wi-Fi / microwave: power, gains, path loss, margin), Wi-Fi channel width vs speed |
| Servers and IT admin | RAID usable capacity, backup / restore time over a link, UPS runtime, uptime % -> downtime per year/month (SLA), storage growth |
| Streamers | bitrate for resolution + fps + codec, upload you need (with headroom), data per hour of stream / recording size |
| Everyday people | how long a download takes, monthly data use (streaming, calls, gaming hours), "is my plan fast enough for N people" |
| Conversions | binary <-> hex <-> decimal (any width), IP <-> integer <-> hex, bits <-> bytes, Mbps <-> MB/s, time units |
| TDM (generic maths only) | E1/T1 timeslots and bandwidth, jitter buffer size from packet delay variation |
- Each is one tool page (11.6): inputs as fields with units, a big answer, a one-line "what this means", Details for
  the formula. Pure arithmetic: nothing sent, nothing read from the PC.

### 11.13 Decided against / not yet (13 Nov)
- Freehand drawing tool: dropped (not a network companion; adds weight).
- Evidence record + the "website or app won't open" end-to-end milestone + three tester cases (DNS fails / TCP times out
  after DNS / TLS rejects the certificate): agreed as the next milestone; NOT in tonight's build unless Adeeb says so.
- Topogram bridge: only a generic, redacted incident-file export on AdeeboMeter's side (no vendor names); any import
  and SR OS alignment lives in topogram.
- Tagline stays "is it me or the network?"; Home subtitle "See where a connection fails and what to check next."

### 11.9 Build order (after Adeeb approves this section and the line packs)
1. Drawing kit: Inter, text hint, palette, card / button / chip / switch / tile / big back button, scrolling.
2. Frame: the five sections (11.11) in Classic + Sidebar, slim health strip, header with Details behaviour + Mood button.
3. Shared tool page + input swaps; search everything (11.11a); Toolbox page.
4. The other pages (11.5, merged into the five sections), then the calculators (11.12).
5. Mood + persona engine, 14 packs, Settings section.
6. Analyzer + self-test + deep test; screenshots of every page in both layouts and themes for Adeeb; real-PC check.

### 11.10 Open questions
- Default layout: decided - Classic, with a one-time Sidebar card.
- Persona names: decided - plain names.
- Faith quotes: who checks each faith's wording before shipping, besides the reviewer?
