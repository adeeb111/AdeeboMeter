# AdeeboMeter - handover to the next Claude chat

Written 28 Nov 2026, 10:40 Toronto. Replaces the 21 Nov handover. Read this file first, then `PLAN-1.1.7.md`. `CLAUDE.md` has the short version for Claude Code.
Owner: Adeeb Khan (adeeb111). Repo: github.com/adeeb111/AdeeboMeter. Website: adeebometer.com.

## Where things stand
```
v1.1.6      released (tag v1.1.6). Program.Version still says "1.1.6" - bump at the 1.1.7 release.
main        = the live website (GitHub Pages from /docs) + release source
dev-1.1.7   = next version. Everything below is pushed here. Nothing released, nothing copied to main.
```
The goal of 1.1.7 (Adeeb, 28 Nov): **simplify**. Everything in PLAN-1.1.7 section D is built (status table there).

### What 28 Nov added (in order)
```
 01967282  navigation grouped: Home, Check, My PC, Toolbox, Gamer, Outlook, Streaming, Sports, Markets, History, Learn
 1b2578d6  welcome cards, "Using AdeeboMeter" lessons, plain words on Home
 4d93bab2  Network Doctor (D1)          5fbf5098  Flow tab in the Terminal (D2)
 2587ac07  Lab: Failure Lab, Challenges, Multicast, Cellular (D3, D4)
 895888b9  PC tools: DLLs, handles, env, USB tree, PCIe, SSD life, disk latency, EDID (D6)
 f472dd53  Your market: 11 markets + Custom (D7)
 87455017  website: Send it to my PC (D9), browser checkup /check (D10)
 d1e75b05  Settings > Updates row; Chinese on par with the other languages
 5cc7fcfe  Check my PC report; 4 quick buttons on Home; "Needs attention" removed; checks share one evidence layer
 6862425d  ISP report: short message in the app's language + full English detail for the ISP's network team
 034045bf  plain names, shorter text, fewer doors on Home (below)
 ccf3dab7  website: the same plain names; Mono self-test helper in session-tools/
 baa691be  CLAUDE.md for Claude Code
```

### The "simplify" pass (Adeeb: "people call this app AI slop")
```
Home now:  Verdict
           [Check my PC] [Check my connection] [Check a website] [Report for my ISP]
                              └ runs the Full checkup (its own card is gone)
           What's going on? tiles · Speed · Watch · Week · Outlook
           Also here: Encryption, Service board, Certificates, Points   (was 14 "Go to" tiles)
```
- Names: AdeeboShell -> **Terminal**, AdeeboLab -> **Network Lab**, Adeebo Points -> **Points**, in the app (5 languages)
  and on the website. **Kept:** "Adeebo Award" (the signed Hall of Fame card text is built from it), URLs/filenames
  (`adeeboshell.html`, `adeebolab-guide.html`, `.adeebolab` files) so links and search rankings don't break, and
  `docs/THEORY-OF-EVERYTHING.md` + its PDF (rebuilt only at release).
- Text: 29 of the wordiest descriptions cut to one or two sentences; "in plain words" filler removed. Privacy
  "what it sends" texts left long on purpose (they have to be exact).
- Nothing is hidden: every page is still in the side bar.

### Waiting for Adeeb's answer (ask, don't build)
1. **Simple mode** still hides pages (`SimpleSecs`, and first-run setup picks Simple). He said "everything should be
   open for everyone" - proposed removing the mode. Not done.
2. **Buddy line on tool results** - he asked for it on 25 Nov; the slop list suggested removing it. Kept until he says.
3. **Merge What's wrong? / Investigate / Network Doctor into one page** - a bigger redesign; show a mockup first.
4. Other slop ideas he hasn't picked yet: gimmicks (roast, therapy, letter) into one "Fun" corner; one visual style
   (drop the newspaper skin); website with real screenshots, his own voice, no "232 tools" claims.
5. Remaining connect-plan items: widget red-dot "Why?" button, Flow blocks for the checks, "what changed" in Report for
   my ISP.

## Open items
### Adeeb's steps (his PC)
- Relay `namespace_id` in `tools/relay/wrangler.toml`, then `npx.cmd wrangler deploy` (he decided: later).
- Release 1.1.7 only when he says - everything together. First real run of `tools/finish-release.ps1`.

### Before the release build
- **Website generators:** do NOT run `tools/site.sh` or `tools/learnsite.py` - they rewrite ~500-670 committed pages
  and lose Codex's later passes. Reconcile them with Codex first, then regenerate once (the "Using AdeeboMeter"
  lessons aren't on the website yet because of this). `tools/site_cache.py` is safe.
- Website changes on dev-1.1.7 are not live. Publishing = copying only the changed `docs/` files to main, on his say-so.
- Release-time counts: tools 232, Toolbox sections, Learn topics, self-test count in README/CHANGELOG/site.
- Codex's open PRs #17, #26, #27, #28, #30 - untouched.

