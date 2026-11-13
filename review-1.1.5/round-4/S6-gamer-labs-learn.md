# Round 4, slice S6: Gamer, Labs, Learn games, calculators, picks, over-time (e80412e)

Reviewer: Claude (subagent S6). Every line of the 19 files in the slice was read. Harnesses are in `scratchpad/review4/h6/` and all run against `build4/r4.dll`:
- `H.exe`: the Calc front end.
- `vlsm.py`, `num.py`: random diffs against Python.
- `Q.exe` and `chk.py`: the quizzes, the jitter lab and the TCP lab.
- `G.exe`: game history load time, the match report, and the Valve list overriding your edits.
- `P.exe`: the probe plan while a game is played.

`G.exe` and `P.exe` run with `HOME` pointed at `h6/home`, so nothing touches a real data folder.

### 1. A match session records almost nothing for the server it is about. The report then says "loss 99-100%", and the overlay plots hours-old samples as the last minute
Severity: wrong result  
Where: src\Gamer\Gamer.cs line 380 (Plan: `p.Servers.Take(MaxServers)` at 1 s), line 339-340 (StartSession: `Server = Best(g)`), line 455 (Evidence: the sample counts only if it is younger than 3 s whenever `Cfg.GameFast`), line 742 (Report: `GameStat.Loss(gv)` counts NaN as lost); src\Ui\FullView.Gamer.cs line 255 (card rows `Take(MaxServers)`), line 219 (session line); src\Ui\StreamOverlay.cs line 68 (`Recent(60/step)` with step = 1)  
How to reproduce: Balanced preset (MaxServers 8). Watch Valorant (or any built-in "world" game) from a PC whose best region is India, Oceania or Middle East, which are entries 9-11. Play a match.
- `P.exe` with playing = valorant prints the plan: NA East … Southeast Asia at **1 s**, India / Oceania / Middle East at **300 s**.
- Any Valve game is worse: its list is 30+ relays in Valve's JSON order, and only the first 8 (16 on Gamer) are probed each second.
- The same thing happens without any region problem when you press "Start session" before the game runs, when the game's exe isn't detected, or when "start by itself" (GameAuto) is off. Playing is then null, so nothing is probed at 1 s, yet Evidence still demands a 3-second-fresh sample. That is about 3 s out of every 30 s background probe.

What happens / what should happen:
- **The session server.** The session's server is `Best(g)`, which is Oceania. Oceania is probed once every 5 minutes, so `Evidence()` writes `Game = NaN` in about 299 of 300 rows.
- **The summary and the report.** "Copy match report" and the THIS MATCH line both say `loss 99%`/`100%` with avg "-". No spike can ever be detected for that server, because it needs 20 samples in 15 min.
- **The lag graph for streaming.** It plots the last 60 survey samples (5 hours) as if they were the last 60 seconds.
- **The game card.** It shows only the first 8 rows, so the ★ best region isn't on the card at all, while its header reads "Best for you over 7 days: Oceania".
- **What should happen.**
  - Always include the session's server, and the best region, in the 1-second set.
  - Choose the 8 or 16 by best score, not by list order.
  - Judge "fresh" by the pace that server is really probed at.
  - Report "not measured" rather than counting unmeasured seconds as loss.

Confidence: confirmed for the plan (ran P.exe). The Evidence, Report and card consequences are from reading the exact lines above.

