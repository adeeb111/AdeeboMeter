# AdeeboMeter 1.1.5 (a0fbd56): performance and hang review, round 2

**Scope.** The whole of `src/`, read with the all-day paths first. Those are AppCtx, Monitor, Brain, History, AirLog, Gamer, StreamOverlay, Controls, Theme and the FullView timers.

**What I ran.** The harness is `review2/harnessP/P.cs` (P.exe). It loads the a0fbd56 build (`build2/r2.exe`) by reflection under `xvfb-run mono` from a portable copy in `harnessP/app/`, and measures four things:

- `draw`: the cost of each widget render.
- `snap`: the cost of BuildSnap and Judge.
- `measure`: the text-measure cache.
- `view`: per-control Paint counts while the engineer view is open and nothing changes.

Mono numbers show relative cost only, because GDI+ is libgdiplus there and every Windows P/Invoke fails. The sampler cannot run on Linux (its first call, `GetSystemTimes`, throws every second), so the `view` test feeds the view an identical snapshot every second, which is what an idle PC looks like.

**Muse overlap.** Where Muse (`review-1.1.5/round-2/muse.md`) already reported an item, I refer to it rather than report it again. See the end of this file. One of Muse's statements is wrong: UpdateBreak does not notify.

---

## Cost table

All rates assume the widget is on, the mouse is away from the corner, the PC is idle and no game is added, unless the row says otherwise.

- **Wakeups/min** counts timer ticks and thread loop iterations, which is roughly the number of times the CPU is woken.
- **Packets/min** counts requests that AdeeboMeter sends. Each one gets a reply.
- **P / B / E** are the Penny, Balanced and Engineer presets.

