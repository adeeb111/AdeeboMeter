# S8 - UI (rest), SelfTest, CI, docs - round 4 (e80412e)

Method: read every line of the 16 C# files in this slice, both workflows, hof_verify.py, the issue templates, release.ps1, build_exe.cmd, store/, .gitignore, tools/, and the docs. What I ran:
- hof_verify.py with a dummy HOF_KEY on a copy, with hostile issue bodies: a 65 KB name, HTML, RTL override, 31 combining marks, Cyrillic look-alikes, 99999 / 900 points, Arabic-Indic digits, a duplicated `### Name` section, a junk author, `..` handles, and a non-ASCII claim code. Test script: `scratchpad/review4/s8/t_hof.py`.
- The C# and Python claim-code vectors, through r4.exe and hof_verify `--code`.
- A tool, topic and points census through reflection on r4.exe (`s8/max.cs`, `s8/tl.cs`).
- docs/ served locally and tested with Playwright and Chrome at 360, 768 and 1366 px, light and dark: console errors, horizontal overflow, broken anchors, local links and WCAG contrast (`s8/web.py`). hall-of-fame.html was also tested with a hostile hall-of-fame.json (`s8/hof_web.py`).
- `git check-ignore` on the paths the build and portable mode create.

### 1. `--deeptest --local` connects to company domain controllers and to the internet
Severity: security  
Where: src\Ui\FullView.DeepTest.cs line 90 (netNoInput) and lines 91-97 (section 2, "Real run"). The header comment at lines 14-17 promises "never your company's servers", and `--local` prints "local only".  
How to reproduce: on a domain-joined PC run `AdeeboMeter.exe --deeptest --local`. Section 2 runs every tool that has no input boxes, unless it is in ByHand, LabGate or SenderGate. Only six of those tools (`dnsrules teams clock cloud doh hairpin`) are skipped when `net` is false.  
What happens / what should happen: these still run for real:
- `dc`: SysTools.DomainControllers. It queries the company DNS for `_ldap._tcp.dc._msdcs.<domain>`, then makes TCP connections to ports 88, 389, 636, 445, 135 and 3268 on up to 3 domain controllers.
- `ipv6score`: resolves www.google.com and pings 2606:4700:4700::1111.
- `hotel`: an HTTP GET to www.msftconnecttest.com.
- `tonight` and `prematch`: ping the game servers of monitored games.
- `vpn`.
- `dupip`.

This is the "company network" traffic that the deep test, and the Traffic Lab tick, exist to avoid. Should: for `--local`, run only tools that are proven local (an allow-list, not a skip-list), and add `dc` to ByHand. ByHand's comment (line 23) also points to a missing TESTPLAN-1.1.md (see #16).  
Confidence: suspected (read it: the tool list was dumped from r4.exe, and each Do body was read)  
Rule: 1

### 2. Every push to main, store or dev-* builds an "official" exe with the Hall of Fame key and publishes it as an artifact
Severity: security  
Where: .github\workflows\build.yml lines 10-11 (push: main, store, "dev-*") and lines 39-41 (HOF_KEY passed to release.ps1). release.ps1 lines 41-49.  
How to reproduce: push any commit to any `dev-*` branch, then download Actions > the run > Artifacts > AdeeboMeter.  
What happens / what should happen: the artifact's exe has the HOF_KEY compiled in. A public repository's artifacts can be downloaded by any signed-in GitHub user. Work-in-progress builds, including ones with known rule-10 holes, therefore sign genuine award cards. The secret also reaches release.ps1 from any branch that someone with push access creates. Should: pass HOF_KEY only for `release` events or a dispatch with a tag. Push builds should stay "unofficial build".  
Confidence: suspected (read it)  
Rule: 10

