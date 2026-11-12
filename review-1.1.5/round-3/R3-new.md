# Round 3: new features in 8502674 (dev-1.1.5)

Scope: Qr.cs and Share, OverTime.cs, GamerTools.cs, PingMap, LearnGames and the Games UI, LearnHome, the new Learn sections (provider and mobile), 6-month History, the Gamer preset, and the removal of Find devices.
Harnesses (all run under Mono against build3/r3.dll, using reflection):
- `review3/harnessQ`: QR encode, then decode with zxing-cpp, plus a module-for-module comparison against Nayuki qrcodegen.
- `review3/harnessO`: 180 days of synthetic gz history (dense, gaps, weekday-only and corrupt variants), plus game history, then SixMonths, Month, BeforeRollOff and ParseGB.
- `review3/harnessP`: points file and policy parse.

---

### 1. The six-month review is never written before roll-off on a PC that isn't on most days
Severity: wrong result
Where: src\Checks\OverTime.cs line 145 (BeforeRollOff)
How to reproduce: Keep history for 6 months. Run the PC on weekdays only, which gives 128 day files over 180 days. History.Tidy then calls BeforeRollOff. harnessO: `ot.exe r3.dll run5 181 weekdays`.
What happens / what should happen: Nothing is written ("BeforeRollOff wrote: nothing"). With the same span recorded every day, the review is written (run6). The guard `days.Count < 150` counts days with data, not the span. Settings, terms (tb.sixmonths) and privacy.html all promise "before it rolls off, AdeeboMeter writes your six-month review". With the current guard, the oldest days roll off and the review is lost for a laptop that sleeps at weekends or someone away for a month. The guard should use the span (oldest day ≥175 days ago), or at least require only a few days.
Confidence: confirmed (ran it: weekday-only history gives no file; daily history writes Six-months-2026-05-to-2026-11.html)
Rule: none (the documented promise is broken)

### 2. Month and Six-month reviews re-read every game-history file once per game: 9 to 21 s and +28 MB
Severity: slow
Where: src\Checks\OverTime.cs line 63-67 (Sum, the "laggiest game" loop) and line 128 (SixMonths runs Sum again for each month)
How to reproduce: 180 days of history plus 30 days of games-*.csv:
- 5 servers a minute (Gamer preset background, GameBgServers=5): about 330 KB a day.
- 16 servers a minute (MaxServers while a game is open on the Gamer preset).

Then run Toolbox › Six months on your network.
What happens / what should happen: SixMonths takes 3.2 s with no game history. It takes 9.0 s with 5 servers, and 21.2 s with 16 servers. Process private memory goes from 35 MB to 63 MB (baseline 35 MB, inside a bare harness, not the full app). GameHistory.Read(d, g.Id) reads and splits the whole day file for each of the 13 built-in games, so every line is parsed 13 times. SixMonths then repeats the whole Sum (history and game history) month by month. The fix is to read each game file once per day and group by game, and to build the month rows from the per-day totals of the first pass. Month() also runs Sum twice (this month and the one before).
Confidence: confirmed (ran it: harnessO run1 with 30 days of 5- and 16-server game history)
Rule: 7

