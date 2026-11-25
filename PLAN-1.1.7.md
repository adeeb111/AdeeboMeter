# AdeeboMeter 1.1.7 - plan

Written 24 Nov 2026 (after 1.1.6 shipped). **Work starts Saturday 28 Nov.** Nothing in here is built yet.

Released together as one 1.1.7:

- **A. The look** - Topogram's type and drawing, copied (Inter, Medium weight, drawn at 2x).
- **B. The fixes** - the hunters15 "last final check" findings on 1.1.6 (84 new + the leftovers), minus what was
  already fixed after that review.
- **C. Tour mode** for recordings (done).
- **D. New:** Network Doctor, Lab additions (Failure Lab, challenges, Multicast Theater), Cellular Lab, Windows
  internals and hardware tools (D6), Markets for every country (D7). Dropped: Flow, PCAP Theater. Parked: Fleet, Remote Help.

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
Done 24 Nov and live (PR #42): SITE-M1 privacy translations (the missing pieces added to the translation table),
SITE-M2 + ML-1 offline site bar, font and pictures (generator fixed), LAB-ML2 sw.js hang, L-6 privacy wording.

| ID | What | Where |
|---|---|---|
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

## D. New in 1.1.7 (Adeeb's picks, 24 Nov)

Built on what exists - no second engine for anything. Each feature ships complete or moves to 1.1.8 without blocking
the rest; the order below is the build order after A and B1.

```
A + B1 ─► D1 Network Doctor ─► D3 Lab: Failure Lab + challenges + Multicast
       ─► D4 Cellular Lab ─► D6 Windows + hardware tools ─► D7 Your market ─► B2-B6
       ─► tester script (+ tour) ─► 1.1.7
```

### D1. Network Doctor (was "AdeeboDoctor") - "it broke after something changed"
- **What:** one page that lines up *what changed on this PC* with *when the network or PC went bad*, and says which
  change is the likely suspect - as a suspect with its evidence, never as a fact.
- **Windows Git** (snapshots): drivers, network adapters and their settings, routes, DNS, firewall rules, services,
  installed apps, Windows updates, proxy/VPN - a small text snapshot once a day and at each network change; a diff
  between any two. Reuses `WinEvidence`, `netsnap`, What changed (`Timeline`), the event log reader.
- **Black Box Recorder:** when a real incident starts (outage, loss, DNS failure, CPU/disk pinned), keep the minute
  before / during / after at full detail (the Time machine already has the minutes; this pins them so they don't roll
  off). Bounded: the last 20 incidents, a few MB.
- **Network DNA** as input: the network's fingerprint (gateway, DNS, Wi-Fi, typical latency) - mostly exists (networks
  told apart by Wi-Fi name / router address); the Doctor shows when the fingerprint itself changed.
- **Shown as:** a timeline - changes as pins, problems as red bands - and "Most likely: Wi-Fi driver updated Tue 18:02,
  drops started 18:05 (seen 4 times since)". Buttons: Compare snapshots, Open in Time machine, Report for my ISP /
  for IT.
- **Rules:** no admin rights (what Windows hides says "not readable here"); everything stays on the PC; history follows
  the History setting.

### D3. AdeeboLab additions (in `tools/lab/lab.src.html` -> `docs/lab.html`, also opened by the app)
- **Failure Lab:** more things to break - DNS, gateway, MTU, firewall rule, duplex, Wi-Fi interference - each with the
  symptom the user would see and the AdeeboMeter tool that would catch it.
- **Challenges:** broken networks to diagnose, scored, with the tools allowed; the existing Heist becomes one of them.
- **Multicast Theater:** IGMP join/leave, PIM join/prune and replication, drawn for IPTV and streaming users.
- Simulations are labelled as simulations everywhere; measured data never mixes in unlabelled.