### Screens to check (28 Nov) - need a real Windows run
Built from code only; nobody has looked at them on Windows yet. Open each with `AdeeboMeter.exe --view <name>`, check
100 % and 150 % scaling, light and dark, English and one long language (French) plus Arabic (right to left):
```
--view overview            Home: 4 quick buttons, What's going on? tiles, "Also here" (4 tiles), no Full checkup card
--view guide:full          Check my connection = the Full checkup report
--view toolbox:mypc        Check my PC report (verdict first, 7 sections, each row opens its tool)
--view toolbox:ispreport   Report for my ISP: short message in the app language + "For their network team"
--view doctor              Network Doctor: 14-day strip, suspect, snapshot compare
--view shell               Terminal, then click the Flow tab: blocks <-> script
--view lab                 Network Lab: Failure Lab, Challenges, Multicast, Cellular
--view toolbox:usbtree     also toolbox:edid, toolbox:disklat, toolbox:dlls, toolbox:handles
--view trade               Markets with Your market (picker, Custom market)
--view settings:q=Updates  Settings: Updates row (Check now opens the website)
--view points              the renamed Points page
```
Also: the browser checkup's endpoints (speed.cloudflare.com, dns.google, ipify) can only be checked from a real browser.

## Adeeb's standing rules (follow exactly)
- **Design first, wait for an explicit go.** Show mockups/ideas and wait; include a diagram when explaining a build.
- **Push back** when something's wrong or there's a better way. Short, plain words; he's a network engineer.
- **One release for everything.** Don't build or share packages unless he asks; test builds go as a zip.
- **Never re-author his commits** or force-push. Co-author lines on commits: either way is fine (Adeeb, 28 Nov).
- Don't regenerate README/docs/handovers unless asked. Timeouts on long work. Don't change repo settings.
- Don't use his PC screen unless asked. No admin rights needed anywhere. No Defender exclusions, no keylogger-like
  scanning, no SendInput replay. The widget never takes clicks except in move mode. No full-window redraws.
- No GitHub mentions on the website or in the app except legal/about text. Website the same across languages;
  icons are 2D vector, never emoji.
- **Never:** commit `secrets/hof.key` or `HALL-OF-FAME-SETUP.txt`, print the key, handle his passwords/tokens/API keys
  (he types them), repeat his phone number, mention his employer or its products, add tracking, use pop-up alerts.

## How to work (cloud container)
- Clone: `git clone https://github.com/adeeb111/AdeeboMeter adeebometer && cd adeebometer && git checkout dev-1.1.7`.
- **Build** (C# 5: no `$""`, no `?.`, no `out var`): `timeout 560 bash session-tools/b48.sh /tmp/am48.exe`.
- **Self-test, fast (Mono, ~5 min):** `timeout 300 bash session-tools/runmono.sh /tmp/am48.exe`. About 25 checks always
  fail under Mono (`session-tools/mono-baseline.txt`); only lines under `new:` are real. Log:
  `/tmp/stx/AdeeboMeter-selftest.txt`. Full Wine run: `session-tools/runwine13.sh`. Don't build while a test runs.
- **Edits:** `session-tools/ed.py` - `edit(path, [(old, new)])`, all-or-nothing, keeps BOM and CRLF. Never put a `//`
  comment mid-line before code; use `/* */`.
- **Translations:** `knowledge/lang-{fr,es,ar,bn,zh}.txt`, lines `english<TAB>translation` keyed by the exact English
  string - changing an English text means replacing its line in all five files. `python3 tools/langcheck.py <code>`
  writes `lang-<code>-todo.txt` (delete it, never commit it). It only sees `L.T("literal")`; texts passed through
  variables or arrays need adding by hand. Tool output text stays English in every language.
- **Website checks:** `python3 -m http.server 8765` in `docs/`, then Playwright. Website text is translated through
  `docs/locale-messages.js`, keyed by the English `data-i18n` text - change both together.
- Tool rows: `{lv, name, val, adv, ev[, act]}`; lv -1 = section header. `act`: `tool:id|prefill`, `run:id`, `guide:id`,
  `checkup`, `copy:`, `show:`, or a page id - `FixGo` routes them.
- GitHub from the container: pushing branches works; creating releases and pushing tags is blocked (Adeeb does those).

## Releasing
1. Bump version + CHANGELOG, update the counts, regenerate the site once (after the generators are reconciled), merge
   dev into main.
2. Adeeb publishes the release on GitHub (tag on main) -> Actions builds, self-tests, attaches files.
3. Signing on his PC: `tools\finish-release.ps1` (Certum cert in SimplySign; reconnect on error 0x80090020).
4. Microsoft Store: the `AdeeboMeter-store` artifact of the release run; Adeeb submits it.

## Handovers for people
`HANDOVER-SEO.md`, `HANDOVER-TRANSLATOR.md`, `HANDOVER-UIUX.md`, `HANDOVER-CONTENT.md`, `HANDOVER-TESTERS-1.1.5.md` -
not updated today (only when asked). The renames touch the translator and UI/UX ones.
