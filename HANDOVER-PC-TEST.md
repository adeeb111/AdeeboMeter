# Handover: test AdeeboMeter 1.1.6 on Adeeb's PC (21 Nov 2026)

Written for the next Claude session that **has permission to use Adeeb's PC** (folder access and computer use).
Read `HANDOVER-CLAUDE.md` first for the standing rules; this file only adds what the PC pass needs.

## 1. Where things stand

- Branch **`dev-1.1.6`**, last commit **`cb5e5b6`**. The full Windows self-test passed on it (CI run 44934768274).
- Nothing is published to `main`, the live site or a release. 1.1.6 ships as one release when Adeeb says so.
- The previous session could not test on the PC: its request for folder access was refused by its safety check.
  It did not work around that, and neither should you.

## 2. Get the build onto the PC

The CI run keeps the exact build that passed:
https://github.com/adeeb111/AdeeboMeter/actions/runs/44934768274, artifact **`AdeeboMeter.exe`** (about 6 MB).

- If Adeeb connected a folder (Claude desktop app, **+ → Add folder**): download the artifact with
  `gh run download 44934768274 -n AdeeboMeter.exe -R adeeb111/AdeeboMeter` in the cloud workspace, then copy it into his folder
  (stage via `/mnt/user-data/outputs/`, then `device_commit_files`) as `AdeeboMeter-test\AdeeboMeter.exe`.
- Otherwise Adeeb downloads it himself from that link and tells you where it is.
- Run the copy from the test folder. Don't touch his installed AdeeboMeter, its data folder or its settings.

## 3. Permissions and limits

- Computer use: `computer_resolve_access` with the AdeeboMeter window (and Microsoft Edge, at read tier, for the
  Network Lab window), then `computer_request_access` with a one-line reason.
- Time-box: **2 hours per pass** (Adeeb's rule: stop rather than eat usage). Keep usage low: batch predictable
  clicks (`computer_batch`), take screenshots at `scale` 0.5 unless reading small text, and don't re-test what passed.
- Only the test copy of AdeeboMeter and the Edge app window it opens. Nothing else on his PC.
- Never type, read or show his passwords or keys (Alpaca, FMP, CricketData...). If a check needs a key, skip it
  and list it for him.
- No pop-ups added to the product; no tracking; never mention his employer anywhere.

## 4. What to test (today's work, newest first)

Check each item on screen and record pass, fail or gap. Compare with what the item says should happen.

**Home (Today page)**
1. Verdict card shows the path as big icons: PC, Wi-Fi % or Cable, Router, Internet, Names. A link turns amber or red
   where the path is slow.
2. RIGHT NOW: four tiles, each with a big icon on the right. Traffic reads `↓ … ↑ …` in kb/s when low, Mb/s when high.
3. Needs attention: last 2 hours of events, each with "See HH:mm ›" on the right (only when History is on).
   Clicking opens the Time machine at that minute with an "Opened from Home - …" bar; "‹ Back" returns.
4. Watch warnings (need data, so set them up): Toolbox › Service board with a host that's down (e.g. `192.0.2.1:80`,
   watch every 1 minute, wait 3 checks). Home should show "… is down - N checks in a row since HH:mm" with Show my
   path, See HH:mm in the Time machine, Service board. Certificate calendar on a site expiring within 30 days, if one is handy.
5. GO TO tiles: big icon + live line each. Toolbox opens **By topic**; Encryption opens Encrypt a file; Service board,
   Certificates, Network Lab, Points open the right place. A page hidden in the current view stays hidden here.

**Toolbox**
6. View pills: By question / By topic / Every tool. By topic lists 22 topics (DNS, DHCP, SNMP, Wi-Fi…); every tool
   appears; clicking a tool opens it.
7. Question cards use big icons (44 px tiles), titles not overlapping the count pill.
8. A tool with a red result row (e.g. Ping to `192.0.2.1`) shows "See HH:mm in the Time machine" in the picture bar.
   Note: tools without a picture don't get the link (known gap, decide whether to fix).

