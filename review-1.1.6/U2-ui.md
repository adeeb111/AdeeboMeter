# Release check 1.1.6 - slice U2 (UI: FullView.Labs .. FullView.SetHome, 26 files, 7,151 lines)

Every line of the 26 files was read. Callers were traced only where a finding depended on them (Policy, LiveCapture.FindSwitch,
MarketData, AppCtx's FullView lifetime, PillButton, Theme.Round, the lang-*.txt tables). Harness dir:
.../scratchpad/review5/U2-ui/ (ampm.cs: AM/PM designators per culture; of.cs: Application.OpenForms membership).

## Findings

### 1. live_capture = off doesn't stop "Find my switch port", which runs tshark in promiscuous mode for 65 s
Severity: security
Where: src\Ui\FullView.Link.cs line 135-139 (PaintSwitch) and 157-166 (StartSwitch); src\Capture\LiveCapture.cs line 370-379 (FindSwitch)
How to reproduce: put `live_capture = off` in AdeeboMeter.policy. Use a wired adapter on a PC with Wireshark and Npcap installed. Open Your connection and press "Find my switch port".
What happens / what should happen: the button is live (`can = LiveCapture.Available && !switchBusy`), and FindSwitch starts `tshark -i \Device\NPF_{..} -a duration:65 -f "ether proto 0x88cc or ..."` (promiscuous on purpose). No policy check exists on this path. Every other capture path checks Policy.NoCapture: Packets.Capture, LiveCapture.Start, Journey, Gamer and the Connections sniffer. The policy header documents live_capture as covering "Live capture and Find my match server (Wireshark's tshark)", so IT believes no capture can run. This one should be greyed out with "Capturing packets is turned off by your IT team", and FindSwitch should return early like Packets.Capture does.
Confidence: suspected (read it; grep shows no NoCapture in FindSwitch or its UI)

### 2. "Keep it light" alerts repeat every time the full window is opened, and with the currency numbers off they report an old week as "this week"
Severity: wrong result
Where: src\Ui\FullView.Markets.cs line 95-120 (CasualTick: casualMoved, casualFriDone, casualAt are FullView instance fields), line 121 (FxOver); src\Watch\MarketData.cs line 203 (ChangeOver)
How to reproduce: (a) In Markets › Casual Investor, tick "Tell me when a currency on my list moves more than 2% in a week" while a listed currency has moved more than 2% in 7 days. Close the window with Esc and open it again from the widget: the same "The X moved N% against the Canadian dollar this week" alert comes back within a minute, every time. (b) Do the same on a Friday after 17:00 with "A Friday evening summary": it repeats on every open. (c) Turn the Bank of Canada numbers off (or set online_lookups = off) and wait weeks. CasualTick then only Load()s the kept FX file. ChangeOver measures from the series' own last day, so a move from weeks ago is announced as "this week". Friday's MkWeekFacts says "Loonie up 0.4% against the US dollar this week" from the same stale file.
What happens / what should happen: Esc calls Close(), FormClosed disposes the view, and AppCtx builds a new FullView next time, so the "once a week each" memory is lost on every open. The last-alert dates should be kept in Cfg or a static field. A series whose LastDay is older than about 10 days should not produce "this week" sentences, and the note should say "no fresh numbers".
Confidence: suspected (read it; FullView lifetime traced in AppCtx.cs 397-406 and FullView.cs 334-349)

### 3. Learn's search box only matches English, while the list shows translated titles
Severity: wrong result
Where: src\Ui\FullView.Learn.cs line 47, 57, 64 (PaintLearnList: hit() on t["title"], t["tag"], aliases, e.Friendly); src\Ui\FullView.Labs.cs line 53 (PaintLabList); src\Ui\FullView.LearnHome.cs line 166 (PaintGameList)
How to reproduce: Settings › Language › Français (or es, ar, bn). Open Learn and type a word of a topic title exactly as the list shows it, e.g. a word from the French title of "How a name gets found".
What happens / what should happen: nothing matches, because the filter tests the English fields and LearnRow draws L.T(title). Ctrl+K (Search.cs line 28) and the Points page (Points.cs line 192) already match `title + " " + L.T(title)`. Learn should do the same. All five tables claim "#coverage 100", so a non-English user sees translated titles that they can't search for.
Confidence: suspected (read it)

### 4. Ctrl+K "Check <site>" and "Browse every tool" can act on the hidden phone-runner window
Severity: wrong result
Where: src\Ui\FullView.Search.cs line 241-242, 256, 266 (Palette.FindForm<FullView>() = Application.OpenForms.OfType<FullView>().FirstOrDefault()); src\AppCtx.cs line 337 (remoteRunner = new FullView(...); var h = remoteRunner.Handle)
How to reproduce: link a phone from the tray and run any check from the phone. That creates the never-shown runner FullView. Then open the full window (or close it with Esc and reopen it). Press Ctrl+K, type `github.com` and pick "Check github.com".
What happens / what should happen: on .NET Framework a Form joins Application.OpenForms as soon as its handle is created. The runner was created first, so FirstOrDefault returns it. The check then runs, with its network traffic, in the invisible view, and nothing happens in the window the user is looking at. "Browse every tool" (the empty-result row) has the same problem. The palette is created with `pal.Show(this)`, so it should use its Owner (`Owner as FullView`), never a global lookup.
Confidence: suspected (read it; under mono a handle-only form is not in OpenForms (of.cs), so this could not be run here; it depends on .NET Framework adding a form to OpenForms at handle creation)

### 5. Outlook "Coming up" says "Toronto keeps the same hours" on US-only and Canada-only holidays
Severity: wrong result
Where: src\Ui\FullView.Outlook.cs line 151-155 (PaintOutlookBody); src\Watch\MarketData.cs line 267-281 (NextSession skips each venue's own holidays)
How to reproduce: open Outlook (or Home) on Wednesday 25 Nov 2026 after 16:00 ET. Thursday 26 Nov is US Thanksgiving. Any Canadian-only holiday (Victoria Day, Canada Day, Civic Holiday, Thanksgiving in October, Boxing Day) shows the reverse case.
What happens / what should happen: the card reads "Markets open Friday 9:30 AM · New York · Toronto keeps the same hours", but Toronto opens Thursday. On Friday 27 Nov New York also closes at 1 PM. The line should name only New York when Markets.Holiday(day, "ca") differs from "us", or show Toronto's own NextSession.
Confidence: suspected (read it)

### 6. Times are hard-coded as 12-hour "h tt", which is ambiguous (or wrong) in 24-hour locales
Severity: wrong result
Where: src\Ui\FullView.Outlook.cs line 77, 114, 154, 155, 156, 172; src\Ui\FullView.Markets.cs line 382, 403
How to reproduce: Windows region French (France) or German, with History and Weather on. Open Outlook and Markets › World Markets.
What happens / what should happen: these formats force 12-hour clocks. Where the culture's AMDesignator is empty (Windows fr-FR and de-DE), "Rain likely around 4", the "Next 12 hours" strip, the 24-hour bar's axis ("8 12 4 8 12 4") and "Markets open Friday 9:30" lose AM/PM and can't be told apart. Under mono, ar-SA already renders `h tt` as "4 " and `dddd h:mm tt` as " 4:00 " (ampm.cs output). Use "t" and "ddd t", or the culture's ShortTimePattern, as Outlook line 61 and the Sessions desk already do.
Confidence: suspected (ran ampm.cs under mono for 8 cultures; Windows' fr-FR and de-DE designators are from memory, not run)

### 7. Language picker: in Arabic, Left and Right move the wrong way
Severity: cosmetic
Where: src\Ui\FullView.Lang.cs line 111-112 (langSheet.KeyDown)
How to reproduce: pick العربية, open the language card and press Right.
What happens / what should happen: with L.IsRtl the code swaps Left and Right. The tiles are still laid out left to right (line 201, x + (i % cols) * ...), and the footer itself says the window doesn't mirror, so Right moves the focus ring one tile to the left. Drop the RTL swap, or mirror the grid.
Confidence: suspected (read it)

### 8. [day 1] Skip on the first setup question still leaves the "Simple or Pro?" question on Home
Severity: cosmetic
Where: src\Ui\FullView.SetHome.cs line 184 (PaintSetupUses Skip) vs line 148 and 169; src\Ui\FullView.Overview.cs line 291
How to reproduce: fresh install (no settings file). On "What do you use your internet for?" press Skip.
What happens / what should happen: Skip on steps 2-4 sets `Cfg.Mode = "simple"` ("no fifth question after the four (review 24 Nov)"). Skip on step 1 sets only SetupDone, so Mode stays "" and Home immediately shows "HOW WOULD YOU LIKE ADEEBOMETER?", a fifth question right after the user asked to skip. Step 1's Skip should set Mode the same way.
Confidence: suspected (read it)

### 9. Phone page: the "m:ss left" and "This code works for m:ss" countdowns update once a minute
Severity: cosmetic
Where: src\Ui\FullView.Phone.cs line 106 (phoneTick key), 189 and 202 (Mmss)
How to reproduce: link a phone, or show the QR code, and watch the page.
What happens / what should happen: the repaint key holds DateTime.Now.Minute and changes every second only while a question is pending. The seconds shown ("24:37 left") stay frozen for up to 60 s, then jump. Either show whole minutes ("24 min left", as the top-bar pill does) or put the second in the key while Waiting or Paired and the page is visible.
Confidence: suspected (read it)

### 10. Learn › "Labs" card counts 12 labs, but its page lists 13
Severity: cosmetic
Where: src\Ui\FullView.LearnHome.cs line 71 (LearnDone "labs": all = Labs.Length) vs line 119 (LearnItems adds "lab:netlab" AdeeboLab)
How to reproduce: finish all 12 lab challenges without finishing AdeeboLab's Break it faults.
What happens / what should happen: the card's ring shows a full green "✓" while the AdeeboLab tile on that card's page is still undone. Count AdeeboLab in both places, or in neither.
Confidence: suspected (read it)

### 11. Painted text that has a translation in all five tables is still drawn in English
Severity: cosmetic
Where (each is drawn without L.T, or is built by concatenation so it can never match a table line):
- src\Ui\FullView.Replay.cs line 159 and src\Ui\FullView.Paint.cs line 455: day chips "Today" / "Yesterday". Both keys exist in lang-fr/ar; "Now (2 h)" (Paint.cs 454) has no key at all.
- src\Ui\FullView.Labs.cs line 67: "Full screen" (key exists); line 114: the window title; lines 330, 361, 388, 418, 503-506, 558: lab headlines and verdicts ("At most N Mbps per download", "A ping now takes ...", "Call score ...", "BLACK HOLE: ...")
- src\Ui\FullView.Paint.cs line 233 ("READY", "RESULT · ", "CHECKING "), 269 ("Save..."), 389 (" · HISTORY IS OFF, SO ONLY THE LAST 2 HOURS"), 425 (the What changed header), 596 (call rows)
- src\Ui\FullView.Replay.cs line 260 ("not measured" in the sentences), 283-290 (the "plain words" sentences that aren't string.Format keys), 319-328 (row names "Lost replies", "Wi-Fi signal" ...), 339-341 ("day:", "- worse than usual")
- src\Ui\FullView.Overview.cs line 364-365 ("Used today on this network: ", "Last call (")
- src\Ui\FullView.Pins.cs line 36, 47, 65, 92, 120 ("Saved list is full (5)", "Can't pin: ", "Run all N", "CHECKING ALL AT ONCE...", the all-day watch header)
- src\Ui\FullView.Learn.cs line 110 (the "See it live on my PC ..." button labels); src\Ui\FullView.Points.cs line 315 ("Reach 500"), 356-357 (feature titles and tags)
How to reproduce: Settings › Language › Français, Español, العربية or বাংলা. Open Time machine, What changed, Learn › Labs, Check a destination and Home.
What happens / what should happen: English words in the middle of a screen whose table says "#coverage 100". The simple ones (chips, "Full screen", "Save...") only need L.T. The sentences need string.Format keys, as Replay.cs 284-289 already does for three of them. This continues round-4 #116 for the new languages.
Confidence: suspected (read it; checked the keys with grep -P "^\Qkey\E\t" in knowledge/lang-fr.txt and lang-ar.txt)

## Still present (from round 4)

| Round-4 # | Title | Status in my files |
|---|---|---|
| 116 | French: untranslated captions | Still present and now in five languages: see finding 11 for the painted strings in this slice |
| 72 | live_capture = off: Start live capture does nothing and says nothing | Fixed for the Live page (FullView.cs 250-254), but the same policy doesn't cover Find my switch port: finding 1 |

## Checked and held up

- Background work in these files always marshals back with BeginInvoke, guarded by try/catch so a closed window doesn't throw: Labs DNS walk and CGNAT trace, Link RefreshLink, PaintWars, StartSwitch, Learn LearnLive (with a generation counter), NetLab OpenLab (busy flag cleared in finally), Markets fetch callbacks (IsDisposed + IOE/ODE caught), Overview weather/closest server, Procs lookups, Security run, Pins RunAll, Replay load (generation counter; a failed read becomes an empty day, never "Reading..." forever), Report and Hall of Fame card send (cardSending cleared on every path).
- Busy flags: labBusy and labNatBusy are separate (finding 11 of an earlier round); alabBusy is cleared in finally; a failed link read clears linkBusy and shows an error instead of "Asking Windows..." for good.
- Double-click: DB is a Panel with StandardDoubleClick, so the second click of a double-click raises MouseDoubleClick, not another MouseClick. SpeedTest.Measure guards with Interlocked.CompareExchange. Quiz and Check yourself answers lock after the first pick, and a wrong first pick is remembered in points.txt.
- Rule 7 / timers: the lab animation timer (66 ms) stops when neither the page nor its full-screen window is visible. The heart glow runs once, for 3.6 s, and only after 35 min visible. The logo animation lasts 1.2 s. Spark timer: only while sparks exist. Home, Outlook and phone repaint only on key changes; the TCP lab and Home's tiles redraw only their LiveSpot; Markets fetches are throttled in MarketData.Fetch (30 min, 5 min after a first failure) and Load short-circuits once loaded; Trading.LoadFx is once per run; LineHours.ByHour is computed on a thread every 10 min; OlFree once a minute; histDays every 30 s.
- GDI: brushes, pens, paths and fonts in these painters are in using blocks. The lang tile art and fonts are cached and capped (40/60, disposed on reset). The history and replay chart bitmaps are rebuilt only when their key changes and the old one is disposed. PingMap disposes the Region from g.Clip.
- Theme.Round clamps the arc size to at least 1, so the negative widths possible on narrow or 200 % windows (LabAir barW, MkFxDesk sparkline) don't throw.
- Rule 9: the Time machine example day never awards a page point (PageUsed is skipped when replayExample is set), never travels to the widget (AppCtx.Rewind is not set) and can't be pinned (PaintTmPins returns at once when replayExample is set).
- Rule 10: lab challenges, quiz streaks, Check yourself and the page-use points go through EarnPts (once each, nothing with Points off); paths are paid once; the award card refuses empty or mixed-script names.
- Day 1: Home shows only the setup card. With nothing measured, the tiles say "not yet" in grey, not red. Outlook says "History is off" or "(0 / 5)" instead of guessing. Replay offers the example day. Markets shows "Turn on". The Wars card explains its empty state. The lab Mathis card hides "your line" until there are pings. LabAir works with no Wi-Fi. connSnap starts as an empty list, so the NAT lab can't hit a null.
- Markets: NoOnline and TradeFx are checked before every fetch (MarketsFetch, FetchFx, FetchQuotes, Fetch). Index levels need the user's own key. Session times use each exchange's time zone with US and Canadian holidays and half days. The Currencies chart dates its last point ("Bank of Canada, <date>").
- Overview: KindName never returns "", so `kn.Substring(0, 1)` is safe. ProblemHidden parses with InvariantCulture.
- Paint.cs Live page: Phs and ByApp percentages are written and parsed with the same CurrentCulture (LiveCapture.cs 325, 339), so a French decimal comma doesn't throw.
- Security Copy as text: RunOne only returns levels 0-3, so `lab[int.Parse(o[0])]` stays in range.
- Remote catalog and RemoteRun (UI side): secret inputs and the RemoteNo/RemoteNoGroups/Lab/Sender gates are enforced before running, and MTR from the phone is capped at 5 min. The security reviewer covers the rest.
- Memory budget text: every place in 1.1.6 says 80 MB (footer, SelfTest, DeepTest, HOW-TO-USE). The brief's 64 MB is the older figure, not a code inconsistency.
