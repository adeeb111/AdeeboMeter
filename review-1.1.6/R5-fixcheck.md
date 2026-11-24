**Release verdict: BLOCKER.** Rule 8 is broken in the repository: `HANDOVER-SEO.md` line 7 names the forbidden vendor, and `PLAN-1.1.5.md` line 558 names its router OS. Both go into the source zip (`release.ps1` line 85, `git archive HEAD`). Each is a one-line fix. No other High item is still present. Round-4 #9 (Hall of Fame handle) is only partly fixed.

# R5 fix check: round-4 findings at 999a100 (dev-1.1.6, 1.1.6)

- **Code:** /home/user/AdeeboMeter-dev at 999a100, read only. Round-4 findings were made at e80412e. Most round-4 fixes are in commit 10d81d3 ("round-4 review fixes (151 findings)"). Each fix was re-checked at 999a100, not at 10d81d3.
- **Build:** the mono build of 999a100 (build6/r6.dll, r6.exe). For the memory investigation, a copy of r6 was rebuilt with only `SelfTest.T()` patched to log RSS before and after each check (review5/fix/b7, the same embedded resources).
- **Harnesses:** the round-4 harnesses were copied to review5/fix/h and re-pointed at r6.dll. Four needed changes:
  - hS2/N.cs: now applies the new `LinkInfo.NormBand`, as `Collect` does.
  - s7/m.cs: now calls `MatchesScored`, because `Matches` has a new overload.
  - h1/mood2.cs: new, because `Mood.downSince` became a property fed by `Track`.
  - h4/ht.cs: new HTTPS driver with `Cfg.Portal = false`.
- **Re-run:** 75 of the 95 confirmed round-4 findings were re-run. The other 20 are text, docs, or a harness that copied old logic (S5h/isp.cs). Those were read.

## Counts

