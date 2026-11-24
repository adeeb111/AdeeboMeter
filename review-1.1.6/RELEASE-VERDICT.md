# AdeeboMeter 1.1.6 - release verdict (999a100, dev-1.1.6)

**Verdict: NOT READY - 4 blockers.** All 4 have small, local fixes. Everything else can ship or wait for 1.1.7, your call.

How it was checked: 12 reviewers read all 220 source files (67,800 lines), the CI, the release scripts and the website (555 pages, 23 in Playwright). They ran a Linux (mono) build of 999a100 with real X clicks for the day-1 journeys. Nothing was run on Windows. Build 1124o on your PC wasn't reachable, and the list of 49 findings wasn't in the repo.

## Blockers

| # | What | Where | Report |
|---|---|---|---|
| B1 | A paired phone can make the PC open any local file or `\\attacker\share`, which sends your Windows sign-in (NTLM) to the attacker. It can also test whether a file exists and read files up to 64 MB. The phone list is a deny-list, so new tools are allowed by default. `Pcap.Read` has no network-path check, and `//server/share` gets past `CertTools.Input`. **Fix:** an allow-list for phone tools, and refuse any path that starts with `\\` after `GetFullPath` (in Pcap, CertTools, CertKeys and FileCrypt). | src\Ui\FullView.Remote.cs:19-27, src\Checks\Pcap.cs:34, src\Checks\CertTools.cs:32 | R5-security #1, P2-checks1 #3 |
| B2 | Rule 1: Sports (BBC, NHL, ESPN, football-data, CricketData), Financial Modeling Prep quotes, Bank of Canada history, Stream Check and the phone-remote relays are missing from About > "What leaves your PC" and from privacy.html. privacy.html still says the app has "no server". The kick-off reminder fetches Sports every 30 min with the window closed. "Follow teams..." turns Sports on before Save, and Cancel leaves it on. | src\Watch\Sports.cs, MarketData.cs; docs\privacy.html | P4 #1, P5 #3, D1 #3 |
| B3 | Rule 8: the source zip (`git archive HEAD`) ships `HANDOVER-SEO.md` line 7, which names the company, and `PLAN-1.1.5.md` line 558, which names its router OS. | HANDOVER-SEO.md:7, PLAN-1.1.5.md:558 | R5-fixcheck N2 |
| B4 | Hall of Fame: cards sent through the relay all look like one GitHub sender, so anyone can re-send a public claim code with their own social handle, replacing or blanking a winner's link. Reproduced on a scratch repo. | .github\scripts\hof_verify.py | P5 #1, R5-fixcheck #9 |

## Fix before release (high impact, small fixes)

- **Policy switches that don't switch off:**
  - `live_capture=off` misses "Find my switch port" and the Routing/Announcements listeners.
  - There is no policy key for the phone remote at all.
  - `online_lookups=off` misses the ISP public-IP lookup, DNS compare's 1.1.1.1 and the sign-in page request. (U2 #1, P3 #1, R5-sec #3, P2 #10)
- **False greens:**
  - The Security checkup accepts a forged certificate as "public authority". (P3 #2)
  - Home shows "All quiet" before anything is measured. (D1 #1)
  - Gamer shows "Ready to play - nothing in the way" above an amber row. (P5 #5)
  - Gamer Zone says "No connection" before the first ping. (U1 #2)
- **Octal addresses in `Lab.Addr`:** `010.000.000.005` becomes 8.0.0.5 and reaches the RADIUS lab (which carries the password and HMAC), the senders, SNMP trap and more. (P2 #1)
- **"What's wrong?" buttons** do Copy instead of their action in every language but English. (U1 #1)
- **Phone pairing:**
  - The six words can be downgraded to four. (R5-sec #2)
  - "Copy link" puts the QR secret in Win+V history. (R5-sec #4)
  - The Allow and Remember buttons aren't tied to the question on screen. (R5-sec #6)
- **Shell and editor memory:** they can reach 0.5–3 GB (`printf '%999999999d'`, vim `100000yy` then `50p`). (R5-sec #5)
- **Hangs:**
  - "What changed on my server" and "Certificate calendar" handshake with no deadline, and Stop doesn't work. (P3 #3)
  - HwSpecs holds a lock across a WMI connect with no timeout. (P4 #3)
- **Arabic table:** it has meaning-changing machine-translation errors. The Example banner says "different data" (rule 9), "Sniff" reads as "stinky", RAID as a police raid, and the app name is misspelled. Get a native speaker to review it. (P1 #4, U1 #7)
- **Day-1 upgrade trap:** "Just run it from here" next to an installed and running 1.1.5 quits 1.1.5, starts as a new user, and has Start with Windows pre-ticked from Downloads. (D1 #5, #8, P1)
- **Import settings:** it silently applies a weather server (which then gets your coordinates), the internet check, phone standby and the sign-in check. (U3 #2)
- **Examples can leave the app** through the picture bar (save, copy, phone, Bluetooth). (U3 #7, rule 9)

## Totals by report

| Report | Findings | Notes |
|---|---|---|
| R5-security.md | 11 | phone remote, Shell, bug report, weather; BLOCKER B1 |
| R5-fixcheck.md | 126 of 137 round-4 fixed, 9 partial, 2 still present; 4 new | memory 2,046 MB is a mono font-measuring artifact (real part: widget bitmaps leak on error); BLOCKER B3 |
| D1-day-one.md | 17 | day-1 journeys run with real clicks |
| P1-core.md | 16 | |
| P2-checks1.md | 15 | |
| P3-checks2.md | 21 | |
| P4-watch-link-deep.md | 11 | B2 |
| P5-gamer-labs-ci-docs.md | 13 | B4 |
| U1-ui.md | 19 | |
| U2-ui.md | 11 | |
| U3-ui.md | 12 | |
| U4-ui.md | 13 | |
| **New findings** | **about 175** | some overlap between reports |

## Held up

- **Never listens (rule 2).** No listener anywhere; the phone remote only connects out.
- **Encryption.** The phone remote encrypts, then authenticates, checks the tag before decrypting, matches the browser's key derivation and uses fresh keys each session. `.adeebo` and EC keys behave the same way.
- **Shell confinement.** AdeeboShell stays inside `/data`. It refuses `..`, alternate data streams and links, and starts no programs.
- **SNMP** stays read-only (rule 5).
- **Fuzzing.** Every binary parser survived 20,000–200,000 hostile inputs each.
- **Day-1 journeys.** In en, fr, es, ar and bn, all 232 tools and every page opened without an exception. Install then uninstall cleans up. The 1.1.5 settings upgrade works.
- **Website analytics** loads only on public pages and honours Do Not Track and GPC.
- **CI and release.** The version is 1.1.6 everywhere, `.gitignore` is complete, and dev branches no longer get the Hall of Fame key.

## For you to decide

- **Memory budget.** Rule 7 says 64 MB; 1.1.6 says 80 MB; the self-test fails only above 120 MB.
- **Replay bonus.** Rule 10 says +2; the code says +3.
- **Website language.** Visiting any translated page switches the English site to that language.
- **Relay hostname.** It contains your personal handle.
