# Round 2: Hall of Fame, Adeebo Points, Windows Update break, Neighbour wars, DevInfo (a0fbd56)

I only read and ran things. Nothing in /home/user/AdeeboMeter-dev was edited, and nothing was sent to GitHub.
All harnesses are in `/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/review2/harnessH/`.
To act as an official build, I compiled a0fbd56 with mcs. I added an `obj/HofKey.cs` made exactly as `release.ps1` makes it, holding a **dummy** key ("dummy-review-key-…", not the real one). That build is `off/official.exe`, and the file was deleted after the build. The real key is not in this checkout or in its git history. No `secrets/`, `obj/HofKey.cs` or `HALL-OF-FAME-SETUP.txt` exists on this machine (I ran `find /` and `git log -p --all -S`). This report contains no secret value.

## How the scheme works

- **Signing:** the claim code is `ADB-` plus the first 80 bits of HMAC-SHA256(key, `"v1|" + CleanName(name) + "|" + total + "|" + yyyy-MM-dd`) in base32. It is symmetric: the same key both creates and checks a code.
- **Where the key lives:** the key is the `HOF_KEY` repo secret, or `secrets\hof.key` on Adeeb's PC. `release.ps1` XORs it with `(i*73+41)&0xFF` and compiles it into the exe through `obj/HofKey.cs`, so **every official exe carries the key**. The verifier (`hof_verify.py`, through `HOF_KEY` in `hall-of-fame.yml`) computes the same HMAC.
- **What the code covers:** only name, points and date. `points.txt` is plain text with no MAC.

---

### 1. You can get a valid award without extracting the key: add 250 junk lines to points.txt and the official exe signs a genuine 500-point card
Severity: security (rule 10; the Hall of Fame can be farmed with a text editor)
Where: src\Points.cs line 84 (`Total`), line 41 (`Value`); src\Ui\FullView.Points.cs line 91 (`MakeAwardCard`)
How to reproduce: write `zz0|1` … `zz249|1` (250 lines) into `points.txt`, or 50 lines `path.fakeN|1`. Start the official exe and press Make the card.
What happens: `Total` adds `Value(id)` for **every key in the file**, and `Value` returns 2 for any id it doesn't know (10 for any `path.*`). The total comes out as exactly 500, and `MakeAwardCard` signs it with the real key.
- I ran this with the dummy-key build: Points.Total=500, then `HallOfFame.Code` and `HallOfFame.Card` gave `forged-card.png`.
- I fed that name, points, date and code to `hof_verify.py` in an issue-form body. The result was `added`.
- 20,000 `path.N` lines gave Total=200000.
- A line `ping|1|<ticks>` adds the +3 replay bonus to a tool that has no replay.

None of this needs reverse engineering. The "honest limit" comment in HallOfFame.cs speaks of "a determined person". In fact anyone who opens a .txt file can do it.
What should happen:
- `Total` should count only keys that `PtKeyKnown` accepts, and count the replay bonus only for ids in `Replay`.
- Cap the total at the real maximum.
- Ideally, put the earned keys into the signed message (or MAC points.txt). A MAC is still breakable, but a typed number would then not be enough.
Confidence: confirmed (ran: harnessH/pts.exe and card.exe against off/official.exe, then hof_verify.py; saw `added`)
Rule: 10

