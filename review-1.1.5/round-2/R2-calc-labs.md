# Round 2: calculators, picks, guided questions, packet journey, Labs (a0fbd56)

Reviewer: Claude (subagent, calc and labs section). Everything marked "confirmed" was run against the mono build of a0fbd56 (`build2/r2.dll`) through reflection harnesses in `scratchpad/review2/harnessL/`:
- `H.exe`: line-driven Calc front end
- `vlsm_ref.py`, `num_ref.py`, `bw_ref.py`: Python reference diffs, using `ipaddress` for VLSM
- `L.exe` / `L2.exe`: LabSim, covering TCP, the quiz, CwndFromCapture, ParseWhen and the jitter lab
- `J.exe`: Journey Kind / Translate / Explain
- `lagrun/G.exe`: Guided.Lag on a synthetic Monitor

**About Muse's report** (`review-1.1.5/round-2/muse.md`):
- **"Calc (VLSM bounds) verified correct": wrong.** A 600-case random diff against Python found 11 wrong allocations, all caused by the same bug (#1). There is also a /32 gateway bug (#2) and octal or hex address parsing (#3).
- **"LabSim, Reno RFC 5681 / CUBIC RFC 9438, verified correct": mostly right for the core formulas.** Beta, K, W_est, ssthresh on 3 duplicate ACKs and on a timeout all match (see "Checked and held up"). It is wrong about the parts around the core: #7 (jitter lab model), #8 (capture window estimate), #11 and #12.
- **ParseWhen: right in substance, wrong in example.** Muse's example "25:00 is tomorrow 01:00" is false: the regex branch returns false for "25:00" (I ran it). The real defect is bare numbers: "9" becomes 9 days in the future (#10).
- I don't repeat the hotel ReadToEnd cap, the "eight picks" count or dnsrace / Lab-gate findings. They are Muse's.

---

### 1. VLSM: one subnet that doesn't fit makes every later subnet "doesn't fit" too
Severity: wrong result
Where: src\Checks\Calc.cs line 129-130 (Vlsm)
How to reproduce: VLSM planner, Block `10.0.1.0/24`, Hosts `500, 100`. Or `192.168.1.0/24` with `300, 50, 50`.
What happens: every subnet shows "doesn't fit - the block is full" and "Used 0 of 256 addresses". The 100-host subnet should land on 10.0.1.0/25.

The cause is that `at` is rounded up to the big subnet's boundary (`at += size - at % size`) *before* the fit check. The `continue` then leaves `at` past the end of the block, so all smaller subnets fail too.

What should happen: align into a local variable, and only commit `at` when the subnet fits.
Confidence: confirmed. 600 random cases diffed against Python `ipaddress` gave 11 mismatches. Modelling this exact bug in the reference made all 11 disappear (0 left), so there is no other VLSM mismatch.

### 2. VLSM: a 1-host subnet becomes a /32 whose "gateway" is outside it, and is another subnet's address
Severity: wrong result
Where: src\Checks\Calc.cs line 126 and 134 (Vlsm)
How to reproduce: `10.0.0.0/24` with `1, 1, 1`.
What happens: the result reads "Subnet 1 10.0.0.0/32 · gateway 10.0.0.1", "Subnet 2 10.0.0.1/32 · gateway 10.0.0.2", and so on. Each gateway is outside its own /32, and it is the next subnet's only address.

What should happen: one of these.
- Size 1 host as a /30, like the 2-host rule on line 127.
- Show no gateway for /32 (and /31). Per RFC 3021 a /31 has 2 usable addresses and no broadcast.

Also note: a block of /31 or /32 is rejected outright ("type an IPv4 block"), while /32 subnets are handed out inside larger blocks.
Confidence: confirmed (H.exe).

### 3. IPv4 inputs with a leading zero are read as octal, and "0x0A" as hex
Severity: wrong result
Where: src\Checks\Calc.cs line 115 (Vlsm) and line 220 (ParseNum). Both use `IPAddress.TryParse`.
How to reproduce:
- VLSM block `010.0.0.0/8` is planned as **8.0.0.0/8**.
- `0x0A.0.0.0/8` is accepted as 10.0.0.0.
- The number converter turns `010.1.1.1` into 8.1.1.1 (decimal 134283521).

What should happen: reject leading zeros or read them as decimal. Users copy padded addresses like "010.001.002.003" from Cisco and other configs. .NET Framework's `IPAddress.Parse` follows inet_aton, so the same thing happens on Windows.
Confidence: confirmed (mono). Behaviour on .NET Framework is suspected to be the same, since it is documented inet_aton parsing.

### 4. Bandwidth: a thousands separator is read as a decimal point (1,000 GB becomes 1 GB)
Severity: wrong result
Where: src\Checks\Calc.cs line 170-171 (Num regex and Parse2)
How to reproduce: Size `1,500 MB` with speed `100 Mbps` gives "Time 120 ms" (expected 2 min). `1,000 GB` at `1 Gbps` gives "8 s" (expected 2.2 h).
What happens / what should happen: the size is off by a factor of 1000. The Packet stack payload box and the number converter both strip "," as thousands, so the calculators disagree with each other. Treat `,` followed by exactly 3 digits as thousands, or follow the current culture.
Confidence: confirmed (H.exe).

### 5. Number converter: decimal numbers made only of 0s and 1s, 9 or more digits long, are silently read as binary
Severity: wrong result
Where: src\Checks\Calc.cs line 225 (ParseNum, `^[01]{9,64}$` is checked before decimal)
How to reproduce: type `100000000` (one hundred million) and you get 256. `1000000000` gives 512. `101010101` gives 341.
What should happen: only treat input as binary with `0b` or a `b` suffix, or show both readings. Round decimal numbers are exactly what users type. ("Read as: binary" is shown, but it's easy to miss.)
Confidence: confirmed.

### 6. Packet stack: the TLS record overhead is taken off every packet, so the "web" MSS reads 1,438 instead of 1,460
Severity: wrong result
Where: src\Checks\Calc.cs line 30 and 84-92 (Stack). `tls` is kind "p" and is summed into `pay` per packet.
How to reproduce: Packet stack, layers `web`.
What happens: "Biggest segment (MSS) ~1,438 bytes - What TCP should advertise ... 1500 - 20 IP - 42 above". Efficiency is understated too (93.5%).

What should happen: TLS's 22 bytes are per record (up to 16 KB, across many segments), as the layer's own note says. TCP's MSS is 1500 − 20 − 20 = 1460 whatever runs inside it. Advising a 1,438-byte MSS clamp is wrong. Exclude TLS from MSS and MTU arithmetic; at most mention it as roughly 0.1% overhead per 16 KB record. (QUIC is per packet, so it is correct as it stands.)
Confidence: confirmed.

### 7. Jitter-buffer lab: the model makes calls unusable at ordinary jitter, and the challenge works in only 2 of 168 settings
Severity: wrong result (teaching)
Where:
- src\Labs\LabSim.cs line 87-100 (Arrivals and Jitter)
- src\Ui\FullView.Labs.cs line 166-172 (LabGoalMet "jitter")

How to reproduce: run LabSim.Jitter over every stepper combination with jitter 30, 50 or 80 (L2.exe).
What happens:
- **Call score.** Every late packet counts as lost. With "jitter" modelled as an exponential with that mean, the 60 ms default buffer at 30 ms jitter gives a call score of **1.73**, and 80 ms jitter caps at 1.96 even with a 200 ms buffer.
- **The challenge.** "With 30 ms of jitter or more, reach a call score of 4.0" can be reached only with jitter 30, buffer 200, network delay 10 or 30, and loss 0. Every 50 or 80 ms jitter setting and every other delay stays below 4.0.

This teaches that calls fail at 30 ms of jitter, which contradicts the usual guidance that a buffer of about 2× the jitter is fine. The challenge is close to impossible to find.

What should happen: use a distribution whose spread matches the "jitter" label, for example RFC 3550-style mean deviation. Count late packets with concealment, not as full loss. Check that the challenge can be reached across the range.
Confidence: confirmed (MOS table from L2.exe).

### 8. "Your real download": mixing two TCP connections gives a window of about 685,000 packets
Severity: wrong result
Where: src\Labs\LabSim.cs line 153-160 (CwndFromCapture). `hiSeq` and `hiAck` are global across every TCP record.
How to reproduce:
1. Make a capture with more than one TCP connection to the host. This happens whenever the journey flow has `LocalPort = 0`: JFlowFromStory (FullView.Journey.cs line 47-48) sets it when the story's connection isn't in connSnap. The BPF is then "host X and port 443", which catches all of a browser's parallel connections.
2. Feed it two flows with different ISNs (L2.exe).

What happens: the per-RTT estimate jumps from 2 to **684,887** packets. The lab clamps it to the top of the graph and labels it "your real download (estimated)".
What should happen: key on the 4-tuple and use only the busiest connection, or refuse when there is more than one.

Also:
- LastRealCwnd is static and is never cleared. A capture with no handshake leaves the previous capture's line in place.
- The link "See how TCP's window grew" is offered for "web" captures too.

Confidence: confirmed for the computation. The trigger in the real UI is suspected (read).

### 9. "Why is my game lagging?" blames your home network when the router simply doesn't answer pings, or the router ping is off
Severity: wrong result
Where: src\Checks\Guided.cs line 109 and 126 (Lag). `lvHome = double.IsNaN(gA) ? 2 : ...`
How to reproduce: Guided.Lag on a Monitor where NetPing is 30 × 20 ms with no loss and GwPing is either empty or all NaN.

GwPing is empty when Settings has the router check off (`Cfg.GwSec == 0` means no probes). It is all NaN when the router drops ICMP, which is common and is why Monitor keeps `GwEverReplied`.

What happens: "Verdict: Your home network - the lag starts before the router" (on Wi-Fi: "Your Wi-Fi ... move closer"), "lost 30 of 30", while the internet leg is clean.
What should happen: if the internet answers, the home leg can't be the cause. Treat "no gateway data" (or `!GwEverReplied`) as unknown (lv 3), never 2. The same goes for `lvIsp` when `NetPing` is empty because `Policy.NoInternetCheck` is set: that blames "Your provider".
Confidence: confirmed (lagrun/G.exe, both scenarios).

### 10. "What changed when it broke?": a bare number is read as a number of days in the future
Severity: wrong result
Where: src\Checks\Picks.cs line 63 (ParseWhen, TimeSpan branch)
How to reproduce: When = `9` gives 2026-11-21 00:00 (today + 9 days). `99` gives 2027-02-19. `yesterday 9` gives today + 8 days. `3.5` gives 5 March.
What happens: the timeline is gathered around a future date and comes back empty or misleading. (Muse's "25:00" example does *not* reproduce: the regex branch returns false for it.)
What should happen: refuse a TimeSpan with Days > 0, or read a bare 1-2 digit number as an hour. Apply the same "not in the future" rule as the DateTime branch.
Confidence: confirmed (L2.exe).

### 11. TCP lab: with a tiny link the window can never go below 2, so every round trip shows a loss
Severity: cosmetic (teaching)
Where: src\Labs\LabSim.cs line 23, 37 and 43 (Bdp is at least 1, cap = bdp × (1 + buf), ss is at least 2)
How to reproduce: TCP lab, Link 1 Mbps, Round trip 5 ms, Buffer 0.1× BDP. Also 5 Mbps / 10 ms / 0.1 gives 126 losses in 480 rounds.
What happens:
- 480 losses in 480 rounds (a loss dot on every round trip). cwnd decays 10 → 7 → 4.9 → … → 2 and stays above cap = 1.1.
- "Throughput 100% of the link" is reported next to "Losses 480".

What should happen: let the window fall to 1, or show that the BDP is below one packet. Don't report 100% utilisation alongside constant loss.
Confidence: confirmed (L2.exe).

### 12. TCP lab: the window is capped at 100,000 packets with no label, and a timeout is shown without its wait
Severity: cosmetic (teaching)
Where: src\Labs\LabSim.cs line 59 and 45
How to reproduce: 1000 Mbps, 300 ms, buffer 8× gives max cwnd = 100,000.0 exactly, zero losses in 480 rounds, and a flat line below the "buffer full" line.

The 100,000 cap acts like an unlabelled receive window. Also, a "hard loss" restarts slow start on the very next round trip. RFC 6298 RTO is at least 1 s (Windows uses a 300 ms minimum), so a real timeout idles for several round trips first. The lab says a timeout is "much more costly" but draws it as almost as cheap as the halving.
Confidence: confirmed (the cap). The RTO point is from reading against RFC 6298.

### 13. Loss lab: one lost ping out of 20 tells you to quote "at most 2 Mbps" to your ISP
Severity: wrong result
Where: src\Ui\FullView.Labs.cs line 290 and 307 (LabMathis, "YOUR LINE RIGHT NOW")
How to reproduce: one ICMP probe lost out of the last 20 means `M.Loss.Avg(20)` = 5%. At 30 ms, `MathisMbps(30, 5)` = **2.12 Mbps** (ran it). The row says "... Mbps - quote this in the ISP report if it's below your plan" (lv 1).

What should happen: there are three problems.
- 20 probes give 5% resolution.
- ICMP drops (rate limiting) are not TCP loss.
- Mathis is the Reno bound, while Windows uses CUBIC (which the TCP lab says itself).

Require a minimum sample and loss count before giving the number, label it a rough Reno bound, and don't advise quoting it.
Confidence: confirmed for the arithmetic. The UI path is from reading.

### 14. Full-screen lab window: changes made in the main window's lab stop repainting
Severity: cosmetic
Where: src\Ui\FullView.Labs.cs line 41 (LabRefresh: "full screen: only that window repaints")
How to reproduce:
1. Open any lab, press "Full screen", go back to the main window (the full-screen window is modeless).
2. Press − or + or a Choice in the main window's lab (same or another lab).

What happens: the value changes, but only `labHost` (the full-screen canvas) is invalidated, so the main window looks dead. The TCP Play timer also refreshes only the full-screen window, so a TCP play started in the main window freezes there.
What should happen: invalidate the control that owns the clicked Act (pass `h`), or both.
Confidence: suspected (read).

### 15. TCP lab Play keeps repainting the Learn pane after you leave the lab, and pauses for good when you return
Severity: slow
Where: src\Ui\FullView.Labs.cs line 115 (LabPlay timer tick)
How to reproduce: TCP lab, Length 480, Play, then click another Learn topic.
What happens:
- The tick only checks `learnPage.Visible`, not `learnSel == "lab:tcp"`. So for up to 480 × 66 ms ≈ 32 s, it invalidates the whole learnInfo 15 times a second, whatever topic is shown (rule 7: repaints when nothing changed).
- If you leave the Learn page instead, the timer stops but `labShown` stays ≥ 0. Coming back, the graph is frozen mid-play, with only "Show all" offered.

Each frame also re-runs both simulations, allocates up to 480 `SolidBrush`es for the phase shading (one per round, line 235), plus the LINQ arrays.

On Penny: neither Labs nor Journey reads `Cfg.Preset`. The 66 ms and 33 ms intervals are the same on every preset (grep shows no Preset reference in either file).

What should happen: stop when `learnSel` isn't the TCP lab. Use one brush per phase colour. Optionally use a slower frame rate on Penny.
Confidence: suspected (read). The Penny point is confirmed by grep.

### 16. Packet journey: "In plain words" and the timeline play head freeze while the replay plays
Severity: cosmetic (feature doesn't work as described)
Where: src\Ui\FullView.Journey.cs line 253 (JStep invalidates only `jLane`) and 258-265 (JLaneOnly paints only the lane)
How to reproduce: capture a conversation, press Play.
What happens: only the lane strip repaints. PaintJTranslate (`upto = FindLastIndex(T <= jPlay)`) and the PaintJTimeline play head are only redrawn on a full paint. That happens on pause, or when the app list refresh invalidates the whole canvas, so the "running translation" (file header line 18) jumps occasionally instead of following the packets.
What should happen: also invalidate the translation block and the timeline bar each step. Both are small.
Confidence: suspected (read; there is no capture driver under mono).

### 17. NAT lab: an ISP's private first hop is reported as "Double NAT"
Severity: wrong result
Where: src\Ui\FullView.Labs.cs line 437-441 (CGNAT check)
How to reproduce: trace from a line whose provider's first router uses RFC 1918 addresses (common on cable and fibre aggregation).
What happens:
- "Double NAT: ... a modem-router in front of your own router. Put the modem in bridge mode". A private address on the next hop is a router address, not proof of NAT.
- Hops that didn't answer are removed *before* `Skip(1)`. If your own router doesn't send time-exceeded, the provider's first hop is skipped as "the router", so the wrong hops are judged.

What should happen: say it's "possibly" double NAT, check the WAN side differently (the gateway's own public address versus the address the Speed test or STUN reports), and keep hop positions.
Also: the challenge is met when `labCg != ""`, so "Couldn't trace: …" and "No hop answered" award the +2 (line 175).
Confidence: suspected (read).

### 18. Packet stack: the UDP fragment count is wrong at the boundary
Severity: wrong result (minor)
Where: src\Checks\Calc.cs line 103 (Stack)
How to reproduce: `udp ipv4 ethernet` with payload `2952`.
What happens: "IP splits it into 3 fragments".
What should happen: 2. The UDP header rides only in the first fragment and each fragment carries 1,480 bytes, so n = ceil((payload + 8) / 1480) = 2960 / 1480 = 2.
Confidence: confirmed.

### 19. Bandwidth: some valid inputs give an empty result
Severity: cosmetic
Where: src\Checks\Calc.cs line 154-167 (Bandwidth)
How to reproduce: Size `4.7 GB` with Speed `0`, or with Time `0`, or Speed `0 Mbps` alone.
What happens:
- The result area is blank (0 rows, no message).
- `1 bps` shows "= 0 bytes/s".
- Sub-millisecond times show "0 ms": 51 of 500 random diffs, all display rounding. The maths was right in all 500.
- About 310 or more digits throw OverflowException. The Toolbox runner catches it as "Couldn't finish: Value was either too large…", so it's not a crash. .NET Framework's `double.Parse` throws the same way.
- Common units are rejected: `Mbit/s` and `MiB/s` give "type e.g. 100 Mbps", although sizes accept MiB.

What should happen: a "speed or time must be above 0" row, more decimals for small values, and TryParse.
Confidence: confirmed.

### 20. QoS lab: an unsupported claim that Teams and Zoom mark audio as EF
Severity: cosmetic (teaching)
Where: src\Ui\FullView.Labs.cs line 544
What happens / what should happen: "Teams and Zoom mark their audio as EF (DSCP 46)".
- Zoom's default audio marking is DSCP 56, not EF (video 34).
- The Teams desktop client on Windows marks only when QoS is turned on by Group Policy or the admin setting, with EF recommended.

Suggested wording: "Teams (when QoS is turned on) marks audio EF (46)".
Confidence: suspected (from vendor documentation, as I remember it).

### 21. Check yourself: a wrong answer shows the right one, and it can be answered after a restart
Severity: cosmetic (points)
Where: src\Ui\FullView.Learn.cs line 106-135 (PaintCheck)
What happens: the comment says "Right first time -> +2, once". In fact a wrong pick reveals the right answer, and after a restart the same item can be answered and earns +2. It is still awarded once, so rule 10's "once" holds, but "right first time" does not.
Confidence: suspected (read).

---

## Checked and held up
- **TCP sim vs RFC 5681 / RFC 9438** (L.exe; defaults 100 Mbps, 40 ms, 1× BDP, BDP 342.5, cap 685):
  - IW10.
  - Slow start doubles and stops at ssthresh.
  - 3 duplicate ACKs: Reno halves (364.5 → 182.3), CUBIC keeps 0.7 (540.1 → 378.0).
  - Timeout: cwnd = 1, ssthresh = max(2, β·cwnd) (Reno 98.1, CUBIC 288.5), then slow start up to ssthresh.
  - CUBIC uses C = 0.4, β = 0.7, K = ∛(Wmax(1−β)/C), α = 3(1−β)/(1+β) for the Reno-friendly W_est, and time-based growth in seconds.
  - The first CA step after a loss matches the hand calculation (482.6).
- **The "240 round trips" claim holds at the defaults.** CUBIC losses come at rounds 7, 195 and 388 (K ≈ 200 RTTs), so one full concave-to-convex climb back to 685 fits in 240. Reno's next loss is at 351, outside 240, which is correct. At 1000 Mbps / 5 ms CUBIC stays in the Reno-friendly region (1 loss in 480), which is real behaviour.
- **Subnetting quiz:** all 65,536 seeds checked in Python. The answer is right, there are always 4 distinct options, and the answer appears once.
- **Number converter:** 800 random decimal, hex, 0x, h-suffix, 0b and dotted-quad values. Decimal, hex and binary are all exact (64-bit included, through the negative-long trick). The 2^64 and 17-hex-digit overflows, `0x`, `0b`, `h`, `-5`, Arabic-Indic and full-width digits are all rejected cleanly. Mask contiguity is right (255.0.255.0 gets no mask row; 255.255.255.255 is /32).
- **Bandwidth:** 500 random size × speed cases across B/kB/MB/GB/TB/KiB–TiB and bps/kbps/Mbps/Gbps/MB/s/kB/s/GB/s/B/s are within 6% of the reference, and every miss is display rounding below 1 ms. Bits versus bytes and 1000 versus 1024 are handled correctly. Negative, NaN and Infinity are rejected.
- **Packet stack headers match textbook values:** Ethernet 18 (1518 frame, 1522 with VLAN), IPv4 20, IPv6 40, TCP 20, UDP 8, PPPoE 8 (MSS 1452), WireGuard 60/80 (MSS 1400 / inner MTU 1440), VXLAN 50 (inner 1450), efficiency 94.9% for TCP/IPv4/Ethernet including preamble and IFG. The MTU range 576–9216 is enforced. `x9` repeats are capped, so `ipv4 x9 ×12` gives 85 rows, bounded.
- **VLSM, apart from #1 and #2:** prefix choice, alignment, no overlap, staying inside the parent, sort by size (stable on ties), /0 blocks, 64 × /8 without overflow, hosts 0 / 2^31 / >16,777,214 rejected, more than 64 subnets rejected, non-IPv4 and IPv6 rejected.
- **Journey Kind / Translate / Explain:** with 0, 1, 3, 5, 2,000 and 20,000 records, shuffled and negative timestamps, TCP/UDP, public and local. No exception, at most 35 ms; the "local" rule's Skip-scan is not quadratic in practice.
- **CwndFromCapture:** a 1e9-second gap with rttS 0.0001 stops at 2,001 bins in 0 ms (the cap works).
- **JStep silence skip:** reasoned through for empty, one-packet, out-of-order and past-the-end cases. It stops cleanly, and `end` = 0 with no packets ends the play.
- **Journey capture:** dumpcap `-s 128`, `-c`, `-a duration`, `Strip()` (agrees with Muse).
- **Other lab challenges are all reachable:** Mathis (150 ms with 0.001% gives about 30 Mbps), bufferbloat (smart queue gives 22 ms), game (128 Hz, 240 fps, 3 ms gives 50.9 ms), QoS (100 Mbps gives 6 ms), airtime and MTU.
- **Lab formulas:** Mathis constant 1.22 = √(3/2); airtime anomaly 600 vs 6 Mbps, so the slow device holds 98% of the air (the "100 times" text is right); 64 Hz = 15.6 ms; DSCP names and ToS = DSCP << 2; VLAN range 1–4094.
- **Check-yourself data:** 46 knowledge checks, all well-formed, and the answers read as correct.
- **Guided.Lag** gives a sensible verdict when every leg has data (self-test path).
- **Toolbox runner:** exceptions from calculators are caught at FullView.Toolbox.cs line 694, so no calculator input crashes the app.
