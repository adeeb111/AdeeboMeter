# PC test pass - AdeeboMeter 1.1.6 (21 Nov 2026)

Build: dev-1.1.6 at 81e0b94 (same source as CI-passed cb5e5b6; the CI artifact couldn't be downloaded from the cloud
workspace - blob storage is blocked - so it was built with session-tools/b48.sh). Test copy:
`Documents\AdeboMeter\AdeeboMeter-test` (portable, its own data). Started with run-test.bat, which closes the installed
1.1.5 first (both use the same single-copy lock); back-to-installed.bat restores it.

Pass 1 stopped after ~15 minutes: the PC dropped off the bridge (asleep or offline) during item 8.

| # | Item | Result |
|---|------|--------|
| 1 | Home verdict card path icons (PC, Wi-Fi 93%, Router, Internet, Names) | pass (path was fine, so no amber/red to see) |
| 2 | RIGHT NOW four tiles with big icons; traffic `↓ 7.4 ↑ 57 kb/s` | pass |
| 3 | Needs attention, "See 16:20 ›" -> Time machine with "Opened from Home" bar, ‹ Back returns (scroll kept) | pass, see F1 |
| 5 | GO TO tiles; Toolbox tile opens By topic | pass, see F2 |
| 6 | Toolbox view pills; By topic grid (DNS 15, DHCP 18, SNMP 21, Wi-Fi 8...) | pass |
| 7 | Question cards: 44 px icon tiles, titles clear of the count pill | pass |
| 8 | Red tool result -> "See HH:mm" in the picture bar | not reached (PC disconnected while TCP ping to 192.0.2.1:80 was being set up) |
| 4, 9-20 | | not reached |

## Findings
- **F1 (minor)** - "See 16:20 ›" opened the Time machine at **16:21**. The event was at 16:20, the app's first recorded
  minute was 16:21, so it snapped to the nearest minute with data. Either show the minute it will open, or say "nearest
  minute". Not fixed - Adeeb to choose.
- **F2 (fixed)** - GO TO "Learn" tile drew an empty icon square. The `book` glyph used compact SVG arc flags (`013`)
  that the path reader doesn't split; written out in full in `Glyphs.cs`. Affects every place the book icon is used.
- **F3 (check with a real mouse)** - several clicks were ignored the first time and worked the second (quick-tip
  "Got it", the "‹ Back" banner link, the "By question" pill). Clicks on tiles and Skip worked first time. Could be
  how computer use sends clicks, or a hover tip window catching the first click. Adeeb to try with his own mouse.

## Still to test (next pass)
4 watch warnings (Service board host 192.0.2.1:80, 1-minute checks) · 8 red result link · 9-11 NOW line, spike/dip/slow
links, FMP update speed (needs his key - skip and list) · 12 Learn money lessons · 13 Security · 14 Time machine banner
× · 15-16 Network Lab in Edge + faults -> Points · 17 widget speeds · 18 phone (ask first) · 19 memory after 15 min
(Home footer showed 49-52 MB of 80 MB in the first minutes) · 20 general.