### 3. Picking "Keep history for: 6 months" in Settings can freeze the UI for the whole six-month review
Severity: slow
Where: src\Ui\FullView.Settings.cs line 133 (the "Keep history for" row's handler calls History.Tidy() on the UI thread) → src\History.cs line 56 → OverTime.BeforeRollOff → SixMonths(null, null)
How to reproduce: Have ≥175 days of history and no Six-months-*.html written in the last 170 days. This happens on the first run past 175 days, or after deleting the reports folder. Then click "6 months" in Settings › History & data.
What happens / what should happen: Tidy runs synchronously on the UI thread. It reads all 180 days twice and the game history 13×. That took 3 to 21 s in finding 2, and the window is frozen for that time. Tidy also gz-compresses uncompressed days there. Tidy should be queued on the thread pool, as History.Write and AppCtx already do.
Confidence: suspected (read the call chain; timings measured in harnessO)
Rule: 7

### 4. "Time lost waiting" counts every outage minute twice
Severity: wrong result
Where: src\Checks\OverTime.cs line 82 (PeriodRows)
How to reproduce: Have a month with a 60-minute internet outage. During it Monitor sets level 2 ("The internet past your router..."), so each of those minutes is also a BadMinute. Run Your month on the network.
What happens / what should happen: `waits = p.OutageMin + p.BadMinutes` adds the 60 outage minutes and the same 60 rough minutes, so it reports 2 h lost for a 1 h outage. The same total goes into the shareable six-month HTML. It should take the union, for example BadMinutes plus only the outage minutes that History didn't record (PC asleep or app closed).
Confidence: suspected (read: Monitor.cs lines 785/779 put inet==2 at level 2; Sum counts level ≥2 minutes as bad)
Rule: none

### 5. The wake-up report leaves out the wakes where the router never came back
Severity: wrong result
Where: src\Checks\OverTime.cs line 210 (WakeUp), against src\Monitor.cs line 488
How to reproduce: The PC wakes and the router doesn't answer within 2 minutes. Monitor logs "Woke up, but the router didn't answer within 2 minutes".
What happens / what should happen: The regex `router (\d+(?:\.\d+)?) s` doesn't match that text, so the worst wake-ups are dropped. "Back online after waking: 3.5 s on average · slowest 3.5 s" stays green while one wake in ten failed outright; in harnessO, 18 such events were all ignored. Each failed wake should count as ≥120 s, or be listed as "didn't come back" and turn the row amber or red.
Confidence: confirmed (read the regex against the exact event text; the harness data included the timeout events)
Rule: none

### 6. Download-time size: "1,000 MB" is read as 0.001 GB
Severity: wrong result
Where: src\Checks\OverTime.cs line 277-282 (ParseGB)
How to reproduce: Toolbox › How long will this download take? › type `1,000 MB` (or `1,500 GB`).
What happens / what should happen: ParseGB('1,000 MB') = 0.001, which is 1000× too small, so the tool says "1 min" for a 1 GB download. The comma is always treated as a decimal point. A thousands separator should be accepted, for example only when exactly 3 digits follow the comma, or with the CurrentCulture rules.
Confidence: confirmed (ran it: harnessO prints `ParseGB '1,000 MB' = 0.001`)
Rule: none

### 7. GPN verdict says "it's the game's own server" when the trace never reached the server
Severity: wrong result
Where: src\Gamer\GamerTools.cs lines 72-73 and 85 (Gpn)
How to reproduce: Run "Would a GPN help?" for any estimate game. The target is dynamodb.<region>.amazonaws.com, which doesn't answer ICMP, so the trace ends at some router inside the cloud or transit network. If that last answering router de-prioritises ICMP (+30 ms) or rate-limits it (≥20% loss), the verdict triggers.
What happens / what should happen: jump = the last index → "No - it's the game's own server … A GPN arrives at the same server". There is no later hop to prove the delay "carries on", and the hop isn't the server: the hop label itself checks `Reached`, but the verdict doesn't. The same happens when the path has more than 16 hops (`hops.Take(16)`). This is the same class of bug as the MTR rule in the brief: a lone last hop that isn't the destination shouldn't be blamed. The verdict should say "can't tell - the server doesn't answer pings".
Confidence: suspected (read)
Rule: none

### 8. Escape room: wrong answers use up the hints, and Hint 1 is skipped
Severity: wrong result
Where: src\Ui\FullView.Games.cs lines 109, 113 and 122 (PaintRoom)
How to reproduce: Learn › Games › Network escape room. Click a wrong "What's broken?" option, then press Hint.
What happens / what should happen: A wrong guess does `roomHints++`, which is both the time penalty and the hint counter. After one wrong guess the button reads "Another hint" and shows Hint2; Hint1 is never shown. After two wrong guesses it says "No more hints" without any hint having been given. The header shows "2 hints used" too. Keep a separate penalty counter for the minutes.
Confidence: confirmed (read; the logic is a single shared counter)
Rule: none

### 9. Games pay points for an answer they have just shown you (no "first time" rule, unlike Check yourself)
Severity: wrong result
Where: src\Ui\FullView.Games.cs lines 134-135 (NewThief), 155 (thief pick), 198 and 205 (detective), 229 and 235 (quick-fire)
How to reproduce:
- Catch the thief: click the wrong device. The thief is revealed (always the same name for a case: android-xxxx, raspberrypi, Galaxy-S23, Dad's PC, or the HP-Printer with a Xiaomi maker). Press Next case. Unsolved cases are drawn again, so with one case left you always get it back. Click the name you were just shown: +3.
- Packet detective: a wrong pick shows the green answer. After 10 puzzles detIdx wraps to the same puzzle: +3.
- Escape room: there is no penalty beyond 1 minute per wrong click. Clicking every option wins within 7 minutes: +4.

What happens / what should happen: Learn checks call Points.MarkTried so that "right first time earns". The games have no tried.* marker, so the 46 game items (about 150 points) can be collected by revealing and replaying. Each item still awards only once, so this isn't repeat farming, but it is points earned without solving anything. Mark game.* as tried on a wrong answer, as the check questions do.
Confidence: confirmed (read the award paths; no MarkTried anywhere in LearnGames or FullView.Games)
Rule: 10

### 10. Points earned for the removed Find devices tool disappear from the total
Severity: wrong result
Where: src\Points.cs line 88 (Total: `Where(kv => Known(kv.Key))`), with "finddev" removed from the Toolbox in aec65f5
How to reproduce: Use a points.txt from 1.1.5-rc1 that has `finddev|<ticks>|-|2|0`, then start this build.
What happens / what should happen: Total drops from 8 to 6 and Done from 3 to 2, so the user loses points they earned. Rule 10 says "the total survives a restart". Grandfather removed tool ids: keep them Known with their value, or move them to an "awarded, retired" list.
Confidence: confirmed (ran it: harnessP/pt.exe prints `Total=6 Done=2`, `finddev known=False`)
Rule: 10

### 11. Policy `history_days` above 30 is silently ignored, so there is no limit at all, now that users can choose 180
Severity: wrong result
Where: src\Policy.cs line 66 (Parse, `d >= 1 && d <= 30`)
How to reproduce: Put `history_days = 90` (or 60 or 180) in AdeeboMeter.policy. The user has chosen 6 months.
What happens / what should happen: MaxHistoryDays stays 0, Cfg.HistoryDays stays 180, and nothing is logged. An IT admin who writes 90 to cap the new 180-day option gets no cap. The accepted range should match HistDayChoices (1-180), and an out-of-range value should be logged, or clamped to 30 rather than dropped.
Confidence: confirmed (ran it: harnessP/pol.exe prints `history_days=90 -> MaxHistoryDays=0 Cfg.HistoryDays=180`)
Rule: 6 (the policy fails to switch off or narrow)

### 12. Learn uses a Nokia-specific product term ("VPRN")
Severity: cosmetic
Where: knowledge\learn.txt lines 487 and 491 (topic l3vpn)
How to reproduce: Learn › Inside the internet › L3VPN.
What happens / what should happen: "L3VPN (VRF, VPRN)" and "some vendors call the service a VPRN". VPRN (Virtual Private Routed Network) is the SR OS (Alcatel-Lucent / Nokia) name for an L3VPN service. Most people would read it as a Nokia reference. Drop it and keep "VRF" or "L3VPN".
Confidence: confirmed (grep; it appears only in learn.txt)
Rule: 8

### 13. Mobile-network Learn: three factual slips
Severity: wrong result (content)
Where: knowledge\learn.txt line 555 (epc ladder), line 545 (ran ladder), line 534 (mobile "breaks")
How to reproduce: Learn › Inside the mobile network › the 4G core, the towers, and the plain-words page.
What happens / what should happen:
- epc ladder "PGW > MME: IP 100.70.3.9 allocated". The PGW never talks to the MME: the Create Session Response goes PGW → SGW (S5/S8), then SGW → MME (S11). It should read "PGW > SGW > MME".
- ran ladder "Neighbour > Phone: you're mine now". In an X2/Xn handover the source cell sends the handover command (RRC reconfiguration). The phone then does random access to the target; the target doesn't speak first.
- mobile "breaks": "Weak signal with steady bars is often a busy cell" contradicts itself. It probably means "Slow data with full bars".

Everything else checked out: S1-MME/S1AP+NAS, S1-U/S5/S8/SGi, S6a Diameter, N2/N3/N4 PFCP/N6/N8/N10/N11/N12, 4G attach versus 5G registration plus PDU session, QCI/5QI, NSA option 3x and EN-DC, RedCap needing SA, PHP, 4-byte labels, RD/RT, IS-IS on L2, 400 Gbps multicast arithmetic, Open Connect, HLS latency. All the check questions mark the right answer.
Confidence: confirmed (read against 3GPP TS 23.401 and 38.300 flows)
Rule: none

### 14. Ping map: server labels in a cluster are drawn on top of each other
Severity: cosmetic
Where: src\Ui\FullView.PingMap.cs line 69 (PaintPingMap)
How to reproduce: Gamer Zone › a game with the built-in "world" list. NA East (Virginia, 38.9/-77.4) and NA Central (Ohio, 40/-83) are about 6 px apart on a 760-px map. CS2's Valve list adds Sterling, Washington, New York and Atlanta.
What happens / what should happen: `(off++ % 2) * 0` always adds 0, so the intended alternating offset is dead code and the labels for neighbouring dots overlap into an unreadable smear. It should be `* (ls.Height + 2 * S)`, or labels should be skipped when they collide. (The ping map sends no traffic and only reads GamePing.Stat, so it holds up for rule 1.)
Confidence: suspected (read; the offset expression is plainly zero)
Rule: none

### 15. QR: mask choice differs from the specification's penalty in about 25% of codes
Severity: cosmetic
Where: src\Qr.cs lines 117-121 (Penalty, the finder-like pattern rule)
How to reproduce: harnessQ/cmp2.py: 334 payloads (v1-v10) compared module by module with Nayuki qrcodegen.
What happens / what should happen: 251 are identical. 83 differ only in the mask, and match qrcodegen exactly when the same mask is forced. That proves the data, Reed-Solomon, interleaving, format and version bits are all correct. The N3 rule needs 4 light modules before or after the pattern inside the symbol, but the spec counts the quiet zone as light. Every code still decodes (see Checked and held up), so this is cosmetic only.
Confidence: confirmed (ran it)
Rule: none

### 16. The Console mode Example uses an address the real tool rejects
Severity: cosmetic
Where: knowledge\examples.txt line 815 (`in: 192.0.2.40|fortnite|30`), against src\Gamer\GamerTools.cs line 169
How to reproduce: Toolbox › Console mode › Example › Fill › Run.
What happens / what should happen: 192.0.2.40 is TEST-NET-1, so it isn't Private, and the run answers "type the console's address on your network". The Example also shows "PC → router 3 ms (192.0.2.1)", which the tool could never print. Use a 192.168.x example that Fill can't turn into a confusing failure, or have Fill leave the address empty.
Confidence: suspected (read)
Rule: 9 (adjacent: the Example's input becomes a failed real run)

### 17. Network-settings snapshot: a short header line makes the compare fail
Severity: crash (caught)
Where: src\Checks\OverTime.cs line 330 (Snapshot, `l.LastIndexOf(' ', l.Length - 7)`)
How to reproduce: Edit netsnap.txt so the first line is `# hi` (or just `#`), then run Network settings snapshot with the box empty.
What happens / what should happen: ArgumentOutOfRangeException, shown as "Couldn't finish" by the Toolbox catch-all. A damaged or hand-edited header should just leave "when" empty.
Confidence: confirmed (ran the exact expression: `# hi` → ArgumentOutOfRangeException; the full Snapshot can't run under Mono because NetworkInterface isn't implemented)
Rule: none

### 18. "6 months is still only about 3 MB" leaves out game history in the same folder
Severity: cosmetic
Where: src\Ui\FullView.Settings.cs line 133 (text) and src\Gamer\Gamer.cs line 532 (games-*.csv, uncompressed, in History.Dir)
How to reproduce: Gamer preset, one game watched in the background, game history 30 days.
What happens / what should happen: Game history is plain CSV, about 330 KB a day at 5 servers and 1.07 MB at 16, so 10 to 32 MB over 30 days in the same history folder. Synthetic minute history came to 19-27 KB a day, so 3.4-4.8 MB for 180 days (my values are noisier than real ones, so real files are probably smaller). The Settings row's size (History.Bytes) includes the game files, so the user sees e.g. 14 MB next to "about 3 MB". Compress finished game days like History does, or reword the text.
Confidence: suspected (sizes measured on synthetic data)
Rule: 7

### 19. Month "this": the outage minutes are compared with a full previous month
Severity: cosmetic
Where: src\Checks\OverTime.cs line 110 (Month)
How to reproduce: On 12 November, run Your month on the network with `this`.
What happens / what should happen: "rougher - 2% rough minutes (was 1.9%), 33 outage minutes (was 93)". The verdict uses rates, which is right, but it sits next to 11 days of outage minutes against 31, so the line reads as both rougher and better. Scale the outage minutes per day, or label the current month as partial.
Confidence: confirmed (ran it: harnessO)
Rule: none

---

## Checked and held up

- **QR encoder: decodes for every size it supports.**
  - 360 payloads went through Qr.Encode and were rendered with a 4-module quiet zone. They included: empty and 1 byte; every version boundary (cap−1, cap, cap+1 for v1-v10: 17/32/53/78/106/134/154/192/230/271); 279 random-length ASCII strings; printable ranges; URLs; UTF-8 (ä€😀, 日本語, "·", tabs and newlines); "€"×90/91; "😀"×67/68; and 5000 bytes.
  - zxing-cpp decoded all 332 non-empty codes byte-exact, as ECC L, across all versions 1-10 (21-57 modules). The empty payload isn't decoded by zxing, but it is identical to qrcodegen's own output, so it's a decoder limit and Summary is never empty.
  - Reed-Solomon (GF 0x11D, generator), the block table (including the two v10 groups 68/69), byte-mode count 8/16 bits, terminator, 0xEC/0x11 padding, version BCH (0x1F25) and placement, and format BCH (0x537 ^ 0x5412) all match qrcodegen bit for bit.
  - Over capacity (272+ UTF-8 bytes, up to 5000) returns null without crashing; ShowQr then returns quietly. QrRows trims the text to ≤270 bytes first.
  - The real rendering path (Qr.Picture, px=4, 270-byte diagnosis text, v10) decodes correctly.
- **Share and privacy.** ShowQr and QrRows make no network call (rule 1). The QR holds only OverTime.Summary: verdict, hint, the five PathMain values (CPU/RAM, signal/gw ms, +ms, DNS ms, internet ms/loss) and the outage count. The SSID, gateway, DNS server and adapter name live in PathSub, which Summary doesn't use. Private IPs and MACs are redacted (GameSession.Redact), and every dotted IPv4 becomes x.x.x.x. The headline can name a process ("chrome uses the most"), which is fine.
- **Six-month review HTML.** Every value is HTML-encoded. It contains no SSID or network column ("HomeNet" from the synthetic Net column: 0 hits), no addresses, and no Speeds list. It is written atomically (tmp + ReplaceFile).
- **History with gaps, corruption and future files.**
  - A corrupt .csv.gz is skipped (179 of 180 days counted, no crash), and a junk line or a future 2099-01-01.csv is tolerated.
  - DST and time-of-day: rows are bucketed by seconds of the day, and `t >= OneDay` lines are skipped.
  - Month parsing: "2026-13" gets a polite error. "2027-01" gives "nothing recorded". "may" maps to the last May. "" and "last" give October.
- **Memory with 6 months loaded.** It's transient: private memory went 35 → 47 MB during SixMonths, with nothing kept after. A paint-time History.Days() is cached 30 s. Outages.Last(183 days) takes 49 ms, so its lock doesn't stall the UI paint.
- **History pruning and migration.** Per-file try/catch in Tidy. Old settings with 1/3/7/14/30/60/90 keep their values (Pick against HistDayChoices). The default stays 3 days, so 6 months is opt-in, as privacy.html describes.
- **Event log query.** Provider Tcpip with 4198/4199 (IPv4 address-conflict events) over the last 30 days (2592000000 ms in the XPath) is the right query. The escape room's "dupip" text matches 4199's real wording.
- **Ping map traffic (rule 1).** None: it paints GamePing.Stat only.
- **gamer_zone=off.** Background pings stop (GamePing.Start returns) even under the Gamer preset, and Policy.Apply runs after the preset re-applies on load (AppCtx line 211-212). GPN, Console mode, Best time and Tonight run only when the user presses Run (rule 1). VPN leak needs Online lookups on and not policy-off. The IPv6 score's ping to 2606:4700:4700::1111 respects internet_check=off.
- **Find devices removal.** No `finddev`, `Reach.Find`, "Find devices" in the UI, terms, examples or Learn, and no dangling search alias. The only leftover is the points total (finding 10).
- **Learn games content.** Port pool (587, 993, 67, 161/162, 1812, 49, 88, 179, 5201, 3478 …), the address quick-fire classes (CGNAT 100.64/10, 169.254, TEST-NETs, multicast), all 10 detective puzzles and their "wrong" indices, the OUIs in the thief cases (TP-Link f4:f2:6d, HP 9c:8e:99 and 3c:52:82, Samsung 8c:79:f5 and 4c:66:41, Pi b8:27:eb, MSI 00:d8:61, Xiaomi 64:09:80, locally administered a6:/de:), and the simulated consoles for all 10 faults (for example, a wrong gateway still lets nslookup reach the on-link DNS) are all correct. Every scenario can be won. Each item awards once through Points.Award.
- **Learn provider content.** BGP and AS counts, iBGP and route reflectors, IXP peering versus transit, OSPF areas and IS-IS on L2, MPLS labels, PHP and the 4-byte MTU cost, MP-BGP VPN-IPv4 RD/RT, IGMP/MLD, PIM-SSM and snooping, CDN ABR ladders, Open Connect and live-latency figures are all right.
