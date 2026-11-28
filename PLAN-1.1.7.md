# AdeeboMeter 1.1.7 - plan

Written 24 Nov 2026 (after 1.1.6 shipped). **Work starts Saturday 28 Nov.** Nothing in here is built yet.

Released together as one 1.1.7:

- **A. The look** - dropped 25 Nov: Adeeb, "font looks ok now". Barlow and today's drawing stay.
- **B. The fixes** - the hunters15 "last final check" findings on 1.1.6 (84 new + the leftovers), minus what was
  already fixed after that review.
- **C. Tour mode** for recordings (done).
- **D. New:** Network Doctor, Lab additions (Failure Lab, challenges, Multicast Theater), Flow (a tab in AdeeboShell -
  automation is the push), Cellular Lab, Windows internals and hardware tools (D6), Markets for every country (D7), easier for non-engineers (D8), Send it to my PC for phone visitors (D9), a checkup in any browser (D10).
  Dropped: PCAP Theater, the Topogram look. Parked: Fleet, Remote Help.

```
Saturday ─► B1 blockers (widget Alt+Tab, hover line) ─► D new features ─► B2-B6 fixes ─► tester script on Adeeb's PC ─► 1.1.7
```

Rules that hold all the way: the real app on Adeeb's PC is the only mockup (no browser mockups as targets); no
full-window redraws except the first paint; the penny budget; Windows' own fonts for Arabic and Bengali; nothing
released until the tester script's pictures are approved.

---

## A. The look - dropped (25 Nov)

Adeeb: "font looks ok now, drop that". Barlow and the current drawing stay; no Inter, no 2x wrapper. The Topogram
comparison stays in git history (commit 730f44e) if it comes back.

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
the rest; the order below is the build order after B1.

