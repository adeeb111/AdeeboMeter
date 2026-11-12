# AdeeboMeter 1.1.5 - plan

Status (12 Nov 2026): built on branch dev-1.1.5 and reviewed (three outside reviews, findings fixed); not released - Adeeb releases it. Designed 11 Nov 2026. Mockups: the "AdeeboMeter Gamer Zone" design canvas (6 artboards).
One release, everything together (Adeeb's rule). Nothing is built, tested, committed or published until he says go.

## Rules every item keeps
No admin · never listens · offline by default (third parties only after the user asks or opts in) · nothing stored beyond
what each feature documents · SNMP read-only · secrets never kept · C# 5 / .NET 4.8 · never mention or reference Nokia.

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
