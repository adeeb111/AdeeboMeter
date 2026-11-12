# AdeeboMeter 1.1.5 (8502674): performance and hang review, round 3

**Scope.** I re-checked everything that changed since round 2's commit a0fbd56: aec65f5 ("lighter Penny + Gamer preset"), b312fd1 (6-month history, games and over-time tools, Learn by goal) and 8502674 (Learn sections). The source is `/home/user/AdeeboMeter-dev`, read-only. I re-ran the round-2 measurements against the new build.

**What I ran.** The harness is `review3/harnessP3/P3.cs` (P3.exe). It is round 2's P.cs with new modes added. It loads `build3/r3.exe` by reflection under `xvfb-run mono`, from a portable copy in `harnessP3/app/` (with `AdeeboMeter.portable`, so all data goes to `app/data/`).

| Mode | What it measures |
|---|---|
| `jobs` | Each preset's real Cfg values, JobsPerMinute, MaxServers and SurveySec |
| `draw` | Chip, hovered card, unhovered card and detail card render cost |
| `snap` | BuildSnap and Judge cost |
| `measure` | The Measure cache |
| `view N page` | Paint counts per control, with identical data each second |
| `overlay N` | Stream overlay invalidations, counted from changes to its `lastKey` |
| `qr` | QR encode and render |
| `histgen 180 1440` | Writes 180 synthetic days in History.Minute's exact line layout (time, 14 numbers, network, disk ms, faults) plus 25 events a day into `app/data/history` |
| `tidy` | The real History.Tidy, which compresses and runs BeforeRollOff |
| `histtime` | The real loaders: History.Load, Week, Outages.Last, and OverTime.Month, RushHour, DownloadTime, WakeUp, Sum, SixMonths and Summary |
| `peak` | Peak heap and working set during SixMonths |
| `gamegen 3 11` | 14 days of `games-*.csv` for 3 games at the Gamer preset's line rate, then GameHistory.Week and SixMonths |
| `games` | Replays GamePing.Loop's scheduler against the real Plan() |

As in round 2, mono numbers show relative cost only. The Windows samplers can't run on Linux, so the rates below come from the code, checked against Cfg values that were read at run time.

---

## 1. Cost table at 8502674

Conditions are the same as round 2: the widget is on, the mouse is away from the corner, the PC is idle, it is on Wi-Fi, and no game is added unless a row says so. **P / B / E / G** are Penny / Balanced / Engineer / Gamer.

**The preset values, read at run time (`jobs`):**

| Preset | Gw | Net | Wi-Fi | Apps | Interfaces | GameBg | GameSrv | MaxServers | SurveySec |
|---|---|---|---|---|---|---|---|---|---|
| Penny | 30 s | 300 s | 120 s | 30 s | 60 s | 60 s | 3 | 8 | 1800 |
| Balanced | 2 | 15 | 10 | 5 | 10 | 30 | 3 | 8 | 300 |
| Engineer | 2 | 15 | 10 | 5 | 10 | 30 | 3 | 8 | 300 |
| Gamer | 2 | 15 | 10 | 5 | 10 | **10** | **5** | **16** | 300 |

At a0fbd56 Balanced was 5 / 30 / 30 / 10, so **Balanced and Engineer are now identical**. Settings shows "~8 checks a minute" for Penny and "~71" for the other three. Penny's 8 matches what Penny actually runs.