```
B1 ─► D1 Network Doctor ─► D2 Flow (Shell tab) ─► D3 Lab: Failure Lab + challenges + Multicast
       ─► D4 Cellular Lab ─► D6 Windows + hardware tools ─► D7 Your market ─► D8 easier to use ─► B2-B6
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

### D2. Flow - a tab in AdeeboShell
- **What:** build a check visually - tool blocks, if / else, repeat, wait, run in parallel, compare, report - and see
  the same thing as an AdeeboShell script beside it. Edit either side; the other follows (for what the blocks can say).
- **Engine:** none new. A flow *is* a shell script; the Run button runs it through AdeeboShell, with its fence (only
  `/data` is writable, nothing starts another program, test traffic needs the typed `--yes`).
- **Blocks:** every Toolbox tool (from the same registry), plus variables, conditions, loops, retries, timeouts,
  dry run. Starts with the ready scripts (`network-check`, `wifi-check`, `isp-evidence`...) as editable flows.
- **Drawing:** in the Shell window's own canvas, partial repaints only; keyboard-operable like the rest.
- **Direction (25 Nov):** automation is where AdeeboMeter pushes next - Flow is its first step.

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
- **Sports follows the country too** (Adeeb, 25 Nov): the market's built-in sports lead (on, first on Overview and
  the widget); anyone can still add any league or any sports key from the Sports page, and their own picks stay
  when the country changes.
  | Market | Leads with |
  |---|---|
  | Canada | NHL, NBA, MLS |
  | United States | NFL, NBA, MLB, NHL, MLS |
  | United Kingdom | Premier League, cricket, Champions League |
  | Bangladesh | cricket (internationals, BPL), Premier League |
  | France | Ligue 1, Champions League |
  | Mexico | Liga MX |
  | Spain | LaLiga, Champions League |
  | Saudi Arabia | Saudi Pro League |
  | China | Chinese Super League, CBA |
  | Germany | Bundesliga, Champions League |
  | Japan | J1 League, NPB |
  - Sources as today (NHL public feed, ESPN scoreboards with no key, BBC headlines, football-data.org / CricketData
    with a key); each new league checked on Saturday for a no-key feed - where there's none (likely cricket, CBA,
    NPB), the Sports page asks for that key first instead of showing an empty tab.
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

### D8. Easier for people who aren't network engineers (user review, 25 Nov)
Review: "Too information savvy. Hard to use and understand for non-networking audiences." All four agreed by Adeeb.
1. **Learn starts with "Using AdeeboMeter"** - four lessons first in Learn (app and website): what it does (the five
   dots, the corner widget, green/yellow/red); getting around (sidebar, What's wrong?, Check a destination, Toolbox);
   changing things (language, theme, buddy, widget, quiet hours); when something's wrong (from the dot to the ISP report).
2. **First-run welcome** - four cards on the same four topics, shown once, Skip on every card, each ends with
   "More in Learn > Using AdeeboMeter". Never shown again after Skip or the last card; reachable from Help.
3. **Simple sidebar by default** - new users see Home, What's wrong?, Check a destination, Learn, Settings and
   "More" (Toolbox, Shell, Lab, Gamer, Streaming, Markets, Sports...). Opening More keeps it open; Settings > "Show every
   page" brings back today's sidebar. Existing users keep the sidebar they have (upgrade = everything shown).
4. **Plain words on Home** - each check gets one line saying what it means for you ("Your Wi-Fi is fine - the slowdown
   is your provider"); the numbers are one click deeper. Lines in all five languages.
- Checks: tester-script pictures of the welcome cards and the simple sidebar in every language; self-test that an
  upgrade keeps every page visible and that a fresh install starts simple.

### D9. Website: "Send it to my PC" for phone visitors (SEO review 25 Nov: 37 of 42 visits on phones, mostly from Facebook)
A phone visitor can't install a Windows app; today they meet a Download button that does nothing useful for them, and most
never come back. Website only - can ship before 1.1.7 if Adeeb wants. No server, no email collected, nothing stored.
- **Who sees it:** phones and tablets (coarse pointer + narrow screen, or a mobile user agent). Desktop is unchanged.
- **The swap:** on phones the hero's Download becomes **"Send it to my PC"**; a small "Download anyway" stays under it.
  Same on the feature pages, the guides, the poster card and the What's new window.
- **What "Send it to my PC" offers** (a bottom sheet, one tap each):
  1. **Share** - the phone's own share sheet (send yourself the link by WhatsApp, Messenger, email...).
     Facebook's and Instagram's in-app browsers often have no share sheet: then this button is hidden.
  2. **Email it to me** - opens their own mail app with subject and body filled in (a mailto link); nothing passes
     through us.
  3. **Remind me later** - a calendar reminder file (.ics): "Install AdeeboMeter on your PC", tomorrow 19:00, with
     the link; works offline in every phone calendar.
  4. **Copy the short link** - `adeebometer.com/get`, big and easy to type on the PC. `/get` is a new page that
     starts the download on a PC and on a phone shows this same sheet.
- **While they're on the phone:** "Meanwhile, learn how your internet works" -> Learn (it works well on phones) and
  the Lab demo. No phone-app push: the phone app needs the PC app first.
- **Counting it honestly:** `/get?via=share|mail|ics|copy` - Cloudflare's page counts show how many phone visitors
  came back on a PC, by route. No cookies, no IDs.
- **Five languages**, through the translation table; Arabic sheet mirrored.
- **Checks:** browser tests at 320/390 px in Chrome, Safari and Facebook's in-app browser (share hidden there);
  desktop pages unchanged byte for byte apart from the shared script.

### D10. Website: a checkup in any browser - for phones, Macs, Chromebooks, locked work PCs (Adeeb, 25 Nov)
`adeebometer.com/check` in all five languages. No install, no account, nothing stored; results stay in the browser.
- **Where it lives (Adeeb, 25 Nov):** a fifth card in the home page's "Try it in your browser" row - Learn, Games,
  Live, Files, **Check** - built and styled like those pages (installable the same way, works offline for the parts
  that can). Not a separate product.
- **What a browser can measure** (each in plain words first, numbers one tap deeper):
  - latency and jitter to the nearest edge (the relay's `/ping`, as Live Data already does);
  - download and upload speed;
  - **latency under load** - "will a video call stutter while someone downloads?" (the bufferbloat test);
  - DNS: does it answer, and how fast (DNS-over-HTTPS lookups timed);
  - IPv6: yes or no;
  - what a video stream would get (SD / HD / 4K) from the measured speed.
- **What it can't see, said plainly:** your Wi-Fi signal, your router, your PC. That's the bridge to the next step.
- **Next step by device:**
  - Windows: "Get the full picture" -> download (desktop) or Send it to my PC (D9, on a phone).
  - **Mac (extra):** no Windows-app pitch. Instead, Mac's own tools explained the AdeeboMeter way: hold Option and
    click the Wi-Fi icon (signal, noise, channel - what good looks like); `networkQuality` in Terminal (macOS 12+,
    built in: speed and responsiveness); Wireless Diagnostics for a dropping Wi-Fi. Safari differences handled
    (no connection-type info; the tests use plain fetch timing only).
  - Android / iPhone: where to find Wi-Fi details in Settings, and Send it to my PC.
  - Chromebook / Linux: the same browser results, plus where their Wi-Fi details live.
- **Phone data:** on mobile data it asks first and offers a light test (~5 MB) instead of the full one (~50 MB).
- **Honesty:** the speed is "to the nearest test server", not "your plan"; one run is a snapshot - "run it again
  when it feels slow and compare".
- Learn links from each result ("What is jitter?") - those lessons already exist in five languages.
- Checks: Chrome, Safari (Mac and iPhone), Firefox, Facebook's in-app browser; 320 / 390 / 1280 px; Arabic mirrored.
- A Mac app: Adeeb wants Mac as a focus (Mac users pay better) - planned separately in PLAN-MAC.md, not inside 1.1.7.

### Status (28 Nov, evening) - everything in D is built and pushed on dev-1.1.7
| Item | Commit | What's in | Left / not done |
|---|---|---|---|
| D1 Doctor | earlier | snapshots, 14-day strip, suspect | Windows visual check |
| D2 Flow | 5fbf5098 | blocks <-> script, Run | Windows visual check |
| D3 Lab | 2587ac07 | 8 faults with "you'd notice / caught by", Challenges (8, scored), Multicast theater | Heist stays in the app's Games (listed in Challenges) |
| D4 Cellular | 2587ac07 | attach -> session -> tunnel, 4 ways to break it (in the Lab) | "deep detail behind more" not added |
| D6 PC tools | 895888b9 | DLLs, handles, env/PATH, USB tree + power, PCIe + calculator, SSD life, disk latency, EDID, battery history | process tree, channels, throttling, bottleneck = existing tools reused; USB/EDID need a Windows run |
| D7 Your market | f472dd53 | 11 presets + Custom, picker on the page and in Settings, rates in your currency, holidays (rules + tables), leagues per market, company cards | NFL/NBA/MLB, CBA, NPB, BPL cricket not wired (soccer only via ESPN); Shanghai 2027 holidays not published yet; tester-script pictures |
| D9 Send to PC | 87455017 | phone sheet, /get | test in Facebook's in-app browser on a real phone |
| D10 /check | 87455017 | 6 languages, device next steps, Mac tools | speed.cloudflare.com / dns.google / ipify reachability checked only from a real browser (blocked here) |

### Release notes for the site (28 Nov)
- The four "Using AdeeboMeter" lessons are in the app (knowledge/learn.txt) and in tools/learnsite.py, but the website's
  Learn pages were NOT regenerated: running tools/site.sh or learnsite.py on its own rewrites ~500-670 committed pages
  (Codex's later passes are missing from that output). Reconcile the generators with Codex before the release build,
  then regenerate once.

### Dropped (25 Nov)
- D5 PCAP Theater - dropped by Adeeb. (Flow was dropped and brought back the same morning.)

### D - done when
- Each feature has its own tester-script checks and appears in the tour (`--view tour`).
- Self-test covers the new parsers and the Flow <-> script round trip; deep test feeds them hostile input.
- Memory/CPU: nothing new runs in the background except the Doctor's daily snapshot (seconds, at low priority).
- Five languages, keyboard, touch, both themes.
- HOW-TO-USE, Theory of Everything and the website feature pages updated at release (only then).

## C. Already in 1.1.7 (24 Nov)
- **Tour mode for recordings** (`--view tour`, `src/Ui/FullView.DemoTour.cs`, `tools/demo/Record-Tour.ps1`): walks every
  tool (open, Example inputs, run or show the example, scroll, hold), writes `tour-chapters.csv`; the script records with
  OBS and cuts one clip per tool with ffmpeg. Adeeb has a test build (`AdeeboMeter-tour-test.zip`, 1.1.6 + tour).
  To check on Windows: a full run, the chapter times against the recording, nothing popping over the tour (awards, tips).
- Handovers updated: `HANDOVER-SEO.md`, `HANDOVER-CONTENT.md` (this week's changes, the tour, the channels).

## Decisions (Adeeb, 28 Nov)
- **The goal of 1.1.7 is simplify.** Every new feature lands behind "More" in the simple sidebar (D8); nothing new
  is added to the first screen a new user sees.
1. Widget hover line: agreed - unlocked -> the move line; else why (takeover/hold); else since (what changed); tips only
   when nothing else applies.
2. Pace buttons: hide "every minute / 5 min" unless the key's plan allows it.
3. What's-new tiles: a real widget picture on all five.
4. The relay's `namespace_id`: later.
5. Codex's bug reports (GitHub issues) are fixed alongside the must-fix bugs: #47 shell grep -r - done 28 Nov.

## Done when
- Tester script on Adeeb's PC: all PASS, every page picture approved at 100 % and 150 %, dark and light.
- Self-test baseline unchanged; ci checks, site checks, langcheck 100 %.
- Memory and CPU within the penny budget with the widget alone and with a page open.