| Status | All 137 | security (15) | wrong result (87) | slow (10) | cosmetic (25) |
|---|---|---|---|---|---|
| FIXED | 126 | 14 | 81 | 10 | 21 |
| PARTIAL | 9 | 1 (#9) | 5 (#33 #35 #55 #60 #101) | 0 | 3 (#114 #116 #136) |
| STILL PRESENT | 2 | 0 | 1 (#32) | 0 | 1 (#135) |
| N/A (code removed) | 0 | | | | |

New bugs: 4. They are listed at the end in the brief's format. N1 is a regression from the #35 fix, and N2 is a new location for the rule-8 break.

## Self-test and --report vs round 4

**Run:** `xvfb-run -a mono AdeeboMeter.exe --selftest` from a fresh portable dir (review5/fix/st and st7).

**Result:** 100 checks, 26-27 FAILED (101 with `--samples`). Round 4 ran 70 checks with 18 FAILED. Every failure except the two GDI+ ones (see the memory section) is a mono or Linux artifact, and the same kinds as round 4:
- P/Invoke: iphlpapi, GetSystemTimes, DestroyIcon, GetCurrentProcess, OpenProcess, GlobalMemoryStatusEx, GetSystemPowerStatus.
- Backslash paths: `..\..\Windows\evil.exe -> /tmp/evil.exe`, "versioned folders".
- `Setup.Under`.
- The ECDH stub: "phone remote crypto" catches only TypeLoadException, but the stub throws NotImplementedException.

New failures since round 4, all mono artifacts:
- review 22 Nov (iphlpapi).
- sitting, tool pictures, UI wave 3, never slow (DestroyIcon).
- GDI / user handles: it now asserts, which is the #101 fix, so on mono it reaches GetCurrentProcess and fails.
- The two GDI+ failures, which are mono font artifacts (see below).

Self-test titles that promise more than the body checks:
- **"version numbers agree"** compares `Program.Version` with `Program.FileVersion` only. It never compares the store manifest (filled from Program.cs by pack.ps1, so that one is fine), CHANGELOG, README or the site.
- **"memory after everything"** fails only above **120 MB**, and prints "80 MB in normal use". The site and docs say "80 MB budget" (docs/index.html line 1014, THEORY lines 153 and 289, HOW-TO-USE lines 76 and 94). The owner's rule 7 says 64 MB. This is the rest of #101.
- **"Adeebo Points 1.1.5: … a challenge per lab …"** still checks no lab (#101).
- **"Ctrl+Alt+A shortcut (a note about this PC, never a failure)"** has no try/catch, so any exception fails it. Under mono it fails with ArgumentNullException.
- **Fixed since round 4:** "GDI / user handles" now asserts a ceiling and that the counts were read, and "no leaks" fails when `gdi0 == 0` on Windows.

**--report** (`--report out.json --seconds 65`, crossing a minute boundary):
- No `data\` folder was written. Round 4 wrote `history\…-events.csv` (#19 fixed).
- Verdict "warning", exit 1. The finding is consistent with the verdict: "…but Windows' own web test couldn't connect (NameResolutionFailure)" (#26 fixed). Round 4 gave "unknown" / exit 3 with "Nothing is wrong … the way in all work".
- "app": "AdeeboMeter 1.1.6".

## Memory: 2,046 MB in "memory after everything" and the GDI+ OutOfMemory in "render chip"

**Verdict:** a mono / libgdiplus artifact, not a regression in AdeeboMeter's own allocations. It does show one small real weakness (N4).

**1. Per-check RSS.** The patched self-test logs RSS around every T(). All checks stay at 68-88 MB until **"speed widgets and full checkup"**, which goes from **RSS 87 to 3,928 MB (+3,841 MB) in 2.8 s, while the managed heap stays at 14 MB**. So the memory is native (GDI+ bitmaps), not .NET objects. RSS then settles at about 2,000 MB and stays there to the end, so "memory after everything" reads 1,226-2,046 MB depending on the run. The external 0.2 s sampler of the unpatched r6.exe shows the same step: 87 MB, then 455, 920, 1,277 … 2,282 MB at 16.9-18.3 s, then 1,201 MB.

**2. The cause.** fix/mem/pv.exe calls `Draw.Preview` per style:
- speed strip and card: 264×196, normal.
- "everyday", "gamer", "engineer" and "auto" cards: **264×6,415 px**.
- "speedstatus", "speedapps" and "calls" cards each add about 480-1,400 MB and then throw "Generic Error" in `Graphics.Clear`, because the bitmap requested is hundreds of thousands of pixels tall.

The card height comes from `Draw.Measure(...)`. fix/mem/ms.exe shows `Draw.Measure("All good", fHead, 240)` returning **Height = 1,386,816 px** some of the time, and 14 px at other times.

**3. Reproduced outside AdeeboMeter.** fix/mem/pf.exe loads Barlow-Bold.ttf, which is embedded in 1.1.6 (round 4 had no embedded fonts), into a `PrivateFontCollection` and calls `MeasureString("All good", font, 240)` 8 times. One answer was **Height = 26,022**; the others were 14-15. DejaVu Sans (a system font) always gives 17.

This is libgdiplus returning garbage metrics for private fonts, not .NET Framework GDI+. "render chip / card / detail" fails with OutOfMemory for the same reason: a bitmap 1.4 M px tall. It is flaky, and passed in one of three runs.

**4. The real part (N4).** The 8 widget renderers (Theme.cs lines 680, 735, 801, 852; SpeedWidget.cs lines 79, 120):
- create the `Bitmap` outside any `using` / try, so a throw while drawing leaks it until a finalizer runs. That is why RSS stays at about 2 GB after `GC.Collect`.
- size the card height from text measurement with no upper bound.

On Windows the measurements are sane, so this is hardening, not a blocker.

## Release gates

**Versions:** consistent.
- `Program.Version` 1.1.6 and `FileVersion` 1.1.6.0.
- store/Package.appxmanifest has `Version="__VERSION__"`, filled by store/pack.ps1 from Program.cs.
- CHANGELOG has "## 1.1.6 (2026-11-23)" and "## 1.1.5 (2026-11-17)", both dated, no "unreleased".
- docs/index.html has `softwareVersion` "1.1.6".
- The report JSON says "AdeeboMeter 1.1.6".

**.gitignore:** OK.
- It covers `__pycache__/`, `*.pyc`, `dist/`, `data/`, `*.sha256`, `AdeeboMeter.portable` and `AdeeboMeter.policy`. `git check-ignore --no-index` confirms all of them.
- No .pyc is tracked: the old one was removed in 10d81d3.
- Note: `data/` also ignores any future `docs/data/` folder.

**build.yml:** OK.
- It triggers on push to main, store and `dev-*`, on release, and on workflow_dispatch with a tag.
- Push builds (dev branches included) still build, self-test and **upload artifacts**: `dist/*` and the single exe.
- `HOF_KEY` is passed only when `event == release`, or on workflow_dispatch with a tag (which checks out the tag). Dev builds are unofficial and their cards carry no claim code (#5 fixed).
- The deep test runs for releases only.

**Rule 8:** BREAKS. `git grep -i nokia` (excluding the review folders) finds one hit: **HANDOVER-SEO.md line 7**, in a file added 17 Nov. **PLAN-1.1.5.md line 558** names the vendor's router OS ("SR OS"), which the self-test itself treats as forbidden in the exe. Both files are tracked and published through `git archive`. The original round-4 line (PLAN-1.1.5.md line 8) was reworded.

**Docs vs code:**

| Claim | Docs say | Code says | Result |
|---|---|---|---|
| Tool count | 232 (README, CHANGELOG, site, THEORY) | 232 (`Tools2().Count` from s8/max.exe) | OK |
| Learn topics | 98 | 98 | OK |
| Self-test checks | "93 checks" (README.md line 86, docs/index.html line 1030); "104 checks" (docs/THEORY-OF-EVERYTHING.md line 281) | 100 T() checks (101 with `--samples`) | Wrong in all three (N3) |
| Memory budget | 80 MB (site, THEORY, HOW-TO-USE) | self-test allows 120 MB; owner's rule is 64 MB | Wrong (#101) |
| Points | "about 1,000" (hall-of-fame.html) | 1,094 earnable, Max 1,200 | Close enough; the in-app text is stale (#114) |

## All 137 round-4 findings at 999a100

(C) or (S) after each title is round 4's confidence (confirmed or suspected). In the evidence column, "ran" means the original repro, or a stated variant, was re-run against r6.dll.

| # | title | status | where @999a100 | evidence |
|---|---|---|---|---|
| 1 | IT policy switches that don't switch their tools off: Traffic Lab's iperf3 / bufferbloat / DSCP / Wake-on-L... (C) | FIXED | Toolkit.cs LabOff() in Iperf/DscpPing/Loaded/Wake; FullView.Overview.cs FindClosestSpeed Policy.NoSpeed; Toolkit.Revocation gate | ran: s7/lab.exe with NoLab=true -> 0 connections, row 'turned off by your IT team'; speed/revocation read |
| 2 | A hostile iperf3 server makes AdeeboMeter open thousands of TCP connections (8,470 in 20 s) (C) | FIXED | Toolkit.cs line 298 IperfOne (CREATE_STREAMS once) | ran: h3/iperfsrv.py + h5.exe -> 'the server asked for its streams twice - stopped', 94 ms (r4: 8,470 sockets) |
| 3 | WhyFails.Clock() sends HTTP to msftconnecttest.com and runs w32tm with the Sign-in page check switched off,... (C) | FIXED | WhyFails.cs line 214 Clock(bool asked) | ran: ht.exe HTTPS on expired h4:8446 with Portal off -> 'Clock: not compared online - the sign-in page check is off' |
| 4 | HTTP request builder shows and keeps a URL's user:password (C) | FIXED | Reach.cs line 83 NoUser; FullView.Toolbox.cs line 891 shown[0] | ran: h3/h2.exe -> row 'GET http://127.0.0.1:18080/api' (no admin:s3cret) |
| 5 | Every push to main, store or dev-* builds an "official" exe with the Hall of Fame key and publishes it as a... (S) | FIXED | .github/workflows/build.yml HOF_KEY expression | read: HOF_KEY only for release / tagged dispatch; push builds still upload artifacts but unofficial |
| 6 | In Serious mode the ARP-spoofing pop-up never appears (C) | FIXED | Brain/Watch.cs line 42 Alerts.OnEvent | ran: h1/misc.exe -> ALERT for both voices |
| 7 | speedservers.txt: the upload host is never checked against private addresses, and the download host is chec... (C) | FIXED | SpeedTest.cs BindIPEndPointDelegate + upload-host check (line 215) | read; SafeUrl('http://127.0.0.1.nip.io/') still True but the address is now checked at connect time and for Up (not run: no internet) |
| 8 | TWAMP-light, MTU over UDP and the ping tools skip the "one device only" guard: up to 1,000 packets to a mul... (C) | FIXED | Protocols.cs lines 405/490 (Twamp), UdpMtu.cs line 32, Icmp.cs line 118 Resolve4 | ran: h3/h1.exe Twamp 224.0.0.1 refused; Resolve4(4294967295 / 010.0.0.1 / 0x7f.1) -> null |
| 9 | Hall of Fame: the social handle isn't covered by the claim code, so whoever submits a shared card first att... (C) | PARTIAL | .github/scripts/hof_verify.py lines 178-200 | ran (fix/hof/t.py): thief's X handle still published on first submit (site x, handle thief_handle); owner's later submit -> 'review', link blanked + disputed. GitHub handle now must equal ISSUE_AUTHOR. Non-GitHub handles still not bound to the card |
| 10 | `--deeptest --local` connects to company domain controllers and to the internet (S) | FIXED | FullView.DeepTest.cs line 103 netNoInput, ByHand (dc, friend) | read: dc in ByHand; ipv6score/hotel/tonight/prematch/vpn/dupip skipped with --local (still a skip-list, not an allow-list) |
| 11 | "Check a PFX" (and the inspection test) build the chain with X509Chain, which downloads missing intermediat... (S) | FIXED | CertTools.cs Pfx -> CertChain.Ladder | read: no X509Chain.Build on PFX |
| 12 | Match / Live / Recipients read any path, including \\server\share UNC paths and files of any size, which Ce... (S) | FIXED | CertKeys.cs Load/Recipients -> CertTools.Input | read: UNC refused, 1 MB cap, inside try |
| 13 | Crash notes keep every folder name after the first space, and the note goes into a GitHub issue URL (C) | FIXED | Brain/Watch.cs line 285 Crash.Clean | ran: hS2/C.exe -> '<folder>\board.adeebo', '<folder>\list.csv', OneDrive path -> '<folder>\keys.adeebokey' |
| 14 | PKCS7 path: a "-----BEGIN PKCS7-----" block holding a passwordless PFX imports a private key (S) | FIXED | CertTools.cs P7b content-type check | read |
| 15 | A .pyc is committed, and .gitignore misses the build, portable-mode and release outputs (C) | FIXED | .gitignore | ran: git check-ignore --no-index: dist/, data/, *.sha256, __pycache__/x.pyc, .portable all ignored; git ls-files has no .pyc |
| 16 | A fresh install, or "Just run it from here", always opens in Pro mode: the Simple start never shows (C) | FIXED | AppCtx.cs LoadSettings (keys==0 -> first) | ran: h1/mode.exe autostart=1 only -> Mode ''; variant autostart+lang=fr -> 'pro' (an upgrade; Setup.cs writes only autostart) |
| 17 | "Time lost waiting" was removed by accident: the row is inside a // comment (C) | FIXED | Checks/OverTime.cs PeriodRows | ran: UTF-16 'Time lost waiting' present in r6.dll |
| 18 | "Can I close it?" tells people svchost.exe is OK to close (C) | FIXED | knowledge/processes.txt svchost close: 2; Explain.cs CloseText[1] | read (msiexec wording now 'usually OK - see what you lose below') |
| 19 | `--report` writes into the user's real history and data files and runs the ISP trace, while its header says... (C) | FIXED | Report.Active guards in History.cs, Brain.cs, Monitor.LearnIsp | ran: --report --seconds 65 in a fresh portable dir -> no data/ folder written (r4 wrote events.csv) |
| 20 | Export settings → Import settings silently drops the voice (mood), certificate revocation, the buddy, its h... (C) | FIXED | Program.cs Cfg.Known | ran: s7/rt.exe 'Export->Import drops:' empty; h1/misc.exe same |
| 21 | A match session records almost nothing for the server it is about, then reports "loss 99-100%", and the str... (C) | FIXED | Gamer.cs Plan/Ranked/PaceOf, Evidence pace | read; P.exe plan unchanged only because the harness has no history (no best region). Report still counts NaN rows as loss if a session server is ever unprobed |
| 22 | Learn games still pay for answers they have already shown: escape room, quick-fire, and any timed game left... (C) | FIXED | FullView.Games.cs Revealed/EarnFirst/NewRun | read: every reveal path marks tried at reveal time |
| 23 | A name that cleans to nothing makes an award card that can never be claimed (C) | FIXED | FullView.Points.cs MakeAwardCard loop | ran verifier: empty / emoji-only name -> mismatch/missing; app side read (dialog re-asks) |
| 24 | With the internet check off (Settings or policy), every Teams call ends with "lost 100% of internet checks"... (C) | FIXED | Brain/Watch.cs Calls (checking flag, mins>=1) | ran: hS2/K.exe -> 'Your 1-minute call ... had a clean network the whole way', level 0 (wording still implies the internet was checked) |
| 25 | Time machine paints the internet red ("no answer") for every minute the internet check didn't run (S) | FIXED | FullView.Replay.cs lanLv/netLv = 3 when NaN | read |
| 26 | "Nothing is wrong ... and the way in all work" when the way-in test couldn't run or is switched off: in Wha... (C) | FIXED | Investigate.Whats.cs portalErr branch; Report.cs Json finding | ran: S5h/inv.exe s3 -> level 1 'Windows own web test couldn't connect'; s1 portal off -> 'and names all work'; --report JSON finding matches verdict |
| 27 | HTTPS check shows a green "A good padlock" when the handshake fails after the certificate (mTLS, client-cer... (C) | FIXED | CertChain.cs Https verdict (proto == "" branch) | ran: ht.exe localhost:8444 mTLS -> level 2 (no green padlock). Under mono the earlier chainBad branch wins ('Windows doesn't trust this chain'): mono SslStream artifact |
| 28 | ISP report: a game server that ignores ping is reported as 100% loss and blamed on a provider hand-off, and... (C) | FIXED | IspReport.cs SAME TIME rows: last answering hop, StartOfLoss | read (S5h/isp.exe embeds a copy of the old lines, so not re-runnable) |
| 29 | ISP report blames the home network for loss inside the provider when the router isn't the first answering hop (C) | FIXED | IspReport.cs lines 96-108 notHome / vpnOn | read: silent router and 192.168.0.x router -> first non-192.168 hop is the provider; VPN gets its own finding |
| 30 | ISP report: the cable-modem page reads and the hop name lookups aren't in "What leaves your PC" (C) | FIXED | FullView.Settings.cs LeaveIsp | read: PTR lookups and the cable-modem page listed |
| 31 | The provider trace is repeated up to four times per network and again on every switch back, while About say... (S) | FIXED | Monitor.cs LearnIsp ispKnown | read: once per network per session, 2 retries only if no public router answered |
| 32 | Privacy texts miss the new revocation lookups and the files written automatically, and the Hall of Fame kee... (C) | STILL PRESENT | docs/privacy.html; docs/hall-of-fame.html line 211; hof_verify.py line 207 | grep: privacy.html has no revocation/OCSP/CRL and no Roast/letter files; HoF page still 'nothing else about you is kept' while the JSON stores by (login), ref (issue URL) and now disputed |
| 33 | PLAN-1.1.5.md names the forbidden company (C) | PARTIAL | PLAN-1.1.5.md line 8 fixed; PLAN-1.1.5.md line 558; HANDOVER-SEO.md line 7 | git grep: line 8 reworded, but HANDOVER-SEO.md:7 names the vendor and PLAN-1.1.5.md:558 names its router OS - both in the source zip (release.ps1 git archive HEAD). See N2 |
| 34 | Policy values other than off/no/false/0 are silently ignored, so IT believes a switch is off (C) | FIXED | Policy.cs Parse | ran: h1/pol.exe disabled / "off" / 'key: off' / OFF / disable all switch off; 'off;' logs 'changes nothing' |
| 35 | Your connection on Russian, Japanese, Chinese, Korean, Arabic and Turkish Windows says "nobody - clean air"... (C) | PARTIAL | Link/LinkInfo.cs KeyWords, RateKind, NormBand | ran: hS2/N.exe (patched for NormBand) ru2/ja2/tr2/ko/ar -> 'Sharing your channel: 3 (3 strong)', LeastCrowded 11. Left: ar Receive/Transmit rate '-' (label 'معدل الاستلام (ميجابت/ث)' unmatched), ko cipher '-', and a NEW Turkish own-signal 0% (N1) |
| 36 | A decimal-comma band ("2,4 GHz") switches off all 2.4 GHz logic for German, French, Spanish, Italian, Portu... (C) | FIXED | LinkInfo.cs NormBand; AirLog uses WlanApi band | ran: N.exe de/fr/it/pl/pt -> band '2.4 GHz', 3 sharing, LeastCrowded 11/49/169 |
| 37 | Non-English Windows: the location block is never noticed ("Nothing to tweak - your Wi-Fi is set up well"), ... (S) | FIXED | LinkInfo.cs (m.WifiBlocked); SecChecks.cs WifiSec via LinkInfo.Fields + localized Open words | read |
| 38 | "Your Wi-Fi dropped N times in the last day" counts your own disconnects on non-English Windows (S) | FIXED | LinkInfo.cs EvLog.ByUser (13 languages) | read (still words, not the numeric ReasonCode) |
| 39 | MTR / ISP report: a destination that answered the trace and then stopped answering is called "No real loss" (C) | FIXED | PathTools.cs DestDark/MtrVerdict | ran: S5h/mtr.exe P6 -> 'The destination stopped answering during the run'; P25/P26 same tail |
| 40 | Steady low loss on every hop (under 3%) is blamed on the destination rate-limiting pings (C) | FIXED | PathTools.cs MtrVerdict little-loss rule | ran: P21 'A little loss (about 2.5%) from hop 1', P22 'about 1.7% from hop 2' |
| 41 | MTR: two routers at the tail that ignore the probes make an earlier rate-limited hop "real loss" (round-2 #... (C) | FIXED | PathTools.cs Answers/DestDark | ran: P15/P16 -> LossStart -1, 'No real loss' |
| 42 | ISP message says "my router answering normally" when the loss starts at the router, and the copied message ... (C) | FIXED | IspReport.cs Message routerFine; GameSession.Redact | ran: isp.exe M1 -> 'This was measured on a cable.' (no 'router answering normally') |
| 43 | CS2 / Dota 2: servers you add, and "My match" from Find my match server, vanish as soon as they are saved (C) | FIXED | Gamer.cs Games.All Valve merge | ran: h6/G.exe valve -> My match, LAN box, IAD, FRA |
| 44 | The match report never shows the router / Wi-Fi / traffic / CPU context for a spike (C) | FIXED | Gamer.cs Report Around(z.T,1) nearest | ran: G.exe report -> '(router 3.0 ms, Wi-Fi 70%, traffic ..., CPU 10%)' |
| 45 | Chain check: a cross-signed copy of a trusted root is preferred over the trusted root itself, so a valid ch... (C) | FIXED | CertChain.cs Walk anchor same name+key | ran: h4/t.exe chain chain-cross.pem -> 'Every link holds', root Test Root CA |
| 46 | Chain check: a root that isn't in the local stores is reported as a missing *middle* certificate, including... (C) | FIXED | CertChain.cs rootMissing | ran: empty store srv-chain.pem -> level 1 '? root not on this PC', no 'middle missing' |
| 47 | HTTPS check calls any site signed by a private root "Your company is inspecting HTTPS", including intranet ... (S) | FIXED | CertChain.cs publicSite | read: private root on a non-public/intranet host -> level 3 'Private root' |
| 48 | The chain ladder doesn't check signature hash, key size, key usage or path length, so an RSA-1024 leaf pass... (C) | FIXED | CertChain.cs weak key/signature, server EKU | ran: leaf-1024 -> level 2 'weak key: RSA 1024 bits' |
| 49 | Chain check stops after 9 certificates and then shows a green "In plain words" that says the root isn't tru... (C) | FIXED | CertChain.cs stopped row | ran: chain-long.pem -> '… stopped', In plain words level 1 |
| 50 | Chain check treats the first certificate in a .p7b as "your certificate", but bundles are often stored root... (S) | FIXED | CertChain.cs Check leafC = signs none of the others | ran variant: root-first PEM bundle -> 'your certificate localhost' (mono can't import .p7b) |
| 51 | Windows version check gives "Pro Education" the Enterprise / Education support dates: 24H2 Pro Education sh... (C) | FIXED | SecChecks.cs VersionVerdict ^(Enterprise\|Education\|IoTEnterprise) | ran: 26100 ProfessionalEducation -> 'security fixes ENDED 2026-10-13' |
| 52 | Startup check flags Windows' own programs as unsigned and counts every per-user Run entry twice (S) | FIXED | SecChecks.cs Startup (no HKCU Registry32, skip %windir%) | read |
| 53 | Roast my network: the picture includes the provider's name although the tool says "no provider name" (C) | FIXED | Fun.cs Roast Burns(w, g, "") | read call site; Burns itself still names a passed provider (ran), only the picture path passes "" |
| 54 | A CSR for a wildcard name gets the subject "CN=.example.com" (C) | FIXED | CertKeys.cs Cn keeps '*.' | ran: Dn('*.example.com') -> CN=*.example.com |
| 55 | IPv4 typed with leading zeros, hex or fewer than four parts is read as a different address in the ping tool... (C) | PARTIAL | Icmp.cs Resolve4; Toolkit.cs Subnet; DestCheck.cs URL branch | ran: Resolve4 octal/hex/dword -> null; Subnet('010.1.2.3/24') refused; DestCheck 'http://010.1.1.1/' refused. Left: Subnet('0x0a.1.2.3/8') still accepted as 10.0.0.0/8 (hex not caught: the check only fires on ^[0-9.]+$) |
| 56 | Revocation checks the wrong certificate when "Name to ask for (SNI)" was used (S) | FIXED | Toolkit.cs Revocation(host, port, sni) | read |
| 57 | Check a destination on port 853 (DNS over TLS) or 636 sends an HTTP HEAD and turns a healthy server yellow (S) | FIXED | DestCheck.cs webPort | read |
| 58 | NTP: a reply with zero (or garbage) receive and transmit timestamps says "Your clock is behind by 291404279... (C) | FIXED | Protocols.cs Ntp zero timestamps; Span() | ran: h2.exe -> 'answered without a time in it (empty timestamps)' |
| 59 | SNMP test trap: sysUpTime is badly encoded for many uptimes (e.g. any uptime between 23.3 and 46.6 hours) (C) | FIXED | Lab.cs TrapPacket | ran: ticks 200 -> 43-02-00-C8; 9000000 -> 43-04-00-89-54-40 |
| 60 | Unicode digits make Many ports, the ICMP builder and the ICMP builder's "Takes ..." estimate throw FormatEx... (C) | PARTIAL | MoreTools.cs / Lab.cs fixed; FullView.Toolbox.cs line 677 Duration case "icmpb" | ran: MoreTools/Lab refuse Arabic digits cleanly; lead/dur.exe Duration('icmpb', 'gap ١٠٠') still THREW FormatException - called from the Toolbox paint (line 1184), DB.OnPaint has no catch |
| 61 | LDAP check: a 16 KB reply with a long-form length near its end throws IndexOutOfRange (C) | FIXED | MoreTools.cs ParseRootDse | ran: crafted 16 KB tail -> 0 entries, no exception |
| 62 | Internationalised and non-default-port hosts go out wrong: HTTP timing sends Host without the port, and HTT... (C) | FIXED | Protocols.cs Host header IdnHost:port; CertChain.cs IdnMapping before hello | ran: captured 'Host: 127.0.0.1:18080'; SNI conversion read |
| 63 | ICMP builder silently ignores options it doesn't know (`-l 1472 -f`), and its chart repeats the last RTT fo... (C) | FIXED | Lab.cs -l/-f/-i mapping, 'not understood'; NaN for lost | ran: '-l 1472 -f' -> '1472-byte payload, don't fragment' |
| 64 | Many ports: "8000-8010" tests only 8000 and 8010 (C) | FIXED | MoreTools.cs range expansion | ran: 8000-8010 -> '0 of 11 ports open' |
| 65 | STUN to an IPv6 server always says "answered, but not with an address" (C) | FIXED | Reach.cs ParseStun family 2 | ran: h6.cs IPv6 XOR-MAPPED decoded |
| 66 | Captive-portal check reads a chunked "Microsoft Connect Test" as a sign-in page and overrides the verdict i... (C) | FIXED | DestCheck.cs ClassifyPortal IndexOf | ran: chunked body -> 0 |
| 67 | Internationalised names typed bare are refused by Check a destination and every ping tool (C) | FIXED | DestCheck.cs IdnMapping for bare names | ran: Parse('bücher.de') -> xn--bcher-kva.de |
| 68 | Host:port splitters disagree: Lab.Split refuses `[2001:db8::1]` without a port; Protocols.Split takes `[::1... (C) | FIXED | Lab.cs Split; Protocols.Split -> Lab.Split | ran: '[2001:db8::1]' True; '[::1]x' False |
| 69 | Toolbox URL rewrite works per tool, not per box: Certificate chain compare loses a URL's port and checks th... (C) | FIXED | FullView.Toolbox.cs UrlTools has certcmp; CertKeys.cs Live parses URLs | ran: s7/url.exe certcmp in UrlTools True; Live URL parse read |
| 70 | Minimising the window (or Win+D) stops a running 60-minute MTR, ISP report or any other running tool (S) | FIXED | FullView.cs Resize (!tbBusy && !sniffing) | read |
| 71 | The open window doesn't take up imported settings (mode tabs, Toolbox view, Points button, language) (S) | FIXED | FullView.Settings.cs ImportSettings L.Use/ApplyMode/SyncPointsBtn | read |
| 72 | live_capture = off: Start live capture does nothing and says nothing (S) | FIXED | FullView.cs liveBtn/liveText Policy.NoCapture | read |
| 73 | Toolbox: clicks that open another tool are silently dropped while a tool runs, and some write into the runn... (S) | FIXED | FullView.Toolbox.cs BusyNote; Overview tbBusy check | read |
| 74 | Settings search finds nothing for "Wi-Fi", "sign-in" or "français" (C) | FIXED | FullView.Settings.cs SetMatch | ran: s7/sm2.exe wi-fi/Wi-Fi/wifi -> 5, sign-in -> 3, français -> Language |
| 75 | Toolbox search: "wifi" misses most Wi-Fi tools, "SPF" opens the routing-protocol listener, "dmarc" suggests... (C) | FIXED | FullView.Toolbox.cs MatchesScored / Synonyms | ran: s7/m.exe (repointed to MatchesScored) wifi 25, SPF -> smtppath,dig, dmarc -> smtppath,dig, port 443 -> port first |
| 76 | Settings › Keep history for highlights the wrong length under an IT policy (S) | FIXED | FullView.Settings.cs Keep history for + Policy.MaxHistoryDays | read: help says 'Your IT team keeps it to N days', index logic handles non-choice values |
| 77 | Health page: the "DONE hh:mm" stamp shows the current time, not when the check finished (S) | FIXED | FullView.Tools.cs toolAt | read |
| 78 | Shutdown or logoff loses up to 5 minutes of data usage every time (S) | FIXED | AppCtx.cs OnSessionEnding Usage.Save | read |
| 79 | Packet journey on DNS over TCP (53) or DNS over TLS (853): "Some questions were asked twice" on every clean... (C) | FIXED | Capture/Journey.cs Kind dns UDP only | ran: hS2/J.exe 853 and 53 TCP -> 'Looks healthy', no 'asked twice' |
| 80 | Live capture call quality: RTP renumbering still gives 0% or 50-60% loss (round-3 #2 only partly fixed) (C) | FIXED | Capture/LiveCapture.cs AddRtp (RFC 3550 A.1) | ran: hS2/R.exe restarts 45000->20000/100/50000, 61000->2000 -> 3.73% shown for 3.75% real |
| 81 | Two Wi-Fi adapters: while the main one is between states, the service path reports the other adapter's "dis... (S) | FIXED | LinkInfo.cs Current(out now, prefer) | read |
| 82 | Startup list: items switched off in Task Manager still look active (Startup folder and 32-bit Run list) (S) | FIXED | WindowsDeep/Inside.cs StartupApproved\<Bucket> (Run/Run32/StartupFolder); ExeOf .exe | read (reworked since 10d81d3, same intent) |
| 83 | A program running from C:\Windows\Temp is described as "ships with Windows" (C) | FIXED | Knowledge/Explain.cs Where (Windows\Temp) | read |
| 84 | What's wrong? calls a router that just ignores ping "weak" and makes that the amber finding (S) | FIXED | Investigate.Whats.cs weak excludes a silent router | ran: inv.exe s1 (router ignores ping+TCP) -> level 0 |
| 85 | The hosts-file check only looks at the first 40 entries, so "A website won't open" misses a pin further down (C) | FIXED | WhyFails.cs Hosts (all lines matched) | ran: S5h/hosts.exe from sysroot -> pins=1 |
| 86 | SNMP Interfaces: a device restart between two runs isn't noticed when the new uptime is higher than the old... (C) | FIXED | SnmpTools.cs Changes (uptime vs wall time) | ran: agent ifreboot twice -> 'the device restarted in between' |
| 87 | SNMP walk: an agent that answers with the same OID again ends the walk silently, shown as complete (C) | FIXED | Snmp.cs Walk repeated OID | ran: agent same -> partial 'stopped moving forward (it repeated ...)' |
| 88 | SNMP: any stray datagram from the device ends the request with an error, although the code says stray packe... (C) | FIXED | Snmp.cs Stray filter (v2c and v3) | ran: garbagefirst / wrongcommfirst -> full answer |
| 89 | An SNMP community with any non-ASCII character never works (C) | FIXED | Snmp.cs Parse2 byte compare | ran: community 'café' -> answer |
| 90 | SNMP Traffic on 32-bit counters: no "Busiest %", a phantom multi-gigabit rate when ifSpeed is missing, and ... (C) | FIXED | SnmpTools.cs Traffic (ls speed, sysUpTime before/after) | ran: t32wrap Busiest 48%; t32nospeed 'reset'; t32reboottop still '10 kb/s' because the test agent doesn't reset sysUpTime (uptime check read) |
| 91 | Inspection treats any root in a "Third-Party Root CAs" (AuthRoot) store as public, including one an admin o... (S) | FIXED | Inspection.cs OnPublicList AuthRoot thumbprint test | read |
| 92 | UPS (APC MIB): output "sleeping", "rebooting" or "sleeping until power returns" is shown green as "other" (S) | FIXED | SnmpGear.cs output status 5/8/11 -> OFF | read |
| 93 | Network sign-in check says "no failed sign-ins (Wi-Fi or wired)" in green when the Wi-Fi log couldn't be read (S) | FIXED | WinEvidence.cs line 100 | read |
| 94 | Bandwidth: "1.000 GB" is read as 1 GB, and a size in "Gb" is read as gigabytes (C) | FIXED | Calc.cs Num / Size | ran: H.exe '1.000 GB' and '4 Gb' refused with the hint; '10.000 MB' refused |
| 95 | Packet stack: the IPv6 fragment count ignores the 8-byte fragment header (C) | FIXED | Calc.cs Stack fragHdr | ran: udp ipv6 2900 -> 3 fragments, 2880 -> 2 |
| 96 | "GB" in the month review, the six-month review and the data-cap forecast is decimal MB ÷ 1024 (S) | FIXED | OverTime.cs / Brain.cs / Theme.cs /1000 | read |
| 97 | Game history is never rolled off when History is off or when no game is watched (S) | FIXED | History.cs Tidy -> GameHistory.TidyNow; AppCtx start | read |
| 98 | "Type an address in the box above and press Enter to see which route it takes" never shows the route, and r... (S) | FIXED | FullView.Story.cs StorySite storyIp for typed IPv4 | read |
| 99 | "Email it": the app says the address is on the Hall of Fame page, but the page still has the placeholder an... (C) | FIXED | docs/hall-of-fame.html lines 240-243; FullView.Points.cs | read: placeholder kept but the email pill and 'or send an email' are hidden when MAIL has no '@'; app text no longer promises an address |
| 100 | hof_verify.py crashes on a claim code with any non-ASCII character, and the issue gets an empty answer (C) | FIXED | hof_verify.py claim normalisation; hall-of-fame.yml empty RESULT | ran: U+2011 code -> added; full-width -> accepted; Cyrillic letter in code -> clear 'mismatch' message, exit 2 |
| 101 | Self-test: three checks prove less than their titles, and the website says they do (C) | PARTIAL | SelfTest.cs line 2202 memory (fails >120 MB), 2224 GDI (now asserts), 497 leak (gdi0==0 now fails), Points 1.1.5 test (no lab check) | read + ran --selftest: GDI and leak test fixed; memory threshold raised to 120 MB ('80 MB in normal use') against the 64 MB rule and the site's '80 MB budget'; 'a challenge per lab' still not checked |
| 102 | Every deep test leaves an encrypted "diagnosis" file in the user's Documents that nobody can open (S) | FIXED | FullView.DeepTest.cs ByHand friend | read |
| 103 | A hostile or oversized key-backup file is decrypted whole into memory before the HMAC is checked (300 MB fi... (C) | FIXED | EcKeys.cs Restore 64 KB cap | ran: h4/t2.exe 300 MB key backup -> 'this isn't a key backup (it's far too big)' in 0.0 s, peak 25 MB (r4: 585 MB, 12 s); 44 KB variant -> 'wrong code', 30 MB |
| 104 | An SNMP walk has no size cap in bytes: a device with long values makes AdeeboMeter use about 450 MB (C) | FIXED | Snmp.cs Walk 4 MB / 4 KB caps | ran: agent huge -> 'stopped at 4 MB of answers - 1018 values', peak RSS 52 MB (r4: 455 MB) |
| 105 | DNS and captive-portal reads have per-read timeouts only - no overall deadline, no size cap on DoH, and no ... (C) | FIXED | Toolkit.cs QueryDoh/ReadN 8 s; DestCheck.cs portal 6 s; Protocols.cs DoH 16 KB / 8 s | ran: DoH drip -> 'too slow - stopped after 8 s' at 8,004 ms; portal/TCP read |
| 106 | One crafted MIB file in the MIB folder makes the index build run for hours (quadratic regex) (C) | FIXED | Snmp.cs RxTc/RxDef bounded + 5 s match timeout | ran: S5h/mib.exe tc 4000 1.45 s, 8000 3.1 s, 16000 hits the 5 s limit and the file is skipped (r4: 8000 -> 81.7 s) |
| 107 | iperf3 download: a reset stream spins in a tight exception loop until the test ends (S) | FIXED | Toolkit.cs Receive catch TimedOut only | read |
| 108 | Three failed Wi-Fi service reads (including "no Wi-Fi interface right now") switch the Wi-Fi watch to netsh... (S) | FIXED | Monitor.cs wlanApi retry after 10 min, rc -2 | read |
| 109 | GameHistory's week verdict is quadratic: 0.37-0.5 s per game while holding the lock the Gamer page's paint ... (C) | FIXED | Gamer.cs GameHistory.Load (average once, read outside the lock) | ran: G.exe load 11 -> 107 ms, 3 more 242 ms (r4: 511 / 1,423 ms) |
| 110 | Game probes block thread-pool threads, and a failed DNS lookup is retried 16 times a second (S) | FIXED | Gamer.cs inFlight, failed DNS cached 30 s | read |
| 111 | What changed repaints every second; Network story every 2 s; a running tool's canvas twice a second (S) | FIXED | FullView.cs changesKey/tbBusyKey/travelKey | read |
| 112 | Widget cards create a new GDI font on every draw (S) | FIXED | Theme.cs fCardNum / fZ | read |
| 113 | UI text and comments still describe netsh, Engineer and old figures (C) | FIXED | FullView.Settings.cs, Program.cs, Points.cs, History.cs, HallOfFame.cs comments | read: netsh/Engineer/580/1-90 wording gone |
| 114 | Points and docs numbers are out of date in several places (C) | PARTIAL | docs/hall-of-fame.html line 136; Points.cs line 28; FullView.Points.cs line 308 | ran s8/max.exe: 232 tools, 98 topics, earnable 1,094, Max 1,200. README/site say 232/98 (OK). Left: Points.cs 'about 910 ... 500 means nearly everything', Points page 'Nearly everything AdeeboMeter can teach' at 500 (46%) |
| 115 | The newest features are missing from the CHANGELOG, README and website (C) | FIXED | CHANGELOG.md '## 1.1.5 (2026-11-17)', '## 1.1.6 (2026-11-23)' | read |
| 116 | French: untranslated captions, and one overlap measured in English (C) | PARTIAL | knowledge/lang-fr.txt; FullView.Toolbox.cs L.T on labels/cues and title width | ran s7/fr.py: 16 literals missing (r4: 25). Real UI ones left: Toolbox cue 'Go to a tool - type ping, DNS, ...', pill 'By topic', GameEditor's 6 cues |
| 117 | Settings rows with many choices spill over the label and the side menu on a 1366×768 laptop at 125 % (C) | FIXED | FullView.Settings.cs SetRow wrap | read (s7/seg.exe no longer binds: option arrays renamed) |
| 118 | Switching from dark to light leaves the Toolbox safety tick's text near-white on white (C) | FIXED | FullView.cs Recolor CheckBox/RadioButton | ran: s7/rc.exe after dark->light fore #16191E on White |
| 119 | High contrast: switching it on or off while running isn't followed, and BlueSoft panels lose their text (S) | FIXED | Theme.cs BlueSoft=Window under HC; AppCtx re-apply on HC change | read |
| 120 | Mood: the provider "excuse" times an outage from the first outage of the day, and the header line never upd... (C) | FIXED | Mood.cs Track (Monitor clocks) | ran: fix/h/h1/mood2.exe: outage clock reset when the line comes back, fresh outage starts now |
| 121 | Certificate expiry: a certificate that expired less than a day ago reads "0 days left" / "expired 1 days ago" (C) | FIXED | CertChain.cs Expires (h ago); SecChecks.cs DateTime.Now > NotAfter | ran: h4:8446 'expired 11 days ago'; <1 day path read |
| 122 | Revocation: "couldn't tell - the issuer's list didn't answer in 10 s" also appears when the certificate has... (S) | FIXED | CertChain.cs Revocation no CDP/OCSP text | read |
| 123 | Letter to my provider says "The outages above ..." when there were no outages (S) | FIXED | Fun.cs letter w.Outages > 0 | read |
| 124 | "The internet is slow" with the router check off ends with green "router no answer" (C) | FIXED | Investigate.cs Slow 'not measured'/'ignores ping' | ran: inv.exe s12 'router not measured', s10 'router ignores ping' |
| 125 | Wi-Fi 7 320 MHz shows as 160 MHz; WPA3-Enterprise (non-192) shows as WPA2-Enterprise; FILS as plain "WPA2" (C) | FIXED | LinkInfo.cs EHT Operation, AKM 5+MFPR, AKM 14 | ran: hS2/W.exe 320 MHz; AKM 14 -> WPA2-Enterprise |
| 126 | Driver dates show one day early west of UTC (S) | FIXED | WindowsDeep/DevInfo.cs Time(dateOnly) | read |
| 127 | Learn › hash says MD5/SHA-1 let you "build a second file with the same fingerprint" (C) | FIXED | knowledge/learn.txt == hash | read |
| 128 | Misleading error text: dig "no answer from X in 3 s" for a name it never sent; iperf "-6" says "Sequence co... (C) | FIXED | Toolkit.cs dig empty-label row; iperf 'no IPv6 address' | ran: 'a..b' -> 'a part of the name is empty or longer than 63 characters - nothing was sent'; iperf -6 -> 'this server has no IPv6 address' |
| 129 | Check a destination: path MTU is 1 byte low for about a third of the sizes below 1200 (C) | FIXED | DestCheck.cs path MTU search | ran: Python copy of the new loop, 924 true sizes: 0 wrong (r4: 277) |
| 130 | Bandwidth calculator: a size alone or a time alone gives a blank result, and common units are refused (C) | FIXED | Calc.cs Bandwidth one-value hint, more units | ran: '50 GB' alone and '2 h' alone -> 'needs one more value'; '10 minutes', '100 Mb', '1 Tbps' accepted |
| 131 | Gamer comments and on-screen notes describe limits that have changed (C) | FIXED | Gamer.cs comments; RouterAvg fills the router column; GameEditor note | read |
| 132 | Learn and console tools contact a game's servers without the Gamer Zone's consent click, unlike Wi-Fi vs cable (S) | FIXED | Gamer/GamerTools.cs Pick | read: unclicked game -> 'click it in the Gamer Zone first' |
| 133 | Survive the outage: "A new personal best" on a tie, and on every repaint (C) | FIXED | FullView.Games.cs outBest | read |
| 134 | Keyboard users never earn the "used a page" point on painted-only pages (S) | FIXED | FullView.cs line 1070 db.Press -> PageUsed | read |
| 135 | Website contrast below WCAG AA in both themes (C) | STILL PRESENT | docs/hall-of-fame.html; docs/index.html | ran s8/web.py (Chrome, 1366 light): HoF td.pts 3.22:1 and th 4.33:1 unchanged (26 low-contrast elements); index 20 (white 'DNS'/'NET' chips on #C4CCD8 1.62:1, blue words 3.39:1) |
| 136 | Hall of Fame page: long handles are cut off, combining marks spill over the label, and a stray "site" value... (C) | PARTIAL | docs/hall-of-fame.html line 55 (name wraps), line 58 (.winner a no wrap), line 265 SITES[String(w.site)] | read: names wrap and marks are capped (app + hof_verify); the handle link still doesn't wrap and SITES is a plain object, so site 'constructor' still throws in forEach |
| 137 | ByHand points to a test plan that doesn't exist (C) | FIXED | FullView.DeepTest.cs line 23 -> LIVE-TESTS.md | ran: git ls-files has LIVE-TESTS.md |
## New bugs

### N1. Turkish Windows: "Your connection" shows your own Wi-Fi signal as 0% in red
Severity: wrong result  
Where: src\Link\LinkInfo.cs line 648 (Collect: `if (f.TryGetValue("Signal", out v)) int.TryParse(v.TrimEnd('%', …), …, out sig);`), with line 519 (the new `{ "sinyal", "Signal" }` KeyWords entry from the #35 fix) and line 651 (`if (sig < 0) sig = psig;`)  
How to reproduce: feed `netsh wlan show interfaces` output from Turkish Windows, which writes the percent sign first (`Sinyal : %88`). Sample: review5/fix/h/hS2/netsh/tr2.if.txt, run with `mono N.exe tr2`.  
What happens / what should happen: the Signal row reads `0%`, level 2 (red), while the access-point list beside it shows 88%. `TrimEnd('%')` leaves "%88", so `TryParse` fails and sets `sig` to 0, not -1. Because 0 is not below 0, the `Monitor.ParseWifi` fallback (which reads the number by its shape) is skipped. In round 4 "Sinyal" wasn't mapped, so the fallback ran and gave 88%. This is a regression from the #35 fix. The fix is to trim '%' on both sides (or take the digits with a regex), and to keep -1 when the parse fails.  
Confidence: confirmed (ran it: hS2/N.exe tr2 → sig=0, using the same LinkInfo.Fields and the same parse as line 648). That Turkish netsh puts % first is suspected; it is Turkish number formatting, and the sample was not captured on a real PC.

### N2. Rule 8: the forbidden vendor and its router OS are named in two tracked files that ship in the source zip
Severity: wrong result  
Where: HANDOVER-SEO.md line 7 ("**No company/employer references anywhere** (no N…, no employers' product names)"); PLAN-1.1.5.md line 558 ("…any import and SR OS alignment lives in topogram."); release.ps1 line 85 (`git archive … HEAD`)  
How to reproduce: `git grep -n -i <vendor>` and `git grep -n 'SR OS'` at 999a100.  
What happens / what should happen: the release's source zip and the public repository contain the name. Round 4 #33's line 8 was reworded, but HANDOVER-SEO.md (added 17 Nov) repeats the same rule with the name spelled out. Reword both ("the forbidden vendor (see the private brief)", "the vendor's router OS"), or keep handover and plan files out of the repository. A `git grep` step for these words in tools/ci_checks.py would stop it happening again.  
Confidence: confirmed (git grep)  
Rule: 8

### N3. The self-test count is wrong in README, on the website and in the Theory of Everything
Severity: cosmetic  
Where: README.md line 86 and docs/index.html line 1030 ("93 checks"); docs/THEORY-OF-EVERYTHING.md line 281 ("104 checks"); src/SelfTest.cs (100 T() checks, 101 with `--samples`)  
How to reproduce: run `AdeeboMeter.exe --selftest` and count the PASS and FAIL lines: 100.  
What happens / what should happen: three different numbers are published, and none matches the code. Have the self-test print its own count, and check it against README in tools/ci_checks.py, as round 4 #114 suggested for the other numbers.  
Confidence: confirmed (ran --selftest under mono; grep)

### N4. Widget renderers leak their bitmap when drawing throws, and size the card from text with no upper bound
Severity: slow  
Where: src\Ui\Theme.cs lines 680, 735, 801, 852 (GamerCard, CallsCard, EngineerCard, Card); src\Ui\SpeedWidget.cs lines 79 and 120. In each, `var bmp = new Bitmap(W, H)` is followed by `using (var g = PrepW(bmp)) {…}` with no try/dispose for `bmp`, and `H` comes from `Measure(headline/quip/hint)` with no cap.  
How to reproduce: under mono, review5/fix/mem/pv.exe calls `Draw.Preview` for every style. libgdiplus returns heights of about 1.4 M px for the embedded Barlow font, so "speedstatus", "speedapps" and "calls" each add 480-1,400 MB of RSS, throw in `Graphics.Clear`, and the memory is never returned (RSS about 2 GB after GC). The self-test's "memory after everything" then reads 2,046 MB.  
What happens / what should happen: on Windows the metrics are sane, so the height stays small. But any exception while drawing a card (GDI+ "Parameter is not valid" from a disposed font happened in 1.1.6, per commit 58a7f4e) leaks one card bitmap per draw, and the widget redraws every 1-5 s. Wrap the drawing in try { … } catch { bmp.Dispose(); throw; }, and clamp `H` (for example to 3× the normal card height).  
Confidence: confirmed on mono (ran it). The Windows leak is suspected (read).