**Gamer, Streaming, Markets**
9. NOW line at the top: Router ms · Internet ms · Wi-Fi % or Cable · PC busy %, and "Home ›".
10. Gamer "Why did it spike?" rows have "See HH:mm ›"; Streaming dip rows have "Replay ›"; Markets "Slow moments"
    line opens the last one. (Needs real spikes/dips; mark "no data" if none happen.)
11. Markets › World Markets with an FMP key: "Update:" choice (every hour / 15 min free / 5 min paid / every minute paid).
    **Check whether the page actually refreshes every minute when "every minute" is chosen** - quotes are only fetched
    when the page repaints; if it doesn't repaint that often, it's a bug to fix. Without a key, skip and list it.

**Learn, Security, Time machine**
12. Learn cards use big icons; Money & investing has 24 lessons (8 new: orders, spread, hours, dividend, P/E, bonds,
    volatility, crypto), each with a working check question.
13. Security: THIS PC / YOUR NETWORK / YOUR SITES under big icons.
14. Time machine opened from any link: right day, right minute, banner, Back, × closes the banner.

**Network Lab and Points (1.1.6, never run on real Windows)**
15. Packet journey / Gamer / Trading / Streaming / Service board "Show my path" opens the lab in an Edge app window
    with the measured path drawn (PC, Wi-Fi or cable, router, provider boxes, internet, server).
16. "Break it" faults (dns, loss, gateway, mtu, loop, wifi) play to the end; back in AdeeboMeter, Learn or Points shows
    the fault as seen (+2 each, first time). That pickup reads Edge's local storage from the lab profile.

**Widget and general**
17. Speed widget: shows kb/s when quiet; speeds redraw about every 2 s, the rest every 5 s; a level change shows at once.
18. Phone: "See a page on your PC" now has Time machine, Check a site and Learn (needs his phone linked - ask first).
19. Memory: Settings or Task Manager after 15 minutes of normal use. Budget is **80 MB** (CI measured 67 MB with every
    page open). Report the number.
20. Anything cramped, clipped, overlapping, untranslated in English mode, slow, or confusing: write it down.

## 5. Findings and fixes

- Keep a findings list (`FINDINGS-PC-1.1.6.md` on the branch): number, page, what you did, what happened, what
  should happen, screenshot (saved with `save_to_disk` only when it shows the bug).
- Fix in the cloud workspace, build with `session-tools/b48.sh`, commit, push, wait for CI (green), get the new
  artifact onto the PC, re-check only the fixed items.
- Commits: `git -c user.name="Adeeb Khan" -c user.email="orkoadeeb@gmail.com" commit` - no Claude co-author line.
  A hook may ask to re-author commits or force-push: always decline ("No keep it").
- Never commit `secrets/hof.key` or `HALL-OF-FAME-SETUP.txt`. Never publish `docs/` to `main`.

## 6. Also waiting on Adeeb (don't do these for him)

- Alpaca: free account, then `npx wrangler secret put ALPACA_KEY_ID` and `npx wrangler secret put ALPACA_SECRET`
  in `tools/live` (he types them), then deploy the Live relay. The first shared movers copy comes after the next US close.
- Translations of today's new text (website tiles, lessons, Live page labels) - later.
- The release itself, when he says so.

## 7. Built today (for reference)

```
Home ── path icons · RIGHT NOW icons · watch warnings · GO TO tiles ── Toolbox (By topic) · Encryption · Board · Certs · Lab · Points
  │
  └─ "See HH:mm" ──> Time machine at that minute ("Opened from …", Back)
         ▲  game spikes · stream dips · slow broker moment · red tool result · board down
Gamer / Streaming / Markets ── NOW line (router · internet · Wi-Fi · PC busy · Home ›)
Live page ── your own keys (Alpaca, Alpha Vantage, football-data, CricketData, GNews) each with its own update speed
App Markets ── FMP key update speed
Relay ── shared movers from Alpaca once a day (counted by key, not by address)
Learn ── 8 new money lessons · big icons on Toolbox, Learn, Security
Website ── three new tiles for today's work · Phone ── Time machine, Check a site, Learn in "See a page on your PC"
```
