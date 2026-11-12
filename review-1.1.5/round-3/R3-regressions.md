# Round 3: regressions introduced by the four round-2 fix commits

Scope: every hunk of a20c019, 89cc23f, d5fa6c6 and aec65f5, read as a PR review, at 8502674 (dev-1.1.5). I looked for bugs the
changes introduce and for neighbouring code they break. Whether each round-2 finding is fixed is a separate check, done by another agent.
Repro harnesses are in `scratchpad/review3/harnessR3/` (a.cs, rtp.cs, pts.cs, pol.cs, max.cs, rep/). Each one loads the mono build of 8502674 (`r3.exe`) by reflection.

---

### 1. --report says "ok" (exit 0) when a step couldn't run: the downgrade check can never fire
Severity: wrong result
Where: src\Report.cs line 36 (Report.Run). Introduced by 89cc23f.
How to reproduce: `AdeeboMeter.exe --report out.json --seconds 3` on a PC where Windows' connectivity test can't run (no proxy access, DNS to msftconnecttest blocked). Ran under mono in `harnessR3/rep/`.
What happens: out.json holds `"6. The way in": "Windows' connectivity test couldn't run (NameResolutionFailure)", level 3, evidence "m"`, yet `"verdict": "ok", "exit_code": 0` and the finding says "...names and the way in all work". The new condition `o[0] == "u" && o[1] == "3"` only matches evidence "u". The "whats" flow (Investigate.Whats.cs) never records "u" evidence: every Obs there is "m", "w" or "i". The check is dead code.
What should happen: a step that couldn't run downgrades a healthy verdict to 3. Mark those steps (for example "6. The way in couldn't run") with "u" in Whats, or match them by name.
Confidence: confirmed (ran `--report` with the r3 build; output quoted above).
Also: the self-test added in the same commit is titled "round-2 core fixes: ... --report verdict", but its body never tests the report verdict, so it passes without checking this.

### 2. Live capture call quality: an RTP numbering restart hides all later loss (shows 0.0%)
Severity: wrong result
Where: src\Capture\LiveCapture.cs lines 263-268 (AddRtp). Introduced by d5fa6c6.
How to reproduce: `harnessR3/rtp.cs`. One SSRC sends 5,000 packets with seq 40000-44999, then restarts at seq 100 and sends 1,000 more with every second one lost.
What happens: `BaseExt = seq - Count` = 100 - 5000 = -4900. Two lines later, `if (r.BaseExt < 0) r.BaseExt = ext` resets it to 100, so "expected" covers only the packets after the restart while Count holds all 5,500 → loss is clamped to **0.0%** (real: 8.3%, and 50% since the restart). The same happens for any restart to a number below the packet count so far (restart at 4000 → 0.0%; at 6000 → 8.3%, correct). RTP senders start from a random number, and a 10-minute call at 50 packets a second is 30,000 packets, so about half of real restarts fall into this case. The approach also forgets any loss from before the restart.
What should happen: count loss per numbering run. Keep the expected and received counts from before the restart, then start a new base at the restart (expected += old run; BaseExt = ext).
Confidence: confirmed (ran rtp.cs against r3: "restart at seq 100: ... => shown loss 0.0%").