### D4. Cellular Lab
- LTE and 5G: attach / registration, the bearer, and a packet's journey from phone or modem through the radio, core
  and out to the internet; what breaks where (no signal, no attach, no data, slow). From the Topogram mobile-core
  reference (allowed - both projects are Adeeb's), simplified for everyday users; deep detail behind "more".

### D6. Windows internals and hardware (Adeeb's 20, all in 1.1.7)
No admin rights anywhere: what Windows hides says "not readable here" instead of guessing. Read-only - nothing is
changed, killed or written. Everything stays on the PC.

**Into Network Doctor (evidence pins on its timeline, not separate pages):**
- Driver history (PnP drivers + `setupapi.dev.log`), Windows update correlation (update history vs incidents),
  hardware change history (device arrive/remove in the daily snapshot), startup timeline (boot/logon/service-start
  events, slow-boot culprits), scheduled tasks (which task ran when the problem started).

**New "PC" group in the Toolbox:**
- **Process ancestry explorer:** parent/child tree, start time, which ones use the network (joins per-app usage).
  Other users' command lines are hidden without admin - shown as such.
- **DLL dependency viewer:** static PE import table of a chosen .exe/.dll, missing DLLs in red. Reads the file only.
- **USB topology + USB power budget** (one tool): hubs and ports as a tree, speed per device, power drawn vs the
  hub's budget.
- **Handle usage viewer:** handle/GDI/USER counts per process, top growers over time (leak hunting). Counts only;
  the handle list itself needs admin and is not attempted.
- **Environment variable inspector:** user vs system vs process, PATH split per entry with dead folders flagged;
  also `env` in AdeeboShell.
- **Registry comparison:** a fixed list of network-relevant keys only (TCP/IP, DNS, proxy, Winsock, NIC settings),
  snapshot vs snapshot. No free browsing of the registry.
- **PCIe link check + bandwidth calculator:** current vs max link speed/width per device (GPU or SSD in the wrong slot),
  the calculator for "what would x4 Gen3 give me".
- **Storage endurance estimator:** real wear and TBW when Windows exposes the reliability counters; otherwise a
  calculator from the drive's rated TBW and this PC's measured writes, labelled as an estimate.
- **Battery wear history:** design vs full-charge capacity over time (from the battery report data), cycles.
- **Memory channel inspector:** sticks, slots, speed, size; "single or dual channel" only as a likely layout from the
  slot labels, never stated as fact.
- **Thermal throttling correlation:** CPU % of max frequency vs load (and temperature where readable) on the Time
  machine graph; "the game lagged at 21:40 while the CPU ran at 62 % speed".
- **Disk latency analyzer:** read/write latency and queue per disk, freezes over 100 ms pinned.
- **Monitor EDID decoder:** maker, model, size, native mode, refresh rates, HDR from the registry EDID.
- **Hardware bottleneck estimator:** built only on the measured evidence above (throttling, disk latency, PCIe link,
  memory channels, network) - "what held this PC back in the last hour", with the numbers. No benchmark-table guesses.

Penny rule: nothing samples while its page is closed, except what the Time machine already keeps.

### D7. Markets for every country - "Your market" (Adeeb, 25 Nov)
Today the page is Canada's: Bank of Canada rates against CAD, the Canadian commodity index, New York + Toronto
holidays, and the free keys price US shares only.
- **Your market:** picked from the Windows region on first run; changeable **on the Markets page itself** (a
  "Market: Canada ▾" button in the page header, a searchable list of countries; the choice redraws the page at once)
  and in Settings. Stored per user; nothing sent anywhere.
- **What follows the choice:** currency rates against your own currency (European Central Bank reference rates, no
  key, ~30 currencies; Bank of Canada stays for Canada); your exchange's index first, then the world list; your
  exchange's hours and holidays (holiday tables for the ~10 biggest exchanges, others say "holidays not checked");
  commodities - Canada keeps its index, everyone else gets gold, oil and gas from the futures list; the corner
  widget's default symbols and the "loonie" lines become your currency's.
- **Honest limits:** a currency or exchange the free services don't cover (e.g. the taka, Dhaka shares) says so in one
  line - "not on the free price services" - instead of dashes that look broken.
- **Your companies:** each company a small card - price and change in its own currency, open/closed dot, day range,
  a one-month line where the key gives one. Fixes the wide row with the empty middle.
- **Built in: 11 markets - one per app language plus the big economies** (Adeeb, 25 Nov). Built in = hand-checked
  presets that work with no key: currency, exchange hours and holidays, index list, default widget symbols.
  | Why | Markets |
  |---|---|
  | English + the current audience (Canada 2.28k, Bangladesh 232, US 224 requests) | Canada, United States, United Kingdom |
  | Bengali | Bangladesh |
  | French | France |
  | Spanish | Mexico, Spain |
  | Arabic | Saudi Arabia |
  | Chinese (when zh ships) | China |
  | Largest economies (IMF Apr 2026 top 5: US, China, Germany, Japan, UK) | Germany, Japan |
  - Rates: ECB reference rates (no key) cover most; the taka and the riyal need one more free, no-key source (find it
    on Saturday; if none, the page says so).
  - Bangladesh leads with the rates people send money on: USD, CAD, GBP, EUR, MYR, SAR against the taka. Dhaka
    (Sun-Thu) and Riyadh (Sun-Thu) hours; local shares there aren't on the free price services - one plain line.
  - Holidays: eleven exchange tables (Toronto, New York, London, Dhaka, Paris, Mexico, Madrid, Riyadh, Shanghai,
    Frankfurt, Tokyo), checked by hand once a year - a release-checklist item.
- **Every other country: "Custom market"**, like the other key-based places (quote keys, sports): pick your currency
  and exchange from a list; rates from the ECB (~30 currencies, no key); prices with your own key (FMP / Finnhub /
  Twelve Data, whichever covers that exchange - the add-key sheet says which); hours from the exchange list;
  holidays say "not checked" unless the key's service gives them. Poland (29 requests) is the first test of this path (PLN, `.WA`
  added to the suffix list).
- Checks: self-test for region -> market mapping and the rate parsers; tester-script pictures for all 11
  built-in markets, and Poland as a Custom market.

### Dropped (25 Nov)
- D2 Flow (AdeeboShell tab) and D5 PCAP Theater - dropped by Adeeb.

### D - done when
- Each feature has its own tester-script checks and appears in the tour (`--view tour`).
- Self-test covers the new parsers; deep test feeds them hostile input.
- Memory/CPU: nothing new runs in the background except the Doctor's daily snapshot (seconds, at low priority).
- Five languages, keyboard, touch, both themes, the 2x drawing from part A.
- HOW-TO-USE, Theory of Everything and the website feature pages updated at release (only then).

## C. Already in 1.1.7 (24 Nov)
- **Tour mode for recordings** (`--view tour`, `src/Ui/FullView.DemoTour.cs`, `tools/demo/Record-Tour.ps1`): walks every
  tool (open, Example inputs, run or show the example, scroll, hold), writes `tour-chapters.csv`; the script records with
  OBS and cuts one clip per tool with ffmpeg. Adeeb has a test build (`AdeeboMeter-tour-test.zip`, 1.1.6 + tour).
  To check on Windows: a full run, the chapter times against the recording, nothing popping over the tour (awards, tips).
- Handovers updated: `HANDOVER-SEO.md`, `HANDOVER-CONTENT.md` (this week's changes, the tour, the channels).

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
