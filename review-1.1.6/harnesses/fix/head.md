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