### 3. Points cap of 600 is below what can really be earned (751): honest points are silently thrown away
Severity: wrong result
Where: src\Points.cs line 87 (Total, `Max = 600`). The cap came in a20c019 with the comment "about 585 ... a file saying more was edited by hand". Learn topics and Games added later (b312fd1, 8502674) made it worse. The same 600 is in .github/scripts/hof_verify.py MAX_POINTS.
How to reproduce: `harnessR3/max.cs` adds up Points.Value over every awardable key: tools 158 → 300, replay 15, 59 Learn topics → 118, 59 checks → 118, labs 44, quiz 8, journey 3, pages 24, paths 30, 27 games → 91. That is **751**.
What happens: everyone who does more than about 80% of the items is stuck at 600. Later awards add nothing ("+3" flashes but the total doesn't move). The ranks text ("500 means you've done nearly everything") and the Max comment are both wrong now. Even at aec65f5, before the games, the total was already about 608 with all 46 checks.
What should happen: derive Max from PtAll() (or raise it, with the HOF check updated), and add a self-test that fails when the earnable total exceeds Points.Max.
Confidence: confirmed (ran max.cs: "everything there is to earn: 751   Points.Max = 600").
Rule: 10

### 4. The Check yourself "tried" marker is skipped while Points is off, so a question can still be farmed
Severity: wrong result
Where: src\Ui\FullView.Learn.cs line 129 (PaintCheck: `else if (Cfg.Points) Points.Me.MarkTried(...)`). Introduced by a20c019.
How to reproduce: Settings › Adeebo Points off. Open a Learn topic and pick a wrong answer. You now see the right answer, and nothing is saved. Restart AdeeboMeter, turn Points on, open the topic, pick the right answer.
What happens: +2 for a question that was answered wrong the first time. The fix only remembers wrong answers while earning is on.
What should happen: record the "tried." marker whatever the switch says (it is worth 0 and isn't counted), or remember any revealed answer.
Confidence: suspected (read it; the only MarkTried call is guarded by Cfg.Points, and checkPick lives only in memory).
Rule: 10

### 5. Portable copies unzipped under Downloads or the Desktop silently stop being portable
Severity: wrong result
Where: src\Program.cs lines 90-96 (InDownloads). Introduced by aec65f5.
How to reproduce: extract the release zip with Explorer's "Extract all" (default: `Downloads\AdeeboMeter-1.1.5\`), or put it in `Desktop\AdeeboMeter\`, and add `AdeeboMeter.portable` beside the exe. Run it.
What happens: InDownloads now matches any folder *inside* Downloads, and the whole Desktop tree. Then `Portable` is false and `WriteDir` = %LOCALAPPDATA%, so settings, points.txt, history, certs and the key list all go into the installed copy's data. There is no notice. This is the "never mixes with the installed one" case the portable marker exists for, and BRIEF.md tells reviewers to use it. A copy someone already ran from a Desktop or Downloads subfolder loses its settings, points and history on upgrade (the files stay in `data\`, unread).
What should happen: keep the subfolder rule for *untrusted settings* if wanted, but still honour the marker, or say on screen why it's ignored. At the least, don't move existing data without migrating it.
Confidence: suspected (read it; on mono the `d + "\\"` test never matches Linux paths, so I couldn't run it. Checked with port.cs: the Linux run ignores it).

### 6. A huge AdeeboMeter.policy is now read whole into memory (1.2 GB for a 300 MB file)
Severity: slow (crash once the line is big enough to run out of memory)
Where: src\Policy.cs lines 43-45 (Load). Introduced by d5fa6c6. Before, a file over 16 KB was ignored.
How to reproduce: `harnessR3/pol.cs`. AdeeboMeter.policy next to the exe = one 300 MB comment line followed by `online_lookups = off`.
What happens: `File.ReadLines(f).Take(2000)` caps the number of lines, not their length. Load takes 1.4 s and the peak working set reaches **1,243 MB**. An OutOfMemoryException isn't in the catch list (IO, Unauthorized and Security only), so a big enough line kills start-up. A portable or downloaded copy's own folder is user-writable.
What should happen: cap the bytes read (for example the first 64 KB), as well as the lines.
Confidence: confirmed (ran pol.cs: "Load took 1424 ms; peak working set 1243 MB"). The big file was deleted afterwards.
Rule: 7

### 7. Unicode digits make the new IPv4 parsers throw (Calc V4, Destination check's leading-zero message)
Severity: wrong result (an error row, or a pin silently not added; logged, no crash)
Where: src\Checks\Calc.cs V4 (a20c019): `\d` plus `int.Parse(..., Invariant)`. src\Checks\DestCheck.cs line 90 (aec65f5): `int.Parse(o)` inside the error message.
How to reproduce: `harnessR3/a.cs`: `Calc.Vlsm("١٠.0.0.0/24","10")`, `Calc.Number("١.2.3.4")`, `DestCheck.Parse("01.1.1.١")`.
What happens: all three throw FormatException. `\d` in .NET matches any Unicode Nd digit, but Invariant int.Parse only takes ASCII. DestCheck.Parse runs on the UI thread from the pin box and from Settings load (pin= lines). The old code (IPAddress.TryParse, no int.Parse) didn't throw.
What should happen: use `[0-9]` or RegexOptions.ECMAScript in these regexes.
Confidence: confirmed (ran a.cs: "THREW FormatException" ×3; `01.2.3.4` correctly gets the leading-zero message).

### 8. Inspection check: roots in Windows' own Root store that aren't on Mozilla's list are now called "interception"
Severity: wrong result
Where: src\Checks\Inspection.cs IsWellKnown (thumbprint list). Introduced by aec65f5.
How to reproduce: Check a destination (or the HTTP inspector) on a Microsoft endpoint that chains to Microsoft's private roots, for example `fe2.update.microsoft.com` (Microsoft Update Secure Server CA → Microsoft Root Certificate Authority 2011, thumbprint 8F43288A…). Those roots ship in the machine **Root** store, not AuthRoot.
What happens: OnPublicList finds the root in Root and not in AuthRoot → IsWellKnown → the thumbprint isn't in the Mozilla set (Microsoft Root CA 2010 and 2011 aren't) → `Inspected = "Microsoft Root Certificate Authority 2011"`, shown as "someone opens your HTTPS". The old O= name match had "Microsoft" in its list.
What should happen: add the roots Windows ships in its own Root store (Microsoft Root Authority, Microsoft Root CA 2010/2011, Microsoft ECC/RSA Product roots) to the thumbprint set.
Confidence: suspected (read it; needs Windows to run).

### 9. Loss lab "your line right now" counts probes from earlier networks
Severity: wrong result
Where: src\Monitor.cs line 263 (ProbeLoop, netChanged block). NetTries and NetLostN came in with a20c019.
How to reproduce: run the internet check on hotel Wi-Fi (losing pings) for an hour, switch to the home network, open Learn › Labs › Loss.
What happens: the netChanged block clears NetPing and Loss but not `NetTries`/`NetLostN`. The field comment says "since the network last changed". The lab therefore shows the old network's loss and a Mathis limit for "your line" that belongs to another network. `icmpMiss` (aec65f5) isn't reset either, so on the new network a single missed ping already triggers the TCP-443 fallback instead of after three.
What should happen: reset NetTries, NetLostN and icmpMiss with the rest of the internet state on a network change.
Confidence: confirmed by reading (only the "check switched off" line resets them; grep of every writer).

### 10. Replay bonus refused after a reboot when you replay at a similar uptime
Severity: wrong result (the bonus comes later, not lost)
Where: src\Points.cs line 129 (Award, Tick check). Introduced by aec65f5.
How to reproduce: `harnessR3/pts.cs`. MTR first run 2 min after boot on Monday (Tick = 120000). Tuesday, after a new boot, replay 5 min after boot.
What happens: returns 0, and again at 9 min of uptime. It pays only at 13 min. `bt >= e.Tick` can't tell a new boot from the same session, so a replay a day later is refused until uptime passes Tick + 10 min.
What should happen: also store the boot time (UtcNow - TickCount) and only apply the uptime check when it is the same boot.
Confidence: confirmed (ran pts.cs: "Tue (new boot...) replay 5 min after boot -> 0").
Rule: 10

### 11. Host names for IPs now refresh only on three tabs; other features that use them see stale or empty names
Severity: wrong result
Where: src\Ui\FullView.cs line 576 (RefreshConns). Introduced by aec65f5.
How to reproduce: open the view straight on What's wrong › Calls (or Gamer › journey, Toolbox › system tools) without visiting Connections, Network story or Apps.
What happens: `M.RefreshIpNames()` now runs only when tab is conn, travel or procs. Monitor.IpNames is also read by M365.Connections (Investigate.cs:160: Teams TCP-443 fallback detection by host role), Monitor.ProbeConns and Connections(). There it stays empty or as old as the last visit to those tabs.
What should happen: refresh names when any consumer needs them (or every 60 s on other tabs), not only where they are painted.
Confidence: suspected (read it).

### 12. DNS second opinion under `online_lookups = off` flags normal CDN rotation as "hosts file / stale cache / VPN rule"
Severity: wrong result (false amber)
Where: src\Checks\DestCheck.cs lines 175-184 (CompareDns, Policy.NoOnline branch). Introduced by aec65f5.
How to reproduce: company PC with `online_lookups = off`; Check a destination on a big round-robin name (outlook.office.com, a CDN host).
What happens: it asks the PC's own DNS server again. Any answer set without overlap (common: rotating subsets, short TTLs) → level 1, "this PC got a different answer than its DNS server gives now - the hosts file, a stale cache or a VPN name rule". The public-resolver branch says "usually a nearby CDN server" for the same situation.
What should happen: same wording and level as the public branch, or treat no overlap as amber only when the PC's answer is 0.0.0.0, 127.x or a private address.
Confidence: suspected (read it).

### 13. Leftovers from removing Find devices, and comments and text that no longer match the code
Severity: cosmetic
Where and what (all from aec65f5 unless noted):
- src\Checks\Picks.cs line 269 (Inventory row text): "Find devices or a Ping sweep fills it in." The tool no longer exists.
- docs\THEORY-OF-EVERYTHING.md line 245: "**Devices:** ... find devices (one mDNS, SSDP and WS-Discovery question each, answers for 3 seconds)". The same commit edited other lines of this file.
- src\Monitor.cs line 270: "Sleep until the next job is due (2 s with the view open; up to 10 s in Penny mode)". On Penny the pace is now min(30, 300, 120, 30, IfEvery 60) = 30 s.
- src\Ui\FullView.Settings.cs line 376 (About › What leaves your PC): "One ping ... and one name lookup, every N". The lookup is now at most once a minute; the Settings row was updated, About wasn't.
- src\Link\AirLog.cs Record comment (a20c019): "an old log goes within the hour (and at start-up)". Nothing calls AirLog.Clear/Record at start-up.
- src\Points.cs `Max = 600 // about 585` (a20c019): see #3.
- src\SelfTest.cs "round-2 core fixes: ... --report verdict" (89cc23f): no report assertion in the body (see #1).
- src\Checks\Reach.cs: Order, SameNet, DnsQ and Ptrs may now be dead code after Find was removed (harmless).
Confidence: confirmed (grep / read).

### 14. Award card: a long name is cut with "…" on the picture but not in the claim
Severity: cosmetic
Where: src\HallOfFame.cs Fit (a20c019).
How to reproduce: a 32-character name of wide letters ("WWWW…") or CJK. The card draws "WWWWWWWWWWWWWWWWWWWWW…". The site says "Type the name ... exactly as they are on the card". Someone retyping from the picture gets a mismatch (clean_name drops "…"). Fit can also cut a surrogate pair in half.
Confidence: suspected (read it).

---

## Self-test (mono, 8502674) against round 2's mono run

- Same 17 environment failures (mono: iphlpapi, GetSystemTimes, DestroyIcon, path semantics, memory 103 MB, down from 114) plus **1 new FAIL**: "Learn games, the QR code and the over-time tools" (NotImplementedException). That test came with b312fd1, not these four commits; it needs a Windows run to tell a mono gap from a real bug.
- New PASS: "round-2 core fixes ..." and "round-2 batch ...". All earlier PASS lines are still PASS.
- **Changed meaning**: "settings round trip + presets" used to assert `penny < balanced < engineer`; it now asserts `balanced == engineer == gamer`. The printed load went from "penny 18, balanced 36, engineer 74" to "penny 8, balanced **71**, engineer 71, gamer 71". The default background work doubled: router every 2 s, internet every 15 s, process list and connection table every 5 s with the view closed. The CHANGELOG and docs record this as the owner's choice, so it's not filed as a bug. Rule 7 reviewers should know the default now does Engineer's work.
- The `Cfg.Read("gw","999")` → `GwSec != 2` assertion can't tell "rejected, back to default" from "ignored", because the default equals the Balanced value set just before. It was just as weak before (5/5), so it's not new.
- The "--report verdict" title covers no assertion (see #1).

## Checked and held up

- **Find devices removal (aec65f5)**: tool, examples.txt `== finddev`, terms.txt `tb.finddev`, lang-fr line, LabGate, Senders, NoEarn, Synonyms (mdns, bonjour, upnp), the tool-time table, TbLayout, ToolHome card ("inventory" swapped in), LIVE-TESTS, index.html and the Traffic Lab row in THEORY are all gone. A script checks every Synonyms, TbLayout, LabGate, SenderGate, Senders and LabExempt id against the 158 tools: no dangling ids. Every tool has an example and a `tb.` term, with no orphans (only "page.gamer" in examples, which is intended). No `tool:finddev` Learn links remain. Points.Known("finddev") is now false, so an old points.txt line counts 0 (no farming).
- SenderGate (89cc23f): twamp, udpmtu, radiuslab, smtppath and dnsrace get the tick and Lab.Allowed, are in NoEarn, and the self-test cross-checks Senders. The policy (NoLab) now also switches these off, which only switches off (rule 6 OK).
- Points.Known / Total locking: lock(k) → lock(known) → PtKeyKnown → static data only. Nothing takes them in the other order, so no deadlock. "tried." markers aren't counted (Known false).
- HallOfFame.CleanName against hof_verify.clean_name on already-clean names: both keep L*/Mn/Mc/Nd and `.-_'` and single spaces, NFC first, so they give the same result on what the app copies.
- hall-of-fame.yml retry loop: a rebase conflict resets to origin, re-runs the verify and recommits. An "added" result that fails to push gets its own comment.
- Toolkit.Open (89cc23f): it alternates IPv6 and IPv4 with a time share each, and every caller checks `tc == null`. An abandoned connect is closed. The task exception goes unobserved but is harmless on .NET 4.5+.
- ReplaceFile (89cc23f): the old file is moved to .bak, not deleted, and put back if the move of the new one fails.
- The SNMPv3 key cache lives on the per-run Cred object (every tool calls Cred.Parse per run). Nothing is kept beyond the walk (rule 4 OK).
- FileCrypt parts.txt (aec65f5): only `.<guid>.part` names are swept, a tampered list can't delete other files, and parts.txt holds paths only (no secrets).
- Twamp `run.SeriesUnit` swallowed into a comment in 89cc23f was fixed again in d5fa6c6.
- Monitor IPv6-only gateway (aec65f5): Gateway stays IPv4 (the router ping skips ""), Snap uses Gateway6 for "offline", PrimaryMtu falls back to 0.
- Named presets re-applied on load (aec65f5): every interval setter calls Custom(), and Policy.Apply runs after the preset, so the policy still wins (rule 6 OK).
- AirLog.Overlap block maths for 40/80/160 MHz on 5 and 6 GHz is correct for 36-64, 100-128 and 149-177.
- Calc: VLSM boundary keeping, 1 host = /30, binary only with 0b or b, the thousands-comma parse (but #7 for Unicode digits).
- No new listening sockets, no admin calls, no "Nokia" in any of the four diffs (the self-test "no company reference" passes).
