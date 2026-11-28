# AdeeboMeter - notes for Claude Code

A Windows network and PC monitor (C# WinForms, .NET Framework 4.8, custom GDI+ painting) plus its website (`docs/`,
GitHub Pages) and phone page. Owner: Adeeb Khan. Start every session by reading `HANDOVER-CLAUDE.md`, then
`PLAN-1.1.7.md`. Work on branch **dev-1.1.7**; `main` is the live website and release source.

## Build and test
**On Windows (Adeeb's PC - the real thing):**
- `build_exe.cmd notest` - builds `AdeeboMeter.exe` with the csc.exe that ships in Windows. No install, no admin.
- `build_exe.cmd` - builds, then runs the self-test (`AdeeboMeter.exe --selftest`, writes `AdeeboMeter-selftest.txt`;
  exit code 0 = all pass). On Windows every check must pass - there is no allowed-failure list here.
- `AdeeboMeter.exe --view <page[:sub]>` opens one page directly (for looking at a change); `--view tour` walks every tool.
- Close the running AdeeboMeter first (the build does `taskkill`); don't build while a self-test runs.

**In a Linux container:**
- `timeout 560 bash session-tools/b48.sh /tmp/am48.exe` (Roslyn + Mono reference assemblies).
- `timeout 300 bash session-tools/runmono.sh /tmp/am48.exe` - ~25 checks always fail under Mono
  (`session-tools/mono-baseline.txt`); only lines under `new:` are real failures. Mono can't show what Windows paints.

Language: **C# 5** - no `$"..."`, no `?.`, no `out var`, no `=>` properties. Never put a `//` comment mid-line before
code (it has swallowed code many times) - use `/* */`.

Every new setting goes in `Cfg.Known`; every new tool needs a `knowledge/examples.txt` entry - the self-test checks both.

## Translations (French, Spanish, Arabic, Bengali, Chinese)
- App: `knowledge/lang-{fr,es,ar,bn,zh}.txt`, one `english<TAB>translation` line per text, keyed by the exact English
  string in `L.T("...")`. Change an English text -> replace its line in all five files. Keep each file's line endings.
- `python3 tools/langcheck.py <code>` lists what's missing in `lang-<code>-todo.txt` (delete it; never commit it). It only
  sees `L.T("literal")` - texts passed through arrays or variables need adding by hand.
- Tool output (result rows) stays English in every language - that's by design.
- Website: `docs/locale-messages.js`, keyed by the English `data-i18n` text in the page - change both together.

## Website
- **Never run `tools/site.sh` or `tools/learnsite.py`** - they rewrite ~500-670 committed pages and undo Codex's work.
  `tools/site_cache.py` is safe. Edit pages directly; check them with `python3 -m http.server 8765` in `docs/`.
- Sources that generate pages: `tools/shell/*` -> `docs/shell.html`, `tools/lab/lab.src.html` -> `docs/lab.html`.
- Publishing = copying only the changed `docs/` files to main, and only when Adeeb says so. Never merge dev into main
  outside a release.
- Same design in every language; icons are 2D vector, never emoji; no GitHub mentions except legal/about text.

## How Adeeb works (follow these)
- **Design first.** For anything still being worked out: show a mockup or the idea, wait for his go. When explaining
  what was built, include a small diagram.
- **Push back** when something is wrong or there's a better way. Short, plain words - he's a network engineer.
- **One release for everything.** Don't build or share packages/zips unless he asks; test builds go as a zip.
- Don't regenerate README, docs or handovers unless asked. Put timeouts on long work. Don't change repo settings.
- Commit and push to dev-1.1.7 when a piece of work is done and tested. Never rewrite history or force-push.
- The app needs no admin rights anywhere. No Defender exclusions, no keylogger-like scanning, no SendInput replay.
- Corner widget: never takes clicks except in move mode, never steals focus, stays faded. No full-window redraws -
  invalidate only what changed. The app must stay light (the "penny" budget: ~0% CPU with the widget alone, 80 MB).
- **Never:** commit `secrets/hof.key` or `HALL-OF-FAME-SETUP.txt`, print the key, handle his passwords, tokens or API keys
  (he types them himself), mention his employer or its products, add tracking, use pop-up alerts in the product.

## Map of the code
- `src/Program.cs` - entry, `Cfg` (settings), version. `src/Monitor.cs` - the live sampler.
- `src/Ui/FullView*.cs` - the full window, one partial file per page (`Overview` = Home, `Toolbox`, `Guide` = What's
  wrong?, `Doctor`, `Settings`, `Shell` = Terminal, `NetLab` / `LabPage` = Network Lab...). `FullView.cs` has the nav
  (`Secs`, `tabNames`); `FullView.Nav.cs` the side bar.
- `src/Checks/` - checks and reports (`Investigate*.cs`, `IspReport.cs`). `src/WindowsDeep/` - PC internals (`PcReport.cs`).
- `src/Ui/Theme.cs`, `Kit.cs`, `Draw` helpers - colours, fonts, icons. `src/Lang.cs` - `L.T` translation lookup.
- `src/SelfTest.cs` - the self-test (add a check with every new parser or rule).
- Tool result rows: `{lv, name, val, adv, ev[, act]}`; lv -1 = section header. `act`: `tool:id|prefill`, `run:id`,
  `guide:id`, `checkup`, `copy:`, `show:`, or a page id - `FixGo` in `FullView.Overview.cs` routes them.
