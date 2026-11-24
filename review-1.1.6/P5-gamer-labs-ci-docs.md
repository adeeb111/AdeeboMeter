# P5: Gamer, Labs, self-test, CI, release, docs and website: release check of 1.1.6 (999a100)

Scope read: src/Gamer/* (all), src/Labs/* except LabLink.cs (BrowserStore, LabSim, LabViews, LearnGames, LearnRounds), src/SelfTest.cs (runner and the tests named below; all 100 T() titles listed), .github/ (3 workflows, hof_verify.py, templates), release.ps1, build_exe.cmd, tools/finish-release.ps1, tools/sign.ps1, tools/ci_checks.py, tools/relay/worker.js, store/, .gitignore, README, CHANGELOG 1.1.6, SECURITY.md, FINDINGS-PC-1.1.6.md, PLAN-1.1.6.md, docs/ (privacy pages in all 5 languages, analytics.js, sw.js, locale.js loader, hall-of-fame page and JSON, sitemap/robots).
Harnesses are in .../scratchpad/review5/P5-gamer-labs-ci-docs/: links.py (static links and anchors, all 555 pages), web.py/t2-t6.py (Playwright, Chromium 1243), t5.py (analytics on the production host via host-resolver), sw.py (service worker across a simulated release), pm.cs (PreMatch through reflection on r6.exe), bs.cs (BrowserStore), max.cs (census), plus a hof_verify run against a scratch copy of the repo.

## Findings

### 1. Hall of Fame: anyone can swap a winner's profile link by re-sending their card through the relay, because every relay issue has the same author
Severity: security  
Where: .github\scripts\hof_verify.py lines 174-196 (main: `by == author` means "the same sender again: let them fix their link"); tools\relay\worker.js lines 76-90 (hof cards are filed as issues with the relay's own GitHub token); hof_verify.py line 207 (the claim code is stored in docs\hall-of-fame.json, which is public)  
How to reproduce: HOF_KEY=testkey123. Send Ada's card (612 points, 2026-11-20) with ISSUE_AUTHOR=adeeb111 (the owner of the relay's token, so the author of every issue the relay files) and handle X/ada_real. The result is "added". Send the same name, points, date and code again with ISSUE_AUTHOR=adeeb111 and handle Instagram/scam_acct. The result is "updated: Updated your link in the Hall of Fame", and the JSON now holds `site: instagram, handle: scam_acct`.  
What happens / what should happen: cards sent from the app ("Send it to the Hall of Fame") or from the website form all arrive as issues written by one account, so the "same sender" test is always true. The code, name, points and date of every winner are public in hall-of-fame.json, so anyone can re-send a card with their own Instagram, X, LinkedIn or Bluesky handle, and the winner's link silently points to them. When the winner used the GitHub form instead, the same re-send through the relay marks the entry disputed and removes the winner's link: anyone can grief any winner. Possible fixes: treat a relay-filed issue (label from-app or from-site, or author = the token owner) as an unknown sender, so it can add an entry but never change one; or keep the code out of the public JSON (store an HMAC of it instead).  
Confidence: confirmed (ran hof_verify.py twice against a scratch repo copy; output above)  
Rule: 10

### 2. A by-hand "rebuild a release" run puts unsigned files back over a signed release, and the notes still say "Code-signed"
Severity: security  
Where: .github\workflows\build.yml lines 117-125 (the guard "the release already has its files (signed by hand)" runs only when `GITHUB_EVENT_NAME == release`; workflow_dispatch goes straight to `gh release upload ... --clobber` and `gh release edit --notes-file`); release.ps1 line 112 (the notes always say "Code-signed by ..."); build.yml line 7 (the header says the by-hand run "rebuilds that release's files from main", but line 34 checks out the tag)  
How to reproduce: after finish-release.ps1 has signed v1.1.6, run Actions › build › Run workflow with tag v1.1.6, as the header describes for refreshing notes.  
What happens / what should happen: the signed AdeeboMeter.exe and the re-zipped win64 zip are replaced by the unsigned CI builds, SHA256SUMS is replaced, and the notes lose the signing line but keep "Code-signed by Open Source Developer ...". The same is true for the normal flow until finish-release runs: publishing attaches unsigned files under notes that say they are signed. Apply the "already has its files" guard to dispatch runs too (or refuse dispatch for a release whose exe has a valid signature). Write the signing sentence only in finish-release.ps1.  
Confidence: suspected (read build.yml, release.ps1 and finish-release.ps1; can't run Actions here)

### 3. The privacy policy and About › "What leaves your PC" leave out Sports, FMP quotes and the phone remote's relays, and the file list misses most 1.1.6 files
Severity: wrong result  
Where: docs\privacy.html "What it sends" and "What stays on your PC" (and es/fr/bn/ar copies); src\Ui\FullView.Settings.cs lines 497-519 (the `leaves` list). Not listed: src\Watch\Sports.cs lines 165-272 (feeds.bbci.co.uk, api-web.nhle.com, site.api.espn.com, api.football-data.org, api.cricapi.com, fetched "while the Sports page is open or a stream plays and a followed team is live"); src\Watch\MarketData.cs line 332 (financialmodelingprep.com with the user's key, every 1-60 min); src\Remote\Remote.cs line 26 (wss://adeebometer-remote...workers.dev) and RemoteKnown.cs line 86 / PhoneAsk.cs line 59 (adeebometer-live...workers.dev /ping). Files written but not listed include sportkeys.dat, sporthits.txt, mk-calls.txt, moments.txt, board-watch/board-log.txt, streamchecks.txt, streamlinks.bin, time-pins.txt, cert-compare/cert-dates.txt, outbox\ (queued reports), lab.html and edge-profile\ (the Network Lab page holds the LAN device list, MACs and the PC name, LabViews.cs lines 55 and 145-153).  
How to reproduce: `grep -rhoE "https?://[A-Za-z0-9.-]+" src | sort -u` and compare with privacy.html; compare `Path.Combine(Program.DataDir, "...")` names with the page.  
What happens / what should happen: the published policy (also the Store listing's) says each item "is listed live in the app under About › What leaves your PC", but the sports background fetch during a stream, the FMP quotes and the phone link's relays aren't in either list. The privacy page doesn't mention the phone remote at all: About has only "Phone waiting room". The page also still says "no server", while three Cloudflare Workers are part of the product. Add Sports, Markets quotes, the phone link (pairing, the waiting room, the clock ping) and the Network Lab files to both lists, and change the date.  
Confidence: confirmed (grep of hosts and file names; read both lists)  
Rule: 1

### 4. "gh attestation verify AdeeboMeter.exe" fails for every signed release, but SECURITY.md, README and build_exe.cmd tell users to run it
Severity: wrong result  
Where: SECURITY.md lines 25-29 ("Each file also carries GitHub build provenance - proof it was built by this repository's workflow, not uploaded by hand"); README.md "Get it" ("build provenance proves where a file came from: gh attestation verify AdeeboMeter.exe"); build_exe.cmd line 27 comment; build.yml line 102 step name; tools\finish-release.ps1 lines 43-69 (signs the exe, re-zips win64 and uploads both by hand)  
How to reproduce: download the 1.1.6 exe or win64 zip after finish-release.ps1 has run, then `gh attestation verify .\AdeeboMeter.exe -R adeeb111/AdeeboMeter`.  
What happens / what should happen: attestation covers the SHA256 of the unsigned CI files. Signing changes the exe, and the zip is rebuilt around it, so no attestation matches either published file (only the source zip still verifies). The release notes say this; the security page tells people to use the check that fails and calls the files "not uploaded by hand". Document the real chain: Authenticode signature, plus the pre-signing hash in the notes checked with attestation. Or attest the signed files from finish-release (not possible from a PC), or attach the unsigned exe as well for verification.  
Confidence: suspected (reasoned from finish-release.ps1 and how attestations bind digests; gh is not available here)

### 5. [day 1] Gamer "Before you queue" says "Ready to play? yes - nothing in the way" (green) while its own Internet row says "Slow right now - expect lag"
Severity: wrong result  
Where: src\Gamer\GamerTools.cs line 108 (PreMatch: the Internet row is amber for > 80 ms or not measured, but `warn` is never increased), line 110 (traffic "- down · - up" counts as "Quiet."), line 122 (the headline uses `warn`)  
How to reproduce: pm.cs harness (reflection on r6.exe): Monitor with router 3 ms and internet 150 ms gives `lv=0 Ready to play?: yes - nothing in the way` above `lv=1 Internet right now: 150 ms | Slow right now - expect lag.` With no internet sample yet (first minute after install, or internet check Off in Settings / policy), it gives the same green "yes" above an amber "Not measured yet ... give it a minute", which with the check off never changes.  
What happens / what should happen: the one-line verdict contradicts the row under it. Count a slow internet as a warning; for "not measured", say "not checked yet" in the headline instead of "yes". Don't call "-" traffic "Quiet".  
Confidence: confirmed (ran it: pm.cs, output above)

### 6. Self-test and website promise more than the checks prove
Severity: wrong result  
Where: src\SelfTest.cs line 2205 ("memory after everything" fails only above 120 MB) vs docs\index.html line 1031 and docs\THEORY-OF-EVERYTHING.md line 289 ("Memory stays inside the 80 MB budget"); index.html line 1030 "93 checks" and ToE line 281 "104 checks" (the file has 100 top-level T() calls); SelfTest.cs line 1499 (title "... faults earn once", but nothing is awarded or awarded twice; only Points.Known and Value are read); line 419 "never listens" (Tables.OwnListener checks TCP LISTEN once, at a moment when nothing that could listen has started; no UDP); line 640 (the Gamer cap test uses the live Cfg.GameFast: with gfast=0 in the settings the "8 servers at most" check passes on an empty plan); line 121 header ("pings to your gateway and one HEAD request to it on port 80 - nothing on the internet" is true today, but nothing enforces it)  
How to reproduce: read the assertions; count with `grep -c '^            T("' src/SelfTest.cs`.  
What happens / what should happen: a build at 81-120 MB passes the check the site advertises as "inside the 80 MB budget", and the two documents disagree on how many checks there are. Either fail above 80 MB (or say 120), print the check count from the code, award "nlab.mtu" twice in the test and expect 2 then 0, and force GameFast on inside the Gamer test. This continues round-4 #101 with new numbers.  
Confidence: confirmed (read; counted)

### 7. The forbidden company is named in HANDOVER-SEO.md and a product name in PLAN-1.1.5.md, and both ship in every release's source zip
Severity: wrong result  
Where: HANDOVER-SEO.md line 7 ("no N*kia, no employers' product names"), PLAN-1.1.5.md line 558 ("S* OS alignment lives in topogram"); release.ps1 line 85 (`git archive HEAD` publishes every tracked file)  
How to reproduce: `git grep -n -i nokia 999a100` and `git grep -n -E "SR[ -]?OS" 999a100`.  
What happens / what should happen: these are the only hits in the repository (the exe, knowledge packs, site, PDF and binaries are clean). The round-4 #33 hit in PLAN-1.1.5 line 8 is gone, but the rule now names the company in another file. Reword both, for example "no employer or vendor names (see the private brief)".  
Confidence: confirmed (git grep over 999a100; pdftotext of the ToE PDF; strings over the tracked binaries)  
Rule: 8

### 8. A damaged LevelDB table in the Network Lab's Edge profile throws OutOfMemoryException out of BrowserStore, which can end the app from Edge's Exited handler
Severity: crash  
Where: src\Labs\BrowserStore.cs line 126 (Entries: `i + ns + vl > blk.Length` overflows int, so `new byte[sh + ns]` is reached with ns near 2^31); the catch lists at lines 140 and 153-155 and in LevelDb (lines 180-184) don't cover OOM; caller src\Labs\LabLink.cs line 281 (Process.Exited → Pickup() with try/finally and no catch, on a pool thread)  
How to reproduce: bs.cs: a .ldb whose index block is `00 F0FFFFFF07 F0FFFFFF07 00000000 00000000`, footer pointing to it, magic at the end. `LevelDb(dir)` gives "THREW OutOfMemoryException". On .NET Framework, `new byte[0x7FFFFFF0]` throws OOM at once ("Array dimensions exceeded"); from the Exited handler that is an unhandled exception on a pool thread, which ends the process.  
What happens / what should happen: the file header says "a damaged, half-written or locked file is skipped, never thrown". Check `ns > blk.Length - i || vl > blk.Length - i - ns` without adding them together. Only a corrupted or planted file in AdeeboMeter's own edge-profile folder triggers it, so it isn't a remote attack.  
Confidence: confirmed (ran it: bs.cs on r6.exe under mono)

### 9. Website in Arabic at 360 px: the "More" (المزيد) menu opens 56 px off the left edge and drags the whole page sideways
Severity: cosmetic  
Where: docs\ar\index.html (details.navmore > div: positioned for LTR), the same header on the other ar/ pages  
How to reproduce: Playwright, 360×800, /ar/index.html, click "المزيد". The menu box is at L=-56 R=134, the last item ("افتح تطبيق الهاتف") is cut off and the page shifts left (screenshot menu-ar_index.html-360.png). English at 360 gives L=80 R=270; Arabic at 768 gives L=47.  
What happens / what should happen: in RTL, anchor the menu with `inset-inline-end`/`right: 0` (or flip it under `[dir=rtl]`) so it opens inside the viewport.  
Confidence: confirmed (Playwright, light and dark)

### 10. After a site release the phone app's first load mixes the new page with the old scripts
Severity: cosmetic  
Where: docs\sw.js lines 26-30 (scripts and styles: cache first, refreshed in the background) with lines 18-24 (pages: network first)  
How to reproduce: sw.py: open /app.html (the SW installs am-4e3a3c7f6b83), then "release" a new app.html, an appended voice.js and a new V in sw.js, and reload. The page is new (the marker is present), but voice.js is the old cached copy. Only the second load gets v2.  
What happens / what should happen: for one visit after every deploy, new HTML runs against old JS and CSS: missing i18n keys or element IDs, possible console errors. Version the asset URLs (?v=hash, from learnsite.py), or have network-first pages fetch their subresources network-first while the SW version changed. Also `cache.addAll` can store copies that are still fresh in the HTTP cache (GitHub Pages max-age=600), so pass `{cache: "reload"}`.  
Confidence: confirmed (ran it: Playwright with a real service worker on a local copy)

### 11. Bengali and Arabic privacy pages (and ar/gaming) are partly English
Severity: cosmetic  
Where: docs\bn\privacy.html (12 English paragraphs: the "last updated" line, the opening sentence, outage log, devices, walk test, programs, phones, games, and the whole "This website" analytics section); docs\ar\privacy.html (6 file items and the whole "other external services" paragraph); docs\ar\gaming.html (3 paragraphs: Time machine links, NOW line, Show my path)  
How to reproduce: t6.py (rendered text nodes with 4 or more lowercase English words, Chromium).  
What happens / what should happen: a privacy policy shown in a language should be in that language, at least the "what it sends" and analytics parts. Translate them, or link to the English section.  
Confidence: confirmed (Playwright text scan)

### 12. Docs and release-note claims that don't match the code
Severity: cosmetic  
Where: CHANGELOG.md line 15 (1.1.6 notes, published as the release body: "stock movers once a day for everyone (or your own key at your own pace)". The PC app has no keyless stock data: FullView.Markets.cs line 351 "Without one this stays empty"; PLAN-1.1.6 says the shared copy is still refused); SECURITY.md line 3 and .github\pull_request_template.md ("no shell") while 1.1.6 ships AdeeboShell; docs\THEORY-OF-EVERYTHING.md line 151 ("332 terms") vs line 232 and knowledge\terms.txt (407); index.html self-test mock "engineer view opens < 1 s" vs the test "under 2 s"; src\HallOfFame.cs line 15 comment ("Nothing is sent by AdeeboMeter") although the card goes through the relay; FINDINGS-PC-1.1.6.md line 5 path "Documents\AdboMeter".  
How to reproduce: read the lines named.  
What happens / what should happen: correct the release body before tagging (release.ps1 copies the CHANGELOG section verbatim), and align the numbers.  
Confidence: confirmed (read; terms counted with `grep -c "^== " knowledge/terms.txt`)

### 13. The 100 Arabic Learn pages aren't in the sitemap
Severity: cosmetic  
Where: docs\sitemap.xml (has bn/es/fr learn pages and ar/ top pages; no ar/learn/*), while each ar/learn page is indexable with a canonical link and hreflang  
How to reproduce: links.py-style comparison (103 pages not in the sitemap: 100 ar/learn, 404, and a few deliberate ones).  
What happens / what should happen: add ar/learn to the sitemap generator (tools/arabic-seo.py), as was done for the other languages.  
Confidence: confirmed (script)

## Still present (from round 4)

| Round-4 # | What | State at 999a100 |
|---|---|---|
| 32 | Hall of Fame keeps more than it says | hall-of-fame.html line 211 / issue form line 11: "Nothing else is shown". hall-of-fame.json (public) still stores code, by (GitHub login) and ref (issue URL); the public code is what makes finding 1 possible |
| 33 | Forbidden company named in a plan file | moved: PLAN-1.1.5 line 8 is clean, but HANDOVER-SEO.md line 7 names it (finding 7) |
| 99 | "Email it" placeholder | hall-of-fame.html line 240 `MAIL = 'HALL-OF-FAME-EMAIL'`; the button is hidden, so the page no longer promises an address; the app's text was not checked here |
| 101 | Self-test proves less than its titles | memory check now 120 MB against the site's "80 MB budget" (finding 6) |
| 114 | Numbers out of date | ToE "332 terms", "93" vs "104" checks (findings 6 and 12) |
| 136 | HOF page `SITES[String(w.site)]` | unchanged (line 265); only a hand-edited JSON can hit it |

Fixed since round 4 (checked): #5 (HOF_KEY only for release or tag dispatch, build.yml line 44); #15 (.gitignore has dist/, data/, *.pyc, *.sha256, portable and policy files; no .pyc tracked); #100 (claim codes are folded to ASCII and checked against `[A-Z0-9-]` before compare_digest); #43 (Valve games keep "exact" servers from games.txt, Gamer.cs line 90); #110 (DNS failure remembered 30 s, in-flight probes skipped, Gamer.cs lines 496 and 570); #109 (week summary read off the paint thread, weighted, Gamer.cs lines 719-750); #137 (no TESTPLAN reference left in FullView.DeepTest.cs).

## Checked and held up

- **CI permissions and injection.** build.yml: contents:read by default, write only in the release job, which never runs the build. No pull_request trigger, so forks never get HOF_KEY. Every action is pinned by SHA. The tag reaches PowerShell through env and is regex-checked. hall-of-fame.yml passes every issue field as env, never `${{ }}` in run lines; the bot comment can't @mention (clean_name allows no "@"); the push retry re-checks that the pushed entry carries this claimant's name. shots.yml: persist-credentials false; the token is used only in the last step.
- **release.ps1** takes the version from Program.cs, refuses a missing CHANGELOG section and a dirty src/knowledge/assets, deletes obj\HofKey.cs even when the build fails, hashes every file in the zip folder (LF line endings), and writes notes and SHA256SUMS. One small gap: docs\lab.html and the ToE PDF are embedded or zipped but are not in the dirty check.
- **finish-release.ps1** checks every SHA256SUMS line format and hash before signing, stops if the notes don't carry GitHub's exe hash or the "from this exact source." sentence, re-downloads and checks the hashes and the Authenticode signature. **sign.ps1** picks signtool by version and timestamps with SHA-256. **store/pack.ps1** refuses a suffix or a non-zero 4th number and an exe whose ProductVersion differs (AssemblyInformationalVersion = Program.Version, so it matches).
- **ci_checks.py** runs clean on 999a100 (29 scripts parsed, 256 words agree). It only parses docs/*.html, not docs/xx/*.html; no breakage found there.
- **Website (Playwright, 360/768/1366 × light/dark).** index, privacy, app, hall-of-fame, gaming, learn/, remote, games, files, live, trading, 404, and es/fr/bn/ar index and privacy, ar/gaming, ar/learn/dns: no console errors, no 4xx, no horizontal scroll, no broken in-page anchors. The only overflow is finding 9. Static check of all 555 pages: no missing local file and no missing #anchor. Every sitemap URL exists. robots.txt is fine. Arabic pages carry dir=rtl; the language-tile strip is a deliberate scroller and scrolls correctly in RTL.
- **Analytics.** On 127.0.0.1 nothing loads. On adeebometer.com (host-resolver map) the Cloudflare beacon loads on public pages only. It doesn't load on app, games, remote, live, files, r or lab, and it is skipped with navigator.doNotTrack="1" or globalPrivacyControl=true. Other external hosts match the privacy page: api.github.com and get.microsoft.com (home), challenges.cloudflare.com (Hall of Fame), the Live worker (app, live, remote).
- **sw.js.** V matches a fresh hash of CORE+EXTRA (4e3a3c7f6b83), no listed file is missing, old am-* caches are deleted on activate, other origins are never touched, and a captive-portal page can't replace a kept one. Only the first-load mix in finding 10.
- **Language memory.** Visiting any /ar/ (or bn/fr/es) page stores that language, and English URLs then redirect to it (locale.js lines 40-43, 56-64). This looks deliberate; noted only because a visitor who opens one Arabic link sees the English site in Arabic until they use the picker.
- **Gamer.** Consent gates (Plan, Pick, GameCompare and ConsoleCheck all need a clicked game). games.txt parsing (host regex, port range, 500 lines, 12 servers, unknown lines kept). The Valve list (2 MB cap, never a cut-off copy, IPv4 only, region container skipped). Probes (one lookup per 5 min, failures 30 s, BeginConnect handle disposed, 4 s overrun filed as a miss). History roll-off with History off (TidyNow). gz line and size caps. Session thinning after 4 h. Report redaction (private, CGNAT, link-local, EUI-64, MAC, Wi-Fi name). MatchFinder (own addresses excluded, deterministic tie-break). GPN verdict (unanswered hops never "where it starts"). Compare (45 s cap, too-few-probes verdict, duplicate paste refused).
- **Labs.** LabSim: CUBIC/Reno per RFC 9438/5681, the BDP floor, M/D/1 queue, the Subnet quiz always has 4 distinct options, CwndFromCapture bounded at 2000 bins. LabViews: JSON escapes `"` `\` `<` and control characters, cut at 400 characters, the "+N more" box at 50 devices. LearnGames: everything simulated; the quick-fire timer is monotonic and refuses answers after 60 s. The Snappy decoder bounds every literal and copy (only the table entry overflow in finding 8).
- **hof_verify.py.** Name cleaning matches HallOfFame.CleanName (NFC, marks capped at 2, 32 code points); mixed-script refusal; date range; points 500-1200 (Points.Max 1200; the reflection census on r6 says 1,094 can be earned); GitHub handle must be the sender. A damaged JSON gives a plain error.
- **Rule 8** elsewhere: the exe, the knowledge packs, docs (including the PDF text) and the binaries are clean. Spanish "últimos" was the only false hit for "TiMOS".
- **Rule 10 note** (outside this slice, for the lead): the replay bonus is +3 (Points.ReplayPts = 3; the HOF page and the self-test say +3), while the brief's rule 10 says +2. Consistent everywhere, so it is probably a deliberate change of the rule.
