# Round 4 - slice S2: Brain, Link, Capture, WindowsDeep, M365, Knowledge (e80412e)

Method: read every line of the 15 source files; diffed them against 8502674; read learn.txt in full, plus the 8502674..HEAD changes to terms.txt and the close levels / selected entries of processes.txt. The harnesses are in /tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/review4/hS2 and call r4.dll by reflection:
- N.cs: real `LinkInfo.ActiveBlock/Fields/NormKey/ParseNetworks/BandOf/Overlaps`, `Monitor.ParseWifi`, `WlanApi.LeastCrowded`, with Collect's glue lines 571-620 copied in. The input is netsh text in 13 languages (netsh/*.txt). I wrote those samples from what I know of localized netsh; none was captured on a real PC.
- W.cs: `WlanApi.FromFreq/ReadIes/Rsn` with built information elements.
- R.cs: `LiveCapture.AddRtp` with sender models.
- J.cs: `Journey.Kind/Translate/Explain`.
- K.cs: `Calls.Second/Summarise`.
- C.cs: `Crash.Clean`.

Mono was used throughout. Where .NET Framework behaves differently it doesn't matter for these paths, because they are pure string and arithmetic code.

### 1. Your connection on Russian, Japanese, Chinese, Korean, Arabic and Turkish Windows says "nobody - clean air" and invents a "faster band"
Severity: wrong result  
Where: src\Link\LinkInfo.cs lines 475-483 (KeyWords), 520-530 (ParseNetworks), 602-607 (Sharing your channel), 620-621 (2.4 GHz note); WlanApi.LeastCrowded line 301; src\Link\WifiAdvisor.cs lines 89-99  
How to reproduce: `mono N.exe ru2 ja2 tr2` uses netsh samples where the PC is on 2.4 GHz channel 6, three neighbours (on channels 6, 4 and 8, all at 70-80%) overlap it, and your network has no 5 GHz access point.  
What happens / what should happen:
- English: "Sharing your channel: 3 (3 strong)", no band note, least crowded channel 11.
- ru, ja, zh, ko, ar: the per-AP labels Сигнал/Канал/Диапазон, シグナル/チャネル/バンド, 信号/信道/波段 and so on aren't in KeyWords. Every AP then has Signal -1, Channel -1 and Band "". The page says "Sharing your channel: nobody - clean air". It also always adds "You're on 2.4 GHz, but your network also offers a faster band", because `!a.Band.StartsWith("2.4")` is true for Band "", including your own AP. LeastCrowded answers "stay on 6, load 0".
- tr: "Sinyal" isn't mapped, so the page shows "3 (0 strong)", gives no crowding warning, and the advice weighs every neighbour as 0.

Monitor.ChanKeys already knows "канал", "チャネル" and "信道", but LinkInfo.KeyWords doesn't. Should: share one label table, fall back to the shape of the values (NN% for signal, small integers for channel, "x GHz" for band) as ParseWifi does for signal, and treat Band "" as "unknown", never as "not 2.4".  
Confidence: confirmed (ran it: N.exe on the 13-language samples; the label words are from my knowledge, not captured)

