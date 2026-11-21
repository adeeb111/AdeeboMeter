# AdeeboMeter - handover to the next Claude chat

Written 21 Nov 2026, Toronto (updated 10:40): after the 637-finding bug hunt, the 55 carry-overs and the new language picker. Read this whole file first, then `PLAN-1.1.6.md`.
Owner: Adeeb Khan (adeeb111). Repo: github.com/adeeb111/AdeeboMeter. Website: adeebometer.com.

## Where things stand
```
v1.1.5      released 17 Nov, code-signed (tag v1.1.5 = 03703a5, protected)
main        = the live website (GitHub Pages from /docs) + release source; app code on main = 1.1.5
dev-1.1.6   = next version. Bug hunt done here, pushed; nothing released. Head: see git log.
              (Program.Version still says "1.1.5" - bump at release)
```
Bug hunt (report: 5 High, 31 Medium, 236 Medium-Low, 365 Low = 637), all on dev-1.1.6:
```
 High + Medium + ML-1..213 ─► fe910e1 and earlier
 ML-214..236               ─► 82c2230
 L-1..365                  ─► c730785
 self-test 86/86 under Wine after each batch
```
Since the handover commit (31ef361), all on dev-1.1.6, all pushed, self-test 86/86 after each:
```
 979f416  55 carry-overs: 44 fixed, 6 already fixed, 5 left/pushed back (section C)
 440897b  language button beside VIEW + language card + Restart; Chinese not offered
 790f656  after a real-Windows run on Adeeb's PC: short "Restart" label, tile patterns cached
```
- A dozen findings were answered with a comment instead of a code change (by design or already fixed): L-2, L-24, L-44,
  L-76, L-119, L-210, L-226, L-234, L-235, L-249/250/251/254 (code had moved on), L-252, L-256, L-260, L-276, L-313, L-324.
- Fixed from code reading only, needs a real Windows run to confirm: L-200 (ChaCha20 name on TLS 1.3), L-287 (process
  names for SonicWall / Sophos / Citrix VPN clients - a wrong name only leaves the product name blank), L-289 (Bluetooth
  code 45 = not around), L-267 (driver status 0x00000000), L-214 (infinite DHCP lease value).

## Open items (in order)

### A. Adeeb's steps (his PC)
1. Deploy the three workers again (`npx.cmd wrangler deploy`, check `name =` first):
   - `tools/remote` (phone relay): "finished" mark survives hibernation, byte-counted message size, sliding per-minute +
     hourly room limits, failed rate-counter write ends the room. Also LIMITER binding (M-14 / ML-126).
   - `tools/live`: keyed URLs never cached, movers stale after 5 days (not 4), store read/write errors logged, 0 stays 0,
     NHL "now" keyed by date, MLB season rule, $15+ five-letter W/U/R tickers kept, `/site` rate limit (M-14).
   - `tools/relay`: 502 wording; `tools/relay/wrangler.toml` needs the rate-limit namespace id pasted in.
2. 1.1.6 release flow, first real run of `tools/finish-release.ps1` together. It's stricter now: refuses a malformed
   SHA256SUMS line, refuses notes without "from this exact source."; `store/pack.ps1` refuses a "-beta" or a 4th version
   number and an exe whose version doesn't match `Program.cs`; the zip's own SHA256SUMS lists every file.

### B. Release-time drift (deferred on purpose - do at the version bump, not before)
- M-8 tool count (232 in code; README/CHANGELOG/site say 158), M-9 six Toolbox sections (docs say five), ML-76 90 Learn
  topics (docs say 59), ML-88 HOW-TO-USE section 10 ("190 tools in four sections"). (Self-test count is fixed: docs say 86.)