### 3. .gitignore misses the build, portable-mode and release outputs, and a .pyc is already committed
Severity: security  
Where: .gitignore; .github\scripts\__pycache__\hof_verify.cpython-313.pyc (tracked since 4fd6e22); build_exe.cmd line 23; release.ps1 lines 54-74  
How to reproduce: `git check-ignore --no-index` on these paths reports NOT ignored:
- `dist/AdeeboMeter-1.1.5-win64.zip`, `dist/SHA256SUMS`, `dist/RELEASE-NOTES-1.1.5.md`
- `AdeeboMeter.exe.sha256`
- `data/points.txt`, `AdeeboMeter.portable`, `AdeeboMeter.policy`
- `sample_chip_light.png`
- `.github/scripts/__pycache__/x.pyc`

What happens / what should happen: build_exe.cmd writes AdeeboMeter.exe.sha256 into the repo root, and release.ps1 writes multi-MB zips to dist\. Running portable from the checkout (the documented way to test) puts history, air.txt (neighbours' BSSIDs, a location trail), points.txt, keys-ec.txt and recipients\ under data\. One `git add -A` publishes all of it, and history can't be cleaned without a rewrite. The committed .pyc has no secret in it (checked with strings), but it is a binary copy of an older hof_verify.py. Should: `git rm --cached` the .pyc, and add `__pycache__/`, `*.pyc`, `dist/`, `data/`, `*.sha256`, `AdeeboMeter.portable`, `AdeeboMeter.policy` and `sample_*.png`.  
Confidence: confirmed (ran git check-ignore on e80412e)

### 4. Time machine paints the internet red ("no answer") for every minute the internet check didn't run
Severity: wrong result  
Where: src\Ui\FullView.Replay.cs line 198 (`netLv = double.IsNaN(net) ? 2`) and lines 226-231 (plain words). Data from src\Brain\Brain.cs lines 51 and 99 (netAvg is NaN when no probe landed in that minute).  
How to reproduce: use Preset Penny (internet every 5 minutes), or set the internet check Off (or `internet_check = off` in the policy). Open Time machine and drag across any hour.  
What happens / what should happen: on Penny 4 of every 5 minutes have no internet sample, so Internet and ISP show red "no answer". With the check off, every minute is red. The widget travels back red too (AppCtx.Rewind, line 207). When the recorded worst level is not 0, the sentence reads "the internet was slow or dropping (no answer)". An unsampled minute is "not measured" (level 3), not an outage. Gaps should show grey, and the loss column should say "not measured". This is a different cause from round-1 #50 (a router that ignores ping).  
Confidence: suspected (read the write path in Brain.Flush and the read path in Replay)

### 5. "Type an address in the box above and press Enter to see which route it takes" never shows the route, and runs a full destination check instead
Severity: wrong result  
Where: src\Ui\FullView.Travel.cs line 151 (the instruction) and line 153 (the card only uses `storyIp`). src\Ui\FullView.Story.cs line 37 (StorySite sets `storyIp = ""`).  
How to reproduce: Network story > click the Router box > type 8.8.8.8 in the box at the top > Enter.  
What happens / what should happen: StorySite clears storyIp and starts FollowPacket, a full DestCheck: DNS, TCP, TLS, HTTP and a trace to the address. The "WHICH ROUTE WINS FOR ..." card never appears for a typed address. It only appears for the auto-picked app's IP. Should: when the typed text is an IP address, set storyIp (and don't start a network check just to look up a route), or reword the help text.  
Confidence: suspected (read it)

### 6. "Email it": the app says the address is on the Hall of Fame page, but the page still has the placeholder and hides the button
Severity: wrong result  
Where: docs\hall-of-fame.html line 215 (`var MAIL = 'HALL-OF-FAME-EMAIL'`), lines 172-176 and 197. src\Ui\FullView.Points.cs line 127.  
How to reproduce: open hall-of-fame.html and look at step 3 "one of two ways". Then make a card in the app.  
What happens / what should happen: the "Email it" pill is hidden because MAIL has no '@', yet the page still says "one of two ways ... By email, paste the same text". The app's award box says "or email it (the address is on the Hall of Fame page)". The workflow's dispatch path for emailed cards can never be used by a winner. Should: put the real address in, or remove the email path from the page and the app text until one exists.  
Confidence: confirmed (Playwright: `#mail` has display:none)