| # | Recurring job (where) | Interval P / B / E / G, view closed | View open | Work per tick | Stops when hidden / locked? | Wakeups/min P / B / E / G | Packets/min P / B / E / G | vs round 2 |
|---|---|---|---|---|---|---|---|---|
| 1 | Widget UI timer (AppCtx.cs:146, OnUi; interval at 351-355) | **1000 ms / 500 / 500 / 500**. 50 ms near the corner. **2000 ms when hidden or locked**. 500 ms when the view is up or you are presenting. | 500 ms | Same as round 2: WidgetScreen, Cursor.Position, a 496 B state key (measured), Presenting every 8th tick | Slowed, not stopped: 30/min when hidden or locked. Display-off is not handled. | **60 / 120 / 120 / 120** | 0 | **was 240 in every preset.** Penny -75 %, the others -50 %. |
| 1a | Cover check (AppCtx.cs:356) | gated at 400 ms, so now **every tick** | | EnumWindows plus 4 calls per visible window | as #1 | 60 / 120 / 120 / 120 (inside #1) | 0 | was ~120 in every preset; Penny now 60 |
| 1b | Widget render (AppCtx.cs:386-389) | Chip: on change only. **30 s top claim = SetWindowPos only** (Controls.cs ClaimTop). **Unhovered card: every 10 s, or when the level or headline changes.** The countdown is drawn only while hovered. | | Chip 0.28 ms. Card hovered 2.71 ms, unhovered 2.28 ms, 14 KB managed, 271 KB pixels + GetHbitmap copy (measured `draw`) | | chip ~0; **unhovered card 6** | 0 | **card 60 → 6; chip 2 → 0** |
| 2 | Sampler (Monitor.cs:119-153) | 1 s in every preset | 1 s | Unchanged. **BuildSnap 0.091 ms, 30.2 KB; Judge 0.107 ms, 17.3 KB** (measured) | No | **60 / 60 / 60 / 60** | 0 | unchanged |
| 2a | Brain PC jobs (Brain.cs:65-71) | k=3 on Penny, 1 otherwise | same | unchanged | No | ~7 / ~21 / ~21 / ~21 | 0 | unchanged |
| 2b | Per-minute flush (Brain.cs:100-109) | 1 min. AirLog hourly. **UpdateBreak: hourly, but once a day (12:20) on Penny, and only with History on** | same | unchanged otherwise | No | 1 | 0 | UpdateBreak now gated by History and by Penny |
| 3 | Probe thread (Monitor.cs:209-273) | pace = min(Gw, Net, Wi-Fi, Apps, IfEvery): **30 s / 2 / 2 / 2**. 1 s for up to 120 s after a resume (wake report). | 2 s | gw ping. Internet ping every Net (view or call: **min(5, Net)**). **DNS at most every 60 s (15 s with the view open)**. TCP 443 also after 3 ICMP misses in a row. | No | **2 / 30 / 30 / 30** | gw **2 / 30 / 30 / 30**; ping **0.2 / 4 / 4 / 4**; DNS **0.2 / 1 / 1 / 1**. Total **2.4 / 35 / 35 / 35**. **View open: 46 in every preset** (30 gw + 12 ping + 4 DNS). Call, view closed: **2.4 / 43 / 43 / 43**. | P 8 → **2.4**. **B 16 → 35 (+119 %)**. E 38 → 35. View open 90 → 46. Call: B 72 → 43, E 90 → 43. |
| 3a | **netsh wlan show interfaces** (Monitor.cs:457) | **120 s / 10 / 10 / 10** | 10 s | a new netsh.exe + conhost | No | process starts/min **0.5 / 6 / 6 / 6** | 0 | **B 2 → 6/min: 2,880 → 8,640 processes a day** (×2 with conhost). P and E unchanged. |
| 3b | Process + connection sampling (Monitor.cs:264-265) | **30 s / 5 / 5 / 5** | 4 s (2 s on What's running) | GetProcesses + OpenProcess/GetProcessTimes for each of ~250 processes. GetExtendedTcpTable. | No | 2+2 / 12+12 / 12+12 / 12+12 | 0 | B doubled (6 → 12 each) |
| 3c | Interfaces + ARP + C: + LearnIsp (Monitor.cs:262, IfEvery 405) | **60 s / 10 / 10 / 10** | 10 s | unchanged | No | 1 / 6 / 6 / 6 | 0 (ISP trace once per network, ~18 packets) | Penny 6 → 1 |
| 4 | Gamer loop (Gamer.cs:404) | **5 s with no game**, 1 s with any game | | GameFinder.Running = **Process.GetProcesses every 5 s** with any game (unchanged). Plan + Best → GameHistory.Week (10-min cache). | No | no game **12**; with a game 60 | One game with 11 regions: **P 1.4 / B 4.2 / E 4.2 / G 8.2**. Three games: **P 4.1 / B 12.6 / E 12.6 / G 24.6**. While playing: **8 servers/s (480) + path hops ≤ 144 = ≤ 624/min (P/B/E); G: 10/s (600) + 144 = ≤ 744** (findings 3 and 4) | no game 60 → 12. Penny's game background is ~3× lighter. Gamer is new and ~2× Balanced. Path-hop timing is new. |
| 5 | Single-instance wait (AppCtx.cs:163) | `WaitAny(..., 30000)` | | nothing | | **2** | 0 | 60 → 2 |
| 6 | Pinned watch (Watch.cs) | 30 s, opt-in | | unchanged | | | 2 if on | unchanged |
| 7 | Engineer view timer (FullView.cs:312, OnTimer2 511-528) | n/a | 500 ms | Path strip and side panel are now keyed. **Measured (`view` 20 s): pathPanel 0, side 0, footer 12-18 paints** (it prints its own CPU to 0.1 %). **gamerCanvas 20 / 20 s with games watched** (finding 2). RefreshIpNames now only on conn / travel / procs; ipconfig backs off to 5 min after two empty results. | Minimize closes it | 120 + footer ~50 | 0 | panel paints 180/min → ~50/min |
| 7a | Freeze watchdog | n/a | 1 s + BeginInvoke | unchanged | closes with the view | (60 + 60) | 0 | unchanged |
| 8 | Labs 66 ms, Journey 33 ms, SlideSwitch 15 ms, **Learn games 1 s (FullView.Games.cs:25)** | n/a | only while animating or a timed game is on screen | | **Yes**: the games timer stops when Learn is hidden, the window is hidden or the selection isn't a game. GameTick(false) once escaped, solved or over. | 0 idle (60 during a timed room or quick game) | 0 | games timer is new and fine |
| 9 | Stream overlay (StreamOverlay.cs:48-53) | 1 s tick | | **Repaint + PNG only when the key changes**. Fonts are fields. **Measured: 1 key change in 15 s (the first) with unchanged data.** | | 60 ticks, ~0 paints | 0 | **round 2's 60 paints/min → ~0** |
| 10 | Live capture limit | 30 s while capturing | | unchanged | | 2 | 0 | unchanged |
| 11 | **Match path recording** (Gamer.cs:330-345, 431; GameSession.TimeHops) | while a game session is open (GameFast is on by default, every preset) | | a 16-TTL trace (≤ 2 probes per hop), then one ping to each of ≤ 12 hops every 5 s (800 ms timeout each) | ends with the session | 12 | **≤ 144** | **new** |

### Totals, view closed, idle, no game

| | Penny | Balanced | Engineer | Gamer |
|---|---|---|---|---|
| Wakeups/min, 8502674 | **≈ 144** (2.4/s) = widget 60 + sampler 60 + PC jobs 7 + flush 1 + probe 2 + Gamer 12 + single 2 | **≈ 246** (4.1/s) = 120 + 60 + 21 + 1 + 30 + 12 + 2 | ≈ 246 | ≈ 246; **294 with a game added** |
| Wakeups/min, round 2 | 434 | 453 | 471 | n/a |
| Packets/min, 8502674 | **2.4** | **35** | 35 | 35 + games (8.2 per game) |
| Packets/min, round 2 | 8 | 16 | 38 | n/a |
| netsh starts/day, 8502674 | 720 | **8,640** | 8,640 | 8,640 |
| netsh starts/day, round 2 | 720 | 2,880 | 8,640 | n/a |
| Process snapshots/min (probe + GameFinder with a game) | 2 (+12) | 12 (+12) | 12 (+12) | 12 (+12) |

**View open, any preset:** about 470-490 wakeups/min plus ~50 footer paints/min (widget 120 + view timer 120 + watchdog 120 + sampler 60 + probe 30 + PC jobs + Gamer 12 + single 2), and 46 packets/min. In round 2 this was about 760-800 wakeups, 180 panel paints and 90 packets.

### The "lighter Penny" claim, against round 2's recommendation

| | Round 2 Penny | 8502674 Penny | Round 2 target | Met? |
|---|---|---|---|---|
| Wakeups/min, view closed | 434 | **144** | 70-80 | **No, about 2×.** What's left: the 1 Hz sampler (60, target 12-30), the widget (60), the Gamer idle loop (12, target 0) and PC jobs (7). |
| Packets/min, view closed | 8 | **2.4** | ~3 | **Yes** |
| Packets/min, view open | 90 | **46** | Penny capped at 5 s, about 12 + 12 + 1 | **No.** The gateway still goes to 2 s on Penny with the view open (Monitor.cs:220, `ViewOpen ? 2`), and DNS every 15 s. |
| Gateway / internet / DNS | 10 s / 60 s / 60 s | 30 s / 5 min / 5 min | 30 s / 5 min / ≥ 60 s | Yes |
| Wi-Fi | netsh 120 s | netsh 120 s | WlanApi in-process 120 s | Rate yes; mechanism no (finding 5) |
| Interfaces / ARP / C: | 10 s | **60 s** | 60 s | Yes |
| Widget idle | 250 ms | **1000 ms**, hidden/locked 2000 ms | 1 s, stopped when hidden or locked | Mostly |
| Unhovered card | 1 s | **10 s** | ≤ every 10 s | Yes |
| Game background / survey | 30 s / 5 min | **60 s / 30 min** | 120 s / 30 min or off | Mostly |
| UpdateBreak | hourly | **daily** | daily | Yes |
| AirLog | hourly | hourly | 3 h | No |
| Sampler, disk counters | 1 s, 3 counters | 1 s, 3 counters | 2-5 s, no disk counters | No |
| Gamer loop / single-instance | 1 Hz / 1 Hz | 0.2 Hz / 1 per 30 s | event-driven | Mostly |

**Verdict.** The packet goal is met: 8 → 2.4/min. Wakeups fell 67 %, 434 → 144/min, but are still about twice the target. The sampler alone is now 42 % of Penny's idle wakeups. The cost of the preset change fell on everyone else: **Balanced, the default, roughly doubled** its packets, its netsh starts and its process sampling (finding 5).

---

## 2. Round-2 findings: status at 8502674

| R2 # | Title | Status | Evidence |
|---|---|---|---|
| 1 | Unhovered card re-renders every second | **FIXED** | AppCtx.cs:386 `cardDue` allows a render only when hovered, on a level or headline change, or every ≥ 10 s. Theme.cs:438 draws the countdown only when `CardHovered`. The card still makes a new Bitmap + GetHbitmap per render (Controls.cs:43-60): no DIB reuse, but now 6/min instead of 60. Measured render: 2.28 ms unhovered, 2.71 ms hovered (2.17 ms in round 2). |
| 2 | Widget timer 4 Hz forever, even hidden or locked | **PARTIAL** | Idle is 500 ms (1000 ms on Penny). Hidden or locked is **2000 ms, not stopped** (AppCtx.cs:351): SessionLock is now handled, display-off is not. The cover check (EnumWindows) now runs on **every** tick, because the 400 ms gate is shorter than the tick (AppCtx.cs:356). Polling, not hooks. 240 → 120 (60 on Penny) wakeups/min. |
| 3 | Gamer loop and single-instance wait wake every second | **PARTIAL** | Gamer.cs:404 sleeps 5 s with no game (12/min). AppCtx.cs:163 uses a 30 s timeout (2/min). Not event-driven, but 120 → 14 wakeups/min. |
| 4 | Chip re-rendered every 30 s | **FIXED** | AppCtx.cs:349 calls `widget.ClaimTop()` (SetWindowPos with NOMOVE, NOSIZE, NOACTIVATE) and does not redraw. The SetWindowPos is unconditional, but that is negligible. |
| 5 | netsh.exe every 30 s / 10 s | **STILL PRESENT, worse for Balanced** | Monitor.cs:457 still uses Job.Run(netsh). WifiSec is now 10 s in Balanced: **8,640 processes a day for the default preset** (2,880 in round 2). Penny is unchanged at 720. |
| 6 | View-open pace 90/min; About under-reports | **PARTIAL** | View open is now 46/min: internet ping at most every 5 s, DNS every 15 s (Monitor.cs:220, 255). The gateway is still 2 s in every preset, Penny included. About still prints `every(Cfg.*)`: **Penny now reads as 2.4/min while 46/min are sent, 19× under (round 2: 11×).** See finding 6. |
| 7 | Path strip, side panel and footer repaint every second | **PARTIAL (mostly fixed)** | Measured (`view` 20 s, unchanged data): pathPanel **0** (round 2: 21), side **0** (21). Footer **18 / 16 / 12 / 15** on overview / conn / toolbox / settings (round 2: 20). The footer key includes CPU to 0.1 % and memory in MB, which change almost every second. Rounding to whole percent would stop it. |
| 8 | ipconfig /displaydns every 30 s with the view open | **FIXED** | Names are now refreshed only on conn / travel / procs (FullView.cs:576). Two empty results back off to 5 min (Monitor.cs:662, 670). |
| 9 | Penny leaves most costs at full rate | **PARTIAL** | Wakeups 434 → 144, packets 8 → 2.4 (table above). Still at full rate: the 1 Hz sampler with 3 disk counters and a 30 KB snapshot, AirLog hourly, the 2 s gateway pace with the view open, netsh rather than WlanApi, and the GameFinder process snapshot every 5 s with a game added. |
| 10 | Gamer survey not scaled by preset | **PARTIAL** | Penny: GameBgSec 60 and SurveySec 1800 (Program.cs:335, Gamer.cs:381); three games now cost 4.1/min, down from 12.6. GameFinder still takes its own `Process.GetProcesses` every 5 s (Gamer.cs:228) and does not reuse the probe's. The new Gamer preset doubles the survey (24.6/min for three games). There is still no pause when idle or locked. |
| 11 | Hover render and Gamer paint wait on files and locks on the UI thread | **STILL PRESENT, now measured** | Theme.cs:476 (`Usage.TodayDU`), 481 (`Usage.Month`) and FullView.Gamer.cs:253 (`GameHistory.Week`) are unchanged. **Measured: a cold GameHistory.Week takes 1,215 ms** (3 games, 14 days at the Gamer preset's rate, mono) and holds lock `k` the whole time. Finding 2 shows why this now reaches the UI thread. |
| 12 | Redundant process, EnumWindows and ARP enumerations | **STILL PRESENT** | Probe, GameFinder, IoTick and LeakTick each still take their own process snapshot. Three ARP readers. Cover EnumWindows now on every tick. Penny reduces the probe's share only. |
| 13 | BuildSnap 30 KB/s; Measure builds its key on every call | **STILL PRESENT** | Measured: BuildSnap **0.091 ms, 30.2 KB** (round 2: 0.079 ms, 30.1 KB). Judge 17.3 KB. Measure cache hit **1,352 B/call**. 3,000 numeric strings make 3,000 entries. |
| 14 | Overlay: 2 fonts per paint, no change check | **FIXED** | Fonts are fields (StreamOverlay.cs:23). The tick builds a key and returns when it is unchanged (48-53). Measured: **1 invalidation in 15 s** with unchanged data (the first one). The key costs a string.Join of 120 values per second, which is fine. |
| 15 | Interfaces, ARP and C: every 10 s in every preset | **PARTIAL** | Penny 60 s (Monitor.cs:405). Balanced, Engineer and Gamer are still 10 s. |

**Summary:** 5 FIXED (1, 4, 8, 14, and 7 mostly), 7 PARTIAL (2, 3, 6, 7, 9, 10, 15), 4 STILL PRESENT (5, 11, 12, 13). Number 5 is worse for the default preset.

---

## 3. New costs: measurements

### 6-month history (b312fd1)

| Item | Measured (mono, 180 synthetic days × 1440 min, real line layout) |
|---|---|
| Raw minute file | **128 KB/day** (today's file stays plain text) |
| Compressed day | **37.9 KB/day** with maximum-noise data. A Python model of a realistic idle PC gives 28-29 KB/day running 24 h. Events ≈ 0.25 KB/day. |
| 180 days on disk | **6.6 MB** (360 files) with maximum-noise data, about 5 MB realistic at 24 h/day, about 1.7 MB at 8 h/day. Settings says "6 months is still only about 3 MB" and "about 15 KB a day once compressed", which is true only for roughly 10-12 h/day of use. |
| `games-yyyyMMdd.csv` (never compressed) | **0.51 MB/day** for 3 games at the Gamer preset's ~11 lines/min. **7.1 MB at GameDays 14**, more than 180 days of History. One game on Balanced is about 0.17 MB/day. |
| History.Days() | 8 ms (180 days) |
| History.Load(one day) | 17 ms, 1.8 MB allocated (gz). Plain text: 12 ms. |
| History.Bytes() (Settings, at most every 10 s in paint) | 3 ms |
| History.Week (heatmap, Task) | 73 ms, 12.6 MB |
| Outages.Last(m, 7) (Outages page paint, 60 s cache) | 10 ms. 180 days: 24 ms. |
| OverTime.Month this / last (Toolbox Task) | 484 ms / 91 MB; **586 ms / 128 MB** |
| OverTime.RushHour (28 days) | 240 ms, 50 MB |
| OverTime.DownloadTime (30 days) | 335 ms, 54 MB |
| OverTime.WakeUp | 8 ms |
| OverTime.Sum(180 days) | **1,723 ms, 360 MB allocated** |
| **OverTime.SixMonths** | **3,424 ms, 745 MB allocated.** Every day is read twice: the total, then month by month. **With 3 games' game files: 8,018 ms**, because each day's games file is parsed once per known game (14), in both passes. |
| SixMonths peak (`peak`) | managed heap 4.2 → **27.4 MB**, working set 38 → **65 MB** (harness only, no UI). GCs: 181 gen-0, 20 gen-2. |
| History.Tidy, review missing (BeforeRollOff → SixMonths) | **3,183 ms**. The first Tidy, which also compressed 179 plain days: 4,916 ms. With the review already present: 1 ms. |
| Replay | History.Load runs in a Task (FullView.Replay.cs:46). Fine. |
| ISP report | 7 days of History or game history, in a Task. Fine. |
| Time machine day click | History.Load on the UI thread (FullView.Paint.cs:342): 17 ms. Acceptable. |

### Learn, games, ping map, QR, Share

- **Learn games timer.** 1 s, only during a timed room or quick game. It stops itself when Learn is hidden or the window is hidden or the selection changes, and it is switched off on escape or game over. Fine.
- **Learn by goal and Learn sections.** No timers. They repaint only on clicks, plus the existing single 15 s "read" repaint. Fine.
- **Labs.** The timer is now limited to `lab:tcp` and `LabRefresh` only invalidates Learn when a lab is selected. Fine.
- **Ping map.** Drawn inside gamerCanvas, which repaints every second with no change check while any game is watched. Measured 20 paints in 20 s with frozen data; UI CPU 0.29 s against 0.13 s without games. It calls `gp.Best(gm)` → GameHistory.Week, and uses `g.MeasureString` per server without the cache. See finding 2.
- **QR.** Qr.Encode on 172 bytes takes 1.53 ms; Qr.Picture at px 6 (342×342) takes 1.27 ms; OverTime.Summary takes 2 ms. All happen on a click (the summary runs inside the tool Task) and the bitmap is disposed. Fine.
- **Share.** Help-a-friend writes and encrypts the bundle in the tool Task, and the plain copy is deleted. Fine.

---

## 4. About › "What leaves your PC" against real packets per preset

The About rows (FullView.Settings.cs:375-382) still print `every(Cfg.GwSec)` and `every(Cfg.NetSec)`.

| Preset | About says (router + ping + lookup) | Actual, view closed | Actual, view open (when About is read) | Under-statement with the view open |
|---|---|---|---|---|
| Penny | every 30 s + every 5 min + every 5 min = **2.4/min** | 2.4 ✓ | **46** | **19×** (round 2: 11×) |
| Balanced | every 2 s + every 15 s + every 15 s = 38/min | 35 (DNS is really every 60 s, so this is an overstatement) | 46 | 1.2× |
| Engineer | 38 | 35 | 46 | 1.2× |
| Gamer | 38, plus "best ones every 10 s, the others every 5 min, every second while a game is open" | 35 + 8.2 per game | 46 + games | 1.2×, plus the hop timing it never mentions |

Other mismatches:

- Program.cs:5-6, the source header, still says Balanced = "gateway every 5 s; 1.1.1.1 and DNS every 30 s".
- The Gamer Zone row says "the others every 5 min", which is 30 min on Penny.
- No row mentions **match path recording**: a trace plus ≤ 144 hop pings/min while playing.

See finding 6.

---

## 5. New findings

### 1. The six-month review reads 180 days twice, plus every games file once per known game: 3.4-8 s and 745 MB of churn, and it can run on the UI thread from Settings
Severity: slow (hang risk, memory)
Where: src\Checks\OverTime.cs line 36-84 (Sum), line 116-136 (SixMonths), line 138-150 (BeforeRollOff); src\History.cs line 56 (Tidy → BeforeRollOff); src\Ui\FullView.Settings.cs line 133 (Keep history for → `History.Tidy()` on the UI thread)
How to reproduce: `mono P3.exe app histgen 180 1440`, then `tidy`, `histtime` and `peak`. Also `gamegen 3 11`. On Windows: keep 180 days of history, delete `data\reports\Six-months-*.html` (or let 170 days pass since the last one) and click Settings › Keep history for › 6 months.
What happens / what should happen:
- **What happens:**
  - SixMonths calls `Sum` over the whole period, then calls `Sum` again for each month. Every day file is decompressed and parsed twice: 360 History.Load calls.
  - Sum also calls `GameHistory.Read(d, g.Id)` for **every game in Games.All (14) for every day**. Read parses the whole `games-*.csv` (never compressed, up to 8 MB) for each game, so each existing games file is parsed 14 times per pass.
  - Measured: SixMonths takes **3,424 ms and allocates 745 MB**. With three games' files it takes **8,018 ms**. Sum keeps every minute's gateway, internet and loss values in three Lists (≈ 260 k doubles each). The heap went from 4 to 27 MB and the harness's working set peaked at 65 MB, which is the whole 64 MB budget before the UI is added.
  - BeforeRollOff runs from History.Tidy. That is normally a Task or the threadpool, but **Settings › Keep history for calls `History.Tidy()` directly on the UI thread**. When the review is due, the window freezes for the whole read (3.2 s measured), plus the gzip of any uncompressed days (4.9 s for 179 days in the worst case).
  - If SaveReview fails (a read-only data folder, or an AV lock), no file appears, so the 180-day read repeats on every Tidy (every 6 h).
- **What should happen:**
  - Read each day once. Accumulate the totals and the per-month buckets in the same pass. Keep running sums and counts instead of Lists of every minute.
  - Read each day's games file once and split it by game, and only for days within GameDays.
  - Never call Tidy on the UI thread: queue it the way History.Write does.
  - Remember a failed BeforeRollOff for a day.
Confidence: confirmed (measured: harness `tidy`, `histtime`, `peak`, `gamegen`, with the real loaders by reflection on 180 synthetic days in the exact History.Minute layout)
Rule: 7

### 2. The Gamer page, now with the ping map, repaints every second while any game is watched even when nothing changed, and its paint can wait over a second on GameHistory.Week's lock
Severity: slow (UI stall)
Where: src\Ui\FullView.cs line 526 (`if (tab == "gamer" && footTick % 2 == 0 && (... || Games.Monitored().Count > 0)) gamerCanvas.Invalidate();`), src\Ui\FullView.Gamer.cs line 123 (PaintPingMap) and line 253 (`GameHistory.Week` in paint), src\Ui\FullView.PingMap.cs line 42-80 (PaintPingMap: `gp.Best(gm)` → Week, `g.MeasureString` per server), src\Gamer\Gamer.cs line 581-600 (Week: up to 7 uncompressed day files parsed under lock `k`)
How to reproduce:
- `GAMEIDS=valorant,league,cs2 mono P3.exe app view 20 gamer` with frozen data: **gamerCanvas painted 20 times in 20 s**. UI CPU was 0.29 s, against 0.13 s on the same page with no game.
- `gamegen 3 11`: **a cold GameHistory.Week took 1,215 ms**.
What happens / what should happen:
- **What happens:**
  - With a game added and nothing being played, the region pings change at most every 10-60 s. The page still repaints the whole canvas, ping map included (8 land polygons, a MeasureString per server), every second.
  - Every 10 minutes the Week cache expires. The next caller re-reads up to 7 `games-*.csv` files, 0.5 MB/day each at the Gamer preset, while holding `k`.
  - That caller is usually the Gamer thread, through Plan → Best. A paint that lands meanwhile (the page paints every second) blocks on `k` for the rest of the read. That is about a second on mono, so a stall roughly every 10 minutes while the page is open.
  - This also affects the widget's gamer card (round 2 #11).
- **What should happen:**
  - Invalidate gamerCanvas only when a key changes: the Version of the stats, Playing, or the session's spike count.
  - Paint must never call Week. Let the Gamer thread refresh it and publish the result, and have paint read the published copy without the lock.
  - Compress or roll up the game-history files.
Confidence: confirmed (measured: harness `view` paint counts and `gamegen` timing). The lock contention is from reading.
Rule: 7

### 3. Gamer preset: "16 game servers" can't be probed at 10 a second. Regions past the 10th are never measured while playing, and the other games' background checks stop entirely
Severity: wrong result / slow
Where: src\Gamer\Gamer.cs line 346 (`MaxServers => Cfg.Preset == "gamer" ? 16 : 8`), line 365 (the played game's servers at 1 s, Take(MaxServers)), line 420-425 (Loop: `if (go.Count >= MaxPerSecond) break;` with MaxPerSecond = 10)
How to reproduce: `mono P3.exe app games` replays Loop's scheduler with the real Plan(), Gamer preset, playing Valorant (11 regions): **NA East … Oceania = 60 probes each in 60 s; Middle East = 0**. A Valve game from the Steam list (dozens of relays) loses regions 11-16 the same way.
What happens / what should happen:
- **What happens:**
  - While a game is played, every one of its ≤ 16 servers is due every second. The loop takes the first 10 in plan order each second, and those 10 are due again by the next pass.
  - Entries 11-16 never reach `go`. Neither do the background and survey entries for every other watched game, which come after them in the plan.
  - The preset that promises "up to 16 game servers" measures 10. Its best-region ranking and the Settings text are wrong for the rest.
  - On Balanced (8 + 2 spare slots) this doesn't occur.
- **What should happen:** pick the servers due this second round-robin (or oldest-due first). Alternatively, either keep MaxServers ≤ MaxPerSecond − GameBgServers or probe the played game's servers every 2 s when there are more than 10.
Confidence: confirmed (measured: scheduler replay against the real Plan and Games.All). Every built-in game has 11 regions.
Rule: 7

### 4. Match path recording adds up to 144 pings a minute to routers on the path while you play, in every preset including Penny, and About doesn't list it
Severity: slow / wrong result (disclosure)
Where: src\Gamer\Gamer.cs line 330-345 (StartSession: `PathTools.Trace(ip, 16, ...)` when `Cfg.GameFast`), line 431 (`TimeHops` every 5 s), GameSession.TimeHops (≤ 12 hops, one ping each with an 800 ms timeout, sequential); src\Ui\FullView.Settings.cs line 382 (Gamer Zone row)
How to reproduce: Add a game, start it (or a process named like its exe) on Penny. Count ICMP to the intermediate hops in Wireshark.
What happens / what should happen:
- **What happens:**
  - Each session starts with a 16-TTL trace: up to 32 probes. Then every 5 s each of up to 12 hops gets an echo, so ≤ 144/min on top of the 480-600 server probes/min.
  - GameFast is on by default and no preset changes it, so Penny pays this too.
  - Silent hops cost 800 ms each, sequentially, so one round can take ~10 s. The `timing` flag stops overlap but not the cost.
  - The About row lists only "every second while a game is open" for the servers.
- **What should happen:**
  - Time hops every 15-30 s, or only after a spike.
  - Skip hops that stayed silent in the trace.
  - Make it opt-in, or off on Penny.
  - Add the row to About ("while a game runs: a trace to its server and one ping to each router on the way every 5 s").
Confidence: confirmed by reading
Rule: 1 (disclosure), 7

### 5. Balanced now runs Engineer's timings: the default preset's background cost has doubled, and every upgrade picks it up silently
Severity: slow
Where: src\Program.cs line 301 (defaults 2 / 15 / 10 / 5), line 333-339 (ApplyPreset: balanced = engineer); src\AppCtx.cs line 210-211 (every start re-applies the named preset's timings, keeping only the game settings); src\Program.cs line 5-6 (header still says 5 s / 30 s)
How to reproduce: `mono P3.exe app jobs`: balanced and engineer print identical values (Gw 2, Net 15, Wi-Fi 10, Apps 5), and both are "~71 checks a minute" (round 2: Balanced 36).
What happens / what should happen:
- **What happens:** for the default preset, compared with a0fbd56:

  | Cost | Before | Now |
  |---|---|---|
  | Packets/min | 16 | **35** |
  | netsh.exe starts/day (each with a conhost) | 2,880 | **8,640** |
  | Process snapshots with OpenProcess per process | 6/min | **12/min** |
  | Interfaces / ARP reads | 6/min | 6/min (unchanged) |
  | Probe-thread wakeups | 12/min | **30/min** |

  - AppCtx re-applies the named preset on every start, so every 1.1.4 and 1.1.5-beta user on Balanced moves to the new pace without choosing it.
  - The Engineer card no longer differs from Balanced at all.
  - The code comment says this was deliberate (Adeeb, 2026-11-12). It is reported because it moves the widget everyone runs all day away from rule 7, and it makes round 2 #5 three times worse for the default user.
- **What should happen:** if intended, at least move Wi-Fi to WlanApi (round 2 #5) and keep Balanced's process sampling at 10 s. Alternatively, restore Balanced to 5 / 30 / 30 / 10 and leave the close pace to Engineer and Gamer. Update the Program.cs header either way.
Confidence: confirmed (measured: `jobs`, real Cfg values)
Rule: 7

### 6. About › "What leaves your PC" now under-reports Penny 19× while the engineer view is open; Balanced's DNS line and the Gamer Zone row are also wrong
Severity: wrong result (cost statement)
Where: src\Ui\FullView.Settings.cs line 373-382 (`every(Cfg.GwSec)`, `every(Cfg.NetSec)`, "the others every 5 min"), src\Monitor.cs line 220 (gateway `ViewOpen ? 2` in every preset) and line 255 (DNS 60 s / 15 s), src\Program.cs line 5-7
How to reproduce: Choose Penny and open About. It reads "router every 30 s" and "one ping … and one name lookup, every 5 min" (2.4/min). While that page is open, the probe sends the gateway ping every 2 s, the internet ping every 5 s and DNS every 15 s: 46/min.
What happens / what should happen:
- **What happens:**
  - Penny's figure is right only while the window is closed. The ratio is now 19×; round 2 found 11×.
  - On Balanced, Engineer and Gamer the row says "one name lookup every 15 s". It is every 60 s with the view closed and every 15 s with it open, while the ping runs every 5 s with the view open.
  - The Gamer Zone row says "the others every 5 min", which is 30 min on Penny, and leaves out match path recording (finding 4).
  - The source header still describes the old Balanced pace.
- **What should happen:**
  - Print each row's view-open and call rates explicitly ("every 2 s while this window is open").
  - Cap Penny's view-open gateway pace at 5-10 s, as round 2 recommended.
  - Print the DNS interval separately.
  - Use `GamePing.SurveySec` in the Gamer row.
Confidence: confirmed by reading (rates from the code paths, Cfg values from `jobs`)
Rule: 1, 7

### 7. Game-history files are never compressed: 7-12 MB at 14 days with three games, more than 180 days of History. The Settings "6 months ≈ 3 MB" figure assumes part-time use.
Severity: slow (minor, disk)
Where: src\Gamer\Gamer.cs line 532 (`games-yyyyMMdd.csv` in History.Dir) and line 554-562 (Tidy only deletes); src\History.cs line 66 (History.Tidy compresses only names that start with a date, so `games-…` is skipped); src\Ui\FullView.Settings.cs line 132-133 ("about 15 KB a day once compressed", "6 months is still only about 3 MB")
How to reproduce: `gamegen 3 11` gives **0.51 MB/day, 7.1 MB for 14 days**, in plain text. The Gamer preset with five games and the hop data is larger still. `histgen 180 1440` + `tidy` gives **37.9 KB/day and 6.6 MB for 180 days** with maximum-noise data. A realistic idle PC on 24 h is about 28 KB/day, ≈ 5 MB.
What happens / what should happen:
- **What happens:** the Gamer Zone's per-minute rows stay as plain text for GameDays. They are re-read every 10 min by Week (finding 2) and 14 times per day by Sum (finding 1). History at 180 days running 24 h is about twice the stated 3 MB.
- **What should happen:** compress finished games days, as History does, or roll them up per hour. State the size as "about 2-5 MB, depending on how many hours a day the PC is on".
Confidence: confirmed (measured: harness generation + the real Tidy)
Rule: 7

### 8. Upgrading Penny users keep the old 30 s game background pace
Severity: slow (minor)
Where: src\AppCtx.cs line 210-211 (`int gb = Cfg.GameBgSec, gs = Cfg.GameBgServers; Cfg.ApplyPreset(Cfg.Preset); Cfg.GameBgSec = gb; ...`)
How to reproduce: Start from a 1.1.4 or a0fbd56 settings file with `preset=penny` and `gbg=30`, then start 8502674.
What happens / what should happen:
- **What happens:** the start-up re-apply keeps the saved GameBgSec (30), so Penny's new 60 s background pace never applies until the user clicks Penny again. The survey does follow SurveySec, which reads the preset live. A Penny user with three games sends 6 + 1.1 = 7.1/min instead of 4.1.
- **What should happen:** keep the saved game pace only when it differs from the old preset's default (30 / 3), or apply the preset's game pace on the first start after an upgrade.
Confidence: confirmed by reading
Rule: 7

---

## Checked and fine

- Single-instance thread: the 30 s timeout is harmless, since it only re-checks `quitting`.
- Wake-up report:
  - After a resume the probe runs at 1 s for at most 120 s: the gateway until it answers, then ping + DNS until DNS answers.
  - It is bounded and ends itself in WakeStep. At worst that is 120 gateway pings when the router never answers.
- ICMP-to-TCP fallback after 3 misses: one extra TCP connect per internet probe while ICMP stays blocked. That is 0.2/min on Penny and 4/min on Balanced. It is disclosed in About.
- Stream overlay change key: it builds one string from 120 values each second. That is about 2 KB of garbage per second, which is acceptable.
- Learn, games and labs timers all stop themselves when hidden. The engineer view still closes on minimize.
- Replay, the ISP report, the week heatmap and every OverTime Toolbox tool run in Tasks. History.Load on a Time machine click (17 ms) is the only History read on the UI thread outside finding 1.

## Harness

The harness is `/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/review3/harnessP3/P3.cs`, built as P3.exe. The app copy with its portable marker and the generated history is in `harnessP3/app/`.

Modes:
- `jobs`, `draw`, `snap`, `measure`, `qr`
- `view <secs> <page>` (set `GAMEIDS=a,b` to watch games)
- `overlay <secs>`
- `histgen <days> <min/day>`
- `tidy`, `histtime`, `peak`
- `gamegen <games> <lines/min>`
- `games`

Run with `xvfb-run -a mono P3.exe app <mode> …`.
