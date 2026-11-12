# Findings - Claude Code, round 2

Reviewed commit: a0fbd56 (dev-1.1.5), 11 commits since round 1's f6d5747. The checkout at /home/user/AdeeboMeter-dev was read-only: nothing was edited, committed or pushed, and nothing was filed on GitHub.

Method:
1. A mono (mcs, C# 5) build of a0fbd56 with the knowledge resources compiled in. The Windows-only ECDiffieHellmanCng gets a small stub so the build links. Everything else is the real source.
2. Reflection harnesses that load that build (portable mode) and call its internal methods: Calc, LabSim, Journey, Guided.Lag, Picks.ParseWhen, PathTools, Outages, Points, HallOfFame, UpdateBreak, AirLog, Monitor.ParseWifi, Lab.NotOne, SpeedTest.SafeUrl, FileCrypt, DestCheck, the widget renderers and BuildSnap. `--report` was run under `xvfb-run mono`. A hostile SMTP server re-checked round 1's mail-server fixes.
3. Python reference diffs: VLSM against `ipaddress` (600 random cases), the number converter (800), bandwidth (500), the subnetting quiz (all 65,536 seeds).
4. Playwright (Chromium) against `docs/hall-of-fame.html` with a hostile `hall-of-fame.json`.
5. A dummy-key "official-style" build for the Hall of Fame test: `obj/HofKey.cs` made exactly as `release.ps1` makes it, but holding a made-up review key. That file was deleted after the build. `.github/scripts/hof_verify.py` was run locally with `HOF_KEY` set to the same dummy value.

**No real Hall of Fame key was seen or used.** The real key isn't in this checkout or its git history, and there is no `secrets/`, `obj/HofKey.cs` or `HALL-OF-FAME-SETUP.txt` on this machine (`find /`, `git log -p --all -S`). This report contains no secret value.

"Confirmed" means it was run: under mono on Linux (not Windows) against the a0fbd56 build, by a harness, by Playwright, or by running the verifier script. "Suspected" means it was only read. Most suspected items depend on Windows behaviour that mono doesn't have. The lead reviewer also read the key lines of several findings again at a0fbd56 (noted as "lead re-read" where it applies). Line numbers are at a0fbd56.

## Round 1 fix status

All 244 round-1 findings were re-checked against a0fbd56: the three "Review fixes" commits (157e38d, 3a888b4, 5a5ae0d) were read line by line, and the round-1 repro was re-run wherever it can run without Windows.

| Status | Count |
|---|---|
| FIXED | 181 |
| PARTIAL | 45 |
| STILL PRESENT | 15 |
| REGRESSED | 2 |
| N/A | 1 (#128: WMI InstalledOn really is M/d/yyyy, so round 1 was likely wrong) |
| **Total** | **244** |

**Still present (15):**
- #38 Ask-the-network discovery binds UDP sockets (rule 2, owner to decide) → finding 9
- #43 Version is still 1.1.4 → finding 58
- #111 System-aware DPI only → finding 123
- #172 Pcap reader: a 63 MB capture takes about 175 MB → finding 92
- #191 French text clipped in fixed-size rectangles → finding 122
- #229 TWAMP-light, MTU over UDP and the Engineer pack send without the Lab gate → finding 6
- #231 History, game history and outages ignore portable mode → finding 57
- #232 The TCP:443 fallback never comes back after one ICMP reply → finding 56
- #233 CI never builds dev-1.1.5 → finding 59
- #234 "printer" in Toolbox search opens Ping sweep → finding 60
- #238 "Same certificate" names the issuer → finding 65
- #239 DSCP compare: `100` can't be typed; headline ignores unmarked loss → finding 66
- #240 Game-ping TCP fallback leaks wait handles → finding 108
- #241 802.1X replay judges only the first 200 frames → finding 67
- #243 Leftovers: Cookie redaction, "ssl" search, dmarc words, Security Save → finding 13

**Regressed (2):**
- #59 MTR outage past hop N: the fix (new `Answers` rule) now turns a quiet tail hop into "real loss to the end" → finding 21 (N1)
- #97 SNMP 32-bit counter reset: the fix now shows every real wrap as "reset" on devices without ifXTable → finding 26 (N6)

**New bugs introduced by the fixes (N1-N7):** N1 → 21, N2 → 25, N3 → 24, N4 → 1, N5 → 2, N6 → 26, N7 → 7.

**PARTIAL items with a concrete remaining repro** have their own entries below, titled "Round-1 #N partly fixed": #1 → 52, #19 → 12, #24 and #36 → 7, #28 → 16, #29 → 75, #33 → 11, #39 → 106, #54 → 61, #55/#163/#165/#199 → 130, #56 and #154 → 28, #60 → 25, #65 → 36 and 37, #73 → 33, #74 → 34, #77 → 53, #101 → 62, #124 → 54, #134 → 63, #136 → 14, #142 → 38, #148 → 119, #150 → 109, #159 → 2, #164 → 1, #176 → 104, #178 → 102, #179 → 93, #187 → 105, #190 → 121, #193 → 129, #207 → 76, #213 → 126, #217/#220/#222/#223 → 131, #244 → 77. #157, #195 and #197 have no concrete remaining repro. Residuals of FIXED items that still have a repro: #13 → 15, #31 → 19, #45 → 128, #85 → 127, #95 → 71.

The per-finding table (status, line at a0fbd56, evidence and where Muse differs) is in `R2-fixcheck.md` in this folder.

## Rule breaks

| Rule | Findings |
|---|---|
| 1. Offline (only what the user ran or About lists) | #8 (DoH box rewritten, a plain DNS query goes out instead), #31 (About under-reports the view-open pace 11×), #10 (air.txt kept and not disclosed) |
| 2. Never listens | #9 (still present, owner to decide) |
| 3. No admin | none found |
| 4. Secrets never kept | #13 (Cookie values after the first `;` kept in Copy/Save), #18 (RADIUS secret not wiped on two early exits, memory only) |
| 5. SNMP read-only | none found |
| 6. Policy only switches things off | none broken. Policy can't switch anything on (`mode=pro` ignored, booleans only OR in "off", history only lowers). Related: #15 (an IT policy over 16 KB is silently ignored), #118 (display only) |
| 7. Low weight | #84, #85, #86, #87, #88, #89, #90, #91, #92, #93, #95, #96, #98, #99, #100, #101, #102, #103, #104 |
| 8. No Nokia references | none (grep over src, knowledge, docs, README, CHANGELOG, store: 0 hits) |
| 9. Examples are made-up data | none found |
| 10. Points can't be farmed | #3, #4, #5, #32, #33, #34, #6 (ungated senders also earn points) |
| Brief guards (Lab tick, one target, 10/s, 1,000 cap; MTR loss rule) | #6, #7, #21 |

## Top 15 to fix before release

1. #3 - add 250 junk lines to points.txt and the official exe signs a genuine 500-point Hall of Fame card. No key needed (rule 10, confirmed, verifier said `added`).
2. #21 - MTR/ISP report regression: a quiet tail hop makes an earlier hop's rate-limit loss "real loss to the end" (brief's MTR rule, confirmed).
3. #22 - VLSM: one subnet that doesn't fit makes every later subnet "doesn't fit" (confirmed, 11 of 600 random plans wrong).
4. #6 - the Lab gate is still bypassed by TWAMP (2,000 packets), UDP MTU, RADIUS lab, SMTP path and the new DNS race (brief guard).
5. #7 - `::ffff:` IPv4-mapped addresses get past the new "one device only" and speed-server guards (confirmed).
6. #24 - ReplaceFile's fallback deletes points.txt, keys-ec.txt or settings before the move; a failed move loses them.
7. #23 - `--report` never exits 0 on a healthy PC (confirmed, exit 3 every run).
8. #8 - dig and DNS compare silently turn a DoH URL into a plaintext UDP/53 query (rule 1).
9. #1 - Exit from the tray under a modal dialog still runs code on the disposed view (crash).
10. #2 - the new SessionEnding handler touches WinForms timers off the UI thread (crash).
11. #27 - TLS, certificate, DoT and mail tools now try IPv6 first and fail on broken-IPv6 networks where 1.1.4 worked.
12. #25 - outage log: "network changed" and "check switched off" close an outage with a made-up end (confirmed).
13. #26 - SNMP Traffic regression: every real 32-bit wrap shows as "reset" on devices without ifXTable.
14. #5 - the Hall of Fame verifier publishes 99999 points, repeat names and impersonations with no human step (confirmed).
15. #10 - air.txt (neighbours' BSSIDs, a location trail) is kept forever once History is off, and no privacy page mentions it.

Release-time housekeeping that also has to happen: #58 (version still 1.1.4) and #59 (CI never builds the dev branch).

## Cost table

From `R2-perf.md`. All rates assume the widget is on, the mouse is away from the corner, the PC is idle and no game is added, unless the row says otherwise. P / B / E = Penny / Balanced / Engineer. Wakeups count timer ticks and loop iterations; packets count requests AdeeboMeter sends.

| # | Recurring job (where) | Interval P / B / E (view closed) | View open | Work per tick | Stops when hidden / locked / on battery? | Wakeups/min P / B / E | Packets/min P / B / E |
|---|---|---|---|---|---|---|---|
| 1 | Widget UI timer (AppCtx.cs:146, OnUi 313) | 250 ms in every preset; 50 ms near the corner; 500 ms hidden / view up / presenting | same | Screen.AllScreens 3-4×, Cursor.Position, ~500 B state string, Presenting() every 8th tick | **No**, never; preset doesn't touch it | **240 / 240 / 240** (1,200 near the corner, 120 hidden) | 0 |
| 1a | Cover check WindowOver (AppCtx.cs:328, 384) | every 400 ms | same | EnumWindows + 4 calls per visible window, new delegate thunk each call | No | ~120 (inside the 240) | 0 |
| 1b | Widget render (AppCtx.cs:357-376) | chip: on change + every 30 s; **open card: every second** (finding 84) | same | chip 0.25 ms / 3.9 KB; **card 2.17 ms / 14.4 KB / 271 KB bitmap** + GetHbitmap copy + UpdateLayeredWindow (100 % scaling; ×2.25 at 150 %) | No | chip ~2; **card 60** | 0 |
| 2 | Sampler thread (Monitor.cs:119-152) | 1 s | 1 s | system/memory/3 disk counters, IP/TCP/UDP/ICMP stats, **BuildSnap 0.08 ms / 30 KB**, Brain.Second; Judge every 10 s | No | **60 / 60 / 60** | 0 |
| 2a | Brain PC jobs (Brain.cs:65-71) | PcHealth 15/5/5 s; HungTick 30/10/10 s; GwTick 90/30/30 s; IoTick 180/60/60 s; LeakTick 15/5/5 min; SpaceTick 6 h | same | as listed | No | ~7 / ~21 / ~21 | 0 |
| 2b | Brain per-minute flush (Brain.cs:100-108) | 1 min | same | History append, Usage (rewrite every 5 min), Baseline, Devices every 10 min, **AirLog hourly**, **UpdateBreak hourly** (2 System-log queries over 14 days + History reads) | No; AirLog and Devices respect History, UpdateBreak doesn't | 1 + small | 0 |
| 3 | Probe thread (Monitor.cs:208-268) | 10 s / 5 s / 2 s | **2 s in every preset** (also during a Teams call) | gateway ping; 1.1.1.1 ping + **DnsQuery BYPASS_CACHE**; every 10 s interfaces + ARP + DriveInfo; netsh; TCP table; process sampling | Only through the preset | 6 / 12 / 30 (30) | closed **8 / 16 / 38**; **open 90 in every preset** |
| 3a | netsh wlan show interfaces (Monitor.cs:437) | 120 / 30 / 10 s, Wi-Fi only | 10 s | **a new netsh.exe process** + console host, up to 3 s on the probe thread | No | 0.5 / 2 / 6 process starts (6) | 0 |
| 3b | ProbeProcesses (Monitor.cs:566) | 30 / 10 / 5 s | 4 s (2 s on What's running) | GetProcesses + OpenProcess/GetProcessTimes for ~250 processes, an exception per protected one | No | 2 / 6 / 12 (15-30) | 0 |
| 3c | LearnIsp (Monitor.cs:459) | once per network | once | 6-hop trace + PTR | n/a | n/a | ~18 per network change |
| 4 | Gamer loop (Gamer.cs:383-420) | **1 s always**, even with no game | 1 s | settings split; with games: GetProcesses every 5 s, probes | **No**; Penny doesn't change it | **60 / 60 / 60** | no game 0; 1 game × 12 regions ~4.2; 3 games ~12.6 (more than all of Penny); playing up to 480 |
| 5 | Single-instance wait (AppCtx.cs:156-171) | WaitAny 1 s timeout only to re-check `quitting` | 1 s | nothing | No | **60** | 0 |
| 6 | Pinned watch (Watch.cs:256) | 30 s, opt-in | 30 s | a ping | No | (on the sampler) | 2 if on |
| 7 | Engineer view timer (FullView.cs:309, 510) | n/a | 500 ms | **pathPanel, side and footer repaint every second with identical data (measured 21/21/20 in 20 s)**; RefreshConns 10 s (2 s on some pages); RefreshIpNames 15 s, can start `ipconfig /displaydns` every 30 s | Keeps running behind other windows or when locked; minimize closes it | (120 + 3 panel paints/s) | 0 |
| 7a | Freeze watchdog (FullView.cs:71) | n/a | 1 s timer + BeginInvoke | a timestamp | stops with the view | (60 + 60) | 0 |
| 8 | Labs 66 ms, Journey 33 ms, SlideSwitch 15 ms | n/a | only while playing | animation | **Yes**, stop when hidden or done | 0 idle | 0 |
| 9 | Stream overlay (StreamOverlay.cs:21, 46) | 1 s while open | 1 s | full repaint, **2 new Fonts per paint**; with PNG on, a second paint + PNG encode every second | No | 60 (+60) | 0 |
| 10 | Live capture limit timer (LiveCapture.cs:125) | 30 s while capturing | | CheckLimit | stops with the capture | 2 | 0 |

**Totals, view closed, idle:** Penny ≈ **434** wakeups/min (7.2/s), Balanced ≈ **453** (7.6/s), Engineer ≈ **471** (7.9/s). Only 13 / 33 / 51 of those come from jobs the preset controls. The other ~420 are the same in every preset: widget timer 240, sampler 60, Gamer loop 60, single-instance wait 60. With the view open add about 120 (view timer) + 120 (watchdog) + 180 panel repaints, and the probe moves to 30/min in every preset.

**Stated packets against real ones:**

| Claim (source) | Penny | Balanced | Engineer |
|---|---|---|---|
| Header (Program.cs:5-7) and About › "What leaves your PC" | 6 gw + 1 ping + 1 DNS = 8/min | 12 + 2 + 2 = 16/min | 30 + 4 + 4 = 38/min |
| Actual, view closed | **8 (matches)** | **16 (matches)** | **38 (matches)** |
| Actual, view open (when About is being read) | **90** | **90** | **90** |
| Actual, Teams call, view closed | 8 (Call off in Penny) | 72 | 90 |
| Settings "~N checks a minute" (Program.cs:338) | 18 | 36 | 74 |
| Same, adding uncounted PC jobs, Gamer loop and widget | ≈ 25 + 60 gamer | ≈ 57 + 60 | ≈ 95 + 60 |

**Penny recommendation.** Penny today is only 4 % lighter than Balanced in wakeups. Suggested Penny values: widget idle interval 1 s, stopped when hidden or locked; unhovered card redraw at most every 10 s; sampler 2-5 s, with BuildSnap skipped when nothing reads it; disk counters dropped; interfaces / ARP / C: free space every 60 s (NetworkAddressChanged already wakes the probe); gateway ping 30 s; internet ping + DNS 5 min; DNS never faster than 60 s, even with the view open; Wi-Fi through WlanApi in-process every 120 s; Gamer background 60 s with the survey off or every 30 min (games being played keep 1 s); AirLog every 3 h; UpdateBreak once a day; view-open pace capped at 5 s; Gamer loop and single-instance wait event-driven. That brings Penny to about 70-80 wakeups/min (≈ 1.2/s) and about 3 packets/min.

---

## Crash

### 1. Round-1 #164 partly fixed (new bug N4): Exit from the tray under a modal dialog still runs code on the disposed view, and the comment now says it's handled
Severity: crash  
Where: src\AppCtx.cs line 292 (Quit, changed in 5a5ae0d)  
How to reproduce: open Ctrl+K (a Form) or Save/Export (a CommonDialog) in the engineer view, then choose Exit from the tray.  
What happens / what should happen: for a modal Form, `f.Close()` only sets DialogResult; ShowDialog returns after this handler has already run `full.Close(); full.Dispose()`, so `pal.Chosen()` and ShowTab run on the disposed view (ObjectDisposedException, logged as "UI error"). CommonDialogs aren't in Application.OpenForms, so they're never closed and their owner is destroyed under them. Should: close the modal and re-post Quit with BeginInvoke so it runs after ShowDialog returns, or check IsDisposed after every ShowDialog.
Confidence: suspected (read it; WinForms modal-loop semantics)  

### 2. Round-1 #159 partly fixed (new bug N5): the new SessionEnding handler stops WinForms timers and captures off the UI thread
Severity: crash  
Where: src\AppCtx.cs line 281 (OnSessionEnding, added in 5a5ae0d for #18) → src\Ui\FullView.cs line 734 (StopCaptures: `jTimer.Enabled = false`, `jSess.Stop()`, `live.Stop()`)  
How to reproduce: log off or shut down with the engineer view open on a running packet journey.  
What happens / what should happen: SystemEvents raises SessionEnding on its own thread (the same release marshals UserPreferenceChanged at AppCtx.cs:137 for that reason, but not this one). A Forms Timer is changed from a foreign thread and captures are stopped concurrently with the UI's own Stop; CleanCaptures then races the still-running journey writer. The try/catch logs it, but the timer can silently keep firing. Should: `marshal.Invoke` (synchronous, so it completes before logoff), then CleanCaptures.
Confidence: suspected (read it)  

## Security

### 3. Hall of Fame: add 250 junk lines to points.txt and the official exe signs a genuine 500-point card - no key extraction needed
Severity: security  
Where: src\Points.cs line 84 (Total) and lines 36-43 (Value); src\Ui\FullView.Points.cs line 91 (MakeAwardCard)  
How to reproduce: write `zz0|1` … `zz249|1` (250 lines), or 50 lines `path.fakeN|1`, into points.txt. Start an official build and press Make the card.  
What happens / what should happen: Total sums Value(id) over every key in the file, and Value returns 2 for any unknown id (10 for any `path.*`, 4 for `lab.*`). The total is exactly 500, and MakeAwardCard signs it with the built-in key. 20,000 `path.N` lines give 200,000. A line `ping|1|<ticks>` adds the +3 replay bonus to a tool that has no replay. Should: count only ids PtKeyKnown accepts, give the replay bonus only to ids in Replay, cap the total at the real maximum (about 580), and ideally put the earned ids into the signed message.
Confidence: confirmed (ran it: dummy-key official-style build gave Points.Total=500, HallOfFame.Code + Card made the PNG, and hof_verify.py with the same dummy key answered `added`; lead re-read Value/Total)  
Rule: 10  

### 4. Hall of Fame: anyone with the official exe can sign any card - `HallOfFame.Key` and `Code` are public, and the XOR mask can be undone blind
Severity: security  
Where: src\HallOfFame.cs line 31 (Key, public), line 34 (Unmask), line 49 (Code, public); release.ps1 lines 41-46  
How to reproduce: (a) `Assembly.LoadFrom("AdeeboMeter.exe")` → `HallOfFame.Key` → `HallOfFame.Code(key, "Adeeb Khan", 99999, "2026-11-12")`, about 6 lines of C#. (b) Without .NET: slide the public `(i*73+41)&0xFF` mask over the PE; the masked bytes are one array-initializer blob and a printable run appears at the key's offset.  
What happens / what should happen: the forged code is accepted by the workflow (a card for "Adeeb Khan" with 99999 points was `added`). A symmetric key shipped to every user can't be fixed by masking harder. Should: describe the code honestly ("checks the card wasn't edited, doesn't prove the points"), make Key/Code internal, add the plausibility checks in #5, and have a person approve entries.
Confidence: confirmed (ran it: reflection forge and the XOR scan against the dummy-key build)  
Rule: 10  

### 5. Hall of Fame verifier publishes impossible totals, repeat names and impersonations with no human step
Severity: security  
Where: .github\scripts\hof_verify.py line 74 (`\d{3,5}`), line 83, line 95 (de-duplicates by code only); .github\workflows\hall-of-fame.yml  
How to reproduce: submit valid codes for "Adeeb Khan" with 99999 points, then 99998; submit "Zed" with points `00500`; use a name like `free-prizes.example.com` or Cyrillic "Adееb".  
What happens / what should happen: 99999 is `added` (real maximum about 580); the same name with 99998 is a second entry; `00500` is accepted; any name goes straight onto the public page and domain-shaped names are autolinked in the bot's comment. The "Your card" picture only has to be some user-attachments URL. Should: reject totals above the real maximum, keep one entry per case-folded NFKC name or per issue author, record the author's login, and publish through a PR or an owner-set "approved" label.
Confidence: confirmed (ran hof_verify.py with a dummy HOF_KEY on copies)  
Rule: 10  

### 6. Round-1 #229 still present: TWAMP, UDP MTU, RADIUS lab, SMTP path and the new DNS race send traffic without the Lab gate (and earn points)
Severity: security  
Where: src\Ui\FullView.Toolbox.cs line 207 (LabGate, unchanged 10 ids) and line 211 (LabToolsMissingGate scans "lab" sections only); dispatches at :31 udpmtu, :55 twamp (cap 2,000), :111 radiuslab, :112 smtppath, :147 dnsrace  
How to reproduce: on a company domain or VPN, without the "allowed to send test traffic" tick, run TWAMP-light with count 2000, MTU over UDP, the RADIUS lab, or DNS race.  
What happens / what should happen: they send with no tick and no Lab.Log line; TWAMP's cap is twice the Lab's 1,000. DNS race (new) sends up to ~168 UDP queries to the DNS server, router, 1.1.1.1, 8.8.8.8, 9.9.9.9 and up to 3 user IPs. They also still earn points (Value = 2). Should: add them to LabGate (or call Lab.Allowed in each), cap TWAMP at 1,000, and make the self-test scan every section.
Confidence: suspected (read it; lead re-read LabGate at line 207, unchanged; Muse agrees)  
Rule: brief guard (Lab tick, 1,000 cap); 10 (ungated senders earn points)  

### 7. Round-1 #24 and #36 partly fixed (new bug N7): an IPv4-mapped address gets past the new "one device only" and speed-server guards
Severity: security  
Where: src\Checks\Lab.cs line 68 (NotOne, added in 157e38d); src\Checks\SpeedTest.cs line 77 (SafeUrl → Watch.IsPublic)  
How to reproduce: Traffic Lab udpsend to `::ffff:255.255.255.255:9`, or ICMP builder to `::ffff:224.0.0.251`; a speedservers.txt line `http://[::ffff:192.168.1.1]/cgi-bin/x`.  
What happens / what should happen: NotOne returns "" (allowed) for all three mapped forms (it checks IPv6 only with IsIPv6Multicast), and SafeUrl returns true for `[::ffff:127.0.0.1]` and `[::ffff:192.168.1.1]`. A dual-mode socket sends them as IPv4 broadcast, multicast or LAN traffic. SafeUrl also passes names that resolve to private addresses (`127.0.0.1.nip.io`, `localtest.me`, `router.asus.com`). Should: unwrap with `IsIPv4MappedToIPv6 ? MapToIPv4()` before every check, and check the resolved address at connect time for speed servers.
Confidence: confirmed (ran NotOne and SafeUrl on the a0fbd56 build); delivery on the wire suspected  
Rule: brief guard (one target)  

### 8. dig and DNS answer compare: a DoH server URL is silently rewritten to a bare host name, so the query goes out as plain UDP/53
Severity: security  
Where: src\Ui\FullView.Toolbox.cs line 243 (UrlTools lacks `dig` and `dnscmp`) and line 671 (StartTool URL rewrite) → src\Checks\Toolkit.cs line 389 (Dig, DoH branch never reached), src\Checks\MoreTools.cs line 130 (DnsCompare)  
How to reproduce: Toolbox › dig, `example.com`, `A`, server `https://cloudflare-dns.com/dns-query` (the box's own example). Or DNS answer compare with Other DNS = `https://dns.google/dns-query`.  
What happens / what should happen: the new "pasted URL where a host is wanted" loop replaces the box with `UrlToTarget(v)` = `cloudflare-dns.com` (also on screen). Dig resolves that name with the PC's DNS and sends an unencrypted UDP/53 query to it. A user who chose DoH so the query isn't visible gets a plaintext query to an endpoint they didn't ask for. Should: add `dig` and `dnscmp` to UrlTools, or skip boxes whose label mentions DoH or `https://`.
Confidence: confirmed (ran it by reflection: `UrlTools.Contains("dig")` and `("dnscmp")` are false, and `UrlToTarget` returns `cloudflare-dns.com`; lead re-read line 243). The full click path wasn't run.  
Rule: 1  

### 9. Round-1 #38 still present: Ask-the-network discovery binds UDP sockets that accept unicast replies from any LAN host
Severity: security  
Where: src\Checks\Reach.cs line 210  
How to reproduce: run the discovery tool; for its 4 s window the socket accepts unicast datagrams from any host.  
What happens / what should happen: unchanged since round 1; still waiting for the owner's decision on whether a short-lived receive socket counts as "listening".
Confidence: suspected (read it)  
Rule: 2 (owner to decide)  

### 10. Neighbour wars: air.txt (neighbours' BSSIDs and SSIDs) outlives "7 days" once History is turned off, and no privacy page mentions it
Severity: security  
Where: src\Link\AirLog.cs lines 23 and 34-36 (pruning only inside Record, which returns at once when History is off); docs\privacy.html line 45; src\Ui\FullView.Settings.cs line 132 (History help text)  
How to reproduce: keep History on for a week while travelling (home, office, cafés), then turn History off.  
What happens / what should happen: up to 40 BSSIDs per hour stay in air.txt indefinitely; BSSIDs can be geolocated, so this is a location trail. Neither privacy.html nor the History help mentions air.txt; only "Delete all saved history" removes it, and that link shows only while `HistDaysCached() > 0`. Should: prune or delete air.txt when History goes off and at start-up; store only your own SSID/BSSID plus per-channel counts and strongest RSSI for neighbours; list it on the privacy page.
Confidence: suspected (read the pruning path; privacy page checked with grep)  
Rule: 1 (disclosure)  

### 11. Round-1 #33 partly fixed: a kill, crash, logoff or plain Exit during decrypt leaves a hidden plaintext `.part`, and nothing cleans it up
Severity: security  
Where: src\Checks\FileCrypt.cs (Temp/Drop, `finally { Drop(tmp); }` at line 344); src\AppCtx.cs (Quit → ExitThread without stopping tool tasks)  
How to reproduce: start decrypting a large .adeebo (the `.<guid>.part` was seen at 56 MB of 300 MB), then end the process from Task Manager, log off, or choose Exit from the tray.  
What happens / what should happen: tool runs are thread-pool tasks; on .NET Framework background threads are torn down at exit without running `finally`, so the hidden, not-indexed temp holding unauthenticated plaintext stays (OneDrive syncs hidden files). There's no DeleteOnClose and no start-up sweep of `*.part`. Stop itself works ("stopped - nothing was saved", no `.part`). Should: open the temp with FileOptions.DeleteOnClose until the rename, or sweep stale `.<guid>.part` files at start.
Confidence: suspected (read it; under mono the `finally` ran at exit, so this can't be confirmed here. Stop confirmed with a 300 MB file)  

### 12. Round-1 #19 partly fixed: ISP report redaction still leaks short SSIDs and erases common words that equal the SSID
Severity: security  
Where: src\Gamer\Gamer.cs lines 613-625 (Redact); src\Checks\IspReport.cs lines 83 and 296  
How to reproduce: (a) SSID `Bob` or `ATT` with a row `Wi-Fi: Bob` or `On ATT (channel 6)`; (b) SSID `Router`, `Home` or `loss`.  
What happens / what should happen: (a) a 2-3 character SSID is redacted only when quoted or right after "Wi-Fi ", so it stays readable; (b) a 4+ character SSID that is a common word replaces every occurrence of that word ("[Wi-Fi name] ([internal address])" for "router (…)"), which gives the name away by elimination, the same class as round 1's "ms". Should: redact the SSID only in the fields that hold it, not by global word replacement.
Confidence: confirmed (ran Redact on the a0fbd56 build)  

### 13. Round-1 #243 still present: Cookie header values after the first `;` stay visible in Copy, Save and Compare (plus the other leftovers)
Severity: security  
Where: src\Ui\FullView.Toolbox.cs line 689 (header-hiding regex stops at `;`); line 517 ("cert"/"ssl" search opens HTTP first)  
How to reproduce: HTTP builder with a `Cookie: a=1; session=SECRET` header, then Copy as text or Save.  
What happens / what should happen: only the first cookie value is hidden; `session=SECRET` is kept in the copied or saved text. The other #243 leftovers are also unchanged: "cert"/"ssl" search opens HTTP first, there are no spf/dkim/dmarc search words, and the Security checkup has no Save. Should: hide every value in Cookie and Set-Cookie (and Authorization), not just up to the first `;`.
Confidence: suspected (read it; Muse agrees)  
Rule: 4  

### 14. Round-1 #136 partly fixed: a self-signed interception root named "O=DigiCert Inc" still counts as a public CA
Severity: security  
Where: src\Checks\Inspection.cs lines 57-68  
How to reproduce: install a self-signed root `CN=Proxy, O=DigiCert Inc` (any public CA's O= value) in the user or machine store, then run the inspection check.  
What happens / what should happen: the check now needs a self-signed root and an O= match, but O= is chosen by whoever made the root, so inspection is reported as absent. Should: match by thumbprint against the Windows CTL (or a shipped list of public root thumbprints), not by name.
Confidence: suspected (read it)  

### 15. Round-1 #13 residual: an AdeeboMeter.policy in ProgramData larger than 16 KB is ignored entirely
Severity: security  
Where: src\Policy.cs lines 36-47 (size check)  
How to reproduce: an admin policy file of 17 KB (for example a long comment header followed by `internet_check = off`).  
What happens / what should happen: the whole file is skipped and every flag stays at its default (all features on), with no message. IT believes things are switched off. Should: read the first 16 KB (or parse line by line up to a larger cap) and log or show "policy file too large" instead of silently dropping it.
Confidence: confirmed (ran Policy.Load on the a0fbd56 build: a 17 KB file gives all-off flags, i.e. nothing switched off)  

### 16. Round-1 #28 partly fixed: a settings file planted next to a downloaded exe still suppresses the install offer, and subfolders of Downloads aren't covered
Severity: security  
Where: src\Setup.cs line 70 (ShouldOffer checks `Program.Dir\AdeeboMeter.settings`); src\Program.cs lines 74-100 (InDownloads)  
How to reproduce: (a) put `AdeeboMeter.settings` next to AdeeboMeter.exe in Downloads; (b) extract a zip to `Downloads\AdeeboMeter-1.1.5\` with a portable marker and settings beside the exe, or run from the Desktop.  
What happens / what should happen: (a) the portable marker and settings are now ignored in Downloads, but ShouldOffer still sees the planted settings and doesn't offer to install; (b) a subfolder isn't "Downloads", so planted files are honoured there. Should: ShouldOffer should use the effective WriteDir, and InDownloads should match any path under the Downloads folder (and the Desktop).
Confidence: suspected (read it)  

### 17. Hall of Fame setup file with the key isn't covered by .gitignore
Severity: security  
Where: .gitignore (only `secrets/` is ignored)  
How to reproduce: with `HALL-OF-FAME-SETUP.txt` (which the owner says holds the key) in the repo root, run `git add -A`.  
What happens / what should happen: it would be staged and could end up in public history, which can't be cleaned without rewriting. Should: keep it under `secrets/`, or add `HALL-OF-FAME-SETUP.txt` and `*.key` to .gitignore. After any slip, rotate HOF_KEY with a `v2|` prefix so old builds' codes are known.
Confidence: suspected (the file isn't on this machine)  

### 18. RADIUS lab: the shared secret isn't wiped on the short-reply and no-answer paths (from Muse, not re-run)
Severity: security  
Where: src\Checks\Protocols.cs line 387 (`r.Length < 20` early return) and line 396 (SocketException catch); the wipe `Array.Clear(sec, …)` is on line 388 only  
How to reproduce: run the RADIUS lab against a host that answers with fewer than 20 bytes, or doesn't answer.  
What happens / what should happen: the secret's byte array is left in memory until GC. In-process only. Should: clear it in a `finally`.
Confidence: suspected (read it; lead re-read lines 387-396)  
Rule: 4 (memory only)  

### 19. Round-1 #31 residual: SafeName keeps U+202E, so an imported key's name can be bidi-spoofed in "My keys"
Severity: security  
Where: src\Checks\EcKeys.cs lines 186-191 (SafeName)  
How to reproduce: a key backup whose name is `a\r\nec|evil` followed by U+202E and reversed text.  
What happens / what should happen: SafeName returns `aecevil` plus the U+202E, so the name is drawn reversed. Should: strip Format (Cf) characters too, as Program.Plain does.
Confidence: confirmed (ran SafeName on the a0fbd56 build)  

### 20. Journey: the dumpcap command line is built by string concatenation with the adapter name (from Muse, not re-run)
Severity: security  
Where: src\Ui\FullView.Journey.cs (dumpcap arguments)  
How to reproduce: an adapter whose friendly name contains `"`.  
What happens / what should happen: the quote can break out of the argument quoting. There's no shell (UseShellExecute = false), so the impact is limited to extra dumpcap arguments. Should: use the adapter's index or GUID, or escape quotes.
Confidence: suspected (read by Muse)  

## Wrong result

### 21. Round-1 #59 regressed (new bug N1): MTR/ISP report - a hop that answered the trace and then ignores every ping makes a normal earlier loss "real loss to the end"
Severity: wrong result  
Where: src\Checks\PathTools.cs line 121 (Answers, changed in 5a5ae0d), lines 123-131 (LossStart), 134-143 (MtrVerdict); src\Checks\IspReport.cs line 209 (StartOfLoss)  
How to reproduce: hops 192.168.1.1 0 %; 203.0.113.1 6/120 lost (rate limiting); 203.0.113.5 120/120 lost but it answered the trace (Ip set); destination silent.  
What happens / what should happen: LossStart = 1, and the verdict reads "Real loss starts at hop 2 (203.0.113.1) and carries on to the end - 5% lost from there… judged by the last router that does (hop 3)", although hop 3 answered none of the 120 probes. At f6d5747 the same input gave "No real loss". The fix treats `Ip != ""` as "answers". Should: count a 100 % hop as an outage only when later hops (or the reached destination) also go to 100 %; a tail hop that answers nothing in the run is a quiet router, and "judged by" must name a hop that answered pings. The round-1 #59 repro itself is fixed (→ hop 3, 100 %).
Confidence: confirmed (ran LossStart and MtrVerdict on the a0fbd56 build)  
Rule: brief guard (MTR loss rule)  

### 22. VLSM: one subnet that doesn't fit makes every later subnet "doesn't fit" too
Severity: wrong result  
Where: src\Checks\Calc.cs lines 129-130 (Vlsm)  
How to reproduce: VLSM planner, Block `10.0.1.0/24`, Hosts `500, 100`. Or `192.168.1.0/24` with `300, 50, 50`.  
What happens / what should happen: every subnet shows "doesn't fit - the block is full" and "Used 0 of 256". The 100-host subnet should land on 10.0.1.0/25. `at` is rounded up to the big subnet's boundary before the fit check, and the `continue` leaves `at` past the end of the block. Should: align into a local and commit `at` only when the subnet fits.
Confidence: confirmed (ran it: 600 random plans diffed against Python `ipaddress` gave 11 mismatches, all explained by this bug; lead re-read line 129)  

### 23. `--report` never exits 0 on a healthy PC: the monitor's own "On LAN / On Wi-Fi" event turns the verdict into "couldn't tell"
Severity: wrong result  
Where: src\Report.cs line 34 (Run); src\Checks\Investigate.Whats.cs lines 58-61 (`around`), 99 and 118  
How to reproduce: portable copy with `portal=0` in AdeeboMeter.settings; `AdeeboMeter.exe --report out.json --seconds 5`.  
What happens / what should happen: exit 3, `"verdict": "unknown"`, while `"finding"` says "Nothing is wrong right now". The only level-3 row is "Around then: On LAN (eth0), gateway … (happened then - not necessarily the cause)", logged by the Monitor that `--report` itself started. "Normal for this network" and "Your router is quiet" do the same. An Intune or RMM script never sees 0. Should: only "couldn't run" rows (a dedicated marker or the "u" evidence kind) should downgrade the verdict.
Confidence: confirmed (ran `--report` under `xvfb-run mono` twice: exit 3 both times; lead re-read line 34)  

### 24. New bug N3: ReplaceFile's fallback deletes the target before the move, so points.txt, keys-ec.txt or settings can vanish
Severity: wrong result  
Where: src\Program.cs lines 211-217 (ReplaceFile, added in 3a888b4); used by Points.Save, EcKeys.Save, AppCtx.SaveSettings, Brain stores, Gamer.SaveGame, History.Tidy, StreamOverlay  
How to reproduce: points.txt or keys-ec.txt held open by another process with FILE_SHARE_DELETE but no replace sharing (AV scanner, OneDrive, backup agent), or an SMB share where File.Replace throws IOException; then trigger a save.  
What happens / what should happen: `catch (IOException) { File.Delete(dst); File.Move(tmp, dst); }`: Delete succeeds (delete-pending), Move fails, and the exception propagates. The original is gone and the data sits in `.tmp`. For keys-ec.txt every .adeebo for you becomes "not made for any of your keys" (round 1 #20's outcome); for points.txt the total is wiped (what #72 tried to prevent). Before 3a888b4 a failed Replace left the old file. Should: fall back only on "not supported" errors; move dst to `.bak` first and restore it on failure; load from `.tmp`/`.bak` when the main file is missing.
Confidence: suspected (read it; lead re-read lines 211-217 and confirmed delete-before-move on any IOException; the trigger is Windows sharing behaviour)  

### 25. Round-1 #60 partly fixed (new bug N2): "network changed" and "check switched off" events close an outage as if the internet came back
Severity: wrong result  
Where: src\Monitor.cs lines 253 and 255 (new level-0 "inet" events); src\Brain\Outages.cs lines 40-42 (Pair: any Lv 0 closes)  
How to reproduce: internet quiet at 18:00; at 18:09 Wi-Fi reconnects with a new address, or the user sets the internet check to Off. Pair [quiet 18:00, level 0 at 18:09].  
What happens / what should happen: the log shows "18:00 - 18:09 · 9 min", a normal-looking outage with a made-up end; the new EndUnseen wording is never used for this case. If the outage continues on the new network, it is split in two with a fabricated gap. Should: treat these two events as end-unseen (no length) and close an outage only on "Internet back". Still open from #60 as well: "From" is the 3rd failure, and sleep counts as outage time.
Confidence: confirmed (ran Pair on the a0fbd56 build with the events Monitor now emits)  

### 26. Round-1 #97 regressed (new bug N6): SNMP Traffic on 32-bit counters shows every real wrap as "reset"
Severity: wrong result  
Where: src\Checks\SnmpTools.cs lines 171-180 (rate lambda, changed in 5a5ae0d)  
How to reproduce: Traffic on a device without ifXTable (no HC counters, which is exactly when `hc = false`) and a 1 Gb/s port at line rate; a 32-bit counter wraps every ~34 s, so about 15 % of 5-second windows contain a wrap.  
What happens / what should happen: ifHighSpeed comes from the missing ifXTable, so `port = 0` and every genuine wrap returns -2 ("reset"). The busiest port shows "reset" instead of near 100 %. The phantom 472 Mb/s became a phantom reset on the same devices. Should: fall back to ifSpeed (1.3.6.1.2.1.2.2.1.5); if neither is known, accept one wrap whose rate is plausible and label it "assumed one wrap".
Confidence: suspected (read it; the logic is deterministic)  

### 27. TLS, certificate, revocation, DoT and mail tools now prefer IPv6 and fail in 5 s on networks with broken IPv6 (regression from 1.1.4)
Severity: wrong result  
Where: src\Checks\Toolkit.cs line 25 (Tcp, dual mode); used at Toolkit.cs 657 and 694, Protocols.cs 122, 198 and 234, CertKeys.cs 253, EngPack.cs 50, MoreTools.cs 227  
How to reproduce: a PC with a global IPv6 address but no working IPv6 path (a common router or ISP fault); run HTTP & TLS against a dual-stack site.  
What happens / what should happen: ConnectAsync(host, port) tries AAAA first; it hangs past `Wait(5000)`, and the tool says "couldn't connect" or "no TLS answer". 1.1.4's IPv4-only TcpClient worked. Should: try each address with a 2-3 s timeout (Happy Eyeballs style), or retry over IPv4 and say "IPv6 didn't connect".
Confidence: suspected (read it; depends on Windows address ordering)  

### 28. Round-1 #56 and #154 partly fixed: an IPv6-only network is shown red, "You're not connected to any network"
Severity: wrong result  
Where: src\Monitor.cs line 726 (Judge, `offline = gwAddr == "" && !vpn`); RefreshInterfaces (lines ~291-320, IPv4 gateways only)  
How to reproduce: a Wi-Fi or Ethernet network that gives only IPv6 (NAT64/DNS64 hotspot, IPv6-only enterprise SSID).  
What happens / what should happen: no IPv4 gateway, so the headline goes red ("Wi-Fi is off, the cable is out, or airplane mode is on"), ISP/DNS/internet are greyed and DNS probes are skipped (line 250), while browsing works. Should: count an IPv6 default gateway, or an Up adapter with a global IPv6 address and a route, as connected.
Confidence: suspected (read it)  

### 29. "Why is my game lagging?" blames your home network when the router doesn't answer pings or the router check is off
Severity: wrong result  
Where: src\Checks\Guided.cs lines 109 and 126 (Lag, `lvHome = double.IsNaN(gA) ? 2 : …`)  
How to reproduce: Guided.Lag with NetPing 30 × 20 ms, no loss, and GwPing empty (Settings router check off, `Cfg.GwSec == 0`) or all NaN (router drops ICMP).  
What happens / what should happen: "Verdict: Your home network - the lag starts before the router" (on Wi-Fi: "move closer"), "lost 30 of 30", although the internet leg is clean. Should: if the internet answers, the home leg can't be the cause; treat no gateway data (or `!GwEverReplied`) as unknown. The same for lvIsp when NetPing is empty because of Policy.NoInternetCheck ("Your provider").
Confidence: confirmed (ran Guided.Lag on a synthetic Monitor, both scenarios)  

### 30. Windows Update break blames an update installed after the problem started; with daily Defender updates one fault matches four install days
Severity: wrong result  
Where: src\Checks\UpdateBreak.cs lines 58-70 (Compare), line 94 (choice of `worst`), line 114 (Background, the Overview line)  
How to reproduce: History where ping goes from 20 to 60 ms on day D and stays; installs on D-3 … D+3 (Defender definitions arrive as event 19 daily).  
What happens / what should happen: installs on D-2, D-1, D and D+1 all give Lv 2, Strong, "things got clearly worse starting the day after". For D+1 the "before" window still has 2 good days. All four tie, so the newest, which post-dates the problem, is blamed in the Verdict and on the Overview, typically "KB2267602 (Defender definitions)". Should: require the day before to be good and the day after bad; ignore or separate Defender definitions; when neighbouring days all match, blame the earliest or say "it started around D".
Confidence: confirmed (ran Compare by reflection with synthetic days)  

### 31. With the engineer view open (or during a Teams call) every preset sends 90 requests a minute, and About says "every 1 min"
Severity: wrong result  
Where: src\Monitor.cs line 219 (`netSec = … fast ? 2 : Cfg.NetSec`, `fast = ViewOpen || CallLikely`), lines 236-250 (ping + ProbeDns with BYPASS_CACHE); src\Ui\FullView.Settings.cs about line 372-376 (prints `every(Cfg.NetSec)`); src\Program.cs lines 5-7  
How to reproduce: choose Penny, open the engineer view at About › "What leaves your PC" ("One ping to 1.1.1.1 … every 1 min"), and count packets.  
What happens / what should happen: the gateway, 1.1.1.1 and a cache-bypassing DNS query for www.microsoft.com each go every 2 s: 90 requests/min, 11× Penny's figure, at exactly the moment About is being read. A Teams call with the view closed gives 72/min on Balanced. Should: run DNS at most every 10-15 s, let Penny cap the view-open pace (5 s), and have About and the header state the view-open and call rates.
Confidence: suspected (read it; the rates follow directly from the code)  
Rule: 1 (disclosure accuracy), 7  

### 32. Check yourself: "right the first time" isn't enforced - a wrong pick shows the answer and a restart pays +2 for it
Severity: wrong result  
Where: src\Ui\FullView.Learn.cs lines 106-135 (PaintCheck; `checkPick` is in memory only)  
How to reproduce: pick a wrong answer ("Not quite - the answer is: X (you can try again next time AdeeboMeter starts)"), restart, pick X.  
What happens / what should happen: +2 each time; across 46 topics about 90 points (~16 % of the maximum) with no learning. The option order is fixed. hall-of-fame.html and the Points page say "+2 for answering right the first time". Should: save a 0-point `check.<id>` "tried" marker on a wrong first answer, or change the wording.
Confidence: suspected (read it; the WinForms paint path can't run under mono)  
Rule: 10  

### 33. Round-1 #73 partly fixed: the replay +3 is paid without waiting 10 minutes if you restart and move the clock
Severity: wrong result  
Where: src\Points.cs lines 107-111 (Award; `firstMono` is per session)  
How to reproduce: run MTR, quit, set the clock forward 11 minutes, start the app, run MTR again.  
What happens / what should happen: +3. In one session the steady clock is checked correctly; after a restart only the wall clock is. At most 5 tools × 3 = 15 points. Should: store a boot-relative tick or session id with the entry, or state on the page that this can be cheated (low priority).
Confidence: confirmed (ran it: new Points instance, wall +11 min, steady +1 min → 3)  
Rule: 10  

### 34. Round-1 #74 partly fixed: "Use a page" still pays +2 for one click on empty space
Severity: wrong result  
Where: src\Ui\FullView.Points.cs lines 201-211 (WatchUse)  
How to reproduce: open each page and click once on empty space or the scrollbar.  
What happens / what should happen: any MouseDown or KeyDown on the page earns +2 per page. Should: require an action on the page (a tool run, a toggle, an Act).
Confidence: suspected (read it)  
Rule: 10  

### 35. Two Wi-Fi adapters: the monitor mixes their netsh blocks - state from the last block, signal from the first
Severity: wrong result  
Where: src\Monitor.cs lines 413-428 (ParseWifi), called from ProbeWifi at line 441 (LinkInfo.ActiveBlock exists at LinkInfo.cs:414 but isn't used here)  
How to reproduce: English netsh output with a connected built-in adapter followed by a disconnected USB dongle.  
What happens / what should happen: `state=disconnected` while connected, so "Wi-Fi dropped/reconnected" is never logged. With both connected, signal comes from A and SSID/BSSID/channel from B, which logs false roams and channel changes. Should: run ActiveBlock before ParseWifi in ProbeWifi too.
Confidence: confirmed (ran ParseWifi on the sample: disconnected; with ActiveBlock first: connected)  

### 36. Round-1 #65 partly fixed: on Turkish Windows the Wi-Fi signal is never read ("Sinyal : %74")
Severity: wrong result  
Where: src\Monitor.cs line 420 (ParseWifi, `^(\d{1,3})\s*%$`); src\Link\LinkInfo.cs around line 493  
How to reproduce: feed `Sinyal : %74` (Turkish puts % first) to Monitor.ParseWifi.  
What happens / what should happen: sig = -1 while SSID, channel and BSSID are read. Since "unknown signal = no verdict", Turkish users never get a weak-Wi-Fi verdict, signal-drop event or Teams-on-weak-Wi-Fi hint; the widget's Wi-Fi is always "unknown" and `wifi_signal` in --report is null. Should: also accept `%\s*(\d{1,3})`.
Confidence: confirmed (ran ParseWifi on a Turkish sample; the sample is written from knowledge of Turkish netsh, not captured on a Turkish PC)  

### 37. Round-1 #65 partly fixed: Japanese, Russian, Turkish and Chinese netsh state and channel lines aren't read, so drops are never logged; French BSSID label likely missed
Severity: wrong result  
Where: src\Monitor.cs line 405 (StateKeys, ChanKeys), line 443 (empty answer rule), NormState  
How to reproduce: ParseWifi on ja, ru and tr samples; on Polish "Kanał".  
What happens / what should happen: `state=` and `ch=` empty for ja and ru, `state=` for tr; a real disconnect then looks like an empty netsh answer and nothing is logged. NormState returns the raw text for 接続されました, подключено, bağlandı. French labels the BSSID "BSSID du point d'accès", which the exact match `k == "BSSID" || k == "AP BSSID"` misses, so roams aren't detected. Should: take state and BSSID from WlanQueryInterface (language-independent), or add the labels plus a "contains BSSID" fallback.
Confidence: confirmed for ja, ru and tr (ran ParseWifi); French label suspected (sample from memory)  

### 38. Round-1 #142 partly fixed: Neighbour wars ignores channel width on 5 GHz - "nobody else is on channel 44" beside a strong 80 MHz neighbour
Severity: wrong result  
Where: src\Link\LinkInfo.cs line 458 (Overlaps: `a == b` on 5 GHz); src\Link\AirLog.cs lines 77 and 83 (recorded Width unused)  
How to reproduce: 7 days of your AP on ch 44 / 80 MHz at -60 dBm, a neighbour on ch 36 / 80 MHz (covers 36-48) at -45 dBm.  
What happens / what should happen: "You were the strongest on your channel 7 of 7 days - nobody else is on channel 44"; quiet channel = 40, inside the same 80 MHz block. Should: work out each AP's occupied block from channel and width and treat overlapping blocks as shared.
Confidence: confirmed (ran AirLog analysis by reflection)  

### 39. Neighbour wars: after your router changes channel, the verdict uses the old channel; a mesh AP is merged into "yours"
Severity: wrong result  
Where: src\Link\AirLog.cs line 60 (MyChannel = most-hours channel), lines 78 and 84  
How to reproduce: your AP on ch 6 for 5 days and ch 11 for the last 2 (auto channel), a neighbour on 6. Or a stronger same-SSID mesh AP on another channel.  
What happens / what should happen: "strongest on your channel 5 of 7 days, and 1 neighbour now shares channel 6", though you left 6; the 2 days on 11 count as lost. With mesh: "strongest 0 of 7 days - nobody else is on channel 6", which contradicts itself. Should: use the newest channel for "now", compare each day against that day's channel, and key "mine" by BSSID.
Confidence: confirmed (ran it, scenarios B and C)  

### 40. IPv4 inputs with a leading zero are read as octal, and "0x0A" as hex (VLSM and number converter)
Severity: wrong result  
Where: src\Checks\Calc.cs line 115 (Vlsm) and line 220 (ParseNum), both IPAddress.TryParse  
How to reproduce: VLSM block `010.0.0.0/8`; `0x0A.0.0.0/8`; number converter `010.1.1.1`.  
What happens / what should happen: `010.0.0.0/8` is planned as 8.0.0.0/8; `0x0A…` is accepted as 10.0.0.0; `010.1.1.1` becomes 8.1.1.1. Users paste zero-padded addresses from Cisco configs. Should: reject leading zeros or read them as decimal.
Confidence: confirmed (ran it on mono; .NET Framework's inet_aton-style parse is expected to match)  

### 41. Bandwidth: a thousands separator is read as a decimal point (1,000 GB becomes 1 GB)
Severity: wrong result  
Where: src\Checks\Calc.cs lines 170-171 (Num regex, Parse2)  
How to reproduce: Size `1,500 MB` at `100 Mbps`; `1,000 GB` at `1 Gbps`.  
What happens / what should happen: "Time 120 ms" (should be 2 min); "8 s" (should be 2.2 h). Packet stack and the number converter strip `,` as thousands, so the calculators disagree. Should: treat `,` followed by exactly 3 digits as thousands, or follow the culture.
Confidence: confirmed (ran it)  

### 42. Number converter: decimal numbers of only 0s and 1s, 9+ digits long, are silently read as binary
Severity: wrong result  
Where: src\Checks\Calc.cs line 225 (ParseNum, `^[01]{9,64}$` before decimal)  
How to reproduce: `100000000`, `1000000000`, `101010101`.  
What happens / what should happen: 256, 512, 341. "Read as: binary" is shown but easy to miss. Should: binary only with `0b` or a `b` suffix, or show both readings.
Confidence: confirmed (ran it)  

### 43. VLSM: a 1-host subnet becomes a /32 whose "gateway" is outside it and is the next subnet's address
Severity: wrong result  
Where: src\Checks\Calc.cs lines 126 and 134 (Vlsm)  
How to reproduce: `10.0.0.0/24` with `1, 1, 1`.  
What happens / what should happen: "Subnet 1 10.0.0.0/32 · gateway 10.0.0.1", "Subnet 2 10.0.0.1/32 · gateway 10.0.0.2"… Should: size 1 host as /30 (like the 2-host rule on line 127), or show no gateway for /32 and /31. A /31 or /32 block is rejected outright, while /32 subnets are handed out inside larger blocks.
Confidence: confirmed (ran it)  

### 44. Packet stack: TLS record overhead is taken off every packet, so the "web" MSS reads 1,438 instead of 1,460
Severity: wrong result  
Where: src\Checks\Calc.cs line 30 and lines 84-92 (Stack; `tls` is kind "p")  
How to reproduce: Packet stack, layers `web`.  
What happens / what should happen: "Biggest segment (MSS) ~1,438 bytes - What TCP should advertise", efficiency 93.5 %. TLS's 22 bytes are per record (up to 16 KB), as the layer's own note says; MSS is 1460. Advising a 1,438 clamp is wrong. Should: exclude TLS from MSS/MTU arithmetic (QUIC is correct as it is).
Confidence: confirmed (ran it)  

### 45. Loss lab: one lost ping out of 20 tells you to quote "at most 2 Mbps" to your ISP
Severity: wrong result  
Where: src\Ui\FullView.Labs.cs lines 290 and 307 (LabMathis, "YOUR LINE RIGHT NOW")  
How to reproduce: one ICMP probe lost in the last 20 (`M.Loss.Avg(20)` = 5 %) at 30 ms.  
What happens / what should happen: MathisMbps(30, 5) = 2.12 Mbps, with "quote this in the ISP report if it's below your plan". 20 probes give 5 % resolution, ICMP drops aren't TCP loss, and Mathis is a Reno bound while Windows uses CUBIC. Should: require a minimum sample and loss count, label it a rough Reno bound, and don't advise quoting it.
Confidence: confirmed for the arithmetic (ran MathisMbps); the UI path read  

### 46. "What changed when it broke?": a bare number is read as days in the future
Severity: wrong result  
Where: src\Checks\Picks.cs line 63 (ParseWhen, TimeSpan branch)  
How to reproduce: When = `9`, `99`, `yesterday 9`, `3.5`.  
What happens / what should happen: `9` → today + 9 days; `99` → 2027-02-19; `yesterday 9` → today + 8 days; `3.5` → 5 March. The timeline is gathered around a future date. Should: refuse a TimeSpan with Days > 0 or read a bare 1-2 digit number as an hour, and apply the DateTime branch's "not in the future" rule. ("25:00" is rejected, see the Muse section.)
Confidence: confirmed (ran ParseWhen)  

### 47. "Your real download": two TCP connections in a capture give a window of about 685,000 packets
Severity: wrong result  
Where: src\Labs\LabSim.cs lines 153-160 (CwndFromCapture; `hiSeq`/`hiAck` global); src\Ui\FullView.Journey.cs lines 47-48 (JFlowFromStory sets LocalPort = 0)  
How to reproduce: a capture with two flows with different ISNs, which happens whenever LocalPort = 0 and the BPF is "host X and port 443".  
What happens / what should happen: the per-RTT estimate jumps from 2 to 684,887 packets and is drawn clamped as "your real download (estimated)". LastRealCwnd is static and never cleared, so a capture with no handshake keeps the previous line. The link is offered for "web" captures too. Should: key on the 4-tuple and use the busiest connection, or refuse when there's more than one.
Confidence: confirmed for the computation (ran it); the UI trigger suspected  

### 48. Jitter-buffer lab: the model makes calls unusable at ordinary jitter, and the challenge works in only 2 of 168 settings
Severity: wrong result  
Where: src\Labs\LabSim.cs lines 87-100 (Arrivals, Jitter); src\Ui\FullView.Labs.cs lines 166-172 (LabGoalMet "jitter")  
How to reproduce: run LabSim.Jitter over every stepper combination with jitter 30, 50, 80.  
What happens / what should happen: every late packet counts as lost, and jitter is an exponential with that mean; the 60 ms default buffer at 30 ms jitter scores 1.73, 80 ms jitter caps at 1.96 even with 200 ms buffer. "With 30 ms of jitter or more, reach 4.0" works only at jitter 30, buffer 200, delay 10 or 30, loss 0. This teaches that calls fail at 30 ms jitter. Should: use RFC 3550-style mean deviation, count late packets with concealment, and check the challenge is reachable.
Confidence: confirmed (ran it, MOS table)  

### 49. NAT lab: an ISP's private first hop is reported as "Double NAT", and failed traces award the challenge
Severity: wrong result  
Where: src\Ui\FullView.Labs.cs lines 437-441 (CGNAT check), line 175 (challenge met when `labCg != ""`)  
How to reproduce: trace from a line whose provider's first router uses RFC 1918 addresses; or a trace where your router doesn't send time-exceeded.  
What happens / what should happen: "Double NAT … put the modem in bridge mode" from a router address, not proof of NAT; silent hops are removed before `Skip(1)`, so the provider's hop is skipped as "the router". "Couldn't trace" and "No hop answered" also award +2. Should: say "possibly", compare the gateway's WAN address with STUN/speed-test, keep hop positions, and require a real result for the challenge.
Confidence: suspected (read it)  

### 50. Hall of Fame: three submissions in a row - the pending middle one is silently cancelled
Severity: wrong result  
Where: .github\workflows\hall-of-fame.yml lines 20-22 (`concurrency: group: hall-of-fame, cancel-in-progress: false`)  
How to reproduce: open three hall-of-fame issues (or edits) within one run's time.  
What happens / what should happen: GitHub keeps one pending run per group and cancels the older pending one, so issue #2 gets no comment, label or entry. Repeated edits of one issue can starve others. Should: push with rebase-and-retry and no concurrency group, or add a scheduled sweep of open issues without a bot comment.
Confidence: suspected (GitHub-documented behaviour; Actions not run)  

### 51. Windows Update break: plugging in a USB stick counts as a "driver change"
Severity: wrong result  
Where: src\Checks\UpdateBreak.cs line 29 (`EventID=20001` with no provider or status filter)  
How to reproduce: plug in a new USB device or phone (UserPnp event 20001, including failed installs).  
What happens / what should happen: it becomes a "driver" change in the heatmap dots and can be blamed under #30 with "Roll Back Driver". Should: filter on Provider and status 0, and only Net/Bluetooth/USB-controller classes.
Confidence: suspected (read it)  

### 52. Round-1 #1 partly fixed: iperf3 UDP with `-l 1M` reports about 0 Mb/s as a success
Severity: wrong result  
Where: src\Checks\Toolkit.cs line 59 (BlockLen), line 87 (ParseOpts), lines 255-289 (stream thread)  
How to reproduce: iperf3 `-u -l 1M` (or `-l 70000`).  
What happens / what should happen: BlockLen = 1,000,000, over the 65,507-byte UDP maximum; Send throws SocketException(MessageSize), the thread's `catch (SocketException) {}` swallows it, and the run reports ~0 Mb/s as fine. The `-l 4` crash is fixed. Should: clamp to 65,507 (or the path MTU) and report a send error.
Confidence: confirmed (ran ParseOpts/BlockLen on the a0fbd56 build)  

### 53. Round-1 #77 partly fixed: "In your own words" still routes common phrases to the wrong guide
Severity: wrong result  
Where: src\Ui\FullView.Overview.cs lines 121-157  
How to reproduce: type "my work laptop is slow", "the network at work is slow", "youtube buffering at work", "streaming works but games lag", "I can't get to work email".  
What happens / what should happen: the first three open the VPN guide (the "at work" rule ranks above slow/buffering); the fourth opens "slow" (stream before game); the last matches nothing. All six round-1 phrases are fixed. Should: rank symptom words above place words.
Confidence: confirmed (ran the matcher)  

### 54. Round-1 #124 partly fixed: DestCheck.Parse accepts `1.2.3`, `0x7f.1`, `4294967295` and `01.02.03.04` as host names
Severity: wrong result  
Where: src\Checks\DestCheck.cs line 74  
How to reproduce: Destination check with any of those values.  
What happens / what should happen: they skip the new strict IPv4 regex by falling into the host-name branch, and Windows' resolver then reads them as numeric IPs (1.2.0.3, 127.0.0.1…), so the tool tests a different address than it shows. Should: reject all-numeric/hex dotted names, or normalise them and show the real address.
Confidence: confirmed (ran DestCheck.Parse)  

### 55. Live capture on an older Wireshark: the first Start ends with "the adapter went away" instead of restarting without the extra fields
Severity: wrong result  
Where: src\Capture\LiveCapture.cs line 172 (Reader, `else if (p == ts)`), stderr handler around lines 106-110 (Restart(gid))  
How to reproduce: a tshark that rejects one of the extra fields; press Start live capture.  
What happens / what should happen: the restart waits 300 ms; meanwhile the Reader sees EOF with `p == ts`, sets "Capture stopped by itself - the adapter went away or the PC slept" and queues Stop, whose `gen++` drops the restart. A second press works. Should: mark the session as restarting before calling Restart and skip the self-stop branch.
Confidence: suspected (read it; a timing race)  

### 56. Round-1 #232 still present: the TCP:443 fallback for the internet check never comes back after one ICMP reply
Severity: wrong result  
Where: src\Monitor.cs lines 241-242 (NetIcmpEver)  
How to reproduce: start where ping to 1.1.1.1 works, then block ICMP while TCP 443 still works.  
What happens / what should happen: unchanged: "The internet went quiet" while the web works, until a network change or restart. Should: re-arm the fallback after N ICMP failures.
Confidence: suspected (read it)  

### 57. Round-1 #231 still present: History, game history and outages ignore portable mode
Severity: wrong result  
Where: src\History.cs line 17  
How to reproduce: run a portable copy next to an installed one.  
What happens / what should happen: unchanged; the portable copy shares the installed copy's history (Store, AirLog and inventory do respect portable mode). Should: use DataDir.
Confidence: suspected (read it)  

### 58. Round-1 #43 still present: version is still 1.1.4
Severity: wrong result  
Where: src\Program.cs lines 113-114  
How to reproduce: `--report`, About, or a Store package built from this branch.  
What happens / what should happen: everything says 1.1.4 (1.1.4.0 for the Store). Known release-time bump; listed so it isn't forgotten.
Confidence: suspected (read it)  

### 59. Round-1 #233 still present: CI never builds the dev branch
Severity: wrong result  
Where: .github\workflows\build.yml line 9 (`[main, store]`)  
How to reproduce: push to dev-1.1.5.  
What happens / what should happen: no build or self-test runs. Should: add the dev branch, or `dev-*`.
Confidence: suspected (read it)  

### 60. Round-1 #234 still present: "printer" in Toolbox search opens Ping sweep
Severity: wrong result  
Where: src\Ui\FullView.Toolbox.cs line 518 (`"printer", "sweep snmpprn arp discover"`); src\Ui\FullView.DeepTest.cs line 149 (`"printers:sweep"`)  
How to reproduce: type "printer" + Enter.  
What happens / what should happen: Ping sweep (a scanner behind the Lab tick) opens instead of the printer tool. Should: change both lines.
Confidence: suspected (read it; Muse agrees)  

### 61. Round-1 #54 partly fixed: IP to MAC still says "not in its ARP table" when the 2,000-entry walk was cut
Severity: wrong result  
Where: src\Checks\SnmpGear.cs lines 75-77  
How to reproduce: IP to MAC on a router with more than 2,000 ARP entries, for an address past the cut.  
What happens / what should happen: "this device doesn't have it in its ARP table". Find a MAC is fixed (direct GET). Should: GET ipNetToMediaPhysAddress for that IP directly, or say the table was cut.
Confidence: suspected (read it)  

### 62. Round-1 #101 partly fixed: What's wrong still trusts ring samples that are minutes old after sleep
Severity: wrong result  
Where: src\Checks\Investigate.Whats.cs lines 49-51  
How to reproduce: resume from sleep (or a slow NetSec) and run What's wrong at once.  
What happens / what should happen: it re-tests only when the newest sample is NaN; samples taken before sleep are averaged as current. Should: timestamp samples and re-test when the newest is older than a few seconds.
Confidence: suspected (read it)  

### 63. Round-1 #134 partly fixed: SNMP Interfaces shows "+N" after a device restart when sysUpTime can't be read
Severity: wrong result  
Where: src\Checks\SnmpTools.cs lines 99 and 136  
How to reproduce: a device that restarts between two Interfaces runs and doesn't return sysUpTime (upt = 0).  
What happens / what should happen: counter deltas are shown as "+N" across the restart. Renumbering is fixed. Should: treat a counter going backwards as a reset when uptime is unknown.
Confidence: suspected (read it)  

### 64. Uninstall helper matches Single.Others(), so "close the stuck one" kills it and a reinstall waits on it
Severity: wrong result  
Where: src\Program.cs line 451 (Others: `ProcessName.StartsWith("AdeeboMeter")`), used by KillOthers and Setup.StopOthers  
How to reproduce: Uninstall (leaves `%TEMP%\AdeeboMeter-remove.exe` waiting), then start AdeeboMeter and answer Yes to "Close the stuck one", or reinstall at once.  
What happens / what should happen: the helper is killed, so the install folder is never removed; StopOthers waits its full 4 s. RunningIsOlder already excludes "-remove". Should: exclude it in Others() too.
Confidence: suspected (read it)  

### 65. Round-1 #238 still present: "Same certificate" names the issuer instead of the subject
Severity: wrong result  
Where: src\Checks\EngPack.cs line 83 (`a.Chain[0][1]`)  
How to reproduce: Certificate compare on two hosts that serve the same certificate.  
What happens / what should happen: unchanged; the issuer's name is printed. Should: print the leaf's subject.
Confidence: suspected (read it)  

### 66. Round-1 #239 still present: DSCP compare can't take `100`, and the headline ignores loss in the unmarked bucket
Severity: wrong result  
Where: src\Checks\Lab.cs (dscp regex and headline)  
How to reproduce: DSCP compare with `100`; or loss only in the unmarked bucket.  
What happens / what should happen: unchanged (Muse agrees). Should: accept 3-digit counts and include both buckets in the headline.
Confidence: suspected (read it)  

### 67. Round-1 #241 still present: 802.1X replay judges only the first 200 frames
Severity: wrong result  
Where: src\Checks\Replay.cs line 211  
How to reproduce: a pcap with a failed EAP exchange after frame 200.  
What happens / what should happen: unchanged; the failure is never seen. Should: filter EAPOL frames first, then cap.
Confidence: suspected (read it)  

### 68. Recipients list fails on a corrupt `.cer` in the recipients folder (from Muse, not re-run)
Severity: wrong result  
Where: src\Checks\CertKeys.cs line 308 (listing loop catches only CryptographicException around Load)  
How to reproduce: put a `.cer` with bad base64 text in the recipients folder and open the recipients tool.  
What happens / what should happen: Load calls Convert.FromBase64String, which throws FormatException; the Toolbox runner catches it as "Couldn't finish", so the whole list is lost (not an app crash). The add path (line 302) already catches FormatException. Should: catch the same set in the listing.
Confidence: suspected (read it; lead re-read line 308)  

### 69. Certificate "Live" check ignores the TLS handshake result (from Muse, not re-run)
Severity: wrong result  
Where: src\Checks\CertKeys.cs line 257 (`hs.Wait(8000)` result ignored), line 260 (catch swallows failures)  
How to reproduce: Live check against a host whose TLS handshake hangs past 8 s or fails.  
What happens / what should happen: the tool goes on to "no certificate came back" instead of "handshake timed out" or the real error. Should: check the Wait result and report the handshake exception.
Confidence: suspected (read it; lead re-read line 257)  

### 70. Guided Lag: subtracting home loss from ISP loss can hide real ISP loss when both happen together (from Muse, not re-run)
Severity: wrong result  
Where: src\Checks\Guided.cs (Lag, ISP-loss subtraction)  
How to reproduce: router and internet probes lose the same samples during an ISP fault.  
What happens / what should happen: the ISP leg can read as clean. Should: subtract only when the router loss explains the internet loss sample by sample.
Confidence: suspected (Muse's guess on correlation)  

### 71. Round-1 #95 residual: MySites enumerates Cfg.Pins on a worker thread without the lock
Severity: wrong result  
Where: src\Checks\MoreTools.cs line 205 (MySites)  
How to reproduce: run My sites while pinning or unpinning a site.  
What happens / what should happen: "Collection was modified" from the worker; the runner shows "Couldn't finish". Should: copy the list under the same lock Watch uses.
Confidence: suspected (read it)  

### 72. Packet stack: the UDP fragment count is one too high at the boundary
Severity: wrong result  
Where: src\Checks\Calc.cs line 103 (Stack)  
How to reproduce: `udp ipv4 ethernet`, payload `2952`.  
What happens / what should happen: "IP splits it into 3 fragments"; should be 2 (ceil((2952 + 8) / 1480)).
Confidence: confirmed (ran it)  

### 73. Hall of Fame names lose letters on the card, and C# and Python clean names differently
Severity: wrong result  
Where: src\HallOfFame.cs line 43 (`char.IsLetterOrDigit` per UTF-16 unit); .github\scripts\hof_verify.py line 31 (per code point)  
How to reproduce: names `𠮷田`, `𝐀𝐝𝐞𝐞𝐛 Khan`, `देवनागरी`, `José` in NFD.  
What happens / what should happen: C# gives `田`, `Khan`, `दवनगर`, `Jose`; Python keeps the astral letters, so a typed (not pasted) name gives "mismatch". Mn/Mc marks are dropped on both sides. Should: NFC, keep Mn/Mc/Nd, iterate by code point in C#, and add an astral-name test.
Confidence: confirmed (ran both on the same inputs)  

### 74. Website and comments say Points are off by default; the code has them on from day 1
Severity: wrong result  
Where: docs\hall-of-fame.html line 114; src\Points.cs line 15; src\Program.cs line 316 (`Points = true`)  
How to reproduce: read the page, then start a fresh install.  
What happens / what should happen: "off until you turn them on" vs on by default. Make them match.
Confidence: confirmed (read the files)  

### 75. Round-1 #29 partly fixed: build.yml still says "anyone can check the download matches the code", which official builds with HOF_KEY can't meet
Severity: wrong result  
Where: .github\workflows\build.yml lines 1-2 and 35-36  
How to reproduce: rebuild from the tagged source and compare hashes.  
What happens / what should happen: official builds compile in obj/HofKey.cs, so a rebuild never matches. The provenance attestation still holds; the Store bundle still isn't attested. Should: say so and publish the hash of the build without the key, or explain the difference.
Confidence: confirmed (read the files)  

### 76. Round-1 #207 partly fixed: index.html still says online lookups send "public IP addresses only"
Severity: wrong result  
Where: docs\index.html lines 833 and 974  
How to reproduce: read the page against About › "What leaves your PC".  
What happens / what should happen: it omits the Security checkup's DoH/TLS checks and other sends. README is fixed. Make the page match About.
Confidence: confirmed (read the file)  

### 77. Round-1 #244 partly fixed: privacy.html still lists none of what's kept (game list, keys and recipients, Points, outage log, air.txt)
Severity: wrong result  
Where: docs\privacy.html  
How to reproduce: compare the page's "what it keeps" with the data folder.  
What happens / what should happen: those files aren't mentioned (Muse agrees for Points and the outage log; air.txt is #10). Add them.
Confidence: confirmed (read the file)  

### 78. Hall of Fame workflow: missing labels and a failed push leave the issue with no answer
Severity: wrong result  
Where: .github\workflows\hall-of-fame.yml lines 47-69  
How to reproduce: run with no "not verified" label in the repo; or push to main during a run so `git push` fails.  
What happens / what should happen: `gh issue edit --add-label "not verified"` fails (cosmetic, the comment is posted first); without a `hall-of-fame` label the workflow never runs. A failed push skips "Answer" by the implicit `success()`: no comment, no entry, no retry. Should: create labels, use `if: always()` on the answer step and retry the push.
Confidence: suspected (read it)  

### 79. Journey: switching story while a capture runs orphans the capture (from Muse, not re-run)
Severity: wrong result  
Where: src\Ui\FullView.Journey.cs (story switch during capture)  
How to reproduce: start a journey capture, then pick another story.  
What happens / what should happen: the first dumpcap keeps running until its own 1,000 packets / 30 s limit and leaves one file; its Process object is never disposed. Should: stop the running capture on switch.
Confidence: suspected (read by Muse)  

### 80. Match finder: Busiest can't tell a game server from a peer (from Muse, not re-run)
Severity: wrong result  
Where: src\Gamer (MatchFinder.Busiest)  
How to reproduce: a peer-to-peer game session with one busy peer.  
What happens / what should happen: the busiest peer is reported as "the match server". Should: label it "busiest connection" when it can't be told apart.
Confidence: suspected (read by Muse)  

### 81. (withdrawn) RADIUS lab IPv6 receive endpoint
Severity: none  
Where: src\Checks\Protocols.cs line 386, src\Checks\EngPack.cs line 287  
What happens / what should happen: not a bug. Both sites call `UdpClient.Receive(ref from)`, and .NET Framework's UdpClient.Receive ignores the endpoint passed in and builds one of the socket's own family. Kept as a numbered entry so later numbers don't move.  
Confidence: withdrawn by the lead (reference-source reading; not runnable here, the container has no IPv6)  

### 82. Points: small gaps in load and save (from Muse, not re-run)
Severity: wrong result  
Where: src\Points.cs (TrySave, Load, Me, Entry.Pts); src\SelfTest.cs  
How to reproduce: read.  
What happens / what should happen: TrySave/Load don't catch SecurityException (the Report writer does); `Points.Me` lazy init has no lock; `Entry.Pts` is written and read but Total never uses it; Load reads the whole file with no size bound (relevant to #3's 20,000-line files); the comment says the self-test checks every NoEarn tool, but SelfTest.cs has no NoEarn reference.
Confidence: suspected (read by Muse)  

### 83. Muse's carried-over lows (from Muse, not re-run)
Severity: wrong result  
Where: various (Muse's morning list)  
How to reproduce: per item, in Muse's report.  
What happens / what should happen: MTR thread-join race; replayBmp stale on empty data; UdpMtu.Connect outside the try; Lang/Examples load throws; "Mark lag" null-session race; GameCompare empty run; ISP report same-minute overwrite; Store manifest en-us only; game presets have no checked-on date; settings migration only within the same WriteDir; VpnAware adapter loop and registry reads unguarded (the Toolbox runner catches); Policy Load never resets (untriggerable today).
Confidence: suspected (read by Muse)  

## Slow

### 84. Widget card on a Busy or Problem level is fully re-rendered every second all day, because of a decorative countdown
Severity: slow  
Where: src\AppCtx.cs lines 353-360 (OnUi); src\Ui\Theme.cs lines 412-437 (Draw.Card, `s.Countdown + "s"`); src\Ui\Controls.cs lines 43-60 (LayeredForm.Present)  
How to reproduce: memory above 80 % or CPU above 60 % (level 1 "Busy"), widget uncovered, mouse away.  
What happens / what should happen: the full card shows at 30 % alpha and each 1 s snapshot differs (countdown, CPU, RAM, Mb/s), so Render runs every second: 2.17 ms / 14.4 KB managed on mono, a new 271 KB bitmap (610 KB at 150 %), a GetHbitmap copy, UpdateLayeredWindow and SetWindowPos. That's ~86,400 composites a day. Should: redraw the unhovered card only when verdict, level or headline change (or every 5-10 s), drop the countdown when unhovered, reuse one DIB section.
Confidence: confirmed (render cost measured, 200 renders; the condition read)  
Rule: 7  

### 85. The widget timer runs at 4 Hz forever, including hidden, locked or display off, in every preset
Severity: slow  
Where: src\AppCtx.cs lines 146, 318, 323, 326 (OnUi)  
How to reproduce: tray › Hide widget, or Win+L, or choose Penny; count ticks.  
What happens / what should happen: 250 ms idle, 50 ms near the corner, 500 ms hidden (120 wakeups/min doing nothing). Nothing reacts to lock; the EnumWindows cover check (2.5 Hz) and the 30 s redraw continue on the lock screen. Each tick calls Screen.AllScreens 3-4 times and builds a ~500 B string. Should: stop when hidden, pause on lock and display-off, 1 Hz when the mouse is far, and use WinEvent hooks instead of polling.
Confidence: suspected (read it; per-tick allocation measured)  
Rule: 7  

### 86. Penny leaves most all-day costs at full rate (4 % fewer wakeups than Balanced)
Severity: slow  
Where: src\Program.cs lines 325-330 (ApplyPreset); src\Brain\Brain.cs line 65  
How to reproduce: compare the cost table's Penny and Balanced columns.  
What happens / what should happen: Penny only slows the gateway/internet/DNS probes, netsh, process sampling, call watching and Brain PC jobs. Widget timer, cover check, card redraw, sampler, Gamer loop, single-instance wait, Gamer probes and survey, interfaces/ARP/C: every 10 s, view-open pace, AirLog, UpdateBreak, History writes and Devices are untouched: ≈ 434 vs 453 wakeups/min. Should: apply the Penny recommendation under the cost table (about 70-80 wakeups/min, ~3 packets/min).
Confidence: confirmed (read and counted)  
Rule: 7  

### 87. Wi-Fi watch starts a netsh.exe process every 30 s by default (every 10 s in Engineer or with the view open)
Severity: slow  
Where: src\Monitor.cs lines 258 and 437 (ProbeWifi → Job.Run(Job.Netsh, …, 3000))  
How to reproduce: Balanced on Wi-Fi; watch process starts.  
What happens / what should happen: 2,880 starts/day on Balanced, 8,640 on Engineer, 720 on Penny, each with a console host and a Defender scan; a slow netsh blocks the probe thread up to 3 s. Should: use WlanQueryInterface / WlanGetNetworkBssList (already in LinkInfo.cs:186), which also removes the language parsing of #36/#37; keep netsh as fallback.
Confidence: suspected (Process.Start path read; cost not measured on Windows)  
Rule: 7  

### 88. The engineer view repaints the path strip, side panel and footer every second with identical data
Severity: slow  
Where: src\Ui\FullView.cs line 511 (`pathPanel.Invalidate(); side.Invalidate();` per snapshot), line 515 (footer every 2nd tick)  
How to reproduce: open the view on overview, conn or toolbox with nothing changing.  
What happens / what should happen: in 20 s: pathPanel 21, side 21, footer 20 paints on every page (the page canvases correctly don't repaint). Should: key each panel on what it prints, as OverviewTick does.
Confidence: confirmed (ran it: Paint counts per control, 3 pages × 20 s)  
Rule: 7  

### 89. The Gamer loop and the single-instance listener each wake once a second forever with nothing to do
Severity: slow  
Where: src\Gamer\Gamer.cs line 390 (`Thread.Sleep(1000)` with no game); src\AppCtx.cs line 163 (`WaitAny(both, 1000)` only to re-check `quitting`)  
How to reproduce: fresh install, no game, any preset.  
What happens / what should happen: 120 idle wakeups/min, a quarter of all idle wakeups. Should: the Gamer thread waits on an event set by Games.SetMonitored; the single-instance thread adds a quit event and waits without timeout.
Confidence: suspected (read it)  
Rule: 7  

### 90. Gamer Zone's background survey isn't scaled by any preset and can send more than all of Penny's checks
Severity: slow  
Where: src\Gamer\Gamer.cs lines 349-366 (Plan: GameBgSec, SurveySec = 300), line 395, line 397 (GetProcesses every 5 s)  
How to reproduce: add three games with 12 regions each; choose Penny.  
What happens / what should happen: ~12.6 requests/min (Penny's own checks are 8), a DNS lookup per host-name probe, and a full process snapshot every 5 s although the probe thread already has `M.PidNames`. Should: stretch GameBgSec and SurveySec on Penny, reuse PidNames, pause the survey when idle or locked.
Confidence: suspected (read it)  
Rule: 7  

### 91. Hotel check reads the whole captive-portal body before cutting it to 200 characters (from Muse; lead re-read)
Severity: slow  
Where: src\Checks\Picks.cs line 130 (`string body = sr.ReadToEnd();` then Substring(0, 200))  
How to reproduce: a portal page that streams without end.  
What happens / what should happen: memory grows until the 5-minute ReadWriteTimeout, and Stop can't interrupt a blocked ReadToEnd. Same class as the HTTP 64 KB cap fixed in 1.1.4. Should: read at most ~1 KB.
Confidence: suspected (read it; lead re-read line 130)  
Rule: 7  

### 92. Round-1 #172 still present: a 63 MB pcap still takes about 175 MB of managed memory
Severity: slow  
Where: src\Checks\Pcap.cs line 37 (ReadAllBytes) and Wanted() filter  
How to reproduce: the round-1 63 MB capture of 300-byte DHCP-like frames.  
What happens / what should happen: the new filter passes them, so 184,526 packets and +120 MB on top of the 63 MB array. The filter only helps for non-DHCP traffic. Should: stream the file and keep only what the replay needs.
Confidence: confirmed (ran it)  
Rule: 7  

### 93. Round-1 #179 partly fixed: opening a 200 MB .p7m still reads it all into memory several times
Severity: slow  
Where: src\Checks\FileCrypt.cs line 25 (MaxCmsOpen 200 MB), Decode(ReadAllBytes)  
How to reproduce: open a 200 MB .p7m.  
What happens / what should happen: the file, the decoded message and the content are held at once; OutOfMemoryException isn't handled specially. Making is now capped at 50 MB. Should: cap opening at the same 50 MB, or stream.
Confidence: suspected (read it)  
Rule: 7  

### 94. `.adeebo` decrypt has no upper size cap (from Muse, not re-run)
Severity: slow  
Where: src\Checks\FileCrypt.cs (Open: only `ctLen >= 16`)  
How to reproduce: a crafted multi-GB .adeebo header.  
What happens / what should happen: it streams to a similar-size hidden temp (and the MAC fails at the end); encrypt is capped at 4 GB. Should: refuse a ctLen above the encrypt cap or the free disk space.
Confidence: suspected (read by Muse)  

### 95. Hover-card render reads and waits on files on the UI thread (Usage lock, GameHistory.Week)
Severity: slow  
Where: src\Ui\Theme.cs line 476 (Usage.TodayDU), line 481 (Usage.Month), lines 300-312 (GamerCard → GameHistory.Week); src\Ui\FullView.Gamer.cs line 251; src\Brain\Brain.cs line 104  
How to reproduce: hover the widget while Usage.Save runs (every 5 min), or as the first hover after start.  
What happens / what should happen: the UI thread blocks on Usage's lock during a file write, does the first `Store.Read("usage.txt")`, and may read up to 7 game-history files (8 MB each) when the 10-minute cache expires. Should: publish totals in the Snap and refresh GameHistory on the Gamer thread only.
Confidence: suspected (read it)  
Rule: 7  

### 96. TCP lab Play keeps repainting the Learn pane after you leave the lab, and is frozen when you return
Severity: slow  
Where: src\Ui\FullView.Labs.cs line 115 (LabPlay tick), line 235 (a SolidBrush per round)  
How to reproduce: TCP lab, Length 480, Play, then click another Learn topic.  
What happens / what should happen: the tick checks only `learnPage.Visible`, so for up to ~32 s it invalidates learnInfo 15×/s on another topic; each frame re-runs both simulations and allocates up to 480 brushes. Leaving the Learn page stops the timer but keeps `labShown`, so the graph is frozen with only "Show all". Labs and Journey ignore Cfg.Preset (grep). Should: stop when `learnSel` isn't the TCP lab, and use one brush per phase colour.
Confidence: suspected (read it; the Preset point confirmed by grep)  
Rule: 7  

### 97. UpdateBreak.Background runs every hour whatever History says
Severity: slow  
Where: src\Brain\Brain.cs line 108 (`if (t.Minute == 20) UpdateBreak.Background();`)  
How to reproduce: turn History off; it still runs every hour.  
What happens / what should happen: two System-log queries over 14 days plus History day reads every hour, in every preset; it only sets ChangeDays and UpdateBreak.Strong. Local only, so not a rule 1 break, but AirLog and Devices are History-gated and this isn't. Should: gate on History and run daily on Penny.
Confidence: suspected (read it; lead re-read line 108)  
Rule: 7  

### 98. The folded chip is fully re-rendered every 30 s even when nothing changed
Severity: slow  
Where: src\AppCtx.cs line 321 (`drawnKey = ""` every 30 s)  
How to reproduce: leave the widget folded.  
What happens / what should happen: 2,880 redundant chip renders a day just to reclaim TOPMOST. Should: call SetWindowPos alone, and only when the window is no longer top-most.
Confidence: suspected (read it)  
Rule: 7  

### 99. With the view open, RefreshIpNames can start `ipconfig /displaydns` every 30 s on any page
Severity: slow  
Where: src\Monitor.cs lines 604-640 (RefreshIpNames); src\Ui\FullView.cs RefreshConns (every 15 s)  
How to reproduce: a Windows build where DnsGetCacheDataTable returns an empty table; keep the view open.  
What happens / what should happen: ipconfig.exe every 30 s plus up to 3,000 cache-only DnsQuery calls every 15 s, even when Connections isn't shown. Should: refresh only on Connections/Travel/What's running, and back off after two empty results.
Confidence: suspected (read it; depends on the Windows build)  
Rule: 7  

### 100. Redundant system-wide enumerations: four process snapshots, two EnumWindows loops, three ARP reads
Severity: slow  
Where: Monitor.cs:566, Gamer.cs:228, PcHealth IoTick/LeakTick (processes); AppCtx.cs:389, PcHealth.HungTick (EnumWindows); Monitor.WatchGatewayMac, Brain.LearnNetwork, RoutingTools.GwTick (ARP)  
How to reproduce: read.  
What happens / what should happen: each job takes its own snapshot; ProbeProcesses throws and catches an exception for each protected process. Should: share one snapshot per tick, remember refused PIDs, or use one NtQuerySystemInformation call.
Confidence: suspected (read it)  
Rule: 7  

### 101. BuildSnap builds a 30 KB snapshot every second whether or not anything shows it; Measure allocates a 1.3 KB key per call
Severity: slow  
Where: src\Monitor.cs lines 792-850 (BuildSnap); src\Ui\Theme.cs lines 234-243 (Draw.Measure key)  
How to reproduce: harness `snap` (2,000 calls) and `measure`.  
What happens / what should happen: 0.079 ms and 30.1 KB per snapshot (2.6 GB/day of gen-0 garbage), Judge 17.2 KB every 10 s, 1,352 B per Measure cache hit, a cache entry per live number until the 3,000 clear. Bounded, so no 64 MB threat. Should: build hover Details lazily, skip top-N sorts when Apps hasn't changed, key Measure with a struct and don't cache strings with digits.
Confidence: confirmed (measured)  
Rule: 7  

### 102. Round-1 #178 partly fixed: the stream overlay still PNG-encodes every second and makes two Fonts per paint
Severity: slow  
Where: src\Ui\StreamOverlay.cs lines 46, 74, 108-112  
How to reproduce: open the stream overlay with "Write a PNG" on.  
What happens / what should happen: the PNG is written only on change, but it is painted and encoded every second just to compare bytes; each paint creates and disposes two Fonts and calls GamePing.Best. The "last 60 s" label, opaque PNG, no owner window and untranslated strings remain from round 1. Should: keep the fonts as fields and key the repaint on (value, timestamp, title).
Confidence: suspected (read it)  
Rule: 7  

### 103. Probe thread runs RefreshInterfaces, an ARP read and DriveInfo("C") every 10 s in every preset
Severity: slow  
Where: src\Monitor.cs line 257  
How to reproduce: read.  
What happens / what should happen: GetAdaptersAddresses over all adapters, route pick, ARP and free-space every 10 s, although NetworkAddressChanged already wakes the loop. Should: 10 s only with the view open; otherwise 60 s or event-driven with a 5-min safety refresh.
Confidence: suspected (read it)  
Rule: 7  

### 104. Round-1 #176 partly fixed: Sniff captures are capped at 200 MB each, but ring copies and the merged file multiply it, with no free-space check
Severity: slow  
Where: src\Ui\FullView.cs (Sniff), src\Capture\LiveCapture.cs (`-a filesize:200000`)  
How to reproduce: a long ring capture on a busy link.  
What happens / what should happen: ring files plus the merged copy can be several × 200 MB. Should: cap the total and check free space first.
Confidence: suspected (read it)  

### 105. Round-1 #187 partly fixed: SNMPv3 still runs the 1 MB password stretch for every message
Severity: slow  
Where: src\Checks\Snmp.cs lines 325-330 (Localize)  
How to reproduce: any v3 walk.  
What happens / what should happen: the key is re-derived per message. Should: cache per engine ID.
Confidence: suspected (read it)  

### 106. Round-1 #39 partly fixed: hung reverse-lookup tasks are still abandoned after 3 s, not cancelled
Severity: slow  
Where: src\Native.cs line 94 (PtrName) and its callers' Wait(3000)  
How to reproduce: a DNS server that never answers PTR queries.  
What happens / what should happen: each lookup leaves a blocked thread-pool thread. The extra forward query is fixed. Should: use DnsQueryEx with a cancel handle, or a single worker queue.
Confidence: suspected (read it)  

### 107. History compaction: one damaged `.csv.gz` stops the nightly Tidy for every later day and leaves a `.gz.tmp`
Severity: slow  
Where: src\History.cs line 73 (Tidy)  
How to reproduce: truncate a day's `.csv.gz` while a late `.csv` for that day exists.  
What happens / what should happen: InvalidDataException ends the loop; later days stay uncompressed until the bad day ages out. Should: per-file try/catch, keep the bad `.gz`, delete the tmp.
Confidence: suspected (read it)  

### 108. Round-1 #240 still present: game-ping TCP fallback leaks wait handles
Severity: slow  
Where: src\Gamer\Gamer.cs lines 478-490 (TcpMs)  
How to reproduce: game probes that fall back to TCP for hours.  
What happens / what should happen: unchanged; one wait handle per probe left to the finalizer. Should: dispose the IAsyncResult's handle.
Confidence: suspected (read it)  
Rule: 7  

### 109. Round-1 #150 partly fixed: dumpcap/tshark are started and then added to the Job, so a child can escape
Severity: slow  
Where: src\Capture\LiveCapture.cs line 101  
How to reproduce: crash the app in the moment between Process.Start and Job.Add.  
What happens / what should happen: Stop2 now kills child dumpcap by PID, but the start is still not suspended, so a child can outlive the app. Should: start suspended or with the job inherited.
Confidence: suspected (read it)  

### 110. Small undisposed objects (from Muse; the arrow caps confirmed by our perf pass)
Severity: slow  
Where: src\Checks\Picks.cs (hotel: WebException.Response, `new X509Certificate2(seen)`); src\Ui\FullView.Journey.cs, FullView.Story.cs:90, FullView.Travel.cs:125 and 249 (`new AdjustableArrowCap` per paint); UdpMtu (`new Random()` per packet)  
How to reproduce: read.  
What happens / what should happen: each is left to the finalizer; none is all-day. Should: dispose them, keep one Random.
Confidence: suspected (read it)  

## Cosmetic

### 111. Full-screen lab window: changes made in the main window's lab stop repainting
Severity: cosmetic  
Where: src\Ui\FullView.Labs.cs line 41 (LabRefresh: only labHost is invalidated)  
How to reproduce: open a lab, Full screen, go back to the main window, press − / + or a Choice there.  
What happens / what should happen: the value changes but the main window looks dead; a TCP Play started there freezes. Should: invalidate the control that owns the clicked Act, or both.
Confidence: suspected (read it)  

### 112. Packet journey: "In plain words" and the timeline play head freeze while the replay plays
Severity: cosmetic  
Where: src\Ui\FullView.Journey.cs line 253 (JStep invalidates only jLane), lines 258-265  
How to reproduce: capture a conversation, press Play.  
What happens / what should happen: only the lane strip repaints; the running translation jumps only on a full paint. Should: also invalidate the translation block and the timeline bar each step.
Confidence: suspected (read it; no capture driver under mono)  

### 113. TCP lab: with a tiny link the window can't go below 2, so every round trip shows a loss next to "100 % of the link"
Severity: cosmetic  
Where: src\Labs\LabSim.cs lines 23, 37 and 43  
How to reproduce: Link 1 Mbps, Round trip 5 ms, Buffer 0.1× BDP.  
What happens / what should happen: 480 losses in 480 rounds; cwnd sits at 2 above cap 1.1; "Throughput 100%" beside "Losses 480". Should: let the window fall to 1 or say the BDP is below one packet.
Confidence: confirmed (ran it)  

### 114. TCP lab: the window is capped at 100,000 packets with no label, and a timeout is drawn without its wait
Severity: cosmetic  
Where: src\Labs\LabSim.cs lines 59 and 45  
How to reproduce: 1000 Mbps, 300 ms, buffer 8×.  
What happens / what should happen: max cwnd 100,000.0 exactly, flat, zero losses; a hard loss restarts slow start on the next round trip although RTO is at least 1 s (300 ms on Windows). Should: label the cap as a receive window and idle for the RTO.
Confidence: confirmed (ran it, the cap); RTO point from RFC 6298  

### 115. Bandwidth: some valid inputs give an empty result; common units rejected
Severity: cosmetic  
Where: src\Checks\Calc.cs lines 154-167 (Bandwidth)  
How to reproduce: `4.7 GB` with Speed `0` or Time `0`; `1 bps`; sub-ms times; `Mbit/s`, `MiB/s`.  
What happens / what should happen: blank result; "= 0 bytes/s"; "0 ms"; "type e.g. 100 Mbps". 310+ digits throw OverflowException (caught as "Couldn't finish"). Should: a "must be above 0" row, more decimals for small values, TryParse, accept the units.
Confidence: confirmed (ran it)  

### 116. QoS lab: unsupported claim that Teams and Zoom mark audio as EF
Severity: cosmetic  
Where: src\Ui\FullView.Labs.cs line 544  
How to reproduce: read the QoS lab.  
What happens / what should happen: Zoom's default audio marking is 56 (video 34); Teams marks only when QoS is enabled by policy. Suggest "Teams (when QoS is turned on) marks audio EF (46)".
Confidence: suspected (vendor documentation, from memory)  

### 117. A long name on the award card runs under the star and off the edge
Severity: cosmetic  
Where: src\HallOfFame.cs line 127 (Fit stops at 36 px)  
How to reproduce: name of 32 × "W".  
What happens / what should happen: drawn across the star and clipped at x = 1200. Should: keep shrinking, wrap or ellipsize.
Confidence: confirmed (rendered HallOfFame.Card under mono)  

### 118. Policy `history_days` not among the Settings choices leaves no pill selected, and locks the row even when the user's value is lower
Severity: cosmetic  
Where: src\Policy.cs lines 65 and 78; src\Ui\FullView.Settings.cs lines 42 and 133 (`Array.IndexOf` = -1)  
How to reproduce: `history_days = 5`; or `history_days = 30` with the user on 7.  
What happens / what should happen: nothing highlighted; or locked though the policy doesn't constrain. Rule 6 holds. Should: show the policy value and lock only choices above it.
Confidence: suspected (read it)  

### 119. Round-1 #148 partly fixed: "Least crowded channel" on 5 GHz offers only 36-48 and 149-165, which much of the EU and Japan can't use
Severity: cosmetic  
Where: src\Link\LinkInfo.cs line 243 (WlanApi.LeastCrowded); src\Link\WifiAdvisor.cs line 95  
How to reproduce: 5 GHz on channel 100 in a busy EU building.  
What happens / what should happen: "set it to 149"; DFS 52-144 are never offered, and 165 is 20 MHz-only. Should: recommend among channels already seen around, or mention DFS carefully.
Confidence: suspected (read it)  

### 120. `--report` with no file name exits 3 ("couldn't tell") instead of 4
Severity: cosmetic  
Where: src\Program.cs line 141  
How to reproduce: `AdeeboMeter.exe --report`.  
What happens / what should happen: exit 3; Report.cs defines 4 as "the report couldn't be made". Return 4.
Confidence: suspected (read it)  

### 121. Round-1 #190 partly fixed: French still has English on the new screens
Severity: cosmetic  
Where: src\Ui\FullView.Points.cs (8 literals + 3 buttons via `L.T(variable)`); src\Ui\FullView.Link.cs lines 250, 279, 287; src\Ui\Controls.cs line 375; section tabs; src\Ui\FullView.Settings.cs line 101  
How to reproduce: Settings › Language › French, open Points › Award, Neighbour wars, a Journey.  
What happens / what should happen: the Award flow, Neighbour wars header and legend, the "Journey" button and the section tabs are English; "What did Windows Update break?" is the one untranslated title of 141. The Settings sentence now says tool names stay English, which understates. (Agrees with Muse.)
Confidence: confirmed (read lang-fr.txt and the literals)  

### 122. Round-1 #191 still present: French text is clipped in fixed-size rectangles
Severity: cosmetic  
Where: src\Ui\FullView.Overview.cs (Fix tiles); src\Gamer\Gamer.cs (120*S / 100*S boxes)  
How to reproduce: French, Overview tiles and Gamer Zone.  
What happens / what should happen: unchanged; not touched by any fix commit.
Confidence: suspected (read it)  

### 123. Round-1 #111 still present: system-aware DPI only
Severity: cosmetic  
Where: src\Program.cs line 130 (SetProcessDPIAware)  
How to reproduce: mixed-DPI monitors.  
What happens / what should happen: unchanged; blurry widget and wrong cover detection on the second monitor. Should: per-monitor-v2 manifest.
Confidence: suspected (read it)  

### 124. Labs never say "simulation" (from Muse, not re-run)
Severity: cosmetic  
Where: src\Ui\FullView.Labs.cs  
How to reproduce: open the TCP lab on defaults.  
What happens / what should happen: the curve can be read as your own download; only "your real download (estimated)" is labelled. One sentence on each graph fixes it.
Confidence: suspected (read by Muse)  

### 125. The website documents none of the 1.1.5 features (from Muse, not re-run)
Severity: cosmetic  
Where: docs\index.html  
How to reproduce: search the page for Gamer Zone, French, encryption, calculator, Points, Neighbour, Award, journey, Labs.  
What happens / what should happen: 0 mentions; versions are fixed.
Confidence: suspected (Muse's grep)  

### 126. Round-1 #213 partly fixed: tool counts disagree (141 registered; README and CHANGELOG say 140; THEORY says 129)
Severity: cosmetic  
Where: README.md line 32, CHANGELOG.md line 7, docs\THEORY line 235  
How to reproduce: count `T(` in Tools2().  
What happens / what should happen: `winupdate` (670f9a3) made it 141. Update the three.
Confidence: confirmed (counted; agrees with Muse)  

### 127. Round-1 #85 residual: a stopped upload says "Nothing else was sent"
Severity: cosmetic  
Where: src\Checks\SpeedTest.cs line 196  
How to reproduce: Stop the speed test during upload.  
What happens / what should happen: part of the upload was sent. Say "the upload was stopped after N MB".
Confidence: suspected (read it)  

### 128. Round-1 #45 residual: `https:/example.com` (one slash) becomes host "https"
Severity: cosmetic  
Where: src\Checks\Toolkit.cs lines 33-49 (HostOnly/UrlToTarget)  
How to reproduce: type `https:/example.com` in Ping.  
What happens / what should happen: pings "https". Accept a single slash or say the URL looks wrong.
Confidence: confirmed (ran UrlToTarget)  

### 129. Round-1 #193 partly fixed: the live-capture "after N min" label is built once
Severity: cosmetic  
Where: src\Ui\FullView.cs (live capture label)  
How to reproduce: change the capture length in Settings with the view open.  
What happens / what should happen: the label keeps the old value. Build it on paint.
Confidence: suspected (read it)  

### 130. Round-1 #55, #163, #165 and #199 partly fixed: accessibility and widget leftovers
Severity: cosmetic  
Where: src\Ui\Controls.cs; src\Ui\FullView.cs; src\AppCtx.cs line 100  
How to reproduce: Narrator on painted pages; an auto-hide taskbar; Windows text scaling.  
What happens / what should happen: painted values, verdicts and path dots have no accessible objects (most acts read "item N of M"); badges ("3 new", "REC") aren't exposed; disabled Primary contrast unchanged; nothing handles an auto-hide taskbar over the widget; text scaling ignored; the widget is hover-only.
Confidence: suspected (read it)  

### 131. Round-1 #217, #220, #222 and #223 partly fixed: website leftovers
Severity: cosmetic  
Where: docs\ (robots.txt), docs\index.html lines 879 and 1068, mobile nav  
How to reproduce: read / open the site at 800 px.  
What happens / what should happen: robots.txt still missing; "opens in under 1 second" (self-test allows 2 s); no `<track>` captions on the video; mobile nav hidden under 860 px.
Confidence: confirmed (read the files)  

### 132. Comment and UI-text drift (from Muse, not re-run)
Severity: cosmetic  
Where: src\Program.cs lines 11-12; src\Ui\FullView.Toolbox.cs line 179; ISP report; Settings; Points page  
How to reproduce: read.  
What happens / what should happen: Program.cs says usage/inventory live in %LOCALAPPDATA% (portable puts them in data\); "three sections" (now five); ISP "Save as PDF" writes HTML; Donate only in Settings; the Points table has no Copy/Save.
Confidence: suspected (read by Muse)  

## Where Muse's round 2 is wrong or understated

- **"Calc (VLSM bounds) verified correct": wrong.** A 600-case random diff against Python `ipaddress` found 11 wrong plans, all from one bug (#22, lead re-read Calc.cs:129: `at` is advanced before the fit check). There is also the /32 gateway bug (#43) and octal/hex parsing (#40).
- **"LabSim verified correct": right for the core formulas only.** Reno and CUBIC (β, K, W_est, ssthresh on dup-ACK and timeout) all match. Wrong around them: the jitter lab model (#48), the capture window estimate (#47), the tiny-link floor (#113), the unlabelled cap and missing RTO wait (#114).
- **Hall of Fame "honest limits, not bugs": understated.** Forging needs no key and no reverse engineering: junk lines in points.txt make the official button sign a genuine card (#3; lead re-read Points.Value returning 2 for any unknown id and Total summing every id). `Key` and `Code` are public, so the mask doesn't matter either (#4). The verifier then accepts 99999 points and repeat names with no moderation (#5). This should be High, not an honest limit.
- **"Verifier reproduces the C# vectors":** only for names that are already clean; astral letters and NFD differ (#73). "Dedups by code" is true, but the same name with different points gets a second entry (#5).
- **#26 "SecondDhcp still bypasses the gate": wrong.** SecChecks.cs:413 returns early with `if (Lab.Company(m)) return …` before `Lab.DhcpOptions(m, "yes")`, so the hard-coded "yes" is reached only when the Lab would pass without the tick anyway (lead re-read).
- **#235 "Outage notification still half-built … no notification call anywhere": wrong.** Brain\Watch.cs:40 raises "inetback" ("Internet back … Every outage is listed in Time machine"), and line 39 already raises "went quiet" (lead re-read). Muse looked for a call in Outages.cs.
- **"UpdateBreak … notifies on ≥3 update-related breaks": wrong.** There is no Alerts.Raise in UpdateBreak.cs; Background() only sets ChangeDays (heatmap dots) and UpdateBreak.Strong (an Overview line). The ungated hourly run is real (#97).
- **"Windows Update break is honest": the wording is, the matching isn't.** It blames an install that post-dates the problem and picks Defender definitions (#30), and USB plug-ins count as driver changes (#51).
- **"Neighbour wars … 7 days, History-gated": writing is gated, keeping isn't.** With History off, air.txt is never pruned and stays indefinitely (#10). The verdict logic Muse called correct ignores channel width and channel changes (#38, #39).
- **ParseWhen "25:00 is tomorrow 01:00": the example is wrong.** The regex branch rejects "25:00" (run). The real bug is bare numbers: "9" is 9 days in the future (#46).
- **"The review-fix commits genuinely fixed most… what's left is mostly things the fix commits never touched": understated.** The fixes introduced 7 new bugs and 2 regressions that Muse doesn't mention: the MTR rule regression (#21), outage pairing (#25), ReplaceFile deleting before the move (#24, while Muse calls the new Points save "atomic"), the Quit-under-modal fix that isn't one (#1), the unmarshalled SessionEnding (#2), the SNMP 32-bit regression (#26) and the IPv4-mapped bypass of the new guards (#7).
- **Not covered by Muse at all:** `--report` exit 3 on every healthy run (#23), dig/DNS compare turning DoH into plaintext (#8), the dual-mode Tcp IPv6-first regression (#27), Wi-Fi parsing on two adapters and non-Latin locales (#35-#37), the VLSM/calculator input bugs (#40-#44), the cost picture (#84-#90, view-open pace 11× the stated rate in #31).
- **Muse's Low "jTimer/labTimer never disposed":** true but harmless; both stop on the first tick after the page hides and are collected with the view. The undisposed AdjustableArrowCaps are real but paint-time only (#110).

## Muse's 200-bug list: the 7 Highs checked

Muse also delivered a list of 200 new bugs (`muse-200-bugs.md` in this folder). Not all 200 were verified. The lead checked the 7 it rates High; verdicts below. A separate check of Muse's Mediums will be added later as `round-2/muse-200-check.md`.

| # | Muse's High | Verdict | Evidence |
|---|---|---|---|
| 1 | IPv6 UDP receive throws (Snmp.cs:113 and other sites) | **Wrong** for .NET Framework 4.8 | Every site is `UdpClient.Receive(ref ep)`. .NET Framework's UdpClient.Receive ignores the endpoint passed in: it builds Any/IPv6Any of the socket's own family and overwrites remoteEP. Suspected-wrong (reference-source reading); not runnable here, the container has no IPv6 sockets. This also covers our #81 (from Muse), which is likely not a bug for the same reason. |
| 2 | NetBIOS offsets wrong (Reach.cs:175, `r[56]`) | **Wrong**, the code is right | In an NBSTAT reply: header 12 + RR_NAME 34 + TYPE 2 + CLASS 2 + TTL 4 + RDLENGTH 2 = 56, so NUM_NAMES is at 56 and names start at 57 (RFC 1002 4.2.18). |
| 3 | A bad server line in games.txt wipes the built-in servers (Gamer.cs:114) | **Confirmed** by reading, but Medium | Servers is reset on line 114 before the line is validated on 115. Needs a hand-edited file. |
| 4 | Spike context never printed (Gamer.cs:601, `Around(z.T, 0)`) | **Confirmed**, Low/Medium | Around (line 583) needs \|Δt\| ≤ 0 s between two different DateTime.Now stamps, so it never matches. Context is lost from the report (cosmetic data loss). |
| 5 | Culture crash with native digits (Paint.cs:91 and others) | **Wrong** | .NET's Int32.ToString(culture) never emits native digits. Run under mono with ar-SA, fa-IR, hi-IN and ar-EG: all give "2", and the InvariantCulture parse succeeds. Inconsistent style, no crash. |
| 6 | Self-test LabToolsMissingGate is vacuous (SelfTest.cs:906) | **Confirmed**, already reported | Part of round-1 #229 and our #6 ("the self-test only scans lab sections"). |
| 7 | Destination check's CompareDns sends the typed name to 1.1.1.1 and 8.8.8.8 with no Online/NoOnline check (DestCheck.cs:193, 206, 494) | **Confirmed** by grep, but Medium (privacy/docs), not High | It runs inside a tool the user started, so rule 1 holds. It contradicts Online.cs's "never the names you type" and the policy's `online_lookups` switch. |

Net: 3 of the 7 Highs are wrong (1, 2, 5), 1 duplicates a known item (6), and the 3 real ones (3, 4, 7) are Medium or lower.

## Checked and held up

- **Fixes that held when re-run (selection):** hostile mail server (endless 250- lines 91 ms / 17 MB; dripped STARTTLS stops at 8 s); corrupt points.txt ticks skipped (MaxValue+1, long.MaxValue, -5, NaN, empty id); malformed keys-ec.txt and crafted key backups refused; policy combining (strictest wins, `mode=pro` ignored, history takes the minimum, user's own values kept); speed `check` respects `speed_test=off`; .adeebo one-pass decrypt (bit flips every 997 bytes: 0 accepted; truncation and padding flips give the MAC error); decrypted names (bidi, `CON .txt`, `COM¹`, `CONIN$`); pcapng ns timestamps; unsupported link types named; ICMP inner IHL<5; hash compare on pasted formats; subnet bad prefixes; scoped IPv6 ports; IDN as punycode; Startup list with illegal path chars; M365.Role; destination-only loss in MTR; URL targets in ping/trace; speed grade with NaN; match server ≠ own public IP; rule 8 grep (0 hits).
- **TCP sim vs RFC 5681 / RFC 9438:** IW10, slow start to ssthresh, Reno halves and CUBIC keeps 0.7 on dup-ACKs, timeout to cwnd 1 with the right ssthresh, CUBIC C = 0.4, β = 0.7, K and α as specified, first CA step matches by hand (482.6). The "240 round trips" claim holds at the defaults.
- **Calculators apart from the listed bugs:** subnetting quiz (all 65,536 seeds: right answer once, 4 distinct options); number converter (800 random values exact, 64-bit included, overflows and odd digits rejected); bandwidth (500 random cases within display rounding, bits/bytes and 1000/1024 right); packet stack header sizes (Ethernet, IPv4/6, TCP, UDP, PPPoE, WireGuard, VXLAN) and efficiency; VLSM alignment, no overlap, sort, limits; no calculator input crashes the app (Toolbox runner catches at FullView.Toolbox.cs:694).
- **Journey and labs:** Kind/Translate/Explain with 0 to 20,000 records, shuffled and negative timestamps (no exception, ≤ 35 ms); CwndFromCapture bin cap works; JStep silence skip stops cleanly; dumpcap is headers-only (`-s 128`, `-p`, caps, Strip()); other lab challenges are reachable and none is met on defaults; lab formulas (Mathis 1.22, airtime anomaly, 64 Hz, DSCP/ToS, VLAN range); 46 check questions well-formed.
- **Hall of Fame workflow and page:** no `${{ }}` reaches a `run:` line; inputs go through env vars; `hmac.compare_digest`; anchored regexes; the GITHUB_OUTPUT heredoc can't be broken (no newline after clean_name); no pull_request_target; job writes only docs/hall-of-fame.json; the secret is masked; 80-bit codes make brute force impractical; date bounds handle UTC+14. hall-of-fame.html with a hostile JSON in Playwright (`<img onerror>`, `<script>`, `javascript:` and quote-break-out picture URLs, RTL override, 5000-char name, `"1e9"`, `-5`, an object; 1366 and 360 px): no script ran, no `<img>` was created, only a well-formed user-attachments link, no horizontal scroll. Defence-in-depth gaps only by editing the JSON directly (no name-length cap; negative or 1e9 points printed as is).
- **Points:** Award once per item; fast repeats, restarts and earning switched off give 0; NoEarn senders give 0; clock moved back then +11 min on the steady clock gives +3 correctly; old 3-field lines migrate.
- **AirLog, DevInfo, UpdateBreak plumbing:** hostile SSIDs can't inject rows; air.txt bounded and atomic; DEVPKEY GUIDs/PIDs right, sizes capped, tool threads only; event times UTC → local; localized titles handled; EvtNext 1 s timeout and 80/40 caps.
- **Native P/Invokes and core:** DNS_RECORD layout and free; WLAN struct strides; WlanGetProfile flags 0 (no plaintext key); GetBestInterface byte order; WINHTTP_PROXY_INFO with GlobalFree; DHCPCAPI stride 20/32 with cleanup; Clock.Mono used consistently; ParseWifi right on en/de/fr/es (signal, SSID, channel, state); FileCrypt temp only CreateNew in the same folder and renamed after the tag check; CleanCaptures only under DataDir\captures and only `*.pcap*`; Setup removes only the fixed base and keeps keys; VpnAware's 128.0.0.0/1 route check is right; SNMP Exchange's recomputed deadline.
- **Rules:** rule 1 - new outbound calls in changed code are user-run tools or listed background checks (PTR is now one query, not two), apart from #8 and #31; rule 2 - no TcpListener, HttpListener, Bind or UdpClient(port) in added lines; rule 4 - the decrypt Code box is a SecretInput, generated codes use CopySecret, IspReport redacts before HTML-escaping; rule 6 - Policy can't switch anything on; rule 8 - 0 hits.
- **Performance that's fine:** Labs/Journey/SlideSwitch timers stop when hidden; the engineer view closes on minimize and frees its data; Overview repaints only on change (0 page-canvas paints in 20 s); ConnList paints visible rows; owner-drawn controls double-buffered; every in-memory structure is bounded (Timeline, Watch.seen, Draw.measured, GameSession, PcHealth.snaps, AirLog…); no network I/O under `M.L`; no DNS or ping on the UI thread in all-day paths.