### 7. hof_verify.py crashes on a claim code with any non-ASCII character, and the issue gets an empty answer
Severity: wrong result  
Where: .github\scripts\hof_verify.py line 125 (`hmac.compare_digest` on str) and line 112 (claim only has whitespace removed). .github\workflows\hall-of-fame.yml lines 186-194.  
How to reproduce: submit the form with the code pasted from Word or Outlook, where hyphens become U+2011, or with a full-width letter. `s8/t_hof.py`-style run: claim `ADB‑XXXX‑…` (U+2011).  
What happens / what should happen: `TypeError: comparing strings with non-ASCII characters is not supported`, exit 1, and nothing is written to GITHUB_OUTPUT. The workflow posts " (Edit the issue to fix it - the check runs again by itself.)" with no reason, and labels the issue "not verified". Should: normalize (NFKC, map Unicode dashes to '-'), then refuse anything outside `[A-Z2-7-]` with a clear message, before compare_digest. In the workflow, treat an empty RESULT as a server error.  
Confidence: confirmed (ran it: exit 1, empty GITHUB_OUTPUT)

### 8. A name that cleans to nothing makes a card that can never be claimed
Severity: wrong result  
Where: src\Ui\FullView.Points.cs lines 100-102 (MakeAwardCard: `name = CleanName(...)`, `Code(key, name, ...)`). src\HallOfFame.cs line 100 (`if (nm == "") nm = "A network detective"`). hof_verify.py line 113.  
How to reproduce: at 500 or more points, press Make my award card and type a name made only of emoji, symbols or punctuation (e.g. "🙂🙂", "@@@").  
What happens / what should happen: the picture shows "A network detective", but the claim code is the HMAC of an empty name. The copied text says "Name: " (empty). Typing the name shown on the card gives a mismatch, and an empty name is refused as "missing". Should: refuse an empty cleaned name in AskName (keep the dialog open with a note), so the name on the card is always the one the code was made from.  
Confidence: confirmed for the verifier side (ran hof_verify with an empty name: "missing"). App side read.  
Rule: 10 (the award path - the user can't claim a card they earned)

### 9. Self-test: three checks prove less than their titles, and the website says they do
Severity: wrong result  
Where: src\SelfTest.cs
- line 1176 "memory after everything" fails only above 96 MB, while it prints "budget 64".
- line 1198 "GDI / user handles" asserts nothing; it always passes.
- line 306: the leak test only judges when `gdi0 > 0`. If GetGuiResources fails (returns 0) it passes silently.
- line 656 "a check question per Learn topic, a challenge per lab, learning paths that exist" never checks labs. Its body checks only check questions, path keys and 4 values.

docs\index.html lines 878-879 say the self-test proves "Memory stays inside the 64 MB budget".  
How to reproduce: read the assertions. Under mono the run printed "private 119 MB (budget 64)"; that failed only because 119 > 96.  
What happens / what should happen: a build at 65-96 MB passes "memory", any GDI count passes "GDI", and a lab without a goal passes the points test. Should: fail above 64 MB (or change the claim), assert a GDI/USER ceiling, fail when GetGuiResources returns 0 on Windows, and check `LabGoals` for every lab except quiz.  
Confidence: confirmed (read every T(); counted 71 T() calls, 70 run without --samples)  
Rule: 7

### 10. Every deep test leaves an encrypted "diagnosis" file in the user's Documents that nobody can open
Severity: wrong result  
Where: src\Ui\FullView.DeepTest.cs lines 91-97 (section 2 runs "friend"). src\Checks\OverTime.cs lines 555-575 (FriendBundle writes Documents\AdeeboMeter-diagnosis-<time>.txt and encrypts it with a fresh code).  
How to reproduce: `AdeeboMeter.exe --deeptest`, then look in Documents.  
What happens / what should happen: each run leaves an AdeeboMeter-diagnosis-yyyyMMdd-HHmm.txt.adeebo whose code was only in rows the deep test throws away. Should: add `friend` to ByHand, or point it at a temp folder in deep-test mode.  
Confidence: suspected (read it)

### 11. Privacy texts miss the new revocation lookups and files written automatically, and the Hall of Fame keeps more than it says
Severity: wrong result  
Where:
- docs\privacy.html, "What it sends": no certificate revocation (CRL/OCSP), which e80412e added. The in-app About lists it (src\Ui\FullView.Settings.cs line 396).
- docs\privacy.html, "What stays on your PC": nothing about reports\Roast-*.png, therapy or letter files (src\Checks\Fun.cs lines 30-34 and 233), which are written on every run, not "only the ones you choose to save". Also missing: games.txt, game-compare.txt, cert-compare.txt, aaa-runs.txt.
- docs\hall-of-fame.html line 203 ("nothing else about you is kept") and .github\ISSUE_TEMPLATE\hall-of-fame.yml line 11. hof_verify.py line 141 stores `by` (GitHub login) and `ref` (issue URL) in the public JSON.

How to reproduce: compare the pages with the code above.  
What happens / what should happen: the published privacy policy (also the Store listing's) doesn't list a network contact that the app makes when the setting is on. It also doesn't list files the app writes without being asked. The Hall of Fame says nothing else is kept, but the GitHub login and issue link are published. Add these to the pages, or drop `by` and `ref`.  
Confidence: confirmed (grep and JSON output from the hof run)  
Rule: 1

### 12. PLAN-1.1.5.md names the forbidden company
Severity: wrong result  
Where: PLAN-1.1.5.md line 8 ("never mention or reference N...")  
How to reproduce: `git grep -n -i nokia`.  
What happens / what should happen: the only hit in the repository is the rule itself, in a file that is published with the public source and the source zip (release.ps1 line 69, `git archive HEAD`). Reword it to "the forbidden vendor (see the private brief)", or keep the plan out of the repo.  
Confidence: confirmed (git grep)  
Rule: 8

### 13. Points and docs numbers are out of date in several places
Severity: cosmetic  
Where (measured from r4.exe):
- Tools2 = 163 tools; Learn = 68 topics; terms 337; earnable 806 (Points.Max 900).
- README.md lines 32 and 37 ("158 tools", "59 topics"); CHANGELOG.md line 7 ("158 tools").
- docs\THEORY-OF-EVERYTHING.md lines 232 and 235 ("332 terms, 59 topics", 158).
- docs\index.html line 623 (59 topics).
- docs\hall-of-fame.html lines 120, 123 and 132 ("158 tools", "59 topics", "About 750 points ... two thirds").
- CHANGELOG.md line 58 ("capped at 800 (everything earnable is about 750)").
- src\Points.cs line 29 comment ("about 580 ... 500 means you've done nearly everything").
- src\Ui\FullView.Points.cs line 277 says "Nearly everything AdeeboMeter can teach" at 500 (62 %).
- Learn › Games (30 items, 100 points) is missing from both "how to earn" lists (FullView.Points.cs line 285; hall-of-fame.html table).
- hall-of-fame.html says a page needs "a click or a key, not just opening it". Time machine pays on simply opening a recorded day (FullView.Replay.cs line 150).
- index.html: "Probe - every 2 to 10 seconds" (Penny's router check is 30 s); "one line to today's 7-day history file" (default 3 days, up to 180).

How to reproduce: `mono s8/max.exe app/AdeeboMeter.exe` gives "tools 163, learn 68 topics, ... everything there is to earn: 806 Points.Max = 900".  
What happens / what should happen: update the numbers. Better, have the self-test print them and compare them with README.  
Confidence: confirmed (reflection census)

### 14. The newest features are missing from the CHANGELOG, README and website
Severity: cosmetic  
Where: e80412e touched none of CHANGELOG.md, README.md or docs\.  
How to reproduce: `git show e80412e --stat`; grep the docs for buddy, mood, roast, therapy, revocation, "Break the Adeebo Award", "packet dungeon" (0 hits).  
What happens / what should happen: the 1.1.5 release notes come from CHANGELOG (release.ps1 lines 22-26). As things stand they would leave out the certificate walk and revocation option, the buddy and hats, the moods, 3 games, and Roast, Therapy and Letter. The CHANGELOG heading is also still "## 1.1.5 (unreleased)", and release.ps1 would publish it that way.  
Confidence: confirmed (git show / grep)

### 15. Website contrast below WCAG AA in both themes
Severity: cosmetic  
Where: docs\hall-of-fame.html (td.pts #2DA44E on white, 3.22:1; th #6B7685 on #F6F8FB, 4.33:1; dark .pill.ghost 4.07:1). docs\index.html (.btn.ghost 4.04:1 light and 3.30:1 dark; .meta/.ver 4.49:1; mock chips with white on green or amber at 2.59 and 3.14; dark "All quiet" chip 2.06:1; dark .k 4.07:1).  
How to reproduce: `python3 s8/web.py` (Chrome, 360/768/1366, light/dark).  
What happens / what should happen: 26-36 text elements per page fall below 4.5:1. Darken the green to about #1A7F37 and the muted greys, and lighten the dark-mode blue links.  
Confidence: confirmed (Playwright computed styles)

### 16. Hall of Fame page: long handles are cut off, combining marks spill over the label, and a stray "site" value hides the list
Severity: cosmetic  
Where: docs\hall-of-fame.html lines 59-60 (.winner overflow:hidden, link not wrapping) and line 240 (`SITES[String(w.site)]` reads Object.prototype keys).  
How to reproduce: `python3 s8/hof_web.py normal` and `... proto` (screenshots `s8/hof_*.png`).  
What happens / what should happen:
- A 253-character Bluesky handle or 100-character LinkedIn handle, both accepted by hof_verify, is clipped mid-word.
- A name of "Z" plus 31 combining marks draws up over "★ ADEEBO AWARD".
- An entry with `"site":"constructor"` (only possible by hand-editing the JSON) throws in forEach: the later cards vanish and the count says "Couldn't load the list".

Should: `overflow-wrap:anywhere` on the link; cap combining marks per base letter in clean_name and CleanName; use `Object.prototype.hasOwnProperty.call(SITES, site)`.  
Confidence: confirmed (Playwright)

### 17. ByHand points to a test plan that doesn't exist
Severity: cosmetic  
Where: src\Ui\FullView.DeepTest.cs line 23 ("checked by hand (TESTPLAN-1.1.md)")  
How to reproduce: `git ls-files | grep -i testplan` finds nothing.  
What happens / what should happen: the deep-test report says 40-odd tools are "checked by hand" against a plan that isn't in the repository. Add the plan, or point at docs\LIVE-TESTS.md.  
Confidence: confirmed

## Still present from earlier rounds

| earlier id | where now | one-line evidence |
|---|---|---|
| R2 #5 (impersonation) | .github\scripts\hof_verify.py line 45 (same_person: NFKC+casefold only) | Cyrillic "Аda Lovelace" was `added` next to "Ada Lovelace" (ran it); 32 combining marks also accepted |
| R2 #4 (key ships in exe) | src\HallOfFame.cs lines 31-35, release.ps1 line 44 | Unchanged by design; #2 above widens it to every push build |
| R1 #50 (router that ignores ping painted red) | src\Ui\FullView.Replay.cs line 197; FullView.Story.cs line 85 | `lanLv = double.IsNaN(gw) ? 2` |
| Muse R2 #45 (dead "no neighbours" message) | src\Ui\FullView.Link.cs lines 91-93 | `if (ls.Wifi) ...; else if (ls.Wifi && ...)` can never run |
| 1.1.4 05-ui (BootWhen per paint) | src\Ui\FullView.Inside.cs lines 91-100 | Process.GetProcessById(...).StartTime for 15 rows on every list paint |
| R1 "never listens" vacuous / pass-on-no-throw | src\SelfTest.cs lines 229, 238, 239, 240-246 | never-listens checks before any view opens; gateway ping, health and destination only print |
| R1 hotkey test | src\SelfTest.cs lines 1170-1175 | only checks Ctrl+Alt+A is free; skips when another copy runs |
| R2 #75 ("from this exact source") | release.ps1 line 104; SECURITY.md "built ... from the public source"; README line 54 | official builds compile obj\HofKey.cs, so a rebuild never matches |
| R2 #77 (privacy "stays" list) | docs\privacy.html | Points and air.txt added now; game list, game-compare, cert-compare, aaa-runs, overlay.png still missing (see #11) |
| R1 version strings | src\Program.cs lines 115-116 ("1.1.4") | website, README and JSON-LD say 1.1.5; release.ps1 and pack.ps1 take the version from Program.cs |
| R1 #213 / R2 #126 (counts) | README, CHANGELOG, THEORY, site | new numbers, same class (see #13) |

## Checked and held up

- Workflows: no `${{ }}` reaches a `run:` line. Issue data and inputs go through env vars. Every action is pinned to a commit SHA. The build job is `contents: read` with `persist-credentials: false`. Only the release job can write, and it checks the tag against `^v[0-9]+(\.[0-9]+){1,3}$` and RELEASE-NOTES-<ver>. A dispatch with a tag builds that tag (round-1 #29 fixed). dev-1.1.5 is built (`"dev-*"`). There's no concurrency group, so there's no silent cancel (R2 #50 fixed). Retry-on-conflict re-runs the verify, and success is checked against the pushed file.
- hof_verify: 99999 is refused (500-900, matches Points.Max 900). HTML, RTL overrides and control characters are stripped. A 65 KB body is handled in milliseconds. A duplicated `### Name` section takes the first one. A junk author is blanked. `ref` must match the repo's issue URL. Handles must fit each site's regex and the page rebuilds the link itself (javascript: handles never become a link; tested). Codes are de-duplicated. Same-name detection works across case and width. Arabic-Indic digits in points are harmless (int() gives the same number).
- C# HallOfFame.Code and CleanName agree with hof_verify on all 4 self-test vectors, including astral, Devanagari and decomposed accents (ran both). Truncation to 32 code points is the same on both sides and idempotent. AskName MaxLength 40 then CleanName gives the card, the copied text and the code the same name.
- hall-of-fame.html writes every field with textContent: no XSS from the JSON (an `<img onerror>` name rendered as text, no dialog). Mixed RTL and Latin names lay out correctly. 32-character names wrap. No horizontal scroll at 360 px.
- Website at 360, 768 and 1366, light and dark: no console errors or page errors (the only 404 was a video I deleted from my local copy), no horizontal overflow, no broken in-page anchors, and every local link and asset exists. The update checker only echoes a version that matches `^\d{1,3}(\.\d{1,4}){1,3}$`.
- Self-test count: 71 T() registrations (70 without --samples), matching the "71 checks" claim. The Security count of 42 matches. Jobs per minute printed: penny 8, balanced 67, gamer 71 plus game pings, which matches "8·67·~100".
- No HOF key anywhere in the repo, its history or the committed .pyc (strings).
- UI files: every background Task posts back with BeginInvoke inside try/catch. Busy flags are cleared on every path (link, inside, travel, wars, week, procs, security, walk test). Replay, week and anatomy bitmaps are disposed when their key changes. Sparks stop their timer when empty. Points earning in paint is guarded by Cfg.Points and awards once. Example days never pay page.replay (`!replayExample`).