### 2. Anyone with the official exe can forge any card: the key is in the exe and can be called through reflection; the XOR mask can be undone without knowing the key's length
Severity: security
Where: src\HallOfFame.cs line 31 (`Key`, public), line 34 (`Unmask`), line 49 (`Code`, public); release.ps1 lines 41-46
How to reproduce:
- (a) `Assembly.LoadFrom("AdeeboMeter.exe")` → `HallOfFame.Key` → `HallOfFame.Code(key, "Adeeb Khan", 99999, "2026-11-12")`. That is about 6 lines of C# (harnessH/forge.cs).
- (b) Without .NET: the masked bytes sit as one contiguous array-initializer blob in the PE. Sliding the public `(i*73+41)` mask over the file shows a printable run that starts with the key (harnessH/extract.py found 4 overlapping candidates, all starting at the same offset). `strings` does not show it.
What happens: the code a forged card carries is accepted by the workflow. I ran this: a card for name "Adeeb Khan" with 99999 points was `added`.
- **Where Muse is right:** this is acknowledged in the code ("a speed bump, not a lock").
- **What Muse missed:** finding 1 makes the key irrelevant, because the app's own button signs whatever points.txt says. Also, `Key` and `Code` are public, so a forger never has to look at the mask at all.
- A symmetric key that ships to every user can't be fixed by masking it harder. Treat the claim code as a typo check only, and do the real gate by hand (see 3).
What should happen: describe the code honestly on the page and in the form ("checks the card wasn't edited, it doesn't prove the points"). Add plausibility checks (3), and have a human moderate before publishing. If a real check is wanted, the only sound option is server-side evidence, which this offline app deliberately doesn't have.
Confidence: confirmed (ran: forge.exe and extract.py against off/official.exe, dummy key)
Rule: 10

### 3. Verifier accepts impossible totals and repeat entries, and publishes with no moderation
Severity: wrong result / abuse
Where: .github\scripts\hof_verify.py line 74 (`\d{3,5}`), line 83, line 95 (dedupe by code only)
How to reproduce: submit valid codes for "Adeeb Khan" with 99999 points, then 99998, then the same with a lowercase or spaced code. Submit "Zed" with points `00500` dated tomorrow.
What happens:
- 99999 points is `added`, although the app's real maximum is about 580.
- The same name with 99998 is a second `added` entry.
- The lowercase or spaced code is `already` (handled correctly).
- `00500` is accepted.
- Any name gets straight onto the public page with no human step: impersonation ("Adeeb Khan", or Cyrillic "Adееb"), profanity, or a domain-shaped name such as `free-prizes.example.com`, which GitHub autolinks in the bot's comment.
- The "Your card" picture only has to be some github.com/user-attachments URL. It is never checked to be the card, so any uploaded image gets linked from the official page.
What should happen:
- Reject totals above the real maximum (about 600).
- Keep one entry per name (case-folded, NFKC) or per issue author.
- Add the entry as a PR, or behind a "approved" label set by the owner, rather than pushing to main straight away.
- Record the issue author's login in the JSON.
Confidence: confirmed (ran hof_verify.py with HOF_KEY=dummy against copies in harnessH/gh)
Rule: 10

