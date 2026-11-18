# AdeeboMeter - handover to the next Claude chat

Written 17 Nov 2026, 19:15 Toronto, at the end of a long chat. Read this whole file first, then `PLAN-1.1.6.md`.
Owner: Adeeb Khan (adeeb111). Repo: github.com/adeeb111/AdeeboMeter. Website: adeebometer.com.

## Where things stand
```
v1.1.5  released 17 Nov, code-signed (tag v1.1.5 = 03703a5, protected - tags can't be moved or deleted)
main    = the live website (GitHub Pages from /docs) + release source; app code on main = 1.1.5
dev-1.1.6  = next version (planning + a few finished fixes); PLAN-1.1.6.md is the to-do list
```
- **Live on the web**: new home top (navy default, "Find out why your internet or PC is slow. And much more.", motto pill),
  phone app (/app.html), Live, Learn (91), Games, Files.
- **Cloudflare workers (Adeeb deploys them with `npx.cmd wrangler deploy` from his PC):**
  - `adeebometer-remote` (phone relay, `tools/remote`) - hibernation + 6 rooms/min per address. Deployed 17 Nov.
  - `adeebometer-live` (Live data, `tools/live`) - key leak fixed (provider text never passed on), stock movers fetched
    once a day on a schedule (21:30 UTC weekdays, retry 23:30) into KV `MOVERS` (id in wrangler.toml). Deployed 17 Nov.
    **Unchecked:** whether the first scheduled fetch (17 Nov 21:30 UTC) stored movers or Alpha Vantage refused it (likely
    because Cloudflare's outgoing addresses are shared). Check `/movers`; if refused -> PLAN-1.1.6 step 2 (another source).
  - `adeebometer-relay` (website/app reports -> GitHub issues).
  - On his PC: `Documents\AdeboMeter\live-relay` (Live) and `...\remote-relay` (relay) - check `name =` in wrangler.toml
    before deploying; he once deployed into the wrong one.
- **Done on dev-1.1.6, not released:** Every tool blink fix (306b9a6), smooth wheel scrolling (3a32a10), handovers,
  tools/langcheck.py, tools/sign.ps1 + tools/finish-release.ps1.

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
  (needs `dotnet` SDK 8 and `mono` reference assemblies; paths inside). Every new setting must be in `Cfg.Known`; every new
  tool needs a `knowledge/examples.txt` entry - the self-test checks both.
- **Self-test** under Wine (`--selftest`, ~8 min): `session-tools/runwine13.sh` (Xvfb + wine64, prefix /tmp/wpfx).
  GitHub's real-Windows self-test runs on every push - check it with `gh api repos/adeeb111/AdeeboMeter/actions/runs`.
- **Screenshots**: `--view <page[:sub]>` opens a page directly; `session-tools/scr.sh out.png <wheelticks> --view toolbox`.
  `--view peek:<page>` writes what screen peek would send.
- **Phone/web tests**: Playwright scripts in `session-tools/` against `python3 -m http.server 8794` in `docs/`.
  After changing site pages run `python3 tools/learnsite.py` (refreshes sw.js so returning visitors update).
- **Edits:** `session-tools/pat.py` (`patch(path, [(old, new)])`, old must occur once). Never put a `//` comment mid-line
  before code in C# (it has swallowed code twice).
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

## Open questions waiting for Adeeb
- Replace "Is it me or the network?" with the motto in the home page `og:title`/link preview and release-notes header?
- Install `gh` on his PC for one-step signed releases?
- Stock movers: result of the first scheduled fetch (see above).
- His company network blocks adeebometer.com as a newly registered domain (~30-90 days, or ask IT / categorisation sites).

## Handovers for people
`HANDOVER-SEO.md`, `HANDOVER-TRANSLATOR.md`, `HANDOVER-UIUX.md`, `HANDOVER-TESTERS-1.1.5.md` - keep them in step when
their areas change (only when asked - he doesn't want docs regenerated on every change).
