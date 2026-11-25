# AdeeboMeter 1.1.7 - plan

Written 24 Nov 2026 (after 1.1.6 shipped). **Work starts Saturday 28 Nov.** Nothing in here is built yet.

Two parts, released together as one 1.1.7:

- **A. The look** - Topogram's type and drawing, copied (Inter, Medium weight, drawn at 2x).
- **B. The fixes** - the hunters15 "last final check" findings on 1.1.6 (84 new + the leftovers), minus what was
  already fixed after that review.

```
Saturday ─► B1 blockers (site live now, widget Alt+Tab) ─► A. the look ─► B2-B6 fixes ─► tester script on Adeeb's PC ─► 1.1.7
```

Rules that hold all the way: the real app on Adeeb's PC is the only mockup (no browser mockups as targets); no
full-window redraws except the first paint; the penny budget; Windows' own fonts for Arabic and Bengali; nothing
released until the tester script's pictures are approved.

---

## A. The look - copy Topogram's type and drawing

Read from Topogram beta-2.9.1 (`src/app/paint.py`, `themes.py`, `topogram_app.py`). Its dark/light colours already
are AdeeboMeter's palettes; the difference is type, weight and how text is drawn.

| | Topogram (copy this) | AdeeboMeter 1.1.6 |
|---|---|---|
| Font | Inter (Regular, Medium, SemiBold, Bold - its own .ttf files, private to the process; OFL) | Barlow SemiBold / Bold |
| Body weight | Medium 500; SemiBold 600 for the open item and headings | 600-700 almost everywhere |
| Drawing | drawn at 2x, shrunk with high-quality filtering (only below 145 % scaling) | 1x, grid-fitted to whole pixels |
| Small text (<= 15.5 px) | drawn after the shrink, anti-aliased, **not** grid-fitted | grid-fitted |
| Spacing | GDI+ GenericTypographic string format | default format |
| Corners | 6 px | mixed |
| Sidebar | floating rounded panel (r 18), 13.5 px at 500 (600 open), rows 24-38 px fitting the height, 10.5 px group labels | 17 px bold, rows 43-50 px |

Adeeb's refinement (24 Nov): **the first paint of a page draws the whole page at 2x; after that only the rectangle
that changed is drawn at 2x and shrunk into place.** WinForms already hands each paint its changed rectangle, so:

```
open a page    ─► whole page at 2x ─► shrink ─► the 2x picture is freed (about 16 MB for a split second)
a number ticks ─► only its box at 2x ─► shrink into place (a few KB)
corner widget  ─► stays 1x (tiny, always on, every second)
```

Steps:
1. `Theme.cs`: load Inter from the exe (as Barlow is loaded now); `UiFam` = Inter Medium, `UiSemi` = Inter SemiBold;
   Barlow kept only for the wordmark (and big numbers, if Adeeb wants them).
2. One shared paint wrapper (`Draw.Paint2x(e, paintFn)`): 2x bitmap of the clip rectangle, `ScaleTransform(2)`,
   paint, `DrawImage` back with HighQualityBicubic; skipped at >= 145 % scaling; small text deferred to after the shrink.
3. Put the wrapper into the ~12 custom-painted controls and page classes (not every page separately).
4. Convert the 22 `TextRenderer.DrawText` calls (they ignore scaling) to the GDI+ path.
5. Weights down: body 500, labels 500, only titles and the open item 600; muted grey for second lines.
6. Sidebar: Topogram's measurements. Chips: status stays a pill; IP/app chip grids become plain lists or tables.
7. Memory and CPU check: widget-only CPU near 0 %, page open within budget, GDI handles flat after visiting every page.
8. Tester script: new step that saves **every page** at 100 % and 150 % (dark and light) as drawn on the PC - these
   pictures are the approval, not a mockup.

---

## B. The fixes (hunters15, reviewed at `7640934`)

Already fixed after that review (not to redo): download window falls back to GitHub (now always the zip);
CHANGELOG dated 24 Nov with the last round; website widget pictures; GitHub links on the site (What's new,
license, Source, privacy questions); language links flashing on reload.

### B1 - blockers (first on Saturday)
| ID | What | Where |
|---|---|---|
| SITE-M1 | **Live now:** 18 privacy list items + 4 paragraphs reverted to English in fr/es/ar/bn | `docs/*/privacy.html` - restore the 225c1b9 translations, keep the new layout |
| SITE-M2 | **Live now:** offline copy dropped `/site.js`, `/sitebar.css`, Barlow 700 (bar unstyled offline) | `tools/learnsite.py:244-246` generator + `docs/sw.js` |
| LAB-ML2 | sw.js navigate: a cache-read rejection never calls `done` (hang) | `docs/sw.js:20,22` + generator `learnsite.py:280-281`: `.catch(() => done(OFF()))` |
| W-ML2 | Press on the widget, Alt+Tab, release elsewhere: the full window opens | `AppCtx.cs:196` - open only if the release is over the widget |
| W-CALM-M1 | One hover line hides why/move: the why-line and "Drag to move · Esc" never show | `AppCtx.cs:777-790` - decide the order (see Decisions) |

### B2 - widget
- ML-1 finger tap mid-mouse-drag opens / grabs overwrite each other - refuse a grab while `dragFrom != null`.
- ML-3 Esc while armed-not-dragging is never seen - check Esc in the tick while armed.
- L-1 keep `WidgetDrag`'s ungated `RePressed()` (load-bearing); correct the comment, not the code.
- L-2 `UpLater` posts an up after 10 s while still held, to the window under the cursor now - give up quietly.
- L-3 `WidgetDrop(false)` leaves `touchDrag` set - clear it there.
- Calm ML-2 the 800 ms wait shows no progress and the card is inert - fill the dwell bar in the uncovered case.
- Calm L-1/L-7 hint hairline invisible and 1 px unscaled; 2 px from the weather line.