### 2. A decimal-comma band ("2,4 GHz") turns off all 2.4 GHz logic for German, French, Spanish, Italian, Portuguese and Polish
Severity: wrong result  
Where: src\Link\LinkInfo.cs line 581 (band from netsh's Band line), lines 538, 589, 602, 620 (`StartsWith("2.4")`); WlanApi.LeastCrowded line 289; src\Link\WifiAdvisor.cs lines 87 and 102  
How to reproduce: `mono N.exe de fr es it pt pl`, using samples whose Band line reads "2,4 GHz" (the way Windows writes it in those languages, e.g. "Band : 2,4 GHz").  
What happens / what should happen: band = "2,4 GHz".
- Overlaps falls back to `a == b`, so the neighbours on channels 4 and 8 aren't counted: "Sharing your channel: 1 (1 strong)" instead of 3 (3 strong), and no crowding note.
- LeastCrowded returns null.
- WifiAdvisor skips the 2.4 GHz channel advice and the "your network is also on 5 GHz" advice.
- The 2.4 GHz amber badge and the band note never appear.

The KeyWords mapping added in 96abea2 makes this worse: before it, these languages had no Band key, so the language-neutral WlanApi band ("2.4 GHz") was used. Should: normalise the band by its leading digit (2 / 5 / 6), or prefer WlanApi's band whenever it is available. Russian "2,4 ГГц" has the same shape.  
Confidence: confirmed for the parsing (ran it). That localized netsh really prints "2,4" is suspected; please check on a German PC.

### 3. With the internet check off (Settings, or the policy), every Teams call ends with "lost 100% of internet checks" and a pop-up
Severity: wrong result  
Where: src\Brain\Watch.cs lines 62 and 72 (Calls: `rows.Count(r => double.IsNaN(r.Net))`), line 82 and line 90 (Alerts.Raise "call"); src\Monitor.cs line 263 (NetPing cleared when the check is off) and line 223 (`netSec = 0` under Policy.NoInternetCheck)  
How to reproduce: Settings › internet check Off (or `NoInternetCheck` in AdeeboMeter.policy), then make a Teams call longer than 20 s on a perfect network. K.exe feeds 120 seconds of `net = NaN` (what `NetPing.Last` returns for an empty ring).  
What happens / what should happen: Summaries reads "Your 2-minute call lost 100% of internet checks - the trouble was past your router (provider or VPN)." It is level 1, so it also raises the "After your call: ..." notification. The data says nothing about the internet, because no check ran. Should: count a sample only when a new internet probe arrived during the call (as Brain does with NetAt), and say nothing about loss when there were none. Related cosmetic issue: a 20-29 s call is called a "0-minute call" (Math.Round).  
Confidence: confirmed (ran it: K.exe output "Your 0-minute call lost 100% of internet checks", level 1)

### 4. Packet journey on DNS over TCP (53) or DNS over TLS (853): "Some questions were asked twice" on every clean lookup
Severity: wrong result  
Where: src\Capture\Journey.cs line 144 (Kind = "dns" for any 53/853), lines 343-349 (Dns: every outbound packet is a "question", every inbound one an "answer")  
How to reproduce: J.exe with one clean TCP exchange: handshake, query, answer, FIN, ACKs. The port 853 run adds a TLS hello.  
What happens / what should happen:
- Port 853: "In short: some questions were asked twice" and "4 question(s) went unanswered and were repeated - each costs about a second".
- Port 53: "3 question(s)" the same way.

SYN, ACK, FIN and TLS handshake packets are all counted as questions, and the "answer time" pairs ACKs with replies. Should: apply the question/answer rule to UDP only (or to TCP segments with payload, ignoring the TLS handshake), and use the TCP rules for DoT.  
Confidence: confirmed (ran it: J.exe)

### 5. "Can I close it?" tells people svchost.exe is OK to close
Severity: wrong result  
Where: knowledge\processes.txt `== svchost` (close: 1), shown by src\Knowledge\Explain.cs line 38 (CloseText[1]); also `== msiexec` (close: 1)  
How to reproduce: What's running › any svchost.exe › hover card.  
What happens / what should happen: the card says "Leave it be. If it misbehaves, closing it is OK - Windows (or the app) brings it back." Its own "without" line says "Kill the wrong copy and the network, the sound or the whole PC goes with it". Ending the svchost that hosts DcomLaunch or RpcSs makes Windows shut down within a minute. Should be close: 2 ("Don't close it"). msiexec at level 1 also promises "Windows brings it back", but killing it mid-install can leave a half-installed app. The wording should be "lets the install fail" (level 1 with its own text, or 2 while an install runs).  
Confidence: confirmed (read it)

### 6. Live capture call quality: the RTP numbering thresholds still give 0% or 50-60% loss for ordinary renumbering (round-3 #2 only partly fixed)
Severity: wrong result  
Where: src\Capture\LiveCapture.cs lines 264-273 (AddRtp)  
How to reproduce: R.exe, same SSRC throughout, 3.75% real loss after the event:
- (a) 45000..45999, then a restart at 20000: shown 0.00%. The backward jump is under 30,000, so it isn't a new run; every later packet falls below MaxExt and the loss is hidden.
- (b) 45000..45999, then a jump to 50000: shown 51.88%. A forward jump of 4,000 within 20 ms is counted as lost packets.
- (c) 60000..60999, then a restart at 2000: shown 63.46%. It is taken for a wrap and 6,536 phantom packets are expected.
- (d) Reordering across a real wrap (65534 arriving after 0): 0.10% phantom loss, about 3 packets per wrap. Negligible.

What should happen: RFC 3550 A.1 uses MAX_DROPOUT 3000 and MAX_MISORDER 100. A jump of more than 3,000 (either way) followed by two packets in sequence starts a new run. A jump far larger than the elapsed time × packet rate can't be loss. Rule (c) should also need the old number within ~100 of 65535.  
Confidence: confirmed (ran it: R.exe)

### 7. Your connection never notices the location block on non-English Windows, then says "Nothing to tweak - your Wi-Fi is set up well"
Severity: wrong result  
Where: src\Link\LinkInfo.cs line 501 (Blocked: English "location" + "permission"), line 569; src\Link\WifiAdvisor.cs lines 77-81 and 125  
How to reproduce: German or French Windows 11 24H2 with "Let desktop apps access your location" off, then open Your connection.  
What happens / what should happen: netsh prints its translated refusal, so Blocked() is false. Fields finds nothing, so sig and ch are -1 and the verdict is "Windows didn't say the signal or channel...". There's no location hint, and the advisor runs with no APs and ends with "Nothing to tweak - your Wi-Fi is set up well." The Monitor already knows this in any language (WlanApi.Current rc 5 → `m.WifiBlocked`). Should: also set `s.LocationBlocked` from `m.WifiBlocked` or from WlanApi rc 5.  
Confidence: suspected (read it; the localized netsh text isn't available here)

### 8. "Your Wi-Fi dropped N times in the last day" counts your own disconnects on non-English Windows
Severity: wrong result  
Where: src\Link\LinkInfo.cs line 676 (`IndexOf("by the user")`), lines 107-109 and 121-130 (EvLog.WifiDrops / Plain)  
How to reproduce: German Windows: disconnect Wi-Fi from the tray three times, then open Your connection.  
What happens / what should happen: event 8003's Reason data is a translated sentence, so "by the user" never matches. All three count as real drops: level 1 and "Your Wi-Fi dropped 3 times in the last day". Plain() also adds no explanation. Should: use the event's numeric ReasonCode field, which is the same in every language.  
Confidence: suspected (read it)

### 9. The Wi-Fi service path is switched off for the whole session after three failed answers, including "no Wi-Fi interface right now"
Severity: slow  
Where: src\Link\LinkInfo.cs lines 195-197 (Current returns -1 when WlanEnumInterfaces lists none, or when the handle fails); src\Monitor.cs lines 463-470 (`++wlanApiFails >= 3` → `wlanApiOk = false`, never re-enabled)  
How to reproduce: resume from sleep while the Wi-Fi adapter takes 20-40 s to come back (AdapterKind is still "Wi-Fi" from before), or three transient query errors in a row.  
What happens / what should happen: every later Wi-Fi read starts netsh.exe again (every 10-30 s, all day). That is the cost c529c2b removed, and it comes back silently until restart. Should: treat "no interface" as "no Wi-Fi now", not as "this Windows can't", and retry the service path after a while (e.g. at every network change or every 10 minutes).  
Confidence: suspected (read it)

### 10. Two Wi-Fi adapters: while the main one is between states, the service path reports the other adapter's "disconnected" as a drop
Severity: wrong result  
Where: src\Link\LinkInfo.cs lines 198-203 (Current: no connected adapter → adapter 0's state); src\Monitor.cs line 570  
How to reproduce: a laptop with an idle USB Wi-Fi dongle enumerated first. The built-in adapter goes through "associating" or "authenticating" while it reconnects.  
What happens / what should happen: Current returns the dongle's "disconnected", so the timeline gets "Wi-Fi dropped" (level 2, counted by the 3-drops alert), then "Wi-Fi reconnected". The adapter that matters never disconnected. When both adapters are connected, the first connected one wins, not necessarily the routing adapter (m.Primary). Should: pick the interface whose GUID is the primary NIC's Id; if that adapter isn't found, return the non-disconnected one.  
Confidence: suspected (read it)

### 11. Crash notes keep every folder name after the first space
Severity: security  
Where: src\Brain\Watch.cs line 286 (Crash.Clean regex `[^\s"<>|:*?]*\\`)  
How to reproduce: C.exe with `Could not find file 'D:\Clients\Acme Corp\Merger plan Q3\board.adeebo'` and `\\fileserver\HR Share\Salaries 2026\list.csv`.  
What happens / what should happen: the results are `<folder>\Acme Corp\Merger plan Q3\board.adeebo` and `<folder>\HR Share\Salaries 2026\list.csv`. `C:\Program Files\Contoso VPN\Secret Project\x.txt` isn't touched at all. On Windows, `%USERPROFILE%\OneDrive - Acme Corporation\Clients\...` also stays whole: the home folder is replaced first, and the rest has no drive letter. The comment promises "only the file's own name is kept". This text goes into the GitHub issue URL. Should: match up to the last backslash before the file name, allowing spaces, e.g. a path ending at a quote, an apostrophe or the end of the line.  
Confidence: confirmed (ran it: C.exe)

### 12. Startup list: items switched off in Task Manager still look active (Startup folder and 32-bit Run list)
Severity: wrong result  
Where: src\WindowsDeep\Inside.cs lines 177-187 (only `StartupApproved\Run` is checked, by value name), line 193 (ExeOf on unquoted paths)  
How to reproduce: disable a Startup-folder shortcut, or a 32-bit app from HKLM\...\WOW6432Node\...\Run, in Task Manager › Startup apps.  
What happens / what should happen: Windows records those under `StartupApproved\StartupFolder` (value name with the ".lnk") and `StartupApproved\Run32`, so they are never marked "switched off in Task Manager". Separately, an unquoted Run value with spaces (`C:\Program Files\Realtek\Audio\HDA\RtkNGUI64.exe -s`) gives Exe "Program", so "why did it start?" misses it. Should: check Run32 and StartupFolder (file name with extension), and resolve unquoted paths with spaces up to ".exe".  
Confidence: suspected (read it)

### 13. A program running from C:\Windows\Temp is described as "ships with Windows"
Severity: wrong result  
Where: src\Knowledge\Explain.cs lines 76-81 (Where: only the user's %TEMP% counts as "temp"; C:\Windows\Temp falls under "windows")  
How to reproduce: run any exe from C:\Windows\Temp (a common malware and installer location for SYSTEM tasks), then open What's running.  
What happens / what should happen: WhereText says "It lives in the Windows folder, so it ships with Windows (or a driver)." Watch.Conns still flags it, by its own "\Temp\" path test. Should: check `%WINDIR%\Temp` (and other Temp folders) before "windows".  
Confidence: confirmed (read it; same logic as the harnessed paths)

### 14. Wi-Fi 7 320 MHz shows as 160 MHz; WPA3-Enterprise (non-192) shows as WPA2-Enterprise; FILS as plain "WPA2"
Severity: cosmetic  
Where: src\Link\LinkInfo.cs lines 314-342 (ReadIes: no EHT Operation, ext. id 106), 279-281 (Rsn)  
How to reproduce: W.exe: an HE 6 GHz IE (width code 3) plus an EHT Operation IE with width 4 (320 MHz); RSN AKM 5 alone; AKM 14.  
What happens / what should happen: "width=160"; AKM 5 → "WPA2-Enterprise" (WPA3-Enterprise only mode is AKM 5 with PMF required); AKM 14 → "WPA2". AirLog then judges a 320 MHz neighbour's overlap with a 160 MHz block. Should: read EHT Operation's channel width; report AKM 5 with MFPR set as WPA3-Enterprise. All the other band, channel and width math checked out (see below).  
Confidence: confirmed (ran it: W.exe)

### 15. Localized netsh: the rate, standard and security rows show "-" for several languages the comment lists, and the German rate is lost in the netsh fallback
Severity: cosmetic  
Where: src\Link\LinkInfo.cs lines 474-490 (comment claims Dutch and Polish; NormKey has no Polish or Dutch rate labels and no Italian "Tipo frequenza radio"); src\Monitor.cs line 452 (ParseWifi parses "144,4" with InvariantCulture → -1)  
How to reproduce: N.exe it, pl, fr, ru (sample labels from my knowledge: "Szybkość odbierania (Mb/s)", "Velocità ricezione (Mbps)", "Réception (Mbits/s)", "Скорость приема").  
What happens / what should happen: Receive and Transmit rate "-", Standard "-" (it), Security "- · -" (ru, ja, zh, ko, ar, tr). Monitor's netsh fallback rate is -1 for "144,4", so "link rate dropped" events and the RxRate history column go blank there. Should: map these labels, or read the rate by shape: the number on the line whose label contains "(Mb".  
Confidence: confirmed on the samples (ran it); the exact label words are suspected

### 16. Driver dates show one day early west of UTC
Severity: cosmetic  
Where: src\WindowsDeep\DevInfo.cs line 71 (`FromFileTimeUtc(ft).ToLocalTime()` for DriverDate)  
How to reproduce: a device whose INF says DriverVer 03/15/2024, on a PC in Ottawa (UTC-5).  
What happens / what should happen: shown as 14 Mar 2024. Inbox() already allows "20 or 21 June 2006" for this reason. Should: keep DriverDate in UTC (date only); only Arrived and Removed are real moments.  
Confidence: suspected (read it)

### 17. Learn › hash: says MD5/SHA-1 let you "build a second file with the same fingerprint"
Severity: cosmetic  
Where: knowledge\learn.txt `== hash` line "more:"  
How to reproduce: Learn › A hash.  
What happens / what should happen: MD5 and SHA-1 are broken for collisions: someone can make two files that share a fingerprint. They are not broken for matching an existing file's fingerprint (second preimage). As written, the text suggests any downloaded file's SHA-1 can be forged to match. Should: "...and nobody can make two different files with the same fingerprint (MD5 and SHA-1 are broken for that)".  
Confidence: confirmed (read it)

## Still present from earlier rounds

| earlier id | where now | one-line evidence |
|---|---|---|
| R1 #142 / R2 #38 (live page part) | src\Link\LinkInfo.cs line 538 (Overlaps), used by LeastCrowded line 301 and Collect line 602 | 5 and 6 GHz still `a == b`: an 80 MHz neighbour on 36 doesn't "share" your 44, and LeastCrowded can pick 40 inside the neighbour's block. AirLog now uses blocks, so only the live page and the advisor are affected. |
| R3-regressions #2 | src\Capture\LiveCapture.cs lines 264-270 | Partly fixed: a restart under 30,000 lower still hides all later loss (R.exe case 4a, 3.75% → 0.00%). See finding 6. |
| R1 #65/#66, R2 #37 | src\Link\LinkInfo.cs KeyWords; src\Monitor.cs ChanKeys/StateKeys | Partly fixed: Korean and Arabic channel/state and the Russian/Japanese/Chinese per-AP labels are still unread. See finding 1. |
| R2 muse "4-hour row cap vs summary" | src\Brain\Watch.cs lines 62 and 71-72 | `mins` comes from `started`, loss from the first 14,400 rows. |
| R2 muse "OpenWifi misses saved WEP" | src\Link\LinkInfo.cs line 171 | `open` still needs `<encryption>none`, so a saved WEP profile that joins by itself isn't flagged. |
| R2 muse #18 (new-device pop-ups) | src\Brain\Watch.cs line 245 | Partly fixed (random MACs no longer count). A home with more than 30 fixed-MAC devices ever seen still never gets a pop-up, and the set is never pruned. |
| R1 #146 (same-second names) | src\Capture\Packets.cs line 38 (`journey-HHmmss.pcapng`); src\Capture\LiveCapture.cs line 420 (`adebometer_<app>_HHmmss`) | The work folder now has a GUID, but these two final names still collide within one second. |
| R2 muse #24 (window scaling) | src\Capture\Journey.cs lines 334-339 | Mitigated by the 0.7-1.1× band; a scaled window that happens to sit in that band is still reported as "capped". |

## Checked and held up
- WLAN structures: WLAN_INTERFACE_INFO (532 bytes, state at +528), WLAN_CONNECTION_ATTRIBUTES offsets (SSID 520/524, BSSID 560, signal 576, rx kbps 580, size ≥ 588), WLAN_BSS_ENTRY (360 bytes; RSSI 56, centre frequency kHz at 92, capability 88, IE offset/size 352/356), opcodes 7 and 8, and the interface-state names. Handles are freed on every path; rc 5 maps to the location block.
- Frequency → band/channel: 2412-2472 → 1-13, 2484 → 14, 5160-5885 → 32-177, 5935 → 6 GHz ch 2, 5955 → 1, 7115 → 233. HT40 bit, VHT 80/160 (new CCFS1 rule and old width 2/3), 80+80 → 80, HE 6 GHz widths 20/40/80/160 all right (W.exe). RSN parsing is bounds-safe for counts that overrun the element.
- AirLog.Block: 5 GHz 40/80/160 blocks (36-48, 100-112, 132-144, 149-161, 36-64, 100-128) and 6 GHz blocks from ch 1 are correct. Channel-change and mesh handling hold.
- ActiveBlock with the header and "Hosted network" blocks, Turkish "%88" and NBSP/NNBSP before %: picks the connected adapter's block.
- RTP: clean wrap 0%, 1% loss across a wrap → 1.20% (exact), SSRC change → separate streams with correct loss, 90 kHz video now gets its clock (R2 muse #23 fixed), 32-bit timestamp wrap (mid-call and in the first 0.2 s) adds no jitter spike, Opus jitter converges to the RFC 3550 value.
- Brain: per-minute averages now over real samples only; flush stamped with its own minute (sleep-safe); Bloat only on new gateway samples; one long-lived PC-jobs thread; Baseline never learns silent minutes and resets the recent window per network; NaN round-trips through baselines.txt.
- Outages.Pair: end-unseen on network change, check switch-off, or a second "quiet" more than 10 minutes later; DST fall-back can't give a negative length.
- Alerts cooldowns per kind; Watch keys versioned folders but reads facts from the real file; Crash.Allowed parses the host and path exactly; IssueUrl stays under 7,000 characters.
- Journey: BPF built only from a parsed IP and integer ports; Rewind reads at most two ring files of 32 MB or less; Stop kills dumpcap within 500 ms; payloads are stripped except ICMP; the resend, keepalive and dup-ACK marks match the RFC 9293 wording.
- LiveCapture: the extras restart only on tshark's own field complaints; a stale reader can't stop its replacement (H2); the 300 MB restart can't throw out of Snapshot.
- DevInfo DEVPKEY GUIDs/PIDs and property types; Svc ENUM_SERVICE_STATUS_PROCESS stride 56/44 and the State/PID offsets; ProcTree PID-reuse age check and cycle breaking (R1 #144 fixed); PcHealth "on battery 255%" fixed; Crashes read the language-neutral event data.
- M365.Role is bound to the domain (R1 #141 fixed); InRange is correct for /14 and /15; WebView2 is attributed by walking up parents.
- Knowledge: no Nokia terms (VPRN gone; grep for SR OS, SAP, SDP, MD-CLI, 7750, VPLS and similar: 0 hits). The mobile ladders now match TS 23.401/23.502 (handover ack/command, Create Session Response from the SGW, PDU session via the AMF). BGP, IGP, MPLS, L3VPN, multicast and CDN facts are right. The crypto entries are right apart from #17 (P-256 ≈ RSA 3072; SHA-256 = 64 hex digits).
- Rules: nothing here sends network traffic without a user action. WlanGetProfile never asks for plaintext keys (rule 4). The new code has no listening sockets and no busy loops.