### 2. CS2 / Dota 2: servers you add, and "My match" from Find my match server, vanish as soon as they are saved
Severity: wrong result  
Where: src\Gamer\Gamer.cs line 89 (`foreach (var g in all.Where(x => x.Valve)) g.Servers = Valve.Cached() ?? g.Servers;`, which runs after ApplyFile); src\Ui\FullView.Gamer.cs line 163-168 (FindMatch: SaveGame + Reload, then "Added as \"My match\" and watched from now on"); src\Ui\GameEditor.cs line 112-113  
How to reproduce: `G.exe valve` writes a small `valve-servers.txt` (IAD, FRA) and a `games.txt` with `server|cs2|My match|found|203.0.113.9|-1|exact` and `server|cs2|LAN box|home|192.168.1.50|27015|exact`. It then reads `Games.All`. The cs2 servers come back as **IAD, FRA**: both of the user's servers are gone.  
What happens / what should happen: whenever a Valve list is cached, which is always after the first click on a Valve game, edits saved through Edit servers are silently replaced on the next `Reload()`. The same goes for the match server that Find my match server "added". The note says it is watched from now on, but it never is. Either merge the user's exact servers with Valve's list, or don't offer Edit / "Added" for Valve games.  
Confidence: confirmed (G.exe)

### 3. The match report never shows the router / Wi-Fi / traffic / CPU context for a spike
Severity: wrong result  
Where: src\Gamer\Gamer.cs line 748 (`var at = Around(z.T, 0).FirstOrDefault();`) and line 685 (`Math.Abs((r.T - t).TotalSeconds) <= secs`)  
How to reproduce: `G.exe report` builds a session with 300 one-second rows, each stamped 137 ms after the second, plus one spike 100 s ago.  
What happens / what should happen: the spike line is printed as `12:48:39  30 -> 120 ms  · your provider` with no "(router … ms, Wi-Fi …%, traffic …, CPU …%)". A window of 0 s only matches a row whose timestamp equals the spike's to the tick. The spike's time is taken on a probe task and the row's time on the loop thread, so they never match. Use the nearest row within about 1 s.  
Confidence: confirmed (G.exe output)

### 4. "Time lost waiting" was removed by accident: the row is inside a comment
Severity: wrong result  
Where: src\Checks\OverTime.cs line 99 (PeriodRows)  
How to reproduce: read line 99:
`double waits = p.BadMinutes + p.OutageUnrecorded;   // rough minutes, plus ... as rough if (waits > 0) rows.Add(R(3, "Time lost waiting", ...));`.
The UTF-16 literal "Time lost waiting" does not occur anywhere in r4.dll, while "Laggiest game" from the line above does.  
What happens / what should happen: the round-3 #4 fix (no double counting) put the `if` on the comment line, so the Month and Six-month reviews, and the saved HTML review, no longer have the "Time lost waiting" row. `OutageUnrecorded` and the `badAt` set are computed for nothing. Move the `if` to its own line.  
Confidence: confirmed (string absent from the compiled assembly)