### B3 - pop-up style
- GameEditor gets `Kit.Polish` (ML-3 / DAY1-ML4).
- Polish strips the company picker's blue washes on first open (ML-1) - polish before `fill()` or skip state buttons.
- Weather `RoundBox` gains a square inner border (ML-4) - don't recurse into `RoundBox`.
- Replay calendar stays system-light (ML-5 / DAY1-L1).
- Dark title bar: try DWMWA 20, then 19 for Windows 10 1809-1909 (ML-6).
- Bold main buttons can clip translations (L-3); Share QR two-tone (L-4); Report links 2.79:1 in dark (L-5);
  two button styles remain (L-6) - pick one.

### B4 - prices and companies
- PX-ML1 Markets page fetches 24/7 - gate `FetchList` on `OpenNow` like the widget.
- PX-ML2 one symbol fetched 2-3 times a round - one "last fetched" per symbol shared by the four callers.
- PX-ML3 two failed symbols abort the round, wrong note - count failures per provider; say "rate limit" on 429.
- PX-ML4 FMP cap breaks the round - `continue`, let other services answer.
- PX-ML5 only FMP's cap tracked; paid paces not checked - per-provider counts (Twelve 800/day + 8/min, Finnhub 60/min).
- PX-ML6 slow FMP poisons the round - skip a failed provider for the rest of the round.
- PX-ML7 Companies desk has no currency - carry `currency` into `Quote.Ccy`, print it.
- PX-ML8 no "as of" on Markets desks - show the oldest quote's time; print `QuoteNote` on Companies.
- PX-ML9 BTC/ETH treated as New York hours - 24/7.
- PX-ML10 search never falls back; keyed users lose the typed-ticker row.
- DAY1-ML1 FMP "Get a free key" goes to docs - point at signup.
- DAY1-ML2 the app still says "Financial Modeling Prep" everywhere; Finnhub-only users see indexes as "-" with no reason.
- DAY1-ML3 20-follow cap undisclosed; both caps silent.
- Lows: PX-L1 BRK.B (dotted tickers), PX-L2 exchange column empty, PX-L3 gold under Crypto, PX-L4 self-tests for the
  three parsers/`Covers`/`Search`, PX-L5 moves over 50 % dropped, DAY1-L2 Arabic chips clipped, DAY1-L3 typed-ticker
  row reads as a sentence, DAY1-L4 1-character search silent.

### B5 - ISP report
- ISP-ML1 loss at the provider's 4th+ hop drawn as not the provider's (drop the `+3` window when owners are known).
- ISP-ML2 the probe chart never shows the hop where loss starts - add that row.
- ISP-ML3 the paste message claims clean evening hours - list the bad hours exactly.
- Carry-over: tier-2 table / ruled-out / message still use "first answering hop" as the home router.
- Lows: ISP-L1 "This PC" stop always green; DAY1-L7 Wi-Fi stop always amber; ISP-L2 "one probe a second" caption;
  ISP-L3 speed test time without a date; ISP-L4 modem ranges too narrow (use the published acceptable ranges).

### B6 - website, tester script, relay
- Site: ML-1 new images not in the offline cache; ML-2 Arabic guide lists use physical left; ML-3 blog vs guide
  five-stop strips disagree; L-1 homepage nav vs site bar names differ (fr/ar/bn); L-2 Bengali meta line in English;
  L-3 Hall of Fame gold 4.28:1; L-4 five unused widget images; L-5/DAY1-L5 four What's-new tiles without visuals;
  L-6 privacy overstates Finnhub/Twelve Data; L-7 `r.html` ALL-CAPS tags; lab.html without the site bar (SITE-L1);
  phone app: DAY1-L8 second hang never gets the notice.
- Tester script: T-ML1 negative tests pass vacuously (assert locked first; fail if the app died); T-ML2 finger tap
  never reaches the tap handler (tiny-move tap, match `open full (tap)`); T-ML3 Esc closing assumed; T-ML4 touch
  injection results ignored; T-ML5 no top-level guard (step 4 can leave AdeeboMeter stopped, no zip); T-ML6 one
  throw skips all live checks; T-L1..L7 (drag position, picture checks, Defender errors, step-4 wording, skipped
  count, "6 minutes", missing sizes file).
- Relay: R-ML1 KV failure after filing returns 500 - duplicate issue on retry; R-ML2 every GitHub failure is one 502 -
  a dead token is retried a week then dropped; R-L1 Hall of Fame fields skip the scrubber; R-L2 clock-bucket limits;
  R-L3 empty reports; R-L4 per-IP quota. **SEC-L2: `namespace_id = "1001"` - Adeeb checks it on Cloudflare.**
- CHANGELOG: name the hunters13 / test4 items still missing (LAB-ML1 remainder).

---

## Decisions for Adeeb (before or on Saturday)
1. Widget hover line: which wins when several apply - why (takeover/hold), since (what changed), move (when unlocked)?
   Suggested: unlocked -> the move line; else why; else since; tips only when nothing else.
2. Big numbers: keep Barlow for them, or Inter everywhere except the wordmark?
3. Pace buttons: hide "every minute / 5 min" unless the key's plan allows it, or keep them with an honest note?
4. What's-new tiles on the home page: give all five a real widget picture, or none?
5. The relay's `namespace_id`.

## Done when
- Tester script on Adeeb's PC: all PASS, every page picture approved at 100 % and 150 %, dark and light.
- Self-test baseline unchanged; ci checks, site checks, langcheck 100 %.
- Memory and CPU within the penny budget with the widget alone and with a page open.
