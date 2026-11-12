# AdeeboMeter 1.1.5 - plan

Status: designed with Adeeb on 11 Nov 2026, not built. Mockups: the "AdeeboMeter Gamer Zone" design canvas (6 artboards).
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
6. Languages: string table, then French.