### 5. Learn games still pay for answers they have revealed: escape room, quick-fire, and any timed game left before its end screen
Severity: wrong result  
Where: src\Ui\FullView.Games.cs:
- line 94 (time's up: "It was: … Fix: …"), with no MarkTried
- line 128 (`roomMiss >= 3 ? ""`), with no MarkTried
- line 233 and 268 (heist and outage mark tried only when the end screen is painted)
- line 333 (dungeon only on death)
- line 389 (quick-fire uses `EarnPts`, with no tried logic at all)

How to reproduce:
1. **Escape room.** Let the clock run out: the fault and the fix are printed. Press Next room until the same fault returns (NewRoom picks among unearned faults; with one left it is always that one). Click the shown answers: +4. The same works after escaping with 3 or more wrong guesses, which pays nothing and marks nothing.
2. **Heist / Survive the outage.** Each round shows the right answer and the reason. Play 9 of 10 (or 7 of 8), then click another Learn item or restart the app, so the end screen never paints. Play again with the answers known: +3.
3. **Dungeon.** Wrong answers cost hearts but you can leave with one heart left. Leave, come back, and Enter again: no tried mark.
4. **Port / Address quick-fire.** Every wrong pick shows "No - 443". Play again with no limit until 8 of 10.

What happens / what should happen: round-3 #9 added `EarnFirst` and `MarkTried` for thief and detective only. Each key still awards once, but it is earned after the answer was shown (rule 10 as the brief reads it). Call `MarkTried(key)` at the moment an answer is revealed: the first wrong pick, the time-out, or the first round of a heist or outage. Don't defer it to an end-of-game paint.  
Confidence: confirmed (read; every award and reveal path listed)  
Rule: 10

### 6. GameHistory's week verdict is quadratic: 0.37-0.5 s per game while holding the lock the Gamer page's paint waits on
Severity: slow  
Where: src\Gamer\Gamer.cs line 663 (Load: `grp.Average(...)` evaluated inside `days.Count(d => d.Where(evening).Any(x => ... grp.Average(...)))`), line 654 (the whole Load under `lock (k)`), line 645 (Week takes the same lock), line 650 (Warm loads each watched game in turn)  
How to reproduce: `G.exe load 11`: 7 days of history, one region every minute plus 10 surveyed every 5 minutes, today plain and older days gz.
- Load takes **511 ms** and 3 more take 1,423 ms, while the 7 `Read` calls alone take **74 ms**.
- With one server: Load 367 ms against Read 25 ms.

What happens / what should happen:
- **Why it is slow.** For each server, every evening row of a day with no trouble recomputes the average over all ~10,000 rows: about 25 M LINQ steps per server per week.
- **Where the cost lands.** Warm runs this for every watched game every 10 minutes on the GamePing thread. A Gamer page or overlay paint that calls `Best → Week` meanwhile blocks on `k`. With 5 games that is about 2 s of locked CPU per cycle.
- **The fix.** Compute the average once per group, and do the file reading and grouping outside the lock.

Confidence: confirmed (timed with G.exe on mono)  
Rule: 7

### 7. Bandwidth: "1.000 GB" is read as 1 GB, and a size in "Gb" is read as gigabytes
Severity: wrong result  
Where: src\Checks\Calc.cs line 186-191 (Num / Parse2) and line 196 (Size lowercases the unit)  
How to reproduce:
- `bw  1.000 GB  100` gives "80 s at the full 100 Mbps": 1 GB.
- `bw  4 Gb  1 Gbps` gives "32 s": 4 gigabytes, where 4 gigabits is 4 s.

What happens / what should happen:
- **Mixed conventions.** The parser accepts a decimal comma ("4,7 GB" = 4.7), so it follows European input. But the European thousands form "1.000 GB" (one thousand) silently becomes 1 GB, a factor of 1,000. Either refuse a dot followed by exactly 3 digits when there's no other separator, or follow the current culture.
- **Gb read as GB.** Speed rejects "Mb" and "Gb" as ambiguous, but Size lowercases "Gb" to "gb" = gigabytes, 8× off. Treat `Gb`/`Mb` (lowercase b) as bits in sizes, or refuse them.

Confidence: confirmed (H.exe)

### 8. Packet stack: the IPv6 fragment count ignores the 8-byte fragment header
Severity: wrong result (minor)  
Where: src\Checks\Calc.cs line 106-107 (Stack: `fragData = (innerMtu - ip) / 8 * 8`)  
How to reproduce: `stack  udp ipv6 ethernet  (MTU empty)  2900`.  
What happens / what should happen: it says "IP splits it into 2 fragments". Each IPv6 fragment also carries an 8-byte Fragment extension header, so it holds (1500 − 40 − 8) / 8 × 8 = 1,448 bytes, and 2,908 bytes need **3**. Payloads 2,889-2,904 (and similar ranges at every multiple) are miscounted. For IPv6, subtract 8 before rounding. The IPv4 boundary from round 2 (2,952 gives 2, 2,953 gives 3) is now right.  
Confidence: confirmed (H.exe)

### 9. "GB" in the month review, the six-month review and the data-cap forecast is decimal MB ÷ 1024
Severity: wrong result (minor)  
Where: src\Checks\OverTime.cs line 61 (`p.GBDown += r[13] / 1024`), line 260 (`Usage.Month(null) / 1024`); the columns come from src\Brain\Brain.cs line 49 (`mbDown += Mbps / 8`, decimal MB)  
How to reproduce: read. History's "MB down" is Mbps ÷ 8, so 10⁶ bytes. Dividing by 1,024 gives neither GB nor GiB.  
What happens / what should happen: totals read 2.3% low. On a 1,000 GB plan the cap forecast says "you stay under" until you are really at about 1,024 GB, and Calc says sizes are decimal "like ISPs". Divide by 1,000.  
Confidence: suspected (read; arithmetic)

### 10. Game history is never rolled off when History is off or when no game is watched
Severity: wrong result (minor, retention)  
Where: src\Gamer\Gamer.cs line 577 (`Minute` returns when `!Cfg.History`) and line 589 (Tidy is only called from Minute); src\History.cs line 66-68 (History.Tidy deletes only `yyyy-MM-dd*` names)  
How to reproduce: watch a game for a few days, then turn History off, or stop monitoring every game (the loop then idles and never calls Minute).  
What happens / what should happen: `history\games-*.csv(.gz)` stay forever, although Settings says game history is kept for "Keep game history for" days. Shortening that setting doesn't prune either until a game is watched again. Run GameHistory's roll-off from History.Tidy as well.  
Confidence: suspected (read)

### 11. Game probes block thread-pool threads, and a failed DNS lookup is retried 16 times a second
Severity: slow  
Where: src\Gamer\Gamer.cs line 441 (`Task.WaitAll(go.Select(p => Task.Factory.StartNew(() => ProbeOne(...))), 2500)`, up to 18 per second), line 510 (`Dns.GetHostAddresses` with no timeout, failures not cached), line 535 (sync `Ping.Send` 1 s) and line 544 (TCP 1.5 s)  
How to reproduce:
- **Slow servers.** On the Gamer preset, play a Valve game whose relays don't answer ICMP: 16 probes of 1 s every second.
- **DNS outage.** Lose DNS while playing an AWS-region game: 16 lookups of about 10 s each are started every second, because the "cached 5 minutes" comment applies only to successes.

What happens / what should happen:
- **Thread pool.** Each probe is a blocking call on a thread-pool thread. The pool starts at the core count and grows by about 2 threads a second, so probes queue behind each other.
- **Backlog.** `WaitAll` gives up at 2.5 s and the next pass queues another 16-18, so the backlog and thread count grow. Other thread-pool work in the app (Tidy, the Toolbox runners, BeginInvoke continuations) waits behind them.
- **The fix.** Use `SendAsync`/`ConnectAsync`, cache a failed lookup for a few seconds, and skip a server whose previous probe hasn't finished.

Confidence: suspected (read)  
Rule: 7

### 12. Bandwidth calculator: a size alone or a time alone gives a blank result, and common units are refused
Severity: cosmetic  
Where: src\Checks\Calc.cs line 165-183 (Bandwidth) and line 202 and 207 (unit lists)  
How to reproduce (H.exe):
- **Blank result.** `bw  50 GB` and `bw  (empty)  (empty)  2 h` return 0 rows: no message, no conversion.
- **Refused as invalid.** `10 minutes`, `30 seconds`, `1 minute` are refused ("type e.g. 90 s, 10 min or 2 h"), although `hours` is accepted. So are `100 Mb` (as an ISP writes a speed), `100 Kbit/s` and `1 Tbps`.

What happens / what should happen: say what a size or a time alone needs (a second value), and accept `minute(s)`, `second(s)`, `Kbit`, `Tbps`, and "Mb" as a speed.  
Confidence: confirmed (H.exe)

### 13. Gamer comments and on-screen notes describe limits that have changed
Severity: cosmetic  
Where:
- src\Gamer\Gamer.cs line 22 ("Caps: 8 servers at once, 1 probe a second each, 10 probes a second in total"; now 8 or 16 and MaxServers + 2 per second)
- line 342 and 688 ("timed every 5 s"; the code uses 15 s, line 443)
- line 568 and 579-586 (the "router ms" column of games-*.csv is always written empty, because `gw` is never assigned)
- src\Ui\GameEditor.cs line 53 ("8 are watched at once"; 16 on the Gamer preset)

How to reproduce: read; open Edit servers on the Gamer preset.  
What happens / what should happen: correct the text, and fill the router column or drop it.  
Confidence: confirmed (read)

### 14. Learn and console tools contact a game's servers without the Gamer Zone's consent click, unlike Wi-Fi vs cable
Severity: cosmetic (inconsistent consent)  
Where: src\Gamer\GamerTools.cs line 27 (Pick: any game by name, watched or not), line 50-52 (Gpn: trace and pings to it), line 191 and 200 (ConsoleCheck: `GamePing.Probe` every 5 s); compare src\Gamer\GameCompare.cs line 290 ("click it in the Gamer Zone first - that's your OK to contact its servers")  
How to reproduce: Toolbox › Would a GPN help me, or Console mode, with a game you never clicked, e.g. `fortnite`.  
What happens / what should happen: the game's region is traced, pinged or probed. The user did run the tool, so rule 1 holds, but the Gamer Zone's header promise ("nothing is sent to a game company's servers until the user clicks that game") and GameCompare's rule say otherwise. Apply the same check in both tools.  
Confidence: suspected (read)

### 15. Survive the outage: "A new personal best" on a tie, and on every repaint
Severity: cosmetic  
Where: src\Ui\FullView.Games.cs line 267-270  
How to reproduce: score exactly your previous best.  
What happens / what should happen: `best` is `outScore > Cfg.OutageBest`, but the line shown is `outScore >= Cfg.OutageBest`, so a tie is announced as a new best. Use the `best` flag computed before saving.  
Confidence: confirmed (read)

## Still present from earlier rounds

| Earlier id | Where now | Evidence |
|---|---|---|
| R2 #11 / R2-cc #113 (TCP lab tiny link) | src\Labs\LabSim.cs line 37, 43, 51 | Q.exe: 1 Mbps / 5 ms / 0.1×, 480 RTTs → **242 losses (CUBIC), 241 (Reno), util 1.000**. cwnd 2 → CA +1 → 3 > max(cap, 2) → loss → 2 …, so a loss every other RTT next to "100% of the link". The lab text says the extra "waits in the queue". |
| R3-new #9 (games pay for shown answers) | FullView.Games.cs line 94, 128, 233, 268, 389 | Fixed for thief and detective only. The escape room, quick-fire and abandoned timed games remain: see #5. |
| R3-new #14 (ping map labels overlap) | FullView.PingMap.cs line 69 | Now `(off++ % 2) * 12 * S`. Neighbours alternate by draw order, so clusters of 3+ (Valve: Sterling / Washington / NY, plus NA East / NA Central) still overlap. |
| R3-perf #2 (paint waits on Week's lock) | Gamer.cs line 645 and 654 | Load still reads and groups under `k`. Now quantified as quadratic in #6. |
| R2-cc #97 (UpdateBreak.Background every hour) | UpdateBreak.cs line 129 | `Changes(14)` (two event-log queries) still runs hourly with History off. Only `Strong` is gated on History. |
| R2-cc #110 (small undisposed objects) | FullView.PingMap.cs line 49 | `var old = g.Clip` (a new Region) on every Gamer paint is never disposed. |
| R2 #15 (TCP lab cost per frame) | FullView.Labs.cs line 237 | One `SolidBrush` per round (up to 480) on every 66 ms Play frame. Both simulations rerun each frame. |

## Checked and held up

- **VLSM.** 1,500 random blocks and sizes, diffed against Python `ipaddress`: every subnet, range, gateway, broadcast, usable / spare count, mask and the "Used / free from" row match (**0 mismatches**). The round-2 fit, /32 and octal bugs are fixed. So are the Unicode-digit throws (regex `[0-9]`).
- **Number converter.** 2,000 random decimal / 0x / …h / 0b / …b / dotted IPv4 / MAC / comma-grouped inputs give the right decimal, hex and padded binary. The only mismatch is the ambiguous `0b` (binary 0 with a b suffix). Large ulongs are handled.
- **Packet stack.** web MSS 1,460 (TLS no longer subtracted); WireGuard 1,440 / 1,420 (outer v6); WireGuard + PPPoE inner MTU 1,432; the IPv4 fragment boundary 2,952 → 2 and 2,953 → 3; efficiency uses 1,538 bytes on the wire.
- **Bandwidth.** `1,500 MB` = 1,500 and `4,7 GB` = 4.7. Speed 0 gives a message. `Mbit/s` and `MiB/s` are accepted. The 1,538-byte frame rate is right.
- **Subnetting quiz.** 5,000 seeds checked against `ipaddress`: every network, broadcast and usable-host answer is right, and there are always 4 distinct options containing the answer.
- **Address quick-fire.** 5,000 addresses, each in the category it is marked as: private, public, loopback, link-local, CGNAT, multicast, documentation. The port pool answers are unique and standard.
- **Other games.** The packet-detective "wrong packet", heist works/fails, dungeon and outage answers were all checked by hand and are right. Every game can be won.
- **TCP lab.** Reno after RFC 5681 (halve, timeout to 1). CUBIC after RFC 9438: K = ∛(Wmax·0.3/0.4), so the loss period of 188 RTT at 685 packets / 40 ms agrees with K ≈ 200 RTT. The Reno-friendly α = 3(1−β)/(1+β). The Reno sawtooth takes 343 RTTs to climb 342 → 685. The 100,000 cap and the timeout wait are now labelled.
- **Mathis lab.** MSS/RTT × 1.22/√p is correct. The lab's 1% random loss at 40 ms gives 3.6% utilisation, which agrees with Mathis' 4.75 Mbps. "Your line" needs ≥ 100 probes and ≥ 3 losses and is labelled a rough Reno bound.
- **Jitter lab.** σ = J / 1.128 gives a measured mean |D| of 30.6 for J = 30. A buffer of 2× the jitter gives 1.2% late for every J. The challenge is reachable in 21 of 630 settings.
- **Labs in general.** Every lab except route and quiz shows "A simulation with the numbers you pick". The NAT lab says "Possibly double NAT" and keeps hop positions, and a failed trace no longer awards. Full-screen and main-window refresh both work, TCP Play stops when you leave, and the QoS text is fixed.
- **Gamer scheduler.** MaxPerSecond = MaxServers + 2, so the Gamer preset reaches every played server. The loop idles at 5 s with nothing watched and uses Clock.Mono. TcpMs disposes its wait handle. Match path pings are every 15 s, off on Penny, and listed in About. Game history is compressed.
- **Picks, Guided, UpdateBreak.**
  - Guided.Lag treats a router that doesn't answer pings, or that isn't checked, as unknown.
  - Picks.ParseWhen reads "9" as 09:00 and never a future day.
  - UpdateBreak needs a real step (day before fine, day after worse), skips Defender, and filters USB, HID and storage drivers.
- **Six-month review.** It reads once with per-month sums, runs off the UI thread (Settings queues Tidy), triggers on the history's span, and its HTML is encoded and redacted. The download-time size parser handles "1,000 MB". The month "this" comparison is per day.
- **Learn and journey.** Check yourself marks tried even with Points off. Starting a new journey or story stops a running capture. LastRealCwnd is cleared on each capture, and the busiest connection is chosen. The play head and translation repaint 4 times a second.
- **Stream overlay.** Fonts are made once, and the PNG is re-encoded only when the key changes and written only when its bytes change.
- **Game editor.** "Pick from running programs" now reads names before Dispose, quoted exe paths are cleaned, and id collisions get -2, -3 and so on.