### 4. Two Hall of Fame submissions at once: the pending one is silently cancelled
Severity: wrong result
Where: .github\workflows\hall-of-fame.yml lines 20-22 (`concurrency: group: hall-of-fame, cancel-in-progress: false`)
How to reproduce: open three hall-of-fame issues (or edits) within the time one run takes.
What happens: `cancel-in-progress: false` only protects the *running* job. GitHub keeps at most one *pending* run per group and cancels the older pending one when a new run queues. So run #2 is cancelled, and that issue gets no comment, no label and no entry. The person sees nothing happen.
- An attacker can do this on purpose by repeatedly editing a hall-of-fame issue, which starves other people's submissions.
- The comment "so two cards never race" is true for the file but not for the submissions.
What should happen: either push with a rebase-and-retry loop and no concurrency group, or add a scheduled sweep that re-checks open hall-of-fame issues that have no bot comment.
Confidence: suspected (GitHub-documented concurrency behaviour; I can't run Actions here)
Rule: -

### 5. Check questions: "right the first time" isn't enforced, so a restart pays for a wrong first answer
Severity: wrong result (farming)
Where: src\Ui\FullView.Learn.cs lines 107-128 (`PaintCheck`, `checkPick` is in memory only)
How to reproduce: pick a wrong answer. The card shows "Not quite - the answer is: X (you can try again next time AdeeboMeter starts)". Restart, and pick X.
What happens: +2 every time. Across 46 topics that is about 90 points, roughly 16% of the maximum, with no learning. The option order is fixed (`Hash(id+o)`), and the right answer has just been shown. hall-of-fame.html and the Points page say "+2 for answering right **the first time**".
What should happen: save a `check.<id>` "tried" marker (0 points) when the first answer is wrong, and never pay that question afterwards. Otherwise change the wording to "+2 when you get it right".
Confidence: confirmed by reading (WinForms paint path; the UI can't run under mono, because Theme.MakeIcon throws)
Rule: 10

### 6. Replay bonus +3 without waiting 10 minutes: restart and move the clock forward
Severity: wrong result (small farming)
Where: src\Points.cs lines 107-111 (`Award`)
How to reproduce: run MTR (+4) and close the app. Set the Windows clock forward 11 minutes, start the app and run MTR again.
What happens: +3. `firstMono` lives only in memory, so after a restart only the wall clock is checked. At most 5 tools × 3 = 15 points.
What should happen: if this matters, store the boot-relative tick or the first-run session id with the entry. Otherwise say on the page that the 10 minutes can be cheated (low priority).
Confidence: confirmed (ran pts.exe: new Points instance, wall +11 min, steady +1 min → 3)
Rule: 10

### 7. Windows Update break blames an update installed *after* the problem started, and with daily Defender updates one fault matches four install days
Severity: wrong result
Where: src\Checks\UpdateBreak.cs lines 58-70 (`Compare`), line 94 (`worst` choice), line 114 (`Background`, the Overview line)
How to reproduce: History where ping goes from 20 to 60 ms on day D and stays there. Installs on D-3 … D+3 (Defender Security Intelligence arrives through Windows Update as event 19 every day on most PCs). harnessH/ub.exe:
```
install on D-2, D-1, D, D+1 : Lv=2 Strong=True Stayed=True "things got clearly worse starting the day after"
install on D-3, D+2, D+3    : no change
```
What happens:
- **Install on D+1:** the "before" window is D, D-1 and D-2. Its median is the 2 good days, so a change that came a day after the slowdown is reported as "worse starting the day after".
- **Install on D-2:** the day after it (D-1) was still fine, yet the sentence says it started then.
- **Which install gets blamed:** all four ties are equal on Lv, Stayed and ping delta, so the newest one is chosen: the D+1 install, which **post-dates** the problem. It goes into the Verdict, and with Defender daily it is "KB2267602 (Defender definitions)". The Overview banner says the same.
What should happen:
- Require the day before the change to be good and the day after to be bad (a real step at d0).
- Ignore Defender definition updates or show them separately.
- When several neighbouring install days all match, blame the earliest one, or say "it started around D; several things were installed that week".
Confidence: confirmed (ran Compare through reflection with synthetic days)
Rule: -

### 8. Plugging in a USB stick counts as a "driver change"
Severity: wrong result
Where: src\Checks\UpdateBreak.cs line 29 (`EventID=20001` with no provider or status filter)
How to reproduce: plug in a new USB device or phone. UserPnp logs event 20001 ("Driver Management concluded … status 0x0"). This includes failed installs (status ≠ 0).
What happens: it becomes a "driver" Change. It goes into the heatmap dots and can be blamed under 7's logic, with the advice "Device Manager › Roll Back Driver".
What should happen: only count 20001 events with status 0 for classes Net, Bluetooth or USB controllers, or don't count device arrivals at all. Filter on `Provider[@Name=…]` for both event IDs.
Confidence: suspected (read; event semantics from Windows)
Rule: -

### 9. Neighbour wars: 5 GHz overlap ignores channel width, so "nobody else is on channel 44" with a strong 80 MHz neighbour there
Severity: wrong result
Where: src\Link\LinkInfo.cs line 458 (`Overlaps`: `a == b` on 5 GHz); src\Link\AirLog.cs lines 77, 83 (the Width it records is never used)
How to reproduce: Analyse 7 days of your AP on ch 44 at 80 MHz, -60 dBm, and a neighbour on ch 36 at 80 MHz (covering 36-48), -45 dBm.
What happens: "You were the strongest on your channel 7 of 7 days - nobody else is on channel 44." Quiet channel = **40**, which is inside the same 80 MHz block. Most 5 GHz routers today use 80 MHz.
What should happen: work out each AP's occupied block from channel and width (20/40/80/160), and treat overlapping blocks as sharing.
Confidence: confirmed (ran harnessH/air.exe through reflection)
Rule: -

### 10. Neighbour wars: when your router changes channel, the verdict uses the old one and recommends the channel you're already on
Severity: wrong result
Where: src\Link\AirLog.cs line 60 (`MyChannel` = most-hours channel), lines 78 and 84
How to reproduce: your AP is on ch 6 for 5 days and on ch 11 for the last 2 days (auto channel). A neighbour sits on ch 6.
What happens: "You were the strongest on your channel 5 of 7 days, and 1 neighbour **now** shares channel 6." You are not on 6 any more. The 2 days on 11 count as lost. It does not notice that your router has already moved away. In a related case, a mesh or same-SSID AP that is stronger on another channel is merged into "your" network (keyed by SSID and band). That gives "strongest 0 of 7 days - nobody else is on channel 6", which contradicts itself.
What should happen: use your newest channel for "now", compare per day against the channel you were on that day, and key "mine" by BSSID rather than by SSID.
Confidence: confirmed (ran air.exe, scenarios B and C)
Rule: -

### 11. air.txt (neighbours' BSSIDs and SSIDs) outlives "7 days" once History is turned off, is on no privacy page, and logs every place the laptop goes
Severity: security (privacy)
Where: src\Link\AirLog.cs lines 23 and 34-36 (pruning only happens inside `Record`, which returns at once when History is off); docs\privacy.html line 45; src\Ui\FullView.Settings.cs line 132 (History help text)
How to reproduce: leave History on for a week while travelling, then turn History off.
What happens:
- Up to 40 BSSIDs per hour, from home, office and cafés, stay in air.txt **indefinitely**, because nothing prunes it when History is off. BSSIDs can be geolocated, so this is a 7-day (or longer) location trail.
- Neither privacy.html nor the History setting mentions air.txt, neighbours' networks or BSSIDs. Only "Delete all saved history" removes it, and that link is shown only while `HistDaysCached() > 0`.
- Muse called this "History-gated, 7 days". That is right for writing and wrong for keeping.
What should happen:
- Prune or delete air.txt when History is turned off, and at start-up.
- Store only your own SSID and BSSID plus hashed or aggregated neighbour data (per channel: count and strongest RSSI is enough for the verdict).
- List air.txt on the privacy page and in the History help text.
Confidence: confirmed by reading (pruning path); privacy page checked with grep
Rule: 1 (disclosure) / privacy

### 12. The setup file with the key isn't covered by .gitignore
Severity: security (suspected)
Where: .gitignore (only `secrets/` is ignored)
How to reproduce: if `HALL-OF-FAME-SETUP.txt` (which the owner says holds the key) is in the repo root, `git add -A` or `git add .` stages it.
What happens: the key, which is already extractable as described in 2, would also end up in public history, from which it can't be removed without rewriting. I couldn't find the file on this machine.
What should happen: keep the file under `secrets/`, or add `HALL-OF-FAME-SETUP.txt` and `*.key` to .gitignore. After any slip, rotate HOF_KEY. Rotating also invalidates codes made by older official builds, so plan for that (a `v2|` prefix).
Confidence: suspected
Rule: -

### 13. Names lose letters on the card: astral-plane letters are dropped, Indic and Thai vowel signs are stripped, and the C# and Python cleaning differ on raw input
Severity: wrong result (low)
Where: src\HallOfFame.cs line 43 (`char.IsLetterOrDigit` per UTF-16 unit); hof_verify.py line 31 (per code point)
How to reproduce: name `𠮷田` or `𝐀𝐝𝐞𝐞𝐛 Khan`; `देवनागरी`; `José` typed in NFD form.
What happens:
- C# CleanName gives `田`, `Khan`, `दवनगर` and `Jose`. Python keeps `𝐀𝐝𝐞𝐞𝐛 Khan` and `𠮷田`.
- If the person types their real name into the form instead of pasting it, the codes differ and the result is "mismatch".
- Devanagari, Thai and Arabic-with-harakat names are mangled on both sides, because Mn and Mc marks are dropped.
- Muse's "verifier reproduces C# vectors" holds only for names that are already clean.
What should happen: normalise to NFC, allow Mn, Mc and Nd, iterate by code point in C# (surrogate pairs), and add a test with an astral name.
Confidence: confirmed (ran forge.exe and hof_verify.py --code on the same inputs)
Rule: -

### 14. A long name on the award card runs under the star and off the edge
Severity: cosmetic
Where: src\HallOfFame.cs line 127 (`Fit` stops at 36 px even when the text is still wider than 900)
How to reproduce: name of 32 × "W".
What happens: the name is drawn across the star and clipped at x=1200 (harnessH/long-card.png).
What should happen: keep shrinking, or wrap, or truncate with an ellipsis.
Confidence: confirmed (rendered with HallOfFame.Card under mono)
Rule: -

### 15. Website and comments say Points are off by default; the code has them on from day 1
Severity: wrong result (docs)
Where: docs\hall-of-fame.html line 114 ("off until you turn them on"); src\Points.cs line 15; src\Program.cs line 316 (`Points = true`, "on from day 1")
What should happen: make the page and the comment match the code, or change the default.
Confidence: confirmed (read)
Rule: -

### 16. build.yml still says "anyone can check the download matches the code", which is no longer true
Severity: wrong result (docs, low)
Where: .github\workflows\build.yml lines 1-2 and 35-36
What happens: official builds now contain bytes that aren't in the source (obj/HofKey.cs), so a rebuild from the source never matches the release hash. The provenance attestation still holds.
What should happen: say so, and publish the hash of the build without the key, or explain the difference.
Confidence: confirmed (read)
Rule: -

### 17. Small items in the workflow
Severity: low
Where: .github\workflows\hall-of-fame.yml lines 47-69
- `gh issue edit --add-label "not verified"` fails if that label doesn't exist in the repo. The comment is posted first, so it's cosmetic. The `hall-of-fame` label must also exist, or the form's label isn't applied and the workflow never runs. (suspected)
- If `git push` fails (main moved because Adeeb pushed during the run), the "Answer" step is skipped by the implicit `success()`. The issue stays open with no comment and no entry, and there is no retry. (read)
Rule: -

---

## Where Muse is right or wrong (Hall of Fame and the items it raised in my area)

- **"The XOR-masked key rides in the exe by design (speed bump)"**: right, and I confirmed both ways of getting at it (reflection, and a blind XOR scan). Muse understates the impact, though. Forging doesn't even need the key: 1 (points.txt) gives a genuine signed card from the official button. That should be High, not an "honest limit".
- **"hof_verify gets the body via an environment variable, so no injection"**: right. I also checked the `GITHUB_OUTPUT` heredoc. The name can't contain a newline after `clean_name`, so `EOM` can't be injected, and `result` is a fixed word. There is no `pull_request_target`, the workflow default is `contents: read`, and the job's `contents: write` only commits docs/hall-of-fame.json from a checkout of main. The secret is masked and never printed.
- **"hall-of-fame.html uses textContent only"**: right. I confirmed it with Playwright: a JSON with `<img onerror>`, `<script>` in code, a `javascript:` picture, a picture URL with a quote break-out, an RTL override, a 5000-char name, `points:"1e9"`, `-5` and an object at 1366 and 360 px. No script ran, no `<img>` was created, the only link was a well-formed user-attachments URL, and there was no horizontal scroll. Two defence-in-depth gaps, both reachable only by editing the JSON directly: the page doesn't cap name length (the 5000-char card is 14,000 px tall), and it prints negative points or 1e9 as they are.
- **"The verifier reproduces the C# vectors"**: right for clean ASCII and BMP names, wrong for astral letters (13).
- **"release.ps1 takes the key from $env:HOF_KEY / secrets\hof.key"**: right. The gap is the unignored setup file (12).
- **"Points corrupt-ticks crash is fixed"**: right. I re-ran round 1's #3 against r2 with MaxValue+1 ticks, long.MaxValue, `-5`, `NaN`, empty id and pts `-3`. Nothing threw. Bad lines are skipped, and the next save drops them.
- **"Replay race: someone who sees a card first could submit it first"**: partly right. The entry still shows the owner's name, and only `ref` points at the other person's issue. It's harmless, apart from the missing author check (3).
- **"Brain.cs:108 UpdateBreak.Background() hourly, not History-gated, not in About"**: right that it isn't gated. `Brain.Second` → `Flush` runs whether History is on or off, and `Background` reads the System event log every hour and fills `ChangeDays`. It's local only (no network, nothing written), so it doesn't break rule 1 and doesn't belong under "What leaves your PC". The harm is small. It does keep feeding the Overview banner from finding 7's flawed logic, but only when History is on.
- **"Neighbour wars records… 7 days, History-gated"**: writing is gated, keeping isn't (11). Muse called the verdict logic correct; it isn't (9, 10).
- **"Windows Update break is honest"**: the wording is careful, but the matching logic still blames a post-dated install (7).

## Checked and held up

- **Workflow injection:** no `${{ }}` reaches a `run:` line. Inputs reach Python and `gh` through environment variables. `hmac.compare_digest` is used. The picture and ref regexes are anchored. The 64 KB issue-body regex is linear. Date bounds are ≥ 2026-11-01 and ≤ today+1 (a UTC+14 user is fine).
- **Brute force:** 80-bit codes and one guess per issue edit, so not practical.
- **No secret in source or history:** grepped base64 blobs, byte arrays and "key/secret". The only `Unmask(new byte` text in history is the release.ps1 template and the self-test vector.
- **Points.Award once per item:** fast repeated Run, a restart, and earning switched off all give 0 (pts.exe and the self-test lines). NoEarn senders give 0. Clock moved back, then +11 min on the steady clock, gives +3 (correct).
- **Lab challenges:** none is met on default settings (ran LabSim: qos default 60 ms wait, game 87 ms, bloat 520 ms). The route lab needs 3 real lookups from a Choice, not keystrokes.
- **AirLog parsing:** a hostile SSID with `\n` or `\t` can't inject rows (`WlanApi` → `Program.Plain` strips control and format characters, and AirLog replaces tabs). The file is bounded (40 rows an hour, `Take(20000)` on read, written atomically).
- **DevInfo:** the DEVPKEY GUIDs and PIDs are correct (Name, DeviceDesc, Class, DriverDate/Version/Provider, LastArrival 102 / LastRemoval 103). Property size is capped at 64 KB, and there are at most 3000 devices. FILETIME is range-guarded. The in-box 2006-06-20/21 date covers negative UTC offsets. It only runs on tool threads, not the UI thread. It can't be exercised on Linux (cfgmgr32).
- **UpdateBreak plumbing:** event times are UTC → local, so days line up with History. Localized event text doesn't matter, because `updateTitle` is a data field. `Short()` handles French and German KB titles (the German one only loses the "Cumulative" label). Event reads run on a Task with a 1 s `EvtNext` timeout and 80/40 caps.