| # | Recurring job (where) | Interval P / B / E (view closed) | View open | Work per tick | Stops when hidden / locked / on battery? | Wakeups/min P / B / E (view open) | Packets/min P / B / E (view open) |
|---|---|---|---|---|---|---|---|
| 1 | Widget UI timer `ui` (AppCtx.cs:146, OnUi 313) | **250 ms in every preset**. 50 ms when the mouse is within 120 px of the corner, hovering or dwelling. 500 ms when the widget is hidden, the view is up or you are presenting. 1 s with no screen. | same (500 ms when the view covers the widget's screen) | Screen.AllScreens (called through WidgetScreen 3-4 times), Cursor.Position, a 7-part state string (~500 B), a Presenting() check every 8th tick | **No.** It never stops: not when the widget is hidden (500 ms), not when the screen is locked, the display is off or the PC is on battery. The preset does not touch it. | **240 / 240 / 240** (1,200 near the corner; 120 with the widget hidden) | 0 |
| 1a | Cover check `WindowOver` (AppCtx.cs:328, 384) | gated to 400 ms, so in practice every second 250 ms tick | same | EnumWindows over every top-level window. For each visible one: GetWindowLong, DwmGetWindowAttribute(cloaked), GetClassName, then DwmGetWindowAttribute(bounds). A new delegate thunk on every call. | No | ~120 (inside the 240) | 0 |
| 1b | Widget render `Render` (AppCtx.cs:357-376) | Folded chip: when its words or colour change, plus **every 30 s** (AppCtx.cs:321). Open card: **every second**, see finding 1. | same | Chip 98×26: 0.25 ms, 3.9 KB managed (mono). **Card 264×263: 2.17 ms, 14.4 KB managed, 271 KB pixel buffer**, plus an equal-size GetHbitmap copy and UpdateLayeredWindow of the whole card and SetWindowPos (Controls.cs:43-60). These sizes are at 100 % scaling; at 150 % they are 2.25×. | No | chip ~2; **card 60** | 0 |
| 2 | Sampler thread (Monitor.cs:119-152) | 1 s in every preset | 1 s | GetSystemTimes, GlobalMemoryStatusEx, three PhysicalDisk PerformanceCounter.NextValue calls, GetIPStatistics, TCPv4/TCPv6/ICMP/UDP statistics, **BuildSnap (0.08 ms, 30 KB, measured)**, Brain.Second. **Judge** every 10 s (0.10 ms, 17 KB). | No. Penny does not change it. | **60 / 60 / 60** | 0 |
| 2a | Brain PC jobs on the "pc jobs" thread (Brain.cs:65-71) | PcHealth.Sample (8 perf counters) every 15 / 5 / 5 s. HungTick (EnumWindows + IsHungAppWindow) every 30 / 10 / 10 s. GwTick (ARP table) every 90 / 30 / 30 s. IoTick (GetProcesses + OpenProcess per process) every 180 / 60 / 60 s. LeakTick (GetProcesses) every 15 / 5 / 5 min. SpaceTick every 6 h. | same | see interval column | No | **~7 / ~21 / ~21** | 0 |
| 2b | Brain per-minute flush (Brain.cs:100-108) | 1 min | same | History append (1 disk write/min), Usage (rewrites usage.txt every 5 min), Baseline, Devices.Check every 10 min (ARP table, devices.txt), **AirLog every hour** (WlanGetNetworkBssList, rewrites air.txt), **UpdateBreak every hour** (two System-log EvtQuery calls over 14 days, plus History day reads) | No. Penny does not change it. AirLog and Devices respect History; UpdateBreak does not. | 1 + small | 0 |
| 3 | Probe thread (Monitor.cs:208-268) | pace = min(gw, net, wifi, apps, 10 s), at least 2 s: **10 s / 5 s / 2 s** | **2 s in every preset** (also during a Teams call when Call is on) | gateway ping; 1.1.1.1 ping + **DnsQuery with BYPASS_CACHE** (a real query each time); every 10 s RefreshInterfaces + ARP table + DriveInfo("C"); Wi-Fi = **a netsh.exe process**; GetExtendedTcpTable + Watch.Conns (ExePath per PID); Process.GetProcesses + TotalProcessorTime per process; ProbeCall (UDP table) on every loop while Call is on | No (it slows down only through the preset) | **6 / 12 / 30 (30)** | gateway **6 / 12 / 30**; ping + DNS **2 / 4 / 8**; total **8 / 16 / 38**; **view open: 90 in every preset** (30 gateway + 30 ping + 30 DNS) |
| 3a | netsh wlan show interfaces (Monitor.cs:437) | 120 s / 30 s / 10 s, only on Wi-Fi | 10 s | **a new netsh.exe process** (plus a console host), stdout read, up to 3 s timeout on the probe thread | No | 0.5 / 2 / 6 process starts per min (6) | 0 |
| 3b | Process sampling ProbeProcesses (Monitor.cs:566) | 30 s / 10 s / 5 s | 4 s, or 2 s on What's running | GetProcesses snapshot, then OpenProcess + GetProcessTimes for each of about 250 processes, throwing and catching an exception for each protected one | No | 2 / 6 / 12 (15-30) | 0 |
| 3c | Provider name LearnIsp (Monitor.cs:459) | once per network | once | 6-hop trace + a reverse-DNS (PTR) lookup | n/a | n/a | about 18 packets per network change |
| 4 | Gamer loop thread (Gamer.cs:383-420) | **1 s, always**, even with no game added | 1 s | Games.Monitored() splits the settings string. With games added: GameFinder.Running = **Process.GetProcesses every 5 s**, Plan + Best (GameHistory.Week, cached 10 min), probes. | **No.** Penny does not change it. | **60 / 60 / 60** | no game: 0. One game with 12 regions: ~4.2 (best region every 30 s plus the 5-min survey). Three games: ~12.6, **more than everything else Penny sends**. While playing: up to 8 servers/s = 480. Penny does not change any of these. |
| 5 | Single-instance wait thread (AppCtx.cs:156-171) | `WaitAny(..., 1000)`, a 1 s timeout used only to re-check `quitting` | 1 s | nothing | No | **60** | 0 |
| 6 | Pinned watch (Watch.cs:256) | 30 s, opt-in | 30 s | a ping on the thread pool | No | (on the sampler) | 2 if on |
| 7 | Engineer view timer (FullView.cs:309, OnTimer2 510) | n/a | 500 ms | On each snapshot: **pathPanel, side and footer repaint every second, measured** (the `view` test, 20 s with identical data: pathPanel 21, side 21, footer 20 paints on the overview, conn and toolbox pages). UpdateBadges every tick. RefreshConns every 10 s (2 s on Connections, Travel and What's running). RefreshIpNames every 15 s, which can start **ipconfig /displaydns every 30 s** (Monitor.cs:631). | Minimizing closes the view (good). It keeps running while the view is open but behind other windows, or the screen is locked. | (120 + 3 panel paints/s) | 0 |
| 7a | Freeze watchdog (FullView.cs:71) | n/a | 1 s threading timer + a BeginInvoke into the UI thread | a timestamp | Stops when the view closes | (60 + 60) | 0 |
| 8 | Labs 66 ms, Journey 33 ms, SlideSwitch 15 ms | n/a | only while playing or animating | animation | **Yes**: each stops itself when its page is hidden or the run ends | 0 when idle | 0 |
| 9 | Stream overlay (StreamOverlay.cs:21, 46) | 1 s while open | 1 s | full repaint with **2 new Fonts each paint**; with "Write a PNG" on, a second paint into a bitmap plus PNG encoding every second (round 1, #14) | No | 60 (+60) | 0 |
| 10 | Live capture limit timer (LiveCapture.cs:125) | 30 s while a capture runs | | CheckLimit | stops with the capture | 2 | 0 |

**Totals, view closed, idle:**

| | Penny | Balanced | Engineer |
|---|---|---|---|
| Wakeups/min | ≈ **434** (7.2/s) | ≈ **453** (7.6/s) | ≈ **471** (7.9/s) |
| Of which jobs the preset controls | 6 + 7 = 13 | 33 | 51 |

The other ~420/min are the same in every preset:

- widget timer: 240
- sampler: 60
- Gamer loop: 60
- single-instance wait: 60

With the view open, add about 120 (view timer) + 120 (watchdog and its BeginInvoke) + 180 panel repaints, and the probe moves to 30/min in every preset.

**Over the ~1/s idle wakeup line.** Three jobs tick at 1 Hz or faster with nothing to do: the widget timer (4 Hz), the Gamer loop (1 Hz with no game) and the single-instance wait (1 Hz). The sampler (1 Hz) is by design, but it builds a 30 KB snapshot whether or not anything will show it.

**Stated packet counts against real ones.**

| Claim (source) | Penny | Balanced | Engineer |
|---|---|---|---|
| Header (Program.cs:5-7) and About › "What leaves your PC" (FullView.Settings.cs:372+) | 6 gw + 1 ping + 1 DNS = 8/min | 12 + 2 + 2 = 16/min | 30 + 4 + 4 = 38/min |
| Actual, view closed | **8 (matches)** | **16 (matches)** | **38 (matches)** |
| Actual, view open (which is when About is being read) | **90** | **90** | **90** |
| Actual, Teams call, view closed | 8 (Call is off in Penny) | 12 + 30 + 30 = 72 | 30 + 30 + 30 = 90 |
| Settings "~N checks a minute" (JobsPerMinute, Program.cs:338) | 18 | 36 | 74 |
| Same, adding the uncounted PC jobs, the Gamer loop and the widget | ≈ 25 + 60 gamer | ≈ 57 + 60 | ≈ 95 + 60 |

So Penny sends what it says while the engineer view is closed. Two things are missing from every "what it costs" statement:

- The view-open and call pace: 90 requests/min, about 11 times Penny's figure. The About rows print `every(Cfg.NetSec)`, so they say "every 1 min" at the moment the true rate is every 2 s.
- The Gamer Zone survey, which is the same in every preset.

---

## Findings

### 1. When the widget card stays open on a Busy or Problem level, it re-renders the whole card every second all day, because a decorative countdown changes every second
Severity: slow
Where: src\AppCtx.cs line 353-360 (OnUi) and src\Ui\Theme.cs line 412-437 (Draw.Card, `string cd = s.Countdown + "s"`), src\Ui\Controls.cs line 43-60 (LayeredForm.Present)
How to reproduce: Use a PC whose memory stays above 80 % (common on an 8 GB laptop), or whose CPU stays above 60 %. Monitor.Judge then sets level 1 ("Busy"). Leave the widget uncovered and the mouse away.
What happens / what should happen:
- **What happens:** `compact = !hovered && (covered || (Level == 0 && !Call))` is false at level 1, so the full card is shown at 30 % alpha. Each new snapshot (1/s) passes `fresh && !compact`, so Render runs every second, and the snapshot always differs because the card prints `Countdown + "s"` and live CPU, RAM and Mb/s numbers.
  - Cost per second: Draw.Card (2.17 ms and 14.4 KB managed on mono), a new 271 KB ARGB bitmap at 100 % (610 KB at 150 %), a second GDI copy from `GetHbitmap`, UpdateLayeredWindow of the whole card, and SetWindowPos(TOPMOST).
  - That is about 86,400 full-card composites a day. Native allocation churn is about 0.5-1.2 MB/s.
- **What should happen:**
  - Re-render the unhovered card only when its verdict, level or headline changes, or at most every 5-10 s. Drop the countdown from the unhovered card, or draw it only while hovered.
  - Keep one reusable bitmap and HBITMAP (a DIB section) instead of `new Bitmap` + `GetHbitmap` per frame.
  - Only call SetWindowPos when it is actually needed (see finding 4).
Confidence: confirmed by reading the condition. Render cost measured (harness `draw`, 200 renders).
Rule: 7

### 2. The widget timer runs at 4 Hz forever, including when the widget is hidden, the screen is locked or the display is off, whatever the preset
Severity: slow
Where: src\AppCtx.cs line 146, 318, 323, 326 (OnUi)
How to reproduce: Choose tray › "Hide widget". Or lock the PC (Win+L). Or switch to Penny. Then count timer ticks.
What happens / what should happen:
- **What happens:** the interval is 250 ms when idle and 50 ms near the corner. With the widget hidden, the view up or presenting it is 500 ms, which still means 120 wakeups/min to do nothing.
  - On lock there is no handling at all (OnSession reacts only to unlock), so the 4 Hz loop, the 2.5 Hz EnumWindows cover check and the 30 s forced redraw continue on the lock screen and with the display asleep.
  - Every tick also calls Screen.AllScreens through WidgetScreen() 3-4 times and builds a ~500 B state string (measured).
- **What should happen:**
  - With the widget hidden by the user, stop the timer: the tray menu re-enables it.
  - Pause on SessionSwitch Lock and RemoteDisconnect, and on display-off (RegisterPowerSettingNotification GUID_CONSOLE_DISPLAY_STATE). Resume on unlock.
  - When the mouse is far away, 1 Hz is enough for the hover and cover state. Better still, detect the mouse with a low-level hook or SetWinEventHook instead of polling. Use EVENT_SYSTEM_FOREGROUND / EVENT_OBJECT_LOCATIONCHANGE to trigger the cover check instead of polling EnumWindows every 400 ms.
  - Penny should at least take the idle interval to 1 s.
Confidence: confirmed by reading (no Windows to count ticks). The allocation per tick was measured.
Rule: 7

### 3. The Gamer loop and the single-instance listener each wake once a second forever, even when nothing can happen
Severity: slow
Where: src\Gamer\Gamer.cs line 390 (GamePing.Loop, `Thread.Sleep(1000)` even with `mon2.Count == 0`), src\AppCtx.cs line 163 (`WaitHandle.WaitAny(both, 1000)` only to re-check `quitting`)
How to reproduce: Use a fresh install with no game added, in any preset.
What happens / what should happen:
- **What happens:** together these are 120 wakeups/min that do no work. They are about a quarter of all idle wakeups, and Penny does not change them.
- **What should happen:**
  - The Gamer thread should wait on an event that `Games.SetMonitored` sets, and sleep without a timeout while no game is monitored.
  - The single-instance thread should add a quit event to the WaitAny array and wait with no timeout. Quit() sets it.
Confidence: confirmed by reading.
Rule: 7

### 4. The folded chip is fully re-rendered every 30 s even when nothing changed
Severity: slow (minor)
Where: src\AppCtx.cs line 321 (`if ((Clock.Mono - lastTop).TotalSeconds > 30) { ...; drawnKey = ""; }`)
What happens / what should happen:
- **What happens:** this is meant to claim the top layer back. It does that by clearing `drawnKey`, which forces Draw.Chip, a new Bitmap, GetHbitmap and UpdateLayeredWindow. That is 2,880 redundant renders a day.
- **What should happen:** call `SetWindowPos(HWND_TOPMOST, NOMOVE|NOSIZE|NOACTIVATE)` alone every 30 s, and only when the window is actually no longer top-most. GetWindow(GW_HWNDPREV) or a check through `WindowOver` can tell.
Confidence: confirmed by reading.
Rule: 7 (repaint when nothing changed)

### 5. Wi-Fi watch starts a netsh.exe process every 30 s by default (every 10 s in Engineer and whenever the view is open)
Severity: slow
Where: src\Monitor.cs line 258, 437 (ProbeWifi → `Job.Run(Job.Netsh, "wlan show interfaces", 3000)`)
How to reproduce: Use Balanced on Wi-Fi and watch Process Monitor's process starts, or the Task Manager PID churn.
What happens / what should happen:
- **What happens:** the process starts are:

  | Preset | netsh starts |
  |---|---|
  | Balanced | 2,880/day |
  | Engineer, or view open | 8,640/day |
  | Penny | 720/day |

  Each one creates a process and a console host, loads netsh's helper DLLs and is scanned by Defender. That is easily the most expensive single recurring job, likely tens of ms of CPU each on a laptop. A slow netsh also blocks the probe thread for up to 3 s, which delays the next gateway ping.
- **What should happen:** the code already has WlanApi (LinkInfo.cs:186). `WlanQueryInterface(wlan_intf_opcode_current_connection)` gives SSID, BSSID, signal quality, rate and state, and `WlanGetNetworkBssList` gives the channel. This needs no process and no language parsing. Keep netsh only as a fallback. Penny's 120 s is fine once the check is in-process.
Confidence: confirmed by reading (Job.Run → Process.Start, Program.cs:503-511). Cost suspected (no Windows here).
Rule: 7

### 6. With the engineer view open (or during a Teams call), every preset sends 90 requests a minute, and About under-reports this by up to 11×
Severity: wrong result (cost statement), slow
Where: src\Monitor.cs line 219 (`netSec = ... fast ? 2 : Cfg.NetSec` with `fast = ViewOpen || CallLikely`), line 236-250 (ping + `ProbeDns` with DNS_QUERY_BYPASS_CACHE each time), src\Ui\FullView.Settings.cs about line 372-376 ("What leaves your PC" prints `every(Cfg.NetSec)`), src\Program.cs line 5-7
How to reproduce: Choose Penny, then open the engineer view to About › "What leaves your PC". It reads "One ping to 1.1.1.1 ... every 1 min". Count packets with Wireshark.
What happens / what should happen:
- **What happens:** while the view is open, the probe pings the gateway every 2 s, 1.1.1.1 every 2 s, and sends a real (cache-bypassing) DNS lookup for www.microsoft.com every 2 s. That is 90 requests/min.
  - With the view closed during a Teams call (Balanced), the internet ping and DNS also go to 2 s: 72/min.
  - Penny's "fewer checks" therefore applies only while you are not looking at the app.
  - The DNS lookup every 2 s is the most questionable part: it is a fresh recursive query to the DNS server 30 times a minute.
- **What should happen:**
  - Keep the 2 s gateway and internet pace while the view is open if wanted, but run DNS at most every 10-15 s.
  - Let Penny cap the view-open pace too, at 5 s for example.
  - Have the About rows and the header print the view-open and call rates ("every 2 s while this window is open or a Teams call is on").
Confidence: confirmed by reading.
Rule: 1 (disclosure accuracy), 7

### 7. The engineer view repaints the path strip, side panel and footer every second even when nothing changed
Severity: slow
Where: src\Ui\FullView.cs line 511 (`pathPanel.Invalidate(); side.Invalidate();` on every new snapshot, with no change check) and line 515 (`if (++footTick % 2 == 0) { SelfStats(); footer.Invalidate(); }`)
How to reproduce: `mono P.exe app view 20 overview` (also `conn` and `toolbox`) feeds an identical snapshot each second.
What happens / what should happen:
- **What happens:** in 20 s there were pathPanel 21, side 21 and footer 20 paints, on every page. The page canvases themselves correctly do not repaint: OverviewTick's key check works.
- **What should happen:** build a key for each panel from what it prints, as headKey and OverviewTick already do, and invalidate only when that key changes. The footer's "AdeeboMeter uses x % CPU / y MB" could round to whole numbers and repaint only when they change.
Confidence: confirmed (measured: Paint counts per control, harness `view`, 3 pages × 20 s).
Rule: 7

### 8. With the view open, RefreshIpNames can start `ipconfig /displaydns` every 30 s
Severity: slow
Where: src\Monitor.cs line 604-640 (RefreshIpNames), called from src\Ui\FullView.cs RefreshConns every 15 s on any tab
What happens / what should happen:
- **What happens:** when `DnsGetCacheDataTable` returns an empty table (the comment says "some Windows builds") and the cache isn't admin-only, the fallback starts ipconfig.exe every 30 s while the view is open, on any page, even when Connections isn't shown. It also does up to 3,000 cache-only DnsQuery calls every 15 s.
- **What should happen:** refresh names only while the Connections, Travel or What's running page is visible, as the conn list's own pace already does (FullView.cs:513). After two empty results in a row, back off to 2-5 min.
Confidence: suspected (read; depends on the Windows build).
Rule: 7

### 9. Penny leaves most all-day costs at full rate
Severity: slow
Where: src\Program.cs line 325-330 (ApplyPreset), src\Brain\Brain.cs line 65 (`k = 3` for Penny only)
What happens / what should happen:
- **What Penny turns down:**
  - gateway ping, from 5 s to 10 s
  - internet ping and DNS, from 30 s to 60 s
  - Wi-Fi netsh, from 30 s to 120 s
  - process and connection sampling, from 10 s to 30 s
  - call watching off
  - Brain PC jobs, three times less often
- **What Penny leaves at full cost:**

  | Job | Penny cost |
  |---|---|
  | Widget timer | 4 Hz |
  | Cover EnumWindows | 2.5 Hz |
  | Unhovered card redraw | 1 Hz |
  | Sampler | 1 Hz, with 3 PerformanceCounter reads and a 30 KB snapshot |
  | Gamer loop | 1 Hz |
  | Single-instance wait | 1 Hz |
  | Gamer probes and 5-min region survey | unchanged |
  | RefreshInterfaces + ARP table + DriveInfo | every 10 s, unchanged |
  | View-open pace | 2 s, unchanged (finding 6) |
  | AirLog | hourly |
  | UpdateBreak (System-log query) | hourly |
  | History write | every minute |
  | Devices | every 10 min |

  Per minute, Penny is ≈ 434 wakeups against Balanced's ≈ 453. That is 4 % fewer wakeups for the "lightest touch".
- **Recommended Penny settings:**

  | Setting | Penny value |
  |---|---|
  | Widget idle interval | 1 s, stopped when hidden or locked (finding 2) |
  | Unhovered card | redraw at most every 10 s (finding 1) |
  | Sampler | 2-5 s, with BuildSnap skipped when nothing reads it (widget folded, view closed) |
  | Disk counters | dropped |
  | Interfaces / ARP / C: free space | every 60 s; NetworkAddressChanged already wakes the probe |
  | Gateway ping | 30 s |
  | Internet ping + DNS | 5 min |
  | DNS | never faster than every 60 s, even with the view open |
  | Wi-Fi | WlanApi in-process every 120 s (finding 5) |
  | Gamer background | GameBgSec 60 s and the survey off (or every 30 min); games actually being played keep 1 s |
  | AirLog | every 3 h |
  | UpdateBreak | once a day |
  | View-open pace | capped at 5 s |
  | Gamer loop and single-instance wait | event-driven (finding 3) |

  Those Penny settings would bring it to about 70-80 wakeups/min (≈ 1.2/s) and about 3 packets/min.
Confidence: confirmed by reading and counting.
Rule: 7

### 10. The Gamer Zone's background survey isn't scaled by any preset and can send more than all of Penny's own checks
Severity: slow
Where: src\Gamer\Gamer.cs line 349-366 (Plan: best regions every GameBgSec, every other region every `SurveySec = 300`), line 395 (probes), line 397 (GameFinder.Running = Process.GetProcesses every 5 s)
How to reproduce: Add three games with 12 regions each. Choose Penny.
What happens / what should happen:
- **What happens:** the best regions are pinged every 30 s (6/min) and the other 33 regions every 5 min (6.6/min), for about 12.6 requests/min. Penny itself sends 8.
  - Each probe of a host name calls Dns.GetHostAddresses.
  - A full process snapshot runs every 5 s just to see whether a game is running, while the probe thread already holds `M.PidNames` from its own snapshot.
- **What should happen:**
  - Penny should stretch GameBgSec and SurveySec (for example 120 s and 30 min).
  - GameFinder should reuse `Monitor.PidNames`, or a single shared snapshot.
  - The survey should pause when the PC is idle or locked.
Confidence: confirmed by reading.
Rule: 7

### 11. Hover-card render reads and waits on files on the UI thread (Usage lock, GameHistory.Week)
Severity: slow (hang risk)
Where: src\Ui\Theme.cs line 476 (`Usage.TodayDU()`), line 481 (`Usage.Month(null)` per render when a data cap is set), line 300-312 (GamerCard → GamerTarget → `GamePing.Me.Best` → `GameHistory.Week`), src\Ui\FullView.Gamer.cs line 251 (`GameHistory.Week` in paint), src\Brain\Brain.cs line 104 → Usage.Add/Save
What happens / what should happen:
- **What happens:**
  - Usage's lock `k` is held by the sampler thread while `Usage.Save` rewrites usage.txt (every 5 min) and on the first `Load()`. A hover render that lands then blocks the UI thread for the duration of a file write. On a slow or AV-scanned disk that can be hundreds of ms.
  - The first hover after start does the `Store.Read("usage.txt")` (up to 400 days × networks) on the UI thread.
  - GameHistory.Week reads up to 7 day-files (each up to 8 MB / 60,000 lines) under lock `k`, which is also taken by GameHistory.Minute's append. The first caller after the 10-minute cache expiry pays for it. Usually that is the Gamer thread, but if the widget's gamer card or the Gamer page paints first, it is the UI thread.
  - `Usage.Month(null)` runs a LINQ pass over all rows on every card render when a cap is set.
- **What should happen:**
  - Have Brain.Second compute today's and this month's totals and publish them in the Snap. Never take Usage's lock on the UI thread. Write the file outside the lock (copy the rows first).
  - Have GameHistory.Week refresh on the Gamer thread only; paint reads the last cached value.
Confidence: suspected (read; not timed on Windows).
Rule: 7

### 12. Redundant system-wide enumerations: four process snapshots, two EnumWindows loops, three ARP table reads
Severity: slow (minor)
Where:

| Enumeration | Callers |
|---|---|
| Process snapshots | Monitor.cs:566 (ProbeProcesses), Gamer.cs:228 (every 5 s with a game added), PcHealth IoTick (each 1-3 min, plus OpenProcess for every PID), LeakTick |
| EnumWindows | AppCtx.cs:389 (2.5 Hz cover check), PcHealth.HungTick (every 10-30 s) |
| ARP table | Monitor.WatchGatewayMac (10 s), Brain.LearnNetwork (60 s), RoutingTools.GwTick (30 s) |

What happens / what should happen:
- **What happens:** ProbeProcesses reads `TotalProcessorTime` per process. For protected processes this throws and is caught. The exact access right depends on the Framework version; I suspect a few dozen first-chance exceptions per sample, each with a stack trace.
- **What should happen:**
  - Take one ARP read per 10 s and share it.
  - Take one process snapshot per probe tick and share it.
  - Remember PIDs that refused access and skip them until they exit.
  - Or read CPU per process with one NtQuerySystemInformation(SystemProcessInformation) call, which carries the times and needs no OpenProcess.
Confidence: suspected (read).
Rule: 7

### 13. Allocation churn: BuildSnap builds a 30 KB snapshot every second whether or not anything shows it; Measure builds a ~1.3 KB key per call
Severity: slow (minor)
Where: src\Monitor.cs line 792-850 (BuildSnap: the Details dictionary, 6 Detail cards, top-6 by CPU and memory with LINQ sorts of all processes, HealthHist arrays), src\Ui\Theme.cs line 234-243 (Draw.Measure key = `f.Name + "|" + f.Size.ToString("R") + ... + text`)
What happens / what should happen:
- **What happens (measured):**
  - BuildSnap is 0.079 ms and 30.1 KB per call with 250 processes, so 2.6 GB/day of gen-0 garbage. Judge is 17.2 KB every 10 s.
  - A Measure cache hit still allocates 1,352 B for the key.
  - Live numeric strings ("↑ 1.2 Mb/s") each add a cache entry until the 3,000-entry clear (3,000 distinct strings gave 3,000 entries).
  - None of this is unbounded, so it does not threaten the 64 MB budget. It does keep the GC and the allocator busy all day.
- **What should happen:**
  - Build the hover Details only when the card is hovered, or lazily.
  - Skip BuildSnap's top-N sorts when Apps hasn't changed since the last snapshot (Apps updates every 10-30 s, BuildSnap runs every 1 s).
  - Key the Measure cache with a struct (Font reference, width, text), and don't cache strings that contain digits.
Confidence: confirmed (measured: harness `snap`, 2,000 calls; harness `measure`).
Rule: 7

### 14. The stream overlay creates two Fonts per paint, and repaints every second with no change check
Severity: slow (minor)
Where: src\Ui\StreamOverlay.cs line 46, 74
What happens / what should happen:
- This extends round 1 #14, which covered the PNG rewrite.
- **What happens:** each 1 s tick, `new Font("Segoe UI", 9f)` and `new Font("Segoe UI Semibold", 20f)` are created and disposed, and GamePing.Best is called. With PNG on, the frame is also painted into a bitmap and PNG-encoded just to compare bytes.
- **What should happen:** keep the fonts as fields. Key the repaint on (last value, last timestamp, title) and skip both the paint and the encode when the key is unchanged.
Confidence: suspected (read). My Paint counter could not see the overlay because its OnPaint override doesn't call base, so the Paint event never fires.
Rule: 7

### 15. Probe thread runs RefreshInterfaces, an ARP read and DriveInfo("C") every 10 s in every preset
Severity: slow (minor)
Where: src\Monitor.cs line 257
What happens / what should happen:
- **What happens:** `GetAllNetworkInterfaces()` (GetAdaptersAddresses over all adapters, including virtual ones), Routes.WindowsPick, an ARP table read and a C: free-space query run every 10 s in all presets. NetworkChange.NetworkAddressChanged already sets `netChanged` and wakes the loop when the network changes.
- **What should happen:** keep the 10 s pace only while the view is open. Otherwise use 60 s, or rely on the change events with a 5-min safety refresh.
Confidence: confirmed by reading.
Rule: 7

---

## Checked and fine (not findings)

- **Labs, Journey, SlideSwitch animation timers** (66 / 33 / 15 ms) each stop themselves when their page is hidden, the window is minimized or the run ends (FullView.Labs.cs:115, FullView.Journey.cs:246, Controls.cs:479).
- **Engineer view** closes on minimize (FullView.cs:361) and frees its data and timers.
- **Overview** repaints only when its key changes (measured: 0 page-canvas paints in 20 s). The weather sentence is computed off the UI thread at most every 30 s.
- **ConnList** paints only visible rows. Owner-drawn controls are double-buffered: DB, PillButton, ConnList and StreamOverlay all set OptimizedDoubleBuffer / AllPaintingInWmPaint.
- **Memory bounds:**

  | Structure | Bound |
  |---|---|
  | Timeline | 2 h / 300 |
  | Once.suppress | 500 |
  | Watch.seen | 2,000 |
  | Explain.fileInfo | 400 |
  | Draw.measured | 3,000 |
  | GameStat | 900 |
  | GameSession / Calls | 14,400 |
  | Spikes | 50 |
  | PcHealth.snaps | 25 |
  | ioMin | 60 |
  | ConnList rows | dropped 3 s after gone |
  | AirLog | ≤ 40 APs × 24 h × 7 d |

  Act/Hint rectangles are cleared per paint (FullView.cs:744). The only structure that never shrinks is UpdateBreak.dayCache, at one small entry per calendar day, which is negligible.
- **Lock contention on `M.L`:** no network I/O is done while holding `M.L` (PingMs, DnsTime and netsh all run outside the lock), so the UI thread's short `lock (M.L)` reads can't stall behind a probe.
- **Network on the UI thread:** no DNS or ping on the UI thread found in the all-day paths. Speed-server and Travel lookups run in tasks.
- **Clicks only:** History.Load runs on the UI thread on clicks (FullView.Paint.cs:342, FullView.cs:665). History.Days() runs in paint at most every 30 s. Both are click or page costs, not all-day ones.

## Muse's items (round 2), checked

- **Journey creates undisposed `AdjustableArrowCap`s each paint:** correct. The same pattern is in FullView.Story.cs:90 and FullView.Travel.cs:125 and 249 (`pen.CustomEndCap = new AdjustableArrowCap(...)`; the Pen is disposed, the cap is not). These are paint-time only, left to the finalizer, with no all-day cost.
- **`jTimer` / `labTimer` never disposed:** true, but harmless. Both stop themselves on the first tick after the page is hidden or the form closes (FullView.Journey.cs:246, FullView.Labs.cs:115). A stopped WinForms Timer isn't rooted, so it is collected with the FullView. There is no leak and no wakeup.
- **Hourly UpdateBreak.Background isn't gated by History or listed:** correct that it runs every hour unconditionally (Brain.cs:108) and that Penny doesn't reduce it. It reads the System log (EvtQuery, 14 days, IDs 19 and 20001, up to 120 events rendered) and History day files.
  - **Wrong detail:** Muse says it "notifies on ≥3 update-related breaks". It does not notify. Background() only sets `ChangeDays` (week-heatmap dots, FullView.Insights.cs:89) and `UpdateBreak.Strong` (an Overview line, FullView.Overview.cs:349). There is no Alerts.Raise in UpdateBreak.cs.
- **DNS race up to ~10 min:** a user-started tool, not all-day. Not re-checked.

## Harness

`/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/review2/harnessP/P.cs` (built as P.exe; the app copy with its portable marker is in `harnessP/app/`). Modes: `draw`, `snap`, `measure`, `view <secs> <page>`, `overlay <secs>`, `monitor <secs>`. The `monitor` mode is not useful on Linux because the sampler throws at GetSystemTimes.
