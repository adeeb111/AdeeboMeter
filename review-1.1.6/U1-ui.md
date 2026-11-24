# U1-ui - release check of AdeeboMeter 1.1.6 (999a100), slice U1

Files: src\Ui\AskCard.cs .. src\Ui\FullView.LabPage.cs (28 files, 7,122 lines), every line read.
Harnesses in `scratchpad/review5/U1-ui/`: `ask.cs` (AskCard size), `askfr.cs` (AskCard buttons per language), `meas.cs` (translated labels measured against their fixed boxes), `lt.py` / `lit.py` (every literal in the slice checked against the four language tables). Mono can't load the app's private Barlow fonts, so the harnesses swap in DejaVu Sans (`fonts.conf` aliases Segoe UI). DejaVu runs about 12% wider than Segoe UI, so each width is multiplied by 0.88 before it's compared with its box.

### 1. In any language but English, "Still broken - check again", "Open as script" and "Report saved - show it" copy the diagnosis to the clipboard instead
Severity: wrong result
Where: src\Ui\FullView.Guide.cs line 241-264 (PaintGuide, the result's action buttons)
How to reproduce: Settings › Language › Français (or es/ar/bn), restart, What's wrong? › any problem, wait for the answer, click "Toujours en panne - revérifier" (or "Ouvrir en script", or "Rapport enregistré : l'afficher" in the 6 s after a report)
What happens / what should happen: the label list mixes English keys ("It's fixed", "Learn why", "Open in AdeeboLab") with already-translated ones (`L.T("Still broken - check again")`, `L.T("Open as script")`, `L.T("Report saved - show it")`). The branches then compare English: `a.StartsWith("Still")`, `a == "Open as script"`, `a.StartsWith("Report saved")`. A translated label matches none of them and falls into the final `else`, which copies the diagnosis to the clipboard. The check never runs again, the script never opens, and the saved report never shows. All three translations exist in all four tables (checked). The list should hold English keys only, translated when drawn.
Confidence: suspected (read it; deterministic - the label list and branches traced, translations confirmed in lang-fr/es/ar/bn)

### 2. [day 1] Gamer Zone says red "No connection - check Home first" before the first internet ping, and forever when the internet check is off
Severity: wrong result
Where: src\Ui\FullView.GamerHud.cs line 46-58 (PaintGamerHud)
How to reproduce: first run, open Gamer in the first ~5 s; or Settings/policy with the internet check off, then open Gamer
What happens / what should happen: `snap.Ping` is `NetPing.Last`, which is NaN until the first internet sample (and always NaN with the check off). The panel shows "--" in red and the verdict "NO CONNECTION - check Home first" in red. Before the very first BuildSnap (`new Snap()`), Ping/Jitter/Loss are 0: "0.0 ms" in green and "Ready to queue · GG". 999a100 fixed the same thing on Home's tiles (grey "not yet" when `Ring.Values(6).Length == 0`), but not here. It should show grey "not yet", or "internet check off".
Confidence: suspected (read it; Snap defaults in Model.cs line 141 and BuildSnap line 960 traced)

### 3. [day 1] Home's path strip still says "no answer" under Router and Internet before anything was measured
Severity: cosmetic
Where: src\Ui\FullView.Gateway.cs line 48-50 (PathStrip)
How to reproduce: first run or right after a network switch, open Home within the first seconds (or with the internet check off)
What happens / what should happen: the stops' colour became grey (`LvMs(Ring,…)` returns 3), but the detail line under them is still `msT(gw)` → "no answer". The same commit gave Overview's tiles and the NOW line `"not yet"` (FullView.Overview.cs, Gateway.cs line 190); the strip on Home's verdict card was missed. Should read "not yet", like the others.
Confidence: suspected (read it; compared with the 999a100 diff of Overview.cs)

### 4. Toolbox › Pinned says "press ☆ on any tool" - there is no ☆ anywhere, and no way to pin a tool
Severity: wrong result
Where: src\Ui\FullView.Index.cs line 259 (PaintPinned), SetPin line 27
How to reproduce: Toolbox › Pinned; look for a ☆ on any tool row (Every tool list, a question's list, a tool page)
What happens / what should happen: `SetPin(id, true)` is never called anywhere in src (grep: the only caller is the ✕ that removes a pin). ToolTile draws an icon, a name, a line, the last run and a chevron - no star. "Yours" can only be filled by an imported settings file. Either draw the ☆ (ToolTile / tool page) or remove the sentence and the "Yours" grid.
Confidence: confirmed (grep of SetPin / TbPins / ☆ across src)

### 5. The question card grows taller than the screen: "Open on my phone" for a long result puts OK/Cancel off-screen
Severity: cosmetic
Where: src\Ui\AskCard.cs line 40-46 (constructor sizing); caller src\Ui\FullView.Export.cs line 412 (OpenOnPhone, up to 60 lines plus headings)
How to reproduce: run MTR or a long tool, Open on my phone (Ctrl+Shift+Q) the first time - the "exactly what the link carries" card
What happens / what should happen: the card's height is the measured message, with no cap and no scroll. Harness (`ask.cs`): a 60-line preview at 125% gives ClientSize 650 × 1505, with the buttons at y = 1435 - below a 1080p screen (and 1440p at 125%). The card is borderless, so it can't be resized. Enter and Esc still answer, but a mouse or touch user can't. 999a100 added Draw.FitScreen for other dialogs but not this one. Cap the height to the working area and scroll or trim the message.
Confidence: confirmed (ran it: ask.cs, AskCard constructor via reflection, 125%)

### 6. French: every Yes/No question shows "Yes | Non"
Severity: cosmetic
Where: src\Ui\AskCard.cs line 36; knowledge\lang-fr.txt (no `Yes` line)
How to reproduce: French, delete a key or a tab, the sound test, Settings › import with extra options ("Oui : appliquer aussi. Non : …" says Oui, while the button says Yes)
What happens / what should happen: lang-fr has `No → Non` but no `Yes`. Harness (`askfr.cs`): fr "Yes | Non", es "Sí | No", ar and bn translated. Add `Yes	Oui`. Retry, Abort and Ignore are missing in all four tables too, but no caller uses them now.
Confidence: confirmed (ran it: askfr.cs)

### 7. Arabic: the Connections row's "Sniff" button reads "نتنة" ("stinky / foul")
Severity: cosmetic
Where: knowledge\lang-ar.txt (`Sniff	نتنة`); drawn by src\Ui\Controls.cs line 561 (ConnList row action)
How to reproduce: Arabic, Connections, hover any row
What happens / what should happen: the translation is the adjective for a bad smell, not packet capture. Something like "التقاط" (capture) fits. fr, es and bn keep "Sniff".
Confidence: confirmed (table lookup)

### 8. Calculators are entirely in English in fr/es/ar/bn: form labels, units, chips, picture titles and footers
Severity: cosmetic
Where: src\Ui\FullView.CalcForm.cs line 60-97 (CalcSpecs - drawn via `L.T(f.Label)` line 167), src\Ui\FullView.CalcVisuals.cs (VisCard titles, VFoot texts, legends, e.g. line 67, 103, 126, 132, 141, 166, 174, 182…), CalcForm line 265 ("Couldn't work it out")
How to reproduce: French, Toolbox › Calculators › any (Link budget, RAID, UPS, Is my plan fast enough…)
What happens / what should happen: about 90 form labels ("Layers, top to bottom", "Link MTU", "RAID level", "Transmit power", "Busy at the same time"…) and about 70 picture strings ("THE BATTERY AGAINST THE LOAD", "The numbers only - not advice.", "Each slot carries 8 bits…") are missing from all four tables (checked with lit.py against fr, spot-checked es, ar and bn). The code already calls L.T on most of them, so the tables just need the lines. Some titles are built by concatenation ("RAID " + lvl + " ACROSS " + n + " DISKS", "THE BLOCK /" + bp + …) and can't be translated as they are.
Confidence: confirmed (lit.py over the slice + grep of the four tables)

### 9. Other untranslated text in this slice (visible in all four languages)
Severity: cosmetic
Where / what (each checked missing from lang-fr; most never pass through L.T at all):
- src\Ui\Evidence.cs line 163/174 (Pill): the evidence pills on every Guide result and tool row ("measured", "Windows-reported", "seen in packets", "inferred", "unavailable") are drawn without L.T and aren't in any table.
- src\Ui\FullView.Gamer.cs line 35, 55, 78, 81, 167 (the "How it measures…" footer), 176-197 (FindMatch notes), 255 ("THIS MATCH ·", "LAST MATCH ·", "NOW"), 257 (the session line "min · avg · 95% · jitter · loss · spikes"), 313-314 (card's "Best for you over N days…", "Best right now…"), 318, 325, 339, 378.
- src\Ui\FullView.Insights.cs line 66, 74-81, 86-93 (InfoCard titles, "Today", "This month", "What's normal here"), 116 (week legend).
- src\Ui\FullView.Journey.cs line 91-99 (stop explanations), 144, 149, 155-160 (time parts), 174 (verdict), 208, 225-227 (capture status), 249 ("real speed", "x slower").
- src\Ui\FullView.Gateway.cs line 132-145: Home's Go-to live lines ("232 tools - …", "Lock a file with a code…", "Your hosts and ports, watched.", "N hosts are down now"…).
- src\Ui\Controls.cs line 507 (ConnList headings App/Proto/Remote/State/Seen), 536 ("connection being closed", "owner not reported"), 543 ("name from"), 711 ("Night theme" accessible name).
- src\Ui\FullView.Guide.cs line 110-123 (problem tiles' one-liners "Name, route, door, lock, answer", "Jitter, loss, upload, Wi-Fi, VPN", "Overlaps, proxy, VPN names, MTU", "CPU, memory, disk, heat, drivers"; "Wi-Fi says \"No internet\"", "Email won't send").
- src\Ui\FullView.LabPage.cs line 18-23 ("See my LAN", "My path", "VPN split", "IPTV viewer", "Server owner").
- src\Ui\FullView.Inside.cs line 179-183 (mode intros, passed to Lead2's L.T but not in the tables), 194, 208-218 (card titles and row names).
- src\Ui\FreezeCard.cs line 228: the fallback "Still working - the window is catching up (N s)." (shown whenever the buddy is quiet) is English only.
How to reproduce: Settings › Language › Français, visit each page above
What happens / what should happen: English text in a translated window. Add the lines (and L.T where it's missing).
Confidence: confirmed (lit.py lists 725 literals not in lang-fr; the ones above were triaged by hand as user-visible)

### 10. French and Spanish labels overflow their hand-painted boxes
Severity: cosmetic
Where: src\Ui\FullView.BuddySheet.cs line 77-80 ("Fun lines" before its switch); src\Ui\FullView.Gamer.cs line 316-320 ("Stop monitoring" in 120 px, "Edit servers" in 100 px, right-aligned and clipped); src\Ui\FullView.Buddy.cs line 132 and 137-138 ("Just the facts…" in a 260 px click area, "Changed: …" running into the Undo link fixed at +380 px)
How to reproduce: fr or es, open the companion sheet (⋯ › buddy); Gamer with a watched game; Settings › Companion › Just the facts
What happens / what should happen: widths measured at 125%, ×0.88 for Segoe UI (meas.cs):
- fr "Répliques amusantes" ≈149 px in an 88 px room: the text runs under the switch and past the card's edge. es "Frases divertidas" ≈118 px.
- fr "Modifier les serveurs" ≈146 px in a 125 px box, "Arrêter la surveillance" ≈154 px in 150 px: cut off on the left, with no ellipsis.
- es "Cambios: bromas desactivadas · …" ≈518 px against Undo at 475 px: the two overlap.
- "Just the facts" is ≈340 px (fr) / ≈376 px (es) but clickable on only 325 px.

Measure first, as other parts of the app already do.
Confidence: confirmed (ran it: meas.cs; widths are approximate - DejaVu stands in for Segoe UI)

### 11. Every click on Home's "Hide ▾" leaks a menu window
Severity: slow
Where: src\Ui\FullView.Hide.cs line 103-111 (ShowHideMenu)
How to reproduce: let Home lead with a problem, click Hide ▾ and close the menu by clicking elsewhere, repeat; watch USER objects in Task Manager
What happens / what should happen: `Menus.Make()` makes a new ContextMenuStrip each time, and nothing disposes it. Its window handle, renderer and items stay alive with the window, and its Opened handler also sets a new Region each time it opens. The save menu in the same slice does it right: `m.Closed += delegate { BeginInvoke((Action)m.Dispose); }` (Export.cs line 181). 7 more `Menus.Make()` calls outside this slice deserve the same check.
Confidence: suspected (read it)
Rule: 7

### 12. Opening Gamer Zone can freeze the window on a Steam library that's unplugged or on a network drive
Severity: slow
Where: src\Ui\FullView.Gamer.cs line 71 (PaintGamer → GameFinder.Installed, src\Gamer\Gamer.cs line 164-196)
How to reproduce: Steam with a second library on a USB disk that's unplugged, or on a mapped share that's offline (libraryfolders.vdf lists it); open Gamer (and again every 2 minutes while it repaints)
What happens / what should happen: the paint runs the registry read, the vdf read, `Directory.Exists` on each library and `EnumerateFiles` on the UI thread. A missing UNC or sleeping disk path blocks for the SMB timeout or disk spin-up, and the freeze card appears. The rest of the Gamer data already follows the "never file reads in paint" rule (GameHistory.Week); this scan should run on a pool thread too.
Confidence: suspected (read it)

### 13. The picture-animation timer ticks 20 times a second for as long as the window is open, on every page
Severity: slow
Where: src\Ui\FullView.CalcForm.cs line 299-303 (EnsureAnim), 291-298 (AnimTick); started by src\Ui\FullView.Toolbox.cs line 856 on any tool open
How to reproduce: open any tool once, go to Home (or switch to another app with the full window behind it)
What happens / what should happen: once a tool has opened, the 50 ms timer is never stopped until the window closes. Each tick returns early (`tab == "toolbox" && Form.ActiveForm == this …`), so it doesn't repaint, but it still wakes the UI thread 20×/s on pages with nothing animated. Start it on tool open, and stop it when AnimHolding, off the Toolbox, or inactive.
Confidence: suspected (read it)
Rule: 7

### 14. Inside Windows › "How Windows starts" opens 15 processes on every repaint
Severity: slow
Where: src\Ui\FullView.Inside.cs line 78 and 93-101 (InsideItems → BootWhen)
How to reproduce: Inside Windows › How Windows starts, move the mouse over the list or scroll
What happens / what should happen: InsideItems runs on every paint and calls BootWhen for each of the 15 boot steps. Each call scans treeRows and runs `Process.GetProcessById(pid).StartTime`: a process enumeration plus an OpenProcess, on the UI thread, every paint. Compute the line once in LoadInside, as treeRows is.
Confidence: suspected (read it)
Rule: 7

### 15. The freeze card is sized in pixels but its text in points: clipped and overlapping at 150% and above
Severity: cosmetic
Where: src\Ui\FreezeCard.cs line 259 and 267-281 (CardForm)
How to reproduce: Windows display scale 150% or 200% (the app is system-DPI-aware, Program.cs line 136); make the window freeze for 2 s (e.g. a slow first paint)
What happens / what should happen: the window is 420×64 px with the text rectangles at fixed pixel positions (y=10 and y=28, the message box 24 px tall). The fonts are 8 pt bold and 10 pt, which become 16/20 px at 150% and 21/27 px at 200%. The "ADEEBOMETER" label runs into the message, the message's bottom is cut off, and the English fallback ellipsizes at 150%. Scale the box and positions by DPI (or use pixel-unit fonts like Draw).
Confidence: suspected (read it; arithmetic, not run)

### 16. Bengali: the PDF/HTML report and the freeze card draw in Segoe UI, which the rest of the app avoids for Bengali
Severity: cosmetic
Where: src\Ui\FullView.Case.cs line 240-244 (RFont, every report font), src\Ui\FreezeCard.cs line 259
How to reproduce: Bengali, What's wrong? › a problem › Make a report (PDF); or a 2 s freeze
What happens / what should happen: the app switches to Nirmala UI / Vrinda for bn (Theme.cs line 359-385, "UseLanguageFonts") because GDI+ text in Segoe UI doesn't render Bengali well. The report and the freeze card hard-code "Segoe UI", so the translated headings, finding and footer are likely boxes or fallback glyphs. Use the language font (Draw's family for bn, Segoe UI for ar).
Confidence: suspected (read it; can't render Bengali here)

### 17. Arabic: the question card and most painted text keep a left-to-right base direction
Severity: cosmetic
Where: src\Ui\AskCard.cs line 60-74 (OnPaint: icon left, text left-aligned, no DirectionRightToLeft); likewise Guide, Gamer and BuddySheet painters in this slice. Only Inside.Lead2/Title2 and PillButton set RTL.
How to reproduce: Arabic, any question (Delete this tab?, the sound test, import)
What happens / what should happen: each sentence is laid out with an LTR paragraph direction. A trailing "؟" or ".", numbers and Latin names sit at the wrong end, and the icon and text order isn't mirrored. Kit.cs and Settings already use `L.RunsRtl(text) ? DirectionRightToLeft`. AskCard is the app's every question, so it's the first to fix.
Confidence: suspected (read it)

### 18. Keyboard test: the new PrtSc key turns the verdict amber on Windows 11 and on laptops without it
Severity: wrong result
Where: src\Ui\FullView.Hardware.cs line 387 (KeyRows), 414 (WndProc key-up), 357 (verdict "Not pressed: … that key is dead or sticky")
How to reproduce: Windows 11 with the default "Use the Print screen key to open screen capture" on (or a laptop with no PrtSc key / PrtSc only on Fn), run the keyboard test and press every key
What happens / what should happen: on Win11 the Snipping overlay takes the key, so the test window loses focus and gets no key-up. PrtSc never lights, and the result says "Not pressed: PrtSc - If you pressed one of these and it didn't light, that key is dead or sticky" in amber. Mark PrtSc (like RCtrl and Win) as "may be taken by Windows" and leave it out of the verdict, or explain it.
Confidence: suspected (read it; Win11 behaviour not testable here)

### 19. "Just the facts" clicked twice loses its Undo
Severity: cosmetic
Where: src\Ui\FullView.Buddy.cs line 133 (PaintCompanion)
How to reproduce: Settings › Companion, click "Just the facts…" twice, then Undo
What happens / what should happen: the second click saves the already-plain state into `factsUndo`, so Undo restores "Plain, Details on, face only" - nothing changes back. Keep the first saved state while `factsUndo != null`.
Confidence: suspected (read it)

## Still present (from round 4, in this slice)
| Round-4 # | Status |
|---|---|
| none | No round-4 finding was still present in these files. #109 (GameHistory.Week in paint) is now cached off-thread, and #133 (personal best on a tie) uses a strict `>` and is stored once. #22 (games paying for revealed answers) now goes through Revealed/EarnFirst. #134 is fixed for Gamer (`PageUsed("gamer")` in paint). |

## Checked and held up
- 999a100 itself: FitScreen (shrinks only fixed dialogs, keeps them on the working area); KeyForm Alt+F4, PrtSc via WM_KEYUP, the count limited to keys in KeyRows; the buddy sheet takes focus (WireKeys makes the DB selectable, Esc closes it in FullView.cs line 339); the "not yet" states in Overview and the NOW line.
- Background threads: every Task or pool callback in the slice (ImportLines, RefreshValve, FindMatch, StartGuide, the report thread, week heatmap, LoadInside, MeasureNow, JMeasure, JCapture, LabPageShown, LabExport, HudBackground) touches controls only through BeginInvoke/Invoke and catches the closed-window exception. Each busy flag (valveBusy, findBusy, guideBusy, reportBusy, weekBusy, insideBusy, jPinging, labSavedBusy, hudBgBusy) is cleared on success and on the failed-BeginInvoke path.
- Double clicks: StartGuide/StartCheckup/OpenCase check guideBusy; the report button checks reportBusy; Measure again checks jPinging; PillButton presses once per key press (held flag).
- Close: ReleaseExtras disposes the anim, calc, journey and lab timers and the cached bitmaps; gameTimer is disposed on FormClosed; minimising closes the window unless a tool runs (FullView.cs line 396).
- FreezeCard thread handshake (wanted/CloseAsap/card) holds for Hide-before-handle and Show-after-Hide.
- Calculator pictures: overflow and NaN paths (0 mW → -∞ dBm, 9e9 % growth → ∞) are inside PaintToolVisual0's try/catch - a bad number loses the picture, never the page.
- ConnList: scroll bounds, keyboard Home/End/Page, mouse wheel remainder.
- Assist chips: secret boxes get none; values are filled in English even when the chip shows a translation.
- AskCard: Esc is the safe answer, Enter the focused or primary one, arrows move between buttons; it falls back to MessageBox on any error.
- All 508 `L.T("…")` literals in the slice exist in all four tables (lt.py) - the gaps above are text that skips L.T or is passed in as a variable.