- Translations still to do (of 2,414 texts): French ~507, Spanish / Arabic / Bangla ~406 each. French M-11 (tool titles),
  ML-86 (About page), ML-87 (palette "Investigate"). `python3 tools/langcheck.py <code>` lists them in lang-<code>-todo.txt
  (don't commit that file) AND now rewrites the `#coverage NN` line in the table - run it for each language after adding
  translations, or the picker shows an old percentage.
- (docs/kb/r1.json is done - the 8 tools are in it.)

### C. Carry-overs from earlier review rounds - worked through (commit after 31ef361)
All 55 were gone through. 44 fixed in code or docs, 6 were already fixed by earlier commits (checked at HEAD), 5 left on purpose:
- Left for Adeeb (product/design): **website** (`docs/index.html` names none of the 1.1.5 features - Gamer Zone, Points, packet journey, Your phone, French, Labs, Report for my ISP, ARM; needs a design pass he approves).
- **Points cap: no change needed** - the report was stale. `Points.Max` is already 1,200 (raised 15 Nov); about 1,020 is earnable; the example check fails the self-test if a release ever goes past it. Adeeb said "raise the cap, new features later" - it is already raised; raise it again only when that check fails.
- Pushed back: **Engineer preset** (merged into Balanced by design, no longer offered; the self-test pins it), **certcmp ungated** (one TLS handshake, the same traffic as the ungated certlive/port/httpreq - gating only it would be inconsistent), **Setup per-monitor DPI** (the process is system-DPI-aware, so primary-monitor DPI is correct there; real per-monitor DPI is an app-wide switch that needs a real-Windows test).
- Already fixed before this pass: History in Portable mode (History.Dir uses DataDir), timers disposed (ReleaseExtras / FormClosed), Gamer preset differs from Balanced (Wi-Fi 10 s, 5 game regions every 10 s), "printer" opens the SNMP printer view, Lab gate (radius in SenderGate, SecondDhcp checks Policy.NoLab and company networks), r1.json has the 8 tools, Inspection reads Windows' own root list for new public CAs.
- Need a real Windows run: IPv6 UDP receives, CertKeys.Live timeout, Hotel Stop, speed-test abort, EvLog big events, DHCP infinite lease, game-ping Stop wake.

### D. Language picker (21 Nov) - built, tested on Adeeb's PC
```
top bar: [VIEW My setup v] [(globe) বাংলা v] [Restart]     <- Restart only while the language differs from the start one
           click -> card: one tile per language ("Welcome" in its script, own colours + pattern, % translated),
                    preview strip of real app words for the tile under the mouse / keyboard focus, arrows/Enter/Esc
           pick  -> switches at once (PickLanguage, also used by Settings) -> Restart: --restart copy waits for the
                    mutex, old copy quits via Local\AdeeboMeter.quit, new one opens the big window
```
- Code: `src/Ui/FullView.Lang.cs` (LangButton, card, LangFont: Nirmala UI for Bangla, Segoe UI for Arabic - Inter has
  neither), `L.In` / `L.Coverage` in `src/Lang.cs`, `--restart` in Program.cs + AppCtx.cs, `--view lang[:bn]` for screenshots.
- Chinese (lang-zh.txt, 4%) is not offered; the file stays. A saved "zh" opens in English.
- Tested on his PC (computer use, test copy in `Documents\AdeboMeter\test-1.1.6-lang` with run-test.bat /
  back-to-installed.bat, portable so its data stays there): picker, Bangla/Arabic text, keyboard preview, pick, Restart
  (reopened in 1 s, fully Bangla) all worked. **Not confirmed:** the preview on mouse hover (computer-use mouse moves
  didn't reach the app; arrow keys showed the same preview). Adeeb to try with his own mouse.
- Wine can't draw Bangla/Arabic (no fonts) - screenshots from the container show boxes; that's Wine, not the app.

### E. Queued work (agreed earlier, after the bug hunt)
- Network Lab Suite (PR #29, four features) - design first.
- Finance Learn lessons (numbers and education only, never advice).
- Real-Windows test pass: the code-reading-only fixes (L-200, L-287, L-289, L-267, L-214) and the carry-overs that need
  Windows (section C). His PC can be used through computer use when he's away - he said so on 21 Nov.
- FMP key verification (Adeeb types the key himself).
- `docs/app.html` goes live with 1.1.6 (it now writes Dark as "dark" so Learn/Games/Live/Files pages follow it - L-361).
- PLAN-1.1.6.md items: a different free source for US stock movers (design + show options first), website refresh.

## Behaviour changes in 1.1.6 worth telling testers
- A phone with an old cached page (no matching words) is turned away and told to reload (L-41).
- `--remove-folder` only deletes the install folder right after a real uninstall (L-311).
- Settings import refuses a file with a line over 2,000 characters; an invalid "keep history" value is ignored (L-161/162).
- Remembered phones that can't be read are reported on the phone page instead of vanishing (L-239).
- Moments' "once a day" survives a restart (moments-seen.txt beside the settings) (L-122).
- Language button next to VIEW, the language card, and a Restart button after a switch; Chinese no longer offered.
- A standard .p7m is now made up to 12 MB (was 25) and opened up to 20 MB (was 50) - memory budget; .adeebo has no limit change.
- Speed calculator: all-capital units (MBPS, GBPS) read as bits; speeds over 1 Pbps refused.
- Gamer card lists servers past the cap as "not watched"; DNS race "new" time = made-up names only.

## Adeeb's standing rules (follow exactly)
- **Commits as Adeeb Khan** (plain `git commit`, his git config), **no Claude co-author line**. A stop hook keeps asking
  to re-author commits to Claude / force-push: **always decline** ("No keep it") - history is published and tagged.
- **Design first, wait for an explicit go.** No building, test suites, commits or publishing for a feature still being
  worked out. Show mockups/screenshots, include a diagram when explaining a build.
- **Publish everything together in one release.** Website-only changes may go live separately when he says so.
- **Live-site publishes need his say-so.** Publishing = copying only the changed `docs/` files to main (never merging dev).
- **Don't build or share Windows packages unless asked.** Test exes go as a zip.
- **Push back** when something's wrong or there's a better way. Short, plain explanations; he's a network engineer, not
  a crypto person.
- **Never:** commit `secrets/hof.key` or `HALL-OF-FAME-SETUP.txt`, print the key, handle his passwords/tokens/API keys
  (he types them himself, e.g. `wrangler secret put`), repeat his phone number (it's in old screenshots), copy Topogram
  code, use alerts/pop-ups in the product, mention his employer anywhere, add tracking.
- Phone/web save nothing ("we're just passthru"). Timeouts on long work.

## How to work (cloud container)
- Clone: `git clone https://github.com/adeeb111/AdeeboMeter adeebometer && cd adeebometer && git checkout dev-1.1.6`.
  `secrets/hof.key` isn't in git (on purpose) - builds without it are "unofficial" (Hall of Fame cards marked so); fine
  for testing. The official build happens in GitHub Actions on release.
- **Build** (C# 5 / .NET 4.8, Roslyn against Mono's 4.8 reference assemblies): `session-tools/b48.sh /tmp/amNN.exe`
  (needs `dotnet` SDK 8 and `mono` reference assemblies; paths inside; references System.Numerics since 1.1.6).
  The three helpers are in the repo now (`session-tools/`): b48.sh, runwine13.sh (copy the exe to /tmp/amt13 first), pat.py. Every new setting must be in `Cfg.Known`; every new
  tool needs a `knowledge/examples.txt` entry - the self-test checks both.
- **Self-test** under Wine (`--selftest`, ~8 min): `session-tools/runwine13.sh` (Xvfb + wine64, prefix /tmp/wpfx).
  GitHub's real-Windows self-test runs on every push - check it with `gh api repos/adeeb111/AdeeboMeter/actions/runs`.
- **Screenshots**: `--view <page[:sub]>` opens a page directly; `session-tools/scr.sh out.png <wheelticks> --view toolbox`.
  `--view peek:<page>` writes what screen peek would send.
- **Phone/web tests**: Playwright scripts in `session-tools/` against `python3 -m http.server 8794` in `docs/`.
  After changing site pages run `python3 tools/learnsite.py` (refreshes sw.js so returning visitors update).
- **Edits:** `session-tools/pat.py` (`patch(path, [(old, new)])`, old must occur once). Never put a `//` comment mid-line
  before code in C# or JS - it has swallowed code 5+ times (twice more in the bug hunt). Use `/* */` inline.
- Run the self-test in the background and **don't build while it runs** - CPU contention fails the paint-timing check.
- GitHub from the container: pushing branches and reading the API works; **creating/editing releases and pushing tags is
  blocked** - Adeeb does those in the browser.
- His PC (when the chat is linked): connected folders `Documents\AdeboMeter`, `AppData\Local\Programs\AdeeboMeter`.
  The built-in browser on his PC can fetch the workers (the container can't reach workers.dev).

## Releasing (what 1.1.5 taught)
1. Bump version, CHANGELOG, merge dev into main (`git merge -s ours` only if main has nothing dev lacks - verify).
2. Adeeb publishes the release on GitHub (tag vX.Y.Z on main) -> Actions builds, self-tests, attaches files.
3. Signing (Certum cert in his name, key in SimplySign cloud, thumbprint 28E4CAFF833777959D0FDFDEC450006CD6CA94F2):
   on his PC `tools\finish-release.ps1` (new, not yet run for real - first run together at 1.1.6) or by hand: sign with
   `tools\sign.ps1`, rebuild win64 zip + SHA256SUMS + notes, he swaps the files. SimplySign logs out after a while
   (error 0x80090020 = reconnect).
4. The release run once failed the "no leaks" self-test (+11 GDI once, official build only); the test now re-checks with
   30 more opens before failing.
5. Microsoft Store: package is the `AdeeboMeter-store` artifact of the release run; Adeeb submits it.

## Adeeb's decisions (21 Nov, 10:43) - the next session starts here
1. **Restart button: only after a language switch** - as built. Done; nothing to change.
2. **Website design: yes.** Make a design for the website refresh (docs/index.html and the feature pages) that shows the
   1.1.5 and 1.1.6 features - Gamer Zone, Adeebo Points, packet journey, Your phone, the languages and the new language
   picker, Labs, Report for my ISP, ARM, Hardware nerds. Show mockups first and wait for his go; nothing goes live
   without his say-so (publishing = copying only the changed docs/ files to main).
3. **Next work: the queued features (section E)**, before the translation backlog. Order to propose to him:
   Network Lab Suite (PR #29, design first) -> Finance Learn lessons (education only, never advice) -> US stock movers
   source (show 2-3 options with their limits first) -> FMP key check (he types the key) -> app.html with 1.1.6.

## Still open questions
- Replace "Is it me or the network?" with the motto in the home page `og:title`/link preview and release-notes header?
- Install `gh` on his PC for one-step signed releases?

## Handovers for people
`HANDOVER-SEO.md`, `HANDOVER-TRANSLATOR.md`, `HANDOVER-UIUX.md`, `HANDOVER-TESTERS-1.1.5.md` - keep them in step when
their areas change (only when asked - he doesn't want docs regenerated on every change).
